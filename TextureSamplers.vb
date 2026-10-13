Imports OpenTK.Graphics.OpenGL4

''' <summary>THE GL SAMPLER OBJECTS OF THE ENGINES' SAMPLER TABLES (SSE 0x14102B910, FO4 0x141856390), created on demand per
''' (ADDR, FILT, LOD, mipmapped) and bound per draw to the app's texture units (SamplerLaw). One context, one cache.
''' <para>D3D samples a texture with one level the same way whatever the mip filter; GL calls a texture whose level range is
''' incomplete for a mipmap filter "incomplete" and returns black. A texture with a single level gets the same sampler with
''' the non-mip form of its filter: the D3D result.</para></summary>
Friend NotInheritable Class TextureSamplers
    Implements IDisposable

    Private ReadOnly _cache As New Dictionary(Of (Integer, Integer, Single, Boolean), Integer)
    Private _maxAniso As Single = -1

    ''' <summary>The sampler of (<paramref name="addr"/>, <paramref name="filt"/>) for a texture with or without mips.</summary>
    Public Function Get_(addr As Integer, filt As Integer, lod As Single, mipmapped As Boolean) As Integer
        Dim key = (addr, filt, lod, mipmapped)
        Dim s As Integer
        If _cache.TryGetValue(key, s) Then Return s
        s = GL.GenSampler()
        ' ADDR: U = S wraps for 2 / 3, V = T wraps for 1 / 3, W = R wraps only for 3; clamp = CLAMP_TO_EDGE (D3D11_TEXTURE_ADDRESS_CLAMP).
        ' SSE 0x14102B910 (jump table 0x14102BB14: 1 = 0x14102B9A2 CLAMP / WRAP / CLAMP, 2 = 0x14102B9B9 WRAP / CLAMP / CLAMP,
        ' 3 = 0x14102B9D0 WRAP / WRAP / WRAP); FO4 0x141856390 (0x141856420..0x14185646C): the same rows
        ' (Tools/re-docs/RE_TEXTURE_ADDRESS_MODES_2026-10-03.md 1.1 / 1.4).
        GL.SamplerParameter(s, SamplerParameterName.TextureWrapS, CInt(If((addr And 2) <> 0, TextureWrapMode.Repeat, TextureWrapMode.ClampToEdge)))
        GL.SamplerParameter(s, SamplerParameterName.TextureWrapT, CInt(If((addr And 1) <> 0, TextureWrapMode.Repeat, TextureWrapMode.ClampToEdge)))
        GL.SamplerParameter(s, SamplerParameterName.TextureWrapR, CInt(If((addr And 3) = 3, TextureWrapMode.Repeat, TextureWrapMode.ClampToEdge)))
        Dim minF As TextureMinFilter, magF As TextureMagFilter
        Select Case filt
            Case 0
                magF = TextureMagFilter.Nearest
                minF = If(mipmapped, TextureMinFilter.NearestMipmapNearest, TextureMinFilter.Nearest)
            Case 1
                magF = TextureMagFilter.Linear
                minF = If(mipmapped, TextureMinFilter.LinearMipmapNearest, TextureMinFilter.Linear)
            Case Else
                magF = TextureMagFilter.Linear
                minF = If(mipmapped, TextureMinFilter.LinearMipmapLinear, TextureMinFilter.Linear)
        End Select
        GL.SamplerParameter(s, SamplerParameterName.TextureMinFilter, CInt(minF))
        GL.SamplerParameter(s, SamplerParameterName.TextureMagFilter, CInt(magF))
        If filt = 0 OrElse filt = 1 Then
            ' One mip: MinLOD = MaxLOD (SSE 0; FO4 the texture's LOD byte, 0; the FO4 effect env cube EnvMapMinLOD).
            GL.SamplerParameter(s, SamplerParameterName.TextureMinLod, lod)
            GL.SamplerParameter(s, SamplerParameterName.TextureMaxLod, lod)
        End If
        If filt = 3 Then
            If _maxAniso < 0 Then
                _maxAniso = 0
                Try
                    _maxAniso = Gl43.MaxAnisotropy()
                Catch
                End Try
                While GL.GetError() <> ErrorCode.NoError : End While
            End If
            ' 16: SSE hard-coded, FO4 iMaxAnisotropy:Display default.
            If _maxAniso >= 1 Then Gl43.SamplerAnisotropy(s, Math.Min(16.0F, _maxAniso))
        End If
        _cache(key) = s
        Return s
    End Function

    ''' <summary>The texture <paramref name="textureId"/> of <paramref name="target"/> (TEXTURE_2D or TEXTURE_CUBE_MAP) has more than one
    ''' level (a GL-complete mip chain).</summary>
    Public Shared Function HasMips(textureId As Integer, target As TextureTarget) As Boolean
        If textureId = 0 Then Return False
        Dim inmutable As Integer, niveles As Integer
        Gl43.GetTexParameter(textureId, target, GetTextureParameter.TextureImmutableFormat, inmutable)
        If inmutable <> 0 Then
            Gl43.GetTexParameter(textureId, target, CType(&H82DF, GetTextureParameter), niveles)   ' GL_TEXTURE_IMMUTABLE_LEVELS
            Return niveles > 1
        End If
        Dim maxLevel As Integer, w1 As Integer
        Gl43.GetTexParameter(textureId, target, GetTextureParameter.TextureMaxLevel, maxLevel)
        Gl43.GetTexLevelParameter(textureId, target, 1, GetTextureParameter.TextureWidth, w1)
        Return maxLevel > 0 AndAlso w1 > 0
    End Function

    Public Sub Dispose() Implements IDisposable.Dispose
        For Each s In _cache.Values
            GL.DeleteSampler(s)
        Next
        _cache.Clear()
    End Sub
End Class
