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
    ''' <summary>MPPT TXST.TX00 — replacement diffuse for the region. May be Nothing if the
    ''' TXST has no diffuse slot filled (then the diffuse channel is left untouched).</summary>
    Public Property SwapDiffuseDdsBytes As Byte()
    ''' <summary>MPPT TXST.TX01 — replacement normal for the region. Optional.</summary>
    Public Property SwapNormalDdsBytes As Byte()
    ''' <summary>MPPT TXST.TX07 — replacement smooth-spec for the region. Optional.</summary>
    Public Property SwapSpecularDdsBytes As Byte()
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
End Class

Public Class FaceTintLayerInput
    Public Property Kind As FaceTintLayerKind = FaceTintLayerKind.PaletteMask
    ''' <summary>For PaletteMask: greyscale mask in .r (the diffuse mask). For TextureSetDiffuse: pre-coloured RGBA detail.</summary>
    Public Property LayerDdsBytes As Byte()
    ''' <summary>TextureSet only — pre-coloured RGBA normal map (TTET[1]). Optional, may be empty.</summary>
    Public Property NormalDdsBytes As Byte()
    ''' <summary>TextureSet only — pre-coloured RGBA specular map (TTET[2]). Optional, may be empty.</summary>
    Public Property SpecularDdsBytes As Byte()
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
    ''' <summary>Optional debug label written to the log when this layer is applied.</summary>
    Public Property DebugName As String = ""

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
End Class

Public Module FaceTintCompositor

    ' Cached compositor program + fullscreen quad VAO. Created lazily on first use.
    Private _program As Integer = 0
    Private _uPrevLoc As Integer = -1
    Private _uLayerLoc As Integer = -1
    Private _uLayerDiffuseAlphaLoc As Integer = -1
    Private _uHasDiffuseMaskLoc As Integer = -1
    Private _uColorLoc As Integer = -1
    Private _uOpacityLoc As Integer = -1
    Private _uBlendOpLoc As Integer = -1
    Private _uLayerKindLoc As Integer = -1
    Private _uChannelLoc As Integer = -1
    Private _quadVao As Integer = 0
    Private _quadVbo As Integer = 0

    ' Cached region-swap program (separate from the tint compositor). Created lazily on
    ' first use. Used by ApplyRegionSwapsOntoFaceTexture to apply Morph Group MPPT TXST
    ' swaps gated by the MPPK region mask, before any tint layers run.
    Private _swapProgram As Integer = 0
    Private _uSwapPrevLoc As Integer = -1
    Private _uSwapTexLoc As Integer = -1
    Private _uSwapMaskLoc As Integer = -1

    Private _uniformBlendProgram As Integer = 0
    Private _uUbPrevLoc As Integer = -1
    Private _uUbColorLoc As Integer = -1
    Private _uUbBlendOpLoc As Integer = -1

    Private Const VertexShaderSource As String = "#version 430
layout(location = 0) in vec2 aPos;
out vec2 vUV;
void main() {
    vUV = vec2((aPos.x + 1.0) * 0.5, (aPos.y + 1.0) * 0.5);
    gl_Position = vec4(aPos, 0.0, 1.0);
}"

    ' Photoshop / W3C SVG compositing formulas. dst = current accumulated face diffuse,
    ' src = the layer's effective colour for that pixel.
    Private Const FragmentShaderSource As String = "#version 430
in vec2 vUV;
out vec4 fragColor;

uniform sampler2D uPrev;
uniform sampler2D uLayer;
uniform sampler2D uLayerDiffuseAlpha;  // TTET[0] diffuse of the layer, used as spatial mask on N/S passes
uniform int uHasDiffuseMask;           // 1 when uLayerDiffuseAlpha is meaningful
uniform vec3 uColor;
uniform float uOpacity;
uniform int uBlendOp;
uniform int uLayerKind;
uniform int uChannel;     // 0=Diffuse 1=Normal 2=Specular

vec3 blendDefault(vec3 d, vec3 s) { return s; }
vec3 blendMultiply(vec3 d, vec3 s) { return d * s; }
vec3 blendOverlay(vec3 d, vec3 s) {
    return mix(2.0 * d * s,
               1.0 - 2.0 * (1.0 - d) * (1.0 - s),
               step(0.5, d));
}
vec3 blendSoftLight(vec3 d, vec3 s) {
    vec3 g = mix(((16.0 * d - 12.0) * d + 4.0) * d,
                 sqrt(d),
                 step(0.25, d));
    return mix(d - (1.0 - 2.0 * s) * d * (1.0 - d),
               d + (2.0 * s - 1.0) * (g - d),
               step(0.5, s));
}
vec3 blendHardLight(vec3 d, vec3 s) { return blendOverlay(s, d); }

