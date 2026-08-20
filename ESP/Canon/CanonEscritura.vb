Namespace Canon

    ''' <summary>Escribir un record al archivo.
    '''
    ''' <para>Un solo emisor para todos los tipos de record y los dos juegos. El orden de los
    ''' subrecords, qué campos van y de qué tamaño es cada uno salen de la declaración, no de una
    ''' secuencia de llamadas escrita para cada caso.</para>
    '''
    ''' <para>Eso borra la diferencia entre los tres modos que antes tenían su propia secuencia
    ''' —sobrescribir en un juego, sobrescribir en el otro, crear uno nuevo—: los tres son el mismo
    ''' recorrido sobre árboles que se armaron distinto. Un record sobrescrito reproduce los campos
    ''' que traía la fuente porque el árbol viene de leerla; uno nuevo arranca con los que el
    ''' formato marca como obligatorios.</para></summary>
    Public Module CanonEscritura

        ''' <summary>Bytes del cuerpo del record, listos para ir detrás de la cabecera.
        '''
        ''' <para><paramref name="alDestino"/> traduce cada referencia del espacio del orden de carga
        ''' al del archivo que se está escribiendo. Se aplica sobre una COPIA del árbol: el record en
        ''' memoria tiene que quedar como estaba, porque se puede seguir editando y guardando otra
        ''' vez, y la segunda vez la traducción partiría de valores ya traducidos.</para>
        '''
        ''' <para><paramref name="indicePropioDelDestino"/> es el índice de master con el que el
        ''' archivo de salida nombra a sus propios records —la cantidad de entradas de su MAST—
        ''' y se pide SIEMPRE, con -1 para decir que no se sabe. Es lo que permite re-emitir una
        ''' referencia con la codificación exacta que traía la fuente cuando esa codificación y la
        ''' que devuelve la traducción significan lo mismo en el archivo destino. Ver
        ''' <see cref="WbFormIdWalker.ReindexarADestino"/>.</para></summary>
        Public Function Cuerpo(vista As CanonView, alDestino As Func(Of UInteger, UInteger),
                               indicePropioDelDestino As Integer) As Byte()
            If vista Is Nothing Then Return Array.Empty(Of Byte)()
            Return Cuerpo(vista.Node, vista.Context, alDestino, indicePropioDelDestino)
        End Function

        ''' <summary>Igual que el anterior, sobre el árbol y su contexto sueltos. Es el que necesita
        ''' un arnés que recorre tipos de record para los que todavía no hay vista.</summary>
        Public Function Cuerpo(arbol As WbNode, ctx As WbContext,
                               alDestino As Func(Of UInteger, UInteger),
                               indicePropioDelDestino As Integer) As Byte()
            If arbol Is Nothing Then Return Array.Empty(Of Byte)()
            If alDestino IsNot Nothing Then
                arbol = arbol.Clonar()
                WbFormIdWalker.ReindexarADestino(arbol, alDestino, indicePropioDelDestino)
            End If
            Return WbWriter.EmitBody(arbol, ctx)
        End Function

        ''' <summary>Cuántas referencias tocaría el reindexado. Sirve para saber a qué otros archivos
        ''' queda atado el record que se está por escribir, sin tener que escribirlo.</summary>
        Public Function ReferenciasDe(vista As CanonView) As List(Of UInteger)
            Dim salida As New List(Of UInteger)
            If vista Is Nothing OrElse vista.Node Is Nothing Then Return salida
            For Each n In WbFormIdWalker.Enumerate(vista.Node)
                Try
                    salida.Add(CUInt(Convert.ToInt64(n.Value) And &HFFFFFFFFL))
                Catch
                End Try
            Next
            Return salida
        End Function

    End Module

End Namespace
