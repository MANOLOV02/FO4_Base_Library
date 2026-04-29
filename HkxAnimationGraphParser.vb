Option Strict On
Option Explicit On

Imports System.Collections.Generic
Imports System.IO
Imports System.Linq

Public Partial Class HkxObjectGraph_Class
    Private Enum HkxSplineTrackValueType_Enum
        Identity = 0
        StaticValue = 1
        SplineValue = 2
    End Enum

    Private Structure HkxSplineTrackMask_Struct
        Public PosQuant As Byte
        Public RotQuant As Byte
        Public ScaleQuant As Byte
        Public PosFlags As Byte
        Public RotFlags As Byte
        Public ScaleFlags As Byte

        Public Function GetPositionType(axis As Integer) As HkxSplineTrackValueType_Enum
            If ((CInt(PosFlags) >> (axis + 4)) And 1) <> 0 Then Return HkxSplineTrackValueType_Enum.SplineValue
            If ((CInt(PosFlags) >> axis) And 1) <> 0 Then Return HkxSplineTrackValueType_Enum.StaticValue
            Return HkxSplineTrackValueType_Enum.Identity
        End Function

        Public Function GetScaleType(axis As Integer) As HkxSplineTrackValueType_Enum
            If ((CInt(ScaleFlags) >> (axis + 4)) And 1) <> 0 Then Return HkxSplineTrackValueType_Enum.SplineValue
            If ((CInt(ScaleFlags) >> axis) And 1) <> 0 Then Return HkxSplineTrackValueType_Enum.StaticValue
            Return HkxSplineTrackValueType_Enum.Identity
        End Function

        Public Function GetRotationType() As HkxSplineTrackValueType_Enum
            If ((CInt(RotFlags) >> 4) And &HF) <> 0 Then Return HkxSplineTrackValueType_Enum.SplineValue
            If (CInt(RotFlags) And &HF) <> 0 Then Return HkxSplineTrackValueType_Enum.StaticValue
            Return HkxSplineTrackValueType_Enum.Identity
        End Function

        Public Function HasAnyPositionSpline() As Boolean
            Return GetPositionType(0) = HkxSplineTrackValueType_Enum.SplineValue OrElse
                   GetPositionType(1) = HkxSplineTrackValueType_Enum.SplineValue OrElse
                   GetPositionType(2) = HkxSplineTrackValueType_Enum.SplineValue
        End Function

        Public Function HasAnyScaleSpline() As Boolean
            Return GetScaleType(0) = HkxSplineTrackValueType_Enum.SplineValue OrElse
                   GetScaleType(1) = HkxSplineTrackValueType_Enum.SplineValue OrElse
                   GetScaleType(2) = HkxSplineTrackValueType_Enum.SplineValue
        End Function
    End Structure

    Private Structure HkxSplineAxisInfo_Struct
        Public Type As HkxSplineTrackValueType_Enum
        Public MinValue As Single
        Public MaxValue As Single
    End Structure

    Private Structure HkxVector3Frame_Struct
        Public X As Single
        Public Y As Single
        Public Z As Single
    End Structure

    Private Structure HkxQuaternionFrame_Struct
        Public X As Single
        Public Y As Single
        Public Z As Single
        Public W As Single
    End Structure

    Public Function ParseAnimationBindings() As List(Of HkaAnimationBindingGraph_Class)
        Dim result As New List(Of HkaAnimationBindingGraph_Class)

        For Each obj In GetObjectsByClassName("hkaAnimationBinding").OrderBy(Function(item) item.RelativeOffset)
            Dim binding = ParseAnimationBinding(obj)
            If Not IsNothing(binding) Then result.Add(binding)
        Next

        Return result
    End Function

    Public Function ParseAnimations() As List(Of HkaSplineCompressedAnimationGraph_Class)
        Dim animations As New List(Of HkaSplineCompressedAnimationGraph_Class)

        For Each obj In GetObjectsByClassName("hkaSplineCompressedAnimation").OrderBy(Function(item) item.RelativeOffset)
            Dim animation = ParseAnimation(obj)
            If Not IsNothing(animation) Then animations.Add(animation)
        Next

        If animations.Count = 0 Then Return animations

        Dim animationsByOffset As New Dictionary(Of Integer, HkaSplineCompressedAnimationGraph_Class)
        For Each animation In animations
            If animation.SourceObject Is Nothing Then Continue For
            animationsByOffset(animation.SourceObject.RelativeOffset) = animation
        Next

        Dim remainingBindings As New List(Of HkaAnimationBindingGraph_Class)
        For Each binding In ParseAnimationBindings()
            If binding.AnimationObject IsNot Nothing Then
                Dim matchedAnimation As HkaSplineCompressedAnimationGraph_Class = Nothing
                If animationsByOffset.TryGetValue(binding.AnimationObject.RelativeOffset, matchedAnimation) AndAlso matchedAnimation.Binding Is Nothing Then
                    matchedAnimation.Binding = binding
                    Continue For
                End If
            End If

            remainingBindings.Add(binding)
        Next

        Dim bindingCursor = 0
        For Each animation In animations
            If animation.Binding IsNot Nothing Then Continue For
            If bindingCursor >= remainingBindings.Count Then Exit For
            animation.Binding = remainingBindings(bindingCursor)
            bindingCursor += 1
        Next

        Return animations
    End Function

    Public Function ParseAnimationBinding(source As HkxVirtualObjectGraph_Class) As HkaAnimationBindingGraph_Class
        If IsNothing(source) OrElse Not source.ClassName.Equals("hkaAnimationBinding", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        Dim baseFieldOffset = BaseObjectFieldOffset
        Dim bindingNameOffset = baseFieldOffset
        Dim animationReferenceOffset = bindingNameOffset + PointerSizeValue
        Dim trackToBoneArrayOffset = animationReferenceOffset + PointerSizeValue
        Dim blendHintOffset = trackToBoneArrayOffset + (2 * ArrayHeaderSizeValue)

        If source.Size > 0 AndAlso source.Size < blendHintOffset + 4 Then
            Throw New InvalidDataException($"hkaAnimationBinding @0x{source.RelativeOffset:X} is truncated.")
        End If

        Dim result As New HkaAnimationBindingGraph_Class With {
            .SourceObject = source,
            .OriginalSkeletonName = ResolveLocalString(source.RelativeOffset + bindingNameOffset),
            .AnimationObject = ResolveGlobalObject(source.RelativeOffset + animationReferenceOffset),
            .BlendHint = ReadInt32(source.RelativeOffset + blendHintOffset)
        }

        result.TransformTrackToBoneIndices.AddRange(ReadInt16Array(source.RelativeOffset + trackToBoneArrayOffset))
        Return result
    End Function

    Public Function ParseAnimation(source As HkxVirtualObjectGraph_Class) As HkaSplineCompressedAnimationGraph_Class
        If IsNothing(source) OrElse Not source.ClassName.Equals("hkaSplineCompressedAnimation", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        Dim baseFieldOffset = BaseObjectFieldOffset
        Dim animationDurationOffset = baseFieldOffset + 4
        Dim transformTrackCountOffset = baseFieldOffset + 8
        Dim floatTrackCountOffset = baseFieldOffset + 12
        Dim extractedMotionOffset = baseFieldOffset + 16
        Dim annotationTracksOffset = extractedMotionOffset + PointerSizeValue
        Dim splineBaseOffset = annotationTracksOffset + ArrayHeaderSizeValue
        Dim frameCountOffset = splineBaseOffset
        Dim blockCountOffset = splineBaseOffset + 4
        Dim maxFramesPerBlockOffset = splineBaseOffset + 8
        Dim maskAndQuantSizeOffset = splineBaseOffset + 12
        Dim frameDurationOffset = splineBaseOffset + 24
        Dim blockOffsetsArrayOffset = AlignValue(splineBaseOffset + 28, PointerSizeValue)
        Dim floatBlockOffsetsArrayOffset = blockOffsetsArrayOffset + ArrayHeaderSizeValue
        Dim transformOffsetsArrayOffset = floatBlockOffsetsArrayOffset + ArrayHeaderSizeValue
        Dim floatOffsetsArrayOffset = transformOffsetsArrayOffset + ArrayHeaderSizeValue
        Dim dataArrayOffset = floatOffsetsArrayOffset + ArrayHeaderSizeValue

        If source.Size > 0 AndAlso source.Size < dataArrayOffset + ArrayHeaderSizeValue Then
            Throw New InvalidDataException($"hkaSplineCompressedAnimation @0x{source.RelativeOffset:X} is truncated.")
        End If

        Dim result As New HkaSplineCompressedAnimationGraph_Class With {
            .SourceObject = source,
            .Duration = ReadSingle(source.RelativeOffset + animationDurationOffset),
            .FrameDuration = ReadSingle(source.RelativeOffset + frameDurationOffset),
            .NumFrames = ReadInt32(source.RelativeOffset + frameCountOffset),
            .NumTransformTracks = ReadInt32(source.RelativeOffset + transformTrackCountOffset),
            .NumFloatTracks = ReadInt32(source.RelativeOffset + floatTrackCountOffset),
            .NumBlocks = ReadInt32(source.RelativeOffset + blockCountOffset),
            .MaxFramesPerBlock = ReadInt32(source.RelativeOffset + maxFramesPerBlockOffset),
            .MaskAndQuantizationSize = ReadInt32(source.RelativeOffset + maskAndQuantSizeOffset)
        }

        If result.NumFrames < 0 OrElse result.NumTransformTracks < 0 OrElse result.NumFloatTracks < 0 OrElse
           result.NumBlocks < 0 OrElse result.MaxFramesPerBlock < 0 OrElse result.MaskAndQuantizationSize < 0 Then
            Throw New InvalidDataException($"hkaSplineCompressedAnimation @0x{source.RelativeOffset:X} has invalid negative counts.")
        End If

        If result.NumBlocks > 0 AndAlso result.MaxFramesPerBlock <= 0 Then
            Throw New InvalidDataException($"hkaSplineCompressedAnimation @0x{source.RelativeOffset:X} has invalid MaxFramesPerBlock={result.MaxFramesPerBlock}.")
        End If

        result.TrackNames.AddRange(ReadAnnotationTrackNames(source.RelativeOffset + annotationTracksOffset))

        Dim blockOffsets = ReadUInt32Array(source.RelativeOffset + blockOffsetsArrayOffset)
        Dim splineBlob = ReadByteArray(source.RelativeOffset + dataArrayOffset)

        If (result.NumFrames > 0 OrElse result.NumTransformTracks > 0 OrElse result.NumBlocks > 0) AndAlso (splineBlob.Length = 0 OrElse blockOffsets.Count = 0) Then
            Throw New InvalidDataException($"hkaSplineCompressedAnimation @0x{source.RelativeOffset:X} has no spline payload.")
        End If

        result.TrackTransforms.AddRange(DecompressSplineAnimation(splineBlob,
                                                                  result.NumTransformTracks,
                                                                  result.NumFrames,
                                                                  result.NumBlocks,
                                                                  result.MaxFramesPerBlock,
                                                                  blockOffsets,
                                                                  result.MaskAndQuantizationSize))

        Return result
    End Function

    Private Function ReadAnnotationTrackNames(fieldRelativeOffset As Integer) As List(Of String)
        Dim result As New List(Of String)
        Dim header = ReadArrayHeader(fieldRelativeOffset)
        If header.Count <= 0 OrElse header.DataRelativeOffset < 0 Then Return result

        Dim annotationTrackStride = PointerSizeValue + ArrayHeaderSizeValue
        For index = 0 To header.Count - 1
            Dim trackOffset = header.DataRelativeOffset + (index * annotationTrackStride)
            result.Add(ResolveLocalString(trackOffset))
        Next

        Return result
    End Function

    Private Function DecompressSplineAnimation(blob As Byte(),
                                               numTracks As Integer,
                                               numFrames As Integer,
                                               numBlocks As Integer,
                                               maxFramesPerBlock As Integer,
                                               blockOffsets As IReadOnlyList(Of UInteger),
                                               maskAndQuantSize As Integer) As HkxAnimationTransformGraph_Class()
        Dim totalTransformCount = CLng(numTracks) * CLng(numFrames)
        If totalTransformCount > Integer.MaxValue Then
            Throw New InvalidDataException($"Animation transform table is too large: {totalTransformCount} entries.")
        End If

        Dim totalTransforms = CInt(totalTransformCount)
        Dim result As HkxAnimationTransformGraph_Class() =
            If(totalTransforms > 0,
               New HkxAnimationTransformGraph_Class(totalTransforms - 1) {},
               Array.Empty(Of HkxAnimationTransformGraph_Class)())

        For index = 0 To result.Length - 1
            result(index) = CreateIdentityAnimationTransform()
        Next

        If numTracks = 0 OrElse numFrames = 0 OrElse numBlocks = 0 Then Return result
        If blockOffsets.Count < numBlocks Then Throw New InvalidDataException("Animation block offset array is truncated.")

        If maskAndQuantSize = 0 Then
            maskAndQuantSize = AlignValue(4 * numTracks, 4)
        End If

        Dim masks(numTracks - 1) As HkxSplineTrackMask_Struct
        Dim scalarControlPoints(2) As List(Of Single)
        For axis = 0 To 2
            scalarControlPoints(axis) = New List(Of Single)()
        Next

        Dim knots As New List(Of Single)
        Dim quaternionControlPoints As New List(Of HkxQuaternionFrame_Struct)

        For blockIndex = 0 To numBlocks - 1
            Dim blockStart = UInt32ToInt32(blockOffsets(blockIndex), "block offset")
            Dim firstFrame = blockIndex * maxFramesPerBlock
            If firstFrame > numFrames Then Throw New InvalidDataException("Animation block frame range is invalid.")

            Dim framesInBlock = If(blockIndex = numBlocks - 1, numFrames - firstFrame, maxFramesPerBlock)
            If framesInBlock < 0 Then Throw New InvalidDataException("Animation block frame count is invalid.")

            Dim offset = blockStart
            EnsureBlobReadable(blob, offset, 4 * numTracks, "Track mask block")
            For trackIndex = 0 To numTracks - 1
                Dim packedMask = blob(offset)
                masks(trackIndex).PosQuant = CByte(packedMask And &H3)
                masks(trackIndex).RotQuant = CByte((packedMask >> 2) And &HF)
                masks(trackIndex).ScaleQuant = CByte((packedMask >> 6) And &H3)
                masks(trackIndex).PosFlags = blob(offset + 1)
                masks(trackIndex).RotFlags = blob(offset + 2)
                masks(trackIndex).ScaleFlags = blob(offset + 3)
                offset += 4
            Next

            offset = blockStart + maskAndQuantSize

            For trackIndex = 0 To numTracks - 1
                Dim mask = masks(trackIndex)

                Dim positionFrames = CreateVector3FrameArray(framesInBlock, 0.0F, 0.0F, 0.0F)
                If mask.HasAnyPositionSpline() Then
                    EnsureBlobReadable(blob, offset, 3, "Position spline header")
                    Dim numItems = CInt(BitConverter.ToUInt16(blob, offset))
                    Dim degree = CInt(blob(offset + 2))
                    offset += 3

                    Dim knotCount = numItems + degree + 2
                    EnsureBlobReadable(blob, offset, knotCount, "Position knot array")
                    knots.Clear()
                    For knotIndex = 0 To knotCount - 1
                        knots.Add(CSng(blob(offset + knotIndex)))
                    Next
                    offset += knotCount
                    offset = AlignValue(offset, 4)

                    Dim axisInfos(2) As HkxSplineAxisInfo_Struct
                    For axis = 0 To 2
                        Dim axisType = mask.GetPositionType(axis)
                        axisInfos(axis).Type = axisType

                        Select Case axisType
                            Case HkxSplineTrackValueType_Enum.SplineValue
                                EnsureBlobReadable(blob, offset, 8, "Position axis range")
                                axisInfos(axis).MinValue = BitConverter.ToSingle(blob, offset)
                                axisInfos(axis).MaxValue = BitConverter.ToSingle(blob, offset + 4)
                                offset += 8
                            Case HkxSplineTrackValueType_Enum.StaticValue
                                EnsureBlobReadable(blob, offset, 4, "Position axis static value")
                                axisInfos(axis).MinValue = BitConverter.ToSingle(blob, offset)
                                axisInfos(axis).MaxValue = axisInfos(axis).MinValue
                                offset += 4
                            Case Else
                                axisInfos(axis).MinValue = 0.0F
                                axisInfos(axis).MaxValue = 0.0F
                        End Select
                    Next

                    For axis = 0 To 2
                        scalarControlPoints(axis).Clear()
                    Next

                    For itemIndex = 0 To numItems
                        For axis = 0 To 2
                            If axisInfos(axis).Type <> HkxSplineTrackValueType_Enum.SplineValue Then Continue For

                            If mask.PosQuant = 0 Then
                                EnsureBlobReadable(blob, offset, 1, "Position 8-bit control point")
                                scalarControlPoints(axis).Add(Read8BitScalar(blob(offset), axisInfos(axis).MinValue, axisInfos(axis).MaxValue))
                                offset += 1
                            Else
                                EnsureBlobReadable(blob, offset, 2, "Position 16-bit control point")
                                scalarControlPoints(axis).Add(Read16BitScalar(BitConverter.ToUInt16(blob, offset), axisInfos(axis).MinValue, axisInfos(axis).MaxValue))
                                offset += 2
                            End If
                        Next
                    Next

                    offset = AlignValue(offset, 4)

                    For frameInBlock = 0 To framesInBlock - 1
                        Dim time = CSng(frameInBlock)
                        Dim value As New HkxVector3Frame_Struct

                        For axis = 0 To 2
                            Select Case axisInfos(axis).Type
                                Case HkxSplineTrackValueType_Enum.SplineValue
                                    Dim span = FindKnotSpan(degree, time, scalarControlPoints(axis).Count, knots)
                                    SetVectorAxis(value, axis, EvalBSplineScalar(span, degree, time, knots, scalarControlPoints(axis)))
                                Case HkxSplineTrackValueType_Enum.StaticValue
                                    SetVectorAxis(value, axis, axisInfos(axis).MinValue)
                            End Select
                        Next

                        positionFrames(frameInBlock) = value
                    Next
                Else
                    Dim staticPosition As New HkxVector3Frame_Struct
                    For axis = 0 To 2
                        If mask.GetPositionType(axis) <> HkxSplineTrackValueType_Enum.StaticValue Then Continue For
                        EnsureBlobReadable(blob, offset, 4, "Static position value")
                        SetVectorAxis(staticPosition, axis, BitConverter.ToSingle(blob, offset))
                        offset += 4
                    Next

                    For frameInBlock = 0 To framesInBlock - 1
                        positionFrames(frameInBlock) = staticPosition
                    Next
                End If

                offset = AlignValue(offset, 4)

                Dim rotationFrames = CreateQuaternionFrameArray(framesInBlock, 0.0F, 0.0F, 0.0F, 1.0F)
                Dim rotationType = mask.GetRotationType()
                Dim quaternionFormat = CInt(mask.RotQuant)
                Dim quaternionAlignment = GetQuaternionAlignment(quaternionFormat)

                If rotationType = HkxSplineTrackValueType_Enum.SplineValue Then
                    EnsureBlobReadable(blob, offset, 3, "Rotation spline header")
                    Dim numItems = CInt(BitConverter.ToUInt16(blob, offset))
                    Dim degree = CInt(blob(offset + 2))
                    offset += 3

                    Dim knotCount = numItems + degree + 2
                    EnsureBlobReadable(blob, offset, knotCount, "Rotation knot array")
                    knots.Clear()
                    For knotIndex = 0 To knotCount - 1
                        knots.Add(CSng(blob(offset + knotIndex)))
                    Next
                    offset += knotCount
                    offset = AlignValue(offset, quaternionAlignment)

                    quaternionControlPoints.Clear()
                    For itemIndex = 0 To numItems
                        Dim consumed = 0
                        Dim quat = ReadQuaternion(quaternionFormat, blob, offset, blob.Length - offset, consumed)
                        offset += consumed

                        If quaternionControlPoints.Count > 0 AndAlso DotQuaternion(quat, quaternionControlPoints(quaternionControlPoints.Count - 1)) < 0.0F Then
                            NegateQuaternion(quat)
                        End If

                        quaternionControlPoints.Add(quat)
                    Next

                    For frameInBlock = 0 To framesInBlock - 1
                        Dim time = CSng(frameInBlock)
                        Dim span = FindKnotSpan(degree, time, quaternionControlPoints.Count, knots)
                        Dim quat = EvalBSplineQuaternion(span, degree, time, knots, quaternionControlPoints)
                        NormalizeQuaternion(quat)
                        rotationFrames(frameInBlock) = quat
                    Next
                ElseIf rotationType = HkxSplineTrackValueType_Enum.StaticValue Then
                    offset = AlignValue(offset, quaternionAlignment)
                    Dim consumed = 0
                    Dim quat = ReadQuaternion(quaternionFormat, blob, offset, blob.Length - offset, consumed)
                    offset += consumed

                    For frameInBlock = 0 To framesInBlock - 1
                        rotationFrames(frameInBlock) = quat
                    Next
                End If

                offset = AlignValue(offset, 4)

                Dim scaleFrames = CreateVector3FrameArray(framesInBlock, 1.0F, 1.0F, 1.0F)
                If mask.HasAnyScaleSpline() Then
                    EnsureBlobReadable(blob, offset, 3, "Scale spline header")
                    Dim numItems = CInt(BitConverter.ToUInt16(blob, offset))
                    Dim degree = CInt(blob(offset + 2))
                    offset += 3

                    Dim knotCount = numItems + degree + 2
                    EnsureBlobReadable(blob, offset, knotCount, "Scale knot array")
                    knots.Clear()
                    For knotIndex = 0 To knotCount - 1
                        knots.Add(CSng(blob(offset + knotIndex)))
                    Next
                    offset += knotCount
                    offset = AlignValue(offset, 4)

                    Dim axisInfos(2) As HkxSplineAxisInfo_Struct
                    For axis = 0 To 2
                        Dim axisType = mask.GetScaleType(axis)
                        axisInfos(axis).Type = axisType

                        Select Case axisType
                            Case HkxSplineTrackValueType_Enum.SplineValue
                                EnsureBlobReadable(blob, offset, 8, "Scale axis range")
                                axisInfos(axis).MinValue = BitConverter.ToSingle(blob, offset)
                                axisInfos(axis).MaxValue = BitConverter.ToSingle(blob, offset + 4)
                                offset += 8
                            Case HkxSplineTrackValueType_Enum.StaticValue
                                EnsureBlobReadable(blob, offset, 4, "Scale axis static value")
                                axisInfos(axis).MinValue = BitConverter.ToSingle(blob, offset)
                                axisInfos(axis).MaxValue = axisInfos(axis).MinValue
                                offset += 4
                            Case Else
                                axisInfos(axis).MinValue = 1.0F
                                axisInfos(axis).MaxValue = 1.0F
                        End Select
                    Next

                    For axis = 0 To 2
                        scalarControlPoints(axis).Clear()
                    Next

                    For itemIndex = 0 To numItems
                        For axis = 0 To 2
                            If axisInfos(axis).Type <> HkxSplineTrackValueType_Enum.SplineValue Then Continue For

                            If mask.ScaleQuant = 0 Then
                                EnsureBlobReadable(blob, offset, 1, "Scale 8-bit control point")
                                scalarControlPoints(axis).Add(Read8BitScalar(blob(offset), axisInfos(axis).MinValue, axisInfos(axis).MaxValue))
                                offset += 1
                            Else
                                EnsureBlobReadable(blob, offset, 2, "Scale 16-bit control point")
                                scalarControlPoints(axis).Add(Read16BitScalar(BitConverter.ToUInt16(blob, offset), axisInfos(axis).MinValue, axisInfos(axis).MaxValue))
                                offset += 2
                            End If
                        Next
                    Next

                    offset = AlignValue(offset, 4)

                    For frameInBlock = 0 To framesInBlock - 1
                        Dim time = CSng(frameInBlock)
                        Dim value As New HkxVector3Frame_Struct With {.X = 1.0F, .Y = 1.0F, .Z = 1.0F}

                        For axis = 0 To 2
                            Select Case axisInfos(axis).Type
                                Case HkxSplineTrackValueType_Enum.SplineValue
                                    Dim span = FindKnotSpan(degree, time, scalarControlPoints(axis).Count, knots)
                                    SetVectorAxis(value, axis, EvalBSplineScalar(span, degree, time, knots, scalarControlPoints(axis)))
                                Case HkxSplineTrackValueType_Enum.StaticValue
                                    SetVectorAxis(value, axis, axisInfos(axis).MinValue)
                            End Select
                        Next

                        scaleFrames(frameInBlock) = value
                    Next
                Else
                    Dim staticScale As New HkxVector3Frame_Struct With {.X = 1.0F, .Y = 1.0F, .Z = 1.0F}
                    For axis = 0 To 2
                        If mask.GetScaleType(axis) <> HkxSplineTrackValueType_Enum.StaticValue Then Continue For
                        EnsureBlobReadable(blob, offset, 4, "Static scale value")
                        SetVectorAxis(staticScale, axis, BitConverter.ToSingle(blob, offset))
                        offset += 4
                    Next

                    For frameInBlock = 0 To framesInBlock - 1
                        scaleFrames(frameInBlock) = staticScale
                    Next
                End If

                offset = AlignValue(offset, 4)

                For frameInBlock = 0 To framesInBlock - 1
                    Dim destinationIndex = ((firstFrame + frameInBlock) * numTracks) + trackIndex
                    Dim destination = result(destinationIndex)
                    destination.Translation.X = positionFrames(frameInBlock).X
                    destination.Translation.Y = positionFrames(frameInBlock).Y
                    destination.Translation.Z = positionFrames(frameInBlock).Z
                    destination.Rotation.X = rotationFrames(frameInBlock).X
                    destination.Rotation.Y = rotationFrames(frameInBlock).Y
                    destination.Rotation.Z = rotationFrames(frameInBlock).Z
                    destination.Rotation.W = rotationFrames(frameInBlock).W
                    destination.Scale.X = scaleFrames(frameInBlock).X
                    destination.Scale.Y = scaleFrames(frameInBlock).Y
                    destination.Scale.Z = scaleFrames(frameInBlock).Z
                Next
            Next
        Next

        Return result
    End Function

    Private Shared Function CreateIdentityAnimationTransform() As HkxAnimationTransformGraph_Class
        Return New HkxAnimationTransformGraph_Class With {
            .Translation = New HkxVector4Graph_Class With {.X = 0.0F, .Y = 0.0F, .Z = 0.0F, .W = 0.0F},
            .Rotation = New HkxQuaternionGraph_Class With {.X = 0.0F, .Y = 0.0F, .Z = 0.0F, .W = 1.0F},
            .Scale = New HkxVector4Graph_Class With {.X = 1.0F, .Y = 1.0F, .Z = 1.0F, .W = 0.0F}
        }
    End Function

    Private Shared Function CreateVector3FrameArray(count As Integer, x As Single, y As Single, z As Single) As HkxVector3Frame_Struct()
        If count <= 0 Then Return Array.Empty(Of HkxVector3Frame_Struct)()

        Dim result(count - 1) As HkxVector3Frame_Struct
        For index = 0 To result.Length - 1
            result(index).X = x
            result(index).Y = y
            result(index).Z = z
        Next
        Return result
    End Function

    Private Shared Function CreateQuaternionFrameArray(count As Integer, x As Single, y As Single, z As Single, w As Single) As HkxQuaternionFrame_Struct()
        If count <= 0 Then Return Array.Empty(Of HkxQuaternionFrame_Struct)()

        Dim result(count - 1) As HkxQuaternionFrame_Struct
        For index = 0 To result.Length - 1
            result(index).X = x
            result(index).Y = y
            result(index).Z = z
            result(index).W = w
        Next
        Return result
    End Function

    Private Shared Function UInt32ToInt32(value As UInteger, fieldName As String) As Integer
        If value > Integer.MaxValue Then Throw New InvalidDataException($"{fieldName} exceeds Int32 range: 0x{value:X8}.")
        Return CInt(value)
    End Function

    Private Shared Sub EnsureBlobReadable(blob As Byte(), offset As Integer, byteCount As Integer, context As String)
        If offset < 0 OrElse byteCount < 0 OrElse offset > blob.Length OrElse byteCount > blob.Length - offset Then
            Throw New InvalidDataException($"{context} is truncated at blob offset 0x{Math.Max(offset, 0):X}.")
        End If
    End Sub

    Private Shared Function AlignValue(offset As Integer, alignment As Integer) As Integer
        If alignment <= 1 Then Return offset
        Dim remainder = offset Mod alignment
        If remainder = 0 Then Return offset
        Return offset + (alignment - remainder)
    End Function

    Private Shared Function Read8BitScalar(value As Byte, minimum As Single, maximum As Single) As Single
        Return minimum + ((maximum - minimum) * (CSng(value) / 255.0F))
    End Function

    Private Shared Function Read16BitScalar(value As UShort, minimum As Single, maximum As Single) As Single
        Return minimum + ((maximum - minimum) * (CSng(value) / 65535.0F))
    End Function

    Private Shared Function ReadQuaternion(format As Integer, data As Byte(), offset As Integer, available As Integer, ByRef consumed As Integer) As HkxQuaternionFrame_Struct
        Select Case format
            Case 0
                consumed = 4
                Return Read32BitQuaternion(data, offset, available)
            Case 1
                consumed = 5
                Return Read40BitQuaternion(data, offset, available)
            Case 2
                consumed = 6
                Return Read48BitQuaternion(data, offset, available)
            Case 5
                consumed = 16
                Return ReadUncompressedQuaternion(data, offset, available)
            Case Else
                consumed = 5
                Return Read40BitQuaternion(data, offset, available)
        End Select
    End Function

    Private Shared Function Read32BitQuaternion(data As Byte(), offset As Integer, available As Integer) As HkxQuaternionFrame_Struct
        EnsureBlobReadable(data, offset, 4, "32-bit quaternion")

        Dim compressed = BitConverter.ToUInt32(data, offset)
        Dim radiusMask = (1UI << 10) - 1UI
        Dim radius = CSng((compressed >> 18) And radiusMask) / CSng(radiusMask)
        radius = 1.0F - (radius * radius)

        Dim phiTheta = CSng(compressed And &H3FFFFUI)
        Dim phi = MathF.Floor(MathF.Sqrt(phiTheta))
        Dim theta As Single = 0.0F
        If phi > 0.0F Then
            theta = CSng((Math.PI / 4.0) * ((phiTheta - (phi * phi)) / phi))
            phi = CSng((Math.PI / 2.0 / 511.0) * phi)
        End If

        Dim magnitude = MathF.Sqrt(Math.Max(0.0F, 1.0F - (radius * radius)))
        Dim result As New HkxQuaternionFrame_Struct With {
            .X = MathF.Sin(phi) * MathF.Cos(theta) * magnitude,
            .Y = MathF.Sin(phi) * MathF.Sin(theta) * magnitude,
            .Z = MathF.Cos(phi) * magnitude,
            .W = radius
        }

        Dim signMasks = {&H10000000UI, &H20000000UI, &H40000000UI, &H80000000UI}
        If (compressed And signMasks(0)) <> 0UI Then result.X = -result.X
        If (compressed And signMasks(1)) <> 0UI Then result.Y = -result.Y
        If (compressed And signMasks(2)) <> 0UI Then result.Z = -result.Z
        If (compressed And signMasks(3)) <> 0UI Then result.W = -result.W

        NormalizeQuaternion(result)
        Return result
    End Function

    Private Shared Function Read40BitQuaternion(data As Byte(), offset As Integer, available As Integer) As HkxQuaternionFrame_Struct
        EnsureBlobReadable(data, offset, 5, "40-bit quaternion")

        Const Fractal As Single = 0.000345436F
        Dim raw As ULong = 0UL
        For byteIndex = 0 To 4
            raw = raw Or (CULng(data(offset + byteIndex)) << (byteIndex * 8))
        Next

        Dim a = CUInt(raw And &HFFFUL)
        Dim b = CUInt((raw >> 12) And &HFFFUL)
        Dim c = CUInt((raw >> 24) And &HFFFUL)
        Dim x = (CSng(a) - 2049.0F) * Fractal
        Dim y = (CSng(b) - 2049.0F) * Fractal
        Dim z = (CSng(c) - 2049.0F) * Fractal
        Dim w = MathF.Sqrt(Math.Max(0.0F, 1.0F - ((x * x) + (y * y) + (z * z))))
        If ((raw >> 38) And 1UL) <> 0UL Then w = -w

        Dim shift = CInt((raw >> 36) And 3UL)
        Dim result As New HkxQuaternionFrame_Struct
        Select Case shift
            Case 0
                result = New HkxQuaternionFrame_Struct With {.X = w, .Y = x, .Z = y, .W = z}
            Case 1
                result = New HkxQuaternionFrame_Struct With {.X = x, .Y = w, .Z = y, .W = z}
            Case 2
                result = New HkxQuaternionFrame_Struct With {.X = x, .Y = y, .Z = w, .W = z}
            Case Else
                result = New HkxQuaternionFrame_Struct With {.X = x, .Y = y, .Z = z, .W = w}
        End Select

        NormalizeQuaternion(result)
        Return result
    End Function

    Private Shared Function Read48BitQuaternion(data As Byte(), offset As Integer, available As Integer) As HkxQuaternionFrame_Struct
        EnsureBlobReadable(data, offset, 6, "48-bit quaternion")

        Const Fractal As Single = 0.000043161F
        Dim mask = (1UI << 15) - 1UI
        Dim half = mask >> 1
        Dim xRaw = BitConverter.ToUInt16(data, offset)
        Dim yRaw = BitConverter.ToUInt16(data, offset + 2)
        Dim zRaw = BitConverter.ToUInt16(data, offset + 4)

        Dim shift = CInt((((CUInt(yRaw) >> 14) And 2UI) Or ((CUInt(xRaw) >> 15) And 1UI)))
        Dim radiusNegative = (CUInt(zRaw) >> 15) <> 0UI
        Dim x = (CSng(CUInt(xRaw) And mask) - CSng(half)) * Fractal
        Dim y = (CSng(CUInt(yRaw) And mask) - CSng(half)) * Fractal
        Dim z = (CSng(CUInt(zRaw) And mask) - CSng(half)) * Fractal
        Dim w = MathF.Sqrt(Math.Max(0.0F, 1.0F - ((x * x) + (y * y) + (z * z))))
        If radiusNegative Then w = -w

        Dim result As New HkxQuaternionFrame_Struct
        Select Case shift
            Case 0
                result = New HkxQuaternionFrame_Struct With {.X = w, .Y = x, .Z = y, .W = z}
            Case 1
                result = New HkxQuaternionFrame_Struct With {.X = x, .Y = w, .Z = y, .W = z}
            Case 2
                result = New HkxQuaternionFrame_Struct With {.X = x, .Y = y, .Z = w, .W = z}
            Case Else
                result = New HkxQuaternionFrame_Struct With {.X = x, .Y = y, .Z = z, .W = w}
        End Select

        NormalizeQuaternion(result)
        Return result
    End Function

    Private Shared Function ReadUncompressedQuaternion(data As Byte(), offset As Integer, available As Integer) As HkxQuaternionFrame_Struct
        EnsureBlobReadable(data, offset, 16, "Uncompressed quaternion")

        Dim result As New HkxQuaternionFrame_Struct With {
            .X = BitConverter.ToSingle(data, offset),
            .Y = BitConverter.ToSingle(data, offset + 4),
            .Z = BitConverter.ToSingle(data, offset + 8),
            .W = BitConverter.ToSingle(data, offset + 12)
        }

        NormalizeQuaternion(result)
        Return result
    End Function

    Private Shared Sub NormalizeQuaternion(ByRef value As HkxQuaternionFrame_Struct)
        Dim magnitude = MathF.Sqrt((value.X * value.X) + (value.Y * value.Y) + (value.Z * value.Z) + (value.W * value.W))
        If magnitude < 1.0E-10F Then
            value.X = 0.0F
            value.Y = 0.0F
            value.Z = 0.0F
            value.W = 1.0F
            Return
        End If

        Dim inverse = 1.0F / magnitude
        value.X *= inverse
        value.Y *= inverse
        value.Z *= inverse
        value.W *= inverse
    End Sub

    Private Shared Function GetQuaternionAlignment(format As Integer) As Integer
        Select Case format
            Case 0
                Return 4
            Case 1, 3
                Return 1
            Case 2, 4
                Return 2
            Case 5
                Return 4
            Case Else
                Return 1
        End Select
    End Function

    Private Shared Function FindKnotSpan(degree As Integer, value As Single, numControlPoints As Integer, knots As List(Of Single)) As Integer
        If numControlPoints <= 0 Then Return 0
        If value >= knots(numControlPoints) Then Return numControlPoints - 1

        Dim low = degree
        Dim high = numControlPoints
        Dim middle = (low + high) \ 2

        For iteration = 0 To 99
            If value < knots(middle) Then
                high = middle
            ElseIf value >= knots(middle + 1) Then
                low = middle
            Else
                Exit For
            End If

            middle = (low + high) \ 2
        Next

        Return middle
    End Function

    Private Shared Function EvalBSplineScalar(knotSpan As Integer,
                                              degree As Integer,
                                              time As Single,
                                              knots As List(Of Single),
                                              controlPoints As List(Of Single)) As Single
        If controlPoints.Count = 0 Then Return 0.0F
        If controlPoints.Count = 1 Then Return controlPoints(0)

        Dim basis(degree) As Single
        basis(0) = 1.0F

        For degreeIndex = 1 To degree
            For basisIndex = degreeIndex - 1 To 0 Step -1
                Dim denominator = knots(knotSpan + degreeIndex - basisIndex) - knots(knotSpan - basisIndex)
                Dim factor = If(denominator >= 1.0E-10F, (time - knots(knotSpan - basisIndex)) / denominator, 0.0F)
                Dim temp = basis(basisIndex) * factor
                If basisIndex + 1 < basis.Length Then basis(basisIndex + 1) += basis(basisIndex) - temp
                basis(basisIndex) = temp
            Next
        Next

        Dim result As Single = 0.0F
        For degreeIndex = 0 To degree
            Dim controlPointIndex = knotSpan - degreeIndex
            If controlPointIndex >= 0 AndAlso controlPointIndex < controlPoints.Count Then
                result += controlPoints(controlPointIndex) * basis(degreeIndex)
            End If
        Next

        Return result
    End Function

    Private Shared Function EvalBSplineQuaternion(knotSpan As Integer,
                                                  degree As Integer,
                                                  time As Single,
                                                  knots As List(Of Single),
                                                  controlPoints As List(Of HkxQuaternionFrame_Struct)) As HkxQuaternionFrame_Struct
        If controlPoints.Count = 0 Then Return New HkxQuaternionFrame_Struct With {.W = 1.0F}
        If controlPoints.Count = 1 Then Return controlPoints(0)

        Dim basis(degree) As Single
        basis(0) = 1.0F

        For degreeIndex = 1 To degree
            For basisIndex = degreeIndex - 1 To 0 Step -1
                Dim denominator = knots(knotSpan + degreeIndex - basisIndex) - knots(knotSpan - basisIndex)
                Dim factor = If(denominator >= 1.0E-10F, (time - knots(knotSpan - basisIndex)) / denominator, 0.0F)
                Dim temp = basis(basisIndex) * factor
                If basisIndex + 1 < basis.Length Then basis(basisIndex + 1) += basis(basisIndex) - temp
                basis(basisIndex) = temp
            Next
        Next

        Dim result As New HkxQuaternionFrame_Struct
        For degreeIndex = 0 To degree
            Dim controlPointIndex = knotSpan - degreeIndex
            If controlPointIndex < 0 OrElse controlPointIndex >= controlPoints.Count Then Continue For

            result.X += controlPoints(controlPointIndex).X * basis(degreeIndex)
            result.Y += controlPoints(controlPointIndex).Y * basis(degreeIndex)
            result.Z += controlPoints(controlPointIndex).Z * basis(degreeIndex)
            result.W += controlPoints(controlPointIndex).W * basis(degreeIndex)
        Next

        Return result
    End Function

    Private Shared Function DotQuaternion(left As HkxQuaternionFrame_Struct, right As HkxQuaternionFrame_Struct) As Single
        Return (left.X * right.X) + (left.Y * right.Y) + (left.Z * right.Z) + (left.W * right.W)
    End Function

    Private Shared Sub NegateQuaternion(ByRef value As HkxQuaternionFrame_Struct)
        value.X = -value.X
        value.Y = -value.Y
        value.Z = -value.Z
        value.W = -value.W
    End Sub

    Private Shared Sub SetVectorAxis(ByRef value As HkxVector3Frame_Struct, axis As Integer, component As Single)
        Select Case axis
            Case 0
                value.X = component
            Case 1
                value.Y = component
            Case Else
                value.Z = component
        End Select
    End Sub
End Class

Public Class HkaAnimationBindingGraph_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property OriginalSkeletonName As String
    Public Property AnimationObject As HkxVirtualObjectGraph_Class
    Public ReadOnly Property TransformTrackToBoneIndices As New List(Of Short)
    Public Property BlendHint As Integer
End Class

Public Class HkxAnimationTransformGraph_Class
    Public Property Translation As HkxVector4Graph_Class
    Public Property Rotation As HkxQuaternionGraph_Class
    Public Property Scale As HkxVector4Graph_Class
End Class

Public Class HkaSplineCompressedAnimationGraph_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Duration As Single
    Public Property FrameDuration As Single
    Public Property NumFrames As Integer
    Public Property NumTransformTracks As Integer
    Public Property NumFloatTracks As Integer
    Public Property NumBlocks As Integer
    Public Property MaxFramesPerBlock As Integer
    Public Property MaskAndQuantizationSize As Integer
    Public ReadOnly Property TrackNames As New List(Of String)
    Public ReadOnly Property TrackTransforms As New List(Of HkxAnimationTransformGraph_Class)
    Public Property Binding As HkaAnimationBindingGraph_Class

    Public Function GetTransform(frameIndex As Integer, trackIndex As Integer) As HkxAnimationTransformGraph_Class
        If frameIndex < 0 OrElse trackIndex < 0 OrElse NumTransformTracks <= 0 Then Return Nothing
        If frameIndex >= NumFrames OrElse trackIndex >= NumTransformTracks Then Return Nothing

        Dim flatIndex = (frameIndex * NumTransformTracks) + trackIndex
        If flatIndex < 0 OrElse flatIndex >= TrackTransforms.Count Then Return Nothing
        Return TrackTransforms(flatIndex)
    End Function
End Class
