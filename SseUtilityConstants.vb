Imports OpenTK.Mathematics

''' <summary>THE CONSTANTS OF THE SKYRIM SE UTILITY PROGRAMS (g7), BY NAME: what BSUtilityShader's SetupTechnique 0x141566710 (cb0),
''' SetupMaterial 0x141567010 (cb1) and SetupGeometry 0x1415673F0 / the mask's 0x141568300 (cb2) write for each name the transcribed
''' GLSL declares (Tools\ShaderTranscription\sse-utility), for the passes S1 draws: the screen-space shadow mask of the sun (technique
''' SseUtilityTechnique.MaskTechnique + the filter bit, record 11381 with uShadowFilterType 3; s1_re 4.2) and the depth / shadow-map passes. SkyrimSE.exe
''' 1.7.104.0; the same rules as SseLightingConstants (Single arithmetic in the exe's order, one owner per value, explicit inputs; a
''' name the technique does not reach is Nothing; every name has its seat in <see cref="Seats"/>). The names both classes declare
''' (VPOSOffset, cb11, the cb12 rows, TexcoordOffset) have their one owner in SseLightingConstants.</summary>
Friend Module SseUtilityConstants

    ''' <summary>The mask's receiver bias scales (SG 0x141568300): 0.00025 [0x141B4ACA8] (.y, 0x14156890E) and 8.333e-5 [0x141B4ACA4]
    ''' (.z, 0x141568928).</summary>
    Friend ReadOnly ShadowBiasYScale As Single = BitConverter.UInt32BitsToSingle(&H3983126FUI)
    Friend ReadOnly ShadowBiasZScale As Single = BitConverter.UInt32BitsToSingle(&H38AEC33FUI)
    ''' <summary>fShadowDirectionalBiasScale:Display (Setting 0x1420D9510, .data 0.3), read at 0x1415688F4 for a directional light.</summary>
    Friend Const ShadowDirectionalBiasScale As Single = 0.3F
    ''' <summary>fShadowBiasScale:Display (Setting 0x1420D9078, .data 1.0): what the base constructor BSShadowLight 0x14155C4B0 leaves in light+0x540
    ''' (0x14155C56A / 0x14155C572). It is NOT the sun's: see <see cref="ShadowLightCtorBias"/>.</summary>
    Friend Const ShadowBiasScaleSetting As Single = 1.0F
    ''' <summary>light+0x540 of BSShadowDirectionalLight: its constructor 0x14157E490 calls the base at 0x14157E4BA and then overwrites the field with
    ''' 60.0 at 0x14157E8A3 (mov dword ptr [rbx+0x540], 0x42700000).</summary>
    Friend Const ShadowDirectionalLightBias As Single = 60.0F

    ''' <summary>The classes of shadow light of the exe (RTTI: BSShadowLight's three concrete classes; vtables 0x141B4E5F8, 0x141B4EFE0, 0x141B4F0F0). The virtuals +0x28 (Frustum
    ''' answers true, 0x141587F80) and +0x38 (Parabolic answers true, 0x141588E30) are what the mask's SetupGeometry tests for "directional" (0x1415683E4..3FD); the base's
    ''' 0x14155DBB0 / 0x14155DBC0 answer false.</summary>
    Friend Enum SseShadowLightClass
        ''' <summary>BSShadowDirectionalLight, ctor 0x14157E490: the sun, created only at 0x14150BDB4 and kept at ShadowSceneNode+0x210 (0x14150BDC1).</summary>
        Directional
        ''' <summary>BSShadowFrustumLight, ctor 0x1415872E0 (factory 0x14150C8D5..906).</summary>
        Frustum
        ''' <summary>BSShadowParabolicLight, ctor 0x141587F90 (factory 0x14150C915..963).</summary>
        Parabolic
    End Enum

    ''' <summary>THE CLASS OF A LIGHT OF THE PREVIEW'S RIG IN THE MASK PASS (the one pass that reads light+0x540: 0x141568906, 0x141568920). Every rig light that casts is a direction fitted with
    ''' an orthographic map (D-9; P1 draws the fills as added passes of the same lighting program, which does not read the field), so all of them are the class of a direction: the
    ''' sun's.</summary>
    Friend Const RigShadowLightClass As SseShadowLightClass = SseShadowLightClass.Directional

    ''' <summary>light+0x540 as the constructor chain of <paramref name="lightClass"/> leaves it: Directional 60.0 (0x14157E8A3, after the base's); Frustum and Parabolic keep the base's
    ''' fShadowBiasScale (their ctors 0x1415872E0 / 0x141587F90 call the base at 0x141587317 / 0x141587FBE and do not write it). Those two are then overwritten by their creators with
    ''' a value that is not the class's: LIGHT_CREATE_PARAMS +0x14 (0x14150C96B / 0x14150C970) or the LIGH form's (0x1403002CD); no such light exists in the preview, the caller of the
    ''' mask's law gives it. No creator writes the sun's.</summary>
    Friend Function ShadowLightCtorBias(lightClass As SseShadowLightClass) As Single
#If DEBUG Then
        ' ParityGate sse-constants-law [bias]: the base ctor's value instead of the class's.
        If Fo4GBufferConstants.GateMutantOn("sse-bias-ctor-base") Then Return ShadowBiasScaleSetting
#End If
        Return If(lightClass = SseShadowLightClass.Directional, ShadowDirectionalLightBias, ShadowBiasScaleSetting)
    End Function
    ''' <summary>[0x1434869B0] = 1.0 (0x1414EAEA7) and [0x1420D8FFC] = 1.0 (0x1414EAEB1), the loader's immediates.</summary>
    Friend Const ShadowBiasGlobal As Single = 1.0F
    Friend Const ShadowBiasZGlobal As Single = 1.0F

    ''' <summary>AlphaTestRef of the mask pass (cb2, SG 0x1415688B0..954; s1_re 4.2): s = directional ? fShadowDirectionalBiasScale :
    ''' 1.0 [0x141B5BE28] (0x1415688AD..906; the light is directional when its virtual +0x28 and +0x38 both answer false,
    ''' 0x1415683E4..3FD: <see cref="SseShadowLightClass"/>); .y = ((light+0x540 * 0.00025) * s) * [0x1434869B0] (0x141568906..946), .z = ((light+0x540 * 8.333e-5) *
    ''' [0x1420D8FFC]) * [0x1434869B0] (0x141568920..954): the sun's 0.0045 and 0.005, in the map's normalized depth (the PS subtracts them from M2 . P). Then the app's Shadow Bias knob multiplies both (PreviewLightRig
    ''' .ShadowBiasMultiplier, as it multiplies the SSE receiver's bias today; the game's own bias at multiplier 1). .x / .w are not written
    ''' by the mask pass (the record 11381 reads .y / .z; its alpha test is cb11).</summary>
    Friend Function MaskAlphaTestRef(lightClass As SseShadowLightClass, light540 As Single, shadowBiasMultiplier As Single) As Vector4
        Dim s As Single = If(lightClass = SseShadowLightClass.Directional, ShadowDirectionalBiasScale, 1.0F)
        Dim y As Single = (light540 * ShadowBiasYScale) * s
        Dim z As Single = (light540 * ShadowBiasZScale) * ShadowBiasZGlobal
        y = y * ShadowBiasGlobal
        z = z * ShadowBiasGlobal
        Dim b = shadowBiasMultiplier
#If DEBUG Then
        ' ParityGate sse-constants-law [bias]: the knob left out.
        If Fo4GBufferConstants.GateMutantOn("sse-bias-no-knob") Then b = 1.0F
#End If
        Return New Vector4(0.0F, y * b, z * b, 0.0F)
    End Function

    ''' <summary>fPoissonRadiusScale:Display (Setting 0x1420D9148, .data 4.0) = [0x1420D9150], read at 0x141566C2D / C4E.</summary>
    Friend Const PoissonRadiusScale As Single = 4.0F

    ''' <summary>ShadowSampleParam of the mask (cb0, ST 0x141566C12..C5F, technique bit 21): with the filter selector [0x1420D6844] in
    ''' 2..4 (add -2 / cmp 2 / ja, 0x141566C12..C1E; SseUtilityTechnique.ShadowFilterType, the one owner) .z = .w = fPoissonRadiusScale /
    ''' float(resolution) (0x141566C24..C5F; [0x1434869C0] = the shadow map's resolution); another filter writes neither. .x / .y not
    ''' written (the PS reads .zw).</summary>
    Friend Function ShadowSampleParamMask(shadowMapResolution As Integer) As Vector4
        Dim f As Long = CLng(SseUtilityTechnique.ShadowFilterType) - 2L      ' the unsigned compare of add -2 / cmp 2 / ja
        If f < 0L OrElse f > 2L Then Return Vector4.Zero
        Dim r As Single = PoissonRadiusScale / CSng(CLng(CUInt(shadowMapResolution)))
        Return New Vector4(0.0F, 0.0F, r, r)
    End Function

    ''' <summary>The split distances of the mask (cb0, ST 0x141566B1E..BDD, technique bit 21) from the sun's shadow light [[0x1420D68B0]
    ''' + 0x210]: n = light+0x140 (the splits, &lt;= 2), the view distances end[i] = light+0x5A4 + 4 i and start[i] = light+0x598 + 4 i,
    ''' the main camera's near / far [[0x1433D3D28] + 0x160 / + 0x164]. Each one to depth: (d f - f n) / (d (f - n)) (0x141566B48..B9A:
    ''' f n and f - n once, then mulss / subss / divss in that order). EndSplitDistances = (z(end 0), z(end 1), z(end n-1)
    ''' (0x141566BB0..BBD), float(n) (0x141566BC1..BCC)); StartSplitDistances = (z(start 0), z(start 1), -, float([0x141B49710] = 4))
    ''' (0x141566BD2..BDD). A split index &gt;= n is not written (0 here); StartSplitDistances.z is not written.</summary>
    Friend Function SplitDistances(splitEnds As Single(), splitStarts As Single(), near As Single, far As Single) As (EndSplit As Vector4, StartSplit As Vector4)
        Dim n = splitEnds.Length
        ' light+0x140 <= 2 (its one writer 0x14155C850 and its 4 callers, F1 informe 1); 0 would make the exe read end[-1].
        If n < 1 OrElse n > 2 OrElse splitStarts.Length <> n Then Throw New ArgumentOutOfRangeException(NameOf(splitEnds), "the splits are 1 or 2")
        Dim fn As Single = far * near
        Dim fmn As Single = far - near
        Dim e(1) As Single, s(1) As Single
        For i = 0 To n - 1
            Dim de = splitEnds(i)
            e(i) = (de * far - fn) / (de * fmn)
            Dim ds = splitStarts(i)
            s(i) = (ds * far - fn) / (ds * fmn)
        Next
        Return (New Vector4(e(0), e(1), e(n - 1), CSng(n)), New Vector4(s(0), s(1), 0.0F, CSng(StartSplitW)))
    End Function

    ''' <summary>iNumSplits:Display (Setting 0x1420D9050, .data 2): [light+0x140] of the sun (0x14150BD6C -&gt; 0x14157E587, 0x14155C850).</summary>
    Friend Const EngineSplitCount As Integer = 2
    ''' <summary>fFirstSliceDistance:Display (.data 1250, value 0x1420D9470) and its floor [0x141B5BFAC] = 500; fSplitOverlap:Display (.data 100, value 0x1420D94E8), [0x141B5BE44] = 2.</summary>
    Friend Const FirstSliceDistance As Single = 1250.0F
    Friend Const FirstSliceFloor As Single = 500.0F
    Friend Const SplitOverlap As Single = 100.0F

    ''' <summary>THE SPLIT DISTANCES OF THE SUN (0x14157F080: light +0x598 start[i], +0x5A4 end[i]; read by ST 0x141566B60 / B82), of the camera's near / far (+0x160, +0x164,
    ''' 0x14157F116): F = min(far, fShadowDistance [0x1434869A8]) (0x14157F12A..145). The slices (0x14157FF1C..FB5): t[0] = min(D / n, max(fFirstSliceDistance, 500)) / D with D = the
    ''' Setting's fShadowDistance (0x1420D9458); t[i] = ((1 - t[0]) i) / (n - 1) + t[0]. end[i] = ((near pow(F / near, t[i]) + ((F - near) t[i] + near)) 0.5 + fSplitOverlap (0x14158005F..08E,
    ''' 0.5 = [0x141B5BE14]); start[0] = near (xmm10 = near, 0x14157F7D7, stored 0x141580070), start[i + 1] = end[i] - fSplitOverlap 2 (0x141581657..678). Each split's camera is the engine's
    ''' over the preview's camera (EngineSunBox, user decision 6-nov-2026).</summary>
    Friend Function EngineSplitSlices(near As Single, far As Single, n As Integer) As (Ends As Single(), Starts As Single())
        Dim d As Single = ShadowDistance
        Dim f As Single = Math.Min(d, far)
        Dim t(n - 1) As Single
        t(0) = Math.Min(d / CSng(n), Math.Max(FirstSliceDistance, FirstSliceFloor)) / d
        For i = 1 To n - 1
            t(i) = ((1.0F - t(0)) * CSng(i)) * (1.0F / CSng(n - 1)) + t(0)
        Next
        Dim e(n - 1) As Single, s(n - 1) As Single
        Dim q As Single = f / near
        Dim span As Single = f - near
        s(0) = near
        For i = 0 To n - 1
            e(i) = (((span * t(i) + near) + MathF.Pow(q, t(i)) * near) * 0.5F) + SplitOverlap
            If i + 1 < n Then s(i + 1) = e(i) - SplitOverlap * 2.0F
        Next
        Return (e, s)
    End Function

    ''' <summary>The split's matrix ([light+0x148] + 0xF0 i; the D3DXMATRIX ShadowMapProj rebases, row-vector) from the rig's fit: world -&gt; (u, v, depth, w) of the map drawn in D3D rows:
    ''' u = (x + w) / 2, v = (w - y) / 2, depth = z of the fit's view x projection (the texture rows the reader takes, as Fo4DeferredLightLaw.ShadowMapProjRows).</summary>
    Friend Function SplitMatrixOfFit(viewProj As Matrix4) As Matrix4
        Dim c0 = viewProj.Column0, c1 = viewProj.Column1, c2 = viewProj.Column2, c3 = viewProj.Column3
        Dim u = 0.5F * c0 + 0.5F * c3
        Dim v = 0.5F * c3 - 0.5F * c1
        Return New Matrix4(u.X, v.X, c2.X, c3.X,
                           u.Y, v.Y, c2.Y, c3.Y,
                           u.Z, v.Z, c2.Z, c3.Z,
                           u.W, v.W, c2.W, c3.W)
    End Function

    ''' <summary>[0x141B49710] = 4 (cvtsi2ss of its dword, 0x141566BD2..BD8).</summary>
    Friend Const StartSplitW As Integer = 4

    ''' <summary>fShadowDistance:Display (Setting 0x1420D9450, .data 8000) = [0x1434869A8]; [0x1434869AC] = its square (mulss,
    ''' 0x1414EAE8B..E9F).</summary>
    Friend Const ShadowDistance As Single = 8000.0F
    ''' <summary>[0x14192D610] = 1e8, the fade distance of a light without its flag +0x63.</summary>
    Friend ReadOnly ShadowFadeOff As Single = BitConverter.UInt32BitsToSingle(&H4CBEBC20UI)

    ''' <summary>ShadowLightParam (PS cb2) of the mask techniques (0x141567685): .z = <see cref="LightFadeDistance"/> of the light's
    ''' byte +0x63 (0x1415676C8..6F6; the sun's is <see cref="LightByte63"/>(Nothing)); .x = [[light+0x48]+0x128] with technique bits
    ''' 23 / 24 (t AND &H1800000, 0x1415676FC..713), light +0x568 with bit 22 (0x141567715..71B) (0x141567721..72B), else not written
    ''' (XWritten false: the sun's mask technique has neither).</summary>
    Friend Function ShadowLightParam(t As UInteger, byte63 As Byte, niLight128 As Single, light568 As Single) As (Value As Vector4, XWritten As Boolean)?
        If Not UtlMaskTechnique(t) Then Return Nothing
        Dim z As Single = LightFadeDistance(byte63)
        If (t And &H1800000UI) <> 0UI Then Return (New Vector4(niLight128, 0.0F, z, 0.0F), True)
        If (t And &H400000UI) <> 0UI Then Return (New Vector4(light568, 0.0F, z, 0.0F), True)
        Return (New Vector4(0.0F, 0.0F, z, 0.0F), False)
    End Function

    ' =============================================================================================================================
    ' The rest of BSUtilityShader's names. t = the technique [shader +0x90] (0x141566865), the pass's property rbp = [pass+8] and
    ' geometry rbx = [pass+0x10] (0x1415674C2..CC); the PS group-2 holder r15 = [0x1420CFF50] (0x141567427; null = no PS bound).
    ' =============================================================================================================================

    ''' <summary>(t AND 0x20004000) = &H4000: the branch 0x1415678BA..8C6 / 0x141566DA4..DB0 that skips the geometry names below and
    ''' writes ParabolaParam.</summary>
    Friend Function UtlParaboloidBranch(t As UInteger) As Boolean
        Return (t And &H20004000UI) = &H4000UI
    End Function

    ''' <summary>The mask techniques: t AND &H1E00000 (bits 21..24; 0x1415668EA, 0x141567685).</summary>
    Friend Function UtlMaskTechnique(t As UInteger) As Boolean
        Return (t And &H1E00000UI) <> 0UI
    End Function

    ''' <summary>World[4] (VS cb2): without technique bit 2 (0x1415674E1): the geometry's world transform - a particle system's (geometry
    ''' vfunc +0x80 non-null, 0x1415674EA..4F9) with its translation moved to the world point of geometry +0x110..+0x118 c:
    ''' (((m[0][1] c.y + m[0][0] c.x) + m[0][2] c.z) s + t.x, ((m[1][1] c.y + m[1][0] c.x) + m[1][2] c.z) s + t.y, ((m[2][0] c.x +
    ''' m[2][1] c.y) + m[2][2] c.z) s + t.z) (0x1415674FF..60B) - through <see cref="SseLightingConstants.WorldRelativeMatrix"/>
    ''' (0x141567617 / 0x14156763C) and D3DXMatrixTranspose (0x14156765D). Then, outside the mask techniques and the paraboloid
    ''' branch, (t AND &H1E00100) = 0x100 rewrites it with the plain world transform (0x1415679D4..A05). Nothing = not written.</summary>
    Friend Function UtlWorld(t As UInteger, world As SseLightingConstants.SseNiTransform, particleCenter As Vector3?, eye As Vector3,
                             route As SseD3dx9.SseD3dxRoute) As Vector4()
        Dim m As Matrix4?
        If (t And 4UI) = 0UI Then
            Dim w = world
#If DEBUG Then
            ' ParityGate sse-constants-law [geometry]: the particle system's centre ignored.
            If Fo4GBufferConstants.GateMutantOn("sse-utlworld-noparticle") Then particleCenter = Nothing
#End If
            If particleCenter.HasValue Then
                Dim c = particleCenter.Value, r = world.Rotation, s = world.Scale, tr = world.Translation
                w.Translation = New Vector3(((r.M12 * c.Y + r.M11 * c.X) + r.M13 * c.Z) * s + tr.X,
                                            ((r.M22 * c.Y + r.M21 * c.X) + r.M23 * c.Z) * s + tr.Y,
                                            ((r.M31 * c.X + r.M32 * c.Y) + r.M33 * c.Z) * s + tr.Z)
            End If
            m = SseD3dx9.MatrixTranspose(route, SseLightingConstants.WorldRelativeMatrix(w, eye))
        End If
        If Not UtlMaskTechnique(t) AndAlso Not UtlParaboloidBranch(t) AndAlso (t And &H1E00100UI) = &H100UI Then
            m = SseD3dx9.MatrixTranspose(route, SseLightingConstants.WorldRelativeMatrix(world, eye))
        End If
        If Not m.HasValue Then Return Nothing
        Return {m.Value.Row0, m.Value.Row1, m.Value.Row2, m.Value.Row3}
    End Function

    ''' <summary>The geometry names the SG writes outside the mask techniques and the paraboloid branch (0x1415678BA..8C6).</summary>
    Private Function UtlGeometryBranch(t As UInteger) As Boolean
        Return Not UtlMaskTechnique(t) AndAlso Not UtlParaboloidBranch(t)
    End Function

    ''' <summary>[0x1420D69F4] (.data 0; written by the water code 0x1405299B0: 0x140529BE0, 0x140529C29, 0x140529C62).</summary>
    Friend Structure SseWaterState
        Public Depth As Single
    End Structure

    ''' <summary>WaterParams (VS cb2): with (t AND &H1200) = &H1200 (0x141567A1E..A2B) .x = [0x1420D69F4] (0x141567A9C..AAF).</summary>
    Friend Function WaterParams(t As UInteger, water As SseWaterState) As Vector4?
        If Not UtlGeometryBranch(t) OrElse (t And &H1200UI) <> &H1200UI Then Return Nothing
        Return New Vector4(water.Depth, 0.0F, 0.0F, 0.0F)
    End Function

    ''' <summary>EyePos (VS cb2): outside (t AND &H1200) = &H1200, with technique bit 9 (0x141567AC5): bit 10 also gives .xyz =
    ''' D3DXVec3TransformCoord(the accumulator eye [[0x1433D3DA8]+0x16C], the inverse world 0x140FDEEC0 of geom +0x7C)
    ''' (0x141567AD8..B07); .w = property +0x104 (0x141567B0C..B12). XyzWritten false = bit 10 clear.</summary>
    Friend Function UtlEyePos(t As UInteger, world As SseLightingConstants.SseNiTransform, accumulatorEye As Vector3, prop104 As Single,
                              route As SseD3dx9.SseD3dxRoute) As (Value As Vector4, XyzWritten As Boolean)?
        If Not UtlGeometryBranch(t) OrElse (t And &H1200UI) = &H1200UI OrElse (t And &H200UI) = 0UI Then Return Nothing
        If (t And &H400UI) = 0UI Then Return (New Vector4(0.0F, 0.0F, 0.0F, prop104), False)
        Dim p = SseD3dx9.Vec3TransformCoord(route, accumulatorEye, SseLightingConstants.InverseWorldMatrix(world))
        Return (New Vector4(p, prop104), True)
    End Function

    ''' <summary>[0x141820E34] = 128, [0x141820E20] = 1/127 (0x3C010204), [0x141820E1C] = 1/128, [0x141B5BF6C] = 255: the debug ramp.</summary>
    Friend ReadOnly DebugRampMid As Single = BitConverter.UInt32BitsToSingle(&H43000000UI)
    Friend ReadOnly DebugRampInv127 As Single = BitConverter.UInt32BitsToSingle(&H3C010204UI)
    Friend ReadOnly DebugRampInv128 As Single = BitConverter.UInt32BitsToSingle(&H3C000000UI)
    Friend ReadOnly DebugRamp255 As Single = BitConverter.UInt32BitsToSingle(&H437F0000UI)

    ''' <summary>What DebugColor reads: the debug mode [0x1420D6974] (.data 0; the console 0x140382940 writes it), the pass byte +0x1D,
    ''' the accumulator's map [acc+0xD0] lookup of the property's [+0x60] (found: the node's u32 +0x10) and its count [acc+0xF0], and the
    ''' accumulator's colour [acc+0x118..0x120] (acc = [0x1433D3DA8], 0x1414ECB90).</summary>
    Friend Structure SseDebugColorInputs
        Public DebugMode As Integer
        Public PassByte1D As Byte
        Public LookupFound As Boolean
        Public LookupValue As UInteger
        Public AccumulatorCount As UInteger
        Public AccumulatorColour As Vector3
    End Structure

    ''' <summary>DebugColor (PS cb2), with a PS (r15, 0x141567B1B): technique bit 17 (0x141567B2A): v = float(pass +0x1D)
    ''' (0x141567B3B..B44); with mode 0xA (0x141567B6F) v = float(found ? node +0x10 : 0) 255 / float(acc +0xF0) (0x141567B7C..C07);
    ''' then (min(max((v - 128) / 127 ... as mulss of 1/127, 0x141567C20..C3D)), min(max((128 - v) (1/128))), min(max((128 - |v - 128|)
    ''' (1/128))), 1.0) with minss 1.0 / maxss 0 (0x141567C2B..C7C); else technique bit 19 (0x141567C8C) = (acc +0x118, +0x11C, +0x120,
    ''' 1.0) (0x141567C9C..CC6).</summary>
    Friend Function DebugColor(t As UInteger, psBound As Boolean, d As SseDebugColorInputs) As Vector4?
        If Not UtlGeometryBranch(t) OrElse (t And &H1200UI) = &H1200UI OrElse Not psBound Then Return Nothing
        If (t And &H20000UI) <> 0UI Then
            Dim v As Single = CSng(CInt(d.PassByte1D))
            If d.DebugMode = &HA Then
                v = CSng(CLng(If(d.LookupFound, d.LookupValue, 0UI)))
                v = (v * DebugRamp255) / CSng(CLng(d.AccumulatorCount))
            End If
            Dim a As Single = v - DebugRampMid
            Dim b As Single = (DebugRampMid - v) * DebugRampInv128
            Dim x As Single = a * DebugRampInv127
            Dim c As Single = (DebugRampMid - Math.Abs(a)) * DebugRampInv128
            x = SseD3dx9.MaxPs(SseD3dx9.MinPs(x, 1.0F), 0.0F)
            b = SseD3dx9.MaxPs(SseD3dx9.MinPs(b, 1.0F), 0.0F)
            c = SseD3dx9.MaxPs(SseD3dx9.MinPs(c, 1.0F), 0.0F)
            Return New Vector4(x, b, c, 1.0F)
        End If
        If (t And &H80000UI) <> 0UI Then Return New Vector4(d.AccumulatorColour, 1.0F)
        Return Nothing
    End Function

    ''' <summary>What the z-prepass's AlphaTestRef .x / .w and PropertyColor read: the pass's NiAlphaProperty (0x141555400 of the pass:
    ''' flags +0x30 bit 0 = blend, ref +0x32), the effect data [property +0x68] (EffectData = its byte +0x7C, Nothing when the pointer
    ''' is null), the property's colour pointer +0x88 (x, y, z), alpha +0x30, flags +0x38, +0x108, the fade node [property +0x60]
    ''' +0x130 / +0x14C and the pass byte +0x1E bit 7.</summary>
    Friend Structure SseUtlAlphaInputs
        Public AlphaPropertyPresent As Boolean
        Public AlphaBlend As Boolean
        Public AlphaRef As Byte
        Public EffectData As Byte?
        Public PropertyColourPresent As Boolean
        Public PropertyColour As Vector3
        Public Alpha As Single
        Public Flags As ULong
        Public Prop108 As Single
        Public FadeNode130 As Single
        Public FadeNode14C As Single
        Public PassFadeBit As Boolean
    End Structure

    ''' <summary>The z-prepass block (0x141567E13..F8D) runs with a PS and (alpha property with blend, or technique bit 7 (js), or
    ''' (t AND &H14000) = &H10000) (0x141567E36..E5E).</summary>
    Private Function UtlAlphaBlockRuns(t As UInteger, psBound As Boolean, a As SseUtlAlphaInputs) As Boolean
        If Not psBound Then Return False
        Return (a.AlphaPropertyPresent AndAlso a.AlphaBlend) OrElse (t And &H80UI) <> 0UI OrElse (t And &H14000UI) = &H10000UI
    End Function

    ''' <summary>PropertyColor (PS cb2): in that block with technique bit 29 (0x141567E64..EBE) = (p.x, p.y, p.z) of the property's
    ''' colour pointer +0x88, or (1.0, 1.0) and the 1.0 left in xmm6 (0x14156767A) without it; .w = property +0x30.</summary>
    Friend Function PropertyColor(t As UInteger, psBound As Boolean, a As SseUtlAlphaInputs) As Vector4?
        If Not UtlAlphaBlockRuns(t, psBound, a) OrElse (t And &H20000000UI) = 0UI Then Return Nothing
        If a.PropertyColourPresent Then Return New Vector4(a.PropertyColour, a.Alpha)
        Return New Vector4(1.0F, 1.0F, 1.0F, a.Alpha)
    End Function

    ''' <summary>The z-prepass AlphaTestRef .x / .w (PS cb2, in that block): [rsp+0x30] = [property +0x68] only in the geometry
    ''' branch with technique bit 20 (0x1415678D3..8E1; 0 from 0x1415674D0 otherwise); .x = that pointer ? float(its +0x7C) (1/255)
    ''' (0x141567EC3..EE0, AlphaRefLaw.Scaled) : alpha property ? SseRenderPassLaw.PrepassThrG(blend, ref) (0x141567EE2..F27) : not written; with
    ''' (t AND &H14000) = &H10000 (0x141567F2C..F3C) .w = (flags AND &H4000 or 0x400000000000 ? property +0x108 : fade +0x130 alpha)
    ''' (0x141567F3E..F74), times fade +0x14C with the pass byte +0x1E bit 7 (0x141567F7A..F8D).</summary>
    Friend Function PrepassAlphaTestRef(t As UInteger, psBound As Boolean, a As SseUtlAlphaInputs) As (X As Single?, W As Single?)
        If Not UtlAlphaBlockRuns(t, psBound, a) Then Return (Nothing, Nothing)
        Dim x As Single? = Nothing
        If a.EffectData.HasValue AndAlso UtlGeometryBranch(t) AndAlso (t And &H100000UI) <> 0UI Then
            x = AlphaRefLaw.Scaled(CSng(CInt(a.EffectData.Value)))
        ElseIf a.AlphaPropertyPresent Then
            x = SseRenderPassLaw.PrepassThrG(a.AlphaBlend, a.AlphaRef)
        End If
        Dim w As Single? = Nothing
        If (t And &H14000UI) = &H10000UI Then
            Dim v As Single = If((a.Flags And &H4000UL) <> 0UL OrElse (a.Flags And &H400000000000UL) <> 0UL, a.Prop108, a.FadeNode130 * a.Alpha)
            If a.PassFadeBit Then v = v * a.FadeNode14C
            w = v
        End If
        Return (x, w)
    End Function

    ''' <summary>BaseColor (PS cb1, SM 0x141567010): technique bit 1 and t AND &H2040280 (0x1415670C0..0D4) and t AND &H2040080
    ''' (0x14156711E) and bit 29 (0x141567169) = (material +0x48 m, +0x4C m, +0x50 m, +0x54) with m = material +0x6C
    ''' (0x14156717A..1B6).</summary>
    Friend Function BaseColor(t As UInteger, colour As Vector4, scale As Single) As Vector4?
        If (t And 2UI) = 0UI OrElse (t And &H2040280UI) = 0UI OrElse (t And &H2040080UI) = 0UI OrElse (t And &H20000000UI) = 0UI Then Return Nothing
        Return New Vector4(colour.X * scale, colour.Y * scale, colour.Z * scale, colour.W)
    End Function

    ''' <summary>RefractionPower (PS cb1, SM): technique bit 1 and t AND &H2040280 and bit 9 (0x141567296) .x = material +0x84
    ''' (0x141567301..311).</summary>
    Friend Function RefractionPower(t As UInteger, power As Single) As Vector4?
        If (t And 2UI) = 0UI OrElse (t And &H2040280UI) = 0UI OrElse (t And &H200UI) = 0UI Then Return Nothing
        Return New Vector4(power, 0.0F, 0.0F, 0.0F)
    End Function

    ''' <summary>[0x1420D9658] / [0x1420D965C] (.data -1.0 / 1.0): written by the paraboloid light 0x141588C80 (0x141588CDD..D19).</summary>
    Friend Structure SseParaboloidState
        Public D9658 As Single
        Public D965C As Single
    End Structure

    ''' <summary>ParabolaParam (VS cb0, ST): the paraboloid branch with technique bit 16 (0x141566DA4..DD3) = (1.0 / [0x1420D9658],
    ''' [0x1420D965C]) (0x141566DD5..DFC). .zw not written.</summary>
    Friend Function ParabolaParam(t As UInteger, p As SseParaboloidState) As Vector4?
        If Not UtlParaboloidBranch(t) OrElse (t And &H10000UI) = 0UI Then Return Nothing
        Return New Vector4(1.0F / p.D9658, p.D965C, 0.0F, 0.0F)
    End Function

    ''' <summary>The BSLight byte +0x63 of a light: the BSLight ctor 0x141575930 stores 1 (0x141575A3F); ShadowSceneNode's light
    ''' factory 0x14150C810 then stores its creation parameters' byte +5 (0x14150CA3B..A3F). The sun (the directional shadow light
    ''' [[0x1420D68B0] + 0x210]) is made by the ShadowSceneNode ctor through BSShadowDirectionalLight's ctor 0x14157E490
    ''' (0x14150BDB4 -&gt; 0x14155C4B0 -&gt; 0x141575930) and stored at 0x14150BDC1: only the ctor writes it. No other store of any width
    ''' covering +0x63 in the light classes' functions or those that reach a ShadowSceneNode (cover8 sweep over .text).
    ''' <paramref name="factoryParam5"/> = Nothing for a light the factory did not make (the sun).</summary>
    Friend Function LightByte63(factoryParam5 As Byte?) As Byte
        If factoryParam5.HasValue Then Return factoryParam5.Value
#If DEBUG Then
        ' ParityGate sse-constants-law [light63]: the sun's byte taken as unset.
        If Fo4GBufferConstants.GateMutantOn("sse-light63-param") Then Return 0
#End If
        Return BSLightCtorByte63
    End Function

    ''' <summary>0x141575A3F: mov byte ptr [rdi+0x63], 1.</summary>
    Friend Const BSLightCtorByte63 As Byte = 1

    ''' <summary>The fade distance a light's byte +0x63 picks (0x1415676C8..6E0): set = [0x1434869AC] = fShadowDistance^2 (the settings
    ''' applier's mulss, 0x1414EAE8B..E9F); clear = [0x14192D610] = 1e8.</summary>
    Friend Function LightFadeDistance(byte63 As Byte) As Single
        Dim d As Single = ShadowDistance
        Return If(byte63 <> 0, d * d, ShadowFadeOff)
    End Function

    ''' <summary>[0x1417F6BA8] = 10000: the far of a light without its byte +0x63 in the shadow fade (0x1415677DE).</summary>
    Friend ReadOnly ShadowFadeFarOff As Single = BitConverter.UInt32BitsToSingle(&H461C4000UI)

    ''' <summary>What ShadowFadeParam .z reads (0x14156773E..841): the light's NiLight [light+0x48] world translation +0xA0..+0xA8 and
    ''' its +0x128, the camera node [0x1433D3E68] (written by 0x140655A10 / 0x1414E9F80) world translation +0xA0..+0xA8 and its
    ''' rotation column +0x7C / +0x88 / +0x94, and the main camera's near / far [[0x1433D3D28] + 0x160 / + 0x164].</summary>
    Friend Structure SseShadowFadeInputs
        Public LightPosition As Vector3
        Public Light128 As Single
        Public CameraPosition As Vector3
        Public CameraAxis As Vector3      ' (+0x7C, +0x88, +0x94)
        Public Near As Single
        Public Far As Single
    End Structure

    ''' <summary>ShadowFadeParam (VS cb2) of the mask techniques (0x141567685): .x = <see cref="LightFadeDistance"/> (0x1415676E0..6E8);
    ''' without technique bit 21 (0x14156772E) .z: d = ((dy a.y + dx a.x) + dz a.z) + light +0x128 with dv = light - camera
    ''' (0x14156776E..7C6), z = minss(byte63 ? fShadowDistance : 10000, d) (0x1415677CE..7ED), v = (z f - f n) / ((f - n) z)
    ''' (0x1415677F1..81B), then &gt; 1 gives 1 and not &gt;= 0 gives 0 (0x14156781F..82E) (0x141567841). The call to 0x141010620
    ''' (the light's rect +0x544..+0x550) sets the viewport, no constant.</summary>
    Friend Function ShadowFadeParam(t As UInteger, byte63 As Byte, f As SseShadowFadeInputs) As (Value As Vector4, ZWritten As Boolean)?
        If Not UtlMaskTechnique(t) Then Return Nothing
        Dim x As Single = LightFadeDistance(byte63)
        If (t And &H200000UI) <> 0UI Then Return (New Vector4(x, 0.0F, 0.0F, 0.0F), False)
        Dim dx As Single = f.LightPosition.X - f.CameraPosition.X
        Dim dy As Single = f.LightPosition.Y - f.CameraPosition.Y
        Dim dz As Single = f.LightPosition.Z - f.CameraPosition.Z
        Dim d As Single = ((dy * f.CameraAxis.Y + dx * f.CameraAxis.X) + dz * f.CameraAxis.Z) + f.Light128
        Dim z As Single = SseD3dx9.MinPs(If(byte63 <> 0, ShadowDistance, ShadowFadeFarOff), d)
        Dim v As Single = (z * f.Far - f.Far * f.Near) / ((f.Far - f.Near) * z)
#If DEBUG Then
        ' ParityGate sse-constants-law [utility]: the clamp to [0, 1] dropped.
        If Fo4GBufferConstants.GateMutantOn("sse-shadowfade-noclamp") Then Return (New Vector4(x, 0.0F, v, 0.0F), True)
#End If
        If v > 1.0F Then
            v = 1.0F
        ElseIf Not (v >= 0.0F) Then
            v = 0.0F
        End If
        Return (New Vector4(x, 0.0F, v, 0.0F), True)
    End Function

    ''' <summary>[0x14331C458..60]: .bss, no RIP writer (ripx --writes): (0, 0, 0). The ShadowSceneNode's +0x2E8 point (read with
    ''' [0x1433D3DB0] = 0x16 or 0x11, 0x14156837B..3A1) is the other source of the rebasing offset.</summary>
    Friend ReadOnly ShadowRebaseOrigin As Vector3 = Vector3.Zero

    ''' <summary>[0x1420D6811] (.data 1, no RIP writer): the rebasing takes +[0x14331C458] (0x141568352..379 jne).</summary>
    Friend Const FocusShadowKeyByte As Byte = 1

    ''' <summary>The point the split matrices are rebased on (0x141568352..3DB): o = [0x1420D6811] != 0 ? +[0x14331C458..60] :
    ''' -([0x1433D3DB0] = 0x16 or 0x11 ? [[0x1420D68B0]+0x2E8..] : [0x14331C458..]) (xorps with [0x141B5C730]); p = o + the frame
    ''' eye (addss, 0x1415683CA..3DB).</summary>
    Friend Function ShadowRebasePoint(eye As Vector3, mode3DB0 As Integer, ssn2E8 As Vector3) As Vector3
        Dim o As Vector3
        If FocusShadowKeyByte <> 0 Then
            o = ShadowRebaseOrigin
        Else
            Dim src = If(mode3DB0 = &H16 OrElse mode3DB0 = &H11, ssn2E8, ShadowRebaseOrigin)
            o = New Vector3(-src.X, -src.Y, -src.Z)
        End If
        Return New Vector3(o.X + eye.X, o.Y + eye.Y, o.Z + eye.Z)
    End Function

    ''' <summary>A split / focus matrix M rebased on p: row 3 c = M[3][c] + ((p.x M[0][c] + p.y M[1][c]) + p.z M[2][c])
    ''' (0x14156846E..567, 0x141568622..72E, 0x141568765..864), then D3DXMatrixTranspose (0x14156856D / 0x141568734 / 0x14156886A).</summary>
    Friend Function RebasedTranspose(m As Matrix4, p As Vector3, route As SseD3dx9.SseD3dxRoute) As Matrix4
        Dim r As Matrix4 = m
#If DEBUG Then
        ' ParityGate sse-constants-law [shadowproj]: the rebasing dropped.
        If Fo4GBufferConstants.GateMutantOn("sse-shadowproj-noeye") Then Return SseD3dx9.MatrixTranspose(route, r)
#End If
        r.M41 = m.M41 + ((p.X * m.M11 + p.Y * m.M21) + p.Z * m.M31)
        r.M42 = m.M42 + ((p.X * m.M12 + p.Y * m.M22) + p.Z * m.M32)
        r.M43 = m.M43 + ((p.X * m.M13 + p.Y * m.M23) + p.Z * m.M33)
        r.M44 = m.M44 + ((p.X * m.M14 + p.Y * m.M24) + p.Z * m.M34)
        Return SseD3dx9.MatrixTranspose(route, r)
    End Function

    ''' <summary>ShadowMapProj[9] (PS cb2, 0x141568300 called from the mask SG 0x1415676B5): a directional light (vfuncs +0x28 and
    ''' +0x38 false, 0x1415683E4..3FD): for each split i &lt; light +0x140 whose bit i is set in the pass byte +0x1D (0x141568430..43D),
    ''' rows 3 i .. 3 i + 2 = rows 0..2 of RebasedTranspose(the split's matrix [light+0x148] + 0xF0 i) (0x141568443..5A6); another light:
    ''' rows 0..3 = RebasedTranspose(the first one) (0x141568759..86A). Rows not reached stay Nothing.</summary>
    Friend Function ShadowMapProj(directional As Boolean, splitMatrices As Matrix4(), passSplitMask As Byte, p As Vector3,
                                  route As SseD3dx9.SseD3dxRoute) As Vector4?()
        Dim rows(8) As Vector4?
        If directional Then
            For i = 0 To splitMatrices.Length - 1
                If ((passSplitMask >> i) And 1) = 0 Then Continue For
                Dim r = RebasedTranspose(splitMatrices(i), p, route)
                rows(3 * i) = r.Row0 : rows(3 * i + 1) = r.Row1 : rows(3 * i + 2) = r.Row2
            Next
        Else
            Dim r = RebasedTranspose(splitMatrices(0), p, route)
            rows(0) = r.Row0 : rows(1) = r.Row1 : rows(2) = r.Row2 : rows(3) = r.Row3
        End If
        Return rows
    End Function

    ''' <summary>FocusShadowMapProj[12] (PS cb2): with the light's byte +0x558 (0x1415685CA) and focus shadows ([0x1433D3E90] &gt; 0,
    ''' 0x1415685D7): for each i &lt; count (at most 4: pushed while the count is below [0x1420D6814] = iNumFocusShadow:Display 4,
    ''' 0x14065A536..542) rows 3 i .. 3 i + 2 = rows 0..2 of RebasedTranspose(the light's inline matrix +0x160 + 0xF0 i)
    ''' (0x141568600..894).</summary>
    Friend Function FocusShadowMapProj(light558 As Boolean, focusMatrices As Matrix4(), p As Vector3, route As SseD3dx9.SseD3dxRoute) As Vector4?()
        Dim rows(11) As Vector4?
        If Not light558 Then Return rows
        For i = 0 To focusMatrices.Length - 1
            Dim r = RebasedTranspose(focusMatrices(i), p, route)
            rows(3 * i) = r.Row0 : rows(3 * i + 1) = r.Row1 : rows(3 * i + 2) = r.Row2
        Next
        Return rows
    End Function

    ''' <summary>fMaxFocusShadowMapDistance:Display (Setting 0x1420D9190, .data 450) = [0x1420D9198]; fFadingFracStart (Setting
    ''' 0x1420D9178, .data 0.25) = [0x1420D9180].</summary>
    Friend ReadOnly MaxFocusShadowMapDistance As Single = BitConverter.UInt32BitsToSingle(&H43E10000UI)
    Friend ReadOnly FadingFracStart As Single = BitConverter.UInt32BitsToSingle(&H3E800000UI)

    ''' <summary>FocusShadowFadeParam (PS cb0, ST, the mask techniques): r = fMaxFocusShadowMapDistance^2 (0x141566D1C..D2C); for each
    ''' i &lt; count, s = fFadingFracStart r (0x141566D40..54), d = the float at [0x1433D3E80] + 16 i (0x141566D58): d &gt;= s gives
    ''' (r - d) / (r - s), else 1.0 (0x141566D5D..D76), into component i (0x141566D7A). Components i &gt;= count are not written.</summary>
    Friend Function FocusShadowFadeParam(t As UInteger, focusDistances As Single()) As Single?()
        Dim c(3) As Single?
        If Not UtlMaskTechnique(t) Then Return c
        Dim r As Single = MaxFocusShadowMapDistance * MaxFocusShadowMapDistance
        For i = 0 To focusDistances.Length - 1
            Dim s As Single = FadingFracStart * r
            Dim d = focusDistances(i)
#If DEBUG Then
            ' ParityGate sse-constants-law [focus]: the start of the fade band dropped (every focus shadow fades).
            If Fo4GBufferConstants.GateMutantOn("sse-focusfade-noband") Then s = Single.NegativeInfinity
#End If
            c(i) = If(d >= s, (r - d) / (r - s), 1.0F)
        Next
        Return c
    End Function

    ''' <summary>TreeParams (VS cb2, technique bit 26, 0x141567CDC..E0D) = (a ? a +0x164 6 : 0, TreeParamsY, TreeFade, a ? a +0x160 :
    ''' 1.0) - the helpers are SseLightingConstants' (one owner).</summary>
    Friend Function UtlTreeParams(t As UInteger, a As SseLightingConstants.SseTreeAnim, band As SseLightingConstants.SseTreeFadeBand) As Vector4?
        If (t And &H4000000UI) = 0UI Then Return Nothing
        Return New Vector4(If(a.Present, a.F164 * SseLightingConstants.WindTimerScale, 0.0F), SseLightingConstants.TreeParamsY(),
                           SseLightingConstants.TreeFade(a, band), If(a.Present, a.F160, 1.0F))
    End Function

    ''' <summary>HighDetailRange (VS cb0, ST): (t AND &H1E00100) = 0x100 gives SseLightingConstants.HighDetailRangeLod (0x141566874..8DB);
    ''' technique bit 21 with [0x1420D6811] != 0 gives (EndSplitDistances .x, .y, .z, 0) (0x141566BE3..C0A).</summary>
    Friend Function UtlHighDetailRange(t As UInteger, lodBounds As Vector4, eye As Vector3, endSplit As Vector4) As Vector4?
        If (t And &H1E00100UI) = &H100UI Then Return SseLightingConstants.HighDetailRangeLod(lodBounds, eye)
        If (t And &H200000UI) <> 0UI AndAlso FocusShadowKeyByte <> 0 Then Return New Vector4(endSplit.X, endSplit.Y, endSplit.Z, 0.0F)
        Return Nothing
    End Function

    ''' <summary>[0x141B5BE6C] = 5.0, [0x141B5BDF8] = 0.2, [0x141B5BE14] = 0.5, [0x141B4E100] = 0.5: the split scheme of 0x141566E95..FB0.</summary>
    Friend ReadOnly SplitSchemeNear As Single = BitConverter.UInt32BitsToSingle(&H40A00000UI)
    Friend ReadOnly SplitSchemeScale As Single = BitConverter.UInt32BitsToSingle(&H3E4CCCCDUI)
    Friend ReadOnly SplitSchemeStep As Single = BitConverter.UInt32BitsToSingle(&H3F000000UI)
    Friend ReadOnly SplitSchemeBlend As Single = BitConverter.UInt32BitsToSingle(&H3F000000UI)

    ''' <summary>EndSplitDistances / StartSplitDistances / ShadowSampleParam outside technique bit 21 (ST):
    ''' <list type="bullet">
    ''' <item>a mask technique (0x141566C6B..D04): End = ([0x1434869B4] (written by the mask pass 0x141539B10, 0x141539D44, and
    ''' 0x14098A3C0), float(4)) (.zw not written); Start.w = float([0x141B49710] = 4) (0x141566C93..CA6); with the filter 2..4,
    ''' ShadowSampleParam = (-, -, 4 / res, 4 (4 / res)) (0x141566CC9..D04);</item>
    ''' <item>no mask, technique bit 18 and a PS (0x141566E95..FB0): for i = 0, 1: s = (float(i) + 1.0) 0.5; End[i] = (powf(D 0.2, s) 5)
    ''' (1 - 0.5) + ((D - 5) s + 5) 0.5 with D = [0x1434869A8] (the ucrt powf the exe imports, api-ms-win-crt-math powf, IAT
    ''' 0x1417C8E88, thunk 0x1415A98CA; Havok.Motor.Simd.PowCrt calls MathF.Pow, which coreclr 8 serves with the same api-set powf);
    ''' End .z / .w are the bytes [rsp+0x38..0x3F] the movups copies (0x141566F77..FB0): the RowPitch / DepthPitch the D3D11 runtime
    ''' returned in the D3D11_MAPPED_SUBRESOURCE of the PS cb0 Map (0x141566834..852, pMappedResource = rsp+0x30) - inputs, as bits.</item>
    ''' </list></summary>
    Friend Function SplitDistancesNoBit21(t As UInteger, psBound As Boolean, end86B4 As Single, shadowDistance As Single, shadowMapResolution As Integer,
                                          mapRowPitch As UInteger, mapDepthPitch As UInteger) As (EndSplit As Vector4?, StartW As Single?, SampleZW As Vector2?)
        If (t And &H200000UI) <> 0UI Then Return (Nothing, Nothing, Nothing)
        If UtlMaskTechnique(t) Then
            Dim e As New Vector4(end86B4, CSng(StartSplitW), 0.0F, 0.0F)
            Dim f As Long = CLng(SseUtilityTechnique.ShadowFilterType) - 2L
            Dim zw As Vector2? = Nothing
            If f >= 0L AndAlso f <= 2L Then
                Dim z As Single = PoissonRadiusScale / CSng(CLng(CUInt(shadowMapResolution)))
                zw = New Vector2(z, PoissonRadiusScale * z)
            End If
            Return (e, CSng(StartSplitW), zw)
        End If
        If (t And &H40000UI) = 0UI OrElse Not psBound Then Return (Nothing, Nothing, Nothing)
        Dim v(1) As Single
        Dim near As Single = SplitSchemeNear
        Dim dScaled As Single = shadowDistance * SplitSchemeScale
        Dim dSpan As Single = shadowDistance - near
        Dim keep As Single = 1.0F - SplitSchemeBlend
#If DEBUG Then
        ' ParityGate sse-constants-law [splits]: the logarithmic part's weight taken as the whole.
        If Fo4GBufferConstants.GateMutantOn("sse-split18-blend") Then keep = 1.0F
#End If
        For i = 0 To 1
            Dim s As Single = (CSng(i) + 1.0F) * SplitSchemeStep
            Dim lg As Single = Havok.Motor.Simd.PowCrt(dScaled, s) * near
            v(i) = lg * keep + ((dSpan * s) + near) * SplitSchemeBlend
        Next
        Return (New Vector4(v(0), v(1), BitConverter.UInt32BitsToSingle(mapRowPitch), BitConverter.UInt32BitsToSingle(mapDepthPitch)), Nothing, Nothing)
    End Function
    ''' <summary>The names of the utility blocks and where they live (see SseLightingConstants.Seats).</summary>
    Friend ReadOnly Seats As (Stage As String, Name As String, Kind As String, Seat As String)() = {
        ("utl-ps", "AlphaTestRef", "law", "MaskAlphaTestRef (.y / .z of the mask) and PrepassAlphaTestRef (.x / .w)"),
        ("utl-ps", "BaseColor", "law", "BaseColor"),
        ("utl-ps", "DebugColor", "law", "DebugColor"),
        ("utl-ps", "EndSplitDistances", "law", "SplitDistances (bit 21) and SplitDistancesNoBit21"),
        ("utl-ps", "FocusShadowFadeParam", "law", "FocusShadowFadeParam"),
        ("utl-ps", "FocusShadowMapProj", "law", "FocusShadowMapProj"),
        ("utl-ps", "PropertyColor", "law", "PropertyColor"),
        ("utl-ps", "RefractionPower", "law", "RefractionPower"),
        ("utl-ps", "ShadowLightParam", "law", "ShadowLightParam"),
        ("utl-ps", "ShadowMapProj", "law", "ShadowMapProj"),
        ("utl-ps", "ShadowSampleParam", "law", "ShadowSampleParamMask (bit 21) and SplitDistancesNoBit21"),
        ("utl-ps", "StartSplitDistances", "law", "SplitDistances (bit 21) and SplitDistancesNoBit21"),
        ("utl-ps", "VPOSOffset", "law", "SseLightingConstants.VposOffset"),
        ("utl-ps", "cb11_AlphaTestRef", "law", "SseLightingConstants.Cb11AlphaTestRef"),
        ("utl-ps", "scb12_44", "law", "SseLightingConstants.Cb12Row44"),
        ("utl-ps", "scb12_invviewproj", "law", "SseLightingConstants.Cb12InvViewProj"),
        ("utl-vs", "EyePos", "law", "UtlEyePos"),
        ("utl-vs", "HighDetailRange", "law", "UtlHighDetailRange"),
        ("utl-vs", "ParabolaParam", "law", "ParabolaParam"),
        ("utl-vs", "ShadowFadeParam", "law", "ShadowFadeParam"),
        ("utl-vs", "TexcoordOffset", "law", "SseLightingConstants.TexcoordOffset"),
        ("utl-vs", "TreeParams", "law", "UtlTreeParams"),
        ("utl-vs", "WaterParams", "law", "WaterParams"),
        ("utl-vs", "World", "law", "UtlWorld"),
        ("utl-vs", "scb12_eye", "law", "SseLightingConstants.Cb12Eye"),
        ("utl-vs", "scb12_view", "law", "SseLightingConstants.Cb12View"),
        ("utl-vs", "scb12_viewproj", "law", "SseLightingConstants.Cb12ViewProj")}
End Module
