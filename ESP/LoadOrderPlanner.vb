Imports System.IO

''' <summary>Resuelve un "Plugins.txt virtual" — una lista de plugins en el orden LITERAL que eligió el usuario
''' más el conjunto de los que están tildados — al ORDEN EFECTIVO que usaría el motor, y reporta los conflictos
''' de masters que queden.
'''
''' <para>Vive acá y no adentro del formulario del Preflight por una razón concreta y cara: la vez anterior la
''' ley nueva quedó en un método privado que leía <c>Config_App</c> por su cuenta, y el revisor pudo INVERTIRLA
''' ENTERA sin que un solo gate se pusiera en rojo. Acá todo entra por parámetro y es <c>Public Shared</c>, así
''' que el probe llama exactamente al mismo código que la UI. Ver 00-reglas-predicciones-que-no-pueden-fallar.</para>
'''
''' <para>Las dos leyes canónicas del motor que aplica:
''' <list type="number">
''' <item>ORDEN EFECTIVO = orden literal + partición estable por grupo master, vía
''' <see cref="PluginManager.StablePartitionMasterGroup"/> — la MISMA función que usa el lector del load order,
''' no una copia.</item>
''' <item>Ningún plugin puede cargar antes de un master suyo — el motor lo resuelve marcando al plugin como
''' de masters faltantes y desactivando al dependiente.</item>
''' </list></para></summary>
Public NotInheritable Class LoadOrderPlanner

    Private Sub New()
    End Sub

    Public NotInheritable Class Plan
        ''' <summary>Orden LITERAL después de reparar (subir masters). Es lo que se le muestra al usuario y lo
        ''' que se persiste: es su Plugins.txt.</summary>
        Public Property LiteralOrder As List(Of String)

        ''' <summary>Índice EFECTIVO de cada plugin TILDADO — el que va a usar el motor. Sólo los tildados: los
        ''' demás no están en el load order.</summary>
        Public Property EffectiveIndex As Dictionary(Of String, Integer)

        ''' <summary>Tildados que cargan antes de un master suyo y que NO se arreglan reordenando, porque el
        ''' dependiente está en el grupo master y su master no. El motor pone todo el grupo master adelante, así
        ''' que la línea puede ir donde sea: el dependiente carga primero igual. Es el mismo caso que
        ''' <c>LoadOrderActivator</c> reporta como "ESM mastering ESP". Se avisa, no se intenta arreglar.</summary>
        Public Property GroupConflicts As List(Of String)

        ''' <summary>Conflictos de orden que sobrevivieron a la reparación. Con plugins sanos queda vacío; si
        ''' trae algo es un ciclo de masters (header corrupto) y hay que decirlo, no taparlo.</summary>
        Public Property UnresolvedOrderConflicts As List(Of String)

        ''' <summary>Cuántas FILAS quedaron en una posición distinta de la que tenían. Se muestra en la barra
        ''' de estado: mover filas por debajo del usuario sin decírselo es peor que no moverlas.
        ''' <para>Se llamaba <c>MastersMoved</c> y contaba "cuántos masters subí", que con el orden
        ''' topológico ya no es una cantidad bien definida: subir un master baja a otro, y un solo intercambio
        ''' cambia DOS posiciones. Contar filas desplazadas es lo que realmente pasó y lo que el usuario puede
        ''' verificar mirando la grilla.</para></summary>
        Public Property RowsReordered As Integer
    End Class

    ''' <summary>Ordena, repara y diagnostica en una sola pasada. NO muta nada de lo que recibe.</summary>
    ''' <param name="literalOrder">Todas las filas de la grilla, en el orden que eligió el usuario.</param>
    ''' <param name="checkedNames">Cuáles están tildadas, o sea cuáles "están en el Plugins.txt".</param>
    ''' <param name="mastersByName">Masters directos de cada plugin. Una entrada con valor <c>Nothing</c>
    ''' significa "no se pudo leer el header": sus dependencias son DESCONOCIDAS y acá no se inventa ninguna
    ''' (de eso se ocupa la validación de presencia, que lo marca como roto).</param>
    ''' <param name="dataPath">Carpeta Data, para resolver el grupo de cada plugin con la ley canónica.</param>
    Public Shared Function Resolve(literalOrder As IEnumerable(Of String),
                                   checkedNames As ICollection(Of String),
                                   mastersByName As Dictionary(Of String, List(Of String)),
                                   dataPath As String) As Plan
        Dim order As New List(Of String)(If(literalOrder, Enumerable.Empty(Of String)()))
        Dim masterMap = If(mastersByName, New Dictionary(Of String, List(Of String))(StringComparer.OrdinalIgnoreCase))
        ' La firma acepta cualquier ICollection, pero la correctitud depende de que la pertenencia sea
        ' case-INSENSITIVE (los nombres de plugin lo son en todo el resto del árbol) y O(1). Un List(Of String)
        ' daría comparación sensible a mayúsculas y búsqueda lineal, en silencio. Se normaliza acá en vez de
        ' confiar en el llamador.
        Dim tildados As New HashSet(Of String)(
            If(checkedNames, CType(Array.Empty(Of String)(), ICollection(Of String))), StringComparer.OrdinalIgnoreCase)
        Dim groupCache As New Dictionary(Of String, Boolean?)(StringComparer.OrdinalIgnoreCase)
        Dim isMasterGroup = Function(n As String) PluginManager.IsMasterGroup(dataPath, n, groupCache).GetValueOrDefault()
        Dim isChecked = Function(n As String) tildados.Contains(n)

        ' ── 1. Los tildados, en el orden literal del usuario, partidos por grupo master.
        ' El motor pone todo el grupo master adelante, así que un master del grupo
        ' master NUNCA puede cargar después de un dependiente que no lo es: esas aristas no pueden violarse y
        ' no hay nada que ordenar entre buckets.
        Dim sel = order.Where(isChecked).ToList()
        ' Los dos buckets usan EL MISMO predicado que la partición (PluginManager.IsMasterGroup con
        ' GetValueOrDefault), así que un grupo desconocido cae del mismo lado en los dos lugares. Antes acá se
        ' decidía con GetValueOrDefault y allá se lo clavaba en su índice: dos reglas para la misma pregunta
        ' dentro de la misma función.
        Dim buckets As New List(Of List(Of String))() From {
            sel.Where(Function(n) isMasterGroup(n)).ToList(),
            sel.Where(Function(n) Not isMasterGroup(n)).ToList()
        }

        ' ── 2. Dentro de cada bucket, orden topológico ESTABLE: los masters antes que sus dependientes y,
        ' entre nodos sin relación, se conserva el orden que eligió el usuario.
        ' Reemplaza a un bucle que subía UN master por vuelta y volvía a empezar, con tope de 500 vueltas.
        ' MEDIDO con 1500 plugins encadenados: se agotaba el tope y dejaba 999 conflictos sin resolver, o sea
        ' que en un rig grande la app se rendía en silencio. Kahn con desempate por posición original es
        ' O(n + aristas) y resuelve la cadena entera de una.
        Dim reordered As Integer = 0
        Dim ordenadoPorBucket As New List(Of List(Of String))()
        For Each bucket In buckets
            ordenadoPorBucket.Add(StableTopoSort(bucket, masterMap))
        Next

        ' ── 3. Reinyectar CONSERVANDO EL LAYOUT LITERAL.
        ' Los buckets sirven SÓLO para acotar las aristas del topo-sort, NO para reordenar la lista que ve
        ' el usuario. Concatenarlos particionaba el orden LITERAL — o sea, le reescribía su Plugins.txt y
        ' dejaba sin sentido la columna "Load #", que existe justamente para mostrar que el motor particiona
        ' sin que el archivo cambie. Cada ranura literal se rellena desde el bucket al que pertenecía su
        ' ocupante original, así que un plugin del grupo master sigue en una ranura de grupo master.
        ' Los NO tildados se quedan clavados en su índice.
        Dim cursor(buckets.Count - 1) As Integer
        Dim resultado As New List(Of String)(order.Count)
        For Each n In order
            If Not isChecked(n) Then
                resultado.Add(n)
                Continue For
            End If
            Dim b = If(isMasterGroup(n), 0, 1)
            resultado.Add(ordenadoPorBucket(b)(cursor(b)))
            cursor(b) += 1
        Next
        For k = 0 To order.Count - 1
            If Not String.Equals(order(k), resultado(k), StringComparison.OrdinalIgnoreCase) Then reordered += 1
        Next
        order = resultado

        ' ── 4. Diagnóstico sobre el orden ya estable.
        Dim finalEff = ComputeEffective(order, tildados, dataPath, groupCache)
        Dim groupConf As New List(Of String)()
        Dim orderConf As New List(Of String)()
        For Each p In finalEff.Keys
            Dim masters As List(Of String) = Nothing
            If Not masterMap.TryGetValue(p, masters) OrElse masters Is Nothing Then Continue For
            For Each m In masters
                Dim mi As Integer
                If Not finalEff.TryGetValue(m, mi) OrElse mi <= finalEff(p) Then Continue For
                If isMasterGroup(p) AndAlso Not isMasterGroup(m) Then
                    If Not groupConf.Contains(p) Then groupConf.Add(p)
                ElseIf Not orderConf.Contains(p) Then
                    orderConf.Add(p)        ' sólo puede quedar algo acá con un CICLO de masters
                End If
            Next
        Next

        Return New Plan With {
            .LiteralOrder = order,
            .EffectiveIndex = finalEff,
            .GroupConflicts = groupConf,
            .UnresolvedOrderConflicts = orderConf,
            .RowsReordered = reordered
        }
    End Function

    ''' <summary>Orden topológico ESTABLE de <paramref name="items"/>: cada master antes que sus dependientes
    ''' y, entre nodos sin relación de dependencia, se conserva el orden de entrada (el que eligió el usuario).
    ''' <para>Kahn con desempate por posición original. Sólo se consideran aristas cuyos DOS extremos están en
    ''' <paramref name="items"/>: un master que no está tildado no es un problema de orden sino de presencia, y
    ''' lo reporta la otra validación.</para>
    ''' <para>Un ciclo de masters es imposible en plugins válidos pero un header corrupto podría declararlo.
    ''' Los nodos que queden en el ciclo se emiten al final, en su orden original, en vez de perderse o de
    ''' colgar el bucle. El diagnóstico posterior los va a listar como conflictos sin resolver, que es
    ''' exactamente lo que son.</para></summary>
    Private Shared Function StableTopoSort(items As List(Of String),
                                           masterMap As Dictionary(Of String, List(Of String))) As List(Of String)
        If items.Count < 2 Then Return New List(Of String)(items)

        Dim pos As New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)
        For i = 0 To items.Count - 1
            pos(items(i)) = i
        Next

        ' dependents(m) = los que esperan a m; pending(d) = cuántos masters le faltan a d.
        Dim dependents As New Dictionary(Of String, List(Of String))(StringComparer.OrdinalIgnoreCase)
        Dim pending As New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)
        For Each n In items
            pending(n) = 0
        Next
        For Each d In items
            Dim ms As List(Of String) = Nothing
            If Not masterMap.TryGetValue(d, ms) OrElse ms Is Nothing Then Continue For
            Dim yaVistos As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
            For Each m In ms
                If Not pos.ContainsKey(m) Then Continue For              ' master no tildado: otra validación
                If String.Equals(m, d, StringComparison.OrdinalIgnoreCase) Then Continue For
                If Not yaVistos.Add(m) Then Continue For                 ' MAST duplicada: una sola arista
                If Not dependents.ContainsKey(m) Then dependents(m) = New List(Of String)()
                dependents(m).Add(d)
                pending(d) += 1
            Next
        Next

        ' Cola de listos, ordenada por posición original ⇒ estabilidad.
        Dim listos As New SortedSet(Of Integer)()
        For Each n In items
            If pending(n) = 0 Then listos.Add(pos(n))
        Next

        Dim salida As New List(Of String)(items.Count)
        Dim emitido As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        While listos.Count > 0
            Dim i = listos.Min
            listos.Remove(i)
            Dim n = items(i)
            salida.Add(n)
            emitido.Add(n)
            Dim hijos As List(Of String) = Nothing
            If dependents.TryGetValue(n, hijos) Then
                For Each d In hijos
                    pending(d) -= 1
                    If pending(d) = 0 Then listos.Add(pos(d))
                Next
            End If
        End While

        ' Lo que quedó es un ciclo: se emite en el orden original para no perder filas.
        If salida.Count < items.Count Then
            For Each n In items
                If Not emitido.Contains(n) Then salida.Add(n)
            Next
        End If
        Return salida
    End Function

    ''' <summary>Orden efectivo de los TILDADOS: su orden literal con la partición del motor encima.
    ''' <para><c>forcedCount = 0</c> porque acá no hay tramo forzado: los masters implícitos y el Creation Club
    ''' no son filas de esta lista — viven en <c>PluginManager.ReadActiveLoadOrder</c>, que sí los pone delante
    ''' por <c>miOfficialIndex</c>/<c>miCCIndex</c> y por eso pasa un corte distinto de cero.</para></summary>
    Private Shared Function ComputeEffective(order As List(Of String), checkedNames As ICollection(Of String),
                                             dataPath As String,
                                             cache As Dictionary(Of String, Boolean?)) As Dictionary(Of String, Integer)
        Dim sel = order.Where(Function(n) checkedNames.Contains(n)).ToList()
        PluginManager.StablePartitionMasterGroup(sel, 0, dataPath, cache)
        Dim map As New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)
        For i = 0 To sel.Count - 1
            map(sel(i)) = i
        Next
        Return map
    End Function

End Class
