Option Strict On
Option Explicit On

' =============================================================================
' hkaLosslessCompressedAnimation decoder.
'
' Decodifica la 2da variante de compresión de animación de FO4 (la 1ra,
' hkaSplineCompressedAnimation, está en HkxAnimationGraphParser.vb). Produce el
' mismo HkaSplineCompressedAnimationGraph_Class (contenedor de animación decodificada)
' con TrackTransforms = TRS local por (frame, track).
'
' PORTADO de HavokLib (PredatorCZ / Lukas Cone), archivos:
'   source/packfile/custom/hka_losslesscompressedanimation.cpp  (algoritmo GetFrame)
'   source/packfile/custom/hka_animation_lossless_compressed.inl (layout de miembros)
' HavokLib está licenciado GNU GPL v3 (or later); este port es trabajo derivado y
' queda bajo la misma GPL-3.0 que el resto de FO4_Base_Library (ver LICENSE_CREDITS.txt).
' Copyright(C) 2020-2022 Lukas Cone (HavokLib).
'
' Layout (FO4 64-bit, HK2014, ptr=8, reusePadding=0; size 224) verificado
' empíricamente con el modo --dump del HkxLoadOrderAudit sobre archivos reales y
' contra la tabla LAYOUTS del .inl de HavokLib:
'   base hkaAnimation: +0x10 type, +0x14 duration, +0x18 numTransformTracks,
'                      +0x1C numFloatTracks, +0x20 extractedMotion ptr, +0x28 annotationTracks(hkArray)
'   tras la base (l = +0x38), 10 slots hkArray contiguos (ptr+count+pad = 16B c/u), luego numFrames(u32):
'     l+0   dynamicTranslations  (float[])
'     l+16  staticTranslations   (float[])
'     l+32  translationTypeAndOffsets (USVector4[] = 4×uint16)
'     l+48  dynamicRotations     (Vector4A16[] = quaternion 16B)
'     l+64  staticRotations      (Vector4A16[])
'     l+80  rotationTypeAndOffsets (uint16[])
'     l+96  dynamicScales        (float[])
'     l+112 staticScales         (float[])
'     l+128 scaleTypeAndOffsets  (USVector4[])
'     l+144 floats               (float[])
'     l+160 numFrames            (uint32)
'
' Algoritmo por valor (hka_losslesscompressedanimation.cpp): cada componente lleva
' un uint16 type+offset → ttype = type AND 3 (0=Identity, 1=Static, 2=Dynamic),
' index = type >> 2. Static => pool[index]; Dynamic => pool[index + frame*stride],
' donde stride = nº total de componentes dinámicos sumados sobre TODOS los tracks
' (por eso el pool dinámico es [frame0:slots][frame1:slots]...). Translations/scales
' son pools de FLOAT por-componente; rotations son pools de QUATERNION (16B).
' Identity: translation=(0,0,0), scale=(1,1,1), rotation=(0,0,0,1).
' =============================================================================

Imports System.Collections.Generic
Imports System.IO
Imports System.Linq

