Imports OpenTK.Mathematics
Imports System.Runtime.Intrinsics

''' <summary>THE CONSTANTS OF THE SKYRIM SE LIGHTING PROGRAMS (g6), BY NAME: what BSLightingShader's SetupTechnique 0x141547D20 (cb0),
''' SetupMaterial 0x141548820 (cb1) and SetupGeometry 0x141549550 (cb2) write for each name the transcribed GLSL declares
''' (`uniform vec4 Name;` / `uniform vec4 Name[n];`, Tools\ShaderTranscription\sse-lighting), plus cb11 and the cb12 rows the blocks
''' read (asm2glsl.SSE_CB12). SkyrimSE.exe 1.7.104.0; RE: D:\WinTmp\rc\olasse\s1\s1_re.md 2, 4, 5, 6, 9, the F2.2 traces
''' (D:\WinTmp\rc\olasse\s1\f2\A\f22) and F2.2-L (D:\WinTmp\rc\olasse\s1\f2\A2\informe_L.md). Single arithmetic in the exe's order of
''' operations; one owner per value. Each function takes its inputs explicitly (the material / property fields by their offset, the
''' geometry's world NiTransform, the camera, the light, the technique, the d3dx9 route) and returns the vector(s) BY NAME (the
''' name's float 0 = .x); a name the technique does not reach is Nothing (the exe does not write it). The frame (F3) and the gates
''' (ParityGate sse-constants-law, ShadowGate --sse-vertex-constants / --sse-rig-passes / --sse-character-light) are fed the same
''' numbers. A component the exe does not write and no block reads is 0 here and says so (the census of reads:
''' sse_constants_oracle.py --uniforms). Every name of the closed list has its seat in <see cref="Seats"/>.
''' <para>The rig passes (P1, s1_re 5): pass 0 = the key with every term; passes 1..3 = the fills (the technique of light k, DirLight =
''' light k) with the ambient, EmitColor, CharacterLight and IBL at 0.</para></summary>
Friend Module SseLightingConstants

    ''' <summary>A geometry's world NiTransform as the engine keeps it at geom + 0x7C (NiAVObject world: the NiMatrix3 at +0x00..+0x20,
    ''' row-major m[i][j] at 4 (3 i + j), the translation at +0x24, the uniform scale at +0x30): the input of the model-space branches
    ''' (0x140FDEEC0 reads it, 0x141549AA4..AB1; the ambient reads the rotation, 0x141549BD5..E86). <see cref="Rotation"/> Row i = m[i].
    ''' The app builds it for the shape it draws (F3); the convention of Fo4GBufferConstants.EngineWorldRows is the same: engine row i =
    ''' the OpenTK row-vector model's column i (rotation x scale).</summary>
    Friend Structure SseNiTransform
        Public Rotation As Matrix3
        Public Translation As Vector3
        Public Scale As Single
    End Structure

    ''' <summary>SetupGeometry's model-space flag r15d: !SKINNED (technique bit 1, 0x1415495A4..5C1), cleared for the technique types 1,
    ''' 11 and 16 (0x14154971E, SseRenderPassLaw.LitPsWorldSpace). Read by the directional light (0x141549B7D), the ambient (0x141549BCB)
    ''' and the camera cb2[6] (0x14154A640).</summary>
    Friend Function LitModelSpace(litSkinned As Boolean, litType As Integer) As Boolean
        Return Not litSkinned AndAlso Not SseRenderPassLaw.LitPsWorldSpace(litType)
    End Function

    ''' <summary>The D3DXMATRIX 0x140FDEEC0 builds with rdx = 0 (every call of the constant laws passes xor edx, edx: 0x141549AAB,
    ''' 0x141567AEC) from the NiTransform at rcx (m[i][j] at 4 (3 i + j), t at +0x24, s at +0x30) into r8: inv = 1.0 / s (1.0 =
    ''' [0x141B5BE28], 0x140FDEEEB..EF3); row i = (inv m[i][0], inv m[i][1], inv m[i][2], 0) (0x140FDEFED..F0BD, the zeros
    ''' 0x140FDEFFB / F006 / F08A); row 3 = (-(inv ((t.y m[1][0] + t.x m[0][0]) + t.z m[2][0])), -(inv ((t.x m[0][1] + t.y m[1][1]) +
    ''' t.z m[2][1])), -(inv ((t.y m[1][2] + t.x m[0][2]) + t.z m[2][2])), 1) (0x140FDEF05..FD4 / FE1..003 / F03C..054; the sign
    ''' mask [0x141B5C730]; 1.0 at 0x140FDF098). The inverse of the world transform: the model-space branches' matrix.</summary>
    Friend Function InverseWorldMatrix(world As SseNiTransform) As Matrix4
        Dim inv As Single = 1.0F / world.Scale
        Dim m = world.Rotation
        Dim t = world.Translation
        Dim r3x As Single = -(((t.Y * m.M21 + t.X * m.M11) + t.Z * m.M31) * inv)
        Dim r3y As Single = -(((t.X * m.M12 + t.Y * m.M22) + t.Z * m.M32) * inv)
        Dim r3z As Single = -(((t.Y * m.M23 + t.X * m.M13) + t.Z * m.M33) * inv)
        Return New Matrix4(inv * m.M11, inv * m.M12, inv * m.M13, 0.0F,
                           inv * m.M21, inv * m.M22, inv * m.M23, 0.0F,
                           inv * m.M31, inv * m.M32, inv * m.M33, 0.0F,
                           r3x, r3y, r3z, 1.0F)
    End Function

    ''' <summary>The camera-relative D3DXMATRIX 0x1415170C0 builds from the NiTransform at rdx into rcx: row r = (s m[0][r], s m[1][r],
    ''' s m[2][r], 0) (s = +0x30; mulss of s by each element, 0x1415170C0..13B; the zeros 0x1415170EA / 114 / 138), row 3 = (t - eye, 1)
    ''' (subss of the frame eye [0x1420CFF5C..64], 0x141517140..178). <paramref name="eye"/> = that eye (cb12 row 40's source; the
    ''' previous-frame callers swap [0x1420CFF68..70] into it first, 0x14154980A..858).</summary>
    Friend Function WorldRelativeMatrix(t As SseNiTransform, eye As Vector3) As Matrix4
        Dim s As Single = t.Scale
        Dim m = t.Rotation
        Dim tx As Single = t.Translation.X - eye.X, ty As Single = t.Translation.Y - eye.Y, tz As Single = t.Translation.Z - eye.Z
#If DEBUG Then
        ' ParityGate sse-constants-law [geometry]: the eye not subtracted.
        If Fo4GBufferConstants.GateMutantOn("sse-world-noeye") Then tx = t.Translation.X : ty = t.Translation.Y : tz = t.Translation.Z
#End If
        Return New Matrix4(s * m.M11, s * m.M21, s * m.M31, 0.0F,
                           s * m.M12, s * m.M22, s * m.M32, 0.0F,
                           s * m.M13, s * m.M23, s * m.M33, 0.0F,
                           tx, ty, tz, 1.0F)
    End Function
    ''' <summary>The dimmer NiLight +0x134 of a light nobody dims: the NiLight ctor 0x140EED140 writes 1.0 (0x140EED180). The rig's light
    ''' is a NiDirectionalLight whose diffuse is the rig colour as PreviewModel.ResolveFrameLights resolves it (raw in SSE).</summary>
    Friend Const NiLightCtorDimmer As Single = 1.0F

    ''' <summary>ImageSpaceManager [0x1433D40A0] +0xE0 = its HDR block (+0xC8) +0x18 = HNAM Sunlight Scale of the moment, the factor
    ''' 0x141549AEE multiplies the dimmer by. The block is the ctor's (0x1414ED370 -> 0x141536A40: 1.8 at 0x141536A72) until
    ''' 0x1414EEC10 copies the frame's image space into it ([ISM+0xB0] = &amp;0x1431DF470, set by 0x1409A052E after the sky's blend
    ''' 0x1404158D0 - the game loop 0x1409A03E0). The preview's image space is its weather table row (PreviewImagingRow.SseWeather
    ''' .SunlightScale). WHICH value the rig takes is the caller's (F3): see the report of F2.2 (Fallout 4's rev-23 b gave its rig no
    ''' Sunlight Scale).</summary>
    Friend ReadOnly ImageSpaceCtorSunlightScale As Single = BitConverter.UInt32BitsToSingle(&H3FE66666UI)

    ''' <summary>DirLightColor (cb2, SG 0x141549AD9..B22): p = light+0x134 (dimmer) * [[0x1433D40A0]+0xE0] (0x141549AE5..AEE), then
    ''' (p diffuse.r, p diffuse.g, p diffuse.b) with the diffuse NiLight +0x11C..+0x124 (0x141549AFC..B19). .w is not written (rows 0..2)
    ''' and no PS reads it.</summary>
    Friend Function DirLightColor(diffuse As Vector3, dimmer As Single, sunlightScale As Single) As Vector4
        Dim p As Single = dimmer * sunlightScale
        Return New Vector4(p * diffuse.X, p * diffuse.Y, p * diffuse.Z, 0.0F)
    End Function

    ''' <summary>DirLightDirection (cb2, SG 0x141549B2D..BA1): d = -(NiDirectionalLight +0x140..+0x148) (xorps with the sign mask
    ''' [0x141B5C730], 0x141549B4D..B6E) - <paramref name="lightDirection"/> is that field, the direction the light travels (the rig's
    ''' Direction() negated); model space (<see cref="LitModelSpace"/>, 0x141549B7D): D3DXVec3TransformNormal(d, inverse world 0x140FDEEC0) then
    ''' D3DXVec3Normalize (0x141549B8E, 0x141549BA1); world space: D3DXVec3Normalize(d) (0x141549B9E..BA1) - SseD3dx9 on
    ''' <paramref name="route"/> (the DLL's dispatch). .w not written / not read.</summary>
    Friend Function DirLightDirection(lightDirection As Vector3, modelSpace As Boolean, world As SseNiTransform,
                                      route As SseD3dx9.SseD3dxRoute) As Vector4
        Dim d As New Vector3(-lightDirection.X, -lightDirection.Y, -lightDirection.Z)
        If modelSpace Then d = SseD3dx9.Vec3TransformNormal(route, d, InverseWorldMatrix(world))
        Return New Vector4(SseD3dx9.Vec3Normalize(route, d), 0.0F)
    End Function

    ''' <summary>P1 (s1_re 5): pass 0 is the key, 1..3 the fills.</summary>
    Friend Function IsKeyPass(rigPass As Integer) As Boolean
        Return rigPass = 0
    End Function

    ''' <summary>DirectionalAmbient[3] (cb2, SG 0x141549BC7..EFB) from the world's directional ambient G at 0x1420D6978..9A4: a NiMatrix3
    ''' g[0..8] and a vector g[9..11]; row r = (M[r], v[r]) and the PS adds dp4(row r, (N, 1)) to channel r (rec 4071 L119-121) - linear,
    ''' no power on it. World space: the rows as G holds them (0x141549E89..EFB with rcx = 0x1420D6978). Model space (0x141549BCB): row r
    ''' col c = (g[3r] m[0][c] + g[3r+1] m[1][c]) + g[3r+2] m[2][c] with the world ROTATION m (no scale, 0x141549BD5..E86), the vector
    ''' unchanged (0x141549E3D..E6B). G of the preview: the rig's hemisphere (rev-22, PreviewLightRig.HemisphereAmbient, the rule FO4 uses):
    ''' M[r] = (0, 0, slope_r) on world Z, v[r] = mean_r, from the rig's sky and ground as ResolveFrameLights resolves them for SSE (raw,
    ''' the ground already times its level: the PS does not raise it, unlike Fallout 4's). The fills (P1): 0.</summary>
    Friend Function DirectionalAmbient(sky As Vector3, ground As Vector3, modelSpace As Boolean, worldRotation As Matrix3,
                                       rigPass As Integer) As Vector4()
        If Not IsKeyPass(rigPass) Then
#If DEBUG Then
            ' ParityGate sse-constants-law [rig-p1]: the fill pass carries the key's ambient.
            If Not Fo4GBufferConstants.GateMutantOn("sse-ambient-fill") Then Return {Vector4.Zero, Vector4.Zero, Vector4.Zero}
#Else
            Return {Vector4.Zero, Vector4.Zero, Vector4.Zero}
#End If
        End If
        Dim g(11) As Single
        For c = 0 To 2
            Dim h = PreviewLightRig.HemisphereAmbient(sky(c), ground(c))
            g(3 * c + 2) = CSng(h.ZSlope)
            g(9 + c) = h.Mean
        Next
        Dim rows(2) As Vector4
        For r = 0 To 2
            If modelSpace Then
                Dim m = worldRotation
                rows(r) = New Vector4((g(3 * r) * m.M11 + g(3 * r + 1) * m.M21) + g(3 * r + 2) * m.M31,
                                      (g(3 * r) * m.M12 + g(3 * r + 1) * m.M22) + g(3 * r + 2) * m.M32,
                                      (g(3 * r) * m.M13 + g(3 * r + 1) * m.M23) + g(3 * r + 2) * m.M33, g(9 + r))
            Else
                rows(r) = New Vector4(g(3 * r), g(3 * r + 1), g(3 * r + 2), g(9 + r))
            End If
        Next
        Return rows
    End Function

    ''' <summary>EmitColor (cb2, SG 0x141549F3A..F9C; X17, s1_re 9): *[prop+0xF0] * [prop+0xF8], each channel one mulss. LoadBinary
    ''' 0x141518860 copies the NIF's emissive colour into *[prop+0xF0] only with SLSF1 bit 22 Own_Emit (0x1415188FD..916); otherwise it
    ''' keeps the ctor's NiColor (0x141518F4A..F64 from 0x14331D6F0, .bss with no writer: 0, 0, 0). The multiple +0xF8 always (the NIF's
    ''' emissive multiple; the app: its rest value). .w not written / not read. The fills (P1): 0.</summary>
    Friend Function EmitColor(ownEmit As Boolean, emissiveColour As Vector3, emissiveMultiple As Single, rigPass As Integer) As Vector4
        If Not IsKeyPass(rigPass) Then Return Vector4.Zero
        Dim c = If(ownEmit, emissiveColour, Vector3.Zero)
        Return New Vector4(c.X * emissiveMultiple, c.Y * emissiveMultiple, c.Z * emissiveMultiple, 0.0F)
    End Function

    ''' <summary>IBLParams (cb1, SM 0x14154936F..3C6): ([shader+0xCC], v.x, v.y, v.z) with v = [shader+0xF0] ? [shader+0xD0] :
    ''' [shader+0xE0] of the instance [0x143436400]. The ctor zeroes them (0x14154730C..321, 0x141547436); their only writers are the
    ''' per-frame update of Main 0x140656E60 and the console's ConfigureIBL: world state the preview does not run (s1_re 2.6) - 0 in every
    ''' pass.</summary>
    Friend Function IBLParams() As Vector4
        Return Vector4.Zero
    End Function

    ''' <summary>CharacterLightParams (cb1, SM 0x1415493C9..455, technique bit 22): [0x1420D68F6] ? [0x1420D69BC..C8] : 0
    ''' (0x141549437..455). The static initializer 0x1400FFA60 (CRT table 0x1417DE068) copies the Settings' .data values:
    ''' bCharacterLighting:Display 1 (0x1400FFA76..A8C), (fCharacterLightPrimaryLightIntensity 0.75, ...SecondaryLightIntensity 0.3,
    ''' ...LumScale 2.0, ...LumMax 0.75) (0x1400FFAAA..FB02). <paramref name="postOn"/> = the preview's post switch (P5): off = the
    ''' engine's off branch (0x141549452). The texture t11 is not this module's.</summary>
    Friend Function CharacterLightParams(postOn As Boolean) As Vector4
#If DEBUG Then
        ' ParityGate sse-constants-law [charlight]: the post switch ignored.
        If Fo4GBufferConstants.GateMutantOn("sse-charlight-postoff") Then postOn = True
#End If
        If Not postOn Then Return Vector4.Zero
#If DEBUG Then
        ' ParityGate sse-constants-law [charlight] and ShadowGate --sse-character-light [params]: a primary intensity other than the Setting's 0.75.
        If Fo4GBufferConstants.GateMutantOn("sse-charlight-params") Then Return New Vector4(1.0F, CharacterLightOn.Y, CharacterLightOn.Z, CharacterLightOn.W)
#End If
        Return CharacterLightOn
    End Function

    ''' <summary>The rig pass's CharacterLightParams (P1): the fills take the off branch.</summary>
    Friend Function CharacterLightParams(postOn As Boolean, rigPass As Integer) As Vector4
        Return CharacterLightParams(postOn AndAlso IsKeyPass(rigPass))
    End Function

    ''' <summary>[0x1420D69BC..C8] after 0x1400FFA60 (see <see cref="CharacterLightParams(Boolean)"/>).</summary>
    Friend ReadOnly CharacterLightOn As New Vector4(0.75F, 0.3F, 2.0F, 0.75F)
    ''' <summary>[0x1420D68F6] after 0x1400FFA60 = bCharacterLighting:Display .data 1.</summary>
    Friend Const CharacterLightingSetting As Byte = 1

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
        Return (param, nc, New Vector4(g.FarColour, 0.0F), nc)
    End Function

    ''' <summary>The preview's fog property: the BSFogProperty ctor's (0x14155C1A0): colours from 0x14331D6F0 (.bss, no writer: 0),
    ''' near = far = 0 - the user decision "no fog" is the engine's off branch.</summary>
    Friend Function PreviewFog() As SseFogProperty
        Return New SseFogProperty With {.NearColour = FogPropertyColour, .FarColour = FogPropertyColour}
    End Function

    ''' <summary>FogParam of the preview (user decision: no fog = the engine's branch near = far = 0).</summary>
    Friend Function FogParam() As Vector4
#If DEBUG Then
        ' ParityGate sse-constants-law [fog-off] / [exe]: a fog that is on (5000 .. 50000, the shape of 0x14154C0BF..EE).
        If Fo4GBufferConstants.GateMutantOn("sse-fog-on") Then Return New Vector4(1.0F / 45000.0F, 5000.0F / 45000.0F, 1.0F, 1.0F)
#End If
        Return FogLaw(PreviewFog()).Value.Param
    End Function

    ''' <summary>The fog property's colours (+0x38..+0x4C) the preview has: the BSFogProperty ctor's (0x14155C1A0) NiColor 0x14331D6F0
    ''' (0x14155C231..25A; .bss, no writer, s1_re 9: 0, 0, 0).</summary>
    Friend ReadOnly FogPropertyColour As Vector3 = Vector3.Zero

    ''' <summary>FogNearColor (VS cb0, ST 0x14154C105 / store 0x14154C163) = (fog +0x38, +0x3C, +0x40, invFrameBufferRange
    ''' [0x1420D694C]) (0x14154C035..08E).</summary>
    Friend Function FogNearColor() As Vector4
        Return FogLaw(PreviewFog()).Value.NearColor
    End Function

    ''' <summary>FogFarColor (VS cb0, ST 0x14154C10D / store 0x14154C184) = (fog +0x44, +0x48, +0x4C, [rsp+0x4C]): the last float is a
    ''' stack slot 0x14154C010 never writes; no VS reads FogFarColor.w (0 here).</summary>
    Friend Function FogFarColor() As Vector4
        Return FogLaw(PreviewFog()).Value.FarColor
    End Function

    ''' <summary>FogColor (PS cb0, ST 0x14154C11C / store 0x14154C1B3) = the same 16 bytes as FogNearColor (xmm6, 0x14154C145). The PS
    ''' reads .w (the tail's k, rec 4071 L139).</summary>
    Friend Function FogColor() As Vector4
        Return FogLaw(PreviewFog()).Value.Color
    End Function

    ''' <summary>NumLightNumShadowLight (cb2, SG 0x141549FA0..FE0): (float(technique bits 3..5), float(bits 6..8)) (shr / and 7,
    ''' cvtdq2ps). .zw not written / not read.</summary>
    Friend Function NumLightNumShadowLight(technique As UInteger) As Vector4
        Return New Vector4(CSng(CInt((technique >> 3) And 7UI)), CSng(CInt((technique >> 6) And 7UI)), 0.0F, 0.0F)
    End Function

    ''' <summary>PointLightPosition[7] / PointLightColor[7] (cb2): memset 0x70 bytes of each (0x14154A00A..023), then 0x14154BB90 writes
    ''' the first (technique bits 3..5) lights (0x14154A028). The preview has no point lights (D-9): 7 rows of 0.</summary>
    Friend Function PointLightRows() As Vector4()
        Return PointLights(Array.Empty(Of SsePointLight)(), 0, False, Nothing, 1.0F, Vector3.Zero, 0, Vector3.Zero, SseD3dx9.MachineRoute()).Position
    End Function

    ''' <summary>A pass light 0x14154BB90 reads (the list of 0x14154D070(pass) from +8, 0x14154BC13..C23): the BSLight r13 (+0x14 its
    ''' factor, +0x520 its mask channel) and its NiLight [r13+0x48] (+0x134 dimmer, +0x11C..+0x124 diffuse, +0xA0..+0xA8 world
    ''' translation, +0x128 radius).</summary>
    Friend Structure SsePointLight
        Public Factor As Single
        Public Dimmer As Single
        Public Diffuse As Vector3
        Public WorldPosition As Vector3
        Public Radius As Single
        Public MaskChannel As UInteger
    End Structure

    ''' <summary>THE POINT-LIGHT LAW 0x14154BB90 (called from SG 0x14154A080 with technique bits 3..5 &gt; 0; SG first memsets both arrays,
    ''' 0x14154A00A..023): ShadowLightMaskSelect = 0 (0x14154BBD1..BD6); for light i &lt; count: p = dimmer factor (0x14154BC2B..C33),
    ''' PointLightColor[i].xyz = p diffuse (0x14154BC39..C64; .w stays the memset 0); PointLightPosition[i]: model space (6th arg = 1,
    ''' 0x14154BC1B) = D3DXVec3TransformCoord(world position + o, the inverse world) (0x14154BC79..CF1) with .w = radius / scale
    ''' (0x14154BCF6..CFE; scale = 1.0 for types 1 / 16, else the geometry's +0xAC, 0x14154A034..06F); world space = (world position -
    ''' eye) + o, .w = radius (0x14154BD07..D93); o = [0x1433D3DB0] = 0x16 or 0x11 ? [[0x1420D68B0]+0x2E8] : [0x14331C458] (0x14154BC83..CEB /
    ''' 0x14154BD49..D86); for i &lt; the shadow count (technique bits 6..8) ShadowLightMaskSelect component i = float(BSLight +0x520)
    ''' (0x14154BD98..DB0).</summary>
    Friend Function PointLights(lights As SsePointLight(), shadowCount As Integer, modelSpace As Boolean, world As SseNiTransform?,
                                radiusScale As Single, eye As Vector3, mode3DB0 As Integer, ssn2E8 As Vector3,
                                route As SseD3dx9.SseD3dxRoute) As (Position As Vector4(), Color As Vector4(), MaskSelect As Vector4)
        Dim pos = New Vector4(6) {}, col = New Vector4(6) {}
        Dim mask(3) As Single
        Dim o = If(mode3DB0 = &H16 OrElse mode3DB0 = &H11, ssn2E8, SseUtilityConstants.ShadowRebaseOrigin)
        Dim inv = If(modelSpace AndAlso world.HasValue, InverseWorldMatrix(world.Value), Matrix4.Identity)
        For i = 0 To lights.Length - 1
            Dim L = lights(i)
            Dim p As Single = L.Dimmer * L.Factor
            col(i) = New Vector4(p * L.Diffuse.X, p * L.Diffuse.Y, p * L.Diffuse.Z, 0.0F)
            If modelSpace Then
                Dim t As New Vector3(L.WorldPosition.X + o.X, L.WorldPosition.Y + o.Y, L.WorldPosition.Z + o.Z)
                Dim w As Single = L.Radius / radiusScale
#If DEBUG Then
                ' ParityGate sse-constants-law [points]: the model-space radius not divided by the scale.
                If Fo4GBufferConstants.GateMutantOn("sse-points-noscale") Then w = L.Radius
#End If
                pos(i) = New Vector4(SseD3dx9.Vec3TransformCoord(route, t, inv), w)
            Else
                pos(i) = New Vector4((L.WorldPosition.X - eye.X) + o.X, (L.WorldPosition.Y - eye.Y) + o.Y, (L.WorldPosition.Z - eye.Z) + o.Z, L.Radius)
            End If
            If i < shadowCount Then mask(i) = CSng(CLng(L.MaskChannel))
        Next
        Return (pos, col, New Vector4(mask(0), mask(1), mask(2), mask(3)))
    End Function

    ''' <summary>ShadowLightMaskSelect (cb2): written only inside 0x14154BB90 (0x14154BBD1..BD6 zero, then the lights'), which runs only
    ''' with a point light (0x14154A028 je). Without point lights it is not written and the PS reads it only inside the loop over them
    ''' (rec 4071 L90..98), which runs 0 times: 0 here.</summary>
    Friend Function ShadowLightMaskSelect() As Vector4
        Return PointLights(Array.Empty(Of SsePointLight)(), 0, False, Nothing, 1.0F, Vector3.Zero, 0, Vector3.Zero, SseD3dx9.MachineRoute()).MaskSelect
    End Function

    ''' <summary>VPOSOffset (lighting PS cb0, ST 0x14154875D..78E, with technique bit 14 / bit 13 and 0x1C0, 0x1415486C4..DA): (1 / W,
    ''' 1 / H, 0, 0), W / H the render target's size [0x14333561C] / [0x143335620] (cvtsi2ss of the zero-extended dword, divss of 1.0
    ''' [0x141B5BE28]; .zw = r12 = 0, 0x141547DAF / 0x141548780). The utility PS writes the same law (ST 0x141566992..9EB: the target size
    ''' 0x1410154E0 / 0x141015520, .zw stored 0 at 0x1415669D5) - SseUtilityConstants reads this one.</summary>
    Friend Function VposOffset(width As Integer, height As Integer) As Vector4
        Return New Vector4(1.0F / CSng(CLng(CUInt(width))), 1.0F / CSng(CLng(CUInt(height))), 0.0F, 0.0F)
    End Function

    ''' <summary>fLightingOutputColourClampPostEnv:General (Setting 0x1420D8D10, .data 1.0), copied by the ctor 0x141547210 to
    ''' [0x143436410] (0x141547454..47C).</summary>
    Friend Const SseLightingOutputClampPostEnv As Single = 1.0F

    ''' <summary>ColourOutputClamp (PS cb0, ST 0x141548622..656): ([0x14343640C], [0x143436410], [0x143436414], 0) = (PostLit, PostEnv,
    ''' PostSpec, 0); the PS reads .x / .z (the tail's C).</summary>
    Friend Function ColourOutputClamp() As Vector4
        Return New Vector4(PreviewModel.RenderableMesh.SseLightingOutputClampPostLit, SseLightingOutputClampPostEnv,
                           PreviewModel.RenderableMesh.SseLightingOutputClampPostSpec, 0.0F)
    End Function

    ''' <summary>cb11_AlphaTestRef: cb11[0].x = the pass's alpha reference, SseRenderPassLaw.SubgroupThreshold (the flush 0x141010DE0
    ''' writes only .x, 0x14101118D..1AB).</summary>
    Friend Function Cb11AlphaTestRef(s As SseRenderPassLaw.SseShapeInputs) As Vector4
        Return New Vector4(SseRenderPassLaw.SubgroupThreshold(s), 0.0F, 0.0F, 0.0F)
    End Function

    ''' <summary>scb12_view[4] (cb12 rows 0..3, the writer 0x14100AE80 0x14100AEA4..AF69 from 0x1420CFFB0 transposed): the camera-relative
    ''' view, the frame's rows the app already builds (Fo4GBufferConstants.EngineFrameRows .View, S0-C) and row 3 = (0, 0, 0, 1) (the view
    ''' is built without translation, 0x14101D47F..487).</summary>
    Friend Function Cb12View(fr As Fo4FrameRows) As Vector4()
        Return {fr.View(0), fr.View(1), fr.View(2), New Vector4(0.0F, 0.0F, 0.0F, 1.0F)}
    End Function

    ''' <summary>scb12_viewproj[4] (rows 8..11, 0x14100AF6E..B003: view x projection 0x1420D0030 transposed) = EngineFrameRows .ViewProj.</summary>
    Friend Function Cb12ViewProj(fr As Fo4FrameRows) As Vector4()
        Return fr.ViewProj
    End Function

    ''' <summary>scb12_viewproj_nooffset[4] (cb12 rows 12..15, read only by the motion-vector code of the Lighting PS o1, under #ifdef
    ''' ENGINE_MOTION_VECTORS): 0x1420D00B0 transposed (the writer 0x14100AFC8..B044) = the camera entry +0x140 (0x14101E7F0
    ''' 0x14101E983..9A7), which 0x14101E4E0 copies from the entry's view x projection +0xC0 (0x14101D400's product, 0x14101E64D..684)
    ''' BEFORE the sub-pixel offset rewrites +0xC0 (0x14101E6A8..78F): the view x projection without the offset = EngineFrameRows
    ''' .ViewProj, the same product rows 8..11 take (the app's frame has no offset).</summary>
    Friend Function Cb12ViewProjNoOffset(fr As Fo4FrameRows) As Vector4()
        Return fr.ViewProj
    End Function

    ''' <summary>One call of a Skyrim SE caller on a camera entry (EngineCameraMotionEntry.Run, the order of <paramref name="caller"/>):
    ''' 0x14101E4E0 writes +0x140 = the view x projection before the offset, whose transpose is rows 12..15 (Cb12ViewProjNoOffset), and
    ''' +0x260 = the eye, row 40 (Cb12Eye .xyz); then the rotation 0x14101CA60.</summary>
    Friend Sub SseCameraFrame(entry As EngineCameraMotionEntry, fr As Fo4FrameRows, caller As EngineCameraCaller)
        entry.Run(caller, EngineCameraMotionEntry.TransposeRows(Cb12ViewProjNoOffset(fr)), Cb12Eye(fr).Xyz)
    End Sub

    ''' <summary>scb12_prevviewproj_nooffset[4] (cb12 rows 16..19, read only by the motion-vector code of the Lighting PS o1, under #ifdef
    ''' ENGINE_MOTION_VECTORS): 0x1420D00F0 transposed (0x14100B007..B08C) = the camera entry +0x180 (0x14101E7F0 0x14101E959..9A7) after
    ''' the frame's rotation 0x14101CA60 = the view x projection before the offset of the frame before (EngineCameraMotionEntry; the
    ''' independent verification D:\WinTmp\rc\olasse\s1\f2\A2\V\verif_cb12_engine_no_writer.md refuted the engine-no-writer of
    ''' this row: 0x14101EC30 is an unreferenced copy of the rotation). Nothing on a new array entry's first frame: the array buffer's
    ''' bytes (EngineCameraMotionEntry.NewSseListEntry; the engine-no-writer seat "cb12 entry +0x180..+0x1BF first frame",
    ''' EngineCameraMotionEntry.FirstFrameSeats); 0 for the default entry (SseDefaultEntry).</summary>
    Friend Function Cb12PrevViewProjNoOffset(entry As EngineCameraMotionEntry) As Vector4()
        Return EngineCameraMotionEntry.TransposeRows(entry.Previous)
    End Function

    ''' <summary>scb12_preveye (row 41, read only by the motion-vector code of the Lighting VS): .xyz = [0x1420CFF68..70] (the writer
    ''' 0x14100B273..2C2) = the camera entry +0x278 (0x14101E7F0 0x14101EA88..AB0) after the frame's rotation 0x14101CA60 = the eye of the
    ''' frame before (EngineCameraMotionEntry). On a new array entry's first frame it is 0 BY LAW: 0x14101CB50 zeroes +0x260..+0x283
    ''' (0x14101CC3B..CC57) before the rotation copies +0x26C; 0 also for the default entry. .w = [0x1420D04F8] only with the dynamic resolution (0x14100B2F1..30C), off: SseSaoConstants.Cb12_41 .w, the
    ''' one copy (the SAO reads only that lane).</summary>
    Friend Function Cb12PrevEye(entry As EngineCameraMotionEntry) As Vector4
        Return New Vector4(entry.PreviousEye.Value, SseSaoConstants.Cb12_41.W)
    End Function

    ''' <summary>PreviousWorld[3] (VS cb2, read only by the motion-vector code): 0x14154B6A0 with r8d = 1 - the law of World over the
    ''' geometry's world transform relative to the PREVIOUS eye [0x1420CFF68..70] (Cb12PrevEye's .xyz, the eye of the frame before: the
    ''' camera entry's rotation, EngineCameraMotionEntry; SetupGeometry swaps it in for
    ''' the frame eye around the call, 0x14154980A..877 / 0x141549A1F..A9B). The transform: the current one (geom +0x7C) for the types
    ''' 9 / 13 / 15 / 18 (0x141549812, the call 0x141549860) and when the call's flags have bit 4 ([rbp + 0x1F0] AND 0x10,
    ''' 0x141549A2C..A4B), else the previous one (geom +0xB0, 0x141549A33). Written when World is (LitWorldWritten).</summary>
    Friend Function PreviousWorld(technique As UInteger, callFlags As UInteger, current As SseNiTransform, previous As SseNiTransform,
                                  previousEye As Vector3) As Vector4()
        Dim ty = LitType(technique)
        Dim cur = ty = 9 OrElse ty = &HD OrElse ty = &HF OrElse ty = &H12 OrElse (callFlags And &H10UI) <> 0UI
        Return World(If(cur, current, previous), previousEye)
    End Function

    ''' <summary>scb12_eye (row 40 = [0x1420CFF5C..64], w 0; 0x14100B28D..2D4) = EngineFrameRows .Eye.</summary>
    Friend Function Cb12Eye(fr As Fo4FrameRows) As Vector4
        Return fr.Eye
    End Function

    ''' <summary>shufps dst, src, imm: (dst[imm 1:0], dst[imm 3:2], src[imm 5:4], src[imm 7:6]).</summary>
    Private Function Shufps(d As Vector128(Of Single), s As Vector128(Of Single), imm As Integer) As Vector128(Of Single)
        Return Vector128.Create(d.GetElement(imm And 3), d.GetElement((imm >> 2) And 3), s.GetElement((imm >> 4) And 3), s.GetElement((imm >> 6) And 3))
    End Function

    ''' <summary>[0x141B54CC0] = (1.0, 1.0, 1.0, 1.0), the numerator of 0x1404AE3F8.</summary>
    Friend Const ExeInverseOne As Single = 1.0F

    ''' <summary>SkyrimSE.exe 0x1404AE130, the 4x4 inverse the cb12 writer calls for rows 32..35 (0x14100B1D8) - instruction by
    ''' instruction (generated from its disassembly by D:\WinTmp\rc\olasse\s1\f2\A2\L\scripts\gen_inverse_vb.py): the rows arrive in
    ''' xmm0..3 and leave in xmm0..3 (vectorcall); mulps / subps / addps / divps are exact IEEE lane operations, so the bits are the
    ''' exe's. Left out because they produce no value: the xmm6..15 saves / restores (0x1404AE13A..18C, 0x1404AE3CA..3F3); the
    ''' determinant store [rcx] (0x1404AE3B3..3B8) does not run at this call (rcx = 0: xor ecx, ecx at 0x14100B17D, unchanged up to
    ''' 0x14100B1D8).</summary>
    Friend Function ExeMatrixInverse(r0 As Vector4, r1 As Vector4, r2 As Vector4, r3 As Vector4) As Vector4()
        Dim x0 = Vector128.Create(r0.X, r0.Y, r0.Z, r0.W), x1 = Vector128.Create(r1.X, r1.Y, r1.Z, r1.W)
        Dim x2 = Vector128.Create(r2.X, r2.Y, r2.Z, r2.W), x3 = Vector128.Create(r3.X, r3.Y, r3.Z, r3.W)
        Dim x4, x5, x6, x7, x8, x9, x10, x11, x12, x13, x14 As Vector128(Of Single)
        x12 = x0   ' 0x1404AE15B
        x12 = Shufps(x12, x1, &H44)   ' 0x1404AE15F
        x13 = x0   ' 0x1404AE16A
        x13 = Shufps(x13, x1, &HEE)   ' 0x1404AE16E
        x1 = x2   ' 0x1404AE173
        x1 = Shufps(x1, x3, &H44)   ' 0x1404AE176
        x10 = x13   ' 0x1404AE17A
        x2 = Shufps(x2, x3, &HEE)   ' 0x1404AE17E
        x10 = Shufps(x10, x2, &H88)   ' 0x1404AE182
        x13 = Shufps(x13, x2, &HDD)   ' 0x1404AE187
        x0 = x13   ' 0x1404AE191
        x0 = Shufps(x0, x13, &H50)   ' 0x1404AE195
        x14 = x12   ' 0x1404AE19A
        x12 = Shufps(x12, x1, &HDD)   ' 0x1404AE19E
        x11 = x13   ' 0x1404AE1A3
        x14 = Shufps(x14, x1, &H88)   ' 0x1404AE1A7
        x9 = x12   ' 0x1404AE1AC
        x11 = Shufps(x11, x13, &HEE)   ' 0x1404AE1B0
        x1 = x10   ' 0x1404AE1B5
        x1 = Shufps(x1, x10, &HEE)   ' 0x1404AE1B9
        x4 = x13   ' 0x1404AE1BE
        x1 = Vector128.Multiply(x1, x0)   ' 0x1404AE1C2
        x0 = x10   ' 0x1404AE1C5
        x0 = Shufps(x0, x10, &H50)   ' 0x1404AE1C9
        x11 = Vector128.Multiply(x11, x0)   ' 0x1404AE1CE
        x0 = x12   ' 0x1404AE1D2
        x0 = Shufps(x0, x12, &H50)   ' 0x1404AE1D6
        x9 = Shufps(x9, x12, &HEE)   ' 0x1404AE1DB
        x4 = Shufps(x4, x12, &HDD)   ' 0x1404AE1E0
        x11 = Vector128.Subtract(x11, x1)   ' 0x1404AE1E5
        x1 = x14   ' 0x1404AE1E9
        x1 = Shufps(x1, x14, &HEE)   ' 0x1404AE1ED
        x1 = Vector128.Multiply(x1, x0)   ' 0x1404AE1F2
        x0 = x14   ' 0x1404AE1F5
        x0 = Shufps(x0, x14, &H50)   ' 0x1404AE1F9
        x9 = Vector128.Multiply(x9, x0)   ' 0x1404AE1FE
        x7 = x11   ' 0x1404AE202
        x0 = x13   ' 0x1404AE206
        x2 = x11   ' 0x1404AE20A
        x0 = Shufps(x0, x12, &H88)   ' 0x1404AE20E
        x9 = Vector128.Subtract(x9, x1)   ' 0x1404AE213
        x1 = x10   ' 0x1404AE217
        x1 = Shufps(x1, x14, &HDD)   ' 0x1404AE21B
        x1 = Vector128.Multiply(x1, x0)   ' 0x1404AE220
        x0 = x10   ' 0x1404AE223
        x0 = Shufps(x0, x14, &H88)   ' 0x1404AE227
        x4 = Vector128.Multiply(x4, x0)   ' 0x1404AE22C
        x8 = x9   ' 0x1404AE22F
        x0 = x12   ' 0x1404AE233
        x3 = x9   ' 0x1404AE237
        x0 = Shufps(x0, x12, &H49)   ' 0x1404AE23B
        x4 = Vector128.Subtract(x4, x1)   ' 0x1404AE240
        x7 = Shufps(x7, x4, &H5D)   ' 0x1404AE243
        x5 = x7   ' 0x1404AE247
        x8 = Shufps(x8, x4, &HFD)   ' 0x1404AE24A
        x6 = x8   ' 0x1404AE24F
        x5 = Shufps(x5, x11, &H32)   ' 0x1404AE253
        x5 = Vector128.Multiply(x5, x0)   ' 0x1404AE258
        x0 = x14   ' 0x1404AE25B
        x0 = Shufps(x0, x14, &H12)   ' 0x1404AE25F
        x7 = Shufps(x7, x11, &H99)   ' 0x1404AE264
        x7 = Vector128.Multiply(x7, x0)   ' 0x1404AE269
        x0 = x13   ' 0x1404AE26C
        x0 = Shufps(x0, x13, &H49)   ' 0x1404AE270
        x6 = Shufps(x6, x9, &H32)   ' 0x1404AE275
        x6 = Vector128.Multiply(x6, x0)   ' 0x1404AE27A
        x0 = x10   ' 0x1404AE27D
        x0 = Shufps(x0, x10, &H12)   ' 0x1404AE281
        x8 = Shufps(x8, x9, &H99)   ' 0x1404AE286
        x8 = Vector128.Multiply(x8, x0)   ' 0x1404AE28B
        x2 = Shufps(x2, x4, &H4)   ' 0x1404AE28F
        x3 = Shufps(x3, x4, &HA4)   ' 0x1404AE293
        x1 = x11   ' 0x1404AE297
        x0 = x12   ' 0x1404AE29B
        x1 = Shufps(x1, x2, &H93)   ' 0x1404AE29F
        x0 = Shufps(x0, x12, &H9E)   ' 0x1404AE2A3
        x1 = Vector128.Multiply(x1, x0)   ' 0x1404AE2A8
        x0 = x14   ' 0x1404AE2AB
        x0 = Shufps(x0, x14, &H7B)   ' 0x1404AE2AF
        x12 = Shufps(x12, x12, &H33)   ' 0x1404AE2B4
        x5 = Vector128.Subtract(x5, x1)   ' 0x1404AE2B9
        x1 = x11   ' 0x1404AE2BC
        x1 = Shufps(x1, x2, &H26)   ' 0x1404AE2C0
        x1 = Vector128.Multiply(x1, x0)   ' 0x1404AE2C4
        x0 = x13   ' 0x1404AE2C7
        x0 = Shufps(x0, x13, &H9E)   ' 0x1404AE2CB
        x13 = Shufps(x13, x13, &H33)   ' 0x1404AE2D0
        x7 = Vector128.Subtract(x7, x1)   ' 0x1404AE2D5
        x1 = x9   ' 0x1404AE2D8
        x1 = Shufps(x1, x3, &H93)   ' 0x1404AE2DC
        x1 = Vector128.Multiply(x1, x0)   ' 0x1404AE2E0
        x0 = x10   ' 0x1404AE2E3
        x0 = Shufps(x0, x10, &H7B)   ' 0x1404AE2E7
        x10 = Shufps(x10, x10, &H8D)   ' 0x1404AE2EC
        x2 = x7   ' 0x1404AE2F1
        x6 = Vector128.Subtract(x6, x1)   ' 0x1404AE2F4
        x1 = x9   ' 0x1404AE2F7
        x1 = Shufps(x1, x3, &H26)   ' 0x1404AE2FB
        x1 = Vector128.Multiply(x1, x0)   ' 0x1404AE2FF
        x0 = x11   ' 0x1404AE302
        x0 = Shufps(x0, x4, &H4A)   ' 0x1404AE306
        x0 = Shufps(x0, x0, &H2C)   ' 0x1404AE30A
        x12 = Vector128.Multiply(x12, x0)   ' 0x1404AE30E
        x0 = x14   ' 0x1404AE312
        x0 = Shufps(x0, x14, &H8D)   ' 0x1404AE316
        x8 = Vector128.Subtract(x8, x1)   ' 0x1404AE31B
        x11 = Shufps(x11, x4, &H4C)   ' 0x1404AE31F
        x1 = x9   ' 0x1404AE324
        x1 = Shufps(x1, x4, &HEA)   ' 0x1404AE328
        x9 = Shufps(x9, x4, &HEC)   ' 0x1404AE32C
        x4 = x6   ' 0x1404AE331
        x1 = Shufps(x1, x1, &H2C)   ' 0x1404AE334
        x13 = Vector128.Multiply(x13, x1)   ' 0x1404AE338
        x1 = x5   ' 0x1404AE33C
        x11 = Shufps(x11, x11, &H93)   ' 0x1404AE33F
        x5 = Vector128.Subtract(x5, x12)   ' 0x1404AE344
        x11 = Vector128.Multiply(x11, x0)   ' 0x1404AE348
        x0 = x14   ' 0x1404AE34C
        x9 = Shufps(x9, x9, &H93)   ' 0x1404AE350
        x1 = Vector128.Add(x1, x12)   ' 0x1404AE355
        x9 = Vector128.Multiply(x9, x10)   ' 0x1404AE359
        x4 = Vector128.Add(x4, x13)   ' 0x1404AE35D
        x6 = Vector128.Subtract(x6, x13)   ' 0x1404AE361
        x2 = Vector128.Subtract(x2, x11)   ' 0x1404AE365
        x1 = Shufps(x1, x5, &HD8)   ' 0x1404AE369
        x5 = x8   ' 0x1404AE36D
        x1 = Shufps(x1, x1, &HD8)   ' 0x1404AE371
        x5 = Vector128.Subtract(x5, x9)   ' 0x1404AE375
        x0 = Vector128.Multiply(x0, x1)   ' 0x1404AE379
        x7 = Vector128.Add(x7, x11)   ' 0x1404AE37C
        x4 = Shufps(x4, x6, &HD8)   ' 0x1404AE380
        x8 = Vector128.Add(x8, x9)   ' 0x1404AE384
        x4 = Shufps(x4, x4, &HD8)   ' 0x1404AE388
        x14 = Shufps(x14, x0, &H40)   ' 0x1404AE38C
        x2 = Shufps(x2, x7, &HD8)   ' 0x1404AE391
        x14 = Vector128.Add(x14, x0)   ' 0x1404AE395
        x5 = Shufps(x5, x8, &HD8)   ' 0x1404AE399
        x2 = Shufps(x2, x2, &HD8)   ' 0x1404AE39E
        x5 = Shufps(x5, x5, &HD8)   ' 0x1404AE3A2
        x0 = Shufps(x0, x14, &H30)   ' 0x1404AE3A6
        x0 = Vector128.Add(x0, x14)   ' 0x1404AE3AB
        x0 = Shufps(x0, x0, &HAA)   ' 0x1404AE3AF
        x3 = Vector128.Create(ExeInverseOne)' 0x1404AE3BB movaps [0x141B54CC0] = (1, 1, 1, 1)
        x3 = Vector128.Divide(x3, x0)   ' 0x1404AE3F8
        x0 = x3   ' 0x1404AE3FB
        x0 = Vector128.Multiply(x0, x1)   ' 0x1404AE3FE
        x1 = x3   ' 0x1404AE401
        x1 = Vector128.Multiply(x1, x2)   ' 0x1404AE404
        x2 = x3   ' 0x1404AE407
        x2 = Vector128.Multiply(x2, x4)   ' 0x1404AE40A
        x3 = Vector128.Multiply(x3, x5)   ' 0x1404AE40D
        Return {New Vector4(x0.GetElement(0), x0.GetElement(1), x0.GetElement(2), x0.GetElement(3)),
                New Vector4(x1.GetElement(0), x1.GetElement(1), x1.GetElement(2), x1.GetElement(3)),
                New Vector4(x2.GetElement(0), x2.GetElement(1), x2.GetElement(2), x2.GetElement(3)),
                New Vector4(x3.GetElement(0), x3.GetElement(1), x3.GetElement(2), x3.GetElement(3))}
    End Function

    ''' <summary>scb12_invviewproj[4] (rows 32..35): the cb12 writer loads the view x projection [0x1420D0030..006F] into xmm0..3
    ''' (0x14100B18D..1C3), inverts it with <see cref="ExeMatrixInverse"/> (0x14100B1D8) and stores its transpose (shufps 0x44 / 0xEE /
    ''' 0x88 / 0xDD, 0x14100B1DD..229; rows at [rbp+0x140..0x170], 0x14100B21B..242). That matrix is the transpose of the rows 8..11
    ''' (<see cref="Cb12ViewProj"/>: row i = column i of [0x1420D0030]): its row i = the components i of the four rows 8..11.</summary>
    Friend Function Cb12InvViewProj(fr As Fo4FrameRows) As Vector4()
#If DEBUG Then
        ' ParityGate sse-constants-law [inverse]: OpenTK's inverse instead of the exe's.
        If Fo4GBufferConstants.GateMutantOn("sse-invviewproj-opentk") Then
            Dim m = fr.AppInvViewProj
            Return {m.Column0, m.Column1, m.Column2, m.Column3}
        End If
#End If
        Dim v = fr.ViewProj
        Dim inv = ExeMatrixInverse(New Vector4(v(0).X, v(1).X, v(2).X, v(3).X), New Vector4(v(0).Y, v(1).Y, v(2).Y, v(3).Y),
                                   New Vector4(v(0).Z, v(1).Z, v(2).Z, v(3).Z), New Vector4(v(0).W, v(1).W, v(2).W, v(3).W))
        Return {New Vector4(inv(0).X, inv(1).X, inv(2).X, inv(3).X), New Vector4(inv(0).Y, inv(1).Y, inv(2).Y, inv(3).Y),
                New Vector4(inv(0).Z, inv(1).Z, inv(2).Z, inv(3).Z), New Vector4(inv(0).W, inv(1).W, inv(2).W, inv(3).W)}
    End Function
    ''' <summary>scb12_42 (row 42, 0x14100B314..358): .x = 1 / fGamma (Shader_Base_Class.DisplayExponent(True), the one owner), .y = (flags
    ''' AND 2) ? 0 : 1 = 1 in the world render (Shader_Class SseLitTailSource), .z = (flags AND 4) ? 1 : 0 = the pass's TailZ
    ''' (SseRenderPassLaw), .w = (flags AND 0x80) ? 1 : 0 - g6 / g7 read only .y / .z (0 here).</summary>
    Friend Function Cb12Row42(tailZ As Single) As Vector4
        Return New Vector4(Shader_Base_Class.DisplayExponent(True), 1.0F, tailZ, 0.0F)
    End Function

    ''' <summary>scb12_43 / scb12_44 (rows 43 / 44, the dynamic resolution, off): SseSaoConstants' one copy.</summary>
    Friend Function Cb12Row43() As Vector4
        Return SseSaoConstants.Cb12_43
    End Function

    Friend Function Cb12Row44() As Vector4
        Return SseSaoConstants.Cb12_44
    End Function

    ''' <summary>SnowRimLightParameters (cb1, SM 0x14154929E..2B7): SseRenderPassLaw's one copy.</summary>
    Friend Function SnowRimLightParameters() As Vector4
        Return SseRenderPassLaw.SnowRimLightParameters
    End Function

    ' =============================================================================================================================
    ' SetupMaterial 0x141548820 (cb1 of the PS, and the material rows of the VS cb1): the material's fields by technique type.
    ' rbp = the shader, rbx = the material (rdx); the type = (technique [rbp+0x94] >> 24) AND 0x3F (0x1415488DA..8FD), dispatched
    ' through the jump table 0x1415494E4 (type - 1, 0x141548900..916).
    ' =============================================================================================================================

    ''' <summary>The lighting technique type: bits 24..29 (shr 0x18 / and 0x3F, 0x1415488F7..8FD and 0x1415496C2..6C7).</summary>
    Friend Function LitType(technique As UInteger) As Integer
        Return CInt((technique >> 24) And &H3FUI)
    End Function

    ''' <summary>EnvmapData (PS cb1, SM): the envmap material (types 1 / 16, 0x1415489BB..9DE then 0x141548F90..F9A) = (material
    ''' +0xB0, material +0xA8 (the envmap mask texture) != null ? 1.0 [0x141B5BE28] : 0); the multilayer parallax material (type 11,
    ''' 0x141548F6D..F9A) = (material +0xC8, material +0xB0 != null ? 1.0 : 0). Other types do not write it. .zw not written (the
    ''' blocks read .xy).</summary>
    Friend Function EnvmapData(litType As Integer, scale As Single, maskTexturePresent As Boolean) As Vector4?
        If litType <> 1 AndAlso litType <> &H10 AndAlso litType <> &HB Then Return Nothing
        Return New Vector4(scale, If(maskTexturePresent, 1.0F, 0.0F), 0.0F, 0.0F)
    End Function

    ''' <summary>LeftEyeCenter / RightEyeCenter (VS cb1, the eye material, type 16, 0x141548953..9B8): left = (material +0xB4, +0xB8,
    ''' +0xBC) (0x141548978..984), right = (+0xC0, +0xC4, +0xC8) (0x1415489AC..9B8). .w not written (the blocks read .xyz).</summary>
    Friend Function EyeCenters(litType As Integer, left As Vector3, right As Vector3) As (Left As Vector4, Right As Vector4)?
        If litType <> &H10 Then Return Nothing
        Return (New Vector4(left, 0.0F), New Vector4(right, 0.0F))
    End Function

    ''' <summary>TintColor (PS cb1, types 5 FacegenRGBTint and 6 HairTint, 0x141548A93..AB8 then 0x141548F94..F9A) = (material +0xA0,
    ''' +0xA4, +0xA8). .w not written.</summary>
    Friend Function TintColor(litType As Integer, tint As Vector3) As Vector4?
        If litType <> 5 AndAlso litType <> 6 Then Return Nothing
        Return New Vector4(tint, 0.0F)
    End Function

    ''' <summary>[0x1420D681E]: .data 1, no RIP writer in .text (ripx --writes): LODTexParams .z = 1.0 (0x141548ABD..ACB / E00..E0E).</summary>
    Friend Const LodTexParamsFlag As Byte = 1

    ''' <summary>LODTexParams (PS cb1): the landscape material (types 8 / 19, 0x141548ABD..AFE) = (material +0x148, +0x14C,
    ''' [0x1420D681E] ? 1.0 : 0, +0x150); the LOD landscape material (types 9 / 18, 0x141548E00..E3F) = (material +0xB8, +0xBC, the
    ''' same flag, +0xC0). <paramref name="xy"/> / <paramref name="w"/> = those fields of the type's material.</summary>
    Friend Function LodTexParams(litType As Integer, xy As Vector2, w As Single) As Vector4?
        If litType <> 8 AndAlso litType <> &H13 AndAlso litType <> 9 AndAlso litType <> &H12 Then Return Nothing
        Return New Vector4(xy.X, xy.Y, If(LodTexParamsFlag <> 0, 1.0F, 0.0F), w)
    End Function

    ''' <summary>bEnableSnowMask:Display (Setting 0x1420D8DA0, .data 1) = [0x1420D8DA8]; iLandscapeMultiNormalTilingFactor:Display
    ''' (Setting 0x1420D8E48, .data 4) = [0x1420D8E50].</summary>
    Friend Const EnableSnowMask As Byte = 1
    Friend Const LandscapeMultiNormalTilingFactor As Integer = 4

    ''' <summary>The four landscape names (PS cb1, the landscape material, types 8 / 19, 0x141548CD9..DFB):
    ''' LandscapeTexture1to4IsSpecPower = (material +0x130, +0x134, +0x138, +0x13C) (0x141548CE7..D1B);
    ''' LandscapeTexture5to6IsSpecPower = (+0x140, +0x144, 0, 0) (0x141548D29..D44, the qword 0 at +8); with technique bit 21
    ''' (0x141548D4D): LandscapeTexture1to4IsSnow = (+0x118, +0x11C, +0x120, +0x124) (0x141548D68..D9C) and LandscapeTexture5to6IsSnow =
    ''' (+0x128, +0x12C, bEnableSnowMask ? 1.0 : 0, 1.0 / float(iLandscapeMultiNormalTilingFactor)) (0x141548D9F..DF4: cvtdq2ps,
    ''' divss of 1.0 [0x141B5BE28]). Without bit 21 the two IsSnow are not written (Nothing).</summary>
    Friend Function Landscape(litType As Integer, technique As UInteger, specPower1to4 As Vector4, specPower5to6 As Vector2,
                              snow1to4 As Vector4, snow5to6 As Vector2) As (SpecPower1to4 As Vector4, SpecPower5to6 As Vector4, Snow1to4 As Vector4?, Snow5to6 As Vector4?)?
        If litType <> 8 AndAlso litType <> &H13 Then Return Nothing
        Dim sp56 As New Vector4(specPower5to6.X, specPower5to6.Y, 0.0F, 0.0F)
        If (technique And &H200000UI) = 0UI Then Return (specPower1to4, sp56, Nothing, Nothing)
        Dim tiling As Single = 1.0F / CSng(LandscapeMultiNormalTilingFactor)
#If DEBUG Then
        ' ParityGate sse-constants-law [landscape]: the tiling factor itself instead of its reciprocal.
        If Fo4GBufferConstants.GateMutantOn("sse-landsnow-recip") Then tiling = CSng(LandscapeMultiNormalTilingFactor)
#End If
        Return (specPower1to4, sp56, snow1to4, New Vector4(snow5to6.X, snow5to6.Y, If(EnableSnowMask <> 0, 1.0F, 0.0F), tiling))
    End Function

    ''' <summary>MultiLayerParallaxData (PS cb1, type 11, 0x141548EF5..F48) = (material +0xB8, +0xBC, +0xC0, +0xC4).</summary>
    Friend Function MultiLayerParallaxData(litType As Integer, data As Vector4) As Vector4?
        If litType <> &HB Then Return Nothing
        Return data
    End Function

    ''' <summary>SparkleParams (PS cb1, type 14 MultiIndexSnow, 0x141548EBD..EF0 then 0x141548F94..F9A) = (material +0xA0, +0xA4,
    ''' +0xA8, +0xAC).</summary>
    Friend Function SparkleParams(litType As Integer, data As Vector4) As Vector4?
        If litType <> &HE Then Return Nothing
        Return data
    End Function

    ''' <summary>The texcoord-offset set the frame reads: [0x1420D69D0], .data 0, flipped (xor 1) every frame by the game loop's
    ''' 0x140658870 (0x140658A97), 0x1409A00A0 (0x1409A0333) and 0x1409A0760 (0x1409A0873). The preview runs no game loop: 0.</summary>
    Friend Const TexcoordOffsetFrameIndex As Integer = 0

    ''' <summary>TexcoordOffset (VS cb1; lighting SM 0x141548F9D..FE3 for every type, utility SM 0x1415670DA..119 with technique bit 1
    ''' and (technique AND &H2040280) != 0, 0x1415670C0..0D4 - the one owner of both): i = [0x1420D69D0]; (material +0xC + 8 i,
    ''' +0x10 + 8 i, +0x1C + 8 i, +0x20 + 8 i) - the material's two (offset u, offset v) pairs at +0xC / +0x14 and two (scale u,
    ''' scale v) pairs at +0x1C / +0x24.</summary>
    Friend Function TexcoordOffset(offsets As Vector2(), scales As Vector2(), frameIndex As Integer) As Vector4
        Dim i = frameIndex
#If DEBUG Then
        ' ParityGate sse-constants-law [material]: the frame's set ignored (always the first).
        If Fo4GBufferConstants.GateMutantOn("sse-texcoord-index") Then i = 0
#End If
        Return New Vector4(offsets(i).X, offsets(i).Y, scales(i).X, scales(i).Y)
    End Function

    ''' <summary>SpecularColor (PS cb1, technique bit 9 SPECULAR, 0x141548FE8..905C): s = material +0x8C; ((material +0x38) s,
    ''' (+0x3C) s, (+0x40) s, material +0x88) (mulss each, 0x14154902D..03F; .w 0x141549052..058).</summary>
    Friend Function SpecularColor(technique As UInteger, color As Vector3, strength As Single, w As Single) As Vector4?
        If (technique And &H200UI) = 0UI Then Return Nothing
        Return New Vector4(color.X * strength, color.Y * strength, color.Z * strength, w)
    End Function

    ''' <summary>LightingEffectParams (PS cb1): with technique bit 10 (0x141549115..185) or bit 11 (0x141549189..205) = (material
    ''' +0x90, material +0x94). .zw not written.</summary>
    Friend Function LightingEffectParams(technique As UInteger, effect1 As Single, effect2 As Single) As Vector4?
        If (technique And &HC00UI) = 0UI Then Return Nothing
        Return New Vector4(effect1, effect2, 0.0F, 0.0F)
    End Function

    ' =============================================================================================================================
    ' SetupGeometry 0x141549550 (cb2): r14 = the pass's shader property ([pass+8]), rbx = the geometry ([pass+0x10]), r12 = the pass,
    ' r15d = the model-space flag (LitModelSpace), [rbp+0x1F0] = the call's flags (r8d of 0x141549550, stored at 0x141549553).
    ' =============================================================================================================================

    ''' <summary>Is World (VS cb2 rows 0..2) written? Types 1 / 11 / 16 without technique bit 1 (0x141549705..714), types 9 / 13 / 15 / 18
    ''' always (0x1415497FB..805; cl = 1 then skips the common write, 0x141549865 / 0x141549A08), the others without bit 1
    ''' (0x1415499FA..A1A).</summary>
    Friend Function LitWorldWritten(technique As UInteger) As Boolean
        Dim ty = LitType(technique)
        If ty = 9 OrElse ty = &HD OrElse ty = &HF OrElse ty = &H12 Then Return True
        Return (technique And 2UI) = 0UI
    End Function

    ''' <summary>World[3] (VS cb2, 0x14154B6A0 with r8d = 0): M = <see cref="WorldRelativeMatrix"/> of the geometry's world transform
    ''' (geom +0x7C) and the frame eye (0x14154B6C3); row c = (M[0][c], M[1][c], M[2][c], M[3][c]) for c = 0..2 (0x14154B6C8..751).
    ''' PreviousWorld is the same function with r8d = 1 and the previous eye.</summary>
    Friend Function World(worldTransform As SseNiTransform, eye As Vector3) As Vector4()
        Dim m = WorldRelativeMatrix(worldTransform, eye)
        Return {m.Column0, m.Column1, m.Column2}
    End Function

    ''' <summary>MaterialData (PS cb2): .x = property +0x104 for types 1 / 11 / 16 (0x14154972C..737); .y = property +0x100 with
    ''' technique bit 9 (0x14154A09C..0BD); .z = property +0x30 (the alpha, 0x141549F11..F1B) times [[property +0x60] +0x14C] when the
    ''' pass's byte +0x1E has bit 7 (0x141549F22..F33). A component the technique does not reach is Nothing in .written.</summary>
    Friend Function MaterialData(technique As UInteger, prop104 As Single, prop100 As Single, alpha As Single, passFadeBit As Boolean,
                                 fadeNode14C As Single) As (Value As Vector4, XWritten As Boolean, YWritten As Boolean)
        Dim ty = LitType(technique)
        Dim xw = ty = 1 OrElse ty = &HB OrElse ty = &H10
        Dim yw = (technique And &H200UI) <> 0UI
        Dim z As Single = alpha
#If DEBUG Then
        ' ParityGate sse-constants-law [material]: the pass's fade left out.
        If Fo4GBufferConstants.GateMutantOn("sse-matdata-nofade") Then passFadeBit = False
#End If
        If passFadeBit Then z = z * fadeNode14C
        Return (New Vector4(If(xw, prop104, 0.0F), If(yw, prop100, 0.0F), z, 0.0F), xw, yw)
    End Function

    ''' <summary>[0x141B5BDF8] = 0.2 (0x141549743), the LOD blend's slope.</summary>
    Friend ReadOnly LandBlendSlope As Single = BitConverter.UInt32BitsToSingle(&H3E4CCCCDUI)

    ''' <summary>The world's LOD land-blend state LandBlendParams reads: [0x1420D6948] (.data 1.0, the divisor), [0x1420D68D0] (.data 0),
    ''' the start / end blend points [0x1420D6958 / 5C] and [0x1420D6960 / 64] and [0x1420D6968] (.data 0): written by the static
    ''' initializer 0x1400FFA60 (0x1400FFA7D..AC2) and the LOD land loaders 0x1401A0450 / 0x1401A0630 / 0x1401A30A0 (world state the
    ''' preview does not run).</summary>
    Friend Structure SseLandBlendState
        Public Divisor As Single         ' [0x1420D6948]
        Public Distance As Single        ' [0x1420D68D0]
        Public DistanceStart As Single   ' [0x1420D6968]
        Public Start As Vector2          ' [0x1420D6958], [0x1420D695C]
        Public [End] As Vector2          ' [0x1420D6960], [0x1420D6964]
    End Structure

    ''' <summary>LandBlendParams (VS cb2, types 8 / 19, 0x14154973F..7F0): t = min(max((D68D0 - D6968) (0.2 / D6948), 0), 1.0)
    ''' (0x141549743..7A5: divss, subss, mulss, maxss 0, minss 1); (o +0x108, o +0x10C, ((D6960 - D6958) t + D6958) - geom +0xA0,
    ''' ((D6964 - D695C) t + D695C) - geom +0xA4) with o = [property +0x78] (0x14154973F, 0x141549787..7F0). When t == 1 the call also
    ''' clears the byte [0x1433D3D94] (0x1415497AA..7C8): a side effect, not a constant.</summary>
    Friend Function LandBlendParams(litType As Integer, o108 As Single, o10C As Single, land As SseLandBlendState, worldTranslation As Vector3) As Vector4?
        If litType <> 8 AndAlso litType <> &H13 Then Return Nothing
        Dim f As Single = LandBlendSlope / land.Divisor
        Dim t As Single = (land.Distance - land.DistanceStart) * f
        t = SseD3dx9.MinPs(SseD3dx9.MaxPs(t, 0.0F), 1.0F)
        Dim z As Single = ((land.End.X - land.Start.X) * t + land.Start.X) - worldTranslation.X
        Dim w As Single = ((land.End.Y - land.Start.Y) * t + land.Start.Y) - worldTranslation.Y
        Return New Vector4(o108, o10C, z, w)
    End Function

    ''' <summary>[[0x1420D68B0] + 0x304]: the ShadowSceneNode 0's float TreeParams .y reads. Its only writers store the immediate 3.0:
    ''' the ShadowSceneNode ctor 0x14150BA50 (0x14150BEB7) and 0x14043E330 (0x14043E3F9..400, through [0x1420D68B0]); no other store
    ''' of any width covers +0x304 in a function that reaches a ShadowSceneNode (dispx / cover8 sweeps over .text).</summary>
    Friend ReadOnly ShadowSceneNodeTreeY As Single = BitConverter.UInt32BitsToSingle(&H40400000UI)

    ''' <summary>[0x141B5BE14] = 0.5, [0x141B5BE2C] = 1.5, magic 0x5F3759DF: the fast square root of 0x1415498E3..92B / 0x141567D57..DAB.</summary>
    Friend ReadOnly FastSqrtHalf As Single = BitConverter.UInt32BitsToSingle(&H3F000000UI)
    Friend ReadOnly FastSqrtThreeHalves As Single = BitConverter.UInt32BitsToSingle(&H3FC00000UI)
    Friend Const FastSqrtMagic As Integer = &H5F3759DF

    ''' <summary>x times its one-Newton inverse square root: y = bits(0x5F3759DF - (bits(x) sar 1)) (0x1415498EB..915), then
    ''' (((1.5 - ((x 0.5) y) y) y) x (0x1415498F8..92B).</summary>
    Friend Function FastSqrt(x As Single) As Single
        Dim y As Single = BitConverter.UInt32BitsToSingle(CUInt((CLng(FastSqrtMagic) - CLng(BitConverter.SingleToInt32Bits(x) >> 1)) And &HFFFFFFFFL))
        Dim h As Single = ((x * FastSqrtHalf) * y) * y
        Return ((FastSqrtThreeHalves - h) * y) * x
    End Function

    ''' <summary>[0x141B5BE74] = 6.0: the wind timers' scale (0x141549992, 0x141567D27).</summary>
    Friend ReadOnly WindTimerScale As Single = BitConverter.UInt32BitsToSingle(&H40C00000UI)

    ''' <summary>The tree animation object a TREE_ANIM draw reads: a = vfunc +0x1F8 of [property +0x60] when that pointer is non-null
    ''' and not [0x1433D3CA0] (0x141549885..8C2 / 0x141567CEC..D1D), else none. Its fields +0x158 .. +0x168.</summary>
    Friend Structure SseTreeAnim
        Public Present As Boolean
        Public F158 As Single, F15C As Single, F160 As Single, F164 As Single, F168 As Single
    End Structure

    ''' <summary>[0x1420D6950] / [0x1420D6954]: the tree fade band, written by the settings applier 0x1414EA2D0 (0x1414EAED5 / EE5),
    ''' .data 0.</summary>
    Friend Structure SseTreeFadeBand
        Public Low As Single
        Public High As Single
    End Structure

    ''' <summary>The band the settings applier 0x1414EA2D0 writes: [0x1420D6950] = fLeafAnimDampenDistStart:Display (.data 3600, value at
    ''' 0x1420D67B0, 0x1414EAECD..ED5), [0x1420D6954] = fLeafAnimDampenDistEnd:Display (.data 4600, 0x1420D67C8, 0x1414EAEDD..EE5): the exe
    ''' defaults, no INI. Without a tree animation object q = 0 gives min(max(4.6, 0), 1) = 1, the cb2[8].z of the rest pose
    ''' (SseRenderPassLaw.LeafRestPosition).</summary>
    Friend ReadOnly LeafAnimDampenBand As New SseTreeFadeBand With {.Low = 3600.0F, .High = 4600.0F}

    ''' <summary>TreeParams .z: c = a ? a+0x15C : 1.0; q = a ? FastSqrt(a+0x158) : 0; min(max((1 - (q - lo) / (hi - lo)) c, 0), c)
    ''' (lighting 0x141549934..974, utility 0x141567DB4..DF1).</summary>
    Friend Function TreeFade(a As SseTreeAnim, band As SseTreeFadeBand) As Single
        Dim q As Single = If(a.Present, FastSqrt(a.F158), 0.0F)
        Dim c As Single = If(a.Present, a.F15C, 1.0F)
        Dim span As Single = band.High - band.Low
        Dim r As Single = (q - band.Low) / span
        Dim v As Single = (1.0F - r) * c
        Return SseD3dx9.MinPs(SseD3dx9.MaxPs(v, 0.0F), c)
    End Function

    ''' <summary>TreeParams .y = [[0x1420D68B0]+0x304] (0x1415498CC..D9 / 0x141567D41..D4E).</summary>
    Friend Function TreeParamsY() As Single
#If DEBUG Then
        ' ParityGate sse-constants-law [tree]: the ShadowSceneNode's float not read.
        If Fo4GBufferConstants.GateMutantOn("sse-treeparams-y") Then Return 0.0F
#End If
        Return ShadowSceneNodeTreeY
    End Function

    ''' <summary>TreeParams (lighting VS cb2, type 12, 0x141549885..99A) = (0 (0x1415498C4), TreeParamsY, TreeFade, a ? a+0x160 : 1.0
    ''' (0x14154997F..99A)); WindTimers (0x1415499A1..9E1) = (a ? a+0x164 6 : 0, a ? a+0x168 6 : 0), and then the draw copies a+0x164
    ''' into a+0x168 (0x1415499E7..9F2: the object's state for the next draw, returned as NextF168). .zw of WindTimers not written.</summary>
    Friend Function LitTreeParams(litType As Integer, a As SseTreeAnim, band As SseTreeFadeBand) As (TreeParams As Vector4, WindTimers As Vector4, NextF168 As Single)?
        If litType <> &HC Then Return Nothing
        Dim tp As New Vector4(0.0F, TreeParamsY(), TreeFade(a, band), If(a.Present, a.F160, 1.0F))
        Dim wt As New Vector4(If(a.Present, a.F164 * WindTimerScale, 0.0F), If(a.Present, a.F168 * WindTimerScale, 0.0F), 0.0F, 0.0F)
        Return (tp, wt, If(a.Present, a.F164, a.F168))
    End Function

    ''' <summary>The LOD bounds [0x1420D68E4..F0] (.data 0; written by 0x1400FFA85 (movups from [0x1420CEEE0]) / 0x1414ECB57..B7B) and [0x141B5BE88] = 15.0.</summary>
    Friend ReadOnly HighDetailRangeLift As Single = BitConverter.UInt32BitsToSingle(&H41700000UI)

    ''' <summary>HighDetailRange from the LOD bounds b: (b.x - eye.x, b.y - eye.y, b.z - 15, b.w - 15) - lighting ST types 9 / 18
    ''' (0x141548597..5F5, copied by 0x1410193A0 at 0x141548607), utility ST with (technique AND &H1E00100) = 0x100 (0x141566882..8DB).
    ''' The one owner of both.</summary>
    Friend Function HighDetailRangeLod(lodBounds As Vector4, eye As Vector3) As Vector4
        Return New Vector4(lodBounds.X - eye.X, lodBounds.Y - eye.Y, lodBounds.Z - HighDetailRangeLift, lodBounds.W - HighDetailRangeLift)
    End Function

    ''' <summary>HighDetailRange (lighting VS cb0): types 9 / 18 only (jump table 0x1415487C0 -&gt; 0x14154852E).</summary>
    Friend Function LitHighDetailRange(litType As Integer, lodBounds As Vector4, eye As Vector3) As Vector4?
        If litType <> 9 AndAlso litType <> &H12 Then Return Nothing
        Return HighDetailRangeLod(lodBounds, eye)
    End Function

    ''' <summary>The world map overlay vectors [0x1433D3EE8] (VS) and [0x1433D3EF8] (PS): .bss 0, written by the game loop
    ''' 0x1409A00A0 (0x1409A027F / 28F) and 0x1409A0760 (0x1409A07C4 / 7D4).</summary>
    Friend Structure SseWorldMapOverlay
        Public Vs As Vector4
        Public Ps As Vector4
    End Structure

    ''' <summary>WorldMapOverlayParameters (VS cb2) and WorldMapOverlayParametersPS (PS cb2), technique bit 18 (0x14154A530): 0x1410193A0
    ''' copies the 16 bytes (0x1410193A0..3B3) of [0x1433D3EE8] to the VS (0x14154A5F4..60B) and [0x1433D3EF8] to the PS
    ''' (0x14154A5E3..61A).</summary>
    Friend Function WorldMapOverlay(technique As UInteger, state As SseWorldMapOverlay) As SseWorldMapOverlay?
        If (technique And &H40000UI) = 0UI Then Return Nothing
        Return state
    End Function

    ''' <summary>The accumulator's eye EyePosition / EyePos read: [[0x1433D3DA8] + 0x16C..0x174] (0x1414ECB90 returns [0x1433D3DA8];
    ''' set by 0x1414EA1E0 and 0x1414ECBA0). EyePosition (VS cb2, 0x14154A61F..697) is written when <paramref name="writes"/>
    ''' (SseRenderPassLaw.LitViewVectorWritten): model space (r15d, 0x14154A640) = D3DXVec3TransformCoord(accumulator eye, the inverse
    ''' world 0x140FDEEC0) (0x14154A646..657); world space = accumulator eye - frame eye (subss, 0x14154A65E..697). .w not written.</summary>
    Friend Function LitEyePosition(writes As Boolean, modelSpace As Boolean, world As SseNiTransform,
                                   accumulatorEye As Vector3, eye As Vector3, route As SseD3dx9.SseD3dxRoute) As Vector4?
        If Not writes Then Return Nothing
#If DEBUG Then
        ' ParityGate sse-constants-law [eye]: the model-space branch dropped.
        If Fo4GBufferConstants.GateMutantOn("sse-eyepos-world") Then modelSpace = False
#End If
        If modelSpace Then Return New Vector4(SseD3dx9.Vec3TransformCoord(route, accumulatorEye, InverseWorldMatrix(world)), 0.0F)
        Return New Vector4(accumulatorEye.X - eye.X, accumulatorEye.Y - eye.Y, accumulatorEye.Z - eye.Z, 0.0F)
    End Function

    ''' <summary>fSpecMaskBegin:Display (Setting 0x1420D86D8, .data 0.1) = [0x1420D86E0]; fSpecMaskSpan:Display (Setting 0x1420D86F0,
    ''' .data 0) = [0x1420D86F8] (the console 0x14037E4E0 rewrites them).</summary>
    Friend ReadOnly SpecMaskBegin As Single = BitConverter.UInt32BitsToSingle(&H3DCCCCCDUI)
    Friend Const SpecMaskSpan As Single = 0.0F

    ''' <summary>SSRParams (PS cb2, 0x14154A79F..810): (fSpecMaskBegin, fSpecMaskSpan + fSpecMaskBegin, [0x143436408] (the lighting
    ''' shader's float, .bss 0, written by 0x14151F540 / 0x1415200D0), w) with w = ((flags AND 2) ? 0 : 1.0) (technique bit 9 ?
    ''' property +0x100 : 0) (0x14154A7E3..810).</summary>
    Friend Function SsrParams(technique As UInteger, sgFlags As UInteger, shader408 As Single, prop100 As Single) As Vector4
        Dim spec As Single = If((technique And &H200UI) <> 0UI, prop100, 0.0F)
        Dim sel As Single = If((sgFlags And 2UI) <> 0UI, 0.0F, 1.0F)
        Return New Vector4(SpecMaskBegin, SpecMaskSpan + SpecMaskBegin, shader408, sel * spec)
    End Function

    ''' <summary>bEnableProjecteUVDiffuseNormals:Display (Setting 0x1420D8D70, .data 1; the console 0x14037BE60 rewrites it) and
    ''' bEnableProjecteUVDiffuseNormalsOnCubemap:Display (Setting 0x1420D8D88, .data 0); fProjectedUVDiffuseNormalTilingScale:Display
    ''' (0x1420D8DB8, .data 0.5) and fProjectedUVNormalDetailTilingScale:Display (0x1420D8DD0, .data 1.2).</summary>
    Friend Const ProjectedUvDiffuseNormals As Byte = 1
    Friend Const ProjectedUvDiffuseNormalsOnCubemap As Byte = 0
    Friend ReadOnly ProjectedUvDiffuseNormalTiling As Single = BitConverter.UInt32BitsToSingle(&H3F000000UI)
    Friend ReadOnly ProjectedUvNormalDetailTiling As Single = BitConverter.UInt32BitsToSingle(&H3F99999AUI)

    ''' <summary>The flag 0x14154A0DE..116 hands 0x14154C600 (r9b): [0x1420D8D78] = 0 gives 0; else flags bit 3 with [0x1420D8D90] != 0
    ''' gives 0; else 1.</summary>
    Friend Function ProjectedUvDiffuseNormalsOn(sgFlags As UInteger) As Boolean
        If ProjectedUvDiffuseNormals = 0 Then Return False
        If (sgFlags And 8UI) <> 0UI AndAlso ProjectedUvDiffuseNormalsOnCubemap <> 0 Then Return False
        Return True
    End Function

    ''' <summary>The projected-UV source 0x14154C600 reads (rcx = rdx ? geometry +0x1BC : property +0x10C, 0x14154C609..62C): rdx is the
    ''' geometry when its vfunc +0x1A8 answered non-null (0x14154A2D9..2F5), else 0 (0x14154A488).</summary>
    Friend Structure SseProjectedUvSource
        Public FromGeometry As Boolean
        Public P As Vector4                ' +0x0 .. +0xC of the source
        Public Geometry1CC As Single       ' geometry +0x1CC
        Public Geometry1D0 As Single       ' geometry +0x1D0
        Public Property11C As Vector4      ' property +0x11C .. +0x128
    End Structure

    ''' <summary>ProjectedUVParams / 2 / 3 (PS cb2, 0x14154C600, called at 0x14154A52B with technique bit 15 and type != 6,
    ''' 0x14154A116..124): ProjectedUVParams = ((1 - p.w) p.x, -, p.z, (1 - p.w) p.y + p.w) (0x14154C61E..659; .y not written);
    ''' ProjectedUVParams2 = geometry: (geometry +0x1D0, geometry +0x1CC, -, -) (0x14154C666..676), property: (property +0x11C, +0x120,
    ''' +0x124, +0x128) (0x14154C67D..6A7); ProjectedUVParams3 = (fProjectedUVDiffuseNormalTilingScale, fProjectedUVNormalDetailTilingScale,
    ''' 0, flag ? 1.0 : 0) (0x14154C6AC..6F0).</summary>
    Friend Function ProjectedUvParams(technique As UInteger, src As SseProjectedUvSource, sgFlags As UInteger) As (P1 As Vector4, P2 As Vector4, P3 As Vector4)?
        If (technique And &H8000UI) = 0UI OrElse LitType(technique) = 6 Then Return Nothing
        Dim k As Single = 1.0F - src.P.W
        Dim p1 As New Vector4(k * src.P.X, 0.0F, src.P.Z, k * src.P.Y + src.P.W)
        Dim p2 = If(src.FromGeometry, New Vector4(src.Geometry1D0, src.Geometry1CC, 0.0F, 0.0F), src.Property11C)
        Dim p3 As New Vector4(ProjectedUvDiffuseNormalTiling, ProjectedUvNormalDetailTiling, 0.0F, If(ProjectedUvDiffuseNormalsOn(sgFlags), 1.0F, 0.0F))
        Return (p1, p2, p3)
    End Function

    ''' <summary>The NiTransform 0x14154A389..3DF lays on the stack: m = (0, 1, 0 / -1, 0, 0 / 0, 0, 1) (the 16 bytes [0x141B47600] =
    ''' (0, 1, 0, -1), then 16 zero bytes, then 1.0 at [rbp-0x60]), t = the frame eye, s = 1.0.</summary>
    Friend ReadOnly TextureProjBasis As Vector4 = New Vector4(0.0F, 1.0F, 0.0F, -1.0F)

    ''' <summary>TextureProj[3] (VS cb2, technique bit 15 and type != 6, 0x14154A116..124): when the geometry's vfunc +0x1A8 answers
    ''' non-null (0x14154A2D9..2E8), rows c = 0..2 = column c of the 4x4 at geometry +0x16C (0x14154A2FC..37F); else A =
    ''' <see cref="WorldRelativeMatrix"/> of (TextureProjBasis, eye, 1) (0x14154A389..3DF); type 1: A (0x14154A3E4..40A); else B =
    ''' WorldRelativeMatrix(world, eye) with its row 3 xyz + eye (0x14154A40C..456), M = D3DXMatrixMultiply(B, A) (0x14154A45B);
    ''' rows c = 0..2 = column c of the result (0x14154A481..517).</summary>
    Friend Function TextureProj(technique As UInteger, geometryProjection As Matrix4?, world As SseNiTransform, eye As Vector3,
                                route As SseD3dx9.SseD3dxRoute) As Vector4()
        If (technique And &H8000UI) = 0UI OrElse LitType(technique) = 6 Then Return Nothing
        If geometryProjection.HasValue Then
            Dim g = geometryProjection.Value
            Return {g.Column0, g.Column1, g.Column2}
        End If
        Dim basis As New SseNiTransform With {
            .Rotation = New Matrix3(TextureProjBasis.X, TextureProjBasis.Y, TextureProjBasis.Z, TextureProjBasis.W, 0.0F, 0.0F, 0.0F, 0.0F, 1.0F),
            .Translation = eye, .Scale = 1.0F}
        Dim a = WorldRelativeMatrix(basis, eye)
        Dim m As Matrix4
        If LitType(technique) = 1 Then
            m = a
        Else
            Dim b = WorldRelativeMatrix(world, eye)
            b.M41 = b.M41 + eye.X : b.M42 = b.M42 + eye.Y : b.M43 = b.M43 + eye.Z
            m = SseD3dx9.MatrixMultiply(route, b, a)
        End If
        Return {m.Column0, m.Column1, m.Column2}
    End Function

    ''' <summary>AmbientSpecularTintAndFresnelPowerB2 (the census pair lit-ps, cb2 offset 14): THE ENGINE HAS NO WRITER for cb2[14].
    ''' The one writer of AmbientSpecularTintAndFresnelPower is SetupMaterial's 0x1415490EF..10B (technique bit 17: [0x1420D69AC] into
    ''' [PS +0x46], the cb1 data of 0x141548820), which binds cb1 (PSSetConstantBuffers slot 1, 0x1415494DC); SetupGeometry binds b2
    ''' at 0x14154A896 without writing it, and GetRenderPasses 0x14151A160 never sets technique bit 17. Verified in
    ''' D:\WinTmp\rc\olasse\s1\f1\verif_ambientspecular.md. Nothing to transcribe: no value.</summary>
    Friend Const AmbientSpecularB2Verification As String = "D:\WinTmp\rc\olasse\s1\f1\verif_ambientspecular.md"
    ''' <summary>The Kind of the one name whose value the ENGINE does not have (no writer in the exe): its Seat must cite the
    ''' verification file. ParityGate sse-constants-law [census] accepts only "law" and this.</summary>
    Friend Const SeatKindEngineNoWriter As String = "engine-no-writer"
    ''' <summary>The verification file of every engine-no-writer seat (ParityGate sse-constants-law [census]): cb2[14], and the first
    ''' frame of a new camera entry (EngineCameraMotionEntry.FirstFrameSeats; the rows themselves have their law,
    ''' Cb12PrevViewProjNoOffset).</summary>
    Friend ReadOnly EngineNoWriterVerifications As String() = {AmbientSpecularB2Verification, EngineCameraMotionEntry.FirstFrameVerification}

    ''' <summary>Where each name of the closed list (the `uniform vec4` declarations of the g6 / g7 blocks, sse_constants_oracle.py
    ''' --uniforms) lives: a law of this module, SseUtilityConstants or SseRenderPassLaw (Kind "law", its function), or - for
    ''' cb2[14] - "engine-no-writer" with its verification (EngineNoWriterVerifications). ParityGate sse-constants-law [census] holds the list against the
    ''' blocks, both ways.</summary>
    Friend ReadOnly Seats As (Stage As String, Name As String, Kind As String, Seat As String)() = {
        ("lit-ps", "AmbientSpecularTintAndFresnelPowerB2", SeatKindEngineNoWriter, "cb2[14]: no writer in SkyrimSE.exe (" & AmbientSpecularB2Verification & ")"),
        ("lit-ps", "CharacterLightParams", "law", "CharacterLightParams"),
        ("lit-ps", "ColourOutputClamp", "law", "ColourOutputClamp"),
        ("lit-ps", "DirLightColor", "law", "DirLightColor"),
        ("lit-ps", "DirLightDirection", "law", "DirLightDirection"),
        ("lit-ps", "DirectionalAmbient", "law", "DirectionalAmbient"),
        ("lit-ps", "EmitColor", "law", "EmitColor"),
        ("lit-ps", "EnvmapData", "law", "EnvmapData"),
        ("lit-ps", "FogColor", "law", "FogColor"),
        ("lit-ps", "IBLParams", "law", "IBLParams"),
        ("lit-ps", "LODTexParams", "law", "LodTexParams"),
        ("lit-ps", "LandscapeTexture1to4IsSnow", "law", "Landscape"),
        ("lit-ps", "LandscapeTexture1to4IsSpecPower", "law", "Landscape"),
        ("lit-ps", "LandscapeTexture5to6IsSnow", "law", "Landscape"),
        ("lit-ps", "LandscapeTexture5to6IsSpecPower", "law", "Landscape"),
        ("lit-ps", "LightingEffectParams", "law", "LightingEffectParams"),
        ("lit-ps", "MaterialData", "law", "MaterialData"),
        ("lit-ps", "MultiLayerParallaxData", "law", "MultiLayerParallaxData"),
        ("lit-ps", "NumLightNumShadowLight", "law", "NumLightNumShadowLight"),
        ("lit-ps", "PointLightColor", "law", "PointLightRows"),
        ("lit-ps", "PointLightPosition", "law", "PointLightRows"),
        ("lit-ps", "ProjectedUVParams", "law", "ProjectedUvParams"),
        ("lit-ps", "ProjectedUVParams2", "law", "ProjectedUvParams"),
        ("lit-ps", "ProjectedUVParams3", "law", "ProjectedUvParams"),
        ("lit-ps", "SSRParams", "law", "SsrParams"),
        ("lit-ps", "ShadowLightMaskSelect", "law", "ShadowLightMaskSelect"),
        ("lit-ps", "SnowRimLightParameters", "law", "SnowRimLightParameters"),
        ("lit-ps", "SparkleParams", "law", "SparkleParams"),
        ("lit-ps", "SpecularColor", "law", "SpecularColor"),
        ("lit-ps", "TintColor", "law", "TintColor"),
        ("lit-ps", "VPOSOffset", "law", "VposOffset"),
        ("lit-ps", "WorldMapOverlayParametersPS", "law", "WorldMapOverlay"),
        ("lit-ps", "cb11_AlphaTestRef", "law", "Cb11AlphaTestRef"),
        ("lit-ps", "scb12_42", "law", "Cb12Row42"),
        ("lit-ps", "scb12_43", "law", "Cb12Row43"),
        ("lit-ps", "scb12_44", "law", "Cb12Row44"),
        ("lit-ps", "scb12_prevviewproj_nooffset", "law", "Cb12PrevViewProjNoOffset"),
        ("lit-ps", "scb12_viewproj_nooffset", "law", "Cb12ViewProjNoOffset"),
        ("lit-vs", "EyePosition", "law", "LitEyePosition"),
        ("lit-vs", "FogFarColor", "law", "FogFarColor"),
        ("lit-vs", "FogNearColor", "law", "FogNearColor"),
        ("lit-vs", "FogParam", "law", "FogParam"),
        ("lit-vs", "HighDetailRange", "law", "LitHighDetailRange"),
        ("lit-vs", "LandBlendParams", "law", "LandBlendParams"),
        ("lit-vs", "LeftEyeCenter", "law", "EyeCenters"),
        ("lit-vs", "PreviousWorld", "law", "PreviousWorld"),
        ("lit-vs", "RightEyeCenter", "law", "EyeCenters"),
        ("lit-vs", "TexcoordOffset", "law", "TexcoordOffset"),
        ("lit-vs", "TextureProj", "law", "TextureProj"),
        ("lit-vs", "TreeParams", "law", "LitTreeParams"),
        ("lit-vs", "WindTimers", "law", "LitTreeParams"),
        ("lit-vs", "World", "law", "World"),
        ("lit-vs", "WorldMapOverlayParameters", "law", "WorldMapOverlay"),
        ("lit-vs", "scb12_eye", "law", "Cb12Eye"),
        ("lit-vs", "scb12_preveye", "law", "Cb12PrevEye"),
        ("lit-vs", "scb12_view", "law", "Cb12View"),
        ("lit-vs", "scb12_viewproj", "law", "Cb12ViewProj")}
End Module
