Imports System.IO
Imports System.Text

''' <summary>Configuracion central de encoding para la E/S de plugins ESP/ESM/ESL. El contrato es
''' reproducir bit a bit las reglas de encoding que un plugin necesita para ser válido: cualquier
''' divergencia es un bug.
''' <para>Globales: general no traducible = cp1252; traducible = cp1252 hasta que sLanguage lo cambie; VMAD
''' siempre UTF-8; para STRINGS el default primario es UTF-8 y el de fallback cp1252, cada uno con su mapa por
''' idioma (FO4 y SSE solo siembran su idioma en el primario, el fallback siempre lleva el mapa completo).</para>
''' <para>sLanguage sale del INI del juego y del custom, y fija el encoding traducible; los parametros de linea
''' de comando -cp-trans y -cp-general lo pisan.</para>
''' <para>Precedencia por archivo: override a nivel de definicion, despues el encoding del propio archivo
''' segun sea traducible o no, y por ultimo el global correspondiente.</para></summary>
Public Module PluginEncodingSettings

    Private ReadOnly _syncRoot As New Object()
    ' UTF-8 con decoder ESTRICTO (throwOnInvalidBytes): tira excepcion ante bytes invalidos en vez
    ' de reemplazarlos silenciosamente. Hace falta para la cadena de fallback del sidecar STRINGS
    ' (LocalizedStrings.DecodeWithEncoding: intenta UTF-8 primero, si tira cae a cp1252). Con el
    ' Encoding.UTF8 default de .NET (fallback de reemplazo) el decoder NUNCA tira, el catch queda
    ' codigo muerto, y un .STRINGS en cp1252 leido como UTF-8 da mojibake U+FFFD en vez de caer a
    ' cp1252 — el mojibake reportado en STRINGS de coreano/espanol.
    '
    ' throwOnInvalidBytes afecta al DECODER (lectura). El ENCODER nunca tira para strings validos
    ' de .NET (UTF-8 codifica cualquier scalar Unicode), asi que esto NO reintroduce el problema de
    ' EncoderFallback a mitad de guardado: ese es de MBCSEncoding(cp), que reemplaza en silencio los
    ' caracteres no codificables en vez de tirar.
    Private ReadOnly _utf8 As Encoding = New UTF8Encoding(encoderShouldEmitUTF8Identifier:=False, throwOnInvalidBytes:=True)
    Private ReadOnly _encodingCache As New Dictionary(Of Integer, Encoding)()

    ''' <summary>
    ''' Mapa completo idioma→codepage, usado como mapa de FALLBACK y, para juegos mas viejos que
    ''' soportan solo idiomas completos, tambien como mapa PRIMARIO.
    '''
    ''' Primer bloque = los 19 nombres de idioma completos con su codepage. NO canonicalizar/aliasear
    ''' estas entradas (nada de "es"→"spanish").
    '''
    ''' Segundo bloque = codigos cortos de idioma que usa FO4 (en/fr/ru/ko…) + coreano. Son los
    ''' sufijos reales de archivo STRINGS y los valores de sLanguage que usa Fallout4.ini
    ''' (Fallout4_en.STRINGS, _ru, _ko…). Es un agregado deliberado para que el fallback INLINE
    ''' (DecodeTranslatable) pueda resolver el codepage correcto a partir de un codigo corto de
    ''' sLanguage. Son entradas directas (token→cp), NO alias que redirigen a otro token.
    ''' El coreano (ko/kor/korean→949) no tiene localizacion oficial de FO4; las traducciones fan usan CP949.
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

    ''' <summary>Mapa primario. Lo puebla InitializeForGame segun el juego.</summary>
    Private _languageMapPrimary As Dictionary(Of String, Integer) = Nothing
    ''' <summary>Mapa de fallback. Siempre igual a _languageMapFull.</summary>
    Private ReadOnly _languageMapFallback As Dictionary(Of String, Integer) = _languageMapFull

    Private _general As Encoding = Nothing
    Private _translatable As Encoding = Nothing
    Private _translatableDefaultPrimary As Encoding = Nothing   ' default cuando falla el lookup primario
    Private _translatableDefaultFallback As Encoding = Nothing  ' default cuando falla el lookup de fallback
    ''' <summary>
    ''' Codepage del sLanguage actual (del mapa completo), usado como fallback INLINE en
    ''' DecodeTranslatable: cuando el primario (Translatable, normalmente UTF-8 para FO4) tira con
    ''' un string inline que no es UTF-8, reintentamos con este. Es una mejora deliberada nuestra
    ''' (sin fallback inline no hay reintento posible) para que los plugins inline en un codepage
    ''' legado (cp1251/CP949/…) se lean bien sin romper los plugins UTF-8.
    ''' Se fija desde SetLanguage = GetEncodingForLanguage(sLanguage, True). Default cp1252 antes de SetLanguage.
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

    ''' <summary>Strings de plugin no traducibles. Default cp1252.</summary>
    Public ReadOnly Property General As Encoding
        Get
            EnsureInitialized()
            Return _general
        End Get
    End Property

    ''' <summary>Strings de plugin traducibles (FULL/SHRT/DESC/etc).</summary>
    Public ReadOnly Property Translatable As Encoding
        Get
            EnsureInitialized()
            Return _translatable
        End Get
    End Property

    ''' <summary>Default cuando falla el lookup primario. Siempre UTF-8.</summary>
    Public ReadOnly Property TranslatableDefaultPrimary As Encoding
        Get
            EnsureInitialized()
            Return _translatableDefaultPrimary
        End Get
    End Property

    ''' <summary>Default cuando falla el lookup de fallback. Siempre cp1252.</summary>
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
    ''' Override de encoding primaria para archivos de strings localizados EXTERNOS, o Nothing cuando
    ''' no hay override explicito (el loader entonces usa el encoding que corresponde al sufijo del
    ''' nombre de archivo). Lo fija OverridePluginEncoding.ini Translatable=. Ver _localizationPrimaryOverride.
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
    ''' Aplica los defaults por juego:
    '''   inicializacion global de encodings (General y los defaults de Translatable)
    '''   poblado del mapa primario segun el juego
    ''' </summary>
    Public Sub InitializeForGame(game As Config_App.Game_Enum)
        SyncLock _syncRoot
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)

            ' Init global (todos los juegos)
            _general = MBCSEncoding(1252)                           ' General
            _translatableDefaultPrimary = _utf8                     ' default primario
            _translatableDefaultFallback = MBCSEncoding(1252)       ' default de fallback

            ' Puebla _languageMapPrimary segun el juego.
            ' Game_Enum.Fallout4 = FO4; Game_Enum.Skyrim se trata como SSE en el resto de la lib
            ' (ver PluginWriter.TES4_RECORD_VERSION_SSE), asi que cae en la rama de SSE aca.
            Dim primary As New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)
            Select Case game
                Case Config_App.Game_Enum.Fallout4
                    primary("en") = 1252
                Case Config_App.Game_Enum.Skyrim
                    ' (Game_Enum.Skyrim se trata como SSE, siguiendo el resto de la lib)
                    primary("english") = 1252
                Case Else
                    ' Mapa completo para el resto de los juegos.
                    For Each kvp In _languageMapFull
                        primary(kvp.Key) = kvp.Value
                    Next
            End Select

            ' ⛔ El coreano NO va en el mapa primario: CP949 nunca tira, asi que un plugin coreano que
            ' resultara ser UTF-8 quedaria mojibake en silencio.
            ' En cambio korean/ko/kor→949 vive en el mapa completo/fallback, y
            ' el fallback inline (DecodeTranslatable: intenta UTF-8 → si tira, cae al codepage de
            ' sLanguage) lee bien tanto plugins CP949 como UTF-8 en coreano. Mismo modelo de
            ' UTF-8-primero + fallback-por-codepage que el sidecar .STRINGS.
            _languageMapPrimary = primary

            ' El valor inicial de Translatable en cp1252 nunca es observable en la practica: siempre
            ' se sobreescribe con el resultado de resolver el idioma actual. Por eso sembramos
            ' _translatable con el default primario (UTF-8 para FO4), asi cualquier acceso ANTES de
            ' que corra SetLanguage obtiene el default correcto en vez de cp1252. SetLanguage (llamado
            ' desde el arranque de la app) lo refina con el sLanguage del INI.
            _translatable = _translatableDefaultPrimary
            ' Inline fallback default = cp1252 until SetLanguage sets it from the sLanguage codepage.
            _translatableInlineFallback = _translatableDefaultFallback

            _initialized = True
        End SyncLock
    End Sub

    ''' <summary>
    ''' Aplica el override de Translatable especifico del idioma, resolviendo el sLanguage
    ''' normalizado (Trim + minusculas) contra el mapa PRIMARIO.
    '''
    ''' Usa el mapa PRIMARIO, que para FO4 solo contiene {'en' → 1252}. Cualquier otro token de
    ''' idioma (incluidos "spanish", "russian", "english") cae al default primario = UTF-8.
    '''
    ''' Esta resolucion corre INCONDICIONALMENTE: un sLanguage vacio o ausente tambien pasa por el
    ''' lookup, que falla y devuelve el default primario (UTF-8 para FO4). Por eso un idioma vacio
    ''' TIENE que fijar _translatable = UTF-8, no dejarlo en un cp1252 previo. (⛔ NO retornar temprano
    ''' con un idioma vacio: deja cp1252 y rompe los plugins en coreano/chino cuando el INI del usuario no
    ''' tiene entrada de sLanguage.)
    ''' </summary>
    Public Sub SetLanguage(language As String)
        EnsureInitialized()

        Dim normalized = NormalizeLanguage(language)

        SyncLock _syncRoot
            ' Lookup inline del codepage primario. Token vacio → no encuentra → default.
            Dim cp As Integer = 0
            If normalized <> "" AndAlso _languageMapPrimary IsNot Nothing AndAlso _languageMapPrimary.TryGetValue(normalized, cp) Then
                _translatable = If(cp = 65001, _utf8, MBCSEncoding(cp))
            Else
                _translatable = _translatableDefaultPrimary
            End If

            ' Fallback inline = codepage de este sLanguage segun el mapa completo/fallback.
            ' Lo usa DecodeTranslatable cuando el primario tira. Para ko → CP949, ru → cp1251, etc.;
            ' token desconocido → default cp1252. Calculado inline para evitar un lock reentrante.
            Dim fbCp As Integer = 0
            If normalized <> "" AndAlso _languageMapFallback.TryGetValue(normalized, fbCp) Then
                _translatableInlineFallback = If(fbCp = 65001, _utf8, MBCSEncoding(fbCp))
            Else
                _translatableInlineFallback = _translatableDefaultFallback
            End If
        End SyncLock
    End Sub

    ''' <summary>Override ACOTADO de la encoding Translatable: la fija y la DEVUELVE al salir del
    ''' <c>Using</c>. Es el que tiene que usar cualquier camino interactivo.
    ''' <para>EXISTE PORQUE <see cref="SetTranslatableOverride"/> NO SE RESTAURA NUNCA y no hay un solo
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

    ''' <summary>Override manual de la encoding Translatable, equivalente a un parametro de linea de comandos.
    ''' <para>NO SE RESTAURA: es el equivalente del parámetro de línea de comandos, o sea una decisión
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

    ''' <summary>Override manual de la encoding General, equivalente a un parametro de linea de comandos.</summary>
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

    ''' <summary>Lee OverridePluginEncoding.ini del directorio dado (tipicamente appdir) y aplica los
    ''' overrides de Translatable / General / TranslatableInlineFallback. Version basada en archivo
    ''' de esos mismos overrides manuales, con la misma convencion que SkipEyebrowsTone.ini
    ''' (lineas planas clave=valor, comentarios con ; o #, [secciones] ignoradas, claves sin distinguir mayusculas).
    ''' Archivo ausente = no-op.</summary>
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
    ''' Decodifica bytes de un subrecord de string inline usando un override de encoding explicito
    ''' por archivo (tipicamente del `&lt;cp:XXXX&gt;` del SNAM de TES4). Decodificacion de un solo
    ''' intento: el override por archivo le gana al global, sin encadenar a una encoding de
    ''' fallback. Si el decoder falla devolvemos "" (el resto del parser/UI maneja strings vacios
    ''' sin problema).
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
    ''' Decodifica bytes de un string TRADUCIBLE inline (FULL/SHRT/ATTX en un plugin no localizado)
    ''' usando la encoding Translatable global, CON un fallback inline al codepage del sLanguage.
    '''
    ''' Mejora deliberada sobre un decode de un solo intento: en vez de rendirnos ante un string
    ''' inline que no es UTF-8, reintentamos con el codepage del sLanguage (_translatableInlineFallback),
    ''' igual que el fallback del sidecar .STRINGS. Esto es SEGURO: el fallback solo corre dentro del
    ''' Catch, o sea solo cuando el primario YA fallo (donde de otro modo el resultado seria basura).
    ''' Nunca cambia un valor que el primario ya decodifico bien. Cubre plugins de FO4 cuyo FULL/etc
    ''' esta en un codepage legado (coreano CP949, ruso cp1251, …) sin afectar los plugins UTF-8.
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
    ''' Decodifica bytes de un subrecord de string inline NO traducible usando la encoding General.
    ''' Para campos marcados como no traducibles el encoding a usar es siempre el General
    ''' (cp1252 para FO4), nunca el Translatable. Se usa para EDID y otros campos de string normales
    ''' u overrideables. Un solo intento, sin cadena de fallback (igual que el camino traducible).
    ''' </summary>
    Public Function DecodeGeneral(data As Byte(), offset As Integer, count As Integer) As String
        Return DecodeWithEncoding(data, offset, count, General)
    End Function

    ''' <summary>
    ''' Codifica un string a bytes usando la encoding Translatable. Una sola llamada a
    ''' encoding.GetBytes con fallback de reemplazo silencioso ('?' para caracteres no codificables).
    ''' La capa de UX (chequeo de conflictos en NpcOverrideSaver) detecta conflictos ANTES de que
    ''' corra el writer, asi que el '?' silencioso solo pasa si se salteo la validacion.
    ''' Usar para campos traducibles (FULL/SHRT/DESC/ATTX/combo-FULL).
    ''' </summary>
    Public Function EncodeTranslatable(value As String) As Byte()
        If String.IsNullOrEmpty(value) Then Return Array.Empty(Of Byte)()
        Return Translatable.GetBytes(value)
    End Function

    ''' <summary>
    ''' Codifica un string a bytes usando la encoding General (no traducible, cp1252 para FO4).
    ''' Para campos marcados como no traducibles el encoding a usar es siempre el General, nunca el
    ''' Translatable. Usar para campos de string normales/overrideables: EDID, ATKE (Attack Event),
    ''' ATKT (Description), DSTA (Sequence Name), DMDL (Model FileName). Fallback de reemplazo
    ''' silencioso ('?').
    ''' </summary>
    Public Function EncodeGeneral(value As String) As Byte()
        If String.IsNullOrEmpty(value) Then Return Array.Empty(Of Byte)()
        Return General.GetBytes(value)
    End Function

    ''' <summary>Codifica el NOMBRE DE ARCHIVO de un master (TES4.MAST) y REHÚSA si no sobrevive el viaje.
    ''' <para>⛔ NUNCA <c>Encoding.ASCII.GetBytes</c> acá — es el peor caso de "lector ≠ escritor" del
    ''' dominio: SUSTITUYE por <c>?</c> sin lanzar, mientras el lector decodifica el
    ''' MAST con la General (<see cref="DecodeGeneral"/>, PluginReader.ReadTES4) porque ese campo del formato
    ''' es un string normal/overrideable ⇒ encoding General, no Translatable. Un master <c>Café Mod.esp</c>
    ''' sale como <c>Caf? Mod.esp</c>: un archivo que no existe, así que el motor y el CK rechazan el plugin
    ''' ENTERO y toda referencia a ese mod queda rota.</para>
    ''' <para>El nombre de un master no es texto de presentación: es una CLAVE de búsqueda en disco, así que
    ''' una sustitución silenciosa no degrada, invalida. Por eso, a diferencia de
    ''' <see cref="EncodeTranslatable"/> (que se queda con el fallback de reemplazo silencioso porque el chequeo
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
    ''' Codifica un string que vive DENTRO de un payload VMAD (nombres de script, nombres de
    ''' propiedad, valores String/Array-of-String). VMAD es el unico lugar donde el encoding esta
    ''' fijado a UTF-8 sin importar juego o idioma — NO la encoding General/cp1252 que usa el resto
    ''' de los strings inline. Igual para FO4 y Skyrim, asi que deliberadamente esto no ramifica por juego.
    ''' <para>No emite BOM ni terminador NUL: los strings de VMAD llevan longitud de 2 bytes como
    ''' prefijo, nunca terminan en cero.</para>
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
    ''' Resuelve el encoding para un idioma: busca el token en el mapa correspondiente y, si no lo
    ''' encuentra, devuelve el default de ese mapa.
    ''' fallback=False → mapa primario + default UTF-8.
    ''' fallback=True  → mapa completo + default cp1252.
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
    ''' Pasa a minusculas y recorta espacios. Los tokens de idioma de Bethesda nunca traen espacios
    ''' internos, asi que el .Replace(" ", "") de aca es un no-op para entradas validas; se deja
    ''' como red de seguridad.
    ''' </summary>
    Public Function NormalizeLanguage(language As String) As String
        Dim normalized = If(language, "").Trim().ToLowerInvariant()
        If normalized = "" Then Return ""
        Return normalized.Replace(" ", "")
    End Function

    ''' <summary>
    ''' Parsea un string de Description SNAM de TES4 y extrae, si esta presente, el tag de encoding
    ''' traducible por archivo. El tag tiene el formato exacto <c>&lt;cp:XXXX&gt;</c>: 9 caracteres,
    ''' con un slot de 4 caracteres para el codigo de pagina. La busqueda es insensible a mayusculas.
    ''' Devuelve Nothing si el SNAM no trae un tag reconocible.
    ''' </summary>
    Public Function ParseSnamCpTag(snamValue As String) As Encoding
        If String.IsNullOrEmpty(snamValue) Then Return Nothing

        ' Busqueda insensible a mayusculas.
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
    ''' Arma el literal del tag SNAM que registra la encoding Translatable actual.
    ''' El tag tiene que matchear `&lt;cp:XXXX&gt;` — 9 caracteres exactos, con un slot de 4
    ''' caracteres para el codigo de pagina. Valores reconocidos:
    '''   &lt;cp:utf8&gt;   UTF-8 (sin guion, "utf8" exactamente 4 caracteres)
    '''   &lt;cp:1252&gt;   Windows-1252         &lt;cp:1251&gt;   Cirilico/Ruso
    '''   &lt;cp:1250&gt;   Europa Central       &lt;cp:1253&gt;   Griego
    '''   &lt;cp:1254&gt;   Turco                &lt;cp:1256&gt;   Arabe
    '''   &lt;cp:0932&gt;   Japones Shift-JIS    &lt;cp:0936&gt;   Chino Simplificado GBK
    '''   &lt;cp:0950&gt;   Chino Tradicional Big5
    ''' El relleno con ceros a la izquierda es valido al parsear el numero.
    '''
    ''' Devuelve "" cuando la Translatable actual es UTF-8 (default de FO4) — en ese caso
    ''' cualquier lector consciente de FO4 ya asume UTF-8 por defecto.
    '''
    ''' NOTA: este tag no se auto-genera en otras herramientas del ecosistema (se maneja a mano en
    ''' la Description); lo emitimos nosotros como una mejora deliberada, para que el plugin se lea
    ''' correctamente en herramientas de terceros sin importar el idioma configurado en la maquina
    ''' de destino. No ayuda in-game (el motor ignora el tag).
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
    ''' Resuelve sLanguage con la precedencia de INI del juego:
    '''   1. Lee sLanguage del INI principal del juego (Fallout4.ini)
    '''   2. Si el INI custom (Fallout4Custom.ini) existe y tiene sLanguage, lo pisa.
    ''' No se lee Fallout4Prefs.ini para sLanguage. Devuelve "" si ninguno lo tiene.
    ''' El pasaje a minusculas lo hace despues SetLanguage vía NormalizeLanguage.
    ''' </summary>
    Public Function ReadLanguageFromIni() As String
        Try
            ' Ubicacion del INI segun el juego: FO4 lee
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
            If customValue <> "" Then result = customValue  ' el INI custom pisa al del juego
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
        ' EL JUEGO SALE DE LA CONFIG, NO HARDCODEADO. Con un juego cableado acá, el PRIMERO que toque
        ' cualquier getter fija ESE mapa de idiomas y marca `_initialized`, así que en una sesión del otro
        ' juego el `SetLanguage` posterior falla el lookup y cae a UTF-8 — un default silencioso que
        ' decide cómo se DECODIFICA todo el plugin.
        InitializeForGame(Config_App.Current.Game)
    End Sub

    ''' <summary>
    ''' Devuelve una Encoding de .NET cacheada para el codigo de pagina dado, con el fallback por
    ''' defecto (reemplazo '?' para caracteres no codificables, sin excepciones) — sin overridear
    ''' EncoderFallback/DecoderFallback.
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
    ''' Parsea el string de encoding pedido:
    '''   utf-8 / utf8 → UTF-8
    '''   windows-XXXX → saca el prefijo → codigo de pagina entero
    '''   65001 → UTF-8
    '''   si no, se interpreta como codigo de pagina numerico
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
