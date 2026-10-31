Option Strict On
Option Explicit On
Imports System.Collections.Concurrent
Imports System.IO
Imports System.Runtime.InteropServices
Imports System.Security.Cryptography
Imports System.Text
Imports OpenTK.Graphics.OpenGL4

''' <summary>A program whose stages did not compile or whose link failed (ProgramCompiler.Build): the message names the stage type
''' and carries the driver's log.</summary>
Friend NotInheritable Class ProgramBuildException
    Inherits Exception
    Friend Sub New(message As String)
        MyBase.New(message)
    End Sub
End Class

''' <summary>THE ONE OWNER OF BUILDING A GL PROGRAM (S0-A, user decision P2 of 29-oct-2026). Every program of the library and of its
''' tools is built here (Shader_Base_Class, FaceTintCompositor, TextOverlayRenderer, the ShadowGate harnesses); nothing else calls
''' glCreateShader / glCompileShader / glLinkProgram / glProgramBinary (ParityGate program-compiler-single).
''' <para>A BUILD: the stages' texts, in order, with their types (each text checked in Debug, Gl43.CheckShaderText) and the
''' transform-feedback varyings. With the context's program binary cache usable (it lists binary formats), the program's entry
''' (ProgramBinaryCache) is loaded with glProgramBinary and its LINK_STATUS checked: linked = done; refused = the entry deleted and
''' the program compiled. Compiled: each stage's shader object is the context's for that text (one compile per text per context,
''' OpenGL 4.6 7.3: a shader object may be attached to any number of programs), every new stage is compiled before the link and the
''' compile and link status are asked only after the link (no stage waits on another's status: the driver may overlap them), then
''' the binary is stored (PROGRAM_BINARY_RETRIEVABLE_HINT set before the link, OpenGL 4.6 7.5).</para>
''' <para>PER CONTEXT: the cache identity, the binary formats and the stage objects live with the context that made them -
''' InitContext / ReleaseContext, called by Gl43's (every context of the app is a PreviewControl's: Gl43.InitContext in OnLoad,
''' Gl43.ReleaseContext in Clean). A build in a context Gl43 did not initialise throws.</para></summary>
Friend NotInheritable Class ProgramCompiler

    Private Sub New()
    End Sub

    ''' <summary>A stage's shader object in its context: compiled once, its status asked once (Nothing = not asked yet).</summary>
    Private NotInheritable Class StageObject
        Friend Id As Integer
        Friend Type As ShaderType
        Friend Compiled As Boolean?
        Friend Log As String
    End Class

    ''' <summary>What a context holds for building: its cache identity (driver and build of the app), the binary formats it lists, its
    ''' stage objects by (type, SHA-256 of the text).</summary>
    Private NotInheritable Class ContextState
        Friend Identity As ProgramBinaryCache.CacheIdentity
        Friend ReadOnly Formats As New HashSet(Of Integer)
        Friend ReadOnly Stages As New Dictionary(Of String, StageObject)(StringComparer.Ordinal)
    End Class

    Private Shared ReadOnly _contexts As New ConcurrentDictionary(Of IntPtr, ContextState)()

