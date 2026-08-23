' Version Uploaded of Fo4Library 3.2.0
Option Strict On
Option Explicit On

' =============================================================================
' Parseo de operadores de render/skin del HKX de tela. Lo llama HclClothPackageParser; el
' consumidor final es Wardrobe_Manager/PhysicsWeightCollapseHelper (no la ruta del render).
'
' LO QUE SIGUE SIN CONFIRMAR CONTRA EL SDK DE HAVOK (todo lo demás está medido):
'  - ParseObjectSpaceSkinPNOperator: offsets (+0x10 name, +0x18 header, +0x20 BoneTransforms,
'    +0x30 BoneIndices, +0x48 TransformSubset, …) empíricos. Gap de 8 bytes en +0x40 y entre
'    +0x78 y +0x88 sin identificar.
'  - ParseSimpleMeshBoneDeformOperator: boneIndex = packedBone \ 64 (bits 6-15) y
'    TriangleIndex = packedValue \ 6 son reverse-engineered; internamente consistentes.
'  - ParseWeightedTransformSubset: layout SIMD de 16 lanes asumido. Los tamaños 224/176/128
'    para 4/3/2 blend salen de la fórmula, no de una medición.
' La escala de cuantización SÍ está medida — ver PositionScaleFromW.
' =============================================================================

Imports System.Collections.Generic
Imports System.Linq

