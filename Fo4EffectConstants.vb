Imports OpenTK.Mathematics

''' <summary>THE CONSTANTS OF THE FALLOUT 4 EFFECT PROGRAMS (block b05), BY NAME (VS BSEffectShaderVertexConstants::GetString
''' 0x142274710, PS BSEffectShaderPixelConstants::GetString 0x1422748C0), as BSEffectShader::SetupTechnique 0x1422250D0 (ST),
''' SetupMaterial 0x142225650 (SM) and SetupGeometry 0x1422261B0 (SG) write them (ola 3, D:\WinTmp\rc\ola3\re.md 3, its dumps
''' D:\WinTmp\rc\ola3\dis\ST.txt / SM.txt / SG.txt). The register each name occupies is per record; the GLSL reads them by name
''' (Fo4EffectSource). Names the preview never writes are those of runtime-only techniques (MEMBRANE, ENVCUBE, MERGE_INSTANCED, UI_MASK,
''' particles; re.md 2.4): their programs are never drawn.</summary>
Friend Module Fo4EffectConstants

    ''' <summary>VS WorldViewProj (cb2, 4 rows), SG 0x142226297..415 for a technique without SKINNED (bit 1: the block is skipped,
    ''' 0x142226286..28A, and the skinned VS read cb12's view-projection): the geometry's world NiTransform (geom + 0x70: rotation rows x
    ''' scale, 0x14222636A..390), translation - eye (0x1422263B8, eye = [state + 0x1B70 + 0x6B0]) with row 3 w = 1 (0x142226394..3D1),
    ''' times the frame's view-projection [state + 0x1B70 + 0x7A0] by 0x1406B9610, its result transposed into the rows
    ''' (0x142226406..411) - the same composition as Fo4GBufferConstants.EngineWorldViewRows with V = the view-projection (cb12 rows
    ''' 8..11). <paramref name="model"/> = RenderableMesh.EngineGeometryModel.</summary>
    Friend Function WorldViewProj(model As Matrix4, frame As Fo4FrameRows) As Vector4()
        Return Fo4GBufferConstants.EngineWorldTimesRows(model, frame.ViewProj, frame.Eye)
    End Function

    ''' <summary>VS World (cb2, 4 rows): W_rel transposed - the rows are the columns of (world NiTransform with translation - eye, row 3
    ''' w = 1) - SG 0x142226521..583 (FALLOFF or RGB_FALLOFF, technique AND 0x200010, without SKINNED, 0x14222650F..518) and
    ''' 0x142226E94..EED (ENVMAP, bit 19); with SKINNED (ENVMAP only) the W of the geometry node's own world [geom + 0x70]
    ''' (0x142226DA3..E2F). <paramref name="model"/> = that world.</summary>
    Friend Function World(model As Matrix4, eye As Vector4) As Vector4()
        Dim m = model * Matrix4.CreateTranslation(-eye.Xyz)
#If DEBUG Then
        If Fo4GBufferConstants.GateMutantOn("efx-world-eye") Then m = model
