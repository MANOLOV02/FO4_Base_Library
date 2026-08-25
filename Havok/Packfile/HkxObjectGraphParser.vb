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
' animation/binding.
'
' ⛔ ESTE ARCHIVO ES EL SUSTRATO, NO UN PARSER DE CLASES. No conoce un solo campo de una clase
' Havok: expone las primitivas que el codigo generado usa para leerlas (enteros, floats, strings,
' cabeceras de array, punteros resueltos por fixup). Quien sabe que campo hay y en que offset es
' la tabla de la reflexion (`HavokLayout*.vb`) y quien la lee es `HavokTyped.vb`.
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

    ' ⛔⛔ EL GRAFO SE ANCLA SOLO. Antes leia `RawBytes(ContentsSection.AbsoluteDataStart + rel)`
    ' y sacaba el juego de `Packfile.Header.PackfileFormat` — las DOS son salidas de parsear la
    ' cabecera, asi que el lector generado no podia leer el envoltorio: para arrancar necesitaba
    ' justo lo que el envoltorio produce. Con anclaje y formato propios esa circularidad no existe:
    ' el grafo de ARRANQUE se ancla en el byte 0 y `Hk_HkPackfileHeader` lee la cabecera como
    ' cualquier otra clase que la reflexion declara.
    Private ReadOnly _bytes As Byte()
    Private ReadOnly _ancla As Integer
    Private ReadOnly _fin As Integer
    Private ReadOnly _pointerSize As Integer

    ''' <summary>Que tabla de la reflexion aplica. Lo declara el propio formato del archivo.</summary>
    Public ReadOnly Property Formato As HkxPackfileFormat_Enum

    Private ReadOnly Property PointerSizeValue As Integer
        Get
            Return _pointerSize
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
        ' `Header` es un Structure: `IsNothing` sobre un tipo por valor da SIEMPRE False y el guard
        ' quedaba mudo. `IsValid` es la pregunta de verdad: hay grafo y hay tabla para ese juego.
        If Not packfile.Header.IsValid Then Throw New InvalidOperationException("The HKX packfile has not been parsed.")

        Me.Packfile = packfile
        Me.ContentsSection = packfile.GetSection(packfile.Header.ContentsSectionIndex)
        If IsNothing(Me.ContentsSection) Then Throw New InvalidOperationException("The HKX contents section was not found.")

        _bytes = packfile.RawBytes
        _ancla = ContentsSection.AbsoluteDataStart
        _fin = ContentsSection.DataEndAbsolute
        ' `pointerSize` es `layoutRules[0]`, y el formato lo derivo el envoltorio: ninguno de los dos
        ' es un campo aparte.
        _pointerSize = Math.Max(1, packfile.Header.LayoutRules(0))
        Me.Formato = packfile.Formato

        BuildIndices()
    End Sub

    ''' <summary>
    ''' ⛔ EL GRAFO DE ARRANQUE: anclado en el byte 0 del archivo, sin secciones ni fixups.
    ''' <para>Existe para UNA cosa: que `Hk_HkPackfileHeader` y `Hk_HkPackfileSectionHeader` puedan
    ''' leer el envoltorio con la misma tabla que todo lo demas. Los offsets relativos son, aca,
    ''' absolutos. No tiene objetos: la lista de objetos sale de los virtual-fixups, que es
    ''' precisamente lo que todavia no se leyo.</para>
    ''' </summary>
    Friend Sub New(bytes As Byte(), formato As HkxPackfileFormat_Enum, pointerSize As Integer)
        If IsNothing(bytes) Then Throw New ArgumentNullException(NameOf(bytes))
        _bytes = bytes
        _ancla = 0
        _fin = bytes.Length
        _pointerSize = Math.Max(1, pointerSize)
        Me.Formato = formato
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

    ''' <summary>
    ''' ⛔⛔ LOS BLOQUES QUE EL CONTENEDOR DECLARA EN UN ARREGLO, EN SU ORDEN.
    ''' <para>`hkaAnimationContainer` declara `skeletons`, `animations` y `bindings`. Sacar
    ''' esas listas con `GetObjectsByClassName(...)` ordenado por `RelativeOffset` es usar el
    ''' orden en que el serializador dejo los bloques, que no es una ley del formato.</para>
    ''' <para>Devuelve el BLOQUE (`HkxVirtualObjectGraph_Class`) y no el objeto leido porque
    ''' el arreglo es de la clase BASE: la subclase concreta la dice el nombre de clase del
    ''' bloque, que es lo unico que la declara.</para>
    ''' <para>Si el archivo no trae contenedor se cae al barrido por clase — ausencia conocida
    ''' del archivo, no una preferencia.</para>
    ''' </summary>
    Public Function BloquesDelContenedor(campo As String, claseSuelta As String()) As List(Of HkxVirtualObjectGraph_Class)
        Dim r As New List(Of HkxVirtualObjectGraph_Class)
        For Each c In GetObjectsByClassName("hkaAnimationContainer")
            Dim cont = Havok.Canon.Objects.HkObj_HkaAnimationContainer.Read(Me, c)
            If cont Is Nothing Then Continue For
            Dim n = 0
            Select Case campo
                Case "animations" : n = cont.Raw.AnimationsCount
                Case "bindings" : n = cont.Raw.BindingsCount
                Case Else : n = 0
            End Select
            For i = 0 To n - 1
                Dim b As HkxVirtualObjectGraph_Class = Nothing
                Select Case campo
                    Case "animations" : b = cont.Raw.AnimationsRef(i)
                    Case "bindings" : b = cont.Raw.BindingsRef(i)
                End Select
                If b IsNot Nothing Then r.Add(b)
            Next
        Next
        If r.Count > 0 Then Return r

        For Each cn In claseSuelta
            r.AddRange(GetObjectsByClassName(cn).OrderBy(Function(x) x.RelativeOffset))
        Next
        Return r
    End Function

    ''' <summary>
    ''' ⛔⛔ LOS ESQUELETOS QUE EL ARCHIVO DECLARA, EN EL ORDEN QUE LOS DECLARA.
    ''' <para>`hkaAnimationContainer` declara `skeletons` como `array of hkaSkeleton`: el
    ''' archivo DICE cuales son. Seis sitios del arbol hacian
    ''' `GetObjectsByClassName("hkaSkeleton").FirstOrDefault()` — o sea el primer BLOQUE de esa
    ''' clase que aparece en el packfile. El orden de los bloques no es una ley: es como
    ''' quedaron serializados, y con dos esqueletos elegir el primero es tirar una moneda.</para>
    ''' <para>Si el archivo no trae contenedor —pasa en los `.hkx` de esqueleto suelto, que son
    ''' un `hkaSkeleton` y nada mas— se cae al barrido por clase, que es lo que habia. Esa
    ''' rama es una AUSENCIA CONOCIDA del archivo, no una preferencia.</para>
    ''' </summary>
    Public Function Esqueletos() As List(Of Havok.Canon.Objects.HkObj_HkaSkeleton)
        Dim r As New List(Of Havok.Canon.Objects.HkObj_HkaSkeleton)
        For Each c In GetObjectsByClassName("hkaAnimationContainer")
            Dim cont = Havok.Canon.Objects.HkObj_HkaAnimationContainer.Read(Me, c)
            If cont Is Nothing OrElse cont.Skeletons Is Nothing Then Continue For
            For Each s In cont.Skeletons
                If s IsNot Nothing Then r.Add(s)
            Next
        Next
        If r.Count > 0 Then Return r

        For Each o In GetObjectsByClassName("hkaSkeleton")
            Dim s = Havok.Canon.Objects.HkObj_HkaSkeleton.Read(Me, o)
            If s IsNot Nothing Then r.Add(s)
        Next
        Return r
    End Function

    ''' <summary>El esqueleto del archivo: el PRIMERO QUE EL CONTENEDOR DECLARA, no el primer
    ''' bloque. Nothing si el archivo no trae ninguno.</summary>
    Public Function EsqueletoPrincipal() As Havok.Canon.Objects.HkObj_HkaSkeleton
        Dim e = Esqueletos()
        If e.Count = 0 Then Return Nothing
        Return e(0)
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
        Dim absoluteOffset = _ancla + relativeOffset
        If absoluteOffset < _ancla OrElse absoluteOffset >= _fin Then Return String.Empty

        Dim endOffset = absoluteOffset
        While endOffset < _fin AndAlso _bytes(endOffset) <> 0
            endOffset += 1
        End While

        Return Encoding.ASCII.GetString(_bytes, absoluteOffset, endOffset - absoluteOffset)
    End Function

    Public Function ReadInt16(relativeOffset As Integer) As Short
        EnsureReadable(relativeOffset, 2)
        Return BitConverter.ToInt16(_bytes, _ancla + relativeOffset)
    End Function

    Public Function ReadByte(relativeOffset As Integer) As Byte
        EnsureReadable(relativeOffset, 1)
        Return _bytes(_ancla + relativeOffset)
    End Function

    Public Function ReadInt32(relativeOffset As Integer) As Integer
        EnsureReadable(relativeOffset, 4)
        Return BitConverter.ToInt32(_bytes, _ancla + relativeOffset)
    End Function

    Public Function ReadUInt32(relativeOffset As Integer) As UInteger
        EnsureReadable(relativeOffset, 4)
        Return BitConverter.ToUInt32(_bytes, _ancla + relativeOffset)
    End Function

    Public Function ReadSingle(relativeOffset As Integer) As Single
        EnsureReadable(relativeOffset, 4)
        Return BitConverter.ToSingle(_bytes, _ancla + relativeOffset)
    End Function

    Public Function ReadBytes(relativeOffset As Integer, byteCount As Integer) As Byte()
        If byteCount <= 0 Then Return Array.Empty(Of Byte)()
        EnsureReadable(relativeOffset, byteCount)

        Dim result(byteCount - 1) As Byte
        Buffer.BlockCopy(_bytes, _ancla + relativeOffset, result, 0, byteCount)
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

    Public Function ReadByteArray(fieldRelativeOffset As Integer) As Byte()
        Dim header = ReadArrayHeader(fieldRelativeOffset)
        If header.Count <= 0 OrElse header.DataRelativeOffset < 0 Then Return Array.Empty(Of Byte)()
        Return ReadBytes(header.DataRelativeOffset, header.Count)
    End Function

    Private Sub EnsureReadable(relativeOffset As Integer, byteCount As Integer)
        Dim dataRelativeEnd = _fin - _ancla
        If relativeOffset < 0 OrElse byteCount < 0 OrElse relativeOffset + byteCount > dataRelativeEnd Then
            Throw New InvalidDataException($"Requested HKX range is out of bounds: offset=0x{relativeOffset:X} size={byteCount}.")
        End If
    End Sub

    ' floatSlots: hkArray<hkStringPtr> — canales float nombrados a los que se bindean float-tracks de animación
    ' (drivers faciales / additive / IK). NO son regiones de body-weight.
    Private Function ReadFloatSlotNames(source As HkxVirtualObjectGraph_Class, fieldOffset As Integer) As List(Of String)
        Dim result As New List(Of String)
        For Each entryOffset In ReadStructureOffsets(source.RelativeOffset + fieldOffset, PointerSizeValue)
            result.Add(ResolveLocalString(entryOffset))
        Next
        Return result
    End Function

    ''' <summary>Envuelve una cabecera de array ya resuelta por el lector generado. Toma la
    ''' CABECERA y no un offset a proposito: si tomara un offset, el llamador tendria que volver a
    ''' calcularlo y el numero volveria a vivir fuera de la tabla.</summary>
    Private Function CreateArrayField(header As HkxObjectArrayHeader_Class) As HkxObjectArrayField_Class
        Return New HkxObjectArrayField_Class With {
            .Header = header
        }
    End Function

    Private Function CreateArrayField(source As HkxVirtualObjectGraph_Class, fieldOffset As Integer) As HkxObjectArrayField_Class
        Return New HkxObjectArrayField_Class With {
            .Header = ReadArrayHeader(source.RelativeOffset + fieldOffset)
        }
    End Function

    ' -------------------------------------------------------------------------
    ' De acá en adelante: offsets HCL determinados empíricamente con
    ' DumpStructuralAnalysis sobre NIFs reales de FO4 64-bit, NO contra el SDK de Havok.
    ' Cada función documenta el archivo con el que se verificó su layout.
    ' -------------------------------------------------------------------------


    ''' <summary>
    ''' ⛔ NAVEGACION DEL GRAFO SOBRE NODOS DE BEHAVIOR — lo unico que quedaba vivo de
    ''' `HkxBehaviorGraphParser.vb`, que se borro.
    '''
    ''' <para>Ese archivo era un PARSER: leia campos de clases `hkb*` con offsets escritos a mano.
    ''' De eso no quedo nada — cada campo lo da el objeto generado. Lo que si quedo son dos
    ''' preguntas que NO son de un campo sino del GRAFO, y por eso viven aca, con las demas
    ''' primitivas: "que clips alcanza este generador siguiendo sus referencias" y "que strings
    ''' referencia este objeto". Ninguna de las dos la puede contestar la reflexion: no son
    ''' campos, son recorridos.</para>
    ''' </summary>
    ' Lee un hkArray<hkStringPtr> (cada elemento = puntero a string, stride = PointerSizeValue).
    ''' <summary>Strings del array, a partir de su CABECERA (lo que devuelve el lector generado).</summary>
    Private Function ReadStringPtrArray(header As HkxObjectArrayHeader_Class) As List(Of String)
        Dim result As New List(Of String)
        If header Is Nothing OrElse header.Count <= 0 OrElse header.DataRelativeOffset < 0 Then Return result
        For i = 0 To header.Count - 1
            result.Add(ResolveLocalString(header.DataRelativeOffset + (i * PointerSizeValue)))
        Next
        Return result
    End Function

    Private Function ReadStringPtrArray(fieldRelativeOffset As Integer) As List(Of String)
        Dim result As New List(Of String)
        Dim header = ReadArrayHeader(fieldRelativeOffset)
        If header.Count <= 0 OrElse header.DataRelativeOffset < 0 Then Return result
        For i = 0 To header.Count - 1
            result.Add(ResolveLocalString(header.DataRelativeOffset + (i * PointerSizeValue)))
        Next
        Return result
    End Function

    ' Todas las strings ASCII imprimibles referenciadas por local-fixups dentro del objeto.
    ''' <summary>
    ''' Todos los strings que un bloque referencia por local-fixup. Es utilidad del GRAFO — recorre
    ''' los fixups del packfile, no campos de una clase — y por eso es publica: la usan consumidores
    ''' que antes la recibian precocinada dentro de un wrapper (`AllStrings`, `Strings`).
    ''' </summary>
    Public Function ReadAllReferencedStrings(source As HkxVirtualObjectGraph_Class) As List(Of String)
        Dim result As New List(Of String)
        For Each lf In GetLocalFixupsInRange(source.RelativeOffset, source.Size)
            Dim s = ReadNullTerminatedString(lf.DestinationRelativeOffset)
            If IsPrintableString(s) Then result.Add(s)
        Next
        Return result
    End Function

    Private Shared Function IsPrintableString(s As String) As Boolean
        If String.IsNullOrEmpty(s) OrElse s.Length > 256 Then Return False
        For Each c In s
            If AscW(c) < 32 OrElse AscW(c) > 126 Then Return False
        Next
        Return True
    End Function

    Private Shared Function LooksLikeAnimationFile(s As String) As Boolean
        If String.IsNullOrEmpty(s) Then Return False
        Dim lc = s.ToLowerInvariant()
        Return (lc.EndsWith(".hkt") OrElse lc.EndsWith(".hkx")) AndAlso lc.Contains("animation")
    End Function

    ''' <summary>Resuelve el objeto referenciado por el puntero que vive EN un offset de source exacto
    ''' (lectura de campo por offset). Devuelve Nothing si no hay fixup global ahí (puntero null). El
    ''' puntero ocupa 8 bytes, así que se busca el fixup cuyo SourceRelativeOffset == el offset pedido
    ''' dentro de un rango de 8.</summary>
    Private Function ResolveGlobalRefAt(sourceRelativeOffset As Integer) As HkxVirtualObjectGraph_Class
        For Each gf In GetGlobalFixupsInRange(sourceRelativeOffset, 8)
            If gf.SourceRelativeOffset = sourceRelativeOffset Then Return GetObject(gf.TargetRelativeOffset)
        Next
        Return Nothing
    End Function


    ''' <summary>El `name` de CUALQUIER nodo de behavior. Va por el lector tipado de la clase
    ''' BASE `hkbNode` a proposito: esto corre sobre bloques de cualquier clase derivada, incluidas
    ''' las `BS*` de Bethesda, y el objeto `HkObj_*` existe por clase CONCRETA.</summary>
    Public Function ReadNodeName(obj As HkxVirtualObjectGraph_Class) As String
        If IsNothing(obj) Then Return ""
        Return If(New Havok.Canon.Typed.Hk_HkbNode(Me, obj).Name, String.Empty)
    End Function
    ''' <summary>Resumen "qué reproduce" un generador, recursando los wrappers (Fase 3a) hasta los
    ''' clips/behaviors/gamebryo reales. Sigue refs cuya clase sea generador; SM anidada = hoja "sm:".</summary>
    Public Function DescribeGenerator(gen As HkxVirtualObjectGraph_Class) As String
        If IsNothing(gen) Then Return ""
        Dim leaves As New List(Of String)
        CollectGeneratorLeaves(gen, leaves, New HashSet(Of Integer), 0)
        If leaves.Count = 0 Then Return gen.ClassName & " '" & ReadNodeName(gen) & "'"
        Dim distinct = leaves.Distinct(StringComparer.OrdinalIgnoreCase).ToList()
        If distinct.Count = 1 AndAlso gen.ClassName.Equals("hkbClipGenerator", StringComparison.OrdinalIgnoreCase) Then Return distinct(0)
        Return gen.ClassName & " → [" & String.Join(", ", distinct) & "]"
    End Function

    ' Recolecta las hojas (clip/behavior/gamebryo/sm) alcanzables siguiendo refs de generador.
    Private Sub CollectGeneratorLeaves(gen As HkxVirtualObjectGraph_Class, leaves As List(Of String), visited As HashSet(Of Integer), depth As Integer)
        If IsNothing(gen) OrElse depth > 8 OrElse Not visited.Add(gen.RelativeOffset) Then Return
        Dim cn = If(gen.ClassName, "")
        If cn.Equals("hkbClipGenerator", StringComparison.OrdinalIgnoreCase) Then
            ' ⛔ DEL LECTOR GENERADO: `hkbClipGenerator.animationName` sale por nombre.
            Dim c1 = Havok.Canon.Objects.HkObj_HkbClipGenerator.Read(Me, gen)
            leaves.Add("clip:" & If(c1 Is Nothing, "", If(c1.AnimationName, "")))
        ElseIf cn.Equals("hkbBehaviorReferenceGenerator", StringComparison.OrdinalIgnoreCase) Then
            Dim b1 = Havok.Canon.Objects.HkObj_HkbBehaviorReferenceGenerator.Read(Me, gen)
            leaves.Add("behavior:" & If(b1 Is Nothing, "", If(b1.BehaviorName, "")))
        ElseIf cn.Equals("BGSGamebryoSequenceGenerator", StringComparison.OrdinalIgnoreCase) Then
            ' ⛔ `BGSGamebryoSequenceGenerator` es de Bethesda y NO esta en la reflexion de Havok;
            ' se lee con el mismo offset que el behaviorReference porque comparte el layout del
            ' generador base. Sigue siendo un offset a mano y por eso queda dicho aca.
            Dim g1 As New Havok.Canon.Typed.Hk_HkbBehaviorReferenceGenerator(Me, gen)
            leaves.Add("gamebryo:" & If(g1.IsValid, g1.BehaviorName, ""))
        ElseIf cn.Equals("hkbStateMachine", StringComparison.OrdinalIgnoreCase) Then
            Dim s1 = Havok.Canon.Objects.HkObj_HkbStateMachine.Read(Me, gen)
            leaves.Add("sm:" & If(s1 Is Nothing, "", If(s1.Name, "")))   ' SM anidada: no expandir
        Else
            ' wrapper (modifier/blender/child/selector/poseMatching/layer/…): seguir refs de generador.
            For Each gf In GetGlobalFixupsInRange(gen.RelativeOffset, gen.Size)
                Dim tgt = GetObject(gf.TargetRelativeOffset)
                If tgt IsNot Nothing AndAlso IsGeneratorClass(tgt.ClassName) Then
                    CollectGeneratorLeaves(tgt, leaves, visited, depth + 1)
                End If
            Next
        End If
    End Sub

    ''' <summary>
    ''' ⛔ LO DECIDE LA HERENCIA QUE DECLARA LA REFLEXION, NO EL NOMBRE.
    ''' <para>Antes esto era `nombre contiene "Generator"` mas un caso especial para
    ''' `hkbStateMachine`. Medido contra la union de las dos tablas: 40 clases derivan de
    ''' `hkbGenerator` y 58 contienen la palabra. La regla por nombre se perdia
    ''' `hkbBehaviorGraph` y los cinco `*TransitionEffect` (generadores de verdad, sin la
    ''' palabra en el nombre) y contaba como generadores 25 estructuras de estado interno.</para>
    ''' <para>Si el archivo es de un juego sin tabla (Skyrim32) no hay herencia que consultar y
    ''' la respuesta es False, igual que para cualquier clase que la tabla no declare.</para>
    ''' </summary>
    Private Function IsGeneratorClass(className As String) As Boolean
        Dim lay = Havok.Canon.HavokLayout.ForGraph(Me)
        If lay Is Nothing Then Return False
        Return lay.DerivaDe(className, "hkbGenerator")
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
    Public Property Header As HkxObjectArrayHeader_Class

End Class
