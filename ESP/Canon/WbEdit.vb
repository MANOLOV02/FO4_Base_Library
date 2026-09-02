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

        ''' <summary>Separador de tramos de una ruta de campo.</summary>
        Private Const SEPARADOR As Char = "\"c

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

        ''' <summary>Se asegura de que exista el subrecord donde vive la CUENTA de un arreglo.
        ''' <para>Hay arreglos cuyo tamaño no va con ellos sino en un subrecord aparte. Al emitir, el
        ''' arreglo actualiza ese valor — pero si el subrecord no existe no hay dónde escribirlo, y el
        ''' record sale con la lista y sin su cuenta: al releerlo el lector cuenta cero y la lista
        ''' entera desaparece. Pasa siempre que una lista va de vacía a tener algo.</para>
        ''' <para>Se prueba en el nodo y en sus ancestros, igual que hace la resolución de esa ruta al
        ''' emitir: la cuenta puede vivir a cualquier altura por encima del arreglo.</para></summary>
        Private Sub AsegurarContador(arreglo As WbNode, ctx As WbContext, rutaDeCuenta As String)
            If String.IsNullOrEmpty(rutaDeCuenta) Then Return
            If WbPath.ResolveUpwards(arreglo, rutaDeCuenta) IsNot Nothing Then Return

            Dim limpia = rutaDeCuenta
            While limpia.StartsWith("..\", StringComparison.Ordinal)
                limpia = limpia.Substring(3)
            End While

            Dim n = arreglo.Parent
            While n IsNot Nothing
                If EnsureFieldPath(n, ctx, limpia) IsNot Nothing Then Return
                n = n.Parent
            End While
        End Sub

        ''' <summary>Pone el valor en un nodo ya resuelto.
        ''' <para>Existe para que quien ya encontró el nodo no tenga que volver a buscarlo por ruta: la
        ''' segunda búsqueda puede usar otras reglas y no llegar al mismo lado, que es justo lo que
        ''' hace que un campo se pueda leer y no escribir.</para>
        ''' <para>Si el nodo es el envoltorio de un subrecord con una sola hoja adentro, el valor va en
        ''' la hoja. Un nodo con estructura debajo no es una hoja y no se toca.</para></summary>
        Public Function PonerValor(n As WbNode, valor As Object) As Boolean
            Dim hoja = HojaEditable(n)
            If hoja Is Nothing Then Return False
            hoja.Value = valor
            QuitarBytesSinDescribir(hoja)
            Return True
        End Function

        ''' <summary>Saca del subrecord de esta hoja los nodos de <c>Bytes sin describir</c>, si la hoja
        ''' es de TEXTO.
        '''
        ''' <para>Esos bytes existen para no perder lo que la declaración no supo explicar en la FUENTE.
        ''' Cuando el texto que los dejó se reescribe, ya no describen nada y se le pegan de cola al
        ''' valor nuevo: un <c>FULL</c> roto de 4 bytes se relee como 1 char + 2 sobrantes, y corregir el
        ''' nombre daba <c>oso[NUL]</c> + <c>[NUL][NUL]</c>.</para>
        '''
        ''' <para>⛔ Sólo para hojas de texto, y a propósito. Un sobrante lo deja un campo de LARGO
        ''' VARIABLE que no llenó el subrecord —o sea, un texto—, así que ligarlo a la edición de un
        ''' entero o de una referencia sería tirar bytes de un campo que el usuario no tocó. Vale para
        ''' los TRES textos y no sólo para el traducible: el <c>EDID</c> de un subrecord mal teselado
        ''' tenía exactamente el mismo problema.</para></summary>
        Private Sub QuitarBytesSinDescribir(hoja As WbNode)
            If Not (TypeOf hoja.Def Is WbStringDef OrElse TypeOf hoja.Def Is WbLStringDef OrElse
                    TypeOf hoja.Def Is WbLenStringDef) Then Return
            Dim sr = hoja
            While sr IsNot Nothing AndAlso String.IsNullOrEmpty(sr.Signature)
                sr = sr.Parent
            End While
            If sr Is Nothing Then Return
            For i = sr.Children.Count - 1 To 0 Step -1
                If String.Equals(sr.Children(i).Name, WbSubrecordDef.BytesSinDescribir, StringComparison.Ordinal) Then
                    sr.QuitarHijoEn(i)
                End If
            Next
        End Sub

        ''' <summary>La hoja que <see cref="PonerValor"/> escribiría, o Nothing si ese nodo no es
        ''' escribible. Existe para que quien necesite tocar algo MÁS del nodo escrito —su estado de
        ''' localización, por ejemplo— apunte exactamente a la misma hoja: si el descenso estuviera
        ''' escrito dos veces, un día una copia bajaría y la otra no.</summary>
        Public Function HojaEditable(n As WbNode) As WbNode
            If n Is Nothing Then Return Nothing
            If n.Children.Count = 1 AndAlso n.Children(0).Children.Count = 0 Then n = n.Children(0)
            If n.Children.Count > 0 Then Return Nothing
            Return n
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
            Return n?.Value
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
                    root.QuitarHijoEn(i)
                    removed += 1
                End If
            Next
            Return removed
        End Function

        ''' <summary>Devuelve el nodo de una ruta de campo, CREANDO los niveles que falten.
        '''
        ''' <para>Existe porque sin esto nada recién creado acepta campos: un record nuevo, o un elemento
        ''' recién agregado a una lista, nacen sólo con lo que el formato marca como obligatorio, y
        ''' escribirles cualquier otro campo no hace nada y no avisa.</para>
        '''
        ''' <para>Busca con BACKTRACKING, igual que la lectura: un tramo puede significar más de una cosa
        ''' —bajar por un hijo, nombrar al nodo en el que ya estamos, o pedir otra rama de una
        ''' alternativa— y hay que quedarse con la interpretación bajo la cual el RESTO de la ruta
        ''' llega a destino. Elegir la primera que parece encajar falla en los casos reales: un elemento
        ''' de lista puede tener un hijo que se llama igual que él.</para>
        '''
        ''' <para>No crea nada si la ruta ya resuelve, así que mirar un record no le agrega nada. Y lo que
        ''' se crea probando un camino que después no llega se deshace, para no dejar basura.</para></summary>
        Public Function EnsureFieldPath(nodo As WbNode, ctx As WbContext, ruta As String) As WbNode
            If nodo Is Nothing OrElse String.IsNullOrEmpty(ruta) Then Return Nothing
            Dim ya = nodo.ByFieldPath(ruta)
            If ya IsNot Nothing Then Return ya
            Dim pasos = ruta.Split(SEPARADOR).Where(Function(x) x.Trim().Length > 0).ToArray()
            Return Asegurar(nodo, ctx, pasos, 0)
        End Function

        Private Function Asegurar(cur As WbNode, ctx As WbContext, pasos As String(), idx As Integer) As WbNode
            If cur Is Nothing Then Return Nothing
            If idx >= pasos.Length Then Return cur
            Dim tramo = pasos(idx).Trim()

            ' 1) Bajar por un hijo que ya está.
            Dim hijo = cur.ByFieldPath(tramo)
            If hijo IsNot Nothing Then
                Dim r = Asegurar(hijo, ctx, pasos, idx + 1)
                If r IsNot Nothing Then Return r
            End If

            ' 2) El tramo nombra al nodo en el que ya estamos. Pasa cuando la ruta de un campo de una
            '    lista arranca nombrando al elemento, y también cuando un subrecord y la estructura que
            '    lleva adentro se llaman igual.
            If NombraA(cur, tramo) Then
                Dim r = Asegurar(cur, ctx, pasos, idx + 1)
                If r IsNot Nothing Then Return r
            End If

            ' 3) Otra rama de una alternativa.
            Dim rama = ReevaluarAlternativa(cur, ctx, tramo)
            If rama IsNot Nothing Then
                Dim r = Asegurar(rama, ctx, pasos, idx + 1)
                If r IsNot Nothing Then Return r
            End If

            ' 4) Crear el miembro que falta. Si el resto de la ruta igual no llega, se deshace.
            Dim antes = cur.Children.Count
            Dim nuevo = CrearMiembro(cur, ctx, tramo)
            If nuevo IsNot Nothing Then
                Dim r = Asegurar(nuevo, ctx, pasos, idx + 1)
                If r IsNot Nothing Then Return r
                If cur.ChildCount > antes Then cur.QuitarHijo(nuevo)
            End If

            Return Nothing
        End Function

        ''' <summary>Vuelve a decidir qué rama de una alternativa corresponde, y la cambia si hace falta.
        '''
        ''' <para>La rama la elige otro campo del record. Al armar un elemento nuevo ese campo todavía
        ''' vale cero, así que queda la rama por defecto; cuando después se le pone el valor real, la
        ''' rama ya no es la que corresponde y escribir en ella no tiene dónde aterrizar — sin error y
        ''' sin aviso.</para>
        '''
        ''' <para>Sólo cambia la rama si la que decide el discriminador ES la que se está pidiendo: así
        ''' no puede reescribir una rama que ya tiene dato bueno.</para></summary>
        Private Function ReevaluarAlternativa(nodo As WbNode, ctx As WbContext, tramo As String) As WbNode
            Dim un = TryCast(nodo.Def, WbUnionDef)
            If un Is Nothing OrElse un.Decider Is Nothing Then Return Nothing

            Dim idx = un.Decider(ctx, Nothing, 0, 0, nodo.Parent)
            If idx < 0 OrElse idx >= un.Members.Length Then Return Nothing
            If idx = nodo.UnionBranch Then Return Nothing

            Dim elegida = un.Members(idx)
            If elegida Is Nothing OrElse Not String.Equals(elegida.Name, tramo, StringComparison.Ordinal) Then Return Nothing

            Dim nuevo = elegida.CreateDefault(ctx)
            If nuevo Is Nothing Then Return Nothing
            nuevo.Parent = nodo
            nodo.LimpiarHijos()
            nodo.AddChild(nuevo)
            nodo.UnionBranch = idx
            Return nuevo
        End Function

        ''' <summary>Ese tramo es el nombre o la firma del nodo mismo.</summary>
        Private Function NombraA(n As WbNode, tramo As String) As Boolean
            If String.Equals(n.Signature, tramo, StringComparison.Ordinal) Then Return True
            Return n.Def IsNot Nothing AndAlso String.Equals(n.Def.Name, tramo, StringComparison.Ordinal)
        End Function

        ''' <summary>Crea bajo el nodo actual el miembro que se llama o se firma así, en la posición que
        ''' le da la declaración. Devuelve el nodo nuevo, o Nothing si ese nivel no tiene miembros de
        ''' subrecord o el formato no declara ese campo ahí.</summary>
        Private Function CrearMiembro(cur As WbNode, ctx As WbContext, tramo As String) As WbNode
            Dim miembros = MiembrosDe(cur, ctx)
            If miembros Is Nothing Then Return Nothing

            Dim idx = IndiceDeMiembro(miembros, tramo)
            If idx < 0 Then Return Nothing

            Dim nuevo = miembros(idx).CreateRequired(ctx)
            If nuevo Is Nothing Then Return Nothing
            nuevo.Parent = cur
            cur.InsertarHijo(PosicionDe(cur, miembros, idx), nuevo)
            Return cur.ByFieldPath(tramo)
        End Function

        ''' <summary>Los miembros que la declaración le da a este nodo, o Nothing si no es un nodo con
        ''' miembros de subrecord.</summary>
        Private Function MiembrosDe(nodo As WbNode, ctx As WbContext) As WbMemberDef()
            Dim st = TryCast(nodo.Def, WbRStructDef)
            If st IsNot Nothing Then Return st.Members
            If TypeOf nodo.Def Is WbRootDef Then
                If ctx Is Nothing Then Return Nothing
                Dim d = WbSchema.Get(ctx.Game, ctx.RecordSignature)
                Return d?.Members
            End If
            Return Nothing
        End Function

        ''' <summary>Índice del miembro que se llama o se firma así.</summary>
        Private Function IndiceDeMiembro(miembros As WbMemberDef(), nombreOFirma As String) As Integer
            For i = 0 To miembros.Length - 1
                Dim m = miembros(i)
                Dim sd = TryCast(m, WbSubrecordDef)
                If sd IsNot Nothing AndAlso String.Equals(sd.Signature, nombreOFirma, StringComparison.Ordinal) Then Return i
                If Not String.IsNullOrEmpty(m.Name) AndAlso String.Equals(m.Name, nombreOFirma, StringComparison.Ordinal) Then Return i
            Next
            ' Una unión de miembros no crea nivel: la firma que se busca puede ser la de una de sus ramas,
            ' y el miembro que hay que crear es la unión.
            For i = 0 To miembros.Length - 1
                Dim un = TryCast(miembros(i), WbRUnionDef)
                If un Is Nothing Then Continue For
                For Each rama In un.Members
                    Dim sd = TryCast(rama, WbSubrecordDef)
                    If sd IsNot Nothing AndAlso String.Equals(sd.Signature, nombreOFirma, StringComparison.Ordinal) Then Return i
                Next
            Next
            Return -1
        End Function

        ''' <summary>Dónde insertar el miembro nuevo: delante del primer hijo que venga DESPUÉS en la
        ''' declaración.
        ''' <para>Un hijo que no se puede ubicar NO corta: hay estructuras que se leen aplanadas, y su
        ''' contenido queda como hijo directo sin ser un miembro de este nivel. Tirar acá haría que
        ''' escribir CUALQUIER campo de un record que traiga una de esas estructuras fallara entero.
        ''' Se lo saltea: no dice nada sobre dónde va el nuevo.</para></summary>
        Private Function PosicionDe(nodo As WbNode, miembros As WbMemberDef(), idx As Integer) As Integer
            For c = 0 To nodo.Children.Count - 1
                Dim i = IndiceDelHijo(miembros, nodo.Children(c))
                If i < 0 Then Continue For
                If i > idx Then Return c
            Next
            Return nodo.Children.Count
        End Function

        ''' <summary>A qué miembro corresponde un hijo, o -1.
        ''' <para>Mira DENTRO de las uniones de subrecords: al leer una unión el nodo que queda es el del
        ''' miembro elegido, no uno de la unión.</para></summary>
        Private Function IndiceDelHijo(miembros As WbMemberDef(), child As WbNode) As Integer
            For i = 0 To miembros.Length - 1
                If miembros(i) Is child.Def Then Return i
                Dim u = TryCast(miembros(i), WbRUnionDef)
                If u IsNot Nothing Then
                    For Each um In u.Members
                        If um Is child.Def Then Return i
                    Next
                End If
            Next
            Return -1
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

            Return InsertarEnPosicionDeclarada(root, def, def.Members(memberIdx).CreateRequired(ctx))
        End Function

        ''' <summary>Mete un nodo YA ARMADO en la posición que su declaración le asigna entre los hijos
        ''' de la raíz: delante del primer hijo cuyo miembro venga DESPUÉS en <c>WbRecordDef.Members</c>.
        ''' Devuelve el mismo nodo.
        '''
        ''' <para>Es la mitad reusable de <see cref="EnsureSubrecord"/>. Se extrajo porque hay un segundo
        ''' dueño que NO puede direccionar por firma: <c>CanonHerencia.Materializar</c> injerta los
        ''' miembros del TERMINAL, y su llave es <c>Def.Name</c> —los armatures, el modelo masculino y las
        ''' keywords cuelgan de contenedores SIN firma (<c>Models</c>/<c>Armature</c>, <c>Male</c>,
        ''' <c>Keywords</c>)—. Injertaba al final, y el final es el lugar equivocado: la DEF de ARMO
        ''' declara <c>TNAM</c> y <c>APPR</c> DESPUÉS de todo lo heredado
        ''' (<c>wbDefinitionsFO4.pas:5840-5842</c>), y en Skyrim <c>DATA</c> del hijo va después de
        ''' <c>Armature</c>/<c>Keywords</c>/<c>DESC</c> (<c>wbDefinitionsTES5.pas:4085-4090</c>). Al releer
        ''' esa salida, el cursor de miembros de <see cref="WbReader"/> —MONÓTONO, y ARMO no es
        ''' <c>AllowUnordered</c>— manda a <c>WbPassthroughDef</c> todo lo que viene después
        ''' (<c>WbReader.vb:85-90</c>): la armadura pierde armatures, slots, keywords y raza al recargar, y
        ''' re-guardarla TIRA (<c>WbReader.vb:192-204</c>).</para>
        '''
        ''' <para>⛔ TIRA si el nodo no corresponde a ningún miembro del record, y TIRA si no puede ubicar
        ''' a un hijo que ya está. Lo segundo es la misma falla ruidosa de siempre y por el mismo motivo:
        ''' si no se sabe dónde está parado lo que ya hay, tampoco se sabe dónde va lo nuevo, y tratar "no
        ''' lo encontré" como "va al final" deja el record corrupto en silencio. En un ARMO de Skyrim sin
        ''' <c>DESC</c>, el <c>DESC</c> terminaba en la posición 2 (<c>EDID DESC BODT RNAM DATA DNAM</c>) y
        ''' al releer esa salida se descartaban BODT, RNAM, DATA y DNAM.</para>
        '''
        ''' <para>⛔ CON UNA EXCEPCIÓN, Y ES DE CAUSA CONOCIDA: un <see cref="WbPassthroughDef"/> NO es un
        ''' hijo que no se pudo identificar — es un hijo que <b>por construcción</b> no tiene miembro. El
        ''' lector fabrica su def en el acto (<c>WbReader.vb:68</c>, <c>:77</c>, <c>:88</c>) y esa instancia
        ''' no está ni puede estar en <c>def.Members</c>, así que <see cref="MemberIndexOf"/> —que compara
        ''' por REFERENCIA— da −1 siempre. Se lo SALTEA: no dice nada sobre dónde va el nuevo, exactamente
        ''' igual que hace <see cref="PosicionDe"/>, el gemelo de este bucle, desde que existe.</para>
        '''
        ''' <para><b>Por qué saltearlo y no tirar</b>, con cita de las dos autoridades:</para>
        ''' <list type="number">
        ''' <item><b>xEdit</b> (<c>TwbMainRecord.DoInit</c>, <c>wbImplementation.pas:10545-10580</c> y
        ''' <c>:10640-10660</c>): un subrecord inesperado o fuera de orden se REPORTA
        ''' (<c>wbProgressCallback('Error: record … contains unexpected (or out of order) subrecord …')</c>),
        ''' marca <c>FoundError</c> y hace <c>Inc(CurrentRecPos); Continue;</c> — el elemento QUEDA en
        ''' <c>cntElements</c>, sin Def y sin SortOrder, y <b>el resto del record se procesa normal. No
        ''' tira y no aborta el record.</b></item>
        ''' <item><b>El motor</b>: la rama de herencia copia un conjunto CERRADO de componentes
        ''' (<c>Fallout4.exe 0x1404626A0</c>, rama viva <c>0x14046276F</c>–<c>0x1404628D5</c>, 13;
        ''' <c>SkyrimSE.exe 0x14027E780</c>, <c>0x14027E837</c>–<c>0x14027E95E</c>, 12 — la tabla está en
        ''' <see cref="CanonHerencia.MiembrosHeredados"/>). Un subrecord que el esquema no declara no está
        ''' en ese conjunto: <b>ni se hereda ni puede impedir la copia</b>. No hace falta dirección nueva;
        ''' la lista cerrada ya citada lo resuelve.</item>
        ''' <item><b>El lector, acá</b>: ya declara al passthrough un HALLAZGO reportado que "se conserva
        ''' sin interpretar" (<c>WbReader.vb:23-24</c> y <c>:156-163</c>), no un fatal.</item>
        ''' </list>
        '''
        ''' <para>Qué NO cambia: el guardado. <c>WbPassthroughDef.Emit</c> (<c>WbReader.vb:192-204</c>)
        ''' sigue tirando salvo <c>AllowPendingSubrecords</c>, así que un record con un passthrough sigue
        ''' siendo INEMITIBLE. Lo que se destraba es la LECTURA — el render y el bake, que llegan acá por
        ''' <c>CanonHerencia.Materializar</c> y no atrapan nada
        ''' (<c>NpcMeshCollector:389</c>, <c>NpcMaterialResolver:373</c>).</para>
        '''
        ''' <para>El orden declarado se preserva y es demostrable: <c>insertAt</c> pasa a ser el índice del
        ''' primer hijo IDENTIFICABLE cuyo miembro viene después. Los passthrough anteriores quedan antes
        ''' del injerto y los posteriores después, y entre miembros declarados no cambia nada. Con
        ''' <c>[EDID(0), ZZZZ(−1), BOD2(8)]</c> e injerto del miembro 5 queda
        ''' <c>[EDID, ZZZZ, nuevo, BOD2]</c>.</para>
        '''
        ''' <para><b>Medido</b> (2026-09-01, gate <c>OutfitDraftSaveGate</c>, M1): ARMO con un hijo de
        ''' primer nivel que la DEF no ubica = <b>0 de 1.067</b> en el orden de carga de FO4 (71 plugins) y
        ''' <b>0 de 4.222</b> en el de SSE (98 plugins). O sea que el throw no era alcanzable sobre los
        ''' datos del usuario: esto es para los plugins de TERCEROS, que es a quienes se distribuye la
        ''' app.</para></summary>
        Public Function InsertarEnPosicionDeclarada(root As WbNode, def As WbRecordDef, nodo As WbNode) As WbNode
            Dim memberIdx = MemberIndexOf(def, nodo)
            If memberIdx < 0 Then
                Throw New InvalidOperationException(
                    $"InsertarEnPosicionDeclarada: el nodo '{If(nodo.Signature, nodo.Def?.Name)}' no " &
                    $"corresponde a ningún miembro de {def.Signature}, así que no tiene posición declarada.")
            End If

            Dim insertAt = root.Children.Count
            For c = 0 To root.Children.Count - 1
                ' ⛔ El passthrough se saltea ANTES de preguntar el índice: su −1 no es una falla de
                ' identificación, es su definición. Preguntarlo primero y después distinguir la causa
                ' dejaría las dos cosas leyéndose como la misma.
                If TypeOf root.Children(c).Def Is WbPassthroughDef Then Continue For
                Dim idx = MemberIndexOf(def, root.Children(c))
                If idx < 0 Then
                    Throw New InvalidOperationException(
                        $"InsertarEnPosicionDeclarada({If(nodo.Signature, nodo.Def?.Name)}): no puedo ubicar " &
                        $"el subrecord '{root.Children(c).Signature}' en los miembros de {def.Signature}; " &
                        "insertar a ciegas corrompería el record.")
                End If
                If idx > memberIdx Then
                    insertAt = c
                    Exit For
                End If
            Next
            root.InsertarHijo(insertAt, nodo)
            Return nodo
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


        ''' <summary>Agrega un elemento vacío al final de un arreglo y lo devuelve.
        '''
        ''' <para>El elemento se arma con la misma declaración que usa la lectura, así que sale con
        ''' la forma que el formato le da: los campos obligatorios presentes y los opcionales no.
        ''' Escribirle encima es lo mismo que escribirle a uno leído.</para>
        '''
        ''' <para>Devuelve Nothing si el nodo no es un arreglo. No inventa uno: agregar un elemento
        ''' a algo que no es una lista es un error de quien llama, no algo que convenga tolerar.</para></summary>
        Public Function AgregarElemento(contenedor As WbNode, ctx As WbContext) As WbNode
            If contenedor Is Nothing Then Return Nothing

            Dim porMiembro = TryCast(contenedor.Def, WbRArrayDef)
            If porMiembro IsNot Nothing Then
                AsegurarContador(contenedor, ctx, porMiembro.CountPath)
                Dim nuevo = porMiembro.Element.CreateRequired(ctx)
                nuevo.Parent = contenedor
                contenedor.AddChild(nuevo)
                Return nuevo
            End If

            Dim porValor = TryCast(contenedor.Def, WbArrayDef)
            If porValor IsNot Nothing Then
                AsegurarContador(contenedor, ctx, porValor.CountPath)
                Dim nuevo = porValor.Element.CreateDefault(ctx)
                nuevo.Parent = contenedor
                contenedor.AddChild(nuevo)
                Return nuevo
            End If

            Return Nothing
        End Function

        ''' <summary>Saca del record el campo que esta en esa ruta, dejandolo AUSENTE.
        ''' <para>Ausente no es lo mismo que valer cero: el formato distingue las dos cosas y hay
        ''' campos donde el motor se comporta distinto segun el subrecord este o no.</para>
        ''' <para>Devuelve False si la ruta no resuelve: no habia nada que sacar.</para></summary>
        Public Function QuitarCampo(nodo As WbNode, ruta As String) As Boolean
            If nodo Is Nothing OrElse String.IsNullOrEmpty(ruta) Then Return False
            Dim destino = nodo.ByFieldPath(ruta)
            If destino Is Nothing Then Return False
            ' Se sube hasta el SUBRECORD que contiene al campo: sacar una hoja de adentro de una
            ' estructura dejaria el subrecord con menos bytes de los que su declaracion espera, y la
            ' relectura descartaria todo lo que viniera despues. El que se va es el subrecord entero.
            Dim actual = destino
            While actual IsNot Nothing
                If TypeOf actual.Def Is WbSubrecordDef AndAlso actual.Parent IsNot Nothing Then
                    ' Una sola implementacion de "sacar": la misma que usa QuitarSubrecord.
                    Return RemoveSubrecord(actual.Parent, actual.Signature) > 0
                End If
                actual = actual.Parent
            End While
            Return False
        End Function

        ''' <summary>Quita un elemento de un arreglo. Devuelve False si el índice no existe.
        ''' <para>El contador del arreglo, si el formato lo declara aparte, se recalcula solo al
        ''' escribir: no hay que acordarse de bajarlo a mano.</para></summary>
        Public Function QuitarElemento(contenedor As WbNode, indice As Integer) As Boolean
            If contenedor Is Nothing OrElse indice < 0 OrElse indice >= contenedor.Children.Count Then Return False
            contenedor.QuitarHijoEn(indice)
            Return True
        End Function

''' <summary>Reordena los elementos de un arreglo segun una permutacion de sus posiciones
''' actuales: <c>permutacion(i)</c> es el elemento que queda en la posicion i.
''' <para>El orden de los elementos ES un dato del record —el formato lo conserva tal cual y hay
''' arreglos donde la posicion misma significa algo—, asi que moverlos tiene que ser una
''' operacion del motor y no algo que cada consumidor arme sacando y volviendo a poner: eso
''' obligaria a copiar los campos de cada elemento a mano.</para>
''' <para>Exige una permutacion COMPLETA. Una lista parcial o con repetidos no reordena a medias:
''' devuelve False y no toca nada, porque un reorden silenciosamente incompleto deja el record
''' en un estado que nadie pidio.</para></summary>
        Public Function ReordenarElementos(contenedor As WbNode, permutacion As IList(Of Integer)) As Boolean
            If contenedor Is Nothing OrElse permutacion Is Nothing Then Return False
            Dim n = contenedor.Children.Count
            If permutacion.Count <> n Then Return False
            Dim visto(If(n = 0, 0, n - 1)) As Boolean
            For Each i In permutacion
                If i < 0 OrElse i >= n OrElse visto(i) Then Return False
                visto(i) = True
            Next
            Dim ordenados As New List(Of WbNode)(n)
            For Each i In permutacion
                ordenados.Add(contenedor.Children(i))
            Next
            contenedor.LimpiarHijos()
            For Each hijo In ordenados
                contenedor.AddChild(hijo)
            Next
            Return True
        End Function

    End Module

End Namespace
