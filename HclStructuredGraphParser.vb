' Version Uploaded of Fo4Library 3.2.0
Option Strict On
Option Explicit On

' =============================================================================
' Parseo de estructuras HCL (Havok Cloth): SimClothData, collidables, capsules,
' operadores (MoveParticles, Simulate, CopyVertices, etc.), cloth states.
' Llamado desde HclClothPackageParser_Class.
'
' ⛔ 2026-08-22 — LOS OFFSETS YA NO SE ADIVINAN NI SE ESCRIBEN A MANO.
' Salen de `Havok.Canon.HavokLayout`, generado desde la reflexión hkClass/hkClassMember que el
' propio ejecutable del juego embebe (Tools/HavokLayoutGen). La tabla se elige por el formato que
' el packfile DECLARA en su header (Fallout64 / Skyrim64 / Skyrim32) ⇒ game-aware por construcción.
' Skyrim32 no tiene tabla (la tabla describe x64) y estas funciones devuelven Nothing en vez de
' inventar números.
'
' hclSimClothData — layout AUTORITATIVO (size 0x180, ver 11):
'   +0x010 simulationInfo  {gravity@+0x00, globalDampingPerSecond@+0x10, collisionTolerance@+0x14,
'                           subSteps@+0x18, pinch/landscape/transferMotion bools @+0x1C..+0x1E}
'   +0x030 name (stringptr)          ⛔ SÍ existe. El comentario viejo decía que era el hkArray de
'                                       m_collidableTransformIndices: era FALSO.
'   +0x038 particleDatas = array de {real mass; real invMass; real radius; real friction} (16 B)
'                                    ⛔ NO es "xyz=posición, w=invMass": son CUATRO escalares.
'   +0x048 fixedParticles (uint16)   +0x058 triangleIndices (uint16)
'   +0x068 triangleFlips (uint8)     ⛔ era "m_unknown68, tipo desconocido"
'   +0x078 totalMass
'   +0x080 collidableTransformMap { transformSetIndex@+0x00, transformIndices@+0x08 (uint32),
'                                   offsets@+0x18 (matrix4) }   ⛔ +0x88 y +0x98 no eran campos
'                                   sueltos ("m_unknown88" / "m_collidableTransforms"): son ESTOS DOS.
'   +0x0A8 perInstanceCollidables    +0x0B8 staticConstraintSets   +0x0C8 antiPinchConstraintSets
'   +0x0D8 simClothPoses             +0x0E8 actions (viento)       +0x0F8 staticCollisionMasks
'   +0x108 perParticlePinchDetectionEnabledFlags (bool[])  — confirma el stride=1 que se había medido
'   +0x118 collidablePinchingDatas   +0x130 maxParticleRadius      +0x14C doNormals
'   +0x134 landscapeCollisionData    +0x150 transferMotionData
'
' hclCollidable (size 0x90, ver 3): name@+0x10, PADDING +0x18..+0x1F, transform@+0x20 (hkTransform:
'   3 filas de rotación + traslación en +0x50 con w=1), linearVelocity@+0x60, angularVelocity@+0x70,
'   pinchDetection{Enabled,Priority,Radius}@+0x80/+0x81/+0x84, shape@+0x88.
'   ⛔ Este parser leía desde +0x18 y TODO salía corrido 8 bytes. Ver ParseCollidable.
' =============================================================================

Imports System.Collections.Generic
Imports System.Linq

