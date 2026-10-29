Option Strict On
Option Explicit On

''' <summary>THE engine default-texture law of both games: what the game samples in a material texture slot whose path is empty or
''' names a file absent from the game's data (user decision 7-oct-2026: the app draws the same, and the frame's notice says so). Pure,
''' no GL. Every branch cites Fallout4.exe 1.11.240 / SkyrimSE.exe 1.6.x (installed); ParityGate `engine-default-textures` checks it
''' by hand with the directed mutants of <see cref="GateMutant"/>. Fallout 4 with an absent file: traced (ola 4b, 15-oct-2026,
''' D:\WinTmp\rc\texfallback\re.md): the texture manager leaves the result NULL (0x1416A6FE7..7079 -&gt; 0x1417A3DCC..DED) and the
''' loader by path 0x142182420 puts the member's default ladder there (0x1421825AC..5D9), the same as for an empty path, with no
''' message; Skyrim SE's: measured (0x1414EC1A8..1E7, the engine's own "TEXTURE ERROR : Unable to load file %s").</summary>
Friend Module EngineDefaultTextureLaw

    ''' <summary>A slot after the load. One predicate of "absent" (rev-03): the file is not in the game's data
    ''' (FilesDictionary_class.TryGetEntry, MaterialData.TextureFileExists).</summary>
    Friend Enum SlotState As Byte
        ''' <summary>No path (FO4: GetTexture 0x14216A1E0 tests the string, 0x14216A204..209; SSE: BSFixedString(NULL) 0x140EE4C3D).</summary>
        Empty = 0
        Loaded = 1
        ''' <summary>A path whose file is not in the game's data.</summary>
        Missing = 2
        ''' <summary>A path whose file is in the data but the preview did not load it (its upload failed): a preview gap, not the law.</summary>
        NotLoadedByPreview = 3
        ''' <summary>Fallout 4 only: a path whose file is in the data and the engine cannot create (EngineTextureCreationLaw.Verdict
        ''' Rejected). The NiTexture exists (0x1416C98DC..8F0; path A 0x1416C9AEA), so neither the ladder nor the smooth-spec White
        ''' swap applies (that one compares the pointer, 0x1421D3CE7..D09), and the binder binds its NULL SRV (lighting
        ''' 0x1422044E0..4FD, effect 0x142225809..82F; RE_FO4_TEXTURE_CREATION_2026-10-19 5). Wins over Loaded and NotLoadedByPreview
        ''' (StateOf): what the preview loaded is not what the game samples.</summary>
        RejectedByEngine = 4
    End Enum

    ''' <summary>What the game samples. Own: the slot's texture. NoTexture: the draw does not sample the slot. PreviousDraw: the draw
    ''' does not bind the slot but its pixel shader samples it. (NotTraced, Fallout 4's absent file before ola 4b, is gone: traced.)
    ''' </summary>
    Friend Enum EngineTexture As Byte
        Own = 0
        Fo4DiffuseMap
        Fo4NormalMap
        Fo4White
        Fo4EyeCubeMap
        SseDefNormalMap
        SseDefHeightMap
        SseDefaultWhiteMap
        SseDefCubeMap
        NoTexture
        PreviousDraw
        ''' <summary>Fallout 4: the NULL SRV the binder binds for a file the engine cannot create (SlotState.RejectedByEngine); the
        ''' flush 0x1418248A0..8C3 passes it to PSSetShaderResources and D3D11 reads 0 in every channel (Rgba).</summary>
        SrvNull
    End Enum

    ''' <summary>The material slots the app samples (the engine member of each, per game and family, below).</summary>
    Friend Enum MaterialSlot As Byte
        Diffuse      ' FO4 lighting +0x48 / effect base +0x58; SSE lighting +0x48 / effect base +0x58
        Normal       ' FO4 lighting +0x50 / effect +0x78; SSE lighting +0x58
        Glow         ' FO4 Glowmap +0xC0 (slot 2: glow / hair flow); SSE Glowmap class (slot 2)
        Lightmask    ' SSE slot 2 with rim / soft: +0x60
        Greyscale    ' FO4 lighting +0x68 / effect +0x60; SSE effect greyscale +0x60
        Cube         ' slot 4
        EnvMask      ' FO4 effect +0x70; SSE Envmap class slot 5
        FaceSlot5    ' FO4 Face +0xC0 (slot 5)
        Slot7        ' FO4 smooth-spec +0x60; SSE +0x68 (MSN specular / back light)
        Height       ' SSE Parallax slot 3: +0xA0 (vf8 0x141525FE7..FF3) -> PS t3
        InnerLayer   ' SSE MultiLayerParallax slot 6: +0xA0 (vf8 0x141528647..656) -> PS t8
    End Enum

    ''' <summary>Fallout 4 lighting: the bits of the shape's G-buffer record (Fo4GBufferTechnique.Tech*; SetupMaterial 0x142204140 binds
    ''' by them) and the property's environment flag. No G-buffer pass: every field False.</summary>
    Friend Structure Fo4LightingContext
        Friend Textured As Boolean
        Friend GlowMap As Boolean
        Friend GradientRemap As Boolean
        Friend Face As Boolean
        Friend EnvironmentFlag As Boolean ' property flag 7: the slot-4 cube registered in the EnvMapArray (ENV 2.3)
    End Structure

    ''' <summary>An effect's technique bits that decide its slots (FO4 0x142178150, SSE 0x14152A2B0).</summary>
    Friend Structure EffectContext
        Friend Recolor As Boolean        ' FO4 bits 13 / 14, SSE bits 19 / 20: flags 4 / 5 and a palette path
        Friend Envmap As Boolean         ' FO4 bit 19 (flag 7); SSE effects have no envmap (BSVER 100 LoadBinary 0x141529450)
    End Structure

    ''' <summary>The Skyrim SE lighting class and the fills its material runs (0x141523F00 and the class's vf10).</summary>
    Friend Structure SseLightingContext
        Friend GlowClass As Boolean     ' shader type 2
        Friend EnvmapClass As Boolean   ' shader types 1 and 16
        Friend RimOrSoft As Boolean
        Friend BackLighting As Boolean
        Friend SpecularMsn As Boolean   ' SPECULAR and MODELSPACENORMALS
        Friend ParallaxClass As Boolean ' shader type 3 (vt 0x141B3FFC8)
        Friend MultiLayerParallaxClass As Boolean ' shader type 11 (vt 0x141B40388)
        Friend EyeClass As Boolean      ' shader type 16 (vt 0x141B3FED8)
    End Structure

#If DEBUG Then
    ''' <summary>DEBUG ONLY (ParityGate engine-default-textures, ShadowGate --default-textures): a directed mutant of the law. Never set
    ''' by the app; a Release build has neither the switch nor a mutant branch (ola 9, 5b: each comparison is its own DEBUG statement
    ''' next to the law it mutates).</summary>
    Friend Enum DefaultTextureMutant
        None
        Fo4DiffuseWhite                 ' FO4 lighting diffuse -> White (the app before C4)
        Fo4SmoothSpecNoWhiteSwap        ' FO4 smooth-spec keeps NormalMap (0x1421D3CE7..D09 dropped)
        Fo4GlowWithoutGlowMapBit        ' FO4 t3 counted as sampled without technique bit 14
        Fo4MissingOwnFallback           ' FO4 absent file: the app's own fallback instead of the ladder (the law before ola 4b)
        Fo4EffectMaskMissingWhite       ' FO4 effect env mask absent: White (the ctor's default) instead of the raw ladder's NormalMap
        Fo4FaceMissingWhite             ' FO4 face slot 5 absent: White instead of the raw ladder's NormalMap (the face apart from normal / glow)
        Fo4EffectGreyscaleEmptyBound    ' FO4 effect palette sampled with an empty path (bits 13 / 14 without the path test)
        EffectBaseEmptyAlwaysPrevious   ' an effect base without TEXTURE read as sampled without recolor
        SseMissingAsEmpty               ' SSE: an absent file takes the class fill (loader 0x1414EC0A0 dropped)
        SseSlot7SpecularFirst           ' SSE slot 7: SPECULAR && MSN before back light (fill order inverted)
        SseLightmaskGlowExcluded        ' SSE slot 2 rim / soft not filled in the Glowmap class (the v2 exclusion without a citation)
        EyeCubeNotNeutral               ' the Fo4EyeCubeMapPolicy exception dropped (EyeCubeMap gives an empty-slot notice)
        Fo4DiffuseMapMagenta            ' FO4 DiffuseMap content (255, 0, 255)
        MlpEnvMaskNotNeutral            ' the SseMlpEnvMaskPolicy exception dropped (an empty MLP mask gives an empty-slot notice)
    End Enum
    Friend GateMutant As DefaultTextureMutant = DefaultTextureMutant.None
