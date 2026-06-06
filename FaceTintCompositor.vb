Imports OpenTK.Graphics.OpenGL4
Imports OpenTK.Mathematics

' ============================================================================
' FaceTintCompositor — GPU ping-pong compositor that bakes NPC tint layers onto
' a copy of the face diffuse texture.
'
' How it works:
'  1. Caller passes the existing face diffuse GL texture ID + dimensions and a
'     list of layers in application order.
'  2. We allocate two ping-pong FBO textures sized to the diffuse.
'  3. Iteration 0: read from the original face diffuse, write blended result to ping0.
'  4. Iteration N: read from ping(N-1 mod 2), write to ping(N mod 2).
'  5. After all layers, the last-written texture is returned. Caller is responsible
'     for binding it (typically by mutating the model's Textures_Dictionary entry
'     so the existing render path picks it up automatically).
'
' Each layer carries its own blend op (Default / Multiply / Overlay / SoftLight /
' HardLight) and a "kind":
'   PaletteMask  — uLayer is a greyscale mask in .r, the tint comes from uColor.
'   TextureSetDiffuse — uLayer is a pre-coloured RGBA detail texture; .a is coverage.
'
' MUST be called on the GL thread (current context). Returns 0 on failure.
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
Public Enum FaceTintBlendConvention
    Linear = 0       ' coverage = mask * opacity (blend in stored/gamma space)
    SrgbOpacity = 1  ' coverage = sRGB(mask) * opacity (mask shaped by the sRGB transfer)
End Enum

