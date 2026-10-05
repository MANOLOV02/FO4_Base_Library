''' <summary>THE DEFERRED TECHNIQUE OF A FALLOUT 4 LIGHTING SHAPE AND THE G-BUFFER PS RECORD IT SELECTS (Fallout4.exe 1.11.240.0).
''' <list type="bullet">
''' <item>The property flags the technique reads: the NIF's 64-bit BSLightingShaderProperty flags (LoadBinary 0x142178BF0 ->
''' 0x142168670), overwritten by the BGSM applicator 0x14216AF90 when a material file was applied (the app's condition:
''' FO4UnifiedMaterial_Class.ArchivoFo4Aplicado), then the slot-42 fixer 0x1421793F0 for every shape.</item>
''' <item>The technique: BSLightingShaderProperty::GetRenderPasses 0x14217A050, deferred branch (one If per bit, cited).</item>
''' <item>The programs: PS id = technique AND 0xFFFFEFE7 (0x142203591 -> 0x142273C10), VS id = 0x142273B80; the engine draws the
''' technique only when both are in the shader cache (BeginTechnique 0x142231AE0), i.e. in the source's closed lists of
''' Fo4GBufferSource (Records: the 470 b09 PS; VertexShaderIds: the 315 b09 VS).</item>
''' </list>
''' Sources: scratchpad opaque-fo4/transcripcion-gbuffer/notas.md 1.1 and census.py technique(); Tools/re-docs/
''' RE_FO4_DEFERRED_FRAME_2026-10-03.md 6-8, RE_FO4_WORLD_ENVMAP_2026-10-03.md 11-12. Gate: ShadowGate --fo4-technique-law (the 22 017
''' corpus shapes against the census).</summary>
Friend Module Fo4GBufferTechnique

    Private Function Bit(v As ULong, n As Integer) As Boolean
        Return ((v >> n) And 1UL) <> 0UL
    End Function

    Private Function SetBit(v As ULong, n As Integer, value As Boolean) As ULong
        Return If(value, v Or (1UL << n), v And Not (1UL << n))
    End Function

    ''' <summary>dword[geom+0x150] &gt;&gt; 22 AND 0x3C = the offset of the COLOR attribute in the vertex desc (0x14216B012..021).</summary>
    Friend Function ColorOffset(vertexDesc As ULong) As UInteger
        Return CUInt((vertexDesc And &HFFFFFFFFUL) >> 22) And &H3CUI
    End Function

    ''' <summary>The bits the BGSM applicator 0x14216AF90 sets from the material's bytes (em\af90b.txt; reader 0x14216BF20, MaterialLib
    ''' Deserialize order), as the unified material holds them: one table for the applied and the NIF-inline cases.</summary>
    Private Function MaterialBits(mb As FO4UnifiedMaterial_Class) As (Bitn As Integer, Value As Boolean)()
        Return {(7, mb.EnvironmentMapping),             ' +0x17A
                (10, mb.Facegen),                       ' +0x96
                (21, mb.SkinTint),                      ' +0x97
                (12, mb.ModelSpaceNormals),             ' +0x90
                (15, mb.Refraction),                    ' +0xBB
                (16, mb.RefractionFalloff),             ' +0xC0
                (18, mb.Hair),                          ' +0x98
                (31, mb.ZBufferTest),                   ' +0xBA
                (32, mb.ZBufferWrite),                  ' +0xB9
                (20, mb.HideSecret),                    ' +0x93
                (35, mb.DecalNoFade),                   ' +0x94
                (61, mb.Tree),                          ' +0x9A
                (4, mb.GrayscaleToPaletteColor),        ' +0x1BC
                (53, mb.AnisoLighting),                 ' +0xB8
                (9, mb.CastShadows),                    ' +0x9E
                (19, mb.DissolveFade),                  ' +0xA0
                (38, mb.Glowmap),                       ' +0xA1
                (36, mb.TwoSided),                      ' +0x99
                (0, mb.SpecularEnabled),                ' +0x8
                (29, mb.ExternalEmittance),             ' +0x92
                (25, mb.Tessellate),                    ' +0xA6
                (54, mb.SkewSpecularAlpha)}             ' +0xA5
    End Function

    ''' <summary>A NIF-inline shape: the bits the unified material reads 1:1 from the SAME bit of the NIF's BSLightingShaderProperty
    ''' (FO4UnifiedMaterial_Class.Create_From_Shader, FO4 path) and owns afterwards - the app's edits included. Every other bit stays
    ''' the NIF's: 16 (RefractionFalloff), 19 (DissolveFade) and 27 (Dynamic_Decal) are not read by the NIF reader; BackLighting,
    ''' SubsurfaceLighting and RimLighting are derived from powers, not read from a bit. An unedited material gives back the NIF's
    ''' bits by construction (gate 8).</summary>
    Private Function InlineMaterialBits(mb As FO4UnifiedMaterial_Class) As (Bitn As Integer, Value As Boolean)()
        Return {(0, mb.SpecularEnabled),                ' HasSpecular
                (4, mb.GrayscaleToPaletteColor),        ' HasGreyscaleToPaletteColor
                (7, mb.EnvironmentMapping),             ' HasEnvironmentMapping
                (9, mb.CastShadows),                    ' CastShadowsFlagValue
                (10, mb.Facegen),                       ' F4SPF1 Face
                (12, mb.ModelSpaceNormals),             ' ModelSpace
                (15, mb.Refraction),                    ' RefractionFlagValue
                (17, mb.EyeEnvironmentMapping),         ' HasEyeEnvironmentMapping
                (18, mb.Hair),                          ' F4SPF1 Hair
                (20, mb.HideSecret),                    ' HideSecretFlagValue
                (21, mb.SkinTint),                      ' F4SPF1 Skin_Tint
                (22, mb.EmitEnabled),                   ' Emissive (Own_Emit)
                (25, mb.Tessellate),                    ' F4SPF1 Tessellate
                (26, mb.Decal),                         ' DecalFlagValue
                (29, mb.ExternalEmittance),             ' HasExternalEmittance
                (31, mb.ZBufferTest),                   ' ZBufferTestFlagValue
                (32, mb.ZBufferWrite),                  ' ZBufferWriteFlagValue
                (35, mb.DecalNoFade),                   ' NoFadeFlagValue
                (36, mb.TwoSided),                      ' DoubleSided
                (38, mb.Glowmap),                       ' HasGlowmap
                (53, mb.AnisoLighting),                 ' AnisotropicLightingFlagValue
                (54, mb.SkewSpecularAlpha),             ' F4SPF2 Skew_Specular_Alpha
                (61, mb.Tree)}                          ' HasTreeAnim
    End Function

    ''' <summary>The flags GetRenderPasses reads and the material the G-buffer constants read (Fo4EngineMaterial).
    ''' <paramref name="nifFlags"/> = F4SPF1 | F4SPF2 &lt;&lt; 32 of the NIF's block as loaded (<paramref name="nifBlock"/>). Without a
    ''' material file the material in memory owns the bits it reads 1:1 (InlineMaterialBits) and every field (FromInlineMaterial).</summary>
    Friend Function EngineState(nifFlags As ULong, mb As FO4UnifiedMaterial_Class, hasSkin As Boolean, vertexDesc As ULong,
                                nifBlock As NiflySharp.Blocks.BSLightingShaderProperty) As (Flags As ULong, Material As Fo4EngineLightingMaterial)
        Dim f = nifFlags
        Dim m As Fo4EngineLightingMaterial
        If mb.ArchivoFo4Aplicado() Then
            ' Applicator 0x14216AF90: the table, then the struct bytes the v2 reader never writes keep the ctor value 0 (0x14216A4E0).
            For Each b In MaterialBits(mb) : f = SetBit(f, b.Bitn, b.Value) : Next
            ' +0x91 Decal -> bits 26 and 27 only when ApplyMaterialData's 3rd argument is 0 (0x14216B12A..0x14216B156: the
            ' 5th argument of 0x14216AF90 = r15b = ApplyMaterialData's r8b, 0x1421715FC / 0x142171671); a material swap or a
            ' LooksMenu overlay keeps the NIF's decal bits.
            If mb.EngineAppliesDecalBits() Then
                f = SetBit(f, 26, mb.Decal)
                f = SetBit(f, 27, mb.Decal)
            End If
            For Each b In {47, 62, 6, 8, 56, 49, 30, 5}         ' +0x95, +0x9D, +0x18F, +0x190, +0x14A, +0x9B, +0x1A4, +0x1BD
                f = SetBit(f, b, False)
            Next
            f = SetBit(f, 1, hasSkin)                          ' 0x14216B001..009 / 0x14216B05E: skin instance [geom+0x140]
            f = SetBit(f, 37, ColorOffset(vertexDesc) <> 0UI)  ' 0x14216B012..021 / 0x14216B070
            m = Fo4EngineMaterial.FromMaterialFile(mb, f)       ' 0x14216B610, after the flags (0x14217168F)
        Else
            For Each b In InlineMaterialBits(mb) : f = SetBit(f, b.Bitn, b.Value) : Next
            m = Fo4EngineMaterial.FromInlineMaterial(mb, f, nifBlock)
        End If
        ' rev-43 b (user decision): a FaceGen part of the NPC Manager preview draws with the wetness the bake writes.
        If mb.WetnessIsBaked() Then
            Dim w = mb.EngineWetness()
            m.Wetness = {w.SpecScale, w.SpecPowerScale, w.SpecMinvar, w.EnvMapScale, w.FresnelPower, w.Metalness}
        End If
        ' Slot-42 fixer 0x1421793F0 (every shape; dump_fixer_1421793F0.txt), in its order.
        f = SetBit(f, 37, ColorOffset(vertexDesc) <> 0UI)
        Fo4EngineMaterial.FixMaterial(m, f)                                                   ' 0x1421794FD..0x142179516
        If Bit(f, 61) AndAlso Not Bit(f, 37) Then f = SetBit(f, 61, False)                   ' 0x142179524..0x14217954F
        If Bit(f, 17) AndAlso Not hasSkin Then f = SetBit(f, 17, False)                       ' 0x142179558..0x142179575
        If Bit(f, 0) AndAlso Fo4EngineMaterial.SpecularIsZero(m) Then f = SetBit(f, 0, False) ' 0x14217957E..0x1421795D7
        If Bit(f, 38) AndAlso Bit(f, 7) Then f = SetBit(f, 38, False)                         ' 0x1421795E0..0x142179605
        f = SetBit(f, 40, False)                                                              ' 0x142179669..0x142179687
        Return (f, m)
    End Function

    ''' <summary>GetRenderPasses 0x14217A050, deferred constructor. <paramref name="alphaFlags"/> = the NiAlphaProperty flags after
    ''' the BGSM alpha law (0x1421718BD..9EE; the app's ResolveEngineAlpha), Nothing without the property. <paramref name="bted"/> =
    ''' the geometry carries the "BTED" extra data (0x142507510). The preview's runtime state (notas 1.1): fade 1, prop+0x64 = 1,
    ''' no effectData, clip-volume global [0x143E5E118] = 0, no LOD copy, not first person, never instanced (ENV 12.1).
    ''' Returns Nothing when the shape has no G-buffer pass, with the reason in <paramref name="noPass"/> (Fo4RenderPassLaw.Fo4NoPass:
    ''' the branch that returned; None with a technique).</summary>
    Friend Function Technique(f As ULong, hasSkin As Boolean, vertexDesc As ULong, alphaFlags As Integer?, matAlpha As Single, bted As Boolean,
                              ByRef group As Integer, Optional ByRef noPass As Fo4RenderPassLaw.Fo4NoPass = Fo4RenderPassLaw.Fo4NoPass.None) As UInteger?
        noPass = Fo4RenderPassLaw.Fo4NoPass.None
        Dim decal = (f And &HC000000UL) <> 0UL
        Dim blend = alphaFlags.HasValue AndAlso (alphaFlags.Value And 1) <> 0
        ' 0x14217A16A..0x14217A184 as read by the app: NOT VERIFIED, its consequence contradicts the game (RE_FO4_PASS_GROUPS_DEPTH 8.1,
        ' under re-examination); followed for now, the shape is listed in the frame's notice.
        If blend AndAlso Not decal Then
            noPass = Fo4RenderPassLaw.Fo4NoPass.LightingBlendWithoutDecal
            Return Nothing
        End If
        If (f And &H8004UL) <> 0UL Then                                                   ' refraction: 0x14217A566..0x14217A668
            noPass = Fo4RenderPassLaw.Fo4NoPass.RefractionOnly
            Return Nothing
        End If
        Dim a = matAlpha
        If Not alphaFlags.HasValue AndAlso a >= 1.0F Then a = 1.0F                        ' 0x14217A457..0x14217A473
        Dim tAlpha = a < 1.0F                                                             ' 0x14217A485..0x14217A489
        Dim t52 = False, t130 = False                                                     ' [rsp+0x52] / [rsp+0x130], 0x14217A50C..0x14217A55E
        If tAlpha OrElse blend Then
            t52 = True
            If alphaFlags.HasValue Then
                If (alphaFlags.Value And &H1E) = 0 Then
                    t130 = True
                ElseIf (alphaFlags.Value And &H1E0) = 0 Then
                    t130 = True
                End If
            End If
        End If
        Dim t As UInteger = If(Bit(f, 12), &H2002UI, &H1AUI)                              ' 0x14217A721..0x14217A748
        If Bit(f, 1) Then t = t Or 4UI                                                    ' 0x14217A74B..0x14217A75F
        If Bit(f, 37) Then t = t Or 1UI                                                   ' 0x14217A762..0x14217A772
        If Bit(f, 14) Then t = t Or &H20UI                                                ' 0x14217A775..0x14217A788
        If Bit(f, 46) Then t = t Or (1UI << 25)                                           ' 0x14217A78B..0x14217A7AB
        If Bit(f, 33) Then t = t Or (1UI << 9)                                            ' 0x14217A7AE..0x14217A7C2
        ' bit 29: (F AND 0x600000000) and [0x143E5E118] <> 0: the preview's global is 0.
        If Bit(f, 1) AndAlso bted Then t = t Or (1UI << 30)                               ' 0x14217A802..0x14217A81B
        If Bit(f, 61) Then t = t Or (1UI << 10)                                           ' 0x14217A820..0x14217A841
        If alphaFlags.HasValue AndAlso ((alphaFlags.Value >> 9) And 1) <> 0 Then t = t Or (1UI << 8)   ' 0x14217A83A..0x14217A858
        If Bit(f, 38) Then t = t Or (1UI << 14)                                           ' 0x14217A85C..0x14217A878
        If Bit(f, 55) Then t = t Or (1UI << 16)                                           ' 0x14217A87B..0x14217A894
        If Bit(f, 60) Then t = t Or (1UI << 23)                                           ' 0x14217A897..0x14217A8B0
        If Bit(f, 28) Then t = t Or (1UI << 12)                                           ' 0x14217A8B3..0x14217A8C3
        If Bit(f, 10) AndAlso (vertexDesc And &H4010000000000000UL) <> 0UL Then t = t Or &H80000040UI   ' 0x14217A8CA..0x14217A8E8
        If Bit(f, 18) Then t = t Or (1UI << 17)                                           ' 0x14217A8EF..0x14217A90E
        If Bit(f, 21) Then t = t Or (1UI << 18)                                           ' 0x14217A912..0x14217A923
        If Bit(f, 54) Then t = t Or &H10800UI                                             ' 0x14217A926..0x14217A941
        If (f And &H800002000000UL) = &H800002000000UL Then t = t Or &H280040UI           ' 0x14217A944..0x14217A955
        If Bit(f, 40) Then t = t Or &H400040UI                                            ' 0x14217A958..0x14217A973
        ' bit 7: property RTTI 0x143E5F270 (grass), never a BSLightingShaderProperty.
        If (f And &H200410UL) = &H10UL Then t = t Or (1UI << 26)                          ' 0x14217A984..0x14217A997
        ' bit 24 from effectData: none; bits 27/28: geometry type <> 0xF and vfunc[0x1F8] null (ENV 12.1).
        If decal Then                                                                     ' 0x14217A9F6..0x14217AA37
            If Not t130 OrElse Bit(f, 49) Then
                If Not t52 AndAlso Not (1.0F > a) Then
                    group = 2
                Else
                    group = 3 : t = t Or (1UI << 15)
                End If
            Else
                group = If(Bit(f, 33), 7, 0)
            End If
        Else
            group = If(Bit(f, 33), 7, 0)                                                   ' 0x14217AA39..0x14217AA56
        End If
        If 1.0F > a AndAlso group <> 3 Then t = t Or (1UI << 24)                          ' 0x14217AA5A..0x14217AA67
        Return t
    End Function

    ''' <summary>id of the PS = technique AND 0xFFFFEFE7 (0x142203591 -> 0x142273C10: drops bits 3, 4 and 12).</summary>
    Friend Function PixelShaderId(technique As UInteger) As UInteger
        Return technique And &HFFFFEFE7UI
    End Function

    ''' <summary>id of the VS (0x142273B80): technique AND 0xFFFE27FF when bits 11 and 16 are both set, else AND 0xFFFF2FFF.</summary>
    Friend Function VertexShaderId(technique As UInteger) As UInteger
        Return technique And If((technique And &H10800UI) = &H10800UI, &HFFFE27FFUI, &HFFFF2FFFUI)
    End Function

    ''' <summary>The engine draws the technique: its PS and its VS are in the shader cache (BeginTechnique 0x142231AE0 looks both up
    ''' and fails when one is missing, 0x142231C45..0x142231C56 -> 0x142231CF6; SetupTechnique then aborts the draw, 0x1422035B3).</summary>
    Friend Function HasEnginePrograms(technique As UInteger) As Boolean
        Return MissingEnginePrograms(technique) Is Nothing
    End Function

    ''' <summary>The programs of the technique that are NOT in the shader cache (the source's closed lists: Fo4GBufferSource.Records,
    ''' the 470 b09 PS; Fo4GBufferSource.VertexShaderIds, the 315 b09 VS), named for the frame's notice ("pixel shader 0x..." and / or
    ''' "vertex shader 0x..."); Nothing when both are there.</summary>
    Friend Function MissingEnginePrograms(technique As UInteger) As String
        Dim ps = PixelShaderId(technique), vs = VertexShaderId(technique)
        Dim psIn = Fo4GBufferSource.Records.ContainsKey(ps)
        Dim vsIn = Fo4GBufferSource.VertexShaderIds.Contains(vs)
        If psIn AndAlso vsIn Then Return Nothing
        If Not psIn AndAlso Not vsIn Then Return $"pixel shader 0x{ps:X8} and vertex shader 0x{vs:X8}"
        Return If(psIn, $"vertex shader 0x{vs:X8}", $"pixel shader 0x{ps:X8}")
    End Function
End Module
