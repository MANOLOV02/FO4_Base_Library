Option Strict On
Option Explicit On

Imports System.Runtime.CompilerServices
Imports System.Runtime.Intrinsics

' =================================================================================================
' Las primitivas aritméticas del motor de tela de Havok, en un solo lugar.
'
' ⛔⛔ POR QUÉ ESTE ARCHIVO EXISTE Y POR QUÉ NADIE MÁS PUEDE HACER ESTA CUENTA
'
' El motor de Havok normaliza de TRES maneras distintas, y la diferencia entre ellas es MEDIBLE:
'
'   `rsqrtps` crudo, sin refinar      error <= 1,5·2^-12 = 3,662e-4   (máximo medido: 3,261e-4)
'   `rsqrtps` + UNA iteración Newton  error ~ 2,626e-7
'   `sqrtss`/`divss` exactos          error de redondeo
'
' Son tres órdenes de magnitud entre la primera y la segunda. Qué sitio usa cuál NO es un detalle:
' es parte de la ley, y costó leerlo del `.exe` uno por uno. Por eso los tres nombres viven acá, con
' la dirección de los sitios que los justifican, y el resto de `Havok/Motor/` NO puede nombrar
' `Sqrt`, `Pow`, `^` ni `ReciprocalSqrt` — eso lo comprueba el gate G19a sobre el fuente.
'
' ⛔ DECISIÓN DEL USUARIO (06-sep, reafirmada el 14-sep): SIN `System.Runtime.Intrinsics.X86.Sse`.
' `FastPow.vb:17` declara la ley del repo — «Solo la API CROSS-PLATFORM `Vector256`/`Vector128`,
' JAMÁS `Avx.*`/`Sse.*`» — y el usuario eligió respetarla. Consecuencia, escrita y no escondida:
'
'   · La ESTIMACIÓN de `rsqrtps`/`rcpps` se reemplaza por el valor exacto (`1 / Vector128.Sqrt(x)`,
'     `1 / x`). `RsqrtCrudo` devuelve eso; `RsqrtNewton`/`RcpNewton` le aplican encima la Newton
'     literal del motor. Es exactamente lo que hace un EMULADOR (QEMU `helper_rsqrtps`), NO lo que
'     hace el procesador.
'   · Medido en la CPU real (i7-8700K, 14-sep, ejecutando la instrucción nativa): `rsqrtps(4)` da
'     0,49987793 y el exacto 0,5; `rsqrtps(2)` 0,70690918 contra 0,70710677. El juego en ese
'     procesador usa la estimación.
'   · Por eso los diferenciales GDF contra unicorn prueban la TRANSCRIPCIÓN (orden de operaciones,
'     ramas, NaN) y NO la igualdad de bits con el juego en los sitios `rsqrtps`/`rcpps`. La
'     diferencia contra la CPU real se mide aparte (fixtures de `Diferencial\cpu\`).
'   · Eso NO se amplifica en el solve (es contractivo: k <= 1, damping < 1), pero SÍ mueve el
'     equilibrio de cada enlace a `|d| = restLength/(1+eps)`: hasta 3,7e-4 relativo, que sobre una
'     prenda de 100 unidades son 0,04 u.
'   · Los sitios donde el motor divide EXACTO (`divss`) quedan bit-idénticos.
'
' ⭐ LOS DOS NOMBRES SE QUEDAN AUNQUE HOY HAGAN LO MISMO. Cuesta cero y preserva el mapa
' `sitio del motor -> clase de aproximación`, que es lo que costó leer. Si algún día se revierte la
' decisión, son dos cuerpos, no una relectura del RE.
'
' ⚠️ Y lo que NO se promete: igualdad bit a bit con el juego. `RSQRTPS` no está garantizado idéntico
' entre fabricantes, `MathF.Pow/Sin/Cos` no da los mismos bits que el CRT de MSVC, y el juego corre
' con FTZ/DAZ en `MXCSR` (Havok lo exige) mientras .NET no. La promesa es misma ley, mismo orden de
' operaciones, y la cota escrita.
'
' Referencia: Tools/re-docs/RE_MOTOR_FISICA_CANONICO_2026-09-05.md, cap. 10.3 y Anexo C.
' =================================================================================================


