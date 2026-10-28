''' <summary>Item 2b: how a world accumulator walks a list when it draws it - THE order inside a list, one owner per game
''' (Fo4RenderPassLaw.ListWalk, SseRenderPassLaw.ListWalk), read by the render (PreviewModel.RebuildRenderBuckets /
''' BuildFo4TechniqueSequences) and by the coverage (MaterialData.EngineCoverage.DrawsAfter).</summary>
Friend Enum EngineListWalk
    ''' <summary>No colour list.</summary>
    None
    ''' <summary>Fallout 4: a technique holder - G-buffer bucket 4, the sub-holder lists 3 / 4 (bit0 of +0x20 clear, ctor 0x14221DC70),
    ''' forward bucket 0 - shader type, technique activation, packets, chain (PreviewModel.Fo4TechniqueBucketKeys; RE_FO4_TECHNIQUE_BUCKET_ORDER
    ''' L5..L8).</summary>
    TechniqueBuckets
    ''' <summary>Skyrim SE: a batch list (+0x26 = 0, holder ctor 0x14155E1C0) - pass ID ascending (0x14155FC90 / 0x14155FF40), sub-list
    ''' 0..4 (0x14155FED0), reverse registration inside (prepend 0x14155F024..02B, walk 0x1415601A9..1DB): the O-OP key
    ''' (PreviewModel.SseBatchKey; RE_FO4_TECHNIQUE_BUCKET_ORDER Addendum r19b-4).</summary>
    PassBatches
    ''' <summary>A linked list the group APPENDS to, drawn head to tail: registration order (FO4 0x1422221C0 0x142222262..28F, walk
    ''' 0x142220600; SSE 0x14155DD70 0x14155DDDC..E05, walk 0x14155DF50).</summary>
    Registration
    ''' <summary>A linked list the group PREPENDS to, drawn head to tail: reverse registration order (FO4 0x142222248..25D; SSE
    ''' 0x14155DDBD..DD7).</summary>
    RegistrationReverse
    ''' <summary>The sorted alpha list (FO4 0x14221DA70, SSE 0x141555540; RE_FO4_ALPHA_ORDER 2).</summary>
    AlphaSorted
End Enum

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
        ''' <summary>F4SF1 9 Cast_Shadows (a .bgsm's bCastShadows sets it, 0x14216B29E) and F4SF2 2 = bit 34 LOD_Objects (the NIF's only:
        ''' the applicator does not write it). Read by ShadowMapCasts.</summary>
        Public CastShadows As Boolean    ' F4SF1 9
        Public LodObjects As Boolean     ' F4SF2 2 (bit 34)
        ''' <summary>Effect: FALLOFF or RGB_FALLOFF technique (bFalloffEnabled / bFalloffColorEnabled).</summary>
        Public Falloff As Boolean
        Public Billboard As Boolean      ' F4SF2 13 ("flag 45")
        Public PipboyScreen As Boolean   ' F4SF2 28 ("flag 60")
        ''' <summary>BGEM SoftEnabled: the SOFT draw binds the depth read-only (0x14183C863), it never writes depth.</summary>
        Public Soft As Boolean
        ' ---- z-prepass / G-buffer technique bits (Tools/re-docs/RE_FO4_PASS_GROUPS_DEPTH_2026-10-03.md 10) ----
        Public AlphaTest As Boolean      ' NiAlphaProperty bit 9 (the 0x1A prepass type, G-buffer technique bit 8)
        Public TestRef As Byte
        ''' <summary>The geometry's full vertex desc has COLOR (Fo4EngineVertexLayout.PrepassColorStream: bit 49 or 59,
        ''' 0x141828F69..F8D): the alpha-test prepass multiplies the vertex alpha (cb2[0].y = 0, else 10000).</summary>
        Public HasColorStream As Boolean
        ''' <summary>The geometry's exact class is BSTriShape, BSSubIndexTriShape or BSMeshLODTriShape (RTTI 0x143449778,
        ''' 0x1438DF4C0, 0x143E71B70) and it has triangles.</summary>
        Public PrepassGeometry As Boolean
    End Structure

    Friend Enum Fo4List
        None
        ''' <summary>G-buffer bucket 4 (opaque lighting, group 0): the first draws of the G-buffer stage (0x1421F3C21 -&gt; 0x1421D6CD0,
        ''' edx = 4).</summary>
        GBufferOpaque
        ''' <summary>G-buffer list 0 (group 7, F4SF2 bit 1 LOD_Landscape): drawn after the decal lists 2 / 3 (0x1421F3C3B -&gt;
        ''' 0x1421D5EA0) at 0x1421F3C4C, before 0x1421D8450, list 1 (0x1421F3CD0) and list 4 (0x1421F3CDC). A linked list (bit0 of
        ''' +0x20, ctor 0x14221E2C3) that group 7 appends to (r9b = 0: 0x1421D4B78 -&gt; 0x1421D4EBE; 0x1422221C0 append
        ''' 0x142222262..28F), drawn head to tail pass by pass (0x1421D6F50 -&gt; 0x142220CA0 -&gt; 0x142220600, next +0x40): registration
        ''' order, no packet replay.</summary>
        GBufferLodLandscape
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

    ''' <summary>Item 2b: the walk of each list the app routes to (EngineListWalk). List 0 (group 7): linked (ctor 0x14221E2C3), routing
    ''' 0x1421D4B78 -&gt; 0x1421D4EBE (r9b = 0): append. List 0xE (group 0x14, the only group the app routes there): linked (ctor
    ''' 0x14221E797), routing 0x1421D4C15..C1E (r9b = 1; jump table 0x1421D4F8C, index group - 1 = 0x13; group 0x13 goes to 0x1421D4C0A, r9b = 0):
    ''' prepend; drawn at 0x1421F93D5 -&gt; 0x1421D6F50(acc, 0xE).</summary>
    Friend Function ListWalk(list As Fo4List) As EngineListWalk
        Select Case list
            Case Fo4List.GBufferOpaque, Fo4List.DecalOpaque, Fo4List.DecalTranslucent, Fo4List.ForwardEffect : Return EngineListWalk.TechniqueBuckets
            Case Fo4List.GBufferLodLandscape : Return EngineListWalk.Registration
            Case Fo4List.ForwardBillboard
#If DEBUG Then
                ' Item 2b oracles' directed mutant (Fo4GBufferConstants.GateMutantOn): list 0xE walked as an appending list.
                If Fo4GBufferConstants.GateMutantOn("lw-0xe-append") Then Return EngineListWalk.Registration
#End If
                Return EngineListWalk.RegistrationReverse
            Case Fo4List.Translucent : Return EngineListWalk.AlphaSorted
            Case Else : Return EngineListWalk.None
        End Select
    End Function

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
        ''' -&gt; 0x14217A237 (Tools/re-docs/RE_FO4_PASS_GROUPS_DEPTH_2026-10-03.md 8.1). MEASURED in the running game (F4seRenderDiag,
        ''' 6-oct-2026; AUDIT_APP_VS_GAME_EFFECTS_SSE_2026-10-05.md row F-BD): GetRenderPasses returns an empty list in modes 0x18 and
        ''' 0x00 for such shapes and no shader draws them. The preview lists the shape in the frame's notice ("Blended without decal:
        ''' not drawn"), never silent.
        ''' Given by Fo4GBufferTechnique.Technique itself, the function that leaves the shape without a technique.</summary>
        LightingBlendWithoutDecal
        ''' <summary>Lighting whose alpha (the one the preview draws, MaterialData.PreviewAlpha) is 0 or NaN without a decal bit:
        ''' GetRenderPasses clears the list (0x14217A144 / 0x14217A147 -> 0x14217A22A -> 0x14217A237), before the blend (0x14217A16A)
        ''' and refraction (0x14217A566) branches; the same list-clearing block is also reached from 0x14217A132 / 0x14217A150 /
        ''' 0x14217A15A, not modelled here. Given by Fo4GBufferTechnique.Technique (L1c of C2). The notice lists the shape under its
        ''' alpha reason (AlphaZeroNotice).</summary>
        LightingAlphaZero
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
        ''' <summary>The prepass entry is the alpha-test one (0x1A): discard tex.a * saturate(vc.a + y) &lt; PrepassThreshold
        ''' (= PrepassAlphaThreshold(ref), the exe's float32 law), y = 0 with a COLOR stream, else 10000 (0x141828F45..F8D).</summary>
        Public PrepassAlphaTest As Boolean
        Public PrepassThreshold As Single
        ''' <summary>The G-buffer's alpha test multiplies the vertex alpha: technique bit 0 (flag 37) without bits 10 / 6.</summary>
        Public GBufferTestVertexAlpha As Boolean
    End Structure

    ''' <summary>DepthBias / SlopeScaledDepthBias / DepthBiasClamp of a rasterizer bias index (table 0x141856170, switch 0x141856364;
    ''' DepthBiasClamp -100 in every state, 0x1424875E0). A D3D11.3 15.10 bias: the draw applies it through Gl43.DepthBias (GL's
    ''' polygon offset unit is the implementation's, measured per context; ola 4 v3).</summary>
    Friend Function BiasOf(index As Integer) As (DepthBias As Single, Slope As Single, Clamp As Single)
        Const clamp As Single = -100.0F
        Select Case index
            Case 1, 5 : Return (-3.0F, -0.4F, clamp)
            Case 2 : Return (-6.0F, -0.8F, clamp)
            Case 3, 4 : Return (-9.0F, -1.2F, clamp)
            Case 6 : Return (3.0F, 0.4F, clamp)
            Case 7 : Return (6.0F, 0.8F, clamp)
            Case 8 : Return (12.0F, 6.0F, clamp)
            Case Else : Return (0.0F, 0.0F, clamp)
        End Select
    End Function

    ''' <summary>FO4's alpha-test threshold as the exe computes it, in float32 (AlphaRefLaw, shared with SSE): cvtdq2ps(ref) *
    ''' [0x1429293FC] (0x3B808081,
    ''' 1/255) + [0x1426A4908] (0x3B80802C). Census of the readers of the bias, closed: 14 functions (every RIP-relative operand
    ''' of .text with a 0/1/2/4-byte immediate; none outside .pdata); of the ref-4 constant, 5. The census of 1/255 alone is
    ''' NOT closed and this law does not need it (24 candidates in code without unwind data, role not traced);
    ''' Tools/re-docs/RE_FO4_PASS_GROUPS_DEPTH_2026-10-03.md 9.4. With the ref-4 constant [0x142915880] (0x3C008056) in
    ''' place of the bias - BSDFPrePassShader
    ''' SetupGeometry 0x142205540 (G-buffer cb2[1].w, EmissiveColor.w; 0x142207354..37D), BSEffectShader SetupGeometry 0x1422261B0
    ''' (cb2[14].x; 0x1422274C8..509) and its MEMBRANE setup 0x142227B60, BSLightingShader SetupGeometry 0x14223B240 (forward),
    ''' BSDFCompositeShader SetupGeometry 0x1422174F0 without blend: MainPassAlphaThreshold; with a blending NiAlphaProperty the
    ''' composite writes the immediate 0x3F7EFEFF (254/255) instead (0x142217823..830). Bias for every ref - the z-prepass 0x1A
    ''' 0x141828DE0 (0x141828F45..F65) and the same driver's merge-instanced list 6, 0x14181FAA0 (0x1418209A0..9B9; only
    ''' BSMergeInstancedTriShape, whose vfunc 0x1F8 0x1417E93F0 returns this, built at runtime 0x14048F56B, never from a NIF):
    ''' PrepassAlphaThreshold. Bias plus 1/255 again for ref 4 - BSUtilityShader SetupGeometry 0x142241270, the shadow map
    ''' (0x142241F41..F64): ShadowMapAlphaThreshold; the G-buffer's cached constant block 0x142207C80 (called from
    ''' BSLightingShaderProperty::GetRenderPasses 0x14217AAA7; 0x142208603..632), which fills the same slot +0x5A .w as
    ''' SetupGeometry 0x142205540 with the same bits as MainPassAlphaThreshold; the per-instance builders 0x1421C9C00 (called from
    ''' 0x1421C96A0) and 0x1422421D0 (role not traced); 0x14223EC60, the per-instance array (+0x8C) of BSInstanceGroup (geometry
    ''' type 0xE), from the culling 0x1417E2830 (0x1417E2A7B) and 0x1417E2BC0 (0x1417E2E0B), w = 1.0 without the test
    ''' (0x14223F000..046), and its twin 0x14223E850 (same law, no fade gate; no caller: no call/jmp, RIP-relative operand or
    ''' absolute pointer reaches it); 0x1421DC470 builds the NVFlex debris (slot 48 of NVFlex::DebrisInstanceNode, vtable
    ''' 0x1429147A0, through 0x1421DC070). The preview builds no merge-instanced shape, no instance group and no Flex debris. In
    ''' every reader ref is the byte NiAlphaProperty+0x2A of the geometry ([geom+0x130]); a material file reaches it only through
    ''' the writer that applies it (0x1421715E0, 0x1421719C5..19CC), modelled in FO4UnifiedMaterial_Class.ResolveEngineAlpha.
    ''' Bits, not decimal literals: 0.00392153F is 0x3B80802E. Tools/re-docs/RE_FO4_PASS_GROUPS_DEPTH_2026-10-03.md 9.1;
    ''' Tools/re-docs/RE_FO4_ALPHA_REF_WRITERS_2026-10-15 (readers 1, writers 2); AUDIT_APP_VS_GAME_EFFECTS_SSE_2026-10-05.md D-B.</summary>
    Private ReadOnly AlphaRefBias4 As Single = BitConverter.Int32BitsToSingle(&H3C008056)

    ''' <summary>Threshold of the colour passes (G-buffer of the world lighting, forward of the effects): ref * 1/255 + bias,
    ''' bias4 for ref 4. With 8-bit alpha (k/255) it keeps k &gt;= ref + 1 for every ref except 4, which keeps k &gt;= 6 (the only
    ''' exception among the 256 refs; the prepass law keeps k &gt;= ref + 1 for all 256). Also EmissiveColor.w of the G-buffer
    ''' (Fo4GBufferConstants.EmissiveColor).</summary>
    Friend Function MainPassAlphaThreshold(ref As Byte) As Single
        Return AlphaRefLaw.Scaled(CSng(ref)) + If(ref = 4, AlphaRefBias4, AlphaRefLaw.RefBias)
    End Function

    ''' <summary>Threshold of the z-prepass 0x1A: ref * 1/255 + bias for every ref (0x141828F45..F65).</summary>
    Friend Function PrepassAlphaThreshold(ref As Byte) As Single
        Return AlphaRefLaw.ScaledBiased(ref)
    End Function

    ''' <summary>Threshold of the shadow map (BSUtilityShader SetupGeometry 0x142241270, constant [0x143E71830]+0x58,
    ''' 0x142241F41..F64): the prepass arithmetic, plus 1/255 again for ref 4 (`cmp [prop+0x2A], 4 / addss xmm0, xmm6`). Same
    ''' bits as MainPassAlphaThreshold for all 256 refs (256 of 256: the A/B of `fo4-pass-law`; exe side in
    ''' RE_FO4_PASS_GROUPS_DEPTH_2026-10-03.md 9.4), written as the shadow writer computes it. The other branches
    ''' of that writer do not reach a shadow-map pass: 254/255 when the property blends (the pass is not created with blend,
    ''' 0x14217B981..98A) and byte [+0x7C] / 255 with technique bit 20.</summary>
    Friend Function ShadowMapAlphaThreshold(ref As Byte) As Single
        Return PrepassAlphaThreshold(ref) + If(ref = 4, AlphaRefLaw.Inv255, 0.0F)
    End Function

    ''' <summary>cb2[14].x of a FO4 effect draw (SetupGeometry 0x1422261B0): 0 (0x14222649A) unless the geometry has a
    ''' NiAlphaProperty ([geom+0x130], 0x142226463/478) with the test bit 9 (0x1422274B7..4C6), then MainPassAlphaThreshold.
    ''' Every non-MEMBRANE b05 PS tests it (780 of 780: `mad r0.x, a, cb2[13].w, -cb2[14].x / lt / discard_nz`, rec0828
    ''' L84-86), so without a test an effect still discards alpha &lt; 0. Hole, declared: MEMBRANE (technique bit 9, set only
    ''' at runtime, 0 of 339 corpus techniques; SetupGeometry 0x1422264A5..DD, RE_BGEM_POINT_LIGHTS_FO4_2026-10-03.md:148)
    ''' has 102 PS without this test that discard only on cb2[14].y; the app does not draw it.</summary>
    Friend Function EffectAlphaThreshold(test As Boolean, ref As Byte) As Single
        Return If(test, MainPassAlphaThreshold(ref), 0.0F)
    End Function

    ''' <summary>THE alpha test of a FO4 shape's colour pass (Render.ApplyMaterial): whether it runs and against which threshold.
    ''' An effect (BGEM) tests always, against EffectAlphaThreshold; a lighting shape tests when its NiAlphaProperty does,
    ''' against MainPassAlphaThreshold. The shadow pass has its own writer: ShadowPassAlphaTest. Gates: `fo4-pass-law` (values),
    ''' `fo4-alpha-wiring` (call site).</summary>
    Friend Function ColourPassAlphaTest(isEffect As Boolean, test As Boolean, ref As Byte) As (Test As Boolean, Threshold As Single)
        If isEffect Then Return (True, EffectAlphaThreshold(test, ref))
        Return (test, MainPassAlphaThreshold(ref))
    End Function

    ''' <summary>THE alpha test of a FO4 shape's shadow-map pass (RenderableMesh.RenderShadowMapFo4: its utility technique's bit 7 and
    ''' AlphaTestRef, ola 4): a
    ''' lighting shape (the only family that has one, ShadowMapCasts). BSLightingShaderProperty slot 44 0x14217B8F0 sets the
    ''' BSUtilityShader technique bit 7 only with the NiAlphaProperty test bit 9 (0x14217BB32..B4A), and BSUtilityShader
    ''' SetupGeometry writes ShadowMapAlphaThreshold. Gates: `fo4-pass-law` (values), `fo4-alpha-wiring` (call site).</summary>
    Friend Function ShadowPassAlphaTest(test As Boolean, ref As Byte) As (Test As Boolean, Threshold As Single)
        Return (test, ShadowMapAlphaThreshold(ref))
    End Function

    ''' <summary>Does Fallout 4 build a SHADOW-MAP pass for this shape (vtable slot 44 of its property; Tools/re-docs/
    ''' RE_FO4_PASS_GROUPS_DEPTH_2026-10-03.md 9.4.3)? An effect: never - BSEffectShaderProperty (vtable 0x1429032E0) slot 44 =
    ''' 0x142168DA0 `xor eax,eax; ret`. A lighting shape, BSLightingShaderProperty slot 44 0x14217B8F0, builds none when
    ''' material alpha (+0x80) x fade (+0x1A0) is below [0x142929458] = 1.0 or unordered (comiss / jb, 0x14217B95C..973; the
    ''' preview has no fade: s.Alpha is the alpha it draws), when its flags carry 0xC008004 (F4SF1 2 Temp_Refraction, 15
    ''' Refraction, 26 / 27 decal; 0x14217B975..97F) or when its NiAlphaProperty blends (+0x28 bit 0, 0x14217B981..98A).
    ''' Those three cuts come before any mode comparison, so they hold in every shadow mode (0xF spot, 0x10 sun and focus shadows,
    ''' 0x11 omni; D:\WinTmp\rc\re29\re.md 1), and both registrars (0x1421D51A0 / 0x1421D54C0) already drop a bit-26 shape before slot
    ''' 44 (0x1421D51BC / 0x1421D54DC); no other path adds a caster (re29 2). After the cuts the pass is built with Cast_Shadows (bit
    ''' 9), or with bit 34 LOD_Objects in mode 0x10 (0x14217B98C..9A5: bt 9, test 0x400000000, cmp ebp 0x10). The preview's shadow map
    ''' is the directional one, mode 0x10 (Fo4UtilityTechnique.ShadowMapTechnique, ola 4), so its law is Cast_Shadows OR
    ''' LOD_Objects. Gate: `fo4-pass-law`.</summary>
    Friend Function ShadowMapCasts(s As Fo4ShapeInputs) As Boolean
        Return ShadowMapCastsCut(s) Is Nothing
    End Function

    ''' <summary>THE SAME LAW WITH ITS REASON (the one owner: ShadowMapCasts reads it): Nothing when the shape casts, else the cut of
    ''' slot 44 that leaves it out, in the exe's order - the diagnostics (ShadowGate --casters) report it instead of a loose bit.</summary>
    Friend Function ShadowMapCastsCut(s As Fo4ShapeInputs) As String
        If s.IsEffect Then Return "an effect (BSEffectShaderProperty slot 44 0x142168DA0: no shadow pass)"
        If Not (s.Alpha >= 1.0F) Then Return "material alpha below 1 (0x14217B95C..973)"
        If s.TempRefraction OrElse s.Refraction OrElse s.Decal Then Return "flags 0xC008004: Temp_Refraction / Refraction / decal (0x14217B975..97F)"
        If s.Blend Then Return "its NiAlphaProperty blends (0x14217B981..98A)"
#If DEBUG Then
        ' ShadowGate full ([castshadows-shape], ola 8): the filter stops reading Cast_Shadows.
        If Fo4GBufferConstants.GateMutantOn("cast-flag-ignored") Then Return Nothing
#End If
        If Not (s.CastShadows OrElse s.LodObjects) Then Return "neither Cast_Shadows (F4SF1 9) nor LOD_Objects (F4SF2 2) (0x14217B98C..9A5)"
        Return Nothing
    End Function

    ''' <summary>BSLightingShaderProperty::GetRenderPasses 0x14217A050, world modes 0x18 / 0x12: the material alpha (vfunc 0x198,
    ''' material +0x80) equal to 0 or unordered (NaN) leaves the shape with no pass unless a decal bit (F4SF1 26 / 27) is set:
    ''' 0x14217A144 ucomiss / 0x14217A147 je 0x14217A227 -> 0x14217A22A and r12d, 0xC000000 / jne (decal: on to 0x14217A16A) ->
    ''' 0x14217A237 (the list cleared, 0x142168A90). It comes BEFORE the blend-without-decal (0x14217A16A) and refraction
    ''' (0x14217A566) branches. Effects have no such branch. Scalar, ONE formula: Fo4GBufferTechnique.Technique applies it to the
    ''' alpha the preview draws (L1c of C2 v3), MaterialData.AlphaLawSkips to any alpha of a material (the raw one for the notice).</summary>
    Friend Function LightingAlphaSkipsPasses(isEffect As Boolean, alpha As Single, decal As Boolean) As Boolean
        Return Not isEffect AndAlso (Single.IsNaN(alpha) OrElse alpha = 0.0F) AndAlso Not decal
    End Function

    ''' <summary>The same law on synthetic inputs (ParityGate); a material's skip is MaterialData.EngineAlphaSkipsPasses.</summary>
    Friend Function LightingAlphaSkipsPasses(s As Fo4ShapeInputs) As Boolean
        Return LightingAlphaSkipsPasses(s.IsEffect, s.Alpha, s.Decal)
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
            If s.LightingNoPass = Fo4NoPass.LightingAlphaZero Then
                ' 0x14217A147 clears the whole list BEFORE the refraction branch (0x14217A566): no refraction pass either (C2 v6 L1c).
                r.Drawn = False : r.NoPass = Fo4NoPass.LightingAlphaZero
            ElseIf s.Refraction OrElse s.TempRefraction Then
                r.Group = 5 : r.Drawn = False : r.NoPass = Fo4NoPass.RefractionOnly
            ElseIf Not s.LightingGroup.HasValue Then
                ' The technique's own reason. Its blend-without-decal branch (0x14217A16A) is measured in the running game (AUDIT row
                ' F-BD: empty pass list, never drawn); listed in the frame's notice.
                r.Drawn = False : r.NoPass = s.LightingNoPass
            Else
                r.Group = s.LightingGroup.Value
            End If
        End If

        Select Case r.Group
            Case 0 : r.List = If(s.IsEffect, Fo4List.ForwardEffect, Fo4List.GBufferOpaque)
            Case 7 : r.List = Fo4List.GBufferLodLandscape
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
            If r.PrepassAlphaTest Then r.PrepassThreshold = PrepassAlphaThreshold(s.TestRef)
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
