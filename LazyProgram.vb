Option Strict On
Option Explicit On

''' <summary>A program the frame needed and could not build (LazyProgram.GetProgram): its frame is ABORTED (PreviewControl.RenderScene) -
''' nothing of it is presented, its game's status card says this message (rev-38 / rev-50: a program that does not build leaves ITS game
''' without a frame, never a fallback). The message is the build's (ProgramBuildException's driver log, or the text check's).</summary>
Friend NotInheritable Class ProgramUnavailableException
    Inherits Exception
    Friend Sub New(message As String, inner As Exception)
        MyBase.New(message, inner)
    End Sub
End Class

''' <summary>What the control does with every program slot it holds, whatever its program's class.</summary>
Friend Interface IProgramSlot
    ''' <summary>The slot's name (the field it stands for), for the diagnostics and the gates.</summary>
    ReadOnly Property Name As String
    ''' <summary>The program if it is built, without building it.</summary>
    ReadOnly Property BuiltProgram As Shader_Base_Class
    ''' <summary>Its build failed and the failure is remembered (GetProgram throws until Release: a gesture's retry, S0 fase 7 rev-08).</summary>
    ReadOnly Property Failed As Boolean
    ''' <summary>Builds the program now (GetProgram) and returns it.</summary>
    Function Build() As Shader_Base_Class
    ''' <summary>F9: where its program is, without asking for it nor waiting (r3 rev-13 / rev-18: Peek).</summary>
    Function Peek() As ProgramState
    ''' <summary>F9 (r2 "Pedidos juntos"): asks the builder for its program without waiting nor throwing (a closed list asked whole).</summary>
    Sub RequestOnly()
    ''' <summary>Deletes the program the slot built (not one a gate put in it) and forgets it and its error: the next use builds again.</summary>
    Sub Release()
End Interface

''' <summary>ONE PROGRAM BUILT ON ITS FIRST USE (S0-A, user decision P2 of 29-oct-2026: on demand in both games and builds), BY THE CONTROL'S
''' BUILDER THREAD (F9: ProgramBuilder - the UI thread never compiles nor links it). The frame asks for a program where it binds it
''' (GetProgram): the first ask requests the slot's spec from the builder; INSIDE A FRAME (ProgramBuilder.InFrame) a program still being
''' built throws ProgramPendingException - the frame is not presented, its card says "Compiling shaders n/m (once per build)" (PreviewControl.PendingFrame)
''' -, OUTSIDE A FRAME the ask waits for it (ProgramBuilder.Await: the gates, the captures that wait). A finished program is wrapped on the
''' UI thread (ProgramBuilder.Wrap, then the slot's wrap function: the program's class on the built id). A build that fails throws
''' ProgramUnavailableException, now and on every later ask (the error is remembered, never retried by itself nor in a loop: only a
''' gesture's retry forgets it, PreviewControl.ForgetProgramFailures), which aborts the frame. So the law of "the frame uses this
''' program" is the frame's own code, nothing lists it a second time, and no frame is presented with part of its programs: the frame is
''' composed in a back buffer or the offscreen targets and presented only after it ends (PreviewControl.RenderScene / PresentFrame).</summary>
Friend NotInheritable Class LazyProgram(Of T As Shader_Base_Class)
    Implements IProgramSlot

    Private ReadOnly _builder As ProgramBuilder
    Private ReadOnly _specOf As Func(Of ProgramSpec)
    Private _spec As ProgramSpec?
    Private ReadOnly _wrap As Func(Of Integer, ProgramSpec, T)
    Private _request As ProgramRequest
    Private _program As T
    ''' <summary>The program the slot built (Release deletes it), whatever a gate put in _program meanwhile (SetProgram).</summary>
    Private _own As T
    Private _error As String
    Private _errorCause As Exception

    Friend ReadOnly Property Name As String Implements IProgramSlot.Name

    ''' <param name="builder">The builder of the control whose context the program is used in (r4 rev-19: every container passes its own).</param>
    ''' <param name="spec">What the program is built from (its class's spec: the one place its texts are paired).</param>
    ''' <param name="wrap">The program's class on a built id (Shader_Base_Class.New(programId, spec) of the class).</param>
    Friend Sub New(name As String, builder As ProgramBuilder, spec As ProgramSpec, wrap As Func(Of Integer, T))
        Me.New(name, builder, Function() spec, Function(id As Integer, s As ProgramSpec) wrap(id))
    End Sub

    ''' <summary>A slot whose spec is composed on its first request (<paramref name="specOf"/>: a text composed from the transcribed blocks,
    ''' or a law check that throws - its failure is the slot's), wrapped with that spec.</summary>
    Friend Sub New(name As String, builder As ProgramBuilder, specOf As Func(Of ProgramSpec), wrap As Func(Of Integer, ProgramSpec, T))
        If builder Is Nothing Then Throw New ArgumentNullException(NameOf(builder))
        Me.Name = name
        _builder = builder
        _specOf = specOf
        _wrap = wrap
    End Sub

    ''' <summary>The slot's request, made now (its spec composed once).</summary>
    Private Function NewRequest() As ProgramRequest
        If Not _spec.HasValue Then _spec = _specOf()
        Return _builder.Request({Name}, {_spec.Value})
    End Function

    Private Sub New(name As String)
        Me.Name = name
    End Sub

    ''' <summary>A slot that holds <paramref name="program"/> and builds nothing (a gate's replacement; not deleted by Release).</summary>
    Friend Shared Function Fixed(name As String, program As T) As LazyProgram(Of T)
        Dim s As New LazyProgram(Of T)(name)
        s._program = program
        Return s
    End Function


    ''' <summary>The program; ProgramPendingException in a frame while it is being built; ProgramUnavailableException when its build
    ''' failed (now or before).</summary>
    Friend Function GetProgram() As T
        Dim st As ProgramState
        Return Access(st, throwPending:=True)
    End Function

    ''' <summary>F9 (r2 rev-01: the lights and composites are asked as records): the program, or Nothing with <paramref name="state"/>
    ''' Pending in a frame while it is being built (the frame is marked pending, PreviewControl.MarkFramePending, and goes on);
    ''' ProgramUnavailableException when its build failed.</summary>
    Friend Function GetOrPending(ByRef state As ProgramState) As T
        Return Access(state, throwPending:=False)
    End Function

    Private Function Access(ByRef state As ProgramState, throwPending As Boolean) As T
        If _program IsNot Nothing Then
            state = ProgramState.Built
            Return _program
        End If
        If _error IsNot Nothing Then Throw New ProgramUnavailableException(_error, _errorCause)
        If _wrap Is Nothing Then
            state = ProgramState.NotBuilt
            Return Nothing
        End If
#If DEBUG Then
        If ProgramCompiler.GateFailSlot IsNot Nothing AndAlso String.Equals(ProgramCompiler.GateFailSlot, Name, StringComparison.Ordinal) Then
            Fail(New ProgramBuildException("build failed (gate --program-abort)"))
        End If
#End If
        If _request Is Nothing Then
            Try
                _request = NewRequest()
            Catch ex As Exception When TypeOf ex IsNot ProgramUnavailableException
                Fail(ex)
            End Try
        End If
        If _request.State = ProgramState.Pending Then
            If _builder.InFrame Then
                state = ProgramState.Pending
                If throwPending Then Throw New ProgramPendingException(Name)
                _builder.Control.MarkFramePending()
                Return Nothing
            End If
            _builder.Await(_request)
            If _request.State = ProgramState.Pending Then
#If DEBUG Then
                Threading.Interlocked.Increment(ProgramBuilderGates.GateOutsideFramePending)
#End If
                state = ProgramState.Pending
                If throwPending Then Throw New ProgramPendingException(Name)
                Return Nothing
            End If
        End If
        state = Resolve()
        Return _program
    End Function

    ''' <summary>The request finished: Built is wrapped (once) into the slot's program, Failed is remembered and thrown.</summary>
    Private Function Resolve() As ProgramState
        If _request.State = ProgramState.Failed Then Fail(_request.Failure)
        Try
            _program = _wrap(_builder.Wrap(_request)(0), _request.Specs(0))
        Catch ex As Exception When TypeOf ex IsNot ProgramUnavailableException
            Fail(ex)
        End Try
        _own = _program
        Return ProgramState.Built
    End Function

    Private Sub Fail(cause As Exception)
        _error = $"{Name}: {If(cause Is Nothing, "build failed", cause.Message)}"
        _errorCause = cause
        Throw New ProgramUnavailableException(_error, cause)
    End Sub

    Friend Function Peek() As ProgramState Implements IProgramSlot.Peek
        If _program IsNot Nothing Then Return ProgramState.Built
        If _error IsNot Nothing Then Return ProgramState.Failed
        If _request Is Nothing Then Return ProgramState.NotBuilt
        Return _request.State
    End Function

    ''' <summary>F9 (r3 rev-13, the card's background): the program when it is built (wrapped now if it just finished), else Nothing -
    ''' never waits, never throws; a slot not asked for yet is asked for when <paramref name="request"/>.</summary>
    Friend Function PeekProgram(request As Boolean) As T
        If _program IsNot Nothing OrElse _error IsNot Nothing OrElse _wrap Is Nothing Then Return _program
        If _request Is Nothing Then
            If Not request Then Return Nothing
            Try
                _request = NewRequest()
            Catch ex As Exception
                _error = $"{Name}: {ex.Message}"
                _errorCause = ex
                Return Nothing
            End Try
        End If
        Select Case _request.State
            Case ProgramState.Built
                Try
                    Resolve()
                Catch ex As ProgramUnavailableException
                    Return Nothing
                End Try
                Return _program
            Case ProgramState.Failed
                _error = $"{Name}: {_request.FailureText}"
                _errorCause = _request.Failure
                Return Nothing
            Case Else
                Return Nothing
        End Select
    End Function

    Friend Sub RequestOnly() Implements IProgramSlot.RequestOnly
        If _program IsNot Nothing OrElse _error IsNot Nothing OrElse _wrap Is Nothing OrElse _request IsNot Nothing Then Return
        Try
            _request = NewRequest()
        Catch ex As Exception
            _error = $"{Name}: {ex.Message}"   ' its GetProgram throws it
            _errorCause = ex
        End Try
    End Sub

    ''' <summary>A gate's program in place of the slot's (not the slot's: Release does not delete it; the slot's own, if built, is still
    ''' deleted by Release).</summary>
    Friend Sub SetProgram(program As T)
        _program = program
    End Sub

    Friend ReadOnly Property BuiltProgram As Shader_Base_Class Implements IProgramSlot.BuiltProgram
        Get
            Return _program
        End Get
    End Property

    Friend ReadOnly Property Failed As Boolean Implements IProgramSlot.Failed
        Get
            Return _error IsNot Nothing
        End Get
    End Property

    Friend Function Build() As Shader_Base_Class Implements IProgramSlot.Build
        Return GetProgram()
    End Function

    ''' <summary>Deletes the program the slot built and forgets it, its error and its request (a request still in the builder is
    ''' abandoned: its programs deleted when it finishes, ProgramBuilder.Abandon).</summary>
    Friend Sub Release() Implements IProgramSlot.Release
        _own?.Dispose()
        _own = Nothing
        If _wrap IsNot Nothing Then _program = Nothing
        If _request IsNot Nothing Then _builder.Abandon(_request)
        _request = Nothing
        _error = Nothing
        _errorCause = Nothing
    End Sub
End Class

#If DEBUG Then
''' <summary>DEBUG ONLY (F9 r2 "Un solo primitivo"): the accesses OUTSIDE a frame that came back pending (the battery requires 0; mutant
''' await-returns-pending).</summary>
Friend Module ProgramBuilderGates
    Friend GateOutsideFramePending As Integer
End Module
#End If
