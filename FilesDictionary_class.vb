' Version Uploaded of Wardrobe 3.1.0
Imports System.Collections.Concurrent
Imports System.IO
Imports System.Runtime.CompilerServices
Imports System.Text
Imports System.Threading
Imports System.Timers
Imports NiflySharp.Enums

Public Module Extensiones
    Public Const MaterialsPrefix As String = "Materials\"
    Public Const TexturesPrefix As String = "Textures\"

    <Extension>
    Public Function Correct_Path_Separator(St As String) As String
        If IsNothing(St) Then Return ""
        Return St.Replace("/", "\")
    End Function

    ''' <summary>Removes prefix (case-insensitive) from the start of the string if present.</summary>
    <Extension>
    Public Function StripPrefix(St As String, prefix As String) As String
        If Not IsNothing(St) AndAlso St.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) Then
            Return St.Substring(prefix.Length)
        End If
        Return St
    End Function
End Module

Public Class FilesDictionary_class
    Public Shared Property TexturesDictionary_Filter As New FilesDictionary_class.DictionaryFilePickerConfig With {.DictionaryProvider = Function() FilesDictionary_class.Dictionary, .RootPrefix = TexturesPrefix, .AllowedExtensions = New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {".dds"}}
    Public Shared Property MaterialsDictionary_Filter As New FilesDictionary_class.DictionaryFilePickerConfig With {.DictionaryProvider = Function() FilesDictionary_class.Dictionary, .RootPrefix = MaterialsPrefix, .AllowedExtensions = New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {".bgsm", ".bgem"}}
    Public Shared Property MaterialsDictionary_BGEM_Filter As New FilesDictionary_class.DictionaryFilePickerConfig With {.DictionaryProvider = Function() FilesDictionary_class.Dictionary, .RootPrefix = MaterialsPrefix, .AllowedExtensions = New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {".bgem"}}
    Public Shared Property MaterialsDictionary_BGSM_Filter As New FilesDictionary_class.DictionaryFilePickerConfig With {.DictionaryProvider = Function() FilesDictionary_class.Dictionary, .RootPrefix = MaterialsPrefix, .AllowedExtensions = New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {".bgsm"}}
    Public Shared Property MeshesDictionary_Filter As New FilesDictionary_class.DictionaryFilePickerConfig With {.DictionaryProvider = Function() FilesDictionary_class.Dictionary, .RootPrefix = "Meshes\", .AllowedExtensions = New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {".nif"}}
    Public Shared Property ALLMeshesDictionary_Filter As New FilesDictionary_class.DictionaryFilePickerConfig With {.DictionaryProvider = Function() FilesDictionary_class.Dictionary, .RootPrefix = "", .AllowedExtensions = New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {".nif"}}
    Public Class DictionaryFilePickerConfig
        ' Debe apuntar a tu ConcurrentDictionary(Of String, File_Location)
        Public Property DictionaryProvider As Func(Of ConcurrentDictionary(Of String, FilesDictionary_class.File_Location))

        ' Prefijo raíz (case-insensitive). Default: "Textures\"
        Public Property RootPrefix As String = TexturesPrefix

        ' Extensiones permitidas (case-insensitive). Default: ".dds"
        Private _allowedExtensions As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {".dds"}

        Public Property AllowedExtensions As HashSet(Of String)
            Get
                Return _allowedExtensions
            End Get
            Set(value As HashSet(Of String))
                _allowedExtensions = value
            End Set
        End Property

        Public Sub SetAllowedExtensions(exts As IEnumerable(Of String))
            ArgumentNullException.ThrowIfNull(exts)
            _allowedExtensions = New HashSet(Of String)(exts, StringComparer.OrdinalIgnoreCase)
        End Sub

        Public Function ExtensionAllowed(normalized As String) As Boolean
            Dim fileName = normalized
            Dim iSlash = normalized.LastIndexOf("\"c)
            If iSlash >= 0 AndAlso iSlash < normalized.Length - 1 Then
                fileName = normalized.Substring(iSlash + 1)
            End If
            Dim iDot = fileName.LastIndexOf("."c)
            If iDot < 0 Then Return False
            Dim ext = fileName.Substring(iDot)
            Return AllowedExtensions.Contains(ext)
        End Function
        Public Shared Function PathStartsWithRoot(normalized As String, rootPrefix As String) As Boolean
            If String.IsNullOrEmpty(rootPrefix) Then Return True
            Return normalized.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase)
        End Function
    End Class

    Private Class DictionaryScanWorkItem
        Public Property IsArchive As Boolean
        Public Property FilePath As String = ""
        Public Property SourceOrder As Integer = Integer.MinValue
    End Class
    Public Class File_Location

        Public Property BA2File As String = ""
        Public Property Index As Integer = -1
        Public Property FullPath As String = ""
        Public Property SourceOrder As Integer = Integer.MinValue
        Public Property FileDate As Date = Date.MinValue

        Public Function GetBytesFromOpenArchive(pack As BSA_BA2_Library_DLL.BethesdaArchive.Core.BethesdaReader) As Byte()
            If IsNothing(pack) OrElse IsLosseFile Then Return Array.Empty(Of Byte)
            Try
                Return pack.ExtractToMemory(Index)
            Catch
                Return Array.Empty(Of Byte)
            End Try
        End Function

        Public ReadOnly Property IsLosseFile As Boolean
            Get
                Return BA2File = ""
            End Get
        End Property
        Public Function GetBytes() As Byte()
            ' O1.1: Check WeakReference byte cache first
            Dim cached As Byte() = Nothing
            Dim weakRef As WeakReference(Of Byte()) = Nothing
            If FilesDictionary_class._bytesCache.TryGetValue(FullPath, weakRef) Then
                If weakRef.TryGetTarget(cached) Then Return cached
            End If

            Dim result As Byte()

            If IsLosseFile Then
                If IO.File.Exists(IO.Path.Combine(FO4Path, Me.FullPath)) = False Then Return Array.Empty(Of Byte)
                result = IO.File.ReadAllBytes(IO.Path.Combine(FO4Path, Me.FullPath))
            Else
                ' O1.2: Use archive reader pool instead of opening/closing each time
                Dim archivePath = IO.Path.Combine(FO4Path, Me.BA2File)
                Dim leased As (Reader As BSA_BA2_Library_DLL.BethesdaArchive.Core.BethesdaReader, Stream As FileStream) = Nothing
                Dim returned As Boolean = False
                Try
                    leased = FilesDictionary_class.LeaseReader(archivePath)
                    result = leased.Reader.ExtractToMemory(Index)
                    FilesDictionary_class.ReturnReader(archivePath, leased)
                    returned = True
                Catch ex As Exception
                    ' On error, dispose the leased reader rather than returning it
                    If Not returned Then
                        If leased.Reader IsNot Nothing Then
                            Try : leased.Reader.Dispose() : Catch : End Try
                        End If
                        If leased.Stream IsNot Nothing Then
                            Try : leased.Stream.Dispose() : Catch : End Try
                        End If
                    End If
                    Return Array.Empty(Of Byte)
                End Try
            End If

            ' O1.1: Store result in WeakReference cache
            If result IsNot Nothing AndAlso result.Length > 0 Then
                FilesDictionary_class._bytesCache(FullPath) = New WeakReference(Of Byte())(result)
            End If

            Return result
        End Function

    End Class
    Private Shared _sliderPresets As IPresetCollection = Nothing
    Private Shared _fO4Path As String = ""
    Private Shared _dictionary As New ConcurrentDictionary(Of String, File_Location)(StringComparer.OrdinalIgnoreCase)
    ''' <summary>Stack of overridden entries per key. When a loose overrides a BA2 (or a BA2 overrides another), the loser is pushed here.</summary>
    Private Shared ReadOnly _overriddenEntries As New ConcurrentDictionary(Of String, ConcurrentStack(Of File_Location))(StringComparer.OrdinalIgnoreCase)
    Private Shared ReadOnly Extensiones As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {".dds", ".bgsm", ".bgem", ".nif", ".tri"}

    ' O1.1: Lazy byte cache with WeakReference — allows GC to reclaim when memory is needed
    Private Shared ReadOnly _bytesCache As New ConcurrentDictionary(Of String, WeakReference(Of Byte()))(StringComparer.OrdinalIgnoreCase)

    ' O1.2: Archive reader pool — reuses BethesdaReader instances to avoid repeated open/close
    Private Shared ReadOnly _archivePool As New ConcurrentDictionary(Of String, ConcurrentBag(Of (Reader As BSA_BA2_Library_DLL.BethesdaArchive.Core.BethesdaReader, Stream As FileStream)))(StringComparer.OrdinalIgnoreCase)
    Private Shared ReadOnly MaxPooledReadersPerArchive As Integer = 2
    Private Shared _poolCleanupTimer As System.Timers.Timer

    Private Shared Sub InitPoolCleanupTimer()
        If _poolCleanupTimer IsNot Nothing Then Return
        _poolCleanupTimer = New System.Timers.Timer(30000) ' 30 seconds
        AddHandler _poolCleanupTimer.Elapsed, Sub(sender, e) DisposeIdleReaders()
        _poolCleanupTimer.AutoReset = True
        _poolCleanupTimer.Start()
    End Sub

    ''' <summary>Lease a BethesdaReader from the pool, or create a new one if pool is empty.</summary>
    Private Shared Function LeaseReader(archivePath As String) As (Reader As BSA_BA2_Library_DLL.BethesdaArchive.Core.BethesdaReader, Stream As FileStream)
        ' Lazy-init the pool cleanup timer on first use
        InitPoolCleanupTimer()

        Dim bag As ConcurrentBag(Of (Reader As BSA_BA2_Library_DLL.BethesdaArchive.Core.BethesdaReader, Stream As FileStream)) = Nothing
        Dim entry As (Reader As BSA_BA2_Library_DLL.BethesdaArchive.Core.BethesdaReader, Stream As FileStream) = Nothing

        If _archivePool.TryGetValue(archivePath, bag) Then
            If bag.TryTake(entry) Then
                Return entry
            End If
        End If

        ' Create new reader
        Dim fs As FileStream = File.OpenRead(archivePath)
        Dim reader As New BSA_BA2_Library_DLL.BethesdaArchive.Core.BethesdaReader(fs)
        Return (reader, fs)
    End Function

    ''' <summary>Return a reader to the pool if below cap, otherwise dispose it.</summary>
    Private Shared Sub ReturnReader(archivePath As String, entry As (Reader As BSA_BA2_Library_DLL.BethesdaArchive.Core.BethesdaReader, Stream As FileStream))
        Dim bag = _archivePool.GetOrAdd(archivePath, Function(key) New ConcurrentBag(Of (Reader As BSA_BA2_Library_DLL.BethesdaArchive.Core.BethesdaReader, Stream As FileStream))())

        If bag.Count < MaxPooledReadersPerArchive Then
            bag.Add(entry)
        Else
            ' Over capacity — dispose
            Try
                entry.Reader.Dispose()
            Catch
            End Try
            Try
                entry.Stream.Dispose()
            Catch
            End Try
        End If
    End Sub

    ''' <summary>Dispose all pooled readers. Called by the 30-second cleanup timer.</summary>
    Private Shared Sub DisposeIdleReaders()
        For Each kvp In _archivePool
            Dim bag = kvp.Value
            Dim entry As (Reader As BSA_BA2_Library_DLL.BethesdaArchive.Core.BethesdaReader, Stream As FileStream) = Nothing
            While bag.TryTake(entry)
                Try
                    entry.Reader.Dispose()
                Catch
                End Try
                Try
                    entry.Stream.Dispose()
                Catch
                End Try
            End While
        Next

        ' Purge dead WeakReference entries from _bytesCache
        For Each key In _bytesCache.Keys
            Dim weakRef As WeakReference(Of Byte()) = Nothing
            If _bytesCache.TryGetValue(key, weakRef) Then
                Dim dummy As Byte() = Nothing
                If Not weakRef.TryGetTarget(dummy) Then
                    _bytesCache.TryRemove(key, weakRef)
                End If
            End If
        Next
    End Sub

    ''' <summary>Clear the byte cache (call when dictionary is rebuilt).</summary>
    Public Shared Sub ClearBytesCache()
        _bytesCache.Clear()
    End Sub

    Private Shared ReadOnly _KeysByExtension As New ConcurrentDictionary(Of String, ConcurrentDictionary(Of String, Byte))(StringComparer.OrdinalIgnoreCase)
    Private Shared ReadOnly _KeysByDirectory As New ConcurrentDictionary(Of String, ConcurrentDictionary(Of String, Byte))(StringComparer.OrdinalIgnoreCase)
    Private Shared ReadOnly _KeysByDirectoryExtension As New ConcurrentDictionary(Of String, ConcurrentDictionary(Of String, Byte))(StringComparer.OrdinalIgnoreCase)

    Public Shared Function GetBytes(File As String) As Byte()
        Dim located_File As File_Location = Nothing
        If Not Dictionary.TryGetValue(NormalizeDictionaryKey(File), located_File) Then
            Return Array.Empty(Of Byte)
        Else
            Return located_File.GetBytes
        End If
    End Function
    Private Shared Function EnumerateSupportedLooseFiles(root As String) As IEnumerable(Of String)
        Dim opts As New EnumerationOptions() With {
        .RecurseSubdirectories = True,
        .IgnoreInaccessible = True
    }

        Return Directory.
        EnumerateFiles(root, "*", opts).
        Where(Function(path) Extensiones.Contains(IO.Path.GetExtension(path)))
    End Function
    Public Shared Function GetMultipleFilesBytes(files As String()) As Byte()()
        If IsNothing(files) OrElse files.Length = 0 Then Return Array.Empty(Of Byte())()

        Dim output As Byte()() = New Byte(files.Length - 1)() {}
        Dim looseIndexes As New Dictionary(Of Integer, File_Location)
        Dim packedGroups As New Dictionary(Of String, List(Of (OutputIndex As Integer, Location As File_Location)))(StringComparer.OrdinalIgnoreCase)

        For i As Integer = 0 To files.Length - 1
            Dim normalizedPath As String = files(i).Correct_Path_Separator
            Dim located_File As File_Location = Nothing

            If Dictionary.TryGetValue(normalizedPath, located_File) = False OrElse IsNothing(located_File) Then
                output(i) = Array.Empty(Of Byte)
                Continue For
            End If

            If located_File.IsLosseFile Then
                looseIndexes.Add(i, located_File)
            Else
                Dim group As List(Of (OutputIndex As Integer, Location As File_Location)) = Nothing
                If packedGroups.TryGetValue(located_File.BA2File, group) = False Then
                    group = New List(Of (OutputIndex As Integer, Location As File_Location))()
                    packedGroups.Add(located_File.BA2File, group)
                End If
                group.Add((i, located_File))
            End If
        Next

        Parallel.ForEach(looseIndexes.Keys, Sub(i As Integer)
                                                Dim located_File As File_Location = looseIndexes(i)
                                                If Not IsNothing(located_File) Then
                                                    output(i) = located_File.GetBytes()
                                                Else
                                                    output(i) = Array.Empty(Of Byte)
                                                End If
                                            End Sub)

        Parallel.ForEach(packedGroups, Sub(group)
                                           Dim archivePath = IO.Path.Combine(FO4Path, group.Key)

                                           Try
                                               Using fs As FileStream = File.OpenRead(archivePath)
                                                   Using pack As New BSA_BA2_Library_DLL.BethesdaArchive.Core.BethesdaReader(fs)
                                                       For Each item In group.Value
                                                           Dim bytes = item.Location.GetBytesFromOpenArchive(pack)
                                                           output(item.OutputIndex) = bytes
                                                           ' Populate _bytesCache so subsequent GetBytes() calls hit the cache
                                                           ' instead of re-opening the archive (prefetch only warms OS cache otherwise)
                                                           If bytes IsNot Nothing AndAlso bytes.Length > 0 Then
                                                               _bytesCache(item.Location.FullPath) = New WeakReference(Of Byte())(bytes)
                                                           End If
                                                       Next
                                                   End Using
                                               End Using
                                           Catch
                                               For Each item In group.Value
                                                   output(item.OutputIndex) = Array.Empty(Of Byte)
                                               Next
                                           End Try
                                       End Sub)

        Return output
    End Function

    Private Shared totalCount As Integer
    Private Shared completed As Integer

    ''' <summary>Preset/pose collection. Typed as IPresetCollection for cross-assembly safety.</summary>
    Public Shared Property SliderPresets As IPresetCollection
        Get
            Return _sliderPresets
        End Get
        Set(value As IPresetCollection)
            _sliderPresets = value
        End Set
    End Property

    Public Shared Property FO4Path As String
        Get
            Return _fO4Path
        End Get
        Set(value As String)
            _fO4Path = value
        End Set
    End Property


    Public Shared Property Dictionary As ConcurrentDictionary(Of String, File_Location)
        Get
            Return _dictionary
        End Get
        Set(value As ConcurrentDictionary(Of String, File_Location))
            If IsNothing(value) Then
                _dictionary = New ConcurrentDictionary(Of String, File_Location)(StringComparer.OrdinalIgnoreCase)
            Else
                _dictionary = value
            End If

            _overriddenEntries.Clear()
            RebuildSearchIndexesFromDictionary()
        End Set
    End Property
    Private Shared Sub PushOverriddenEntry(normalized As String, loser As File_Location)
        Dim stack = _overriddenEntries.GetOrAdd(normalized, Function(key) New ConcurrentStack(Of File_Location)())
        stack.Push(loser)
    End Sub

    Private Shared Function NormalizeDictionaryKey(fullPath As String) As String
        If IsNothing(fullPath) Then Return ""
        ' O5.4: Intern dictionary keys to reduce GC pressure and enable reference equality
        Return String.Intern(fullPath.Correct_Path_Separator)
    End Function

    Private Shared Function NormalizeDirectoryKey(directoryPath As String) As String
        If IsNothing(directoryPath) Then Return ""
        Dim normalized = directoryPath.Correct_Path_Separator.Trim()

        While normalized.EndsWith("\"c, StringComparison.Ordinal)
            normalized = normalized.Substring(0, normalized.Length - 1)
        End While

        Return normalized
    End Function

    Private Shared Function NormalizeRootPrefix(rootPrefix As String) As String
        Dim normalized = NormalizeDirectoryKey(rootPrefix)
        If String.IsNullOrEmpty(normalized) Then Return ""
        Return normalized & "\"
    End Function

    Private Shared Function NormalizeExtensionKey(extension As String) As String
        If String.IsNullOrWhiteSpace(extension) Then Return ""
        Dim ext = extension.Trim()
        If ext.StartsWith("."c) = False Then ext = "." & ext
        Return ext.ToLowerInvariant()
    End Function

    Private Shared Function BuildDirectoryExtensionBucketKey(directoryPath As String, extension As String) As String
        Return NormalizeDirectoryKey(directoryPath) & "|" & NormalizeExtensionKey(extension)
    End Function

    Private Shared Sub AddKeyToSearchIndex(index As ConcurrentDictionary(Of String, ConcurrentDictionary(Of String, Byte)), bucketKey As String, fullKey As String)
        Dim bucket = index.GetOrAdd(bucketKey, Function(key) New ConcurrentDictionary(Of String, Byte)(StringComparer.OrdinalIgnoreCase))
        bucket.TryAdd(fullKey, 0)
    End Sub

    Private Shared Sub IndexDictionaryKey(fullKey As String)
        fullKey = NormalizeDictionaryKey(fullKey)
        If String.IsNullOrEmpty(fullKey) Then Exit Sub

        Dim directoryKey = NormalizeDirectoryKey(IO.Path.GetDirectoryName(fullKey))
        Dim extensionKey = NormalizeExtensionKey(IO.Path.GetExtension(fullKey))

        AddKeyToSearchIndex(_KeysByDirectory, directoryKey, fullKey)

        If extensionKey <> "" Then
            AddKeyToSearchIndex(_KeysByExtension, extensionKey, fullKey)
            AddKeyToSearchIndex(_KeysByDirectoryExtension, BuildDirectoryExtensionBucketKey(directoryKey, extensionKey), fullKey)
        End If
    End Sub

    Private Shared Sub ClearSearchIndexes()
        _KeysByExtension.Clear()
        _KeysByDirectory.Clear()
        _KeysByDirectoryExtension.Clear()
    End Sub

    Private Shared Sub RebuildSearchIndexesFromDictionary()
        ClearSearchIndexes()

        For Each key In _dictionary.Keys
            IndexDictionaryKey(key)
        Next
    End Sub

    Public Shared Function TryAddDictionaryEntry(fullPath As String, location As File_Location) As Boolean
        Dim normalized = NormalizeDictionaryKey(fullPath)
        If _dictionary.TryAdd(normalized, location) Then
            IndexDictionaryKey(normalized)
            ' Clear stale byte cache for this entry
            Dim dummy As WeakReference(Of Byte()) = Nothing
            _bytesCache.TryRemove(normalized, dummy)
            Return True
        End If
        Return False
    End Function

    ''' <summary>Adds a new entry or updates an existing one (e.g. a BA2 entry replaced by a loose file). Only BA2 entries are pushed to the override stack (loose-over-loose is the same file overwritten).</summary>
    Public Shared Sub AddOrUpdateDictionaryEntry(fullPath As String, location As File_Location)
        Dim normalized = NormalizeDictionaryKey(fullPath)
        _dictionary.AddOrUpdate(
            normalized,
            location,
            Function(key, existing)
                If Not existing.IsLosseFile Then PushOverriddenEntry(normalized, existing)
                Return location
            End Function)
        IndexDictionaryKey(normalized)
        Dim dummy As WeakReference(Of Byte()) = Nothing
        _bytesCache.TryRemove(normalized, dummy)
    End Sub

    ''' <summary>Removes the current entry. If an overridden entry exists (e.g. BA2 behind a loose), restores it.</summary>
    Public Shared Sub RemoveDictionaryEntry(fullPath As String)
        Dim normalized = NormalizeDictionaryKey(fullPath)
        Dim dummy As WeakReference(Of Byte()) = Nothing
        _bytesCache.TryRemove(normalized, dummy)

        ' Try to restore a previously overridden entry
        Dim stack As ConcurrentStack(Of File_Location) = Nothing
        Dim restored As File_Location = Nothing
        If _overriddenEntries.TryGetValue(normalized, stack) AndAlso stack.TryPop(restored) Then
            _dictionary(normalized) = restored
        Else
            Dim removed As File_Location = Nothing
            If _dictionary.TryRemove(normalized, removed) Then
                ' Remove from search indexes only if truly gone
                Dim directoryKey = NormalizeDirectoryKey(IO.Path.GetDirectoryName(normalized))
                Dim extensionKey = NormalizeExtensionKey(IO.Path.GetExtension(normalized))
                Dim bucket As ConcurrentDictionary(Of String, Byte) = Nothing
                If _KeysByDirectory.TryGetValue(directoryKey, bucket) Then bucket.TryRemove(normalized, 0)
                If extensionKey <> "" Then
                    If _KeysByExtension.TryGetValue(extensionKey, bucket) Then bucket.TryRemove(normalized, 0)
                    If _KeysByDirectoryExtension.TryGetValue(BuildDirectoryExtensionBucketKey(directoryKey, extensionKey), bucket) Then bucket.TryRemove(normalized, 0)
                End If
            End If
        End If
    End Sub

    ''' <summary>Returns the overridden entries for a key (from most recent to oldest), or empty if none.</summary>
    Public Shared Function GetOverriddenEntries(fullPath As String) As File_Location()
        Dim normalized = NormalizeDictionaryKey(fullPath)
        Dim stack As ConcurrentStack(Of File_Location) = Nothing
        If _overriddenEntries.TryGetValue(normalized, stack) Then
            Return stack.ToArray()
        End If
        Return Array.Empty(Of File_Location)()
    End Function

    Public Shared Function GetFilesInDirectory(directoryPath As String, allowedExtensions As IEnumerable(Of String)) As List(Of String)
        Dim directoryKey = NormalizeDirectoryKey(directoryPath)
        Dim results As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)

        Dim extensionSet As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        If Not IsNothing(allowedExtensions) Then
            For Each ext In allowedExtensions
                Dim normalizedExt = NormalizeExtensionKey(ext)
                If normalizedExt <> "" Then extensionSet.Add(normalizedExt)
            Next
        End If

        If extensionSet.Count = 0 Then
            Dim directoryBucket As ConcurrentDictionary(Of String, Byte) = Nothing
            If _KeysByDirectory.TryGetValue(directoryKey, directoryBucket) Then
                For Each key In directoryBucket.Keys
                    results.Add(key)
                Next
            End If
        Else
            For Each ext In extensionSet
                Dim bucketKey = BuildDirectoryExtensionBucketKey(directoryKey, ext)
                Dim directoryExtBucket As ConcurrentDictionary(Of String, Byte) = Nothing

                If _KeysByDirectoryExtension.TryGetValue(bucketKey, directoryExtBucket) Then
                    For Each key In directoryExtBucket.Keys
                        results.Add(key)
                    Next
                End If
            Next
        End If

        Return results.OrderBy(Function(k) k, StringComparer.OrdinalIgnoreCase).ToList()
    End Function

    Public Shared Function GetFileNamesInDirectory(directoryPath As String, allowedExtensions As IEnumerable(Of String)) As String()
        Return GetFilesInDirectory(directoryPath, allowedExtensions).
        Select(Function(k) IO.Path.GetFileName(k)).
        OrderBy(Function(k) k, StringComparer.OrdinalIgnoreCase).
        ToArray()
    End Function

    Public Shared Function GetFilteredKeys(config As DictionaryFilePickerConfig) As List(Of String)
        ArgumentNullException.ThrowIfNull(config)
        Return GetFilteredKeys(config.RootPrefix, config.AllowedExtensions)
    End Function

    Public Shared Function GetFilteredKeys(rootPrefix As String, allowedExtensions As IEnumerable(Of String)) As List(Of String)
        Dim normalizedRoot = NormalizeRootPrefix(rootPrefix)
        Dim results As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        Dim extensionSet As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)

        If Not IsNothing(allowedExtensions) Then
            For Each ext In allowedExtensions
                Dim normalizedExt = NormalizeExtensionKey(ext)
                If normalizedExt <> "" Then extensionSet.Add(normalizedExt)
            Next
        End If

        If extensionSet.Count = 0 Then Return New List(Of String)

        For Each ext In extensionSet
            Dim suffix = "|" & ext   ' ej: "|.dds"

            For Each bucketKey In _KeysByDirectoryExtension.Keys   ' recorre directorios, no archivos
                If Not bucketKey.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) Then Continue For
                If Not DictionaryFilePickerConfig.PathStartsWithRoot(bucketKey, normalizedRoot) Then Continue For

                Dim bucket As ConcurrentDictionary(Of String, Byte) = Nothing
                If _KeysByDirectoryExtension.TryGetValue(bucketKey, bucket) Then
                    For Each key In bucket.Keys
                        results.Add(key)
                    Next
                End If
            Next
        Next

        Return results.OrderBy(Function(k) k, StringComparer.OrdinalIgnoreCase).ToList()
    End Function

    Public Shared Async Function Fill_DictionaryAsync(Fo4DataPath As String, progress As IProgress(Of (Stepn As String, Value As Integer, Max As Integer))) As Task
        Try
            FO4Path = Fo4DataPath
            Dictionary.Clear()
            _overriddenEntries.Clear()
            ClearSearchIndexes()

            ' O1.1: Clear byte cache when dictionary is rebuilt
            ClearBytesCache()

            ' O1.2: Dispose idle readers and initialize pool cleanup timer
            DisposeIdleReaders()
            InitPoolCleanupTimer()

            Dim ba2Files = EnumerateFilesWithSymlinkSupport(Fo4DataPath, "*.ba2;*.bsa", False).
            OrderBy(Function(p) p, StringComparer.OrdinalIgnoreCase).
            ToList()

            ' O1.3: EnumerateSupportedLooseFiles already uses Directory.EnumerateFiles (lazy enumeration)
            Dim looseFiles = EnumerateSupportedLooseFiles(Fo4DataPath).
            OrderBy(Function(p) p, StringComparer.OrdinalIgnoreCase).
            ToList()

            Dim archivePriority = BuildArchivePriority(ba2Files)

            totalCount = ba2Files.Count + looseFiles.Count
            completed = 0
            progress.Report(("Escaneando archivos...", completed, totalCount))

            Dim workQueue As New ConcurrentQueue(Of DictionaryScanWorkItem)

            For Each ba2 In ba2Files
                Dim ba2Name = Path.GetFileName(ba2)
                Dim sourceOrder As Integer = Integer.MinValue
                If archivePriority.TryGetValue(ba2Name, sourceOrder) = False Then
                    sourceOrder = Integer.MinValue
                End If

                workQueue.Enqueue(New DictionaryScanWorkItem With {
                .IsArchive = True,
                .FilePath = ba2,
                .SourceOrder = sourceOrder
            })
            Next

            For Each file In looseFiles
                workQueue.Enqueue(New DictionaryScanWorkItem With {
                .IsArchive = False,
                .FilePath = file,
                .SourceOrder = Integer.MaxValue
            })
            Next

            Dim workerCount As Integer = Math.Min(4, Math.Max(1, workQueue.Count))

            Dim workers = Enumerable.Range(0, workerCount).
            Select(Function(funza)
                       Return Task.Run(
                           Sub()
                               Dim item As DictionaryScanWorkItem = Nothing

                               While workQueue.TryDequeue(item)
                                   If item.IsArchive Then
                                       ProcessBa2File(item.FilePath, item.SourceOrder, progress)
                                   Else
                                       ProcessLooseFile(item.FilePath, Fo4DataPath, progress)
                                   End If
                               End While
                           End Sub)
                   End Function).
            ToArray()

            Await Task.WhenAll(workers).ConfigureAwait(False)

            ' O1.3: Build all secondary indexes in a single batch pass after the parallel scan completes.
            ' This avoids lock contention on ConcurrentDictionary secondary indexes during parallel insert.
            RebuildSearchIndexesFromDictionary()

        Catch ex As Exception
            MsgBox(ex.ToString)
        End Try
    End Function
    Private Shared Function GetPluginsTxtPath() As String
        If Config_App.Current.Game = Config_App.Game_Enum.Fallout4 Then
            Return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Fallout4", "loadorder.txt")
        Else
            Return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Skyrim Special Edition", "loadorder.txt")
        End If
    End Function

    Private Shared Function ReadPluginsLoadOrder() As List(Of String)
        Dim result As New List(Of String)
        Dim pluginsTxt = GetPluginsTxtPath()

        If File.Exists(pluginsTxt) = False Then Return result

        For Each rawLine In File.ReadLines(pluginsTxt, Encoding.UTF8)
            Dim line = rawLine.Trim()

            If line = "" Then Continue For
            If line.StartsWith("#", StringComparison.OrdinalIgnoreCase) Then Continue For
            If line.StartsWith(";", StringComparison.OrdinalIgnoreCase) Then Continue For

            If line.StartsWith("*", StringComparison.OrdinalIgnoreCase) Then
                line = line.Substring(1).Trim()
            End If

            If line = "" Then Continue For

            Dim ext = Path.GetExtension(line)
            If ext.Equals(".esp", StringComparison.OrdinalIgnoreCase) OrElse
           ext.Equals(".esm", StringComparison.OrdinalIgnoreCase) OrElse
           ext.Equals(".esl", StringComparison.OrdinalIgnoreCase) Then

                result.Add(Path.GetFileName(line))
            End If
        Next

        Return result
    End Function

    Private Shared Function ArchiveBelongsToPlugin(archiveFileName As String, pluginFileName As String) As Boolean
        Dim archiveBase = Path.GetFileNameWithoutExtension(archiveFileName)
        Dim pluginBase = Path.GetFileNameWithoutExtension(pluginFileName)
        If archiveBase.Equals(pluginBase, StringComparison.OrdinalIgnoreCase) Then Return True
        If archiveBase.StartsWith(pluginBase & " - ", StringComparison.OrdinalIgnoreCase) Then Return True
        Return False
    End Function

    Private Shared Function BuildArchivePriority(ba2Files As List(Of String)) As Dictionary(Of String, Integer)
        Dim result As New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)

        Dim archiveNames = ba2Files.
        Select(Function(p) Path.GetFileName(p)).
        OrderBy(Function(n) n, StringComparer.OrdinalIgnoreCase).
        ToList()

        Dim fullPathsByName = ba2Files.
        GroupBy(Function(p) Path.GetFileName(p), StringComparer.OrdinalIgnoreCase).
        ToDictionary(Function(g) g.Key, Function(g) g.First(), StringComparer.OrdinalIgnoreCase)

        Dim pending As New HashSet(Of String)(archiveNames, StringComparer.OrdinalIgnoreCase)
        Dim nextOrder As Integer = 0

        Dim baseAndDlcOrder As String() = {
        "Fallout4",
        "DLCRobot",
        "DLCworkshop01",
        "DLCCoast",
        "DLCworkshop02",
        "DLCworkshop03",
        "DLCNukaWorld",
        "DLCUltraHighResolution"
    }

        For Each prefix In baseAndDlcOrder
            Dim matches = pending.
            Where(Function(name) Path.GetFileNameWithoutExtension(name).StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).
            OrderBy(Function(name) name, StringComparer.OrdinalIgnoreCase).
            ToList()

            For Each match In matches
                result(match) = nextOrder
                nextOrder += 1
                pending.Remove(match)
            Next
        Next

        Dim pluginsLoadOrder = ReadPluginsLoadOrder()

        For Each plugin In pluginsLoadOrder
            Dim matches = pending.
            Where(Function(name) ArchiveBelongsToPlugin(name, plugin)).
            OrderBy(Function(name) name, StringComparer.OrdinalIgnoreCase).
            ToList()

            For Each match In matches
                result(match) = nextOrder
                nextOrder += 1
                pending.Remove(match)
            Next
        Next

        Dim fallbackMatches = pending.
        OrderBy(Function(name) File.GetLastWriteTimeUtc(fullPathsByName(name))).
        ThenBy(Function(name) name, StringComparer.OrdinalIgnoreCase).
        ToList()

        For Each match In fallbackMatches
            result(match) = nextOrder
            nextOrder += 1
            pending.Remove(match)
        Next

        Return result
    End Function
    Private Shared Sub ProcessBa2File(ba2 As String, sourceOrder As Integer, progress As IProgress(Of (String, Integer, Integer)))
        Try
            ' O5.4: Intern the BA2 filename since it is stored in many File_Location instances
            Dim ba2FileName = String.Intern(Path.GetFileName(ba2))
            Dim ba2Date = File.GetLastWriteTime(ba2)

            Using fs As FileStream = File.OpenRead(ba2)
                Using arc As New BSA_BA2_Library_DLL.BethesdaArchive.Core.BethesdaReader(fs)
                    For Each fil In arc.EntriesFiles
                        ' O5.4: Intern the standardized path — stored long-term as dictionary key and File_Location.FullPath
                        Dim standardized = String.Intern(fil.FullPath.Correct_Path_Separator)
                        Dim entry As New File_Location With {
                        .BA2File = ba2FileName,
                        .Index = fil.Index,
                        .FullPath = standardized,
                        .SourceOrder = sourceOrder,
                        .FileDate = ba2Date
                    }

                        ' O1.3: During scan, only populate _dictionary; indexes are built in batch after scan
                        Dictionary.AddOrUpdate(
                        standardized,
                        entry,
                        Function(key, existing)
                            If Resolve_Conflict(existing, entry) Then
                                PushOverriddenEntry(standardized, existing)
                                Return entry
                            Else
                                PushOverriddenEntry(standardized, entry)
                                Return existing
                            End If
                        End Function)
                    Next
                End Using
            End Using

        Catch ex As Exception
            MsgBox("Error procesing Ba2 " + ba2 + " :" + ex.ToString)
        Finally
            Dim current = Interlocked.Increment(completed)
            progress.Report(($"Procesado: {Path.GetFileName(ba2)}", current, totalCount))
        End Try
    End Sub

    Public Shared Function EnumerateFilesWithSymlinkSupport(root As String, pattern As String, Recursive As Boolean) As IEnumerable(Of String)
        Dim spl() As String = {pattern}
        If pattern.Contains(";"c) Then
            spl = pattern.Split(";"c)
        End If
        Dim result As IEnumerable(Of String) = Enumerable.Empty(Of String)()
        Dim opts As New EnumerationOptions() With {.RecurseSubdirectories = Recursive}

        For Each pat In spl
            result = result.Concat(Directory.EnumerateFiles(root, pat, opts))
        Next
        Return result
    End Function

    Private Shared Sub ProcessLooseFile(file As String, basePath As String, progress As IProgress(Of (String, Integer, Integer)))
        Try
            ' O5.4: Intern the standardized path — stored long-term as dictionary key and File_Location.FullPath
            Dim standardized = String.Intern(Path.GetRelativePath(basePath, file).Correct_Path_Separator)

            Dim entry As New File_Location With {
            .BA2File = String.Empty,
            .Index = -1,
            .FullPath = standardized,
            .SourceOrder = Integer.MaxValue,
            .FileDate = IO.File.GetLastWriteTime(file)
        }

            ' O1.3: During scan, only populate _dictionary; indexes are built in batch after scan
            Dictionary.AddOrUpdate(
            standardized,
            entry,
            Function(key, existing)
                If Resolve_Conflict(existing, entry) Then
                    PushOverriddenEntry(standardized, existing)
                    Return entry
                Else
                    PushOverriddenEntry(standardized, entry)
                    Return existing
                End If
            End Function)

        Catch ex As Exception
            MsgBox("Error procesing Loose file " + file + " :" + ex.ToString)
        Finally
            Dim current = Interlocked.Increment(completed)
            progress.Report(($"Procesado: {Path.GetFileName(file)}", current, totalCount))
        End Try
    End Sub

    Private Shared Function Resolve_Conflict(Original As File_Location, Nueva As File_Location) As Boolean
        If IsNothing(Original) Then Return True
        If IsNothing(Nueva) Then Return False

        If Nueva.IsLosseFile AndAlso Original.IsLosseFile = False Then Return True
        If Original.IsLosseFile AndAlso Nueva.IsLosseFile = False Then Return False

        If Nueva.SourceOrder > Original.SourceOrder Then Return True
        If Nueva.SourceOrder < Original.SourceOrder Then Return False

        If Nueva.IsLosseFile AndAlso Original.IsLosseFile Then
            Return False
        End If

        Return StringComparer.OrdinalIgnoreCase.Compare(Nueva.BA2File, Original.BA2File) >= 0
    End Function

End Class