Friend NotInheritable Class HclStructuredGraphParser_Class
    Friend Shared Function ParseSimClothData(graph As HkxObjectGraph_Class, source As HkxVirtualObjectGraph_Class,
                                             Optional collidableCache As Dictionary(Of Integer, HclCollidableDetail_Class) = Nothing) As HclSimClothDataDetail_Class
        If IsNothing(graph) OrElse IsNothing(source) Then Return Nothing
        If Not source.ClassName.Equals("hclSimClothData", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        ' Lector generado (HavokTyped.vb): los offsets salen de la reflexion de los dos
        ' .exe y la tabla la elige el packfile. Sin literales que se puedan desincronizar.
        Dim r As New Havok.Canon.Typed.Hk_HclSimClothData(graph, source)
        If Not r.IsValid Then Return Nothing

        Const cls = "hclSimClothData"
        ' Guard por CLASE: Skyrim32 no tiene tabla, y la tabla de Skyrim64 no declara clases hcl.
        Dim layout = CanonLayoutOf(graph)
        If layout Is Nothing OrElse Not layout.HasClass(cls) Then Return Nothing
        Dim rel = source.RelativeOffset
        Dim Off = Function(path As String) rel + layout.RequireOffset(cls, path)

        Dim collidableObjects = graph.ReadObjectReferenceArray(Off("perInstanceCollidables"))
        Dim constraintObjects = graph.ReadObjectReferenceArray(Off("staticConstraintSets"))
        Dim defaultPoseObjects = graph.ReadObjectReferenceArray(Off("simClothPoses"))
        Dim actionObjects = graph.ReadObjectReferenceArray(Off("actions"))

        Dim result As New HclSimClothDataDetail_Class With {
            .SourceObject = source,
            .Name = graph.ResolveLocalString(Off("name")),
            .Field38Vectors = ReadVector4Array(graph, graph.ReadArrayHeader(Off("particleDatas"))),
            .Field48UInt16 = ReadUInt16Array(graph, graph.ReadArrayHeader(Off("fixedParticles"))),
            .Field58UInt16 = ReadUInt16Array(graph, graph.ReadArrayHeader(Off("triangleIndices"))),
            .Field88UInt32 = ReadUInt32Array(graph, graph.ReadArrayHeader(Off("collidableTransformMap.transformIndices"))),
            .Field98Matrices = ReadMatrix4Array(graph, graph.ReadArrayHeader(Off("collidableTransformMap.offsets"))),
            .Collidables = collidableObjects,
            .ConstraintSets = constraintObjects,
            .DefaultClothPoses = defaultPoseObjects,
            .Actions = actionObjects,
            .FieldF8UInt32 = ReadUInt32Array(graph, graph.ReadArrayHeader(Off("staticCollisionMasks"))),
            .Field118Pairs = ReadUInt32PairArray(graph, graph.ReadArrayHeader(Off("collidablePinchingDatas"))),
            .CollidableTransformSetIndex = graph.ReadInt32(Off("collidableTransformMap.transformSetIndex"))
        }

        ' ---- hclSimClothDataOverridableSimulationInfo (+0x10): los PARÁMETROS DE LA SIMULACIÓN ----
        ' El motor los usa así (RE de hclSimulateOperator::execute @0x14195C350 y 0x1418C75B0):
        '   a = (mass·gravity + F)·invMass ;  v = (P-Pprev)·damp ;  P' = P + v + a·dtSub²
        '   damp = si d>=1 -> 0 ; si d=0 -> 1 ; si no -> (1-d)^dtRef      con d = globalDampingPerSecond
        '   dtSub = dt / subSteps  (si subSteps=0 el motor cae al subSteps del hclSimulateOperator)
        result.Gravity = ReadVector4At(graph, Off("simulationInfo.gravity"))
        result.GlobalDampingPerSecond = graph.ReadSingle(Off("simulationInfo.globalDampingPerSecond"))
        result.CollisionTolerance = graph.ReadSingle(Off("simulationInfo.collisionTolerance"))
        result.SubSteps = graph.ReadInt32(Off("simulationInfo.subSteps"))
        result.PinchDetectionEnabled = graph.ReadByte(Off("simulationInfo.pinchDetectionEnabled")) <> 0
        result.LandscapeCollisionEnabled = graph.ReadByte(Off("simulationInfo.landscapeCollisionEnabled")) <> 0
        result.TransferMotionEnabled = graph.ReadByte(Off("simulationInfo.transferMotionEnabled")) <> 0
        result.TotalMass = graph.ReadSingle(Off("totalMass"))
        result.MaxParticleRadius = graph.ReadSingle(Off("maxParticleRadius"))
        result.MaxCollisionPairs = graph.ReadInt32(Off("maxCollisionPairs"))
        result.DoNormals = graph.ReadByte(Off("doNormals")) <> 0
        result.NumLandscapeCollidableParticles = graph.ReadInt32(Off("numLandscapeCollidableParticles"))
        result.LandscapeRadius = graph.ReadSingle(Off("landscapeCollisionData.landscapeRadius"))
        result.EnableStuckParticleDetection = graph.ReadByte(Off("landscapeCollisionData.enableStuckParticleDetection")) <> 0
        result.StuckParticlesStretchFactorSq = graph.ReadSingle(Off("landscapeCollisionData.stuckParticlesStretchFactorSq"))
        result.LandscapePinchDetectionEnabled = graph.ReadByte(Off("landscapeCollisionData.pinchDetectionEnabled")) <> 0
        result.LandscapePinchPriority = CInt(CSByte(graph.ReadByte(Off("landscapeCollisionData.pinchDetectionPriority"))))
        result.LandscapePinchRadius = graph.ReadSingle(Off("landscapeCollisionData.pinchDetectionRadius"))
        result.TransferMotionTransformSetIndex = graph.ReadInt32(Off("transferMotionData.transformSetIndex"))
        result.TransferMotionTransformIndex = graph.ReadInt32(Off("transferMotionData.transformIndex"))
        result.TransferTranslationMotion = graph.ReadByte(Off("transferMotionData.transferTranslationMotion")) <> 0
        result.TransferRotationMotion = graph.ReadByte(Off("transferMotionData.transferRotationMotion")) <> 0
        result.MinTranslationSpeed = graph.ReadSingle(Off("transferMotionData.minTranslationSpeed"))
        result.MaxTranslationSpeed = graph.ReadSingle(Off("transferMotionData.maxTranslationSpeed"))
        result.MinTranslationBlend = graph.ReadSingle(Off("transferMotionData.minTranslationBlend"))
        result.MaxTranslationBlend = graph.ReadSingle(Off("transferMotionData.maxTranslationBlend"))
        result.MinRotationSpeed = graph.ReadSingle(Off("transferMotionData.minRotationSpeed"))
        result.MaxRotationSpeed = graph.ReadSingle(Off("transferMotionData.maxRotationSpeed"))
        result.MinRotationBlend = graph.ReadSingle(Off("transferMotionData.minRotationBlend"))
        result.MaxRotationBlend = graph.ReadSingle(Off("transferMotionData.maxRotationBlend"))

        result.ParticleDatas.AddRange(ParseSimParticleData(result.Field38Vectors))
        result.FixedParticleIndices.AddRange(result.Field48UInt16.Select(Function(value) CInt(value)))
        result.Triangles.AddRange(ReadUInt16TriangleArray(result.Field58UInt16))
        result.StaticCollisionMasks.AddRange(result.FieldF8UInt32)
        result.TriangleFlips.AddRange(ReadByteArray(graph, graph.ReadArrayHeader(Off("triangleFlips"))))
        result.AntiPinchConstraintSets = graph.ReadObjectReferenceArray(Off("antiPinchConstraintSets"))
        If result.AntiPinchConstraintSets IsNot Nothing Then
            result.AntiPinchDetails.AddRange(result.AntiPinchConstraintSets.
                Select(Function(obj) ParseConstraintObject(graph, obj)).Where(Function(d) Not IsNothing(d)))
        End If
        ' `perParticlePinchDetectionEnabledFlags` (+0x108, array of bool): el stride=1 que se había MEDIDO
        ' sobre 276 hclSimClothData (Tools/PinchStrideProbe) queda CONFIRMADO por el tipo declarado.
        result.PinchDetectionFlags.AddRange(ReadByteArray(graph, graph.ReadArrayHeader(Off("perParticlePinchDetectionEnabledFlags"))))
        result.MinPinchedParticleIndex = U16(graph, Off("minPinchedParticleIndex"))
        result.MaxPinchedParticleIndex = U16(graph, Off("maxPinchedParticleIndex"))
        Dim hPinch = graph.ReadArrayHeader(Off("collidablePinchingDatas"))
        If hPinch IsNot Nothing Then
            For iP = 0 To hPinch.Count - 1
                Dim o = hPinch.DataRelativeOffset + iP * 8
                result.CollidablePinchingDatas.Add(New HclCollidablePinchingData_Class With {
                    .Enabled = graph.ReadByte(o) <> 0,
                    .Priority = CInt(CSByte(graph.ReadByte(o + 1))),
                    .Radius = graph.ReadSingle(o + 4)})
            Next
        End If
        result.ActionDetails.AddRange(actionObjects.Select(Function(obj) ParseActionObject(graph, obj)).Where(Function(d) Not IsNothing(d)))
        result.CollidableDetails.AddRange(collidableObjects.Select(Function(obj) ParseCollidable(graph, obj, collidableCache)).Where(Function(detail) Not IsNothing(detail)))
        result.DefaultClothPoseDetails.AddRange(defaultPoseObjects.Select(Function(obj) graph.ParseSimClothPose(obj)).Where(Function(detail) Not IsNothing(detail)))
        result.ConstraintDetails.AddRange(constraintObjects.Select(Function(obj) ParseConstraintObject(graph, obj)).Where(Function(detail) Not IsNothing(detail)))
        Return result
    End Function

    Friend Shared Function ParseClothState(graph As HkxObjectGraph_Class, source As HkxVirtualObjectGraph_Class) As HclClothStateDetail_Class
        If IsNothing(graph) OrElse IsNothing(source) Then Return Nothing
        If Not source.ClassName.Equals("hclClothState", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        ' Lector generado (HavokTyped.vb): los offsets salen de la reflexion de los dos
        ' .exe y la tabla la elige el packfile. Sin literales que se puedan desincronizar.
        Dim r As New Havok.Canon.Typed.Hk_HclClothState(graph, source)
        If Not r.IsValid Then Return Nothing

        Dim result As New HclClothStateDetail_Class With {
            .SourceObject = source,
            .Name = r.Name,
            .Field18UInt32 = ReadUInt32Array(graph, r.Operators),
            .Field28Vectors = ReadVector4Array(graph, r.UsedBuffers),
            .Field48Vectors = ReadVector4Array(graph, r.UsedSimCloths)
        }

        result.OperatorIndices.AddRange(result.Field18UInt32.Select(Function(value) CInt(value)))
        result.BufferAccesses.AddRange(ParseStateBufferAccessArray(graph, r.UsedBuffers))
        result.AuxiliaryBufferAccesses.AddRange(ParseStateBufferAccessArray(graph, r.UsedSimCloths))
        result.TransformAccessContainers.AddRange(ParseStateTransformAccessContainerArray(graph, r.UsedTransformSets))
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
            .NestedAccessHeader = nestedHeader
        }

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
            .EntryRelativeOffset = raw.EntryRelativeOffset
        }

        For subIndex = 0 To 2
            Dim componentAccess = ParseStateTransformComponentAccess(graph, raw, subIndex)
            If Not IsNothing(componentAccess) Then result.ComponentAccesses.Add(componentAccess)
        Next

        result.HasAnyMaskData = result.ComponentAccesses.Any(Function(access) access.MaskIndices.Any())
        Return result
    End Function

    Private Shared Function ParseStateTransformComponentAccess(graph As HkxObjectGraph_Class, raw As HkxRawStructGraph_Class, subIndex As Integer) As HclClothStateTransformComponentAccessDetail_Class
        If IsNothing(graph) OrElse IsNothing(raw) Then Return Nothing
        If subIndex < 0 OrElse subIndex > 2 Then Return Nothing

        Dim wordBase = subIndex * 6
        Dim headerOffset = raw.EntryRelativeOffset + (subIndex * 24)
        Dim header = graph.ReadArrayHeader(headerOffset)

        Dim maskBytes = ReadByteArray(graph, header)   ' local: se consume para MaskIndices, no se guarda crudo
        Dim result As New HclClothStateTransformComponentAccessDetail_Class With {
            .SubIndex = subIndex,
            .HeaderRelativeOffset = headerOffset,
            .ArrayHeader = header,
            .MaskCount = header.Count,
            .CapacityAndFlags = header.CapacityAndFlags,
            .TransformCount = If(raw.UInt32Values.Count > wordBase + 4, CInt(raw.UInt32Values(wordBase + 4)), 0),
            .ReservedValue = If(raw.UInt32Values.Count > wordBase + 5, raw.UInt32Values(wordBase + 5), 0UI)
        }

        result.MaskIndices.AddRange(DecodeMaskIndices(maskBytes))
        Return result
    End Function

    Friend Shared Function ParseBufferDefinition(graph As HkxObjectGraph_Class, source As HkxVirtualObjectGraph_Class) As HclBufferDefinitionDetail_Class
        If IsNothing(graph) OrElse IsNothing(source) Then Return Nothing
        If Not source.ClassName.Equals("hclBufferDefinition", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        ' Lector generado (HavokTyped.vb): los offsets salen de la reflexion de los dos
        ' .exe y la tabla la elige el packfile. Sin literales que se puedan desincronizar.
        Dim r As New Havok.Canon.Typed.Hk_HclBufferDefinition(graph, source)
        If Not r.IsValid Then Return Nothing

        Dim payloadUInt32 = ReadPayloadUInt32(graph, source, &H20)
        Return New HclBufferDefinitionDetail_Class With {
            .SourceObject = source,
            .Name = r.Name,
            .PayloadUInt32 = payloadUInt32,
            .ParticleCount = If(payloadUInt32.Count > 0, CInt(payloadUInt32(0)), 0),
            .TriangleCount = If(payloadUInt32.Count > 1, CInt(payloadUInt32(1)), 0)
        }
    End Function

    Friend Shared Function ParseScratchBufferDefinition(graph As HkxObjectGraph_Class, source As HkxVirtualObjectGraph_Class) As HclScratchBufferDefinitionDetail_Class
        If IsNothing(graph) OrElse IsNothing(source) Then Return Nothing
        If Not source.ClassName.Equals("hclScratchBufferDefinition", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        ' Lector generado (HavokTyped.vb): los offsets salen de la reflexion de los dos
        ' .exe y la tabla la elige el packfile. Sin literales que se puedan desincronizar.
        Dim r As New Havok.Canon.Typed.Hk_HclScratchBufferDefinition(graph, source)
        If Not r.IsValid Then Return Nothing

        Dim payloadUInt32 = ReadPayloadUInt32(graph, source, &H20)
        Return New HclScratchBufferDefinitionDetail_Class With {
            .SourceObject = source,
            .Name = r.Name,
            .PayloadUInt32 = payloadUInt32,
            .ParticleCount = If(payloadUInt32.Count > 0, CInt(payloadUInt32(0)), 0),
            .TriangleCount = If(payloadUInt32.Count > 1, CInt(payloadUInt32(1)), 0)
        }
    End Function

    Friend Shared Function ParseMoveParticlesOperator(graph As HkxObjectGraph_Class, source As HkxVirtualObjectGraph_Class) As HclMoveParticlesOperatorDetail_Class
        If IsNothing(graph) OrElse IsNothing(source) Then Return Nothing
        If Not source.ClassName.Equals("hclMoveParticlesOperator", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        ' Lector generado (HavokTyped.vb): los offsets salen de la reflexion de los dos
        ' .exe y la tabla la elige el packfile. Sin literales que se puedan desincronizar.
        Dim r As New Havok.Canon.Typed.Hk_HclMoveParticlesOperator(graph, source)
        If Not r.IsValid Then Return Nothing

        Return New HclMoveParticlesOperatorDetail_Class With {
            .SourceObject = source,
            .Name = r.Name,
            .RefBufferIdx = CInt(r.RefBufferIdx),
            .SimClothIndex = CInt(r.SimClothIndex),
            .Pairs = ReadVertexParticlePairs(graph, r.VertexParticlePairs)
        }
    End Function

    Friend Shared Function ParseSimulateOperator(graph As HkxObjectGraph_Class, source As HkxVirtualObjectGraph_Class) As HclSimulateOperatorDetail_Class
        If IsNothing(graph) OrElse IsNothing(source) Then Return Nothing
        If Not source.ClassName.Equals("hclSimulateOperator", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        ' Lector generado (HavokTyped.vb): los offsets salen de la reflexion de los dos
        ' .exe y la tabla la elige el packfile. Sin literales que se puedan desincronizar.
        Dim r As New Havok.Canon.Typed.Hk_HclSimulateOperator(graph, source)
        If Not r.IsValid Then Return Nothing

        Dim header = ReadUInt32Block(graph, source.RelativeOffset + &H18, 6)
        ' ⛔ Los enteros CRUDOS del operador. `subSteps` (+0x24) y `numberOfSolveIterations` (+0x28) son
        ' lo unico que decide cuanto trabajo hace el solver por frame; leerlos corridos un slot cambia
        ' la fisica entera y no se nota en ningun otro lado.
        If Logger.Enabled Then
            Dim h = header
            Logger.LogLazy(Function() $"[CLOTH-OPHDR] hclSimulateOperator +0x18..+0x2C = {String.Join(" ", h)}  (+0x20=simClothIndex +0x24=subSteps +0x28=iters)")
        End If
        Return New HclSimulateOperatorDetail_Class With {
            .SourceObject = source,
            .Name = r.Name,
            .SubstepCount = If(header.Count > 3, CInt(header(3)), 0),
            .SolveIterationCount = If(header.Count > 4, CInt(header(4)), 0),
            .AdaptConstraintStiffness = r.AdaptConstraintStiffness,
            .Configs = ReadUInt32ConfigArray(graph, r.ConstraintExecution)
        }
    End Function

    Friend Shared Function ParseCopyVerticesOperator(graph As HkxObjectGraph_Class, source As HkxVirtualObjectGraph_Class) As HclCopyVerticesOperatorDetail_Class
        If IsNothing(graph) OrElse IsNothing(source) Then Return Nothing
        If Not source.ClassName.Equals("hclCopyVerticesOperator", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        ' Lector generado (HavokTyped.vb): los offsets salen de la reflexion de los dos
        ' .exe y la tabla la elige el packfile. Sin literales que se puedan desincronizar.
        Dim r As New Havok.Canon.Typed.Hk_HclCopyVerticesOperator(graph, source)
        If Not r.IsValid Then Return Nothing

        Dim payloadBytes = ReadPayloadBytes(graph, source, &H20)
        Dim payloadUInt32 = ReadPayloadUInt32(graph, source, &H20)
        Return New HclCopyVerticesOperatorDetail_Class With {
            .SourceObject = source,
            .Name = r.Name,
            .PayloadUInt32 = payloadUInt32,
            .ElementCount = If(payloadUInt32.Count > 2, CInt(payloadUInt32(2)), 0),
            .PayloadAsciiTag = ExtractPrintableAscii(payloadBytes),
            .NumberOfVertices = CInt(r.NumberOfVertices),
            .StartVertexIn = CInt(r.StartVertexIn),
            .StartVertexOut = CInt(r.StartVertexOut),
            .InputBufferIdx = CInt(r.InputBufferIdx),
            .OutputBufferIdx = CInt(r.OutputBufferIdx),
            .CopyNormals = r.CopyNormals
        }
    End Function

    Friend Shared Function ParseGatherAllVerticesOperator(graph As HkxObjectGraph_Class, source As HkxVirtualObjectGraph_Class) As HclGatherAllVerticesOperatorDetail_Class
        If IsNothing(graph) OrElse IsNothing(source) Then Return Nothing
        If Not source.ClassName.Equals("hclGatherAllVerticesOperator", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        ' Lector generado (HavokTyped.vb): los offsets salen de la reflexion de los dos
        ' .exe y la tabla la elige el packfile. Sin literales que se puedan desincronizar.
        Dim r As New Havok.Canon.Typed.Hk_HclGatherAllVerticesOperator(graph, source)
        If Not r.IsValid Then Return Nothing

        Dim payloadBytes = ReadPayloadBytes(graph, source, &H20)
        Dim payloadUInt32 = ReadPayloadUInt32(graph, source, &H20)
        Return New HclGatherAllVerticesOperatorDetail_Class With {
            .SourceObject = source,
            .Name = r.Name,
            .PayloadUInt32 = payloadUInt32,
            .ElementCount = If(payloadUInt32.Count > 2, CInt(payloadUInt32(2)), 0),
            .GatheredVertexIndices = DecodePackedUInt16List(payloadUInt32.Skip(12), If(payloadUInt32.Count > 2, CInt(payloadUInt32(2)), 0)),
            .PayloadAsciiTag = ExtractPrintableAscii(payloadBytes),
            .InputBufferIdx = CInt(r.InputBufferIdx),
            .OutputBufferIdx = CInt(r.OutputBufferIdx),
            .GatherNormals = r.GatherNormals,
            .PartialGather = r.PartialGather
        }
    End Function

    ' uint16 sin signo desde el grafo.
    Private Shared Function U16(graph As HkxObjectGraph_Class, relativeOffset As Integer) As Integer
        Return CInt(graph.ReadInt16(relativeOffset)) And &HFFFF
    End Function

    ' --- Cloth-menores: layouts {name@+0x10, hkArray principal@+0x20}, structs verificados por --dump
    '     (multi-elemento) sobre DC Guard / Residents 6Suit / Institute Lab Coat. Todo a campos tipados.

    ' hclBendLinkConstraintSet — stride 20: {u16 particleA, u16 particleB, 4×float}. (DC Guard)
    Friend Shared Function ParseBendLinkConstraintSet(graph As HkxObjectGraph_Class, source As HkxVirtualObjectGraph_Class) As HclBendLinkConstraintSetDetail_Class
        If IsNothing(graph) OrElse IsNothing(source) OrElse Not source.ClassName.Equals("hclBendLinkConstraintSet", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        ' Lector generado (HavokTyped.vb): los offsets salen de la reflexion de los dos
        ' .exe y la tabla la elige el packfile. Sin literales que se puedan desincronizar.
        Dim r As New Havok.Canon.Typed.Hk_HclBendLinkConstraintSet(graph, source)
        If Not r.IsValid Then Return Nothing
        Dim h = r.Links
        Dim result As New HclBendLinkConstraintSetDetail_Class With {.SourceObject = source, .Name = r.Name}
        If h.Count > 0 AndAlso h.DataRelativeOffset >= 0 Then
            For i = 0 To h.Count - 1
                Dim e = h.DataRelativeOffset + (i * 20)
                result.Links.Add(New HclBendLink_Class With {
                    .ParticleA = U16(graph, e + 0), .ParticleB = U16(graph, e + 2),
                    .BendMinLength = graph.ReadSingle(e + 4), .StretchMaxLength = graph.ReadSingle(e + 8),
                    .BendStiffness = graph.ReadSingle(e + 12), .StretchStiffness = graph.ReadSingle(e + 16)})
            Next
        End If
        Return result
    End Function

    ' hclCompressibleLinkConstraintSet — stride 16: {u16 particleA, u16 particleB, 3×float}. (Institute Lab Coat)
    Friend Shared Function ParseCompressibleLinkConstraintSet(graph As HkxObjectGraph_Class, source As HkxVirtualObjectGraph_Class) As HclCompressibleLinkConstraintSetDetail_Class
        If IsNothing(graph) OrElse IsNothing(source) OrElse Not source.ClassName.Equals("hclCompressibleLinkConstraintSet", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        ' Lector generado (HavokTyped.vb): los offsets salen de la reflexion de los dos
        ' .exe y la tabla la elige el packfile. Sin literales que se puedan desincronizar.
        Dim r As New Havok.Canon.Typed.Hk_HclCompressibleLinkConstraintSet(graph, source)
        If Not r.IsValid Then Return Nothing
        Dim h = r.Links
        Dim result As New HclCompressibleLinkConstraintSetDetail_Class With {.SourceObject = source, .Name = r.Name}
        If h.Count > 0 AndAlso h.DataRelativeOffset >= 0 Then
            For i = 0 To h.Count - 1
                Dim e = h.DataRelativeOffset + (i * 16)
                result.Links.Add(New HclCompressibleLink_Class With {
                    .ParticleA = U16(graph, e + 0), .ParticleB = U16(graph, e + 2),
                    .RestLength = graph.ReadSingle(e + 4), .CompressionLength = graph.ReadSingle(e + 8),
                    .Stiffness = graph.ReadSingle(e + 12)})
            Next
        End If
        Return result
    End Function

    ' hclBonePlanesConstraintSet — stride 32: {plane normal(3f)+dist(f), boneIndex(u16), index1(u16), weight(f), 2×f}. (Residents 6Suit)
    Friend Shared Function ParseBonePlanesConstraintSet(graph As HkxObjectGraph_Class, source As HkxVirtualObjectGraph_Class) As HclBonePlanesConstraintSetDetail_Class
        If IsNothing(graph) OrElse IsNothing(source) OrElse Not source.ClassName.Equals("hclBonePlanesConstraintSet", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        ' Lector generado (HavokTyped.vb): los offsets salen de la reflexion de los dos
        ' .exe y la tabla la elige el packfile. Sin literales que se puedan desincronizar.
        Dim r As New Havok.Canon.Typed.Hk_HclBonePlanesConstraintSet(graph, source)
        If Not r.IsValid Then Return Nothing
        Dim h = r.BonePlanes
        Dim result As New HclBonePlanesConstraintSetDetail_Class With {.SourceObject = source, .Name = r.Name}
        If h.Count > 0 AndAlso h.DataRelativeOffset >= 0 Then
            For i = 0 To h.Count - 1
                Dim e = h.DataRelativeOffset + (i * 32)
                result.Constraints.Add(New HclBonePlaneConstraint_Class With {
                    .NormalX = graph.ReadSingle(e + 0), .NormalY = graph.ReadSingle(e + 4), .NormalZ = graph.ReadSingle(e + 8),
                    .PlaneDistance = graph.ReadSingle(e + 12),
                    .ParticleIndex = U16(graph, e + 16), .TransformIndex = U16(graph, e + 18),
                    .Stiffness = graph.ReadSingle(e + 20), .Value0 = graph.ReadSingle(e + 24), .Value1 = graph.ReadSingle(e + 28)})
            Next
        End If
        Return result
    End Function

    ' hclGatherSomeVerticesOperator — stride 4: pares {u16 source, u16 target} de remap de vértices. (Institute Lab Coat)
    Friend Shared Function ParseGatherSomeVerticesOperator(graph As HkxObjectGraph_Class, source As HkxVirtualObjectGraph_Class) As HclGatherSomeVerticesOperatorDetail_Class
        If IsNothing(graph) OrElse IsNothing(source) OrElse Not source.ClassName.Equals("hclGatherSomeVerticesOperator", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        ' Lector generado (HavokTyped.vb): los offsets salen de la reflexion de los dos
        ' .exe y la tabla la elige el packfile. Sin literales que se puedan desincronizar.
        Dim r As New Havok.Canon.Typed.Hk_HclGatherSomeVerticesOperator(graph, source)
        If Not r.IsValid Then Return Nothing
        Dim h = r.VertexPairs
        Dim result As New HclGatherSomeVerticesOperatorDetail_Class With {
            .SourceObject = source, .Name = r.Name,
            .InputBufferIdx = CInt(r.InputBufferIdx),
            .OutputBufferIdx = CInt(r.OutputBufferIdx),
            .GatherNormals = r.GatherNormals}
        If h.Count > 0 AndAlso h.DataRelativeOffset >= 0 Then
            For i = 0 To h.Count - 1
                Dim e = h.DataRelativeOffset + (i * 4)
                result.Pairs.Add(New HclVertexGatherPair_Class With {.Source = U16(graph, e + 0), .Target = U16(graph, e + 2)})
            Next
        End If
        Return result
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

    Friend Shared Function ParseCapsuleShape(graph As HkxObjectGraph_Class, source As HkxVirtualObjectGraph_Class) As HclCapsuleShapeDetail_Class
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

    ' collidableCache opcional (key = RelativeOffset, identidad canónica del objeto en el grafo): cuando
    ' se pasa, memoiza para que un mismo hclCollidable, parseado a nivel package Y referenciado por uno o
    ' más sims, no se re-parsee. Compartir la instancia entre package.Collidables y sim.CollidableDetails
    ' es equivalente PORQUE HclCollidableDetail_Class es inmutable tras el parse (ningún sitio la muta);
    ' si eso deja de valer, la caché pasa a ser un aliasing de estado. Con Nothing se parsea cada vez.
    Friend Shared Function ParseCollidable(graph As HkxObjectGraph_Class, source As HkxVirtualObjectGraph_Class,
                                           Optional collidableCache As Dictionary(Of Integer, HclCollidableDetail_Class) = Nothing) As HclCollidableDetail_Class
        If IsNothing(graph) OrElse IsNothing(source) Then Return Nothing
        If Not source.ClassName.Equals("hclCollidable", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        ' Lector generado (HavokTyped.vb): los offsets salen de la reflexion de los dos
        ' .exe y la tabla la elige el packfile. Sin literales que se puedan desincronizar.
        Dim r As New Havok.Canon.Typed.Hk_HclCollidable(graph, source)
        If Not r.IsValid Then Return Nothing

        Dim cached As HclCollidableDetail_Class = Nothing
        If collidableCache IsNot Nothing AndAlso collidableCache.TryGetValue(source.RelativeOffset, cached) Then Return cached

        ' ⛔⛔ BUG CORREGIDO 2026-08-22 — el payload arrancaba en +0x18 y TODO quedaba corrido 8 bytes.
        ' Layout REAL de hclCollidable (reflexión hkClass del propio Fallout4.exe, size 0x90 ver 3):
        '     +0x10 name (stringptr) · +0x18..+0x1F PADDING · +0x20 transform (hkTransform, 0x40)
        '     +0x60 linearVelocity · +0x70 angularVelocity
        '     +0x80 pinchDetectionEnabled · +0x81 pinchDetectionPriority · +0x84 pinchDetectionRadius
        '     +0x88 shape -> hclShape
        ' CONTRAEJEMPLO medido (FemaleHair04.nif cloth, hclCollidable @0x1210): +0x00..+0x1F son TODO
        ' CEROS; la rotación arranca en +0x20 y la traslación está en +0x50 con w=1 (-3.983, 113.227);
        ' +0x60..+0x7F en cero (velocidades) y +0x84 = 0.01 (pinchDetectionRadius). Leyendo desde +0x18
        ' la fila 0 de la matriz salía en ceros y "LinearVelocity" era media traslación (113.22, 1, 0, 0).
        ' El parser hermano (HkxObjectGraphParser.ParseCollidable) SIEMPRE leyó +0x20: eran dos leyes
        ' distintas para la misma clase y ésta era la equivocada.
        Const cls = "hclCollidable"
        ' ⛔ El guard es por CLASE, no sólo por "hay tabla". Son dos casos distintos y los dos existen:
        '   · Skyrim32 no tiene tabla (la tabla describe x64)  → layout Is Nothing
        '   · Skyrim64 SÍ tiene tabla, pero esa tabla NO declara NINGUNA clase hcl (Havok Cloth es de
        '     FO4: 0 clases hcl en SkyrimSE.exe, medido) → HasClass devuelve False
        ' Sin el segundo, `Offset` devolvía -1 y se leía en `rel - 1` antes de que el `RequireOffset`
        ' de la línea siguiente lanzara. Y `Offset` acá era la ÚNICA lectura de esta función que no
        ' usaba `RequireOffset`: misma ley, dos formas, y la débil se ejecutaba primero.
        Dim layout = CanonLayoutOf(graph)
        If layout Is Nothing OrElse Not layout.HasClass(cls) Then Return Nothing
        Dim rel = source.RelativeOffset
        Dim transformOffset = layout.RequireOffset(cls, "transform")
        Dim transformVectors = ReadVector4Block(graph, rel + transformOffset, 4)
        Dim shapeObject = graph.ResolveGlobalObject(rel + layout.RequireOffset(cls, "shape"))
        Dim result As New HclCollidableDetail_Class With {
            .SourceObject = source,
            .Name = graph.ResolveLocalString(rel + layout.RequireOffset(cls, "name")),
            .ShapeObject = shapeObject,
            .ShapeDetail = ParseCapsuleShape(graph, shapeObject),
            .PayloadVectors = transformVectors,
            .TransformMatrix = CreateMatrix4FromVectorRows(transformVectors, 0),
            .LinearVelocity = ReadVector4At(graph, rel + layout.RequireOffset(cls, "linearVelocity")),
            .AngularVelocity = ReadVector4At(graph, rel + layout.RequireOffset(cls, "angularVelocity")),
            .PinchDetectionEnabled = graph.ReadByte(rel + layout.RequireOffset(cls, "pinchDetectionEnabled")) <> 0,
            .PinchDetectionPriority = CInt(graph.ReadByte(rel + layout.RequireOffset(cls, "pinchDetectionPriority"))),
            .PinchDetectionRadius = graph.ReadSingle(rel + layout.RequireOffset(cls, "pinchDetectionRadius"))
        }
        result.PayloadUInt32 = New List(Of UInteger)
        If collidableCache IsNot Nothing Then collidableCache(source.RelativeOffset) = result
        Return result
    End Function

    ''' <summary>
    ''' Dispatcher de `hclAction` (el array `actions` de hclSimClothData / hclClothData).
    ''' ⛔ NO se cuelga de ParseConstraintObject: las actions NO son constraint sets, viven en otro array
    ''' (+0xE8 vs +0xB8) y ese dispatcher nunca las vería — sería código muerto que además haría que un
    ''' censo de viento respondiera "no hay" midiendo en vacío.
    ''' </summary>
    Friend Shared Function ParseActionObject(graph As HkxObjectGraph_Class, source As HkxVirtualObjectGraph_Class) As HclActionDetail_Class
        If IsNothing(graph) OrElse IsNothing(source) Then Return Nothing
        Select Case source.ClassName.ToLowerInvariant()
            Case "hclsimplewindaction"
                Return ParseSimpleWindAction(graph, source)
            Case Else
                Return New HclGenericActionDetail_Class With {.SourceObject = source, .ClassName = source.ClassName}
        End Select
    End Function

    Friend Shared Function ParseSimpleWindAction(graph As HkxObjectGraph_Class, source As HkxVirtualObjectGraph_Class) As HclSimpleWindActionDetail_Class
        If IsNothing(graph) OrElse IsNothing(source) Then Return Nothing
        If Not source.ClassName.Equals("hclSimpleWindAction", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        ' Lector generado (HavokTyped.vb): los offsets salen de la reflexion de los dos
        ' .exe y la tabla la elige el packfile. Sin literales que se puedan desincronizar.
        Dim r As New Havok.Canon.Typed.Hk_HclSimpleWindAction(graph, source)
        If Not r.IsValid Then Return Nothing
        Const cls = "hclSimpleWindAction"
        Dim layout = CanonLayoutOf(graph)
        If layout Is Nothing OrElse Not layout.HasClass(cls) Then Return Nothing
        Dim rel = source.RelativeOffset
        Return New HclSimpleWindActionDetail_Class With {
            .SourceObject = source,
            .ClassName = source.ClassName,
            .WindDirection = ReadVector4At(graph, rel + layout.RequireOffset(cls, "windDirection")),
            .WindMinSpeed = graph.ReadSingle(rel + layout.RequireOffset(cls, "windMinSpeed")),
            .WindMaxSpeed = graph.ReadSingle(rel + layout.RequireOffset(cls, "windMaxSpeed")),
            .WindFrequency = graph.ReadSingle(rel + layout.RequireOffset(cls, "windFrequency")),
            .MaximumDrag = graph.ReadSingle(rel + layout.RequireOffset(cls, "maximumDrag")),
            .AirVelocity = ReadVector4At(graph, rel + layout.RequireOffset(cls, "airVelocity")),
            .CurrentTime = graph.ReadSingle(rel + layout.RequireOffset(cls, "currentTime"))
        }
    End Function

    ''' <summary>Tabla canónica del formato que el packfile DECLARA. Nothing = formato sin tabla (Skyrim32).</summary>
    Friend Shared Function CanonLayoutOf(graph As HkxObjectGraph_Class) As Havok.Canon.HavokLayout
        ' Delega: la ley "que tabla usa este archivo" vive en HavokLayout.ForGraph y en ningun otro lado.
        Return Havok.Canon.HavokLayout.ForGraph(graph)
    End Function

    Private Shared Function ReadVector4At(graph As HkxObjectGraph_Class, relativeOffset As Integer) As HkxVector4Graph_Class
        Return New HkxVector4Graph_Class With {
            .X = graph.ReadSingle(relativeOffset),
            .Y = graph.ReadSingle(relativeOffset + 4),
            .Z = graph.ReadSingle(relativeOffset + 8),
            .W = graph.ReadSingle(relativeOffset + 12)
        }
    End Function
    Friend Shared Function ParseStandardLinkConstraintSet(graph As HkxObjectGraph_Class, source As HkxVirtualObjectGraph_Class) As HclStandardLinkConstraintSetDetail_Class
        If IsNothing(graph) OrElse IsNothing(source) Then Return Nothing
        If Not source.ClassName.Equals("hclStandardLinkConstraintSet", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        ' Lector generado (HavokTyped.vb): los offsets salen de la reflexion de los dos
        ' .exe y la tabla la elige el packfile. Sin literales que se puedan desincronizar.
        Dim r As New Havok.Canon.Typed.Hk_HclStandardLinkConstraintSet(graph, source)
        If Not r.IsValid Then Return Nothing

        Dim rawLinks = ReadRawStructArray(graph, r.Links, 12)
        Dim result As New HclStandardLinkConstraintSetDetail_Class With {
            .SourceObject = source,
            .Name = r.Name
        }
        result.LinkDetails.AddRange(ParseDistanceConstraints(rawLinks))
        Return result
    End Function

    Friend Shared Function ParseStretchLinkConstraintSet(graph As HkxObjectGraph_Class, source As HkxVirtualObjectGraph_Class) As HclStretchLinkConstraintSetDetail_Class
        If IsNothing(graph) OrElse IsNothing(source) Then Return Nothing
        If Not source.ClassName.Equals("hclStretchLinkConstraintSet", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        ' Lector generado (HavokTyped.vb): los offsets salen de la reflexion de los dos
        ' .exe y la tabla la elige el packfile. Sin literales que se puedan desincronizar.
        Dim r As New Havok.Canon.Typed.Hk_HclStretchLinkConstraintSet(graph, source)
        If Not r.IsValid Then Return Nothing

        Dim rawLinks = ReadRawStructArray(graph, r.Links, 12)
        Dim result As New HclStretchLinkConstraintSetDetail_Class With {
            .SourceObject = source,
            .Name = r.Name
        }
        result.LinkDetails.AddRange(ParseDistanceConstraints(rawLinks))
        Return result
    End Function

    Friend Shared Function ParseBendStiffnessConstraintSet(graph As HkxObjectGraph_Class, source As HkxVirtualObjectGraph_Class) As HclBendStiffnessConstraintSetDetail_Class
        If IsNothing(graph) OrElse IsNothing(source) Then Return Nothing
        If Not source.ClassName.Equals("hclBendStiffnessConstraintSet", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        ' Lector generado (HavokTyped.vb): los offsets salen de la reflexion de los dos
        ' .exe y la tabla la elige el packfile. Sin literales que se puedan desincronizar.
        Dim r As New Havok.Canon.Typed.Hk_HclBendStiffnessConstraintSet(graph, source)
        If Not r.IsValid Then Return Nothing

        Dim rawLinks = ReadRawStructArray(graph, r.Links, 32)
        Dim result As New HclBendStiffnessConstraintSetDetail_Class With {
            .SourceObject = source,
            .Name = r.Name,
            .UseRestPoseConfig = r.UseRestPoseConfig
        }
        result.LinkDetails.AddRange(ParseBendConstraints(rawLinks))
        Return result
    End Function

    Friend Shared Function ParseLocalRangeConstraintSet(graph As HkxObjectGraph_Class, source As HkxVirtualObjectGraph_Class) As HclLocalRangeConstraintSetDetail_Class
        If IsNothing(graph) OrElse IsNothing(source) Then Return Nothing
        If Not source.ClassName.Equals("hclLocalRangeConstraintSet", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        ' Lector generado (HavokTyped.vb): los offsets salen de la reflexion de los dos
        ' .exe y la tabla la elige el packfile. Sin literales que se puedan desincronizar.
        Dim r As New Havok.Canon.Typed.Hk_HclLocalRangeConstraintSet(graph, source)
        If Not r.IsValid Then Return Nothing

        Const cls = "hclLocalRangeConstraintSet"
        Dim layout = CanonLayoutOf(graph)
        If layout Is Nothing OrElse Not layout.HasClass(cls) Then Return Nothing
        Dim rel = source.RelativeOffset
        Dim rawConstraints = ReadRawStructArray(graph, graph.ReadArrayHeader(rel + layout.RequireOffset(cls, "localConstraints")), 16)
        Dim result As New HclLocalRangeConstraintSetDetail_Class With {
            .SourceObject = source,
            .Name = graph.ResolveLocalString(rel + layout.RequireOffset(cls, "name")),
            .ReferenceMeshBufferIdx = graph.ReadInt32(rel + layout.RequireOffset(cls, "referenceMeshBufferIdx")),
            .Stiffness = graph.ReadSingle(rel + layout.RequireOffset(cls, "stiffness")),
            .ShapeType = graph.ReadInt32(rel + layout.RequireOffset(cls, "shapeType")),
            .ApplyNormalComponent = graph.ReadByte(rel + layout.RequireOffset(cls, "applyNormalComponent")) <> 0
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

    Friend Shared Function ParseVolumeConstraintMx(graph As HkxObjectGraph_Class, source As HkxVirtualObjectGraph_Class) As HclVolumeConstraintMxDetail_Class
        If IsNothing(graph) OrElse IsNothing(source) Then Return Nothing
        If Not source.ClassName.Equals("hclVolumeConstraintMx", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        ' Lector generado (HavokTyped.vb): los offsets salen de la reflexion de los dos
        ' .exe y la tabla la elige el packfile. Sin literales que se puedan desincronizar.
        Dim r As New Havok.Canon.Typed.Hk_HclVolumeConstraintMx(graph, source)
        If Not r.IsValid Then Return Nothing

        ' Raw structs LOCALES (se consumen acá para producir batches/quad-slots tipados; no se guardan en el result).
        Dim f20raw = ReadRawStructArray(graph, r.FrameBatchDatas, 352)
        Dim f40raw = ReadRawStructArray(graph, r.ApplyBatchDatas, 352)
        Dim result As New HclVolumeConstraintMxDetail_Class With {
            .SourceObject = source,
            .Name = r.Name,
            .Field20VectorBlocks = ReadVectorStructArray(graph, r.FrameBatchDatas, 22),
            .Field30VectorBlocks = ReadVectorStructArray(graph, r.FrameSingleDatas, 2),
            .Field40VectorBlocks = ReadVectorStructArray(graph, r.ApplyBatchDatas, 22),
            .Field50VectorBlocks = ReadVectorStructArray(graph, r.ApplySingleDatas, 2)
        }

        result.Field20Batches.AddRange(ParseVolumeConstraintBatches(f20raw, result.Field20VectorBlocks))
        result.Field30Entries.AddRange(ParseVolumeConstraintVectorEntries(result.Field30VectorBlocks))
        result.Field40Batches.AddRange(ParseVolumeConstraintBatches(f40raw, result.Field40VectorBlocks))
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
        result.Field30LeadEntryCount = If(result.Field50PivotTailStartIndex, 0)
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
                           Return New With { .Slot = reference, sharedParticles, .SharedCount = sharedParticles.Count }
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
                .ParticleIndex = BitConverter.ToUInt16(raw.RawBytes, 0),
                .ReferenceVertexIndex = BitConverter.ToUInt16(raw.RawBytes, 2),
                .MaximumDistance = BitConverter.ToSingle(raw.RawBytes, 4),
                .MaximumNormalDistance = BitConverter.ToSingle(raw.RawBytes, 8),
                .MinimumNormalDistance = BitConverter.ToSingle(raw.RawBytes, 12)
            })
        Next

        Return result
    End Function

    Friend Shared Function ParseConstraintObject(graph As HkxObjectGraph_Class, source As HkxVirtualObjectGraph_Class) As Object
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
                .Value = graph.ReadUInt32(entryOffset)
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
            result.Add(graph.ReadUInt32(relativeOffset + (i * 4)))
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
                .FirstValue = graph.ReadUInt32(entryOffset),
                .SecondValue = graph.ReadUInt32(entryOffset + 4)
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
        Dim bytes = graph.ReadBytes(entryRelativeOffset, byteCount)
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
            result.Add(graph.ReadUInt32(field.DataRelativeOffset + (i * 4)))
        Next

        Return result
    End Function

    Private Shared Function ReadByteArray(graph As HkxObjectGraph_Class, field As HkxObjectArrayHeader_Class) As Byte()
        If IsNothing(field) OrElse field.Count <= 0 OrElse field.DataRelativeOffset < 0 Then Return Array.Empty(Of Byte)()
        Return graph.ReadBytes(field.DataRelativeOffset, field.Count)
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
        Return graph.ReadBytes(source.RelativeOffset + payloadOffset, byteCount)
    End Function

    Private Shared Function ReadPayloadUInt32(graph As HkxObjectGraph_Class, source As HkxVirtualObjectGraph_Class, payloadOffset As Integer) As List(Of UInteger)
        Dim result As New List(Of UInteger)
        Dim bytes = ReadPayloadBytes(graph, source, payloadOffset)
        For i = 0 To (bytes.Length \ 4) - 1
            result.Add(BitConverter.ToUInt32(bytes, i * 4))
        Next
        Return result
    End Function

    ' Thin uint16 wrapper sobre el reader canónico THROWING graph.ReadInt16 (no existe un
    ' graph.ReadUInt16 escalar). Convierte a unsigned sin sign-extension. Si el offset HCL
    ' (empírico) cae fuera del contenido, graph.ReadInt16 lanza InvalidDataException — NO
    ' devuelve 0 silencioso (HKX-002/HKX-009): un offset mal adivinado debe aflorar como bug.
    Private Shared Function ReadUInt16(graph As HkxObjectGraph_Class, relativeOffset As Integer) As UShort
        Return CUShort(CInt(graph.ReadInt16(relativeOffset)) And &HFFFF)
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
    ' Array +0x108, stride 1 MEDIDO (ver ParseSimClothData). Lo que NO está confirmado contra el SDK
    ' de Havok es la SEMÁNTICA: el nombre sale de que el 99,8% de los bytes valen 0/1.
    Public ReadOnly Property PinchDetectionFlags As New List(Of Byte)
    ''' <summary>`collidablePinchingDatas` (+0x118) — UNA entrada por colisionable, 8 bytes:
    ''' <c>bool pinchDetectionEnabled; int8 pinchDetectionPriority; real pinchDetectionRadius</c>.
    ''' Dice, POR COLISIONABLE, si participa de la deteccion de pellizco, con que prioridad y con que
    ''' radio. Antes no se leia: el censo de subsistemas lo encontro declarado en 1.659 sim-cloth del
    ''' corpus y sin parsear siquiera.</summary>
    Public ReadOnly Property CollidablePinchingDatas As New List(Of HclCollidablePinchingData_Class)
    ''' <summary>`minPinchedParticleIndex`/`maxPinchedParticleIndex` (+0x128/+0x12A): el RANGO de
    ''' particulas que el pellizco puede tocar. Fuera de el, el motor ni mira.</summary>
    Public Property MinPinchedParticleIndex As Integer
    Public Property MaxPinchedParticleIndex As Integer
    Public Property Field118Pairs As List(Of HkxUInt32PairGraph_Class)
    Public Property CollidableBindingUniformParameter As Single?
    Public Property CollidableBindingParametersUniform As Boolean
    Public Property CollidableBindingsAllMatrixIdentity As Boolean
    Public Property VolumeConstraintCount As Integer
    Public Property VolumeConstraintField30MatchesBindingParameter As Boolean
    Public Property VolumeConstraintField50MatchesBindingParameter As Boolean

    ' ======================================================================================
    ' PARÁMETROS DE SIMULACIÓN — hclSimClothDataOverridableSimulationInfo (+0x10) y compañía.
    ' Antes no se leían. Son los que el motor usa en hclSimulateOperator::execute.
    ' ======================================================================================
    ''' <summary>gravity (u/s²). Medido en vanilla: ≈ -686.7 en Z para ropa (1 g a 70 u/m), ≈ -1500 en pelo.</summary>
    Public Property Gravity As HkxVector4Graph_Class
    ''' <summary>Fracción de velocidad perdida POR SEGUNDO. El factor por paso es (1-d)^dt.
    ''' ⛔ El motor tiene dos ramas duras: d&gt;=1 ⇒ factor 0 (sin inercia) y d=0 ⇒ factor 1.</summary>
    Public Property GlobalDampingPerSecond As Single
    ''' <summary>Margen de colisión. Lo que antes figuraba como "+0x024 = 13.998, campo sin nombrar".</summary>
    Public Property CollisionTolerance As Single
    ''' <summary>Substeps por frame. 0 ⇒ el motor usa el subSteps del hclSimulateOperator.</summary>
    Public Property SubSteps As Integer
    Public Property PinchDetectionEnabled As Boolean
    Public Property LandscapeCollisionEnabled As Boolean
    Public Property TransferMotionEnabled As Boolean
    ''' <summary>Masa total. El viento reparte su fuerza por mass_i/totalMass; si es 0 el motor NO aplica viento.</summary>
    Public Property TotalMass As Single
    Public Property MaxParticleRadius As Single
    Public Property MaxCollisionPairs As Integer
    Public Property DoNormals As Boolean
    Public Property NumLandscapeCollidableParticles As Integer
    Public Property LandscapeRadius As Single
    ''' <summary>`landscapeCollisionData.enableStuckParticleDetection` (+0x04) y
    ''' `stuckParticlesStretchFactorSq` (+0x08): una particula cuyo link supera ese factor de
    ''' estiramiento AL CUADRADO se considera enganchada en el terreno y el motor la libera.</summary>
    Public Property EnableStuckParticleDetection As Boolean
    Public Property StuckParticlesStretchFactorSq As Single
    ''' <summary>Pinch del TERRENO: `landscapeCollisionData.pinchDetectionEnabled` (+0x0C),
    ''' `pinchDetectionPriority` (+0x0D, con signo) y `pinchDetectionRadius` (+0x10).</summary>
    Public Property LandscapePinchDetectionEnabled As Boolean
    Public Property LandscapePinchPriority As Integer
    Public Property LandscapePinchRadius As Single
    Public Property CollidableTransformSetIndex As Integer
    Public Property TransferMotionTransformSetIndex As Integer
    Public Property TransferMotionTransformIndex As Integer
    Public Property TransferTranslationMotion As Boolean
    ''' <summary>Los cuatro numeros que arman la mezcla de TRASLACION: por debajo de
    ''' `minTranslationSpeed` se transfiere `minTranslationBlend`, por encima de `maxTranslationSpeed`
    ''' se transfiere `maxTranslationBlend`, y en el medio va lineal. La velocidad es
    ''' |delta traslacion| / dt, en unidades por segundo.</summary>
    Public Property MinTranslationSpeed As Single
    Public Property MaxTranslationSpeed As Single
    Public Property MinTranslationBlend As Single
    Public Property MaxTranslationBlend As Single
    Public Property TransferRotationMotion As Boolean
    ''' <summary>Idem para la ROTACION, pero la velocidad va en GRADOS por segundo: el motor
    ''' multiplica el angulo por 57,29578 (= 180/π) antes de compararlo (0x141A13B50).</summary>
    Public Property MinRotationSpeed As Single
    Public Property MaxRotationSpeed As Single
    Public Property MinRotationBlend As Single
    Public Property MaxRotationBlend As Single
    ''' <summary>`triangleFlips` (+0x68). Antes se documentaba como "m_unknown68, tipo desconocido".</summary>
    Public ReadOnly Property TriangleFlips As New List(Of Byte)
    ''' <summary>`actions` (+0xE8) — acciones de la sim (viento). Antes no se leía.</summary>
    Public Property Actions As List(Of HkxVirtualObjectGraph_Class)
    Public ReadOnly Property ActionDetails As New List(Of HclActionDetail_Class)
    ''' <summary>`antiPinchConstraintSets` (+0xC8). Antes no se leía.</summary>
    Public Property AntiPinchConstraintSets As List(Of HkxVirtualObjectGraph_Class)
    ''' <summary>Los `antiPinchConstraintSets` YA PARSEADOS, igual que `ConstraintDetails`. Antes solo
    ''' se guardaban los objetos crudos del grafo, o sea que estaban "leidos" y no se podian ejecutar:
    ''' el motor los resuelve despues de cada colision con k = 1,0 (`CollideAndSolve`, 0x141A69730).</summary>
    Public ReadOnly Property AntiPinchDetails As New List(Of Object)
End Class

''' <summary>Base de las hclAction. Hoy la única serializable es hclSimpleWindAction.</summary>
''' <summary>`hclSimClothDataCollidablePinchingData` — 8 bytes, uno por colisionable.</summary>
Public Class HclCollidablePinchingData_Class
    Public Property Enabled As Boolean
    ''' <summary>`pinchDetectionPriority` es int8 CON SIGNO: gana el de prioridad mas alta cuando una
    ''' particula queda entre dos colisionables.</summary>
    Public Property Priority As Integer
    Public Property Radius As Single
End Class

Public MustInherit Class HclActionDetail_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property ClassName As String = ""
End Class

''' <summary>
''' `hclSimpleWindAction` — el VIENTO. Layout por reflexión. El motor lo aplica así
''' (applyAction @0x1418F8420, compartida con hclBSClothParameterizedWindAction):
'''     f      = |dot(normal_i, windDirection)|   (0.7 si no hay normales por partícula)
'''     relAir = airVelocity - (P[i]-Pprev[i])/dt
'''     F[i]  += relAir * maximumDrag * f * (mass_i / totalMass)
''' </summary>
Public Class HclSimpleWindActionDetail_Class
    Inherits HclActionDetail_Class
    Public Property WindDirection As HkxVector4Graph_Class   ' +0x10
    Public Property WindMinSpeed As Single                   ' +0x20
    Public Property WindMaxSpeed As Single                   ' +0x24
    Public Property WindFrequency As Single                  ' +0x28
    Public Property MaximumDrag As Single                    ' +0x2C
    Public Property AirVelocity As HkxVector4Graph_Class     ' +0x30
    Public Property CurrentTime As Single                    ' +0x40
End Class

''' <summary>Acción genérica: la clase existe pero no tiene lector específico todavía.</summary>
Public Class HclGenericActionDetail_Class
    Inherits HclActionDetail_Class
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
    Public Property Field48Vectors As List(Of HkxVector4Graph_Class)
    Public ReadOnly Property BufferAccesses As New List(Of HclClothStateBufferAccessDetail_Class)
    Public ReadOnly Property AuxiliaryBufferAccesses As New List(Of HclClothStateBufferAccessDetail_Class)
    Public ReadOnly Property TransformAccessContainers As New List(Of HclClothStateTransformAccessContainerDetail_Class)
    Public ReadOnly Property TransformSetAccesses As New List(Of HclClothStateTransformSetAccessDetail_Class)
End Class

Public Class HclClothStateBufferAccessDetail_Class
    Public Property EntryIndex As Integer
    Public Property EntryRelativeOffset As Integer
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
    Public Property NestedAccessHeader As HkxObjectArrayHeader_Class
    Public ReadOnly Property Accesses As New List(Of HclClothStateTransformSetAccessDetail_Class)
End Class

Public Class HclClothStateTransformSetAccessDetail_Class
    Public Property EntryIndex As Integer
    Public Property EntryRelativeOffset As Integer
    Public ReadOnly Property ComponentAccesses As New List(Of HclClothStateTransformComponentAccessDetail_Class)
    Public Property HasAnyMaskData As Boolean
End Class

Public Class HclClothStateTransformComponentAccessDetail_Class
    Public Property SubIndex As Integer
    Public Property HeaderRelativeOffset As Integer
    Public Property ArrayHeader As HkxObjectArrayHeader_Class
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
    Public Property PayloadUInt32 As List(Of UInteger)
    Public Property ParticleCount As Integer
    Public Property TriangleCount As Integer
End Class

Public Class HclScratchBufferDefinitionDetail_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Name As String
    Public Property PayloadUInt32 As List(Of UInteger)
    Public Property ParticleCount As Integer
    Public Property TriangleCount As Integer
End Class

Public Class HclMoveParticlesOperatorDetail_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Name As String
    Public Property Pairs As List(Of HclMoveParticlesVertexParticlePairGraph_Class)
    ''' <summary>`refBufferIdx` (+0x34): el buffer del que salen las posiciones de las anclas.</summary>
    Public Property RefBufferIdx As Integer
    Public Property SimClothIndex As Integer
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
    Public Property SubstepCount As Integer
    Public Property SolveIterationCount As Integer
    ''' <summary>`adaptConstraintStiffness` (+0x40). ⛔ NO es informativo: es lo unico serializado que
    ''' pone a `StiffnessFactor` en el modo 2, donde el factor `k` de TODOS los constraint sets deja de
    ''' valer 1 y pasa a `(subSteps·s1·s2)^-1,725`. MEDIDO: 846 de los 1.248 operadores del corpus.</summary>
    Public Property AdaptConstraintStiffness As Boolean
    Public Property Configs As List(Of HclSimulateOperatorConfigGraph_Class)
End Class

Public Class HclSimulateOperatorConfigGraph_Class
    Public Property EntryIndex As Integer
    Public Property EntryRelativeOffset As Integer
    Public Property Value As UInteger
    Public Property ConstraintIndex As Integer = -1
    Public Property IsTerminator As Boolean
    ''' <summary>Friend A PROPOSITO. Es un handle `Object` sobre un grafo que ahora es `Friend`, y con
    ''' `Option Strict Off` un `cfg.ResolvedConstraint.Name` desde otro ensamblado COMPILA LIMPIO y tira
    ''' `MissingMemberException` recien en produccion. Los consumidores externos leen
    ''' `ResolvedConstraintName`/`ResolvedConstraintType`, que son String y siguen publicos.</summary>
    Friend Property ResolvedConstraint As Object
    Public Property ResolvedConstraintName As String
    Public Property ResolvedConstraintType As String
End Class

Public Class HclCopyVerticesOperatorDetail_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Name As String
    Public Property PayloadUInt32 As List(Of UInteger)
    Public Property ElementCount As Integer
    Public Property PayloadAsciiTag As String
    ''' <summary>`numberOfVertices`, `startVertexIn` y `startVertexOut`: la copia es
    ''' <c>out[startVertexOut + i] = in[startVertexIn + i]</c> para i en [0, numberOfVertices).
    ''' MEDIDO en el corpus: 875 de 875 con los dos `start` en 0 y la cuenta completa — o sea la
    ''' IDENTIDAD. Por eso se lo trataba como no-op; pero ser la identidad es justamente lo que lo
    ''' convierte en el puente particula↔vertice del estado SIN fisica, y ahi si hace falta.</summary>
    Public Property NumberOfVertices As Integer
    Public Property StartVertexIn As Integer
    Public Property StartVertexOut As Integer
    Public Property InputBufferIdx As Integer
    Public Property OutputBufferIdx As Integer
    ''' <summary>`copyNormals` (+0x30). MEDIDO: True en los 875 del corpus.</summary>
    Public Property CopyNormals As Boolean
End Class

Public Class HclGatherAllVerticesOperatorDetail_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Name As String
    Public Property PayloadUInt32 As List(Of UInteger)
    Public Property ElementCount As Integer
    Public Property PayloadAsciiTag As String
    Public Property GatheredVertexIndices As New List(Of UShort)
    ''' <summary>`inputBufferIdx` (+0x30) / `outputBufferIdx` (+0x34) / `gatherNormals` (+0x38).
    ''' Sin estos el operador no se puede ejecutar: dicen DE QUE buffer lee y A CUAL escribe.</summary>
    Public Property InputBufferIdx As Integer
    Public Property OutputBufferIdx As Integer
    Public Property GatherNormals As Boolean
    Public Property PartialGather As Boolean
End Class

' --- Cloth-menores: detalles TIPADOS (cero bytes crudos), structs verificados por --dump multi-elemento.
''' <summary>`hclBendLinkConstraintSetLink` — 20 bytes. Los nombres salen de la reflexion; antes
''' eran `Value0..Value3` y por eso no se podia saber cual era cual sin abrir el binario.</summary>
Friend Class HclBendLink_Class
    Friend Property ParticleA As Integer
    Friend Property ParticleB As Integer
    ''' <summary>`bendMinLength` (+0x04): por debajo de esto el link EMPUJA hacia afuera.</summary>
    Friend Property BendMinLength As Single
    ''' <summary>`stretchMaxLength` (+0x08): por encima de esto el link FRENA el estiron.</summary>
    Friend Property StretchMaxLength As Single
    ''' <summary>`bendStiffness` (+0x0C).</summary>
    Friend Property BendStiffness As Single
    ''' <summary>`stretchStiffness` (+0x10).</summary>
    Friend Property StretchStiffness As Single
End Class
Friend Class HclBendLinkConstraintSetDetail_Class
    Friend Property SourceObject As HkxVirtualObjectGraph_Class
    Friend Property Name As String
    Friend ReadOnly Property Links As New List(Of HclBendLink_Class)
End Class

''' <summary>`hclCompressibleLinkConstraintSetLink` — 16 bytes. Nombres de la reflexion.</summary>
Friend Class HclCompressibleLink_Class
    Friend Property ParticleA As Integer
    Friend Property ParticleB As Integer
    ''' <summary>`restLength` (+0x04): el tope SUPERIOR.</summary>
    Friend Property RestLength As Single
    ''' <summary>`compressionLength` (+0x08): el tope INFERIOR.</summary>
    Friend Property CompressionLength As Single
    ''' <summary>`stiffness` (+0x0C).</summary>
    Friend Property Stiffness As Single
End Class
Friend Class HclCompressibleLinkConstraintSetDetail_Class
    Friend Property SourceObject As HkxVirtualObjectGraph_Class
    Friend Property Name As String
    Friend ReadOnly Property Links As New List(Of HclCompressibleLink_Class)
End Class

''' <summary>`hclBonePlanesConstraintSetBonePlane` — 32 bytes. Los nombres salen de la reflexion.
''' ⛔ Antes se llamaban `BoneIndex`, `Index1` y `Weight`, y los dos primeros estaban CAMBIADOS: el
''' de +0x10 es la PARTICULA y el de +0x12 es el indice del transform, no un hueso y un "index1".
''' Con esos nombres nadie podia escribir el solver sin volver a abrir el binario.</summary>
Friend Class HclBonePlaneConstraint_Class
    ''' <summary>`planeEquationBone`.xyz — la normal, en el espacio DEL HUESO.</summary>
    Friend Property NormalX As Single
    Friend Property NormalY As Single
    Friend Property NormalZ As Single
    ''' <summary>`planeEquationBone`.w — la distancia del plano.</summary>
    Friend Property PlaneDistance As Single
    ''' <summary>`particleIndex` (+0x10).</summary>
    Friend Property ParticleIndex As Integer
    ''' <summary>`transformIndex` (+0x12): que matriz del transform-set define el plano.</summary>
    Friend Property TransformIndex As Integer
    ''' <summary>`stiffness` (+0x14).</summary>
    Friend Property Stiffness As Single
    Friend Property Value0 As Single
    Friend Property Value1 As Single
End Class
Friend Class HclBonePlanesConstraintSetDetail_Class
    Friend Property SourceObject As HkxVirtualObjectGraph_Class
    Friend Property Name As String
    Friend ReadOnly Property Constraints As New List(Of HclBonePlaneConstraint_Class)
End Class

Public Class HclVertexGatherPair_Class
    Public Property Source As Integer
    Public Property Target As Integer
End Class
Public Class HclGatherSomeVerticesOperatorDetail_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Name As String
    Public ReadOnly Property Pairs As New List(Of HclVertexGatherPair_Class)
    Public Property InputBufferIdx As Integer
    Public Property OutputBufferIdx As Integer
    Public Property GatherNormals As Boolean
End Class

' hclCollidable — TODOS los campos por reflexión (size 0x90, ver 3). Ver ParseCollidable para el
' contraejemplo que destapó el corrimiento de 8 bytes que este parser tuvo hasta 2026-08-22.
Public Class HclCollidableDetail_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Name As String
    Public Property ShapeObject As HkxVirtualObjectGraph_Class
    Public Property ShapeDetail As HclCapsuleShapeDetail_Class
    ''' <summary>Offset del `transform` (+0x20). Se conserva el nombre viejo para no romper llamadores.</summary>
    ''' <summary>⛔ OBSOLETO: quedaba de leer el objeto como un blob desde +0x18. Siempre vacío.</summary>
    Public Property PayloadUInt32 As List(Of UInteger)
    ''' <summary>Los 4 hkVector4 del `transform` (3 filas de rotación + traslación).</summary>
    Public Property PayloadVectors As List(Of HkxVector4Graph_Class)
    ''' <summary>`transform` (+0x20): 3 filas de rotación + traslación en la 4.ª, con w=1.</summary>
    Public Property TransformMatrix As HkxMatrix4Graph_Class
    Public Property LinearVelocity As HkxVector4Graph_Class      ' +0x60
    Public Property AngularVelocity As HkxVector4Graph_Class     ' +0x70
    Public Property PinchDetectionEnabled As Boolean             ' +0x80
    Public Property PinchDetectionPriority As Integer            ' +0x81 (int8)
    Public Property PinchDetectionRadius As Single               ' +0x84
    ''' <summary>⛔ OBSOLETO: era el vector 6 del blob mal alineado. Fuera de hclCollidable no hay tal campo.</summary>
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

Friend Class HclVolumeConstraintBridgeSlot_Class
    Friend Property TargetSlot As HclVolumeConstraintQuadSlot_Class
    Friend Property FirstSourceSlot As HclVolumeConstraintQuadSlot_Class
    Friend Property SecondSourceSlot As HclVolumeConstraintQuadSlot_Class
    Friend ReadOnly Property SharedParticlesFirst As New List(Of Integer)
    Friend ReadOnly Property SharedParticlesSecond As New List(Of Integer)
    Friend ReadOnly Property OuterParticlesFirst As New List(Of Integer)
    Friend ReadOnly Property OuterParticlesSecond As New List(Of Integer)
    Friend ReadOnly Property BridgeParticles As New List(Of Integer)
End Class

Public Class HclStandardLinkConstraintSetDetail_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Name As String
    Public ReadOnly Property LinkDetails As New List(Of HclDistanceConstraintGraph_Class)
End Class

Public Class HclStretchLinkConstraintSetDetail_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Name As String
    Public ReadOnly Property LinkDetails As New List(Of HclDistanceConstraintGraph_Class)
End Class

Friend Class HclVolumeConstraintQuadSlot_Class
    Friend Property RawStructEntryIndex As Integer
    Friend Property SlotIndex As Integer
    Friend Property ByteOffset As Integer
    Friend Property ParticleA As Integer
    Friend Property ParticleB As Integer
    Friend Property ParticleC As Integer
    Friend Property ParticleD As Integer
    Friend ReadOnly Property Particles As New List(Of Integer)
    Friend Property IsAllZero As Boolean
End Class

Friend Class HclVolumeConstraintVectorEntry_Class
    Friend Property EntryIndex As Integer
    Friend Property Pivot As HkxVector4Graph_Class
    Friend Property Parameters As HkxVector4Graph_Class
End Class

Friend Class HclVolumeConstraintPivotMatch_Class
    Friend Property EntryIndex As Integer
    Friend Property MatchedEntryIndex As Integer
End Class

Friend Class HclVolumeConstraintLane_Class
    Friend Property LaneIndex As Integer
    Friend Property QuadSlot As HclVolumeConstraintQuadSlot_Class
    Friend Property ParameterVector As HkxVector4Graph_Class
    Friend ReadOnly Property CoefficientVectors As New List(Of HkxVector4Graph_Class)
End Class

Friend Class HclVolumeConstraintBatch_Class
    Friend Property EntryIndex As Integer
    Friend Property VectorBlock As HkxVectorStructBlockGraph_Class
    Friend ReadOnly Property AllVectors As New List(Of HkxVector4Graph_Class)
    Friend ReadOnly Property PreQuadVectors As New List(Of HkxVector4Graph_Class)
    Friend ReadOnly Property MidVectors As New List(Of HkxVector4Graph_Class)
    Friend ReadOnly Property PostQuadVectors As New List(Of HkxVector4Graph_Class)
    Friend Property MidVectorsLookZeroish As Boolean
    Friend Property UniformLaneParameter As Single?
    Friend Property LaneParameterIsUniform As Boolean
    Friend ReadOnly Property QuadSlots As New List(Of HclVolumeConstraintQuadSlot_Class)
    Friend ReadOnly Property Lanes As New List(Of HclVolumeConstraintLane_Class)
End Class

Public Class HclBendStiffnessConstraintSetDetail_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Name As String
    ''' <summary>`useRestPoseConfig` (+0x30). ⛔ NO es cosmetico: el motor lo lee en
    ''' <c>hclBendStiffnessConstraintSet::solve</c> (0x1419F9A62) y con el ELIGE ENTRE DOS LEYES
    ''' DISTINTAS — 0x1419F9B50 (lineal, sin restCurvature) contra 0x1419F9CF0 (angulo diedro con
    ''' normales). Sin este campo el simulador no puede saber cual le toca.</summary>
    Public Property UseRestPoseConfig As Boolean
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
    Public ReadOnly Property ConstraintDetails As New List(Of HclLocalRangeConstraintGraph_Class)
    Public Property UniformMaximumDistance As Single?
    Public Property UniformMaximumNormalDistance As Single?
    Public Property UniformMinimumNormalDistance As Single?
    Public Property DistinctParticleCount As Integer
    Public Property DistinctReferenceVertexCount As Integer
    Public Property ParticleReferenceIdentityCount As Integer
    ''' <summary>Buffer cuyas posiciones son la REFERENCIA de la correa (el cuerpo skinneado).</summary>
    Public Property ReferenceMeshBufferIdx As Integer
    Public Property Stiffness As Single
    ''' <summary>enum hclLocalRangeConstraintSetShapeType. Es lo que decide si la correa es cono o esfera:
    ''' hasta ahora "cono" salía de mirar los valores, no de leer este campo.</summary>
    Public Property ShapeType As Integer
    Public Property ApplyNormalComponent As Boolean
End Class

Friend Class HclVolumeConstraintMxDetail_Class
    Friend Property SourceObject As HkxVirtualObjectGraph_Class
    Friend Property Name As String
    Friend Property Field20VectorBlocks As List(Of HkxVectorStructBlockGraph_Class)
    Friend Property Field30VectorBlocks As List(Of HkxVectorStructBlockGraph_Class)
    Friend Property Field40VectorBlocks As List(Of HkxVectorStructBlockGraph_Class)
    Friend Property Field50VectorBlocks As List(Of HkxVectorStructBlockGraph_Class)
    Friend ReadOnly Property Field20Batches As New List(Of HclVolumeConstraintBatch_Class)
    Friend ReadOnly Property Field20QuadSlots As New List(Of HclVolumeConstraintQuadSlot_Class)
    Friend ReadOnly Property Field30Entries As New List(Of HclVolumeConstraintVectorEntry_Class)
    Friend ReadOnly Property Field40Batches As New List(Of HclVolumeConstraintBatch_Class)
    Friend ReadOnly Property Field40QuadSlots As New List(Of HclVolumeConstraintQuadSlot_Class)
    Friend ReadOnly Property Field40BridgeSlots As New List(Of HclVolumeConstraintBridgeSlot_Class)
    Friend ReadOnly Property Field40TerminalQuadSlots As New List(Of HclVolumeConstraintQuadSlot_Class)
    Friend ReadOnly Property Field20BridgeSourceQuadSlots As New List(Of HclVolumeConstraintQuadSlot_Class)
    Friend ReadOnly Property Field20NonBridgeQuadSlots As New List(Of HclVolumeConstraintQuadSlot_Class)
    Friend ReadOnly Property Field40BridgeSourceChain As New List(Of HclVolumeConstraintQuadSlot_Class)
    Friend ReadOnly Property Field30ParameterValues As New List(Of Single)
    Friend ReadOnly Property Field50ParameterValues As New List(Of Single)
    Friend ReadOnly Property Field50ToField30PivotMatches As New List(Of HclVolumeConstraintPivotMatch_Class)
    Friend Property Field20MidVectorsLookZeroish As Boolean
    Friend Property Field40MidVectorsLookZeroish As Boolean
    Friend Property Field20BatchUniformParameter As Single?
    Friend Property Field40BatchUniformParameter As Single?
    Friend Property Field20LaneParametersUniformAcrossBatches As Boolean
    Friend Property Field40LaneParametersUniformAcrossBatches As Boolean
    Friend Property Field30UniformParameter As Single?
    Friend Property Field50UniformParameter As Single?
    Friend Property Field20BatchParameterMatchesField30Parameter As Boolean
    Friend Property Field40BatchParameterMatchesField50Parameter As Boolean
    Friend Property Field20AndField40ParametersDistinct As Boolean
    Friend Property HasDistinctParameterGroups As Boolean
    Friend Property Field50PivotReuseOffset As Integer?
    Friend Property Field50PivotReuseCount As Integer
    Friend Property Field40BridgeCountMatchesField50Count As Boolean
    Friend Property Field40BridgeSlotsExact As Boolean
    Friend Property Field40BridgeFormsSequentialChain As Boolean
    Friend Property Field40TerminalExtendsBridgeChain As Boolean
    Friend Property Field40TerminalSharedParticleCount As Integer
    Friend Property Field40TerminalAddedParticleCount As Integer
    Friend Property Field40BridgeSourceChainCount As Integer
    Friend Property Field30LeadCountMatchesField20ExtraActiveQuadCount As Boolean
    Friend Property Field30TailCountMatchesField40BridgeSourceChainCount As Boolean
    Friend Property Field50EntryCountMatchesField40BridgeSourceChainCount As Boolean
    Friend Property Field40NonZeroQuadCount As Integer
    Friend Property Field40ExactBridgeCount As Integer
    Friend Property Field50PivotTailStartIndex As Integer?
    Friend Property Field50MatchesField30Tail As Boolean
    Friend Property Field20NonZeroQuadCount As Integer
    Friend Property Field20NonZeroQuadCountMatchesField30Count As Boolean
    Friend Property Field40NonZeroQuadCountMatchesField50Count As Boolean
    Friend Property Field30LeadEntryCount As Integer
    Friend Property Field30TailEntryCount As Integer
    Friend Property Field40TerminalQuadCount As Integer
    Friend Property Field20ExtraActiveQuadCount As Integer
    Friend Property Field20BridgeSourceQuadCount As Integer
    Friend Property Field20NonBridgeQuadCount As Integer
    Friend Property Field50TailSourceEntryCount As Integer
    Friend Property Field20BridgeSourceAndNonBridgePartitionMatchesActiveQuads As Boolean
    Friend Property Field40BridgeAndTerminalPartitionMatchesActiveQuads As Boolean
    Friend Property Field40BridgeSourceChainMatchesField20BridgeSourceCount As Boolean
    Friend Property Field50TailSourceCountMatchesField50EntryCount As Boolean
    Friend Property Field50TailSourceCountMatchesField30TailEntryCount As Boolean
    Friend ReadOnly Property Field50Entries As New List(Of HclVolumeConstraintVectorEntry_Class)
    Friend ReadOnly Property Field30LeadEntries As New List(Of HclVolumeConstraintVectorEntry_Class)
    Friend ReadOnly Property Field30TailEntries As New List(Of HclVolumeConstraintVectorEntry_Class)
    Friend ReadOnly Property Field50TailSourceEntries As New List(Of HclVolumeConstraintVectorEntry_Class)
End Class

Friend Class HkxVectorStructBlockGraph_Class
    Friend Property EntryIndex As Integer
    Friend Property EntryRelativeOffset As Integer
    Friend Property Vectors As List(Of HkxVector4Graph_Class)
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
    Public Property ParticleA As UShort
    Public Property ParticleB As UShort
    Public Property RestLength As Single
    Public Property Stiffness As Single
End Class

Public Class HclBendConstraintGraph_Class
    Public Property EntryIndex As Integer
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












