' Version Uploaded of Fo4Library 3.2.0
Imports System.Collections.Concurrent
Imports System.IO
Imports System.Runtime.CompilerServices
Imports System.Text
Imports System.Threading
Imports System.Timers
Imports NiflySharp.Enums

Public Module Extensions
    Public Const MaterialsPrefix As String = "Materials\"
    Public Const TexturesPrefix As String = "Textures\"
    Public Const MeshesPrefix As String = "Meshes\"

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
        ' Loose-file mtime captured at enumeration time (from WIN32_FIND_DATA) so the
        ' worker doesn't need to issue a second syscall per file.
        Public Property LooseLastWrite As Date = Date.MinValue
        ' Loose-file path RELATIVE to the data root, computed by the walk (which already knows the
        ' root) instead of by Path.GetRelativePath in the worker — that call re-normalizes BOTH
        ' paths on every invocation, and it ran once per loose file.
        Public Property RelativePath As String = ""
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
    Private Shared _fO4Path As String = ""
    Private Shared _cacheDirectory As String = ""
    Private Shared _dictionary As New ConcurrentDictionary(Of String, File_Location)(StringComparer.OrdinalIgnoreCase)
    ''' <summary>Stack of overridden entries per key. When a loose overrides a BA2 (or a BA2 overrides another), the loser is pushed here.</summary>
    Private Shared ReadOnly _overriddenEntries As New ConcurrentDictionary(Of String, ConcurrentStack(Of File_Location))(StringComparer.OrdinalIgnoreCase)
    ''' <summary>Extensions the dictionary indexes from loose files and archives.
    ''' The last three cover the data files RaceMenu (skee64) and LooksMenu (f4ee) read — both open their
    ''' configuration through the game's archive layer (BSResourceNiBinaryStream), so it can live inside a BSA/BA2
    ''' and frequently does:
    '''   • <c>.ini</c> — RaceMenu extended face morphs (<c>Meshes\actors\character\FaceGenMorphs\&lt;mod&gt;\races.ini</c>,
    '''     <c>morphs.ini</c>, <c>sliders\*.ini</c> — shipped inside RaceMenu.bsa) and BodyGen
    '''     (<c>...\BodyGenData\&lt;mod&gt;\templates.ini</c>, <c>morphs.ini</c>); LooksMenu bodygen/bodymorph.
    '''   • <c>.jslot</c> / <c>.slot</c> — RaceMenu presets (JSON and binary), <c>Data\SKSE\Plugins\CharGen\</c>.
    '''   • <c>.pex</c> / <c>.psc</c> — compiled/source Papyrus scripts. RaceMenu (skee64) builds its warpaint and
    '''     body/hand/feet/face paint lists at runtime from every mod's <c>Add*Paint(name,path)</c> registrations,
    '''     which live in the scripts (loose <c>Data\Scripts\</c> or inside a mod's BSA). Indexing them here makes
    '''     them readable via <see cref="GetBytes"/>; only the SSE editors parse them (RaceMenuPaintCatalog).
    ''' Omitting <c>.ini</c> made the extended-slider config invisible, so the catalog loaded nothing and every
    ''' extended face slider silently resolved to no morph. Extensions verified against the plugin sources.</summary>
    Private Shared ReadOnly SupportedExtensions As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {".dds", ".bgsm", ".bgem", ".nif", ".tri", ".txt", ".json", ".xml", ".ssf", ".sclp", ".hkx", ".hkt", ".ini", ".jslot", ".slot", ".pex", ".psc"}

    ''' <summary>App-specific data store. Apps register their own data here (presets, high heels, etc.) keyed by type.</summary>
    Private Shared ReadOnly _appData As New ConcurrentDictionary(Of Type, Object)

    ' O1.1: Lazy byte cache with WeakReference — allows GC to reclaim when memory is needed
    Private Shared ReadOnly _bytesCache As New ConcurrentDictionary(Of String, WeakReference(Of Byte()))(StringComparer.OrdinalIgnoreCase)

    ' O1.2: Archive reader pool — reuses BethesdaReader instances to avoid repeated open/close
    Private Shared ReadOnly _archivePool As New ConcurrentDictionary(Of String, ConcurrentBag(Of (Reader As BSA_BA2_Library_DLL.BethesdaArchive.Core.BethesdaReader, Stream As FileStream)))(StringComparer.OrdinalIgnoreCase)
    Private Shared ReadOnly MaxPooledReadersPerArchive As Integer = 2
    Private Shared _poolCleanupTimer As System.Timers.Timer

    ' Track archives mounted at runtime via RegisterArchive (vs. those discovered by Fill_DictionaryAsync).
    ' Key: archive file name (matches File_Location.BA2File). Value: unused (used as a set).
    Private Shared ReadOnly _registeredArchives As New ConcurrentDictionary(Of String, Byte)(StringComparer.OrdinalIgnoreCase)

    ''' <summary>
    ''' SourceOrder for archives mounted via RegisterArchive after the initial scan.
    ''' Higher than any value assigned by BuildArchivePriority but lower than Integer.MaxValue
    ''' (which is reserved for loose files), so runtime-registered archives win over scan-time
    ''' archives but loose still overrides everything.
    ''' </summary>
    Public Const ArchiveSourceOrder_RuntimeRegistered As Integer = Integer.MaxValue - 1

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

    ''' <summary>Count of entries in _bytesCache (for memory diagnostics).</summary>
    Public Shared Function BytesCacheCount() As Integer
        Return _bytesCache.Count
    End Function

    ''' <summary>Count of total pooled archive readers (for memory diagnostics).</summary>
    Public Shared Function ArchivePoolReaderCount() As Integer
        Dim total = 0
        For Each kvp In _archivePool
            total += kvp.Value.Count
        Next
        Return total
    End Function

    ''' <summary>Dispose all pooled archive readers and clear the bytes cache.
    ''' Call periodically during bulk load to keep memory from ballooning.</summary>
    Public Shared Sub PurgeCachesAndReaders()
        DisposeIdleReaders()
        _bytesCache.Clear()
    End Sub

    ' There used to be a third index here, _KeysByExtension (every key bucketed by extension alone).
    ' It was written on every insert and cleared on every rebuild, but NO query ever read it —
    ' GetFilesInDirectory reads _KeysByDirectory / _KeysByDirectoryExtension, GetFilteredKeys reads
    ' only _KeysByDirectoryExtension. On a modded install that is a full extra copy of every key
    ' (millions) built and held for nothing. Removed.
    ''' <summary>⛔ LAZY — built on first use, NOT during the scan. Its ONLY reader is the
    ''' <c>extensionSet.Count = 0</c> branch of <see cref="GetFilesInDirectory"/> (i.e. "give me every file
    ''' in this directory, any extension"), and no caller in either app passes an empty extension set today:
    ''' WM's two call sites pass <c>{".bgsm",".bgem"}</c> and <c>{ext}</c>, and GetFilteredKeys reads only
    ''' <see cref="_KeysByDirectoryExtension"/>. Populating it during the scan therefore built a SECOND full
    ''' copy of every dictionary key — millions of them on a modded install, hashed OrdinalIgnoreCase over
    ''' long paths — for a query nobody makes. That is the same "write-only index" trap that _KeysByExtension
    ''' was deleted for; this one survives because the empty-extension query is part of the public contract,
    ''' so it must still WORK — it just doesn't get to cost anything until someone actually asks.
    '''
    ''' <para>Kept coherent afterwards: once built, <see cref="IndexDictionaryKey"/> maintains it like before,
    ''' and <see cref="ClearSearchIndexes"/> drops it back to the unbuilt state.</para></summary>
    Private Shared ReadOnly _KeysByDirectory As New ConcurrentDictionary(Of String, ConcurrentDictionary(Of String, Byte))(StringComparer.OrdinalIgnoreCase)
    Private Shared _keysByDirectoryBuilt As Boolean = False
    Private Shared ReadOnly _keysByDirectoryLock As New Object

    Private Shared ReadOnly _KeysByDirectoryExtension As New ConcurrentDictionary(Of String, ConcurrentDictionary(Of String, Byte))(StringComparer.OrdinalIgnoreCase)

    ''' <summary>De-duplicates the strings we store LONG-TERM (dictionary keys and
    ''' <see cref="File_Location.FullPath"/>), which is what <c>String.Intern</c> used to do here.
    '''
    ''' <para>⛔ Why not String.Intern: it takes a lock on the runtime's GLOBAL intern table, and the scan
    ''' pushes every entry of every archive through it — millions of paths on a heavily modded install,
    ''' from several workers at once. Worse, interned strings are never released, so every load-order
    ''' reload leaked the previous scan's paths for the life of the process. This pool is cleared at the
    ''' start of each scan.</para>
    '''
    ''' <para>⛔ Ordinal, NOT OrdinalIgnoreCase: an ignore-case pool would collapse <c>Textures\A.dds</c>
    ''' and <c>textures\a.dds</c> onto a single instance and silently rewrite the casing — and these
    ''' strings are surfaced verbatim in the pickers and written into records.</para>
    '''
    ''' <para>⛔ Insert sites only (<see cref="ProcessBa2File"/> / <see cref="ProcessLooseFile"/>), never
    ''' the lookup path: pooling arbitrary caller-supplied lookup strings would recreate exactly the leak
    ''' described above.</para></summary>
    Private Shared _pathPool As New ConcurrentDictionary(Of String, String)(StringComparer.Ordinal)

    Private Shared Function PoolPath(s As String) As String
        If String.IsNullOrEmpty(s) Then Return s
        Return _pathPool.GetOrAdd(s, s)
    End Function

    Public Shared Function GetBytes(File As String) As Byte()
        Dim located_File As File_Location = Nothing
        If Not Dictionary.TryGetValue(NormalizeDictionaryKey(File), located_File) Then
            Return Array.Empty(Of Byte)
        Else
            Return located_File.GetBytes
        End If
    End Function


    ''' <summary>
    ''' Reads the ARCHIVED (BA2/BSA) original bytes for <paramref name="path"/>, IGNORING any loose
    ''' override AND the path-keyed <see cref="_bytesCache"/>. The cache is keyed by File_Location.FullPath,
    ''' which is identical for the loose winner and the BA2 loser of the same logical path — so a cached
    ''' read of the loose winner would otherwise be returned for the BA2 entry too (collision). This reads
    ''' straight from the archive via ExtractToMemory and never touches the cache.
    '''
    ''' Resolution: among ALL archived (IsLosseFile=False) candidates for the key — the dictionary winner
    ''' (if archived) plus every archived entry in the override stack — pick the one with the LOWEST
    ''' File_Location.SourceOrder. That is the VANILLA archive: BuildArchivePriority assigns the base game
    ''' (Fallout4*) + DLC archives the lowest SourceOrder (they're processed first), active-mod BA2s rank
    ''' higher, and loose files get Integer.MaxValue (excluded here since they're loose). "First non-loose"
    ''' was wrong: the override stack is a ConcurrentStack filled in parallel, so when a mod ships its
    ''' override inside a .ba2 (multiple archived candidates) the first could be the MOD's archive.
    ''' NPC_Manager loads with includeInactive=False, so inactive-mod archives aren't in the dictionary at
    ''' all and can't be picked. Returns Nothing when no archived candidate exists at all (the caller should
    ''' then fall back to the normal resolver, whose winner is already vanilla).
    ''' </summary>
    Public Shared Function GetArchiveOriginalBytes(path As String) As Byte()
        Dim key = NormalizeDictionaryKey(path)
        If String.IsNullOrEmpty(key) Then Return Nothing

        ' Pick the vanilla archived entry = the archived candidate with the minimum SourceOrder.
        ' Candidates: the dictionary winner (only if it's archived) plus every archived loser shadowed
        ' in the override stack. Loose entries (SourceOrder = Integer.MaxValue) are excluded.
        Dim entry As File_Location = Nothing
        Dim winner As File_Location = Nothing
        If _dictionary.TryGetValue(key, winner) AndAlso winner IsNot Nothing AndAlso Not winner.IsLosseFile Then
            entry = winner
        End If
        For Each loser In GetOverriddenEntries(key)
            If loser IsNot Nothing AndAlso Not loser.IsLosseFile Then
                If entry Is Nothing OrElse loser.SourceOrder < entry.SourceOrder Then
                    entry = loser
                End If
            End If
        Next

        If entry Is Nothing Then Return Nothing

        ' Read directly from the archive, bypassing _bytesCache. Reuse the reader pool.
        Dim archivePath = IO.Path.Combine(FO4Path, entry.BA2File)
        Dim leased As (Reader As BSA_BA2_Library_DLL.BethesdaArchive.Core.BethesdaReader, Stream As FileStream) = Nothing
        Dim returned As Boolean = False
        Try
            leased = LeaseReader(archivePath)
            Dim result = entry.GetBytesFromOpenArchive(leased.Reader)
            ReturnReader(archivePath, leased)
            returned = True
            Return result
        Catch
            If Not returned Then
                If leased.Reader IsNot Nothing Then
                    Try : leased.Reader.Dispose() : Catch : End Try
                End If
                If leased.Stream IsNot Nothing Then
                    Try : leased.Stream.Dispose() : Catch : End Try
                End If
            End If
            Return Nothing
        End Try
    End Function
    ''' <summary>⛔ TOP-LEVEL only (RecurseSubdirectories = False) — the recursion is done by
    ''' <see cref="WalkLooseFilesParallel"/>, one directory per work item, so it can be spread across
    ''' threads. The other two settings are load-bearing and must match what the old single recursive
    ''' <c>EnumerateFiles</c> call used, or the walk would return a DIFFERENT set of files:
    '''   • IgnoreInaccessible = True — a directory we can't open is skipped, not thrown on.
    '''   • AttributesToSkip is left at its DEFAULT (Hidden | System), which is what an
    '''     <c>EnumerationOptions</c> constructed with no explicit value gives you. It applies to
    '''     directories as well as files, so hidden/system subtrees stay excluded exactly as before.</summary>
    Private Shared ReadOnly _looseEnumOptionsTopLevel As New EnumerationOptions() With {
        .RecurseSubdirectories = False,
        .IgnoreInaccessible = True
    }

    ''' <summary>PARALLEL recursive walk of Data\, filtering by extension in managed code, streaming each
    ''' matching file to <paramref name="onFile"/> as it is found. Returns the number of files emitted.
    '''
    ''' <para>⛔ History, because both mistakes are easy to make again. It FIRST globbed once per supported
    ''' extension ("*.dds", then "*.nif", then "*.tri"…) — ~17 full traversals of the Data tree. That became
    ''' ONE recursive traversal + a HashSet lookup per file. This is the next step: that single traversal was
    ''' still SEQUENTIAL, and it is the one cost in the whole scan that scales with the thing users actually
    ''' complain about (hundreds of thousands of loose files). Under MO2 every FindNextFile goes through the
    ''' USVFS hook, so the traversal — not the archives — is what "freezes on Mounting archives".</para>
    '''
    ''' <para>⛔ Why a work QUEUE of directories and not a fixed depth split (e.g. one task per subdir of
    ''' Data\): the tree is wildly unbalanced. Textures\ and Meshes\ hold the overwhelming majority of a
    ''' modded install, so a depth-1 split degenerates into one thread doing ~all the work while the rest
    ''' idle. Here EVERY directory found at ANY depth goes back into the shared queue, so the threads
    ''' rebalance continuously and depth doesn't matter — a deep Textures\actors\character\… subtree is
    ''' spread across all of them.</para>
    '''
    ''' <para>Completion: <paramref name="pending"/> counts directories enqueued-but-not-yet-finished. A
    ''' worker that finds the queue momentarily empty while others still have directories in flight spins
    ''' (SpinWait yields/sleeps, it does not burn a core); when the last directory is done the count hits 0
    ''' and every worker exits. No worker can exit while a directory that might still produce subdirectories
    ''' is being processed, which is the bug a naive "queue empty ⇒ done" check would have.</para>
    '''
    ''' <para>Still DirectoryInfo/FileInfo, not the string overload: FileInfo comes back with its metadata
    ''' pre-populated from WIN32_FIND_DATA, so reading fi.LastWriteTime issues NO second syscall — and that
    ''' mtime is not decoration, it lands in <see cref="File_Location.FileDate"/>, which WM's clone planner
    ''' reads to decide whether an already-cloned file needs rewriting. EnumerateFileSystemInfos (not
    ''' EnumerateFiles + EnumerateDirectories) so each directory is enumerated ONCE for both.</para>
    '''
    ''' <para>Order is NOT preserved, and never was: the old walk wasn't sorted either. Two distinct loose
    ''' files under one root cannot produce the same relative path, so loose-vs-loose never reaches a
    ''' conflict and the insertion order of the results cannot change the resulting dictionary.</para>
    '''
    ''' <param name="extensions">Snapshot of the supported extensions (OrdinalIgnoreCase). Taken by the
    ''' caller before the walk so a concurrent RegisterExtensions can't mutate the set mid-enumeration.</param>
    ''' <param name="onFile">Called once per matching file, FROM MULTIPLE THREADS — must be thread-safe.
    ''' Receives the full path, the path relative to <paramref name="root"/>, and the mtime.</param>
    ''' </summary>
    Private Shared Function WalkLooseFilesParallel(root As String,
                                                   extensions As HashSet(Of String),
                                                   dop As Integer,
                                                   onFile As Action(Of String, String, Date)) As Integer
        ' Relative paths are cut with a Substring against this length instead of Path.GetRelativePath
        ' (which re-normalizes both operands on every call). Trimming any trailing separator off the root
        ' first makes the cut correct whether the caller passed "…\Data" or "…\Data\".
        Dim rootTrimmed = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
        Dim cutAt As Integer = rootTrimmed.Length + 1

        Dim dirs As New ConcurrentQueue(Of String)
        dirs.Enqueue(rootTrimmed)

        ' Directories enqueued but not yet finished. Starts at 1 for the root.
        Dim pending As Integer = 1
        Dim emitted As Integer = 0

        Dim workers = Enumerable.Range(0, Math.Max(1, dop)).
            Select(Function(unused)
                       Return Task.Run(
                           Sub()
                               Dim dir As String = Nothing

                               ' ⛔ Declared OUTSIDE the loop on purpose. SpinWait escalates: the first few
                               ' SpinOnce calls busy-spin, then it starts yielding the thread and finally
                               ' sleeping — but only because it COUNTS its own calls. Constructing a fresh
                               ' one inside the loop resets that counter every iteration, so it would never
                               ' get past the cheapest tight spin and an idle worker would burn a core at
                               ' 100% while another chews a big directory — stealing CPU from the scan
                               ' workers, which now run CONCURRENTLY with this walk. Reset it only when we
                               ' actually get work, so the back-off restarts from cheap each time.
                               Dim spin As New SpinWait()

                               Do
                                   If Not dirs.TryDequeue(dir) Then
                                       ' Nothing to take right now. If no directory is in flight anywhere,
                                       ' the walk is over; otherwise another worker is about to enqueue
                                       ' children, so back off and retry.
                                       If Volatile.Read(pending) = 0 Then Exit Do
                                       spin.SpinOnce()
                                       Continue Do
                                   End If
                                   spin.Reset()

                                   Try
                                       Dim di As New DirectoryInfo(dir)
                                       For Each info In di.EnumerateFileSystemInfos("*", _looseEnumOptionsTopLevel)
                                           Dim sub_ = TryCast(info, DirectoryInfo)
                                           If sub_ IsNot Nothing Then
                                               ' Count it BEFORE publishing it, or a worker could dequeue and
                                               ' finish this child before we incremented, driving pending to 0
                                               ' while the walk is still going and letting everyone exit early.
                                               Interlocked.Increment(pending)
                                               dirs.Enqueue(sub_.FullName)
                                           Else
                                               Dim fi = TryCast(info, FileInfo)
                                               If fi IsNot Nothing AndAlso extensions.Contains(fi.Extension) Then
                                                   Interlocked.Increment(emitted)
                                                   onFile(fi.FullName, fi.FullName.Substring(cutAt), fi.LastWriteTime)
                                               End If
                                           End If
                                       Next
                                   Catch ex As Exception
                                       ' A directory that vanished or that we can't read is skipped, exactly as
                                       ' IgnoreInaccessible did for the old recursive walk. One unreadable folder
                                       ' must not abort the scan of the other 400.000 files.
                                       _scanErrors.Enqueue("Error walking directory " & dir & ": " & ex.Message)
                                   Finally
                                       Interlocked.Decrement(pending)
                                   End Try
                               Loop
                           End Sub)
                   End Function).
            ToArray()

        Task.WaitAll(workers)
        Return Volatile.Read(emitted)
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

    ''' <summary>⛔ Loose files report their count progress every Nth item (this mask + 1), not every item.
    '''
    ''' <para>The count channel is reported once per WORK ITEM, and on a heavily modded rig the loose files
    ''' alone are hundreds of thousands of them. Every Report is a SynchronizationContext.Post that the UI
    ''' thread has to drain, each one setting ProgressBar.Value and Label.Text (invalidate + repaint). The
    ''' workers never block on it — Post is fire-and-forget — so the message queue grew without bound and
    ''' the form kept churning through it long after the scan itself had finished. That is the shape of the
    ''' "stuck on Mounting archives for ten minutes" reports.</para>
    '''
    ''' <para>Archives are NOT throttled: there are only tens-to-hundreds of them, and each one carries the
    ''' informative label. Neither is the BYTE channel (<see cref="_archiveByteProgress"/>), which already
    ''' fires once per archive and is the only thing driving Preflight_Form's Detail bar — throttling that
    ''' by item count would freeze the bar at 0 for the whole archive phase.</para>
    '''
    ''' <para>The final item always reports regardless of the mask: MainForm.UpdateAssetLoadProgress and
    ''' Wardrobe_Manager_Form set Value straight from the report and never clamp to Max afterwards, so
    ''' without a forced last tick their bars would stop visibly short.</para></summary>
    Private Const LooseProgressReportMask As Integer = &H1FF   ' report every 512th loose file

    ''' <summary>Heartbeat cadence for the loose WALK (every 4096th file discovered). Same reasoning as
    ''' <see cref="LooseProgressReportMask"/> — every Report is a Post the UI thread has to drain — but a
    ''' coarser mask, because this one fires from the walk threads while the scan workers are ALSO
    ''' reporting, and its only job is to prove the app is alive during what used to be a dark window.</summary>
    Private Const WalkHeartbeatMask As Integer = &HFFF

    ''' <summary>True once the loose walk has stopped producing (<c>CompleteAdding</c> called).
    '''
    ''' <para>⛔ Load-bearing for the progress throttle. <see cref="ProcessLooseFile"/> force-reports the
    ''' item where <c>completed &gt;= totalCount</c> so consumers that never clamp their bar to Max still
    ''' finish full. That was safe when totalCount was known up front. Now the walk STREAMS and totalCount
    ''' GROWS behind it, so during the scan the workers are routinely caught up with the producer and
    ''' <c>completed = totalCount</c> holds for a large fraction of the files — which would fire the
    ''' "final" report on nearly EVERY loose file and rebuild the exact Post storm the throttle exists to
    ''' prevent. Gating the force on "production is finished" restores it to what it means: the last
    ''' item.</para></summary>
    Private Shared _scanProductionComplete As Boolean = False

    ''' <summary>Null-safe progress report. Fill_DictionaryAsync's <c>progress</c> parameter is not
    ''' optional, but callers do pass Nothing (the CLI used to, and hit an NRE that its own Try swallowed —
    ''' leaving an EMPTY dictionary and no error). A no-op is the right answer for a caller that doesn't
    ''' want progress.</summary>
    Private Shared Sub ReportScan(progress As IProgress(Of (Stepn As String, Value As Integer, Max As Integer)),
                                  stepName As String, value As Integer, max As Integer)
        progress?.Report((stepName, value, max))
    End Sub

    ''' <summary>One-line summary of the LAST <see cref="Fill_DictionaryAsync"/>: volumes (archives, cache
    ''' hits, loose, entries) and per-phase timings. Held in memory only — building it is three stopwatches
    ''' and one string, and NOTHING is written anywhere unless a caller decides to (NPC_Manager only does so
    ''' under its <c>--diagnoseLoad</c> switch). Empty until the first scan completes.</summary>
    Public Shared Property LastScanDiagnostics As String = ""

    ' Byte-weighted progress for the archive (BA2/BSA) phase only. Mirrors the completed/totalCount
    ' pattern above (module-level Shared, read/incremented by ProcessBa2File). Loose files are NOT
    ' counted here (stat'ing thousands of loose files would be expensive); this bar reaches 100% when
    ' the BA2/BSA set is done. Nothing when the caller didn't request a byte progress (CLI/WM/MainForm).
    Private Shared _archiveBytesDone As Long
    Private Shared _archiveBytesTotal As Long
    Private Shared _archiveByteProgress As IProgress(Of (Done As Long, Total As Long))

    ' Diagnostics for the per-phase log (see Fill_DictionaryAsync). Reset at the start of each scan.
    Private Shared _archivesFromCache As Integer
    Private Shared _archivesReindexed As Integer

    ''' <summary>
    ''' Errores acumulados por workers durante Fill_DictionaryAsync. Se drenan en el
    ''' UI thread después del await. NUNCA mostrar MsgBox desde un worker: bloquea
    ''' el Parallel.ForEach indefinidamente si la UI no pumpea (ventana oculta atrás
    ''' del form principal) y cuelga toda la app.
    ''' </summary>
    Private Shared ReadOnly _scanErrors As New System.Collections.Concurrent.ConcurrentQueue(Of String)

    Public Shared Function DrainScanErrors() As List(Of String)
        Dim result As New List(Of String)
        Dim msg As String = Nothing
        While _scanErrors.TryDequeue(msg)
            result.Add(msg)
        End While
        Return result
    End Function

    ''' <summary>
    ''' Per-archive scan outcome reported by Fill_DictionaryAsync workers. Apps drain
    ''' this after the fill to log whether each BA2/BSA was loaded from the index
    ''' cache or re-scanned from the archive.
    ''' </summary>
    Private Shared ReadOnly _scanReport As New System.Collections.Concurrent.ConcurrentQueue(Of (ArchiveName As String, CacheHit As Boolean))

    Public Shared Function DrainScanReport() As List(Of (ArchiveName As String, CacheHit As Boolean))
        Dim result As New List(Of (ArchiveName As String, CacheHit As Boolean))
        Dim item As (ArchiveName As String, CacheHit As Boolean) = Nothing
        While _scanReport.TryDequeue(item)
            result.Add(item)
        End While
        Return result
    End Function

    ''' <summary>Register app-specific extensions to include in dictionary scans (e.g. ".osp", ".xml").</summary>
    Public Shared Sub RegisterExtensions(ParamArray extensions() As String)
        For Each ext In extensions
            SupportedExtensions.Add(ext)
        Next
    End Sub

    ''' <summary>Store app-specific data by type. Apps use this to attach their own state to the dictionary lifecycle.</summary>
    Public Shared Sub SetAppData(Of T As Class)(value As T)
        _appData(GetType(T)) = value
    End Sub

    ''' <summary>Retrieve app-specific data by type. Returns Nothing if not set.</summary>
    Public Shared Function GetAppData(Of T As Class)() As T
        Dim val As Object = Nothing
        If _appData.TryGetValue(GetType(T), val) Then Return DirectCast(val, T)
        Return Nothing
    End Function

    Public Shared Property FO4Path As String
        Get
            Return _fO4Path
        End Get
        Set(value As String)
            _fO4Path = value
        End Set
    End Property

    ''' <summary>
    ''' RAÍZ del cache de índices de archives. El caller la setea antes de Fill_DictionaryAsync; vacía =
    ''' cache deshabilitado. Los <c>.cac</c> NO viven acá directamente: van en una SUBCARPETA POR JUEGO
    ''' (ver <see cref="EffectiveCacheDirectory"/>). Los ~30 call sites siguen seteando la raíz y no saben
    ''' nada del juego — la game-awareness vive acá adentro, en un solo lugar, para que no puedan divergir.
    ''' </summary>
    Public Shared Property CacheDirectory As String
        Get
            Return _cacheDirectory
        End Get
        Set(value As String)
            _cacheDirectory = If(value, "")
        End Set
    End Property

    ''' <summary>Subcarpeta del cache para el juego ACTIVO. Se lee en cada operación de cache (no se
    ''' memoiza) porque el juego se puede cambiar en caliente desde el selector de la app.</summary>
    Private Shared Function GameCacheFolderName() As String
        ' Config_App.Current se inicializa siempre (= New Config_App()), pero es una propiedad seteable:
        ' si alguien la pone en Nothing, un nombre neutro es mejor que mezclar los dos juegos en la raíz.
        If Config_App.Current Is Nothing Then Return "Unknown"
        Return If(Config_App.Current.Game = Config_App.Game_Enum.Skyrim, "Skyrim", "Fallout4")
    End Function

    ''' <summary>Etiqueta estable (8 hex) del SET DE EXTENSIONES con el que se indexó. FNV-1a sobre la lista
    ''' canónica (minúsculas, ordenada) — ⛔ NO <c>String.GetHashCode</c>: está randomizado por proceso, así
    ''' que daría una carpeta distinta en cada arranque y el cache no serviría nunca.</summary>
    Private Shared Function ExtensionSetTag() As String
        Dim exts = _canonicalExtensionsSnapshot
        If exts Is Nothing Then exts = BuildCanonicalExtensionsSnapshot()

        Dim h As ULong = 2166136261UL
        For Each ext In exts
            For Each ch In ext
                h = ((h Xor CULng(AscW(ch))) * 16777619UL) And &HFFFFFFFFUL
            Next
            h = ((h Xor 124UL) * 16777619UL) And &HFFFFFFFFUL   ' "|" separator
        Next
        Return h.ToString("x8")
    End Function

    ''' <summary>⛔ Dónde viven REALMENTE los <c>.cac</c>: <c>{CacheDirectory}\{Juego}\{ExtSetTag}\</c>.
    '''
    ''' <para>Sin la subcarpeta POR JUEGO los dos juegos compartían carpeta, y eso NO era sólo desprolijo:
    ''' <see cref="CleanupOrphanCacheFiles"/> borra todo <c>.cac</c> que no esté en la lista de archives
    ''' del juego ACTIVO — así que cada vez que se cambiaba de juego se DESTRUÍA el cache del otro, y el
    ''' siguiente arranque re-indexaba todos los archives desde cero. Con la subcarpeta, el barrido de un
    ''' juego no puede ni ver los <c>.cac</c> del otro (EnumerateFiles no recursa), y los dos sobreviven.</para>
    '''
    ''' <para>⛔ La subcarpeta POR SET DE EXTENSIONES arregla exactamente el MISMO bug una capa más abajo. Un
    ''' <c>.cac</c> sólo es válido para el set de extensiones con el que se generó (<see cref="TryLoadArchiveIndex"/>
    ''' compara la lista y rechaza si difiere), y las apps NO comparten set: el CLI hace
    ''' <c>RegisterExtensions(".ssf",".sclp",".hkx",".hkt")</c> y NPC_Manager no registra nada. Con una sola
    ''' carpeta, cada app rechazaba los <c>.cac</c> de la otra, los REESCRIBÍA con su set, y la próxima
    ''' corrida de la otra volvía a re-indexar TODOS los archives desde cero — un scan frío eterno, cada vez,
    ''' para cualquiera que use las dos. Separados por tag, coexisten en vez de pisarse.</para>
    '''
    ''' <para>Devuelve "" cuando el cache está deshabilitado, para que los callers puedan seguir usando el
    ''' mismo guard de siempre.</para></summary>
    Private Shared Function EffectiveCacheDirectory() As String
        If Not IsCacheEnabled() Then Return ""
        Return Path.Combine(_cacheDirectory, GameCacheFolderName(), ExtensionSetTag())
    End Function

    ' ================== Archive index cache ==================
    ' Binary format "FD4I" v1 per-archive file at {CacheDirectory}\{name}.cac
    '   4B  magic        = 'F','D','4','I'
    '   2B  version u16  = 1
    '   8B  size i64
    '   8B  mtimeUtc i64 (DateTime.ToBinary of LastWriteTimeUtc)
    '   4B  ext_count u32
    '   N x [u16 len + utf8 bytes]   lowercase, sorted ordinal ascending (canonical)
    '   4B  entry_count u32
    '   N x [i32 index + u16 dir_len + utf8 fullpath bytes]
    Private Shared ReadOnly CacheMagic As Byte() = {&H46, &H44, &H34, &H49} ' "FD4I"
    Private Const CacheFormatVersion As UShort = 1US
    Private Const CacheFileSuffix As String = ".cac"
    Private Const CacheTempSuffix As String = ".cac.tmp"

    Private Shared _canonicalExtensionsSnapshot As List(Of String) = Nothing

    Private Structure CachedEntry
        Public Index As Integer
        Public FullPath As String
    End Structure

    Private Shared Function IsCacheEnabled() As Boolean
        Return Not String.IsNullOrEmpty(_cacheDirectory)
    End Function

    Private Shared Function GetCacheFilePath(archiveFileName As String) As String
        If Not IsCacheEnabled() Then Return ""
        ' Subcarpeta por juego. WriteCacheFile crea el directorio a partir de este path, así que no hace
        ' falta crearlo antes en ningún lado.
        Return Path.Combine(EffectiveCacheDirectory(), archiveFileName & CacheFileSuffix)
    End Function

    Private Shared Function BuildCanonicalExtensionsSnapshot() As List(Of String)
        Dim result As New List(Of String)(SupportedExtensions.Count)
        For Each ext In SupportedExtensions
            If Not String.IsNullOrEmpty(ext) Then result.Add(ext.ToLowerInvariant())
        Next
        result.Sort(StringComparer.Ordinal)
        Return result
    End Function

    Private Shared Sub WriteUtf8String(bw As BinaryWriter, s As String)
        If s Is Nothing Then s = ""
        Dim bytes = Encoding.UTF8.GetBytes(s)
        If bytes.Length > UInt16.MaxValue Then
            Throw New InvalidDataException("Archive cache: string exceeds u16 length prefix.")
        End If
        bw.Write(CUShort(bytes.Length))
        If bytes.Length > 0 Then bw.Write(bytes)
    End Sub

    Private Shared Function ReadUtf8String(br As BinaryReader) As String
        Dim len As UShort = br.ReadUInt16()
        If len = 0US Then Return ""
        Dim bytes = br.ReadBytes(CInt(len))
        If bytes.Length <> CInt(len) Then Throw New EndOfStreamException("Archive cache: short string read.")
        Return Encoding.UTF8.GetString(bytes)
    End Function

    Private Shared Function TryLoadArchiveIndex(
        cachePath As String,
        expectedSize As Long,
        expectedMtimeUtc As Date,
        expectedExtsCanonical As List(Of String),
        ByRef entries As List(Of CachedEntry)) As Boolean

        entries = Nothing
        If String.IsNullOrEmpty(cachePath) Then Return False
        If Not File.Exists(cachePath) Then Return False

        Try
            Using fs As FileStream = File.OpenRead(cachePath)
                Using br As New BinaryReader(fs, Encoding.UTF8, leaveOpen:=False)
                    Dim magic = br.ReadBytes(4)
                    If magic.Length <> 4 Then Return False
                    For i As Integer = 0 To 3
                        If magic(i) <> CacheMagic(i) Then Return False
                    Next

                    Dim version As UShort = br.ReadUInt16()
                    If version <> CacheFormatVersion Then Return False

                    Dim cachedSize As Long = br.ReadInt64()
                    If cachedSize <> expectedSize Then Return False

                    Dim cachedMtimeBinary As Long = br.ReadInt64()
                    Dim cachedMtime = Date.FromBinary(cachedMtimeBinary)
                    If cachedMtime <> expectedMtimeUtc Then Return False

                    Dim extCount As UInteger = br.ReadUInt32()
                    If extCount > 1024UI Then Return False
                    If CInt(extCount) <> expectedExtsCanonical.Count Then Return False
                    For i As Integer = 0 To CInt(extCount) - 1
                        Dim ext = ReadUtf8String(br)
                        If Not String.Equals(ext, expectedExtsCanonical(i), StringComparison.Ordinal) Then Return False
                    Next

                    Dim entryCount As UInteger = br.ReadUInt32()
                    If entryCount > 10000000UI Then Return False
                    Dim result As New List(Of CachedEntry)(CInt(entryCount))
                    For i As Integer = 0 To CInt(entryCount) - 1
                        Dim idx As Integer = br.ReadInt32()
                        Dim fullPath = ReadUtf8String(br)
                        result.Add(New CachedEntry With {.Index = idx, .FullPath = fullPath})
                    Next

                    entries = result
                    Return True
                End Using
            End Using
        Catch
            entries = Nothing
            Return False
        End Try
    End Function

    Private Shared Sub SaveArchiveIndex(
        cachePath As String,
        archiveSize As Long,
        archiveMtimeUtc As Date,
        extsCanonical As List(Of String),
        entries As List(Of CachedEntry))

        If String.IsNullOrEmpty(cachePath) Then Return

        Dim dir = Path.GetDirectoryName(cachePath)
        If Not String.IsNullOrEmpty(dir) AndAlso Not Directory.Exists(dir) Then
            Directory.CreateDirectory(dir)
        End If

        Dim temp = cachePath & ".tmp"
        Try
            Using fs As FileStream = File.Create(temp)
                Using bw As New BinaryWriter(fs, Encoding.UTF8, leaveOpen:=False)
                    bw.Write(CacheMagic)
                    bw.Write(CacheFormatVersion)
                    bw.Write(archiveSize)
                    bw.Write(archiveMtimeUtc.ToBinary())
                    bw.Write(CUInt(extsCanonical.Count))
                    For Each ext In extsCanonical
                        WriteUtf8String(bw, ext)
                    Next
                    bw.Write(CUInt(entries.Count))
                    For Each e In entries
                        bw.Write(e.Index)
                        WriteUtf8String(bw, e.FullPath)
                    Next
                End Using
            End Using

            If File.Exists(cachePath) Then
                File.Replace(temp, cachePath, Nothing, ignoreMetadataErrors:=True)
            Else
                File.Move(temp, cachePath)
            End If
        Catch
            Try
                If File.Exists(temp) Then File.Delete(temp)
            Catch
            End Try
            Throw
        End Try
    End Sub

    ''' <summary>Borra los <c>.cac</c> del juego ACTIVO que ya no corresponden a ningún archive escaneado.
    '''
    ''' <para>⛔ Opera SÓLO dentro de <see cref="EffectiveCacheDirectory"/> (la subcarpeta del juego) y
    ''' <c>EnumerateFiles</c> NO recursa ⇒ el cache del otro juego es literalmente invisible para este
    ''' barrido. Antes los dos juegos compartían carpeta y este mismo código, al no encontrar los archives
    ''' del otro juego en <c>scannedArchives</c>, los declaraba huérfanos y los BORRABA: cambiar de juego
    ''' costaba un re-index completo.</para></summary>
    Private Shared Sub CleanupOrphanCacheFiles(scannedArchives As IEnumerable(Of String))
        If Not IsCacheEnabled() Then Return

        PurgeLegacyRootCaches()

        Dim dir = EffectiveCacheDirectory()
        If Not Directory.Exists(dir) Then Return

        Try
            Dim validNames As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
            For Each ba2 In scannedArchives
                validNames.Add(Path.GetFileName(ba2) & CacheFileSuffix)
            Next

            For Each cacheFile In Directory.EnumerateFiles(dir, "*" & CacheFileSuffix)
                Dim name = Path.GetFileName(cacheFile)
                If Not validNames.Contains(name) Then
                    Try
                        File.Delete(cacheFile)
                    Catch
                    End Try
                End If
            Next

            ' Clean leftover temp files from aborted writes.
            For Each tmpFile In Directory.EnumerateFiles(dir, "*" & CacheTempSuffix)
                Try
                    File.Delete(tmpFile)
                Catch
                End Try
            Next
        Catch ex As Exception
            _scanErrors.Enqueue("Cache cleanup failed: " & ex.Message)
        End Try
    End Sub

    ''' <summary>Migración de una sola vez: barre los <c>.cac</c> que quedaron en la RAÍZ del cache, de
    ''' cuando los dos juegos compartían carpeta.
    '''
    ''' <para>Son inalcanzables desde el momento en que los <c>.cac</c> pasaron a vivir en la subcarpeta
    ''' del juego: nadie los lee, y el barrido por-juego no los ve. Sin esto quedarían de basura para
    ''' siempre.</para>
    '''
    ''' <para>Borrar es seguro: un <c>.cac</c> es cache PURO derivado del archive (índice de entradas), y
    ''' se regenera solo. El peor caso es un re-index una única vez por juego. Además NO se puede saber a
    ''' qué juego pertenece cada uno (justamente el bug que arreglamos), así que conservarlos tampoco
    ''' serviría de nada. <c>EnumerateFiles</c> no recursa ⇒ las subcarpetas por juego quedan intactas.</para></summary>
    Private Shared Sub PurgeLegacyRootCaches()
        If Not IsCacheEnabled() Then Return

        ' Las DOS ubicaciones que quedaron obsoletas, en orden histórico:
        '   1. la RAÍZ del cache        — de cuando los dos juegos compartían carpeta;
        '   2. {raíz}\{Juego}\          — de cuando un juego tenía UNA sola carpeta para todos los sets de
        '                                 extensiones (ver EffectiveCacheDirectory).
        ' Ninguna de las dos es alcanzable ya: nadie las lee y el barrido por-set no las ve. EnumerateFiles
        ' NO recursa, así que borrar en {raíz}\{Juego}\ deja intactas las subcarpetas por-set que cuelgan de
        ' ella. Borrar es seguro en cualquier caso: un .cac es cache PURO derivado del archive y se regenera
        ' solo; el peor caso es un re-index una única vez.
        For Each legacyDir In {_cacheDirectory, Path.Combine(_cacheDirectory, GameCacheFolderName())}
            Try
                If Not Directory.Exists(legacyDir) Then Continue For
                For Each pat In {"*" & CacheFileSuffix, "*" & CacheTempSuffix}
                    For Each f In Directory.EnumerateFiles(legacyDir, pat)   ' sólo ese nivel, no recursa
                        Try
                            File.Delete(f)
                        Catch
                        End Try
                    Next
                Next
            Catch
                ' Best-effort: si no se puede limpiar, son bytes muertos y nada más. No romper el scan por esto.
            End Try
        Next
    End Sub
    ' =========================================================


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

    ''' <summary>Normalizes a path into a dictionary key. This is the HOT LOOKUP path (GetBytes,
    ''' GetOverriddenEntries, RemoveDictionaryEntry…), so it must stay allocation-free for the common
    ''' case — String.Replace returns the same instance when there is nothing to replace. It used to
    ''' String.Intern the result, which permanently retained every string anyone ever looked up; see
    ''' <see cref="PoolPath"/> for why that went away and where de-duplication happens now.</summary>
    Private Shared Function NormalizeDictionaryKey(fullPath As String) As String
        If IsNothing(fullPath) Then Return ""
        Return fullPath.Correct_Path_Separator
    End Function

    ''' <summary>Inserts <paramref name="entry"/> under <paramref name="key"/> during a scan, resolving a
    ''' collision with any entry already there via <see cref="Resolve_Conflict"/> and pushing the LOSER
    ''' onto the override stack.
    '''
    ''' <para>⛔ Why this exists instead of ConcurrentDictionary.AddOrUpdate: AddOrUpdate's
    ''' updateValueFactory is documented to run MORE THAN ONCE when its CAS loses a race, and the previous
    ''' code called PushOverriddenEntry from INSIDE that factory — so a losing attempt pushed a loser onto
    ''' the override stack and then pushed it again on the retry, leaving duplicate/phantom entries. That
    ''' was rare with 4 workers and becomes routine as the worker count goes up. Here the push happens only
    ''' after the compare-and-swap that actually won.</para></summary>
    Private Shared Sub AddEntryResolvingConflict(key As String, entry As File_Location)
        Do
            Dim existing As File_Location = Nothing
            If Not _dictionary.TryGetValue(key, existing) Then
                If _dictionary.TryAdd(key, entry) Then Return
                Continue Do   ' another worker inserted between the read and the add — re-resolve against it
            End If

            If Resolve_Conflict(existing, entry) Then
                ' New entry wins. Swap it in, then retire the loser. TryUpdate fails only if someone
                ' changed the slot meanwhile, in which case we re-resolve against the new occupant.
                If _dictionary.TryUpdate(key, entry, existing) Then
                    PushOverriddenEntry(key, existing)
                    Return
                End If
            Else
                ' Existing wins: OUR entry is the loser. The slot is untouched, so there is nothing to CAS.
                PushOverriddenEntry(key, entry)
                Return
            End If
        Loop
    End Sub

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
        IndexNormalizedKey(NormalizeDictionaryKey(fullKey))
    End Sub

    ''' <summary>Index a key that is ALREADY normalized (dictionary keys always are — every insert path
    ''' goes through <see cref="NormalizeDictionaryKey"/> or stores a <see cref="PoolPath"/>'d
    ''' Correct_Path_Separator'd path). Splitting this out of <see cref="IndexDictionaryKey"/> is what lets
    ''' the rebuild skip re-normalizing millions of keys that are normalized by construction.
    '''
    ''' <para>The directory and extension keys are each computed ONCE. The old code derived them, then
    ''' handed them to BuildDirectoryExtensionBucketKey, which normalized BOTH AGAIN (a second
    ''' Correct_Path_Separator + Trim + trailing-slash strip, and a second ToLowerInvariant allocation) —
    ''' four normalization passes per key where one does. Same normalizers, same resulting bucket strings;
    ''' only the redundant work is gone, so the lookup side (which still calls the normalizers on the
    ''' caller's argument) keeps matching exactly.</para></summary>
    Private Shared Sub IndexNormalizedKey(fullKey As String)
        If String.IsNullOrEmpty(fullKey) Then Exit Sub

        Dim directoryKey = NormalizeDirectoryKey(IO.Path.GetDirectoryName(fullKey))
        Dim extensionKey = NormalizeExtensionKey(IO.Path.GetExtension(fullKey))

        ' Only maintained once someone has actually asked for it — see _KeysByDirectory.
        If Volatile.Read(_keysByDirectoryBuilt) Then
            AddKeyToSearchIndex(_KeysByDirectory, directoryKey, fullKey)
        End If

        If extensionKey <> "" Then
            AddKeyToSearchIndex(_KeysByDirectoryExtension, directoryKey & "|" & extensionKey, fullKey)
        End If
    End Sub

    ''' <summary>Build <see cref="_KeysByDirectory"/> on demand, from the dictionary as it stands. Idempotent
    ''' and thread-safe; the flag is published INSIDE the lock and only after the index is fully populated, so
    ''' a concurrent <see cref="IndexNormalizedKey"/> either sees "not built" (and skips, because this pass
    ''' will pick its key up from the dictionary anyway) or sees "built" (and maintains it from then on).</summary>
    Private Shared Sub EnsureKeysByDirectoryBuilt()
        If Volatile.Read(_keysByDirectoryBuilt) Then Exit Sub
        SyncLock _keysByDirectoryLock
            If _keysByDirectoryBuilt Then Exit Sub
            _KeysByDirectory.Clear()
            For Each kvp In _dictionary
                Dim key = kvp.Key
                If String.IsNullOrEmpty(key) Then Continue For
                AddKeyToSearchIndex(_KeysByDirectory, NormalizeDirectoryKey(IO.Path.GetDirectoryName(key)), key)
            Next
            Volatile.Write(_keysByDirectoryBuilt, True)
        End SyncLock
    End Sub

    Private Shared Sub ClearSearchIndexes()
        SyncLock _keysByDirectoryLock
            _KeysByDirectory.Clear()
            Volatile.Write(_keysByDirectoryBuilt, False)
        End SyncLock
        _KeysByDirectoryExtension.Clear()
    End Sub

    ''' <summary>Rebuilds the search index from the dictionary. Runs in PARALLEL: this is a pass over every
    ''' key (millions on a modded install), and each key costs a GetDirectoryName + GetExtension +
    ''' ToLowerInvariant plus a ConcurrentDictionary insert hashed OrdinalIgnoreCase over a long path.
    ''' Serially that was tens of seconds at the tail of the scan, with the progress bar already full.
    ''' Safe to parallelize: the index and its buckets are ConcurrentDictionary, and the inserts are
    ''' order-independent (a set of keys per bucket — no last-writer-wins semantics to preserve).
    ''' AddKeyToSearchIndex's GetOrAdd factory may run twice under contention; the loser is a discarded
    ''' empty bucket.
    '''
    ''' <para>Iterates the dictionary directly rather than <c>.Keys</c>: that property takes every internal
    ''' lock and materializes a snapshot ARRAY of all keys — tens of MB of pure garbage at the exact moment
    ''' the process is already at its peak. The ConcurrentDictionary enumerator is lock-free and safe to use
    ''' concurrently with writers by design, and here there are none anyway (the scan workers have joined).</para></summary>
    Private Shared Sub RebuildSearchIndexesFromDictionary()
        ClearSearchIndexes()
        Parallel.ForEach(_dictionary, Sub(kvp) IndexNormalizedKey(kvp.Key))
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

    ''' <summary>Adds a new entry or updates an existing one (e.g. a BA2 entry replaced by a loose file
    ''' that WM or the material cloner just wrote). Only BA2 entries are pushed to the override stack
    ''' (loose-over-loose is the same file overwritten, so there is nothing to restore).
    '''
    ''' <para>⛔ Explicit CAS, not ConcurrentDictionary.AddOrUpdate — same reason as
    ''' <see cref="AddEntryResolvingConflict"/>: AddOrUpdate's updateValueFactory may run MORE THAN ONCE
    ''' when its CAS loses a race, and this factory has a SIDE EFFECT (PushOverriddenEntry), so a losing
    ''' attempt would push the same loser twice. The exposure here is far smaller than in the scan (callers
    ''' write one distinct key per file they produced, and the factory only retries on same-key contention),
    ''' but the trap is identical, so it does not get to stay.</para>
    '''
    ''' <para>NOTE the semantics differ from the scan path: this does NOT consult Resolve_Conflict. The
    ''' caller's entry always wins — it just wrote the file — so there is no winner to compute.</para></summary>
    Public Shared Sub AddOrUpdateDictionaryEntry(fullPath As String, location As File_Location)
        Dim normalized = NormalizeDictionaryKey(fullPath)

        Do
            Dim existing As File_Location = Nothing
            If Not _dictionary.TryGetValue(normalized, existing) Then
                If _dictionary.TryAdd(normalized, location) Then Exit Do
                Continue Do   ' someone inserted between the read and the add — re-read and replace it
            End If

            ' Replace, then retire the loser — but only once the swap actually landed. TryUpdate fails
            ' only if another thread changed the slot meanwhile, in which case we retry against the new
            ' occupant (and push THAT one, not the stale one we had read).
            If _dictionary.TryUpdate(normalized, location, existing) Then
                If Not existing.IsLosseFile Then PushOverriddenEntry(normalized, existing)
                Exit Do
            End If
        Loop

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
                    If _KeysByDirectoryExtension.TryGetValue(BuildDirectoryExtensionBucketKey(directoryKey, extensionKey), bucket) Then bucket.TryRemove(normalized, 0)
                End If
            End If
        End If
    End Sub

    ''' <summary>
    ''' Mounts a BA2/BSA archive at runtime, populating Dictionary with all of its supported entries.
    ''' Use this when adding archives generated after Fill_DictionaryAsync has run (e.g. WM Pack output).
    ''' Idempotent: a second call on the same archive name is a no-op (call UnregisterArchive first if
    ''' the archive content changed and needs to be re-read).
    ''' </summary>
    ''' <param name="archivePath">Absolute or Data-relative path to the .ba2 or .bsa file.</param>
    ''' <param name="sourceOrder">Resolve_Conflict priority. Default makes runtime-registered archives
    ''' win over any scan-time archive while still letting loose files override them.</param>
    Public Shared Sub RegisterArchive(archivePath As String,
                                      Optional sourceOrder As Integer = ArchiveSourceOrder_RuntimeRegistered)
        If String.IsNullOrWhiteSpace(archivePath) Then Throw New ArgumentException("archivePath is empty.", NameOf(archivePath))

        Dim absolutePath As String = If(Path.IsPathRooted(archivePath),
                                        archivePath,
                                        Path.Combine(FO4Path, archivePath))
        If Not File.Exists(absolutePath) Then
            Throw New FileNotFoundException("Archive not found: " & absolutePath, absolutePath)
        End If

        Dim archiveFileName = Path.GetFileName(absolutePath)
        If Not _registeredArchives.TryAdd(archiveFileName, 0) Then Exit Sub

        Dim added As New ConcurrentBag(Of String)()
        Dim noopProgress As IProgress(Of (String, Integer, Integer)) =
            New Progress(Of (String, Integer, Integer))(Sub(_x)
                                                            ' no-op: runtime register doesn't surface progress
                                                        End Sub)

        ProcessBa2File(absolutePath, sourceOrder, noopProgress, added)

        ' Index only the keys touched by this archive instead of rebuilding the entire search index.
        For Each key In added
            IndexDictionaryKey(key)
        Next
    End Sub

    ''' <summary>
    ''' Unmounts an archive registered at runtime (or discovered by the initial scan): removes its
    ''' entries from Dictionary, restoring any previously overridden entry from the override stack,
    ''' and disposes pooled readers for the archive file.
    ''' Safe to call on archives that aren't currently mounted (no-op).
    ''' </summary>
    ''' <param name="archivePath">Absolute or Data-relative path to the .ba2 or .bsa file.</param>
    Public Shared Sub UnregisterArchive(archivePath As String)
        If String.IsNullOrWhiteSpace(archivePath) Then Throw New ArgumentException("archivePath is empty.", NameOf(archivePath))

        Dim absolutePath As String = If(Path.IsPathRooted(archivePath),
                                        archivePath,
                                        Path.Combine(FO4Path, archivePath))
        Dim archiveFileName = Path.GetFileName(absolutePath)

        ' Snapshot keys to remove before mutating the dictionary.
        Dim toRemove As New List(Of String)
        For Each kvp In _dictionary
            If kvp.Value IsNot Nothing AndAlso
               kvp.Value.BA2File.Equals(archiveFileName, StringComparison.OrdinalIgnoreCase) Then
                toRemove.Add(kvp.Key)
            End If
        Next

        For Each key In toRemove
            RemoveDictionaryEntry(key)
        Next

        ' Drop pooled readers for this archive (their backing FileStream may be invalid after rewrite).
        Dim bag As ConcurrentBag(Of (Reader As BSA_BA2_Library_DLL.BethesdaArchive.Core.BethesdaReader, Stream As FileStream)) = Nothing
        If _archivePool.TryRemove(absolutePath, bag) Then
            Dim entry As (Reader As BSA_BA2_Library_DLL.BethesdaArchive.Core.BethesdaReader, Stream As FileStream) = Nothing
            While bag.TryTake(entry)
                Try : entry.Reader.Dispose() : Catch : End Try
                Try : entry.Stream.Dispose() : Catch : End Try
            End While
        End If

        Dim removedFlag As Byte = 0
        _registeredArchives.TryRemove(archiveFileName, removedFlag)
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
            ' "Every file in this directory, any extension" — the only query that reads _KeysByDirectory.
            ' The index is not populated during the scan (see _KeysByDirectory); build it on first ask.
            EnsureKeysByDirectoryBuilt()
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

    ''' <param name="includeInactiveArchives">When True, archives belonging to plugins that are
    ''' NOT loaded are still indexed in the Dictionary, but ordered with the LOWEST SourceOrder so any
    ''' loaded plugin's archive (and loose files) wins on conflict. WM uses this so the user can
    ''' inspect/clone material from inactive mods. NPC_Manager uses False (default): only archives of
    ''' loaded plugins are indexed. False matches the engine; True is a WM-specific extension.</param>
    ''' <param name="loadedPlugins">The plugin set this session considers LOADED, in load order — the
    ''' single answer to "what is loaded", shared by records and assets. NPC_Manager passes the
    ''' Preflight selection (default-checked = the active load order, then the user's edits), so
    ''' unticking a plugin drops its records AND its archives together. Nothing (default) = read the
    ''' active load order from Plugins.txt, which is what the engine loads; WM and the CLI use that.
    ''' Before this existed, records came from the ticks while archives always came from Plugins.txt:
    ''' two different notions of "loaded", which let an unticked plugin's assets still be indexed while
    ''' its config (e.g. RaceMenu's races.ini) was skipped.</param>
    Public Shared Async Function Fill_DictionaryAsync(Fo4DataPath As String,
                                                      progress As IProgress(Of (Stepn As String, Value As Integer, Max As Integer)),
                                                      Optional includeInactiveArchives As Boolean = False,
                                                      Optional archiveByteProgress As IProgress(Of (Done As Long, Total As Long)) = Nothing,
                                                      Optional loadedPlugins As IEnumerable(Of String) = Nothing) As Task
        Try
            ' Sub-phase timings + counts. The preflight slowdown reports come from users' rigs, not from
            ' a repro we have, so the log has to say WHICH phase ate the time (enumerate / scan / index)
            ' and on what volume (archives, cache hits, loose, entries).
            Dim swTotal = System.Diagnostics.Stopwatch.StartNew()
            Dim swPhase = System.Diagnostics.Stopwatch.StartNew()
            _archivesFromCache = 0
            _archivesReindexed = 0

            FO4Path = Fo4DataPath
            Dictionary.Clear()
            _overriddenEntries.Clear()
            ClearSearchIndexes()

            ' Drop the previous scan's pooled paths — they're only referenced by the dictionary we
            ' just cleared. (String.Intern, which this replaced, could never release them.)
            _pathPool = New ConcurrentDictionary(Of String, String)(StringComparer.Ordinal)

            ' O1.1: Clear byte cache when dictionary is rebuilt
            ClearBytesCache()

            ' O1.2: Dispose idle readers and initialize pool cleanup timer
            DisposeIdleReaders()
            InitPoolCleanupTimer()

            ' Snapshot SupportedExtensions once per scan, BEFORE the loose walk that filters against it.
            ' Workers read these read-only, so extensions registered AFTER the scan starts won't affect
            ' this run (and can't mutate the set mid-enumeration).
            _canonicalExtensionsSnapshot = BuildCanonicalExtensionsSnapshot()
            Dim extensionsSnapshot As New HashSet(Of String)(SupportedExtensions, StringComparer.OrdinalIgnoreCase)

            Dim ba2Files = EnumerateFilesWithSymlinkSupport(Fo4DataPath, "*.ba2;*.bsa", False).
            OrderBy(Function(p) p, StringComparer.OrdinalIgnoreCase).
            ToList()

            Dim archivePriority = BuildArchivePriority(ba2Files, includeInactiveArchives, Fo4DataPath, loadedPlugins)
            Dim msArchiveEnum = swPhase.ElapsedMilliseconds
            Logger.LogLazy(Function() $"[FilesDictionary] archive enumerate + priority: {ba2Files.Count} archives in {msArchiveEnum} ms")
            swPhase.Restart()

            ' The queue is a BlockingCollection, not a plain ConcurrentQueue, because the loose WALK is now
            ' a PRODUCER that streams into it while the workers below are already draining it. Previously
            ' the walk had to finish and be fully materialized into a List before a single worker started,
            ' which meant the longest phase of the scan (a recursive traversal of a huge Data tree, through
            ' the USVFS hook under MO2) ran with every core idle and NOT ONE progress report emitted — the
            ' bar sat at 0% under the label "Mounting archives...", which is exactly the freeze users see.
            ' Now the walk overlaps the archive mounting and the loose insertion entirely.
            Dim workQueue As New BlockingCollection(Of DictionaryScanWorkItem)(New ConcurrentQueue(Of DictionaryScanWorkItem)())

            ' Sum the byte size of ONLY the archives we will actually index (those that passed the
            ' archivePriority filter and get enqueued below as IsArchive=True). This drives the
            ' byte-weighted Detail bar. A FileInfo.Length is a cheap stat; there are only tens-to-
            ' hundreds of archives. Wrap each in Try so a vanished file just contributes 0, no throw.
            Dim archiveBytesTotal As Long = 0
            Dim indexableArchiveCount As Integer = 0

            For Each ba2 In ba2Files
                Dim ba2Name = Path.GetFileName(ba2)
                Dim sourceOrder As Integer = Integer.MinValue
                If archivePriority.TryGetValue(ba2Name, sourceOrder) = False Then
                    ' Archive not in priority map = doesn't belong to any active plugin (and, if
                    ' includeInactiveArchives is False, doesn't belong to any inactive one either,
                    ' since BuildArchivePriority skips inactives in that mode). Skip indexing it
                    ' so an orphan/inactive .ba2 can't override vanilla paths.
                    Continue For
                End If

                Try
                    archiveBytesTotal += New FileInfo(ba2).Length
                Catch
                    ' File vanished between enumeration and stat — counts as 0, don't abort the scan.
                End Try

                indexableArchiveCount += 1
                workQueue.Add(New DictionaryScanWorkItem With {
                .IsArchive = True,
                .FilePath = ba2,
                .SourceOrder = sourceOrder
            })
            Next

            ' Reset and publish the byte total upfront. If there are no indexable archives (all loose),
            ' total is 0 and the (0,0) report + the consumer's b.Total>0 guard handle it gracefully.
            _archiveByteProgress = archiveByteProgress
            _archiveBytesDone = 0
            _archiveBytesTotal = archiveBytesTotal
            archiveByteProgress?.Report((0L, _archiveBytesTotal))

            ' ⛔ totalCount is now a MOVING target: it starts at the archives (the only thing we can count
            ' up front) and GROWS as the walk discovers loose files. Every consumer re-reads Max from each
            ' report and re-sets its bar's Maximum, so a growing Max is fine — the bar rubber-bands a little
            ' while the walk runs, which is a fair picture of "still discovering how much there is". What is
            ' NOT fine is reporting Max=0 to say "unknown": Wardrobe_Manager assigns Max to ProgressBar1
            ' .Maximum unconditionally, so a 0 there would blank its bar on every heartbeat.
            totalCount = indexableArchiveCount
            completed = 0
            Volatile.Write(_scanProductionComplete, False)
            ReportScan(progress, "Mounting archives…", 0, totalCount)

            ' Capped at 8 rather than ProcessorCount: on a cache MISS each archive worker opens a FileStream
            ' over a (possibly multi-GB) archive, and 16-32 concurrent big-archive reads on a spinning disk
            ' regress wall-clock instead of improving it. On a cache HIT no archive is opened at all (the
            ' .cac index is read instead) and the loose branch is pure CPU; both parallelize freely.
            Dim workerCount As Integer = Math.Min(8, Math.Max(1, Environment.ProcessorCount))

            Dim workers = Enumerable.Range(0, workerCount).
            Select(Function(funza)
                       Return Task.Run(
                           Sub()
                               ' Blocks while the queue is empty and the walk is still producing; ends
                               ' cleanly once CompleteAdding has been called AND the queue has drained.
                               For Each item In workQueue.GetConsumingEnumerable()
                                   If item.IsArchive Then
                                       ProcessBa2File(item.FilePath, item.SourceOrder, progress)
                                   Else
                                       ProcessLooseFile(item.FilePath, item.RelativePath, item.LooseLastWrite, progress)
                                   End If
                               Next
                           End Sub)
                   End Function).
            ToArray()

            ' --- Producer: the parallel loose walk, running CONCURRENTLY with the workers above. ---
            Dim looseCount As Integer = 0
            Try
                Dim walkDop As Integer = Math.Min(8, Math.Max(1, Environment.ProcessorCount))
                looseCount = Await Task.Run(
                    Function()
                        Return WalkLooseFilesParallel(Fo4DataPath, extensionsSnapshot, walkDop,
                            Sub(fullPath, relativePath, lastWrite)
                                Dim discovered = Interlocked.Increment(totalCount)
                                workQueue.Add(New DictionaryScanWorkItem With {
                                    .IsArchive = False,
                                    .FilePath = fullPath,
                                    .RelativePath = relativePath,
                                    .SourceOrder = Integer.MaxValue,
                                    .LooseLastWrite = lastWrite
                                })

                                ' Heartbeat so the walk is no longer a dark window. Throttled hard (every
                                ' 4096th file) for the same reason ProcessLooseFile's own reporting is:
                                ' each Report is a SynchronizationContext.Post the UI thread must drain.
                                If (discovered And WalkHeartbeatMask) = 0 Then
                                    Dim found = discovered - indexableArchiveCount
                                    ReportScan(progress,
                                               $"Scanning Data folder — {found:N0} loose files found…",
                                               Volatile.Read(completed), discovered)
                                End If
                            End Sub)
                    End Function).ConfigureAwait(False)
            Catch ex As Exception
                ' Swallow-and-record rather than rethrow: falling out of here without reaching the Finally
                ' would leave CompleteAdding uncalled and every worker blocked in GetConsumingEnumerable
                ' forever — a hang instead of a scan that came up short.
                _scanErrors.Enqueue("Loose file walk failed: " & ex.Message)
                Logger.LogLazy(Function() "[FilesDictionary] WalkLooseFilesParallel error: " & ex.ToString())
            Finally
                Volatile.Write(_scanProductionComplete, True)
                workQueue.CompleteAdding()
            End Try

            Dim msWalkAndScan = swPhase.ElapsedMilliseconds
            Await Task.WhenAll(workers).ConfigureAwait(False)

            ' Read the counters, not _scanReport: that's a queue the apps DRAIN, so it may still hold
            ' (or have already lost) items from an earlier scan.
            Dim hits = _archivesFromCache, missed = _archivesReindexed
            Dim msScan = swPhase.ElapsedMilliseconds
            Dim entryCount = _dictionary.Count
            Logger.LogLazy(Function() $"[FilesDictionary] walk+scan: {workerCount} workers, {looseCount} loose, {hits} cache-hit / {missed} re-indexed archives, {entryCount} entries in {msScan} ms (walk done at {msWalkAndScan} ms)")
            swPhase.Restart()

            ' Phase 2 of the "it looks frozen" problem: this pass runs with the bar already at 100% and the
            ' label still naming the last archive, so on a big install it reads as a hang at the very end.
            ' Say what it is doing.
            ReportScan(progress, "Building search index…", totalCount, totalCount)

            ' O1.3: Build the secondary index in a single batch pass after the parallel scan completes.
            ' This avoids lock contention on the ConcurrentDictionary index during parallel insert.
            RebuildSearchIndexesFromDictionary()

            Dim msIndex = swPhase.ElapsedMilliseconds
            Logger.LogLazy(Function() $"[FilesDictionary] index rebuild: {msIndex} ms")

            ' Remove cache files for archives that no longer exist in the data root.
            CleanupOrphanCacheFiles(ba2Files)

            Dim msTotal = swTotal.ElapsedMilliseconds
            Logger.LogLazy(Function() $"[FilesDictionary] Fill_DictionaryAsync total: {msTotal} ms")

            ' In-memory only (three stopwatches and a string — no I/O, no log). A caller that wants to profile
            ' a rig can read it; NPC_Manager only persists it under --diagnoseLoad. ⛔ Deliberately NOT wired
            ' to Logger.Enabled: that flag also drives FaceGenBuilder.DebugMode, so using it as a profiling
            ' switch would silently change how FaceGen bakes.
            LastScanDiagnostics =
                $"archives={ba2Files.Count} (indexed={indexableArchiveCount}, cache-hit={hits}, re-indexed={missed}), " &
                $"loose={looseCount}, entries={entryCount}, workers={workerCount} | " &
                $"archive-enum={msArchiveEnum}ms walk+scan={msScan}ms (walk={msWalkAndScan}ms) index={msIndex}ms TOTAL={msTotal}ms"

        Catch ex As Exception
            ' No MsgBox desde acá: después del ConfigureAwait(False) estamos en el
            ' ThreadPool, sin sync context de la UI. MsgBox desde worker cuelga.
            _scanErrors.Enqueue("Fill_DictionaryAsync failed: " & ex.Message)
            Logger.LogLazy(Function() "[FilesDictionary] Fill_DictionaryAsync error: " & ex.ToString())
        End Try
    End Function
    Private Shared Function ArchiveBelongsToPlugin(archiveFileName As String, pluginFileName As String) As Boolean
        Dim archiveBase = Path.GetFileNameWithoutExtension(archiveFileName)
        Dim pluginBase = Path.GetFileNameWithoutExtension(pluginFileName)
        If archiveBase.Equals(pluginBase, StringComparison.OrdinalIgnoreCase) Then Return True
        If archiveBase.StartsWith(pluginBase & " - ", StringComparison.OrdinalIgnoreCase) Then Return True
        Return False
    End Function

    ''' <summary>Inverts <see cref="ArchiveBelongsToPlugin"/> into a lookup: plugin base name → the archives
    ''' that belong to it, pre-sorted OrdinalIgnoreCase (the order the priority groups assign in).
    '''
    ''' <para>⛔ Why: the priority groups used to ask, for EVERY plugin in the load order, "which of the
    ''' still-unassigned archives belong to you?" — a LINQ scan of the whole pending set per plugin, with
    ''' ArchiveBelongsToPlugin allocating two substrings per comparison. A heavily modded load order is
    ''' thousands of plugins against hundreds of archives, so that is millions of comparisons and millions
    ''' of throwaway strings, and it runs BEFORE the first progress report — inside the window where the
    ''' user is already staring at a motionless bar.</para>
    '''
    ''' <para>The predicate is: archiveBase == pluginBase, OR archiveBase starts with pluginBase + " - ".
    ''' So the set of plugin bases that can claim a given archive is exactly {the full archive base} ∪ {every
    ''' prefix of it that ends right before a " - "}. There are one to three of those, so we enumerate them
    ''' once per ARCHIVE and index by them. Same matches, same order, no per-plugin scan.</para></summary>
    Private Shared Function BuildPluginBaseToArchives(archiveNames As IEnumerable(Of String)) As Dictionary(Of String, List(Of String))
        Const Sep As String = " - "
        Dim map As New Dictionary(Of String, List(Of String))(StringComparer.OrdinalIgnoreCase)

        Dim add = Sub(pluginBase As String, archiveName As String)
                      Dim bucket As List(Of String) = Nothing
                      If Not map.TryGetValue(pluginBase, bucket) Then
                          bucket = New List(Of String)
                          map(pluginBase) = bucket
                      End If
                      bucket.Add(archiveName)
                  End Sub

        For Each name In archiveNames
            Dim archiveBase = Path.GetFileNameWithoutExtension(name)
            add(archiveBase, name)

            ' Every prefix that ends immediately before a " - " is a plugin base this archive would match
            ' via the StartsWith arm. E.g. "Foo - Main.ba2" → also claimable by plugin "Foo"; a pathological
            ' "A - B - C.ba2" → also by "A" and by "A - B", exactly as the predicate says.
            Dim at As Integer = archiveBase.IndexOf(Sep, StringComparison.Ordinal)
            While at > 0
                add(archiveBase.Substring(0, at), name)
                at = archiveBase.IndexOf(Sep, at + 1, StringComparison.Ordinal)
            End While
        Next

        For Each bucket In map.Values
            bucket.Sort(StringComparer.OrdinalIgnoreCase)
        Next
        Return map
    End Function

    ''' <summary>Assign the next SourceOrder values to the still-pending archives claimed by
    ''' <paramref name="pluginFileName"/>, in OrdinalIgnoreCase name order. Mirrors what the old
    ''' <c>pending.Where(ArchiveBelongsToPlugin).OrderBy(name)</c> loop did, off the prebuilt lookup.</summary>
    Private Shared Sub AssignArchivesOfPlugin(pluginFileName As String,
                                              byPluginBase As Dictionary(Of String, List(Of String)),
                                              pending As HashSet(Of String),
                                              result As Dictionary(Of String, Integer),
                                              ByRef nextOrder As Integer)
        Dim candidates As List(Of String) = Nothing
        If Not byPluginBase.TryGetValue(Path.GetFileNameWithoutExtension(pluginFileName), candidates) Then Exit Sub

        For Each match In candidates          ' already sorted OrdinalIgnoreCase
            If Not pending.Remove(match) Then Continue For   ' claimed by an earlier group/plugin
            result(match) = nextOrder
            nextOrder += 1
        Next
    End Sub

    ''' <summary>Build the SourceOrder priority map for archives. Higher value = wins on conflict
    ''' (see Resolve_Conflict at line 1304). Priority, LOWEST → HIGHEST:
    '''   0. Orphan archives (BA2/BSA no plugin claims) — negative orders, below everything. The engine
    '''      would not load them at all (nothing mounts them short of an sResourceArchiveList entry), so
    '''      they must never shadow a real mod. They used to be assigned LAST, i.e. ABOVE every active
    '''      plugin: one stray leftover .ba2 in Data\ silently outranked vanilla and every active mod.
    '''      Kept in the map (when <paramref name="includeInactive"/>) so WM can still browse them; ordered
    '''      among themselves by mtime.
    '''   1. Inactive plugin archives (only when <paramref name="includeInactive"/> is True; WM uses this so
    '''      inspect can see inactive mod content but actives never lose).
    '''   2. Implicit base + DLC archives.
    '''   3. Loaded plugin archives, in load order (see loadedPlugins) — these win.
    ''' Archives whose plugin is not loaded AND <paramref name="includeInactive"/> is False are excluded
    ''' from the result entirely; the caller skips indexing them.</summary>
    Private Shared Function BuildArchivePriority(ba2Files As List(Of String),
                                                 includeInactive As Boolean,
                                                 dataPath As String,
                                                 Optional loadedPlugins As IEnumerable(Of String) = Nothing) As Dictionary(Of String, Integer)
        Dim result As New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)

        ' "Loaded" = the caller's set if it gave one (NPC_Manager: the Preflight selection), else the engine's
        ' active load order. Both groups below — the loaded archives AND the inactive ones — must be derived from
        ' the SAME set, or an unticked plugin would land in neither and its archive would vanish silently.
        Dim loadedOrder As List(Of String) = If(loadedPlugins Is Nothing,
                                                PluginManager.ReadActiveLoadOrder(),
                                                loadedPlugins.Where(Function(p) Not String.IsNullOrEmpty(p)).ToList())

        Dim archiveNames = ba2Files.
        Select(Function(p) Path.GetFileName(p)).
        OrderBy(Function(n) n, StringComparer.OrdinalIgnoreCase).
        ToList()

        Dim fullPathsByName = ba2Files.
        GroupBy(Function(p) Path.GetFileName(p), StringComparer.OrdinalIgnoreCase).
        ToDictionary(Function(g) g.Key, Function(g) g.First(), StringComparer.OrdinalIgnoreCase)

        Dim pending As New HashSet(Of String)(archiveNames, StringComparer.OrdinalIgnoreCase)
        Dim nextOrder As Integer = 0

        ' Built ONCE and shared by the two plugin-driven groups below (see BuildPluginBaseToArchives).
        Dim byPluginBase = BuildPluginBaseToArchives(archiveNames)

        ' Group 1: archives of inactive plugins. Only included in the map if the caller asked for
        ' it (WM mode). Order within this group: loadorder.txt if present (skipping anything that
        ' is in the active set), alphabetical fallback for anything on disk that loadorder.txt
        ' didn't list. Inactives are processed FIRST so they get the lowest SourceOrder — every
        ' active plugin's archive (and loose files) wins on conflict.
        If includeInactive Then
            For Each plugin In EnumerateInactivePlugins(dataPath, loadedOrder)
                AssignArchivesOfPlugin(plugin, byPluginBase, pending, result, nextOrder)
            Next
        End If

        ' Group 2: implicit base + DLC archives (always loaded by the engine regardless of any
        ' Plugins.txt / loadorder.txt state; matched by archive name prefix).
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

        ' Group 3: archives of the LOADED plugins, in load order (loadedOrder above). When the caller passes
        ' nothing this is PluginManager's canonical active load order (single source of truth: implicit DLCs,
        ' Creation Club entries from Fallout4.ccc/Skyrim.ccc, and Plugins.txt actives). Pre-2026-05-01 this read
        ' loadorder.txt directly via a duplicated parser that missed CC content entirely, leaving cc*.ba2 at
        ' fallback (mtime) priority — wrong against the engine.
        For Each plugin In loadedOrder
            AssignArchivesOfPlugin(plugin, byPluginBase, pending, result, nextOrder)
        Next

        ' Orphans. Archives in Data\ that no plugin claims. Only indexed when the caller wants to see
        ' inactive content (WM); in NPC mode (engine parity) an orphan archive isn't indexed at all — same
        ' as the engine ignoring it.
        ' They are identified LAST (whatever no plugin claimed) but must rank LOWEST: nothing mounts them
        ' in-game, so a leftover .ba2 must not shadow vanilla or an active mod. Every plugin-claimed archive
        ' above got an order >= 0, so orphans take NEGATIVE orders — still ordered among themselves by mtime.
        If includeInactive Then
            Dim fallbackMatches = pending.
            OrderBy(Function(name) File.GetLastWriteTimeUtc(fullPathsByName(name))).
            ThenBy(Function(name) name, StringComparer.OrdinalIgnoreCase).
            ToList()

            Dim orphanOrder As Integer = -fallbackMatches.Count
            For Each match In fallbackMatches
                result(match) = orphanOrder
                orphanOrder += 1
                pending.Remove(match)
            Next
        End If

        Return result
    End Function

    ''' <summary>Enumerate plugins on disk in <paramref name="dataPath"/> that are NOT loaded this session
    ''' (<paramref name="loadedOrder"/> — the caller's loaded set, i.e. the active load order unless the app
    ''' passed its own selection). Order: loadorder.txt order for entries it lists (filtered to the not-loaded
    ''' ones present on disk), then alphabetical for anything on disk that loadorder.txt didn't list.</summary>
    Private Shared Function EnumerateInactivePlugins(dataPath As String, loadedOrder As List(Of String)) As List(Of String)
        Dim result As New List(Of String)
        If String.IsNullOrEmpty(dataPath) OrElse Not Directory.Exists(dataPath) Then Return result

        Dim active = New HashSet(Of String)(loadedOrder, StringComparer.OrdinalIgnoreCase)

        Dim diskPlugins As New List(Of String)
        For Each ext In {"*.esp", "*.esm", "*.esl"}
            For Each fp In Directory.EnumerateFiles(dataPath, ext, SearchOption.TopDirectoryOnly)
                diskPlugins.Add(Path.GetFileName(fp))
            Next
        Next
        Dim diskSet = New HashSet(Of String)(diskPlugins, StringComparer.OrdinalIgnoreCase)

        ' VR builds (Fallout4VR / Skyrim VR) keep loadorder.txt in their own LocalAppData subdir; the
        ' shared resolver falls back to the VR folder when the base game folder is absent.
        Dim loadorderTxt = Path.Combine(PluginManager.ResolveGameAppDataDir(), "loadorder.txt")

        Dim emitted As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        If File.Exists(loadorderTxt) Then
            For Each line In File.ReadAllLines(loadorderTxt, Encoding.UTF8)
                Dim trimmed = line.Trim()
                If trimmed.Length = 0 Then Continue For
                If trimmed.StartsWith("#") OrElse trimmed.StartsWith(";") Then Continue For
                If trimmed.StartsWith("*") Then trimmed = trimmed.Substring(1).Trim()
                If trimmed.Length = 0 Then Continue For
                If active.Contains(trimmed) Then Continue For
                If Not diskSet.Contains(trimmed) Then Continue For
                If emitted.Add(trimmed) Then result.Add(trimmed)
            Next
        End If

        Dim leftovers = diskPlugins.
            Where(Function(p) Not active.Contains(p) AndAlso Not emitted.Contains(p)).
            OrderBy(Function(p) p, StringComparer.OrdinalIgnoreCase)
        For Each p In leftovers
            If emitted.Add(p) Then result.Add(p)
        Next

        Return result
    End Function
    Private Shared Sub ProcessBa2File(ba2 As String,
                                      sourceOrder As Integer,
                                      progress As IProgress(Of (String, Integer, Integer)),
                                      Optional addedKeys As ConcurrentBag(Of String) = Nothing)
        ' Declared at method scope with a safe default so the Finally can attribute this archive's
        ' bytes even if the FileInfo below throws (vanished file). Assigned once we have the FileInfo.
        Dim ba2Size As Long = 0
        Try
            ' O5.4: Intern the BA2 filename since it is stored in many File_Location instances
            Dim ba2FileName = String.Intern(Path.GetFileName(ba2))
            Dim fi As New FileInfo(ba2)
            ba2Size = fi.Length
            Dim ba2DateLocal As Date = fi.LastWriteTime   ' preserved for File_Location.FileDate
            Dim ba2DateUtc As Date = fi.LastWriteTimeUtc  ' cache signature component
            Dim extsCanonical = _canonicalExtensionsSnapshot
            Dim cachePath = GetCacheFilePath(ba2FileName)

            ' Cache hit: populate dict from index without opening the archive.
            Dim cachedEntries As List(Of CachedEntry) = Nothing
            If extsCanonical IsNot Nothing AndAlso
               TryLoadArchiveIndex(cachePath, ba2Size, ba2DateUtc, extsCanonical, cachedEntries) Then
                For Each ce In cachedEntries
                    Dim standardized = PoolPath(ce.FullPath)
                    Dim entry As New File_Location With {
                        .BA2File = ba2FileName,
                        .Index = ce.Index,
                        .FullPath = standardized,
                        .SourceOrder = sourceOrder,
                        .FileDate = ba2DateLocal
                    }
                    AddEntryResolvingConflict(standardized, entry)
                    addedKeys?.Add(standardized)
                Next
                _scanReport.Enqueue((ba2FileName, True))
                Interlocked.Increment(_archivesFromCache)
                Return
            End If

            ' Cache miss: open the archive, filter entries by SupportedExtensions,
            ' populate dict and collect for cache write.
            Dim collected As List(Of CachedEntry) = Nothing
            If extsCanonical IsNot Nothing AndAlso IsCacheEnabled() Then
                collected = New List(Of CachedEntry)
            End If

            Using fs As FileStream = File.OpenRead(ba2)
                Using arc As New BSA_BA2_Library_DLL.BethesdaArchive.Core.BethesdaReader(fs)
                    For Each fil In arc.EntriesFiles
                        Dim rawPath = fil.FullPath.Correct_Path_Separator
                        Dim extKey = NormalizeExtensionKey(IO.Path.GetExtension(rawPath))
                        If extKey = "" OrElse Not SupportedExtensions.Contains(extKey) Then Continue For

                        ' O5.4: De-dupe the standardized path — stored long-term as dictionary key and File_Location.FullPath
                        Dim standardized = PoolPath(rawPath)
                        Dim entry As New File_Location With {
                            .BA2File = ba2FileName,
                            .Index = fil.Index,
                            .FullPath = standardized,
                            .SourceOrder = sourceOrder,
                            .FileDate = ba2DateLocal
                        }

                        ' O1.3: During scan, only populate _dictionary; indexes are built in batch after scan
                        AddEntryResolvingConflict(standardized, entry)
                        addedKeys?.Add(standardized)

                        collected?.Add(New CachedEntry With {.Index = fil.Index, .FullPath = standardized})
                    Next
                End Using
            End Using
            _scanReport.Enqueue((ba2FileName, False))
            Interlocked.Increment(_archivesReindexed)

            If collected IsNot Nothing Then
                Try
                    SaveArchiveIndex(cachePath, ba2Size, ba2DateUtc, extsCanonical, collected)
                Catch ex As Exception
                    _scanErrors.Enqueue("Error saving cache for " & ba2FileName & ": " & ex.Message)
                End Try
            End If

        Catch ex As Exception
            _scanErrors.Enqueue("Error processing BA2 " & ba2 & ": " & ex.Message)
            Logger.LogLazy(Function() "[FilesDictionary] ProcessBa2File error: " & ex.ToString())
        Finally
            Dim current = Interlocked.Increment(completed)
            ReportScan(progress, $"Indexed: {Path.GetFileName(ba2)}", current, totalCount)

            ' Byte-weighted Detail bar (archives only). _archiveByteProgress is a Progress(Of T)
            ' created on the UI thread, so Report marshals back safely from this worker.
            If _archiveByteProgress IsNot Nothing Then
                Dim bd = Interlocked.Add(_archiveBytesDone, ba2Size)
                _archiveByteProgress.Report((bd, _archiveBytesTotal))
            End If
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

    ''' <param name="relativePath">Path relative to the data root, computed by the walk. It used to be
    ''' derived here with <c>Path.GetRelativePath(basePath, file)</c>, which re-normalizes both operands on
    ''' every call — once per loose file. The walk already knows the root, so it just cuts the prefix.</param>
    Private Shared Sub ProcessLooseFile(file As String, relativePath As String, lastWrite As Date, progress As IProgress(Of (String, Integer, Integer)))
        Try
            ' O5.4: De-dupe the standardized path — stored long-term as dictionary key and File_Location.FullPath
            Dim standardized = PoolPath(relativePath.Correct_Path_Separator)

            Dim entry As New File_Location With {
            .BA2File = String.Empty,
            .Index = -1,
            .FullPath = standardized,
            .SourceOrder = Integer.MaxValue,
            .FileDate = lastWrite
        }

            ' O1.3: During scan, only populate _dictionary; indexes are built in batch after scan
            AddEntryResolvingConflict(standardized, entry)

        Catch ex As Exception
            _scanErrors.Enqueue("Error processing loose file " & file & ": " & ex.Message)
            Logger.LogLazy(Function() "[FilesDictionary] ProcessLooseFile error: " & ex.ToString())
        Finally
            ' Throttled — see LooseProgressReportMask. The genuinely-last item always reports, so consumers
            ' that don't clamp the bar to Max still finish full; the _scanProductionComplete gate is what
            ' keeps "last item" from meaning "workers momentarily caught up with the walk" (see that field).
            Dim current = Interlocked.Increment(completed)
            If (current And LooseProgressReportMask) = 0 OrElse
               (Volatile.Read(_scanProductionComplete) AndAlso current >= totalCount) Then
                ReportScan(progress, $"Indexed: {Path.GetFileName(file)}", current, totalCount)
            End If
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