''' <summary>One per-region face texture swap from a RACE Morph Group preset (MPPT TXST).
''' Bethesda's "Arrugado", "Con textura", "Curtido", etc. presets work by swapping the
''' base diffuse/normal/spec inside a single face region (Forehead, Eyes, Nose, Ears,
''' Cheeks, Mouth, Neck) defined by an alpha mask in face UV space. The swap textures
''' come from the preset's MPPT TXST (TX00/TX01/TX07). The mask comes from the Morph
''' Group's MPPK enum, which resolves to a TintTemplateOption in slot 0..6 whose TTET[0]
''' is a 1024x1024 BC1_UNORM grayscale mask.
'''
''' This is a hard replace gated by the mask: inside the white region of the mask the
''' swap texture fully overrides the base; outside it the base is preserved. Applied as
''' a pre-pass before any tint layers so the TTET layers below blend on top of the
''' region-swapped base.</summary>
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
    ''' light them. See <see cref="TakesSkinToneOcclusion"/>.</summary>
    Public Property IsSkinTone As Boolean = False

    ''' <summary>Opt-in for the per-pixel grayscale-to-palette path on the Diffuse channel. When
    ''' True, the shader samples the per-fragment colour from a hair palette LUT instead of the
    ''' authored RGB. The X coordinate depends on the layer kind: PaletteMask uses mask.r (the
    ''' .r channel of the diffuse mask), TextureSet uses <c>dot(layerSample.rgb, vec3(0.299,
    ''' 0.587, 0.114))</c> -- the standard luminance grayscale of the layer's own diffuse.
    ''' The Y coordinate is always <see cref="HairPaletteRow"/> (= CLFM.RemappingIndex). Caller
    ''' must supply <see cref="HairLutDdsBytes"/>; missing LUT bytes silently fall through to
    ''' the default path. No-op on Normal/Specular channels.</summary>
    Public Property UseHairPalette As Boolean = False
    ''' <summary>Force the shader's TextureSet diffuse branch to use the uniform <c>uColor</c>
    ''' instead of the layer's authored RGB, while keeping coverage from the layer's diffuse
    ''' alpha. Used by the brow-tint override when the hair CLFM carries an RGB colour
    ''' (HasColor) -- the layer keeps its shape (alpha) but the colour comes from HCLF. Ignored
    ''' for PaletteMask layers (they already use uColor by default) and on N/S channels.</summary>
    Public Property ForceUniformColor As Boolean = False
    ''' <summary>Hair palette LUT DDS bytes (the same 2D texture the hair shader samples). Rows =
    ''' hair-tone gradients (highlight→shadow). Loaded into a GL texture by the compositor's batch
    ''' loader, sampled at <c>(mask.r, HairPaletteRow)</c> when <see cref="UseHairPalette"/> is True.</summary>
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
    Friend _uWorkingSpaceLoc As Integer = -1
    Friend _uSrcSpaceLoc As Integer = -1
    Friend _uOutputSpaceLoc As Integer = -1
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
    Friend _quadVao As Integer = 0
    Friend _quadVbo As Integer = 0


    ' Persistent ping-pong colour attachments shared by ComposeOntoFaceTexture,
    ' ApplyRegionSwapsOntoFaceTexture and ApplyUniformBlendOntoFaceTexture. Allocated lazily
    ' to (_pingW, _pingH); reused across calls when dims match, re-allocated when dims change.
    ' The "result snapshot" is a fresh texture per call — it carries the final pass output to
    ' the caller, who owns its lifetime. Pings stay private to the state and are only released
    ' on Dispose() or on dim-mismatch re-alloc.
    Friend _pingTex(1) As Integer
    Friend _pingFbo(1) As Integer
    Friend _pingW As Integer = 0
    Friend _pingH As Integer = 0

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
            ' bytes-per-pixel del decode que devuelve el wrapper (ChooseDecompressFormatForSource):
            '   BC1/BC2/BC3/BC7 -> R8G8B8A8(28/29) ó B8G8R8A8(87/88/91/93) = 4 canales
            '   BC5 (normal/spec) -> R8G8_UNORM/SNORM(49/50) = 2 canales ; BC4 -> R8(61/62) = 1 canal
            Dim bpp As Integer = 0
            Select Case fmt
                Case 28, 29, 87, 88, 91, 93 : bpp = 4
                Case 49, 50 : bpp = 2
                Case 61, 62 : bpp = 1
            End Select
            If w <= 0 OrElse h <= 0 OrElse px Is Nothing OrElse bpp = 0 OrElse px.Length < w * h * bpp Then
                Dim p = outPath, dx = fmt, nb = If(px Is Nothing, 0, px.Length)
                Logger.LogLazy(Function() $"[PRISTINE-DUMP] '{System.IO.Path.GetFileName(p)}' formato no soportado (DxgiFinal={dx}, bytes={nb}) -> skip")
                Return
            End If
            Dim isBgra8 = (fmt = 87 OrElse fmt = 88 OrElse fmt = 91 OrElse fmt = 93)
            Dim bgra(w * h * 4 - 1) As Byte
            For i As Integer = 0 To w * h - 1
                Dim o As Integer = i * 4, s As Integer = i * bpp
                Select Case bpp
                    Case 4
                        If isBgra8 Then
                            bgra(o) = px(s) : bgra(o + 1) = px(s + 1) : bgra(o + 2) = px(s + 2) : bgra(o + 3) = px(s + 3)
                        Else
                            bgra(o) = px(s + 2) : bgra(o + 1) = px(s + 1) : bgra(o + 2) = px(s) : bgra(o + 3) = px(s + 3)   ' RGBA -> BGRA
                        End If
                    Case 2   ' R8G8 (BC5, p.ej. normal): R=X, G=Y, B=0, A=255 — igual que el readback GL de un RG texture
                        bgra(o) = 0 : bgra(o + 1) = px(s + 1) : bgra(o + 2) = px(s) : bgra(o + 3) = 255
                    Case 1   ' R8 (BC4): grayscale replicado
                        bgra(o) = px(s) : bgra(o + 1) = px(s) : bgra(o + 2) = px(s) : bgra(o + 3) = 255
                End Select
            Next
            WriteBgraToTga(outPath, bgra, w, h)
        Catch ex As Exception
            Dim p = outPath, msg = ex.Message
            Logger.LogLazy(Function() $"[PRISTINE-DUMP] '{System.IO.Path.GetFileName(p)}' fail: {msg}")
        End Try
    End Sub


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
uniform float uPaletteRow;    // V coordinate into uHairLut when uUseHairPalette=1 (= CLFM.RemappingIndex)
uniform int uForceOpaqueAlpha; // 1 = write opaque alpha (1.0) on the FINAL drawn layer (last pass).
uniform int uWorkingSpace;     // 0=linear 1=srgb 2=g22. Espacio donde corre el blend.
uniform int uSrcSpace;         // 0=linear 1=srgb 2=g22. Espacio del color de la capa (D=srgb, N/S=linear).
uniform int uOutputSpace;      // 0=linear 1=srgb 2=g22. Espacio del acumulador/almacenamiento (D=g22, N/S=linear).
uniform int uCompositeSpace;   // 0=linear 1=srgb 2=g22. Espacio donde corre el COMPOSITE (lerp por cov). Ley gen3: blend en working, lerp en linear. ==uWorkingSpace reduce al modelo previo.
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
// derived-model blend dispatch (uBlendOp: 0=replace 1=mult 2=overlay 3=softlight 4=hardlight, 5..19 estandar)
vec3 blendDispatch(vec3 d, vec3 s){
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
//   base_w  = cvt(prev -> uWorkingSpace)     (prev = acumulador corriente en uOutputSpace)
//   src_w   = cvt(src  -> uWorkingSpace)
//   blended = blendDispatch(base_w, src_w)   (blend en uWorkingSpace)
//   res_c   = cvt(prev->uCompositeSpace) + cov*(cvt(blended->uCompositeSpace) - cvt(prev->uCompositeSpace))
//   final   = cvt(res_c -> uOutputSpace)     (el resultado ES el nuevo prev)
// mask source: PaletteMask -> layer.G ; TextureSet D -> layer.a ; TextureSet N/S -> uLayerDiffuseAlpha.a
// src: PaletteMask -> uColor (o LUT) ; TextureSet -> layer.rgb (o LUT / uColor forzado)
void main() {
    vec4 prevRgba = texture(uPrev, vUV);
    vec3 prev = prevRgba.rgb;
    vec4 layerSample = texture(uLayer, vUV);

    // uMode==1: region swap = alpha-over mix(prev, swap, mask.r * intensity). Es composicion de
    // color por cobertura -> se hace en LINEAR. prev viene en uOutputSpace, swap en uSrcSpace;
    // se convierten a linear, se mezclan, y vuelve a uOutputSpace. mask RAW (.r).
    if (uMode == 1) {
        // Region swap = REPLACE resuelto por FaceTintConvention.ResolveConvention(forSwap) (NO hardcoded):
        // cov = convMask(mask, uMaskConvFull) * op ; compose generico (blend en uWorkingSpace, lerp en
        // uCompositeSpace, storage en uOutputSpace), blended=src (replace). = misma algebra que ComposeOne (CPU).
        // El override de convencion (incl. #If DEBUG full-linear) ahora alcanza tambien los swaps.
        float mask = texture(uLayerDiffuseAlpha, vUV).r;
        float cov = clamp(uOpacity * convMaskFull(mask), 0.0, 1.0);
        vec3 src_w   = cvt(layerSample.rgb, uSrcSpace, uWorkingSpace);
        vec3 base_c  = cvt(prev, uOutputSpace, uCompositeSpace);
        vec3 blend_c = cvt(src_w, uWorkingSpace, uCompositeSpace);   // replace: blended = src_w
        vec3 res_c   = clamp(base_c + cov * (blend_c - base_c), 0.0, 1.0);
        fragColor = vec4(cvt(res_c, uCompositeSpace, uOutputSpace), prevRgba.a);
        return;
    }

    // uMode==2: CONVERT puro de espacio (sin blend, sin mask). Convierte la textura bindeada en uPrev
    // de uSrcSpace a uOutputSpace. Se usa para el SEED del path unico (source sRGB -> acumulador g22 en
    // D) y queda reservado para el camino inverso g22 -> sRGB (flag BakeMode, si el render lo necesita).
    if (uMode == 2) {
        fragColor = vec4(cvt(prev, uSrcSpace, uOutputSpace), prevRgba.a);
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
        if (uUseHairPalette == 1)        srcColor = texture(uHairLut, vec2(layerSample.g, uPaletteRow)).rgb;
        else if (uForceUniformColor == 1) srcColor = uColor;
        else                              srcColor = layerSample.rgb;
        if (uChannel == 0) {
            maskV = layerSample.a;
        } else {
            maskV = (uHasDiffuseMask == 1) ? texture(uLayerDiffuseAlpha, vUV).a
                                           : max(max(layerSample.r, layerSample.g), layerSample.b);
        }
    } else {
        if (uUseHairPalette == 1) srcColor = texture(uHairLut, vec2(layerSample.g, uPaletteRow)).rgb;
        else                      srcColor = uColor;
        maskV = layerSample.g;
    }

    // Pre-tono TakesSkinTone (guard uPreToneSkin): aplica el softlight del skintone al SOURCE de la capa
    // flagged con la coverage del skintone (mask .g) en este pixel, ANTES del composite normal. = el
    // ComposeOne(src, skinColor, skinCov, skinConv, softlight) del CPU. Inerte byte-identico si uPreToneSkin==0.
    if (uPreToneSkin == 1) {
        float skMaskV = (uSkinMaskCh == 3) ? texture(uSkinMask, vUV).a : texture(uSkinMask, vUV).g;
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
    // over-RUNNING + 4 espacios (shader AGNOSTICO): el acumulador prev vive en uOutputSpace; el BLEND OP
    // corre en uWorkingSpace; el color de capa esta en uSrcSpace; y el COMPOSITE (lerp por cov) corre en
    // uCompositeSpace. Ley gen3: el blend va en su espacio (g22/srgb) pero la lerp por cobertura va en
    // LINEAR-light. uFramework decide como blend(prev/base,src) entra al acumulador (ver FaceTintFramework).
    // base = uBase (original sin tintar, en uOutputSpace). OverPrev (0, default) = el modelo previo
    // BYTE-IDENTICO (cuando uCompositeSpace==uWorkingSpace se reduce a lerp en working). 1:1 con CPU ComposeOne.
    vec3 src_w = cvt(srcColor, uSrcSpace, uWorkingSpace);
    vec3 base  = texture(uBase, vUV).rgb;
    vec3 res_c;
    if (uFramework == 1) {                 // OverBase: mix(base, blend(base,src), cov)
        vec3 anchor_w = cvt(base, uOutputSpace, uWorkingSpace);
        vec3 blended  = blendDispatch(anchor_w, src_w);
        vec3 anchor_c = cvt(base, uOutputSpace, uCompositeSpace);
        vec3 blend_c  = cvt(blended, uWorkingSpace, uCompositeSpace);
        res_c = anchor_c + cov * (blend_c - anchor_c);
    } else if (uFramework == 2) {          // AddBase: prev + cov*(blend(base,src) - base)
        vec3 anchor_w = cvt(base, uOutputSpace, uWorkingSpace);
        vec3 blended  = blendDispatch(anchor_w, src_w);
        vec3 prev_c   = cvt(prev, uOutputSpace, uCompositeSpace);
        vec3 base_c   = cvt(base, uOutputSpace, uCompositeSpace);
        vec3 blend_c  = cvt(blended, uWorkingSpace, uCompositeSpace);
        res_c = prev_c + cov * (blend_c - base_c);
    } else if (uFramework == 3) {          // ModSrc: blend(prev, mix(neutral,src,cov)); replace -> OverPrev
        vec3 base_w = cvt(prev, uOutputSpace, uWorkingSpace);
        if (uBlendOp == 0) {
            vec3 bc = cvt(prev, uOutputSpace, uCompositeSpace);
            vec3 sc = cvt(src_w, uWorkingSpace, uCompositeSpace);
            res_c = bc + cov * (sc - bc);
        } else {
            vec3 neut    = vec3(blendNeutral(uBlendOp));
            vec3 smod_w  = neut + cov * (src_w - neut);
            vec3 blended = blendDispatch(base_w, smod_w);
            res_c = cvt(blended, uWorkingSpace, uCompositeSpace);
        }
    } else {                               // OverPrev (0, default): mix(prev, blend(prev,src), cov)
        vec3 base_w  = cvt(prev, uOutputSpace, uWorkingSpace);
        vec3 blended = blendDispatch(base_w, src_w);
        vec3 base_c  = cvt(prev, uOutputSpace, uCompositeSpace);
        vec3 blend_c = cvt(blended, uWorkingSpace, uCompositeSpace);
        res_c = base_c + cov * (blend_c - base_c);
    }
    res_c = clamp(res_c, 0.0, 1.0);
    vec3 finalRgb = cvt(res_c, uCompositeSpace, uOutputSpace);
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

    ''' <summary>Backward-compat wrapper that composes onto the diffuse channel without skin tinting.</summary>
    Public Function ComposeOntoFaceDiffuse(state As FaceTintCompositorState, originalTexId As Integer, width As Integer, height As Integer, layers As IList(Of FaceTintLayerInput)) As Integer
        Return ComposeOntoFaceTexture(state, originalTexId, width, height, layers, FaceTintChannel.Diffuse)
    End Function

    ''' <summary>Compose all layers that contribute to the requested channel onto a copy of the
    ''' supplied face texture (diffuse / normal / specular) and return the new GL texture ID.
    ''' The original is left untouched. Returns 0 on failure or when no layer contributes data
    ''' for the requested channel.
    ''' MUST run on the GL thread.
    ''' <paramref name="skinTint"/> is the NPC's skin tint colour (0..1 vec3). When supplied on
    ''' the Diffuse channel, the compositor tints the base texture by this value on the first
    ''' iteration and multiplies TakesSkinTone layer colours by it. The caller is responsible
    ''' for setting materialBase.SkinTint = False on the face mesh after composing so the
    ''' render shader's own tint uniform becomes a no-op -- otherwise skin tone is applied twice.
    ''' Pass Nothing (default) to skip skin-tone handling entirely and rely on the legacy render
    ''' uniform. No-op on Normal/Specular channels regardless of the value.
    ''' </summary>
    Public Function ComposeOntoFaceTexture(state As FaceTintCompositorState, originalTexId As Integer, width As Integer, height As Integer, layers As IList(Of FaceTintLayerInput), channel As FaceTintChannel, Optional cache As FaceTintTextureCache = Nothing) As Integer
        ArgumentNullException.ThrowIfNull(state)
        If originalTexId = 0 OrElse width <= 0 OrElse height <= 0 Then Return 0
        If layers Is Nothing OrElse layers.Count = 0 Then Return 0

        EnsureCompositorInitialized(state)
        If state._program = 0 OrElse state._quadVao = 0 Then Return 0

        ' Save GL state we are about to clobber.
        Dim prevFbo As Integer = GL.GetInteger(GetPName.DrawFramebufferBinding)
        Dim prevProg As Integer = GL.GetInteger(GetPName.CurrentProgram)
        Dim prevVao As Integer = GL.GetInteger(GetPName.VertexArrayBinding)
        Dim prevActiveTex As Integer = GL.GetInteger(GetPName.ActiveTexture)
        Dim prevTex0 As Integer = GL.GetInteger(GetPName.TextureBinding2D)
        Dim prevViewport(3) As Integer
        GL.GetInteger(GetPName.Viewport, prevViewport)
        Dim wasBlend As Boolean = GL.IsEnabled(EnableCap.Blend)
        Dim wasDepth As Boolean = GL.IsEnabled(EnableCap.DepthTest)
        Dim wasScissor As Boolean = GL.IsEnabled(EnableCap.ScissorTest)

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
            Dim addRequest = Sub(reqKey As String, b As Byte(), cacheable As Boolean)
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
                    batchLoaded = cache.GetOrLoadBatch(loadKeys, loadBytes, loadCacheable, wrapClampToEdge:=True)
                Else
                    batchLoaded = DirectXDDSLoader.Load_And_GenerateOpenGLTextures_Memory(loadKeys.ToArray(), loadBytes.ToArray(), True, True)
                    ' Library default is Repeat wrap; compositor samples a fullscreen quad over UV [0,1]
                    ' and seams at the edges would bleed, so force ClampToEdge on each loaded texture.
                    If batchLoaded IsNot Nothing Then
                        For Each kvp In batchLoaded
                            Dim e = kvp.Value
                            If e IsNot Nothing AndAlso e.Texture_ID <> 0 Then
                                GL.BindTexture(TextureTarget.Texture2D, e.Texture_ID)
                                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, CInt(TextureWrapMode.ClampToEdge))
                                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, CInt(TextureWrapMode.ClampToEdge))
                            End If
                        Next
                        GL.BindTexture(TextureTarget.Texture2D, 0)
                    End If
                End If
            End If

            ' Reuse persistent ping-pong attachments at this size; allocate the caller-owned
            ' result texture+fbo for the final pass output. Pings stay alive in the state
            ' across calls, eliminating the per-call GenTexture+TexImage2D+DeleteTexture
            ' churn for 1024^2 face textures.
            If Not EnsurePingPongAllocated(state, width, height) Then Return 0
            If Not AllocateResultTextureAndFbo(width, height, resultTex, resultFbo) Then Return 0

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
                Dim k As String = Nothing
                If Not layerChannelKey.TryGetValue(i, k) Then Continue For
                Dim e As PreviewModel.Texture_Loaded_Class = Nothing
                If batchLoaded Is Nothing OrElse Not batchLoaded.TryGetValue(k, e) _
                   OrElse e Is Nothing OrElse e.Texture_ID = 0 Then Continue For
                drawableCount += 1
            Next

            If drawableCount = 0 Then
                ' Nothing to draw; release the result handles and return 0 (matches legacy behaviour).
                Try : GL.DeleteFramebuffer(resultFbo) : Catch : End Try
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
                    Dim sConv = FaceTintConvention.ResolveConvention(sLayer.IsTextureSet, sLayer.Slot, sLayer.BlendOp, channel, False)
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

                Dim layerTex As Integer = chanEntry.Texture_ID

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

                ' Unit 4 (uBase): VESTIGIAL desde el cambio a over-RUNNING. El shader ya NO lee uBase
                ' (la base del blend es el acumulador corriente uPrev). Se sigue bindeando un texture
                ' valido para que el sampler nunca quede indefinido si el compilador no lo elimina.
                GL.ActiveTexture(TextureUnit.Texture4)
                GL.BindTexture(TextureTarget.Texture2D, baseTexForCompose)
                GL.Uniform1(state._uBaseLoc, 4)
                Dim useHairPaletteEffective As Boolean = (hairLutTex <> 0 AndAlso layer.UseHairPalette _
                                                           AndAlso channel = FaceTintChannel.Diffuse)
                GL.Uniform1(state._uUseHairPaletteLoc, If(useHairPaletteEffective, 1, 0))
                ' Derived model: resolver de convencion centralizado (FaceTintConvention).
                ' ws/maskconv/blend salen de (entry_type + slot + blendOp + channel + useHairPalette).
                ' SIN occlusion footprint (descartado empiricamente B07-B09).
                Dim conv = FaceTintConvention.ResolveConvention(
                    layer.IsTextureSet, layer.Slot, layer.BlendOp, channel, useHairPaletteEffective)
                GL.Uniform1(state._uModeLoc, 0)   ' tint = additive-over-base
                GL.Uniform1(state._uWorkingSpaceLoc, CInt(conv.WorkingSpace))
                GL.Uniform1(state._uSrcSpaceLoc, CInt(conv.SrcSpace))
                GL.Uniform1(state._uOutputSpaceLoc, CInt(conv.OutputSpace))
                GL.Uniform1(state._uCompositeSpaceLoc, CInt(conv.CompositeSpace))
                GL.Uniform1(state._uMaskConvFullLoc, CInt(conv.MaskConv))
                GL.Uniform1(state._uSoftLightLoc, CInt(conv.SoftLight))   ' modelo de softlight (agnostico) para bop3
                GL.Uniform1(state._uFrameworkLoc, CInt(conv.Framework))   ' framework de composite (OverPrev default)
                ' Alpha del resultado: no hay footprint; el ultimo layer escribe alpha opaca.
                GL.Uniform1(state._uForceOpaqueAlphaLoc, If(isLast, 1, 0))
                GL.Uniform1(state._uPaletteRowLoc, Math.Max(0.0F, Math.Min(1.0F, layer.HairPaletteRow)))
                ' Flat HCLF-RGB tint for TextureSet brow layers. Mutually exclusive with the LUT
                ' path above (palette branch wins when both are set, mirroring the CPU rule).
                Dim forceUniformColorEffective As Boolean = (layer.ForceUniformColor _
                                                             AndAlso layer.Kind = FaceTintLayerKind.TextureSetDiffuse _
                                                             AndAlso channel = FaceTintChannel.Diffuse _
                                                             AndAlso Not useHairPaletteEffective)
                GL.Uniform1(state._uForceUniformColorLoc, If(forceUniformColorEffective, 1, 0))

                GL.Uniform3(state._uColorLoc,
                            CSng(layer.R) / 255.0F,
                            CSng(layer.G) / 255.0F,
                            CSng(layer.B) / 255.0F)
                GL.Uniform1(state._uOpacityLoc, Math.Max(0.0F, Math.Min(1.0F, layer.Opacity)))
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

                ' Next iteration reads from what we just wrote (resultTex on the last pass,
                ' otherwise the ping we just bound).
                readTexId = If(isLast, resultTex, state._pingTex(writeIdx))

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

            ' Free the result FBO (always scratch, the texture is owned by the caller).
            ' Pings are persistent and stay in the state for next call.
            If resultFbo <> 0 Then
                Try : GL.DeleteFramebuffer(resultFbo) : Catch : End Try
            End If

            ' Restore state.
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, prevFbo)
            GL.UseProgram(prevProg)
            GL.BindVertexArray(prevVao)
            GL.ActiveTexture(TextureUnit.Texture0)
            GL.BindTexture(TextureTarget.Texture2D, prevTex0)
            GL.ActiveTexture(CType(prevActiveTex, TextureUnit))
            GL.Viewport(prevViewport(0), prevViewport(1), prevViewport(2), prevViewport(3))
            If wasDepth Then GL.Enable(EnableCap.DepthTest) Else GL.Disable(EnableCap.DepthTest)
            If wasScissor Then GL.Enable(EnableCap.ScissorTest) Else GL.Disable(EnableCap.ScissorTest)
            If wasBlend Then GL.Enable(EnableCap.Blend) Else GL.Disable(EnableCap.Blend)
        End Try

        Return resultTex
    End Function

    ''' <summary>Format a pair of DXGI codes (original + final) as a short readable tag.
    ''' Reports orig->final when they differ, single name when they're identical.
    ''' "+a" suffix marks formats that guarantee an alpha channel — important to know
    ''' which layer masks actually carry alpha vs. constant 1. Diagnostic only.</summary>
    Private Function DescribeFormat(origCode As Integer, finCode As Integer) As String
        Dim orig = DxgiName(origCode)
        Dim fin = DxgiName(finCode)
        If orig = fin Then Return orig
        Return $"{orig}->{fin}"
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

    ' Region-swap fragment shader. Replace gated by the BC1 mask AND the morph intensity:
    '   weight = convMask(mask.r) * uSwapIntensity
    '   result = mix(prev, swap, weight)
    ' The mask is a TintTemplateOption TTET[0] -- the SAME mask type the tint compositor reads --
    ' so it shares THE single coverage convention (convMask, FaceTintBlendConvention): the spatial
    ' mask is shaped (sRGB by default), intensity stays the linear scalar. uSwapIntensity = the
    ' NPC's MSDV morph value for this preset (0..1): the engine blends the region variant
    ' proportionally to the slider, NOT on/off (verified vs CK on 001A679C, ear/cheek morphs ~0.56;
    ' applying at full mask over-applied ~4 levels). mask = WHERE, intensity = HOW MUCH; weight 0
    ' leaves the base untouched. The blend stays REPLACE (mix) -- a region swap substitutes the
    ' whole region's variant texture, it is not an additive feature delta like the tint layers.
    '
    ' Alpha contract: input alpha (from uPrev) is PRESERVED into the output. The swap and
    ' mask textures contribute only RGB / weight respectively; the accumulator alpha rides
    ' along untouched so callers passing alpha-tested diffuses do not lose their cutout.

    ''' <summary>Apply a list of per-region MPPT TXST swaps onto the supplied face texture
    ''' for the requested channel and return the new GL texture ID. The original is left
    ''' untouched. Returns 0 on failure or when no swap actually contributes data for the
    ''' requested channel. MUST run on the GL thread.
    '''
    ''' Each swap mixes its swap texture into the previous accumulator using the region
    ''' mask's red channel as the per-pixel weight (hard replace inside the mask, leave
    ''' base outside). Swaps are applied in list order; if multiple swaps overlap on the
    ''' same region (shouldn't happen in vanilla — one preset per group at a time) the
    ''' last one wins inside the overlap.</summary>
    Public Function ApplyRegionSwapsOntoFaceTexture(state As FaceTintCompositorState,
                                                     originalTexId As Integer,
                                                     width As Integer, height As Integer,
                                                     swaps As IList(Of FaceRegionSwapInput),
                                                     channel As FaceTintChannel,
                                                     Optional cache As FaceTintTextureCache = Nothing) As Integer
        ArgumentNullException.ThrowIfNull(state)
        If originalTexId = 0 OrElse width <= 0 OrElse height <= 0 Then Return 0
        If swaps Is Nothing OrElse swaps.Count = 0 Then Return 0

        EnsureCompositorInitialized(state)
        If state._program = 0 OrElse state._quadVao = 0 Then Return 0

        ' Save GL state we are about to clobber.
        Dim prevFbo As Integer = GL.GetInteger(GetPName.DrawFramebufferBinding)
        Dim prevProg As Integer = GL.GetInteger(GetPName.CurrentProgram)
        Dim prevVao As Integer = GL.GetInteger(GetPName.VertexArrayBinding)
        Dim prevActiveTex As Integer = GL.GetInteger(GetPName.ActiveTexture)
        Dim prevTex0 As Integer = GL.GetInteger(GetPName.TextureBinding2D)
        Dim prevViewport(3) As Integer
        GL.GetInteger(GetPName.Viewport, prevViewport)
        Dim wasBlend As Boolean = GL.IsEnabled(EnableCap.Blend)
        Dim wasDepth As Boolean = GL.IsEnabled(EnableCap.DepthTest)
        Dim wasScissor As Boolean = GL.IsEnabled(EnableCap.ScissorTest)

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
                loadKeys.Add(kS) : loadBytes.Add(sb) : loadCacheable.Add(Not String.IsNullOrEmpty(swCacheKey)) : swapTexKey(i) = kS
                loadKeys.Add(kM) : loadBytes.Add(sw.RegionMaskDdsBytes) : loadCacheable.Add(Not String.IsNullOrEmpty(mkCacheKey)) : swapMaskKey(i) = kM
            Next
            If loadKeys.Count = 0 Then
                Return 0
            End If

            If cache IsNot Nothing Then
                batchLoaded = cache.GetOrLoadBatch(loadKeys, loadBytes, loadCacheable, wrapClampToEdge:=True)
            Else
                batchLoaded = DirectXDDSLoader.Load_And_GenerateOpenGLTextures_Memory(loadKeys.ToArray(), loadBytes.ToArray(), True, True)
                If batchLoaded IsNot Nothing Then
                    For Each kvp In batchLoaded
                        Dim e = kvp.Value
                        If e IsNot Nothing AndAlso e.Texture_ID <> 0 Then
                            GL.BindTexture(TextureTarget.Texture2D, e.Texture_ID)
                            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, CInt(TextureWrapMode.ClampToEdge))
                            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, CInt(TextureWrapMode.ClampToEdge))
                        End If
                    Next
                    GL.BindTexture(TextureTarget.Texture2D, 0)
                End If
            End If

            ' Reuse persistent ping-pong attachments at this size; allocate caller-owned
            ' result for the final pass.
            If Not EnsurePingPongAllocated(state, width, height) Then Return 0
            If Not AllocateResultTextureAndFbo(width, height, resultTex, resultFbo) Then Return 0

            GL.Viewport(0, 0, width, height)
            GL.Disable(EnableCap.DepthTest)
            GL.Disable(EnableCap.ScissorTest)
            GL.Disable(EnableCap.Blend)

            GL.UseProgram(state._program)
            GL.BindVertexArray(state._quadVao)
            ' Shader unico: region swap = uMode=1 (RUNNING CLOSED-FORM en stored space = build_3 + CPU). swap
            ' tex -> uLayer(1), region mask -> uLayerDiffuseAlpha(2), intensity(msdv) -> uOpacity, SEED ->
            ' uBase(4). El acumulador D vive en sRGB (storage build_3); el swap tex (MPPT TXST diffuse) es
            ' sRGB -> src=srgb(1)=output. N/S: datos lineales, src=output=linear(0). El running necesita el
            ' SEED (= originalTexId) aparte del acumulador (uPrev): se bindea uBase=originalTexId POR-DRAW en
            ' el loop (no solo en el setup) para garantizar que la unit 4 este siempre el seed en cada draw.
            GL.Uniform1(state._uModeLoc, 1)
            ' Swap = replace resuelto por la MISMA tabla que los tints (forSwap:=True) -> el override de convención
            ' (incl. #If DEBUG full-linear) alcanza también los swaps. NON-DEBUG byte-idéntico (paridad con CPU).
            Dim swConv = FaceTintConvention.ResolveConvention(False, 0US, 0, channel, False, forBake:=True)
            GL.Uniform1(state._uSrcSpaceLoc, CInt(swConv.SrcSpace))
            GL.Uniform1(state._uOutputSpaceLoc, CInt(swConv.OutputSpace))
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
                Try : GL.DeleteFramebuffer(resultFbo) : Catch : End Try
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
                GL.Uniform1(state._uOpacityLoc, Math.Max(0.0F, Math.Min(1.0F, sw.Intensity)))

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

            ' Result FBO is scratch; pings stay persistent in the state.
            If resultFbo <> 0 Then
                Try : GL.DeleteFramebuffer(resultFbo) : Catch : End Try
            End If

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, prevFbo)
            GL.UseProgram(prevProg)
            GL.BindVertexArray(prevVao)
            GL.ActiveTexture(TextureUnit.Texture0)
            GL.BindTexture(TextureTarget.Texture2D, prevTex0)
            GL.ActiveTexture(CType(prevActiveTex, TextureUnit))
            GL.Viewport(prevViewport(0), prevViewport(1), prevViewport(2), prevViewport(3))
            If wasDepth Then GL.Enable(EnableCap.DepthTest) Else GL.Disable(EnableCap.DepthTest)
            If wasScissor Then GL.Enable(EnableCap.ScissorTest) Else GL.Disable(EnableCap.ScissorTest)
            If wasBlend Then GL.Enable(EnableCap.Blend) Else GL.Disable(EnableCap.Blend)
        End Try

        Return resultTex
    End Function

    ' Uniform-blend fragment shader. Applies a single per-pixel blend op against a uniform
    ' colour over the entire texture (no mask, full coverage). Used by ApplyUniformBlendOntoFaceTexture
    ' for the body SkinTone pre-pass: softlight(body_diffuse, QNAM_color). The body has no
    ' equivalent of the face's TTET mask layers, so the blend covers the whole texture.
    '
    ' Alpha contract: input alpha is PRESERVED into the output. The blend operations
    ' (Multiply/Overlay/SoftLight/HardLight) are colour-space operations defined for RGB only;
    ' touching alpha would silently corrupt callers that pass alpha-tested diffuses (regression
    ' 2026-05-15: pre-Bug-A fix, chunks marcados Kind=Skin → SkinTint=True → este pase corría
    ' sobre pack_d.dds y reescribia su alpha a 1.0, rompiendo el discard del alpha test). Even
    ' though the current callers (face / body skin) do not use alpha-test on the diffuse, the
    ' shader stays honest about its scope: blend RGB, leave alpha alone.

    ''' <summary>Apply a single uniform-colour blend onto an entire face texture and return
    ''' the new GL texture ID. The original is left untouched. Returns 0 on failure.
    ''' MUST run on the GL thread.
    '''
    ''' Used by the body SkinTone pre-pass (softlight(body_diffuse, QNAM)) and the face
    ''' fallback (TryApplyFaceSkinSoftLight). This is a WHOLE-texture uniform blend with no TTET
    ''' mask and no FaceGen gamma-2.2 base, so it does NOT use the main compositor's masked
    ''' additive-over-base math; attenuation is <c>mix(prev, blended, opacity)</c>. The caller
    ''' passes the FULL source colour (not pre-attenuated toward neutral grey) plus an opacity
    ''' scalar (typically tl.Value/100 or qnam.A/255); the shader interpolates between prev
    ''' (no-op) and the full-strength blend by the opacity factor.
    '''
    ''' blendOp follows the BGSCharacterTint enum: 0=Default 1=Multiply 2=Overlay 3=SoftLight 4=HardLight.</summary>
    ''' <param name="workingSpace">Working space (FaceTintWorkingSpace: 0=Linear 1=Srgb 2=G22) en el
    ''' que se hace el blend, igual que la cara. El body SkinTone debe pasar la ws que el resolver
    ''' (FaceTintConvention.ResolveConvention slot=SkinTone) da, para matchear la cara byte-a-byte.</param>
    Public Function ApplyUniformBlendOntoFaceTexture(state As FaceTintCompositorState,
                                                      originalTexId As Integer,
                                                      width As Integer, height As Integer,
                                                      r As Single, g As Single, b As Single,
                                                      blendOp As Integer,
                                                      opacity As Single,
                                                      Optional workingSpace As Integer = 1) As Integer
        ArgumentNullException.ThrowIfNull(state)
        If originalTexId = 0 OrElse width <= 0 OrElse height <= 0 Then Return 0

        EnsureCompositorInitialized(state)
        If state._program = 0 OrElse state._quadVao = 0 Then Return 0

        Dim prevFbo As Integer = GL.GetInteger(GetPName.DrawFramebufferBinding)
        Dim prevProg As Integer = GL.GetInteger(GetPName.CurrentProgram)
        Dim prevVao As Integer = GL.GetInteger(GetPName.VertexArrayBinding)
        Dim prevActiveTex As Integer = GL.GetInteger(GetPName.ActiveTexture)
        Dim prevTex0 As Integer = GL.GetInteger(GetPName.TextureBinding2D)
        Dim prevViewport(3) As Integer
        GL.GetInteger(GetPName.Viewport, prevViewport)
        Dim wasBlend As Boolean = GL.IsEnabled(EnableCap.Blend)
        Dim wasDepth As Boolean = GL.IsEnabled(EnableCap.DepthTest)
        Dim wasScissor As Boolean = GL.IsEnabled(EnableCap.ScissorTest)

        Dim outTex As Integer = 0
        Dim outFbo As Integer = 0
        Dim resultTex As Integer = 0

        Try
            outTex = GL.GenTexture()
            GL.BindTexture(TextureTarget.Texture2D, outTex)
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, CInt(TextureMinFilter.Linear))
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, CInt(TextureMagFilter.Linear))
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, CInt(TextureWrapMode.ClampToEdge))
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, CInt(TextureWrapMode.ClampToEdge))
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8,
                          width, height, 0,
                          PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero)

            outFbo = GL.GenFramebuffer()
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, outFbo)
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
                                    TextureTarget.Texture2D, outTex, 0)
            Dim status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer)
            If status <> FramebufferErrorCode.FramebufferComplete Then Return 0

            GL.Viewport(0, 0, width, height)
            GL.Disable(EnableCap.DepthTest)
            GL.Disable(EnableCap.ScissorTest)
            GL.Disable(EnableCap.Blend)

            ' Shader unico: body skin = uMode=0 + uLayerKind=2 (UniformColor). Single pass whole-texture,
            ' base==prev==la textura del cuerpo (bind a uPrev unit0 Y uBase unit4). src=uColor, mask=1.
            ' workingSpace lo da el resolver SkinTone (sync con la cara). uBlendOp=SoftLight.
            GL.UseProgram(state._program)
            GL.BindVertexArray(state._quadVao)
            GL.Uniform1(state._uModeLoc, 0)
            GL.Uniform1(state._uFrameworkLoc, 0)   ' body = OverPrev (single pass, prev==base)
            GL.Uniform1(state._uLayerKindLoc, 2)
            GL.Uniform1(state._uChannelLoc, 0)
            GL.Uniform1(state._uUseHairPaletteLoc, 0)
            GL.Uniform1(state._uForceUniformColorLoc, 0)
            GL.Uniform1(state._uForceOpaqueAlphaLoc, 1)
            GL.Uniform1(state._uHasDiffuseMaskLoc, 0)
            GL.Uniform1(state._uMaskConvFullLoc, 0)
            GL.Uniform1(state._uWorkingSpaceLoc, workingSpace)
            ' body skin = diffuse color, acumulador sRGB (src srgb, output srgb); el blend (softlight)
            ' corre en workingSpace (g22). El COMPOSITE (lerp por op) corre en LINEAR = igual que la cara
            ' (ley gen3). El body es textura sRGB (NO FaceCustomization g22) -> output queda sRGB.
            GL.Uniform1(state._uSrcSpaceLoc, 1)
            GL.Uniform1(state._uOutputSpaceLoc, 1)
            GL.Uniform1(state._uCompositeSpaceLoc, 0)   ' linear = como la cara

            GL.ActiveTexture(TextureUnit.Texture0)
            GL.BindTexture(TextureTarget.Texture2D, originalTexId)
            GL.Uniform1(state._uPrevLoc, 0)
            ' uLayer (unit1) se samplea aunque kind=2 no lo use; bind algo valido.
            GL.ActiveTexture(TextureUnit.Texture1)
            GL.BindTexture(TextureTarget.Texture2D, originalTexId)
            GL.Uniform1(state._uLayerLoc, 1)
            ' uBase (unit4) = la misma textura del cuerpo (prev==base en single pass).
            GL.ActiveTexture(TextureUnit.Texture4)
            GL.BindTexture(TextureTarget.Texture2D, originalTexId)
            GL.Uniform1(state._uBaseLoc, 4)

            GL.Uniform3(state._uColorLoc, r, g, b)
            GL.Uniform1(state._uBlendOpLoc, blendOp)
            GL.Uniform1(state._uOpacityLoc, Math.Max(0.0F, Math.Min(1.0F, opacity)))
            ' body skin: sin pre-tono TakesSkinTone (no aplica). Bind unit5 valido por completitud del sampler.
            GL.Uniform1(state._uPreToneSkinLoc, 0)
            GL.ActiveTexture(TextureUnit.Texture5)
            GL.BindTexture(TextureTarget.Texture2D, originalTexId)
            GL.Uniform1(state._uSkinMaskLoc, 5)

            GL.DrawArrays(PrimitiveType.Triangles, 0, 6)
            resultTex = outTex

        Catch ex As Exception
            resultTex = 0
        Finally
            If outFbo <> 0 Then
                Try : GL.DeleteFramebuffer(outFbo) : Catch : End Try
            End If
            If outTex <> 0 AndAlso outTex <> resultTex Then
                Try : GL.DeleteTexture(outTex) : Catch : End Try
            End If

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, prevFbo)
            GL.UseProgram(prevProg)
            GL.BindVertexArray(prevVao)
            GL.ActiveTexture(TextureUnit.Texture0)
            GL.BindTexture(TextureTarget.Texture2D, prevTex0)
            GL.ActiveTexture(CType(prevActiveTex, TextureUnit))
            GL.Viewport(prevViewport(0), prevViewport(1), prevViewport(2), prevViewport(3))
            If wasDepth Then GL.Enable(EnableCap.DepthTest) Else GL.Disable(EnableCap.DepthTest)
            If wasScissor Then GL.Enable(EnableCap.ScissorTest) Else GL.Disable(EnableCap.ScissorTest)
            If wasBlend Then GL.Enable(EnableCap.Blend) Else GL.Disable(EnableCap.Blend)
        End Try

        Return resultTex
    End Function


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

    ''' <summary>Allocate one fresh RGBA8 texture + framebuffer at (width, height) for the
    ''' caller-owned final output of a pass. The caller is responsible for deleting
    ''' <paramref name="resultTex"/> (per existing contract); the FBO is internal scratch and
    ''' must be deleted by the caller's Finally block. Returns False on FBO incompleteness
    ''' (handles freed before return). MUST run on the GL thread.</summary>
    Private Function AllocateResultTextureAndFbo(width As Integer, height As Integer,
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

        resultFbo = GL.GenFramebuffer()
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, resultFbo)
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
                                TextureTarget.Texture2D, resultTex, 0)
        Dim status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer)
        If status <> FramebufferErrorCode.FramebufferComplete Then
            Try : GL.DeleteFramebuffer(resultFbo) : Catch : End Try
            Try : GL.DeleteTexture(resultTex) : Catch : End Try
            resultFbo = 0
            resultTex = 0
            Return False
        End If
        Return True
    End Function

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

        state._uPrevLoc = GL.GetUniformLocation(state._program, "uPrev")
        state._uLayerLoc = GL.GetUniformLocation(state._program, "uLayer")
        state._uBaseLoc = GL.GetUniformLocation(state._program, "uBase")
        state._uLayerDiffuseAlphaLoc = GL.GetUniformLocation(state._program, "uLayerDiffuseAlpha")
        state._uHasDiffuseMaskLoc = GL.GetUniformLocation(state._program, "uHasDiffuseMask")
        state._uColorLoc = GL.GetUniformLocation(state._program, "uColor")
        state._uOpacityLoc = GL.GetUniformLocation(state._program, "uOpacity")
        state._uBlendOpLoc = GL.GetUniformLocation(state._program, "uBlendOp")
        state._uLayerKindLoc = GL.GetUniformLocation(state._program, "uLayerKind")
        state._uChannelLoc = GL.GetUniformLocation(state._program, "uChannel")
        state._uHairLutLoc = GL.GetUniformLocation(state._program, "uHairLut")
        state._uPaletteRowLoc = GL.GetUniformLocation(state._program, "uPaletteRow")
        state._uUseHairPaletteLoc = GL.GetUniformLocation(state._program, "uUseHairPalette")
        state._uForceOpaqueAlphaLoc = GL.GetUniformLocation(state._program, "uForceOpaqueAlpha")
        state._uForceUniformColorLoc = GL.GetUniformLocation(state._program, "uForceUniformColor")
        state._uWorkingSpaceLoc = GL.GetUniformLocation(state._program, "uWorkingSpace")
        state._uSrcSpaceLoc = GL.GetUniformLocation(state._program, "uSrcSpace")
        state._uOutputSpaceLoc = GL.GetUniformLocation(state._program, "uOutputSpace")
        state._uCompositeSpaceLoc = GL.GetUniformLocation(state._program, "uCompositeSpace")
        state._uMaskConvFullLoc = GL.GetUniformLocation(state._program, "uMaskConvFull")
        state._uModeLoc = GL.GetUniformLocation(state._program, "uMode")
        state._uSoftLightLoc = GL.GetUniformLocation(state._program, "uSoftLight")
        state._uFrameworkLoc = GL.GetUniformLocation(state._program, "uFramework")
        state._uPreToneSkinLoc = GL.GetUniformLocation(state._program, "uPreToneSkin")
        state._uSkinMaskLoc = GL.GetUniformLocation(state._program, "uSkinMask")
        state._uSkinColorLoc = GL.GetUniformLocation(state._program, "uSkinColor")
        state._uSkinOpacityLoc = GL.GetUniformLocation(state._program, "uSkinOpacity")
        state._uSkinWsLoc = GL.GetUniformLocation(state._program, "uSkinWs")
        state._uSkinCsLoc = GL.GetUniformLocation(state._program, "uSkinCs")
        state._uSkinSsLoc = GL.GetUniformLocation(state._program, "uSkinSs")
        state._uSkinOsLoc = GL.GetUniformLocation(state._program, "uSkinOs")
        state._uSkinBopLoc = GL.GetUniformLocation(state._program, "uSkinBop")
        state._uSkinSlLoc = GL.GetUniformLocation(state._program, "uSkinSl")
        state._uSkinMcLoc = GL.GetUniformLocation(state._program, "uSkinMc")
        state._uSkinMaskChLoc = GL.GetUniformLocation(state._program, "uSkinMaskCh")

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
        EnsureCompositorInitialized(state)
        If state._program = 0 OrElse state._quadVao = 0 Then Return 0

        Dim prevFbo As Integer = GL.GetInteger(GetPName.DrawFramebufferBinding)
        Dim prevProg As Integer = GL.GetInteger(GetPName.CurrentProgram)
        Dim prevVao As Integer = GL.GetInteger(GetPName.VertexArrayBinding)
        Dim prevActiveTex As Integer = GL.GetInteger(GetPName.ActiveTexture)
        Dim prevTex0 As Integer = GL.GetInteger(GetPName.TextureBinding2D)
        Dim prevViewport(3) As Integer
        GL.GetInteger(GetPName.Viewport, prevViewport)

        Dim outTex As Integer = 0
        Dim outFbo As Integer = 0
        Try
            If Not AllocateResultTextureAndFbo(width, height, outTex, outFbo) Then Return 0
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, outFbo)
            GL.Viewport(0, 0, width, height)
            GL.Disable(EnableCap.DepthTest)
            GL.Disable(EnableCap.ScissorTest)
            GL.Disable(EnableCap.Blend)
            GL.UseProgram(state._program)
            GL.BindVertexArray(state._quadVao)
            GL.Uniform1(state._uModeLoc, 2)
            GL.Uniform1(state._uSrcSpaceLoc, fromSpace)
            GL.Uniform1(state._uOutputSpaceLoc, toSpace)
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
            If outFbo <> 0 Then Try : GL.DeleteFramebuffer(outFbo) : Catch : End Try
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, prevFbo)
            GL.UseProgram(prevProg)
            GL.BindVertexArray(prevVao)
            GL.ActiveTexture(CType(prevActiveTex, TextureUnit))
            GL.BindTexture(TextureTarget.Texture2D, prevTex0)
            GL.Viewport(prevViewport(0), prevViewport(1), prevViewport(2), prevViewport(3))
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
    End Class

    ''' <summary>Apply the face-tint pipeline (region-swap → tint compose) to a triplet of
    ''' source GL textures and return the per-channel result.
    '''
    ''' Single source of truth for both the live render path and the offline bake. Neither
    ''' caller replicates the orchestration; the difference between them is purely how they
    ''' consume the output:
    '''   • Render: swap result IDs into <c>Textures_Dictionary</c>, GL.DeleteTexture the IDs
    '''     the dictionary previously held.
    '''   • Bake: GL.GetTexImage from result IDs, encode to DDS on disk, GL.DeleteTexture the
    '''     fresh result IDs (and any temporaries the bake itself uploaded as inputs).
    '''
    ''' QNAM softlight is intentionally NOT included: in the render path it runs after this
    ''' function as a separate pass (TryApplyFaceSkinSoftLight) gated on NpcHasSkinToneLayer;
    ''' the bake replicates that final pass on its own. Folding it in here would force the
    ''' render to thread the QNAM color and the skip flag into TryApplyFaceTints, breaking
    ''' the existing TryApplyFaceSkinSoftLight contract that lives outside.
    '''
    ''' This function does NOT touch any dictionary, model, or NIF — it is pure GL on the
    ''' supplied state + cache. <paramref name="state"/> + <paramref name="cache"/> must be
    ''' valid for the current GL context.
    '''
    ''' Returns IsFresh=True for channels where swap/compose produced a new texture (caller
    ''' owns it); IsFresh=False when no contribution touched that channel (the input ID is
    ''' returned verbatim — caller MUST NOT delete it on the fresh-cleanup path).
    '''
    ''' MUST run on the GL thread with the owning context current.</summary>
    Public Function ApplyFaceTintPipeline(state As FaceTintCompositorState,
                                          cache As FaceTintTextureCache,
                                          srcDiffuseId As Integer,
                                          srcNormalId As Integer,
                                          srcSpecId As Integer,
                                          width As Integer,
                                          height As Integer,
                                          layers As IList(Of FaceTintLayerInput),
                                          swaps As IList(Of FaceRegionSwapInput),
                                          Optional resolution As FaceTintConvention.FaceTintResolutionSettings = Nothing) As FaceTintPipelineResult
        ArgumentNullException.ThrowIfNull(state)

        ' Target de resolución POR CANAL (Inherit -> nativo = width/height del source). El acumulador GL
        ' trabaja a ESTE tamaño; los samplers GL resizean source/capas/swaps por UV (bilineal) igual que
        ' el CPU (FaceTintCpuCompositor.SampleChannelAt) -> GL==CPU para cualquier resolución. Bodyparts:
        ' el caller pasa resolution=Nothing (fuerzan heredar; el enum es solo para la cara).
        Dim dT = ChannelTargetSize(resolution, FaceTintChannel.Diffuse, width, height)
        Dim nT = ChannelTargetSize(resolution, FaceTintChannel.Normal, width, height)
        Dim sT = ChannelTargetSize(resolution, FaceTintChannel.Specular, width, height)

        Dim result As New FaceTintPipelineResult With {
            .Diffuse = New FaceTintPipelineChannelResult With {.TextureId = srcDiffuseId, .IsFresh = False, .Width = dT.W, .Height = dT.H},
            .Normal = New FaceTintPipelineChannelResult With {.TextureId = srcNormalId, .IsFresh = False, .Width = nT.W, .Height = nT.H},
            .Specular = New FaceTintPipelineChannelResult With {.TextureId = srcSpecId, .IsFresh = False, .Width = sT.W, .Height = sT.H}
        }

        If width <= 0 OrElse height <= 0 Then Return result

        ' --- SEED/RESIZE del PATH UNICO al target por canal ---
        ' Seed = BASEIN DIRECTO (sRGB) en los 3 canales (cambio 2026-06-01: g22(BASEIN) era conclusion errada;
        ' CK NO aplica gamma). D acumula en sRGB (= CPU seedG22=False); N/S lineal raw. Ningun canal convierte
        ' espacio en el seed: solo se resizea si el target != nativo (igual que el CPU, paridad GL/CPU).

        If SeedConventionIs_G22 Then
            ConvertChannelIfNeeded(result.Diffuse, state, dT.W, dT.H, width, height, 1, 2)
        Else
            ConvertChannelIfNeeded(result.Diffuse, state, dT.W, dT.H, width, height)
        End If
        ConvertChannelIfNeeded(result.Normal, state, nT.W, nT.H, width, height)
        ConvertChannelIfNeeded(result.Specular, state, sT.W, sT.H, width, height)

        ' --- Region-swap pre-pass (no-op if swaps empty / no contribution to a channel) ---
        If swaps IsNot Nothing AndAlso swaps.Count > 0 Then
            ProcessChannel(result.Diffuse, FaceTintChannel.Diffuse, state, cache, dT.W, dT.H, Nothing, swaps)
            ProcessChannel(result.Normal, FaceTintChannel.Normal, state, cache, nT.W, nT.H, Nothing, swaps)
            ProcessChannel(result.Specular, FaceTintChannel.Specular, state, cache, sT.W, sT.H, Nothing, swaps)
        End If

        ' --- Tint compose ---
        If layers IsNot Nothing AndAlso layers.Count > 0 Then
            ProcessChannel(result.Diffuse, FaceTintChannel.Diffuse, state, cache, dT.W, dT.H, layers, Nothing)
            ProcessChannel(result.Normal, FaceTintChannel.Normal, state, cache, nT.W, nT.H, layers, Nothing)
            ProcessChannel(result.Specular, FaceTintChannel.Specular, state, cache, sT.W, sT.H, layers, Nothing)
        End If

        Return result
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
                                   Optional fromSpace As Integer = 0, Optional toSpace As Integer = 0)
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
                               swaps As IList(Of FaceRegionSwapInput))
        If ch.TextureId = 0 Then
            Return
        End If
        Dim newId As Integer
        If swaps IsNot Nothing Then
            newId = ApplyRegionSwapsOntoFaceTexture(state, ch.TextureId, width, height, swaps, channel, cache)
        Else
            newId = ComposeOntoFaceTexture(state, ch.TextureId, width, height, layers, channel, cache)
        End If
        If newId = 0 OrElse newId = ch.TextureId Then Return
        Dim oldId = ch.TextureId
        Dim oldFresh = ch.IsFresh
        ch.TextureId = newId
        ch.IsFresh = True
        If oldFresh Then
            Try : GL.DeleteTexture(oldId) : Catch : End Try
        End If
    End Sub

