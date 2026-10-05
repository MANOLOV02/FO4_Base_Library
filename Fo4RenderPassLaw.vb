''' <summary>HOW FALLOUT 4 DRAWS ONE SHAPE IN THE WORLD RENDER: its pass group, the stage and list it lands in, the depth test /
''' write / bias of its colour pass. One pure law (gate ParityGate `fo4-pass-law`), read by RenderAll / Render. Sources
''' (Fallout4.exe 1.11.240.0): Tools/re-docs/RE_FO4_PASS_GROUPS_DEPTH_2026-10-03.md (groups and routing 3, G-buffer depth
''' and H3, final table 4, the blend-without-decal lighting check 8).
''' <para>Stages of the world accumulator (mode 0x18) in frame order: G-buffer 0x1421F38B0 (z-prepass of eligible
''' lighting, bucket 4 = opaque lighting, decal lists 2 / 3 incl. effect decals, lists 0 / 1, list 4) -> deferred
''' lights and composite -> forward 0x1421F9100 (bucket 0 = opaque effects, ..., list 0xE) -> alpha finish 0x1421D5AC0
''' (list 7, the sorted list). FO4 has no EQUAL depth mode in the world (no write of mode 4 among the 121 writes).</para></summary>
Friend Module Fo4RenderPassLaw

    Friend Structure Fo4ShapeInputs
        Public IsEffect As Boolean
        ''' <summary>BSLighting: the group of GetRenderPasses' deferred constructor (Fo4GBufferTechnique.Technique,
        ''' 0x14217A9F6..0x14217AA56), Nothing when it builds no G-buffer pass. The pass law reads it; it does not re-derive it (rev-45).</summary>
        Public LightingGroup As Integer?
        ''' <summary>BSLighting: that pass's technique (Fo4GBufferTechnique.Technique). The z-prepass mask, the G-buffer depth mode
        ''' and the vertex-alpha test are read from its bits (Tools/re-docs/RE_FO4_PASS_GROUPS_DEPTH_2026-10-03.md 9.1, 10.4,
        ''' 11.1; v4 B-rev-02: one owner).</summary>
        Public LightingTechnique As UInteger?
        ''' <summary>BSLighting without a G-buffer pass (LightingGroup Nothing): why, as Fo4GBufferTechnique.Technique returns it.</summary>
        Public LightingNoPass As Fo4NoPass
        ''' <summary>BSEffect: baseColor.a; BSLighting: materialAlpha. The fade is 1.</summary>
        Public Alpha As Single
        Public Blend As Boolean
        Public ZTest As Boolean          ' F4SF1 31
        Public ZWrite As Boolean         ' F4SF2 0
        ''' <summary>F4SF1 26 / 27 (a .bgsm's bDecal sets both, 0x14216B131..B156).</summary>
        Public Decal As Boolean
        Public Refraction As Boolean     ' F4SF1 15
        Public TempRefraction As Boolean ' F4SF1 2
        ''' <summary>Effect: FALLOFF or RGB_FALLOFF technique (bFalloffEnabled / bFalloffColorEnabled).</summary>
        Public Falloff As Boolean
        Public Billboard As Boolean      ' F4SF2 13 ("flag 45")
        Public PipboyScreen As Boolean   ' F4SF2 28 ("flag 60")
        ''' <summary>BGEM SoftEnabled: the SOFT draw binds the depth read-only (0x14183C863), it never writes depth.</summary>
        Public Soft As Boolean
        ' ---- z-prepass / G-buffer technique bits (Tools/re-docs/RE_FO4_PASS_GROUPS_DEPTH_2026-10-03.md 10) ----
        Public AlphaTest As Boolean      ' NiAlphaProperty bit 9 (the 0x1A prepass type, G-buffer technique bit 8)
        Public TestRef As Byte
        ''' <summary>vertexDesc COLOR: the prepass test multiplies the vertex alpha (cb2[0].y = 0, else 10000).</summary>
        Public HasColorStream As Boolean
        ''' <summary>The geometry's exact class is BSTriShape, BSSubIndexTriShape or BSMeshLODTriShape (RTTI 0x143449778,
        ''' 0x1438DF4C0, 0x143E71B70) and it has triangles.</summary>
        Public PrepassGeometry As Boolean
    End Structure

    Friend Enum Fo4List
        None
        ''' <summary>G-buffer bucket 4 (opaque lighting) and list 0 (LOD landscape).</summary>
        GBufferOpaque
        ''' <summary>G-buffer list 3 (group 2) - lighting and effect decals.</summary>
        DecalOpaque
        ''' <summary>G-buffer list 4 (group 3).</summary>
        DecalTranslucent
        ''' <summary>Forward bucket 0: the opaque effects (group 0), after the whole G-buffer.</summary>
        ForwardEffect
        ''' <summary>Forward list 0xE: the effects with F4SF2 bit 13 (group 0x14).</summary>
        ForwardBillboard
        ''' <summary>The sorted alpha list (group 1).</summary>
        Translucent
    End Enum

    ''' <summary>Why a shape has no colour pass (Fo4Pass.Drawn = False): the branch of GetRenderPasses that leaves it without one.</summary>
    Friend Enum Fo4NoPass
        None
        ''' <summary>Refraction: lighting flags AND 0x8004 (0x14217A566..0x14217A668) or an effect's Refraction (row 4): only the
        ''' utility refraction pass, group 5 - the refraction-normals pass draws it (user decision 5-oct-2026: no notice).</summary>
        RefractionOnly
        ''' <summary>Effect with Temp_Refraction (flag 2) and without flag 60 Pipboy_Screen: no pass at all
        ''' (BSEffectShaderProperty::GetRenderPasses 0x1421777A0, row 2: 0x142177919..0x14217792E).</summary>
        EffectTempRefraction
        ''' <summary>Lighting with a blending NiAlphaProperty and no decal bit: no G-buffer pass, as the app reads 0x14217A16A..0x14217A184
        ''' -&gt; 0x14217A237 (Tools/re-docs/RE_FO4_PASS_GROUPS_DEPTH_2026-10-03.md 8.1). NOT VERIFIED: its consequence contradicts the
        ''' game (shapes blended without decal are seen in game); the link that reads it is under re-examination (same document, 8.1).
        ''' The preview follows it for now and lists the shape in the frame's notice ("Blended without decal: not drawn"), never silent.
        ''' Given by Fo4GBufferTechnique.Technique itself, the function that leaves the shape without a technique.</summary>
        LightingBlendWithoutDecal
    End Enum

    Friend Structure Fo4Pass
        Public Group As Integer
        Public List As Fo4List
        Public Drawn As Boolean
        ''' <summary>Why Drawn is False (Fo4NoPass.None when it is True).</summary>
        Public NoPass As Fo4NoPass
        Public DepthTest As Boolean
        Public DepthWrite As Boolean
        ''' <summary>Rasterizer bias index (table 0x141856170): 0 none, 1 = (-3, -0.4), 4 = (-9, -1.2).</summary>
        Public BiasIndex As Integer
        ''' <summary>Lighting: the shape has a z-prepass entry (groups 0x19 / 0x1A, drawn by 0x14181FAA0 at the start of
        ''' the G-buffer stage).</summary>
        Public InPrepass As Boolean
        ''' <summary>The prepass entry is the alpha-test one (0x1A): discard tex.a * saturate(vc.a + y) &lt; ref/255 +
        ''' 0.00392153, y = 0 with a COLOR stream, else 10000 (0x141828F45..F8D).</summary>
        Public PrepassAlphaTest As Boolean
        Public PrepassThreshold As Single
        ''' <summary>The G-buffer's alpha test multiplies the vertex alpha: technique bit 0 (flag 37) without bits 10 / 6.</summary>
        Public GBufferTestVertexAlpha As Boolean
    End Structure

    ''' <summary>DepthBias / SlopeScaledDepthBias of a rasterizer bias index (table 0x141856170, switch 0x141856364;
    ''' DepthBiasClamp -100 everywhere). The main depth buffer is D24_UNORM_S8_UINT, standard Z (clear 1.0, 0x141823EC4;
    ''' LESS_EQUAL): GL's polygon offset (factor = slope, units = DepthBias) is the same bias.</summary>
    Friend Function BiasOf(index As Integer) As (DepthBias As Single, Slope As Single)
        Select Case index
            Case 1, 5 : Return (-3.0F, -0.4F)
            Case 2 : Return (-6.0F, -0.8F)
            Case 3, 4 : Return (-9.0F, -1.2F)
            Case 6 : Return (3.0F, 0.4F)
            Case 7 : Return (6.0F, 0.8F)
            Case 8 : Return (12.0F, 6.0F)
            Case Else : Return (0.0F, 0.0F)
        End Select
    End Function

    ''' <summary>The alpha-test threshold of FO4's world lighting pass (BSDFPrePassShader, the G-buffer; 155 PS with technique
    ''' bit 8 discard on tex0.a [* COLOR0.w] - cb2[1].w &lt; 0): cb2[1].w = ref/255 + 0.00392153, and 4/255 + 0.0078431 for
    ''' ref 4 (0x142207340..37D, 0x14220736A). With 8-bit alpha it keeps a &gt; ref/255. The z-prepass (0x1A) uses ref/255 +
    ''' 0.00392153 for every ref (0x141828F45..F65). Tools/re-docs/RE_FO4_PASS_GROUPS_DEPTH_2026-10-03.md 9.1.</summary>
    Friend Function GBufferAlphaThreshold(ref As Byte) As Single
        If ref = 4 Then Return 4.0F / 255.0F + 0.0078431F
        Return ref / 255.0F + 0.00392153F
    End Function

    Friend Function Classify(s As Fo4ShapeInputs) As Fo4Pass
        Dim r As New Fo4Pass With {.Drawn = True, .DepthTest = True}
        Dim alphaBelowOne = Not (s.Alpha >= 1.0F)
        If s.IsEffect Then
            ' BSEffectShaderProperty::GetRenderPasses (3): Refraction -> only the utility refraction pass (group 5);
            ' Temp_Refraction without flag 60 -> no passes; decal -> 2 / 3 (blend); flag 45 -> 0x14; alpha < 1, blend,
            ' particle, FALLOFF / RGB_FALLOFF -> 1; else 0.
            ' The order is the engine's (3.1 rows 2 and 4, 0x142177919 before 0x142177950; RefractionLaw.Fo4EffectNoPasses, one owner):
            ' Temp_Refraction without flag 60 first (no passes), then Refraction (only the utility pass, group 5).
            If RefractionLaw.Fo4EffectNoPasses(s.TempRefraction, s.PipboyScreen) Then
                r.Drawn = False : r.NoPass = Fo4NoPass.EffectTempRefraction
            ElseIf s.Refraction Then
                r.Group = 5 : r.Drawn = False : r.NoPass = Fo4NoPass.RefractionOnly
            ElseIf s.Decal Then
                r.Group = If(s.Blend, 3, 2)
            ElseIf s.Billboard Then
                r.Group = &H14
            ElseIf alphaBelowOne OrElse s.Blend OrElse s.Falloff Then
                r.Group = 1
            Else
                r.Group = 0
            End If
        Else
            ' BSLightingShaderProperty::GetRenderPasses 0x14217A050, deferred modes: refraction -> only utility 5; otherwise the group
            ' is GetRenderPasses' own (Fo4GBufferTechnique.Technique, 0x14217A9F6..0x14217AA56: one owner, rev-45); no G-buffer pass
            ' (NiAlphaProperty blend without decal, 0x14217A16A -> 0x14217A237, 8.1) -> not drawn.
            If s.Refraction OrElse s.TempRefraction Then
                r.Group = 5 : r.Drawn = False : r.NoPass = Fo4NoPass.RefractionOnly
            ElseIf Not s.LightingGroup.HasValue Then
                ' The technique's own reason. Its blend-without-decal branch (0x14217A16A) is NOT VERIFIED (RE_FO4_PASS_GROUPS_DEPTH 8.1,
                ' under re-examination: the game shows such shapes); followed for now, listed in the frame's notice.
                r.Drawn = False : r.NoPass = s.LightingNoPass
            Else
                r.Group = s.LightingGroup.Value
            End If
        End If

        Select Case r.Group
            Case 0 : r.List = If(s.IsEffect, Fo4List.ForwardEffect, Fo4List.GBufferOpaque)
            Case 7 : r.List = Fo4List.GBufferOpaque
            Case 1 : r.List = Fo4List.Translucent
            Case 2 : r.List = Fo4List.DecalOpaque
            Case 3 : r.List = Fo4List.DecalTranslucent
            Case &H14 : r.List = Fo4List.ForwardBillboard
            Case Else : r.List = Fo4List.None
        End Select
        If Not r.Drawn Then r.List = Fo4List.None : r.DepthTest = False : Return r

        If Not s.IsEffect Then
            ' G-buffer: LESS_EQUAL always, ZTest / ZWrite not read (pass+0x28 is never set, H3). Group 3: mode 1. Otherwise the
            ' technique decides (10.4, 11.1): the z-prepass mask M = 0x33480684 = SKINNED | GRASS | LOD_LANDSCAPE | TREE_ANIM |
            ' TESSELLATE_DISP_HEIGHT | DISMEMBERMENT_MEATCUFF | ADDITIONAL_ALPHA_MASK | LAND_LOD_BLEND | COMBINED | CLIP_VOLUME, or
            ' 0x23480684 when the technique has both bits 27 and 28 (0x14217AAD2); the G-buffer draws with mode 1 (no write) when
            ' the technique has none of M nor bit 5 LANDSCAPE (0x334806A4, 2.2) - the depth then comes from the z-prepass, if the
            ' shape got an entry - else mode 3.
            Dim t = s.LightingTechnique.Value
            Dim m As UInteger = If((t And &H18000000UI) = &H18000000UI, &H23480684UI, &H33480684UI)
            Dim mascaraLibre = (t And m) = 0UI
            r.DepthWrite = r.Group <> 3 AndAlso (t And (m Or &H20UI)) <> 0UI
            ' z-prepass entry (14217AAB7..ABE6): main group not 3, the mask, bZPrePass:Display (1 in .data, no INI sets it),
            ' the geometry class / triangles.
            r.InPrepass = r.Group <> 3 AndAlso mascaraLibre AndAlso s.PrepassGeometry
            r.PrepassAlphaTest = r.InPrepass AndAlso s.AlphaTest
            If r.PrepassAlphaTest Then r.PrepassThreshold = s.TestRef / 255.0F + 0.00392153F
            ' The G-buffer alpha test multiplies the vertex alpha with technique bit 0 (VC) and neither bit 10 (TREE_ANIM) nor
            ' bit 6 (EYE) (9.1, 11.1).
            r.GBufferTestVertexAlpha = (t And 1UI) <> 0UI AndAlso (t And &H400UI) = 0UI AndAlso (t And &H40UI) = 0UI
        ElseIf r.Group = 2 OrElse r.Group = 3 Then
            ' Effect decals land in the G-buffer decal lists: list 3 = 3, list 4 = 1, whatever the flags.
            r.DepthWrite = r.Group = 2
        Else
            ' Effect SetupGeometry 0x1422261B0: ZTest without ZWrite -> 1, no ZTest -> 0, PIPBOY -> 0; ZTest + ZWrite keeps
            ' the inherited 3 (RestoreGeometry only ever puts 3).
            If Not s.ZTest OrElse s.PipboyScreen Then
                r.DepthTest = False : r.DepthWrite = False
            Else
                r.DepthWrite = s.ZWrite
            End If
        End If
        ' SOFT binds the depth as a read-only DSV while it draws (0x14183C863): no write.
        If s.IsEffect AndAlso s.Soft Then r.DepthWrite = False

        ' Bias: decal lists 3 / 4 index 1 (0x1421D5F13..F7F, 0x1421D607E..60A6); flag-45 effects index 4.
        If r.Group = 2 OrElse r.Group = 3 Then r.BiasIndex = 1
        If r.Group = &H14 Then r.BiasIndex = 4
        Return r
    End Function
End Module