#End If
        Return {m.Column0, m.Column1, m.Column2, m.Column3}
    End Function

    ''' <summary>VS EyePosition (cb2), SG 0x142226E3B..E89 (ENVMAP, the VS has the slot): the eye 0x142183780() + 0x570 (absolute world)
    ''' times the matrix 0x1417A2580 built at 0x142226260..267 (not SKINNED: the inverse of the geometry's world NiTransform -
    ''' transposed rotation / scale, -t R^T / s, row 3 w = 1, 0x1417A2596..663) or at 0x142226DA3..DC0 (SKINNED: the inverse of the
    ''' translation by the eye, 0x1417A2670..6E2, so the eye goes to (0, 0, 0)), divided by its w (0x142226E7F..E86). The NiTransform's
    ''' inverse is the engine's explicit one, not a general matrix inverse: with <paramref name="model"/> = [A; t] (rows 0..2 = A = s R,
    ''' row 3 = the translation; a NiTransform composition has a rotation R and a uniform scale s) the inverse is [R^T / s; -t R^T / s] =
    ''' [A^T / s^2; -t A^T / s^2], s^2 = |row 0 of A|^2, so the eye in the geometry's space is (e - t) A^T / s^2 and its w is 1.</summary>
    Friend Function EyePosition(model As Matrix4, eyeWorld As Vector4, skinned As Boolean) As Vector4
#If DEBUG Then
        If Fo4GBufferConstants.GateMutantOn("efx-eyepos-skinned") Then skinned = Not skinned
#End If
        If skinned Then Return New Vector4(eyeWorld.X - eyeWorld.X, eyeWorld.Y - eyeWorld.Y, eyeWorld.Z - eyeWorld.Z, 1.0F)
        Dim d = eyeWorld.Xyz - model.Row3.Xyz
        Dim s2 = model.M11 * model.M11 + model.M12 * model.M12 + model.M13 * model.M13
        Return New Vector4(Vector3.Dot(d, model.Row0.Xyz) / s2, Vector3.Dot(d, model.Row1.Xyz) / s2, Vector3.Dot(d, model.Row2.Xyz) / s2, 1.0F)
    End Function

    ''' <summary>VS FalloffData (cb1), SM 0x142225BC4..BEA (FALLOFF or RGB_FALLOFF): the material's +0x38..+0x44 raw - start angle, stop
    ''' angle, start opacity, stop opacity (ApplyMaterialData copies the BGEM's +0x194..+0x1A0, 0x1421717E2 / 812).</summary>
    Friend Function FalloffData(startAngle As Single, stopAngle As Single, startOpacity As Single, stopOpacity As Single) As Vector4
#If DEBUG Then
        If Fo4GBufferConstants.GateMutantOn("efx-falloff-order") Then Return New Vector4(startOpacity, stopOpacity, startAngle, stopAngle)
#End If
        Return New Vector4(startAngle, stopAngle, startOpacity, stopOpacity)
    End Function

    ''' <summary>VS SoftMaterialVSParams (cb1), SM 0x142225B8A..BB1 (SOFT): .x = the soft depth +0x80; .yzw not written.</summary>
    Friend Function SoftMaterialVSParams(softDepth As Single) As Vector4
#If DEBUG Then
        If Fo4GBufferConstants.GateMutantOn("efx-softvs") Then Return New Vector4(2.0F * softDepth, 0.0F, 0.0F, 0.0F)