End Module

''' <summary>Process-lifetime cache of decoded DDS → GL textures, keyed by an opaque string
''' the caller supplies (typically the normalized texture path). Allows the compositor to
''' reuse GPU texture objects across calls instead of decoding + uploading + deleting on every
''' invocation.
'''
''' Lifecycle:
'''  - The caller owns one cache instance for the lifetime of the GL context.
'''  - The compositor reads from / writes into it through <see cref="GetOrLoadBatch"/> on every
'''    call when a cache is supplied.
'''  - Cache entries are NOT deleted by the compositor's per-call Finally block — they survive
'''    for reuse. Pingpong / FBO textures and ad-hoc allocations (no cache key) follow the
'''    original allocate-and-delete path unchanged.
'''  - The caller MUST call <see cref="Clear"/> when the underlying byte sources change
'''    (FilesDictionary rebuild, BA2 mount/unmount, plugin reload) and BEFORE GL context
'''    teardown. Failing to clear before context teardown leaks GL texture handles owned by
'''    the cache.
'''
''' Thread safety: callers must invoke from the GL thread (same as the compositor itself).
''' No internal locking.</summary>
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

    ''' <summary>Resolve a batch of (key → bytes) requests, splitting into hits and misses.
    ''' Misses go through <c>DirectXDDSLoader.Load_And_GenerateOpenGLTextures_Memory</c> in a
    ''' single native call (matching the original compositor batch behaviour) and the resulting
    ''' entries are stored. Hits are returned from cache untouched.
    '''
    ''' Returns a brand-new dictionary keyed by the caller's request keys, containing every
    ''' resolved entry. Entries marked with <paramref name="isCacheable"/>=False are *not* added
    ''' to the persistent cache and the caller is expected to delete them after use, matching
    ''' the legacy per-call lifecycle. Entries marked True remain in the cache and outlive the
    ''' call.
    '''
    ''' The compositor uses isCacheable=True for entries whose request key was supplied by the
    ''' caller (texture path) and False for synthetic per-call keys. This lets the same batch
    ''' loader call serve both lifetimes uniformly.</summary>
    Public Function GetOrLoadBatch(keys As IList(Of String), bytes As IList(Of Byte()), isCacheable As IList(Of Boolean), wrapClampToEdge As Boolean) As Dictionary(Of String, PreviewModel.Texture_Loaded_Class)
        Dim result As New Dictionary(Of String, PreviewModel.Texture_Loaded_Class)(StringComparer.OrdinalIgnoreCase)
        If keys Is Nothing OrElse keys.Count = 0 Then Return result

        Dim missKeys As New List(Of String)
        Dim missBytes As New List(Of Byte())
        Dim missCacheable As New List(Of Boolean)

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

            missKeys.Add(k)
            missBytes.Add(b)
            missCacheable.Add(cacheable)
        Next

        If missKeys.Count > 0 Then
            Dim loaded = DirectXDDSLoader.Load_And_GenerateOpenGLTextures_Memory(missKeys.ToArray(), missBytes.ToArray(), True, True)
            If loaded IsNot Nothing Then
                If wrapClampToEdge Then
                    For Each kvp In loaded
                        Dim e = kvp.Value
                        If e IsNot Nothing AndAlso e.Texture_ID <> 0 Then
                            OpenTK.Graphics.OpenGL4.GL.BindTexture(OpenTK.Graphics.OpenGL4.TextureTarget.Texture2D, e.Texture_ID)
                            OpenTK.Graphics.OpenGL4.GL.TexParameter(OpenTK.Graphics.OpenGL4.TextureTarget.Texture2D, OpenTK.Graphics.OpenGL4.TextureParameterName.TextureWrapS, CInt(OpenTK.Graphics.OpenGL4.TextureWrapMode.ClampToEdge))
                            OpenTK.Graphics.OpenGL4.GL.TexParameter(OpenTK.Graphics.OpenGL4.TextureTarget.Texture2D, OpenTK.Graphics.OpenGL4.TextureParameterName.TextureWrapT, CInt(OpenTK.Graphics.OpenGL4.TextureWrapMode.ClampToEdge))
                        End If
                    Next
                    OpenTK.Graphics.OpenGL4.GL.BindTexture(OpenTK.Graphics.OpenGL4.TextureTarget.Texture2D, 0)
                End If

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
End Class

