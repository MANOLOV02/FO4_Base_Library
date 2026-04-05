' Version Uploaded of Fo4Library 3.2.0
Option Strict On
Option Explicit On

' =============================================================================
' ESTADO: DEBUG / EN REVISIÓN — NO CERRADO
' -----------------------------------------------------------------------------
' Parseo de operadores de render/skin del HKX de tela.
' NO conectado al render actual. Built but unused.
'
' PENDIENTES CONOCIDOS:
'  - ParseObjectSpaceSkinPNOperator: offsets (+0x10 name, +0x18 header, +0x20
'    BoneTransforms, +0x30 BoneIndices, +0x48 TransformSubset, etc.) determinados
'    empíricamente. NO verificados contra Havok SDK. Gap de 8 bytes en +0x40 y
'    entre +0x78 y +0x88 sin identificar.
'  - ParseSimpleMeshBoneDeformOperator: boneIndex = packedBone \ 64 (bits 6-15)
'    es reverse-engineered. Internamente consistente pero no confirmado.
'    TriangleIndex = packedValue \ 6: misma situación.
'  - ParseWeightedTransformSubset: layout SIMD 16-lane asumido. Tamaños 224/176/128
'    para 4/3/2 blend son consistentes con la fórmula pero no verificados.
'  - ReadMatrix4: stride 64 bytes (4×4 floats). Correcto para hkMatrix4.
'  - DecodeQuantizedVector3: scale=256.0 para posiciones, 32767.0 para normales.
'    Origen de estos valores: no documentado. A verificar.
' =============================================================================

Imports System.Collections.Generic
Imports System.Linq