void main() {
    vec3 prev = texture(uPrev, vUV).rgb;
    vec4 layerSample = texture(uLayer, vUV);

    vec3 srcColor;
    float coverage;
    vec3 blended;

    // ===== Unified Normal/Specular path for TextureSet layers =====
    // Detail normal/specular textures in FO4 are ABSOLUTE full-face maps, not
    // deltas-from-neutral. Verified empirically from Scar6_n.png / Scar6_s.png:
    // each contains the base face curvature / spec level with the scar/wrinkle
    // features pre-integrated on top. Multiple scar layers can share the SAME
    // Scar_n / Scar_s and pick out their region via their own TTET[0] alpha.
    //
    // Additive math (base + detail - neutral) is therefore WRONG for this format:
    // it double-counts the base face curvature producing over-bumped normals and
    // ~2x specular brightness at the feature location. The correct operation is
    // HARD REPLACE gated by the mask: inside the mask the authored detail fully
    // takes over (it already contains the intended final values), outside the
    // mask the accumulator is preserved.
    //
    // Coverage: TTET[0] diffuse alpha as spatial mask, multiplied by uOpacity so
    // the slider fades the N/S contribution proportionally -- uniform across all
    // TextureSet layers (skin-tone and non-skin-tone alike) so colour and relief
    // decay together when the slider drops. Fallback to max(rgb) only when no
    // diffuse mask could be bound on unit 2.
    if (uLayerKind == 1 && uChannel != 0) {
        float maskA = (uHasDiffuseMask == 1) ? texture(uLayerDiffuseAlpha, vUV).a :
                      max(max(layerSample.r, layerSample.g), layerSample.b);
        float cov = clamp(maskA * uOpacity, 0.0, 1.0);
        vec3 finalRgb = mix(prev, layerSample.rgb, cov);
        fragColor = vec4(finalRgb, 1.0);
        return;
    }

    // ===== Diffuse channel (TextureSet or Palette) =====
    // Photoshop blend ops are correct here -- the authored blendOp (SoftLight,
    // Multiply, etc.) reflects how the author wants the detail colour to composite
    // onto the skin, and that math applies cleanly to RGB diffuse data.
    //
    // Skin tone handling: the slot 12 SkinTone layer flows through the Palette
    // branch below (srcColor from uColor=TEND, coverage from layerSample.r, blended
    // via its authored TemplateColors blendOp). Because layers compose in order, any
    // non-skin-tone detail (scar, wrinkle, freckle) that comes AFTER slot 12 blends
    // naturally against the already-tinted accumulator via its own blendOp. No extra
    // per-layer tint multiply is needed, and the caller disables materialBase.SkinTint
    // on the face mesh post-compose so the render shader's global tint uniform becomes
    // a no-op (otherwise we'd double-tint everything).
    if (uLayerKind == 1) {
        // TextureSet Diffuse: detail RGBA, coverage from diffuse alpha.
        srcColor = layerSample.rgb;
        coverage = layerSample.a;
    } else {
        // Palette: greyscale mask in .r, tint colour from uColor uniform (TEND bytes).
        srcColor = uColor;
        coverage = layerSample.r;
    }
    coverage *= uOpacity;
    coverage = clamp(coverage, 0.0, 1.0);

    if (uBlendOp == 1)      blended = blendMultiply(prev, srcColor);
    else if (uBlendOp == 2) blended = blendOverlay(prev, srcColor);
    else if (uBlendOp == 3) blended = blendSoftLight(prev, srcColor);
    else if (uBlendOp == 4) blended = blendHardLight(prev, srcColor);
    else                    blended = blendDefault(prev, srcColor);

    vec3 finalRgb = mix(prev, blended, coverage);
    fragColor = vec4(finalRgb, 1.0);
}"

    ''' <summary>Backward-compat wrapper that composes onto the diffuse channel without skin tinting.</summary>
    Public Function ComposeOntoFaceDiffuse(originalTexId As Integer, width As Integer, height As Integer, layers As IList(Of FaceTintLayerInput)) As Integer
        Return ComposeOntoFaceTexture(originalTexId, width, height, layers, FaceTintChannel.Diffuse, logger:=Nothing)
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
    ''' <paramref name="logger"/> is optional — when supplied, the compositor reports per-layer
    ''' disposition (DRAWN / no-channel-bytes / dds-load-failed) and an end-of-call summary.
    ''' Diagnostic only; null in production builds.</summary>
    Public Function ComposeOntoFaceTexture(originalTexId As Integer, width As Integer, height As Integer, layers As IList(Of FaceTintLayerInput), channel As FaceTintChannel, Optional logger As Action(Of String) = Nothing) As Integer
        If originalTexId = 0 OrElse width <= 0 OrElse height <= 0 Then Return 0
        If layers Is Nothing OrElse layers.Count = 0 Then Return 0

        EnsureCompositorInitialized()
        If _program = 0 OrElse _quadVao = 0 Then Return 0

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

        Dim pingTex(1) As Integer
        Dim pingFbo(1) As Integer
        pingTex(0) = 0 : pingTex(1) = 0
        pingFbo(0) = 0 : pingFbo(1) = 0
        Dim resultTex As Integer = 0
        Dim batchLoaded As Dictionary(Of String, PreviewModel.Texture_Loaded_Class) = Nothing

        Try
            ' === Batch preload every DDS byte buffer this pass needs, in ONE wrapper call. ===
            ' Per layer: its own channel bytes + its TTET[0] diffuse bytes when we need a spatial
            ' mask (N/S passes on TextureSet layers). The library helper decompresses the full
            ' batch in a single native call and uploads each to GL via PBO, returning a dict
            ' of Texture_Loaded_Class { Texture_ID, DGXFormat_Original, DGXFormat_Final, ... }.
            ' Synthetic keys identify each entry; we map layerIndex -> key for lookup below.
            Dim loadKeys As New List(Of String)
            Dim loadBytes As New List(Of Byte())
            Dim layerChannelKey As New Dictionary(Of Integer, String)
            Dim layerMaskKey As New Dictionary(Of Integer, String)
            For i As Integer = 0 To layers.Count - 1
                Dim layer = layers(i)
                If layer Is Nothing Then Continue For
                Dim channelBytes = layer.GetChannelBytes(channel)
                If channelBytes Is Nothing OrElse channelBytes.Length = 0 Then Continue For
                Dim kC = $"l{i}c"
                loadKeys.Add(kC)
                loadBytes.Add(channelBytes)
                layerChannelKey(i) = kC
                If layer.Kind = FaceTintLayerKind.TextureSetDiffuse AndAlso channel <> FaceTintChannel.Diffuse _
                   AndAlso layer.LayerDdsBytes IsNot Nothing AndAlso layer.LayerDdsBytes.Length > 0 Then
                    Dim kM = $"l{i}m"
                    loadKeys.Add(kM)
                    loadBytes.Add(layer.LayerDdsBytes)
                    layerMaskKey(i) = kM
                End If
            Next
            If loadKeys.Count > 0 Then
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

            ' Allocate two ping-pong colour attachments matching the face diffuse size.
            For i As Integer = 0 To 1
                pingTex(i) = GL.GenTexture()
                GL.BindTexture(TextureTarget.Texture2D, pingTex(i))
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, CInt(TextureMinFilter.Linear))
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, CInt(TextureMagFilter.Linear))
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, CInt(TextureWrapMode.ClampToEdge))
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, CInt(TextureWrapMode.ClampToEdge))
                GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8,
                              width, height, 0,
                              PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero)

                pingFbo(i) = GL.GenFramebuffer()
                GL.BindFramebuffer(FramebufferTarget.Framebuffer, pingFbo(i))
                GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
                                        TextureTarget.Texture2D, pingTex(i), 0)
                Dim status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer)
                If status <> FramebufferErrorCode.FramebufferComplete Then
                    Return 0
                End If
            Next

            GL.Viewport(0, 0, width, height)
            GL.Disable(EnableCap.DepthTest)
            GL.Disable(EnableCap.ScissorTest)
            GL.Disable(EnableCap.Blend)   ' the shader does its own blending against uPrev

            GL.UseProgram(_program)
            GL.BindVertexArray(_quadVao)

            Dim writeIdx As Integer = 0
            Dim readTexId As Integer = originalTexId  ' first iteration reads from the unmodified face diffuse

            Dim drawnLayers As Integer = 0
            Dim totalLayers As Integer = If(layers IsNot Nothing, layers.Count, 0)
            For i As Integer = 0 To layers.Count - 1
                Dim layer = layers(i)
                If layer Is Nothing Then Continue For
                Dim layerName As String = If(String.IsNullOrEmpty(layer.DebugName), "<unnamed>", layer.DebugName)

                ' TEST: TakesSkinTone layers do not contribute to the Diffuse channel.
                If channel = FaceTintChannel.Diffuse AndAlso layer.TakesSkinTone Then
                    If logger IsNot Nothing Then logger($"  layer '{layerName}' SKIP takesSkinTone-on-diffuse (test: relief only)")
                    Continue For
                End If

                Dim chanKey As String = Nothing
                If Not layerChannelKey.TryGetValue(i, chanKey) Then
                    If logger IsNot Nothing Then logger($"  layer '{layerName}' SKIP no-channel-bytes")
                    Continue For
                End If

                Dim chanEntry As PreviewModel.Texture_Loaded_Class = Nothing
                If batchLoaded Is Nothing _
                   OrElse Not batchLoaded.TryGetValue(chanKey, chanEntry) _
                   OrElse chanEntry Is Nothing OrElse chanEntry.Texture_ID = 0 Then
                    If logger IsNot Nothing Then logger($"  layer '{layerName}' SKIP dds-load-failed")
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

                GL.BindFramebuffer(FramebufferTarget.Framebuffer, pingFbo(writeIdx))

                GL.ActiveTexture(TextureUnit.Texture0)
                GL.BindTexture(TextureTarget.Texture2D, readTexId)
                GL.Uniform1(_uPrevLoc, 0)

                GL.ActiveTexture(TextureUnit.Texture1)
                GL.BindTexture(TextureTarget.Texture2D, layerTex)
                GL.Uniform1(_uLayerLoc, 1)

                ' Unit 2 always has a valid binding (fallback to layerTex) so the sampler
                ' is never undefined; uHasDiffuseMask tells the shader whether to read it.
                GL.ActiveTexture(TextureUnit.Texture2)
                GL.BindTexture(TextureTarget.Texture2D, If(diffuseMaskTex <> 0, diffuseMaskTex, layerTex))
                GL.Uniform1(_uLayerDiffuseAlphaLoc, 2)
                GL.Uniform1(_uHasDiffuseMaskLoc, If(diffuseMaskTex <> 0, 1, 0))

                GL.Uniform3(_uColorLoc,
                            CSng(layer.R) / 255.0F,
                            CSng(layer.G) / 255.0F,
                            CSng(layer.B) / 255.0F)
                GL.Uniform1(_uOpacityLoc, Math.Max(0.0F, Math.Min(1.0F, layer.Opacity)))
                GL.Uniform1(_uBlendOpLoc, layer.BlendOp)
                GL.Uniform1(_uLayerKindLoc, CInt(layer.Kind))
                GL.Uniform1(_uChannelLoc, CInt(channel))

                GL.DrawArrays(PrimitiveType.Triangles, 0, 6)
                drawnLayers += 1
                If logger IsNot Nothing Then
                    Dim fmtStr As String = DescribeFormat(chanEntry.DGXFormat_Original, chanEntry.DGXFormat_Final)
                    Dim maskNote As String = ""
                    If maskEntry IsNot Nothing AndAlso diffuseMaskTex <> 0 Then
                        maskNote = $" mask={DescribeFormat(maskEntry.DGXFormat_Original, maskEntry.DGXFormat_Final)}"
                    End If
                    logger($"  layer '{layerName}' DRAWN kind={CInt(layer.Kind)} blendOp={layer.BlendOp} opacity={layer.Opacity:F2} takesSkinTone={layer.TakesSkinTone} fmt={fmtStr}{maskNote}")
                End If

                ' Unbind sampler slots; textures themselves are freed in the Finally block.
                GL.ActiveTexture(TextureUnit.Texture2)
                GL.BindTexture(TextureTarget.Texture2D, 0)
                GL.ActiveTexture(TextureUnit.Texture1)
                GL.BindTexture(TextureTarget.Texture2D, 0)

                ' Next iteration reads from what we just wrote.
                readTexId = pingTex(writeIdx)
                writeIdx = 1 - writeIdx
            Next

            ' Whatever readTexId points at now is the final result. If it still points at
            ' originalTexId, no layer actually drew — return 0.
            If readTexId = originalTexId Then
                resultTex = 0
            Else
                resultTex = readTexId
            End If

            If logger IsNot Nothing Then logger($"  drew {drawnLayers} of {totalLayers} layers on channel {CInt(channel)}")

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0)
        Catch ex As Exception
            If logger IsNot Nothing Then logger($"  EXCEPTION {ex.GetType().Name}: {ex.Message}")
            resultTex = 0
        Finally
            ' Free every GL texture that the batch loader created for this pass. The
            ' Texture_Loaded_Class entries are one-off — we own them and must release here.
            If batchLoaded IsNot Nothing Then
                For Each kvp In batchLoaded
                    Dim e = kvp.Value
                    If e IsNot Nothing AndAlso e.Texture_ID <> 0 Then
                        Try : GL.DeleteTexture(e.Texture_ID) : Catch : End Try
                    End If
                Next
            End If

            ' Free whichever ping-pong texture is NOT the result. Free both FBOs.
            For i As Integer = 0 To 1
                If pingFbo(i) <> 0 Then
                    Try : GL.DeleteFramebuffer(pingFbo(i)) : Catch : End Try
                End If
                If pingTex(i) <> 0 AndAlso pingTex(i) <> resultTex Then
                    Try : GL.DeleteTexture(pingTex(i)) : Catch : End Try
                End If
            Next

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

    ' Region-swap fragment shader. Hard replace gated by the BC1 mask:
    '   weight = mask.r   (BC1 is grayscale-in-RGB so any channel works; .r is canonical)
    '   result = mix(prev, swap, weight)
    ' No tint colours, no blend ops, no per-iter opacity — the mask itself is the only
    ' authored coverage signal. The swap texture is treated as authoritative inside the
    ' masked region (it already contains the intended base diffuse / normal / spec).
    Private Const RegionSwapFragmentSource As String = "#version 430
