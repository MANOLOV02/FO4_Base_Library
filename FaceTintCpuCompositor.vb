Imports FO4_Base_Library.FaceTintConvention
Imports System.Runtime.CompilerServices

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
    ' (reference_cpu_gpu_parity_bc_decode); el codigo no tiene por que pagarlo en cada bake.

    ' ---- Conversiones de espacio (transcripción 1:1 del shader; ws: 0=linear 1=srgb 2=g22) ----
    ' ⭐ AggressiveInlining: estos helpers se invocan MILLONES de veces por capa desde los loops
    ' per-pixel y el costo de la LLAMADA domina sobre su cuerpo (3-6 lineas). Es una HINT de
    ' compilacion: no cambia ni una operacion ni el orden, y .NET usa SSE sin precision excedente
    ' (no hay x87), asi que la salida es BIT-IDENTICA. Ver el izado de invariantes de los 3 loops.
    ' ⛔ SACADO (2026-07-30): las variantes de diagnostico `FGBAKE_COMPOSE_F32` (angostar cada intermedio
    ' de ComposeOne a Single) y `FGBAKE_MASK_POW_F32` (emular el pow float32 del GLSL en la mascara).
    ' Las DOS quedaron REFUTADAS con datos: la primera dio un histograma BIT-IDENTICO en aislamiento
    ' (1 swap, 0 capas) y la segunda no movio un pixel. La causa real era otra —el GPU mezclaba MIPMAPS
    ' donde este modulo siempre muestrea mip 0— y esta arreglada en FaceTintCompositor.ForceMip0Sampling.
    ' Se sacan porque cambiaban (o podian cambiar) el artefacto horneado sin comprar nada.
    ' El detalle de las mediciones queda en memoria: reference_cpu_gpu_parity_bc_decode.

    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Function Clamp01(c As Double) As Double
        If c < 0.0 Then Return 0.0
        If c > 1.0 Then Return 1.0
        Return c
    End Function

    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Function SrgbToLin1(c As Double) As Double
        c = Clamp01(c)
        Return If(c <= 0.04045, c / 12.92, Math.Pow((c + 0.055) / 1.055, 2.4))
    End Function

    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Function LinToSrgb1(c As Double) As Double
        c = Clamp01(c)
        Return If(c <= 0.0031308, c * 12.92, 1.055 * Math.Pow(c, 1.0 / 2.4) - 0.055)
    End Function

    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Function G22ToLin1(c As Double) As Double
        Return Math.Pow(Clamp01(c), 2.2)
    End Function

    ''' <summary>Convierte los canales RGB (g22) de un BGRA byte-array a LINEAL, in place (deja A). El compose
    ''' CPU del DIFFUSE saca g22 (para el DDS del bake); el RENDER GL deja el output en linear (G22→Linear final).
    ''' Para que el render en modo CPU-skinning se vea IGUAL que en GPU, el diffuse compuesto por CPU se convierte
    ''' a linear antes de subir a GL. N/S ya son lineales (no llamar esto sobre ellos).</summary>
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
            lut(b) = CByte(Math.Round(G22ToLin1(b / 255.0) * 255.0))
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
    Private Function LinToG221(c As Double) As Double
        Return Math.Pow(Clamp01(c), 1.0 / 2.2)
    End Function

    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Function G24ToLin1(c As Double) As Double
        Return Math.Pow(Clamp01(c), 2.4)
    End Function

    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Function LinToG241(c As Double) As Double
        Return Math.Pow(Clamp01(c), 1.0 / 2.4)
    End Function

    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Function SpaceToLin1(c As Double, s As Integer) As Double
        If s = 0 Then Return c
        If s = 1 Then Return SrgbToLin1(c)
        If s = 3 Then Return G24ToLin1(c)
        Return G22ToLin1(c)   ' s=2
    End Function

    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Function LinToSpace1(c As Double, s As Integer) As Double
        If s = 0 Then Return c
        If s = 1 Then Return LinToSrgb1(c)
        If s = 3 Then Return LinToG241(c)
        Return LinToG221(c)   ' s=2
    End Function

    ''' <summary>cvt agnóstico entre espacios (0=linear 1=srgb 2=g22) via linear. = shader cvt().</summary>
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Function Cvt1(c As Double, fromS As Integer, toS As Integer) As Double
        If fromS = toS Then Return c
        Return LinToSpace1(SpaceToLin1(c, fromS), toS)
    End Function

    ''' <summary>mask conv (0=raw 1=srgbEnc 2=srgbDec 3=g22Enc 4=g22Dec). = shader convMaskFull().</summary>
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Function ConvMask1(m As Double, mc As Integer) As Double
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
    Private Function BlendOverlay1(d As Double, s As Double) As Double
        ' GLSL step(0.5,d): d>=0.5 -> 1-2(1-d)(1-s) ; d<0.5 -> 2ds
        If d >= 0.5 Then Return 1.0 - 2.0 * (1.0 - d) * (1.0 - s)
        Return 2.0 * d * s
    End Function

    ''' <summary>Soft-light AGNOSTICO por modelo (= shader blendSoftLightModel; paridad CPU/GL). model:
    ''' 0=W3C 1=GIMP 2=Illusions 3=pegtop (FaceTintSoftLight). d=base, s=src. Default del resolver = GIMP.</summary>
    Private Function BlendSoftLightModel(model As Integer, d As Double, s As Double) As Double
        d = Clamp01(d) : s = Clamp01(s)
        Select Case model
            Case 1 ' GIMP / Photoshop
                If s <= 0.5 Then Return 2.0 * d * s + d * d * (1.0 - 2.0 * s)
                Return 2.0 * d * (1.0 - s) + Math.Sqrt(d) * (2.0 * s - 1.0)
            Case 2 ' Illusions.hu  d^(2^(2(0.5-s)))
                Return Math.Pow(Math.Max(d, 0.000001), Math.Pow(2.0, 2.0 * (0.5 - s)))
            Case 3 ' pegtop
                Return (1.0 - 2.0 * s) * d * d + 2.0 * s * d
            Case Else ' 0 = W3C SVG
                Dim g As Double = If(d >= 0.25, Math.Sqrt(d), ((16.0 * d - 12.0) * d + 4.0) * d)
                If s >= 0.5 Then Return d + (2.0 * s - 1.0) * (g - d)
                Return d - (1.0 - 2.0 * s) * d * (1.0 - d)
        End Select
    End Function

    ' ---- Modos separables estandar adicionales (5..19). Transcripcion 1:1 del shader. ----
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Function BlendColorDodge1(d As Double, s As Double) As Double
        If s >= 1.0 Then Return 1.0
        Return Math.Min(1.0, d / (1.0 - s))
    End Function
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Function BlendColorBurn1(d As Double, s As Double) As Double
        If s <= 0.0 Then Return 0.0
        Return 1.0 - Math.Min(1.0, (1.0 - d) / s)
    End Function
    Private Function BlendDivide1(d As Double, s As Double) As Double
        If s <= 0.0 Then Return 1.0
        Return Math.Min(1.0, d / s)
    End Function
    Private Function BlendVividLight1(d As Double, s As Double) As Double
        If s < 0.5 Then Return BlendColorBurn1(d, 2.0 * s)
        Return BlendColorDodge1(d, 2.0 * (s - 0.5))
    End Function
    Private Function BlendPinLight1(d As Double, s As Double) As Double
        If s < 0.5 Then Return Math.Min(d, 2.0 * s)
        Return Math.Max(d, 2.0 * s - 1.0)
    End Function

    ''' <summary>Identidad del blend op: el src que hace blend(prev,src)=prev. La usa ModSrc para que
    ''' cov=0 deje prev intacto: mix(neutral, src, cov). = shader blendNeutral(). bop=replace no tiene
    ''' identidad constante -> ModSrc degrada a OverPrev (ver ComposeOne).</summary>
    ''' ⭐ TABLA en vez de Select Case: `bop` es INVARIANTE para toda la textura, asi que este Select
    ''' devolvia SIEMPRE la misma constante y se re-evaluaba una vez por pixel y por canal (millones por
    ''' capa). La tabla da el MISMO valor con un indexado, sin ramas.
    ''' ⛔ BIT-IDENTICO: son exactamente los mismos tres literales (1.0 / 0.5 / 0.0) mapeados a los mismos
    ''' bop. El indice 0 y cualquier bop fuera de [0,18] caen en 0.0, igual que el `Case Else` de antes.
    Private ReadOnly BlendNeutralTable As Double() = BuildBlendNeutralTable()
    Private Function BuildBlendNeutralTable() As Double()
        Dim t(18) As Double                                  ' default 0.0 = el Case Else previo
        For Each b In New Integer() {1, 6, 9, 13, 15} : t(b) = 1.0 : Next    ' multiply/darken/colorburn/linearburn/divide
        For Each b In New Integer() {2, 3, 4, 16, 17, 18} : t(b) = 0.5 : Next ' overlay/softlight/hardlight/linearlight/vividlight/pinlight
        Return t
    End Function

    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Function BlendNeutral1(bop As Integer) As Double
        If bop < 0 OrElse bop > 18 Then Return 0.0
        Return BlendNeutralTable(bop)
    End Function

    ''' <summary>Dispatch de blend por canal escalar. 0=replace 1=mult 2=overlay 3=softlight 4=hardlight,
    ''' 5..19 = modos separables estandar. softLight: modelo cuando blendOp=3. = shader blendDispatchBop().</summary>
    Private Function BlendDispatch1(blendOp As Integer, softLight As Integer, d As Double, s As Double) As Double
        Select Case blendOp
            Case 1 : Return d * s                                ' multiply
            Case 2 : Return BlendOverlay1(d, s)                  ' overlay
            Case 3 : Return BlendSoftLightModel(softLight, d, s) ' softlight (modelo elegido)
            Case 4 : Return BlendOverlay1(s, d)                  ' hardlight = overlay(s,d)
            Case 5 : Return d + s - d * s                        ' screen
            Case 6 : Return Math.Min(d, s)                       ' darken
            Case 7 : Return Math.Max(d, s)                       ' lighten
            Case 8 : Return BlendColorDodge1(d, s)               ' colordodge
            Case 9 : Return BlendColorBurn1(d, s)                ' colorburn
            Case 10 : Return Math.Abs(d - s)                     ' difference
            Case 11 : Return d + s - 2.0 * d * s                 ' exclusion
            Case 12 : Return Math.Min(1.0, d + s)                ' lineardodge (add)
            Case 13 : Return Math.Max(0.0, d + s - 1.0)          ' linearburn
            Case 14 : Return Math.Max(0.0, d - s)                ' subtract
            Case 15 : Return BlendDivide1(d, s)                  ' divide
            Case 16 : Return Clamp01(d + 2.0 * s - 1.0)          ' linearlight
            Case 17 : Return BlendVividLight1(d, s)              ' vividlight
            Case 18 : Return BlendPinLight1(d, s)                ' pinlight
            Case 19 : Return If(d + s >= 1.0, 1.0, 0.0)          ' hardmix
            Case Else : Return s                                 ' replace (0, default)
        End Select
    End Function

    ''' <summary>Per-channel blend op, PUBLIC re-use of the CPU/GL-parity dispatch (BlendDispatch1 = shader
    ''' blendDispatchBop). Used by the SSE RaceMenu-overlay compositor so it shares the SAME blend math as the
    ''' FO4 facetint (one source of truth, CPU==GL). blendOp/softLightModel per the enum above.</summary>
    Public Function BlendChannel(blendOp As Integer, softLightModel As Integer, base As Double, src As Double) As Double
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
        ' ⭐ STORAGE en Byte: el ancho NATIVO del dato. TODO lo que produce DecodeDds es de 8 bits (BC1/3/7 y
        ' RGBA8/BGRA8 → RGBA8; BC5 → R8G8; BC4 → R8: no hay UN formato de más de 8 bits en la tabla de abajo),
        ' así que guardarlo en Single desperdiciaba 4× (16 B/px para 4 B/px de información). Antes fue
        ' Double→Single ("536 MB→268 MB @4K"); esto lo lleva a 67 MB. Se lee vía ByteToUnit ⇒ el Single que sale
        ' es EL MISMO que se guardaba antes, bit a bit, y la MATEMÁTICA sigue en Double (los escalares widen).
        ' ⛔ INVARIANTE: este buffer es READ-ONLY después del decode. Nadie escribe en él — ReconstructNormalZ
        ' y el compose trabajan sobre ACUMULADORES aparte (outp/macc/acc), que siguen en Single y sin cuantizar.
        ' Si alguna vez hiciera falta escribir un valor arbitrario acá, esto deja de ser lossless.
        ' ⛔ El campo se llama Rgba8 y NO Rgba A PROPOSITO: al angostar el storage, TODO consumidor que siguiera
        ' escribiendo `t.Unit(i)` habria COMPILADO igual (VB widenea Byte→Double en silencio) y leido 255 donde
        ' esperaba 1,0 — corrupcion muda en ~80 sitios. Con el nombre nuevo el compilador los marca a todos y la
        ' migracion es exhaustiva por construccion. Leer SIEMPRE por Unit()/CopyUnitTo(), nunca el byte crudo.
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

    ''' <summary>⭐ LA ley de reconstrucción del eje Z de un normal map de 2 canales (BC5/R8G8), in-place sobre un
    ''' buffer RGBA [0,1]. UNA sola implementación: la usan el decode de las texturas de overlay
    ''' (<c>SseFaceTintComposer.DecodeNormalRgba</c>) y el decode del <c>_msn</c> de la cabeza en el bake y en el
    ''' render — si divergieran, el mismo tatuaje se hornearía distinto de como se ve.
    '''
    ''' <para>Fórmula: se decodifica x,y a [−1,1] y se despeja <c>z = sqrt(max(0, 1 − x² − y²))</c>, que es la
    ''' inversa EXACTA (no una heurística) del encode de un normal unitario. El signo no es ambiguo: una fuente de
    ''' 2 canales no puede ser model-space —es justo lo que valida CharGen Options para el <c>_msn</c>— así que es
    ''' tangent-space autorada y ahí <c>z ≥ 0</c> SIEMPRE.</para>
    '''
    ''' <para>Se aplica DESPUÉS del resample, no sobre los texels de origen: es lo que hace el hardware (se
    ''' samplea el BC5 ya FILTRADO y recién ahí el shader despeja z), así que este orden es el que matchea al
    ''' GPU. El alpha no se toca (una fuente de 2 canales no tiene alpha; vale la constante 1 del pack).</para></summary>
    Public Sub ReconstructNormalZ(rgba As Single(), npix As Integer)
        If rgba Is Nothing OrElse npix <= 0 OrElse rgba.Length < npix * 4 Then Return
        ' Por-píxel puro, escrituras disjuntas ⇒ bit-idéntico al serial (misma justificación que el resto del
        ' módulo). El _msn de la cabeza puede ser 4096² con COtR.
        System.Threading.Tasks.Parallel.ForEach(
            System.Collections.Concurrent.Partitioner.Create(0, npix),
            Sub(range)
                For i = range.Item1 To range.Item2 - 1
                    Dim x = 2.0 * rgba(i * 4) - 1.0
                    Dim y = 2.0 * rgba(i * 4 + 1) - 1.0
                    Dim zz = 1.0 - x * x - y * y
                    Dim z = If(zz > 0.0, Math.Sqrt(zz), 0.0)
                    rgba(i * 4 + 2) = CSng((z + 1.0) * 0.5)
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
        Dim c00 = t.Unit((y0 * w + x0) * 4 + ch)
        Dim c10 = t.Unit((y0 * w + x1) * 4 + ch)
        Dim c01 = t.Unit((y1 * w + x0) * 4 + ch)
        Dim c11 = t.Unit((y1 * w + x1) * 4 + ch)
        Return c00 * (1 - tx) * (1 - ty) + c10 * tx * (1 - ty) + c01 * (1 - tx) * ty + c11 * tx * ty
    End Function

    ''' <summary>Resample un BGRA (byte, sw*sh*4) a (dw,dh) con EL MISMO filtro que el compositor FO4:
    ''' GL_LINEAR + CLAMP_TO_EDGE, texel = uv*size-0.5, pixel-center u=(x+0.5)/dw (idéntico a <see cref="SampleBilinear"/>
    ''' / <c>SampleChannelAt</c>). Para que el resize del bake SSE (diffuse/normal plegados a una resolución != Inherit)
    ''' matchee el resample per-layer de FO4. sw==dw AndAlso sh==dh ⇒ devuelve el mismo array (no-op, byte-inerte).</summary>
    ''' <summary>⭐ Gemelo FLOAT de <see cref="ResampleBgra"/>: MISMO filtro (GL_LINEAR + CLAMP_TO_EDGE,
    ''' texel = uv*size-0.5, pixel-center u=(x+0.5)/dw) sobre un acumulador <c>Single()</c> RGBA, SIN pasar por
    ''' bytes.
    '''
    ''' <para>Existe para que el RENDER pueda honrar la resolución de CharGen Options igual que el bake. El bake
    ''' resamplea el buffer ya convertido a BGRA (es lo que va a un DDS de 8 bits); el render trabaja en float de
    ''' punta a punta y NO debe cuantizar en el medio — es la misma regla que ya rige acá: la pérdida de 8 bits y
    ''' de BCn es del ARCHIVO, no del COMPOSE. Con el filtro idéntico, render y bake dan el mismo píxel salvo esa
    ''' cuantización final que sólo paga el archivo.</para>
    '''
    ''' <para>⚠️ Se resamplea SOBRE LOS MISMOS VALORES que el bake, o sea en el espacio en que esté el buffer
    ''' (para el fold SSE: sRGB, ANTES del sRGB→lineal final). Bilinear en sRGB ≠ bilinear en lineal, así que el
    ''' punto de la cadena donde se llama es parte del contrato — ver el call site del fold.</para>
    ''' <para>sw==dw AndAlso sh==dh ⇒ devuelve el MISMO array (no-op, bit-inerte).</para></summary>
    Public Function ResampleRgbaFloat(src As Single(), sw As Integer, sh As Integer, dw As Integer, dh As Integer) As Single()
        If src Is Nothing OrElse sw <= 0 OrElse sh <= 0 OrElse dw <= 0 OrElse dh <= 0 Then Return src
        If sw = dw AndAlso sh = dh Then Return src
        Dim outp(dw * dh * 4 - 1) As Single
        System.Threading.Tasks.Parallel.For(0, dh, Sub(y)
                                                       Dim v = (y + 0.5) / dh
                                                       Dim fy = Clamp01(v) * sh - 0.5
                                                       Dim iy = CInt(Math.Floor(fy)) : Dim ty = fy - iy
                                                       Dim y0 = Math.Max(0, Math.Min(sh - 1, iy)), y1 = Math.Max(0, Math.Min(sh - 1, iy + 1))
                                                       For x = 0 To dw - 1
                                                           Dim u = (x + 0.5) / dw
                                                           Dim fx = Clamp01(u) * sw - 0.5
                                                           Dim ix = CInt(Math.Floor(fx)) : Dim tx = fx - ix
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
                                                       Dim v = (y + 0.5) / dh
                                                       Dim fy = Clamp01(v) * sh - 0.5
                                                       Dim iy = CInt(Math.Floor(fy)) : Dim ty = fy - iy
                                                       Dim y0 = Math.Max(0, Math.Min(sh - 1, iy)), y1 = Math.Max(0, Math.Min(sh - 1, iy + 1))
                                                       For x = 0 To dw - 1
                                                           Dim u = (x + 0.5) / dw
                                                           Dim fx = Clamp01(u) * sw - 0.5
                                                           Dim ix = CInt(Math.Floor(fx)) : Dim tx = fx - ix
                                                           Dim x0 = Math.Max(0, Math.Min(sw - 1, ix)), x1 = Math.Max(0, Math.Min(sw - 1, ix + 1))
                                                           Dim i00 = (y0 * sw + x0) * 4, i10 = (y0 * sw + x1) * 4, i01 = (y1 * sw + x0) * 4, i11 = (y1 * sw + x1) * 4
                                                           Dim o = (y * dw + x) * 4
                                                           For ch = 0 To 3
                                                               Dim c = bgra(i00 + ch) * (1 - tx) * (1 - ty) + bgra(i10 + ch) * tx * (1 - ty) +
                                                                       bgra(i01 + ch) * (1 - tx) * ty + bgra(i11 + ch) * tx * ty
                                                               outp(o + ch) = CByte(Math.Max(0, Math.Min(255.0, Math.Round(c, MidpointRounding.ToEven))))
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
    Private Function SampleLutEngine(t As DecodedTex, green01 As Double, v01 As Double, ch As Integer, srcSpace As Integer, coordSpace As Integer) As Double
        Dim u As Double = Cvt1(green01, srcSpace, coordSpace)
        Dim tx As Integer = Math.Max(0, Math.Min(t.Width - 1, CInt(Math.Floor(u * t.Width))))
        Dim ty As Integer = Math.Max(0, Math.Min(t.Height - 1, CInt(Math.Floor(v01 * t.Height))))
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

    ''' <summary>Cache de decode PERSISTENTE entre bakes — para el BATCH. Cuando esta activo (Begin/End
    ''' alrededor del loop de clones), ComposeCpuPipeline lo usa en vez del dict per-call: las texturas
    ''' source (face d/_n/_s) + tint + swap se REPITEN entre clones, asi que cada DDS se decodifica UNA
    ''' sola vez en todo el batch (el path GPU ya hacia esto via TintGpuCache). Sin esto, cada clon
    ''' re-decodifica las ~49 texturas via DirectXTex. Nothing = comportamiento per-cara (1 bake aislado).
    ''' ⭐ CONCURRENTDICTIONARY. La nota vieja decia "los bakes del batch son SECUENCIALES (un await a la vez)
    ''' -> Dictionary plano alcanza; si se paraleliza el loop de clones, cambiar a ConcurrentDictionary". Es cierto
    ''' ENTRE BAKES, pero el segundo hilo no es otro bake: es el RENDER. El batch corre cada bake con
    ''' `Await Task.Run(...)` (release, WriteGPUSandboxOutput=False) y durante ese await la bomba de mensajes de
    ''' WinForms SIGUE VIVA -> un WM_PAINT entra al render EN EL HILO UI y llega a este mismo cache mientras el
    ''' bake escribe desde el ThreadPool. Un Dictionary en escritura concurrente no da "un valor raro": puede
    ''' colgar el proceso en un bucle infinito dentro de Insert() al rehashear. El patron de uso (TryGetValue +
    ''' indexer set) es identico, asi que no cambia ni la logica ni el resultado. Mismo motivo que los caches de
    ''' SseFaceTintComposer.</summary>
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
    ''' comportamiento historico, asi que este agregado no cambia nada si nadie lo setea.
    '''
    ''' <para><b>Por que hace falta.</b> El cache no tenia NINGUN limite: crecia durante todo el bake
    ''' (`BeginBatchDecodeCache` … `EndBatchDecodeCache`, o sea miles de NPCs). Medido en el barrido FO4
    ''' completo: working set pico ~9,5 GB. En una maquina con menos RAM eso es paginacion o muerte por
    ''' agotamiento — que ya paso antes en este arnes.</para>
    '''
    ''' <para><b>Por que ADMISION y no evicción.</b> Evictar una entrada GARANTIZA que se re-decodifique
    ''' cuando el proximo NPC la pida; no admitirla solo lo ARRIESGA. Con claves que son rutas de textura
    ''' SOURCE compartidas entre NPCs, las primeras que entran son justamente las mas reusadas (las bases
    ''' de cabeza), asi que quedarse con las primeras es mejor politica que LRU.</para>
    '''
    ''' <para><b>Por que no puede cambiar la salida.</b> El valor cacheado es funcion PURA de
    ''' (bytes de la textura, tamaño destino) — es lo que devuelve <c>DecodeDds</c>. No cachear no altera
    ''' el valor, solo lo recalcula. La salida es byte-identica por construccion; lo unico que se mueve es
    ''' el tiempo.</para></summary>
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

    ''' <summary>Compone los 3 canales por CPU (espejo de FaceTintCompositor.ApplyFaceTintPipeline).
    ''' Trabaja sobre las DDS YA LEÍDAS de los inputs. Devuelve BGRA byte por canal (D g22 / N/S lineal).
    ''' MISMA ley que el GL (resolver + math de arriba). Sin GL: pura CPU.</summary>
    ''' <param name="resolution">Resolución por canal (A/B/C). Nothing/default = Inherit (nativo) en los 3
    ''' = comportamiento gen3. Bodyparts: pasar Nothing (fuerzan heredar; el enum es solo para la cara).</param>
    ''' <param name="diffuseKey">Keys de las texturas source (path estable) para cachear su decode entre
    ''' clones cuando BatchDecodeCache esta activo. Nothing = no cachear el source (se decodifica directo).</param>
    ''' <param name="decodeCache">⭐ Cache de decode PROPIEDAD DEL CALLER, con la vida que el caller decida.
    ''' Es el equivalente CPU del <c>TintGpuCache</c> per-host del camino GL: el RENDER en modo CPU recompone la
    ''' cara entera en cada refresh de edicion viva, y sin esto cada refresh vuelve a decodificar por DirectXTex
    ''' TODAS las DDS (source D/N/S + cada capa + cada mascara de swap) que el camino GPU ya tenia residentes.
    ''' Tiene PRIORIDAD sobre <see cref="BatchDecodeCache"/> — se pasa explicito justamente para no pisar el
    ''' global del batch, que puede estar corriendo en otro hilo. Nothing = comportamiento previo.
    ''' No puede cambiar la salida: el valor cacheado es funcion PURA de (bytes, tamaño destino).</param>
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

    ''' <summary>Compone UN canal. seed (D=g22(src), N/S=src) -> region swaps (crossfade en linear) ->
    ''' tint layers (over-running, ley del resolver). Espejo de ComposeOntoFaceTexture + el seed +
    ''' ApplyRegionSwapsOntoFaceTexture del GL.</summary>
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
        ' Acumulador ALPHA: PASSTHROUGH del base, nunca compuesto. Las blend-ops son RGB-only por
        ' definicion (mismo contrato que documenta el shader GL), asi que el alpha del base viaja
        ' intacto hasta el pack. MEDIDO sobre el corpus: el _d que hornea el CK lleva EXACTAMENTE el
        ' alpha del head diffuse de origen (Valentine 0x00002F24 vs gen2skinheadvalentine_d.dds:
        ' RMS 0,229/255, 99,59% byte-exact, maxD 18 en 0,02% de px = ruido de bloque BC3). Antes esta
        ' funcion escribia alpha=255 fija y el canal se perdia. Inerte para N/S (BC5 no tiene alpha).
        ' ⭐ El acumulador ALPHA sólo se consume en el pack de más abajo, y SÓLO bajo `keepBaseAlpha`
        ' (= headDiffuseAlphaTest AndAlso isD); si no, ese pack escribe 255 fijo. Los dos términos son
        ' parámetros de esta función, así que la condición se conoce ACÁ, antes de reservar nada. Para N/S
        ' es muerto SIEMPRE (isD=False), y para un diffuse sin el flag ACBS también. Antes se reservaba el
        ' array y se sampleaba el canal 3 por píxel en los tres canales, pasara lo que pasara: a 4096² son
        ' 67 MB y una pasada completa por canal tirados. Mismo valor empaquetado, mismo byte de salida.
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
                    Dim r0 As Double, g0 As Double, b0 As Double
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
                Dim msdv As Double = CDbl(FaceTintConvention.ClampSwapIntensity(sw.Intensity))   ' ley UNICA compartida con el GL
                ' Swap = replace resuelto por la MISMA tabla que los tints (forSwap:=True) -> sin convención
                ' hardcodeada; el override (incl. #If DEBUG full-linear) alcanza también los swaps. NON-DEBUG
                ' byte-idéntico al closed-form previo (cov=srgbenc(mask), D lerp linear-desde-srgb, N/S raw).
                Dim cv = FaceTintConvention.ResolveConvention(False, 0US, 0, channel, False, forBake:=True, forSwap:=True)
                Dim sws = CInt(cv.WorkingSpace), scs = CInt(cv.CompositeSpace), sss = CInt(cv.SrcSpace), sos = CInt(cv.OutputSpace)
                ' ⛔⛔ `sos` (Swap.OutputSpace) ES UN ARGUMENTO MUERTO ACA, a proposito y declarado.
                ' `ComposeOne` lo usa SOLO para derivar `asp` (`asp = If(accSpace < 0, os, accSpace)`); como abajo
                ' se pasa `accSpace:=accSp` explicito, `os` no se lee en NINGUNA de las 4 ramas de framework.
                ' Se sigue pasando por simetria con los otros call sites, no porque haga algo.
                ' CAMBIO DE COMPORTAMIENTO DECLARADO: antes (sin accSpace) el acumulador del swap se trataba como
                ' si viviera en `sos`, y el de los tints en el del CANAL — dos etiquetas para EL MISMO buffer.
                ' Ahora manda el canal. Con los defaults de fabrica de los dos juegos es byte-identico
                ' (FO4 Swap.OutputSpace = Diffuse.OutputSpace = G22; SSE todo Linear), pero NO si el usuario
                ' separa esos combos en CharGen Options. El GL hace lo MISMO (ver el guard gemelo alla), asi que
                ' la paridad CPU/GPU se mantiene; lo que cambia es la salida respecto de la version previa.
                ' ⛔ La advertencia va por FaceTintConvention (latcheada + always-on), NO por Logger: `Logger`
                ' esta APAGADO en release, asi que un aviso por ahi no existiria justo para el usuario que
                ' necesita verlo. El runner lo imprime en el resumen. Latcheado ⇒ no spamea por swap ni por NPC.
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
                            Dim sr As Double, sg As Double, sb As Double, mask As Double
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

        ' base = SNAPSHOT del acc POST-swaps. Paridad con el GL: el pase de tints (ComposeOntoFaceTexture,
        ' línea ~2168) recibe como input la textura YA swapeada del pre-pass (FaceTintCompositor:2160-2170), y
        ' su uBase = ese input -> uBase del GL es post-swap. Se captura acá (después de los region swaps) para
        ' que los frameworks base-relativos (OverBase/AddBase) compongan sobre el baseline young-morpheado, NO
        ' sobre el seed Hero pre-swap. OverPrev (default) NO usa base -> byte-idéntico al modelo previo.
        ' ⭐ ¿HACE FALTA el snapshot? SÓLO lo leen los frameworks OverBase(1) y AddBase(2) dentro de
        ' ComposeOne; OverPrev(0 — el DEFAULT) y ModSrc(3) no tocan `base` en ninguna de sus ramas. Con la
        ' config default esto era trabajo puro perdido en CADA canal de CADA NPC: tres arrays + tres
        ' Array.Copy de n elementos (a 4096², 201 MB de LOH y tres pasadas completas de memoria por canal).
        ' Tampoco lo usan los region-swaps ni el pre-tono TakesSkinTone: sus ComposeOne no pasan
        ' base/framework, así que caen a los Optional (0.0, OverPrev).
        '
        ' ⛔ El pre-scan enumera el ESPACIO DE PARÁMETROS COMPLETO que el loop de capas puede pasarle a
        ' ResolveConvention: los (IsTextureSet, Slot, BlendOp) reales de cada capa × LOS DOS valores de
        ' useHairPalette (que ahí depende del decode de la LUT, no resuelto todavía acá). Con eso el guard
        ' sigue siendo correcto aunque alguien haga que Framework dependa de cualquiera de esas entradas.
        ' Hoy no depende de ninguna (ResolveConvention: Framework = bucket.Framework, y el bucket se elige
        ' sólo por (canal, forSwap)), así que en la práctica el barrido converge en la primera capa.
        '
        ' ⛔ Es CONSERVADOR a propósito: NO replica los `Continue For` del loop real (capa sin bytes para
        ' este canal, o cuya textura no decodifica). Como mucho reserva el snapshot de más — nunca de menos,
        ' que es el único error que cambiaría un byte.
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
            Dim stColR As Double = 0, stColG As Double = 0, stColB As Double = 0, stOpac As Double = 0
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
                    stColR = sLayer.R / 255.0 : stColG = sLayer.G / 255.0 : stColB = sLayer.B / 255.0
                    stOpac = Math.Max(0.0, Math.Min(1.0, CDbl(sLayer.Opacity)))
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
                Dim op = Math.Max(0.0, Math.Min(1.0, CDbl(layer.Opacity)))
                Dim uColR = layer.R / 255.0, uColG = layer.G / 255.0, uColB = layer.B / 255.0
                Dim row = Math.Max(0.0, Math.Min(1.0, CDbl(layer.HairPaletteRow)))
                ' brow grayscale->palette LUT lookup = ENGINE-EXACT (BSFaceCustomizationShader PS, `ld` t4):
                ' el mask (verde) se decodea sRGB->linear (t1 = SRV sRGB), U=pow(lin,1/2.2), texel=ftoi(U*W,
                ' row*H), fetch NEAREST (ld; sin bilineal ni half-texel). Verificado byte-exact vs CK (resid 0.4).
                ' El verde crudo (lg) se pasa a SampleLutEngine, que hace sRGB-decode + pow + ftoi + nearest.
                Dim luY As Double = row
                Dim kind = layer.Kind
                ' GUARD del pre-tono TakesSkinTone: solo D, capa flagged, y skintone ya compuesto antes.
                ' Pre-tono si: capa flagged (D) Y hay skintone Y (ya se compuso antes -> over-running tona
                ' las de antes desde arriba, las de despues necesitan source-pretono) O el framework no acumula
                ' (OverBase/AddBase -> el skintone NO llega por el base, hay que pre-tonar TODA flagged).
                Dim preToneSkin As Boolean = (isD AndAlso layer.TakesSkinTone AndAlso skintoneFound AndAlso (stSeen OrElse nonAccum))

                ' Paralelo POR RANGOS — ver la nota del seed. Es el loop MAS PESADO de los tres (corre una
                ' vez por CAPA, y su cuerpo hace varios Math.Pow por canal via ComposeOne), y era el que
                ' pagaba un delegate por pixel. Cuerpo per-pixel con escrituras disjuntas ⇒ bit-idéntico.
                ' ⭐ INVARIANTE IZADO (ver SampleChannelAt): "¿el tex mide lo mismo que el acumulador?" NO
                ' depende del pixel, pero se evaluaba DENTRO de SampleChannelAt, o sea 4 veces por pixel y
                ' por CAPA — a 1024² con ~18 capas son >70M ramas + llamadas anidadas de puro overhead. Se
                ' resuelve una vez por capa y el caso directo lee los 4 canales desde UN indice base.
                ' ⛔ BIT-IDENTICO por construccion: es el MISMO valor por el MISMO camino (ByteToUnit sobre
                ' el byte crudo, widening a Double al asignar) — lo unico que desaparece es la re-decision.
                ' Los tipos van EXPLICITOS en Double: si quedaran inferidos como Single, la aritmetica de
                ' abajo cambiaria de precision y la salida dejaria de ser identica.
                Dim layerDirect As Boolean = (layerTex.Width = w AndAlso layerTex.Height = h)
                Dim layerPx As Byte() = If(layerDirect, layerTex.Rgba8, Nothing)
                Dim lut = ByteToUnit
                System.Threading.Tasks.Parallel.ForEach(
                    System.Collections.Concurrent.Partitioner.Create(0, n),
                    Sub(range)
                        For i As Integer = range.Item1 To range.Item2 - 1
                        Dim lr As Double, lg As Double, lb As Double, la As Double
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
                        Dim maskV As Double
                        Dim srcR As Double, srcG As Double, srcB As Double
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
                        Dim bR As Double = 0.0, bG As Double = 0.0, bB As Double = 0.0
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

        ' --- Pack a BGRA byte (clamp+round). D ya está en g22, N/S lineal. ---
        ' ALPHA del _d (corregido 2026-07-20): passthrough del alpha del base SÓLO si la cabeza usa Diffuse Alpha
        ' Test (flag ACBS 0x01000000); si no, OPACO (255). El CK aplana el alpha del _d salvo cuando el head
        ' material lo testea. Valentine (flag SET) → passthrough (transparencia); DiMA (CLEAR, RACE.DFTM apunta a
        ' SkinHeadValentine con alpha en la textura, pero su material NO la testea) → opaco, como el CK.
        ' Antes: passthrough incondicional inventaba el alpha de DiMA (medición: DLC03DiMA _d ALPHA varía=True vs
        ' CK=False). Inerte para N/S (BC5 sin alpha). Ver reference_acbs_diffuse_alpha_test_flag.
        ' ⭐ CONVERSION FINAL accSp -> OutputSpace, UNA sola vez para todo el canal. Con el default
        ' (accSp == OutputSpace) `Cvt1` cortocircuita y esto es un no-op exacto: el pack ve los MISMOS doubles
        ' que antes. Con el acumulador en CompositeSpace, este es el UNICO lugar donde se paga la conversion de
        ' salida, en vez de pagarla ida-y-vuelta en CADA capa (que es de donde salen los Math.Pow del 94,9 %).
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
    ''' la cobertura <paramref name="cov"/> ya resuelta (mask x opacidad), usando la convencion
    ''' <paramref name="conv"/> (working/composite/src/output/ACCUM spaces, blend op, softlight, framework). Es
    ''' el MISMO <see cref="ComposeOne"/> que usa el loop FO4 — expuesto para que otros compositores (SSE)
    ''' compongan por la ley del config en vez de hardcodear el algebra. GL == CPU == este por construccion.
    ''' <para>Honra <c>conv.AccumSpace</c>: el acumulador que recibe y devuelve vive en AccumSpace, no en
    ''' OutputSpace. Antes no lo pasaba, asi que el acumulador se trataba SIEMPRE como OutputSpace y cada capa
    ''' pagaba el ida-y-vuelta — es decir, el unico caller (SSE) no podia honrar AccumInCompositeSpace ni
    ''' aunque el config lo prendiera, y habia que suprimir el flag del lado GL para que los dos no
    ''' divergieran. El caller es responsable de sembrar EN AccumSpace y de hacer la unica conversion final a
    ''' OutputSpace.</para>
    ''' <paramref name="base"/> solo lo usan los frameworks OverBase/AddBase; en OverPrev (default) es inerte.</summary>
    Public Function ComposePixel(prev As Double, src As Double, cov As Double,
                                 conv As FaceTintConvention.FaceTintConventionSet,
                                 Optional base As Double = 0.0) As Double
        Return ComposeOne(prev, src, cov,
                          CInt(conv.WorkingSpace), CInt(conv.CompositeSpace), CInt(conv.SrcSpace),
                          CInt(conv.OutputSpace), CInt(conv.Blend), CInt(conv.SoftLight),
                          base, CInt(conv.Framework), accSpace:=CInt(conv.AccumSpace))
    End Function

    ''' <summary>Conversion de espacio expuesta, para que un compositor que mantiene su propio acumulador
    ''' (SSE) siembre en AccumSpace y haga la conversion final con EXACTAMENTE la misma funcion que usa el
    ''' compose. Mismo criterio que <see cref="ConvMaskShared"/>.</summary>
    Public Function ConvertSpaceShared(v As Double, fromSpace As Integer, toSpace As Integer) As Double
        Return Cvt1(v, fromSpace, toSpace)
    End Function

    ''' <summary>mask conv expuesta (0=raw 1=srgbEnc 2=srgbDec 3=g22Enc 4=g22Dec…) para que los compositores
    ''' que resuelven su propia cobertura (SSE) apliquen la MISMA transformación de máscara que el loop FO4.</summary>
    Public Function ConvMaskShared(m As Double, maskConv As Integer) As Double
        Return ConvMask1(m, maskConv)
    End Function

    ''' <param name="accSpace">Espacio en el que VIVE el acumulador (`prev` y `base`) y en el que se devuelve el
    ''' resultado. −1 (default) = usar <paramref name="os"/> ⇒ comportamiento previo EXACTO, y ningun call site
    ''' existente cambia. Ver FaceTintConventionSet.AccumSpace: con accSpace = cs, las conversiones del
    ''' acumulador por capa (os→ws, os→cs, cs→os) colapsan a identidad y desaparecen sus Math.Pow.</param>
    Private Function ComposeOne(prev As Double, src As Double, cov As Double,
                                ws As Integer, cs As Integer, ss As Integer, os As Integer, bop As Integer,
                                softLight As Integer,
                                Optional base As Double = 0.0, Optional framework As Integer = 0,
                                Optional accSpace As Integer = -1) As Double
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
    Private Function SampleChannelAt(t As DecodedTex, accIdx As Integer, accW As Integer, accH As Integer, ch As Integer) As Double
        If t.Width = accW AndAlso t.Height = accH Then
            Return t.Unit(accIdx * 4 + ch)
        End If
        Dim x = accIdx Mod accW, y = accIdx \ accW
        Dim u = (x + 0.5) / accW, v = (y + 0.5) / accH
        Return SampleBilinear(t, u, v, ch)
    End Function

    Private Function ToByte(c As Double) As Byte
        ' np.rint de gen3 = round-half-to-EVEN (banker's) = MidpointRounding.ToEven (default de Math.Round).
        ' El redondeo a byte se hace SOLO al final (los acumuladores quedan float toda la pasada), igual
        ' que gen3 (rint solo en el write). Asi CPU == `_3` byte-exacto.
        ' Guard NaN: Clamp01 NO atrapa NaN (Math.Min/Max con NaN devuelve NaN) y CByte(NaN) tira
        ' OverflowException. ±Infinity SI lo clampa Clamp01. NaN -> 0 (defensivo; no cambia ningún byte
        ' válido -> la paridad byte-exacta con _3 se preserva, sólo evita el crash si un blend/framework NaN-ea).
        If Double.IsNaN(c) Then c = 0.0
        Dim v = Math.Round(Clamp01(c) * 255.0, MidpointRounding.ToEven)
        If v < 0 Then v = 0
        If v > 255 Then v = 255
        Return CByte(v)
    End Function

End Module
