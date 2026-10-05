Imports OpenTK.Graphics.OpenGL4

''' <summary>FALLOUT 4'S WORLD ENVMAP ARRAY (the "EnvMapArray" of BSDFCompositeShader), one per GL context. Tools/re-docs/
''' RE_FO4_WORLD_ENVMAP_2026-10-03.md 1-3, 7, 13, 16, 19.
''' <list type="bullet">
''' <item>State (1.1): 42 entries {texture, stamp} (iMaxNumberOfCubemapsPerFrame = 42, [0x142F993D0]; no user INI sets it), a lock
''' per entry and period, the queue of slices to copy, the array.</item>
''' <item>Register (1.2, 0x142239930), called INSIDE the G-buffer draw of each shape with property bit 7: returns the slice or -1
''' (0xFF).</item>
''' <item>Copy (1.4, 0x142239B60, the first call of the composite 0x1421F82C4): creates the array from the first queued cube's
''' description (format = its storage's sRGB twin, 13.3 / 16.2) and copies every queued slice, mip by mip and face by face.</item>
''' <item>Reset on model load (user decision rev-09 b): PreviewModel load event (Reset).</item>
''' </list>
''' Held declared: the exact semantics of the period counter [0x143E5DFA0] (6): the preview's period is its frame; the engine's
''' linear subresource copy that shifts the faces of a cube with MORE mips than the array (1.4) is not reproduced (vanilla has none:
''' every compatible cube is 128x128x8, 5).</summary>
Friend NotInheritable Class Fo4EnvMapArray
    Private Const SliceCount As Integer = 42

    ''' <summary>A slice's texture: the loader's entry AND the GL name it had when registered (v4 B-rev-03). The engine keys by
    ''' (texture pointer, creation frame tex+0x38) so a new texture at a reused address is never taken for the old one (ENV 8.5);
    ''' GL recycles deleted names, a re-upload makes a new entry, and an entry can be handed a new name: both halves are compared.</summary>
    Private Structure Entry
        Public Tex As PreviewModel.Texture_Loaded_Class
        Public Texture As Integer
        Public Dxgi As Integer            ' the promoted DXGI byte [tex+0x2D] of the cube (MakeSrgbDxgi of the file's format)
        Public Stamp As Long
    End Structure

    Private Shared Function Same(e As Entry, tex As PreviewModel.Texture_Loaded_Class) As Boolean
        Return e.Tex IsNot Nothing AndAlso ReferenceEquals(e.Tex, tex) AndAlso e.Texture = tex.Texture_ID
    End Function

    Private ReadOnly _entries(SliceCount - 1) As Entry
    Private ReadOnly _locked(SliceCount - 1) As Boolean
    Private ReadOnly _queue As New List(Of Integer)
    Private _array As Integer
    Private _arrayW As Integer, _arrayH As Integer, _arrayLevels As Integer, _arrayFormat As Integer, _arrayDxgi As Integer
    Private _period As Long

    ''' <summary>The array texture (0 before the first copy).</summary>
    Public ReadOnly Property Texture As Integer
        Get
            Return _array
        End Get
    End Property

    ''' <summary>Opens the frame's period (the stamp the register writes).</summary>
    Public Sub BeginPeriod(frameIndex As Long)
        _period = frameIndex
    End Sub

    Private Shared Function Describe(tex As Integer, ByRef w As Integer, ByRef h As Integer, ByRef levels As Integer, ByRef fmt As Integer) As Boolean
        If tex = 0 Then Return False
        Dim immutable As Integer
        GL.GetTextureParameter(tex, GetTextureParameter.TextureImmutableFormat, immutable)
        GL.GetTextureParameter(tex, GetTextureParameter.TextureImmutableLevels, levels)
        GL.GetTextureLevelParameter(tex, 0, GetTextureParameter.TextureWidth, w)
        GL.GetTextureLevelParameter(tex, 0, GetTextureParameter.TextureHeight, h)
        GL.GetTextureLevelParameter(tex, 0, GetTextureParameter.TextureInternalFormat, fmt)
        Return immutable <> 0 AndAlso levels > 0
    End Function

    ''' <summary>The register 0x142239930: the slice of the material's slot-4 texture <paramref name="tex"/> (Nothing when the slot
    ''' resolved to nothing), or -1. The compared format is the DXGI byte the loader PROMOTED (MakeSRGB 0x14183E680, ENV 13.3 / 16.2:
    ''' DirectXDDSLoader.MakeSrgbDxgi of the file's DXGI), not the GL storage (rev-03: 0x1C / 0x57 / 0x58 share GL_RGBA8 and promote
    ''' to three different bytes); the cube flag byte[+0x2E] is compared too (ENV 1.2, rev-46).</summary>
    Public Function Register(tex As PreviewModel.Texture_Loaded_Class) As Integer
        If tex Is Nothing OrElse tex.Texture_ID = 0 Then Return -1                       ' 0x14223993C
        Dim cube = tex.Texture_ID
        Dim w, h, lv, fmt As Integer
        If Not Describe(cube, w, h, lv, fmt) Then Return -1
        Dim promoted = DirectXDDSLoader.MakeSrgbDxgi(tex.DGXFormat_Original)
        If _array <> 0 Then
            ' 0x142239942..0x142239980: same height, width, format and cube flag, mips >= the array's.
            If h <> _arrayH OrElse w <> _arrayW OrElse promoted <> _arrayDxgi OrElse Not tex.Cubemap OrElse lv < _arrayLevels Then Return -1
        ElseIf _queue.Count > 0 Then
            ' 0x1422399AA..0x1422399EE: the same comparison against the first queued cube (0x142239CC0).
            Dim first = _entries(_queue(0))
            Dim w0, h0, lv0, f0 As Integer
            Describe(first.Texture, w0, h0, lv0, f0)
            If h <> h0 OrElse w <> w0 OrElse promoted <> first.Dxgi OrElse Not tex.Cubemap OrElse lv < lv0 Then Return -1
        ElseIf w <> 128 Then
            Return -1                                                                     ' word[tex+0x2A] == 0x80 (7.2)
        ElseIf Not tex.Cubemap Then
            ' The engine seeds with the width alone; a 2D seed (no vanilla subject, declared) cannot become a cube array slice in GL.
            Return -1
        End If
        ' 0x142239A30..0x142239ABB: first the same texture (no lock), else an empty entry, else the oldest stamp with a lock.
        Dim best = -1
        Dim bestStamp = Long.MaxValue
        For i = 0 To SliceCount - 1
            If Same(_entries(i), tex) Then
                _entries(i).Stamp = _period
                Return i
            End If
            If _entries(i).Tex Is Nothing Then
                If Not _locked(i) Then
                    _locked(i) = True
                    best = i
                    Exit For
                End If
            ElseIf _entries(i).Stamp < bestStamp AndAlso Not _locked(i) Then
                If best >= 0 Then _locked(best) = False
                _locked(i) = True
                best = i : bestStamp = _entries(i).Stamp
            End If
        Next
        If best < 0 Then Return -1                                                        ' 0x142239AC6
        _entries(best).Stamp = _period                                                    ' 0x142239ACE..0x142239B23
        If Not Same(_entries(best), tex) Then
            _entries(best).Tex = tex
            _entries(best).Texture = cube
            _entries(best).Dxgi = promoted
            _queue.Add(best)
        End If
        Return best
    End Function

    ''' <summary>The copy 0x142239B60: array from the first queued cube, every queued slice copied; queue and locks cleared.</summary>
    Public Sub CopyQueued()
        If _array = 0 AndAlso _queue.Count > 0 Then
            Dim w, h, lv, fmt As Integer
            If Describe(_entries(_queue(0)).Texture, w, h, lv, fmt) Then
                _arrayW = w : _arrayH = h : _arrayLevels = lv : _arrayDxgi = _entries(_queue(0)).Dxgi
                _arrayFormat = DirectXDDSLoader.SrgbTwinOf(fmt)                ' the SRV is promoted to sRGB (13.3, 16.2)
                GL.CreateTextures(TextureTarget.TextureCubeMapArray, 1, _array)
                GL.TextureStorage3D(_array, lv, CType(_arrayFormat, SizedInternalFormat), w, h, SliceCount * 6)
                GL.TextureParameter(_array, TextureParameterName.TextureWrapS, CInt(TextureWrapMode.ClampToEdge))
                GL.TextureParameter(_array, TextureParameterName.TextureWrapT, CInt(TextureWrapMode.ClampToEdge))
                GL.TextureParameter(_array, TextureParameterName.TextureMinFilter, CInt(TextureMinFilter.LinearMipmapLinear))
                GL.TextureParameter(_array, TextureParameterName.TextureMagFilter, CInt(TextureMagFilter.Linear))
            End If
        End If
        If _array <> 0 Then
            For Each slice In _queue
                Dim src = _entries(slice).Texture
                For level = 0 To _arrayLevels - 1
                    Dim lw = Math.Max(1, _arrayW >> level), lh = Math.Max(1, _arrayH >> level)
                    ' One call per mip: the 6 faces of the cube (src layers 0..5) -> array layers slice*6 .. slice*6+5.
                    GL.CopyImageSubData(src, ImageTarget.TextureCubeMap, level, 0, 0, 0,
                                        _array, ImageTarget.TextureCubeMapArray, level, 0, 0, slice * 6, lw, lh, 6)
                Next
            Next
        End If
        _queue.Clear()
        Array.Clear(_locked, 0, _locked.Length)
    End Sub

    ''' <summary>Model load (user decision rev-09 b): entries, queue and array are released. Texture deletions need no reset: a
    ''' slice is matched by its entry and GL name (Same), never by the name alone.</summary>
    Public Sub Reset()
        Array.Clear(_entries, 0, _entries.Length)
        Array.Clear(_locked, 0, _locked.Length)
        _queue.Clear()
        If _array <> 0 Then GL.DeleteTexture(_array) : _array = 0
        _arrayW = 0 : _arrayH = 0 : _arrayLevels = 0 : _arrayFormat = 0 : _arrayDxgi = 0
    End Sub
End Class
