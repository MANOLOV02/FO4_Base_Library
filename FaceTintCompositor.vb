Imports OpenTK.Graphics.OpenGL4
Imports OpenTK.Mathematics
Imports DirectXTexWrapperCLI

' ============================================================================
' FaceTintCompositor — GPU/FBO-based composer for NPC face tint layers.
'
' For each tint layer the NPC brings (TETI/TEND subrecords), we render a fullscreen
' quad into an off-screen FBO with blending enabled. The compositor fragment shader
' outputs (layer_color.rgb, mask.r * layer_opacity), and alpha-over blending (via
' GL_SRC_ALPHA / GL_ONE_MINUS_SRC_ALPHA) accumulates each layer on top of the
' previous ones.
'
' The resulting color attachment texture is returned to the caller. It's a single
' RGBA texture where:
'   rgb = accumulated tint color where tints apply
'   a   = accumulated opacity (0 = no tint; 1 = fully tinted)
' The face shader binds this as texFaceTintOverlay and does
' `albedo = mix(albedo, overlay.rgb, overlay.a)`.
'
' This is the "apply on top" approach: base diffuse stays intact, overlay is a
' separate texture sampled in the face shader. No re-encoding of the base face
' diffuse, no CPU pixel iteration.
'
' MUST be called on the GL thread. Returns 0 if no layers produced any visible effect.
' ============================================================================

Public Class FaceTintLayerInput
    Public Property MaskDdsBytes As Byte()
    Public Property R As Byte
    Public Property G As Byte
    Public Property B As Byte
    Public Property Opacity As Single   ' 0..1
End Class

