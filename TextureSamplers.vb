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
        GL.CreateSamplers(1, s)
        ' ADDR: U = S wraps for 2 / 3, V = T wraps for 1 / 3; clamp = CLAMP_TO_EDGE (D3D11_TEXTURE_ADDRESS_CLAMP).
        GL.SamplerParameter(s, SamplerParameterName.TextureWrapS, CInt(If((addr And 2) <> 0, TextureWrapMode.Repeat, TextureWrapMode.ClampToEdge)))
        GL.SamplerParameter(s, SamplerParameterName.TextureWrapT, CInt(If((addr And 1) <> 0, TextureWrapMode.Repeat, TextureWrapMode.ClampToEdge)))
        GL.SamplerParameter(s, SamplerParameterName.TextureWrapR, CInt(If((addr And 1) <> 0, TextureWrapMode.Repeat, TextureWrapMode.ClampToEdge)))
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
                    GL.GetFloat(CType(&H84FF, GetPName), _maxAniso)   ' GL_MAX_TEXTURE_MAX_ANISOTROPY
                Catch
                End Try
                While GL.GetError() <> ErrorCode.NoError : End While
            End If
            ' 16: SSE hard-coded, FO4 iMaxAnisotropy:Display default.
            If _maxAniso >= 1 Then GL.SamplerParameter(s, CType(&H84FE, SamplerParameterName), Math.Min(16.0F, _maxAniso))
        End If
        _cache(key) = s
        Return s
    End Function

    ''' <summary>The texture bound to a 2D / cube target has more than one level (a GL-complete mip chain).</summary>
    Public Shared Function HasMips(textureId As Integer) As Boolean
        If textureId = 0 Then Return False
        Dim inmutable As Integer, niveles As Integer
        GL.GetTextureParameter(textureId, GetTextureParameter.TextureImmutableFormat, inmutable)
        If inmutable <> 0 Then
            GL.GetTextureParameter(textureId, CType(&H82DF, GetTextureParameter), niveles)   ' GL_TEXTURE_IMMUTABLE_LEVELS
            Return niveles > 1
        End If
        Dim maxLevel As Integer, w1 As Integer
        GL.GetTextureParameter(textureId, GetTextureParameter.TextureMaxLevel, maxLevel)
        GL.GetTextureLevelParameter(textureId, 1, GetTextureParameter.TextureWidth, w1)
        Return maxLevel > 0 AndAlso w1 > 0
    End Function

    Public Sub Dispose() Implements IDisposable.Dispose
        For Each s In _cache.Values
            GL.DeleteSampler(s)
        Next
        _cache.Clear()
    End Sub
End Class