#End If
        Return New Vector4(softDepth, 0.0F, 0.0F, 0.0F)
    End Function

    ''' <summary>PS PropertyColor (cb2), SG 0x142226EF9..FCA: rgb = powf([prop + 0x78] colour, 2.2), 1.0 when the pointer is null - the
    ''' ctor leaves it null (0x1421774F4) and the preview has no writer of it (re.md 6.2) -, so powf(1, 2.2) = 1; w = the pass alpha
    ''' [prop + 0x28] (0x142226F3A), times [[geom + 0x48] + 0x13C] only when [pass + 0x4E] AND 0x80 (0x142226F86..FB5: the pass ctor
    ''' 0x14221D35A writes 3 there and the preview has no other writer). [prop + 0x28] = GetRenderPasses' 0x1421694B0 of the material
    ''' alpha +0x54 (vfunc +0x198 = 0x142178940) times the fade [[prop + 0x48] + 0x1A0] (0x142177881..89E) when the accumulator's
    ''' first-person byte [r13 + 0xB0] is 0 (r13 = GetRenderPasses' 4th argument), the alpha alone without the fade when it is not
    ''' (0x142177869..87F). The preview is never first person (the same byte the technique reads, 0x142177A28) and its fade is 1
    ''' (notas 1.1, as the G-buffer's pass alpha): both branches give the material alpha.</summary>
    Friend Function PropertyColor(materialAlpha As Single) As Vector4
#If DEBUG Then
        If Fo4GBufferConstants.GateMutantOn("efx-propcolor-w") Then Return New Vector4(1.0F, 1.0F, 1.0F, 1.0F)
#End If
        Return New Vector4(1.0F, 1.0F, 1.0F, materialAlpha * 1.0F)
    End Function

    ''' <summary>PS AlphaTestRef (cb2): .x = Fo4RenderPassLaw.EffectAlphaThreshold (0x14222649A / 0x1422274B7..509), .y = 1.0
    ''' (0x14222649E); .zw not written.</summary>
    Friend Function AlphaTestRef(test As Boolean, ref As Byte) As Vector4
#If DEBUG Then
        If Fo4GBufferConstants.GateMutantOn("efx-alphatest-y") Then Return New Vector4(Fo4RenderPassLaw.EffectAlphaThreshold(test, ref), 0.0F, 0.0F, 0.0F)
#End If
        Return New Vector4(Fo4RenderPassLaw.EffectAlphaThreshold(test, ref), 1.0F, 0.0F, 0.0F)
    End Function

    ''' <summary>The point lights of a LIGHTING technique (bit 10, SG 0x14222659B..9F) with no point light (user decision 13-oct-2026:
    ''' the effects' point lights are 0; the pass's light list [pass + 0x1C8] holds the directional one only, count min([+0x50], 5) - 1
    ''' = 0, 0x1422267BA..7D2): every component of the 12 names (PLightPosition X / Y / Z, PSpotDir X / Y / Z, PSpotPower, PSpotAngle,
    ''' PLightingRadiusInverseSquared, PLightColor R / G / B) is filled for the absent lights 0..3 by 0x142226B89..C81 with 0
    ''' (PointLightNone), except PSpotPower = 0x3A83126F (0.001, 0x142226C6D: PSpotPowerNone).</summary>
    Friend ReadOnly PointLightNone As Vector4 = Vector4.Zero
    Friend Function PSpotPowerNone() As Vector4
#If DEBUG Then
        If Fo4GBufferConstants.GateMutantOn("efx-pspot") Then Return Vector4.Zero
#End If
        Return New Vector4(BitConverter.Int32BitsToSingle(&H3A83126F))
    End Function

    ''' <summary>PS CameraData (cb0), ST 0x1422253CD..43C (SOFT): .y = near [[0x143E5DE48] + 0x170], .z = far - near ([+0x174] - [+0x170]);
    ''' .xw not written. The app's near / far = the frame's (PreviewControl.FrameNearFar, the same SetViewFrustum law, ola 1).</summary>
    Friend Function CameraData(near As Single, far As Single) As Vector4
#If DEBUG Then
        If Fo4GBufferConstants.GateMutantOn("efx-cameradata") Then Return New Vector4(0.0F, near, far, 0.0F)
#End If
        Return New Vector4(0.0F, near, far - near, 0.0F)
    End Function

    ''' <summary>PS LightingInfluence (cb1), SM 0x142225B15..B84: .x = |byte +0xB5 x 1/255| (0x142225B2E..B4B; 1/255 = 0x3B808081); with
    ''' SOFT .y = far x near / soft depth ([0x143E5E0C4] x [0x143E5E0C0], which ST writes from the camera at 0x142225423 / 42F, divided
    ''' by +0x80, 0x142225B5D..B84); .zw not written.</summary>
    Friend Function LightingInfluence(influenceByte As Integer, soft As Boolean, near As Single, far As Single, softDepth As Single) As Vector4
        Dim x = Math.Abs(CSng(influenceByte) * BitConverter.Int32BitsToSingle(&H3B808081))
#If DEBUG Then
        If Fo4GBufferConstants.GateMutantOn("efx-li-soft") Then Return New Vector4(x, If(soft, far / softDepth, 0.0F), 0.0F, 0.0F)
