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

    Sub New(name As String, weight As Single, deltas As List(Of MorphData), Optional isZap As Boolean = False,
            Optional engineApplied As Boolean = True)
        Me.Name = name
        Me.Weight = weight
        Me.Deltas = deltas
        Me.IsZap = isZap
        Me.EngineApplied = engineApplied
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

''' <summary>
''' Proveedor de la <b>geometría BASE pre-skin</b> de un shape — el array del que
''' <see cref="MorphEngine.ApplyMorphPlan"/> parte para aplicar los canales de morph.
'''
''' <para><b>Para qué existe.</b> Normalmente la base es lo que trae el NIF, y la establece
''' <c>SkinningHelper.ExtractSkinnedGeometry</c> al cargar. Pero hay casos donde la base correcta
''' NO es la del archivo: en Fallout 4 el motor y el CK no dibujan la malla `_faceBones` de una
''' head part — la usan como INSUMO para calcular las posiciones de la malla PLANA (el FaceGeom), y
''' dibujan ésa. Para replicar eso, la app necesita entregar la geometría horneada como base del
''' shape plano. Esto NO es parchear un buffer aguas abajo: es proveer el valor correcto en el punto
''' donde el pipeline define "la base pre-skin de esta malla".</para>
'''
''' <para><b>Por qué NO se hace con un <see cref="IMorphResolver"/>.</b> Un canal de morph pasa por
''' el gate de bloques del CK de <see cref="MorphEngine.ApplyChannelsToVertexArray"/>, que descarta
''' bloques de 4 con delta crudo &lt; 0,01. Ese gate existe para <b>decodificar un `.tri` comprimido</b>
''' (mapa RLE precomputado); geometría calculada no es data de `.tri` y someterla a esa regla es
''' aplicarla fuera de su dominio — además de dejar la malla a medio hornear.</para>
'''
''' <para><b>Contrato.</b> Invocado <b>en serie</b> al principio de <c>PipelineStep_Morphs</c>, ANTES
''' del <c>Parallel.ForEach</c> de los resolvers, y sólo para los shapes marcados dirty. La
''' implementación debe:</para>
''' <list type="bullet">
''' <item><description>escribir <b>IN PLACE</b> en <c>geom.NifLocalVertices</c> (p.ej. <c>Array.Copy</c>).
''' <see cref="SkinnedGeometry"/> es una <c>Structure</c>; el array es referencia, así que mutar sus
''' elementos propaga, pero reasignar el array sólo funciona por la cadena de campos del caller.</description></item>
''' <item><description><b>NUNCA</b> leer <c>geom.Vertices</c> (lo reescribe <c>ApplyMorphPlan</c> en
''' cada pasada ⇒ se realimenta) ni <c>geom.PerVertexSkinMatrix</c> (queda stale dentro de este paso).</description></item>
''' <item><description>ser <b>absoluta, no incremental</b>: partir siempre de una copia pristina propia,
''' nunca del valor actual de <c>NifLocalVertices</c>.</description></item>
''' </list>
''' <para>Devuelve True si reescribió la base de ese shape (sólo informativo / diagnóstico).</para>
''' </summary>
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
            If channel.Deltas Is Nothing Then Continue For
            Dim t = channel.Weight
            If Single.IsNaN(t) Then t = 0

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
                    If geom.VertexMask(i) = 1 Then
                        geom.VertexMask(i) = 0
                        geom.dirtyMaskIndices.Add(i)
                        geom.dirtyMaskFlags(i) = True
                    End If
                End If
            Next
        Else
            Array.Clear(geom.VertexMask, 0, count)
            geom.dirtyMaskIndices.Clear()
            For i = 0 To count - 1
                geom.dirtyMaskFlags(i) = False
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
                For Each morph In channel.Deltas
                    Dim i = CInt(morph.index)
                    If i >= 0 AndAlso i < count Then
                        geom.VertexMask(i) = -t
                        geom.dirtyMaskIndices.Add(i)
                        geom.dirtyMaskFlags(i) = True
                    End If
                Next
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

        ' Recalculate normals/TBN if needed
        If recalculateNormals AndAlso geom.dirtyVertexIndices.Count > 0 Then
            Dim opt As RecalcTBN.TBNOptions = Config_App.Current.Setting_TBN
            Dim adicionales = RecalcTBN.RecalculateNormalsTangentsBitangents(geom, opt)
            adicionales.ExceptWith(geom.dirtyVertexIndices)
            For Each ad In adicionales
                geom.dirtyVertexIndices.Add(ad)
                geom.dirtyVertexFlags(ad) = True
            Next
        End If
    End Sub
End Class
