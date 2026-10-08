Option Strict On
Option Explicit On

''' <summary>THE engine default-texture law of both games: what the game samples in a material texture slot whose path is empty or
''' names a file absent from the game's data (user decision 7-oct-2026: the app draws the same, and the frame's notice says so). Pure,
''' no GL. Every branch cites Fallout4.exe 1.11.240 / SkyrimSE.exe 1.6.x (installed); ParityGate `engine-default-textures` checks it
''' by hand with the directed mutants of <see cref="GateMutant"/>. Fallout 4 with an absent file: NotTraced (C4 L7); Skyrim SE's:
''' measured (0x1414EC1A8..1E7, the engine's own "TEXTURE ERROR : Unable to load file %s").</summary>
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
    End Enum

    ''' <summary>What the game samples. Own: the slot's texture. NoTexture: the draw does not sample the slot. PreviousDraw: the draw
    ''' does not bind the slot but its pixel shader samples it. NotTraced: what the game samples is not traced (Fallout 4, a file absent
    ''' from the data, C4 L7): the app keeps its own fallback.</summary>
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
        NotTraced
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
    End Structure

    ''' <summary>GATE ONLY (ParityGate engine-default-textures): a directed mutant of the law. Never set by the app.</summary>
    Friend Enum DefaultTextureMutant
        None
        Fo4DiffuseWhite                 ' FO4 lighting diffuse -> White (the app before C4)
        Fo4SmoothSpecNoWhiteSwap        ' FO4 smooth-spec keeps NormalMap (0x1421D3CE7..D09 dropped)
        Fo4GlowWithoutGlowMapBit        ' FO4 t3 counted as sampled without technique bit 14
        Fo4MissingAsLadder              ' FO4 absent file given the NULL ladder (the untraced premise of L7 taken as law)
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

    ''' <param name="inData">The file is in the game's data (FilesDictionary_class.TryGetEntry).</param>
    Friend Function StateOf(path As String, loaded As Boolean, inData As Boolean) As SlotState
        If loaded Then Return SlotState.Loaded
        If String.IsNullOrEmpty(path) Then Return SlotState.Empty
        If inData Then Return SlotState.NotLoadedByPreview
        Return SlotState.Missing
    End Function

    ''' <summary>Fallout 4 lighting (BSLightingShaderMaterial and its classes). Empty: GetTexture 0x14216A1E0 (0x14216A257..288), the
    ''' member's ladder (cube -> EyeCubeMap, sRGB -> DiffuseMap, raw -> NormalMap). Missing: NotTraced (C4 L7).</summary>
    Friend Function Fo4Lighting(slot As MaterialSlot, st As SlotState, c As Fo4LightingContext) As EngineTexture
        If st = SlotState.Loaded OrElse st = SlotState.NotLoadedByPreview Then Return EngineTexture.Own
        Dim sampled As Boolean
        Select Case slot
            Case MaterialSlot.Diffuse, MaterialSlot.Normal, MaterialSlot.Slot7 : sampled = c.Textured
            Case MaterialSlot.Glow : sampled = c.GlowMap OrElse GateMutant = DefaultTextureMutant.Fo4GlowWithoutGlowMapBit
            Case MaterialSlot.Greyscale : sampled = c.Textured AndAlso c.GradientRemap
            Case MaterialSlot.FaceSlot5 : sampled = c.Face
            Case MaterialSlot.Cube : sampled = c.EnvironmentFlag
            Case Else : sampled = False      ' EnvMask: +0xC8 nulled by 0x1421CF8F5..91A, no G-buffer record reads it; Lightmask: no FO4 member
        End Select
        If Not sampled Then Return EngineTexture.NoTexture
        If st = SlotState.Missing AndAlso GateMutant <> DefaultTextureMutant.Fo4MissingAsLadder Then Return EngineTexture.NotTraced
        Select Case slot
            Case MaterialSlot.Diffuse            ' +0x48 <- slot 0, sRGB (0x1421D3C72..C84)
                Return If(GateMutant = DefaultTextureMutant.Fo4DiffuseWhite, EngineTexture.Fo4White, EngineTexture.Fo4DiffuseMap)
            Case MaterialSlot.Slot7              ' +0x60 <- slot 7 raw (0x1421D3CBB..CC9), NormalMap swapped for White (0x1421D3CE7..D09)
                Return If(GateMutant = DefaultTextureMutant.Fo4SmoothSpecNoWhiteSwap, EngineTexture.Fo4NormalMap, EngineTexture.Fo4White)
            Case MaterialSlot.Greyscale : Return EngineTexture.Fo4DiffuseMap     ' +0x68 <- slot 3, sRGB (0x1421D3CD2..CE1)
            Case MaterialSlot.Cube : Return EngineTexture.Fo4EyeCubeMap          ' slot 4 (0x14216A25A..25F)
            Case Else : Return EngineTexture.Fo4NormalMap                        ' normal 0x1421D3C8D..C9B, glow 0x1421D0468..475, face 0x1421D168A..697
        End Select
    End Function

    ''' <summary>Fallout 4 effect (BSEffectShaderMaterial): an empty path keeps the ctor default (0x1422242F0, 0x142224980); Missing:
    ''' NotTraced (C4 L7).</summary>
    Friend Function Fo4Effect(slot As MaterialSlot, st As SlotState, c As EffectContext) As EngineTexture
        If st = SlotState.Loaded OrElse st = SlotState.NotLoadedByPreview Then Return EngineTexture.Own
        Select Case slot
            Case MaterialSlot.Diffuse
                If st = SlotState.Missing Then Return If(GateMutant = DefaultTextureMutant.Fo4MissingAsLadder, EngineTexture.Fo4DiffuseMap, EngineTexture.NotTraced)
                ' TEXTURE (bit 2) off (0x142178150): no t0 bind (0x142225800..803); a recolor PS samples it (rec1003 L60, rec1430 L53).
                If c.Recolor OrElse GateMutant = DefaultTextureMutant.EffectBaseEmptyAlwaysPrevious Then Return EngineTexture.PreviousDraw
                Return EngineTexture.NoTexture
            Case MaterialSlot.Greyscale
                If st = SlotState.Empty AndAlso GateMutant <> DefaultTextureMutant.Fo4EffectGreyscaleEmptyBound Then Return EngineTexture.NoTexture  ' bits 13/14 need the path
                Return If(GateMutant = DefaultTextureMutant.Fo4MissingAsLadder OrElse st = SlotState.Empty, EngineTexture.Fo4DiffuseMap, EngineTexture.NotTraced)
            Case MaterialSlot.Cube, MaterialSlot.EnvMask, MaterialSlot.Normal
                If Not c.Envmap Then Return EngineTexture.NoTexture                   ' t5 / t7 / t1 under bit 19 only
                If st = SlotState.Missing AndAlso GateMutant <> DefaultTextureMutant.Fo4MissingAsLadder Then Return EngineTexture.NotTraced
                If slot = MaterialSlot.Cube Then Return EngineTexture.Fo4EyeCubeMap   ' ctor 0x14222435B
                If slot = MaterialSlot.EnvMask Then Return If(st = SlotState.Empty, EngineTexture.Fo4White, EngineTexture.Fo4NormalMap)  ' ctor 0x14222437A / raw ladder
                Return EngineTexture.Fo4NormalMap                                     ' ctor 0x142224399
            Case Else
                Return EngineTexture.NoTexture
        End Select
    End Function

    ''' <summary>Skyrim SE lighting: an absent file -> loader 0x1414EC0A0 -> BSShader_DefNormalMap [0x143336448] (a cube DefCubeMap) after
    ''' the engine's "TEXTURE ERROR : Unable to load file %s" (0x1414EC1A8..1E7); an empty path -> the class fill (base 0x141523F00,
    ''' Glowmap / Envmap vf10). A slot the class does not sample: NoTexture.</summary>
    Friend Function SseLighting(slot As MaterialSlot, st As SlotState, c As SseLightingContext) As EngineTexture
        If st = SlotState.Loaded OrElse st = SlotState.NotLoadedByPreview Then Return EngineTexture.Own
        If Not SseLightingSamples(slot, c) Then Return EngineTexture.NoTexture
        If st = SlotState.Missing AndAlso GateMutant <> DefaultTextureMutant.SseMissingAsEmpty Then
            Return If(slot = MaterialSlot.Cube, EngineTexture.SseDefCubeMap, EngineTexture.SseDefNormalMap)
        End If
        Select Case slot
            Case MaterialSlot.Diffuse, MaterialSlot.Normal : Return EngineTexture.SseDefNormalMap    ' 0x141523F00..F32
            Case MaterialSlot.Glow : Return EngineTexture.SseDefaultWhiteMap                         ' Glowmap vf10 -> [0x143336418]
            Case MaterialSlot.Lightmask : Return EngineTexture.SseDefNormalMap                       ' +0x60 0x141523F36..F72
            Case MaterialSlot.Cube : Return EngineTexture.SseDefCubeMap                              ' Envmap vf10
            Case MaterialSlot.EnvMask
                ' MultiLayerParallax vf10 0x1415287EC..806: +0xB0 -> DefNormalMap, so cb1[2].y = 1 and t5 is sampled (0x141548F6D..F7F).
                If c.MultiLayerParallaxClass Then Return EngineTexture.SseDefNormalMap
                Return EngineTexture.NoTexture                                                       ' +0xA8 NULL: cb1[2].y = 0 (0x1415489BB)
            Case MaterialSlot.Height : Return EngineTexture.SseDefHeightMap                          ' Parallax vf10 0x1415260BA..D4: [0x143336428] = state+0x88 (0x14101DAF8)
            Case MaterialSlot.InnerLayer : Return EngineTexture.SseDefNormalMap                      ' MultiLayerParallax vf10 0x1415287AA..C4
            Case MaterialSlot.Slot7
                If GateMutant <> DefaultTextureMutant.SseSlot7SpecularFirst AndAlso c.BackLighting Then Return EngineTexture.SseDefNormalMap  ' ..F94
                If c.SpecularMsn Then Return EngineTexture.SseDefHeightMap                                                                  ' ..FBA
                Return EngineTexture.SseDefNormalMap
            Case Else : Return EngineTexture.NoTexture
        End Select
    End Function

    ''' <summary>Which Skyrim SE lighting slots the class samples: diffuse and normal always (0x141523F00..F32); slot 2 in the Glowmap
    ''' class (vf10) / with rim or soft (0x141523F36..F72, any class); cube and mask in the Envmap classes; slot 7 with back light or
    ''' SPECULAR &amp;&amp; MSN (0x141523F76..FBA). (Facegen is not described: C4 leaves its slots out.)</summary>
    Private Function SseLightingSamples(slot As MaterialSlot, c As SseLightingContext) As Boolean
        Select Case slot
            Case MaterialSlot.Diffuse, MaterialSlot.Normal : Return True
            Case MaterialSlot.Glow : Return c.GlowClass
            Case MaterialSlot.Lightmask : Return c.RimOrSoft AndAlso Not (GateMutant = DefaultTextureMutant.SseLightmaskGlowExcluded AndAlso c.GlowClass)
            Case MaterialSlot.Cube, MaterialSlot.EnvMask : Return c.EnvmapClass OrElse c.MultiLayerParallaxClass   ' MLP t4 / t5 0x141548F4B..F68
            Case MaterialSlot.Height : Return c.ParallaxClass                                    ' t3 0x141548A07..A16
            Case MaterialSlot.InnerLayer : Return c.MultiLayerParallaxClass                       ' t8 0x141548EF5..F04
            Case MaterialSlot.Slot7 : Return c.BackLighting OrElse c.SpecularMsn
            Case Else : Return False
        End Select
    End Function

    ''' <summary>Skyrim SE effect (ctor 0x1415560A0; SetupMaterial 0x141556C40).</summary>
    Friend Function SseEffect(slot As MaterialSlot, st As SlotState, c As EffectContext) As EngineTexture
        If st = SlotState.Loaded OrElse st = SlotState.NotLoadedByPreview Then Return EngineTexture.Own
        Select Case slot
            Case MaterialSlot.Diffuse
                If st = SlotState.Missing Then Return EngineTexture.SseDefNormalMap
                ' TEXTURE (bit 6 = path non-empty, 0x140EB12C0) off: t0 is not bound (0x141556DB5..DB7).
                If c.Recolor OrElse GateMutant = DefaultTextureMutant.EffectBaseEmptyAlwaysPrevious Then Return EngineTexture.PreviousDraw
                Return EngineTexture.NoTexture
            Case MaterialSlot.Greyscale
                If st = SlotState.Empty Then Return EngineTexture.NoTexture  ' bits 19 / 20 need the path (0x141556E4F..E59)
                Return EngineTexture.SseDefNormalMap
            Case Else
                Return EngineTexture.NoTexture
        End Select
    End Function

    ''' <summary>The bytes (R, G, B, A, R8G8B8A8_UNORM) of a 2D built-in texture: the creator's dword, little-endian (FO4 0x14182C5B0,
    ''' SSE 0x14101D750); SSE DefCubeMap: zeros (0x14101DDA0). Nothing for EyeCubeMap (a file) and for Own / NoTexture / PreviousDraw /
    ''' NotTraced.</summary>
    Friend Function Rgba(t As EngineTexture) As Byte()
        Select Case t
            Case EngineTexture.Fo4DiffuseMap
                If GateMutant = DefaultTextureMutant.Fo4DiffuseMapMagenta Then Return {255, 0, 255, 255}
                Return {128, 0, 255, 255}                                   ' 0xFFFF0080 (0x14182CBD1)
            Case EngineTexture.Fo4NormalMap : Return {128, 128, 255, 255}   ' 0xFFFF8080 (0x14182CB07)
            Case EngineTexture.Fo4White : Return {255, 255, 255, 255}       ' 0xFFFFFFFF (0x14182CD65)
            Case EngineTexture.SseDefNormalMap : Return {128, 128, 255, 255} ' 0xFFFF8080 (0x14101D9B0)
            Case EngineTexture.SseDefHeightMap : Return {0, 0, 0, 255}      ' 0xFF000000 (0x14101DA80)
            Case EngineTexture.SseDefaultWhiteMap : Return {255, 255, 255, 255} ' 0xFFFFFFFF (0x14101DC37)
            Case EngineTexture.SseDefCubeMap : Return {0, 0, 0, 0}          ' zero data (0x14101DDA0..DDA7)
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
    ''' comes only from the MultiLayerParallax class (SseLighting: the Envmap class gives NoTexture).</summary>
    Friend Const SseMlpEnvMaskPolicy As Boolean = True

    ''' <summary>C4 L8: is <paramref name="t"/> the identity of <paramref name="slot"/>'s role? Multiplicative slots (diffuse, glow,
    ''' masks) -> white; normal -> flat (128, 128, 255, 255); cube -> zeros (no reflection); the palette has no identity; the FACE t8 role
    ''' is not traced (never neutral). Derived from Rgba, except EyeCubeMap (<see cref="Fo4EyeCubeMapPolicy"/>) and an empty
    ''' MultiLayerParallax mask (<see cref="SseMlpEnvMaskPolicy"/>).</summary>
    Friend Function IsNeutral(slot As MaterialSlot, t As EngineTexture) As Boolean
        If slot = MaterialSlot.EnvMask AndAlso t = EngineTexture.SseDefNormalMap Then Return SseMlpEnvMaskPolicy AndAlso GateMutant <> DefaultTextureMutant.MlpEnvMaskNotNeutral
        If t = EngineTexture.Fo4EyeCubeMap Then Return Fo4EyeCubeMapPolicy AndAlso GateMutant <> DefaultTextureMutant.EyeCubeNotNeutral
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
