Imports OpenTK.Mathematics

''' <summary>THE CONSTANTS OF THE FALLOUT 4 G-BUFFER PS, BY NAME (BSDFPrePassShaderPixelConstants::GetString 0x1422733F0, jump table
''' 0x1422734C0), as BSDFPrePassShader::SetupGeometry 0x142205540 / SetupMaterial 0x142204140 write them. The register each name
''' occupies is per record; the GLSL reads them by name. Sources: scratchpad opaque-fo4/transcripcion-gbuffer/notas.md 1.4 and 4;
''' Tools/re-docs/RE_FO4_DEFERRED_FRAME_2026-10-03.md 7, 8, 14, 18; RE_FO4_WORLD_ENVMAP_2026-10-03.md 2.3, 11, 12.</summary>
Friend Module Fo4GBufferConstants

    ''' <summary>SpecularParam (constant 1), 0x14220762B..0x142207693: x = Smoothness (0.22 fLODLandSpecPower with technique bit 9),
    ''' y = SpecularMult only with property bit 0 (else 0; DF 7), z = fBackLightPower (mat+0xB4), w = SSS rolloff (mat+0xAC).</summary>
    Friend Function SpecularParam(technique As UInteger, flags As ULong, smoothness As Single, specularMult As Single,
                                  backLightPower As Single, subsurfaceRolloff As Single) As Vector4
        Dim lodLand = (technique And (1UI << 9)) <> 0UI
        Return New Vector4(If(lodLand, 0.22F, smoothness),
                           If(lodLand, 0.06F, If((flags And 1UL) <> 0UL, specularMult, 0.0F)),
                           backLightPower, subsurfaceRolloff)
    End Function

    ''' <summary>SetupGeometry writes AdditionalAlphaMaskRef and binds t15 when technique AND 0x1000020 = 0x1000000 (bit 24 without
    ''' bit 5, 0x14220714B..0x142207150).</summary>
    Friend Function HasAdditionalAlphaMask(technique As UInteger) As Boolean
        Return (technique And &H1000020UI) = &H1000000UI
    End Function

    ''' <summary>AdditionalAlphaMaskRef (constant 17) of a pass without effectData (the preview never has one): .x is not written
    ''' (0x1422071E4 only under effectData; the buffer is WRITE_DISCARD) and the PS reads it only when .w is not 0, so 0 here;
    ''' .y = -1 with property flag 58 (F4SPF2 bit 26), +1 without (0x1422072A7..0x1422072B1: xmm13 -1 from 0x142205B4B /
    ''' 0x142206A01 / 0x142206A99, xmm15 +1 from 0x1422058B6); .z = the pass alpha [prop+0x28] = material alpha * fade (1) *
    ''' prop+0x64 (1) (0x14217A47C..0x14217A4AB), because [pass+0x4E] is 3 &gt;= 0 (BSRenderPass ctor 0x14221D35A; 0x142207292);
    ''' .w = 0 without effectData (0x1422071B7).</summary>
    Friend Function AdditionalAlphaMaskRef(flags As ULong, materialAlpha As Single) As Vector4
        Return New Vector4(0.0F, If((flags And (1UL << 58)) <> 0UL, -1.0F, 1.0F), materialAlpha, 0.0F)
    End Function

    ''' <summary>EmissiveColor.xyz (constant 2), 0x142207432..0x1422074D6: pow(colour * mult, 2.2) per channel (DF 14; the reader puts
    ''' black when bEmitEnabled = 0, DF 18.1). .w: the alpha-test / blend-discard reference, written when the technique has ALPHA_TEST
    ''' (bit 8) or the NiAlphaProperty blends (0x142207340..0x14220737D): Fo4RenderPassLaw.MainPassAlphaThreshold(ref) (the one owner
    ''' of ref * 0x3B808081 + 0x3B80802C, + 0x3C008056 for ref 4, 0x142207354..0x142207371); otherwise 0.</summary>
    Friend Function EmissiveColor(emitColor As Vector3, emitMult As Single, technique As UInteger, alphaBlend As Boolean, alphaRef As Byte) As Vector4
        Dim c = emitColor * emitMult
        Dim p = New Vector3(CSng(Math.Pow(c.X, 2.2)), CSng(Math.Pow(c.Y, 2.2)), CSng(Math.Pow(c.Z, 2.2)))
        Dim w = 0.0F
        If (technique And (1UI << 8)) <> 0UI OrElse alphaBlend Then
            w = Fo4RenderPassLaw.MainPassAlphaThreshold(alphaRef)
        End If
        Return New Vector4(p, w)
    End Function

    ''' <summary>AlphaScale (constant 3), only with technique bit 15 (0x14220738B): x = the pass alpha [prop+0x28] = material alpha *
    ''' fade (1) * prop+0x64 (1) (0x14217A47C..0x14217A4AB); y = 1 when the NiAlphaProperty blends (0x1422073BB..0x1422073D6).</summary>
    Friend Function AlphaScale(technique As UInteger, materialAlpha As Single, alphaBlend As Boolean) As Vector4
        If (technique And (1UI << 15)) = 0UI Then Return Vector4.Zero
        Return New Vector4(materialAlpha, If(alphaBlend, 1.0F, 0.0F), 0.0F, 0.0F)
    End Function

    ''' <summary>TintColor (constant 4), 0x142206AAB..0x142206BB4: SKIN_TINT (technique bit 18) with the SkinTint material class:
    ''' (pow(tone, 2.2), tone alpha); GRADIENT_REMAP (bit 26): x = grayscaleToPaletteScale (mat+0xB8); otherwise 0.</summary>
    Friend Function TintColor(technique As UInteger, toneLinear As Vector3, toneAlpha As Single, hasTone As Boolean, paletteScale As Single) As Vector4
        If (technique And (1UI << 18)) <> 0UI Then Return If(hasTone, New Vector4(toneLinear, toneAlpha), Vector4.Zero)
        If (technique And (1UI << 26)) <> 0UI Then Return New Vector4(paletteScale, 0.0F, 0.0F, 0.0F)
        Return Vector4.Zero
    End Function

    ''' <summary>SpecularParam2 (constant 7), 0x1422076AA..0x1422076F2: (WetnessControl_SpecPowerScale mat+0x98, WetnessControl_SpecScale
    ''' mat+0x94), or (-1, -1) with technique bit 5 (bit 28 without instance object never happens in the preview, ENV 12.1).</summary>
    Friend Function SpecularParam2(technique As UInteger, wetSpecPowerScale As Single, wetSpecScale As Single) As Vector4
        If (technique And &H20UI) <> 0UI Then Return New Vector4(-1.0F, -1.0F, 0.0F, 0.0F)
        Return New Vector4(wetSpecPowerScale, wetSpecScale, 0.0F, 0.0F)
    End Function

    ''' <summary>WetnessControl_Spec (constant 8), 0x142207705..0x142207805: (mat+0x94 SpecScale, +0x98 SpecPowerScale, +0x9C SpecMin,
    ''' +0xA8 Metalness); an Envmap-class material without technique bit 5 replaces .x/.y with the bytes mat+0xD4 / +0xD5
    ''' (bScreenSpaceReflections, bWetnessControl_ScreenSpaceReflections; ENV 8.2).</summary>
    Friend Function WetnessControlSpec(technique As UInteger, envmapClass As Boolean, wetSpecScale As Single, wetSpecPowerScale As Single,
                                       wetSpecMin As Single, wetMetalness As Single, ssr As Boolean, wetSsr As Boolean) As Vector4
        If envmapClass AndAlso (technique And &H20UI) = 0UI Then
            Return New Vector4(If(ssr, 1.0F, 0.0F), If(wetSsr, 1.0F, 0.0F), wetSpecMin, wetMetalness)
        End If
        Return New Vector4(wetSpecScale, wetSpecPowerScale, wetSpecMin, wetMetalness)
    End Function

    ''' <summary>CubeMapIdxR_EnvmapScaleG (constant 20), 0x14220593F..0x1422059A9 (ENV 2.3): x = slice + 1 of the EnvMapArray (0 for 0xFF
    ''' or without bit 7), y = property bit 7 ? 1 : 0, z = bit 7 ? mat+0xD0 read as a float (Fo4EngineLightingMaterial.MaterialD0: the
    ''' Envmap class' scale; the Eye class' left eye reflection centre x) : 0, w = mat+0xA0 (WetnessControl_EnvMapScale).</summary>
    Friend Function CubeMapIdxEnvmapScale(flags As ULong, slice As Integer, materialD0 As Single, wetEnvMapScale As Single) As Vector4
        Dim envBit = (flags And (1UL << 7)) <> 0UL
        Return New Vector4(If(envBit AndAlso slice >= 0, slice + 1, 0),
                           If(envBit, 1.0F, 0.0F),
                           If(envBit, materialD0, 0.0F),
                           wetEnvMapScale)
    End Function

    ''' <summary>cb2 World rows (SetupGeometry 0x142205B77..CC1; the prepass writes the same composition, 0x141828AEC..B5F): row i =
    ''' (R*scale row i, t_i) for the column-vector engine = column i of the OpenTK row-vector model; row 3 = (0, 0, 0, 1)
    ''' (0x142205B8D). ONE function for the G-buffer and the prepass (revisor-19).</summary>
    Friend Function EngineWorldRows(model As Matrix4) As Vector4()
        Return {model.Column0, model.Column1, model.Column2, New Vector4(0.0F, 0.0F, 0.0F, 1.0F)}
    End Function

    ''' <summary>cb12 rows 0..2, 8..11, 12..15, 20..23, 35 and 41..46 of the frame (writer 0x14181E1C0), revisor-26 / ola 3. R = the GL
    ''' view's rotation (LookAt without translation), P = EngineCameraLaw.Perspective (built FOR the GL view: M34 = -1, z in [0, 1]).
    ''' <list type="bullet">
    ''' <item>View (0..2): the engine VS takes o0.i = dp4(cb12[8+i], p - eye) (rec2273 L54-57) = column i of R * P (ViewProj, 8..11); its
    ''' view rows (rec2273 L69-79) are the engine view = GL view with z negated: columns 0..2 of R * diag(1, 1, -1).</item>
    ''' <item>InvView (12..15, the inverse of the view by 0x140336140, written 0x14181E442..44D): the rows of the engine view -&gt; world
    ''' rotation, row 3 = (0, 0, 0, 1) (the view is camera-relative); InvProj (20..23, the inverse of the projection +0x760,
    ''' 0x14181E532..53D) = inverse(A * P_e), A mapping z to 1.01 z - 0.01 w (the lights' notas 4.2 / D9; P_e = the projection's rows
    ''' times diag(1, 1, -1, 1)). One owner: the deferred lights read these rows too (PreviewModel.BuildFo4DeferredFrame).</item>
    ''' <item>Fog (41..46): Fo4EffectConstants.FogRows of the engine's fog-off state (user decision 13-oct-2026, Fo4EffectConstants.FogOff).</item>
    ''' </list></summary>
    Friend Function EngineFrameRows(viewGl As Matrix4, projection As Matrix4, eye As Vector3) As Fo4FrameRows
        Dim r = viewGl.ClearTranslation()
        Dim vp = r * projection
        Dim rs = r * Matrix4.CreateScale(1.0F, 1.0F, -1.0F)
        Dim w = ViewToWorldRows(viewGl)
        Dim rRow = Function(i As Integer) New Vector4(CType(w(i), Vector3), 0.0F)
        Return New Fo4FrameRows With {.View = {rs.Column0, rs.Column1, rs.Column2}, .ViewProj = {vp.Column0, vp.Column1, vp.Column2, vp.Column3},
                                      .InvView = {rRow(0), rRow(1), rRow(2), New Vector4(0.0F, 0.0F, 0.0F, 1.0F)},
                                      .InvProj = InverseProjectionRows(projection, 1.01, -0.01),
                                      .Eye = New Vector4(eye, 0.0F), .Fog = Fo4EffectConstants.FogRows(Fo4EffectConstants.FogOff()),
                                      .AppInvViewProj = vp.Inverted()}
    End Function

    ''' <summary>The engine view -&gt; world rotation's rows, in double (one owner: cb12 12..14 and the deferred ambient,
    ''' PreviewModel.BuildFo4DeferredFrame): R = inverse(GL view rotation) * diag(1, 1, -1) in the column-vector convention - OpenTK
    ''' stores the row-vector matrix, so the column-vector rotation is its transpose, inverted in general, then the columns' z negated.</summary>
    Friend Function ViewToWorldRows(viewGl As Matrix4) As Vector3d()
        Dim v3 As New Matrix3d(viewGl.M11, viewGl.M12, viewGl.M13, viewGl.M21, viewGl.M22, viewGl.M23, viewGl.M31, viewGl.M32, viewGl.M33)
        Dim rcv = Matrix3d.Transpose(v3)
        rcv.Invert()
        Dim rRow = Function(i As Integer) New Vector3d(rcv(i, 0), rcv(i, 1), -rcv(i, 2))
        Return {rRow(0), rRow(1), rRow(2)}
    End Function

    ''' <summary>The rows of inverse(P_e with its z row replaced by zScale * z + wScale * w), P_e = the GL projection's column-vector rows
    ''' times diag(1, 1, -1, 1): cb12 20..23 (1.01, -0.01) and the lights' first-person 24..27 (100, 0) (notas 4.2 / D9).</summary>
    Friend Function InverseProjectionRows(projection As Matrix4, zScale As Double, wScale As Double) As Vector4()
        Dim flipZ = Function(c As Vector4) New Vector4d(c.X, c.Y, -c.Z, c.W)
        Dim p0 = flipZ(projection.Column0), p1 = flipZ(projection.Column1), p2 = flipZ(projection.Column2), p3 = flipZ(projection.Column3)
        Dim m As New Matrix4d(p0, p1, If(wScale = 0.0, zScale * p2, zScale * p2 + wScale * p3), p3)
        m.Invert()
        Return {CType(m.Row0, Vector4), CType(m.Row1, Vector4), CType(m.Row2, Vector4), CType(m.Row3, Vector4)}
    End Function

    ''' <summary>cb1 TexcoordOffset (SetupMaterial 0x14220421F..296 / 0x142204325..39B): (offset.u, offset.v, scale.u, scale.v).</summary>
    Friend Function TexcoordOffset(offset As Vector2, scale As Vector2) As Vector4
        Return New Vector4(offset.X, offset.Y, scale.X, scale.Y)
    End Function

