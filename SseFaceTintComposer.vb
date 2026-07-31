Imports System.Collections.Concurrent
Imports System.Runtime.CompilerServices
Imports System.Linq

''' <summary>
''' SSE (Skyrim Special Edition) face-tint compositor — the engine-faithful reproduction of what the
''' CreationKit bakes into <c>FaceGenData\FaceTint\&lt;plugin&gt;\&lt;fid&gt;.dds</c> (512×512 DXT5, _d only).
'''
''' ENGINE MODEL (verified in CreationKit.exe, re_sseck — NOT curve-fit):
'''  - BSFaceGenUtils (bsfacegenutils.cpp) builds a float4[16] of {color×1/255, interp} per tint mask and
'''    hands it to an image-space pixel shader. It NEVER reads a "type"/TINP field: EVERY layer is the same
'''    coverage-gated lerp — there is NO per-type blend-op. Setter @0x142ce73e0 (imagespaceshaderparam.cpp)
'''    is a pure store into the pixel-constant buffer. Color unpack constant = 1/255 (0.00392156) confirmed.
'''  - Per layer: coverage = maskR × TINV, then acc = lerp(acc, color, coverage). Uniform for all layers.
'''  - Base = the NPC skin colour (QNAM) FLAT. Measured: CK's baked _d skin region is flat = QNAM (Dremora
'''    QNAM=0 → CK black exact). The diffuse skin DETAIL is added at RENDER by the shader, so it is NOT in the
'''    baked _d (using the head diffuse as the base gives ~52/255 — wrong). The DXT5 target has a ~3-5/255
'''    compression floor.
'''
''' ⚠️ SOBRE LA PALABRA "LINEAR" EN ESTE MODULO (anotado, NO renombrado): ni
''' <see cref="FaceTintCpuCompositor.DecodeDds"/> ni <see cref="DecodeTextureRgba"/> aplican la curva
''' sRGB→lineal. Devuelven <c>byte/255</c> CRUDO (ver el bucle de conversion de DecodeDds:
''' <c>outArr(o) = CSng(r / 255.0)</c>, sin curva). O sea que "linear RGBA" en las firmas y summaries de este
''' modulo, de SseOverlayCompositor y de SseSkeeMaskReader significa "valor de ALMACENAMIENTO normalizado a
''' [0,1]" — que para una textura de color es sRGB, no luz lineal. Es correcto para lo que hace el compose del
''' facetint (las mascaras son datos, no color, y el seed 0.5 es un valor de almacenamiento), pero NO hay que
''' leerlo como "aca se trabaja en lineal". El UNICO lugar que de verdad linealiza es el fold
''' (<see cref="SseFaceGenBaker.FoldFacetintIntoDiffuse"/>, que llama Srgb2Lin/Lin2Srgb explicitamente).
'''
''' Measured mean 4.68/255 over 2032 Skyrim.esm NPCs (median 2.36, 98.4% ≤20/255). Tattoo masks (TINI 65-74)
''' carry TINT but no TINP — they must still register (flush-on-new-TINI), else war-paint NPCs go to ~74/255.
'''
''' This is the SSE analogue of <see cref="FaceTintInputBuilder"/> (FO4). It is SSE-only; callers gate on
''' <c>Config_App.Current.Game = Config_App.Game_Enum.Skyrim</c>. See project_sse_facetint_spec.
''' </summary>
Public Module SseFaceTintComposer

    ''' <summary>CAPACIDAD DECLARADA de este compositor, para los caminos GL que lo tienen de ESPEJO.
    ''' Este modulo SI implementa la ley de los cuatro espacios: compone con
    ''' <see cref="FaceTintCpuCompositor.ComposePixel"/> (el MISMO ComposeOne del loop FO4) y mantiene su
    ''' acumulador en <c>conv.AccumSpace</c> — siembra en AccumSpace y hace UNA sola conversion a OutputSpace
    ''' al final. Por eso declara <c>FourSpaceAccumulator</c> y <c>AccumInCompositeSpace</c> vale acá igual
    ''' que en FO4.
    ''' <para>ANTES declaraba <c>OutputSpaceOnly</c> y el flag quedaba INERTE en SSE. Eso NO era un diseño:
    ''' era una compensacion. El GL comparte <c>FaceTintCompositor.ApplyFaceTintPipeline</c> con FO4
    ''' (SseFoldLayerStack lo llama en 7 sitios), asi que con el CPU sin implementarlo habia que SUPRIMIR el
    ''' flag del lado GL para que los dos no divergieran — se apagaba el sintoma en un lado en vez de cerrar
    ''' el hueco en el otro. El argumento de que "SSE es all-linear asi que da igual" tampoco servia: eso es
    ''' una COINCIDENCIA de configuracion (alcanza con poner os=G22 desde CharGen Options para romperla), y
    ''' ademas dejaba abierto un hueco MAYOR — con espacios separados el GL honraba la ley y el CPU no.</para>
    ''' <para>REGRESION: nula por construccion en vanilla. Con los defaults de SSE (all-linear)
    ''' CompositeSpace == OutputSpace ⇒ AccumSpace == OutputSpace prendido o apagado ⇒ las dos conversiones
    ''' nuevas son no-op y la salida es byte-identica al modelo previo.</para></summary>
    Public Const AccumSpaceCapability As FaceTintConvention.FaceTintCpuMirrorCapability =
        FaceTintConvention.FaceTintCpuMirrorCapability.FourSpaceAccumulator

    ''' <summary>One RACE tint mask: the greyscale mask texture path + its TINP mask type (-1 when the layer
    ''' omits TINP, e.g. tattoos). Type is retained for tooling/diagnostics only — the blend is uniform.</summary>
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
    ' RESULTADO (decode + resize) cacheado por (path, TAMAÑO DESTINO, color/normal) — las máscaras compartidas
    ' del RACE se decodifican y resamplean UNA vez por tamaño. ⛔ La key llevaba SOLO el path, y para no servir
    ' un buffer del tamaño equivocado el código restringía el caché a 512² con cuatro guardas: o sea que el fold
    ' (que compone a la resolución NATIVA del complexion) no podía cachear NUNCA, y el camino no-plegado perdía
    ' el caché apenas el usuario elegía otra resolución en CharGen Options. Ver DecodeMask.
    Private ReadOnly _texCache As New ConcurrentDictionary(Of String, Single())(StringComparer.OrdinalIgnoreCase)
    ' Resolved CLFM formID -> linear RGB [0,1] (race-default colours), cached.
    Private ReadOnly _clfmCache As New ConcurrentDictionary(Of UInteger, Double())

    ' ⛔ ESTOS CACHES **NO** CONSULTAN EL TECHO (`BatchDecodeCacheBudgetBytes`), A PROPOSITO — misma decision
    ' que del lado FO4, donde `CachedDecode` saltea el presupuesto con `Not ReferenceEquals(cache,
    ' BatchDecodeCache)` para todo cache que no sea el del batch.
    ' POR QUE: su vida es PER-NPC (ver ClearTextureCaches). Rechazar una entrada en un cache per-NPC no ahorra
    ' nada duradero — garantiza el re-decode/re-resample DENTRO DEL MISMO NPC, o sea exactamente durante la
    ' edicion viva, que es el caso para el que el cache existe. Lo que acota la memoria aca es la VIDA, no un
    ' presupuesto.
    ' (Hubo una version intermedia con admision por presupuesto: se escribio ANTES de fijar la vida per-NPC,
    '  para reemplazar los limites accidentales que se estaban sacando —el gate de 512² y el literal de 4 MB—
    '  y quedo mal encajada apenas la vida paso a ser per-NPC. No re-proponerla.)

    ''' <summary>⭐ Suelta el caché de TEXTURA — el que pesa: el resultado decodificado+resampleado
    ''' (<see cref="_texCache"/>).
    ''' <para><b>VIDA PER-NPC, igual que <c>ClearFaceTintCaches</c> del lado FO4</b>: se conservan entre
    ''' recargas del MISMO NPC —para que la edicion viva siga siendo rapida al segundo click— y se sueltan al
    ''' cambiar de NPC raiz. Las mascaras del RACE son compartidas entre NPCs de esa raza, asi que soltarlas
    ''' cuesta re-decode+re-resample en el proximo cambio; se paga a proposito para que la app no acumule
    ''' memoria navegando (decision explicita: la app corre SIN techo de presupuesto).</para>
    ''' <para>⛔ NO toca <see cref="_layersCache"/> ni <see cref="_clfmCache"/>: son datos de RECORD (lista
    ''' ordenada de capas por raza+genero, CLFM→RGB), no pesan y re-parsearlos en cada cambio de NPC seria
    ''' churn puro. Su vida es la del LOAD ORDER y la maneja <see cref="ClearCaches"/>. Es el mismo reparto que
    ''' del lado FO4, donde <c>ClearFaceTintCaches</c> suelta bytes/GL/decodes y los caches de PARSE los suelta
    ''' <c>InvalidateParseCaches</c>.</para>
    ''' <para>⛔ El BARRIDO del bake NO pasa por aca (llama a <c>BuildCharGen</c> directo, no al camino de load
    ''' del render), asi que ahi la reutilizacion entre NPCs de la misma raza se conserva — que es justo donde
    ''' mas rinde.</para></summary>
    Public Sub ClearTextureCaches()
        _texCache.Clear()
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
        Dim npix = w * h

        ' ENGINE-EXACT — decoded 100% from (a) the facegen-tint pixel shader (ps_5_0, DXBC @0x40033A8) and
        ' (b) the cb2-source builder @0x18C9F40. Shader: base = 0.5; per layer acc = lerp(acc, colour,
        ' mask.r × interp); out.w = 1; UNIFORM, no per-type branch. Builder: iterate the RACE's tint layers
        ' IN RACE ORDER — for each, colour+interp come from the NPC's authored tint for that layer INDEX if
        ' present, else the RACE default (TIND→CLFM colour). interp = value_byte × 0.01 (both confirmed in the
        ' binary). The RACE order (not NPC subrecord order) is what fills cb2[0..15]; lerp is not commutative.

        ' Tints AUTORADOS del NPC — FUENTE ÚNICA compartida con la réplica GPU (BuildLayerInputs).
        Dim npcMap = BuildNpcAuthoredTintMap(npcRec, npcTintOverride)

        Dim layers = SortSseTintLayers(GetRaceLayersOrdered(pm, raceFormID, isFemale), npcMap)   ' orden configurable, default RaceMenu

        ' Ley SSE del config (FaceTintConvention.ActiveSettings, default DefaultsFor(Skyrim)): el compositor NO
        ' hardcodea el álgebra — seed, canal de máscara y espacios/blend vienen de la convención. Con los defaults
        ' SSE (seed constante 0.5, máscara canal R cruda, todo Linear, Blend=Replace) esto es BYTE-IDÉNTICO al
        ' modelo previo (lerp(acc,color,maskR×tinv)); el usuario puede tunearlos desde CharGen Options (tab SSE).
        Dim settings = FaceTintConvention.ActiveSettings()
        Dim conv = FaceTintConvention.ResolveConvention(isTextureSet:=False, slot:=0US, blendOp:=0,
                                                        channel:=FaceTintChannel.Diffuse, useHairPalette:=False)
        Dim maskConvI As Integer = CInt(conv.MaskConv)
        Dim maskCh As Integer = MaskChannelIndex(settings.Diffuse)   ' SSE default = R (0)

        ' Seed del acumulador: Constant (SSE, engine-verificado 0.5) o la baseImg del caller (diagnóstico).
        Dim acc(npix * 4 - 1) As Single
        Dim seedR As Double = 0.5, seedG As Double = 0.5, seedB As Double = 0.5
        If settings.SeedMode = FaceTintConvention.FaceTintSeedMode.Constant AndAlso settings.SeedConstant IsNot Nothing AndAlso settings.SeedConstant.Length >= 3 Then
            seedR = settings.SeedConstant(0) : seedG = settings.SeedConstant(1) : seedB = settings.SeedConstant(2)
        End If
        ' ACUMULADOR EN AccumSpace (misma ley que FO4). El seed del motor (0,5) y la baseImg del caller estan
        ' expresados en OutputSpace; el compose corre en AccumSpace y hay UNA sola conversion al final. Con la
        ' config default de SSE (all-linear) CompositeSpace == OutputSpace ⇒ accSp == osSp ⇒ las dos
        ' conversiones son no-op y la salida es BYTE-IDENTICA al modelo previo. Solo cambia si el usuario
        ' separa los espacios desde CharGen Options — que es justo el caso donde antes el CPU se quedaba en
        ' OutputSpace y el GL (compartido con FO4) honraba la ley, o sea DIVERGIAN.
        Dim accSp As Integer = CInt(conv.AccumSpace)
        Dim osSp As Integer = CInt(conv.OutputSpace)
        Dim needSpaceCvt As Boolean = (accSp <> osSp)
        If needSpaceCvt Then
            seedR = FaceTintCpuCompositor.ConvertSpaceShared(seedR, osSp, accSp)
            seedG = FaceTintCpuCompositor.ConvertSpaceShared(seedG, osSp, accSp)
            seedB = FaceTintCpuCompositor.ConvertSpaceShared(seedB, osSp, accSp)
        End If
        If baseImg IsNot Nothing AndAlso baseImg.Length >= npix * 4 Then
            Array.Copy(baseImg, acc, npix * 4)
            If needSpaceCvt Then
                System.Threading.Tasks.Parallel.ForEach(
                    System.Collections.Concurrent.Partitioner.Create(0, npix),
                    Sub(range)
                        For i = range.Item1 To range.Item2 - 1
                            For ch = 0 To 2
                                acc(i * 4 + ch) = CSng(FaceTintCpuCompositor.ConvertSpaceShared(acc(i * 4 + ch), osSp, accSp))
                            Next
                        Next
                    End Sub)
            End If
        Else
            ' Paralelo por rangos (escrituras disjuntas por píxel ⇒ bit-idéntico); a 4K son 67M de writes.
            System.Threading.Tasks.Parallel.ForEach(
                System.Collections.Concurrent.Partitioner.Create(0, npix),
                Sub(range)
                    For i = range.Item1 To range.Item2 - 1
                        acc(i * 4) = CSng(seedR) : acc(i * 4 + 1) = CSng(seedG) : acc(i * 4 + 2) = CSng(seedB) : acc(i * 4 + 3) = 1.0F
                    Next
                End Sub)
        End If

        ' Compose the RACE's tint layers IN RACE ORDER (= cb2 slot order; lerp is not commutative). The SKIN
        ' layer (type 6) is NOT special — it is a normal layer. Its colour = TINC (or the RACE default CLFM),
        ' composed at its TINV over base 0.5. VERIFIED (2026-07-17, vanilla shipped facetints vs resolved QNAM,
        ' 12/12 NPC discriminantes con hue-match; p.ej. 0010D4B9 QNAM (183,156,145) ≈ facetint (181,158,147)):
        ' el facetint SÍ hornea el skintone = lerp(0.5, skinTINC, skinTINV) = QNAM. So QNAM is the RESULT of this
        ' composite (stored for the body to match). This matches the disassembled bake builder (@0x18C9F40)
        ' literally: authored → npcTint colour = TINC; unauthored → RACE default CLFM (entry+0x30). No per-type branch.
        For Each layer In layers
            Dim cr As Double, cg As Double, cbb As Double, iv As Double
            Dim authored As Double() = Nothing
            If npcMap.TryGetValue(layer.Index, authored) Then
                cr = authored(0) : cg = authored(1) : cbb = authored(2) : iv = authored(3)   ' TINC + TINV/100 (raw)
            Else
                Dim dc = ResolveClfmColor(pm, layer.DefaultClfm)
                cr = dc(0) : cg = dc(1) : cbb = dc(2) : iv = layer.DefaultValue
            End If
            ' RaceMenu can override this layer's mask texture by index (PresetInterface.cpp:203). When present,
            ' composite the custom path instead of the RACE layer's own TINT path; else use the RACE path.
            Dim maskPath = layer.Path
            Dim custPath As String = Nothing
            If tintTexOverride IsNot Nothing AndAlso tintTexOverride.TryGetValue(layer.Index, custPath) AndAlso Not String.IsNullOrEmpty(custPath) Then
                maskPath = custPath
            End If
            If iv <= 0.0 OrElse String.IsNullOrEmpty(maskPath) Then Continue For
            Dim mi = DecodeMask(maskPath, w, h)
            If mi IsNot Nothing Then ComposeLayer(acc, mi, cr, cg, cbb, iv, npix, conv, maskConvI, maskCh)
        Next
        ' UNICA conversion de vuelta a OutputSpace (no-op cuando accSp == osSp).
        If needSpaceCvt Then
            System.Threading.Tasks.Parallel.ForEach(
                System.Collections.Concurrent.Partitioner.Create(0, npix),
                Sub(range)
                    For i = range.Item1 To range.Item2 - 1
                        For ch = 0 To 2
                            acc(i * 4 + ch) = CSng(FaceTintCpuCompositor.ConvertSpaceShared(acc(i * 4 + ch), accSp, osSp))
                        Next
                    Next
                End Sub)
        End If
        Return acc
    End Function

    ''' <summary>Mapa de tints AUTORADOS del NPC: índice de capa → {R, G, B (TINC/255), interp (TINV/100)}.
    ''' Subrecords por capa: TINI, TINC, TINV, TIAS (TIAS cierra la capa → commit).
    ''' ⭐⭐ FUENTE ÚNICA de las DOS réplicas: la CPU (<see cref="ComposeLinearRgba"/>) y la GPU
    ''' (<see cref="BuildLayerInputs"/>) llaman ACÁ. ⛔ Estaba DUPLICADO literalmente en ambas: editar una sola
    ''' habría hecho divergir CPU y GPU en el VALOR (no en los últimos bits), en silencio, sin que nadie se
    ''' equivocara en la matemática — el modo de fallo más barato de introducir y el más caro de diagnosticar.
    ''' ⛔ NO volver a inlinearlo en ninguna de las dos.
    ''' <paramref name="npcTintOverride"/> = capas editadas en el editor; Nothing ⇒ el record crudo. Se normaliza
    ''' a (sig, data) para que el PluginRecord y la lista de NPC_RawSubrecord se parseen idénticamente.
    ''' ⚠️ TINV NO se acota A PROPÓSITO. En spec vale 0-100 ⇒ tvv ∈ [0,1] y las dos réplicas coinciden. Fuera de
    ''' spec divergen: el GPU acota la cobertura (uOpacity, Math.Max/Min en FaceTintCompositor) y el CPU no. NO se
    ''' unifica: no está RE-ado qué hace el motor con TINV > 100 (la RE sólo confirma `interp = value × 0.01`, sin
    ''' clamp), y este valor alimenta el BAKE (ComposeFacetintAcc → BakeFaceTintDds), validado byte-exacto contra
    ''' el CK ⇒ un clamp inventado sería regresión, no fix. El RESULTADO sí queda acotado aguas abajo en ambos
    ''' caminos. Se loguea para tener un NPC concreto contra el que hacer RE si alguna vez aparece.</summary>
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
                .R = ClampByteLocal(cr * 255.0), .G = ClampByteLocal(cg * 255.0), .B = ClampByteLocal(cbb * 255.0),
                .Opacity = CSng(iv), .BlendOp = 0, .Slot = 0US, .IsTextureSet = False,
                .PaletteMaskChannel = maskCh,
                .DebugName = $"sse-tint idx={layer.Index}"})
        Next
        Return outp
    End Function

    ''' <summary>Clamp a double a byte [0,255] con redondeo. Local (SseFaceTintComposer no comparte el de FaceGenBuilder).</summary>
    Private Function ClampByteLocal(v As Double) As Byte
        If v < 0.0 Then Return 0
        If v > 255.0 Then Return 255
        Return CByte(Math.Round(v))
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
    ''' byte-exact (Afflicted TINC=(0.263,0.016,0.004)@0.52 → QNAM (96,63,61)). SSE-only; FO4 never calls this.</summary>
    Public Function ResolveSkinToneQnam(pm As PluginManager, npc As NPC_Data, race As RACE_Data,
                                        raceFid As UInteger, isFemale As Boolean) As Nullable(Of System.Drawing.Color)
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

        ' Fold intensity into the colour: q = lerp(0.5, TINC, TINV) per channel. QNAM.A = 255 (no SSE alpha).
        Dim rB = FoldSkinChannel(cr, iv)
        Dim gB = FoldSkinChannel(cg, iv)
        Dim bB = FoldSkinChannel(cbb, iv)
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

    ''' <summary>Fold the skin-tone intensity into one colour channel: q = clamp(0.5 + tinv*(cNorm - 0.5), 0, 1),
    ''' returned as a 0..255 byte (round). cNorm is TINC/255 (0..1), tinv is the layer interp (0..1).</summary>
    Private Function FoldSkinChannel(cNorm As Double, tinv As Double) As Integer
        Dim q = 0.5 + tinv * (cNorm - 0.5)
        If q < 0.0 Then q = 0.0
        If q > 1.0 Then q = 1.0
        Return CInt(Math.Round(q * 255.0))
    End Function

    ''' <summary>Resolve a CLFM (Color Form) formID to linear RGB [0,1] from its CNAM (byte RGBA). Cached.
    ''' Returns white when the formID is 0 or unresolved.</summary>
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

    ''' <summary>One tint layer onto the accumulator, vía el compositor COMPARTIDO (FaceTintCpuCompositor.
    ''' ComposePixel) con la convención <paramref name="conv"/> de la ley SSE. coverage = convMask(mask[ch],
    ''' maskConv) × TINV, y el composite lo hace la ley (default SSE = lerp uniforme en linear, byte-idéntico al
    ''' modelo previo). El canal de máscara y la mask-conv salen de la ley — sin ramas por tipo hardcodeadas.</summary>
    Private Sub ComposeLayer(acc As Single(), mask As Single(), cR As Double, cG As Double, cB As Double, tinv As Double, npix As Integer,
                             conv As FaceTintConvention.FaceTintConventionSet, maskConv As Integer, maskCh As Integer,
                             Optional cov As Single() = Nothing)
        ' PARALELO por rangos: cada píxel toca sólo sus índices (acc/cov por i) ⇒ bit-idéntico al serial. El fold
        ' SSE compone a la resolución NATIVA del complexion (4096² con COtR), donde el serial era parte de los
        ' segundos por fold. El orden ENTRE capas (no conmutativo) lo preserva el caller (loop de capas serial).
        System.Threading.Tasks.Parallel.ForEach(
            System.Collections.Concurrent.Partitioner.Create(0, npix),
            Sub(range)
                For i = range.Item1 To range.Item2 - 1
                    Dim a = FaceTintCpuCompositor.ConvMaskShared(mask(i * 4 + maskCh), maskConv) * tinv   ' cobertura por la ley
                    If a <= 0.0 Then Continue For
                    acc(i * 4) = CSng(FaceTintCpuCompositor.ComposePixel(acc(i * 4), cR, a, conv))
                    acc(i * 4 + 1) = CSng(FaceTintCpuCompositor.ComposePixel(acc(i * 4 + 1), cG, a, conv))
                    acc(i * 4 + 2) = CSng(FaceTintCpuCompositor.ComposePixel(acc(i * 4 + 2), cB, a, conv))
                    If cov IsNot Nothing Then cov(i) = CSng(cov(i) + a * (1 - cov(i)))   ' accumulate coverage
                Next
            End Sub)
    End Sub

    ''' <summary>Parse the RACE's per-gender tint layers IN ORDER (Male/Female Head Data, tracked by
    ''' MNAM/FNAM). Each Tint Layer: TINI index, TINT mask path, TINP mask type (optional), TIND default CLFM.
    ''' Returns the ordered list (= the cb2 slot order the engine composes). Cached per race+gender. TINP/TIND
    ''' are OPTIONAL (tattoo masks omit TINP) → flush on each new TINI so no-TINP masks still register.</summary>
    ''' <summary>Aplica el orden configurable SSE (<c>Setting_FaceTintSort_SSE.TintRules</c>, claves
    ''' <see cref="FaceTintSseTintSortKey"/>) sobre las capas del RACE que devuelve <see cref="GetRaceLayersOrdered"/>.
    ''' DEFAULT = <c>[Race_Order asc]</c> = IDENTIDAD ⇒ orden RaceMenu (posición en el RACE) ⇒ compose byte-idéntico.
    ''' El sort tiene tiebreak final por posición original (orden RACE), así claves iguales preservan RaceMenu. El
    ''' lerp NO es conmutativo ⇒ cualquier regla != default DESVÍA de RaceMenu (elección explícita del usuario).</summary>
    Public Function SortSseTintLayers(layers As List(Of SseTintMask), npcMap As Dictionary(Of Integer, Double())) As List(Of SseTintMask)
        If layers Is Nothing OrElse layers.Count <= 1 Then Return layers
        Dim cfg = Config_App.Current?.Setting_FaceTintSort_SSE
        Dim rules = If(cfg IsNot Nothing, cfg.TintRules, Nothing)
        If rules Is Nothing OrElse rules.Count = 0 Then Return layers
        Dim items As New List(Of (Layer As SseTintMask, Pos As Integer))
        For i = 0 To layers.Count - 1 : items.Add((layers(i), i)) : Next
        items.Sort(Function(a, b)
                       For Each r In rules
                           Dim c = SseTintKey(a.Layer, a.Pos, npcMap, r.Key).CompareTo(SseTintKey(b.Layer, b.Pos, npcMap, r.Key))
                           If r.Descending Then c = -c
                           If c <> 0 Then Return c
                       Next
                       Return a.Pos.CompareTo(b.Pos)   ' tiebreak estable = orden RACE (RaceMenu)
                   End Function)
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
                    ' ⛔ REMAPEO DE MASTERS OBLIGATORIO. TIND y el TINC de la RACE son FormIDs, y el FormID
                    ' guardado en el archivo usa el indice LOCAL de masters del plugin que lo escribio. Leerlo
                    ' con BitConverter a secas devuelve un FormID de OTRO plugin.
                    ' MEDIDO (2026-07-21): RACE 0x06000817 (Mazken, ccBGSSSE025-AdvDSGS.esm) capa idx=24
                    ' (SkinTone, cobertura 1,000) tiene TIND local 0x050010DD = AUTO-REFERENCIA (indice local
                    ' == cantidad de masters ⇒ el plugin se apunta a si mismo). Sin remapear buscabamos
                    ' 0x050010DD (otro plugin) ⇒ no existe ⇒ ResolveClfmColor degradaba a BLANCO ⇒ la unica
                    ' capa con cobertura completa pintaba la cara entera de blanco. El correcto es 0x060010DD
                    ' = CLFM con CNAM=(75,55,64), y el CK hornea (74,57,66). 5 NPCs vanilla afectados
                    ' (Aureales/Mazken), con 99,3% de los pixeles en 255 contra el tono oscuro del CK.
                    ' ⛔ Ojo con la ambiguedad de TINC: a nivel RACE es un FormID de CLFM (esto), a nivel NPC
                    ' son 3 bytes RGB (BuildNpcAuthoredTintMap). Los del NPC NO se remapean: no son FormIDs.
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

    ''' <summary>⭐ LA normalización de una ruta de textura a clave del FilesDictionary, para TODO el SSE.
    ''' Delega en <see cref="FO4UnifiedMaterial_Class.CorrectTexturePath"/>, que es la MISMA que ya usaba el
    ''' camino GPU (<c>SseFoldLayerStack.BuildFaceOverlayGpuLayers</c> / <c>BuildSkeeGpuLayers</c>).
    '''
    ''' <para>⛔ POR QUÉ EXISTE: acá había una normalización propia — <c>Replace("/","\").ToLowerInvariant()</c>
    ''' + anteponer <c>textures\</c> si faltaba — mientras el GPU usaba <c>CorrectTexturePath</c>. Dos leyes
    ''' distintas para el MISMO path ⇒ una podía resolver y la otra no, y eso se manifiesta como "el overlay
    ''' aparece en un camino y desaparece en el otro" (rompe la paridad CPU==GPU en el ORIGEN, antes de componer
    ''' un solo píxel). El caso concreto que sólo fallaba en CPU: un path que ya trae el prefijo <c>data\</c> —
    ''' el que <c>FaceGenBuilder.EmbeddedEngineTexPath</c> escribe en el slot 6 — daba
    ''' <c>textures\data\textures\…</c> ⇒ no existe. CorrectTexturePath lo resuelve (busca <c>\textures</c> en
    ''' cualquier posición), y además cubre rutas absolutas y con <c>\</c> inicial.</para></summary>
    Public Function NormalizeTextureKey(texPath As String) As String
        Return FO4UnifiedMaterial_Class.CorrectTexturePath(texPath)
    End Function

    ''' <summary>Decode a texture (FilesDictionary key) to linear RGBA[0,1] at exactly W×H (bilinear). Public
    ''' wrapper over <see cref="DecodeMask"/> so other SSE compositors (overlays into the per-NPC diffuse) reuse
    ''' the SAME decode+resize+cache path. Nothing when the file is missing/undecodable.</summary>
    Public Function DecodeTextureRgba(texPath As String, w As Integer, h As Integer) As Single()
        Return DecodeMask(texPath, w, h)
    End Function

    ''' <summary>⭐ Decode de un NORMAL MAP (FilesDictionary key) a RGBA[0,1] en exactamente W×H — el MISMO
    ''' camino de decode + resize + caché que <see cref="DecodeTextureRgba"/>, y encima la reconstrucción del eje Z
    ''' cuando la fuente trae 2 canales (BC5/R8G8), vía <see cref="FaceTintCpuCompositor.ReconstructNormalZ"/>.
    '''
    ''' <para>⛔ POR QUÉ EXISTE Y POR QUÉ NO ALCANZA CON EL DE COLOR: <c>DecodeDds</c> empaqueta las fuentes de 2
    ''' canales como <c>B=0, A=1</c>. Leído como color eso es inocuo; leído como VECTOR da <c>z = −1</c> — la
    ''' normal del tatuaje apuntando hacia adentro, con el lighting invertido en toda la zona cubierta. Y BC5 es
    ''' justamente el formato estándar de los normales tangent-space de SSE, que es lo que traen los face-paint de
    ''' RaceMenu. La compresión que el usuario elige en CharGen Options es la de SALIDA y no cubre nada de esto:
    ''' el defecto está en la ENTRADA, en una textura que la app no controla.</para>
    '''
    ''' <para>La caché usa un NAMESPACE PROPIO (sufijo <c>|nrm</c>): la misma ruta puede pedirse como color
    ''' (cobertura) y como normal, y servir una por la otra devolvería un buffer con el B equivocado.</para></summary>
    Public Function DecodeNormalRgba(texPath As String, w As Integer, h As Integer) As Single()
        Return DecodeMask(texPath, w, h, asNormalMap:=True)
    End Function

    ''' <summary>Decode a mask texture (FilesDictionary key) to linear RGBA[0,1] at exactly W×H (bilinear).
    ''' Cached at ANY size — la clave lleva el tamaño destino, así que no hay ningún tamaño hardcodeado; lo que
    ''' acota la memoria es la VIDA del caché (per-NPC, ver <see cref="ClearTextureCaches"/>), no un
    ''' presupuesto. Nothing when the file is missing/undecodable.</summary>
    ''' <param name="asNormalMap">True ⇒ la fuente se interpreta como VECTOR: si trae 2 canales se despeja el eje
    ''' Z tras el resample (ver <see cref="DecodeNormalRgba"/>). False (default) = comportamiento previo, sin tocar
    ''' un solo byte de ningún caller existente.</param>
    Private Function DecodeMask(texPath As String, w As Integer, h As Integer,
                                Optional asNormalMap As Boolean = False) As Single()
        Dim key = NormalizeTextureKey(texPath)   ' ⭐ MISMA normalización que el camino GPU (ver NormalizeTextureKey)
        If String.IsNullOrEmpty(key) Then Return Nothing
        ' ⭐⭐ LA CLAVE ES LA IDENTIDAD COMPLETA DEL VALOR: path + TAMAÑO DESTINO + namespace color/normal.
        ' ⛔ ANTES la clave era solo (path[, |nrm]) — le FALTABA el tamaño, aunque el valor devuelto depende de
        ' el. Para no servir un buffer del tamaño equivocado con una clave incompleta, el codigo restringia el
        ' dominio a UN solo tamaño con cuatro guardas `w = 512 AndAlso h = 512`, y 512 se eligio por ser el del
        ' facetint vanilla. O sea: el 512 no era un dato del motor, era un PARCHE de la clave incompleta.
        ' Consecuencias, las dos SILENCIOSAS:
        '   · el camino PLEGADO (que compone a la resolucion nativa del complexion) no podia pegarle al cache
        '     NUNCA, por construccion;
        '   · el camino NO plegado perdia el cache apenas el usuario elegia en CharGen Options cualquier
        '     resolucion que no diera 512 (ResolveResolutionSize devuelve `512 << (n-1)`).
        ' Con el tamaño en la clave las cuatro guardas DESAPARECEN — no se reemplazan por otro umbral — y el
        ' cache funciona a cualquier resolucion. Es el esquema que la caché de fuentes de esta misma funcion
        ' (`_texSrcCache`, ya eliminada) usaba desde el principio; lo unico que se hizo fue dejar de tener dos
        ' esquemas de clave distintos conviviendo.
        Dim ckey = $"{key}|{w}x{h}" & If(asNormalMap, "|nrm", "")
        Dim cached As Single() = Nothing
        ' Incluye el NEGATIVO (entrada Nothing = archivo ausente/indecodificable a este tamaño): cuesta 0 bytes
        ' y evita re-pedirle el archivo al FilesDictionary en cada capa de cada cara.
        If _texCache.TryGetValue(ckey, cached) Then Return cached
        ' ⛔ SACADO: `_texSrcCache`, la caché del DECODE de la fuente por (path, target). Existía porque
        ' `_texCache` sólo retenía a 512², así que a cualquier otro tamaño era la ÚNICA caché. Con `_texCache`
        ' funcionando a todo tamaño quedó INALCANZABLE como hit: las dos se poblaban y se limpiaban juntas y no
        ' hay evicción, así que un miss de `_texCache` implica siempre un miss del source. Es el mismo argumento
        ' que el código ya hacía para el caso 512² (`isRedundantAt512`, medido: 0 hits/156 misses, 864 MB de
        ' duplicación exacta) — ahora vale para todos los tamaños. Era código muerto, no una capa de respaldo.
        Dim b = FilesDictionary_class.GetBytes(key)
        Dim t As FaceTintCpuCompositor.DecodedTex = If(b Is Nothing, Nothing, FaceTintCpuCompositor.DecodeDds(b, w, h))
        If t IsNot Nothing AndAlso t.Rgba8 Is Nothing Then t = Nothing
        If t Is Nothing Then
            ' NEGATIVO: no vuelve a pedirle el archivo al FilesDictionary ni a intentar el decode para esta
            ' terna. Cuesta 0 bytes.
            _texCache(ckey) = Nothing
            Return Nothing
        End If
        Dim needsZ As Boolean = asNormalMap AndAlso t.Channels < 3
        Dim outp(w * h * 4 - 1) As Single
        ' ⭐ IDENTIDAD: si la fuente ya está en el tamaño pedido, el bilineal de abajo devuelve exactamente el
        ' texel de origen y sólo cuesta una pasada completa de trabajo. Con sw=dw: fx = (x+0,5)·W/W − 0,5 = x
        ' EXACTO en Double (el producto usa ≤24 bits de mantisa y la división por el mismo entero es exacta),
        ' así que x0=x, tx=0 y la fórmula colapsa a (p00·1 + p10·0)·1 + (…)·0 = p00. Ídem en y. Es la misma
        ' salida bit a bit, sin la pasada. Sus dos gemelos del compositor FO4 (ResampleBgra /
        ' ResampleRgbaFloat) ya traían este corto; éste no lo tenía, y 512²→512² es el caso NORMAL en SSE.
        ' ⛔ Se COPIA (expande), no se aliasea t.Rgba8: `outp` es Single en unidad [0,1] y `t.Rgba8` es Byte
        ' crudo, o sea que ni siquiera son el mismo tipo — pero además el array devuelto termina en _texCache y
        ' en manos de varios consumidores, así que tiene que ser suyo. (Contrato idéntico al de siempre: el
        ' bilineal también devolvía un array fresco.)
        If t.Width = w AndAlso t.Height = h AndAlso t.Rgba8.Length = outp.Length Then
            ' ⛔ NO Array.Copy: la fuente es Byte() crudo (0..255) y el destino es la unidad [0,1]. Array.Copy
            ' haría la conversión WIDENING (255 → 255,0F) en vez de la de escala, así que la expansión pasa
            ' explícita por ByteToUnit — que devuelve el mismo Single que guardaba el storage viejo.
            Dim srcArr = t.Rgba8
            Dim lut = FaceTintCpuCompositor.ByteToUnit
            For i As Integer = 0 To outp.Length - 1
                outp(i) = lut(srcArr(i))
            Next
            If needsZ Then FaceTintCpuCompositor.ReconstructNormalZ(outp, w * h)
            _texCache(ckey) = outp
            Return outp
        End If
        ' Resample bilineal PARALELO por filas (misma fórmula, cada fila escribe sólo sus índices ⇒ bit-idéntico).
        ' A 4096² de target el serial era parte de los segundos por fold.
        System.Threading.Tasks.Parallel.For(0, h, Sub(y)
                                                      Dim fy = (y + 0.5) * t.Height / h - 0.5
                                                      Dim y0 = Math.Max(0, Math.Min(t.Height - 1, CInt(Math.Floor(fy)))) : Dim y1 = Math.Min(t.Height - 1, y0 + 1) : Dim ty = fy - Math.Floor(fy)
                                                      For x = 0 To w - 1
                                                          Dim fx = (x + 0.5) * t.Width / w - 0.5
                                                          Dim x0 = Math.Max(0, Math.Min(t.Width - 1, CInt(Math.Floor(fx)))) : Dim x1 = Math.Min(t.Width - 1, x0 + 1) : Dim tx = fx - Math.Floor(fx)
                                                          For c = 0 To 3
                                                              Dim p00 = t.Unit((y0 * t.Width + x0) * 4 + c), p10 = t.Unit((y0 * t.Width + x1) * 4 + c)
                                                              Dim p01 = t.Unit((y1 * t.Width + x0) * 4 + c), p11 = t.Unit((y1 * t.Width + x1) * 4 + c)
                                                              outp((y * w + x) * 4 + c) = CSng((p00 * (1 - tx) + p10 * tx) * (1 - ty) + (p01 * (1 - tx) + p11 * tx) * ty)
                                                          Next
                                                      Next
                                                  End Sub)
        ' DESPUÉS del resample, igual que el hardware (se samplea el BC5 filtrado y recién ahí se despeja z).
        If needsZ Then FaceTintCpuCompositor.ReconstructNormalZ(outp, w * h)
        _texCache(ckey) = outp
        Return outp
    End Function

End Module