Namespace Havok.Motor

    ''' <summary>
    ''' ⭐ Contadores por **clase de aproximación** — el instrumento que mide el mapa
    ''' `sitio del motor → clase`, que es lo que costó leer del `.exe`.
    ''' <para>⛔ Hace falta porque por el NÚMERO no se pueden distinguir: la decisión del usuario
    ''' de no usar `Sse.*` y la ausencia de `ReciprocalSqrtEstimate` en .NET 8 hacen que
    ''' <see cref="Simd.RsqrtCrudo"/> y <see cref="Simd.RsqrtNewton"/> compartan cuerpo hoy
    ''' (lo mide `GS1i`). Un gate de FUENTE que sólo exija que los nombres aparezcan custodia la
    ''' presencia, no el mapeo: con él en verde se pueden intercambiar las dos ramas del cono y
    ''' nadie se entera (motor-60). Un contador sí es un entero que se mide.</para>
    ''' <para>El costo es un `+= 1` por llamada, en un motor que ya está limitado a Debug y que
    ''' paga una división exacta donde el juego usa `rsqrtps`. Es ruido al lado de eso.</para>
    ''' </summary>
    Friend Module CuentasDeSimd

        Friend RsqrtCrudo As Long
        Friend RsqrtNewton As Long
        Friend RcpNewton As Long
        Friend DivExacta As Long
        Friend SqrtExacta As Long
        Friend PowCrt As Long
        ''' <summary>`rcpps` CRUDO, sin Newton y sin guarda — la rama `scaleNormalBehaviour = 2` de
        ''' `hclMeshMeshDeformOperator` (`0x141953819`). Es una CLASE distinta de
        ''' <see cref="RcpNewton"/> y por eso lleva su propio contador.</summary>
        Friend RcpCrudo As Long

        ''' <summary>Pone las siete en cero. El arnés lo llama antes de cada caso.</summary>
        Friend Sub Cerar()
            RsqrtCrudo = 0 : RsqrtNewton = 0 : RcpNewton = 0
            DivExacta = 0 : SqrtExacta = 0 : PowCrt = 0 : RcpCrudo = 0
        End Sub

        ''' <summary>Las siete, en el orden de los campos, para compararlas de un saque.
        ''' <para>⛔ `RcpCrudo` va AL FINAL a propósito: los gates indexan por posición
        ''' (`cu(0)`, `cu(1)`, `cu(2)`), así que agregar al final no corre ningún índice vivo.</para></summary>
        Friend Function Instantanea() As Long()
            Return New Long() {RsqrtCrudo, RsqrtNewton, RcpNewton, DivExacta, SqrtExacta, PowCrt, RcpCrudo}
        End Function

    End Module

    Friend Module Simd

        ''' <summary>Las tres lanes de un `hkVector4` que participan de un producto punto de 3
        ''' componentes. La `w` se descarta.</summary>
        Friend Const LaneX As Integer = 0
        Friend Const LaneY As Integer = 1
        Friend Const LaneZ As Integer = 2
        Friend Const LaneW As Integer = 3

        ''' <summary>
        ''' `rsqrtps` CRUDO, sin refinar.
        ''' <para>Sitios del motor que lo usan: el enlace estándar (`0x141A06170`, un solo `rsqrtps`
        ''' y ninguna constante 3,0/0,5 en toda la función), la dirección del heightfield
        ''' (`0x141A00E65`) y la normalización del marco de contacto.</para>
        ''' <para>⛔ Refinarlo en esos sitios CAMBIA el resultado del motor. El valor es el exacto, no
        ''' la estimación del procesador (decisión de no usar `Sse.*`, ver el encabezado).</para>
        ''' </summary>
        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Friend Function RsqrtCrudo(x As Vector128(Of Single)) As Vector128(Of Single)
            CuentasDeSimd.RsqrtCrudo += 1L                    ' motor-60, el instrumento del mapa
            Return Vector128.Divide(Vector128.Create(1.0F), Vector128.Sqrt(x))
        End Function

        ''' <summary>
        ''' `rsqrtps` + UNA iteración de Newton: `(3 - x·r²)·r·0,5`.
        ''' <para>Constantes del motor: `3,0` en `0x142629510` y `0,5` en `0x142629520`.</para>
        ''' <para>Sitios: la normalización del cuaternión del colisionable (`0x14195CADD`), el
        ''' centroide de `convexGeometry` (`0x141A004C0`), la dirección del viento
        ''' (`0x1418F8369`), la fase 3 del terreno (`0x141A14549`), y la cola del eigensolver
        ''' (`0x141361096`).</para>
        ''' </summary>
        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Friend Function RsqrtNewton(x As Vector128(Of Single)) As Vector128(Of Single)
            CuentasDeSimd.RsqrtNewton += 1L                    ' motor-60, el instrumento del mapa
            ' ⛔ LA NEWTON SE HACE, instrucción por instrucción: `r = rsqrtps(x)` (evaluado exacto, que
            ' es la semántica con la que se emula el `.exe` — QEMU `helper_rsqrtps` = 1/sqrt) y después
            ' `(3 − (x·r)·r)·(r·0,5)`. Devolver `1/sqrt(x)` a secas difiere en ulps y en inf/NaN:
            ' con x = 0 el motor da NaN (0·inf), no +inf. Lo cazó el diferencial GDF.
            Dim r = Vector128.Divide(Vector128.Create(1.0F), Vector128.Sqrt(x))
            Dim t = Vector128.Multiply(Vector128.Multiply(x, r), r)
            Return Vector128.Multiply(Vector128.Subtract(Vector128.Create(3.0F), t), Vector128.Multiply(r, Vector128.Create(0.5F)))
        End Function

        ''' <summary>
        ''' `rcpps` + UNA iteración de Newton: `(2 - x·r)·r`. Constante `2,0` en `0x142629500`.
        ''' <para>Sitios: la escala del heightfield (`0x141A00E68`) y el determinante de la inversa
        ''' 3×3 (`0x14136012B`).</para>
        ''' </summary>
        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Friend Function RcpNewton(x As Vector128(Of Single)) As Vector128(Of Single)
            CuentasDeSimd.RcpNewton += 1L                    ' motor-60, el instrumento del mapa
            ' ⛔ `r = rcpps(x)` (exacto, como la emulación) y la Newton literal `(2 − x·r)·r`. Con x
            ' subnormal `r = +inf` y la Newton lo vuelve −inf; `1/x` a secas daba +inf (GDFf2,
            ' `0x1419F9E73`-`0x1419F9E9E`).
            Dim r = Vector128.Divide(Vector128.Create(1.0F), x)
            Return Vector128.Multiply(Vector128.Subtract(Vector128.Create(2.0F), Vector128.Multiply(x, r)), r)
        End Function

        ''' <summary>
        ''' `rcpps` **crudo**: sin Newton y **sin guarda**.
        ''' <para>Sitio: la rama `scaleNormalBehaviour = 2` del marco de triángulo de
        ''' `hclMeshMeshDeformOperator` (`0x141953819 rcpps` + `0x14195381C mulps`), que escala la
        ''' normal por la INVERSA del área.</para>
        ''' <para>⛔⛔ Ahí **no hay `cmpleps`/`andnps`**, a diferencia de
        ''' <see cref="RsqrtConGuarda"/>: con un triángulo degenerado el motor produce `+inf` y lo
        ''' propaga. Ponerle una guarda que el motor no tiene es inventar.</para>
        ''' </summary>
        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Friend Function RcpCrudo(x As Vector128(Of Single)) As Vector128(Of Single)
            CuentasDeSimd.RcpCrudo += 1L                     ' motor-60, el instrumento del mapa
            Return Vector128.Divide(Vector128.Create(1.0F), x)
        End Function

        ''' <summary>
        ''' ⭐ La suma horizontal de las **CUATRO** lanes, `(x + z) + (y + w)`, por dos `shufps`.
        ''' <para>⛔⛔ **NO es `Dot3` más la `w` aparte.** `Dot3` asocia `((y + x) + z)` y después
        ''' sumarle `w` da `((y+x)+z)+w`; esto da `(x+z)+(y+w)`. En punto flotante son **otros
        ''' bits**, y la diferencia se ve con lanes de magnitudes distintas.</para>
        ''' <para>⭐ **UNA ley, UN lugar.** El motor la usa en tres sitios y los tres son la misma
        ''' red: el plano de `Formas` (`0x141A6E8C2` `0x4E` + `0x141A6E8CC` `0xB1`), la norma del
        ''' cuaternión de `hclSkinOperator` (`0x141909DFF` + `0x141909E10`, y `0x14191003E` +
        ''' `0x14191004F` para el dual mezclado) y la salida de planos del terreno
        ''' (`0x141A1459D` + `0x141A145A7`). Tenerla escrita tres veces era la duplicación que la
        ''' regla del workspace prohíbe.</para>
        ''' </summary>
        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Friend Function Hsum4(p As Vector128(Of Single)) As Vector128(Of Single)
            Dim s = Vector128.Add(Vector128.Shuffle(p, Vector128.Create(2, 3, 0, 1)), p)
            Return Vector128.Add(Vector128.Shuffle(s, Vector128.Create(1, 0, 3, 2)), s)
        End Function

        ''' <summary>
        ''' `cvttss2si` — trunca hacia cero, y si no entra en 32 bits devuelve el «entero
        ''' indefinido» `0x80000000`, que es lo que el x86 pone.
        ''' <para>⭐ **UNA ley, UN lugar.** Sitios: el `VC_SHORT3` de salida de `Convertir`
        ''' (`0x14195ECC0`, `0x14195ECCC`, `0x14195ECD9`), el `exp` por bits del terreno
        ''' (`0x141A15935`) y la cuantización de la broadphase (`0x141A1518B`).</para>
        ''' <para>⛔⛔ Y no es un detalle de estilo: `CInt(Double)` en VB compila a `conv.ovf.i4` y
        ''' **lanza** `OverflowException` con `NaN` o fuera de rango, justo donde el motor escribe
        ''' `0x80000000` y sigue. Ya mordió dos veces en este árbol.</para>
        ''' </summary>
        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Friend Function ATruncado(x As Single) As Integer
            ' ⛔ NO ES UN UMBRAL: es el RANGO del `cvttss2si` (`0x141A15935`, `0x141A1518B`).
            ' Fuera de [-2^31, 2^31) la instruccion devuelve el indefinido entero `0x80000000`,
            ' que es `Integer.MinValue`. La cita va aca pegada y no solo en la cabecera, para que
            ' una auditoria de umbrales inventados no lo tenga que adivinar.
            If Single.IsNaN(x) OrElse x >= 2147483648.0F OrElse x < -2147483648.0F Then
                Return Integer.MinValue
            End If
            Return CInt(Math.Truncate(CDbl(x)))
        End Function

        ''' <summary>
        ''' `FLT_EPSILON` = 1,1920929e-07. En el motor aparece en varias direcciones con el MISMO
        ''' valor: `0x142F3C760` (los acumuladores de la fase 2 de Volume), `0x142468470` (la
        ''' tolerancia del eigensolver) y `0x142F5AE70` (el que la colisión suma a `|w|²` antes de la
        ''' raíz, `0x141A7030C` y `0x141A07E00`).
        ''' </summary>
        Friend Const FltEpsilon As Single = 1.1920929E-07F

        ''' <summary>`3,40282002e+38` — `0x7F7FFFEE` en `0x142F3C740`.
        ''' <para>⛔ **No es `FLT_MAX`** (`0x7F7FFFFF` = `3,40282347e+38`): es un valor propio del
        ''' motor y se transcribe el que esta. Lo usan las AABB (`0x1418C7349`), las formas y
        ''' `hclAntiPinchConstraintSet` (`0x1419F8376`).</para></summary>
        Friend Const CasiFltMax As Single = 3.40282002E+38F

        ''' <summary>
        ''' `rsqrtps` **con UNA Newton** y con la guarda de norma nula — el patrón de la colisión:
        ''' `rsqrtps` · `cmpleps` contra cero · Newton `(3 − x·r²)·r·0,5` · `andnps`.
        ''' <para>Sitios: la esfera (`0x141A70314`-`0x141A70338`), la cápsula (`0x141A6A7D…`), y las
        ''' tres regiones del cono (`0x141A07E07`-`0x141A07E26`, `0x141A07EEA`-`0x141A07F0A`,
        ''' `0x141A07F5C`-`0x141A07F7C`).</para>
        ''' <para>⛔ Ojo con la diferencia respecto de <see cref="RsqrtConGuarda"/>: **aquí hay
        ''' Newton y allá no**. Los cuatro kernels de enlace usan la cruda; los de colisión, ésta.
        ''' No son intercambiables aunque hoy compartan cuerpo por la decisión de no usar `Sse.*`.</para>
        ''' </summary>
        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Friend Function RsqrtNewtonConGuarda(x As Vector128(Of Single)) As Vector128(Of Single)
            Dim r = RsqrtNewton(x)
            Dim nulo = Vector128.LessThanOrEqual(x, Vector128(Of Single).Zero)
            Return Vector128.AndNot(r, nulo)
        End Function

        ''' <summary>
        ''' Normaliza con `rsqrtps` **CRUDO** y guarda — `0x141A07FFE`-`0x141A0800C`, la normal del
        ''' lateral del cono. ⛔ Ahí el motor NO refina: es el único `rsqrtps` sin Newton de todo el
        ''' camino de colisión, y refinarlo cambia la normal.
        ''' </summary>
        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Friend Function NormalizarRsqrtCrudoConGuarda(v As Vector128(Of Single)) As Vector128(Of Single)
            Return Vector128.Multiply(v, RsqrtConGuarda(Dot3(v, v)))
        End Function

        ''' <summary>
        ''' `rsqrtps` CRUDO **con la guarda de norma nula** — el patrón exacto que repiten los cuatro
        ''' kernels de enlace: `rsqrtps` · `cmpleps` contra cero · `andnps`.
        ''' <para>`(x &lt;= 0) ? 0 : rsqrt(x)`. Sitios: el enlace estándar (`0x141A061E7`-`0x141A061F1`),
        ''' el de estiramiento (`0x141A06E1F`-`0x141A06E29`), el de doblez (`0x1419F9020`-`0x1419F902B`)
        ''' y el compresible (`0x1419FE8E8`-`0x1419FE8F3`).</para>
        ''' <para>⛔ La guarda es `&lt;= 0`, no `== 0`: con `len2` negativo (que no puede pasar con un
        ''' `dot3` de reales, pero el motor no lo asume) también da cero. Y el `rsqrtps` va
        ''' **crudo**: ninguno de los cuatro carga las constantes `3,0`/`0,5` de la Newton.</para>
        ''' </summary>
        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Friend Function RsqrtConGuarda(x As Vector128(Of Single)) As Vector128(Of Single)
            Dim r = RsqrtCrudo(x)                                              ' 0x141A061E7 rsqrtps
            Dim nulo = Vector128.LessThanOrEqual(x, Vector128(Of Single).Zero) ' 0x141A061ED cmpleps
            Return Vector128.AndNot(r, nulo)                                   ' 0x141A061F1 andnps
        End Function

        ''' <summary>
        ''' División EXACTA (`divss`/`divps`), que es lo que el motor hace en estos sitios y que por
        ''' lo tanto queda **bit-idéntica**:
        ''' <para>`setTransform` (`0x1419607B8`, `0x1419607FC`) · `TransferMotion` (`0x141A139E1`,
        ''' `0x141A13AE6/AEA`, `0x141A13BA9`) · `prepare` (`0x14195B83E`, `0x14195BA29`) ·
        ''' `Simulate` (`0x14195C3EF` el `dt/s1`, `0x14195C6E6` el `dt/numSubSteps`) · Transition
        ''' (`0x141A08EE1`, `0x141A08EF7`, `0x141A08F53`, `0x141A09085/97/A5`) ·
        ''' `InitializeClothJobs` (`0x141875311`, `0x141875351`) · el eigensolver (`0x141360EB8`,
        ''' `0x141360EDF`, `0x141360F0A`).</para>
        ''' <para>⛔ Reemplazar una de éstas por `RcpNewton` es un invento de ~1 ulp que además
        ''' desincroniza el `dtSub` cacheado y con él el `powf` del damping.</para>
        ''' </summary>
        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Friend Function DivExacta(a As Vector128(Of Single), b As Vector128(Of Single)) As Vector128(Of Single)
            CuentasDeSimd.DivExacta += 1L
            Return Vector128.Divide(a, b)
        End Function

        ''' <summary>
        ''' `sqrtps` de las cuatro lanes, EXACTO — `0x14135FAA2`, dentro del `asin` minimax del
        ''' ángulo del cuaternión.
        ''' <para>⛔ Va acá porque `G19a` exige que `Simd.vb` sea la **única** casa de `Sqrt` en
        ''' todo `Havok/Motor/`. Y es la raíz **exacta** de verdad: en ese sitio el motor no
        ''' aproxima.</para>
        ''' </summary>
        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Friend Function SqrtPacked(x As Vector128(Of Single)) As Vector128(Of Single)
            CuentasDeSimd.SqrtExacta += 1L
            Return Vector128.Sqrt(x)
        End Function

        ''' <summary>
        ''' Raíz cuadrada EXACTA escalar — `sqrtss`, y por lo tanto **bit-idéntica** al motor.
        ''' <para>Sitios: el eigensolver, `0x141360ED5` (`√(θ²+1)`) y `0x141360F06` (`√(t²+1)`). Los
        ''' dos son `sqrtss` sobre un `float`, no una cuenta en doble: la aritmética de adentro va en
        ''' simple precisión a propósito.</para>
        ''' <para>⛔ Ésta y los tres `Rsqrt`/`Rcp` de arriba son **la única casa** de la raíz en todo
        ''' el motor. G19a comprueba sobre el fuente que ningún otro archivo de `Havok/Motor/`
        ''' nombre `Sqrt`, `Pow` ni `Reciprocal`.</para>
        ''' </summary>
        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Friend Function SqrtExacta(x As Single) As Single
            CuentasDeSimd.SqrtExacta += 1L
            Return MathF.Sqrt(x)
        End Function

        ''' <summary>
        ''' `powf` — el del CRT de MSVC, `0x1422C47F8`. Lo llaman **exactamente dos** sitios del
        ''' motor, y los dos alimentan multiplicadores de posición:
        ''' <para>· el damping efectivo, `powf(1 − globalDampingPerSecond, dtSub)` (`0x14195B998` y
        ''' su gemelo `0x14195BB15`);</para>
        ''' <para>· la base del factor de rigidez, `powf(numSubSteps·s1·s2, −1,725)` (`0x1418C64A1`).</para>
        ''' <para>⚠️⚠️ **Divergencia declarada, no escondida.** `MathF.Pow` de .NET y el `powf` de
        ''' MSVC no prometen el mismo bit: los dos están dentro de 1 ulp del resultado exacto, pero
        ''' no tienen por qué coincidir entre sí. No hay forma de cerrar eso sin re-implementar el
        ''' `powf` de MSVC, y re-implementarlo «parecido» sería peor: metería un error propio, mayor
        ''' y sin cita. Así que se usa el de la plataforma y la diferencia queda anotada acá.</para>
        ''' <para>Dónde se nota: el damping multiplica `(pos − prev)` una vez por substep, así que un
        ''' ulp se compone. En 60 substeps la deriva relativa es del orden de `60 · 2⁻²⁴ ≈ 3,6e-06`
        ''' — por debajo de lo que cualquier gate de posición puede separar del ruido de `rsqrtps`,
        ''' que es 5 órdenes más grande (`≤ 1,5·2⁻¹²`).</para>
        ''' <para>⛔ Es la ÚNICA casa de `Pow` en `Havok/Motor/`; G19a lo comprueba sobre el fuente.</para>
        ''' </summary>
        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Friend Function PowCrt(baseV As Single, exponente As Single) As Single
            CuentasDeSimd.PowCrt += 1L
            Return MathF.Pow(baseV, exponente)
        End Function

        ''' <summary>
        ''' El producto punto de 3 componentes REPLICADO en las 4 lanes, con **la misma secuencia de
        ''' barajas y el mismo orden de sumas** que el motor: `shufps 0x55` · `shufps 0x00` · `addps`
        ''' · `shufps 0xAA` · `addps`.
        ''' <para>Sitios con esta forma exacta: el enlace estándar (`0x141A061C5`-`0x141A061DA`) y la
        ''' cápsula (`0x141A6ACD9`-`0x141A6ACE8`).</para>
        ''' <para>⚠️ El primer reductor de la cápsula (`0x141A6A7C7`) es **SoA de 4 partículas con
        ''' transposición** (`shufps 0x44/0xEE/0xDD/0x88`) y suma `(x²+y²)+z²` por carril: da el mismo
        ''' resultado por conmutatividad, pero es otra forma. No usar `Dot3` para reproducirlo.</para>
        ''' <para>⛔ Los índices de `Shuffle` van **inline**. Guardados en un `Shared ReadOnly` —como
        ''' hace `FastPow.vb:311` con `BcastIdx128`— el JIT no los pliega y emite un permute por
        ''' registro, o el camino gestionado sin AVX.</para>
        ''' <para>⛔ El orden de las sumas importa en punto flotante: `(y+x)+z`, no `x+(y+z)`.</para>
        ''' </summary>
        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Friend Function Dot3(a As Vector128(Of Single), b As Vector128(Of Single)) As Vector128(Of Single)
            Dim p = Vector128.Multiply(a, b)
            Dim yy = Vector128.Shuffle(p, Vector128.Create(1, 1, 1, 1))
            Dim xx = Vector128.Shuffle(p, Vector128.Create(0, 0, 0, 0))
            Dim s = Vector128.Add(yy, xx)
            Dim zz = Vector128.Shuffle(p, Vector128.Create(2, 2, 2, 2))
            Return Vector128.Add(s, zz)
        End Function

        ''' <summary>Difunde una lane a las cuatro, con el índice inline (ver la nota de
        ''' <see cref="Dot3"/> sobre por qué no puede salir de un campo).</summary>
        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Friend Function BcastX(v As Vector128(Of Single)) As Vector128(Of Single)
            Return Vector128.Shuffle(v, Vector128.Create(0, 0, 0, 0))
        End Function

        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Friend Function BcastY(v As Vector128(Of Single)) As Vector128(Of Single)
            Return Vector128.Shuffle(v, Vector128.Create(1, 1, 1, 1))
        End Function

        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Friend Function BcastZ(v As Vector128(Of Single)) As Vector128(Of Single)
            Return Vector128.Shuffle(v, Vector128.Create(2, 2, 2, 2))
        End Function

        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Friend Function BcastW(v As Vector128(Of Single)) As Vector128(Of Single)
            Return Vector128.Shuffle(v, Vector128.Create(3, 3, 3, 3))
        End Function

        ''' <summary>
        ''' Normaliza con la guarda del motor: si `|v|² &lt;= 0` devuelve **cero**, no NaN.
        ''' <para>El motor lo hace con `cmpleps` + `andnps` (p. ej. `0x141A00E61`/`0x141A00E6B` en el
        ''' heightfield y `0x141A1455A`/`0x141A14572` en la fase 3 del terreno). Sin esa guarda, un
        ''' vector nulo produce NaN y el NaN se propaga a toda la prenda.</para>
        ''' </summary>
        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Friend Function NormalizarCrudo(v As Vector128(Of Single)) As Vector128(Of Single)
            Dim n2 = Dot3(v, v)
            Dim vale = Vector128.GreaterThan(n2, Vector128(Of Single).Zero)
            Return Vector128.ConditionalSelect(vale, Vector128.Multiply(v, RsqrtCrudo(n2)), Vector128(Of Single).Zero)
        End Function

        ''' <summary>Ídem, para los sitios donde el motor refina con una Newton.</summary>
        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Friend Function NormalizarNewton(v As Vector128(Of Single)) As Vector128(Of Single)
            Dim n2 = Dot3(v, v)
            Dim vale = Vector128.GreaterThan(n2, Vector128(Of Single).Zero)
            Return Vector128.ConditionalSelect(vale, Vector128.Multiply(v, RsqrtNewton(n2)), Vector128(Of Single).Zero)
        End Function

        ''' <summary>
        ''' Lee un `hkVector4` (4 `Single` contiguos) del arreglo plano de partículas.
        ''' <para>⛔ El índice es `UIntPtr`: bajo `Option Strict On` VB NO convierte implícitamente
        ''' desde `UInteger` (`BC30512`). Y el arreglo gestionado **no** está alineado a 16 B — da
        ''' igual, `LoadUnsafe` emite `movups`.</para>
        ''' </summary>
        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Friend Function Leer(arr As Single(), indiceDeVector As Integer) As Vector128(Of Single)
            Return Vector128.LoadUnsafe(arr(0), New UIntPtr(CUInt(indiceDeVector) * 4UI))
        End Function

        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Friend Sub Escribir(arr As Single(), indiceDeVector As Integer, v As Vector128(Of Single))
            Vector128.StoreUnsafe(v, arr(0), New UIntPtr(CUInt(indiceDeVector) * 4UI))
        End Sub

        ''' <summary>La lane 0 de un vector, como escalar.</summary>
        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Friend Function Lane0(v As Vector128(Of Single)) As Single
            Return v.GetElement(0)
        End Function

    End Module

End Namespace