Public NotInheritable Class HclRenderGraphParser_Class
    Public Shared Function ParseTransformSetDefinition(graph As HkxObjectGraph_Class, source As HkxVirtualObjectGraph_Class) As HclTransformSetDefinitionGraph_Class
        If IsNothing(graph) OrElse IsNothing(source) Then Return Nothing
        If Not source.ClassName.Equals("hclTransformSetDefinition", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        Return New HclTransformSetDefinitionGraph_Class With {
            .SourceObject = source,
            .Name = graph.ResolveLocalString(source.RelativeOffset + &H10),
            .TransformCount = graph.ReadInt32(source.RelativeOffset + &H18),
            .FloatSlotCount = graph.ReadInt32(source.RelativeOffset + &H1C)
        }
    End Function

    Public Shared Function ParseObjectSpaceSkinPNOperator(graph As HkxObjectGraph_Class, source As HkxVirtualObjectGraph_Class) As HclObjectSpaceSkinPNOperatorGraph_Class
        If IsNothing(graph) OrElse IsNothing(source) Then Return Nothing
        If Not source.ClassName.Equals("hclObjectSpaceSkinPNOperator", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        Dim result As New HclObjectSpaceSkinPNOperatorGraph_Class With {
            .SourceObject = source,
            .Name = graph.ResolveLocalString(source.RelativeOffset + &H10),
            .HeaderUInt32 = ReadUInt32Block(graph, source.RelativeOffset + &H18, 2),
            .BoneTransformsField = graph.ReadArrayHeader(source.RelativeOffset + &H20),
            .BoneIndicesField = graph.ReadArrayHeader(source.RelativeOffset + &H30),
            .TransformSubsetField = graph.ReadArrayHeader(source.RelativeOffset + &H48),
            .UnknownStructArrayField = graph.ReadArrayHeader(source.RelativeOffset + &H58),
            .UnknownSingleStructField = graph.ReadArrayHeader(source.RelativeOffset + &H68),
            .UnknownBytesField = graph.ReadArrayHeader(source.RelativeOffset + &H88),
            .UnknownLargeStructField = graph.ReadArrayHeader(source.RelativeOffset + &HA0)
        }

        result.BoneIndices = ReadUInt16Array(graph, result.BoneIndicesField)
        result.BoneTransforms = ReadMatrix4Array(graph, result.BoneTransformsField)
        result.TransformSubsets = ReadWeightedTransformSubsetArray(graph, result.TransformSubsetField, 224, 4)
        result.UnknownStructs = ReadRawStructArray(graph, result.UnknownStructArrayField, 176)
        result.UnknownSingleStructs = ReadRawStructArray(graph, result.UnknownSingleStructField, 128)
        result.UnknownBytes = ReadByteArray(graph, result.UnknownBytesField)
        result.UnknownLargeStructs = ReadRawStructArray(graph, result.UnknownLargeStructField, 256)

        result.ThreeBlendSubsets = ReadWeightedTransformSubsetArray(graph, result.UnknownStructArrayField, 176, 3)
        result.TwoBlendSubsets = ReadWeightedTransformSubsetArray(graph, result.UnknownSingleStructField, 128, 2)
        result.SkinBlockTypeBytes = If(result.UnknownBytes, Array.Empty(Of Byte)())
        result.LocalBlocks = ReadLocalBlockPNArray(graph, result.UnknownLargeStructField)
        result.SkinBlocks.AddRange(BuildSkinBlocks(result))
        Return result
    End Function

    Public Shared Function ParseSimpleMeshBoneDeformOperator(graph As HkxObjectGraph_Class,
                                                             source As HkxVirtualObjectGraph_Class,
                                                             Optional skeleton As HkaSkeletonGraph_Class = Nothing) As HclSimpleMeshBoneDeformOperatorGraph_Class
        If IsNothing(graph) OrElse IsNothing(source) Then Return Nothing
        If Not source.ClassName.Equals("hclSimpleMeshBoneDeformOperator", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        Dim result As New HclSimpleMeshBoneDeformOperatorGraph_Class With {
            .SourceObject = source,
            .Name = graph.ResolveLocalString(source.RelativeOffset + &H10),
            .HeaderUInt32 = ReadUInt32Block(graph, source.RelativeOffset + &H18, 4),
            .MappingField = graph.ReadArrayHeader(source.RelativeOffset + &H28),
            .BindMatrixField = graph.ReadArrayHeader(source.RelativeOffset + &H38)
        }

        result.BindMatrices = ReadMatrix4Array(graph, result.BindMatrixField)

        If result.MappingField.Count > 0 AndAlso result.MappingField.DataRelativeOffset >= 0 Then
            For i = 0 To result.MappingField.Count - 1
                Dim entryOffset = result.MappingField.DataRelativeOffset + (i * 4)
                Dim packedBone = ReadUInt16(graph, entryOffset)
                Dim packedValue = ReadUInt16(graph, entryOffset + 2)
                Dim boneIndex = packedBone \ 64
                Dim boneName = String.Empty

                If Not IsNothing(skeleton) AndAlso Not IsNothing(skeleton.Bones) AndAlso boneIndex >= 0 AndAlso boneIndex < skeleton.Bones.Count Then
                    boneName = skeleton.Bones(boneIndex).Name
                End If

                result.BoneMappings.Add(New HclSimpleMeshBoneDeformMapping_Class With {
                    .EntryIndex = i,
                    .EntryRelativeOffset = entryOffset,
                    .PackedBoneValue = packedBone,
                    .PackedBoneFlags = packedBone And &H3F,
                    .BoneIndex = boneIndex,
                    .TriangleIndex = packedValue \ 6,
                    .PackedValueFlags = packedValue Mod 6,
                    .BoneName = boneName,
                    .PackedValue = packedValue,
                    .BindMatrix = If(i < result.BindMatrices.Count, result.BindMatrices(i), Nothing)
                })
            Next
        End If

        Return result
    End Function

    Private Shared Function ReadUInt32Block(graph As HkxObjectGraph_Class, relativeOffset As Integer, count As Integer) As List(Of UInteger)
        Dim result As New List(Of UInteger)
        If count <= 0 Then Return result
        Dim startAbs = graph.ContentsSection.AbsoluteDataStart + relativeOffset
        If startAbs < 0 OrElse startAbs + (count * 4) > graph.Packfile.RawBytes.Length Then Return result

        For i = 0 To count - 1
            result.Add(BitConverter.ToUInt32(graph.Packfile.RawBytes, startAbs + (i * 4)))
        Next

        Return result
    End Function

    Private Shared Function ReadUInt16Array(graph As HkxObjectGraph_Class, field As HkxObjectArrayHeader_Class) As List(Of UShort)
        Dim result As New List(Of UShort)
        If IsNothing(field) OrElse field.Count <= 0 OrElse field.DataRelativeOffset < 0 Then Return result

        For i = 0 To field.Count - 1
            result.Add(ReadUInt16(graph, field.DataRelativeOffset + (i * 2)))
        Next

        Return result
    End Function

    Private Shared Function ReadMatrix4Array(graph As HkxObjectGraph_Class, field As HkxObjectArrayHeader_Class) As List(Of HkxMatrix4Graph_Class)
        Dim result As New List(Of HkxMatrix4Graph_Class)
        If IsNothing(field) OrElse field.Count <= 0 OrElse field.DataRelativeOffset < 0 Then Return result

        For i = 0 To field.Count - 1
            Dim matrixOffset = field.DataRelativeOffset + (i * 64)
            result.Add(ReadMatrix4(graph, matrixOffset))
        Next

        Return result
    End Function

    Private Shared Function ReadWeightedTransformSubsetArray(graph As HkxObjectGraph_Class,
                                                             field As HkxObjectArrayHeader_Class,
                                                             structSize As Integer,
                                                             influenceCount As Integer) As List(Of HclObjectSpaceSkinTransformSubsetGraph_Class)
        Dim result As New List(Of HclObjectSpaceSkinTransformSubsetGraph_Class)
        For Each raw In ReadRawStructArray(graph, field, structSize)
            Dim subset = ParseWeightedTransformSubset(raw, influenceCount)
            If Not IsNothing(subset) Then result.Add(subset)
        Next
        Return result
    End Function

    Private Shared Function ParseWeightedTransformSubset(raw As HkxRawStructGraph_Class, influenceCount As Integer) As HclObjectSpaceSkinTransformSubsetGraph_Class
        If influenceCount <= 0 Then Return Nothing
        If IsNothing(raw?.RawBytes) Then Return Nothing

        Dim expectedLength = 32 + (influenceCount * 32) + (16 * influenceCount)
        If raw.RawBytes.Length < expectedLength Then Return Nothing

        Dim result As New HclObjectSpaceSkinTransformSubsetGraph_Class With {
            .EntryIndex = raw.EntryIndex,
            .EntryRelativeOffset = raw.EntryRelativeOffset,
            .RawStruct = raw,
            .RawBytes = raw.RawBytes,
            .InfluenceCount = influenceCount
        }

        For influence = 0 To influenceCount - 1
            result.InfluenceIndexGroups.Add(New List(Of UShort))
        Next

        Dim weightsOffset = 32 + (influenceCount * 32)

        For lane = 0 To 15
            Dim vertexIndex = BitConverter.ToUInt16(raw.RawBytes, lane * 2)
            result.VertexIndices.Add(vertexIndex)

            Dim laneInfo As New HclObjectSpaceSkinVertexInfluenceGraph_Class With {
                .LaneIndex = lane,
                .VertexIndex = vertexIndex,
                .InfluenceCount = influenceCount
            }

            For influence = 0 To influenceCount - 1
                Dim influenceOffset = 32 + (influence * 32) + (lane * 2)
                Dim transformIndex = BitConverter.ToUInt16(raw.RawBytes, influenceOffset)
                result.InfluenceIndexGroups(influence).Add(transformIndex)
                laneInfo.TransformIndices.Add(transformIndex)
            Next

            For influence = 0 To influenceCount - 1
                laneInfo.WeightBytes.Add(raw.RawBytes(weightsOffset + (lane * influenceCount) + influence))
            Next

            laneInfo.WeightByteSum = laneInfo.WeightBytes.Sum(Function(value) CInt(value))
            result.VertexInfluences.Add(laneInfo)
        Next

        Return result
    End Function

    Private Shared Function ReadLocalBlockPNArray(graph As HkxObjectGraph_Class, field As HkxObjectArrayHeader_Class) As List(Of HclObjectSpaceSkinLocalBlockPNGraph_Class)
        Dim result As New List(Of HclObjectSpaceSkinLocalBlockPNGraph_Class)
        For Each raw In ReadRawStructArray(graph, field, 256)
            Dim block = ParseLocalBlockPN(raw)
            If Not IsNothing(block) Then result.Add(block)
        Next
        Return result
    End Function

    Private Shared Function ParseLocalBlockPN(raw As HkxRawStructGraph_Class) As HclObjectSpaceSkinLocalBlockPNGraph_Class
        If IsNothing(raw?.RawBytes) OrElse raw.RawBytes.Length < 256 Then Return Nothing

        Dim result As New HclObjectSpaceSkinLocalBlockPNGraph_Class With {
            .EntryIndex = raw.EntryIndex,
            .EntryRelativeOffset = raw.EntryRelativeOffset,
            .RawStruct = raw,
            .RawBytes = raw.RawBytes
        }

        For lane = 0 To 15
            Dim laneBytes(15) As Byte
            Array.Copy(raw.RawBytes, lane * 16, laneBytes, 0, 16)

            Dim laneInfo As New HclObjectSpaceSkinLocalBlockLaneGraph_Class With {
                .LaneIndex = lane,
                .RawBytes = laneBytes
            }

            For i = 0 To 7
                laneInfo.UInt16Values.Add(BitConverter.ToUInt16(laneBytes, i * 2))
                laneInfo.Int16Values.Add(BitConverter.ToInt16(laneBytes, i * 2))
            Next

            For i = 0 To 3
                laneInfo.UInt32Values.Add(BitConverter.ToUInt32(laneBytes, i * 4))
                laneInfo.VectorAUInt16Values.Add(laneInfo.UInt16Values(i))
                laneInfo.VectorAInt16Values.Add(laneInfo.Int16Values(i))
                laneInfo.VectorBUInt16Values.Add(laneInfo.UInt16Values(4 + i))
                laneInfo.VectorBInt16Values.Add(laneInfo.Int16Values(4 + i))
            Next

            result.Lanes.Add(laneInfo)
        Next

        For lane = 0 To Math.Min(7, result.Lanes.Count - 1)
            result.DecodedPositions.Add(DecodeQuantizedVector3(result.Lanes(lane).VectorAInt16Values, 256.0R, lane, 0))
            result.DecodedPositions.Add(DecodeQuantizedVector3(result.Lanes(lane).VectorBInt16Values, 256.0R, lane, 1))
        Next

        For lane = 8 To Math.Min(15, result.Lanes.Count - 1)
            result.DecodedNormals.Add(DecodeQuantizedVector3(result.Lanes(lane).VectorAInt16Values, 32767.0R, lane, 0))
            result.DecodedNormals.Add(DecodeQuantizedVector3(result.Lanes(lane).VectorBInt16Values, 32767.0R, lane, 1))
        Next

        Return result
    End Function

    Private Shared Function DecodeQuantizedVector3(values As IReadOnlyList(Of Short), scale As Double, laneIndex As Integer, pairIndex As Integer) As HclObjectSpaceSkinQuantizedVectorGraph_Class
        Dim result As New HclObjectSpaceSkinQuantizedVectorGraph_Class With {
            .LaneIndex = laneIndex,
            .PairIndex = pairIndex,
            .Scale = scale
        }

        If values Is Nothing OrElse values.Count < 3 Then Return result

        For Each value In values
            result.RawInt16Values.Add(value)
        Next

        result.X = values(0) / scale
        result.Y = values(1) / scale
        result.Z = values(2) / scale
        If values.Count > 3 Then result.W = values(3) / scale
        result.Length = Math.Sqrt((result.X * result.X) + (result.Y * result.Y) + (result.Z * result.Z))
        Return result
    End Function

    Private Shared Function BuildSkinBlocks(source As HclObjectSpaceSkinPNOperatorGraph_Class) As List(Of HclObjectSpaceSkinBlockGraph_Class)
        Dim result As New List(Of HclObjectSpaceSkinBlockGraph_Class)
        If IsNothing(source) Then Return result

        Dim fourBlendIndex = 0
        Dim threeBlendIndex = 0
        Dim twoBlendIndex = 0
        Dim blockTypeBytes = If(source.SkinBlockTypeBytes, Array.Empty(Of Byte)())
        Dim blockCount = Math.Max(blockTypeBytes.Length, source.LocalBlocks.Count)

        For blockIndex = 0 To blockCount - 1
            Dim blockType = If(blockIndex < blockTypeBytes.Length, CInt(blockTypeBytes(blockIndex)), -1)
            Dim subset As HclObjectSpaceSkinTransformSubsetGraph_Class = Nothing
            Dim blendCount = 0
            Dim blockTypeName = "unknown"

            Select Case blockType
                Case 0
                    blendCount = 4
                    blockTypeName = "four-blend"
                    If fourBlendIndex < source.TransformSubsets.Count Then
                        subset = source.TransformSubsets(fourBlendIndex)
                        fourBlendIndex += 1
                    End If
                Case 1
                    blendCount = 3
                    blockTypeName = "three-blend"
                    If threeBlendIndex < source.ThreeBlendSubsets.Count Then
                        subset = source.ThreeBlendSubsets(threeBlendIndex)
                        threeBlendIndex += 1
                    End If
                Case 2
                    blendCount = 2
                    blockTypeName = "two-blend"
                    If twoBlendIndex < source.TwoBlendSubsets.Count Then
                        subset = source.TwoBlendSubsets(twoBlendIndex)
                        twoBlendIndex += 1
                    End If
            End Select

            Dim localBlock As HclObjectSpaceSkinLocalBlockPNGraph_Class = Nothing
            If blockIndex < source.LocalBlocks.Count Then
                localBlock = source.LocalBlocks(blockIndex)
            End If

            Dim block As New HclObjectSpaceSkinBlockGraph_Class With {
                .BlockIndex = blockIndex,
                .BlockType = blockType,
                .BlockTypeName = blockTypeName,
                .BlendCount = blendCount,
                .InfluenceBlock = subset,
                .LocalBlock = localBlock
            }

            If Not IsNothing(subset) AndAlso Not IsNothing(localBlock) Then
                For slot = 0 To Math.Min(subset.VertexIndices.Count, 16) - 1
                    Dim entry As New HclObjectSpaceSkinBlockVertexEntryGraph_Class With {
                        .SlotIndex = slot,
                        .VertexIndex = subset.VertexIndices(slot)
                    }
                    If slot < localBlock.DecodedPositions.Count Then entry.Position = localBlock.DecodedPositions(slot)
                    If slot < localBlock.DecodedNormals.Count Then entry.Normal = localBlock.DecodedNormals(slot)
                    block.VertexEntries.Add(entry)
                Next
            End If

            result.Add(block)
        Next

        Return result
    End Function

    Private Shared Function ReadRawStructArray(graph As HkxObjectGraph_Class, field As HkxObjectArrayHeader_Class, structSize As Integer) As List(Of HkxRawStructGraph_Class)
        Dim result As New List(Of HkxRawStructGraph_Class)
        If IsNothing(field) OrElse structSize <= 0 OrElse field.Count <= 0 OrElse field.DataRelativeOffset < 0 Then Return result

        For i = 0 To field.Count - 1
            Dim entryOffset = field.DataRelativeOffset + (i * structSize)
            result.Add(CreateRawStruct(graph, i, entryOffset, structSize))
        Next

        Return result
    End Function

    Private Shared Function CreateRawStruct(graph As HkxObjectGraph_Class, entryIndex As Integer, entryRelativeOffset As Integer, byteCount As Integer) As HkxRawStructGraph_Class
        Dim bytes = ReadByteBlock(graph, entryRelativeOffset, byteCount)
        Dim result As New HkxRawStructGraph_Class With {
            .EntryIndex = entryIndex,
            .EntryRelativeOffset = entryRelativeOffset,
            .RawBytes = bytes
        }

        For i = 0 To (bytes.Length \ 2) - 1
            result.UInt16Values.Add(BitConverter.ToUInt16(bytes, i * 2))
        Next

        For i = 0 To (bytes.Length \ 4) - 1
            result.UInt32Values.Add(BitConverter.ToUInt32(bytes, i * 4))
            result.SingleValues.Add(BitConverter.ToSingle(bytes, i * 4))
        Next

        Return result
    End Function

    Private Shared Function ReadByteArray(graph As HkxObjectGraph_Class, field As HkxObjectArrayHeader_Class) As Byte()
        If IsNothing(field) OrElse field.Count <= 0 OrElse field.DataRelativeOffset < 0 Then Return Array.Empty(Of Byte)()
        Return ReadByteBlock(graph, field.DataRelativeOffset, field.Count)
    End Function

    Private Shared Function ReadByteBlock(graph As HkxObjectGraph_Class, relativeOffset As Integer, byteCount As Integer) As Byte()
        If byteCount <= 0 Then Return Array.Empty(Of Byte)()
        Dim absoluteOffset = graph.ContentsSection.AbsoluteDataStart + relativeOffset
        If absoluteOffset < 0 OrElse absoluteOffset + byteCount > graph.Packfile.RawBytes.Length Then Return Array.Empty(Of Byte)()
        Dim result(byteCount - 1) As Byte
        Array.Copy(graph.Packfile.RawBytes, absoluteOffset, result, 0, byteCount)
        Return result
    End Function

    Private Shared Function ReadMatrix4(graph As HkxObjectGraph_Class, relativeOffset As Integer) As HkxMatrix4Graph_Class
        Dim values(15) As Single
        For i = 0 To 15
            values(i) = ReadSingle(graph, relativeOffset + (i * 4))
        Next

        Return New HkxMatrix4Graph_Class With {
            .RelativeOffset = relativeOffset,
            .Values = values
        }
    End Function

    Private Shared Function ReadUInt16(graph As HkxObjectGraph_Class, relativeOffset As Integer) As UShort
        Dim absoluteOffset = graph.ContentsSection.AbsoluteDataStart + relativeOffset
        If absoluteOffset < 0 OrElse absoluteOffset + 2 > graph.Packfile.RawBytes.Length Then Return 0
        Return BitConverter.ToUInt16(graph.Packfile.RawBytes, absoluteOffset)
    End Function

    Private Shared Function ReadSingle(graph As HkxObjectGraph_Class, relativeOffset As Integer) As Single
        Dim absoluteOffset = graph.ContentsSection.AbsoluteDataStart + relativeOffset
        If absoluteOffset < 0 OrElse absoluteOffset + 4 > graph.Packfile.RawBytes.Length Then Return 0
        Return BitConverter.ToSingle(graph.Packfile.RawBytes, absoluteOffset)
    End Function
End Class

Public Class HclTransformSetDefinitionGraph_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Name As String
    Public Property TransformCount As Integer
    Public Property FloatSlotCount As Integer
End Class

Public Class HclObjectSpaceSkinPNOperatorGraph_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Name As String
    Public Property HeaderUInt32 As List(Of UInteger)
    Public Property BoneTransformsField As HkxObjectArrayHeader_Class
    Public Property BoneIndicesField As HkxObjectArrayHeader_Class
    Public Property TransformSubsetField As HkxObjectArrayHeader_Class
    Public Property UnknownStructArrayField As HkxObjectArrayHeader_Class
    Public Property UnknownSingleStructField As HkxObjectArrayHeader_Class
    Public Property UnknownBytesField As HkxObjectArrayHeader_Class
    Public Property UnknownLargeStructField As HkxObjectArrayHeader_Class
    Public Property BoneIndices As List(Of UShort)
    Public Property BoneTransforms As List(Of HkxMatrix4Graph_Class)
    Public Property TransformSubsets As List(Of HclObjectSpaceSkinTransformSubsetGraph_Class)
    Public Property UnknownStructs As List(Of HkxRawStructGraph_Class)
    Public Property UnknownSingleStructs As List(Of HkxRawStructGraph_Class)
    Public Property UnknownBytes As Byte()
    Public Property UnknownLargeStructs As List(Of HkxRawStructGraph_Class)
    Public Property ThreeBlendSubsets As List(Of HclObjectSpaceSkinTransformSubsetGraph_Class)
    Public Property TwoBlendSubsets As List(Of HclObjectSpaceSkinTransformSubsetGraph_Class)
    Public Property SkinBlockTypeBytes As Byte()
    Public Property LocalBlocks As List(Of HclObjectSpaceSkinLocalBlockPNGraph_Class)
    Public ReadOnly Property SkinBlocks As New List(Of HclObjectSpaceSkinBlockGraph_Class)
    Public ReadOnly Property CoveredVertexIndices As New List(Of Integer)
    Public Property CoveredVertexCount As Integer
    Public Property SimParticleCount As Integer?
    Public Property CoversSimParticles As Boolean?
    Public ReadOnly Property ResolvedBoneNames As New List(Of String)
End Class

Public Class HclObjectSpaceSkinTransformSubsetGraph_Class
    Public Property EntryIndex As Integer
    Public Property EntryRelativeOffset As Integer
    Public Property RawStruct As HkxRawStructGraph_Class
    Public Property RawBytes As Byte()
    Public Property InfluenceCount As Integer
    Public ReadOnly Property VertexIndices As New List(Of UShort)
    Public ReadOnly Property InfluenceIndexGroups As New List(Of List(Of UShort))
    Public ReadOnly Property VertexInfluences As New List(Of HclObjectSpaceSkinVertexInfluenceGraph_Class)
End Class

Public Class HclObjectSpaceSkinVertexInfluenceGraph_Class
    Public Property LaneIndex As Integer
    Public Property VertexIndex As UShort
    Public Property InfluenceCount As Integer
    Public ReadOnly Property TransformIndices As New List(Of UShort)
    Public ReadOnly Property WeightBytes As New List(Of Byte)
    Public Property WeightByteSum As Integer
    Public ReadOnly Property ResolvedSkeletonIndices As New List(Of Integer)
    Public ReadOnly Property ResolvedBoneNames As New List(Of String)
End Class

Public Class HclObjectSpaceSkinLocalBlockPNGraph_Class
    Public Property EntryIndex As Integer
    Public Property EntryRelativeOffset As Integer
    Public Property RawStruct As HkxRawStructGraph_Class
    Public Property RawBytes As Byte()
    Public ReadOnly Property Lanes As New List(Of HclObjectSpaceSkinLocalBlockLaneGraph_Class)
    Public ReadOnly Property DecodedPositions As New List(Of HclObjectSpaceSkinQuantizedVectorGraph_Class)
    Public ReadOnly Property DecodedNormals As New List(Of HclObjectSpaceSkinQuantizedVectorGraph_Class)
End Class

Public Class HclObjectSpaceSkinLocalBlockLaneGraph_Class
    Public Property LaneIndex As Integer
    Public Property RawBytes As Byte()
    Public ReadOnly Property UInt16Values As New List(Of UShort)
    Public ReadOnly Property Int16Values As New List(Of Short)
    Public ReadOnly Property UInt32Values As New List(Of UInteger)
    Public ReadOnly Property VectorAUInt16Values As New List(Of UShort)
    Public ReadOnly Property VectorAInt16Values As New List(Of Short)
    Public ReadOnly Property VectorBUInt16Values As New List(Of UShort)
    Public ReadOnly Property VectorBInt16Values As New List(Of Short)
End Class

Public Class HclObjectSpaceSkinQuantizedVectorGraph_Class
    Public Property LaneIndex As Integer
    Public Property PairIndex As Integer
    Public Property Scale As Double
    Public Property X As Double
    Public Property Y As Double
    Public Property Z As Double
    Public Property W As Double
    Public Property Length As Double
    Public ReadOnly Property RawInt16Values As New List(Of Short)
End Class

Public Class HclObjectSpaceSkinBlockGraph_Class
    Public Property BlockIndex As Integer
    Public Property BlockType As Integer
    Public Property BlockTypeName As String
    Public Property BlendCount As Integer
    Public Property InfluenceBlock As HclObjectSpaceSkinTransformSubsetGraph_Class
    Public Property LocalBlock As HclObjectSpaceSkinLocalBlockPNGraph_Class
    Public ReadOnly Property VertexEntries As New List(Of HclObjectSpaceSkinBlockVertexEntryGraph_Class)
    Public Property MatchedDefaultPosePositions As Integer
    Public Property AllPositionsMatchDefaultPose As Boolean?
End Class

Public Class HclObjectSpaceSkinBlockVertexEntryGraph_Class
    Public Property SlotIndex As Integer
    Public Property VertexIndex As UShort
    Public Property Position As HclObjectSpaceSkinQuantizedVectorGraph_Class
    Public Property Normal As HclObjectSpaceSkinQuantizedVectorGraph_Class
    Public Property ExpectedPositionX As Double?
    Public Property ExpectedPositionY As Double?
    Public Property ExpectedPositionZ As Double?
    Public Property ExpectedPositionW As Double?
    Public Property PositionError As Double?
    Public Property MatchesDefaultPosePosition As Boolean?
End Class

Public Class HclSimpleMeshBoneDeformOperatorGraph_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Name As String
    Public Property HeaderUInt32 As List(Of UInteger)
    Public Property MappingField As HkxObjectArrayHeader_Class
    Public Property BindMatrixField As HkxObjectArrayHeader_Class
    Public Property BoneMappings As New List(Of HclSimpleMeshBoneDeformMapping_Class)
    Public Property BindMatrices As List(Of HkxMatrix4Graph_Class)
End Class

Public Class HclSimpleMeshBoneDeformMapping_Class
    Public Property EntryIndex As Integer
    Public Property EntryRelativeOffset As Integer
    Public Property PackedBoneValue As UShort
    Public Property PackedBoneFlags As Integer
    Public Property BoneIndex As Integer
    Public Property TriangleIndex As Integer
    Public Property PackedValueFlags As Integer
    Public Property BoneName As String
    Public Property PackedValue As UShort
    Public Property BindMatrix As HkxMatrix4Graph_Class
    Public Property ResolvedTriangle As HkxUInt16TriangleGraph_Class
End Class

Public Class HkxMatrix4Graph_Class
    Public Property RelativeOffset As Integer
    Public Property Values As Single()
End Class








