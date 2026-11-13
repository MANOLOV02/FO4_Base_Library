Option Strict On
Option Explicit On

Imports OpenTK.Mathematics

''' <summary>THE FOG LAW OF SKYRIM SE'S SetupTechnique (Ola SSE S2 F2.3, rule 5: one seat for the Lighting and Effect programs - moved here
''' from SseLightingConstants): Lighting's 0x14154C010 and Effect's ST 0x1415568DA..9C1 do the same operations (s2_re.md 3.1: the off
''' branch 0x141556978..989 loads the same constant, the on branch the same r = 1 / (far - near) chain, FogNearColor.w the same
''' [0x1420D694C]); ParityGate sse-fog-law-shared holds the two classes' constants to the same bits, fog off and on.</summary>
Friend Module SseFogLaw

    ''' <summary>FogParam with the fog off: 0x14154C010 with fog near == far == 0 (+0x50 / +0x54, ucomiss 0x14154C0A2..B4) loads the
    ''' constant [0x141B44EA0] = (5e6, 0.1, 1.0, 0.0) (0x14154C0B6) and stores it to FogParam (0x14154C141). The VS then gives
    ''' min(sat(d 0.1 - 5e6) 1, 0) = 0 (rec 3901 L88..92): the fog factor COLOR1.w is 0 and the PS's lerp leaves the colour.</summary>
    Friend ReadOnly FogParamOff As New Vector4(BitConverter.UInt32BitsToSingle(&H4A989680UI), BitConverter.UInt32BitsToSingle(&H3DCCCCCDUI),
                                               1.0F, 0.0F)

    ''' <summary>The fog property 0x14154C010 reads: 0x1414EC040([0x1420D6970]) (0x14154C020..02F; null = the function writes nothing,
    ''' je 0x14154C1BB). Its near colour +0x38..+0x40, far colour +0x44..+0x4C, near +0x50, far +0x54, +0x84 and +0x88.</summary>
    Friend Structure SseFogProperty
        Public NearColour As Vector3
        Public FarColour As Vector3
        Public Near As Single
        Public Far As Single
        Public F84 As Single
        Public F88 As Single
    End Structure

    ''' <summary>THE FOG LAW 0x14154C010 (ST): with no fog property nothing is written (Nothing). FogParam: near == far == 0 (ucomiss /
    ''' jne both, 0x14154C0A2..0B4; an unordered compare does not jump either) = the constant [0x141B44EA0] (0x14154C0B6); else
    ''' r = 1.0 / (far - near) (0x14154C0BF..0CB) and (r near, r, +0x84, +0x88) (mulss and the shufps chain 0x14154C0CF..0EE), stored
    ''' at 0x14154C141. FogNearColor = (near colour, [0x1420D694C]) (0x14154C035..08E, store 0x14154C163); FogFarColor = (far colour,
    ''' the stack slot [rsp+0x4C] the function never writes: no VS reads .w) (store 0x14154C184); FogColor (PS) = FogNearColor's 16
    ''' bytes (xmm6, 0x14154C145 / 0x14154C1B3).</summary>
    Friend Function FogLaw(fog As SseFogProperty?) As (Param As Vector4, NearColor As Vector4, FarColor As Vector4, Color As Vector4)?
        If Not fog.HasValue Then Return Nothing
        Dim g = fog.Value
        Dim param As Vector4
        If (g.Far = 0.0F OrElse Single.IsNaN(g.Far)) AndAlso (g.Near = 0.0F OrElse Single.IsNaN(g.Near)) Then
            param = FogParamOff
        Else
            Dim r As Single = 1.0F / (g.Far - g.Near)
            param = New Vector4(r * g.Near, r, g.F84, g.F88)
        End If
        Dim nc As New Vector4(g.NearColour, PreviewModel.RenderableMesh.SseInvFrameBufferRangeWorld)
#If DEBUG Then
        ' ParityGate sse-effect-constants-law [fog]: FogNearColor.w other than [0x1420D694C].
        If Fo4GBufferConstants.GateMutantOn("sse-fog-nearw-one") Then nc.W = 1.0F
#End If
        Return (param, nc, New Vector4(g.FarColour, 0.0F), nc)
    End Function

    ''' <summary>The preview's fog property: the BSFogProperty ctor's (0x14155C1A0): colours from 0x14331D6F0 (.bss, no writer: 0),
    ''' near = far = 0 - the user decision "no fog" is the engine's off branch.</summary>
    Friend Function PreviewFog() As SseFogProperty
        Return New SseFogProperty With {.NearColour = FogPropertyColour, .FarColour = FogPropertyColour}
    End Function

    ''' <summary>The fog property's colours (+0x38..+0x4C) the preview has: the BSFogProperty ctor's (0x14155C1A0) NiColor 0x14331D6F0
    ''' (0x14155C231..25A; .bss, no writer, s1_re 9: 0, 0, 0).</summary>
    Friend ReadOnly FogPropertyColour As Vector3 = Vector3.Zero
End Module
