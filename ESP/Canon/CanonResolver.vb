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
    ''' <para>Antes cada campo hacía su propia llamada: más de trescientas repeticiones de estas dos
    ''' reglas repartidas por los parsers. Acá se aplican una vez, en la propiedad que devuelve el
    ''' campo, y por eso no pueden quedar sitios que se las olviden.</para></summary>
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

        ''' <summary>Sin gestor de plugins no hay orden de carga contra el cual traducir, así que el
        ''' FormID vuelve crudo. Es la misma política que tenían los parsers: es preferible un valor
        ''' local reconocible a uno traducido con una tabla que no está.</summary>
        Public Function GlobalId(raw As UInteger) As UInteger
            If raw = 0UI OrElse _plugins Is Nothing OrElse _rec Is Nothing Then Return raw
            Return _plugins.ResolveReferencedFormID(_rec.SourcePluginName, raw)
        End Function

        ''' <summary>Texto de un campo traducible.
        ''' <para>El árbol guarda lo que había en el record: el texto mismo cuando el archivo no usa
        ''' tablas externas, o el identificador numérico cuando sí las usa. El identificador cero
        ''' significa "sin texto", no un error.</para></summary>
        Public Function Text(node As WbNode,
                             Optional kind As LocalizedStringTableKind = LocalizedStringTableKind.Strings) As String
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
            Return _plugins.ResolveLocalizedString(_rec.SourcePluginName, id, kind)
        End Function

    End Class

End Namespace
