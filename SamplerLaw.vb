''' <summary>HOW EACH GAME SAMPLES EACH MATERIAL TEXTURE: address mode (the material's clamp mode, or a fixed one) and filter, per
''' slot, per shader, per game (gate ParityGate `sampler-law`). Tools/re-docs/RE_TEXTURE_ADDRESS_MODES_2026-10-03.md.
''' <para>ADDR is the material clamp value 0..3 used directly (no remap): 0 clamp/clamp, 1 U clamp V wrap, 2 U wrap V clamp,
''' 3 wrap/wrap. FILT: 0 point (one mip), 1 bilinear of one mip (SSE mip 0; FO4 MinLOD = MaxLOD = the texture's LOD byte, 0),
''' 2 trilinear, 3 anisotropic 16 (SSE hard-coded; FO4 iMaxAnisotropy:Display default 16). A slot the shader binds without
''' writing FILT inherits the last value of that slot in the frame (reset to 3 each frame: SSE 0x141007B70, FO4 0x141816080).</para>
''' <para>Where the clamp comes from: lighting = mat+0x70, effect = SSE mat+0x80 / FO4 mat+0xB4. NIF: BSLightingShaderProperty
''' Texture Clamp Mode (SSE 0x1415241D0, FO4 0x1421CE880), BSEffectShaderProperty byte 0 of its clamp field (SSE 0x14152951B,
''' FO4 0x142176F30). FO4 material files override it with bTileU * 2 + bTileV (lighting 0x14216B5A0, effect 0x14216BB10). SSE has
''' no material-file reader.</para></summary>
Friend Module SamplerLaw

    ''' <summary>The texture units the app binds material textures to (Render.vb ApplyMaterial).</summary>
    Friend Enum AppUnit
        Diffuse = 0
        Normal = 1
        Cube = 2
        EnvMask = 3
        Specular = 4
        Greyscale = 5
        Glow = 6
        Lightmask = 7
        Detail = 8
    End Enum

    Friend Const FiltInherit As Integer = -1

    Friend Structure SlotSampler
        Public Unit As AppUnit
        ''' <summary>The engine's PS slot (t0..t15): the slot whose filter is inherited / written.</summary>
        Public EngineSlot As Integer
        Public Addr As Integer
        Public Filt As Integer
        ''' <summary>MinLOD = MaxLOD for FILT 0 / 1 (the single mip sampled); FO4 effect env cube: EnvMapMinLOD.</summary>
        Public Lod As Single
    End Structure

    Friend Structure SamplerInputs
        Public IsSse As Boolean
        Public IsEffect As Boolean
        ''' <summary>The material's clamp value 0..3 (lighting mat+0x70 / effect mat+0x80 or +0xB4).</summary>
        Public Clamp As Integer
        ''' <summary>SSE: the FaceGen technique (tint t3, detail t4, subsurface t12 with FILT 3).</summary>
        Public Facegen As Boolean
        ''' <summary>FO4 effect env cube: the material's Env Map Min LOD byte (+0xB6).</summary>
        Public EnvMapMinLod As Integer
    End Structure

    Friend Function Slots(s As SamplerInputs) As List(Of SlotSampler)
        Dim r As New List(Of SlotSampler)
        Dim add = Sub(u As AppUnit, slot As Integer, addr As Integer, filt As Integer, lod As Single)
                      r.Add(New SlotSampler With {.Unit = u, .EngineSlot = slot, .Addr = addr, .Filt = filt, .Lod = lod})
                  End Sub
        Dim m = s.Clamp And 3
        If s.IsSse Then
            If s.IsEffect Then
                ' BSEffectShader 0x141556840 / 0x141556C40 / 0x141557090: t0 source (mat+0x80) / aniso; t4 palette 0 / bilinear mip 0.
                add(AppUnit.Diffuse, 0, m, 3, 0)
                add(AppUnit.Greyscale, 4, 0, 1, 0)
            Else
                ' BSLightingShader 0x141547D20 / 0x141548820 / 0x141549550.
                add(AppUnit.Diffuse, 0, m, 3, 0)
                add(AppUnit.Normal, 1, m, 3, 0)
                add(AppUnit.Specular, 2, m, 3, 0)
                add(AppUnit.Cube, 4, m, 3, 0)
                add(AppUnit.EnvMask, 5, m, 3, 0)
                ' glow t6; the FaceGen tint the app binds on the same unit is t3: both mat / aniso.
                add(AppUnit.Glow, If(s.Facegen, 3, 6), m, 3, 0)
                ' rim / soft / subsurface t12: FaceGen writes aniso, otherwise the slot keeps the frame's last filter.
                add(AppUnit.Lightmask, 12, m, If(s.Facegen, 3, FiltInherit), 0)
                add(AppUnit.Detail, 4, m, 3, 0)
            End If
        ElseIf s.IsEffect Then
            ' BSEffectShader 0x1422250D0 / 0x142225650: t0 base (mat+0xB4) / aniso; t4 palette 0 / one mip; t5 env cube 0 / one mip at
            ' max(texture LOD, EnvMapMinLOD); t7 env mask 3 / one mip (ENVMAP); t1 normal 3 / one mip (ENVMAP).
            add(AppUnit.Diffuse, 0, m, 3, 0)
            add(AppUnit.Greyscale, 4, 0, 1, 0)
            add(AppUnit.Cube, 5, 0, 1, Math.Max(0, s.EnvMapMinLod))
            add(AppUnit.EnvMask, 7, 3, 1, 0)
            add(AppUnit.Normal, 1, 3, 1, 0)
        Else
            ' FO4 world lighting = the G-buffer pass BSDFPrePassShader 0x142203560 / 0x142204140 / 0x142205540: t0 / t1 / t2 mat / aniso;
            ' t5 palette 0 / one mip; t3 glow 3 / one mip. The env cube is sampled by BSDFCompositeShader from the EnvMapArray at t8,
            ' ADDR 0 / FILT 3 (0x1422172EB..2FB, 0x142217321..331). The env mask texture is bound by neither pass (RE 6.4): no slot.
            add(AppUnit.Diffuse, 0, m, 3, 0)
            add(AppUnit.Normal, 1, m, 3, 0)
            add(AppUnit.Specular, 2, m, 3, 0)
            add(AppUnit.Greyscale, 5, 0, 1, 0)
            add(AppUnit.Glow, 3, 3, 1, 0)
            add(AppUnit.Cube, 8, 0, 3, 0)
        End If
        Return r
    End Function

    ''' <summary>The utility shader's t0 (z-prepass alpha test, shadow maps): SSE lighting +0x70 / effect +0x80, FILT 3
    ''' (0x141567010); FO4 always mat+0x48 with mat+0x70 (0x142240F40).</summary>
    Friend Function UtilityDiffuse(s As SamplerInputs) As SlotSampler
        Return New SlotSampler With {.Unit = AppUnit.Diffuse, .EngineSlot = 0, .Addr = s.Clamp And 3, .Filt = 3}
    End Function
End Module
