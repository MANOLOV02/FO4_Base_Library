Option Strict On
Option Explicit On

Imports System.Collections.Concurrent
Imports System.Runtime.Intrinsics
Imports NiflySharp.Blocks

' =================================================================================================
' EL CABLEADO — de las shapes del render al motor canónico, y de vuelta.
'
' No hay solver acá: el solver es `Havok.Motor`, transcrito del `.exe`. Acá vive lo que en el juego
' hace Bethesda alrededor del motor, y también está transcrito:
'
'   1. qué shapes tienen un `BSClothExtraData` y qué paquete `hcl` trae cada una;
'   2. EL RELOJ: cuántos pasos corre la tela este cuadro y de qué largo (`RelojDelMundo`);
'   3. `setStiffnessMode(2, s1, 1)` en cada prenda, cada cuadro (`0x1418753B5`);
'   4. por paso: la máquina de estados del job (Animate→Simulate, y vuelta a Animate tras un NaN),
'      abrir el `BSTransformSet` (entrada interpolada), correr la cadena, cerrarlo (salida
'      extrapolada y escrita en los nodos) — `ConjuntoDeTransforms`;
'   5. el nodo escrito se traduce a `HierarchiBone_class.PhysicsDeltaTransform`, que es como la app
'      representa el local que el motor deja en `node+0x30`.
'
' ⛔ El código entra en TODAS las configuraciones. Lo que decide si corre es
' `HavokPhysicsSettings.Enabled`, que la aplicación sólo prende en Debug.
'
' ⛔ El estado VIVE entre cuadros: `Instancia.Posiciones`/`Previas` son el Verlet, el transform set es
' `hclClothInstance.transformSets`, y el `BSTransformSet` guarda la entrada y la salida anteriores.
' =================================================================================================