#If DEBUG Then
    ''' <summary>GATE ONLY (ShadowGate --program-cache): the context lists no binary formats (GL_NUM_PROGRAM_BINARY_FORMATS = 0) - the
    ''' case of a driver without program binaries, on this one.</summary>
    Friend Shared GateNoBinaryFormats As Boolean = False
    ''' <summary>GATE ONLY: the programs Build returned in this process (cache or compile).</summary>
    Friend Shared GateBuilds As Integer
    ''' <summary>GATE ONLY (ShadowGate --program-first-frame): every program Build returned, with its build time and whether it came
    ''' from the cache.</summary>
    Friend Shared ReadOnly GateBuildLog As New List(Of (Program As Integer, Ms As Double, Hit As Boolean))
    ''' <summary>GATE ONLY (ShadowGate --program-abort): the name of the program slot (LazyProgram.Name) whose build fails, as a build
    ''' that does not compile would; Nothing = none.</summary>
    Friend Shared GateFailSlot As String = Nothing

    ''' <summary>DEBUG ONLY - A REAL COMPILE FAILURE, ONCE (S0 fase 7c rev-10): the name of the program slot (LazyProgram.Name; "*" = the
    ''' first slot that compiles a fragment) whose FRAGMENT stage is handed to glCompileShader broken (an #error line after its
    ''' #version line) the first time it is compiled; the stage object is kept under its real text's key, as a transient driver failure of
    ''' that text would leave it. Fires once per process (GateFailStageFired), then every compile is the real one. Set by ShadowGate
    ''' --program-abort, or in the apps (Debug build) from the environment variable FO4_DEBUG_FAIL_STAGE_ONCE (the slot's name or "*")
    ''' through PreviewControl.DebugFailStageOnceFromEnvironment, which only Wardrobe Manager and NPC Manager call at their start (S0
    ''' fase 7d: the library does not read the environment, so no harness - ShadowGate, the other gates, a run outside the battery - is
    ''' ever turned on by it): the program error card shows with its Retry button, and Retry compiles the stage again and draws the frame. Until it fires, the
    ''' slot it waits for is built without the program binary cache (Build), so it fires with a warm cache too.</summary>
    Friend Shared GateFailStageSlot As String = Nothing
    ''' <summary>DEBUG ONLY (S0 fase 7d): the name GBufferProgram builds a record's variant without early tests under
    ''' (GateBuildingSlot), so GateFailStageSlot can make that variant's fragment fail while its record compiles - the case that orphaned
    ''' the record (ShadowGate --program-abort). It is not a program slot: "*" (the first slot that compiles a fragment) never takes it,
    ''' only its own name does.</summary>
    Friend Const GateEarlyZVariantSlot As String = "FO4 G-buffer record without early tests"
    ''' <summary>DEBUG ONLY: the one-shot failure waits for the slot being built on this thread (not fired yet).</summary>
    Private Shared Function GateFailStagePending() As Boolean
        Return Not GateFailStageFired AndAlso GateFailStageSlot IsNot Nothing AndAlso GateBuildingSlot IsNot Nothing AndAlso
               ((GateFailStageSlot = "*" AndAlso GateBuildingSlot <> GateEarlyZVariantSlot) OrElse
                String.Equals(GateFailStageSlot, GateBuildingSlot, StringComparison.Ordinal))
    End Function
    ''' <summary>DEBUG ONLY: GateFailStageSlot already fired.</summary>
    Friend Shared GateFailStageFired As Boolean
    ''' <summary>DEBUG ONLY: the glCompileShader calls of this process (StageOf).</summary>
    Friend Shared GateShaderCompiles As Integer
    ''' <summary>DEBUG ONLY: the slot whose program is being built on this thread (LazyProgram.GetProgram sets it around its build).</summary>
    <ThreadStatic> Friend Shared GateBuildingSlot As String
#End If

    ''' <summary>The current context <paramref name="ctx"/> starts (Gl43.InitContext): its identity (the driver's four strings and the
    ''' build of the app, ProgramBinaryCache.AppBuild) and binary formats are read.</summary>
    Friend Shared Sub InitContext(ctx As IntPtr)
        Dim st As New ContextState With {.Identity = New ProgramBinaryCache.CacheIdentity With {
            .Vendor = GL.GetString(StringName.Vendor), .Renderer = GL.GetString(StringName.Renderer),
            .Version = GL.GetString(StringName.Version), .GlslVersion = GL.GetString(StringName.ShadingLanguageVersion),
            .AppBuild = ProgramBinaryCache.AppBuild}}
        Dim n = GL.GetInteger(GetPName.NumProgramBinaryFormats)
        If n > 0 Then
            Dim formats(n - 1) As Integer
            GL.GetInteger(GetPName.ProgramBinaryFormats, formats)
            For Each f In formats : st.Formats.Add(f) : Next
        End If
        _contexts(ctx) = st
    End Sub

    ''' <summary>Context <paramref name="ctx"/> is released (Gl43.ReleaseContext): its state forgotten, its stage objects deleted when
    ''' <paramref name="deleteObjects"/> (the context alive and current; otherwise they go with the context).</summary>
    Friend Shared Sub ReleaseContext(ctx As IntPtr, deleteObjects As Boolean)
        Dim st As ContextState = Nothing
        If Not _contexts.TryRemove(ctx, st) Then Return
        If deleteObjects Then DeleteStages(st)
    End Sub

    ''' <summary>THE RETRY OF THE STAGES (S0 fase 7c rev-10): a stage that did not compile is remembered by its object here
    ''' (StageObject.Compiled False) - CompileAndLink throws for it before any compile -, so a gesture's retry
    ''' (PreviewControl.ForgetProgramFailures: Retry, a reload, a change of game) deletes from the current context every stage object
    ''' whose compile failed (glDeleteShader) and forgets it: the next build of its text calls glCompileShader again. The stages that
    ''' compiled are kept (one compile per text per context). Nothing when the current context was not initialised. A failure is
    ''' remembered in one place for the user: the slot (LazyProgram), which the same gesture releases.</summary>
    Friend Shared Sub ForgetFailedStages()
        Dim st As ContextState = Nothing
        If Not _contexts.TryGetValue(Gl43.CurrentContext(), st) Then Return
#If DEBUG Then
        ' GATE (ShadowGate --program-abort, subject mutant): the failed stages kept (the defect of rev-10: a transient compile failure
        ' stays until the process ends).
        If Fo4GBufferConstants.GateMutantOn("program-retry-keeps-failed-stages") Then Return
#End If
        For Each kv In st.Stages.Where(Function(x) x.Value.Compiled.HasValue AndAlso Not x.Value.Compiled.Value).ToList()
            GL.DeleteShader(kv.Value.Id)
            st.Stages.Remove(kv.Key)
        Next
    End Sub

#If DEBUG Then
    ''' <summary>GATE ONLY (ShadowGate --program-census: one block at a time, its stages released with its programs; --program-cache's
    ''' warm phase): deletes the current context's stage objects. Debug only (S0 fase 7 rev-07: no Release caller).</summary>
    Friend Shared Sub ReleaseStages()
        DeleteStages(Current())
    End Sub

    ''' <summary>GATE ONLY: the current context's stage objects (ShadowGate --program-census reads it after ReleaseStages: 0).</summary>
    Friend Shared Function StageCount() As Integer
        Return Current().Stages.Count
    End Function
#End If

    Private Shared Sub DeleteStages(st As ContextState)
        For Each s In st.Stages.Values
            GL.DeleteShader(s.Id)
        Next
        st.Stages.Clear()
    End Sub

    Private Shared Function Current() As ContextState
        Dim st As ContextState = Nothing
        If Not _contexts.TryGetValue(Gl43.CurrentContext(), st) Then
            Throw New InvalidOperationException("ProgramCompiler: the current GL context was not initialised (Gl43.InitContext)")
        End If
        Return st
    End Function

    ''' <summary>The context can keep program binaries: it lists at least one binary format (none = the cache is off and every program
    ''' is compiled, on any card).</summary>
    Private Shared Function CacheUsable(st As ContextState) As Boolean
        Dim n = st.Formats.Count
#If DEBUG Then
        If GateNoBinaryFormats Then
            n = 0
            ' GATE (ShadowGate --program-cache, subject mutant): the "no formats, no cache" rule gone.
            If Fo4GBufferConstants.GateMutantOn("program-cache-ignore-no-formats") Then n = st.Formats.Count
        End If
#End If
        Return n > 0
    End Function

    ''' <summary>A compute program (one stage).</summary>
    Friend Shared Function BuildCompute(compute As String) As Integer
        Return Build({(ShaderType.ComputeShader, compute)})
    End Function

    ''' <summary>A vertex + fragment program.</summary>
    Friend Shared Function Build(vertex As String, fragment As String) As Integer
        Return Build({(ShaderType.VertexShader, vertex), (ShaderType.FragmentShader, fragment)})
    End Function

    ''' <summary>Builds the program of <paramref name="stages"/> (in order) capturing <paramref name="varyings"/> by transform feedback,
    ''' interleaved (OpenGL 4.6 11.1.2.1: declared before the link); returns the linked program. A stage that does not compile or a
    ''' link that fails throws ProgramBuildException with the driver's log, the program deleted.</summary>
    Friend Shared Function Build(stages As IReadOnlyList(Of (Type As ShaderType, Source As String)), Optional varyings As String() = Nothing) As Integer
        Dim st = Current()
#If DEBUG Then
        Dim reloj = Stopwatch.StartNew()
#End If
        For Each s In stages
            Gl43.CheckShaderText(s.Source)
        Next
        Dim hashes = stages.Select(Function(s) SHA256.HashData(Encoding.UTF8.GetBytes(s.Source))).ToArray()
        Dim useCache = CacheUsable(st)
#If DEBUG Then
        ' The one-shot compile failure (GateFailStageSlot) needs a compile: the slot it waits for is built without the cache until it fires
        ' (in the app the cache is warm after the first run, and a hit compiles nothing).
        If GateFailStagePending() Then useCache = False
#End If
        Dim key As Byte() = Nothing
        If useCache Then
            key = ProgramKey(st.Identity, stages, hashes, varyings)
            Dim fmt As Integer, bin As Byte() = Nothing
            If ProgramBinaryCache.TryLoad(key, st.Identity, st.Formats, fmt, bin) Then
                Dim loaded = GL.CreateProgram()
                Dim pin = GCHandle.Alloc(bin, GCHandleType.Pinned)
                Try
                    GL.ProgramBinary(loaded, CType(fmt, BinaryFormat), pin.AddrOfPinnedObject(), bin.Length)
                Finally
                    pin.Free()
                End Try
                Dim ok As Integer
                GL.GetProgram(loaded, GetProgramParameterName.LinkStatus, ok)
#If DEBUG Then
                ' GATE (ShadowGate --program-cache, subject mutant): the binary's LINK_STATUS not checked.
                If Fo4GBufferConstants.GateMutantOn("program-cache-skip-link-check") Then ok = 1
#End If
                If ok <> 0 Then
                    ProgramBinaryCache.Accepted()
#If DEBUG Then
                    CountBuild(loaded, reloj.Elapsed.TotalMilliseconds, True)
#End If
                    Return loaded
                End If
                GL.DeleteProgram(loaded)
                ProgramBinaryCache.Refused(key, st.Identity)
            End If
        End If
        Dim program = CompileAndLink(st, stages, hashes, varyings, useCache)
        If useCache Then
            Dim length As Integer
            GL.GetProgram(program, GetProgramParameterName.ProgramBinaryLength, length)
            If length > 0 Then
                Dim bin(length - 1) As Byte
                Dim written As Integer, fmt As BinaryFormat
                GL.GetProgramBinary(program, length, written, fmt, bin)
                If written = length Then ProgramBinaryCache.Store(key, st.Identity, CInt(fmt), bin)
            End If
        End If
#If DEBUG Then
        CountBuild(program, reloj.Elapsed.TotalMilliseconds, False)
#End If
        Return program
    End Function

    ''' <summary>Compiles <paramref name="source"/> as a stage of type <paramref name="type"/> in the current context (the context's
    ''' object for that text) and returns its compile log when it does not compile, Nothing when it does (ShadowGate's per-stage
    ''' census of the transcribed records: which stage of a program fails).</summary>
    Friend Shared Function StageError(type As ShaderType, source As String) As String
        Dim st = Current()
        Gl43.CheckShaderText(source)
        Dim s = StageOf(st, type, source, SHA256.HashData(Encoding.UTF8.GetBytes(source)))
        AskStatus(s)
        Return If(s.Compiled.Value, Nothing, s.Log)
    End Function

    ''' <summary>The cache key of a program: SHA-256 of FormatVersion, the cache identity (the driver's four strings and the build of the
    ''' app), each stage's type and text hash in order, the varyings in order and their mode (ProgramBinaryCache).</summary>
    Private Shared Function ProgramKey(id As ProgramBinaryCache.CacheIdentity, stages As IReadOnlyList(Of (Type As ShaderType, Source As String)),
                                       hashes As Byte()(), varyings As String()) As Byte()
        Dim version = If(id.Version, "")
        Dim build = If(id.AppBuild, "")
#If DEBUG Then
        ' GATE (ShadowGate --program-cache, subject mutants): the driver's version, the build of the app out of the key (and of the
        ' identity check).
        If Fo4GBufferConstants.GateMutantOn("program-cache-key-no-version") Then version = ""
        If Fo4GBufferConstants.GateMutantOn("program-cache-key-no-appbuild") Then build = ""
#End If
        Using ms As New MemoryStream()
            Using w As New BinaryWriter(ms, Encoding.UTF8, leaveOpen:=True)
                w.Write(ProgramBinaryCache.FormatVersion)
                w.Write(If(id.Vendor, ""))
                w.Write(If(id.Renderer, ""))
                w.Write(version)
                w.Write(If(id.GlslVersion, ""))
                w.Write(build)
                w.Write(stages.Count)
                For i = 0 To stages.Count - 1
                    w.Write(CInt(stages(i).Type))
                    w.Write(hashes(i))
                Next
                If varyings Is Nothing Then
                    w.Write(-1)
                Else
                    w.Write(varyings.Length)
                    For Each v In varyings : w.Write(v) : Next
                    w.Write(CInt(TransformFeedbackMode.InterleavedAttribs))
                End If
            End Using
            Return SHA256.HashData(ms.ToArray())
        End Using
    End Function

    ''' <summary>The context's shader object of a stage text: created and compiled on its first use (its status not asked).</summary>
    Private Shared Function StageOf(st As ContextState, type As ShaderType, source As String, hash As Byte()) As StageObject
        Dim k = CInt(type).ToString(Globalization.CultureInfo.InvariantCulture) & ":" & Convert.ToHexString(hash)
        Dim s As StageObject = Nothing
        If st.Stages.TryGetValue(k, s) Then Return s
        s = New StageObject With {.Id = GL.CreateShader(type), .Type = type}
        Dim text = source
#If DEBUG Then
        Threading.Interlocked.Increment(GateShaderCompiles)
        If type = ShaderType.FragmentShader AndAlso GateFailStagePending() Then
            GateFailStageFired = True
            ' after the #version line (GLSL 4.30 3.3: #version must come before anything but comments and white space)
            Dim eol = source.IndexOf(ChrW(10), Math.Max(0, source.IndexOf("#version", StringComparison.Ordinal)))
            text = Gl43.GateHarnessText("rev-10 stage made to fail once", source.Insert(eol + 1, "#error stage made to fail once (FO4_DEBUG_FAIL_STAGE_ONCE / gate --program-abort)" & ChrW(10)))
        End If
#End If
        Gl43.ShaderSource(s.Id, text)
        GL.CompileShader(s.Id)
        st.Stages(k) = s
        Return s
    End Function

    ''' <summary>The stage's compile status and, when it failed, its log (asked once).</summary>
    Private Shared Sub AskStatus(s As StageObject)
        If s.Compiled.HasValue Then Return
        Dim ok As Integer
        GL.GetShader(s.Id, ShaderParameter.CompileStatus, ok)
        s.Compiled = ok <> 0
        If ok = 0 Then s.Log = GL.GetShaderInfoLog(s.Id)
    End Sub

    ''' <summary>The program compiled from its texts: every new stage compiled, a stage already known not to compile throws before the
    ''' link; then the link; then the statuses (the first stage in order that did not compile, else the link).</summary>
    Private Shared Function CompileAndLink(st As ContextState, stages As IReadOnlyList(Of (Type As ShaderType, Source As String)),
                                           hashes As Byte()(), varyings As String(), retrievable As Boolean) As Integer
        Dim objs As New List(Of StageObject)(stages.Count)
        For i = 0 To stages.Count - 1
            objs.Add(StageOf(st, stages(i).Type, stages(i).Source, hashes(i)))
        Next
        Dim known = objs.FirstOrDefault(Function(o) o.Compiled.HasValue AndAlso Not o.Compiled.Value)
        If known IsNot Nothing Then Throw New ProgramBuildException($"Error compiling {known.Type}: {known.Log}")
        Dim program = GL.CreateProgram()
        If retrievable Then GL.ProgramParameter(program, ProgramParameterName.ProgramBinaryRetrievableHint, 1)
        For Each o In objs : GL.AttachShader(program, o.Id) : Next
        If varyings IsNot Nothing Then GL.TransformFeedbackVaryings(program, varyings.Length, varyings, TransformFeedbackMode.InterleavedAttribs)
        GL.LinkProgram(program)
        For Each o In objs : AskStatus(o) : Next
        Dim failed = objs.FirstOrDefault(Function(o) Not o.Compiled.Value)
        Dim linkOk As Integer
        If failed Is Nothing Then GL.GetProgram(program, GetProgramParameterName.LinkStatus, linkOk)
        Dim linkLog = If(failed Is Nothing AndAlso linkOk = 0, GL.GetProgramInfoLog(program), Nothing)
        For Each o In objs : GL.DetachShader(program, o.Id) : Next
        If failed IsNot Nothing Then
            GL.DeleteProgram(program)
            Throw New ProgramBuildException($"Error compiling {failed.Type}: {failed.Log}")
        End If
        If linkOk = 0 Then
            GL.DeleteProgram(program)
            Throw New ProgramBuildException($"Shader program link error: {linkLog}")
        End If
        Return program
    End Function

#If DEBUG Then
    Private Shared Sub CountBuild(program As Integer, ms As Double, hit As Boolean)
        Threading.Interlocked.Increment(GateBuilds)
        SyncLock GateBuildLog
            GateBuildLog.Add((program, ms, hit))
        End SyncLock
    End Sub
#End If

End Class
