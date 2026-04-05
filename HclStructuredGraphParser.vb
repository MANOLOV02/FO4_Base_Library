' Version Uploaded of Wardrobe 3.1.0
Option Strict On
Option Explicit On

' =============================================================================
' ESTADO: DEBUG / EN REVISIÓN — NO CERRADO
' -----------------------------------------------------------------------------
' Parseo de estructuras HCL (Havok Cloth): SimClothData, collidables, capsules,
' operadores (MoveParticles, Simulate, CopyVertices, etc.), cloth states.
' Llamado desde HclClothPackageParser_Class.
'
' PENDIENTES CONOCIDOS:
'  - Todos los offsets de campos determinados empíricamente para FO4 64-bit.
'    No verificados contra Havok SDK, pero todos confirmados con DumpStructuralAnalysis
'    en CasualDress.nif.
'  - hclSimClothData layout verificado (ver HkxObjectGraphParser.vb para offsets):
'      +0x038: Particles (hkVector4 xyz=pos w=invMass)
'      +0x048: FixedParticles (uint16 indices)
'      +0x058: TriangleIndices (uint16 triplets)
'      +0x068: m_unknown68 (54 elems, tipo desconocido — ReadByteArray con stride 1 probablemente incorrecto)
'      +0x088: m_unknown88 (uint32 SIN fixups = bone indices para collidables) ← Field88UInt32
'      +0x098: m_collidableTransforms (5×hkMatrix4=64B embedded) ← Field98Matrices
'      +0x0A8: m_collidables (GLOBAL fixups → hclCollidable) ← offset CORRECTO
'      +0x0B8: m_staticConstraintSets (GLOBAL fixups)
'      +0x0D8: m_simClothPoses (GLOBAL fixups)
'  - BUG: .Name = ResolveLocalString(+0x030) lee el ptr de hkArray (m_collidableTransformIndices)
'    como string — es semánticamente incorrecto. Devuelve vacío cuando count=0 (todos los samples
'    conocidos), pero retornaría garbage para arrays no vacíos. La clase no tiene m_name serializado.
'  - hclCollidable: ShapeObject resuelto vía GLOBAL fixup en +0x88. VERIFICADO.
'    hclCollidable.m_transform: hkMatrix4 column-major en +0x020 (4×hkVector4). VERIFICADO.
'  - Operadores de simulación: campos internos parcialmente mapeados.
'  - Sin soporte para Skyrim SSE (PointerSize=4).
' =============================================================================

Imports System.Collections.Generic
Imports System.Linq

