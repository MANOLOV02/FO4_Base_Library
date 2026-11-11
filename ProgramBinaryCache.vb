Option Strict On
Option Explicit On
Imports System.IO
Imports System.Security.Cryptography
Imports System.Text
Imports System.Threading

''' <summary>THE PROGRAM BINARY CACHE (S0-A; user decision P2 of 29-oct-2026: programs built on demand in both games and both builds,
''' sped up by a standard cache that works on any card). glGetProgramBinary / glProgramBinary are core since OpenGL 4.1 (inside the
''' app's OpenGL 4.3, no extension); ProgramCompiler is its one user. Its root is Folder (Config_App.ShaderCacheDirectory: ShaderCache
''' next to config.json), never the driver's own cache (user order 27-oct-2026).
''' <para>THE IDENTITY (CacheIdentity): the context's GL_VENDOR / GL_RENDERER / GL_VERSION / GL_SHADING_LANGUAGE_VERSION and the state of
''' its creation that a link sees and the text does not (F9 r2 §2, the closed census of GL 4.6 §23's tables): GL_CONTEXT_FLAGS (its
''' robust-access bit included) and GL_CONTEXT_PROFILE_MASK. GL_RESET_NOTIFICATION_STRATEGY is not in it: it is not a core 4.3 token
''' (ARB / KHR_robustness, core in 4.5), and it is the same for every context the app creates, by construction - none asks for
''' robustness (OpenTK 4.7.4's NativeWindowSettings has no robustness attribute, GLFW's default is GLFW_NO_ROBUSTNESS, and without
''' WGL_CONTEXT_RESET_NOTIFICATION_STRATEGY_ARB the strategy is NO_RESET_NOTIFICATION_ARB, WGL_ARB_create_context_robustness), watched by
''' ParityGate program-compiler-single (no source asks for it). The build of the app is NOT in it (F9: a new build with the same texts
''' keeps its entries). THE KEY (ProgramCompiler.ProgramKey) has two levels: the program's digest (ProgramCompiler.ProgramDigest:
''' every stage's type and text hash in order, the transform-feedback varyings with their mode, the derivative hint and the
''' retrievable hint of its ProgramSpec), then the SHA-256 of FormatVersion, the identity and that digest. Another driver, card,
''' context or text never meets an entry of the old one.</para>
''' <para>ONE FOLDER PER RENDERER (S0 fase 7 rev-02, decided: a machine with two GPUs - an integrated and a discrete one - keeps both
''' sets): RendererFolder = the root's subfolder named by the SHA-256 of GL_VENDOR and GL_RENDERER. A process reads, writes and
''' cleans only its context's folder.</para>
''' <para>AN ENTRY: Magic, FormatVersion, the key, the identity (its five strings), binaryFormat, the binary's length, its SHA-256, the
''' binary. A read checks every field (the key and identity against the caller's, the hash against the bytes, the format against the
''' context's GL_PROGRAM_BINARY_FORMATS: OpenGL 4.6 7.5, glProgramBinary with a format the context does not list is INVALID_ENUM); an
''' entry that fails any of them is deleted and the program compiled. glProgramBinary may still refuse a well-formed entry
''' (LINK_STATUS false, OpenGL 4.6 7.5): ProgramCompiler then calls Refused and compiles.</para>
''' <para>WRITTEN to a pending file of its own (FileShare.None while written) and moved over the entry (File.Move with replace: one
''' rename on the volume), so a reader never sees half an entry and two instances writing the same key leave one whole entry.</para>
''' <para>CLEANUP, by rule (no size or age limit), the first time a process uses the root with an identity: in its renderer's folder,
''' every entry whose header is not of this FormatVersion and of exactly this identity is deleted - another driver version, GLSL
''' version or context: its key can no longer be met by this driver -, every entry of this identity whose key is not one of the
''' installed manifest's programs or of the host's registered ones (ProgramCatalog.KeptKeys, F9 r2 rev-07 (b): another text - no
''' build of the app asks for it; with no usable manifest nothing is deleted by text, and the log says so), and every pending file no writer
''' holds (a writer killed mid-write); in the root itself, the files of the flat layout of FormatVersion 1 (no reader looks there any
''' more). The other renderers' folders are not touched: each is cleaned by the processes that run on its renderer. So a renderer's
''' folder holds only programs its current driver and the current build of the app can ask for: it grows with the programs used,
''' never across driver or app versions.</para></summary>
Friend NotInheritable Class ProgramBinaryCache

    Private Sub New()
    End Sub

    ''' <summary>The layout of an entry and of the folder, and the key's first field: a change of either changes it, and the cleanup
    ''' deletes the old ones. 2 (S0 fase 7): one folder per renderer. 3 (F9): the identity without the build of the app and with the
    ''' context's flags and profile; the key in two levels.</summary>
    Friend Const FormatVersion As Integer = 3
    ''' <summary>"FPBC" read as a little-endian Int32: the first four bytes of every entry.</summary>
    Private Const Magic As Integer = &H43425046
    Friend Const EntryExtension As String = ".glprog"
    Private Const PendingExtension As String = ".pending"

    ''' <summary>What an entry is valid for: the four strings of a context that name its driver and the two values of its creation a
    ''' link sees (GL 4.6 §23: CONTEXT_FLAGS, CONTEXT_PROFILE_MASK, read with glGet in ProgramCompiler.ReadIdentity; the reset
    ''' notification strategy is the same for every context of the app, see the class) - part of the key and of every entry's header.</summary>
    Friend Structure CacheIdentity
        Friend Vendor As String
        Friend Renderer As String
        Friend Version As String
        Friend GlslVersion As String
        Friend ContextFlags As Integer
        Friend ProfileMask As Integer
    End Structure

    ''' <summary>What a cache operation did (counted in Debug: GateTally).</summary>
    Friend Enum Outcome
        Hit = 0
        Miss
        Store
        Unreadable
        HeaderRejected
        HashRejected
        FormatRejected
        DriverRejected
        ForeignRemoved
        PendingRemoved
        WriteFailure
        ''' <summary>F9: an entry of this identity whose program is not in the installed manifest nor registered (another text).</summary>
        TextRemoved
    End Enum
    Friend Const OutcomeCount As Integer = 12

    ''' <summary>An entry's header: what the cleanup reads, and what a load checks before the binary.</summary>
    Private NotInheritable Class Header
        Friend Version As Integer
        Friend Key As Byte()
        Friend Identity As CacheIdentity
        Friend BinaryFormat As Integer
        Friend Length As Integer
        Friend Hash As Byte()
    End Class

    Private Shared ReadOnly _lock As New Object()
    ''' <summary>The roots (with the identity they were cleaned for) this process already cleaned.</summary>
    Private Shared ReadOnly _cleaned As New HashSet(Of String)(StringComparer.Ordinal)

#If DEBUG Then
    ''' <summary>GATE ONLY (ShadowGate --program-cache): the root of the run, set before any program is built (Nothing = the app's).</summary>
    Friend Shared GateDirectory As String = Nothing

    ''' <summary>GATE ONLY (ShadowGate --program-cache): how many times each outcome happened in this process (index: Outcome).</summary>
    Friend Shared ReadOnly GateTally(OutcomeCount - 1) As Integer
#End If

    ''' <summary>The cache's root: Config_App.ShaderCacheDirectory (a gate's own folder in Debug, GateDirectory).</summary>
    Friend Shared ReadOnly Property Folder As String
        Get
#If DEBUG Then
            If GateDirectory IsNot Nothing Then Return GateDirectory
#End If
            Return Config_App.ShaderCacheDirectory
        End Get
    End Property

    ''' <summary>The folder of identity <paramref name="id"/>'s renderer under <paramref name="root"/>: named by the hex of the SHA-256 of
    ''' GL_VENDOR, a NUL and GL_RENDERER (UTF-8) - a name every file system takes, the same for every driver version of that renderer.</summary>
    Friend Shared Function RendererFolder(root As String, id As CacheIdentity) As String
        Dim name = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(If(id.Vendor, "") & ChrW(0) & If(id.Renderer, ""))))
        Return Path.Combine(root, name)
    End Function

    ''' <summary>The file of key <paramref name="key"/> in <paramref name="dir"/>.</summary>
    Friend Shared Function EntryPath(dir As String, key As Byte()) As String
        Return Path.Combine(dir, Convert.ToHexString(key) & EntryExtension)
    End Function

    ''' <summary>The identity an entry names is the caller's: the four strings ordinal and the two context values.</summary>
    Friend Shared Function IdentityMatches(a As CacheIdentity, b As CacheIdentity) As Boolean
        Dim version = String.Equals(a.Version, b.Version, StringComparison.Ordinal)
        Dim flags = a.ContextFlags = b.ContextFlags
        Dim profile = a.ProfileMask = b.ProfileMask
