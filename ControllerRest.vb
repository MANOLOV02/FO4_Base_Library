Imports System.Collections.Generic
Imports NiflySharp.Blocks
Imports NiflySharp.Structs
Imports NiflySharp.Enums

''' <summary>THE VALUE A MATERIAL CONTROLLER GIVES AT THE GAME CLOCK'S ZERO, the preview's "at rest" (user decision 6-oct-2026: the
''' animated material still, with the game's value at t = 0). Transcribed from Fallout4.exe 1.11.240.0 / SkyrimSE.exe (identical
''' code; scripts and disassembly: scratchpad grupo-e\re_ctrl).
''' <para>The clock: every controller of the corpus that writes is APP_TIME (flags bit 0 clear), so its scaled time follows the
''' engine's global clock (FO4 0x1430E8940, from 0 at launch, + the frame delta, 0x1402D1B40): the game has no fixed instant per
''' object. The preview uses the clock's origin, 0 - the first update with lastTime = -FLT_MAX gives t * freq + phase
''' (0x1416CB23C..2AF / 0x140EF86D0) = phase.</para>
''' <para>Holes, declared: StartAnimations (0x1416CB020) activates inactive controllers; which loads call it is NOT TRACED - an
''' inactive controller writes nothing here (its Update returns at bit 3). Whether the engine starts a NiControllerSequence by
''' itself is NOT TRACED: a manager-controlled controller whose interpolator is a NiBlend* with no sequence (count 0,
''' 0x1417442D7) writes nothing. TBC keys between two keys: derived tangents NOT TRACED (no value). Several controllers on one
''' variable: the chain order of UpdateControllers (0x1416CF440), last one wins - DEDUCED from the call order.</para></summary>
Friend Module ControllerRestLaw

    Private ReadOnly NegMax As Single = BitConverter.Int32BitsToSingle(&HFF7FFFFF)
    Private ReadOnly PosMax As Single = BitConverter.Int32BitsToSingle(&H7F7FFFFF)
    ''' <summary>pi / 180 as the exe writes it (0x14224BB57 / 0x14157BFF7: cos(v * 0.0174532924)).</summary>
    Private ReadOnly DegToRad As Single = BitConverter.Int32BitsToSingle(&H3C8EFA35)

    ''' <summary>NiTimeController::ComputeScaledTime (FO4 0x1416CB210, SSE 0x140EF86D0) at its FIRST update with clock 0:
    ''' st = (0 * freq + 0) + phase (APP_INIT: delta 0, the same), then the cycle when [lo, hi] is set (hi &lt;&gt; -FLT_MAX and
    ''' lo &lt;&gt; +FLT_MAX): LOOP st = fmodf(st - lo, span) + lo, + span if below lo; REVERSE x = fmodf(st, 2 span), + 2 span if
    ''' negative, lo + (2 span - x) past span else lo + x; CLAMP nothing; a zero span gives lo. Then r = st &gt; hi ? hi :
    ''' max(lo, st), and backwards (bit 4) r = hi - (r - lo). fmodf of the CRT (0x1422C47BC).</summary>
    Friend Function ScaledTimeAtClockZero(c As NiTimeController) As Single
        Dim fl = CInt(c.Flags.Value)
        Dim lo = c.StartTime, hi = c.StopTime
        Dim st = (0.0F * c.Frequency + 0.0F) + c.Phase
        If hi <> NegMax AndAlso lo <> PosMax Then
            Select Case (fl >> 1) And 3
                Case 0
                    Dim span = hi - lo
                    If span = 0.0F Then
                        st = lo
                    Else
                        st = FModF(st - lo, span) + lo
                        If Not (st >= lo) Then st += span
                    End If
                Case 1
                    Dim span = hi - lo
                    If span = 0.0F Then
                        st = lo
                    Else
                        Dim two = span + span
                        Dim x = FModF(st, two)
                        If Not (x >= 0.0F) Then x += two
                        st = If(x > span, lo + (two - x), lo + x)
                    End If
            End Select
        End If
        Dim r = If(st > hi, hi, Math.Max(lo, st))
        If (fl And &H10) <> 0 Then r = hi - (r - lo)
        Return r
    End Function

    ''' <summary>The CRT fmodf the exe imports (IAT 0x142441108): C fmod semantics (sign of the dividend), which .NET's % on Single
    ''' implements (ECMA-335 rem = C fmod).</summary>
    Private Function FModF(a As Single, b As Single) As Single
        Return a Mod b
    End Function

    ''' <summary>GenInterp of a float key group (FO4 0x14174C400 = SSE 0x140F6ECB0) from lastIdx 0: one key or t = -FLT_MAX =&gt; key 0;
    ''' i advances while t &gt; key(i+1).time; past the last key =&gt; its value; u = (t - t_i) / (t_i+1 - t_i). LINEAR (1 - u) v0 + u v1
    ''' (0x141775810); CONST/step u &gt;= 1 ? v1 : v0 (0x141770230); QUADRATIC Hermite with D0 = the SECOND file tangent of key i
    ''' (Backward) and D1 = the FIRST of key i+1 (Forward) (load 0x14176A687..6C0; 0x141775840); TBC only at u = 0 (NOT TRACED
    ''' otherwise); NOINTERP writes nothing. Nothing = no write.</summary>
    Friend Function FloatAt(kg As KeyGroup(Of Single), t As Single) As Single?
        Dim k = kg.Keys
        If k Is Nothing OrElse k.Count = 0 Then Return Nothing
        Dim n = k.Count
        If n = 1 OrElse t = NegMax Then Return k(0).Value
        Dim i = 0
        While i + 1 <= n - 1 AndAlso t > k(i + 1).Time
            i += 1
        End While
        If i + 1 >= n Then Return k(i).Value
        Dim u = (t - k(i).Time) / (k(i + 1).Time - k(i).Time)
        Dim v0 = k(i).Value, v1 = k(i + 1).Value
        Select Case kg.Interpolation
            Case KeyType.LINEAR_KEY : Return (1.0F - u) * v0 + u * v1
            Case KeyType.CONST_KEY : Return If(u >= 1.0F, v1, v0)
            Case KeyType.QUADRATIC_KEY : Return Hermite(v0, v1, k(i).Backward, k(i + 1).Forward, u)
            Case KeyType.TBC_KEY : Return If(u = 0.0F, v0, CType(Nothing, Single?))
        End Select
        Return Nothing
    End Function

    ''' <summary>Point3 GenInterp (FO4 0x14174D500): as FloatAt, but equal times give u = 1 and past the last key the last key is
    ''' interpolated with itself (j = min(i + 1, n - 1)); per component the float key functions (0x14176E930 / 0x141770850 /
    ''' 0x14176B8B0).</summary>
    Friend Function Point3At(kg As KeyGroup(Of System.Numerics.Vector3), t As Single) As System.Numerics.Vector3?
        Dim k = kg.Keys
        If k Is Nothing OrElse k.Count = 0 Then Return Nothing
        Dim n = k.Count
        If n = 1 OrElse t = NegMax Then Return k(0).Value
        Dim i = 0
        While i + 1 <= n - 1 AndAlso t > k(i + 1).Time
            i += 1
        End While
        Dim j = Math.Min(i + 1, n - 1)
        Dim u = If(k(j).Time = k(i).Time, 1.0F, (t - k(i).Time) / (k(j).Time - k(i).Time))
        Dim a = k(i), b = k(j)
        Select Case kg.Interpolation
            Case KeyType.LINEAR_KEY
                Return New System.Numerics.Vector3((1.0F - u) * a.Value.X + u * b.Value.X, (1.0F - u) * a.Value.Y + u * b.Value.Y, (1.0F - u) * a.Value.Z + u * b.Value.Z)
            Case KeyType.CONST_KEY
                Return If(u >= 1.0F, b.Value, a.Value)
            Case KeyType.QUADRATIC_KEY
                Return New System.Numerics.Vector3(Hermite(a.Value.X, b.Value.X, a.Backward.X, b.Forward.X, u),
                                                   Hermite(a.Value.Y, b.Value.Y, a.Backward.Y, b.Forward.Y, u),
                                                   Hermite(a.Value.Z, b.Value.Z, a.Backward.Z, b.Forward.Z, u))
            Case KeyType.TBC_KEY
                Return If(u = 0.0F, a.Value, CType(Nothing, System.Numerics.Vector3?))
        End Select
        Return Nothing
    End Function

    ''' <summary>The exe's Hermite (0x141775840): a = D0 + D1; b = (D0 + D0) + D1; d = P1 - P0; c3 = d * 3 - b; c2 = a - (d + d);
    ''' r = ((c2 u + c3) u + D0) u + P0.</summary>
    Private Function Hermite(p0 As Single, p1 As Single, d0 As Single, d1 As Single, u As Single) As Single
        Dim a = d0 + d1
        Dim b = (d0 + d0) + d1
        Dim d = p1 - p0
        Dim c3 = d * 3.0F - b
        Dim c2 = a - (d + d)
        Return ((c2 * u + c3) * u + d0) * u + p0
    End Function

    ''' <summary>The interpolator's output for the controller's first update (NiFloatInterpolator::Update 0x14173D540 = SSE
    ''' 0x140F616A0; NiPoint3Interpolator 0x141762270): with keys, GenInterp(t); without, the pose; an INVALID result (-FLT_MAX)
    ''' writes nothing. A NiBlend* interpolator with no sequence writes nothing (count 0, 0x1417442D7). Manager-controlled (bit 5):
    ''' Update evaluates at the interpolator's initial lastTime (-FLT_MAX) =&gt; the pose. Inactive (bit 3 clear): nothing. A scaled
    ''' time equal to the stored -FLT_MAX: nothing (0x14224BA30 / 0x14157BED0 and the other three Updates).
    ''' <para>Why the manager-controlled path gives the pose (rev-06, scratchpad grupo-e/re_e3v2/ctor_lastTime.py): Update returns the
    ''' stored value +0x18 when t equals lastTime +0x10 (FO4 0x14173D558 / 0x141762288, SSE 0x140F616B8). +0x10 = -FLT_MAX from the
    ''' NiInterpolator constructor (FO4 0x14174B455, SSE 0x140F6CD85), which every NiFloatInterpolator / NiPoint3Interpolator
    ''' constructor reaches through NiKeyBasedInterpolator's (FO4 0x14174BEC0, SSE 0x140F6E9C0; float FO4 0x14173D3D0 / 0x14173D490 /
    ''' 0x14173D4F0 / 0x14173D910, SSE 0x140F61520 / 0x140F615F0 / 0x140F61650 / 0x140F61A70; point3 FO4 0x1417620D0 / 0x1417621B0 /
    ''' 0x141762220 / 0x141762740, SSE 0x140F84B50 / 0x140F84C30 / 0x140F84CA0 / 0x140F851C0). LoadBinary does not write it: float FO4
    ''' 0x14173D210 / SSE 0x140F61340 write +0x18 (the pose) and +0x20 (data), point3 FO4 0x141761F40 / SSE 0x140F849C0 write
    ''' +0x18..0x20 and +0x28; their base LoadBinary (FO4 0x14174BC00 -&gt; 0x14174B230 -&gt; 0x1416BAC60, SSE 0x140F6E5B0 -&gt; 0x140F6CBB0
    ''' -&gt; 0x140EDC790) is a bare ret, and LinkObject (FO4 0x14174BC10 -&gt; 0x1416BAC70) as well.</para></summary>
    Friend Function FloatAtRest(c As NiSingleInterpController, interp As NiInterpolator, data As NiFloatData) As Single?
        Dim fl = CInt(c.Flags.Value)
        Dim fi = TryCast(interp, NiFloatInterpolator)
        If fi Is Nothing Then Return Nothing
        If (fl And &H20) <> 0 Then Return If(fi.Value = NegMax, CType(Nothing, Single?), fi.Value)
        If (fl And 8) = 0 Then Return Nothing
        Dim t = ScaledTimeAtClockZero(c)
        If t = NegMax Then Return Nothing
        Dim v As Single? = If(data IsNot Nothing AndAlso data.Data.Keys IsNot Nothing AndAlso data.Data.Keys.Count > 0, FloatAt(data.Data, t), fi.Value)
        If v.HasValue AndAlso v.Value = NegMax Then Return Nothing
        Return v
    End Function

    Friend Function Point3AtRest(c As NiSingleInterpController, interp As NiInterpolator, data As NiPosData) As System.Numerics.Vector3?
        Dim fl = CInt(c.Flags.Value)
        Dim pi = TryCast(interp, NiPoint3Interpolator)
        If pi Is Nothing Then Return Nothing
        Dim invalid = Function(p As System.Numerics.Vector3) p.X = NegMax AndAlso p.Y = NegMax AndAlso p.Z = NegMax
        If (fl And &H20) <> 0 Then Return If(invalid(pi.Value), CType(Nothing, System.Numerics.Vector3?), pi.Value)
        If (fl And 8) = 0 Then Return Nothing
        Dim t = ScaledTimeAtClockZero(c)
        If t = NegMax Then Return Nothing
        Dim v = If(data IsNot Nothing AndAlso data.Data.Keys IsNot Nothing AndAlso data.Data.Keys.Count > 0, Point3At(data.Data, t), pi.Value)
        If v.HasValue AndAlso invalid(v.Value) Then Return Nothing
        Return v
    End Function

    ''' <summary>cos of a falloff angle as the effect controller writes it (vars 1 and 2: cos(v * 0.0174532924), 0x14224BB57).</summary>
    Friend Function FalloffCos(v As Single) As Single
        Return MathF.Cos(v * DegToRad)
    End Function
End Module

''' <summary>The material variables a shader-property controller can move, as the preview names them (the app's material field
''' each one replaces; the engine's destination offset from the update tables FO4 0x14224BB90 / 0x14224A940 / 0x14224B550, SSE
''' 0x14157C030 / 0x14157AD60 / 0x14157B9C0).</summary>
Friend Enum RestVariable
    UOffset
    VOffset
    UScale
    VScale
    ''' <summary>Effect var 0 (material +0x84 FO4 / +0x6C SSE) = the app's BaseColorScale.</summary>
    EffectEmissiveMultiple
    ''' <summary>Effect colour var 0 (material +0x48..0x50) = the app's BaseColor.</summary>
    EffectEmissiveColor
    FalloffStartAngle
    FalloffStopAngle
    FalloffStartOpacity
    FalloffStopOpacity
    ''' <summary>Lighting var 11 (prop+0xC8 FO4 / +0xF8 SSE) = EmittanceMult.</summary>
    LightingEmissiveMultiple
    ''' <summary>Lighting colour var 1 (*(prop+0xB8) FO4 / *(prop+0xF0) SSE) = EmittanceColor.</summary>
    LightingEmissiveColor
    ''' <summary>Lighting colour var 0 (material +0x38) = SpecularColor.</summary>
    SpecularColor
    ''' <summary>Lighting var 0 (+0x84) = RefractionPower.</summary>
    Refraction
    ''' <summary>Lighting var 8 (+0xD0 FO4, feature 1 only; +0xB0 SSE) = EnvironmentMappingMaskScale.</summary>
    EnvMapScale
    ''' <summary>Lighting var 9 (+0x88) = Smoothness / glossiness.</summary>
    Glossiness
    ''' <summary>Lighting var 10 (+0x8C) = SpecularMult.</summary>
    SpecularStrength
End Enum

''' <summary>A shape's material at rest: the variables its active shader controllers write at clock 0, nothing for the rest. The
''' alpha (lighting var 12, effect var 5) is NOT here: its owner is C2's PreviewAlphaRule (user decision: an animated alpha shows its
''' maximum).</summary>
Friend NotInheritable Class MaterialRest
    Public Shared ReadOnly None As New MaterialRest()
    Private ReadOnly _f As New Dictionary(Of RestVariable, Single)
    Private ReadOnly _c As New Dictionary(Of RestVariable, OpenTK.Mathematics.Vector3)

    Friend Sub SetValue(v As RestVariable, x As Single)
        _f(v) = x
    End Sub
    Friend Sub SetColour(v As RestVariable, x As OpenTK.Mathematics.Vector3)
        _c(v) = x
    End Sub

    ''' <summary>The value at rest, or <paramref name="material"/> when no controller writes it.</summary>
    Public Function Value(v As RestVariable, material As Single) As Single
        Dim x As Single
        Return If(_f.TryGetValue(v, x), x, material)
    End Function

    ''' <summary>The colour at rest (0..1 floats, as the controller writes them), or Nothing when no controller writes it.</summary>
    Public Function Colour(v As RestVariable) As OpenTK.Mathematics.Vector3?
        Dim x As OpenTK.Mathematics.Vector3
        Return If(_c.TryGetValue(v, x), x, CType(Nothing, OpenTK.Mathematics.Vector3?))
    End Function

    Public ReadOnly Property IsEmpty As Boolean
        Get
            Return _f.Count = 0 AndAlso _c.Count = 0
        End Get
    End Property

    ''' <summary>Does a controller write <paramref name="v"/> (a value or a colour)?</summary>
    Public Function Has(v As RestVariable) As Boolean
        Return _f.ContainsKey(v) OrElse _c.ContainsKey(v)
    End Function

    ''' <summary>THE material field each float variable replaces (the one map; the destinations in RestVariable's summaries). Glossiness
    ''' is Skyrim SE's raw glossiness (NifGlossiness, +0x88) and Fallout 4's smoothness (+0x88). Colours have no Single field: they go
    ''' through Colour.</summary>
    Friend Shared Function MaterialValue(mb As FO4UnifiedMaterial_Class, v As RestVariable, isSse As Boolean) As Single
        Select Case v
            Case RestVariable.UOffset : Return mb.UOffset
            Case RestVariable.VOffset : Return mb.VOffset
            Case RestVariable.UScale : Return mb.UScale
            Case RestVariable.VScale : Return mb.VScale
            Case RestVariable.EffectEmissiveMultiple : Return mb.BaseColorScale
            Case RestVariable.FalloffStartAngle : Return mb.FalloffStartAngle
            Case RestVariable.FalloffStopAngle : Return mb.FalloffStopAngle
            Case RestVariable.FalloffStartOpacity : Return mb.FalloffStartOpacity
            Case RestVariable.FalloffStopOpacity : Return mb.FalloffStopOpacity
            Case RestVariable.LightingEmissiveMultiple : Return mb.EmittanceMult
            Case RestVariable.Refraction : Return mb.RefractionPower
            Case RestVariable.EnvMapScale : Return mb.EnvironmentMappingMaskScale
            Case RestVariable.Glossiness : Return If(isSse, mb.NifGlossiness, mb.Smoothness)
            Case RestVariable.SpecularStrength : Return mb.SpecularMult
        End Select
        Throw New ArgumentOutOfRangeException(NameOf(v), v, "a colour variable has no Single field")
    End Function

    ''' <summary>The variable's name in the frame's notice (rev-04; UI in English).</summary>
    Friend Shared Function DisplayName(v As RestVariable, isSse As Boolean) As String
        Select Case v
            Case RestVariable.UOffset : Return "U offset"
            Case RestVariable.VOffset : Return "V offset"
            Case RestVariable.UScale : Return "U scale"
            Case RestVariable.VScale : Return "V scale"
            Case RestVariable.EffectEmissiveMultiple : Return "base color scale"
            Case RestVariable.EffectEmissiveColor : Return "base color"
            Case RestVariable.FalloffStartAngle : Return "falloff start angle"
            Case RestVariable.FalloffStopAngle : Return "falloff stop angle"
            Case RestVariable.FalloffStartOpacity : Return "falloff start opacity"
            Case RestVariable.FalloffStopOpacity : Return "falloff stop opacity"
            Case RestVariable.LightingEmissiveMultiple : Return "emittance multiple"
            Case RestVariable.LightingEmissiveColor : Return "emittance color"
            Case RestVariable.SpecularColor : Return "specular color"
            Case RestVariable.Refraction : Return "refraction power"
            Case RestVariable.EnvMapScale : Return "environment map scale"
            Case RestVariable.Glossiness : Return If(isSse, "glossiness", "smoothness")
            Case RestVariable.SpecularStrength : Return "specular strength"
        End Select
        Throw New ArgumentOutOfRangeException(NameOf(v))
    End Function
End Class
