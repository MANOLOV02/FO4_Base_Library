''' <summary>WHICH SHAPES REFRACT AND HOW (both games): the refraction-normals pass a shape gets instead of its colour pass, its
''' technique bits, strength, fade and depth. One pure law (gate ParityGate `refraction-law`), read by the refraction pass
''' (Render.vb). Tools/re-docs/RE_REFRACTION_BOTH_2026-10-03.md.</summary>
Friend Module RefractionLaw

    Friend Structure RefractionInputs
        Public IsSse As Boolean
        Public IsEffect As Boolean
        Public Refraction As Boolean        ' SLSF1 / F4SF1 15 (FO4: bgsm/bgem bRefraction)
        Public TempRefraction As Boolean    ' SLSF1 / F4SF1 2
        ''' <summary>SLSF1 / F4SF1 16 (Fire_Refraction = clib kRefractionFalloff; FO4: bRefractionFalloff).</summary>
        Public RefractionFalloff As Boolean
        ''' <summary>SLSF1 / F4SF1 13 (Non_Projective_Shadows = the clamp variant; set by the invisibility setter).</summary>
        Public ClampVariant As Boolean
        Public ModelSpaceNormals As Boolean ' SLSF1 / F4SF1 12
        Public Skinned As Boolean           ' SLSF1 / F4SF1 1
        Public VertexColors As Boolean      ' SLSF2 / F4SF2 5 (bit 37)
        Public PipboyScreen As Boolean      ' F4SF2 28
        Public RefractionWritesDepth As Boolean ' F4SF2 31
        ''' <summary>Lighting: material refractionPower (SSE raw; FO4 .bgsm clamp(0, 1), NIF raw). Effect: fRefractionPower.</summary>
        Public Strength As Single
    End Structure

    Friend Structure RefractionPass
        ''' <summary>The shape gets the refraction-normals pass (and no colour pass).</summary>
        Public Applies As Boolean
        ''' <summary>Its technique has a VS / PS in the game's cache (else SetupTechnique fails and the pass is skipped).</summary>
        Public Drawn As Boolean
        Public Technique As Integer
        Public ClampVariant As Boolean
        Public Falloff As Boolean
        Public VertexAlpha As Boolean
        Public ModelSpace As Boolean
        Public Skinned As Boolean
        ''' <summary>FO4 with F4SF2 31: depth 3 (write); else depth 1.</summary>
        Public DepthWrite As Boolean
        ''' <summary>Lighting carries the LOD fade (cb2[5].w / cb2[4].w); a FO4 effect writes 1.0.</summary>
        Public UsesLodFade As Boolean
        Public Strength As Single
    End Structure

    ''' <summary>FO4 refraction VS shipped in the shader pack (recs 1519..1695): any other technique is not drawn (4.3).</summary>
    Private ReadOnly Fo4Vs As Integer() = {&H202, &H21A, &H21B, &H21E, &H21F, &H61A, &H61B, &HA06, &HA1A, &HA1B, &HA1E, &HA1F}

    ''' <summary>A FO4 BSEffect builds no pass at all with Temp_Refraction (flag 2) and without flag 60 Pipboy_Screen
    ''' (BSEffectShaderProperty::GetRenderPasses 0x1421777A0, row 2: 0x142177919..0x14217792E), before the Refraction test (row 4).
    ''' One owner: Fo4RenderPassLaw and this law read it.</summary>
    Friend Function Fo4EffectNoPasses(tempRefraction As Boolean, pipboyScreen As Boolean) As Boolean
        Return tempRefraction AndAlso Not pipboyScreen
    End Function

    Friend Function Classify(s As RefractionInputs) As RefractionPass
        Dim r As New RefractionPass With {.Strength = s.Strength}
        If s.IsSse Then
            ' Only BSLighting refracts (flags & 0x8004, 0x14151A6BB); BSEffect has no refraction path (3.1).
            If s.IsEffect OrElse Not (s.Refraction OrElse s.TempRefraction) Then Return r
            r.Applies = True
            r.ModelSpace = s.ModelSpaceNormals : r.VertexAlpha = s.VertexColors : r.Skinned = s.Skinned
            r.Falloff = s.RefractionFalloff : r.ClampVariant = s.ClampVariant
            r.Technique = If(s.ModelSpaceNormals, 2, &H1A) Or If(s.VertexColors, 1, 0) Or If(s.Skinned, 4, 0) Or
                          If(s.RefractionFalloff, &H400, 0) Or &H200 Or If(s.ClampVariant, &H800, 0)
            ' Every {2,0x1A} x VC x skin x {none, 0x400, 0x800} VS ships; 0x400 with 0x800 does not (1.1).
            r.Drawn = Not (s.RefractionFalloff AndAlso s.ClampVariant)
            r.UsesLodFade = True
            Return r
        End If
        If s.IsEffect Then
            ' BSEffect: Temp_Refraction without Pipboy_Screen -> no passes; Refraction -> the utility pass (4.1).
            If Fo4EffectNoPasses(s.TempRefraction, s.PipboyScreen) Then Return r
            If Not s.Refraction Then Return r
            r.Applies = True
            r.Skinned = s.Skinned : r.Falloff = s.RefractionFalloff : r.ClampVariant = s.ClampVariant
            r.Technique = If(s.Skinned, &H21E, &H21A) Or If(s.RefractionFalloff, &H400, 0) Or If(s.ClampVariant, &H800, 0)
            r.UsesLodFade = False
        Else
            If Not (s.Refraction OrElse s.TempRefraction) Then Return r
            r.Applies = True
            r.ModelSpace = s.ModelSpaceNormals : r.VertexAlpha = s.VertexColors : r.Skinned = s.Skinned
            r.Falloff = s.RefractionFalloff : r.ClampVariant = s.ClampVariant
            r.Technique = If(s.ModelSpaceNormals, 2, &H1A) Or If(s.VertexColors, 1, 0) Or If(s.Skinned, 4, 0) Or
                          If(s.RefractionFalloff, &H400, 0) Or &H200 Or If(s.ClampVariant, &H800, 0)
            r.UsesLodFade = True
        End If
        ' VS = T & 0xF7A5FFDF must ship (4.3).
        r.Drawn = Array.IndexOf(Fo4Vs, r.Technique And &HF7A5FFDF) >= 0
        r.DepthWrite = s.RefractionWritesDepth
        Return r
    End Function

    ''' <summary>The refraction LOD fade (SSE 0x1414E8480, FO4 0x142169A30, the same law): x = distance * s / 7200;
    ''' x &gt; fRefractionLODFadeEnd (0.03) -&gt; no pass at all (returns -1); x &lt;= fRefractionLODFadeStart (0.025) -&gt; 1;
    ''' between, clamp((x - end) / (start - end), 0, 1).</summary>
    Friend Function LodFade(distance As Single, s As Single) As Single
        Const FadeStart As Single = 0.025F, FadeEnd As Single = 0.03F
        Dim x = distance * s * 0.000138889F
        If x > FadeEnd Then Return -1.0F
        If x <= FadeStart Then Return 1.0F
        Return Math.Min(1.0F, Math.Max(0.0F, (x - FadeEnd) / (FadeStart - FadeEnd)))
    End Function

    ''' <summary>s of the refraction LOD fade for a NIF shown on its own (Tools/re-docs/RE_REFRACTION_BOTH_2026-10-03.md 7, 9):
    ''' s = lodAdjust / fLODFadeOutMultObjects:LOD (the root BSFadeNode has fade type 0). The multiplier is the exe default
    ''' (SSE 5, FO4 4.5; user decision 3-oct-2026: app values, not the installed INI files). lodAdjust = worldFOV /
    ''' fDefaultFOV (80 in both exes), raw degree numbers (SSE 0x141516FD7, FO4 0x1421C8EAA), and the world camera is the
    ''' PREVIEW's (user decision): the engine's FOV number is the horizontal FOV of a 16:9 frame - NiCamera::SetPerspective
    ''' (SSE 0x140EF17A0, FO4 0x1421C8CC0) gives half-height tan(fov/2) * 0.5625 for the World scene graph - so the
    ''' preview's vertical FOV v is the engine number 2 atan(tan(v/2) / 0.5625).</summary>
    Friend Function LodScale(isSse As Boolean) As Single
        Dim mitadV = PreviewControl.PreviewFovYDegrees * Math.PI / 360.0
        Dim fovMotor = CSng(2.0 * Math.Atan(Math.Tan(mitadV) / 0.5625) * 180.0 / Math.PI)
        Return (fovMotor / 80.0F) / If(isSse, 5.0F, 4.5F)
    End Function

    ''' <summary>The image-space offset scale k of o = k * N.z * (N.xy - 0.5): SSE 0.1 (PS 15822: k = 0.05 N.z, o = 0.1 N.z (N.xy - 0.5)), FO4
    ''' 0.25 (rec 3658); and the alpha it keeps (SSE S0.a, FO4 C.a) and its tint (SSE manager +0x98 = 0; FO4 (0,0,0,0.3)).</summary>
    Friend Function ImageSpaceConstants(isSse As Boolean) As (OffsetScale As Single, SceneAlpha As Boolean, Tint As OpenTK.Mathematics.Vector4)
        If isSse Then Return (0.1F, True, New OpenTK.Mathematics.Vector4(0, 0, 0, 0))
        Return (0.25F, False, New OpenTK.Mathematics.Vector4(0, 0, 0, 0.3F))
    End Function
End Module
