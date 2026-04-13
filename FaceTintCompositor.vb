Imports OpenTK.Graphics.OpenGL4
Imports OpenTK.Mathematics
Imports DirectXTexWrapperCLI

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
    ''' <summary>Optional debug label written to the log when this layer is applied.</summary>
    Public Property DebugName As String = ""

    ''' <summary>Get the DDS bytes for the requested channel. Returns Nothing if the layer doesn't
    ''' contribute to that channel (Palette layers contribute to Diffuse only).</summary>
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
    Private _uColorLoc As Integer = -1
    Private _uOpacityLoc As Integer = -1
    Private _uBlendOpLoc As Integer = -1
    Private _uLayerKindLoc As Integer = -1
    Private _uChannelLoc As Integer = -1
    Private _quadVao As Integer = 0
    Private _quadVbo As Integer = 0

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

    // Coverage source depends on layer kind AND channel:
    //   PaletteMask        : grayscale mask in .r, colour from uColor uniform
    //   TextureSet/Diffuse : pre-coloured RGBA, coverage in .a (FaceDetail diffuse maps
    //                        have variable alpha - verified empirically)
    //   TextureSet/Normal  : pre-coloured RGB normal map, no meaningful alpha
    //                        coverage = max(rgb) - black areas don't contribute
    //   TextureSet/Specular: pre-coloured RGB specular map, no meaningful alpha
    //                        coverage = max(rgb) - same logic as normal
    vec3 srcColor;
    float coverage;
    if (uLayerKind == 1) {
        srcColor = layerSample.rgb;
        if (uChannel == 0) {
            coverage = layerSample.a;
        } else {
            coverage = max(max(layerSample.r, layerSample.g), layerSample.b);
        }
    } else {
        srcColor = uColor;
        coverage = layerSample.r;
    }
    coverage *= uOpacity;
    coverage = clamp(coverage, 0.0, 1.0);

    vec3 blended;
    if (uBlendOp == 1)      blended = blendMultiply(prev, srcColor);
    else if (uBlendOp == 2) blended = blendOverlay(prev, srcColor);
    else if (uBlendOp == 3) blended = blendSoftLight(prev, srcColor);
    else if (uBlendOp == 4) blended = blendHardLight(prev, srcColor);
    else                    blended = blendDefault(prev, srcColor);

    vec3 finalRgb = mix(prev, blended, coverage);
    fragColor = vec4(finalRgb, 1.0);
}"

    ''' <summary>Backward-compat wrapper that composes onto the diffuse channel.</summary>
    Public Function ComposeOntoFaceDiffuse(originalTexId As Integer, width As Integer, height As Integer, layers As IList(Of FaceTintLayerInput)) As Integer
        Return ComposeOntoFaceTexture(originalTexId, width, height, layers, FaceTintChannel.Diffuse)
    End Function

    ''' <summary>Compose all layers that contribute to the requested channel onto a copy of the
    ''' supplied face texture (diffuse / normal / specular) and return the new GL texture ID.
    ''' The original is left untouched. Returns 0 on failure or when no layer contributes data
    ''' for the requested channel.
    ''' MUST run on the GL thread.</summary>
    Public Function ComposeOntoFaceTexture(originalTexId As Integer, width As Integer, height As Integer, layers As IList(Of FaceTintLayerInput), channel As FaceTintChannel) As Integer
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

        Try
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
                    If LayerDecisionLog IsNot Nothing Then LayerDecisionLog.Invoke($"[COMPOSITOR] FBO #{i} status not complete: {status}")
                    Return 0
                End If
            Next
            If LayerDecisionLog IsNot Nothing Then LayerDecisionLog.Invoke($"[COMPOSITOR/{channel}] FBOs allocated: {pingTex(0)}, {pingTex(1)}; original={originalTexId} ({width}x{height})")

            GL.Viewport(0, 0, width, height)
            GL.Disable(EnableCap.DepthTest)
            GL.Disable(EnableCap.ScissorTest)
            GL.Disable(EnableCap.Blend)   ' the shader does its own blending against uPrev

            GL.UseProgram(_program)
            GL.BindVertexArray(_quadVao)

            Dim writeIdx As Integer = 0
            Dim readTexId As Integer = originalTexId  ' first iteration reads from the unmodified face diffuse

            Dim drawnLayers As Integer = 0
            For Each layer In layers
                If layer Is Nothing Then Continue For
                Dim channelBytes = layer.GetChannelBytes(channel)
                If channelBytes Is Nothing OrElse channelBytes.Length = 0 Then
                    ' This layer has no data for the requested channel — skip silently.
                    Continue For
                End If
                Dim layerTex As Integer = CreateGLTextureFromDDS(channelBytes)
                If layerTex = 0 Then
                    If LayerDecisionLog IsNot Nothing Then LayerDecisionLog.Invoke($"[COMPOSITOR/{channel}] skip: DDS decode failed for '{layer.DebugName}' ({channelBytes.Length} bytes)")
                    Continue For
                End If

                If LayerDecisionLog IsNot Nothing Then
                    LayerDecisionLog.Invoke($"[COMPOSITOR/{channel}] draw '{layer.DebugName}' kind={layer.Kind} blendOp={layer.BlendOp} opacity={layer.Opacity:F2} layerTex={layerTex}")
                End If

                GL.BindFramebuffer(FramebufferTarget.Framebuffer, pingFbo(writeIdx))

                GL.ActiveTexture(TextureUnit.Texture0)
                GL.BindTexture(TextureTarget.Texture2D, readTexId)
                GL.Uniform1(_uPrevLoc, 0)

                GL.ActiveTexture(TextureUnit.Texture1)
                GL.BindTexture(TextureTarget.Texture2D, layerTex)
                GL.Uniform1(_uLayerLoc, 1)

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

                GL.ActiveTexture(TextureUnit.Texture1)
                GL.BindTexture(TextureTarget.Texture2D, 0)
                GL.DeleteTexture(layerTex)

                ' Next iteration reads from what we just wrote.
                readTexId = pingTex(writeIdx)
                writeIdx = 1 - writeIdx
            Next

            If LayerDecisionLog IsNot Nothing Then LayerDecisionLog.Invoke($"[COMPOSITOR/{channel}] drew {drawnLayers}/{layers.Count} layers")

            ' Whatever readTexId points at now is the final result. If it still points at
            ' originalTexId, no layer actually drew — return 0.
            If readTexId = originalTexId Then
                resultTex = 0
            Else
                resultTex = readTexId
            End If

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0)
        Catch ex As Exception
            If LayerDecisionLog IsNot Nothing Then LayerDecisionLog.Invoke($"[COMPOSITOR] EXCEPTION: {ex.GetType().Name}: {ex.Message}")
            resultTex = 0
        Finally
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

    ''' <summary>Optional callback for the host to log compositor decisions per layer (e.g. coverage source).</summary>
    Public LayerDecisionLog As Action(Of String) = Nothing

    ''' <summary>Decode a DDS byte buffer into a one-off RGBA GL texture. Caller must delete it.</summary>
    Private Function CreateGLTextureFromDDS(ddsBytes As Byte()) As Integer
        If ddsBytes Is Nothing OrElse ddsBytes.Length = 0 Then Return 0
        Try
            Dim tex = Loader.ConvertForBitmap(ddsBytes)
            If tex Is Nothing OrElse Not tex.Loaded OrElse tex.Levels.Count = 0 Then Return 0
            Dim lvl = tex.Levels(0)

            Dim texID As Integer = GL.GenTexture()
            GL.BindTexture(TextureTarget.Texture2D, texID)
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, CInt(TextureMinFilter.Linear))
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, CInt(TextureMagFilter.Linear))
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, CInt(TextureWrapMode.ClampToEdge))
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, CInt(TextureWrapMode.ClampToEdge))
            ' ConvertForBitmap returns BGRA8 data
            Dim handle = System.Runtime.InteropServices.GCHandle.Alloc(lvl.Data, System.Runtime.InteropServices.GCHandleType.Pinned)
            Try
                GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8,
                              lvl.Width, lvl.Height, 0,
                              PixelFormat.Bgra, PixelType.UnsignedByte, handle.AddrOfPinnedObject())
            Finally
                handle.Free()
            End Try
            GL.BindTexture(TextureTarget.Texture2D, 0)
            For Each l In tex.Levels
                l.Data = Nothing
            Next
            tex.Levels.Clear()
            Return texID
        Catch
            Return 0
        End Try
    End Function

    Private Sub TraceLog(msg As String)
        Try
            System.IO.File.AppendAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "compositor_trace.log"),
                                        $"[{DateTime.Now:HH:mm:ss.fff}] {msg}" & Environment.NewLine)
        Catch
        End Try
    End Sub

    Private Sub EnsureCompositorInitialized()
        If _program <> 0 AndAlso _quadVao <> 0 Then Return

        Dim vs = GL.CreateShader(ShaderType.VertexShader)
        GL.ShaderSource(vs, VertexShaderSource)
        GL.CompileShader(vs)
        Dim vsOk As Integer
        GL.GetShader(vs, ShaderParameter.CompileStatus, vsOk)
        If vsOk = 0 Then
            Dim infoLog = GL.GetShaderInfoLog(vs)
            TraceLog("VS COMPILE FAILED:" & Environment.NewLine & infoLog)
            GL.DeleteShader(vs)
            Return
        End If
        TraceLog("VS compiled OK")

        Dim fs = GL.CreateShader(ShaderType.FragmentShader)
        GL.ShaderSource(fs, FragmentShaderSource)
        GL.CompileShader(fs)
        Dim fsOk As Integer
        GL.GetShader(fs, ShaderParameter.CompileStatus, fsOk)
        If fsOk = 0 Then
            Dim infoLog = GL.GetShaderInfoLog(fs)
            TraceLog("FS COMPILE FAILED:" & Environment.NewLine & infoLog)
            GL.DeleteShader(vs)
            GL.DeleteShader(fs)
            Return
        End If
        TraceLog("FS compiled OK")

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
            Dim infoLog = GL.GetProgramInfoLog(_program)
            TraceLog("PROGRAM LINK FAILED:" & Environment.NewLine & infoLog)
            GL.DeleteProgram(_program)
            _program = 0
            Return
        End If
        TraceLog("program linked OK")

        _uPrevLoc = GL.GetUniformLocation(_program, "uPrev")
        _uLayerLoc = GL.GetUniformLocation(_program, "uLayer")
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
