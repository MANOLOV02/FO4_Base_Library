Option Strict On
Option Explicit On
Imports System.Runtime.InteropServices
Imports System.Threading
Imports OpenTK.Graphics.OpenGL4
Imports OpenTK.Windowing.Desktop

''' <summary>Where a program request is (F9 r3 rev-18: the state of the PROGRAM, apart from the admission of its draw).</summary>
Friend Enum ProgramState
    ''' <summary>Not asked for yet.</summary>
    NotBuilt
    ''' <summary>Asked for; the builder thread has not finished it.</summary>
    Pending
    ''' <summary>Linked (and, once wrapped on the UI thread, usable).</summary>
    Built
    ''' <summary>Its build failed (the failure kept with the request).</summary>
    Failed
End Enum

''' <summary>F9 (r1 §1.3, r2 rev-01..04): a program a FRAME asked for is still being built by the control's builder thread. The frame is
''' not presented: DrawSceneFrame catches it and shows "Compiling shaders n/m (once per build)" (PreviewControl.PendingFrame), never a partial frame and
''' never a frozen window. Only a slot inside a frame throws it; outside a frame every access waits (ProgramBuilder.Await).</summary>
Friend NotInheritable Class ProgramPendingException
    Inherits Exception
    Friend Sub New(name As String)
        MyBase.New($"{name}: still compiling")
    End Sub
End Class

