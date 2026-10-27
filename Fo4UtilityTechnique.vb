Imports OpenTK.Mathematics

''' <summary>THE BSUtilityShader DRAWS THE PREVIEW MAKES (ola 4; D:\WinTmp\rc\ola4\re.md 1.1 / 2.2): the directional shadow map's
''' emitter and the refraction normals, with the transcribed b07 programs (Fo4UtilitySource).
''' <list type="bullet">
''' <item>The shadow-map technique: BSLightingShaderProperty slot 44 0x14217B8F0 in mode 0x10 (the directional light,
''' BSShadowDirectionalLight 0x142250A00 writes it at 0x14225295C): base (vfunc +0x1E8 = 0x142179FB0) | 0x4000 | 0x8000 | ALPHA_TEST
''' with the NiAlphaProperty's test (0x14217BAD2..BB7A).</item>
''' <item>The refraction technique: RefractionLaw (the one owner, RE_REFRACTION_BOTH 4.1).</item>
''' <item>The raster state of a utility draw: SetupTechnique sets depth bias index 8 (0x142240C37..C4D: DepthBias 12, SlopeScaledDepthBias
''' 6.0, Fo4RenderPassLaw.BiasOf) and write mode 0 for the shadow map; SetupGeometry culls NONE for Double_Sided (flag 36,
''' 0x1422412A8..2E6) and RestoreGeometry puts BACK back (0x142242D4D..D8C).</item>
''' <item>The constants (writer -&gt; asm2glsl.UVGROUP / UPGROUP): World = the geometry's world NiTransform rows (SetupGeometry
''' 0x1422414D3..56C, whenever the VS has the slot: Fo4GBufferConstants.EngineWorldRows, the G-buffer's composition); TexcoordOffset
''' (SetupTechnique 0x142240E99..EA3 writes (0, 0, 1, 1), SetupMaterial 0x142240FF0..1016 the material's offset / scale with TEXTURE and t
''' AND 0x2040280); AlphaTestRef.x = Fo4RenderPassLaw.ShadowMapAlphaThreshold (SetupGeometry 0x142241EBB..F68); RefractionPower.x =
''' the material's refraction power (SetupMaterial 0x1422410AA..163); the frame rows 0..2, 8..11, 35 of the camera the pass draws with
''' (the light's for the shadow map, 0x14182BC80 from 0x14224F59B..5B2).</item>
''' </list></summary>
Friend Module Fo4UtilityTechnique

    ''' <summary>RENDER_SHADOWMAP (bit 14) | RENDER_SHADOWMAP_CLAMPED (bit 15): the directional light's mode 0x10 (0x14217BAD2..0x14217BB7A).</summary>
    Friend Const TechShadowMapDirectional As UInteger = &HC000UI
    Friend Const TechAlphaTest As UInteger = &H80UI

    ''' <summary>vfunc +0x1E8 of BSLightingShaderProperty = 0x142179FB0, the base of its utility techniques: (MSN flag 12 ? 2 : 0x1A) |
    ''' (SKINNED flag 1 ? 4) | (VC flag 37 ? 1) | (flag 14 ? 0x20) | (EYE flag 17 ? 0x40) | (TREE_ANIM flag 61 without 60 ? 1 &lt;&lt; 26 :
    ''' flag 61 with 60 ? 1 &lt;&lt; 30 SPLINE) (0x142179FB0..A0E8). <paramref name="flags"/> = MaterialData.Fo4Flags (the applicator and
    ''' the slot-42 fixer included). The geometry-type bits (COMBINED 0x200000, INSTANCED 0xA00000, 0x14217BADE..B1A) are runtime geometry
    ''' the preview does not have (precombined meshes: ola 7).</summary>
    Friend Function LightingBase(flags As ULong) As UInteger
        Dim b = Function(n As Integer) Fo4GBufferTechnique.Bit(flags, n)
        Dim t As UInteger = If(b(12), 2UI, &H1AUI)
        If b(1) Then t = t Or 4UI
        If b(37) Then t = t Or 1UI
        If b(14) Then t = t Or &H20UI
        If b(17) Then t = t Or &H40UI
        If b(61) AndAlso Not b(60) Then t = t Or (1UI << 26)
        If b(61) AndAlso b(60) Then t = t Or (1UI << 30)
        Return t
    End Function

    ''' <summary>The directional shadow map's technique of a lighting shape (slot 44 0x14217B8F0, mode 0x10): LightingBase | 0xC000 |
    ''' ALPHA_TEST when its NiAlphaProperty tests (0x14217BB32..B4A). Bit 28 (ADDITIONAL_ALPHA_MASK) comes from the property's runtime
    ''' effect data ([prop + 0x110] + 0x20, 0x14217BB5E..B7A), which a loaded NIF does not carry: never set here (HUECO declared, re.md
    ''' 7: the writer of that effect data is a runtime system). Caster filter first: Fo4RenderPassLaw.ShadowMapCasts.</summary>
    Friend Function ShadowMapTechnique(flags As ULong, alphaTest As Boolean) As UInteger
        Dim t = LightingBase(flags) Or TechShadowMapDirectional
        If alphaTest Then t = t Or TechAlphaTest
#If DEBUG Then
        ' ShadowGate --fo4-shadow-law's mutants (Fo4GBufferConstants.GateMutantOn, the one switch).
        If Fo4GBufferConstants.GateMutantOn("utl-shadow-clamped") Then t = t And Not &H8000UI
        If Fo4GBufferConstants.GateMutantOn("utl-shadow-alpha") Then t = t And Not TechAlphaTest
#End If
        Return t
    End Function

    ''' <summary>The cull of a utility draw: none with Double_Sided (flag 36, SetupGeometry 0x1422412A8..2E6), else BACK (the working
    ''' state RestoreGeometry puts back, 0x142242D4D..D8C; rasterizer table 0x141856170: index 1 = D3D11_CULL_BACK, 0x141856213).</summary>
    Friend Function CullsBack(flags As ULong) As Boolean
        Dim doubleSided = Fo4GBufferTechnique.Bit(flags, 36)
#If DEBUG Then
        If Fo4GBufferConstants.GateMutantOn("utl-cull-none") Then Return False
#End If
        Return Not doubleSided
    End Function

    ''' <summary>The shadow map's raster bias: SetupTechnique's index 8 (0x142240C37..C4D) -&gt; DepthBias 12, SlopeScaledDepthBias 6.0
    ''' (Fo4RenderPassLaw.BiasOf, the table 0x141856170), clamp -100. On the D16_UNORM map (0x141822330: re.md 7 H-1) a unit of
    ''' DepthBias is one code, 1 / (2^16 - 1) (D3D11.3 15.10); the draw applies it through Gl43.DepthBias.
    ''' <para>APP (user decision 26-oct-2026): DepthBias and SlopeScaledDepthBias times the rig's Shadow Bias
    ''' <paramref name="shadowBias"/> (PreviewLightRig.ShadowBiasMultiplier): 1 = the engine's index 8 bit for bit (x 1.0 is exact), 0 = no bias
    ''' (Gl43.DepthBias turns the offset off). The clamp is not a bias: it stays.</para></summary>
    Friend Function ShadowMapBias(shadowBias As Single) As (DepthBias As Single, Slope As Single, Clamp As Single)
#If DEBUG Then
        If Fo4GBufferConstants.GateMutantOn("utl-bias-zero") Then Return (0.0F, 0.0F, -100.0F)
#End If
        Dim b = Fo4RenderPassLaw.BiasOf(8)
        Return (b.DepthBias * shadowBias, b.Slope * shadowBias, b.Clamp)
    End Function

    ''' <summary>TexcoordOffset of a utility draw: SetupTechnique writes (0, 0, 1, 1) (0x142240E99..EA3); SetupMaterial writes the
    ''' material's offset and scale when the technique has TEXTURE (bit 1) and t AND 0x2040280 (ALPHA_TEST, RENDER_NORMAL,
    ''' RENDER_BASE_TEXTURE, GRAYSCALE_MASK; 0x142240FC1..1016).</summary>
    Friend Function TexcoordOffset(technique As UInteger, offset As Vector2, scale As Vector2) As Vector4
        If (technique And 2UI) <> 0UI AndAlso (technique And &H2040280UI) <> 0UI Then Return Fo4GBufferConstants.TexcoordOffset(offset, scale)
        Return New Vector4(0.0F, 0.0F, 1.0F, 1.0F)
    End Function
End Module

''' <summary>One utility draw of a shape (Fo4UtilityTechnique): its technique, the program (Nothing when the engine has no program for it -
''' MissingPrograms, it does not draw it - or when the preview cannot draw it - Gap) and why.</summary>
Friend Structure Fo4UtilityDraw
    Public Technique As UInteger
    Public Program As Fo4_Deferred_Shader_Class
    Public MissingPrograms As String
    Public Gap As String
End Structure
