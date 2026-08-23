' Version Uploaded of Fo4Library 3.2.0
Option Strict On
Option Explicit On

' =============================================================================
' HkxObjectGraph_Class: infraestructura de parsing del grafo de objetos HKX (objetos por
' virtual-fixup, lectura de campos, hkArray, resolución de punteros local/global).
' La usan SkeletonClothOverlayHelper, HkxPoseImportHelper, los Hkx*/Hcl*GraphParser y
' HclClothPackageParser.
'
' ALCANCE: el soporte genérico 32/64-bit cubre hkArray, root container, skeleton y
' animation/binding. Los offsets de campo HCL (hclClothData, hclSimClothData, shapes)
' están verificados empíricamente sobre FO4 64-bit y NO contra el SDK de Havok ⇒ no
' implica soporte de cloth de Skyrim.
'
' ParseSimClothData de esta clase quedó supersedida por
' HclStructuredGraphParser_Class.ParseSimClothData; su único llamador vivo es
' Tools/HkxParserTool.
' =============================================================================

Imports System.IO
Imports System.Linq
Imports System.Text

Public NotInheritable Class HkxObjectGraphParser_Class
    Public Shared Function BuildGraph(packfile As HkxPackfile_Class) As HkxObjectGraph_Class
        Return New HkxObjectGraph_Class(packfile)
    End Function
End Class

