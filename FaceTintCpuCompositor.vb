Imports FO4_Base_Library.FaceTintConvention

' ============================================================================
' FaceTintCpuCompositor — espejo CPU EXACTO del compositor GL (FaceTintCompositor).
'
' ============================ CONTRATO DE SYNC (LEER) ========================
' Hay DOS implementaciones de la MISMA ley de composición FaceTint:
'   1. GL  : FaceTintCompositor (shader FragmentShaderSource + ApplyFaceTintPipeline). DEFAULT.
'   2. CPU : ESTE módulo. Referencia byte de gen3 (Tools/FaceTintDerive), float64.
' AMBOS deben producir el MISMO resultado por canal (igual que el skinning GPU/CPU). Cualquier cambio
' en la ley (espacios, blend, coverage, mask/src por kind, region-swap, seed) DEBE reflejarse en LOS
' DOS. La ley NO se hardcodea acá: sale de FaceTintConvention.ResolveConvention (compositor AGNÓSTICO),
' idéntico a como el shader la lee por uniforms. Las funciones de espacio/blend/maskconv de abajo son
' la transcripción 1:1 de las del shader (cvt / convMaskFull / blendDispatch). Si tocás una, tocá la otra.
'
' PRECISIÓN / PARIDAD GL vs CPU (caveat, leer):
'  - El CPU corre en float64 con el mismo pow() que np.power -> CPU == gen3 (`_3`) BYTE-EXACTO. Es la
'    referencia. El bake GPU (default) corre en float32 (FBO Rgba32f) -> puede diferir +-1 byte en píxeles
'    cuyo valor cae cerca de x.5 (redondeo). Es inherente al GPU (no es bug); no se puede bit-matchear
'    float32 con float64. Para output EXACTO a gen3, usar el path CPU.
'  - GL == CPU es exacto SOLO en resolución Inherit (nativo, sin resize ni mip). En resoluciones override
'    (enum != Inherit) cada path resamplea distinto (GL: bilineal/decode-BC del GPU ; CPU: mip-stored o
'    bilineal/decode-DirectXTex) -> NO son byte-idénticos entre sí; ambos son aproximaciones de CALIDAD.
'    El byte-test (vs gen3) se corre en Inherit.
'
' Trabaja sobre las DDS YA LEÍDAS (mismos FaceTintLayerInput/FaceRegionSwapInput que el GL): decodifica
' cada DDS por CPU/DirectXTex (wrapper, useCompress:=False — igual que WritePristineTga), cachea por
' cache-key para reusar, y compone en float. El producto es BGRA byte por canal (D en sRGB = storage de
' build_3 / formato de CK en disco, N/S lineales raw), listo para el encode DDS del bake.
' ============================================================================