Friend NotInheritable Class HclRenderGraphParser_Class
    Friend Shared Function ParseTransformSetDefinition(graph As HkxObjectGraph_Class, source As HkxVirtualObjectGraph_Class) As HclTransformSetDefinitionGraph_Class
        If IsNothing(graph) OrElse IsNothing(source) Then Return Nothing
        If Not source.ClassName.Equals("hclTransformSetDefinition", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        ' Lector generado (HavokTyped.vb): los offsets salen de la reflexion de los dos
        ' .exe y la tabla la elige el packfile. Sin literales que se puedan desincronizar.
        Dim r As New Havok.Canon.Typed.Hk_HclTransformSetDefinition(graph, source)
        If Not r.IsValid Then Return Nothing

        ' ⛔ EL PARSER VIEJO LOS TENIA CRUZADOS. `hclTransformSetDefinition` declara
        ' `+0x10 name`, `+0x18 type`, `+0x1C numTransforms` — y NO declara ningun `numFloatSlots`.
        ' El codigo leia +0x18 (que es `type`) en `TransformCount` y +0x1C (que es `numTransforms`)
        ' en `FloatSlotCount`. Lo destapo la migracion al lector generado, que mapea cada offset a
        ' su nombre declarado. (VB no admite comentarios DENTRO de un inicializador de objeto.)
        Return New HclTransformSetDefinitionGraph_Class With {
            .SourceObject = source,
            .Name = r.Name,
            .TransformCount = CInt(r.NumTransforms),
            .FloatSlotCount = r.Type
        }
    End Function

    Friend Shared Function ParseObjectSpaceSkinPNOperator(graph As HkxObjectGraph_Class, source As HkxVirtualObjectGraph_Class) As HclObjectSpaceSkinPNOperatorGraph_Class
        If IsNothing(graph) OrElse IsNothing(source) Then Return Nothing
        If Not source.ClassName.Equals("hclObjectSpaceSkinPNOperator", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        ' ⭐ LECTOR GENERADO (HavokTyped.vb). El `hclObjectSpaceDeformer` va EMBEBIDO en +0x48 del
        ' operador y el generador lo expone como un sub-lector: `r.ObjectSpaceDeformer.OneBlendEntries`
        ' resuelve solo, sin sumar el offset del struct a mano y sin un literal que se desincronice.
        Dim r As New Havok.Canon.Typed.Hk_HclObjectSpaceSkinPNOperator(graph, source)
        If Not r.IsValid Then Return Nothing
        Dim d = r.ObjectSpaceDeformer
        Dim rel = source.RelativeOffset

        Dim result As New HclObjectSpaceSkinPNOperatorGraph_Class With {
            .SourceObject = source,
            .Name = r.Name,
            .BoneTransformsField = r.BoneFromSkinMeshTransforms,
            .BoneIndicesField = r.TransformSubset,
            .OutputBufferIndex = CInt(r.OutputBufferIndex),
            .TransformSetIndex = CInt(r.TransformSetIndex),
            .TransformSubsetField = d.FourBlendEntries,
            .UnknownStructArrayField = d.ThreeBlendEntries,
            .UnknownSingleStructField = d.TwoBlendEntries,
            .OneBlendSubsetField = d.OneBlendEntries,
            .StartVertexIndex = CUShort(d.StartVertexIndex),
            .EndVertexIndex = CUShort(d.EndVertexIndex),
            .PartialWrite = d.PartialWrite,
            .UnknownBytesField = d.ControlBytes,
            .UnknownLargeStructField = r.LocalPNs,
            .LocalUnpackedPNsField = r.LocalUnpackedPNs
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
        ' ⛔ LA CUARTA FAMILIA. `hclObjectSpaceDeformer` declara CUATRO arrays de entradas
        ' (four/three/two/oneBlendEntries en +0x00/+0x10/+0x20/+0x30 del deformer, o sea
        ' +0x48/+0x58/+0x68/+0x78 del operador) y aca se leian TRES. Los vertices con UNA sola
        ' influencia quedaban SIN skinnear: no entraban al diccionario, asi que la particula que los
        ' usaba caia al DefaultClothPose, que esta en otro espacio.
        ' En HouseDress\Dress.nif no hay bloques de este tipo (medido: tipo0x17 tipo1x4 tipo2x1),
        ' pero eso es una propiedad del ARCHIVO, no del formato.
        result.OneBlendSubsets = ReadOneBlendTransformSubsetArray(graph, result.OneBlendSubsetField)
        result.SkinBlockTypeBytes = If(result.UnknownBytes, Array.Empty(Of Byte)())
        result.LocalBlocks = ReadLocalBlockPNArray(graph, result.UnknownLargeStructField)
        result.SkinBlocks.AddRange(BuildSkinBlocks(result))
        Return result
    End Function

    Friend Shared Function ParseSimpleMeshBoneDeformOperator(graph As HkxObjectGraph_Class,
                                                             source As HkxVirtualObjectGraph_Class,
                                                             Optional skeleton As Havok.Canon.Objects.HkObj_HkaSkeleton = Nothing) As HclSimpleMeshBoneDeformOperatorGraph_Class
        If IsNothing(graph) OrElse IsNothing(source) Then Return Nothing
        If Not source.ClassName.Equals("hclSimpleMeshBoneDeformOperator", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        ' ⭐ LECTOR GENERADO. Los offsets ya no viven aca: salen de `HavokTyped.vb`, que se genera
        ' de la reflexion de los DOS .exe y elige la tabla segun lo que el packfile declara. Es
        ' game-aware por construccion y no hay un solo literal que se pueda desincronizar.
        ' `IsValid` es False cuando el formato no tiene tabla (Skyrim32) o la clase no existe en
        ' ese juego — que es el caso de todas las `hcl` en Skyrim, sin motor de cloth.
        Dim r As New Havok.Canon.Typed.Hk_HclSimpleMeshBoneDeformOperator(graph, source)
        If Not r.IsValid OrElse Not r.HasTriangleBonePairs Then Return Nothing
        Dim rel = source.RelativeOffset

        Dim result As New HclSimpleMeshBoneDeformOperatorGraph_Class With {
            .SourceObject = source,
            .Name = r.Name,
            .InputBufferIndex = CInt(r.InputBufferIdx),
            .OutputTransformSetIndex = CInt(r.OutputTransformSetIdx),
            .MappingField = r.TriangleBonePairs,
            .BindMatrixField = r.LocalBoneTransforms
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

        ' graph.ReadUInt32 es el reader canónico THROWING (bounds-check vía EnsureReadable).
        ' Si un offset HCL empírico cae fuera del contenido, lanza InvalidDataException en vez
        ' de devolver una lista vacía silenciosa (HKX-002/HKX-009).
        For i = 0 To count - 1
            result.Add(graph.ReadUInt32(relativeOffset + (i * 4)))
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

    ''' <summary>
    ''' `hclObjectSpaceDeformerOneBlendEntryBlock`: `vertexIndices` uint16x16 en +0x00 y `boneIndices`
    ''' uint16x16 en +0x20 - 64 bytes, y SIN array de pesos, porque con una sola influencia el peso es
    ''' 1 por definicion. Por eso no puede usar <see cref="ParseWeightedTransformSubset"/>, que calcula
    ''' el tamano como `32 + n*32 + 16*n` (con n=1 daria 80 y leeria pesos que no existen).
    ''' </summary>
    Private Shared Function ReadOneBlendTransformSubsetArray(graph As HkxObjectGraph_Class,
                                                             field As HkxObjectArrayHeader_Class) As List(Of HclObjectSpaceSkinTransformSubsetGraph_Class)
        Dim result As New List(Of HclObjectSpaceSkinTransformSubsetGraph_Class)
        For Each raw In ReadRawStructArray(graph, field, 64)
            If IsNothing(raw) OrElse IsNothing(raw.RawBytes) OrElse raw.RawBytes.Length < 64 Then Continue For
            Dim subset As New HclObjectSpaceSkinTransformSubsetGraph_Class With {
                .EntryIndex = raw.EntryIndex,
                .EntryRelativeOffset = raw.EntryRelativeOffset,
                .RawStruct = raw,
                .RawBytes = raw.RawBytes,
                .InfluenceCount = 1
            }
            subset.InfluenceIndexGroups.Add(New List(Of UShort))
            For lane = 0 To 15
                Dim vertexIndex = BitConverter.ToUInt16(raw.RawBytes, lane * 2)
                subset.VertexIndices.Add(vertexIndex)
                Dim transformIndex = BitConverter.ToUInt16(raw.RawBytes, 32 + (lane * 2))
                subset.InfluenceIndexGroups(0).Add(transformIndex)
                Dim laneInfo As New HclObjectSpaceSkinVertexInfluenceGraph_Class With {
                    .LaneIndex = lane,
                    .VertexIndex = vertexIndex,
                    .InfluenceCount = 1
                }
                laneInfo.TransformIndices.Add(transformIndex)
                laneInfo.WeightBytes.Add(CByte(255))
                laneInfo.WeightByteSum = 255
                subset.VertexInfluences.Add(laneInfo)
            Next
            result.Add(subset)
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
            result.DecodedPositions.Add(DecodeQuantizedVector3(result.Lanes(lane).VectorAInt16Values, PositionScaleFromW(result.Lanes(lane).VectorAInt16Values), lane, 0))
            result.DecodedPositions.Add(DecodeQuantizedVector3(result.Lanes(lane).VectorBInt16Values, PositionScaleFromW(result.Lanes(lane).VectorBInt16Values), lane, 1))
        Next

        If Logger.Enabled Then
            ' Histograma de los tags `w` del vector de POSICION. La regla actual mira UN bit y ofrece
            ' dos escalas; si aca aparecen mas de dos valores distintos, la regla esta incompleta por
            ' construccion y no hace falta discutirlo.
            Dim ws As New List(Of String)
            For lane = 0 To Math.Min(7, result.Lanes.Count - 1)
                ws.Add("0x" & (result.Lanes(lane).VectorAInt16Values(3) And &HFFFF).ToString("X4"))
                ws.Add("0x" & (result.Lanes(lane).VectorBInt16Values(3) And &HFFFF).ToString("X4"))
            Next
            Dim wl = ws
            Logger.LogLazy(Function() "[CLOTH-WTAG] " & String.Join(",", wl))
        End If

        ' ⛔⛔ LA NORMAL SE DECODIFICA COMO LA POSICION, NO CON UN 32767 INVENTADO.
        '
        ' Aca habia `32767.0R` fijo — el maximo de un int16 — que es lo que uno supondria si las
        ' normales vinieran normalizadas a escala completa. NO ES LO QUE HACE EL MOTOR. En el deformer
        ' que consume estos bloques (`hclObjectSpaceSkinPNOperator` -> 0x14193C5E0) las UNICAS dos
        ' constantes que multiplican son `65536.0` (0x14262BA50) y `1/255` (0x142492850): la primera es
        ' el `<< 16` del reinterpretado y la segunda es la normalizacion de los PESOS. No hay ningun
        ' 32767 en el camino.
        '
        ' O sea que la normal usa el MISMO esquema que la posicion — `float(v << 16) x
        ' bitcast_float(w << 16)`, con el factor por-bloque en el cuarto int16 — y por eso comparte
        ' `PositionScaleFromW`.
        '
        ' LO QUE COSTABA, medido: con el 32767 la magnitud de la normal ya skinneada caia en
        ' [0,50 .. 1,00] en vez de ~1, con 0,5 EXACTOS en parte de los vertices. Eso importa porque la
        ' correa (`hclLocalRangeConstraintSet`) compara `minNormalDistance` y `maxNormalDistance`
        ' contra una proyeccion ESCALADA por |n|, y el motor no la re-normaliza: con |n| = 0,5 los dos
        ' limites actuaban al DOBLE de la distancia que declara el archivo.
        For lane = 8 To Math.Min(15, result.Lanes.Count - 1)
            Dim va = result.Lanes(lane).VectorAInt16Values
            Dim vb = result.Lanes(lane).VectorBInt16Values
            result.DecodedNormals.Add(DecodeQuantizedVector3(va, PositionScaleFromW(va), lane, 0))
            result.DecodedNormals.Add(DecodeQuantizedVector3(vb, PositionScaleFromW(vb), lane, 1))
        Next

        Return result
    End Function

    ''' <summary>
    ''' Dequantizacion de la POSICION del bloque local del ObjectSpaceDeformer.
    '''
    ''' <para>⭐ LEIDA DEL MOTOR, no ajustada al dato. `TtObject Space Deform` @0x141939390, bucle de
    ''' cuatro influencias en 0x1419399E0:</para>
    ''' <code>
    '''   movsd     xmm0, [ptr]        ; 8 bytes = los 4 int16 del vertice (x,y,z,w)
    '''   punpcklwd xmm2, xmm0         ; xmm2=0 ⇒ cada int16 queda en los 16 bits ALTOS de un dword
    '''   pshufd    xmm0, xmm2, 0xFF   ; lane 3 (el tag `w`) broadcast, SIN convertir
    '''   cvtdq2ps  xmm1, xmm2         ; float(v &lt;&lt; 16)
    '''   mulps     xmm1, xmm0         ; × el tag interpretado como PATRON DE BITS de un float
    ''' </code>
    ''' <para>O sea: <c>valor = float(v &lt;&lt; 16) × bitcast_float(w &lt;&lt; 16)</c>. El `w` NO es un flag:
    ''' es la MITAD ALTA de un float IEEE-754 (mantisa baja en cero) que multiplica al vector entero.
    ''' Es el truco clasico para guardar un exponente en 16 bits.</para>
    '''
    ''' <para>⛔ LO QUE HABIA ACA ESTABA ADIVINADO y por eso rompia. Decia: <i>"la escala es per-vertice
    ''' {256, 512}, seleccionada por el bit 7"</i> — un ajuste de DOS puntos (w=0x3380→256,
    ''' w=0x3300→512) presentado como ley. Los dos casos salen bien con la formula real
    ''' (bitcast(0x33800000)=2^-24, ×65536 = 1/256 ✓ ; bitcast(0x33000000)=2^-25 ⇒ 1/512 ✓), pero
    ''' cualquier otro exponente se decodificaba con la escala de al lado. MEDIDO en
    ''' HouseDress\Dress.nif: 24 de 321 particulas salian con la posicion ×4 EXACTO (un exponente de
    ''' diferencia, 1/1024 leido como 1/256), y con UNA sola alcanzaba para que el triangulo del
    ''' cloth-bone del ruedo quedara convertido en una astilla de 100 unidades y la pollera se abriera
    ''' en abanico.</para>
    ''' <para>Las NORMALES son otra cosa: van con 32767 fijo (ver el llamador).</para>
    ''' </summary>
    Private Shared Function PositionScaleFromW(values As IReadOnlyList(Of Short)) As Double
        If values Is Nothing OrElse values.Count < 4 Then Return 256.0R
        ' El multiplicador que aplica el motor: bitcast_float(w << 16) escalado por el << 16 de los datos.
        Dim mul = CDbl(BitConverter.Int32BitsToSingle(CInt(values(3)) << 16)) * 65536.0R
        ' `DecodeQuantizedVector3` DIVIDE, asi que se devuelve el reciproco. Un tag de cero (o
        ' desnormalizado) daria una escala infinita: se cae al 256 historico antes que emitir infinitos.
        If mul <= 0.0R OrElse Double.IsNaN(mul) OrElse Double.IsInfinity(mul) Then Return 256.0R
        Return 1.0R / mul
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
        Dim oneBlendIndex = 0
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
                Case 3
                    blendCount = 1
                    blockTypeName = "one-blend"
                    If oneBlendIndex < source.OneBlendSubsets.Count Then
                        subset = source.OneBlendSubsets(oneBlendIndex)
                        oneBlendIndex += 1
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
                    ' ⛔⛔ RELLENO DEL BLOQUE PARCIAL. Los bloques son de 16 vertices FIJOS y el ultimo de
                    ' cada familia viene a medias: Havok rellena los slots sobrantes REPITIENDO el ultimo
                    ' indice valido. MEDIDO en HouseDress\Dress.nif: el bloque 20 termina en
                    ' `...,336,337,337,337,337` y el 21 en `338,339,339,...` (nueve veces 339).
                    ' Escribirlos igual PISA la entrada buena de ese vertice con la posicion local de un
                    ' slot de relleno, y con UNO alcanza para que el triangulo de un cloth-bone quede
                    ' convertido en una astilla de 100 unidades.
                    ' ⛔ Filtrar por startVertexIndex/endVertexIndex NO alcanza: el relleno repite un
                    ' indice que esta DENTRO del rango (339 <= endVertexIndex = 339). La senal es la
                    ' REPETICION, no el rango: un vertice no puede aparecer dos veces en un deformer.
                    If slot > 0 AndAlso subset.VertexIndices(slot) = subset.VertexIndices(slot - 1) Then Exit For
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

        If Logger.Enabled Then
            Dim hist As New Dictionary(Of Integer, Integer)
            For Each bt In blockTypeBytes
                Dim k = CInt(bt)
                hist(k) = If(hist.ContainsKey(k), hist(k) + 1, 1)
            Next
            Dim h = String.Join(" ", hist.OrderBy(Function(kv) kv.Key).Select(Function(kv) $"tipo{kv.Key}x{kv.Value}"))
            ' Volcado de los DOS ultimos bloques: es donde vive el relleno, y ver los indices crudos
            ' es lo unico que dice si el relleno repite, pone cero o trae basura.
            For Each b3 In result.Skip(Math.Max(0, result.Count - 2))
                Dim idxs = String.Join(",", b3.VertexEntries.Select(Function(e) e.VertexIndex.ToString()))
                Dim bi = b3.BlockIndex
                Dim bt = b3.BlockTypeName
                Logger.LogLazy(Function() $"[CLOTH-SKINBLK-LAST] bloque {bi} ({bt}) idx={idxs}")
            Next
            Dim sv = source.StartVertexIndex, ev = source.EndVertexIndex
            Logger.LogLazy(Function() $"[CLOTH-SKINBLK-RANGE] startVertexIndex={sv} endVertexIndex={ev}")
            Dim lb = source.LocalBlocks.Count
            Dim c4 = source.TransformSubsets.Count, c3 = source.ThreeBlendSubsets.Count, c2 = source.TwoBlendSubsets.Count
            Dim sinSubset = result.Where(Function(b2) b2.InfluenceBlock Is Nothing).Count
            Logger.LogLazy(Function() $"[CLOTH-SKINBLK] bloques={blockCount} localBlocks={lb} " &
                           $"subsets 4/3/2 = {c4}/{c3}/{c2} · controlBytes: {h} · bloques SIN subset={sinSubset}")
        End If

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

    Private Shared Function ReadByteArray(graph As HkxObjectGraph_Class, field As HkxObjectArrayHeader_Class) As Byte()
        If IsNothing(field) OrElse field.Count <= 0 OrElse field.DataRelativeOffset < 0 Then Return Array.Empty(Of Byte)()
        Return graph.ReadBytes(field.DataRelativeOffset, field.Count)
    End Function

    Private Shared Function ReadMatrix4(graph As HkxObjectGraph_Class, relativeOffset As Integer) As HkxMatrix4Graph_Class
        Dim values(15) As Single
        For i = 0 To 15
            values(i) = graph.ReadSingle(relativeOffset + (i * 4))
        Next

        Return New HkxMatrix4Graph_Class With {
            .RelativeOffset = relativeOffset,
            .Values = values
        }
    End Function

    ' Thin uint16 wrapper sobre el reader canónico THROWING graph.ReadInt16 (no existe un
    ' graph.ReadUInt16 escalar). Convierte a unsigned sin sign-extension. Un offset HCL
    ' (empírico) fuera del contenido lanza InvalidDataException — NO devuelve 0 silencioso
    ' (HKX-002/HKX-009).
    Private Shared Function ReadUInt16(graph As HkxObjectGraph_Class, relativeOffset As Integer) As UShort
        Return CUShort(CInt(graph.ReadInt16(relativeOffset)) And &HFFFF)
    End Function
End Class

Public Class HclTransformSetDefinitionGraph_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Name As String
    Public Property TransformCount As Integer
    Public Property FloatSlotCount As Integer
End Class

Public Class HclObjectSpaceSkinPNOperatorGraph_Class
    ''' <summary>
    ''' `hclObjectSpaceDeformer.startVertexIndex` / `endVertexIndex` (+0x98 / +0x9A del operador; la
    ''' reflexion los declara en +0x50/+0x52 del deformer, que va embebido en +0x48).
    ''' <para>⛔ EXISTEN PORQUE EL ULTIMO BLOQUE ES PARCIAL. Los bloques son de 16 vertices fijos, asi
    ''' que el ultimo trae RELLENO: slots cuyo `vertexIndex` no corresponde a ningun vertice de este
    ''' deformer. Escribirlos igual pisa entradas legitimas con posiciones de otro lado.</para>
    ''' </summary>
    ''' <summary>`outputBufferIndex` (+0x40) y `transformSetIndex` (+0x44) del operador base.</summary>
    Public Property OutputBufferIndex As Integer = -1
    Public Property TransformSetIndex As Integer = -1
    ''' <summary>`partialWrite` (+0x56 del deformer): el ultimo bloque escribe menos de 16 vertices.</summary>
    Public Property PartialWrite As Boolean
    ''' <summary>`localUnpackedPNs` (+0xB0): variante SIN cuantizar de los bloques locales. En el corpus
    ''' de FO4 viene vacia (se usa `localPNs`), pero el formato la declara y no leerla dejaba un camino
    ''' entero del formato invisible.</summary>
    Public Property LocalUnpackedPNsField As HkxObjectArrayHeader_Class
    ''' <summary>`hclObjectSpaceDeformer.oneBlendEntries` (+0x30 del deformer = +0x78 del operador).</summary>
    Public Property OneBlendSubsetField As HkxObjectArrayHeader_Class
    Public Property OneBlendSubsets As New List(Of HclObjectSpaceSkinTransformSubsetGraph_Class)
    Public Property StartVertexIndex As UShort
    Public Property EndVertexIndex As UShort

    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Name As String
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
    ''' <summary>`inputBufferIdx` (+0x20): que buffer de la escena lee este operador. Lo declara el
    ''' formato y hasta ahora no se leia; con mas de un buffer por config es lo que dice cual.</summary>
    Public Property InputBufferIndex As Integer = -1
    ''' <summary>`outputTransformSetIdx` (+0x24): en que transform-set escribe los huesos.</summary>
    Public Property OutputTransformSetIndex As Integer = -1
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Name As String
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








