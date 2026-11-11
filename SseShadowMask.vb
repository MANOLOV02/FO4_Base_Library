Option Strict On
Option Explicit On

Imports OpenTK.Graphics.OpenGL4
Imports OpenTK.Mathematics

''' <summary>SKYRIM SE'S SCREEN-SPACE SHADOW MASK (Ola SSE S1 F3.3; s1_re.md 4.2, rev-01, rev-12, rev-17): the pass 0x141539B10 draws, for each light that casts,
''' a fullscreen PS into a colour target; each light writes its own channel and the lighting PS reads it in t14.
''' <list type="bullet">
''' <item>The target: RT 0x12, RGBA8_UNORM of the screen's size (format [+8] = 0x1C at 0x14153B75F, width and height 0x14153B6E9 / 6F4, created 0x14153BBD4), no depth
''' (0x141539B38..B4F). Cleared to (1, 0, 0, 0): SetClearColor 0x14100A3F0 at 0x141539AA4..AC6, mode 0 of SetRenderTarget clears (0x141010E5E..E6F).</item>
''' <item>The channel of light k: write modes 5..8 (table 0x141B436A0, 0x141539CA1..CB9) are RenderTargetWriteMask 1 / 2 / 4 / 8 of RT 0 (0x14102AF6C..AFB6): k.</item>
''' <item>t14 of the colour pass of light k (P1): a texture view with its R swizzled from channel k (<see cref="ChannelView"/>), recreated with the target (rev-17):
''' the PS reads t14.x as the game's sun, untouched.</item>
''' <item>t4: the map's layers of light k, a two-layer view (ShadowMapTarget.SplitsView): the PS's split index picks the layer. Sampled by the mask's sampler (SG 0x141568238..251: address 0, filter 4 of the table
''' 0x14102B910): CLAMP on the three axes (0x14102B988..99D), D3D11_FILTER_COMPARISON_MIN_MAG_LINEAR_MIP_POINT 0x94, ComparisonFunc 2 LESS (0x14102BA5D..A72).</item>
''' </list>
''' Each split's camera is the engine's (EngineSunBox over the preview's camera; user decision 6-nov-2026, replaces rev-27).</summary>
Friend NotInheritable Class SseShadowMask
    Implements IDisposable

    Private _tex, _fbo, _w, _h, _vao, _vbo, _sampler As Integer
    Private ReadOnly _channel(3) As Integer

    ''' <summary>The target's texture (GATE: read back; the colour pass: through ChannelView).</summary>
    Friend ReadOnly Property Texture As Integer
        Get
            Return _tex
        End Get
    End Property

    Friend ReadOnly Property Framebuffer As Integer
        Get
            Return _fbo
        End Get
    End Property

    ''' <summary>The view of channel <paramref name="k"/> with R taken from it (rev-17).</summary>
    Friend ReadOnly Property ChannelView(k As Integer) As Integer
        Get
            Return _channel(k)
        End Get
    End Property

    ''' <summary>The fullscreen triangle: POSITION0 at the engine layout's location (Fo4EngineVertexLayout.Sem.Position0), clip x, y (the mask's VS: mov o0.xy, v0.xy, rec 11137 L24).</summary>
    Friend ReadOnly Property Vao As Integer
        Get
            Return _vao
        End Get
    End Property

    ''' <summary>The comparison sampler of t4.</summary>
    Friend ReadOnly Property Sampler As Integer
        Get
            Return _sampler
        End Get
    End Property

    ''' <summary>THE VALUE t5 GIVES THE MASK WITH FOCUS SHADOWS (user decision 9-nov-2026: as the game). The engine binds in t5 the stencil of depth-stencil target 0,
    ''' [0x143331F40 + 0x20A8] (0x1415680D7..14156813F; the technique's own bind 0x141566A7A..AD0), the view created only at 0x14100AB65..B81 with format 0x2F,
    ''' X24_TYPELESS_G8_UINT; target 0 is the only one (0x141015790 is xor eax, eax). The ten mask programs with a focus branch (shaders011.fxp, the one fxp of
    ''' Skyrim - Shaders.bsa: 11373, 11378, 11380, 11382, 11384, 11392, 11397, 11399, 11401, 11403; D:\WinTmp\rc\olasse\s1\f6\foco_sse\informe.md 1) read its component x (rec 11382 L1047
    ''' ld r0.z, r2, t5.yzxw) and enter the branch only if it is not 0 (L1140 if_nz). In that view x is the 24 unused bits: D3D11.3 19.1.3.1 ("X - Unused bits in
    ''' format") and 19.1.3.3 (a missing component reads 0); the engine's index i + 1 lives in .y. Measured: the game's own rec 11382 bytecode in D3D11 on the RTX
    ''' 2080 Ti gives no focus shadow with stencil 1 (D:\WinTmp\rc\olasse\s1\f6\capucha3\run11382; WARP too, f6\foco_sse\stcw). So the texture is one texel holding <see cref="FocusIndexX"/>:
    ''' every pixel skips the focus branch, as in the game.</summary>
    Friend ReadOnly Property FocusIndexTexture As Integer
        Get
            If _focusIndex = 0 Then
                Dim texel = FocusIndexX
#If DEBUG Then
                ' ShadowGate --ray-law [focus-index]: the index the 8-nov preview gave every pixel (focus 0 applied).
                If Fo4GBufferConstants.GateMutantOn("sse-focus-index-one") Then texel = CByte(1)
#End If
                _focusIndex = Gl43.CreateTexture(TextureTarget.Texture2D)
                Gl43.TexStorage2D(_focusIndex, TextureTarget2d.Texture2D, 1, SizedInternalFormat.R8ui, 1, 1)
                Gl43.TexSubImage2D(_focusIndex, TextureTarget.Texture2D, 0, 0, 0, 1, 1, PixelFormat.RedInteger, PixelType.UnsignedByte, New Byte() {texel})
                Gl43.TexParameter(_focusIndex, TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, CInt(TextureMinFilter.Nearest))
                Gl43.TexParameter(_focusIndex, TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, CInt(TextureMagFilter.Nearest))
            End If
            Return _focusIndex
        End Get
    End Property
    Private _focusIndex As Integer

    ''' <summary>The x component of the engine's t5 view (X24_TYPELESS_G8_UINT): the unused bits, 0 (see <see cref="FocusIndexTexture"/>).</summary>
    Friend Const FocusIndexX As Byte = 0

    ''' <summary>The target for a screen of <paramref name="w"/> x <paramref name="h"/>: created, or recreated with its views when the size changes (rev-17).</summary>
    Friend Function Ensure(w As Integer, h As Integer) As Boolean
        If w <= 0 OrElse h <= 0 Then Return False
        If _vao = 0 Then
            _vao = GL.GenVertexArray() : _vbo = GL.GenBuffer()
            GL.BindVertexArray(_vao)
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo)
            GL.BufferData(BufferTarget.ArrayBuffer, 6 * 4, New Single() {-1.0F, -1.0F, 3.0F, -1.0F, -1.0F, 3.0F}, BufferUsageHint.StaticDraw)
            GL.EnableVertexAttribArray(CInt(Fo4EngineVertexLayout.Sem.Position0))
            GL.VertexAttribPointer(CInt(Fo4EngineVertexLayout.Sem.Position0), 2, VertexAttribPointerType.Float, False, 8, 0)
            GL.BindVertexArray(0)
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0)
            _sampler = GL.GenSampler()
            GL.SamplerParameter(_sampler, SamplerParameterName.TextureMinFilter, CInt(TextureMinFilter.Linear))
            GL.SamplerParameter(_sampler, SamplerParameterName.TextureMagFilter, CInt(TextureMagFilter.Linear))
            GL.SamplerParameter(_sampler, SamplerParameterName.TextureWrapS, CInt(TextureWrapMode.ClampToEdge))
            GL.SamplerParameter(_sampler, SamplerParameterName.TextureWrapT, CInt(TextureWrapMode.ClampToEdge))
            GL.SamplerParameter(_sampler, SamplerParameterName.TextureCompareMode, CInt(TextureCompareMode.CompareRefToTexture))
            GL.SamplerParameter(_sampler, SamplerParameterName.TextureCompareFunc, CInt(All.Less))
        End If
        If _tex <> 0 AndAlso w = _w AndAlso h = _h Then Return True
        FreeTarget()
        _tex = Gl43.CreateTexture(TextureTarget.Texture2D)
        Gl43.TexStorage2D(_tex, TextureTarget2d.Texture2D, 1, SizedInternalFormat.Rgba8, w, h)
        Gl43.TexParameter(_tex, TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, CInt(TextureMinFilter.Linear))
        Gl43.TexParameter(_tex, TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, CInt(TextureMagFilter.Linear))
        Gl43.TexParameter(_tex, TextureTarget.Texture2D, TextureParameterName.TextureWrapS, CInt(TextureWrapMode.ClampToEdge))
        Gl43.TexParameter(_tex, TextureTarget.Texture2D, TextureParameterName.TextureWrapT, CInt(TextureWrapMode.ClampToEdge))
        Dim canales = {All.Red, All.Green, All.Blue, All.Alpha}
        For k = 0 To 3
            _channel(k) = GL.GenTexture()
            GL.TextureView(_channel(k), TextureTarget.Texture2D, _tex, PixelInternalFormat.Rgba8, 0, 1, 0, 1)
            GL.BindTexture(TextureTarget.Texture2D, _channel(k))
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureSwizzleR, CInt(canales(k)))
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, CInt(TextureMinFilter.Linear))
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, CInt(TextureMagFilter.Linear))
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, CInt(TextureWrapMode.ClampToEdge))
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, CInt(TextureWrapMode.ClampToEdge))
            GL.BindTexture(TextureTarget.Texture2D, 0)
        Next
        Dim prev = GL.GetInteger(GetPName.FramebufferBinding)
        _fbo = GL.GenFramebuffer()
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo)
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, _tex, 0)
        GL.DrawBuffer(DrawBufferMode.ColorAttachment0)
        Dim status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer)
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, prev)
        If status <> FramebufferErrorCode.FramebufferComplete Then
            Logger.Log($"[SSE-MASK] incomplete framebuffer ({status}) at {w}x{h}")
            FreeTarget()
            Return False
        End If
        _w = w : _h = h
        Return True
    End Function

    Private Sub FreeTarget()
        If _fbo <> 0 Then GL.DeleteFramebuffer(_fbo) : _fbo = 0
        For k = 0 To 3
            If _channel(k) <> 0 Then GL.DeleteTexture(_channel(k)) : _channel(k) = 0
        Next
        If _tex <> 0 Then GL.DeleteTexture(_tex) : _tex = 0
        _w = 0 : _h = 0
    End Sub

    ''' <summary>Every GL object deleted (the context current).</summary>
    Friend Sub Free()
        If _focusIndex <> 0 Then GL.DeleteTexture(_focusIndex) : _focusIndex = 0
        FreeTarget()
        If _vao <> 0 Then GL.DeleteVertexArray(_vao) : _vao = 0
        If _vbo <> 0 Then GL.DeleteBuffer(_vbo) : _vbo = 0
        If _sampler <> 0 Then GL.DeleteSampler(_sampler) : _sampler = 0
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        Free()
    End Sub
End Class
