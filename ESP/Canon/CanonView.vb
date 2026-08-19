Imports System.Runtime.CompilerServices

Namespace Canon

    ''' <summary>Base de las vistas tipadas de un record.
    '''
    ''' <para>Una vista NO copia nada: envuelve el árbol de campos y cada propiedad lee y escribe
    ''' directamente sobre él. Eso es lo que evita tener dos representaciones del mismo record —
    ''' una para leer y otra para escribir— que puedan desincronizarse.</para>
    '''
    ''' <para>Consecuencia práctica: editar una propiedad de la vista y después emitir el árbol
    ''' produce el archivo con ese cambio y nada más. No hay un paso intermedio donde alguien tenga
    ''' que acordarse de volcar los campos de la vista a otra estructura.</para></summary>
    Public MustInherit Class CanonView

        ''' <summary>El árbol del record. Es el único estado de la vista.</summary>
        Public ReadOnly Property Node As WbNode

        ''' <summary>Avisos de la lectura: campos que no cubrieron todos los bytes, subrecords que la
        ''' estructura no supo ubicar. Nunca impiden leer; están para poder mirarlos.</summary>
        Public ReadOnly Property Context As WbContext

        Protected Sub New(node As WbNode, ctx As WbContext)
            _Node = node
            _Context = ctx
        End Sub

        Public ReadOnly Property IsEmpty As Boolean
            Get
                Return Node Is Nothing
            End Get
        End Property

        '==========================================================================================
        ' Lectura. Un campo ausente devuelve el valor por defecto del tipo: un record que no trae
        ' cierto subrecord simplemente no tiene ese dato.
        '==========================================================================================

        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Protected Function N(path As String) As WbNode
            Return CanonBridge.Find(Node, path)
        End Function

        Protected Function U32(path As String) As UInteger
            Return CanonBridge.U32(Node, path)
        End Function

        Protected Function U16(path As String) As UShort
            Return CUShort(CanonBridge.I64(Node, path) And &HFFFFL)
        End Function

        Protected Function U8(path As String) As Byte
            Return CByte(CanonBridge.I64(Node, path) And &HFFL)
        End Function

        Protected Function I32(path As String) As Integer
            Return CInt(CanonBridge.I64(Node, path))
        End Function

        Protected Function Flt(path As String) As Single
            Return CanonBridge.Flt(Node, path)
        End Function

        Protected Function Txt(path As String) As String
            Return CanonBridge.Txt(Node, path)
        End Function

        Protected Function Bool(path As String) As Boolean
            Return CanonBridge.I64(Node, path) <> 0L
        End Function

        ''' <summary>True si el campo está presente en el record, más allá de su valor. Distingue
        ''' "vale cero" de "no está", que para varios campos son cosas distintas.</summary>
        Protected Function Present(path As String) As Boolean
            Return CanonBridge.Has(Node, path)
        End Function

        '==========================================================================================
        ' Escritura. Escribe sobre el árbol; si el campo no existe todavía se crea el subrecord que
        ' lo contiene en la posición que le corresponde en la estructura.
        '==========================================================================================

        ''' <summary>Escribe un campo. Devuelve False si el campo no existe en el record: crear un
        ''' subrecord nuevo tiene que respetar la posición que le da la estructura, y eso se pide
        ''' aparte.</summary>
        Protected Function Put(path As String, value As Object) As Boolean
            Return WbEdit.SetValue(Node, path, value)
        End Function

        '==========================================================================================
        ' Listas. Devuelven los nodos hijos de un arreglo para que la vista concreta los proyecte.
        '==========================================================================================

        Protected Function Items(path As String) As IList(Of WbNode)
            Dim n = CanonBridge.Find(Node, path)
            If n Is Nothing Then Return Array.Empty(Of WbNode)()
            Return n.Children
        End Function

        ''' <summary>Todos los subrecords del record con esa firma, en orden. Sirve para los campos
        ''' que en la estructura son un arreglo suelto de subrecords repetidos.</summary>
        Protected Function Repeated(signature As String) As IEnumerable(Of WbNode)
            If Node Is Nothing Then Return Array.Empty(Of WbNode)()
            Return Node.Walk().Where(Function(x) String.Equals(x.Signature, signature, StringComparison.Ordinal))
        End Function

    End Class

End Namespace
