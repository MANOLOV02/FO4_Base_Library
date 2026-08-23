Option Strict On
Option Explicit On

Imports System.Collections.Generic
Imports System.Globalization
Imports System.Linq

' =================================================================================================
' LECTOR CANONICO GENERICO DE OBJETOS HAVOK
'
' ⛔ POR QUE EXISTE ESTO Y NO UN PARSER ESCRITO A MANO POR CLASE.
'
' La reflexion embebida en los .exe declara 946 clases en Fallout 4 y 609 en Skyrim SE, con el
' offset y el TIPO de cada campo. Escribir un parser tipado por clase significa 1.555 oportunidades
' de dejarse un campo, y eso fue exactamente lo que paso: un censo en runtime sobre el corpus
' vanilla mostro que de las 149 clases `hcl` los parsers a mano tocaban 3, y que en las que si
' tocaban faltaban campos enteros (`oneBlendEntries`, `startVertexIndex`, `partialWrite`...). Cada
' hueco es silencioso: no rompe el build, no rompe un gate, simplemente devuelve cero y la fisica
' se comporta mal sin que nada diga por que.
'
' Este lector le da la vuelta al problema: en vez de enumerar campos a mano, RECORRE LA TABLA. Lee
' todos los miembros que la clase declara, con el tipo que declara, resolviendo structs anidados y
' arrays. La cobertura es del 100 % POR CONSTRUCCION y no depende de que alguien se acuerde.
'
' Y es GAME-AWARE por el mismo mecanismo: la tabla sale del .exe de cada juego, asi que el mismo
' codigo lee Fallout 4 y Skyrim con los offsets de cada uno. No hay un `If juego = ...` en ningun
' lado; la unica decision es que tabla se usa, y esa la decide lo que el ARCHIVO declara en su
' header (ver HavokLayout.For).
'
' Los parsers tipados siguen existiendo y siguen siendo los que manda: son mas rapidos y exponen
' propiedades con nombre. Este lector es la RED: cubre todo lo que aquellos no miran, sirve de
' referencia contra la que compararlos, y hace medible la palabra "completo".
' =================================================================================================

