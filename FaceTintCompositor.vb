Imports OpenTK.Graphics.OpenGL4
Imports OpenTK.Mathematics

' ============================================================================
' FaceTintCompositor - compositor GPU (ping-pong) que hornea las capas de tint del NPC sobre una copia del
' diffuse de la cara.
'
' El caller pasa el texture-id del diffuse + dimensiones + las capas en orden de aplicacion; se alocan dos
' texturas de ping-pong, la iteracion 0 lee del diffuse original y cada iteracion N lee de ping(N-1) y escribe
' en ping(N). Al final se devuelve la ultima escrita y el caller la bindea (normalmente mutando la entrada del
' Textures_Dictionary del modelo).
'
' Cada capa lleva su blend op y su "kind": PaletteMask (mascara greyscale en .r, el tinte viene de uColor) o
' TextureSetDiffuse (RGBA ya coloreada, .a es cobertura).
'
' DEBE llamarse en el hilo GL, con el contexto actual. Devuelve 0 si falla.
' ============================================================================

Public Enum FaceTintLayerKind
    ''' <summary>Greyscale mask in red channel + uniform tint colour from TEND.
    ''' Used by Palette entries (lipcolor, eyeliner, blush, etc.).</summary>
    PaletteMask = 0
    ''' <summary>Pre-coloured RGBA detail texture. Coverage is taken from .a if alpha varies,
    ''' or from max(rgb) when alpha is degenerate (DXT1 / constant). The compositor decides
    ''' per-layer at decode time and substitutes the appropriate enum value.</summary>
    TextureSetDiffuse = 1
End Enum

Public Enum FaceTintChannel
    Diffuse = 0
    Normal = 1
    Specular = 2
End Enum

''' <summary>Single coverage convention applied to EVERY path (diffuse and N/S, all blend ops
''' and occlusion classes): the spatial mask is shaped by this transfer, then multiplied by
''' opacity (opacity stays OUTSIDE the transfer). Both options are named standard transfers, no
''' magic constants. SrgbOpacity (default) uses the IEC 61966-2-1 transfer; it is the verified
''' convention -- the same TTET[0] mask drives diffuse and N/S, and N/S matches CK at slope 1.000
''' only under sRGB. Linear is kept for A/B comparison. Applied identically by render and bake.</summary>
' Enum LEGACY: FaceTintConvention lo reemplaza y lo dice por escrito ("el enum legacy ... sólo tenía
' Linear/SrgbOpacity"). Única referencia en todo el árbol: esta declaración. Friend hasta que se borre.
Friend Enum FaceTintBlendConvention
    Linear = 0       ' coverage = mask * opacity (blend in stored/gamma space)
    SrgbOpacity = 1  ' coverage = sRGB(mask) * opacity (mask shaped by the sRGB transfer)
End Enum

''' <summary>Un swap de textura de cara por region, de un preset MPPT TXST de un Morph Group. Los presets tipo
''' "Arrugado" o "Curtido" cambian el diffuse/normal/spec base DENTRO de una region de la cara (frente, ojos,
''' nariz, orejas, mejillas, boca, cuello) definida por una mascara alpha en UV de cara: las texturas salen del
''' MPPT TXST del preset y la mascara del enum MPPK del grupo, que resuelve a un TintTemplateOption cuyo TTET[0]
''' es la mascara greyscale.
''' <para>Es un reemplazo duro gateado por la mascara: dentro del blanco el swap pisa la base, fuera se
''' conserva. Se aplica como pre-pase antes de las capas de tint, para que estas mezclen sobre la base ya
''' cambiada.</para></summary>
Public Class FaceRegionSwapInput
    ''' <summary>Region mask DDS bytes. Grayscale weight in .r (BC1 in vanilla, all three
    ''' channels are equal).</summary>
    Public Property RegionMaskDdsBytes As Byte()
    ''' <summary>Optional cache key (typically the normalized texture path) for the region mask.
    ''' When provided together with a <see cref="FaceTintTextureCache"/> on the compositor call,
    ''' the decoded GL texture is reused across calls instead of re-decoded every frame.</summary>
    Public Property RegionMaskCacheKey As String = Nothing
    ''' <summary>MPPT TXST.TX00 — replacement diffuse for the region. May be Nothing if the
    ''' TXST has no diffuse slot filled (then the diffuse channel is left untouched).</summary>
    Public Property SwapDiffuseDdsBytes As Byte()
    Public Property SwapDiffuseCacheKey As String = Nothing
    ''' <summary>MPPT TXST.TX01 — replacement normal for the region. Optional.</summary>
    Public Property SwapNormalDdsBytes As Byte()
    Public Property SwapNormalCacheKey As String = Nothing
    ''' <summary>MPPT TXST.TX07 — replacement smooth-spec for the region. Optional.</summary>
    Public Property SwapSpecularDdsBytes As Byte()
    Public Property SwapSpecularCacheKey As String = Nothing
    ''' <summary>Morph intensity (NPC MSDV value, 0..1) for this region preset. Scales how much
    ''' the variant texture blends in: effective coverage = regionMask.r * Intensity. The engine
    ''' applies face-region morphs proportionally to the slider, not as on/off. Default 1.0.</summary>
    Public Property Intensity As Single = 1.0F
    ''' <summary>Optional debug label written to the log when this swap runs.</summary>
    Public Property DebugName As String = ""

    ''' <summary>Get the swap DDS bytes for the requested channel. Returns Nothing if the
    ''' MPPT TXST does not contribute to that channel — the caller should skip the swap
    ''' for that channel and leave the base untouched.</summary>
    Public Function GetSwapBytes(channel As FaceTintChannel) As Byte()
        Select Case channel
            Case FaceTintChannel.Diffuse : Return SwapDiffuseDdsBytes
            Case FaceTintChannel.Normal : Return SwapNormalDdsBytes
            Case FaceTintChannel.Specular : Return SwapSpecularDdsBytes
            Case Else : Return Nothing
        End Select
    End Function

    ''' <summary>Companion to <see cref="GetSwapBytes"/>: returns the cache key authored alongside
    ''' the bytes for that channel. Nothing when the caller did not provide one.</summary>
    Public Function GetSwapCacheKey(channel As FaceTintChannel) As String
        Select Case channel
            Case FaceTintChannel.Diffuse : Return SwapDiffuseCacheKey
            Case FaceTintChannel.Normal : Return SwapNormalCacheKey
            Case FaceTintChannel.Specular : Return SwapSpecularCacheKey
            Case Else : Return Nothing
        End Select
    End Function
End Class

Public Class FaceTintLayerInput
    Public Property Kind As FaceTintLayerKind = FaceTintLayerKind.PaletteMask
    ''' <summary>For PaletteMask: greyscale mask in .r (the diffuse mask). For TextureSetDiffuse: pre-coloured RGBA detail.</summary>
    Public Property LayerDdsBytes As Byte()
    ''' <summary>Optional cache key for <see cref="LayerDdsBytes"/> (typically the normalized texture
    ''' path). Enables GL-texture reuse across compositor calls when a <see cref="FaceTintTextureCache"/>
    ''' is supplied to the compositor. Nothing disables caching for this layer.</summary>
    Public Property LayerCacheKey As String = Nothing
    ''' <summary>Textura GL YA SUBIDA para esta capa (canal Diffuse), en vez de <see cref="LayerDdsBytes"/>. 0 = sin usar
    ''' (el default: la capa se decodifica del DDS, como siempre).
    ''' POR QUÉ EXISTE: un DDS sólo puede transportar 8 bits por canal, y hay capas cuya fuente NO es un archivo sino un
    ''' buffer calculado (el facetint compuesto del pliegue SSE). Meterlo en un DDS lo CUANTIZA, y el fold lo amplifica
    ''' ×255/64 ⇒ el error de redondeo se multiplica por 4. MEDIDO: el pliegue GPU con transporte de 8 bits daba RMS
    ''' 2,4/255 y máx 18 contra el CPU (peor en las sombras, firma de cuantizar en LINEAL). Pasando la capa como textura
    ''' Rgba32f ya subida, el transporte deja de limitar la paridad. El caller es dueño de la textura (la libera él).
    ''' Tiene PRIORIDAD sobre LayerDdsBytes; el cache de texturas no se toca (no hay bytes que cachear).</summary>
    Public Property LayerTextureId As Integer = 0
    ' NO AGREGAR una "fuente ya decodificada" para la capa. Existió (`LayerUnitRgba`, fase 5) y se BORRÓ:
    ' nació para que SSE conservara su propio resample cuando el bilineal estaba escrito cuatro veces, y en
    ' cuanto la ley quedó UNA (BilinearAxis/BilinearMix) dejó de aportar nada — materializar el resample y
    ' muestrearlo por píxel dan los MISMOS BITS, y eso lo fija el self-test `bilinear`. La regla es la de
    ' Fallout: la capa viaja como BYTES y el compositor la decodifica y muestrea. Un segundo origen para el
    ' mismo dato es como vuelven las dos leyes.
    ''' <summary>TextureSet only — pre-coloured RGBA normal map (TTET[1]). Optional, may be empty.</summary>
    Public Property NormalDdsBytes As Byte()
    Public Property NormalCacheKey As String = Nothing
    ''' <summary>TextureSet only — pre-coloured RGBA specular map (TTET[2]). Optional, may be empty.</summary>
    Public Property SpecularDdsBytes As Byte()
    Public Property SpecularCacheKey As String = Nothing
    ''' <summary>PaletteMask only — uniform tint colour applied through the mask.</summary>
    Public Property R As Byte
    Public Property G As Byte
    Public Property B As Byte
    ''' <summary>0..1 intensity from TEND.Value / 100.</summary>
    Public Property Opacity As Single
    ''' <summary>0=Default 1=Multiply 2=Overlay 3=SoftLight 4=HardLight (BGSCharacterTint blendOp enum).</summary>
    Public Property BlendOp As Integer = 0
    ''' <summary>TTEF 0x0004 "Takes Skin Tone" — marks scar/
    ''' detail layers whose Normal and
    ''' Specular textures are full-face baked. The compositor applies these via the mask-gated hard
    ''' replace branch in the shader, using the layer's own TTET[0] alpha as the spatial mask.</summary>
    Public Property TakesSkinTone As Boolean = False

    ''' <summary>True for the slot-12 skin-tone Palette layer itself (the QNAM/TEND softlight that
    ''' tones the base skin). Classed with TakesSkinTone=True layers for the occlusion dispatch:
    ''' it is masked OUT of TakesSkinTone=False feature footprints (brows/tattoos) so it does not
    ''' light them.
    ''' <para>The dispatch is not a member: it is the <c>occlusionActive</c> branch inside
    ''' <see cref="ApplyFaceTintPipeline"/>. This used to point at a <c>TakesSkinToneOcclusion</c>
    ''' property that no longer exists anywhere in the tree.</para></summary>
    Public Property IsSkinTone As Boolean = False

    ''' <summary>Opt-in for the per-pixel grayscale-to-palette path on the Diffuse channel. When
    ''' True, the shader samples the per-fragment colour from a hair palette LUT instead of the
    ''' authored RGB. The X coordinate is the layer diffuse's green channel (<c>layerSample.g</c>)
    ''' for BOTH layer kinds (PaletteMask and TextureSet), mirroring the hair mesh shader's
    ''' <c>baseMap.g</c> grayscale-to-palette lookup. The Y coordinate is always
    ''' <see cref="HairPaletteRow"/> (= CLFM.RemappingIndex). Caller must supply
    ''' <see cref="HairLutDdsBytes"/>; missing LUT bytes silently fall through to the default
    ''' path. No-op on Normal/Specular channels.</summary>
    Public Property UseHairPalette As Boolean = False
    ''' <summary>Force the shader's TextureSet diffuse branch to use the uniform <c>uColor</c>
    ''' instead of the layer's authored RGB, while keeping coverage from the layer's diffuse
    ''' alpha. Used by the brow-tint override when the hair CLFM carries an RGB colour
    ''' (HasColor) -- the layer keeps its shape (alpha) but the colour comes from HCLF. Ignored
    ''' for PaletteMask layers (they already use uColor by default) and on N/S channels.</summary>
    Public Property ForceUniformColor As Boolean = False
    ''' <summary>skee overlay type-0: la src del TextureSet-diffuse = layerSample.rgb × uColor (tex × tint, tint
    ''' uniforme, NO horneado en la textura). Mutuamente excluyente con ForceUniformColor/LUT. Default off (FO4 inerte).</summary>
    Public Property MultiplyTextureByColor As Boolean = False

    ''' <summary>SSE fold de la cadena de albedo facegen. Ley del engine (rama <c>uFgTintFold</c> del shader):
    ''' <c>albedo = softlight(srgbToLin(complexion), TINT) × ((DETAIL + off) × amp)</c>, con la cobertura forzada a 1
    ''' (cara completa) y early-out fuera del composite de capas. Réplica exacta del pliegue CPU
    ''' (<c>SseFaceGenBaker.FoldFacetintIntoDiffuse</c>). Layer texture = el facetint _d (slot 6, el término del
    ''' SOFT-LIGHT); <see cref="FoldDetailTextureId"/> = el detail (slot 3, el término AMPLIFICADO); base =
    ''' complexion. Los nombres FgTint* quedaron por compatibilidad: el offset/amp se aplican al DETAIL, no al
    ''' tint. Default off (FO4 inerte).</summary>
    Public Property FgTintFold As Boolean = False
    ''' <summary>Con <see cref="FgTintFold"/>: en vez de PLEGAR la cadena, aplica su INVERSA (unfold). Deja en el
    ''' diffuse el valor que, despues de que el motor le aplique softlight(.,TINT) x amplify(DETAIL), devuelve
    ''' exactamente lo que entro. Hace falta porque los slots 3 y 6 ya NO se neutralizan. Espejo GPU de
    ''' <c>SseFaceGenBaker.PreCompensateEngineChain</c> (CPU) — si tocas una, toca la otra.</summary>
    Public Property FgTintUnfold As Boolean = False
    ''' <summary>Offset por canal del amplify del DETAIL (engine (1/255, 0, 1/255)). Solo si <see cref="FgTintFold"/>.</summary>
    Public Property FgTintOffR As Single = 0F
    Public Property FgTintOffG As Single = 0F
    Public Property FgTintOffB As Single = 0F
    ''' <summary>Amplitud del amplify del DETAIL (engine 255/64 = 3.984375). Solo si <see cref="FgTintFold"/>.</summary>
    Public Property FgTintAmp As Single = 1F
    ''' <summary>Textura GL del DETAIL (slot 3, el término AMPLIFICADO) del pliegue SSE. Solo si
    ''' <see cref="FgTintFold"/>. 0 = sin detail ⇒ el shader usa 0.251 (<c>BSShader_DefFacegenDetail</c>, el default
    ''' del engine ⇒ multiplicador (1.015625, 1.0, 1.015625)), igual que el fold CPU cuando
    ''' <c>detailRgba Is Nothing</c>.</summary>
    Public Property FoldDetailTextureId As Integer = 0

    ''' <summary>Canal de la máscara PaletteMask: 0=R 1=G 2=B 3=A. Default 1 (VERDE) = convención FO4 (la máscara
    ''' palette vive en el canal verde). SSE la usa en ROJO (0) — el builder SSE lo setea. Ignorado en TextureSet.</summary>
    Public Property PaletteMaskChannel As Integer = 1
    ''' <summary>Hair palette LUT DDS bytes (the same 2D texture the hair shader samples). Rows =
    ''' hair-tone gradients (highlight→shadow). Loaded into a GL texture by the compositor's batch
    ''' loader, sampled at <c>(layerSample.g, HairPaletteRow)</c> when <see cref="UseHairPalette"/> is True.</summary>
    Public Property HairLutDdsBytes As Byte()
    ''' <summary>Optional cache key for <see cref="HairLutDdsBytes"/> (typically the normalized
    ''' texture path). Same caching semantics as <see cref="LayerCacheKey"/>: when supplied
    ''' together with a <see cref="FaceTintTextureCache"/> on the compositor call, the decoded
    ''' GL texture is reused across calls.</summary>
    Public Property HairLutCacheKey As String = Nothing
    ''' <summary>0..1 V coordinate into the LUT (= CLFM.RemappingIndex). Picks the tone row whose
    ''' horizontal gradient becomes the per-pixel colour samples when <see cref="UseHairPalette"/>
    ''' is True. Ignored otherwise.</summary>
    Public Property HairPaletteRow As Single = 0F

    ''' <summary>Optional debug label written to the log when this layer is applied.</summary>
    Public Property DebugName As String = ""

    ''' <summary>RACE TintTemplateOption.Slot (12 = SkinTone). Usado por el compositor para resolver
    ''' la convención de composición (ws/maskconv/framework) vía FaceTintConvention.ResolveConvention.
    ''' Lo setea el builder desde la Option del RACE. Default 0xFFFF = desconocido (cae a Linear).</summary>
    Public Property Slot As UShort = &HFFFFUS

    ''' <summary>True si la Option del RACE es TextureSet (disc=2); False si Palette/Mask (disc=1).
    ''' Redundante con Kind pero explícito para el resolver de convención. Lo setea el builder.</summary>
    Public Property IsTextureSet As Boolean = False

    ''' <summary>Get the DDS bytes for the requested channel. Returns Nothing if the layer doesn't
    ''' contribute to that channel (Palette layers only contribute to Diffuse; TextureSet layers
    ''' may have any subset of Diffuse / Normal / Specular depending on which TTET slots are filled).</summary>
    Public Function GetChannelBytes(channel As FaceTintChannel) As Byte()
        If Kind = FaceTintLayerKind.PaletteMask Then
            ' Palette tints only modify the diffuse — they have no normal/specular content.
            If channel = FaceTintChannel.Diffuse Then Return LayerDdsBytes
            Return Nothing
        End If
        Select Case channel
            Case FaceTintChannel.Diffuse : Return LayerDdsBytes
            Case FaceTintChannel.Normal : Return NormalDdsBytes
            Case FaceTintChannel.Specular : Return SpecularDdsBytes
            Case Else : Return Nothing
        End Select
    End Function

    ''' <summary>Companion to <see cref="GetChannelBytes"/>: returns the cache key authored
    ''' alongside the bytes for that channel. Nothing when the caller did not provide one.</summary>
    Public Function GetChannelCacheKey(channel As FaceTintChannel) As String
        If Kind = FaceTintLayerKind.PaletteMask Then
            If channel = FaceTintChannel.Diffuse Then Return LayerCacheKey
            Return Nothing
        End If
        Select Case channel
            Case FaceTintChannel.Diffuse : Return LayerCacheKey
            Case FaceTintChannel.Normal : Return NormalCacheKey
            Case FaceTintChannel.Specular : Return SpecularCacheKey
            Case Else : Return Nothing
        End Select
    End Function
End Class

