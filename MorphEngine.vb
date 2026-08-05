Imports OpenTK.Mathematics

''' <summary>
''' A single morph channel: a named set of vertex deltas with a weight.
''' Produced by IMorphResolver, consumed by MorphEngine.
''' </summary>
Public Class MorphChannel
    Public Property Name As String
    Public Property Weight As Single = 0
    Public Property Deltas As List(Of MorphData)
    Public Property IsZap As Boolean = False

    ''' <summary>⭐ True cuando este canal lo aplica LA RUTINA DE MORPH DEL MOTOR (el applier nativo de
    ''' BSFaceGenMorphData), que VALIDA el peso contra [-1,1] y, fuera de rango, <b>ABORTA EL CANAL ENTERO
    ''' — no clampea</b>. Fijado al CONSTRUIR el canal, por ORIGEN: no es por juego ni global.
    ''' <para>VERIFICADO POR DESENSAMBLADO en los tres binarios:</para>
    ''' <list type="bullet">
    ''' <item>SSE — el check vive DENTRO del applier único <c>SkyrimSE.exe 0x140430190</c>
    ''' (<c>0x1404301DF comiss/jb</c> vs -1.0 @RVA 0x1769578, <c>0x1404301EC comiss/ja</c> vs +1.0
    ''' @RVA 0x1ad2870; ambos saltan a la salida 0x1404305CF). El applier se alcanza por UN solo thunk
    ''' (0x14042FCA7) ⇒ en Skyrim NO existe camino sin validar: NAM9, NAMA, race base, VampireMorph y
    ''' SkinnyMorph pasan todos por ahí.</item>
    ''' <item>FO4 / CK — el check está en el loop per-canal (<c>Fallout4.exe 0x1406E54E6</c> /
    ''' <c>CreationKit.exe 0x140EC8670</c>): <c>0x1406E551F comiss/jb</c> (-1.0 @0x14291FF90) y
    ''' <c>0x1406E5524 comiss/ja</c> (+1.0 @0x14291FC98) antes de llamar al applier 0x1406E5590.</item>
    ''' </list>
    ''' <para><b>Inclusive en ±1</b>: <c>jb</c>/<c>ja</c> saltan sólo ESTRICTAMENTE fuera. NaN aborta también
    ''' (comiss deja CF=1 en unordered ⇒ el <c>jb</c> se toma).</para>
    ''' <para>⛔ False SÓLO para los canales de RaceMenu (skee64), que NO usan el applier del motor sino su
    ''' propio <c>TRIFile::Apply</c> (SKSE64Plugins-master\skee64\FaceMorphInterface.cpp:216-246, :1115-1119;
    ''' SKEEHooks.cpp:736-741), SIN validación de rango — y que además tienen una descomposición DELIBERADA
    ''' para |v|&gt;1 (FaceMorphInterface.cpp:1156-1163 parte 2.5 en 1.0+1.0+0.5 preservando la magnitud).
    ''' Descartarlos revertiría ese mecanismo a propósito diseñado para saltarse el límite.</para></summary>
    Public Property EngineApplied As Boolean = True

    ''' <summary>
    ''' True cuando este canal debe pasar por el gate de bloques de 4 índices del CK
    ''' (ver <see cref="MorphEngine.ApplyChannelsToVertexArray"/>). Esa es la ley de selección con la
    ''' que el CK aplica los morphs de CABEZA de un <c>.tri</c> de FaceGen — NO es una ley universal.
    '''
    ''' ⛔ False para los canales cuyo origen es un <c>.osd</c> de BodySlide: ahí no interviene ningún
    ''' applier del motor, la geometría la hornea BodySlide y <c>ApplySliders</c> aplica TODO diff sin
    ''' gate (BodySlideApp.cpp:1338-1347). Con el gate prendido, un slider de detalle cuyos deltas
    ''' viven entre 1e-4 y 1e-2 no se veía en el viewport pero sí quedaba en el NIF construido.
    '''
    ''' Default True: preserva el comportamiento de todos los emisores existentes.
    ''' </summary>
    Public Property ApplyCkBlockGate As Boolean = True

    ''' <summary>
    ''' Canal de CLAMP de BodySlide: NO es un morph. Se aplica en un SEGUNDO PASE, despues de todos los
    ''' canales normales, y con ASIGNACION ABSOLUTA — <c>verts[i] = delta</c>, sin sumar y sin escalar
    ''' por el peso (DiffDataSets::ApplyClamp, DiffData.cpp:534-537).
    ''' ⚠️ El gate NO vive aca: el emisor decide si emite el canal, y lo hace contra el DEFAULT crudo
    ''' del slider para el peso que se construye (<c>defBigValue &gt; 0</c>, BodySlideApp.cpp:4406/:4410),
    ''' no contra el valor vivo. El <c>Weight &lt;= 0</c> de abajo es solo defensa: los emisores mandan 1.0.
    ''' </summary>
    Public Property IsClamp As Boolean = False

    ''' <summary>
    ''' Canal de SLIDER UV de BodySlide: NO mueve vertices, mueve el array de UVs.
    ''' <c>DiffDataSets::ApplyUVDiff</c> (DiffData.cpp:458-487) ACUMULA
    ''' <c>uvs[i].u += diff.x * percent ; uvs[i].v += diff.y * percent</c>, con <c>percent == 0</c>
    ''' como unico early-out y sin umbral de magnitud. La Z del delta se descarta.
    ''' Un canal con esta marca NO entra a <see cref="MorphEngine.ApplyChannelsToVertexArray"/>:
    ''' sumar sus deltas a posiciones deformaba la malla.
    ''' </summary>
    Public Property IsUvMorph As Boolean = False

    Sub New(name As String, weight As Single, deltas As List(Of MorphData), Optional isZap As Boolean = False,
            Optional engineApplied As Boolean = True, Optional applyCkBlockGate As Boolean = True,
            Optional isClamp As Boolean = False, Optional isUvMorph As Boolean = False)
        Me.Name = name
        Me.Weight = weight
        Me.Deltas = deltas
        Me.IsZap = isZap
        Me.EngineApplied = engineApplied
        Me.ApplyCkBlockGate = applyCkBlockGate
        Me.IsClamp = isClamp
        Me.IsUvMorph = isUvMorph
    End Sub
End Class

''' <summary>
''' A complete morph plan for one shape: all active channels with their weights and deltas.
''' The engine doesn't know WHERE these came from (sliders, face morphs, expressions, etc.)
''' </summary>
Public Class MorphPlan
    Public Property Channels As New List(Of MorphChannel)
    Public ReadOnly Property HasMorphs As Boolean
        Get
            Return Channels.Count > 0
        End Get
    End Property
    Public ReadOnly Property HasZaps As Boolean
        Get
            Return Channels.Any(Function(c) c.IsZap)
        End Get
    End Property
    Public ReadOnly Property HasUvMorphs As Boolean
        Get
            Return Channels.Any(Function(c) c.IsUvMorph)
        End Get
    End Property
End Class

''' <summary>
''' Resolves morph data for a shape. Consumers implement this to produce morph plans
''' from their specific data sources (WM sliders, NPC face morphs, expressions, etc.)
''' </summary>
Public Interface IMorphResolver
    ''' <summary>
    ''' Build a morph plan for the given shape. Called once per shape per render update.
    ''' Return an empty MorphPlan (no channels) if no morphs apply.
    ''' <para>CONCURRENCIA: el pipeline invoca esto en paralelo para shapes DISTINTAS
    ''' (<c>PipelineStep_Morphs</c> usa <c>Parallel.ForEach</c>). La implementación debe ser
    ''' thread-safe para llamadas concurrentes con shapes distintas: campos compartidos mutables
    ''' (p.ej. cachés de .tri por path) deben protegerse con <c>SyncLock</c> o estructuras
    ''' concurrentes. El estado per-shape (escribir el geo recibido) es seguro porque cada shape
    ''' trae su propio geo. NO se garantiza no-concurrencia para la MISMA shape (el pipeline
    ''' procesa cada shape una sola vez por update).</para>
    ''' </summary>
    Function ResolveMorphPlan(shape As IRenderableShape, geom As SkinnedGeometry) As MorphPlan
End Interface

''' <summary>Proveedor de la <b>geometria BASE pre-skin</b> de un shape: el array del que parte
''' <see cref="MorphEngine.ApplyMorphPlan"/> para aplicar los canales de morph.
''' <para><b>Para que existe.</b> Normalmente la base es la del NIF y la establece
''' <c>SkinningHelper.ExtractSkinnedGeometry</c> al cargar, pero hay casos donde la base correcta NO es la del
''' archivo: en FO4 el motor y el CK no dibujan la malla <c>_faceBones</c> de una head part, la usan como INSUMO
''' para calcular las posiciones de la malla PLANA y dibujan esa. Para replicarlo hay que entregar la geometria
''' horneada como base del shape plano - no es parchear un buffer aguas abajo, es proveer el valor correcto en
''' el punto donde el pipeline define la base pre-skin.</para>
''' <para><b>Por que NO con un <see cref="IMorphResolver"/>.</b> Un canal de morph pasa por el gate de bloques
''' del CK, que descarta bloques de 4 con delta crudo &lt; 0,01. Ese gate existe para DECODIFICAR un <c>.tri</c>
''' comprimido; geometria calculada no es data de <c>.tri</c> y someterla a esa regla es aplicarla fuera de su
''' dominio, ademas de dejar la malla a medio hornear.</para>
''' <para><b>Contrato.</b> Se invoca EN SERIE al principio de <c>PipelineStep_Morphs</c>, antes del
''' <c>Parallel.ForEach</c> de los resolvers y solo para los shapes dirty. La implementacion tiene que escribir
''' IN PLACE en <c>geom.NifLocalVertices</c> (<see cref="SkinnedGeometry"/> es Structure: mutar los elementos
''' propaga, reasignar el array no); â›” NUNCA leer <c>geom.Vertices</c> -lo reescribe ApplyMorphPlan en cada
''' pasada, o sea que se REALIMENTA- ni <c>geom.PerVertexSkinMatrix</c>, que queda stale dentro de este paso; y
''' ser ABSOLUTA, no incremental, partiendo siempre de una copia pristina propia.</para>
''' <para>Devuelve True si reescribio la base de ese shape (solo informativo).</para></summary>
Public Interface IBaseGeometryProvider
    Function TryProvideBaseGeometry(shape As IRenderableShape, ByRef geom As SkinnedGeometry) As Boolean
End Interface

''' <summary>
''' A geometry modifier that transforms geometry after morphs are applied.
''' Examples: vertex masking, topology compaction (zap removal), etc.
''' </summary>
Public Interface IGeometryModifier
    ''' <summary>Apply this modifier to the geometry. Called in pipeline order after morphs.</summary>
    Sub Apply(shape As IRenderableShape, ByRef geom As SkinnedGeometry)
End Interface

''' <summary>
''' Generic morph engine that applies a MorphPlan to geometry.
''' Does NOT know about sliders, presets, BodySlide, face morphs, or any consumer-specific concepts.
''' Works purely with vertex deltas in NIF local space.
''' </summary>
Public Class MorphEngine

    ''' <summary>
    ''' Pure-math entry point: apply position-morph channels to a vertex buffer and return
    ''' the result, without any of the runtime concerns (dirty flags, mask, world cache,
    ''' TBN recalc) that <see cref="ApplyMorphPlan"/> handles for the live render pipeline.
    '''
    ''' Semantics:
    '''   out[i] = baseVerts[i] + Σ channel.Weight × channel.Deltas[i].PosDiff   for non-zap channels
    ''' Zap channels (channel.IsZap = True) are skipped here — they only make sense for the
    ''' renderable mesh (mask flag), not for an offline bake of vertex positions.
    '''
    ''' Vertex storage uses <see cref="Vector3d"/> (double) to match the runtime pipeline
    ''' (SkinnedGeometry.NifLocalVertices). Morph deltas are <see cref="Vector3"/> (float)
    ''' from the .tri file format; they get implicitly widened to double on the add.
    '''
    ''' Use this from offline bakes / file builders / anything that needs the morph math
    ''' without spinning up a SkinnedGeometry. The runtime renderer goes through ApplyMorphPlan,
    ''' which delegates the inner loop here so the two paths can never drift.
    ''' </summary>
    ''' <summary>
    ''' Restaura las UVs desde <see cref="SkinnedGeometry.BaseUvs_Weight"/>. Es el equivalente exacto
    ''' de partir las posiciones desde <c>NifLocalVertices</c>: <c>ApplyUVDiff</c> ACUMULA, asi que sin
    ''' este reset dos aplicaciones seguidas del mismo slider sumarian dos veces.
    ''' Si no hay base (geometria armada a mano, o un zap que reindexo y cambio el largo) toma la
    ''' instantanea de lo que haya ahora y no toca nada.
    ''' </summary>
    Public Shared Function ResetUvsFromBase(ByRef geom As SkinnedGeometry,
                                            Optional tocados As HashSet(Of Integer) = Nothing) As Boolean
        If geom.Uvs_Weight Is Nothing Then Return False
        If geom.BaseUvs_Weight Is Nothing OrElse geom.BaseUvs_Weight.Length <> geom.Uvs_Weight.Length Then
            geom.BaseUvs_Weight = CType(geom.Uvs_Weight.Clone(), Vector3())
            Return False
        End If
        Dim cambio As Boolean = False
        For i = 0 To geom.Uvs_Weight.Length - 1
            If geom.Uvs_Weight(i) <> geom.BaseUvs_Weight(i) Then
                geom.Uvs_Weight(i) = geom.BaseUvs_Weight(i)
                If tocados IsNot Nothing Then tocados.Add(i)
                cambio = True
            End If
        Next
        Return cambio
    End Function

    ''' <summary>
    ''' Aplica los canales UV del plan sobre <c>geom.Uvs_Weight</c>, replicando
    ''' <c>DiffDataSets::ApplyUVDiff</c> (DiffData.cpp:458-487):
    ''' <code>if (percent == 0) return; ... uvs[i].u += diff.x * percent; uvs[i].v += diff.y * percent;</code>
    ''' <c>Uvs_Weight</c> empaqueta (U, V, peso del primer hueso): la Z NO se toca.
    ''' Devuelve True si escribio algo (el VBO de UV hay que resubirlo).
    ''' </summary>
    Public Shared Function ApplyUvChannels(ByRef geom As SkinnedGeometry, plan As MorphPlan,
                                           Optional tocados As HashSet(Of Integer) = Nothing) As Boolean
        If geom.Uvs_Weight Is Nothing Then Return False
        If plan Is Nothing OrElse Not plan.HasUvMorphs Then Return False
        Dim uvCount = geom.Uvs_Weight.Length
        Dim wrote As Boolean = False
        For Each channel In plan.Channels
            If Not channel.IsUvMorph OrElse channel.Deltas Is Nothing Then Continue For
            Dim t = channel.Weight
            If Single.IsNaN(t) OrElse t = 0.0F Then Continue For
            For Each morph In channel.Deltas
                Dim i = CInt(morph.index)
                If i < 0 OrElse i >= uvCount Then Continue For
                Dim cur = geom.Uvs_Weight(i)
                geom.Uvs_Weight(i) = New Vector3(cur.X + morph.PosDiff.X * t,
                                                 cur.Y + morph.PosDiff.Y * t,
                                                 cur.Z)
                If tocados IsNot Nothing Then tocados.Add(i)
                wrote = True
            Next
        Next
        Return wrote
    End Function

    Public Shared Function ApplyChannelsToVertexArray(baseVerts As Vector3d(), plan As MorphPlan) As Vector3d()
        If baseVerts Is Nothing Then Return Nothing
        Dim count = baseVerts.Length
        Dim verts = baseVerts.ToArray()
        If count = 0 Then Return verts
        If plan Is Nothing OrElse Not plan.HasMorphs Then Return verts

        ' ⭐⭐ LEY DE SELECCIÓN DEL CK — el gate NO es por vértice, es por BLOQUE DE 4 ÍNDICES CONSECUTIVOS.
        ' Para cada bloque b que cubre los vértices 4b..4b+3:
        '     · bloque de COLA (4b+4 > nV): se aplica SIEMPRE, sin mirar magnitud.
        '     · resto: blockmax = max(|PosDiff.X|,|PosDiff.Y|,|PosDiff.Z|) sobre los 4 vértices del bloque.
        '              blockmax >= 0,01 ⇒ se aplica el bloque ENTERO; si no, se saltea ENTERO.
        ' ⛔ El gate usa el delta CRUDO del .tri (int16 × multiplier), NO escalado por el peso del canal.
        '    Probado: diff(w50,w100) y diff(w0,w100) dan conjuntos IDÉNTICOS en las 4 shapes medidas.
        ' Umbral 0,01 acotado empíricamente a (0,00998540 – 0,01009503] — 0,01 es el único valor redondo dentro.
        ' VALIDACIÓN: 6.027 decisiones / 0 errores (experimento BAKETEST de inputs controlados) · 1.455 / 0
        ' (superposición multicanal) · 4.617 instancias sobre ~3.159 NPCs vanilla del CK y 213 mallas distintas
        ' / 0 errores (corpus independiente: los .tri de hair/hairline tienen UN solo morph, así que
        ' CK − malla fuente es el canal gateado puro).
        ' ⛔ El bloque de cola es LOAD-BEARING, no cosmético: 821 bloques parciales se aplican pese a estar bajo
        '    umbral, y 3.407 de 4.617 shapes tienen nV mod 4 <> 0.
        ' Esto REEMPLAZA el viejo skip por-vértice `|delta·peso|² < 0.000001F` (= |delta| < 0,001), que era un
        ' proxy tosco de esta regla: por eso quitarlo EMPEORABA el corpus (694→716 NPCs) — sin él aplicábamos
        ' todavía más deltas que el CK nunca aplica.
        ' Explica todas las paradojas que bloqueaban el caso: v91 (delta 2,5e-05) se aplica porque comparte el
        ' bloque 88-91 con v88 (2,1e-02); v111, 350× más grande, se saltea porque su bloque entero queda bajo
        ' umbral; los gemelos especulares caen en bloques distintos. Y BrowsMaleHumanoid04 aplica 0 de 88 porque
        ' su SkinnyMorph tiene multiplier degenerado 2,04e-09 ⇒ ningún bloque alcanza el umbral.
        ' ⚠️ Sin probar: el gate resultó no-escalado por el peso, demostrado sobre el canal de PESO (w50). Para
        '    sliders de chargen sólo se midió |v|=1,0. Si aparece residual en NPCs con sliders fraccionarios,
        '    ése es el primer lugar donde mirar.
        Const BlockGateThreshold As Single = 0.01F
        For Each channel In plan.Channels
            If channel.IsZap Then Continue For
            If channel.IsUvMorph Then Continue For  ' mueve UVs, no vertices: ApplyUvChannels
            If channel.IsClamp Then Continue For   ' segundo pase, al final
            If channel.Deltas Is Nothing Then Continue For
            Dim t = channel.Weight
            If Single.IsNaN(t) Then t = 0

            ' Canal que NO pasa por el applier del CK (deltas de un .osd de BodySlide): se aplica TODO,
            ' sin umbral de magnitud. DiffDataSets::ApplyDiff (DiffData.cpp:489-517) suma cada entrada
            ' sin mirar el tamaño; su único early-out es `percent == 0`. Idéntico a lo que hace el BAKE
            ' en MorphingHelper.ApplyMorph_CPU ⇒ RENDER == BAKE.
            If Not channel.ApplyCkBlockGate Then
                If t <> 0.0F Then
                    For Each morph In channel.Deltas
                        Dim iu = CInt(morph.index)
                        If iu < 0 OrElse iu >= count Then Continue For
                        Dim du = morph.PosDiff * t
                        verts(iu) = verts(iu) + New Vector3d(du.X, du.Y, du.Z)
                    Next
                End If
                Continue For
            End If

            ' 1) blockmax por bloque de 4, con el delta CRUDO (sin peso).
            Dim blockMax As New Dictionary(Of Integer, Single)()
            For Each morph In channel.Deltas
                Dim i = CInt(morph.index)
                If i < 0 OrElse i >= count Then Continue For
                Dim pd = morph.PosDiff
                Dim m = Math.Max(Math.Abs(pd.X), Math.Max(Math.Abs(pd.Y), Math.Abs(pd.Z)))
                Dim b = i \ 4
                Dim cur As Single
                If Not blockMax.TryGetValue(b, cur) OrElse m > cur Then blockMax(b) = m
            Next

            ' 2) aplicar sólo los bloques que pasan el gate (o los de cola, que pasan siempre).
            For Each morph In channel.Deltas
                Dim i = CInt(morph.index)
                If i < 0 OrElse i >= count Then Continue For
                Dim b = i \ 4
                Dim isTailBlock = (b * 4 + 4 > count)
                If Not isTailBlock Then
                    Dim m As Single
                    If Not blockMax.TryGetValue(b, m) Then Continue For
                    If m < BlockGateThreshold Then Continue For
                End If
                Dim delta = morph.PosDiff * t
                verts(i) = verts(i) + New Vector3d(delta.X, delta.Y, delta.Z)
            Next
        Next

        ' SEGUNDO PASE: clamps. Despues de TODOS los morphs y con asignacion ABSOLUTA, igual que
        ' BodySlideApp::ApplySliders:1351-1354 -> DiffDataSets::ApplyClamp (DiffData.cpp:534-537).
        For Each channel In plan.Channels
            If Not channel.IsClamp OrElse channel.Deltas Is Nothing Then Continue For
            Dim w = channel.Weight
            If Single.IsNaN(w) OrElse w <= 0.0F Then Continue For
            For Each morph In channel.Deltas
                Dim i = CInt(morph.index)
                If i < 0 OrElse i >= count Then Continue For
                verts(i) = New Vector3d(morph.PosDiff.X, morph.PosDiff.Y, morph.PosDiff.Z)
            Next
        Next

        Return verts
    End Function

    ''' <summary>
    ''' Apply all channels in the plan to the geometry.
    ''' Deltas are applied in NIF local space (pre-skinning).
    '''
    ''' Contract for null/empty plans: if <paramref name="plan"/> is Nothing or has no
    ''' channels, the method performs a RESET — geom.Vertices is rewritten from
    ''' NifLocalVertices (raw, pre-skin), mask/dirty state is cleared, and TBN is
    ''' recalculated for any vertex that changed. This lets callers toggle morphs OFF
    ''' by passing a null plan (or a resolver that returns null) instead of keeping
    ''' stale deltas pegged on the mesh.
    ''' </summary>
    Public Shared Sub ApplyMorphPlan(ByRef geom As SkinnedGeometry, plan As MorphPlan,
                                     recalculateNormals As Boolean,
                                     Optional allowMask As Boolean = False,
                                     Optional maskedVertices As HashSet(Of Integer) = Nothing)
        ' Single chokepoint that (re)computes the zap mask (clears VertexMask, then re-applies
        ' VertexMask=-1 for zap channels). Mark the zap topology dirty on entry so
        ' Render.EnsureZapIndexBuffer rebuilds the filtered element buffer exactly once after this
        ' recompute. Covers every internal path (zap applied, mask-only cleared, null/empty-plan reset).
        ' SkinnedGeometry is a Structure passed ByRef, so this writes back to the caller's field.
        geom.ZapTopologyDirty = True

        ' UVs: mismo contrato que las posiciones — se parte SIEMPRE de la base, asi bajar un slider uv
        ' a 0 (o pasar un plan nulo) las devuelve a su lugar en vez de dejarlas corridas.
        ' ⛔ Va ANTES del early-out de `count = 0`: ese Return mira NifLocalVertices, y una shape sin
        ' vertices pero con UVs pobladas se quedaba con las UVs corridas del pase anterior.
        ' ⛔ El gate `HasUvMorphs OrElse UvsMorphed` NO es cosmetico: esto corre por shape en CADA
        ' update de morphs (arrastrar un slider dispara una cadena), y sin el se pagaba un Array.Copy
        ' del array de uvs completo en todo modelo, incluidos los que no tienen un solo slider uv.
        ' El segundo termino es el que hace correcta la optimizacion: cubre el update en el que el
        ' ultimo canal uv se apaga, que es justo cuando hay que restaurar.
        Dim uvsCambiaron As Boolean = False
        Dim uvTocados As HashSet(Of Integer) = Nothing
        Dim tieneUv = (plan IsNot Nothing AndAlso plan.HasUvMorphs)
        If tieneUv OrElse geom.UvsMorphed Then
            uvTocados = New HashSet(Of Integer)()
            Dim resetCambio = ResetUvsFromBase(geom, uvTocados)
            Dim escribio = ApplyUvChannels(geom, plan, uvTocados)
            ' El reset informa si REALMENTE toco el array: eso cubre el update en el que el ultimo
            ' canal uv se apaga, sin el falso positivo de mirar un flag pegajoso.
            Dim cambioAlgo = escribio OrElse resetCambio
            If cambioAlgo Then geom.UvsDirty = True
            geom.UvsMorphed = escribio
            ' El cache de TBN guarda las DERIVADAS UV por triangulo (BuildTBNCache), asi que mover las
            ' UVs lo invalida. Sin esto, RecalculateNormalsTangentsBitangents reusaba las derivadas de
            ' la primera aplicacion y el normal-map del viewport quedaba rotado respecto del NIF.
            ' ⛔ La condicion es `cambioAlgo`, NO el flag `UvsDirty`: ese es PEGAJOSO — lo limpia
            ' UpdateUvBuffer_GL y solo si el VBO ya existe. Mirarlo tiraba el cache y forzaba un
            ' BuildTBNCache completo en cada update mientras el VBO no estuviera creado.
            ' ⭐ Se refrescan SOLO las derivadas UV de los triangulos tocados, en vez de tirar el
            ' cache entero: la adjacencia depende de los indices, que un slider uv no mueve.
            If cambioAlgo Then RecalcTBN.RefreshUvDerivatives(geom, uvTocados)
            uvsCambiaron = cambioAlgo
        End If

        Dim count = geom.NifLocalVertices.Length
        If count = 0 Then Return

        ' Apply mask if provided (kept here, runtime concern)
        If allowMask AndAlso maskedVertices IsNot Nothing Then
            For i = 0 To count - 1
                If maskedVertices.Contains(i) Then
                    geom.VertexMask(i) = 1
                    geom.dirtyMaskIndices.Add(i)
                    geom.dirtyMaskFlags(i) = True
                Else
                    ' `<> 0`, no `= 1`: un zap previo dejo un valor NEGATIVO en la mascara y compararlo
                    ' contra 1 lo dejaba pegado. Con el guard `t > 0` del loop de zaps de abajo nadie
                    ' reescribe ese vertice, asi que el reset debe limpiar cualquier residuo.
                    If geom.VertexMask(i) <> 0 Then
                        geom.VertexMask(i) = 0
                        geom.dirtyMaskIndices.Add(i)
                        geom.dirtyMaskFlags(i) = True
                    End If
                End If
            Next
        Else
            ' ⛔ NO alcanza con poner la mascara en 0 del lado CPU: el vertice que DEJA de estar zapeado
            ' tiene que SUBIRSE al vboMask, y UpdateUpdateSkinBuffersMask_GL sale temprano cuando
            ' dirtyMaskIndices esta vacio (Render.vb). El `Array.Clear` + `dirtyMaskIndices.Clear()`
            ' hacia justo lo contrario: limpiaba la mascara Y borraba la lista de "hay que subir", asi
            ' que la GPU se quedaba con el -1 y el shader seguia descartando esos vertices.
            '
            ' Era ASIMETRICO: PRENDER un zap si subia (el loop de zaps de abajo hace
            ' `dirtyMaskIndices.Add`), APAGARLO no. La rama de arriba (allowMask) ya lo hacia bien.
            '
            ' SINTOMA MEDIDO (CBBE Underwear, zap `Remove Bow Ties 1`): se zapea la shape en el editor,
            ' se vuelve al preview principal y se cambia a un preset que no trae el zap — la shape NO
            ' reaparece. Solo volvia cambiando de proyecto y regresando, porque esa recarga pasa por
            ' Setup_GL, que recrea el vboMask con BufferData desde el array ya en cero.
            '
            ' ⛔ Tampoco se limpia el set: una entrada pendiente de una pasada anterior todavia
            ' necesita subirse. El uploader lo vacia el mismo despues de escribir.
            For i = 0 To count - 1
                If geom.VertexMask(i) <> 0 Then
                    geom.VertexMask(i) = 0
                    geom.dirtyMaskIndices.Add(i)
                    geom.dirtyMaskFlags(i) = True
                Else
                    geom.dirtyMaskFlags(i) = False
                End If
            Next
        End If

        geom.dirtyVertexIndices.Clear()

        ' Position-morph application: pure math in ApplyChannelsToVertexArray.
        Dim verts = ApplyChannelsToVertexArray(geom.NifLocalVertices, plan)

        ' Zap channels — mask flag setup (mismo paso ANTES en el bucle anterior; preserva
        ' comportamiento exacto del runtime para el toggle on/off de zaps).
        ' Gate por HasZaps (no HasMorphs): un plan SÓLO-zap (sin canales de posición — el caso de la
        ' hairline HNAM-extra, que recibe el zap pero ningún chargen-TRI morph) debe entrar igual a
        ' setear VertexMask=-1. HasMorphs (=Channels.Count>0) ya lo cubría, pero HasZaps deja explícito
        ' que el zap-only NO se puede saltear y blinda el gate ante futuros cambios del predicado.
        If plan IsNot Nothing AndAlso plan.HasZaps Then
            For Each channel In plan.Channels
                If Not channel.IsZap Then Continue For
                If channel.Deltas Is Nothing Then Continue For
                Dim t = channel.Weight
                If Single.IsNaN(t) Then t = 0
                ' `if (val > 0)` de BodySlideApp::ApplySliders:1332: un zap con peso 0 no aporta NADA.
                ' Escribir -0.0F igual PISA el negativo que dejo otro canal de zap solapado y resucita
                ' el vertice. La mascara ya viene reseteada arriba, asi que saltear es lo correcto.
                '
                ' ⛔ NO filtrar aca los deltas todo-cero: eso es una ley del OSD de BodySlide y vive en
                ' el RESOLVER (SliderMorphResolver de WM). HairTopZapResolver emite a proposito
                ' PosDiff=Vector3.Zero y usa la lista solo como indice de vertices a ocultar — filtrar
                ' en el motor anulaba el hair-zap entero.
                If t > 0.0F Then
                    For Each morph In channel.Deltas
                        Dim i = CInt(morph.index)
                        If i >= 0 AndAlso i < count Then
                            geom.VertexMask(i) = -t
                            geom.dirtyMaskIndices.Add(i)
                            geom.dirtyMaskFlags(i) = True
                        End If
                    Next
                End If
            Next
        End If

        ' Track dirty vertices
        For i = 0 To count - 1
            If geom.Vertices(i) <> verts(i) Then
                geom.dirtyVertexIndices.Add(i)
                geom.dirtyVertexFlags(i) = True
            Else
                geom.dirtyVertexFlags(i) = False
            End If
        Next

        ' Optimize: if >60% dirty, mark all dirty
        If geom.dirtyVertexIndices.Count > count * 0.6 Then
            geom.dirtyVertexIndices = New HashSet(Of Integer)(Enumerable.Range(0, count))
            For i = 0 To count - 1
                geom.dirtyVertexFlags(i) = True
            Next
        End If

        geom.Vertices = verts

        ' Invalidate caches
        geom.WorldCacheValid = False
        geom.CachedWorldVertices = Nothing
        geom.CachedWorldNormals = Nothing

        ' ⭐ La base tangente depende de las UVs, asi que un slider uv la invalida — pero NO mueve un
        ' solo vertice, con lo que dirtyVertexIndices queda vacio y el recalculo de abajo no corria
        ' NUNCA: se tiraba el cache y no habia quien lo reconstruyera.
        ' Canonico: CalcTangentsForShape corre INCONDICIONAL en la fase 3 del build
        ' (BodySlideApp.cpp:4501 y :4529), fuera de todo gate de vertices; lo unico gateado ahi son
        ' las NORMALES (por lockNormals, :4494). Por eso, cuando el unico cambio son UVs, se fuerza
        ' el recalculo y despues se RESTAURAN las normales: el efecto neto es "solo tangentes",
        ' que es exactamente lo que hace el canonico.
        ' Sin triangulos no hay base tangente que recalcular, y BuildTBNCache desreferencia el array
        ' de indices. El recalculo por UV corre en casos donde el de normales nunca corria, asi que
        ' el guard va aca y no en el llamador.
        If uvsCambiaron AndAlso Not (geom.Indices IsNot Nothing AndAlso geom.Indices.Length >= 3 AndAlso
                                     geom.Uvs_Weight IsNot Nothing AndAlso geom.Normals IsNot Nothing) Then
            uvsCambiaron = False
        End If

        ' ⭐ `soloTangentes` NO se decide por el ajuste de recalcular normales, sino por si SE MOVIERON
        ' VERTICES. Las normales se derivan de POSICIONES; las UVs no entran en su calculo. El
        ' canonico lo separa igual: `CalcNormalsForShape` (posiciones, gateada por lockNormals) y
        ' `CalcTangentsForShape` (UVs) son dos pases distintos (BodySlideApp.cpp:4494-4501).
        ' ⛔ MEDIDO en un build real (UBE brows, slider uv `Thin` a 100): con el ajuste en True esto
        ' daba False, corria el recalculo COMPLETO y las 501 normales pasaban de AUTORADAS (las del
        ' NIF fuente, que es lo que queda cuando nada esta sucio) a CALCULADAS — un salto de hasta
        ' 0,279 con las posiciones IDENTICAS. Mover un slider uv un 1 % te cambiaba todas las
        ' normales de la malla.
        Dim huboCambioDePosicion As Boolean = geom.dirtyVertexIndices.Count > 0
        ' ⛔ La condicion es `Not (pidioNormales AndAlso huboCambioDePosicion)`, NO
        ' `uv AndAlso Not posicion`. Esa version anterior cubria solo el caso UV-PURO: con un slider
        ' uv Y uno de posicion a la vez, `huboCambioDePosicion` daba True, KeepExistingNormals caia a
        ' False y las normales se recalculaban AUNQUE el ajuste estuviera apagado — o sea que el uv
        ' reactivaba por la ventana un recalculo que el usuario habia desactivado.
        ' Las normales se recomputan si y solo si el usuario lo pidio Y se movio geometria; las UVs
        ' nunca las tocan. Es la separacion del canonico: `if (!lockNormals) CalcNormalsForShape`
        ' (posiciones) y `CalcTangentsForShape` (UVs) son dos pases independientes
        ' (BodySlideApp.cpp:4494-4501).
        Dim soloTangentes As Boolean = Not (recalculateNormals AndAlso huboCambioDePosicion)
        If uvsCambiaron Then
            ' ⭐ SOLO los vertices cuyas UV se movieron. RecalculateNormalsTangentsBitangents hace
            ' la clausura sola (dirty -> triangulos incidentes -> los 3 vertices de cada uno) y elige
            ' acumuladores SPARSE por debajo del 40 % de los triangulos. Marcar la malla entera
            ' forzaba el camino full y el maximo trabajo posible en CADA tick del arrastre.
            For Each iv In uvTocados
                If iv >= 0 AndAlso iv < count Then
                    geom.dirtyVertexIndices.Add(iv)
                    geom.dirtyVertexFlags(iv) = True
                End If
            Next
        End If

        ' Recalculate normals/TBN if needed
        ' ⚠️ ABIERTO: el canonico llama `CalcTangentsForShape` INCONDICIONAL (BodySlideApp.cpp:4501);
        ' aca se gatea por `recalculateNormals` (decision de WM, el switch del usuario manda) Y ademas
        ' por si se movio algo. Un shape que el preset no toca conserva las tangentes AUTORADAS.
        ' ⛔ PROBADO Y DESCARTADO: forzar el recalculo completo cuando no hay nada sucio NO cambio el
        ' resultado de `BaseUndies` (35,20 grados, identico en 4 versiones distintas del codigo), asi
        ' que la divergencia de ese shape NO esta aca — sus tangentes llegan al NIF por otro camino.
        ' Se revirtio para no pagar un recalculo de malla entera por frame sin beneficio medido.
        If ((recalculateNormals AndAlso huboCambioDePosicion) OrElse uvsCambiaron) AndAlso geom.dirtyVertexIndices.Count > 0 Then
            Dim opt As RecalcTBN.TBNOptions = Config_App.Current.Setting_TBN
            opt.KeepExistingNormals = soloTangentes
            Dim adicionales = RecalcTBN.RecalculateNormalsTangentsBitangents(geom, opt)
            adicionales.ExceptWith(geom.dirtyVertexIndices)
            For Each ad In adicionales
                geom.dirtyVertexIndices.Add(ad)
                geom.dirtyVertexFlags(ad) = True
            Next
        End If
    End Sub
End Class
