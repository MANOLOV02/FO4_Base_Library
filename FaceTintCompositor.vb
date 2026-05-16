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
    Friend _uLayerDiffuseAlphaLoc As Integer = -1
    Friend _uHasDiffuseMaskLoc As Integer = -1
    Friend _uColorLoc As Integer = -1
    Friend _uOpacityLoc As Integer = -1
    Friend _uBlendOpLoc As Integer = -1
    Friend _uLayerKindLoc As Integer = -1
    Friend _uChannelLoc As Integer = -1
    Friend _quadVao As Integer = 0
    Friend _quadVbo As Integer = 0

    ' Region-swap program (separate from the tint compositor). Created lazily by
    ' EnsureRegionSwapInitialized. Shares _quadVao/_quadVbo with the tint compositor.
    Friend _swapProgram As Integer = 0
    Friend _uSwapPrevLoc As Integer = -1
    Friend _uSwapTexLoc As Integer = -1
    Friend _uSwapMaskLoc As Integer = -1

    ' Uniform-blend program. Created lazily by EnsureUniformBlendInitialized.
    Friend _uniformBlendProgram As Integer = 0
    Friend _uUbPrevLoc As Integer = -1
    Friend _uUbColorLoc As Integer = -1
    Friend _uUbBlendOpLoc As Integer = -1
    Friend _uUbOpacityLoc As Integer = -1

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
        If _swapProgram <> 0 Then
            Try : GL.DeleteProgram(_swapProgram) : Catch : End Try
            _swapProgram = 0
        End If
        If _uniformBlendProgram <> 0 Then
            Try : GL.DeleteProgram(_uniformBlendProgram) : Catch : End Try
            _uniformBlendProgram = 0
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
    vec4 prevRgba = texture(uPrev, vUV);
    vec3 prev = prevRgba.rgb;
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
        fragColor = vec4(finalRgb, prevRgba.a);
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
    fragColor = vec4(finalRgb, prevRgba.a);
}"

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
        If state Is Nothing Then Throw New ArgumentNullException(NameOf(state))
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

            GL.UseProgram(state._program)
            GL.BindVertexArray(state._quadVao)

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
            Dim readTexId As Integer = originalTexId  ' first iteration reads from the unmodified face diffuse
            Dim drawnSoFar As Integer = 0

            Dim drawnLayers As Integer = 0
            Dim totalLayers As Integer = If(layers IsNot Nothing, layers.Count, 0)
            For i As Integer = 0 To layers.Count - 1
                Dim layer = layers(i)
                If layer Is Nothing Then Continue For
                Dim layerName As String = If(String.IsNullOrEmpty(layer.DebugName), "<unnamed>", layer.DebugName)

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

                GL.Uniform3(state._uColorLoc,
                            CSng(layer.R) / 255.0F,
                            CSng(layer.G) / 255.0F,
                            CSng(layer.B) / 255.0F)
                GL.Uniform1(state._uOpacityLoc, Math.Max(0.0F, Math.Min(1.0F, layer.Opacity)))
                GL.Uniform1(state._uBlendOpLoc, layer.BlendOp)
                GL.Uniform1(state._uLayerKindLoc, CInt(layer.Kind))
                GL.Uniform1(state._uChannelLoc, CInt(channel))

                GL.DrawArrays(PrimitiveType.Triangles, 0, 6)
                drawnLayers += 1

                ' Unbind sampler slots; textures themselves are freed in the Finally block.
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

    ' Region-swap fragment shader. Hard replace gated by the BC1 mask:
    '   weight = mask.r   (BC1 is grayscale-in-RGB so any channel works; .r is canonical)
    '   result = mix(prev, swap, weight)
    ' No tint colours, no blend ops, no per-iter opacity — the mask itself is the only
    ' authored coverage signal. The swap texture is treated as authoritative inside the
    ' masked region (it already contains the intended base diffuse / normal / spec).
    '
    ' Alpha contract: input alpha (from uPrev) is PRESERVED into the output. The swap and
    ' mask textures contribute only RGB / weight respectively; the accumulator alpha rides
    ' along untouched so callers passing alpha-tested diffuses do not lose their cutout.
    Private Const RegionSwapFragmentSource As String = "#version 430
in vec2 vUV;
out vec4 fragColor;

uniform sampler2D uPrev;
uniform sampler2D uSwap;
uniform sampler2D uMask;