Public Module FaceTintCpuCompositor

    ' ---- Conversiones de espacio (transcripción 1:1 del shader; ws: 0=linear 1=srgb 2=g22) ----
    Private Function Clamp01(c As Double) As Double
        If c < 0.0 Then Return 0.0
        If c > 1.0 Then Return 1.0
        Return c
    End Function

    Private Function SrgbToLin1(c As Double) As Double
        c = Clamp01(c)
        Return If(c <= 0.04045, c / 12.92, Math.Pow((c + 0.055) / 1.055, 2.4))
    End Function

    Private Function LinToSrgb1(c As Double) As Double
        c = Clamp01(c)
        Return If(c <= 0.0031308, c * 12.92, 1.055 * Math.Pow(c, 1.0 / 2.4) - 0.055)
    End Function

    Private Function G22ToLin1(c As Double) As Double
        Return Math.Pow(Clamp01(c), 2.2)
    End Function

    Private Function LinToG221(c As Double) As Double
        Return Math.Pow(Clamp01(c), 1.0 / 2.2)
    End Function

    Private Function SpaceToLin1(c As Double, s As Integer) As Double
        If s = 0 Then Return c
        If s = 1 Then Return SrgbToLin1(c)
        Return G22ToLin1(c)
    End Function

    Private Function LinToSpace1(c As Double, s As Integer) As Double
        If s = 0 Then Return c
        If s = 1 Then Return LinToSrgb1(c)
        Return LinToG221(c)
    End Function

    ''' <summary>cvt agnóstico entre espacios (0=linear 1=srgb 2=g22) via linear. = shader cvt().</summary>
    Private Function Cvt1(c As Double, fromS As Integer, toS As Integer) As Double
        If fromS = toS Then Return c
        Return LinToSpace1(SpaceToLin1(c, fromS), toS)
    End Function

    ''' <summary>mask conv (0=raw 1=srgbEnc 2=srgbDec 3=g22Enc 4=g22Dec). = shader convMaskFull().</summary>
    Private Function ConvMask1(m As Double, mc As Integer) As Double
        Select Case mc
            Case 1 : Return LinToSrgb1(m)
            Case 2 : Return SrgbToLin1(m)
            Case 3 : Return LinToG221(m)
            Case 4 : Return G22ToLin1(m)
            Case Else : Return m
        End Select
    End Function

    ' ---- Blend ops (transcripción 1:1 del shader blendDispatch; uBlendOp 0..4) ----
    Private Function BlendOverlay1(d As Double, s As Double) As Double
        ' GLSL step(0.5,d): d>=0.5 -> 1-2(1-d)(1-s) ; d<0.5 -> 2ds
        If d >= 0.5 Then Return 1.0 - 2.0 * (1.0 - d) * (1.0 - s)
        Return 2.0 * d * s
    End Function

    Private Function BlendSoftLight1(d As Double, s As Double) As Double
        ' W3C soft-light (BlendOp 3). d=base, s=src. Re-derivado vs CK 2026-05-31 (formula_test sobre clones
        ' op-sweep: skintone W3C 0.67 vs Illusions 1.07; TS-bop3 W3C gana; el "Illusions ganaba" era artefacto
        ' del set contaminado por BASEIN roto). = shader blendSoftLight (paridad CPU/GL).
        Dim g As Double = If(d >= 0.25, Math.Sqrt(Clamp01(d)), ((16.0 * d - 12.0) * d + 4.0) * d)
        If s >= 0.5 Then
            Return d + (2.0 * s - 1.0) * (g - d)
        Else
            Return d - (1.0 - 2.0 * s) * d * (1.0 - d)
        End If
    End Function

    ''' <summary>Dispatch de blend por canal escalar. blendOp: 0=replace 1=mult 2=overlay 3=softlight
    ''' 4=hardlight. = shader blendDispatch().</summary>
    Private Function BlendDispatch1(blendOp As Integer, d As Double, s As Double) As Double
        Select Case blendOp
            Case 1 : Return d * s                       ' multiply
            Case 2 : Return BlendOverlay1(d, s)         ' overlay
            Case 3 : Return BlendSoftLight1(d, s)       ' softlight (W3C)
            Case 4 : Return BlendOverlay1(s, d)         ' hardlight = overlay(s,d)
            Case Else : Return s                        ' replace (default)
        End Select
    End Function

    ' ---- Decode DDS -> RGBA float [0,1] (mirror de FaceTintCompositor.WritePristineTga) ----
    Public Class DecodedTex
        Public Width As Integer
        Public Height As Integer
        Public Rgba As Double()   ' length W*H*4, orden R,G,B,A en [0,1]
    End Class

    ''' <summary>Decodifica un DDS (BCn -> uncompressed) por CPU/DirectXTex (useCompress:=False) a RGBA
    ''' float [0,1]. 4-canales (BC1/3/7 -> RGBA/BGRA), 2-canales (BC5 -> R8G8, B=0 A=1), 1-canal (BC4 ->
    ''' gray). Nothing si falla o formato no soportado. MISMA tabla de formatos que WritePristineTga.
    ''' <paramref name="preferW"/>/<paramref name="preferH"/>: si &gt;0 y el DDS trae un MIP STORED a ese
    ''' tamaño, se usa ESE mip (mejor camino = filtro propio de Bethesda, matchea a CK donde usó ese mip);
    ''' si no existe, cae al mip0 (el caller hace resize bilineal). regla "mip-stored-sino-resize".</summary>
    Public Function DecodeDds(ddsBytes As Byte(), Optional preferW As Integer = 0, Optional preferH As Integer = 0) As DecodedTex
        If ddsBytes Is Nothing OrElse ddsBytes.Length = 0 Then Return Nothing
        Try
            Dim loaded = DirectXTexWrapperCLI.Loader.LoadTextures(New Byte()() {ddsBytes}, useCompress:=False, forceOpenGL:=False)
            If loaded Is Nothing OrElse loaded.Count = 0 OrElse loaded(0) Is Nothing OrElse Not loaded(0).Loaded Then Return Nothing
            Dim tex = loaded(0)
            If tex.Levels Is Nothing OrElse tex.Levels.Count = 0 OrElse tex.Levels(0) Is Nothing Then Return Nothing
            ' Selección de mip para el target (mips ordenados largest->smallest, level 0 = nativo):
            '   1) EXACTO: hay un mip a ESE tamaño -> usarlo (mejor camino, filtro de Bethesda).
            '   2) DOWNSIZE: no exacto pero target < nativo -> usar el mip más cercano-MAYOR (el más chico
            '      con W>=target y H>=target). Downsamplear desde ahí (paso chico) aliasa menos que un único
            '      bilineal grande desde el mip0.
            '   3) UPSIZE (target > nativo) o sin mips: no hay mip >= target -> usar el mip0 (el más grande).
            ' El caller (SampleChannelAt) hace el resize bilineal desde el mip elegido.
            Dim lvlIdx As Integer = 0
            If preferW > 0 AndAlso preferH > 0 AndAlso tex.Levels.Count > 1 Then
                Dim exactIdx As Integer = -1
                Dim geIdx As Integer = -1   ' mip más cercano-mayor (>= target); como i sube y el size baja,
                For li As Integer = 0 To tex.Levels.Count - 1   ' el último que cumpla >=target es el más chico >=target
                    Dim cand = tex.Levels(li)
                    If cand Is Nothing Then Continue For
                    If cand.Width = preferW AndAlso cand.Height = preferH Then exactIdx = li : Exit For
                    If cand.Width >= preferW AndAlso cand.Height >= preferH Then geIdx = li
                Next
                If exactIdx >= 0 Then
                    lvlIdx = exactIdx
                ElseIf geIdx >= 0 Then
                    lvlIdx = geIdx
                Else
                    lvlIdx = 0   ' upsize: ningún mip >= target -> el más grande (mip0)
                End If
            End If
            Dim lvl = tex.Levels(lvlIdx)
            Dim w = lvl.Width, h = lvl.Height
            Dim px = lvl.Data
            Dim fmt = tex.DxgiCodeFinal
            Dim bpp As Integer = 0
            Select Case fmt
                Case 28, 29, 87, 88, 91, 93 : bpp = 4
                Case 49, 50 : bpp = 2
                Case 61, 62 : bpp = 1
            End Select
            If w <= 0 OrElse h <= 0 OrElse px Is Nothing OrElse bpp = 0 OrElse px.Length < w * h * bpp Then Return Nothing
            Dim isBgra8 = (fmt = 87 OrElse fmt = 88 OrElse fmt = 91 OrElse fmt = 93)
            Dim outArr(w * h * 4 - 1) As Double
            For i As Integer = 0 To w * h - 1
                Dim o As Integer = i * 4, s As Integer = i * bpp
                Dim r As Double, g As Double, b As Double, a As Double
                Select Case bpp
                    Case 4
                        If isBgra8 Then
                            b = px(s) : g = px(s + 1) : r = px(s + 2) : a = px(s + 3)
                        Else
                            r = px(s) : g = px(s + 1) : b = px(s + 2) : a = px(s + 3)
                        End If
                    Case 2
                        r = px(s) : g = px(s + 1) : b = 0 : a = 255
                    Case Else ' 1
                        r = px(s) : g = px(s) : b = px(s) : a = 255
                End Select
                outArr(o) = r / 255.0 : outArr(o + 1) = g / 255.0 : outArr(o + 2) = b / 255.0 : outArr(o + 3) = a / 255.0
            Next
            Return New DecodedTex With {.Width = w, .Height = h, .Rgba = outArr}
        Catch
            Return Nothing
        End Try
    End Function

    ''' <summary>Sample bilineal de un canal (0=R 1=G 2=B 3=A) en coord normalizada (u,v) [0,1], clamp
    ''' a borde. Para la hair-LUT (= GL_LINEAR del sampler uHairLut). Mismo filtro que el shader.</summary>
    Private Function SampleBilinear(t As DecodedTex, u As Double, v As Double, ch As Integer) As Double
        Dim w = t.Width, h = t.Height
        ' Convencion GL_LINEAR + CLAMP_TO_EDGE (= el sampler del shader, single source of truth): el texel es
        ' uv*size - 0.5 (offset de medio texel), se lerpea entre floor(texel) y floor(texel)+1, ambos
        ' clampeados a [0,size-1]. (Antes era uv*(size-1) "fit-endpoints", que NO matchea el sampler GL ->
        ' el resampling GPU/CPU divergia en canales a OTRA resolucion que el acumulador, p.ej. S: acumulador
        ' 512 con capas/swaps 1024. D/N no entran aca: SampleChannelAt usa indice directo si los tamanos
        ' coinciden. Tambien alinea el sample de la hair-LUT del brow.)
        Dim fx = Clamp01(u) * w - 0.5
        Dim fy = Clamp01(v) * h - 0.5
        Dim ix = CInt(Math.Floor(fx)), iy = CInt(Math.Floor(fy))
        Dim tx = fx - ix, ty = fy - iy
        Dim x0 = Math.Max(0, Math.Min(w - 1, ix)), x1 = Math.Max(0, Math.Min(w - 1, ix + 1))
        Dim y0 = Math.Max(0, Math.Min(h - 1, iy)), y1 = Math.Max(0, Math.Min(h - 1, iy + 1))
        Dim c00 = t.Rgba((y0 * w + x0) * 4 + ch)
        Dim c10 = t.Rgba((y0 * w + x1) * 4 + ch)
        Dim c01 = t.Rgba((y1 * w + x0) * 4 + ch)
        Dim c11 = t.Rgba((y1 * w + x1) * 4 + ch)
        Return c00 * (1 - tx) * (1 - ty) + c10 * tx * (1 - ty) + c01 * (1 - tx) * ty + c11 * tx * ty
    End Function

    ' ---- Resultado de la pipeline CPU (espejo de FaceTintPipelineResult) ----
    Public Class CpuChannelResult
        Public Width As Integer
        Public Height As Integer
        ''' <summary>BGRA byte, listo para el encode DDS del bake (D en g22, N/S lineales). Nothing si
        ''' el canal no tiene source.</summary>
        Public Bgra As Byte()
    End Class

    Public Class CpuPipelineResult
        Public Diffuse As CpuChannelResult
        Public Normal As CpuChannelResult
        Public Specular As CpuChannelResult
    End Class

    ''' <summary>Cache de decode PERSISTENTE entre bakes — para el BATCH. Cuando esta activo (Begin/End
    ''' alrededor del loop de clones), ComposeCpuPipeline lo usa en vez del dict per-call: las texturas
    ''' source (face d/_n/_s) + tint + swap se REPITEN entre clones, asi que cada DDS se decodifica UNA
    ''' sola vez en todo el batch (el path GPU ya hacia esto via TintGpuCache). Sin esto, cada clon
    ''' re-decodifica las ~49 texturas via DirectXTex. Nothing = comportamiento per-cara (1 bake aislado).
    ''' OJO: los bakes del batch son SECUENCIALES (un await a la vez) -> Dictionary plano alcanza; si se
    ''' paraleliza el loop de clones, cambiar a ConcurrentDictionary.</summary>
    Public Property BatchDecodeCache As Dictionary(Of String, DecodedTex)

    ''' <summary>Arranca el cache de decode batch (llamar ANTES del loop de clones).</summary>
    Public Sub BeginBatchDecodeCache()
        BatchDecodeCache = New Dictionary(Of String, DecodedTex)(StringComparer.OrdinalIgnoreCase)
    End Sub

    ''' <summary>Cierra y libera el cache de decode batch (llamar en Finally despues del loop). Los
    ''' DecodedTex son managed (Double() Rgba, sin recursos nativos) -> Clear + GC alcanza.</summary>
    Public Sub EndBatchDecodeCache()
        Dim c = BatchDecodeCache
        BatchDecodeCache = Nothing
        If c IsNot Nothing Then c.Clear()
    End Sub

    ''' <summary>Compone los 3 canales por CPU (espejo de FaceTintCompositor.ApplyFaceTintPipeline).
    ''' Trabaja sobre las DDS YA LEÍDAS de los inputs. Devuelve BGRA byte por canal (D g22 / N/S lineal).
    ''' MISMA ley que el GL (resolver + math de arriba). Sin GL: pura CPU.</summary>
    ''' <param name="resolution">Resolución por canal (A/B/C). Nothing/default = Inherit (nativo) en los 3
    ''' = comportamiento gen3. Bodyparts: pasar Nothing (fuerzan heredar; el enum es solo para la cara).</param>
    ''' <param name="diffuseKey">Keys de las texturas source (path estable) para cachear su decode entre
    ''' clones cuando BatchDecodeCache esta activo. Nothing = no cachear el source (se decodifica directo).</param>
    Public Function ComposeCpuPipeline(diffuseBytes As Byte(), normalBytes As Byte(), specBytes As Byte(),
                                       layers As IList(Of FaceTintLayerInput),
                                       swaps As IList(Of FaceRegionSwapInput),
                                       Optional resolution As FaceTintResolutionSettings = Nothing,
                                       Optional diffuseKey As String = Nothing,
                                       Optional normalKey As String = Nothing,
                                       Optional specKey As String = Nothing) As CpuPipelineResult
        Dim res As New CpuPipelineResult()
        ' BatchDecodeCache (si activo) reusa decodes entre clones; si no, dict per-call (1 cara).
        Dim cache = If(BatchDecodeCache, New Dictionary(Of String, DecodedTex)(StringComparer.OrdinalIgnoreCase))
        res.Diffuse = ComposeChannelCpu(diffuseBytes, FaceTintChannel.Diffuse, layers, swaps, cache, resolution, diffuseKey)
        res.Normal = ComposeChannelCpu(normalBytes, FaceTintChannel.Normal, layers, swaps, cache, resolution, normalKey)
        res.Specular = ComposeChannelCpu(specBytes, FaceTintChannel.Specular, layers, swaps, cache, resolution, specKey)
        Return res
    End Function

    ''' <summary>Decode cacheado. preferW/H>0 -> usa el MIP de ese tamaño (key suffix @WxH para no chocar
    ''' con el mip0 de la misma textura).</summary>
    Private Function CachedDecode(cache As Dictionary(Of String, DecodedTex), key As String, bytes As Byte(),
                                  Optional preferW As Integer = 0, Optional preferH As Integer = 0) As DecodedTex
        If bytes Is Nothing OrElse bytes.Length = 0 Then Return Nothing
        Dim ck = If(preferW > 0 OrElse preferH > 0, $"{key}@{preferW}x{preferH}", key)
        Dim t As DecodedTex = Nothing
        If Not String.IsNullOrEmpty(key) AndAlso cache.TryGetValue(ck, t) Then Return t
        t = DecodeDds(bytes, preferW, preferH)
        If Not String.IsNullOrEmpty(key) AndAlso t IsNot Nothing Then cache(ck) = t
        Return t
    End Function

    ''' <summary>Compone UN canal. seed (D=g22(src), N/S=src) -> region swaps (crossfade en linear) ->
    ''' tint layers (over-running, ley del resolver). Espejo de ComposeOntoFaceTexture + el seed +
    ''' ApplyRegionSwapsOntoFaceTexture del GL.</summary>
    Private Function ComposeChannelCpu(srcBytes As Byte(), channel As FaceTintChannel,
                                       layers As IList(Of FaceTintLayerInput),
                                       swaps As IList(Of FaceRegionSwapInput),
                                       cache As Dictionary(Of String, DecodedTex),
                                       resolution As FaceTintResolutionSettings,
                                       Optional srcKey As String = Nothing) As CpuChannelResult
        ' Source cacheado por key (face set) si se paso srcKey + hay cache batch; si no, decode directo.
        Dim src = If(String.IsNullOrEmpty(srcKey), DecodeDds(srcBytes), CachedDecode(cache, srcKey, srcBytes))
        If src Is Nothing Then Return Nothing
        Dim isD = (channel = FaceTintChannel.Diffuse)
        ' Tamaño del ACUMULADOR: Inherit (default) = nativo del source (preserva no-cuadrado, sin
        ' downgrade). Enum explícito = cuadrado del target. Regla mip-stored-sino-resize: HOY se resize
        ' el mip0 via SampleChannelAt bilineal; usar el MIP STORED del source a ese tamaño es refinamiento
        ' de calidad (TODO). Bodyparts: el caller pasa Nothing -> Inherit (el enum es solo cara).
        Dim res = If(resolution IsNot Nothing, resolution.ForChannel(channel), FaceTintChannelResolution.Inherit)
        Dim w As Integer, h As Integer
        If res = FaceTintChannelResolution.Inherit Then
            w = src.Width : h = src.Height
        Else
            Dim target = ResolveResolutionSize(res, Math.Min(src.Width, src.Height))
            w = target : h = target
        End If
        ' mip-stored como seed: si el target difiere del nativo, re-decode prefiriendo el MIP STORED a ese
        ' tamaño (mejor camino = filtro de Bethesda); si no existe, queda el mip0 y SampleChannelAt resizea.
        If w <> src.Width OrElse h <> src.Height Then
            Dim mipSrc = If(String.IsNullOrEmpty(srcKey), DecodeDds(srcBytes, w, h), CachedDecode(cache, srcKey, srcBytes, w, h))
            If mipSrc IsNot Nothing Then src = mipSrc
        End If
        Dim n = w * h
        ' Acumulador RGB en OutputSpace del canal (build_3): D=sRGB (= src directo, SIN g22) ; N/S=raw lineal.
        ' El storage del engine FaceCustomization es sRGB (= formato de CK en disco); no se acumula en g22.
        ' Seed via SampleChannelAt (índice directo si tamaños iguales; bilineal si difieren = resize).
        Dim accR(n - 1) As Double, accG(n - 1) As Double, accB(n - 1) As Double
        ' Seed: D = g22(srgb_to_lin(source)) -> el acumulador D vive en g22 (= storage de CK en disco: op0 =
        ' g22(source), 99.2% vs heatmap). Cvt1(.,Srgb=1,G22=2) = lin_to_g22(srgb_to_lin). N/S = lineal raw.
        ' (El composite trata el acc como OutputSpace=Srgb (label) -> validado en formula_test sobre op0.)
        ' Parallel.For sobre pixeles: cada i escribe su propio accX(i) (indices disjuntos), lee inmutables.
        Dim seedG22 As Boolean = isD
        System.Threading.Tasks.Parallel.For(0, n, Sub(i)
                                                      Dim r0 = SampleChannelAt(src, i, w, h, 0)
                                                      Dim g0 = SampleChannelAt(src, i, w, h, 1)
                                                      Dim b0 = SampleChannelAt(src, i, w, h, 2)
                                                      If seedG22 Then
                                                          accR(i) = Cvt1(r0, 1, 2) : accG(i) = Cvt1(g0, 1, 2) : accB(i) = Cvt1(b0, 1, 2)
                                                      Else
                                                          accR(i) = r0 : accG(i) = g0 : accB(i) = b0
                                                      End If
                                                  End Sub)

        ' --- Region swaps: RUNNING CLOSED-FORM en stored space (= build_3.apply_region_swaps + GL uMode=1).
        '     base = SEED (acc pre-swap, constante); bake@1 = lerp(base, swaptex, mask) ->
        '     n = base + msdv*mask*(swaptex-base) ; cov = g22_encode(mask)*msdv ; acc = n + (1-cov)*(acc-base).
        '     Stored space: D=sRGB, N/S=raw -> swaptex (DDS decodificada) y acc ya estan ahi (sin conversion).
        If swaps IsNot Nothing Then
            Dim baseR(n - 1) As Double, baseG(n - 1) As Double, baseB(n - 1) As Double
            Array.Copy(accR, baseR, n) : Array.Copy(accG, baseG, n) : Array.Copy(accB, baseB, n)
            For Each sw In swaps
                If sw Is Nothing Then Continue For
                Dim swBytes = sw.GetSwapBytes(channel)
                If swBytes Is Nothing OrElse swBytes.Length = 0 Then Continue For
                If sw.RegionMaskDdsBytes Is Nothing OrElse sw.RegionMaskDdsBytes.Length = 0 Then Continue For
                Dim swTex = CachedDecode(cache, sw.GetSwapCacheKey(channel), swBytes)
                Dim mkTex = CachedDecode(cache, sw.RegionMaskCacheKey, sw.RegionMaskDdsBytes)
                If swTex Is Nothing OrElse mkTex Is Nothing Then Continue For
                Dim msdv As Double = Math.Max(0.0, CDbl(sw.Intensity))
                System.Threading.Tasks.Parallel.For(0, n, Sub(i)
                                                              Dim sr = SampleChannelAt(swTex, i, w, h, 0)
                                                              Dim sg = SampleChannelAt(swTex, i, w, h, 1)
                                                              Dim sb = SampleChannelAt(swTex, i, w, h, 2)
                                                              If isD Then       ' swaptex -> g22 (= la base g22 del seed); N/S raw
                                                                  sr = Cvt1(sr, 1, 2) : sg = Cvt1(sg, 1, 2) : sb = Cvt1(sb, 1, 2)
                                                              End If
                                                              Dim mask = SampleChannelAt(mkTex, i, w, h, 0)        ' regionmask .r (raw)
                                                              Dim cov = Clamp01(msdv * LinToG221(mask))            ' g22_encode(mask)*msdv
                                                              Dim mm = msdv * mask
                                                              accR(i) = Clamp01((baseR(i) + mm * (sr - baseR(i))) + (1.0 - cov) * (accR(i) - baseR(i)))
                                                              accG(i) = Clamp01((baseG(i) + mm * (sg - baseG(i))) + (1.0 - cov) * (accG(i) - baseG(i)))
                                                              accB(i) = Clamp01((baseB(i) + mm * (sb - baseB(i))) + (1.0 - cov) * (accB(i) - baseB(i)))
                                                          End Sub)
            Next
        End If

        ' --- Tint layers (over-running). La ley sale del resolver (compositor AGNÓSTICO). ---
        If layers IsNot Nothing Then
            For Each layer In layers
                If layer Is Nothing Then Continue For
                Dim chanBytes = layer.GetChannelBytes(channel)
                If chanBytes Is Nothing OrElse chanBytes.Length = 0 Then Continue For
                Dim layerTex = CachedDecode(cache, layer.GetChannelCacheKey(channel), chanBytes)
                If layerTex Is Nothing Then Continue For

                Dim useHairPalette = (layer.UseHairPalette AndAlso isD AndAlso layer.HairLutDdsBytes IsNot Nothing AndAlso layer.HairLutDdsBytes.Length > 0)
                Dim lutTex As DecodedTex = Nothing
                If useHairPalette Then
                    lutTex = CachedDecode(cache, layer.HairLutCacheKey, layer.HairLutDdsBytes)
                    If lutTex Is Nothing Then useHairPalette = False
                End If
                Dim forceUniform = (layer.ForceUniformColor AndAlso layer.Kind = FaceTintLayerKind.TextureSetDiffuse AndAlso isD AndAlso Not useHairPalette)

                ' Mask diffuse (uLayerDiffuseAlpha) para N/S de TextureSet (alpha del diffuse del layer).
                Dim diffMaskTex As DecodedTex = Nothing
                If layer.Kind = FaceTintLayerKind.TextureSetDiffuse AndAlso Not isD _
                   AndAlso layer.LayerDdsBytes IsNot Nothing AndAlso layer.LayerDdsBytes.Length > 0 Then
                    diffMaskTex = CachedDecode(cache, layer.LayerCacheKey, layer.LayerDdsBytes)
                End If

                Dim conv = FaceTintConvention.ResolveConvention(
                    layer.IsTextureSet, layer.Slot, layer.BlendOp, channel, useHairPalette, forBake:=True)
                Dim ws = CInt(conv.WorkingSpace), cs = CInt(conv.CompositeSpace)
                Dim ss = CInt(conv.SrcSpace), os = CInt(conv.OutputSpace)
                Dim mc = CInt(conv.MaskConv), bop = CInt(conv.Blend)
                Dim op = Math.Max(0.0, Math.Min(1.0, CDbl(layer.Opacity)))
                Dim uColR = layer.R / 255.0, uColG = layer.G / 255.0, uColB = layer.B / 255.0
                Dim row = Math.Max(0.0, Math.Min(1.0, CDbl(layer.HairPaletteRow)))
                Dim kind = layer.Kind

                System.Threading.Tasks.Parallel.For(0, n, Sub(i)
                    Dim lr = SampleChannelAt(layerTex, i, w, h, 0)
                    Dim lg = SampleChannelAt(layerTex, i, w, h, 1)
                    Dim lb = SampleChannelAt(layerTex, i, w, h, 2)
                    Dim la = SampleChannelAt(layerTex, i, w, h, 3)

                    ' mask + src por kind (= rama uLayerKind del shader)
                    Dim maskV As Double
                    Dim srcR As Double, srcG As Double, srcB As Double
                    If kind = FaceTintLayerKind.PaletteMask Then
                        If useHairPalette Then
                            srcR = SampleBilinear(lutTex, lg, row, 0) : srcG = SampleBilinear(lutTex, lg, row, 1) : srcB = SampleBilinear(lutTex, lg, row, 2)
                        Else
                            srcR = uColR : srcG = uColG : srcB = uColB
                        End If
                        maskV = lg
                    Else ' TextureSetDiffuse
                        If useHairPalette Then
                            srcR = SampleBilinear(lutTex, lg, row, 0) : srcG = SampleBilinear(lutTex, lg, row, 1) : srcB = SampleBilinear(lutTex, lg, row, 2)
                        ElseIf forceUniform Then
                            srcR = uColR : srcG = uColG : srcB = uColB
                        Else
                            srcR = lr : srcG = lg : srcB = lb
                        End If
                        If isD Then
                            maskV = la
                        ElseIf diffMaskTex IsNot Nothing Then
                            maskV = SampleChannelAt(diffMaskTex, i, w, h, 3)
                        Else
                            maskV = Math.Max(lr, Math.Max(lg, lb))
                        End If
                    End If

                    Dim cov = Clamp01(ConvMask1(maskV, mc) * op)

                    ' composite agnóstico (= shader): blend en ws, lerp en cs, storage en os.
                    accR(i) = ComposeOne(accR(i), srcR, cov, ws, cs, ss, os, bop)
                    accG(i) = ComposeOne(accG(i), srcG, cov, ws, cs, ss, os, bop)
                    accB(i) = ComposeOne(accB(i), srcB, cov, ws, cs, ss, os, bop)
                End Sub)
            Next
        End If

        ' --- Pack a BGRA byte (clamp+round). D ya está en g22, N/S lineal. Alpha = 255. ---
        Dim outB(n * 4 - 1) As Byte
        For i As Integer = 0 To n - 1
            Dim o = i * 4
            outB(o) = ToByte(accB(i)) : outB(o + 1) = ToByte(accG(i)) : outB(o + 2) = ToByte(accR(i)) : outB(o + 3) = 255
        Next
        Return New CpuChannelResult With {.Width = w, .Height = h, .Bgra = outB}
    End Function

    ''' <summary>composite de UN canal escalar = exactamente el bloque del shader:
    ''' base_w=cvt(prev,os,ws); src_w=cvt(src,ss,ws); blended=blend(base_w,src_w);
    ''' base_c=cvt(prev,os,cs); blend_c=cvt(blended,ws,cs); res_c=clamp(base_c+cov*(blend_c-base_c));
    ''' final=cvt(res_c,cs,os).</summary>
    Private Function ComposeOne(prev As Double, src As Double, cov As Double,
                                ws As Integer, cs As Integer, ss As Integer, os As Integer, bop As Integer) As Double
        Dim base_w = Cvt1(prev, os, ws)
        Dim src_w = Cvt1(src, ss, ws)
        Dim blended = BlendDispatch1(bop, base_w, src_w)
        Dim base_c = Cvt1(prev, os, cs)
        Dim blend_c = Cvt1(blended, ws, cs)
        Dim res_c = Clamp01(base_c + cov * (blend_c - base_c))
        Return Cvt1(res_c, cs, os)
    End Function

    ''' <summary>Sample de un canal del DecodedTex en el índice de píxel del acumulador (w,h). Si el tex
    ''' es del MISMO tamaño, índice directo; si difiere, bilineal por UV (resolución por canal / LUT).</summary>
    Private Function SampleChannelAt(t As DecodedTex, accIdx As Integer, accW As Integer, accH As Integer, ch As Integer) As Double
        If t.Width = accW AndAlso t.Height = accH Then
            Return t.Rgba(accIdx * 4 + ch)
        End If
        Dim x = accIdx Mod accW, y = accIdx \ accW
        Dim u = (x + 0.5) / accW, v = (y + 0.5) / accH
        Return SampleBilinear(t, u, v, ch)
    End Function

    Private Function ToByte(c As Double) As Byte
        ' np.rint de gen3 = round-half-to-EVEN (banker's) = MidpointRounding.ToEven (default de Math.Round).
        ' El redondeo a byte se hace SOLO al final (los acumuladores quedan float toda la pasada), igual
        ' que gen3 (rint solo en el write). Asi CPU == `_3` byte-exacto.
        Dim v = Math.Round(Clamp01(c) * 255.0, MidpointRounding.ToEven)
        If v < 0 Then v = 0
        If v > 255 Then v = 255
        Return CByte(v)
    End Function

End Module
