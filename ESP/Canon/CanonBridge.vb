Namespace Canon

    ''' <summary>Puerta única entre un record leído del archivo y su árbol de campos.
    '''
    ''' <para>Los parsers de la aplicación entregan vistas planas y tipadas (<c>NPC_Data</c>, …) que
    ''' consume el resto del código. Lo que cambia con este puente es de
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
                .FormID = rec.Header.FormID,
                .RecordFlags = rec.Header.Flags,
                .EditorId = rec.EditorID
            }
        End Function

        ''' <summary>Árbol de campos del record, o Nothing si el juego no declara ese tipo de record.
        ''' <para>Los avisos de cobertura quedan en el contexto: quien quiera saber si la lectura
        ''' explicó todos los bytes puede mirarlos, pero un campo que falta nunca hace fallar la
        ''' lectura — devuelve el valor por defecto de la vista.</para></summary>
        Public Function Tree(rec As PluginRecord, plugins As PluginManager, ByRef ctx As WbContext) As WbNode
            If rec Is Nothing OrElse rec.Header.Signature Is Nothing Then Return Nothing
            Dim game = SessionGame()
            Dim def = WbSchema.Get(game, rec.Header.Signature)
            If def Is Nothing Then Return Nothing
            ctx = ContextFor(rec, game)
            Dim raiz = WbReader.Parse(def, rec, ctx)
            NormalizarReferencias(raiz, rec, plugins)
            Return raiz
        End Function

        ''' <summary>Igual que <see cref="Tree"/> cuando no interesa inspeccionar los avisos.</summary>
        Public Function Tree(rec As PluginRecord, plugins As PluginManager) As WbNode
            Dim ctx As WbContext = Nothing
            Return Tree(rec, plugins, ctx)
        End Function

        ''' <summary>Pasa TODAS las referencias del record de locales al archivo a globales del
        ''' orden de carga, de una sola vez.
        '''
        ''' <para>El FormID guardado en un record es local: su byte alto es un indice dentro de la
        ''' lista de masters de ESE archivo. Dos archivos distintos usan el mismo numero para cosas
        ''' distintas, asi que fuera del archivo un FormID local no significa nada.</para>
        '''
        ''' <para>Se hace al leer y no en cada campo por dos razones. La primera es que asi leer y
        ''' escribir son simetricos: la propiedad devuelve lo mismo que acepta, y guardar lo que se
        ''' leyo no puede corromper la referencia. La segunda es que el paso inverso ya existe y
        ''' ocurre una sola vez, al escribir, cuando se sabe cual es el archivo destino.</para>
        '''
        ''' <para>Sin gestor de plugins no hay orden de carga contra el cual traducir y el arbol
        ''' queda con los valores del archivo. Es lo correcto para inspeccionar un archivo suelto.</para>
        '''
        ''' <para>La traduccion NO es reversible por si sola: un indice de master que el archivo no
        ''' tiene se pliega al propio archivo, igual que el indice canonico de "propio", y el camino
        ''' inverso solo puede devolver el canonico. Por eso la pasada deja anotado en cada nodo que
        ''' decia el archivo; ver <see cref="WbFormIdWalker.NormalizarDesdeArchivo"/>.</para>
        '''
        ''' <para>El plugin de origen se resuelve UNA vez y el lock de lectura se toma UNA vez, para
        ''' todo el record. ⛔ NO tomar el lock ni buscar el plugin por NOMBRE por REFERENCIA: el arbol
        ''' de los NPC de un orden de carga real tiene 203 mil referencias.</para>
        '''
        ''' <para>La LEY no se re-escribe: se llama a <c>PluginManager.ResolveReferenciaNoLock</c>,
        ''' que es la misma que usa el camino con lock. Un mapa "indice de master -> byte alto"
        ''' habria sido una segunda implementacion Y ADEMAS estaria mal: para un dueno light el
        ''' resultado depende del object id, y por debajo de 0x800 la ley tambien.</para>
        ''' </summary>
        Public Sub NormalizarReferencias(raiz As WbNode, rec As PluginRecord, plugins As PluginManager)
            If raiz Is Nothing OrElse plugins Is Nothing OrElse rec Is Nothing Then Return
            Dim origen = rec.SourcePluginName
            If String.IsNullOrEmpty(origen) Then Return

            plugins.RunUnderRecordsReadLock(
                Function()
                    ' El plugin se resuelve por nombre UNA vez; la LEY de la traduccion es la del
                    ' gestor y no se re-escribe aca. Un plugin no indexado deja la referencia cruda,
                    ' incluida la ANOTACION en el nodo — que es lo que hace reversible la vuelta.
                    Dim duenio = plugins.GetPluginByNameNoLock(origen)
                    WbFormIdWalker.NormalizarDesdeArchivo(
                        raiz, Function(local As UInteger) plugins.ResolveReferenciaNoLock(duenio, local))
                    Return True
                End Function)
        End Sub

        '==========================================================================================
        ' Lectura de campos por nombre. Devuelven el valor por defecto cuando el campo no está, que
        ' es la misma política que tenían los parsers planos: un subrecord ausente deja el campo de
        ' la vista en su valor inicial.
        '==========================================================================================

        ''' <summary>El valor de una hoja como entero. Los tipos que el motor pone en una hoja son
        ''' pocos y conocidos —<c>Long</c> en todos los enteros, <c>UInteger</c> en las referencias,
        ''' <c>Single</c> en los flotantes, <c>String</c> en los textos, <c>Byte()</c> en los
        ''' bloques—, así que los dos primeros se resuelven sin pasar por la conversión general, que
        ''' EMPAQUETA y es varias veces más cara. Esto se llama una vez por lectura de campo y hay
        ''' millones por carga.
        '''
        ''' <para>⛔ El atajo va DENTRO del <c>Try</c> igual que todo lo demás, y sólo cubre los dos
        ''' tipos que no pueden fallar. Un <c>Single</c> infinito —los hay en el corpus de Skyrim— tira
        ''' al convertirlo, y hoy eso devuelve cero; sacarlo del <c>Try</c> lo convertiría en una
        ''' excepción que sube hasta la propiedad y hace desaparecer al NPC de la lista. Un texto
        ''' también sigue por el camino general: <c>Convert</c> sabe leer "12" y un atajo por tipo no.</para></summary>
        Public Function AEntero(v As Object) As Long
            Try
                If TypeOf v Is Long Then Return DirectCast(v, Long)
                If TypeOf v Is UInteger Then Return CLng(DirectCast(v, UInteger))
                Return Convert.ToInt64(v)
            Catch
                Return 0L
            End Try
        End Function

        Public Function U32(node As WbNode, path As String) As UInteger
            Dim n = Find(node, path)
            If n Is Nothing OrElse n.Value Is Nothing Then Return 0UI
            Dim v = n.Value
            If TypeOf v Is UInteger Then Return DirectCast(v, UInteger)
            Try
                Return CUInt(Convert.ToInt64(v) And &HFFFFFFFFL)
            Catch
                Return 0UI
            End Try
        End Function

        Public Function I64(node As WbNode, path As String) As Long
            Dim n = Find(node, path)
            If n Is Nothing OrElse n.Value Is Nothing Then Return 0L
            Return AEntero(n.Value)
        End Function

        Public Function Flt(node As WbNode, path As String) As Single
            Dim n = Find(node, path)
            If n Is Nothing OrElse n.Value Is Nothing Then Return 0.0F
            Dim v = n.Value
            If TypeOf v Is Single Then Return DirectCast(v, Single)
            Try
                Return Convert.ToSingle(v)
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
            Dim n = node.ByFieldPath(path)
            If n IsNot Nothing Then Return n
            If path.Length = 4 Then Return node.BySignature(path)
            Return Nothing
        End Function

    End Module

End Namespace