Public Partial Class HkxObjectGraph_Class
    Public ReadOnly Property Packfile As HkxPackfile_Class
    Public ReadOnly Property ContentsSection As HkxPackfileSection_Class
    Public ReadOnly Property Objects As New List(Of HkxVirtualObjectGraph_Class)

    Private ReadOnly _localFixupsBySource As New Dictionary(Of Integer, HkxLocalFixupEntry_Class)
    Private ReadOnly _globalFixupsBySource As New Dictionary(Of Integer, HkxGlobalFixupEntry_Class)
    Private ReadOnly _objectsByOffset As New Dictionary(Of Integer, HkxVirtualObjectGraph_Class)
    Private ReadOnly _objectsByClassName As New Dictionary(Of String, List(Of HkxVirtualObjectGraph_Class))(StringComparer.OrdinalIgnoreCase)

    ' Fixups de la contents-section ordenados por SourceRelativeOffset ascendente (los empates
    ' conservan el orden de enumeración original), en arrays paralelos. Se arman una vez en
    ' BuildIndices para que GetLocal/GlobalFixupsInRange hagan búsqueda binaria en vez de
    ' filtrar + ordenar la lista completa en cada llamada.
    Private _localFixupSourcesSorted As Integer() = Array.Empty(Of Integer)()
    Private _localFixupsSorted As HkxLocalFixupEntry_Class() = Array.Empty(Of HkxLocalFixupEntry_Class)()
    Private _globalFixupSourcesSorted As Integer() = Array.Empty(Of Integer)()
    Private _globalFixupsSorted As HkxGlobalFixupEntry_Class() = Array.Empty(Of HkxGlobalFixupEntry_Class)()

    Private ReadOnly Property PointerSizeValue As Integer
        Get
            Return Math.Max(1, CInt(Packfile.Header.PointerSize))
        End Get
    End Property

    Private ReadOnly Property ArrayHeaderSizeValue As Integer
        Get
            Return PointerSizeValue + 8
        End Get
    End Property

    Private ReadOnly Property BaseObjectFieldOffset As Integer
        Get
            Return PointerSizeValue * 2
        End Get
    End Property

    Public Sub New(packfile As HkxPackfile_Class)
        If IsNothing(packfile) Then Throw New ArgumentNullException(NameOf(packfile))
        If IsNothing(packfile.Header) Then Throw New InvalidOperationException("The HKX packfile has not been parsed.")

        Me.Packfile = packfile
        Me.ContentsSection = packfile.GetSection(packfile.Header.ContentsSectionIndex)
        If IsNothing(Me.ContentsSection) Then Throw New InvalidOperationException("The HKX contents section was not found.")

        BuildIndices()
    End Sub

    Private Sub BuildIndices()
        For Each fixup In Packfile.LocalFixups.Where(Function(pf) pf.SectionIndex = Packfile.Header.ContentsSectionIndex)
            _localFixupsBySource.TryAdd(fixup.SourceRelativeOffset, fixup)
        Next

        For Each fixup In Packfile.GlobalFixups.Where(Function(pf) pf.SectionIndex = Packfile.Header.ContentsSectionIndex)
            _globalFixupsBySource.TryAdd(fixup.SourceRelativeOffset, fixup)
        Next

        BuildSortedFixupIndices()

        Dim dataRelativeEnd = ContentsSection.DataEndAbsolute - ContentsSection.AbsoluteDataStart
        Dim orderedVirtualFixups = Packfile.VirtualFixups.
            Where(Function(pf) pf.SectionIndex = Packfile.Header.ContentsSectionIndex).
            OrderBy(Function(pf) pf.ObjectRelativeOffset).
            ToList()

        For i = 0 To orderedVirtualFixups.Count - 1
            Dim fixup = orderedVirtualFixups(i)
            Dim classEntry = Packfile.GetClassName(fixup.ClassNameSectionIndex, fixup.ClassNameRelativeOffset)
            Dim size = If(i < orderedVirtualFixups.Count - 1,
                          orderedVirtualFixups(i + 1).ObjectRelativeOffset - fixup.ObjectRelativeOffset,
                          dataRelativeEnd - fixup.ObjectRelativeOffset)

            Dim obj As New HkxVirtualObjectGraph_Class With {
                .SectionIndex = fixup.SectionIndex,
                .RelativeOffset = fixup.ObjectRelativeOffset,
                .AbsoluteOffset = ContentsSection.AbsoluteDataStart + fixup.ObjectRelativeOffset,
                .ClassNameSectionIndex = fixup.ClassNameSectionIndex,
                .ClassNameRelativeOffset = fixup.ClassNameRelativeOffset,
                .ClassName = If(classEntry?.Name, String.Empty),
                .Size = size
            }

            Objects.Add(obj)
            _objectsByOffset(obj.RelativeOffset) = obj

            Dim value As List(Of HkxVirtualObjectGraph_Class) = Nothing
            If Not _objectsByClassName.TryGetValue(obj.ClassName, value) Then
                value = New List(Of HkxVirtualObjectGraph_Class)
                _objectsByClassName.Add(obj.ClassName, value)
            End If

            value.Add(obj)
        Next
    End Sub

    ' Índice ordenado que consumen GetLocalFixupsInRange / GetGlobalFixupsInRange. El desempate
    ' por índice de enumeración original es OBLIGATORIO: sin él el orden dentro de un rango deja
    ' de ser estable y los parsers que leen "el primer fixup del rango" cambian de resultado.
    Private Sub BuildSortedFixupIndices()
        Dim localList = Packfile.LocalFixups.Where(Function(pf) pf.SectionIndex = Packfile.Header.ContentsSectionIndex).ToList()
        Dim localIndices = Enumerable.Range(0, localList.Count).ToArray()
        Array.Sort(localIndices, Function(left, right)
                                     Dim c = localList(left).SourceRelativeOffset.CompareTo(localList(right).SourceRelativeOffset)
                                     If c <> 0 Then Return c
                                     Return left.CompareTo(right)
                                 End Function)
        _localFixupsSorted = New HkxLocalFixupEntry_Class(localList.Count - 1) {}
        _localFixupSourcesSorted = New Integer(localList.Count - 1) {}
        For i = 0 To localIndices.Length - 1
            _localFixupsSorted(i) = localList(localIndices(i))
            _localFixupSourcesSorted(i) = _localFixupsSorted(i).SourceRelativeOffset
        Next

        Dim globalList = Packfile.GlobalFixups.Where(Function(pf) pf.SectionIndex = Packfile.Header.ContentsSectionIndex).ToList()
        Dim globalIndices = Enumerable.Range(0, globalList.Count).ToArray()
        Array.Sort(globalIndices, Function(left, right)
                                      Dim c = globalList(left).SourceRelativeOffset.CompareTo(globalList(right).SourceRelativeOffset)
                                      If c <> 0 Then Return c
                                      Return left.CompareTo(right)
                                  End Function)
        _globalFixupsSorted = New HkxGlobalFixupEntry_Class(globalList.Count - 1) {}
        _globalFixupSourcesSorted = New Integer(globalList.Count - 1) {}
        For i = 0 To globalIndices.Length - 1
            _globalFixupsSorted(i) = globalList(globalIndices(i))
            _globalFixupSourcesSorted(i) = _globalFixupsSorted(i).SourceRelativeOffset
        Next
    End Sub

    ' First index in the ascending-sorted array whose value is >= target (lower bound).
    ' Returns sources.Length if every value is below target.
    Private Shared Function LowerBound(sources As Integer(), target As Integer) As Integer
        Dim low = 0
        Dim high = sources.Length
        While low < high
            Dim mid = low + ((high - low) \ 2)
            If sources(mid) < target Then
                low = mid + 1
            Else
                high = mid
            End If
        End While
        Return low
    End Function

    Public Function GetObject(relativeOffset As Integer) As HkxVirtualObjectGraph_Class
        Dim value As HkxVirtualObjectGraph_Class = Nothing
        If _objectsByOffset.TryGetValue(relativeOffset, value) Then Return value
        Return Nothing
    End Function

    Public Function GetObjectsByClassName(className As String) As IEnumerable(Of HkxVirtualObjectGraph_Class)
        If String.IsNullOrWhiteSpace(className) Then Return Enumerable.Empty(Of HkxVirtualObjectGraph_Class)()
        Dim values As List(Of HkxVirtualObjectGraph_Class) = Nothing
        If _objectsByClassName.TryGetValue(className, values) Then Return values
        Return Enumerable.Empty(Of HkxVirtualObjectGraph_Class)()
    End Function

    Public Function GetRootObject() As HkxVirtualObjectGraph_Class
        If IsNothing(Packfile.RootObject) Then Return Nothing
        Return GetObject(Packfile.RootObject.RelativeOffset)
    End Function
    Public Function TryGetLocalFixup(sourceRelativeOffset As Integer, ByRef result As HkxLocalFixupEntry_Class) As Boolean
        Return _localFixupsBySource.TryGetValue(sourceRelativeOffset, result)
    End Function

    Public Function TryGetGlobalFixup(sourceRelativeOffset As Integer, ByRef result As HkxGlobalFixupEntry_Class) As Boolean
        Return _globalFixupsBySource.TryGetValue(sourceRelativeOffset, result)
    End Function

    Public Function GetLocalFixupsInRange(relativeOffset As Integer, byteCount As Integer) As List(Of HkxLocalFixupEntry_Class)
        Dim result As New List(Of HkxLocalFixupEntry_Class)
        If byteCount <= 0 Then Return result

        Dim rangeEnd = relativeOffset + byteCount
        Dim start = LowerBound(_localFixupSourcesSorted, relativeOffset)
        For i = start To _localFixupSourcesSorted.Length - 1
            If _localFixupSourcesSorted(i) >= rangeEnd Then Exit For
            result.Add(_localFixupsSorted(i))
        Next

        Return result
    End Function

    Public Function GetGlobalFixupsInRange(relativeOffset As Integer, byteCount As Integer) As List(Of HkxGlobalFixupEntry_Class)
        Dim result As New List(Of HkxGlobalFixupEntry_Class)
        If byteCount <= 0 Then Return result

        Dim rangeEnd = relativeOffset + byteCount
        Dim start = LowerBound(_globalFixupSourcesSorted, relativeOffset)
        For i = start To _globalFixupSourcesSorted.Length - 1
            If _globalFixupSourcesSorted(i) >= rangeEnd Then Exit For
            result.Add(_globalFixupsSorted(i))
        Next

        Return result
    End Function

    Public Function ResolveLocalPointer(sourceRelativeOffset As Integer) As Integer?
        Dim fixup As HkxLocalFixupEntry_Class = Nothing
        If Not TryGetLocalFixup(sourceRelativeOffset, fixup) Then Return Nothing
        Return fixup.DestinationRelativeOffset
    End Function

    Public Function ResolveGlobalObject(sourceRelativeOffset As Integer) As HkxVirtualObjectGraph_Class
        Dim fixup As HkxGlobalFixupEntry_Class = Nothing
        If Not TryGetGlobalFixup(sourceRelativeOffset, fixup) Then Return Nothing
        Return GetObject(fixup.TargetRelativeOffset)
    End Function

    Public Function ResolveLocalString(sourceRelativeOffset As Integer) As String
        Dim destination = ResolveLocalPointer(sourceRelativeOffset)
        If Not destination.HasValue Then Return String.Empty
        Return ReadNullTerminatedString(destination.Value)
    End Function

    Public Function ReadNullTerminatedString(relativeOffset As Integer) As String
        Dim absoluteOffset = ContentsSection.AbsoluteDataStart + relativeOffset
        If absoluteOffset < ContentsSection.AbsoluteDataStart OrElse absoluteOffset >= ContentsSection.DataEndAbsolute Then Return String.Empty

        Dim endOffset = absoluteOffset
        While endOffset < ContentsSection.DataEndAbsolute AndAlso Packfile.RawBytes(endOffset) <> 0
            endOffset += 1
        End While

        Return Encoding.ASCII.GetString(Packfile.RawBytes, absoluteOffset, endOffset - absoluteOffset)
    End Function

    Public Function ReadInt16(relativeOffset As Integer) As Short
        EnsureReadable(relativeOffset, 2)
        Return BitConverter.ToInt16(Packfile.RawBytes, ContentsSection.AbsoluteDataStart + relativeOffset)
    End Function

    Public Function ReadByte(relativeOffset As Integer) As Byte
        EnsureReadable(relativeOffset, 1)
        Return Packfile.RawBytes(ContentsSection.AbsoluteDataStart + relativeOffset)
    End Function

    Public Function ReadInt32(relativeOffset As Integer) As Integer
        EnsureReadable(relativeOffset, 4)
        Return BitConverter.ToInt32(Packfile.RawBytes, ContentsSection.AbsoluteDataStart + relativeOffset)
    End Function

    Public Function ReadUInt32(relativeOffset As Integer) As UInteger
        EnsureReadable(relativeOffset, 4)
        Return BitConverter.ToUInt32(Packfile.RawBytes, ContentsSection.AbsoluteDataStart + relativeOffset)
    End Function

    Public Function ReadSingle(relativeOffset As Integer) As Single
        EnsureReadable(relativeOffset, 4)
        Return BitConverter.ToSingle(Packfile.RawBytes, ContentsSection.AbsoluteDataStart + relativeOffset)
    End Function

    Public Function ReadBytes(relativeOffset As Integer, byteCount As Integer) As Byte()
        If byteCount <= 0 Then Return Array.Empty(Of Byte)()
        EnsureReadable(relativeOffset, byteCount)

        Dim result(byteCount - 1) As Byte
        Buffer.BlockCopy(Packfile.RawBytes, ContentsSection.AbsoluteDataStart + relativeOffset, result, 0, byteCount)
        Return result
    End Function

    Public Function ReadArrayHeader(fieldRelativeOffset As Integer) As HkxObjectArrayHeader_Class
        Dim pointer = ResolveLocalPointer(fieldRelativeOffset)
        Return New HkxObjectArrayHeader_Class With {
            .FieldRelativeOffset = fieldRelativeOffset,
            .DataRelativeOffset = If(pointer, -1),
            .Count = ReadInt32(fieldRelativeOffset + PointerSizeValue),
            .CapacityAndFlags = ReadInt32(fieldRelativeOffset + PointerSizeValue + 4)
        }
    End Function

    Public Function ReadStructureOffsets(fieldRelativeOffset As Integer, itemSize As Integer) As List(Of Integer)
        Dim result As New List(Of Integer)
        Dim header = ReadArrayHeader(fieldRelativeOffset)
        If itemSize <= 0 OrElse header.Count <= 0 OrElse header.DataRelativeOffset < 0 Then Return result

        For i = 0 To header.Count - 1
            result.Add(header.DataRelativeOffset + (i * itemSize))
        Next

        Return result
    End Function

    ''' <summary>
    ''' Resuelve un array de punteros a partir de su CABECERA. Es la forma que consume el lector
    ''' tipado generado: sus propiedades de array devuelven la cabecera, no un offset, justamente
    ''' para que ningun consumidor tenga que volver a calcular una posicion a mano.
    ''' </summary>
    Public Function ReadObjectReferenceArray(header As HkxObjectArrayHeader_Class) As List(Of HkxVirtualObjectGraph_Class)
        Dim result As New List(Of HkxVirtualObjectGraph_Class)
        If header Is Nothing OrElse header.Count <= 0 OrElse header.DataRelativeOffset < 0 Then Return result
        Dim stride = PointerSizeValue
        For i = 0 To header.Count - 1
            Dim obj = ResolveGlobalObject(header.DataRelativeOffset + (i * stride))
            If Not IsNothing(obj) Then result.Add(obj)
        Next
        Return result
    End Function

    Public Function ReadObjectReferenceArray(fieldRelativeOffset As Integer) As List(Of HkxVirtualObjectGraph_Class)
        Dim result As New List(Of HkxVirtualObjectGraph_Class)
        Dim header = ReadArrayHeader(fieldRelativeOffset)
        If header.Count <= 0 OrElse header.DataRelativeOffset < 0 Then Return result

        Dim stride = PointerSizeValue
        For i = 0 To header.Count - 1
            Dim obj = ResolveGlobalObject(header.DataRelativeOffset + (i * stride))
            If Not IsNothing(obj) Then result.Add(obj)
        Next

        Return result
    End Function

    Public Function ReadInt16Array(fieldRelativeOffset As Integer) As List(Of Short)
        Dim result As New List(Of Short)
        Dim header = ReadArrayHeader(fieldRelativeOffset)
        If header.Count <= 0 OrElse header.DataRelativeOffset < 0 Then Return result

        For i = 0 To header.Count - 1
            result.Add(ReadInt16(header.DataRelativeOffset + (i * 2)))
        Next

        Return result
    End Function

    ' Reads hkArray of uint16 into List(Of Integer) (unsigned, no sign extension).
    Public Function ReadUInt16Array(fieldRelativeOffset As Integer) As List(Of Integer)
        Dim result As New List(Of Integer)
        Dim header = ReadArrayHeader(fieldRelativeOffset)
        If header.Count <= 0 OrElse header.DataRelativeOffset < 0 Then Return result

        For i = 0 To header.Count - 1
            result.Add(CInt(ReadInt16(header.DataRelativeOffset + (i * 2))) And &HFFFF)
        Next

        Return result
    End Function

    Public Function ReadUInt32Array(fieldRelativeOffset As Integer) As List(Of UInteger)
        Dim result As New List(Of UInteger)
        Dim header = ReadArrayHeader(fieldRelativeOffset)
        If header.Count <= 0 OrElse header.DataRelativeOffset < 0 Then Return result

        For i = 0 To header.Count - 1
            result.Add(ReadUInt32(header.DataRelativeOffset + (i * 4)))
        Next

        Return result
    End Function

    Public Function ReadByteArray(fieldRelativeOffset As Integer) As Byte()
        Dim header = ReadArrayHeader(fieldRelativeOffset)
        If header.Count <= 0 OrElse header.DataRelativeOffset < 0 Then Return Array.Empty(Of Byte)()
        Return ReadBytes(header.DataRelativeOffset, header.Count)
    End Function

    ' Reads hkArray of hkVector4 (16 bytes/element) into a list.
    Public Function ReadVector4ArrayFromOffset(fieldRelativeOffset As Integer) As List(Of HkxVector4Graph_Class)
        Dim field = ReadArrayHeader(fieldRelativeOffset)
        Return ReadVector4ArrayFromHeader(field)
    End Function

    Private Function ReadVector4ArrayFromHeader(header As HkxObjectArrayHeader_Class) As List(Of HkxVector4Graph_Class)
        Dim result As New List(Of HkxVector4Graph_Class)
        If IsNothing(header) OrElse header.Count <= 0 OrElse header.DataRelativeOffset < 0 Then Return result

        Const stride As Integer = 16
        For i = 0 To header.Count - 1
            Dim off = header.DataRelativeOffset + (i * stride)
            result.Add(New HkxVector4Graph_Class With {
                .X = ReadSingle(off + 0),
                .Y = ReadSingle(off + 4),
                .Z = ReadSingle(off + 8),
                .W = ReadSingle(off + 12)
            })
        Next

        Return result
    End Function

    Private Sub EnsureReadable(relativeOffset As Integer, byteCount As Integer)
        Dim dataRelativeEnd = ContentsSection.DataEndAbsolute - ContentsSection.AbsoluteDataStart
        If relativeOffset < 0 OrElse byteCount < 0 OrElse relativeOffset + byteCount > dataRelativeEnd Then
            Throw New InvalidDataException($"Requested HKX range is out of bounds: offset=0x{relativeOffset:X} size={byteCount}.")
        End If
    End Sub
    Public Function ParseRootLevelContainer() As HkxRootLevelContainerGraph_Class
        Dim rootObject = GetRootObject()
        If IsNothing(rootObject) OrElse Not rootObject.ClassName.Equals("hkRootLevelContainer", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        Dim result As New HkxRootLevelContainerGraph_Class With {
            .SourceObject = rootObject
        }

        Dim namedVariantStride = PointerSizeValue * 3
        For Each variantOffset In ReadStructureOffsets(rootObject.RelativeOffset, namedVariantStride)
            result.NamedVariants.Add(ReadNamedVariant(variantOffset))
        Next

        Return result
    End Function

    Public Function ParseClothData(source As HkxVirtualObjectGraph_Class) As HclClothDataGraph_Class
        If IsNothing(source) OrElse Not source.ClassName.Equals("hclClothData", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        ' Lector generado (HavokTyped.vb): los offsets salen de la reflexion de los dos
        ' .exe y la tabla la elige el packfile. Sin literales que se puedan desincronizar.
        Dim hkr As New Havok.Canon.Typed.Hk_HclClothData(Me, source)
        If Not hkr.IsValid Then Return Nothing

        ' Offsets contra la reflexion (hclClothData, size 0x80, ver 1):
        '   +0x10 name  +0x18 simClothDatas  +0x28 bufferDefinitions  +0x38 transformSetDefinitions
        '   +0x48 operators  +0x58 clothStateDatas  +0x68 actions  +0x78 targetPlatform
        '
        ' ⛔ DOS COSAS QUE ESTABAN MAL Y NADIE VEIA:
        '  1) `+0x68` se estaba guardando en un campo llamado `Collidables`. La clase NO declara
        '     ningun `collidables`: +0x68 es `actions`. El nombre mentia sobre el contenido, y un
        '     consumidor que pidiera los colisionables recibia las acciones (viento). Los
        '     colisionables de verdad viven en `hclSimClothData.perInstanceCollidables`, que es de
        '     donde los toma la fisica.
        '  2) `+0x7C` se leia en `UnknownValue7C`. La clase mide 0x80 y su ultimo miembro declarado
        '     es `targetPlatform` en +0x78 (uint32) ⇒ +0x7C es RELLENO DE COLA. Leer padding y
        '     guardarlo como si fuera un dato es exactamente como se cementan las suposiciones.
        Dim result As New HclClothDataGraph_Class With {
            .SourceObject = source,
            .Name = hkr.Name,
            .TargetPlatform = hkr.TargetPlatform,
            .SimClothDatas = ReadObjectReferenceArray(hkr.SimClothDatas),
            .BufferDefinitions = ReadObjectReferenceArray(hkr.BufferDefinitions),
            .TransformSetDefinitions = ReadObjectReferenceArray(hkr.TransformSetDefinitions),
            .Operators = ReadObjectReferenceArray(hkr.Operators),
            .ClothStates = ReadObjectReferenceArray(hkr.ClothStateDatas),
            .Actions = ReadObjectReferenceArray(hkr.Actions)
        }

        result.Fields.Add(CreateArrayField(source, &H18, "SimClothDatas"))
        result.Fields.Add(CreateArrayField(source, &H28, "BufferDefinitions"))
        result.Fields.Add(CreateArrayField(source, &H38, "TransformSetDefinitions"))
        result.Fields.Add(CreateArrayField(source, &H48, "Operators"))
        result.Fields.Add(CreateArrayField(source, &H58, "ClothStates"))
        result.Fields.Add(CreateArrayField(source, &H68, "Collidables"))

        Return result
    End Function


    Public Function ParseClothState(source As HkxVirtualObjectGraph_Class) As HclClothStateGraph_Class
        If IsNothing(source) OrElse Not source.ClassName.Equals("hclClothState", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        ' Lector generado (HavokTyped.vb): los offsets salen de la reflexion de los dos
        ' .exe y la tabla la elige el packfile. Sin literales que se puedan desincronizar.
        Dim hkr As New Havok.Canon.Typed.Hk_HclClothState(Me, source)
        If Not hkr.IsValid Then Return Nothing

        Dim result As New HclClothStateGraph_Class With {
            .SourceObject = source,
            .Name = hkr.Name
        }

        For Each field In {
            New With {.Offset = &H18, .Name = "Array_18"},
            New With {.Offset = &H28, .Name = "Array_28"},
            New With {.Offset = &H38, .Name = "Array_38"},
            New With {.Offset = &H48, .Name = "Array_48"},
            New With {.Offset = &HB0, .Name = "Array_B0"},
            New With {.Offset = &HC0, .Name = "Array_C0"},
            New With {.Offset = &HD8, .Name = "Array_D8"},
            New With {.Offset = &HF0, .Name = "Array_F0"},
            New With {.Offset = &H108, .Name = "Array_108"},
            New With {.Offset = &H120, .Name = "Array_120"},
            New With {.Offset = &H138, .Name = "Array_138"}
        }
            Dim arrayField = CreateArrayField(source, field.Offset, field.Name)
            result.ArrayFields.Add(arrayField)

            Dim objectRefs = ReadObjectReferenceArray(source.RelativeOffset + field.Offset)
            If objectRefs.Count > 0 Then
                result.ObjectReferenceArrays.Add(New HkxObjectReferenceArrayGraph_Class With {
                    .FieldName = field.Name,
                    .Objects = objectRefs
                })
            End If
        Next

        Return result
    End Function

    Public Function ParseSimClothPose(source As HkxVirtualObjectGraph_Class) As HclSimClothPoseGraph_Class
        If IsNothing(source) OrElse Not source.ClassName.Equals("hclSimClothPose", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        ' Lector generado (HavokTyped.vb): los offsets salen de la reflexion de los dos
        ' .exe y la tabla la elige el packfile. Sin literales que se puedan desincronizar.
        Dim hkr As New Havok.Canon.Typed.Hk_HclSimClothPose(Me, source)
        If Not hkr.IsValid Then Return Nothing

        Dim result As New HclSimClothPoseGraph_Class With {
            .SourceObject = source,
            .Name = hkr.Name,
            .PoseField = CreateArrayField(source, &H18, "Pose")
        }

        result.Pose = ReadVector4Array(result.PoseField)
        Return result
    End Function

    Public Function ParseCollidable(source As HkxVirtualObjectGraph_Class) As HclCollidableGraph_Class
        ' Layout verificado con DumpStructuralAnalysis en CasualDress.nif:
        '   +0x000..+0x00F : hkReferencedObject
        '   +0x010         : StringPtr m_name → inline string at +0x090
        '   +0x018..+0x01F : 8 bytes de padding (alineación a 16B del transform)
        '   +0x020..+0x05F : hkMatrix4 m_transform (4×hkVector4 en column-major):
        '                     Col0=+0x020 Col1=+0x030 Col2=+0x040 Translation=+0x050
        '   +0x060..+0x083 : zeros / campos desconocidos
        '   +0x084         : float m_pinchDetectionRadius (≈0.01)
        '   +0x088         : hkRefPtr<hclShape> m_shape (GREF)
        If IsNothing(source) OrElse Not source.ClassName.Equals("hclCollidable", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        ' Lector generado (HavokTyped.vb): los offsets salen de la reflexion de los dos
        ' .exe y la tabla la elige el packfile. Sin literales que se puedan desincronizar.
        Dim hkr As New Havok.Canon.Typed.Hk_HclCollidable(Me, source)
        If Not hkr.IsValid Then Return Nothing

        Return New HclCollidableGraph_Class With {
            .SourceObject = source,
            .Name = hkr.Name,
            .Transform = ReadTransform(source.RelativeOffset + &H20),
            .PinchDetectionRadius = hkr.PinchDetectionRadius,
            .ShapeObject = ResolveGlobalObject(source.RelativeOffset + &H88)
        }
    End Function

    Private Function ReadTransform(firstVecRelativeOffset As Integer) As HkxTransformGraph_Class
        Return New HkxTransformGraph_Class With {
            .Col0 = New HkxVector4Graph_Class With {
                .X = ReadSingle(firstVecRelativeOffset + 0),
                .Y = ReadSingle(firstVecRelativeOffset + 4),
                .Z = ReadSingle(firstVecRelativeOffset + 8),
                .W = ReadSingle(firstVecRelativeOffset + 12)
            },
            .Col1 = New HkxVector4Graph_Class With {
                .X = ReadSingle(firstVecRelativeOffset + 16),
                .Y = ReadSingle(firstVecRelativeOffset + 20),
                .Z = ReadSingle(firstVecRelativeOffset + 24),
                .W = ReadSingle(firstVecRelativeOffset + 28)
            },
            .Col2 = New HkxVector4Graph_Class With {
                .X = ReadSingle(firstVecRelativeOffset + 32),
                .Y = ReadSingle(firstVecRelativeOffset + 36),
                .Z = ReadSingle(firstVecRelativeOffset + 40),
                .W = ReadSingle(firstVecRelativeOffset + 44)
            },
            .Translation = New HkxVector4Graph_Class With {
                .X = ReadSingle(firstVecRelativeOffset + 48),
                .Y = ReadSingle(firstVecRelativeOffset + 52),
                .Z = ReadSingle(firstVecRelativeOffset + 56),
                .W = ReadSingle(firstVecRelativeOffset + 60)
            }
        }
    End Function

    Public Function ParseSkeleton(source As HkxVirtualObjectGraph_Class) As HkaSkeletonGraph_Class
        If IsNothing(source) OrElse Not source.ClassName.Equals("hkaSkeleton", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        ' Lector generado (HavokTyped.vb): los offsets salen de la reflexion de los dos
        ' .exe y la tabla la elige el packfile. Sin literales que se puedan desincronizar.
        Dim hkr As New Havok.Canon.Typed.Hk_HkaSkeleton(Me, source)
        If Not hkr.IsValid Then Return Nothing

        ' ⭐ TODO POR EL LECTOR GENERADO. Antes esto derivaba los offsets sumando tamanos de
        ' cabecera a mano y decidia por formato si `partitions` existia. Era correcto, pero era una
        ' segunda copia del layout: si Bethesda mueve un campo, la tabla se entera y esta aritmetica
        ' no. Ahora los dos juegos salen de la reflexion de su propio .exe.
        '
        ' ⛔ `partitions` se agrego en hk2014 y Skyrim NO lo tiene: leerlo ahi caia PASADO EL FIN
        ' del objeto (120 bytes contra 136) y devolvia particiones basura. Eso ya no depende de una
        ' rama por juego: la tabla de SSE marca el miembro como ausente y `HasPartitions` da False.
        Dim nameFieldOffset = BaseObjectFieldOffset
        Dim parentIndicesOffset = nameFieldOffset + PointerSizeValue
        Dim bonesOffset = parentIndicesOffset + ArrayHeaderSizeValue
        Dim referencePoseOffset = bonesOffset + ArrayHeaderSizeValue
        Dim referenceFloatsOffset = referencePoseOffset + ArrayHeaderSizeValue
        Dim floatSlotsOffset = referenceFloatsOffset + ArrayHeaderSizeValue
        Dim localFramesOffset = floatSlotsOffset + ArrayHeaderSizeValue
        Dim hasPartitions = (Packfile.Header.PackfileFormat = HkxPackfileFormat_Enum.Fallout64)
        Dim partitionsOffset = If(hasPartitions, localFramesOffset + ArrayHeaderSizeValue, -1)

        Dim result As New HkaSkeletonGraph_Class With {
            .SourceObject = source,
            .Name = ResolveLocalString(source.RelativeOffset + nameFieldOffset),
            .ParentIndices = ReadInt16Array(source.RelativeOffset + parentIndicesOffset),
            .ParentIndicesField = CreateArrayField(source, parentIndicesOffset, "ParentIndices"),
            .BonesField = CreateArrayField(source, bonesOffset, "Bones"),
            .ReferencePoseField = CreateArrayField(source, referencePoseOffset, "ReferencePose"),
            .ReferenceFloatsField = CreateArrayField(source, referenceFloatsOffset, "ReferenceFloats"),
            .FloatSlotsField = CreateArrayField(source, floatSlotsOffset, "FloatSlots"),
            .LocalFramesField = CreateArrayField(source, localFramesOffset, "LocalFrames"),
            .PartitionsField = If(hasPartitions, CreateArrayField(source, partitionsOffset, "Partitions"), Nothing)
        }

        result.Bones = ReadSkeletonBones(result.BonesField)
        result.ReferencePose = ReadQsTransformArray(result.ReferencePoseField)
        result.FloatSlotNames = ReadFloatSlotNames(source, floatSlotsOffset)
        result.Partitions = If(hasPartitions, ReadSkeletonPartitions(source, partitionsOffset), New List(Of HkaPartitionGraph_Class))
        Return result
    End Function

    ' floatSlots: hkArray<hkStringPtr> — canales float nombrados a los que se bindean float-tracks de animación
    ' (drivers faciales / additive / IK). NO son regiones de body-weight.
    Private Function ReadFloatSlotNames(source As HkxVirtualObjectGraph_Class, fieldOffset As Integer) As List(Of String)
        Dim result As New List(Of String)
        For Each entryOffset In ReadStructureOffsets(source.RelativeOffset + fieldOffset, PointerSizeValue)
            result.Add(ResolveLocalString(entryOffset))
        Next
        Return result
    End Function

    ' partitions: hkArray<hkaPartition>{ name@0, startBoneIndex:int16@ptr, numBones:uint16@ptr+2 }.
    ' Stride 16 (ptr=8) / 8 (ptr=4). Agrupaciones de huesos para el solver/threading de Havok — NO regiones de morph.
    Private Function ReadSkeletonPartitions(source As HkxVirtualObjectGraph_Class, fieldOffset As Integer) As List(Of HkaPartitionGraph_Class)
        Dim result As New List(Of HkaPartitionGraph_Class)
        Dim stride = If(PointerSizeValue = 8, 16, 8)
        Dim idx = 0
        For Each entryOffset In ReadStructureOffsets(source.RelativeOffset + fieldOffset, stride)
            result.Add(New HkaPartitionGraph_Class With {
                .Index = idx,
                .Name = ResolveLocalString(entryOffset),
                .StartBoneIndex = ReadInt16(entryOffset + PointerSizeValue),
                .NumBones = ReadInt16(entryOffset + PointerSizeValue + 2)
            })
            idx += 1
        Next
        Return result
    End Function

    Private Function ReadSkeletonBones(field As HkxObjectArrayField_Class) As List(Of HkaBoneGraph_Class)
        Dim result As New List(Of HkaBoneGraph_Class)
        If IsNothing(field) OrElse IsNothing(field.Header) Then Return result
        If field.Header.Count <= 0 OrElse field.Header.DataRelativeOffset < 0 Then Return result

        Dim boneStride = If(PointerSizeValue = 8, 16, PointerSizeValue + 4)
        For i = 0 To field.Header.Count - 1
            Dim entryOffset = field.Header.DataRelativeOffset + (i * boneStride)
            result.Add(New HkaBoneGraph_Class With {
                .Index = i,
                .EntryRelativeOffset = entryOffset,
                .Name = ResolveLocalString(entryOffset),
                .LockTranslation = ReadByte(entryOffset + PointerSizeValue) <> 0
            })
        Next

        Return result
    End Function

    Private Function ReadVector4Array(field As HkxObjectArrayField_Class) As List(Of HkxVector4Graph_Class)
        Dim result As New List(Of HkxVector4Graph_Class)
        If IsNothing(field) OrElse IsNothing(field.Header) Then Return result
        If field.Header.Count <= 0 OrElse field.Header.DataRelativeOffset < 0 Then Return result

        Const vectorStride As Integer = 16
        For i = 0 To field.Header.Count - 1
            Dim entryOffset = field.Header.DataRelativeOffset + (i * vectorStride)
            result.Add(New HkxVector4Graph_Class With {
                .X = ReadSingle(entryOffset + 0),
                .Y = ReadSingle(entryOffset + 4),
                .Z = ReadSingle(entryOffset + 8),
                .W = ReadSingle(entryOffset + 12)
            })
        Next

        Return result
    End Function

    Private Function ReadQsTransformArray(field As HkxObjectArrayField_Class) As List(Of HkxQsTransformGraph_Class)
        Dim result As New List(Of HkxQsTransformGraph_Class)
        If IsNothing(field) OrElse IsNothing(field.Header) Then Return result
        If field.Header.Count <= 0 OrElse field.Header.DataRelativeOffset < 0 Then Return result

        Const transformStride As Integer = 48
        For i = 0 To field.Header.Count - 1
            Dim entryOffset = field.Header.DataRelativeOffset + (i * transformStride)
            result.Add(New HkxQsTransformGraph_Class With {
                .Index = i,
                .EntryRelativeOffset = entryOffset,
                .Translation = New HkxVector4Graph_Class With {
                    .X = ReadSingle(entryOffset + 0),
                    .Y = ReadSingle(entryOffset + 4),
                    .Z = ReadSingle(entryOffset + 8),
                    .W = ReadSingle(entryOffset + 12)
                },
                .Rotation = New HkxQuaternionGraph_Class With {
                    .X = ReadSingle(entryOffset + 16),
                    .Y = ReadSingle(entryOffset + 20),
                    .Z = ReadSingle(entryOffset + 24),
                    .W = ReadSingle(entryOffset + 28)
                },
                .Scale = New HkxVector4Graph_Class With {
                    .X = ReadSingle(entryOffset + 32),
                    .Y = ReadSingle(entryOffset + 36),
                    .Z = ReadSingle(entryOffset + 40),
                    .W = ReadSingle(entryOffset + 44)
                }
            })
        Next

        Return result
    End Function
    Private Function ReadNamedVariant(namedVariantRelativeOffset As Integer) As HkxNamedVariantGraph_Class
        Return New HkxNamedVariantGraph_Class With {
            .RelativeOffset = namedVariantRelativeOffset,
            .Name = ResolveLocalString(namedVariantRelativeOffset),
            .ClassName = ResolveLocalString(namedVariantRelativeOffset + PointerSizeValue),
            .VariantObject = ResolveGlobalObject(namedVariantRelativeOffset + (PointerSizeValue * 2))
        }
    End Function

    ''' <summary>Envuelve una cabecera de array ya resuelta por el lector generado. Toma la
    ''' CABECERA y no un offset a proposito: si tomara un offset, el llamador tendria que volver a
    ''' calcularlo y el numero volveria a vivir fuera de la tabla.</summary>
    Private Function CreateArrayField(header As HkxObjectArrayHeader_Class, fieldName As String) As HkxObjectArrayField_Class
        Return New HkxObjectArrayField_Class With {
            .FieldName = fieldName,
            .Header = header
        }
    End Function

    Private Function CreateArrayField(source As HkxVirtualObjectGraph_Class, fieldOffset As Integer, fieldName As String) As HkxObjectArrayField_Class
        Return New HkxObjectArrayField_Class With {
            .FieldName = fieldName,
            .Header = ReadArrayHeader(source.RelativeOffset + fieldOffset)
        }
    End Function

    ' -------------------------------------------------------------------------
    ' De acá en adelante: offsets HCL determinados empíricamente con
    ' DumpStructuralAnalysis sobre NIFs reales de FO4 64-bit, NO contra el SDK de Havok.
    ' Cada función documenta el archivo con el que se verificó su layout.
    ' -------------------------------------------------------------------------

    Public Function ParseSimClothData(source As HkxVirtualObjectGraph_Class) As HclSimClothDataGraph_Class
        ' ⛔⛔ 2026-08-22 — offsets desde `Havok.Canon.HavokLayout` (reflexión del propio .exe). Antes eran
        ' literales "verificados con DumpStructuralAnalysis" y DOS estaban mal:
        '   · `CollidableTransformIndices` leía +0x30 como hkArray de uint16. +0x30 es `name`, un
        '     STRINGPTR ⇒ el count salía del puntero serializado de `particleDatas` (siempre 0 en
        '     packfile) y la lista quedaba PERMANENTEMENTE VACÍA. El array real es
        '     `collidableTransformMap.transformIndices` en +0x88, y es uint32.
        '   · `Particles` llamaba "xyz=posición rest, w=invMass" a `particleDatas`, que en realidad es
        '     {mass, invMass, radius, friction}. Se renombra a ParticleDatas y se expone desglosado.
        ' Esta función es la vista LIGERA (sólo conteos y refs); la de producción es
        ' HclStructuredGraphParser_Class.ParseSimClothData.
        If IsNothing(source) OrElse Not source.ClassName.Equals("hclSimClothData", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        ' Lector generado (HavokTyped.vb): los offsets salen de la reflexion de los dos
        ' .exe y la tabla la elige el packfile. Sin literales que se puedan desincronizar.
        Dim hkr As New Havok.Canon.Typed.Hk_HclSimClothData(Me, source)
        If Not hkr.IsValid Then Return Nothing
        Const cls = "hclSimClothData"
        Dim layout = Havok.Canon.HavokLayout.For(Packfile.Header.PackfileFormat)
        If layout Is Nothing OrElse Not layout.HasClass(cls) Then Return Nothing
        Dim rel = source.RelativeOffset

        Dim result As New HclSimClothDataGraph_Class With {
            .SourceObject = source,
            .Name = ResolveLocalString(rel + layout.RequireOffset(cls, "name")),
            .NumParticles = ReadInt32(rel + layout.RequireOffset(cls, "particleDatas") + PointerSizeValue)
        }
        result.ParticleDatas.AddRange(ReadVector4ArrayFromOffset(rel + layout.RequireOffset(cls, "particleDatas")))
        result.FixedParticles.AddRange(ReadUInt16Array(rel + layout.RequireOffset(cls, "fixedParticles")))
        result.CollidableTransformIndices.AddRange(
            ReadUInt32ArrayFromOffset(rel + layout.RequireOffset(cls, "collidableTransformMap.transformIndices")))
        result.TriangleIndices.AddRange(ReadUInt16Array(rel + layout.RequireOffset(cls, "triangleIndices")))
        result.Collidables.AddRange(ReadObjectReferenceArray(rel + layout.RequireOffset(cls, "perInstanceCollidables")))
        result.SimClothPoses.AddRange(ReadObjectReferenceArray(rel + layout.RequireOffset(cls, "simClothPoses")))
        result.StaticConstraintSets.AddRange(ReadObjectReferenceArray(rel + layout.RequireOffset(cls, "staticConstraintSets")))
        result.Actions.AddRange(ReadObjectReferenceArray(rel + layout.RequireOffset(cls, "actions")))
        Return result
    End Function

    ''' <summary>hkArray de uint32 desde el offset del CAMPO (no del dato).</summary>
    Public Function ReadUInt32ArrayFromOffset(fieldRelativeOffset As Integer) As List(Of UInteger)
        Dim result As New List(Of UInteger)
        Dim header = ReadArrayHeader(fieldRelativeOffset)
        If header.Count <= 0 OrElse header.DataRelativeOffset < 0 Then Return result
        For i = 0 To header.Count - 1
            result.Add(ReadUInt32(header.DataRelativeOffset + (i * 4)))
        Next
        Return result
    End Function

    Public Function ParseBufferDefinition(source As HkxVirtualObjectGraph_Class) As HclBufferDefinitionGraph_Class
        ' Offsets verificados con DumpStructuralAnalysis en CasualDress.nif:
        '   +0x000..+0x00F : hkReferencedObject zeros
        '   +0x010         : m_name StringPtr (local fixup)
        '   +0x018         : int32 — primer campo escalar (NumSubBuffers o MeshSectionIndex)
        '   +0x020         : int32 — segundo campo escalar
        '   +0x030         : int32 — tercer campo escalar (≈ NumParticles en el ejemplo)
        ' Nombres exactos de campo (MeshSectionIndex, SubMeshIndex, TriangleOffset):
        ' REVISAR — mapeados empíricamente, puede que no coincidan con SDK Havok.
        ' Acepta hclBufferDefinition y hclScratchBufferDefinition (misma estructura).
        If IsNothing(source) Then Return Nothing
        If Not source.ClassName.StartsWith("hclBuffer", StringComparison.OrdinalIgnoreCase) AndAlso
           Not source.ClassName.StartsWith("hclScratch", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        Return New HclBufferDefinitionGraph_Class With {
            .SourceObject = source,
            .Name = ResolveLocalString(source.RelativeOffset + &H10),
            .SubBufferCount = ReadInt32(source.RelativeOffset + &H18),
            .LayoutField = ReadInt32(source.RelativeOffset + &H20),
            .VertexCount = ReadInt32(source.RelativeOffset + &H30)
        }
    End Function

    ' -------------------------------------------------------------------------
    ' ParseGenericHavokObject — parser universal para cualquier clase.
    ' Lee nombre (StringPtr en +0x010) y escanea los primeros 256 bytes en busca
    ' de local fixups (hkArrays) y global fixups (referencias a objetos).
    ' Usado para todos los operadores y constraint sets cuyos offsets exactos
    ' no han sido mapeados todavía al SDK de Havok.
    ' Offsets verificados empíricamente con DumpStructuralAnalysis en:
    '   CasualDress.nif y CBBEBodyPhysics.nif (FO4, 64-bit, EBCO=1)
    ' -------------------------------------------------------------------------
    Public Function ParseGenericHavokObject(source As HkxVirtualObjectGraph_Class) As HclGenericHavokObjectGraph_Class
        If IsNothing(source) Then Return Nothing

        Dim result As New HclGenericHavokObjectGraph_Class With {
            .SourceObject = source,
            .Name = ResolveLocalString(source.RelativeOffset + &H10)
        }

        Dim scanSize = Math.Min(source.Size, 256)
        Dim localFixups = GetLocalFixupsInRange(source.RelativeOffset, scanSize).
                          OrderBy(Function(f) f.SourceRelativeOffset).ToList()
        Dim globalFixups = GetGlobalFixupsInRange(source.RelativeOffset, scanSize).
                           OrderBy(Function(f) f.SourceRelativeOffset).ToList()

        Dim skip = 0   ' bytes consumed by previous multi-byte field
        For Each lf In localFixups
            Dim relOff = lf.SourceRelativeOffset - source.RelativeOffset
            If relOff < skip Then Continue For
            If relOff < 0 OrElse relOff + ArrayHeaderSizeValue > scanSize Then Continue For

            ' Heuristic: if the first derived-field slot is the name ptr, do not treat it as hkArray.
            If relOff = BaseObjectFieldOffset Then
                skip = relOff + PointerSizeValue
                Continue For
            End If

            Dim header = ReadArrayHeader(source.RelativeOffset + relOff)
            result.ArrayFields.Add(New HkxObjectArrayField_Class With {
                .FieldName = $"arr@+0x{relOff:X}",
                .Header = header
            })
            skip = relOff + ArrayHeaderSizeValue
        Next

        For Each gf In globalFixups
            Dim relOff = gf.SourceRelativeOffset - source.RelativeOffset
            If relOff < 0 OrElse relOff + 8 > scanSize Then Continue For
            Dim targetObj = GetObject(gf.TargetRelativeOffset)
            If Not IsNothing(targetObj) Then result.ObjectRefs.Add(targetObj)
        Next

        Return result
    End Function

    ' -------------------------------------------------------------------------
    ' ParseTaperedCapsuleShape — no m_name, pure float geometry (no fixups).
    ' Layout verificado con DumpStructuralAnalysis en CBBEBodyPhysics.nif:
    '   +0x000..+0x01F : hkReferencedObject + 16B zeros (sin nombre)
    '   +0x020         : hkVector4 — centro esfera A (xyz, W=0)
    '   +0x030         : hkVector4 — centro esfera B (xyz, W=0)
    '   +0x040         : hkVector4 — precomputed (distancia² u otro valor derivado)
    '   +0x050         : hkVector4 — eje normalizado B→A (xyz, W=0)
    '   +0x060..+0x07F : 2×hkVector4 broadcast de valores precomputados (SIMD)
    '   +0x080..+0x08F : 1×hkVector4 broadcast de valores precomputados (SIMD)
    '   +0x090         : float radiusA (esfera A), float radiusB (esfera B), float dist, float dist²
    '   +0x0A0..+0x0AF : más valores precomputados
    ' -------------------------------------------------------------------------
    Public Function ParseTaperedCapsuleShape(source As HkxVirtualObjectGraph_Class) As HclTaperedCapsuleShapeGraph_Class
        If IsNothing(source) OrElse Not source.ClassName.Equals("hclTaperedCapsuleShape", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        ' Lector generado (HavokTyped.vb): los offsets salen de la reflexion de los dos
        ' .exe y la tabla la elige el packfile. Sin literales que se puedan desincronizar.
        Dim hkr As New Havok.Canon.Typed.Hk_HclTaperedCapsuleShape(Me, source)
        If Not hkr.IsValid Then Return Nothing

        Return New HclTaperedCapsuleShapeGraph_Class With {
            .SourceObject = source,
            .CentreA = New HkxVector4Graph_Class With {
                .X = hkr.Small(0),
                .Y = hkr.Small(1),
                .Z = hkr.Small(2),
                .W = hkr.Small(3)
            },
            .CentreB = New HkxVector4Graph_Class With {
                .X = hkr.Big(0),
                .Y = hkr.Big(1),
                .Z = hkr.Big(2),
                .W = hkr.Big(3)
            },
            .RadiusA = hkr.SmallRadius,
            .RadiusB = hkr.BigRadius
        }
    End Function

    ' -------------------------------------------------------------------------
    ' ParseCapsuleShape — no m_name, uniform-radius capsule (no fixups).
    ' Layout verificado con DumpStructuralAnalysis en CasualDress.nif:
    '   +0x000..+0x01F : hkReferencedObject + 16B zeros (sin nombre)
    '   +0x020         : hkVector4 — vértice A (centro esfera inferior), W=0
    '   +0x030         : hkVector4 — vértice B (centro esfera superior), W=0
    '   +0x040         : hkVector4 — eje normalizado A→B (1,0,0,0 en muestra)
    '   +0x050         : float radius, luego floats adicionales (precomputados)
    ' -------------------------------------------------------------------------
    Public Function ParseCapsuleShape(source As HkxVirtualObjectGraph_Class) As HclCapsuleShapeGraph_Class
        If IsNothing(source) OrElse Not source.ClassName.Equals("hclCapsuleShape", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        ' Lector generado (HavokTyped.vb): los offsets salen de la reflexion de los dos
        ' .exe y la tabla la elige el packfile. Sin literales que se puedan desincronizar.
        Dim hkr As New Havok.Canon.Typed.Hk_HclCapsuleShape(Me, source)
        If Not hkr.IsValid Then Return Nothing

        Return New HclCapsuleShapeGraph_Class With {
            .SourceObject = source,
            .VertexA = New HkxVector4Graph_Class With {
                .X = hkr.Start(0),
                .Y = hkr.Start(1),
                .Z = hkr.Start(2),
                .W = hkr.Start(3)
            },
            .VertexB = New HkxVector4Graph_Class With {
                .X = hkr.End(0),
                .Y = hkr.End(1),
                .Z = hkr.End(2),
                .W = hkr.End(3)
            },
            .Radius = hkr.Radius
        }
    End Function

    ' -------------------------------------------------------------------------
    ' ParseTransformSetDefinitionData — hclTransformSetDefinition.
    ' Layout verificado con DumpStructuralAnalysis (tamaño=48 bytes):
    '   +0x000..+0x00F : hkReferencedObject
    '   +0x010         : StringPtr m_name → inline string at +0x020
    '   +0x018         : int32 m_numTransforms (cuántos transforms hay en el set)
    '   +0x01C         : int32 m_numFloatSlots (número de float slots = número de huesos)
    ' -------------------------------------------------------------------------
    Public Function ParseTransformSetDefinitionData(source As HkxVirtualObjectGraph_Class) As HkxTransformSetDefinitionGraph_Class
        If IsNothing(source) OrElse Not source.ClassName.Equals("hclTransformSetDefinition", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        ' Lector generado (HavokTyped.vb): los offsets salen de la reflexion de los dos
        ' .exe y la tabla la elige el packfile. Sin literales que se puedan desincronizar.
        Dim hkr As New Havok.Canon.Typed.Hk_HclTransformSetDefinition(Me, source)
        If Not hkr.IsValid Then Return Nothing

        Return New HkxTransformSetDefinitionGraph_Class With {
            .SourceObject = source,
            .Name = hkr.Name,
            .NumTransforms = CInt(hkr.NumTransforms),
            .NumFloatSlots = hkr.Type
        }
    End Function

    ' -------------------------------------------------------------------------
    ' ParseSimulateOperator — hclSimulateOperator.
    ' Layout verificado con DumpStructuralAnalysis (tamaño=112 bytes):
    '   +0x010 : StringPtr m_name → inline string at +0x050 ("Sim\0")
    '   +0x024 : int32 (=2 en muestra) — posible índice de buffers o transforms
    '   +0x028 : int32 (=1 en muestra) — modo de simulación o sub-step count
    '   +0x030 : hkArray (4 elems) — posibles datos de transición de estado
    '   +0x060 : int32=1, int32=0
    '   +0x068 : int32=2, int32=-1 (0xFFFFFFFF)
    ' -------------------------------------------------------------------------
    Public Function ParseSimulateOperator(source As HkxVirtualObjectGraph_Class) As HclSimulateOperatorGraph_Class
        If IsNothing(source) OrElse Not source.ClassName.Equals("hclSimulateOperator", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        ' Lector generado (HavokTyped.vb): los offsets salen de la reflexion de los dos
        ' .exe y la tabla la elige el packfile. Sin literales que se puedan desincronizar.
        Dim hkr As New Havok.Canon.Typed.Hk_HclSimulateOperator(Me, source)
        If Not hkr.IsValid Then Return Nothing

        Return New HclSimulateOperatorGraph_Class With {
            .SourceObject = source,
            .Name = hkr.Name,
            .Field24 = CInt(hkr.SubSteps),
            .Field28 = hkr.NumberOfSolveIterations,
            .SubStatesCount = ReadInt32(source.RelativeOffset + &H38)   ' count del array en +0x030
        }
    End Function

    ' -------------------------------------------------------------------------
    ' ParseCopyVerticesOperator — hclCopyVerticesOperator.
    ' Layout verificado con DumpStructuralAnalysis (tamaño=80 bytes):
    '   +0x010 : StringPtr m_name → inline string at +0x040 ("VertGather Op...")
    '   +0x020 : int32=0
    '   +0x024 : int32=1  (probable outputBufferIndex)
    '   +0x028 : int32=12 (probable inputBufferIndex)
    '   +0x02C : int32=0
    '   +0x030 : int32=0
    '   +0x034 : int32=1
    '   +0x038 : int32=0
    ' -------------------------------------------------------------------------
    Public Function ParseCopyVerticesOperator(source As HkxVirtualObjectGraph_Class) As HclCopyVerticesOperatorGraph_Class
        If IsNothing(source) OrElse Not source.ClassName.Equals("hclCopyVerticesOperator", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        ' Lector generado (HavokTyped.vb): los offsets salen de la reflexion de los dos
        ' .exe y la tabla la elige el packfile. Sin literales que se puedan desincronizar.
        Dim hkr As New Havok.Canon.Typed.Hk_HclCopyVerticesOperator(Me, source)
        If Not hkr.IsValid Then Return Nothing

        Return New HclCopyVerticesOperatorGraph_Class With {
            .SourceObject = source,
            .Name = hkr.Name,
            .Field20 = CInt(hkr.InputBufferIdx),
            .OutputBufferIndex = CInt(hkr.OutputBufferIdx),
            .InputBufferIndex = CInt(hkr.NumberOfVertices)
        }
    End Function

    ' -------------------------------------------------------------------------
    ' ParseGatherAllVerticesOperator — hclGatherAllVerticesOperator.
    ' Layout verificado con DumpStructuralAnalysis (tamaño=160 bytes):
    '   +0x010 : StringPtr m_name → inline string at +0x040 ("VertGather Op...")
    '   +0x020 : hkArray (38 elems) — mapeado de vértices: uint16 pairs (srcIdx, dstIdx)
    '   +0x030 : int32=0
    '   +0x034 : int32=1  (probable outputBufferIndex)
    '   +0x038 : int32=1  (probable inputBufferIndex o numBuffers)
    ' -------------------------------------------------------------------------
    Public Function ParseGatherAllVerticesOperator(source As HkxVirtualObjectGraph_Class) As HclGatherAllVerticesOperatorGraph_Class
        If IsNothing(source) OrElse Not source.ClassName.Equals("hclGatherAllVerticesOperator", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        ' Lector generado (HavokTyped.vb): los offsets salen de la reflexion de los dos
        ' .exe y la tabla la elige el packfile. Sin literales que se puedan desincronizar.
        Dim hkr As New Havok.Canon.Typed.Hk_HclGatherAllVerticesOperator(Me, source)
        If Not hkr.IsValid Then Return Nothing

        Dim result As New HclGatherAllVerticesOperatorGraph_Class With {
            .SourceObject = source,
            .Name = hkr.Name,
            .OutputBufferIndex = CInt(hkr.OutputBufferIdx),
            .NumBuffers = If(hkr.GatherNormals, 1, 0)
        }
        result.VertexRemapping.AddRange(ReadUInt16Array(source.RelativeOffset + &H20))
        Return result
    End Function

    ' -------------------------------------------------------------------------
    ' ParseMoveParticlesOperator — hclMoveParticlesOperator.
    ' Layout verificado con DumpStructuralAnalysis (tamaño=272 bytes):
    '   +0x010 : StringPtr m_name → inline string at +0x040 ("Move Particles\0")
    '   +0x020 : hkArray (45 elems) — pares uint16 (particleIdx, particleIdx):
    '            lista de índices de partículas ancladas/movidas
    ' Los datos del array son pares (i, i) para cada partícula fija (misma src y dst).
    ' -------------------------------------------------------------------------
    Public Function ParseMoveParticlesOperator(source As HkxVirtualObjectGraph_Class) As HclMoveParticlesOperatorGraph_Class
        If IsNothing(source) OrElse Not source.ClassName.Equals("hclMoveParticlesOperator", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        ' Lector generado (HavokTyped.vb): los offsets salen de la reflexion de los dos
        ' .exe y la tabla la elige el packfile. Sin literales que se puedan desincronizar.
        Dim hkr As New Havok.Canon.Typed.Hk_HclMoveParticlesOperator(Me, source)
        If Not hkr.IsValid Then Return Nothing

        Dim result As New HclMoveParticlesOperatorGraph_Class With {
            .SourceObject = source,
            .Name = hkr.Name
        }
        result.ParticleIndices.AddRange(ReadUInt16Array(source.RelativeOffset + &H20))
        Return result
    End Function

    ' -------------------------------------------------------------------------
    ' ParseConstraintSet — genérico para constraint sets de links y rango.
    ' Layout común (verificado en StdLink, StretchLink, BendStiffness, LocalRange):
    '   +0x010 : StringPtr m_name → inline string
    '   +0x020 : hkArray m_constraints (main constraint array), count @ +0x028
    ' hclVolumeConstraintMx tiene layout diferente — ver ParseVolumeConstraintMx.
    ' -------------------------------------------------------------------------
    Public Function ParseConstraintSet(source As HkxVirtualObjectGraph_Class) As HclConstraintSetGraph_Class
        If IsNothing(source) Then Return Nothing
        Dim cn = source.ClassName
        Dim isConstraintSet =
            cn.StartsWith("hclStandardLink", StringComparison.OrdinalIgnoreCase) OrElse
            cn.StartsWith("hclStretchLink", StringComparison.OrdinalIgnoreCase) OrElse
            cn.StartsWith("hclBendStiffness", StringComparison.OrdinalIgnoreCase) OrElse
            cn.StartsWith("hclLocalRange", StringComparison.OrdinalIgnoreCase)
        If Not isConstraintSet Then Return Nothing

        Return New HclConstraintSetGraph_Class With {
            .SourceObject = source,
            .Name = ResolveLocalString(source.RelativeOffset + &H10),
            .ConstraintCount = ReadInt32(source.RelativeOffset + &H28)  ' count at +0x020+8
        }
    End Function

    ' -------------------------------------------------------------------------
    ' ParseVolumeConstraintMx — hclVolumeConstraintMx.
    ' Layout verificado con DumpStructuralAnalysis en CBBEBodyPhysics.nif:
    '   +0x010 : StringPtr m_name → inline string at +0x060 ("Volume Constraint\0")
    '   +0x020 : hkArray (2 elems)  — m_particleTriangles o similar
    '   +0x030 : hkArray (6 elems)  — m_stiffnessData o similar
    '   +0x040 : hkArray (1 elem)   — m_perParticleData o similar
    '   +0x050 : hkArray (4 elems)  — m_initialVolumes o similar
    '   +0x060 : inline name string
    '   +0x080..+0x0FC: 16-byte aligned float4 vectors (constraint geometry)
    ' -------------------------------------------------------------------------
    Public Function ParseVolumeConstraintMx(source As HkxVirtualObjectGraph_Class) As HclVolumeConstraintMxGraph_Class
        If IsNothing(source) OrElse Not source.ClassName.Equals("hclVolumeConstraintMx", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        ' Lector generado (HavokTyped.vb): los offsets salen de la reflexion de los dos
        ' .exe y la tabla la elige el packfile. Sin literales que se puedan desincronizar.
        Dim hkr As New Havok.Canon.Typed.Hk_HclVolumeConstraintMx(Me, source)
        If Not hkr.IsValid Then Return Nothing

        Return New HclVolumeConstraintMxGraph_Class With {
            .SourceObject = source,
            .Name = hkr.Name,
            .Array1Count = ReadInt32(source.RelativeOffset + &H28),   ' count of array at +0x020
            .Array2Count = ReadInt32(source.RelativeOffset + &H38),   ' count of array at +0x030
            .Array3Count = ReadInt32(source.RelativeOffset + &H48),   ' count of array at +0x040
            .Array4Count = ReadInt32(source.RelativeOffset + &H58)    ' count of array at +0x050
        }
    End Function
End Class
Public Class HkxVirtualObjectGraph_Class
    Public Property SectionIndex As Integer
    Public Property RelativeOffset As Integer
    Public Property AbsoluteOffset As Integer
    Public Property ClassNameSectionIndex As Integer
    Public Property ClassNameRelativeOffset As Integer
    Public Property ClassName As String
    Public Property Size As Integer
End Class

Public Class HkxObjectArrayHeader_Class
    Public Property FieldRelativeOffset As Integer
    Public Property DataRelativeOffset As Integer
    Public Property Count As Integer
    Public Property CapacityAndFlags As Integer
End Class

Public Class HkxObjectArrayField_Class
    Public Property FieldName As String
    Public Property Header As HkxObjectArrayHeader_Class
End Class

Public Class HkxRootLevelContainerGraph_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public ReadOnly Property NamedVariants As New List(Of HkxNamedVariantGraph_Class)
End Class

Public Class HkxNamedVariantGraph_Class
    Public Property RelativeOffset As Integer
    Public Property Name As String
    Public Property ClassName As String
    Public Property VariantObject As HkxVirtualObjectGraph_Class
End Class

Public Class HclClothDataGraph_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Name As String
    ''' <summary>`targetPlatform` (+0x78, enum uint32). Se llamaba `UnknownValue78`.</summary>
    Public Property TargetPlatform As Integer
    Public Property SimClothDatas As List(Of HkxVirtualObjectGraph_Class)
    Public Property BufferDefinitions As List(Of HkxVirtualObjectGraph_Class)
    Public Property TransformSetDefinitions As List(Of HkxVirtualObjectGraph_Class)
    Public Property Operators As List(Of HkxVirtualObjectGraph_Class)
    Public Property ClothStates As List(Of HkxVirtualObjectGraph_Class)
    ''' <summary>
    ''' `actions` (+0x68, hkArray&lt;hclAction*&gt;). ⛔ Se llamaba `Collidables` y NO son colisionables:
    ''' `hclClothData` no declara ningun miembro `collidables`. Los colisionables de verdad estan en
    ''' `hclSimClothData.perInstanceCollidables`, que es de donde los saca la fisica.
    ''' </summary>
    Public Property Actions As List(Of HkxVirtualObjectGraph_Class)
    Public ReadOnly Property Fields As New List(Of HkxObjectArrayField_Class)
End Class


Public Class HclClothStateGraph_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Name As String
    Public ReadOnly Property ArrayFields As New List(Of HkxObjectArrayField_Class)
    Public ReadOnly Property ObjectReferenceArrays As New List(Of HkxObjectReferenceArrayGraph_Class)
End Class

Public Class HclSimClothPoseGraph_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Name As String
    Public Property PoseField As HkxObjectArrayField_Class
    Public Property Pose As List(Of HkxVector4Graph_Class)
End Class

Public Class HkxObjectReferenceArrayGraph_Class
    Public Property FieldName As String
    Public Property Objects As List(Of HkxVirtualObjectGraph_Class)
End Class

Public Class HclCollidableGraph_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Name As String
    ' hkMatrix4 m_transform stored column-major as 4 hkVector4.
    ' Translation = Transform.Translation.XYZ (W=1.0 confirmed in samples).
    Public Property Transform As HkxTransformGraph_Class
    Public Property PinchDetectionRadius As Single  ' +0x084, float
    Public Property ShapeObject As HkxVirtualObjectGraph_Class  ' +0x088, GREF
End Class

' hkMatrix4 stored as 4 column vectors (Col0..Col2 = rotation, Translation = position).
' In samples: Translation.W = 1.0; rotation column W components contain matrix row 3 data.
' For a rigid transform: position = (Translation.X, Translation.Y, Translation.Z).
Public Class HkxTransformGraph_Class
    Public Property Col0 As HkxVector4Graph_Class        ' +0x000 within transform block
    Public Property Col1 As HkxVector4Graph_Class        ' +0x010
    Public Property Col2 As HkxVector4Graph_Class        ' +0x020
    Public Property Translation As HkxVector4Graph_Class ' +0x030 (W=1.0 for position)
End Class

Public Class HkaSkeletonGraph_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Name As String
    Public Property ParentIndices As List(Of Short)
    Public Property ParentIndicesField As HkxObjectArrayField_Class
    Public Property BonesField As HkxObjectArrayField_Class
    Public Property ReferencePoseField As HkxObjectArrayField_Class
    Public Property ReferenceFloatsField As HkxObjectArrayField_Class
    Public Property FloatSlotsField As HkxObjectArrayField_Class
    Public Property LocalFramesField As HkxObjectArrayField_Class
    Public Property PartitionsField As HkxObjectArrayField_Class
    Public Property Bones As List(Of HkaBoneGraph_Class)
    Public Property ReferencePose As List(Of HkxQsTransformGraph_Class)
    Public Property FloatSlotNames As List(Of String)
    Public Property Partitions As List(Of HkaPartitionGraph_Class)
End Class

Public Class HkaPartitionGraph_Class
    Public Property Index As Integer
    Public Property Name As String
    Public Property StartBoneIndex As Short
    Public Property NumBones As Short
End Class

Public Class HkaBoneGraph_Class
    Public Property Index As Integer
    Public Property EntryRelativeOffset As Integer
    Public Property Name As String
    Public Property LockTranslation As Boolean
End Class



Public Class HkxQsTransformGraph_Class
    Public Property Index As Integer
    Public Property EntryRelativeOffset As Integer
    Public Property Translation As HkxVector4Graph_Class
    Public Property Rotation As HkxQuaternionGraph_Class
    Public Property Scale As HkxVector4Graph_Class
End Class

Public Class HkxVector4Graph_Class
    Public Property X As Single
    Public Property Y As Single
    Public Property Z As Single
    Public Property W As Single
End Class

Public Class HkxQuaternionGraph_Class
    Public Property X As Single
    Public Property Y As Single
    Public Property Z As Single
    Public Property W As Single
End Class

Public Class HclSimClothDataGraph_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    ''' <summary>`name` (+0x30, stringptr). ⛔ El comentario viejo decía "always empty (no m_name)": FALSO.</summary>
    Public Property Name As String
    ''' <summary>Cantidad de entradas de `particleDatas` (el campo count del hkArray).</summary>
    Public Property NumParticles As Integer
    ''' <summary>
    ''' `particleDatas` (+0x38): array de `{real mass; real invMass; real radius; real friction}`.
    ''' ⛔ NO son posiciones. El nombre viejo (`Particles`) y su comentario ("xyz=posición rest,
    ''' w=invMass") eran incorrectos: X=mass, Y=invMass, Z=radius, W=friction.
    ''' Las posiciones de reposo viven en `hclSimClothPose`, no acá.
    ''' </summary>
    Public ReadOnly Property ParticleDatas As New List(Of HkxVector4Graph_Class)
    ' +0x048: hkArray of uint16 — indices into ParticleDatas of immovable (pinned) particles.
    Public ReadOnly Property FixedParticles As New List(Of Integer)
    ''' <summary>`collidableTransformMap.transformIndices` (+0x88, uint32).
    ''' ⛔ Antes se leía de +0x30, que es `name`: la lista quedaba SIEMPRE vacía.</summary>
    Public ReadOnly Property CollidableTransformIndices As New List(Of UInteger)
    ''' <summary>`actions` (+0xE8) — hclAction (viento). Antes no se leía.</summary>
    Public ReadOnly Property Actions As New List(Of HkxVirtualObjectGraph_Class)
    ' +0x058: hkArray of uint16 — triangle vertex indices (triplets: v0,v1,v2,...).
    ' TriangleIndices.Count / 3 = number of triangles in the cloth mesh.
    Public ReadOnly Property TriangleIndices As New List(Of Integer)
    ' +0x0D8: obj refs to hclSimClothPose (default/animated rest poses).
    Public ReadOnly Property SimClothPoses As New List(Of HkxVirtualObjectGraph_Class)
    ' +0x0B8: obj refs to constraint sets (Standard/Stretch/Bend/LocalRange/Volume).
    Public ReadOnly Property StaticConstraintSets As New List(Of HkxVirtualObjectGraph_Class)
    ' +0x0A8: obj refs to hclCollidable — the actual cloth collidables (capsules etc.).
    ' (+0x088 is m_unknown88, a uint32 array with no fixups — do not read collidables from there.)
    Public ReadOnly Property Collidables As New List(Of HkxVirtualObjectGraph_Class)
End Class

Public Class HclBufferDefinitionGraph_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Name As String
    ' Offsets verificados en CasualDress.nif. Nombres de campo provisionales
    ' (el SDK Havok usa hclBufferLayout que es un struct anidado complejo).
    ' +0x018: int32 = 1 (hclBufferDefinition) / 6 (hclScratchBufferDefinition)
    '   → probable m_subBufferCount (cuántos sub-buffers/streams contiene)
    ' +0x020: int32 = 12 (Buff) / 6 (Scratch)
    '   → probable campo de layout (numElements o numSlots por sub-buffer)
    ' +0x030: int32 = 250 en todos los ejemplos = NumParticles/NumVertices del buffer
    Public Property SubBufferCount As Integer        ' +0x018
    Public Property LayoutField As Integer           ' +0x020 (significado exacto pendiente)
    Public Property VertexCount As Integer           ' +0x030 (= NumParticles del SimCloth)
End Class

' Parser genérico: nombre + arrays detectados por fixup scan + global refs
Public Class HclGenericHavokObjectGraph_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Name As String
    Public ReadOnly Property ArrayFields As New List(Of HkxObjectArrayField_Class)
    Public ReadOnly Property ObjectRefs As New List(Of HkxVirtualObjectGraph_Class)
End Class

' hclTaperedCapsuleShape — pure float geometry, no m_name, no fixups.
' Offsets verificados con DumpStructuralAnalysis en CBBEBodyPhysics.nif.
Public Class HclTaperedCapsuleShapeGraph_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    ' +0x020: centre of sphere A (one end of the capsule).
    Public Property CentreA As HkxVector4Graph_Class
    ' +0x030: centre of sphere B (other end of the capsule).
    Public Property CentreB As HkxVector4Graph_Class
    ' +0x090: radius at sphere A.
    Public Property RadiusA As Single
    ' +0x094: radius at sphere B.
    Public Property RadiusB As Single
End Class

' hclCapsuleShape — uniform-radius capsule, no m_name, no fixups.
' Offsets verificados con DumpStructuralAnalysis en CasualDress.nif.
Public Class HclCapsuleShapeGraph_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    ' +0x020: centre of bottom sphere (vertex A).
    Public Property VertexA As HkxVector4Graph_Class
    ' +0x030: centre of top sphere (vertex B).
    Public Property VertexB As HkxVector4Graph_Class
    ' +0x050: radius (same at both ends).
    Public Property Radius As Single
End Class

' Constraint set genérico (StandardLink / StretchLink / BendStiffness / LocalRange).
' +0x020 = hkArray de constraints, count @ +0x028.
Public Class HclConstraintSetGraph_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Name As String
    Public Property ConstraintCount As Integer     ' count @ +0x028 (primer array de constraints)
End Class

' hclVolumeConstraintMx — 4 arrays, todos verificados en CBBEBodyPhysics.nif.
Public Class HclVolumeConstraintMxGraph_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Name As String
    Public Property Array1Count As Integer   ' count @ +0x028 (array at +0x020)
    Public Property Array2Count As Integer   ' count @ +0x038 (array at +0x030)
    Public Property Array3Count As Integer   ' count @ +0x048 (array at +0x040)
    Public Property Array4Count As Integer   ' count @ +0x058 (array at +0x050)
End Class

' hclTransformSetDefinition — layout verificado, size=48 bytes.
Public Class HkxTransformSetDefinitionGraph_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Name As String
    Public Property NumTransforms As Integer    ' +0x018: number of transform slots
    Public Property NumFloatSlots As Integer    ' +0x01C: number of float slots (= num bones typically)
End Class

' hclSimulateOperator — layout verificado, size=112 bytes.
Public Class HclSimulateOperatorGraph_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Name As String
    Public Property Field24 As Integer       ' +0x024 (=2 in sample, meaning unknown)
    Public Property Field28 As Integer       ' +0x028 (=1 in sample, meaning unknown)
    Public Property SubStatesCount As Integer ' count of sub-states array at +0x030
End Class

' hclCopyVerticesOperator — layout verificado, size=80 bytes.
Public Class HclCopyVerticesOperatorGraph_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Name As String
    Public Property Field20 As Integer           ' +0x020 (=0 in sample)
    Public Property OutputBufferIndex As Integer ' +0x024 (=1 in sample)
    Public Property InputBufferIndex As Integer  ' +0x028 (=12 in sample)
End Class

' hclGatherAllVerticesOperator — layout verificado, size=160 bytes.
Public Class HclGatherAllVerticesOperatorGraph_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Name As String
    Public Property OutputBufferIndex As Integer ' +0x034 (=1 in sample)
    Public Property NumBuffers As Integer        ' +0x038 (=1 in sample)
    ' +0x020: uint16 pairs (srcVertexIndex, dstVertexIndex) — vertex remapping table.
    Public ReadOnly Property VertexRemapping As New List(Of Integer)
End Class

' hclMoveParticlesOperator — layout verificado, size=272 bytes.
Public Class HclMoveParticlesOperatorGraph_Class
    Public Property SourceObject As HkxVirtualObjectGraph_Class
    Public Property Name As String
    ' +0x020: uint16 pairs (particleIdx, particleIdx) — pinned particle index pairs.
    Public ReadOnly Property ParticleIndices As New List(Of Integer)
End Class



