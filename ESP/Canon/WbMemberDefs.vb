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

    ''' <summary>Base de la CAPA A: un miembro de un record, a nivel SUBRECORD. Los cuatro tipos
    ''' concretos son subrecord suelto, array de subrecords, struct de subrecords y unión de
    ''' miembros.</summary>
    Public MustInherit Class WbMemberDef
        Inherits WbDef

        ''' <summary>Marca de miembro requerido. SÓLO se consulta al CREAR un record nuevo; al leer
        ''' un record existente NO interviene, y por eso un OVERRIDE reproduce la presencia de la
        ''' FUENTE. La marca describe qué debe tener un record NUEVO, no autoriza a agregarle
        ''' subrecords a uno ajeno: hacerlo le mete al override subrecords que la fuente no
        ''' traía.</summary>
        Public Property Required As Boolean

        ''' <summary>El miembro CIERRA la estructura que lo contiene apenas se lee.
        ''' <para>Hace falta en las estructuras sin orden, donde el cursor de miembros no manda: sin
        ''' esta marca, los subrecords que vienen detrás llenan los huecos vacíos del grupo que acaba
        ''' de terminar en vez de abrir el siguiente, y quedan atribuidos al elemento equivocado.</para>
        ''' <para>La marca no se pone a mano: el esquema la deriva de la forma del grupo, y la lleva
        ''' el último miembro declarado cuando además es obligatorio. Es lo que pasa de verdad en los
        ''' datos de subgrafo de una raza — las palabras clave que preceden al siguiente grafo se le
        ''' acreditaban al anterior — y en las combinaciones de objeto de una armadura, un mueble, un
        ''' actor o un arma, donde se corrían el nombre visible y la marca de sólo-editor.</para></summary>
        Public Property IsTerminator As Boolean

        ''' <summary>Marca este miembro como el que cierra su estructura.</summary>
        Public Function AsTerminator() As WbMemberDef
            IsTerminator = True
            Return Me
        End Function

        ''' <summary>Es el ÚNICO discriminador del recorrido: decide si el miembro en el que está
        ''' parado el cursor se queda con este subrecord o si el cursor avanza al siguiente
        ''' miembro.</summary>
        Public MustOverride Function CanHandle(ctx As WbContext, sig As String, dataLen As Integer) As Boolean

        ''' <summary>Todas las firmas de subrecord alcanzables desde este miembro.</summary>
        Public MustOverride Sub CollectSignatures(into As HashSet(Of String))

        ''' <summary>Consume subrecords desde <paramref name="pos"/> y devuelve el nodo. Avanza
        ''' <paramref name="pos"/> exactamente lo que consumió.</summary>
        Public MustOverride Function Parse(ctx As WbContext, subs As IList(Of SubrecordData), ByRef pos As Integer, parent As WbNode) As WbNode

        Public MustOverride Sub Emit(node As WbNode, bw As BinaryWriter, ctx As WbContext)

        ''' <summary>Crea el nodo del miembro sin bytes de origen: sólo para records NUEVOS.</summary>
        Public MustOverride Function CreateRequired(ctx As WbContext) As WbNode

        Public Function AsRequired() As WbMemberDef
            Required = True
            Return Me
        End Function

    End Class

    ''' <summary>Un subrecord suelto: una firma + un árbol de valores.
    ''' <para><see cref="SizeMatch"/> agrega el discriminador POR LARGO: el miembro sólo se hace
    ''' cargo si el tamaño del subrecord coincide EXACTO con el tamaño fijo de su valor. Es el corte
    ''' correcto para los subrecords POLISÉMICOS que se separan por tamaño — el caso de los dos
    ''' <c>SNAM</c> de <c>TERM</c>, donde el Looping Sound mide 4 y el array de marker params nunca
    ''' puede medir 4.</para></summary>
    Public NotInheritable Class WbSubrecordDef
        Inherits WbMemberDef

        Public ReadOnly Property Signature As String
        Public ReadOnly Property Value As WbValueDef
        Public Property SizeMatch As Boolean
        ''' <summary>Subrecord declarado como PENDIENTE: se sabe que existe, todavía no se declaró su
        ''' estructura. Sus bytes se consumen como un bloque anónimo y el reporte lo cuenta
        ''' en el bucket PENDIENTE. Es lo contrario de copiar en silencio: existe justamente para
        ''' que no se pueda dar por verde un record que no se entendió.</summary>
        Public Property IsPending As Boolean

        Public Sub New(sig As String, value As WbValueDef)
            _Signature = sig
            _Value = value
            Name = If(String.IsNullOrEmpty(value.Name), sig, value.Name)
        End Sub

        Public Function WithSizeMatch() As WbSubrecordDef
            SizeMatch = True
            Return Me
        End Function

        Public Overrides Function CanHandle(ctx As WbContext, sig As String, dataLen As Integer) As Boolean
            ' Primer corte: la firma tiene que ser exactamente la del miembro.
            If Not String.Equals(sig, Signature, StringComparison.Ordinal) Then Return False
            ' Con el discriminador por largo, además el tamaño del dato tiene que dar exacto.
            If SizeMatch Then
                Dim ds = Value.DefaultSize(ctx)
                If ds < 0 Then Return False
                Return dataLen = ds
            End If
            Return True
        End Function

        Public Overrides Sub CollectSignatures(into As HashSet(Of String))
            into.Add(Signature)
        End Sub

        ''' <summary>Nombre del nodo que recoge los bytes de un subrecord que ningun campo declarado
        ''' describe. Existe para que esos bytes sean visibles en el arbol en vez de viajar escondidos.</summary>
        Public Const BytesSinDescribir As String = "Bytes sin describir"

        Public Overrides Function Parse(ctx As WbContext, subs As IList(Of SubrecordData), ByRef pos As Integer, parent As WbNode) As WbNode
            Dim sr = subs(pos)
            Dim data = If(sr.Data, Array.Empty(Of Byte)())
            Dim n As New WbNode(Me) With {.Signature = sr.Signature}
            n.Parent = parent
            n.DataLength = data.Length
            ' Cuántos textos crudos había ANTES de parsear el contenido de este subrecord: la
            ' diferencia dice si hay alguno ADENTRO. Ver el barrido de fidelidad al final.
            Dim crudosAntes = ctx.TextosCrudos
            Dim child = Value.Parse(ctx, data, 0, data.Length, n)
            n.AddChild(child)
            n.SourceLength = child.SourceLength
            ' Los campos declarados tienen que cubrir los bytes EXACTOS del subrecord.
            If child.SourceLength <> data.Length Then
                ctx.Report(WbFindingKind.Tessellation, n.Path,
                           $"{sr.Signature}: la declaración consume {child.SourceLength} de {data.Length} bytes")
                If child.SourceLength < data.Length Then
                    ' Los bytes que ningun campo describe entran al arbol como un nodo PROPIO, con
                    ' nombre y ruta. No hay ninguna via por la que el escritor copie bytes que no
                    ' esten en el modelo: si algo sale al archivo, se puede ver, contar y editar.
                    Dim extra(data.Length - child.SourceLength - 1) As Byte
                    Buffer.BlockCopy(data, child.SourceLength, extra, 0, extra.Length)
                    Dim resto As New WbNode(New WbByteArrayDef(BytesSinDescribir, extra.Length))
                    resto.Parent = n
                    resto.Value = extra
                    resto.SourceLength = extra.Length
                    n.AddChild(resto)
                End If
            End If
            If IsPending Then
                ctx.Report(WbFindingKind.Pending, n.Path, $"{sr.Signature}: {data.Length} bytes sin declarar")
            End If
            ' Fidelidad de texto: se cuenta cada hoja de TEXTO que no vuelve a los mismos bytes.
            ' El recorrido corre SÓLO si el parseo de este subrecord conservó algún crudo. Correrlo
            ' siempre es un barrido del subárbol entero POR SUBRECORD para un caso que es el 0,02 %
            ' (218 hojas en Fallout 4 y 448 en Skyrim, sobre 1,87 millones de nodos). El aviso que sale
            ' es el mismo, con la misma ruta y en el mismo orden.
            If ctx.TextosCrudos <> crudosAntes Then
                For Each leaf In n.Walk()
                    If leaf.RawOverride IsNot Nothing AndAlso TypeOf leaf.Def Is WbStringDef Then
                        ctx.Report(WbFindingKind.EncodingFallback, leaf.Path, "decode→encode no reproduce la fuente")
                    End If
                Next
            End If
            pos += 1
            Return n
        End Function

        Public Overrides Sub Emit(node As WbNode, bw As BinaryWriter, ctx As WbContext)
            ' FAIL LOUD: un pendiente se re-emite copiando bytes, y sus FormID son INVISIBLES para
            ' el remapper de índices de master. Salir con eso es publicar un ESP con referencias
            ' apuntando a otro mod, sin excepción ni log. Sólo un arnés de medición puede
            ' habilitarlo.
            If IsPending AndAlso Not ctx.AllowPendingSubrecords Then
                Throw New InvalidOperationException(
                    $"{ctx.RecordSignature}\{Signature}: subrecord PENDIENTE (sin declarar). " &
                    "Emitirlo copiaría sus bytes y sus FormID no pasarían por el remapper de masters. " &
                    "Declaralo antes de migrar, o poné WbContext.AllowPendingSubrecords sólo en un arnés de medición.")
            End If
            Dim body As Byte()
            Using ms As New MemoryStream()
                Using w As New BinaryWriter(ms)
                    For Each c In node.Children
                        CType(c.Def, WbValueDef).Emit(c, w, ctx)
                    Next
                End Using
                body = ms.ToArray()
            End Using
            WbWriter.EmitSubrecord(bw, node.Signature, body)
        End Sub

        Public Overrides Function CreateRequired(ctx As WbContext) As WbNode
            Dim n As New WbNode(Me) With {.Signature = Signature}
            Dim c = Value.CreateDefault(ctx)
            n.AddChild(c)
            n.SourceLength = c.SourceLength
            Return n
        End Function
    End Class

    ''' <summary>Struct de subrecords: agrupa una CORRIDA de subrecords bajo un nombre.
    ''' <para>Es la pieza que hace IMPOSIBLE por construcción el bug del subrecord POLISÉMICO. Un
    ''' <c>MODC</c> dentro del struct 'Male' y otro dentro del 'Female' son miembros de structs
    ''' distintos: el cursor sólo puede alcanzar uno de los dos según dónde esté parado. Nunca hace
    ''' falta una cola por firma ni una heurística de posición — un escritor con cola por firma
    ''' tiene que rechazar el caso de dos MODC justamente porque no puede separarlos.</para>
    ''' <para>Con <see cref="AllowAnyMember"/> el struct acepta CUALQUIERA de sus firmas como
    ''' apertura. Sin esa bandera, sólo abre con su primer miembro.</para></summary>
    Public NotInheritable Class WbRStructDef
        Inherits WbMemberDef

        Public ReadOnly Property Members As WbMemberDef()
        Public Property AllowAnyMember As Boolean
        Public Property AllowUnordered As Boolean
        Public ReadOnly Property SkipSignatures As New HashSet(Of String)(StringComparer.Ordinal)

        Private _sigCache As HashSet(Of String)

        Public Sub New(name As String, members As WbMemberDef())
            Me.Name = name
            _Members = members
        End Sub

        Public Function WithAnyMember() As WbRStructDef
            AllowAnyMember = True
            Return Me
        End Function

        ''' <summary>El struct acepta a sus miembros EN CUALQUIER ORDEN. Es MÁS que
        ''' <see cref="AllowAnyMember"/>: los dos hacen que el struct se abra con cualquiera de sus
        ''' firmas, pero además esto reinicia el cursor de miembros a 0 después de cada uno, o sea
        ''' que el struct puede dar varias vueltas.
        ''' <para>Confundir los dos cuesta records enteros: la 'Combination' del Object Template de
        ''' Fallout 4 es un struct sin orden. Sin esto, el array de Combinations corta después de la
        ''' PRIMERA y todo lo que sigue (OBTS, OBTF, STOP) queda fuera de orden.</para></summary>
        Public Function WithUnordered() As WbRStructDef
            AllowUnordered = True
            Return Me
        End Function

        ''' <summary>Firmas que el struct SALTEA sin cerrarse: aparecen en medio de la corrida y no
        ''' la interrumpen.</summary>
        Public Function WithSkip(sig As String) As WbRStructDef
            SkipSignatures.Add(sig)
            Return Me
        End Function

        ''' <summary>Las firmas que este contenedor puede aceptar, memoizadas.
        ''' <para>El grafo de definiciones se cachea POR PROCESO (WbSchemaGen*.Get) y los arboles se arman
        ''' desde varios hilos, asi que esta memoizacion la corren N hilos EN FRIO sobre la MISMA def. La
        ''' publicacion va con `Volatile.Write` y la lectura con `Volatile.Read`: sin la barrera, un lector
        ''' podria ver la referencia publicada y el HashSet a medio construir. Que dos hilos construyan el
        ''' mismo set y uno pise al otro es inofensivo — son iguales por construccion.</para></summary>
        Private ReadOnly Property AllSignatures As HashSet(Of String)
            Get
                Dim actual = Threading.Volatile.Read(_sigCache)
                If actual IsNot Nothing Then Return actual
                Dim s As New HashSet(Of String)(StringComparer.Ordinal)
                For Each m In Members
                    m.CollectSignatures(s)
                Next
                Threading.Volatile.Write(_sigCache, s)
                Return s
            End Get
        End Property

        Public Overrides Function CanHandle(ctx As WbContext, sig As String, dataLen As Integer) As Boolean
            ' Sin orden, o aceptando cualquier miembro, el struct abre con cualquiera de sus firmas.
            If AllowUnordered OrElse AllowAnyMember Then Return AllSignatures.Contains(sig)
            Return Members(0).CanHandle(ctx, sig, dataLen)
        End Function

        Public Overrides Sub CollectSignatures(into As HashSet(Of String))
            For Each s In AllSignatures
                into.Add(s)
            Next
        End Sub

        ''' <summary>Cursor interno sobre los miembros, cursor externo sobre los subrecords: el
        ''' struct TERMINA en cuanto aparece una firma que no le pertenece.</summary>
        Public Overrides Function Parse(ctx As WbContext, subs As IList(Of SubrecordData), ByRef pos As Integer, parent As WbNode) As WbNode
            Dim n As New WbNode(Me)
            n.Parent = parent
            Dim found(Members.Length - 1) As WbNode
            Dim defPos = 0
            While pos < subs.Count AndAlso defPos < Members.Length
                Dim sig = subs(pos).Signature
                Dim dataLen = If(subs(pos).Data Is Nothing, 0, subs(pos).Data.Length)

                ' Si la firma no pertenece al struct: o se saltea (firma declarada como salteable)
                ' o el struct se CIERRA. Ese corte es lo que delimita la corrida.
                If Not AllSignatures.Contains(sig) Then
                    If SkipSignatures.Contains(sig) Then
                        pos += 1
                        Continue While
                    End If
                    Exit While
                End If

                If AllowUnordered Then
                    Dim idx = IndexOfMemberFor(ctx, sig, dataLen)
                    If idx < 0 Then
                        pos += 1
                        Continue While
                    End If
                    defPos = idx
                End If

                ' El miembro no puede con el subrecord ⇒ avanza el cursor de MIEMBROS, no el de
                ' subrecords.
                If Not Members(defPos).CanHandle(ctx, sig, dataLen) Then
                    defPos += 1
                    Continue While
                End If

                ' Miembro ya ocupado ⇒ el struct se cierra: no se permiten duplicados.
                If found(defPos) IsNot Nothing Then Exit While

                Dim child = Members(defPos).Parse(ctx, subs, pos, n)
                n.AddChild(child)
                found(defPos) = child

                ' Un miembro terminador cierra el struct acá mismo: lo que siga pertenece al
                ' elemento siguiente, no a este.
                If Members(defPos).IsTerminator Then Exit While

                If AllowUnordered Then defPos = 0 Else defPos += 1
            End While
            Return n
        End Function

        Private Function IndexOfMemberFor(ctx As WbContext, sig As String, dataLen As Integer) As Integer
            For i = 0 To Members.Length - 1
                If Members(i).CanHandle(ctx, sig, dataLen) Then Return i
            Next
            Return -1
        End Function

        Public Overrides Sub Emit(node As WbNode, bw As BinaryWriter, ctx As WbContext)
            For Each c In node.Children
                CType(c.Def, WbMemberDef).Emit(c, bw, ctx)
            Next
        End Sub

        ''' <summary>Miembros de un struct NUEVO: solo los marcados Required.
        ''' <para>⛔ Aca hubo una regla "el miembro 0 se crea siempre", copiada del predicado de
        ''' <c>AddRequiredElements</c>. Estaba mal aplicada y se saco: el sintoma que la motivo -un ARMO
        ''' nuevo sin EDID- es la conducta de xEdit, no un defecto. Con ella, los 120 records de TES5 que
        ''' declaran EDID sin <c>AsRequired</c> lo emitian igual.</para></summary>
        Public Overrides Function CreateRequired(ctx As WbContext) As WbNode
            Dim n As New WbNode(Me)
            For Each m In Members
                If m.Required Then n.AddChild(m.CreateRequired(ctx))
            Next
            Return n
        End Function
    End Class

    ''' <summary>Array de subrecords: repite UN miembro mientras el elemento pueda hacerse cargo del
    ''' siguiente subrecord.</summary>
    Public NotInheritable Class WbRArrayDef
        Inherits WbMemberDef

        Public ReadOnly Property Element As WbMemberDef
        ''' <summary>El array se declara como ordenable, pero el orden del ARCHIVO es el de la
        ''' fuente. El motor conserva el orden de la fuente — ordenar al emitir cambiaría bytes, y
        ''' un cambio de bytes lo decide el usuario.</summary>
        Public Property Sorted As Boolean
        ''' <summary>Ruta al nodo que lleva la cantidad de elementos. Al emitir se RECALCULA desde
        ''' la longitud real del array.</summary>
        Public Property CountPath As String

        Public Sub New(name As String, element As WbMemberDef)
            Me.Name = name
            _Element = element
        End Sub

        Public Function WithCountPath(path As String) As WbRArrayDef
            CountPath = path
            Return Me
        End Function

        Public Overrides Function CanHandle(ctx As WbContext, sig As String, dataLen As Integer) As Boolean
            ' Delega en el elemento: lo que él acepte, lo acepta el array.
            Return Element.CanHandle(ctx, sig, dataLen)
        End Function

        Public Overrides Sub CollectSignatures(into As HashSet(Of String))
            Element.CollectSignatures(into)
        End Sub

        ''' <summary>Mientras el elemento pueda con el subrecord, se agrega; en cuanto no puede, el
        ''' array termina.</summary>
        Public Overrides Function Parse(ctx As WbContext, subs As IList(Of SubrecordData), ByRef pos As Integer, parent As WbNode) As WbNode
            Dim n As New WbNode(Me)
            n.Parent = parent
            While pos < subs.Count
                Dim sig = subs(pos).Signature
                Dim dataLen = If(subs(pos).Data Is Nothing, 0, subs(pos).Data.Length)
                If Not Element.CanHandle(ctx, sig, dataLen) Then Exit While
                Dim before = pos
                n.AddChild(Element.Parse(ctx, subs, pos, n))
                If pos = before Then Exit While   ' guarda anti-bucle: un elemento que no consume nada
            End While
            n.ParsedCount = n.Children.Count
            Return n
        End Function

        Public Overrides Sub Emit(node As WbNode, bw As BinaryWriter, ctx As WbContext)
            If Not String.IsNullOrEmpty(CountPath) AndAlso node.ParsedCount <> node.Children.Count Then
                Dim cn = WbPath.ResolveUpwards(node, CountPath)
                If cn IsNot Nothing Then cn.Value = WbCajas.Caja(CLng(node.ChildCount))
            End If
            For Each c In node.Children
                CType(c.Def, WbMemberDef).Emit(c, bw, ctx)
            Next
        End Sub

        Public Overrides Function CreateRequired(ctx As WbContext) As WbNode
            Return New WbNode(Me)
        End Function
    End Class

    ''' <summary>Unión de miembros: elige el PRIMER miembro que puede hacerse cargo del
    ''' subrecord.</summary>
    Public NotInheritable Class WbRUnionDef
        Inherits WbMemberDef

        Public ReadOnly Property Members As WbMemberDef()

        ''' <summary>Decisor opcional. Cuando está presente MANDA sobre la prueba miembro por
        ''' miembro: se usa el miembro que él elige, y sólo se cae a probar uno por uno si el
        ''' decisor no resuelve (devuelve un índice fuera de rango).
        ''' <para>Ignorarlo y quedarse con "el primer miembro que pueda" hace que uniones como el
        ''' Action de SCEN tomen la rama equivocada, dejen sus subrecords sin consumir y CORTEN el
        ''' array de Actions entero.</para></summary>
        Public Property Decider As WbDecider

        Public Sub New(name As String, members As WbMemberDef())
            Me.Name = name
            _Members = members
        End Sub

        Public Function WithDecider(d As WbDecider) As WbRUnionDef
            Decider = d
            Return Me
        End Function

        ''' <summary>Miembro que elige el decisor, o Nothing si no resuelve.</summary>
        Private Function Decided(ctx As WbContext, parent As WbNode) As WbMemberDef
            If Decider Is Nothing Then Return Nothing
            Dim i = Decider(ctx, Nothing, 0, 0, parent)
            If i < 0 OrElse i >= Members.Length Then Return Nothing
            Return Members(i)
        End Function

        Public Overrides Function CanHandle(ctx As WbContext, sig As String, dataLen As Integer) As Boolean
            For Each m In Members
                If m.CanHandle(ctx, sig, dataLen) Then Return True
            Next
            Return False
        End Function

        Public Overrides Sub CollectSignatures(into As HashSet(Of String))
            For Each m In Members
                m.CollectSignatures(into)
            Next
        End Sub

        Public Overrides Function Parse(ctx As WbContext, subs As IList(Of SubrecordData), ByRef pos As Integer, parent As WbNode) As WbNode
            Dim sig = subs(pos).Signature
            Dim dataLen = If(subs(pos).Data Is Nothing, 0, subs(pos).Data.Length)
            Dim dm = Decided(ctx, parent)
            If dm IsNot Nothing AndAlso dm.CanHandle(ctx, sig, dataLen) Then Return dm.Parse(ctx, subs, pos, parent)
            For Each m In Members
                If m.CanHandle(ctx, sig, dataLen) Then Return m.Parse(ctx, subs, pos, parent)
            Next
            Throw New WbLayoutException(Name, $"ningún miembro de la unión puede con {sig}")
        End Function

        Public Overrides Sub Emit(node As WbNode, bw As BinaryWriter, ctx As WbContext)
            CType(node.Def, WbMemberDef).Emit(node, bw, ctx)
        End Sub

        Public Overrides Function CreateRequired(ctx As WbContext) As WbNode
            Return Members(0).CreateRequired(ctx)
        End Function
    End Class

    ''' <summary>Definición de un record entero: firma + lista ORDENADA de miembros.
    ''' Un <see cref="WbRecordDef"/> es GAME-AWARE por construcción: el esquema de Fallout 4 y el de
    ''' Skyrim son dos objetos distintos, porque son dos formatos distintos que ni siquiera
    ''' comparten el orden de los campos.</summary>
    Public NotInheritable Class WbRecordDef
        Public ReadOnly Property Signature As String
        Public ReadOnly Property Members As WbMemberDef()
        Public ReadOnly Property Game As WbGame
        ''' <summary>El record se indexa por firma en vez de recorrerse con cursor monótono.</summary>
        Public Property AllowUnordered As Boolean

        ''' <summary>Nombre de cada bit de la CABECERA del record, por posición; vacío el que no
        ''' tiene nombre. Son bits del encabezado, no del cuerpo: no ocupan bytes del árbol y por eso
        ''' no aparecen como campo de ninguna estructura.</summary>
        Public Property RecordFlagNames As String()
        ''' <summary>Al menos un campo del record no se pudo declarar. No cuenta como verificado y el
        ''' arnés lo reporta aparte.</summary>
        Public Property IsIncomplete As Boolean

        Private _sigCache As HashSet(Of String)

        Public Sub New(game As WbGame, sig As String, members As WbMemberDef())
            _Game = game
            _Signature = sig
            _Members = members
        End Sub

        ''' <summary>Las firmas que este record puede aceptar, memoizadas. Misma barrera y mismo motivo que
        ''' la de <c>WbRStructDef</c>: la def esta cacheada por proceso y varios hilos arman arboles a la vez.</summary>
        Public ReadOnly Property AllSignatures As HashSet(Of String)
            Get
                Dim actual = Threading.Volatile.Read(_sigCache)
                If actual IsNot Nothing Then Return actual
                Dim s As New HashSet(Of String)(StringComparer.Ordinal)
                For Each m In Members
                    m.CollectSignatures(s)
                Next
                Threading.Volatile.Write(_sigCache, s)
                Return s
            End Get
        End Property
    End Class

    ''' <summary>Resolución de rutas que sube por los ancestros. Un contador declarado por ruta —
    ''' el <c>KSIZ</c> que cuenta los keywords, por ejemplo — apunta a un HERMANO del array dentro
    ''' del struct que los contiene, así que la búsqueda no puede quedarse en el nodo.</summary>
    Public Module WbPath
        Public Function ResolveUpwards(from As WbNode, path As String) As WbNode
            ' Los `..` iniciales son redundantes acá: esta función ya prueba en TODOS los
            ' ancestros. Las rutas declaradas los llevan porque están escritas como rutas
            ' relativas estrictas (`..\XCNT\Swimming Count`).
            Dim p = path
            While p.StartsWith("..\", StringComparison.Ordinal)
                p = p.Substring(3)
            End While
            Dim n = from
            While n IsNot Nothing
                Dim hit = n.ByPath(p)
                If hit IsNot Nothing AndAlso hit IsNot n Then Return LeafOf(hit)
                n = n.Parent
            End While
            Return Nothing
        End Function

        ''' <summary>Baja de un nodo CONTENEDOR a su hoja de valor.
        ''' <para>Una ruta de contador puede apuntar a la FIRMA del subrecord, y el nodo del
        ''' subrecord no tiene valor: el valor está en su hijo. Sin bajar, el contador se lee como
        ''' Nothing ⇒ 0 elementos ⇒ el <c>KWDA</c> queda vacío y sus bytes sin consumir. Afecta a
        ''' AMMO, ARMO, ARTO, CONT y a todo lo que lleva keywords.</para></summary>
        Public Function LeafOf(n As WbNode) As WbNode
            Dim cur = n
            While cur IsNot Nothing AndAlso cur.Value Is Nothing AndAlso cur.Children.Count = 1
                cur = cur.Children(0)
            End While
            Return cur
        End Function
    End Module

End Namespace
