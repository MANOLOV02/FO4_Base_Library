Namespace Canon

    ''' <summary>Puerta única entre un record leído del archivo y su árbol de campos.
    '''
    ''' <para>Los parsers de la aplicación entregan vistas planas y tipadas (<c>RACE_Data</c>,
    ''' <c>NPC_Data</c>, …) que consume el resto del código. Lo que cambia con este puente es de
    ''' dónde salen esos valores: en vez de recorrer los subrecords a mano y decidir por firma, se
    ''' arma el árbol una vez y cada campo se lee por su nombre.</para>
    '''
    ''' <para>El juego sale de la sesión, no de cada llamada: la estructura de un record depende del
    ''' juego, y dejar que cada sitio lo decida por su cuenta es exactamente cómo se separan un
    ''' lector y un escritor que deberían leer lo mismo.</para></summary>
    Public Module CanonBridge

        ''' <summary>Juego de la sesión, traducido al que entiende el motor.</summary>
        Public Function SessionGame() As WbGame
            If Config_App.Current IsNot Nothing AndAlso
               Config_App.Current.Game = Config_App.Game_Enum.Fallout4 Then
                Return WbGame.Fallout4
            End If
            Return WbGame.Skyrim
        End Function

        ''' <summary>Contexto de lectura de un record: versión de formato, si el plugin usa tablas de
        ''' texto externas y con qué codificación se leen los textos traducibles.</summary>
        Public Function ContextFor(rec As PluginRecord, game As WbGame) As WbContext
            Return New WbContext(game) With {
                .FormVersion = rec.Header.Version,
                .Localized = rec.SourcePluginIsLocalized,
                .TranslatableEncoding = rec.SourcePluginTranslatableEncoding,
                .RecordSignature = rec.Header.Signature,
                .RecordFlags = rec.Header.Flags,
                .EditorId = rec.EditorID
            }
        End Function

        ''' <summary>Árbol de campos del record, o Nothing si el juego no declara ese tipo de record.
        ''' <para>Los avisos de cobertura quedan en el contexto: quien quiera saber si la lectura
        ''' explicó todos los bytes puede mirarlos, pero un campo que falta nunca hace fallar la
        ''' lectura — devuelve el valor por defecto de la vista, igual que antes.</para></summary>
        Public Function Tree(rec As PluginRecord, ByRef ctx As WbContext) As WbNode
            If rec Is Nothing OrElse rec.Header.Signature Is Nothing Then Return Nothing
            Dim game = SessionGame()
            Dim def = WbSchema.Get(game, rec.Header.Signature)
            If def Is Nothing Then Return Nothing
            ctx = ContextFor(rec, game)
            Return WbReader.Parse(def, rec, ctx)
        End Function

        ''' <summary>Igual que <see cref="Tree"/> cuando no interesa inspeccionar los avisos.</summary>
        Public Function Tree(rec As PluginRecord) As WbNode
            Dim ctx As WbContext = Nothing
            Return Tree(rec, ctx)
        End Function

        '==========================================================================================
        ' Lectura de campos por nombre. Devuelven el valor por defecto cuando el campo no está, que
        ' es la misma política que tenían los parsers planos: un subrecord ausente deja el campo de
        ' la vista en su valor inicial.
        '==========================================================================================

        Public Function U32(node As WbNode, path As String) As UInteger
            Dim n = Find(node, path)
            If n Is Nothing OrElse n.Value Is Nothing Then Return 0UI
            Try
                Return CUInt(Convert.ToInt64(n.Value) And &HFFFFFFFFL)
            Catch
                Return 0UI
            End Try
        End Function

        Public Function I64(node As WbNode, path As String) As Long
            Dim n = Find(node, path)
            If n Is Nothing OrElse n.Value Is Nothing Then Return 0L
            Try
                Return Convert.ToInt64(n.Value)
            Catch
                Return 0L
            End Try
        End Function

        Public Function Flt(node As WbNode, path As String) As Single
            Dim n = Find(node, path)
            If n Is Nothing OrElse n.Value Is Nothing Then Return 0.0F
            Try
                Return Convert.ToSingle(n.Value)
            Catch
                Return 0.0F
            End Try
        End Function

        Public Function Txt(node As WbNode, path As String) As String
            Dim n = Find(node, path)
            If n Is Nothing OrElse n.Value Is Nothing Then Return ""
            Return Convert.ToString(n.Value)
        End Function

        ''' <summary>True si el campo existe en el árbol, sin importar su valor.</summary>
        Public Function Has(node As WbNode, path As String) As Boolean
            Return Find(node, path) IsNot Nothing
        End Function

        ''' <summary>Busca por ruta y, si no la encuentra, por firma de subrecord. Las vistas planas
        ''' se escribieron pensando en firmas, así que aceptar las dos formas evita reescribir la
        ''' proyección cuando un campo está dentro de un grupo.</summary>
        Public Function Find(node As WbNode, path As String) As WbNode
            If node Is Nothing OrElse String.IsNullOrEmpty(path) Then Return Nothing
            Dim n = node.ByPath(path)
            If n IsNot Nothing Then Return n
            If path.Length = 4 Then Return node.BySignature(path)
            Return Nothing
        End Function

    End Module

End Namespace
