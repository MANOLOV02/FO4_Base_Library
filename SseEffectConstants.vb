Option Strict On
Option Explicit On

Imports OpenTK.Mathematics

''' <summary>THE CONSTANTS OF THE SKYRIM SE EFFECT PROGRAMS (g5), BY NAME (Ola SSE S2 F2.3; s2_re.md 3): what BSEffectShader's
''' SetupTechnique 0x141556840 (cb0), SetupMaterial 0x141556C40 (cb1), SetupGeometry 0x141557090 (cb2) and its MEMBRANE branch
''' 0x141558160 write for each of the 32 names of the class (VS GetString 0x14158D470: 14, PS 0x14158D5C0: 18; their rows
''' Tools\ShaderTranscription\sse-common\sse_const_data.ROWS). SkyrimSE.exe 1.7.104.0. Single arithmetic in the exe's order; each function
''' takes its inputs explicitly (the material fields by their offset - read from the LIVE material, rev-17 -, the geometry's world, the
''' frame, the lights, the technique, the d3dx9 route) and returns the value BY NAME; a name the technique does not reach is Nothing (the
''' exe does not write it). The World, the fog and the camera laws are the shared seats (SseWorldMatrix, SseFogLaw, SseLightingConstants);
''' every name has its seat in <see cref="Seats"/> (ParityGate / ShadowGate --sse-effect-constants hold them).</summary>
Friend Module SseEffectConstants

    ' =============================================================================================================================
    ' VS
    ' =============================================================================================================================

    ''' <summary>World (VS cb2, 4 rows): not written with technique bit 3 SKINNED (SG 0x141557193..196 jumps past both writes). Particles
    ''' (bit 12, 0x1415571AD..2E3): the world transform's translation moved to the particle system's centre c = geometry +0x110..+0x118 -
    ''' (((m00 c.x + m01 c.y) + m02 c.z) s + t.x, ((m10 c.x + m11 c.y) + m12 c.z) s + t.y, ((m20 c.x + m21 c.y) + m22 c.z) s + t.z)
    ''' (0x1415571B7..298) - then 0x1415170C0 (0x14155729D); a mesh: 0x1415170C0 of the geometry's world (0x1415572E8..2EF). Then
    ''' D3DXMatrixTranspose into the name (0x14155736D..381): the rows of the transpose.</summary>
    Friend Function World(technique As UInteger, world0 As SseLightingConstants.SseNiTransform, particleCenter As Vector3?, eye As Vector3,
                          route As SseD3dx9.SseD3dxRoute) As Vector4()
        If (technique And 8UI) <> 0UI Then Return Nothing
        Dim m = SseD3dx9.MatrixTranspose(route, SseWorldMatrix.WorldRelativeMatrix(ParticleWorld(technique, world0, particleCenter), eye))
#If DEBUG Then
        ' ShadowGate --sse-coplanar (rev-16): the effect's World without the eye subtracted (the lit pair keeps it): the pair's positions part.
        If Fo4GBufferConstants.GateMutantOn("sse-eff-world-no-eye") Then m = SseD3dx9.MatrixTranspose(route, SseWorldMatrix.WorldRelativeMatrix(ParticleWorld(technique, world0, particleCenter), Vector3.Zero))
        ' ShadowGate --sse-effect-constants: the particle World on a mesh (the centre folded whatever the technique).
        If Fo4GBufferConstants.GateMutantOn("sse-eff-world-particles-mesh") AndAlso particleCenter.HasValue Then
            m = SseD3dx9.MatrixTranspose(route, SseWorldMatrix.WorldRelativeMatrix(ParticleWorld(technique Or &H1000UI, world0, particleCenter), eye))
        End If
#End If
        Return {m.Row0, m.Row1, m.Row2, m.Row3}
    End Function

    ''' <summary>The world transform SetupGeometry builds World from: with technique bit 12 the translation moved to the particle centre
    ''' (0x1415571B7..298, the arithmetic of SseUtilityConstants.UtlWorld's 0x1415674FF..60B, the same float order), else the geometry's.</summary>
    Friend Function ParticleWorld(technique As UInteger, world0 As SseLightingConstants.SseNiTransform, particleCenter As Vector3?) As SseLightingConstants.SseNiTransform
        If (technique And &H1000UI) = 0UI OrElse Not particleCenter.HasValue Then Return world0
        Dim c = particleCenter.Value, r = world0.Rotation, s = world0.Scale, tr = world0.Translation
        Dim w = world0
        w.Translation = New Vector3(((r.M11 * c.X + r.M12 * c.Y) + r.M13 * c.Z) * s + tr.X,
                                    ((r.M21 * c.X + r.M22 * c.Y) + r.M23 * c.Z) * s + tr.Y,
                                    ((r.M31 * c.X + r.M32 * c.Y) + r.M33 * c.Z) * s + tr.Z)
        Return w
    End Function

    ''' <summary>PreviousWorld (VS cb2, 4 rows; read only by the motion-vector code, ENGINE_MOTION_VECTORS - the app never defines it):
    ''' particles: the World matrix copied (0x1415572A2..2D7); a mesh: 0x1415170C0 of the geometry's previous world (+0xB0) with the frame
    ''' eye swapped to the previous one [0x1420CFF68..70] (0x1415572F4..364); not written with SKINNED; the transpose (0x141557386..399).</summary>
    Friend Function PreviousWorld(technique As UInteger, world0 As SseLightingConstants.SseNiTransform, previous As SseLightingConstants.SseNiTransform,
                                  particleCenter As Vector3?, eye As Vector3, previousEye As Vector3, route As SseD3dx9.SseD3dxRoute) As Vector4()
        If (technique And 8UI) <> 0UI Then Return Nothing
        If (technique And &H1000UI) <> 0UI Then Return World(technique, world0, particleCenter, eye, route)
#If DEBUG Then
        ' ParityGate sse-effect-constants-law directed mutant
        If Fo4GBufferConstants.GateMutantOn("sse-eff-prevworld-eye") Then previousEye = eye
#End If
        Dim m = SseD3dx9.MatrixTranspose(route, SseWorldMatrix.WorldRelativeMatrix(previous, previousEye))
        Return {m.Row0, m.Row1, m.Row2, m.Row3}
    End Function

    ''' <summary>FogParam / FogNearColor / FogFarColor (VS cb0, ST 0x1415568DA..9C1): the fog law of the class's SetupTechnique, the same
    ''' operations as Lighting's (SseFogLaw: off branch 0x141556978..989, FogNearColor.w = [0x1420D694C], 0x141556904..92F); the preview's
    ''' fog property (SseFogLaw.PreviewFog, user decision: no fog).</summary>
    Friend Function Fog() As (Param As Vector4, NearColor As Vector4, FarColor As Vector4, Color As Vector4)?
        Return SseFogLaw.FogLaw(SseFogLaw.PreviewFog())
    End Function

    ''' <summary>FalloffData (VS cb1, SM 0x141556FDD..FF0): with technique bit 8 the 16 bytes material +0x38..+0x44 = (falloff start
    ''' angle, stop angle, start opacity, stop opacity).</summary>
    Friend Function FalloffData(technique As UInteger, startAngle As Single, stopAngle As Single, startOpacity As Single, stopOpacity As Single) As Vector4?
        If (technique And &H100UI) = 0UI Then Return Nothing
        Return New Vector4(startAngle, stopAngle, startOpacity, stopOpacity)
    End Function

    ''' <summary>SoftMaterialVSParams (VS cb1, SM 0x141556FC8..FD4): with technique bit 18, .x = material +0x68 (softDepth).</summary>
    Friend Function SoftMaterialVSParams(technique As UInteger, softDepth As Single) As Vector4?
        If (technique And &H40000UI) = 0UI Then Return Nothing
#If DEBUG Then
        ' ShadowGate --soft-law [soft-depth-zero]: softDepth 0 guarded (the engine's +Inf / NaN avoided).
        If Fo4GBufferConstants.GateMutantOn("sse-eff-softdepth-guard") AndAlso softDepth = 0.0F Then Return New Vector4(1.0F, 0.0F, 0.0F, 0.0F)
#End If
        Return New Vector4(softDepth, 0.0F, 0.0F, 0.0F)
    End Function

    ''' <summary>TexcoordOffset (VS cb1, SM 0x141556E0B..E4A, technique bit 6): material +0xC / +0x10 / +0x1C / +0x20 of the side
    ''' [0x1420D69D0] - SseLightingConstants.TexcoordOffset, the one owner of the two classes' law (F0.5: the loader fills both sides with
    ''' the NIF's, 0x1414E7534..7562, so the value at rest is the live material's whatever the side).</summary>
    Friend Function TexcoordOffset(technique As UInteger, offset As Vector2, scale As Vector2, frameIndex As Integer) As Vector4?
        If (technique And &H40UI) = 0UI Then Return Nothing
#If DEBUG Then
        ' ShadowGate --sse-effect-constants: the other side of the parity (the scale where the offset goes).
        If Fo4GBufferConstants.GateMutantOn("sse-eff-texoff-side") Then Return New Vector4(scale.X, scale.Y, offset.X, offset.Y)
#End If
        Return SseLightingConstants.TexcoordOffset({offset, offset}, {scale, scale}, frameIndex)
    End Function

    ''' <summary>The rows of SubTexOffset (VS cb11, SG 0x141557A78..B30, technique bit 7): a buffer of 128 rows (0x1410104F0, edx 0x80),
    ''' the n = [[geom + 0x158] + 0x68] rows of the particle data [[geom + 0x158] + 0x60] (0x14010C240, n * 16 bytes) and 0 in the rows
    ''' n..127 (0x141557AD4..AFD, memset 0x1415A97F8; none when n &gt;= 128, jae 0x141557B02).</summary>
    Friend Const SubTexOffsetRows As Integer = &H80

    Friend Function SubTexOffset(technique As UInteger, particleRows As Vector4()) As Vector4()
        If (technique And &H80UI) = 0UI Then Return Nothing
        Dim r = New Vector4(SubTexOffsetRows - 1) {}
        Dim n = If(particleRows Is Nothing, 0, Math.Min(particleRows.Length, SubTexOffsetRows))
        For i = 0 To n - 1 : r(i) = particleRows(i) : Next
#If DEBUG Then
        ' ParityGate sse-effect-constants-law directed mutant
        If Fo4GBufferConstants.GateMutantOn("sse-eff-subtex-fill-one") Then For i = n To SubTexOffsetRows - 1 : r(i) = New Vector4(1.0F) : Next
#End If
        Return r
    End Function

    ''' <summary>MatProj (VS cb2, 4 rows, SG 0x141557BE3..C08, technique bit 17 PROJECTED_UV): D3DXMatrixTranspose of the 64 bytes at
    ''' geometry +0x16C (no test of the geometry's type, F0.1: only BSMultiIndexTriShape has them; a NIF's shape ends before, so the
    ''' preview never has the input: <paramref name="geometryProjection"/> Nothing = no value, the draw is not made, U1).</summary>
    Friend Function MatProj(technique As UInteger, geometryProjection As Matrix4?, route As SseD3dx9.SseD3dxRoute) As Vector4()
        If (technique And &H20000UI) = 0UI OrElse Not geometryProjection.HasValue Then Return Nothing
        Dim m = SseD3dx9.MatrixTranspose(route, geometryProjection.Value)
#If DEBUG Then
        ' ParityGate sse-effect-constants-law directed mutant
        If Fo4GBufferConstants.GateMutantOn("sse-eff-matproj-no-transpose") Then m = geometryProjection.Value
#End If
        Return {m.Row0, m.Row1, m.Row2, m.Row3}
    End Function

    ' =============================================================================================================================
    ' PS
    ' =============================================================================================================================

    ''' <summary>PropertyColor (PS cb2, SG 0x141557B3B..B82, 0x141557C72..C94): xyz = the three floats at the property's colour pointer
    ''' [prop + 0x88] or (1, 1, 1) when it is null (immediates 0x141557B65 / B6D; .z = xmm6 = 1.0 from [0x141B5BE28], 0x141557416, the
    ''' only other writer of xmm6 up to 0x141557B76 being 0x141557B5E - out\xmm6_trace.txt); the constructor nulls it (0x141529A26..A28) and
    ''' no loader writes it: (1, 1, 1) at rest. w = prop +0x30 (the alpha with the fade; fade 1 in the preview), times the fade node's
    ''' +0x14C when the pass byte +0x1E has bit 7 (0x141557C72..C94).</summary>
    Friend Function PropertyColor(colourPointer As Vector3?, propAlpha As Single, passFadeBit As Boolean, fadeNode14C As Single) As Vector4
        Dim c = If(colourPointer, New Vector3(1.0F, 1.0F, 1.0F))
        Dim w = propAlpha
        If passFadeBit Then w = fadeNode14C * w
#If DEBUG Then
        ' ParityGate sse-effect-constants-law directed mutant
        If Fo4GBufferConstants.GateMutantOn("sse-eff-propcolor-nofade") Then w = propAlpha
#End If

        Return New Vector4(c, w)
    End Function

    ''' <summary>The two weapon-blood globals [0x1434866F0] / [0x1434866F4] (fWeaponBloodFalloffThreshold, fWeaponBloodAlphaToRGBScale):
    ''' .data 0, one writer 0x141558820 called only by the game's temporary blood (0x1404B5F79 ..): 0 at rest (s2_re.md 5, user decision
    ''' 6-oct-2026 "the game's formula at rest").</summary>
    Friend Const BloodFalloffThresholdAtRest As Single = 0.0F
    Friend Const BloodAlphaToRgbScaleAtRest As Single = 0.0F

    ''' <summary>AlphaTestRef (PS cb2) with technique bit 14 BLOOD (SG 0x141557B87..BD2): (cvtdq2ps(NiAlphaProperty +0x32) x [0x141B5BDC8]
    ''' (1/255), [0x1434866F0], [0x1434866F4]); .w not written. The reference of the property 0x141555400 gives (the shape's).</summary>
    Friend Function AlphaTestRefBlood(technique As UInteger, alphaRef As Byte, falloffThreshold As Single, alphaToRgbScale As Single) As Vector4?
        If (technique And &H4000UI) = 0UI Then Return Nothing
#If DEBUG Then
        ' ShadowGate --sse-effect-constants / --sse-blood: the globals not at rest.
        If Fo4GBufferConstants.GateMutantOn("sse-eff-blood-globals") Then falloffThreshold = 0.5F : alphaToRgbScale = 1.0F
#End If
        Return New Vector4(CSng(CInt(alphaRef)) * AlphaRefLaw.Inv255, falloffThreshold, alphaToRgbScale, 0.0F)
    End Function

    ''' <summary>A light of an effect draw as SetupGeometry reads it (the pass's list 0x14154D070, slot 8 (k + 1) for the point light k,
    ''' 0x14155755D..56D): the BSLight factor +0x14, its NiLight [+0x48]: dimmer +0x134, diffuse +0x11C..+0x124, world translation
    ''' +0xA0..+0xA8, radius +0x128. Light 0 (DLightColor, 0x141557435..489) reads the dimmer and +0x14C..+0x154.</summary>
    Friend Structure SseEffectLight
        Public Factor As Single
        Public Dimmer As Single
        Public Diffuse As Vector3
        Public WorldPosition As Vector3
        Public Radius As Single
    End Structure

    ''' <summary>THE LIGHTS OF AN EFFECT (PS cb2 4..11, SG 0x14155741E..A78), with technique bit 16 LIGHTING and the pass's light count
    ''' [pass +0x1F] above 0 (0x14155741E..42F; otherwise none of the 8 names is written). DLightColor = [ISM +0xE0] x dimmer of light 0
    ''' times its colour (0x141557449..489; .w not written). n = min(count, 5) (0x1415574FD..50D): the point light k + 1 (k &lt; n - 1)
    ''' writes component k of the seven SoA names (the loop 0x141557516..74E): colour = (factor x dimmer) x diffuse (0x141557575..5AD);
    ''' position, SKINNED (0x1415575E2..65D) = (world - G) + V with radius +0x128, else (0x141557662..6F6) D3DXVec3TransformCoord(world
    ''' + V, the inverse world 0x140FDEEC0) with radius / the geometry's scale (+0xAC); RadiusInverseSquared = 1.0 / r^2 (xmm6 = 1.0,
    ''' 0x14155772F..744). Then components n - 1..3 = 0 in the seven (0x14155776D..A6B). G = [0x1420CFF5C..64] (cb12[40]'s source);
    ''' V = [0x1433D3DB0] in {0x11, 0x16} ? [[0x1420D68B0] + 0x2E8] : 0 (0x141557618..655; the world pass mode 0: V = 0, s2_re.md 3.3).
    ''' Returns Nothing when the eight names are not written.</summary>
    Friend Function EffectLights(technique As UInteger, lights As IReadOnlyList(Of SseEffectLight), ismE0 As Single, light0Colour As Vector3,
                                 skinned As Boolean, world As SseLightingConstants.SseNiTransform, g As Vector3, v As Vector3,
                                 route As SseD3dx9.SseD3dxRoute) As (DLightColor As Vector4, SoA As Vector4())?
        If (technique And &H10000UI) = 0UI OrElse lights Is Nothing OrElse lights.Count = 0 Then Return Nothing
        Dim d0 = ismE0 * lights(0).Dimmer
        Dim dl As New Vector4(d0 * light0Colour.X, d0 * light0Colour.Y, d0 * light0Colour.Z, 0.0F)
#If DEBUG Then
        ' ShadowGate --sse-effect-constants: DLightColor without the light's dimmer (fade).
        If Fo4GBufferConstants.GateMutantOn("sse-eff-dlight-nofade") Then dl = New Vector4(ismE0 * light0Colour.X, ismE0 * light0Colour.Y, ismE0 * light0Colour.Z, 0.0F)
#End If
        ' SoA(i) = PLightPositionX, Y, Z, PLightingRadiusInverseSquared, PLightColorR, G, B (names PS 4..10)
        Dim soa = New Vector4(6) {}
        Dim c(6, 3) As Single
        Dim n = Math.Min(lights.Count, 5)
        Dim inv = If(skinned, Matrix4.Identity, SseWorldMatrix.InverseWorldMatrix(world))
        For k = 0 To n - 2
            Dim L = lights(k + 1)
            Dim p As Single = L.Factor * L.Dimmer
            Dim pos As Vector3, r As Single
            If skinned Then
                pos = New Vector3((L.WorldPosition.X - g.X) + v.X, (L.WorldPosition.Y - g.Y) + v.Y, (L.WorldPosition.Z - g.Z) + v.Z)
                r = L.Radius
            Else
                pos = SseD3dx9.Vec3TransformCoord(route, New Vector3(L.WorldPosition.X + v.X, L.WorldPosition.Y + v.Y, L.WorldPosition.Z + v.Z), inv)
                r = L.Radius / world.Scale
#If DEBUG Then
                ' ParityGate sse-effect-constants-law directed mutant (the tester's MB)
                If Fo4GBufferConstants.GateMutantOn("sse-eff-light-radius-noscale") Then r = L.Radius
#End If
            End If
            c(0, k) = pos.X : c(1, k) = pos.Y : c(2, k) = pos.Z
            c(3, k) = 1.0F / (r * r)
            c(4, k) = p * L.Diffuse.X : c(5, k) = p * L.Diffuse.Y : c(6, k) = p * L.Diffuse.Z
        Next
        Dim fillFrom = n - 1
#If DEBUG Then
        ' ShadowGate --sse-effect-constants: the lights without the fill to 0 (the components after the point lights keep a value).
        If Fo4GBufferConstants.GateMutantOn("sse-eff-lights-nofill") Then
            For i = 0 To 6
                For k = fillFrom To 3 : c(i, k) = 1.0F : Next
            Next
        End If
#End If
        For i = 0 To 6 : soa(i) = New Vector4(c(i, 0), c(i, 1), c(i, 2), c(i, 3)) : Next
        Return (dl, soa)
    End Function

    ''' <summary>VPOSOffset (PS cb0, ST 0x141556AA0..ACF, technique bit 18): (1 / W, 1 / H, 0, 0) of the render target
    ''' ([0x14333561C] / [0x143335620]; .zw the qword r15 = 0, 0x141556AC1) - SseLightingConstants.VposOffset's law.</summary>
    Friend Function VposOffset(technique As UInteger, width As Integer, height As Integer) As Vector4?
        If (technique And &H40000UI) = 0UI Then Return Nothing
#If DEBUG Then
        ' ParityGate sse-effect-constants-law directed mutant
        If Fo4GBufferConstants.GateMutantOn("sse-eff-vpos-swap") Then Return SseLightingConstants.VposOffset(height, width)
#End If
        Return SseLightingConstants.VposOffset(width, height)
    End Function

    ''' <summary>CameraData (PS cb0, ST 0x141556A3D..A72, technique bit 18): c = [0x1433D3D28] (the camera's frustum), .y = c +0x160
    ''' (near), .z = c +0x164 - c +0x160 (far - near); .x / .w not written and no PS reads them (rev-18).</summary>
    Friend Function CameraData(technique As UInteger, near As Single, far As Single) As Vector4?
        If (technique And &H40000UI) = 0UI Then Return Nothing
#If DEBUG Then
        ' ParityGate sse-effect-constants-law directed mutant
        If Fo4GBufferConstants.GateMutantOn("sse-eff-cameradata-far") Then Return New Vector4(0.0F, near, far, 0.0F)
#End If
        Return New Vector4(0.0F, near, far - near, 0.0F)
    End Function

    ''' <summary>FilteringParam (PS cb0, ST 0x141556B55..B7C, every technique): (fFilteringWaterDepthThreshold [0x1420D8FD0],
    ''' fFilteringAlphaThreshold [0x1420D8FE8]) - their .data values 0.005 (0x3BA3D70A) and 0.01 (0x3C23D70A) (decision 3-oct: exe
    ''' defaults, not the INI).</summary>
    Friend ReadOnly FilteringWaterDepthThreshold As Single = BitConverter.UInt32BitsToSingle(&H3BA3D70AUI)
    Friend ReadOnly FilteringAlphaThreshold As Single = BitConverter.UInt32BitsToSingle(&H3C23D70AUI)

    Friend Function FilteringParam() As Vector4
#If DEBUG Then
        ' ParityGate sse-effect-constants-law directed mutant
        If Fo4GBufferConstants.GateMutantOn("sse-eff-filtering-swap") Then Return New Vector4(FilteringAlphaThreshold, FilteringWaterDepthThreshold, 0.0F, 0.0F)
#End If
        Return New Vector4(FilteringWaterDepthThreshold, FilteringAlphaThreshold, 0.0F, 0.0F)
    End Function

    ''' <summary>BaseColor (PS cb1, SM 0x141556EFB..F5B): with technique bit 19 (palette colour) the 16 bytes material +0x48..+0x54 as
    ''' they are (0x141556F10..F14); else (+0x48 x +0x6C, +0x4C x +0x6C, +0x50 x +0x6C, +0x54) (0x141556F29..F5B: mulss +0x6C x +0x48,
    ''' then +0x4C x +0x6C, +0x50 x +0x6C). +0x48..+0x50 = the base colour as the engine keeps it (the file's colour / 255, raw in Skyrim
    ''' SE), +0x54 its alpha, +0x6C the base colour scale (the effect's emissive multiple).</summary>
    Friend Function BaseColor(technique As UInteger, baseColour As Vector3, baseAlpha As Single, scale As Single) As Vector4
        If (technique And &H80000UI) <> 0UI Then Return New Vector4(baseColour, baseAlpha)
        Return New Vector4(scale * baseColour.X, baseColour.Y * scale, baseColour.Z * scale, baseAlpha)
    End Function

    ''' <summary>BaseColorScale (PS cb1, SM 0x141556F18..F24, technique bit 19): .x = material +0x6C.</summary>
    Friend Function BaseColorScale(technique As UInteger, scale As Single) As Vector4?
        If (technique And &H80000UI) = 0UI Then Return Nothing
        Return New Vector4(scale, 0.0F, 0.0F, 0.0F)
    End Function

    ''' <summary>The lighting-influence byte material +0x81 as the engine holds it: LoadBinary writes the file's byte only when it is not 0
    ''' (0x14152952D) over the constructor's 0xFF - a 0 in the file reads 255; the truncation is the shared seat FO4UnifiedMaterial_Class.LightingInfluenceByte.</summary>
    Friend Function InfluenceByte(lightingInfluence As Single) As Integer
        Dim b = FO4UnifiedMaterial_Class.LightingInfluenceByte(lightingInfluence)
        If b = 0 Then b = &HFF
        Return b
    End Function

    ''' <summary>LightingInfluence (PS cb1, SM 0x141556F5F..FC1): .x = cvtdq2ps(byte material +0x81) x [0x141B5BDC8] (1/255,
    ''' 0x141556F5F..F83); with technique bit 18 .y = (TLS +0x9F8 x TLS +0x9FC) / material +0x68 (0x141556F95..FC1) - the far and the
    ''' near SetupTechnique stores there (0x141556A89..A9B: +0x9FC = near, +0x9F8 = far). .zw not written.</summary>
    Friend Function LightingInfluence(technique As UInteger, influenceByte As Integer, near As Single, far As Single, softDepth As Single) As Vector4
        Dim x As Single = CSng(influenceByte) * AlphaRefLaw.Inv255
        Dim y As Single = 0.0F
        If (technique And &H40000UI) <> 0UI Then
            y = (far * near) / softDepth
#If DEBUG Then
            ' ShadowGate --sse-effect-constants / --soft-law: .y without the division by softDepth.
            If Fo4GBufferConstants.GateMutantOn("sse-eff-li-nosoftdepth") Then y = far * near
#End If
        End If
        Return New Vector4(x, y, 0.0F, 0.0F)
    End Function

    ' =============================================================================================================================
    ' MEMBRANE 0x141558160 (technique bit 15: the effectData pass of a BSLightingShaderProperty; U2, no caller in the preview)
    ' =============================================================================================================================

    ''' <summary>The BSEffectShaderData fields 0x141558160 reads ([prop + 0x68], 0x141558186): +0x2C..+0x38 the fill colour, +0x3C..+0x48
    ''' the rim colour, +0x4C the fill colour's alpha scale, +0x58..+0x64 the texcoord offset / scale, +0x68 the membrane var, +0x70 /
    ''' +0x74 the source / destination blend.</summary>
    Friend Structure SseEffectShaderData
        Public FillColor As Vector4        ' +0x2C
        Public RimColor As Vector4         ' +0x3C
        Public FillAlphaScale As Single    ' +0x4C
        Public Texcoord As Vector4         ' +0x58
        Public Var68 As Single             ' +0x68
        Public SrcBlend As Integer         ' +0x70
        Public DstBlend As Integer         ' +0x74
    End Structure

    ''' <summary>What 0x141558160 writes: the names (Nothing = not written) and the alpha mode its tail selects ([0x1420CFC50 + ...], the
    ''' render state 0 = none).</summary>
    Friend Structure SseMembraneConstants
        Public PropertyColor As Vector4
        Public MembraneVars As Vector4
        Public MembraneRimColor As Vector4
        Public World As Vector4()
        Public PreviousWorld As Vector4()
        Public TexcoordOffsetMembrane As Vector4
        Public EyePosition As Vector4
        Public PosAdjust As Vector4
        Public AlphaTestRef As Vector4?
        Public BlendMode As Integer
    End Structure

    ''' <summary>THE MEMBRANE LAW 0x141558160 (s2_re.md 3.1, 3.2, 3.4, 3.6; F0.2):
    ''' <list type="bullet">
    ''' <item>MembraneVars.x = ed +0x68 (0x14155819F..1B0); with technique bit 19 PropertyColor = ed +0x2C..+0x38 as they are and
    ''' MembraneVars.z = ed +0x4C (0x1415581B4..1CC); else PropertyColor = (ed +0x2C..+0x38) each x ed +0x4C (0x1415581D3..21E).</item>
    ''' <item>MembraneRimColor = ed +0x3C..+0x48 (0x141558223..23B).</item>
    ''' <item>World / PreviousWorld = D3DXMatrixTranspose of the matrices SetupGeometry hands in r8 / r9 (0x14155823F..263: the World of
    ''' the geometry, SseEffectConstants.World's matrix, and the previous one).</item>
    ''' <item>TexcoordOffsetMembrane = ed +0x58..+0x64 (0x141558268..289).</item>
    ''' <item>EyePosition = D3DXVec3TransformCoord(the accumulator's eye [[0x1433D3DA8] + 0x16C], the inverse world SetupGeometry passes
    ''' at [rsp + 0x80]) (0x14155828D..2AE): SseLightingConstants.LitEyePosition's model-space branch (one seat); .w not written.</item>
    ''' <item>PosAdjust = G [0x1420CFF5C..64] (0x1415582B3..2E0), then .x / .y rewritten (0x1415582E6..324): mode [0x1433D3DB0] = 0x16:
    ''' (-k - k, k + k) with k = [0x1420D69E8]; else (f +0xEC - f +0xF0, f +0xF0 + f +0xF0), f = [prop + 0x60] the fade node (its world
    ''' bound's centre .z and radius, the NiAVObject ctor 0x140EE10B0 +0xEC / +0xF0): (base, height, G.z); .w not written.</item>
    ''' <item>AlphaTestRef with technique bit 23 (0x1415583EA..436): cvtdq2ps(NiAlphaProperty +0x32) x 1/255 + [0x141B5BDC8] bias, one
    ''' more 1/255 for ref 4 (SseRenderPassLaw.PrepassThrG's unblended arithmetic).</item>
    ''' <item>The alpha mode (0x14155843C..4D9): (src, dst) = (6, 7) 1, (6, 0) / (6, 9) 2, (4, 7) 3, (1, 2) 4, else unchanged (0).</item>
    ''' </list></summary>
    Friend Function Membrane(technique As UInteger, ed As SseEffectShaderData, worldMatrix As Matrix4, previousMatrix As Matrix4,
                             world As SseLightingConstants.SseNiTransform, accumulatorEye As Vector3, g As Vector3, mode3DB0 As Integer,
                             k As Single, fadeBoundCenterZ As Single, fadeBoundRadius As Single, alphaRef As Byte,
                             route As SseD3dx9.SseD3dxRoute) As SseMembraneConstants
        Dim r As New SseMembraneConstants
        If (technique And &H80000UI) <> 0UI Then
            r.PropertyColor = ed.FillColor
            r.MembraneVars = New Vector4(ed.Var68, 0.0F, ed.FillAlphaScale, 0.0F)
        Else
            Dim s = ed.FillAlphaScale
            r.PropertyColor = New Vector4(ed.FillColor.X * s, ed.FillColor.Y * s, ed.FillColor.Z * s, ed.FillColor.W * s)
            r.MembraneVars = New Vector4(ed.Var68, 0.0F, 0.0F, 0.0F)
        End If
        r.MembraneRimColor = ed.RimColor
        Dim wt = SseD3dx9.MatrixTranspose(route, worldMatrix), pt = SseD3dx9.MatrixTranspose(route, previousMatrix)
        r.World = {wt.Row0, wt.Row1, wt.Row2, wt.Row3}
        r.PreviousWorld = {pt.Row0, pt.Row1, pt.Row2, pt.Row3}
        r.TexcoordOffsetMembrane = ed.Texcoord
        Dim ep = SseLightingConstants.LitEyePosition(True, True, world, accumulatorEye, Vector3.Zero, route)
#If DEBUG Then
        ' ShadowGate --sse-membrane-law: EyePosition without inv(World) (the world branch).
        If Fo4GBufferConstants.GateMutantOn("sse-membrane-eye-world") Then ep = SseLightingConstants.LitEyePosition(True, False, world, accumulatorEye, Vector3.Zero, route)
#End If
        r.EyePosition = ep.Value
        Dim px As Single, py As Single
        If mode3DB0 = &H16 Then
            px = -k - k : py = k + k
        Else
            px = fadeBoundCenterZ - fadeBoundRadius : py = fadeBoundRadius + fadeBoundRadius
        End If
#If DEBUG Then
        ' ShadowGate --sse-membrane-law: PosAdjust .x / .y left at G (without the second write).
        If Fo4GBufferConstants.GateMutantOn("sse-membrane-posadjust-g") Then px = g.X : py = g.Y
#End If
        r.PosAdjust = New Vector4(px, py, g.Z, 0.0F)
        If (technique And &H800000UI) <> 0UI Then
            Dim t = AlphaRefLaw.ScaledBiased(alphaRef)
#If DEBUG Then
            If Not Fo4GBufferConstants.GateMutantOn("sse-membrane-ref4") Then
                If alphaRef = 4 Then t += AlphaRefLaw.Inv255
            End If
#Else
            If alphaRef = 4 Then t += AlphaRefLaw.Inv255
#End If
            r.AlphaTestRef = New Vector4(t, 0.0F, 0.0F, 0.0F)
        End If
        Select Case True
            Case ed.SrcBlend = 6 AndAlso ed.DstBlend = 7 : r.BlendMode = 1
            Case ed.SrcBlend = 6 AndAlso (ed.DstBlend = 0 OrElse ed.DstBlend = 9) : r.BlendMode = 2
            Case ed.SrcBlend = 4 AndAlso ed.DstBlend = 7 : r.BlendMode = 3
            Case ed.SrcBlend = 1 AndAlso ed.DstBlend = 2 : r.BlendMode = 4
            Case Else : r.BlendMode = 0
        End Select
        Return r
    End Function

    ''' <summary>Where each name of the closed list (the 32 of the class, the `uniform vec4` declarations of the g5 block) lives: a law of
    ''' this module or a shared seat (ParityGate / ShadowGate hold the list against the block's declarations both ways).</summary>
    Friend ReadOnly Seats As (Stage As String, Name As String, Kind As String, Seat As String)() = {
        ("eff-vs", "World", "law", "World"),
        ("eff-vs", "PreviousWorld", "law", "PreviousWorld"),
        ("eff-vs", "Bones", "law", "RenderableMesh.BindEnginePalette (cb10, S1 D4)"),
        ("eff-vs", "EyePosition", "law", "Membrane"),
        ("eff-vs", "FogParam", "law", "Fog"),
        ("eff-vs", "FogNearColor", "law", "Fog"),
        ("eff-vs", "FogFarColor", "law", "Fog"),
        ("eff-vs", "FalloffData", "law", "FalloffData"),
        ("eff-vs", "SoftMaterialVSParams", "law", "SoftMaterialVSParams"),
        ("eff-vs", "TexcoordOffset", "law", "TexcoordOffset"),
        ("eff-vs", "TexcoordOffsetMembrane", "law", "Membrane"),
        ("eff-vs", "SubTexOffset", "law", "SubTexOffset"),
        ("eff-vs", "PosAdjust", "law", "Membrane"),
        ("eff-vs", "MatProj", "law", "MatProj"),
        ("eff-ps", "PropertyColor", "law", "PropertyColor"),
        ("eff-ps", "AlphaTestRef", "law", "AlphaTestRefBlood"),
        ("eff-ps", "MembraneRimColor", "law", "Membrane"),
        ("eff-ps", "MembraneVars", "law", "Membrane"),
        ("eff-ps", "PLightPositionX", "law", "EffectLights"),
        ("eff-ps", "PLightPositionY", "law", "EffectLights"),
        ("eff-ps", "PLightPositionZ", "law", "EffectLights"),
        ("eff-ps", "PLightingRadiusInverseSquared", "law", "EffectLights"),
        ("eff-ps", "PLightColorR", "law", "EffectLights"),
        ("eff-ps", "PLightColorG", "law", "EffectLights"),
        ("eff-ps", "PLightColorB", "law", "EffectLights"),
        ("eff-ps", "DLightColor", "law", "EffectLights"),
        ("eff-ps", "VPOSOffset", "law", "VposOffset"),
        ("eff-ps", "CameraData", "law", "CameraData"),
        ("eff-ps", "FilteringParam", "law", "FilteringParam"),
        ("eff-ps", "BaseColor", "law", "BaseColor"),
        ("eff-ps", "BaseColorScale", "law", "BaseColorScale"),
        ("eff-ps", "LightingInfluence", "law", "LightingInfluence")}
End Module