#If DEBUG Then
        ' GATE (ShadowGate --program-cache, subject mutants): a field out of the identity (and out of the key, ProgramCompiler.ProgramKey).
        If Fo4GBufferConstants.GateMutantOn("program-cache-key-no-version") Then version = True
        If Fo4GBufferConstants.GateMutantOn("program-cache-key-no-context-flags") Then flags = True
        If Fo4GBufferConstants.GateMutantOn("program-cache-key-no-profile") Then profile = True
#End If
        Return version AndAlso flags AndAlso profile AndAlso String.Equals(a.Vendor, b.Vendor, StringComparison.Ordinal) AndAlso
               String.Equals(a.Renderer, b.Renderer, StringComparison.Ordinal) AndAlso
               String.Equals(a.GlslVersion, b.GlslVersion, StringComparison.Ordinal)
    End Function

    ''' <summary>The binary of key <paramref name="key"/> for a context of identity <paramref name="id"/> whose binary formats are
    ''' <paramref name="formats"/>: True with its format and bytes when a whole entry for exactly that key, identity and a listed format
    ''' is there; False otherwise (no entry, or one that failed a check: deleted).</summary>
    Friend Shared Function TryLoad(key As Byte(), id As CacheIdentity, formats As HashSet(Of Integer),
                                   ByRef binaryFormat As Integer, ByRef binary As Byte()) As Boolean
        Dim root = Folder
        EnsureCleaned(root, id)
        Dim p = EntryPath(RendererFolder(root, id), key)
        If Not File.Exists(p) Then
            Count(Outcome.Miss)
            Return False
        End If
        Dim h As Header = Nothing
        Dim bytes As Byte() = Nothing
        If Not ReadEntry(p, True, h, bytes) OrElse h.Version <> FormatVersion Then
            Count(Outcome.Unreadable)
            Logger.LogLazy(Function() $"[PROGRAM-CACHE] {Path.GetFileName(p)}: not a whole entry of format {FormatVersion}, deleted")
            TryDelete(p)
            Return False
        End If
        If Not h.Key.SequenceEqual(key) OrElse Not IdentityMatches(h.Identity, id) Then
            Count(Outcome.HeaderRejected)
            Logger.LogLazy(Function() $"[PROGRAM-CACHE] {Path.GetFileName(p)}: header of another key, driver or context ({h.Identity.Renderer} / {h.Identity.Version} / flags 0x{h.Identity.ContextFlags:X} / profile 0x{h.Identity.ProfileMask:X}), deleted")
            TryDelete(p)
            Return False
        End If
        Dim hashOk = SHA256.HashData(bytes).SequenceEqual(h.Hash)
#If DEBUG Then
        ' GATE (ShadowGate --program-cache, subject mutant): the binary's hash not checked.
        If Fo4GBufferConstants.GateMutantOn("program-cache-skip-hash") Then hashOk = True
#End If
        If Not hashOk Then
            Count(Outcome.HashRejected)
            Logger.LogLazy(Function() $"[PROGRAM-CACHE] {Path.GetFileName(p)}: the binary does not match its hash, deleted")
            TryDelete(p)
            Return False
        End If
        If Not formats.Contains(h.BinaryFormat) Then
            Count(Outcome.FormatRejected)
            Logger.LogLazy(Function() $"[PROGRAM-CACHE] {Path.GetFileName(p)}: binary format 0x{h.BinaryFormat:X} not listed by the context, deleted")
            TryDelete(p)
            Return False
        End If
        binaryFormat = h.BinaryFormat
        binary = bytes
        Return True
    End Function

    ''' <summary>The entry just loaded linked (a hit).</summary>
    Friend Shared Sub Accepted()
        Count(Outcome.Hit)
    End Sub

    ''' <summary>glProgramBinary refused the entry of <paramref name="key"/> for identity <paramref name="id"/> (LINK_STATUS false,
    ''' OpenGL 4.6 7.5): deleted; the caller compiles the program and stores it again.</summary>
    Friend Shared Sub Refused(key As Byte(), id As CacheIdentity)
        Count(Outcome.DriverRejected)
        Dim p = EntryPath(RendererFolder(Folder, id), key)
        Logger.LogLazy(Function() $"[PROGRAM-CACHE] {Path.GetFileName(p)}: refused by the driver, deleted")
        TryDelete(p)
    End Sub

    ''' <summary>Writes the entry of <paramref name="key"/>: a pending file of its own, then moved over the entry. A folder that cannot
    ''' be written (read-only install, disk full) leaves the program built and the next start compiling it again; logged.</summary>
    Friend Shared Sub Store(key As Byte(), id As CacheIdentity, binaryFormat As Integer, binary As Byte())
        Dim root = Folder
        EnsureCleaned(root, id)
        Dim dir = RendererFolder(root, id)
        Dim final = EntryPath(dir, key)
        Dim pending = Path.Combine(dir, Convert.ToHexString(key) & "." & Guid.NewGuid().ToString("N") & PendingExtension)
        Try
            Directory.CreateDirectory(dir)
            Using fs As New FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.None),
                  w As New BinaryWriter(fs, Encoding.UTF8)
                w.Write(Magic)
                w.Write(FormatVersion)
                w.Write(key)
                w.Write(If(id.Vendor, ""))
                w.Write(If(id.Renderer, ""))
                w.Write(If(id.Version, ""))
                w.Write(If(id.GlslVersion, ""))
                w.Write(id.ContextFlags)
                w.Write(id.ProfileMask)
                w.Write(binaryFormat)
                w.Write(binary.Length)
                w.Write(SHA256.HashData(binary))
                w.Write(binary)
            End Using
            File.Move(pending, final, overwrite:=True)
            Count(Outcome.Store)
        Catch ex As Exception When TypeOf ex Is IOException OrElse TypeOf ex Is UnauthorizedAccessException
            Count(Outcome.WriteFailure)
            Logger.LogLazy(Function() $"[PROGRAM-CACHE] the entry {Path.GetFileName(final)} was not written: {ex.Message}")
            TryDelete(pending)
        End Try
    End Sub

    ''' <summary>The cleanup of root <paramref name="root"/> for identity <paramref name="id"/>, once per process (see the class): the
    ''' root's flat files of FormatVersion 1, then the renderer's folder - never another renderer's.</summary>
    Private Shared Sub EnsureCleaned(root As String, id As CacheIdentity)
        Dim memo = String.Join(ChrW(0), root, id.Vendor, id.Renderer, id.Version, id.GlslVersion, id.ContextFlags, id.ProfileMask)
        SyncLock _lock
            If Not _cleaned.Add(memo) Then Return
        End SyncLock
