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

    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Function SrgbToLin1(c As Single) As Single
        c = Clamp01(c)
        Return If(c <= 0.04045F, c / 12.92F, FastPow.Pow1((c + 0.055F) / 1.055F, FastPow.G24))
    End Function

    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Function LinToSrgb1(c As Single) As Single
        c = Clamp01(c)
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
            Case 3 ' pegtop
                Return (1.0F - 2.0F * s) * d * d + 2.0F * s * d
            Case Else ' 0 = W3C SVG
                Dim g As Single = If(d >= 0.25F, MathF.Sqrt(d), ((16.0F * d - 12.0F) * d + 4.0F) * d)
                If s >= 0.5F Then Return d + (2.0F * s - 1.0F) * (g - d)
                Return d - (1.0F - 2.0F * s) * d * (1.0F - d)
        End Select
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

    ''' <summary>Sample bilineal de un canal (0=R 1=G 2=B 3=A) en coord normalizada (u,v) [0,1], clamp
    ''' a borde. Para la hair-LUT (= GL_LINEAR del sampler uHairLut). Mismo filtro que el shader.</summary>
    Private Function SampleBilinear(t As DecodedTex, u As Single, v As Single, ch As Integer) As Single
        Dim w = t.Width, h = t.Height
        ' Convencion GL_LINEAR + CLAMP_TO_EDGE (= el sampler del shader, single source of truth): el texel es
        ' uv*size - 0.5 (offset de medio texel), se lerpea entre floor(texel) y floor(texel)+1, ambos
        ' clampeados a [0,size-1]. (Antes era uv*(size-1) "fit-endpoints", que NO matchea el sampler GL ->
        ' el resampling GPU/CPU divergia en canales a OTRA resolucion que el acumulador, p.ej. S: acumulador
        ' 512 con capas/swaps 1024. D/N no entran aca: SampleChannelAt usa indice directo si los tamanos
        ' coinciden. Tambien alinea el sample de la hair-LUT del brow.)
        Dim fx = Clamp01(u) * w - 0.5F
        Dim fy = Clamp01(v) * h - 0.5F
        Dim ix = CInt(MathF.Floor(fx)), iy = CInt(MathF.Floor(fy))
        Dim tx = fx - ix, ty = fy - iy
        Dim x0 = Math.Max(0, Math.Min(w - 1, ix)), x1 = Math.Max(0, Math.Min(w - 1, ix + 1))
        Dim y0 = Math.Max(0, Math.Min(h - 1, iy)), y1 = Math.Max(0, Math.Min(h - 1, iy + 1))
        Dim c00 = t.Unit((y0 * w + x0) * 4 + ch)
        Dim c10 = t.Unit((y0 * w + x1) * 4 + ch)
        Dim c01 = t.Unit((y1 * w + x0) * 4 + ch)
        Dim c11 = t.Unit((y1 * w + x1) * 4 + ch)
        Return c00 * (1 - tx) * (1 - ty) + c10 * tx * (1 - ty) + c01 * (1 - tx) * ty + c11 * tx * ty
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
                                                       Dim v = CSng((y + 0.5) / dh)
                                                       Dim fy = Clamp01(v) * sh - 0.5F
                                                       Dim iy = CInt(MathF.Floor(fy)) : Dim ty = fy - iy
                                                       Dim y0 = Math.Max(0, Math.Min(sh - 1, iy)), y1 = Math.Max(0, Math.Min(sh - 1, iy + 1))
                                                       For x = 0 To dw - 1
                                                           Dim u = CSng((x + 0.5) / dw)
                                                           Dim fx = Clamp01(u) * sw - 0.5F
                                                           Dim ix = CInt(MathF.Floor(fx)) : Dim tx = fx - ix
                                                           Dim x0 = Math.Max(0, Math.Min(sw - 1, ix)), x1 = Math.Max(0, Math.Min(sw - 1, ix + 1))
                                                           Dim i00 = (y0 * sw + x0) * 4, i10 = (y0 * sw + x1) * 4, i01 = (y1 * sw + x0) * 4, i11 = (y1 * sw + x1) * 4
                                                           Dim o = (y * dw + x) * 4
                                                           For ch = 0 To 3
                                                               outp(o + ch) = CSng(src(i00 + ch) * (1 - tx) * (1 - ty) + src(i10 + ch) * tx * (1 - ty) +
                                                                                   src(i01 + ch) * (1 - tx) * ty + src(i11 + ch) * tx * ty)
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
                                                       Dim v = CSng((y + 0.5) / dh)
                                                       Dim fy = Clamp01(v) * sh - 0.5F
                                                       Dim iy = CInt(MathF.Floor(fy)) : Dim ty = fy - iy
                                                       Dim y0 = Math.Max(0, Math.Min(sh - 1, iy)), y1 = Math.Max(0, Math.Min(sh - 1, iy + 1))
                                                       For x = 0 To dw - 1
                                                           Dim u = CSng((x + 0.5) / dw)
                                                           Dim fx = Clamp01(u) * sw - 0.5F
                                                           Dim ix = CInt(MathF.Floor(fx)) : Dim tx = fx - ix
                                                           Dim x0 = Math.Max(0, Math.Min(sw - 1, ix)), x1 = Math.Max(0, Math.Min(sw - 1, ix + 1))
                                                           Dim i00 = (y0 * sw + x0) * 4, i10 = (y0 * sw + x1) * 4, i01 = (y1 * sw + x0) * 4, i11 = (y1 * sw + x1) * 4
                                                           Dim o = (y * dw + x) * 4
                                                           For ch = 0 To 3
                                                               Dim c = bgra(i00 + ch) * (1 - tx) * (1 - ty) + bgra(i10 + ch) * tx * (1 - ty) +
                                                                       bgra(i01 + ch) * (1 - tx) * ty + bgra(i11 + ch) * tx * ty
                                                               outp(o + ch) = CByte(MathF.Max(0.0F, MathF.Min(255.0F, MathF.Round(c, MidpointRounding.ToEven))))
                                                           Next
                                                       Next
                                                   End Sub)
        Return outp
    End Function

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

    ''' <summary>Bytes vivos en el <see cref="BatchDecodeCache"/> (solo se contabiliza si hay presupuesto).</summary>
    Private _batchCacheBytes As Long = 0
    ''' <summary>Cuantas entradas se rechazaron por presupuesto. Se reporta: un rechazo alto significa que el
    ''' techo esta costando re-decodes, y eso hay que poder verlo en vez de deducirlo del reloj.</summary>
    Private _batchCacheRejected As Integer = 0

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
        If raw = "" Then
            Dim avail = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes
            Dim b = CLng(avail * 0.25)
            b = Math.Max(512L * 1024L * 1024L, Math.Min(4096L * 1024L * 1024L, b))
            Return (b, $"Decode cache: techo {b \ (1024L * 1024L)} MB = 25% de {avail \ (1024L * 1024L)} MB disponibles")
        End If
        Dim mb As Integer = -1
        If Not Integer.TryParse(raw, mb) Then mb = -1
        If mb > 0 Then Return (CLng(mb) * 1024L * 1024L, $"Decode cache: {mb} MB ceiling (set by FGBAKE_DECODE_CACHE_MB)")
        Return (0L, "Decode cache: NO ceiling (FGBAKE_DECODE_CACHE_MB=0, historical behaviour)")
    End Function

    ''' <summary>Bytes vivos y rechazos del cache batch, para el log del runner.</summary>
    Public Function BatchDecodeCacheStats() As (Bytes As Long, Rejected As Integer)
        Return (Threading.Interlocked.Read(_batchCacheBytes), Threading.Volatile.Read(_batchCacheRejected))
    End Function

    ''' <summary>Arranca el cache de decode batch (llamar ANTES del loop de clones).</summary>
    Public Sub BeginBatchDecodeCache()
        BatchDecodeCache = New System.Collections.Concurrent.ConcurrentDictionary(Of String, DecodedTex)(StringComparer.OrdinalIgnoreCase)
        Threading.Interlocked.Exchange(_batchCacheBytes, 0L)
        Threading.Volatile.Write(_batchCacheRejected, 0)
    End Sub

    ''' <summary>Cierra y libera el cache de decode batch (llamar en Finally despues del loop). Los
    ''' DecodedTex son managed (Double() Rgba, sin recursos nativos) -> Clear + GC alcanza.</summary>
    Public Sub EndBatchDecodeCache()
        Dim c = BatchDecodeCache
        BatchDecodeCache = Nothing
        If c IsNot Nothing Then c.Clear()
    End Sub

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
        Dim cache = If(decodeCache, If(BatchDecodeCache, New System.Collections.Concurrent.ConcurrentDictionary(Of String, DecodedTex)(StringComparer.OrdinalIgnoreCase)))
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
        Dim ck = If(preferW > 0 OrElse preferH > 0, $"{key}@{preferW}x{preferH}", key)
        Dim t As DecodedTex = Nothing
        If Not String.IsNullOrEmpty(key) AndAlso cache.TryGetValue(ck, t) Then Return t
        t = DecodeDds(bytes, preferW, preferH)
        If Not String.IsNullOrEmpty(key) AndAlso t IsNot Nothing Then
            ' El presupuesto SOLO aplica al cache de batch (el compartido entre NPCs). Cuando `cache` es el
            ' diccionario per-call de una sola cara, no cachear no ahorraria nada y solo agregaria re-decodes
            ' dentro del mismo compose.
            Dim budget = BatchDecodeCacheBudgetBytes
            If budget <= 0L OrElse Not Object.ReferenceEquals(cache, BatchDecodeCache) Then
                cache(ck) = t
            Else
                Dim sz As Long = If(t.Rgba8 Is Nothing, 0L, CLng(t.Rgba8.Length))   ' Byte() ⇒ 1 B por elemento
                If Threading.Interlocked.Add(_batchCacheBytes, sz) <= budget Then
                    cache(ck) = t
                Else
                    ' No entra: se devuelve el decode igual (correcto), simplemente no se retiene.
                    Threading.Interlocked.Add(_batchCacheBytes, -sz)
                    Threading.Interlocked.Increment(_batchCacheRejected)
                End If
            End If
        End If
        Return t
    End Function

    ''' <summary>Compone UN canal: seed (D = g22(src), N/S = src) → region swaps (crossfade en linear) →
    ''' capas de tint (over-running, ley del resolver).
    ''' <para>⛔ SYNC: CPU/GPU compositor — es el espejo EXACTO del seed + <c>ApplyRegionSwapsOntoFaceTexture</c>
    ''' + <c>ComposeOntoFaceTexture</c> del camino GL (FaceTintCompositor). Los dos leen sus parámetros del
    ''' MISMO <c>FaceTintConvention.ResolveConvention</c>, que es lo que hace que la paridad sea por
    ''' construcción y no por coincidencia. Duele si diverge porque el BAKE corre 100 % CPU y el RENDER por
    ''' GL: un barrido validaría un camino que el usuario nunca ve. Ver 50-facetint-leyes-y-compositor.md.</para></summary>
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
        ' Acumulador RGB en OutputSpace del canal (build_3): D=sRGB (= src directo, SIN g22) ; N/S=raw lineal.
        ' El storage del engine FaceCustomization es sRGB (= formato de CK en disco); no se acumula en g22.
        ' Seed via SampleChannelAt (índice directo si tamaños iguales; bilineal si difieren = resize).
        ' Acumuladores en Single (storage): el math por-píxel abajo corre en Double y se guarda con CSng.
        Dim accR(n - 1) As Single, accG(n - 1) As Single, accB(n - 1) As Single
        ' Acumulador ALPHA: PASSTHROUGH del base, nunca compuesto. Las blend-ops son RGB-only por definicion
        ' (mismo contrato que documenta el shader GL), asi que el alpha del base viaja intacto hasta el pack.
        ' Medido: el _d que hornea el CK lleva exactamente el alpha del head diffuse de origen. Antes esta
        ' funcion escribia alpha=255 fija y el canal se perdia. Inerte para N/S (BC5 no tiene alpha).
        ' El acumulador solo se consume en el pack de mas abajo y SOLO bajo keepBaseAlpha, cuyos dos terminos
        ' son parametros de esta funcion: se decide ACA, antes de reservar nada. Antes se reservaba el array y
        ' se sampleaba el canal 3 por pixel en los tres canales pasara lo que pasara (67 MB a 4096^2).
        Dim keepBaseAlpha As Boolean = headDiffuseAlphaTest AndAlso isD
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
        Dim srcDirect As Boolean = (src.Width = w AndAlso src.Height = h)
        Dim srcPx As Byte() = If(srcDirect, src.Rgba8, Nothing)
        Dim seedLut = ByteToUnit
        ' ⭐ SEED VECTORIZADO. Deja de ser gratis justo con la convención REAL de FO4: ahí `accSp`(Linear) y
        ' `outSp`(G22) DIFIEREN, así que `Cvt1` NO cortocircuita y esto es UN POW POR PÍXEL Y POR CANAL a
        ' resolución nativa, una vez por canal. Con el config viejo (accSp == outSp) era identidad y por eso
        ' nunca figuró como resto escalar.
        ' Sólo el camino `srcDirect`: el otro es SampleChannelAt = bilineal por UV = gather.
        Dim seedFromSp As Integer = If(SeedConventionIs_G22 AndAlso isD, seedSrc, outSp)
        Dim seedVecOk As Boolean = FastPow.AcceleratedV AndAlso srcDirect
        System.Threading.Tasks.Parallel.ForEach(
            System.Collections.Concurrent.Partitioner.Create(0, n),
            Sub(range)
                Dim iv = range.Item1
                If seedVecOk Then
                    While iv + lanes <= range.Item2
                        Dim rV, gV, bV, aV As Vector(Of Single)
                        LoadRgba8BlockV(srcPx, iv * 4, rV, gV, bV, aV)
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
                        r0 = SampleChannelAt(src, i, w, h, 0)
                        g0 = SampleChannelAt(src, i, w, h, 1)
                        b0 = SampleChannelAt(src, i, w, h, 2)
                        If keepBaseAlpha Then accA(i) = CSng(SampleChannelAt(src, i, w, h, 3))
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
                Dim cv = FaceTintConvention.ResolveConvention(False, 0US, 0, channel, False, forBake:=True, forSwap:=True)
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
                Dim swVecOk As Boolean = swDirect AndAlso mkDirect AndAlso VecComposeSupported(0, sbop, ssl)
                System.Threading.Tasks.Parallel.ForEach(
                    System.Collections.Concurrent.Partitioner.Create(0, n),
                    Sub(range)
                        Dim iv = range.Item1
                        If swVecOk Then
                            Dim msdvV = VBroadcast(msdv)
                            While iv + lanes <= range.Item2
                                Dim srV, sgV, sbV, saV As Vector(Of Single)
                                LoadRgba8BlockV(swPx, iv * 4, srV, sgV, sbV, saV)
                                ' la máscara del swap es el canal R (igual que el escalar: mkPx(i*4))
                                Dim mkR, mkG, mkB, mkA As Vector(Of Single)
                                LoadRgba8BlockV(mkPx, iv * 4, mkR, mkG, mkB, mkA)
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
                                sr = SampleChannelAt(swTex, i, w, h, 0)
                                sg = SampleChannelAt(swTex, i, w, h, 1)
                                sb = SampleChannelAt(swTex, i, w, h, 2)
                            End If
                            If mkDirect Then
                                mask = swLut(mkPx(i * 4))
                            Else
                                mask = SampleChannelAt(mkTex, i, w, h, 0)
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
        ' ⛔ El pre-scan que decide si hace falta enumera el ESPACIO DE PARÁMETROS COMPLETO que el loop puede
        ' pasarle a ResolveConvention, no sólo el caso actual: así el guard sigue siendo correcto aunque
        ' alguien haga que el Framework dependa de alguna de esas entradas. Y es CONSERVADOR a propósito (no
        ' replica los `Continue For` del loop real): como mucho reserva de más, nunca de menos, que es el
        ' único error que cambiaría un byte.
        Dim needsBase As Boolean = False
        If layers IsNot Nothing Then
            For Each pLayer In layers
                If pLayer Is Nothing Then Continue For
                For Each hp As Boolean In New Boolean() {False, True}
                    Dim pfw = FaceTintConvention.ResolveConvention(
                        pLayer.IsTextureSet, pLayer.Slot, pLayer.BlendOp, channel, hp, forBake:=True).Framework
                    If pfw = FaceTintFramework.OverBase OrElse pfw = FaceTintFramework.AddBase Then
                        needsBase = True
                        Exit For
                    End If
                Next
                If needsBase Then Exit For
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
                    Dim sConv = FaceTintConvention.ResolveConvention(sLayer.IsTextureSet, sLayer.Slot, sLayer.BlendOp, channel, False, forBake:=True)
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

                Dim conv = FaceTintConvention.ResolveConvention(
                    layer.IsTextureSet, layer.Slot, layer.BlendOp, channel, useHairPalette, forBake:=True)
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
                '   Fase B (por bloques de 8, VECTORIAL): los 3 ComposeOne. Ahi vive el 83 % del kernel (los
                '     pow de las conversiones de espacio), medido sobre el ComposePixel real.
                ' El acumulador de FO4 es SoA (accR/accG/accB separados) ⇒ 8 pixeles son 8 floats contiguos:
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
                '   - Not layerDirect  -> el sampleo es bilineal por UV = GATHER. Es LA barrera real de todo
                '     este trabajo: la API cross-platform Vector(Of T) no tiene gather y Avx2.GatherVector256
                '     es x86-only, o sea que usarlo reintroduciria DOS leyes segun la CPU.
                '   - useHairPalette   -> SampleLutEngine es un fetch NEAREST indexado por el valor del pixel:
                '     otro gather, y ademas engine-exact (no se toca).
                '   - preToneSkin      -> muestrea la mascara del skintone y corre 3 ComposeOne mas por pixel;
                '     es un camino raro (flagged-after-skintone) y no vale duplicarlo.
                '   - diffMask no directa -> idem layerDirect, para la textura de la mascara.
                ' Lo que queda cubierto es el caso NORMAL y el 100 % de la data vanilla: PaletteMask con color
                ' plano y TextureSet con la textura de la capa.
                Dim diffMaskDirect As Boolean = (diffMaskTex IsNot Nothing AndAlso diffMaskTex.Width = w AndAlso diffMaskTex.Height = h)
                Dim diffMaskPx As Byte() = If(diffMaskDirect, diffMaskTex.Rgba8, Nothing)
                Dim fastA As Boolean = vecOk AndAlso layerDirect AndAlso Not useHairPalette AndAlso Not preToneSkin _
                                       AndAlso (diffMaskTex Is Nothing OrElse diffMaskDirect)
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
                              LoadRgba8BlockV(layerPx, iStart * 4, lrV, lgV, lbV, laV)
                              Dim sRV, sGV, sBV, mV As Vector(Of Single)
                              ' ⭐ El ESPEJO de LayerSrcMaskPixel: la MISMA funcion que el loop escalar
                              ' llama, y la misma que el self-test contrasta. Tenerla extraida es lo que
                              ' hace que el test valga: inline, el test tendria que re-implementar la
                              ' cadena y podria coincidir con el bug en vez de detectarlo.
                              Dim dmAV = If(diffMaskPx Is Nothing, Vector(Of Single).Zero,
                                            LoadAlpha8BlockV(diffMaskPx, iStart * 4))
                              LayerSrcMaskBlockV(lrV, lgV, lbV, laV, dmAV,
                                                 isPalette, isD, forceUniform, texTimesColor,
                                                 diffMaskPx IsNot Nothing,
                                                 colRV, colGV, colBV, sRV, sGV, sBV, mV)
                              ' Cobertura: mismas ops y mismo orden que CovBlockV (convertir, multiplicar, clampear).
                              Dim covV = Clamp01V(Vector.Multiply(ConvMaskV(mV, mc), opV))
                              If Not Vector.LessThanOrEqualAll(covV, zeroV) Then
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
                            lr = SampleChannelAt(layerTex, i, w, h, 0)
                            lg = SampleChannelAt(layerTex, i, w, h, 1)
                            lb = SampleChannelAt(layerTex, i, w, h, 2)
                            la = SampleChannelAt(layerTex, i, w, h, 3)
                        End If

                        ' mask + src por kind (= rama uLayerKind del shader)
                        Dim maskV As Single
                        Dim srcR As Single, srcG As Single, srcB As Single
                        Dim isPal As Boolean = (kind = FaceTintLayerKind.PaletteMask)
                        Dim hasDm As Boolean = (diffMaskTex IsNot Nothing)
                        Dim dmA As Single = 0.0F
                        If hasDm AndAlso Not isPal AndAlso Not isD Then dmA = SampleChannelAt(diffMaskTex, i, w, h, 3)
                        ' ⭐ LA MISMA funcion que el bloque vectorial (via LayerSrcMaskBlockV) y que el
                        ' self-test contrastan. Extraida a proposito: inline, el test tendria que
                        ' re-implementar la cadena y podria coincidir con el bug en vez de detectarlo.
                        LayerSrcMaskPixel(lr, lg, lb, la, dmA, isPal, isD, forceUniform, texTimesColor, hasDm,
                                          uColR, uColG, uColB, srcR, srcG, srcB, maskV)
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
                                If Not Vector.LessThanOrEqualAll(covV, Vector(Of Single).Zero) Then
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

        ' --- Pack a BGRA byte (clamp+round). D ya esta en g22, N/S lineal. ---
        ' ALPHA del _d: passthrough del alpha del base SOLO si la cabeza usa Diffuse Alpha Test (flag ACBS
        ' 0x01000000); si no, opaco. El CK aplana el alpha del _d salvo cuando el material de la cabeza lo
        ' testea; el passthrough incondicional le inventaba alpha a DiMA. Inerte para N/S. Ver 40-bake-leyes-fo4.
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

        Dim outB(n * 4 - 1) As Byte
        ' keepBaseAlpha se resolvió ARRIBA, antes del seed, porque además de elegir el alpha de salida decide
        ' si accA se reserva y se llena (ver ahí). Mismos dos términos, mismo valor.
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
        Return New CpuChannelResult With {.Width = w, .Height = h, .Bgra = outB}
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
    Private Function SrgbToLinV(c As Vector(Of Single)) As Vector(Of Single)
        c = Clamp01V(c)
        Dim loB = Vector.Divide(c, VBroadcast(12.92F))
        Dim hiB = FastPow.PowV(Vector.Divide(Vector.Add(c, VBroadcast(0.055F)),
                                                   VBroadcast(1.055F)), FastPow.G24)
        Return Vector.ConditionalSelect(Vector.LessThanOrEqual(c, VBroadcast(0.04045F)), loB, hiB)
    End Function

    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Function LinToSrgbV(c As Vector(Of Single)) As Vector(Of Single)
        c = Clamp01V(c)
        Dim loB = Vector.Multiply(c, VBroadcast(12.92F))
        Dim hiB = Vector.Subtract(Vector.Multiply(VBroadcast(1.055F),
                                                        FastPow.PowV(c, FastPow.InvG24)),
                                     VBroadcast(0.055F))
        Return Vector.ConditionalSelect(Vector.LessThanOrEqual(c, VBroadcast(0.0031308F)), loB, hiB)
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
        Dim eq = Vector.Equals(Of Single)(a, b)
        Dim r = Vector.ConditionalSelect(eq, Vector.BitwiseOr(a, b),
                                            Vector.ConditionalSelect(Vector.LessThan(a, b), a, b))
        ' `b` NaN primero y `a` NaN despues: en una cadena de selects gana el ULTIMO, y en Math.Min/Max gana `a`.
        r = Vector.ConditionalSelect(Vector.Equals(Of Single)(b, b), r, b)
        Return Vector.ConditionalSelect(Vector.Equals(Of Single)(a, a), r, a)
    End Function

    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Function MaxV(a As Vector(Of Single), b As Vector(Of Single)) As Vector(Of Single)
        Dim eq = Vector.Equals(Of Single)(a, b)
        Dim r = Vector.ConditionalSelect(eq, Vector.BitwiseAnd(a, b),
                                            Vector.ConditionalSelect(Vector.GreaterThan(a, b), a, b))
        r = Vector.ConditionalSelect(Vector.Equals(Of Single)(b, b), r, b)
        Return Vector.ConditionalSelect(Vector.Equals(Of Single)(a, a), r, a)
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
            Case 3 ' pegtop: (1-2s)*d*d + 2*s*d
                Return Vector.Add(Vector.Multiply(Vector.Multiply(Vector.Subtract(one, Vector.Multiply(two, s)), d), d),
                                     Vector.Multiply(Vector.Multiply(two, s), d))
            Case Else ' 0 = W3C SVG
                Dim poly = Vector.Multiply(Vector.Add(Vector.Multiply(Vector.Subtract(Vector.Multiply(VBroadcast(16.0F), d), VBroadcast(12.0F)), d), VBroadcast(4.0F)), d)
                Dim g = Vector.ConditionalSelect(Vector.GreaterThanOrEqual(d, VBroadcast(0.25F)), Vector.SquareRoot(d), poly)
                Dim hiB = Vector.Add(d, Vector.Multiply(Vector.Subtract(Vector.Multiply(two, s), one), Vector.Subtract(g, d)))
                Dim loB = Vector.Subtract(d, Vector.Multiply(Vector.Multiply(Vector.Subtract(one, Vector.Multiply(two, s)), d), Vector.Subtract(one, d)))
                Return Vector.ConditionalSelect(Vector.GreaterThanOrEqual(s, half), hiB, loB)
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
                                 ByRef srcR As Single, ByRef srcG As Single, ByRef srcB As Single,
                                 ByRef maskV As Single)
        If isPalette Then
            srcR = uColR : srcG = uColG : srcB = uColB
            maskV = lg
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
                                   ByRef sRV As Vector(Of Single), ByRef sGV As Vector(Of Single),
                                   ByRef sBV As Vector(Of Single), ByRef mV As Vector(Of Single))
        If isPalette Then
            sRV = colRV : sGV = colGV : sBV = colBV
            mV = lgV
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

    ''' <summary>8 lanes con un valor 0..255 en cada una → unidad [0,1]. Espejo vectorial del LUT
    ''' <see cref="ByteToUnit"/>.</summary>
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
        c = Vector.ConditionalSelect(Vector.Equals(Of Single)(c, c), c, Vector(Of Single).Zero)   ' NaN -> 0
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
                        If Not Vector.LessThanOrEqualAll(covV, Vector(Of Single).Zero) Then
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

            For blkI = 0 To npx \ lanes - 1
                Dim at = blkI * lanes
                Dim lrV, lgV, lbV, laV As Vector(Of Single)
                LoadRgba8BlockV(px, at * 4, lrV, lgV, lbV, laV)
                Dim dmAV = If(hasDiffMask, LoadAlpha8BlockV(dm, at * 4), Vector(Of Single).Zero)
                Dim sRV, sGV, sBV, mV As Vector(Of Single)
                LayerSrcMaskBlockV(lrV, lgV, lbV, laV, dmAV, isPalette, isD, forceUniform, texTimesColor,
                                   hasDiffMask, colRV, colGV, colBV, sRV, sGV, sBV, mV)

                For j = 0 To lanes - 1
                    Dim p = (at + j) * 4
                    Dim lr = lut(px(p)), lg = lut(px(p + 1)), lb = lut(px(p + 2)), la = lut(px(p + 3))
                    Dim dmA = If(hasDiffMask, lut(dm(p + 3)), 0.0F)
                    Dim wR As Single, wG As Single, wB As Single, wM As Single
                    LayerSrcMaskPixel(lr, lg, lb, la, dmA, isPalette, isD, forceUniform, texTimesColor,
                                      hasDiffMask, uColR, uColG, uColB, wR, wG, wB, wM)
                    If BitConverter.SingleToInt32Bits(sRV(j)) <> BitConverter.SingleToInt32Bits(wR) OrElse
                       BitConverter.SingleToInt32Bits(sGV(j)) <> BitConverter.SingleToInt32Bits(wG) OrElse
                       BitConverter.SingleToInt32Bits(sBV(j)) <> BitConverter.SingleToInt32Bits(wB) OrElse
                       BitConverter.SingleToInt32Bits(mV(j)) <> BitConverter.SingleToInt32Bits(wM) Then
                        Return $"FASE A vector MISMATCH: pal={isPalette} isD={isD} uni={forceUniform} " &
                               $"txc={texTimesColor} dm={hasDiffMask} px={at + j} " &
                               $"escalar=({wR},{wG},{wB} | {wM}) vector=({sRV(j)},{sGV(j)},{sBV(j)} | {mV(j)})"
                    End If
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
                                       colRV, colGV, colBV, sRV, sGV, sBV, mV)
                    Dim wR As Single, wG As Single, wB As Single, wM As Single
                    LayerSrcMaskPixel(edges(a), edges(b), edges(c), edges((a + b) Mod edges.Length), 0.0F,
                                      False, False, False, False, False, uColR, uColG, uColB, wR, wG, wB, wM)
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
