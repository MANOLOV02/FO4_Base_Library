' ============================================================================================
' Este archivo transcribe comportamiento de xEdit (TES5Edit), que esta bajo Mozilla Public
' License 2.0, y por lo tanto es una obra derivada de el.
'
' This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
' If a copy of the MPL was not distributed with this file, You can obtain one at
' https://mozilla.org/MPL/2.0/
'
' Proyecto original: https://github.com/TES5Edit/TES5Edit  (ElminsterAU y colaboradores)
' Las citas `wbI:` / `wbIf:` son de wbImplementation.pas / wbInterface.pas del tag xedit-4.1.5q.
' ============================================================================================
Option Strict On
Option Infer On

Imports System.Globalization
Imports System.Text
Imports FO4_Base_Library.Canon

Namespace XEdit

    ''' <summary>
    ''' La ley de ORDEN de los arreglos que xEdit declara ordenados (<c>wbArrayS</c>, <c>wbRArrayS</c>).
    ''' <para>CUANDO se ordena (medido sobre el parche de referencia y citado, ver el diseño §2 L5):</para>
    ''' <list type="bullet">
    ''' <item>copia de un record a otro archivo: los <c>wbArrayS</c> CONSERVAN el orden de la fuente
    ''' (<c>TwbArrayDef.GetSorted</c> devuelve False mientras corre la copia, wbIf:14247-14253); los <c>wbRArrayS</c>
    ''' SE ORDENAN (<c>TwbSubRecordArrayDef.GetSorted</c> no mira la copia, wbIf:12151-12157);</item>
    ''' <item>copia dentro de un record (parciales, <c>resetToMaster</c>): se ordenan los dos;</item>
    ''' <item>agregar un elemento (AddKeyword): va al final y no se reordena (medido: 8 ARMO).</item>
    ''' </list>
    ''' <para>La comparacion es <c>CompareStr</c> ordinal sobre la clave extendida, ESTABLE (el desempate de
    ''' <c>CompareSortKeys</c> compara un elemento contra si mismo, wbI:2151-2202, y <c>wbMergeSortPtr</c> es
    ''' estable, wbSort.pas:77-150): los empates conservan el orden previo.</para>
    ''' </summary>
    Public Module XSortLaw

        ''' <summary>Ordena los hijos de un arreglo de subrecords declarado ordenado.</summary>
        Public Sub SortRArray(arr As WbNode, rec As XEditRecord)
            SortChildren(arr, rec)
        End Sub

        ''' <summary>Ordena los elementos de un arreglo de valores (el nodo de valor del arreglo).</summary>
        Public Sub SortValueArray(arr As WbNode, rec As XEditRecord)
            SortChildren(arr, rec)
        End Sub

        Private Sub SortChildren(arr As WbNode, rec As XEditRecord)
            If arr.ChildCount < 2 Then Return
            Dim items = arr.Children.Select(Function(c, i) New With {.Node = c, .Key = SortKey(c, rec, True), .Pos = i}).ToList()
            Dim ordered = items.OrderBy(Function(x) x.Key, StringComparer.Ordinal).ThenBy(Function(x) x.Pos).ToList()
            Dim cambio = False
            For i = 0 To ordered.Count - 1
                If ordered(i).Pos <> i Then cambio = True
            Next
            If Not cambio Then Return
            arr.LimpiarHijos()
            For Each x In ordered
                arr.AddChild(x.Node)
            Next
        End Sub

        ''' <summary>Si el arreglo (nodo de valor o de subrecords) se ORDENA. LA SEDE UNICA de esa decision: la usan la
        ''' lectura (XElement), la copia (XCopy.SortTree) y la creacion de miembros.</summary>
        Public Function IsSortedArray(n As WbNode) As Boolean
            Dim ad = TryCast(n.Def, WbArrayDef)
            If ad IsNot Nothing Then Return ad.Sorted
            Dim ra = TryCast(n.Def, WbRArrayDef)
            If ra IsNot Nothing Then Return RArrayIsSorted(ra)
            Return False
        End Function

        ''' <summary>Un arreglo de subrecords declarado ordenado puede delegar en un callback (<c>GetSorted</c>,
        ''' wbInterface.pas:12151-12157). Transcripto: <c>wbFLSTLNAMIsSorted</c> devuelve False en sus dos ramas
        ''' (wbDefinitionsFO4.pas:1473-1491 del tag 4.1.5q, «Should not be sorted»), asi que el LNAM de un FLST NO se
        ''' ordena. Otro callback no esta transcripto.</summary>
        Public Function RArrayIsSorted(ra As WbRArrayDef) As Boolean
            If Not ra.Sorted Then Return False
            If String.IsNullOrEmpty(ra.SortedCallback) Then Return True
            If ra.SortedCallback = "wbFLSTLNAMIsSorted" Then Return False
            Throw New NotSupportedException($"Sorting '{ra.Name}' by the callback '{ra.SortedCallback}' is not transcribed.")
        End Function

        ''' <summary>La clave de orden de un elemento (<c>SortKey[aExtended]</c>).</summary>
        Public Function SortKey(n As WbNode, rec As XEditRecord, extended As Boolean) As String
            If n Is Nothing Then Return ""
            Dim d = n.Def
            If TypeOf d Is WbSubrecordDef Then
                ' Subrecord: la clave de su valor (wbI:16058-16076).
                If n.ChildCount = 0 Then Return ""
                Return SortKey(n.Children(0), rec, extended)
            End If
            If TypeOf d Is WbUnionDef Then
                If n.ChildCount = 0 Then Return ""
                Return SortKey(n.Children(0), rec, extended)
            End If
            If TypeOf d Is WbRStructDef Then Return RStructKey(n, DirectCast(d, WbRStructDef), rec, extended)
            If TypeOf d Is WbStructDef Then Return StructKey(n, DirectCast(d, WbStructDef), rec, extended)
            If TypeOf d Is WbFormIdDef Then Return FormIdKey(n)
            If TypeOf d Is WbIntegerDef Then Return IntegerKey(n, DirectCast(d, WbIntegerDef))
            If TypeOf d Is WbFloatDef Then Return FloatKey(n, DirectCast(d, WbFloatDef))
            If TypeOf d Is WbStringDef OrElse TypeOf d Is WbLStringDef OrElse TypeOf d Is WbLenStringDef Then
                ' String: UpperCase ASCII (wbIf:16578-16583; LenString y resto: UpperCase(ToString), wbIf:19927-19933).
                ' Los "KC" (EDID, FULL) no pasan a mayusculas (wbIf:22129-22143): el esquema no los distingue y
                ' ningun arreglo ordenado del sorter los usa como clave; si aparece uno, se declara.
                Dim s = If(TryCast(n.Value, String), "")
                Return AsciiUpper(s)
            End If
            If TypeOf d Is WbByteArrayDef Then
                Return XEditValue.BytesToText(TryCast(n.Value, Byte()))
            End If
            If TypeOf d Is WbEmptyDef Then Return ""
            If TypeOf d Is WbArrayDef Then
                ' Un arreglo sin clave propia: sus elementos unidos por '|' (TwbValueDef.ToSortKey por defecto).
                Return String.Join("|", n.Children.Select(Function(c) SortKey(c, rec, extended)))
            End If
            Throw New NotSupportedException($"Sort key of '{d?.Name}' ({d?.GetType().Name}) is not transcribed.")
        End Function

        ''' <summary>FormID (wbIf:18387-18434): FormID del orden de carga en 8 hex mayusculas.</summary>
        Private Function FormIdKey(n As WbNode) As String
            Dim fid = CUInt(Convert.ToInt64(n.Value) And &HFFFFFFFFL)
            Return fid.ToString("X8", CultureInfo.InvariantCulture)
        End Function

        ''' <summary>
        ''' Entero (wbIf:13646-13697): con formateador de banderas, 64 caracteres 0/1 con el bit 0 primero
        ''' (wbIf:15608-15618); si no, hex con <c>2*bytes+1</c> digitos, sumando <c>|Low|</c> a los tipos con signo.
        ''' </summary>
        Private Function IntegerKey(n As WbNode, d As WbIntegerDef) As String
            Dim v = Convert.ToInt64(n.Value)
            If d.FlagNames IsNot Nothing Then
                Dim sb As New StringBuilder(64)
                Dim u = BitConverter.ToUInt64(BitConverter.GetBytes(v), 0)
                For i = 0 To 63
                    sb.Append(If((u And (1UL << i)) <> 0UL, "1"c, "0"c))
                Next
                Return sb.ToString()
            End If
            Select Case If(d.EnumName, "")
                Case "", "wbEnum"
                Case "wbScriptObjectAliasToStr"
                    ' ctToSortKey: IntToHex64(aInt, 8) (wbDefinitionsCommon.pas:4175-4190 y :3163); un negativo
                    ' sale en complemento a dos de 16 digitos, como IntToHex64 de Delphi.
                    Dim h = BitConverter.ToUInt64(BitConverter.GetBytes(v), 0).ToString("X", CultureInfo.InvariantCulture)
                    Return If(h.Length < 8, h.PadLeft(8, "0"c), h)
                Case Else
                    Throw New NotSupportedException($"Sort key of '{d.Name}': the formatter '{d.EnumName}' is not transcribed.")
            End Select
            Dim ancho = WbIntegerDef.WidthOf(d.IntType)
            Select Case d.IntType
                Case WbIntType.s8 : v += 128L
                Case WbIntType.s16 : v += 32768L
                Case WbIntType.s32 : v += 2147483648L
            End Select
            Dim hex = BitConverter.ToUInt64(BitConverter.GetBytes(v), 0).ToString("X", CultureInfo.InvariantCulture)
            Dim digits = ancho * 2 + 1
            Return If(hex.Length < digits, hex.PadLeft(digits, "0"c), hex)
        End Function

        ''' <summary>Float (wbIf:17178-17232): signo + valor absoluto fijo con sus decimales, con ceros a la
        ''' izquierda hasta 39; NaN = 40 espacios, +inf = 40 '+', -inf = 40 '-'.</summary>
        Private Function FloatKey(n As WbNode, d As WbFloatDef) As String
            Dim f = CSng(n.Value)
            If Single.IsNaN(f) Then Return New String(" "c, 40)
            If Single.IsPositiveInfinity(f) Then Return New String("+"c, 40)
            If Single.IsNegativeInfinity(f) Then Return New String("-"c, 40)
            Dim bits = BitConverter.SingleToUInt32Bits(f)
            If bits = &H7F7FFFFFUI Then Return "+" & New String("9"c, 39)
            If bits = &HFF7FFFFFUI Then Return "-" & New String("9"c, 39)
            Dim v As Double = f
            If Math.Abs(v) <= Single.Epsilon Then v = 0
            If d.Scale <> 1.0 Then v = v * d.Scale
            Dim digits = If(d.Digits < 0, XEditValue.FloatDigits, d.Digits)
            Dim s = Math.Abs(v).ToString("F" & digits.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture)
            If s.Length < 39 Then s = New String("0"c, 39 - s.Length) & s
            Return If(v < 0, "-", "+") & s
        End Function

        ''' <summary>Struct de valores (wbIf:14867-14946): claves de los miembros de la clave de orden unidas por
        ''' '|', y con la extendida '|' + los de la clave extendida; sin clave, TODOS los miembros unidos por '|'.</summary>
        Private Function StructKey(n As WbNode, d As WbStructDef, rec As XEditRecord, extended As Boolean) As String
            Dim sk = If(d.SortKey, Array.Empty(Of Integer)())
            Dim ex = If(d.ExSortKey, Array.Empty(Of Integer)())
            If sk.Length > 0 OrElse (extended AndAlso ex.Length > 0) Then
                Dim sb As New StringBuilder()
                For i = 0 To sk.Length - 1
                    If sk(i) < n.ChildCount Then sb.Append(SortKey(n.Children(sk(i)), rec, extended))
                    If i < sk.Length - 1 Then sb.Append("|"c)
                Next
                If extended Then
                    If sk.Length > 0 AndAlso ex.Length > 0 Then sb.Append("|"c)
                    For i = 0 To ex.Length - 1
                        If ex(i) < n.ChildCount Then sb.Append(SortKey(n.Children(ex(i)), rec, extended))
                        If i < ex.Length - 1 Then sb.Append("|"c)
                    Next
                End If
                Return sb.ToString()
            End If
            Return String.Join("|", n.Children.Select(Function(c) SortKey(c, rec, extended)))
        End Function

        ''' <summary>Grupo de subrecords (wbI:21697-21720): SOLO los miembros de su clave de orden, por posicion
        ''' declarada, unidos por '|'; sin clave, cadena vacia.</summary>
        Private Function RStructKey(n As WbNode, d As WbRStructDef, rec As XEditRecord, extended As Boolean) As String
            Dim sk = If(d.SortKey, Array.Empty(Of Integer)())
            If sk.Length = 0 Then Return ""
            Dim keys = sk.ToList()
            If extended AndAlso d.ExSortKey IsNot Nothing Then keys.AddRange(d.ExSortKey)
            Dim sb As New StringBuilder()
            For i = 0 To keys.Count - 1
                Dim member = d.Members(keys(i))
                For Each c In n.Children
                    If c.Def Is member Then
                        sb.Append(SortKey(c, rec, extended))
                        Exit For
                    End If
                Next
                If i < keys.Count - 1 Then sb.Append("|"c)
            Next
            Return sb.ToString()
        End Function

        ''' <summary><c>UpperCase</c> de Delphi: solo a-z.</summary>
        Private Function AsciiUpper(s As String) As String
            Dim cs = s.ToCharArray()
            For i = 0 To cs.Length - 1
                If cs(i) >= "a"c AndAlso cs(i) <= "z"c Then cs(i) = ChrW(AscW(cs(i)) - 32)
            Next
            Return New String(cs)
        End Function

    End Module

End Namespace
