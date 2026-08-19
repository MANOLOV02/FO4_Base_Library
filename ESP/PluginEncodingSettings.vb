Imports System.IO
Imports System.Text

''' <summary>Configuracion central de encoding para la E/S de plugins ESP/ESM/ESL. Es un espejo LITERAL del
''' subsistema de encoding de xEdit: cualquier divergencia contra xEdit es un bug, y el detalle con las
''' citas file:line vive en la memoria 20-app-encoding-xedit.
''' <para>Globales: general no traducible = cp1252; traducible = cp1252 hasta que sLanguage lo cambie; VMAD
''' siempre UTF-8; para STRINGS el default primario es UTF-8 y el de fallback cp1252, cada uno con su mapa por
''' idioma (FO4 y SSE solo siembran su idioma en el primario, el fallback siempre lleva el mapa completo).</para>
''' <para>sLanguage sale del INI del juego y del custom, y fija el encoding traducible; los parametros de linea
''' de comando -cp-trans y -cp-general lo pisan.</para>
''' <para>Precedencia por archivo: override a nivel de definicion, despues el encoding del propio archivo
''' segun sea traducible o no, y por ultimo el global correspondiente.</para></summary>
Public Module PluginEncodingSettings

    Private ReadOnly _syncRoot As New Object()
    ' UTF-8 with STRICT decoder (throwOnInvalidBytes). Mirror of Delphi TEncoding.UTF8 whose
    ' GetString raises EEncodingError on malformed bytes — REQUIRED for the STRINGS-sidecar
    ' fallback chain (LocalizedStrings.DecodeWithEncoding: try UTF-8 primary → catch → cp1252
    ' fallback, mirror of TwbLocalizationFile.ReadZString wbLocalization.pas:259-264). With .NET's
    ' default Encoding.UTF8 (replacement fallback) the decoder NEVER throws, the catch is dead
    ' code, and a cp1252 .STRINGS file read as UTF-8 yields U+FFFD mojibake instead of falling
    ' back to cp1252. (Earlier comment claimed the opposite — that was the bug causing the Korean/
    ' Spanish STRINGS mojibake report.)
    '
    ' throwOnInvalidBytes affects the DECODER (read). The ENCODER never throws for valid .NET
    ' strings (UTF-8 encodes every Unicode scalar), so this does NOT reintroduce the mid-save
    ' EncoderFallback problem — that was MBCSEncoding(cp) with ExceptionFallback, fixed separately
    ' (MBCSEncoding now uses Delphi-default replacement).
    Private ReadOnly _utf8 As Encoding = New UTF8Encoding(encoderShouldEmitUTF8Identifier:=False, throwOnInvalidBytes:=True)
    Private ReadOnly _encodingCache As New Dictionary(Of Integer, Encoding)()

    ''' <summary>
    ''' Full language→codepage map, used as the FALLBACK map (wbLEncoding[True]) and, for games
    ''' &lt;= gmEnderal, also the PRIMARY map.
    '''
    ''' First block = LITERAL mirror of wbAddDefaultLEncodingsIfMissing (wbInterface.pas:23665-23686):
    ''' the 19 full language NAMES, same codepages. Do NOT canonicalize/alias these (no "es"→"spanish").
    '''
    ''' Second block = FO4 short language CODES (en/fr/ru/ko…) + Korean. These are the actual
    ''' STRINGS-file suffixes and Fallout4.ini sLanguage values FO4 uses (Fallout4_en.STRINGS,
    ''' _ru, _ko…). xEdit does NOT have these in its full map — this is a deliberate addition so the
    ''' INLINE fallback (DecodeTranslatable) can resolve the right codepage from a short sLanguage
    ''' code. They are direct entries (token→cp), NOT aliases that redirect to another token.
    ''' Korean (ko/kor/korean→949) has no official FO4 localization; fan translations use CP949.
    ''' </summary>
    Private ReadOnly _languageMapFull As New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase) From {
        {"english", 1252},
        {"french", 1252},
        {"polish", 1250},
        {"czech", 1250},
        {"danish", 1252},
        {"finnish", 1252},
        {"german", 1252},
        {"greek", 1253},
        {"italian", 1252},
        {"japanese", 65001},
        {"norwegian", 1252},
        {"portuguese", 1252},
        {"spanish", 1252},
        {"swedish", 1252},
        {"turkish", 1254},
        {"russian", 1251},
        {"chinese", 65001},
        {"hungarian", 1250},
        {"arabic", 1256},
        {"en", 1252},
        {"fr", 1252},
        {"de", 1252},
        {"it", 1252},
        {"es", 1252},
        {"pt", 1252},
        {"pl", 1250},
        {"ru", 1251},
        {"ja", 65001},
        {"zh", 65001},
        {"ko", 949},
        {"kor", 949},
        {"korean", 949}
    }

    ''' <summary>Primary map (mirror of xEdit wbLEncoding[False]). Populated per-game by InitializeForGame.</summary>
    Private _languageMapPrimary As Dictionary(Of String, Integer) = Nothing
    ''' <summary>Fallback map (mirror of xEdit wbLEncoding[True]). Always = _languageMapFull (xeInit.pas:1131).</summary>
    Private ReadOnly _languageMapFallback As Dictionary(Of String, Integer) = _languageMapFull

    Private _general As Encoding = Nothing
    Private _translatable As Encoding = Nothing
    Private _translatableDefaultPrimary As Encoding = Nothing   ' wbLEncodingDefault[False]
    Private _translatableDefaultFallback As Encoding = Nothing  ' wbLEncodingDefault[True]
    ''' <summary>
    ''' Codepage of the current sLanguage (from the full map), used as the INLINE fallback in
    ''' DecodeTranslatable: when the primary (Translatable, usually UTF-8 for FO4) throws on a
    ''' non-UTF-8 inline string, we retry with this. xEdit has NO inline fallback (ToStringNative
    ''' is single-shot, wbInterface.pas:16514); this is a deliberate improvement so inline plugins
    ''' in a legacy codepage (cp1251/CP949/…) read correctly without breaking UTF-8 plugins.
    ''' Set from SetLanguage = GetEncodingForLanguage(sLanguage, True). Default cp1252 pre-SetLanguage.
    ''' </summary>
    Private _translatableInlineFallback As Encoding = Nothing

    ''' <summary>
    ''' Persistent global override for decoding EXTERNAL localized string files
    ''' (.STRINGS/.DLSTRINGS/.ILSTRINGS). Nothing unless OverridePluginEncoding.ini Translatable=
    ''' set it. When non-Nothing, LocalizedStringTable uses this as the primary encoding (with
    ''' _translatableInlineFallback as the fallback) instead of the filename-suffix language map.
    ''' DELIBERATELY separate from _translatable: the SaveEsp dialog combo mutates _translatable
    ''' (a transient write-time choice) via SetTranslatableOverride, and that must NOT leak into
    ''' external STRINGS read-decoding. Only ApplyOverrideIni (persistent, startup) sets this.
    ''' </summary>
    Private _localizationPrimaryOverride As Encoding = Nothing
    Private _initialized As Boolean = False

    ''' <summary>
    ''' Legacy public read-only access to the full language map (for external diagnostics).
    ''' Kept as the merged "fallback" view since that's what's most useful when inspecting.
    ''' </summary>
    Public ReadOnly Property LanguageCodePages As IReadOnlyDictionary(Of String, Integer)
        Get
            Return _languageMapFull
        End Get
    End Property

    ''' <summary>Non-translatable plugin strings. Mirror of xEdit wbEncoding (wbInterface.pas:24295, default cp1252).</summary>
    Public ReadOnly Property General As Encoding
        Get
            EnsureInitialized()
            Return _general
        End Get
    End Property

    ''' <summary>Translatable plugin strings (FULL/SHRT/DESC/etc). Mirror of xEdit wbEncodingTrans.</summary>
    Public ReadOnly Property Translatable As Encoding
        Get
            EnsureInitialized()
            Return _translatable
        End Get
    End Property

    ''' <summary>Default for primary lookup miss. Mirror of xEdit wbLEncodingDefault[False] (wbInterface.pas:24299, always UTF-8).</summary>
    Public ReadOnly Property TranslatableDefaultPrimary As Encoding
        Get
            EnsureInitialized()
            Return _translatableDefaultPrimary
        End Get
    End Property

    ''' <summary>Default for fallback lookup miss. Mirror of xEdit wbLEncodingDefault[True] (wbInterface.pas:24300, always cp1252).</summary>
    Public ReadOnly Property TranslatableDefaultFallback As Encoding
        Get
            EnsureInitialized()
            Return _translatableDefaultFallback
        End Get
    End Property

    ''' <summary>Back-compat name. Same as TranslatableDefaultFallback (cp1252).</summary>
    Public ReadOnly Property TranslatableFallback As Encoding
        Get
            Return TranslatableDefaultFallback
        End Get
    End Property

    ''' <summary>
    ''' Primary encoding override for EXTERNAL localized string files, or Nothing when no explicit
    ''' override is active (the loader then uses its xEdit-faithful filename-suffix encodings). Set by
    ''' OverridePluginEncoding.ini Translatable=. See _localizationPrimaryOverride.
    ''' </summary>
    Public Function TryGetLocalizationPrimaryOverride() As Encoding
        EnsureInitialized()
        SyncLock _syncRoot
            Return _localizationPrimaryOverride
        End SyncLock
    End Function

    ''' <summary>
    ''' Inline/localization fallback codepage retried when the primary throws DecoderFallbackException.
    ''' Derived from sLanguage (SetLanguage) or forced by OverridePluginEncoding.ini TranslatableInlineFallback=.
    ''' Consumed by DecodeTranslatable (inline) and, when TryGetLocalizationPrimaryOverride is active,
    ''' by LocalizedStringTable (external STRINGS). Always non-Nothing after init (cp1252 default).
    ''' </summary>
    Public ReadOnly Property TranslatableInlineFallback As Encoding
        Get
            EnsureInitialized()
            SyncLock _syncRoot
                Return _translatableInlineFallback
            End SyncLock
        End Get
    End Property

    ''' <summary>
    ''' Apply per-game defaults. LITERAL mirror of:
    '''   wbInterface.pas:24295-24310 — global initialization
    '''   xeInit.pas:1118-1131       — game-specific primary/fallback map population
    ''' </summary>
    Public Sub InitializeForGame(game As Config_App.Game_Enum)
        SyncLock _syncRoot
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)

            ' wbInterface.pas:24295-24300 — global init (all games)
            _general = MBCSEncoding(1252)                              ' wbEncoding
            _translatableDefaultPrimary = _utf8                    ' wbLEncodingDefault[False]
            _translatableDefaultFallback = MBCSEncoding(1252)          ' wbLEncodingDefault[True]

            ' xeInit.pas:1118-1129 — populate _languageMapPrimary based on game.
            ' Game_Enum.Fallout4 = FO4 (gmFO4); Game_Enum.Skyrim is treated as SSE elsewhere in
            ' the lib (PluginWriter.vb:50), so it gets the SSE branch here.
            Dim primary As New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)
            Select Case game
                Case Config_App.Game_Enum.Fallout4
                    ' xeInit.pas:1127 — wbAddLEncodingIfMissing('en', '1252', False)
                    primary("en") = 1252
                Case Config_App.Game_Enum.Skyrim
                    ' xeInit.pas:1125 — wbAddLEncodingIfMissing('english', '1252', False)
                    ' (treating Game_Enum.Skyrim as SSE per the rest of the lib's contract)
                    primary("english") = 1252
                Case Else
                    ' xeInit.pas:1120 — wbAddDefaultLEncodingsIfMissing(False): FULL map
                    For Each kvp In _languageMapFull
                        primary(kvp.Key) = kvp.Value
                    Next
            End Select

            ' NOTE: Korean is NOT added to the primary map (that was a fragile patch — CP949 in the
            ' primary never throws, so a Korean plugin that happens to be UTF-8 would be silently
            ' mojibake'd). Instead korean/ko/kor→949 lives in the FULL/fallback map, and the inline
            ' fallback (DecodeTranslatable: try UTF-8 → catch → sLanguage codepage) reads both
            ' CP949 and UTF-8 Korean plugins correctly. Same UTF-8-first + codepage-fallback model
            ' as the .STRINGS sidecar.
            _languageMapPrimary = primary

            ' xEdit's wbInterface init sets wbEncodingTrans := wbEncoding (cp1252), but xeInit.pas:1323
            ' ALWAYS overwrites it with wbEncodingForLanguage(wbLanguage, False) — so the cp1252 value
            ' is never observable in practice. We seed _translatable with the primary default (UTF-8
            ' for FO4) so that any access BEFORE SetLanguage runs gets the correct default rather than
            ' cp1252. SetLanguage (called from app startup) refines it from the INI's sLanguage.
            _translatable = _translatableDefaultPrimary
            ' Inline fallback default = cp1252 until SetLanguage sets it from the sLanguage codepage.
            _translatableInlineFallback = _translatableDefaultFallback

            _initialized = True
        End SyncLock
    End Sub

    ''' <summary>
    ''' Apply language-specific override to Translatable. LITERAL mirror of xeInit.pas:1323:
    '''   wbEncodingTrans := wbEncodingForLanguage(wbLanguage, False)
    ''' With wbLanguage normalized at xeInit.pas:1289 (Trim + ToLower).
    '''
    ''' Uses the PRIMARY map (wbLEncoding[False]) which for FO4 contains only {'en' → 1252}.
    ''' Any other language token (incl. "spanish", "russian", "english") falls through to
    ''' _translatableDefaultPrimary = UTF-8.
    '''
    ''' Mirrors xeInit.pas:1323 which runs UNCONDITIONALLY: an empty/missing sLanguage still goes
    ''' through wbEncodingForLanguage("", False), which fails the Find and returns the primary
    ''' default (UTF-8 for FO4). So an empty language MUST set _translatable = UTF-8, NOT leave it
    ''' on a stale cp1252. (Earlier this early-returned, leaving cp1252 → broke Korean/Chinese
    ''' plugins when the user's INI had no sLanguage entry.)
    ''' </summary>
    Public Sub SetLanguage(language As String)
        EnsureInitialized()

        Dim normalized = NormalizeLanguage(language)

        SyncLock _syncRoot
            ' Mirror of wbEncodingForLanguage(normalized, False) inline. Empty token → Find miss → default.
            Dim cp As Integer = 0
            If normalized <> "" AndAlso _languageMapPrimary IsNot Nothing AndAlso _languageMapPrimary.TryGetValue(normalized, cp) Then
                _translatable = If(cp = 65001, _utf8, MBCSEncoding(cp))
            Else
                _translatable = _translatableDefaultPrimary
            End If

            ' Inline fallback = codepage of this sLanguage from the FULL/fallback map (= wbEncodingForLanguage(normalized, True)).
            ' Used by DecodeTranslatable when the primary throws. For ko → CP949, ru → cp1251, etc.;
            ' unknown token → cp1252 default. Computed inline to avoid re-entrant lock.
            Dim fbCp As Integer = 0
            If normalized <> "" AndAlso _languageMapFallback.TryGetValue(normalized, fbCp) Then
                _translatableInlineFallback = If(fbCp = 65001, _utf8, MBCSEncoding(fbCp))
            Else
                _translatableInlineFallback = _translatableDefaultFallback
            End If
        End SyncLock
    End Sub

    ''' <summary>⭐ Override ACOTADO de la encoding Translatable: la fija y la DEVUELVE al salir del
    ''' <c>Using</c>. Es el que tiene que usar cualquier camino interactivo.
    ''' <para>⛔ EXISTE PORQUE <see cref="SetTranslatableOverride"/> NO SE RESTAURA NUNCA y no hay un solo
    ''' sitio en el árbol que lo devuelva. El diálogo de Save ESP lo llamaba con la elección del combo, que
    ''' es una decisión TRANSITORIA de escritura, y esa elección quedaba de global para el resto de la
    ''' sesión. El daño concreto: guardás el NPC A en cp1251 y después abrís Save para el NPC B en un
    ''' plugin UTF-8 — la opción "Auto" hereda cp1251, el sniff de SNAM/CNAM auto-detecta mal y
    ''' <c>EmitLString</c> escribe cp1251 adentro de un plugin UTF-8. El resultado del segundo guardado
    ''' dependía del primero.</para>
    ''' <para>Un <c>codePageOrName</c> vacío o no reconocido NO cambia nada, pero el scope igual es válido
    ''' (restaura lo mismo que había) — así el llamador no necesita una rama para el caso "Auto".</para></summary>
    Public Function PushTranslatableOverride(codePageOrName As String) As IDisposable
        EnsureInitialized()
        Dim enc = ParseEncoding(codePageOrName)
        Dim previa As Encoding
        SyncLock _syncRoot
            previa = _translatable
            If enc IsNot Nothing Then _translatable = enc
        End SyncLock
        Return New TranslatableOverrideScope(previa)
    End Function

    ''' <summary>Devuelve <c>_translatable</c> a un valor previo. Sólo para <see cref="TranslatableOverrideScope"/>.</summary>
    Friend Sub RestoreTranslatable(previa As Encoding)
        SyncLock _syncRoot
            _translatable = previa
        End SyncLock
    End Sub

    ''' <summary>Manual override for Translatable encoding. Mirror of xeInit.pas -cp-trans / -cp command-line param.
    ''' <para>⛔ NO SE RESTAURA: es el equivalente del parámetro de línea de comandos, o sea una decisión
    ''' de arranque para toda la corrida. Cualquier uso TRANSITORIO (un diálogo, un guardado puntual) tiene
    ''' que ir por <see cref="PushTranslatableOverride"/>, o la elección se filtra a todo lo que venga
    ''' después.</para></summary>
    Public Sub SetTranslatableOverride(codePageOrName As String)
        EnsureInitialized()
        Dim enc = ParseEncoding(codePageOrName)
        If enc Is Nothing Then Return
        SyncLock _syncRoot
            _translatable = enc
        End SyncLock
    End Sub

    ''' <summary>Manual override for General encoding. Mirror of xeInit.pas -cp-general command-line param.</summary>
    Public Sub SetGeneralOverride(codePageOrName As String)
        EnsureInitialized()
        Dim enc = ParseEncoding(codePageOrName)
        If enc Is Nothing Then Return
        SyncLock _syncRoot
            _general = enc
        End SyncLock
    End Sub

    ''' <summary>Manual override for the inline fallback codepage consumed by DecodeTranslatable
    ''' when the primary Translatable encoding throws DecoderFallbackException. Normally derived
    ''' from the sLanguage FULL/fallback map by SetLanguage; this setter lets the OverridePluginEncoding.ini
    ''' force a specific fallback (e.g. CP949 for plugins where some FULL strings are UTF-8 but a
    ''' few legacy strings are CP949).</summary>
    Public Sub SetTranslatableInlineFallbackOverride(codePageOrName As String)
        EnsureInitialized()
        Dim enc = ParseEncoding(codePageOrName)
        If enc Is Nothing Then Return
        SyncLock _syncRoot
            _translatableInlineFallback = enc
        End SyncLock
    End Sub

    ''' <summary>
    ''' Set the persistent localization primary override consumed by external STRINGS decoding.
    ''' Called ONLY from ApplyOverrideIni (Translatable key) — NOT from the transient SaveEsp combo —
    ''' so the write-time encoding choice never leaks into vanilla/mod STRINGS read-decoding.
    ''' </summary>
    Public Sub SetLocalizationPrimaryOverride(codePageOrName As String)
        EnsureInitialized()
        Dim enc = ParseEncoding(codePageOrName)
        If enc Is Nothing Then Return
        SyncLock _syncRoot
            _localizationPrimaryOverride = enc
        End SyncLock
    End Sub

    ''' <summary>Read OverridePluginEncoding.ini from the given directory (typically appdir) and apply
    ''' Translatable / General / TranslatableInlineFallback overrides. File-based mirror of xEdit's
    ''' -cp-trans / -cp-general CLI params, matching the SkipEyebrowsTone.ini convention
    ''' (flat key=value lines, ; or # comments, [sections] ignored, case-insensitive keys).
    ''' Missing file = no-op.</summary>
    Public Sub ApplyOverrideIni(iniDirectory As String)
        If String.IsNullOrEmpty(iniDirectory) Then Return
        Dim iniPath = IO.Path.Combine(iniDirectory, "OverridePluginEncoding.ini")
        If Not IO.File.Exists(iniPath) Then Return
        Try
            For Each rawLine In IO.File.ReadAllLines(iniPath)
                Dim line = rawLine.Trim()
                If line.Length = 0 OrElse line.StartsWith(";") OrElse line.StartsWith("#") OrElse line.StartsWith("[") Then Continue For
                Dim eq = line.IndexOf("="c)
                If eq <= 0 Then Continue For
                Dim key = line.Substring(0, eq).Trim().ToLowerInvariant()
                Dim val = line.Substring(eq + 1).Trim()
                If val = "" Then Continue For
                Select Case key
                    Case "translatable"
                        SetTranslatableOverride(val)
                        SetLocalizationPrimaryOverride(val)
                        Logger.LogLazy(Function() $"[ENCODING-OVERRIDE-INI] Translatable={val} (inline + external STRINGS)")
                    Case "general"
                        SetGeneralOverride(val)
                        Logger.LogLazy(Function() $"[ENCODING-OVERRIDE-INI] General={val}")
                    Case "translatableinlinefallback"
                        SetTranslatableInlineFallbackOverride(val)
                        Logger.LogLazy(Function() $"[ENCODING-OVERRIDE-INI] TranslatableInlineFallback={val}")
                End Select
            Next
        Catch ex As Exception
            Logger.LogLazy(Function() $"[ENCODING-OVERRIDE-INI] read failed: {ex.GetType().Name}: {ex.Message}")
        End Try
    End Sub

    ''' <summary>
    ''' Decode bytes for an inline string subrecord using an explicit per-file encoding override
    ''' (typically from TES4 SNAM `&lt;cp:XXXX&gt;`). Mirror of TwbStringDef.ToStringNative
    ''' (wbInterface.pas:16480-16567) — single-shot decode via bsdGetEncoding precedence
    ''' (wbInterface.pas:23519-23535: per-file beats global). xEdit does NOT chain to a fallback
    ''' encoding for inline strings; on decoder failure it produces a hex+error string. We
    ''' return "" instead (the rest of the parser/UI handles empty strings gracefully).
    ''' </summary>
    Public Function DecodeWithEncoding(data As Byte(), offset As Integer, count As Integer, primary As Encoding) As String
        If data Is Nothing OrElse count <= 0 Then Return ""
        If offset < 0 Then offset = 0
        If offset >= data.Length Then Return ""
        Dim safeCount = Math.Min(count, data.Length - offset)
        If safeCount <= 0 Then Return ""

        If primary Is Nothing Then primary = Translatable
        Try
            Return primary.GetString(data, offset, safeCount)
        Catch ex As DecoderFallbackException
            Return ""
        End Try
    End Function

    ''' <summary>
    ''' Decode bytes for an inline TRANSLATABLE string (FULL/SHRT/ATTX in a non-localized plugin)
    ''' using the global Translatable encoding, WITH an inline fallback to the sLanguage codepage.
    '''
    ''' DIVERGENCE FROM xEdit (deliberate improvement): xEdit's ToStringNative is single-shot
    ''' (wbInterface.pas:16514) — on a non-UTF-8 inline string it dumps hex+error. We instead retry
    ''' with the sLanguage codepage (_translatableInlineFallback), mirroring the .STRINGS sidecar
    ''' fallback. This is SAFE: the fallback only runs inside the Catch, i.e. only when the primary
    ''' ALREADY failed (where xEdit/we would otherwise yield garbage). It never changes a value the
    ''' primary decoded successfully. Covers FO4 plugins whose FULL/etc are in a legacy codepage
    ''' (Korean CP949, Russian cp1251, …) while keeping UTF-8 plugins correct.
    ''' </summary>
    Public Function DecodeTranslatable(data As Byte(), offset As Integer, count As Integer) As String
        If data Is Nothing OrElse count <= 0 Then Return ""
        If offset < 0 Then offset = 0
        If offset >= data.Length Then Return ""
        Dim safeCount = Math.Min(count, data.Length - offset)
        If safeCount <= 0 Then Return ""

        Dim primary = Translatable
        Try
            Return primary.GetString(data, offset, safeCount)
        Catch ex As DecoderFallbackException
            ' Primary (usually UTF-8 for FO4) failed → bytes are not in the primary encoding.
            ' Retry with the sLanguage codepage. Only reached on primary failure, so it can only
            ' improve (or leave unchanged) what would otherwise be unreadable.
            Dim fb = _translatableInlineFallback
            If fb IsNot Nothing AndAlso Not Object.ReferenceEquals(primary, fb) Then
                Try
                    Return fb.GetString(data, offset, safeCount)
                Catch
                End Try
            End If
            Return ""
        End Try
    End Function

    ''' <summary>
    ''' Decode bytes for a NON-translatable inline string subrecord using the General encoding.
    ''' Mirror of TwbStringDef.ToStringNative for fields where dfTranslatable is NOT set:
    ''' bsdGetEncoding (wbInterface.pas:23530-23533) returns wbEncoding (General, cp1252 for FO4),
    ''' not wbEncodingTrans. Used for EDID (wbStringKC cpOverride) and other cpNormal/cpOverride
    ''' string fields. Single-shot, no fallback chain (same as the translatable path).
    ''' </summary>
    Public Function DecodeGeneral(data As Byte(), offset As Integer, count As Integer) As String
        Return DecodeWithEncoding(data, offset, count, General)
    End Function

    ''' <summary>
    ''' Encode string to bytes using Translatable encoding. Mirror of TwbStringDef.FromStringNative
    ''' (wbInterface.pas:16322) — single call to encoding.GetBytes with Delphi-default replacement
    ''' fallback (silent '?' for unencodable chars). UX layer (conflict check in NpcOverrideSaver)
    ''' detects conflicts BEFORE the writer runs, so silent '?' only happens if validation is bypassed.
    ''' Use for cpTranslate fields (FULL/SHRT/DESC/ATTX/combo-FULL).
    ''' </summary>
    Public Function EncodeTranslatable(value As String) As Byte()
        If String.IsNullOrEmpty(value) Then Return Array.Empty(Of Byte)()
        Return Translatable.GetBytes(value)
    End Function

    ''' <summary>
    ''' Encode string to bytes using the General (non-translatable) encoding (cp1252 for FO4).
    ''' Mirror of TwbStringDef.FromStringNative for fields where dfTranslatable is NOT set:
    ''' bsdGetEncoding returns wbEncoding (General), not wbEncodingTrans. Use for cpOverride/cpNormal
    ''' string fields: EDID, ATKE (Attack Event), ATKT (Description), DSTA (Sequence Name),
    ''' DMDL (Model FileName). Delphi-default replacement fallback (silent '?').
    ''' </summary>
    Public Function EncodeGeneral(value As String) As Byte()
        If String.IsNullOrEmpty(value) Then Return Array.Empty(Of Byte)()
        Return General.GetBytes(value)
    End Function

    ''' <summary>Codifica el NOMBRE DE ARCHIVO de un master (TES4.MAST) y REHÚSA si no sobrevive el viaje.
    ''' <para>⛔ Acá vivía el peor caso de "lector ≠ escritor" del dominio: los dos writers usaban
    ''' <c>Encoding.ASCII.GetBytes</c>, que SUSTITUYE por <c>?</c> sin lanzar, mientras el lector decodifica el
    ''' MAST con la General (<see cref="DecodeGeneral"/>, PluginReader.ReadTES4) porque xEdit lo declara
    ''' <c>wbStringForward(MAST,'FileName',0,cpNormal,True)</c> (wbDefinitionsFO4.pas:12475 y el TES4 de
    ''' wbDefinitionsTES5.pas) ⇒ cpNormal ⇒ <c>wbEncoding</c>. Un master <c>Café Mod.esp</c> salía como
    ''' <c>Caf? Mod.esp</c>: un archivo que no existe, así que el motor y el CK rechazan el plugin ENTERO y toda
    ''' referencia a ese mod queda rota.</para>
    ''' <para>El nombre de un master no es texto de presentación: es una CLAVE de búsqueda en disco, así que
    ''' una sustitución silenciosa no degrada, invalida. Por eso, a diferencia de
    ''' <see cref="EncodeTranslatable"/> (que se queda con el fallback de reemplazo de Delphi porque el chequeo
    ''' de conflictos corre antes, aguas arriba), acá el único final aceptable es rehusar con el nombre en el
    ''' mensaje.</para></summary>
    ''' <exception cref="InvalidDataException">El nombre no se puede representar en la codificación General
    ''' vigente, así que escribirlo produciría un MAST que apunta a un archivo inexistente.</exception>
    Public Function EncodeMasterFileName(masterName As String) As Byte()
        If String.IsNullOrEmpty(masterName) Then Return Array.Empty(Of Byte)()
        Dim bytes = General.GetBytes(masterName)
        ' Round-trip: si vuelve distinto es porque hubo sustitución (o una conversión con pérdida).
        If Not String.Equals(General.GetString(bytes), masterName, StringComparison.Ordinal) Then
            Throw New IO.InvalidDataException(
                $"The master plugin name '{masterName}' cannot be written with the current plugin encoding " &
                $"(code page {General.CodePage}), so the saved file would list a master that does not exist and " &
                "the game would refuse to load it. Set the plugin encoding to one that covers this name " &
                "(Setup → plugin encoding, or OverridePluginEncoding.ini), or rename the plugin.")
        End If
        Return bytes
    End Function

    ''' <summary>
    ''' Encode a string that lives INSIDE a VMAD payload (script names, property names, String/
    ''' Array-of-String values). VMAD is the one place xEdit pins the encoding regardless of game or
    ''' language: <c>wbEncodingVMAD := TEncoding.UTF8</c> (wbInterface.pas:24295-24310) — NOT the
    ''' General/cp1252 encoding the rest of the inline strings use. Same for FO4 and Skyrim, so this
    ''' deliberately does not branch on game.
    ''' <para>Emits no BOM and no NUL terminator: VMAD strings are u16-length-prefixed, never
    ''' zero-terminated (wbLenString(..., 2)).</para>
    ''' </summary>
    Public Function EncodeVmad(value As String) As Byte()
        If String.IsNullOrEmpty(value) Then Return Array.Empty(Of Byte)()
        Return _utf8.GetBytes(value)
    End Function

    ''' <summary>
    ''' Test whether the given string can be encoded in the current Translatable encoding without
    ''' loss of characters. Builds a strict-fallback variant of Translatable temporarily and
    ''' attempts encoding — if it throws, returns False (chars would be silently replaced with
    ''' '?'). Use this in the UI BEFORE invoking the writer to warn the user about wrong
    ''' encoding choice.
    ''' </summary>
    Public Function CanEncodeTranslatableStrict(value As String) As Boolean
        If String.IsNullOrEmpty(value) Then Return True
        Dim enc = Translatable
        If enc Is Nothing Then Return True
        ' UTF-8 can encode every Unicode char — fast path.
        If enc.CodePage = 65001 Then Return True
        Try
            Dim strict = Encoding.GetEncoding(enc.CodePage, EncoderFallback.ExceptionFallback, DecoderFallback.ReplacementFallback)
            strict.GetBytes(value)
            Return True
        Catch ex As EncoderFallbackException
            Return False
        Catch
            Return False
        End Try
    End Function

    ''' <summary>
    ''' LITERAL mirror of wbEncodingForLanguage (wbInterface.pas:23688-23695):
    ''' <code>
    '''   Result := wbLEncodingDefault[aFallback];
    '''   if wbLEncoding[aFallback].Find(aLanguage, i) then
    '''     Result := wbLEncoding[aFallback].Objects[i] as TEncoding;
    ''' </code>
    ''' fallback=False → primary map + UTF-8 default.
    ''' fallback=True  → full map + cp1252 default.
    ''' </summary>
    Public Function GetEncodingForLanguage(language As String, fallback As Boolean) As Encoding
        EnsureInitialized()

        Dim defaultEnc = If(fallback, _translatableDefaultFallback, _translatableDefaultPrimary)
        Dim map = If(fallback, _languageMapFallback, _languageMapPrimary)

        Dim normalized = NormalizeLanguage(language)
        If normalized = "" Then Return defaultEnc

        Dim cp As Integer = 0
        If map IsNot Nothing AndAlso map.TryGetValue(normalized, cp) Then
            Return If(cp = 65001, _utf8, MBCSEncoding(cp))
        End If

        Return defaultEnc
    End Function

    ''' <summary>
    ''' Lowercase + trim. Mirror of xEdit xeInit.pas:1297
    '''   s := Trim(ReadString('General', 'sLanguage', '')).ToLower
    ''' Note: xEdit does NOT remove inner whitespace, but BGS language tokens never contain it
    ''' so the .Replace(" ", "") here is a no-op for valid inputs.
    ''' </summary>
    Public Function NormalizeLanguage(language As String) As String
        Dim normalized = If(language, "").Trim().ToLowerInvariant()
        If normalized = "" Then Return ""
        Return normalized.Replace(" ", "")
    End Function

    ''' <summary>
    ''' Parse a TES4 SNAM Description string and extract the per-file translatable encoding tag
    ''' if present. Mirror of wbImplementation.pas:5724-5737 reader:
    ''' <code>
    '''   s := Header.ElementEditValues['SNAM'].ToLower;
    '''   i := Pos('&lt;cp:', s);
    '''   if i &gt; 0 then begin
    '''     s := Copy(s, i, 9);              // exactly 9 chars: &lt;cp:XXXX&gt;
    '''     if (Length(s) = 9) and (s[9] = '&gt;') then begin
    '''       s := Copy(s, 5, 4);            // exactly 4 chars: XXXX
    '''       flEncodingTrans := wbMBCSEncoding(s);
    ''' </code>
    ''' Returns Nothing if the SNAM does not contain a recognizable tag.
    ''' </summary>
    Public Function ParseSnamCpTag(snamValue As String) As Encoding
        If String.IsNullOrEmpty(snamValue) Then Return Nothing

        ' Case-insensitive match — xEdit does ToLower before Pos.
        Dim lower = snamValue.ToLowerInvariant()
        Dim idx = lower.IndexOf("<cp:", StringComparison.Ordinal)
        If idx < 0 Then Return Nothing
        If idx + 9 > lower.Length Then Return Nothing
        ' Must be exactly 9 chars ending in '>'
        If lower(idx + 8) <> ">"c Then Return Nothing
        Dim cpToken = lower.Substring(idx + 4, 4)
        Return ParseEncoding(cpToken)
    End Function

    ''' <summary>
    ''' Build the xEdit SNAM-tag literal that records the current Translatable encoding.
    ''' Mirror of wbImplementation.pas:5724-5737 parser: tag must match `&lt;cp:XXXX&gt;` —
    ''' 9 chars exact, 4-char code page slot. xEdit accepts:
    '''   &lt;cp:utf8&gt;   UTF-8 (no dash, "utf8" exactly 4 chars)
    '''   &lt;cp:1252&gt;   Windows-1252         &lt;cp:1251&gt;   Cyrillic/Russian
    '''   &lt;cp:1250&gt;   Central European     &lt;cp:1253&gt;   Greek
    '''   &lt;cp:1254&gt;   Turkish              &lt;cp:1256&gt;   Arabic
    '''   &lt;cp:0932&gt;   Japanese Shift-JIS   &lt;cp:0936&gt;   Simplified Chinese GBK
    '''   &lt;cp:0950&gt;   Traditional Chinese Big5
    ''' Padding works because xEdit calls StrToInt('0936') (accepts leading zeros).
    '''
    ''' Returns "" when the current Translatable is UTF-8 (FO4 default per xeInit.pas:1122)
    ''' — in that case any FO4-aware reader already defaults to UTF-8.
    '''
    ''' NOTE: xEdit reads this tag but does NOT auto-emit it (user-managed in Description).
    ''' We emit it as a deliberate divergence (mejora documentada): plugins become readable
    ''' in xEdit regardless of the destination user's sLanguage. Does NOT help in-game
    ''' (engine ignores the tag) but improves the xEdit cross-sLanguage round-trip.
    ''' </summary>
    Public Function GetTranslatableSnamCpTag() As String
        Dim enc = Translatable
        If enc Is Nothing Then Return ""
        If enc.CodePage = 65001 Then Return ""
        Dim cp = enc.CodePage
        If cp <= 0 OrElse cp > 9999 Then Return ""
        Return "<cp:" & cp.ToString("D4") & ">"
    End Function

    ''' <summary>
    ''' LITERAL mirror of xEdit's sLanguage resolution (xeInit.pas:1274-1320):
    '''   1. Read sLanguage from wbTheGameIniFileName (Fallout4.ini)
    '''   2. If wbCustomIniFileName (Fallout4Custom.ini) exists and has sLanguage, override.
    ''' xEdit does NOT read Fallout4Prefs.ini for sLanguage. Returns "" if neither has it.
    ''' xEdit applies .ToLower (xeInit.pas:1289); we let SetLanguage's NormalizeLanguage do it.
    ''' </summary>
    Public Function ReadLanguageFromIni() As String
        Try
            ' GAME-AWARE INI location (xEdit wbTheGameIniFileName / wbCustomIniFileName): FO4 reads
            ' My Games\Fallout4\Fallout4[.Custom].ini; SSE reads My Games\Skyrim Special Edition\
            ' Skyrim[.Custom].ini. Reading the FO4 path for a Skyrim session picked the wrong (or a
            ' missing) sLanguage, so a non-English SSE user got the wrong plugin-string codepage.
            ' The VR folder (+ the game-root fallback VR needs) lives in PluginManager.ResolveGameIniPath,
            ' which picks the folder from the configured exe. The ini FILE names are the same in VR.
            Dim isSse = (Config_App.Current IsNot Nothing AndAlso Config_App.Current.Game = Config_App.Game_Enum.Skyrim)
            Dim gameIni = PluginManager.ResolveGameIniPath(If(isSse, "Skyrim.ini", "Fallout4.ini"))
            Dim customIni = PluginManager.ResolveGameIniPath(If(isSse, "SkyrimCustom.ini", "Fallout4Custom.ini"))

            Dim result As String = ReadSLanguageFrom(gameIni)
            Dim customValue As String = ReadSLanguageFrom(customIni)
            If customValue <> "" Then result = customValue  ' xeInit.pas:1319 — custom overrides game
            Return result
        Catch
            Return ""
        End Try
    End Function

    Private Function ReadSLanguageFrom(iniPath As String) As String
        If Not File.Exists(iniPath) Then Return ""
        Try
            For Each rawLine In File.ReadLines(iniPath)
                Dim line = rawLine.Trim()
                If line.StartsWith("sLanguage=", StringComparison.OrdinalIgnoreCase) Then
                    Return line.Substring("sLanguage=".Length).Trim()
                End If
            Next
        Catch
        End Try
        Return ""
    End Function

    Private Sub EnsureInitialized()
        If _initialized Then Return
        ' ⛔ EL JUEGO SALE DE LA CONFIG, NO HARDCODEADO. Estaba fijo en Fallout4: el PRIMERO que tocara
        ' cualquier getter fijaba el mapa de idiomas de FO4 y marcaba `_initialized`, así que en una sesión
        ' de Skyrim el `SetLanguage("english")` posterior fallaba el lookup y caía a UTF-8 — un default
        ' silencioso que decide cómo se DECODIFICA todo el plugin.
        InitializeForGame(Config_App.Current.Game)
    End Sub

    ''' <summary>
    ''' Get a cached .NET Encoding for the given code page. Mirror of wbMBCSEncoding
    ''' (wbInterface.pas:23700-23712): TMBCSEncoding.Create(cp) uses Delphi default fallback
    ''' (replacement '?' for unencodable chars, no exceptions). We do the same — no override
    ''' of EncoderFallback/DecoderFallback.
    ''' </summary>
    Private Function MBCSEncoding(codePage As Integer) As Encoding
        SyncLock _encodingCache
            Dim enc As Encoding = Nothing
            If _encodingCache.TryGetValue(codePage, enc) Then Return enc
            enc = Encoding.GetEncoding(codePage)
            _encodingCache(codePage) = enc
            Return enc
        End SyncLock
    End Function

    ''' <summary>Public wrapper around ParseEncoding for callers (e.g. .cpoverride sidecar files).</summary>
    Public Function ParseEncodingPublic(value As String) As Encoding
        Return ParseEncoding(value)
    End Function

    ''' <summary>
    ''' Mirror of wbMBCSEncoding(string) overload (wbInterface.pas:23714-23729):
    '''   utf-8 / utf8 → TEncoding.UTF8
    '''   windows-XXXX → strip prefix → integer code page
    '''   65001 → TEncoding.UTF8
    '''   else → wbMBCSEncoding(int)
    ''' </summary>
    Private Function ParseEncoding(value As String) As Encoding
        Dim normalized = If(value, "").Trim().ToLowerInvariant()
        If normalized = "" Then Return Nothing
        If normalized = "utf8" OrElse normalized = "utf-8" OrElse normalized = "65001" Then Return _utf8
        If normalized.StartsWith("windows-") Then normalized = normalized.Substring("windows-".Length)

        Dim codePage As Integer
        If Integer.TryParse(normalized, codePage) Then
            If codePage = 65001 Then Return _utf8
            Return MBCSEncoding(codePage)
        End If

        Try
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)
            Return Encoding.GetEncoding(value)
        Catch
            Return Nothing
        End Try
    End Function

End Module

''' <summary>Scope que devuelve la encoding Translatable al salir. Lo crea
''' <see cref="PluginEncodingSettings.PushTranslatableOverride"/>.
''' <para>Vive FUERA del Module a propósito: un Module de VB no puede declarar tipos anidados.</para>
''' <para>Idempotente ante un Dispose doble — un <c>Using</c> anidado con el mismo scope no debe
''' restaurar dos veces.</para></summary>
Friend NotInheritable Class TranslatableOverrideScope
    Implements IDisposable

    Private ReadOnly _previa As System.Text.Encoding
    Private _liberado As Boolean = False

    Friend Sub New(previa As System.Text.Encoding)
        _previa = previa
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        If _liberado Then Return
        _liberado = True
        PluginEncodingSettings.RestoreTranslatable(_previa)
    End Sub
End Class
