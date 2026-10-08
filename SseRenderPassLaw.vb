''' <summary>HOW SKYRIM SE DRAWS ONE SHAPE IN THE WORLD RENDER: its pass group, whether its depth goes into the z-prepass and
''' with which alpha test, the depth function and write of its colour pass, the main-pass alpha test and the cb12[42].z its
''' lighting tail sees. One pure law (gate ParityGate `sse-pass-law`), read by RenderAll / Render / the prepass.
''' Sources (SkyrimSE.exe 1.7.104.0): Tools/re-docs/RE_SSE_PASS_GROUPS_DEPTH_2026-10-03.md (groups 1-2, lists and finishes
''' 3, final table 4, prepass alpha law 10, main-pass alpha 12, depth branches 11), RE_SSE_LIGHTING_PREPASS_FOGTAIL_2026-10-03.md
''' (slot 0x2B 1.2-1.5, EQUAL 4), RE_SSE_ZPREPASS_BGSM_JSON_2026-10-03.md (the prepass 0x141538FA0 and its copy).
''' <para>The preview's frame is a world render with early-Z on (default, [0x143336405] = 0), no fade (currentFade 1: no
''' fade node fades), not first person (acc+0xB9 = 0), camera above water, no particle draws and no LOD copies.</para></summary>
Friend Module SseRenderPassLaw

    ''' <summary>What the law reads from a shape: its shader property flags (SLSF1 / SLSF2 bits as nif.xml numbers them),
    ''' its NiAlphaProperty and its geometry.</summary>
    Friend Structure SseShapeInputs
        Public IsEffect As Boolean
        ''' <summary>BSEffect: baseColor.a (material+0x54); BSLighting: materialAlpha (+0x80). The fade is 1.</summary>
        Public Alpha As Single
        Public AlphaPropertyPresent As Boolean
        Public Blend As Boolean
        Public Test As Boolean
        ''' <summary>NiAlphaProperty threshold byte (+0x32).</summary>
        Public TestRef As Byte
        Public ZTest As Boolean          ' SLSF1 31
        Public ZWrite As Boolean         ' SLSF2 0
        Public Decal As Boolean          ' SLSF1 26
        Public DynamicDecal As Boolean   ' SLSF1 27
        Public HairSoftLighting As Boolean ' SLSF1 18 (clib kHairTint: the "decal that writes depth" bit)
        Public Refraction As Boolean     ' SLSF1 15
        Public TempRefraction As Boolean ' SLSF1 2
        Public Falloff As Boolean        ' SLSF1 6 (effect)
        Public Billboard As Boolean      ' SLSF2 13 (bit 45)
        Public NoTransparencyMultisampling As Boolean ' SLSF2 22 (bit 54)
        Public LodLandscape As Boolean   ' SLSF2 1
        Public ProjectedUV As Boolean    ' SLSF1 23
        Public ParallaxOcclusion As Boolean ' SLSF1 28
        Public MultiTextureLandscape As Boolean ' SLSF1 14
        Public MultiIndexSnow As Boolean ' SLSF2 9 (bit 41)
        Public Specular As Boolean       ' SLSF1 0
        Public Slsf1Bit30 As Boolean     ' SLSF1 30: the second snow test (0x14151AAD1..AD5)
        Public LodObjects As Boolean     ' SLSF2 2 (bit 34)
        Public NoLodLandBlend As Boolean ' SLSF2 14 (bit 46)
        Public Slsf2Bit28 As Boolean     ' SLSF2 28 (bit 60): the first snow test (0x14151AA9E..AABB)
        Public TreeAnim As Boolean       ' SLSF2 29 (bit 61)
        Public HdLodObjects As Boolean   ' SLSF2 31 (bit 63)
        ' The rest of the lighting technique-type selector (LitTechniqueType), of the cb2[6] write (LitViewVectorWritten) and the
        ' effect's BLOOD bit (EffectBloodTechnique).
        Public EnvMap As Boolean         ' SLSF1 7
        Public FaceGenDetail As Boolean  ' SLSF1 10
        Public Parallax As Boolean       ' SLSF1 11
        Public Eye As Boolean            ' SLSF1 17 (Eye_Environment_Mapping)
        Public FaceGenRgbTint As Boolean ' SLSF1 21
        Public GlowMap As Boolean        ' SLSF2 6 (bit 38)
        Public WeaponBlood As Boolean    ' SLSF2 17 (bit 49): effect technique bit 14 BLOOD (0x14152A3A3..3B6, 0x14152A580..595)
        Public MultiLayerParallax As Boolean ' SLSF2 24 (bit 56)
        Public SoftLighting As Boolean   ' SLSF2 25 (bit 57): descriptor bit 10
        Public RimLighting As Boolean    ' SLSF2 26 (bit 58): descriptor bit 11
        Public BackLighting As Boolean   ' SLSF2 27 (bit 59): descriptor bit 12
        ''' <summary>SLSF1 1 = the LIGHTING technique's bit 1 SKINNED (descriptor 0x14151A976..A985); the effect's bit 3 is
        ''' <see cref="Skinned"/>.</summary>
        Public LitSkinned As Boolean
        ''' <summary>The vertex declaration carries NORMAL (vertexDesc bit 47 or 57).</summary>
        Public HasNormals As Boolean
        ''' <summary>BSEffect with greyscale-to-palette-alpha and its palette texture (technique bit 15).</summary>
        Public PaletteAlpha As Boolean
        ''' <summary>The vertex colour stream the VS reads (vertexDesc COLOR / kVertexColors).</summary>
        Public HasVertexColors As Boolean
        ''' <summary>Effect technique bit 3: <see cref="EffectSkinnedBit"/> of the shape.</summary>
        Public Skinned As Boolean
        ' O-OP (chunk C8): the rest of the bits the pass ID, the sub-list and the slot read (PassId / SubList / OpaqueSlot). SseInputs
        ' fills each one from the material where Save_To_Shader writes the bit from it, else from the block (census of the SK path).
        Public VertexColorsFlag As Boolean ' SLSF2 5 (bit 37): descriptor bit 0, effect technique bit 0 (the save does not write it)
        Public ModelSpaceNormals As Boolean ' SLSF1 12: descriptor bit 2 (the save writes ModelSpace from ModelSpaceNormals)
        Public TwoSided As Boolean         ' SLSF2 4 (bit 36): batch sub-list bit 1 (the save writes DoubleSided from TwoSided)
        Public PackedTangent As Boolean    ' SLSF2 8 (bit 40): descriptor bit 22 (the save does not write it)
        Public AnisoLighting As Boolean    ' SLSF2 21 (bit 53): descriptor bit 16 (the save writes it from AnisoLighting)
        Public Slsf2Bit23 As Boolean       ' SLSF2 23 (bit 55): the descriptor tail's bit 18 (0x14151AE2E..E56; the save does not write it)
        Public EffectLighting As Boolean   ' SLSF2 30 (bit 62) of a BSEffect: technique bit 16 (the BGEM save writes it from EffectLightingEnabled)
        ''' <summary>SLSF1 30 Soft_Effect of a BSEffect: technique bit 18 (the BGEM save writes it from SoftEnabled). The lighting reading
        ''' of the same bit (no writer: the block's) is <see cref="Slsf1Bit30"/>.</summary>
        Public SoftEffect As Boolean
        ''' <summary>BSEffect with greyscale-to-palette-colour (SLSF1 4) and its palette texture: technique bit 19 (0x14152A3D7..3F6, the
        ''' texture by 0x140EB12C0 on material+0x78). The alpha twin is <see cref="PaletteAlpha"/>.</summary>
        Public PaletteColor As Boolean
        ''' <summary>BSEffect: the base texture path is not empty (technique 0x42: 0x140EB12C0 on material+0x70; C4).</summary>
        Public EffectBaseTexture As Boolean
        ''' <summary>The NiAlphaProperty blend functions the effect technique reads (flags bits 1-4 and 5-8, 0x14152A4B9..4FD).</summary>
        Public AlphaSrc As Integer
        Public AlphaDst As Integer
    End Structure

    Friend Enum SseDepthFunc
        ''' <summary>Depth mode 0: DepthEnable off, no test and no write (0x14102B178).</summary>
        Off
        LessEqual
        ''' <summary>Depth mode 4: EQUAL, no write - the opaque batch against the prepass (0x14151EF40).</summary>
        Equal
    End Enum

    ''' <summary>The colour-pass list the shape lands in, in the order the two finishes draw them (3.2): the opaque
    ''' batch (EQUAL), the billboards (list 0xD), the opaque decals (list 3), the translucent decals (list 4), the
    ''' translucent list 0x10 (sorted back to front).</summary>
    Friend Enum SseList
        None
        OpaqueBatch
        Billboard
        DecalOpaque
        DecalTranslucent
        Translucent
    End Enum

    ''' <summary>Why a shape has no colour pass (SsePass.Drawn = False).</summary>
    Friend Enum SseNoPass
        None
        ''' <summary>Lighting with kRefraction or kTempRefraction: only the utility pass (0x14151A6BB..77E) - the refraction-normals
        ''' pass draws it (user decision 5-oct-2026: no notice).</summary>
        RefractionOnly
        ''' <summary>Effect with kTempRefraction and kDynamicDecal: no pass (0x141529DD7..DE5).</summary>
        EffectTempRefractionDynamicDecal
        ''' <summary>Lighting technique 6 (Hair) with DEPTH_WRITE_DECALS and without DO_ALPHA_TEST: no such PS in the cache,
        ''' SetupTechnique fails and the pass is skipped (RE_SSE_PASS_GROUPS_DEPTH 13.2).</summary>
        HairDepthWriteDecalWithoutAlphaTest
        ''' <summary>Lighting whose drawn alpha (MaterialData.PreviewAlpha) is 0 or NaN: no pass (0x14151A550 / 0x14151A554 -> 0x14151B185),
        ''' no decal exception, before the refraction branch (0x14151A6BB); the same list-clearing target is also reached from
        ''' 0x14151A55C, not modelled here (C2 L1c). The notice lists the shape under its alpha reason (AlphaZeroNotice).</summary>
        LightingAlphaZero
    End Enum

    Friend Structure SsePass
        Public Group As Integer
        Public List As SseList
        ''' <summary>The shape has a colour pass (refraction lighting has only its refraction-normals pass; an effect with
        ''' kTempRefraction + kDynamicDecal has none).</summary>
        Public Drawn As Boolean
        ''' <summary>Why Drawn is False (SseNoPass.None when it is True).</summary>
        Public NoPass As SseNoPass
        Public InPrepass As Boolean
        ''' <summary>The prepass PS has the alpha test (technique bit 7: the NiAlphaProperty test).</summary>
        Public PrepassAlphaTest As Boolean
        ''' <summary>thrG = cb2[2].x of the prepass (0x141567EC3..F27).</summary>
        Public PrepassThreshold As Single
        Public DepthFunc As SseDepthFunc
        Public DepthWrite As Boolean
        ''' <summary>Main colour pass discards alpha &lt; MainAlphaThreshold (lighting DO_ALPHA_TEST / effect PS).</summary>
        Public MainAlphaTest As Boolean
        Public MainAlphaThreshold As Single
        ''' <summary>BSEffect technique bit 26 (<see cref="EffectTechniqueBit26"/>): its PS has no alpha test; its output alpha
        ''' is the computed one (AUDIT_SSE_RE: idx 2359 writes o0.w = A, correcting RE_SSE_PASS_GROUPS 10.5).</summary>
        Public EffectNoAlphaTest As Boolean
        ''' <summary>cb12[42].z of the lighting tail (lists 7 / 0x10 are drawn with flags | 4).</summary>
        Public TailZ As Single
        ''' <summary>BSLighting technique 6 (Hair) with descriptor bit 15 (DEPTH_WRITE_DECALS): the main PS discards
        ''' a &lt; 4/255, then saturate(1.05 a) &lt; threshold, and outputs saturate(1.05 a) as its alpha (13.2, 13.3).</summary>
        Public HairDepthWriteDecal As Boolean
    End Structure

    ''' <summary>The engine's default alpha-test reference [0x1420CFCB8]: the immediate 0x3F008081 (128/255) written by the opaque
    ''' finish (0x14151F13B..156, after ucomiss against [0x141B3F23C] = the same bits); 0 only after a snow / rain emitter draws
    ''' (BSParticleShader 0x141579E48; the preview draws none). It reaches only a pass that turns the test on WITHOUT setting its
    ''' own reference (Tools/re-docs/AUDIT_SSE_RE_2026-10-03.md S2).</summary>
    Friend ReadOnly GlobalAlphaTestRef As Single = BitConverter.Int32BitsToSingle(&H3F008081)

    ''' <summary>cb11[0].x of a pass (AUDIT_SSE_RE S2, correcting RE_SSE_PASS_GROUPS 10.2 / 12, whose writer census missed
    ''' 0x141578930): a pass drawn with the alpha test that has a NiAlphaProperty sets its OWN reference right before drawing,
    ''' SubgroupRefThreshold(NiAlphaProperty.ref, prop+0x30) (0x141562270, 0x1415622C7..EB; prop+0x30 = materialAlpha * fade for
    ''' lighting, fade * baseColor.a for an effect; fade 1 here) - in the prepass, the opaque batch, the sorted list and the shadow
    ''' map (the utility SHADOWMAP PS test only cb11[0].x: idx 11306 / 11307, 34 of 34). The subgroup (0x141560D10) turns the test on
    ''' for NiAlphaProperty test or SLSF2 No_Transparency_Multisampling (bit 54); the latter without a NiAlphaProperty keeps the
    ''' default 128/255. No test: 0. The shadow map's own test decision: ShadowAlphaTest.</summary>
    Friend Function SubgroupThreshold(s As SseShapeInputs) As Single
        If s.AlphaPropertyPresent AndAlso s.Test Then Return SubgroupRefThreshold(s.TestRef, s.Alpha)
        If s.NoTransparencyMultisampling Then Return GlobalAlphaTestRef
        Return 0.0F
    End Function

    ''' <summary>The reference 0x141578930 computes, in the exe's ops: cvtdq2ps(ref) * prop+0x30 (float32 mulss, 0x14157893B..93E);
    ''' cvttss2si rcx, the 64-bit truncation toward zero (0x141578944; NaN or a product outside the int64 range give the integer
    ''' indefinite 0x8000000000000000); mov ecx, ecx keeps its low 32 bits (0x141578951); cvtsi2ss rcx (0x141578953); mulss
    ''' [0x141B5BDC8] = AlphaRefLaw.Inv255 (0x141578958). So a negative product wraps to 2^32 - n (a threshold far above 1: every
    ''' texel discarded) and NaN gives 0. Neither the double product (it truncates one lower where the float32 product rounds up
    ''' to an integer, e.g. 240 * 0x3F666666) nor n / 255 (126 of 256 n differ from n * K1). With 8-bit alpha k/255 it keeps
    ''' k &gt;= n + 1 for those 126 n (n * K1 is 1 ulp above float32(n / 255)) and k &gt;= n for the other 130.</summary>
    Friend Function SubgroupRefThreshold(ref As Byte, propAlpha As Single) As Single
        Dim p As Single = CSng(ref) * propAlpha
        Dim n As Long
        If Single.IsNaN(p) OrElse Math.Abs(CDbl(p)) >= 9.2233720368547758E+18 Then
            n = Long.MinValue
        Else
            n = CLng(Math.Truncate(CDbl(p)))
        End If
        Return AlphaRefLaw.Scaled(CSng(CUInt(n And &HFFFFFFFFL)))
    End Function

    ''' <summary>thrG = cb2[2].x of the z-prepass, BSUtilityShader::SetupGeometry 0x1415673F0, in the exe's float32 ops: a
    ''' NiAlphaProperty with blend (flags +0x30 bit 0) gives the immediate 0x3F7EFEFF (254/255, 0x141567EE7..EF5); else
    ''' AlphaRefLaw.ScaledBiased(ref +0x32) (0x141567EF7..F17) and, for ref 4, one more addss of the same [0x141B5BDC8] on the
    ''' stored sum (0x141567F1C..F27). With 8-bit alpha (k/255) it keeps k &gt;= ref + 1 for every ref except 4, which keeps
    ''' k &gt;= 6 (the only exception among the 256 refs). Hole, declared: with effectData (utility bit 20,
    ''' 0x1415678D3..8E1) the exe writes effectData+0x7C * 1/255 without bias (0x141567EC3..EE0); effectData is set at runtime,
    ''' never from the NIF. The BSEffect writer 0x1415583F6..436 (technique bit 23) uses this same arithmetic.</summary>
    Friend ReadOnly PrepassBlendThreshold As Single = BitConverter.Int32BitsToSingle(&H3F7EFEFF)

    Friend Function PrepassThrG(blend As Boolean, ref As Byte) As Single
        If blend Then Return PrepassBlendThreshold
        Dim t = AlphaRefLaw.ScaledBiased(ref)
        If ref = 4 Then t += AlphaRefLaw.Inv255
        Return t
    End Function

    ''' <summary>THE alpha test of a SSE shape's shadow-map pass: whether it runs and against which threshold, the pair of
    ''' Fo4RenderPassLaw.ShadowPassAlphaTest for RenderDepthOnly (Shader_Class SHADOW SYNC CONTRACT; the compared quantity is
    ''' Fragment_ShadowDepth's, app design). Test: the lighting slot 0x2B builds the utility SHADOWMAP technique with bit 7 (the
    ''' only SHADOWMAP PS that discard: 34 of 177 utility PS, bit 7 or stipple) exactly when the NiAlphaProperty has its test
    ''' bit 9 (0x14151B641..65A); SLSF2 bit 54 (No_Transparency_Multisampling) does not enter the technique. Threshold: cb11[0].x
    ''' of the batch, SubgroupThreshold (shadow-map finish 0x1415206A0 -> slot 0x2B -> 0x141521190 -> 0x14155FF40 ->
    ''' 0x141560340 -> 0x141562270 -> 0x141578930). Who reaches the pass is not decided here: an effect never reaches the shadow
    ''' pass (SseRenderPassLaw.ShadowMapCasts / the caster filter; effect slot 0x2B 0x14152A0C2 builds a pass only in mode 0xC),
    ''' and the lighting slot 0x2B's own exclusions (blend, alpha * fade below 1) are that same filter's. Gates: `sse-pass-law`
    ''' (values), `fo4-alpha-wiring` (the SSE branch of the shadow upload).</summary>
    Friend Function ShadowAlphaTest(s As SseShapeInputs) As (Test As Boolean, Threshold As Single)
        Return (s.AlphaPropertyPresent AndAlso s.Test, SubgroupThreshold(s))
    End Function

    ''' <summary>BSEffect technique bit 26 (0x14152A43A..47C): NORMAL ∧ (no NiAlphaProperty ∨ its test) ∧ ZTest ∧ ZWrite. Its PS has no alpha test (RE_SAO_BOTH 12.6.1) and writes the vertex normal to o2 (12.6.3).</summary>
    Friend Function EffectTechniqueBit26(s As SseShapeInputs) As Boolean
        Return s.HasNormals AndAlso (Not s.AlphaPropertyPresent OrElse s.Test) AndAlso s.ZTest AndAlso s.ZWrite
    End Function

    ''' <summary>BSLightingShaderProperty::GetRenderPasses 0x14151A160: prop+0x30 = material +0x80 * fade (0x14151A471..4AA; fade 1)
    ''' equal to 0 or unordered (NaN) clears the pass list: 0x14151A550 ucomiss / 0x14151A554 je 0x14151B185 -> 0x1414E77F0. No decal
    ''' exception. It comes before the refraction branch (0x14151A6BB). Effects have no such branch. Scalar, ONE formula: Classify applies
    ''' it to SseShapeInputs.Alpha, the alpha the preview draws (C2 v3 L1c); MaterialData.AlphaLawSkips to the raw alpha for the notice.</summary>
    Friend Function LightingAlphaSkipsPasses(isEffect As Boolean, alpha As Single) As Boolean
        Return Not isEffect AndAlso (Single.IsNaN(alpha) OrElse alpha = 0.0F)
    End Function

    ''' <summary>The filters of BSLightingShaderProperty slot 0x2B (0x14151B1E0) that do not look at the mode - the same code for
    ''' the z-prepass (0xC) and the shadow maps (0xD..0xF), all through 0x14151B2F5 (Tools/re-docs/
    ''' RE_SSE_LIGHTING_PREPASS_FOGTAIL_2026-10-03.md 1.2): a decal (bits 26 | 27) WITH bit 18 builds a pass only with
    ''' kZBufferWrite and a NiAlphaProperty that blends (0x14151B38D..3C0; a decal without bit 18 is not filtered here,
    ''' 0x14151B395 je 0x14151B3C3); currentFade x materialAlpha below 1.0 or NaN builds none (0x14151B3C3..3E3; the fade is
    ''' 1 and the stipple branch is mode 0xC only); kRefraction / kTempRefraction none (flags AND 0x8004, 0x14151B3E9); a
    ''' NiAlphaProperty that blends none, except the decal that writes depth (0x14151B3F6..405). Read by the prepass
    ''' (Classify), which adds its accumulator's registration filter (decalPasaFiltro, 0x14151EC1A..EC7D), and by the shadow
    ''' map (ShadowMapCasts).</summary>
    Friend Function LightingSlot2BPasses(s As SseShapeInputs) As Boolean
        Dim decal = s.Decal OrElse s.DynamicDecal
        Dim blend = s.AlphaPropertyPresent AndAlso s.Blend
        Dim depthWriteDecal = decal AndAlso s.HairSoftLighting AndAlso s.ZWrite AndAlso blend   ' r9b, 0x14151B3C0
        If decal AndAlso s.HairSoftLighting AndAlso Not depthWriteDecal Then Return False      ' 0x14151B38D..3BA
        If Not (s.Alpha >= 1.0F) Then Return False
        If s.Refraction OrElse s.TempRefraction Then Return False
        Return Not blend OrElse depthWriteDecal
    End Function

    ''' <summary>Does Skyrim SE build a SHADOW-MAP pass for this shape? An effect: never - BSEffectShaderProperty slot 0x2B
    ''' (0x14152A0A0) returns null outside mode 0xC (0x14152A0C2; RE_FO4_PASS_GROUPS_DEPTH_2026-10-03.md 9.5). A lighting
    ''' shape: LightingSlot2BPasses, except a decal (a declared hole, below). Cast_Shadows: where Skyrim SE reads it is not
    ''' traced; the caster filter reads the material's CastShadows (as before). Gate: `sse-pass-law`.</summary>
    Friend Function ShadowMapCasts(s As SseShapeInputs) As Boolean
        If s.IsEffect Then Return False
        ' HOLE, declared: a decal (bits 26 | 27). Slot 0x2B lets some through and alpha-tests every decal (0x20080,
        ' 0x14151B5ED..5F6 / 0x14151B6C8..6D2), but whether the shadow-map accumulators register a decal (0x14151EC10,
        ' +0x12C / +0x12D) and the cb11 of a decal without a NiAlphaProperty test are not traced: kept out, as before group B.
        If s.Decal OrElse s.DynamicDecal Then Return False
        Return LightingSlot2BPasses(s)
    End Function

    ''' <summary>The lists of the opaque finish 0x14151EF40 in the order it draws them (RE_SSE_PASS_GROUPS_DEPTH 3.2): the batch
    ''' RenderBatches[1, 0x5C00002F], list 9 (group 0xB), list 8 (group 0xF), list 1 (group 7), list 0 (group 6).</summary>
    Friend Enum SseOpaqueSlot
        Batch = 0
        List9 = 1
        List8 = 2
        List1 = 3
        List0 = 4
    End Enum

    ''' <summary>The preview frame of the opaque order (D-L2): no point light and no shadow-casting light of the engine reach a shape
    ''' (AUDIT section 3: no point lights, the preview's own shadows), fade 1 (this module's header). So the descriptor's bits 3-8,
    ''' 13, 14 and 23 are 0, and [rsp+0x50] = [rsp+0x58] = 0 clear r9b (0x14151A8C3..8D3).</summary>
    Private Const PreviewHasEngineShadowLight As Boolean = False

    ''' <summary>[0x1420D6D70] (effect lighting), image value.</summary>
    Private Const EffectLightingOn As Boolean = True

    ''' <summary>O-OP: the list of the opaque finish a shape of the opaque batch list lands in. Lighting (0x14151ACB6..ACFF): bits
    ''' 33|34|63 -&gt; group 6 (bit 33) or 7; bit 61 -&gt; 0xB; no engine shadow (the preview frame) -&gt; 0xF; else 0. Effect: group 0 (batch).</summary>
    Friend Function OpaqueSlot(s As SseShapeInputs) As SseOpaqueSlot
        If s.IsEffect Then Return SseOpaqueSlot.Batch
        If s.LodLandscape OrElse s.LodObjects OrElse s.HdLodObjects Then Return If(s.LodLandscape, SseOpaqueSlot.List0, SseOpaqueSlot.List1)
        If s.TreeAnim Then Return SseOpaqueSlot.List9
        If Not PreviewHasEngineShadowLight Then Return SseOpaqueSlot.List8
        Return SseOpaqueSlot.Batch
    End Function

    ''' <summary>O-OP: the batch sub-list (0x141560D4F..D86): bit 54 -&gt; 4, else the NiAlphaProperty test bit | (bit 36 kTwoSided ? 2 : 0).</summary>
    Friend Function SubList(s As SseShapeInputs) As Integer
        If s.NoTransparencyMultisampling Then Return 4
        Return If(s.Test, 1, 0) Or If(s.TwoSided, 2, 0)
    End Function

    ''' <summary>O-OP: the pass ID the batch orders by. Lighting: the descriptor (0x14151A932..AE58) + 0x4800002D (0x14151AE58); effect:
    ''' the technique (0x14152A2B0) + 0x4000002C (0x141529E13).</summary>
    Friend Function PassId(s As SseShapeInputs) As UInteger
        If s.IsEffect Then Return EffectTechnique(s) + &H4000002CUI
        Return LitDescriptor(s) + &H4800002DUI
    End Function

    ''' <summary>The lighting descriptor in the preview frame (D-L2), transcribed from 0x14151A932..AE58 (dump of 7-oct-2026). Two
    ''' masks of the exe: the snow tests use 0x8000400600000000 (0x14151AA2C, 0x14151AADC; bits 63|46|34|33) and the tail
    ''' 0x8000000600000000 (0x14151AE2E; bits 63|34|33, NOT 46).</summary>
    Friend Function LitDescriptor(s As SseShapeInputs) As UInteger
        Dim decal = s.Decal OrElse s.DynamicDecal                                   ' bits 26|27 (f &amp; 0xC000000)
        Dim translucent = Not (s.Alpha >= 1.0F) OrElse s.Blend                      ' T, 0x14151A598..5B0 (fade 1)
        Dim tailLod = s.LodLandscape OrElse s.LodObjects OrElse s.HdLodObjects      ' 0x8000000600000000
        Dim snowLod = tailLod OrElse s.NoLodLandBlend                               ' 0x8000400600000000
        Dim d As UInteger = 0UI                                                     ' bits 3-8: lights, 0 in the preview frame
        If s.VertexColorsFlag Then d = d Or 1UI                                     ' bit 37, 0x14151A956..973
        If s.LitSkinned Then d = d Or 2UI                                           ' bit 1, 0x14151A976..988
        If s.ModelSpaceNormals Then d = d Or 4UI                                    ' bit 12, 0x14151A98B..99F
        If s.Specular OrElse s.MultiIndexSnow Then d = d Or &H200UI                 ' mask 0x20000000001, 0x14151A9A2..9BB
        If s.SoftLighting Then d = d Or &H400UI                                     ' bit 57, 0x14151A9BE..9D6
        If s.RimLighting Then d = d Or &H800UI                                      ' bit 58, 0x14151A9DA..9F1
        If s.BackLighting Then d = d Or &H1000UI                                    ' bit 59, 0x14151A9F5..A0E
        ' bits 14 (r9b) and 13 ([rsp+0x50]): 0 in the preview frame (0x14151AA11..A27)
        If s.ProjectedUV Then                                                       ' bit 23, 0x14151AA3E..A5F
            d = d Or &H8000UI
            If s.Slsf1Bit30 AndAlso Not snowLod AndAlso ImprovedSnowExeDefault Then d = d Or &H80000UI
        End If
        If s.AnisoLighting Then d = d Or &H10000UI                                  ' bit 53, 0x14151AA64..A7B
        If (decal AndAlso Not s.MultiIndexSnow) OrElse (translucent AndAlso s.Test) Then d = d Or &H100000UI   ' 0x14151AA7F..A9A (AT: early-Z on)
        If SnowTechnique(s, ImprovedSnowExeDefault) Then d = d Or &H200200UI        ' snow A or B, 0x14151AA9E..AF0 (group C's one rule)
        If s.PackedTangent Then d = d Or &H400000UI                                 ' bit 40, 0x14151AAF6..B0F
        ' bit 23: stipple, 0 at fade 1 (0x14151AB12..B1D)
        If decal AndAlso s.ZWrite AndAlso s.HairSoftLighting Then d = d Or &H8000UI ' bits 26|27, 32, 18: 0x14151AB20..B42
        d = (CUInt(LitTechniqueType(s)) << 24) Or d                                 ' selector 0x14151AB5E..ADB5 (group C), 0x14151ADB8..ADBD
        ' 0x14151ADC0..AE2A: needs [rsp+0x50] != 0: not in the preview frame.
        If tailLod Then                                                             ' 0x14151AE2E..AE56
            d = d And Not &H4000UI
            If s.Slsf2Bit23 Then d = d Or &H40000UI
        End If
        Return d
    End Function

    ''' <summary>BSEffectShaderProperty's technique 0x14152A2B0 (dump of 7-oct-2026, 0x14152A2B0..5FB) for a mesh shape: no particles
    ''' and no strip (the preview draws none: 0x84, bit 12, 0x2013 never set).</summary>
    Friend Function EffectTechnique(s As SseShapeInputs) As UInteger
        Dim t As UInteger = If(s.VertexColorsFlag, 1UI, 0UI)                       ' bit 37, 0x14152A2CF..2E2, 0x14152A50D..510
        If s.EffectBaseTexture Then t = t Or &H42UI                                 ' 0x14152A2E5, 0x14152A514..518
        If s.Skinned Then t = t Or 8UI                                              ' 0x14152A2F2..312, 0x14152A527..534
        If s.Falloff Then t = t Or &H110UI                                          ' bit 6, 0x14152A31A..324, 0x14152A537..541
        If s.AlphaPropertyPresent Then                                              ' 0x14152A4B9..4FD
            If s.AlphaDst = 0 Then t = t Or &H400UI                                 ' (ap &amp; 0x1E0) = 0, 0x14152A562..56B
            If s.AlphaSrc = 4 AndAlso s.AlphaDst = 7 Then
                t = t Or &H400000UI                                                 ' (ap &amp; 0x1E) = 8, (ap &amp; 0x1E0) = 0xE0, 0x14152A57A..583
            ElseIf s.AlphaDst = 2 Then
                t = t Or &H800UI                                                    ' (ap &amp; 0x1E0) = 0x40, 0x14152A56E..577
            End If
        End If
        If s.WeaponBlood Then t = t Or &H4000UI                                     ' bit 49, 0x14152A3A3..3B6, 0x14152A586..592
        If s.EffectLighting AndAlso EffectLightingOn Then t = t Or &H10000UI        ' bit 62, 0x14152A377..399, 0x14152A595..5A3
        If s.ProjectedUV Then t = t Or &H20012UI                                    ' bit 23, 0x14152A3BB..3C4, 0x14152A5A6..5B3
        If s.SoftEffect Then t = t Or &H40000UI                                     ' bit 30, 0x14152A3C9..3D2, 0x14152A5B6..5C2
        If s.PaletteColor Then t = t Or &H80000UI                                   ' bit 4 + texture, 0x14152A3D7..3F6, 0x14152A5C5..5D3
        If s.PaletteAlpha Then t = t Or &H100000UI                                  ' bit 5 + texture, 0x14152A3FE..41C, 0x14152A5D6..5E1
        If s.Billboard Then t = t Or &H1000000UI                                    ' bit 45, 0x14152A421..432, 0x14152A5E4..5ED
        If EffectTechniqueBit26(s) Then t = t Or &H4000000UI                        ' 0x14152A43A..47C, 0x14152A5F0..5FB
        Return t
    End Function

    Friend Function Classify(s As SseShapeInputs) As SsePass
        Dim r As New SsePass With {.Drawn = True}
        Dim decal = s.Decal OrElse s.DynamicDecal
        Dim blend = s.AlphaPropertyPresent AndAlso s.Blend
        Dim test = s.AlphaPropertyPresent AndAlso s.Test
        ' NaN alpha counts as below 1 (comiss ... jb, 0x141529EAE).
        Dim alphaBelowOne = Not (s.Alpha >= 1.0F)

        ' ---- group (1, 2) ----
        Dim translucentLighting = alphaBelowOne OrElse blend   ' T, 0x14151A598..5B0
        If s.IsEffect Then
            If s.TempRefraction AndAlso s.DynamicDecal Then r.Drawn = False : r.NoPass = SseNoPass.EffectTempRefractionDynamicDecal   ' 0x141529DD7..DE5
            If decal Then
                r.Group = If(blend, 3, 2)                                            ' 0x141529E5A..EA0
            ElseIf s.Billboard Then
                r.Group = &H12                                                       ' 0x141529E83..E92
            ElseIf alphaBelowOne OrElse blend OrElse s.Falloff Then
                r.Group = 1                                                          ' 0x141529EAE..EF2
            Else
                r.Group = 0
            End If
        Else
            If LightingAlphaSkipsPasses(False, s.Alpha) Then
                ' 0x14151A550 ucomiss / 0x14151A554 je 0x14151B185 -> 0x1414E77F0: no pass, before refraction (C2 v3 L1c). s.Alpha is the
                ' alpha the preview draws (MaterialData.PreviewAlpha).
                r.Drawn = False : r.NoPass = SseNoPass.LightingAlphaZero
            ElseIf s.Refraction OrElse s.TempRefraction Then
                r.Group = 4 : r.Drawn = False : r.NoPass = SseNoPass.RefractionOnly ' only the utility pass, 0x14151A6BB..77E
            ElseIf decal Then
                r.Group = If(translucentLighting, 3, 2)                              ' 0x14151A913..92A
            ElseIf translucentLighting Then
                r.Group = 1                                                          ' fade 1: 0x14151AC17..C1B
            ElseIf s.LodLandscape Then
                r.Group = 6
            Else
                r.Group = 0                                                          ' 0 / 0xF / 0xB / 7: the same depth rules; their list order is OpaqueSlot (C8)
            End If
        End If

        ' ---- list (3.1) ----
        Select Case r.Group
            Case 0, 6 : r.List = SseList.OpaqueBatch
            Case 1 : r.List = SseList.Translucent
            Case 2 : r.List = SseList.DecalOpaque
            Case 3 : r.List = SseList.DecalTranslucent
            Case &H12 : r.List = SseList.Billboard
            Case Else : r.List = SseList.None
        End Select
        If Not r.Drawn Then r.List = SseList.None

        ' ---- prepass: slot 0x2B (FOGTAIL 1.2 / 1.3) and the registration filter (1.4) ----
        Dim decalPasaFiltro = Not decal OrElse (s.ZWrite AndAlso s.HairSoftLighting AndAlso blend)   ' 0x14151EC1A..C7D
        If s.IsEffect Then
            r.InPrepass = decalPasaFiltro AndAlso EffectTechniqueBit26(s)                ' 0x14152A0CB..A102 (slot 0x2B = the bit-26 condition)
            ' Palette alpha with the test needs PS 0x2000a083; 0x2000a082 (no vertex colour) is not in the cache (10.1).
            If r.InPrepass AndAlso test AndAlso s.PaletteAlpha AndAlso Not s.HasVertexColors Then r.InPrepass = False
        Else
            ' Slot 0x2B's own filters (LightingSlot2BPasses, shared with the shadow map) and the prepass registration filter.
            r.InPrepass = LightingSlot2BPasses(s) AndAlso decalPasaFiltro
        End If
        ' The lighting slot 0x2B ORs bit 7 (alpha test) for the NiAlphaProperty test AND always 0x20080 for a decal
        ' (14151B606..14151B6D5; RE_SSE_LIGHTING_PREPASS_FOGTAIL 1.2): a hair decal that reaches the prepass (ZWrite + kHairTint +
        ' blend, e.g. the hairline) is alpha-tested there even when its NiAlphaProperty only blends.
        r.PrepassAlphaTest = r.InPrepass AndAlso (test OrElse (Not s.IsEffect AndAlso decal))
        If r.PrepassAlphaTest Then r.PrepassThreshold = PrepassThrG(blend, s.TestRef)   ' thrG, 0x141567EE2..F27

        ' ---- depth of the colour pass (3.2, 3.3, 11) ----
        If decal Then
            ' Decal lists draw with their default whatever the flags: list 3 = 3 (test + write), list 4 = 1 (test).
            r.DepthFunc = SseDepthFunc.LessEqual
            r.DepthWrite = r.Group = 2
        ElseIf Not s.ZTest Then
            r.DepthFunc = SseDepthFunc.Off                                           ' 0x141557D22 / 0x14154A769
        ElseIf Not s.ZWrite Then
            r.DepthFunc = SseDepthFunc.LessEqual                                     ' 0x141557CB0 / 0x14154A721
        ElseIf r.Group = 0 Then
            r.DepthFunc = SseDepthFunc.Equal                                         ' opaque batch, early-Z
        ElseIf r.Group = &H12 Then
            ' Billboards inherit the list's state: 3 for the first, 4 after another effect pass of the list
            ' (RestoreGeometry 0x141557FF1..8019). RenderAll resolves it in draw order (BillboardDepthAfterFirst).
            r.DepthFunc = SseDepthFunc.LessEqual
            r.DepthWrite = True
        Else
            r.DepthFunc = SseDepthFunc.LessEqual                                     ' list 0x10 / LOD land: 3
            r.DepthWrite = True
        End If

        ' ---- main-pass alpha test (12) ----
        Dim thrS = SubgroupThreshold(s)
        If s.IsEffect Then
            r.EffectNoAlphaTest = EffectTechniqueBit26(s)                             ' technique bit 26, 0x14152A43A..47C
            r.MainAlphaTest = Not r.EffectNoAlphaTest AndAlso (test OrElse s.NoTransparencyMultisampling)
            r.MainAlphaThreshold = thrS
        Else
            ' DO_ALPHA_TEST (bit 20): a decal without kMultiIndexSnow always (0x14151AA7F..A9A); otherwise early-Z off ∧ test,
            ' or T ∧ test (0x14151A5B4..5EA) - with early-Z, only the translucent ones.
            Dim doAlphaTest = If(decal AndAlso Not s.MultiIndexSnow, True, test AndAlso translucentLighting)
            ' Technique: the selector's later rules override the earlier (kHairTint -> 6, then kParallaxOcclusion -> 7, then
            ' kMultiTextureLandscape -> 8+; 0x14151AB5E..BC8, 0x14151AD1A..DB5). Descriptor bit 15 = kProjectedUV, or decal ∧
            ' kZBufferWrite ∧ kHairTint (0x14151AA3E..A45, 0x14151AB20..B42); in technique 6 it is DEPTH_WRITE_DECALS.
            Dim hairTechnique = LitTechniqueType(s) = 6   ' the full selector 0x14151AB5E..ADB5 (15.948 / 15.948 corpus shapes agree)
            Dim bit15 = s.ProjectedUV OrElse (decal AndAlso s.ZWrite AndAlso s.HairSoftLighting)
            r.HairDepthWriteDecal = hairTechnique AndAlso bit15
            ' No Hair + bit 15 PS without bit 20 is in the cache: SetupTechnique fails and the pass is skipped (13.2).
            ' A refraction shape keeps its reason: its refraction pass is drawn whatever its colour technique would be.
            If r.HairDepthWriteDecal AndAlso Not doAlphaTest Then
                r.Drawn = False : r.List = SseList.None
                If r.NoPass = SseNoPass.None Then r.NoPass = SseNoPass.HairDepthWriteDecalWithoutAlphaTest
            End If
            r.MainAlphaTest = doAlphaTest
            r.MainAlphaThreshold = thrS
            r.TailZ = If(r.Group = 1, 1.0F, 0.0F)
        End If
        ' No colour pass: no colour-pass depth state.
        If Not r.Drawn Then r.DepthFunc = SseDepthFunc.Off : r.DepthWrite = False
        Return r
    End Function

    ''' <summary>What the opaque pass writes to the SAO normals target (o2 of the opaque MRT, RE_SAO_BOTH 11-12).</summary>
    Friend Enum SseAoNormal
        None
        ''' <summary>Lighting: the normal-mapped view normal (PS 4255). Effect with technique bit 26: the interpolated vertex normal (VS 420).</summary>
        Normal
        ''' <summary>Effect without bit 26 outside the decal lists: its colour (o0), 1673 PS (RE_SAO_BOTH 8.2).</summary>
        Colour
    End Enum

    ''' <summary>RT2 write state of the opaque finish, in draw order (RE_SAO_BOTH 11): the list opens with its mode (opaque batch 1 = on,
    ''' list 3 0xA = on, list 4 0xB = off, list 0xD 0xB = off; 0x14151F18F / F212 / F239); a lighting decal of group 3 with ZWrite writes
    ''' during its draw (SetupGeometry 0x14154969D, Restore 0x14154A996); an effect decal without bit 26 does not write and its Restore
    ''' leaves the list ON (0x141557DE7 / 0x141558043).</summary>
    Friend Function AoNormalWrite(listOn As Boolean, s As SseShapeInputs, p As SsePass, ByRef listOnAfter As Boolean) As Boolean
        listOnAfter = listOn
        If p.List = SseList.Billboard Then Return False
        Dim decal = p.List = SseList.DecalOpaque OrElse p.List = SseList.DecalTranslucent
        If s.IsEffect AndAlso decal AndAlso Not p.EffectNoAlphaTest Then listOnAfter = True : Return False
        If Not s.IsEffect AndAlso p.List = SseList.DecalTranslucent AndAlso s.ZWrite Then Return True
        Return listOn
    End Function

    ''' <summary>Which VS law produces the o2 normal of a bit-26 effect (RE_SAO_BOTH 12.6.3, 12.7): FALLOFF (VS 431/440/441) = world
    ''' normal; SKINNED (VS 425) = raw bind-pose normal; else (VS 420) = view normal. MEMBRANE never reaches bit 26 at runtime (12.7).</summary>
    Friend Enum SseAoEffectNormal
        None
        View
        RawSkinned
        World
    End Enum

    Friend Function AoEffectNormalClass(s As SseShapeInputs) As SseAoEffectNormal
        If Not s.IsEffect OrElse Not EffectTechniqueBit26(s) Then Return SseAoEffectNormal.None
        If s.Falloff Then Return SseAoEffectNormal.World
        If s.Skinned Then Return SseAoEffectNormal.RawSkinned
        Return SseAoEffectNormal.View
    End Function

    ''' <summary>[0x1420D6CE8] = bEnableImprovedSnow:Display, the byte both snow tests of the lighting descriptor read
    ''' (0x14151AA36): its .data value, 1. It is the value (+8) of the Setting 0x1420D6CE0 (vtable 0x1417EFB70
    ''' SettingT&lt;INIPrefSettingCollection&gt;, name 0x141B3E138), registered by the static initializer 0x1401004E0 in the
    ''' collection 0x1401B5310; its one writer is ReadSetting 0x140FD0E10 (type 'b', 0x140FC25D0; 0x140FD1128..114B), which the
    ''' INI files feed. User decision 3-oct-2026: app values / exe defaults, not the installed INI - so the .data value. The 6
    ''' RIP references of the byte are reads (Tools/re-docs/RE_SSE_PASS_GROUPS_DEPTH_2026-10-03.md 8.2).</summary>
    Friend Const ImprovedSnowExeDefault As Boolean = True

    ''' <summary>cb1[13] SnowRimLightParameters of the snow technique (SetupMaterial 0x141549267..2B7): x = fSnowRimLightIntensity
    ''' (.data 0x1420D8DF0 = 0.3), y = fSnowGeometrySpecPower (0x1420D8E08 = 3), z = fSnowNormalSpecPower (0x1420D8E20 = 2), w = 1.0
    ''' (0x1415488E7, [0x141B5BE28]) zeroed when bEnableSnowRimLighting [0x1420D8E38] is 0 (.data 1). Exe defaults, not the INI.</summary>
    Friend ReadOnly SnowRimLightParameters As New OpenTK.Mathematics.Vector4(0.3F, 3.0F, 2.0F, 1.0F)

    ''' <summary>The snow terms of the SAO composite (PS 16004, constants 0x141542DD9..FC9): EyePosition.w = [0x1420D6CE8]
    ''' (ImprovedSnowExeDefault) and SparklesParameters3.x = bDeactivateAOOnSnow (.data 0x1420D8940 = 1).</summary>
    Friend Const DeactivateAoOnSnowExeDefault As Single = 1.0F

    ''' <summary>The lighting descriptor's SNOW technique (`or edx, 0x200200` at 0x14151AAF0: bit 21 SNOW and bit 9 SPECULAR):
    ''' [0x1420D6CE8] (<see cref="ImprovedSnowExeDefault"/>) set and either test: (1) 0x14151AA9E..AACF: (flags and
    ''' 0x9000400600000000) = bit 60 (SLSF2 28 with none of the LOD bits 33 / 34 / 46 / 63) and not bit 61 (SLSF2 29); (2)
    ''' 0x14151AAD1..AEE: SLSF1 30 with none of the LOD bits (mask 0x8000400600000000). <paramref name="improvedSnow"/> =
    ''' ImprovedSnowExeDefault in the draw; a gate fixes it. Measured over sse_lit_shapes.pkl: bit 9 = Specular or
    ''' Multi_Index_Snow or this, on all 89.278 drawn lighting shapes (31 snow).</summary>
    Friend Function SnowTechnique(s As SseShapeInputs, improvedSnow As Boolean) As Boolean
        If s.LodLandscape OrElse s.LodObjects OrElse s.NoLodLandBlend OrElse s.HdLodObjects Then Return False
        If Not ((s.Slsf2Bit28 AndAlso Not s.TreeAnim) OrElse s.Slsf1Bit30) Then Return False
        Return improvedSnow
    End Function

    ''' <summary>Lighting technique bit 9 SPECULAR: SLSF1 Specular or SLSF2 Multi_Index_Snow (mask 0x20000000001 of the 64-bit
    ''' flags, 0x14151A9A2..A9BB), or the snow technique (<see cref="SnowTechnique"/>, 0x14151AAF0). It picks the two-stage
    ''' output tail (SseLitTailSource.sseLitOutput) and the specular selector cb2[7].w of the SAO normals target.
    ''' <paramref name="improvedSnow"/> = ImprovedSnowExeDefault in the draw; a gate fixes it.
    ''' <para>This is the whole law of the two-stage tail for what the engine draws. The shader cache holds two-stage PS for
    ''' bit 9, bit 17 AMBIENT_SPECULAR and technique 14 MultiIndexSnow (5208 of the 6924, the 1716 others one stage:
    ''' D:\WinTmp\scopeverify\re2\c5v3\ps_tail_census.py), but bit 17 never reaches a BSLighting technique (the descriptor
    ''' 0x14151A932..AE58 sets bits 0-16 and 18-23, never 17; SetupTechnique 0x141547D20 only clears type bits) and technique
    ''' 14 needs SLSF2 9 (0x14151AD8E..ADA7), which sets bit 9 (Tools/re-docs/RE_SSE_PASS_GROUPS_DEPTH_2026-10-03.md 8.1).</para></summary>
    Friend Function SpecularTechnique(s As SseShapeInputs, improvedSnow As Boolean) As Boolean
        Return s.Specular OrElse s.MultiIndexSnow OrElse SnowTechnique(s, improvedSnow)
    End Function

    ''' <summary>THE LIGHTING TECHNIQUE TYPE the descriptor builds (bits 24-29), the selector 0x14151AB5E..0x14151ADB5 in its order (a
    ''' later rule overrides an earlier one). The bypass before it ([0x1420D9668] != 0 -> type 0, 0x14151AB52..AB58) is off: the byte is
    ''' 0 in .data and its writer is not traced. SetupTechnique 0x141547D2C..D71 then rewrites only 7 (parallax occlusion off) and 0x12,
    ''' never 3 or 11. The landscape branch (bit 14) walks the textures (0x14151ABD1..AD16) for descriptor bit 21, not the type. An
    ''' effect (BSEffectShader, its own descriptor 0x14152A2B0) has no lighting type: -1, so no caller tests IsEffect itself (rev-09).
    ''' The one selector of the type: Classify (Hair 6), the eye (16), TREE_ANIM (12), Parallax (3), MultiLayerParallax (11).</summary>
    Friend Function LitTechniqueType(s As SseShapeInputs) As Integer
        If s.IsEffect Then Return -1
        Dim t = If(s.EnvMap, 1, 0)                                      ' 0x14151AB5E..AB65 (flag 7)
        If s.GlowMap Then t = 2                                         ' 0x14151AB68..AB75 (bit 38)
        If s.Parallax AndAlso Not s.ParallaxOcclusion Then t = 3        ' 0x14151AB79..AB8C ((f & 0x10000800) = 0x800)
        If s.FaceGenDetail Then t = 4                                   ' 0x14151AB8F..AB99 (bit 10)
        If s.FaceGenRgbTint Then t = 5                                  ' 0x14151AB9C..ABA6 (bit 21)
        If s.HairSoftLighting Then t = 6                                ' 0x14151ABA9..ABB3 (bit 18)
        If s.ParallaxOcclusion Then t = 7                               ' 0x14151ABB6..ABC0 (bit 28)
        If s.MultiTextureLandscape Then t = 8                           ' 0x14151ABC3..ABCE (bit 14)
        If s.NoLodLandBlend Then t = &H13                               ' 0x14151AD1A..AD2C (bit 46)
        If s.LodLandscape Then t = &H12                                 ' 0x14151AD2F..AD41 (bit 33)
        If s.LodObjects Then t = &HD                                    ' 0x14151AD44..AD56 (bit 34)
        If s.HdLodObjects Then t = &HF                                  ' 0x14151AD59..AD61 (bit 63)
        If s.MultiLayerParallax Then t = &HB                            ' 0x14151AD64..AD76 (bit 56)
        If s.TreeAnim Then t = &HC                                      ' 0x14151AD79..AD8B (bit 61)
        If s.MultiIndexSnow AndAlso s.ProjectedUV Then t = &HE          ' 0x14151AD8E..ADA7 (bits 41 and 23)
        If s.Eye Then t = &H10                                          ' 0x14151ADAB..ADB5 (bit 17)
        Return t
    End Function

    ''' <summary>The lighting PS of technique type <paramref name="litType"/> works in WORLD space whatever SKINNED says: SetupGeometry
    ''' 0x141549550's case for types 1 (Envmap), 11 (MultiLayerParallax) and 16 (Eye) (jump table 0x14154A8A8 -> 0x141549705) clears
    ''' the model-space flag r15d = !SKINNED (xor r15d, r15d at 0x14154971E) that the directional light (0x141549B7D), the point lights
    ''' (0x141549BCB) and the camera cb2[6] (0x14154A640) test; their rigid VS hand the PS cb2[6] - World.pos and World.TBN.</summary>
    Friend Function LitPsWorldSpace(litType As Integer) As Boolean
        Return litType = 1 OrElse litType = &HB OrElse litType = &H10
    End Function

    ''' <summary>SetupGeometry writes the VS camera cb2[6] (the PS's view vector v6 = cb2[6] - pos) only when [rbp+0x1E0] is set
    ''' (0x14154A61F..626): the type-1 / 11 / 16 case (0x141549725) or SPECULAR (0x14154A09C..A0AB) or technique &amp; 0x21C00 (bits
    ''' 10 soft, 11 rim, 12 back light; bit 17 never reaches a lighting descriptor) (0x14154A0C8..A0E5). Otherwise the PS reads what
    ''' the buffer held (S-P: shaderspheres02, user decision 6-oct-2026: notice).</summary>
    Friend Function LitViewVectorWritten(s As SseShapeInputs) As Boolean
        Return LitPsWorldSpace(LitTechniqueType(s)) OrElse SpecularTechnique(s, ImprovedSnowExeDefault) OrElse
               s.SoftLighting OrElse s.RimLighting OrElse s.BackLighting
    End Function

    ''' <summary>Effect technique bit 14 BLOOD: property flag bit 49 (SLSF2 17 Weapon_Blood; 0x14152A3A3..3B6 and bts 0xE at
    ''' 0x14152A588..592). PS 0x4842 / 0x4843 (re2/snowblood validate_blood.py).</summary>
    Friend Function EffectBloodTechnique(s As SseShapeInputs) As Boolean
        Return s.IsEffect AndAlso s.WeaponBlood
    End Function

    ''' <summary>Lighting technique type 12 TREE_ANIM: LitTechniqueType (the full selector 0x14151AB5E..ADB5) gives &amp;HC - flag bit 61
    ''' (0x14151AD79..AD8B), not overridden by 41 + 23 (0xE) or 17 (0x10); an effect gives -1. The NIF shader type does not enter (the
    ''' 500 corpus shapes of type 12 have NIF type 0: tree_type_census_out.txt).</summary>
    Friend Function TreeAnimTechnique(s As SseShapeInputs) As Boolean
        Return LitTechniqueType(s) = &HC
    End Function

    ''' <summary>THE REST POSE OF THE TREE_ANIM VS: o2 of rec 3993 (and its family; the utility depth / shadow VS 11237..11282 the
    ''' same) = the raw local vertex plus its leaf offset, with the leaf constants of a draw without BSLeafAnimNode (0x141549885..9E1:
    ''' cb2[8] = (0, [[0x1420D68B0]+0x304], 1, 1), cb2[9] = (0, 0); user decision 6-oct-2026): phase 0, P = x + y + z + 0.5 of the RAW
    ''' vertex (local, before skinning), f = 2 frac(P) - 1, s = |f|^2 (3 - 2|f|), offset = ((0.1 s) + s) (vc.a * 1) * n, n the raw
    ''' UNORM8 normal b / 255 * 2 - 1, NOT normalized. Float32 in the bytecode's order; ParityGate sse-leaf-pose holds it bit for bit
    ''' against dxbc_eval of rec 3993 on real vertices.</summary>
    Friend Function LeafRestPosition(p As OpenTK.Mathematics.Vector3, rawNormal As OpenTK.Mathematics.Vector3, vertexAlpha As Single) As OpenTK.Mathematics.Vector3
        Dim t As Single = ((p.X + p.Y) + p.Z) + 0.5F                     ' dp3 v0, (1, 1, 1); + phase 0; + 0.5
        t = t - CSng(Math.Floor(t))                                       ' frc
        t = t * 2.0F + -1.0F
        Dim a As Single = Math.Abs(t)
        Dim s As Single = (a * a) * (-(a * 2.0F) + 3.0F)
        Dim m As Single = ((s * 0.1F) + s) * (vertexAlpha * 1.0F)         ' 0.1 = token 0x3DCCCCCD; cb2[8].z = 1
        Return New OpenTK.Mathematics.Vector3(m * rawNormal.X + p.X, m * rawNormal.Y + p.Y, m * rawNormal.Z + p.Z)   ' add r1.xyz, r1.xzwx, v0
    End Function

    ''' <summary>Effect technique bit 3 (0x14152A2EA..312): SLSF1 bit 1 (kSkinned) ∧ vertexDesc AND 0x1004000000000000 (bit 50 = VF_SKINNED &lt;&lt; 44, or bit 60;
    ''' RE_SAO_BOTH 12.7).</summary>
    Friend Function EffectSkinnedBit(slsf1 As UInteger, vertexDesc As ULong) As Boolean
        Return ((slsf1 >> 1) And 1UI) <> 0UI AndAlso (vertexDesc And &H1004000000000000UL) <> 0UL
    End Function

    ''' <summary>A billboard whose own flags leave the list's state (ZTest ∧ ZWrite without blend) after another effect pass
    ''' of the same list: RestoreGeometry left mode 4 (EQUAL); the first one of the list gets 3.</summary>
    Friend Function BillboardInheritsEqual(s As SseShapeInputs, afterAnotherEffectPass As Boolean) As Boolean
        Return afterAnotherEffectPass AndAlso s.ZTest AndAlso s.ZWrite AndAlso Not (s.AlphaPropertyPresent AndAlso s.Blend)
    End Function
End Module
