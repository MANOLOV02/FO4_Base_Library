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
    ''' El TEXTO de un valor tal como lo muestra y lo acepta xEdit (<c>EditValue</c>), por tipo de definicion.
    ''' Es lo que leen y escriben las reglas del sorter: un error de formato aca cambia el resultado de una
    ''' condicion. Separador decimal SIEMPRE el punto: xEdit lo fuerza al arrancar (xEdit.dpr del tag,
    ''' <c>FormatSettings.DecimalSeparator := '.'</c>).
    ''' </summary>
    Public Module XEditValue

        ''' <summary>Decimales por defecto de un float (<c>wbFloatDigits</c>, wbIf:72).</summary>
        Public Const FloatDigits As Integer = 6

        ' ============================================================================================ lectura

        ''' <summary>EditValue de una hoja. <paramref name="rec"/> es el record al que pertenece (para resolver
        ''' referencias y textos localizados desde SU archivo).</summary>
        Public Function GetEditValue(node As WbNode, rec As XEditRecord) As String
            If node Is Nothing Then Return ""
            Dim d = node.Def
            If TypeOf d Is WbIntegerDef Then Return IntegerToText(DirectCast(d, WbIntegerDef), node)
            If TypeOf d Is WbFormIdDef Then Return FormIdToText(DirectCast(d, WbFormIdDef), CUInt(Convert.ToInt64(node.Value) And &HFFFFFFFFL), rec)
            If TypeOf d Is WbFloatDef Then Return FloatToText(DirectCast(d, WbFloatDef), CSng(node.Value))
            If TypeOf d Is WbStringDef Then Return If(TryCast(node.Value, String), "")
            If TypeOf d Is WbLStringDef Then Return LStringToText(node, rec)
            If TypeOf d Is WbLenStringDef Then Return If(TryCast(node.Value, String), "")
            If TypeOf d Is WbByteArrayDef Then Return BytesToText(TryCast(node.Value, Byte()))
            If TypeOf d Is WbEmptyDef Then Return ""
            ' Contenedores (struct, array): '' (TwbValueDef.ToEditValue, wbIf:19916-19920).
            Return ""
        End Function

        ''' <summary>
        ''' Entero (wbIf:13519-13559): con banderas, la cadena de 0/1 (<see cref="FlagsToText"/>); con enumerado,
        ''' el nombre o el numero si no lo tiene (wbIf:16256-16280); sin formateador, <c>IntToStr</c> con signo.
        ''' Un formateador por CALLBACK que no este transcrito es un error declarado, no un numero pelado.
        ''' </summary>
        Public Function IntegerToText(d As WbIntegerDef, node As WbNode) As String
            Dim v = Convert.ToInt64(node.Value)
            If d.FlagNames IsNot Nothing Then Return FlagsToText(d, v)
            If d.EnumValues IsNot Nothing Then
                Dim nombre As String = Nothing
                If d.EnumValues.TryGetValue(v, nombre) AndAlso nombre <> "" Then Return nombre
                Return v.ToString(CultureInfo.InvariantCulture)
            End If
            Select Case If(d.EnumName, "")
                Case "", "wbEnum", "wbFlags"
                    Return v.ToString(CultureInfo.InvariantCulture)
                Case Else
                    Throw New NotSupportedException($"EditValue of '{d.Name}': the text formatter '{d.EnumName}' is not transcribed.")
            End Select
        End Function

        ''' <summary>
        ''' Banderas (wbIf:15591-15606): se quitan los bits llamados <c>Unused</c> (wbIf:15164-15175) y queda una
        ''' cadena de 0/1 con el bit 0 primero, CORTADA despues del ultimo 1. Valor 0 = cadena vacia.
        ''' </summary>
        Public Function FlagsToText(d As WbIntegerDef, v As Long) As String
            Dim unusedMask As ULong = 0UL
            For i = 0 To Math.Min(d.FlagNames.Length, 64) - 1
                If String.Equals(d.FlagNames(i), "Unused", StringComparison.OrdinalIgnoreCase) Then unusedMask = unusedMask Or (1UL << i)
            Next
            Dim bits = BitConverter.ToUInt64(BitConverter.GetBytes(v), 0) And Not unusedMask
            Dim sb As New StringBuilder()
            Dim ultimo = -1
            For i = 0 To 63
                If (bits And (1UL << i)) <> 0UL Then ultimo = i
            Next
            For i = 0 To ultimo
                sb.Append(If((bits And (1UL << i)) <> 0UL, "1"c, "0"c))
            Next
            Return sb.ToString()
        End Function

        ''' <summary>Indice de una bandera por nombre (<c>FindFlag</c>, wbIf:15422-15461): sin mayusculas; acepta
        ''' tambien <c>0x..</c>, <c>$..</c> o el indice decimal. -1 si no existe.</summary>
        Public Function FindFlag(d As WbIntegerDef, name As String) As Integer
            If d.FlagNames Is Nothing Then Return -1
            For i = 0 To d.FlagNames.Length - 1
                If String.Equals(d.FlagNames(i), name, StringComparison.OrdinalIgnoreCase) Then Return i
            Next
            Dim n As Integer
            If name.StartsWith("0x", StringComparison.OrdinalIgnoreCase) AndAlso Integer.TryParse(name.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, n) Then Return n
            If name.StartsWith("$") AndAlso Integer.TryParse(name.Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, n) Then Return n
            If Integer.TryParse(name, NumberStyles.None, CultureInfo.InvariantCulture, n) Then Return n
            Return -1
        End Function

        ''' <summary>
        ''' Float (<c>TwbFloatDef.ToValue</c> wbIf:17043-17151 y <c>ToStringInternal</c> wbIf:17242-17290):
        ''' <list type="bullet">
        ''' <item>bits <c>7F7FFFFF</c> → <c>Default</c>, <c>FF7FFFFF</c> → <c>Min</c>; NaN/±Inf por nombre;</item>
        ''' <item>distinto de cero pero <c>SingleSameValue(v, 0)</c> → 0;</item>
        ''' <item>normalizador (angulos: no transcrito), despues <c>v * escala</c>;</item>
        ''' <item>con decimales (el default -1 vale <c>wbFloatDigits</c> = 6, wbIf:16797-16804):
        ''' <c>RoundToEx(v, -d)</c> y <c>FloatToStrF(v, ffFixed, 99, d)</c>.</item>
        ''' </list>
        ''' <c>RoundToEx(v, -d)</c> = <c>Round(v / 10^-d) * 10^-d</c>, y <c>Round</c> de Delphi redondea al par.
        ''' </summary>
        Public Function FloatToText(d As WbFloatDef, f As Single) As String
            If d.IsAngle Then Throw New NotSupportedException($"EditValue of angle '{d.Name}' is not transcribed.")
            Dim bits = BitConverter.SingleToUInt32Bits(f)
            If bits = &H7F7FFFFFUI Then Return "Default"
            If bits = &HFF7FFFFFUI Then Return "Min"
            If Single.IsNaN(f) Then Return "NaN"
            If Single.IsPositiveInfinity(f) Then Return "Inf"
            If Single.IsNegativeInfinity(f) Then Return "-Inf"
            Dim v As Double = f
            If v <> 0 AndAlso DoSingleSameValue(f, 0.0F) Then v = 0
            If d.Scale <> 1.0 Then v = v * d.Scale
            Dim digits = If(d.Digits < 0, FloatDigits, d.Digits)
            Dim paso = Math.Pow(10, -digits)
            Dim r = Math.Round(v / paso, MidpointRounding.ToEven) * paso
            If r = 0 Then r = 0   ' Round devuelve un entero: -0 no sobrevive
            Return r.ToString("F" & digits.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture)
        End Function

        ''' <summary><c>DoSingleSameValue</c> (wbIf:5507-5512): <c>|A-B| &lt;= Max(Min(|A|,|B|) * res, res)</c> con
        ''' <c>res = 0.000000499999999999999999999</c>.</summary>
        Private Function DoSingleSameValue(a As Single, b As Single) As Boolean
            Const res As Single = 0.000000499999999999999999999F
            Return Math.Abs(a - b) <= Math.Max(Math.Min(Math.Abs(a), Math.Abs(b)) * res, res)
        End Function

        ''' <summary>Byte array (wbIf:19094-19118): hex mayusculas de 2 digitos separados por un espacio.</summary>
        Public Function BytesToText(b As Byte()) As String
            If b Is Nothing OrElse b.Length = 0 Then Return ""
            Return String.Join(" ", b.Select(Function(x) x.ToString("X2", CultureInfo.InvariantCulture)))
        End Function

        ''' <summary>LString (wbIf:21673-21703): el texto (resuelto contra las tablas si la fuente es localizada).
        ''' El id 0 es ''. Un id que no se resuelve NO se escribe nunca como texto de error (B30): se lanza.</summary>
        Public Function LStringToText(node As WbNode, rec As XEditRecord) As String
            If TypeOf node.Value Is String Then Return CStr(node.Value)
            Dim id As UInteger
            Try
                id = CUInt(Convert.ToInt64(node.Value) And &HFFFFFFFFL)
            Catch
                Return ""
            End Try
            If id = 0UI Then Return ""
            ' Un identificador presente con texto VACIO es un valor valido (las tablas del juego los traen); solo el que
            ' no esta en la tabla es el "<Error: Unknown lstring ID>" de xEdit (wbLocalization.pas:416-419).
            Dim t As String = Nothing
            If Not rec.Session.TryResolveText(rec, node, t) Then Throw New XEditUnresolvedTextException(rec, node, id)
            Return t
        End Function

        ''' <summary>
        ''' Referencia (wbIf:18436-18635, con <c>wbDisplayLoadOrderFormID</c>): <c>EDID "FULL" [SIG:XXXXXXXX]</c>
        ''' del record visible desde el archivo del elemento (wbI:4881-4900); <c>NULL - Null Reference
        ''' [00000000]</c>; <c>FFFF - None Reference [FFFFFFFF]</c>; los no resueltos con su aviso.
        ''' </summary>
        Public Function FormIdToText(d As WbFormIdDef, fid As UInteger, rec As XEditRecord) As String
            If fid = 0UI Then
                Dim allowsTrgt = d.AllowedSignatures.Contains("TRGT")
                Dim allowsNull = d.AllowedSignatures.Contains("NULL")
                If allowsTrgt AndAlso Not allowsNull Then Return "TARGET - Target Reference [00000000]"
                Return "NULL - Null Reference [00000000]"
            End If
            If fid = &HFFFFFFFFUI Then Return "FFFF - None Reference [FFFFFFFF]"
            Dim target = rec.Session.RecordVisibleFrom(fid, rec.FileName)
            If target Is Nothing Then
                If (fid And &HFFFFFFUI) < &H800UI Then
                    Return "[" & fid.ToString("X8", CultureInfo.InvariantCulture) & "] <Warning: Could not be resolved, but is possibly hardcoded in the engine>"
                End If
                Return "[" & fid.ToString("X8", CultureInfo.InvariantCulture) & "] <Error: Could not be resolved>"
            End If
            Return RecordName(target)
        End Function

        ''' <summary>
        ''' Nombre de un record (<c>GetShortNameInternal(True)</c>, wbI:12151-12190, con
        ''' <c>wbDisplayShorterNames</c>): EDID, un espacio, el FULL entre comillas (comillas internas duplicadas),
        ''' un espacio y <c>[SIG:FORMID]</c>. Los tipos con informacion adicional (REFR, CELL, DIAL, NAVM, INFO...)
        ''' no son de las firmas que lee el sorter: se declara no soportado.
        ''' </summary>
        Public Function RecordName(r As XEditRecord) As String
            Select Case r.Signature
                Case "REFR", "ACHR", "PGRE", "PMIS", "PARW", "PBEA", "PFLA", "PCON", "PBAR", "PHZD",
                     "CELL", "DIAL", "NAVM", "DLBR", "SCEN", "INFO", "LAND"
                    Throw New NotSupportedException($"Record name with additional info ({r.Signature}) is not transcribed.")
            End Select
            Dim sb As New StringBuilder()
            Dim edid = r.EditorID
            If edid <> "" Then sb.Append(edid)
            Dim full = FullName(r)
            If full <> "" Then
                If sb.Length > 0 Then sb.Append(" "c)
                sb.Append(""""c).Append(full.Replace("""", """""")).Append(""""c)
            End If
            If sb.Length > 0 Then sb.Append(" "c)
            sb.Append("["c).Append(r.Signature).Append(":"c).Append(r.FormID.ToString("X8", CultureInfo.InvariantCulture)).Append("]"c)
            Return sb.ToString()
        End Function

        ''' <summary>El FULL de un record (texto resuelto), o ''.</summary>
        Public Function FullName(r As XEditRecord) As String
            Dim n = r.Root.BySignature("FULL")
            If n Is Nothing OrElse n.ChildCount = 0 Then Return ""
            Return LStringToTextLenient(n.Children(0), r)
        End Function

        ''' <summary>Como <see cref="LStringToText"/> pero un id sin resolver da '' (para NOMBRES que se muestran,
        ''' no para valores que se escriben).</summary>
        Public Function LStringToTextLenient(node As WbNode, rec As XEditRecord) As String
            Try
                If TypeOf node.Def Is WbLStringDef Then Return LStringToText(node, rec)
                Return If(TryCast(node.Value, String), "")
            Catch ex As XEditUnresolvedTextException
                Return ""
            End Try
        End Function

        ' ============================================================================================ escritura

        ''' <summary>
        ''' Asigna un EditValue a una hoja (<c>SetEditValue</c>). Devuelve True si el valor CAMBIO: xEdit solo
        ''' escribe si el texto nuevo es distinto del actual (wbI:24621-24643).
        ''' </summary>
        Public Function SetEditValue(node As WbNode, rec As XEditRecord, value As String) As Boolean
            Dim d = node.Def
            If String.Equals(GetEditValueSafe(node, rec), value, StringComparison.Ordinal) Then Return False
            If TypeOf d Is WbIntegerDef Then
                node.Value = WbCajas.Caja(TextToInteger(DirectCast(d, WbIntegerDef), value, node))
                Return True
            End If
            If TypeOf d Is WbFormIdDef Then
                node.Value = WbCajas.Caja(TextToFormId(value, rec))
                Return True
            End If
            If TypeOf d Is WbFloatDef Then
                node.Value = WbCajas.Caja(TextToFloat(DirectCast(d, WbFloatDef), value))
                Return True
            End If
            If TypeOf d Is WbStringDef OrElse TypeOf d Is WbLenStringDef Then
                node.Value = value
                node.RawOverride = Nothing
                If TypeOf d Is WbStringDef AndAlso DirectCast(d, WbStringDef).FixedLength = 0 Then node.TerminatorCount = 1
                Return True
            End If
            If TypeOf d Is WbLStringDef Then
                ' Escribir texto en una LString de un archivo SIN tablas: queda texto inline (wbIf:21603-21631).
                node.Value = value
                node.RawOverride = Nothing
                node.ValorLocalizado = WbLocalizacion.Texto
                node.TerminatorCount = 1
                Return True
            End If
            If TypeOf d Is WbByteArrayDef Then
                node.Value = TextToBytes(DirectCast(d, WbByteArrayDef), value)
                Return True
            End If
            Throw New InvalidOperationException($"'{d.Name}' can not be edited")
        End Function

        Private Function GetEditValueSafe(node As WbNode, rec As XEditRecord) As String
            Try
                Return GetEditValue(node, rec)
            Catch ex As NotSupportedException
                Return Nothing
            Catch ex As XEditUnresolvedTextException
                Return Nothing
            End Try
        End Function

        ''' <summary>Entero desde texto (wbIf:13163-13178, 16071-16116, 15198-15211).</summary>
        Public Function TextToInteger(d As WbIntegerDef, value As String, node As WbNode) As Long
            If d.FlagNames IsNot Nothing Then
                Dim v As ULong = 0UL
                For i = 0 To value.Length - 1
                    Select Case value(i)
                        Case "1"c : v = v Or (1UL << i)
                        Case "0"c
                        Case Else : Throw New FormatException($"'{value}' is not a valid flags value")
                    End Select
                Next
                Return BitConverter.ToInt64(BitConverter.GetBytes(v), 0)
            End If
            If value = "" Then Return 0L
            If d.EnumValues IsNot Nothing Then
                For Each kv In d.EnumValues
                    If String.Equals(kv.Value, value, StringComparison.OrdinalIgnoreCase) Then Return kv.Key
                Next
            End If
            Dim r As Long
            If Long.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, r) Then Return r
            If value.StartsWith("$") AndAlso Long.TryParse(value.Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, r) Then Return r
            Throw New FormatException($"'{value}' is not a valid integer value")
        End Function

        ''' <summary>
        ''' Referencia desde texto (wbIf:17756-17862): el contenido del PRIMER <c>[...]</c> de 8 o 9 hex (saltando lo
        ''' que este entre comillas), quitando un prefijo <c>SIG:</c>; si no hay corchetes, el texto; se interpreta
        ''' como FormID DEL ORDEN DE CARGA. La pertenencia del dueño a los masters se resuelve al escribir el archivo
        ''' (la MAST sale del recorrido de emision), no aca.
        ''' </summary>
        Public Function TextToFormId(value As String, rec As XEditRecord) As UInteger
            Dim s = value.Trim()
            Dim hex As String = Nothing
            If s.Contains("["c) Then
                Dim i = 0
                Dim enComillas = False
                While i < s.Length
                    Dim c = s(i)
                    If c = """"c Then
                        enComillas = Not enComillas
                    ElseIf Not enComillas AndAlso c = "["c Then
                        Dim j = s.IndexOf("]"c, i + 1)
                        If j < 0 Then Exit While
                        Dim inner = s.Substring(i + 1, j - i - 1)
                        If (inner.Length = 13 OrElse inner.Length = 14) AndAlso inner(4) = ":"c Then inner = inner.Substring(5)
                        If inner.Length = 8 OrElse inner.Length = 9 Then
                            hex = inner
                            Exit While
                        End If
                        i = j
                    End If
                    i += 1
                End While
            Else
                If (s.Length = 13 OrElse s.Length = 14) AndAlso s(4) = ":"c Then s = s.Substring(5)
                hex = s
            End If
            If hex Is Nothing Then Throw New FormatException($"'{value}' is not a valid FormID value")
            If String.Equals(hex, "Self", StringComparison.OrdinalIgnoreCase) Then Return rec.FormID
            Dim r As UInteger
            If UInteger.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, r) Then Return r
            Throw New FormatException($"'{value}' is not a valid FormID value")
        End Function

        ''' <summary>Float desde texto (wbIf:16808-16967): punto decimal obligatorio (una coma es error),
        ''' especiales, tope en ±MaxSingle y division por la escala al reves (se multiplica).</summary>
        Public Function TextToFloat(d As WbFloatDef, value As String) As Single
            Dim s = value.Trim()
            If s = "" Then Return 0.0F
            Select Case s.ToLowerInvariant()
                Case "nan" : Return Single.NaN
                Case "inf", "+inf" : Return Single.PositiveInfinity
                Case "-inf" : Return Single.NegativeInfinity
                Case "default", "max" : Return BitConverter.UInt32BitsToSingle(&H7F7FFFFFUI)
                Case "min" : Return BitConverter.UInt32BitsToSingle(&HFF7FFFFFUI)
            End Select
            If s.Contains(","c) Then Throw New FormatException($"'{value}' is not a valid floating point value")
            Dim v As Double
            If Not Double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, v) Then
                Throw New FormatException($"'{value}' is not a valid floating point value")
            End If
            If v > Single.MaxValue Then v = Single.MaxValue
            If v < -Single.MaxValue Then v = -Single.MaxValue
            If d.Scale <> 1.0 Then v = v / d.Scale
            If v > Single.MaxValue Then v = Single.MaxValue
            If v < -Single.MaxValue Then v = -Single.MaxValue
            Return CSng(v)
        End Function

        ''' <summary>Byte array desde texto (wbIf:18680-18724): separadores espacio, coma y punto y coma; tamaño
        ''' fijo = se trunca o se completa.</summary>
        Public Function TextToBytes(d As WbByteArrayDef, value As String) As Byte()
            Dim partes = value.Split({" "c, ","c, ";"c}, StringSplitOptions.RemoveEmptyEntries)
            Dim out As New List(Of Byte)
            For Each p In partes
                If p.Length <> 2 Then Throw New FormatException($"'{value}' is not a valid byte array value")
                out.Add(Byte.Parse(p, NumberStyles.HexNumber, CultureInfo.InvariantCulture))
            Next
            If d.Size > 0 Then
                While out.Count < d.Size
                    out.Add(0)
                End While
                If out.Count > d.Size Then out.RemoveRange(d.Size, out.Count - d.Size)
            End If
            Return out.ToArray()
        End Function

    End Module

    ''' <summary>Un texto localizado que no se pudo resolver contra las tablas del idioma (B30: nunca se
    ''' escribe el texto de error de xEdit adentro del archivo).</summary>
    Public NotInheritable Class XEditUnresolvedTextException
        Inherits Exception
        Public Sub New(rec As XEditRecord, node As WbNode, id As UInteger)
            MyBase.New($"{rec}: {node.Path}: localized string 0x{id:X8} could not be resolved against the language tables.")
            StringId = id
        End Sub

        Public ReadOnly Property StringId As UInteger

        ''' <summary>El texto que xEdit MUESTRA para ese id (wbLocalization.pas:416-419). Solo para pantalla: nunca se
        ''' escribe (B30).</summary>
        Public ReadOnly Property DisplayText As String
            Get
                Return "<Error: Unknown lstring ID " & StringId.ToString("X8") & ">"
            End Get
        End Property
    End Class

End Namespace
