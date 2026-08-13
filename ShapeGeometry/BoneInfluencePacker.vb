''' <summary>⭐ LA LEY DEL EMPAQUETADO DE INFLUENCIAS DE HUESO, ESCRITA UNA VEZ.
''' <para>Estaba transcripta en tres lugares y CADA COPIA TENÍA LA MITAD CORRECTA:</para>
''' <list type="bullet">
''' <item><c>NiTriShapeGeometry.FillFromPartition</c> / <c>FillFromSkinData</c> (esta librería) se negaban
''' —bien— a truncar un índice de hueso &gt; 255, con el motivo escrito: el <c>Debug.Assert</c> es un no-op
''' en Release y la máscara <c>And &amp;HFF</c> rebindea el vértice a OTRO hueso, en silencio. Pero ordenaban
''' con <c>List.Sort</c>, que es INESTABLE: con dos influencias del mismo peso el orden de slot queda sin
''' especificar y la misma malla puede salir distinta entre corridas.</item>
''' <item><c>PhysicsWeightCollapseHelper</c> (Wardrobe Manager) ordenaba con desempate determinista
''' (peso descendente, después índice de hueso ascendente) — correcto — pero hacía literalmente el
''' <c>CByte(idx And &amp;HFF)</c> que la otra copia declaraba defecto shipeado y removido.</item>
''' </list>
''' <para>⛔ El resultado era que la MISMA malla salía distinta según qué app la tocó, y con más de 256
''' huesos una de las dos bindeaba mal sin avisar. Este módulo es el único sitio donde vive la decisión.</para>
''' <para>⚠️ Adoptar el desempate determinista en la librería MUEVE BYTES en las mallas que tengan dos
''' influencias con peso exactamente igual: antes el orden lo decidía el algoritmo de <c>List.Sort</c>.
''' Es el cambio deseado — un orden no determinista no se puede medir con un A/A (mismo motivo que
''' 21-render-orden-de-dibujo-no-determinista).</para></summary>
Public Module BoneInfluencePacker

    ''' <summary>Codifica un índice de hueso a NIVEL DE SHAPE en el slot Byte de la paleta.
    ''' <para>⛔ SE NIEGA en vez de truncar: &gt; 255 no es representable y no hay fallback correcto.
    ''' Enmascarar con <c>&amp;HFF</c> produce un índice VÁLIDO pero de otro hueso, así que el vértice queda
    ''' bindeado a un hueso equivocado y el archivo sale sin un solo síntoma hasta que se anima.</para>
    ''' <param name="origen">De dónde salió el índice, para que el mensaje diga qué malla mirar.</param></summary>
    Public Function PackPaletteIndex(shapeBoneIdx As Integer, origen As String) As Byte
        If shapeBoneIdx < 0 OrElse shapeBoneIdx > 255 Then
            Throw New InvalidOperationException(
                $"Bone palette overflow: {origen} bone index {shapeBoneIdx} cannot be encoded as Byte " &
                "(>256 bones). Truncating would silently bind the vertex to the wrong bone.")
        End If
        Return CByte(shapeBoneIdx)
    End Function

    ''' <summary>Orden CANÓNICO de las influencias de un vértice: peso descendente y, ante empate,
    ''' índice de hueso ascendente.
    ''' <para>⛔ EL DESEMPATE NO ES COSMÉTICO. Sin él hay que usar un sort inestable, y entonces dos
    ''' influencias del mismo peso pueden caer en cualquier orden de slot: la salida deja de ser función
    ''' de la entrada y un A/A de bytes puede dar rojo sin que nadie haya cambiado nada. Los pesos iguales
    ''' no son raros — un vértice a mitad de camino entre dos huesos sale 0,5/0,5 del pintado.</para></summary>
    Public Function CompararInfluencias(pesoA As Single, huesoA As Integer, pesoB As Single, huesoB As Integer) As Integer
        Dim porPeso = pesoB.CompareTo(pesoA)          ' descendente
        If porPeso <> 0 Then Return porPeso
        Return huesoA.CompareTo(huesoB)               ' ascendente, sólo para romper el empate
    End Function

End Module
