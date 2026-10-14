''' <summary>THE EFFECT TECHNIQUE OF A FALLOUT 4 BSEffectShaderProperty AND THE b05 PROGRAMS IT SELECTS (Fallout4.exe 1.11.240.0; ola 3,
''' D:\WinTmp\rc\ola3\re.md 2).
''' <list type="bullet">
''' <item>The flags the technique reads (EngineFlags): the NIF block's 64-bit flags (BSShaderProperty LoadBinary 0x142168761..771) or,
''' for a LooksMenu overlay, the new property's (ctor 0x1421774D0: 0, then bits 31 / 32, 0x142177504..51F); overwritten by the
''' applicator 0x14216AF90 when a material file was applied (ApplyMaterialData 0x1421715E0, 0x14217167F), then bit 29 from bit 62
''' (0x14217189E..8B8); then the property's slot 42 0x142177570 for every shape.</item>
''' <item>The technique: BSEffectShaderProperty::GetRenderPasses 0x1421777A0 -&gt; 0x142178150 (one If per bit, cited) and the pass's
''' modifiers 0x142177A15..A69.</item>
''' <item>The programs: Fo4EffectSource.VertexShaderId / PixelShaderId (SetupTechnique 0x1422250D0 -&gt; 0x142274F30 / 0x142274F40); the
''' engine draws the technique only when both are in the cache (BeginTechnique 0x142231AE0) and, with UI_MASK_RECTS, only with rects
''' (0x1422250F9..11E).</item>
''' </list></summary>
Friend Module Fo4EffectTechnique

    ' Technique bits (BSEffectShader define builder 0x142274B40..EF8; asm2glsl.ETECHBITS).
    Friend Const TechVc As UInteger = 1UI << 0
    Friend Const TechSkinned As UInteger = 1UI << 1
    Friend Const TechTexture As UInteger = 1UI << 2
    Friend Const TechSoft As UInteger = 1UI << 12
    Friend Const TechGrayscaleToColor As UInteger = 1UI << 13
    Friend Const TechGrayscaleToAlpha As UInteger = 1UI << 14
    Friend Const TechEnvmap As UInteger = 1UI << 19
    Friend Const TechLighting As UInteger = 1UI << 10
    Friend Const TechFalloff As UInteger = 1UI << 4
    Friend Const TechRgbFalloff As UInteger = 1UI << 21
    Friend Const TechUiMaskRects As UInteger = 1UI << 27
    Friend Const TechDecal As UInteger = 1UI << 28
    Friend Const TechPremultiplyAlpha As UInteger = 1UI << 30

    ''' <summary>The flags of a BSEffectShaderProperty the constructor leaves (0x1421774D0: BSShaderProperty's ctor writes 0 at +0x30,
    ''' 0x1421688D0; then bit 31 ZBufferTest and bit 32 ZBufferWrite, 0x142177504..51F): the flags of a LooksMenu overlay's new property
    ''' before its material is applied (F4EE OverlayInterface.cpp:164-179, RE_OVERLAYS_BGEM_FO4_SSE 1.1).</summary>
    Friend Const EffectPropertyCtorFlags As ULong = (1UL << 31) Or (1UL << 32)

    ''' <summary>The bits the applicator 0x14216AF90 sets from an EFFECT material file (rbx = the BGEM file struct, ctor 0x14216A4E0, read by
    ''' the binary reader 0x14216BF20 - common part 0x14216C01D..3CB, effect part 0x14216C6AC..706 - or the JSON reader 0x14216EFF0), as the
    ''' unified material holds the file's fields. The struct bytes neither reader writes for an effect keep the ctor's 0 (0x14216A4F7,
    ''' 0x14216A5D8..A616, 0x14216A69C): bits 0 (+0x8), 9 (+0x9E), 10 (+0x96), 12 (+0x90), 18 (+0x98), 19 (+0xA0), 20 (+0x93), 21 (+0x97),
    ''' 25 (+0xA6), 29 (+0x92), 38 (+0xA1), 47 (+0x95), 53 (+0xB8), 54 (+0xA5), 56 (+0x14A), 61 (+0x9A); bit 7 = +0x17A and not +0x14A
    ''' (0x14216B085..09F), +0x14A being 0.</summary>
    Friend Function EffectMaterialBits(mb As FO4UnifiedMaterial_Class) As (Bitn As Integer, Value As Boolean)()
        Return {(7, mb.EnvironmentMapping),             ' +0x17A (0x14216C371)
                (15, mb.Refraction),                    ' +0xBB (0x14216C305)
                (16, mb.RefractionFalloff),             ' +0xC0 (0x14216C317)
                (31, mb.ZBufferTest),                   ' +0xBA (0x14216C28A)
                (32, mb.ZBufferWrite),                  ' +0xB9 (0x14216C278)
                (35, mb.DecalNoFade),                   ' +0x94 (0x14216C2E1)
                (36, mb.TwoSided),                      ' +0x99 (0x14216C2CF)
                (4, mb.GrayscaleToPaletteColor),        ' +0x1BC (0x14216C3CB)
                (62, mb.EffectLightingEnabled),         ' +0x9D (0x14216C6BE)
                (6, mb.FalloffEnabled),                 ' +0x18F (0x14216C6D0)
                (8, mb.FalloffColorEnabled),            ' +0x190 (0x14216C6E2)
                (49, mb.BloodEnabled),                  ' +0x9B (0x14216C6AC)
                (30, mb.SoftEnabled),                   ' +0x1A4 (0x14216C706)
                (5, mb.GrayscaleToPaletteAlpha),        ' +0x1BD (0x14216C6F4)
                (0, False), (9, False), (10, False), (12, False), (18, False), (19, False), (20, False), (21, False), (25, False),
                (29, False), (38, False), (47, False), (53, False), (54, False), (56, False), (61, False)}
    End Function

    ''' <summary>The 64-bit flags GetRenderPasses 0x1421777A0 reads for an effect shape. <paramref name="nifFlags"/> = F4SPF1 | F4SPF2 &lt;&lt; 32
    ''' of the NIF's BSEffectShaderProperty (ignored for an <paramref name="overlay"/>: a new property, EffectPropertyCtorFlags).
    ''' <paramref name="influenceByte"/> = the material's LightingInfluence byte +0xB5 (FO4UnifiedMaterial_Class.EngineEffectConstants:
    ''' trunc(LI * 255), 0x142171806..825).</summary>
    Friend Function EngineFlags(nifFlags As ULong, mb As FO4UnifiedMaterial_Class, hasSkin As Boolean, vertexDesc As ULong, overlay As Boolean,
                                influenceByte As Integer) As ULong
        Dim f = If(overlay, EffectPropertyCtorFlags, nifFlags)
        Dim decalApplies = mb.EngineAppliesDecalBits(), skin = hasSkin, slot42Vc = True, slot42Li = True