Public Module FaceTintCompositor

    ''' <summary>Canvas size for the composed overlay. Matches vanilla face texture resolution.</summary>
    Public Const CanvasSize As Integer = 1024

    ' Cached compositor program + fullscreen quad VAO. Created lazily on first use.
    Private _program As Integer = 0
    Private _uMaskLoc As Integer = -1
    Private _uColorLoc As Integer = -1
    Private _uOpacityLoc As Integer = -1
    Private _quadVao As Integer = 0
    Private _quadVbo As Integer = 0

    Private Const VertexShaderSource As String = "#version 430
layout(location = 0) in vec2 aPos;
out vec2 vUV;
void main() {
    vUV = vec2((aPos.x + 1.0) * 0.5, (aPos.y + 1.0) * 0.5);
    gl_Position = vec4(aPos, 0.0, 1.0);
}"

    ' Premultiplied-alpha output: rgb carries (color * alpha), a carries alpha.
    ' Combined with GL_ONE / GL_ONE_MINUS_SRC_ALPHA blending this gives correct
    ' alpha-over accumulation for BOTH rgb and a (standard GL_SRC_ALPHA/...
    ' applies the same factor to alpha too and produces wrong accumulated alpha).
    Private Const FragmentShaderSource As String = "#version 430
in vec2 vUV;
out vec4 fragColor;
uniform sampler2D uMask;
uniform vec3 uColor;
uniform float uOpacity;
void main() {
    float maskR = texture(uMask, vUV).r;
    float alpha = maskR * uOpacity;
    fragColor = vec4(uColor * alpha, alpha);
}"

    ''' <summary>Compose a list of tint layers into an OpenGL texture and return its ID.
    ''' MUST be called on the GL thread (needs current context).
    ''' Returns 0 if no layers produced any visible contribution.</summary>
    Public Function ComposeToGLTexture(layers As IEnumerable(Of FaceTintLayerInput)) As Integer
        If layers Is Nothing Then Return 0
        Dim layerList = layers.Where(Function(l) l IsNot Nothing AndAlso l.MaskDdsBytes IsNot Nothing AndAlso l.Opacity > 0.001F).ToList()
        If layerList.Count = 0 Then Return 0

        EnsureCompositorInitialized()
        If _program = 0 OrElse _quadVao = 0 Then Return 0

        ' Save relevant GL state so we can restore after compositing.
        Dim prevFbo As Integer = GL.GetInteger(GetPName.DrawFramebufferBinding)
        Dim prevProg As Integer = GL.GetInteger(GetPName.CurrentProgram)
        Dim prevVao As Integer = GL.GetInteger(GetPName.VertexArrayBinding)
        Dim prevActiveTex As Integer = GL.GetInteger(GetPName.ActiveTexture)
        Dim prevViewport(3) As Integer
        GL.GetInteger(GetPName.Viewport, prevViewport)
        Dim wasBlend As Boolean = GL.IsEnabled(EnableCap.Blend)
        Dim wasDepth As Boolean = GL.IsEnabled(EnableCap.DepthTest)
        Dim wasScissor As Boolean = GL.IsEnabled(EnableCap.ScissorTest)

        Dim resultTexId As Integer = 0
        Dim fbo As Integer = 0

        Try
            ' 1) Create destination RGBA8 texture
            resultTexId = GL.GenTexture()
            GL.BindTexture(TextureTarget.Texture2D, resultTexId)
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, CInt(TextureMinFilter.Linear))
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, CInt(TextureMagFilter.Linear))
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, CInt(TextureWrapMode.ClampToEdge))
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, CInt(TextureWrapMode.ClampToEdge))
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8,
                          CanvasSize, CanvasSize, 0,
                          PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero)

            ' 2) Create FBO + attach
            fbo = GL.GenFramebuffer()
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, fbo)
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0,
                                    TextureTarget.Texture2D, resultTexId, 0)
            Dim status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer)
            If status <> FramebufferErrorCode.FramebufferComplete Then Return 0

            ' 3) Set GL state for compositing
            GL.Viewport(0, 0, CanvasSize, CanvasSize)
            GL.Disable(EnableCap.DepthTest)
            GL.Disable(EnableCap.ScissorTest)
            GL.Enable(EnableCap.Blend)
            ' Premultiplied-over: src already contains (color * alpha, alpha)
            GL.BlendFunc(BlendingFactor.One, BlendingFactor.OneMinusSrcAlpha)
            GL.ClearColor(0.0F, 0.0F, 0.0F, 0.0F)
            GL.Clear(ClearBufferMask.ColorBufferBit)

            GL.UseProgram(_program)
            GL.BindVertexArray(_quadVao)

            ' 4) For each layer: upload mask as temp GL texture, set uniforms, draw quad, delete temp tex
            Dim anyApplied As Boolean = False
            For Each layer In layerList
                Dim maskTex As Integer = CreateGLTextureFromDDS(layer.MaskDdsBytes)
                If maskTex = 0 Then Continue For

                GL.ActiveTexture(TextureUnit.Texture0)
                GL.BindTexture(TextureTarget.Texture2D, maskTex)
                GL.Uniform1(_uMaskLoc, 0)
                GL.Uniform3(_uColorLoc,
                            CSng(layer.R) / 255.0F,
                            CSng(layer.G) / 255.0F,
                            CSng(layer.B) / 255.0F)
                GL.Uniform1(_uOpacityLoc, Math.Max(0.0F, Math.Min(1.0F, layer.Opacity)))

                GL.DrawArrays(PrimitiveType.Triangles, 0, 6)
                anyApplied = True

                GL.BindTexture(TextureTarget.Texture2D, 0)
                GL.DeleteTexture(maskTex)
            Next

            ' 5) Detach, release, return
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0)

            If Not anyApplied Then
                GL.DeleteTexture(resultTexId)
                resultTexId = 0
            End If
        Catch
            If resultTexId <> 0 Then
                Try : GL.DeleteTexture(resultTexId) : Catch : End Try
                resultTexId = 0
            End If
        Finally
            If fbo <> 0 Then
                Try : GL.DeleteFramebuffer(fbo) : Catch : End Try
            End If
            ' Restore state
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, prevFbo)
            GL.UseProgram(prevProg)
            GL.BindVertexArray(prevVao)
            GL.ActiveTexture(CType(prevActiveTex, TextureUnit))
            GL.Viewport(prevViewport(0), prevViewport(1), prevViewport(2), prevViewport(3))
            If wasDepth Then GL.Enable(EnableCap.DepthTest) Else GL.Disable(EnableCap.DepthTest)
            If wasScissor Then GL.Enable(EnableCap.ScissorTest) Else GL.Disable(EnableCap.ScissorTest)
            If wasBlend Then GL.Enable(EnableCap.Blend) Else GL.Disable(EnableCap.Blend)
        End Try

        Return resultTexId
    End Function

    ''' <summary>Decode a DDS byte buffer into a one-off RGBA GL texture (no mipmaps, filtered linearly).
    ''' Used to load each tint layer mask into GPU memory for compositing. Caller must delete the texture.</summary>
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
            ' Free decoded CPU data
            For Each l In tex.Levels
                l.Data = Nothing
            Next
            tex.Levels.Clear()
            Return texID
        Catch
            Return 0
        End Try
    End Function

    ''' <summary>Lazily compile the compositor shader + build the fullscreen quad VAO.</summary>
    Private Sub EnsureCompositorInitialized()
        If _program <> 0 AndAlso _quadVao <> 0 Then Return

        ' Compile shaders
        Dim vs = GL.CreateShader(ShaderType.VertexShader)
        GL.ShaderSource(vs, VertexShaderSource)
        GL.CompileShader(vs)
        Dim vsOk As Integer
        GL.GetShader(vs, ShaderParameter.CompileStatus, vsOk)
        If vsOk = 0 Then
            Dim infoLog = GL.GetShaderInfoLog(vs)
            System.Diagnostics.Debug.WriteLine($"[FaceTintCompositor] VS compile failed: {infoLog}")
            GL.DeleteShader(vs)
            Return
        End If

        Dim fs = GL.CreateShader(ShaderType.FragmentShader)
        GL.ShaderSource(fs, FragmentShaderSource)
        GL.CompileShader(fs)
        Dim fsOk As Integer
        GL.GetShader(fs, ShaderParameter.CompileStatus, fsOk)
        If fsOk = 0 Then
            Dim infoLog = GL.GetShaderInfoLog(fs)
            System.Diagnostics.Debug.WriteLine($"[FaceTintCompositor] FS compile failed: {infoLog}")
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
            Dim infoLog = GL.GetProgramInfoLog(_program)
            System.Diagnostics.Debug.WriteLine($"[FaceTintCompositor] program link failed: {infoLog}")
            GL.DeleteProgram(_program)
            _program = 0
            Return
        End If

        _uMaskLoc = GL.GetUniformLocation(_program, "uMask")
        _uColorLoc = GL.GetUniformLocation(_program, "uColor")
        _uOpacityLoc = GL.GetUniformLocation(_program, "uOpacity")

        ' Build fullscreen quad as 2 triangles in NDC (-1..+1)
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
