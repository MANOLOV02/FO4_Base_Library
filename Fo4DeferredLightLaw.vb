Imports OpenTK.Mathematics

''' <summary>THE DEFERRED LIGHTS AND COMPOSITE OF THE PREVIEW'S FALLOUT 4 FRAME (ola 4; D:\WinTmp\rc\ola4\re.md 1.3, 1.4, 2.3): which
''' transcribed b10 / b11 technique each pass draws with the exe's Settings defaults (.data, never an INI: user rule), and the constants
''' their writers write - BSDFLightShader::SetupGeometry 0x142246C20, BSDFCompositeShader SetupTechnique 0x142216AC0 / SetupGeometry
''' 0x1422174F0 - with the rig as the world: every rig light is a directional light drawn with the sun's program and its own shadow map
''' (decision of the PLAN, ola 4: the rig gives the lights, their directions, intensities and the map's area; the game the
''' computation; no positional light).</summary>
Friend Module Fo4DeferredLightLaw

    ''' <summary>The sun's technique (0x1421F5E71..638A) with the .data Settings: [0x142F97B44] (.data 1) -&gt; 0x202 | the filter
    ''' 0x142271530(min(uiOrthoShadowFilter, 5)) = 1 &lt;&lt; (3 + 18) = 0x200000 FILTER_POISSON (uiOrthoShadowFilter .data 3, 0x142F99450;
    ''' the min kept by the ctor at [0x142F99428], 0x142246830..852); iDirShadowSplits .data 2 (0x142F985B8): no DIRSPLITS=3 (bit 27,
    ''' 0x1421F5E92..E9B); BLENDSPLIT (bit 24) when [0x143E71D94] = max(fBlendSplitDirShadow, 0) &gt; 0 (fBlendSplitDirShadow .data 100,
    ''' 0x142F97B68; 0x142181A8D / AE7; 0x1421F6373..638A). Without shadow 0x201. AMBIENT (bit 17) fused when there is no tiled lighting
    ''' (at least iTiledLightingMinLights lights), the list [0x143E62198] is empty ([0x143E621A8] = 0) and [ssn + 0x150] = 0
    ''' (0x1421F5E9F..ECB): the preview runs none of those. Records: 0x01200202 rec3227, 0x01220202 rec3232, 0x201 rec3321, 0x20201
    ''' rec3109.</summary>
    Friend Function SunTechnique(shadow As Boolean, ambient As Boolean) As UInteger
        Dim t As UInteger = If(shadow, &H200000UI Or &H202UI Or &H1000000UI, &H201UI)
        If ambient Then t = t Or &H20000UI
#If DEBUG Then
        ' ShadowGate --fo4-light-law's mutants (Fo4GBufferConstants.GateMutantOn, the one switch).
        If Fo4GBufferConstants.GateMutantOn("dfl-sun-ini") Then t = If(shadow, &H8200202UI, t)          ' the INI permutation (rec3133)
        If Fo4GBufferConstants.GateMutantOn("dfl-no-blendsplit") AndAlso shadow Then t = t And Not &H1000000UI
#End If
        Return t
    End Function

    ''' <summary>The ambient alone (0x20000 rec3107, 0x1421F739E..7470): drawn when no light pass carries it fused.</summary>
    Friend Const AmbientTechnique As UInteger = &H20000UI

    ''' <summary>The composite's techniques (0x1421F82A0; RE_FO4_DEFERRED_FRAME 4.1, RE_SAO_BOTH 5.10): pass 1 0x88 (rec3432) into RT 2;
    ''' pass 2 (group 0xA ? 0x20 : 0x60) | 0x800 SSLR | 0x100 SSSSS | 0x200 APPLY_AO with the .data Settings (bScreenSpaceReflections 1
    ''' 0x142F97210, bScreenSpaceSubsurfaceScattering 1 0x142F97228, bSAOEnable 1 0x142F986D0): 0xB60 (rec3495); group 0xA is set only by
    ''' the interface forward 0x14217AEA0 (re.md H-4). With the post off (user rule 3-oct-2026: what is image space goes only with "Apply
    ''' post process") neither the SAO nor the SSLR chain runs: 0x160 (rec3505).</summary>
    Friend Const CompositePass1 As UInteger = &H88UI
    Friend Function CompositePass2(postOn As Boolean) As UInteger
        Return If(postOn, &HB60UI, &H160UI)
    End Function

    ''' <summary>The techniques the frame draws, built at the programs' start.</summary>
    Friend ReadOnly AppLightTechniques As UInteger() = {SunTechnique(True, True), SunTechnique(True, False), SunTechnique(False, True),
                                                        SunTechnique(False, False), AmbientTechnique}
    Friend ReadOnly AppCompositeTechniques As UInteger() = {CompositePass1, CompositePass2(True), CompositePass2(False)}

    ''' <summary>The rows 3k .. 3k + 2 of ShadowMapProj (cb2 name 9) for cascade k of the BLENDSPLIT writer (0x142247E3E..FD3): the
    ''' cascade matrix (vfunc +0x20 of the light, BSShadowDirectionalLight 0x14224FB30) with its translation moved to the eye
    ''' ([state + 0x6B0]: row 3 + e.x row 0 + e.y row 1 + e.z row 2) and composed by 0x1406B9610 with the camera's view rows (+0x720..0x750,
    ''' transposed at 0x142247ABE..B0E): the rows that take the engine-view position the PS rebuilds (cb12 20..23) to (u, v, depth) of the
    ''' map. The rig's map is the cascade: world -&gt; the light's view and projection (ShadowMapMath.Fit, EngineCameraLaw.Orthographic: depth
    ''' [0, 1]) -&gt; texture rows in D3D order (the map is drawn under UPPER_LEFT: u = (x + 1) / 2, v = (1 - y) / 2; re.md H-3, the
    ''' convention of the reader rec3227 L1080..1086). In double, as the inverse of the view (Fo4GBufferConstants.ViewToWorldRows).</summary>
    Friend Function ShadowMapProjRows(lightViewProj As Matrix4, viewGl As Matrix4, eye As Vector3) As Vector4()
        ' engine view -> world: p_world = eye + R p_view, R = the rows of ViewToWorldRows (engine view = GL view with z negated)
        Dim r = Fo4GBufferConstants.ViewToWorldRows(viewGl)
        Dim m = New Matrix4d(lightViewProj.M11, lightViewProj.M12, lightViewProj.M13, lightViewProj.M14,
                             lightViewProj.M21, lightViewProj.M22, lightViewProj.M23, lightViewProj.M24,
                             lightViewProj.M31, lightViewProj.M32, lightViewProj.M33, lightViewProj.M34,
                             lightViewProj.M41, lightViewProj.M42, lightViewProj.M43, lightViewProj.M44)
        ' OpenTK row-vector: clip = (p_world, 1) * m; the column c of m is clip component c as a function of p_world.
        Dim rows(2) As Vector4
        For k = 0 To 2
            ' clip component: x (k = 0), y (k = 1), z (k = 2); u = 0.5 x + 0.5 w, v = 0.5 w - 0.5 y, depth = z (an orthographic w = 1)
            Dim col As Vector4d
            Select Case k
                Case 0 : col = 0.5 * m.Column0 + 0.5 * m.Column3
                Case 1 : col = 0.5 * m.Column3 - 0.5 * m.Column1
                Case Else : col = m.Column2
            End Select
            ' as a function of p_view: col.xyz . (eye + R p_view) + col.w
            Dim a = New Vector3d(col.X, col.Y, col.Z)
            Dim px = a.X * r(0).X + a.Y * r(1).X + a.Z * r(2).X
            Dim py = a.X * r(0).Y + a.Y * r(1).Y + a.Z * r(2).Y
            Dim pz = a.X * r(0).Z + a.Y * r(1).Z + a.Z * r(2).Z
            Dim pw = a.X * eye.X + a.Y * eye.Y + a.Z * eye.Z + col.W
            rows(k) = New Vector4(CSng(px), CSng(py), CSng(pz), CSng(pw))
        Next
