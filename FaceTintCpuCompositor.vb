Option Strict On

Imports FO4_Base_Library.FaceTintConvention
Imports System.Runtime.CompilerServices

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
    ''' (0,45454544 en vez de 0,45454547) y por lo tanto otra imagen.</summary>
    Private ReadOnly InvG22 As Single = CSng(1.0 / 2.2)
    Private ReadOnly InvG24 As Single = CSng(1.0 / 2.4)

    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Function Clamp01(c As Single) As Single
        If c < 0.0F Then Return 0.0F
        If c > 1.0F Then Return 1.0F
        Return c
    End Function

    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Function SrgbToLin1(c As Single) As Single
        c = Clamp01(c)
        Return If(c <= 0.04045F, c / 12.92F, MathF.Pow((c + 0.055F) / 1.055F, 2.4F))
    End Function

    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Function LinToSrgb1(c As Single) As Single
        c = Clamp01(c)
        Return If(c <= 0.0031308F, c * 12.92F, 1.055F * MathF.Pow(c, InvG24) - 0.055F)
    End Function

    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Function G22ToLin1(c As Single) As Single
        Return MathF.Pow(Clamp01(c), 2.2F)
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
        Return MathF.Pow(Clamp01(c), InvG22)
    End Function

    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Function G24ToLin1(c As Single) As Single
        Return MathF.Pow(Clamp01(c), 2.4F)
    End Function

    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Function LinToG241(c As Single) As Single
        Return MathF.Pow(Clamp01(c), InvG24)
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
                Return MathF.Pow(MathF.Max(d, 0.000001F), MathF.Pow(2.0F, 2.0F * (0.5F - s)))
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
        System.Threading.Tasks.Parallel.ForEach(
            System.Collections.Concurrent.Partitioner.Create(0, n),
            Sub(range)
                For i As Integer = range.Item1 To range.Item2 - 1
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
                Dim swTex = CachedDecode(cache, sw.GetSwapCacheKey(channel), swBytes)
                Dim mkTex = CachedDecode(cache, sw.RegionMaskCacheKey, sw.RegionMaskDdsBytes)
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
                System.Threading.Tasks.Parallel.ForEach(
                    System.Collections.Concurrent.Partitioner.Create(0, n),
                    Sub(range)
                        For i As Integer = range.Item1 To range.Item2 - 1
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
                    Dim sTex = CachedDecode(cache, sLayer.GetChannelCacheKey(channel), sBytes)
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
                Dim layerTex = CachedDecode(cache, layer.GetChannelCacheKey(channel), chanBytes)
                If layerTex Is Nothing Then Continue For

                Dim useHairPalette = (layer.UseHairPalette AndAlso isD AndAlso layer.HairLutDdsBytes IsNot Nothing AndAlso layer.HairLutDdsBytes.Length > 0)
                Dim lutTex As DecodedTex = Nothing
                If useHairPalette Then
                    lutTex = CachedDecode(cache, layer.HairLutCacheKey, layer.HairLutDdsBytes)
                    If lutTex Is Nothing Then useHairPalette = False
                End If
                Dim forceUniform = (layer.ForceUniformColor AndAlso layer.Kind = FaceTintLayerKind.TextureSetDiffuse AndAlso isD AndAlso Not useHairPalette)
                Dim texTimesColor = (layer.MultiplyTextureByColor AndAlso layer.Kind = FaceTintLayerKind.TextureSetDiffuse AndAlso isD AndAlso Not useHairPalette AndAlso Not forceUniform)

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
                System.Threading.Tasks.Parallel.ForEach(
                    System.Collections.Concurrent.Partitioner.Create(0, n),
                    Sub(range)
                        For i As Integer = range.Item1 To range.Item2 - 1
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
                        If kind = FaceTintLayerKind.PaletteMask Then
                            If useHairPalette Then
                                srcR = SampleLutEngine(lutTex, lg, luY, 0, ss, os) : srcG = SampleLutEngine(lutTex, lg, luY, 1, ss, os) : srcB = SampleLutEngine(lutTex, lg, luY, 2, ss, os)
                            Else
                                srcR = uColR : srcG = uColG : srcB = uColB
                            End If
                            maskV = lg
                        Else ' TextureSetDiffuse
                            If useHairPalette Then
                                srcR = SampleLutEngine(lutTex, lg, luY, 0, ss, os) : srcG = SampleLutEngine(lutTex, lg, luY, 1, ss, os) : srcB = SampleLutEngine(lutTex, lg, luY, 2, ss, os)
                            ElseIf forceUniform Then
                                srcR = uColR : srcG = uColG : srcB = uColB
                            ElseIf texTimesColor Then
                                srcR = lr * uColR : srcG = lg * uColG : srcB = lb * uColB   ' skee type-0: tex × tint
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

                        Dim cov = Clamp01(ConvMask1(maskV, mc) * op)

                        ' base SÓLO existe si algún framework del canal es OverBase/AddBase (ver needsBase). Con
                        ' OverPrev/ModSrc ComposeOne no lee este parámetro en ninguna rama, así que pasar 0.0
                        ' es exactamente lo mismo que pasar el snapshot. Cuando SÍ hace falta, se lee el mismo
                        ' Single y se ensancha a Double igual que antes ⇒ bit-idéntico en los dos caminos.
                        Dim bR As Single = 0.0F, bG As Single = 0.0F, bB As Single = 0.0F
                        If needsBase Then
                            bR = baseR(i) : bG = baseG(i) : bB = baseB(i)
                        End If
                        ' composite agnostico (= shader): blend en ws, lerp en cs, storage en os.
                        accR(i) = CSng(ComposeOne(accR(i), srcR, cov, ws, cs, ss, os, bop, sl, bR, fw, accSpace:=accSp))
                        accG(i) = CSng(ComposeOne(accG(i), srcG, cov, ws, cs, ss, os, bop, sl, bG, fw, accSpace:=accSp))
                        accB(i) = CSng(ComposeOne(accB(i), srcB, cov, ws, cs, ss, os, bop, sl, bB, fw, accSpace:=accSp))
                        Next
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
            System.Threading.Tasks.Parallel.ForEach(
                System.Collections.Concurrent.Partitioner.Create(0, n),
                Sub(range)
                    For i As Integer = range.Item1 To range.Item2 - 1
                        accR(i) = CSng(Cvt1(accR(i), accSp, outSp))
                        accG(i) = CSng(Cvt1(accG(i), accSp, outSp))
                        accB(i) = CSng(Cvt1(accB(i), accSp, outSp))
                    Next
                End Sub)
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
        System.Threading.Tasks.Parallel.ForEach(
            System.Collections.Concurrent.Partitioner.Create(0, n),
            Sub(range)
                For i As Integer = range.Item1 To range.Item2 - 1
                    Dim o = i * 4
                    outB(o) = ToByte(accB(i)) : outB(o + 1) = ToByte(accG(i)) : outB(o + 2) = ToByte(accR(i)) : outB(o + 3) = If(keepBaseAlpha, ToByte(accA(i)), CByte(255))
                Next
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

End Module
