Imports OpenTK.Graphics.OpenGL4

''' <summary>THE ENGINE'S INPUT LAYOUT of a vertex desc, ONE law with a row per game (ola SSE S1 F2.3, regla 5): per semantic the slot,
''' the format and the byte offset ((desc &gt;&gt; s) AND &amp;H3C) of the D3D11_INPUT_ELEMENT_DESC array the engine builds.
''' <list type="bullet">
''' <item>Fallout 4: Fallout4.exe 1.11.240 0x141823210 (ola 2 propuesta-v3 3.3).</item>
''' <item>Skyrim SE: SkyrimSE.exe 1.7.104 0x141011750 (re_vertices.md 1.2). Attribute k: slot 0 if bit 44+k, else slot 1 if bit 54+k,
''' else absent - the scheme of Fallout 4. Its two rows of its own: POSITION is always R32G32B32A32_FLOAT (SkyrimSE.exe 0x14101176D), and
''' bit 51 gives TEXCOORD2 and TEXCOORD3 (land data, SkyrimSE.exe 0x141011A51..AB3), where Fallout 4 gives TEXCOORD3 alone.</item>
''' </list>
''' The GL location of each semantic is the app's fixed numbering, Fo4EngineVertexLayout.Sem (declared seat: the generated GLSL of both
''' games names it). The Fallout 4 z-prepass layouts (PrepassRigidDesc / PrepassAlphaDesc) are Fallout 4 law and stay in
''' Fo4EngineVertexLayout.</summary>
Friend Module EngineVertexLayout

    Friend Structure Element
        Public Semantic As Fo4EngineVertexLayout.Sem, Slot As Integer, Offset As Integer, Components As Integer
        Public GlType As VertexAttribPointerType, Normalized As Boolean, PerInstance As Boolean
    End Structure

    Private Function Present(desc As ULong, k As Integer, ByRef slot As Integer) As Boolean
        ' Fallout4.exe 0x141823294 (each attribute: the slot-0 test first); the same order in SkyrimSE.exe 0x1410117C1..7F1
        If ((desc >> (44 + k)) And 1UL) <> 0UL Then slot = 0 : Return True
        If ((desc >> (54 + k)) And 1UL) <> 0UL Then slot = 1 : Return True
        Return False
    End Function

    ''' <summary>The elements of <paramref name="desc"/> in the engine's order, for <paramref name="game"/>.</summary>
    Friend Function Elements(desc As ULong, game As Config_App.Game_Enum) As List(Of Element)
        Dim r As New List(Of Element)
        Dim fl = VertexAttribPointerType.Float, hf = VertexAttribPointerType.HalfFloat, ub = VertexAttribPointerType.UnsignedByte
        If game = Config_App.Game_Enum.Skyrim Then
            ' POSITION0, element 0 always: R32G32B32A32_FLOAT (SkyrimSE.exe 0x14101176D, format 2); slot 0 with bit 44, else 1 with bit 54,
            ' else 0xFFFFFFFF (SkyrimSE.exe 0x141011789..7B6) - an invalid input slot, kept as -1.
            Dim ps = If(((desc >> 44) And 1UL) <> 0UL, 0, If(((desc >> 54) And 1UL) <> 0UL, 1, -1))
            r.Add(New Element With {.Semantic = Fo4EngineVertexLayout.Sem.Position0, .Slot = ps, .Offset = 0, .Components = 4, .GlType = fl})
        Else
            ' POSITION: R32G32B32A32 when bits 44 and 54 are both set or both clear, else R16G16B16A16 (Fallout4.exe 0x141823225..25F);
            ' slot = NOT bit 44 (Fallout4.exe 0x141823272..286)
            Dim both = (desc And &H40100000000000UL)
            Dim posFull = (both = &H40100000000000UL) OrElse both = 0UL
            r.Add(New Element With {.Semantic = Fo4EngineVertexLayout.Sem.Position0, .Slot = If(((desc >> 44) And 1UL) <> 0UL, 0, 1), .Offset = 0,
                                    .Components = 4, .GlType = If(posFull, fl, hf)})
        End If
        Dim s As Integer
        ' k = 1..6: the same in both games (Skyrim SE: re_vertices.md 1.2, SkyrimSE.exe 0x1410117F3..A26)
        If Present(desc, 1, s) Then r.Add(New Element With {.Semantic = Fo4EngineVertexLayout.Sem.Texcoord0, .Slot = s, .Offset = CInt((desc >> 6) And &H3CUL), .Components = 2, .GlType = hf})
        If Present(desc, 2, s) Then r.Add(New Element With {.Semantic = Fo4EngineVertexLayout.Sem.Texcoord1, .Slot = s, .Offset = CInt((desc >> 10) And &H3CUL), .Components = 4, .GlType = hf})
        If Present(desc, 3, s) Then r.Add(New Element With {.Semantic = Fo4EngineVertexLayout.Sem.Normal0, .Slot = s, .Offset = CInt((desc >> 14) And &H3CUL), .Components = 4, .GlType = ub, .Normalized = True})
        If Present(desc, 4, s) Then r.Add(New Element With {.Semantic = Fo4EngineVertexLayout.Sem.Binormal0, .Slot = s, .Offset = CInt((desc >> 18) And &H3CUL), .Components = 4, .GlType = ub, .Normalized = True})
        If Present(desc, 5, s) Then r.Add(New Element With {.Semantic = Fo4EngineVertexLayout.Sem.Color0, .Slot = s, .Offset = CInt((desc >> 22) And &H3CUL), .Components = 4, .GlType = ub, .Normalized = True})
        If Present(desc, 6, s) Then
            Dim o = CInt((desc >> 26) And &H3CUL)
            r.Add(New Element With {.Semantic = Fo4EngineVertexLayout.Sem.BlendWeight0, .Slot = s, .Offset = o, .Components = 4, .GlType = hf})
            r.Add(New Element With {.Semantic = Fo4EngineVertexLayout.Sem.BlendIndices0, .Slot = s, .Offset = o + 8, .Components = 4, .GlType = ub, .Normalized = True})
        End If
        If Present(desc, 7, s) Then
            Dim o = CInt((desc >> 30) And &H3CUL)
            If game = Config_App.Game_Enum.Skyrim Then
                ' land data: TEXCOORD2 at o and TEXCOORD3 at o + 4, both R8G8B8A8_UNORM (SkyrimSE.exe 0x141011A51..AB3)
                r.Add(New Element With {.Semantic = Fo4EngineVertexLayout.Sem.Texcoord2, .Slot = s, .Offset = o, .Components = 4, .GlType = ub, .Normalized = True})
                r.Add(New Element With {.Semantic = Fo4EngineVertexLayout.Sem.Texcoord3, .Slot = s, .Offset = o + 4, .Components = 4, .GlType = ub, .Normalized = True})
            Else
                r.Add(New Element With {.Semantic = Fo4EngineVertexLayout.Sem.Texcoord3, .Slot = s, .Offset = o, .Components = 4, .GlType = ub, .Normalized = True})
            End If
        End If
        ' eye data: TEXCOORD2 R32_FLOAT (Skyrim SE: SkyrimSE.exe 0x141011ADD..B0E)
        If Present(desc, 8, s) Then r.Add(New Element With {.Semantic = Fo4EngineVertexLayout.Sem.Texcoord2, .Slot = s, .Offset = CInt((desc >> 34) And &H3CUL), .Components = 1, .GlType = fl})
        ' INSTANCE (bits 53 / 63): per-instance, never in the preview (geometry type 0xF, notas 1.1): not listed
        Return r
    End Function

    ''' <summary>Vertex size in bytes of slot 0 = (desc AND &amp;HF) * 4 (the NIF's vertex data size nibble; the layout's stride; Skyrim SE:
    ''' SkyrimSE.exe 0x14100BD70..7E).</summary>
    Friend Function Stride(desc As ULong) As Integer
        Return CInt(desc And &HFUL) * 4
    End Function

    ''' <summary>Vertex size in bytes of slot 1 (the BSDynamicTriShape stream: POSITION float4) = ((desc &gt;&gt; 4) AND &amp;HF) * 4 (Skyrim SE:
    ''' SkyrimSE.exe 0x14100C7FB..806; the v83 packer writes that nibble from its slot-1 accumulator, SkyrimSE.exe 0x141015FFC..601F).</summary>
    Friend Function Slot1Stride(desc As ULong) As Integer
        Return CInt((desc >> 4) And &HFUL) * 4
    End Function
End Module
