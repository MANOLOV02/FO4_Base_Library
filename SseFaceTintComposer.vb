Option Strict On

Imports System.Collections.Concurrent
Imports System.Linq

''' <summary>BUILDER del face-tint de SSE: resuelve QUÉ hornea el CreationKit en
''' <c>FaceGenData\FaceTint\&lt;plugin&gt;\&lt;fid&gt;.dds</c> — las capas, su orden, su color y su cobertura, más
''' el seed. ⛔ Ya NO compone: el compose lo hace <see cref="FaceTintCpuCompositor.ComposeChannelAccum"/>, el
''' mismo que usa Fallout. Este módulo aporta DATOS y su set de defaults; cómo se compone es único.
''' <para>MODELO DEL MOTOR (verificado en CreationKit.exe, no curve-fit): BSFaceGenUtils arma un float4[16] de
''' {color x 1/255, interp} por mascara de tint y se lo pasa a un pixel shader de image-space. NUNCA lee un
''' campo de "tipo": TODA capa es el mismo lerp gateado por cobertura, no hay blend-op por tipo. Por capa,
''' <c>coverage = maskR x TINV</c> y <c>acc = lerp(acc, color, coverage)</c>.</para>
''' <para>La base es el color de piel del NPC (QNAM) PLANO: medido, la region de piel del _d que hornea el CK
''' es plana e igual al QNAM. El DETALLE del diffuse lo agrega el shader en RENDER, asi que no esta en el _d
''' horneado (usar el head diffuse como base da ~52/255, mal).</para>
''' <para>âš ï¸ SOBRE LA PALABRA "LINEAR" EN ESTE MODULO (anotado, NO renombrado): ni DecodeDds ni
''' DecodeTextureRgba aplican la curva sRGB a lineal, devuelven <c>byte/255</c> CRUDO. "linear RGBA" en las
''' firmas de este modulo, de SseOverlayCompositor y de SseSkeeMaskReader significa "valor de ALMACENAMIENTO
''' normalizado a [0,1]", no "en espacio lineal". El unico lugar que de verdad linealiza es el fold
''' (<see cref="SseFaceGenBaker.FoldFacetintIntoDiffuse"/>, que llama Srgb2Lin/Lin2Srgb explicitamente).</para>
''' <para>Las mascaras de tatuaje (TINI 65-74) traen TINT pero no TINP: igual tienen que registrarse
''' (flush-on-new-TINI) o los NPC con war-paint se van a ~74/255.</para>
''' <para>Es el analogo SSE de <see cref="FaceTintInputBuilder"/> (FO4) y es SSE-only: los callers gatean por
''' juego. Ver 31-sse-facetint-spec.</para></summary>
Public Module SseFaceTintComposer
    ''' <summary>CAPACIDAD DECLARADA del espejo CPU de ESTE camino, para los call sites GL que lo tienen de
    ''' espejo. El espejo es <see cref="FaceTintCpuCompositor"/>, que implementa la ley de los cuatro espacios
    ''' completa: siembra EN AccumSpace, compone entero ahí y hace UN solo pase AccumSpace→OutputSpace al
    ''' cerrar el canal.
    ''' <para>⛔ La constante sigue viviendo acá y no se reemplaza por la de <c>FaceTintCpuCompositor</c> en
    ''' los call sites: lo que declara no es "qué compositor corre" sino "qué CAMINO se está espejando", y el
    ''' del facetint de SSE es distinto del de la cara de FO4 aunque hoy los dos terminen en la misma función.
    ''' Ver <see cref="FaceTintConvention.FaceTintCpuMirrorCapability"/>.</para>
    ''' <para>Antes declaraba OutputSpaceOnly y el flag quedaba INERTE en SSE. Eso no era diseno sino
    ''' compensacion: el GL comparte ApplyFaceTintPipeline con FO4, asi que con el CPU sin implementarlo habia
    ''' que SUPRIMIR el flag del lado GL para que no divergieran - se apagaba el sintoma en un lado en vez de
    ''' cerrar el hueco en el otro. "SSE es all-linear asi que da igual" tampoco servia: es una COINCIDENCIA de
    ''' configuracion (alcanza con poner os=G22 en CharGen Options para romperla).</para></summary>
    Public Const AccumSpaceCapability As FaceTintConvention.FaceTintCpuMirrorCapability =
        FaceTintConvention.FaceTintCpuMirrorCapability.FourSpaceAccumulator

    ''' <summary>One RACE tint-layer preset: a named colour swatch the CK's tinting dropdown offers for this layer.
    ''' The NPC record selects one by its <see cref="Tirs"/> value stored in the layer's TIAS field (TIAS = TIRS →
    ''' the preset's CLFM colour; TIAS = -1 → the layer uses a custom RGB, not a preset). Verified against vanilla
    ''' Skyrim.esm: for every NPC skin-tone layer with TIAS≥0, its TINC == the CLFM colour of the preset whose
    ''' TIRS == TIAS (1673/1673, zero exceptions).</summary>
    Public Structure SseTintPreset
        Public Tirs As Integer       ' TIRS — the preset's index; the NPC's TIAS references this value
        Public Clfm As UInteger      ' TINC (RACE preset) — the CLFM formID whose CNAM colour this preset applies
        Public Value As Double       ' TINV (RACE preset) — the preset's default coverage (FLOAT 0-1)
    End Structure

    ''' <summary>One RACE tint mask: the greyscale mask texture path + its TINP mask type (-1 when the layer
    ''' omits TINP, e.g. tattoos). Type is retained for tooling/diagnostics only — the blend is uniform.</summary>
    Public Structure SseTintMask
        Public Index As Integer      ' TINI — the layer's index (NPC tints reference the RACE layer by this)
        Public Path As String        ' TINT — greyscale mask texture path
        Public MaskType As Integer   ' TINP — mask type (-1 when omitted). Diagnostic only; blend is uniform.
        Public DefaultClfm As UInteger ' TIND — CLFM formID of the default preset (colour for unauthored layers)
        Public DefaultValue As Double  ' the default preset's TINV (coverage for unauthored layers)
        Public Presets As List(Of SseTintPreset) ' the CK dropdown's swatches for this layer (TIRS→CLFM/value); may be empty
    End Structure

    ' ⭐⭐ CONCURRENTDICTIONARY, NO Dictionary. Estos cuatro caches los tocan DOS HILOS a la vez:
    '   • el BAKE corre en el ThreadPool (MainForm.RunChargenBake / BuildCharGenSingle / el batch loose hacen
    '     `Await Task.Run(...)` cuando WriteGPUSandboxOutput=False, que es el caso de RELEASE), y
    '   • el RENDER corre en el hilo UI — que sigue bombeando mensajes DURANTE ese await, así que un WM_PAINT
    '     entra a NpcFaceTintResolver → ComposeLinearRgba/DecodeTextureRgba → acá, en paralelo con el bake.
    ' Un Dictionary en escritura concurrente no da "un valor raro": puede colgar el proceso en un bucle infinito
    ' dentro de Insert() al re-hashear. Con ConcurrentDictionary el patrón de uso (TryGetValue + indexer set) es
    ' idéntico, así que no cambia ni la lógica ni el resultado — sólo deja de ser una bomba de tiempo.
    ' Per-race+gender ORDERED tint-layer list cache (identical across NPCs of the same race). The engine
    ' composes cb2[0..15] in this RACE order (builder @0x18C9F40). Keyed "<raceFid><F|M>".
    Private ReadOnly _layersCache As New ConcurrentDictionary(Of String, List(Of SseTintMask))
    ' _texCache SE BORRO: era la segunda implementacion de la cache de decode+resample, con estado de modulo,
    ' clave propia (SIN el tamaño destino, parcheada restringiendo el dominio a 512²) y criterio de negativos
    ' propio. Su reemplazo es el NIVEL 2 de FaceTintCpuCompositor (CachedUnitDecode), que SI consulta el techo
    ' `BatchDecodeCacheBudgetBytes` mientras hay lote activo. Ver DecodeMask.
    ' Resolved CLFM formID -> linear RGB [0,1] (race-default colours), cached.
    Private ReadOnly _clfmCache As New ConcurrentDictionary(Of UInteger, Double())

    ' LOS DOS CACHES QUE QUEDAN ACA son datos de RECORD, no texturas: no consultan techo porque no pesan, y su
    ' vida es la del LOAD ORDER (ClearCaches). El que pesa —y el que el techo gobierna— ya no vive en este
    ' modulo.

    ''' <summary>Suelta el cache de TEXTURA, el que pesa: el resultado decodificado y resampleado.
    ''' <para>VIDA PER-NPC, igual que del lado FO4: se conserva entre recargas del MISMO NPC -para que la
    ''' edicion viva siga rapida al segundo click- y se suelta al cambiar de NPC raiz. Las mascaras del RACE se
    ''' comparten entre NPCs de esa raza, asi que soltarlas cuesta re-decode y re-resample en el proximo cambio;
    ''' se paga a proposito para que la app no acumule memoria navegando.</para>
    ''' <para>â›” NO toca <see cref="_layersCache"/> ni <see cref="_clfmCache"/>: son datos de RECORD (lista
    ''' ordenada de capas por raza+genero, CLFM a RGB), no pesan y re-parsearlos en cada cambio de NPC seria
    ''' churn puro. Su vida es la del LOAD ORDER y la maneja <see cref="ClearCaches"/>.</para>
    ''' <para>Suelta SOLO el cache de SESION (el que sobrevive entre refrescos de la edicion viva). El BARRIDO
    ''' del bake no pasa por aca: usa el cache de LOTE, que abren y cierran Begin/EndBatchDecodeCache, asi que
    ''' ahi la reutilizacion entre NPCs de la misma raza se conserva, que es donde mas rinde.</para>
    ''' <para>El cache vive en FaceTintCpuCompositor desde el colapso de `_texCache`; aca queda el nombre que
    ''' ya llamaban los callers del otro repositorio, para no tener que editarlos.</para></summary>
    Public Sub ClearTextureCaches()
        FaceTintCpuCompositor.ClearSessionUnitCache()
    End Sub

    ''' <summary>Drop the per-race layer + decoded-texture + CLFM caches (call on FilesDictionary rebuild).</summary>
    Public Sub ClearCaches()
        _layersCache.Clear()
        ClearTextureCaches()
        _clfmCache.Clear()
    End Sub

    ''' <summary>Compose the SSE face tint for an NPC into a W×H linear RGBA buffer ([0,1], length W*H*4,
    ''' order R,G,B,A). Returns Nothing when the race/face-texture/QNAM can't be resolved. Pure — no GL, no
    ''' file writes. The caller encodes to DXT5 (bake) or uploads to GL (render).</summary>
    ''' <param name="npcRec">The NPC_ record (source of QNAM + the TINI/TINC/TINV/TIAS tint layer list).</param>
    ''' <param name="race">Parsed RACE (source of the default face TXST + the per-gender tint masks).</param>
    ''' <param name="npcTintOverride">Optional edited tint layers (TINI/TINC/TINV/TIAS raw subrecords) from the
    ''' editor overlay. When provided, the NPC-authored tint map is read from THIS instead of the raw
    ''' npcRec.Subrecords — so live edits (Edit Face → Face Tints) reflect in render + bake. Nothing = raw.</param>
    Public Function ComposeLinearRgba(pm As PluginManager, npcRec As PluginRecord, race As RACE_Data,
                                      raceFormID As UInteger, isFemale As Boolean,
                                      Optional w As Integer = 512, Optional h As Integer = 512,
                                      Optional baseImg As Single() = Nothing,
                                      Optional npcTintOverride As IList(Of NPC_RawSubrecord) = Nothing,
                                      Optional tintTexOverride As Dictionary(Of Integer, String) = Nothing) As Single()
        If pm Is Nothing OrElse npcRec Is Nothing OrElse race Is Nothing Then Return Nothing
        If w <= 0 OrElse h <= 0 Then Return Nothing
        Dim npix = w * h

        ' ENGINE-EXACT — decoded 100% from (a) the facegen-tint pixel shader (ps_5_0, DXBC @0x40033A8) and
        ' (b) the cb2-source builder @0x18C9F40. Shader: base = 0.5; per layer acc = lerp(acc, colour,
        ' mask.r × interp); out.w = 1; UNIFORM, no per-type branch. Builder: iterate the RACE's tint layers
        ' IN RACE ORDER — for each, colour+interp come from the NPC's authored tint for that layer INDEX if
        ' present, else the RACE default (TIND→CLFM colour). interp = value_byte × 0.01 (both confirmed in the
        ' binary). The RACE order (not NPC subrecord order) is what fills cb2[0..15]; lerp is not commutative.
        ' Esa ley YA NO se ejecuta acá: vive en la convención (bucket SSE) y la aplica el compositor
        ' compartido. Este módulo aporta los DATOS — qué capas, en qué orden, con qué color y cobertura.

        ' CAPAS: la MISMA fuente que el camino GPU. ⛔ Antes esta función tenía su propia resolución de capas
        ' (idéntica línea por línea a la de BuildLayerInputs) y su propio loop de compose. Las dos cosas se
        ' borraron: dos implementaciones del mismo camino divergen sin que nadie se equivoque en la matemática.
        ' Las capas viajan con sus BYTES y el compositor las decodifica y muestrea — el MISMO camino que
        ' Fallout, sin excepción para este juego. La capa que no decodifica la descarta el compositor
        ' (mismo criterio que tenía el loop propio, donde DecodeMask devolvía Nothing).
        Dim layers = BuildLayerInputs(pm, npcRec, race, raceFormID, isFemale, npcTintOverride, tintTexOverride)
        If layers Is Nothing Then Return Nothing

        ' SEED: la baseImg del caller (modo diagnóstico del probe) gana; si no, la ley del bucket.
        Dim seed = If(baseImg IsNot Nothing AndAlso baseImg.Length >= npix * 4,
                      FaceTintCpuCompositor.FaceTintSeedSpec.FromBuffer(baseImg),
                      BuildSeedSpec())
        ' El ALPHA: con baseImg viaja el del caller (Passthrough, que es lo que hacía el Array.Copy); con el
        ' seed de la ley el facetint sale opaco (out.w = 1 del shader del motor).
        Dim alphaPolicy = If(seed.Kind = FaceTintCpuCompositor.FaceTintSeedKind.Provided,
                             FaceTintCpuCompositor.FaceTintAlphaPolicy.Passthrough,
                             FaceTintCpuCompositor.FaceTintAlphaPolicy.Opaque)

        ' Sin region swaps: el facetint de SSE es tint-only.
        Dim acc = FaceTintCpuCompositor.ComposeChannelAccum(seed, w, h, FaceTintChannel.Diffuse,
                                                            layers, Nothing, Nothing, alphaPolicy)
        ' AoS float y NO bytes: el fold amplifica ×255/64, así que cuantizar acá es una regresión MEDIDA.
        Return FaceTintCpuCompositor.AccumToRgbaAos(acc)
    End Function

    ''' <summary>⭐ El SEED del acumulador del facetint SSE, resuelto desde la LEY (el bucket de CharGen
    ''' Options) y no desde un literal. Hermano de <see cref="BuildLayerInputs"/>: el builder de este juego
    ''' aporta las capas Y el seed, que son los dos DATOS que el compositor compartido necesita.
    ''' <para>⛔ Va como función aparte y no como segundo valor de retorno de <see cref="BuildLayerInputs"/> por
    ''' la misma razón que el plan hizo opcional el parámetro <c>stage</c> del pipeline GL: <c>BuildLayerInputs</c>
    ''' tiene cuatro call sites en el OTRO repositorio y los dos repos no tienen commit atómico. La fuente
    ''' sigue siendo única — es este módulo.</para>
    ''' <para>⚠️ Con <c>SeedMode = BaseTexture</c> devuelve el modo de textura SIN textura, y el compose sale
    ''' Nothing: el facetint del CK es TINT-ONLY, no hay base de donde sembrar. No se tapa con el constante
    ''' —era justo el literal que escondía el bug— y el caller ya REPORTA el fallo (el slot 6 depende de que
    ''' esto no sea Nothing). Un config viejo no puede llegar acá: lo corrige el upgrade de versión
    ''' (<see cref="FaceTintConvention.FaceTintConventionSettings.UpgradeInPlace"/>).</para></summary>
    Public Function BuildSeedSpec() As FaceTintCpuCompositor.FaceTintSeedSpec
        If FaceTintConvention.SeedModeValue <> FaceTintConvention.FaceTintSeedMode.Constant Then
            Return FaceTintCpuCompositor.FaceTintSeedSpec.FromTextureSource(Nothing)
        End If
        Dim k = FaceTintConvention.SeedConstantValue()
        Return FaceTintCpuCompositor.FaceTintSeedSpec.FromConstant(k(0), k(1), k(2))
    End Function

    ''' <summary>⭐ El seed del facetint SSE como TERNA RGB [0,1], para los caminos que lo siembran como
    ''' TEXTURA PLANA (los cuatro de GPU: el facetint del render plegado y del NO plegado, y los sandboxes
    ''' <c>_2b</c>/<c>_2d</c> del bake). Sale de <see cref="BuildSeedSpec"/> — la MISMA ley que consumen el
    ''' compose CPU y el QNAM del cuerpo —, no de un literal.
    ''' <para>⛔ POR QUE EXISTE: los cuatro sitios tenían el 0,5 CABLEADO (tres como <c>0.5F</c> y el del
    ''' <c>_2b</c> como el byte <c>128</c> = 0,50196), así que el seed de CharGen Options entraba por el
    ''' camino CPU y NO por el GPU — que es el que corre por default (<c>Setting_GPUSkinning</c>). Síntomas:
    ''' mover el seed no cambiaba el render, y CPU y GPU componían desde números distintos sin que ningún
    ''' instrumento lo viera.</para>
    ''' <para>⛔ DEVUELVE Nothing —y NO 0,5— cuando la ley no pide seed constante. El facetint del CK es
    ''' TINT-ONLY: no hay textura base de donde sembrar, así que el caller tiene que ABORTAR con log, que es
    ''' exactamente lo que hace el camino CPU (<see cref="ComposeLinearRgba"/> devuelve Nothing). Taparlo con
    ''' un constante reintroduciría el literal que escondía el bug.</para></summary>
    Public Function TryGetFlatSeedRgb() As Single()
        Dim spec = BuildSeedSpec()
        If spec.Kind <> FaceTintCpuCompositor.FaceTintSeedKind.Constant Then Return Nothing
        Return New Single() {spec.R, spec.G, spec.B}
    End Function

    ''' <summary>Mapa de tints AUTORADOS del NPC: indice de capa -> {R, G, B (TINC/255), interp (TINV/100)}.
    ''' Subrecords por capa: TINI, TINC, TINV, TIAS (el TIAS cierra la capa y la commitea).
    ''' <para>FUENTE UNICA de las DOS replicas: la CPU (<see cref="ComposeLinearRgba"/>) y la GPU
    ''' (<see cref="BuildLayerInputs"/>) llaman ACA. â›” Estaba DUPLICADO literalmente en las dos: editar una
    ''' sola habria hecho divergir CPU y GPU en el VALOR, en silencio y sin que nadie se equivocara en la
    ''' matematica. No volver a inlinearlo.</para>
    ''' <para><paramref name="npcTintOverride"/> = capas editadas en el editor; Nothing = el record crudo. Se
    ''' normaliza a (sig, data) para que el PluginRecord y la lista de subrecords crudos se parseen igual.</para>
    ''' <para>âš ï¸ TINV NO se acota A PROPOSITO. En spec vale 0-100 y las dos replicas coinciden; fuera de spec
    ''' divergen (el GPU acota la cobertura y el CPU no). No se unifica porque no esta RE-ado que hace el motor
    ''' con TINV &gt; 100 y este valor alimenta el BAKE, validado byte-exacto contra el CK: un clamp inventado
    ''' seria regresion, no fix. El RESULTADO si queda acotado aguas abajo en los dos caminos.</para></summary>
    Private Function BuildNpcAuthoredTintMap(npcRec As PluginRecord,
                                             npcTintOverride As IList(Of NPC_RawSubrecord)) As Dictionary(Of Integer, Double())
        Dim npcMap As New Dictionary(Of Integer, Double())
        Dim tIdx As Integer = -1, tr As Double = 0, tg As Double = 0, tb As Double = 0, tvv As Double = 0
        ' Sin override Y sin record ⇒ no hay tints autorados: mapa vacío (lo usa ResolveSkinToneQnam, que sólo
        ' tiene la lista cruda `npc.SseTintRaw` y puede venir Nothing). Antes ese caller guardaba por su cuenta.
        If npcTintOverride Is Nothing AndAlso npcRec Is Nothing Then Return npcMap
        Dim tintPairs As IEnumerable(Of (Sig As String, Data As Byte()))
        If npcTintOverride IsNot Nothing Then
            tintPairs = npcTintOverride.Select(Function(s) (s.Sig, s.Data))
        Else
            tintPairs = npcRec.Subrecords.Select(Function(s) (s.Signature, s.Data))
        End If
        ' Guardas de nulidad/longitud = las MÁS ESTRICTAS de las tres copias que esto reemplaza (venían de
        ' ResolveSkinToneQnam). Sólo evitan excepciones con datos malformados; con datos bien formados el
        ' resultado es idéntico ⇒ no tocan el bake.
        For Each sr In tintPairs
            Select Case sr.Sig
                Case "TINI" : If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 2 Then tIdx = BitConverter.ToUInt16(sr.Data, 0)
                Case "TINC" : If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 3 Then tr = sr.Data(0) / 255.0 : tg = sr.Data(1) / 255.0 : tb = sr.Data(2) / 255.0
                Case "TINV"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        tvv = BitConverter.ToUInt32(sr.Data, 0) / 100.0
                        If tvv > 1.0 Then
                            Dim tvvLog = tvv, idxLog = tIdx
                            Logger.LogLazy(Function() $"[SSE-TINT] TINV FUERA DE SPEC: idx={idxLog} valor={tvvLog:F4} (>1.0). " &
                                                      "CPU y GPU divergen acá (el GPU acota la cobertura, el CPU no). " &
                                                      "Hace falta RE del motor antes de unificar.")
                        End If
                    End If
                Case "TIAS" : If tIdx >= 0 Then npcMap(tIdx) = New Double() {tr, tg, tb, tvv} : tIdx = -1 : tr = 0 : tg = 0 : tb = 0 : tvv = 0
            End Select
        Next
        Return npcMap
    End Function

    ''' <summary>Contraparte GPU de <see cref="ComposeLinearRgba"/>: resuelve las capas de tint del NPC como
    ''' <see cref="FaceTintLayerInput"/> (PaletteMask) para el compositor GL (<c>ApplyFaceTintPipeline</c>). La
    ''' resolución es IDÉNTICA a ComposeLinearRgba (RACE order, color authored-vs-default, TINV, override de máscara),
    ''' de modo que componer estas capas sobre un base PLANO = seed (0.5) por GL reproduce el compose CPU del <c>_2</c>
    ''' → el par <c>_2</c> vs <c>_2b</c> mide la paridad CPU==GPU del facetint base. Los bytes de máscara salen del
    ''' FilesDictionary (el pipeline GL los decodea/sube). Nothing si no resuelve race/npc. SSE-only (debug sandbox).</summary>
    Public Function BuildLayerInputs(pm As PluginManager, npcRec As PluginRecord, race As RACE_Data,
                                     raceFormID As UInteger, isFemale As Boolean,
                                     Optional npcTintOverride As IList(Of NPC_RawSubrecord) = Nothing,
                                     Optional tintTexOverride As Dictionary(Of Integer, String) = Nothing) As List(Of FaceTintLayerInput)
        If pm Is Nothing OrElse npcRec Is Nothing OrElse race Is Nothing Then Return Nothing

        ' Tints AUTORADOS del NPC — MISMA FUENTE que la réplica CPU (ComposeLinearRgba). ⛔ No re-inlinear:
        ' estaban duplicados y una edición en un solo sitio hacía divergir CPU y GPU en silencio.
        Dim npcMap = BuildNpcAuthoredTintMap(npcRec, npcTintOverride)

        Dim layers = SortSseTintLayers(GetRaceLayersOrdered(pm, raceFormID, isFemale), npcMap)   ' orden configurable, default RaceMenu
        ' Canal de la máscara según la ley SSE (default R). El shader GL PaletteMask lo lee por uniform (FO4=verde).
        Dim maskCh As Integer = MaskChannelIndex(FaceTintConvention.ActiveSettings().Diffuse)
        Dim outp As New List(Of FaceTintLayerInput)
        For Each layer In layers
            Dim cr As Double, cg As Double, cbb As Double, iv As Double
            Dim authored As Double() = Nothing
            If npcMap.TryGetValue(layer.Index, authored) Then
                cr = authored(0) : cg = authored(1) : cbb = authored(2) : iv = authored(3)
            Else
                Dim dc = ResolveClfmColor(pm, layer.DefaultClfm)
                cr = dc(0) : cg = dc(1) : cbb = dc(2) : iv = layer.DefaultValue
            End If
            Dim maskPath = layer.Path
            Dim custPath As String = Nothing
            If tintTexOverride IsNot Nothing AndAlso tintTexOverride.TryGetValue(layer.Index, custPath) AndAlso Not String.IsNullOrEmpty(custPath) Then
                maskPath = custPath
            End If
            If iv <= 0.0 OrElse String.IsNullOrEmpty(maskPath) Then Continue For
            Dim key = NormalizeTextureKey(maskPath)   ' ⭐ MISMA normalización que el CPU (ver NormalizeTextureKey)
            If String.IsNullOrEmpty(key) Then Continue For
            Dim mb = FilesDictionary_class.GetBytes(key)
            If mb Is Nothing Then Continue For
            outp.Add(New FaceTintLayerInput With {
                .Kind = FaceTintLayerKind.PaletteMask,
                .LayerDdsBytes = mb, .LayerCacheKey = key,
                .R = ClampByteLocal(CSng(cr * 255.0)), .G = ClampByteLocal(CSng(cg * 255.0)), .B = ClampByteLocal(CSng(cbb * 255.0)),
                .Opacity = CSng(iv), .BlendOp = 0, .Slot = 0US, .IsTextureSet = False,
                .PaletteMaskChannel = maskCh,
                .DebugName = $"sse-tint idx={layer.Index}"})
        Next
        Return outp
    End Function

    ''' <summary>Clamp a double a byte [0,255] con redondeo. Local (SseFaceTintComposer no comparte el de FaceGenBuilder).</summary>
    Private Function ClampByteLocal(v As Single) As Byte
        If v < 0.0F Then Return 0
        If v > 255.0F Then Return 255
        Return CByte(MathF.Round(v, MidpointRounding.ToEven))
    End Function

    ''' <summary>Índice de canal (0..3) de la máscara según la ley del bucket. ByKind en SSE = R (todas las
    ''' capas facegen-tint usan el canal rojo, verificado en los .fx type==1). R/G/B/A = ese canal explícito.</summary>
    Private Function MaskChannelIndex(bucket As FaceTintConvention.FaceTintBucketConvention) As Integer
        If bucket Is Nothing Then Return 0
        Select Case bucket.MaskChannel
            Case FaceTintConvention.FaceTintMaskChannel.G : Return 1
            Case FaceTintConvention.FaceTintMaskChannel.B : Return 2
            Case FaceTintConvention.FaceTintMaskChannel.A : Return 3
            Case Else : Return 0   ' R y ByKind → canal rojo (SSE)
        End Select
    End Function

    ''' <summary>SSE analogue of FO4's <c>DeriveSkinToneQnam</c>: derive the effective QNAM (TextureLighting)
    ''' colour from the RACE's SKIN-TONE tint layer (the one whose TINP mask type == 6). Returns Nothing when the
    ''' race has no such layer. Unlike FO4 (where QNAM.A carries the intensity), SSE QNAM has NO alpha — the engine
    ''' soft-lights the body at FULL strength and the intensity is FOLDED INTO the colour:
    ''' <c>q = clamp(0.5 + TINV*(TINC/255 - 0.5), 0, 1)</c> per channel (byte = round(q*255)). Returned A = 255.
    ''' <para>The (TINC, TINV) resolution MIRRORS <see cref="ComposeLinearRgba"/> EXACTLY so the body QNAM and the
    ''' baked/rendered face use the identical skin-tone input: authored NPC tint for that layer INDEX if present
    ''' (TINC bytes /255, TINV u32 /100), else the RACE default (TIND→CLFM colour, DefaultValue). Measured
    ''' byte-exact (Afflicted TINC=(0.263,0.016,0.004)@0.52 → QNAM (96,63,61)). SSE-only; FO4 never calls this.
    ''' <para><paramref name="offset"/> = ajuste manual del tono del CUERPO (editor de cuerpo). Se suma a
    ''' (TINC, TINV) ANTES del pliegue, que es el ÚNICO punto donde la intensidad tiene su significado SSE: acá
    ''' la lleva el interp de la capa (el QNAM de SSE no tiene alpha donde guardarla), y el pliegue corre con el
    ''' seed y la convención que resuelve la config — no con literales. Nothing (el default) ⇒ byte-idéntico al
    ''' comportamiento previo. ⛔ SÓLO lo pasan los call sites del CUERPO/save; los de la CARA (compositor,
    ''' sentinels skee, preset −2 del bake) NO, o el origen del match se movería junto con el destino.</para></summary>
    Public Function ResolveSkinToneQnam(pm As PluginManager, npc As NPC_Data, race As RACE_Data,
                                        raceFid As UInteger, isFemale As Boolean,
                                        Optional offset As SkinToneQnamOffset = Nothing) As Nullable(Of System.Drawing.Color)
        If pm Is Nothing OrElse npc Is Nothing Then Return Nothing

        ' Find the RACE skin-tone layer (TINP mask type == 6 = SkinTone). No slot-12 in SSE.
        Dim layers = GetRaceLayersOrdered(pm, raceFid, isFemale)
        If layers Is Nothing Then Return Nothing
        Dim skin As SseTintMask? = Nothing
        For Each layer In layers
            If layer.MaskType = 6 Then skin = layer : Exit For
        Next
        If Not skin.HasValue Then
            If Logger.Enabled Then
                Dim nLayers = If(layers Is Nothing, 0, layers.Count)
                Logger.LogLazy(Function() $"[SSE-QNAM] raceFid=0x{raceFid:X8} female={isFemale} → NO skin-tone layer (MaskType=6) among {nLayers} race layers → QNAM=Nothing")
            End If
            Return Nothing
        End If

        ' Tints AUTORADOS del NPC — MISMA FUENTE que las réplicas CPU/GPU de la cara (BuildNpcAuthoredTintMap).
        ' Era la TERCERA copia literal del mismo parseo; su comentario ya declaraba "EXACTLY as ComposeLinearRgba
        ' does", que es justo lo que una copia no puede garantizar. Sus guardas de nulidad (las más estrictas de
        ' las tres) se adoptaron en la función compartida. ⛔ No re-inlinear: el tono del CUERPO y el de la CARA
        ' tienen que salir del MISMO parseo o divergen en silencio.
        ' Sólo hay lista cruda (npc.SseTintRaw), sin PluginRecord ⇒ se pasa como override; Nothing ⇒ mapa vacío.
        Dim npcMap = BuildNpcAuthoredTintMap(Nothing, npc.SseTintRaw)

        ' Resolve the skin layer's colour + interp: authored (npcMap) wins, else RACE default CLFM + DefaultValue.
        ' This is the SAME branch ComposeLinearRgba runs per layer (see the For Each layer loop there).
        Dim cr As Double, cg As Double, cbb As Double, iv As Double
        Dim authored As Double() = Nothing
        If npcMap.TryGetValue(skin.Value.Index, authored) Then
            cr = authored(0) : cg = authored(1) : cbb = authored(2) : iv = authored(3)   ' TINC/255 + TINV/100
        Else
            Dim dc = ResolveClfmColor(pm, skin.Value.DefaultClfm)
            cr = dc(0) : cg = dc(1) : cbb = dc(2) : iv = skin.Value.DefaultValue
        End If

        ' Ajuste manual del tono del CUERPO: se suma ACÁ, sobre (TINC, TINV) ya normalizados a [0..1] y ANTES
        ' del pliegue, para que la intensidad la aplique el MISMO lerp(seed, color, cobertura) del engine con el
        ' seed y la convención de la config. Post-procesar el color plegado obligaría a re-derivar el seed y el
        ' espacio afuera, duplicando la ley. Offset Nothing/cero ⇒ no toca nada.
        If offset IsNot Nothing AndAlso Not offset.IsZero Then
            offset.ApplyToRgb01(cr, cg, cbb)
            iv = offset.ApplyToIntensity01(iv)
        End If

        ' Fold intensity into the colour: q = lerp(0.5, TINC, TINV) per channel. QNAM.A = 255 (no SSE alpha).
        ' El QNAM se compone con EL MISMO seed y LA MISMA ley que la cara (fase 8): si salieran de números
        ' distintos, el cuerpo y la cara se desincronizarían apenas el usuario mueva el bucket.
        Dim qSeed = BuildSeedSpec()
        Dim qConv = FaceTintConvention.ResolveConvention(FaceTintConvention.FaceTintStage.TintDiffuse,
                                                         FaceTintChannel.Diffuse, isTextureSet:=False, blendOp:=0)
        Dim sR As Single = qSeed.R, sG As Single = qSeed.G, sB As Single = qSeed.B
        If qSeed.Kind <> FaceTintCpuCompositor.FaceTintSeedKind.Constant Then
            ' Sin seed constante no hay un número plano del que partir (el facetint es tint-only): se usa el
            ' default del campo, que es de donde salía el literal 0,5 que esto reemplaza.
            Dim k = FaceTintConvention.SeedConstantValue()
            sR = k(0) : sG = k(1) : sB = k(2)
        End If
        Dim rB = FoldSkinChannel(CSng(cr), CSng(iv), sR, qConv)
        Dim gB = FoldSkinChannel(CSng(cg), CSng(iv), sG, qConv)
        Dim bB = FoldSkinChannel(CSng(cbb), CSng(iv), sB, qConv)
        If Logger.Enabled Then
            Dim skIdx = skin.Value.Index
            Dim wasAuthored = (authored IsNot Nothing)
            Dim crL = cr, cgL = cg, cbL = cbb, ivL = iv, rBL = rB, gBL = gB, bBL = bB
            Logger.LogLazy(Function() $"[SSE-QNAM] raceFid=0x{raceFid:X8} female={isFemale} skinLayerIdx={skIdx} authored={wasAuthored} TINC=({crL:F3},{cgL:F3},{cbL:F3}) TINV={ivL:F3} → QNAM=({rBL},{gBL},{bBL})")
        End If
        Return System.Drawing.Color.FromArgb(255, rB, gB, bB)
    End Function

    ''' <summary>True iff the RACE (for this gender) has a SKIN-TONE tint layer (TINP mask type == 6). The SSE
    ''' body-softlight guard uses this in place of FO4's slot-12 catalog check. SSE-only.</summary>
    Public Function RaceHasSkinToneLayer(pm As PluginManager, raceFid As UInteger, isFemale As Boolean) As Boolean
        If pm Is Nothing Then Return False
        Dim layers = GetRaceLayersOrdered(pm, raceFid, isFemale)
        If layers Is Nothing Then Return False
        For Each layer In layers
            If layer.MaskType = 6 Then Return True
        Next
        Return False
    End Function

    ''' <summary>Pliega la intensidad del skin-tone en UN canal de color y devuelve el byte 0..255.
    ''' <c>cNorm</c> = TINC/255, <c>tinv</c> = la cobertura de la capa.
    ''' <para>⭐ FASE 8 — EL <c>0,5</c> SALE DEL BUCKET, NO DE UN LITERAL. Esto era
    ''' <c>q = 0,5 + tinv·(cNorm − 0,5)</c>, o sea EXACTAMENTE el composite de una capa sobre el seed del
    ''' acumulador: <c>lerp(seed, color, cobertura)</c>. Tenerlo cableado significaba que el QNAM del CUERPO
    ''' y el facetint de la CARA se calculaban desde dos números distintos apenas el usuario moviera el seed
    ''' en CharGen Options — y se desincronizaban en silencio, que es justo lo que el QNAM existe para
    ''' evitar (el cuello matchea el pecho).</para>
    ''' <para>Ahora el seed es el MISMO <see cref="BuildSeedSpec"/> que usa el compose de la cara, y el
    ''' composite es el dispatch compartido con la convención de la capa de piel. Con los defaults de SSE
    ''' (seed 0,5 · Blend=Replace · todo Linear) da <b>exactamente</b> el mismo número que el literal ⇒
    ''' byte-idéntico. Medido byte-exacto contra el CK (Afflicted TINC=(0.263,0.016,0.004)@0.52 → QNAM
    ''' (96,63,61)).</para></summary>
    Private Function FoldSkinChannel(cNorm As Single, tinv As Single, seedChannel As Single,
                                     conv As FaceTintConvention.FaceTintConventionSet) As Integer
        Dim q = FaceTintCpuCompositor.ComposePixel(seedChannel, cNorm, tinv, conv)
        If q < 0.0F Then q = 0.0F
        If q > 1.0F Then q = 1.0F
        Return CInt(MathF.Round(q * 255.0F, MidpointRounding.ToEven))
    End Function

    ''' <summary>Color de una CLFM (CNAM). ⛔ EL FALLO NO PUEDE SER SILENCIOSO.
    ''' Un CLFM que no resuelve devolvia BLANCO sin decir nada, y BLANCO es un color perfectamente valido:
    ''' un fallo de RESOLUCION se disfrazaba de resultado. Asi es como el bug de remapeo de TIND
    ''' (ver GetRaceLayersOrdered) sobrevivio hasta hoy — con la capa SkinTone a cobertura 1,0, la cara
    ''' entera salia blanca y no habia ni una linea de log que lo dijera.
    ''' ⚠️ El VALOR del fallback NO se cambia: no esta RE-ado que hace el motor con una CLFM irresoluble, y
    ''' inventar otro color seria cambiar el bake sin fuente. Lo que cambia es que ahora se LOGUEA con el
    ''' FormID y la causa, una sola vez por FormID (la cache evita el spam), para que el caso aparezca en vez
    ''' de esconderse. `clfmFid = 0` NO se loguea: es "esta capa no declara color por defecto", condicion
    ''' normal del corpus (las capas de warpaint) y ademas siempre viene con cobertura 0 ⇒ inerte.</summary>
    Private Function ResolveClfmColor(pm As PluginManager, clfmFid As UInteger) As Double()
        If clfmFid = 0 Then Return New Double() {1.0, 1.0, 1.0}
        Dim cached As Double() = Nothing
        If _clfmCache.TryGetValue(clfmFid, cached) Then Return cached
        Dim col = New Double() {1.0, 1.0, 1.0}
        Dim rec = pm.GetRecord(clfmFid)
        Dim why As String = Nothing
        If rec Is Nothing Then
            why = "el record NO EXISTE (¿FormID sin remapear a los masters del plugin?)"
        ElseIf rec.Header.Signature <> "CLFM" Then
            why = $"el record es '{rec.Header.Signature}', no CLFM"
        Else
            Dim found = False
            For Each sr In rec.Subrecords
                If sr.Signature = "CNAM" AndAlso sr.Data.Length >= 3 Then
                    col = New Double() {sr.Data(0) / 255.0, sr.Data(1) / 255.0, sr.Data(2) / 255.0}
                    found = True
                    Exit For
                End If
            Next
            If Not found Then why = "la CLFM no trae CNAM"
        End If
        If why IsNot Nothing Then
            Dim fidLog = clfmFid, whyLog = why
            Logger.LogLazy(Function() $"[SSE-TINT] CLFM 0x{fidLog:X8} NO RESUELVE: {whyLog}. Se usa BLANCO (1,1,1) " &
                                      "como fallback SIN FUENTE — si esta capa tiene cobertura > 0 el resultado " &
                                      "esta MAL. Ver ResolveClfmColor.")
        End If
        _clfmCache(clfmFid) = col
        Return col
    End Function

    ' ⛔⛔ BORRADOS `ComposeLayer`, `ComposeLayerOne` y `ComposeLayerRangeV` (fase 5 de la unificación).
    ' Eran el LOOP de capas propio de SSE — la segunda implementación del mismo camino que ya corría en
    ' `FaceTintCpuCompositor.ComposeChannelAccum`. El álgebra por píxel ya era compartida (llamaban a
    ' ComposePixel/ComposeOneV): lo duplicado era el recorrido, el acumulador AoS, el early-out de bloque, el
    ' prólogo/cola de alineación y la protección del alpha — cinco sutilezas escritas dos veces.
    ' Con `ComposeLinearRgba` convertida en fachada (builder → accum) no queda nada que las llame.
    ' Sus EJES de test se migraron al self-test compartido y NO se perdió ninguno:
    '   · ley SSE (all-linear/Replace) y ley FO4 (softlight en G22) → ComposeVectorSelfTest, casos 1/2/6
    '   · mask conv y tamaños que no son múltiplo del ancho      → ComposeVectorSelfTest (mc 0..6 × 11 largos)
    '   · los cuatro canales de máscara                          → PhaseAVectorSelfTest (palMaskCh 0..3)
    '   · cobertura CERO                                         → ComposeVectorSelfTest, índice 7
    '   · fuente de capa YA decodificada (lo NUEVO de esta fase)  → PhaseAVectorSelfTest, bloque `layerUnit`
    ' ⚠️ CAMBIO DE COMPORTAMIENTO DECLARADO, cobertura NaN: el guard de acá era `a <= 0`, que con NaN es FALSO
    ' y por lo tanto COMPONÍA; el compositor compartido usa `cov > 0`, que con NaN es FALSO y SALTEA — igual
    ' que el GLSL. Es la decisión 9 del plan (alinear al GPU). NO mueve bytes: la cobertura sale de máscaras de
    ' 8 bits × TINV, y por ahí el NaN no es alcanzable.

    ''' <summary>Aplica el orden configurable SSE (<c>Setting_FaceTintSort_SSE.TintRules</c>, claves
    ''' <see cref="FaceTintSseTintSortKey"/>) sobre las capas del RACE que devuelve <see cref="GetRaceLayersOrdered"/>.
    ''' DEFAULT = <c>[Race_Order asc]</c> = IDENTIDAD ⇒ orden RaceMenu (posición en el RACE) ⇒ compose byte-idéntico.
    ''' El sort tiene tiebreak final por posición original (orden RACE), así claves iguales preservan RaceMenu. El
    ''' lerp NO es conmutativo ⇒ cualquier regla != default DESVÍA de RaceMenu (elección explícita del usuario).</summary>
    Public Function SortSseTintLayers(layers As List(Of SseTintMask), npcMap As Dictionary(Of Integer, Double())) As List(Of SseTintMask)
        If layers Is Nothing OrElse layers.Count <= 1 Then Return layers
        Dim cfg = Config_App.Current?.Setting_FaceTintSort_SSE
        Dim rules = If(cfg IsNot Nothing, cfg.TintRules, Nothing)
        Dim placement = If(cfg IsNot Nothing, cfg.SkinTonePlacement, CInt(FaceTintSkinTonePlacement.Positional))
        Dim hasRules = (rules IsNot Nothing AndAlso rules.Count > 0)
        ' Sin reglas Y con el placement en su default no hay nada que reordenar ⇒ lista tal cual (orden RACE).
        If Not hasRules AndAlso placement = CInt(FaceTintSkinTonePlacement.Positional) Then Return layers
        Dim items As New List(Of (Layer As SseTintMask, Pos As Integer))
        For i = 0 To layers.Count - 1 : items.Add((layers(i), i)) : Next
        If hasRules Then
            items.Sort(Function(a, b)
                           For Each r In rules
                               Dim c = SseTintKey(a.Layer, a.Pos, npcMap, r.Key).CompareTo(SseTintKey(b.Layer, b.Pos, npcMap, r.Key))
                               If r.Descending Then c = -c
                               If c <> 0 Then Return c
                           Next
                           Return a.Pos.CompareTo(b.Pos)   ' tiebreak estable = orden RACE (RaceMenu)
                       End Function)
        End If
        ' ⭐ SkinTonePlacement — la MISMA ley que FO4 (FaceTintInputBuilder.OrderMergedLayers): GANA sobre las
        ' reglas y saca la capa de SKIN-TONE del orden para forzarla al frente (queda al fondo) o al final
        ' (queda encima). En SSE la capa de piel es la de TINP MaskType == 6 — no hay slot 12 —, la MISMA que
        ' identifica ResolveSkinToneQnam, así que la cara y el QNAM del cuerpo hablan de la misma capa.
        ' ⛔ ESTA OPCION NO SE LEIA ACA: existía en la UI del tab "Tint Order", se persistía en
        ' Setting_FaceTintSort_SSE, y su ÚNICO consumidor era el builder de FO4 ⇒ en Skyrim era un control
        ' editable que no movía un byte. Default = Positional ⇒ byte-inerte mientras no se toque.
        If placement = CInt(FaceTintSkinTonePlacement.FirstOfAll) Then
            items = items.Where(Function(x) x.Layer.MaskType = 6).Concat(items.Where(Function(x) x.Layer.MaskType <> 6)).ToList()
        ElseIf placement = CInt(FaceTintSkinTonePlacement.LastOfAll) Then
            items = items.Where(Function(x) x.Layer.MaskType <> 6).Concat(items.Where(Function(x) x.Layer.MaskType = 6)).ToList()
        End If
        Return items.Select(Function(x) x.Layer).ToList()
    End Function

    Private Function SseTintKey(layer As SseTintMask, pos As Integer, npcMap As Dictionary(Of Integer, Double()), key As Integer) As Double
        Select Case CType(key, FaceTintSseTintSortKey)
            Case FaceTintSseTintSortKey.Tint_Index : Return layer.Index
            Case FaceTintSseTintSortKey.Mask_Type : Return layer.MaskType
            Case FaceTintSseTintSortKey.Authored : Return If(npcMap IsNot Nothing AndAlso npcMap.ContainsKey(layer.Index), 1.0, 0.0)
            Case FaceTintSseTintSortKey.Coverage
                Dim authored As Double() = Nothing
                If npcMap IsNot Nothing AndAlso npcMap.TryGetValue(layer.Index, authored) Then Return authored(3)
                Return layer.DefaultValue
            Case Else : Return pos   ' Race_Order (default) = posición en el RACE
        End Select
    End Function

    Public Function GetRaceLayersOrdered(pm As PluginManager, raceFid As UInteger, female As Boolean) As List(Of SseTintMask)
        Dim key = raceFid.ToString() & If(female, "F", "M")
        Dim cached As List(Of SseTintMask) = Nothing
        If _layersCache.TryGetValue(key, cached) Then Return cached
        Dim layers As New List(Of SseTintMask)
        Dim rr = pm.GetRecord(raceFid)
        If rr IsNot Nothing Then
            ' RACE tint layer: TINI, TINT, TINP, TIND(default preset formID), then a preset LIST of
            ' [TINC(CLFM formID), TINV(FLOAT 0-1), TIRS(idx)]×N. The default = the preset whose TINC==TIND;
            ' its TINV is the race-default coverage. (Gender: the head-data MNAM/FNAM markers precede the
            ' male/female TINI blocks — the earlier movement MNAM/FNAM are before any TINI so are harmless.)
            Dim inFemale = False, ci = -1, cp = "", ct = -1
            Dim cd As UInteger = 0
            ' preset list: [CLFM formID, TINV float, TIRS id] per preset (default value + TIAS→value lookup)
            Dim presets As New List(Of (Clfm As UInteger, Val As Double, Tirs As Integer))
            Dim lastClfm As UInteger = 0, lastVal As Double = 0
            Dim flush = Sub()
                            If female = inFemale AndAlso ci >= 0 AndAlso cp <> "" Then
                                ' default preset = the one whose CLFM == TIND; its TINV = default coverage (engine-
                                ' verified: 0xFCB3F2/0xFE52CA find the TIND-matching preset; ColorAverage@0 = off).
                                Dim dval As Double = 0
                                For Each pr In presets
                                    If pr.Clfm = cd Then dval = pr.Val : Exit For
                                Next
                                ' Snapshot the layer's preset swatches (TIRS→CLFM/value) for the editor's dropdown. Same
                                ' list the default-value lookup above reads; copied so the shared 'presets' can be reused.
                                Dim presetSnap As New List(Of SseTintPreset)(presets.Count)
                                For Each pr In presets
                                    presetSnap.Add(New SseTintPreset With {.Tirs = pr.Tirs, .Clfm = pr.Clfm, .Value = pr.Val})
                                Next
                                layers.Add(New SseTintMask With {.Index = ci, .Path = cp, .MaskType = ct, .DefaultClfm = cd, .DefaultValue = dval, .Presets = presetSnap})
                            End If
                        End Sub
            For Each sr In rr.Subrecords
                Select Case sr.Signature
                    Case "MNAM" : flush() : inFemale = False : ci = -1 : cp = "" : ct = -1 : cd = 0 : presets.Clear()
                    Case "FNAM" : flush() : inFemale = True : ci = -1 : cp = "" : ct = -1 : cd = 0 : presets.Clear()
                    Case "TINI" : flush() : ci = BitConverter.ToUInt16(sr.Data, 0) : cp = "" : ct = -1 : cd = 0 : presets.Clear()
                    Case "TINT" : cp = sr.AsStringGeneral
                    Case "TINP" : ct = BitConverter.ToUInt16(sr.Data, 0)
                    ' â›” REMAPEO DE MASTERS OBLIGATORIO. El TIND y el TINC de la RACE son FormIDs, y el FormID
                    ' guardado usa el indice LOCAL de masters del plugin que lo escribio: leerlo con
                    ' BitConverter a secas devuelve un FormID de OTRO plugin.
                    ' Medido: una RACE con TIND local que es AUTO-REFERENCIA (indice local == cantidad de
                    ' masters) resolvia a un plugin ajeno, no existia, y ResolveClfmColor degradaba a BLANCO -
                    ' la unica capa con cobertura completa pintaba la cara entera de blanco.
                    ' â›” Ojo con la ambiguedad de TINC: a nivel RACE es un FormID de CLFM (esto), a nivel NPC son
                    ' 3 bytes RGB (BuildNpcAuthoredTintMap). Los del NPC NO se remapean, no son FormIDs.
                    Case "TIND" : If sr.Data.Length >= 4 Then cd = pm.ResolveReferencedFormID(rr.SourcePluginName, BitConverter.ToUInt32(sr.Data, 0))
                    Case "TINC" : If sr.Data.Length >= 4 Then lastClfm = pm.ResolveReferencedFormID(rr.SourcePluginName, BitConverter.ToUInt32(sr.Data, 0))  ' RACE preset: CLFM formID
                    Case "TINV" : If sr.Data.Length >= 4 Then lastVal = BitConverter.ToSingle(sr.Data, 0)   ' RACE TINV = FLOAT 0-1
                    Case "TIRS" : If sr.Data.Length >= 2 Then presets.Add((lastClfm, lastVal, BitConverter.ToUInt16(sr.Data, 0)))
                End Select
            Next
            flush()
        End If
        _layersCache(key) = layers
        Return layers
    End Function

    ''' <summary>LA normalizacion de una ruta de textura a clave del FilesDictionary, para TODO el SSE. Delega
    ''' en <see cref="FO4UnifiedMaterial_Class.CorrectTexturePath"/>, la MISMA que ya usaba el camino GPU.
    ''' <para>â›” Existe porque aca habia una normalizacion propia mientras el GPU usaba CorrectTexturePath: dos
    ''' leyes para el MISMO path, o sea que una podia resolver y la otra no, y eso se manifiesta como "el
    ''' overlay aparece en un camino y desaparece en el otro" - rompe la paridad CPU==GPU en el ORIGEN, antes de
    ''' componer un pixel. El caso concreto que solo fallaba en CPU: un path que ya trae el prefijo
    ''' <c>data\</c> daba <c>textures\data\textures\...</c>.</para></summary>
    Public Function NormalizeTextureKey(texPath As String) As String
        Return FO4UnifiedMaterial_Class.CorrectTexturePath(texPath)
    End Function

    ''' <summary>Decode a texture (FilesDictionary key) to linear RGBA[0,1] at exactly W×H (bilinear). Public
    ''' wrapper over <see cref="DecodeMask"/> so other SSE compositors (overlays into the per-NPC diffuse) reuse
    ''' the SAME decode+resize+cache path. Nothing when the file is missing/undecodable.</summary>
    Public Function DecodeTextureRgba(texPath As String, w As Integer, h As Integer) As Single()
        Return DecodeMask(texPath, w, h)
    End Function

    ''' <summary>Decode de un NORMAL MAP a RGBA[0,1] en exactamente W x H: el MISMO camino de decode + resize +
    ''' cache que <see cref="DecodeTextureRgba"/>, mas la reconstruccion del eje Z cuando la fuente trae 2
    ''' canales (BC5/R8G8).
    ''' <para>â›” Por que no alcanza con el de color: <c>DecodeDds</c> empaqueta las fuentes de 2 canales como
    ''' B=0, A=1. Leido como color es inocuo; leido como VECTOR da z = -1, o sea la normal del tatuaje apuntando
    ''' hacia adentro y el lighting invertido en toda la zona cubierta. Y BC5 es justamente el formato estandar
    ''' de los normales tangent-space de SSE, que es lo que traen los face-paint de RaceMenu.</para>
    ''' <para>El cache usa un NAMESPACE PROPIO (sufijo |nrm): la misma ruta puede pedirse como color y como
    ''' normal, y servir una por la otra devolveria un buffer con el B equivocado.</para></summary>
    Public Function DecodeNormalRgba(texPath As String, w As Integer, h As Integer) As Single()
        Return DecodeMask(texPath, w, h, asNormalMap:=True)
    End Function

    ''' <summary>Decode a mask texture (FilesDictionary key) to linear RGBA[0,1] at exactly W×H (bilinear).
    ''' Cached at ANY size — la clave lleva el tamaño destino, así que no hay ningún tamaño hardcodeado. La
    ''' memoria la acotan DOS cosas: la VIDA del caché (per-NPC fuera del lote, ver
    ''' <see cref="ClearTextureCaches"/>) y, con lote activo, el techo que aplica
    ''' <c>FaceTintCpuCompositor.CachedUnitDecode</c>. Nothing when the file is missing/undecodable.</summary>
    ''' <param name="asNormalMap">True ⇒ la fuente se interpreta como VECTOR: si trae 2 canales se despeja el eje
    ''' Z tras el resample (ver <see cref="DecodeNormalRgba"/>). False (default) = comportamiento previo, sin tocar
    ''' un solo byte de ningún caller existente.</param>
    Private Function DecodeMask(texPath As String, w As Integer, h As Integer,
                                Optional asNormalMap As Boolean = False) As Single()
        ' ⭐ COLAPSADO. Todo el cuerpo —clave, negativos, decode, atajo de identidad, resample bilineal y
        ' reconstruccion de Z— vivia aca duplicado sobre un `_texCache` de MODULO propio. Ahora es
        ' FaceTintCpuCompositor.CachedUnitDecode: mismo algoritmo, una sola implementacion, y la clave gana el
        ' eje de POLITICA DE MIP que a esta le faltaba.
        ' La NORMALIZACION del path se queda aca: es la misma que usa el camino GPU (ver NormalizeTextureKey),
        ' y lo que se comparte con el GL es el normalizador, NO la clave completa (agregarle WxH a la clave del
        ' cache GL fragmentaria VRAM: el GL guarda la textura con todos sus mips y elige nivel en el shader).
        Return FaceTintCpuCompositor.CachedUnitDecode(NormalizeTextureKey(texPath), w, h, asNormalMap)
    End Function

End Module