#If DEBUG Then
    ''' <summary>DEBUG ONLY (ola 2b rev-B12): THE one switch of the deferred frame's constant mutants. ShadowGate
    ''' --fo4-vertex-constants: the laws here ("worldview-eye", also the effects' WorldViewProj, one composition; "scalemask-bit",
    ''' "actorcollision-w", "bted-bit") and their writers' conditions in RenderableMesh.BindEngineVertexStage / EngineVao
    ''' ("worldview-cond", "scalemask-cond", "bted-cond", "swap-blend-loc"). Ola 3, ShadowGate --fo4-effect-technique-law: the flags
    ''' and the technique (Fo4EffectTechnique: "efx-mat-N" one applicator bit inverted, "efx-decal-arg3", "efx-skin", "efx-slot42-vc",
    ''' "efx-slot42-li", "efx-tech-addblend", "efx-tech-palette", "efx-tech-normals"; Fo4EffectSource.VertexShaderId "efx-vsid-mask");
    ''' --fo4-effect-constants: Fo4EffectConstants ("efx-world-eye", "efx-eyepos-skinned", "efx-falloff-order", "efx-softvs",
    ''' "efx-propcolor-w", "efx-alphatest-y", "efx-pspot", "efx-cameradata", "efx-li-soft", "efx-envscale", "efx-fog-on") and
    ''' BindEffectVertexStage ("efx-world-cond", "efx-texoff") and RenderableMesh.EffectGeometryWorld ("efx-world-node", "efx-world-appskin"). Ola 4: --fo4-shadow-law, Fo4UtilityTechnique
    ''' ("utl-shadow-clamped", "utl-shadow-alpha", "utl-cull-none", "utl-bias-zero"); --fo4-light-law, Fo4DeferredLightLaw ("dfl-sun-ini",
    ''' "dfl-no-blendsplit", "dfl-smp-gl-rows") and Fo4DeferredTargets.Ensure ("dfl-cmp-lequal"); --fo4-camz, Fo4DeferredTargets.RunCameraZ
    ''' ("camz-clipinfos", "camz-dispatch-w"); --fo4-post-law, Fo4ImageSpaceChain ("post-bloom-row6", "post-offsets-no7", "post-drp-zw1",
    ''' "post-lut-noweight"); --fo4-refraction-normals, Render.vb RenderRefractionNormalsFo4 ("utl-refr-power0", "utl-eyepos-world").
    ''' ParityGate (19-oct) fo4-lut-loader, SceneTargets.LutFromBytes ("lut-dx10-ignored", "lut-no-swap", "lut-xz-transposed",
    ''' "lut-skip-flags", "lut-skip-bitcount", "lut-skip-width", "lut-skip-height", "lut-truncated-loaded", "lut-sig-check"; LutVolume
    ''' "lut-volume-nofile"); alpha-sort-key,
    ''' EngineWorldBound.AlphaSortKey / ViewDirection ("alpha-key-assoc", "alpha-key-translation", "alpha-dir-sign").
    ''' Item 2b oracles (19-oct): ParityGate fo4-technique-bucket-law and ShadowGate --draw-order - PreviewModel.Fo4TechniqueBucketKeys
    ''' ("tbo-packet-ignored", "tbo-packet-reversed", "tbo-type-ignored"; "tbo-old-law" = the whole order before item 2b, also in
    ''' RebuildRenderBuckets and the stage), the G-buffer stage of list 0 in DrawOpaqueStage
    ''' ("tbo-lod0-stage-old"), Fo4GBufferTechnique.HasDrawPacket ("pkt-geomtype-ignored", "pkt-propctl-ignored", "pkt-alphactl-ignored",
    ''' "pkt-uv-ignored", "pkt-uv-always", "pkt-programs-ignored"); round 2: NifSceneVisibility.Fo4GeometryClassOf ("geomclass-notreg-ignored"),
    ''' Fo4RenderPassLaw.ListWalk ("lw-0xe-append"), PreviewModel.Fo4MergeList ("ovl-attach-reversed");
    ''' item 1b oracles (19-oct): ParityGate occlusion-coverage-oracle and Tools\OcclusionRaytracerGate - MaterialData.EngineColourCoverage
    ''' ("cov-fo4-colour-prepass-law", "cov-sse-alpha-one-side", "cov-fo4-va-none", "cov-drawn-ignored", "cov-blendform-diffuse",
    ''' "cov-effect-evaluated"), EngineAlphaCut.Keeps ("cut-equal-discards"), EngineCoverage.Transmittance ("cov-transmit-srcalpha") and
    ''' DrawsAfter ("cov-listorder-reversed"); the A/B instrument of the oracles' --occlusion-ab ("cov-head-threshold": HEAD's ref/255);
    ''' item 2b round 3 (Skyrim SE list order): PreviewModel.SseDecalOrder ("sse-decal-registration", "sse-decal-sublist-ignored"),
    ''' SseRenderPassLaw.ListWalk ("sse-0xd-ascending"), the SSE EngineColourCoverage ("cov-sse-orderkey-missing"); the A/B instrument of
    ''' ShadowGate --order-ab, EngineWorldBound.AlphaSortKey ("alpha-key-old": the key before item 2, with GateAlphaOldViewM43).
    ''' Item 3 oracles (19-oct): ParityGate fo4-texture-creation-law - EngineTextureCreationLaw ("tcl-e050-fourcc-first",
    ''' "tcl-e050-numeric", "tcl-e050-lum-masks", "tcl-bpp-100", "tcl-b-word", "tcl-b-mips256", "tcl-b-cube-faces", "tcl-a-mips-max",
    ''' "tcl-no-srgb-rescue", "tcl-integer-sampled", "tcl-big-accepted", "tcl-bc-mult4-off", "tcl-packed-odd-off", "tcl-mips-chain-off",
    ''' "tcl-cube-square-off", "tcl-short-off", "tcl-empty-notlaw", "tcl-lightcube-accepts-ignored", "tcl-effectcube-empty-rejected",
    ''' "tcl-tile-unread", "tcl-reader-dx10-as-header"), Fo4EnvMapArray.AcceptsHeader ("tcl-envarray-mips-gt"), EngineDefaultTextureLaw
    ''' ("dtl-rejected-loses", "dtl-rejected-white", "dtl-effect-rejected-ladder", "dtl-srvnull-alpha", "dtl-srgb-palette-raw"),
    ''' PreviewModel.SeverityOf ("ntc-rejected-warning"); fo4-texture-creation-corpus ("tcl-e050-rgb24", "tcl-a-cube-bit");
    ''' fo4-texture-creation-reader ("tcl-reader-hw-swapped"); EngineDefaultTextureLaw ("dtl-palette-rejected-bound",
    ''' "dtl-missing-palette-bound"). ShadowGate --texture-creation (19-oct): DirectXDDSLoader ("gl-21-noswizzle", "gl-rgba8view-off",
    ''' "gl-srgb-promote-all"), FilesDictionary_class.File_Location ("ba2-tile-refused"), Fo4EnvMapArray.AcceptsHeader's queue branch
    ''' ("tcl-envarray-queue-mips-gt"), and the library ones above it reuses. Round r19c (19-oct): EngineTextureCreationLaw
    ''' ("tcl-crash-unflagged", "tcl-lightcube-seed-ignored", "tcl-negcache-off"), PreviewModel.SeverityOf ("ntc-crash-warning"),
    ''' Fo4EnvMapArray.SeededBefore ("tcl-seed-self-counts").
    ''' Nothing = the laws. Read only under #If DEBUG: a
    ''' Release build has no mutant branch.</summary>
    Friend GateVertexConstantsMutant As String = Nothing

    ''' <summary>DEBUG ONLY (ShadowGate --order-ab): the M43 of the view matrix the frame sorts with (OrbitCamera.GetViewMatrix), read
    ''' by the A/B instrument "alpha-key-old" - the key before item 2, -Vector3.TransformPosition(centre, view).Z, which is
    ''' -(((c.x M13 + c.y M23) + c.z M33) + M43) in float32 (OpenTK's TransformPosition, left to right).</summary>
    Friend GateAlphaOldViewM43 As Single = 0.0F

    ''' <summary>DEBUG ONLY (user decision 19-oct-2026, "una sola entrada, dos switches"): THE entry point of the gates' mutants. Each
    ''' library keeps its own switch - this module's GateVertexConstantsMutant (GateMutantOn) and the BA2 library's Ba2GateMutants, which
    ''' cannot see this one (the dependency goes the other way) - and this sets BOTH to <paramref name="name"/> (Nothing = the laws).
    ''' ParityGate / ShadowGate --gate-mutant and ParityGate's runner of directed mutants (LutAlphaOrderGates.ConMutantes) call it.</summary>
    Friend Sub SetGateMutant(name As String)
        GateVertexConstantsMutant = name
        BSA_BA2_Library_DLL.BethesdaArchive.Core.Ba2GateMutants.Name = name
    End Sub

    ''' <summary>DEBUG ONLY (rev-B12): the mutant <paramref name="name"/> is on. The same one switch may hold several names joined by
    ''' '+' (item 5 r19f-01: "h5-clear-0+h5-t3-cleared" = HEAD's G-buffer clear and t3 together): each of them is on.</summary>
    Friend Function GateMutantOn(name As String) As Boolean
        Return BSA_BA2_Library_DLL.BethesdaArchive.Core.Ba2GateMutants.Matches(GateVertexConstantsMutant, name)   ' the one reading
    End Function

    ''' <summary>DEBUG ONLY (ShadowGate --effect-scenes, ola 3 rev-C5): the scene bounds PreviewControl.GetSceneBounds returns (the
    ''' camera's framing and the shadow map's, Render.vb), so a frame with some shapes hidden keeps the framing of the frame it is
    ''' compared with. Nothing = the visible shapes' own bounds.</summary>
    Friend GateSceneBounds As (Min As Vector3, Max As Vector3)? = Nothing

    ''' <summary>DEBUG ONLY (ShadowGate --effect-exposure, ola 3 rev-C1): the mean luminance the post reads (SceneTargets.Composite
    ''' writes it over the frame's own reduction, the rest of the post unchanged), so a frame can be shown with another frame's
    ''' exposure and contrast pivot. Nothing = the frame's own mean.</summary>
    Friend GateAvgLumOverride As Single? = Nothing

    ''' <summary>DEBUG ONLY (the texture-readiness gate, D:\WinTmp\rc\texready\propuesta.md): when set, every background texture
    ''' load (PreviewModel.Process_Textures_GL) waits on it before decoding until the gate sets it - so the frames a load spans,
    ''' and what the host does meanwhile (a second MarkDirty(Shapes), a scene swap), are the gate's to choose. A cancellation
    ''' ends the wait. Nothing = the loads run at once.</summary>
    Friend GateTextureLoadHold As Threading.ManualResetEventSlim = Nothing
#End If

    ''' <summary>WorldView (VS constant 3, cb2, 4 rows; D:\WinTmp\rc\ola2b\re.md 2.3), SetupGeometry 0x142206BC9..C86 (the pre-built
    ''' constants 0x142208330..3D8 the same) when technique AND 0x800400 = 0x800000 (0x142206BB6..BC3; bit 23 = flags bit 60
    ''' Pipboy_Screen, GetRenderPasses 0x14217A897..8B0): 0x1422094B0 builds W_rel from the geometry's world NiTransform (geom + 0x70):
    ''' rows 0..2 the rotation rows x scale, row 3 = (t - eye, 1), eye = [state + 0x1B70 + 0x6B0] (0x1422094FD..51B); 0x1406B9610
    ''' returns (W_rel * V) transposed with V = [state + 0x1B70 + 0x720..0x750], the view whose columns are cb12[0..3]
    ''' (0x14181E280..2F3); the rows written = columns 0..3 of W_rel * V (0x142206C71..C86). The app: W = <paramref name="model"/>
    ''' (EngineGeometryModel, the NiTransform World is built from), eye = <paramref name="eye"/>, V = the editor camera's engine view:
    ''' columns 0..2 = <paramref name="view"/> (EngineFrameRows' cb12 rows 0..2, no translation), column 3 = (0, 0, 0, 1). Row 3 of the
    ''' result is written by the engine and read by no VS (rec2267 / 2268 / 2432 / 2433 read cb2[8..10]). V's column 3 = (0, 0, 0, 1): the
    ''' camera's V is built by 0x14182D750 with row 3 = (0, 0, 0, 1) (0x14182D88F / 0x14182D893 zero, 0x14182D89A [+0x8C] = 1.0) and
    ''' copied to [state + 0x1B70 + 0x720..0x750] (0x14182BD34..0x14182BD62).</summary>
    Friend Function EngineWorldViewRows(model As Matrix4, view As Vector4(), eye As Vector4) As Vector4()
        Return EngineWorldTimesRows(model, {view(0), view(1), view(2), New Vector4(0.0F, 0.0F, 0.0F, 1.0F)}, eye)
    End Function

    ''' <summary>THE composition 0x1422094B0 / SG 0x142226394..411 -&gt; 0x1406B9610 of a geometry's VS constant with a frame matrix (one owner
    ''' for WorldView of the G-buffer and WorldViewProj of the effects, ola 3): W_rel = the world NiTransform with translation - eye and
    ''' row 3 w = 1, times the frame matrix whose columns are <paramref name="frameRows"/> (cb12 rows), the result transposed into the
    ''' rows (= the columns of W_rel * F).</summary>
    Friend Function EngineWorldTimesRows(model As Matrix4, frameRows As Vector4(), eye As Vector4) As Vector4()
        Dim f = frameRows
        Dim v As New Matrix4(f(0).X, f(1).X, f(2).X, f(3).X,
                             f(0).Y, f(1).Y, f(2).Y, f(3).Y,
                             f(0).Z, f(1).Z, f(2).Z, f(3).Z,
                             f(0).W, f(1).W, f(2).W, f(3).W)
        Dim wRel = model * Matrix4.CreateTranslation(-eye.Xyz)
#If DEBUG Then
        If GateMutantOn("worldview-eye") Then wRel = model
#End If
        Dim m = wRel * v
        Return {m.Column0, m.Column1, m.Column2, m.Column3}
    End Function

    ''' <summary>ScaleMask (VS constant 15, cb2, 1 row; re.md 2.1), SetupGeometry 0x142207079..0C9 (pre-built 0x142208855..8B5), only with
    ''' technique bit 7 (0x142206F59; GetRenderPasses gives bit 7 only to a BSGrassShaderProperty, RTTI 0x143E5F270 named at
    ''' 0x1401DB200): .zw = 1 (0x1422070A7 / 0x1422070AE); .xy = 1 when [prop + 0x30] AND 0x80000000000 (bit 43 = SF2.11
    ''' Grass_Uniform_Scale, the NIF's raw flags, BSShaderProperty LoadBinary 0x142168761..771), else 0 (0x1422070C9).
    ''' PARA ANALIZAR EN EL FUTURO (re.md 7, H1, ola 6): who sets bit 43 on the game's grass property (no immediate writer of
    ''' prop + 0x30 bit 43 in the exe).</summary>
    Friend Function ScaleMask(flags As ULong) As Vector4
        Dim bit = 43
#If DEBUG Then
        If GateMutantOn("scalemask-bit") Then bit = 42
#End If
        Dim xy = If((flags And (1UL << bit)) <> 0UL, 1.0F, 0.0F)
        Return New Vector4(xy, xy, 1.0F, 1.0F)
    End Function

    ''' <summary>ActorCollision (VS constant 24, cb2, 4 rows; re.md 2.2), SetupGeometry 0x1422059B6..B34 whenever the VS has the slot (no
    ''' technique test): row i = (G_i.x - eye.x, G_i.y - eye.y, G_i.z, G_i.w) of the globals 0x143E5DF00..3C (0x142205A02..B34; eye x
    ''' [state + 0x2220] = 0x1B70 + 0x6B0, y + 0x6B4). Their one writer 0x140C356D0: a row without an actor is z = 1, w = -1
    ''' (0x140C359FC..A02; all four 0x140C35B40..B86) and x / y are never written then (the .bss 0). The preview has no actors: rows
    ''' (-eye.x, -eye.y, 1, -1); the grass VS's push max(w - d, 0) is 0 (rec2183 L160..178). <paramref name="eye"/> = cb12 row 35.
    ''' PARA ANALIZAR EN EL FUTURO (re.md 7, ola 6): the rows of real actors.</summary>
    Friend Function ActorCollisionNoActors(eye As Vector4) As Vector4()
        Dim w = -1.0F
#If DEBUG Then
        If GateMutantOn("actorcollision-w") Then w = 1.0F
#End If
        Dim r = New Vector4(-eye.X, -eye.Y, 1.0F, w)
        Return {r, r, r, r}
    End Function

    ''' <summary>BoneTintIndices (VS constant 21, cb2, 4 rows of 32-bit words, GLSL uvec4; re.md 2.5), SetupGeometry with technique bit 30
    ''' (0x142205CCA..CDB): with the geometry's "BTED" extra data (name 0x142507510; 0x142207C48, lookup 0x142205D16) every integer b
    ''' sets bit (b AND 31) of word ((b &gt;&gt; 5) AND 3) (0x142205D71..D8B) and row i = (word i) x 4 (0x142205DA6..DE1); without it the
    ''' 4 rows are 0 (0x142205F95..FD2). <paramref name="bones"/> = the NIF's BTED integers (MaterialData.BtedBoneIndices), Nothing
    ''' without one. PARA ANALIZAR EN EL FUTURO (re.md 7, H2): the engine creates / removes BTED at runtime (0x1406F3300, the LooksMenu's
    ''' face region 0x140A80F4F); the preview has the NIF's only.</summary>
    Friend Function BoneTintIndices(bones As UInteger()) As UInteger()
        Dim w(3) As UInteger
        If bones IsNot Nothing Then
            Dim lowBits = 31UI
#If DEBUG Then
            If GateMutantOn("bted-bit") Then lowBits = 15UI
#End If
            For Each b In bones
                Dim i = CInt((b >> 5) And 3UI)
                w(i) = w(i) Or (1UI << CInt(b And lowBits))
            Next
        End If
        Dim r(15) As UInteger
        For k = 0 To 15 : r(k) = w(k \ 4) : Next
        Return r
    End Function
End Module

''' <summary>The cb12 rows of a frame (writer 0x14181E1C0) the transcribed programs read (Fo4GBufferConstants.EngineFrameRows): View 0..2,
''' ViewProj 8..11, InvView 12..15, InvProj 20..23, Eye 35, Fog 41..46.</summary>
Friend Structure Fo4FrameRows
    Public View As Vector4()
    Public ViewProj As Vector4()
    Public InvView As Vector4()
    Public InvProj As Vector4()
    Public Eye As Vector4
    Public Fog As Vector4()
    ''' <summary>APP (not a cb12 row): the inverse of the view-projection whose columns are ViewProj (OpenTK row-vector convention): the
    ''' D3D clip position -&gt; camera-relative world of the effect decals' base surface foot (Fo4EffectSource.AppSurfaceVertexFoot).</summary>
    Public AppInvViewProj As Matrix4
End Structure
