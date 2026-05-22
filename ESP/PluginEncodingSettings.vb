Imports System.IO
Imports System.Text

''' <summary>
''' Central encoding settings for ESP/ESM/ESL plugin I/O. LITERAL mirror of xEdit's encoding
''' subsystem. Read this file alongside the cited xEdit source — every value, every map entry,
''' every default has a file:line reference. Any divergence from xEdit is a bug.
'''
''' Globals (TES5Edit/Core/wbInterface.pas:24295-24310):
'''   wbEncoding         := wbMBCSEncoding(1252)              — non-translatable, cp1252
'''   wbEncodingTrans    := wbEncoding                        — translatable, cp1252 initially
'''   wbEncodingVMAD     := TEncoding.UTF8                    — VMAD scripts always UTF-8
'''   wbLEncodingDefault[False] := TEncoding.UTF8             — primary default for STRINGS
'''   wbLEncodingDefault[True]  := wbMBCSEncoding(1252)       — fallback default for STRINGS
'''   wbLEncoding[False] := TStringList ...                   — primary map (game-specific contents)
'''   wbLEncoding[True]  := TStringList ...                   — fallback map (full map for all games)
'''
''' Game init (TES5Edit/xEdit/xeInit.pas:1118-1131):
'''   if wbGameMode &lt;= gmEnderal then
'''     wbAddDefaultLEncodingsIfMissing(False)               ' primary = full map (TES4/FO3/FNV/TES5/Enderal)
'''   else begin
'''     wbLEncodingDefault[False] := TEncoding.UTF8           ' redundant — already UTF-8
'''     case wbGameMode of
'''       gmSSE/TES5VR/EnderalSE: wbAddLEncodingIfMissing('english', '1252', False)
'''     else {FO4, FO76}:        wbAddLEncodingIfMissing('en',      '1252', False)
'''   end;
'''   wbAddDefaultLEncodingsIfMissing(True)                  ' fallback = full map (unconditional)
'''
''' sLanguage propagation (TES5Edit/xEdit/xeInit.pas:1274-1329):
'''   wbLanguage := Trim(ReadString('General', 'sLanguage', '')).ToLower   ' from game INI + custom INI
'''   wbEncodingTrans := wbEncodingForLanguage(wbLanguage, False)           ' primary map lookup
'''   -cp-trans param overrides wbEncodingTrans
'''   -cp-general param overrides wbEncoding
'''
''' Per-file precedence (bsdGetEncoding, wbInterface.pas:23519-23535):
'''   bsdEncodingOverride (def-level)
'''     → aElement._File.Encoding[translatable]                 ' per-file flEncodingTrans
'''     → wbEncodingTrans (translatable) or wbEncoding (non-translatable)  ' global
''' </summary>
Public Module PluginEncodingSettings

    Private ReadOnly _syncRoot As New Object()
    ' xEdit uses TEncoding.UTF8 (Delphi default, replacement fallback for invalid bytes / chars).
    ' Mirror that — do NOT use UTF8Encoding(False, True) strict mode (that was a regression that
    ' threw EncoderFallbackException mid-save when the user chose an encoding incompatible with
    ' some character, breaking the save flow without giving the user actionable info).
    Private ReadOnly _utf8 As Encoding = Encoding.UTF8
    Private ReadOnly _encodingCache As New Dictionary(Of Integer, Encoding)()

    ''' <summary>
    ''' Full language→codepage map. LITERAL mirror of wbAddDefaultLEncodingsIfMissing
    ''' (TES5Edit/Core/wbInterface.pas:23665-23686). xEdit calls this procedure to populate the
    ''' FALLBACK array `wbLEncoding[True]` (xeInit.pas:1131, unconditional), and also to populate
    ''' the PRIMARY array `wbLEncoding[False]` for games &lt;= gmEnderal (xeInit.pas:1120).
    ''' For games &gt; gmEnderal (FO4, FO76, SSE, TES5VR, EnderalSE) the primary array gets only
    ''' one entry — see _languageMapPrimary built in InitializeForGame.
    ''' Treat this list as a synced copy of xEdit. Do NOT add aliases. Do NOT canonicalize.
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
        {"arabic", 1256}
    }

    ''' <summary>Primary map (mirror of xEdit wbLEncoding[False]). Populated per-game by InitializeForGame.</summary>
    Private _languageMapPrimary As Dictionary(Of String, Integer) = Nothing
    ''' <summary>Fallback map (mirror of xEdit wbLEncoding[True]). Always = _languageMapFull (xeInit.pas:1131).</summary>
    Private ReadOnly _languageMapFallback As Dictionary(Of String, Integer) = _languageMapFull

    Private _general As Encoding = Nothing
    Private _translatable As Encoding = Nothing
    Private _translatableDefaultPrimary As Encoding = Nothing   ' wbLEncodingDefault[False]
    Private _translatableDefaultFallback As Encoding = Nothing  ' wbLEncodingDefault[True]
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
            _languageMapPrimary = primary

            ' wbInterface.pas:24296 — wbEncodingTrans := wbEncoding (cp1252)
            ' (xeInit.pas:1323 will overwrite this after SetLanguage is called)
            _translatable = _general

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
    ''' </summary>
    Public Sub SetLanguage(language As String)
        EnsureInitialized()

        Dim normalized = NormalizeLanguage(language)
        If normalized = "" Then Return

        SyncLock _syncRoot
            ' Mirror of wbEncodingForLanguage(normalized, False) inline
            Dim cp As Integer = 0
            If _languageMapPrimary IsNot Nothing AndAlso _languageMapPrimary.TryGetValue(normalized, cp) Then
                _translatable = If(cp = 65001, _utf8, MBCSEncoding(cp))
            Else
                _translatable = _translatableDefaultPrimary
            End If
        End SyncLock
    End Sub

    ''' <summary>Manual override for Translatable encoding. Mirror of xeInit.pas -cp-trans / -cp command-line param.</summary>
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
    ''' Decode bytes for an inline string subrecord using the global Translatable encoding.
    ''' Mirror of TwbStringDef.ToStringNative (wbInterface.pas:16480-16567) — single-shot decode
    ''' via wbEncodingTrans, no fallback chain. See DecodeWithEncoding doc.
    ''' </summary>
    Public Function DecodeTranslatable(data As Byte(), offset As Integer, count As Integer) As String
        Return DecodeWithEncoding(data, offset, count, Translatable)
    End Function

    ''' <summary>
    ''' Encode string to bytes using Translatable encoding. Mirror of TwbStringDef.FromStringNative
    ''' (wbInterface.pas:16322) — single call to encoding.GetBytes with Delphi-default replacement
    ''' fallback (silent '?' for unencodable chars). UX layer (pre-flight check in SaveEsp_Form)
    ''' detects conflicts BEFORE save reaches the writer, so silent '?' only happens if the user
    ''' bypasses validation.
    ''' </summary>
    Public Function EncodeTranslatable(value As String) As Byte()
        If String.IsNullOrEmpty(value) Then Return Array.Empty(Of Byte)()
        Return Translatable.GetBytes(value)
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
            Dim documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            Dim iniDir = Path.Combine(documents, "My Games", "Fallout4")
            Dim gameIni = Path.Combine(iniDir, "Fallout4.ini")
            Dim customIni = Path.Combine(iniDir, "Fallout4Custom.ini")

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
        InitializeForGame(Config_App.Game_Enum.Fallout4)
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