#If DEBUG Then
        ' ShadowGate --fo4-effect-technique-law's mutants (Fo4GBufferConstants.GateMutantOn, the one switch)
        If Fo4GBufferConstants.GateMutantOn("efx-decal-arg3") Then decalApplies = Not decalApplies
        If Fo4GBufferConstants.GateMutantOn("efx-skin") Then skin = Not skin
        If Fo4GBufferConstants.GateMutantOn("efx-slot42-vc") Then slot42Vc = False
        If Fo4GBufferConstants.GateMutantOn("efx-slot42-li") Then slot42Li = False
#End If
        If mb.ArchivoFo4Aplicado() Then
            For Each b In EffectMaterialBits(mb)
                Dim v = b.Value
#If DEBUG Then
                If Fo4GBufferConstants.GateMutantOn("efx-mat-" & b.Bitn) Then v = Not v
#End If
                f = Fo4GBufferTechnique.SetBit(f, b.Bitn, v)
            Next
            ' +0x91 Decal -> bits 26 and 27 only when ApplyMaterialData's 3rd argument is 0 (0x14216B12A..156; a LooksMenu overlay passes 1).
            If decalApplies Then
                f = Fo4GBufferTechnique.SetBit(f, 26, mb.Decal)
                f = Fo4GBufferTechnique.SetBit(f, 27, mb.Decal)
            End If
            f = Fo4GBufferTechnique.SetBit(f, 1, skin)                                                  ' 0x14216B001..009 / B05E
            f = Fo4GBufferTechnique.SetBit(f, 37, Fo4GBufferTechnique.ColorOffset(vertexDesc) <> 0UI)  ' 0x14216B012..021 / B070
            f = Fo4GBufferTechnique.SetBit(f, 29, Fo4GBufferTechnique.Bit(f, 62))                       ' 0x14217189E..8B8
        End If
        ' Slot 42 0x142177570 (every effect shape).
        If slot42Vc Then f = Fo4GBufferTechnique.SetBit(f, 37, Fo4GBufferTechnique.ColorOffset(vertexDesc) <> 0UI)  ' 0x1421775B3..5C8
        If slot42Li AndAlso Fo4GBufferTechnique.Bit(f, 62) AndAlso Not (influenceByte * (1.0F / 255.0F) > 0.0F) Then ' 0x1421775CD..609 (1/255 = 0x3B808081)
            f = Fo4GBufferTechnique.SetBit(f, 62, False)
        End If
        Return f
    End Function

    ''' <summary>What the technique reads besides the flags (GetRenderPasses' arguments and the property's material): the material's
    ''' base texture path +0x88 and palette path +0x90 (non-empty, 0x14167C620), the geometry's NiAlphaProperty [geom+0x130] (Nothing
    ''' without one), and the geometry's class - a particle system (vfunc +0x88), a strip of particles (RTTI 0x14344B100), its
    ''' sub-texture count [[geom+0x160]+0x78] and the distortion textures +0xA0 / +0xA8. The preview draws no particles (user decision
    ''' 12-oct-2026: notice only), so its draws pass False / 0 there; the census gate passes them.</summary>
    Friend Structure EffectInputs
        Public Flags As ULong
        Public BasePath As Boolean
        Public PalettePath As Boolean
        Public AlphaFlags As Integer?
        Public Particle As Boolean
        Public StripParticle As Boolean
        Public SubTextures As Boolean
        Public DistortionTextures As Boolean
    End Structure

    ''' <summary>THE TECHNIQUE 0x142178150 and the pass's modifiers 0x142177A15..A69, at the preview's runtime state: no merged instance
    ''' (geom vfunc +0x1F8 null, 0x1416D5A60), not first person ([r9+0xB0] 0), no precipitation emitter ([prop+0x70] null, ctor
    ''' 0x1421774F0), PREMULTIPLY global [0x143E5E345] 0 (.data; set only by the interface render 0x140A3DDDC).</summary>
    Friend Function Technique(e As EffectInputs) As UInteger
        Dim f = e.Flags
        Dim bit = Function(n As Integer) Fo4GBufferTechnique.Bit(f, n)
        ' TEXTURE: the base path +0x88 non-empty, or +0x58 a texture other than the default [0x143D872B0] (0x14217818A..1BE); the
        ' preview's +0x58 is set only from the path (slot 42 puts the default there for a named base, 0x142177672..6A1).
        Dim tex = e.BasePath
        Dim t As UInteger = If(bit(37), TechVc, 0UI)                                                      ' 0x14217841B..42E
        If tex Then t = t Or TechTexture                                                                 ' 0x142178431..438
        If e.Particle AndAlso e.SubTextures AndAlso tex Then t = t Or (1UI << 3)                         ' 0x142178398..3C7 / 440..448
        If bit(1) Then t = t Or TechSkinned                                                              ' 0x14217844B..456
        If bit(6) AndAlso Not e.Particle Then t = t Or TechFalloff                                       ' 0x14217837D..38F / 459..461
        If bit(8) AndAlso Not e.Particle Then t = t Or TechRgbFalloff                                    ' 0x142178384 / 464..46C
        If e.Particle Then t = t Or (1UI << 7)                                                           ' 0x1421781EE..215 / 46F..478
        If e.StripParticle Then t = t Or (1UI << 8)                                                      ' 0x14217821A..235 / 47B..489
        If e.AlphaFlags.HasValue Then                                                                    ' 0x1421783C7..41B
            Dim a = e.AlphaFlags.Value
            Dim src = (a >> 1) And &HF, dst = (a >> 5) And &HF
            Dim addDst = 0