in vec2 vUV;
out vec4 fragColor;

uniform sampler2D uPrev;
uniform sampler2D uSwap;
uniform sampler2D uMask;

void main() {
    vec3 base = texture(uPrev, vUV).rgb;
    vec3 swap = texture(uSwap, vUV).rgb;
    float w   = texture(uMask, vUV).r;
    fragColor = vec4(mix(base, swap, w), 1.0);
}"

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
    Public Function ApplyRegionSwapsOntoFaceTexture(originalTexId As Integer,
                                                     width As Integer, height As Integer,
                                                     swaps As IList(Of FaceRegionSwapInput),
                                                     channel As FaceTintChannel,
                                                     Optional logger As Action(Of String) = Nothing) As Integer
        If originalTexId = 0 OrElse width <= 0 OrElse height <= 0 Then Return 0
        If swaps Is Nothing OrElse swaps.Count = 0 Then Return 0

        EnsureCompositorInitialized()
        EnsureRegionSwapInitialized()
        If _swapProgram = 0 OrElse _quadVao = 0 Then Return 0

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

        Dim pingTex(1) As Integer
        Dim pingFbo(1) As Integer
        pingTex(0) = 0 : pingTex(1) = 0
        pingFbo(0) = 0 : pingFbo(1) = 0
        Dim resultTex As Integer = 0
        Dim batchLoaded As Dictionary(Of String, PreviewModel.Texture_Loaded_Class) = Nothing

        Try
            ' === Batch preload every DDS this pass needs in ONE wrapper call. ===
            ' Per swap: its own swap channel bytes + its region mask bytes. Mask is the
            ' same DDS for every channel (D/N/S) so a higher-level cache could share it
            ' across the three pre-passes — for now we re-upload per channel which is
            ' simple and matches the pattern used by ComposeOntoFaceTexture.
            Dim loadKeys As New List(Of String)
            Dim loadBytes As New List(Of Byte())
            Dim swapTexKey As New Dictionary(Of Integer, String)
            Dim swapMaskKey As New Dictionary(Of Integer, String)
            For i As Integer = 0 To swaps.Count - 1
                Dim sw = swaps(i)
                If sw Is Nothing Then Continue For
                Dim sb = sw.GetSwapBytes(channel)
                If sb Is Nothing OrElse sb.Length = 0 Then Continue For
                If sw.RegionMaskDdsBytes Is Nothing OrElse sw.RegionMaskDdsBytes.Length = 0 Then Continue For
                Dim kS = $"s{i}t"
                Dim kM = $"s{i}m"
                loadKeys.Add(kS) : loadBytes.Add(sb) : swapTexKey(i) = kS
                loadKeys.Add(kM) : loadBytes.Add(sw.RegionMaskDdsBytes) : swapMaskKey(i) = kM
            Next
            If loadKeys.Count = 0 Then
                If logger IsNot Nothing Then logger($"  no swap contributes to channel {CInt(channel)}, skip")
                Return 0
            End If

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

            ' Allocate two ping-pong colour attachments matching the face texture size.
            For i As Integer = 0 To 1
                pingTex(i) = GL.GenTexture()
                GL.BindTexture(TextureTarget.Texture2D, pingTex(i))
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, CInt(TextureMinFilter.Linear))
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, CInt(TextureMagFilter.Linear))
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, CInt(TextureWrapMode.ClampToEdge))
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, CInt(TextureWrapMode.ClampToEdge))
                GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8,
                              width, height, 0,
                              PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero)

                pingFbo(i) = GL.GenFramebuffer()
                GL.BindFramebuffer(FramebufferTarget.Framebuffer, pingFbo(i))
                GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
                                        TextureTarget.Texture2D, pingTex(i), 0)
                Dim status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer)
                If status <> FramebufferErrorCode.FramebufferComplete Then
                    Return 0
                End If
            Next

            GL.Viewport(0, 0, width, height)
            GL.Disable(EnableCap.DepthTest)
            GL.Disable(EnableCap.ScissorTest)
            GL.Disable(EnableCap.Blend)

            GL.UseProgram(_swapProgram)
            GL.BindVertexArray(_quadVao)

            Dim writeIdx As Integer = 0
            Dim readTexId As Integer = originalTexId
            Dim drawn As Integer = 0

            For i As Integer = 0 To swaps.Count - 1
                Dim sw = swaps(i)
                If sw Is Nothing Then Continue For
                Dim swapName As String = If(String.IsNullOrEmpty(sw.DebugName), "<unnamed>", sw.DebugName)

                Dim sKey As String = Nothing
                Dim mKey As String = Nothing
                If Not swapTexKey.TryGetValue(i, sKey) OrElse Not swapMaskKey.TryGetValue(i, mKey) Then
                    If logger IsNot Nothing Then logger($"  swap '{swapName}' SKIP missing-channel-or-mask")
                    Continue For
                End If

                Dim sEntry As PreviewModel.Texture_Loaded_Class = Nothing
                Dim mEntry As PreviewModel.Texture_Loaded_Class = Nothing
                If Not batchLoaded.TryGetValue(sKey, sEntry) OrElse sEntry Is Nothing OrElse sEntry.Texture_ID = 0 _
                   OrElse Not batchLoaded.TryGetValue(mKey, mEntry) OrElse mEntry Is Nothing OrElse mEntry.Texture_ID = 0 Then
                    If logger IsNot Nothing Then logger($"  swap '{swapName}' SKIP dds-load-failed")
                    Continue For
                End If

                GL.BindFramebuffer(FramebufferTarget.Framebuffer, pingFbo(writeIdx))

                GL.ActiveTexture(TextureUnit.Texture0)
                GL.BindTexture(TextureTarget.Texture2D, readTexId)
                GL.Uniform1(_uSwapPrevLoc, 0)

                GL.ActiveTexture(TextureUnit.Texture1)
                GL.BindTexture(TextureTarget.Texture2D, sEntry.Texture_ID)
                GL.Uniform1(_uSwapTexLoc, 1)

                GL.ActiveTexture(TextureUnit.Texture2)
                GL.BindTexture(TextureTarget.Texture2D, mEntry.Texture_ID)
                GL.Uniform1(_uSwapMaskLoc, 2)

                GL.DrawArrays(PrimitiveType.Triangles, 0, 6)
                drawn += 1
                If logger IsNot Nothing Then
                    logger($"  swap '{swapName}' DRAWN swapFmt={DescribeFormat(sEntry.DGXFormat_Original, sEntry.DGXFormat_Final)} maskFmt={DescribeFormat(mEntry.DGXFormat_Original, mEntry.DGXFormat_Final)}")
                End If

                GL.ActiveTexture(TextureUnit.Texture2)
                GL.BindTexture(TextureTarget.Texture2D, 0)
                GL.ActiveTexture(TextureUnit.Texture1)
                GL.BindTexture(TextureTarget.Texture2D, 0)

                readTexId = pingTex(writeIdx)
                writeIdx = 1 - writeIdx
            Next

            If readTexId = originalTexId Then
                resultTex = 0
            Else
                resultTex = readTexId
            End If

            If logger IsNot Nothing Then logger($"  drew {drawn} of {swaps.Count} swaps on channel {CInt(channel)}")

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0)
        Catch ex As Exception
            If logger IsNot Nothing Then logger($"  EXCEPTION {ex.GetType().Name}: {ex.Message}")
            resultTex = 0
        Finally
            If batchLoaded IsNot Nothing Then
                For Each kvp In batchLoaded
                    Dim e = kvp.Value
                    If e IsNot Nothing AndAlso e.Texture_ID <> 0 Then
                        Try : GL.DeleteTexture(e.Texture_ID) : Catch : End Try
                    End If
                Next
            End If

            For i As Integer = 0 To 1
                If pingFbo(i) <> 0 Then
                    Try : GL.DeleteFramebuffer(pingFbo(i)) : Catch : End Try
                End If
                If pingTex(i) <> 0 AndAlso pingTex(i) <> resultTex Then
                    Try : GL.DeleteTexture(pingTex(i)) : Catch : End Try
                End If
            Next

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
    Private Const UniformBlendFragmentSource As String = "#version 430
