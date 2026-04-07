Imports System.IO
Imports System.Text

Public Enum LocalizedStringTableKind
    Strings
    DLStrings
    ILStrings
End Enum

Friend Module PluginTextDecoding
    Private ReadOnly _strictUtf8 As New UTF8Encoding(False, True)
    Private ReadOnly _encodingCache As New Dictionary(Of Integer, Encoding)()
    Private ReadOnly _localizedCodePages As New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase) From {
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

    Public Function DecodePluginString(data As Byte(), offset As Integer, count As Integer) As String
        Return DecodeWithEncoding(data, offset, count, GetCodePageEncoding(1252), Nothing)
    End Function

    Public Function DecodeLocalizedString(data As Byte(), offset As Integer, count As Integer, primary As Encoding, fallback As Encoding) As String
        Return DecodeWithEncoding(data, offset, count, primary, fallback)
    End Function

    Public Function NormalizeLanguage(language As String) As String
        Dim normalized = If(language, "").Trim().ToLowerInvariant()
        If normalized = "" Then Return ""
        Return normalized.Replace(" ", "")
    End Function

    Public Function CanonicalizeLanguageAlias(language As String) As String
        Dim normalized = NormalizeLanguage(language)

        Select Case normalized
            Case "en", "eng"
                Return "english"
            Case "es", "spa", "espanol"
                Return "spanish"
            Case "fr", "fre", "fra"
                Return "french"
            Case "de", "ger", "deu"
                Return "german"
            Case "it", "ita"
                Return "italian"
            Case "ja", "jpn"
                Return "japanese"
            Case "pl", "pol"
                Return "polish"
            Case "pt", "ptbr", "por", "brazilian"
                Return "portuguese"
            Case "ru", "rus"
                Return "russian"
            Case "zh", "chi", "zhhans", "zhhant"
                Return "chinese"
            Case Else
                Return normalized
        End Select
    End Function

    Public Function GetLocalizationPrimaryEncoding(language As String) As Encoding
        Return GetLocalizationEncoding(language, fallback:=False)
    End Function

    Public Function GetLocalizationFallbackEncoding(language As String) As Encoding
        Return GetLocalizationEncoding(language, fallback:=True)
    End Function

    Public Function TryGetCodePageOverride(stringsFilePath As String) As Encoding
        If String.IsNullOrWhiteSpace(stringsFilePath) Then Return Nothing

        Dim overridePath = Path.ChangeExtension(stringsFilePath, ".cpoverride")
        If Not File.Exists(overridePath) Then Return Nothing

        Try
            Dim firstLine = File.ReadLines(overridePath).FirstOrDefault()
            Dim value = If(firstLine, "").Trim()
            If value = "" Then Return Nothing
            Return ParseEncoding(value)
        Catch
            Return Nothing
        End Try
    End Function

    Private Function GetLocalizationEncoding(language As String, fallback As Boolean) As Encoding
        Dim normalized = NormalizeLanguage(language)
        Dim codePage As Integer = 0
        If _localizedCodePages.TryGetValue(normalized, codePage) Then
            Return ParseEncoding(codePage.ToString())
        End If

        If fallback Then
            Return GetCodePageEncoding(1252)
        End If

        Return _strictUtf8
    End Function

    Private Function ParseEncoding(value As String) As Encoding
        Dim normalized = If(value, "").Trim().ToLowerInvariant()
        If normalized = "" Then Return Nothing
        If normalized = "utf8" OrElse normalized = "utf-8" OrElse normalized = "65001" Then Return _strictUtf8
        If normalized.StartsWith("windows-") Then normalized = normalized.Substring("windows-".Length)

        Dim codePage As Integer
        If Integer.TryParse(normalized, codePage) Then
            If codePage = 65001 Then Return _strictUtf8
            Return GetCodePageEncoding(codePage)
        End If

        Return Encoding.GetEncoding(value, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback)
    End Function

    Private Function GetCodePageEncoding(codePage As Integer) As Encoding
        SyncLock _encodingCache
            Dim enc As Encoding = Nothing
            If _encodingCache.TryGetValue(codePage, enc) Then Return enc

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)
            enc = Encoding.GetEncoding(codePage, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback)
            _encodingCache(codePage) = enc
            Return enc
        End SyncLock
    End Function

    Private Function DecodeWithEncoding(data As Byte(), offset As Integer, count As Integer, primary As Encoding, fallback As Encoding) As String
        If data Is Nothing OrElse count <= 0 Then Return ""
        If offset < 0 Then offset = 0
        If offset >= data.Length Then Return ""
        count = Math.Min(count, data.Length - offset)
        If count <= 0 Then Return ""

        If primary Is Nothing Then primary = GetCodePageEncoding(1252)

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
    Private ReadOnly _values As New Dictionary(Of UInteger, String)()
    Private ReadOnly _primaryEncoding As Encoding
    Private ReadOnly _fallbackEncoding As Encoding

    Public Sub New(resourceName As String, kind As LocalizedStringTableKind, data As Byte(), Optional looseFilePath As String = "")
        _kind = kind

        Dim language = ExtractLanguageToken(resourceName)
        Dim primary = PluginTextDecoding.GetLocalizationPrimaryEncoding(language)
        Dim fallback = PluginTextDecoding.GetLocalizationFallbackEncoding(language)
        Dim overrideEncoding = PluginTextDecoding.TryGetCodePageOverride(looseFilePath)

        If overrideEncoding IsNot Nothing Then
            primary = overrideEncoding
        End If

        If fallback IsNot Nothing AndAlso primary IsNot Nothing AndAlso String.Equals(primary.WebName, fallback.WebName, StringComparison.OrdinalIgnoreCase) Then
            fallback = Nothing
        End If

        _primaryEncoding = primary
        _fallbackEncoding = fallback
        Parse(data)
    End Sub

    Public Function Resolve(stringId As UInteger) As String
        Dim value As String = Nothing
        If _values.TryGetValue(stringId, value) Then
            Return value
        End If
        Return ""
    End Function

    Private Sub Parse(data As Byte())
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

            Dim value = ReadValue(data, CInt(absoluteOffset))
            _values(stringId) = value
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
    Private NotInheritable Class ResourceLocation
        Public Property LoosePath As String = ""
        Public Property ArchivePath As String = ""
        Public Property EntryIndex As Integer = -1
        Public Property DisplayName As String = ""

        Public ReadOnly Property CacheKey As String
            Get
                If LoosePath <> "" Then Return LoosePath
                Return $"{ArchivePath}|{EntryIndex}|{DisplayName}"
            End Get
        End Property

        Public Function ReadAllBytes() As Byte()
            If LoosePath <> "" Then
                If File.Exists(LoosePath) Then
                    Return File.ReadAllBytes(LoosePath)
                End If
                Return Array.Empty(Of Byte)()
            End If

            If ArchivePath = "" OrElse EntryIndex < 0 OrElse Not File.Exists(ArchivePath) Then
                Return Array.Empty(Of Byte)()
            End If

            Using fs As FileStream = File.OpenRead(ArchivePath)
                Using reader As New BSA_BA2_Library_DLL.BethesdaArchive.Core.BethesdaReader(fs)
                    Return reader.ExtractToMemory(EntryIndex)
                End Using
            End Using
        End Function
    End Class

    Private ReadOnly _dataPath As String
    Private ReadOnly _preferredLanguages As List(Of String)
    Private ReadOnly _tableCache As New Dictionary(Of String, LocalizedStringTable)(StringComparer.OrdinalIgnoreCase)
    Private ReadOnly _resourceCache As New Dictionary(Of String, ResourceLocation)(StringComparer.OrdinalIgnoreCase)
    Private ReadOnly _archiveStringIndex As New Dictionary(Of String, ResourceLocation)(StringComparer.OrdinalIgnoreCase)
    Private ReadOnly _syncRoot As New Object()
    Private _archiveIndexBuilt As Boolean

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

        Dim bytes = location.ReadAllBytes()
        If bytes Is Nothing OrElse bytes.Length = 0 Then Return Nothing

        Dim table = New LocalizedStringTable(location.DisplayName, kind, bytes, location.LoosePath)

        SyncLock _syncRoot
            _tableCache(location.CacheKey) = table
        End SyncLock

        Return table
    End Function

    Private Function GetResourceLocation(pluginFileName As String, kind As LocalizedStringTableKind) As ResourceLocation
        Dim pluginBase = Path.GetFileNameWithoutExtension(pluginFileName)
        Dim cacheKey = $"{pluginBase}|{CInt(kind)}"

        SyncLock _syncRoot
            If _resourceCache.ContainsKey(cacheKey) Then
                Return _resourceCache(cacheKey)
            End If
        End SyncLock

        Dim found = FindResourceLocation(pluginBase, kind)

        SyncLock _syncRoot
            _resourceCache(cacheKey) = found
        End SyncLock

        Return found
    End Function

    Private Function FindResourceLocation(pluginBase As String, kind As LocalizedStringTableKind) As ResourceLocation
        Dim preferred = BuildPreferredResourceNames(pluginBase, kind)

        For Each relativePath In preferred.Concat(DiscoverLooseCandidates(pluginBase, kind))
            Dim fullPath = Path.Combine(_dataPath, relativePath.Replace("\"c, Path.DirectorySeparatorChar))
            If File.Exists(fullPath) Then
                Return New ResourceLocation With {
                    .LoosePath = fullPath,
                    .DisplayName = relativePath
                }
            End If
        Next

        EnsureArchiveIndex()

        For Each relativePath In preferred.Concat(DiscoverArchiveCandidates(pluginBase, kind))
            Dim archived As ResourceLocation = Nothing
            SyncLock _syncRoot
                If _archiveStringIndex.TryGetValue(relativePath, archived) Then
                    Return archived
                End If
            End SyncLock
        Next

        Return Nothing
    End Function

    Private Function BuildPreferredResourceNames(pluginBase As String, kind As LocalizedStringTableKind) As IEnumerable(Of String)
        Dim ext = GetExtension(kind)
        Return _preferredLanguages.Select(Function(lang) $"Strings\{pluginBase}_{lang}{ext}")
    End Function

    Private Function DiscoverLooseCandidates(pluginBase As String, kind As LocalizedStringTableKind) As IEnumerable(Of String)
        Dim stringsDir = Path.Combine(_dataPath, "Strings")
        If Not Directory.Exists(stringsDir) Then Return Enumerable.Empty(Of String)()

        Dim pattern = $"{pluginBase}_*{GetExtension(kind)}"
        Dim matches = Directory.EnumerateFiles(stringsDir, pattern, SearchOption.TopDirectoryOnly).
            Select(Function(path) $"Strings\{IO.Path.GetFileName(path)}")

        Return OrderByLanguagePreference(matches)
    End Function

    Private Function DiscoverArchiveCandidates(pluginBase As String, kind As LocalizedStringTableKind) As IEnumerable(Of String)
        Dim ext = GetExtension(kind)
        Dim prefix = $"Strings\{pluginBase}_"

        SyncLock _syncRoot
            Return OrderByLanguagePreference(_archiveStringIndex.Keys.
                Where(Function(key) key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) AndAlso
                                     key.EndsWith(ext, StringComparison.OrdinalIgnoreCase)).
                ToList())
        End SyncLock
    End Function

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

    Private Sub EnsureArchiveIndex()
        SyncLock _syncRoot
            If _archiveIndexBuilt Then Return
        End SyncLock

        Dim tempIndex As New Dictionary(Of String, ResourceLocation)(StringComparer.OrdinalIgnoreCase)
        Dim archives = EnumerateArchivePathsByPriority()

        For Each archivePath In archives
            Try
                Using fs As FileStream = File.OpenRead(archivePath)
                    Using reader As New BSA_BA2_Library_DLL.BethesdaArchive.Core.BethesdaReader(fs)
                        For Each entry In reader.EntriesFiles
                            Dim fullPath = NormalizeArchivePath(entry.FullPath)
                            If Not fullPath.StartsWith("Strings\", StringComparison.OrdinalIgnoreCase) Then Continue For
                            If Not IsLocalizationExtension(Path.GetExtension(fullPath)) Then Continue For

                            tempIndex(fullPath) = New ResourceLocation With {
                                .ArchivePath = archivePath,
                                .EntryIndex = entry.Index,
                                .DisplayName = fullPath
                            }
                        Next
                    End Using
                End Using
            Catch ex As Exception
                Debug.Print($"[Strings] Failed to scan archive {Path.GetFileName(archivePath)}: {ex.Message}")
            End Try
        Next

        SyncLock _syncRoot
            If _archiveIndexBuilt Then Return
            _archiveStringIndex.Clear()
            For Each kvp In tempIndex
                _archiveStringIndex(kvp.Key) = kvp.Value
            Next
            _archiveIndexBuilt = True
        End SyncLock
    End Sub

    Private Function EnumerateArchivePathsByPriority() As List(Of String)
        Dim archives = Directory.EnumerateFiles(_dataPath, "*.ba2", SearchOption.TopDirectoryOnly).
            Concat(Directory.EnumerateFiles(_dataPath, "*.bsa", SearchOption.TopDirectoryOnly)).
            Distinct(StringComparer.OrdinalIgnoreCase).
            ToList()

        Dim archivePriority As New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)
        Dim nextOrder = 0

        Dim baseAndDlcOrder = {
            "Fallout4",
            "DLCRobot",
            "DLCworkshop01",
            "DLCCoast",
            "DLCworkshop02",
            "DLCworkshop03",
            "DLCNukaWorld",
            "DLCUltraHighResolution"
        }

        Dim pending = New HashSet(Of String)(archives.Select(Function(path) IO.Path.GetFileName(path)), StringComparer.OrdinalIgnoreCase)

        For Each prefix In baseAndDlcOrder
            Dim matches = pending.
                Where(Function(name) Path.GetFileNameWithoutExtension(name).StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).
                OrderBy(Function(name) name, StringComparer.OrdinalIgnoreCase).
                ToList()

            For Each match In matches
                archivePriority(match) = nextOrder
                nextOrder += 1
                pending.Remove(match)
            Next
        Next

        For Each plugin In PluginManager.ReadLoadOrder()
            Dim matches = pending.
                Where(Function(name) ArchiveBelongsToPlugin(name, plugin)).
                OrderBy(Function(name) name, StringComparer.OrdinalIgnoreCase).
                ToList()

            For Each match In matches
                archivePriority(match) = nextOrder
                nextOrder += 1
                pending.Remove(match)
            Next
        Next

        For Each match In pending.OrderBy(Function(name) name, StringComparer.OrdinalIgnoreCase)
            archivePriority(match) = nextOrder
            nextOrder += 1
        Next

        Return archives.
            OrderBy(Function(path) archivePriority(IO.Path.GetFileName(path))).
            ThenBy(Function(path) path, StringComparer.OrdinalIgnoreCase).
            ToList()
    End Function

    Private Shared Function ArchiveBelongsToPlugin(archiveFileName As String, pluginFileName As String) As Boolean
        Dim archiveBase = Path.GetFileNameWithoutExtension(archiveFileName)
        Dim pluginBase = Path.GetFileNameWithoutExtension(pluginFileName)
        If archiveBase.Equals(pluginBase, StringComparison.OrdinalIgnoreCase) Then Return True
        If archiveBase.StartsWith(pluginBase & " - ", StringComparison.OrdinalIgnoreCase) Then Return True
        Return False
    End Function

    Private Shared Function NormalizeArchivePath(path As String) As String
        Return path.Replace("/"c, "\"c)
    End Function

    Private Shared Function IsLocalizationExtension(ext As String) As Boolean
        Return ext.Equals(".STRINGS", StringComparison.OrdinalIgnoreCase) OrElse
               ext.Equals(".DLSTRINGS", StringComparison.OrdinalIgnoreCase) OrElse
               ext.Equals(".ILSTRINGS", StringComparison.OrdinalIgnoreCase)
    End Function

    Private Shared Function GetExtension(kind As LocalizedStringTableKind) As String
        Select Case kind
            Case LocalizedStringTableKind.DLStrings
                Return ".DLSTRINGS"
            Case LocalizedStringTableKind.ILStrings
                Return ".ILSTRINGS"
            Case Else
                Return ".STRINGS"
        End Select
    End Function

    Private Function BuildPreferredLanguageList() As List(Of String)
        Dim result As New List(Of String)

        AddLanguage(result, ReadLanguageFromIni())
        AddLanguage(result, Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName)
        AddLanguage(result, "english")
        AddLanguage(result, "spanish")

        Return result
    End Function

    Private Shared Sub AddLanguage(target As List(Of String), language As String)
        Dim normalized = PluginTextDecoding.NormalizeLanguage(language)
        If normalized = "" Then Return

        Dim candidates = New List(Of String) From {normalized}
        Dim canonical = PluginTextDecoding.CanonicalizeLanguageAlias(normalized)
        If canonical <> normalized Then candidates.Add(canonical)

        For Each candidate In candidates
            If target.Exists(Function(entry) String.Equals(entry, candidate, StringComparison.OrdinalIgnoreCase)) Then Continue For
            target.Add(candidate)
        Next
    End Sub

    Private Shared Function ReadLanguageFromIni() As String
        Dim documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        Dim iniDir = Path.Combine(documents, "My Games", "Fallout4")
        Dim iniFiles = {
            Path.Combine(iniDir, "Fallout4Custom.ini"),
            Path.Combine(iniDir, "Fallout4.ini"),
            Path.Combine(iniDir, "Fallout4Prefs.ini")
        }

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