#If DEBUG Then
            If Fo4GBufferConstants.GateMutantOn("efx-tech-addblend") Then addDst = 1
#End If
            If dst = addDst Then t = t Or (1UI << 5)                                                      ' ADDBLEND, 0x1421783D4..3E0 / 48C..494
            If src = 4 AndAlso dst = 7 Then
                t = t Or (1UI << 16)                                                                     ' MULTBLEND_DECAL, 0x1421783E4..3F8 / 4A7..4B2
            ElseIf dst = 2 OrElse src = 4 Then
                t = t Or (1UI << 6)                                                                      ' MULTBLEND, 0x1421783FF..413 / 497..4A4
            End If
        End If
        If bit(62) Then t = t Or TechLighting                                                            ' 0x14217824F..261 / 4B5..4C1
        If bit(23) Then t = t Or (1UI << 11)                                                             ' PROJECTED_UV 0x142178266 / 4C4..4CD
        If bit(30) Then t = t Or TechSoft                                                                ' 0x142178274 / 4D0..4DC
        Dim palette = e.PalettePath
#If DEBUG Then
        If Fo4GBufferConstants.GateMutantOn("efx-tech-palette") Then palette = True
#End If
        If bit(4) AndAlso palette Then t = t Or TechGrayscaleToColor                                     ' 0x142178282..29F / 4DF..4EA
        If bit(5) AndAlso palette Then t = t Or TechGrayscaleToAlpha                                     ' 0x1421782A4..2C3 / 4ED..4F8
        If bit(45) Then t = t Or (1UI << 18)                                                             ' SKY_OBJECT 0x1421782C8..2DC / 4FB..507
        If bit(7) Then t = t Or TechEnvmap                                                               ' 0x142178184 / 50A..513
        If (bit(60) AndAlso Not bit(61)) OrElse bit(55) Then t = t Or (1UI << 20)                         ' PIPBOY 0x1421782E1..313 / 516..521
        If e.Particle AndAlso e.DistortionTextures Then t = t Or (1UI << 22)                              ' 0x142178318..34A / 524..52F
        ' NORMALS: FALLOFF / RGB_FALLOFF (as set above), PROJECTED_UV or ENVMAP, without a precipitation emitter (0x142178532..587).
        Dim normalsEnv = bit(7)
