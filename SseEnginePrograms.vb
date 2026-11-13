Option Strict On
Option Explicit On

''' <summary>A transcribed program of Skyrim SE's frame (g6 Lighting, g7 Utility): SseEnginePrograms builds it from its spec.</summary>
Public Class Sse_Engine_Shader_Class
    Inherits Shader_Base_Class
    ''' <summary>The program already built from <paramref name="spec"/>.</summary>
    Friend Sub New(programId As Integer, spec As ProgramSpec)
        MyBase.New(programId, spec)
    End Sub
End Class

''' <summary>THE TRANSCRIBED PROGRAMS OF SKYRIM SE'S FRAME (Ola SSE S1 F3), each built on its first use by the control's builder (F9), as
''' Fo4DeferredPrograms: the utility programs (g7: the z-prepass, the shadow map, the screen-space mask; SetupTechnique 0x141566710) and
''' the lighting programs (g6: the colour pass; SetupTechnique 0x141547D20), keyed by the records' descriptors and the vertex route. A
''' technique the engine has no record for is not drawn (BeginTechnique 0x141579120 returns 0: MissingEnginePrograms, the frame's notice); a
''' build that fails is remembered with its message and reported; a record still being built, in a frame, answers Nothing and marks the
''' frame pending ("Compiling shaders n/m").</summary>
Friend NotInheritable Class SseEnginePrograms
    Private ReadOnly _builder As ProgramBuilder
    Private ReadOnly _built As New List(Of Shader_Base_Class)
    Private ReadOnly _requests As ProgramRecordRequests(Of Sse_Engine_Shader_Class)

    ''' <summary>The programs of one record list by (VS key, PS key, route), and the keys whose build failed with the message.</summary>
    Private NotInheritable Class RecordPrograms
        Friend ReadOnly Programs As New Dictionary(Of (UInteger, UInteger, Boolean), Sse_Engine_Shader_Class)
        Friend ReadOnly Errors As New Dictionary(Of (UInteger, UInteger, Boolean), String)
    End Class
    Private ReadOnly _utility As New RecordPrograms, _lighting As New RecordPrograms, _effect As New RecordPrograms, _effectSurface As New RecordPrograms

    Friend Sub New(builder As ProgramBuilder)
        _builder = builder
        _requests = New ProgramRecordRequests(Of Sse_Engine_Shader_Class)(builder, Function(id, s) New Sse_Engine_Shader_Class(id, s), _built)
    End Sub

    ' ---- THE SPECS: the one place each pairs its texts (these programs build from them and ProgramCatalog enumerates them). <paramref
    ' name="cpuRoute"/> is the EFFECTIVE route (the CPU route of a VS that has one, Sse*Source.EffectiveCpuLayout). ----

    ''' <summary>A g7 program: VS <paramref name="vs"/> on its route with PS <paramref name="ps"/>, or with the null fragment when
    ''' <paramref name="nullPs"/> (SseUtilityTechnique.PixelShaderNull: BeginTechnique binds no PS, 0x141579204).</summary>
    Friend Shared Function UtilitySpec(vs As UInteger, ps As UInteger, nullPs As Boolean, cpuRoute As Boolean) As ProgramSpec
        Return Shader_Base_Class.SpecOf(SseUtilitySource.VertexOf(vs, cpuRoute), If(nullPs, SseUtilitySource.NullFragment(), SseUtilitySource.FragmentOf(ps)))
    End Function

    ''' <summary>A g6 program: VS <paramref name="vs"/> on its route with PS <paramref name="ps"/>.</summary>
    Friend Shared Function LightingSpec(vs As UInteger, ps As UInteger, cpuRoute As Boolean) As ProgramSpec
        Return Shader_Base_Class.SpecOf(SseLightingSource.VertexOf(vs, cpuRoute), SseLightingSource.FragmentOf(ps))
    End Function

    ''' <summary>A g5 program (Ola SSE S2 F3.1): VS <paramref name="vs"/> on its route with PS <paramref name="ps"/>.</summary>
    Friend Shared Function EffectSpec(vs As UInteger, ps As UInteger, cpuRoute As Boolean) As ProgramSpec
        Return Shader_Base_Class.SpecOf(SseEffectSource.VertexOf(vs, cpuRoute), SseEffectSource.FragmentOf(ps))
    End Function

    ''' <summary>The translucent decals' base surface of an effect decal (Ola SSE S2 F3.8, Fallout 4's Fo4DeferredPrograms.EffectSurfaceSpec):
    ''' its engine VS with the surface foot and SseEffectSource.AppSurfaceFragment.</summary>
    Friend Shared Function EffectSurfaceSpec(vs As UInteger, cpuRoute As Boolean) As ProgramSpec
        Return Shader_Base_Class.SpecOf(SseEffectSource.VertexOf(vs, cpuRoute, surface:=True), SseEffectSource.AppSurfaceFragment)
    End Function

    ''' <summary>One record through the builder, remembered in <paramref name="list"/>.</summary>
    Private Function Record(list As RecordPrograms, family As String, key As (UInteger, UInteger, Boolean), specOf As Func(Of ProgramSpec),
                            ByRef errorText As String, ByRef state As ProgramState) As Sse_Engine_Shader_Class
        state = ProgramState.Built
        Dim p As Sse_Engine_Shader_Class = Nothing
        If list.Programs.TryGetValue(key, p) Then Return p
        If list.Errors.TryGetValue(key, errorText) Then state = ProgramState.Failed : Return Nothing
        Dim ps As Sse_Engine_Shader_Class() = Nothing
        state = _requests.Ask($"{family}|{key}", {Nothing}, Function() {specOf()}, ps, errorText)
        Select Case state
            Case ProgramState.Built
                list.Programs(key) = ps(0)
                Return ps(0)
            Case ProgramState.Failed
                list.Errors(key) = errorText
        End Select
        Return Nothing
    End Function

    ''' <summary>The g7 program of utility technique <paramref name="t"/> (SetupTechnique 0x141566710: VS key, PS key, the null PS), on the
    ''' draw's vertex route. Nothing with <paramref name="errorText"/> = the programs the engine does not have (the pass is not drawn,
    ''' 0x1415667A2) or the build's message; Nothing with errorText Nothing: still being built (<paramref name="state"/> Pending).</summary>
    Friend Function UtilityProgram(t As UInteger, cpuRoute As Boolean, ByRef errorText As String, ByRef state As ProgramState, Optional filter As Integer = SseUtilityTechnique.ShadowFilterType) As Sse_Engine_Shader_Class
        errorText = SseUtilityTechnique.MissingEnginePrograms(t, filter)
        If errorText IsNot Nothing Then state = ProgramState.Failed : Return Nothing
        Dim vs = SseUtilityTechnique.VertexShaderKey(t)
        Dim ps = SseUtilityTechnique.PixelShaderKey(t, filter)
        Dim nullPs = SseUtilityTechnique.PixelShaderNull(t)
        Dim cpu = SseUtilitySource.EffectiveCpuLayout(vs, cpuRoute) <> Fo4CpuRouteLayout.None
        Return Record(_utility, "ut", (vs, If(nullPs, 0UI, ps), cpu), Function() UtilitySpec(vs, ps, nullPs, cpu), errorText, state)
    End Function

    ''' <summary>The g6 program of lighting technique <paramref name="t"/> (SetupTechnique 0x141547D20, keys 0x1415896A0 / 0x1415896D0), on the
    ''' draw's vertex route. The same answers as UtilityProgram (0x141547DA4: no record, the pass is not drawn).</summary>
    Friend Function LightingProgram(t As UInteger, cpuRoute As Boolean, ByRef errorText As String, ByRef state As ProgramState) As Sse_Engine_Shader_Class
        errorText = SseLightingTechnique.MissingEnginePrograms(t)
        If errorText IsNot Nothing Then state = ProgramState.Failed : Return Nothing
        Dim vs = SseLightingTechnique.VertexShaderKey(t)
        Dim ps = SseLightingTechnique.PixelShaderKey(t)
        Dim cpu = SseLightingSource.EffectiveCpuLayout(vs, cpuRoute) <> Fo4CpuRouteLayout.None
        Return Record(_lighting, "lit", (vs, ps, cpu), Function() LightingSpec(vs, ps, cpu), errorText, state)
    End Function

    ''' <summary>The g5 program of effect technique <paramref name="t"/> (Ola SSE S2 F3.1; SetupTechnique 0x141556840, keys 0x14158DAB0 /
    ''' 0x14158DAC0), on the draw's vertex route. The same answers as LightingProgram (0x141556877 -&gt; 0x141556C22: no record, the pass is
    ''' not drawn).</summary>
    Friend Function EffectProgram(t As UInteger, cpuRoute As Boolean, ByRef errorText As String, ByRef state As ProgramState) As Sse_Engine_Shader_Class
        errorText = SseEffectTechnique.MissingEnginePrograms(t)
        If errorText IsNot Nothing Then state = ProgramState.Failed : Return Nothing
        Dim vs = SseEffectTechnique.VertexShaderKey(t)
        Dim ps = SseEffectTechnique.PixelShaderKey(t)
        Dim cpu = SseEffectSource.EffectiveCpuLayout(vs, cpuRoute) <> Fo4CpuRouteLayout.None
        Return Record(_effect, "eff", (vs, ps, cpu), Function() EffectSpec(vs, ps, cpu), errorText, state)
    End Function

    ''' <summary>The decals' base surface program of effect technique <paramref name="t"/>'s VS on the draw's route (EffectSurfaceSpec); the
    ''' same answers as EffectProgram.</summary>
    Friend Function EffectSurfaceProgram(t As UInteger, cpuRoute As Boolean, ByRef errorText As String, ByRef state As ProgramState) As Sse_Engine_Shader_Class
        errorText = SseEffectTechnique.MissingEnginePrograms(t)
        If errorText IsNot Nothing Then state = ProgramState.Failed : Return Nothing
        Dim vs = SseEffectTechnique.VertexShaderKey(t)
        Dim cpu = SseEffectSource.EffectiveCpuLayout(vs, cpuRoute) <> Fo4CpuRouteLayout.None
        Return Record(_effectSurface, "effsurf", (vs, 0UI, cpu), Function() EffectSurfaceSpec(vs, cpu), errorText, state)
    End Function

    ''' <summary>The remembered build failures forgotten (a gesture's retry, PreviewControl.ForgetProgramFailures).</summary>
    Friend Sub ForgetFailures()
        _utility.Errors.Clear()
        _lighting.Errors.Clear()
        _effect.Errors.Clear()
        _effectSurface.Errors.Clear()
    End Sub

    ''' <summary>Every program deleted, every request still in the builder let go (the control's Clean, the context current).</summary>
    Friend Sub Dispose()
        For Each p In _built : p.Dispose() : Next
        _built.Clear()
        _utility.Programs.Clear() : _utility.Errors.Clear()
        _lighting.Programs.Clear() : _lighting.Errors.Clear()
        _effect.Programs.Clear() : _effect.Errors.Clear()
        _effectSurface.Programs.Clear() : _effectSurface.Errors.Clear()
        _requests.AbandonAll()
    End Sub
End Class
