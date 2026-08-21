Namespace Canon

    ''' <summary>Base de las vistas de un record.
    '''
    ''' <para>Una vista NO copia nada: envuelve el árbol de campos y cada propiedad lo consulta en el
    ''' momento. Eso es lo que evita tener dos representaciones del mismo record —una para leer y
    ''' otra para escribir— que puedan desincronizarse.</para>
    '''
    ''' <para>Las clases concretas están generadas a partir de la declaración del formato: el nombre
    ''' de cada propiedad es el nombre del campo. Acá viven sólo las operaciones comunes a todas.</para></summary>
    Public MustInherit Class CanonView

        ''' <summary>El árbol del record. Es el único estado de la vista.</summary>
        Public ReadOnly Property Node As WbNode

        ''' <summary>Avisos de la lectura: campos que no cubrieron todos los bytes, subrecords que la
        ''' estructura no supo ubicar. Nunca impiden leer; están para poder mirarlos.</summary>
        Public ReadOnly Property Context As WbContext

        ''' <summary>Traduce referencias al orden de carga y resuelve los textos de tablas externas.</summary>
        Public ReadOnly Property Resolver As CanonResolver

        Protected Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            _Node = node
            _Context = ctx
            _Resolver = resolver
        End Sub

        ''' <summary>Identificador del record.</summary>
        Public ReadOnly Property FormID As UInteger
            Get
                If Context Is Nothing Then Return 0UI
                Return Context.FormID
            End Get
        End Property

        ''' <summary>Banderas de la CABECERA del record.
        '''
        ''' <para>No salen del árbol: la cabecera son 24 bytes que van delante de los subrecords y
        ''' llevan cosas que valen para el record entero — si está borrado, si es una plantilla, si
        ''' trae datos de escultura. Varios campos que la aplicación usa salen de acá y no de un
        ''' campo, y por eso hay que poder preguntarlo desde la vista.</para></summary>
        Public ReadOnly Property RecordFlags As UInteger
            Get
                If Context Is Nothing Then Return 0UI
                Return Context.RecordFlags
            End Get
        End Property

        ''' <summary>Un bit con nombre de las banderas de la cabecera.</summary>
        Public Function BanderaDeCabecera(bitIndex As Integer) As Boolean
            Return (RecordFlags And (1UI << bitIndex)) <> 0UI
        End Function

        ''' <summary>Cambia un bit de las banderas de la cabecera.
        ''' <para>La cabecera no vive en el árbol de campos, pero SÍ es parte del record: decide, entre
        ''' otras cosas, si una armadura es no-jugable o si un complemento trae datos de escultura.
        ''' Vive en el contexto, que se lee del record de origen al abrirlo y es lo que el grabado
        ''' emite, así que editarlo acá es editar lo que se va a guardar.</para></summary>
        Public Sub PonerBanderaDeCabecera(bitIndex As Integer, valor As Boolean)
            If Context Is Nothing Then Return
            Dim mascara = 1UI << bitIndex
            If valor Then
                Context.RecordFlags = Context.RecordFlags Or mascara
            Else
                Context.RecordFlags = Context.RecordFlags And Not mascara
            End If
        End Sub

        Public ReadOnly Property IsEmpty As Boolean
            Get
                Return Node Is Nothing
            End Get
        End Property

        '==========================================================================================
        ' Lectura de campos. Un campo ausente devuelve el valor por defecto de su tipo: un record que
        ' no trae cierto subrecord simplemente no tiene ese dato.
        '==========================================================================================

        Protected Function Entero(ruta As String) As Long
            Dim n = CanonBridge.Find(Node, ruta)
            If n Is Nothing OrElse n.Value Is Nothing Then Return 0L
            Return CanonBridge.AEntero(n.Value)
        End Function

        Protected Function Flt(ruta As String) As Single
            Return CanonBridge.Flt(Node, ruta)
        End Function

        Protected Function Txt(ruta As String) As String
            Return CanonBridge.Txt(Node, ruta)
        End Function

        Protected Function Bytes(ruta As String) As Byte()
            Dim n = CanonBridge.Find(Node, ruta)
            Dim b = TryCast(If(n Is Nothing, Nothing, n.Value), Byte())
            Return If(b, Array.Empty(Of Byte)())
        End Function

        ''' <summary>Referencia a otro record. El árbol ya viene con las referencias en el espacio
        ''' del orden de carga, así que leer y escribir usan el mismo valor.</summary>
        ''' <summary>Pone o saca el campo de esa ruta. Con False lo deja AUSENTE; con True se
        ''' asegura de que exista, con su valor por defecto.</summary>
        Protected Sub PonerPresencia(ruta As String, presente As Boolean)
            If Node Is Nothing Then Return
            If presente Then
                WbEdit.EnsureFieldPath(Node, Context, ruta)
            Else
                WbEdit.QuitarCampo(Node, ruta)
            End If
        End Sub

        Protected Function Referencia(ruta As String) As UInteger
            Return CanonBridge.U32(Node, ruta)
        End Function

        Protected Sub PonerReferencia(ruta As String, valor As UInteger)
            Escribir(ruta, CLng(valor))
        End Sub

        ''' <summary>Texto de un campo traducible. Si el archivo guarda los textos en tablas
        ''' externas, el record sólo tiene un identificador y se resuelve contra ellas.</summary>
        Protected Function TextoTraducible(ruta As String) As String
            Dim n = CanonBridge.Find(Node, ruta)
            If n Is Nothing Then Return ""
            If Resolver Is Nothing Then Return If(n.Value Is Nothing, "", Convert.ToString(n.Value))
            Return Resolver.Text(n)
        End Function

        ''' <summary>Un bit con nombre de un campo de banderas.</summary>
        Protected Function Bit(ruta As String, bitIndex As Integer) As Boolean
            Return (Entero(ruta) And (1L << bitIndex)) <> 0L
        End Function

        ''' <summary>Nombre del valor de un campo enumerado, o el número si no está declarado.</summary>
        Protected Function NombreDeValor(ruta As String) As String
            Dim n = CanonBridge.Find(Node, ruta)
            If n Is Nothing OrElse n.Value Is Nothing Then Return ""
            Dim def = TryCast(n.Def, WbIntegerDef)
            Dim v As Long
            Try
                v = Convert.ToInt64(n.Value)
            Catch
                Return ""
            End Try
            Dim nombre As String = Nothing
            If def IsNot Nothing AndAlso def.EnumValues IsNot Nothing AndAlso
               def.EnumValues.TryGetValue(v, nombre) AndAlso Not String.IsNullOrEmpty(nombre) Then
                Return nombre
            End If
            Return v.ToString()
        End Function

        ''' <summary>Campo presente en el record, más allá de su valor. Distingue "vale cero" de
        ''' "no está", que para varios campos son cosas distintas.</summary>
        Protected Function Presente(ruta As String) As Boolean
            Return CanonBridge.Has(Node, ruta)
        End Function

        '==========================================================================================
        ' Escritura. Escribe sobre el mismo árbol que se leyó: no hay una copia aparte que alguien
        ' tenga que acordarse de volcar. Guardar el record emite exactamente lo que se ve acá.
        '==========================================================================================

        ''' <summary>Escribe una hoja, creando el campo si el record todavía no lo trae.
        '''
        ''' <para>Resuelve la ruta con EXACTAMENTE la misma búsqueda con la que se lee. Tenerlas
        ''' distintas daba el peor resultado posible: un campo que se podía leer y no escribir, sin
        ''' error y sin aviso — el valor simplemente no aparecía, y el campo hasta figuraba como
        ''' presente.</para>
        '''
        ''' <para>Sólo se crea lo que se escribe, así que mirar un record no le agrega nada.</para></summary>
        Protected Function Escribir(ruta As String, valor As Object) As Boolean
            If Node Is Nothing Then Return False
            Dim n = Node.ByFieldPath(ruta)
            ' Escribir un campo que no está LO CREA, cualquiera sea el valor.
            '
            ' Tuve una guarda que no lo creaba cuando el valor era el de por defecto, para que una
            ' ida y vuelta por un editor no le agregara subrecords vacíos a un record que no los
            ' traía. Estaba mal: hay campos cuyo valor normal ES cero —el índice del complemento base
            ' de una armadura, sin ir más lejos— y se perdían en silencio. Que un editor no reescriba
            ' lo que no tocó es problema del editor, no de acá.
            If n Is Nothing Then n = Asegurar(ruta)
            Return WbEdit.PonerValor(n, valor)
        End Function

        ''' <summary>Enciende o apaga un MARCADOR: un subrecord vacío cuyo dato es estar o no estar.
        ''' <para>No tiene valor que escribir, así que ponerlo en verdadero es crearlo y en falso es
        ''' sacarlo. Sin esto no había forma de marcar, por ejemplo, que una combinación de armadura es
        ''' de sólo-editor.</para></summary>
        Protected Function PonerMarcador(ruta As String, encendido As Boolean) As Boolean
            If Node Is Nothing Then Return False
            If encendido Then Return Asegurar(ruta) IsNot Nothing

            Dim n = Node.ByFieldPath(ruta)
            If n Is Nothing Then Return True
            ' Lo que hay que sacar es el SUBRECORD entero, no la hoja: se sube hasta el nodo que
            ' lleva la firma.
            While n IsNot Nothing AndAlso String.IsNullOrEmpty(n.Signature)
                n = n.Parent
            End While
            If n Is Nothing OrElse n.Parent Is Nothing Then Return False
            Return n.Parent.QuitarHijo(n)
        End Function

        ''' <summary>El nodo de esa ruta, creándolo si falta. Devuelve Nothing si el formato no
        ''' declara ese campo para este tipo de record.</summary>
        Protected Function Asegurar(ruta As String) As WbNode
            If Node Is Nothing OrElse Context Is Nothing Then Return Nothing
            Return WbEdit.EnsureFieldPath(Node, Context, ruta)
        End Function

        ''' <summary>Enciende o apaga un bit con nombre de un campo de banderas.
        ''' <para>Si el campo no está en el record y el resultado sería cero, no se escribe nada: apagar
        ''' un bit que ya estaba apagado no puede AGREGARLE un subrecord vacío a un record que no lo
        ''' traía. Escribir crea el campo cuando falta, y ese es justamente el caso en que no
        ''' corresponde.</para></summary>
        Protected Sub PonerBit(ruta As String, bitIndex As Integer, encendido As Boolean)
            Dim actual = Entero(ruta)
            Dim mascara = 1L << bitIndex
            Dim nuevo = If(encendido, actual Or mascara, actual And Not mascara)
            If nuevo = actual AndAlso Not Presente(ruta) Then Return
            Escribir(ruta, nuevo)
        End Sub

        ''' <summary>Escribe un texto traducible. Si el archivo usa tablas externas el campo guarda
        ''' un identificador, no el texto, y cambiarlo requiere darlo de alta en esa tabla: por eso
        ''' acá se rechaza en vez de escribir algo que el juego no podría mostrar.</summary>
        Protected Function EscribirTextoTraducible(ruta As String, valor As String) As Boolean
            If Context IsNot Nothing AndAlso Context.Localized Then Return False
            Return Escribir(ruta, valor)
        End Function

        '==========================================================================================
        ' Arreglos
        '==========================================================================================

        ''' <summary>Elementos de un arreglo, cada uno envuelto en su propia vista. La lista se arma
        ''' al pedirla; el árbol sigue siendo el único dueño de los datos.</summary>
        Protected Function Elementos(Of T As CanonView)(ruta As String, envolver As Func(Of WbNode, T)) As IReadOnlyList(Of T)
            Dim cont = CanonBridge.Find(Node, ruta)
            If cont Is Nothing Then Return Array.Empty(Of T)()
            cont = Contenedor(cont)
            Dim salida As New List(Of T)(cont.Children.Count)
            For Each hijo In cont.Children
                salida.Add(envolver(hijo))
            Next
            Return salida
        End Function

        ''' <summary>El nodo que REALMENTE tiene los elementos.
        ''' <para>Un arreglo puede estar declarado sin nombre, y entonces queda un nodo intermedio entre
        ''' el subrecord y los elementos. Quedarse con el de arriba devuelve UN elemento —el envoltorio—
        ''' en vez de los que hay, y leer un campo de ese único elemento devuelve el del primero: una
        ''' lista de dos entradas se lee como una sola, sin ningún aviso.</para></summary>
        Private Shared Function Contenedor(n As WbNode) As WbNode
            Dim cur = n
            While cur.Children.Count = 1
                Dim solo = cur.Children(0)
                Dim esArreglo = TypeOf solo.Def Is WbArrayDef OrElse TypeOf solo.Def Is WbRArrayDef
                If Not esArreglo OrElse Not String.IsNullOrEmpty(solo.Def.Name) Then Exit While
                cur = solo
            End While
            Return cur
        End Function

    End Class

End Namespace
