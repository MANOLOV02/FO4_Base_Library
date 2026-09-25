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
Imports FO4_Base_Library.Canon

Namespace XEdit

    ''' <summary>
    ''' Un elemento de un record visto como lo ve xEdit (<c>IwbElement</c>/<c>IwbContainer</c>), sobre el arbol del
    ''' motor. Las diferencias entre los dos modelos son de FORMA y se resuelven aca:
    ''' <list type="bullet">
    ''' <item>en xEdit el SUBRECORD es el contenedor de su valor: los miembros de un struct o los elementos de un
    ''' arreglo cuelgan directo del subrecord; en el motor cuelgan del nodo de valor, hijo del subrecord;</item>
    ''' <item>una UNION en xEdit es directamente la rama elegida; en el motor es un nivel con un hijo;</item>
    ''' <item>el record tiene como primer elemento la cabecera (<c>Record Header</c>), que en el motor vive en el
    ''' contexto y no en el arbol;</item>
    ''' <item>las banderas encendidas de un campo de banderas son hijos con nombre (<c>wbFlagsAsArray</c>).</item>
    ''' </list>
    ''' </summary>
    Public NotInheritable Class XElement

        Public Enum Kinds
            Record
            Header
            HeaderField
            Member
            Value
            Flag
        End Enum

        Public ReadOnly Property Kind As Kinds
        Public ReadOnly Property Record As XEditRecord
        ''' <summary>Nodo del motor (miembro o valor). Nothing en la cabecera y sus campos.</summary>
        Public ReadOnly Property Node As WbNode
        Public ReadOnly Property Container As XElement
        Private ReadOnly _field As String
        Private ReadOnly _flagIndex As Integer

        Private Sub New(kind As Kinds, rec As XEditRecord, node As WbNode, container As XElement,
                        Optional field As String = Nothing, Optional flagIndex As Integer = -1)
            _Kind = kind
            _Record = rec
            _Node = node
            _Container = container
            _field = field
            _flagIndex = flagIndex
        End Sub

        Public Shared Function ForRecord(rec As XEditRecord) As XElement
            Return New XElement(Kinds.Record, rec, rec.Root, Nothing)
        End Function

        Private Function Wrap(n As WbNode) As XElement
            If TypeOf n.Def Is WbMemberDef Then Return New XElement(Kinds.Member, _Record, n, Me)
            Return New XElement(Kinds.Value, _Record, n, Me)
        End Function

        ''' <summary>Una union es su rama elegida.</summary>
        Friend Shared Function Eff(n As WbNode) As WbNode
            While n IsNot Nothing AndAlso TypeOf n.Def Is WbUnionDef AndAlso n.ChildCount = 1
                n = n.Children(0)
            End While
            Return n
        End Function

        ''' <summary>El nodo de VALOR de un subrecord (su primer hijo, sin los bytes sin describir), efectivo.</summary>
        Private Shared Function ValueOf(sr As WbNode) As WbNode
            If sr Is Nothing OrElse sr.ChildCount = 0 Then Return Nothing
            Return Eff(sr.Children(0))
        End Function

        Private ReadOnly Property IsSubrecord As Boolean
            Get
                Return _Kind = Kinds.Member AndAlso TypeOf _Node.Def Is WbSubrecordDef
            End Get
        End Property

        ''' <summary>El nodo que tiene el valor o los hijos de este elemento en el modelo de xEdit.</summary>
        Private ReadOnly Property Content As WbNode
            Get
                Select Case _Kind
                    Case Kinds.Member
                        If IsSubrecord Then Return ValueOf(_Node)
                        Return _Node
                    Case Kinds.Value
                        Return Eff(_Node)
                    Case Kinds.Record
                        Return _Node
                    Case Else
                        Return Nothing
                End Select
            End Get
        End Property

        ''' <summary>La definicion de banderas del elemento (la de <c>FlagValues</c>, xejviScriptAdapterElement.pas:311-336), o
        ''' Nothing si no es un campo de banderas. Para <c>Record Header\Record Flags</c> es la de la firma del record.</summary>
        Public ReadOnly Property FlagDefinition As WbIntegerDef
            Get
                Return FlagsDef
            End Get
        End Property

        ''' <summary>La definicion de enteros-banderas del contenido, si es un campo de banderas.</summary>
        Private ReadOnly Property FlagsDef As WbIntegerDef
            Get
                If _Kind = Kinds.HeaderField AndAlso _field = "Record Flags" Then Return HeaderFlagsDef()
                Dim c = Content
                If c Is Nothing Then Return Nothing
                Dim d = TryCast(c.Def, WbIntegerDef)
                If d IsNot Nothing AndAlso d.FlagNames IsNot Nothing Then Return d
                Return Nothing
            End Get
        End Property

        Private Function HeaderFlagsDef() As WbIntegerDef
            Dim def = WbSchema.Get(_Record.Context.Game, _Record.Signature)
            Dim names = If(def?.RecordFlagNames, Array.Empty(Of String)())
            Return New WbIntegerDef("Record Flags", WbIntType.u32, "wbFlags", names)
        End Function

        ' ============================================================================================ nombres

        ''' <summary>
        ''' Nombre como xEdit: subrecord = <c>SIG - nombre de su definicion</c> (TwbSubRecord.GetName, wbI:16013-16021);
        ''' grupo y arreglo de subrecords = nombre de la definicion (wbI:21032, 21675); valor = nombre de la
        ''' definicion; bandera = nombre del bit; cabecera = <c>Record Header</c>.
        ''' </summary>
        Public ReadOnly Property Name As String
            Get
                Select Case _Kind
                    Case Kinds.Record : Return _Record.Signature
                    Case Kinds.Header : Return "Record Header"
                    Case Kinds.HeaderField : Return _field
                    Case Kinds.Flag : Return FlagsOfContainer()?.FlagNames(_flagIndex)
                    Case Kinds.Member
                        If IsSubrecord Then Return _Node.Signature & " - " & SubrecordDefName(_Node)
                        Return If(_Node.Def.Name, "")
                    Case Else
                        Return _Node.Name
                End Select
            End Get
        End Property

        Private Function FlagsOfContainer() As WbIntegerDef
            Return _Container?.FlagsDef
        End Function

        ''' <summary>El nombre de la definicion del subrecord: uno declarado vacio se llama como su firma
        ''' (<c>TwbSignatureDef.Create</c>, wbInterface.pas:10764-10765; ej. el DNAM de AMMO es <c>DNAM - DNAM</c>). Esa ley
        ''' ya la aplica <see cref="WbSubrecordDef.Name"/>; el nombre del VALOR queda vacio.</summary>
        Private Shared Function SubrecordDefName(sr As WbNode) As String
            Dim d = DirectCast(sr.Def, WbSubrecordDef)
            Return If(d.Name, "")
        End Function

        ''' <summary>DisplayName (wbI:15898-15930, 24426): subrecord = <c>SIG - nombre del valor resuelto</c> (la rama
        ''' de una union) o el de la definicion; valor = nombre de la definicion resuelta.</summary>
        Public ReadOnly Property DisplayName As String
            Get
                Select Case _Kind
                    Case Kinds.Member
                        If IsSubrecord Then
                            Dim v = ValueOf(_Node)
                            Dim s = If(v?.Def?.Name, "")
                            If s <> "" Then Return _Node.Signature & " - " & s
                            Return _Node.Signature & " - " & SubrecordDefName(_Node)
                        End If
                        Return Name
                    Case Kinds.Value
                        Dim e = Eff(_Node)
                        Dim s = If(e?.Def?.Name, "")
                        Return If(s <> "", s, _Node.Name)
                    Case Else
                        Return Name
                End Select
            End Get
        End Property

        ''' <summary>Firma: la del subrecord; la de un grupo o arreglo de subrecords es la de su PRIMER hijo que
        ''' sea un subrecord (wbI:21036-21056, 21680-21695); '' para el resto.</summary>
        Public ReadOnly Property Signature As String
            Get
                If _Kind = Kinds.Record Then Return _Record.Signature
                If _Kind <> Kinds.Member Then Return ""
                If IsSubrecord Then Return _Node.Signature
                For Each c In _Node.Children
                    If TypeOf c.Def Is WbSubrecordDef Then Return c.Signature
                Next
                Return ""
            End Get
        End Property

        ' ============================================================================================ hijos

        ''' <summary>Los hijos en el modelo de xEdit.</summary>
        ''' <param name="indexOrder">True (acceso POR INDICE: <c>ElementByIndex</c>, <c>ElementCount</c>, recorrer): un arreglo
        ''' declarado ordenado sale ordenado, como <c>GetElement(i)</c> → <c>DoInit(True)</c>. False (acceso POR NOMBRE):
        ''' el orden guardado, como <c>GetElementByName</c> → <c>DoInit(False)</c> (wbImplementation.pas:7568).</param>
        Public Function Children(Optional indexOrder As Boolean = True) As List(Of XElement)
            Dim out As New List(Of XElement)
            Select Case _Kind
                Case Kinds.Record
                    out.Add(New XElement(Kinds.Header, _Record, Nothing, Me))
                    For Each c In _Node.Children
                        If TypeOf c.Def Is WbPassthroughDef Then Continue For
                        out.Add(Wrap(c))
                    Next
                Case Kinds.Header
                    For Each f In HeaderFields
                        out.Add(New XElement(Kinds.HeaderField, _Record, Nothing, Me, f))
                    Next
                Case Kinds.HeaderField
                    If _field = "Record Flags" Then AddFlagChildren(out, CLng(_Record.RecordFlags), HeaderFlagsDef())
                Case Kinds.Member, Kinds.Value
                    Dim c = Content
                    If c Is Nothing Then Return out
                    If TypeOf c.Def Is WbStructDef OrElse TypeOf c.Def Is WbArrayDef OrElse
                       TypeOf c.Def Is WbRStructDef OrElse TypeOf c.Def Is WbRArrayDef Then
                        Dim hijos As IEnumerable(Of WbNode) = c.Children
                        ' Un arreglo declarado ordenado se LEE ordenado: GetElement(i) hace DoInit(True), que ordena por
                        ' SortKey[True] con CompareStr y merge sort estable (wbImplementation.pas:7549, 22358-22377, 2151-2172;
                        ' wbSortSubRecords := True, xeMainForm.pas:5151). Es solo la vista de lectura: el orden guardado (el
                        ' que se escribe) no cambia. MEDIDO en la referencia: el texto {{{componentes}}} que arma el plugin
                        ' recorriendo CVPA sigue el orden por FormID en los 225 MISC con mas de un componente, mientras los
                        ' bytes de CVPA conservan el orden de la fuente.
                        ' MEDIDO que el acceso por NOMBRE no ordena: DLC04_Armor_SpaceSuit_Helmet trae DAMA
                        ' [dtRadiationExposure 0x60A85, dtEnergy 0x60A81] y la regla FIS2.ini:183
                        ' "DAMA\Resistance\[0] contains dtRadiationExposure" MATCHEA en la referencia: "Resistance" se
                        ' resolvio por nombre, en el orden guardado.
                        If indexOrder AndAlso XSortLaw.IsSortedArray(c) AndAlso c.ChildCount > 1 Then
                            hijos = c.Children.Select(Function(h, i) New With {.N = h, .K = XSortLaw.SortKey(h, _Record, True), .P = i}).
                                               OrderBy(Function(x) x.K, StringComparer.Ordinal).ThenBy(Function(x) x.P).
                                               Select(Function(x) x.N).ToList()
                        End If
                        For Each h In hijos
                            If String.Equals(h.Name, WbSubrecordDef.BytesSinDescribir, StringComparison.Ordinal) Then Continue For
                            out.Add(Wrap(h))
                        Next
                    Else
                        Dim fd = FlagsDef
                        If fd IsNot Nothing Then AddFlagChildren(out, Convert.ToInt64(c.Value), fd)
                    End If
            End Select
            Return out
        End Function

        Private Shared ReadOnly HeaderFields As String() = {"Signature", "Record Flags", "FormID", "Version Control Info 1", "Form Version", "Version Control Info 2"}

        Private Sub AddFlagChildren(out As List(Of XElement), v As Long, fd As WbIntegerDef)
            Dim txt = XEditValue.FlagsToText(fd, v)
            For i = 0 To txt.Length - 1
                If txt(i) = "1"c AndAlso i < fd.FlagNames.Length AndAlso fd.FlagNames(i) <> "" AndAlso
                   Not String.Equals(fd.FlagNames(i), "Unused", StringComparison.OrdinalIgnoreCase) Then
                    out.Add(New XElement(Kinds.Flag, _Record, Nothing, Me, Nothing, i))
                End If
            Next
        End Sub

        Public ReadOnly Property ElementCount As Integer
            Get
                Return Children().Count
            End Get
        End Property

        Public Function ElementByIndex(i As Integer) As XElement
            Dim c = Children()
            If i < 0 OrElse i >= c.Count Then Return Nothing
            Return c(i)
        End Function

        Public Function IndexOfChild(x As XElement) As Integer
            Dim c = Children()
            For i = 0 To c.Count - 1
                If c(i).SameAs(x) Then Return i
            Next
            Return -1
        End Function

        Public Function SameAs(other As XElement) As Boolean
            If other Is Nothing Then Return False
            If _Kind <> other._Kind OrElse _Record IsNot other._Record Then Return False
            If _Node IsNot Nothing Then Return _Node Is other._Node
            Return _field = other._field AndAlso _flagIndex = other._flagIndex
        End Function

        ''' <summary><c>GetElementByName</c> (wbI:7562-7585): primero por <see cref="Name"/>, despues por
        ''' <see cref="DisplayName"/>, sin mayusculas.</summary>
        Public Function ElementByName(n As String) As XElement
            Dim c = Children(indexOrder:=False)
            For Each x In c
                If String.Equals(x.Name, n, StringComparison.OrdinalIgnoreCase) Then Return x
            Next
            For Each x In c
                If String.Equals(x.DisplayName, n, StringComparison.OrdinalIgnoreCase) Then Return x
            Next
            Return Nothing
        End Function

        ''' <summary><c>GetElementBySignature</c> (wbI:7625-7641): primer hijo con esa firma.</summary>
        Public Function ElementBySignature(sig As String) As XElement
            For Each x In Children()
                If x.Signature = sig Then Return x
            Next
            Return Nothing
        End Function

        ''' <summary>
        ''' <c>ResolveElementName</c> (wbI:8540-8611) + la variante del record que CREA (wbI:13832-13844): primer
        ''' tramo de la ruta; <c>.</c>, <c>..</c>, <c>...</c> (este o cualquier ancestro), <c>[n]</c>, nombre; si no
        ''' se encontro y el tramo mide 4, por firma. Con <paramref name="canCreate"/> y un record, una firma de 4
        ''' letras que no existe se CREA como miembro.
        ''' </summary>
        Public Function ResolveElementName(aName As String, ByRef remaining As String, Optional canCreate As Boolean = False) As XElement
            remaining = ""
            Dim i = aName.IndexOf("\"c)
            If i >= 0 Then
                remaining = aName.Substring(i + 1)
                aName = aName.Substring(0, i)
            End If
            Dim result As XElement = Nothing
            If aName = "." Then
                result = Me
            ElseIf aName = ".." Then
                result = _Container
            ElseIf aName = "..." Then
                Dim nextRemaining = ""
                Dim nextName = remaining
                Dim j = nextName.IndexOf("\"c)
                If j >= 0 Then
                    nextRemaining = nextName.Substring(j + 1)
                    nextName = nextName.Substring(0, j)
                End If
                nextName = nextName.Trim()
                If nextName = "" Then Return Me
                Dim cont = Me
                While cont IsNot Nothing
                    Dim rem2 As String = Nothing
                    Dim hit = cont.ResolveElementName(remaining, rem2, canCreate)
                    If hit IsNot Nothing Then
                        remaining = rem2
                        Return hit
                    End If
                    If String.Equals(cont.Name, nextName, StringComparison.OrdinalIgnoreCase) OrElse
                       String.Equals(cont.DisplayName, nextName, StringComparison.OrdinalIgnoreCase) Then
                        remaining = nextRemaining
                        Return cont
                    End If
                    cont = cont._Container
                End While
                Return Nothing
            ElseIf aName.Length > 0 AndAlso aName(0) = "["c AndAlso aName(aName.Length - 1) = "]"c Then
                Dim idx As Integer
                If Not Integer.TryParse(aName.Substring(1, aName.Length - 2), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, idx) Then idx = 0
                result = ElementByIndex(idx)
            Else
                result = ElementByName(aName)
            End If
            If result Is Nothing AndAlso aName.Length = 4 Then result = ElementBySignature(aName)
            If result Is Nothing AndAlso canCreate AndAlso _Kind = Kinds.Record AndAlso aName.Length = 4 Then
                Dim created = CreateRecordMemberFor(aName)
                If created Then result = ElementBySignature(aName)
            End If
            Return result
        End Function

        ''' <summary><c>ElementByPath</c> (wbI:7587-7607).</summary>
        Public Function ElementByPath(path As String) As XElement
            Dim rest As String = Nothing
            Dim e = ResolveElementName(path, rest)
            If e Is Nothing Then Return Nothing
            If rest = "" Then Return e
            Return e.ElementByPath(rest)
        End Function

        ''' <summary><c>ElementExists</c> (wbI:7699-7719). Una bandera "existe" solo si esta encendida.</summary>
        Public Function ElementExists(path As String) As Boolean
            Dim rest As String = Nothing
            Dim e = ResolveElementName(path, rest)
            If e Is Nothing Then Return False
            If rest = "" Then Return True
            Return e.ElementExists(rest)
        End Function

        ''' <summary><c>GetElementEditValues</c> (wbI:7674-7697): si el ultimo tramo no resuelve y es el nombre de una
        ''' bandera del elemento, su caracter (<c>'0'</c> si esta apagada; <c>GetMemberEditValue</c>, wbI:7840-7859).</summary>
        Public Function GetElementEditValues(path As String) As String
            Dim rest As String = Nothing
            Dim e = ResolveElementName(path, rest)
            If e Is Nothing Then
                If rest = "" Then Return GetMemberEditValue(path)
                Return ""
            End If
            If rest = "" Then Return e.EditValue
            Return e.GetElementEditValues(rest)
        End Function

        Private Function GetMemberEditValue(aName As String) As String
            Dim fd = FlagsDef
            If fd Is Nothing Then Return ""
            Dim idx = XEditValue.FindFlag(fd, aName)
            If idx < 0 Then Return ""
            Dim s = EditValue
            If s.Length >= idx + 1 Then Return s(idx).ToString()
            Return "0"
        End Function

        ''' <summary><c>SetElementEditValues</c> (wbI:8649-8670, 8708-8735): resuelve CREANDO (solo tramos de 4 letras
        ''' en el nivel del record); si no resuelve, bandera por nombre o <c>Add(nombre)</c>.</summary>
        Public Sub SetElementEditValues(path As String, value As String)
            Dim rest As String = Nothing
            Dim e = ResolveElementName(path, rest, True)
            If e Is Nothing Then
                If rest = "" Then SetMemberEditValue(path, value)
                Return
            End If
            If rest = "" Then
                e.EditValue = value
            Else
                e.SetElementEditValues(rest, value)
            End If
        End Sub

        Private Sub SetMemberEditValue(aName As String, value As String)
            Dim fd = FlagsDef
            If fd IsNot Nothing Then
                Dim idx = XEditValue.FindFlag(fd, aName)
                If idx >= 0 Then
                    Dim s = EditValue.PadRight(64, "0"c).ToCharArray()
                    If idx < s.Length Then
                        s(idx) = If(value = "1", "1"c, "0"c)
                        EditValue = New String(s).TrimEnd("0"c)
                    End If
                    Return
                End If
            End If
            Dim added = Add(aName, True)
            If added IsNot Nothing Then added.EditValue = value
        End Sub

        ' ============================================================================================ valor

        ''' <summary>EditValue (ver <see cref="XEditValue"/>). Record = FormID en 8 hex (wbI:11420-11426).</summary>
        Public Property EditValue As String
            Get
                Select Case _Kind
                    Case Kinds.Record
                        Return _Record.FormID.ToString("X8", CultureInfo.InvariantCulture)
                    Case Kinds.Header
                        Return ""
                    Case Kinds.HeaderField
                        Return HeaderFieldValue()
                    Case Kinds.Flag
                        Return "1"
                    Case Else
                        Dim c = Content
                        If c Is Nothing Then Return ""
                        If c.ChildCount > 0 OrElse TypeOf c.Def Is WbMemberDef Then Return ""
                        Return XEditValue.GetEditValue(c, _Record)
                End Select
            End Get
            Set(value As String)
                Select Case _Kind
                    Case Kinds.HeaderField
                        SetHeaderField(value)
                    Case Kinds.Member, Kinds.Value
                        Dim c = Content
                        If c Is Nothing OrElse c.ChildCount > 0 OrElse TypeOf c.Def Is WbMemberDef Then
                            Throw New InvalidOperationException($"'{Name}' can not be edited")
                        End If
                        XEditValue.SetEditValue(c, _Record, value)
                    Case Kinds.Flag
                        If value <> "1" Then _Container.SetMemberEditValue(Name, "0")
                    Case Else
                        Throw New InvalidOperationException($"'{Name}' can not be edited")
                End Select
            End Set
        End Property

        Private Function HeaderFieldValue() As String
            Select Case _field
                Case "Signature" : Return _Record.Signature
                Case "Record Flags" : Return XEditValue.FlagsToText(HeaderFlagsDef(), CLng(_Record.RecordFlags))
                Case "FormID" : Return XEditValue.RecordName(_Record)
                Case "Form Version" : Return _Record.FormVersion.ToString(CultureInfo.InvariantCulture)
                Case Else
                    If _Record.IsPatch OrElse _Record.Source Is Nothing Then Return "0"
                    Return If(_field = "Version Control Info 1", _Record.Source.Header.VCS1, CUInt(_Record.Source.Header.VCS2)).ToString(CultureInfo.InvariantCulture)
            End Select
        End Function

        Private Sub SetHeaderField(value As String)
            If _field <> "Record Flags" Then Throw New InvalidOperationException($"'{_field}' can not be edited")
            Dim bits = XEditValue.TextToInteger(HeaderFlagsDef(), value, Nothing)
            _Record.Context.RecordFlags = CUInt(bits And &HFFFFFFFFL)
        End Sub

        ''' <summary>Valor nativo de una hoja (entero, FormID global, float, texto).</summary>
        Public ReadOnly Property NativeValue As Object
            Get
                If _Kind = Kinds.HeaderField AndAlso _field = "Record Flags" Then Return CLng(_Record.RecordFlags)
                Dim c = Content
                If c Is Nothing Then Return Nothing
                Return c.Value
            End Get
        End Property

        ''' <summary><c>LinksTo</c>: el record al que apunta una referencia, visto desde el archivo de este record.</summary>
        Public Function LinksTo() As XEditRecord
            Dim c = Content
            If c Is Nothing OrElse Not TypeOf c.Def Is WbFormIdDef Then Return Nothing
            Dim fid = CUInt(Convert.ToInt64(c.Value) And &HFFFFFFFFL)
            If fid = 0UI OrElse fid = &HFFFFFFFFUI Then Return Nothing
            Return _Record.Session.RecordVisibleFrom(fid, _Record.FileName)
        End Function

        Public ReadOnly Property IsFormId As Boolean
            Get
                Dim c = Content
                Return c IsNot Nothing AndAlso TypeOf c.Def Is WbFormIdDef
            End Get
        End Property

        ' ============================================================================================ edicion

        ''' <summary>
        ''' <c>Add(nombre)</c>: en un record, el miembro de PRIMER nivel cuyo nombre o firma por defecto coincide
        ''' (sin mayusculas); si ya existe se devuelve, si no se crea en su posicion declarada (wbI:9042-9151). En un
        ''' grupo de subrecords, por firma (wbI:21191-21222). En cualquier otro contenedor, Nothing.
        ''' </summary>
        Public Function Add(aName As String, silent As Boolean) As XElement
            Select Case _Kind
                Case Kinds.Record
                    Dim def = WbSchema.Get(_Record.Context.Game, _Record.Signature)
                    For i = 0 To def.Members.Length - 1
                        Dim m = def.Members(i)
                        If String.Equals(MemberName(m), aName, StringComparison.OrdinalIgnoreCase) OrElse
                           String.Equals(DefaultSignature(m), aName, StringComparison.OrdinalIgnoreCase) Then
                            Return EnsureMember(_Node, def.Members, i)
                        End If
                    Next
                    Return Nothing
                Case Kinds.Member
                    Dim rs = TryCast(_Node.Def, WbRStructDef)
                    If rs Is Nothing Then Return Nothing
                    For i = 0 To rs.Members.Length - 1
                        Dim sigs As New HashSet(Of String)(StringComparer.Ordinal)
                        rs.Members(i).CollectSignatures(sigs)
                        If sigs.Contains(aName) Then Return EnsureMember(_Node, rs.Members, i)
                    Next
                    Return Nothing
                Case Else
                    Return Nothing
            End Select
        End Function

        Private Shared Function MemberName(m As WbMemberDef) As String
            Dim s = TryCast(m, WbSubrecordDef)
            If s IsNot Nothing Then Return If(s.Name, "")   ' vacio = la firma (wbInterface.pas:10764-10765)
            Return If(m.Name, "")
        End Function

        ''' <summary>Firma por defecto de un miembro: la del subrecord, o la del primer subrecord de un grupo.</summary>
        Private Shared Function DefaultSignature(m As WbMemberDef) As String
            Dim s = TryCast(m, WbSubrecordDef)
            If s IsNot Nothing Then Return s.Signature
            Dim rs = TryCast(m, WbRStructDef)
            If rs IsNot Nothing AndAlso rs.Members.Length > 0 Then Return DefaultSignature(rs.Members(0))
            Dim ra = TryCast(m, WbRArrayDef)
            If ra IsNot Nothing Then Return DefaultSignature(ra.Element)
            Dim ru = TryCast(m, WbRUnionDef)
            If ru IsNot Nothing AndAlso ru.Members.Length > 0 Then Return DefaultSignature(ru.Members(0))
            Return ""
        End Function

        ''' <summary>El hijo de <paramref name="container"/> que corresponde al miembro <paramref name="index"/>; si no
        ''' existe se CREA (con la ley de creacion de xEdit, <see cref="XCreate"/>) y se inserta en su posicion.</summary>
        Private Function EnsureMember(container As WbNode, members As WbMemberDef(), index As Integer) As XElement
            For Each c In container.Children
                If MemberIndexOf(members, c) = index Then Return WrapAt(container, c)
            Next
            Dim created = XCreate.CreateMember(_Record, members(index))
            Dim insertAt = container.ChildCount
            For p = 0 To container.ChildCount - 1
                Dim idx = MemberIndexOf(members, container.Children(p))
                If idx > index Then
                    insertAt = p
                    Exit For
                End If
            Next
            container.InsertarHijo(insertAt, created)
            Return WrapAt(container, created)
        End Function

        Private Function WrapAt(container As WbNode, n As WbNode) As XElement
            If container Is _Node Then Return Wrap(n)
            Return New XElement(Kinds.Member, _Record, n, Me)
        End Function

        Private Shared Function MemberIndexOf(members As WbMemberDef(), child As WbNode) As Integer
            For i = 0 To members.Length - 1
                If members(i) Is child.Def Then Return i
                Dim u = TryCast(members(i), WbRUnionDef)
                If u IsNot Nothing Then
                    For Each um In u.Members
                        If um Is child.Def Then Return i
                    Next
                End If
            Next
            Return -1
        End Function

        Private Function CreateRecordMemberFor(sig As String) As Boolean
            Dim def = WbSchema.Get(_Record.Context.Game, _Record.Signature)
            For i = 0 To def.Members.Length - 1
                Dim sigs As New HashSet(Of String)(StringComparer.Ordinal)
                def.Members(i).CollectSignatures(sigs)
                If sigs.Contains(sig) Then
                    EnsureMember(_Node, def.Members, i)
                    Return True
                End If
            Next
            Return False
        End Function

        ''' <summary>
        ''' <c>ElementAssign(indice, fuente, soloClave)</c> (<c>Assign</c> de xEdit):
        ''' <list type="bullet">
        ''' <item>grupo de subrecords: el miembro <paramref name="index"/> (se crea si falta); con fuente, se le copia;</item>
        ''' <item>arreglo de subrecords (wbI:20690-20775): si esta "recien creado vacio" Y hay fuente, reusa su unico
        ''' elemento; si no, agrega uno al final; con fuente, se le copia; si el arreglo se ordena, se reordena;</item>
        ''' <item>subrecord con arreglo de valores (wbI:15344-15398): si esta "recien creado vacio" reusa su unico
        ''' elemento (con o sin fuente); si no, agrega uno al final;</item>
        ''' <item>arreglo de valores anidado: agrega siempre uno al final.</item>
        ''' </list>
        ''' </summary>
        Public Function ElementAssign(index As Integer, source As XElement, onlySK As Boolean) As XElement
            Dim s = _Record.Session
            Select Case _Kind
                Case Kinds.Member
                    Dim rs = TryCast(_Node.Def, WbRStructDef)
                    If rs IsNot Nothing Then
                        If index < 0 OrElse index >= rs.Members.Length Then Return Nothing
                        Dim m = EnsureMember(_Node, rs.Members, index)
                        If source IsNot Nothing Then XCopy.AssignElement(m, source, toRecord:=True)
                        Return m
                    End If
                    Dim ra = TryCast(_Node.Def, WbRArrayDef)
                    If ra IsNot Nothing Then
                        Dim elem As WbNode
                        If s.IsCreatedEmpty(_Node) AndAlso source IsNot Nothing AndAlso _Node.ChildCount = 1 Then
                            elem = _Node.Children(0)
                            s.ClearCreatedEmpty(_Node)
                        Else
                            elem = XCreate.CreateMember(_Record, ra.Element)
                            _Node.AddChild(elem)
                        End If
                        Dim x = Wrap(elem)
                        If source IsNot Nothing Then
                            XCopy.AssignElement(x, source, toRecord:=True)
                            s.ClearCreatedEmpty(_Node)
                        End If
                        If XSortLaw.RArrayIsSorted(ra) Then XSortLaw.SortRArray(_Node, _Record)
                        Return x
                    End If
                    If IsSubrecord Then
                        Dim arr = ValueOf(_Node)
                        If arr IsNot Nothing AndAlso TypeOf arr.Def Is WbArrayDef Then
                            Dim ad = DirectCast(arr.Def, WbArrayDef)
                            Dim elem As WbNode
                            If s.IsCreatedEmpty(arr) AndAlso arr.ChildCount = 1 Then
                                elem = arr.Children(0)
                                s.ClearCreatedEmpty(arr)
                            Else
                                elem = ad.Element.CreateDefault(_Record.Context)
                                arr.AddChild(elem)
                            End If
                            Dim x = New XElement(Kinds.Value, _Record, elem, Me)
                            If source IsNot Nothing Then XCopy.AssignElement(x, source, toRecord:=True)
                            Return x
                        End If
                    End If
                    Return Nothing
                Case Kinds.Value
                    Dim c = Content
                    If c IsNot Nothing AndAlso TypeOf c.Def Is WbArrayDef Then
                        Dim elem = DirectCast(c.Def, WbArrayDef).Element.CreateDefault(_Record.Context)
                        c.AddChild(elem)
                        Dim x = New XElement(Kinds.Value, _Record, elem, Me)
                        If source IsNot Nothing Then XCopy.AssignElement(x, source, toRecord:=True)
                        Return x
                    End If
                    Return Nothing
                Case Kinds.Record
                    Dim def = WbSchema.Get(_Record.Context.Game, _Record.Signature)
                    If index < 0 OrElse index >= def.Members.Length Then Return Nothing
                    Dim m = EnsureMember(_Node, def.Members, index)
                    If source IsNot Nothing Then XCopy.AssignElement(m, source, toRecord:=True)
                    Return m
                Case Else
                    Return Nothing
            End Select
        End Function

        ''' <summary>Quita el elemento de su contenedor.</summary>
        Public Sub Remove()
            Select Case _Kind
                Case Kinds.Member, Kinds.Value
                    Dim p = _Node.Parent
                    If p Is Nothing Then Return
                    p.QuitarHijo(_Node)
                    _Node.Parent = Nothing
                    ' Un subrecord que se queda sin nada adentro de un grupo sigue siendo un grupo vacio: xEdit
                    ' lo conserva. Un arreglo de valores sin elementos, tambien.
                Case Kinds.Flag
                    EditValue = "0"
                Case Else
                    Throw New InvalidOperationException($"'{Name}' can not be removed")
            End Select
        End Sub

        ''' <summary>El nodo del motor que es el "hermano" de este en su contenedor (el mismo nodo salvo para los
        ''' elementos de valor dentro de un subrecord, donde el contenedor real es el nodo de valor).</summary>
        Private ReadOnly Property SiblingList As WbNode
            Get
                If _Node Is Nothing Then Return Nothing
                Return _Node.Parent
            End Get
        End Property

        Public Function CanMoveUp() As Boolean
            Dim p = SiblingList
            If p Is Nothing Then Return False
            Return p.IndiceDeHijo(_Node) > 0
        End Function

        Public Function CanMoveDown() As Boolean
            Dim p = SiblingList
            If p Is Nothing Then Return False
            Dim i = p.IndiceDeHijo(_Node)
            Return i >= 0 AndAlso i < p.ChildCount - 1
        End Function

        Public Sub MoveUp()
            Dim p = SiblingList
            If p Is Nothing Then Return
            Dim i = p.IndiceDeHijo(_Node)
            If i <= 0 Then Return
            p.QuitarHijoEn(i)
            p.InsertarHijo(i - 1, _Node)
        End Sub

        Public Sub MoveDown()
            Dim p = SiblingList
            If p Is Nothing Then Return
            Dim i = p.IndiceDeHijo(_Node)
            If i < 0 OrElse i >= p.ChildCount - 1 Then Return
            p.QuitarHijoEn(i)
            p.InsertarHijo(i + 1, _Node)
        End Sub

        Public Overrides Function ToString() As String
            Return $"{_Record.Signature}:{_Record.FormID:X8}\{Name}"
        End Function

    End Class

End Namespace
