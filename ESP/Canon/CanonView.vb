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
            Try
                Return Convert.ToInt64(n.Value)
            Catch
                Return 0L
            End Try
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

        ''' <summary>Escribe una hoja. Devuelve False si el campo no está presente en el record:
        ''' crear un subrecord que no existe cambia la forma del record y se pide aparte.</summary>
        Protected Function Escribir(ruta As String, valor As Object) As Boolean
            Return WbEdit.SetValue(Node, ruta, valor)
        End Function

        ''' <summary>Enciende o apaga un bit con nombre de un campo de banderas.</summary>
        Protected Sub PonerBit(ruta As String, bitIndex As Integer, encendido As Boolean)
            Dim actual = Entero(ruta)
            Dim mascara = 1L << bitIndex
            Escribir(ruta, If(encendido, actual Or mascara, actual And Not mascara))
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
            Dim salida As New List(Of T)(cont.Children.Count)
            For Each hijo In cont.Children
                salida.Add(envolver(hijo))
            Next
            Return salida
        End Function

    End Class

End Namespace