''' <summary>ONE REQUEST TO THE BUILDER (F9 r2 rev-04 / r3 rev-12): one program, or an atomic unit of programs that are published
''' together (a G-buffer record and its variant without early tests: Built only when all are, Failed if any fails - the others deleted).
''' The builder thread writes its state (Pending -&gt; Built(ids, fence) | Failed(cause)) and sets Done; the UI thread wraps it once
''' (ProgramBuilder.Wrap: glWaitSync on the fence, then each program re-bound with glUseProgram, OpenGL 4.6 Appendix D.3).</summary>
Friend NotInheritable Class ProgramRequest
    Friend ReadOnly Names As String()
    Friend ReadOnly Specs As ProgramSpec()
    Private _state As Integer = ProgramState.Pending
    Friend Ids As Integer()
    Friend Sync As IntPtr
    Friend Failure As Exception
    ''' <summary>Set by the UI thread when it wrapped the programs (from then on they are the consumer's: the builder never deletes them).</summary>
    Friend Wrapped As Boolean
    ''' <summary>Its consumer let it go before it finished (LazyProgram.Release, a record's retry): the worker deletes its programs.</summary>
    Friend Abandoned As Boolean
    Friend ReadOnly Done As New ManualResetEventSlim(False)

    Friend Sub New(names As String(), specs As ProgramSpec())
        Me.Names = names
        Me.Specs = specs
    End Sub

    Friend ReadOnly Property State As ProgramState
        Get
            Return CType(Volatile.Read(_state), ProgramState)
        End Get
    End Property

    ''' <summary>The name the request is told by (its first program's).</summary>
    Friend ReadOnly Property Name As String
        Get
            Return Names(0)
        End Get
    End Property

    ''' <summary>The builder thread publishes the linked programs and their fence.</summary>
    Friend Sub PublishBuilt(ids As Integer(), sync As IntPtr)
        Me.Ids = ids
        Me.Sync = sync
        Volatile.Write(_state, ProgramState.Built)
        Done.Set()
    End Sub

    ''' <summary>The request failed (its build, the builder's death, the control's close).</summary>
    Friend Sub PublishFailed(cause As Exception)
        If State <> ProgramState.Pending Then Return
        Failure = cause
        Volatile.Write(_state, ProgramState.Failed)
        Done.Set()
    End Sub

    ''' <summary>The failure's message as the slots and records keep it.</summary>
    Friend ReadOnly Property FailureText As String
        Get
            Return If(Failure Is Nothing, "build failed", Failure.Message)
        End Get
    End Property
End Class

''' <summary>THE CONTROL'S PROGRAM BUILDER (F9 r1 §1.1-1.2, r2 rev-04/05/09/10, r3 rev-14, r4 rev-14/19): every program a PreviewControl's
''' slots and records ask for is built OUTSIDE the UI thread, by one builder thread per control on a hidden context that shares the
''' control's objects (OpenGL 4.6 §5.1 / Appendix D: programs and shaders are shared; the hidden context is an invisible OpenTK
''' NativeWindow made from the control's own GLControlSettings, PreviewControl.HiddenWindowSettings). ProgramCompiler builds there with
''' its cache, unchanged in law; the UI wraps each finished program (Wrap).
''' <para>LAZY (r2 rev-09): this object is the control's from its construction and every program container receives it (r4 rev-19: a
''' mandatory parameter), but the hidden window and the thread (the WORKER) are made only by the first request, on the main thread (GLFW
''' creates windows there). A control that never asks for a program - the bake's - never makes one. A worker that died is replaced by
''' the next request after a gesture's retry (ResetAfterDeath).</para>
''' <para>IN A FRAME / OUTSIDE (r2 "Un solo primitivo"): the control's InFrame (DrawSceneFrame's body) decides what an access does with a
''' pending program - in a frame, a slot throws ProgramPendingException and a record returns Nothing marking the frame pending; outside
''' a frame, it waits (Await, never pumping the message loop). The UI never waits in production (r3 rev-03, the Debug counter
''' GateUiAwaits).</para>
''' <para>CLOSE (r3 rev-04, r4 rev-14): Close (the control's Clean) marks the close and empties the queue, without waiting; the worker
''' ends the program in hand, deletes what nobody wrapped and its stage objects, lets go of its context and says Done; HiddenContexts
''' disposes its window on the main thread.</para></summary>
Friend NotInheritable Class ProgramBuilder

    Private ReadOnly _control As PreviewControl
    Private ReadOnly _lock As New Object()
    Private _worker As Worker
    ''' <summary>The wave the card counts (r1 §1.3): requested since it started, finished since it started; it ends at the first complete
    ''' frame presented (EndWave).</summary>
    Private _waveRequested, _waveDone As Integer
    ''' <summary>Bumped by every request the worker finishes (the control compares it around a pending frame: a finish the card's
    ''' UpdateRequired = False could hide).</summary>
    Private _completions As Integer

    Friend Sub New(control As PreviewControl)
        _control = control
    End Sub

    ''' <summary>The control this builder serves.</summary>
    Friend ReadOnly Property Control As PreviewControl
        Get
            Return _control
        End Get
    End Property

    ''' <summary>The control is drawing a frame (DrawSceneFrame's body): a pending program does not wait there.</summary>
    Friend ReadOnly Property InFrame As Boolean
        Get
            Return _control.InFrame
        End Get
    End Property

    ''' <summary>Requests the worker finished (see _completions).</summary>
    Friend ReadOnly Property Completions As Integer
        Get
            Return Volatile.Read(_completions)
        End Get
    End Property

    ''' <summary>The card's n/m: finished and requested in this wave.</summary>
    Friend ReadOnly Property Wave As (Done As Integer, Requested As Integer)
        Get
            SyncLock _lock
                Return (_waveDone, _waveRequested)
            End SyncLock
        End Get
    End Property

    ''' <summary>A complete frame was presented: the wave ends (the next request starts another).</summary>
    Friend Sub EndWave()
        SyncLock _lock
            If _worker Is Nothing OrElse _worker.PendingCount = 0 Then _waveDone = 0 : _waveRequested = 0
        End SyncLock
    End Sub

    ''' <summary>Some request is queued or being built.</summary>
    Friend ReadOnly Property HasPending As Boolean
        Get
            SyncLock _lock
                Return _worker IsNot Nothing AndAlso _worker.PendingCount > 0
            End SyncLock
        End Get
    End Property

    ''' <summary>The worker exists (Debug gates: 0 in the bake; ShadowGate's lazy-builder checks).</summary>
    Friend ReadOnly Property Started As Boolean
        Get
            SyncLock _lock
                Return _worker IsNot Nothing
            End SyncLock
        End Get
    End Property

    ''' <summary>ASKS FOR the programs of <paramref name="specs"/> (named <paramref name="names"/>, one each), as one unit. Makes the worker on
    ''' the first request (main thread only); during the control's teardown, after its close or with a dead worker, the request comes back
    ''' Failed (r3 rev-13: nothing is queued nor made in a teardown).</summary>
    Friend Function Request(names As String(), specs As ProgramSpec()) As ProgramRequest
        Dim req As New ProgramRequest(names, specs)
        Dim cerrando = _control.TearingDown
#If DEBUG Then
        ' GATE (subject mutant card-enqueues-in-teardown): a request taken in a teardown.
        If Fo4GBufferConstants.GateMutantOn("card-enqueues-in-teardown") Then cerrando = False
#End If
        If cerrando Then
            req.PublishFailed(New InvalidOperationException("the preview is closing"))
            Return req
        End If
        Dim w As Worker
        SyncLock _lock
            w = _worker
        End SyncLock
        If w Is Nothing Then w = StartWorker()
        If Not w.Enqueue(req) Then Return req
#If DEBUG Then
        If _control.TearingDown Then Interlocked.Increment(GateRequestsTakenInTeardown)
#End If
        SyncLock _lock
            _waveRequested += 1
        End SyncLock
        Return req
    End Function

    ''' <summary>Makes the worker (r2 rev-10, r3 rev-14): on the main thread only (GLFW); the hidden window through HiddenContexts, the
    ''' control's context made current again before returning (Debug: checked, GateBuilderLeftHiddenCurrent).</summary>
    Friend Function StartWorker() As Worker
        If Not GLFWProvider.IsOnMainThread Then
            Throw New InvalidOperationException("ProgramBuilder: the builder is made on the main thread (GLFW); a first request from another thread is a programming error")
        End If
        Dim w As New Worker(Me)
        SyncLock _lock
            If _worker IsNot Nothing Then Return _worker
            _worker = w
        End SyncLock
        w.Start()
        Return w
    End Function

    ''' <summary>A gesture's retry (PreviewControl.ForgetProgramFailures): a dead worker is let go (its window goes to HiddenContexts'
    ''' disposal), the next request makes a new one; a live worker forgets its failed stages (ProgramCompiler.ForgetFailedStages, on its
    ''' own context, before its next build).</summary>
    Friend Sub ForgetFailures()
        SyncLock _lock
            If _worker Is Nothing Then Return
            If _worker.Dead IsNot Nothing Then
                _worker = Nothing
            Else
                _worker.ForgetFailedStagesNext()
            End If
        End SyncLock
    End Sub

#If DEBUG Then
    ''' <summary>GATE ONLY (ShadowGate --program-abort, the cache warm): the worker's stage objects deleted before its next request
    ''' (ProgramCompiler.ReleaseStages frees the current context's: the worker's context is its own).</summary>
    Friend Sub GateReleaseWorkerStages()
        SyncLock _lock
            _worker?.GateReleaseStagesNext()
        End SyncLock
    End Sub
#End If

    ''' <summary>WAITS for <paramref name="req"/> (r2 "Un solo primitivo", pump False: no message is dispatched while it waits): until it
    ''' is Built or Failed, or the worker ends. The one wait of the builder; counted on the UI thread (GateUiAwaits, r6 rev-03 (2)).</summary>
    Friend Sub Await(req As ProgramRequest)
        If req.State <> ProgramState.Pending Then Return
#If DEBUG Then
        ' GATE (subject mutant await-returns-pending): an access outside a frame gets the pending state back (GateOutsideFramePending).
        If Fo4GBufferConstants.GateMutantOn("await-returns-pending") Then Return
        If Not _control.InvokeRequired Then NoteUiAwait()
        ' r3 rev-13: the card never waits (subject mutants card-awaits-background / card-background-pending-throws)
        If _control.GateInCard Then Interlocked.Increment(GateCardAwaits)
#End If
        Dim w As Worker
        SyncLock _lock
            w = _worker
        End SyncLock
        If w Is Nothing Then
            req.PublishFailed(New InvalidOperationException("no builder"))
            Return
        End If
        WaitHandle.WaitAny({req.Done.WaitHandle, w.Finished})
        If req.State = ProgramState.Pending Then req.PublishFailed(If(w.Dead, New InvalidOperationException("the program builder ended")))
    End Sub

    ''' <summary>Waits for every request queued or in hand (CaptureBitmap's frame, the gates).</summary>
    Friend Sub AwaitAll()
        Dim w As Worker
        SyncLock _lock
            w = _worker
        End SyncLock
        If w Is Nothing Then Return
        For Each r In w.Snapshot()
            Await(r)
        Next
    End Sub

    ''' <summary>WRAPS <paramref name="req"/> on the UI thread, once (r2 rev-10, Appendix D.3): waits on its fence in the GL server
    ''' (glWaitSync), deletes the fence, re-binds each program (glUseProgram) so this context sees it complete, and gives the ids to the
    ''' consumer (from then on the consumer deletes them). The control's context is current.</summary>
    Friend Function Wrap(req As ProgramRequest) As Integer()
        SyncLock req
            If req.Wrapped Then Return req.Ids
            req.Wrapped = True
        End SyncLock
        _control.EnsureContextCurrent()
        If req.Sync <> IntPtr.Zero Then
            GL.WaitSync(req.Sync, WaitSyncFlags.None, ULong.MaxValue)   ' GL_TIMEOUT_IGNORED (0xFFFFFFFFFFFFFFFF), the one value glWaitSync takes
            GL.DeleteSync(req.Sync)
            req.Sync = IntPtr.Zero
        End If
        Dim rebind = True
#If DEBUG Then
        ' GATE (subject mutant wrap-without-rebind): the programs used without the re-bind D.3 asks for.
        If Fo4GBufferConstants.GateMutantOn("wrap-without-rebind") Then rebind = False
#End If
        If rebind Then Gl43.MakeLinkedProgramsCurrent(req.Ids)
#If DEBUG Then
        GateProgramsWrapped += req.Ids.Length
#End If
        Return req.Ids
    End Function

    ''' <summary>The consumer lets <paramref name="req"/> go (LazyProgram.Release, a record forgotten): its programs, when built and not
    ''' wrapped, are deleted now (on the control's context, the share group's); while it is being built, by the worker when it finishes.</summary>
    Friend Sub Abandon(req As ProgramRequest)
        SyncLock req
            If req.Wrapped Then Return
            If req.State <> ProgramState.Built Then
                req.Abandoned = True
                Return
            End If
            req.Wrapped = True
        End SyncLock
        If Not _control.EnsureContextCurrent() Then Return   ' no context left: the programs go with the share group
        For Each id In req.Ids : GL.DeleteProgram(id) : Next
        If req.Sync <> IntPtr.Zero Then
            GL.DeleteSync(req.Sync)
            req.Sync = IntPtr.Zero
        End If
    End Sub

    ''' <summary>The control's Clean (r3 rev-04): the close marked and the queue emptied (its requests Failed "closed"); NOTHING is waited
    ''' for (Debug: GateCloseWaits stays 0). The worker ends in its own time; HiddenContexts disposes its window.</summary>
    ''' <summary>The worker has a request in hand: the driver may be linking it, and destroying this control's window would wait for the link
    ''' (HiddenContexts.DisposeParked).</summary>
    Friend ReadOnly Property RequestInHand As Boolean
        Get
            Dim w As Worker
            SyncLock _lock
                w = _worker
            End SyncLock
            Return w IsNot Nothing AndAlso w.RequestInHand
        End Get
    End Property

    Friend Sub Close()
        Dim w As Worker
        SyncLock _lock
            w = _worker
        End SyncLock
        w?.Close()
#If DEBUG Then
        ' GATE (subject mutant close-waits): the close waits for the program in hand (GateCloseWaits > 0).
        If w IsNot Nothing AndAlso Fo4GBufferConstants.GateMutantOn("close-waits") Then
            Interlocked.Increment(GateCloseWaits)
            w.WaitFinished()
        End If
#End If
    End Sub

    ''' <summary>The worker finished a request (its thread): the wave counts it and the control is told to draw again - through its
    ''' volatile UpdateRequired, read by its RenderTimer (r2 rev-04 2: no BeginInvoke, which a destroyed handle would refuse).</summary>
    Private Sub Finished(req As ProgramRequest)
        Interlocked.Increment(_completions)
        SyncLock _lock
            _waveDone += 1
        End SyncLock
        NotifyControl()
    End Sub

    ''' <summary>The control told to draw again, from the worker's thread: its volatile UpdateRequired (read by its RenderTimer) - valid also
    ''' after the control was released (r2 rev-04 2).</summary>
    Private Sub NotifyControl()
#If DEBUG Then
        ' GATE (subject mutant notify-by-begininvoke): the notice by BeginInvoke - refused once the control's handle is gone.
        If Fo4GBufferConstants.GateMutantOn("notify-by-begininvoke") Then
            _control.BeginInvoke(Sub() _control.UpdateRequired = True)
            Return
        End If
#End If
        _control.UpdateRequired = True
    End Sub

#If DEBUG Then
    ''' <summary>GATE ONLY (ShadowGate --program-builder): the worker's notice given from another thread now (to a released control: it
    ''' must not fail); the exception it threw, or Nothing.</summary>
    Friend Function GateNotifyFromThread() As Exception
        Dim err As Exception = Nothing
        Dim t As New Thread(Sub()
                                Try
                                    NotifyControl()
                                Catch ex As Exception
                                    err = ex
                                End Try
                            End Sub)
        t.Start()
        t.Join()
        Return err
    End Function
#End If

#If DEBUG Then
    ''' <summary>DEBUG ONLY (r3 rev-03, r6 rev-03 (2)): every Await on the UI thread, with the set of assemblies on its stack outside the
    ''' library and the framework (System.*, Microsoft.*, OpenTK.*, mscorlib, netstandard): ParityGate ui-awaits-production partitions them by
    ''' the tree (a Tools project's assembly = Tools); one of production counts.</summary>
    Friend Shared ReadOnly GateUiAwaits As New Concurrent.ConcurrentQueue(Of String())

    Private Shared Sub NoteUiAwait()
        Dim lib0 = GetType(ProgramBuilder).Assembly
        Dim asms As New SortedSet(Of String)(StringComparer.Ordinal)
        For Each f In New Diagnostics.StackTrace(False).GetFrames()
            Dim a = f.GetMethod()?.DeclaringType?.Assembly
            If a Is Nothing OrElse a Is lib0 Then Continue For
            Dim n = a.GetName().Name
            If n.StartsWith("System", StringComparison.Ordinal) OrElse n.StartsWith("Microsoft.", StringComparison.Ordinal) OrElse
               n.StartsWith("OpenTK", StringComparison.Ordinal) OrElse n = "mscorlib" OrElse n = "netstandard" Then Continue For
            asms.Add(n)
        Next
        GateUiAwaits.Enqueue(asms.ToArray())
    End Sub

    ''' <summary>DEBUG ONLY: the programs the builder built that nobody wrapped yet (the requests asked together that the frame did not
    ''' use: r2 "Pedidos juntos"): ShadowGate --program-cache's "builds = programs created" counts them.</summary>
    Friend Function GateBuiltNotWrapped() As Integer
        Dim w As Worker
        SyncLock _lock
            w = _worker
        End SyncLock
        Return If(w Is Nothing, 0, w.GateBuiltNotWrapped())
    End Function

    ''' <summary>DEBUG ONLY (r2 rev-10): the programs Wrap handed to their consumers; each one re-bound once (Gl43.GateLinkedProgramsBound).
    ''' UI thread only.</summary>
    Friend Shared GateProgramsWrapped As Integer
    ''' <summary>DEBUG ONLY (ShadowGate --program-builder, E29): the workers do not take what is queued: a request queued with none in hand, deterministically.</summary>
    Friend Shared GateHoldQueue As Boolean
    ''' <summary>DEBUG ONLY (r3 rev-13): waits made while a status card was being drawn (0: the card never waits).</summary>
    Friend Shared GateCardAwaits As Integer
    ''' <summary>DEBUG ONLY (r3 rev-13): requests queued while their control was in its teardown (0: nothing is asked then).</summary>
    Friend Shared GateRequestsTakenInTeardown As Integer
    ''' <summary>DEBUG ONLY (r3 rev-04): the waits Close made (0: the close never waits).</summary>
    Friend Shared GateCloseWaits As Integer
    ''' <summary>DEBUG ONLY (r2 rev-04 4): workers whose thread died (0 in a run without the builder-dies mutant; notify-by-begininvoke).</summary>
    Friend Shared GateWorkersDied As Integer

    ''' <summary>DEBUG ONLY: workers made in this process (r2 rev-09: 0 in the bake; mutant builder-eager).</summary>
    Friend Shared GateWorkersMade As Integer
    ''' <summary>DEBUG ONLY (r2 rev-10): a worker's creation left the hidden context current on the UI thread (builder-leaves-hidden-current).</summary>
    Friend Shared GateBuilderLeftHiddenCurrent As Integer
    ''' <summary>DEBUG ONLY (r3 rev-14 a): a worker's thread ended with a context still current on it (builder-dies-current).</summary>
    Friend Shared GateWorkerEndedCurrent As Integer
    ''' <summary>DEBUG ONLY (r3 rev-16): a worker whose hidden context's identity was not its control's (logged; the battery expects 0).</summary>
    Friend Shared GateIdentityDiffers As Integer
#End If

    ''' <summary>THE WORKER: the hidden window (HiddenContexts owns it), the thread, the queue.</summary>
    Friend NotInheritable Class Worker
        Private ReadOnly _owner As ProgramBuilder
        Private ReadOnly _lock As New Object()
        Private ReadOnly _queue As New Queue(Of ProgramRequest)
        Private ReadOnly _published As New List(Of ProgramRequest)
        Private ReadOnly _wake As New AutoResetEvent(False)
        ''' <summary>The request being built; Nothing between two.</summary>
        Private _enMano As ProgramRequest
        Private _closing As Boolean
        Private _forgetStages As Boolean
        Private _thread As Thread
        Private _window As NativeWindow
        Private _controlIdentity As ProgramBinaryCache.CacheIdentity
        ''' <summary>Set when the thread ended (Done or Dead): HiddenContexts disposes the window then.</summary>
        Friend ReadOnly Finished As New ManualResetEvent(False)
        ''' <summary>Why the thread died (Nothing while alive or after a normal end).</summary>
        Friend Dead As Exception

        Friend Sub New(owner As ProgramBuilder)
            _owner = owner
        End Sub

        Friend ReadOnly Property Owner As ProgramBuilder
            Get
                Return _owner
            End Get
        End Property

        Friend ReadOnly Property Closing As Boolean
            Get
                SyncLock _lock
                    Return _closing
                End SyncLock
            End Get
        End Property

        ''' <summary>Requests queued or in hand.</summary>
        Friend ReadOnly Property PendingCount As Integer
            Get
                SyncLock _lock
                    Return _queue.Count + If(_enMano Is Nothing, 0, 1)
                End SyncLock
            End Get
        End Property

        ''' <summary>The requests queued or in hand, now.</summary>
        Friend Function Snapshot() As List(Of ProgramRequest)
            SyncLock _lock
                Dim r = _queue.ToList()
                If _enMano IsNot Nothing Then r.Add(_enMano)
                Return r
            End SyncLock
        End Function

        ''' <summary>The hidden window and the thread (main thread). The control's identity is read here, its context current.</summary>
        Friend Sub Start()
            Dim pc = _owner.Control
            pc.EnsureContextCurrent()
            _controlIdentity = ProgramCompiler.ReadIdentity()
            _window = HiddenContexts.Create(pc.HiddenWindowSettings(), Me)
            ' The constructor left the hidden context current on this thread (NativeWindow.cs: MakeCurrent when the API is OpenGL): let it
            ' go and give the control its own back (r2 rev-10 secuencia).
            _window.Context.MakeNoneCurrent()
            Dim restore = True
#If DEBUG Then
            ' GATE (subject mutant builder-leaves-hidden-current): the control's context not made current again.
            If Fo4GBufferConstants.GateMutantOn("builder-leaves-hidden-current") Then restore = False
#End If
            If restore Then pc.EnsureContextCurrent()
#If DEBUG Then
            ' Gate builder-context-restored: the UI thread's current context is the control's after the creation.
            If Gl43.CurrentContext() <> pc.GateGl43Context Then Interlocked.Increment(GateBuilderLeftHiddenCurrent)
            Interlocked.Increment(GateWorkersMade)
#End If
            _thread = New Thread(AddressOf Lazo) With {.IsBackground = True, .Name = "PreviewControl program builder"}
            _thread.Start()
        End Sub

        ''' <summary>Queues <paramref name="req"/>; False (the request Failed) when the worker closed or died.</summary>
        Friend Function Enqueue(req As ProgramRequest) As Boolean
            SyncLock _lock
                If _closing Then
                    req.PublishFailed(New InvalidOperationException("the preview is closing"))
                    Return False
                End If
                If Dead IsNot Nothing Then
                    req.PublishFailed(Dead)
                    Return False
                End If
                _queue.Enqueue(req)
            End SyncLock
            _wake.Set()
            Return True
        End Function

        Friend Sub ForgetFailedStagesNext()
            SyncLock _lock
                _forgetStages = True
            End SyncLock
        End Sub

#If DEBUG Then
        Private _gateReleaseStages As Boolean
        ''' <summary>GATE ONLY: see ProgramBuilder.GateReleaseWorkerStages.</summary>
        Friend Sub GateReleaseStagesNext()
            SyncLock _lock
                _gateReleaseStages = True
            End SyncLock
        End Sub
#End If

        ''' <summary>See ProgramBuilder.Close.</summary>
        Friend Sub Close()
            Dim vaciados As List(Of ProgramRequest)
            SyncLock _lock
                _closing = True
                vaciados = _queue.ToList()
                _queue.Clear()
            End SyncLock
            For Each r In vaciados
                r.PublishFailed(New InvalidOperationException("the preview closed"))
            Next
            _wake.Set()
        End Sub

        ''' <summary>The thread: its context current, ProgramCompiler's state for it, then one request at a time until the close. Finally
        ''' (r3 rev-14 a), whatever the end: on a close, the programs nobody wrapped deleted; the stage objects deleted; the context let go;
        ''' Finished set.</summary>
        Private Sub Lazo()
            Dim ctx = IntPtr.Zero
            Try
                _window.Context.MakeCurrent()
                ctx = Gl43.CurrentContext()
                ProgramCompiler.InitContext(ctx)
                Dim id = ProgramCompiler.ReadIdentity()
                If Not ProgramBinaryCache.IdentityMatches(id, _controlIdentity) Then
                    ' r2 rev-05: only logged - what this context builds is kept and looked up under ITS identity.
                    Logger.LogLazy(Function() $"[PROGRAM-BUILDER] the hidden context's identity ({id.Renderer} / {id.Version} / flags 0x{id.ContextFlags:X} / profile 0x{id.ProfileMask:X}) is not its control's ({_controlIdentity.Renderer} / {_controlIdentity.Version} / flags 0x{_controlIdentity.ContextFlags:X} / profile 0x{_controlIdentity.ProfileMask:X})")
#If DEBUG Then
                    Interlocked.Increment(GateIdentityDiffers)
#End If
                End If
                Do
                    Dim req As ProgramRequest = Nothing
                    Dim olvidar = False
                    SyncLock _lock
                        If _closing Then Exit Do
                        olvidar = _forgetStages : _forgetStages = False
                        Dim tomar = _queue.Count > 0
#If DEBUG Then
                        If GateHoldQueue Then tomar = False
#End If
                        If tomar Then req = _queue.Dequeue()
                        _enMano = req
                    End SyncLock
                    If olvidar Then ProgramCompiler.ForgetFailedStages()
#If DEBUG Then
                    Dim liberar As Boolean
                    SyncLock _lock
                        liberar = _gateReleaseStages : _gateReleaseStages = False
                    End SyncLock
                    If liberar Then ProgramCompiler.ReleaseStages()
#End If
                    If req Is Nothing Then
                        _wake.WaitOne()
                        Continue Do
                    End If
#If DEBUG Then
                    ' GATE (subject mutant builder-dies): the thread dies with a request in hand (a lost context, an out-of-memory).
                    If Fo4GBufferConstants.GateMutantOn("builder-dies") Then Throw New InvalidOperationException("builder-dies (gate mutant)")
#End If
                    Construir(req)
                Loop
            Catch ex As Exception
                Dead = ex
#If DEBUG Then
                Interlocked.Increment(GateWorkersDied)
#End If
                Dim m = ex.ToString()
                Logger.LogLazy(Function() "[PROGRAM-BUILDER] the builder thread died: " & m)
            Finally
                Dim fallidos As List(Of ProgramRequest)
                Dim cerrando As Boolean
                SyncLock _lock
                    fallidos = _queue.ToList()
                    If _enMano IsNot Nothing Then fallidos.Add(_enMano)
                    _queue.Clear()
                    _enMano = Nothing
                    cerrando = _closing
                End SyncLock
                For Each r In fallidos
                    r.PublishFailed(If(Dead, New InvalidOperationException("the preview closed")))
                Next
                If ctx <> IntPtr.Zero Then
                    Try
                        If cerrando Then
                            ' the programs nobody wrapped are this thread's to delete (the consumer is gone)
                            For Each r In _published
                                SyncLock r
                                    If r.Wrapped OrElse r.State <> ProgramState.Built Then Continue For
                                    r.Wrapped = True
                                End SyncLock
                                For Each pid In r.Ids : GL.DeleteProgram(pid) : Next
                                If r.Sync <> IntPtr.Zero Then GL.DeleteSync(r.Sync) : r.Sync = IntPtr.Zero
                            Next
                        End If
                        ProgramCompiler.ReleaseContext(ctx, deleteObjects:=True)
                    Catch ex As Exception
                        Dim m = ex.Message
                        Logger.LogLazy(Function() "[PROGRAM-BUILDER] releasing the hidden context's objects: " & m)
                    End Try
                End If
                Dim soltar = True
#If DEBUG Then
                ' GATE (subject mutant builder-dies-current): the Finally without MakeNoneCurrent.
                If Fo4GBufferConstants.GateMutantOn("builder-dies-current") Then soltar = False
#End If
                Try
                    If soltar Then _window.Context.MakeNoneCurrent()
                Catch
                End Try
#If DEBUG Then
                If Gl43.CurrentContext() <> IntPtr.Zero Then Interlocked.Increment(GateWorkerEndedCurrent)
#End If
                Finished.Set()
                If Dead IsNot Nothing Then _owner.Control.UpdateRequired = True   ' the card with Retry
            End Try
        End Sub

        ''' <summary>A specification's build may fail as a build (its texts, its link, a Debug text or hint check): that request fails, the
        ''' next one goes on. Anything else kills the thread (Lazo's Catch).</summary>
        Private Shared Function FallaDeBuild(ex As Exception) As Boolean
            Return TypeOf ex Is ProgramBuildException OrElse TypeOf ex Is InvalidOperationException OrElse TypeOf ex Is ArgumentException
        End Function

        ''' <summary>The context's derivative hint set to <paramref name="spec"/>'s before its program is compiled and linked (r3 rev-06; checked
        ''' by ProgramCompiler.Build in Debug).</summary>
        Private Shared Sub FijarHint(spec As ProgramSpec)
            Dim fijar = True
#If DEBUG Then
            ' GATE (subject mutant builder-ignores-hint): the hint of the spec not set (the Debug check of ProgramCompiler falls).
            If Fo4GBufferConstants.GateMutantOn("builder-ignores-hint") Then fijar = False
#End If
            If fijar Then GL.Hint(HintTarget.FragmentShaderDerivativeHint, spec.DerivativeHint)
        End Sub

        ''' <summary>A request done: out of the worker's hands, kept while a close would have to delete it (built and not wrapped), its wave told.</summary>
        Private Sub Terminar(req As ProgramRequest)
            SyncLock _lock
                _enMano = Nothing
                _published.RemoveAll(Function(r) r.Wrapped OrElse r.State <> ProgramState.Built)
                If req.State = ProgramState.Built Then _published.Add(req)
            End SyncLock
            _owner.Finished(req)
        End Sub

        ''' <summary>One request, its programs one at a time (ProgramCompiler.Build), then published: its programs and one fence (Built), or Failed with
        ''' its first failure, the others it built deleted (S0 fase 7d's rule, the unit of r3 rev-12).</summary>
        Private Sub Construir(r As ProgramRequest)
            Dim ids = New Integer?(r.Specs.Length - 1) {}
            Dim falla As Exception = Nothing
            For i = 0 To r.Specs.Length - 1
                FijarHint(r.Specs(i))
#If DEBUG Then
                Dim previo = ProgramCompiler.GateBuildingSlot
                ProgramCompiler.GateBuildingSlot = r.Names(i)
#End If
                Try
                    ids(i) = ProgramCompiler.Build(r.Specs(i))
                Catch ex As Exception When FallaDeBuild(ex)
                    falla = ex
                    Exit For
#If DEBUG Then
                Finally
                    ProgramCompiler.GateBuildingSlot = previo
#End If
                End Try
            Next
            Publicar(r, ids, falla)
            Terminar(r)
        End Sub

        ''' <summary>A request published: Failed (its programs that were built deleted), or its programs with one fence for the
        ''' unit and a flush - unless its consumer let it go meanwhile (ProgramBuilder.Abandon: deleted).</summary>
        Private Shared Sub Publicar(req As ProgramRequest, hechos As Integer?(), causa As Exception)
            If causa IsNot Nothing Then
                For Each h In hechos
                    If h.HasValue Then GL.DeleteProgram(h.Value)
                Next
                req.PublishFailed(causa)
                Return
            End If
            Dim ids = hechos.Select(Function(h) h.Value).ToArray()
#If DEBUG Then
            ' GATE (subject mutant record-published-before-variant, r3 rev-12): the unit published with its first program only.
            If ids.Length > 1 AndAlso Fo4GBufferConstants.GateMutantOn("record-published-before-variant") Then ids = {ids(0)}
#End If
            Dim sync = GL.FenceSync(SyncCondition.SyncGpuCommandsComplete, WaitSyncFlags.None)
            GL.Flush()
            SyncLock req
                If req.Abandoned Then
                    ' let go while it was being built (ProgramBuilder.Abandon): nobody will wrap it
                    req.Wrapped = True
                    For Each pid In hechos : GL.DeleteProgram(pid.Value) : Next
                    GL.DeleteSync(sync)
                    req.PublishFailed(New InvalidOperationException("abandoned"))
                    Return
                End If
                req.PublishBuilt(ids, sync)
            End SyncLock
        End Sub

        ''' <summary>A request is in hand.</summary>
        Friend ReadOnly Property RequestInHand As Boolean
            Get
                SyncLock _lock
                    Return _enMano IsNot Nothing
                End SyncLock
            End Get
        End Property

#If DEBUG Then
        ''' <summary>DEBUG ONLY: the programs this worker built that nobody wrapped yet (asked together, not used).</summary>
        Friend Function GateBuiltNotWrapped() As Integer
            SyncLock _lock
                Return _published.Where(Function(r) Not r.Wrapped AndAlso r.State = ProgramState.Built).Sum(Function(r) r.Ids.Length)
            End SyncLock
        End Function
#End If

        ''' <summary>Waits for the thread's end (HiddenContexts.Drain only: the gates' end of run).</summary>
        Friend Sub WaitFinished()
            Finished.WaitOne()
        End Sub
    End Class
End Class

''' <summary>THE ONE OWNER OF THE HIDDEN WINDOWS (F9 r3 rev-14, r4 rev-14 b, r5 rev-20): the builders' NativeWindows are made and disposed
''' here, on the main thread only (GLFW: NativeWindow.Dispose throws off it), and every window stays reachable from this list until it
''' is disposed - so none ever reaches ~NativeWindow (which would throw on the finalizer thread). Application.Idle disposes the windows of
''' finished workers (and closes the worker of a control disposed without its Clean); Drain (the gates' end of run) closes, waits and
''' disposes all; Application.ApplicationExit only marks every worker closing and returns (r5 rev-20: the process ends without waiting;
''' the threads are background threads).
''' <para>THE CONTROLS PARKED BY A CLOSE DURING A BATCH (F9 E16, user decision 4-nov: the window closes at once; what was in flight is not stored
''' at the app's exit, the next opening compiles it): destroying a control's window while its builder has a link in flight waits for the link
''' inside the driver (GLFW.DestroyWindow from GLControl.OnHandleDestroyed: 26.9 s, the window visible and frozen; NVIDIA 610.88).
''' ParkIfBuilding takes such a control out of its form; Application.Idle disposes it when its request ended; at the app's exit it is not destroyed
''' at all (the list keeps it reachable, the same as the windows above).</para></summary>
Friend Module HiddenContexts

    <DllImport("user32.dll", SetLastError:=True)>
    Private Function SetParent(child As IntPtr, parent As IntPtr) As IntPtr
    End Function
    ''' <summary>HWND_MESSAGE: a message-only window. WinForms' parking window would not do: the UI thread's end destroys it with what it holds
    ''' (ThreadContext.DisposeThreadWindows -> DisposeParkingWindow), and that waits for the link the same.</summary>
    Private ReadOnly HwndMessage As New IntPtr(-3)
    Private ReadOnly _parked As New List(Of PreviewControl)

    Private ReadOnly _entries As New List(Of (Window As NativeWindow, Worker As ProgramBuilder.Worker))
    Private _hooked As Boolean
    Private _inExit As Boolean

    ''' <summary>A hidden window for <paramref name="worker"/>: made and listed together (a window not listed is disposed in the Finally).</summary>
    Friend Function Create(settings As NativeWindowSettings, worker As ProgramBuilder.Worker) As NativeWindow
        Hook()
        Dim w As NativeWindow = Nothing
        Dim listed = False
        Try
            w = New NativeWindow(settings)
            _entries.Add((w, worker))
            listed = True
#If DEBUG Then
            GateWindowsMade += 1
#End If
        Finally
            If Not listed AndAlso w IsNot Nothing Then w.Dispose()
        End Try
        Return w
    End Function

    Private Sub Hook()
        If _hooked Then Return
        _hooked = True
        AddHandler Application.Idle, AddressOf OnIdle
        AddHandler Application.ApplicationExit, AddressOf OnApplicationExit
    End Sub

    ''' <summary>PreviewControl.Release's last step when <paramref name="control"/>'s builder has work: its GL resources freed now (Clean
    ''' with the scene and the batch in flight: 17 ms measured; it closes the builder too), the control out of its form and under a message-only
    ''' window, listed for OnIdle. It asks HasPending, not RequestInHand: between two requests the worker holds nothing with the queue full, and it could
    ''' take the next one right after the check. False - the caller disposes the control - when the builder has no work, or when Clean threw or
    ''' SetParent failed (both logged: that Dispose waits for the link).</summary>
    Friend Function ParkIfBuilding(control As PreviewControl) As Boolean
        DisposeParked()
        Dim conTrabajo = control.Builder.HasPending
#If DEBUG Then
        ' GATE (subject mutant park-asks-in-hand): only what is in hand counts, the queue does not.
        If Fo4GBufferConstants.GateMutantOn("park-asks-in-hand") Then conTrabajo = control.Builder.RequestInHand
#End If
        If Not conTrabajo OrElse Not control.IsHandleCreated Then Return False
#If DEBUG Then
        ' GATE (subject mutant close-destroys-in-batch): the window destroyed with the link in flight, as before E16.
        If Fo4GBufferConstants.GateMutantOn("close-destroys-in-batch") Then Return False
#End If
        Try
            control.Clean()
        Catch ex As Exception
            Dim m = ex.GetType().Name & ": " & ex.Message
            Logger.LogLazy(Function() "[PROGRAM-BUILDER] ParkIfBuilding: Clean threw, the control is disposed instead: " & m)
            Return False
        End Try
        control.Parent = Nothing
        Dim movida = SetParent(control.Handle, HwndMessage) <> IntPtr.Zero
#If DEBUG Then
        ' GATE (subject mutant park-setparent-fails): SetParent refused: today's Dispose.
        If Fo4GBufferConstants.GateMutantOn("park-setparent-fails") Then movida = False
#End If
        If Not movida Then
            Dim er = Marshal.GetLastWin32Error()
            Logger.LogLazy(Function() $"[PROGRAM-BUILDER] ParkIfBuilding: SetParent failed (error {er}), the control is disposed instead: it waits for the link")
#If DEBUG Then
            GateParkFallbacks += 1
#End If
            Return False
        End If
        _parked.Add(control)
#If DEBUG Then
        GateControlsParked += 1
#End If
        Return True
    End Function

    ''' <summary>The parked controls whose request ended (or every one: the request of every worker ended, Drain) disposed. A failure is reported as
    ''' Release reports its own (PreviewControl.UnexpectedErrorChannel), and the next one goes on.</summary>
    Friend Sub DisposeParked()
        For i = _parked.Count - 1 To 0 Step -1
            Dim c = _parked(i)
            If c.Builder.RequestInHand Then Continue For
            _parked.RemoveAt(i)
            Try
                c.Dispose()
#If DEBUG Then
                GateParkedDisposed += 1
#End If
            Catch ex As Exception
                Dim m = ex.GetType().Name & ": " & ex.Message
                Logger.LogLazy(Function() "[PROGRAM-BUILDER] a parked control's Dispose threw " & m)
                PreviewControl.UnexpectedErrorChannel?.Invoke(ex, "the preview parked by a close during a batch")
            End Try
        Next
    End Sub

    ''' <summary>The main thread's ordinary path: each finished worker's window disposed; a worker whose control was disposed without its
    ''' Clean (PreviewControl.ReleasedWithoutContext) closed; each parked control whose request ended disposed.</summary>
    Private Sub OnIdle(sender As Object, e As EventArgs)
        DisposeParked()
        For i = _entries.Count - 1 To 0 Step -1
            Dim en = _entries(i)
            If Not en.Worker.Closing AndAlso en.Worker.Owner.Control.IsDisposed Then en.Worker.Close()
            If en.Worker.Finished.WaitOne(0) Then Dispose(i)
        Next
    End Sub

    Private Sub Dispose(i As Integer)
        Dim en = _entries(i)
        _entries.RemoveAt(i)
#If DEBUG Then
        ' GATE (subject mutant drop-dead-builder-window): the window let go without Dispose - its finalizer runs off the main thread.
        If Fo4GBufferConstants.GateMutantOn("drop-dead-builder-window") Then Return
#End If
        en.Window.Dispose()
        GC.SuppressFinalize(en.Window)
#If DEBUG Then
        GateWindowsDisposed += 1
#End If
    End Sub

    ''' <summary>THE GATES' END OF RUN (r4 rev-14 b): every worker closed, waited for (the program in hand ends) and its window disposed,
    ''' on the main thread. Never during use, never at the app's exit.</summary>
    Friend Sub Drain()
        For Each en In _entries.ToList()
            en.Worker.Close()
        Next
        For i = _entries.Count - 1 To 0 Step -1
#If DEBUG Then
            If _inExit Then GateApplicationExitWaits += 1
#End If
            _entries(i).Worker.WaitFinished()
            Dispose(i)
        Next
        DisposeParked()
    End Sub

    ''' <summary>The app's exit (r5 rev-20): every worker marked closing; nothing waited for.</summary>
    Friend Sub MarkAllClosing()
        For Each en In _entries
            en.Worker.Close()
        Next
    End Sub

    Private Sub OnApplicationExit(sender As Object, e As EventArgs)
        _inExit = True
        Try
#If DEBUG Then
            GateApplicationExitRuns += 1
            ' GATE (subject mutant exit-drains): the exit waits for the programs in hand (GateApplicationExitWaits > 0).
            If Fo4GBufferConstants.GateMutantOn("exit-drains") Then
                Drain()
                Return
            End If
#End If
            MarkAllClosing()
        Finally
            _inExit = False
#If DEBUG Then
            Dim runs = GateApplicationExitRuns, waits = GateApplicationExitWaits
            Logger.LogLazy(Function() $"[PROGRAM-BUILDER] ApplicationExit: run {runs}, waits {waits}, UI awaits {ProgramBuilder.GateUiAwaits.Count}")
#End If
        End Try
    End Sub

    ''' <summary>Hidden windows still listed (made and not disposed).</summary>
    Friend ReadOnly Property Count As Integer
        Get
            Return _entries.Count
        End Get
    End Property

#If DEBUG Then
    Friend GateWindowsMade As Integer
    Friend GateWindowsDisposed As Integer
    ''' <summary>DEBUG ONLY (E16): the controls still parked.</summary>
    Friend ReadOnly Property GateParkedNow As Integer
        Get
            Return _parked.Count
        End Get
    End Property
    ''' <summary>DEBUG ONLY (E16): controls parked by a close during a batch / disposed afterwards / SetParent's failures (today's Dispose taken).</summary>
    Friend GateControlsParked, GateParkedDisposed, GateParkFallbacks As Integer
    ''' <summary>DEBUG ONLY (r6 rev-20 b): the waits made while Application.ApplicationExit ran (the gate requires 0).</summary>
    Friend GateApplicationExitWaits As Integer
    ''' <summary>DEBUG ONLY (r7 rev-20 a): how many times Application.ApplicationExit ran (the gate fails when 0: no subject).</summary>
    Friend GateApplicationExitRuns As Integer
#End If

End Module

''' <summary>F9, Ola SSE S1 F3: THE REQUESTS OF A HOLDER OF RECORDS (the one code of Fo4DeferredPrograms and SseEnginePrograms). One record through
''' the builder: the record's request (made on its first ask: <c>specs</c> composed then - a composition that throws is the record's
''' failure -, named <c>names</c>, one per program, the unit published whole) resolved: Built gives its programs (wrapped once, owned by the
''' holder: <c>built</c>); Failed gives the build's message; Pending, in a frame, marks the frame pending (PreviewControl.MarkFramePending)
''' and returns; outside a frame it is waited for (ProgramBuilder.Await).</summary>
Friend NotInheritable Class ProgramRecordRequests(Of T As Shader_Base_Class)
    Private ReadOnly _builder As ProgramBuilder
    Private ReadOnly _wrap As Func(Of Integer, ProgramSpec, T)
    Private ReadOnly _built As List(Of Shader_Base_Class)
    ''' <summary>The records' requests not resolved yet, by record (family, route and key).</summary>
    Friend ReadOnly Pending As New Dictionary(Of String, ProgramRequest)(StringComparer.Ordinal)

    Friend Sub New(builder As ProgramBuilder, wrap As Func(Of Integer, ProgramSpec, T), built As List(Of Shader_Base_Class))
        _builder = builder : _wrap = wrap : _built = built
    End Sub

    Friend Function Ask(clave As String, names As String(), specs As Func(Of ProgramSpec()), ByRef programs As T(), ByRef errorText As String) As ProgramState
        programs = Nothing
        Dim req As ProgramRequest = Nothing
        If Not Pending.TryGetValue(clave, req) Then
            Dim sp As ProgramSpec()
            Try
                sp = specs()
            Catch ex As Exception
                errorText = ex.Message
                Return ProgramState.Failed
            End Try
            req = _builder.Request(names, sp)
            Pending(clave) = req
        End If
        If req.State = ProgramState.Pending Then
            If _builder.InFrame Then
                _builder.Control.MarkFramePending()
                Return ProgramState.Pending
            End If
            _builder.Await(req)
            If req.State = ProgramState.Pending Then
#If DEBUG Then
                Threading.Interlocked.Increment(ProgramBuilderGates.GateOutsideFramePending)
#End If
                Return ProgramState.Pending
            End If
        End If
        Pending.Remove(clave)
        If req.State = ProgramState.Failed Then
            errorText = req.FailureText
            Return ProgramState.Failed
        End If
        Dim ids = _builder.Wrap(req)
        programs = New T(ids.Length - 1) {}
        For i = 0 To ids.Length - 1
            programs(i) = _wrap(ids(i), req.Specs(i))
            _built.Add(programs(i))
        Next
        Return ProgramState.Built
    End Function

    ''' <summary>Where a record is without asking for it (r3 rev-13 / rev-18: the admission of a shape outside a frame never enqueues nor waits).</summary>
    Friend Function State(clave As String) As ProgramState
        Dim req As ProgramRequest = Nothing
        If Not Pending.TryGetValue(clave, req) Then Return ProgramState.NotBuilt
        Return req.State
    End Function

    ''' <summary>A record still in the builder is let go (its programs deleted when it finishes, ProgramBuilder.Abandon).</summary>
    Friend Sub AbandonAll()
        For Each r In Pending.Values : _builder.Abandon(r) : Next
        Pending.Clear()
    End Sub
End Class
