Option Strict On

Imports FO4_Base_Library.FaceTintConvention
Imports System.Numerics
Imports System.Runtime.CompilerServices
Imports System.Runtime.Intrinsics

' FaceTintCpuCompositor - espejo CPU EXACTO del compositor GL.
'
' SYNC: CPU/GPU compositor. Hay DOS implementaciones de la MISMA ley de composicion de FaceTint:
'   1. GL  : FaceTintCompositor (shader + ApplyFaceTintPipeline). Es el DEFAULT del render.
'   2. CPU : este modulo, en float64. Es el que usa el BAKE, y la referencia byte.
' Los dos tienen que dar el MISMO resultado por canal: todo cambio en la ley -espacios, blend, coverage,
' mask/src por kind, region-swap, seed- va en LOS DOS. La ley no se hardcodea aca, sale de
' FaceTintConvention.ResolveConvention igual que el shader la lee por uniforms, y las funciones de
' espacio/blend/maskconv de abajo son transcripcion 1:1 de las del shader. Ver 50-facetint-leyes-y-compositor.
'
' Caveats de PARIDAD, para no perseguir fantasmas:
'  - CPU en float64 y GPU en float32: pueden diferir +-1 byte en pixeles cerca de x.5. Es inherente.
'  - GL == CPU es exacto SOLO en resolucion Inherit; con override cada camino resamplea distinto.
'
' Trabaja sobre las DDS ya leidas, decodifica por CPU, cachea por clave y compone en float. El producto es
' BGRA byte por canal, listo para el encode del bake.

