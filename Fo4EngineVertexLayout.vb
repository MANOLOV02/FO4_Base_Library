Imports OpenTK.Graphics.OpenGL4

''' <summary>THE ENGINE'S INPUT LAYOUT of a vertex desc (Fallout4.exe 1.11.240 0x141823210; ola 2 propuesta-v3 3.3): per semantic the
''' slot (bit 44+k -> 0, bit 54+k -> 1), the DXGI format and the byte offset ((desc >> s) AND 0x3C). Prepass masks: 0x141828B77
''' (rigid) and 0x141828E34 (alpha test). The GL location of each semantic is fixed (EngineLocation).</summary>
Friend Module Fo4EngineVertexLayout
    Friend Enum Sem
        Position0 = 0 : Texcoord0 = 1 : Texcoord1 = 2 : Normal0 = 3 : Binormal0 = 4 : Color0 = 5
        BlendWeight0 = 6 : BlendIndices0 = 7 : Texcoord2 = 8 : Texcoord3 = 9 : Texcoord4 = 10
    End Enum
    Friend Structure Element
        Public Semantic As Sem, Slot As Integer, Offset As Integer, Components As Integer
        Public GlType As OpenTK.Graphics.OpenGL4.VertexAttribPointerType, Normalized As Boolean, PerInstance As Boolean
    End Structure
    Friend Const PrepassRigidMask As ULong = &H401000000000FFUL       ' 0x141828B77
    Friend Const PrepassAlphaMask As ULong = &HC030000F000FFFUL       ' 0x141828E34

    Private Function Present(desc As ULong, k As Integer, ByRef slot As Integer) As Boolean
        If ((desc >> (44 + k)) And 1UL) <> 0UL Then slot = 0 : Return True    ' 0x141823294 (each attribute: s0 test first)
        If ((desc >> (54 + k)) And 1UL) <> 0UL Then slot = 1 : Return True
        Return False
    End Function

    Friend Function Elements(desc As ULong) As List(Of Element)
        Dim r As New List(Of Element)
        Dim fl = VertexAttribPointerType.Float, hf = VertexAttribPointerType.HalfFloat, ub = VertexAttribPointerType.UnsignedByte
        ' POSITION: R32G32B32A32 when bits 44 and 54 are both set or both clear, else R16G16B16A16 (0x141823225..25F);
        ' slot = NOT bit 44 (0x141823272..286)
        Dim both = (desc And &H40100000000000UL)
        Dim posFull = (both = &H40100000000000UL) OrElse both = 0UL
        r.Add(New Element With {.Semantic = Sem.Position0, .Slot = If(((desc >> 44) And 1UL) <> 0UL, 0, 1), .Offset = 0,
                                .Components = 4, .GlType = If(posFull, fl, hf)})
        Dim s As Integer
        If Present(desc, 1, s) Then r.Add(New Element With {.Semantic = Sem.Texcoord0, .Slot = s, .Offset = CInt((desc >> 6) And &H3CUL), .Components = 2, .GlType = hf})
        If Present(desc, 2, s) Then r.Add(New Element With {.Semantic = Sem.Texcoord1, .Slot = s, .Offset = CInt((desc >> 10) And &H3CUL), .Components = 4, .GlType = hf})
        If Present(desc, 3, s) Then r.Add(New Element With {.Semantic = Sem.Normal0, .Slot = s, .Offset = CInt((desc >> 14) And &H3CUL), .Components = 4, .GlType = ub, .Normalized = True})
        If Present(desc, 4, s) Then r.Add(New Element With {.Semantic = Sem.Binormal0, .Slot = s, .Offset = CInt((desc >> 18) And &H3CUL), .Components = 4, .GlType = ub, .Normalized = True})
        If Present(desc, 5, s) Then r.Add(New Element With {.Semantic = Sem.Color0, .Slot = s, .Offset = CInt((desc >> 22) And &H3CUL), .Components = 4, .GlType = ub, .Normalized = True})
        If Present(desc, 6, s) Then
            Dim o = CInt((desc >> 26) And &H3CUL)
            r.Add(New Element With {.Semantic = Sem.BlendWeight0, .Slot = s, .Offset = o, .Components = 4, .GlType = hf})
            r.Add(New Element With {.Semantic = Sem.BlendIndices0, .Slot = s, .Offset = o + 8, .Components = 4, .GlType = ub, .Normalized = True})
        End If
        If Present(desc, 7, s) Then r.Add(New Element With {.Semantic = Sem.Texcoord3, .Slot = s, .Offset = CInt((desc >> 30) And &H3CUL), .Components = 4, .GlType = ub, .Normalized = True})
        If Present(desc, 8, s) Then r.Add(New Element With {.Semantic = Sem.Texcoord2, .Slot = s, .Offset = CInt((desc >> 34) And &H3CUL), .Components = 1, .GlType = fl})
        ' INSTANCE (bits 53/63): per-instance, never in the preview (geometry <> 0xF, notas 1.1): not listed
        Return r
    End Function

    ''' <summary>Vertex size in bytes = (desc AND 0xF) * 4 (the NIF's vertex data size nibble; the layout's stride).</summary>
    Friend Function Stride(desc As ULong) As Integer
        Return CInt(desc And &HFUL) * 4
    End Function
End Module