#If DEBUG Then
        ' GATE (ShadowGate --program-cache, subject mutant): no cleanup.
        If Fo4GBufferConstants.GateMutantOn("program-cache-no-cleanup") Then Return
#End If
        If Not Directory.Exists(root) Then Return
        ' The flat layout of FormatVersion 1 (entries and pending files in the root itself): no reader of this version looks there.
        For Each f In Directory.EnumerateFiles(root)
            If f.EndsWith(EntryExtension, StringComparison.OrdinalIgnoreCase) Then
                If TryDelete(f) Then Count(Outcome.ForeignRemoved)
            ElseIf f.EndsWith(PendingExtension, StringComparison.OrdinalIgnoreCase) Then
                DeleteIfAbandoned(f)
            End If
        Next
        Dim folders As IEnumerable(Of String) = {RendererFolder(root, id)}
#If DEBUG Then
        ' GATE (ShadowGate --program-cache, subject mutant): the cleanup sweeps every renderer's folder (rev-02: the entries of the
        ' other GPU of the machine deleted by this one's first use, and back).
        If Fo4GBufferConstants.GateMutantOn("program-cache-clean-every-renderer") Then folders = Directory.EnumerateDirectories(root).ToList()
#End If
        ' F9 r2 rev-07 (b): the keys of the installed manifest's programs and of the registered ones; Nothing = no usable manifest.
        Dim kept = ProgramCatalog.KeptKeys(id)
        For Each rendererDir In folders
            If Not Directory.Exists(rendererDir) Then Continue For
            For Each f In Directory.EnumerateFiles(rendererDir)
                If f.EndsWith(EntryExtension, StringComparison.OrdinalIgnoreCase) Then
                    Dim h As Header = Nothing
                    If ReadEntry(f, False, h, Nothing) AndAlso h.Version = FormatVersion AndAlso IdentityMatches(h.Identity, id) Then
                        If kept Is Nothing OrElse kept.Contains(Convert.ToHexString(h.Key)) Then Continue For
                        If TryDelete(f) Then Count(Outcome.TextRemoved)
                        Continue For
                    End If
                    If TryDelete(f) Then Count(Outcome.ForeignRemoved)
                ElseIf f.EndsWith(PendingExtension, StringComparison.OrdinalIgnoreCase) Then
                    DeleteIfAbandoned(f)
                End If
            Next
        Next
    End Sub

    ''' <summary>Deletes pending file <paramref name="f"/> when no writer holds it (its writer keeps it with FileShare.None until it is
    ''' moved): a writer killed mid-write.</summary>
    Private Shared Sub DeleteIfAbandoned(f As String)
        Try
            Using New FileStream(f, FileMode.Open, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose)
            End Using
            Count(Outcome.PendingRemoved)
        Catch ex As Exception When TypeOf ex Is IOException OrElse TypeOf ex Is UnauthorizedAccessException
            ' held by its writer, or already moved / deleted: not abandoned
        End Try
    End Sub

    ''' <summary>Reads entry <paramref name="p"/>: its header, and the binary when <paramref name="withBinary"/>. False when the file is
    ''' not a whole entry (magic, then for this FormatVersion a length that is the rest of the file) or cannot be read; an entry of
    ''' another FormatVersion returns True with only its Version read.</summary>
    Private Shared Function ReadEntry(p As String, withBinary As Boolean, ByRef h As Header, ByRef binary As Byte()) As Boolean
        Try
            Using fs As New FileStream(p, FileMode.Open, FileAccess.Read, FileShare.Read Or FileShare.Delete),
                  r As New BinaryReader(fs, Encoding.UTF8)
                If fs.Length < 8 OrElse r.ReadInt32() <> Magic Then Return False
                h = New Header With {.Version = r.ReadInt32()}
                If h.Version <> FormatVersion Then Return True
                h.Key = r.ReadBytes(32)
                h.Identity = New CacheIdentity With {.Vendor = r.ReadString(), .Renderer = r.ReadString(), .Version = r.ReadString(),
                                                     .GlslVersion = r.ReadString(), .ContextFlags = r.ReadInt32(), .ProfileMask = r.ReadInt32()}
                h.BinaryFormat = r.ReadInt32()
                h.Length = r.ReadInt32()
                h.Hash = r.ReadBytes(32)
                If h.Key.Length <> 32 OrElse h.Hash.Length <> 32 OrElse h.Length <= 0 OrElse fs.Length - fs.Position <> h.Length Then Return False
                If withBinary Then binary = r.ReadBytes(h.Length)
                Return True
            End Using
        Catch ex As Exception When TypeOf ex Is IOException OrElse TypeOf ex Is UnauthorizedAccessException OrElse TypeOf ex Is FormatException
            ' EndOfStreamException is an IOException (a truncated header); FormatException: a string length that is not one
            Return False
        End Try
    End Function

    ''' <summary>Deletes <paramref name="p"/>; False when it could not (gone already, or held).</summary>
    Private Shared Function TryDelete(p As String) As Boolean
        Try
            If Not File.Exists(p) Then Return False
            File.Delete(p)
            Return True
        Catch ex As Exception When TypeOf ex Is IOException OrElse TypeOf ex Is UnauthorizedAccessException
            Return False
        End Try
    End Function

    ''' <summary>One outcome counted (Debug: ShadowGate --program-cache reads GateTally; nothing in Release).</summary>
    <Diagnostics.Conditional("DEBUG")>
    Private Shared Sub Count(o As Outcome)
#If DEBUG Then
        Interlocked.Increment(GateTally(o))
#End If
    End Sub

End Class
