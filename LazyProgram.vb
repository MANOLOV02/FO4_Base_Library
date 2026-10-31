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
    ''' <summary>Deletes the program the slot built (not one a gate put in it) and forgets it and its error: the next use builds again.</summary>
    Sub Release()
End Interface

''' <summary>ONE PROGRAM BUILT ON ITS FIRST USE (S0-A, user decision P2 of 29-oct-2026: on demand in both games and builds). The frame
''' asks for a program where it binds it (GetProgram): the first ask builds it through the program's own class (ProgramCompiler, with
''' its cache); a build that fails throws ProgramUnavailableException, now and on every later ask (the error is remembered, never
''' retried by itself nor in a loop: only a gesture's retry forgets it, PreviewControl.ForgetProgramFailures), which aborts the frame. So the law of "the frame uses this program" is the frame's own code, nothing lists it a
''' second time, and no frame is presented with part of its programs: the frame is composed in a back buffer or the offscreen targets and
''' presented only after it ends (PreviewControl.RenderScene / PresentFrame).</summary>
Friend NotInheritable Class LazyProgram(Of T As Shader_Base_Class)
    Implements IProgramSlot

    Private ReadOnly _make As Func(Of T)
    Private _program As T
    ''' <summary>The program the slot built (Release deletes it), whatever a gate put in _program meanwhile (SetProgram).</summary>
    Private _own As T
    Private _error As String
    Private _errorCause As Exception

    Friend ReadOnly Property Name As String Implements IProgramSlot.Name

    Friend Sub New(name As String, make As Func(Of T))
        Me.Name = name
        _make = make
    End Sub

    ''' <summary>A slot that holds <paramref name="program"/> and builds nothing (a gate's replacement; not deleted by Release).</summary>
    Friend Shared Function Fixed(name As String, program As T) As LazyProgram(Of T)
        Dim s As New LazyProgram(Of T)(name, Nothing)
        s._program = program
        Return s
    End Function

    ''' <summary>The program, built on this first ask; ProgramUnavailableException when its build fails (now or before).</summary>
    Friend Function GetProgram() As T
        If _program IsNot Nothing Then Return _program
        If _error IsNot Nothing Then Throw New ProgramUnavailableException(_error, _errorCause)
        Try
#If DEBUG Then
            If ProgramCompiler.GateFailSlot IsNot Nothing AndAlso String.Equals(ProgramCompiler.GateFailSlot, Name, StringComparison.Ordinal) Then
                Throw New ProgramBuildException("build failed (gate --program-abort)")
            End If
#End If
#If DEBUG Then
            Dim previous = ProgramCompiler.GateBuildingSlot
            ProgramCompiler.GateBuildingSlot = Name
            Try
                _program = _make()
            Finally
                ProgramCompiler.GateBuildingSlot = previous
            End Try
#Else
            _program = _make()
#End If
            _own = _program
        Catch ex As Exception When TypeOf ex IsNot ProgramUnavailableException
            _error = $"{Name}: {ex.Message}"
            _errorCause = ex
            Throw New ProgramUnavailableException(_error, ex)
        End Try
        Return _program
    End Function

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

    Friend Sub Release() Implements IProgramSlot.Release
        _own?.Dispose()
        _own = Nothing
        If _make IsNot Nothing Then _program = Nothing
        _error = Nothing
        _errorCause = Nothing
    End Sub
End Class
