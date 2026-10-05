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
        ''' <summary>The vertex declaration carries NORMAL (vertexDesc bit 47 or 57).</summary>
        Public HasNormals As Boolean
        ''' <summary>BSEffect with greyscale-to-palette-alpha and its palette texture (technique bit 15).</summary>
        Public PaletteAlpha As Boolean
        ''' <summary>The vertex colour stream the VS reads (vertexDesc COLOR / kVertexColors).</summary>
        Public HasVertexColors As Boolean
        ''' <summary>Effect technique bit 3: <see cref="EffectSkinnedBit"/> of the shape.</summary>
        Public Skinned As Boolean
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

    ''' <summary>The engine's default alpha-test reference [0x1420CFCB8]: 128/255 written by the opaque finish (0x14151F156); 0 only
    ''' after a snow / rain emitter draws (BSParticleShader 0x141579E48; the preview draws none). It reaches only a pass that turns
    ''' the test on WITHOUT setting its own reference (Tools/re-docs/AUDIT_SSE_RE_2026-10-03.md S2).</summary>
    Friend Const GlobalAlphaTestRef As Single = 128.0F / 255.0F

    ''' <summary>cb11[0].x of a pass (AUDIT_SSE_RE S2, correcting RE_SSE_PASS_GROUPS 10.2 / 12, whose writer census missed
    ''' 0x141578930): a pass drawn with the alpha test that has a NiAlphaProperty sets its OWN reference right before drawing,
    ''' trunc(NiAlphaProperty.ref * prop+0x30) / 255 (0x141562270, 0x1415622C7..EB; prop+0x30 = materialAlpha * fade for lighting,
    ''' fade * baseColor.a for an effect; fade 1 here) - in the prepass, the opaque batch and the sorted list. The subgroup (0x141560D10)
    ''' turns the test on for NiAlphaProperty test or SLSF2 No_Transparency_Multisampling (bit 54); the latter without a NiAlphaProperty
    ''' keeps the default 128/255. No test: 0.</summary>
    Friend Function SubgroupThreshold(s As SseShapeInputs) As Single
        If s.AlphaPropertyPresent AndAlso s.Test Then Return CSng(Math.Truncate(s.TestRef * CDbl(s.Alpha))) / 255.0F
        If s.NoTransparencyMultisampling Then Return GlobalAlphaTestRef
        Return 0.0F
    End Function

    ''' <summary>BSEffect technique bit 26 (0x14152A43A..47C): NORMAL ∧ (no NiAlphaProperty ∨ its test) ∧ ZTest ∧ ZWrite. Its PS has no alpha test (RE_SAO_BOTH 12.6.1) and writes the vertex normal to o2 (12.6.3).</summary>
    Friend Function EffectTechniqueBit26(s As SseShapeInputs) As Boolean
        Return s.HasNormals AndAlso (Not s.AlphaPropertyPresent OrElse s.Test) AndAlso s.ZTest AndAlso s.ZWrite
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
            If s.Refraction OrElse s.TempRefraction Then
                r.Group = 4 : r.Drawn = False : r.NoPass = SseNoPass.RefractionOnly ' only the utility pass, 0x14151A6BB..77E
            ElseIf decal Then
                r.Group = If(translucentLighting, 3, 2)                              ' 0x14151A913..92A
            ElseIf translucentLighting Then
                r.Group = 1                                                          ' fade 1: 0x14151AC17..C1B
            ElseIf s.LodLandscape Then
                r.Group = 6
            Else
                r.Group = 0                                                          ' 0 / 0xF / 0xB / 7: same list rules
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
            r.InPrepass = Not (s.Refraction OrElse s.TempRefraction) AndAlso decalPasaFiltro AndAlso
                          Not alphaBelowOne AndAlso (decal OrElse Not blend)          ' 0x14151B380..B405
        End If
        ' The lighting slot 0x2B ORs bit 7 (alpha test) for the NiAlphaProperty test AND always 0x20080 for a decal
        ' (14151B606..14151B6D5; RE_SSE_LIGHTING_PREPASS_FOGTAIL 1.2): a hair decal that reaches the prepass (ZWrite + kHairTint +
        ' blend, e.g. the hairline) is alpha-tested there even when its NiAlphaProperty only blends.
        r.PrepassAlphaTest = r.InPrepass AndAlso (test OrElse (Not s.IsEffect AndAlso decal))
        If r.PrepassAlphaTest Then
            ' thrG (0x141567EC3..F27): 254/255 with blend; else ref/255 + 0.00392153, one more 1/255 when ref == 4.
            If blend Then
                r.PrepassThreshold = BitConverter.Int32BitsToSingle(&H3F7EFEFF)
            Else
                r.PrepassThreshold = s.TestRef / 255.0F + 0.00392153F + If(s.TestRef = 4, 1.0F / 255.0F, 0.0F)
            End If
        End If

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
            Dim hairTechnique = s.HairSoftLighting AndAlso Not s.ParallaxOcclusion AndAlso Not s.MultiTextureLandscape
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