Public NotInheritable Class HclStructuredGraphParser_Class
    Public Shared Function ParseSimClothData(graph As HkxObjectGraph_Class, source As HkxVirtualObjectGraph_Class) As HclSimClothDataDetail_Class
        If IsNothing(graph) OrElse IsNothing(source) Then Return Nothing
        If Not source.ClassName.Equals("hclSimClothData", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        Dim collidableObjects = graph.ReadObjectReferenceArray(source.RelativeOffset + &HA8)
        Dim constraintObjects = graph.ReadObjectReferenceArray(source.RelativeOffset + &HB8)
        Dim defaultPoseObjects = graph.ReadObjectReferenceArray(source.RelativeOffset + &HD8)

        Dim result As New HclSimClothDataDetail_Class With {
            .SourceObject = source,
            .Name = String.Empty,  ' hclSimClothData no tiene m_name serializado; +0x030 es hkArray ptr (m_collidableTransformIndices), no string
            .Field38Vectors = ReadVector4Array(graph, graph.ReadArrayHeader(source.RelativeOffset + &H38)),
            .Field48UInt16 = ReadUInt16Array(graph, graph.ReadArrayHeader(source.RelativeOffset + &H48)),
            .Field58UInt16 = ReadUInt16Array(graph, graph.ReadArrayHeader(source.RelativeOffset + &H58)),
            .Field68Bytes = ReadByteArray(graph, graph.ReadArrayHeader(source.RelativeOffset + &H68)),
            .Field88UInt32 = ReadUInt32Array(graph, graph.ReadArrayHeader(source.RelativeOffset + &H88)),
            .Field98Matrices = ReadMatrix4Array(graph, graph.ReadArrayHeader(source.RelativeOffset + &H98)),
            .Collidables = collidableObjects,
            .ConstraintSets = constraintObjects,
            .DefaultClothPoses = defaultPoseObjects,
            .FieldF8UInt32 = ReadUInt32Array(graph, graph.ReadArrayHeader(source.RelativeOffset + &HF8)),
            .Field108Bytes = ReadByteArray(graph, graph.ReadArrayHeader(source.RelativeOffset + &H108)),
            .Field118Pairs = ReadUInt32PairArray(graph, graph.ReadArrayHeader(source.RelativeOffset + &H118))
        }

        result.ParticleDatas.AddRange(ParseSimParticleData(result.Field38Vectors))
        result.FixedParticleIndices.AddRange(result.Field48UInt16.Select(Function(value) CInt(value)))
        result.Triangles.AddRange(ReadUInt16TriangleArray(result.Field58UInt16))
        result.StaticCollisionMasks.AddRange(result.FieldF8UInt32)
        result.PinchDetectionFlags.AddRange(result.Field108Bytes)
        result.CollidableDetails.AddRange(collidableObjects.Select(Function(obj) ParseCollidable(graph, obj)).Where(Function(detail) Not IsNothing(detail)))
        result.DefaultClothPoseDetails.AddRange(defaultPoseObjects.Select(Function(obj) graph.ParseSimClothPose(obj)).Where(Function(detail) Not IsNothing(detail)))
        result.ConstraintDetails.AddRange(constraintObjects.Select(Function(obj) ParseConstraintObject(graph, obj)).Where(Function(detail) Not IsNothing(detail)))
        Return result
    End Function

    Public Shared Function ParseClothState(graph As HkxObjectGraph_Class, source As HkxVirtualObjectGraph_Class) As HclClothStateDetail_Class
        If IsNothing(graph) OrElse IsNothing(source) Then Return Nothing
        If Not source.ClassName.Equals("hclClothState", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        Dim result As New HclClothStateDetail_Class With {
            .SourceObject = source,
            .Name = graph.ResolveLocalString(source.RelativeOffset + &H10),
            .Field18UInt32 = ReadUInt32Array(graph, graph.ReadArrayHeader(source.RelativeOffset + &H18)),
            .Field28Vectors = ReadVector4Array(graph, graph.ReadArrayHeader(source.RelativeOffset + &H28)),
            .Field38Structs = ReadRawStructArray(graph, graph.ReadArrayHeader(source.RelativeOffset + &H38), 32),
            .Field48Vectors = ReadVector4Array(graph, graph.ReadArrayHeader(source.RelativeOffset + &H48)),
            .FieldB0Structs = ReadRawStructArray(graph, graph.ReadArrayHeader(source.RelativeOffset + &HB0), 72),
            .FieldC0Bytes = ReadByteArray(graph, graph.ReadArrayHeader(source.RelativeOffset + &HC0)),
            .FieldD8Bytes = ReadByteArray(graph, graph.ReadArrayHeader(source.RelativeOffset + &HD8)),
            .FieldF0Bytes = ReadByteArray(graph, graph.ReadArrayHeader(source.RelativeOffset + &HF0)),
            .Field108Bytes = ReadByteArray(graph, graph.ReadArrayHeader(source.RelativeOffset + &H108)),
            .Field120Bytes = ReadByteArray(graph, graph.ReadArrayHeader(source.RelativeOffset + &H120)),
            .Field138Bytes = ReadByteArray(graph, graph.ReadArrayHeader(source.RelativeOffset + &H138))
        }

        result.OperatorIndices.AddRange(result.Field18UInt32.Select(Function(value) CInt(value)))
        result.BufferAccesses.AddRange(ParseStateBufferAccessArray(graph, graph.ReadArrayHeader(source.RelativeOffset + &H28)))
        result.AuxiliaryBufferAccesses.AddRange(ParseStateBufferAccessArray(graph, graph.ReadArrayHeader(source.RelativeOffset + &H48)))
        result.TransformAccessContainers.AddRange(ParseStateTransformAccessContainerArray(graph, graph.ReadArrayHeader(source.RelativeOffset + &H38)))
        For Each container In result.TransformAccessContainers
            result.TransformSetAccesses.AddRange(container.Accesses)
        Next
        Return result
    End Function

    Private Shared Function ParseStateBufferAccessArray(graph As HkxObjectGraph_Class, field As HkxObjectArrayHeader_Class) As List(Of HclClothStateBufferAccessDetail_Class)
        Dim result As New List(Of HclClothStateBufferAccessDetail_Class)
        For Each raw In ReadRawStructArray(graph, field, 16)
            Dim access = ParseStateBufferAccess(raw)
            If Not IsNothing(access) Then result.Add(access)
        Next
        Return result
    End Function

    Private Shared Function ParseStateBufferAccess(raw As HkxRawStructGraph_Class) As HclClothStateBufferAccessDetail_Class
        If IsNothing(raw) Then Return Nothing

        Dim result As New HclClothStateBufferAccessDetail_Class With {
            .EntryIndex = raw.EntryIndex,
            .EntryRelativeOffset = raw.EntryRelativeOffset,
            .RawStruct = raw,
            .Word0 = If(raw.UInt32Values.Count > 0, raw.UInt32Values(0), 0UI),
            .Word1 = If(raw.UInt32Values.Count > 1, raw.UInt32Values(1), 0UI),
            .Word2 = If(raw.UInt32Values.Count > 2, raw.UInt32Values(2), 0UI),
            .Word3 = If(raw.UInt32Values.Count > 3, raw.UInt32Values(3), 0UI)
        }

        result.BufferIndex = CInt(result.Word0)
        result.AccessCode = CInt(result.Word1)
        result.AccessCodeLowByte = result.AccessCode And &HFF
        result.AccessCodeHighByte = (result.AccessCode >> 8) And &HFF
        Return result
    End Function

    Private Shared Function ParseStateTransformAccessContainerArray(graph As HkxObjectGraph_Class, field As HkxObjectArrayHeader_Class) As List(Of HclClothStateTransformAccessContainerDetail_Class)
        Dim result As New List(Of HclClothStateTransformAccessContainerDetail_Class)
        For Each raw In ReadRawStructArray(graph, field, 32)
            Dim container = ParseStateTransformAccessContainer(graph, raw)
            If Not IsNothing(container) Then result.Add(container)
        Next
        Return result
    End Function

    Private Shared Function ParseStateTransformAccessContainer(graph As HkxObjectGraph_Class, raw As HkxRawStructGraph_Class) As HclClothStateTransformAccessContainerDetail_Class
        If IsNothing(graph) OrElse IsNothing(raw) Then Return Nothing

        Dim nestedHeader = graph.ReadArrayHeader(raw.EntryRelativeOffset + &H10)
        Dim result As New HclClothStateTransformAccessContainerDetail_Class With {
            .EntryIndex = raw.EntryIndex,
            .EntryRelativeOffset = raw.EntryRelativeOffset,
            .RawStruct = raw,
            .NestedAccessHeader = nestedHeader
        }
        result.HeaderUInt32.AddRange(raw.UInt32Values.Take(4))

        For Each nestedRaw In ReadRawStructArray(graph, nestedHeader, 72)
            Dim access = ParseStateTransformSetAccess(graph, nestedRaw)
            If Not IsNothing(access) Then result.Accesses.Add(access)
        Next

        Return result
    End Function

    Private Shared Function ParseStateTransformSetAccess(graph As HkxObjectGraph_Class, raw As HkxRawStructGraph_Class) As HclClothStateTransformSetAccessDetail_Class
        If IsNothing(graph) OrElse IsNothing(raw) Then Return Nothing

        Dim result As New HclClothStateTransformSetAccessDetail_Class With {
            .EntryIndex = raw.EntryIndex,
            .EntryRelativeOffset = raw.EntryRelativeOffset,
            .RawStruct = raw
        }

        For subIndex = 0 To 2
            Dim componentAccess = ParseStateTransformComponentAccess(graph, raw, subIndex)
            If Not IsNothing(componentAccess) Then result.ComponentAccesses.Add(componentAccess)
        Next

        result.HasAnyMaskData = result.ComponentAccesses.Any(Function(access) access.MaskBytes.Any(Function(value) value <> 0))
        Return result
    End Function

    Private Shared Function ParseStateTransformComponentAccess(graph As HkxObjectGraph_Class, raw As HkxRawStructGraph_Class, subIndex As Integer) As HclClothStateTransformComponentAccessDetail_Class
        If IsNothing(graph) OrElse IsNothing(raw) Then Return Nothing
        If subIndex < 0 OrElse subIndex > 2 Then Return Nothing

        Dim wordBase = subIndex * 6
        Dim headerOffset = raw.EntryRelativeOffset + (subIndex * 24)
        Dim header = graph.ReadArrayHeader(headerOffset)

        Dim result As New HclClothStateTransformComponentAccessDetail_Class With {
            .SubIndex = subIndex,
            .HeaderRelativeOffset = headerOffset,
            .ArrayHeader = header,
            .MaskBytes = ReadByteArray(graph, header),
            .MaskCount = header.Count,
            .CapacityAndFlags = header.CapacityAndFlags,
            .TransformCount = If(raw.UInt32Values.Count > wordBase + 4, CInt(raw.UInt32Values(wordBase + 4)), 0),
            .ReservedValue = If(raw.UInt32Values.Count > wordBase + 5, raw.UInt32Values(wordBase + 5), 0UI)
        }

        result.MaskIndices.AddRange(DecodeMaskIndices(result.MaskBytes))
        Return result
    End Function

    Public Shared Function ParseBufferDefinition(graph As HkxObjectGraph_Class, source As HkxVirtualObjectGraph_Class) As HclBufferDefinitionDetail_Class
        If IsNothing(graph) OrElse IsNothing(source) Then Return Nothing
        If Not source.ClassName.Equals("hclBufferDefinition", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        Dim payloadUInt32 = ReadPayloadUInt32(graph, source, &H20)
        Return New HclBufferDefinitionDetail_Class With {
            .SourceObject = source,
            .Name = graph.ResolveLocalString(source.RelativeOffset + &H10),
            .PayloadRelativeOffset = source.RelativeOffset + &H20,
            .PayloadBytes = ReadPayloadBytes(graph, source, &H20),
            .PayloadUInt32 = payloadUInt32,
            .ParticleCount = If(payloadUInt32.Count > 0, CInt(payloadUInt32(0)), 0),
            .TriangleCount = If(payloadUInt32.Count > 1, CInt(payloadUInt32(1)), 0)
        }
    End Function

    Public Shared Function ParseScratchBufferDefinition(graph As HkxObjectGraph_Class, source As HkxVirtualObjectGraph_Class) As HclScratchBufferDefinitionDetail_Class
        If IsNothing(graph) OrElse IsNothing(source) Then Return Nothing
        If Not source.ClassName.Equals("hclScratchBufferDefinition", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        Dim payloadUInt32 = ReadPayloadUInt32(graph, source, &H20)
        Return New HclScratchBufferDefinitionDetail_Class With {
            .SourceObject = source,
            .Name = graph.ResolveLocalString(source.RelativeOffset + &H10),
            .PayloadRelativeOffset = source.RelativeOffset + &H20,
            .PayloadBytes = ReadPayloadBytes(graph, source, &H20),
            .PayloadUInt32 = payloadUInt32,
            .ParticleCount = If(payloadUInt32.Count > 0, CInt(payloadUInt32(0)), 0),
            .TriangleCount = If(payloadUInt32.Count > 1, CInt(payloadUInt32(1)), 0)
        }
    End Function

    Public Shared Function ParseMoveParticlesOperator(graph As HkxObjectGraph_Class, source As HkxVirtualObjectGraph_Class) As HclMoveParticlesOperatorDetail_Class
        If IsNothing(graph) OrElse IsNothing(source) Then Return Nothing
        If Not source.ClassName.Equals("hclMoveParticlesOperator", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        Return New HclMoveParticlesOperatorDetail_Class With {
            .SourceObject = source,
            .Name = graph.ResolveLocalString(source.RelativeOffset + &H10),
            .HeaderUInt32 = ReadUInt32Block(graph, source.RelativeOffset + &H18, 2),
            .Pairs = ReadVertexParticlePairs(graph, graph.ReadArrayHeader(source.RelativeOffset + &H20))
        }
    End Function

    Public Shared Function ParseSimulateOperator(graph As HkxObjectGraph_Class, source As HkxVirtualObjectGraph_Class) As HclSimulateOperatorDetail_Class
        If IsNothing(graph) OrElse IsNothing(source) Then Return Nothing
        If Not source.ClassName.Equals("hclSimulateOperator", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        Dim header = ReadUInt32Block(graph, source.RelativeOffset + &H18, 6)
        Return New HclSimulateOperatorDetail_Class With {
            .SourceObject = source,
            .Name = graph.ResolveLocalString(source.RelativeOffset + &H10),
            .HeaderUInt32 = header,
            .SubstepCount = If(header.Count > 3, CInt(header(3)), 0),
            .SolveIterationCount = If(header.Count > 4, CInt(header(4)), 0),
            .Configs = ReadUInt32ConfigArray(graph, graph.ReadArrayHeader(source.RelativeOffset + &H30))
        }
    End Function

    Public Shared Function ParseCopyVerticesOperator(graph As HkxObjectGraph_Class, source As HkxVirtualObjectGraph_Class) As HclCopyVerticesOperatorDetail_Class
        If IsNothing(graph) OrElse IsNothing(source) Then Return Nothing
        If Not source.ClassName.Equals("hclCopyVerticesOperator", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        Dim payloadBytes = ReadPayloadBytes(graph, source, &H20)
        Dim payloadUInt32 = ReadPayloadUInt32(graph, source, &H20)
        Return New HclCopyVerticesOperatorDetail_Class With {
            .SourceObject = source,
            .Name = graph.ResolveLocalString(source.RelativeOffset + &H10),
            .HeaderUInt32 = ReadUInt32Block(graph, source.RelativeOffset + &H18, 2),
            .PayloadRelativeOffset = source.RelativeOffset + &H20,
            .PayloadBytes = payloadBytes,
            .PayloadUInt32 = payloadUInt32,
            .ElementCount = If(payloadUInt32.Count > 2, CInt(payloadUInt32(2)), 0),
            .PayloadAsciiTag = ExtractPrintableAscii(payloadBytes)
        }
    End Function

    Public Shared Function ParseGatherAllVerticesOperator(graph As HkxObjectGraph_Class, source As HkxVirtualObjectGraph_Class) As HclGatherAllVerticesOperatorDetail_Class
        If IsNothing(graph) OrElse IsNothing(source) Then Return Nothing
        If Not source.ClassName.Equals("hclGatherAllVerticesOperator", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        Dim payloadBytes = ReadPayloadBytes(graph, source, &H20)
        Dim payloadUInt32 = ReadPayloadUInt32(graph, source, &H20)
        Return New HclGatherAllVerticesOperatorDetail_Class With {
            .SourceObject = source,
            .Name = graph.ResolveLocalString(source.RelativeOffset + &H10),
            .HeaderUInt32 = ReadUInt32Block(graph, source.RelativeOffset + &H18, 2),
            .PayloadRelativeOffset = source.RelativeOffset + &H20,
            .PayloadBytes = payloadBytes,
            .PayloadUInt32 = payloadUInt32,
            .ElementCount = If(payloadUInt32.Count > 2, CInt(payloadUInt32(2)), 0),
            .GatheredVertexIndices = DecodePackedUInt16List(payloadUInt32.Skip(12), If(payloadUInt32.Count > 2, CInt(payloadUInt32(2)), 0)),
            .PayloadAsciiTag = ExtractPrintableAscii(payloadBytes)
        }
    End Function

    Private Shared Function DecodePackedUInt16List(words As IEnumerable(Of UInteger), takeCount As Integer) As List(Of UShort)
        Dim result As New List(Of UShort)
        If IsNothing(words) OrElse takeCount <= 0 Then Return result

        For Each word In words
            If result.Count < takeCount Then result.Add(CUShort(word And &HFFFFUI))
            If result.Count < takeCount Then result.Add(CUShort((word >> 16) And &HFFFFUI))
            If result.Count >= takeCount Then Exit For
        Next

        Return result
    End Function

    Public Shared Function ParseCapsuleShape(graph As HkxObjectGraph_Class, source As HkxVirtualObjectGraph_Class) As HclCapsuleShapeDetail_Class
        If IsNothing(graph) OrElse IsNothing(source) Then Return Nothing
        Dim className = If(source.ClassName, String.Empty)
        If Not className.Equals("hclCapsuleShape", StringComparison.OrdinalIgnoreCase) AndAlso
           Not className.Equals("hclTaperedCapsuleShape", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        Dim isTapered = className.Equals("hclTaperedCapsuleShape", StringComparison.OrdinalIgnoreCase)
        Dim vectorCount = Math.Max(0, (source.Size - &H10) \ 16)
        Dim vectors = ReadVector4Block(graph, source.RelativeOffset + &H10, vectorCount)
        Dim endpointA = If(vectorCount > 1, vectors(1), Nothing)
        Dim endpointB = If(vectorCount > 2, vectors(2), Nothing)
        Dim extraVector0 = If(isTapered AndAlso vectorCount > 8, vectors(8), Nothing)
        Dim extraVector1 = If(isTapered AndAlso vectorCount > 9, vectors(9), Nothing)
        Dim segmentLength = 0.0F
        If endpointA IsNot Nothing AndAlso endpointB IsNot Nothing Then
            Dim dx = endpointA.X - endpointB.X
            Dim dy = endpointA.Y - endpointB.Y
            Dim dz = endpointA.Z - endpointB.Z
            segmentLength = CSng(Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz)))
        End If
        If isTapered AndAlso vectorCount > 5 Then
            segmentLength = vectors(5).X
        End If
        Dim radiusA = If(Not isTapered AndAlso vectorCount > 4, vectors(4).X, If(extraVector0 IsNot Nothing, extraVector0.X, 0.0F))
        Dim radiusB = If(Not isTapered AndAlso vectorCount > 4, vectors(4).X, If(extraVector0 IsNot Nothing, extraVector0.Y, 0.0F))
        Dim taperFactor = 0.0F
        If segmentLength > 0.000001F Then taperFactor = Math.Abs(radiusB - radiusA) / segmentLength
        Dim taperCosine = If(isTapered AndAlso extraVector1 IsNot Nothing, extraVector1.X, CSng(Math.Sqrt(Math.Max(0.0R, 1.0R - (taperFactor * taperFactor)))))

        Return New HclCapsuleShapeDetail_Class With {
            .SourceObject = source,
            .ShapeClassName = className,
            .Vectors = vectors,
            .EndpointA = endpointA,
            .EndpointB = endpointB,
            .AxisHint = If(vectorCount > 3, vectors(3), Nothing),
            .ParameterVector = If(vectorCount > 4, vectors(4), Nothing),
            .Radius = radiusA,
            .AuxiliaryRadius = radiusB,
            .SegmentLength = segmentLength,
            .TaperFactor = taperFactor,
            .TaperCosine = taperCosine,
            .ExtraScalar0 = If(isTapered AndAlso vectorCount > 5, vectors(5).X, 0.0F),
            .ExtraScalar1 = If(isTapered AndAlso vectorCount > 6, vectors(6).X, 0.0F),
            .ExtraScalar2 = If(isTapered AndAlso vectorCount > 7, vectors(7).X, 0.0F),
            .ExtraVector0 = extraVector0,
            .ExtraVector1 = extraVector1
        }
    End Function

    Public Shared Function ParseCollidable(graph As HkxObjectGraph_Class, source As HkxVirtualObjectGraph_Class) As HclCollidableDetail_Class
        If IsNothing(graph) OrElse IsNothing(source) Then Return Nothing
        If Not source.ClassName.Equals("hclCollidable", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        Dim payloadBytes = ReadPayloadBytes(graph, source, &H18)
        Dim payloadVectors = ReadVector4Block(graph, source.RelativeOffset + &H18, If(IsNothing(payloadBytes), 0, payloadBytes.Length \ 16))
        Return New HclCollidableDetail_Class With {
            .SourceObject = source,
            .Name = graph.ResolveLocalString(source.RelativeOffset + &H10),
            .ShapeObject = graph.ResolveGlobalObject(source.RelativeOffset + &H88),
            .ShapeDetail = ParseCapsuleShape(graph, graph.ResolveGlobalObject(source.RelativeOffset + &H88)),
            .PayloadRelativeOffset = source.RelativeOffset + &H18,
            .PayloadBytes = payloadBytes,
            .PayloadUInt32 = ReadPayloadUInt32(graph, source, &H18),
            .PayloadVectors = payloadVectors,
            .TransformMatrix = CreateMatrix4FromVectorRows(payloadVectors, 0),
            .LinearVelocity = If(payloadVectors.Count > 4, payloadVectors(4), Nothing),
            .AngularVelocity = If(payloadVectors.Count > 5, payloadVectors(5), Nothing),
            .ParameterVector = If(payloadVectors.Count > 6, payloadVectors(6), Nothing)
        }
    End Function
    Public Shared Function ParseStandardLinkConstraintSet(graph As HkxObjectGraph_Class, source As HkxVirtualObjectGraph_Class) As HclStandardLinkConstraintSetDetail_Class
        If IsNothing(graph) OrElse IsNothing(source) Then Return Nothing
        If Not source.ClassName.Equals("hclStandardLinkConstraintSet", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        Dim rawLinks = ReadRawStructArray(graph, graph.ReadArrayHeader(source.RelativeOffset + &H20), 12)
        Dim result As New HclStandardLinkConstraintSetDetail_Class With {
            .SourceObject = source,
            .Name = graph.ResolveLocalString(source.RelativeOffset + &H10),
            .Links = rawLinks
        }
        result.LinkDetails.AddRange(ParseDistanceConstraints(rawLinks))
        Return result
    End Function

    Public Shared Function ParseStretchLinkConstraintSet(graph As HkxObjectGraph_Class, source As HkxVirtualObjectGraph_Class) As HclStretchLinkConstraintSetDetail_Class
        If IsNothing(graph) OrElse IsNothing(source) Then Return Nothing
        If Not source.ClassName.Equals("hclStretchLinkConstraintSet", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        Dim rawLinks = ReadRawStructArray(graph, graph.ReadArrayHeader(source.RelativeOffset + &H20), 12)
        Dim result As New HclStretchLinkConstraintSetDetail_Class With {
            .SourceObject = source,
            .Name = graph.ResolveLocalString(source.RelativeOffset + &H10),
            .Links = rawLinks
        }
        result.LinkDetails.AddRange(ParseDistanceConstraints(rawLinks))
        Return result
    End Function

    Public Shared Function ParseBendStiffnessConstraintSet(graph As HkxObjectGraph_Class, source As HkxVirtualObjectGraph_Class) As HclBendStiffnessConstraintSetDetail_Class
        If IsNothing(graph) OrElse IsNothing(source) Then Return Nothing
        If Not source.ClassName.Equals("hclBendStiffnessConstraintSet", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        Dim rawLinks = ReadRawStructArray(graph, graph.ReadArrayHeader(source.RelativeOffset + &H20), 32)
        Dim result As New HclBendStiffnessConstraintSetDetail_Class With {
            .SourceObject = source,
            .Name = graph.ResolveLocalString(source.RelativeOffset + &H10),
            .Links = rawLinks
        }
        result.LinkDetails.AddRange(ParseBendConstraints(rawLinks))
        Return result
    End Function

    Public Shared Function ParseLocalRangeConstraintSet(graph As HkxObjectGraph_Class, source As HkxVirtualObjectGraph_Class) As HclLocalRangeConstraintSetDetail_Class
        If IsNothing(graph) OrElse IsNothing(source) Then Return Nothing
        If Not source.ClassName.Equals("hclLocalRangeConstraintSet", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        Dim rawConstraints = ReadRawStructArray(graph, graph.ReadArrayHeader(source.RelativeOffset + &H20), 16)
        Dim result As New HclLocalRangeConstraintSetDetail_Class With {
            .SourceObject = source,
            .Name = graph.ResolveLocalString(source.RelativeOffset + &H10),
            .Constraints = rawConstraints
        }
        result.ConstraintDetails.AddRange(ParseLocalRangeConstraints(rawConstraints))
        result.UniformMaximumDistance = ResolveUniformParameter(result.ConstraintDetails.Select(Function(item) item.MaximumDistance))
        result.UniformMaximumNormalDistance = ResolveUniformParameter(result.ConstraintDetails.Select(Function(item) item.MaximumNormalDistance))
        result.UniformMinimumNormalDistance = ResolveUniformParameter(result.ConstraintDetails.Select(Function(item) item.MinimumNormalDistance))
        result.DistinctParticleCount = result.ConstraintDetails.Select(Function(item) CInt(item.ParticleIndex)).Distinct().Count()
        result.DistinctReferenceVertexCount = result.ConstraintDetails.Select(Function(item) CInt(item.ReferenceVertexIndex)).Distinct().Count()
        result.ParticleReferenceIdentityCount = result.ConstraintDetails.Where(Function(item) item.ParticleIndex = item.ReferenceVertexIndex).Count()
        Return result
    End Function

    Public Shared Function ParseVolumeConstraintMx(graph As HkxObjectGraph_Class, source As HkxVirtualObjectGraph_Class) As HclVolumeConstraintMxDetail_Class
        If IsNothing(graph) OrElse IsNothing(source) Then Return Nothing
        If Not source.ClassName.Equals("hclVolumeConstraintMx", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        Dim result As New HclVolumeConstraintMxDetail_Class With {
            .SourceObject = source,
            .Name = graph.ResolveLocalString(source.RelativeOffset + &H10),
            .Field20RawStructs = ReadRawStructArray(graph, graph.ReadArrayHeader(source.RelativeOffset + &H20), 352),
            .Field30RawStructs = ReadRawStructArray(graph, graph.ReadArrayHeader(source.RelativeOffset + &H30), 32),
            .Field40RawStructs = ReadRawStructArray(graph, graph.ReadArrayHeader(source.RelativeOffset + &H40), 352),
            .Field50RawStructs = ReadRawStructArray(graph, graph.ReadArrayHeader(source.RelativeOffset + &H50), 32),
            .Field20VectorBlocks = ReadVectorStructArray(graph, graph.ReadArrayHeader(source.RelativeOffset + &H20), 22),
            .Field30VectorBlocks = ReadVectorStructArray(graph, graph.ReadArrayHeader(source.RelativeOffset + &H30), 2),
            .Field40VectorBlocks = ReadVectorStructArray(graph, graph.ReadArrayHeader(source.RelativeOffset + &H40), 22),
            .Field50VectorBlocks = ReadVectorStructArray(graph, graph.ReadArrayHeader(source.RelativeOffset + &H50), 2)
        }

        result.Field20Batches.AddRange(ParseVolumeConstraintBatches(result.Field20RawStructs, result.Field20VectorBlocks))
        result.Field30Entries.AddRange(ParseVolumeConstraintVectorEntries(result.Field30VectorBlocks))
        result.Field40Batches.AddRange(ParseVolumeConstraintBatches(result.Field40RawStructs, result.Field40VectorBlocks))
        result.Field50Entries.AddRange(ParseVolumeConstraintVectorEntries(result.Field50VectorBlocks))
        result.Field20QuadSlots.AddRange(result.Field20Batches.SelectMany(Function(batch) batch.QuadSlots))
        result.Field40QuadSlots.AddRange(result.Field40Batches.SelectMany(Function(batch) batch.QuadSlots))
        result.Field40BridgeSlots.AddRange(ParseVolumeConstraintBridgeSlots(result.Field40QuadSlots, result.Field20QuadSlots))
        result.Field20BridgeSourceQuadSlots.AddRange(BuildVolumeBridgeSourceQuadSlots(result.Field40BridgeSlots))
        result.Field40BridgeSourceChain.AddRange(BuildVolumeBridgeSourceChain(result.Field40BridgeSlots))
        result.Field20NonBridgeQuadSlots.AddRange(BuildVolumeNonBridgeQuadSlots(result.Field20QuadSlots, result.Field20BridgeSourceQuadSlots))
        result.Field30ParameterValues.AddRange(ExtractVolumeConstraintParameterValues(result.Field30Entries))
        result.Field50ParameterValues.AddRange(ExtractVolumeConstraintParameterValues(result.Field50Entries))
        result.Field50ToField30PivotMatches.AddRange(BuildVolumeConstraintPivotMatches(result.Field50Entries, result.Field30Entries))
        result.Field40TerminalQuadSlots.AddRange(BuildVolumeTerminalQuadSlots(result.Field40QuadSlots, result.Field40BridgeSlots))
        result.Field30UniformParameter = ResolveUniformParameter(result.Field30ParameterValues)
        result.Field50UniformParameter = ResolveUniformParameter(result.Field50ParameterValues)
        result.Field50PivotReuseOffset = ResolvePivotReuseOffset(result.Field50ToField30PivotMatches)
        result.Field50PivotReuseCount = result.Field50ToField30PivotMatches.Count
        result.Field20MidVectorsLookZeroish = result.Field20Batches.All(Function(batch) batch Is Nothing OrElse batch.MidVectorsLookZeroish)
        result.Field40MidVectorsLookZeroish = result.Field40Batches.All(Function(batch) batch Is Nothing OrElse batch.MidVectorsLookZeroish)
        result.Field20BatchUniformParameter = ResolveUniformParameter(result.Field20Batches.Where(Function(batch) batch IsNot Nothing AndAlso batch.UniformLaneParameter.HasValue).Select(Function(batch) batch.UniformLaneParameter.Value))
        result.Field40BatchUniformParameter = ResolveUniformParameter(result.Field40Batches.Where(Function(batch) batch IsNot Nothing AndAlso batch.UniformLaneParameter.HasValue).Select(Function(batch) batch.UniformLaneParameter.Value))
        result.Field20LaneParametersUniformAcrossBatches = result.Field20BatchUniformParameter.HasValue
        result.Field40LaneParametersUniformAcrossBatches = result.Field40BatchUniformParameter.HasValue
        result.Field20BatchParameterMatchesField30Parameter = result.Field20BatchUniformParameter.HasValue AndAlso result.Field30UniformParameter.HasValue AndAlso Math.Abs(CDbl(result.Field20BatchUniformParameter.Value - result.Field30UniformParameter.Value)) <= 0.0001R
        result.Field40BatchParameterMatchesField50Parameter = result.Field40BatchUniformParameter.HasValue AndAlso result.Field50UniformParameter.HasValue AndAlso Math.Abs(CDbl(result.Field40BatchUniformParameter.Value - result.Field50UniformParameter.Value)) <= 0.0001R
        result.Field20AndField40ParametersDistinct = result.Field20BatchUniformParameter.HasValue AndAlso result.Field40BatchUniformParameter.HasValue AndAlso Math.Abs(CDbl(result.Field20BatchUniformParameter.Value - result.Field40BatchUniformParameter.Value)) > 0.0001R
        result.HasDistinctParameterGroups = result.Field20AndField40ParametersDistinct AndAlso result.Field20BatchParameterMatchesField30Parameter AndAlso result.Field40BatchParameterMatchesField50Parameter
        result.Field40BridgeCountMatchesField50Count = (result.Field40BridgeSlots.Count > 0 AndAlso result.Field40BridgeSlots.Count = result.Field50Entries.Count)
        result.Field40BridgeSlotsExact = result.Field40BridgeSlots.All(Function(slot) slot IsNot Nothing AndAlso slot.SharedParticlesFirst.Count = 2 AndAlso slot.SharedParticlesSecond.Count = 2 AndAlso slot.BridgeParticles.Count = 6)
        result.Field40BridgeFormsSequentialChain = ResolveVolumeBridgeSequentialChain(result.Field40BridgeSlots)
        Dim terminalExtension = ResolveVolumeTerminalBridgeExtension(result.Field40TerminalQuadSlots, result.Field40BridgeSlots)
        result.Field40TerminalExtendsBridgeChain = terminalExtension.Item1
        result.Field40TerminalSharedParticleCount = terminalExtension.Item2
        result.Field40TerminalAddedParticleCount = terminalExtension.Item3
        result.Field40BridgeSourceChainCount = result.Field40BridgeSourceChain.Count
        result.Field40NonZeroQuadCount = result.Field40QuadSlots.Where(Function(slot) slot IsNot Nothing AndAlso Not slot.IsAllZero).Count()
        result.Field40ExactBridgeCount = result.Field40BridgeSlots.Count
        result.Field50PivotTailStartIndex = ResolvePivotTailStartIndex(result.Field50ToField30PivotMatches)
        result.Field50MatchesField30Tail = result.Field50PivotTailStartIndex.HasValue AndAlso (result.Field50PivotTailStartIndex.Value + result.Field50PivotReuseCount = result.Field30Entries.Count)
        result.Field20NonZeroQuadCount = result.Field20QuadSlots.Where(Function(slot) slot IsNot Nothing AndAlso Not slot.IsAllZero).Count()
        result.Field20NonZeroQuadCountMatchesField30Count = (result.Field20NonZeroQuadCount > 0 AndAlso result.Field20NonZeroQuadCount = result.Field30Entries.Count)
        result.Field40NonZeroQuadCountMatchesField50Count = (result.Field40NonZeroQuadCount > 0 AndAlso result.Field40NonZeroQuadCount = result.Field50Entries.Count)
        result.Field30LeadEntryCount = If(result.Field50PivotTailStartIndex.HasValue, result.Field50PivotTailStartIndex.Value, 0)
        result.Field30TailEntryCount = result.Field30Entries.Count - result.Field30LeadEntryCount
        result.Field30LeadEntries.AddRange(result.Field30Entries.Where(Function(entry) entry IsNot Nothing AndAlso entry.EntryIndex < result.Field30LeadEntryCount))
        result.Field30TailEntries.AddRange(result.Field30Entries.Where(Function(entry) entry IsNot Nothing AndAlso entry.EntryIndex >= result.Field30LeadEntryCount))
        result.Field50TailSourceEntries.AddRange(result.Field30TailEntries.Where(Function(entry) result.Field50ToField30PivotMatches.Any(Function(match) match.MatchedEntryIndex = entry.EntryIndex)))
        result.Field40TerminalQuadCount = Math.Max(0, result.Field40NonZeroQuadCount - result.Field40ExactBridgeCount)
        result.Field20ExtraActiveQuadCount = Math.Max(0, result.Field20NonZeroQuadCount - result.Field30Entries.Count)
        result.Field20BridgeSourceQuadCount = result.Field20BridgeSourceQuadSlots.Count
        result.Field20NonBridgeQuadCount = result.Field20NonBridgeQuadSlots.Count
        result.Field50TailSourceEntryCount = result.Field50TailSourceEntries.Count
        result.Field20BridgeSourceAndNonBridgePartitionMatchesActiveQuads = (result.Field20BridgeSourceQuadCount + result.Field20NonBridgeQuadCount = result.Field20NonZeroQuadCount)
        result.Field40BridgeAndTerminalPartitionMatchesActiveQuads = (result.Field40ExactBridgeCount + result.Field40TerminalQuadCount = result.Field40NonZeroQuadCount)
        result.Field40BridgeSourceChainMatchesField20BridgeSourceCount = (result.Field40BridgeSourceChainCount = result.Field20BridgeSourceQuadCount)
        result.Field30LeadCountMatchesField20ExtraActiveQuadCount = (result.Field30LeadEntryCount = result.Field20ExtraActiveQuadCount)
        result.Field30TailCountMatchesField40BridgeSourceChainCount = (result.Field30TailEntryCount = result.Field40BridgeSourceChainCount)
        result.Field50EntryCountMatchesField40BridgeSourceChainCount = (result.Field50Entries.Count = result.Field40BridgeSourceChainCount)
        result.Field50TailSourceCountMatchesField50EntryCount = (result.Field50TailSourceEntryCount = result.Field50Entries.Count)
        result.Field50TailSourceCountMatchesField30TailEntryCount = (result.Field50TailSourceEntryCount = result.Field30TailEntryCount)
        Return result
    End Function

    Private Shared Function ParseVolumeConstraintBatches(rawStructs As IEnumerable(Of HkxRawStructGraph_Class),
                                                         vectorBlocks As IEnumerable(Of HkxVectorStructBlockGraph_Class)) As List(Of HclVolumeConstraintBatch_Class)
        Dim result As New List(Of HclVolumeConstraintBatch_Class)
        If IsNothing(rawStructs) Then Return result

        Dim vectorByEntry As New Dictionary(Of Integer, HkxVectorStructBlockGraph_Class)
        If Not IsNothing(vectorBlocks) Then
            For Each block In vectorBlocks
                If IsNothing(block) Then Continue For
                vectorByEntry(block.EntryIndex) = block
            Next
        End If

        For Each raw In rawStructs
            If IsNothing(raw) Then Continue For

            Dim block As HkxVectorStructBlockGraph_Class = Nothing
            vectorByEntry.TryGetValue(raw.EntryIndex, block)

            Dim batch As New HclVolumeConstraintBatch_Class With {
                .EntryIndex = raw.EntryIndex,
                .RawStruct = raw,
                .VectorBlock = block
            }

            If block IsNot Nothing AndAlso block.Vectors IsNot Nothing Then
                batch.AllVectors.AddRange(block.Vectors)
                batch.PreQuadVectors.AddRange(block.Vectors.Take(16))
                batch.MidVectors.AddRange(block.Vectors.Skip(16).Take(2))
                batch.PostQuadVectors.AddRange(block.Vectors.Skip(18).Take(4))
            End If

            batch.QuadSlots.AddRange(ParseVolumeConstraintQuadSlots({raw}))
            PopulateVolumeConstraintBatchLanes(batch)
            batch.MidVectorsLookZeroish = batch.MidVectors.All(Function(v) v Is Nothing OrElse (Math.Abs(CDbl(v.X)) <= 0.0001R AndAlso Math.Abs(CDbl(v.Y)) <= 0.0001R AndAlso Math.Abs(CDbl(v.Z)) <= 0.0001R AndAlso Math.Abs(CDbl(v.W)) <= 0.0001R))
            batch.UniformLaneParameter = ResolveUniformParameter(batch.Lanes.Where(Function(l) l?.ParameterVector IsNot Nothing).Select(Function(l) CSng(l.ParameterVector.Y)))
            batch.LaneParameterIsUniform = batch.UniformLaneParameter.HasValue
            result.Add(batch)
        Next

        Return result
    End Function

    Private Shared Sub PopulateVolumeConstraintBatchLanes(batch As HclVolumeConstraintBatch_Class)
        If IsNothing(batch) Then Return
        batch.Lanes.Clear()

        For laneIndex = 0 To 3
            Dim lane As New HclVolumeConstraintLane_Class With {
                .LaneIndex = laneIndex,
                .QuadSlot = If(laneIndex < batch.QuadSlots.Count, batch.QuadSlots(laneIndex), Nothing),
                .ParameterVector = If(laneIndex < batch.PostQuadVectors.Count, batch.PostQuadVectors(laneIndex), Nothing)
            }

            lane.CoefficientVectors.AddRange(batch.PreQuadVectors.Skip(laneIndex * 4).Take(4))
            batch.Lanes.Add(lane)
        Next
    End Sub

    Private Shared Function ParseVolumeConstraintQuadSlots(items As IEnumerable(Of HkxRawStructGraph_Class)) As List(Of HclVolumeConstraintQuadSlot_Class)
        Dim result As New List(Of HclVolumeConstraintQuadSlot_Class)
        If IsNothing(items) Then Return result

        For Each raw In items
            If IsNothing(raw?.RawBytes) OrElse raw.RawBytes.Length < 288 Then Continue For

            For slotIndex = 0 To 3
                Dim byteOffset = 256 + (slotIndex * 8)
                If byteOffset + 7 >= raw.RawBytes.Length Then Exit For

                Dim quad As New HclVolumeConstraintQuadSlot_Class With {
                    .RawStructEntryIndex = raw.EntryIndex,
                    .SlotIndex = slotIndex,
                    .ByteOffset = byteOffset,
                    .ParticleA = BitConverter.ToUInt16(raw.RawBytes, byteOffset),
                    .ParticleB = BitConverter.ToUInt16(raw.RawBytes, byteOffset + 2),
                    .ParticleC = BitConverter.ToUInt16(raw.RawBytes, byteOffset + 4),
                    .ParticleD = BitConverter.ToUInt16(raw.RawBytes, byteOffset + 6)
                }
                quad.Particles.AddRange(New Integer() {quad.ParticleA, quad.ParticleB, quad.ParticleC, quad.ParticleD})
                quad.IsAllZero = (quad.ParticleA = 0 AndAlso quad.ParticleB = 0 AndAlso quad.ParticleC = 0 AndAlso quad.ParticleD = 0)
                result.Add(quad)
            Next
        Next

        Return result
    End Function

    Private Shared Function ParseVolumeConstraintBridgeSlots(subsetSlots As IEnumerable(Of HclVolumeConstraintQuadSlot_Class),
                                                             referenceSlots As IEnumerable(Of HclVolumeConstraintQuadSlot_Class)) As List(Of HclVolumeConstraintBridgeSlot_Class)
        Dim result As New List(Of HclVolumeConstraintBridgeSlot_Class)
        If IsNothing(subsetSlots) OrElse IsNothing(referenceSlots) Then Return result

        Dim references = referenceSlots.ToList()
        For Each slot In subsetSlots
            If IsNothing(slot) OrElse slot.Particles.Count = 0 Then Continue For

            Dim overlaps = references.
                Select(Function(reference)
                           If IsNothing(reference) Then Return Nothing
                           Dim sharedParticles = slot.Particles.Intersect(reference.Particles).ToList()
                           Return New With { .Slot = reference, .SharedParticles = sharedParticles, .SharedCount = sharedParticles.Count }
                       End Function).
                Where(Function(match) match IsNot Nothing AndAlso match.SharedCount > 0).
                OrderByDescending(Function(match) match.SharedCount).
                ThenBy(Function(match) match.Slot.RawStructEntryIndex).
                ThenBy(Function(match) match.Slot.SlotIndex).
                ToList()

            Dim bridgeMatches = overlaps.Where(Function(match) match.SharedCount = 2).Take(2).ToList()
            If bridgeMatches.Count < 2 Then Continue For

            Dim bridge As New HclVolumeConstraintBridgeSlot_Class With {
                .TargetSlot = slot,
                .FirstSourceSlot = bridgeMatches(0).Slot,
                .SecondSourceSlot = bridgeMatches(1).Slot
            }
            bridge.SharedParticlesFirst.AddRange(bridgeMatches(0).SharedParticles)
            bridge.SharedParticlesSecond.AddRange(bridgeMatches(1).SharedParticles)
            bridge.OuterParticlesFirst.AddRange(bridgeMatches(0).Slot.Particles.Except(bridgeMatches(0).SharedParticles))
            bridge.OuterParticlesSecond.AddRange(bridgeMatches(1).Slot.Particles.Except(bridgeMatches(1).SharedParticles))
            bridge.BridgeParticles.AddRange(bridgeMatches(0).Slot.Particles.Union(bridgeMatches(1).Slot.Particles).Distinct())
            result.Add(bridge)
        Next

        Return result
    End Function

    Private Shared Function BuildVolumeTerminalQuadSlots(activeSlots As IEnumerable(Of HclVolumeConstraintQuadSlot_Class),
                                                         bridgeSlots As IEnumerable(Of HclVolumeConstraintBridgeSlot_Class)) As List(Of HclVolumeConstraintQuadSlot_Class)
        Dim result As New List(Of HclVolumeConstraintQuadSlot_Class)
        If IsNothing(activeSlots) Then Return result

        Dim bridgeTargets As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        If Not IsNothing(bridgeSlots) Then
            For Each bridge In bridgeSlots
                If bridge?.TargetSlot Is Nothing Then Continue For
                bridgeTargets.Add(CreateVolumeConstraintQuadSlotKey(bridge.TargetSlot))
            Next
        End If

        For Each slot In activeSlots
            If slot Is Nothing OrElse slot.IsAllZero Then Continue For
            Dim key = CreateVolumeConstraintQuadSlotKey(slot)
            If bridgeTargets.Contains(key) Then Continue For
            result.Add(slot)
        Next

        Return result
    End Function

    Private Shared Function BuildVolumeBridgeSourceQuadSlots(bridgeSlots As IEnumerable(Of HclVolumeConstraintBridgeSlot_Class)) As List(Of HclVolumeConstraintQuadSlot_Class)
        Dim result As New List(Of HclVolumeConstraintQuadSlot_Class)
        If IsNothing(bridgeSlots) Then Return result

        Dim seen As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        For Each bridge In bridgeSlots
            If bridge Is Nothing Then Continue For
            For Each slot In New HclVolumeConstraintQuadSlot_Class() {bridge.FirstSourceSlot, bridge.SecondSourceSlot}
                If slot Is Nothing OrElse slot.IsAllZero Then Continue For
                Dim key = CreateVolumeConstraintQuadSlotKey(slot)
                If seen.Add(key) Then result.Add(slot)
            Next
        Next

        Return result
    End Function

    Private Shared Function BuildVolumeBridgeSourceChain(bridgeSlots As IEnumerable(Of HclVolumeConstraintBridgeSlot_Class)) As List(Of HclVolumeConstraintQuadSlot_Class)
        Dim result As New List(Of HclVolumeConstraintQuadSlot_Class)
        If IsNothing(bridgeSlots) Then Return result

        Dim ordered = bridgeSlots.
            Where(Function(slot) slot?.TargetSlot IsNot Nothing AndAlso slot.FirstSourceSlot IsNot Nothing AndAlso slot.SecondSourceSlot IsNot Nothing).
            OrderBy(Function(slot) slot.TargetSlot.RawStructEntryIndex).
            ThenBy(Function(slot) slot.TargetSlot.SlotIndex).
            ThenBy(Function(slot) slot.TargetSlot.ByteOffset).
            ToList()

        If ordered.Count = 0 Then Return result
        If ordered.Count = 1 Then
            result.Add(ordered(0).FirstSourceSlot)
            result.Add(ordered(0).SecondSourceSlot)
            Return result
        End If

        Dim nextKeys = New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {
            CreateVolumeConstraintQuadSlotKey(ordered(1).FirstSourceSlot),
            CreateVolumeConstraintQuadSlotKey(ordered(1).SecondSourceSlot)
        }

        Dim firstSlot = ordered(0).FirstSourceSlot
        Dim secondSlot = ordered(0).SecondSourceSlot
        If nextKeys.Contains(CreateVolumeConstraintQuadSlotKey(firstSlot)) AndAlso Not nextKeys.Contains(CreateVolumeConstraintQuadSlotKey(secondSlot)) Then
            result.Add(secondSlot)
            result.Add(firstSlot)
        Else
            result.Add(firstSlot)
            result.Add(secondSlot)
        End If

        For i = 1 To ordered.Count - 1
            Dim tailKey = CreateVolumeConstraintQuadSlotKey(result(result.Count - 1))
            Dim leftSlot = ordered(i).FirstSourceSlot
            Dim rightSlot = ordered(i).SecondSourceSlot
            Dim leftKey = CreateVolumeConstraintQuadSlotKey(leftSlot)
            Dim rightKey = CreateVolumeConstraintQuadSlotKey(rightSlot)

            If StringComparer.OrdinalIgnoreCase.Equals(leftKey, tailKey) Then
                result.Add(rightSlot)
            ElseIf StringComparer.OrdinalIgnoreCase.Equals(rightKey, tailKey) Then
                result.Add(leftSlot)
            Else
                result.Clear()
                Return result
            End If
        Next

        Return result

    End Function
    Private Shared Function BuildVolumeNonBridgeQuadSlots(activeSlots As IEnumerable(Of HclVolumeConstraintQuadSlot_Class),
                                                          bridgeSourceSlots As IEnumerable(Of HclVolumeConstraintQuadSlot_Class)) As List(Of HclVolumeConstraintQuadSlot_Class)
        Dim result As New List(Of HclVolumeConstraintQuadSlot_Class)
        If IsNothing(activeSlots) Then Return result

        Dim bridgeKeys As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        If Not IsNothing(bridgeSourceSlots) Then
            For Each slot In bridgeSourceSlots
                If slot Is Nothing OrElse slot.IsAllZero Then Continue For
                bridgeKeys.Add(CreateVolumeConstraintQuadSlotKey(slot))
            Next
        End If

        For Each slot In activeSlots
            If slot Is Nothing OrElse slot.IsAllZero Then Continue For
            Dim key = CreateVolumeConstraintQuadSlotKey(slot)
            If bridgeKeys.Contains(key) Then Continue For
            result.Add(slot)
        Next

        Return result
    End Function

    Private Shared Function CreateVolumeConstraintQuadSlotKey(slot As HclVolumeConstraintQuadSlot_Class) As String
        If slot Is Nothing Then Return String.Empty
        Return $"{slot.RawStructEntryIndex}:{slot.SlotIndex}:{slot.ByteOffset}"
    End Function

    Private Shared Function ParseVolumeConstraintVectorEntries(items As IEnumerable(Of HkxVectorStructBlockGraph_Class)) As List(Of HclVolumeConstraintVectorEntry_Class)
        Dim result As New List(Of HclVolumeConstraintVectorEntry_Class)
        If IsNothing(items) Then Return result

        For Each item In items
            If IsNothing(item) Then Continue For
            result.Add(New HclVolumeConstraintVectorEntry_Class With {
                .EntryIndex = item.EntryIndex,
                .Pivot = If(item.Vectors.Count > 0, item.Vectors(0), Nothing),
                .Parameters = If(item.Vectors.Count > 1, item.Vectors(1), Nothing)
            })
        Next

        Return result
    End Function

    Private Shared Function ExtractVolumeConstraintParameterValues(entries As IEnumerable(Of HclVolumeConstraintVectorEntry_Class)) As IEnumerable(Of Single)
        If IsNothing(entries) Then Return Enumerable.Empty(Of Single)()

        Return entries.
            Where(Function(entry) entry?.Parameters IsNot Nothing).
            Select(Function(entry) entry.Parameters.Y).
            Distinct().
            OrderBy(Function(value) value).
            ToList()
    End Function

    Private Shared Function BuildVolumeConstraintPivotMatches(subsetEntries As IEnumerable(Of HclVolumeConstraintVectorEntry_Class),
                                                             referenceEntries As IEnumerable(Of HclVolumeConstraintVectorEntry_Class)) As IEnumerable(Of HclVolumeConstraintPivotMatch_Class)
        Dim result As New List(Of HclVolumeConstraintPivotMatch_Class)
        If IsNothing(subsetEntries) OrElse IsNothing(referenceEntries) Then Return result

        Dim references = referenceEntries.Where(Function(entry) entry?.Pivot IsNot Nothing).ToList()
        For Each entry In subsetEntries.Where(Function(item) item?.Pivot IsNot Nothing)
            Dim matchIndex = references.FindIndex(Function(candidate) VolumeConstraintVectorsAlmostEqual(entry.Pivot, candidate.Pivot, 0.001F))
            If matchIndex < 0 Then Continue For

            result.Add(New HclVolumeConstraintPivotMatch_Class With {
                .EntryIndex = entry.EntryIndex,
                .MatchedEntryIndex = references(matchIndex).EntryIndex
            })
        Next

        Return result
    End Function

    Private Shared Function ResolveUniformParameter(values As IEnumerable(Of Single)) As Single?
        If IsNothing(values) Then Return Nothing

        Dim distinctValues = values.Distinct().ToList()
        If distinctValues.Count <> 1 Then Return Nothing
        Return distinctValues(0)
    End Function

    Private Shared Function ResolvePivotTailStartIndex(matches As IEnumerable(Of HclVolumeConstraintPivotMatch_Class)) As Integer?
        If IsNothing(matches) Then Return Nothing

        Dim ordered = matches.Select(Function(match) match.MatchedEntryIndex).Distinct().OrderBy(Function(value) value).ToList()
        If ordered.Count = 0 Then Return Nothing

        For i = 1 To ordered.Count - 1
            If ordered(i) <> ordered(i - 1) + 1 Then Return Nothing
        Next

        Return ordered(0)
    End Function

    Private Shared Function ResolvePivotReuseOffset(matches As IEnumerable(Of HclVolumeConstraintPivotMatch_Class)) As Integer?
        If IsNothing(matches) Then Return Nothing

        Dim deltas = matches.Select(Function(match) match.MatchedEntryIndex - match.EntryIndex).Distinct().ToList()
        If deltas.Count <> 1 Then Return Nothing
        Return deltas(0)
    End Function

    Private Shared Function ResolveVolumeBridgeSequentialChain(bridgeSlots As IEnumerable(Of HclVolumeConstraintBridgeSlot_Class)) As Boolean
        If IsNothing(bridgeSlots) Then Return False

        Dim ordered = bridgeSlots.
            Where(Function(slot) slot?.TargetSlot IsNot Nothing AndAlso slot.FirstSourceSlot IsNot Nothing AndAlso slot.SecondSourceSlot IsNot Nothing).
            OrderBy(Function(slot) slot.TargetSlot.RawStructEntryIndex).
            ThenBy(Function(slot) slot.TargetSlot.SlotIndex).
            ThenBy(Function(slot) slot.TargetSlot.ByteOffset).
            ToList()

        If ordered.Count = 0 Then Return False
        If ordered.Count = 1 Then Return True

        Dim nextKeys = New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {
            CreateVolumeConstraintQuadSlotKey(ordered(1).FirstSourceSlot),
            CreateVolumeConstraintQuadSlotKey(ordered(1).SecondSourceSlot)
        }

        Dim chain As New List(Of String)
        Dim firstKey = CreateVolumeConstraintQuadSlotKey(ordered(0).FirstSourceSlot)
        Dim secondKey = CreateVolumeConstraintQuadSlotKey(ordered(0).SecondSourceSlot)
        If nextKeys.Contains(firstKey) AndAlso Not nextKeys.Contains(secondKey) Then
            chain.Add(secondKey)
            chain.Add(firstKey)
        Else
            chain.Add(firstKey)
            chain.Add(secondKey)
        End If

        For i = 1 To ordered.Count - 1
            Dim tail = chain(chain.Count - 1)
            Dim leftKey = CreateVolumeConstraintQuadSlotKey(ordered(i).FirstSourceSlot)
            Dim rightKey = CreateVolumeConstraintQuadSlotKey(ordered(i).SecondSourceSlot)

            If StringComparer.OrdinalIgnoreCase.Equals(leftKey, tail) Then
                chain.Add(rightKey)
            ElseIf StringComparer.OrdinalIgnoreCase.Equals(rightKey, tail) Then
                chain.Add(leftKey)
            Else
                Return False
            End If
        Next

        Return chain.Count = ordered.Count + 1
    End Function

    Private Shared Function ResolveVolumeTerminalBridgeExtension(terminalSlots As IEnumerable(Of HclVolumeConstraintQuadSlot_Class),
                                                                bridgeSlots As IEnumerable(Of HclVolumeConstraintBridgeSlot_Class)) As Tuple(Of Boolean, Integer, Integer)
        Dim terminals = If(terminalSlots, Enumerable.Empty(Of HclVolumeConstraintQuadSlot_Class)()).Where(Function(slot) slot IsNot Nothing AndAlso Not slot.IsAllZero).ToList()
        Dim ordered = If(bridgeSlots, Enumerable.Empty(Of HclVolumeConstraintBridgeSlot_Class)()).
            Where(Function(slot) slot?.TargetSlot IsNot Nothing AndAlso slot.FirstSourceSlot IsNot Nothing AndAlso slot.SecondSourceSlot IsNot Nothing).
            OrderBy(Function(slot) slot.TargetSlot.RawStructEntryIndex).
            ThenBy(Function(slot) slot.TargetSlot.SlotIndex).
            ThenBy(Function(slot) slot.TargetSlot.ByteOffset).
            ToList()

        If terminals.Count <> 1 OrElse ordered.Count = 0 Then Return Tuple.Create(False, 0, 0)

        Dim chain As New List(Of HclVolumeConstraintQuadSlot_Class)
        chain.Add(ordered(0).FirstSourceSlot)
        chain.Add(ordered(0).SecondSourceSlot)

        For i = 1 To ordered.Count - 1
            Dim tailKey = CreateVolumeConstraintQuadSlotKey(chain(chain.Count - 1))
            Dim leftSlot = ordered(i).FirstSourceSlot
            Dim rightSlot = ordered(i).SecondSourceSlot
            Dim leftKey = CreateVolumeConstraintQuadSlotKey(leftSlot)
            Dim rightKey = CreateVolumeConstraintQuadSlotKey(rightSlot)

            If StringComparer.OrdinalIgnoreCase.Equals(leftKey, tailKey) Then
                chain.Add(rightSlot)
            ElseIf StringComparer.OrdinalIgnoreCase.Equals(rightKey, tailKey) Then
                chain.Add(leftSlot)
            Else
                Return Tuple.Create(False, 0, 0)
            End If
        Next

        Dim lastSource = chain(chain.Count - 1)
        Dim terminal = terminals(0)
        Dim sharedCount = terminal.Particles.Intersect(lastSource.Particles).Count()
        Dim added = terminal.Particles.Except(lastSource.Particles).Count()
        Return Tuple.Create(sharedCount = 2 AndAlso added = 2, sharedCount, added)
    End Function

    Private Shared Function VolumeConstraintVectorsAlmostEqual(left As HkxVector4Graph_Class,
                                                             right As HkxVector4Graph_Class,
                                                             tolerance As Single) As Boolean
        If IsNothing(left) OrElse IsNothing(right) Then Return False
        Return Math.Abs(left.X - right.X) <= tolerance AndAlso
               Math.Abs(left.Y - right.Y) <= tolerance AndAlso
               Math.Abs(left.Z - right.Z) <= tolerance AndAlso
               Math.Abs(left.W - right.W) <= tolerance
    End Function

    Private Shared Function ParseSimParticleData(values As IEnumerable(Of HkxVector4Graph_Class)) As List(Of HclSimParticleDataGraph_Class)
        Dim result As New List(Of HclSimParticleDataGraph_Class)
        If IsNothing(values) Then Return result

        Dim entryIndex = 0
        For Each value In values
            If IsNothing(value) Then Continue For
            result.Add(New HclSimParticleDataGraph_Class With {
                .EntryIndex = entryIndex,
                .Mass = value.X,
                .InverseMass = value.Y,
                .Radius = value.Z,
                .Friction = value.W
            })
            entryIndex += 1
        Next

        Return result
    End Function

    Private Shared Function ParseDistanceConstraints(rawLinks As IEnumerable(Of HkxRawStructGraph_Class)) As List(Of HclDistanceConstraintGraph_Class)
        Dim result As New List(Of HclDistanceConstraintGraph_Class)
        If IsNothing(rawLinks) Then Return result

        For Each raw In rawLinks
            If IsNothing(raw) OrElse IsNothing(raw.RawBytes) OrElse raw.RawBytes.Length < 12 Then Continue For
            result.Add(New HclDistanceConstraintGraph_Class With {
                .EntryIndex = raw.EntryIndex,
                .RawStruct = raw,
                .ParticleA = BitConverter.ToUInt16(raw.RawBytes, 0),
                .ParticleB = BitConverter.ToUInt16(raw.RawBytes, 2),
                .RestLength = BitConverter.ToSingle(raw.RawBytes, 4),
                .Stiffness = BitConverter.ToSingle(raw.RawBytes, 8)
            })
        Next

        Return result
    End Function

    Private Shared Function ParseBendConstraints(rawLinks As IEnumerable(Of HkxRawStructGraph_Class)) As List(Of HclBendConstraintGraph_Class)
        Dim result As New List(Of HclBendConstraintGraph_Class)
        If IsNothing(rawLinks) Then Return result

        For Each raw In rawLinks
            If IsNothing(raw) OrElse IsNothing(raw.RawBytes) OrElse raw.RawBytes.Length < 32 Then Continue For
            result.Add(New HclBendConstraintGraph_Class With {
                .EntryIndex = raw.EntryIndex,
                .RawStruct = raw,
                .WeightA = BitConverter.ToSingle(raw.RawBytes, 0),
                .WeightB = BitConverter.ToSingle(raw.RawBytes, 4),
                .WeightC = BitConverter.ToSingle(raw.RawBytes, 8),
                .WeightD = BitConverter.ToSingle(raw.RawBytes, 12),
                .BendStiffness = BitConverter.ToSingle(raw.RawBytes, 16),
                .RestCurvature = BitConverter.ToSingle(raw.RawBytes, 20),
                .ParticleA = BitConverter.ToUInt16(raw.RawBytes, 24),
                .ParticleB = BitConverter.ToUInt16(raw.RawBytes, 26),
                .ParticleC = BitConverter.ToUInt16(raw.RawBytes, 28),
                .ParticleD = BitConverter.ToUInt16(raw.RawBytes, 30)
            })
        Next

        Return result
    End Function

    Private Shared Function ParseLocalRangeConstraints(rawConstraints As IEnumerable(Of HkxRawStructGraph_Class)) As List(Of HclLocalRangeConstraintGraph_Class)
        Dim result As New List(Of HclLocalRangeConstraintGraph_Class)
        If IsNothing(rawConstraints) Then Return result

        For Each raw In rawConstraints
            If IsNothing(raw) OrElse IsNothing(raw.RawBytes) OrElse raw.RawBytes.Length < 16 Then Continue For
            result.Add(New HclLocalRangeConstraintGraph_Class With {
                .EntryIndex = raw.EntryIndex,
                .RawStruct = raw,
                .ParticleIndex = BitConverter.ToUInt16(raw.RawBytes, 0),
                .ReferenceVertexIndex = BitConverter.ToUInt16(raw.RawBytes, 2),
                .MaximumDistance = BitConverter.ToSingle(raw.RawBytes, 4),
                .MaximumNormalDistance = BitConverter.ToSingle(raw.RawBytes, 8),
                .MinimumNormalDistance = BitConverter.ToSingle(raw.RawBytes, 12)
            })
        Next

        Return result
    End Function

    Public Shared Function ParseConstraintObject(graph As HkxObjectGraph_Class, source As HkxVirtualObjectGraph_Class) As Object
        If IsNothing(graph) OrElse IsNothing(source) Then Return Nothing

        Select Case source.ClassName.ToLowerInvariant()
            Case "hclstandardlinkconstraintset"
                Return ParseStandardLinkConstraintSet(graph, source)
            Case "hclstretchlinkconstraintset"
                Return ParseStretchLinkConstraintSet(graph, source)
            Case "hclbendstiffnessconstraintset"
                Return ParseBendStiffnessConstraintSet(graph, source)
            Case "hcllocalrangeconstraintset"
                Return ParseLocalRangeConstraintSet(graph, source)
            Case "hclvolumeconstraintmx"
                Return ParseVolumeConstraintMx(graph, source)
            Case Else
                Return source
        End Select
    End Function

    Private Shared Function CreateMatrix4FromVectorRows(vectors As IReadOnlyList(Of HkxVector4Graph_Class), startIndex As Integer) As HkxMatrix4Graph_Class
        If IsNothing(vectors) Then Return Nothing
        If startIndex < 0 OrElse vectors.Count < startIndex + 4 Then Return Nothing

        Dim values As New List(Of Single)(16)
        For i = 0 To 3
            Dim row = vectors(startIndex + i)
            If IsNothing(row) Then Return Nothing
            values.Add(row.X)
            values.Add(row.Y)
            values.Add(row.Z)
            values.Add(row.W)
        Next

        Return New HkxMatrix4Graph_Class With {
            .Values = values.ToArray()
        }
    End Function
    Private Shared Function ReadVertexParticlePairs(graph As HkxObjectGraph_Class, field As HkxObjectArrayHeader_Class) As List(Of HclMoveParticlesVertexParticlePairGraph_Class)
        Dim result As New List(Of HclMoveParticlesVertexParticlePairGraph_Class)
        If IsNothing(field) OrElse field.Count <= 0 OrElse field.DataRelativeOffset < 0 Then Return result

        For i = 0 To field.Count - 1
            Dim entryOffset = field.DataRelativeOffset + (i * 4)
            result.Add(New HclMoveParticlesVertexParticlePairGraph_Class With {
                .EntryIndex = i,
                .EntryRelativeOffset = entryOffset,
                .VertexIndex = ReadUInt16(graph, entryOffset),
                .ParticleIndex = ReadUInt16(graph, entryOffset + 2)
            })
        Next

        Return result
    End Function

    Private Shared Function ReadUInt32ConfigArray(graph As HkxObjectGraph_Class, field As HkxObjectArrayHeader_Class) As List(Of HclSimulateOperatorConfigGraph_Class)
        Dim result As New List(Of HclSimulateOperatorConfigGraph_Class)
        If IsNothing(field) OrElse field.Count <= 0 OrElse field.DataRelativeOffset < 0 Then Return result

        For i = 0 To field.Count - 1
            Dim entryOffset = field.DataRelativeOffset + (i * 4)
            result.Add(New HclSimulateOperatorConfigGraph_Class With {
                .EntryIndex = i,
                .EntryRelativeOffset = entryOffset,
                .Value = ReadUInt32(graph, entryOffset)
            })
        Next

        Return result
    End Function

    Private Shared Function ReadUInt16TriangleArray(values As IEnumerable(Of UShort)) As List(Of HkxUInt16TriangleGraph_Class)
        Dim result As New List(Of HkxUInt16TriangleGraph_Class)
        If IsNothing(values) Then Return result

        Dim items = values.ToList()
        For i = 0 To (items.Count \ 3) - 1
            Dim baseIndex = i * 3
            result.Add(New HkxUInt16TriangleGraph_Class With {
                .TriangleIndex = i,
                .Value0 = items(baseIndex),
                .Value1 = items(baseIndex + 1),
                .Value2 = items(baseIndex + 2)
            })
        Next

        Return result
    End Function

    Private Shared Function ExtractPrintableAscii(bytes As Byte()) As String
        If IsNothing(bytes) OrElse bytes.Length = 0 Then Return String.Empty

        Dim chars = bytes.
            SkipWhile(Function(b) b = 0).
            Select(Function(b) If(b >= 32 AndAlso b <= 126, ChrW(b), ControlChars.NullChar)).
            ToArray()

        Dim text = New String(chars)
        Dim parts = text.Split(ControlChars.NullChar).Where(Function(part) part.Length >= 4).ToList()
        If parts.Count = 0 Then Return String.Empty
        Return parts(parts.Count - 1)
    End Function

    Private Shared Function ReadUInt32Block(graph As HkxObjectGraph_Class, relativeOffset As Integer, count As Integer) As List(Of UInteger)
        Dim result As New List(Of UInteger)
        If count <= 0 Then Return result

        For i = 0 To count - 1
            result.Add(ReadUInt32(graph, relativeOffset + (i * 4)))
        Next

        Return result
    End Function

    Private Shared Function ReadUInt32PairArray(graph As HkxObjectGraph_Class, field As HkxObjectArrayHeader_Class) As List(Of HkxUInt32PairGraph_Class)
        Dim result As New List(Of HkxUInt32PairGraph_Class)
        If IsNothing(field) OrElse field.Count <= 0 OrElse field.DataRelativeOffset < 0 Then Return result

        For i = 0 To field.Count - 1
            Dim entryOffset = field.DataRelativeOffset + (i * 8)
            result.Add(New HkxUInt32PairGraph_Class With {
                .EntryIndex = i,
                .EntryRelativeOffset = entryOffset,
                .FirstValue = ReadUInt32(graph, entryOffset),
                .SecondValue = ReadUInt32(graph, entryOffset + 4)
            })
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


    Private Shared Function ReadVectorStructArray(graph As HkxObjectGraph_Class, field As HkxObjectArrayHeader_Class, vectorCountPerEntry As Integer) As List(Of HkxVectorStructBlockGraph_Class)
        Dim result As New List(Of HkxVectorStructBlockGraph_Class)
        If IsNothing(field) OrElse vectorCountPerEntry <= 0 OrElse field.Count <= 0 OrElse field.DataRelativeOffset < 0 Then Return result

        Dim structSize = vectorCountPerEntry * 16
        For i = 0 To field.Count - 1
            Dim entryOffset = field.DataRelativeOffset + (i * structSize)
            result.Add(New HkxVectorStructBlockGraph_Class With {
                .EntryIndex = i,
                .EntryRelativeOffset = entryOffset,
                .Vectors = ReadVector4Block(graph, entryOffset, vectorCountPerEntry)
            })
        Next

        Return result
    End Function

    Private Shared Function CreateRawStruct(graph As HkxObjectGraph_Class, entryIndex As Integer, entryRelativeOffset As Integer, byteCount As Integer) As HkxRawStructGraph_Class
        Dim bytes = ReadBytes(graph, entryRelativeOffset, byteCount)
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

    Private Shared Function ReadVector4Array(graph As HkxObjectGraph_Class, field As HkxObjectArrayHeader_Class) As List(Of HkxVector4Graph_Class)
        If IsNothing(field) Then Return New List(Of HkxVector4Graph_Class)
        Return ReadVector4Block(graph, field.DataRelativeOffset, field.Count)
    End Function

    Private Shared Function ReadVector4Block(graph As HkxObjectGraph_Class, dataRelativeOffset As Integer, count As Integer) As List(Of HkxVector4Graph_Class)
        Dim result As New List(Of HkxVector4Graph_Class)
        If count <= 0 OrElse dataRelativeOffset < 0 Then Return result

        For i = 0 To count - 1
            Dim entryOffset = dataRelativeOffset + (i * 16)
            result.Add(New HkxVector4Graph_Class With {
                .X = graph.ReadSingle(entryOffset + 0),
                .Y = graph.ReadSingle(entryOffset + 4),
                .Z = graph.ReadSingle(entryOffset + 8),
                .W = graph.ReadSingle(entryOffset + 12)
            })
        Next

        Return result
    End Function

    Private Shared Function ReadMatrix4Array(graph As HkxObjectGraph_Class, field As HkxObjectArrayHeader_Class) As List(Of HkxMatrix4Graph_Class)
        Dim result As New List(Of HkxMatrix4Graph_Class)
        If IsNothing(field) OrElse field.Count <= 0 OrElse field.DataRelativeOffset < 0 Then Return result

        For i = 0 To field.Count - 1
            Dim matrixOffset = field.DataRelativeOffset + (i * 64)
            Dim values(15) As Single
            For j = 0 To 15
                values(j) = graph.ReadSingle(matrixOffset + (j * 4))
            Next
            result.Add(New HkxMatrix4Graph_Class With {
                .RelativeOffset = matrixOffset,
                .Values = values
            })
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

    Private Shared Function ReadUInt32Array(graph As HkxObjectGraph_Class, field As HkxObjectArrayHeader_Class) As List(Of UInteger)
        Dim result As New List(Of UInteger)
        If IsNothing(field) OrElse field.Count <= 0 OrElse field.DataRelativeOffset < 0 Then Return result

        For i = 0 To field.Count - 1
            result.Add(ReadUInt32(graph, field.DataRelativeOffset + (i * 4)))
        Next

        Return result
    End Function

    Private Shared Function ReadByteArray(graph As HkxObjectGraph_Class, field As HkxObjectArrayHeader_Class) As Byte()
        If IsNothing(field) OrElse field.Count <= 0 OrElse field.DataRelativeOffset < 0 Then Return Array.Empty(Of Byte)()
        Return ReadBytes(graph, field.DataRelativeOffset, field.Count)
    End Function

    Private Shared Function DecodeMaskIndices(mask As Byte()) As List(Of Integer)
        Dim result As New List(Of Integer)
        If IsNothing(mask) Then Return result

        For byteIndex = 0 To mask.Length - 1
            Dim value = mask(byteIndex)
            For bit = 0 To 7
                If (value And CByte(1 << bit)) <> 0 Then
                    result.Add((byteIndex * 8) + bit)
                End If
            Next
        Next

        Return result
    End Function

    Private Shared Function ReadPayloadBytes(graph As HkxObjectGraph_Class, source As HkxVirtualObjectGraph_Class, payloadOffset As Integer) As Byte()
        Dim byteCount = Math.Max(0, source.Size - payloadOffset)
        If byteCount <= 0 Then Return Array.Empty(Of Byte)()
        Return ReadBytes(graph, source.RelativeOffset + payloadOffset, byteCount)
    End Function

    Private Shared Function ReadPayloadUInt32(graph As HkxObjectGraph_Class, source As HkxVirtualObjectGraph_Class, payloadOffset As Integer) As List(Of UInteger)
        Dim result As New List(Of UInteger)
        Dim bytes = ReadPayloadBytes(graph, source, payloadOffset)
        For i = 0 To (bytes.Length \ 4) - 1
            result.Add(BitConverter.ToUInt32(bytes, i * 4))
        Next
        Return result
    End Function

    Private Shared Function ReadBytes(graph As HkxObjectGraph_Class, relativeOffset As Integer, byteCount As Integer) As Byte()
        If IsNothing(graph) OrElse byteCount <= 0 Then Return Array.Empty(Of Byte)()
        Dim absoluteOffset = graph.ContentsSection.AbsoluteDataStart + relativeOffset
        If absoluteOffset < 0 OrElse absoluteOffset + byteCount > graph.Packfile.RawBytes.Length Then Return Array.Empty(Of Byte)()
        Dim result(byteCount - 1) As Byte
        Array.Copy(graph.Packfile.RawBytes, absoluteOffset, result, 0, byteCount)
        Return result
    End Function

    Private Shared Function ReadUInt16(graph As HkxObjectGraph_Class, relativeOffset As Integer) As UShort
        Dim absoluteOffset = graph.ContentsSection.AbsoluteDataStart + relativeOffset
        If absoluteOffset < 0 OrElse absoluteOffset + 2 > graph.Packfile.RawBytes.Length Then Return 0
        Return BitConverter.ToUInt16(graph.Packfile.RawBytes, absoluteOffset)
    End Function

    Private Shared Function ReadUInt32(graph As HkxObjectGraph_Class, relativeOffset As Integer) As UInteger
        Dim absoluteOffset = graph.ContentsSection.AbsoluteDataStart + relativeOffset
        If absoluteOffset < 0 OrElse absoluteOffset + 4 > graph.Packfile.RawBytes.Length Then Return 0
        Return BitConverter.ToUInt32(graph.Packfile.RawBytes, absoluteOffset)
    End Function
End Class

Public Class HclSimClothDataDetail_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Name As String
    Public Property Field38Vectors As List(Of HkxVector4Graph_Class)
    Public ReadOnly Property ParticleDatas As New List(Of HclSimParticleDataGraph_Class)
    Public Property Field48UInt16 As List(Of UShort)
    Public ReadOnly Property FixedParticleIndices As New List(Of Integer)
    Public Property Field48MatchesMoveParticles As Boolean
    Public ReadOnly Property ResolvedMoveParticlePairs As New List(Of HclMoveParticlesVertexParticlePairGraph_Class)
    Public Property Field58UInt16 As List(Of UShort)
    Public ReadOnly Property Triangles As New List(Of HkxUInt16TriangleGraph_Class)
    Public Property Field68Bytes As Byte()
    Public Property Field88UInt32 As List(Of UInteger)
    Public Property Field98Matrices As List(Of HkxMatrix4Graph_Class)
    Public Property Collidables As List(Of HkxVirtualObjectGraph_Class)
    Public ReadOnly Property CollidableDetails As New List(Of HclCollidableDetail_Class)
    Public ReadOnly Property CollidableBindings As New List(Of HclSimCollidableBinding_Class)
    Public Property ConstraintSets As List(Of HkxVirtualObjectGraph_Class)
    Public ReadOnly Property ConstraintDetails As New List(Of Object)
    Public Property DefaultClothPoses As List(Of HkxVirtualObjectGraph_Class)
    Public ReadOnly Property DefaultClothPoseDetails As New List(Of HclSimClothPoseGraph_Class)
    Public Property FieldF8UInt32 As List(Of UInteger)
    Public ReadOnly Property StaticCollisionMasks As New List(Of UInteger)
    Public Property Field108Bytes As Byte()
    Public ReadOnly Property PinchDetectionFlags As New List(Of Byte)
    Public Property Field118Pairs As List(Of HkxUInt32PairGraph_Class)
    Public Property CollidableBindingUniformParameter As Single?
    Public Property CollidableBindingParametersUniform As Boolean
    Public Property CollidableBindingsAllMatrixIdentity As Boolean
    Public Property VolumeConstraintCount As Integer
    Public Property VolumeConstraintField30MatchesBindingParameter As Boolean
    Public Property VolumeConstraintField50MatchesBindingParameter As Boolean
End Class

Public Class HclSimCollidableBinding_Class
    Public Property EntryIndex As Integer
    Public Property BoneIndex As Integer
    Public Property BoneName As String
    Public Property TransformSetIndex As UInteger
    Public Property ParameterRaw As UInteger
    Public Property ParameterSingle As Single
    Public Property Collidable As HclCollidableDetail_Class
    Public Property Matrix As HkxMatrix4Graph_Class
    Public Property MatrixIdentityDelta As Double
    Public Property CollidableTransformIdentityDelta As Double
    Public Property BindTimesCollidableIdentityDelta As Double
    Public Property CollidableTimesBindIdentityDelta As Double
    Public Property BindingInverseCollidableDelta As Double
    Public Property MatrixIsIdentity As Boolean
    Public Property CollidableTransformIsIdentity As Boolean
    Public Property BindingMatchesInverseCollidable As Boolean
End Class

Public Class HclClothStateDetail_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Name As String
    Public Property Field18UInt32 As List(Of UInteger)
    Public ReadOnly Property OperatorIndices As New List(Of Integer)
    Public ReadOnly Property ResolvedOperators As New List(Of HkxVirtualObjectGraph_Class)
    Public ReadOnly Property ResolvedOperatorNames As New List(Of String)
    Public Property Field28Vectors As List(Of HkxVector4Graph_Class)
    Public Property Field38Structs As List(Of HkxRawStructGraph_Class)
    Public Property Field48Vectors As List(Of HkxVector4Graph_Class)
    Public Property FieldB0Structs As List(Of HkxRawStructGraph_Class)
    Public Property FieldC0Bytes As Byte()
    Public Property FieldD8Bytes As Byte()
    Public Property FieldF0Bytes As Byte()
    Public Property Field108Bytes As Byte()
    Public Property Field120Bytes As Byte()
    Public Property Field138Bytes As Byte()
    Public ReadOnly Property BufferAccesses As New List(Of HclClothStateBufferAccessDetail_Class)
    Public ReadOnly Property AuxiliaryBufferAccesses As New List(Of HclClothStateBufferAccessDetail_Class)
    Public ReadOnly Property TransformAccessContainers As New List(Of HclClothStateTransformAccessContainerDetail_Class)
    Public ReadOnly Property TransformSetAccesses As New List(Of HclClothStateTransformSetAccessDetail_Class)
End Class

Public Class HclClothStateBufferAccessDetail_Class
    Public Property EntryIndex As Integer
    Public Property EntryRelativeOffset As Integer
    Public Property RawStruct As HkxRawStructGraph_Class
    Public Property Word0 As UInteger
    Public Property Word1 As UInteger
    Public Property Word2 As UInteger
    Public Property Word3 As UInteger
    Public Property BufferIndex As Integer
    Public Property AccessCode As Integer
    Public Property AccessCodeLowByte As Integer
    Public Property AccessCodeHighByte As Integer
    Public Property ResolvedBufferName As String
End Class

Public Class HclClothStateTransformAccessContainerDetail_Class
    Public Property EntryIndex As Integer
    Public Property EntryRelativeOffset As Integer
    Public Property RawStruct As HkxRawStructGraph_Class
    Public ReadOnly Property HeaderUInt32 As New List(Of UInteger)
    Public Property NestedAccessHeader As HkxObjectArrayHeader_Class
    Public ReadOnly Property Accesses As New List(Of HclClothStateTransformSetAccessDetail_Class)
End Class

Public Class HclClothStateTransformSetAccessDetail_Class
    Public Property EntryIndex As Integer
    Public Property EntryRelativeOffset As Integer
    Public Property RawStruct As HkxRawStructGraph_Class
    Public ReadOnly Property ComponentAccesses As New List(Of HclClothStateTransformComponentAccessDetail_Class)
    Public Property HasAnyMaskData As Boolean
End Class

Public Class HclClothStateTransformComponentAccessDetail_Class
    Public Property SubIndex As Integer
    Public Property HeaderRelativeOffset As Integer
    Public Property ArrayHeader As HkxObjectArrayHeader_Class
    Public Property MaskBytes As Byte()
    Public Property MaskCount As Integer
    Public Property CapacityAndFlags As Integer
    Public Property TransformCount As Integer
    Public ReadOnly Property MatchingSkinPaletteIndices As New List(Of Integer)
    Public ReadOnly Property MatchingSkinBoneNames As New List(Of String)
    Public Property ReservedValue As UInteger
    Public ReadOnly Property MaskIndices As New List(Of Integer)
    Public ReadOnly Property ResolvedBoneNames As New List(Of String)
End Class

Public Class HclBufferDefinitionDetail_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Name As String
    Public Property PayloadRelativeOffset As Integer
    Public Property PayloadBytes As Byte()
    Public Property PayloadUInt32 As List(Of UInteger)
    Public Property ParticleCount As Integer
    Public Property TriangleCount As Integer
End Class

Public Class HclScratchBufferDefinitionDetail_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Name As String
    Public Property PayloadRelativeOffset As Integer
    Public Property PayloadBytes As Byte()
    Public Property PayloadUInt32 As List(Of UInteger)
    Public Property ParticleCount As Integer
    Public Property TriangleCount As Integer
End Class

Public Class HclMoveParticlesOperatorDetail_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Name As String
    Public Property HeaderUInt32 As List(Of UInteger)
    Public Property Pairs As List(Of HclMoveParticlesVertexParticlePairGraph_Class)
End Class

Public Class HclMoveParticlesVertexParticlePairGraph_Class
    Public Property EntryIndex As Integer
    Public Property EntryRelativeOffset As Integer
    Public Property VertexIndex As UShort
    Public Property ParticleIndex As UShort
End Class

Public Class HclSimulateOperatorDetail_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Name As String
    Public Property HeaderUInt32 As List(Of UInteger)
    Public Property SubstepCount As Integer
    Public Property SolveIterationCount As Integer
    Public Property Configs As List(Of HclSimulateOperatorConfigGraph_Class)
End Class

Public Class HclSimulateOperatorConfigGraph_Class
    Public Property EntryIndex As Integer
    Public Property EntryRelativeOffset As Integer
    Public Property Value As UInteger
    Public Property ConstraintIndex As Integer = -1
    Public Property IsTerminator As Boolean
    Public Property ResolvedConstraint As Object
    Public Property ResolvedConstraintName As String
    Public Property ResolvedConstraintType As String
End Class

Public Class HclCopyVerticesOperatorDetail_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Name As String
    Public Property HeaderUInt32 As List(Of UInteger)
    Public Property PayloadRelativeOffset As Integer
    Public Property PayloadBytes As Byte()
    Public Property PayloadUInt32 As List(Of UInteger)
    Public Property ElementCount As Integer
    Public Property PayloadAsciiTag As String
End Class

Public Class HclGatherAllVerticesOperatorDetail_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Name As String
    Public Property HeaderUInt32 As List(Of UInteger)
    Public Property PayloadRelativeOffset As Integer
    Public Property PayloadBytes As Byte()
    Public Property PayloadUInt32 As List(Of UInteger)
    Public Property ElementCount As Integer
    Public Property PayloadAsciiTag As String
    Public Property GatheredVertexIndices As New List(Of UShort)
End Class

Public Class HclCollidableDetail_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Name As String
    Public Property ShapeObject As HkxVirtualObjectGraph_Class
    Public Property ShapeDetail As HclCapsuleShapeDetail_Class
    Public Property PayloadRelativeOffset As Integer
    Public Property PayloadBytes As Byte()
    Public Property PayloadUInt32 As List(Of UInteger)
    Public Property PayloadVectors As List(Of HkxVector4Graph_Class)
    Public Property TransformMatrix As HkxMatrix4Graph_Class
    Public Property LinearVelocity As HkxVector4Graph_Class
    Public Property AngularVelocity As HkxVector4Graph_Class
    Public Property ParameterVector As HkxVector4Graph_Class
End Class

Public Class HclCapsuleShapeDetail_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property ShapeClassName As String
    Public Property Vectors As List(Of HkxVector4Graph_Class)
    Public Property EndpointA As HkxVector4Graph_Class
    Public Property EndpointB As HkxVector4Graph_Class
    Public Property AxisHint As HkxVector4Graph_Class
    Public Property ParameterVector As HkxVector4Graph_Class
    Public Property Radius As Single
    Public Property AuxiliaryRadius As Single
    Public Property SegmentLength As Single
    Public Property TaperFactor As Single
    Public Property TaperCosine As Single
    Public Property ExtraScalar0 As Single
    Public Property ExtraScalar1 As Single
    Public Property ExtraScalar2 As Single
    Public Property ExtraVector0 As HkxVector4Graph_Class
    Public Property ExtraVector1 As HkxVector4Graph_Class
End Class

Public Class HclVolumeConstraintBridgeSlot_Class
    Public Property TargetSlot As HclVolumeConstraintQuadSlot_Class
    Public Property FirstSourceSlot As HclVolumeConstraintQuadSlot_Class
    Public Property SecondSourceSlot As HclVolumeConstraintQuadSlot_Class
    Public ReadOnly Property SharedParticlesFirst As New List(Of Integer)
    Public ReadOnly Property SharedParticlesSecond As New List(Of Integer)
    Public ReadOnly Property OuterParticlesFirst As New List(Of Integer)
    Public ReadOnly Property OuterParticlesSecond As New List(Of Integer)
    Public ReadOnly Property BridgeParticles As New List(Of Integer)
End Class

Public Class HclStandardLinkConstraintSetDetail_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Name As String
    Public Property Links As List(Of HkxRawStructGraph_Class)
    Public ReadOnly Property LinkDetails As New List(Of HclDistanceConstraintGraph_Class)
End Class

Public Class HclStretchLinkConstraintSetDetail_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Name As String
    Public Property Links As List(Of HkxRawStructGraph_Class)
    Public ReadOnly Property LinkDetails As New List(Of HclDistanceConstraintGraph_Class)
End Class

Public Class HclVolumeConstraintQuadSlot_Class
    Public Property RawStructEntryIndex As Integer
    Public Property SlotIndex As Integer
    Public Property ByteOffset As Integer
    Public Property ParticleA As Integer
    Public Property ParticleB As Integer
    Public Property ParticleC As Integer
    Public Property ParticleD As Integer
    Public ReadOnly Property Particles As New List(Of Integer)
    Public Property IsAllZero As Boolean
End Class

Public Class HclVolumeConstraintVectorEntry_Class
    Public Property EntryIndex As Integer
    Public Property Pivot As HkxVector4Graph_Class
    Public Property Parameters As HkxVector4Graph_Class
End Class

Public Class HclVolumeConstraintPivotMatch_Class
    Public Property EntryIndex As Integer
    Public Property MatchedEntryIndex As Integer
End Class

Public Class HclVolumeConstraintLane_Class
    Public Property LaneIndex As Integer
    Public Property QuadSlot As HclVolumeConstraintQuadSlot_Class
    Public Property ParameterVector As HkxVector4Graph_Class
    Public ReadOnly Property CoefficientVectors As New List(Of HkxVector4Graph_Class)
End Class

Public Class HclVolumeConstraintBatch_Class
    Public Property EntryIndex As Integer
    Public Property RawStruct As HkxRawStructGraph_Class
    Public Property VectorBlock As HkxVectorStructBlockGraph_Class
    Public ReadOnly Property AllVectors As New List(Of HkxVector4Graph_Class)
    Public ReadOnly Property PreQuadVectors As New List(Of HkxVector4Graph_Class)
    Public ReadOnly Property MidVectors As New List(Of HkxVector4Graph_Class)
    Public ReadOnly Property PostQuadVectors As New List(Of HkxVector4Graph_Class)
    Public Property MidVectorsLookZeroish As Boolean
    Public Property UniformLaneParameter As Single?
    Public Property LaneParameterIsUniform As Boolean
    Public ReadOnly Property QuadSlots As New List(Of HclVolumeConstraintQuadSlot_Class)
    Public ReadOnly Property Lanes As New List(Of HclVolumeConstraintLane_Class)
End Class

Public Class HclBendStiffnessConstraintSetDetail_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Name As String
    Public Property Links As List(Of HkxRawStructGraph_Class)
    Public ReadOnly Property LinkDetails As New List(Of HclBendConstraintGraph_Class)
    Public Property ResolvedTopologyCount As Integer
    Public Property ResolvedRestGeometryCount As Integer
    Public Property SignedUnitCount As Integer
    Public Property OppOppEdgeEdgeOrderCount As Integer
    Public Property AverageRestEdgeLength As Single?
    Public Property AverageAbsRestCurvatureMinusDihedralOverEdge As Single?
End Class

Public Class HclLocalRangeConstraintSetDetail_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Name As String
    Public Property Constraints As List(Of HkxRawStructGraph_Class)
    Public ReadOnly Property ConstraintDetails As New List(Of HclLocalRangeConstraintGraph_Class)
    Public Property UniformMaximumDistance As Single?
    Public Property UniformMaximumNormalDistance As Single?
    Public Property UniformMinimumNormalDistance As Single?
    Public Property DistinctParticleCount As Integer
    Public Property DistinctReferenceVertexCount As Integer
    Public Property ParticleReferenceIdentityCount As Integer
End Class

Public Class HclVolumeConstraintMxDetail_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Name As String
    Public Property Field20RawStructs As List(Of HkxRawStructGraph_Class)
    Public Property Field30RawStructs As List(Of HkxRawStructGraph_Class)
    Public Property Field40RawStructs As List(Of HkxRawStructGraph_Class)
    Public Property Field50RawStructs As List(Of HkxRawStructGraph_Class)
    Public Property Field20VectorBlocks As List(Of HkxVectorStructBlockGraph_Class)
    Public Property Field30VectorBlocks As List(Of HkxVectorStructBlockGraph_Class)
    Public Property Field40VectorBlocks As List(Of HkxVectorStructBlockGraph_Class)
    Public Property Field50VectorBlocks As List(Of HkxVectorStructBlockGraph_Class)
    Public ReadOnly Property Field20Batches As New List(Of HclVolumeConstraintBatch_Class)
    Public ReadOnly Property Field20QuadSlots As New List(Of HclVolumeConstraintQuadSlot_Class)
    Public ReadOnly Property Field30Entries As New List(Of HclVolumeConstraintVectorEntry_Class)
    Public ReadOnly Property Field40Batches As New List(Of HclVolumeConstraintBatch_Class)
    Public ReadOnly Property Field40QuadSlots As New List(Of HclVolumeConstraintQuadSlot_Class)
    Public ReadOnly Property Field40BridgeSlots As New List(Of HclVolumeConstraintBridgeSlot_Class)
    Public ReadOnly Property Field40TerminalQuadSlots As New List(Of HclVolumeConstraintQuadSlot_Class)
    Public ReadOnly Property Field20BridgeSourceQuadSlots As New List(Of HclVolumeConstraintQuadSlot_Class)
    Public ReadOnly Property Field20NonBridgeQuadSlots As New List(Of HclVolumeConstraintQuadSlot_Class)
    Public ReadOnly Property Field40BridgeSourceChain As New List(Of HclVolumeConstraintQuadSlot_Class)
    Public ReadOnly Property Field30ParameterValues As New List(Of Single)
    Public ReadOnly Property Field50ParameterValues As New List(Of Single)
    Public ReadOnly Property Field50ToField30PivotMatches As New List(Of HclVolumeConstraintPivotMatch_Class)
    Public Property Field20MidVectorsLookZeroish As Boolean
    Public Property Field40MidVectorsLookZeroish As Boolean
    Public Property Field20BatchUniformParameter As Single?
    Public Property Field40BatchUniformParameter As Single?
    Public Property Field20LaneParametersUniformAcrossBatches As Boolean
    Public Property Field40LaneParametersUniformAcrossBatches As Boolean
    Public Property Field30UniformParameter As Single?
    Public Property Field50UniformParameter As Single?
    Public Property Field20BatchParameterMatchesField30Parameter As Boolean
    Public Property Field40BatchParameterMatchesField50Parameter As Boolean
    Public Property Field20AndField40ParametersDistinct As Boolean
    Public Property HasDistinctParameterGroups As Boolean
    Public Property Field50PivotReuseOffset As Integer?
    Public Property Field50PivotReuseCount As Integer
    Public Property Field40BridgeCountMatchesField50Count As Boolean
    Public Property Field40BridgeSlotsExact As Boolean
    Public Property Field40BridgeFormsSequentialChain As Boolean
    Public Property Field40TerminalExtendsBridgeChain As Boolean
    Public Property Field40TerminalSharedParticleCount As Integer
    Public Property Field40TerminalAddedParticleCount As Integer
    Public Property Field40BridgeSourceChainCount As Integer
    Public Property Field30LeadCountMatchesField20ExtraActiveQuadCount As Boolean
    Public Property Field30TailCountMatchesField40BridgeSourceChainCount As Boolean
    Public Property Field50EntryCountMatchesField40BridgeSourceChainCount As Boolean
    Public Property Field40NonZeroQuadCount As Integer
    Public Property Field40ExactBridgeCount As Integer
    Public Property Field50PivotTailStartIndex As Integer?
    Public Property Field50MatchesField30Tail As Boolean
    Public Property Field20NonZeroQuadCount As Integer
    Public Property Field20NonZeroQuadCountMatchesField30Count As Boolean
    Public Property Field40NonZeroQuadCountMatchesField50Count As Boolean
    Public Property Field30LeadEntryCount As Integer
    Public Property Field30TailEntryCount As Integer
    Public Property Field40TerminalQuadCount As Integer
    Public Property Field20ExtraActiveQuadCount As Integer
    Public Property Field20BridgeSourceQuadCount As Integer
    Public Property Field20NonBridgeQuadCount As Integer
    Public Property Field50TailSourceEntryCount As Integer
    Public Property Field20BridgeSourceAndNonBridgePartitionMatchesActiveQuads As Boolean
    Public Property Field40BridgeAndTerminalPartitionMatchesActiveQuads As Boolean
    Public Property Field40BridgeSourceChainMatchesField20BridgeSourceCount As Boolean
    Public Property Field50TailSourceCountMatchesField50EntryCount As Boolean
    Public Property Field50TailSourceCountMatchesField30TailEntryCount As Boolean
    Public ReadOnly Property Field50Entries As New List(Of HclVolumeConstraintVectorEntry_Class)
    Public ReadOnly Property Field30LeadEntries As New List(Of HclVolumeConstraintVectorEntry_Class)
    Public ReadOnly Property Field30TailEntries As New List(Of HclVolumeConstraintVectorEntry_Class)
    Public ReadOnly Property Field50TailSourceEntries As New List(Of HclVolumeConstraintVectorEntry_Class)
End Class

Public Class HkxVectorStructBlockGraph_Class
    Public Property EntryIndex As Integer
    Public Property EntryRelativeOffset As Integer
    Public Property Vectors As List(Of HkxVector4Graph_Class)
End Class


Public Class HclSimParticleDataGraph_Class
    Public Property EntryIndex As Integer
    Public Property Mass As Single
    Public Property InverseMass As Single
    Public Property Radius As Single
    Public Property Friction As Single
End Class

Public Class HclDistanceConstraintGraph_Class
    Public Property EntryIndex As Integer
    Public Property RawStruct As HkxRawStructGraph_Class
    Public Property ParticleA As UShort
    Public Property ParticleB As UShort
    Public Property RestLength As Single
    Public Property Stiffness As Single
End Class

Public Class HclBendConstraintGraph_Class
    Public Property EntryIndex As Integer
    Public Property RawStruct As HkxRawStructGraph_Class
    Public Property WeightA As Single
    Public Property WeightB As Single
    Public Property WeightC As Single
    Public Property WeightD As Single
    Public Property ParticleA As UShort
    Public Property ParticleB As UShort
    Public Property ParticleC As UShort
    Public Property ParticleD As UShort
    Public Property BendStiffness As Single
    Public Property RestCurvature As Single
    Public Property WeightSum As Single
    Public Property HasZeroWeightSum As Boolean
    Public Property SharedEdgeParticleA As Integer = -1
    Public Property SharedEdgeParticleB As Integer = -1
    Public Property OppositeParticleA As Integer = -1
    Public Property OppositeParticleB As Integer = -1
    Public Property TriangleIndexA As Integer = -1
    Public Property TriangleIndexB As Integer = -1
    Public Property HasResolvedTopology As Boolean
    Public Property PositiveWeightPairSum As Single
    Public Property NegativeWeightPairSum As Single
    Public Property FirstPairFormsUnit As Boolean
    Public Property SecondPairFormsNegativeUnit As Boolean
    Public Property WeightPairsFormSignedUnit As Boolean
    Public Property ParticleOrderMatchesOppOppEdgeEdge As Boolean
    Public Property HasResolvedRestGeometry As Boolean
    Public Property RestEdgeLength As Single
    Public Property RestDihedralAngle As Single
    Public Property RestDihedralOverEdge As Single
    Public Property RestCurvatureMinusDihedral As Single
    Public Property RestCurvatureMinusDihedralOverEdge As Single
End Class

Public Class HclLocalRangeConstraintGraph_Class
    Public Property EntryIndex As Integer
    Public Property RawStruct As HkxRawStructGraph_Class
    Public Property ParticleIndex As UShort
    Public Property ReferenceVertexIndex As UShort
    Public Property MaximumDistance As Single
    Public Property MaximumNormalDistance As Single
    Public Property MinimumNormalDistance As Single
End Class

Public Class HkxUInt16TriangleGraph_Class
    Public Property TriangleIndex As Integer
    Public Property Value0 As UShort
    Public Property Value1 As UShort
    Public Property Value2 As UShort
End Class

Public Class HkxUInt32PairGraph_Class
    Public Property EntryIndex As Integer
    Public Property EntryRelativeOffset As Integer
    Public Property FirstValue As UInteger
    Public Property SecondValue As UInteger
End Class

Public Class HkxRawStructGraph_Class
    Public Property EntryIndex As Integer
    Public Property EntryRelativeOffset As Integer
    Public Property RawBytes As Byte()
    Public ReadOnly Property UInt16Values As New List(Of UShort)
    Public ReadOnly Property UInt32Values As New List(Of UInteger)
    Public ReadOnly Property SingleValues As New List(Of Single)
End Class