Public Partial Class HkxObjectGraph_Class

    Private Enum LosslessTrackType_Enum
        Identity = 0
        StaticVal = 1
        Dynamic = 2
    End Enum

    ' Memoizes parsed skeletons by source RelativeOffset so ParseSkeletonMapper does not re-parse
    ' the same full skeleton once per mapper (HKX-007). ParseSkeleton is a pure function of its
    ' source object, so the cached HkaSkeletonGraph_Class yields identical bone names.
    Private ReadOnly _parsedSkeletonCache As New Dictionary(Of Integer, HkaSkeletonGraph_Class)

    Private Function ParseSkeletonMemoized(source As HkxVirtualObjectGraph_Class) As HkaSkeletonGraph_Class
        If IsNothing(source) Then Return Nothing
        Dim cached As HkaSkeletonGraph_Class = Nothing
        If _parsedSkeletonCache.TryGetValue(source.RelativeOffset, cached) Then Return cached
        Dim parsed = ParseSkeleton(source)
        _parsedSkeletonCache(source.RelativeOffset) = parsed
        Return parsed
    End Function

    ' hkArray slot size dentro del objeto (ptr + count(4) + capFlags(4)) = ArrayHeaderSizeValue.
    Private ReadOnly Property LosslessArraysBaseOffset As Integer
        Get
            ' = annotationTracksOffset + ArrayHeaderSizeValue (mismo cálculo de base que el parser spline).
            Dim baseField = BaseObjectFieldOffset
            Dim extractedMotionOffset = baseField + 16
            Dim annotationTracksOffset = extractedMotionOffset + PointerSizeValue
            Return annotationTracksOffset + ArrayHeaderSizeValue
        End Get
    End Property

    ''' <summary>Decodifica todos los hkaLosslessCompressedAnimation del grafo y los empareja con
    ''' su hkaAnimationBinding (misma lógica posicional/por-referencia que ParseAnimations).</summary>
    Public Function ParseLosslessAnimations() As List(Of HkaSplineCompressedAnimationGraph_Class)
        Dim animations As New List(Of HkaSplineCompressedAnimationGraph_Class)

        For Each obj In GetObjectsByClassName("hkaLosslessCompressedAnimation").OrderBy(Function(item) item.RelativeOffset)
            Dim animation = ParseLosslessAnimation(obj)
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
                Dim matched As HkaSplineCompressedAnimationGraph_Class = Nothing
                If animationsByOffset.TryGetValue(binding.AnimationObject.RelativeOffset, matched) AndAlso matched.Binding Is Nothing Then
                    matched.Binding = binding
                    Continue For
                End If
            End If
            remainingBindings.Add(binding)
        Next

        ' Positional fallback for bindings whose AnimationObject ref did not resolve.
        ' Only safe when it is UNAMBIGUOUS: exactly one unmatched animation and exactly
        ' one unmatched binding. Stapling N bindings to N animations by enumeration order
        ' would silently mis-pair tracks→bones on any file where the ref-match was the
        ' real mapping, so leave Binding = Nothing and log instead of guessing.
        Dim unmatchedAnimations = animations.Where(Function(a) a.Binding Is Nothing).ToList()
        If remainingBindings.Count = 1 AndAlso unmatchedAnimations.Count = 1 Then
            unmatchedAnimations(0).Binding = remainingBindings(0)
        ElseIf remainingBindings.Count > 0 Then
            Dim bindingCount = remainingBindings.Count
            Dim animCount = unmatchedAnimations.Count
            Logger.LogLazy(Function() $"[HKX-LOSSLESS] {bindingCount} binding(s) did not resolve their AnimationObject ref and {animCount} animation(s) are unbound; positional fallback skipped (ambiguous). Affected animations left with Binding=Nothing.")
        End If

        Return animations
    End Function

    ''' <summary>Decodifica un hkaLosslessCompressedAnimation a TRS por (frame, track).</summary>
    Public Function ParseLosslessAnimation(source As HkxVirtualObjectGraph_Class) As HkaSplineCompressedAnimationGraph_Class
        If IsNothing(source) OrElse Not source.ClassName.Equals("hkaLosslessCompressedAnimation", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        Dim rel = source.RelativeOffset
        Dim baseField = BaseObjectFieldOffset
        Dim duration = ReadSingle(rel + baseField + 4)
        Dim numTransformTracks = ReadInt32(rel + baseField + 8)
        Dim numFloatTracks = ReadInt32(rel + baseField + 12)

        Dim l = LosslessArraysBaseOffset
        Dim ahs = ArrayHeaderSizeValue
        Dim dynTransH = ReadArrayHeader(rel + l + 0 * ahs)
        Dim staTransH = ReadArrayHeader(rel + l + 1 * ahs)
        Dim transTypeH = ReadArrayHeader(rel + l + 2 * ahs)
        Dim dynRotH = ReadArrayHeader(rel + l + 3 * ahs)
        Dim staRotH = ReadArrayHeader(rel + l + 4 * ahs)
        Dim rotTypeH = ReadArrayHeader(rel + l + 5 * ahs)
        Dim dynScaleH = ReadArrayHeader(rel + l + 6 * ahs)
        Dim staScaleH = ReadArrayHeader(rel + l + 7 * ahs)
        Dim scaleTypeH = ReadArrayHeader(rel + l + 8 * ahs)
        Dim numFrames = ReadInt32(rel + l + 10 * ahs)

        If numFrames < 0 OrElse numTransformTracks < 0 Then
            Throw New InvalidDataException($"hkaLosslessCompressedAnimation @0x{rel:X} tiene counts negativos (numFrames={numFrames}, tracks={numTransformTracks}).")
        End If
        If CLng(numFrames) * CLng(numTransformTracks) > Integer.MaxValue Then
            Throw New InvalidDataException($"hkaLosslessCompressedAnimation @0x{rel:X} tabla de transforms demasiado grande.")
        End If

        Dim result As New HkaSplineCompressedAnimationGraph_Class With {
            .SourceObject = source,
            .Duration = duration,
            .FrameDuration = If(numFrames > 0, duration / numFrames, 1.0F / 30.0F),
            .NumFrames = numFrames,
            .NumTransformTracks = numTransformTracks,
            .NumFloatTracks = numFloatTracks,
            .SourceCompression = "lossless"
        }

        ' Annotation track names (campo de la base hkaAnimation, mismo layout que spline). Son los
        ' nombres de hueso por track; el pose-import los usa para mapear track→hueso del NIF vivo.
        Dim annotationTracksOffset = baseField + 16 + PointerSizeValue
        result.TrackNames.AddRange(ReadAnnotationTrackNames(rel + annotationTracksOffset))

        ' Leer las tablas type+offset (una entrada por track).
        Dim transType = ReadUSVector4Table(transTypeH, numTransformTracks)
        Dim scaleType = ReadUSVector4Table(scaleTypeH, numTransformTracks)
        Dim rotType = ReadUInt16Table(rotTypeH, numTransformTracks)

        ' Strides dinámicos por-frame = nº de componentes Dynamic sumados sobre todos los tracks.
        Dim transStride = SumDynamicComponents(transType)
        Dim scaleStride = SumDynamicComponents(scaleType)
        Dim rotStride = 0
        For Each rt In rotType
            If (rt And 3) = LosslessTrackType_Enum.Dynamic Then rotStride += 1
        Next

        ' Decodificar todos los frames × tracks.
        Dim totalTransforms = numFrames * numTransformTracks
        For i = 0 To totalTransforms - 1
            result.TrackTransforms.Add(Nothing)
        Next

        For frame = 0 To numFrames - 1
            For track = 0 To numTransformTracks - 1
                Dim tT = If(track < transType.Count, transType(track), New Integer() {0, 0, 0, 0})
                Dim sT = If(track < scaleType.Count, scaleType(track), New Integer() {0, 0, 0, 0})
                Dim rT = If(track < rotType.Count, rotType(track), 0)

                Dim tx = ResolveScalar(tT(0), staTransH, dynTransH, 0.0F, transStride, frame)
                Dim ty = ResolveScalar(tT(1), staTransH, dynTransH, 0.0F, transStride, frame)
                Dim tz = ResolveScalar(tT(2), staTransH, dynTransH, 0.0F, transStride, frame)

                Dim sx = ResolveScalar(sT(0), staScaleH, dynScaleH, 1.0F, scaleStride, frame)
                Dim sy = ResolveScalar(sT(1), staScaleH, dynScaleH, 1.0F, scaleStride, frame)
                Dim sz = ResolveScalar(sT(2), staScaleH, dynScaleH, 1.0F, scaleStride, frame)

                Dim q = ResolveQuaternion(rT, staRotH, dynRotH, rotStride, frame)

                result.TrackTransforms((frame * numTransformTracks) + track) = New HkxAnimationTransformGraph_Class With {
                    .Translation = New HkxVector4Graph_Class With {.X = tx, .Y = ty, .Z = tz, .W = 0.0F},
                    .Rotation = q,
                    .Scale = New HkxVector4Graph_Class With {.X = sx, .Y = sy, .Z = sz, .W = 0.0F},
                    .TranslationXAnimated = (tT(0) And 3) <> LosslessTrackType_Enum.Identity,
                    .TranslationYAnimated = (tT(1) And 3) <> LosslessTrackType_Enum.Identity,
                    .TranslationZAnimated = (tT(2) And 3) <> LosslessTrackType_Enum.Identity,
                    .RotationAnimated = (rT And 3) <> LosslessTrackType_Enum.Identity,
                    .ScaleXAnimated = (sT(0) And 3) <> LosslessTrackType_Enum.Identity,
                    .ScaleYAnimated = (sT(1) And 3) <> LosslessTrackType_Enum.Identity,
                    .ScaleZAnimated = (sT(2) And 3) <> LosslessTrackType_Enum.Identity
                }
            Next
        Next

        Return result
    End Function

    Private Function ReadUSVector4Table(header As HkxObjectArrayHeader_Class, count As Integer) As List(Of Integer())
        Dim result As New List(Of Integer())
        If IsNothing(header) OrElse header.DataRelativeOffset < 0 Then Return result
        Dim n = Math.Min(count, header.Count)
        For i = 0 To n - 1
            Dim off = header.DataRelativeOffset + (i * 8)   ' USVector4 = 4×uint16
            result.Add(New Integer() {
                CInt(ReadInt16(off + 0)) And &HFFFF,
                CInt(ReadInt16(off + 2)) And &HFFFF,
                CInt(ReadInt16(off + 4)) And &HFFFF,
                CInt(ReadInt16(off + 6)) And &HFFFF
            })
        Next
        Return result
    End Function

    Private Function ReadUInt16Table(header As HkxObjectArrayHeader_Class, count As Integer) As List(Of Integer)
        Dim result As New List(Of Integer)
        If IsNothing(header) OrElse header.DataRelativeOffset < 0 Then Return result
        Dim n = Math.Min(count, header.Count)
        For i = 0 To n - 1
            result.Add(CInt(ReadInt16(header.DataRelativeOffset + (i * 2))) And &HFFFF)
        Next
        Return result
    End Function

    Private Shared Function SumDynamicComponents(typeTable As List(Of Integer())) As Integer
        Dim total = 0
        For Each entry In typeTable
            For c = 0 To 3
                If (entry(c) And 3) = LosslessTrackType_Enum.Dynamic Then total += 1
            Next
        Next
        Return total
    End Function

    Private Function ResolveScalar(indexType As Integer, staticH As HkxObjectArrayHeader_Class,
                                   dynamicH As HkxObjectArrayHeader_Class, identity As Single,
                                   stride As Integer, frame As Integer) As Single
        Dim ttype = indexType And 3
        Dim index = indexType >> 2
        Select Case ttype
            Case LosslessTrackType_Enum.StaticVal
                If IsNothing(staticH) OrElse staticH.DataRelativeOffset < 0 Then Return identity
                Return ReadSingle(staticH.DataRelativeOffset + (index * 4))
            Case LosslessTrackType_Enum.Dynamic
                If IsNothing(dynamicH) OrElse dynamicH.DataRelativeOffset < 0 Then Return identity
                Return ReadSingle(dynamicH.DataRelativeOffset + ((index + (frame * stride)) * 4))
            Case Else
                Return identity
        End Select
    End Function

    Private Function ResolveQuaternion(indexType As Integer, staticH As HkxObjectArrayHeader_Class,
                                       dynamicH As HkxObjectArrayHeader_Class, stride As Integer,
                                       frame As Integer) As HkxQuaternionGraph_Class
        Dim ttype = indexType And 3
        Dim index = indexType >> 2
        Dim off As Integer = -1
        Select Case ttype
            Case LosslessTrackType_Enum.StaticVal
                If Not IsNothing(staticH) AndAlso staticH.DataRelativeOffset >= 0 Then off = staticH.DataRelativeOffset + (index * 16)
            Case LosslessTrackType_Enum.Dynamic
                If Not IsNothing(dynamicH) AndAlso dynamicH.DataRelativeOffset >= 0 Then off = dynamicH.DataRelativeOffset + ((index + (frame * stride)) * 16)
        End Select

        If off < 0 Then
            Return New HkxQuaternionGraph_Class With {.X = 0.0F, .Y = 0.0F, .Z = 0.0F, .W = 1.0F}
        End If
        Return New HkxQuaternionGraph_Class With {
            .X = ReadSingle(off + 0),
            .Y = ReadSingle(off + 4),
            .Z = ReadSingle(off + 8),
            .W = ReadSingle(off + 12)
        }
    End Function

    ''' <summary>hkaDefaultAnimatedReferenceFrame → ROOT MOTION (hkArray&lt;hkVector4&gt; por-frame
    ''' {X,Y,Z = desplazamiento del root, W = ángulo alrededor de up}; es lo que hace que walk/run
    ''' DESPLACEN al actor). El layout difiere entre Skyrim (hk2010/2011) y FO4 (hk2014) — canonical
    ''' HavokLib (hka_animated_reference_frame_default.inl LAYOUTS, ptr=8):
    '''   Skyrim HK500..HK2011_3: up=0x10, forward=0x20, duration=0x30, samples=0x38.
    '''   FO4    HK2012_1..HK2019: up=0x20, forward=0x30, duration=0x40, samples=0x48 (--dump confirmó).
    ''' Gateado por formato, igual que blendHint del binding y partitions del skeleton.</summary>
    Public Function ParseAnimatedReferenceFrame(source As HkxVirtualObjectGraph_Class) As HkaAnimatedReferenceFrameGraph_Class
        If IsNothing(source) OrElse Not source.ClassName.Equals("hkaDefaultAnimatedReferenceFrame", StringComparison.OrdinalIgnoreCase) Then Return Nothing
        Dim rel = source.RelativeOffset
        Dim isFo4 = (Packfile.Header.PackfileFormat = HkxPackfileFormat_Enum.Fallout64)
        Dim upOff = If(isFo4, &H20, &H10)
        Dim fwdOff = If(isFo4, &H30, &H20)
        Dim durOff = If(isFo4, &H40, &H30)
        Dim sampOff = If(isFo4, &H48, &H38)
        Dim result As New HkaAnimatedReferenceFrameGraph_Class With {
            .SourceObject = source,
            .Up = New HkxVector4Graph_Class With {.X = ReadSingle(rel + upOff), .Y = ReadSingle(rel + upOff + 4), .Z = ReadSingle(rel + upOff + 8), .W = ReadSingle(rel + upOff + 12)},
            .Forward = New HkxVector4Graph_Class With {.X = ReadSingle(rel + fwdOff), .Y = ReadSingle(rel + fwdOff + 4), .Z = ReadSingle(rel + fwdOff + 8), .W = ReadSingle(rel + fwdOff + 12)},
            .Duration = ReadSingle(rel + durOff)
        }
        result.Samples.AddRange(ReadVector4ArrayFromOffset(rel + sampOff))
        Return result
    End Function

    ''' <summary>hkaAnimationContainer → enumera sus arrays de refs: skeletons@+0x10, animations@+0x20,
    ''' bindings@+0x30, attachments@+0x40, skins@+0x50.</summary>
    Public Function ParseAnimationContainer(source As HkxVirtualObjectGraph_Class) As HkaAnimationContainerGraph_Class
        If IsNothing(source) OrElse Not source.ClassName.Equals("hkaAnimationContainer", StringComparison.OrdinalIgnoreCase) Then Return Nothing
        Dim rel = source.RelativeOffset
        Dim result As New HkaAnimationContainerGraph_Class With {.SourceObject = source}
        result.Skeletons.AddRange(ReadObjectReferenceArray(rel + &H10))
        result.Animations.AddRange(ReadObjectReferenceArray(rel + &H20))
        result.Bindings.AddRange(ReadObjectReferenceArray(rel + &H30))
        result.Attachments.AddRange(ReadObjectReferenceArray(rel + &H40))
        result.Skins.AddRange(ReadObjectReferenceArray(rel + &H50))
        Return result
    End Function

    ''' <summary>hkaSkeletonMapper (embebe hkaSkeletonMapperData inline) → el puente de huesos
    ''' entre dos esqueletos (típicamente ragdoll ↔ animación). Layout hk2014 verificado contra
    ''' HavokLib (classgen/hka_skeleton_mapper.py, patches HK700 + HK2012_1) y --dump sobre archivos
    ''' reales: skeletonA@+0x10, skeletonB@+0x18, partitionMap@+0x20, simple/chainMappingPartitionRanges
    ''' @+0x30/+0x40, simpleMappings@+0x50 (SimpleMapping=64B), chainMappings@+0x60 (ChainMapping=112B),
    ''' unmappedBones@+0x70 (int16[]), extractedMotionMapping(hkQsTransform)@+0x80, keepUnmappedLocal@+0xB0,
    ''' mappingType@+0xB4. SimpleMapping = {i16 boneA, i16 boneB, pad, hkQsTransform aFromB@+0x10}.
    ''' ChainMapping = {i16 startA, i16 endA, i16 startB, i16 endB, pad, hkQsTransform startAFromB@+0x10,
    ''' hkQsTransform endAFromB@+0x40}.</summary>
    Public Function ParseSkeletonMapper(source As HkxVirtualObjectGraph_Class) As HkaSkeletonMapperGraph_Class
        If IsNothing(source) OrElse Not source.ClassName.Equals("hkaSkeletonMapper", StringComparison.OrdinalIgnoreCase) Then Return Nothing
        Dim rel = source.RelativeOffset
        Dim result As New HkaSkeletonMapperGraph_Class With {
            .SourceObject = source,
            .SkeletonA = ResolveGlobalObject(rel + &H10),
            .SkeletonB = ResolveGlobalObject(rel + &H18),
            .KeepUnmappedLocal = ReadByte(rel + &HB0) <> 0,
            .MappingType = ReadInt32(rel + &HB4),
            .ExtractedMotionMapping = ReadQsTransformAt(rel + &H80)
        }

        ' Nombres de hueso (índice → nombre) para legibilidad; depende de poder parsear los hkaSkeleton.
        Dim bonesA = If(IsNothing(result.SkeletonA), Nothing, ParseSkeletonMemoized(result.SkeletonA)?.Bones)
        Dim bonesB = If(IsNothing(result.SkeletonB), Nothing, ParseSkeletonMemoized(result.SkeletonB)?.Bones)
        result.SkeletonAName = If(IsNothing(result.SkeletonA), "", ResolveLocalString(result.SkeletonA.RelativeOffset + BaseObjectFieldOffset))
        result.SkeletonBName = If(IsNothing(result.SkeletonB), "", ResolveLocalString(result.SkeletonB.RelativeOffset + BaseObjectFieldOffset))

        Dim simpleHeader = ReadArrayHeader(rel + &H50)
        If simpleHeader.Count > 0 AndAlso simpleHeader.DataRelativeOffset >= 0 Then
            For i = 0 To simpleHeader.Count - 1
                Dim off = simpleHeader.DataRelativeOffset + (i * 64)
                Dim m As New HkaSimpleBoneMapping_Class With {
                    .BoneA = ReadInt16(off + 0),
                    .BoneB = ReadInt16(off + 2),
                    .AFromBTransform = ReadQsTransformAt(off + &H10)
                }
                m.BoneAName = BoneName(bonesA, m.BoneA)
                m.BoneBName = BoneName(bonesB, m.BoneB)
                result.SimpleMappings.Add(m)
            Next
        End If

        Dim chainHeader = ReadArrayHeader(rel + &H60)
        If chainHeader.Count > 0 AndAlso chainHeader.DataRelativeOffset >= 0 Then
            For i = 0 To chainHeader.Count - 1
                Dim off = chainHeader.DataRelativeOffset + (i * 112)
                result.ChainMappings.Add(New HkaChainBoneMapping_Class With {
                    .StartBoneA = ReadInt16(off + 0),
                    .EndBoneA = ReadInt16(off + 2),
                    .StartBoneB = ReadInt16(off + 4),
                    .EndBoneB = ReadInt16(off + 6),
                    .StartAFromBTransform = ReadQsTransformAt(off + &H10),
                    .EndAFromBTransform = ReadQsTransformAt(off + &H40)
                })
            Next
        End If

        result.UnmappedBones.AddRange(ReadInt16Array(rel + &H70))
        Return result
    End Function

    ''' <summary>Todos los hkaSkeletonMapper del grafo.</summary>
    Public Function ParseSkeletonMappers() As List(Of HkaSkeletonMapperGraph_Class)
        Dim result As New List(Of HkaSkeletonMapperGraph_Class)
        For Each obj In GetObjectsByClassName("hkaSkeletonMapper")
            Dim mapper = ParseSkeletonMapper(obj)
            If Not IsNothing(mapper) Then result.Add(mapper)
        Next
        Return result
    End Function

    Private Shared Function BoneName(bones As List(Of HkaBoneGraph_Class), index As Short) As String
        If IsNothing(bones) OrElse index < 0 OrElse index >= bones.Count Then Return ""
        Return bones(index).Name
    End Function

    ''' <summary>Lee un hkQsTransform (48B: translation vec4 @+0, rotation quat @+16, scale vec4 @+32)
    ''' en un offset relativo arbitrario (no necesariamente cabecera de array).</summary>
    Private Function ReadQsTransformAt(relativeOffset As Integer) As HkxQsTransformGraph_Class
        Return New HkxQsTransformGraph_Class With {
            .EntryRelativeOffset = relativeOffset,
            .Translation = New HkxVector4Graph_Class With {
                .X = ReadSingle(relativeOffset + 0), .Y = ReadSingle(relativeOffset + 4),
                .Z = ReadSingle(relativeOffset + 8), .W = ReadSingle(relativeOffset + 12)},
            .Rotation = New HkxQuaternionGraph_Class With {
                .X = ReadSingle(relativeOffset + 16), .Y = ReadSingle(relativeOffset + 20),
                .Z = ReadSingle(relativeOffset + 24), .W = ReadSingle(relativeOffset + 28)},
            .Scale = New HkxVector4Graph_Class With {
                .X = ReadSingle(relativeOffset + 32), .Y = ReadSingle(relativeOffset + 36),
                .Z = ReadSingle(relativeOffset + 40), .W = ReadSingle(relativeOffset + 44)}
        }
    End Function

End Class

Public Class HkaAnimationContainerGraph_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public ReadOnly Property Skeletons As New List(Of HkxVirtualObjectGraph_Class)
    Public ReadOnly Property Animations As New List(Of HkxVirtualObjectGraph_Class)
    Public ReadOnly Property Bindings As New List(Of HkxVirtualObjectGraph_Class)
    Public ReadOnly Property Attachments As New List(Of HkxVirtualObjectGraph_Class)
    Public ReadOnly Property Skins As New List(Of HkxVirtualObjectGraph_Class)
End Class

Public Class HkaAnimatedReferenceFrameGraph_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Up As HkxVector4Graph_Class
    Public Property Forward As HkxVector4Graph_Class
    Public Property Duration As Single
    ' Por-frame: X,Y,Z = posición/desplazamiento del root; W = ángulo (rotación alrededor de Up).
    Public ReadOnly Property Samples As New List(Of HkxVector4Graph_Class)
End Class

' hkaSkeletonMapper: correspondencia de huesos entre dos esqueletos (ragdoll ↔ animación / retargeting).
Public Class HkaSkeletonMapperGraph_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property SkeletonA As HkxVirtualObjectGraph_Class
    Public Property SkeletonB As HkxVirtualObjectGraph_Class
    Public Property SkeletonAName As String = ""
    Public Property SkeletonBName As String = ""
    Public ReadOnly Property SimpleMappings As New List(Of HkaSimpleBoneMapping_Class)
    Public ReadOnly Property ChainMappings As New List(Of HkaChainBoneMapping_Class)
    Public ReadOnly Property UnmappedBones As New List(Of Short)
    Public Property KeepUnmappedLocal As Boolean
    ' Havok MappingType: 0 = HK_RAGDOLL_MAPPING, 1 = HK_RETARGETING_MAPPING.
    Public Property MappingType As Integer
    Public Property ExtractedMotionMapping As HkxQsTransformGraph_Class
End Class

' Una entrada simpleMappings: boneA (en SkeletonA) ↔ boneB (en SkeletonB) + transform A-desde-B.
Public Class HkaSimpleBoneMapping_Class
    Public Property BoneA As Short
    Public Property BoneB As Short
    Public Property BoneAName As String = ""
    Public Property BoneBName As String = ""
    Public Property AFromBTransform As HkxQsTransformGraph_Class
End Class

' Una entrada chainMappings: cadena de huesos start..end en cada esqueleto + transforms en los extremos.
Public Class HkaChainBoneMapping_Class
    Public Property StartBoneA As Short
    Public Property EndBoneA As Short
    Public Property StartBoneB As Short
    Public Property EndBoneB As Short
    Public Property StartAFromBTransform As HkxQsTransformGraph_Class
    Public Property EndAFromBTransform As HkxQsTransformGraph_Class
End Class