Namespace Havok.Physics

    ''' <summary>El cableado del motor canónico de tela.</summary>
    Public NotInheritable Class ClothCanonico

        Private Sub New()
        End Sub

        ''' <summary>El paquete `hcl` parseado de cada bloque. Cachear es obligatorio: parsear un
        ''' packfile por frame no es una opción.</summary>
        Private Shared ReadOnly _paquete As New Dictionary(Of BSClothExtraData, HclClothPackageGraph_Class)

        ''' <summary>El estado vivo por bloque, una entrada por `ClothConfig`. ⛔ Persiste entre cuadros.</summary>
        Private Shared ReadOnly _prendas As New Dictionary(Of BSClothExtraData, PrendaViva())

        ''' <summary>Una prenda viva: la instancia del motor. Sus `BSTransformSet` —uno por
        ''' definición, `0x1418A36F0`— viven en la instancia (`Prenda.Conjuntos`, `[inst+0x40]`).</summary>
        Friend NotInheritable Class PrendaViva
            Friend Prenda As Havok.Motor.PrendaSimulada
            ''' <summary>Los nombres de hueso del `hkaSkeleton` de cada `BSTransformSet`, por índice de
            ''' set: la vista de nodos de cada uno busca sus nodos por ESOS nombres
            ''' (`0x1418A4568` GetObjectByName con `[skel+0x28][i]`).</summary>
            Friend NombresPorSet As List(Of String)()
            ''' <summary>`[inst+0x1C]`: el tiempo medido del último paso, en segundos. El constructor lo
            ''' deja en −1 (`0x1418C7EEA mov dword [rcx+0x1C], 0xBF800000`); `Runtime Buffers Release`
            ''' le copia el del job (`0x1418C8F0D`, medido en `0x1418791B5`…`0x141879326`). Lo lee el
            ''' costo del LOD (`0x1418A1AFA`).</summary>
            Friend TiempoDelPaso As Single = -1.0F
            ''' <summary>El BLOQUE (`BSClothExtraData`) está enganchado al mundo de tela: `[prenda+0x78] ≠ 0`
            ''' (`0x1418A0DA0`). Al crear se engancha (`0x1406F1484`). Sólo las del mundo pasan por
            ''' `InitializeClothJobs`/`BuildClothJobsForStep` (recorren `[mgr+0x10]`, `0x141871BD0`/`0x141871C90`).</summary>
            Friend EnElMundo As Boolean = True
            ''' <summary>El `BSTransformSet` con el que corre el job: `[[inst+0x40]]`, el 0
            ''' (`0x141879036 mov r13, [rax]`). La máquina de estados sólo lo mira a él.</summary>
            Friend ReadOnly Property ConjuntoDelJob As ConjuntoDeTransforms
                Get
                    Dim l = Prenda?.Conjuntos
                    Return If(l Is Nothing OrElse l.Length = 0, Nothing, l(0))
                End Get
            End Property
        End Class

        ''' <summary>
        ''' Las prendas simuladas vivas, para que el ARNÉS pueda medir adentro.
        ''' <para>⛔ Sólo lectura: no cambia ninguna ley, expone lo que ya existe.</para>
        ''' </summary>
        Friend Shared Function PrendasVivas() As List(Of Havok.Motor.PrendaSimulada)
            Dim r As New List(Of Havok.Motor.PrendaSimulada)()
            SyncLock _prendas
                For Each kv In _prendas
                    If kv.Value Is Nothing Then Continue For
                    For Each p In kv.Value
                        If p IsNot Nothing AndAlso p.Prenda IsNot Nothing Then r.Add(p.Prenda)
                    Next
                Next
            End SyncLock
            Return r
        End Function

        ''' <summary>
        ''' ¿Queda alguna prenda viva en `Animate` con el pasaje a `Simulate` todavía por hacer?
        ''' (`ShouldSwitchState` y `+0x194` en 1 y el estado actual = `+0x198`, `0x141879405`.)
        ''' <para>Para el ARNÉS: el `Simulate` no puede correr antes del tercer job, así que medir
        ''' «el simulate corre» antes de eso mide el `Animate`.</para>
        ''' </summary>
        Friend Shared Function QuedanPasajesPendientes() As Boolean
            SyncLock _prendas
                For Each kv In _prendas
                    If kv.Value Is Nothing Then Continue For
                    For Each v In kv.Value
                        Dim ts = v?.ConjuntoDelJob
                        If ts Is Nothing Then Continue For
                        If ts.PuedeCambiarDeEstado(BAnimClothLOD) AndAlso ts.Valido AndAlso
                           v.Prenda.EstadoActual = ts.IndiceAnimate Then Return True
                    Next
                Next
            End SyncLock
            Return False
        End Function

        ''' <summary>Los esqueletos a los que esta simulación le escribió la capa, para poder
        ''' limpiarla cuando se apaga.</summary>
        Private Shared ReadOnly _tocados As New ConcurrentDictionary(Of SkeletonInstance, Object)

        ''' <summary>`bAnimClothLOD` — el byte `0x142F4FB88`, que en el `.exe` vale 1. Lo lee
        ''' `ShouldSwitchState` (`0x1418A7A02`).</summary>
        Private Const BAnimClothLOD As Boolean = True

        ''' <summary>El reloj real entre cuadros, en ms enteros como el timer del juego.</summary>
        Private Shared ReadOnly _reloj As Stopwatch = Stopwatch.StartNew()
        Private Shared _msAnterior As Long = -1

        ''' <summary>
        ''' ⭐ Un cuadro del reloj: se llama UNA vez por render, antes de simular cualquier esqueleto.
        ''' <para>`milisegundos` ≥ 0 lo fija el llamador (los arneses, para ser reproducibles); si
        ''' es negativo se mide con el reloj real, como el timer del juego (`0x14165BA10`).</para>
        ''' </summary>
        Friend Shared Function NuevoCuadro(milisegundos As Long) As CuadroDeTela
            Dim ms = milisegundos
            If ms < 0 Then
                Dim ahora = _reloj.ElapsedMilliseconds
                ms = If(_msAnterior < 0, 0L, ahora - _msAnterior)
                _msAnterior = ahora
            End If
            Return RelojDelMundo.Avanzar(RelojDelMundo.DtDelTimer(ms))
        End Function

        ''' <summary>
        ''' Corre un cuadro de física sobre las shapes con `HasPhysics` de cada esqueleto y escribe la
        ''' capa `PhysicsDeltaTransform` de los cloth-bones. Llamar DESPUÉS de `ApplyPose` y ANTES del
        ''' skinning.
        ''' </summary>
        ''' <summary>Los esqueletos que el llamador declara jugador cuando no los pasa por parámetro (el
        ''' camino del render no lo pasa). `Nothing` = ninguno.</summary>
        Friend Shared JugadoresDelLlamador As ICollection(Of SkeletonInstance)

        ''' <summary>El estado del actor que el LOD lee y la app no tiene (sentado, en el suelo, muerto,
        ''' nadando, visibilidad…), por esqueleto, lo da el llamador. Sin entrada: `EntradaDeLodDeTela.ActorSinEstado`.
        ''' La distancia², `EsJugador` y la escala las pone `StepShapes` (`0x140DB31B0` / `0x140BD65A0`).</summary>
        Friend Shared EstadosDelLlamador As IDictionary(Of SkeletonInstance, EntradaDeLodDeTela)

        ''' <param name="jugadores">Los esqueletos que en el juego serían el jugador: `0x1406F145F` pasa
        ''' `actor == [0x1431EDE50]` a `0x1418A1B60` al crear la prenda. Lo decide el llamador; sin
        ''' lista, todos son NPC (byte en 0).</param>
        ''' <param name="camara">La posición de la cámara del preview: `0x1410265E0(PlayerCamera, &amp;p, 1)`
        ''' (`0x140DB329F`) para la distancia² del LOD. Sin cámara el LOD no se corre y todas las prendas
        ''' quedan en el mundo (hueco declarado del llamador).</param>
        Public Shared Sub StepShapes(porEsqueleto As IDictionary(Of SkeletonInstance, List(Of IRenderableShape)),
                                     Optional milisegundos As Long = -1,
                                     Optional jugadores As ICollection(Of SkeletonInstance) = Nothing,
                                     Optional camara As System.Numerics.Vector3? = Nothing)
            If porEsqueleto Is Nothing Then Exit Sub
            If Not HavokPhysicsSettings.Enabled OrElse HavokPhysicsSettings.Mode = HavokPhysicsMode.Off Then
                For Each kv In porEsqueleto
                    LimpiarCapa(kv.Key)
                Next
                Exit Sub
            End If

            Dim cuadro = NuevoCuadro(milisegundos)

            ' Todas las prendas de todos los esqueletos, con sus nodos.
            Dim todo As New List(Of (Viva As PrendaViva, Nodos As NodosDeEsqueleto(), Esqueleto As SkeletonInstance))
            ' Las prendas de cada actor en su orden (`0x140D3B720`): un bloque = la lista `[prenda+0x60]` de instancias.
            Dim bloquesPorActor As New List(Of (Esqueleto As SkeletonInstance, EsJugador As Boolean, Bloques As List(Of PrendaViva())))
            Dim js = If(jugadores, JugadoresDelLlamador)
            For Each kv In porEsqueleto
                Dim skeleton = kv.Key
                If skeleton Is Nothing OrElse Not skeleton.HasSkeleton OrElse kv.Value Is Nothing Then Continue For
                Dim esJugador = js IsNot Nothing AndAlso js.Contains(skeleton)
                Dim bloques As New List(Of PrendaViva())
                ' ⛔ UN bloque por prenda aunque lo compartan varias shapes: el `BSClothExtraData` es el
                ' dueño del estado, y simular dos veces el mismo bloque le duplica el tiempo.
                Dim vistos As New HashSet(Of BSClothExtraData)()
                For Each shape In kv.Value
                    If shape Is Nothing OrElse Not shape.HasPhysics OrElse shape.NifContent Is Nothing Then Continue For
                    Dim bl = SkeletonClothOverlayHelper_Class.ResolveClothBlockForShape(shape)
                    If bl Is Nothing OrElse Not vistos.Add(bl) Then Continue For
                    Try
                        Dim antes = todo.Count
                        PrepararBloque(bl, skeleton, todo, esJugador)
                        If todo.Count > antes Then bloques.Add(todo.Skip(antes).Select(Function(x) x.Viva).ToArray())
                    Catch ex As Exception
                        Dim exL = ex
                        Logger.LogLazy(Function() $"[CLOTH-CANON] la prenda fallo al armarse y queda SIN fisica: {exL}")
                    End Try
                Next
                bloquesPorActor.Add((skeleton, esJugador, bloques))
            Next
            If todo.Count = 0 Then Exit Sub

            ' ---- EL LOD POR ACTOR — antes de los jobs del paso.
            ' ⛔ HUECO DECLARADO: el orden, dentro del cuadro del juego, entre el job del LOD de los NPC
            ' (`0x140DB31B0`, lista de jobs vía `0x140BD5A40`) e `InitializeClothJobs` (`0x140BD63C7`) no está
            ' medido; acá corre antes, con el `nPasosTela` de este cuadro. El del jugador (`0x140BD65A0`) corre
            ' justo antes de armar sus jobs (`0x140BD670C` → `0x140BD6729` `0x1418756F0`).
            If camara.HasValue AndAlso HavokPhysicsSettings.CorreSimulacion Then
                CorrerLod(bloquesPorActor, camara.Value, CUInt(cuadro.NPasos))
            End If
            Dim trabajo = todo.Where(Function(t) t.Viva.EnElMundo).ToList()
            If trabajo.Count = 0 Then Exit Sub

            ' El reloj del viento — 0x1418752B8-0x1418752F5 (con `mgr[+0x170]`, que sin clima es falso).
            Havok.Motor.Viento.AvanzarPool(cuadro.DtConsumido)

            ' `setStiffnessMode(2, s1, 1)` — 0x1418753B5 → 0x1418C6540 → 0x1418C6500.
            For Each t In trabajo
                Dim inst = t.Viva.Prenda.Estado
                inst.Modo = 2                                                   ' 0x1418C6500: inst[+0x1CC]
                inst.S1 = cuadro.S1                                             '   [+0x1D0]
                inst.S2 = 1.0F                                                  '   [+0x1D4] (xmm3 = 1,0 en 0x1418753A6)
            Next

            ' ⛔ LOS PASOS VAN AFUERA Y LAS PRENDAS ADENTRO: `BuildClothJobsForStep` arma un job por
            ' prenda POR PASO (`0x14187575B`-`0x1418757FC`).
            Dim escritosPorEsqueleto As New Dictionary(Of SkeletonInstance, Integer)
            For Each t In trabajo
                ' 0x141875489-0x14187552A: cada set de `[inst+0x40]`, en orden.
                For Each ts In t.Viva.Prenda.Conjuntos
                    ts?.CargarCuadro(cuadro)                                    ' 0x14187551B (0x1418A5DF0)
                Next
            Next
            For paso = 1 To cuadro.NPasos
                For Each t In trabajo
                    CorrerJob(t.Viva, t.Nodos, cuadro.Paso, t.Esqueleto, escritosPorEsqueleto)
                Next
            Next

            For Each kv In escritosPorEsqueleto
                If kv.Value > 0 Then
                    kv.Key.MarkPhysicsLayerWritten()
                    _tocados(kv.Key) = Nothing
                End If
            Next
            If Logger.Enabled Then
                Dim cq = cuadro, pq = trabajo.Count, eq = escritosPorEsqueleto.Values.Sum()
                Logger.LogLazy(Function() $"[CLOTH-RELOJ] pasos={cq.NPasos} paso={cq.Paso:0.#######} dt={cq.DtConsumido:0.#####} sobrante {cq.SobranteViejo:0.#####}→{cq.SobranteNuevo:0.#####} · prendas={pq} · nodos escritos={eq}")
            End If
        End Sub

        ''' <summary>Un cuadro para UN esqueleto. Es la misma ley: arma el diccionario y llama a la de
        ''' arriba (un cuadro del reloj).</summary>
        Public Shared Sub StepShapes(shapes As IEnumerable(Of IRenderableShape), skeleton As SkeletonInstance,
                                     Optional milisegundos As Long = -1)
            If skeleton Is Nothing OrElse shapes Is Nothing Then Exit Sub
            Dim d As New Dictionary(Of SkeletonInstance, List(Of IRenderableShape)) From {{skeleton, shapes.ToList()}}
            StepShapes(d, milisegundos)
        End Sub

        ''' <summary>
        ''' Los índices de set que el estado actual declara, en su orden — lo que recorren
        ''' `Operator Prepare` (`0x1418C8D96`-`0x1418C8DED`: `[inst+0x40][usedTransformSets[k].
        ''' transformSetIndex]→vtbl[+0x20]`) y `Runtime Buffers Release` (`0x1418C9059`-`0x1418C9099`:
        ''' `→vtbl[+0x28]`), con el estado `[clothData+0x58][inst+0x18]` (`0x1418C8B9E`-`0x1418C8BAE`).
        ''' <para>⛔ Un set declarado dos veces se abre dos veces: el contador `+0x190` lo cuenta.</para>
        ''' <para>⛔ Las dos pasadas exigen `[ctx+0x20] ≠ 0` (`0x1418C8DC4`, `0x1418C9059`). Ese byte es
        ''' `[mundo+0x99]` copiado al job (`0x1418BFC13`; `0x1418BEF97` en el camino sin job), que el
        ''' constructor del mundo `0x1418BE160` toma de su `cinfo` (`0x1418BE1F2`), y el ÚNICO que lo
        ''' construye (`0x141873AF6`) lo arma en 1 (`0x141873ACC mov byte [rsp+0x50], 1`). Por eso
        ''' corren siempre.</para>
        ''' </summary>
        Private Shared Function SetsDelEstado(p As Havok.Motor.PrendaSimulada) As List(Of Integer)
            Dim r As New List(Of Integer)
            If p.Estados Is Nothing OrElse p.EstadoActual < 0 OrElse p.EstadoActual >= p.Estados.Count Then Return r
            Dim est = p.Estados(p.EstadoActual)
            If est?.UsedTransformSets Is Nothing Then Return r
            For Each acc In est.UsedTransformSets
                If acc IsNot Nothing Then r.Add(CInt(acc.TransformSetIndex))   ' 0x1418C8DB8 movsxd [r8+rbp]
            Next
            Return r
        End Function

        ''' <summary>
        ''' `TtSolveClothJob` — `0x141878F50`: un job por prenda por paso.
        ''' <para>```
        ''' veces = 1                                                   ' 0x141878FDB mov edi, 1
        ''' ts = [[inst+0x40]]                                          ' 0x141879036
        ''' si ts[+0x1B7] y ts[+0x194] = 1 y ts[+0x1A0] ≠ 0 y ts[+0x1B4] y ts[+0x1B5]:   ' 0x14187903E…06D
        '''   veces = uNumSimSettleSteps                                ' 0x14187906F
        '''   0x1418A5DF0(ts, sobranteViejo 0, sobranteNuevo 0, dt = veces·PASO, paso = PASO, n = veces)
        '''                                                             ' 0x141879075…09C
        '''   si veces = 0: el job no corre                             ' 0x1418790A1 je 0x14187939B
        ''' repetir veces: máquina de estados + cadena                  ' 0x1418790B3 / 0x141879362…37A
        ''' ```</para>
        ''' </summary>
        Private Shared Sub CorrerJob(viva As PrendaViva, nodosPorSet As NodosDeEsqueleto(), dt As Single,
                                     esqueleto As SkeletonInstance,
                                     escritosPorEsqueleto As Dictionary(Of SkeletonInstance, Integer))
            Dim veces = 1                                                       ' 0x141878FDB
            Dim ts = viva.ConjuntoDelJob
            If ts IsNot Nothing AndAlso ts.Asentar AndAlso ts.Valido AndAlso ts.Fase <> 0 AndAlso
               ts.SinInterpolar AndAlso ts.SinExtrapolar Then                   ' 0x14187903E…06D
                veces = AjustesDeTela.Defecto.UNumSimSettleSteps                      ' 0x14187906F
                Dim paso = RelojDelMundo.PasoActual                             ' 0x141879078 [0x143D87E8C]
                Dim c As CuadroDeTela
                c.SobranteViejo = 0.0F                                          ' 0x141879087 xmm1
                c.SobranteNuevo = 0.0F                                          ' 0x141879080 xmm2
                c.DtConsumido = CSng(veces) * paso                              ' 0x141879093 / 0x141879098
                c.Paso = paso                                                   ' 0x14187908D [rsp+0x20]
                c.NPasos = veces                                                ' 0x141879083 [rsp+0x28]
                ts.CargarCuadro(c)                                              ' 0x14187909C
                If veces = 0 Then Exit Sub                                      ' 0x1418790A1
                If Logger.Enabled Then
                    Dim vq = veces
                    Logger.LogLazy(Function() $"[CLOTH-JOB] asentamiento: {vq} vueltas (0x14187903E)")
                End If
            End If
            For i = 1 To veces                                                  ' 0x141879362 sub [rsp+0x38], 1
                CorrerPaso(viva, nodosPorSet, dt, esqueleto, escritosPorEsqueleto)
            Next
        End Sub

        ''' <summary>
        ''' Al enganchar al mundo — `0x1418A0DA0` con un mundo distinto del actual, por instancia
        ''' (`0x1418A0EB0`-`0x1418A0F17`): `setCurrentState(Animate de [[inst+0x40]] si ShouldSwitchState,
        ''' si no 0)`; después, en CADA set, `0x1418A6070` (banderas a 1) y fase −1.
        ''' </summary>
        Private Shared Sub Enganchar(p As Havok.Motor.PrendaSimulada)
            Dim lista = p.Conjuntos
            If HavokPhysicsSettings.CorreSimulacion Then
                p.FijarEstado(If(lista(0).PuedeCambiarDeEstado(BAnimClothLOD), lista(0).IndiceAnimate, 0))   ' 0x1418A0EC4 / 0x1418A0ED9
            End If
            For Each c In lista                                                 ' 0x1418A0EF0
                c.ReiniciarBanderas()                                           ' 0x1418A0EFB
                c.Fase = -1                                                     ' 0x1418A0F03
            Next
        End Sub

        ''' <summary>La escala del costo: `1 / [0x142F3105C]` (`0x140BD66BD`…`0x140BD6708`;
        ''' `0x140DB37E2` `0x140BD4490` + `0x140DB37E7`…`0x140DB37FD`).</summary>
        Private Shared Function EscalaDeCosto() As Single
            Return 1.0F / CSng(6)                                               ' [0x142F3105C] = 6 en la imagen
        End Function

        ''' <summary>
        ''' Los jobs del LOD de la tela. NPC — `0x140DB31B0`: la distancia² de cada actor a la cámara
        ''' (`0x140DB3495`…`0x140DB3519`), orden ascendente (`0x140DB3742`…`0x140DB37D0`, `0x140DBA4F0`),
        ''' cuenta y costo compartidos desde 0 (`0x140DB37D5`/`0x140DB37D8`) y `0x140DB3A40` por actor
        ''' (`0x140DB3841`). Jugador — `0x140BD65A0`/`0x140BD6850`: distancia² 0, cuenta 0, costo 0
        ''' (`0x140BD66D0`…`0x140BD66FB`), `0x140DB3A40` (`0x140BD670C`).
        ''' La lista de entrada va en el orden del diccionario del llamador (el `.exe` la arma recorriendo los
        ''' handles de alto proceso `[lists+0x40]`, `0x140DB32F0`).
        ''' </summary>
        Private Shared Sub CorrerLod(actores As List(Of (Esqueleto As SkeletonInstance, EsJugador As Boolean, Bloques As List(Of PrendaViva()))),
                                     camara As System.Numerics.Vector3, nPasosTela As UInteger)
            Dim npcs As New List(Of (Actor As (Esqueleto As SkeletonInstance, EsJugador As Boolean, Bloques As List(Of PrendaViva())), D2 As Single))
            For Each a In actores
                If a.EsJugador Then Continue For
                ' `[actor+0xD0..+0xD8]`: la posición de la referencia. En la app, el mundo del nodo raíz del esqueleto.
                Dim raiz = a.Esqueleto.SkeletonStructure.FirstOrDefault(Function(b) b IsNot Nothing AndAlso b.Parent Is Nothing)
                Dim pos = If(raiz Is Nothing, System.Numerics.Vector3.Zero, raiz.GetGlobalTransform.Translation)
                npcs.Add((a, LodDeTela.DistanciaCuadrada(camara, pos)))        ' 0x140DB3495…0x140DB3536
            Next
            Dim perm = LodDeTela.OrdenarPorDistancia(npcs.Select(Function(x) x.D2).ToArray())   ' 0x140DB3727…0x140DB37D5
            Dim ordenados = perm.Select(Function(ix) npcs(ix)).ToList()
            Dim cuenta = 0UI, costo = 0.0F                                      ' 0x140DB37D8 / 0x140DB37DC
            For Each n In ordenados
                LodDeActor(n.Actor.Esqueleto, False, n.D2, n.Actor.Bloques, nPasosTela, cuenta, costo)   ' 0x140DB3841
            Next
            For Each a In actores
                If Not a.EsJugador Then Continue For
                Dim cj = 0UI, kj = 0.0F                                         ' 0x140BD66F3 / 0x140BD66FB
                LodDeActor(a.Esqueleto, True, 0.0F, a.Bloques, nPasosTela, cj, kj)   ' 0x140BD66DA xorps xmm2 → 0x140BD670C
            Next
        End Sub

        ''' <summary>`0x140DB3A40` sobre las prendas de un actor, y lo que hace con cada modo:
        ''' `0x1418A0DA0(prenda, mundo o NULL)` (`0x140DB3E00`) y `0x1418A1B20` (`0x140DB3E1E`).</summary>
        Private Shared Sub LodDeActor(esqueleto As SkeletonInstance, esJugador As Boolean, d2 As Single,
                                      bloques As List(Of PrendaViva()), nPasosTela As UInteger,
                                      ByRef cuenta As UInteger, ByRef costo As Single)
            Dim dado As EntradaDeLodDeTela = Nothing
            Dim est = EstadosDelLlamador
            If est Is Nothing OrElse Not est.TryGetValue(esqueleto, dado) OrElse dado Is Nothing Then
                dado = EntradaDeLodDeTela.ActorSinEstado(d2, esJugador)
            End If
            Dim e = dado.Copia()
            e.DistanciaCuadrada = d2
            e.EsJugador = esJugador
            e.EscalaDeCosto = EscalaDeCosto()
            Dim costos(bloques.Count - 1) As Single?
            For i = 0 To bloques.Count - 1
                costos(i) = LodDeTela.CostoDePrenda(nPasosTela, bloques(i).Where(Function(v) v IsNot Nothing).Select(Function(v) v.TiempoDelPaso).ToArray())   ' 0x140DB3E26 (0x1418A1AC0)
            Next
            Dim r = LodDeTela.Decidir(e, costos, cuenta, costo)
            If r Is Nothing Then Exit Sub                                       ' 0x140DB3A64 / 0x140DB3A7D
            For i = 0 To bloques.Count - 1
                Dim enMundo = r(i).EnElMundo
                Dim antes = bloques(i).Any(Function(v) v IsNot Nothing AndAlso v.EnElMundo)
                If enMundo AndAlso Not antes Then                               ' 0x1418A0DDC cmp rdx, [rsi+0x78]
                    For Each v In bloques(i)
                        If v Is Nothing Then Continue For
                        v.EnElMundo = True                                      ' 0x1418A0E66 / 0x1418A0E7A (0x141871BD0)
                        Enganchar(v.Prenda)                                     ' 0x1418A0EB0-0x1418A0F17
                    Next
                ElseIf Not enMundo AndAlso antes Then
                    For Each v In bloques(i)
                        If v IsNot Nothing Then v.EnElMundo = False             ' 0x1418A0DF8 (0x141871C90) / 0x1418A0E66
                    Next
                End If
                If r(i).EscribeValido Then                                      ' 0x140DB3E08
                    For Each v In bloques(i)
                        Dim ts = v?.ConjuntoDelJob                              ' 0x1418A1B40…0x1418A1B44: [[inst+0x40]]
                        If ts IsNot Nothing Then ts.Valido = r(i).Valido        ' 0x1418A1B47 mov [rcx+0x194], r9d
                    Next
                End If
            Next
            If Logger.Enabled Then
                Dim mq = String.Join(",", r.Select(Function(x) x.Modo)), dq = d2, jq = esJugador
                Logger.LogLazy(Function() $"[CLOTH-LOD] jugador={jq} d²={dq} modos={mq}")
            End If
        End Sub

        Private Shared Sub CorrerPaso(viva As PrendaViva, nodosPorSet As NodosDeEsqueleto(), dt As Single,
                                      esqueleto As SkeletonInstance,
                                      escritosPorEsqueleto As Dictionary(Of SkeletonInstance, Integer))
            Dim p = viva.Prenda
            Dim sets = p.TransformSets
            Dim lista = p.Conjuntos
            Try
                If HavokPhysicsSettings.CorreSimulacion Then MaquinaDeEstados(viva)
                ' Después de la máquina: el estado pudo cambiar (0x141879140 / 0x1418795F8 recargan r14).
                Dim usados = SetsDelEstado(p)
                ' El tiempo del paso: `0x1418791B5` marca de ciclos (`0x141605EE0` rdtsc) antes del BeginAccess,
                ' `0x1418792FB` otra después de la cadena, `0x141879311` `0x1415F6AD0`: `(double)Δ / (double)frecuencia`
                ' (`cvtsi2sd`/`divsd`) → `cvtpd2ps`. ⛔ La app no tiene rdtsc: mide con `Stopwatch` (QPC) y su frecuencia.
                Dim marca = Stopwatch.GetTimestamp()
                For Each idx In usados                                          ' 0x1418C8DB0
                    If idx < 0 OrElse idx >= lista.Length OrElse idx >= sets.Length OrElse lista(idx) Is Nothing Then Continue For
                    lista(idx).AbrirAcceso(sets(idx), nodosPorSet(idx), p)      ' 0x1418C8DE0 call [r9+0x20]
                Next
                p.Cuadro(dt, 0)                                                 ' 0x1418C5DE0 por operador
                Dim delta = Stopwatch.GetTimestamp() - marca                    ' 0x1418792FB…0x141879303
                viva.TiempoDelPaso = CSng(CDbl(delta) / CDbl(Stopwatch.Frequency))   ' 0x1415F6AD6…AE4 → [job+0x34] (0x141879326) → [inst+0x1C] (0x1418C8F0D)
                Dim e = 0
                For Each idx In usados                                          ' 0x1418C9070
                    If idx < 0 OrElse idx >= lista.Length OrElse idx >= sets.Length OrElse lista(idx) Is Nothing Then Continue For
                    e += lista(idx).CerrarAcceso(sets(idx), {p.Estado}, nodosPorSet(idx))   ' 0x1418C908C call [rax+0x28]
                Next
                Dim prev = 0
                escritosPorEsqueleto.TryGetValue(esqueleto, prev)
                escritosPorEsqueleto(esqueleto) = prev + e
                If e > 0 AndAlso Logger.Enabled Then
                    For Each idx In usados
                        If idx < 0 OrElse idx >= lista.Length OrElse lista(idx) Is Nothing Then Continue For
                        InstrumentoDeCapa(lista(idx), nodosPorSet(idx), e, idx)
                    Next
                End If
            Catch ex As Exception
                Dim exL = ex
                Logger.LogLazy(Function() $"[CLOTH-CANON] el paso fallo: {exL}")
            End Try
        End Sub

        ''' <summary>INSTRUMENTO: ¿el mundo con el que dibuja la app es el W del cierre?</summary>
        Private Shared Sub InstrumentoDeCapa(conjunto As ConjuntoDeTransforms, nodos As NodosDeEsqueleto, e As Integer, idxSet As Integer)
            Try
                If True Then
                    Dim maxT = 0.0F, maxR = 0.0F, peor = -1
                    For ib = 0 To conjunto.NumHuesos - 1
                        Dim wc As NiTransformDelMotor
                        If Not conjunto.MundoDelCierre(ib, wc) Then Continue For
                        Dim ga = nodos.Mundo(ib)
                        If ga Is Nothing Then Continue For
                        Dim wa = NiTransformDelMotor.DeTransform(ga)
                        Dim difT = Vector128.Subtract(wa.T, wc.T).WithElement(3, 0.0F)
                        Dim t = MathF.Sqrt(Vector128.Dot(difT, difT))
                        Dim r = MathF.Abs(wa.F0.GetElement(0) - wc.F0.GetElement(0)) + MathF.Abs(wa.F1.GetElement(1) - wc.F1.GetElement(1)) +
                                MathF.Abs(wa.F2.GetElement(2) - wc.F2.GetElement(2)) + MathF.Abs(wa.F0.GetElement(1) - wc.F0.GetElement(1)) +
                                MathF.Abs(wa.F1.GetElement(2) - wc.F1.GetElement(2)) + MathF.Abs(wa.F2.GetElement(0) - wc.F2.GetElement(0))
                        If t > maxT Then maxT = t : peor = ib
                        If r > maxR Then maxR = r
                    Next
                    Dim tq = maxT, rq = maxR, pq = peor
                    Logger.LogLazy(Function() $"[CLOTH-CAPAvsW] |app − W| traslacion max={tq:0.#####} (hueso {pq}) · rotacion max={rq:0.#####}")
                    Dim eq = e, nq = nodos.SinBase, lq = conjunto.LeidosSinNodo, sq = idxSet, cq = nodos.Colgados
                    Logger.LogLazy(Function() $"[CLOTH-CANONCAPA] set {sq} · escritos={eq} · sin base local={nq} · leidos sin nodo={lq} · colgados={cq}")
                End If
            Catch ex As Exception
                Dim exL = ex
                Logger.LogLazy(Function() $"[CLOTH-CANON] el paso fallo: {exL}")
            End Try
        End Sub

        ''' <summary>
        ''' El job de tela — `0x141879109`-`0x14187961A`, ANTES de correr la cadena del estado.
        ''' <para>```
        ''' valido = ts[+0x194]                                     ' 0x141879109
        ''' si ShouldSwitchState(ts):                               ' 0x141879113 (0x1418A79F0)
        '''   si no valido:                                         ' 0x14187911C
        '''     si estado = Simulate: setCurrentState(Animate); ts.banderas = 1   ' 0x14187913B / 0x141879153
        '''     fase = 0                                            ' 0x141879158
        '''   si no, si estado = Animate:                           ' 0x141879405
        '''     fase −1 ⇒ 0                                         ' 0x14187941C
        '''     fase  0 ⇒ previous = positions; siembra transferencia; fase = 1   ' 0x1418794D0 / 0x1418794EA / 0x141879504
        '''     fase  1 ⇒ ts[+0x1B6] = 1; rebase de previous; setCurrentState(Simulate); fase = 0
        '''                                                         ' 0x141879516 / 0x1418795BA / 0x1418795F3 / 0x14187960A
        ''' ```</para>
        ''' </summary>
        Private Shared Sub MaquinaDeEstados(viva As PrendaViva)
            Dim ts = viva.ConjuntoDelJob, p = viva.Prenda                       ' 0x141879036: [[inst+0x40]]
            If ts Is Nothing Then Exit Sub
            Dim valido = ts.Valido
            If Not ts.PuedeCambiarDeEstado(BAnimClothLOD) Then Exit Sub         ' 0x14187911A je
            Dim estado = p.EstadoActual                                         ' ebx = [inst+0x18] (0x14187900B)
            If Not valido Then
                If estado = ts.IndiceSimulate Then                              ' 0x141879124
                    p.FijarEstado(ts.IndiceAnimate)                             ' 0x14187913B
                    ts.ReiniciarBanderas()                                      ' 0x141879153
                End If
                ts.Fase = 0                                                     ' 0x141879158
                Exit Sub
            End If
            If estado <> ts.IndiceAnimate Then Exit Sub                         ' 0x141879405
            Select Case ts.Fase
                Case -1                                                         ' 0x14187941C
                    ts.Fase = 0
                Case 0                                                          ' 0x141879424
                    p.PreviasIgualAPosiciones()                                 ' 0x1418794B6-0x1418794E2
                    p.SembrarTransferencia()                                    ' 0x1418794EA
                    ts.Fase = 1                                                 ' 0x141879504
                Case Else
                    ts.Teletransporte = True                                    ' 0x141879516
                    p.RebasarPrevias()                                          ' 0x1418795BA
                    p.FijarEstado(ts.IndiceSimulate)                            ' 0x1418795F3
                    ts.Fase = 0                                                 ' 0x14187960A
            End Select
            If Logger.Enabled Then
                Dim fq = ts.Fase, eq = estado, nq = p.EstadoActual
                Logger.LogLazy(Function() $"[CLOTH-JOB] estado {eq}→{nq} · fase → {fq} · Animate={ts.IndiceAnimate} Simulate={ts.IndiceSimulate}")
            End If
        End Sub

        Private Shared Sub PrepararBloque(bloque As BSClothExtraData, skeleton As SkeletonInstance,
                                          trabajo As List(Of (Viva As PrendaViva, Nodos As NodosDeEsqueleto(), Esqueleto As SkeletonInstance)),
                                          esJugador As Boolean)
            Dim pkg As HclClothPackageGraph_Class = Nothing
            SyncLock _paquete
                If Not _paquete.TryGetValue(bloque, pkg) Then
                    pkg = HclClothPackageParser_Class.Parse(HkxPackfileParser_Class.Parse(bloque))
                    If pkg Is Nothing Then Exit Sub
                    _paquete(bloque) = pkg
                End If
            End SyncLock

            Dim prendas As PrendaViva() = Nothing
            SyncLock _prendas
                If Not _prendas.TryGetValue(bloque, prendas) OrElse prendas Is Nothing OrElse
                   prendas.Length <> pkg.ClothConfigs.Count Then
                    prendas = New PrendaViva(Math.Max(0, pkg.ClothConfigs.Count - 1)) {}
                    _prendas(bloque) = prendas
                End If
            End SyncLock

            For ci = 0 To pkg.ClothConfigs.Count - 1
                Dim cfg = pkg.ClothConfigs(ci)
                If cfg Is Nothing OrElse cfg.ClothData Is Nothing Then Continue For
                If prendas(ci) Is Nothing Then
                    ' ---- 0x1418A36F0: un BSTransformSet por `transformSetDefinitions[i]`, con el
                    ' `hkaSkeleton` raíz cuyo nombre es prefijo del de la definición.
                    Dim esqueletosRaiz = ConjuntoDeTransforms.EsqueletosRaiz(pkg.Graph)
                    Dim nDefs = cfg.ClothData.Raw.TransformSetDefinitionsCount         ' 0x1418A3719 [cd+0x40]
                    Dim esqueletos(Math.Max(0, nDefs) - 1) As Havok.Canon.Objects.HkObj_HkaSkeleton
                    Dim sinEsqueleto = -1
                    For i = 0 To nDefs - 1
                        Dim crudoD = cfg.ClothData.Raw.TransformSetDefinitionsRef(i)   ' 0x1418A3751 [cd+0x38][i]
                        Dim def = If(crudoD Is Nothing, Nothing,
                                     Havok.Canon.Objects.HkObj_HclTransformSetDefinition.Leer(cfg.ClothData.Graph, crudoD))
                        Dim casan = ConjuntoDeTransforms.EsqueletosDeLaDefinicion(esqueletosRaiz, def?.Name)
                        If casan.Count > 1 AndAlso Logger.Enabled Then
                            Dim nq = def?.Name, cq = casan.Count
                            Logger.LogLazy(Function() $"[CLOTH-TS] '{nq}' casa con {cq} esqueletos: el motor toma el primero del BSTSet (orden por hash, no transcripto); la app toma el primero del contenedor")
                        End If
                        Dim s = If(casan.Count = 0, Nothing, casan(0))
                        If s Is Nothing OrElse s.Bones Is Nothing Then
                            sinEsqueleto = i
                            Exit For
                        End If
                        esqueletos(i) = s
                    Next
                    If nDefs = 0 OrElse sinEsqueleto >= 0 Then
                        Dim iq = sinEsqueleto, dq = nDefs
                        Logger.LogLazy(Function() $"[CLOTH-TS] definiciones={dq} · la {iq} no tiene hkaSkeleton que case: el motor pasa NULO a 0x1418C85F0 ([r8+0xA] sin guarda) — la prenda queda SIN fisica")
                        Continue For
                    End If
                    Dim nombresPorSet(nDefs - 1) As List(Of String)
                    Dim huesosPorSet(nDefs - 1) As Integer
                    For i = 0 To nDefs - 1
                        nombresPorSet(i) = esqueletos(i).Bones.Select(Function(bn) If(bn Is Nothing, Nothing, bn.Name)).ToList()
                        huesosPorSet(i) = nombresPorSet(i).Count                 ' 0x1418A413F [skel+0x30]
                    Next

                    ' La instancia nace en el estado 0 (`0x1418ECFA2`). En `DeformOnly` (selector de
                    ' depuración) se fija el estado sin simulador y la máquina no corre.
                    Dim inicial = If(HavokPhysicsSettings.CorreSimulacion, Nothing, EstadoSinSimulador(cfg))
                    Dim p = Havok.Motor.PrendaSimulada.Crear(cfg.ClothData, inicial, nombresPorSet(0), huesosPorSet)
                    If p Is Nothing Then Continue For
                    Sembrar(p, _referencia)                                     ' 0x1418A0A4F-0x1418A0AB6, antes de los sets

                    Dim tipo17 = HuesosDeTipo17(cfg)                            ' 0x1418A38A7-0x1418A3907
                    Dim lista(nDefs - 1) As ConjuntoDeTransforms
                    Dim armado = True
                    For i = 0 To nDefs - 1
                        Dim nodos As New NodosDeEsqueleto(skeleton, nombresPorSet(i))
                        Dim c = ConjuntoDeTransforms.Crear(cfg.ClothData.ClothStateDatas, esqueletos(i).ParentIndices, huesosPorSet(i))
                        If c Is Nothing Then armado = False : Exit For
                        ' +0x140 = qs(referencia con S = 1) — 0x1418A360B + 0x1418A413A (0x14082B4D0)
                        Dim refS1 = _referencia
                        refS1.S = 1.0F
                        c.Raiz = ConjuntoDeTransforms.QsDelNodo(refS1)
                        c.FijarRaiz(nodos, esqueletos(i).ReferencePose)          ' 0x1418A4540-0x1418A4F79
                        c.FijarCreados(nodos, tipo17)                           ' 0x1418A45EE-0x1418A46B1
                        lista(i) = c                                            ' 0x1418A39AA (0x1418C85F0)
                    Next
                    If Not armado Then Continue For
                    p.Conjuntos = lista

                    ' La acción de viento — 0x1418A0BB4 (0x141878540) + 0x1418A0BC2 (addAction) + 0x1418A0BC7.
                    Dim viento = Havok.Motor.Viento.AccionParaUnaPrenda()
                    p.AccionesDeLaInstancia.Add(viento)
                    viento.Encendida = True
                    If Logger.Enabled Then
                        Dim mq = p.Sim.TotalMass, nq = p.Estado.Normales IsNot Nothing, aq = p.AccionesSinTranscribir, sq = nDefs
                        Logger.LogLazy(Function() $"[CLOTH-VIENTO] totalMass={mq} · normales={nq} · acciones del archivo sin transcribir={aq} · BSTransformSet={sq}")
                    End If
                    Enganchar(p)                                                ' 0x1406F1484 → 0x1418A0DA0
                    ' `0x1406F145F` → `0x1418A1B60`: `[[ci+0x40]][+0x1B7] = (actor == jugador)`, sólo al
                    ' set 0 (`0x1418A1B8D mov rcx, [rdx]`), una vez al crear.
                    lista(0).Asentar = esJugador
                    prendas(ci) = New PrendaViva With {.Prenda = p, .NombresPorSet = nombresPorSet}
                End If
                Dim viva = prendas(ci)
                Dim nodosPorSet(viva.NombresPorSet.Length - 1) As NodosDeEsqueleto
                For i = 0 To nodosPorSet.Length - 1
                    nodosPorSet(i) = New NodosDeEsqueleto(skeleton, viva.NombresPorSet(i))
                Next
                trabajo.Add((viva, nodosPorSet, skeleton))
            Next
        End Sub

        ''' <summary>
        ''' La lista que arma el llamador del constructor — `0x1418A38A7`-`0x1418A3907`: por cada
        ''' operador de `clothData.operators` con `type = 17` (`hclSimpleMeshBoneDeformOperator`),
        ''' `triangleBonePairs[k].boneOffset &gt;&gt; 6` (`0x1418A38C7 shr di, 6`: el offset es en bytes,
        ''' 64 por hueso).
        ''' </summary>
        Private Shared Function HuesosDeTipo17(cfg As HclClothConfigGraph_Class) As List(Of Integer)
            Dim r As New List(Of Integer)
            Dim raw = cfg.ClothData.Raw
            For idx = 0 To raw.OperatorsCount - 1
                Dim crudo = raw.OperatorsRef(idx)
                If crudo Is Nothing Then Continue For
                Dim op = Havok.Canon.Objects.HkObj_HclSimpleMeshBoneDeformOperator.Leer(cfg.ClothData.Graph, crudo)
                If op?.TriangleBonePairs Is Nothing Then Continue For
                For Each par In op.TriangleBonePairs
                    If par IsNot Nothing Then r.Add(CInt(par.BoneOffset) >> 6)
                Next
            Next
            Return r
        End Function

        ''' <summary>Limpia la capa de física del esqueleto.</summary>
        Public Shared Sub LimpiarCapa(skeleton As SkeletonInstance)
            If skeleton Is Nothing Then Exit Sub
            skeleton.ResetPhysics()
        End Sub

        ''' <summary>Limpia la capa en TODO esqueleto que esta simulación haya tocado.</summary>
        Friend Shared Sub LimpiarTodos()
            For Each kv In _tocados
                kv.Key?.ResetPhysics()
            Next
            _tocados.Clear()
        End Sub

        ''' <summary>Tira el estado vivo y el reloj. Lo usan los arneses entre corridas: sin esto, la
        ''' segunda corrida arranca de las partículas que dejó la primera y el A/A no compara nada.</summary>
        Public Shared Sub ResetAll()
            SyncLock _prendas
                _prendas.Clear()
            End SyncLock
            RelojDelMundo.Reiniciar()
            _msAnterior = -1
        End Sub

        ''' <summary>
        ''' `DeformOnly` (selector de depuración, no ley del motor): el primer `hclClothState` cuya
        ''' cadena no tiene `hclSimulateOperator`. Si todos lo tienen, Nothing (el 0).
        ''' </summary>
        Private Shared Function EstadoSinSimulador(cfg As HclClothConfigGraph_Class) As Havok.Canon.Objects.HkObj_HclClothState
            Dim estados = cfg.ClothData.ClothStateDatas
            If estados Is Nothing OrElse estados.Count = 0 Then Return Nothing
            For Each est In estados
                If est Is Nothing OrElse est.Operators Is Nothing Then Continue For
                Dim tiene = False
                For Each iu In est.Operators
                    Dim idx = CInt(iu)
                    If idx < 0 OrElse idx >= cfg.ClothData.Raw.OperatorsCount Then Continue For
                    Dim crudo = cfg.ClothData.Raw.OperatorsRef(idx)
                    If crudo IsNot Nothing AndAlso
                       Havok.Canon.Objects.HkObj_HclSimulateOperator.Leer(cfg.ClothData.Graph, crudo) IsNot Nothing Then
                        tiene = True
                        Exit For
                    End If
                Next
                If Not tiene Then Return est
            Next
            Return Nothing
        End Function

        ''' <summary>
        ''' El transform de la REFERENCIA — `0x140510750`: rotación de los ángulos de la ref
        ''' (`0x140510690`), traslación `refr+0xD0..+0xD8` y escala `GetScale` (`0x1404FF850`).
        ''' <para>La app dibuja al actor en el origen, sin giro y con escala 1: el pipeline de render
        ''' no tiene ningún transform de referencia (buscado en `FO4_Base_Library`), así que la
        ''' referencia es la identidad.</para>
        ''' </summary>
        Private Shared ReadOnly _referencia As NiTransformDelMotor = ReferenciaEnElOrigen()

        Private Shared Function ReferenciaEnElOrigen() As NiTransformDelMotor
            Dim r = NiTransformDelMotor.DeTransform(New Transform_Class())
            r.S = 1.0F
            Return r
        End Function

        ''' <summary>
        ''' LA SIEMBRA — `0x1418A09BA`-`0x1418A0AB6` + `hclClothInstance::create` (`0x1418ECFF0`).
        ''' <para>```
        ''' q = normalizar(quat(referencia))                       ' 0x1418A09BA (0x1416CD820) … 0x1418A0A2D
        ''' T = referencia.T con w = 0                              ' 0x1418A0A3C andps
        ''' M = (filas de matrizDe(q) CON sus w, T)                  ' 0x1418A0A5C (0x141365A80), sin escala
        ''' siembra = todo sim-cloth trae simClothPoses              ' 0x1418A0A80…AA0 ([sim+0xE0] ≠ 0)
        ''' positions[i] = previous[i] = ((p.y·M1 + p.x·M0) + p.z·M2) + M3   ' 0x1418ED0C9 / 0x1418ED0E0 (0x141339F90)
        ''' UpdateAABBs                                             ' 0x1418ED0F9 (0x1418C73C0)
        ''' ```</para>
        ''' </summary>
        Private Shared Sub Sembrar(prenda As Havok.Motor.PrendaSimulada, referencia As NiTransformDelMotor)
            Dim m = MatrizDeReferencia(referencia)
            ComponerColisionables(prenda, m)
            Dim poses = prenda.Sim.SimClothPoses
            If poses Is Nothing OrElse poses.Count = 0 Then Exit Sub                          ' [sim+0xE0] = 0
            If poses(0) Is Nothing OrElse poses(0).Positions Is Nothing Then Exit Sub
            Dim pp = poses(0).Positions
            For i = 0 To Math.Min(pp.Count, prenda.Estado.NumParticulas) - 1                ' i < data.particleDatas
                Dim p = Havok.Motor.Fachada.V4(pp(i))
                Dim v = Vector128.Add(Vector128.Multiply(Havok.Motor.Simd.BcastY(p), m.F1),
                                      Vector128.Multiply(Havok.Motor.Simd.BcastX(p), m.F0))  ' 0x141339F9E…B1
                v = Vector128.Add(v, Vector128.Multiply(Havok.Motor.Simd.BcastZ(p), m.F2))   ' 0x141339FB4
                v = Vector128.Add(v, m.F3)                                                  ' 0x141339FB7
                Havok.Motor.Simd.Escribir(prenda.Estado.Posiciones, i, v)
                Havok.Motor.Simd.Escribir(prenda.Estado.Previas, i, v)
            Next
            Havok.Motor.Aabb.ActualizarAabbs(prenda.Estado)                                 ' 0x1418ED0F9
        End Sub

        ''' <summary>
        ''' `M` de la siembra — `0x1418A0A2D`-`0x1418A0A6E`: filas 0-2 de `matrizDe(q)` (`0x141365A80`,
        ''' con sus `w`) y fila 3 = traslación de la referencia con `w = 0` (`0x1418A0A3C andps` con
        ''' `0x142924BE0`, escrita en `0x1418A0A6E`). Es la MISMA matriz que viaja a `create`
        ''' (`0x1418A0AB6`) y de ahí al constructor del sim-cloth como `r8` (`0x1418C65C0`, guardada en
        ''' `[rsp+0x18]` y releída en `0x1418C687A`); medido en la emulación: los 16 floats coinciden.
        ''' </summary>
        Private Shared Function MatrizDeReferencia(referencia As NiTransformDelMotor) As Havok.Motor.Mat4
            Dim q = ConjuntoDeTransforms.QsDelNodo(referencia).R
            Dim r3 = Havok.Motor.Cuaternion.AMatriz(q)                                        ' 0x141365A80, con sus w
            Dim m As Havok.Motor.Mat4
            m.F0 = r3.F0 : m.F1 = r3.F1 : m.F2 = r3.F2
            m.F3 = referencia.T.WithElement(3, 0.0F)                                           ' 0x1418A0A3C
            Return m
        End Function

        ''' <summary>
        ''' Los colisionables de la instancia — constructor del sim-cloth `0x1418C65C0`, lazo
        ''' `0x1418C6890`-`0x1418C6907`: `clone(perInstanceCollidables[i])` (`0x1418C689E`,
        ''' copia el transform del archivo en `+0x20..+0x5F`, `0x1419608A6`-`0x1419608CA`) y después
        ''' `transform = Componer(M, transformDelArchivo)` (`0x1418C68DF` → `0x141298180`, `rcx =
        ''' col+0x20`, `rdx = M`, `r8 = archivo+0x20`).
        ''' <para>⛔ No es un adorno: el archivo trae `w` distintas de cero en las filas 0, 1 y 3, y
        ''' `Componer` las recalcula (`B.y·M1.w + B.x·M0.w + B.z·M2.w`, más `M3.w = 0` en la fila 3).
        ''' Sin esto las lanes 3, 7 y 15 del colisionable diferían del `.exe` en las llamadas 0-1.</para>
        ''' </summary>
        Private Shared Sub ComponerColisionables(prenda As Havok.Motor.PrendaSimulada, m As Havok.Motor.Mat4)
            Dim cols = prenda.Colisionadores
            If cols Is Nothing Then Exit Sub
            For Each c In cols
                If c Is Nothing Then Continue For
                c.Transform = Havok.Motor.Mat4.Componer(m, c.Transform)                      ' 0x1418C68DF
            Next
        End Sub

        ''' <summary>Los nodos vivos de un esqueleto, por índice del `hkaSkeleton` de la prenda.</summary>
        Friend NotInheritable Class NodosDeEsqueleto
            Implements INodosDeTela

            Private ReadOnly _huesos As HierarchiBone_class()
            Private ReadOnly _indicePorHueso As New Dictionary(Of HierarchiBone_class, Integer)
            ''' <summary>Nodos escritos cuya pose local sin física no existe.</summary>
            Friend SinBase As Integer

            Friend Sub New(skeleton As SkeletonInstance, nombres As List(Of String))
                ReDim _huesos(nombres.Count - 1)
                For i = 0 To nombres.Count - 1
                    Dim nm = nombres(i)
                    If String.IsNullOrWhiteSpace(nm) Then Continue For
                    Dim b As HierarchiBone_class = Nothing
                    If skeleton.SkeletonDictionary.TryGetValue(nm.Trim(), b) AndAlso b IsNot Nothing Then
                        _huesos(i) = b
                        If Not _indicePorHueso.ContainsKey(b) Then _indicePorHueso(b) = i
                    End If
                Next
            End Sub

            Private Function Hueso(i As Integer) As HierarchiBone_class
                If i < 0 OrElse i >= _huesos.Length Then Return Nothing
                Return _huesos(i)
            End Function

            Public Function Mundo(indice As Integer) As Transform_Class Implements INodosDeTela.Mundo
                Return Hueso(indice)?.GetGlobalTransform
            End Function

            Public Function IndiceDelPadreNi(indice As Integer) As Integer Implements INodosDeTela.IndiceDelPadreNi
                Dim c As Integer
                If _colgados.TryGetValue(indice, c) Then Return c
                Dim p = Hueso(indice)?.Parent
                If p Is Nothing Then Return -1
                Dim r As Integer
                Return If(_indicePorHueso.TryGetValue(p, r), r, -1)
            End Function

            Public Function MundoDelPadreNi(indice As Integer) As Transform_Class Implements INodosDeTela.MundoDelPadreNi
                Dim c As Integer
                If _colgados.TryGetValue(indice, c) Then Return Mundo(c)
                Return Hueso(indice)?.Parent?.GetGlobalTransform
            End Function

            ''' <summary>El local del motor como capa de la app: `LocaLTransform = base ∘ delta` ⇒
            ''' `delta = base⁻¹ ∘ local`.
            ''' <para>⛔ Un hueso COLGADO por el cierre (`0x1418A6F5A`) sigue siendo raíz en el
            ''' esqueleto de la app, donde mundo = local: lo que se le escribe es el mundo que
            ''' `NiAVObject::UpdateWorldData` (`0x1416C8AC0`) le deriva después del `AttachChild`,
            ''' `0x140344820(W(padre), local)` (`ConjuntoDeTransforms.ActualizarMundoDelNodo`).</para>
            ''' <para>⛔ Las ramas de `+0x100` (objeto de colisión, `0x1416C8ACE`-`AF0`) y del bit 45 de
            ''' `+0x108` (`0x1416C8BE7`) no se transcriben porque NO SE ALCANZAN: el barrido de las 632
            ''' prendas con tela del corpus (1968 pasadas de la capa, 14-sep) mide `colgados=0` en todas
            ''' (`[CLOTH-CANONCAPA]`). Si alguna vez aparece un colgado, el log lo muestra.</para></summary>
            Public Sub EscribirLocal(indice As Integer, local As NiTransformDelMotor) Implements INodosDeTela.EscribirLocal
                Dim b = Hueso(indice)
                If b Is Nothing Then Exit Sub
                Dim baseLocal = b.LocaLTransformWithoutPhysics
                If baseLocal Is Nothing Then
                    SinBase += 1
                    Exit Sub
                End If
                Dim deseado = local.ATransform()
                Dim c As Integer
                If _colgados.TryGetValue(indice, c) AndAlso b.Parent Is Nothing Then
                    Dim wp = Mundo(c)
                    If wp IsNot Nothing Then
                        deseado = ConjuntoDeTransforms.ActualizarMundoDelNodo(NiTransformDelMotor.DeTransform(wp), local, False).ATransform()
                    End If
                End If
                b.PhysicsDeltaTransform = baseLocal.Inverse().ComposeTransforms(deseado)
            End Sub

            ''' <summary>Los huesos que el cierre colgó (`AttachChild`, `0x1418A6F5A`) → su padre.</summary>
            Private ReadOnly _colgados As New Dictionary(Of Integer, Integer)

            ''' <summary>INSTRUMENTO: cuántos huesos del esqueleto colgó el cierre.</summary>
            Friend ReadOnly Property Colgados As Integer
                Get
                    Return _colgados.Count
                End Get
            End Property

            Public Sub Colgar(indice As Integer, padre As Integer) Implements INodosDeTela.Colgar
                _colgados(indice) = padre
            End Sub
        End Class

    End Class

End Namespace