#End If

    ''' <summary>The sRGB flag Fallout 4's loader passes for <paramref name="slot"/> (the 5th argument of 0x142182420, the dl of
    ''' MakeSRGB 0x14183E680, RE_FO4_TEXTURE_CREATION_2026-10-19 1.3), the same in both families for the slots they share. Lighting
    ''' (common part 0x1421D3C60): slot 0 diffuse sRGB (0x1421D3C72..C84), 1 normal, 2 glow and 7 smooth-spec raw, 3 greyscale
    ''' palette sRGB (0x1421D3CD2..CE1), slot 4 cube sRGB (Envmap / Eye / MultiLayerParallax 0x1421CF8C0 / 0x1421D00A0 /
    ''' 0x1421D3330), Face slot 5 raw (0x1421D15C0). Effect (0x1422249CE..C52): base, palette, cube sRGB (0x1422249D6 / A6C / B06);
    ''' mask, normal raw (0x1422249A6 / BB0 / C52).</summary>
    Friend Function Fo4SlotLoadsSrgb(slot As MaterialSlot) As Boolean
#If DEBUG Then
        ' ParityGate fo4-texture-creation-law's directed mutants (Fo4GBufferConstants.GateMutantOn, the one switch).
        If Fo4GBufferConstants.GateMutantOn("dtl-srgb-palette-raw") AndAlso slot = MaterialSlot.Greyscale Then Return False
#End If
        Select Case slot
            Case MaterialSlot.Diffuse, MaterialSlot.Greyscale, MaterialSlot.Cube : Return True
            Case Else : Return False
        End Select
    End Function

    ''' <summary>The slots whose file Fallout 4's loader of the family creates: lighting +0x48 / +0x50 / +0x58 / +0x60 / +0x68
    ''' (0x1421D3C72..CE1), the cube (slot 4, GetTexture 0x14216A214..224) and the Face class's slot 5 (0x1421D168A..697) - not the
    ''' env mask, whose +0xC8 is the default (0x1421CF8F5..91A); effect: base, palette, cube, mask, normal (0x142224980:
    ''' 0x1422249E3 / A79 / B13 / BBD / C5F).</summary>
    Friend Function Fo4LoadedSlots(effect As Boolean) As MaterialSlot()
        If effect Then Return {MaterialSlot.Diffuse, MaterialSlot.Greyscale, MaterialSlot.Cube, MaterialSlot.EnvMask, MaterialSlot.Normal}
        Return {MaterialSlot.Diffuse, MaterialSlot.Normal, MaterialSlot.Glow, MaterialSlot.Slot7, MaterialSlot.Greyscale, MaterialSlot.Cube, MaterialSlot.FaceSlot5}
    End Function

    ''' <param name="inData">The file is in the game's data (FilesDictionary_class.TryGetEntry).</param>
    ''' <param name="engineRejects">Fallout 4: the engine cannot create that file in this slot (EngineTextureCreationLaw.Verdict
    ''' Rejected for the slot's sRGB flag); it wins over what the preview loaded.</param>
    Friend Function StateOf(path As String, loaded As Boolean, inData As Boolean, Optional engineRejects As Boolean = False) As SlotState
#If DEBUG Then
        If Fo4GBufferConstants.GateMutantOn("dtl-rejected-loses") AndAlso loaded Then Return SlotState.Loaded
#End If
        If inData AndAlso engineRejects AndAlso Not String.IsNullOrEmpty(path) Then Return SlotState.RejectedByEngine
        If loaded Then Return SlotState.Loaded
        If String.IsNullOrEmpty(path) Then Return SlotState.Empty
        If inData Then Return SlotState.NotLoadedByPreview
        Return SlotState.Missing
    End Function

    ''' <summary>Fallout 4 lighting (BSLightingShaderMaterial and its classes). Empty: GetTexture 0x14216A1E0 (0x14216A257..288), the
    ''' member's ladder (cube -&gt; EyeCubeMap, sRGB -&gt; DiffuseMap, raw -&gt; NormalMap). Missing (ola 4b, traced): GetTexture calls
    ''' 0x142182420 (0x14216A242) with the same cube / sRGB flags; the manager's result for an absent file is NULL (the demand
    ''' 0x1416A6E50 returns 1 with no stream, 0x1416A6FE7..7079; 0x1417A3DCC..DED copies the entry only on 0) and 0x142182420 takes the
    ''' ladder (0x1421825AC..5D9): the same texture as Empty, smooth-spec's NormalMap -&gt; White swap included (0x1421D3CE7..D09 compares
    ''' the pointer). What is sampled stays the record's bits.</summary>
    Friend Function Fo4Lighting(slot As MaterialSlot, st As SlotState, c As Fo4LightingContext) As EngineTexture
        If st = SlotState.Loaded OrElse st = SlotState.NotLoadedByPreview Then Return EngineTexture.Own
        Dim sampled As Boolean
        Select Case slot
            Case MaterialSlot.Diffuse, MaterialSlot.Normal, MaterialSlot.Slot7 : sampled = c.Textured
            Case MaterialSlot.Glow : sampled = c.GlowMap
            Case MaterialSlot.Greyscale : sampled = c.Textured AndAlso c.GradientRemap
            Case MaterialSlot.FaceSlot5 : sampled = c.Face
            Case MaterialSlot.Cube : sampled = c.EnvironmentFlag
            Case Else : sampled = False      ' EnvMask: +0xC8 nulled by 0x1421CF8F5..91A, no G-buffer record reads it; Lightmask: no FO4 member
        End Select
#If DEBUG Then
        If slot = MaterialSlot.Glow AndAlso GateMutant = DefaultTextureMutant.Fo4GlowWithoutGlowMapBit Then sampled = True
#End If
        If Not sampled Then Return EngineTexture.NoTexture
        ' A file the engine cannot create: its NiTexture, so no ladder and no White swap; the NULL SRV is bound (SlotState doc). The
        ' cube is not bound by SetupMaterial: the register 0x142239930 takes its texture, whose header 0x14183E680 filled before D3D
        ' failed (RE_FO4_TEXTURE_CREATION Addendum A.1 / A.2); refused (header unlike the array's, or fewer mips) it gives 0xFF
        ' (0x14223999F) and the constant .x 0 (0x142205947..961): no reflection, the zeros of SrvNull add none either (the preview
        ' registers nothing, MaterialData.EnvmapTexture). An empty file: no reflection (0x14220593A) save the global cube of
        ' 0x14220591E..938 (not traced). A header the register accepts never reaches this state: LightingCubeVerdict makes it NotLaw.
#If DEBUG Then
        If Fo4GBufferConstants.GateMutantOn("dtl-rejected-white") AndAlso st = SlotState.RejectedByEngine AndAlso slot = MaterialSlot.Slot7 Then Return EngineTexture.Fo4White
#End If
        If st = SlotState.RejectedByEngine Then Return EngineTexture.SrvNull
#If DEBUG Then
        If st = SlotState.Missing AndAlso GateMutant = DefaultTextureMutant.Fo4MissingOwnFallback Then Return EngineTexture.Own
#End If
        Select Case slot
            Case MaterialSlot.Diffuse            ' +0x48 <- slot 0, sRGB (0x1421D3C72..C84)
#If DEBUG Then
                If GateMutant = DefaultTextureMutant.Fo4DiffuseWhite Then Return EngineTexture.Fo4White
#End If
                Return EngineTexture.Fo4DiffuseMap
            Case MaterialSlot.Slot7              ' +0x60 <- slot 7 raw (0x1421D3CBB..CC9), NormalMap swapped for White (0x1421D3CE7..D09)
#If DEBUG Then
                If GateMutant = DefaultTextureMutant.Fo4SmoothSpecNoWhiteSwap Then Return EngineTexture.Fo4NormalMap
#End If
                Return EngineTexture.Fo4White
            Case MaterialSlot.Greyscale : Return EngineTexture.Fo4DiffuseMap     ' +0x68 <- slot 3, sRGB (0x1421D3CD2..CE1)
            Case MaterialSlot.Cube : Return EngineTexture.Fo4EyeCubeMap          ' slot 4 (0x14216A25A..25F)
            Case MaterialSlot.FaceSlot5                                           ' face 0x1421D168A..697, raw ladder
#If DEBUG Then
                If st = SlotState.Missing AndAlso GateMutant = DefaultTextureMutant.Fo4FaceMissingWhite Then Return EngineTexture.Fo4White
#End If
                Return EngineTexture.Fo4NormalMap
            Case Else : Return EngineTexture.Fo4NormalMap                        ' normal 0x1421D3C8D..C9B, glow 0x1421D0468..475
        End Select
    End Function

    ''' <summary>Fallout 4 effect (BSEffectShaderMaterial): an empty path keeps the ctor default (0x1422242F0, 0x142224980). Missing
    ''' (ola 4b, traced): the loader 0x142224980 calls 0x142182420 per member (0x1422249E3 / A79 / B13 / BBD / C5F; base and palette
    ''' sRGB 0x1422249D6 / A6C, cube r9b 1 0x142224B03, mask raw 0x142224BB0 / call 0x142224BBD, normal raw 0x142224C52 / call
    ''' 0x142224C5F) and gets the ladder
    ''' (0x1421825AC..5D9): base and palette DiffuseMap, cube EyeCubeMap, mask and normal NormalMap (the mask is NOT the ctor's White).
    ''' A named path sets TEXTURE (bit 2) and the palette bits 13 / 14 (0x142178150 tests the path, not the file): base and palette are
    ''' bound (t0 0x142225800..803, t4).</summary>
    Friend Function Fo4Effect(slot As MaterialSlot, st As SlotState, c As EffectContext) As EngineTexture
        If st = SlotState.Loaded OrElse st = SlotState.NotLoadedByPreview Then Return EngineTexture.Own
        Select Case slot
            Case MaterialSlot.Diffuse
                ' A file the engine cannot create: a named path (TEXTURE set, t0 bound); a failed creation binds the object's NULL SRV
                ' (0x142225827), an empty file the NULL texture (0x14222582C) (RE_FO4_TEXTURE_CREATION Addendum A.5).
#If DEBUG Then
                If Fo4GBufferConstants.GateMutantOn("dtl-effect-rejected-ladder") AndAlso st = SlotState.RejectedByEngine Then Return EngineTexture.Fo4DiffuseMap
#End If
                If st = SlotState.RejectedByEngine Then Return EngineTexture.SrvNull
                If st = SlotState.Missing Then
#If DEBUG Then
                    If GateMutant = DefaultTextureMutant.Fo4MissingOwnFallback Then Return EngineTexture.Own
#End If
                    Return EngineTexture.Fo4DiffuseMap
                End If
                ' TEXTURE (bit 2) off (0x142178150): no t0 bind (0x142225800..803); a recolor PS samples it (rec1003 L60, rec1430 L53).
                If c.Recolor Then Return EngineTexture.PreviousDraw
#If DEBUG Then
                If GateMutant = DefaultTextureMutant.EffectBaseEmptyAlwaysPrevious Then Return EngineTexture.PreviousDraw
#End If
                Return EngineTexture.NoTexture
            Case MaterialSlot.Greyscale
#If DEBUG Then
                ' the mutant: an empty path bound as if bits 13 / 14 did not need it - the ladder's DiffuseMap the code below gives
                If st = SlotState.Empty AndAlso GateMutant = DefaultTextureMutant.Fo4EffectGreyscaleEmptyBound Then Return EngineTexture.Fo4DiffuseMap
#End If
                If st = SlotState.Empty Then Return EngineTexture.NoTexture  ' bits 13/14 need the path
                ' t4 is bound under bits 13 / 14 only (test ecx, 0x6000, 0x142225919; A.5), and they need flag 4 / 5 AND the palette path
                ' (0x142178282..2C3, Fo4EffectTechnique; c.Recolor): bound, a failed creation binds its NULL SRV (0x142225937 / 0x14222593F).
#If DEBUG Then
                If Fo4GBufferConstants.GateMutantOn("dtl-palette-rejected-bound") AndAlso st = SlotState.RejectedByEngine Then Return EngineTexture.SrvNull
                If Fo4GBufferConstants.GateMutantOn("dtl-missing-palette-bound") AndAlso st = SlotState.Missing Then Return EngineTexture.Fo4DiffuseMap
#End If
                If st = SlotState.RejectedByEngine Then Return If(c.Recolor, EngineTexture.SrvNull, EngineTexture.NoTexture)
                ' An absent file: the same bind - the path is named, so the flag 4 / 5 decides (0x142178282..2C3; A.5, 0x142225919);
                ' without it t4 is not bound whatever the loader put in +0x60.
                If st = SlotState.Missing AndAlso Not c.Recolor Then Return EngineTexture.NoTexture
#If DEBUG Then
                If st = SlotState.Missing AndAlso GateMutant = DefaultTextureMutant.Fo4MissingOwnFallback Then Return EngineTexture.Own
#End If
                Return EngineTexture.Fo4DiffuseMap                                    ' sRGB ladder (Missing)
            Case MaterialSlot.Cube, MaterialSlot.EnvMask, MaterialSlot.Normal
                If Not c.Envmap Then Return EngineTexture.NoTexture                   ' t5 / t7 / t1 under bit 19 only
                ' A.5: mask t7 / normal t1 bind NULL through 0x1421DAF40 (0x1421DAF45 / 0x1421DAF4A); the cube t5 binds the NULL SRV of a
                ' failed creation (0x142225C1E) - its empty file is NotLaw (EngineTextureCreationLaw.EffectCubeVerdict), never this state.
                If st = SlotState.RejectedByEngine Then Return EngineTexture.SrvNull
#If DEBUG Then
                If st = SlotState.Missing AndAlso GateMutant = DefaultTextureMutant.Fo4MissingOwnFallback Then Return EngineTexture.Own
#End If
                If slot = MaterialSlot.Cube Then Return EngineTexture.Fo4EyeCubeMap   ' ctor 0x14222435B / cube ladder
                If slot = MaterialSlot.EnvMask Then                                   ' ctor 0x14222437A (Empty) / raw ladder (Missing)
#If DEBUG Then
                    If GateMutant = DefaultTextureMutant.Fo4EffectMaskMissingWhite Then Return EngineTexture.Fo4White
#End If
                    Return If(st = SlotState.Empty, EngineTexture.Fo4White, EngineTexture.Fo4NormalMap)
                End If
                Return EngineTexture.Fo4NormalMap                                     ' ctor 0x142224399
            Case Else
                Return EngineTexture.NoTexture
        End Select
    End Function

    ''' <summary>Skyrim SE lighting: an absent file -> loader 0x1414EC0A0 -> BSShader_DefNormalMap [0x143336448] (a cube DefCubeMap) after
    ''' the engine's "TEXTURE ERROR : Unable to load file %s" (0x1414EC1A8..1E7); an empty path -> the class fill (base 0x141523F00,
    ''' Glowmap / Envmap vf10). A slot the class does not sample: NoTexture.</summary>
    Friend Function SseLighting(slot As MaterialSlot, st As SlotState, c As SseLightingContext) As EngineTexture
        If st = SlotState.RejectedByEngine Then Throw New ArgumentException("RejectedByEngine is Fallout 4's (EngineTextureCreationLaw transcribes Fallout4.exe)", NameOf(st))
        If st = SlotState.Loaded OrElse st = SlotState.NotLoadedByPreview Then Return EngineTexture.Own
        If Not SseLightingSamples(slot, c) Then Return EngineTexture.NoTexture
#If DEBUG Then
        If st = SlotState.Missing AndAlso GateMutant = DefaultTextureMutant.SseMissingAsEmpty Then st = SlotState.Empty
#End If
        If st = SlotState.Missing Then
            Return If(slot = MaterialSlot.Cube, EngineTexture.SseDefCubeMap, EngineTexture.SseDefNormalMap)
        End If
        Select Case slot
            Case MaterialSlot.Diffuse, MaterialSlot.Normal : Return EngineTexture.SseDefNormalMap    ' 0x141523F00..F32
            Case MaterialSlot.Glow : Return EngineTexture.SseDefaultWhiteMap                         ' Glowmap vf10 -> [0x143336418]
            Case MaterialSlot.Lightmask : Return EngineTexture.SseDefNormalMap                       ' +0x60 0x141523F36..F72
            Case MaterialSlot.Cube : Return EngineTexture.SseDefCubeMap                              ' Envmap vf10
            Case MaterialSlot.EnvMask
                ' MultiLayerParallax vf10 0x1415287EC..806 and Eye vf10 0x141525640 (0x14152569B..6B5): +0xA8 / +0xB0 -> DefNormalMap,
                ' so cb1[2].y = 1 and t5 is sampled (0x1415489BB, 0x141548F6D..F7F).
                If c.MultiLayerParallaxClass OrElse c.EyeClass Then Return EngineTexture.SseDefNormalMap
                Return EngineTexture.NoTexture                                                       ' +0xA8 NULL: cb1[2].y = 0 (0x1415489BB)
            Case MaterialSlot.Height : Return EngineTexture.SseDefHeightMap                          ' Parallax vf10 0x1415260BA..D4: [0x143336428] = state+0x88 (0x14101DAF8)
            Case MaterialSlot.InnerLayer : Return EngineTexture.SseDefNormalMap                      ' MultiLayerParallax vf10 0x1415287AA..C4
            Case MaterialSlot.Slot7
#If DEBUG Then
                ' the mutant: SPECULAR && MSN tested before the back light (the fill order inverted)
                If GateMutant = DefaultTextureMutant.SseSlot7SpecularFirst AndAlso c.SpecularMsn Then Return EngineTexture.SseDefHeightMap
#End If
                If c.BackLighting Then Return EngineTexture.SseDefNormalMap                                                                 ' ..F94
                If c.SpecularMsn Then Return EngineTexture.SseDefHeightMap                                                                  ' ..FBA
                Return EngineTexture.SseDefNormalMap
            Case Else : Return EngineTexture.NoTexture
        End Select
    End Function

    ''' <summary>Which Skyrim SE lighting slots the class samples: diffuse and normal always (0x141523F00..F32); slot 2 in the Glowmap
    ''' class (vf10) / with rim or soft (0x141523F36..F72, any class); cube and mask in the Envmap classes; slot 7 with back light or
    ''' SPECULAR &amp;&amp; MSN (0x141523F76..FBA). (Facegen is not described: C4 leaves its slots out.)</summary>
    Private Function SseLightingSamples(slot As MaterialSlot, c As SseLightingContext) As Boolean
#If DEBUG Then
        If slot = MaterialSlot.Lightmask AndAlso GateMutant = DefaultTextureMutant.SseLightmaskGlowExcluded AndAlso c.GlowClass Then Return False
#End If
        Select Case slot
            Case MaterialSlot.Diffuse, MaterialSlot.Normal : Return True
            Case MaterialSlot.Glow : Return c.GlowClass
            Case MaterialSlot.Lightmask : Return c.RimOrSoft
            Case MaterialSlot.Cube, MaterialSlot.EnvMask : Return c.EnvmapClass OrElse c.MultiLayerParallaxClass   ' MLP t4 / t5 0x141548F4B..F68
            Case MaterialSlot.Height : Return c.ParallaxClass                                    ' t3 0x141548A07..A16
            Case MaterialSlot.InnerLayer : Return c.MultiLayerParallaxClass                       ' t8 0x141548EF5..F04
            Case MaterialSlot.Slot7 : Return c.BackLighting OrElse c.SpecularMsn
            Case Else : Return False
        End Select
    End Function

    ''' <summary>Skyrim SE effect (ctor 0x1415560A0; SetupMaterial 0x141556C40).</summary>
    Friend Function SseEffect(slot As MaterialSlot, st As SlotState, c As EffectContext) As EngineTexture
        If st = SlotState.RejectedByEngine Then Throw New ArgumentException("RejectedByEngine is Fallout 4's (EngineTextureCreationLaw transcribes Fallout4.exe)", NameOf(st))
        If st = SlotState.Loaded OrElse st = SlotState.NotLoadedByPreview Then Return EngineTexture.Own
        Select Case slot
            Case MaterialSlot.Diffuse
                If st = SlotState.Missing Then Return EngineTexture.SseDefNormalMap
                ' TEXTURE (bit 6 = path non-empty, 0x140EB12C0) off: t0 is not bound (0x141556DB5..DB7).
                If c.Recolor Then Return EngineTexture.PreviousDraw
#If DEBUG Then
                If GateMutant = DefaultTextureMutant.EffectBaseEmptyAlwaysPrevious Then Return EngineTexture.PreviousDraw
#End If
                Return EngineTexture.NoTexture
            Case MaterialSlot.Greyscale
                If st = SlotState.Empty Then Return EngineTexture.NoTexture  ' bits 19 / 20 need the path (0x141556E4F..E59)
                Return EngineTexture.SseDefNormalMap
            Case Else
                Return EngineTexture.NoTexture
        End Select
    End Function

    ''' <summary>The bytes (R, G, B, A, R8G8B8A8_UNORM) of a 2D built-in texture: the creator's dword, little-endian (FO4 0x14182C5B0,
    ''' SSE 0x14101D750); SSE DefCubeMap: zeros (0x14101DDA0); Fallout 4's NULL SRV: zeros (D3D11 reads 0 from an unbound slot,
    ''' RE_FO4_TEXTURE_CREATION_2026-10-19 5 and D:\WinTmp\rc\texfallback\re.md 3). Nothing for EyeCubeMap (a file) and for Own /
    ''' NoTexture / PreviousDraw.</summary>
    Friend Function Rgba(t As EngineTexture) As Byte()
        Select Case t
            Case EngineTexture.Fo4DiffuseMap
#If DEBUG Then
                If GateMutant = DefaultTextureMutant.Fo4DiffuseMapMagenta Then Return {255, 0, 255, 255}
#End If
                Return {128, 0, 255, 255}                                   ' 0xFFFF0080 (0x14182CBD1)
            Case EngineTexture.Fo4NormalMap : Return {128, 128, 255, 255}   ' 0xFFFF8080 (0x14182CB07)
            Case EngineTexture.Fo4White : Return {255, 255, 255, 255}       ' 0xFFFFFFFF (0x14182CD65)
            Case EngineTexture.SseDefNormalMap : Return {128, 128, 255, 255} ' 0xFFFF8080 (0x14101D9B0)
            Case EngineTexture.SseDefHeightMap : Return {0, 0, 0, 255}      ' 0xFF000000 (0x14101DA80)
            Case EngineTexture.SseDefaultWhiteMap : Return {255, 255, 255, 255} ' 0xFFFFFFFF (0x14101DC37)
            Case EngineTexture.SseDefCubeMap : Return {0, 0, 0, 0}          ' zero data (0x14101DDA0..DDA7)
            Case EngineTexture.SrvNull
#If DEBUG Then
                If Fo4GBufferConstants.GateMutantOn("dtl-srvnull-alpha") Then Return {0, 0, 0, 255}
#End If
                Return {0, 0, 0, 0}                                         ' NULL SRV (PSSetShaderResources 0x1418248A0..8C3)
            Case Else : Return Nothing
        End Select
    End Function

    ''' <summary>POLICY EXCEPTION, not the law (coordinator's decision 7-oct-2026, C4 L8 "Fo4EyeCubeMapPolicy"): Fallout 4's EyeCubeMap
    ''' in an empty cube slot counts as neutral - 53,997 FO4 shapes (vanilla, flag 7 without a cube; c4_fo4_census_out.txt), and the
    ''' app already draws EyeCubeMap there as the game does (MaterialData.EnvmapTexturePath), so a notice would report no difference.</summary>
    Friend Const Fo4EyeCubeMapPolicy As Boolean = True

    ''' <summary>POLICY EXCEPTION, not the law (rev-06, 7-oct-2026; the same kind as Fo4EyeCubeMapPolicy): Skyrim SE MultiLayerParallax
    ''' fills an empty slot 5 (environment mask) with DefNormalMap (vf10 0x1415287EC..806), so cb1[2].y = 1 and t5.r = 128/255 scales
    ''' the reflection (0x141548F6D..F7F) - not neutral by C4 L8. 593 MLP shapes leave the slot empty (579 vanilla; grupo-c
    ''' parallax-mlp misc_out.txt, re2 mlp_compare_out.txt) and the app draws that same default (MaterialData.EngineSlotTextureId), so
    ''' a notice would fill the panel in every ice cave with no difference to report. EnvMask with SseDefNormalMap for an EMPTY slot
    ''' comes from the MultiLayerParallax class and from the Eye class, which fills the same slot the same way (vf10 0x141525640; user decision 12-oct-2026: no notice either); the Envmap class gives NoTexture.</summary>
    Friend Const SseMlpEnvMaskPolicy As Boolean = True

    ''' <summary>C4 L8: is <paramref name="t"/> the identity of <paramref name="slot"/>'s role? Multiplicative slots (diffuse, glow,
    ''' masks) -> white; normal -> flat (128, 128, 255, 255); cube -> zeros (no reflection); the palette has no identity; the FACE t8 role
    ''' is not traced (never neutral). Derived from Rgba, except EyeCubeMap (<see cref="Fo4EyeCubeMapPolicy"/>) and an empty
    ''' MultiLayerParallax or Eye mask (<see cref="SseMlpEnvMaskPolicy"/>; the name stays, it covers both classes since 12-oct-2026).</summary>
    Friend Function IsNeutral(slot As MaterialSlot, t As EngineTexture) As Boolean
