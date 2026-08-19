Namespace Canon

    ''' <summary>Edición del árbol parseado.
    '''
    ''' <para>El árbol es a la vez el modelo de lectura, el de edición y el de escritura: se lee de
    ''' él, se modifica en el lugar y se emite. Eso evita tener el mismo layout transcrito en
    ''' varias clases paralelas que después hay que mantener en sincronía a mano.</para>
    '''
    ''' <list type="bullet">
    ''' <item><b>editar</b> = escribir un valor por RUTA (<c>DESC\Description</c>, <c>DATA\Weight</c>)</item>
    ''' <item><b>borrar</b> = sacar el nodo, con lo cual el subrecord DESAPARECE de la salida. Sin
    ''' esto hace falta una bandera paralela por cada campo opcional para distinguir "vacío" de
    ''' "ausente".</item>
    ''' <item><b>agregar</b> = crear el nodo del miembro declarado en la POSICIÓN que le asigna el
    ''' esquema (<see cref="EnsureSubrecord"/>), no al final. El orden no lo elige el llamador.</item>
    ''' </list>
    '''
    ''' <para>Lo que NO vive acá: la metadata de autoría (si es override, si está sucio, FormID
    ''' provisional, prefijo de EditorID, dependencias al guardar). Eso no es layout.</para>
    ''' </summary>
    Public Module WbEdit

        ''' <summary>Valor de una hoja por ruta, o Nothing si el nodo no existe.
        ''' Nothing significa AUSENTE; una cadena vacía significa presente y vacía. Son cosas
        ''' distintas y el árbol las distingue sin necesidad de banderas paralelas.</summary>
        Public Function GetValue(root As WbNode, path As String) As Object
            Dim n = root.ByPath(path)
            If n Is Nothing Then Return Nothing
            If n.Children.Count = 1 AndAlso n.Children(0).Children.Count = 0 Then Return n.Children(0).Value
            Return n.Value
        End Function

        ''' <summary>Escribe una hoja por ruta. Devuelve False si el nodo no existe: no lo crea al
        ''' voleo, porque crear un subrecord tiene que respetar la POSICIÓN que le asigna el
        ''' esquema (ver <see cref="EnsureSubrecord"/>).</summary>
        Public Function SetValue(root As WbNode, path As String, value As Object) As Boolean
            Dim n = root.ByPath(path)
            If n Is Nothing Then Return False
            If n.Children.Count = 1 AndAlso n.Children(0).Children.Count = 0 Then n = n.Children(0)
            If n.Children.Count > 0 Then Return False
            n.Value = value
            Return True
        End Function

        ''' <summary>Busca un subrecord por firma en TODO el árbol, no sólo entre los hijos
        ''' directos.
        ''' <para>Hace falta porque muchos subrecords viven DENTRO de una agrupación: <c>KSIZ</c> y
        ''' <c>KWDA</c> cuelgan del grupo 'Keywords', no del record. Quien pregunta por "el KWDA"
        ''' no tiene por qué saber en qué agrupación quedó.</para></summary>
        Public Function FindSubrecord(root As WbNode, sig As String) As WbNode
            For Each n In root.Walk()
                If String.Equals(n.Signature, sig, StringComparison.Ordinal) Then Return n
            Next
            Return Nothing
        End Function

        ''' <summary>Localiza una hoja por (firma de subrecord, nombre de campo) sin depender de
        ''' cómo se llame la estructura intermedia.
        ''' <para>Hace falta porque los contenedores no se llaman igual en los dos juegos: el
        ''' <c>DATA</c> del ARMO es una estructura sin nombre en Fallout 4 y una llamada 'Data' en
        ''' Skyrim. Quien pregunte por "el Value del DATA" tiene que poder escribir lo mismo para
        ''' los dos, o vuelven a hacer falta dos códigos.</para></summary>
        Public Function FindField(root As WbNode, sig As String, fieldName As String) As WbNode
            Dim srNode = FindSubrecord(root, sig)
            If srNode Is Nothing Then Return Nothing
            For Each n In srNode.Walk()
                If n Is srNode Then Continue For
                If n.Children.Count = 0 AndAlso String.Equals(n.Name, fieldName, StringComparison.Ordinal) Then Return n
            Next
            Return Nothing
        End Function

        ''' <summary>Valor de un campo por (firma, nombre). Nothing = ausente.</summary>
        Public Function GetField(root As WbNode, sig As String, fieldName As String) As Object
            Dim n = FindField(root, sig, fieldName)
            Return If(n Is Nothing, Nothing, n.Value)
        End Function

        ''' <summary>Escribe un campo por (firma, nombre). False si no existe.</summary>
        Public Function SetField(root As WbNode, sig As String, fieldName As String, value As Object) As Boolean
            Dim n = FindField(root, sig, fieldName)
            If n Is Nothing Then Return False
            n.Value = value
            Return True
        End Function

        ''' <summary>Saca un hijo directo de la raíz por firma de subrecord. Devuelve cuántos sacó.
        ''' Es la operación de "sacale la descripción".</summary>
        Public Function RemoveSubrecord(root As WbNode, sig As String) As Integer
            Dim removed = 0
            For i = root.Children.Count - 1 To 0 Step -1
                If String.Equals(root.Children(i).Signature, sig, StringComparison.Ordinal) Then
                    root.Children.RemoveAt(i)
                    removed += 1
                End If
            Next
            Return removed
        End Function

        ''' <summary>Se asegura de que exista el subrecord <paramref name="sig"/> de nivel superior,
        ''' insertándolo en la POSICIÓN que le corresponde si falta: la que dicta el orden de
        ''' <c>WbRecordDef.Members</c>, es decir el orden con el que se declaró el record.
        ''' <para>Así el orden de los subrecords deja de ser una secuencia de llamadas que hay que
        ''' mantener idéntica en cada emisor: es un dato de la declaración.</para>
        ''' <para>Devuelve el nodo (existente o nuevo), o Nothing si el esquema no declara esa firma
        ''' como miembro de nivel superior.</para></summary>
        Public Function EnsureSubrecord(root As WbNode, def As WbRecordDef, sig As String, ctx As WbContext) As WbNode
            Dim existing = root.BySignature(sig)
            If existing IsNot Nothing Then Return existing

            Dim memberIdx = -1
            For i = 0 To def.Members.Length - 1
                Dim sd = TryCast(def.Members(i), WbSubrecordDef)
                If sd IsNot Nothing AndAlso String.Equals(sd.Signature, sig, StringComparison.Ordinal) Then
                    memberIdx = i
                    Exit For
                End If
            Next
            If memberIdx < 0 Then Return Nothing

            Dim node = def.Members(memberIdx).CreateRequired(ctx)
            node.Parent = root

            ' Insertar delante del primer hijo cuyo miembro venga DESPUÉS en la declaración.
            Dim insertAt = root.Children.Count
            For c = 0 To root.Children.Count - 1
                Dim idx = MemberIndexOf(def, root.Children(c))
                If idx < 0 Then
                    ' Falla ruidosa a propósito. Si no se sabe a qué miembro corresponde un hijo
                    ' que ya está, tampoco se sabe dónde va el nuevo: tratar "no lo encontré" como
                    ' "va al final" INSERTA EL SUBRECORD EN EL LUGAR EQUIVOCADO y el record sale
                    ' corrupto, porque al releerlo se descarta todo lo que venga fuera de orden.
                    ' Un fallback silencioso sobre datos derivados es peor que cortar.
                    Throw New InvalidOperationException(
                        $"EnsureSubrecord({sig}): no puedo ubicar el subrecord '{root.Children(c).Signature}' " &
                        $"en los miembros de {def.Signature}; insertar a ciegas corrompería el record.")
                End If
                If idx > memberIdx Then
                    insertAt = c
                    Exit For
                End If
            Next
            root.Children.Insert(insertAt, node)
            Return node
        End Function

        ''' <summary>Índice del miembro del record al que corresponde un hijo de la raíz, o -1.
        ''' <para>Tiene que mirar DENTRO de las uniones de subrecords: al parsear una unión, el nodo
        ''' que queda es el del MIEMBRO elegido (el subrecord BOD2 o el BODT, por ejemplo) y no un
        ''' nodo de la unión, así que compararlo contra el miembro de nivel superior nunca
        ''' acierta.</para>
        ''' <para>Tratar "no lo identifiqué" como "va último" rompe el record: en un ARMO de Skyrim
        ''' sin DESC, el DESC terminaba en la posición 2 (<c>EDID DESC BODT RNAM DATA DNAM</c> en
        ''' vez de <c>EDID BODT RNAM DESC DATA DNAM</c>), y al releer esa salida se descartaban
        ''' BODT, RNAM, DATA y DNAM por venir fuera de orden — una armadura sin body template, sin
        ''' raza, sin valor ni peso y sin armor rating.</para></summary>
        Private Function MemberIndexOf(def As WbRecordDef, child As WbNode) As Integer
            For i = 0 To def.Members.Length - 1
                If def.Members(i) Is child.Def Then Return i
                Dim u = TryCast(def.Members(i), WbRUnionDef)
                If u IsNot Nothing Then
                    For Each um In u.Members
                        If um Is child.Def Then Return i
                    Next
                End If
            Next
            Return -1
        End Function

    End Module

End Namespace
