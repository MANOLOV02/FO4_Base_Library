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

Imports FO4_Base_Library.Canon

Namespace XEdit

    ''' <summary>
    ''' Las dos copias de xEdit que usa el sorter:
    ''' <list type="bullet">
    ''' <item><b>copia como override</b> a otro archivo (<c>wbCopyElementToFile(e, archivo, False, True)</c>,
    ''' wbI:175-201, <c>CopyMainRecord</c> wbI:17000-17108, <c>AssignInternal</c> wbI:9284-9484);</item>
    ''' <item><b>copia dentro de un record</b> (<c>wbCopyElementToRecord</c>, wbI:203-227): los parciales y el
    ''' <c>resetToMaster</c> de INNR.</item>
    ''' </list>
    ''' <para>xEdit no copia bytes: reasigna valor por valor (<c>SetEditValue(fuente.EditValue)</c> para enteros y
    ''' textos, bytes crudos para floats, wbIf:16733-16759 y 10166-10174). Lo que eso cambia, y aca se aplica:</para>
    ''' <list type="bullet">
    ''' <item>un texto localizado de un archivo con tablas pasa a texto inline (el parche no esta localizado);</item>
    ''' <item>un texto se re-codifica y queda con UN terminador (wbIf:16412-16447, 16596-16686);</item>
    ''' <item>un parametro de condicion de tipo <c>String</c> queda en 0: su ToInt escribe el texto en
    ''' <c>CIS1</c>/<c>CIS2</c> (creandolo si falta) y devuelve 0 (<c>wbConditionStringToInt</c>, wbDefinitionsCommon.pas
    ''' :2924-2940; funciones 447, 629, 659, 660, 675 de FO4);</item>
    ''' <item>la cabecera: banderas de la fuente menos 0x4000 (partial form), su form version, control de versiones
    ''' en 0 (wbI:9367-9377);</item>
    ''' <item>el orden: ver <see cref="XSortLaw"/>.</item>
    ''' </list>
    ''' </summary>
    Public Module XCopy

        ''' <summary>Copia un record de disco como override al parche.</summary>
        Public Function CopyAsOverride(session As XEditSession, src As XEditRecord, Optional targetFile As String = Nothing) As XEditRecord
            If src.IsPatch Then Throw New InvalidOperationException("The source of an override must be a record of a loaded file.")
            If src.IsDeleted Then Throw New XEditCopyException($"{src}: deleted records are not copied (B29).")
            CheckFullyDeclared(src)
            Dim root = src.Root.Clonar()
            Dim ctx = src.Context.Clonar()
            ctx.Localized = False
            ctx.DestinoLocalizado = False
            ctx.RecordFlags = src.RecordFlags And Not &H4000UI
            ctx.FormID = src.FormID
            Dim patch = New XEditRecord(session, src.Signature, src.FormID, root, ctx, If(targetFile, session.OutputFileName), session.CutIndex)
            Normalize(root, src, patch)
            ' Copia a ARCHIVO: se ordenan los wbRArrayS; los wbArrayS conservan el orden de la fuente.
            SortTree(root, patch, rArraysOnly:=True)
            session.AddPatchRecord(patch)
            Return patch
        End Function

        ''' <summary>
        ''' <c>wbCopyElementToRecord</c>: pone una copia de <paramref name="source"/> (un miembro de primer nivel de
        ''' otra version) en el record <paramref name="target"/>. Si el record ya tiene ese miembro se REEMPLAZA
        ''' entero (<c>AddIfMissingInternal</c> + <c>Assign(wbAssignThis)</c>, wbI:9153-9213); si no, se inserta en su
        ''' posicion declarada. Con el orden ACTIVO: se ordenan todos los arreglos declarados ordenados.
        ''' </summary>
        Public Function CopyElementToRecord(source As XElement, target As XEditRecord) As XElement
            Dim srcNode = source.Node
            If srcNode Is Nothing OrElse Not TypeOf srcNode.Def Is WbMemberDef Then
                Throw New InvalidOperationException("Only top-level members can be copied into a record.")
            End If
            Dim clone = srcNode.Clonar()
            If Not source.Record.IsPatch Then Normalize(clone, source.Record, target)
            Dim def = WbSchema.Get(target.Context.Game, target.Signature)
            Dim idx = MemberIndexOf(def.Members, srcNode)
            If idx < 0 Then Throw New InvalidOperationException($"'{source.Name}' is not a member of {target.Signature}.")
            Dim root = target.Root
            Dim existing As WbNode = Nothing
            For Each c In root.Children
                If MemberIndexOf(def.Members, c) = idx Then
                    existing = c
                    Exit For
                End If
            Next
            If existing IsNot Nothing Then
                Dim pos = root.IndiceDeHijo(existing)
                root.QuitarHijoEn(pos)
                root.InsertarHijo(pos, clone)
            Else
                WbEdit.InsertarEnPosicionDeclarada(root, def, clone)
            End If
            SortTree(clone, target, rArraysOnly:=False)
            Return XElement.ForRecord(target).Children().First(Function(x) x.Node Is clone)
        End Function

        ''' <summary>
        ''' <c>Assign(wbAssignThis, fuente)</c> sobre un elemento ya existente: su contenido pasa a ser una copia del de
        ''' la fuente (mismo tipo de definicion). Con <paramref name="toRecord"/> el orden esta activo.
        ''' </summary>
        Public Sub AssignElement(target As XElement, source As XElement, toRecord As Boolean)
            Dim t = target.Node
            Dim s = source.Node
            If t Is Nothing OrElse s Is Nothing Then Throw New InvalidOperationException("Only field elements can be assigned.")
            If t.Def IsNot s.Def Then
                Throw New InvalidOperationException($"Assigning '{source.Name}' into '{target.Name}': different definitions.")
            End If
            Dim clone = s.Clonar()
            If Not source.Record.IsPatch Then Normalize(clone, source.Record, target.Record)
            t.Value = clone.Value
            t.RawOverride = clone.RawOverride
            t.TerminatorCount = clone.TerminatorCount
            t.ValorLocalizado = clone.ValorLocalizado
            t.UnionBranch = clone.UnionBranch
            t.LimpiarHijos()
            For Each h In clone.Children.ToList()
                t.AddChild(h)
            Next
            target.Record.Session.ClearCreatedEmpty(t)
            If toRecord Then SortTree(t, target.Record, rArraysOnly:=False)
        End Sub

        ''' <summary>Ordena los arreglos declarados ordenados de un subarbol.</summary>
        Public Sub SortTree(n As WbNode, rec As XEditRecord, rArraysOnly As Boolean)
            For Each c In n.Children.ToList()
                SortTree(c, rec, rArraysOnly)
            Next
            Dim ra = TryCast(n.Def, WbRArrayDef)
            If ra IsNot Nothing Then
                If XSortLaw.RArrayIsSorted(ra) Then XSortLaw.SortRArray(n, rec)
                Return
            End If
            If rArraysOnly Then Return
            Dim ad = TryCast(n.Def, WbArrayDef)
            If ad IsNot Nothing AndAlso ad.Sorted Then XSortLaw.SortValueArray(n, rec)
        End Sub

        ''' <summary>Aplica a un subarbol copiado de un record de DISCO lo que la reasignacion por texto de xEdit
        ''' cambia (ver la cabecera del modulo).</summary>
        Private Sub Normalize(n As WbNode, src As XEditRecord, dest As XEditRecord)
            If TypeOf n.Def Is WbLStringDef Then
                Dim texto = XEditValue.LStringToText(n, src)
                n.Value = texto
                n.RawOverride = Nothing
                n.ValorLocalizado = WbLocalizacion.Texto
                n.TerminatorCount = 1
                Return
            End If
            Dim sd = TryCast(n.Def, WbStringDef)
            If sd IsNot Nothing Then
                n.RawOverride = Nothing
                If sd.FixedLength = 0 Then n.TerminatorCount = 1
                Return
            End If
            Dim idf = TryCast(n.Def, WbIntegerDef)
            If idf IsNot Nothing AndAlso idf.EnumName = "wbConditionStringToStr" Then
                ConditionStringToInt(n, src)
                Return
            End If
            For Each c In n.Children.ToList()
                Normalize(c, src, dest)
            Next
        End Sub

        ''' <summary>
        ''' Parametro de condicion de tipo String copiado por texto: ToStr devuelve el texto de <c>CIS1</c>/<c>CIS2</c> (o
        ''' el entero en decimal si esta vacio, wbDefinitionsCommon.pas:3348-3367); ToInt lo escribe en ese subrecord
        ''' (creandolo si falta) y devuelve 0 (:2924-2940).
        ''' </summary>
        Private Sub ConditionStringToInt(n As WbNode, src As XEditRecord)
            Dim original = Convert.ToInt64(n.Value)
            ' ¿Parametro 1 o 2? Lo dice la union que contiene la rama.
            Dim union = n.Parent
            Dim which = If(union IsNot Nothing AndAlso union.Name = "Parameter #2", "CIS2", "CIS1")
            ' El CTDA cuelga del grupo 'Condition' (CTDA, CIS1, CIS2).
            Dim ctda = n.Parent
            While ctda IsNot Nothing AndAlso Not (TypeOf ctda.Def Is WbSubrecordDef AndAlso ctda.Signature = "CTDA")
                ctda = ctda.Parent
            End While
            Dim cond = ctda?.Parent
            If cond IsNot Nothing AndAlso TypeOf cond.Def Is WbRStructDef Then
                Dim cis = cond.BySignature(which)
                Dim texto As String = Nothing
                If cis IsNot Nothing AndAlso cis.ChildCount > 0 Then texto = TryCast(cis.Children(0).Value, String)
                If String.IsNullOrEmpty(texto) Then
                    texto = original.ToString(Globalization.CultureInfo.InvariantCulture)
                    If cis Is Nothing Then
                        Dim rs = DirectCast(cond.Def, WbRStructDef)
                        Dim idx = -1
                        For i = 0 To rs.Members.Length - 1
                            Dim s = TryCast(rs.Members(i), WbSubrecordDef)
                            If s IsNot Nothing AndAlso s.Signature = which Then idx = i
                        Next
                        If idx >= 0 Then
                            Dim sdef = DirectCast(rs.Members(idx), WbSubrecordDef)
                            Dim nuevo As New WbNode(sdef) With {.Signature = which}
                            Dim v = sdef.Value.CreateDefault(src.Context)
                            v.Value = texto
                            v.TerminatorCount = 1
                            nuevo.AddChild(v)
                            Dim insertAt = cond.ChildCount
                            For p = 0 To cond.ChildCount - 1
                                Dim pi = MemberIndexOf(rs.Members, cond.Children(p))
                                If pi > idx Then
                                    insertAt = p
                                    Exit For
                                End If
                            Next
                            cond.InsertarHijo(insertAt, nuevo)
                        End If
                    End If
                End If
            End If
            n.Value = WbCajas.Caja(0L)
        End Sub

        Private Function MemberIndexOf(members As WbMemberDef(), child As WbNode) As Integer
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

        ''' <summary>B28: un record con subrecords que el esquema no declara, o con bytes que no describe, no se copia:
        ''' la copia de xEdit va campo por campo y esos bytes no tienen campo.</summary>
        Private Sub CheckFullyDeclared(src As XEditRecord)
            For Each n In src.Root.Walk()
                If TypeOf n.Def Is WbPassthroughDef Then
                    Throw New XEditCopyException($"{src}: subrecord {n.Signature} is not declared by the schema (B28).")
                End If
                If String.Equals(n.Name, WbSubrecordDef.BytesSinDescribir, StringComparison.Ordinal) Then
                    Throw New XEditCopyException($"{src}: {n.Parent?.Signature} has bytes the schema does not describe (B28).")
                End If
                Dim sd = TryCast(n.Def, WbSubrecordDef)
                If sd IsNot Nothing AndAlso sd.IsPending Then
                    Throw New XEditCopyException($"{src}: subrecord {n.Signature} is declared pending (B28).")
                End If
            Next
        End Sub

    End Module

    Public NotInheritable Class XEditCopyException
        Inherits Exception
        Public Sub New(message As String)
            MyBase.New(message)
        End Sub
    End Class

End Namespace