#If DEBUG Then
        If slot = MaterialSlot.EnvMask AndAlso t = EngineTexture.SseDefNormalMap AndAlso GateMutant = DefaultTextureMutant.MlpEnvMaskNotNeutral Then Return False
        If t = EngineTexture.Fo4EyeCubeMap AndAlso GateMutant = DefaultTextureMutant.EyeCubeNotNeutral Then Return False
#End If
        If slot = MaterialSlot.EnvMask AndAlso t = EngineTexture.SseDefNormalMap Then Return SseMlpEnvMaskPolicy
        If t = EngineTexture.Fo4EyeCubeMap Then Return Fo4EyeCubeMapPolicy
        Dim got = Rgba(t)
        If got Is Nothing Then Return False
        Dim identity As Byte()
        Select Case slot
            Case MaterialSlot.Diffuse, MaterialSlot.Glow, MaterialSlot.Lightmask, MaterialSlot.EnvMask, MaterialSlot.Slot7 : identity = {255, 255, 255, 255}
            Case MaterialSlot.Normal : identity = {128, 128, 255, 255}
            Case MaterialSlot.Cube : identity = {0, 0, 0, 0}
            Case Else : Return False            ' Greyscale (no identity), FaceSlot5 (role not traced)
        End Select
        Return got.SequenceEqual(identity)
    End Function
End Module