#If DEBUG Then
        If Fo4GBufferConstants.GateMutantOn("dfl-smp-gl-rows") Then rows(1) = -rows(1) + New Vector4(0, 0, 0, 2.0F * rows(1).W)
#End If
        Return rows
    End Function

    ''' <summary>ShadowWorldScale (cb2 name 11) of a cascade: (r - l, t - b, near, far) of its camera's frustum (+0x160..0x174;
    ''' 0x142247C0E..C66: subss +0x164 - +0x160 and +0x168 - +0x16C): the rig map's orthographic box, the float32 differences of its
    ''' frustum (ShadowMapMath.Fit), not 2 R.</summary>
    Friend Function ShadowWorldScale(fit As ShadowMapMath.LightFit) As Vector4
        Return New Vector4(fit.FrustumRight - fit.FrustumLeft, fit.FrustumTop - fit.FrustumBottom, fit.ZNear, fit.ZFar)
    End Function

    ''' <summary>ShadowSampleParam (cb2 name 10): .zw = 1 / (width, height) of the cascade's depth target [cascade + 0x50] (the render
    ''' target table [0x142F4D750] + 0xC80 / 0xC84; 0x142247BAB..BBB). .x / .y are read by the PCSS filters only (0x1422479C8..A54), which
    ''' the .data filter (POISSON) does not select: not written here (their source [light + 0x1D0] is a game light's).
    ''' <para>APP, THE RIG'S SOFTNESS (user decision 14-oct-2026, rev-E08: a design entry over the game's filter): .z is read by the
    ''' sun's PS only as the radius of its 16 Poisson taps (rec3227 L1083 / L1118, rec3232 L1098 / L1133: r = ShadowSampleParam.z x 3,
    ''' tap k = centre + 2 r (icb[k] - 0.5), icb in [0, 1]: +-3 texels), so .z x <paramref name="radiusScale"/> scales that radius and
    ''' nothing else (.w is read by no drawn PS). radiusScale = RigShadowRadiusScale: 1 for the default rig, where .z is the game's
    ''' 1 / size bit for bit (x 1.0 is exact).</para></summary>
    Friend Function ShadowSampleParam(mapSize As Integer, radiusScale As Single) As Vector4
        Dim inv = 1.0F / mapSize
        Return New Vector4(0.0F, 0.0F, inv * radiusScale, inv)
    End Function

    ''' <summary>The default rig's softness (PreviewLightRig.DefaultPreset, "Portrait": 2.0 texels, the value every preset has): the
    ''' value that maps to the game's filter (rev-E08).</summary>
    Friend ReadOnly DefaultRigSoftness As Single = PreviewLightRig.DefaultPreset().Rig.ShadowSoftnessTexels

    ''' <summary>APP (rev-E08): the scale of the game's Poisson radius for the rig's Softness, softness / the default rig's softness:
    ''' 2.0 (every preset; the default; Fallout 4's floor) -&gt; 1 = the game's +-3 texels; 4.0 -&gt; 2 (+-6 texels). The stored Softness
    ''' is in Fallout 4's range [DefaultRigSoftness, PreviewShadowSettings.MaxPcfRadius] at the source (PreviewLightRig
    ''' .WithShadowSoftnessInRange; user decision 21-oct-2026: only softer than the game, never harder - a rig saved below it is
    ''' migrated at the config's load): the number reads as this ratio on Fallout 4's engine receiver (and as texels of PCF radius on
    ''' the ground's).</summary>
    Friend Function RigShadowRadiusScale(softnessTexels As Single) As Single
        Return softnessTexels / DefaultRigSoftness
    End Function

    ''' <summary>APP (rev-E08): the value of the sun PS's AppShadowLift uniform for the rig's Darkness D: 1 - D, D bounded to [0, 1] as
    ''' the rig's other receivers bound it. The PS's shadow factor becomes mix(1, s, D) (Fo4DFLightSource.AppShadowDarknessLines); D = 1
    ''' (every preset) gives lift 0, the game's factor bit for bit, and so does a program whose uniform was never set (GL's 0).</summary>
    Friend Function RigShadowLift(darkness As Single) As Single
        Return 1.0F - Math.Clamp(darkness, 0.0F, 1.0F)
    End Function

    ''' <summary>FadeDistances (cb2 name 8): .x = d(s_k), .y = d(s_k + blend) with d(s) = (f s - f n) / ((f - n) s), the device depth
    ''' of the view distance s (0x142247D81..DD3; n, f the frame's near and far), blend = [0x143E71D94] = 100 (fBlendSplitDirShadow .data).
    ''' s_k = [light + 0x250 + 4 k] is the cascade fit's split (0x142250A00, not traced: re.md H-3); the rig's one map covers the whole
    ''' scene the camera sees: its split is the camera's far, so every pixel takes cascade 0 (z' &lt; .y) and none cascade 1 (.x &lt; z' is
    ''' false: z' &lt;= 1 = d(f)). .zw only with 3 splits (iDirShadowSplits .data 2: not written).</summary>
    Friend Function FadeDistances(near As Single, far As Single) As Vector4
        Const Blend As Single = 100.0F
        Dim d = Function(s As Single) (far * s - far * near) / ((far - near) * s)
        Return New Vector4(d(far), d(far + Blend), 0.0F, 0.0F)
    End Function

    ''' <summary>ShadowFadeParam.x (cb2 name 12) = [0x143E71D8C]^2, [0x143E71D8C] = max(fDirShadowDistance, 1000) = 3000
    ''' (fDirShadowDistance .data 3000, 0x142F99698; 0x142181AA4..AC8; written 0x1422482C6). .yzw are never written.</summary>
    Friend Function ShadowFadeParam() As Vector4
        Const DirShadowDistance As Single = 3000.0F
        Return New Vector4(DirShadowDistance * DirShadowDistance, 0.0F, 0.0F, 0.0F)
    End Function

    ''' <summary>The composite's DynamicResolutionParams (cb2 name 5; 0x14221765F..659): (sx - 1 / W, sy - 1 / H, 1, 1), s = the dynamic
    ''' resolution scale [0x142F4E708] / [0x142F4E70C] (.data 1.0).</summary>
    Friend Function DynamicResolutionParams(w As Integer, h As Integer) As Vector4
        Return New Vector4(1.0F - 1.0F / w, 1.0F - 1.0F / h, 1.0F, 1.0F)
    End Function

    ''' <summary>The composite's SunColor.w (0x142217ACB..AD3) = [0x142F987D0], 8.0 in the .data; SunDirection.w (0x142217A30..A38) =
    ''' [0x143E5F10C], a .bss value only the sky update writes (0x1406C76D7 / 0x1406C7DE3): 0 without a sky (the preview has none).</summary>
    Friend Const SunColorW As Single = 8.0F
    Friend Const SunDirectionW As Single = 0.0F
End Module