''' <summary>Per-GL-context state for the FaceTintCompositor: shader programs, fullscreen
''' quad VAO/VBO, and uniform locations. GL handles are per-context (NOT shared across
''' GLControls / contexts), so each owning host (e.g. <c>NpcRenderHost</c>) must hold its
''' own instance and pass it to every compositor call. Caller MUST invoke <see cref="Dispose"/>
''' from the GL thread with the owning context current before context teardown — otherwise
''' the GL handles leak.</summary>
Public NotInheritable Class FaceTintCompositorState
    ' Tint compositor program + fullscreen quad VAO/VBO. Created lazily by EnsureCompositorInitialized.
    Friend _program As Integer = 0
    Friend _uPrevLoc As Integer = -1
    Friend _uLayerLoc As Integer = -1
    Friend _uBaseLoc As Integer = -1
    Friend _uLayerDiffuseAlphaLoc As Integer = -1
    Friend _uHasDiffuseMaskLoc As Integer = -1
    Friend _uColorLoc As Integer = -1
    Friend _uOpacityLoc As Integer = -1
    Friend _uBlendOpLoc As Integer = -1
    Friend _uLayerKindLoc As Integer = -1
    Friend _uChannelLoc As Integer = -1
    Friend _uHairLutLoc As Integer = -1
    Friend _uPaletteRowLoc As Integer = -1
    Friend _uUseHairPaletteLoc As Integer = -1
    Friend _uForceOpaqueAlphaLoc As Integer = -1
    Friend _uForceUniformColorLoc As Integer = -1
    Friend _uTexTimesColorLoc As Integer = -1
    Friend _uFgTintFoldLoc As Integer = -1
    Friend _uFgTintOffLoc As Integer = -1
    Friend _uFgTintAmpLoc As Integer = -1
    Friend _uFoldDetailLoc As Integer = -1
    Friend _uHasFoldDetailLoc As Integer = -1
    Friend _uPaletteMaskChannelLoc As Integer = -1
    Friend _uWorkingSpaceLoc As Integer = -1
    Friend _uSrcSpaceLoc As Integer = -1
    Friend _uOutputSpaceLoc As Integer = -1
    ' Espacio del ACUMULADOR (ver FaceTintConventionSet.AccumSpace). Con el default vale lo mismo que
    ' _uOutputSpaceLoc, asi que el render queda BYTE-IDENTICO mientras no se cambie el config.
    Friend _uAccumSpaceLoc As Integer = -1

    Friend _uCompositeSpaceLoc As Integer = -1
    Friend _uMaskConvFullLoc As Integer = -1
    Friend _uModeLoc As Integer = -1
    Friend _uSoftLightLoc As Integer = -1
    Friend _uFrameworkLoc As Integer = -1
    ' Pre-tono TakesSkinTone (flagged-after-skintone). Default inerte (uPreToneSkin=0).
    Friend _uPreToneSkinLoc As Integer = -1
    Friend _uSkinMaskLoc As Integer = -1
    Friend _uSkinColorLoc As Integer = -1
    Friend _uSkinOpacityLoc As Integer = -1
    Friend _uSkinWsLoc As Integer = -1
    Friend _uSkinCsLoc As Integer = -1
    Friend _uSkinSsLoc As Integer = -1
    Friend _uSkinOsLoc As Integer = -1
    Friend _uSkinBopLoc As Integer = -1
    Friend _uSkinSlLoc As Integer = -1
    Friend _uSkinMcLoc As Integer = -1
    Friend _uSkinMaskChLoc As Integer = -1
    Friend _uTargetSizeLoc As Integer = -1
    Friend _uDownsizeFromMip0Loc As Integer = -1
    Friend _quadVao As Integer = 0
    Friend _quadVbo As Integer = 0


    ' Persistent ping-pong colour attachments shared by ComposeOntoFaceTexture
    ' and ApplyRegionSwapsOntoFaceTexture. Allocated lazily
    ' to (_pingW, _pingH); reused across calls when dims match, re-allocated when dims change.
    ' The "result snapshot" is a fresh texture per call — it carries the final pass output to
    ' the caller, who owns its lifetime. Pings stay private to the state and are only released
    ' on Dispose() or on dim-mismatch re-alloc.
    Friend _pingTex(1) As Integer
    Friend _pingFbo(1) As Integer
    Friend _pingW As Integer = 0
    Friend _pingH As Integer = 0

    ' Persistent SCRATCH result FBO container, reused across calls (same rationale as the pings:
    ' avoid GenFramebuffer/DeleteFramebuffer churn per compose call). The per-call result TEXTURE
    ' is still freshly allocated and caller-owned; only this FBO container is reused — each call
    ' attaches its fresh result texture to this FBO via GL.FramebufferTexture2D and re-checks
    ' completeness. Allocated lazily on first use; released in Dispose().
    Friend _scratchResultFbo As Integer = 0

    ''' <summary>Release all GL handles owned by this state. Caller MUST invoke from the GL
    ''' thread with the owning context current. Idempotent — safe to call when handles are 0.</summary>
    Public Sub Dispose()
        If _program <> 0 Then
            Try : GL.DeleteProgram(_program) : Catch : End Try
            _program = 0
        End If
        If _quadVao <> 0 Then
            Try : GL.DeleteVertexArray(_quadVao) : Catch : End Try
            _quadVao = 0
        End If
        If _quadVbo <> 0 Then
            Try : GL.DeleteBuffer(_quadVbo) : Catch : End Try
            _quadVbo = 0
        End If
        If _scratchResultFbo <> 0 Then
            Try : GL.DeleteFramebuffer(_scratchResultFbo) : Catch : End Try
            _scratchResultFbo = 0
        End If
        ReleasePingPongInternal()
    End Sub

    ''' <summary>Free the cached ping-pong textures + FBOs. Idempotent. Used by Dispose and by
    ''' the compose path when the requested width/height change between calls.</summary>
    Friend Sub ReleasePingPongInternal()
        For i As Integer = 0 To 1
            If _pingFbo(i) <> 0 Then
                Try : GL.DeleteFramebuffer(_pingFbo(i)) : Catch : End Try
                _pingFbo(i) = 0
            End If
            If _pingTex(i) <> 0 Then
                Try : GL.DeleteTexture(_pingTex(i)) : Catch : End Try
                _pingTex(i) = 0
            End If
        Next
        _pingW = 0
        _pingH = 0
    End Sub
End Class

Public Module FaceTintCompositor

    ''' <summary>Setter para que la app empuje el valor persistido (NPC_Config.UseHardwareBcDecode). La
    ''' variable de entorno <c>FGBAKE_GL_DECODE_HW</c> tiene PRIORIDAD sobre el config: existe para medir el
    ''' A/B de paridad sin tocar el archivo del usuario ni recompilar.</summary>
    Public Sub SetGlDecodeUseCompress(value As Boolean)
        Dim ov = If(Environment.GetEnvironmentVariable("FGBAKE_GL_DECODE_HW"), "").Trim()
        If ov = "0" OrElse ov = "1" Then Return   ' el entorno manda: no lo pisa el config
        _glDecodeUseCompress = value
    End Sub

    Public ReadOnly Property GlDecodeUseCompress As Boolean
        Get
            If _glDecodeUseCompress Is Nothing Then
                Dim ov = If(Environment.GetEnvironmentVariable("FGBAKE_GL_DECODE_HW"), "").Trim()
                _glDecodeUseCompress = (ov <> "0")   ' default ON; solo un "0" explicito lo apaga
            End If
            Return _glDecodeUseCompress.Value
        End Get
    End Property
    Private _glDecodeUseCompress As Boolean? = Nothing


    ' === TGA writers (output final + CLI --dump/_3). La instrumentacion de dump/diff (per-layer
    ' readback GL.GetTexImage, mask/intermediate dump) fue REMOVIDA de la libreria 2026-06-06: los
    ' dumps viven en el CLI (FO4_FaceTint_CLI --dump). El render GL ya no hace readbacks de debug. ===

    ''' <summary>TEMP DEBUG: write a BGRA buffer as an uncompressed 32-bit TGA (top-left origin,
    ''' matching CK's FaceGen TGA layout). Alpha PRESERVED so the mask channel can be inspected.
    ''' Public so the FaceGen bake (NPC_Manager) can also dump its final composited D/N/S buffers
    ''' alongside the _2.dds outputs in DebugMode.</summary>
    Public Sub WriteBgraToTga(path As String, bgra As Byte(), w As Integer, h As Integer)
        Dim hdr(17) As Byte
        hdr(2) = 2                                  ' uncompressed true-color
        hdr(12) = CByte(w And &HFF) : hdr(13) = CByte((w >> 8) And &HFF)
        hdr(14) = CByte(h And &HFF) : hdr(15) = CByte((h >> 8) And &HFF)
        hdr(16) = 32                                ' bpp (BGRA, alpha preserved)
        hdr(17) = &H28                              ' top-left origin (0x20) + 8 alpha bits (0x08)
        Using fs = System.IO.File.Create(path)
            fs.Write(hdr, 0, 18) : fs.Write(bgra, 0, w * h * 4)
        End Using
    End Sub


    ''' <summary>PRISTINE dumper (single source of truth) — recibe SOLO dos paths: el de la textura source
    ''' y el de salida. Re-lee el DDS fresco del FilesDictionary, lo CPU-decodifica (BCn → uncompressed
    ''' RGBA) por el wrapper DirectXTex (<c>Loader.LoadTextures(useCompress:=False, forceOpenGL:=False)</c>,
    ''' IsCompressedGL=False) — NO GPU — swap RGBA→BGRA, escribe TGA uncompressed a <paramref name="outPath"/>.
    ''' Byte-identico a texconv/CK (Tools/PristineDumpProbe: max 0). Maneja 4-canales (BC1/3/7→RGBA/BGRA),
    ''' 2-canales (BC5→R8G8: normal/spec, B=0) y 1-canal (BC4→gray); BC6H/16-bit se loguean y saltean. Public para que el bake
    ''' (NPC_Manager) dumpee BASEIN con el mismo path.</summary>
    Public Sub WritePristineTga(sourceTexturePath As String, outPath As String)
        If String.IsNullOrEmpty(sourceTexturePath) OrElse String.IsNullOrEmpty(outPath) Then Return
        Dim ddsBytes As Byte() = Nothing
        Try : ddsBytes = FilesDictionary_class.GetBytes(sourceTexturePath) : Catch : Return : End Try
        If ddsBytes Is Nothing OrElse ddsBytes.Length = 0 Then Return
        Try
            Dim loaded = DirectXTexWrapperCLI.Loader.LoadTextures(New Byte()() {ddsBytes}, useCompress:=False, forceOpenGL:=False)
            If loaded Is Nothing OrElse loaded.Count = 0 OrElse loaded(0) Is Nothing OrElse Not loaded(0).Loaded Then Return
            Dim tex = loaded(0)
            If tex.Levels Is Nothing OrElse tex.Levels.Count = 0 OrElse tex.Levels(0) Is Nothing Then Return
            Dim lvl = tex.Levels(0)
            Dim w = lvl.Width, h = lvl.Height
            Dim px = lvl.Data
            Dim fmt = tex.DxgiCodeFinal
            ' La tabla de formatos y el desempaquetado viven UNA sola vez, en FaceTintCpuCompositor:
            ' acá estaban transcriptos, con el mismo `Select Case` por píxel adentro del loop y encima
            ' SERIAL. Es la misma ley con la salida en BGRA en vez de RGBA.
            Dim bpp As Integer = FaceTintCpuCompositor.CanalesDelFormatoDecodificado(fmt)
            If w <= 0 OrElse h <= 0 OrElse px Is Nothing OrElse bpp = 0 OrElse px.Length < w * h * bpp Then
                Dim p = outPath, dx = fmt, nb = If(px Is Nothing, 0, px.Length)
                Logger.LogLazy(Function() $"[PRISTINE-DUMP] '{System.IO.Path.GetFileName(p)}' formato no soportado (DxgiFinal={dx}, bytes={nb}) -> skip")
                Return
            End If
            Dim bgra(w * h * 4 - 1) As Byte
            FaceTintCpuCompositor.EmpaquetarPixeles(px, bgra, w * h, bpp,
                                                    FaceTintCpuCompositor.EsBgra8(fmt), salidaEsBgra:=True)
            WriteBgraToTga(outPath, bgra, w, h)
        Catch ex As Exception
            Dim p = outPath, msg = ex.Message
            Logger.LogLazy(Function() $"[PRISTINE-DUMP] '{System.IO.Path.GetFileName(p)}' fail: {msg}")
        End Try
    End Sub


    ''' <summary>GUARD: los dos shaders tienen que ser ASCII PURO. Devuelve "" si lo son, o el detalle
    ''' del primer caracter que no lo sea.
    ''' <para><b>Por qué existe.</b> Un no-ASCII adentro del string GLSL —una flecha, un guion largo, una
    ''' vocal acentuada en un COMENTARIO— hace que el shader no compile. Y ese fallo es MUDO en producción:
    ''' <see cref="EnsureCompositorInitialized"/> reporta el error de compilación por <c>Logger.LogLazy</c>,
    ''' que está APAGADO en Release, y el bake sigue andando porque el bake es CPU. El síntoma es que el
    ''' compositor GL —el que usa el RENDER— deja de dibujar.</para>
    ''' <para>Y NINGÚN barrido de bytes lo ve: el A/B de corpus corre con <c>FGBAKE_GPU_PARITY=0</c>, o sea
    ''' con el camino GL apagado. Pasó de verdad (2026-07-31) y costó una corrida entera de paridad: el gate
    ''' de bytes daba PASS con el shader roto. Por eso el chequeo va acá, donde corre SIEMPRE y sin GL.</para>
    ''' <para>La regla "GLSL ASCII puro" ya estaba escrita; lo que faltaba era algo que la hiciera cumplir.</para></summary>
    ''' <summary>Todos los shaders GLSL que el gate tiene que revisar. NO sólo los dos de este compositor:
    ''' el fallo es igual de mudo en los del RENDER, y cubrir dos de ocho daba una falsa sensación de gate.
    ''' <para>Los dos del <c>TextOverlayRenderer</c> (Render.vb) YA ESTAN: eran variables locales dentro
    ''' de un <c>Private Sub</c> —invisibles tanto para esta lista como para el barrido por reflexion del
    ''' gate, porque una local no es un campo— y se izaron a <c>Friend Const</c> el 2026-08-12. Lo destapo
    ''' un revisor: quedaba un shader de produccion que NINGUN gate cubria.</para></summary>
    ''' <remarks>`Friend` y no `Private` para que el gate de BUILD (Tools/ParityGate, `glsl-ascii`) pueda
    ''' barrer los ocho fuentes. El gate SALIO de esta lib el 2026-08-08: es lexico sobre strings constantes,
    ''' da lo mismo en toda maquina ⇒ no tiene nada que hacer corriendo en el proceso del usuario.</remarks>
    Friend Function AllShaderSources() As (Name As String, Text As String)()
        Return New (Name As String, Text As String)() {
            ("FACETINT-VERTEX", VertexShaderSource),
            ("FACETINT-FRAGMENT", FragmentShaderSource),
            ("RENDER-FO4-VERTEX", Shader_Class_Fo4.Vertex_FO4),
            ("RENDER-FO4-FRAGMENT", Shader_Class_Fo4.Fragment_FO4),
            ("RENDER-FLOOR-VERTEX", Floor_Shader_Class.Vertex_Floor),
            ("RENDER-FLOOR-FRAGMENT", Floor_Shader_Class.Fragment_Floor),
            ("RENDER-SSE-VERTEX", Shader_Class_SSE.Vertex_SSE),
            ("RENDER-SSE-FRAGMENT", Shader_Class_SSE.Fragment_SSE),
            ("SHADOW-DEPTH-FRAGMENT", ShadowDepthShaderSource.Fragment_ShadowDepth),
            ("SHADOW-UNIFORMS", ShadowDepthShaderSource.SharedUniformsGlsl),
            ("SHADOW-LOOKUP", ShadowDepthShaderSource.SharedLookupGlsl),
            ("GROUND-VERTEX", GroundShadowShaderSource.Vertex_Ground),
            ("GROUND-FRAGMENT", GroundShadowShaderSource.Fragment_Ground),
            ("OVERLAY-VERTEX", TextOverlayRenderer.VertexOverlaySrc),
            ("OVERLAY-FRAGMENT", TextOverlayRenderer.FragmentOverlaySrc)}
    End Function

    ' EL GATE `glsl-ascii` YA NO VIVE ACA. Se mudó a Tools/ParityGate (LawGates.ShaderSourceAsciiGate) el
    ' 2026-08-08: es léxico sobre strings constantes, o sea que da EXACTAMENTE lo mismo en toda máquina, y
    ' corría en el proceso del usuario en cada primer bake. Sigue siendo obligatorio antes de publicar —
    ' un no-ASCII deja el shader sin compilar y el fallo es MUDO en Release.

    Private Const VertexShaderSource As String = "#version 430
layout(location = 0) in vec2 aPos;
out vec2 vUV;
void main() {
    vUV = vec2((aPos.x + 1.0) * 0.5, (aPos.y + 1.0) * 0.5);
    gl_Position = vec4(aPos, 0.0, 1.0);
}"

    ' Photoshop / W3C SVG compositing formulas. dst = current accumulated face diffuse,
    ' src = the layer's effective colour for that pixel.
    '
    ' Alpha contract: input alpha is PRESERVED into the output (read once into prevRgba.a,
    ' written back on every fragColor). Blend operations are RGB-only by definition; touching
    ' alpha here would corrupt callers passing alpha-tested diffuses. The current callers
    ' (face diffuse with AlphaTest=False) make this a no-op visually, but the contract stays
    ' honest so future callers can reuse this shader on alpha-tested textures safely.
    Private Const FragmentShaderSource As String = "#version 430
in vec2 vUV;
out vec4 fragColor;

uniform sampler2D uPrev;
uniform sampler2D uLayer;
uniform sampler2D uBase;               // ORIGINAL unmodified face channel (before any layer); reference for the N/S additive blend
uniform sampler2D uLayerDiffuseAlpha;  // TTET[0] diffuse of the layer, used as spatial mask on N/S passes
uniform int uHasDiffuseMask;           // 1 when uLayerDiffuseAlpha is meaningful
uniform sampler2D uHairLut;            // Hair palette LUT (grayscale-to-palette) for PaletteMask layers that opt into uUseHairPalette. Unused on units that don't bind a real LUT.
uniform vec3 uColor;
uniform float uOpacity;
uniform int uBlendOp;
uniform int uLayerKind;
uniform int uChannel;     // 0=Diffuse 1=Normal 2=Specular
uniform int uUseHairPalette;  // 1 = sample uHairLut per-pixel instead of authored colour (Diffuse only)
uniform int uForceUniformColor;  // 1 = TextureSet diffuse uses uColor instead of layerSample.rgb (brow tint override path; ignored on PaletteMask)
uniform int uTexTimesColor;      // 1 = TextureSet diffuse uses layerSample.rgb * uColor (skee overlay type-0: texxtint, tint uniforme). Ignorado en PaletteMask/ForceUniform.
uniform int uFgTintFold;         // 1 = SSE fold de la cadena facegen: softlight(complexion, layerSample=TINT) * ((uFoldDetail + uFgTintOff) * uFgTintAmp). Default 0 = FO4 inerte.
uniform vec3 uFgTintOff;         // offset por canal del amplify del DETAIL (engine (1/255,0,1/255)). NO se aplica al tint.
uniform float uFgTintAmp;        // amplitud del amplify del DETAIL (engine 255/64).
uniform int uPaletteMaskChannel; // canal de la mascara PaletteMask: 0=R 1=G 2=B 3=A. Default 1 (verde, FO4); SSE=0 (rojo).
uniform float uPaletteRow;    // V coordinate into uHairLut when uUseHairPalette=1 (= CLFM.RemappingIndex)
uniform int uForceOpaqueAlpha; // 1 = write opaque alpha (1.0) on the FINAL drawn layer (last pass).
uniform int uWorkingSpace;     // 0=linear 1=srgb 2=g22. Espacio donde corre el blend.
uniform int uSrcSpace;         // 0=linear 1=srgb 2=g22. Espacio del color de la capa (D=srgb, N/S=linear).
uniform int uOutputSpace;      // 0=linear 1=srgb 2=g22. Espacio del acumulador/almacenamiento (D=g22, N/S=linear).
uniform int uCompositeSpace;   // 0=linear 1=srgb 2=g22. Espacio donde corre el COMPOSITE (lerp por cov). Ley gen3: blend en working, lerp en linear. ==uWorkingSpace reduce al modelo previo.
uniform int uAccumSpace;       // 0=linear 1=srgb 2=g22. Espacio en el que VIVE el acumulador durante el compose. Default = uOutputSpace (comportamiento previo). Ver FaceTintConventionSet.AccumSpace.
uniform int uMaskConvFull;     // mask conv: 0=raw 1=srgbEncode 2=srgbDecode 3=g22Encode 4=g22Decode
uniform int uMode;             // 0=tint (additive-over-base) ; 1=region swap (crossfade mix(prev,swap,mask.r*op))
uniform int uSoftLight;        // modelo de soft-light cuando uBlendOp==3: 0=W3C 1=GIMP 2=Illusions 3=pegtop
uniform int uFramework;        // composite: 0=OverPrev(default) 1=OverBase 2=AddBase 3=ModSrc. base = uBase
// Pre-tono TakesSkinTone (ASCII-only). Una capa flagged que compone DESPUES del skintone recibe el softlight
// del skintone sobre su SOURCE. uPreToneSkin=1 lo activa (0 = inerte, path byte-identico). TODA la conv del
// skintone llega EXPLICITA en uSkin* (color/op/mask + espacios + blendop/softlight/mask-conv/mask-channel),
// resuelta del record por el caller; el GL usa esos (no los de la capa) -> GL == CPU por construccion.
uniform int uPreToneSkin;      // 1 = pre-tonar el source con el skintone (solo flagged-after-skintone)
uniform sampler2D uSkinMask;   // mask del skintone
uniform sampler2D uFoldDetail; // SSE fold: detail (slot 3 -> t4), el termino AMPLIFICADO. Solo se lee con uFgTintFold==1.
uniform int uHasFoldDetail;    // 0 = sin detail -> 0.2509803922 (0.251 = BSShader_DefFacegenDetail), igual que el fold CPU.
uniform vec3 uSkinColor;       // color del skintone
uniform float uSkinOpacity;    // opacidad del skintone
uniform int uSkinWs;           // working space del skintone
uniform int uSkinCs;           // composite space del skintone
uniform int uSkinSs;           // src space del skintone
uniform int uSkinOs;           // output space del skintone
uniform int uSkinBop;          // blendop del skintone (ResolveConvention slot 12)
uniform int uSkinSl;           // softlight model del skintone
uniform int uSkinMc;           // mask conv del skintone
uniform int uSkinMaskCh;       // canal del mask del skintone: 1=.g (Palette) 3=.a (TextureSet)
uniform ivec2 uTargetSize;     // tamano del DESTINO de este pase (= viewport). Lo necesita fetchAt para
                               // decidir 1:1 vs resample; no se puede consultar desde el fragment shader.
uniform int uDownsizeFromMip0; // 1 = pickLod devuelve 0 siempre (opcion Downsize-from-mip-0 de CharGen
                               // Options). Espeja FaceTintCpuCompositor.DownsizeFromMip0: el MISMO valor
                               // tiene que llegar a los dos compositores o el par CPU/GPU se parte.
                               // OJO: este shader vive en un string de VB, aca NO pueden ir comillas dobles.

// SYNC: CPU/GPU compositor - transcripcion EXACTA de SampleChannelAt + SampleBilinear del CPU:
// indice directo si el tamano coincide, si no bilineal con texel = uv*size-0.5 y clamp a borde.
// vUV interpolado YA vale (x+0.5)/W en el centro del fragmento, o sea el mismo u,v que calcula el CPU.
//
// Se lee por texelFetch y NO por texture() a proposito: asi el resultado no depende de NINGUN estado de
// sampleo del objeto textura (filtro, wrap, nivel de mip, anisotropia). El compositor ESPECIFICA su
// filtro en vez de heredarlo del driver, que es lo que un espejo bit a bit de un resampler por software
// necesita. Ademas cierra por construccion una familia de divergencias que ya costaron mediciones: el
// GPU leyendo mip 1 donde el CPU lee 0, y los pesos cuantizados del bilineal de funcion fija.
// El parametro lod queda explicito porque es el punto de entrada para elegir un mip STORED distinto
// del 0 sin tocar estado de la textura. Ver 50-facetint-leyes-y-compositor.
// Espejo EXACTO de SelectLevelForTarget (FaceTintCpuCompositor): nivel con el tamano EXACTO del target si
// existe; si no, el mas CHICO de los que son >= target; si ninguno llega, el 0. Los niveles vienen del DDS
// ordenados grande->chico. Sin mips (acumulador, ping-pong) textureQueryLevels da 1 y esto devuelve 0.
// OJO: la ley vive en DOS idiomas pero es UNA sola. Si se toca alla, se toca aca. Es lo que hace que CPU
// y GPU lean el MISMO texel en vez de coincidir por casualidad.
int pickLod(sampler2D tex) {
    if (uDownsizeFromMip0 == 1) return 0;
    int n = textureQueryLevels(tex);
    int ge = -1;
    for (int i = 0; i < n; i++) {
        ivec2 s = textureSize(tex, i);
        if (s == uTargetSize) return i;
        if (s.x >= uTargetSize.x && s.y >= uTargetSize.y) ge = i;
    }
    return (ge >= 0) ? ge : 0;
}

vec4 fetchAt(sampler2D tex) {
    int lod = pickLod(tex);
    ivec2 ssz = textureSize(tex, lod);
    if (ssz == uTargetSize) {
        return texelFetch(tex, clamp(ivec2(gl_FragCoord.xy), ivec2(0), ssz - ivec2(1)), lod);
    }
    float fx = clamp(vUV.x, 0.0, 1.0) * float(ssz.x) - 0.5;
    float fy = clamp(vUV.y, 0.0, 1.0) * float(ssz.y) - 0.5;
    int ix = int(floor(fx));
    int iy = int(floor(fy));
    float tx = fx - float(ix);
    float ty = fy - float(iy);
    int x0 = clamp(ix,     0, ssz.x - 1);
    int x1 = clamp(ix + 1, 0, ssz.x - 1);
    int y0 = clamp(iy,     0, ssz.y - 1);
    int y1 = clamp(iy + 1, 0, ssz.y - 1);
    vec4 c00 = texelFetch(tex, ivec2(x0, y0), lod);
    vec4 c10 = texelFetch(tex, ivec2(x1, y0), lod);
    vec4 c01 = texelFetch(tex, ivec2(x0, y1), lod);
    vec4 c11 = texelFetch(tex, ivec2(x1, y1), lod);
    return c00 * (1.0 - tx) * (1.0 - ty) + c10 * tx * (1.0 - ty)
         + c01 * (1.0 - tx) * ty         + c11 * tx * ty;
}

vec3 blendDefault(vec3 d, vec3 s) { return s; }
vec3 blendMultiply(vec3 d, vec3 s) { return d * s; }
vec3 blendOverlay(vec3 d, vec3 s) {
    return mix(2.0 * d * s,
               1.0 - 2.0 * (1.0 - d) * (1.0 - s),
               step(0.5, d));
}
vec3 blendSoftLightW3C(vec3 d, vec3 s) {
    vec3 g = mix(((16.0*d - 12.0)*d + 4.0)*d, sqrt(clamp(d, 0.0, 1.0)), step(0.25, d));
    return mix(d - (1.0 - 2.0*s)*d*(1.0 - d), d + (2.0*s - 1.0)*(g - d), step(0.5, s));
}
vec3 blendSoftLightGimp(vec3 d, vec3 s) {            // GIMP/Photoshop
    d = clamp(d, 0.0, 1.0);
    return mix(2.0*d*s + d*d*(1.0 - 2.0*s), 2.0*d*(1.0 - s) + sqrt(d)*(2.0*s - 1.0), step(0.5, s));
}
vec3 blendSoftLightIllusions(vec3 d, vec3 s) {       // Illusions.hu  d^(2^(2(0.5-s)))
    return pow(max(d, vec3(1e-6)), pow(vec3(2.0), 2.0*(vec3(0.5) - s)));
}
vec3 blendSoftLightPegtop(vec3 d, vec3 s) {          // pegtop
    return (1.0 - 2.0*s)*d*d + 2.0*s*d;
}
// soft-light AGNOSTICO por modelo (= CPU BlendSoftLightModel; paridad CPU/GL). uSoftLight: 0=W3C 1=GIMP 2=Illusions 3=pegtop
vec3 blendSoftLightModel(vec3 d, vec3 s) {
    if (uSoftLight==1) return blendSoftLightGimp(d, s);
    if (uSoftLight==2) return blendSoftLightIllusions(d, s);
    if (uSoftLight==3) return blendSoftLightPegtop(d, s);
    return blendSoftLightW3C(d, s);
}
// ---- INVERSA del soft-light POR MODELO (= CPU BlendSoftLightModelInverse; paridad CPU/GL) ----
// Resuelve d dado (y, s). Las cuatro son ANALITICAS, ninguna itera:
//   pegtop  : k*d^2 + 2*s*d - y = 0  =>  d = (-s + sqrt(s*s + k*y))/k,  k = 1-2s. k->0 = identidad.
//   GIMP    : s<=0.5 ES pegtop; s>0.5 es cuadratica en t=sqrt(d): 2(1-s)t^2 + (2s-1)t - y = 0.
//   Illusion: p(1-s) = 1/p(s)  =>  la inversa ES el forward con el source reflejado.
//   W3C     : s<0.5 ES pegtop; s>=0.5 se parte POR EL VALOR de y (las ramas se tocan en d=0.25, donde
//             y0 = 0.25 + 0.25b): arriba, cuadratica en sqrt(d); abajo, CUBICA por Cardano
//             (u^3 + p*u + q = 0 con p = 1/(16b) > 0 => discriminante siempre positivo => UNA raiz real).
// ATENCION: `y` NO se acota (es un lineal que puede pasarse de 1 tras dividir por el amplify). `s` si.
// Sin ramas por lane: se calculan las dos y se elige con step/mix, igual que el espejo vectorial del CPU.
vec3 cbrtV3(vec3 v) { return sign(v) * pow(abs(v), vec3(1.0/3.0)); }
vec3 softLightInvPegtop(vec3 y, vec3 s) {
    vec3 k = vec3(1.0) - 2.0*s;
    vec3 isId = step(abs(k), vec3(0.000001));
    vec3 ksafe = mix(k, vec3(1.0), isId);
    vec3 xr = (-s + sqrt(max(s*s + k*y, vec3(0.0)))) / ksafe;
    return mix(xr, y, isId);
}
vec3 softLightInvGimp(vec3 y, vec3 s) {
    vec3 a = 2.0*(vec3(1.0) - s);
    vec3 b = 2.0*s - vec3(1.0);
    vec3 asafe = max(a, vec3(0.000001));
    vec3 t = (-b + sqrt(max(b*b + 4.0*a*y, vec3(0.0)))) / (2.0*asafe);
    vec3 hi = mix(t*t, y*y, step(a, vec3(0.000001)));
    return mix(hi, softLightInvPegtop(y, s), step(s, vec3(0.5)));
}
vec3 softLightInvIllusions(vec3 y, vec3 s) {
    return blendSoftLightIllusions(y, vec3(1.0) - s);
}
vec3 softLightInvW3C(vec3 y, vec3 s) {
    vec3 b = 2.0*s - vec3(1.0);
    vec3 y0 = vec3(0.25) + 0.25*b;
    vec3 a = vec3(1.0) - b;
    vec3 asafe = max(a, vec3(0.000001));
    vec3 t = (-b + sqrt(max(b*b + 4.0*a*y, vec3(0.0)))) / (2.0*asafe);
    vec3 hiSqrt = mix(t*t, y*y, step(a, vec3(0.000001)));
    vec3 bsafe = max(b, vec3(0.000001));
    vec3 p = vec3(1.0) / (16.0*bsafe);
    vec3 q = (b + vec3(1.0) - 4.0*y) / (64.0*bsafe);
    vec3 mq2 = -0.5*q;
    vec3 p3 = p / 3.0;
    vec3 delta = max(mq2*mq2 + p3*p3*p3, vec3(0.0));
    vec3 sq = sqrt(delta);
    vec3 hiCubic = cbrtV3(mq2 + sq) + cbrtV3(mq2 - sq) + vec3(0.25);
    vec3 hi = mix(hiCubic, hiSqrt, step(y0, y));
    hi = mix(hi, y, step(b, vec3(0.000001)));
    return mix(hi, softLightInvPegtop(y, s), step(s, vec3(0.5)));
}
vec3 softLightModelInverseSl(vec3 y, vec3 s, int sl) {
    vec3 sc = clamp(s, 0.0, 1.0);
    if (sl==1) return softLightInvGimp(y, sc);
    if (sl==2) return softLightInvIllusions(y, sc);
    if (sl==3) return softLightInvPegtop(y, sc);
    return softLightInvW3C(y, sc);
}
vec3 blendHardLight(vec3 d, vec3 s) { return blendOverlay(s, d); }
// Modos separables estandar adicionales (5..19). Transcripcion 1:1 del CPU (BlendDispatch1).
vec3 blendScreen(vec3 d, vec3 s){ return d + s - d*s; }
vec3 blendDarken(vec3 d, vec3 s){ return min(d, s); }
vec3 blendLighten(vec3 d, vec3 s){ return max(d, s); }
vec3 blendColorDodge(vec3 d, vec3 s){ return mix(min(vec3(1.0), d/max(vec3(1.0)-s, vec3(1e-6))), vec3(1.0), step(vec3(1.0), s)); }
vec3 blendColorBurn(vec3 d, vec3 s){ return mix(vec3(1.0)-min(vec3(1.0), (vec3(1.0)-d)/max(s, vec3(1e-6))), vec3(0.0), step(s, vec3(0.0))); }
vec3 blendDifference(vec3 d, vec3 s){ return abs(d - s); }
vec3 blendExclusion(vec3 d, vec3 s){ return d + s - 2.0*d*s; }
vec3 blendLinearDodge(vec3 d, vec3 s){ return min(vec3(1.0), d + s); }
vec3 blendLinearBurn(vec3 d, vec3 s){ return max(vec3(0.0), d + s - vec3(1.0)); }
vec3 blendSubtract(vec3 d, vec3 s){ return max(vec3(0.0), d - s); }
vec3 blendDivide(vec3 d, vec3 s){ return mix(min(vec3(1.0), d/max(s, vec3(1e-6))), vec3(1.0), step(s, vec3(0.0))); }
vec3 blendLinearLight(vec3 d, vec3 s){ return clamp(d + 2.0*s - vec3(1.0), 0.0, 1.0); }
vec3 blendVividLight(vec3 d, vec3 s){ return mix(blendColorBurn(d, 2.0*s), blendColorDodge(d, 2.0*(s-vec3(0.5))), step(vec3(0.5), s)); }
vec3 blendPinLight(vec3 d, vec3 s){ return mix(min(d, 2.0*s), max(d, 2.0*s-vec3(1.0)), step(vec3(0.5), s)); }
vec3 blendHardMix(vec3 d, vec3 s){ return step(vec3(1.0), d + s); }
// --- Modos NO SEPARABLES de skee/RaceMenu (20,21). Usan los 3 canales del DESTINO juntos, por eso no pueden
// pasar por el dispatch escalar per-canal (BlendDispatch1) como el resto. Transcripcion 1:1 del CPU
// (SseOverlayCompositor.ApplyOverlays: ramas Grayscale y ColorMode + RgbToHsv/HsvToRgb).
// NOTA sobre mod: en el CPU RgbToHsv el Mod 6 de la rama mx==r es un NO-OP (su argumento (g-b)/d cae en
// [-1,1], siempre menor que 6), y en HsvToRgb el argumento es positivo, donde VB Mod y GLSL mod coinciden.
// Por eso aca se usa mod() de GLSL sin desviarse de la formula del CPU.
vec3 rgb2hsvC(vec3 c){
    float mx = max(c.r, max(c.g, c.b));
    float mn = min(c.r, min(c.g, c.b));
    float dd = mx - mn;
    float h = 0.0;
    if (dd > 0.0000001) {
        if (mx == c.r)      h = (c.g - c.b) / dd;          // Mod 6 = identidad en [-1,1]
        else if (mx == c.g) h = (c.b - c.r) / dd + 2.0;
        else                h = (c.r - c.g) / dd + 4.0;
        h /= 6.0;
        if (h < 0.0) h += 1.0;
    }
    float sat = (mx <= 0.0) ? 0.0 : (dd / mx);
    return vec3(h, sat, mx);
}
vec3 hsv2rgbC(float h, float sat, float v){
    float r = clamp(abs(mod(h*6.0 + 0.0, 6.0) - 3.0) - 1.0, 0.0, 1.0);
    float g = clamp(abs(mod(h*6.0 + 4.0, 6.0) - 3.0) - 1.0, 0.0, 1.0);
    float b = clamp(abs(mod(h*6.0 + 2.0, 6.0) - 3.0) - 1.0, 0.0, 1.0);
    return vec3(v*(1.0 + sat*(r-1.0)), v*(1.0 + sat*(g-1.0)), v*(1.0 + sat*(b-1.0)));
}
// Grayscale: luminancia del DESTINO (pesos 0.299/0.587/0.114) escalando el color de la capa.
vec3 blendGrayscale(vec3 d, vec3 s){
    float lum = 0.299*d.r + 0.587*d.g + 0.114*d.b;
    return lum * s;
}
// ColorMode: H y S de la CAPA, V del DESTINO (V = max de los 3 canales del destino).
vec3 blendColorMode(vec3 d, vec3 s){
    vec3 hsvS = rgb2hsvC(s);
    float vDst = max(d.r, max(d.g, d.b));
    return hsv2rgbC(hsvS.x, hsvS.y, vDst);
}
// Identidad del blend (para ModSrc: mix(neutral,src,cov)). = CPU BlendNeutral1.
float blendNeutral(int bop){
    if (bop==1 || bop==6 || bop==9 || bop==13 || bop==15) return 1.0;
    if (bop==2 || bop==3 || bop==4 || bop==16 || bop==17 || bop==18) return 0.5;
    return 0.0;
}

// sRGB transfer (IEC 61966-2-1) for the coverage convention. Standard, not magic.
float linearToSrgb1(float c) {
    c = clamp(c, 0.0, 1.0);
    return (c <= 0.0031308) ? (c * 12.92) : (1.055 * pow(c, 1.0 / 2.4) - 0.055);
}
// ---- Derived-model helpers (parity with test_conventions.to_space). ASCII only. ----
float srgbToLin1(float c){ c=clamp(c,0.0,1.0); return (c<=0.04045)?(c/12.92):pow((c+0.055)/1.055,2.4); }
float g22ToLin1(float c){ return pow(clamp(c,0.0,1.0),2.2); }
float linToG22_1(float c){ return pow(clamp(c,0.0,1.0),1.0/2.2); }
float g24ToLin1(float c){ return pow(clamp(c,0.0,1.0),2.4); }
float linToG24_1(float c){ return pow(clamp(c,0.0,1.0),1.0/2.4); }
// sRGB stored value -> working space (ws: 0=linear 1=srgb 2=g22)
vec3 srgbToWS(vec3 v, int ws){
    if (ws==1) return v;
    vec3 lin = vec3(srgbToLin1(v.r), srgbToLin1(v.g), srgbToLin1(v.b));
    if (ws==0) return lin;
    return vec3(linToG22_1(lin.r), linToG22_1(lin.g), linToG22_1(lin.b));
}
// working space -> sRGB stored value
vec3 wsToSrgb(vec3 v, int ws){
    if (ws==1) return v;
    vec3 lin = (ws==2) ? vec3(g22ToLin1(v.r), g22ToLin1(v.g), g22ToLin1(v.b)) : v;
    return vec3(linearToSrgb1(lin.r), linearToSrgb1(lin.g), linearToSrgb1(lin.b));
}
// ---- Conversion generica entre espacios (0=linear 1=srgb 2=g22) via linear. Shader AGNOSTICO:
//      solo aplica los espacios que el resolver pone en los uniforms. ----
vec3 spaceToLin(vec3 v, int s){
    if (s==0) return v;
    if (s==1) return vec3(srgbToLin1(v.r), srgbToLin1(v.g), srgbToLin1(v.b));
    if (s==3) return vec3(g24ToLin1(v.r), g24ToLin1(v.g), g24ToLin1(v.b));
    return vec3(g22ToLin1(v.r), g22ToLin1(v.g), g22ToLin1(v.b));   // s=2
}
vec3 linToSpace(vec3 v, int s){
    if (s==0) return v;
    if (s==1) return vec3(linearToSrgb1(v.r), linearToSrgb1(v.g), linearToSrgb1(v.b));
    if (s==3) return vec3(linToG24_1(v.r), linToG24_1(v.g), linToG24_1(v.b));
    return vec3(linToG22_1(v.r), linToG22_1(v.g), linToG22_1(v.b));   // s=2
}
vec3 cvt(vec3 v, int fromS, int toS){
    if (fromS==toS) return v;
    return linToSpace(spaceToLin(v, fromS), toS);
}
// derived-model mask conv (0=raw 1=srgbEnc 2=srgbDec 3=g22Enc 4=g22Dec)
float convMaskFull(float m){
    if (uMaskConvFull==1) return linearToSrgb1(m);
    if (uMaskConvFull==2) return srgbToLin1(m);
    if (uMaskConvFull==3) return linToG22_1(m);
    if (uMaskConvFull==4) return g22ToLin1(m);
    if (uMaskConvFull==5) return linToG24_1(m);
    if (uMaskConvFull==6) return g24ToLin1(m);
    return m;
}
// derived-model blend dispatch (uBlendOp: 0=replace 1=mult 2=overlay 3=softlight 4=hardlight, 5..19 estandar,
// 20=grayscale 21=colormode -> los dos NO SEPARABLES de skee, ver arriba)
vec3 blendDispatch(vec3 d, vec3 s){
    if (uBlendOp==20) return blendGrayscale(d,s);
    if (uBlendOp==21) return blendColorMode(d,s);
    if (uBlendOp==1) return blendMultiply(d,s);
    if (uBlendOp==2) return blendOverlay(d,s);
    if (uBlendOp==3) return blendSoftLightModel(d,s);
    if (uBlendOp==4) return blendHardLight(d,s);
    if (uBlendOp==5) return blendScreen(d,s);
    if (uBlendOp==6) return blendDarken(d,s);
    if (uBlendOp==7) return blendLighten(d,s);
    if (uBlendOp==8) return blendColorDodge(d,s);
    if (uBlendOp==9) return blendColorBurn(d,s);
    if (uBlendOp==10) return blendDifference(d,s);
    if (uBlendOp==11) return blendExclusion(d,s);
    if (uBlendOp==12) return blendLinearDodge(d,s);
    if (uBlendOp==13) return blendLinearBurn(d,s);
    if (uBlendOp==14) return blendSubtract(d,s);
    if (uBlendOp==15) return blendDivide(d,s);
    if (uBlendOp==16) return blendLinearLight(d,s);
    if (uBlendOp==17) return blendVividLight(d,s);
    if (uBlendOp==18) return blendPinLight(d,s);
    if (uBlendOp==19) return blendHardMix(d,s);
    return blendDefault(d,s);
}
// Versiones PARAMETRIZADAS (= CPU ConvMask1 / BlendDispatch1). El pre-tono TakesSkinTone las usa con la
// conv del SKINTONE (uSkin*), NO con la de la capa: asi GL == CPU por construccion sin asumir que skintone
// y capa comparten mask-conv / blendop / softlight-model.
float convMaskMc(float m, int mc){
    if (mc==1) return linearToSrgb1(m);
    if (mc==2) return srgbToLin1(m);
    if (mc==3) return linToG22_1(m);
    if (mc==4) return g22ToLin1(m);
    if (mc==5) return linToG24_1(m);
    if (mc==6) return g24ToLin1(m);
    return m;
}
vec3 softLightModelSl(vec3 d, vec3 s, int sl){
    if (sl==1) return blendSoftLightGimp(d, s);
    if (sl==2) return blendSoftLightIllusions(d, s);
    if (sl==3) return blendSoftLightPegtop(d, s);
    return blendSoftLightW3C(d, s);
}
vec3 blendDispatchBop(vec3 d, vec3 s, int bop, int sl){
    if (bop==1) return blendMultiply(d,s);
    if (bop==2) return blendOverlay(d,s);
    if (bop==3) return softLightModelSl(d,s,sl);
    if (bop==4) return blendHardLight(d,s);
    if (bop==5) return blendScreen(d,s);
    if (bop==6) return blendDarken(d,s);
    if (bop==7) return blendLighten(d,s);
    if (bop==8) return blendColorDodge(d,s);
    if (bop==9) return blendColorBurn(d,s);
    if (bop==10) return blendDifference(d,s);
    if (bop==11) return blendExclusion(d,s);
    if (bop==12) return blendLinearDodge(d,s);
    if (bop==13) return blendLinearBurn(d,s);
    if (bop==14) return blendSubtract(d,s);
    if (bop==15) return blendDivide(d,s);
    if (bop==16) return blendLinearLight(d,s);
    if (bop==17) return blendVividLight(d,s);
    if (bop==18) return blendPinLight(d,s);
    if (bop==19) return blendHardMix(d,s);
    return blendDefault(d,s);
}
// Shader AGNOSTICO: compone CADA capa sobre el acumulador corriente (uPrev) aplicando las
// convenciones que llegan por uniforms (uWorkingSpace / uMaskConvFull / uBlendOp / uLayerKind).
// over-RUNNING: cada capa se compone sobre el resultado de las capas previas (no sobre un base
// original fijo). Asi N/S con replace reemplazan secuencialmente (last-wins) en vez de acumular
// deltas. Single-layer es identico a over-original (prev == base en la 1a capa). Parity con
// compose_py (Tools/FaceGenByteCompare) / FaceTintConvention.ResolveConvention.
//   cov     = convMaskFull(mask) * opacity
//   base_w  = cvt(prev -> uWorkingSpace)     (prev = acumulador corriente en uAccumSpace)
//   src_w   = cvt(src  -> uWorkingSpace)
//   blended = blendDispatch(base_w, src_w)   (blend en uWorkingSpace)
//   res_c   = cvt(prev->uCompositeSpace) + cov*(cvt(blended->uCompositeSpace) - cvt(prev->uCompositeSpace))
//   final   = cvt(res_c -> uAccumSpace)     (el resultado ES el nuevo prev)
// mask source: PaletteMask -> layer.G ; TextureSet D -> layer.a ; TextureSet N/S -> uLayerDiffuseAlpha.a
// src: PaletteMask -> uColor (o LUT) ; TextureSet -> layer.rgb (o LUT / uColor forzado)
void main() {
    vec4 prevRgba = fetchAt(uPrev);
    vec3 prev = prevRgba.rgb;
    vec4 layerSample = fetchAt(uLayer);

    // uMode==1: region swap = alpha-over mix(prev, swap, mask.r * intensity). Es composicion de
    // color por cobertura -> se hace en LINEAR. prev viene en uAccumSpace, swap en uSrcSpace;
    // se convierten a linear, se mezclan, y vuelve a uAccumSpace. mask RAW (.r).
    if (uMode == 1) {
        // Region swap = REPLACE resuelto por FaceTintConvention.ResolveConvention(forSwap) (NO hardcoded):
        // cov = convMask(mask, uMaskConvFull) * op ; compose generico (blend en uWorkingSpace, lerp en
        // uCompositeSpace, storage en uAccumSpace), blended=src (replace). = misma algebra que ComposeOne (CPU).
        // El override de convencion (incl. #If DEBUG full-linear) ahora alcanza tambien los swaps.
        float mask = fetchAt(uLayerDiffuseAlpha).r;
        float cov = clamp(uOpacity * convMaskFull(mask), 0.0, 1.0);
        vec3 src_w   = cvt(layerSample.rgb, uSrcSpace, uWorkingSpace);
        vec3 base_c  = cvt(prev, uAccumSpace, uCompositeSpace);
        vec3 blend_c = cvt(src_w, uWorkingSpace, uCompositeSpace);   // replace: blended = src_w
        vec3 res_c   = clamp(base_c + cov * (blend_c - base_c), 0.0, 1.0);
        fragColor = vec4(cvt(res_c, uCompositeSpace, uAccumSpace), prevRgba.a);
        return;
    }

    // uMode==2: CONVERT puro de espacio (sin blend, sin mask). Convierte la textura bindeada en uPrev
    // de uSrcSpace a uAccumSpace. Se usa para el SEED del path unico (source sRGB -> acumulador g22 en
    // D) y queda reservado para el camino inverso g22 -> sRGB (flag BakeMode, si el render lo necesita).
    if (uMode == 2) {
        fragColor = vec4(cvt(prev, uSrcSpace, uAccumSpace), prevRgba.a);
        return;
    }

    // uFgTintFold==1: PLIEGUE SSE = LEY FIJA DEL ENGINE (PS facegen de BSLightingShader, DXBC verificado byte a byte):
    //     albedo = softlight(srgbToLin(complexion), TINT) * ((DETAIL + uFgTintOff) * uFgTintAmp)
    //     softlight(a,b) = a*a + 2*a*b*(1-a)                   [pegtop]
    //     TINT   = el facetint  = texture-set slot 6 -> material+0xA0 -> PS t3  (llega en uLayer/layerSample)
    //     DETAIL = texture-set slot 3 -> material+0xA8 -> PS t4                 (llega en uFoldDetail)
    //     [engine: off=(1/255,0,1/255), amp=255/64]
    // CORREGIDO: antes esto estaba INVERTIDO (amplify sobre el facetint, softlight con el detail). El x255/64
    // normaliza el DETAIL (neutro 64 -> 1.0), NO el facetint; el facetint entra por soft-light igual que el skin
    // tint del cuerpo. Con el orden viejo un skin tone saturado aplastaba R/B (cuello mucho mas saturado).
    // RE: SetupMaterial 0x1414DC310 rama facegen 0x1414DC542; OnLoadTextureSet 0x1414BA6E0.
    // ES UNA REPLICA EXACTA DEL CPU (SseFaceGenBaker.FoldFacetintIntoDiffuse), y por eso hace early-out SIN pasar por
    // uWorkingSpace/uBlendOp/uFramework: esa convencion es la ley (configurable) del bake de FaceTint del CK, OTRA cosa.
    // Si el fold la heredara, cambiar una opcion de la UI lo desviaria del engine y el bake dejaria de matchear el juego.
    // prev = complexion en sRGB (el caller lo sube en Rgba32f: un DDS lo cuantizaria a 8 bits y el fgTint amplifica x4).
    // Salida en sRGB = exactamente lo que escribe el fold CPU.
    // uFgTintFold==2: INVERSA de la cadena (unfold). Espejo exacto de PreCompensateEngineChain (CPU).
    //   y = srgbToLin(prev) / amplify(DETAIL)
    //   softlight(x,b) = x*x*(1-2b) + 2bx = y  =>  x = (-b + sqrt(b*b + k*y)) / k,  k = 1-2b
    //   k -> 0 (b = 0.5) es la identidad: el limite es x = y (la formula daria 0/0).
    if (uFgTintFold == 2) {
        vec3 cs = clamp(prev, 0.0, 1.0);
        vec3 y  = vec3(srgbToLin1(cs.r), srgbToLin1(cs.g), srgbToLin1(cs.b));
        vec3 dt = (uHasFoldDetail == 1) ? fetchAt(uFoldDetail).rgb : vec3(0.2509803922);
        // SIN PISO (decision 1: el motor no acota, no hay _sat en ningun paso del desensamblado).
        // POLITICA DE DOMINIO, replicada LITERAL de SseFaceGenBaker.FgAmpInverse: con amp <= 0 la inversa
        // NO divide y devuelve el valor tal cual. Con amp = 0 la directa multiplico por 0 y destruyo la
        // informacion; ninguna politica la recupera, asi que la unica definida y no explosiva es la
        // identidad -- y es la MISMA en los dos lenguajes, a diferencia de dividir por 0 (+-Inf).
        vec3 fg = (dt + uFgTintOff) * uFgTintAmp;
        vec3 fgSafe = mix(vec3(1.0), fg, greaterThan(fg, vec3(0.0)));
        y = y / fgSafe;
        // EL DISPATCH COMPARTIDO POR MODELO, no la inversa de pegtop escrita a mano. Espejo de
        // SseFaceGenBaker.PreCompOne, que llama a BlendSoftLightModelInverse con el MISMO uSoftLight
        // (el caller resuelve la convencion con stage=Fold en los dos caminos).
        vec3 b = layerSample.rgb;
        vec3 x = clamp(softLightModelInverseSl(y, b, uSoftLight), 0.0, 1.0);
        vec3 outc = vec3(linearToSrgb1(x.r), linearToSrgb1(x.g), linearToSrgb1(x.b));
        fragColor = vec4(outc, prevRgba.a);
        return;
    }

    if (uFgTintFold == 1) {
        vec3 cs = clamp(prev, 0.0, 1.0);
        vec3 cl = vec3(srgbToLin1(cs.r), srgbToLin1(cs.g), srgbToLin1(cs.b));
        // Slot 3 vacio: el engine NO deja el amplify en identidad, usa su default BSShader_DefFacegenDetail
        // = 64/255 = 0.251 (RE byte-level SkyrimSE.exe init 0x140E57E30, fill 0x40404040 = vanilla
        // blankdetailmap) => multiplicador (1.015625, 1.0, 1.015625). DEBE ser el MISMO default que el CPU
        // (SseFaceGenBaker.EngineDefaultDetail) o el fold GPU se desvia del bake para NPCs sin detail
        // (caso Enhanced Khajiit, TX04 borrado).
        vec3 dt = (uHasFoldDetail == 1) ? fetchAt(uFoldDetail).rgb : vec3(0.2509803922);
        // EL DISPATCH COMPARTIDO (modelo 3), no una expresion propia: `softLightModelSl` con sl=3 ES la
        // forma del motor `d*d + 2*d*s*(1-d)` desde la decision 4. Escribirla aca de nuevo era la quinta
        // copia de la misma cuenta. Espejo de SseFaceGenBaker.FoldOne, que llama al MISMO dispatch.
        vec3 sl = softLightModelSl(cl, layerSample.rgb, uSoftLight);   // softlight(complexion_lin, TINT = facetint)
        // SIN PISO: la DIRECTA multiplica por el amp REAL (decision 1). Espejo de FgTintChannel.
        vec3 fg = (dt + uFgTintOff) * uFgTintAmp;   // amplify del DETAIL, sin acotar
        vec3 lin = sl * fg;
        vec3 outc = vec3(linearToSrgb1(lin.r), linearToSrgb1(lin.g), linearToSrgb1(lin.b));
        fragColor = vec4(outc, prevRgba.a);
        return;
    }

    // uMode==0: tint / body uniform = additive-over-base.
    // uLayerKind: 0=PaletteMask (src=uColor, mask=layer.g) ; 1=TextureSet (src=layer.rgb, mask=alpha) ;
    //             2=UniformColor (body skin: src=uColor, mask=1, base=prev via uBase).
    float maskV;
    vec3 srcColor;
    if (uLayerKind == 2) {
        srcColor = uColor;
        maskV = 1.0;
    } else if (uLayerKind == 1) {
        // brow grayscale->palette ENGINE-EXACT (BSFaceCustomizationShader PS, `ld` t4; PARIDAD con CPU
        // SampleLutEngine): mask sRGB->lin, U=pow(lin,1/2.2), texel=ftoi(U*W, row*H), texelFetch NEAREST
        // (sin bilineal ni half-texel). Verificado byte-exact vs CK.
        if (uUseHairPalette == 1) {
            ivec2 lsz1 = textureSize(uHairLut, 0);
            float lu1 = cvt(vec3(layerSample.g), uSrcSpace, uOutputSpace).x; // U = Cvt(green, conv.SrcSpace(=DiffuseTextureSrcSpace,Srgb), conv.OutputSpace(=G22)) = engine pow(srgbToLin(green),1/2.2). Sin hardcode; PARIDAD CPU Cvt1(green,ss,os).
            int ltx1 = clamp(int(lu1 * float(lsz1.x)), 0, lsz1.x - 1);
            int lty1 = clamp(int(uPaletteRow * float(lsz1.y)), 0, lsz1.y - 1);
            srcColor = texelFetch(uHairLut, ivec2(ltx1, lty1), 0).rgb;
        }
        else if (uForceUniformColor == 1) srcColor = uColor;
        else if (uTexTimesColor == 1)     srcColor = layerSample.rgb * uColor;   // skee type-0: tex x tint (tint uniforme)
        else                              srcColor = layerSample.rgb;
        // (uFgTintFold ya hizo early-out arriba: el pliegue SSE NO pasa por el composite de capas.)
        if (uChannel == 0) {
            maskV = layerSample.a;
        } else {
            maskV = (uHasDiffuseMask == 1) ? fetchAt(uLayerDiffuseAlpha).a
                                           : max(max(layerSample.r, layerSample.g), layerSample.b);
        }
    } else {
        if (uUseHairPalette == 1) {  // engine-exact (= rama uLayerKind==1 y CPU SampleLutEngine)
            ivec2 lsz2 = textureSize(uHairLut, 0);
            float lu2 = cvt(vec3(layerSample.g), uSrcSpace, uOutputSpace).x; // = Cvt(green, conv.SrcSpace, conv.OutputSpace); sin hardcode
            int ltx2 = clamp(int(lu2 * float(lsz2.x)), 0, lsz2.x - 1);
            int lty2 = clamp(int(uPaletteRow * float(lsz2.y)), 0, lsz2.y - 1);
            srcColor = texelFetch(uHairLut, ivec2(ltx2, lty2), 0).rgb;
        }
        else                      srcColor = uColor;
        // Canal de la mascara palette: verde por defecto (FO4), rojo en SSE (uPaletteMaskChannel).
        maskV = (uPaletteMaskChannel == 0) ? layerSample.r
              : (uPaletteMaskChannel == 2) ? layerSample.b
              : (uPaletteMaskChannel == 3) ? layerSample.a
              : layerSample.g;
    }

    // Pre-tono TakesSkinTone (guard uPreToneSkin): aplica el softlight del skintone al SOURCE de la capa
    // flagged con la coverage del skintone (mask .g) en este pixel, ANTES del composite normal. = el
    // ComposeOne(src, skinColor, skinCov, skinConv, softlight) del CPU. Inerte byte-identico si uPreToneSkin==0.
    if (uPreToneSkin == 1) {
        vec4 skMaskRgba = fetchAt(uSkinMask);   // una sola lectura: antes se sampleaba dos veces por el ternario
        float skMaskV = (uSkinMaskCh == 3) ? skMaskRgba.a : skMaskRgba.g;
        float skCov   = clamp(convMaskMc(skMaskV, uSkinMc) * uSkinOpacity, 0.0, 1.0);
        vec3 sk_bw  = cvt(srcColor, uSkinOs, uSkinWs);
        vec3 sk_sw  = cvt(uSkinColor, uSkinSs, uSkinWs);
        vec3 sk_bl  = blendDispatchBop(sk_bw, sk_sw, uSkinBop, uSkinSl);
        vec3 sk_bc  = cvt(srcColor, uSkinOs, uSkinCs);
        vec3 sk_blc = cvt(sk_bl, uSkinWs, uSkinCs);
        vec3 sk_rc  = clamp(sk_bc + skCov * (sk_blc - sk_bc), 0.0, 1.0);
        srcColor = cvt(sk_rc, uSkinCs, uSkinOs);
    }

    float cov = clamp(convMaskFull(maskV) * uOpacity, 0.0, 1.0);
    // COBERTURA CERO = IDENTIDAD. Una capa que no cubre este pixel no puede cambiarlo, asi que el
    // acumulador sale tal cual entro. ESTA RAMA ES OBLIGATORIA, no es una optimizacion del shader:
    // el CPU saltea igual (FaceTintCpuCompositor, loop de capas: early-out de bloque + `If cov > 0`), y
    // RENDER == BAKE exige que los dos compositores hagan LO MISMO. Si uno saltea y el otro compone,
    // divergen: no hoy (con uAccumSpace == uCompositeSpace componer con cov=0 ya es identidad) pero si
    // en cuanto alguien separe los espacios desde CharGen Options, que el codigo soporta.
    // Ademas ALINEA el tercer camino: SseFaceTintComposer.ComposeLayer ya salteaba con cov<=0 y este
    // shader (que es COMPARTIDO FO4/SSE) no, o sea que SSE estaba divergiendo del GPU. Ahora los tres
    // (CPU FO4, CPU SSE y GLSL) saltean con cobertura CERO.
    // OJO, MATIZ MEDIDO, no lo borres: con cov = NaN NO coinciden. Aca `!(cov > 0.0)` es TRUE => saltea, y
    // el CPU de SSE (SseFaceTintComposer, guard `a <= 0.0`) da FALSE => COMPONE. El CPU de FO4 saltea, o
    // sea que FO4 y el shader si coinciden. Es inerte con data real (las mascaras salen de bytes, que no
    // producen NaN) y alinearlo exigiria cambiar la LEY ESCALAR de SSE, que es la referencia. Se deja
    // anotado en vez de afirmar una coincidencia que no existe.
    // ATENCION: ASCII PURO. Este texto viaja DENTRO del string GLSL y el compilador de shaders lo
    // rechaza si trae no-ASCII. Un caracter como los que uso en los comentarios de VB deja el programa
    // sin compilar y el compositor GL sin dibujar, y eso NO lo ve ningun barrido de bytes del bake:
    // el A/B corre con FGBAKE_GPU_PARITY=0. Costo: una corrida entera de paridad.
    if (!(cov > 0.0)) {
        fragColor = vec4(prev, (uForceOpaqueAlpha == 1) ? 1.0 : prevRgba.a);
        return;
    }
    // over-RUNNING + 4 espacios (shader AGNOSTICO): el acumulador prev vive en uAccumSpace; el BLEND OP
    // corre en uWorkingSpace; el color de capa esta en uSrcSpace; y el COMPOSITE (lerp por cov) corre en
    // uCompositeSpace. Ley gen3: el blend va en su espacio (g22/srgb) pero la lerp por cobertura va en
    // LINEAR-light. uFramework decide como blend(prev/base,src) entra al acumulador (ver FaceTintFramework).
    // base = uBase (original sin tintar, en uAccumSpace). OverPrev (0, default) = el modelo previo
    // BYTE-IDENTICO (cuando uCompositeSpace==uWorkingSpace se reduce a lerp en working). 1:1 con CPU ComposeOne.
    vec3 src_w = cvt(srcColor, uSrcSpace, uWorkingSpace);
    vec3 base  = fetchAt(uBase).rgb;
    vec3 res_c;
    if (uFramework == 1) {                 // OverBase: mix(base, blend(base,src), cov)
        vec3 anchor_w = cvt(base, uAccumSpace, uWorkingSpace);
        vec3 blended  = blendDispatch(anchor_w, src_w);
        vec3 anchor_c = cvt(base, uAccumSpace, uCompositeSpace);
        vec3 blend_c  = cvt(blended, uWorkingSpace, uCompositeSpace);
        res_c = anchor_c + cov * (blend_c - anchor_c);
    } else if (uFramework == 2) {          // AddBase: prev + cov*(blend(base,src) - base)
        vec3 anchor_w = cvt(base, uAccumSpace, uWorkingSpace);
        vec3 blended  = blendDispatch(anchor_w, src_w);
        vec3 prev_c   = cvt(prev, uAccumSpace, uCompositeSpace);
        vec3 base_c   = cvt(base, uAccumSpace, uCompositeSpace);
        vec3 blend_c  = cvt(blended, uWorkingSpace, uCompositeSpace);
        res_c = prev_c + cov * (blend_c - base_c);
    } else if (uFramework == 3) {          // ModSrc: blend(prev, mix(neutral,src,cov)); replace -> OverPrev
        vec3 base_w = cvt(prev, uAccumSpace, uWorkingSpace);
        if (uBlendOp == 0) {
            vec3 bc = cvt(prev, uAccumSpace, uCompositeSpace);
            vec3 sc = cvt(src_w, uWorkingSpace, uCompositeSpace);
            res_c = bc + cov * (sc - bc);
        } else {
            vec3 neut    = vec3(blendNeutral(uBlendOp));
            vec3 smod_w  = neut + cov * (src_w - neut);
            vec3 blended = blendDispatch(base_w, smod_w);
            res_c = cvt(blended, uWorkingSpace, uCompositeSpace);
        }
    } else {                               // OverPrev (0, default): mix(prev, blend(prev,src), cov)
        vec3 base_w  = cvt(prev, uAccumSpace, uWorkingSpace);
        vec3 blended = blendDispatch(base_w, src_w);
        vec3 base_c  = cvt(prev, uAccumSpace, uCompositeSpace);
        vec3 blend_c = cvt(blended, uWorkingSpace, uCompositeSpace);
        res_c = base_c + cov * (blend_c - base_c);
    }
    res_c = clamp(res_c, 0.0, 1.0);
    vec3 finalRgb = cvt(res_c, uCompositeSpace, uAccumSpace);
    float outA = (uForceOpaqueAlpha == 1) ? 1.0 : prevRgba.a;
    fragColor = vec4(finalRgb, outA);
}"

    ' Base sRGB -> gamma-2.2 conversion shader. Reads the stored-sRGB diffuse base and writes its
    ' gamma-2.2 re-encoding (decode sRGB to linear, re-encode 2.2). CK encodes the FaceGen diffuse
    ' base in gamma-2.2; ours is stored sRGB. Verified empirically: this transfer maps our pre-tint
    ' base onto CK base at RMS ~0.5 across R/G/B (vs 2.2/3.6 untouched). Run ONCE before the compose
    ' loop into an Rgba32f target so the converted value is bit-identical to computing it in-shader
    ' per layer (full float32, no intra-loop requantization). Alpha is passed through unchanged so
    ' the accumulator seeded from this texture preserves the base alpha exactly. srgbToLinear is the
    ' IEC 61966-2-1 standard transfer (same as the compositor's), not a magic curve.

    ' ELIMINADO `ComposeOntoFaceDiffuse` (2026-07-30). Se anunciaba como "backward-compat wrapper" pero
    ' NO tenia UN SOLO caller en todo el repo, y en esta misma tanda se le habia agregado un parametro
    ' OBLIGATORIO (cpuMirror) — o sea que su compatibilidad hacia atras ya estaba rota igual. Un wrapper muerto
    ' que miente en su propio resumen es exactamente la clase de superficie que hace elegir la funcion
    ' equivocada. El reemplazo es `ComposeOntoFaceTexture(..., FaceTintChannel.Diffuse, cpuMirror)`.


    ''' <summary>Compone todas las capas que aportan al canal pedido (diffuse/normal/specular) sobre una copia
    ''' de la textura de cara y devuelve el nuevo texture-id; el original queda intacto. Devuelve 0 si falla o
    ''' si ninguna capa aporta datos a ese canal. DEBE correr en el hilo GL.
    ''' <para><paramref name="skinTint"/> es el color de tono de piel del NPC (vec3 0..1). En el canal Diffuse
    ''' el compositor tinta la base con ese valor en la primera iteracion y multiplica por el los colores de las
    ''' capas TakesSkinTone; el caller debe apagar el SkinTint del material de la cara despues de componer para
    ''' que el uniform del shader quede no-op, si no el tono se aplica dos veces. Nothing = saltear el manejo de
    ''' tono. Inerte en Normal y Specular.</para></summary>
    ''' <param name="cpuMirror">Capacidad del compositor CPU que espeja este camino: decide si el acumulador
    ''' puede vivir fuera de OutputSpace. Ver <see cref="FaceTintConvention.AccumSpaceForChannel"/>.</param>
    ''' <param name="stage">FASE que pide la convención. Nothing (default) = la etapa de TINT del canal, o sea
    ''' EXACTAMENTE lo que hacía antes de que el eje existiera. Un valor explícito (Fold/Overlay) elige el
    ''' bucket de esa etapa — y sólo en Diffuse, que es donde esas etapas existen (lo gatea ResolveConvention).</param>
    Public Function ComposeOntoFaceTexture(state As FaceTintCompositorState, originalTexId As Integer, width As Integer, height As Integer, layers As IList(Of FaceTintLayerInput), channel As FaceTintChannel,
                                           cpuMirror As FaceTintConvention.FaceTintCpuMirrorCapability,
                                           Optional cache As FaceTintTextureCache = Nothing, Optional headDiffuseAlphaTest As Boolean = False,
                                           Optional stage As FaceTintConvention.FaceTintStage? = Nothing) As Integer
        ' LA ETAPA EFECTIVA, resuelta UNA vez. No se puede usar `stage` a secas con un default fijo: la
        ' pipeline llama a los TRES canales, y para N/S la etapa correcta es TintNormalSpecular. Nullable ⇒
        ' "no me lo dijeron" es distinguible de "me dijeron TintDiffuse".
        Dim effStage As FaceTintConvention.FaceTintStage =
            If(stage.HasValue, stage.Value, FaceTintConvention.TintStageFor(channel))
        ArgumentNullException.ThrowIfNull(state)
        If originalTexId = 0 OrElse width <= 0 OrElse height <= 0 Then Return 0
        If layers Is Nothing OrElse layers.Count = 0 Then Return 0

        EnsureCompositorInitialized(state)
        If state._program = 0 OrElse state._quadVao = 0 Then Return 0

        ' Save GL state we are about to clobber (FT-014: capture incl. the FT-004 unit-0 fix).
        Dim glSaved As GlStateSnapshot = SaveGlState()

        Dim resultTex As Integer = 0
        Dim resultFbo As Integer = 0
        Dim batchLoaded As Dictionary(Of String, PreviewModel.Texture_Loaded_Class) = Nothing

        Try
            ' Drain pre-existing GL errors so the post-composite check below only flags
            ' failures caused by THIS pass.
            Dim drainGuard As Integer = 0
            Do While GL.GetError() <> ErrorCode.NoError
                drainGuard += 1
                If drainGuard > 32 Then Exit Do
            Loop

            ' === Batch preload every DDS byte buffer this pass needs, in ONE wrapper call. ===
            ' Per layer: its own channel bytes + its TTET[0] diffuse bytes when we need a spatial
            ' mask (N/S passes on TextureSet layers). The library helper decompresses the full
            ' batch in a single native call and uploads each to GL via PBO, returning a dict
            ' of Texture_Loaded_Class { Texture_ID, DGXFormat_Original, DGXFormat_Final, ... }.
            '
            ' When a FaceTintTextureCache is supplied, layers carrying a cache key reuse the
            ' decoded GL texture from previous calls instead of decoding+uploading every time.
            ' Layers with no cache key (legacy callers) fall through to a synthetic per-call
            ' key and follow the original allocate-and-delete lifecycle.
            Dim loadKeys As New List(Of String)
            Dim loadBytes As New List(Of Byte())
            Dim loadCacheable As New List(Of Boolean)
            Dim layerChannelKey As New Dictionary(Of Integer, String)
            Dim layerMaskKey As New Dictionary(Of Integer, String)
            Dim layerHairLutKey As New Dictionary(Of Integer, String)
            ' De-dupe by key BEFORE the loader runs. The loader/cache returns a dict keyed by
            ' request key, so two requests with the same key collapse to ONE entry — but the
            ' loader still generated a GL texture per request, so the earlier IDs would be lost
            ' and never deleted (cleanup iterates the returned dict). Uploading each unique key
            ' once and letting all referencing layers reuse it is pixel-identical (same bytes →
            ' same texture) and closes the leak. Synthetic per-call keys (l{i}c/l{i}m/l{i}lut)
            ' are already unique per layer/role so they pass through untouched.
            Dim requestedKeys As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
            Dim addRequest = Sub(reqKey As String, b As Byte(), cacheable As Boolean)
                                 If Not requestedKeys.Add(reqKey) Then Return
                                 loadKeys.Add(reqKey)
                                 loadBytes.Add(b)
                                 loadCacheable.Add(cacheable)
                             End Sub
            For i As Integer = 0 To layers.Count - 1
                Dim layer = layers(i)
                If layer Is Nothing Then Continue For
                Dim channelBytes = layer.GetChannelBytes(channel)
                If channelBytes Is Nothing OrElse channelBytes.Length = 0 Then Continue For

                ' Channel entry: prefer the caller-supplied cache key (typically the texture
                ' path) so multiple calls with the same source share a GL texture; fall back
                ' to a synthetic key when the caller didn't tag this layer.
                Dim chanCacheKey As String = layer.GetChannelCacheKey(channel)
                Dim kC As String = If(Not String.IsNullOrEmpty(chanCacheKey), chanCacheKey, $"l{i}c")
                addRequest(kC, channelBytes, Not String.IsNullOrEmpty(chanCacheKey))
                layerChannelKey(i) = kC

                If layer.Kind = FaceTintLayerKind.TextureSetDiffuse AndAlso channel <> FaceTintChannel.Diffuse _
                   AndAlso layer.LayerDdsBytes IsNot Nothing AndAlso layer.LayerDdsBytes.Length > 0 Then
                    Dim maskCacheKey As String = layer.LayerCacheKey
                    Dim kM As String = If(Not String.IsNullOrEmpty(maskCacheKey), maskCacheKey, $"l{i}m")
                    addRequest(kM, layer.LayerDdsBytes, Not String.IsNullOrEmpty(maskCacheKey))
                    layerMaskKey(i) = kM
                End If

                ' Hair LUT for layers that opt into the grayscale-to-palette path (typically slot
                ' Brows). Works for both PaletteMask (mask.r as X) and TextureSet (luminance grey
                ' of layerSample.rgb as X); shader branch picks the X source from uLayerKind.
                ' Only meaningful on Diffuse; skipping N/S keeps the batch small.
                If channel = FaceTintChannel.Diffuse _
                   AndAlso layer.UseHairPalette _
                   AndAlso layer.HairLutDdsBytes IsNot Nothing AndAlso layer.HairLutDdsBytes.Length > 0 Then
                    Dim lutCacheKey As String = layer.HairLutCacheKey
                    Dim kL As String = If(Not String.IsNullOrEmpty(lutCacheKey), lutCacheKey, $"l{i}lut")
                    addRequest(kL, layer.HairLutDdsBytes, Not String.IsNullOrEmpty(lutCacheKey))
                    layerHairLutKey(i) = kL
                End If
            Next
            If loadKeys.Count > 0 Then
                If cache IsNot Nothing Then
                    batchLoaded = cache.GetOrLoadBatch(loadKeys, loadBytes, loadCacheable)
                Else
                    ' srgb=False para TODAS: las texturas del compositor se cargan CRUDAS; el decode lo hace el
                    ' shader por convención (uSrcSpace/ss) por-capa. sRGB-loadearlas acá = doble decode.
                    batchLoaded = DirectXDDSLoader.Load_And_GenerateOpenGLTextures_Memory(loadKeys.ToArray(), loadBytes.ToArray(), GlDecodeUseCompress, True, New Boolean(loadKeys.Count - 1) {})
                End If
            End If

            ' Reuse persistent ping-pong attachments at this size; allocate the caller-owned
            ' result texture+fbo for the final pass output. Pings stay alive in the state
            ' across calls, eliminating the per-call GenTexture+TexImage2D+DeleteTexture
            ' churn for 1024^2 face textures.
            If Not EnsurePingPongAllocated(state, width, height) Then Return 0
            If Not AllocateResultTextureAndFbo(state, width, height, resultTex, resultFbo) Then Return 0

            GL.Viewport(0, 0, width, height)
            GL.Disable(EnableCap.DepthTest)
            GL.Disable(EnableCap.ScissorTest)
            GL.Disable(EnableCap.Blend)   ' the shader does its own blending against uPrev

            ' Diffuse base sRGB->gamma-2.2 conversion (CK encodes the FaceGen diffuse base in 2.2).
            ' Done ONCE here -- the base is invariant across layers -- into an Rgba32f texture, then
            ' used as BOTH the accumulator seed and the uBase reference, so the per-layer fragment
            ' path carries no gamma branch. Float target keeps it bit-identical to the old per-layer
            ' in-shader recompute. N/S are linear data and never converted. Reversible: when
            ' ConvertDiffuseBaseToGamma22 is False, baseTexForCompose stays the raw stored base.
            ' On conversion failure we fall back to the raw base (stored space) rather than abort.
            ' DERIVED MODEL: el shader convierte sRGB->ws por capa internamente, asi que uBase
            ' DEBE ser el base RAW sRGB (sin pre-pass g22). Sin occlusion (descartado B07-B09).
            ' El pre-pass g22 + footprint del modelo viejo se eliminan: baseTexForCompose = raw.
            Dim convertBaseEffective As Boolean = False
            Dim occlusionActive As Boolean = False
            Dim baseTexForCompose As Integer = originalTexId

            GL.UseProgram(state._program)
            GL.BindVertexArray(state._quadVao)
            ' fetchAt necesita el tamaño del DESTINO para decidir indice directo vs resample, y no hay forma
            ' de consultarlo desde el fragment shader. Va pegado al UseProgram de cada pase, con los MISMOS
            ' width/height del GL.Viewport de arriba: si los dos valores se separan, fetchAt resamplea donde
            ' correspondia copia directa (y al reves) sin que nada falle visiblemente.
            GL.Uniform2(state._uTargetSizeLoc, width, height)
            GL.Uniform1(state._uDownsizeFromMip0Loc, If(FaceTintCpuCompositor.DownsizeFromMip0, 1, 0))   ' MISMO valor que la ley del CPU

            ' BASEIN se dumpea PRISTINO (CPU/DirectXTex) NPC-side en FaceGenBuilder.DumpPristineTgas.
            ' El readback GL de aca daba el decode de la GPU (~max 62 off vs CK) y gastaba recursos
            ' (glGetTexImage de la textura comprimida); removido — un solo camino para ese dump.

            ' Pre-pass: count drawable layers so we can route the LAST one to resultFbo
            ' (caller-owned) instead of the persistent pings (which would mutate under the
            ' caller's feet on the next compose call).
            Dim drawableCount As Integer = 0
            For i As Integer = 0 To layers.Count - 1
                Dim ll = layers(i)
                If ll Is Nothing Then Continue For
                ' Este conteo DEBE aceptar exactamente las mismas capas que el loop de compose de abajo (si no,
                ' isLast apunta al draw equivocado y la última capa no escribe en el resultFbo). Una capa con
                ' LayerTextureId (textura GL ya subida, p.ej. el facetint float del pliegue SSE) NO aporta bytes al
                ' batch ⇒ no está en batchLoaded ⇒ acá NO se contaba y drawableCount daba 0 ⇒ Return 0.
                If ll.LayerTextureId <> 0 AndAlso channel = FaceTintChannel.Diffuse Then
                    drawableCount += 1
                    Continue For
                End If
                Dim k As String = Nothing
                If Not layerChannelKey.TryGetValue(i, k) Then Continue For
                Dim e As PreviewModel.Texture_Loaded_Class = Nothing
                If batchLoaded Is Nothing OrElse Not batchLoaded.TryGetValue(k, e) _
                   OrElse e Is Nothing OrElse e.Texture_ID = 0 Then Continue For
                drawableCount += 1
            Next

            If drawableCount = 0 Then
                ' Nothing to draw; release the per-call result texture and return 0 (matches legacy
                ' behaviour). The scratch FBO is persistent (state-owned) — do NOT delete it here.
                Try : GL.DeleteTexture(resultTex) : Catch : End Try
                resultFbo = 0
                resultTex = 0
                Return 0
            End If

            Dim writeIdx As Integer = 0
            ' First iteration reads the accumulator seed = baseTexForCompose: the gamma-2.2-converted
            ' base on the diffuse channel (so the whole accumulation lives in 2.2), or the raw base on
            ' N/S / when conversion is off. Identical to the old "convert prev once on the first layer".
            ' over-RUNNING: el acumulador arranca en el base RAW (originalTexId) y cada capa se
            ' compone sobre el resultado de la anterior (readTexId se reapunta a lo recien escrito).
            Dim readTexId As Integer = baseTexForCompose
            Dim drawnSoFar As Integer = 0

            Dim drawnLayers As Integer = 0
            Dim totalLayers As Integer = If(layers IsNot Nothing, layers.Count, 0)
            ' Pre-tono TakesSkinTone: captura del skintone (slot 12) tras componerlo, para pre-tonar las
            ' flagged-after-skintone. GUARD: stSeen False hasta pasar el skintone -> inerte en todo bake actual.
            Dim stSeen As Boolean = False
            Dim stMaskTexId As Integer = 0
            Dim stColR As Single = 0, stColG As Single = 0, stColB As Single = 0, stOpac As Single = 0
            Dim stWs As Integer = 0, stCs As Integer = 0, stSs As Integer = 0, stOs As Integer = 0
            Dim stBop As Integer = 0, stSl As Integer = 0, stMc As Integer = 0, stMaskCh As Integer = 1
            ' Pre-scan TakesSkinTone (2-pass, = CPU FaceTintCpuCompositor): params del skintone ANTES del loop,
            ' para pre-tonar tambien las flagged que componen ANTES del skintone bajo OverBase/AddBase (nonAccum).
            ' OverPrev/ModSrc -> nonAccum=False -> el guard se reduce a stSeen (byte-identico: uPreToneSkin=0
            ' hace que el shader ignore los uSkin*). Misma logica/captura que el CPU -> paridad GL/CPU.
            Dim skintoneFound As Boolean = False
            Dim nonAccum As Boolean = False
            If channel = FaceTintChannel.Diffuse Then
                For si As Integer = 0 To layers.Count - 1
                    Dim sLayer = layers(si)
                    If sLayer Is Nothing OrElse Not sLayer.IsSkinTone Then Continue For
                    Dim sKey As String = Nothing
                    If Not layerChannelKey.TryGetValue(si, sKey) Then Continue For
                    Dim sEntry As PreviewModel.Texture_Loaded_Class = Nothing
                    If batchLoaded Is Nothing OrElse Not batchLoaded.TryGetValue(sKey, sEntry) _
                       OrElse sEntry Is Nothing OrElse sEntry.Texture_ID = 0 Then Continue For
                    Dim sConv = FaceTintConvention.ResolveConvention(effStage, channel, sLayer.IsTextureSet, sLayer.BlendOp)
                    stMaskTexId = sEntry.Texture_ID
                    stColR = CSng(sLayer.R) / 255.0F : stColG = CSng(sLayer.G) / 255.0F : stColB = CSng(sLayer.B) / 255.0F
                    stOpac = Math.Max(0.0F, Math.Min(1.0F, sLayer.Opacity))
                    stWs = CInt(sConv.WorkingSpace) : stCs = CInt(sConv.CompositeSpace)
                    stSs = CInt(sConv.SrcSpace) : stOs = CInt(sConv.OutputSpace)
                    stBop = CInt(sConv.Blend) : stSl = CInt(sConv.SoftLight) : stMc = CInt(sConv.MaskConv)
                    stMaskCh = If(sLayer.Kind = FaceTintLayerKind.PaletteMask, 1, 3)
                    nonAccum = (sConv.Framework = FaceTintFramework.OverBase OrElse sConv.Framework = FaceTintFramework.AddBase)
                    skintoneFound = True
                    Exit For
                Next
            End If
            For i As Integer = 0 To layers.Count - 1
                Dim layer = layers(i)
                If layer Is Nothing Then Continue For

                ' Previously: TakesSkinTone layers were skipped on the Diffuse channel under
                ' the hypothesis that the scar/wrinkle _d slot only carried relief and the
                ' base face _d had the colour pre-baked. Empirically wrong — Alijo's
                ' BaseFemaleHead_d has no per-scar pixels, so the visible scar comes from
                ' the layer's TTET[0] (Scar6_d / Scar11_d / etc.) being composited via its
                ' own diffuse alpha and the authored blendOp. Skip removed; the standard
                ' TextureSet-Diffuse path below handles it.

                ' Fuente de la capa: una textura GL ya subida (LayerTextureId, p.ej. el facetint del pliegue SSE en
                ' Rgba32f — ver la doc de la propiedad: un DDS la cuantizaría a 8 bits) o, por defecto, el DDS decodificado.
                Dim layerTex As Integer = 0
                If layer.LayerTextureId <> 0 AndAlso channel = FaceTintChannel.Diffuse Then
                    layerTex = layer.LayerTextureId
                Else
                    Dim chanKey As String = Nothing
                    If Not layerChannelKey.TryGetValue(i, chanKey) Then
                        Continue For
                    End If

                    Dim chanEntry As PreviewModel.Texture_Loaded_Class = Nothing
                    If batchLoaded Is Nothing _
                       OrElse Not batchLoaded.TryGetValue(chanKey, chanEntry) _
                       OrElse chanEntry Is Nothing OrElse chanEntry.Texture_ID = 0 Then
                        Continue For
                    End If
                    layerTex = chanEntry.Texture_ID
                End If

                ' Diffuse mask lookup (present for TextureSet layers on N/S passes only).
                Dim diffuseMaskTex As Integer = 0
                Dim maskEntry As PreviewModel.Texture_Loaded_Class = Nothing
                Dim maskKey As String = Nothing
                If layerMaskKey.TryGetValue(i, maskKey) Then
                    If batchLoaded.TryGetValue(maskKey, maskEntry) AndAlso maskEntry IsNot Nothing AndAlso maskEntry.Texture_ID <> 0 Then
                        diffuseMaskTex = maskEntry.Texture_ID
                    End If
                End If

                ' Hair LUT lookup for brow palette layers. Resolved HERE (before any texture-unit
                ' binding); bound on unit 3 further down.
                Dim hairLutTex As Integer = 0
                Dim lutKey As String = Nothing
                If layerHairLutKey.TryGetValue(i, lutKey) Then
                    Dim lutEntry As PreviewModel.Texture_Loaded_Class = Nothing
                    If batchLoaded IsNot Nothing _
                       AndAlso batchLoaded.TryGetValue(lutKey, lutEntry) _
                       AndAlso lutEntry IsNot Nothing AndAlso lutEntry.Texture_ID <> 0 Then
                        hairLutTex = lutEntry.Texture_ID
                    End If
                End If

                ' Last drawable layer writes to caller-owned resultFbo; intermediate layers
                ' bounce through the persistent pings.
                Dim isLast As Boolean = (drawnSoFar = drawableCount - 1)
                Dim drawFbo As Integer = If(isLast, resultFbo, state._pingFbo(writeIdx))
                GL.BindFramebuffer(FramebufferTarget.Framebuffer, drawFbo)

                GL.ActiveTexture(TextureUnit.Texture0)
                GL.BindTexture(TextureTarget.Texture2D, readTexId)
                GL.Uniform1(state._uPrevLoc, 0)

                GL.ActiveTexture(TextureUnit.Texture1)
                GL.BindTexture(TextureTarget.Texture2D, layerTex)
                GL.Uniform1(state._uLayerLoc, 1)

                ' Unit 2 always has a valid binding (fallback to layerTex) so the sampler
                ' is never undefined; uHasDiffuseMask tells the shader whether to read it.
                GL.ActiveTexture(TextureUnit.Texture2)
                GL.BindTexture(TextureTarget.Texture2D, If(diffuseMaskTex <> 0, diffuseMaskTex, layerTex))
                GL.Uniform1(state._uLayerDiffuseAlphaLoc, 2)
                GL.Uniform1(state._uHasDiffuseMaskLoc, If(diffuseMaskTex <> 0, 1, 0))

                ' Hair LUT on unit 3 (resolved + dumped earlier, before the bindings). Unit always
                ' has a valid binding (fallback to layerTex when the layer didn't opt in) so the
                ' sampler is never undefined; uUseHairPalette tells the shader whether to read it.
                GL.ActiveTexture(TextureUnit.Texture3)
                GL.BindTexture(TextureTarget.Texture2D, If(hairLutTex <> 0, hairLutTex, layerTex))
                GL.Uniform1(state._uHairLutLoc, 3)

                ' Unit 4 (uBase): el shader SÍ lee uBase en los frameworks OverBase (uFramework==1) y
                ' AddBase (uFramework==2) — ahí 'base' es el ancla del blend/composite (el original sin
                ' tintar). VIVE EN uAccumSpace, no en uOutputSpace: `baseTexForCompose` es la textura de
                ' ENTRADA de este pase, o sea el acumulador ya sembrado y post-region-swaps, y el seed del
                ' pipeline lo deja en AccumSpace (ver ApplyFaceTintPipeline). Es exactamente lo que hace el
                ' CPU: su snapshot `base` se toma de accR/accG/accB despues de los swaps, o sea en accSp.
                ' Por eso el shader lo lee con uAccumSpace y no hay conversion que agregar acá.
                ' En OverPrev (0, default) y ModSrc (3) la base del blend es el
                ' acumulador corriente uPrev y uBase no se usa; aun así se bindea un texture válido para
                ' que el sampler nunca quede indefinido (el shader lo samplea incondicionalmente).
                GL.ActiveTexture(TextureUnit.Texture4)
                GL.BindTexture(TextureTarget.Texture2D, baseTexForCompose)
                GL.Uniform1(state._uBaseLoc, 4)
                Dim useHairPaletteEffective As Boolean = (hairLutTex <> 0 AndAlso layer.UseHairPalette _
                                                           AndAlso channel = FaceTintChannel.Diffuse)
                GL.Uniform1(state._uUseHairPaletteLoc, If(useHairPaletteEffective, 1, 0))
                ' Derived model: resolver de convencion centralizado (FaceTintConvention).
                ' ws/maskconv/blend salen de (etapa + canal + entry_type + blendOp). Decía que también
                ' salían de `slot` y `useHairPalette`: el resolver no leía ninguno de los dos.
                ' SIN occlusion footprint (descartado empiricamente B07-B09).
                ' `effStage`, NO `TintStageFor(channel)` cableado: el eje de etapa llega del caller y ES el que
                ' elige el bucket (Fold/Overlay tienen el suyo). Acá estaba fijo en la etapa de tint, así que el
                ' parámetro `stage` de ApplyFaceTintPipeline se ACEPTABA Y SE DESCARTABA — ningún bucket que no
                ' fuera el del canal podía llegar nunca al shader.
                Dim conv = FaceTintConvention.ResolveConvention(
                    effStage, channel, layer.IsTextureSet, layer.BlendOp)
                GL.Uniform1(state._uModeLoc, 0)   ' tint = additive-over-base
                GL.Uniform1(state._uWorkingSpaceLoc, CInt(conv.WorkingSpace))
                GL.Uniform1(state._uSrcSpaceLoc, CInt(conv.SrcSpace))
                GL.Uniform1(state._uOutputSpaceLoc, CInt(conv.OutputSpace))
                GL.Uniform1(state._uAccumSpaceLoc, CInt(FaceTintConvention.AccumSpaceForChannel(channel, cpuMirror)))   ' PARIDAD CPU: MISMA funcion que usa ComposeChannelCpu
                GL.Uniform1(state._uCompositeSpaceLoc, CInt(conv.CompositeSpace))
                GL.Uniform1(state._uMaskConvFullLoc, CInt(conv.MaskConv))
                GL.Uniform1(state._uSoftLightLoc, CInt(conv.SoftLight))   ' modelo de softlight (agnostico) para bop3
                GL.Uniform1(state._uFrameworkLoc, CInt(conv.Framework))   ' framework de composite (OverPrev default)
                ' Alpha del _d (corregido 2026-07-20): PASSTHROUGH del alpha del base SÓLO si la cabeza usa
                ' Diffuse Alpha Test (flag ACBS 0x01000000, canal Diffuse); si no, OPACO en el último layer
                ' (comportamiento original). El CK aplana el alpha del _d salvo cuando el head material lo testea.
                ' Valentine (flag SET) → passthrough (transparencia); DiMA (CLEAR) → opaco, como el CK. Antes:
                ' passthrough incondicional inventaba el alpha de DiMA (medición: DLC03DiMA _d ALPHA varía). Espejo
                ' del CPU compositor (keepBaseAlpha = flag AndAlso isD). Ver 40-bake-leyes-fo4.
                GL.Uniform1(state._uForceOpaqueAlphaLoc, If(headDiffuseAlphaTest AndAlso channel = FaceTintChannel.Diffuse, 0, If(isLast, 1, 0)))
                GL.Uniform1(state._uPaletteRowLoc, Math.Max(0.0F, Math.Min(1.0F, layer.HairPaletteRow)))
                ' Flat HCLF-RGB tint for TextureSet brow layers. Mutually exclusive with the LUT
                ' path above (palette branch wins when both are set, mirroring the CPU rule).
                Dim forceUniformColorEffective As Boolean = (layer.ForceUniformColor _
                                                             AndAlso layer.Kind = FaceTintLayerKind.TextureSetDiffuse _
                                                             AndAlso channel = FaceTintChannel.Diffuse _
                                                             AndAlso Not useHairPaletteEffective)
                GL.Uniform1(state._uForceUniformColorLoc, If(forceUniformColorEffective, 1, 0))
                ' skee overlay type-0: tex × tint. Solo TextureSet-diffuse, no combinado con Force/LUT.
                Dim texTimesColorEffective As Boolean = (layer.MultiplyTextureByColor _
                                                         AndAlso layer.Kind = FaceTintLayerKind.TextureSetDiffuse _
                                                         AndAlso channel = FaceTintChannel.Diffuse _
                                                         AndAlso Not useHairPaletteEffective AndAlso Not forceUniformColorEffective)
                GL.Uniform1(state._uTexTimesColorLoc, If(texTimesColorEffective, 1, 0))
                ' SSE fold facetint→albedo: solo TextureSet-diffuse, no combinado con tex×color/Force/LUT. src =
                ' (layerSample.rgb + off)·amp, cov=1 (multiply de cara completa) ⇒ blend forzado a Multiply abajo.
                Dim fgTintFoldEffective As Boolean = (layer.FgTintFold _
                                                      AndAlso layer.Kind = FaceTintLayerKind.TextureSetDiffuse _
                                                      AndAlso channel = FaceTintChannel.Diffuse _
                                                      AndAlso Not useHairPaletteEffective AndAlso Not forceUniformColorEffective AndAlso Not texTimesColorEffective)
                GL.Uniform1(state._uFgTintFoldLoc, If(fgTintFoldEffective, If(layer.FgTintUnfold, 2, 1), 0))
                GL.Uniform3(state._uFgTintOffLoc, layer.FgTintOffR, layer.FgTintOffG, layer.FgTintOffB)
                GL.Uniform1(state._uFgTintAmpLoc, layer.FgTintAmp)
                ' Detail (slot 3) del pliegue SSE en la unit 6. Sin detail ⇒ uHasFoldDetail=0 ⇒ el shader usa
                ' b=0.2509803922 (0.251 = default engine BSShader_DefFacegenDetail, oscurece), EXACTAMENTE como el fold
                ' CPU (SseFaceGenBaker emptyDetailDefault). Se bindea layerTex de relleno para que el
                ' sampler nunca quede indefinido (no se lee salvo con uFgTintFold==1 y uHasFoldDetail==1).
                GL.ActiveTexture(TextureUnit.Texture6)
                GL.BindTexture(TextureTarget.Texture2D, If(layer.FoldDetailTextureId <> 0, layer.FoldDetailTextureId, layerTex))
                GL.Uniform1(state._uFoldDetailLoc, 6)
                GL.Uniform1(state._uHasFoldDetailLoc, If(fgTintFoldEffective AndAlso layer.FoldDetailTextureId <> 0, 1, 0))
                GL.Uniform1(state._uPaletteMaskChannelLoc, layer.PaletteMaskChannel)   ' PaletteMask: verde (FO4) / rojo (SSE)

                GL.Uniform3(state._uColorLoc,
                            CSng(layer.R) / 255.0F,
                            CSng(layer.G) / 255.0F,
                            CSng(layer.B) / 255.0F)
                GL.Uniform1(state._uOpacityLoc, Math.Max(0.0F, Math.Min(1.0F, layer.Opacity)))
                ' uBlendOp: 0=Default 1=Multiply 2=Overlay 3=SoftLight 4=HardLight (contrato del shader). El fold SSE ya
                ' NO se expresa como blend-op: hace early-out en el shader con la ley del engine (ver uFgTintFold).
                GL.Uniform1(state._uBlendOpLoc, CInt(conv.Blend))
                GL.Uniform1(state._uLayerKindLoc, CInt(layer.Kind))
                GL.Uniform1(state._uChannelLoc, CInt(channel))

                ' Pre-tono TakesSkinTone: solo si la capa es flagged (D) y el skintone ya se compuso. uSkinMask
                ' en unit 5 (fallback layerTex para que el sampler nunca quede indefinido; solo se lee con
                ' uPreToneSkin==1). Color/op/espacios del skintone capturados al pasarlo. Inerte si stSeen=False.
                ' Pre-tono: flagged (D) Y hay skintone Y (ya compuesto antes -> over-running tona las de antes
                ' desde arriba, las de despues necesitan source-pretono) O framework no acumula (OverBase/AddBase
                ' -> el skintone no llega por el base -> pre-tonar TODA flagged). = guard del CPU (paridad).
                Dim preTone As Boolean = (channel = FaceTintChannel.Diffuse AndAlso layer.TakesSkinTone AndAlso skintoneFound AndAlso (stSeen OrElse nonAccum))
                GL.Uniform1(state._uPreToneSkinLoc, If(preTone, 1, 0))
                GL.ActiveTexture(TextureUnit.Texture5)
                GL.BindTexture(TextureTarget.Texture2D, If(stMaskTexId <> 0, stMaskTexId, layerTex))
                GL.Uniform1(state._uSkinMaskLoc, 5)
                GL.Uniform3(state._uSkinColorLoc, stColR, stColG, stColB)
                GL.Uniform1(state._uSkinOpacityLoc, stOpac)
                GL.Uniform1(state._uSkinWsLoc, stWs)
                GL.Uniform1(state._uSkinCsLoc, stCs)
                GL.Uniform1(state._uSkinSsLoc, stSs)
                GL.Uniform1(state._uSkinOsLoc, stOs)
                GL.Uniform1(state._uSkinBopLoc, stBop)
                GL.Uniform1(state._uSkinSlLoc, stSl)
                GL.Uniform1(state._uSkinMcLoc, stMc)
                GL.Uniform1(state._uSkinMaskChLoc, stMaskCh)

                GL.DrawArrays(PrimitiveType.Triangles, 0, 6)
                drawnLayers += 1

                ' Capturar el skintone (slot 12) tras componerlo: mask tex (Palette .g), color, op, espacios,
                ' para pre-tonar las flagged-after-skintone. El tex vive en batchLoaded toda la pasada.
                If channel = FaceTintChannel.Diffuse AndAlso layer.IsSkinTone Then
                    stMaskTexId = layerTex
                    stColR = CSng(layer.R) / 255.0F : stColG = CSng(layer.G) / 255.0F : stColB = CSng(layer.B) / 255.0F
                    stOpac = Math.Max(0.0F, Math.Min(1.0F, layer.Opacity))
                    stWs = CInt(conv.WorkingSpace) : stCs = CInt(conv.CompositeSpace)
                    stSs = CInt(conv.SrcSpace) : stOs = CInt(conv.OutputSpace)
                    stBop = CInt(conv.Blend) : stSl = CInt(conv.SoftLight) : stMc = CInt(conv.MaskConv)
                    stMaskCh = If(layer.Kind = FaceTintLayerKind.PaletteMask, 1, 3)
                    stSeen = True
                End If

                ' Unbind sampler slots; textures themselves are freed in the Finally block.
                GL.ActiveTexture(TextureUnit.Texture6)
                GL.BindTexture(TextureTarget.Texture2D, 0)
                GL.ActiveTexture(TextureUnit.Texture5)
                GL.BindTexture(TextureTarget.Texture2D, 0)
                GL.ActiveTexture(TextureUnit.Texture4)
                GL.BindTexture(TextureTarget.Texture2D, 0)
                GL.ActiveTexture(TextureUnit.Texture3)
                GL.BindTexture(TextureTarget.Texture2D, 0)
                GL.ActiveTexture(TextureUnit.Texture2)
                GL.BindTexture(TextureTarget.Texture2D, 0)
                GL.ActiveTexture(TextureUnit.Texture1)
                GL.BindTexture(TextureTarget.Texture2D, 0)

                ' Next iteration reads from what we just wrote (the ping we just bound). On the LAST
                ' pass there is no next iteration, so updating readTexId would be a dead write (it
                ' pointed at resultTex, which is never re-read) — guard it (FT-013).
                If Not isLast Then readTexId = state._pingTex(writeIdx)

                writeIdx = 1 - writeIdx
                drawnSoFar += 1
            Next

            ' resultTex now holds the final composite (drawableCount > 0 guaranteed by the
            ' early-return above, so the last layer always wrote into resultFbo).

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0)

            ' Certify GL produced a clean result. A silent error here means resultTex points
            ' at a texture with undefined contents — caller must NOT cache it.
            Dim postErr = GL.GetError()
            If postErr <> ErrorCode.NoError Then
                ' Hand the result texture back to the cleanup path by clearing resultTex;
                ' the Finally will delete the orphan via the resultTex-on-failure branch.
                Try : GL.DeleteTexture(resultTex) : Catch : End Try
                resultTex = 0
            End If
        Catch ex As Exception
            Dim exType = ex.GetType().Name, exMsg = ex.Message
            Logger.LogLazy(Function() $"[FACETINT] compose failed: {exType}: {exMsg}")
            If resultTex <> 0 Then
                Try : GL.DeleteTexture(resultTex) : Catch : End Try
                resultTex = 0
            End If
        Finally
            ' Free every GL texture that the batch loader created for this pass — except the
            ' ones the cache adopted. Cached entries survive across calls and will be released
            ' by FaceTintTextureCache.Clear() when the caller invalidates the cache.
            If batchLoaded IsNot Nothing Then
                For Each kvp In batchLoaded
                    Dim e = kvp.Value
                    If e Is Nothing OrElse e.Texture_ID = 0 Then Continue For
                    If cache IsNot Nothing AndAlso cache.IsCached(kvp.Key) Then Continue For
                    Try : GL.DeleteTexture(e.Texture_ID) : Catch : End Try
                Next
            End If

            ' The result FBO is the persistent scratch container (state-owned, reused across
            ' calls) — NOT deleted here. The result TEXTURE is caller-owned (returned). Pings
            ' and the scratch FBO stay in the state and are released only by state.Dispose().

            ' Restore state (FT-014).
            RestoreGlState(glSaved)
        End Try

        Return resultTex
    End Function

    ''' <summary>DXGI_FORMAT code -> short name for the formats actually seen in FO4 face textures.</summary>
    Private Function DxgiName(code As Integer) As String
        Select Case code
            Case 0 : Return "UNKNOWN"
            Case 28 : Return "RGBA8+a"
            Case 71 : Return "BC1_TL"
            Case 72 : Return "BC1+a"
            Case 73 : Return "BC1_SRGB"
            Case 74 : Return "BC2_TL"
            Case 75 : Return "BC2+a"
            Case 76 : Return "BC2_SRGB"
            Case 77 : Return "BC3_TL"
            Case 78 : Return "BC3+a"
            Case 79 : Return "BC3_SRGB"
            Case 80 : Return "BC4_TL"
            Case 81 : Return "BC4"
            Case 82 : Return "BC4s"
            Case 83 : Return "BC5_TL"
            Case 84 : Return "BC5"
            Case 85 : Return "BC5s"
            Case 86 : Return "B5G6R5"
            Case 87 : Return "B5G5R5A1"
            Case 88 : Return "BGRA8+a"
            Case 89 : Return "BGRX8"
            Case 94 : Return "BC6H_TL"
            Case 95 : Return "BC6H_UF16"
            Case 96 : Return "BC6H_SF16"
            Case 97 : Return "BC7_TL"
            Case 98 : Return "BC7+a"
            Case 99 : Return "BC7_SRGB"
            Case Else : Return $"DXGI={code}"
        End Select
    End Function

    ' Fragment shader del region-swap. Reemplazo gateado por la mascara BC1 Y por la intensidad del morph:
    '   weight = convMask(mask.r) * uSwapIntensity ; result = mix(prev, swap, weight)
    ' La mascara es un TTET[0], el MISMO tipo que lee el compositor de tints, asi que comparte LA convencion de
    ' cobertura (convMask): la mascara espacial se moldea, la intensidad queda como escalar lineal.
    ' uSwapIntensity es el valor MSDV del preset (0..1): el motor mezcla la variante proporcionalmente al
    ' slider, NO on/off (verificado contra el CK; aplicar a mascara completa sobre-aplicaba ~4 niveles).
    ' mask = DONDE, intensity = CUANTO; weight 0 deja la base intacta. El blend es REPLACE (mix): un region swap
    ' sustituye la variante entera de la region, no es un delta aditivo como las capas de tint.
    ' Contrato de alpha: el alpha de entrada (uPrev) se PRESERVA en la salida - swap y mascara aportan solo RGB
    ' y peso -, asi que un diffuse con alpha-test no pierde su recorte.

    ''' <summary>Aplica una lista de swaps MPPT TXST por region sobre la textura de cara del canal pedido y
    ''' devuelve el nuevo texture-id; el original queda intacto. Devuelve 0 si falla o si ningun swap aporta a
    ''' ese canal. DEBE correr en el hilo GL.
    ''' <para>Cada swap mezcla su textura sobre el acumulador previo usando el canal rojo de la mascara de
    ''' region como peso por pixel. Se aplican en orden de lista; si dos se solaparan (no pasa en vanilla: un
    ''' preset por grupo a la vez) gana el ultimo dentro del solape.</para></summary>
    ''' <param name="cpuMirror">Capacidad del compositor CPU que espeja este camino: decide si el acumulador
    ''' puede vivir fuera de OutputSpace. Ver <see cref="FaceTintConvention.AccumSpaceForChannel"/>.</param>
    Public Function ApplyRegionSwapsOntoFaceTexture(state As FaceTintCompositorState,
                                                     originalTexId As Integer,
                                                     width As Integer, height As Integer,
                                                     swaps As IList(Of FaceRegionSwapInput),
                                                     channel As FaceTintChannel,
                                                     cpuMirror As FaceTintConvention.FaceTintCpuMirrorCapability,
                                                     Optional cache As FaceTintTextureCache = Nothing) As Integer
        ArgumentNullException.ThrowIfNull(state)
        If originalTexId = 0 OrElse width <= 0 OrElse height <= 0 Then Return 0
        If swaps Is Nothing OrElse swaps.Count = 0 Then Return 0

        EnsureCompositorInitialized(state)
        If state._program = 0 OrElse state._quadVao = 0 Then Return 0

        ' Save GL state we are about to clobber (FT-014: capture incl. the FT-004 unit-0 fix).
        Dim glSaved As GlStateSnapshot = SaveGlState()

        Dim resultTex As Integer = 0
        Dim resultFbo As Integer = 0
        Dim batchLoaded As Dictionary(Of String, PreviewModel.Texture_Loaded_Class) = Nothing

        Try
            ' Drain pre-existing GL errors so the post-pass check only flags THIS pass.
            Dim drainGuard As Integer = 0
            Do While GL.GetError() <> ErrorCode.NoError
                drainGuard += 1
                If drainGuard > 32 Then Exit Do
            Loop

            ' === Batch preload every DDS this pass needs in ONE wrapper call. ===
            ' Per swap: its own swap channel bytes + its region mask bytes. Mask is the
            ' same DDS for every channel (D/N/S) so a higher-level cache could share it
            ' across the three pre-passes — for now we re-upload per channel which is
            ' simple and matches the pattern used by ComposeOntoFaceTexture.
            Dim loadKeys As New List(Of String)
            Dim loadBytes As New List(Of Byte())
            Dim loadCacheable As New List(Of Boolean)
            Dim swapTexKey As New Dictionary(Of Integer, String)
            Dim swapMaskKey As New Dictionary(Of Integer, String)
            ' De-dupe by key: swaps that share a region mask (or texture) would otherwise enqueue
            ' the same key twice, and the loader/cache returns one entry per key while generating
            ' one GL texture per enqueue — the extra IDs would leak (cleanup iterates the returned
            ' dict). Enqueue each unique key once; every swap reuses the single shared texture. The
            ' per-swap key maps (swapTexKey/swapMaskKey) still point each swap at its key, so the
            ' draw loop is unchanged and pixel-identical.
            Dim seenSwapKeys As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
            For i As Integer = 0 To swaps.Count - 1
                Dim sw = swaps(i)
                If sw Is Nothing Then Continue For
                Dim sb = sw.GetSwapBytes(channel)
                If sb Is Nothing OrElse sb.Length = 0 Then Continue For
                If sw.RegionMaskDdsBytes Is Nothing OrElse sw.RegionMaskDdsBytes.Length = 0 Then Continue For

                Dim swCacheKey As String = sw.GetSwapCacheKey(channel)
                Dim mkCacheKey As String = sw.RegionMaskCacheKey
                Dim kS As String = If(Not String.IsNullOrEmpty(swCacheKey), swCacheKey, $"s{i}t")
                Dim kM As String = If(Not String.IsNullOrEmpty(mkCacheKey), mkCacheKey, $"s{i}m")
                If seenSwapKeys.Add(kS) Then loadKeys.Add(kS) : loadBytes.Add(sb) : loadCacheable.Add(Not String.IsNullOrEmpty(swCacheKey))
                swapTexKey(i) = kS
                If seenSwapKeys.Add(kM) Then loadKeys.Add(kM) : loadBytes.Add(sw.RegionMaskDdsBytes) : loadCacheable.Add(Not String.IsNullOrEmpty(mkCacheKey))
                swapMaskKey(i) = kM
            Next
            If loadKeys.Count = 0 Then
                Return 0
            End If

            If cache IsNot Nothing Then
                batchLoaded = cache.GetOrLoadBatch(loadKeys, loadBytes, loadCacheable)
            Else
                ' srgb=False para TODAS: las texturas del compositor se cargan CRUDAS; el decode lo hace el
                ' shader por convención (uSrcSpace/ss) por-capa. sRGB-loadearlas acá = doble decode.
                batchLoaded = DirectXDDSLoader.Load_And_GenerateOpenGLTextures_Memory(loadKeys.ToArray(), loadBytes.ToArray(), GlDecodeUseCompress, True, New Boolean(loadKeys.Count - 1) {})
            End If

            ' Reuse persistent ping-pong attachments at this size; allocate caller-owned
            ' result for the final pass.
            If Not EnsurePingPongAllocated(state, width, height) Then Return 0
            If Not AllocateResultTextureAndFbo(state, width, height, resultTex, resultFbo) Then Return 0

            GL.Viewport(0, 0, width, height)
            GL.Disable(EnableCap.DepthTest)
            GL.Disable(EnableCap.ScissorTest)
            GL.Disable(EnableCap.Blend)

            GL.UseProgram(state._program)
            GL.BindVertexArray(state._quadVao)
            GL.Uniform2(state._uTargetSizeLoc, width, height)
            GL.Uniform1(state._uDownsizeFromMip0Loc, If(FaceTintCpuCompositor.DownsizeFromMip0, 1, 0))   ' MISMO valor que la ley del CPU
            ' Shader unico: region swap = uMode=1 (RUNNING CLOSED-FORM en stored space = build_3 + CPU). swap
            ' tex -> uLayer(1), region mask -> uLayerDiffuseAlpha(2), intensity(msdv) -> uOpacity, SEED ->
            ' uBase(4). El acumulador vive en uAccumSpace (con el default = OutputSpace: D en g22, N/S en
            ' linear); el swap tex (MPPT TXST diffuse) es sRGB -> src=srgb(1). N/S: datos lineales, src=0.
            ' El running necesita el
            ' SEED (= originalTexId) aparte del acumulador (uPrev): se bindea uBase=originalTexId POR-DRAW en
            ' el loop (no solo en el setup) para garantizar que la unit 4 este siempre el seed en cada draw.
            GL.Uniform1(state._uModeLoc, 1)
            ' Swap = replace resuelto por la MISMA tabla que los tints (forSwap:=True) -> el override de convención
            ' (incl. #If DEBUG full-linear) alcanza también los swaps. NON-DEBUG byte-idéntico (paridad con CPU).
            Dim swConv = FaceTintConvention.ResolveConvention(FaceTintConvention.FaceTintStage.RegionSwap, channel, isTextureSet:=False, blendOp:=0)
            ' FT-002 guard: the uMode==1 swap branch of the shader hardcodes blended = src_w (Replace)
            ' and carries no blend-op uniform. ResolveConvention currently pins swap.Blend = Replace
            ' (FaceTintConvention.vb), so this is inert. If a future config ever resolves a non-Replace
            ' swap blend, the GL path would SILENTLY stay on Replace while the CPU path honours the op —
            ' a GL/CPU divergence. Surface it loudly here instead of producing a wrong-but-quiet result.
            ' Fixing it properly means adding a blend-op uniform + routing the swap through blendDispatch
            ' in the shader; left out to preserve the byte-for-byte Replace output until that's needed.
            If swConv.Blend <> FaceTintBlend.Replace Then
                Dim bopName = swConv.Blend.ToString()
                Logger.LogLazy(Function() $"[FACETINT] GL region-swap resolved Blend={bopName} but the shader only implements Replace; GL output will NOT match the CPU path for this swap.")
            End If
            GL.Uniform1(state._uSrcSpaceLoc, CInt(swConv.SrcSpace))
            GL.Uniform1(state._uOutputSpaceLoc, CInt(swConv.OutputSpace))
            ' NO swConv.AccumSpace: el storage del acumulador es del CANAL, no de la fase del swap
            ' (ver AccumSpaceForChannel). Con swConv se podria configurar el swap en un espacio y el tint en
            ' otro sobre el MISMO buffer, y el CPU —que usa el del canal— divergiria.
            Dim swAccSp As Integer = CInt(FaceTintConvention.AccumSpaceForChannel(channel, cpuMirror))
            ' CAMBIO DE COMPORTAMIENTO DECLARADO (no es inerte). ANTES el acumulador del swap se trataba
            ' como si viviera en `swConv.OutputSpace` (el bucket SWAP) y el de los tints en el del CANAL: DOS
            ' etiquetas para EL MISMO buffer. Ahora manda el canal, en los dos lados (CPU y GL se movieron
            ' juntos, asi que la PARIDAD no se rompe). Con los defaults de fabrica de los dos juegos es
            ' byte-identico —FO4 Swap.OutputSpace = Diffuse.OutputSpace = G22; SSE todo Linear— pero NO lo es
            ' si el usuario separa esos dos combos en CharGen Options, que es editable. Mismo tratamiento que
            ' el guard FT-002 de arriba: se avisa FUERTE en vez de cambiar la salida en silencio.
            ' Misma advertencia y mismo canal que el CPU (FaceTintConvention, latcheada + always-on). NO se
            ' usa Logger: esta apagado en release, que es justo donde el usuario necesitaria el aviso.
            If channel = FaceTintChannel.Diffuse AndAlso CInt(swConv.OutputSpace) <> swAccSp Then
                FaceTintConvention.NoteSwapAccumMismatch(channel, CInt(swConv.OutputSpace), swAccSp)
            End If
            GL.Uniform1(state._uAccumSpaceLoc, swAccSp)
            GL.Uniform1(state._uCompositeSpaceLoc, CInt(swConv.CompositeSpace))
            GL.Uniform1(state._uWorkingSpaceLoc, CInt(swConv.WorkingSpace))
            GL.Uniform1(state._uMaskConvFullLoc, CInt(swConv.MaskConv))

            ' Pre-pass: count drawable swaps so we can route the LAST one to resultFbo.
            Dim drawableSwaps As Integer = 0
            For i As Integer = 0 To swaps.Count - 1
                Dim ss = swaps(i)
                If ss Is Nothing Then Continue For
                Dim sk As String = Nothing
                Dim mk As String = Nothing
                If Not swapTexKey.TryGetValue(i, sk) OrElse Not swapMaskKey.TryGetValue(i, mk) Then Continue For
                Dim se As PreviewModel.Texture_Loaded_Class = Nothing
                Dim mE2 As PreviewModel.Texture_Loaded_Class = Nothing
                If Not batchLoaded.TryGetValue(sk, se) OrElse se Is Nothing OrElse se.Texture_ID = 0 Then Continue For
                If Not batchLoaded.TryGetValue(mk, mE2) OrElse mE2 Is Nothing OrElse mE2.Texture_ID = 0 Then Continue For
                drawableSwaps += 1
            Next

            If drawableSwaps = 0 Then
                ' Persistent scratch FBO is state-owned — do NOT delete it; only free the per-call texture.
                Try : GL.DeleteTexture(resultTex) : Catch : End Try
                resultFbo = 0
                resultTex = 0
                Return 0
            End If

            Dim writeIdx As Integer = 0
            Dim readTexId As Integer = originalTexId
            Dim drawn As Integer = 0
            Dim drawnSoFar As Integer = 0

            For i As Integer = 0 To swaps.Count - 1
                Dim sw = swaps(i)
                If sw Is Nothing Then Continue For

                Dim sKey As String = Nothing
                Dim mKey As String = Nothing
                If Not swapTexKey.TryGetValue(i, sKey) OrElse Not swapMaskKey.TryGetValue(i, mKey) Then
                    Continue For
                End If

                Dim sEntry As PreviewModel.Texture_Loaded_Class = Nothing
                Dim mEntry As PreviewModel.Texture_Loaded_Class = Nothing
                If Not batchLoaded.TryGetValue(sKey, sEntry) OrElse sEntry Is Nothing OrElse sEntry.Texture_ID = 0 _
                   OrElse Not batchLoaded.TryGetValue(mKey, mEntry) OrElse mEntry Is Nothing OrElse mEntry.Texture_ID = 0 Then
                    Continue For
                End If


                Dim isLastSwap As Boolean = (drawnSoFar = drawableSwaps - 1)
                Dim drawFbo As Integer = If(isLastSwap, resultFbo, state._pingFbo(writeIdx))
                GL.BindFramebuffer(FramebufferTarget.Framebuffer, drawFbo)

                GL.ActiveTexture(TextureUnit.Texture0)
                GL.BindTexture(TextureTarget.Texture2D, readTexId)
                GL.Uniform1(state._uPrevLoc, 0)

                GL.ActiveTexture(TextureUnit.Texture1)
                GL.BindTexture(TextureTarget.Texture2D, sEntry.Texture_ID)
                GL.Uniform1(state._uLayerLoc, 1)

                GL.ActiveTexture(TextureUnit.Texture2)
                GL.BindTexture(TextureTarget.Texture2D, mEntry.Texture_ID)
                GL.Uniform1(state._uLayerDiffuseAlphaLoc, 2)

                ' uBase(4) = el SEED (originalTexId): base del running closed-form, APARTE del acumulador
                ' (uPrev unit 0). Se bindea ACA, por-draw, para garantizar que la unit 4 tenga el seed en
                ' CADA pasada (solo el bind de setup no alcanzaba -> uBase leia ~negro y rompia el running GL).
                GL.ActiveTexture(TextureUnit.Texture4)
                GL.BindTexture(TextureTarget.Texture2D, originalTexId)
                GL.Uniform1(state._uBaseLoc, 4)

                ' Morph intensity (MSDV value) -> uOpacity = el msdv del running (escala n y cov). Clamp.
                GL.Uniform1(state._uOpacityLoc, FaceTintConvention.ClampSwapIntensity(sw.Intensity))   ' ley UNICA compartida con el CPU

                GL.DrawArrays(PrimitiveType.Triangles, 0, 6)
                drawn += 1

                GL.ActiveTexture(TextureUnit.Texture4)
                GL.BindTexture(TextureTarget.Texture2D, 0)
                GL.ActiveTexture(TextureUnit.Texture2)
                GL.BindTexture(TextureTarget.Texture2D, 0)
                GL.ActiveTexture(TextureUnit.Texture1)
                GL.BindTexture(TextureTarget.Texture2D, 0)

                readTexId = If(isLastSwap, resultTex, state._pingTex(writeIdx))

                writeIdx = 1 - writeIdx
                drawnSoFar += 1
            Next

            ' resultTex now holds the final composite (drawableSwaps > 0 guaranteed above).


            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0)

            Dim postErr = GL.GetError()
            If postErr <> ErrorCode.NoError Then
                Try : GL.DeleteTexture(resultTex) : Catch : End Try
                resultTex = 0
            End If
        Catch ex As Exception
            Dim exType = ex.GetType().Name, exMsg = ex.Message
            Logger.LogLazy(Function() $"[FACETINT] compose failed: {exType}: {exMsg}")
            If resultTex <> 0 Then
                Try : GL.DeleteTexture(resultTex) : Catch : End Try
                resultTex = 0
            End If
        Finally
            ' Cached entries survive across calls; only delete the per-call ones.
            If batchLoaded IsNot Nothing Then
                For Each kvp In batchLoaded
                    Dim e = kvp.Value
                    If e Is Nothing OrElse e.Texture_ID = 0 Then Continue For
                    If cache IsNot Nothing AndAlso cache.IsCached(kvp.Key) Then Continue For
                    Try : GL.DeleteTexture(e.Texture_ID) : Catch : End Try
                Next
            End If

            ' Result FBO is the persistent scratch container (state-owned); pings stay persistent
            ' in the state. Neither is deleted here — both live until state.Dispose().

            ' Restore state (FT-014).
            RestoreGlState(glSaved)
        End Try

        Return resultTex
    End Function



    ''' <summary>Snapshot of the GL state the compositor passes clobber, captured by
    ''' <see cref="SaveGlState"/> and replayed by <see cref="RestoreGlState"/>. Holds the six
    ''' bindings (FBO / program / VAO / active texture unit / unit-0 2D binding / viewport) plus
    ''' the three enable-caps (Blend / DepthTest / ScissorTest) the compose passes toggle.</summary>
    Private Structure GlStateSnapshot
        Public Fbo As Integer
        Public Prog As Integer
        Public Vao As Integer
        Public ActiveTex As Integer
        Public Tex0 As Integer
        Public Viewport0 As Integer
        Public Viewport1 As Integer
        Public Viewport2 As Integer
        Public Viewport3 As Integer
        Public WasBlend As Boolean
        Public WasDepth As Boolean
        Public WasScissor As Boolean
    End Structure

    ''' <summary>Capture the GL state the compose passes are about to clobber. Includes the FT-004
    ''' fix in ONE place: select texture unit 0 BEFORE reading its 2D binding, because
    ''' GetInteger(TextureBinding2D) reports the binding of the CURRENTLY ACTIVE unit while the
    ''' restore rebinds Tex0 onto unit 0. For callers that enter with unit 0 already active this is
    ''' a no-op (identical capture). MUST run on the GL thread.</summary>
    Private Function SaveGlState() As GlStateSnapshot
        Dim s As GlStateSnapshot
        s.Fbo = GL.GetInteger(GetPName.DrawFramebufferBinding)
        s.Prog = GL.GetInteger(GetPName.CurrentProgram)
        s.Vao = GL.GetInteger(GetPName.VertexArrayBinding)
        s.ActiveTex = GL.GetInteger(GetPName.ActiveTexture)
        ' FT-004: select unit 0 before reading TextureBinding2D so the captured Tex0 is unit 0's
        ' binding (the unit the restore rebinds onto), not the previously-active unit's binding.
        GL.ActiveTexture(TextureUnit.Texture0)
        s.Tex0 = GL.GetInteger(GetPName.TextureBinding2D)
        Dim vp(3) As Integer
        GL.GetInteger(GetPName.Viewport, vp)
        s.Viewport0 = vp(0) : s.Viewport1 = vp(1) : s.Viewport2 = vp(2) : s.Viewport3 = vp(3)
        s.WasBlend = GL.IsEnabled(EnableCap.Blend)
        s.WasDepth = GL.IsEnabled(EnableCap.DepthTest)
        s.WasScissor = GL.IsEnabled(EnableCap.ScissorTest)
        Return s
    End Function

    ''' <summary>Restore the GL state captured by <see cref="SaveGlState"/>. Replays exactly what the
    ''' compose-pass Finally blocks did pre-refactor: rebind FBO/program/VAO, restore unit-0's 2D
    ''' binding then reselect the original active unit, restore the viewport, and re-apply the three
    ''' enable-caps. MUST run on the GL thread.</summary>
    Private Sub RestoreGlState(s As GlStateSnapshot)
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, s.Fbo)
        GL.UseProgram(s.Prog)
        GL.BindVertexArray(s.Vao)
        GL.ActiveTexture(TextureUnit.Texture0)
        GL.BindTexture(TextureTarget.Texture2D, s.Tex0)
        GL.ActiveTexture(CType(s.ActiveTex, TextureUnit))
        GL.Viewport(s.Viewport0, s.Viewport1, s.Viewport2, s.Viewport3)
        If s.WasDepth Then GL.Enable(EnableCap.DepthTest) Else GL.Disable(EnableCap.DepthTest)
        If s.WasScissor Then GL.Enable(EnableCap.ScissorTest) Else GL.Disable(EnableCap.ScissorTest)
        If s.WasBlend Then GL.Enable(EnableCap.Blend) Else GL.Disable(EnableCap.Blend)
    End Sub

    ''' <summary>Allocate (or reuse) the two persistent ping-pong colour attachments at
    ''' (width, height). Re-allocates when dims change; reuses verbatim when they match.
    ''' Returns True on success; False on framebuffer-incompleteness (in which case the
    ''' state is rolled back to "no pings allocated"). MUST run on the GL thread.</summary>
    Private Function EnsurePingPongAllocated(state As FaceTintCompositorState, width As Integer, height As Integer) As Boolean
        If state._pingTex(0) <> 0 AndAlso state._pingTex(1) <> 0 _
           AndAlso state._pingFbo(0) <> 0 AndAlso state._pingFbo(1) <> 0 _
           AndAlso state._pingW = width AndAlso state._pingH = height Then
            Return True
        End If

        ' Dim mismatch (or never allocated): release stale handles before re-allocating.
        state.ReleasePingPongInternal()

        For i As Integer = 0 To 1
            state._pingTex(i) = GL.GenTexture()
            GL.BindTexture(TextureTarget.Texture2D, state._pingTex(i))
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, CInt(TextureMinFilter.Linear))
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, CInt(TextureMagFilter.Linear))
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, CInt(TextureWrapMode.ClampToEdge))
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, CInt(TextureWrapMode.ClampToEdge))
            ' Rgba32f: float storage so the accumulator never quantizes BETWEEN layers
            ' (only at the final GetTexImage when the bake reads back). With Rgba8 the
            ' per-layer write rounded each blend to 8 bits and the next layer sampled
            ' that quantized value, compounding ~0.5/255 of noise per pass. Verified
            ' against the Python sim: float storage closes ~5/7 of the bit-diff on the
            ' 5-layer Diffuse compose.
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba32f,
                          width, height, 0,
                          PixelFormat.Rgba, PixelType.Float, IntPtr.Zero)

            state._pingFbo(i) = GL.GenFramebuffer()
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, state._pingFbo(i))
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
                                    TextureTarget.Texture2D, state._pingTex(i), 0)
            Dim status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer)
            If status <> FramebufferErrorCode.FramebufferComplete Then
                state.ReleasePingPongInternal()
                Return False
            End If
        Next

        state._pingW = width
        state._pingH = height
        Return True
    End Function

    ''' <summary>Allocate one fresh RGBA32f texture for the caller-owned final output of a pass
    ''' and attach it to the state's PERSISTENT scratch FBO (reused across calls — see
    ''' <see cref="FaceTintCompositorState._scratchResultFbo"/>). The caller is responsible for
    ''' deleting <paramref name="resultTex"/> (per existing contract); the FBO container is
    ''' persistent and is NOT deleted by the caller — it lives until the state's Dispose().
    ''' The completed scratch FBO handle is returned via <paramref name="resultFbo"/> (its
    ''' colour attachment is the fresh <paramref name="resultTex"/>). Returns False on FBO
    ''' incompleteness (the fresh texture is freed before return; the persistent FBO is left
    ''' intact for the next call). MUST run on the GL thread.</summary>
    Private Function AllocateResultTextureAndFbo(state As FaceTintCompositorState, width As Integer, height As Integer,
                                                  ByRef resultTex As Integer, ByRef resultFbo As Integer) As Boolean
        resultTex = GL.GenTexture()
        GL.BindTexture(TextureTarget.Texture2D, resultTex)
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, CInt(TextureMinFilter.Linear))
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, CInt(TextureMagFilter.Linear))
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, CInt(TextureWrapMode.ClampToEdge))
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, CInt(TextureWrapMode.ClampToEdge))
        ' Rgba32f: see EnsurePingPongAllocated. The caller-owned output of the LAST
        ' pass holds the same precision as the intermediates so the final byte readback
        ' has one quantization step (the readback itself) instead of N+1 (one per layer
        ' + the readback). Eliminates per-layer 8-bit truncation noise on multi-layer
        ' composes (Diffuse mainly; N/S single-layer is unchanged byte-wise here).
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba32f,
                      width, height, 0,
                      PixelFormat.Rgba, PixelType.Float, IntPtr.Zero)

        ' Reuse the persistent scratch FBO container instead of Gen/Delete per call. The result
        ' texture lifetime contract is unchanged (fresh per call, caller-owned); only the FBO
        ' container is reused. Re-attaching the fresh texture + re-checking completeness produces
        ' an identical render target to the old per-call GenFramebuffer path.
        If state._scratchResultFbo = 0 Then state._scratchResultFbo = GL.GenFramebuffer()
        resultFbo = state._scratchResultFbo
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, resultFbo)
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
                                TextureTarget.Texture2D, resultTex, 0)
        Dim status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer)
        If status <> FramebufferErrorCode.FramebufferComplete Then
            Try : GL.DeleteTexture(resultTex) : Catch : End Try
            resultTex = 0
            resultFbo = 0
            Return False
        End If
        Return True
    End Function

    ''' <summary>Resuelve UNA location y registra el nombre si el driver devuelve -1. Existe para que la
    ''' guarda tenga la lista SIN repetir los 45 nombres en una segunda tabla, que es como esa tabla se
    ''' desincroniza del bloque real.</summary>
    Private Function UniLoc(program As Integer, name As String, missing As List(Of String)) As Integer
        Dim l = GL.GetUniformLocation(program, name)
        If l < 0 Then missing.Add(name)
        Return l
    End Function

    Private ReadOnly _uniMissing As New List(Of String)
    Private ReadOnly _uniWarnLock As New Object()
    Private _uniformsMissingWarning As String = Nothing

    ''' <summary>Primer link cuyo shader no expuso alguna de las 45 locations, o Nothing. Latcheado y
    ''' always-on (NO por Logger, que esta apagado en release): mismo criterio que
    ''' <c>FaceTintConvention.SwapAccumWarning</c>.</summary>
    Public ReadOnly Property UniformsMissingWarning As String
        Get
            SyncLock _uniWarnLock
                Return _uniformsMissingWarning
            End SyncLock
        End Get
    End Property

    Private Sub NoteUniformsMissing(names As String)
        SyncLock _uniWarnLock
            If _uniformsMissingWarning IsNot Nothing Then Return
            _uniformsMissingWarning = "uniforms sin location (-1) en el link del compositor: " & names &
                ". Escribir en -1 es un no-op MUDO: si alguno de estos lo escribe el codigo esperando efecto, " &
                "el shader corre con el default. Un -1 es legitimo solo si NINGUN camino del GLSL lo lee."
        End SyncLock
    End Sub

    Private Sub EnsureCompositorInitialized(state As FaceTintCompositorState)
        If state._program <> 0 AndAlso state._quadVao <> 0 Then Return

        Dim vs = GL.CreateShader(ShaderType.VertexShader)
        GL.ShaderSource(vs, VertexShaderSource)
        GL.CompileShader(vs)
        Dim vsOk As Integer
        GL.GetShader(vs, ShaderParameter.CompileStatus, vsOk)
        If vsOk = 0 Then
            Dim vsErr = GL.GetShaderInfoLog(vs)
            Logger.LogLazy(Function() $"[FACETINT-SHADER] VERTEX compile FAILED: {vsErr}")
            GL.DeleteShader(vs)
            Return
        End If

        Dim fs = GL.CreateShader(ShaderType.FragmentShader)
        GL.ShaderSource(fs, FragmentShaderSource)
        GL.CompileShader(fs)
        Dim fsOk As Integer
        GL.GetShader(fs, ShaderParameter.CompileStatus, fsOk)
        If fsOk = 0 Then
            Dim fsErr = GL.GetShaderInfoLog(fs)
            Logger.LogLazy(Function() $"[FACETINT-SHADER] FRAGMENT compile FAILED: {fsErr}")
            GL.DeleteShader(vs)
            GL.DeleteShader(fs)
            Return
        End If

        state._program = GL.CreateProgram()
        GL.AttachShader(state._program, vs)
        GL.AttachShader(state._program, fs)
        GL.LinkProgram(state._program)
        GL.DetachShader(state._program, vs)
        GL.DetachShader(state._program, fs)
        GL.DeleteShader(vs)
        GL.DeleteShader(fs)

        Dim linkOk As Integer
        GL.GetProgram(state._program, GetProgramParameterName.LinkStatus, linkOk)
        If linkOk = 0 Then
            Dim linkErr = GL.GetProgramInfoLog(state._program)
            Logger.LogLazy(Function() $"[FACETINT-SHADER] PROGRAM link FAILED: {linkErr}")
            GL.DeleteProgram(state._program)
            state._program = 0
            Return
        End If
        Logger.LogLazy(Function() $"[FACETINT-SHADER] program linked OK id={state._program}")

        state._uPrevLoc = UniLoc(state._program, "uPrev", _uniMissing)
        state._uLayerLoc = UniLoc(state._program, "uLayer", _uniMissing)
        state._uBaseLoc = UniLoc(state._program, "uBase", _uniMissing)
        state._uLayerDiffuseAlphaLoc = UniLoc(state._program, "uLayerDiffuseAlpha", _uniMissing)
        state._uHasDiffuseMaskLoc = UniLoc(state._program, "uHasDiffuseMask", _uniMissing)
        state._uColorLoc = UniLoc(state._program, "uColor", _uniMissing)
        state._uOpacityLoc = UniLoc(state._program, "uOpacity", _uniMissing)
        state._uBlendOpLoc = UniLoc(state._program, "uBlendOp", _uniMissing)
        state._uLayerKindLoc = UniLoc(state._program, "uLayerKind", _uniMissing)
        state._uChannelLoc = UniLoc(state._program, "uChannel", _uniMissing)
        state._uHairLutLoc = UniLoc(state._program, "uHairLut", _uniMissing)
        state._uPaletteRowLoc = UniLoc(state._program, "uPaletteRow", _uniMissing)
        state._uUseHairPaletteLoc = UniLoc(state._program, "uUseHairPalette", _uniMissing)
        state._uForceOpaqueAlphaLoc = UniLoc(state._program, "uForceOpaqueAlpha", _uniMissing)
        state._uForceUniformColorLoc = UniLoc(state._program, "uForceUniformColor", _uniMissing)
        state._uTexTimesColorLoc = UniLoc(state._program, "uTexTimesColor", _uniMissing)
        state._uFgTintFoldLoc = UniLoc(state._program, "uFgTintFold", _uniMissing)
        state._uFgTintOffLoc = UniLoc(state._program, "uFgTintOff", _uniMissing)
        state._uFgTintAmpLoc = UniLoc(state._program, "uFgTintAmp", _uniMissing)
        state._uFoldDetailLoc = UniLoc(state._program, "uFoldDetail", _uniMissing)
        state._uHasFoldDetailLoc = UniLoc(state._program, "uHasFoldDetail", _uniMissing)
        state._uPaletteMaskChannelLoc = UniLoc(state._program, "uPaletteMaskChannel", _uniMissing)
        state._uWorkingSpaceLoc = UniLoc(state._program, "uWorkingSpace", _uniMissing)
        state._uSrcSpaceLoc = UniLoc(state._program, "uSrcSpace", _uniMissing)
        state._uOutputSpaceLoc = UniLoc(state._program, "uOutputSpace", _uniMissing)
        state._uAccumSpaceLoc = UniLoc(state._program, "uAccumSpace", _uniMissing)
        state._uCompositeSpaceLoc = UniLoc(state._program, "uCompositeSpace", _uniMissing)
        state._uMaskConvFullLoc = UniLoc(state._program, "uMaskConvFull", _uniMissing)
        state._uModeLoc = UniLoc(state._program, "uMode", _uniMissing)
        state._uSoftLightLoc = UniLoc(state._program, "uSoftLight", _uniMissing)
        state._uFrameworkLoc = UniLoc(state._program, "uFramework", _uniMissing)
        state._uPreToneSkinLoc = UniLoc(state._program, "uPreToneSkin", _uniMissing)
        state._uSkinMaskLoc = UniLoc(state._program, "uSkinMask", _uniMissing)
        state._uSkinColorLoc = UniLoc(state._program, "uSkinColor", _uniMissing)
        state._uSkinOpacityLoc = UniLoc(state._program, "uSkinOpacity", _uniMissing)
        state._uSkinWsLoc = UniLoc(state._program, "uSkinWs", _uniMissing)
        state._uSkinCsLoc = UniLoc(state._program, "uSkinCs", _uniMissing)
        state._uSkinSsLoc = UniLoc(state._program, "uSkinSs", _uniMissing)
        state._uSkinOsLoc = UniLoc(state._program, "uSkinOs", _uniMissing)
        state._uSkinBopLoc = UniLoc(state._program, "uSkinBop", _uniMissing)
        state._uSkinSlLoc = UniLoc(state._program, "uSkinSl", _uniMissing)
        state._uSkinMcLoc = UniLoc(state._program, "uSkinMc", _uniMissing)
        state._uSkinMaskChLoc = UniLoc(state._program, "uSkinMaskCh", _uniMissing)
        state._uTargetSizeLoc = UniLoc(state._program, "uTargetSize", _uniMissing)
        state._uDownsizeFromMip0Loc = UniLoc(state._program, "uDownsizeFromMip0", _uniMissing)

        ' =========================================================================================
        ' GUARDA DE UNIFORMS -- UNA VEZ POR LINK, aca, donde el bloque de locations esta completo.
        ' =========================================================================================
        ' POR QUE HACE FALTA: `GL.Uniform*(-1, ...)` es un NO-OP MUDO. Si un uniform se renombra en el
        ' GLSL, o el compilador lo elimina porque su rama quedo muerta, el codigo lo sigue "escribiendo" y
        ' el shader corre con el valor por defecto. No falla nada: sale una imagen, DISTINTA. Hasta hoy no
        ' habia NI UNA guarda sobre las 45 locations.
        ' Un -1 NO es necesariamente un bug: el compilador GLSL elimina los uniforms que ningun camino
        ' lee, y eso es legitimo. Por eso esto REPORTA (latcheado, una vez) en vez de abortar.
        ' MEDIDO 2026-08-01 sobre un link REAL (corrida -GpuParity, 23 NPCs, 21 imagenes comparadas):
        ' NINGUNA de las 45 quedo en -1. O sea que hoy la lista de "requeridos" son las 45, y CUALQUIER -1
        ' que aparezca es una regresion — un uniform renombrado en el GLSL o una rama que quedo muerta.
        ' Se reporta en vez de abortar por el mismo criterio que SwapAccumWarning: el bake sigue y el aviso
        ' sale SIEMPRE (por `log()`, no por Logger, que en release esta apagado).
        If _uniMissing.Count > 0 Then
            NoteUniformsMissing(String.Join(", ", _uniMissing))
        End If
        _uniMissing.Clear()

        Dim quadVerts() As Single = {
            -1.0F, -1.0F,
             1.0F, -1.0F,
            -1.0F, 1.0F,
            -1.0F, 1.0F,
             1.0F, -1.0F,
             1.0F, 1.0F
        }
        state._quadVao = GL.GenVertexArray()
        state._quadVbo = GL.GenBuffer()
        GL.BindVertexArray(state._quadVao)
        GL.BindBuffer(BufferTarget.ArrayBuffer, state._quadVbo)
        GL.BufferData(BufferTarget.ArrayBuffer, quadVerts.Length * 4, quadVerts, BufferUsageHint.StaticDraw)
        GL.EnableVertexAttribArray(0)
        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, False, 2 * 4, 0)
        GL.BindBuffer(BufferTarget.ArrayBuffer, 0)
        GL.BindVertexArray(0)
    End Sub


    ''' <summary>Convierte una textura GL de <paramref name="fromSpace"/> a <paramref name="toSpace"/>
    Public Function ConvertTextureSpace(state As FaceTintCompositorState, srcTexId As Integer,
                                        width As Integer, height As Integer,
                                        fromSpace As Integer, toSpace As Integer) As Integer
        ArgumentNullException.ThrowIfNull(state)
        If srcTexId = 0 OrElse width <= 0 OrElse height <= 0 Then Return 0

        ' ESTA FUNCION TENIA LA CAPTURA/RESTAURACION ESCRITA A MANO, campo por campo, duplicando a
        ' SaveGlState/RestoreGlState (200 lineas mas arriba, EN ESTE MISMO ARCHIVO). Los tres ENABLES
        ' faltaban en la copia: la funcion los apagaba y el Finally restauraba todo MENOS ellos. Se
        ' dispara en el camino vivo —ConvertChannelIfNeeded corre en cada composite del editor de cara—
        ' asi que cada tick de slider dejaba DEPTH_TEST y BLEND apagados; quedaba tapado porque
        ' ApplyMaterial los re-fija por material en el frame siguiente, pero eso es suerte, no diseño, y a
        ' SCISSOR_TEST no lo re-fija nadie.
        ' El arreglo NO es completar la copia sino BORRARLA: completarla deja doce campos transcriptos
        ' que vuelven a divergir en cuanto alguien agregue ColorMask o BlendFunc al snapshot. Una sola ley.
        ' Y se toma ANTES de EnsureCompositorInitialized: esa funcion, en su PRIMERA llamada, bindea y
        ' desbindea VAO/VBO, asi que capturar despues leia prevVao = 0 y despues "restauraba" ese 0 encima
        ' del VAO del llamador.
        Dim snap = SaveGlState()
        Dim outTex As Integer = 0
        Dim outFbo As Integer = 0
        Try
            EnsureCompositorInitialized(state)
            If state._program = 0 OrElse state._quadVao = 0 Then Return 0
            If Not AllocateResultTextureAndFbo(state, width, height, outTex, outFbo) Then Return 0
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, outFbo)
            GL.Viewport(0, 0, width, height)
            GL.Disable(EnableCap.DepthTest)
            GL.Disable(EnableCap.ScissorTest)
            GL.Disable(EnableCap.Blend)
            GL.UseProgram(state._program)
            GL.BindVertexArray(state._quadVao)
            ' ACA es donde el pase RESAMPLEA: uMode=2 convierte espacio Y cambia de tamaño (es el seed del
            ' acumulador y el resize por canal). fetchAt compara el tamaño de la fuente contra este target y
            ' aplica el bilineal del CPU cuando difieren. Ver ComposeOntoFaceTexture.
            GL.Uniform2(state._uTargetSizeLoc, width, height)
            GL.Uniform1(state._uDownsizeFromMip0Loc, If(FaceTintCpuCompositor.DownsizeFromMip0, 1, 0))   ' MISMO valor que la ley del CPU
            GL.Uniform1(state._uModeLoc, 2)
            GL.Uniform1(state._uSrcSpaceLoc, fromSpace)
            GL.Uniform1(state._uOutputSpaceLoc, toSpace)
            ' OBLIGATORIO: uMode=2 es el conversor GENERICO de espacios y comparte el shader con el SEED del
            ' acumulador (la misma linea `cvt(prev, uSrcSpace, uAccumSpace)`). Para este pase el destino es
            ' `toSpace`, NO el AccumSpace de la convencion: aca no se esta sembrando un acumulador, se esta
            ' convirtiendo una textura de A a B. Sin esta linea el pase convertiria al espacio del acumulador y
            ' el render quedaria en el espacio equivocado en cuanto AccumSpace deje de ser OutputSpace.
            GL.Uniform1(state._uAccumSpaceLoc, toSpace)
            GL.ActiveTexture(TextureUnit.Texture0)
            GL.BindTexture(TextureTarget.Texture2D, srcTexId)
            GL.Uniform1(state._uPrevLoc, 0)
            GL.DrawArrays(PrimitiveType.Triangles, 0, 6)
            GL.BindTexture(TextureTarget.Texture2D, 0)
        Catch ex As Exception
            If outTex <> 0 Then Try : GL.DeleteTexture(outTex) : Catch : End Try
            outTex = 0
            Dim msg = ex.Message
            Logger.LogLazy(Function() $"[FACETINT-CONVERT] space convert failed ({msg})")
        Finally
            ' outFbo is the persistent scratch container (state-owned, reused across calls) — NOT
            ' deleted here. It lives until state.Dispose().
            RestoreGlState(snap)
            ' [AUDIT-GLSTATE] valida que los tres enables vuelven. Si alguno entra en True, ANTES se
            ' perdia; que aparezca en el log es la prueba de que el caso se ejercito de verdad.
            If Logger.Enabled Then
                Dim d = snap.WasDepth, sc = snap.WasScissor, b = snap.WasBlend
                Logger.LogLazy(Function() $"[AUDIT-GLSTATE] ConvertTextureSpace restaura depth={d} scissor={sc} blend={b}")
            End If
        End Try
        Return outTex
    End Function

    ''' <summary>Per-channel result of <see cref="ApplyFaceTintPipeline"/>: the GL texture ID
    ''' that came out of swap+compose (or the original input ID when no work was done on that
    ''' channel) and a flag saying whether the ID is a fresh texture the caller now owns
    ''' (must be deleted) or just a passthrough of the input. The two consumers handle the
    ''' "fresh" flag differently: the live render swaps the new ID into Textures_Dictionary
    ''' and deletes the old one; the offline bake reads the new ID, encodes to disk, and
    ''' deletes it itself.</summary>
    Public Class FaceTintPipelineChannelResult
        Public Property TextureId As Integer
        Public Property IsFresh As Boolean
        ''' <summary>Tamaño del resultado del canal = target de resolución (o nativo si Inherit). El caller
        ''' (bake) lee back a ESTE tamaño, no al del source. 0 = no seteado (usar el nativo del caller).</summary>
        Public Property Width As Integer
        Public Property Height As Integer
    End Class

    ''' <summary>Aggregate result of <see cref="ApplyFaceTintPipeline"/>: one entry per channel
    ''' (Diffuse / Normal / Specular). Channels whose input ID was 0 come back as IsFresh=False
    ''' TextureId=0 (no work attempted).</summary>
    Public Class FaceTintPipelineResult
        Public Property Diffuse As FaceTintPipelineChannelResult
        Public Property Normal As FaceTintPipelineChannelResult
        Public Property Specular As FaceTintPipelineChannelResult
        ''' <summary>True si el pase final AccumSpace-&gt;OutputSpace no se pudo ejecutar en algun canal (fallo
        ''' de asignacion GL). Cuando pasa, ESE canal queda en AccumSpace en vez de OutputSpace: la textura es
        ''' consumible pero tiene la gamma corrida. Se MARCA en vez de degradarse en silencio — un instrumento
        ''' de paridad tiene que poder fallar la corrida en vez de reportar una divergencia como si fuera del
        ''' compositor. Con el default (AccumSpace == OutputSpace) el pase ni se intenta y esto es siempre False.</summary>
        Public Property SpaceConversionFailed As Boolean
    End Class

    ''' <summary>Aplica el pipeline de facetint (region-swap -> compose de tints) sobre un triplete de texturas
    ''' GL y devuelve el resultado por canal. Es la fuente unica del render en vivo y del bake offline: ninguno
    ''' replica la orquestacion, solo difieren en como consumen la salida (el render intercambia los ids en el
    ''' Textures_Dictionary; el bake hace GetTexImage, encodea a DDS y borra los ids frescos).
    ''' <para>El QNAM/skin-tone de la cara se compone ACA como capa sintetica slot-12, secuenciada en el rango
    ''' del motor junto a las demas, NO como post-pase. El tono del CUERPO se maneja aparte y no se hornea
    ''' nunca: el shader lo softlightea en render desde el material.</para>
    ''' <para>No toca ningun diccionario, modelo ni NIF: es GL puro sobre el state + cache provistos, que deben
    ''' ser validos para el contexto actual. IsFresh=True en los canales donde swap/compose produjo textura
    ''' nueva (el caller la posee); False cuando nada aporto y se devuelve el id de entrada verbatim, que el
    ''' caller NO debe borrar. DEBE correr en el hilo GL con el contexto duenio actual.</para>
    ''' <para>â›” SYNC: CPU/GPU compositor - este es el camino GL y su espejo es
    ''' <c>FaceTintCpuCompositor.ComposeChannelCpu</c> / <c>ComposeOne</c>. Los dos leen sus parametros del
    ''' MISMO <c>FaceTintConvention.ResolveConvention</c>, por eso la paridad es por construccion. Duele si
    ''' diverge porque el BAKE corre 100 % CPU y el RENDER por aca, asi que un barrido validaria un camino que
    ''' el usuario nunca ve. Ver 50-facetint-leyes-y-compositor.</para></summary>
    Public Function ApplyFaceTintPipeline(state As FaceTintCompositorState,
                                          cache As FaceTintTextureCache,
                                          srcDiffuseId As Integer,
                                          srcNormalId As Integer,
                                          srcSpecId As Integer,
                                          width As Integer,
                                          height As Integer,
                                          layers As IList(Of FaceTintLayerInput),
                                          swaps As IList(Of FaceRegionSwapInput),
                                          cpuMirror As FaceTintConvention.FaceTintCpuMirrorCapability,
                                          Optional resolution As FaceTintConvention.FaceTintResolutionSettings = Nothing,
                                          Optional baseDiffuseIsLinearOnGpu As Boolean = False,
                                          Optional headDiffuseAlphaTest As Boolean = False,
                                          Optional stage As FaceTintConvention.FaceTintStage? = Nothing) As FaceTintPipelineResult
        ' `stage` va OPCIONAL Y AL FINAL a propósito: así los call sites del otro repositorio (que es un repo
        ' git independiente, sin commit atómico cruzado) no necesitan una sola edición. Sin valor reproduce
        ' exactamente lo que hacía antes de existir el eje.
        ' ES NULLABLE Y NO `= TintDiffuse`. El default fijo era ADEMÁS incorrecto para N/S: esta función
        ' compone los TRES canales, y en Normal/Specular la etapa correcta es TintNormalSpecular. Con el default
        ' fijo, el día que alguien empezara a honrar el parámetro, el `_msn` habría resuelto el bucket del
        ' diffuse. Nothing = "el caller no opina" ⇒ cada canal resuelve SU etapa de tint.
        ' Y hasta este cambio el parámetro se ACEPTABA Y SE DESCARTABA: no llegaba a ningún ResolveConvention.
        ArgumentNullException.ThrowIfNull(state)

        ' SYNC: CPU/GPU compositor — el target de resolución es POR CANAL, no el del diffuse. El `_msn` y
        ' el `_s` de una cabeza no tienen por qué medir lo mismo que el `_d` (caso común: 1024² y 512²), y el
        ' CPU compone cada canal a SU tamaño. Usar el del diffuse para los tres dejaba buffers de distinto
        ' largo ⇒ el instrumento de paridad descartaba esos slots y, con GPU encendido, el `_s` ni se
        ' escribía. Se consulta al GL el tamaño real de cada textura fuente; si no existe, se cae al par del
        ' caller.
        ' Las tres fuentes son PROPIEDAD DEL CALLER — en el camino vivo son las MISMAS texturas del render 3D.
        ' Antes se les forzaba acá mip 0 + filtro lineal, sin restaurar: eso pisaba el LinearMipmapLinear y la
        ' anisotropía que el loader les pone a propósito, y quedaba pisado (los parámetros de sampleo viven
        ' adentro del objeto textura). Se veía cuando la original seguía siendo la que se dibuja: canal que
        ' vuelve IsFresh=False (sin swap) u objeto compartido bajo la misma clave.
        ' Ya no hace falta tocar nada: el shader las lee por texelFetch (ver fetchAt), que ignora filtro, wrap,
        ' nivel de mip y anisotropía. El compositor especifica su sampleo en vez de heredarlo del objeto.

        Dim dNat = SourceTextureSize(srcDiffuseId, width, height)
        Dim nNat = SourceTextureSize(srcNormalId, width, height)
        Dim sNat = SourceTextureSize(srcSpecId, width, height)
        Dim dT = ChannelTargetSize(resolution, FaceTintChannel.Diffuse, dNat.W, dNat.H)
        Dim nT = ChannelTargetSize(resolution, FaceTintChannel.Normal, nNat.W, nNat.H)
        Dim sT = ChannelTargetSize(resolution, FaceTintChannel.Specular, sNat.W, sNat.H)

        Dim result As New FaceTintPipelineResult With {
            .Diffuse = New FaceTintPipelineChannelResult With {.TextureId = srcDiffuseId, .IsFresh = False, .Width = dT.W, .Height = dT.H},
            .Normal = New FaceTintPipelineChannelResult With {.TextureId = srcNormalId, .IsFresh = False, .Width = nT.W, .Height = nT.H},
            .Specular = New FaceTintPipelineChannelResult With {.TextureId = srcSpecId, .IsFresh = False, .Width = sT.W, .Height = sT.H}
        }

        If width <= 0 OrElse height <= 0 Then Return result

        ' SEED del acumulador. El del DIFFUSE vive en G22, y la conversión depende del espacio REAL en que
        ' llega la base al GL:
        ' - base ya lineal en GPU (SRV sRGB del render): encode-only Linear→G22. NO aplicar srgbToLin acá
        '    o se hace DOBLE DECODE, porque el render ya decodeó. Es el caso LIVE.
        '  - base cruda (bake/CLI, cargada UNORM): Srgb→G22, o sea decode + encode. Es el camino byte-exact.
        ' N/S van lineales, sin conversión.
        ' SYNC: CPU/GPU compositor — el target del seed es AccumSpace, NO OutputSpace: el shader interpreta
        ' `prev` y `uBase` como AccumSpace en TODAS sus ramas, así que sembrar en OutputSpace produce un
        ' buffer MAL ETIQUETADO (cara lavada). El CPU siembra al acumulador igual. Con el default los dos
        ' espacios coinciden, así que esto es un no-op salvo que se separen.
        Dim accSpD As Integer = CInt(FaceTintConvention.AccumSpaceForChannel(FaceTintChannel.Diffuse, cpuMirror))
        Dim outSpD As Integer = CInt(FaceTintConvention.OutputSpaceForChannel(FaceTintChannel.Diffuse))
        ' Caso live (baseDiffuseIsLinearOnGpu): el GPU ya decodeó el SRV sRGB → la base entra LINEAL (0).
        If baseDiffuseIsLinearOnGpu Then
            ConvertChannelIfNeeded(result.Diffuse, state, dT.W, dT.H, dNat.W, dNat.H, 0, accSpD)
        ElseIf SeedConventionIs_G22 Then
            ConvertChannelIfNeeded(result.Diffuse, state, dT.W, dT.H, dNat.W, dNat.H, SeedDiffuseSrcSpaceValue, accSpD)
        Else
            ' Seed CRUDO: su espacio implicito es OutputSpace ⇒ outSp->accSp. MISMA regla y misma justificacion
            ' que el Else del seed del CPU (ver FaceTintCpuCompositor). No-op exacto con el default.
            ConvertChannelIfNeeded(result.Diffuse, state, dT.W, dT.H, dNat.W, dNat.H, outSpD, accSpD)
        End If
        ' N/S: seed crudo tambien, y por lo tanto la MISMA conversion implicita outSp->accSp que el diffuse
        ' crudo de arriba. El CPU hace exactamente esto (su Else cubre N/S siempre, porque isD=False).
        ConvertChannelIfNeeded(result.Normal, state, nT.W, nT.H, nNat.W, nNat.H,
                               CInt(FaceTintConvention.OutputSpaceForChannel(FaceTintChannel.Normal)),
                               CInt(FaceTintConvention.AccumSpaceForChannel(FaceTintChannel.Normal, cpuMirror)))
        ConvertChannelIfNeeded(result.Specular, state, sT.W, sT.H, sNat.W, sNat.H,
                               CInt(FaceTintConvention.OutputSpaceForChannel(FaceTintChannel.Specular)),
                               CInt(FaceTintConvention.AccumSpaceForChannel(FaceTintChannel.Specular, cpuMirror)))

        ' --- Region-swap pre-pass (no-op if swaps empty / no contribution to a channel) ---
        If swaps IsNot Nothing AndAlso swaps.Count > 0 Then
            ProcessChannel(result.Diffuse, FaceTintChannel.Diffuse, state, cache, dT.W, dT.H, Nothing, swaps, cpuMirror, headDiffuseAlphaTest)
            ProcessChannel(result.Normal, FaceTintChannel.Normal, state, cache, nT.W, nT.H, Nothing, swaps, cpuMirror)
            ProcessChannel(result.Specular, FaceTintChannel.Specular, state, cache, sT.W, sT.H, Nothing, swaps, cpuMirror)
        End If

        ' --- Tint compose ---
        ' `stage` VIAJA HASTA EL LOOP DE CAPAS. Antes moría acá: la firma lo aceptaba y ningún ProcessChannel
        ' lo recibía, así que el bucket lo elegía siempre la etapa de tint del canal. Nullable ⇒ cuando el caller
        ' no lo pasa el comportamiento es EXACTAMENTE el previo (byte-idéntico), y N/S siguen resolviendo su
        ' propia etapa aunque el caller haya pedido Fold/Overlay (lo gatea ResolveConvention por canal).
        If layers IsNot Nothing AndAlso layers.Count > 0 Then
            ProcessChannel(result.Diffuse, FaceTintChannel.Diffuse, state, cache, dT.W, dT.H, layers, Nothing, cpuMirror, headDiffuseAlphaTest, stage)
            ProcessChannel(result.Normal, FaceTintChannel.Normal, state, cache, nT.W, nT.H, layers, Nothing, cpuMirror, False, stage)
            ProcessChannel(result.Specular, FaceTintChannel.Specular, state, cache, sT.W, sT.H, layers, Nothing, cpuMirror, False, stage)
        End If

        ' --- PASE FINAL AccumSpace -> OutputSpace: UNA SOLA VEZ, ACA, PARA LOS TRES CANALES ---
        ' El compose deja el acumulador en AccumSpace y el consumidor (el DDS del bake y el render) espera
        ' OutputSpace. El CPU hace exactamente esta conversion una vez por canal, en el pack.
        ' â›” POR QUE ACA Y NO EN ProcessChannel: ese se llama DOS VECES por canal (region swaps y tints), asi que
        ' adentro toda cara CON swaps se comeria DOS conversiones de gamma, la segunda sobre un buffer ya
        ' convertido. El acumulador es UNO y cruza las dos fases: su conversion de salida es del PIPELINE.
        ' â›” TAMBIEN CORRE SIN CAPAS NI SWAPS: el seed ya dejo el diffuse en AccumSpace, asi que saltearlo cuando
        ' no hay contribuciones sacaria el canal en el espacio equivocado. El CPU tampoco lo condiciona.
        ' Con el default (AccumSpace == OutputSpace en los 3 canales) es un no-op exacto: el guard de
        ' ConvertChannelIfNeeded ve mismo tamano y mismo espacio y no dibuja nada.
        ConvertAccumToOutputSpace(result, FaceTintChannel.Diffuse, result.Diffuse, state, dT.W, dT.H, cpuMirror)
        ConvertAccumToOutputSpace(result, FaceTintChannel.Normal, result.Normal, state, nT.W, nT.H, cpuMirror)
        ConvertAccumToOutputSpace(result, FaceTintChannel.Specular, result.Specular, state, sT.W, sT.H, cpuMirror)

        ' --- B: salida LIVE del DIFFUSE en LINEAL ---
        ' El render reusa la textura fresca (Rgba32f = float; los float NO tienen decode sRGB) y la samplea
        ' CRUDA. El acumulador del diffuse está en G22 (os); para que el render obtenga LINEAL (su contrato: el
        ' sample del diffuse es albedo lineal) hay que convertir el resultado del diffuse G22→Linear. SOLO en el
        ' path live (baseDiffuseIsLinearOnGpu): el BAKE mantiene G22 (hornea el _d.dds vía GetTexImage, que toma
        ' el byte G22 crudo, = vanilla). N/S ya están en Linear (os=Linear). Sin pérdida (float). Junto con el
        ' seed encode-only, el path live queda lineal-consistente extremo a extremo.
        ' El origen es OutputSpace (no AccumSpace): el pase final de arriba ya corrió, así que a esta altura
        ' el diffuse está SIEMPRE en OutputSpace, tenga el flag prendido o no. Se lee por OutputSpaceForChannel
        ' —el MISMO resolver que usó ese pase como destino— para que el par no se pueda desalinear.
        If baseDiffuseIsLinearOnGpu Then
            ConvertChannelIfNeeded(result.Diffuse, state, dT.W, dT.H, dT.W, dT.H,
                                   CInt(FaceTintConvention.OutputSpaceForChannel(FaceTintChannel.Diffuse)), 0)
        End If

        Return result
    End Function

    ''' <summary>Pase final del acumulador de UN canal: AccumSpace → OutputSpace. Es el espejo GL de la
    ''' conversión que el compositor CPU hace en su pack, y por eso usa el MISMO par de resolvers
    ''' (<see cref="FaceTintConvention.AccumSpaceForChannel"/> / <see cref="FaceTintConvention.OutputSpaceForChannel"/>)
    ''' con los mismos argumentos: origen y destino no pueden desalinearse.
    ''' <para>No-op EXACTO cuando los dos espacios coinciden (el caso del default): no se dibuja nada.</para>
    ''' <para>Si la conversión falla (asignación GL), NO se degrada en silencio: se loguea y se marca
    ''' <see cref="FaceTintPipelineResult.SpaceConversionFailed"/> para que un instrumento de paridad pueda
    ''' invalidar la corrida en vez de atribuirle la divergencia al compositor.</para></summary>
    Private Sub ConvertAccumToOutputSpace(result As FaceTintPipelineResult,
                                          channel As FaceTintChannel,
                                          ch As FaceTintPipelineChannelResult,
                                          state As FaceTintCompositorState,
                                          width As Integer, height As Integer,
                                          cpuMirror As FaceTintConvention.FaceTintCpuMirrorCapability)
        If ch Is Nothing OrElse ch.TextureId = 0 Then Return
        Dim accSp As Integer = CInt(FaceTintConvention.AccumSpaceForChannel(channel, cpuMirror))
        Dim outSp As Integer = CInt(FaceTintConvention.OutputSpaceForChannel(channel))
        If accSp = outSp Then Return

        Dim converted = ConvertTextureSpace(state, ch.TextureId, width, height, accSp, outSp)
        If converted = 0 Then
            result.SpaceConversionFailed = True
            Logger.LogLazy(Function() $"[FACETINT] FINAL PASS FAILED channel={channel} {accSp}->{outSp}: the accumulator stays in AccumSpace (gamma will be off). Run marked invalid.")
            Return
        End If
        Dim oldId = ch.TextureId, oldFresh = ch.IsFresh
        ch.TextureId = converted
        ch.IsFresh = True
        If oldFresh Then Try : GL.DeleteTexture(oldId) : Catch : End Try
    End Sub

    ''' <summary>Tamaño REAL de una textura fuente del GL (mip 0). Se consulta al driver en vez de asumir
    ''' el par del caller, porque los tres canales de una cabeza pueden medir distinto y el CPU compone cada
    ''' uno al SUYO. Devuelve el fallback si la textura es 0 o el driver devuelve algo no positivo — nunca
    ''' un tamaño invalido, que es peor que el fallback.</summary>
    Private Function SourceTextureSize(texId As Integer, fbW As Integer, fbH As Integer) As (W As Integer, H As Integer)
        If texId = 0 Then Return (fbW, fbH)
        Try
            Dim prev As Integer = GL.GetInteger(GetPName.TextureBinding2D)
            GL.BindTexture(TextureTarget.Texture2D, texId)
            Dim tw As Integer = 0, th As Integer = 0
            GL.GetTexLevelParameter(TextureTarget.Texture2D, 0, GetTextureParameter.TextureWidth, tw)
            GL.GetTexLevelParameter(TextureTarget.Texture2D, 0, GetTextureParameter.TextureHeight, th)
            GL.BindTexture(TextureTarget.Texture2D, prev)
            If tw > 0 AndAlso th > 0 Then Return (tw, th)
        Catch
        End Try
        Return (fbW, fbH)
    End Function

    ''' <summary>Tamaño target de un canal: nativo si Inherit (o resolution Nothing), si no el del enum
    ''' (cuadrado). = FaceTintCpuCompositor (misma regla) -> GL y CPU resuelven el mismo tamaño.</summary>
    Private Function ChannelTargetSize(resolution As FaceTintConvention.FaceTintResolutionSettings,
                                       channel As FaceTintChannel, nativeW As Integer, nativeH As Integer) As (W As Integer, H As Integer)
        Dim r = If(resolution Is Nothing, FaceTintConvention.FaceTintChannelResolution.Inherit, resolution.ForChannel(channel))
        If r = FaceTintConvention.FaceTintChannelResolution.Inherit Then Return (nativeW, nativeH)
        Dim sz = FaceTintConvention.ResolveResolutionSize(r, Math.Min(nativeW, nativeH))
        Return (sz, sz)
    End Function

    ''' Convierte + resizea un canal al target. (fromSpace,toSpace)=(0,0) -> solo resize (N/S, linear->linear).
    ''' (1,2) -> Srgb->G22 ADEMÁS del resize (D). No-op SOLO si no hay resize NI conversión.
    Private Sub ConvertChannelIfNeeded(ch As FaceTintPipelineChannelResult, state As FaceTintCompositorState,
                                   targetW As Integer, targetH As Integer, nativeW As Integer, nativeH As Integer,
                                   fromSpace As Integer, toSpace As Integer)
        If ch.TextureId = 0 Then Return
        If targetW = nativeW AndAlso targetH = nativeH AndAlso fromSpace = toSpace Then Return  ' <- el guard ahora incluye el espacio
        Dim converted = ConvertTextureSpace(state, ch.TextureId, targetW, targetH, fromSpace, toSpace)
        If converted = 0 Then Return
        Dim oldId = ch.TextureId, oldFresh = ch.IsFresh
        ch.TextureId = converted
        ch.IsFresh = True
        If oldFresh Then Try : GL.DeleteTexture(oldId) : Catch : End Try
    End Sub

    ''' <summary>Run one channel through either the region-swap pre-pass (when
    ''' <paramref name="swaps"/> is non-Nothing) or the tint compose (when
    ''' <paramref name="layers"/> is non-Nothing). Updates <paramref name="ch"/> in place: if
    ''' the compositor produced a new texture, the channel result switches to that ID and the
    ''' previous fresh ID (if any) is deleted; if the compositor returned 0/no-op, the channel
    ''' is left untouched. Source IDs (IsFresh=False) are never deleted — those belong to the
    ''' caller, who is responsible for their lifetime.</summary>
    Private Sub ProcessChannel(ch As FaceTintPipelineChannelResult,
                               channel As FaceTintChannel,
                               state As FaceTintCompositorState,
                               cache As FaceTintTextureCache,
                               width As Integer, height As Integer,
                               layers As IList(Of FaceTintLayerInput),
                               swaps As IList(Of FaceRegionSwapInput),
                               cpuMirror As FaceTintConvention.FaceTintCpuMirrorCapability,
                               Optional headDiffuseAlphaTest As Boolean = False,
                               Optional stage As FaceTintConvention.FaceTintStage? = Nothing)
        If ch.TextureId = 0 Then
            Return
        End If
        Dim newId As Integer
        If swaps IsNot Nothing Then
            ' El pre-pass de swaps NO recibe `stage`: su etapa es RegionSwap por definición y la resuelve
            ' ApplyRegionSwapsOntoFaceTexture. Pasarle la del caller le daría el bucket equivocado.
            newId = ApplyRegionSwapsOntoFaceTexture(state, ch.TextureId, width, height, swaps, channel, cpuMirror, cache)
        Else
            newId = ComposeOntoFaceTexture(state, ch.TextureId, width, height, layers, channel, cpuMirror, cache, headDiffuseAlphaTest, stage)
        End If
        If newId = 0 OrElse newId = ch.TextureId Then Return

        ' ACA NO VA EL PASE FINAL AccumSpace->OutputSpace. Esta funcion corre DOS VECES por canal (una para
        ' region swaps y otra para tints) y el acumulador es UNO SOLO que cruza las dos fases: convertirlo acá
        ' lo convertiria dos veces en toda cara con swaps. Vive en ApplyFaceTintPipeline, una sola vez al cierre.
        Dim oldId = ch.TextureId
        Dim oldFresh = ch.IsFresh
        ch.TextureId = newId
        ch.IsFresh = True
        If oldFresh Then
            Try : GL.DeleteTexture(oldId) : Catch : End Try
        End If
    End Sub

End Module

''' <summary>Cache de proceso de DDS decodificadas a texturas GL, indexada por una string opaca que provee el
''' caller (normalmente el path normalizado), para reusar los objetos de textura entre llamadas en vez de
''' decodificar + subir + borrar en cada invocacion.
''' <para>Ciclo de vida: el caller tiene UNA instancia por vida del contexto GL; el compositor lee y escribe por
''' <see cref="GetOrLoadBatch"/>; las entradas NO las borra el Finally por llamada del compositor, sobreviven
''' para reuso (las de ping-pong/FBO y las ad-hoc siguen el camino alocar-y-borrar de siempre). El caller DEBE
''' llamar <see cref="Clear"/> cuando cambian los bytes de origen (rebuild del FilesDictionary, mount/unmount de
''' BA2, recarga de plugins) y ANTES del teardown del contexto: si no, se filtran handles de textura GL.</para>
''' <para>Thread safety: solo desde el hilo GL, igual que el compositor. Sin locking interno.</para></summary>
Public NotInheritable Class FaceTintTextureCache

    ''' <summary>Backing dictionary. Keys are opaque caller-supplied strings (we just compare
    ''' them); values are the same Texture_Loaded_Class entries the compositor would otherwise
    ''' allocate and discard per-call.</summary>
    Private ReadOnly _entries As New Dictionary(Of String, PreviewModel.Texture_Loaded_Class)(StringComparer.OrdinalIgnoreCase)

    ''' <summary>Number of cached GL textures currently alive.</summary>
    Public ReadOnly Property Count As Integer
        Get
            Return _entries.Count
        End Get
    End Property

    ''' <summary>Resuelve un lote de pedidos (key -> bytes) separando hits de misses. Los misses van por
    ''' DirectXDDSLoader en UNA sola llamada nativa y se guardan; los hits salen del cache intactos. Devuelve un
    ''' diccionario nuevo indexado por las keys del caller.
    ''' <para>Las entradas con <paramref name="isCacheable"/>=False NO entran al cache persistente y el caller
    ''' debe borrarlas despues de usarlas; las True sobreviven a la llamada. El compositor usa True para las
    ''' keys que provee el caller (path de textura) y False para keys sinteticas por llamada, asi el mismo
    ''' batch loader sirve a los dos ciclos de vida.</para></summary>
    Public Function GetOrLoadBatch(keys As IList(Of String), bytes As IList(Of Byte()), isCacheable As IList(Of Boolean)) As Dictionary(Of String, PreviewModel.Texture_Loaded_Class)
        Dim result As New Dictionary(Of String, PreviewModel.Texture_Loaded_Class)(StringComparer.OrdinalIgnoreCase)
        If keys Is Nothing OrElse keys.Count = 0 Then Return result

        Dim missKeys As New List(Of String)
        Dim missBytes As New List(Of Byte())
        Dim missCacheable As New List(Of Boolean)
        ' Guard against duplicate keys in the request: the loader returns a dict keyed by key,
        ' so a key requested twice would generate two GL textures yet keep only one entry,
        ' leaking the other. Upload each unique miss key once; the dict result is reused by all
        ' referencing callers. Same bytes per key → pixel-identical.
        Dim seenMiss As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)

        For i As Integer = 0 To keys.Count - 1
            Dim k = keys(i)
            If String.IsNullOrEmpty(k) Then Continue For
            Dim b = bytes(i)
            If b Is Nothing OrElse b.Length = 0 Then Continue For

            Dim cacheable As Boolean = (i < isCacheable.Count) AndAlso isCacheable(i)
            If cacheable Then
                Dim hit As PreviewModel.Texture_Loaded_Class = Nothing
                If _entries.TryGetValue(k, hit) AndAlso hit IsNot Nothing AndAlso hit.Texture_ID <> 0 Then
                    result(k) = hit
                    Continue For
                End If
            End If

            If Not seenMiss.Add(k) Then Continue For

            missKeys.Add(k)
            missBytes.Add(b)
            missCacheable.Add(cacheable)
        Next

        If missKeys.Count > 0 Then
            ' srgb=False: texturas del compositor crudas; el decode lo hace el shader por convención (ss).
            Dim loaded = DirectXDDSLoader.Load_And_GenerateOpenGLTextures_Memory(missKeys.ToArray(), missBytes.ToArray(), GlDecodeUseCompress, True, New Boolean(missKeys.Count - 1) {})
            ' NO se le tocan los parametros de sampleo a lo que sale del loader (antes se forzaba aca mip 0 y
            ' ClampToEdge). El shader lee TODO por texelFetch con coordenada entera y clamp explicito (ver
            ' fetchAt), asi que filtro, wrap, mip y anisotropia de la textura son irrelevantes para el compose
            ' — y estas entradas viven en un cache compartido, donde pisarlas viajaba a otros consumidores.
            If loaded IsNot Nothing Then
                For i As Integer = 0 To missKeys.Count - 1
                    Dim k = missKeys(i)
                    Dim entry As PreviewModel.Texture_Loaded_Class = Nothing
                    If Not loaded.TryGetValue(k, entry) Then Continue For
                    If entry Is Nothing OrElse entry.Texture_ID = 0 Then Continue For
                    result(k) = entry
                    If missCacheable(i) Then _entries(k) = entry
                Next
            End If
        End If

        Return result
    End Function

    ''' <summary>True iff the key has a usable cached entry. Used by the compositor's Finally
    ''' block to decide whether a per-call entry is owned by the cache (do not delete) or by
    ''' the call (delete as before).</summary>
    Public Function IsCached(key As String) As Boolean
        If String.IsNullOrEmpty(key) Then Return False
        Dim e As PreviewModel.Texture_Loaded_Class = Nothing
        If Not _entries.TryGetValue(key, e) Then Return False
        Return e IsNot Nothing AndAlso e.Texture_ID <> 0
    End Function

    ''' <summary>Delete every cached GL texture and forget its key. MUST be called on the GL
    ''' thread. Call this before the GL context is torn down or whenever the underlying byte
    ''' sources change (FilesDictionary rebuild) so a stale entry cannot leak into a new asset
    ''' set.</summary>
    Public Sub Clear()
        For Each kvp In _entries
            Dim e = kvp.Value
            If e IsNot Nothing AndAlso e.Texture_ID <> 0 Then
                Try : OpenTK.Graphics.OpenGL4.GL.DeleteTexture(e.Texture_ID) : Catch : End Try
            End If
        Next
        _entries.Clear()
    End Sub

    ''' <summary>Olvida las claves SIN borrar un solo handle de GL.
    ''' <para>Es para el único caso en que <see cref="Clear"/> no se puede usar: cuando NO se pudo hacer
    ''' current el contexto dueño. Ahí borrar es lo peligroso —los nombres de GL son por contexto y el
    ''' <c>DeleteTexture</c> caería sobre el que esté current, matando texturas vivas de otro preview— pero
    ''' DEJAR LAS ENTRADAS tampoco sirve: <see cref="GetOrLoadBatch"/> sirve el hit sin revalidar nada, así
    ''' que el caché seguiría entregando las texturas del NPC anterior para siempre. Olvidar sin borrar deja
    ''' el caché coherente con su espejo de CPU (que sí se limpia) al precio de no liberar handles.</para>
    ''' <para>Ese precio es casi siempre cero: <c>EnsureContextCurrent</c> sólo falla si el control está
    ''' <c>Nothing</c>/<c>IsDisposed</c> —contexto muerto, los handles ya se fueron con él— o si tiró, en
    ''' cuyo caso el contexto no es usable igual. Devuelve cuántas entradas se olvidaron para que el
    ''' llamador lo pueda loguear: si eso es distinto de 0 seguido, hay un contexto que nunca vuelve.</para></summary>
    Public Function OlvidarSinBorrar() As Integer
        Dim n = _entries.Count
        _entries.Clear()
        Return n
    End Function
End Class