#End If
        Return New Vector4(x, If(soft, far * near / softDepth, 0.0F), 0.0F, 0.0F)
    End Function

    ''' <summary>PS BaseColor (cb1), SM 0x1422259FF..B11 (FO4UnifiedMaterial_Class.EngineEffectConstants: rgb = powf(rgb x scale, 2.2), with
    ''' GRAYSCALE_TO_COLOR powf(rgb, 2.2)), a = +0x54 raw (the material alpha, 0x142225AA2 / B01).</summary>
    Friend Function BaseColor(linearRgb As Vector3, alpha As Single) As Vector4
        Return New Vector4(linearRgb, alpha)
    End Function

    ''' <summary>PS BaseColorScale (cb1), SM 0x142225A2C..A4A (GRAYSCALE_TO_COLOR only): .x = +0x84; .yzw not written.</summary>
    Friend Function BaseColorScale(scale As Single) As Vector4
        Return New Vector4(scale, 0.0F, 0.0F, 0.0F)
    End Function

    ''' <summary>PS EnvmapScale (cb1), SM 0x142225DA7..DD5 (ENVMAP): .x = +0xB0 (ApplyMaterialData copies the BGEM's env map mask scale
    ''' +0x180, 0x142171829 / 869); .yzw not written.</summary>
    Friend Function EnvmapScale(scale As Single) As Vector4
#If DEBUG Then
        If Fo4GBufferConstants.GateMutantOn("efx-envscale") Then Return New Vector4(1.0F - scale, 0.0F, 0.0F, 0.0F)
#End If
        Return New Vector4(scale, 0.0F, 0.0F, 0.0F)
    End Function

    ''' <summary>The fog globals the frame writer 0x14181E1C0 reads (0x143D871B4..0x143D87210): Near / Far 0x143D871B4 / B8; the four
    ''' colours 0x143D871C4..D0, D4..E0, E4..F0, F4..200 (rgb, w); the two height pairs 0x143D87204 / 208 and 0x143D8720C / 210.</summary>
    Friend Structure Fo4FogGlobals
        Public Near As Single, Far As Single
        Public Colours As Vector4()
        Public HeightA As Single, HeightARange As Single, HeightB As Single, HeightBRange As Single
    End Structure

    ''' <summary>THE FOG OF THE PREVIEW'S EFFECTS (user decision 13-oct-2026: no fog, the engine's own fog-off state, as the opaques): the
    ''' globals 0x141067780 writes (an interface render that turns the fog off): Near = Far = 1e9 (0x14106789F / 8A9), the colours from a
    ''' zeroed block (0x1422C46A8 of 0x60 bytes at 0x141067808; 0x141067825..8C6), the heights 0 (0x1410678CC / 8D3). The .data value
    ''' (all 0) is not a state the engine draws with (power 0: exp(log(0) x 0) in the VS, re.md 7 D3).</summary>
    Friend Function FogOff() As Fo4FogGlobals
#If DEBUG Then
        ' ShadowGate --fo4-effect-constants' positive control of the fog-off law: a fog that is on (a gate fixture, not a law).
        If Fo4GBufferConstants.GateMutantOn("efx-fog-on") Then
            Dim g = New Vector4(0.5F, 0.5F, 0.5F, 1.0F)
            Return New Fo4FogGlobals With {.Near = 0.0F, .Far = 1000.0F, .Colours = {g, g, g, g}}
        End If
#End If
        Return New Fo4FogGlobals With {.Near = 1.0E+9F, .Far = 1.0E+9F, .Colours = {Vector4.Zero, Vector4.Zero, Vector4.Zero, Vector4.Zero}}
    End Function

    ''' <summary>cb12 rows 41..46 of the frame writer 0x14181E1C0 (0x14181E80E..B05): a pair whose two values are 0 becomes (1e8, 1e9)
    ''' (0x14181E8A0..8D5: [0x142542018] 1e8, [0x1426A490C] 1e9); 41 = (1 / (far - near), 1 / (hA+ - hA-), near / (far - near),
    ''' hA- / (hA+ - hA-)) with hA+- = HeightA +- HeightARange; 42..45 = (powf(colour.rgb, 2.2), colour.w) (0x1422C47F8, 2.2 = 0x400CCCCD);
    ''' 46 = (1 / (hA+ - hA-), 1 / (hB+ - hB-), hA- / (hA+ - hA-), hB- / (hB+ - hB-)). Single precision as the writer (divss / mulss); a
    ''' division by 0 is +Inf, as divss.</summary>
    Friend Function FogRows(g As Fo4FogGlobals) As Vector4()
        Dim near = g.Near, far = g.Far
        If near = 0.0F AndAlso far = 0.0F Then near = 1.0E+8F : far = 1.0E+9F
        Dim ap = g.HeightA + g.HeightARange, am = g.HeightA - g.HeightARange
        If ap = 0.0F AndAlso am = 0.0F Then am = 1.0E+8F : ap = 1.0E+9F
        Dim bp = g.HeightB + g.HeightBRange, bm = g.HeightB - g.HeightBRange
        If bp = 0.0F AndAlso bm = 0.0F Then bm = 1.0E+8F : bp = 1.0E+9F
        Dim dn = far - near, da = ap - am, db = bp - bm
        Dim ia = 1.0F / da
        Dim r(5) As Vector4
        r(0) = New Vector4(1.0F / dn, ia, near / dn, ia * am)
        For i = 0 To 3
            Dim c = g.Colours(i)
            r(1 + i) = New Vector4(CSng(Math.Pow(c.X, 2.2F)), CSng(Math.Pow(c.Y, 2.2F)), CSng(Math.Pow(c.Z, 2.2F)), c.W)
        Next
        r(5) = New Vector4(ia, 1.0F / db, ia * am, bm / db)
        Return r
    End Function
End Module