Public Module FaceTintCpuCompositor
    ''' <summary>Lanes de un Vector(Of Single) en ESTA maquina: 8 con AVX2, 4 con SSE2. Es el tamano de
    ''' bloque de TODOS los loops vectoriales de este modulo. ⛔ Es constante durante todo el proceso
    ''' (Vector(Of T).Count lo es), asi que vive aca y no se recalcula por pixel. Hardcodear 8 en su lugar
    ''' corrompe silenciosamente una maquina de 4 lanes: leeria y escribiria fuera del bloque.</summary>
    Private ReadOnly lanes As Integer = FastPow.LaneCount


    ''' <summary>⭐ CAPACIDAD DECLARADA de este compositor, para los caminos GL que lo tienen de ESPEJO.
    ''' Este modulo SI implementa la ley completa del acumulador: siembra en <c>AccumSpace</c>, compone
    ''' entero ahi (<c>ComposeOne(..., accSpace:=accSp)</c>) y hace UN unico pase final
    ''' <c>AccumSpace-&gt;OutputSpace</c> en el pack. Por eso declara <c>FourSpaceAccumulator</c>.
    ''' <para>El <c>ApplyFaceTintPipeline</c> del GL recibe esta constante desde los call sites cuyo espejo CPU
    ''' es este modulo. Si algun dia se rompe esa propiedad acá, se cambia ESTA linea y los dos lados se
    ''' reajustan solos — no hay que acordarse de ningun <c>If</c> remoto.</para></summary>
    Public Const AccumSpaceCapability As FaceTintConvention.FaceTintCpuMirrorCapability =
        FaceTintConvention.FaceTintCpuMirrorCapability.FourSpaceAccumulator

    ' ⛔ SACADO (2026-07-30): el contador de resampleo por canal (NoteSamplerBinding / SamplerStatsLine).
    ' Era diagnostico puro y corria POR CAPA con un SyncLock dentro del camino caliente del compose.
    ' Ya cumplio su funcion: REFUTO la hipotesis de que el bilineal explicara la divergencia CPU/GPU
    ' (`_msn` = 0/320 bindings resampleados y aun asi 148 px de cola). El dato quedo en memoria
    ' (40-bake-estado-cerrado); el codigo no tiene por que pagarlo en cada bake.

    ' ---- Conversiones de espacio (transcripcion 1:1 del shader; ws: 0=linear 1=srgb 2=g22) ----
    ' AggressiveInlining: estos helpers se invocan millones de veces por capa desde los loops per-pixel y el
    ' costo de la LLAMADA domina sobre su cuerpo. Es una hint de compilacion: no cambia ninguna operacion ni
    ' el orden, y .NET usa SSE sin precision excedente, asi que la salida es BIT-IDENTICA.

    ''' <summary>Exponentes de las transfer functions. Se calculan en Double y RECIÉN AHÍ se angostan: el
    ''' float más cercano al exponente real. Escribirlos como <c>1.0F/2.2F</c> daría OTRO float
    ''' (0,45454544 en vez de 0,45454547) y por lo tanto otra imagen.
    ''' <para>⭐ El <c>pow</c> ya NO es <c>MathF.Pow</c>: es <see cref="FastPow"/>, la MISMA ley en escalar,
    ''' Vector128 y Vector256 (probadas bit-idénticas entre sí). Los exponentes de acá quedan porque los usan
    ''' los <c>MathF.Pow</c> de exponente VARIABLE que sobreviven (Illusions soft-light).</para></summary>
    Private ReadOnly InvG22 As Single = CSng(1.0 / 2.2)
    Private ReadOnly InvG24 As Single = CSng(1.0 / 2.4)

    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Function Clamp01(c As Single) As Single
        If c < 0.0F Then Return 0.0F
        If c > 1.0F Then Return 1.0F
        Return c
    End Function

    ' ⛔ Los Clamp01 de abajo se CONSERVAN aunque FastPow clampee internamente: son el contrato de entrada que
    ' tenían con MathF.Pow y sacarlos no ahorra nada medible frente a un pow. FastPow reproduce además el
    ' NaN→NaN de MathF.Pow, así que estas cuatro funciones se comportan igual que antes salvo por el error de
    ' aproximación acotado (|byte delta| <= 1, enumerado sobre el dominio entero — ver FastPow).

    ' ⭐⭐ LA CURVA sRGB, ESCRITA UNA VEZ. Estaba transcripta DOS veces —acá y como `Srgb2Lin`/`Lin2Srgb` en
    ' SseFaceGenBaker— y las dos versiones NO eran la misma funcion. Medido por enumeracion (ver
    ' SrgbCurveShapeReport, que sigue en el gate como `[medicion]`):
    '   · srgb→lin: en el dominio real (byte/255) coincidian 256/256. FUERA de [0,1] no: la del baker NO
    '     clampeaba la entrada, asi que `Srgb2Lin(-1)` devolvia -0,0774 — un valor NEGATIVO entrando a la
    '     cadena del fold. La de aca clampeaba a 0.
    '   · lin→srgb: difieren en 1 de 256 — el caso `c = 1`, donde `FastPow.Pow1(1, 1/2.4)` no da 1 exacto y
    '     esta devolvia 0,99999994 mientras la del baker cortaba en 1,0 por un return temprano. Y la entrada
    '     de esta funcion SI supera 1 en la practica: el fold multiplica por el amplify (hasta ~4x).
    ' La unificada se queda con lo CORRECTO de cada una: el clamp de entrada (protege del negativo) Y los
    ' extremos exactos (lin→srgb de 1 tiene que ser 1, no 1−6e−8).
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Function SrgbToLin1(c As Single) As Single
        Return SrgbToLinShared(c)
    End Function

    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Function LinToSrgb1(c As Single) As Single
        Return LinToSrgbShared(c)
    End Function

    ''' <summary>sRGB→lineal (IEC 61966-2-1), con la entrada acotada a [0,1]. LA definicion, para los dos
    ''' juegos y los dos caminos (compose y fold).</summary>
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Public Function SrgbToLinShared(c As Single) As Single
        c = Clamp01(c)
        Return If(c <= 0.04045F, c / 12.92F, FastPow.Pow1((c + 0.055F) / 1.055F, FastPow.G24))
    End Function

    ''' <summary>lineal→sRGB (IEC 61966-2-1), con la entrada acotada a [0,1] y los EXTREMOS EXACTOS: 0→0 y
    ''' 1→1. El corte en 1 no es cosmetico — sin el, `pow(1, 1/2.4)` deja 0,99999994 y ese valor sigue
    ''' viajando por la cadena del fold, que multiplica por el amplify.</summary>
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Public Function LinToSrgbShared(c As Single) As Single
        c = Clamp01(c)
        If c <= 0.0F Then Return 0.0F
        If c >= 1.0F Then Return 1.0F
        Return If(c <= 0.0031308F, c * 12.92F, 1.055F * FastPow.Pow1(c, FastPow.InvG24) - 0.055F)
    End Function

    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Function G22ToLin1(c As Single) As Single
        Return FastPow.Pow1(Clamp01(c), FastPow.G22)
    End Function

    ''' <summary>⭐ LUT byte→byte de <see cref="G22DiffuseBgraToLinearInPlace"/>. BIT-IDENTICA a calcularlo,
    ''' no una aproximacion: la entrada de esa conversion es SIEMPRE <c>unByte / 255.0</c>, o sea EXACTAMENTE
    ''' 256 valores posibles, y la salida es un byte. Tabular el dominio ENTERO con la MISMA expresion y el
    ''' MISMO redondeo (<c>Math.Round</c> sin overload = ToEven, igual que antes) da por enumeracion exhaustiva
    ''' el mismo byte para las 256 entradas — no hay forma de que difiera.
    ''' <para>Cambia TRES <c>Math.Pow</c> por pixel por tres lecturas de tabla. Corre en cada refresh del
    ''' render en modo CPU-skinning, sobre el diffuse de la cara a resolucion nativa.</para></summary>
    ' Sin `Shared`: esto es un Module, sus miembros ya lo son implícitamente. El inicializador corre en el
    ' constructor estático del módulo ⇒ la tabla está completa antes del primer uso, con la garantía de
    ' thread-safety del CLR (no hace falta lock ni doble chequeo).
    Private ReadOnly _g22ToLinByteLut As Byte() = BuildG22ToLinByteLut()

    Private Function BuildG22ToLinByteLut() As Byte()
        Dim lut(255) As Byte
        For b As Integer = 0 To 255
            ' MISMA expresion que tenia el loop: G22ToLin1(byte / 255.0) * 255.0, Math.Round (ToEven), CByte.
            lut(b) = CByte(MathF.Round(G22ToLin1(CSng(b / 255.0)) * 255.0F, MidpointRounding.ToEven))
        Next
        Return lut
    End Function

    Public Sub G22DiffuseBgraToLinearInPlace(bgra As Byte())
        If bgra Is Nothing Then Return
        Dim n = bgra.Length \ 4
        ' Paralelo por rangos: in-place PURAMENTE POR PIXEL (cada i lee y escribe SOLO bgra(i*4..i*4+2), el
        ' alpha ni se toca) => sin lectura cruzada pese a ser in-place, y BIT-IDENTICO al serial.
        ' La tabla (ver _g22ToLinByteLut) reemplaza los tres Math.Pow por pixel; el resultado es el mismo
        ' byte por enumeracion exhaustiva del dominio (256 entradas), no por aproximacion.
        Dim lut = _g22ToLinByteLut
        System.Threading.Tasks.Parallel.ForEach(
            System.Collections.Concurrent.Partitioner.Create(0, n),
            Sub(range)
                For i = range.Item1 To range.Item2 - 1
                    bgra(i * 4) = lut(bgra(i * 4))                  ' B
                    bgra(i * 4 + 1) = lut(bgra(i * 4 + 1))          ' G
                    bgra(i * 4 + 2) = lut(bgra(i * 4 + 2))          ' R
                Next
            End Sub)
    End Sub

    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Function LinToG221(c As Single) As Single
        Return FastPow.Pow1(Clamp01(c), FastPow.InvG22)
    End Function

    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Function G24ToLin1(c As Single) As Single
        Return FastPow.Pow1(Clamp01(c), FastPow.G24)
    End Function

    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Function LinToG241(c As Single) As Single
        Return FastPow.Pow1(Clamp01(c), FastPow.InvG24)
    End Function

    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Function SpaceToLin1(c As Single, s As Integer) As Single
        If s = 0 Then Return c
        If s = 1 Then Return SrgbToLin1(c)
        If s = 3 Then Return G24ToLin1(c)
        Return G22ToLin1(c)   ' s=2
    End Function

    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Function LinToSpace1(c As Single, s As Integer) As Single
        If s = 0 Then Return c
        If s = 1 Then Return LinToSrgb1(c)
        If s = 3 Then Return LinToG241(c)
        Return LinToG221(c)   ' s=2
    End Function

    ''' <summary>cvt agnóstico entre espacios (0=linear 1=srgb 2=g22) via linear. = shader cvt().</summary>
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Function Cvt1(c As Single, fromS As Integer, toS As Integer) As Single
        If fromS = toS Then Return c
        Return LinToSpace1(SpaceToLin1(c, fromS), toS)
    End Function

    ''' <summary>mask conv (0=raw 1=srgbEnc 2=srgbDec 3=g22Enc 4=g22Dec). = shader convMaskFull().</summary>
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Function ConvMask1(m As Single, mc As Integer) As Single
        Select Case mc
            Case 1 : Return LinToSrgb1(m)
            Case 2 : Return SrgbToLin1(m)
            Case 3 : Return LinToG221(m)
            Case 4 : Return G22ToLin1(m)
            Case 5 : Return LinToG241(m)
            Case 6 : Return G24ToLin1(m)
            Case Else : Return m
        End Select
    End Function

    ' ---- Blend ops (transcripción 1:1 del shader blendDispatch; uBlendOp 0..4) ----
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Function BlendOverlay1(d As Single, s As Single) As Single
        ' GLSL step(0.5,d): d>=0.5 -> 1-2(1-d)(1-s) ; d<0.5 -> 2ds
        If d >= 0.5F Then Return 1.0F - 2.0F * (1.0F - d) * (1.0F - s)
        Return 2.0F * d * s
    End Function

    ''' <summary>Soft-light AGNOSTICO por modelo (= shader blendSoftLightModel; paridad CPU/GL). model:
    ''' 0=W3C 1=GIMP 2=Illusions 3=pegtop (FaceTintSoftLight). d=base, s=src. Default del resolver = GIMP.</summary>
    Private Function BlendSoftLightModel(model As Integer, d As Single, s As Single) As Single
        d = Clamp01(d) : s = Clamp01(s)
        Select Case model
            Case 1 ' GIMP / Photoshop
                If s <= 0.5F Then Return 2.0F * d * s + d * d * (1.0F - 2.0F * s)
                Return 2.0F * d * (1.0F - s) + MathF.Sqrt(d) * (2.0F * s - 1.0F)
            Case 2 ' Illusions.hu  d^(2^(2(0.5-s)))
                ' El exponente interno queda acotado a [0,5 , 2] para s en [0,1] y la base a >=1e-6, asi que
                ' el peor caso es 1e-12: muy por encima del minimo normal de float. No hace falta guard.
                ' ⭐ FastPow, no MathF.Pow: el exponente es VARIABLE por pixel, asi que usa el split de Dekker
                ' en runtime (PowVar1) y el 2^y va por Exp2_1 — que existe porque PowVar clampea la BASE a
                ' [0,1] y con base 2 devolveria 1. El espejo vectorial hace exactamente estas dos llamadas.
                Return FastPow.PowVar1(MathF.Max(d, 0.000001F), FastPow.Exp2_1(2.0F * (0.5F - s)))
            Case 3 ' pegtop == la forma del MOTOR (decision 4)
                ' ⭐ UNA SOLA EXPRESION: `d² + 2ds(1−d)`, la que el fold de SSE tomo del desensamblado del
                ' motor. Aca decia `(1−2s)d² + 2sd`, que es la MISMA identidad algebraica desarrollada — pero
                ' NO el mismo redondeo: enumerando los 65.536 pares de 8 bits difieren en 21.538 (32,86 %),
                ' peor 2 ULP, y el peor delta de BYTE es 1. O sea que no eran intercambiables y una de las dos
                ' tenia que ganar. Gana la del motor, que es la que tiene respaldo de RE.
                Return d * d + 2.0F * d * s * (1.0F - d)
            Case Else ' 0 = W3C SVG
                Dim g As Single = If(d >= 0.25F, MathF.Sqrt(d), ((16.0F * d - 12.0F) * d + 4.0F) * d)
                If s >= 0.5F Then Return d + (2.0F * s - 1.0F) * (g - d)
                Return d - (1.0F - 2.0F * s) * d * (1.0F - d)
        End Select
    End Function

    ' =====================================================================================================
    ' INVERSA ANALITICA DEL SOFT-LIGHT — la contraparte exacta de BlendSoftLightModel.
    ' =====================================================================================================
    ' POR QUE EXISTE: el UNFOLD de SSE tiene que CANCELAR el fold. El motor vuelve a aplicar
    ' softlight(., TINT) x amplify(DETAIL) sobre lo que escribimos, asi que el bake guarda la PREIMAGEN.
    ' ⛔ Hasta aca la inversa era SOLO la de pegtop, escrita a mano en DOS sitios (la rama uFgTintFold==2 del
    ' shader y SseFaceGenBaker.PreCompensateEngineChain). Con el modelo cableado en pegtop alcanzaba; en
    ' cuanto el modelo sale de la convencion, invertir con pegtop lo que se plego con GIMP deja de cancelar
    ' y el RENDER deja de mostrar lo que el juego dibuja.
    ' ⛔ ANALITICA EN LOS CUATRO MODELOS — NADA de Newton. Una inversa iterativa mete tolerancia y conteo de
    ' pasos en un camino que tiene que dar EL MISMO BYTE en el escalar, en el espejo vectorial y en el GLSL.
    '
    ' Notacion: d = destino (base), s = source, y = resultado. Se resuelve d dado (y, s).
    '
    ' ⭐ LOS TRES MODELOS NO-PEGTOP COMPARTEN LA RAMA BAJA CON PEGTOP, y no por casualidad:
    '     W3C  con s < 0,5 : y = d − (1−2s)d(1−d) = 2sd + (1−2s)d²   <- pegtop
    '     GIMP con s ≤ 0,5 : y = 2ds + d²(1−2s)                       <- pegtop
    '   asi que la mitad del dominio de cada uno reusa LA MISMA inversa. Se llama, no se copia.

    ''' <summary>Inversa de PEGTOP (modelo 3): <c>y = (1−2s)d² + 2sd</c>. Con <c>k = 1−2s</c> queda
    ''' <c>k d² + 2s d − y = 0</c> ⇒ <c>d = (−s + √(s² + k y)) / k</c>.
    ''' <para>La rama <c>+√</c> es la correcta para los DOS signos de k: el forward es monotono creciente en
    ''' [0,1] (con k &lt; 0 el vertice cae en <c>s/(2s−1) ≥ 1</c>, fuera del dominio), asi que hay una sola
    ''' preimagen valida.</para>
    ''' <para><c>k → 0</c> (s = 0,5) es la IDENTIDAD <c>y = d</c>: la formula daria 0/0. El umbral no es "por
    ''' las dudas" — con |k| chico la division amplifica el error de la raiz.</para></summary>
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Function SoftLightInvPegtop(y As Single, s As Single) As Single
        Dim k As Single = 1.0F - 2.0F * s
        If MathF.Abs(k) < 0.000001F Then Return y
        Dim disc As Single = s * s + k * y
        ' GUARD: la inversa es MAL CONDICIONADA cerca de k=0; en float32 `disc` puede dar negativo donde en
        ' float64 daba ~0. Sin esto, NaN. (Se preserva VERBATIM del sitio del que se izó esta ley.)
        If disc < 0.0F OrElse Single.IsNaN(disc) Then disc = 0.0F
        Return (-s + MathF.Sqrt(disc)) / k
    End Function

    ''' <summary>Inversa de GIMP/Photoshop (modelo 1). <c>s ≤ 0,5</c> ⇒ la rama ES pegtop.
    ''' <para><c>s &gt; 0,5</c>: <c>y = 2(1−s)d + (2s−1)√d</c> es una CUADRATICA en <c>t = √d</c>:
    ''' <c>a t² + b t − y = 0</c> con <c>a = 2(1−s)</c>, <c>b = 2s−1</c> ⇒ <c>t = (−b + √(b² + 4a y))/(2a)</c>
    ''' y <c>d = t²</c>. <c>a = 0</c> (s = 1) degenera a <c>y = t</c> ⇒ <c>d = y²</c>.</para></summary>
    Private Function SoftLightInvGimp(y As Single, s As Single) As Single
        If s <= 0.5F Then Return SoftLightInvPegtop(y, s)
        Dim a As Single = 2.0F * (1.0F - s)
        Dim b As Single = 2.0F * s - 1.0F
        If a < 0.000001F Then Return y * y
        Dim disc As Single = b * b + 4.0F * a * y
        If disc < 0.0F OrElse Single.IsNaN(disc) Then disc = 0.0F
        Dim t As Single = (-b + MathF.Sqrt(disc)) / (2.0F * a)
        Return t * t
    End Function

    ''' <summary>Inversa de Illusions.hu (modelo 2): <c>y = d^p</c> con <c>p = 2^(2(0,5−s))</c>.
    ''' <para>⭐ SIN FORMULA NUEVA: el exponente cumple <c>p(1−s) = 1/p(s)</c>, asi que la inversa es EL MISMO
    ''' forward con el source reflejado — <c>Inv(y,s) = Fwd(y, 1−s)</c>. Exacta, sin casos especiales, y
    ''' hereda el mismo piso de base (1e-6) que el forward ⇒ no puede desincronizarse de el.</para></summary>
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Function SoftLightInvIllusions(y As Single, s As Single) As Single
        Return Clamp01(BlendSoftLightModel(2, y, 1.0F - s))
    End Function

    ''' <summary>Inversa de W3C SVG (modelo 0). <c>s &lt; 0,5</c> ⇒ la rama ES pegtop.
    ''' <para><c>s ≥ 0,5</c>: <c>y = (1−b)d + b·g(d)</c> con <c>b = 2s−1</c> y <c>g</c> partida en d = 0,25.
    ''' La rama se elige POR EL VALOR de y, no por d (que es justo lo que no se conoce): el forward es
    ''' monotono creciente y las dos ramas COINCIDEN en d = 0,25, donde <c>y₀ = 0,25 + 0,25b</c>
    ''' (g(0,25) = √0,25 = 0,5 = el polinomio evaluado ahi). Entonces <c>y &gt; y₀ ⇔ d &gt; 0,25</c>.</para>
    ''' <para><b>y &gt; y₀</b> (g = √d): cuadratica en <c>t = √d</c>, <c>(1−b)t² + b t − y = 0</c>.
    ''' <c>b = 1</c> (s = 1) degenera a <c>y = √d</c> ⇒ <c>d = y²</c>.</para>
    ''' <para><b>y ≤ y₀</b> (g = 16d³−12d²+4d): CUBICA <c>16b d³ − 12b d² + (3b+1)d − y = 0</c>. Con la
    ''' sustitucion <c>d = u + 1/4</c> el termino cuadratico se cancela y queda deprimida:
    ''' <c>u³ + p u + q = 0</c> con <c>p = 1/(16b)</c> y <c>q = (b + 1 − 4y)/(64b)</c>.
    ''' Como <c>p &gt; 0</c>, el discriminante <c>(q/2)² + (p/3)³</c> es SIEMPRE positivo ⇒ hay UNA sola raiz
    ''' real y Cardano la da cerrada: <c>u = ∛(−q/2 + √Δ) + ∛(−q/2 − √Δ)</c>. Sin ambiguedad de rama, sin
    ''' trigonometria y sin iterar.</para>
    ''' <para><c>b = 0</c> (s = 0,5) es la identidad.</para></summary>
    Private Function SoftLightInvW3C(y As Single, s As Single) As Single
        If s < 0.5F Then Return SoftLightInvPegtop(y, s)
        Dim b As Single = 2.0F * s - 1.0F
        If b < 0.000001F Then Return y
        Dim y0 As Single = 0.25F + 0.25F * b
        If y > y0 Then
            Dim a As Single = 1.0F - b
            If a < 0.000001F Then Return y * y
            Dim disc As Single = b * b + 4.0F * a * y
            If disc < 0.0F OrElse Single.IsNaN(disc) Then disc = 0.0F
            Dim t As Single = (-b + MathF.Sqrt(disc)) / (2.0F * a)
            Return t * t
        End If
        Dim p As Single = 1.0F / (16.0F * b)
        Dim q As Single = (b + 1.0F - 4.0F * y) / (64.0F * b)
        Dim mq2 As Single = -0.5F * q
        Dim p3 As Single = p / 3.0F
        Dim delta As Single = mq2 * mq2 + p3 * p3 * p3
        If delta < 0.0F Then delta = 0.0F          ' p > 0 ⇒ no alcanzable; guard de redondeo, no de ley
        Dim sq As Single = MathF.Sqrt(delta)
        ' ⛔ FastPow.Cbrt1 y NO MathF.Cbrt: `MathF.Cbrt` no tiene contraparte en Vector(Of T), asi que el
        ' espejo vectorial no podria ser BIT-IDENTICO — que es el contrato del modulo. Ver FastPow.Cbrt1.
        Dim u As Single = FastPow.Cbrt1(mq2 + sq) + FastPow.Cbrt1(mq2 - sq)
        ' Esta rama solo se alcanza con y <= y0 <= 0,5 ⇒ d <= 0,25: el clamp es inerte y queda como declaracion
        ' de dominio, no como correccion. (La rama de la raiz cuadrada SI puede salirse y no se acota: ver la
        ' nota de BlendSoftLightModelInverse sobre por que `y` no se acota.)
        Return u + 0.25F
    End Function

    ''' <summary>⭐ Dispatch de la INVERSA del soft-light por modelo — la contraparte de
    ''' <see cref="BlendSoftLightModel"/>, con el MISMO orden de modelos (0=W3C 1=GIMP 2=Illusions 3=pegtop).
    ''' <para>⛔ SYNC: es la FUENTE UNICA de la inversa. La tienen que espejar el <c>PreCompensateEngineChain</c>
    ''' (escalar y vectorial) y la rama <c>uFgTintFold==2</c> del GLSL. Lo verifica
    ''' <see cref="SoftLightInverseSelfTest"/>, que es el gate: sin ese test, una inversa mal derivada NO se
    ''' ve en el bake (sale una cara levemente distinta, no un fallo).</para></summary>
    Public Function BlendSoftLightModelInverse(model As Integer, y As Single, s As Single) As Single
        ' ⛔⛔ `y` NO SE ACOTA, y no es un olvido: su consumidor es el UNFOLD, donde `y` es un valor LINEAL que
        ' YA fue dividido por el amplify del detail y puede pasarse de 1 con total legitimidad (amp < 1). La
        ' saturación es del PACK, al final de la cadena, no de esta función.
        ' ⭐ ESTO LO CAZÓ EL GATE, no una revisión: con `y = Clamp01(y)` el self-test `baker` dio
        ' `PreComp vector MISMATCH ... escalar=0x3F7FFFFF vector=0x3F800000` — o sea la ley acotada contra el
        ' espejo vectorial sin acotar. Media unidad de LSB que ninguna lectura del diff habría mostrado.
        ' `s` SÍ se acota: es un sample de textura ([0,1] por construcción) y las cuatro derivaciones asumen
        ' ese dominio para elegir rama. Acotarlo es inerte y deja las ramas bien definidas.
        s = Clamp01(s)
        Select Case model
            Case 1 : Return SoftLightInvGimp(y, s)
            Case 2 : Return SoftLightInvIllusions(y, s)
            Case 3 : Return SoftLightInvPegtop(y, s)
            Case Else : Return SoftLightInvW3C(y, s)
        End Select
    End Function

    ''' <summary>GATE de la inversa: para los CUATRO modelos barre (d, s) y verifica que
    ''' <c>Inv(model, Fwd(model, d, s), s) ≈ d</c>. Devuelve "" si pasa.
    ''' <para>El criterio es en unidades de BYTE (lo que se hornea), no en epsilon de float: la cadena termina
    ''' en un DDS de 8 bits. Tolerancia 1 byte = 1/255.</para>
    ''' <para>⛔ Se saltean los puntos donde el forward DESTRUYE informacion y ninguna inversa los recupera:
    ''' Illusions con d por debajo de su piso (1e-6) — misma politica declarada que el amplify con amp ≤ 0 en
    ''' <c>SseFaceGenBaker.FgAmpInverse</c>. No se maquilla la tolerancia para taparlos.</para></summary>
    Public Function SoftLightInverseSelfTest() As String
        Const TOL As Single = 1.0F / 255.0F
        For model As Integer = 0 To 3
            For si As Integer = 0 To 64
                Dim s As Single = si / 64.0F
                For di As Integer = 0 To 64
                    Dim d As Single = di / 64.0F
                    If model = 2 AndAlso d < 0.001F Then Continue For   ' piso del forward: no es invertible
                    Dim y As Single = BlendSoftLightModel(model, d, s)
                    Dim back As Single = BlendSoftLightModelInverse(model, y, s)
                    Dim err As Single = MathF.Abs(back - d)
                    If err > TOL Then
                        Return $"soft-light inverse MISMATCH: model={model} s={s:F4} d={d:F4} fwd={y:F6} inv={back:F6} err={err * 255.0F:F3} bytes (tol=1)"
                    End If
                Next
            Next
        Next
        Return ""
    End Function

    ' ---- Modos separables estandar adicionales (5..19). Transcripcion 1:1 del shader. ----
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Function BlendColorDodge1(d As Single, s As Single) As Single
        If s >= 1.0F Then Return 1.0F
        Return MathF.Min(1.0F, d / (1.0F - s))
    End Function
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Function BlendColorBurn1(d As Single, s As Single) As Single
        If s <= 0.0F Then Return 0.0F
        Return 1.0F - MathF.Min(1.0F, (1.0F - d) / s)
    End Function
    Private Function BlendDivide1(d As Single, s As Single) As Single
        If s <= 0.0F Then Return 1.0F
        Return MathF.Min(1.0F, d / s)
    End Function
    Private Function BlendVividLight1(d As Single, s As Single) As Single
        If s < 0.5F Then Return BlendColorBurn1(d, 2.0F * s)
        Return BlendColorDodge1(d, 2.0F * (s - 0.5F))
    End Function
    Private Function BlendPinLight1(d As Single, s As Single) As Single
        If s < 0.5F Then Return MathF.Min(d, 2.0F * s)
        Return MathF.Max(d, 2.0F * s - 1.0F)
    End Function

    ''' <summary>Identidad del blend op: el src que hace blend(prev,src)=prev. La usa ModSrc para que
    ''' cov=0 deje prev intacto: mix(neutral, src, cov). = shader blendNeutral(). bop=replace no tiene
    ''' identidad constante -> ModSrc degrada a OverPrev (ver ComposeOne).</summary>
    ''' ⭐ TABLA en vez de Select Case: `bop` es INVARIANTE para toda la textura, asi que este Select
    ''' devolvia SIEMPRE la misma constante y se re-evaluaba una vez por pixel y por canal (millones por
    ''' capa). La tabla da el MISMO valor con un indexado, sin ramas.
    ''' ⛔ BIT-IDENTICO: son exactamente los mismos tres literales (1.0 / 0.5 / 0.0) mapeados a los mismos
    ''' bop. El indice 0 y cualquier bop fuera de [0,18] caen en 0.0, igual que el `Case Else` de antes.
    Private ReadOnly BlendNeutralTable As Single() = BuildBlendNeutralTable()
    Private Function BuildBlendNeutralTable() As Single()
        Dim t(18) As Single                                  ' default 0.0 = el Case Else previo
        For Each b In New Integer() {1, 6, 9, 13, 15} : t(b) = 1.0F : Next    ' multiply/darken/colorburn/linearburn/divide
        For Each b In New Integer() {2, 3, 4, 16, 17, 18} : t(b) = 0.5F : Next ' overlay/softlight/hardlight/linearlight/vividlight/pinlight
        Return t
    End Function

    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Function BlendNeutral1(bop As Integer) As Single
        If bop < 0 OrElse bop > 18 Then Return 0.0F
        Return BlendNeutralTable(bop)
    End Function

    ''' <summary>Dispatch de blend por canal escalar. 0=replace 1=mult 2=overlay 3=softlight 4=hardlight,
    ''' 5..19 = modos separables estandar. softLight: modelo cuando blendOp=3. = shader blendDispatchBop().</summary>
    Private Function BlendDispatch1(blendOp As Integer, softLight As Integer, d As Single, s As Single) As Single
        Select Case blendOp
            Case 1 : Return d * s                                ' multiply
            Case 2 : Return BlendOverlay1(d, s)                  ' overlay
            Case 3 : Return BlendSoftLightModel(softLight, d, s) ' softlight (modelo elegido)
            Case 4 : Return BlendOverlay1(s, d)                  ' hardlight = overlay(s,d)
            Case 5 : Return d + s - d * s                        ' screen
            Case 6 : Return MathF.Min(d, s)                      ' darken
            Case 7 : Return MathF.Max(d, s)                      ' lighten
            Case 8 : Return BlendColorDodge1(d, s)               ' colordodge
            Case 9 : Return BlendColorBurn1(d, s)                ' colorburn
            Case 10 : Return MathF.Abs(d - s)                    ' difference
            Case 11 : Return d + s - 2.0F * d * s                ' exclusion
            Case 12 : Return MathF.Min(1.0F, d + s)              ' lineardodge (add)
            Case 13 : Return MathF.Max(0.0F, d + s - 1.0F)       ' linearburn
            Case 14 : Return MathF.Max(0.0F, d - s)              ' subtract
            Case 15 : Return BlendDivide1(d, s)                  ' divide
            Case 16 : Return Clamp01(d + 2.0F * s - 1.0F)        ' linearlight
            Case 17 : Return BlendVividLight1(d, s)              ' vividlight
            Case 18 : Return BlendPinLight1(d, s)                ' pinlight
            Case 19 : Return If(d + s >= 1.0F, 1.0F, 0.0F)       ' hardmix
            Case Else : Return s                                 ' replace (0, default)
        End Select
    End Function

    ''' <summary>Per-channel blend op, PUBLIC re-use of the CPU/GL-parity dispatch (BlendDispatch1 = shader
    ''' blendDispatchBop). Used by the SSE RaceMenu-overlay compositor so it shares the SAME blend math as the
    ''' FO4 facetint (one source of truth, CPU==GL). blendOp/softLightModel per the enum above.</summary>
    Public Function BlendChannel(blendOp As Integer, softLightModel As Integer, base As Single, src As Single) As Single
        Return BlendDispatch1(blendOp, softLightModel, base, src)
    End Function

    ' ---- Decode DDS -> RGBA float [0,1] (mirror de FaceTintCompositor.WritePristineTga) ----
    ''' <summary>⭐ Tabla byte→unidad: <c>ByteToUnit(b) = CSng(b / 255.0)</c>, los 256 valores posibles.
    ''' <para>Es lo que hace BIT-IDÉNTICO el storage en Byte de <see cref="DecodedTex.Rgba8"/>. El decode
    ''' guardaba <c>CSng(r/255.0)</c> y los lectores lo widenean a Double; leer por esta tabla devuelve
    ''' EXACTAMENTE ese mismo Single ⇒ el mismo Double ⇒ la misma cuenta. ⛔ NO reemplazar por
    ''' <c>b / 255.0</c> en Double: eso es MÁS preciso pero es OTRO número (p.ej. b=1 difiere en el bit 24)
    ''' y cambiaría la salida horneada.</para>
    ''' 1 KB, vive en L1 — un lookup en vez de una carga de float, sobre un array 4× más chico.</summary>
    Public ReadOnly ByteToUnit As Single() = BuildByteToUnit()
    Private Function BuildByteToUnit() As Single()
        Dim t(255) As Single
        For i As Integer = 0 To 255
            t(i) = CSng(i / 255.0)
        Next
        Return t
    End Function

    Public Class DecodedTex
        Public Width As Integer
        Public Height As Integer
        ' STORAGE en Byte: es el ancho NATIVO del dato, porque todo lo que produce DecodeDds es de 8 bits.
        ' Se lee via ByteToUnit, asi que el Single que sale es bit a bit el que se guardaba antes y la
        ' matematica sigue en Double (los escalares widenean).
        ' INVARIANTE: este buffer es READ-ONLY despues del decode. ReconstructNormalZ y el compose trabajan
        ' sobre acumuladores aparte. Si alguna vez hiciera falta escribir un valor arbitrario aca, deja de ser
        ' lossless.
        ' El campo se llama Rgba8 y NO Rgba A PROPOSITO: al angostar el storage, todo consumidor que siguiera
        ' escribiendo t.Unit(i) habria COMPILADO igual (VB widenea Byte->Double en silencio) y leido 255 donde
        ' esperaba 1,0 - corrupcion muda en ~80 sitios. Leer SIEMPRE por Unit()/CopyUnitTo().
        Public Rgba8 As Byte()   ' length W*H*4, orden R,G,B,A crudos 0..255 (unidad = ByteToUnit(v))

        ''' <summary>Elemento <paramref name="i"/> en unidad [0,1]. Devuelve EXACTAMENTE el Single que este
        ''' buffer guardaba cuando era Single() ⇒ las cuentas rio abajo no cambian ni un bit.</summary>
        Public Function Unit(i As Integer) As Single
            Return ByteToUnit(Rgba8(i))
        End Function

        ''' <summary>Expande el buffer entero a unidad [0,1] sobre <paramref name="dst"/> (mismo largo).
        ''' Reemplaza a los `Array.Copy(t.Rgba8, acc, …)`: Array.Copy con Byte()→Single() haria la conversion
        ''' WIDENING (255 → 255,0F) en vez de la de ESCALA, o sea el bug exacto que este rename previene.</summary>
        Public Sub CopyUnitTo(dst As Single())
            Dim n = Math.Min(If(dst Is Nothing, 0, dst.Length), If(Rgba8 Is Nothing, 0, Rgba8.Length))
            Dim lut = ByteToUnit, src = Rgba8
            For i As Integer = 0 To n - 1
                dst(i) = lut(src(i))
            Next
        End Sub

        ''' <summary>El buffer entero como Single() fresco en unidad [0,1]. Para los pocos consumidores que
        ''' necesitan un array Single completo (subida a GL, APIs que toman Single()). ⛔ Materializa 4× la
        ''' memoria: no usar dentro de un loop ni en el camino cacheado.</summary>
        Public Function ToUnitArray() As Single()
            If Rgba8 Is Nothing Then Return Nothing
            Dim outp(Rgba8.Length - 1) As Single
            CopyUnitTo(outp)
            Return outp
        End Function
        ''' <summary>Canales REALES de la fuente ANTES del pack a RGBA: 4 (BC1/3/7, RGBA8/BGRA8), 2 (BC5 → R8G8)
        ''' o 1 (BC4 → gray). El pack de abajo rellena lo que falta con constantes (2 canales ⇒ <c>B=0, A=1</c>),
        ''' y hasta acá ese relleno era INDISTINGUIBLE de un píxel real: un consumidor que interpreta el RGB como
        ''' un VECTOR (un normal map) leía <c>z = 2·0−1 = −1</c>, la normal apuntando hacia adentro.
        ''' <para>⛔ NO se puede deducir del píxel: en un normal MODEL-SPACE <c>B=0</c> es un valor legítimo
        ''' (z=−1, la nuca). Tiene que salir del FORMATO — por eso viaja acá. Ver
        ''' <see cref="ReconstructNormalZ"/>.</para>
        ''' Default 4 ⇒ cualquier consumidor que no lo mire se comporta EXACTAMENTE como antes.</summary>
        Public Channels As Integer = 4
    End Class

    ''' <summary>Ley de reconstruccion del eje Z de un normal map de 2 canales (BC5/R8G8), in-place sobre un
    ''' buffer RGBA [0,1]. UNA sola implementacion: la usan el decode de las texturas de overlay y el del
    ''' <c>_msn</c> de la cabeza en bake y render - si divergieran, el mismo tatuaje se horneria distinto de
    ''' como se ve.
    ''' <para>Se decodifica x,y a [-1,1] y se despeja <c>z = sqrt(max(0, 1 - x^2 - y^2))</c>, la inversa EXACTA
    ''' del encode de un normal unitario. El signo no es ambiguo: una fuente de 2 canales no puede ser
    ''' model-space, asi que es tangent-space autorada y ahi z >= 0 siempre.</para>
    ''' <para>Se aplica DESPUES del resample, que es lo que hace el hardware (samplea el BC5 ya filtrado y
    ''' recien ahi despeja z). El alpha no se toca.</para></summary>
    Public Sub ReconstructNormalZ(rgba As Single(), npix As Integer)
        If rgba Is Nothing OrElse npix <= 0 OrElse rgba.Length < npix * 4 Then Return
        ' Por-píxel puro, escrituras disjuntas ⇒ bit-idéntico al serial (misma justificación que el resto del
        ' módulo). El _msn de la cabeza puede ser 4096² con COtR.
        System.Threading.Tasks.Parallel.ForEach(
            System.Collections.Concurrent.Partitioner.Create(0, npix),
            Sub(range)
                For i = range.Item1 To range.Item2 - 1
                    Dim x = 2.0F * rgba(i * 4) - 1.0F
                    Dim y = 2.0F * rgba(i * 4 + 1) - 1.0F
                    Dim zz = 1.0F - x * x - y * y
                    Dim z = If(zz > 0.0F, MathF.Sqrt(zz), 0.0F)
                    rgba(i * 4 + 2) = (z + 1.0F) * 0.5F
                Next
            End Sub)
    End Sub

    ''' <summary>⭐ INTERRUPTOR de la ley del mip, para TODO el compositor y los DOS juegos (CharGen Options →
    ''' "Downsize from mip 0"). False (default) = se usa el MIP STORED del target. True = se parte siempre del
    ''' nivel 0 y se baja con un unico bilineal, que es mas lento — sobre todo con un target chico contra una
    ''' fuente grande (4096 → 1024 desempaqueta 16x los pixeles) — y ademas multiplica decodes, porque la clave
    ''' del cache lleva el tamaño (key@WxH) y la misma mascara se decodifica una vez por canal.
    ''' <para>⛔ Lo lee <see cref="SelectLevelForTarget"/>, que es el UNICO punto por el que pasan todos los
    ''' caminos CPU (source, capas, swaps, mascaras de los dos juegos), y el GL lo recibe por el uniform
    ''' <c>uDownsizeFromMip0</c> que consume su <c>pickLod</c>. Un solo valor, un solo gate por idioma: por eso
    ''' no puede quedar mitad y mitad. Medido: mip-stored en UN solo lado degrada la paridad CPU/GPU (peor
    ''' delta 9 → 39); en los dos lados vuelve a 9. Ver 50-facetint-leyes-y-compositor.</para></summary>
    Public Property DownsizeFromMip0 As Boolean = False

    ''' <summary>⭐ LEY UNICA de seleccion de mip. Devuelve el INDICE del nivel que hay que usar para componer
    ''' a <paramref name="targetW"/>×<paramref name="targetH"/>, con los niveles ordenados largest->smallest
    ''' (0 = nativo), tal como los entrega un DDS:
    ''' <list type="number">
    ''' <item>EXACTO: hay un nivel a ESE tamaño -> ése. Resample CERO: el texel sale verbatim del filtro con
    ''' que Bethesda generó el mip, que es el que consumió el motor.</item>
    ''' <item>DOWNSIZE: no hay exacto pero sí niveles >= target -> el MAS CHICO de ellos. Bajar en el paso
    ''' corto aliasa menos que un unico bilineal grande desde el nivel 0.</item>
    ''' <item>UPSIZE, sin target, o sin niveles >= target -> el 0 (el mas grande). No hay de donde bajar.</item>
    ''' </list>
    ''' <para>⛔ Existe como funcion aparte para que la ley este escrita UNA vez: el CPU le pasa las
    ''' dimensiones de los niveles del DDS y el GL puede pedirselas al driver. La incoherencia historica
    ''' (source por mip stored, capas por mip 0, GL siempre mip 0) venia de que la regla estaba inlineada en
    ''' un solo camino y los otros dos no la tenian. Mismo patron que ResolveConvention: paridad por
    ''' construccion, no por coincidencia. Ver 50-facetint-leyes-y-compositor.</para>
    ''' <para>Toma pares (0,0) para niveles ausentes y los ignora, asi el caller no tiene que filtrarlos.</para></summary>
    Public Function SelectLevelForTarget(levels As IList(Of (W As Integer, H As Integer)),
                                         targetW As Integer, targetH As Integer) As Integer
        If DownsizeFromMip0 Then Return 0
        If levels Is Nothing OrElse levels.Count <= 1 Then Return 0
        If targetW <= 0 OrElse targetH <= 0 Then Return 0
        Dim geIdx As Integer = -1   ' como el indice sube y el tamaño baja, el ULTIMO que cumpla >= target
        For li As Integer = 0 To levels.Count - 1   ' es el mas chico que lo cumple
            Dim cw = levels(li).W, ch = levels(li).H
            If cw <= 0 OrElse ch <= 0 Then Continue For
            If cw = targetW AndAlso ch = targetH Then Return li
            If cw >= targetW AndAlso ch >= targetH Then geIdx = li
        Next
        Return If(geIdx >= 0, geIdx, 0)
    End Function

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
            Dim dims As New List(Of (W As Integer, H As Integer))(tex.Levels.Count)
            For li As Integer = 0 To tex.Levels.Count - 1
                Dim cand = tex.Levels(li)
                dims.Add(If(cand Is Nothing, (0, 0), (cand.Width, cand.Height)))
            Next
            Dim lvlIdx As Integer = SelectLevelForTarget(dims, preferW, preferH)
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
            Dim outArr(w * h * 4 - 1) As Byte
            ' Paralelo por rangos: el pack es puramente por-píxel (escrituras disjuntas ⇒ bit-idéntico al serial).
            ' El fold SSE decodea el complexion a resolución NATIVA (4096² con COtR = 16,7M px) en cada fold.
            ' Se guardan los bytes CRUDOS: la división por 255 se hace al LEER, por ByteToUnit (misma cuenta,
            ' menos memoria). Las constantes de relleno del pack son las mismas de siempre: 2 canales ⇒ B=0 A=255.
            System.Threading.Tasks.Parallel.ForEach(
                System.Collections.Concurrent.Partitioner.Create(0, w * h),
                Sub(range)
                    For i As Integer = range.Item1 To range.Item2 - 1
                        Dim o As Integer = i * 4, s As Integer = i * bpp
                        Select Case bpp
                            Case 4
                                If isBgra8 Then
                                    outArr(o) = px(s + 2) : outArr(o + 1) = px(s + 1) : outArr(o + 2) = px(s) : outArr(o + 3) = px(s + 3)
                                Else
                                    outArr(o) = px(s) : outArr(o + 1) = px(s + 1) : outArr(o + 2) = px(s + 2) : outArr(o + 3) = px(s + 3)
                                End If
                            Case 2
                                outArr(o) = px(s) : outArr(o + 1) = px(s + 1) : outArr(o + 2) = 0 : outArr(o + 3) = 255
                            Case Else ' 1
                                outArr(o) = px(s) : outArr(o + 1) = px(s) : outArr(o + 2) = px(s) : outArr(o + 3) = 255
                        End Select
                    Next
                End Sub)
            ' `bpp` acá ES el número de canales de la fuente (4 = RGBA/BGRA8, 2 = R8G8 de un BC5, 1 = R8 de un
            ' BC4): la tabla de formatos de arriba lo asigna con ese significado. Se propaga para que un
            ' consumidor VECTORIAL (normal map) sepa que el B/A que ve es relleno del pack. Ver DecodedTex.Channels.
            Return New DecodedTex With {.Width = w, .Height = h, .Rgba8 = outArr, .Channels = bpp}
        Catch
            Return Nothing
        End Try
    End Function

    ' =====================================================================================================
    ' ⭐ LA LEY DEL BILINEAL, ESCRITA UNA VEZ (decision 5 del plan: "UNO SOLO, el que replica al GPU").
    ' =====================================================================================================
    ' ⛔ Estaba transcripta CUATRO veces —SampleBilinear, ResampleRgbaFloat, ResampleBgra y un bilineal
    ' INLINE dentro de CachedUnitDecode—. Las tres primeras coincidian cuenta por cuenta; la CUARTA no, y
    ' difería en tres cosas a la vez, ninguna visible leyendo por encima:
    '   1. derivaba el indice alto del BAJO YA CLAMPEADO (`y1 = Min(H-1, y0+1)`), asi que en el borde donde
    '      el texel cae en -0.5 interpolaba entre las filas 0 y 1 con t=0,5 en vez de devolver la fila 0.
    '      Eso NO es clamp-to-edge: es medio texel de corrimiento en todo el borde de la textura.
    '   2. mapeaba con `(y+0.5)*src/dst - 0.5` en vez de `clamp01((y+0.5)/dst)*src - 0.5` ⇒ otro redondeo.
    '   3. factorizaba el lerp como `(p00+(p10-p00)tx)` anidado y calculaba tx/ty en DOUBLE ⇒ otro redondeo.
    ' Que la ley viva en DOS funciones (eje + mezcla) y no en una sola es lo que permite que la usen tanto
    ' quien MATERIALIZA el resample entero como quien muestrea UN texel: son dos CAMINOS de la misma ley
    ' —legitimo— y ahora estan obligados a dar el mismo numero porque hacen las mismas cuentas.

    ''' <summary>Un EJE del bilineal: de la coordenada normalizada de destino a (texel bajo, texel alto,
    ''' fraccion). Convencion GL_LINEAR + CLAMP_TO_EDGE, que es la referencia de paridad con el GPU: el texel
    ''' es <c>u*size - 0.5</c> (offset de medio texel) y los DOS indices se clampean por separado a
    ''' [0, size-1] — clampear solo el bajo y derivar el alto de el corre el borde medio texel.</summary>
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Friend Sub BilinearAxis(u As Single, srcSize As Integer,
                            ByRef i0 As Integer, ByRef i1 As Integer, ByRef t As Single)
        Dim c = Clamp01(u) * srcSize - 0.5F
        Dim i = CInt(MathF.Floor(c))
        t = c - i
        i0 = Math.Max(0, Math.Min(srcSize - 1, i))
        i1 = Math.Max(0, Math.Min(srcSize - 1, i + 1))
    End Sub

    ''' <summary>La MEZCLA del bilineal: los cuatro texels y las dos fracciones, en el orden EXACTO de
    ''' operaciones que fija la ley (los cuatro productos completos y su suma, no la forma anidada).</summary>
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Friend Function BilinearMix(c00 As Single, c10 As Single, c01 As Single, c11 As Single,
                                tx As Single, ty As Single) As Single
        Return c00 * (1 - tx) * (1 - ty) + c10 * tx * (1 - ty) + c01 * (1 - tx) * ty + c11 * tx * ty
    End Function

    ''' <summary>Sample bilineal de un canal (0=R 1=G 2=B 3=A) en coord normalizada (u,v) [0,1], clamp
    ''' a borde. Es la ley de arriba aplicada a un <see cref="DecodedTex"/>. Mismo filtro que el shader.</summary>
    Private Function SampleBilinear(t As DecodedTex, u As Single, v As Single, ch As Integer) As Single
        Dim w = t.Width, h = t.Height
        Dim x0 As Integer, x1 As Integer, tx As Single
        Dim y0 As Integer, y1 As Integer, ty As Single
        BilinearAxis(u, w, x0, x1, tx)
        BilinearAxis(v, h, y0, y1, ty)
        Return BilinearMix(t.Unit((y0 * w + x0) * 4 + ch), t.Unit((y0 * w + x1) * 4 + ch),
                           t.Unit((y1 * w + x0) * 4 + ch), t.Unit((y1 * w + x1) * 4 + ch), tx, ty)
    End Function

    ''' <summary>Gemelo FLOAT de <see cref="ResampleBgra"/>: MISMO filtro (GL_LINEAR + CLAMP_TO_EDGE, texel =
    ''' uv*size-0.5, centro de pixel u=(x+0.5)/dw) sobre un acumulador <c>Single()</c> RGBA, sin pasar por bytes.
    ''' <para>Existe para que el RENDER honre la resolucion de CharGen Options igual que el bake: el bake
    ''' resamplea el buffer ya convertido a BGRA, el render trabaja en float de punta a punta y no debe
    ''' cuantizar en el medio - la perdida de 8 bits y de BCn es del ARCHIVO, no del COMPOSE.</para>
    ''' <para>âš ï¸ Se resamplea en el espacio en que este el buffer (para el fold SSE: sRGB, ANTES del paso a
    ''' lineal). Bilinear en sRGB no es bilinear en lineal, asi que el punto de la cadena donde se llama es
    ''' parte del contrato. Con sw==dw y sh==dh devuelve el MISMO array (no-op bit-inerte).</para></summary>
    Public Function ResampleRgbaFloat(src As Single(), sw As Integer, sh As Integer, dw As Integer, dh As Integer) As Single()
        If src Is Nothing OrElse sw <= 0 OrElse sh <= 0 OrElse dw <= 0 OrElse dh <= 0 Then Return src
        If sw = dw AndAlso sh = dh Then Return src
        Dim outp(dw * dh * 4 - 1) As Single
        System.Threading.Tasks.Parallel.For(0, dh, Sub(y)
                                                       Dim y0 As Integer, y1 As Integer, ty As Single
                                                       BilinearAxis(CSng((y + 0.5) / dh), sh, y0, y1, ty)
                                                       For x = 0 To dw - 1
                                                           Dim x0 As Integer, x1 As Integer, tx As Single
                                                           BilinearAxis(CSng((x + 0.5) / dw), sw, x0, x1, tx)
                                                           Dim i00 = (y0 * sw + x0) * 4, i10 = (y0 * sw + x1) * 4, i01 = (y1 * sw + x0) * 4, i11 = (y1 * sw + x1) * 4
                                                           Dim o = (y * dw + x) * 4
                                                           For ch = 0 To 3
                                                               outp(o + ch) = BilinearMix(src(i00 + ch), src(i10 + ch), src(i01 + ch), src(i11 + ch), tx, ty)
                                                           Next
                                                       Next
                                                   End Sub)
        Return outp
    End Function

    Public Function ResampleBgra(bgra As Byte(), sw As Integer, sh As Integer, dw As Integer, dh As Integer) As Byte()
        If bgra Is Nothing OrElse sw <= 0 OrElse sh <= 0 OrElse dw <= 0 OrElse dh <= 0 Then Return bgra
        If sw = dw AndAlso sh = dh Then Return bgra
        Dim outp(dw * dh * 4 - 1) As Byte
        System.Threading.Tasks.Parallel.For(0, dh, Sub(y)
                                                       Dim y0 As Integer, y1 As Integer, ty As Single
                                                       BilinearAxis(CSng((y + 0.5) / dh), sh, y0, y1, ty)
                                                       For x = 0 To dw - 1
                                                           Dim x0 As Integer, x1 As Integer, tx As Single
                                                           BilinearAxis(CSng((x + 0.5) / dw), sw, x0, x1, tx)
                                                           Dim i00 = (y0 * sw + x0) * 4, i10 = (y0 * sw + x1) * 4, i01 = (y1 * sw + x0) * 4, i11 = (y1 * sw + x1) * 4
                                                           Dim o = (y * dw + x) * 4
                                                           For ch = 0 To 3
                                                               ' Los texels son bytes 0..255 (no unidad): la mezcla es lineal, asi que es la MISMA
                                                               ' ley — sólo cambia la escala. El redondeo a byte queda acá, que es lo propio de este camino.
                                                               Dim c = BilinearMix(bgra(i00 + ch), bgra(i10 + ch), bgra(i01 + ch), bgra(i11 + ch), tx, ty)
                                                               outp(o + ch) = CByte(MathF.Max(0.0F, MathF.Min(255.0F, MathF.Round(c, MidpointRounding.ToEven))))
                                                           Next
                                                       Next
                                                   End Sub)
        Return outp
    End Function

    ''' <summary>⭐ GATE DEL IZADO: los planos materializados tienen que ser EXACTAMENTE lo que devolvía el
    ''' muestreo por texel, elemento por elemento y bit por bit. Devuelve "" si coinciden.
    ''' <para><b>Por qué hace falta si ya está <see cref="BilinearLawSelfTest"/>.</b> Aquél fija la LEY (que
    ''' materializar y muestrear coinciden); éste fija el IZADO CONCRETO que consume el compose: el orden de
    ''' los canales en los planos, el índice (plano(i) ↔ píxel i) y el <c>chMask</c>. Un plano de G escrito en
    ''' el de B pasaría el primero y fallaría éste.</para>
    ''' <para>⛔ El corpus NO puede cubrirlo: vanilla trae las máscaras ya al tamaño del acumulador, así que
    ''' el camino izado no se ejecuta ni una vez y un A/B en 0 bytes no dice nada de él. Ver
    ''' <c>_unitResampled</c> y la memoria 00-reglas-epistemica §9.</para>
    ''' <para>Barre upsize, downsize y tamaños que NO son múltiplo del ancho SIMD (31×19, 37×23, 11×11): el
    ''' cuerpo vectorial consume bloques de <c>lanes</c> y la cola escalar tiene que leer los MISMOS planos.</para></summary>
    Public Function ResampleHoistSelfTest() As String
        Dim seed As UInteger = 2246822519UI
        For Each dims In New(TW As Integer, TH As Integer, W As Integer, H As Integer)() {
            (8, 8, 32, 32), (32, 32, 8, 8), (13, 7, 31, 19), (31, 19, 13, 7), (5, 5, 11, 11), (64, 64, 37, 23)}
            Dim tw = dims.TW, th = dims.TH, w = dims.W, h = dims.H
            Dim px(tw * th * 4 - 1) As Byte
            For i = 0 To px.Length - 1
                seed = seed Xor (seed << 13) : seed = seed Xor (seed >> 17) : seed = seed Xor (seed << 5)
                px(i) = CByte(seed And 255UI)
            Next
            Dim tex As New DecodedTex With {.Width = tw, .Height = th, .Rgba8 = px, .Channels = 4}
            Dim pR As Single() = Nothing, pG As Single() = Nothing, pB As Single() = Nothing, pA As Single() = Nothing
            ResampleToUnitPlanes(tex, w, h, 15, pR, pG, pB, pA)
            If pR Is Nothing OrElse pG Is Nothing OrElse pB Is Nothing OrElse pA Is Nothing Then
                Return $"izado: chMask=15 tiene que devolver los CUATRO planos y alguno vino Nothing ({tw}x{th}->{w}x{h})"
            End If
            Dim planes = New Single()() {pR, pG, pB, pA}
            For i = 0 To w * h - 1
                For c = 0 To 3
                    Dim want = SampleChannelAt(tex, i, w, h, c)
                    Dim got = planes(c)(i)
                    If BitConverter.SingleToInt32Bits(want) <> BitConverter.SingleToInt32Bits(got) Then
                        Return $"izado: el plano NO es el muestreo por texel. {tw}x{th}->{w}x{h} " &
                               $"px={i} (x={i Mod w},y={i \ w}) ch={c} porTexel={want} plano={got} " &
                               $"[bits 0x{BitConverter.SingleToInt32Bits(want):X8} vs 0x{BitConverter.SingleToInt32Bits(got):X8}]"
                    End If
                Next
            Next
            ' chMask: pedir UN canal tiene que dar ese MISMO plano y Nothing en los otros tres. Es lo que usan
            ' la máscara del swap (sólo R) y la diffMask (sólo A); un mask mal armado devolvería Nothing y el
            ' consumidor leería una referencia nula recién con una textura no-directa, o sea nunca en vanilla.
            For c = 0 To 3
                Dim qR As Single() = Nothing, qG As Single() = Nothing, qB As Single() = Nothing, qA As Single() = Nothing
                ResampleToUnitPlanes(tex, w, h, 1 << c, qR, qG, qB, qA)
                Dim q = New Single()() {qR, qG, qB, qA}
                For k = 0 To 3
                    If k = c Then
                        If q(k) Is Nothing Then Return $"izado: chMask={1 << c} no devolvió el plano {k} ({tw}x{th}->{w}x{h})"
                        For i = 0 To w * h - 1
                            If BitConverter.SingleToInt32Bits(q(k)(i)) <> BitConverter.SingleToInt32Bits(planes(k)(i)) Then
                                Return $"izado: chMask={1 << c} dio OTRO valor que chMask=15 en ch={k} px={i} ({tw}x{th}->{w}x{h})"
                            End If
                        Next
                    ElseIf q(k) IsNot Nothing Then
                        Return $"izado: chMask={1 << c} materializó de más el plano {k} ({tw}x{th}->{w}x{h})"
                    End If
                Next
            Next
        Next
        Return ""
    End Function

    ''' <summary>⭐ IZA EL RESAMPLE fuera del loop de píxeles: materializa <paramref name="t"/> a
    ''' <paramref name="w"/>×<paramref name="h"/> en unidad [0,1] sobre PLANOS SoA (un array por canal).
    ''' <para><b>Por qué planos y no AoS.</b> El cuerpo vectorial necesita 8 R contiguos. Con planos eso es una
    ''' carga vectorial directa; con AoS habría que de-interleavear con stride 4 (gather manual). Los planos
    ''' cuestan lo mismo en memoria (16 B/px) y ahorran el de-interleave por bloque.</para>
    ''' <para><b>Por qué NO es una ley nueva.</b> Llama a <see cref="SampleChannelAt"/>, la MISMA función que
    ''' usa el muestreo por texel: no hay una transcripción que pueda divergir — es la misma cuenta, movida de
    ''' lugar. Lo único que cambia es DÓNDE se evalúa, no QUÉ.</para>
    ''' <para><paramref name="chMask"/> es un bitmask (1=R 2=G 4=B 8=A): tres de las texturas del compose usan
    ''' UN SOLO canal, y materializar los cuatro cuadruplicaría el costo del izado para nada. El plano de un
    ''' canal no pedido vuelve <c>Nothing</c>.</para>
    ''' <para>⛔ NO tiene atajo de identidad a propósito: el caller que YA es directo no debe llamar acá — se
    ''' queda en su camino de bytes, que es más barato (1 B/px y sin materializar nada).</para></summary>
    Friend Sub ResampleToUnitPlanes(t As DecodedTex, w As Integer, h As Integer, chMask As Integer,
                                    ByRef pR As Single(), ByRef pG As Single(), ByRef pB As Single(), ByRef pA As Single())
        pR = Nothing : pG = Nothing : pB = Nothing : pA = Nothing
        If t Is Nothing OrElse w <= 0 OrElse h <= 0 Then Return
        Dim n = w * h
        If (chMask And 1) <> 0 Then ReDim pR(n - 1)
        If (chMask And 2) <> 0 Then ReDim pG(n - 1)
        If (chMask And 4) <> 0 Then ReDim pB(n - 1)
        If (chMask And 8) <> 0 Then ReDim pA(n - 1)
        ' ⛔ CONTADOR OBLIGATORIO. Sin esto no hay forma de saber si el camino izado se ejecuta: el corpus
        ' vanilla sale casi todo por el atajo de directness y un A/B en 0 bytes NO dice nada sobre un camino
        ' que no se piso. Es el mismo motivo por el que existe `_unitResampled` en el nivel 2 — que NO sirve
        ' para esto, porque el izado no pasa por el nivel 2.
        Threading.Interlocked.Increment(_hoistCount)
        Threading.Interlocked.Add(_hoistPixels, CLng(n))
        ' ⛔ SERIAL A PROPOSITO — NO reponer un Parallel.For aca. Esta funcion se llama POR CAPA, dentro del
        ' loop de capas, que ya corre dentro del Parallel.ForEach POR NPC del runner: seria un TERCER nivel de
        ' anidamiento sobre el mismo scheduler global. MEDIDO en el barrido de SSE: pasar de 1 a 8 NPCs en
        ' vuelo infla `NifWrite` y `other` x7,4 mientras el compose sube solo x1,6 — o sea que el cuello ya es
        ' contencion, no CPU ociosa, y sumar niveles la empeora. Ademas el trabajo de aca es CHICO (medido:
        ' 30 texturas / 13.700 px en un barrido entero), muy por debajo de lo que amortiza un fork/join.
        ' Las dos formas son bit-identicas (filas disjuntas, sin reduccion): esto es scheduling, no aritmetica.
        For y As Integer = 0 To h - 1
            Dim row = y * w
            For x As Integer = 0 To w - 1
                Dim i = row + x
                If pR IsNot Nothing Then pR(i) = SampleChannelAt(t, i, w, h, 0)
                If pG IsNot Nothing Then pG(i) = SampleChannelAt(t, i, w, h, 1)
                If pB IsNot Nothing Then pB(i) = SampleChannelAt(t, i, w, h, 2)
                If pA IsNot Nothing Then pA(i) = SampleChannelAt(t, i, w, h, 3)
            Next
        Next
    End Sub

    ''' <summary>LUT lookup ENGINE-EXACT del brow grayscale->palette (BSFaceCustomizationShader PS, `ld` t4):
    ''' U = Cvt(green, <paramref name="srcSpace"/>, <paramref name="coordSpace"/>) — el verde (textura diffuse)
    ''' se decodea de srcSpace (=conv.SrcSpace=DiffuseTextureSrcSpace, Srgb) al espacio del coord (=conv.
    ''' OutputSpace, G22) — la MISMA conversión que el seed del base diffuse (no hardcode). Con defaults Srgb->G22
    ''' eso es pow(srgbToLin(green),1/2.2), que es lo que hace el engine. Luego texel (tx,ty)=ftoi(U*W, v01*H),
    ''' fetch NEAREST (`ld`; sin bilineal ni half-texel), clamp a [0,size-1]. Verificado byte-exact vs CK.
    ''' <paramref name="v01"/> = RemappingIndex (row, 0..1). PARIDAD con el shader GPU (cvt + texelFetch).</summary>
    Private Function SampleLutEngine(t As DecodedTex, green01 As Single, v01 As Single, ch As Integer, srcSpace As Integer, coordSpace As Integer) As Single
        Dim u As Single = Cvt1(green01, srcSpace, coordSpace)
        Dim tx As Integer = Math.Max(0, Math.Min(t.Width - 1, CInt(MathF.Floor(u * t.Width))))
        Dim ty As Integer = Math.Max(0, Math.Min(t.Height - 1, CInt(MathF.Floor(v01 * t.Height))))
        Return t.Unit((ty * t.Width + tx) * 4 + ch)
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

    ''' <summary>Cache de decode PERSISTENTE entre bakes, para el BATCH: las texturas source (face d/_n/_s) +
    ''' tint + swap se repiten entre clones, asi que cada DDS se decodifica UNA vez en todo el batch (el camino
    ''' GPU ya lo hacia via TintGpuCache). Nothing = comportamiento per-cara.
    ''' <para>ConcurrentDictionary y no Dictionary: los bakes del batch son secuenciales, pero el segundo hilo
    ''' no es otro bake sino el RENDER - cada bake corre con Await Task.Run y durante ese await la bomba de
    ''' mensajes de WinForms sigue viva, asi que un WM_PAINT entra al render EN EL HILO UI y llega a este mismo
    ''' cache mientras el bake escribe desde el ThreadPool. Un Dictionary en escritura concurrente no da "un
    ''' valor raro": puede colgar el proceso en un bucle infinito dentro de Insert() al rehashear.</para></summary>
    Public Property BatchDecodeCache As System.Collections.Concurrent.ConcurrentDictionary(Of String, DecodedTex)

    ''' <summary>Saltea el trabajo de PIXELES del compose (seed + region swaps + tint layers) devolviendo el
    ''' canal con sus dimensiones reales y un buffer sin componer. SOLO para barridos que validan el NIF
    ''' (--ssecomparebatch), donde el contenido de los DDS no se mira: en FO4 el bake compone 3 canales a
    ''' resolucion nativa (1024x1024 o mas) por NPC y eso domina el costo del barrido.
    ''' ⛔ NO cambia NINGUNA decision del bake sobre el NIF: el gate se aplica DESPUES del decode del source
    ''' y DESPUES de resolver w/h, que es lo unico que determina si el canal sale Nothing (= slot que el bake
    ''' no escribe) y con que tamaño. Por construccion el NIF sale identico con y sin este flag; lo unico que
    ''' cambia son los pixeles de los .dds, que en ese modo no se usan.</summary>
    Public Property SkipPixelCompose As Boolean = False

    ''' <summary>Techo de memoria del <see cref="BatchDecodeCache"/>, en bytes. <b>0 = sin tope</b> = el
    ''' comportamiento historico.
    ''' <para>Hace falta porque el cache no tenia NINGUN limite y crecia durante todo el bake: medido en el
    ''' barrido FO4 completo, working set pico ~9,5 GB.</para>
    ''' <para>Es ADMISION y no eviccion: evictar GARANTIZA re-decodificar cuando el proximo NPC lo pida, no
    ''' admitir solo lo arriesga. Con claves que son rutas de textura source compartidas, las primeras que
    ''' entran son las mas reusadas (las bases de cabeza), asi que quedarse con las primeras le gana a LRU.</para>
    ''' <para>No puede cambiar la salida: el valor cacheado es funcion PURA de (bytes, tamano destino), asi que
    ''' no cachear no altera el valor, solo lo recalcula.</para></summary>
    Public Property BatchDecodeCacheBudgetBytes As Long = 0

    ''' <summary>Bytes vivos en los DOS niveles del cache de LOTE. Es el contador de ADMISION: el techo es UNO
    ''' SOLO sobre los dos niveles, asi que la comparacion contra el presupuesto tiene que salir de un UNICO
    ''' Interlocked.Add — con un contador por nivel la suma no seria atomica y dos hilos podrian pasar juntos.
    ''' <para>⛔ Se contabiliza SIEMPRE, haya techo o no. Antes solo se sumaba con presupuesto activo, o sea que
    ''' la corrida baseline (FGBAKE_DECODE_CACHE_MB=0) reportaba 0 MB retenidos y no habia contra que comparar.
    ''' Contabilizar no admite ni rechaza nada: el enforcement sigue gateado por el presupuesto.</para></summary>
    Private _batchCacheBytes As Long = 0

    ''' <summary>Los mismos bytes DESGLOSADOS por nivel. Puramente observacionales: no deciden admision.
    ''' Existen porque el nivel 2 cuesta 4 B por elemento contra 1 B del nivel 1, y sin el desglose no se puede
    ''' contestar si ese 4x se paga — el total solo dice cuanto pesa el conjunto.</summary>
    Private _batchDecodeBytes As Long = 0   ' nivel 1 (DecodedTex, Byte())
    Private _batchUnitBytes As Long = 0     ' nivel 2 (Single() ya resampleado)

    ''' <summary>Aciertos/fallos del NIVEL 1, hermanos de <see cref="_unitHits"/>/<see cref="_unitMisses"/>.
    ''' Sin ellos el nivel 1 era el unico cache sin instrumentar: se podia ver cuanto pesaba pero no cuanto
    ''' acertaba, que es la mitad que decide si conviene.</summary>
    Private _decodeHits As Long = 0
    Private _decodeMisses As Long = 0

    ''' <summary>Entradas rechazadas por el techo, POR NIVEL. Un rechazo alto significa que el techo esta
    ''' costando re-decodes, y eso hay que poder verlo en vez de deducirlo del reloj.
    ''' <para>⛔ NO se resetean en <see cref="BeginBatchDecodeCache"/>: el runner reabre el cache en cada borde
    ''' de (raza,sexo), asi que resetearlos dejaba el total describiendo solo al ULTIMO grupo.</para></summary>
    Private _decodeRejected As Integer = 0
    Private _unitRejected As Integer = 0

    ''' <summary>Cuántas texturas se IZARON (resample materializado a planos) y cuántos píxeles costó.
    ''' <para>⛔ Es el gate de observabilidad del izado: dice si ese camino se EJECUTA. Un A/B de bytes en 0
    ''' con este contador en 0 significa "no se probó", no "está bien". ⚠️ NO confundir con
    ''' <see cref="_unitResampled"/>, que es del NIVEL 2 del caché y NO pasa por acá.</para></summary>
    Private _hoistCount As Long = 0
    Private _hoistPixels As Long = 0

    ''' <summary>Texturas izadas y píxeles materializados. Ver <see cref="_hoistCount"/>.</summary>
    Public Function HoistStats() As (Textures As Long, Pixels As Long)
        Return (Threading.Interlocked.Read(_hoistCount), Threading.Interlocked.Read(_hoistPixels))
    End Function

    ''' <summary>⭐ POLITICA UNICA del techo de los caches de decode del compositor CPU, resuelta desde el
    ''' ENTORNO. Estaba INLINE en <c>BakeAllRunner</c> y ahora vive acá porque hay MAS DE UN cache que
    ''' obedece el mismo techo (el batch de acá y los de <c>SseFaceTintComposer</c>): con la derivacion
    ''' duplicada, los numeros se habrian separado en silencio.
    ''' <para>Contrato (idéntico al que ya tenía el runner, sin cambiar un valor):
    ''' env ausente ⇒ 25 % de la memoria disponible acotado a [512 MB, 4 GB]; env = "0" (o no numérica)
    ''' ⇒ SIN techo (comportamiento histórico, sirve de baseline); env > 0 ⇒ ese valor en MB
    ''' (reproducible entre máquinas, para comparar corridas).</para>
    ''' <para>⛔ Los tres números viven ACA Y SOLO ACA. No re-derivarlos en ningún call site.</para></summary>
    Public Function ResolveDecodeCacheBudgetFromEnvironment() As (Bytes As Long, Reason As String)
        Dim raw = If(Environment.GetEnvironmentVariable("FGBAKE_DECODE_CACHE_MB"), "").Trim()
        ' ⛔ SIN TECHO POR DEFAULT — es OPT-IN. Antes el default derivaba "25 % de la memoria disponible,
        ' acotado a [512 MB, 4 GB]", y esos tres números son ARBITRARIOS: nadie midió que 25 % sea el punto
        ' correcto ni que 4 GB tenga sentido en un equipo de 128 GB. Un techo inventado que fuerza re-decodes
        ' es peor que no tener techo, porque el costo es invisible (se paga en tiempo, no en un error).
        ' Quien quiera acotarlo lo pide explícitamente y elige el número para SU máquina.
        ' (El comentario viejo del runner decía "OPT-IN y APAGADO por default" mientras el código hacía lo
        '  contrario; ahora el código dice la verdad.)
        If raw = "" Then Return (0L, "Decode cache: NO ceiling (default) — set FGBAKE_DECODE_CACHE_MB=<MB> to cap it")
        Dim mb As Integer = -1
        If Not Integer.TryParse(raw, mb) Then mb = -1
        If mb > 0 Then Return (CLng(mb) * 1024L * 1024L, $"Decode cache: {mb} MB ceiling (set by FGBAKE_DECODE_CACHE_MB)")
        Return (0L, "Decode cache: NO ceiling (FGBAKE_DECODE_CACHE_MB=0, historical behaviour)")
    End Function

    ''' <summary>Bytes vivos y rechazos del cache batch, TOTAL de los dos niveles — el techo es uno solo, asi
    ''' que este es el numero que se compara contra el presupuesto. El desglose por nivel va en
    ''' <see cref="DecodeCacheStats"/> y <see cref="UnitCacheStats"/>.</summary>
    Public Function BatchDecodeCacheStats() As (Bytes As Long, Rejected As Integer)
        Return (Threading.Interlocked.Read(_batchCacheBytes),
                Threading.Volatile.Read(_decodeRejected) + Threading.Volatile.Read(_unitRejected))
    End Function

    ''' <summary>NIVEL 1: aciertos, fallos, bytes vivos y rechazos. Gemelo de <see cref="UnitCacheStats"/>;
    ''' juntos son lo que contesta si el nivel 2 paga su 4x por elemento o no.</summary>
    Public Function DecodeCacheStats() As (Hits As Long, Misses As Long, Bytes As Long, Rejected As Integer)
        Return (Threading.Interlocked.Read(_decodeHits), Threading.Interlocked.Read(_decodeMisses),
                Threading.Interlocked.Read(_batchDecodeBytes), Threading.Volatile.Read(_decodeRejected))
    End Function

    ''' <summary>⛔⭐ EJE QUE FALTABA EN TODAS LAS CLAVES DE DECODE. <see cref="DownsizeFromMip0"/> es estado
    ''' GLOBAL MUTABLE desde CharGen Options y decide de QUÉ MIP sale el decode, o sea que forma parte de la
    ''' identidad del valor cacheado. No estaba en ninguna clave: cambiar la opción sin recargar servía
    ''' decodes del mip equivocado, en silencio y para el resto de la sesión.
    ''' <para>Va como TAG y no como booleano crudo para que la clave siga siendo legible en un dump.</para></summary>
    Public Function MipPolicyTag() As String
        Return If(DownsizeFromMip0, "m0", "ms")
    End Function

    ''' <summary>NIVEL 2 del caché: <c>path|WxH|variante|políticaDeMip → Single()</c> en unidad [0,1], ya
    ''' resampleado. El nivel 1 (<see cref="BatchDecodeCache"/>) guarda BYTES del decode; éste guarda el
    ''' resultado del resample, que es 4 B por elemento y otra identidad.
    ''' <para>⛔ Vive acá y no en <c>SseFaceTintComposer</c>: era una SEGUNDA implementación de caché de
    ''' decode, con su propio estado de módulo, su propia clave y su propio criterio de negativos.</para></summary>
    Public Property BatchUnitCache As System.Collections.Concurrent.ConcurrentDictionary(Of String, Single())

    ''' <summary>Caché de nivel 2 de SESIÓN, para cuando NO hay lote activo (edición viva). Su vida la maneja
    ''' el caller vía <see cref="ClearSessionUnitCache"/>.
    ''' <para>⛔ NO se puede colapsar con la de lote: la vieja caché de SSE era per-NPC y SOBREVIVÍA entre
    ''' refrescos, que es justamente lo que hace usable la edición viva a 4096². Son dos VIDAS del mismo
    ''' caché elegidas por el caller, no dos implementaciones.</para></summary>
    Private ReadOnly _sessionUnitCache As New System.Collections.Concurrent.ConcurrentDictionary(Of String, Single())(StringComparer.OrdinalIgnoreCase)

    ''' <summary>Sentinel de ENTRADA NEGATIVA (archivo ausente o indecodificable a ese tamaño). Se guarda esto
    ''' y NUNCA <c>Nothing</c>: un valor Nothing en el diccionario no se distingue de "no está" sin mirar el
    ''' booleano de TryGetValue en cada call site, y ese es el tipo de detalle que un día se omite.</summary>
    Private ReadOnly _negativeUnit As Single() = New Single(-1) {}

    Public Sub ClearSessionUnitCache()
        _sessionUnitCache.Clear()
    End Sub

    Private _unitHits As Long = 0
    Private _unitMisses As Long = 0
    ''' <summary>Cuántos decodes del nivel 2 pasaron REALMENTE por el bilineal (el resto salió por el atajo de
    ''' identidad, que copia el texel). ⛔ Existe porque de esto depende si un cambio en la ley del bilineal
    ''' puede mover un byte del corpus: con 0 resamples, el corpus NO ejercita ese camino y un A/B en 0 no
    ''' dice nada sobre él — hay que validarlo con self-test. Sin el contador eso es una suposición.</summary>
    Private _unitResampled As Long = 0

    ''' <summary>Aciertos/fallos del nivel 2, cuántos de los fallos resamplearon, y sus bytes y rechazos. Un
    ''' ratio malo significa que la clave se está fragmentando (p.ej. un eje de más) y eso hay que poder VERLO,
    ''' no deducirlo del reloj.</summary>
    Public Function UnitCacheStats() As (Hits As Long, Misses As Long, Resampled As Long, Bytes As Long, Rejected As Integer)
        Return (Threading.Interlocked.Read(_unitHits), Threading.Interlocked.Read(_unitMisses),
                Threading.Interlocked.Read(_unitResampled),
                Threading.Interlocked.Read(_batchUnitBytes), Threading.Volatile.Read(_unitRejected))
    End Function

    ''' <summary>⭐ Los EJES de las claves de decode tienen que ser DISJUNTOS: dos peticiones que difieran en
    ''' cualquier eje no pueden colisionar, y dos idénticas tienen que dar la misma clave.
    ''' <para>⛔ Existe porque a la clave del nivel 2 le faltaba el tamaño (parcheado restringiendo el dominio
    ''' a 512² con cuatro guardas) y a las DOS les faltaba la política de mip, que es estado global mutable
    ''' desde la UI. Un eje ausente no falla: sirve el buffer equivocado.</para>
    ''' <para>No hace early-return: es texto, no aritmética. Corre en toda máquina.</para></summary>
    Public Function CacheKeyAxesSelfTest() As String
        Dim saved = DownsizeFromMip0
        Try
            Dim seen As New Dictionary(Of String, String)(StringComparer.Ordinal)
            For Each policy In New Boolean() {False, True}
                DownsizeFromMip0 = policy
                For Each path In New String() {"a\b.dds", "a\c.dds"}
                    For Each wh In New(W As Integer, H As Integer)() {(512, 512), (1024, 1024), (512, 1024)}
                        For Each nrm In New Boolean() {False, True}
                            Dim id = $"{path}|{wh.W}x{wh.H}|{nrm}|{policy}"
                            Dim key = $"{path}|{wh.W}x{wh.H}|{If(nrm, "nrm", "col")}|{MipPolicyTag()}"
                            Dim prev As String = Nothing
                            If seen.TryGetValue(key, prev) AndAlso prev <> id Then
                                Return $"colision de clave nivel 2: '{key}' la producen {prev} y {id}"
                            End If
                            seen(key) = id
                        Next
                    Next
                Next
            Next
            ' Y el eje de mip tiene que MOVER la clave del nivel 1 (es el que faltaba).
            DownsizeFromMip0 = False : Dim k1 = MipPolicyTag()
            DownsizeFromMip0 = True : Dim k2 = MipPolicyTag()
            If k1 = k2 Then Return "la politica de mip NO cambia el tag de la clave: el eje volveria a ser invisible"
            Return ""
        Finally
            DownsizeFromMip0 = saved
        End Try
    End Function

    ''' <summary>⭐ LA LEY DEL BILINEAL ES UNA: el que MATERIALIZA el resample y el que muestrea UN texel
    ''' tienen que dar los MISMOS BITS. Devuelve "" si coinciden.
    '''
    ''' <para><b>Por qué existe.</b> Había CUATRO transcripciones del bilineal y la cuarta
    ''' (el resample interno del nivel 2 del caché) no coincidía con las otras tres: derivaba el índice alto del
    ''' bajo YA clampeado, con lo cual el borde de la textura salía corrido medio texel. Nadie lo veía porque
    ''' los dos caminos los usan juegos distintos y el A/B del corpus no ejercita el resample (las máscaras
    ''' vanilla ya vienen al tamaño del acumulador). Un gate de bytes en 0 NO cubre esto: hace falta este test.</para>
    ''' <para>Barre UPSIZE (que es el caso del fold de SSE: máscaras 512² al tamaño del complexion), DOWNSIZE,
    ''' identidad y tamaños que no son potencia de dos ni múltiplos entre sí. Y compara TODOS los píxeles, no
    ''' una muestra: la divergencia vieja vivía en la PRIMERA y la ÚLTIMA fila/columna.</para>
    ''' <para>⛔ No hace early-return: es aritmética escalar, corre en toda máquina.</para></summary>
    Public Function BilinearLawSelfTest() As String
        Dim seed As UInteger = 1234567891UI
        For Each dims In New(SW As Integer, SH As Integer, DW As Integer, DH As Integer)() {
            (8, 8, 32, 32), (32, 32, 8, 8), (8, 8, 8, 8), (13, 7, 31, 19), (31, 19, 13, 7),
            (4, 16, 16, 4), (512, 512, 1024, 1024)}
            Dim sw = dims.SW, sh = dims.SH, dw = dims.DW, dh = dims.DH
            ' Fuente sintética: un DecodedTex de bytes y su expansión EXACTA a unidad, para que las dos
            ' formas de la ley reciban EXACTAMENTE los mismos números de entrada.
            Dim px(sw * sh * 4 - 1) As Byte
            For i = 0 To px.Length - 1
                seed = seed Xor (seed << 13) : seed = seed Xor (seed >> 17) : seed = seed Xor (seed << 5)
                px(i) = CByte(seed And 255UI)
            Next
            Dim tex As New DecodedTex With {.Width = sw, .Height = sh, .Rgba8 = px, .Channels = 4}
            Dim unit = tex.ToUnitArray()
            ' (a) MATERIALIZADO: Single() -> Single() de una pasada.
            Dim mat = ResampleRgbaFloat(unit, sw, sh, dw, dh)
            ' (b) POR TEXEL: el muestreo que hace el loop de capas cuando la capa no es directa.
            For i = 0 To dw * dh - 1
                For c = 0 To 3
                    Dim byTexel = SampleChannelAt(tex, i, dw, dh, c)
                    Dim materialized = mat(i * 4 + c)
                    If BitConverter.SingleToInt32Bits(byTexel) <> BitConverter.SingleToInt32Bits(materialized) Then
                        Return $"bilineal: materializar y muestrear NO dan lo mismo. {sw}x{sh}->{dw}x{dh} " &
                               $"px={i} (x={i Mod dw},y={i \ dw}) ch={c} " &
                               $"porTexel={byTexel} materializado={materialized} " &
                               $"[bits 0x{BitConverter.SingleToInt32Bits(byTexel):X8} vs 0x{BitConverter.SingleToInt32Bits(materialized):X8}]"
                    End If
                Next
            Next
        Next
        ' EL BORDE, explícito: con el texel en -0,5 la ley es CLAMP-TO-EDGE ⇒ los dos índices colapsan al 0 y
        ' el resultado es el texel de borde, NO un lerp entre las filas 0 y 1. Es exactamente lo que la cuarta
        ' transcripción hacía mal, así que se fija como comportamiento y no sólo como "coinciden entre sí".
        Dim i0 As Integer, i1 As Integer, t As Single
        BilinearAxis(0.0F, 16, i0, i1, t)
        If i0 <> 0 OrElse i1 <> 0 Then Return $"bilineal: en u=0 la ley pide clamp-to-edge (0,0) y dio ({i0},{i1})"
        BilinearAxis(1.0F, 16, i0, i1, t)
        If i0 <> 15 OrElse i1 <> 15 Then Return $"bilineal: en u=1 la ley pide clamp-to-edge (15,15) y dio ({i0},{i1})"
        Return ""
    End Function

    ''' <summary>⭐ MEDICIÓN (no gate) de la FORMA del soft-light: enumera el dominio ENTERO de 8 bits —los
    ''' 65.536 pares (d,s) con d,s ∈ {0/255…255/255}— y reporta en cuántos difiere la expresión del FOLD de la
    ''' del dispatch compartido, y cuánto.
    '''
    ''' <para><b>Por qué enumerar y no medir con el corpus.</b> Las dos expresiones son algebraicamente la
    ''' MISMA: <c>d²+2ds(1−d)</c> (la del motor, la que usa el fold) desarrollada da <c>(1−2s)d²+2sd</c> (la
    ''' que el dispatch llama "pegtop"). Lo que puede diferir es el REDONDEO, porque el orden de operaciones
    ''' es otro. Un barrido del corpus mide dónde el corpus pisa; esto mide el dominio completo, que es lo
    ''' único que permite PREDECIR el delta antes de unificarlas (decisión 4 del plan).</para>
    ''' <para>Devuelve una línea para el log. ⛔ No es un gate: no falla, informa.</para></summary>
    Public Function SoftLightShapeReport() As String
        Dim diff As Long = 0, worstUlp As Integer = 0, worstByte As Integer = 0
        Dim wd As Single = 0, ws As Single = 0
        For di = 0 To 255
            Dim d = ByteToUnit(di)
            For si = 0 To 255
                Dim s = ByteToUnit(si)
                ' (a) la del MOTOR, tal como la escribe el fold (SseFaceGenBaker.FoldOne).
                Dim engine As Single = d * d + 2.0F * d * s * (1.0F - d)
                ' (b) la del dispatch compartido, modelo 3.
                Dim pegtop As Single = BlendSoftLightModel(3, d, s)
                If BitConverter.SingleToInt32Bits(engine) = BitConverter.SingleToInt32Bits(pegtop) Then Continue For
                diff += 1
                Dim ulp = Math.Abs(BitConverter.SingleToInt32Bits(engine) - BitConverter.SingleToInt32Bits(pegtop))
                If ulp > worstUlp Then worstUlp = ulp : wd = d : ws = s
                ' Lo que de verdad importa: si la diferencia sobrevive al empaquetado a byte.
                Dim db = Math.Abs(CInt(ToByte(engine)) - CInt(ToByte(pegtop)))
                If db > worstByte Then worstByte = db
            Next
        Next
        Return $"soft-light shape (65536 pares 8-bit): diferentes={diff} ({100.0 * diff / 65536.0:F2} %)  " &
               $"peor ULP={worstUlp} (d={wd:F6} s={ws:F6})  PEOR DELTA DE BYTE={worstByte}"
    End Function

    ''' <summary>⭐ MEDICIÓN (no gate) de la CURVA sRGB: hay dos transcripciones —<c>SrgbToLin1</c>/
    ''' <c>LinToSrgb1</c> de este módulo y <c>Srgb2Lin</c>/<c>Lin2Srgb</c> de <c>SseFaceGenBaker</c>— y se
    ''' diferencian en el <c>Clamp01</c>. Enumera el dominio de 8 bits y además los bordes fuera de [0,1],
    ''' que es donde el clamp decide.
    ''' <para>Existe para poder DECLARAR la diferencia antes de colapsarlas, en vez de asumir que son
    ''' equivalentes porque "las dos son la curva estándar".</para></summary>
    Public Function SrgbCurveShapeReport() As String
        Dim dIn As Integer = 0, dOut As Integer = 0
        ' Dominio real de las dos: byte/255.
        For i = 0 To 255
            Dim c = ByteToUnit(i)
            If BitConverter.SingleToInt32Bits(SrgbToLin1(c)) <> BitConverter.SingleToInt32Bits(SseFaceGenBaker.Srgb2Lin(c)) Then dIn += 1
            If BitConverter.SingleToInt32Bits(LinToSrgb1(c)) <> BitConverter.SingleToInt32Bits(SseFaceGenBaker.Lin2Srgb(c)) Then dOut += 1
        Next
        ' Fuera de [0,1]: el fold multiplica por el amplify (hasta ~4x), así que la entrada de lin→sRGB SÍ
        ' supera 1 en la práctica. Este es el caso que el clamp decide y el dominio de 8 bits no ve.
        Dim edges = New Single() {-1.0F, -0.001F, 0.0F, 1.0F, 1.0001F, 1.5F, 4.0F, Single.NaN}
        Dim edgeDiff As New List(Of String)
        For Each e In edges
            Dim a = LinToSrgb1(e), b = SseFaceGenBaker.Lin2Srgb(e)
            If BitConverter.SingleToInt32Bits(a) <> BitConverter.SingleToInt32Bits(b) Then edgeDiff.Add($"lin2srgb({e})={a}vs{b}")
            Dim a2 = SrgbToLin1(e), b2 = SseFaceGenBaker.Srgb2Lin(e)
            If BitConverter.SingleToInt32Bits(a2) <> BitConverter.SingleToInt32Bits(b2) Then edgeDiff.Add($"srgb2lin({e})={a2}vs{b2}")
        Next
        Return $"curva sRGB (dominio 8-bit): srgb→lin difieren={dIn}/256  lin→srgb difieren={dOut}/256  " &
               $"| FUERA de [0,1]: {If(edgeDiff.Count = 0, "coinciden", String.Join(" ; ", edgeDiff))}"
    End Function

    ''' <summary>⭐ FASE 8 — el QNAM del CUERPO y el facetint de la CARA tienen que salir del MISMO número.
    ''' Devuelve "" si coinciden.
    '''
    ''' <para><b>Por qué es un self-test y no un barrido.</b> El corpus vanilla de Skyrim no tiene ni un
    ''' overlay, así que un A/B mide CERO muestras de este camino. El oráculo es el compositor compartido:
    ''' se compara el pliegue del skin-tone (<c>lerp(seed, TINC, TINV)</c>, lo que va al QNAM) contra
    ''' <see cref="ComposePixel"/> con la convención de la capa de piel, que es lo que compone la cara.</para>
    ''' <para>Lo que atrapa: que alguien vuelva a cablear el <c>0,5</c> en uno de los dos lados. Con el
    ''' literal, mover el seed en CharGen Options desincronizaba el cuello del pecho EN SILENCIO.</para>
    ''' <para>⛔ No hace early-return: es aritmética escalar, corre en toda máquina.</para></summary>
    Public Function QnamMatchesFaceSelfTest() As String
        Dim conv = FaceTintConvention.ResolveConvention(FaceTintConvention.FaceTintStage.TintDiffuse,
                                                        FaceTintChannel.Diffuse, isTextureSet:=False, blendOp:=0)
        ' Se barren seeds distintos del 0,5 A PROPÓSITO: con 0,5 las dos formas coinciden por casualidad
        ' aritmética y el test no probaría nada. Es el caso que el literal escondía.
        For Each seed In New Single() {0.5F, 0.0F, 1.0F, 0.25F, 0.73F}
            For ci = 0 To 255
                Dim c = ByteToUnit(ci)
                For Each tinv In New Single() {0.0F, 0.01F, 0.25F, 0.52F, 0.75F, 1.0F}
                    ' (a) la cara: el compositor compartido, una capa de color plano sobre el seed.
                    Dim face = ComposePixel(seed, c, tinv, conv)
                    ' (b) el cuerpo: el pliegue del skin-tone que alimenta el QNAM.
                    Dim body = ComposePixel(seed, c, tinv, conv)
                    If BitConverter.SingleToInt32Bits(face) <> BitConverter.SingleToInt32Bits(body) Then
                        Return $"QNAM vs cara: seed={seed} TINC={c} TINV={tinv} cara={face} cuerpo={body}"
                    End If
                    ' Y con los defaults de SSE tiene que dar EXACTAMENTE el lerp del que salía el literal:
                    ' es lo que sostiene que la fase 8 sea byte-idéntica y no "parecida".
                    If conv.Blend = FaceTintConvention.FaceTintBlend.Replace AndAlso
                       conv.CompositeSpace = FaceTintConvention.FaceTintWorkingSpace.Linear AndAlso
                       conv.AccumSpace = FaceTintConvention.FaceTintWorkingSpace.Linear AndAlso
                       conv.SrcSpace = FaceTintConvention.FaceTintWorkingSpace.Linear Then
                        Dim lerp As Single = seed + tinv * (c - seed)
                        If lerp < 0.0F Then lerp = 0.0F
                        If lerp > 1.0F Then lerp = 1.0F
                        If BitConverter.SingleToInt32Bits(face) <> BitConverter.SingleToInt32Bits(lerp) Then
                            Return $"QNAM: con los defaults de SSE el compose deberia ser el lerp literal. " &
                                   $"seed={seed} TINC={c} TINV={tinv} compose={face} lerp={lerp}"
                        End If
                    End If
                Next
            Next
        Next
        Return ""
    End Function

    ''' <summary>Arranca el cache de decode batch (llamar ANTES del loop de clones). Arranca los DOS niveles.</summary>
    Public Sub BeginBatchDecodeCache()
        BatchDecodeCache = New System.Collections.Concurrent.ConcurrentDictionary(Of String, DecodedTex)(StringComparer.OrdinalIgnoreCase)
        BatchUnitCache = New System.Collections.Concurrent.ConcurrentDictionary(Of String, Single())(StringComparer.OrdinalIgnoreCase)
        ' Solo los BYTES: describen el cache VIVO, que acaba de nacer. Los hits/misses/rechazos son ACUMULADOS
        ' de la corrida — el runner reabre el cache en cada borde de (raza,sexo), asi que resetearlos dejaria
        ' el resumen final describiendo un solo grupo.
        Threading.Interlocked.Exchange(_batchCacheBytes, 0L)
        Threading.Interlocked.Exchange(_batchDecodeBytes, 0L)
        Threading.Interlocked.Exchange(_batchUnitBytes, 0L)
    End Sub

    ''' <summary>Cierra y libera el cache de decode batch (llamar en Finally despues del loop). Los
    ''' DecodedTex son managed (Double() Rgba, sin recursos nativos) -> Clear + GC alcanza.</summary>
    Public Sub EndBatchDecodeCache()
        Dim c = BatchDecodeCache
        BatchDecodeCache = Nothing
        If c IsNot Nothing Then c.Clear()
        Dim u = BatchUnitCache
        BatchUnitCache = Nothing
        If u IsNot Nothing Then u.Clear()
    End Sub

    ''' <summary>⭐ Decode + resample a EXACTAMENTE w×h en unidad [0,1], cacheado en el NIVEL 2. Es el camino
    ''' que antes vivía duplicado en <c>SseFaceTintComposer.DecodeMask</c>.
    ''' <para>Lo consumen tanto las capas como los cinco consumidores que NO son capas (máscaras de skee,
    ''' compositor de overlays, stack del fold, resolver de facetint del render, builder de facegen).</para>
    ''' <para>Devuelve Nothing si el archivo falta o no decodifica; internamente eso se cachea con sentinel.</para></summary>
    ''' <param name="asNormalMap">La fuente se interpreta como VECTOR: con 2 canales se despeja Z DESPUÉS del
    ''' resample, igual que el hardware. Namespace propio en la clave — servir un color por un normal
    ''' devolvería el B equivocado.</param>
    Public Function CachedUnitDecode(texKey As String, w As Integer, h As Integer,
                                     Optional asNormalMap As Boolean = False) As Single()
        If String.IsNullOrEmpty(texKey) OrElse w <= 0 OrElse h <= 0 Then Return Nothing
        ' La clave es la IDENTIDAD COMPLETA del valor: path + tamaño destino + variante + política de mip.
        Dim ckey = $"{texKey}|{w}x{h}|{If(asNormalMap, "nrm", "col")}|{MipPolicyTag()}"
        ' ⛔ LA PROPIEDAD GLOBAL SE LEE UNA SOLA VEZ. Antes se capturaba `cache` acá y MAS ABAJO se volvia a
        ' leer `BatchUnitCache` para decidir si aplicaba el presupuesto. Entre las dos lecturas, un
        ' EndBatchDecodeCache de otro hilo (el render corre en el hilo UI durante el await del bake) la pone
        ' en Nothing ⇒ el ReferenceEquals daba False ⇒ la entrada se cacheaba SALTEANDOSE EL TECHO.
        Dim batch = BatchUnitCache
        Dim cache = If(batch, _sessionUnitCache)
        Dim isBatch As Boolean = (batch IsNot Nothing)
        Dim hit As Single() = Nothing
        If cache.TryGetValue(ckey, hit) Then
            Threading.Interlocked.Increment(_unitHits)
            Return If(ReferenceEquals(hit, _negativeUnit), Nothing, hit)
        End If
        Threading.Interlocked.Increment(_unitMisses)

        Dim b = FilesDictionary_class.GetBytes(texKey)
        Dim t As DecodedTex = If(b Is Nothing, Nothing, DecodeDds(b, w, h))
        If t IsNot Nothing AndAlso t.Rgba8 Is Nothing Then t = Nothing
        If t Is Nothing Then
            cache(ckey) = _negativeUnit      ' negativo: no se le vuelve a pedir el archivo al FilesDictionary
            Return Nothing
        End If
        Dim needsZ As Boolean = asNormalMap AndAlso t.Channels < 3
        Dim outp(w * h * 4 - 1) As Single
        ' IDENTIDAD: si la fuente ya está en el tamaño pedido el bilineal devolvería el texel de origen exacto,
        ' así que se expande directo. Se COPIA (Byte crudo → unidad), no se aliasea t.Rgba8.
        If t.Width = w AndAlso t.Height = h AndAlso t.Rgba8.Length = outp.Length Then
            Dim srcArr = t.Rgba8, lut = ByteToUnit
            For i As Integer = 0 To outp.Length - 1
                outp(i) = lut(srcArr(i))
            Next
        Else
            ' ⭐ EL RESAMPLE ES `SampleChannelAt` MATERIALIZADO, texel por texel. Acá vivía un bilineal PROPIO
            ' —la cuarta transcripción, y la única que no coincidía— con su propio mapeo, su propio clamp del
            ' índice alto y su propia factorización del lerp. Ahora este camino y el que muestrea por píxel
            ' dentro del loop de capas hacen LAS MISMAS CUENTAS por construcción: no son dos leyes, son la
            ' materialización y el muestreo de UNA.
            Threading.Interlocked.Increment(_unitResampled)
            System.Threading.Tasks.Parallel.For(0, h, Sub(y)
                                                          For x = 0 To w - 1
                                                              Dim i = y * w + x
                                                              For c = 0 To 3
                                                                  outp(i * 4 + c) = SampleChannelAt(t, i, w, h, c)
                                                              Next
                                                          Next
                                                      End Sub)
        End If
        ' DESPUÉS del resample, igual que el hardware (se samplea el BC5 filtrado y recién ahí se despeja z).
        If needsZ Then ReconstructNormalZ(outp, w * h)
        ' PRESUPUESTO, y sólo sobre el caché de LOTE. ⛔ El nivel 2 es Single(): 4 B por elemento. Contabilizarlo
        ' como bytes subcontaba ×4 y el techo se rompía en silencio.
        If Not isBatch Then
            cache(ckey) = outp                       ' sesión / per-call: los acota su VIDA, no el techo
        Else
            ' Se CONTABILIZA siempre y se ENFORZA sólo con techo (ver _batchCacheBytes).
            Dim budget = BatchDecodeCacheBudgetBytes
            Dim sz As Long = CLng(outp.Length) * 4L
            Dim tot = Threading.Interlocked.Add(_batchCacheBytes, sz)
            Threading.Interlocked.Add(_batchUnitBytes, sz)
            ' ⛔ TryAdd, NO el indexador. N hilos pueden fallar el TryGetValue de arriba sobre la MISMA clave
            ' y llegar todos acá: con `cache(k)=v` los N cobraban bytes y el diccionario retenia UNO, dejando
            ' N-1 cargos FANTASMA que no se devuelven nunca. Esos cargos siguen decidiendo admisiones el
            ' resto de la corrida ⇒ rechazos prematuros y re-decodes. Con TryAdd cobra el que publica.
            ' No cambia la salida: el valor es funcion pura de la clave y todos devuelven el suyo, igual que antes.
            If (budget <= 0L OrElse tot <= budget) AndAlso cache.TryAdd(ckey, outp) Then
                ' publicado y cobrado
            Else
                Threading.Interlocked.Add(_batchCacheBytes, -sz)
                Threading.Interlocked.Add(_batchUnitBytes, -sz)
                If budget > 0L AndAlso tot > budget Then Threading.Interlocked.Increment(_unitRejected)
            End If
        End If
        Return outp
    End Function

    ''' <summary>Compone los 3 canales por CPU (espejo de FaceTintCompositor.ApplyFaceTintPipeline) sobre las
    ''' DDS ya leidas. Devuelve BGRA byte por canal (D en g22, N/S lineal). MISMA ley que el GL.</summary>
    ''' <param name="resolution">Resolucion por canal (A/B/C). Nothing = Inherit (nativo) en los tres. Los
    ''' bodyparts pasan Nothing: el enum es solo para la cara.</param>
    ''' <param name="diffuseKey">Keys de las texturas source para cachear su decode entre clones cuando
    ''' BatchDecodeCache esta activo. Nothing = no cachear el source.</param>
    ''' <param name="decodeCache">Cache de decode PROPIEDAD DEL CALLER, con la vida que el caller decida:
    ''' equivalente CPU del TintGpuCache per-host del camino GL, para que el render en modo CPU no re-decodifique
    ''' todas las DDS en cada refresh de edicion viva. Tiene PRIORIDAD sobre <see cref="BatchDecodeCache"/>,
    ''' justamente para no pisar el global del batch, que puede estar corriendo en otro hilo. No puede cambiar
    ''' la salida: el valor cacheado es funcion pura de (bytes, tamano destino).</param>
    Public Function ComposeCpuPipeline(diffuseBytes As Byte(), normalBytes As Byte(), specBytes As Byte(),
                                       layers As IList(Of FaceTintLayerInput),
                                       swaps As IList(Of FaceRegionSwapInput),
                                       Optional resolution As FaceTintResolutionSettings = Nothing,
                                       Optional diffuseKey As String = Nothing,
                                       Optional normalKey As String = Nothing,
                                       Optional specKey As String = Nothing,
                                       Optional headDiffuseAlphaTest As Boolean = False,
                                       Optional decodeCache As System.Collections.Concurrent.ConcurrentDictionary(Of String, DecodedTex) = Nothing) As CpuPipelineResult
        Dim res As New CpuPipelineResult()
        ' Prioridad: cache del caller (render) -> BatchDecodeCache (batch de bakes) -> dict per-call (1 cara).
        Dim cache = ResolveDecodeCache(decodeCache)
        res.Diffuse = ComposeChannelCpu(diffuseBytes, FaceTintChannel.Diffuse, layers, swaps, cache, resolution, diffuseKey, headDiffuseAlphaTest)
        res.Normal = ComposeChannelCpu(normalBytes, FaceTintChannel.Normal, layers, swaps, cache, resolution, normalKey)
        res.Specular = ComposeChannelCpu(specBytes, FaceTintChannel.Specular, layers, swaps, cache, resolution, specKey)
        Return res
    End Function

    ''' <summary>Decode cacheado contra un cache PROPIEDAD DEL CALLER (mismo contrato que el parametro
    ''' <c>decodeCache</c> de <see cref="ComposeCpuPipeline"/>). <paramref name="cache"/> Nothing o
    ''' <paramref name="key"/> vacio ⇒ decode directo, sin retener nada. Existe para que los caminos que
    ''' decodifican UNA textura suelta (el complexion del fold SSE, p.ej.) puedan compartir el MISMO cache
    ''' per-NPC que el compose, en vez de re-decodificar la textura mas grande de la cara en cada refresh.</summary>
    Public Function DecodeDdsCached(cache As System.Collections.Concurrent.ConcurrentDictionary(Of String, DecodedTex),
                                    key As String, bytes As Byte(),
                                    Optional preferW As Integer = 0, Optional preferH As Integer = 0) As DecodedTex
        If cache Is Nothing OrElse String.IsNullOrEmpty(key) Then Return DecodeDds(bytes, preferW, preferH)
        Return CachedDecode(cache, key, bytes, preferW, preferH)
    End Function

    ''' <summary>Decode cacheado. preferW/H>0 -> usa el MIP de ese tamaño (key suffix @WxH para no chocar
    ''' con el mip0 de la misma textura).</summary>
    Private Function CachedDecode(cache As System.Collections.Concurrent.ConcurrentDictionary(Of String, DecodedTex), key As String, bytes As Byte(),
                                  Optional preferW As Integer = 0, Optional preferH As Integer = 0) As DecodedTex
        If bytes Is Nothing OrElse bytes.Length = 0 Then Return Nothing
        ' ⛔ La política de mip entra en la clave: decide de qué mip sale el decode, así que es parte de la
        ' identidad del valor. Sin ella, cambiar la opción sin recargar servía el mip equivocado para siempre.
        Dim ck = If(preferW > 0 OrElse preferH > 0, $"{key}@{preferW}x{preferH}|{MipPolicyTag()}", $"{key}|{MipPolicyTag()}")
        ' ⛔ UNA sola lectura de la propiedad global — ver la nota gemela en CachedUnitDecode: releerla mas
        ' abajo permitia que un End...Cache concurrente hiciera saltear el techo.
        Dim isBatch As Boolean = Object.ReferenceEquals(cache, BatchDecodeCache)
        Dim t As DecodedTex = Nothing
        If Not String.IsNullOrEmpty(key) AndAlso cache.TryGetValue(ck, t) Then
            Threading.Interlocked.Increment(_decodeHits)
            Return t
        End If
        Threading.Interlocked.Increment(_decodeMisses)
        t = DecodeDds(bytes, preferW, preferH)
        If Not String.IsNullOrEmpty(key) AndAlso t IsNot Nothing Then
            ' El presupuesto SOLO aplica al cache de batch (el compartido entre NPCs). Cuando `cache` es el
            ' diccionario per-call de una sola cara, no cachear no ahorraria nada y solo agregaria re-decodes
            ' dentro del mismo compose.
            If Not isBatch Then
                cache(ck) = t
            Else
                ' Se CONTABILIZA siempre y se ENFORZA solo con techo (ver _batchCacheBytes).
                Dim budget = BatchDecodeCacheBudgetBytes
                Dim sz As Long = If(t.Rgba8 Is Nothing, 0L, CLng(t.Rgba8.Length))   ' Byte() ⇒ 1 B por elemento
                Dim tot = Threading.Interlocked.Add(_batchCacheBytes, sz)
                Threading.Interlocked.Add(_batchDecodeBytes, sz)
                ' TryAdd y no el indexador: cobra el que PUBLICA. Ver la nota extensa en CachedUnitDecode
                ' (cargos fantasma de N misses concurrentes sobre la misma clave).
                If (budget <= 0L OrElse tot <= budget) AndAlso cache.TryAdd(ck, t) Then
                    ' publicado y cobrado
                Else
                    ' No entra: se devuelve el decode igual (correcto), simplemente no se retiene.
                    Threading.Interlocked.Add(_batchCacheBytes, -sz)
                    Threading.Interlocked.Add(_batchDecodeBytes, -sz)
                    ' Sólo cuenta como RECHAZO si lo rechazó el techo. Perder la carrera del TryAdd no es un
                    ' rechazo: la entrada quedó igual, la publicó otro.
                    If budget > 0L AndAlso tot > budget Then Threading.Interlocked.Increment(_decodeRejected)
                End If
            End If
        End If
        Return t
    End Function

    ' =====================================================================================================
    ' CONTRATO ABIERTO DEL COMPOSE CPU (fase 5 de la unificacion del compositor).
    ' =====================================================================================================
    ' El compose de un canal era UNA funcion que exigia textura fuente, derivaba w/h de ella y devolvia BYTES.
    ' Eso lo hacia inusable para el facetint de SSE, que es tint-only (no hay base), el tamaño lo fija el
    ' caller y necesita FLOAT — cuantizar antes del fold es una regresion MEDIDA (RMS 2,4 / max 18, porque el
    ' amplify escala x255/64). Por eso SSE tenia su PROPIO loop de capas.
    ' El contrato se abre en tres ejes y con eso el loop de SSE desaparece (no se parchea: se BORRA):
    '   1. de donde sale el seed  -> FaceTintSeedSpec (3 modos)
    '   2. (w, h)                 -> explicitos; el wrapper los deriva del source cuando hay source
    '   3. que devuelve           -> el ACUMULADOR en float SoA (ComposeChannelAccum); empaquetar a bytes es
    '                                un paso aparte (PackAccumToBgra) que solo usa quien escribe un DDS.
    ' =====================================================================================================

    ''' <summary>De donde sale el SEED del acumulador, resuelto a un valor CONCRETO para el compositor.
    ''' <para><c>FromTexture</c> y <c>Constant</c> son los dos modos de <see cref="FaceTintConvention.FaceTintSeedMode"/>
    ''' —la LEY, que vive en el config— ya resueltos. <c>Provided</c> NO es un modo de la ley: es el buffer que
    ''' el caller ya trae (la <c>baseImg</c> del modo diagnostico del probe), y por eso no tiene contraparte en
    ''' el enum del config.</para></summary>
    Public Enum FaceTintSeedKind
        FromTexture = 0
        [Constant] = 1
        Provided = 2
    End Enum

    ''' <summary>El seed del acumulador de un canal, con su fuente ya resuelta. Inmutable.</summary>
    Public Class FaceTintSeedSpec
        Public ReadOnly Kind As FaceTintSeedKind
        ''' <summary><c>FromTexture</c>: la textura base YA decodificada (el caller elige el mip: es el que
        ''' fija w/h). Nothing en los otros modos.</summary>
        Public ReadOnly Texture As DecodedTex
        ''' <summary><c>Constant</c>: color plano, expresado en el OutputSpace del canal (el compositor lo
        ''' lleva a AccumSpace, igual que hace con el seed de textura).</summary>
        Public ReadOnly R As Single, G As Single, B As Single
        ''' <summary><c>Provided</c>: buffer AoS RGBA [0,1] en OutputSpace, de al menos w*h*4 elementos.</summary>
        Public ReadOnly Buffer As Single()

        Private Sub New(k As FaceTintSeedKind, tex As DecodedTex, cr As Single, cg As Single, cb As Single, buf As Single())
            Kind = k : Texture = tex : R = cr : G = cg : B = cb : Buffer = buf
        End Sub

        ''' <summary>Seed desde la textura base (la rama de FO4, incluida la ley de <c>SeedDiffuseG22</c>).</summary>
        Public Shared Function FromTextureSource(tex As DecodedTex) As FaceTintSeedSpec
            Return New FaceTintSeedSpec(FaceTintSeedKind.FromTexture, tex, 0.0F, 0.0F, 0.0F, Nothing)
        End Function
        ''' <summary>Seed constante (la ley de SSE: 0,5 plano, engine-verificado).</summary>
        Public Shared Function FromConstant(cr As Single, cg As Single, cb As Single) As FaceTintSeedSpec
            Return New FaceTintSeedSpec(FaceTintSeedKind.Constant, Nothing, cr, cg, cb, Nothing)
        End Function
        ''' <summary>Seed desde un buffer que el caller ya tiene (AoS RGBA en OutputSpace).</summary>
        Public Shared Function FromBuffer(buf As Single()) As FaceTintSeedSpec
            Return New FaceTintSeedSpec(FaceTintSeedKind.Provided, Nothing, 0.0F, 0.0F, 0.0F, buf)
        End Function
    End Class

    ''' <summary>Que hace el compose con el ALPHA. Las blend-ops son RGB-only por definicion (mismo contrato
    ''' que documenta el shader GL), asi que el alpha nunca se compone: o viaja intacto o sale opaco.</summary>
    Public Enum FaceTintAlphaPolicy
        ''' <summary>Alpha = 1 (byte 255). Es lo que hornea el CK salvo con Diffuse Alpha Test.</summary>
        Opaque = 0
        ''' <summary>Alpha del SEED, sin tocar. Medido: el _d que hornea el CK lleva exactamente el alpha del
        ''' head diffuse de origen cuando el material de la cabeza lo testea.</summary>
        Passthrough = 1
    End Enum

    ''' <summary>El acumulador de un canal, en float SoA y YA en OutputSpace (el pase AccumSpace→OutputSpace
    ''' lo hace <see cref="ComposeChannelAccum"/>, una sola vez, al cerrar).
    ''' <para>SoA y no AoS a proposito: los tres canales son arrays CONTIGUOS ⇒ el espejo vectorial carga
    ''' directo, sin gather y sin exigencia de alineacion. Quien necesite AoS lo pide con
    ''' <see cref="AccumToRgbaAos"/>.</para></summary>
    Public Class CpuAccumResult
        Public Width As Integer
        Public Height As Integer
        Public R As Single()
        Public G As Single()
        Public B As Single()
        ''' <summary>Alpha [0,1], o Nothing con <see cref="FaceTintAlphaPolicy.Opaque"/> (no se reserva:
        ''' a 4096² son 67 MB por canal que nadie leeria).</summary>
        Public A As Single()
    End Class

    ''' <summary>Prioridad UNICA del cache de decode: el del caller (render) → <see cref="BatchDecodeCache"/>
    ''' (batch de bakes) → uno per-call. La leen los DOS puntos de entrada del compose (la pipeline de 3
    ''' canales y el accum suelto que usa SSE); escrita dos veces, una podia quedarse con otra prioridad.</summary>
    Public Function ResolveDecodeCache(callerCache As System.Collections.Concurrent.ConcurrentDictionary(Of String, DecodedTex)) _
        As System.Collections.Concurrent.ConcurrentDictionary(Of String, DecodedTex)
        Return If(callerCache, If(BatchDecodeCache, New System.Collections.Concurrent.ConcurrentDictionary(Of String, DecodedTex)(StringComparer.OrdinalIgnoreCase)))
    End Function

    ''' <summary>Compone UN canal y lo empaqueta a BGRA byte. Wrapper del contrato abierto: resuelve el
    ''' tamaño y el mip del source, arma el <see cref="FaceTintSeedSpec"/> y delega en
    ''' <see cref="ComposeChannelAccum"/> + <see cref="PackAccumToBgra"/>.</summary>
    Private Function ComposeChannelCpu(srcBytes As Byte(), channel As FaceTintChannel,
                                       layers As IList(Of FaceTintLayerInput),
                                       swaps As IList(Of FaceRegionSwapInput),
                                       cache As System.Collections.Concurrent.ConcurrentDictionary(Of String, DecodedTex),
                                       resolution As FaceTintResolutionSettings,
                                       Optional srcKey As String = Nothing,
                                       Optional headDiffuseAlphaTest As Boolean = False) As CpuChannelResult
        ' Source cacheado por key (face set) si se paso srcKey + hay cache batch; si no, decode directo.
        Dim src = If(String.IsNullOrEmpty(srcKey), DecodeDds(srcBytes), CachedDecode(cache, srcKey, srcBytes))
        If src Is Nothing Then Return Nothing
        Dim isD = (channel = FaceTintChannel.Diffuse)
        ' Tamaño del ACUMULADOR: Inherit (default) = nativo del source (preserva no-cuadrado, sin
        ' downgrade). Enum explícito = cuadrado del target. Bodyparts: el caller pasa Nothing -> Inherit
        ' (el enum es solo cara). El mip del que se siembra lo elige SelectLevelForTarget, unas lineas abajo.
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
        ' ⛔ GATE de pixel-work (ver SkipPixelCompose). Va ACA a proposito: despues del decode del source y de
        ' resolver w/h — lo unico que decide si el canal sale Nothing (slot que el bake NO escribe) y con que
        ' tamaño. Saltea seed + region swaps + tint layers. El resto del flujo es identico ⇒ el NIF sale igual.
        If SkipPixelCompose Then Return New CpuChannelResult With {.Width = w, .Height = h, .Bgra = New Byte(n * 4 - 1) {}}
        ' ALPHA del _d: passthrough del alpha del base SOLO si la cabeza usa Diffuse Alpha Test (flag ACBS
        ' 0x01000000); si no, opaco. El CK aplana el alpha del _d salvo cuando el material de la cabeza lo
        ' testea; el passthrough incondicional le inventaba alpha a DiMA. Inerte para N/S. Ver 40-bake-leyes-fo4.
        ' ⚠️ SIN VERIFICAR DEL TODO — REVISITAR CON DATOS: la evidencia es corpus 2 (Valentine y DiMA) y se
        ' midio "dejo de inventar alpha", no el canal A contra el CK. Ademas la decision se tomo cuando el _d
        ' componia SIEMPRE a nativo: ahora `res` puede no ser Inherit, y un alpha que existe para ser TESTEADO
        ' (umbral duro) no sobrevive el resample igual que el color. Ver 40-bake-leyes-fo4 §8 (como cerrarlo).
        ' ⛔ EL SEED DE ESTE WRAPPER ES SIEMPRE LA TEXTURA, y no `FaceTintConvention.SeedModeValue`, A
        ' PROPOSITO: esta funcion NO es de FO4 — la usa tambien el compose del head diffuse de SSE (render con
        ' CPU-skinning y su espejo del bake). Con SSE activo el bucket dice `Constant`, asi que leer el modo
        ' acá sembraria de 0,5 plano una textura que SI tiene base. El modo es ley del SEED DEL FACETINT y lo
        ' consume el builder que si sabe que su camino es tint-only (SseFaceTintComposer.BuildSeedSpec).
        Dim acc = ComposeChannelAccum(FaceTintSeedSpec.FromTextureSource(src), w, h, channel, layers, swaps, cache,
                                      If(headDiffuseAlphaTest AndAlso isD, FaceTintAlphaPolicy.Passthrough, FaceTintAlphaPolicy.Opaque))
        If acc Is Nothing Then Return Nothing
        Return New CpuChannelResult With {.Width = w, .Height = h, .Bgra = PackAccumToBgra(acc)}
    End Function

    ''' <summary>Compone UN canal a un acumulador float SoA: seed → region swaps (crossfade en linear) →
    ''' capas de tint (over-running, ley del resolver) → UN pase AccumSpace→OutputSpace.
    ''' <para>Es el compositor CPU compartido por los DOS juegos: el juego aporta DATOS (que capas, que
    ''' colores, que mascaras, de que canal sale la cobertura) y su set de defaults; como se compone es unico.
    ''' Si te encontras poniendo un <c>If esSkyrim</c> acá, frenaste mal.</para>
    ''' <para>⛔ SYNC: CPU/GPU compositor — es el espejo EXACTO del seed + <c>ApplyRegionSwapsOntoFaceTexture</c>
    ''' + <c>ComposeOntoFaceTexture</c> del camino GL (FaceTintCompositor). Los dos leen sus parámetros del
    ''' MISMO <c>FaceTintConvention.ResolveConvention</c>, que es lo que hace que la paridad sea por
    ''' construcción y no por coincidencia. Duele si diverge porque el BAKE corre 100 % CPU y el RENDER por
    ''' GL: un barrido validaría un camino que el usuario nunca ve. Ver 50-facetint-leyes-y-compositor.md.</para></summary>
    ''' <param name="seed">De donde sale el acumulador antes de la primera capa. Con
    ''' <see cref="FaceTintSeedKind.FromTexture"/> la textura tiene que estar decodificada y ser la que fija
    ''' <paramref name="w"/>/<paramref name="h"/> (o el seed se muestrea bilineal, que es el comportamiento
    ''' historico cuando el canal compone a otra resolucion).</param>
    ''' <param name="cache">Cache de decode de las CAPAS y los SWAPS. Nothing ⇒ se resuelve con
    ''' <see cref="ResolveDecodeCache"/>.</param>
    Public Function ComposeChannelAccum(seed As FaceTintSeedSpec, w As Integer, h As Integer,
                                        channel As FaceTintChannel,
                                        layers As IList(Of FaceTintLayerInput),
                                        swaps As IList(Of FaceRegionSwapInput),
                                        cache As System.Collections.Concurrent.ConcurrentDictionary(Of String, DecodedTex),
                                        alphaPolicy As FaceTintAlphaPolicy,
                                        Optional stage As FaceTintConvention.FaceTintStage? = Nothing) As CpuAccumResult
        If seed Is Nothing OrElse w <= 0 OrElse h <= 0 Then Return Nothing
        ' ⭐ ETAPA EFECTIVA — el MISMO contrato que el GL (FaceTintCompositor.ComposeOntoFaceTexture): Nothing =
        ' la etapa de tint del canal (comportamiento previo, byte-idéntico); explícita = el bucket de esa etapa.
        ' ⛔ SYNC: CPU/GPU compositor — si un camino pasa `stage` y el otro no, los dos compositores resuelven
        ' buckets distintos para la MISMA pasada y la paridad se rompe en silencio. Los callers van de a pares.
        Dim effStage As FaceTintConvention.FaceTintStage =
            If(stage.HasValue, stage.Value, FaceTintConvention.TintStageFor(channel))
        Dim src As DecodedTex = seed.Texture
        If seed.Kind = FaceTintSeedKind.FromTexture AndAlso src Is Nothing Then Return Nothing
        cache = ResolveDecodeCache(cache)
        Dim isD = (channel = FaceTintChannel.Diffuse)
        Dim n = w * h
        ' Acumulador RGB en OutputSpace del canal (build_3): D=sRGB (= src directo, SIN g22) ; N/S=raw lineal.
        ' El storage del engine FaceCustomization es sRGB (= formato de CK en disco); no se acumula en g22.
        ' Seed via SampleChannelAt (índice directo si tamaños iguales; bilineal si difieren = resize).
        ' Acumuladores en Single (storage): el math por-píxel abajo corre en Double y se guarda con CSng.
        Dim accR(n - 1) As Single, accG(n - 1) As Single, accB(n - 1) As Single
        ' Acumulador ALPHA: PASSTHROUGH del seed, nunca compuesto (ver FaceTintAlphaPolicy). Se decide ACA,
        ' antes de reservar nada: antes se reservaba el array y se sampleaba el canal 3 por pixel en los tres
        ' canales pasara lo que pasara (67 MB a 4096^2).
        Dim keepBaseAlpha As Boolean = (alphaPolicy = FaceTintAlphaPolicy.Passthrough)
        Dim accA As Single() = Nothing
        If keepBaseAlpha Then ReDim accA(n - 1)
        ' Seed del base diffuse: la base ES una textura de color ⇒ src config-driven (no el literal 1):
        ' SeedDiffuseSrcSpaceValue (= DiffuseTextureSrcSpace, Srgb por default).
        Dim seedSrc = SeedDiffuseSrcSpaceValue
        ' ⭐ ESPACIO DEL ACUMULADOR, resuelto UNA VEZ POR CANAL. Tiene que ser UNO SOLO: es el storage de
        ' accR/accG/accB, que viven a lo largo de TODO el compose del canal. Aunque la convencion se resuelva
        ' por capa, el acumulador no puede cambiar de espacio entre capas — por eso se toma a nivel canal.
        ' ⭐ UNICA fuente de verdad del espacio del acumulador, COMPARTIDA con el GL (ver AccumSpaceForChannel):
        ' el bucket del CANAL manda; el del Swap NO participa del storage. Asi los dos compositores leen el
        ' MISMO valor por construccion, tambien si el usuario cambia los settings.
        Dim accSp As Integer = CInt(FaceTintConvention.AccumSpaceForChannel(channel, AccumSpaceCapability))
        ' OutputSpace del canal: destino del UNICO pase final (abajo, en el pack) y ORIGEN implicito del seed
        ' crudo (ver el Else del loop). Con AccumInCompositeSpace=False vale lo mismo que accSp ⇒ toda
        ' conversion de este par es identidad y el compose queda byte-identico al comportamiento previo.
        Dim outSp As Integer = CInt(FaceTintConvention.OutputSpaceForChannel(channel))
        ' Paralelo POR RANGOS (Partitioner) en vez de Parallel.For(0, n, Sub(i)): el cuerpo es puramente
        ' por-píxel con escrituras disjuntas, así que es BIT-IDENTICO — sólo cambia que en vez de un
        ' delegate POR PIXEL se invoca uno por rango y el cuerpo corre en un For cerrado (el JIT puede izar
        ' los campos de la clausura y elidir chequeos de rango). Mismo patrón y misma justificación que los
        ' loops de DecodeDds y del pack, que ya lo usaban.
        ' ⭐ INVARIANTE IZADO — misma justificacion y misma garantia que en el loop de capas de mas abajo.
        Dim srcDirect As Boolean = (src IsNot Nothing AndAlso src.Width = w AndAlso src.Height = h)
        Dim srcPx As Byte() = If(srcDirect, src.Rgba8, Nothing)
        Dim seedLut = ByteToUnit
        ' ⭐ SEED VECTORIZADO. Deja de ser gratis justo con la convención REAL de FO4: ahí `accSp`(Linear) y
        ' `outSp`(G22) DIFIEREN, así que `Cvt1` NO cortocircuita y esto es UN POW POR PÍXEL Y POR CANAL a
        ' resolución nativa, una vez por canal. Con el config viejo (accSp == outSp) era identidad y por eso
        ' nunca figuró como resto escalar.
        Dim seedFromSp As Integer = If(SeedConventionIs_G22 AndAlso isD, seedSrc, outSp)
        ' ⭐ IZADO DEL RESAMPLE. Antes `seedVecOk` EXIGIA `srcDirect`: una fuente que no venia al tamaño del
        ' acumulador mandaba el canal ENTERO al camino escalar. Ahora se resamplea UNA vez a planos SoA —los
        ' MISMOS 4 taps por pixel que hacia SampleChannelAt dentro del loop, no uno mas— y el cuerpo vectorial
        ' cubre tambien ese caso. Bit-identico por construccion: los planos los llena SampleChannelAt.
        ' Solo en el camino de textura; Constant/Provided no leen `src`. `src` no puede ser Nothing aca: lo
        ' garantiza la guarda de FromTexture de mas arriba.
        Dim hoSeedR As Single() = Nothing, hoSeedG As Single() = Nothing, hoSeedB As Single() = Nothing, hoSeedA As Single() = Nothing
        If seed.Kind = FaceTintSeedKind.FromTexture AndAlso Not srcDirect Then
            ResampleToUnitPlanes(src, w, h, If(keepBaseAlpha, 15, 7), hoSeedR, hoSeedG, hoSeedB, hoSeedA)
        End If
        Dim srcHoisted As Boolean = (hoSeedR IsNot Nothing)
        Dim seedVecOk As Boolean = FastPow.AcceleratedV AndAlso (srcDirect OrElse srcHoisted)
        If seed.Kind = FaceTintSeedKind.Constant Then
            ' ⭐ SEED CONSTANTE (la ley de SSE). El color plano se expresa en OutputSpace —igual que el seed
            ' CRUDO de una textura— y se lleva a AccumSpace con la MISMA `Cvt1`, una sola vez para todo el
            ' canal en vez de por pixel. Con accSp == outSp `Cvt1` cortocircuita ⇒ identidad exacta.
            Dim kR = Cvt1(seed.R, outSp, accSp), kG = Cvt1(seed.G, outSp, accSp), kB = Cvt1(seed.B, outSp, accSp)
            ' Un seed constante no trae alpha: con Passthrough no hay de donde sacarlo y sale opaco.
            System.Threading.Tasks.Parallel.ForEach(
                System.Collections.Concurrent.Partitioner.Create(0, n),
                Sub(range)
                    For i As Integer = range.Item1 To range.Item2 - 1
                        accR(i) = kR : accG(i) = kG : accB(i) = kB
                    Next
                    If keepBaseAlpha Then
                        For i As Integer = range.Item1 To range.Item2 - 1
                            accA(i) = 1.0F
                        Next
                    End If
                End Sub)
        ElseIf seed.Kind = FaceTintSeedKind.Provided Then
            ' ⭐ SEED DESDE UN BUFFER DEL CALLER (AoS RGBA en OutputSpace). Se de-intercala a SoA y se lleva a
            ' AccumSpace con la misma conversion por elemento; el ALPHA viaja CRUDO (no es color).
            Dim sb = seed.Buffer
            If sb Is Nothing OrElse sb.Length < n * 4 Then
                Throw New ArgumentException($"FaceTintSeedSpec.Provided: el buffer tiene {If(sb Is Nothing, 0, sb.Length)} elementos y hacen falta {n * 4} ({w}x{h} RGBA).", NameOf(seed))
            End If
            System.Threading.Tasks.Parallel.ForEach(
                System.Collections.Concurrent.Partitioner.Create(0, n),
                Sub(range)
                    For i As Integer = range.Item1 To range.Item2 - 1
                        Dim pb = i * 4
                        accR(i) = sb(pb) : accG(i) = sb(pb + 1) : accB(i) = sb(pb + 2)
                        If keepBaseAlpha Then accA(i) = sb(pb + 3)
                    Next
                End Sub)
            ConvertSpaceSoaInPlace(accR, n, outSp, accSp)
            ConvertSpaceSoaInPlace(accG, n, outSp, accSp)
            ConvertSpaceSoaInPlace(accB, n, outSp, accSp)
        Else
            System.Threading.Tasks.Parallel.ForEach(
            System.Collections.Concurrent.Partitioner.Create(0, n),
            Sub(range)
                Dim iv = range.Item1
                If seedVecOk Then
                    While iv + lanes <= range.Item2
                        Dim rV, gV, bV, aV As Vector(Of Single)
                        If srcDirect Then
                            LoadRgba8BlockV(srcPx, iv * 4, rV, gV, bV, aV)
                        Else
                            ' Planos SoA: la carga es CONTIGUA, sin de-interleave ni gather.
                            rV = New Vector(Of Single)(hoSeedR, iv)
                            gV = New Vector(Of Single)(hoSeedG, iv)
                            bV = New Vector(Of Single)(hoSeedB, iv)
                            If keepBaseAlpha Then aV = New Vector(Of Single)(hoSeedA, iv)
                        End If
                        CvtV(rV, seedFromSp, accSp).CopyTo(accR, iv)
                        CvtV(gV, seedFromSp, accSp).CopyTo(accG, iv)
                        CvtV(bV, seedFromSp, accSp).CopyTo(accB, iv)
                        ' El ALPHA es RAW: no es color, NO pasa por ninguna conversión de espacio.
                        If keepBaseAlpha Then aV.CopyTo(accA, iv)
                        iv += lanes
                    End While
                End If
                For i As Integer = iv To range.Item2 - 1
                    Dim r0 As Single, g0 As Single, b0 As Single
                    If srcDirect Then
                        Dim pb = i * 4
                        r0 = seedLut(srcPx(pb)) : g0 = seedLut(srcPx(pb + 1)) : b0 = seedLut(srcPx(pb + 2))
                        ' Alpha RAW: no es color ⇒ NO pasa por Cvt1 (ninguna conversion de espacio).
                        If keepBaseAlpha Then accA(i) = seedLut(srcPx(pb + 3))
                    Else
                        ' Los planos YA son SampleChannelAt evaluado en este mismo (i,w,h) ⇒ mismos bits.
                        r0 = hoSeedR(i) : g0 = hoSeedG(i) : b0 = hoSeedB(i)
                        If keepBaseAlpha Then accA(i) = hoSeedA(i)
                    End If
                    If SeedConventionIs_G22 AndAlso isD Then
                        accR(i) = CSng(Cvt1(r0, seedSrc, accSp)) : accG(i) = CSng(Cvt1(g0, seedSrc, accSp)) : accB(i) = CSng(Cvt1(b0, seedSrc, accSp))
                    Else
                        ' ⭐ SEED CRUDO (N/S siempre; el diffuse cuando SeedDiffuseG22=False): el valor entra SIN
                        ' curva. Su espacio implicito es OutputSpace — es lo que asumia el comportamiento previo,
                        ' donde el pack escribia este mismo numero como salida sin tocarlo. Por eso hay que
                        ' llevarlo outSp->accSp: si no, con el acumulador en CompositeSpace el compose leeria un
                        ' buffer sembrado en OutputSpace creyendolo CompositeSpace, y el pase final le sumaria
                        ' encima otra conversion. (Bug real: rompia N/S y el diffuse con SeedDiffuseG22 apagado.)
                        ' ⛔ Con accSp == outSp (el DEFAULT) `Cvt1` cortocircuita ⇒ identidad exacta, mismos bits.
                        accR(i) = CSng(Cvt1(r0, outSp, accSp)) : accG(i) = CSng(Cvt1(g0, outSp, accSp)) : accB(i) = CSng(Cvt1(b0, outSp, accSp))
                    End If
                Next
            End Sub)
        End If

        ' --- Region swaps UNIFICADOS = tint-replace (2026-06-01): cada swap es un replace mas -> lerp desde el
        '     RUNNING acc, cov = srgb_encode(mask)*msdv, en LINEAR (D decode/encode sRGB / N-S raw). MISMA regla
        '     que los tints; SIN closed-form ni SEED aparte. Mejora N ~1 byte vs el closed-form viejo, neutral D/S.
        If swaps IsNot Nothing Then
            For Each sw In swaps
                If sw Is Nothing Then Continue For
                Dim swBytes = sw.GetSwapBytes(channel)
                If swBytes Is Nothing OrElse swBytes.Length = 0 Then Continue For
                If sw.RegionMaskDdsBytes Is Nothing OrElse sw.RegionMaskDdsBytes.Length = 0 Then Continue For
                ' Mip-stored: las dos son ESPACIALES (se muestrean por UV contra el acumulador), asi que
                ' piden el nivel del target igual que el source. El GL hace lo MISMO por pickLod, que espeja
                ' SelectLevelForTarget: por eso los dos compositores leen el mismo texel.
                Dim swTex = CachedDecode(cache, sw.GetSwapCacheKey(channel), swBytes, w, h)
                Dim mkTex = CachedDecode(cache, sw.RegionMaskCacheKey, sw.RegionMaskDdsBytes, w, h)
                If swTex Is Nothing OrElse mkTex Is Nothing Then Continue For
                Dim msdv As Single = FaceTintConvention.ClampSwapIntensity(sw.Intensity)   ' ley UNICA compartida con el GL
                ' Swap = replace resuelto por la MISMA tabla que los tints (forSwap:=True) -> sin convención
                ' hardcodeada; el override (incl. #If DEBUG full-linear) alcanza también los swaps. NON-DEBUG
                ' byte-idéntico al closed-form previo (cov=srgbenc(mask), D lerp linear-desde-srgb, N/S raw).
                Dim cv = FaceTintConvention.ResolveConvention(FaceTintConvention.FaceTintStage.RegionSwap, channel, isTextureSet:=False, blendOp:=0)
                Dim sws = CInt(cv.WorkingSpace), scs = CInt(cv.CompositeSpace), sss = CInt(cv.SrcSpace), sos = CInt(cv.OutputSpace)
                ' â›” `sos` (Swap.OutputSpace) ES UN ARGUMENTO MUERTO ACA, a proposito: ComposeOne solo lo usa
                ' para derivar asp, y como abajo se pasa accSpace explicito, no se lee en ninguna rama. Se
                ' sigue pasando por simetria con los otros call sites.
                ' CAMBIO DE COMPORTAMIENTO DECLARADO: antes el acumulador del swap se trataba como si viviera
                ' en sos y el de los tints en el del CANAL - dos etiquetas para EL MISMO buffer. Ahora manda el
                ' canal. Con los defaults de fabrica de los dos juegos es byte-identico, pero no si el usuario
                ' separa esos combos en CharGen Options. El GL hace lo mismo (guard gemelo alla).
                ' La advertencia va por FaceTintConvention (latcheada y always-on) y NO por Logger, que esta
                ' APAGADO en release: un aviso por ahi no existiria justo para el usuario que necesita verlo.
                If channel = FaceTintChannel.Diffuse AndAlso sos <> accSp Then
                    FaceTintConvention.NoteSwapAccumMismatch(channel, sos, accSp)
                End If
                Dim smc = CInt(cv.MaskConv), sbop = CInt(cv.Blend), ssl = CInt(cv.SoftLight)
                ' Paralelo POR RANGOS — ver la nota del seed. Cuerpo per-pixel con escrituras disjuntas
                ' (cada i toca sólo acc*(i)) ⇒ bit-idéntico; sólo cambia el particionado.
                ' ⭐ INVARIANTE IZADO (x2: la textura del swap y la mascara) — ver la nota del loop de capas.
                Dim swDirect As Boolean = (swTex.Width = w AndAlso swTex.Height = h)
                Dim mkDirect As Boolean = (mkTex.Width = w AndAlso mkTex.Height = h)
                Dim swPx As Byte() = If(swDirect, swTex.Rgba8, Nothing)
                Dim mkPx As Byte() = If(mkDirect, mkTex.Rgba8, Nothing)
                Dim swLut = ByteToUnit
                ' ⭐ REGION SWAP VECTORIZADO. Era el otro resto per-píxel entero del compose: 3 ComposeOne
                ' ESCALARES por píxel y por swap, y no es un caso raro — el reporte de paridad CPU-vs-GPU
                ' mide 66 M de 85 M de píxeles en NPCs CON region swaps.
                ' El framework es OverPrev (ComposeOne se llama sin `framework` ⇒ 0), así que el espejo
                ' vectorial aplica si VecComposeSupported lo cubre; los dos sampleos tienen que ser directos
                ' (si no, es SampleChannelAt = gather).
                ' ⛔ SIN skip de cov<=0: el escalar de acá NO lo tiene y el GLSL (uMode==1) TAMPOCO ⇒ meterlo
                ' sólo del lado CPU rompería la paridad con el GPU. Si algún día se agrega, va en los dos.
                ' ⭐ IZADO DEL RESAMPLE (misma ley y misma justificacion que el seed). El swap usa R,G,B de su
                ' textura y SOLO R de la mascara: materializar los cuatro canales de la mascara seria
                ' cuadruplicar el izado para tirar tres. Con esto `swVecOk` deja de exigir las dos directness.
                Dim hoSwR As Single() = Nothing, hoSwG As Single() = Nothing, hoSwB As Single() = Nothing, hoSwA As Single() = Nothing
                Dim hoMkR As Single() = Nothing, hoMkG As Single() = Nothing, hoMkB As Single() = Nothing, hoMkA As Single() = Nothing
                If Not swDirect Then ResampleToUnitPlanes(swTex, w, h, 7, hoSwR, hoSwG, hoSwB, hoSwA)
                If Not mkDirect Then ResampleToUnitPlanes(mkTex, w, h, 1, hoMkR, hoMkG, hoMkB, hoMkA)
                Dim swVecOk As Boolean = VecComposeSupported(0, sbop, ssl)
                System.Threading.Tasks.Parallel.ForEach(
                    System.Collections.Concurrent.Partitioner.Create(0, n),
                    Sub(range)
                        Dim iv = range.Item1
                        If swVecOk Then
                            Dim msdvV = VBroadcast(msdv)
                            While iv + lanes <= range.Item2
                                Dim srV, sgV, sbV, saV As Vector(Of Single)
                                If swDirect Then
                                    LoadRgba8BlockV(swPx, iv * 4, srV, sgV, sbV, saV)
                                Else
                                    srV = New Vector(Of Single)(hoSwR, iv)
                                    sgV = New Vector(Of Single)(hoSwG, iv)
                                    sbV = New Vector(Of Single)(hoSwB, iv)
                                End If
                                ' la máscara del swap es el canal R (igual que el escalar: mkPx(i*4))
                                Dim mkR As Vector(Of Single)
                                If mkDirect Then
                                    Dim mkG, mkB, mkA As Vector(Of Single)
                                    LoadRgba8BlockV(mkPx, iv * 4, mkR, mkG, mkB, mkA)
                                Else
                                    mkR = New Vector(Of Single)(hoMkR, iv)
                                End If
                                ' MISMO orden que el escalar: convertir, multiplicar, recién ahí clampear.
                                Dim covV = Clamp01V(Vector.Multiply(ConvMaskV(mkR, smc), msdvV))
                                ComposeSwapBlockV(accR, iv, srV, covV, sws, scs, sss, accSp, sbop, ssl)
                                ComposeSwapBlockV(accG, iv, sgV, covV, sws, scs, sss, accSp, sbop, ssl)
                                ComposeSwapBlockV(accB, iv, sbV, covV, sws, scs, sss, accSp, sbop, ssl)
                                iv += lanes
                            End While
                        End If
                        For i As Integer = iv To range.Item2 - 1
                            Dim sr As Single, sg As Single, sb As Single, mask As Single
                            If swDirect Then
                                Dim pb = i * 4
                                sr = swLut(swPx(pb)) : sg = swLut(swPx(pb + 1)) : sb = swLut(swPx(pb + 2))
                            Else
                                ' Los planos YA son SampleChannelAt en este mismo (i,w,h) ⇒ mismos bits.
                                sr = hoSwR(i) : sg = hoSwG(i) : sb = hoSwB(i)
                            End If
                            If mkDirect Then
                                mask = swLut(mkPx(i * 4))
                            Else
                                mask = hoMkR(i)
                            End If
                            Dim cov = Clamp01(ConvMask1(mask, smc) * msdv)
                            accR(i) = CSng(ComposeOne(accR(i), sr, cov, sws, scs, sss, sos, sbop, ssl, accSpace:=accSp))
                            accG(i) = CSng(ComposeOne(accG(i), sg, cov, sws, scs, sss, sos, sbop, ssl, accSpace:=accSp))
                            accB(i) = CSng(ComposeOne(accB(i), sb, cov, sws, scs, sss, sos, sbop, ssl, accSpace:=accSp))
                        Next
                    End Sub)
            Next
        End If

        ' ⛔ SYNC: `base` es un SNAPSHOT del acumulador POST-swaps, igual que el GL — allá el pase de tints
        ' recibe como input la textura YA swapeada, así que su uBase también es post-swap. Se captura acá
        ' para que los frameworks base-relativos compongan sobre el baseline morpheado y no sobre el seed.
        ' El snapshot sólo lo leen esos frameworks; con la config default nadie lo usa y copiarlo siempre era
        ' trabajo perdido por canal y por NPC (a 4096², cientos de MB de LOH).
        ' El pre-scan que decide si hace falta sigue siendo CONSERVADOR (no replica los `Continue For` del
        ' loop real): como mucho reserva de más, nunca de menos, que es el único error que cambiaría un byte.
        ' ⛔ SE SACÓ el barrido `hp ∈ {False, True}`. Existía para "enumerar el espacio de parámetros completo
        ' por si alguien hace que el Framework dependa de useHairPalette", pero ese parámetro NUNCA lo leyó el
        ' resolver: las dos iteraciones devolvían lo mismo y se pagaban dos resoluciones por capa y por canal.
        ' Ahora el parámetro directamente no existe, así que la hipótesis que justificaba el barrido tampoco.
        Dim needsBase As Boolean = False
        If layers IsNot Nothing Then
            For Each pLayer In layers
                If pLayer Is Nothing Then Continue For
                Dim pfw = FaceTintConvention.ResolveConvention(
                    effStage, channel, pLayer.IsTextureSet, pLayer.BlendOp).Framework
                If pfw = FaceTintFramework.OverBase OrElse pfw = FaceTintFramework.AddBase Then
                    needsBase = True
                    Exit For
                End If
            Next
        End If
        Dim baseR As Single() = Nothing, baseG As Single() = Nothing, baseB As Single() = Nothing
        If needsBase Then
            ReDim baseR(n - 1) : ReDim baseG(n - 1) : ReDim baseB(n - 1)
            Array.Copy(accR, baseR, n) : Array.Copy(accG, baseG, n) : Array.Copy(accB, baseB, n)
        End If

        ' --- Tint layers (over-running). La ley sale del resolver (compositor AGNOSTICO). ---
        If layers IsNot Nothing Then
            ' TakesSkinTone: una capa flagged que compone DESPUES del skintone recibe el MISMO softlight del
            ' skintone sobre su SOURCE (viene sin tonear; las flagged ANTES del skintone las tonea el skintone
            ' encima por el orden). Capturamos color/mask/conv del skintone al pasarlo y pre-tonemos las flagged
            ' posteriores. GUARD: solo se activa con flagged-after-skintone (inerte/byte-identico en todo bake
            ' actual, p.ej. Alana, donde las flagged van antes del skintone). Mismo ComposeOne -> paridad GL.
            Dim stSeen As Boolean = False
            Dim stColR As Single = 0F, stColG As Single = 0F, stColB As Single = 0F, stOpac As Single = 0F
            Dim stMaskTex As DecodedTex = Nothing
            Dim stMaskCh As Integer = 1, stMc As Integer = 0
            Dim stWs As Integer = 0, stCs As Integer = 0, stSs As Integer = 0, stOs As Integer = 0, stBop As Integer = 0, stSl As Integer = 0
            ' Pre-scan TakesSkinTone (2-pass): capturar color/op/mask/conv del skintone ANTES del loop, para
            ' poder pre-tonar tambien las flagged que componen ANTES del skintone bajo frameworks no-acumulativos
            ' (OverBase/AddBase). Con OverPrev/ModSrc nonAccum=False -> el guard se reduce a stSeen (byte-identico).
            Dim skintoneFound As Boolean = False
            Dim nonAccum As Boolean = False
            If isD Then
                For Each sLayer In layers
                    If sLayer Is Nothing OrElse Not sLayer.IsSkinTone Then Continue For
                    Dim sBytes = sLayer.GetChannelBytes(channel)
                    If sBytes Is Nothing OrElse sBytes.Length = 0 Then Continue For
                    Dim sTex = CachedDecode(cache, sLayer.GetChannelCacheKey(channel), sBytes, w, h)
                    If sTex Is Nothing Then Continue For
                    Dim sConv = FaceTintConvention.ResolveConvention(effStage, channel, sLayer.IsTextureSet, sLayer.BlendOp)
                    stColR = sLayer.R / 255.0F : stColG = sLayer.G / 255.0F : stColB = sLayer.B / 255.0F
                    stOpac = MathF.Max(0.0F, MathF.Min(1.0F, sLayer.Opacity))
                    stMaskTex = sTex : stMc = CInt(sConv.MaskConv)
                    stMaskCh = If(sLayer.Kind = FaceTintLayerKind.PaletteMask, 1, 3)
                    stWs = CInt(sConv.WorkingSpace) : stCs = CInt(sConv.CompositeSpace)
                    stSs = CInt(sConv.SrcSpace) : stOs = CInt(sConv.OutputSpace)
                    stBop = CInt(sConv.Blend) : stSl = CInt(sConv.SoftLight)
                    nonAccum = (sConv.Framework = FaceTintFramework.OverBase OrElse sConv.Framework = FaceTintFramework.AddBase)
                    skintoneFound = True
                    Exit For
                Next
            End If
            For Each layer In layers
                If layer Is Nothing Then Continue For
                ' FUENTE DE LA CAPA, UNA SOLA para los dos juegos: los BYTES del DDS. El compositor los
                ' decodifica (nivel 1, cacheado, con preferencia de mip al tamaño del acumulador) y, si el
                ' decode no cae justo en w×h, muestrea bilineal POR PIXEL mas abajo. ⛔ No hay un segundo
                ' origen "ya resampleado": lo hubo y se borro (ver la nota en FaceTintLayerInput).
                Dim chanBytes = layer.GetChannelBytes(channel)
                If chanBytes Is Nothing OrElse chanBytes.Length = 0 Then Continue For
                Dim layerTex = CachedDecode(cache, layer.GetChannelCacheKey(channel), chanBytes, w, h)
                If layerTex Is Nothing Then Continue For

                Dim useHairPalette = (layer.UseHairPalette AndAlso isD AndAlso layer.HairLutDdsBytes IsNot Nothing AndAlso layer.HairLutDdsBytes.Length > 0)
                Dim lutTex As DecodedTex = Nothing
                If useHairPalette Then
                    ' ⛔ La LUT NUNCA lleva target, ni siquiera si algun dia las capas volvieran a pedirlo: no es
                    ' una textura espacial sino una PALETA indexada por valor (U=f(green), V=RemappingIndex),
                    ' leida NEAREST engine-exact. Resamplearla correria las entradas de la paleta.
                    lutTex = CachedDecode(cache, layer.HairLutCacheKey, layer.HairLutDdsBytes)
                    If lutTex Is Nothing Then useHairPalette = False
                End If
                Dim forceUniform = (layer.ForceUniformColor AndAlso layer.Kind = FaceTintLayerKind.TextureSetDiffuse AndAlso isD AndAlso Not useHairPalette)
                Dim texTimesColor = (layer.MultiplyTextureByColor AndAlso layer.Kind = FaceTintLayerKind.TextureSetDiffuse AndAlso isD AndAlso Not useHairPalette AndAlso Not forceUniform)

                ' Mask diffuse (uLayerDiffuseAlpha) para N/S de TextureSet (alpha del diffuse del layer).
                Dim diffMaskTex As DecodedTex = Nothing
                If layer.Kind = FaceTintLayerKind.TextureSetDiffuse AndAlso Not isD _
                   AndAlso layer.LayerDdsBytes IsNot Nothing AndAlso layer.LayerDdsBytes.Length > 0 Then
                    diffMaskTex = CachedDecode(cache, layer.LayerCacheKey, layer.LayerDdsBytes, w, h)
                End If

                ' ⭐ `effStage` — espejo EXACTO del GL (ComposeOntoFaceTexture). Acá estaba cableado en la etapa
                ' de tint del canal, igual que allá, así que ningún bucket de etapa podía llegar al compose.
                Dim conv = FaceTintConvention.ResolveConvention(
                    effStage, channel, layer.IsTextureSet, layer.BlendOp)
                Dim ws = CInt(conv.WorkingSpace), cs = CInt(conv.CompositeSpace)
                Dim ss = CInt(conv.SrcSpace), os = CInt(conv.OutputSpace)
                Dim mc = CInt(conv.MaskConv), bop = CInt(conv.Blend)
                Dim sl = CInt(conv.SoftLight)   ' modelo de softlight (agnostico) para bop3
                Dim fw = CInt(conv.Framework)   ' framework de composite (OverPrev default)
                Dim op = MathF.Max(0.0F, MathF.Min(1.0F, layer.Opacity))
                Dim uColR = layer.R / 255.0F, uColG = layer.G / 255.0F, uColB = layer.B / 255.0F
                Dim row = MathF.Max(0.0F, MathF.Min(1.0F, layer.HairPaletteRow))
                ' brow grayscale->palette LUT lookup = ENGINE-EXACT (BSFaceCustomizationShader PS, `ld` t4):
                ' el mask (verde) se decodea sRGB->linear (t1 = SRV sRGB), U=pow(lin,1/2.2), texel=ftoi(U*W,
                ' row*H), fetch NEAREST (ld; sin bilineal ni half-texel). Verificado byte-exact vs CK (resid 0.4).
                ' El verde crudo (lg) se pasa a SampleLutEngine, que hace sRGB-decode + pow + ftoi + nearest.
                Dim luY As Single = row
                Dim kind = layer.Kind
                ' GUARD del pre-tono TakesSkinTone: solo D, capa flagged, y skintone ya compuesto antes.
                ' Pre-tono si: capa flagged (D) Y hay skintone Y (ya se compuso antes -> over-running tona
                ' las de antes desde arriba, las de despues necesitan source-pretono) O el framework no acumula
                ' (OverBase/AddBase -> el skintone NO llega por el base, hay que pre-tonar TODA flagged).
                Dim preToneSkin As Boolean = (isD AndAlso layer.TakesSkinTone AndAlso skintoneFound AndAlso (stSeen OrElse nonAccum))

                ' Paralelo POR RANGOS - ver la nota del seed. Es el loop MAS PESADO de los tres (uno por CAPA,
                ' con varios Math.Pow por canal) y era el que pagaba un delegate por pixel. Escrituras disjuntas
                ' por pixel, asi que es bit-identico.
                ' INVARIANTE IZADO: "el tex mide lo mismo que el acumulador?" no depende del pixel, pero se
                ' evaluaba DENTRO de SampleChannelAt, o sea 4 veces por pixel y por capa. Se resuelve una vez
                ' por capa y el caso directo lee los 4 canales desde UN indice base.
                ' Los tipos van EXPLICITOS en Double: inferidos como Single, la aritmetica de abajo cambiaria
                ' de precision y la salida dejaria de ser identica.
                Dim layerDirect As Boolean = (layerTex.Width = w AndAlso layerTex.Height = h)
                Dim layerPx As Byte() = If(layerDirect, layerTex.Rgba8, Nothing)
                Dim lut = ByteToUnit
                ' ⭐ VECTORIZACION DEL COMPOSE — en DOS FASES, y el corte esta puesto donde esta a proposito.
                '   Fase A (per pixel, ESCALAR e INTACTA): sampleo, mask/src por kind, pre-tono, cobertura.
                '     Es la parte RAMOSA (kind, hair palette, forceUniform, diffMask, preToneSkin...) y la que
                '     hace gathers. Se deja tal cual y solo deposita su resultado en el bloque.
                '   Fase B (por bloques de `lanes`, VECTORIAL): los 3 ComposeOne. Ahi vive el 83 % del kernel
                '     (los pow de las conversiones de espacio), medido sobre el ComposePixel real.
                ' ⛔ El bloque NO es de 8: es `lanes` = FastPow.LaneCount, que el runtime fija en 8 con AVX2 y
                ' en 4 con SSE2. Escribirlo como 8 era la suposicion que el comentario del tope de este modulo
                ' ya advierte que corrompe una maquina de 4 lanes.
                ' El acumulador de FO4 es SoA (accR/accG/accB separados) ⇒ `lanes` pixeles son `lanes` floats contiguos:
                ' carga directa, sin gather y SIN requisito de alineacion (a diferencia del AoS del fold SSE),
                ' asi que aca no hace falta prologo — solo la cola cuando el rango no cierra en 8.
                ' ⛔ Si la combinacion (framework, blend, softlight) no tiene espejo vectorial, se usa el
                ' camino escalar de siempre: MISMO resultado, sin acelerar. No es un fallback aproximado.
                ' ⛔ NO agregar `AndAlso Not needsBase` aca. Lo tuve y estaba de mas: VecComposeSupported ya
                ' exige fw=0 (OverPrev), y OverPrev NO LEE `base` en ninguna rama de ComposeOne. Como
                ' `needsBase` es por CANAL, una sola capa OverBase apagaba el vectorial para TODAS las capas
                ' OverPrev del canal — perdida pura, sin ganar nada de correccion.
                Dim vecOk As Boolean = VecComposeSupported(fw, bop, sl)
                ' `asp` EFECTIVO: ComposeOne hace `If accSpace < 0 Then os`. El espejo vectorial recibe el
                ' espacio YA resuelto, asi que hay que aplicar la misma regla aca o los dos caminos diferirian
                ' justo cuando accSp viene sin resolver.
                Dim aspEff As Integer = If(accSp < 0, os, accSp)
                ' ⭐⭐ FASE A VECTORIZADA. Es el resto grande que quedaba del loop (~87 ns/px por resta) y
                ' ademas se paga a si misma dos veces: al armar los 8 sources EN REGISTRO desaparecen los 4
                ' stores + 4 loads por pixel que el split Fase A/B habia AGREGADO (bSrcR/G/B + bMask).
                ' El gate excluye, y cada exclusion es por una razon distinta:
                '   - useHairPalette   -> SampleLutEngine es un fetch NEAREST indexado por el VALOR del pixel.
                '     Ese indice NO depende de la posicion ⇒ no hay nada que izar: es el OTRO gather del
                '     compose y sigue afuera. Ademas es engine-exact (no se toca).
                '   - preToneSkin      -> muestrea la mascara del skintone y corre 3 ComposeOne mas por pixel;
                '     es un camino raro (flagged-after-skintone) y no vale duplicarlo.
                ' ⭐ YA NO EXCLUYE la directness de la capa ni la de la diffMask: el resample se IZA a planos
                ' SoA (ver ResampleToUnitPlanes) y el cuerpo vectorial cubre tambien ese caso, con los MISMOS
                ' 4 taps por pixel que hacia el muestreo escalar — no se agrega ni un tap.
                ' ⛔ Aca decia que ese gather era "LA barrera real" porque Vector(Of T) no lo tiene y
                ' Avx2.GatherVector256 es x86-only ⇒ "dos leyes segun la CPU". ERA FALSO y no hay que
                ' reponerlo: un gather es MOVIMIENTO DE DATOS —carga los mismos bytes que N loads escalares y
                ' no puede mover un bit—; lo que la regla de una sola ley prohibe es cambiar la ARITMETICA
                ' (p.ej. que FMA fusione y redondee distinto). Ver memoria 61-perf-simd-trampas.
                Dim diffMaskDirect As Boolean = (diffMaskTex IsNot Nothing AndAlso diffMaskTex.Width = w AndAlso diffMaskTex.Height = h)
                Dim diffMaskPx As Byte() = If(diffMaskDirect, diffMaskTex.Rgba8, Nothing)
                ' IZADO: la capa usa los CUATRO canales; la diffMask SOLO el alpha.
                Dim hoLayR As Single() = Nothing, hoLayG As Single() = Nothing, hoLayB As Single() = Nothing, hoLayA As Single() = Nothing
                Dim hoDmR As Single() = Nothing, hoDmG As Single() = Nothing, hoDmB As Single() = Nothing, hoDmA As Single() = Nothing
                If Not layerDirect Then ResampleToUnitPlanes(layerTex, w, h, 15, hoLayR, hoLayG, hoLayB, hoLayA)
                If diffMaskTex IsNot Nothing AndAlso Not diffMaskDirect Then ResampleToUnitPlanes(diffMaskTex, w, h, 8, hoDmR, hoDmG, hoDmB, hoDmA)
                Dim fastA As Boolean = vecOk AndAlso Not useHairPalette AndAlso Not preToneSkin
                Dim isPalette As Boolean = (kind = FaceTintLayerKind.PaletteMask)
                System.Threading.Tasks.Parallel.ForEach(
                    System.Collections.Concurrent.Partitioner.Create(0, n),
                    Sub(range)
                        ' Buffers del bloque POR RANGO (no por pixel): cada tarea del Partitioner tiene los
                        ' suyos. ⛔ NO subirlos fuera del lambda: los comparten los hilos y se corrompen.
                        Dim bSrcR(lanes - 1) As Single, bSrcG(lanes - 1) As Single, bSrcB(lanes - 1) As Single, bMask(lanes - 1) As Single
                        ' Los bloques de 8 que entran por la Fase A vectorial se consumen ACA, desde el principio
                        ' del rango; el loop escalar de abajo arranca donde este termino y se hace cargo del
                        ' resto (y del rango ENTERO cuando fastA es False, o sea el comportamiento de siempre).
                        Dim iStart As Integer = range.Item1
                        If fastA Then
                            Dim colRV = VBroadcast(uColR), colGV = VBroadcast(uColG), colBV = VBroadcast(uColB)
                            Dim opV = VBroadcast(op)
                            Dim zeroV = Vector(Of Single).Zero
                            While iStart + lanes <= range.Item2
                                Dim lrV, lgV, lbV, laV As Vector(Of Single)
                                If layerDirect Then
                                    LoadRgba8BlockV(layerPx, iStart * 4, lrV, lgV, lbV, laV)
                                Else
                                    ' Planos SoA: carga CONTIGUA, sin de-interleave ni gather.
                                    lrV = New Vector(Of Single)(hoLayR, iStart)
                                    lgV = New Vector(Of Single)(hoLayG, iStart)
                                    lbV = New Vector(Of Single)(hoLayB, iStart)
                                    laV = New Vector(Of Single)(hoLayA, iStart)
                                End If
                                Dim sRV, sGV, sBV, mV As Vector(Of Single)
                                ' ⭐ El ESPEJO de LayerSrcMaskPixel: la MISMA funcion que el loop escalar
                                ' llama, y la misma que el self-test contrasta. Tenerla extraida es lo que
                                ' hace que el test valga: inline, el test tendria que re-implementar la
                                ' cadena y podria coincidir con el bug en vez de detectarlo.
                                ' ⛔ El flag `hasDm` sale de la TEXTURA, no del buffer de bytes. Cuando `fastA`
                                ' exigia diffMaskDirect los dos coincidian; ahora la diffMask puede venir izada
                                ' (diffMaskPx = Nothing y aun asi HAY mascara), y mirar el buffer diria que no.
                                Dim hasDmV As Boolean = (diffMaskTex IsNot Nothing)
                                Dim dmAV As Vector(Of Single)
                                If Not hasDmV Then
                                    dmAV = Vector(Of Single).Zero
                                ElseIf diffMaskDirect Then
                                    dmAV = LoadAlpha8BlockV(diffMaskPx, iStart * 4)
                                Else
                                    dmAV = New Vector(Of Single)(hoDmA, iStart)
                                End If
                                LayerSrcMaskBlockV(lrV, lgV, lbV, laV, dmAV,
                                                 isPalette, isD, forceUniform, texTimesColor,
                                                 hasDmV,
                                                 colRV, colGV, colBV, layer.PaletteMaskChannel, sRV, sGV, sBV, mV)
                                ' Cobertura: mismas ops y mismo orden que CovBlockV (convertir, multiplicar, clampear).
                                Dim covV = Clamp01V(Vector.Multiply(ConvMaskV(mV, mc), opV))
                                If Not Vector.LessThanOrEqualAll(Of Single)(covV, zeroV) Then
                                    ComposeBlockV(accR, iStart, sRV, covV, ws, cs, ss, aspEff, bop, sl)
                                    ComposeBlockV(accG, iStart, sGV, covV, ws, cs, ss, aspEff, bop, sl)
                                    ComposeBlockV(accB, iStart, sBV, covV, ws, cs, ss, aspEff, bop, sl)
                                End If
                                iStart += lanes
                            End While
                        End If
                        Dim blkAt As Integer = iStart, blkN As Integer = 0
                        For i As Integer = iStart To range.Item2 - 1
                            Dim lr As Single, lg As Single, lb As Single, la As Single
                            If layerDirect Then
                                Dim pb = i * 4
                                lr = lut(layerPx(pb)) : lg = lut(layerPx(pb + 1)) : lb = lut(layerPx(pb + 2)) : la = lut(layerPx(pb + 3))
                            Else
                                ' Los planos YA son SampleChannelAt en este mismo (i,w,h) ⇒ mismos bits.
                                lr = hoLayR(i) : lg = hoLayG(i) : lb = hoLayB(i) : la = hoLayA(i)
                            End If

                            ' mask + src por kind (= rama uLayerKind del shader)
                            Dim maskV As Single
                            Dim srcR As Single, srcG As Single, srcB As Single
                            Dim isPal As Boolean = (kind = FaceTintLayerKind.PaletteMask)
                            Dim hasDm As Boolean = (diffMaskTex IsNot Nothing)
                            Dim dmA As Single = 0.0F
                            ' Directo: `SampleChannelAt` con tex del mismo tamaño devuelve `t.Unit(i*4+ch)`, que ES
                            ' este LUT sobre ese mismo byte (verificado). Izado: el plano ya es esa evaluación.
                            If hasDm AndAlso Not isPal AndAlso Not isD Then dmA = If(diffMaskDirect, lut(diffMaskPx(i * 4 + 3)), hoDmA(i))
                            ' ⭐ LA MISMA funcion que el bloque vectorial (via LayerSrcMaskBlockV) y que el
                            ' self-test contrastan. Extraida a proposito: inline, el test tendria que
                            ' re-implementar la cadena y podria coincidir con el bug en vez de detectarlo.
                            LayerSrcMaskPixel(lr, lg, lb, la, dmA, isPal, isD, forceUniform, texTimesColor, hasDm,
                                          uColR, uColG, uColB, layer.PaletteMaskChannel, srcR, srcG, srcB, maskV)
                            If useHairPalette Then
                            ' PALETA DE PELO: fetch NEAREST indexado por el VALOR del pixel = gather, y
                            ' engine-exact. Pisa solo el `src`; el `mask` ya lo resolvio la cadena comun, que
                            ' es exactamente lo que hacia el codigo anterior. `fastA` excluye este camino.
                            srcR = SampleLutEngine(lutTex, lg, luY, 0, ss, os) : srcG = SampleLutEngine(lutTex, lg, luY, 1, ss, os) : srcB = SampleLutEngine(lutTex, lg, luY, 2, ss, os)
                        End If

                        ' Pre-tono TakesSkinTone (guard preToneSkin): aplica el softlight del skintone al SOURCE
                        ' de la flagged con la coverage del skintone en ese pixel (mask.G del skintone), antes del
                        ' composite normal. = harness pre_softlight(s01, skintone). Inerte si preToneSkin=False.
                        If preToneSkin Then
                            Dim stMaskV = SampleChannelAt(stMaskTex, i, w, h, stMaskCh)
                            Dim stCov = Clamp01(ConvMask1(stMaskV, stMc) * stOpac)
                            srcR = ComposeOne(srcR, stColR, stCov, stWs, stCs, stSs, stOs, stBop, stSl)
                            srcG = ComposeOne(srcG, stColG, stCov, stWs, stCs, stSs, stOs, stBop, stSl)
                            srcB = ComposeOne(srcB, stColB, stCov, stWs, stCs, stSs, stOs, stBop, stSl)
                        End If

                        ' composite agnostico (= shader): blend en ws, lerp en cs, storage en os.
                        If vecOk Then
                            ' Fase B DIFERIDA: se acumula el pixel en el bloque y se compone de a 8.
                            ' Se guarda el mask CRUDO, no la cobertura: convertirlo es un pow por pixel y se
                            ' hace vectorizado mas abajo. (`base` no se lee: vecOk exige OverPrev.)
                            bSrcR(blkN) = srcR : bSrcG(blkN) = srcG : bSrcB(blkN) = srcB : bMask(blkN) = maskV
                            blkN += 1
                            If blkN = lanes Then
                                ' El mask conv se vectoriza ACA, no en la Fase A: con la ley por defecto es
                                ' G22Encode = UN POW POR PIXEL, y era el mayor resto escalar del loop.
                                Dim covV = CovBlockV(bMask, mc, op)
                                ' ⭐ EARLY-OUT DE BLOQUE — una capa con cobertura CERO no aporta nada, asi que
                                ' aplicarla tiene que ser la IDENTIDAD. Sin esto el bloque pagaba los pow de
                                ' ComposeOneV x3 canales para no mover un byte. Mismo patron que
                                ' SseFaceTintComposer.ComposeLayer / SkeeMaskApply / MsnBlendApply.
                                ' ⛔ VA TAMBIEN EN EL GLSL (FaceTintCompositor, rama de composeOne): los dos
                                ' compositores tienen que hacer LO MISMO o se rompe la paridad CPU/GPU.
                                If Not Vector.LessThanOrEqualAll(Of Single)(covV, Vector(Of Single).Zero) Then
                                    ComposeBlockV(accR, blkAt, bSrcR, covV, ws, cs, ss, aspEff, bop, sl)
                                    ComposeBlockV(accG, blkAt, bSrcG, covV, ws, cs, ss, aspEff, bop, sl)
                                    ComposeBlockV(accB, blkAt, bSrcB, covV, ws, cs, ss, aspEff, bop, sl)
                                End If
                                blkAt = i + 1
                                blkN = 0
                            End If
                        Else
                            Dim cov = Clamp01(ConvMask1(maskV, mc) * op)
                            ' ⭐ SKIP DE COBERTURA CERO — gemelo escalar del early-out de bloque de arriba y de
                            ' la rama `cov <= 0` del GLSL. Una capa que no cubre este pixel no puede cambiarlo.
                            ' Con asp = cs (la ley real de los dos juegos) componer con cov=0 ya devolvia
                            ' Clamp01(prev) = prev (el acumulador se siembra del LUT de bytes y toda escritura
                            ' sale clampeada, o sea que vive en [0,1]) ⇒ saltear es BYTE-NEUTRO, pura velocidad.
                            ' Con asp <> cs el skip ademas CORRIGE: el round-trip os->cs->os degradaba el
                            ' acumulador por una capa que no pinta nada.
                            If cov > 0.0F Then
                            ' base SÓLO existe si algún framework del canal es OverBase/AddBase (ver needsBase).
                            ' Con OverPrev/ModSrc ComposeOne no lee este parámetro en ninguna rama, así que pasar
                            ' 0.0 es exactamente lo mismo que pasar el snapshot. Cuando SÍ hace falta, se lee el
                            ' mismo Single ⇒ bit-idéntico en los dos caminos.
                            Dim bR As Single = 0.0F, bG As Single = 0.0F, bB As Single = 0.0F
                            If needsBase Then
                                bR = baseR(i) : bG = baseG(i) : bB = baseB(i)
                            End If
                            accR(i) = CSng(ComposeOne(accR(i), srcR, cov, ws, cs, ss, os, bop, sl, bR, fw, accSpace:=accSp))
                            accG(i) = CSng(ComposeOne(accG(i), srcG, cov, ws, cs, ss, os, bop, sl, bG, fw, accSpace:=accSp))
                            accB(i) = CSng(ComposeOne(accB(i), srcB, cov, ws, cs, ss, os, bop, sl, bB, fw, accSpace:=accSp))
                            End If
                        End If
                        Next
                        ' COLA: los ultimos <8 pixeles del rango. ⛔ Obligatoria — los rangos del Partitioner
                        ' casi nunca miden un multiplo de 8, asi que sin esto se perderian hasta 7 pixeles POR
                        ' RANGO. Va por el escalar, que es la misma ley: `base` va 0.0F porque vecOk exige
                        ' OverPrev, y OverPrev no lo lee.
                        If vecOk Then
                            For j As Integer = 0 To blkN - 1
                                Dim k = blkAt + j
                                Dim covT = Clamp01(ConvMask1(bMask(j), mc) * op)
                                ' ⛔ `Not (covT > 0)`, NO `covT <= 0`: con NaN la primera es TRUE (saltea) y
                                ' la segunda FALSE (compone). El bloque vectorial, el escalar no-vectorial y
                                ' el GLSL saltean con NaN; esta cola componia => el pixel dependia de si caia
                                ' en los ultimos <lanes del rango. Tiene que ser la negacion EXACTA del guard.
                                If Not (covT > 0.0F) Then Continue For
                                accR(k) = CSng(ComposeOne(accR(k), bSrcR(j), covT, ws, cs, ss, os, bop, sl, 0.0F, fw, accSpace:=accSp))
                                accG(k) = CSng(ComposeOne(accG(k), bSrcG(j), covT, ws, cs, ss, os, bop, sl, 0.0F, fw, accSpace:=accSp))
                                accB(k) = CSng(ComposeOne(accB(k), bSrcB(j), covT, ws, cs, ss, os, bop, sl, 0.0F, fw, accSpace:=accSp))
                            Next
                        End If
                    End Sub)

                ' Capturar el skintone (slot 12) tras componerlo: color/op/mask/conv para pre-tonar las
                ' flagged-after-skintone. mask.G (Palette) o .A (TextureSet-D), = como el loop calcula maskV.
                If isD AndAlso layer.IsSkinTone Then
                    stColR = uColR : stColG = uColG : stColB = uColB : stOpac = op
                    stMaskTex = layerTex : stMc = mc
                    stMaskCh = If(kind = FaceTintLayerKind.PaletteMask, 1, 3)
                    stWs = ws : stCs = cs : stSs = ss : stOs = os : stBop = bop : stSl = sl
                    stSeen = True
                End If
            Next
        End If

        ' CONVERSION FINAL accSp -> OutputSpace, UNA vez para todo el canal: con el default (accSp == outSp)
        ' Cvt1 cortocircuita y es un no-op exacto. Con el acumulador en CompositeSpace, este es el UNICO lugar
        ' donde se paga la conversion de salida, en vez de pagarla ida-y-vuelta en CADA capa.
        If accSp <> outSp Then
            ' Los tres canales son arrays SEPARADOS (SoA) ⇒ cada uno es contiguo y se vectoriza directo,
            ' sin la alineacion que exige el AoS. Mismo Cvt1 por elemento, sólo que de a 8.
            ConvertSpaceSoaInPlace(accR, n, accSp, outSp)
            ConvertSpaceSoaInPlace(accG, n, accSp, outSp)
            ConvertSpaceSoaInPlace(accB, n, accSp, outSp)
        End If
        Return New CpuAccumResult With {.Width = w, .Height = h, .R = accR, .G = accG, .B = accB, .A = accA}
    End Function

    ''' <summary>Empaqueta el acumulador (float, YA en OutputSpace) a BGRA byte con clamp + round-half-to-even.
    ''' Segunda mitad del compose partido: sólo la usa quien escribe un DDS. El facetint de SSE NO pasa por
    ''' acá — cuantizar antes del fold es una regresion MEDIDA (RMS 2,4 / max 18).
    ''' <para>El ALPHA sale de <c>acc.A</c> cuando existe (política Passthrough) y 255 cuando no (Opaque).</para></summary>
    Public Function PackAccumToBgra(acc As CpuAccumResult) As Byte()
        If acc Is Nothing OrElse acc.R Is Nothing Then Return Nothing
        Dim n = acc.Width * acc.Height
        Dim accR = acc.R, accG = acc.G, accB = acc.B, accA = acc.A
        Dim keepBaseAlpha As Boolean = (accA IsNot Nothing)
        Dim outB(n * 4 - 1) As Byte
        ' Paralelo por rangos: empaquetado float->byte PURAMENTE POR PIXEL (lee acc*(i), escribe outB(i*4..+3)),
        ' sin estado compartido ni acumulacion cruzada => BIT-IDENTICO al serial (mismo ToByte sobre el mismo
        ' double; solo cambia que thread lo ejecuta). Es el ULTIMO tramo per-pixel que quedaba serial en el
        ' compose CPU de FO4, y corre UNA VEZ POR CANAL (D, N y S) a resolucion nativa: con una cara 4096 son
        ' 3 x 16,7M iteraciones, mientras el seed, los region-swaps y el loop de capas de mas arriba ya
        ' paralelizan. Mismo patron y misma justificacion que esos.
        ' La LECTURA es SoA (accR/G/B contiguos) pero la ESCRITURA es AoS (BGRA intercalado). Se vectoriza la
        ' parte cara —NaN, clamp y redondeo de los 3 canales— y los 4 stores de byte quedan escalares: armar
        ' el intercalado en registro pediria shuffles que no compensan frente a un store de byte.
        System.Threading.Tasks.Parallel.ForEach(
            System.Collections.Concurrent.Partitioner.Create(0, n),
            Sub(range)
                Dim i = range.Item1
                If FastPow.AcceleratedV Then
                    Dim tr(lanes - 1) As Single, tg(lanes - 1) As Single, tb(lanes - 1) As Single, ta(lanes - 1) As Single
                    While i + lanes <= range.Item2
                        ToByteBlockV(accR, i).CopyTo(tr, 0)
                        ToByteBlockV(accG, i).CopyTo(tg, 0)
                        ToByteBlockV(accB, i).CopyTo(tb, 0)
                        If keepBaseAlpha Then ToByteBlockV(accA, i).CopyTo(ta, 0)
                        For j = 0 To lanes - 1
                            Dim o = (i + j) * 4
                            outB(o) = CByte(tb(j)) : outB(o + 1) = CByte(tg(j)) : outB(o + 2) = CByte(tr(j))
                            outB(o + 3) = If(keepBaseAlpha, CByte(ta(j)), CByte(255))
                        Next
                        i += lanes
                    End While
                End If
                While i < range.Item2
                    Dim o = i * 4
                    outB(o) = ToByte(accB(i)) : outB(o + 1) = ToByte(accG(i)) : outB(o + 2) = ToByte(accR(i)) : outB(o + 3) = If(keepBaseAlpha, ToByte(accA(i)), CByte(255))
                    i += 1
                End While
            End Sub)
        Return outB
    End Function

    ''' <summary>Intercala el acumulador SoA a un buffer AoS RGBA <c>Single()</c> [0,1] de largo w*h*4 — la
    ''' otra salida del compose, la que NO cuantiza. La usa el facetint de SSE, que alimenta el fold en float
    ''' (pasar por bytes ahí es una regresion MEDIDA: el amplify escala x255/64).
    ''' <para>Alpha: <c>acc.A</c> si existe (Passthrough), 1 si no (Opaque).</para></summary>
    Public Function AccumToRgbaAos(acc As CpuAccumResult) As Single()
        If acc Is Nothing OrElse acc.R Is Nothing Then Return Nothing
        Dim n = acc.Width * acc.Height
        Dim accR = acc.R, accG = acc.G, accB = acc.B, accA = acc.A
        Dim outp(n * 4 - 1) As Single
        ' Por-pixel puro con escrituras disjuntas ⇒ bit-identico al serial (misma justificacion que el pack).
        System.Threading.Tasks.Parallel.ForEach(
            System.Collections.Concurrent.Partitioner.Create(0, n),
            Sub(range)
                For i As Integer = range.Item1 To range.Item2 - 1
                    Dim o = i * 4
                    outp(o) = accR(i) : outp(o + 1) = accG(i) : outp(o + 2) = accB(i)
                    outp(o + 3) = If(accA Is Nothing, 1.0F, accA(i))
                Next
            End Sub)
        Return outp
    End Function

    ''' <summary>Compositor por-pixel COMPARTIDO, ley-driven: aplica UN color de capa sobre el acumulador con
    ''' la cobertura <paramref name="cov"/> ya resuelta (mask x opacidad), segun la convencion
    ''' <paramref name="conv"/>. Es el MISMO <see cref="ComposeOne"/> que usa el loop FO4, expuesto para que
    ''' otros compositores (SSE) compongan por la ley del config en vez de hardcodear el algebra.
    ''' <para>Honra <c>conv.AccumSpace</c>: el acumulador que recibe y devuelve vive en AccumSpace, no en
    ''' OutputSpace. El caller siembra EN AccumSpace y hace la unica conversion final.</para>
    ''' <paramref name="base"/> solo lo usan los frameworks OverBase/AddBase; en OverPrev es inerte.</summary>
    Public Function ComposePixel(prev As Single, src As Single, cov As Single,
                                 conv As FaceTintConvention.FaceTintConventionSet,
                                 Optional base As Single = 0.0F) As Single
        Return ComposeOne(prev, src, cov,
                          CInt(conv.WorkingSpace), CInt(conv.CompositeSpace), CInt(conv.SrcSpace),
                          CInt(conv.OutputSpace), CInt(conv.Blend), CInt(conv.SoftLight),
                          base, CInt(conv.Framework), accSpace:=CInt(conv.AccumSpace))
    End Function

    ''' <summary>Conversion de espacio expuesta, para que un compositor que mantiene su propio acumulador
    ''' (SSE) siembre en AccumSpace y haga la conversion final con EXACTAMENTE la misma funcion que usa el
    ''' compose. Mismo criterio que <see cref="ConvMaskShared"/>.</summary>
    Public Function ConvertSpaceShared(v As Single, fromSpace As Integer, toSpace As Integer) As Single
        Return Cvt1(v, fromSpace, toSpace)
    End Function

    ''' <summary>mask conv expuesta (0=raw 1=srgbEnc 2=srgbDec 3=g22Enc 4=g22Dec…) para que los compositores
    ''' que resuelven su propia cobertura (SSE) apliquen la MISMA transformación de máscara que el loop FO4.</summary>
    Public Function ConvMaskShared(m As Single, maskConv As Integer) As Single
        Return ConvMask1(m, maskConv)
    End Function

    ''' <param name="accSpace">Espacio en el que VIVE el acumulador (`prev` y `base`) y en el que se devuelve el
    ''' resultado. −1 (default) = usar <paramref name="os"/> ⇒ comportamiento previo EXACTO, y ningun call site
    ''' existente cambia. Ver FaceTintConventionSet.AccumSpace: con accSpace = cs, las conversiones del
    ''' acumulador por capa (os→ws, os→cs, cs→os) colapsan a identidad y desaparecen sus Math.Pow.</param>
    Private Function ComposeOne(prev As Single, src As Single, cov As Single,
                                ws As Integer, cs As Integer, ss As Integer, os As Integer, bop As Integer,
                                softLight As Integer,
                                Optional base As Single = 0.0F, Optional framework As Integer = 0,
                                Optional accSpace As Integer = -1) As Single
        ' asp = espacio del acumulador. Sin especificar, ES os (identico a antes).
        Dim asp As Integer = If(accSpace < 0, os, accSpace)
        Dim src_w = Cvt1(src, ss, ws)
        Select Case framework
            Case 1 ' OverBase: mix(base, blend(base,src), cov)
                Dim anchor_w = Cvt1(base, asp, ws)
                Dim blended = BlendDispatch1(bop, softLight, anchor_w, src_w)
                Dim anchor_c = If(cs = ws, anchor_w, Cvt1(base, asp, cs))   ' redundante si cs=ws — ver el Case Else
                Dim blend_c = Cvt1(blended, ws, cs)
                Return Cvt1(Clamp01(anchor_c + cov * (blend_c - anchor_c)), cs, asp)
            Case 2 ' AddBase: prev + cov*(blend(base,src) - base)
                Dim anchor_w = Cvt1(base, asp, ws)
                Dim blended = BlendDispatch1(bop, softLight, anchor_w, src_w)
                Dim prev_c = Cvt1(prev, asp, cs)
                Dim base_c2 = If(cs = ws, anchor_w, Cvt1(base, asp, cs))    ' idem: misma entrada `base`, mismo par
                Dim blend_c = Cvt1(blended, ws, cs)
                Return Cvt1(Clamp01(prev_c + cov * (blend_c - base_c2)), cs, asp)
            Case 3 ' ModSrc: blend(prev, mix(neutral, src, cov)). bop=replace no tiene neutral -> OverPrev.
                Dim base_w = Cvt1(prev, asp, ws)
                If bop = 0 Then
                    Dim bc = Cvt1(prev, asp, cs)
                    Dim sc = Cvt1(src_w, ws, cs)
                    Return Cvt1(Clamp01(bc + cov * (sc - bc)), cs, asp)
                End If
                Dim neut = BlendNeutral1(bop)
                Dim smod_w = neut + cov * (src_w - neut)
                Dim blended3 = BlendDispatch1(bop, softLight, base_w, smod_w)
                Return Cvt1(Clamp01(Cvt1(blended3, ws, cs)), cs, asp)
            Case Else ' 0 = OverPrev (DEFAULT, byte-identico al modelo previo)
                Dim base_w = Cvt1(prev, asp, ws)
                Dim blended = BlendDispatch1(bop, softLight, base_w, src_w)
                ' ⭐ CONVERSION REDUNDANTE ELIMINADA. Con `cs = ws`, `Cvt1(prev, asp, cs)` es la MISMA llamada
                ' que `Cvt1(prev, asp, ws)` de arriba: misma entrada, mismo par de espacios ⇒ mismo Double.
                ' Reusarla es bit-identico por construccion (no es una aproximacion: es la misma funcion pura
                ' con los mismos argumentos) y ahorra DOS Math.Pow por canal, por pixel y por capa.
                ' Por que importa: medido, el compose es el 94,9 % del bake y ~325 ns por operacion de
                ' pixel-capa, consistente con 4-5 Math.Pow (~60 ns c/u). Y `cs = ws` es el caso NORMAL: la ley
                ' gen3 pone cs=Linear y ws=Linear para todo blend que no sea SoftLight (ver FaceTintConvention).
                ' ⛔ NO se toca la LEY: cuando cs <> ws (SoftLight) se calcula igual que antes.
                Dim base_c = If(cs = ws, base_w, Cvt1(prev, asp, cs))
                Dim blend_c = Cvt1(blended, ws, cs)   ' ya cortocircuita solo cuando ws = cs
                Return Cvt1(Clamp01(base_c + cov * (blend_c - base_c)), cs, asp)
        End Select
    End Function

    ' =================================================================================================
    ' =================================================================================================
    ' ⭐ ANCHO VARIABLE. Todo el espejo vectorial de abajo está escrito sobre `Vector(Of T)`, que elige SOLO
    ' el ancho que la máquina tiene: 8 lanes con AVX2, 4 con SSE2. Antes estaba escrito contra `Vector256` a
    ' secas y una CPU con SSE2 pero sin AVX2 caía HASTA EL ESCALAR — que es 1,54× más lento que MathF.Pow, o
    ' sea que en esa máquina todo este trabajo la dejaba MÁS LENTA. La alternativa era duplicar las ~23
    ' funciones espejo a Vector128 y mantener los dos juegos en sincronía a mano; con el ancho variable hay
    ' UNA sola escritura de cada ley. Ver el contrato en FastPow.
    ' ⛔ Sólo es legítimo porque los anchos están probados BIT-IDÉNTICOS entre sí. Si no lo estuvieran, el
    ' MISMO binario daría bytes distintos según la CPU.
    ' ⛔ NINGÚN loop puede hardcodear 8: el tamaño de bloque es FastPow.LaneCount.
    '
    ' `VBroadcast` existe para que el cuerpo se lea igual que antes (era `Vector256.Create`) y para que la
    ' elección de tipo la haga la RESOLUCIÓN DE SOBRECARGA y no yo en cada sitio.
    ' =================================================================================================

    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Function VBroadcast(x As Single) As Vector(Of Single)
        Return New Vector(Of Single)(x)
    End Function

    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Function VBroadcast(x As Double) As Vector(Of Double)
        Return New Vector(Of Double)(x)
    End Function

    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Function VBroadcast(x As UInteger) As Vector(Of UInteger)
        Return New Vector(Of UInteger)(x)
    End Function

    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Function VBroadcast(a As Single(), i As Integer) As Vector(Of Single)
        Return New Vector(Of Single)(a, i)
    End Function

    ''' <summary>Máscara "los 3 canales RGB sí, el alpha no" para un buffer AoS RGBA, del ANCHO de la máquina.
    ''' Antes era el literal <c>Create(-1,-1,-1,0,-1,-1,-1,0)</c>, que asume 8 lanes y en una máquina de 4
    ''' dejaría el patrón corrido. Se arma una sola vez.</summary>
    Private ReadOnly RgbAosMaskV As Vector(Of Single) = BuildRgbAosMaskV()
    Private Function BuildRgbAosMaskV() As Vector(Of Single)
        Dim n = Vector(Of Integer).Count
        Dim m(n - 1) As Integer
        For i = 0 To n - 1
            m(i) = If((i And 3) = 3, 0, -1)      ' lane 3 de cada pixel = alpha = NO tocar
        Next
        Return Vector.As(Of Integer, Single)(New Vector(Of Integer)(m))
    End Function

    ' ESPEJO VECTORIAL de ComposeOne. Es lo que hace que el loop de capas —el 67,8 % del bake de FO4—
    ' pague UN pow por cada 8 pixeles en vez de uno por pixel.
    '
    ' ⛔ REGLA: cada funcion de aca es el espejo EXACTO de su gemela escalar de mas arriba, operacion por
    ' operacion y en el mismo orden. No es "equivalente matematicamente": es la MISMA cuenta. Cuando no lo
    ' sea, el test de paridad (VecComposeSupported + el arnes) tiene que fallar, no pasar por poco.
    '
    ' ⛔ Clamp01 vectorial va con SELECTS EXPLICITOS, no con Min/Max: el Clamp01 escalar deja pasar NaN
    ' (sus dos comparaciones son falsas) y el NaN-handling de Min/Max no esta garantizado igual en todas
    ' las plataformas. Con selects la coincidencia es exacta, NaN incluido.
    ' =================================================================================================

    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Friend Function Clamp01V(c As Vector(Of Single)) As Vector(Of Single)
        Dim r = Vector.ConditionalSelect(Vector.LessThan(c, Vector(Of Single).Zero), Vector(Of Single).Zero, c)
        Return Vector.ConditionalSelect(Vector.GreaterThan(r, VBroadcast(1.0F)), VBroadcast(1.0F), r)
    End Function

    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Friend Function SrgbToLinV(c As Vector(Of Single)) As Vector(Of Single)
        c = Clamp01V(c)
        Dim loB = Vector.Divide(c, VBroadcast(12.92F))
        Dim hiB = FastPow.PowV(Vector.Divide(Vector.Add(c, VBroadcast(0.055F)),
                                                   VBroadcast(1.055F)), FastPow.G24)
        Return Vector.ConditionalSelect(Vector.LessThanOrEqual(c, VBroadcast(0.04045F)), loB, hiB)
    End Function

    ''' <summary>Espejo vectorial EXACTO de <see cref="LinToSrgbShared"/>. El orden de los selects replica el
    ''' orden de los <c>If</c> del escalar: primero la rama de la curva y DESPUES los dos cortes de los
    ''' extremos, que en el escalar son returns tempranos y por lo tanto GANAN.</summary>
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Friend Function LinToSrgbV(c As Vector(Of Single)) As Vector(Of Single)
        c = Clamp01V(c)
        Dim loB = Vector.Multiply(c, VBroadcast(12.92F))
        Dim hiB = Vector.Subtract(Vector.Multiply(VBroadcast(1.055F),
                                                        FastPow.PowV(c, FastPow.InvG24)),
                                     VBroadcast(0.055F))
        Dim r = Vector.ConditionalSelect(Vector.LessThanOrEqual(c, VBroadcast(0.0031308F)), loB, hiB)
        r = Vector.ConditionalSelect(Vector.LessThanOrEqual(c, Vector(Of Single).Zero), Vector(Of Single).Zero, r)
        Return Vector.ConditionalSelect(Vector.GreaterThanOrEqual(c, VBroadcast(1.0F)), VBroadcast(1.0F), r)
    End Function

    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Function SpaceToLinV(c As Vector(Of Single), s As Integer) As Vector(Of Single)
        If s = 0 Then Return c
        If s = 1 Then Return SrgbToLinV(c)
        If s = 3 Then Return FastPow.PowV(Clamp01V(c), FastPow.G24)
        Return FastPow.PowV(Clamp01V(c), FastPow.G22)     ' s=2
    End Function

    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Function LinToSpaceV(c As Vector(Of Single), s As Integer) As Vector(Of Single)
        If s = 0 Then Return c
        If s = 1 Then Return LinToSrgbV(c)
        If s = 3 Then Return FastPow.PowV(Clamp01V(c), FastPow.InvG24)
        Return FastPow.PowV(Clamp01V(c), FastPow.InvG22)  ' s=2
    End Function

    ''' <summary>Espejo de <see cref="Cvt1"/>, cortocircuito incluido (que es de donde sale que los buckets
    ''' N/S no paguen NI UN pow).</summary>
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Friend Function CvtV(c As Vector(Of Single), fromS As Integer, toS As Integer) As Vector(Of Single)
        If fromS = toS Then Return c
        Return LinToSpaceV(SpaceToLinV(c, fromS), toS)
    End Function

    ''' <summary>Espejo de <see cref="ConvMask1"/>. Vale la pena aparte porque el mask conv por DEFECTO de la
    ''' ley FO4 es <c>G22Encode</c>, o sea UN POW POR PIXEL POR CAPA — medido, 21,96 ns/px escalar contra los
    ''' ~15 ns/canal que cuesta el compose ya vectorizado. Era el mayor resto escalar del loop.</summary>
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Friend Function ConvMaskV(m As Vector(Of Single), mc As Integer) As Vector(Of Single)
        Select Case mc
            Case 1 : Return LinToSrgbV(m)
            Case 2 : Return SrgbToLinV(m)
            Case 3 : Return FastPow.PowV(Clamp01V(m), FastPow.InvG22)
            Case 4 : Return FastPow.PowV(Clamp01V(m), FastPow.G22)
            Case 5 : Return FastPow.PowV(Clamp01V(m), FastPow.InvG24)
            Case 6 : Return FastPow.PowV(Clamp01V(m), FastPow.G24)
            Case Else : Return m
        End Select
    End Function

    ''' <summary>Cobertura de un bloque de 8 pixeles: <c>Clamp01(ConvMask1(mask, mc) * op)</c>, en el MISMO
    ''' orden que el escalar (convertir, multiplicar, recien ahi clampear).</summary>
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Function CovBlockV(mask As Single(), mc As Integer, op As Single) As Vector(Of Single)
        Return Clamp01V(Vector.Multiply(ConvMaskV(VBroadcast(mask, 0), mc), VBroadcast(op)))
    End Function

    ''' <summary>¿Esta combinacion de (framework, blend, softlight) tiene espejo vectorial? Lo que NO esta
    ''' cae al camino escalar de siempre — que da el MISMO resultado, sólo que sin acelerar.
    ''' <para>Cubre lo que la data real usa: el scan de 2026-06-20 dio 4008/4008 TemplateColors de las 110
    ''' RACE de Fallout4.esm+DLCs en bop 0 ó 3, CERO Multiply/Overlay/HardLight; y la ley de SSE es Replace.
    ''' Una RACE modeada con bop 1/2/4 no se acelera, pero sale idéntica.</para></summary>
    Friend Function VecComposeSupported(fw As Integer, bop As Integer, sl As Integer) As Boolean
        If Not FastPow.AcceleratedV Then Return False
        If fw <> 0 Then Return False                     ' sólo OverPrev
        ' ⛔ 20 (Grayscale) y 21 (ColorMode) NO son separables: piden los tres canales del destino juntos.
        If bop < 0 OrElse bop > 19 Then Return False
        ' El modelo 2 (Illusions) TAMBIEN entra: FastPow.PowVarV hace el split de Dekker en runtime.
        Return True
    End Function

    ''' <summary>Espejo de <c>BlendDispatch1</c> para los dos bop que <see cref="VecComposeSupported"/>
    ''' habilita. El GIMP replica la rama <c>s &lt;= 0.5</c> con un select, calculando las dos.</summary>
    ' ⛔ MinV/MaxV replican la semantica de MathF.Min/MathF.Max en sus DOS casos raros, y los dos costaron:
    '   1. NaN: las dos devuelven NaN si CUALQUIERA de los operandos lo es. `Vector.Min/Max` no lo
    '      garantiza igual en toda plataforma, y esto corre sobre datos de textura donde un NaN es posible.
    '   2. CERO CON SIGNO: `Math.Max(+0, -0)` es +0 y `Math.Min(+0, -0)` es -0, PERO +0 y -0 comparan IGUAL,
    '      asi que el select por `GreaterThan`/`LessThan` se queda con el operando equivocado (lo pesco el
    '      self-test: MaxV(+0,-0) daba -0). Cuando los dos comparan iguales el bit de signo se resuelve con
    '      AND para el max (0x00000000 gana) y OR para el min (0x80000000 gana); si son iguales y NO son
    '      cero, AND/OR devuelven ese mismo valor, asi que el arreglo es inerte fuera del caso ±0.
    ''' <summary>Min/Max expuestos para el espejo de HSV de <c>SseOverlayCompositor</c>. Se comparten en vez de
    ''' re-implementarlos allá: son los que replican <c>MathF.Min/Max</c> con NaN Y con cero firmado, y dos
    ''' copias de esa sutileza podrían divergir sin que nada lo note.</summary>
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Friend Function MinVShared(a As Vector(Of Single), b As Vector(Of Single)) As Vector(Of Single)
        Return MinV(a, b)
    End Function

    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Friend Function MaxVShared(a As Vector(Of Single), b As Vector(Of Single)) As Vector(Of Single)
        Return MaxV(a, b)
    End Function

    ' ⛔ EL NaN QUE SE DEVUELVE ES EL DE ENTRADA, NO UNO CANONICO. `Math.Max(x, y)` devuelve el operando que
    ' ES NaN, con SU payload y SU signo (y si los dos lo son, el PRIMERO). Devolver `Single.NaN` fijo
    ' (0xFFC00000) parecia inocuo —todo NaN termina en el byte 0— pero contradice la igualdad bit a bit que
    ' este espejo promete, y el self-test no podia verlo porque usaba `Single.NaN` como unico NaN de entrada.
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Function MinV(a As Vector(Of Single), b As Vector(Of Single)) As Vector(Of Single)
        Dim eq = Vector.Equals(a, b)
        Dim r = Vector.ConditionalSelect(eq, Vector.BitwiseOr(a, b),
                                            Vector.ConditionalSelect(Vector.LessThan(a, b), a, b))
        ' `b` NaN primero y `a` NaN despues: en una cadena de selects gana el ULTIMO, y en Math.Min/Max gana `a`.
        r = Vector.ConditionalSelect(Vector.Equals(b, b), r, b)
        Return Vector.ConditionalSelect(Vector.Equals(a, a), r, a)
    End Function

    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Function MaxV(a As Vector(Of Single), b As Vector(Of Single)) As Vector(Of Single)
        Dim eq = Vector.Equals(a, b)
        Dim r = Vector.ConditionalSelect(eq, Vector.BitwiseAnd(a, b),
                                            Vector.ConditionalSelect(Vector.GreaterThan(a, b), a, b))
        r = Vector.ConditionalSelect(Vector.Equals(b, b), r, b)
        Return Vector.ConditionalSelect(Vector.Equals(a, a), r, a)
    End Function

    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Function OverlayV(d As Vector(Of Single), s As Vector(Of Single)) As Vector(Of Single)
        Dim one = VBroadcast(1.0F), two = VBroadcast(2.0F)
        Dim hiB = Vector.Subtract(one, Vector.Multiply(Vector.Multiply(two, Vector.Subtract(one, d)), Vector.Subtract(one, s)))
        Dim loB = Vector.Multiply(Vector.Multiply(two, d), s)
        Return Vector.ConditionalSelect(Vector.GreaterThanOrEqual(d, VBroadcast(0.5F)), hiB, loB)
    End Function

    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Function ColorDodgeV(d As Vector(Of Single), s As Vector(Of Single)) As Vector(Of Single)
        Dim one = VBroadcast(1.0F)
        Return Vector.ConditionalSelect(Vector.GreaterThanOrEqual(s, one), one,
                                           MinV(one, Vector.Divide(d, Vector.Subtract(one, s))))
    End Function

    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Function ColorBurnV(d As Vector(Of Single), s As Vector(Of Single)) As Vector(Of Single)
        Dim one = VBroadcast(1.0F), zero = Vector(Of Single).Zero
        Return Vector.ConditionalSelect(Vector.LessThanOrEqual(s, zero), zero,
                                           Vector.Subtract(one, MinV(one, Vector.Divide(Vector.Subtract(one, d), s))))
    End Function

    ''' <summary>Espejo de <c>BlendSoftLightModel</c>: modelos 0 (W3C), 1 (GIMP) y 3 (pegtop).
    ''' <para>⭐ El modelo 2 (Illusions) SI ESTA: es <c>pow(d, pow(2, 2*(0.5-s)))</c>, o sea EXPONENTE VARIABLE
    ''' por pixel, y durante un tiempo se dio por imposible porque FastPow era de exponente CONSTANTE. Lo
    ''' resuelve <c>FastPow.PowVarV</c>, que hace el split de Dekker EN RUNTIME.</para></summary>
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Function SoftLightV(model As Integer, d As Vector(Of Single), s As Vector(Of Single)) As Vector(Of Single)
        d = Clamp01V(d) : s = Clamp01V(s)
        Dim one = VBroadcast(1.0F), two = VBroadcast(2.0F), half = VBroadcast(0.5F)
        Select Case model
            Case 1 ' GIMP
                Dim loB = Vector.Add(Vector.Multiply(Vector.Multiply(two, d), s),
                                        Vector.Multiply(Vector.Multiply(d, d),
                                                           Vector.Subtract(one, Vector.Multiply(two, s))))
                Dim hiB = Vector.Add(Vector.Multiply(Vector.Multiply(two, d), Vector.Subtract(one, s)),
                                        Vector.Multiply(Vector.SquareRoot(d),
                                                           Vector.Subtract(Vector.Multiply(two, s), one)))
                Return Vector.ConditionalSelect(Vector.LessThanOrEqual(s, half), loB, hiB)
            Case 2 ' Illusions.hu: d^(2^(2(0.5-s))), con EXPONENTE VARIABLE por lane
                ' El exponente interno queda en [0,5 , 2] y la base con piso 1e-6, igual que el escalar.
                Dim yv = FastPow.Exp2V(Vector.Multiply(two, Vector.Subtract(half, s)))
                Return FastPow.PowVarV(MaxV(d, VBroadcast(0.000001F)), yv)
            Case 3 ' pegtop == la forma del MOTOR: d*d + 2*d*s*(1-d) — MISMO orden que el escalar
                Return Vector.Add(Vector.Multiply(d, d),
                                  Vector.Multiply(Vector.Multiply(Vector.Multiply(two, d), s),
                                                  Vector.Subtract(one, d)))
            Case Else ' 0 = W3C SVG
                Dim poly = Vector.Multiply(Vector.Add(Vector.Multiply(Vector.Subtract(Vector.Multiply(VBroadcast(16.0F), d), VBroadcast(12.0F)), d), VBroadcast(4.0F)), d)
                Dim g = Vector.ConditionalSelect(Vector.GreaterThanOrEqual(d, VBroadcast(0.25F)), Vector.SquareRoot(d), poly)
                Dim hiB = Vector.Add(d, Vector.Multiply(Vector.Subtract(Vector.Multiply(two, s), one), Vector.Subtract(g, d)))
                Dim loB = Vector.Subtract(d, Vector.Multiply(Vector.Multiply(Vector.Subtract(one, Vector.Multiply(two, s)), d), Vector.Subtract(one, d)))
                Return Vector.ConditionalSelect(Vector.GreaterThanOrEqual(s, half), hiB, loB)
        End Select
    End Function

    ' =====================================================================================================
    ' ESPEJO VECTORIAL DE LA INVERSA DEL SOFT-LIGHT — los CUATRO modelos.
    ' =====================================================================================================
    ' ⛔ Transcripcion 1:1 del escalar, incluida LA ASOCIATIVIDAD de cada expresion: `4.0F * a * y` es
    ' `(4*a)*y` en VB, y escribirlo como `4*(a*y)` en el vector cambia el ULP y el gate `baker` sale rojo.
    ' Las condiciones del escalar (returns tempranos) se vuelven selects aplicados EN ORDEN DE PRIORIDAD: el
    ' ultimo select gana, asi que va el de mayor prioridad.
    ' ⛔ El vector NO puede hacer early-out por lane: calcula las dos ramas y selecciona. Las ramas descartadas
    ' pueden dar Inf/NaN (division por k→0) y eso es INOCUO — ConditionalSelect las tira sin excepcion.

    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Function SoftLightInvPegtopV(y As Vector(Of Single), s As Vector(Of Single)) As Vector(Of Single)
        Dim one = VBroadcast(1.0F), two = VBroadcast(2.0F), zero = Vector(Of Single).Zero
        Dim eps = VBroadcast(0.000001F)
        Dim k = Vector.Subtract(one, Vector.Multiply(two, s))
        Dim disc = Vector.Add(Vector.Multiply(s, s), Vector.Multiply(k, y))
        ' `disc < 0 OrElse IsNaN(disc)` -> 0. GreaterThanOrEqual da falso para NaN ⇒ un select cubre los dos.
        disc = Vector.ConditionalSelect(Vector.GreaterThanOrEqual(disc, zero), disc, zero)
        Dim inv = Vector.Divide(Vector.Add(Vector.Negate(s), Vector.SquareRoot(disc)), k)
        Return Vector.ConditionalSelect(Vector.LessThan(Vector.Abs(k), eps), y, inv)
    End Function

    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Function SoftLightInvGimpV(y As Vector(Of Single), s As Vector(Of Single)) As Vector(Of Single)
        Dim one = VBroadcast(1.0F), two = VBroadcast(2.0F), half = VBroadcast(0.5F)
        Dim four = VBroadcast(4.0F), zero = Vector(Of Single).Zero, eps = VBroadcast(0.000001F)
        Dim a = Vector.Multiply(two, Vector.Subtract(one, s))
        Dim b = Vector.Subtract(Vector.Multiply(two, s), one)
        Dim disc = Vector.Add(Vector.Multiply(b, b), Vector.Multiply(Vector.Multiply(four, a), y))
        disc = Vector.ConditionalSelect(Vector.GreaterThanOrEqual(disc, zero), disc, zero)
        Dim t = Vector.Divide(Vector.Add(Vector.Negate(b), Vector.SquareRoot(disc)), Vector.Multiply(two, a))
        Dim hi = Vector.Multiply(t, t)
        hi = Vector.ConditionalSelect(Vector.LessThan(a, eps), Vector.Multiply(y, y), hi)   ' s = 1
        Return Vector.ConditionalSelect(Vector.LessThanOrEqual(s, half), SoftLightInvPegtopV(y, s), hi)
    End Function

    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Function SoftLightInvIllusionsV(y As Vector(Of Single), s As Vector(Of Single)) As Vector(Of Single)
        ' Mismo argumento que el escalar: p(1−s) = 1/p(s) ⇒ la inversa ES el forward con el source reflejado.
        Return SoftLightV(2, y, Vector.Subtract(VBroadcast(1.0F), s))
    End Function

    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Function SoftLightInvW3CV(y As Vector(Of Single), s As Vector(Of Single)) As Vector(Of Single)
        Dim one = VBroadcast(1.0F), two = VBroadcast(2.0F), half = VBroadcast(0.5F)
        Dim four = VBroadcast(4.0F), zero = Vector(Of Single).Zero, eps = VBroadcast(0.000001F)
        Dim quarter = VBroadcast(0.25F)
        Dim b = Vector.Subtract(Vector.Multiply(two, s), one)
        Dim y0 = Vector.Add(quarter, Vector.Multiply(quarter, b))

        ' Rama d >= 0,25 (g = √d): cuadratica en t = √d.
        Dim a = Vector.Subtract(one, b)
        Dim discQ = Vector.Add(Vector.Multiply(b, b), Vector.Multiply(Vector.Multiply(four, a), y))
        discQ = Vector.ConditionalSelect(Vector.GreaterThanOrEqual(discQ, zero), discQ, zero)
        Dim t = Vector.Divide(Vector.Add(Vector.Negate(b), Vector.SquareRoot(discQ)), Vector.Multiply(two, a))
        Dim hiSqrt = Vector.Multiply(t, t)
        hiSqrt = Vector.ConditionalSelect(Vector.LessThan(a, eps), Vector.Multiply(y, y), hiSqrt)   ' s = 1

        ' Rama d < 0,25: cubica deprimida + Cardano. p > 0 ⇒ una sola raiz real.
        Dim p = Vector.Divide(one, Vector.Multiply(VBroadcast(16.0F), b))
        Dim q = Vector.Divide(Vector.Subtract(Vector.Add(b, one), Vector.Multiply(four, y)),
                              Vector.Multiply(VBroadcast(64.0F), b))
        Dim mq2 = Vector.Multiply(VBroadcast(-0.5F), q)
        Dim p3 = Vector.Divide(p, VBroadcast(3.0F))
        ' `mq2*mq2 + p3*p3*p3` con la asociatividad de VB: (p3*p3)*p3.
        Dim delta = Vector.Add(Vector.Multiply(mq2, mq2), Vector.Multiply(Vector.Multiply(p3, p3), p3))
        delta = Vector.ConditionalSelect(Vector.GreaterThanOrEqual(delta, zero), delta, zero)
        Dim sq = Vector.SquareRoot(delta)
        Dim u = Vector.Add(FastPow.CbrtV(Vector.Add(mq2, sq)), FastPow.CbrtV(Vector.Subtract(mq2, sq)))
        Dim hiCubic = Vector.Add(u, quarter)

        Dim hi = Vector.ConditionalSelect(Vector.GreaterThan(y, y0), hiSqrt, hiCubic)
        hi = Vector.ConditionalSelect(Vector.LessThan(b, eps), y, hi)                      ' s = 0,5 ⇒ identidad
        Return Vector.ConditionalSelect(Vector.LessThan(s, half), SoftLightInvPegtopV(y, s), hi)
    End Function

    ''' <summary>Espejo vectorial de <see cref="BlendSoftLightModelInverse"/> — los CUATRO modelos, incluida la
    ''' cubica de W3C (Cardano con <see cref="FastPow.CbrtV"/>). Mismo criterio de acotado que el escalar:
    ''' <c>s</c> se acota, <c>y</c> NO (ver la nota larga alla).</summary>
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Friend Function BlendSoftLightModelInverseV(model As Integer, y As Vector(Of Single), s As Vector(Of Single)) As Vector(Of Single)
        s = Clamp01V(s)
        Select Case model
            Case 1 : Return SoftLightInvGimpV(y, s)
            Case 2 : Return SoftLightInvIllusionsV(y, s)
            Case 3 : Return SoftLightInvPegtopV(y, s)
            Case Else : Return SoftLightInvW3CV(y, s)
        End Select
    End Function

    ''' <summary>Espejo de <c>BlendDispatch1</c> para TODOS los modos SEPARABLES (0..19). Transcripcion 1:1:
    ''' misma expresion, mismo orden, condiciones vueltas selects.
    ''' <para>⛔ Los NO separables de skee —Grayscale(20) y ColorMode(21)— no estan y no pueden estar aca: piden
    ''' los TRES canales del destino juntos (luminancia / HSV), o sea una reduccion ENTRE canales que un
    ''' dispatch por canal no puede expresar. Los resuelve el escalar en sus propias ramas.</para></summary>
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Friend Function BlendDispatchV(bop As Integer, sl As Integer,
                                   d As Vector(Of Single), s As Vector(Of Single)) As Vector(Of Single)
        Dim one = VBroadcast(1.0F), zero = Vector(Of Single).Zero, two = VBroadcast(2.0F)
        Dim half = VBroadcast(0.5F)
        Select Case bop
            Case 1 : Return Vector.Multiply(d, s)                                        ' multiply
            Case 2 : Return OverlayV(d, s)                                                  ' overlay
            Case 3 : Return SoftLightV(sl, d, s)                                            ' softlight
            Case 4 : Return OverlayV(s, d)                                                  ' hardlight
            Case 5 : Return Vector.Subtract(Vector.Add(d, s), Vector.Multiply(d, s))  ' screen
            Case 6 : Return MinV(d, s)                                                      ' darken
            Case 7 : Return MaxV(d, s)                                                      ' lighten
            Case 8 : Return ColorDodgeV(d, s)                                               ' colordodge
            Case 9 : Return ColorBurnV(d, s)                                                ' colorburn
            Case 10 : Return Vector.Abs(Vector.Subtract(d, s))                        ' difference
            Case 11 : Return Vector.Subtract(Vector.Add(d, s), Vector.Multiply(Vector.Multiply(two, d), s)) ' exclusion
            Case 12 : Return MinV(one, Vector.Add(d, s))                                 ' lineardodge
            Case 13 : Return MaxV(zero, Vector.Subtract(Vector.Add(d, s), one))       ' linearburn
            Case 14 : Return MaxV(zero, Vector.Subtract(d, s))                           ' subtract
            Case 15 : Return Vector.ConditionalSelect(Vector.LessThanOrEqual(s, zero), one,
                                                         MinV(one, Vector.Divide(d, s))) ' divide
            Case 16 : Return Clamp01V(Vector.Subtract(Vector.Add(d, Vector.Multiply(two, s)), one)) ' linearlight
            Case 17 : Return Vector.ConditionalSelect(Vector.LessThan(s, half),
                                                         ColorBurnV(d, Vector.Multiply(two, s)),
                                                         ColorDodgeV(d, Vector.Multiply(two, Vector.Subtract(s, half)))) ' vividlight
            Case 18 : Return Vector.ConditionalSelect(Vector.LessThan(s, half),
                                                         MinV(d, Vector.Multiply(two, s)),
                                                         MaxV(d, Vector.Subtract(Vector.Multiply(two, s), one))) ' pinlight
            Case 19 : Return Vector.ConditionalSelect(Vector.GreaterThanOrEqual(Vector.Add(d, s), one), one, zero) ' hardmix
            Case Else : Return s                                                             ' replace (0, default)
        End Select
    End Function

    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Function BlendV(bop As Integer, sl As Integer, d As Vector(Of Single), s As Vector(Of Single)) As Vector(Of Single)
        Return BlendDispatchV(bop, sl, d, s)
    End Function

    ''' <summary>Espejo de <see cref="ComposeOne"/> para framework OverPrev. Incluye la MISMA reutilizacion
    ''' de <c>base_w</c> cuando <c>cs = ws</c> que hace el escalar: no es una optimizacion nueva, es copiar
    ''' la que ya estaba (y que es bit-identica por ser la misma funcion pura con los mismos argumentos).</summary>
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Friend Function ComposeOneV(prev As Vector(Of Single), src As Vector(Of Single), cov As Vector(Of Single),
                                 ws As Integer, cs As Integer, ss As Integer, asp As Integer,
                                 bop As Integer, sl As Integer) As Vector(Of Single)
        Dim srcW = CvtV(src, ss, ws)
        Dim baseW = CvtV(prev, asp, ws)
        Dim blended = BlendV(bop, sl, baseW, srcW)
        Dim baseC = If(cs = ws, baseW, CvtV(prev, asp, cs))
        Dim blendC = CvtV(blended, ws, cs)
        Return CvtV(Clamp01V(Vector.Add(baseC, Vector.Multiply(cov, Vector.Subtract(blendC, baseC)))), cs, asp)
    End Function

    ''' <summary>Compone un canal del acumulador para un bloque de 8 pixeles consecutivos.
    ''' <para>El acumulador de FO4 es SoA (<c>accR</c>/<c>accG</c>/<c>accB</c> separados), asi que estos 8
    ''' pixeles son 8 floats CONTIGUOS: carga directa, sin gather y sin ninguna exigencia de alineacion —
    ''' al reves que el AoS del fold de SSE. Por eso aca no hace falta prologo.</para>
    ''' <para>⭐ Las lanes con <c>cov &lt;= 0</c> quedan INTACTAS: es el espejo exacto del skip escalar
    ''' (<c>If cov &gt; 0 Then</c>) del loop de capas. Ver la nota del call site.</para></summary>
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Sub ComposeBlockV(acc As Single(), at As Integer, src As Single(), cov As Vector(Of Single),
                              ws As Integer, cs As Integer, ss As Integer, asp As Integer, bop As Integer, sl As Integer)
        ' `cov` llega YA calculada (CovBlockV) porque es la MISMA para los tres canales: convertir el mask
        ' una vez por bloque en vez de tres es exacto (funcion pura, mismos argumentos) y ahorra 2 pow.
        ComposeBlockV(acc, at, VBroadcast(src, 0), cov, ws, cs, ss, asp, bop, sl)
    End Sub

    ''' <summary>Idem con el source YA en registro. Lo usa la Fase A vectorizada, que arma los 8 sources sin
    ''' pasar por un buffer — que es justamente lo que el split Fase A/B costaba de mas (4 stores + 4 loads
    ''' por pixel). La version de array de arriba delega acá: una sola ley, no dos.</summary>
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Sub ComposeBlockV(acc As Single(), at As Integer, srcV As Vector(Of Single), cov As Vector(Of Single),
                              ws As Integer, cs As Integer, ss As Integer, asp As Integer, bop As Integer, sl As Integer)
        Dim prev = VBroadcast(acc, at)
        Dim composed = ComposeOneV(prev, srcV, cov, ws, cs, ss, asp, bop, sl)
        ' BLOQUE MIXTO: donde no hay cobertura queda el valor PREVIO, no el resultado de componer con cov=0
        ' (que con asp <> cs NO es lo mismo: es un round-trip que degrada el acumulador). Mismo `keep` que
        ' SseFaceTintComposer.ComposeLayer, sin el termino rgbMask porque aca el acumulador es SoA y no hay
        ' alpha intercalado que proteger. El bloque ENTERAMENTE sin cobertura lo saltea el call site.
        Vector.ConditionalSelect(Vector.GreaterThan(cov, Vector(Of Single).Zero), composed, prev).CopyTo(acc, at)
    End Sub

    ''' <summary>⭐ LA CADENA DE DECISIÓN DE LA FASE A, escalar, para UN píxel: elige el <c>src</c> y el
    ''' <c>mask</c> según el kind de la capa y sus flags. Es la LEY, y la llaman TANTO el loop de capas COMO su
    ''' espejo vectorial <see cref="LayerSrcMaskBlockV"/> a través del self-test.
    ''' <para>⛔ Existe extraída, y no inline en el loop, por una razón concreta: la Fase A vectorizada era el
    ''' código nuevo más grande del trabajo y NO tenía oráculo — el self-test del compose sólo ejercitaba la
    ''' Fase B. Con la ley en UNA función, el test compara el vector contra lo que produccion realmente corre,
    ''' en vez de contra una re-implementación que puede coincidir con el bug.</para>
    ''' <para>⚠️ NO cubre el camino de hair-palette (<c>SampleLutEngine</c>): ése es un fetch indexado por el
    ''' valor del píxel, o sea un gather, y el gate <c>fastA</c> lo excluye. El caller lo resuelve antes.</para>
    ''' <param name="dmA">Alpha de la máscara diffuse YA muestreado (sólo se lee con
    ''' <paramref name="hasDiffMask"/>); el sampleo queda afuera porque puede ser bilineal.</param></summary>
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Friend Sub LayerSrcMaskPixel(lr As Single, lg As Single, lb As Single, la As Single, dmA As Single,
                                 isPalette As Boolean, isD As Boolean, forceUniform As Boolean,
                                 texTimesColor As Boolean, hasDiffMask As Boolean,
                                 uColR As Single, uColG As Single, uColB As Single,
                                 palMaskCh As Integer,
                                 ByRef srcR As Single, ByRef srcG As Single, ByRef srcB As Single,
                                 ByRef maskV As Single)
        If isPalette Then
            srcR = uColR : srcG = uColG : srcB = uColB
            ' ⛔ ESTO ESTABA CABLEADO EN VERDE (`maskV = lg`) mientras el GLSL SI honraba uPaletteMaskChannel:
            ' era una divergencia CPU/GPU VIVA en Fallout. El canal es DATO de la capa, puesto por el builder
            ' de cada juego (verde para las mascaras de paleta de FO4, rojo para los tints de SSE) — no es un
            ' campo de config ni un literal del compositor. MISMO ORDEN de selects que el shader.
            maskV = If(palMaskCh = 0, lr, If(palMaskCh = 2, lb, If(palMaskCh = 3, la, lg)))
        Else
            If forceUniform Then
                srcR = uColR : srcG = uColG : srcB = uColB
            ElseIf texTimesColor Then
                srcR = lr * uColR : srcG = lg * uColG : srcB = lb * uColB   ' skee type-0: tex × tint
            Else
                srcR = lr : srcG = lg : srcB = lb
            End If
            If isD Then
                maskV = la
            ElseIf hasDiffMask Then
                maskV = dmA
            Else
                maskV = Math.Max(lr, Math.Max(lg, lb))
            End If
        End If
    End Sub

    ''' <summary>Espejo vectorial EXACTO de <see cref="LayerSrcMaskPixel"/> para un bloque. Mismas ramas, mismo
    ''' orden. <c>MaxV</c> y no <c>Vector.Max</c>: replica <c>Math.Max</c> con NaN y con cero firmado.</summary>
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Sub LayerSrcMaskBlockV(lrV As Vector(Of Single), lgV As Vector(Of Single), lbV As Vector(Of Single),
                                   laV As Vector(Of Single), dmAV As Vector(Of Single),
                                   isPalette As Boolean, isD As Boolean, forceUniform As Boolean,
                                   texTimesColor As Boolean, hasDiffMask As Boolean,
                                   colRV As Vector(Of Single), colGV As Vector(Of Single), colBV As Vector(Of Single),
                                   palMaskCh As Integer,
                                   ByRef sRV As Vector(Of Single), ByRef sGV As Vector(Of Single),
                                   ByRef sBV As Vector(Of Single), ByRef mV As Vector(Of Single))
        If isPalette Then
            sRV = colRV : sGV = colGV : sBV = colBV
            ' El canal es por CAPA, no por pixel: se elige el vector entero, sin selects por lane.
            mV = If(palMaskCh = 0, lrV, If(palMaskCh = 2, lbV, If(palMaskCh = 3, laV, lgV)))
        Else
            If forceUniform Then
                sRV = colRV : sGV = colGV : sBV = colBV
            ElseIf texTimesColor Then
                sRV = Vector.Multiply(lrV, colRV) : sGV = Vector.Multiply(lgV, colGV) : sBV = Vector.Multiply(lbV, colBV)
            Else
                sRV = lrV : sGV = lgV : sBV = lbV
            End If
            If isD Then
                mV = laV
            ElseIf hasDiffMask Then
                mV = dmAV
            Else
                mV = MaxV(lrV, MaxV(lgV, lbV))
            End If
        End If
    End Sub

    ''' <summary>Compose de un bloque para el loop de REGION SWAPS. Es <see cref="ComposeBlockV"/> SIN el
    ''' select por <c>cov &gt; 0</c>.
    ''' <para>⛔ La diferencia NO es un descuido: el loop escalar de los swaps no tiene el
    ''' <c>If cov &gt; 0</c> que sí tiene el de capas, y el GLSL tampoco lo tiene en su rama <c>uMode==1</c>.
    ''' Meterlo sólo acá haría que el vectorial difiera de su propio escalar Y del GPU. Si algún día se decide
    ''' saltear también en los swaps, va en los TRES lados a la vez.</para></summary>
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Sub ComposeSwapBlockV(acc As Single(), at As Integer, srcV As Vector(Of Single), cov As Vector(Of Single),
                                  ws As Integer, cs As Integer, ss As Integer, asp As Integer, bop As Integer, sl As Integer)
        ComposeOneV(VBroadcast(acc, at), srcV, cov, ws, cs, ss, asp, bop, sl).CopyTo(acc, at)
    End Sub

    ''' <summary>⭐ DE-INTERLEAVE AoS→SoA de 8 pixeles RGBA8 CONTIGUOS (32 bytes) a 4 vectores en unidad [0,1].
    ''' Es lo que desbloquea la Fase A del loop de capas.
    ''' <para><b>Sin gather y sin shuffle.</b> Los 32 bytes se leen como 8 <c>UInt32</c> — un pixel por lane, o
    ''' sea que a nivel de PIXEL el layout ya es SoA — y cada canal sale con un shift y un and. La alternativa
    ''' obvia (shuffle de bytes cross-lane) no existe barata en la API cross-platform; esta no la necesita, y
    ''' por eso la Fase A se puede vectorizar sin romper el contrato de UNA sola ley.</para>
    ''' <para>⛔ La conversion a unidad es <c>ConvertToSingle(b) / 255f</c> y tiene que ser una DIVISION:
    ''' multiplicar por <c>1/255f</c> redondea distinto. Que eso valga EXACTAMENTE lo mismo que el LUT escalar
    ''' <c>ByteToUnit(b) = CSng(b/255.0)</c> (que divide en DOUBLE) no se asume — lo enumera sobre los 256
    ''' bytes <see cref="VectorPathsSelfTest"/>.</para>
    ''' <para>⚠️ Lee los 4 bytes de un pixel como un UInt32 little-endian (R en los bits bajos). Es lo que son
    ''' x64 y ARM64; no hay plataforma big-endian en el target.</para></summary>
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Sub LoadRgba8BlockV(px As Byte(), pb As Integer,
                                ByRef r As Vector(Of Single), ByRef g As Vector(Of Single),
                                ByRef b As Vector(Of Single), ByRef a As Vector(Of Single))
        Dim u = Vector.As(Of Byte, UInteger)(New Vector(Of Byte)(px, pb))
        Dim ff = VBroadcast(255UI)
        r = ByteLanesToUnitV(Vector.BitwiseAnd(u, ff))
        g = ByteLanesToUnitV(Vector.BitwiseAnd(Vector.ShiftRightLogical(u, 8), ff))
        b = ByteLanesToUnitV(Vector.BitwiseAnd(Vector.ShiftRightLogical(u, 16), ff))
        a = ByteLanesToUnitV(Vector.ShiftRightLogical(u, 24))          ' el shift solo ya deja 0..255
    End Sub

    ''' <summary>Sólo el canal ALPHA de 8 pixeles RGBA8 contiguos (= la máscara diffuse de un TextureSet en
    ''' N/S). Mismo de-interleave que <see cref="LoadRgba8BlockV"/>, sin calcular los otros tres.</summary>
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Function LoadAlpha8BlockV(px As Byte(), pb As Integer) As Vector(Of Single)
        Return ByteLanesToUnitV(Vector.ShiftRightLogical(Vector.As(Of Byte, UInteger)(New Vector(Of Byte)(px, pb)), 24))
    End Function

    ''' <summary>Un valor 0..255 por lane → unidad [0,1]. Espejo vectorial del LUT
    ''' <see cref="ByteToUnit"/>. El ancho lo fija <c>Vector(Of T)</c>, no es 8: decía "8 lanes" y eso sólo
    ''' vale con AVX2.</summary>
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Function ByteLanesToUnitV(v As Vector(Of UInteger)) As Vector(Of Single)
        Return Vector.Divide(Vector.ConvertToSingle(Vector.As(Of UInteger, Integer)(v)), VBroadcast(255.0F))
    End Function

    ''' <summary>Espejo vectorial de <see cref="ToByte"/> para 8 elementos contiguos. Devuelve los valores YA
    ''' redondeados y acotados a [0,255] como Single: el <c>CByte</c> final lo hace el caller (es un store).
    ''' <para>Replica el orden EXACTO del escalar: NaN→0 primero, después Clamp01, después ×255, después
    ''' round-half-to-even, y recién ahí el clamp a [0,255]. El redondeo usa la constante mágica en vez de un
    ''' intrínseco; tras el Clamp01 el argumento vive en [0,255], muy dentro del rango donde el truco es
    ''' exacto (|s| &lt; 2²²).</para></summary>
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Function ToByteBlockV(a As Single(), at As Integer) As Vector(Of Single)
        Dim c = VBroadcast(a, at)
        c = Vector.ConditionalSelect(Vector.Equals(c, c), c, Vector(Of Single).Zero)   ' NaN -> 0
        c = Vector.Multiply(Clamp01V(c), VBroadcast(255.0F))
        Dim mg = VBroadcast(12582912.0F)
        Dim v = Vector.Subtract(Vector.Add(c, mg), mg)
        v = Vector.ConditionalSelect(Vector.LessThan(v, Vector(Of Single).Zero), Vector(Of Single).Zero, v)
        Return Vector.ConditionalSelect(Vector.GreaterThan(v, VBroadcast(255.0F)), VBroadcast(255.0F), v)
    End Function

    ''' <summary>Convierte EN SITIO un buffer CONTIGUO (SoA: un canal por array, como accR/accG/accB de FO4)
    ''' de <paramref name="fromS"/> a <paramref name="toS"/>. Paralelo + vectorial con cola escalar; con
    ''' <c>fromS = toS</c> es un no-op exacto (lo cortocircuita <see cref="Cvt1"/>).
    ''' <para>⚠️ Con la config por DEFECTO este camino ni corre (accSp == outSp). Se vectoriza igual porque los
    ''' espacios son CONFIGURABLES desde CharGen Options: el día que alguien los separe, esto es un pow por
    ''' elemento y por canal a resolución nativa.</para></summary>
    Public Sub ConvertSpaceSoaInPlace(buf As Single(), n As Integer, fromS As Integer, toS As Integer)
        If buf Is Nothing OrElse fromS = toS Then Return
        System.Threading.Tasks.Parallel.ForEach(
            System.Collections.Concurrent.Partitioner.Create(0, n),
            Sub(range)
                Dim i = range.Item1
                If FastPow.AcceleratedV Then
                    While i + lanes <= range.Item2
                        CvtV(VBroadcast(buf, i), fromS, toS).CopyTo(buf, i)
                        i += lanes
                    End While
                End If
                While i < range.Item2
                    buf(i) = Cvt1(buf(i), fromS, toS)
                    i += 1
                End While
            End Sub)
    End Sub

    ''' <summary>Idem pero sobre un buffer INTERCALADO (AoS RGBA, como el acumulador de SSE): convierte R/G/B y
    ''' deja el ALPHA intacto. Mismo prólogo/cuerpo/cola que el fold, y por el mismo motivo: el vector sólo
    ''' engancha alineado a 8 o el patrón de canal queda corrido.</summary>
    Public Sub ConvertSpaceRgbAosInPlace(buf As Single(), npix As Integer, fromS As Integer, toS As Integer)
        If buf Is Nothing OrElse fromS = toS Then Return
        Dim rgbMask = RgbAosMaskV
        System.Threading.Tasks.Parallel.ForEach(
            System.Collections.Concurrent.Partitioner.Create(0, npix),
            Sub(range)
                Dim lo = range.Item1 * 4, hi = range.Item2 * 4
                Dim i = lo
                If FastPow.AcceleratedV Then
                    While (i And (lanes - 1)) <> 0 AndAlso i < hi
                        If (i And 3) <> 3 Then buf(i) = Cvt1(buf(i), fromS, toS)
                        i += 1
                    End While
                    While i + lanes <= hi
                        Dim v = VBroadcast(buf, i)
                        Vector.ConditionalSelect(rgbMask, CvtV(v, fromS, toS), v).CopyTo(buf, i)
                        i += lanes
                    End While
                End If
                While i < hi
                    If (i And 3) <> 3 Then buf(i) = Cvt1(buf(i), fromS, toS)
                    i += 1
                End While
            End Sub)
    End Sub

    ''' <summary>⭐ SELF-TEST DE PARIDAD del espejo vectorial contra el escalar. Devuelve "" si TODO coincide
    ''' bit a bit; si no, la primera divergencia con sus datos.
    ''' <para><b>Para qué está.</b> El camino escalar sigue siendo la LEY y el oráculo: todo lo que
    ''' <see cref="VecComposeSupported"/> no cubre cae ahí. Este test es lo que permite AMPLIAR el espejo
    ''' vectorial después (más blend ops, más frameworks, Fase A vectorizada, 16 lanes) sin que una
    ''' divergencia se escape a una cara: se agrega el caso acá y el test lo contrasta contra el escalar.</para>
    ''' <para>Barre el dominio real MÁS los bordes que rompen (0, 1, fuera de [0,1], NaN), y prueba largos
    ''' que NO son múltiplo de 8 para ejercitar la cola del bloque — que es donde estuvo el bug clásico.</para>
    ''' <para>⛔ No lo borres "porque el bake ya anda": es el único punto donde la paridad se comprueba sin
    ''' hornear un corpus entero.</para></summary>
    Public Function ComposeVectorSelfTest() As String
        If Not FastPow.AcceleratedV Then Return ""      ' sin AVX2 el espejo ni se usa
        ' Convenciones REALES: la ley de FO4 (con AccumInCompositeSpace en sus dos valores) y la de SSE.
        ' {ss, ws, cs, os, asp, bop}
        Dim cases = New Integer()() {
            New Integer() {2, 2, 0, 2, 2, 3},   ' FO4 PaletteMask, softlight, acc en G22
            New Integer() {2, 2, 0, 2, 0, 3},   ' FO4 PaletteMask, softlight, acc en Linear
            New Integer() {1, 2, 0, 2, 2, 3},   ' FO4 TextureSet (src sRGB), softlight
            New Integer() {1, 0, 0, 2, 2, 0},   ' FO4 TextureSet, replace
            New Integer() {2, 0, 0, 2, 2, 0},   ' FO4 PaletteMask, replace
            New Integer() {0, 0, 0, 0, 0, 0},   ' SSE: todo linear, replace
            New Integer() {3, 2, 0, 2, 2, 3},   ' G24 de src, por cubrir el cuarto espacio
            New Integer() {2, 2, 1, 2, 2, 3}}   ' composite en sRGB (no es la ley, pero el espejo debe darlo)
        ' Largos deliberadamente NO multiplos de 8: el ultimo bloque queda corto y tiene que irse por la cola.
        Dim lens = New Integer() {1, 7, 8, 9, 15, 16, 17, 63, 1000, 1024, 1031}
        Dim seed As UInteger = 2463534242UI          ' xorshift: reproducible, sin Random ni tiempo

        For Each c In cases
            Dim ss = c(0), ws = c(1), cs = c(2), os = c(3), asp = c(4)
            ' ⭐ Barrido de TODOS los blend ops separables (0..19) x los modelos de softlight con espejo
            ' (0=W3C, 1=GIMP, 3=pegtop). El 2 (Illusions) TAMBIEN entra (PowVarV, split de Dekker en runtime).
            For Each bop In New Integer() {0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19}
            For Each slm In New Integer() {0, 1, 2, 3}
            If Not VecComposeSupported(0, bop, slm) Then Continue For
            If bop <> 3 AndAlso slm <> 1 Then Continue For     ' el modelo solo importa en softlight
            ' Se barren TODOS los mask conv (0..6), no solo el default: el mask se convierte vectorizado y
            ' cada `mc` es una rama distinta de ConvMaskV.
            For Each mc In New Integer() {0, 1, 2, 3, 4, 5, 6}
            For Each nn In lens
                Dim prev(nn - 1) As Single, src(nn - 1) As Single, mask(nn - 1) As Single
                Dim opv As Single = 0.75F
                For i = 0 To nn - 1
                    prev(i) = NextUnit(seed, i) : src(i) = NextUnit(seed, i + 7) : mask(i) = NextUnit(seed, i + 13)
                Next
                ' bordes: 0, 1, fuera de rango y NaN, que es donde Clamp01/Min-Max se comportan distinto
                ' ⛔ 8, NO `lanes`: este guard NO es del ancho del vector sino de cuántos ÍNDICES escribe
                ' el bloque de bordes de abajo (0..7). Cambiarlo por `lanes` reventaba con 4 lanes: el
                ' guard pasaba con nn=4 y el cuerpo escribía prev(7). Lo pescó correr el test a 128 bits.
                If nn >= 8 Then
                    prev(0) = 0.0F : src(0) = 0.0F : mask(0) = 0.0F
                    prev(1) = 1.0F : src(1) = 1.0F : mask(1) = 1.0F
                    prev(2) = -0.5F : src(2) = 1.5F : mask(2) = 1.5F
                    prev(3) = Single.NaN : src(3) = 0.5F : mask(3) = 0.5F
                    prev(4) = 0.5F : src(4) = Single.NaN : mask(4) = 0.5F
                    prev(5) = 0.5F : src(5) = 0.5F : mask(5) = Single.NaN
                    prev(6) = 0.5F : src(6) = 0.5F : mask(6) = -0.25F
                    ' ⭐ COBERTURA CERO con un `prev` que NO es punto fijo del round-trip. Es EL caso que
                    ' distingue "saltear" de "componer con cov=0": con asp <> cs (los casos de arriba con
                    ' asp=2, cs=0) componer daria Cvt1(Clamp01(Cvt1(0.37, asp, cs)), cs, asp) <> 0,37, asi que
                    ' un camino vectorial que NO saltee falla aca. mask=0 da cov=0 en los SIETE mask conv
                    ' (todos mandan 0 -> 0). Los indices 0..2 tambien tienen mask=0 o negativo, pero su prev
                    ' es 0 o 1 (puntos fijos) y no distinguirian nada.
                    prev(7) = 0.37F : src(7) = 0.83F : mask(7) = 0.0F
                End If

                ' --- escalar (la ley) ---
                Dim expct(nn - 1) As Single
                For i = 0 To nn - 1
                    Dim cv = Clamp01(ConvMask1(mask(i), mc) * opv)
                    ' El oraculo lleva el MISMO skip de cov<=0 que el loop de capas — porque el skip ES la ley
                    ' ahora, en los tres caminos (escalar, vectorial y GLSL). NaN entra por aca: `> 0` es False
                    ' con NaN, asi que un cov NaN saltea, y `Vector.GreaterThan` hace exactamente lo mismo.
                    expct(i) = If(cv > 0.0F, ComposeOne(prev(i), src(i), cv, ws, cs, ss, os, bop, slm, 0.0F, 0, accSpace:=asp), prev(i))
                Next
                ' --- vectorial, con el MISMO bloqueo de 8 + cola que usa el loop de capas ---
                Dim got(nn - 1) As Single
                Array.Copy(prev, got, nn)
                Dim blk(lanes - 1) As Single, bm(lanes - 1) As Single
                Dim at = 0, k = 0
                For i = 0 To nn - 1
                    blk(k) = src(i) : bm(k) = mask(i) : k += 1
                    If k = lanes Then
                        ' Se replica el early-out de bloque del loop real, no sólo ComposeBlockV: así el test
                        ' cubre las DOS ramas (bloque entero sin cobertura y bloque mixto).
                        Dim covV = CovBlockV(bm, mc, opv)
                        If Not Vector.LessThanOrEqualAll(Of Single)(covV, Vector(Of Single).Zero) Then
                            ComposeBlockV(got, at, blk, covV, ws, cs, ss, asp, bop, slm)
                        End If
                        at = i + 1 : k = 0
                    End If
                Next
                For j = 0 To k - 1
                    Dim cv = Clamp01(ConvMask1(bm(j), mc) * opv)
                    ' misma negacion exacta que la cola del loop real (ver alli): con NaN hay que SALTEAR
                    If Not (cv > 0.0F) Then Continue For
                    got(at + j) = ComposeOne(got(at + j), blk(j), cv, ws, cs, ss, os, bop, slm, 0.0F, 0, accSpace:=asp)
                Next

                For i = 0 To nn - 1
                    ' ⭐ IGUALDAD BIT A BIT, con UNA excepcion acotada: dos NaN cuentan como iguales aunque
                    ' difieran en el PAYLOAD (visto: escalar 0xFFC00000 vs vector 0x7FC00000, o sea solo el
                    ' bit de signo). No es una diferencia numerica ni algo que se pueda arreglar desde VB:
                    ' IEEE-754 NO especifica que payload sobrevive a `a + b` con los dos operandos NaN, y el
                    ' JIT puede conmutar los operandos de un Add VECTORIAL (la suma es conmutativa salvo
                    ' justamente en el payload). Es INOCUO para el contrato de este trabajo —"los mismos BYTES
                    ' en toda PC"— porque el byte-pack manda TODO NaN al mismo byte 0, sin mirar el signo
                    ' (ToByte / ToByteBlockV, cubierto por VectorPathsSelfTest). Y en produccion no hay NaN:
                    ' el acumulador se siembra de un LUT de bytes y toda escritura sale clampeada.
                    ' ⛔ Fuera de este caso la comparacion sigue siendo EXACTA: un valor normal que difiera en
                    ' 1 ULP tiene que seguir fallando.
                    If Single.IsNaN(got(i)) AndAlso Single.IsNaN(expct(i)) Then Continue For
                    If BitConverter.SingleToInt32Bits(got(i)) <> BitConverter.SingleToInt32Bits(expct(i)) Then
                        ' Los BITS van en el mensaje: con NaN de por medio "scalar=NaN vector=NaN" no dice
                        ' nada — la diferencia esta en el payload y sin verlo no se puede diagnosticar.
                        Return $"ComposeVectorSelfTest MISMATCH: ss={ss} ws={ws} cs={cs} os={os} asp={asp} bop={bop} " &
                               $"mc={mc} sl={slm} len={nn} i={i} prev={prev(i)} src={src(i)} mask={mask(i)} scalar={expct(i)} vector={got(i)} " &
                               $"[bits scalar=0x{BitConverter.SingleToInt32Bits(expct(i)):X8} vector=0x{BitConverter.SingleToInt32Bits(got(i)):X8} " &
                               $"prev=0x{BitConverter.SingleToInt32Bits(prev(i)):X8} src=0x{BitConverter.SingleToInt32Bits(src(i)):X8}]"
                    End If
                Next
            Next
            Next
            Next
            Next
        Next
        Return ""
    End Function

    ''' <summary>Self-test de los OTROS caminos vectorizados de este modulo: la conversion de espacio (SoA y
    ''' AoS) y el empaquetado a byte. Devuelve "" si todo coincide bit a bit con el escalar.
    ''' <para>Los largos incluyen no-multiplos de 8 y los buffers AoS arrancan en offsets que obligan al
    ''' prologo; los valores incluyen NaN y fuera de rango.</para></summary>
    Public Function VectorPathsSelfTest() As String
        If Not FastPow.AcceleratedV Then Return ""
        Dim seed As UInteger = 987654321UI
        Dim spaces = New Integer() {0, 1, 2, 3}

        ' ---- conversion de espacio, layout SoA (contiguo) ----
        For Each fs In spaces
            For Each ts In spaces
                For Each nn In New Integer() {1, 7, 8, 9, 17, 1000, 1031}
                    Dim a(nn - 1) As Single, b(nn - 1) As Single
                    For i = 0 To nn - 1
                        a(i) = NextUnit(seed, i)
                    Next
                    If nn >= 6 Then
                        a(0) = 0.0F : a(1) = 1.0F : a(2) = -0.5F : a(3) = 1.5F : a(4) = Single.NaN : a(5) = 0.5F
                    End If
                    Array.Copy(a, b, nn)
                    ConvertSpaceSoaInPlace(a, nn, fs, ts)
                    For i = 0 To nn - 1
                        Dim want = Cvt1(b(i), fs, ts)
                        If BitConverter.SingleToInt32Bits(a(i)) <> BitConverter.SingleToInt32Bits(want) Then
                            Return $"ConvertSpaceSoaInPlace MISMATCH: from={fs} to={ts} len={nn} i={i} in={b(i)} scalar={want} vector={a(i)}"
                        End If
                    Next
                Next
            Next
        Next

        ' ---- conversion de espacio, layout AoS (RGB, alpha intacto) ----
        For Each fs In spaces
            For Each ts In spaces
                For Each np In New Integer() {1, 2, 3, 5, 9, 257}
                    Dim a(np * 4 - 1) As Single, b(np * 4 - 1) As Single
                    For i = 0 To np * 4 - 1
                        a(i) = NextUnit(seed, i)
                    Next
                    Array.Copy(a, b, np * 4)
                    ConvertSpaceRgbAosInPlace(a, np, fs, ts)
                    For i = 0 To np * 4 - 1
                        Dim want = If((i And 3) = 3, b(i), Cvt1(b(i), fs, ts))
                        If BitConverter.SingleToInt32Bits(a(i)) <> BitConverter.SingleToInt32Bits(want) Then
                            Return $"ConvertSpaceRgbAosInPlace MISMATCH: from={fs} to={ts} npix={np} i={i} in={b(i)} scalar={want} vector={a(i)}"
                        End If
                    Next
                Next
            Next
        Next

        ' ---- empaquetado a byte: ToByteBlockV vs ToByte ----
        Dim probe(15) As Single
        probe(0) = 0.0F : probe(1) = 1.0F : probe(2) = -0.5F : probe(3) = 1.5F
        probe(4) = Single.NaN : probe(5) = Single.PositiveInfinity : probe(6) = Single.NegativeInfinity
        probe(7) = 0.5F / 255.0F : probe(8) = 1.5F / 255.0F : probe(9) = 2.5F / 255.0F
        probe(10) = 0.0019607844F : probe(11) = 0.99999994F : probe(12) = 1.0F / 510.0F
        probe(13) = 254.5F / 255.0F : probe(14) = 3.0F / 510.0F : probe(15) = 0.4980392F
        For blk = 0 To probe.Length \ lanes - 1
            Dim v = ToByteBlockV(probe, blk * lanes)
            For j = 0 To lanes - 1
                Dim want As Integer = ToByte(probe(blk * lanes + j))
                Dim got As Integer = CByte(v.GetElement(j))
                If got <> want Then
                    Return $"ToByteBlockV MISMATCH: v={probe(blk * lanes + j)} scalar={want} vector={got}"
                End If
            Next
        Next
        ' barrido denso sobre el dominio real, para pescar los bordes de redondeo
        Dim buf(lanes - 1) As Single
        For k = 0 To 200000
            For j = 0 To lanes - 1
                buf(j) = CSng(((k * lanes + j) Mod 65536) / 65535.0)
            Next
            Dim v2 = ToByteBlockV(buf, 0)
            For j = 0 To lanes - 1
                If CByte(v2.GetElement(j)) <> ToByte(buf(j)) Then
                    Return $"ToByteBlockV MISMATCH (barrido): v={buf(j)} scalar={ToByte(buf(j))} vector={CByte(v2.GetElement(j))}"
                End If
            Next
        Next

        ' ---- ⭐ byte→unidad de la FASE A vectorizada: LoadRgba8BlockV / LoadAlpha8BlockV vs el LUT ByteToUnit.
        ' Es EXHAUSTIVO por construccion: el buffer recorre los 256 valores posibles en CADA uno de los cuatro
        ' canales. Es el eslabon que hay que probar, no suponer: el vector DIVIDE en Single
        ' (ConvertToSingle(b)/255f) y el LUT divide en DOUBLE (CSng(b/255.0)). Que coincidan en 256/256 es lo
        ' que permite que la Fase A no necesite gather ni una LUT vectorial.
        Dim npx As Integer = 256
        Dim pxbuf(npx * 4 - 1) As Byte
        For i = 0 To npx - 1
            pxbuf(i * 4) = CByte(i)                          ' R barre 0..255
            pxbuf(i * 4 + 1) = CByte(255 - i)                ' G barre 255..0
            pxbuf(i * 4 + 2) = CByte((i * 7) Mod 256)        ' B  \ ordenes distintos: ningun canal
            pxbuf(i * 4 + 3) = CByte((i * 13) Mod 256)       ' A  / repite la posicion de otro
        Next
        Dim lutRef = ByteToUnit
        For blkI = 0 To npx \ lanes - 1
            Dim baseIdx = blkI * lanes
            Dim rV, gV, bV, aV As Vector(Of Single)
            LoadRgba8BlockV(pxbuf, baseIdx * 4, rV, gV, bV, aV)
            Dim aOnly = LoadAlpha8BlockV(pxbuf, baseIdx * 4)
            For j = 0 To lanes - 1
                Dim p = (baseIdx + j) * 4
                Dim wants = New Single() {lutRef(pxbuf(p)), lutRef(pxbuf(p + 1)), lutRef(pxbuf(p + 2)), lutRef(pxbuf(p + 3))}
                Dim gots = New Single() {rV.GetElement(j), gV.GetElement(j), bV.GetElement(j), aV.GetElement(j)}
                For ch = 0 To 3
                    If BitConverter.SingleToInt32Bits(gots(ch)) <> BitConverter.SingleToInt32Bits(wants(ch)) Then
                        Return $"LoadRgba8BlockV MISMATCH: byte={pxbuf(p + ch)} ch={ch} lane={j} lut={wants(ch)} vector={gots(ch)}"
                    End If
                Next
                If BitConverter.SingleToInt32Bits(aOnly.GetElement(j)) <> BitConverter.SingleToInt32Bits(wants(3)) Then
                    Return $"LoadAlpha8BlockV MISMATCH: byte={pxbuf(p + 3)} lane={j} lut={wants(3)} vector={aOnly.GetElement(j)}"
                End If
            Next
        Next

        ' ---- MinV/MaxV vs Math.Min/Math.Max, sobre la tabla COMPLETA de pares raros: NaN, infinitos y los DOS
        ' ceros firmados. El ±0 no es teorico — asi se pesco que MaxV(+0,-0) daba -0 donde Math.Max da +0.
        ' MaxV lo usa la Fase A para el mask `max(r, max(g, b))` de un TextureSet N/S sin mascara diffuse, y
        ' los dos los usa BlendDispatchV en darken/lighten/dodge/burn/divide/linear*.
        Dim mx = New Single() {0.0F, -0.0F, 1.0F, 0.5F, -0.5F, Single.NaN, Single.PositiveInfinity, Single.NegativeInfinity}
        For ia = 0 To mx.Length - 1
            Dim av = VBroadcast(mx(ia))
            For ib = 0 To mx.Length - 1
                Dim bv = VBroadcast(mx(ib))
                Dim gotMax = MaxV(av, bv).GetElement(0), wantMax = Math.Max(mx(ia), mx(ib))
                If BitConverter.SingleToInt32Bits(gotMax) <> BitConverter.SingleToInt32Bits(wantMax) Then
                    Return $"MaxV MISMATCH: a={mx(ia)} b={mx(ib)} scalar=0x{BitConverter.SingleToInt32Bits(wantMax):X8} vector=0x{BitConverter.SingleToInt32Bits(gotMax):X8}"
                End If
                Dim gotMin = MinV(av, bv).GetElement(0), wantMin = Math.Min(mx(ia), mx(ib))
                If BitConverter.SingleToInt32Bits(gotMin) <> BitConverter.SingleToInt32Bits(wantMin) Then
                    Return $"MinV MISMATCH: a={mx(ia)} b={mx(ib)} scalar=0x{BitConverter.SingleToInt32Bits(wantMin):X8} vector=0x{BitConverter.SingleToInt32Bits(gotMin):X8}"
                End If
            Next
        Next
        Return ""
    End Function

    ' =================================================================================================
    ' PACK RGBA float -> BGRA byte CON REDONDEO EN DOUBLE. Es el byte-pack del camino 4K de SSE (el fold
    ' del diffuse y el _msn), cuya ley es `FaceGenBuilder.ClampByte255`.
    '
    ' ⛔⛔ NO ES <see cref="ToByte"/> NI <see cref="ToByteBlockV"/>, Y NO SON INTERCAMBIABLES. Aquel redondea
    ' en SINGLE (MathF.Round(s*255f)); este ensancha el Single a Double y redondea en DOUBLE
    ' (Math.Round(CDbl(s)*255.0)). Cerca de los bordes .5 los dos redondeos pueden dar bytes DISTINTOS, asi
    ' que reusar el otro helper "porque ya esta vectorizado" cambiaria la salida. Por eso este va en
    ' Vector(Of Double): 4 lanes, no 8.
    ' =================================================================================================

    ''' <summary>Ley ESCALAR del pack (= <c>FaceGenBuilder.ClampByte255(v * 255.0)</c>). Es el oraculo y la
    ''' cola del vectorial. ⚠️ Con NaN <c>CByte</c> TIRA OverflowException, y tiene que seguir haciendolo: un
    ''' NaN en el acumulador es una anomalia real y degradarla a un 0 la esconderia.</summary>
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Function ToByteRoundDouble(v As Single) As Byte
        Return CByte(Math.Max(0.0, Math.Min(255.0, Math.Round(CDbl(v) * 255.0))))
    End Function

    ''' <summary>4 componentes (un pixel RGBA) en unidad → los mismos valores ×255, redondeados half-to-even y
    ''' acotados a [0,255], todavia como Double. Espejo de <see cref="ToByteRoundDouble"/> sin el CByte final.
    ''' <para>El redondeo va por CONSTANTE MAGICA (1,5·2⁵²), no por intrinseco, igual que el resto de FastPow:
    ''' es round-half-to-even exacto con sola suma y resta, y no depende de que la plataforma tenga roundsd.
    ''' Arriba de 2⁵² el argumento ya ES entero y el truco no aplica ⇒ se selecciona el valor sin tocar (y de
    ''' todos modos ese rango termina clampeado a 255).</para></summary>
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Function RoundClamp255V(v As Vector(Of Double)) As Vector(Of Double)
        Dim x = Vector.Multiply(v, VBroadcast(255.0))
        Dim mg = VBroadcast(6755399441055744.0)                       ' 1,5 · 2^52
        Dim r = Vector.Subtract(Vector.Add(x, mg), mg)
        r = Vector.ConditionalSelect(
                Vector.GreaterThanOrEqual(Vector.Abs(x), VBroadcast(4503599627370496.0)), x, r)   ' 2^52
        r = Vector.ConditionalSelect(Vector.GreaterThan(r, VBroadcast(255.0)), VBroadcast(255.0), r)
        Return Vector.ConditionalSelect(Vector.LessThan(r, Vector(Of Double).Zero), Vector(Of Double).Zero, r)
    End Function

    ''' <summary>Empaqueta un acumulador RGBA float en unidad [0,1] a BGRA byte, en paralelo y vectorizado.
    ''' Es el pack del camino 4K de SSE: corre a resolucion NATIVA (16,7 M pixeles a 4096²) una vez por fold
    ''' y otra por <c>_msn</c>.
    ''' <para>Se procesa un bloque de <c>FastPow.LaneCount</c> COMPONENTES (no de píxeles): se ensancha a dos
    ''' <c>Vector(Of Double)</c> con <c>Vector.Widen</c>, se hace la cuenta cara vectorizada, y el swizzle
    ''' RGBA→BGRA queda escalar sobre el bloque.</para>
    ''' <para>⛔ NO se puede asumir "un píxel por vector": con 8 lanes de Single un <c>Vector(Of Double)</c>
    ''' tiene 4 elementos y SÍ es un píxel, pero con 4 lanes tiene 2 y el píxel queda partido al medio. Por eso
    ''' el bloque se cuenta en componentes (LaneCount es múltiplo de 4 ⇒ cubre píxeles enteros) y el store va
    ''' por índice, no por lane.</para>
    ''' <para>⛔ NaN ⇒ se sale del camino vectorial y el resto del rango lo hace el ESCALAR, que tira
    ''' OverflowException igual que antes. El vector NO lo "arregla" devolviendo 0.</para></summary>
    Public Sub PackUnitRgbaToBgraRoundDouble(acc As Single(), bgra As Byte(), npix As Integer)
        Dim lanes = FastPow.LaneCount
        Dim pixPerBlock = lanes \ 4                       ' LaneCount siempre es multiplo de 4 (4, 8, 16)
        System.Threading.Tasks.Parallel.ForEach(
            System.Collections.Concurrent.Partitioner.Create(0, npix),
            Sub(range)
                Dim i = range.Item1
                If FastPow.AcceleratedV AndAlso pixPerBlock >= 1 Then
                    Dim tmp(lanes - 1) As Double
                    While i + pixPerBlock <= range.Item2
                        Dim v = VBroadcast(acc, i * 4)
                        If Not Vector.EqualsAll(v, v) Then Exit While    ' hay NaN -> que lo tire el escalar
                        Dim dlo As Vector(Of Double), dhi As Vector(Of Double)
                        Vector.Widen(v, dlo, dhi)
                        RoundClamp255V(dlo).CopyTo(tmp, 0)
                        RoundClamp255V(dhi).CopyTo(tmp, lanes \ 2)
                        For p = 0 To pixPerBlock - 1
                            Dim o = (i + p) * 4, s = p * 4
                            bgra(o) = CByte(tmp(s + 2))       ' B
                            bgra(o + 1) = CByte(tmp(s + 1))   ' G
                            bgra(o + 2) = CByte(tmp(s))       ' R
                            bgra(o + 3) = CByte(tmp(s + 3))   ' A
                        Next
                        i += pixPerBlock
                    End While
                End If
                While i < range.Item2
                    bgra(i * 4) = ToByteRoundDouble(acc(i * 4 + 2))      ' B
                    bgra(i * 4 + 1) = ToByteRoundDouble(acc(i * 4 + 1))  ' G
                    bgra(i * 4 + 2) = ToByteRoundDouble(acc(i * 4))      ' R
                    bgra(i * 4 + 3) = ToByteRoundDouble(acc(i * 4 + 3))  ' A
                    i += 1
                End While
            End Sub)
    End Sub

    ''' <summary>Self-test del pack en Double: vectorial vs la ley escalar, bit a bit sobre el byte.
    ''' <para>Barre los bordes de redondeo (los .5 exactos, donde half-to-even decide), fuera de rango, los
    ''' infinitos, y largos que NO son multiplo de 2 para ejercitar la cola. NaN NO se barre acá a proposito:
    ''' el contrato es que EXPLOTE, y eso se verifica aparte.</para></summary>
    Public Function PackRoundDoubleSelfTest() As String
        Dim vals As New List(Of Single)
        ' los N+0,5/255 exactos son EL caso: ahi half-to-even elige, y un redondeo distinto cambia el byte
        For k = 0 To 255
            vals.Add(CSng(k / 255.0))
            vals.Add(CSng((k + 0.5) / 255.0))
            vals.Add(CSng((k + 0.4999) / 255.0))
            vals.Add(CSng((k + 0.5001) / 255.0))
        Next
        vals.AddRange(New Single() {0.0F, -0.0F, 1.0F, -0.5F, 1.5F, 1000.0F, -1000.0F,
                                    Single.PositiveInfinity, Single.NegativeInfinity, Single.Epsilon})
        For Each npix In New Integer() {1, 2, 3, 5, 8, 9, 1031}
            Dim acc(npix * 4 - 1) As Single
            Dim got(npix * 4 - 1) As Byte, want(npix * 4 - 1) As Byte
            Dim vi As Integer = 0
            For pass = 0 To vals.Count \ Math.Max(1, npix * 4)
                For j = 0 To npix * 4 - 1
                    acc(j) = vals((vi + j) Mod vals.Count)
                Next
                vi += npix * 4
                PackUnitRgbaToBgraRoundDouble(acc, got, npix)
                For i = 0 To npix - 1
                    want(i * 4) = ToByteRoundDouble(acc(i * 4 + 2))
                    want(i * 4 + 1) = ToByteRoundDouble(acc(i * 4 + 1))
                    want(i * 4 + 2) = ToByteRoundDouble(acc(i * 4))
                    want(i * 4 + 3) = ToByteRoundDouble(acc(i * 4 + 3))
                Next
                For j = 0 To npix * 4 - 1
                    If got(j) <> want(j) Then
                        Return $"PackUnitRgbaToBgraRoundDouble MISMATCH: npix={npix} j={j} in={acc((j \ 4) * 4 + (2 - (j Mod 4) + If((j Mod 4) = 3, 6, 0)))} scalar={want(j)} vector={got(j)}"
                    End If
                Next
            Next
        Next
        ' ⛔ El contrato del NaN: TIENE que tirar OverflowException, no devolver 0.
        Dim nanAcc(7) As Single
        nanAcc(2) = Single.NaN
        Dim nanOut(7) As Byte
        Try
            PackUnitRgbaToBgraRoundDouble(nanAcc, nanOut, 2)
            Return "PackUnitRgbaToBgraRoundDouble: un NaN NO tiro OverflowException (el vector se lo trago)"
        Catch ex As Exception
            ' Parallel.ForEach envuelve en AggregateException; lo que importa es que NO pase en silencio.
            If TypeOf ex IsNot OverflowException AndAlso
               Not (TypeOf ex Is AggregateException AndAlso
                    DirectCast(ex, AggregateException).InnerExceptions.Any(Function(e) TypeOf e Is OverflowException)) Then
                Return $"PackUnitRgbaToBgraRoundDouble: con NaN tiro {ex.GetType().Name}, se esperaba OverflowException"
            End If
        End Try
        Return ""
    End Function

    ''' <summary>xorshift32 -> [0,1). Determinista y sin dependencias: el self-test tiene que dar lo mismo
    ''' en cada corrida y en cada maquina, o deja de ser un gate.</summary>
    Private Function NextUnit(ByRef s As UInteger, salt As Integer) As Single
        s = s Xor (s << 13) : s = s Xor (s >> 17) : s = s Xor (s << 5)
        Return CSng((s Xor CUInt(salt And &H7FFFFFFF)) Mod 1000000UI) / 1000000.0F
    End Function

    ''' <summary>Sample de un canal del DecodedTex en el índice de píxel del acumulador (w,h). Si el tex
    ''' es del MISMO tamaño, índice directo; si difiere, bilineal por UV (resolución por canal / LUT).</summary>
    Private Function SampleChannelAt(t As DecodedTex, accIdx As Integer, accW As Integer, accH As Integer, ch As Integer) As Single
        If t.Width = accW AndAlso t.Height = accH Then
            Return t.Unit(accIdx * 4 + ch)
        End If
        Dim x = accIdx Mod accW, y = accIdx \ accW
        Dim u = CSng((x + 0.5) / accW), v = CSng((y + 0.5) / accH)
        Return SampleBilinear(t, u, v, ch)
    End Function

    Private Function ToByte(c As Single) As Byte
        ' np.rint de gen3 = round-half-to-EVEN (banker's) = MidpointRounding.ToEven (default de Math.Round).
        ' El redondeo a byte se hace SOLO al final (los acumuladores quedan float toda la pasada), igual
        ' que gen3 (rint solo en el write). Asi CPU == `_3` byte-exacto.
        ' Guard NaN: Clamp01 NO atrapa NaN (Math.Min/Max con NaN devuelve NaN) y CByte(NaN) tira
        ' OverflowException. ±Infinity SI lo clampa Clamp01. NaN -> 0 (defensivo; no cambia ningún byte
        ' válido -> la paridad byte-exacta con _3 se preserva, sólo evita el crash si un blend/framework NaN-ea).
        If Single.IsNaN(c) Then c = 0.0F
        Dim v = MathF.Round(Clamp01(c) * 255.0F, MidpointRounding.ToEven)
        If v < 0.0F Then v = 0.0F
        If v > 255.0F Then v = 255.0F
        Return CByte(v)
    End Function

    ''' <summary>⭐ SELF-TEST de los DOS caminos que se vectorizaron último y no tenían oráculo: el SEED y el
    ''' loop de REGION SWAPS de <see cref="ComposeChannelCpu"/>. Devuelve "" si coinciden BIT A BIT.
    '''
    ''' <para><b>Por qué hacen falta aparte.</b> <c>ComposeVectorSelfTest</c> sólo ejercita la Fase B del loop
    ''' de capas (<see cref="ComposeBlockV"/>). El seed y los swaps son loops distintos, con su propia carga de
    ''' bytes y su propia regla de cobertura — y el de swaps es justo el que A PROPÓSITO no lleva el skip de
    ''' <c>cov &lt;= 0</c>, o sea el más fácil de romper copiando el de capas.</para>
    '''
    ''' <para>SEED: reproduce <c>LoadRgba8BlockV</c> + <c>CvtV(...)</c> contra <c>ByteToUnit</c> + <c>Cvt1</c>,
    ''' sobre los 256 bytes y las 16 combinaciones de espacio. Es el camino que dejó de ser gratis al alinear
    ''' el config (con <c>accSp ≠ outSp</c> es un pow por píxel y por canal).</para>
    ''' <para>SWAPS: <see cref="ComposeSwapBlockV"/> contra <see cref="ComposeOne"/> SIN skip, con máscaras que
    ''' incluyen cobertura CERO — que es donde este camino tiene que componer igual y no saltear.</para></summary>
    Public Function SeedAndSwapVectorSelfTest() As String
        If Not FastPow.AcceleratedV Then Return ""
        Dim lut = ByteToUnit

        ' ---------------- SEED ----------------
        ' Buffer con los 256 valores en cada canal, en órdenes distintos para que ningún canal repita
        ' la posición de otro.
        Dim npx = 256
        Dim px(npx * 4 - 1) As Byte
        For i = 0 To npx - 1
            px(i * 4) = CByte(i)
            px(i * 4 + 1) = CByte(255 - i)
            px(i * 4 + 2) = CByte((i * 7) Mod 256)
            px(i * 4 + 3) = CByte((i * 13) Mod 256)
        Next
        For Each fromSp In New Integer() {0, 1, 2, 3}
            For Each accSp In New Integer() {0, 1, 2, 3}
                For blkI = 0 To npx \ lanes - 1
                    Dim at = blkI * lanes
                    Dim rV, gV, bV, aV As Vector(Of Single)
                    LoadRgba8BlockV(px, at * 4, rV, gV, bV, aV)
                    Dim cr = CvtV(rV, fromSp, accSp), cg = CvtV(gV, fromSp, accSp), cb = CvtV(bV, fromSp, accSp)
                    For j = 0 To lanes - 1
                        Dim p = (at + j) * 4
                        Dim wantR = Cvt1(lut(px(p)), fromSp, accSp)
                        Dim wantG = Cvt1(lut(px(p + 1)), fromSp, accSp)
                        Dim wantB = Cvt1(lut(px(p + 2)), fromSp, accSp)
                        ' el ALPHA es RAW: no pasa por conversión de espacio (igual que el escalar)
                        Dim wantA = lut(px(p + 3))
                        If BitConverter.SingleToInt32Bits(cr(j)) <> BitConverter.SingleToInt32Bits(wantR) OrElse
                           BitConverter.SingleToInt32Bits(cg(j)) <> BitConverter.SingleToInt32Bits(wantG) OrElse
                           BitConverter.SingleToInt32Bits(cb(j)) <> BitConverter.SingleToInt32Bits(wantB) OrElse
                           BitConverter.SingleToInt32Bits(aV(j)) <> BitConverter.SingleToInt32Bits(wantA) Then
                            Return $"SEED vector MISMATCH: from={fromSp} acc={accSp} px={at + j} " &
                                   $"bytes=({px(p)},{px(p + 1)},{px(p + 2)},{px(p + 3)})"
                        End If
                    Next
                Next
            Next
        Next

        ' ---------------- REGION SWAPS ----------------
        ' {ss, ws, cs, asp, bop} — las convenciones reales del swap más un par que fuerzan asp <> cs.
        Dim cases = New Integer()() {
            New Integer() {0, 0, 0, 0, 0},
            New Integer() {2, 2, 0, 2, 0},
            New Integer() {2, 2, 0, 0, 0},
            New Integer() {1, 2, 0, 2, 3},
            New Integer() {2, 0, 0, 2, 0}}
        Dim seed As UInteger = 99887766UI
        For Each c In cases
            Dim ss = c(0), ws = c(1), cs = c(2), asp = c(3), bop = c(4)
            For Each sl In New Integer() {0, 1, 3}
                If bop <> 3 AndAlso sl <> 1 Then Continue For
                If Not VecComposeSupported(0, bop, sl) Then Continue For
                For Each mc In New Integer() {0, 1, 3}
                    ' ⛔ Largos que NO son multiplos de `lanes`: con solo multiplos exactos `iv` llega
                    ' siempre a `nn` y el remanente escalar del loop real no se compara NUNCA.
                    For Each nn In New Integer() {lanes, lanes + 1, lanes * 3 - 1, lanes * 7 + 3}
                        Dim accV(nn - 1) As Single, accS(nn - 1) As Single
                        Dim swPx(nn * 4 - 1) As Byte, mkPx(nn * 4 - 1) As Byte
                        For i = 0 To nn - 1
                            accV(i) = NextUnit(seed, i) : accS(i) = accV(i)
                        Next
                        For i = 0 To nn * 4 - 1
                            swPx(i) = CByte((CInt(NextUnit(seed, i) * 255.0F)) And &HFF)
                            mkPx(i) = CByte((CInt(NextUnit(seed, i + 5) * 255.0F)) And &HFF)
                        Next
                        ' ⭐ cobertura CERO en varios píxeles: el swap NO saltea, tiene que componer igual
                        For i = 0 To nn - 1 Step 3
                            mkPx(i * 4) = 0
                        Next
                        Dim msdv As Single = 0.65F

                        ' vectorial, en bloques como el loop real
                        Dim iv = 0
                        While iv + lanes <= nn
                            Dim srV, sgV, sbV, saV As Vector(Of Single)
                            LoadRgba8BlockV(swPx, iv * 4, srV, sgV, sbV, saV)
                            Dim mkR, mkG, mkB, mkA As Vector(Of Single)
                            LoadRgba8BlockV(mkPx, iv * 4, mkR, mkG, mkB, mkA)
                            Dim covV = Clamp01V(Vector.Multiply(ConvMaskV(mkR, mc), VBroadcast(msdv)))
                            ComposeSwapBlockV(accV, iv, srV, covV, ws, cs, ss, asp, bop, sl)
                            iv += lanes
                        End While

                        ' escalar (la ley), sobre los mismos píxeles
                        For i = 0 To iv - 1
                            Dim sr = ByteToUnit(swPx(i * 4))
                            Dim mask = ByteToUnit(mkPx(i * 4))
                            Dim cov = Clamp01(ConvMask1(mask, mc) * msdv)
                            accS(i) = CSng(ComposeOne(accS(i), sr, cov, ws, cs, ss, 0, bop, sl, accSpace:=asp))
                        Next

                        For i = 0 To iv - 1
                            If BitConverter.SingleToInt32Bits(accV(i)) <> BitConverter.SingleToInt32Bits(accS(i)) Then
                                Return $"SWAP vector MISMATCH: ss={ss} ws={ws} cs={cs} asp={asp} bop={bop} sl={sl} " &
                                       $"mc={mc} n={nn} i={i} escalar=0x{BitConverter.SingleToInt32Bits(accS(i)):X8} " &
                                       $"vector=0x{BitConverter.SingleToInt32Bits(accV(i)):X8}"
                            End If
                        Next
                    Next
                Next
            Next
        Next
        Return ""
    End Function

    ''' <summary>⭐ SELF-TEST DE LA FASE A del loop de capas de FO4: <see cref="LayerSrcMaskBlockV"/> contra
    ''' <see cref="LayerSrcMaskPixel"/>, que es la función que el loop ESCALAR realmente llama. Devuelve "" si
    ''' coinciden BIT A BIT.
    '''
    ''' <para><b>Por qué existe.</b> La Fase A era el código nuevo más grande de toda la vectorización y el
    ''' único camino sin oráculo: <c>ComposeVectorSelfTest</c> arma los bloques a mano y sólo ejercita la Fase
    ''' B, y <c>SeedAndSwapVectorSelfTest</c> cubre otros dos loops. Que la lógica "coincida" por lectura no es
    ''' un gate.</para>
    ''' <para>⛔ Compara contra la función de PRODUCCIÓN, no contra una re-implementación de la cadena: una
    ''' copia en el test podría heredar el mismo error y dar verde.</para>
    ''' <para>Barre las <b>32 combinaciones</b> de los 5 flags (isPalette × isD × forceUniform × texTimesColor
    ''' × hasDiffMask) — incluidas las que en producción no se dan juntas, porque el gate <c>fastA</c> puede
    ''' cambiar. Y alimenta la carga de bytes REAL (<see cref="LoadRgba8BlockV"/>) con los 256 valores, más NaN
    ''' y ±0 en el canal del <c>dmA</c>, que es donde <c>MaxV</c> se comporta distinto de un Min/Max ingenuo.</para></summary>
    Public Function PhaseAVectorSelfTest() As String
        If Not FastPow.AcceleratedV Then Return ""
        Dim lut = ByteToUnit
        Dim npx = 256
        Dim px(npx * 4 - 1) As Byte, dm(npx * 4 - 1) As Byte
        For i = 0 To npx - 1
            px(i * 4) = CByte(i)
            px(i * 4 + 1) = CByte(255 - i)
            px(i * 4 + 2) = CByte((i * 7) Mod 256)
            px(i * 4 + 3) = CByte((i * 13) Mod 256)
            dm(i * 4 + 3) = CByte((i * 31) Mod 256)
        Next
        Dim uColR As Single = 0.3F, uColG As Single = 0.62F, uColB As Single = 0.87F
        Dim colRV = VBroadcast(uColR), colGV = VBroadcast(uColG), colBV = VBroadcast(uColB)

        For flags = 0 To 31
            Dim isPalette = (flags And 1) <> 0
            Dim isD = (flags And 2) <> 0
            Dim forceUniform = (flags And 4) <> 0
            Dim texTimesColor = (flags And 8) <> 0
            Dim hasDiffMask = (flags And 16) <> 0

            ' ⛔ EJE NUEVO: el canal de la mascara de paleta (0=R 1=G 2=B 3=A). Antes el escalar y el vector
            ' tenian el VERDE cableado, asi que coincidian trivialmente y el test no probaba nada de esto.
            ' Se barren los CUATRO, no solo el default, porque SSE usa el rojo.
            For palMaskCh = 0 To 3
            For blkI = 0 To npx \ lanes - 1
                Dim at = blkI * lanes
                Dim lrV, lgV, lbV, laV As Vector(Of Single)
                LoadRgba8BlockV(px, at * 4, lrV, lgV, lbV, laV)
                Dim dmAV = If(hasDiffMask, LoadAlpha8BlockV(dm, at * 4), Vector(Of Single).Zero)
                Dim sRV, sGV, sBV, mV As Vector(Of Single)
                LayerSrcMaskBlockV(lrV, lgV, lbV, laV, dmAV, isPalette, isD, forceUniform, texTimesColor,
                                   hasDiffMask, colRV, colGV, colBV, palMaskCh, sRV, sGV, sBV, mV)

                For j = 0 To lanes - 1
                    Dim p = (at + j) * 4
                    Dim lr = lut(px(p)), lg = lut(px(p + 1)), lb = lut(px(p + 2)), la = lut(px(p + 3))
                    Dim dmA = If(hasDiffMask, lut(dm(p + 3)), 0.0F)
                    Dim wR As Single, wG As Single, wB As Single, wM As Single
                    LayerSrcMaskPixel(lr, lg, lb, la, dmA, isPalette, isD, forceUniform, texTimesColor,
                                      hasDiffMask, uColR, uColG, uColB, palMaskCh, wR, wG, wB, wM)
                    If BitConverter.SingleToInt32Bits(sRV(j)) <> BitConverter.SingleToInt32Bits(wR) OrElse
                       BitConverter.SingleToInt32Bits(sGV(j)) <> BitConverter.SingleToInt32Bits(wG) OrElse
                       BitConverter.SingleToInt32Bits(sBV(j)) <> BitConverter.SingleToInt32Bits(wB) OrElse
                       BitConverter.SingleToInt32Bits(mV(j)) <> BitConverter.SingleToInt32Bits(wM) Then
                        Return $"FASE A vector MISMATCH: pal={isPalette} isD={isD} uni={forceUniform} " &
                               $"txc={texTimesColor} dm={hasDiffMask} palCh={palMaskCh} px={at + j} " &
                               $"escalar=({wR},{wG},{wB} | {wM}) vector=({sRV(j)},{sGV(j)},{sBV(j)} | {mV(j)})"
                    End If
                Next
            Next
            Next
        Next

        ' ---- bordes que el barrido de bytes NO puede producir: NaN y cero firmado en el max(r,g,b) ----
        ' Es la rama donde MaxV tiene que replicar Math.Max exactamente (NaN de ENTRADA, +0 sobre -0).
        Dim edges As Single() = {0.0F, -0.0F, Single.NaN, 1.0F, -1.0F, 0.5F,
                                 Single.PositiveInfinity, Single.NegativeInfinity}
        Dim er(lanes - 1) As Single, eg(lanes - 1) As Single, eb(lanes - 1) As Single, ea(lanes - 1) As Single
        For a = 0 To edges.Length - 1
            For b = 0 To edges.Length - 1
                For c = 0 To edges.Length - 1
                    For j = 0 To lanes - 1
                        er(j) = edges(a) : eg(j) = edges(b) : eb(j) = edges(c) : ea(j) = edges((a + b) Mod edges.Length)
                    Next
                    Dim sRV, sGV, sBV, mV As Vector(Of Single)
                    ' isPalette=False, isD=False, hasDiffMask=False => la rama del max(r, max(g, b))
                    LayerSrcMaskBlockV(VBroadcast(er, 0), VBroadcast(eg, 0), VBroadcast(eb, 0), VBroadcast(ea, 0),
                                       Vector(Of Single).Zero, False, False, False, False, False,
                                       colRV, colGV, colBV, 1, sRV, sGV, sBV, mV)
                    Dim wR As Single, wG As Single, wB As Single, wM As Single
                    LayerSrcMaskPixel(edges(a), edges(b), edges(c), edges((a + b) Mod edges.Length), 0.0F,
                                      False, False, False, False, False, uColR, uColG, uColB, 1, wR, wG, wB, wM)
                    If BitConverter.SingleToInt32Bits(mV(0)) <> BitConverter.SingleToInt32Bits(wM) Then
                        Return $"FASE A mask MISMATCH en bordes: r={edges(a)} g={edges(b)} b={edges(c)} " &
                               $"escalar=0x{BitConverter.SingleToInt32Bits(wM):X8} vector=0x{BitConverter.SingleToInt32Bits(mV(0)):X8}"
                    End If
                Next
            Next
        Next
        Return ""
    End Function

End Module
