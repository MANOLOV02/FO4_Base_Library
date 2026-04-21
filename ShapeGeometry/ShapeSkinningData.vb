Imports SysHalf = System.Half

''' <summary>
''' Per-vertex skinning data normalized across BSTriShape (inline VertexData[i].BoneIndices/BoneWeights)
''' and NiTriShape (NiSkinPartition vertex map + per-partition vertex bone arrays).
'''
''' The flat-array layout matches what BlendBoneMatrices expects: a fixed number of bone influences
''' per vertex (WeightsPerVertex, almost always 4 in FO4/SSE) packed in vertex-major order.
''' Slot j of vertex i lives at index (i * WeightsPerVertex + j) in both arrays.
'''
''' For unused slots both BoneIndices(slot)=0 and BoneWeights(slot)=0 — same convention as the GPU
''' arrays in SkinningHelper.ExtractSkinnedGeometry (gpuBoneIdx/gpuBoneWgt).
'''
''' BoneRefIndices is the bone palette local-to-shape: BoneIndices stores indices into this palette,
''' which in turn references NiNode block indices via the shape's skin instance Bones list. The render
''' pipeline does not need this field directly (ExtractSkinnedGeometry uses ShapeBones from
''' IRenderableShape) but it is exposed for diagnostics and writeback symmetry.
''' </summary>
Public Structure ShapeSkinningData
    Public BoneIndices() As Byte
    Public BoneWeights() As SysHalf
    Public WeightsPerVertex As Integer
    Public VertexCount As Integer
    Public BoneRefIndices() As Integer

    ''' <summary>
    ''' Returns a fresh Byte() of length WeightsPerVertex with the bone palette indices for vertex
    ''' <paramref name="vertexIdx"/>.  Allocates per call — for hot loops use the flat BoneIndices
    ''' array with offset arithmetic instead.
    ''' </summary>
    Public Function GetBoneIndices(vertexIdx As Integer) As Byte()
        If BoneIndices Is Nothing OrElse WeightsPerVertex <= 0 Then Return Array.Empty(Of Byte)()
        Dim result(WeightsPerVertex - 1) As Byte
        Dim base As Integer = vertexIdx * WeightsPerVertex
        Array.Copy(BoneIndices, base, result, 0, WeightsPerVertex)
        Return result
    End Function

    ''' <summary>
    ''' Returns a fresh System.Half() of length WeightsPerVertex with the bone weights for vertex
    ''' <paramref name="vertexIdx"/>.  Allocates per call — for hot loops use the flat BoneWeights
    ''' array with offset arithmetic instead.
    ''' </summary>
    Public Function GetBoneWeights(vertexIdx As Integer) As SysHalf()
        If BoneWeights Is Nothing OrElse WeightsPerVertex <= 0 Then Return Array.Empty(Of SysHalf)()
        Dim result(WeightsPerVertex - 1) As SysHalf
        Dim base As Integer = vertexIdx * WeightsPerVertex
        Array.Copy(BoneWeights, base, result, 0, WeightsPerVertex)
        Return result
    End Function

    ''' <summary>
    ''' Empty (zero vertex) skinning data for unskinned shapes.  Caller must still treat the shape
    ''' as having an implicit single bone (GlobalTransform) — that responsibility lives in the
    ''' rendering pipeline, not here.
    ''' </summary>
    Public Shared ReadOnly Empty As ShapeSkinningData = New ShapeSkinningData With {
        .BoneIndices = Array.Empty(Of Byte)(),
        .BoneWeights = Array.Empty(Of SysHalf)(),
        .WeightsPerVertex = 0,
        .VertexCount = 0,
        .BoneRefIndices = Array.Empty(Of Integer)()
    }
End Structure