void main() {
    vec4 baseRgba = texture(uPrev, vUV);
    vec3 base = baseRgba.rgb;
    vec3 swap = texture(uSwap, vUV).rgb;
    float w   = texture(uMask, vUV).r;
    fragColor = vec4(mix(base, swap, w), baseRgba.a);
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
    Public Function ApplyRegionSwapsOntoFaceTexture(state As FaceTintCompositorState,
                                                     originalTexId As Integer,
                                                     width As Integer, height As Integer,
                                                     swaps As IList(Of FaceRegionSwapInput),
                                                     channel As FaceTintChannel,
                                                     Optional cache As FaceTintTextureCache = Nothing) As Integer
        If state Is Nothing Then Throw New ArgumentNullException(NameOf(state))
        If originalTexId = 0 OrElse width <= 0 OrElse height <= 0 Then Return 0
        If swaps Is Nothing OrElse swaps.Count = 0 Then Return 0

        EnsureCompositorInitialized(state)
        EnsureRegionSwapInitialized(state)
        If state._swapProgram = 0 OrElse state._quadVao = 0 Then Return 0

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

            GL.UseProgram(state._swapProgram)
            GL.BindVertexArray(state._quadVao)

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
                Dim swapName As String = If(String.IsNullOrEmpty(sw.DebugName), "<unnamed>", sw.DebugName)

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
                GL.Uniform1(state._uSwapPrevLoc, 0)

                GL.ActiveTexture(TextureUnit.Texture1)
                GL.BindTexture(TextureTarget.Texture2D, sEntry.Texture_ID)
                GL.Uniform1(state._uSwapTexLoc, 1)

                GL.ActiveTexture(TextureUnit.Texture2)
                GL.BindTexture(TextureTarget.Texture2D, mEntry.Texture_ID)
                GL.Uniform1(state._uSwapMaskLoc, 2)

                GL.DrawArrays(PrimitiveType.Triangles, 0, 6)
                drawn += 1

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
    Private Const UniformBlendFragmentSource As String = "#version 430
in vec2 vUV;
out vec4 fragColor;

uniform sampler2D uPrev;
uniform vec3 uColor;
uniform int uBlendOp;
uniform float uOpacity;

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
    vec4 prevRgba = texture(uPrev, vUV);
    vec3 prev = prevRgba.rgb;
    vec3 blended;
    if (uBlendOp == 1)      blended = blendMultiply(prev, uColor);
    else if (uBlendOp == 2) blended = blendOverlay(prev, uColor);
    else if (uBlendOp == 3) blended = blendSoftLight(prev, uColor);
    else if (uBlendOp == 4) blended = blendHardLight(prev, uColor);
    else                    blended = blendDefault(prev, uColor);
    // Opacity attenuation: mix prev (no-op) toward blended (full strength).
    // Matches ComposeOntoFaceTexture main shader math `mix(prev, blended, coverage)`
    // so face compositor and body uniform-blend pass use the same attenuation formula.
    vec3 finalRgb = mix(prev, blended, clamp(uOpacity, 0.0, 1.0));
    fragColor = vec4(finalRgb, prevRgba.a);
}"

    ''' <summary>Apply a single uniform-colour blend onto an entire face texture and return
    ''' the new GL texture ID. The original is left untouched. Returns 0 on failure.
    ''' MUST run on the GL thread.
    '''
    ''' Used by the body SkinTone pre-pass (softlight(body_diffuse, QNAM)) and the face
    ''' fallback (TryApplyFaceSkinSoftLight). Same attenuation math as
    ''' <see cref="ComposeOntoFaceTexture"/>: <c>mix(prev, blended, opacity)</c> — caller passes
    ''' the FULL source colour (not pre-attenuated toward neutral grey) plus an opacity scalar
    ''' (typically tl.Value/100 or qnam.A/255). The shader interpolates between prev (no-op)
    ''' and the full-strength blend by the opacity factor. This matches the main compositor's
    ''' coverage math so face and body produce identical visual results for the same opacity.
    '''
    ''' blendOp follows the BGSCharacterTint enum: 0=Default 1=Multiply 2=Overlay 3=SoftLight 4=HardLight.</summary>
    Public Function ApplyUniformBlendOntoFaceTexture(state As FaceTintCompositorState,
                                                      originalTexId As Integer,
                                                      width As Integer, height As Integer,
                                                      r As Single, g As Single, b As Single,
                                                      blendOp As Integer,
                                                      opacity As Single) As Integer
        If state Is Nothing Then Throw New ArgumentNullException(NameOf(state))
        If originalTexId = 0 OrElse width <= 0 OrElse height <= 0 Then Return 0

        EnsureCompositorInitialized(state)
        EnsureUniformBlendInitialized(state)
        If state._uniformBlendProgram = 0 OrElse state._quadVao = 0 Then Return 0

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

            GL.UseProgram(state._uniformBlendProgram)
            GL.BindVertexArray(state._quadVao)

            GL.ActiveTexture(TextureUnit.Texture0)
            GL.BindTexture(TextureTarget.Texture2D, originalTexId)
            GL.Uniform1(state._uUbPrevLoc, 0)

            GL.Uniform3(state._uUbColorLoc, r, g, b)
            GL.Uniform1(state._uUbBlendOpLoc, blendOp)
            GL.Uniform1(state._uUbOpacityLoc, Math.Max(0.0F, Math.Min(1.0F, opacity)))

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

    Private Sub EnsureUniformBlendInitialized(state As FaceTintCompositorState)
        If state._uniformBlendProgram <> 0 Then Return

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

        state._uniformBlendProgram = GL.CreateProgram()
        GL.AttachShader(state._uniformBlendProgram, vs)
        GL.AttachShader(state._uniformBlendProgram, fs)
        GL.LinkProgram(state._uniformBlendProgram)
        GL.DetachShader(state._uniformBlendProgram, vs)
        GL.DetachShader(state._uniformBlendProgram, fs)
        GL.DeleteShader(vs)
        GL.DeleteShader(fs)

        Dim linkOk As Integer
        GL.GetProgram(state._uniformBlendProgram, GetProgramParameterName.LinkStatus, linkOk)
        If linkOk = 0 Then
            GL.DeleteProgram(state._uniformBlendProgram)
            state._uniformBlendProgram = 0
            Return
        End If

        state._uUbPrevLoc = GL.GetUniformLocation(state._uniformBlendProgram, "uPrev")
        state._uUbColorLoc = GL.GetUniformLocation(state._uniformBlendProgram, "uColor")
        state._uUbBlendOpLoc = GL.GetUniformLocation(state._uniformBlendProgram, "uBlendOp")
        state._uUbOpacityLoc = GL.GetUniformLocation(state._uniformBlendProgram, "uOpacity")
    End Sub

    Private Sub EnsureRegionSwapInitialized(state As FaceTintCompositorState)
        If state._swapProgram <> 0 Then Return

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

        state._swapProgram = GL.CreateProgram()
        GL.AttachShader(state._swapProgram, vs)
        GL.AttachShader(state._swapProgram, fs)
        GL.LinkProgram(state._swapProgram)
        GL.DetachShader(state._swapProgram, vs)
        GL.DetachShader(state._swapProgram, fs)
        GL.DeleteShader(vs)
        GL.DeleteShader(fs)

        Dim linkOk As Integer
        GL.GetProgram(state._swapProgram, GetProgramParameterName.LinkStatus, linkOk)
        If linkOk = 0 Then
            GL.DeleteProgram(state._swapProgram)
            state._swapProgram = 0
            Return
        End If

        state._uSwapPrevLoc = GL.GetUniformLocation(state._swapProgram, "uPrev")
        state._uSwapTexLoc = GL.GetUniformLocation(state._swapProgram, "uSwap")
        state._uSwapMaskLoc = GL.GetUniformLocation(state._swapProgram, "uMask")
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
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8,
                          width, height, 0,
                          PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero)

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
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8,
                      width, height, 0,
                      PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero)

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
            GL.DeleteProgram(state._program)
            state._program = 0
            Return
        End If

        state._uPrevLoc = GL.GetUniformLocation(state._program, "uPrev")
        state._uLayerLoc = GL.GetUniformLocation(state._program, "uLayer")
        state._uLayerDiffuseAlphaLoc = GL.GetUniformLocation(state._program, "uLayerDiffuseAlpha")
        state._uHasDiffuseMaskLoc = GL.GetUniformLocation(state._program, "uHasDiffuseMask")
        state._uColorLoc = GL.GetUniformLocation(state._program, "uColor")
        state._uOpacityLoc = GL.GetUniformLocation(state._program, "uOpacity")
        state._uBlendOpLoc = GL.GetUniformLocation(state._program, "uBlendOp")
        state._uLayerKindLoc = GL.GetUniformLocation(state._program, "uLayerKind")
        state._uChannelLoc = GL.GetUniformLocation(state._program, "uChannel")

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
                                          swaps As IList(Of FaceRegionSwapInput)) As FaceTintPipelineResult
        If state Is Nothing Then Throw New ArgumentNullException(NameOf(state))

        Dim result As New FaceTintPipelineResult With {
            .Diffuse = New FaceTintPipelineChannelResult With {.TextureId = srcDiffuseId, .IsFresh = False},
            .Normal = New FaceTintPipelineChannelResult With {.TextureId = srcNormalId, .IsFresh = False},
            .Specular = New FaceTintPipelineChannelResult With {.TextureId = srcSpecId, .IsFresh = False}
        }

        If width <= 0 OrElse height <= 0 Then Return result

        ' --- Region-swap pre-pass (no-op if swaps empty / no contribution to a channel) ---
        If swaps IsNot Nothing AndAlso swaps.Count > 0 Then
            ProcessChannel(result.Diffuse, FaceTintChannel.Diffuse, state, cache, width, height, Nothing, swaps)
            ProcessChannel(result.Normal, FaceTintChannel.Normal, state, cache, width, height, Nothing, swaps)
            ProcessChannel(result.Specular, FaceTintChannel.Specular, state, cache, width, height, Nothing, swaps)
        End If

        ' --- Tint compose ---
        If layers IsNot Nothing AndAlso layers.Count > 0 Then
            ProcessChannel(result.Diffuse, FaceTintChannel.Diffuse, state, cache, width, height, layers, Nothing)
            ProcessChannel(result.Normal, FaceTintChannel.Normal, state, cache, width, height, layers, Nothing)
            ProcessChannel(result.Specular, FaceTintChannel.Specular, state, cache, width, height, layers, Nothing)
        End If

        Return result
    End Function

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

