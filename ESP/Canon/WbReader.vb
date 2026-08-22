' ============================================================================================
' Este archivo transcribe a mano material de las declaraciones de formato de xEdit (ordinales de
' tipo, constantes de formato, y el DSL de declaracion en si), que estan bajo Mozilla Public
' License 2.0, y por lo tanto es una obra derivada de ellas.
'
' This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
' If a copy of the MPL was not distributed with this file, You can obtain one at
' https://mozilla.org/MPL/2.0/
'
' Proyecto original: https://github.com/TES5Edit/TES5Edit  (ElminsterAU y colaboradores)
' Ver THIRD-PARTY-NOTICES.md en la raiz del repositorio.
' ============================================================================================
Imports System.IO
Imports FO4_Base_Library

Namespace Canon

    ''' <summary>Lector: asocia cada subrecord de un record a la definición que le corresponde.
    '''
    ''' <para><b>El algoritmo:</b> hay DOS cursores, uno sobre los subrecords del archivo y otro
    ''' sobre los MIEMBROS de la definición, y el de miembros es MONÓTONO:</para>
    ''' <list type="number">
    ''' <item>Si la firma no aparece en ninguna parte de la definición del record ⇒ subrecord
    ''' INESPERADO: se REPORTA como no consumido y se conserva sin interpretar.</item>
    ''' <item>Si el miembro donde está parado el cursor no puede hacerse cargo del subrecord ⇒
    ''' avanza el cursor de MIEMBROS y se vuelve a probar con el MISMO subrecord. Así se saltean
    ''' los miembros opcionales ausentes sin ninguna lista escrita a mano.</item>
    ''' <item>Según el tipo del miembro se consume UN subrecord o una CORRIDA de ellos (struct o
    ''' array de subrecords).</item>
    ''' <item>Consumido el miembro, avanzan los dos cursores.</item>
    ''' </list>
    '''
    ''' <para>La consecuencia que importa: un subrecord POLISÉMICO no se puede confundir. Los dos
    ''' <c>CNAM</c> de <c>PACK</c>, los dos <c>NNAM</c> de <c>QUST</c>, los <c>PNAM</c>/<c>TNAM</c>
    ''' de <c>SCEN</c> viven en miembros distintos del árbol, y el cursor sólo puede estar en uno.
    ''' No hace falta ninguna heurística de corte (mirar si quedó un objetivo abierto, una bandera
    ''' en <c>PKCU</c>, el largo del <c>ANAM</c>): la ESTRUCTURA ya decide. Un
    ''' <c>Select Case sr.Signature</c> plano no puede hacerlo por construcción.</para>
    ''' </summary>
    Public NotInheritable Class WbReader

        Private Sub New()
        End Sub

        ''' <summary>Parsea un record completo. Devuelve la raíz del árbol; los hallazgos quedan en
        ''' <c>ctx.Findings</c>.</summary>
        Public Shared Function Parse(def As WbRecordDef, rec As PluginRecord, ctx As WbContext) As WbNode
            ctx.RecordSignature = def.Signature
            ctx.FormVersion = rec.Header.Version
            ctx.Localized = rec.SourcePluginIsLocalized
            ctx.TranslatableEncoding = rec.SourcePluginTranslatableEncoding
            ctx.RecordFlags = rec.Header.Flags
            ctx.EditorId = rec.EditorID

            Dim root As New WbNode(New WbRootDef(def.Signature)) With {.ParsedFormVersion = rec.Header.Version}
            Dim subs = rec.Subrecords
            Dim defPos = 0
            Dim recPos = 0

            While recPos < subs.Count
                Dim sig = subs(recPos).Signature
                Dim dataLen = If(subs(recPos).Data Is Nothing, 0, subs(recPos).Data.Length)

                ' Firma que el record no declara en ninguno de sus miembros.
                If Not def.AllSignatures.Contains(sig) Then
                    ctx.Report(WbFindingKind.Unconsumed, $"{def.Signature}\{sig}",
                               $"subrecord no declarado en el esquema ({dataLen} bytes)")
                    root.AddChild(New WbPassthroughDef(sig).Parse(ctx, subs, recPos, root))
                    Continue While
                End If

                If def.AllowUnordered Then
                    Dim idx = IndexOfMemberFor(def, ctx, sig, dataLen)
                    If idx < 0 Then
                        ctx.Report(WbFindingKind.Unconsumed, $"{def.Signature}\{sig}",
                                   "ningún miembro puede hacerse cargo (record AllowUnordered)")
                        root.AddChild(New WbPassthroughDef(sig).Parse(ctx, subs, recPos, root))
                        Continue While
                    End If
                    defPos = idx
                End If

                ' Cursor de miembros agotado: el subrecord llegó fuera de orden. Se reporta y se
                ' conserva sin interpretar.
                If defPos >= def.Members.Length Then
                    ctx.Report(WbFindingKind.Unconsumed, $"{def.Signature}\{sig}",
                               "subrecord fuera de orden: el cursor de miembros ya pasó su posición")
                    root.AddChild(New WbPassthroughDef(sig).Parse(ctx, subs, recPos, root))
                    Continue While
                End If

                ' El miembro no puede con este subrecord ⇒ avanza el cursor de MIEMBROS y se
                ' reintenta con el MISMO subrecord.
                If Not def.Members(defPos).CanHandle(ctx, sig, dataLen) Then
                    defPos += 1
                    Continue While
                End If

                Dim before = recPos
                Dim child As WbNode
                Try
                    child = def.Members(defPos).Parse(ctx, subs, recPos, root)
                Catch ex As WbLayoutException
                    ctx.Report(WbFindingKind.LayoutError, ex.Path, ex.Message)
                    recPos += 1
                    defPos += 1
                    Continue While
                Catch ex As Exception
                    ' Un plugin con datos corruptos (un prefijo de conteo ≥ 0x80000000 desborda la
                    ' conversión a entero con signo de un array o de una cadena con prefijo de
                    ' longitud) no puede TUMBAR al consumidor: tiene que salir como HALLAZGO.
                    ctx.Report(WbFindingKind.LayoutError, $"{def.Signature}\{sig}",
                               $"{ex.GetType().Name} al parsear: {ex.Message}")
                    recPos += 1
                    defPos += 1
                    Continue While
                End Try
                root.AddChild(child)
                If recPos = before Then recPos += 1   ' guarda anti-bucle
                If Not def.AllowUnordered Then defPos += 1
            End While

            Return root
        End Function

        Private Shared Function IndexOfMemberFor(def As WbRecordDef, ctx As WbContext, sig As String, dataLen As Integer) As Integer
            For i = 0 To def.Members.Length - 1
                If def.Members(i).CanHandle(ctx, sig, dataLen) Then Return i
            Next
            Return -1
        End Function

        ''' <summary>Crea un record NUEVO desde cero: sólo los miembros marcados como requeridos.
        ''' Este es el ÚNICO lugar donde esa marca se consulta.
        ''' <para>Esa es la diferencia entera entre "record nuevo" y "override": un override sale de
        ''' <see cref="Parse"/> y por lo tanto reproduce la presencia de la FUENTE. No hay ningún
        ''' <c>required:=True</c> que pueda inyectarle un subrecord que no traía.</para></summary>
        Public Shared Function CreateNew(def As WbRecordDef, ctx As WbContext) As WbNode
            ctx.RecordSignature = def.Signature
            Dim root As New WbNode(New WbRootDef(def.Signature))
            ' ⛔ SOLO los miembros marcados Required, SIN regla del miembro 0. Es literalmente lo que
            ' hace xEdit al crear un main record:
            '     for i := 0 to Pred(mrDef.MemberCount) do if mrDef.Members[i].Required then Assign(i, nil, False)
            ' El predicado con `CurrentDefPos = 0` que trae `AddRequiredElements` es de los STRUCTS
            ' (TwbSubRecordStruct), no de los main records; copiarlo aca hacia emitir EDID en los 120
            ' records de TES5 que lo declaran SIN AsRequired, o sea divergir de xEdit en todos ellos.
            ' El sintoma que me hizo agregarlo -un ARMO nuevo sin EDID- es la conducta CORRECTA: xEdit
            ' hace lo mismo, y el EditorID lo pone la app al crear (ArmoEditor_Form.vb:222 y :342).
            For Each m In def.Members
                If m.Required Then root.AddChild(m.CreateRequired(ctx))
            Next
            Return root
        End Function
    End Class

    ''' <summary>Subrecord que el esquema NO supo ubicar. Se conserva TAL CUAL y se re-emite en
    ''' su posición original.
    ''' <para>Regla del escritor: <b>nunca perder un subrecord</b>. Un visor puede darse el lujo de
    ''' descartar lo que no encaja, porque sólo MUESTRA el archivo; acá se vuelve a ESCRIBIR, y
    ''' descartarlo significa entregar un ESP al que le faltan datos — corrupción silenciosa.</para>
    ''' <para>NO tapa el hueco: cada uno sigue contándose como <c>Unconsumed</c> con su firma, así
    ''' que el esquema incompleto se ve igual. Lo que cambia es que el round-trip deja de castigar
    ''' al usuario por una carencia del esquema.</para></summary>
    Public NotInheritable Class WbPassthroughDef
        Inherits WbMemberDef

        Public ReadOnly Property Signature As String

        Public Sub New(sig As String)
            _Signature = sig
            Name = sig
        End Sub

        Public Overrides Function CanHandle(ctx As WbContext, sig As String, dataLen As Integer) As Boolean
            Return String.Equals(sig, Signature, StringComparison.Ordinal)
        End Function

        Public Overrides Sub CollectSignatures(into As HashSet(Of String))
            into.Add(Signature)
        End Sub

        Public Overrides Function Parse(ctx As WbContext, subs As IList(Of SubrecordData), ByRef pos As Integer, parent As WbNode) As WbNode
            Dim sr = subs(pos)
            Dim n As New WbNode(Me) With {.Signature = sr.Signature}
            n.Parent = parent
            n.RawOverride = If(sr.Data, Array.Empty(Of Byte)())
            n.SourceLength = n.RawOverride.Length
            pos += 1
            Return n
        End Function

        Public Overrides Sub Emit(node As WbNode, bw As BinaryWriter, ctx As WbContext)
            ' Un subrecord que la estructura no supo ubicar se guarda tal cual, y sus bytes pueden
            ' contener referencias a otros archivos. Emitirlo sin haberlo entendido significa que
            ' esas referencias no pasan por el reindexado de masters: el archivo sale apuntando al
            ' mod equivocado, sin aviso. Por eso escribir uno es un error, no una tolerancia.
            If Not ctx.AllowPendingSubrecords Then
                Throw New InvalidOperationException(
                    $"{ctx.RecordSignature}\{node.Signature}: el subrecord llegó en un orden que la " &
                    "estructura no puede ubicar y quedó sin interpretar. Escribirlo copiaría sus bytes " &
                    "y cualquier referencia que contengan no pasaría por el reindexado de masters.")
            End If
            WbWriter.EmitSubrecord(bw, node.Signature, node.RawOverride)
        End Sub

        Public Overrides Function CreateRequired(ctx As WbContext) As WbNode
            Return New WbNode(Me) With {.Signature = Signature}
        End Function
    End Class

    ''' <summary>Def de la raíz del árbol de un record. Sólo lleva el nombre (la signature).</summary>
    Public NotInheritable Class WbRootDef
        Inherits WbDef

        Public Sub New(sig As String)
            Name = sig
        End Sub
    End Class

End Namespace
