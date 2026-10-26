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
    ''' <summary>Bit 49, COLOR present in slot 0: the alpha-test prepass ORs it into its layout's desc after the mask, always
    ''' (0x141828E48 movabs + 0x141828E5A or; RE re22 1). The colour offset nibble (bits 24-27) is in the mask, so COLOR0 lands at the
    ''' desc's colour offset ((desc &gt;&gt; 22) AND 0x3C), slot 0.</summary>
    Friend Const PrepassAlphaForcedColor As ULong = &H2000000000000UL   ' 0x141828E48
    ''' <summary>Bits 49 and 59 (COLOR in slot 0 or 1), the test of cb2[0].y on the FULL desc (0x141828F69..F73).</summary>
    Friend Const PrepassColorStreamBits As ULong = &H802000000000000UL  ' 0x141828F69

    ''' <summary>THE DESC OF THE ALPHA-TEST Z-PREPASS'S INPUT LAYOUT (0x141828DE0, entry 0x1A): (desc AND PrepassAlphaMask) OR
    ''' PrepassAlphaForcedColor (0x141828E34..E5A) - the key of the layout cache (compared 0x141829017, stored 0x141829020), built by
    ''' 0x141823210 (0x14182908B). COLOR0 is always in it, as VS 3929 reads it (rec3929 L48) and D3D11 needs every VS input in the
    ''' layout; a geometry without a colour stream reads, there, the bytes at its desc's colour offset, which cb2[0].y = 10000 cancels
    ''' (PrepassColorStream). The one owner of that layout: Render's prepass VAO and RenderableMesh.PrepassStreamHasColor.</summary>
    Friend Function PrepassAlphaDesc(desc As ULong) As ULong
#If DEBUG Then
        ' Ola 7h tester (ShadowGate --ola7h, ParityGate fo4-prepass-colour-layout): the layout without the engine's OR (the 7f layout):
        ' no COLOR0 without a colour stream - the prepass VAO and PrepassStreamHasColor as before the 7h.
        If Fo4GBufferConstants.GateMutantOn("p7h-no-or") Then Return desc And PrepassAlphaMask
#End If
        Return (desc And PrepassAlphaMask) Or PrepassAlphaForcedColor
    End Function

    ''' <summary>The desc of the rigid z-prepass's input layout (0x141828B77, entry 0x19): desc AND PrepassRigidMask, no OR - VS 3928
    ''' reads no COLOR (RE re22 1).</summary>
    Friend Function PrepassRigidDesc(desc As ULong) As ULong
        Return desc And PrepassRigidMask
    End Function

    ''' <summary>Does the alpha-test prepass multiply the vertex alpha? cb2[0].y = 0 when the geometry's FULL desc has COLOR in slot 0
    ''' or 1 (bit 49 or 59: (desc AND 0x0802000000000000) &lt;&gt; 0, 0x141828F69..F8D), else [0x142475504] = 10000, so saturate(COLOR0.w +
    ''' 10000) = 1 cuts nothing. The one owner of Fo4ShapeInputs.HasColorStream.</summary>
    Friend Function PrepassColorStream(desc As ULong) As Boolean
        Return (desc And PrepassColorStreamBits) <> 0UL
    End Function

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
