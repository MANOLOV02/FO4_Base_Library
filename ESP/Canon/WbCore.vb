Imports System.Text

''' <summary>
''' Motor de layout: declara cómo está armado por dentro un record de un plugin, y esa MISMA
''' declaración se usa para leerlo y para volver a escribirlo.
'''
''' <para>El formato de un record es un ÁRBOL de DOS capas:</para>
''' <list type="bullet">
''' <item><b>Capa A — árbol de MIEMBROS</b> (nivel subrecord): un record es una lista ORDENADA de
''' miembros, de cuatro clases posibles: subrecord suelto, struct de subrecords (una corrida de
''' firmas agrupada bajo un nombre), array de subrecords (un miembro que se repite) y unión de
''' miembros (una de varias formas alternativas para el mismo lugar).</item>
''' <item><b>Capa B — árbol de VALORES</b> (dentro de los bytes de UN subrecord): structs,
''' arrays, uniones, enteros, floats, cadenas de ancho fijo o terminadas en cero, cadenas
''' localizables, referencias a otros records, bloques de bytes y campos de cero bytes.</item>
''' </list>
'''
''' <para>El lector recorre esa declaración (ver <see cref="WbReader"/>) y el escritor emite el
''' MISMO árbol (ver <see cref="WbWriter"/>): UNA declaración, dos recorridos. No hay copia
''' verbatim de bytes en ningún punto del motor — si un subrecord no está declarado se REPORTA
''' como pendiente, nunca se copia en silencio.</para>
''' </summary>
Namespace Canon

    ''' <summary>Juego cuya definición de formato manda. El layout de un mismo record NO se comparte
    ''' entre los dos: el ARMO de Fallout 4 y el de Skyrim tienen distinto orden de campos y campos
    ''' que el otro no lleva.</summary>
    Public Enum WbGame
        Fallout4 = 0
        Skyrim = 1
    End Enum

    ''' <summary>Ancho y signo de un entero. Los anchos, en el orden de esta enumeración, son
    ''' 0, 1, 1, 2, 2, 4, 4, 8, 8 y 3 bytes.
    ''' <para>El TAMAÑO sale de acá y de ningún otro lado: el motor no tiene offsets escritos a mano.</para>
    ''' <para><see cref="i0"/> es el ancho CERO, con valor siempre 0. Es un alias de solapamiento:
    ''' sirve para darle nombre a bits que ya consumió el campo anterior, sin gastar bytes propios.
    ''' Es lo que hace el 'General Flags' del <c>BOD2</c> de Skyrim.</para></summary>
    Public Enum WbIntType
        ''' <summary>Ancho 0: no ocupa bytes, valor siempre 0.</summary>
        i0 = 0
        u8 = 1
        s8 = 2
        u16 = 3
        s16 = 4
        u32 = 5
        s32 = 6
        u64 = 7
        s64 = 8
        ''' <summary>3 bytes sin signo.</summary>
        u24 = 9
    End Enum

    ''' <summary>Contexto de parseo/emisión. <see cref="FormVersion"/> es la Form Version del HEADER
    ''' del record y es la entrada de los deciders de versión: si la versión del record llega al
    ''' mínimo que pide un campo se elige la rama que lo incluye, y si no, una rama de CERO bytes.
    ''' Por eso el tamaño de un struct es f(versión), no una constante.</summary>
    Public NotInheritable Class WbContext
        Public ReadOnly Property Game As WbGame
        ''' <summary>Form Version del record (RecordHeader.Version). Entrada de los deciders de versión.</summary>
        Public Property FormVersion As UShort
        ''' <summary>Flag 0x80 del TES4 del archivo FUENTE. Con localización un lstring es un id u32;
        ''' sin ella es una zstring inline.</summary>
        Public Property Localized As Boolean
        ''' <summary>Encoding traducible del archivo fuente (TES4.SNAM &lt;cp:XXXX&gt;). Nothing =
        ''' usar el global de <c>PluginEncodingSettings</c>.</summary>
        Public Property TranslatableEncoding As Encoding
        ''' <summary>Signature del record que se está parseando (para diagnósticos).</summary>
        Public Property RecordSignature As String = ""

        ''' <summary>Identificador del record. No sale del arbol sino de la cabecera, y hace falta
        ''' porque casi todo lo que la aplicacion hace con un record empieza por identificarlo.</summary>
        Public Property FormID As UInteger
        ''' <summary>Flags del HEADER del record. Los consultan los deciders que dependen de ellos,
        ''' como el que distingue el tipo de un INFO mirando el bit 0x40.</summary>
        Public Property RecordFlags As UInteger
        ''' <summary>EditorID del record. Lo consulta el decider de los GMST, que despacha por su
        ''' PRIMERA LETRA.</summary>
        Public Property EditorId As String = ""

        ''' <summary>Permite EMITIR subrecords declarados como PENDIENTES. Por defecto <b>False</b>,
        ''' y entonces emitir uno TIRA.
        ''' <para>Un pendiente se re-emite copiando sus bytes, y como no está declarado el
        ''' <see cref="WbFormIdWalker"/> no ve NI UNA de las referencias que tiene adentro. Emitirlo
        ''' así deja esas referencias con los índices de master del archivo FUENTE, o sea un override
        ''' apuntando al plugin equivocado, y sin ningún aviso.</para>
        ''' <para>Sólo el arnés de medición lo prende, para poder MEDIR el round-trip. La app no
        ''' puede.</para></summary>
        Public Property AllowPendingSubrecords As Boolean

        ''' <summary>Devuelve la firma del record al que apunta un FormID, o cadena vacia si no se
        ''' puede resolver.
        ''' <para>Hace falta cuando el tipo de un campo depende de a QUE apunta otro campo del mismo
        ''' record. El caso concreto es el bloque de datos extra de un item: el campo que sigue al
        ''' dueño es una referencia a variable global si el dueño es un NPC, un rango numerico si es
        ''' una faccion, y relleno si no hay dueño. Los tres miden 4 bytes, asi que el archivo sale
        ''' igual en cualquier caso — pero solo uno de los tres es una referencia, y remapear el que
        ''' no es corrompe el dato en silencio.</para>
        ''' <para>Sin resolvedor se elige el relleno, que es la opcion que NO remapea nada.</para>
        ''' </summary>
        Public Property ResolveSignature As Func(Of UInteger, String)

        ''' <summary>Hallazgos del parseo del record en curso. El motor NUNCA absorbe una
        ''' inconsistencia en silencio: todo lo que no tesela, no está declarado o no round-trippea
        ''' entra acá y sale en el reporte.</summary>
        Public ReadOnly Property Findings As New List(Of WbFinding)

        Public Sub Report(kind As WbFindingKind, path As String, message As String)
            Findings.Add(New WbFinding(kind, RecordSignature, path, message))
        End Sub

        Public Sub New(game As WbGame)
            _Game = game
        End Sub
    End Class

    ''' <summary>Clases de hallazgo del motor. Cada una corresponde a un criterio de aceptación.</summary>
    Public Enum WbFindingKind
        ''' <summary>C1: bytes sin consumir, solapes o faltantes dentro de un subrecord.</summary>
        Tessellation = 0
        ''' <summary>C1: un subrecord del record no lo consumió ningún miembro declarado.</summary>
        Unconsumed = 1
        ''' <summary>C1: subrecord declarado explícitamente como PENDIENTE (todavía sin estructura).</summary>
        Pending = 2
        ''' <summary>C2: el round-trip no dio byte-idéntico.</summary>
        RoundTrip = 3
        ''' <summary>Texto cuyo decode→encode no reproduce la fuente (se conservó el crudo del campo).</summary>
        EncodingFallback = 4
        ''' <summary>La declaración no pudo parsear los bytes (excepción de layout).</summary>
        LayoutError = 5
    End Enum

    ''' <summary>Un hallazgo, con la RUTA POR NOMBRE del campo — nunca "el byte 47 difiere".</summary>
    Public NotInheritable Class WbFinding
        Public ReadOnly Property Kind As WbFindingKind
        Public ReadOnly Property RecordSignature As String
        Public ReadOnly Property Path As String
        Public ReadOnly Property Message As String

        Public Sub New(kind As WbFindingKind, recSig As String, path As String, message As String)
            _Kind = kind
            _RecordSignature = recSig
            _Path = path
            _Message = message
        End Sub

        Public Overrides Function ToString() As String
            Return $"[{Kind}] {RecordSignature} {Path}: {Message}"
        End Function
    End Class

    ''' <summary>Base de toda definición. El NOMBRE es parte del contrato: es la ruta con la que el
    ''' diagnóstico por campo (C3) y el remapper identifican un valor.
    ''' <para>Un índice de enum NUNCA puede vivir en un nodo cuyo def sea <see cref="WbFormIdDef"/>:
    ''' el remapper recorre EXCLUSIVAMENTE ese tipo (ver <c>WbFormIdWalker</c>), así que un entero
    ''' con un enum asociado no puede entrar aunque mida los mismos 4 bytes.</para></summary>
    Public MustInherit Class WbDef
        Public Property Name As String = ""
        ''' <summary>Def que CONTIENE a esta. Es la cadena por la que se sube para resolver una
        ''' estructura recursiva (una def que se refiere a sí misma). La fijan los contenedores al
        ''' construirse.</summary>
        Public Property DefParent As WbDef
    End Class

    ''' <summary>Nodo del árbol parseado. Presencia = existencia del nodo: "" y AUSENTE son cosas
    ''' distintas por construcción, sin banderas <c>HasXxx</c> paralelas al valor.</summary>
    Public NotInheritable Class WbNode
        Public ReadOnly Property Def As WbDef
        Public Property Parent As WbNode
        Public ReadOnly Property Children As New List(Of WbNode)
        ''' <summary>Valor tipado de una hoja. Nothing en los contenedores.</summary>
        Public Property Value As Object
        ''' <summary>Firma del subrecord si este nodo ES un subrecord; "" si no.</summary>
        Public Property Signature As String = ""
        ''' <summary>Bytes que el nodo consumió al parsear. Para C1 (teselación) y diagnóstico.</summary>
        Public Property SourceLength As Integer

        ''' <summary>Bytes que el subrecord ocupa en el archivo. Solo lo llevan los nodos de
        ''' subrecord, y se asigna ANTES de parsear su contenido, porque hay campos cuya presencia
        ''' depende de este largo.</summary>
        Public Property DataLength As Integer = -1
        ''' <summary>Para zstrings: cuántos NUL de terminación traía la fuente (0..n). Se re-emiten
        ''' tal cual, así el round-trip no depende de asumir que siempre hay exactamente uno.</summary>
        Public Property TerminatorCount As Integer
        ''' <summary>Rama elegida por una unión (índice en Members). -1 si no es unión.</summary>
        Public Property UnionBranch As Integer = -1
        ''' <summary>Bytes crudos SÓLO para hojas de texto cuyo decode→encode no reproduce la
        ''' fuente (secuencias que el codepage no representa de ida y vuelta). El campo sigue
        ''' declarado, nombrado y con sus bytes contabilizados; lo único que se conserva es la
        ''' forma exacta del texto. Se CUENTA en el reporte: no es un blob ni una copia por
        ''' firma.</summary>
        Public Property RawOverride As Byte()
        ''' <summary>Nombre por posición dentro de un array cuyos elementos tienen nombre propio
        ''' declarado ('Textures', 'Addon Nodes', …). Pisa al de la def.</summary>
        Public Property OverrideName As String
        ''' <summary>Sólo en la RAÍZ: la Form Version con la que se parseó.
        ''' <para>Las ramas de unión que dependen de la versión se ELIGEN en el parseo según ese
        ''' número. Si el consumidor después escribe el header del override con otra versión, los
        ''' bytes de campos como MODT o DAMA quedan en un formato que el header desmiente. El
        ''' escritor verifica que la versión de emisión sea la misma; ver <c>WbWriter.EmitBody</c>.</para>
        ''' <para>-1 = árbol creado desde cero (record nuevo), no hay versión de origen que atar.</para></summary>
        Public Property ParsedFormVersion As Integer = -1
        ''' <summary>La hoja se leyó con MENOS bytes de los que declara su tipo. Se
        ''' re-emite con esa misma cantidad para no cambiar el archivo.</summary>
        Public Property ShortRead As Boolean
        ''' <summary>Cantidad de elementos que el CONTADOR declaraba al parsear.
        ''' <para>Un round-trip NO puede "corregir" nada: hay records cuyo contador ya viene mal en
        ''' el archivo original (hay NPC_ del master de Fallout 4 que declaran <c>COCT = 2</c> y
        ''' traen UN solo <c>CNTO</c>). Recalcularlo siempre cambiaría ese byte. El contador se
        ''' recalcula sólo si la cantidad de elementos CAMBIÓ respecto de lo que decía la fuente —
        ''' o sea, sólo si alguien editó el array.</para>
        ''' <para>-1 = el nodo no vino de un parseo (array creado desde cero).</para></summary>
        Public Property ParsedCount As Integer = -1

        ''' <summary>Sólo en las hojas de REFERENCIA, y sólo cuando la lectura tradujo el valor al
        ''' espacio del orden de carga: qué decía el archivo y a qué se tradujo.
        '''
        ''' <para>La traducción local → orden de carga NO es inyectiva. Un índice de master
        ''' mayor que la cantidad de masters del archivo no existe, y el motor lo pliega al propio
        ''' archivo; dos índices distintos entran así al mismo valor, y la vuelta sólo puede
        ''' devolver la forma canónica. Lo que decía el archivo no está en ninguna otra parte, así
        ''' que lo guarda el nodo.</para>
        '''
        ''' <para>Es la misma política que <see cref="ParsedCount"/>, <see
        ''' cref="TerminatorCount"/> y <see cref="ShortRead"/>: la fuente puede traer una forma que
        ''' no es la canónica y el round-trip no la "corrige"; se vuelve a la canónica sólo si
        ''' alguien EDITÓ el campo.</para>
        '''
        ''' <para>False = el nodo no se leyó de un archivo (record creado desde cero) o se leyó
        ''' sin traducir (inspección de un archivo suelto). En los dos casos el valor ya ES el del
        ''' archivo y no hay nada que restituir.</para></summary>
        Public Property ReferenciaDeArchivo As Boolean
        ''' <summary>El valor TAL CUAL lo trae el archivo, con el índice de master de ESE archivo.
        ''' Sólo vale si <see cref="ReferenciaDeArchivo"/>.</summary>
        Public Property ReferenciaLocalDeArchivo As UInteger
        ''' <summary>El valor del orden de carga al que se tradujo ese local. Sirve para saber si
        ''' alguien cambió a dónde apunta la referencia después de leerla: si el valor actual
        ''' del nodo ya no es éste, el local de origen dejó de describirlo y no se
        ''' restituye.</summary>
        Public Property ReferenciaGlobalDeArchivo As UInteger

        Public Sub New(d As WbDef)
            _Def = d
        End Sub

        Public ReadOnly Property Name As String
            Get
                Return If(String.IsNullOrEmpty(OverrideName), Def.Name, OverrideName)
            End Get
        End Property

        Public Function AddChild(child As WbNode) As WbNode
            child.Parent = Me
            Children.Add(child)
            Return child
        End Function

        ''' <summary>Ruta completa por NOMBRE, estilo <c>ARMO\DATA\Weight</c>. Es lo que imprime el
        ''' reporte de diagnóstico en vez de "el byte 47 difiere".</summary>
        Public ReadOnly Property Path As String
            Get
                Dim parts As New List(Of String)
                Dim n = Me
                While n IsNot Nothing
                    Dim label = If(String.IsNullOrEmpty(n.Name), n.Signature, n.Name)
                    If Not String.IsNullOrEmpty(label) Then parts.Add(label)
                    n = n.Parent
                End While
                parts.Reverse()
                Return String.Join("\", parts)
            End Get
        End Property

        ''' <summary>Primer hijo con ese nombre (comparación ordinal), o Nothing.
        ''' <para>Los contenedores ANÓNIMOS son transparentes: hay structs declarados sin nombre —
        ''' el DATA del ARMO de Fallout 4 no lleva nombre y el de Skyrim sí. Sin la transparencia,
        ''' la MISMA ruta lógica cambiaría según el juego.</para></summary>
        Public Function ByName(n As String) As WbNode
            For Each c In Children
                If String.Equals(c.Name, n, StringComparison.Ordinal) Then Return c
            Next
            For Each c In Children
                If String.IsNullOrEmpty(c.Name) Then
                    Dim deep = c.ByName(n)
                    If deep IsNot Nothing Then Return deep
                End If
            Next
            Return Nothing
        End Function

        ''' <summary>Primer hijo con esa firma de subrecord, o Nothing.</summary>
        Public Function BySignature(sig As String) As WbNode
            For Each c In Children
                If String.Equals(c.Signature, sig, StringComparison.Ordinal) Then Return c
            Next
            Return Nothing
        End Function

        ''' <summary>Resuelve una ruta relativa: <c>Counters\[0]</c>, <c>Flags</c>,
        ''' <c>..\Data\Value</c>. Admite nombres de campo, índices entre corchetes y <c>..</c> para
        ''' subir al padre — lo que necesitan las rutas de contador declaradas en el esquema.</summary>

        ''' <summary>Copia profunda del nodo y de todo lo que cuelga de él.
        '''
        ''' <para>Hace falta para editar sobre una copia sin tocar el original: abrir un editor,
        ''' cancelar y que el record quede como estaba. Copiar el árbol es la forma barata de tener
        ''' ese "deshacer" sin inventar un modelo paralelo.</para></summary>
        Public Function Clonar(Optional padre As WbNode = Nothing) As WbNode
            Dim c As New WbNode(Def) With {
                .Parent = padre,
                .Signature = Signature,
                .SourceLength = SourceLength,
                .DataLength = DataLength,
                .TerminatorCount = TerminatorCount,
                .UnionBranch = UnionBranch,
                .OverrideName = OverrideName,
                .ParsedFormVersion = ParsedFormVersion,
                .ShortRead = ShortRead,
                .ParsedCount = ParsedCount,
                .ReferenciaDeArchivo = ReferenciaDeArchivo,
                .ReferenciaLocalDeArchivo = ReferenciaLocalDeArchivo,
                .ReferenciaGlobalDeArchivo = ReferenciaGlobalDeArchivo
            }
            ' El valor es un tipo simple o un arreglo de bytes; el arreglo se copia para que las dos
            ' ramas no compartan el mismo buffer.
            Dim bytes = TryCast(Value, Byte())
            c.Value = If(bytes IsNot Nothing, DirectCast(bytes.Clone(), Byte()), Value)
            If RawOverride IsNot Nothing Then c.RawOverride = DirectCast(RawOverride.Clone(), Byte())
            For Each h In Children
                c.AddChild(h.Clonar(c))
            Next
            Return c
        End Function

        Public Function ByPath(path As String) As WbNode
            If String.IsNullOrEmpty(path) Then Return Nothing
            Dim cur = Me
            For Each rawStep In path.Split("\"c)
                If cur Is Nothing Then Return Nothing
                Dim s = rawStep.Trim()
                If s.Length = 0 Then Continue For
                If s = ".." Then
                    cur = cur.Parent
                ElseIf s.StartsWith("[", StringComparison.Ordinal) AndAlso s.EndsWith("]", StringComparison.Ordinal) Then
                    Dim idx As Integer
                    If Not Integer.TryParse(s.Substring(1, s.Length - 2), idx) Then Return Nothing
                    If idx < 0 OrElse idx >= cur.Children.Count Then Return Nothing
                    cur = cur.Children(idx)
                Else
                    Dim nxt = cur.ByName(s)
                    If nxt Is Nothing Then
                        nxt = cur.BySignature(s)
                        ' El nodo del subrecord es un envoltorio: su VALOR es el hijo único, así
                        ' que una ruta como XCNT\Swimming Count tiene que atravesarlo para llegar
                        ' al valor. Sin esto no resuelven los contadores que apuntan a una firma.
                        If nxt IsNot Nothing AndAlso nxt.Children.Count = 1 Then nxt = nxt.Children(0)
                    End If
                    cur = nxt
                End If
            Next
            Return cur
        End Function

        ''' <summary>Resuelve la ruta de un CAMPO tal como la nombra la estructura del record:
        ''' <c>CNAM\Color/Index</c>, <c>Model\MODL\Model FileName</c>.
        '''
        ''' <para>Se diferencia de <see cref="ByPath"/> en un punto: no atraviesa el envoltorio de
        ''' un subrecord hasta llegar al final de la ruta. Atravesarlo antes deja los segmentos que
        ''' siguen buscando entre los hijos del valor en vez de entre los del subrecord, y la ruta
        ''' no resuelve.</para>
        '''
        ''' <para>Las dos formas coexisten porque responden preguntas distintas: las rutas de
        ''' contador declaradas en la estructura apuntan a la firma y quieren el valor, mientras que
        ''' un campo se nombra bajando por la estructura completa.</para></summary>
        Public Function ByFieldPath(path As String) As WbNode
            If String.IsNullOrEmpty(path) Then Return Nothing
            Return ResolverCampo(Me, path.Split("\"c), 0)
        End Function

        ''' <summary>Busca el nodo que satisface la ruta ENTERA, no el primero que coincide con un
        ''' tramo.
        '''
        ''' <para>La diferencia importa cuando dos hermanos comparten la firma y sólo se distinguen
        ''' por el nombre de su valor. Pasa de verdad: una raza declara el esqueleto masculino y el
        ''' femenino con la MISMA firma, uno detrás del otro. Quedarse con el primero que coincide
        ''' hacía que la ruta del femenino no resolviera nunca, y leerlo devolvía vacío sin ningún
        ''' aviso — que es exactamente la peor forma de fallar.</para>
        '''
        ''' <para>El orden en que se prueban los candidatos es el mismo de siempre: primero los que
        ''' coinciden por nombre, después los envoltorios sin nombre, y al final los que coinciden
        ''' por firma. Así una ruta que ya resolvía sigue resolviendo al mismo nodo; lo único que
        ''' cambia es que ahora, si ese camino no llega a destino, se prueba el siguiente.</para></summary>
        Private Shared Function ResolverCampo(cur As WbNode, pasos As String(), idx As Integer) As WbNode
            If cur Is Nothing Then Return Nothing
            If idx >= pasos.Length Then Return Desenvolver(cur)

            Dim s = pasos(idx).Trim()
            If s.Length = 0 Then Return ResolverCampo(cur, pasos, idx + 1)
            If s = ".." Then Return ResolverCampo(cur.Parent, pasos, idx + 1)

            If s.StartsWith("[", StringComparison.Ordinal) AndAlso s.EndsWith("]", StringComparison.Ordinal) Then
                Dim i As Integer
                If Not Integer.TryParse(s.Substring(1, s.Length - 2), i) Then Return Nothing
                If i < 0 OrElse i >= cur.Children.Count Then Return Nothing
                Return ResolverCampo(cur.Children(i), pasos, idx + 1)
            End If

            For Each cand In Candidatos(cur, s)
                Dim r = ResolverCampo(cand, pasos, idx + 1)
                If r IsNot Nothing Then Return r
            Next

            ' El segmento puede nombrar al nodo en el que YA estamos. Pasa cuando el elemento de un
            ' arreglo es el subrecord mismo: la ruta del campo arranca con su firma, y desde el
            ' elemento esa firma no es un hijo sino uno mismo.
            If String.Equals(cur.Signature, s, StringComparison.Ordinal) OrElse
               (cur.Def IsNot Nothing AndAlso String.Equals(cur.Def.Name, s, StringComparison.Ordinal)) Then
                Return ResolverCampo(cur, pasos, idx + 1)
            End If

            Return Nothing
        End Function

        ''' <summary>Hijos que pueden ser el tramo <paramref name="s"/>, en orden de preferencia.
        ''' <para>Un envoltorio sin nombre no es un candidato en sí: se lo atraviesa y se prueba el
        ''' mismo tramo contra lo que tiene adentro.</para></summary>
        Private Shared Iterator Function Candidatos(cur As WbNode, s As String) As IEnumerable(Of WbNode)
            For Each c In cur.Children
                If String.Equals(c.Name, s, StringComparison.Ordinal) Then Yield c
            Next
            For Each c In cur.Children
                If String.IsNullOrEmpty(c.Name) Then
                    For Each d In Candidatos(c, s)
                        Yield d
                    Next
                End If
            Next
            For Each c In cur.Children
                If String.Equals(c.Signature, s, StringComparison.Ordinal) AndAlso
                   Not String.Equals(c.Name, s, StringComparison.Ordinal) Then Yield c
            Next
        End Function

        ''' <summary>Si la ruta terminó en el envoltorio de un subrecord, lo que se busca es su
        ''' valor.
        ''' <para>Se exige que sea un subrecord y no cualquier nodo con un hijo: un ARREGLO DE UN
        ''' SOLO ELEMENTO también tiene un hijo, y devolver ese elemento en su lugar deja la lista
        ''' vacía para quien pidió el arreglo.</para></summary>
        Private Shared Function Desenvolver(cur As WbNode) As WbNode
            If cur IsNot Nothing AndAlso TypeOf cur.Def Is WbSubrecordDef AndAlso
               cur.Children.Count = 1 AndAlso cur.Children(0).Children.Count = 0 Then
                Return cur.Children(0)
            End If
            Return cur
        End Function

        ''' <summary>Recorre el árbol en pre-orden.</summary>
        Public Iterator Function Walk() As IEnumerable(Of WbNode)
            Yield Me
            For Each c In Children
                For Each d In c.Walk()
                    Yield d
                Next
            Next
        End Function
    End Class

End Namespace
