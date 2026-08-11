Option Strict On

Imports System.Numerics
Imports System.Runtime.CompilerServices

Imports System.Linq

''' <summary>
''' RaceMenu / NiOverride (skee64) face-overlay compositor — the engine-EXACT blend of overlay layers ON TOP
''' of the vanilla facetint, decoded from the RaceMenu HLSL SOURCE (.fx in RaceMenu.bsa, not inferred). See
''' 60-racemenu-blend-de-overlays. skee applies overlays live (bExternalHeads=0 → not baked); to bake a
''' WYSIWYG _d (as the app does for LooksMenu overlays in FO4) we composite them here.
'''
''' Per layer: (1) TYPE combines the overlay texture with the layer colour; (2) BLENDMODE composites over the
''' accumulator, weighted by the layer alpha. Order = Ovl0..N (skee applies in slot order; lerp/blend NOT
''' commutative). SSE-only; the FO4 facetint path is untouched.
''' </summary>
Public Module SseOverlayCompositor
    ''' <summary>Lanes de un Vector(Of Single) en ESTA maquina: 8 con AVX2, 4 con SSE2. Es el tamano de
    ''' bloque de TODOS los loops vectoriales de este modulo. ⛔ Es constante durante todo el proceso
    ''' (Vector(Of T).Count lo es), asi que vive aca y no se recalcula por pixel. Hardcodear 8 en su lugar
    ''' corrompe silenciosamente una maquina de 4 lanes: leeria y escribiria fuera del bloque.</summary>
    Private ReadOnly lanes As Integer = FastPow.LaneCount

    ''' <summary>Offsets por canal de HsvToRgb (r=0, g=4, b=2). CONSTANTE: se calcula UNA vez y no por
    ''' bloque — VPerChannel asigna un array, y adentro del loop caliente eso es basura por bloque.</summary>
    Private ReadOnly HsvChannelOffsetsV As Vector(Of Single) = FastPow.VPerChannel(0.0F, 4.0F, 2.0F, 0.0F)


    ''' <summary>NiOverride blend modes (technique name → this enum). Math is per 60-racemenu-blend-de-overlays.</summary>
    Public Enum SseBlendMode
        Normal
        Multiply
        Overlay
        SoftLight       ' Pegtop
        LinearDodge     ' = Add / Screen-ish
        LinearBurn
        LinearLight
        ColorDodge
        ColorBurn
        Darken
        Lighten
        Tint
        Grayscale
        ColorMode       ' HSV: H,S from blend + V from source
        Rnm             ' reoriented normal map (normals only) — treated as Normal for colour _d
        TextureMode     ' replace/normal for textures — treated as Normal
    End Enum

    ''' <summary>One overlay layer. Color is RGBA [0,1]; Texture is decoded RGBA [0,1] (w*h*4) or Nothing
    ''' (type 2 solid). LayerType: 0 = texture×color, 1 = colour.rgb + (mask.r × colour.a) alpha, 2 = solid.</summary>
    Public Structure SseOverlay
        Public BlendMode As SseBlendMode
        Public LayerType As Integer
        Public Color As Double()     ' length 4, RGBA (color/valor: se queda en Double, no es buffer de píxeles)
        Public Texture As Single()   ' length w*h*4 RGBA, or Nothing (buffer de píxeles → Single storage)
    End Structure

    ''' <summary>Map a NiOverride blend-mode technique string (lowercase .fx name) to the enum. Unknown → Normal.</summary>
    Public Function BlendModeFromName(name As String) As SseBlendMode
        Select Case If(name, "").Trim().ToLowerInvariant()
            Case "multiply" : Return SseBlendMode.Multiply
            Case "overlay" : Return SseBlendMode.Overlay
            Case "softlight" : Return SseBlendMode.SoftLight
            Case "lineardodge", "add", "screen" : Return SseBlendMode.LinearDodge
            Case "linearburn" : Return SseBlendMode.LinearBurn
            Case "linearlight" : Return SseBlendMode.LinearLight
            Case "colordodge" : Return SseBlendMode.ColorDodge
            Case "colorburn" : Return SseBlendMode.ColorBurn
            Case "darken" : Return SseBlendMode.Darken
            Case "lighten" : Return SseBlendMode.Lighten
            Case "tint" : Return SseBlendMode.Tint
            Case "grayscale" : Return SseBlendMode.Grayscale
            Case "color" : Return SseBlendMode.ColorMode
            Case "rnm" : Return SseBlendMode.Rnm
            Case "texture" : Return SseBlendMode.TextureMode
            Case Else : Return SseBlendMode.Normal
        End Select
    End Function

    ''' <summary>Mapea un blend-mode de skee al par (blendOp, softLightModel) del dispatch COMPARTIDO CPU/GL.
    ''' ⭐ FUENTE ÚNICA: la usan <see cref="ApplyOverlays"/> (CPU) y el path GPU (uniform uBlendOp del compositor),
    ''' así los dos no pueden desincronizarse (antes el mapeo vivía inline en el CPU y el GPU no tenía ninguno).
    ''' softLightModel = 3 (Pegtop) SIEMPRE: es el que usa RaceMenu.
    ''' ⛔ Grayscale(20) y ColorMode(21) son NO SEPARABLES (necesitan los 3 canales del DESTINO juntos: luminancia y
    ''' HSV). El shader los implementa en <c>blendDispatch</c> (blendGrayscale/blendColorMode); el CPU los resuelve en
    ''' ramas propias dentro de ApplyOverlays con la MISMA fórmula. Por eso NO pueden pasar por BlendChannel, que es
    ''' escalar (per-canal).</summary>
    Public Function BlendOpFromSseMode(mode As SseBlendMode) As (BlendOp As Integer, SoftLight As Integer)
        Const PEGTOP As Integer = 3
        Select Case mode
            Case SseBlendMode.Multiply : Return (1, PEGTOP)
            Case SseBlendMode.Overlay, SseBlendMode.Tint : Return (2, PEGTOP)   ' RaceMenu "tint" == overlay (misma fórmula)
            Case SseBlendMode.SoftLight : Return (3, PEGTOP)
            Case SseBlendMode.LinearDodge : Return (12, PEGTOP)
            Case SseBlendMode.LinearBurn : Return (13, PEGTOP)
            Case SseBlendMode.LinearLight : Return (16, PEGTOP)
            Case SseBlendMode.ColorDodge : Return (8, PEGTOP)
            Case SseBlendMode.ColorBurn : Return (9, PEGTOP)
            Case SseBlendMode.Darken : Return (6, PEGTOP)
            Case SseBlendMode.Lighten : Return (7, PEGTOP)
            Case SseBlendMode.Grayscale : Return (20, PEGTOP)    ' NO separable → shader blendGrayscale
            Case SseBlendMode.ColorMode : Return (21, PEGTOP)    ' NO separable → shader blendColorMode
            Case Else : Return (0, PEGTOP)                        ' Normal / Rnm / TextureMode
        End Select
    End Function

    Public Sub ApplyOverlays(acc As Single(), overlays As IList(Of SseOverlay), w As Integer, h As Integer)
        If overlays Is Nothing OrElse overlays.Count = 0 Then Return
        Dim npix = w * h
        ' El loop de CAPAS queda SERIAL (el composite no es conmutativo: cada capa lee el acumulado de la
        ' anterior). El loop de PÍXELES dentro de cada capa es paralelo por rangos: cada píxel lee/escribe sólo
        ' sus propios índices ⇒ bit-idéntico al serial. El fold SSE corre a la resolución nativa del complexion
        ' (4096² con COtR), donde esto era parte de los segundos por fold.
        For Each ovIter In overlays
            Dim ov = ovIter   ' copia local para el lambda (el iterador muta)
            ' El mapeo modo→(blendOp, softLight) es constante por capa: se resuelve UNA vez, no por píxel.
            Dim m = BlendOpFromSseMode(ov.BlendMode)
            ' Color de capa: invariante del loop. Se iza a Single una vez por capa (antes se releia
            ' del Double() en cada pixel).
            Dim c0 = CSng(ov.Color(0)), c1 = CSng(ov.Color(1)), c2 = CSng(ov.Color(2)), c3 = CSng(ov.Color(3))
            Dim isNormal = (ov.BlendMode = SseBlendMode.Normal OrElse ov.BlendMode = SseBlendMode.Rnm OrElse ov.BlendMode = SseBlendMode.TextureMode)
            Dim isGray = (ov.BlendMode = SseBlendMode.Grayscale)
            ' ⭐ ColorMode (HSV) YA NO se queda escalar. Estuvo excluido por "ramoso", que era comodidad y no
            ' una barrera: las reducciones ENTRE canales son la misma horizontal que ya usaban Grayscale y el
            ' blend de normales, los tres If de la tinta son selects, y el Mod 6 resulta EXACTO por el rango
            ' de sus argumentos. Ver ColorModeBlockV.
            Dim isColor = (ov.BlendMode = SseBlendMode.ColorMode)
            ' ⭐ LA CONVENCION DE LA ETAPA `Overlay`, resuelta POR CAPA (el blendOp es de la capa) y FUERA del
            ' loop de pixeles. Es lo que hace que el bucket Overlay de CharGen Options IMPACTE: hasta acá este
            ' composite no leía la convención y el bucket era un control muerto. `isTextureSet` sigue el mismo
            ' criterio que el builder GPU (BuildSkeeGpuLayers): las capas Mask (type 1) son PaletteMask, el
            ' resto TextureSet — así el resolver elige el MISMO SrcSpace en los dos caminos.
            ' El espacio del ACUMULADOR sale de AccumSpaceForChannel con la capacidad del espejo CPU de ESTE
            ' camino, que es la MISMA que declara el GPU en sus ApplyFaceTintPipeline ⇒ no pueden discrepar.
            Dim ovConv = FaceTintConvention.ResolveConvention(FaceTintConvention.FaceTintStage.Overlay,
                                                              FaceTintChannel.Diffuse,
                                                              isTextureSet:=(ov.LayerType <> 1),
                                                              blendOp:=m.BlendOp)
            Dim ss = CInt(ovConv.SrcSpace), ws = CInt(ovConv.WorkingSpace), cs = CInt(ovConv.CompositeSpace)
            Dim asp = CInt(FaceTintConvention.AccumSpaceForChannel(FaceTintChannel.Diffuse,
                                                                   SseFaceTintComposer.AccumSpaceCapability))
            Dim vecOk = FastPow.AcceleratedV AndAlso
                        (isNormal OrElse isGray OrElse isColor OrElse FaceTintCpuCompositor.VecComposeSupported(0, m.BlendOp, m.SoftLight))
            System.Threading.Tasks.Parallel.ForEach(
                System.Collections.Concurrent.Partitioner.Create(0, npix),
                Sub(range)
                    Dim lo = range.Item1 * 4, hi = range.Item2 * 4
                    Dim e = lo
        ' lanes LOCAL: `Vector(Of Single).Count` es constante para el JIT y deja plegar los limites
        ' del loop; leerlo del campo de modulo lo vuelve una carga de memoria y mata la optimizacion.
        Dim lanes = Vector(Of Single).Count
                    If vecOk Then
                        ' POR PIXEL ENTERO: Grayscale lee ar,ag,ab y escribe los tres ⇒ read-after-write.
                        While (e And (lanes - 1)) <> 0 AndAlso e < hi
                            ApplyOverlayPixel(acc, ov, m, c0, c1, c2, c3, e >> 2, ss, ws, cs, asp) : e += 4
                        End While
                        e = ApplyOverlayRangeV(acc, ov, m, c0, c1, c2, c3, isNormal, isGray, isColor, e, hi, ss, ws, cs, asp)
                    End If
                    While e < hi
                        ApplyOverlayPixel(acc, ov, m, c0, c1, c2, c3, e >> 2, ss, ws, cs, asp) : e += 4
                    End While
                End Sub)
        Next
    End Sub

    ''' <summary>Un PIXEL del composite de overlay — la ley escalar VERBATIM (prologo, cola y los modos sin
    ''' espejo vectorial). Por pixel y no por elemento: Grayscale lee los tres canales y escribe los tres.</summary>
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Sub ApplyOverlayPixel(acc As Single(), ov As SseOverlay, m As (BlendOp As Integer, SoftLight As Integer),
                                  c0 As Single, c1 As Single, c2 As Single, c3 As Single, px As Integer,
                                  ss As Integer, ws As Integer, cs As Integer, asp As Integer)
            ' (1) TYPE: combine the overlay texture with the layer colour → premultiplied layer {rgb, a}
            Dim lr As Single, lg As Single, lb As Single, la As Single
            Dim tr = 1.0F, tg = 1.0F, tb = 1.0F, ta = 1.0F
            If ov.Texture IsNot Nothing Then tr = ov.Texture(px * 4) : tg = ov.Texture(px * 4 + 1) : tb = ov.Texture(px * 4 + 2) : ta = ov.Texture(px * 4 + 3)
            Select Case ov.LayerType
                Case 1 : lr = c0 : lg = c1 : lb = c2 : la = tr * c3          ' colour.rgb, alpha = mask.r × colour.a
                Case 2 : lr = c0 : lg = c1 : lb = c2 : la = c3               ' solid colour
                Case Else : lr = tr * c0 : lg = tg * c1 : lb = tb * c2 : la = ta * c3 ' texture × colour
            End Select
            If la <= 0.0F Then Return

            Dim ar = acc(px * 4), ag = acc(px * 4 + 1), ab = acc(px * 4 + 2)
            ' ⭐ CONVERSIONES DE ESPACIO DE LA ETAPA OVERLAY (bucket `Overlay` de CharGen Options).
            ' ⛔ Antes este composite corría SIN NINGUNA conversión, así que el bucket Overlay era un control
            ' que no movía un byte — y coincidía con el GPU sólo porque la ley SSE es all-Linear y las
            ' conversiones del compositor compartido quedaban en no-op. Era una COINCIDENCIA de los defaults,
            ' no una propiedad del diseño.
            ' ⛔ Las expresiones NO se reescribieron como un ComposeOne genérico: `mix(a,l,la)` y
            ' `l*la + a*(1−la)` son iguales en álgebra y DISTINTAS en redondeo. Se ENVUELVEN las de siempre,
            ' así que con ss=ws=cs=asp cada Cvt devuelve su entrada y la cuenta colapsa LITERALMENTE a la
            ' previa ⇒ byte-inerte hasta que el usuario mueva el bucket.
            Dim ac_r = Cvt(ar, asp, cs), ac_g = Cvt(ag, asp, cs), ac_b = Cvt(ab, asp, cs)
            If ov.BlendMode = SseBlendMode.Normal OrElse ov.BlendMode = SseBlendMode.Rnm OrElse ov.BlendMode = SseBlendMode.TextureMode Then
                ' normal.fx: over con el color de capa. No hay función de blend ⇒ no hay working space que
                ' aplicar; lo único definido es EN QUÉ ESPACIO se mezcla (CompositeSpace).
                Dim lc_r = Cvt(lr, ss, cs), lc_g = Cvt(lg, ss, cs), lc_b = Cvt(lb, ss, cs)
                acc(px * 4) = Cvt(CSng(lc_r * la + ac_r * (1 - la)), cs, asp)
                acc(px * 4 + 1) = Cvt(CSng(lc_g * la + ac_g * (1 - la)), cs, asp)
                acc(px * 4 + 2) = Cvt(CSng(lc_b * la + ac_b * (1 - la)), cs, asp)
            Else
                ' all other modes un-premultiply the layer colour, blend, then alpha-over
                Dim br = Clamp01(lr / la), bg = Clamp01(lg / la), bbl = Clamp01(lb / la)
                ' Source y destino AL WORKING SPACE antes del blend — mismo orden que ComposeOne y que el GLSL.
                Dim bw_r = Cvt(br, ss, ws), bw_g = Cvt(bg, ss, ws), bw_b = Cvt(bbl, ss, ws)
                Dim aw_r = Cvt(ar, asp, ws), aw_g = Cvt(ag, asp, ws), aw_b = Cvt(ab, asp, ws)
                Dim rr As Single, rg As Single, rb As Single
                If ov.BlendMode = SseBlendMode.Grayscale Then
                    Dim lum = 0.299F * aw_r + 0.587F * aw_g + 0.114F * aw_b
                    rr = lum * bw_r : rg = lum * bw_g : rb = lum * bw_b
                ElseIf ov.BlendMode = SseBlendMode.ColorMode Then
                    Dim hsvBlend = RgbToHsv(bw_r, bw_g, bw_b)
                    Dim vSrc = MathF.Max(aw_r, MathF.Max(aw_g, aw_b))
                    Dim outc = HsvToRgb(hsvBlend(0), hsvBlend(1), vSrc)
                    rr = outc(0) : rg = outc(1) : rb = outc(2)
                Else
                    ' Reuse the SHARED FO4 blend dispatch (CPU/GL parity). El mapeo modo→(blendOp, softLight)
                    ' sale de BlendOpFromSseMode = la MISMA fuente que usa el path GPU (uBlendOp del compositor),
                    ' así CPU y GL no pueden desincronizarse.
                    rr = FaceTintCpuCompositor.BlendChannel(m.BlendOp, m.SoftLight, aw_r, bw_r)
                    rg = FaceTintCpuCompositor.BlendChannel(m.BlendOp, m.SoftLight, aw_g, bw_g)
                    rb = FaceTintCpuCompositor.BlendChannel(m.BlendOp, m.SoftLight, aw_b, bw_b)
                End If
                Dim rc_r = Cvt(rr, ws, cs), rc_g = Cvt(rg, ws, cs), rc_b = Cvt(rb, ws, cs)
                acc(px * 4) = Cvt(CSng((1 - la) * ac_r + rc_r * la), cs, asp)
                acc(px * 4 + 1) = Cvt(CSng((1 - la) * ac_g + rc_g * la), cs, asp)
                acc(px * 4 + 2) = Cvt(CSng((1 - la) * ac_b + rc_b * la), cs, asp)
            End If
    End Sub

    ''' <summary>Conversión de espacio de UN canal — la MISMA función que usa el compositor compartido
    ''' (<c>FaceTintCpuCompositor.ConvertSpaceShared</c> = su <c>Cvt1</c>), con el mismo cortocircuito cuando
    ''' origen y destino coinciden. ⛔ No re-implementarla acá: si el overlay convirtiera con otra curva, CPU
    ''' y GL divergirían en el VALOR sin que nadie se equivoque en la matemática del blend.</summary>
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Function Cvt(v As Single, fromS As Integer, toS As Integer) As Single
        Return FaceTintCpuCompositor.ConvertSpaceShared(v, fromS, toS)
    End Function

    ''' <summary>Cuerpo vectorial del composite de overlay: 8 floats = 2 pixeles.
    ''' <para>El alpha de capa sale de un lane DIFUNDIDO (type 1 → lane 0, type 0 → lane 3) con un permute de
    ''' indices constantes. Grayscale usa la MISMA reduccion horizontal bit-exacta que el blend de normales:
    ''' con el lane 3 en cero el arbol da <c>((0.299ar + 0.587ag) + 0.114ab)</c>, que es como asocia VB.</para></summary>
    Private Function ApplyOverlayRangeV(acc As Single(), ov As SseOverlay, m As (BlendOp As Integer, SoftLight As Integer),
                                        c0 As Single, c1 As Single, c2 As Single, c3 As Single,
                                        isNormal As Boolean, isGray As Boolean, isColor As Boolean, lo As Integer, hi As Integer,
                                        ss As Integer, ws As Integer, cs As Integer, asp As Integer) As Integer
        Dim e = lo
        ' lanes LOCAL: `Vector(Of Single).Count` es constante para el JIT y deja plegar los limites
        ' del loop; leerlo del campo de modulo lo vuelve una carga de memoria y mata la optimizacion.
        Dim lanes = Vector(Of Single).Count
        ' scratch DEL HILO para las permutaciones dentro del pixel (reemplazan a Vector256.Shuffle,
        ' que no existe en la API de ancho variable). ⛔ Local: compartirlo entre hilos lo corrompe.
        Dim shTmp(2 * lanes - 1) As Single   ' mitad baja = copia, mitad alta = destino (ver FastPow)
        Dim tex = ov.Texture
        Dim lt = ov.LayerType
        Dim zero = Vector(Of Single).Zero, one = VBroadcastS(1.0F)
        Dim colV = FastPow.VPerChannel(c0, c1, c2, 0.0F)
        Dim c3V = VBroadcastS(c3)
        Dim rgbMask = FastPow.VPerChannelMask(-1, -1, -1, 0)
        Dim grayW = FastPow.VPerChannel(0.299F, 0.587F, 0.114F, 0.0F)
        While e + lanes <= hi
            Dim t = If(tex Is Nothing, one, VBroadcastS(tex, e))
            Dim lv As Vector(Of Single), la As Vector(Of Single)
            If lt = 1 Then
                lv = colV : la = Vector.Multiply(FastPow.VBroadcastChannelV(t, 0, shTmp), c3V)
            ElseIf lt = 2 Then
                lv = colV : la = c3V
            Else
                lv = Vector.Multiply(t, colV) : la = Vector.Multiply(FastPow.VBroadcastChannelV(t, 3, shTmp), c3V)
            End If
            ' early-out de bloque: restituye el `If la <= 0 Then Continue For` del escalar (ver la trampa 1)
            If Vector.LessThanOrEqualAll(Of Single)(la, zero) Then
                e += lanes
                Continue While
            End If
            Dim a = VBroadcastS(acc, e)
            ' ⭐ Espejo EXACTO de las conversiones del escalar (ver ApplyOverlayPixel), en el mismo orden y con
            ' la MISMA función (CvtV es el espejo de ConvertSpaceShared, cortocircuito incluido). El lane del
            ' ALPHA también se convierte y da igual: lo descarta el `rgbMask` del select final.
            Dim ac = FaceTintCpuCompositor.CvtV(a, asp, cs)
            Dim res As Vector(Of Single)
            If isNormal Then
                Dim lc = FaceTintCpuCompositor.CvtV(lv, ss, cs)
                res = FaceTintCpuCompositor.CvtV(
                    Vector.Add(Vector.Multiply(lc, la), Vector.Multiply(ac, Vector.Subtract(one, la))), cs, asp)
            Else
                Dim b = FaceTintCpuCompositor.Clamp01V(Vector.Divide(lv, la))
                Dim bw = FaceTintCpuCompositor.CvtV(b, ss, ws)
                Dim aw = FaceTintCpuCompositor.CvtV(a, asp, ws)
                Dim r As Vector(Of Single)
                If isGray Then
                    Dim wv = Vector.ConditionalSelect(rgbMask, Vector.Multiply(aw, grayW), zero)
                    Dim s1 = Vector.Add(wv, FastPow.VSwapWithinPixel(wv, 1, shTmp))
                    Dim lum = Vector.Add(s1, FastPow.VSwapWithinPixel(s1, 2, shTmp))
                    r = Vector.Multiply(lum, bw)
                ElseIf isColor Then
                    r = ColorModeBlockV(aw, bw, shTmp)
                Else
                    r = FaceTintCpuCompositor.BlendDispatchV(m.BlendOp, m.SoftLight, aw, bw)
                End If
                Dim rc = FaceTintCpuCompositor.CvtV(r, ws, cs)
                res = FaceTintCpuCompositor.CvtV(
                    Vector.Add(Vector.Multiply(Vector.Subtract(one, la), ac), Vector.Multiply(rc, la)), cs, asp)
            End If
            Dim keep = Vector.AndNot(rgbMask, Vector.LessThanOrEqual(la, zero))
            Vector.ConditionalSelect(keep, res, a).CopyTo(acc, e)
            e += lanes
        End While
        Return e
    End Function

    ''' <summary>⭐ ColorMode (HSV) VECTORIZADO: <c>HsvToRgb(H,S del blend, V del source)</c>. Espejo exacto de
    ''' la rama <c>ElseIf ov.BlendMode = ColorMode</c> del escalar (<see cref="ApplyOverlayPixel"/>), que usa
    ''' <see cref="RgbToHsv"/> + <see cref="HsvToRgb"/>.
    '''
    ''' <para><b>Por qué SÍ se puede, después de estar declarado "no vectorizable".</b> Lo ramoso de HSV son
    ''' dos cosas y ninguna es una barrera: (1) las reducciones ENTRE canales (max/min de R,G,B) son la MISMA
    ''' reducción horizontal dentro del píxel que ya usa Grayscale y el blend de normales; (2) los tres `If`
    ''' de la tinta se vuelven selects. Lo único que parecía irreducible era el <c>Mod 6</c>.</para>
    '''
    ''' <para>⛔⭐ <b>EL <c>Mod 6</c> ES EXACTO SIN fmod GENERAL, POR EL RANGO DE SUS ARGUMENTOS</b>, y esto es
    ''' lo que hace que el espejo sea bit-idéntico y no una aproximación:
    ''' <list type="bullet">
    ''' <item>En RgbToHsv el argumento es <c>(g−b)/d</c> con <c>mx = r</c>, o sea <c>g,b ≤ r</c> y
    '''   <c>d = r − mn</c> ⇒ <c>|(g−b)/d| ≤ 1</c>. Con |x| &lt; 6, <c>fmod(x,6) = x</c>: el Mod es IDENTIDAD.</item>
    ''' <item>En HsvToRgb es <c>h·6 + k</c> con <c>h ∈ [0,1]</c> y <c>k ∈ {0,2,4}</c> ⇒ <c>x ∈ [0,10]</c>. Ahí
    '''   <c>fmod(x,6)</c> es <c>x</c> si <c>x &lt; 6</c> y <c>x − 6</c> si no — y esa resta es EXACTA (Sterbenz:
    '''   x y 6 están dentro de un factor 2 para x ∈ [6,12]).</item>
    ''' </list>
    ''' Por eso NO hace falta emular fmod con <c>x − 6·trunc(x/6)</c>, que sí introduciría redondeo en la
    ''' división y en el producto y podría mover un bit. El self-test lo verifica; no se asume.</para>
    '''
    ''' <para>El vector viene en AoS: cada píxel son 4 lanes (R,G,B,A). Los offsets por canal (0,4,2) de
    ''' HsvToRgb son un vector CONSTANTE en ese layout.</para></summary>
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Function ColorModeBlockV(a As Vector(Of Single), b As Vector(Of Single), shTmp As Single()) As Vector(Of Single)
        Dim one = VBroadcastS(1.0F), zero = Vector(Of Single).Zero
        Dim six = VBroadcastS(6.0F), three = VBroadcastS(3.0F)

        ' --- RgbToHsv(b) --- cada canal difundido a los 4 lanes de su pixel
        Dim br = FastPow.VBroadcastChannelV(b, 0, shTmp)
        Dim bg = FastPow.VBroadcastChannelV(b, 1, shTmp)
        Dim bb = FastPow.VBroadcastChannelV(b, 2, shTmp)
        ' MISMO anidado que el escalar: MathF.Max(r, MathF.Max(g, b)).
        Dim mx = FaceTintCpuCompositor.MaxVShared(br, FaceTintCpuCompositor.MaxVShared(bg, bb))
        Dim mn = FaceTintCpuCompositor.MinVShared(br, FaceTintCpuCompositor.MinVShared(bg, bb))
        Dim d = Vector.Subtract(mx, mn)

        ' h por la rama del canal que es el maximo (los tres If del escalar, vueltos selects y en el MISMO orden)
        Dim hR = ModSixV(Vector.Divide(Vector.Subtract(bg, bb), d), six)          ' identidad: |x| <= 1
        Dim hG = Vector.Add(Vector.Divide(Vector.Subtract(bb, br), d), VBroadcastS(2.0F))
        Dim hB = Vector.Add(Vector.Divide(Vector.Subtract(br, bg), d), VBroadcastS(4.0F))
        Dim h = Vector.ConditionalSelect(Vector.Equals(mx, br), hR,
                    Vector.ConditionalSelect(Vector.Equals(mx, bg), hG, hB))
        h = Vector.Divide(h, six)
        h = Vector.ConditionalSelect(Vector.LessThan(h, zero), Vector.Add(h, one), h)
        ' d <= 1e-7 => h = 0 (el `If d > 0.0000001F` del escalar; con d = 0 las divisiones dan NaN/Inf y este
        ' select las descarta, igual que el escalar nunca las evalua)
        h = Vector.ConditionalSelect(Vector.GreaterThan(d, VBroadcastS(0.0000001F)), h, zero)
        ' s = If(mx <= 0, 0, d/mx)
        ' ⛔ La condicion es `mx <= 0`, NO `mx > 0`: con mx = NaN el escalar (If(mx <= 0, 0, d/mx)) evalua
        ' la rama FALSA y devuelve d/mx = NaN, mientras que `mx > 0` tambien es falsa y daba 0.
        Dim s = Vector.ConditionalSelect(Vector.LessThanOrEqual(mx, zero), zero, Vector.Divide(d, mx))

        ' --- V del SOURCE: MathF.Max(ar, MathF.Max(ag, ab)) ---
        Dim ar = FastPow.VBroadcastChannelV(a, 0, shTmp)
        Dim ag = FastPow.VBroadcastChannelV(a, 1, shTmp)
        Dim ab = FastPow.VBroadcastChannelV(a, 2, shTmp)
        Dim v = FaceTintCpuCompositor.MaxVShared(ar, FaceTintCpuCompositor.MaxVShared(ag, ab))

        ' --- HsvToRgb(h, s, v) --- los offsets por canal son constantes en el layout AoS
        Dim x = ModSixV(Vector.Add(Vector.Multiply(h, six), HsvChannelOffsetsV), six)
        Dim c = FaceTintCpuCompositor.Clamp01V(Vector.Subtract(Vector.Abs(Vector.Subtract(x, three)), one))
        Return Vector.Multiply(v, Vector.Add(one, Vector.Multiply(s, Vector.Subtract(c, one))))
    End Function

    ''' <summary><c>x Mod 6</c> para <c>|x| &lt; 12</c>, que es TODO el dominio real de HSV (ver la nota de
    ''' <see cref="ColorModeBlockV"/>). Exacto: en ese rango fmod sólo puede restar o sumar 6 una vez, y esa
    ''' operación no redondea. ⛔ NO usar <c>x − 6·trunc(x/6)</c>: eso sí redondearía.</summary>
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Function ModSixV(x As Vector(Of Single), six As Vector(Of Single)) As Vector(Of Single)
        Dim r = Vector.ConditionalSelect(Vector.GreaterThanOrEqual(x, six), Vector.Subtract(x, six), x)
        Return Vector.ConditionalSelect(Vector.LessThanOrEqual(r, Vector.Negate(six)), Vector.Add(r, six), r)
    End Function

    ''' <summary>Orden configurable de los overlays Face[Ovl] (= análogo SSE de los SWAPS de FO4:
    ''' <c>Setting_FaceTintSort_SSE.SwapRules</c>, claves <see cref="FaceTintSseOverlaySortKey"/>). DEFAULT =
    ''' <c>[Ovl_Index asc]</c> = orden skee (Ovl0 abajo→OvlN arriba, OverlayInterface for i=0..N) = IDENTIDAD ⇒
    ''' byte-idéntico. Tiebreak final por posición original ⇒ claves iguales preservan el orden skee. Recibe la lista
    ''' YA filtrada (los dos callers filtran por DiffusePath vs NormalPath). Reordenar DESVÍA de skee (elección del usuario).</summary>
    Public Function SortFaceOverlays(list As List(Of RaceMenuJslot.JslotOverlayNode)) As List(Of RaceMenuJslot.JslotOverlayNode)
        If list Is Nothing OrElse list.Count <= 1 Then Return list
        Dim cfg = Config_App.Current?.Setting_FaceTintSort_SSE
        Dim rules = If(cfg IsNot Nothing, cfg.SwapRules, Nothing)
        If rules Is Nothing OrElse rules.Count = 0 Then Return list.OrderBy(Function(o) CompositeOrderKey(o.NodeName)).ToList()
        Dim items As New List(Of (Ov As RaceMenuJslot.JslotOverlayNode, Pos As Integer))
        For i = 0 To list.Count - 1 : items.Add((list(i), i)) : Next
        items.Sort(Function(a, b)
                       For Each r In rules
                           Dim c = SseOverlayKey(a.Ov, r.Key).CompareTo(SseOverlayKey(b.Ov, r.Key))
                           If r.Descending Then c = -c
                           If c <> 0 Then Return c
                       Next
                       Return a.Pos.CompareTo(b.Pos)   ' tiebreak estable = orden de entrada (post-skee OrderBy del default)
                   End Function)
        Return items.Select(Function(x) x.Ov).ToList()
    End Function

    Private Function SseOverlayKey(ov As RaceMenuJslot.JslotOverlayNode, key As Integer) As Double
        Select Case CType(key, FaceTintSseOverlaySortKey)
            Case FaceTintSseOverlaySortKey.Alpha : Return If(ov.HasAlpha, ov.Alpha, 1.0)
            Case FaceTintSseOverlaySortKey.Has_Tint : Return If(ov.HasTint, 1.0, 0.0)
            Case Else : Return CompositeOrderKey(ov.NodeName)   ' Ovl_Index (default) = orden skee (pool primario, y encima el magic)
        End Select
    End Function

    Public Function ComposeFaceOverlaysIntoDiffuse(acc As Single(), overlays As IList(Of RaceMenuJslot.JslotOverlayNode),
                                                   w As Integer, h As Integer,
                                                   decode As Func(Of String, Integer, Integer, Single())) As Boolean
        If acc Is Nothing OrElse overlays Is Nothing OrElse overlays.Count = 0 OrElse decode Is Nothing Then Return False
        Dim npix = w * h
        Dim any = False
        ' ORDEN = skee: por ÍNDICE DE NODO Ovl{n} ASCENDENTE (Ovl0 abajo → OvlN arriba). skee instala
        ' `for i=0..N` + AttachChild ⇒ Ovl0 se dibuja primero (abajo) y OvlN último (arriba); el topmost gana en
        ' solapes. NO por posición en la lista (el jslot puede venir en cualquier orden). Ver OverlayInterface.cpp.
        Dim faceOrdered = SortFaceOverlays(overlays.
            Where(Function(o) IsFoldableFaceOverlay(o) AndAlso Not String.IsNullOrEmpty(o.DiffusePath)).ToList())   ' predicado unico + orden skee
        For Each ov In faceOrdered
            ' Predicado ÚNICO con el gate (HasBakeableFaceOverlays) ⇒ no pueden discrepar. Va ANTES del decode:
            ' una capa invisible no justifica leer su textura.
            If Not OverlayIsVisible(ov) Then Continue For
            Dim tex = decode(ov.DiffusePath, w, h)
            If tex Is Nothing OrElse tex.Length < npix * 4 Then
                ' El gate dijo que SÍ (hay ruta + opacidad) y acá no se pudo leer ⇒ es un ERROR, no un no-op.
                Dim dpFail = ov.DiffusePath
                Logger.LogLazy(Function() $"[SSE-OVL] overlay de cara SALTEADO: no se pudo leer/decodificar el diffuse '{dpFail}'")
                Continue For
            End If
            Dim opacity As Double = If(ov.HasAlpha, ov.Alpha, 1.0)
            ' Invariantes del loop: se angostan UNA vez por overlay, no por pixel.
            Dim opa = CSng(opacity)
            Dim tr = CSng(If(ov.HasTint, ov.TintR, 1.0))
            Dim tg = CSng(If(ov.HasTint, ov.TintG, 1.0))
            Dim tb = CSng(If(ov.HasTint, ov.TintB, 1.0))
            ' COBERTURA = ALPHA del diffuse (FIEL AL ENGINE). VERIFICADO (Shader_Class.vb:1851-1859, sse_facegen_skin
            ' RE): en SSE el BSLightingShader —el shader del overlay decal (SkinTint/FaceGen)— NO tiene greyscale-to-
            ' color/alpha (eso vive SOLO en el BSEffectShader). El diffuse se usa normal: RGB=color, alpha=cobertura
            ' (color.a *= baseMap.a). type 0 de skee: color = tex.rgb × tint.
            ' Paralelo por rangos (píxeles independientes ⇒ bit-idéntico); el orden ENTRE overlays lo da el
            ' For Each de afuera, que sigue serial (alpha-over no conmutativo).
            SkeeMaskApply(acc, tex, tr, tg, tb, opa, npix)
            any = True
        Next
        Return any
    End Function


    ''' <summary>Alpha-over de skee sobre el acumulador (prologo escalar / cuerpo vectorial / cola).
    ''' Extraida del lambda para que <see cref="OverlayVectorSelfTest"/> pueda contrastarla contra
    ''' <see cref="SkeeMaskOne"/>; el codigo es el mismo.</summary>
    Private Sub SkeeMaskApply(acc As Single(), tex As Single(), tr As Single, tg As Single, tb As Single,
                              opa As Single, npix As Integer)
        System.Threading.Tasks.Parallel.ForEach(
                System.Collections.Concurrent.Partitioner.Create(0, npix),
                Sub(range)
                    Dim lo = range.Item1 * 4, hi = range.Item2 * 4
                    Dim e = lo
        ' lanes LOCAL: `Vector(Of Single).Count` es constante para el JIT y deja plegar los limites
        ' del loop; leerlo del campo de modulo lo vuelve una carga de memoria y mata la optimizacion.
        Dim lanes = Vector(Of Single).Count
                    If FastPow.AcceleratedV Then
                        While (e And (lanes - 1)) <> 0 AndAlso e < hi
                            SkeeMaskOne(acc, tex, tr, tg, tb, opa, e) : e += 1
                        End While
                        ' AoS, 8 floats = 2 pixeles. `la` sale del ALPHA de cada pixel ⇒ se difunde con un
                        ' permute de indices constantes (3,3,3,3, 7,7,7,7), igual que el mask de ComposeLayer.
                        Dim tintV = FastPow.VPerChannel(tr, tg, tb, 1.0F)
                        Dim rgbMask = FastPow.VPerChannelMask(-1, -1, -1, 0)
                        Dim opaV = VBroadcastS(opa)
                        ' scratch DEL HILO para las permutaciones dentro del pixel. ⛔ Local por llamada.
                        Dim shTmp(2 * lanes - 1) As Single   ' mitad baja = copia, mitad alta = destino (ver FastPow)
                        Dim one = VBroadcastS(1.0F)
                        Dim zero = Vector(Of Single).Zero
                        While e + lanes <= hi
                            Dim t = VBroadcastS(tex, e)
                            Dim a = VBroadcastS(acc, e)
                            Dim la = Clamp01V(Vector.Multiply(FastPow.VBroadcastChannelV(t, 3, shTmp), opaV))
                            ' early-out de bloque: restituye el `If la <= 0 Then Continue For` del escalar
                            If Vector.LessThanOrEqualAll(Of Single)(la, zero) Then
                                e += lanes
                                Continue While
                            End If
                            ' (tex*tint)*la + acc*(1-la) — MISMO orden de operaciones que el escalar
                            Dim res = Vector.Add(Vector.Multiply(Vector.Multiply(t, tintV), la),
                                                    Vector.Multiply(a, Vector.Subtract(one, la)))
                            ' replica los dos guards a la vez: alpha intacto, y `If la <= 0 Then Continue For`
                            Dim keep = Vector.AndNot(rgbMask, Vector.LessThanOrEqual(la, zero))
                            Vector.ConditionalSelect(keep, res, a).CopyTo(acc, e)
                            e += lanes
                        End While
                    End If
                    While e < hi
                        SkeeMaskOne(acc, tex, tr, tg, tb, opa, e) : e += 1
                    End While
                End Sub)
    End Sub


    ''' <summary>Blend de normales MSN sobre el acumulador (prologo / cuerpo vectorial / cola, todo por
    ''' PIXEL ENTERO). Extraida del lambda para que <see cref="OverlayVectorSelfTest"/> la pueda contrastar
    ''' contra <see cref="MsnBlendPixel"/>; el codigo es el mismo.</summary>
    Private Sub MsnBlendApply(msnAcc As Single(), ovNorm As Single(), ovDiff As Single(),
                              opa As Single, npix As Integer)
        System.Threading.Tasks.Parallel.ForEach(
                System.Collections.Concurrent.Partitioner.Create(0, npix),
                Sub(range)
                    Dim lo = range.Item1 * 4, hi = range.Item2 * 4
                    Dim e = lo
        ' lanes LOCAL: `Vector(Of Single).Count` es constante para el JIT y deja plegar los limites
        ' del loop; leerlo del campo de modulo lo vuelve una carga de memoria y mata la optimizacion.
        Dim lanes = Vector(Of Single).Count
                    If FastPow.AcceleratedV Then
                        ' de a PIXEL ENTERO (4 elementos), no de a elemento: ver MsnBlendPixel
                        While (e And (lanes - 1)) <> 0 AndAlso e < hi
                            MsnBlendPixel(msnAcc, ovNorm, ovDiff, opa, e >> 2) : e += 4
                        End While
                        Dim rgbMask = FastPow.VPerChannelMask(-1, -1, -1, 0)
                        Dim opaV = VBroadcastS(opa)
                        Dim one = VBroadcastS(1.0F), two = VBroadcastS(2.0F), half = VBroadcastS(0.5F)
                        Dim zero = Vector(Of Single).Zero
                        Dim epsV = VBroadcastS(0.0000001F)
                        ' scratch DEL HILO para las permutaciones dentro del pixel. ⛔ Local por llamada.
                        Dim shTmp(2 * lanes - 1) As Single   ' mitad baja = copia, mitad alta = destino (ver FastPow)
                        While e + lanes <= hi
                            Dim accV = VBroadcastS(msnAcc, e)
                            Dim covV = Vector.Multiply(FastPow.VBroadcastChannelV(VBroadcastS(ovDiff, e), 3, shTmp), opaV)
                            ' early-out de bloque: restituye el `If cov <= 0 Then Continue For` del escalar
                            If Vector.LessThanOrEqualAll(Of Single)(covV, zero) Then
                                e += lanes
                                Continue While
                            End If
                            covV = Vector.ConditionalSelect(Vector.GreaterThan(covV, one), one, covV)   ' If cov > 1 Then cov = 1
                            Dim hv = Vector.Subtract(Vector.Multiply(two, accV), one)
                            Dim ovv = Vector.Subtract(Vector.Multiply(two, VBroadcastS(ovNorm, e)), one)
                            Dim nv = Vector.Add(hv, Vector.Multiply(covV, Vector.Subtract(ovv, hv)))
                            ' len = sqrt(nx*nx + ny*ny + nz*nz) — suma HORIZONTAL dentro de cada pixel.
                            ' ⭐ El orden del arbol coincide EXACTAMENTE con el del escalar: con el lane 3 puesto
                            ' en 0, el primer paso da (nx²+ny²) y (nz²+0), y el segundo los suma ⇒ ((nx²+ny²)+nz²),
                            ' que es como asocia VB. La suma float NO es asociativa, asi que esto no es un detalle.
                            Dim sq = Vector.ConditionalSelect(rgbMask, Vector.Multiply(nv, nv), zero)
                            Dim s1 = Vector.Add(sq, FastPow.VSwapWithinPixel(sq, 1, shTmp))
                            Dim ss = Vector.Add(s1, FastPow.VSwapWithinPixel(s1, 2, shTmp))
                            Dim lenV = Vector.SquareRoot(ss)
                            Dim norm = Vector.ConditionalSelect(Vector.GreaterThan(lenV, epsV),
                                                                   Vector.Divide(nv, lenV), nv)
                            Dim res = Vector.Multiply(Vector.Add(norm, one), half)
                            ' guards del escalar: alpha intacto y `If cov <= 0 Then Continue For`
                            Dim keep = Vector.AndNot(rgbMask, Vector.LessThanOrEqual(covV, zero))
                            Vector.ConditionalSelect(keep, res, accV).CopyTo(msnAcc, e)
                            e += lanes
                        End While
                    End If
                    While e < hi
                        MsnBlendPixel(msnAcc, ovNorm, ovDiff, opa, e >> 2) : e += 4
                    End While
                End Sub)
    End Sub

    ''' <summary>Self-test de paridad de los DOS loops vectorizados de este modulo (alpha-over de skee y blend
    ''' de normales MSN) contra su ley escalar. Devuelve "" si todo coincide bit a bit.
    ''' <para>⛔ HACE FALTA que exista: el corpus VANILLA de SSE no tiene overlays de RaceMenu, asi que un
    ''' barrido A/B de corpus NO ejercita nada de esto. Sin este test, estos dos caminos irian sin cobertura.</para>
    ''' <para>Los tamaños son deliberadamente impares para que el prologo y la cola entren en juego.</para></summary>
    Public Function OverlayVectorSelfTest() As String
        If Not FastPow.AcceleratedV Then Return ""
        Dim seed As UInteger = 24680135UI
        For Each np In New Integer() {1, 2, 3, 7, 9, 33, 1021}
            Dim tex(np * 4 - 1) As Single, nrm(np * 4 - 1) As Single, dif(np * 4 - 1) As Single
            For i = 0 To np * 4 - 1
                tex(i) = Rnd01(seed) : nrm(i) = Rnd01(seed) : dif(i) = Rnd01(seed)
            Next
            ' bordes: cobertura 0 (dispara el Continue For) y 1, y un normal degenerado (len ~ 0)
            If np >= 3 Then
                dif(3) = 0.0F : dif(7) = 1.0F
                nrm(4) = 0.5F : nrm(5) = 0.5F : nrm(6) = 0.5F      ' -> n = (0,0,0) tras el decode
            End If
            Dim opa As Single = 0.8F, tr As Single = 0.9F, tg As Single = 0.7F, tb As Single = 1.1F

            ' ---- alpha-over de skee ----
            Dim aVec(np * 4 - 1) As Single, aRef(np * 4 - 1) As Single
            For i = 0 To np * 4 - 1
                aVec(i) = Rnd01(seed) : aRef(i) = aVec(i)
            Next
            SkeeMaskApply(aVec, tex, tr, tg, tb, opa, np)
            For e = 0 To np * 4 - 1
                SkeeMaskOne(aRef, tex, tr, tg, tb, opa, e)
            Next
            For i = 0 To np * 4 - 1
                If BitConverter.SingleToInt32Bits(aVec(i)) <> BitConverter.SingleToInt32Bits(aRef(i)) Then
                    Return $"SkeeMask vector MISMATCH: npix={np} i={i} scalar={aRef(i)} vector={aVec(i)}"
                End If
            Next

            ' ---- blend de normales MSN ----
            Dim mVec(np * 4 - 1) As Single, mRef(np * 4 - 1) As Single
            For i = 0 To np * 4 - 1
                mVec(i) = Rnd01(seed) : mRef(i) = mVec(i)
            Next
            MsnBlendApply(mVec, nrm, dif, opa, np)
            For px = 0 To np - 1
                MsnBlendPixel(mRef, nrm, dif, opa, px)
            Next
            For i = 0 To np * 4 - 1
                If BitConverter.SingleToInt32Bits(mVec(i)) <> BitConverter.SingleToInt32Bits(mRef(i)) Then
                    Return $"MsnBlend vector MISMATCH: npix={np} i={i} scalar={mRef(i)} vector={mVec(i)}"
                End If
            Next
        Next

        ' ---- ApplyOverlays: TODOS los blend modes x los 3 layer types ----
        For Each bm In [Enum].GetValues(GetType(SseBlendMode))
            Dim mode = CType(bm, SseBlendMode)
            For Each lt In New Integer() {0, 1, 2}
                For Each np In New Integer() {1, 2, 3, 7, 9, 33, 1021}
                    Dim tex(np * 4 - 1) As Single
                    For i = 0 To np * 4 - 1
                        tex(i) = Rnd01(seed)
                    Next
                    If np >= 3 Then tex(3) = 0.0F : tex(0) = 0.0F     ' alpha/mask 0 -> Continue For
                    ' ⭐ NaN EN LA COBERTURA. Es EL caso que distingue el guard escalar `<= 0` (falso con
                    ' NaN => COMPONE) del vectorial `> 0` (tambien falso => SALTEABA). Como el prologo/cola
                    ' son escalares y el cuerpo vectorial, sin esto el resultado del pixel dependia de donde
                    ' corto el Partitioner. Va en un pixel ALTO para que caiga en el cuerpo vectorial.
                    If np >= 12 Then tex(4 * 9 + 3) = Single.NaN : tex(4 * 9) = Single.NaN
                    If np >= 12 Then tex(4 * 10 + 1) = Single.NaN
                    Dim ovT As New SseOverlay With {.BlendMode = mode, .LayerType = lt,
                                                    .Color = New Double() {0.4, 0.55, 0.7, 0.8},
                                                    .Texture = tex}
                    Dim aVec(np * 4 - 1) As Single, aRef(np * 4 - 1) As Single
                    For i = 0 To np * 4 - 1
                        aVec(i) = Rnd01(seed) : aRef(i) = aVec(i)
                    Next
                    ApplyOverlays(aVec, New SseOverlay() {ovT}, np, 1)
                    Dim mm = BlendOpFromSseMode(mode)
                    ' La referencia escalar resuelve la MISMA convención que la entrada pública (misma etapa,
                    ' mismo isTextureSet, mismo blendOp): si tomara otra, el test compararía dos leyes y daría
                    ' rojo por el motivo equivocado.
                    Dim refConv = FaceTintConvention.ResolveConvention(FaceTintConvention.FaceTintStage.Overlay,
                                                                       FaceTintChannel.Diffuse,
                                                                       isTextureSet:=(lt <> 1), blendOp:=mm.BlendOp)
                    Dim refAsp = CInt(FaceTintConvention.AccumSpaceForChannel(FaceTintChannel.Diffuse,
                                                                              SseFaceTintComposer.AccumSpaceCapability))
                    For px = 0 To np - 1
                        ApplyOverlayPixel(aRef, ovT, mm, 0.4F, 0.55F, 0.7F, 0.8F, px,
                                          CInt(refConv.SrcSpace), CInt(refConv.WorkingSpace),
                                          CInt(refConv.CompositeSpace), refAsp)
                    Next
                    For i = 0 To np * 4 - 1
                        If BitConverter.SingleToInt32Bits(aVec(i)) <> BitConverter.SingleToInt32Bits(aRef(i)) Then
                            Return $"ApplyOverlays vector MISMATCH: mode={mode} layerType={lt} npix={np} i={i} scalar={aRef(i)} vector={aVec(i)}"
                        End If
                    Next
                Next
            Next
        Next
        Return ""
    End Function

    Private Function Rnd01(ByRef s As UInteger) As Single
        s = s Xor (s << 13) : s = s Xor (s >> 17) : s = s Xor (s << 5)
        Return CSng(s Mod 1000003UI) / 1000003.0F
    End Function

    ''' <summary>Un PIXEL COMPLETO del blend de normales MSN — la ley escalar, verbatim.
    ''' <para>⛔ ES POR PIXEL Y NO POR ELEMENTO, A PROPOSITO. A diferencia del fold o del alpha-over, este
    ''' cuerpo LEE los tres canales (<c>hx,hy,hz</c>) y ESCRIBE los tres: hacerlo elemento por elemento
    ''' introduce un read-after-write —al calcular G ya se leyó una R pisada— y cambia el resultado. Como
    ''' <c>lo</c> y <c>hi</c> son multiplos de 4 y el vector avanza de a 8, el prologo y la cola cubren 0 ó 1
    ''' pixel ENTERO, nunca medio.</para></summary>
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Sub MsnBlendPixel(msnAcc As Single(), ovNorm As Single(), ovDiff As Single(), opa As Single, px As Integer)
        Dim cov As Single = ovDiff(px * 4 + 3) * opa
        If cov <= 0.0F Then Return
        If cov > 1.0F Then cov = 1.0F
        Dim hx = 2.0F * msnAcc(px * 4) - 1.0F, hy = 2.0F * msnAcc(px * 4 + 1) - 1.0F, hz = 2.0F * msnAcc(px * 4 + 2) - 1.0F
        Dim ox = 2.0F * ovNorm(px * 4) - 1.0F, oy = 2.0F * ovNorm(px * 4 + 1) - 1.0F, oz = 2.0F * ovNorm(px * 4 + 2) - 1.0F
        Dim nx = hx + cov * (ox - hx), ny = hy + cov * (oy - hy), nz = hz + cov * (oz - hz)
        Dim len = MathF.Sqrt(nx * nx + ny * ny + nz * nz)
        If len > 0.0000001F Then nx /= len : ny /= len : nz /= len
        msnAcc(px * 4) = (nx + 1.0F) * 0.5F
        msnAcc(px * 4 + 1) = (ny + 1.0F) * 0.5F
        msnAcc(px * 4 + 2) = (nz + 1.0F) * 0.5F
    End Sub

    ''' <summary>Un ELEMENTO del alpha-over de skee: la ley escalar, usada por el prologo y la cola del
    ''' cuerpo vectorial de arriba. Una sola definicion ⇒ el resultado no depende de donde corte la particion.</summary>
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Sub SkeeMaskOne(acc As Single(), tex As Single(), tr As Single, tg As Single, tb As Single,
                            opa As Single, e As Integer)
        Dim ch = e And 3
        If ch = 3 Then Return                                    ' el alpha no se toca
        Dim px = e >> 2
        Dim la = Clamp01(tex(px * 4 + 3) * opa)
        If la <= 0.0F Then Return
        Dim tint = If(ch = 0, tr, If(ch = 1, tg, tb))
        acc(e) = CSng((tex(e) * tint) * la + acc(e) * (1 - la))
    End Sub

    ''' <summary>Compose the FACE overlays' NORMAL maps into the head normal accumulator (MODEL-SPACE / MSN, in
    ''' place), in the SAME node-index order as the diffuse (Ovl0 bottom → OvlN top). Los normales NO se mezclan
    ''' como colores: se DECODIFICAN a vector [-1,1], se lerpean por cobertura, se RENORMALIZAN y se re-encodean —
    ''' así promediar dos normales no aplana la superficie. Espacio: el decal copia el flag MSN del head, así que
    ''' el normal del overlay se interpreta en el MISMO espacio que el head (sin conversión TS→MS). Cobertura =
    ''' alpha del DIFFUSE del overlay × opacidad (el decal blendea por el alpha del diffuse). Solo overlays con
    ''' <see cref="JslotOverlayNode.NormalPath"/>; los que no traen normal no tocan el _msn. Returns True si alguno
    ''' contribuyó. <paramref name="msnAcc"/> = head _msn decodificado RGBA [0,1] (length w*h*4).
    '''
    ''' <para>⭐ EL ALPHA NO SE MEZCLA, y eso MATCHEA a skee exactamente (no es una aproximación). skee reemplaza
    ''' el slot 1 del decal ENTERO —RGB y alpha— con el normal del overlay (OverlayInterface::InstallOverlay), pero
    ''' el decal hereda el flag MSN de la cabeza (:198-202), y en una malla MODEL-SPACE el mask especular sale del
    ''' SLOT 7 (t2, canal .r), que el decal COPIA de la cabeza (:214-233). O sea que el alpha del normal del overlay
    ''' no lo lee nadie tampoco in-game. Vale con o sin BC5: con BC5 ni siquiera existe.</para>
    '''
    ''' <para>⚠️ Lo que SÍ es aproximación es el RGB, y sólo en el medio: el motor dibuja DOS shapes y mezcla dos
    ''' resultados YA SOMBREADOS (alpha-over del decal sobre la cabeza), mientras que un bake tiene una sola
    ''' geometría y sombrea UNA entrada mezclada. Coinciden EXACTO en los extremos —cobertura 0 (queda el normal de
    ''' la cabeza) y cobertura 1 (queda el del overlay tal cual, que es lo que el decal muestra)— y divergen en el
    ''' medio porque el lighting no es lineal en N. No hay forma de cerrarlo sin una segunda geometría.</para>
    '''
    ''' <para>La COBERTURA sí es exacta: skee usa <c>iAlphaFlags=4845</c> (0x12ED = blend SRC_ALPHA/INV_SRC_ALPHA +
    ''' alpha-test GREATER) con <c>iAlphaThreshold=0</c> (main.cpp:130-133) ⇒ descarta sólo alpha==0 y mezcla suave
    ''' el resto — que es lo que hace el loop de abajo (<c>If cov &lt;= 0 Then Continue For</c> + lerp).</para></summary>
    Public Function ComposeFaceOverlayNormalsIntoMsn(msnAcc As Single(), overlays As IList(Of RaceMenuJslot.JslotOverlayNode),
                                                     w As Integer, h As Integer,
                                                     decode As Func(Of String, Integer, Integer, Single()),
                                                     Optional decodeNormal As Func(Of String, Integer, Integer, Single()) = Nothing) As Boolean
        If msnAcc Is Nothing OrElse overlays Is Nothing OrElse overlays.Count = 0 OrElse decode Is Nothing Then Return False
        ' El normal se lee con el decode VECTORIAL (reconstruye el eje Z de una fuente BC5/2-canales). Si el caller
        ' no lo pasa se cae al de color = comportamiento previo exacto, así que ningún call site queda roto.
        Dim decodeN = If(decodeNormal, decode)
        Dim npix = w * h
        Dim any = False
        ' Filtro = "puede aportar": nodo Face + normal que plegar + diffuse del que sacar la COBERTURA. Los tres
        ' términos están también en el gate (HasFaceOverlayNormals) ⇒ no pueden discrepar. El chequeo de adentro
        ' cubre el otro caso, el que el gate NO puede saber sin tocar disco: declarado pero ilegible = ERROR.
        Dim faceOrdered = SortFaceOverlays(overlays.
            Where(Function(o) IsFoldableFaceOverlay(o) AndAlso Not String.IsNullOrEmpty(o.NormalPath) AndAlso
                              Not String.IsNullOrEmpty(o.DiffusePath)).ToList())   ' predicado unico + orden skee
        For Each ov In faceOrdered
            ' Predicado ÚNICO con el gate (HasFaceOverlayNormals), y ANTES del decode. Ver ComposeFaceOverlaysIntoDiffuse.
            If Not OverlayIsVisible(ov) Then Continue For
            Dim ovNorm = decodeN(ov.NormalPath, w, h)
            If ovNorm Is Nothing OrElse ovNorm.Length < npix * 4 Then
                Dim npFail = ov.NormalPath
                Logger.LogLazy(Function() $"[SSE-OVL] normal de overlay SALTEADO: no se pudo leer/decodificar '{npFail}'")
                Continue For
            End If
            ' ⭐⭐ COBERTURA = ALPHA DEL DIFFUSE DEL OVERLAY × opacidad, y NADA MÁS. Es la ley del MOTOR: el overlay
            ' es una geometría clonada que se dibuja encima, y lo que la recorta es el alpha de su SLOT 0
            ' (OverlayInterface::InstallOverlay) — el normal vive en el slot 1 y no recorta nada.
            ' ⛔ ACÁ HABÍA UN FALLBACK AL ALPHA DEL PROPIO NORMAL, roto por partida doble: (1) no es lo que hace el
            ' motor, y (2) toda fuente sin alpha real —BC5 y BC1, o sea la mayoría de los normales— decodifica con
            ' A=1 CONSTANTE ⇒ cobertura plena en TODA la cara: el "tatuaje" se comía la cabeza entera.
            ' Un overlay SIN diffuse no tiene de dónde sacar cobertura y por eso se saltea — que es también lo que
            ' pasa in-game: sin diffuse propio el slot 0 del decal queda con la textura por defecto de skee (la de
            ' `sDefaultTexture`, en blanco), así que el overlay no se ve. MISMO predicado que
            ' ComposeFaceOverlaysIntoDiffuse, que ya filtraba por DiffusePath: los dos composers, una sola regla.
            Dim ovDiff = If(Not String.IsNullOrEmpty(ov.DiffusePath), decode(ov.DiffusePath, w, h), Nothing)
            If ovDiff Is Nothing OrElse ovDiff.Length < npix * 4 Then
                Dim cpFail = If(ov.DiffusePath, "(sin diffuse)")
                Logger.LogLazy(Function() $"[SSE-OVL] normal de overlay SALTEADO: sin COBERTURA legible (diffuse = '{cpFail}'). El alpha del propio normal NO se usa: no es la ley del motor y las fuentes de 2 canales lo devuelven constante = cara entera cubierta.")
                Continue For
            End If
            Dim opacity As Double = If(ov.HasAlpha, ov.Alpha, 1.0)
            Dim opa = CSng(opacity)   ' invariante del loop: se angosta una vez, no por pixel
            ' Paralelo por rangos (píxeles independientes ⇒ bit-idéntico); orden entre overlays = For Each serial.
            MsnBlendApply(msnAcc, ovNorm, ovDiff, opa, npix)
            any = True
        Next
        Return any
    End Function

    ''' <summary>True iff any FACE overlay carries a normal map the fold can actually consume (cheap check, no
    ''' decode): nodo Face + <see cref="JslotOverlayNode.NormalPath"/> + <see cref="JslotOverlayNode.DiffusePath"/>
    ''' + visible.
    ''' ⭐ La opacidad entra en el gate porque entra en el compose (<see cref="ComposeFaceOverlayNormalsIntoMsn"/>
    ''' saltea <c>opacity &lt;= 0</c>) — ver la nota de <see cref="HasBakeableFaceOverlays"/>.
    '''
    ''' <para>⭐ Y EL DIFFUSE TAMBIÉN, por la MISMA razón: es de su alpha que sale la COBERTURA del normal, así que
    ''' un overlay solo-normal no aporta nada y hacer que el gate dijera "sí" dejaba al compose sin hacer NADA — el
    ''' patrón que este archivo ya documenta dos veces como caro (en el bake hace entrar al camino plegado para
    ''' salir sin componer). Es además lo que hace el motor: sin diffuse propio el slot 0 del decal queda con la
    ''' textura por defecto de skee y el overlay no se ve in-game.</para></summary>
    Public Function HasFaceOverlayNormals(overlays As IList(Of RaceMenuJslot.JslotOverlayNode)) As Boolean
        If overlays Is Nothing Then Return False
        For Each ov In overlays
            If IsFoldableFaceOverlay(ov) AndAlso Not String.IsNullOrEmpty(ov.NormalPath) AndAlso
               Not String.IsNullOrEmpty(ov.DiffusePath) AndAlso OverlayIsVisible(ov) Then Return True
        Next
        Return False
    End Function

    ''' <summary>Opacidad efectiva del overlay (key8 <c>Alpha</c>, 1.0 si no la declara) &gt; 0. Predicado ÚNICO
    ''' compartido por los gates y por los dos composers, para que no puedan discrepar.</summary>
    Public Function OverlayIsVisible(ov As RaceMenuJslot.JslotOverlayNode) As Boolean
        If ov Is Nothing Then Return False
        Return If(ov.HasAlpha, ov.Alpha, 1.0) > 0.0
    End Function

    ''' <summary>⭐ THE canonical "is this overlay on the head?" test. EVERY path — CPU bake, GPU bake, the folded
    ''' and non-folded variants, the live render, and the Papyrus apply-script emitter — must agree on this one
    ''' predicate, or an overlay ends up composited twice or not at all.
    '''
    ''' <para>It exists because they did NOT agree: <c>BuildFaceOverlayGpuLayers</c> filtered on "has a diffuse"
    ''' and forgot the node check entirely, so the GPU bake path composited BODY tattoos into the FACE texture
    ''' while the CPU path did not.</para></summary>
    Public Function IsFaceOverlay(ov As RaceMenuJslot.JslotOverlayNode) As Boolean
        Return ov IsNot Nothing AndAlso IsFaceOverlayNodeName(ov.NodeName)
    End Function

    ''' <summary>El MISMO test, por nombre de nodo — para los call sites que sólo tienen el string (el emisor del
    ''' script Papyrus, el ruteo de shapes del render). Que exista una sola implementación es el punto entero.
    ''' <para>Cubre las DOS familias de la cara: <c>Face [Ovl{n}]</c> y <c>Face [SOvl{n}]</c>. Es ruteo de
    ''' GEOMETRÍA (¿va en la cabeza?), no de mecanismo — el mecanismo lo decide
    ''' <see cref="IsSpellOverlayNodeName"/>.</para></summary>
    Public Function IsFaceOverlayNodeName(nodeName As String) As Boolean
        Return Not String.IsNullOrEmpty(nodeName) AndAlso
               nodeName.TrimStart().StartsWith("Face", StringComparison.OrdinalIgnoreCase)
    End Function

    ''' <summary>⭐ THE canonical "is this the MAGIC (spell) pool?" test — el nodo <c>… [SOvl{n}]</c> en vez de
    ''' <c>… [Ovl{n}]</c>. skee64 mantiene DOS pools por zona, cada uno con su propio contador
    ''' (<c>g_numSpell*Overlays</c> / key <c>iSpellOverlays</c>, main.cpp:775-781, default 1) y su propia malla
    ''' plantilla (<c>*_magicoverlay.nif</c> vs <c>*_overlay.nif</c>, OverlayInterface.h:23-46).
    '''
    ''' <para>⭐⭐ QUÉ CAMBIA DE VERDAD ENTRE LOS DOS POOLS — MEDIDO sobre los 8 NIF de <c>RaceMenu.bsa</c>
    ''' (parseo de bloques, 2026-08-10), no razonado: los dos son <c>NiNode → NiTriShape +
    ''' BSLightingShaderProperty + BSShaderTextureSet + NiAlphaProperty</c> con el MISMO <c>default.dds</c>; el magic
    ''' agrega <c>BSEffectShaderPropertyFloatController + NiFloatInterpolator + NiFloatData</c>, y los campos del
    ''' controller son: <c>typeOfControlledVariable = 5</c> (=<b>Alpha</b>), <c>target</c> = el
    ''' <c>BSLightingShaderProperty</c>, <c>flags = 0x4A</c> = <b>ACTIVE + CYCLE_REVERSE</b>, <c>frequency = 8</c>,
    ''' <c>startTime = 0</c>, <c>stopTime = 10</c>, y 2 keys LINEALES <c>(t=0, v=0) → (t=10, v=1)</c>.
    ''' <para>⇒ La OPACIDAD del pool magic la maneja el motor: <b>pulsa 0↔1</b> mientras el controller corre. No
    ''' "arranca apagada" (eso era una inferencia sobre la primera key) y nuestro <c>KEY_ALPHA</c> autorado lo pisa el
    ''' controller mientras anima.</para>
    ''' Y la GEOMETRÍA no sale del NIF en ninguno de los dos: <c>InstallOverlay</c> descarta la shape del archivo
    ''' y crea una BSTriShape nueva copiando <c>vertexDesc</c>, los vértices dinámicos, <c>m_localTransform</c> y
    ''' <c>m_spSkinInstance</c> DE LA PIEL del actor (OverlayInterface.cpp:137-186); del NIF sólo sobreviven el
    ''' shader/alpha property, y encima <c>g_overlayAlphaOverride</c>+<c>g_overlayForceDecal</c> (defaults true)
    ''' reescriben alpha flags y fuerzan el flag Decal en AMBOS.</para>
    '''
    ''' <para>⇒ Dibujar un spell overlay con el MISMO decal coplanar alpha-over que uno normal es FIEL en
    ''' geometría, UV, skinning, blend y decal. Lo único que no se replica es el controller — y es un valor que
    ''' depende del TIEMPO, o sea que ningún cuadro estático puede mostrarlo "bien": el preview lo muestra a su
    ''' alpha autorada = el PICO del ciclo, que es lo que el autor necesita ver. Que la opacidad real sea un pulso y
    ''' no un estado es justamente por qué el preview PRINCIPAL no los dibuja.</para></summary>
    Public Function IsSpellOverlayNodeName(nodeName As String) As Boolean
        If String.IsNullOrEmpty(nodeName) Then Return False
        ' El corchete es lo que distingue: "[SOvl" vs "[Ovl". Buscar "[SOvl" y no "SOvl" a secas evita que un
        ' nodo ajeno llamado "…SOvl…" sin corchete se confunda con el pool.
        Return nodeName.IndexOf("[SOvl", StringComparison.OrdinalIgnoreCase) >= 0
    End Function

    ''' <summary>El mismo test sobre el overlay. Ver <see cref="IsSpellOverlayNodeName"/>.</summary>
    Public Function IsSpellOverlay(ov As RaceMenuJslot.JslotOverlayNode) As Boolean
        Return ov IsNot Nothing AndAlso IsSpellOverlayNodeName(ov.NodeName)
    End Function

    ''' <summary>Posición del <c>[</c> que abre el tag de overlay (<c>[Ovl</c> o <c>[SOvl</c>), o −1.
    ''' <para>Se exige el CORCHETE y no sólo las letras: sin él, un nodo ajeno que contuviera "Ovl" en el nombre
    ''' pasaría por overlay. Es la única función que sabe cómo se escribe el tag, y de ella dependen el índice
    ''' (<see cref="ParseOvlIndex"/>) y el orden (<see cref="CompositeOrderKey"/>).</para></summary>
    ''' <remarks>Prefiere <c>[Ovl</c> sobre <c>[SOvl</c> si un nombre trajera los DOS tags (daría el índice del
    ''' pool normal con <c>IsSpell=True</c>). Inalcanzable con el dato real: el decoder del <c>.jslot</c> exige
    ''' <c>^(Body|Hands|Feet|Face) \[S?Ovl\d+\]$</c>, o sea UN tag y nada más.</remarks>
    Private Function FindOvlTagStart(nodeName As String) As Integer
        Dim k = nodeName.IndexOf("[Ovl", StringComparison.OrdinalIgnoreCase)
        If k >= 0 Then Return k
        Return nodeName.IndexOf("[SOvl", StringComparison.OrdinalIgnoreCase)
    End Function

    ''' <summary>⭐⭐ LA MEMBRESÍA DEL FOLD: overlay de cara que el pliegue (render Y bake) se queda.
    ''' <para>Es <see cref="IsFaceOverlay"/> MENOS el pool magic, y la razón es de MECANISMO, no de gusto: plegar
    ''' es escribir el overlay DENTRO del diffuse de la cabeza, o sea volverlo permanente e incondicional. Un
    ''' <c>Face [SOvl{n}]</c> es la capa que un magic effect prende y apaga en runtime (su plantilla trae un
    ''' controller que PULSA su alpha 0↔1; ver <see cref="IsSpellOverlayNodeName"/>): horneada, quedaría prendida para siempre y el
    ''' efecto del mod la volvería a pintar ENCIMA. Por eso la ley es UNA y sin solape:</para>
    ''' <para>  cara NO-magic ⇒ SÓLO fold (nunca decal vivo) · cara magic ⇒ SÓLO decal vivo (nunca fold).</para>
    ''' <para>Vive acá, en el mismo archivo que los gates y los dos composers, para que ninguno pueda discrepar:
    ''' los composers filtran por este predicado, así que aunque un caller pase la lista sin filtrar, un spell
    ''' overlay NO PUEDE terminar plegado.</para></summary>
    Public Function IsFoldableFaceOverlay(ov As RaceMenuJslot.JslotOverlayNode) As Boolean
        Return IsFaceOverlay(ov) AndAlso Not IsSpellOverlay(ov)
    End Function

    ''' <summary>⭐ EL ORDEN DE COMPOSICIÓN DE skee, como clave única y ordenable.
    ''' <para><c>SetupOverlay</c> corre el loop del pool PRIMARIO completo y DESPUÉS el del secundario
    ''' (OverlayInterface.cpp:659-668), y cada <c>InstallOverlay</c> termina en <c>AttachChild</c> (:257) ⇒ el
    ''' orden de dibujo es: <b>TODOS los <c>[Ovl]</c> ascendentes, y encima TODOS los <c>[SOvl]</c>
    ''' ascendentes</b>. NO es un único índice compartido.</para>
    ''' <para>⛔ Ordenar por <see cref="ParseOvlIndex"/> a secas —que saltea la <c>S</c>— empataba
    ''' <c>[SOvl0]</c> con <c>[Ovl0]</c> y podía dejar el spell DEBAJO de <c>[Ovl1]</c>. Con el offset, el pool
    ''' magic siempre queda arriba, que es lo que hace el motor.</para>
    ''' <para>⛔ EL OFFSET NO PUEDE SER "UN NÚMERO QUE ALCANCE". Era 1000, justificado con "skee clampea todo
    ''' contador a 0x7F", y eso confunde lo que el MOTOR instancia con lo que el DATO puede traer: el decoder del
    ''' <c>.jslot</c> acepta <c>\[S?Ovl\d+\]</c> sin techo (y el barrido del apply-script existe justamente porque un
    ''' preset importado puede traer un <c>[SOvl40]</c>), así que un <c>Body [Ovl2000]</c> ordenaba ARRIBA de un
    ''' <c>Body [SOvl0]</c> — rompiendo el único invariante que esta función existe para sostener. Y sumarle 1000 a un
    ''' índice cercano a <c>Integer.MaxValue</c> DESBORDA: VB chequea overflow por default, así que era una excepción
    ''' dentro del <c>OrderBy</c> del render, en un Task en background.
    ''' <para>Solución: el índice se SATURA en un rango acotado y el pool aporta un offset que ese rango no puede
    ''' alcanzar. Así el pool domina SIEMPRE, el orden dentro del pool se preserva para cualquier índice
    ''' representable, y no hay suma que pueda desbordar.</para></para></summary>
    Public Function CompositeOrderKey(nodeName As String) As Integer
        Dim idx = ParseOvlIndex(nodeName)
        ' ⚠️ El índice literal 2147483647 ES el centinela, o sea indistinguible de "sin índice" ya desde
        ' ParseOvlIndex. Inalcanzable con dato real (skee clampea a 127) y sin consecuencia (los dos ordenan
        ' último y el OrderBy es estable), pero queda dicho: el dominio no está perfectamente separado.
        If idx = Integer.MaxValue Then Return Integer.MaxValue   ' sin índice = al final, como antes
        Dim bounded = Math.Max(0, Math.Min(idx, PoolOrderOffset - 1))
        Return If(IsSpellOverlayNodeName(nodeName), PoolOrderOffset, 0) + bounded
    End Function

    ''' <summary>Offset del pool magic en <see cref="CompositeOrderKey"/>: <c>Integer.MaxValue \ 2</c>.
    ''' <para>⛔ ERA <c>1 &lt;&lt; 30</c>, (este comentario decia que eso es <c>MaxValue + 1</c> y es FALSO: es <c>MaxValue \ 2 + 1</c> — la
    ''' conclusion de abajo era correcta, la cuenta escrita no), y ese +1 hacía que el extremo del pool magic
    ''' (<c>offset + offset-1</c>) diera EXACTAMENTE <c>Integer.MaxValue</c> — el centinela de "sin índice, al
    ''' final". Efecto observable nulo (los dos ordenan último y OrderBy es estable), pero es una colisión de
    ''' dominios: el valor que significa "no tiene índice" pasaba a ser también un índice válido. Con MaxValue
    ''' el tope del magic queda en MaxValue-2 y el centinela vuelve a ser inalcanzable.</para></summary>
    Private ReadOnly PoolOrderOffset As Integer = Integer.MaxValue \ 2

    ''' <summary>The FACE overlays the FOLD owns — node filter only, no texture requirement.
    ''' Callers pass THIS to the composers; each composer then keeps what it can actually consume (the diffuse
    ''' composer wants <c>DiffusePath</c>, the normal composer wants <c>NormalPath</c>). Filtering on a texture
    ''' at the CALLER is what dropped normal-only face overlays.
    ''' <para>⛔ EXCLUYE el pool magic (<see cref="IsFoldableFaceOverlay"/>): un <c>Face [SOvl{n}]</c> NO se
    ''' hornea nunca — se dibuja como decal vivo y viaja por el apply-script. Ver la ley completa en
    ''' <see cref="IsFoldableFaceOverlay"/>.</para></summary>
    Public Function FaceOverlaysOnly(overlays As IList(Of RaceMenuJslot.JslotOverlayNode)) As List(Of RaceMenuJslot.JslotOverlayNode)
        If overlays Is Nothing Then Return New List(Of RaceMenuJslot.JslotOverlayNode)()
        Return overlays.Where(AddressOf IsFoldableFaceOverlay).ToList()
    End Function

    ''' <summary>True iff <paramref name="overlays"/> has at least one FACE overlay with a diffuse texture AND
    ''' non-zero opacity — i.e. whether <see cref="ComposeFaceOverlaysIntoDiffuse"/> would emit anything.
    '''
    ''' <para>⭐ LA OPACIDAD ENTRA EN EL GATE. Antes el gate sólo exigía <c>DiffusePath</c> mientras el compose
    ''' además saltea <c>opacity &lt;= 0</c>: un overlay con opacidad 0 hacía que el gate dijera SÍ y el compose
    ''' no hiciera NADA. Eso no era inerte — el bake entraba al camino plegado, salía por el return de
    ''' <c>WriteSseFaceDiffuseWithOverlays</c> sin componer, y de paso se saltaba el borrado de los artefactos
    ''' del fold anterior. Las dos condiciones que el gate NO puede replicar sin tocar disco (que la textura
    ''' exista y decodifique) quedan como error REPORTADO, no como salida silenciosa.</para></summary>
    Public Function HasBakeableFaceOverlays(overlays As IList(Of RaceMenuJslot.JslotOverlayNode)) As Boolean
        If overlays Is Nothing Then Return False
        For Each ov In overlays
            If IsFoldableFaceOverlay(ov) AndAlso Not String.IsNullOrEmpty(ov.DiffusePath) AndAlso OverlayIsVisible(ov) Then Return True
        Next
        Return False
    End Function

    ''' <summary>⭐ The gate EVERY bake path must use: is there ANY face overlay the bake can fold — diffuse o
    ''' normal?
    ''' <para>⛔ ESTA NOTA DECÍA que un overlay solo-normal era legal porque el compose usaba "el alpha del propio
    ''' normal como cobertura". Ese fallback SE ELIMINÓ: no es la ley del motor (la cobertura sale del slot 0 del
    ''' decal, nunca del normal) y encima estaba roto — una fuente de 2 canales decodifica con A=1 constante, así
    ''' que cubría la cara ENTERA. Hoy los dos términos exigen diffuse, con lo cual esta función es equivalente a
    ''' <see cref="HasBakeableFaceOverlays"/>; se conserva como el nombre que expresa la intención en los call
    ''' sites del bake y del render, y para que el día que aparezca una fuente de cobertura legítima para el
    ''' normal el cambio sea de UNA línea acá.</para></summary>
    Public Function HasAnyFoldableFaceOverlay(overlays As IList(Of RaceMenuJslot.JslotOverlayNode)) As Boolean
        Return HasBakeableFaceOverlays(overlays) OrElse HasFaceOverlayNormals(overlays)
    End Function

    ''' <summary>skee's colour presets (TintMaskInterface.h): a MASKC / TintData colour stored as this raw
    ''' SInt32 is NOT a literal colour but a live-NPC colour reference. −2 = the NPC skin colour, −1 = the NPC
    ''' hair colour (hair channels ×2, clamped — CreateTintsFromData:59-61). As unsigned: 0xFFFFFFFE / 0xFFFFFFFF.</summary>
    Public Const SkeePresetSkin As UInteger = &HFFFFFFFEUI
    Public Const SkeePresetHair As UInteger = &HFFFFFFFFUI

    ''' <summary>Build ONE skee GPU-compositor layer (TintMaskInterface / CDXNifTextureRenderer) as an
    ''' <see cref="SseOverlay"/> ready for <see cref="ApplyOverlays"/>. This is the skee analogue of the vanilla
    ''' facetint: MASKT (texture) + MASKC (colour, ARGB with A=opacity) + MASKA (alpha) per index, or a TintData
    ''' XML mask. <paramref name="colorArgbOrPreset"/> is the raw MASKC/TintData colour — if it equals
    ''' <see cref="SkeePresetSkin"/>/<see cref="SkeePresetHair"/> the live NPC skin/hair colour is substituted
    ''' (<paramref name="skinRgb"/>/<paramref name="hairRgb"/>, hair ×2 clamped). <paramref name="opacity"/> is
    ''' MASKA (skee folds it into the colour's A byte). <paramref name="layerType"/> 0=Normal/1=Mask/2=Color;
    ''' <paramref name="blend"/> the technique (default normal). <paramref name="texRgba"/> = decoded mask texture
    ''' (RGBA de almacenamiento [0,1], w*h*4) or Nothing for a type-2 solid layer.</summary>
    ''' <param name="hasColor">False = la capa NO declara color (p.ej. un MASKC ausente en el NIF). ⭐ NO es lo
    ''' mismo que "el color vale 0xFFFFFFFF": ese valor ES <see cref="SkeePresetHair"/>, el sentinel de skee para
    ''' "usar el color de pelo del NPC". Usarlo como default de "sin dato" hacía que una capa sin MASKC entrara por
    ''' la resolución de presets; y como los dos callers pasan <c>hairRgb = Nothing</c>, caía al decode literal del
    ''' 0xFFFFFFFF ⇒ BLANCO opaco pintado con la cobertura de la máscara. Con hasColor=False se saltea la
    ''' resolución de sentinels y se usa blanco DIRECTO — el mismo valor de antes (no hay fuente RE para otro),
    ''' pero por la rama correcta y sin poder confundirse con un preset.</param>
    Public Function BuildSkeeMaskLayer(colorArgbOrPreset As UInteger, opacity As Double, texRgba As Single(),
                                       layerType As Integer, blend As SseBlendMode,
                                       skinRgb As Double(), hairRgb As Double(),
                                       Optional hasColor As Boolean = True) As SseOverlay
        Dim r As Double, g As Double, b As Double
        If Not hasColor Then
            ' "Esta capa no trae color" ⇒ blanco (neutro multiplicativo). NUNCA pasa por los sentinels.
            r = 1.0 : g = 1.0 : b = 1.0
        ElseIf colorArgbOrPreset = SkeePresetSkin AndAlso skinRgb IsNot Nothing AndAlso skinRgb.Length >= 3 Then
            r = skinRgb(0) : g = skinRgb(1) : b = skinRgb(2)
        ElseIf colorArgbOrPreset = SkeePresetHair AndAlso hairRgb IsNot Nothing AndAlso hairRgb.Length >= 3 Then
            r = Clamp01Dbl(hairRgb(0) * 2.0) : g = Clamp01Dbl(hairRgb(1) * 2.0) : b = Clamp01Dbl(hairRgb(2) * 2.0)   ' skee ×2 clamp
        Else
            ' ARGB byte order (skee SetColorA: A<<24|R<<16|G<<8|B). RGB from bits 16/8/0.
            r = CDbl((colorArgbOrPreset >> 16) And &HFF) / 255.0
            g = CDbl((colorArgbOrPreset >> 8) And &HFF) / 255.0
            b = CDbl(colorArgbOrPreset And &HFF) / 255.0
        End If
        Return New SseOverlay With {
            .BlendMode = blend,
            .LayerType = layerType,
            .Color = New Double() {r, g, b, Clamp01Dbl(opacity)},
            .Texture = texRgba}
    End Function

    ''' <summary>Índice del nodo <c>[Ovl{n}]</c> / <c>[SOvl{n}]</c> (n entero) o Integer.MaxValue si no matchea
    ''' (los sin índice van al final). Es el ÍNDICE DENTRO DE SU POOL; el orden total entre pools lo da
    ''' <see cref="CompositeOrderKey"/>.
    ''' <para>⛔⛔ ESTO ESTABA ROTO PARA EL POOL MAGIC, y el comentario afirmaba lo contrario ("salta '[Ovl' o
    ''' '[SOvl'"). El buscado era el literal <c>"[Ovl"</c>, que NO es substring de <c>"[SOvl0]"</c> —entre el
    ''' corchete y la <c>O</c> hay una <c>S</c>— así que TODO nodo <c>[SOvl{n}]</c> caía en el
    ''' <c>open &lt; 0</c> y devolvía MaxValue. Consecuencias medidas por el gate: el índice del pool magic era
    ''' siempre −1/MaxValue, con lo cual (a) el orden dentro del pool magic era arbitrario, (b) buscar el primer
    ''' slot libre daba 0 siempre —dos overlays magic autorados en el MISMO nodo, uno pisando al otro— y (c) el
    ''' Up/Down no podía reordenarlos. El "salta hasta el primer dígito" de abajo era correcto y nunca se
    ''' ejecutaba para el caso que decía cubrir.</para></summary>
    Public Function ParseOvlIndex(nodeName As String) As Integer
        If String.IsNullOrEmpty(nodeName) Then Return Integer.MaxValue
        Dim open = FindOvlTagStart(nodeName)
        If open < 0 Then Return Integer.MaxValue
        Dim close = nodeName.IndexOf("]"c, open)
        If close < 0 Then Return Integer.MaxValue
        ' Salta el tag ("Ovl" o "SOvl") hasta el primer dígito. Arranca en open+1 (no en open+4) para no asumir
        ' el largo del tag: es lo que hace que los dos pools entren por el mismo camino.
        Dim i = open + 1
        While i < close AndAlso Not Char.IsDigit(nodeName(i))
            i += 1
        End While
        Dim digits = nodeName.Substring(i, close - i)
        Dim n As Integer
        Return If(Integer.TryParse(digits, n), n, Integer.MaxValue)
    End Function

    ''' <summary>Clamp en Double para los DATOS (color de capa, opacidad del jslot). El de Single es para
    ''' la math por pixel.</summary>
    Private Function Clamp01Dbl(v As Double) As Double
        Return If(v < 0.0, 0.0, If(v > 1.0, 1.0, v))
    End Function

    Private Function Clamp01(v As Single) As Single
        Return If(v < 0.0F, 0.0F, If(v > 1.0F, 1.0F, v))
    End Function

    Private Function RgbToHsv(r As Single, g As Single, b As Single) As Single()
        Dim mx = MathF.Max(r, MathF.Max(g, b)), mn = MathF.Min(r, MathF.Min(g, b)), d = mx - mn
        Dim h As Single = 0.0F
        If d > 0.0000001F Then
            If mx = r Then h = ((g - b) / d) Mod 6.0F Else If mx = g Then h = (b - r) / d + 2.0F Else h = (r - g) / d + 4.0F
            h /= 6.0F : If h < 0.0F Then h += 1.0F
        End If
        Return New Single() {h, If(mx <= 0.0F, 0.0F, d / mx), mx}
    End Function

    Private Function HsvToRgb(h As Single, s As Single, v As Single) As Single()
        Dim r = Clamp01(MathF.Abs(((h * 6.0F + 0.0F) Mod 6.0F) - 3.0F) - 1.0F)
        Dim g = Clamp01(MathF.Abs(((h * 6.0F + 4.0F) Mod 6.0F) - 3.0F) - 1.0F)
        Dim b = Clamp01(MathF.Abs(((h * 6.0F + 2.0F) Mod 6.0F) - 3.0F) - 1.0F)
        Return New Single() {v * (1.0F + s * (r - 1.0F)), v * (1.0F + s * (g - 1.0F)), v * (1.0F + s * (b - 1.0F))}
    End Function

End Module
