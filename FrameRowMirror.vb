Option Strict On
Option Explicit On

Imports OpenTK.Graphics.OpenGL4

''' <summary>THE ROW MIRROR OF ONE PREVIEW (one GL context): turns an image of the frame between D3D and GL row order, in place
''' (MirrorRows), or into another texture (CopyMirrored). The engine frame of both games is drawn in D3D row order (glClipControl
''' UPPER_LEFT: Fallout 4 since the zfight-overlay fix, 8-oct-2026, and ola 1; Skyrim SE since the SSE wave's S0-C); the one owner of
''' that stage is PreviewControl (BeginD3dRowsStage .. EndD3dRowsStage), which turns the frame's targets here at its end - the single
''' mirror before everything that reads them in GL rows (ISRefraction, the post, the display overlays, the capture). Fallout 4's
''' tonemap t3 (RT 0x1E, Fo4DeferredTargets.CopyGlossGlRows) is turned with CopyMirrored.
''' <para>One scratch image per format it turns - R11F_G11F_B10F, RGBA8 and DEPTH24_STENCIL8, the formats of the frame's targets
''' (SceneTargets) and of the refraction normals (RefractionTargets: RGBA8 Skyrim SE, R11F_G11F_B10F Fallout 4) - and its read / draw
''' framebuffers, allocated by Ensure for the frame's size.</para></summary>
Friend NotInheritable Class FrameRowMirror
    Private _w As Integer, _h As Integer
    Private _r11, _rgba8, _ds, _fboSrc, _fboDst As Integer

    ''' <summary>Allocates (or reallocates on resize) the scratch images and framebuffers for <paramref name="w"/> x
    ''' <paramref name="h"/>.</summary>
    Public Sub Ensure(w As Integer, h As Integer)
        If _fboSrc <> 0 AndAlso w = _w AndAlso h = _h Then Return
        Free()
        _r11 = Scratch(SizedInternalFormat.R11fG11fB10f, w, h)
        _rgba8 = Scratch(SizedInternalFormat.Rgba8, w, h)
        _ds = Scratch(SizedInternalFormat.Depth24Stencil8, w, h)
        _fboSrc = Gl43.CreateFramebuffer()
        _fboDst = Gl43.CreateFramebuffer()
        _w = w : _h = h
    End Sub

    Private Shared Function Scratch(fmt As SizedInternalFormat, w As Integer, h As Integer) As Integer
        Dim t = Gl43.CreateTexture(TextureTarget.Texture2D)
        Gl43.TexStorage2D(t, TextureTarget2d.Texture2D, 1, fmt, w, h)
        Return t
    End Function

    ''' <summary>One image of the frame turned between D3D and GL row order, in place: blitted mirrored in y into the scratch image of
    ''' its format (NEAREST, same size and format: nothing converted), then copied back raw (CopyImageSubData, GL 4.6 18.3.2: "a
    ''' manner similar to a CPU memcpy"). <paramref name="format"/> is the image's: R11F_G11F_B10F, RGBA8 or DEPTH24_STENCIL8 (depth
    ''' and stencil together). The frame's own framebuffers are not touched (the image is attached to this class's pair).</summary>
    Public Sub MirrorRows(image As Integer, target As ImageTarget, format As SizedInternalFormat)
        Dim scratch As Integer
        Select Case format
            Case SizedInternalFormat.R11fG11fB10f : scratch = _r11
            Case SizedInternalFormat.Rgba8 : scratch = _rgba8
            Case SizedInternalFormat.Depth24Stencil8 : scratch = _ds
            Case Else : Throw New ArgumentException($"MirrorRows: no scratch image of format {format}", NameOf(format))
        End Select
        BlitMirrored(image, target, scratch, format, copyBack:=True)
    End Sub

    ''' <summary><paramref name="image"/> mirrored in y into the texture <paramref name="destination"/> (same size and format, the
    ''' frame's), <paramref name="image"/> left as it is.</summary>
    Public Sub CopyMirrored(image As Integer, target As ImageTarget, destination As Integer, format As SizedInternalFormat)
        BlitMirrored(image, target, destination, format, copyBack:=False)
    End Sub

    ''' <summary>The one mirrored blit (NEAREST, same size and format: nothing converted) of <paramref name="image"/> into the texture
    ''' <paramref name="destination"/>; with <paramref name="copyBack"/> <paramref name="destination"/> is the scratch and the result
    ''' goes back over <paramref name="image"/> raw (MirrorRows).</summary>
    Private Sub BlitMirrored(image As Integer, target As ImageTarget, destination As Integer, format As SizedInternalFormat, copyBack As Boolean)
        Dim isDs = format = SizedInternalFormat.Depth24Stencil8
        Dim att = If(isDs, FramebufferAttachment.DepthStencilAttachment, FramebufferAttachment.ColorAttachment0)
        Dim attach = Sub(fbo As Integer, img As Integer, tgt As ImageTarget)
                         If tgt = ImageTarget.Renderbuffer Then
                             Gl43.FramebufferRenderbuffer(fbo, att, RenderbufferTarget.Renderbuffer, img)
                         Else
                             Gl43.FramebufferTexture(fbo, att, img, 0)
                         End If
                     End Sub
        attach(_fboSrc, image, target)
        attach(_fboDst, destination, ImageTarget.Texture2D)
        Gl43.ReadBuffer(_fboSrc, If(isDs, ReadBufferMode.None, ReadBufferMode.ColorAttachment0))
        Gl43.DrawBuffer(_fboDst, If(isDs, DrawBufferMode.None, DrawBufferMode.ColorAttachment0))
        Try
            ' Never copy back from a blit that did not happen: an incomplete pair would leave the scratch's old rows, and
            ' CopyImageSubData would write them over the frame silently. The blit honours the scissor (GL 4.6 18.3.1) and this
            ' runs from a Finally after whatever state an aborted stage left: off.
            Dim src = Gl43.CheckFramebufferStatus(_fboSrc, FramebufferTarget.ReadFramebuffer)
            Dim dst = Gl43.CheckFramebufferStatus(_fboDst, FramebufferTarget.DrawFramebuffer)
            If src <> FramebufferStatus.FramebufferComplete OrElse dst <> FramebufferStatus.FramebufferComplete Then
                Throw New InvalidOperationException($"MirrorRows: incomplete framebuffer (source {src}, scratch {dst}) for {format}")
            End If
            GL.Disable(EnableCap.ScissorTest)
            Dim mask = If(isDs, ClearBufferMask.DepthBufferBit Or ClearBufferMask.StencilBufferBit, ClearBufferMask.ColorBufferBit)
            Gl43.BlitFramebuffer(_fboSrc, _fboDst, 0, 0, _w, _h, 0, _h, _w, 0, mask, BlitFramebufferFilter.Nearest)
            If copyBack Then GL.CopyImageSubData(destination, ImageTarget.Texture2D, 0, 0, 0, 0, image, target, 0, 0, 0, 0, _w, _h, 1)
        Finally
            attach(_fboSrc, 0, target)
            attach(_fboDst, 0, ImageTarget.Texture2D)
        End Try
    End Sub

    Public Sub Free()
        For Each fb In {_fboSrc, _fboDst}
            If fb <> 0 Then GL.DeleteFramebuffer(fb)
        Next
        For Each t In {_r11, _rgba8, _ds}
            If t <> 0 Then GL.DeleteTexture(t)
        Next
        _fboSrc = 0 : _fboDst = 0 : _r11 = 0 : _rgba8 = 0 : _ds = 0
        _w = 0 : _h = 0
    End Sub
End Class
