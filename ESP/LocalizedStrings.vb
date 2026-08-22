Imports System.IO
Imports System.Text

Public Enum LocalizedStringTableKind
    Strings
    DLStrings
    ILStrings
End Enum

''' <summary>Las extensiones de las tablas de texto externo, UNA sola vez y derivadas del enum: la
''' extension de cada clase la decide <see cref="LocalizedStringResolver"/>, y el diccionario de Data
''' las necesita para indexarlas.
''' <para>Estaban escritas dos veces —aca y en el `SupportedExtensions` del diccionario—, y si esas
''' dos listas divergen la resolucion devuelve cadena vacia PARA SIEMPRE y en silencio: el archivo
''' existe, el diccionario no lo indexo, y nadie tira. Derivarlas hace que no puedan divergir.</para></summary>
Public Module LocalizedStringExtensions

    Public ReadOnly Property Todas As IReadOnlyList(Of String) = Construir()

    Private Function Construir() As IReadOnlyList(Of String)
        Dim vistas As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        Dim result As New List(Of String)
        For Each k As LocalizedStringTableKind In [Enum].GetValues(GetType(LocalizedStringTableKind))
            Dim ext = ExtensionDe(k)
            If vistas.Add(ext) Then result.Add(ext)
        Next
        Return result
    End Function

    ''' <summary>La extension de una clase de tabla. Unico sitio donde se decide.</summary>
    Public Function ExtensionDe(kind As LocalizedStringTableKind) As String
        Select Case kind
            Case LocalizedStringTableKind.DLStrings
                Return ".DLSTRINGS"
            Case LocalizedStringTableKind.ILStrings
                Return ".ILSTRINGS"
            Case Else
                Return ".STRINGS"
        End Select
    End Function

End Module


''' <summary>
''' Thin compatibility shim. Authoritative encoding settings live in PluginEncodingSettings.
''' This module exposes only the helpers required by LocalizedStringTable / LocalizedStringResolver.
''' </summary>
Friend Module PluginTextDecoding

    ''' <summary>Inline plugin string (FULL/SHRT/etc) — Translatable encoding with fallback.</summary>
    Public Function DecodePluginString(data As Byte(), offset As Integer, count As Integer) As String
        Return PluginEncodingSettings.DecodeTranslatable(data, offset, count)
    End Function

    ''' <summary>Strings/DLStrings/ILStrings sidecar — explicit primary+fallback (per-file).</summary>
    Public Function DecodeLocalizedString(data As Byte(), offset As Integer, count As Integer, primary As Encoding, fallback As Encoding) As String
        Return DecodeWithEncoding(data, offset, count, primary, fallback)
    End Function

    Public Function NormalizeLanguage(language As String) As String
        Return PluginEncodingSettings.NormalizeLanguage(language)
    End Function

    Public Function GetLocalizationPrimaryEncoding(language As String) As Encoding
        Return PluginEncodingSettings.GetEncodingForLanguage(language, fallback:=False)
    End Function

    Public Function GetLocalizationFallbackEncoding(language As String) As Encoding
        Return PluginEncodingSettings.GetEncodingForLanguage(language, fallback:=True)
    End Function

    Public Function TryGetCodePageOverride(stringsFilePath As String) As Encoding
        If String.IsNullOrWhiteSpace(stringsFilePath) Then Return Nothing

        Dim overridePath = Path.ChangeExtension(stringsFilePath, ".cpoverride")
        If Not File.Exists(overridePath) Then Return Nothing

        Try
            Dim firstLine = File.ReadLines(overridePath).FirstOrDefault()
            Dim value = If(firstLine, "").Trim()
            If value = "" Then Return Nothing
            Return PluginEncodingSettings.ParseEncodingPublic(value)
        Catch
            Return Nothing
        End Try
    End Function

    Private Function DecodeWithEncoding(data As Byte(), offset As Integer, count As Integer, primary As Encoding, fallback As Encoding) As String
        If data Is Nothing OrElse count <= 0 Then Return ""
        If offset < 0 Then offset = 0
        If offset >= data.Length Then Return ""
        count = Math.Min(count, data.Length - offset)
        If count <= 0 Then Return ""

        If primary Is Nothing Then primary = PluginEncodingSettings.TranslatableFallback

        Try
            Return primary.GetString(data, offset, count)
        Catch ex As DecoderFallbackException
            If fallback IsNot Nothing AndAlso Not Object.ReferenceEquals(primary, fallback) Then
                Return fallback.GetString(data, offset, count)
            End If
            Throw
        End Try
    End Function
End Module

Friend NotInheritable Class LocalizedStringTable
    Private ReadOnly _kind As LocalizedStringTableKind

    ''' <summary>Identificador → DÓNDE empieza su texto dentro de <see cref="_data"/>. Se arma
    ''' recorriendo el directorio, SIN decodificar nada.
    '''
    ''' <para>⛔ NO guardar acá identificador → TEXTO decodificando la tabla ENTERA al construirla.
    ''' Las tablas del juego traen del orden de cien mil textos —todos los diálogos, todas las
    ''' descripciones— y la aplicación usa unos pocos miles: los nombres de los NPC que muestra la
    ''' lista. MEDIDO en el orden de carga real: <b>1,35 s</b> de arranque y una cadena por entrada,
    ''' para tirar el 95 %.</para>
    '''
    ''' <para>El directorio es aritmética pura sobre el arreglo de bytes, así que indexarlo cuesta
    ''' milisegundos; el texto se decodifica cuando alguien lo pide, y no antes.</para></summary>
    Private ReadOnly _offsets As New Dictionary(Of UInteger, Integer)()

    ''' <summary>Los textos YA decodificados. Es un caché, no la fuente.
    ''' <para>Concurrente porque acá se llega desde el <c>Parallel.ForEach</c> por NPC del horneado:
    ''' un diccionario común se corrompe con dos hilos escribiendo. Decodificar es una función pura
    ''' del mismo arreglo de bytes, así que si dos hilos decodifican el mismo identificador a la vez
    ''' producen la MISMA cadena y la carrera no puede dar un valor distinto.</para></summary>
    Private ReadOnly _decodificados As New Concurrent.ConcurrentDictionary(Of UInteger, String)()

    ''' <summary>Los bytes de la tabla. Se conservan porque son la fuente de la que se decodifica.
    ''' <para>EL INTERCAMBIO, DICHO: quien resuelve POCO paga sólo el blob y ahorra las ~100 mil cadenas
    ''' que la versión ansiosa construía —el caso de la app, que muestra unos miles de nombres—. Quien
    ''' resuelve CASI TODO termina con el blob MÁS las cadenas, o sea peor que la ansiosa: es el caso de un
    ''' volcado completo (`Tools/StringsResolverGate` resuelve 34.604 filas en FO4). El blob son unos pocos
    ''' MB por plugin localizado; las cadenas, decenas.</para></summary>
    Private ReadOnly _data As Byte()

    Private ReadOnly _primaryEncoding As Encoding
    Private ReadOnly _fallbackEncoding As Encoding

    Public Sub New(resourceName As String, kind As LocalizedStringTableKind, data As Byte(), Optional looseFilePath As String = "")
        _kind = kind

        Dim language = ExtractLanguageToken(resourceName)
        Dim primary = PluginTextDecoding.GetLocalizationPrimaryEncoding(language)
        Dim fallback = PluginTextDecoding.GetLocalizationFallbackEncoding(language)

        ' Global INI override (OverridePluginEncoding.ini Translatable=): apply the same primary+fallback
        ' chain the inline DecodeTranslatable path uses, so EXTERNAL STRINGS honor the user's encoding
        ' escape hatch (canonical: Korean FO4 fan translations shipped under an _en suffix whose bytes
        ' are actually UTF-8/CP949). Opt-in only — Nothing here leaves the standard filename-suffix
        ' encodings untouched (so e.g. an _ru.STRINGS still decodes cp1251). The override REPLACES the
        ' primary on purpose: the _en filename-suffix primary is cp1252, which never throws and would
        ' shadow any UTF-8/CP949 fallback, so layering it in as a mere fallback would never trigger.
        Dim primaryOverride = PluginEncodingSettings.TryGetLocalizationPrimaryOverride()
        If primaryOverride IsNot Nothing Then
            primary = primaryOverride
            fallback = PluginEncodingSettings.TranslatableInlineFallback
        End If

        ' Per-file .cpoverride sidecar is the most specific signal — wins over the global INI override.
        Dim overrideEncoding = PluginTextDecoding.TryGetCodePageOverride(looseFilePath)
        If overrideEncoding IsNot Nothing Then
            primary = overrideEncoding
        End If

        If fallback IsNot Nothing AndAlso primary IsNot Nothing AndAlso String.Equals(primary.WebName, fallback.WebName, StringComparison.OrdinalIgnoreCase) Then
            fallback = Nothing
        End If

        _primaryEncoding = primary
        _fallbackEncoding = fallback
        _data = data
        Indexar(data)
    End Sub

    ''' <summary>El texto de ese identificador, o "" si la tabla no lo trae.
    ''' <para>Un identificador que el directorio no declara —o que declara un desplazamiento fuera
    ''' de la tabla— devuelve "": esos nunca entraron al índice.</para></summary>
    Public Function Resolve(stringId As UInteger) As String
        Dim yaEsta As String = Nothing
        If _decodificados.TryGetValue(stringId, yaEsta) Then Return yaEsta

        Dim offset As Integer
        If Not _offsets.TryGetValue(stringId, offset) Then Return ""

        ' GetOrAdd y no un Add pelado: dos hilos pueden llegar juntos al mismo identificador y los
        ' dos decodifican lo mismo, pero el que publica tiene que ser uno solo.
        Return _decodificados.GetOrAdd(stringId, Function(unused) ReadValue(_data, offset))
    End Function

    ''' <summary>Recorre el DIRECTORIO y anota dónde empieza cada texto. No decodifica ninguno.
    '''
    ''' <para>REGLA de aceptación: un desplazamiento que se va de la tabla se saltea (y entonces ese
    ''' identificador no existe, y <see cref="Resolve"/> devuelve ""), y si el mismo identificador
    ''' aparece dos veces gana el ÚLTIMO.</para></summary>
    Private Sub Indexar(data As Byte())
        If data Is Nothing OrElse data.Length < 8 Then Return

        Dim stringCount = BitConverter.ToUInt32(data, 0)
        Dim baseOffset = 8L + CLng(stringCount) * 8L
        If baseOffset > data.Length Then Return

        For i = 0 To CInt(stringCount) - 1
            Dim dirOffset = 8 + i * 8
            If dirOffset + 8 > data.Length Then Exit For

            Dim stringId = BitConverter.ToUInt32(data, dirOffset)
            Dim relativeOffset = BitConverter.ToUInt32(data, dirOffset + 4)
            Dim absoluteOffset = baseOffset + relativeOffset
            If absoluteOffset < 0 OrElse absoluteOffset >= data.Length Then Continue For

            _offsets(stringId) = CInt(absoluteOffset)
        Next
    End Sub

    Private Function ReadValue(data As Byte(), offset As Integer) As String
        If _kind = LocalizedStringTableKind.Strings Then
            Return ReadZeroTerminated(data, offset)
        End If

        Return ReadLengthPrefixed(data, offset)
    End Function

    Private Function ReadZeroTerminated(data As Byte(), offset As Integer) As String
        Dim [end] = offset
        While [end] < data.Length AndAlso data([end]) <> 0
            [end] += 1
        End While

        Return PluginTextDecoding.DecodeLocalizedString(data, offset, [end] - offset, _primaryEncoding, _fallbackEncoding)
    End Function

    Private Function ReadLengthPrefixed(data As Byte(), offset As Integer) As String
        If offset + 4 > data.Length Then Return ""

        Dim lengthWithNull = BitConverter.ToInt32(data, offset)
        If lengthWithNull <= 0 Then Return ""

        Dim count = Math.Min(lengthWithNull - 1, data.Length - (offset + 4))
        If count <= 0 Then Return ""

        Return PluginTextDecoding.DecodeLocalizedString(data, offset + 4, count, _primaryEncoding, _fallbackEncoding)
    End Function

    Private Shared Function ExtractLanguageToken(resourceName As String) As String
        Dim fileName = Path.GetFileNameWithoutExtension(resourceName)
        Dim underscore = fileName.LastIndexOf("_"c)
        If underscore < 0 OrElse underscore >= fileName.Length - 1 Then Return ""
        Return PluginTextDecoding.NormalizeLanguage(fileName.Substring(underscore + 1))
    End Function
End Class

Friend NotInheritable Class LocalizedStringResolver
    ''' <summary>Donde vive una tabla de textos. Guarda la CLAVE del diccionario de Data, no un archive
    ''' con un indice: el indice de una entrada es un numero dentro de UN .ba2 concreto y deja de valer
    ''' cuando ese archive se reescribe, mientras que la clave sigue nombrando el mismo archivo y deja
    ''' que el ganador vigente lo decida el diccionario en cada lectura.</summary>
    Private NotInheritable Class ResourceLocation
        Public Property DictionaryKey As String = ""
        Public Property DisplayName As String = ""

        ''' <summary>Ruta absoluta SOLO si el ganador es un archivo suelto. Es lo que necesita el sidecar
        ''' `.cpoverride` de <see cref="PluginTextDecoding.TryGetCodePageOverride"/>, que por definicion
        ''' no puede existir para una entrada empaquetada.</summary>
        Public Property LoosePath As String = ""

        Public ReadOnly Property CacheKey As String
            Get
                Return DictionaryKey
            End Get
        End Property

        Public Function ReadAllBytes() As Byte()
            Return FilesDictionary_class.GetBytes(DictionaryKey)
        End Function
    End Class

    Private ReadOnly _dataPath As String
    Private ReadOnly _preferredLanguages As List(Of String)
    Private ReadOnly _tableCache As New Dictionary(Of String, LocalizedStringTable)(StringComparer.OrdinalIgnoreCase)
    Private ReadOnly _resourceCache As New Dictionary(Of String, ResourceLocation)(StringComparer.OrdinalIgnoreCase)
    Private ReadOnly _syncRoot As New Object()

    ''' <summary>Los paths bajo `Strings\` que hay en Data, sacados del diccionario del preflight, junto
    ''' con la generacion de scan de la que salieron. Sirve para el descubrimiento por patron
    ''' (`&lt;plugin&gt;_*.EXT`), que es lo unico que no se puede contestar con un TryGetEntry por clave.
    ''' <para>Un re-scan de Data cambia que archivo gana para cada path, asi que la generacion invalida
    ''' esta lista Y las dos caches de arriba: una tabla cacheada de la carga anterior es justo el
    ''' resultado equivocado.</para></summary>
    Private _clavesStrings As List(Of String)
    Private _generacionDeLasClaves As Integer = -1

    Public Sub New(dataPath As String)
        _dataPath = dataPath
        _preferredLanguages = BuildPreferredLanguageList()
    End Sub

    Public Function Resolve(pluginFileName As String, stringId As UInteger, Optional kind As LocalizedStringTableKind = LocalizedStringTableKind.Strings) As String
        If stringId = 0UI OrElse String.IsNullOrWhiteSpace(pluginFileName) Then Return ""

        Dim table = GetTable(pluginFileName, kind)
        If table Is Nothing Then Return ""

        Return table.Resolve(stringId)
    End Function

    Private Function GetTable(pluginFileName As String, kind As LocalizedStringTableKind) As LocalizedStringTable
        Dim location = GetResourceLocation(pluginFileName, kind)
        If location Is Nothing Then Return Nothing

        SyncLock _syncRoot
            Dim cached As LocalizedStringTable = Nothing
            If _tableCache.TryGetValue(location.CacheKey, cached) Then
                Return cached
            End If
        End SyncLock

        ' Leer los bytes y armar la tabla van FUERA del candado a proposito (descomprimir del BA2 y
        ' indexar el directorio no tienen por que serializar a los demas lectores), pero entonces entre
        ' esas dos cosas y la publicacion cabe una invalidacion: si el diccionario cambio en el medio, esta
        ' tabla es de la generacion VIEJA y publicarla la deja viva en un cache recien vaciado.
        Dim genAlLeer = FilesDictionary_class.ScanGeneration
        Dim bytes = location.ReadAllBytes()
        If bytes Is Nothing OrElse bytes.Length = 0 Then Return Nothing

        Dim table = New LocalizedStringTable(location.DisplayName, kind, bytes, location.LoosePath)

        SyncLock _syncRoot
            InvalidarSiCambioElDiccionario()
            ' Se DEVUELVE igual: para este llamador la tabla sirve —la leyo de lo que habia—, lo que no se
            ' hace es dejarla en el cache para los que vengan despues.
            If FilesDictionary_class.ScanGeneration = genAlLeer Then _tableCache(location.CacheKey) = table
        End SyncLock

        Return table
    End Function

    Private Function GetResourceLocation(pluginFileName As String, kind As LocalizedStringTableKind) As ResourceLocation
        Dim pluginBase = Path.GetFileNameWithoutExtension(pluginFileName)
        Dim cacheKey = $"{pluginBase}|{CInt(kind)}"

        SyncLock _syncRoot
            InvalidarSiCambioElDiccionario()
            Dim value As ResourceLocation = Nothing
            If _resourceCache.TryGetValue(cacheKey, value) Then
                Return value
            End If
        End SyncLock

        Dim found = FindResourceLocation(pluginBase, kind)

        SyncLock _syncRoot
            _resourceCache(cacheKey) = found
        End SyncLock

        Return found
    End Function

    ''' <summary>Que archivo de textos gana para este plugin. NO abre ningun archive: el diccionario de
    ''' Data ya resolvio, en el preflight, cual es el archivo final para esta carga de plugins — incluida
    ''' la precedencia del suelto sobre el empaquetado y el orden entre archives. Aca solo queda elegir
    ''' el IDIOMA, que es lo unico que este resolver decide por si mismo.
    ''' <para>⚠️ PRECEDENCIA ENTRE IDIOMA Y MEDIO: manda el IDIOMA, y el medio lo decide el diccionario
    ''' —que es como resuelve el motor: pide `Strings\X_&lt;sLanguage&gt;.STRINGS` y la capa de archivos
    ''' elige suelto o empaquetado—. ⛔ NO probar TODOS los idiomas preferidos en SUELTOS y recién
    ''' después todos en archives: así un `X_en.STRINGS` suelto le gana a un `X_de.STRINGS` empaquetado
    ''' aunque el INI diga aleman. El gate de textos no puede ver esta diferencia: el corpus tiene un
    ''' solo idioma instalado.</para>
    ''' <para>Las DOS fases estan escritas separadas y no como `preferidos.Concat(descubiertos)` a
    ''' proposito: VB evalua los argumentos ANTES de llamar, asi que ese Concat corria el descubrimiento
    ''' —que recorre el diccionario entero— aunque el primer nombre preferido acertara. Con las fases
    ''' separadas, el caso normal (el idioma del INI existe) son tres busquedas por clave y nada mas.</para></summary>
    Private Function FindResourceLocation(pluginBase As String, kind As LocalizedStringTableKind) As ResourceLocation
        AvisarSiElDiccionarioNoEstaMontado()

        For Each relativePath In BuildPreferredResourceNames(pluginBase, kind)
            Dim porIdioma = ArmarUbicacion(relativePath)
            If porIdioma IsNot Nothing Then Return porIdioma
        Next

        For Each relativePath In DiscoverCandidates(pluginBase, kind)
            Dim descubierta = ArmarUbicacion(relativePath)
            If descubierta IsNot Nothing Then Return descubierta
        Next

        Return Nothing
    End Function

    ''' <summary>Grita UNA vez si se pide un texto sin que nadie haya escaneado Data.
    ''' <para>Este resolver depende del diccionario del preflight. Si no se monto, no hay ninguna tabla que
    ''' encontrar y CADA nombre sale vacio — con la forma exacta de "este plugin no trae textos", que es un
    ''' caso legitimo. Un arnes que se olvide de llamar a `Fill_DictionaryAsync` mediria entonces una
    ''' resolucion de nombres que no ocurre y la reportaria como rapidisima. El aviso es lo que separa "no
    ''' hay nada que resolver" de "no se puede resolver".</para></summary>
    Private Sub AvisarSiElDiccionarioNoEstaMontado()
        If _avisoDeDiccionarioDado Then Return
        If FilesDictionary_class.ScanGeneration <> 0 Then Return
        _avisoDeDiccionarioDado = True
        Logger.LogLazy(Function() "[Strings] se pidio un texto externo y el diccionario de Data NUNCA se " &
                                  "escaneo (ScanGeneration = 0): TODOS los nombres van a salir vacios. " &
                                  "Falta un Fill_DictionaryAsync antes de leer records.")
    End Sub

    Private _avisoDeDiccionarioDado As Boolean

    Private Function ArmarUbicacion(relativePath As String) As ResourceLocation
        Dim entrada = FilesDictionary_class.TryGetEntry(relativePath)
        If entrada Is Nothing Then Return Nothing

        Dim suelto As String = ""
        If entrada.IsLosseFile Then suelto = Path.Combine(_dataPath, entrada.FullPath)

        Return New ResourceLocation With {
            .DictionaryKey = entrada.FullPath,
            .DisplayName = entrada.FullPath,
            .LoosePath = suelto
        }
    End Function

    Private Function BuildPreferredResourceNames(pluginBase As String, kind As LocalizedStringTableKind) As IEnumerable(Of String)
        Dim ext = GetExtension(kind)
        Return _preferredLanguages.Select(Function(lang) $"Strings\{pluginBase}_{lang}{ext}")
    End Function

    ''' <summary>Los textos de este plugin que hay en Data en CUALQUIER idioma, por orden de preferencia.
    ''' Es el camino para cuando ninguno de los idiomas preferidos existe: un plugin traducido puede
    ''' shippear un unico sufijo que no esta en la lista.</summary>
    Private Function DiscoverCandidates(pluginBase As String, kind As LocalizedStringTableKind) As IEnumerable(Of String)
        Dim ext = GetExtension(kind)
        Dim prefix = $"Strings\{pluginBase}_"

        Return OrderByLanguagePreference(ClavesDeStrings().
            Where(Function(key) key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) AndAlso
                                key.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
    End Function

    ''' <summary>Los paths bajo `Strings\` del diccionario, cacheados por generacion de scan.
    ''' <para>Una pasada directa sobre las claves, no `GetFilteredKeys`: esa consulta construye —la
    ''' primera vez que alguien la usa— un indice por (directorio, extension) de TODO Data, y hacer que
    ''' lo dispare la primera resolucion de un nombre pone medio segundo donde no corresponde. Aca hace
    ''' falta UN directorio.</para></summary>
    Private Function ClavesDeStrings() As List(Of String)
        SyncLock _syncRoot
            InvalidarSiCambioElDiccionario()
            If _clavesStrings IsNot Nothing Then Return _clavesStrings

            ' El sello se lee ANTES de copiar, no despues: si un scan termina entre la copia y la lectura,
            ' esta lista a medias quedaria estampada con la generacion NUEVA y no se invalidaria nunca mas —
            ' y con ella se congelarian tambien las dos caches de tablas, que usan el mismo sello.
            Dim gen = FilesDictionary_class.ScanGeneration
            Dim encontradas As New List(Of String)
            For Each clave In FilesDictionary_class.Dictionary.Keys
                If clave.StartsWith("Strings\", StringComparison.OrdinalIgnoreCase) Then encontradas.Add(clave)
            Next
            _clavesStrings = encontradas
            _generacionDeLasClaves = gen
            Return _clavesStrings
        End SyncLock
    End Function

    ''' <summary>Tira todo lo derivado del diccionario si Data se re-escaneo. Se llama con `_syncRoot`
    ''' tomado. Sin esto, un re-scan (montar o desmontar archives, cambiar la seleccion de plugins) deja
    ''' vivas tablas de textos del load order ANTERIOR, que es exactamente el resultado equivocado.</summary>
    Private Sub InvalidarSiCambioElDiccionario()
        Dim gen = FilesDictionary_class.ScanGeneration
        If gen = _generacionDeLasClaves Then Return
        _clavesStrings = Nothing
        _resourceCache.Clear()
        _tableCache.Clear()
        _generacionDeLasClaves = gen
    End Sub

    Private Function OrderByLanguagePreference(paths As IEnumerable(Of String)) As IEnumerable(Of String)
        Return paths.
            Where(Function(path) Not String.IsNullOrWhiteSpace(path)).
            Distinct(StringComparer.OrdinalIgnoreCase).
            OrderBy(Function(path)
                        Dim lang = ExtractLanguageFromResource(path)
                        Dim idx = _preferredLanguages.FindIndex(Function(candidate) String.Equals(candidate, lang, StringComparison.OrdinalIgnoreCase))
                        If idx >= 0 Then Return idx
                        Return Integer.MaxValue
                    End Function).
            ThenBy(Function(path) path, StringComparer.OrdinalIgnoreCase).
            ToList()
    End Function

    Private Shared Function GetExtension(kind As LocalizedStringTableKind) As String
        Return LocalizedStringExtensions.ExtensionDe(kind)
    End Function

    Private Shared Function BuildPreferredLanguageList() As List(Of String)
        ' El comportamiento estándar resuelve el idioma sólo con sLanguage (INI); si falta el sidecar
        ' STRINGS de ese idioma, el resultado es un marcador de error tipo "<Error: Unknown lstring ID>".
        ' We diverge here by also probing CurrentUICulture and "english" as fallbacks — the
        ' english fallback is justified because every vanilla FO4 plugin ships English STRINGS.
        ' No language aliases (la búsqueda es directa por token literal, no hay tabla de alias).
        Dim result As New List(Of String)

        AddLanguage(result, ReadLanguageFromIni())
        AddLanguage(result, Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName)
        AddLanguage(result, "english")

        Return result
    End Function

    Private Shared Sub AddLanguage(target As List(Of String), language As String)
        ' Direct token add (no alias canonicalization — la búsqueda es directa por token).
        Dim normalized = PluginTextDecoding.NormalizeLanguage(language)
        If normalized = "" Then Return
        If target.Exists(Function(entry) String.Equals(entry, normalized, StringComparison.OrdinalIgnoreCase)) Then Return
        target.Add(normalized)
    End Sub

    Private Shared Function ReadLanguageFromIni() As String
        ' GAME-AWARE: FO4 = My Games\Fallout4\Fallout4[Custom/Prefs].ini; SSE = My Games\Skyrim Special
        ' Edition\Skyrim[Custom/Prefs].ini. Hardcoding Fallout4 read the wrong game's language on SSE.
        ' VR folder selection (and VR's game-root fallback) lives in PluginManager.ResolveGameIniPath,
        ' which picks the folder from the configured exe. The ini FILE names are the same in VR.
        Dim isSse = (Config_App.Current IsNot Nothing AndAlso Config_App.Current.Game = Config_App.Game_Enum.Skyrim)
        Dim iniFiles = If(isSse,
            {PluginManager.ResolveGameIniPath("SkyrimCustom.ini"), PluginManager.ResolveGameIniPath("Skyrim.ini"), PluginManager.ResolveGameIniPath("SkyrimPrefs.ini")},
            {PluginManager.ResolveGameIniPath("Fallout4Custom.ini"), PluginManager.ResolveGameIniPath("Fallout4.ini"), PluginManager.ResolveGameIniPath("Fallout4Prefs.ini")})

        For Each iniPath In iniFiles
            If Not File.Exists(iniPath) Then Continue For

            For Each rawLine In File.ReadLines(iniPath)
                Dim line = rawLine.Trim()
                If line.StartsWith("sLanguage=", StringComparison.OrdinalIgnoreCase) Then
                    Return line.Substring("sLanguage=".Length).Trim()
                End If
            Next
        Next

        Return ""
    End Function

    Private Shared Function ExtractLanguageFromResource(resourcePath As String) As String
        Dim fileName = Path.GetFileNameWithoutExtension(resourcePath)
        Dim underscore = fileName.LastIndexOf("_"c)
        If underscore < 0 OrElse underscore >= fileName.Length - 1 Then Return ""
        Return PluginTextDecoding.NormalizeLanguage(fileName.Substring(underscore + 1))
    End Function
End Class
