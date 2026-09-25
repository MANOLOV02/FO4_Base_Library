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
    ''' La ley con la que xEdit CREA un elemento nuevo (no leido de un archivo). Decide los bytes de todo lo que el
    ''' sorter agrega: un grupo de keywords nuevo, un nombre de INNR, un object template entero.
    ''' <list type="bullet">
    ''' <item>subrecord: su valor por defecto (<c>TwbSubRecord.Create</c>, wbI:15648-15670);</item>
    ''' <item>grupo de subrecords: el miembro 0 salvo que el grupo sea sin orden o lleve
    ''' <c>dfStructFirstNotRequired</c>, mas los obligatorios (<c>AddRequiredElements</c>, wbI:21253-21291);</item>
    ''' <item>arreglo de subrecords: UN elemento, y el arreglo queda "creado vacio" (<c>TwbSubRecordArray.Create</c>,
    ''' wbI: constructor, MinCount = 1 salvo <c>dfArrayCanBeEmpty</c>);</item>
    ''' <item>struct de valores: TODOS sus miembros (StructDoInit);</item>
    ''' <item>arreglo de valores (<c>ArrayDoInit</c>, wbI:21900-22020): cantidad fija si la declara; con prefijo, 0;
    ''' con contador por callback, lo que diga el contador (0); de tamaño variable, UN elemento y el arreglo queda
    ''' "creado vacio";</item>
    ''' <item>los valores por defecto declarados (<c>SetDefaultNativeValue</c>).</item>
    ''' </list>
    ''' </summary>
    Public Module XCreate

        ''' <summary>Crea el nodo de un miembro de record o de grupo.</summary>
        Public Function CreateMember(rec As XEditRecord, m As WbMemberDef) As WbNode
            Dim ctx = rec.Context
            Dim s = TryCast(m, WbSubrecordDef)
            If s IsNot Nothing Then
                Dim n As New WbNode(s) With {.Signature = s.Signature}
                Dim v = CreateValue(rec, s.Value)
                n.AddChild(v)
                n.SourceLength = v.SourceLength
                Return n
            End If
            Dim rs = TryCast(m, WbRStructDef)
            If rs IsNot Nothing Then
                Dim n As New WbNode(rs)
                For i = 0 To rs.Members.Length - 1
                    Dim mm = rs.Members(i)
                    If (i = 0 AndAlso Not (rs.AllowUnordered OrElse rs.FirstNotRequired)) OrElse mm.Required Then
                        Dim u = TryCast(mm, WbRUnionDef)
                        If u IsNot Nothing Then mm = u.Members(0)
                        n.AddChild(CreateMember(rec, mm))
                    End If
                Next
                Return n
            End If
            Dim ra = TryCast(m, WbRArrayDef)
            If ra IsNot Nothing Then
                Dim n As New WbNode(ra)
                n.AddChild(CreateMember(rec, ra.Element))
                rec.Session.MarkCreatedEmpty(n)
                Return n
            End If
            Dim ru = TryCast(m, WbRUnionDef)
            If ru IsNot Nothing Then Return CreateMember(rec, ru.Members(0))
            Throw New NotSupportedException($"Creating member '{m?.Name}' ({m?.GetType().Name}) is not transcribed.")
        End Function

        ''' <summary>Crea el nodo de un valor.</summary>
        Public Function CreateValue(rec As XEditRecord, v As WbValueDef) As WbNode
            Dim ctx = rec.Context
            Dim st = TryCast(v, WbStructDef)
            If st IsNot Nothing Then
                Dim n As New WbNode(st)
                For Each m In st.Members
                    n.AddChild(CreateValue(rec, m))
                Next
                Return n
            End If
            Dim ad = TryCast(v, WbArrayDef)
            If ad IsNot Nothing Then
                Dim n As New WbNode(ad)
                Dim cuantos As Integer
                If ad.Count > 0 Then
                    cuantos = ad.Count
                ElseIf ad.Count < 0 Then
                    cuantos = 0
                ElseIf ad.CountPathIsCallback OrElse ad.Counter IsNot Nothing Then
                    cuantos = 0
                Else
                    cuantos = 1
                End If
                For i = 1 To cuantos
                    Dim c = CreateValue(rec, ad.Element)
                    If ad.ElementNames IsNot Nothing AndAlso i - 1 < ad.ElementNames.Length Then c.OverrideName = ad.ElementNames(i - 1)
                    n.AddChild(c)
                Next
                If ad.Count = 0 AndAlso Not ad.CountPathIsCallback AndAlso ad.Counter Is Nothing Then rec.Session.MarkCreatedEmpty(n)
                Return n
            End If
            Dim u = TryCast(v, WbUnionDef)
            If u IsNot Nothing Then
                Dim n As New WbNode(u)
                Dim idx = u.Decider(ctx, Nothing, 0, 0, Nothing)
                If idx < 0 OrElse idx >= u.Members.Length Then idx = 0
                n.UnionBranch = idx
                n.AddChild(CreateValue(rec, u.Members(idx)))
                Return n
            End If
            Dim nd = v.CreateDefault(ctx)
            If v.DefaultNative IsNot Nothing Then
                If TypeOf v Is WbIntegerDef Then nd.Value = WbCajas.Caja(Convert.ToInt64(v.DefaultNative))
                ' El valor NATIVO es el que se guarda: no pasa por la escala del texto.
                If TypeOf v Is WbFloatDef Then nd.Value = WbCajas.Caja(CSng(Convert.ToDouble(v.DefaultNative)))
            End If
            If TypeOf v Is WbLStringDef Then
                nd.Value = ""
                nd.ValorLocalizado = WbLocalizacion.Texto
                nd.TerminatorCount = 1
            End If
            Return nd
        End Function

    End Module

End Namespace
