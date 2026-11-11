Option Strict On
Option Explicit On

Imports OpenTK.Graphics.OpenGL4

''' <summary>SKYRIM SE'S CHARACTERLIGHT INPUT t11 (Ola SSE S1 F3.6; s1_re.md 6, P5, rev-06, rev-19): the texture the PS reads as t11(1, 1).x times LumScale (rec 4801 l.94-102). The engine binds there the adapted
''' luminance of its HDR (SetupMaterial 0x1415493D9..41A, the render target [0x1420D7608], address 0, 0x14154942D); the preview's is an input of design of the app (rev-06): the rig's luma
''' (LightRigUniforms.CharacterLightLuminance), a 1 x 1 R32F, uploaded when its bits change: it reads no target of the frame, so two equal frames give the same bits.</summary>
Friend NotInheritable Class SseCharacterLightInput
    Implements IDisposable

    Private _tex As Integer
    Private _bits As UInteger

    ''' <summary>The texture (0 before the first Upload). GATE: read back by --sse-character-light.</summary>
    Friend ReadOnly Property Texture As Integer
        Get
            Return _tex
        End Get
    End Property

    ''' <summary>The frame's value into the texture (the context current): a new value's bits only.</summary>
    Friend Sub Upload(luminance As Single)
        Dim bits = BitConverter.SingleToUInt32Bits(luminance)
        If _tex <> 0 AndAlso bits = _bits Then Exit Sub
        Dim v = {luminance}
        If _tex = 0 Then
            _tex = Gl43.CreateTexture(TextureTarget.Texture2D)
            Gl43.TexStorage2D(_tex, TextureTarget2d.Texture2D, 1, SizedInternalFormat.R32f, 1, 1)
            Gl43.TexParameter(_tex, TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, CInt(TextureMinFilter.Nearest))
            Gl43.TexParameter(_tex, TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, CInt(TextureMagFilter.Nearest))
            Gl43.TexParameter(_tex, TextureTarget.Texture2D, TextureParameterName.TextureWrapS, CInt(TextureWrapMode.ClampToEdge))
            Gl43.TexParameter(_tex, TextureTarget.Texture2D, TextureParameterName.TextureWrapT, CInt(TextureWrapMode.ClampToEdge))
        End If
        GL.ActiveTexture(TextureUnit.Texture11)
        GL.BindTexture(TextureTarget.Texture2D, _tex)
        GL.PixelStore(PixelStoreParameter.UnpackAlignment, 4)
        GL.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, 1, 1, OpenTK.Graphics.OpenGL4.PixelFormat.Red, PixelType.Float, v)
        GL.BindTexture(TextureTarget.Texture2D, 0)
        GL.ActiveTexture(TextureUnit.Texture0)
        _bits = bits
    End Sub

    ''' <summary>The texture deleted (the context current).</summary>
    Friend Sub Free()
        If _tex <> 0 Then GL.DeleteTexture(_tex) : _tex = 0
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        Free()
    End Sub
End Class