#If DEBUG Then
        If Fo4GBufferConstants.GateMutantOn("efx-tech-normals") Then normalsEnv = False
#End If
        If (t And (TechFalloff Or TechRgbFalloff)) <> 0UI OrElse bit(23) OrElse normalsEnv Then t = t Or (1UI << 23)
        If bit(17) Then t = t Or (1UI << 26)                                                             ' EYE 0x14217834F..35A / 55E..567
        ' The pass's modifiers (0x142177A15..A69): bit 29 merged instance (none), bit 12 cleared in first person (no), bit 27 flag 11,
        ' bit 30 the PREMULTIPLY global (0), bit 28 flag 26 or 27.
        If bit(11) Then t = t Or TechUiMaskRects                                                         ' 0x142177A32..A45
        If bit(26) OrElse bit(27) Then t = t Or TechDecal                                                ' 0x142177A57..A69
        Return t
    End Function

    ''' <summary>The UI-mask veto (SetupTechnique 0x1422250F9..11E): UI_MASK_RECTS with the rect count [0x143E5E120] = 0 draws nothing.
    ''' The count's only writer is the interface render 0x140A3D4C0..5EA (the rects of a menu): 0 in the preview's world frame.</summary>
    Friend Function UiMaskVetoed(technique As UInteger) As Boolean
        Return (technique And TechUiMaskRects) <> 0UI
    End Function
End Module

''' <summary>An effect technique (GetRenderPasses 0x1421777A0): its own type, as Fo4Technique for the G-buffer.</summary>
Friend Structure Fo4EffectTech
    Friend ReadOnly Value As UInteger
    Friend Sub New(value As UInteger)
        Me.Value = value
    End Sub
    ''' <summary>SetupTechnique's two ids (0x1422250E6 / 0x1422250EF); no HS / DS.</summary>
    Friend Function Key() As (Vs As UInteger, Ps As UInteger)
        Return (Fo4EffectSource.VertexShaderId(Value), Fo4EffectSource.PixelShaderId(Value))
    End Function
End Structure
