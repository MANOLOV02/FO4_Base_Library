Namespace Canon

    ''' <summary>Las dos traducciones que necesita cualquier campo leído de un record, aplicadas en
    ''' un solo lugar.
    '''
    ''' <para><b>Referencias.</b> El FormID guardado en un record es LOCAL al archivo: su byte alto
    ''' es un índice dentro de la lista de masters de ese archivo. Para que sirva fuera hay que
    ''' traducirlo al índice que ese master tiene en el orden de carga. Sin esa traducción una
    ''' referencia apunta al archivo equivocado.</para>
    '''
    ''' <para><b>Textos.</b> Un archivo puede guardar los textos en tablas externas y dejar en el
    ''' record sólo un identificador. Resolverlo depende del archivo y del idioma.</para>
    '''
    ''' <para>⛔ NO aplicar estas dos reglas campo por campo: son más de trescientas repeticiones
    ''' repartidas por los parsers, y el sitio que se las olvida no avisa. Acá se aplican una vez, en
    ''' la propiedad que devuelve el campo.</para></summary>
    Public NotInheritable Class CanonResolver

        Private ReadOnly _rec As PluginRecord
        Private ReadOnly _plugins As PluginManager

        Public Sub New(rec As PluginRecord, plugins As PluginManager)
            _rec = rec
            _plugins = plugins
        End Sub

        Public ReadOnly Property Record As PluginRecord
            Get
                Return _rec
            End Get
        End Property

        ' ⛔ NO reponer acá un GlobalId(raw) que traduzca una referencia al orden de carga: sería una
        ' TERCERA copia de la ley que vive en PluginManager.ResolveReferenciaNoLock.
        ' El árbol ya viene con las referencias traducidas (CanonBridge.NormalizarReferencias), así
        ' que una vista NO tiene que traducir nada al leer un campo.

        ''' <summary>Texto de un campo traducible.
        ''' <para>El árbol guarda lo que había en el record: el texto mismo cuando el archivo no usa
        ''' tablas externas, o el identificador numérico cuando sí las usa. El identificador cero
        ''' significa "sin texto", no un error.</para>
        ''' <para>De qué tabla sale el texto NO está en el identificador: lo decide el par (record,
        ''' subrecord), y se saca del propio nodo. Pasarle siempre la tabla general hace que toda
        ''' descripción vuelva vacía, porque las descripciones viven en otra.</para></summary>
        Public Function Text(node As WbNode,
                             Optional kind As LocalizedStringTableKind? = Nothing) As String
            If node Is Nothing OrElse node.Value Is Nothing Then Return ""
            If TypeOf node.Value Is String Then Return CStr(node.Value)

            Dim id As UInteger
            Try
                id = CUInt(Convert.ToInt64(node.Value) And &HFFFFFFFFL)
            Catch
                Return ""
            End Try
            If id = 0UI Then Return ""
            If _plugins Is Nothing OrElse _rec Is Nothing Then Return ""

            Dim tabla = If(kind.HasValue, kind.Value, TablaDe(node))
            Return _plugins.ResolveLocalizedString(_rec.SourcePluginName, id, tabla)
        End Function

        ''' <summary>Como <see cref="Text"/>, pero False si el identificador no esta en la tabla (un texto vacio de la
        ''' tabla da True y "").</summary>
        Public Function TryText(node As WbNode, ByRef value As String) As Boolean
            value = ""
            If node Is Nothing OrElse node.Value Is Nothing Then Return True
            If TypeOf node.Value Is String Then value = CStr(node.Value) : Return True
            Dim id As UInteger
            Try
                id = CUInt(Convert.ToInt64(node.Value) And &HFFFFFFFFL)
            Catch
                Return True
            End Try
            If id = 0UI Then Return True
            If _plugins Is Nothing OrElse _rec Is Nothing Then Return False
            Return _plugins.TryResolveLocalizedString(_rec.SourcePluginName, id, TablaDe(node), value)
        End Function

        ''' <summary>⛔⛔ EN QUÉ TABLA VIVE EL TEXTO DE UN CAMPO — <b>LA SEDE ÚNICA DE ESA LEY</b>.
        ''' <para>Depende del par (tipo de record, subrecord), no del valor. Son cuatro casos y el
        ''' resto va a la tabla general.</para>
        ''' <para>⛔ ES <c>Public Shared</c> Y TOMA LAS DOS FIRMAS, no un nodo, A PROPÓSITO: el que necesita
        ''' la ley no siempre tiene un <see cref="WbNode"/> en la mano. El caso medido es
        ''' <c>Tools\StringsResolverGate</c>, que recorre subrecords crudos y llamaba a
        ''' <c>PluginManager.ResolveFieldString</c> <b>sin</b> la tabla — o sea, pidiendo SIEMPRE la general.
        ''' MEDIDO el 21-sep: con la tabla general daba <b>3.800</b> textos «rotos» en Fallout 4 y <b>2.150</b>
        ''' en Skyrim; con esta ley, <b>7</b> y <b>32</b>. Las otras 3.793 resuelven a TEXTO REAL (se verificó
        ''' que pasan a texto y no a vacío), o sea que el 99,8 % de ese rojo lo fabricaba el llamador.</para>
        ''' <para>⛔ Y POR ESO NO SE COPIA: un segundo dueño de esta tabla es un dueño que el día que la ley
        ''' cambie se queda atrás y miente en silencio — que es exactamente lo que pasó. El que la necesite la
        ''' LLAMA.</para></summary>
        Public Shared Function TablaDeCampo(recSig As String, subSig As String) As LocalizedStringTableKind
            If subSig = "DESC" AndAlso recSig <> "LSCR" Then Return LocalizedStringTableKind.DLStrings
            If subSig = "CNAM" AndAlso (recSig = "QUST" OrElse recSig = "BOOK") Then Return LocalizedStringTableKind.DLStrings
            If recSig = "INFO" AndAlso subSig <> "RNAM" Then Return LocalizedStringTableKind.ILStrings
            Return LocalizedStringTableKind.Strings
        End Function

        ''' <summary>La misma ley, para el que SÍ tiene el nodo. ⛔ No repite las condiciones: las delega en
        ''' <see cref="TablaDeCampo"/>. Lo único suyo es de dónde saca las dos firmas.</summary>
        Private Function TablaDe(node As WbNode) As LocalizedStringTableKind
            Return TablaDeCampo(If(_rec Is Nothing, "", _rec.Header.Signature), FirmaDelSubrecord(node))
        End Function

        ''' <summary>Firma del subrecord del que cuelga el nodo, subiendo hasta encontrarla.</summary>
        Private Shared Function FirmaDelSubrecord(node As WbNode) As String
            Dim n = node
            While n IsNot Nothing
                If Not String.IsNullOrEmpty(n.Signature) Then Return n.Signature
                n = n.Parent
            End While
            Return ""
        End Function

    End Class

End Namespace