Namespace Havok.Canon

    ''' <summary>Que clase de dato es un <see cref="HavokValue"/>. Se guarda explicito para que el
    ''' consumidor no tenga que adivinar cual de los campos mirar.</summary>
    Public Enum HavokValueKind
        Empty
        Number
        Text
        Vector
        Struct
        Items
        Reference
        Raw
    End Enum

    ''' <summary>Un valor leido de un objeto Havok, con su tipo declarado a cuestas.</summary>
    Public NotInheritable Class HavokValue
        Public Property Kind As HavokValueKind = HavokValueKind.Empty
        ''' <summary>Nombre del tipo tal como lo declara la reflexion (`real`, `uint32`, `vector4`...).</summary>
        Public Property DeclaredType As String = ""
        ''' <summary>Numeros (enteros, reales, bools como 0/1). Un solo campo evita convertir dos veces.</summary>
        Public Property Number As Double
        Public Property Text As String
        ''' <summary>vector4 / quaternion / transform / matrix3 / matrix4 / qstransform, en orden de archivo.</summary>
        Public Property Vector As Single()
        ''' <summary>Campos de un struct anidado.</summary>
        Public Property Fields As Dictionary(Of String, HavokValue)
        ''' <summary>Elementos de un array (hkArray, array fijo o relarray).</summary>
        Public Property Items As List(Of HavokValue)
        ''' <summary>Objeto apuntado, si el campo es un puntero y se pudo resolver.</summary>
        Public Property Reference As HkxVirtualObjectGraph_Class
        ''' <summary>Bytes crudos, para los tipos que no tienen una interpretacion util (`variant`).</summary>
        Public Property RawBytes As Byte()

        Public ReadOnly Property AsInt As Integer
            Get
                If Double.IsNaN(Number) OrElse Double.IsInfinity(Number) Then Return 0
                Return CInt(Math.Max(Integer.MinValue, Math.Min(Integer.MaxValue, Math.Truncate(Number))))
            End Get
        End Property

        Public ReadOnly Property AsSingle As Single
            Get
                Return CSng(Number)
            End Get
        End Property

        Public ReadOnly Property AsBool As Boolean
            Get
                Return Number <> 0.0R
            End Get
        End Property

        Public ReadOnly Property Count As Integer
            Get
                Return If(Items Is Nothing, 0, Items.Count)
            End Get
        End Property

        Public Overrides Function ToString() As String
            Select Case Kind
                Case HavokValueKind.Number
                    Return Number.ToString("0.####", CultureInfo.InvariantCulture)
                Case HavokValueKind.Text
                    Return If(Text, "")
                Case HavokValueKind.Vector
                    Return "(" & String.Join(", ", If(Vector, Array.Empty(Of Single)()).
                                             Select(Function(v) v.ToString("0.###", CultureInfo.InvariantCulture))) & ")"
                Case HavokValueKind.Items
                    Return $"[{Count}]"
                Case HavokValueKind.Struct
                    Return "{" & If(Fields Is Nothing, 0, Fields.Count) & " campos}"
                Case HavokValueKind.Reference
                    Return If(Reference Is Nothing, "null", "->" & Reference.ClassName)
                Case Else
                    Return "-"
            End Select
        End Function
    End Class

    ''' <summary>
    ''' Lee CUALQUIER objeto declarado por la reflexion, recorriendo la tabla en vez de una lista de
    ''' campos escrita a mano. Ver el encabezado del archivo para el porque.
    ''' </summary>
    Public NotInheritable Class HavokGenericReader

        Private Sub New()
        End Sub

        ''' <summary>
        ''' Tamano en bytes de un tipo declarado, en el layout x64 que describe la tabla.
        ''' <para>⛔ Estos NO son numeros elegidos: son los tamanos del layout que la propia tabla
        ''' describe (la tabla trae `objectSize` por clase, y los offsets consecutivos de sus miembros
        ''' los confirman). Un tamano mal puesto aca corre TODO un array de structs, asi que
        ''' <see cref="SizeOfType"/> devuelve -1 para lo que no sabe y el lector se planta en vez de
        ''' inventar un stride.</para>
        ''' </summary>
        Public Shared Function SizeOfType(layout As HavokLayout, typeName As String, subType As String, structClass As String) As Integer
            Select Case LCase(If(typeName, ""))
                Case "bool", "int8", "uint8", "char" : Return 1
                Case "int16", "uint16", "half" : Return 2
                Case "int32", "uint32", "real" : Return 4
                Case "int64", "uint64", "ulong", "pointer", "cstring", "stringptr", "variant" : Return 8
                Case "vector4", "quaternion" : Return 16
                Case "matrix3", "rotation" : Return 48
                Case "qstransform" : Return 48
                Case "matrix4", "transform" : Return 64
                Case "array", "simplearray" : Return 16
                Case "relarray" : Return 4
                Case "void" : Return 0
                Case "enum", "flags"
                    ' El ancho de un enum lo da su tipo BASE, que la tabla declara como subtipo.
                    Dim baseSize = SizeOfType(layout, subType, "", "")
                    Return If(baseSize > 0, baseSize, 4)
                Case "struct"
                    If layout Is Nothing OrElse String.IsNullOrEmpty(structClass) Then Return -1
                    Dim sz = layout.SizeOfClass(structClass)
                    Return If(sz > 0, sz, -1)
                Case Else
                    Return -1
            End Select
        End Function

        ''' <summary>
        ''' Lee todos los miembros que <paramref name="className"/> declara, en el objeto que empieza
        ''' en <paramref name="relativeOffset"/>. Devuelve Nothing si la clase no esta en la tabla del
        ''' juego (por ejemplo cualquier clase `hcl` en Skyrim, que no tiene motor de cloth).
        ''' </summary>
        Public Shared Function ReadObject(graph As HkxObjectGraph_Class, layout As HavokLayout,
                                          className As String, relativeOffset As Integer) As Dictionary(Of String, HavokValue)
            If graph Is Nothing OrElse layout Is Nothing Then Return Nothing
            If String.IsNullOrWhiteSpace(className) OrElse Not layout.HasClass(className) Then Return Nothing
            Dim members = layout.MembersOf(className)
            If members Is Nothing Then Return Nothing

            Dim result As New Dictionary(Of String, HavokValue)(StringComparer.OrdinalIgnoreCase)
            For Each m In members
                Try
                    result(m.Name) = ReadMember(graph, layout, relativeOffset + m.Offset, m, 0)
                Catch
                    ' Un campo ilegible no puede tumbar la lectura del objeto entero: se deja vacio y
                    ' el resto sigue. Un objeto a medias sigue siendo mas util que ninguno, y el hueco
                    ' queda visible como Kind=Empty en vez de como un cero indistinguible de un cero real.
                    result(m.Name) = New HavokValue With {.DeclaredType = m.TypeName}
                End Try
            Next
            Return result
        End Function

        ''' <summary>Igual que <see cref="ReadObject"/> pero tomando la clase del propio objeto.</summary>
        Public Shared Function ReadObject(graph As HkxObjectGraph_Class, layout As HavokLayout,
                                          source As HkxVirtualObjectGraph_Class) As Dictionary(Of String, HavokValue)
            If source Is Nothing Then Return Nothing
            Return ReadObject(graph, layout, source.ClassName, source.RelativeOffset)
        End Function

        ' -----------------------------------------------------------------------------------------
        Private Const MaxDepth As Integer = 6
        ''' <summary>Tope de elementos por array. Un hkArray con un `size` corrupto pedia millones de
        ''' elementos y colgaba el proceso; el bug del VMAD con el contador basura es el precedente.</summary>
        Private Const MaxItems As Integer = 200000

        Private Shared Function ReadMember(graph As HkxObjectGraph_Class, layout As HavokLayout,
                                           at As Integer, m As HavokMember, depth As Integer) As HavokValue
            Dim v As New HavokValue With {.DeclaredType = m.TypeName}
            Dim t = LCase(If(m.TypeName, ""))

            ' Array de tamano FIJO declarado en la propia tabla (`arrayCount`): son N elementos
            ' consecutivos del tipo base, sin cabecera de hkArray.
            If m.CArraySize > 1 AndAlso t <> "array" AndAlso t <> "simplearray" AndAlso t <> "relarray" Then
                Dim elemSize = SizeOfType(layout, m.TypeName, m.SubTypeName, m.StructClassName)
                If elemSize <= 0 Then Return v
                v.Kind = HavokValueKind.Items
                v.Items = New List(Of HavokValue)
                For i = 0 To Math.Min(m.CArraySize, MaxItems) - 1
                    v.Items.Add(ReadScalar(graph, layout, at + (i * elemSize), m.TypeName, m.SubTypeName, m.StructClassName, depth))
                Next
                Return v
            End If

            Select Case t
                Case "array", "simplearray"
                    Return ReadHkArray(graph, layout, at, m, depth)
                Case "relarray"
                    ' hkRelArray: uint16 size + uint16 offset RELATIVO al propio campo.
                    Dim n = CInt(CUShort(graph.ReadInt16(at)))
                    Dim off = CInt(CUShort(graph.ReadInt16(at + 2)))
                    v.Kind = HavokValueKind.Items
                    v.Items = New List(Of HavokValue)
                    Dim elemSize = SizeOfType(layout, m.SubTypeName, "", m.StructClassName)
                    If elemSize <= 0 OrElse n <= 0 Then Return v
                    For i = 0 To Math.Min(n, MaxItems) - 1
                        v.Items.Add(ReadScalar(graph, layout, at + off + (i * elemSize), m.SubTypeName, "", m.StructClassName, depth))
                    Next
                    Return v
                Case Else
                    Return ReadScalar(graph, layout, at, m.TypeName, m.SubTypeName, m.StructClassName, depth)
            End Select
        End Function

        Private Shared Function ReadHkArray(graph As HkxObjectGraph_Class, layout As HavokLayout,
                                            at As Integer, m As HavokMember, depth As Integer) As HavokValue
            Dim v As New HavokValue With {.DeclaredType = m.TypeName, .Kind = HavokValueKind.Items,
                                          .Items = New List(Of HavokValue)}
            Dim header = graph.ReadArrayHeader(at)
            If header Is Nothing OrElse header.Count <= 0 OrElse header.DataRelativeOffset < 0 Then Return v
            Dim n = Math.Min(header.Count, MaxItems)

            ' El elemento de un hkArray lo declara el SUBTIPO. Si es `struct` o `pointer`, la clase del
            ' elemento viene aparte.
            Dim elemType = If(String.IsNullOrEmpty(m.SubTypeName), "pointer", m.SubTypeName)
            Dim elemSize = SizeOfType(layout, elemType, "", m.StructClassName)
            If elemSize <= 0 Then Return v
            If depth >= MaxDepth Then Return v

            For i = 0 To n - 1
                v.Items.Add(ReadScalar(graph, layout, header.DataRelativeOffset + (i * elemSize),
                                       elemType, "", m.StructClassName, depth + 1))
            Next
            Return v
        End Function

        Private Shared Function ReadScalar(graph As HkxObjectGraph_Class, layout As HavokLayout, at As Integer,
                                           typeName As String, subType As String, structClass As String,
                                           depth As Integer) As HavokValue
            Dim v As New HavokValue With {.DeclaredType = typeName}
            Select Case LCase(If(typeName, ""))
                Case "bool"
                    v.Kind = HavokValueKind.Number : v.Number = If(graph.ReadByte(at) <> 0, 1.0R, 0.0R)
                Case "int8"
                    v.Kind = HavokValueKind.Number : v.Number = CDbl(CSByte(CType(graph.ReadByte(at), Integer) - If(graph.ReadByte(at) > 127, 256, 0)))
                Case "uint8", "char"
                    v.Kind = HavokValueKind.Number : v.Number = CDbl(graph.ReadByte(at))
                Case "int16"
                    v.Kind = HavokValueKind.Number : v.Number = CDbl(graph.ReadInt16(at))
                Case "uint16"
                    v.Kind = HavokValueKind.Number : v.Number = CDbl(CUShort(graph.ReadInt16(at)))
                Case "int32"
                    v.Kind = HavokValueKind.Number : v.Number = CDbl(graph.ReadInt32(at))
                Case "uint32"
                    v.Kind = HavokValueKind.Number : v.Number = CDbl(graph.ReadUInt32(at))
                Case "int64", "uint64", "ulong"
                    Dim lo = CLng(graph.ReadUInt32(at)), hi = CLng(graph.ReadUInt32(at + 4))
                    v.Kind = HavokValueKind.Number : v.Number = CDbl(lo + (hi * 4294967296L))
                Case "real"
                    v.Kind = HavokValueKind.Number : v.Number = CDbl(graph.ReadSingle(at))
                Case "half"
                    ' half IEEE-754 de 16 bits. Se decodifica a mano: no hay conversion directa en
                    ' el framework para el patron crudo leido de un archivo.
                    v.Kind = HavokValueKind.Number : v.Number = CDbl(HalfToSingle(CUShort(graph.ReadInt16(at))))
                Case "enum", "flags"
                    Dim baseSize = SizeOfType(layout, subType, "", "")
                    v.Kind = HavokValueKind.Number
                    Select Case baseSize
                        Case 1 : v.Number = CDbl(graph.ReadByte(at))
                        Case 2 : v.Number = CDbl(CUShort(graph.ReadInt16(at)))
                        Case Else : v.Number = CDbl(graph.ReadUInt32(at))
                    End Select
                Case "vector4", "quaternion"
                    v.Kind = HavokValueKind.Vector : v.Vector = ReadFloats(graph, at, 4)
                Case "matrix3", "rotation", "qstransform"
                    v.Kind = HavokValueKind.Vector : v.Vector = ReadFloats(graph, at, 12)
                Case "matrix4", "transform"
                    v.Kind = HavokValueKind.Vector : v.Vector = ReadFloats(graph, at, 16)
                Case "stringptr", "cstring"
                    v.Kind = HavokValueKind.Text : v.Text = graph.ResolveLocalString(at)
                Case "pointer"
                    v.Kind = HavokValueKind.Reference : v.Reference = graph.ResolveGlobalObject(at)
                Case "struct"
                    If depth >= MaxDepth OrElse String.IsNullOrEmpty(structClass) Then Return v
                    Dim inner = ReadObject(graph, layout, structClass, at)
                    If inner IsNot Nothing Then
                        v.Kind = HavokValueKind.Struct
                        v.Fields = inner
                    End If
                Case "variant"
                    v.Kind = HavokValueKind.Raw : v.RawBytes = graph.ReadBytes(at, 8)
                Case "void"
                    ' Nada que leer.
                Case Else
                    v.Kind = HavokValueKind.Raw : v.RawBytes = graph.ReadBytes(at, 4)
            End Select
            Return v
        End Function

        Private Shared Function ReadFloats(graph As HkxObjectGraph_Class, at As Integer, n As Integer) As Single()
            Dim r(n - 1) As Single
            For i = 0 To n - 1
                r(i) = graph.ReadSingle(at + (i * 4))
            Next
            Return r
        End Function

        ''' <summary>half IEEE-754 (1 signo, 5 exponente, 10 mantisa) a Single.</summary>
        Private Shared Function HalfToSingle(bits As UShort) As Single
            Dim sign = (CInt(bits) >> 15) And 1
            Dim exp = (CInt(bits) >> 10) And &H1F
            Dim man = CInt(bits) And &H3FF
            Dim value As Single
            If exp = 0 Then
                value = CSng(man) * CSng(Math.Pow(2, -24))
            ElseIf exp = 31 Then
                value = If(man = 0, Single.PositiveInfinity, Single.NaN)
            Else
                value = CSng((1024 + man) * Math.Pow(2, exp - 25))
            End If
            Return If(sign <> 0, -value, value)
        End Function

    End Class

End Namespace