in vec2 vUV;
out vec4 fragColor;

uniform sampler2D uPrev;
uniform vec3 uColor;
uniform int uBlendOp;

vec3 blendDefault(vec3 d, vec3 s) { return s; }
vec3 blendMultiply(vec3 d, vec3 s) { return d * s; }
vec3 blendOverlay(vec3 d, vec3 s) {
    return mix(2.0 * d * s,
               1.0 - 2.0 * (1.0 - d) * (1.0 - s),
               step(0.5, d));
}
vec3 blendSoftLight(vec3 d, vec3 s) {
    vec3 g = mix(((16.0 * d - 12.0) * d + 4.0) * d,
                 sqrt(d),
                 step(0.25, d));
    return mix(d - (1.0 - 2.0 * s) * d * (1.0 - d),
               d + (2.0 * s - 1.0) * (g - d),
               step(0.5, s));
}
vec3 blendHardLight(vec3 d, vec3 s) { return blendOverlay(s, d); }

void main() {
    vec3 prev = texture(uPrev, vUV).rgb;
    vec3 blended;
    if (uBlendOp == 1)      blended = blendMultiply(prev, uColor);
    else if (uBlendOp == 2) blended = blendOverlay(prev, uColor);
    else if (uBlendOp == 3) blended = blendSoftLight(prev, uColor);
    else if (uBlendOp == 4) blended = blendHardLight(prev, uColor);
    else                    blended = blendDefault(prev, uColor);
    fragColor = vec4(blended, 1.0);
}"

    ''' <summary>Apply a single uniform-colour blend onto an entire face texture and return
    ''' the new GL texture ID. The original is left untouched. Returns 0 on failure.
    ''' MUST run on the GL thread.
    '''
    ''' Used by the body SkinTone pre-pass (softlight(body_diffuse, QNAM)) so the body and
    ''' face produce symmetric results when ENABLE_TWO_STEP_SKIN_TINT is on. There is no
    ''' mask — the blend covers the entire texture.
    '''
    ''' blendOp follows the BGSCharacterTint enum: 0=Default 1=Multiply 2=Overlay 3=SoftLight 4=HardLight.</summary>
    Public Function ApplyUniformBlendOntoFaceTexture(originalTexId As Integer,
                                                      width As Integer, height As Integer,
                                                      r As Single, g As Single, b As Single,
                                                      blendOp As Integer,
                                                      Optional logger As Action(Of String) = Nothing) As Integer
        If originalTexId = 0 OrElse width <= 0 OrElse height <= 0 Then Return 0

        EnsureCompositorInitialized()
        EnsureUniformBlendInitialized()
        If _uniformBlendProgram = 0 OrElse _quadVao = 0 Then Return 0

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

            GL.UseProgram(_uniformBlendProgram)
            GL.BindVertexArray(_quadVao)

            GL.ActiveTexture(TextureUnit.Texture0)
            GL.BindTexture(TextureTarget.Texture2D, originalTexId)
            GL.Uniform1(_uUbPrevLoc, 0)

            GL.Uniform3(_uUbColorLoc, r, g, b)
            GL.Uniform1(_uUbBlendOpLoc, blendOp)

            GL.DrawArrays(PrimitiveType.Triangles, 0, 6)
            resultTex = outTex

            If logger IsNot Nothing Then logger($"  uniform-blend DRAWN blendOp={blendOp} color=({r:F3},{g:F3},{b:F3}) onto {width}x{height}")
        Catch ex As Exception
            If logger IsNot Nothing Then logger($"  EXCEPTION {ex.GetType().Name}: {ex.Message}")
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

    Private Sub EnsureUniformBlendInitialized()
        If _uniformBlendProgram <> 0 Then Return

        Dim vs = GL.CreateShader(ShaderType.VertexShader)
        GL.ShaderSource(vs, VertexShaderSource)
        GL.CompileShader(vs)
        Dim vsOk As Integer
        GL.GetShader(vs, ShaderParameter.CompileStatus, vsOk)
        If vsOk = 0 Then
            GL.DeleteShader(vs)
            Return
        End If

        Dim fs = GL.CreateShader(ShaderType.FragmentShader)
        GL.ShaderSource(fs, UniformBlendFragmentSource)
        GL.CompileShader(fs)
        Dim fsOk As Integer
        GL.GetShader(fs, ShaderParameter.CompileStatus, fsOk)
        If fsOk = 0 Then
            GL.DeleteShader(vs)
            GL.DeleteShader(fs)
            Return
        End If

        _uniformBlendProgram = GL.CreateProgram()
        GL.AttachShader(_uniformBlendProgram, vs)
        GL.AttachShader(_uniformBlendProgram, fs)
        GL.LinkProgram(_uniformBlendProgram)
        GL.DetachShader(_uniformBlendProgram, vs)
        GL.DetachShader(_uniformBlendProgram, fs)
        GL.DeleteShader(vs)
        GL.DeleteShader(fs)

        Dim linkOk As Integer
        GL.GetProgram(_uniformBlendProgram, GetProgramParameterName.LinkStatus, linkOk)
        If linkOk = 0 Then
            GL.DeleteProgram(_uniformBlendProgram)
            _uniformBlendProgram = 0
            Return
        End If

        _uUbPrevLoc = GL.GetUniformLocation(_uniformBlendProgram, "uPrev")
        _uUbColorLoc = GL.GetUniformLocation(_uniformBlendProgram, "uColor")
        _uUbBlendOpLoc = GL.GetUniformLocation(_uniformBlendProgram, "uBlendOp")
    End Sub

    Private Sub EnsureRegionSwapInitialized()
        If _swapProgram <> 0 Then Return

        Dim vs = GL.CreateShader(ShaderType.VertexShader)
        GL.ShaderSource(vs, VertexShaderSource)
        GL.CompileShader(vs)
        Dim vsOk As Integer
        GL.GetShader(vs, ShaderParameter.CompileStatus, vsOk)
        If vsOk = 0 Then
            GL.DeleteShader(vs)
            Return
        End If

        Dim fs = GL.CreateShader(ShaderType.FragmentShader)
        GL.ShaderSource(fs, RegionSwapFragmentSource)
        GL.CompileShader(fs)
        Dim fsOk As Integer
        GL.GetShader(fs, ShaderParameter.CompileStatus, fsOk)
        If fsOk = 0 Then
            GL.DeleteShader(vs)
            GL.DeleteShader(fs)
            Return
        End If

        _swapProgram = GL.CreateProgram()
        GL.AttachShader(_swapProgram, vs)
        GL.AttachShader(_swapProgram, fs)
        GL.LinkProgram(_swapProgram)
        GL.DetachShader(_swapProgram, vs)
        GL.DetachShader(_swapProgram, fs)
        GL.DeleteShader(vs)
        GL.DeleteShader(fs)

        Dim linkOk As Integer
        GL.GetProgram(_swapProgram, GetProgramParameterName.LinkStatus, linkOk)
        If linkOk = 0 Then
            GL.DeleteProgram(_swapProgram)
            _swapProgram = 0
            Return
        End If

        _uSwapPrevLoc = GL.GetUniformLocation(_swapProgram, "uPrev")
        _uSwapTexLoc = GL.GetUniformLocation(_swapProgram, "uSwap")
        _uSwapMaskLoc = GL.GetUniformLocation(_swapProgram, "uMask")
    End Sub

    Private Sub EnsureCompositorInitialized()
        If _program <> 0 AndAlso _quadVao <> 0 Then Return

        Dim vs = GL.CreateShader(ShaderType.VertexShader)
        GL.ShaderSource(vs, VertexShaderSource)
        GL.CompileShader(vs)
        Dim vsOk As Integer
        GL.GetShader(vs, ShaderParameter.CompileStatus, vsOk)
        If vsOk = 0 Then
            GL.DeleteShader(vs)
            Return
        End If

        Dim fs = GL.CreateShader(ShaderType.FragmentShader)
        GL.ShaderSource(fs, FragmentShaderSource)
        GL.CompileShader(fs)
        Dim fsOk As Integer
        GL.GetShader(fs, ShaderParameter.CompileStatus, fsOk)
        If fsOk = 0 Then
            GL.DeleteShader(vs)
            GL.DeleteShader(fs)
            Return
        End If

        _program = GL.CreateProgram()
        GL.AttachShader(_program, vs)
        GL.AttachShader(_program, fs)
        GL.LinkProgram(_program)
        GL.DetachShader(_program, vs)
        GL.DetachShader(_program, fs)
        GL.DeleteShader(vs)
        GL.DeleteShader(fs)

        Dim linkOk As Integer
        GL.GetProgram(_program, GetProgramParameterName.LinkStatus, linkOk)
        If linkOk = 0 Then
            GL.DeleteProgram(_program)
            _program = 0
            Return
        End If

        _uPrevLoc = GL.GetUniformLocation(_program, "uPrev")
        _uLayerLoc = GL.GetUniformLocation(_program, "uLayer")
        _uLayerDiffuseAlphaLoc = GL.GetUniformLocation(_program, "uLayerDiffuseAlpha")
        _uHasDiffuseMaskLoc = GL.GetUniformLocation(_program, "uHasDiffuseMask")
        _uColorLoc = GL.GetUniformLocation(_program, "uColor")
        _uOpacityLoc = GL.GetUniformLocation(_program, "uOpacity")
        _uBlendOpLoc = GL.GetUniformLocation(_program, "uBlendOp")
        _uLayerKindLoc = GL.GetUniformLocation(_program, "uLayerKind")
        _uChannelLoc = GL.GetUniformLocation(_program, "uChannel")

        Dim quadVerts() As Single = {
            -1.0F, -1.0F,
             1.0F, -1.0F,
            -1.0F, 1.0F,
            -1.0F, 1.0F,
             1.0F, -1.0F,
             1.0F, 1.0F
        }
        _quadVao = GL.GenVertexArray()
        _quadVbo = GL.GenBuffer()
        GL.BindVertexArray(_quadVao)
        GL.BindBuffer(BufferTarget.ArrayBuffer, _quadVbo)
        GL.BufferData(BufferTarget.ArrayBuffer, quadVerts.Length * 4, quadVerts, BufferUsageHint.StaticDraw)
        GL.EnableVertexAttribArray(0)
        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, False, 2 * 4, 0)
        GL.BindBuffer(BufferTarget.ArrayBuffer, 0)
        GL.BindVertexArray(0)
    End Sub
End Module
