Option Strict On
Option Explicit On

Imports System.Runtime.Intrinsics

' =================================================================================================
' EL TERRENO — `hclSimClothInstance::computeContactPlanes`, `0x14195DA70`. RE cap. 16.
'
' Es el PRODUCTOR de los planos que `Formas.PlanosPorParticula` (`hclPointContactPlanesShape`,
' tipo 10) consume. Aquella clase ya estaba transcrita; ésta fabrica su `(n̂, w)` cada cuadro.
'
' ⛔⛔⛔ **NO SE CABLEA, Y AHORA SE SABE POR QUÉ: EN FALLOUT 4 ESTA RAMA ES INALCANZABLE.**
'
' La geometría —triángulos y convexos— no sale del `.hkx`: son dos punteros de la instancia,
' `[inst+0xF8]` y `[inst+0x100]`. MEDIDO en el binario, quién los toca:
'
'   · `0x1418C7B10(rcx = inst, rdx = triángulos, r8 = convexos)` es **el único** que les pone un
'     valor real: `0x1418C7CBC` escribe `r15` (= `rdx`) en `+0xF8` y `0x1418C7CC6` escribe `r14`
'     (= `r8`) en `+0x100`, y de paso construye el estado de `+0xF0` con campos del ARCHIVO
'     (`data+0x140`, `+0x141`, `+0x144` → `estado+0x80`, `+0x81`, `+0x84`).
'   · ⛔ **A esa función NO LA LLAMA NADIE.** Barrido exhaustivo del `.exe`: cero `call rel32`,
'     cero `jmp rel32`, cero `lea` rip-relativo y cero punteros de 64 bits en ninguna sección. Sus
'     únicas tres referencias son datos de desenrollado: `.pdata 0x14403A778` y dos entradas de la
'     tabla de ámbitos de SEH (`.rdata 0x142D8CC5C` y `0x142D8CCB4`), reconocibles porque son
'     tripletes `inicio, fin, handler` que apuntan al MEDIO de la función.
'   · Los otros sitios que tocan los tres punteros los **ponen en cero**: `0x1418C6677` es el
'     constructor (escribe el mismo `rsi` en `+0xE0`, `+0xE8`, `+0xF0`, `+0xF8`, `+0x100`, `+0x108`)
'     y `0x1418C7D3A` es el borrado (`xor eax, eax` inmediatamente antes).
'
' ⇒ La puerta `0x14195E3A0` exige `[inst+0xF8]` o `[inst+0x100]` **no nulos y con `[+0x18] != 0`**.
' Como nada los llena, la puerta **siempre da falso** y `computeContactPlanes` —que el bucle de
' simulación sí llama cada cuadro desde `0x14195CB7A`— vuelve en la primera comparación.
'
' ⛔ Por eso cablear esto sería **inventar comportamiento que el juego no tiene**. Queda transcrito
' y probado (33 casos de gate, 23 mutaciones) porque es la ley del motor, y queda SIN consumidor
' porque el motor tampoco lo consume.
'
' Del archivo salen sólo los parámetros: `hclSimClothData::LandscapeCollisionData` (+0x134) con
' `landscapeRadius`, `enableStuckParticleDetection`, `stuckParticlesStretchFactorSq`,
' `pinchDetectionEnabled`, `pinchDetectionPriority` y `pinchDetectionRadius`, más
' `numLandscapeCollidableParticles` (+0x148). Y el permiso, `data+0x10+0x1d` (`0x14195E3AE`) —
' ⛔ que **no** es un flag global del juego: `0x1418C7730` devuelve `[inst+0x1d8]` si no es cero y
' si no `[inst+0x10] + 0x10`, y `[inst+0x10]` es el propio `hclSimClothData`.
'
' ⚠️ MEDIDO sobre el corpus: **13 prendas declaran `landscapeCollisionEnabled`** (censo M17). O sea
' que el flag está AUTORIZADO en el archivo y es INERTE en tiempo de ejecución. Las dos cosas son
' ciertas a la vez, y por eso el número solo no alcanzaba para decidir.
'
' ⭐⭐ LA CADENA, LEÍDA DE PUNTA A PUNTA:
'
'     0x14195DA70  computeContactPlanes   el orquestador
'     0x14195E3A0  la puerta
'     0x141A14030  la caja + el cero del scratch  → 0x141A14FF0  la SIEMBRA (cuantización)
'     0x141A18F20  el orden (counting sort ESTABLE, 257 cubos, clave `lo[eje] >> 7`)
'     0x141A14130  triángulos → 0x141A14E00 (cuantiza) → 0x141A152C0 (sweep + contacto)
'     0x141A14280  convexos: OCHO kernels, uno por tipo de forma, todos con el MISMO final
'     0x141A144C0  la salida: normaliza el acumulado y lo convierte en `(n̂, w)`
'     0x14195DF00  releaseStuckParticles → 0x141A14600 (tipo 1) / 0x141A146C0 (tipo 13)
'
' ⭐⭐⭐ LA SORPRESA DEL CAPÍTULO: el terreno **no elige un contacto, acumula una suma GAUSSIANA**.
' Cada triángulo o convexo cercano suma `(n̂, −distanciaConSigno) · peso`, con
' `peso = exp(−2·d² / (landscapeRadius² + 1e-4))` calculado por el truco de bits (`0x14271A340`,
' que es `2²³/ln2`, y su sesgo). Recién `0x141A144C0` normaliza esa suma y le mete el radio en la `w`.
'
' ⭐⭐ LOS SIETE KERNELS DE CONVEXO, COMPARADOS UNO POR UNO CONTRA `Formas.vb`. Lo que acá era un
' hueco declarado resultó ser, para UNA forma, una DIVERGENCIA medida:
'
'   · los SIETE reducen con la misma red transpuesta `({y} + {x}) + {z}` — bit a bit la asociación
'     de `Simd.Dot3` (medido en `0x141A18110`, `0x141A179D0`, `0x141A15AF0`, `0x141A18890`,
'     `0x141A16B90`, `0x141A163E0` y `0x141A17220`);
'   · CUATRO (tipos 3, 5, 9 y 11) no tienen **ni una constante propia**: llaman a
'     `0x141A08550`/`0x141A07C00`/`0x141A078E0` (cono), `0x141A00A90`/`0x141A00B00` (heightfield),
'     `0x1419FFDB0`/`0x1419FFE20` (convexGeometry) y `0x141A019C0`/`0x141A01A30` (convexPlanes) —
'     **las mismas funciones que `Formas.vb` transcribe**. Ahí la equivalencia es por IDENTIDAD;
'   · la ESFERA y la CÁPSULA las inlinean con las MISMAS constantes: el `FLT_EPSILON` de
'     `0x142F5AE70` antes del `rsqrt` (igual que `0x141A7030C`), la Newton de `3`/`0,5`, y el
'     `clamp(0, 1)` del parámetro de la cápsula;
'   · ⛔⛔ **el PLANO NO.** El terreno hace `Dot3(P, n̂) + n̂.w` (`0x141A17CA7`/`AE`, y el
'     `orps` + `addps` de `0x141A17CB8`/`BB`); `Formas.Plano` hace la suma horizontal de las CUATRO
'     lanes con la `w` metida en la lane 3 (`0x141A6E8C2` + `0x141A6E8CC`). `((y+x)+z)+w` contra
'     `(x+z)+(y+w)`: **otro orden de sumas, otros bits**. Por eso el plano NO delega.
' =================================================================================================

#If DEBUG Then

Namespace Havok.Motor

    ''' <summary>Un triángulo del terreno — TRES `hkVector4` seguidos, 48 B (`0x141A14EF1`, el paso
    ''' `t·0x30`).</summary>
    Friend Structure TrianguloDeTerreno
        Friend V0 As Vector128(Of Single)
        Friend V1 As Vector128(Of Single)
        Friend V2 As Vector128(Of Single)
    End Structure

    ''' <summary>Un convexo del terreno: la forma **ya derivada al mundo** y su AABB (`+0x50` y
    ''' `+0x60` del bloque de 0x70 B que recorre `0x14195DD50`).</summary>
    Friend NotInheritable Class ConvexoDeTerreno
        Friend ReadOnly Forma As Forma
        Friend ReadOnly Minimo As Vector128(Of Single)
        Friend ReadOnly Maximo As Vector128(Of Single)

        Friend Sub New(forma As Forma, minimo As Vector128(Of Single),
                       maximo As Vector128(Of Single))
            Me.Forma = forma
            Me.Minimo = minimo
            Me.Maximo = maximo
        End Sub
    End Class

    ''' <summary>
    ''' Lo que el capítulo 16 necesita del MUNDO y que el archivo no trae.
    ''' <para>⛔ Los AABB de las dos listas se validan con `max &lt; min` + `movmskps` + `test al,7` y
    ''' con `[+0x18] != 0` (`0x14195DB64`-`0x14195DBA3`). El margen es un `float` GLOBAL, `+0x14` del
    ''' bloque que devuelve `0x1418C7730` (`0x141A140A4` y `0x141A142CB`), y el interruptor es
    ''' `byte[globals+0x1D]` (`0x14195E3B2`).</para>
    ''' </summary>
    Friend NotInheritable Class TerrenoDelMundo
        Friend ReadOnly Triangulos As TrianguloDeTerreno()
        Friend ReadOnly MinTriangulos As Vector128(Of Single)
        Friend ReadOnly MaxTriangulos As Vector128(Of Single)
        Friend ReadOnly Convexos As ConvexoDeTerreno()
        Friend ReadOnly MinConvexos As Vector128(Of Single)
        Friend ReadOnly MaxConvexos As Vector128(Of Single)
        Friend ReadOnly Margen As Single
        Friend ReadOnly HabilitadoGlobal As Boolean

        Friend Sub New(triangulos As TrianguloDeTerreno(),
                       minTriangulos As Vector128(Of Single), maxTriangulos As Vector128(Of Single),
                       convexos As ConvexoDeTerreno(),
                       minConvexos As Vector128(Of Single), maxConvexos As Vector128(Of Single),
                       margen As Single, habilitadoGlobal As Boolean)
            Me.Triangulos = If(triangulos, Array.Empty(Of TrianguloDeTerreno)())
            Me.MinTriangulos = minTriangulos
            Me.MaxTriangulos = maxTriangulos
            Me.Convexos = If(convexos, Array.Empty(Of ConvexoDeTerreno)())
            Me.MinConvexos = minConvexos
            Me.MaxConvexos = maxConvexos
            Me.Margen = margen
            Me.HabilitadoGlobal = habilitadoGlobal
        End Sub
    End Class

    ''' <summary>
    ''' Una entrada del broadphase — los 16 B que arma `0x141A14FF0` y ordena `0x141A18F20`.
    ''' <para>```
    ''' +0x00 uint16  lo[eje]            ' el eje MAYOR primero, los otros dos rotados mod 3
    ''' +0x02 uint16  lo[(eje+1) mod 3]
    ''' +0x04 uint16  lo[(eje+2) mod 3]
    ''' +0x06 uint16  índice (de partícula o de triángulo)
    ''' +0x08 uint16  hi[eje]            ' cada uno con `+1` conservador (`inc ax`)
    ''' +0x0A uint16  hi[(eje+1) mod 3]
    ''' +0x0C uint16  hi[(eje+2) mod 3]
    ''' +0x0E uint16  scratch del ordenamiento
    ''' ```</para>
    ''' </summary>
    Friend Structure EntradaDeBroadphase
        Friend Lo0 As Integer
        Friend Lo1 As Integer
        Friend Lo2 As Integer
        Friend Indice As Integer
        Friend Hi0 As Integer
        Friend Hi1 As Integer
        Friend Hi2 As Integer
    End Structure

    ''' <summary>El cuantizador que comparten partículas y triángulos — `0x141A15018`-`0x141A150AC`
    ''' y su gemelo `0x141A14E0A`-`0x141A14E98`.</summary>
    Friend Structure Cuantizador
        Friend Lo As Vector128(Of Single)
        Friend Hi As Vector128(Of Single)
        Friend Escala As Vector128(Of Single)
        Friend Eje As Integer
    End Structure

    ''' <summary>Los cuatro unitarios, las tres perpendiculares y las cuatro longitudes de un
    ''' triángulo — `0x141A15544`-`0x141A156B3`.</summary>
    Friend Structure MarcoDeTerreno
        Friend P0 As Vector128(Of Single)
        Friend P1 As Vector128(Of Single)
        Friend E0 As Vector128(Of Single)
        Friend E1 As Vector128(Of Single)
        Friend E2 As Vector128(Of Single)
        Friend N As Vector128(Of Single)
        Friend Perp0 As Vector128(Of Single)
        Friend Perp1 As Vector128(Of Single)
        Friend Perp2 As Vector128(Of Single)
        ''' <summary>`{|e0|, |e1|, |e2|, |n|}` — `0x141A15628`, `(1/|·|) · |·|²`.</summary>
        Friend Largos As Vector128(Of Single)
    End Structure

    ' =============================================================================================

    Friend Module Terreno

        ''' <summary>`32766` — `0x14271A350`, el techo de la grilla.</summary>
        Friend Const Grilla As Single = 32766.0F

        ''' <summary>`1e-4` — `0x142498010`, la ε que se le suma a `landscapeRadius²`.</summary>
        Friend Const EpsilonDeRadio As Single = 9.99999975E-05F

        ''' <summary>`−87` — `0x14271A348`, el piso del exponente antes del `exp` rápido.</summary>
        Friend Const PisoDelExponente As Single = -87.0F

        ''' <summary>`2²³/ln 2` = `12102203` — `0x14271A340`.</summary>
        Friend Const EscalaDeExp As Single = 12102203.0F

        ''' <summary>`1,0648073e+09` — `0x14271A344`, el sesgo del `exp` rápido.</summary>
        Friend Const SesgoDeExp As Single = 1.0648073E+09F

        ''' <summary>`33` — `0x14271A2F0`, la distancia² con la que se DESCARTA el candidato de cara
        ''' cuando el punto no proyecta adentro del triángulo.</summary>
        Friend Const DistanciaDeDescarte As Single = 33.0F

        ''' <summary>`−1000000` — el piso del parámetro de la CARA; los otros tres van con piso 0.
        ''' <para>⛔ El dato está en `0x14271A2EC`, que es la lane `w` del `{0,0,0,−1000000}` que
        ''' `0x141A15769` carga desde `0x14271A2E0`. Se cita el DATO, no la base del vector: es lo
        ''' que le permite a GEX leerlo del `.exe`.</para></summary>
        Friend Const PisoDeCara As Single = -1000000.0F

        ''' <summary>257 — `0x141A18F95` pone a cero `0x202` **bytes** de histograma, o sea 257
        ''' `uint16`. Alcanza porque la clave es `lo &gt;&gt; 7` con `lo &lt;= 32766`, o sea 0..255, más el
        ''' `+1` del desplazamiento.</summary>
        Friend Const CubosDelOrden As Integer = 257

        ' -----------------------------------------------------------------------------------------
        ' La puerta — 0x14195E3A0
        ' -----------------------------------------------------------------------------------------

        ''' <summary>
        ''' `0x14195E3A0`, leída entera. Son CUATRO comprobaciones, no una.
        ''' <para>```
        ''' si byte[globals+0x1D] == 0                              → false   ' 0x14195E3B2
        ''' si NINGUNA de las dos listas tiene [+0x18] != 0         → false   ' 0x14195E3B7-D9
        ''' si data.numLandscapeCollidableParticles (+0x148) == 0   → false   ' 0x14195E3DF
        ''' si [inst+0xF0] == 0                                     → false   ' 0x14195E3E8
        ''' ```</para>
        ''' </summary>
        Friend Function Habilitado(inst As Instancia, mundo As TerrenoDelMundo,
                                   numParticulasDeTerreno As Integer,
                                   hayContexto As Boolean) As Boolean
            If inst Is Nothing OrElse mundo Is Nothing Then Return False
            If Not mundo.HabilitadoGlobal Then Return False
            If mundo.Triangulos.Length = 0 AndAlso mundo.Convexos.Length = 0 Then Return False
            If numParticulasDeTerreno = 0 Then Return False
            Return hayContexto
        End Function

        ' -----------------------------------------------------------------------------------------
        ' La caja de trabajo — 0x14195DBA8-0x14195DC01
        ' -----------------------------------------------------------------------------------------

        ''' <summary>
        ''' Arranca en `[+CasiFltMax, −CasiFltMax]` y se recorta con las AABB de las listas vivas.
        ''' <para>⛔ El `−CasiFltMax` sale de un `xorps` contra `{0x80000000 ×4}` sobre el mismo
        ''' `0x7F7FFFEE` (`0x14195DBB7`-`0x14195DBC9`), no de otra constante.</para>
        ''' </summary>
        Friend Sub CajaDeTrabajo(mundo As TerrenoDelMundo,
                                 ByRef lo As Vector128(Of Single), ByRef hi As Vector128(Of Single))
            lo = Vector128.Create(Simd.CasiFltMax)                       ' 0x142F3C740
            hi = Vector128.Xor(lo.AsUInt32(), Vector128.Create(2147483648UI)).AsSingle()
            If mundo.Triangulos.Length > 0 Then
                lo = Vector128.Min(lo, mundo.MinTriangulos)              ' 0x14195DBD6
                hi = Vector128.Max(hi, mundo.MaxTriangulos)              ' 0x14195DBDA
            End If
            If mundo.Convexos.Length > 0 Then
                lo = Vector128.Min(lo, mundo.MinConvexos)                ' 0x14195DBED
                hi = Vector128.Max(hi, mundo.MaxConvexos)
            End If
        End Sub

        ' -----------------------------------------------------------------------------------------
        ' El cuantizador — 0x141A15018-0x141A150AC
        ' -----------------------------------------------------------------------------------------

        ''' <summary>
        ''' El dominio sale de `inst[+0x90]`/`[+0xA0]` — el AABB de las partículas con
        ''' `staticCollisionMasks` NEGATIVA, que es el mismo filtro que aplica la siembra.
        ''' <para>```
        ''' ext   = hi − lo
        ''' mitad = |ext · 0,5|                            ' 0x141A15042/49/4E, pslld 1 + psrld 1
        ''' eje   = argmax de las TRES lanes de `mitad`    ' cmpleps + movmskps + tabla 0x142639E70
        ''' esc   = 32766 · ((2 − ext·rcp(ext))·rcp(ext))  ' rcp CRUDO + UNA Newton; la w queda en 1
        ''' ```</para>
        ''' </summary>
        Friend Function HacerCuantizador(lo As Vector128(Of Single),
                                         hi As Vector128(Of Single)) As Cuantizador
            Dim q As Cuantizador
            q.Lo = lo
            q.Hi = hi
            Dim ext = Vector128.Subtract(hi, lo)                          ' 0x141A1503C
            Dim medio = Vector128.Multiply(ext, Vector128.Create(0.5F))   ' 0x142F3C650
            medio = Vector128.ShiftRightLogical(
                Vector128.ShiftLeft(medio.AsInt32(), 1), 1).AsSingle()    ' 0x141A15049/4E
            Dim x = Simd.Lane0(medio)
            Dim y = Vector128.GetElement(medio, 1)
            Dim z = Vector128.GetElement(medio, 2)
            ' el argmax con el orden del motor: `y` contra `x`, y después `z` contra el mayor
            q.Eje = 0
            If y >= x Then q.Eje = 1
            If z >= Math.Max(x, y) Then q.Eje = 2
            Dim inv = Simd.RcpNewton(ext)                                 ' 0x141A1506B-96
            q.Escala = Vector128.Multiply(Vector128.Create(Grilla),
                                          inv.WithElement(Simd.LaneW, 1.0F))  ' 0x141A150A1-AC
            Return q
        End Function

        ''' <summary>La entrada de 16 B de una caja ya recortada — `0x141A1517D`-`0x141A15209`, con
        ''' la rotación `j = (eje + k) mod 3` que arma el `mul 0xAAAAAAAB` + `shr 1`.</summary>
        Friend Function Entrada(q As Cuantizador, lo As Vector128(Of Single),
                                hi As Vector128(Of Single), indice As Integer) As EntradaDeBroadphase
            Dim a = Vector128.Multiply(Vector128.Subtract(lo, q.Lo), q.Escala)
            Dim b = Vector128.Multiply(Vector128.Subtract(hi, q.Lo), q.Escala)
            Dim l(2) As Integer, h(2) As Integer
            For k = 0 To 2
                ' `cvttss2si` trunca hacia cero; el `+1` de arriba es el `inc ax` conservador
                l((q.Eje + k) Mod 3) = CInt(Math.Truncate(CDbl(Vector128.GetElement(a, k))))
                h((q.Eje + k) Mod 3) = CInt(Math.Truncate(CDbl(Vector128.GetElement(b, k)))) + 1
            Next
            Dim e As EntradaDeBroadphase
            e.Lo0 = l(0) : e.Lo1 = l(1) : e.Lo2 = l(2)
            e.Hi0 = h(0) : e.Hi1 = h(1) : e.Hi2 = h(2)
            e.Indice = indice
            Return e
        End Function

        ' -----------------------------------------------------------------------------------------
        ' La siembra — 0x141A14FF0 y 0x141A14E00
        ' -----------------------------------------------------------------------------------------

        ''' <summary>
        ''' Las entradas de las PARTÍCULAS. Tres filtros, en este orden:
        ''' <para>```
        ''' si p está fuera de la caja expandida             → saltear   ' 0x141A15124/28/35
        ''' si particleDatas[p].invMass == 0                 → saltear   ' 0x141A1513D ucomiss
        ''' si (int32) staticCollisionMasks[p] &gt;= 0       → saltear   ' 0x141A15148 cmp/jge
        ''' barrido = clamp([ min(pos,prev) − margen , max(pos,prev) + margen ], dominio)
        ''' ```</para>
        ''' </summary>
        Friend Function EntradasDeParticula(inst As Instancia, q As Cuantizador,
                                            cajaLo As Vector128(Of Single),
                                            cajaHi As Vector128(Of Single),
                                            margen As Single) As List(Of EntradaDeBroadphase)
            Dim r As New List(Of EntradaDeBroadphase)()
            Dim m = Vector128.Create(margen)
            For p = 0 To inst.NumParticulas - 1
                Dim pos = Simd.Leer(inst.Posiciones, p)
                If Not DentroDe(pos, cajaLo, cajaHi) Then Continue For
                If inst.InvMasa Is Nothing OrElse inst.InvMasa(p) = 0.0F Then Continue For
                If inst.MascarasDeColision Is Nothing Then Continue For
                ' ⛔ el motor compara los MISMOS 32 bits con signo (`cmp dword [rbx+p*4], 0`
                ' + `jge`, `0x141A15148`): es una REINTERPRETACION, no una conversion. `CInt`
                ' sobre un `UInteger` con el bit 31 puesto DESBORDA.
                If (inst.MascarasDeColision(p) And &H80000000UI) = 0UI Then Continue For
                Dim prev = Simd.Leer(inst.Previas, p)
                Dim lo = Vector128.Subtract(Vector128.Min(pos, prev), m)  ' 0x141A15157/65
                Dim hi = Vector128.Add(Vector128.Max(pos, prev), m)       ' 0x141A1515C/6A
                lo = Vector128.Min(Vector128.Max(lo, q.Lo), q.Hi)         ' 0x141A1516F/75
                hi = Vector128.Min(Vector128.Max(hi, q.Lo), q.Hi)         ' 0x141A15172/79
                r.Add(Entrada(q, lo, hi, p))
            Next
            Return r
        End Function

        ''' <summary>Las entradas de los TRIÁNGULOS — `0x141A14EE0`-`0x141A14F92`: el MISMO
        ''' cuantizador y la misma rotación, sobre el AABB de los tres vértices.</summary>
        Friend Function EntradasDeTriangulo(tri As TrianguloDeTerreno(),
                                            q As Cuantizador) As List(Of EntradaDeBroadphase)
            Dim r As New List(Of EntradaDeBroadphase)()
            For t = 0 To tri.Length - 1
                Dim lo = Vector128.Min(Vector128.Min(tri(t).V0, tri(t).V1), tri(t).V2)
                Dim hi = Vector128.Max(Vector128.Max(tri(t).V0, tri(t).V1), tri(t).V2)
                lo = Vector128.Min(Vector128.Max(lo, q.Lo), q.Hi)         ' 0x141A14F0C
                hi = Vector128.Min(Vector128.Max(hi, q.Lo), q.Hi)         ' 0x141A14F0F
                r.Add(Entrada(q, lo, hi, t))
            Next
            Return r
        End Function

        ''' <summary>`(lo &lt;= p) AND (p &lt;= hi)` en las TRES lanes — `movmskps` + `and eax,7` +
        ''' `cmp al,7` (`0x141A15124`-`0x141A15137`).</summary>
        Private Function DentroDe(p As Vector128(Of Single), lo As Vector128(Of Single),
                                  hi As Vector128(Of Single)) As Boolean
            Dim m = Vector128.BitwiseAnd(Vector128.LessThanOrEqual(lo, p),
                                         Vector128.LessThanOrEqual(p, hi)).AsUInt32()
            For k = 0 To 2
                If m.GetElement(k) = 0UI Then Return False
            Next
            Return True
        End Function

        ' -----------------------------------------------------------------------------------------
        ' El orden — 0x141A18F20
        ' -----------------------------------------------------------------------------------------

        ''' <summary>
        ''' Counting sort **ESTABLE de una sola pasada** por `Lo0 &gt;&gt; 7` (`0x141A18FB7`).
        ''' <para>⛔ NO es un orden total: los que caen en el mismo cubo de 128 conservan su orden
        ''' relativo. Son 257 cubos porque `Lo0 &lt;= 32766` (`0x141A18F95` pone a cero `0x202`
        ''' bytes, y el prefijo son 255 sumas desenrolladas de a cinco, `0x141A18FCA`).</para>
        ''' </summary>
        Friend Sub Ordenar(l As List(Of EntradaDeBroadphase))
            If l Is Nothing OrElse l.Count < 2 Then Return
            Dim hist(CubosDelOrden - 1) As Integer
            For Each e In l
                Dim c = (e.Lo0 >> 7) + 1
                If c >= 0 AndAlso c < CubosDelOrden Then hist(c) += 1
            Next
            For k = 1 To CubosDelOrden - 1
                hist(k) += hist(k - 1)
            Next
            Dim salida(l.Count - 1) As EntradaDeBroadphase
            For Each e In l
                Dim c = e.Lo0 >> 7
                If c < 0 Then c = 0
                If c >= CubosDelOrden Then c = CubosDelOrden - 1
                salida(hist(c)) = e
                hist(c) += 1
            Next
            l.Clear()
            l.AddRange(salida)
        End Sub

        ' -----------------------------------------------------------------------------------------
        ' El sweep-and-prune — 0x141A15420-0x141A154B0
        ' -----------------------------------------------------------------------------------------

        ''' <summary>
        ''' El solape en los DOS ejes menores, con las restas de 16 bits empaquetadas y un solo
        ''' `test` contra `0x80008000` (`0x141A15433`-`0x141A15445`): ninguna de las cuatro puede
        ''' quedar negativa.
        ''' </summary>
        Friend Function SolapanMenores(a As EntradaDeBroadphase, b As EntradaDeBroadphase) As Boolean
            If a.Hi1 - b.Lo1 < 0 OrElse a.Hi2 - b.Lo2 < 0 Then Return False
            If b.Hi1 - a.Lo1 < 0 OrElse b.Hi2 - a.Lo2 < 0 Then Return False
            Return True
        End Function

        ''' <summary>
        ''' El merge de las dos listas ordenadas: avanza la que tenga el `Lo0` menor y recorre la
        ''' otra mientras `Lo0 &lt; Hi0` de la cabeza (`0x141A15425`, `0x141A1544F`).
        ''' </summary>
        Friend Function Pares(a As List(Of EntradaDeBroadphase),
                              b As List(Of EntradaDeBroadphase)) As List(Of ParDeTerreno)
            Dim r As New List(Of ParDeTerreno)()
            Dim ia = 0, ib = 0
            While ia < a.Count AndAlso ib < b.Count
                If a(ia).Lo0 <= b(ib).Lo0 Then
                    Dim j = ib
                    While j < b.Count AndAlso b(j).Lo0 < a(ia).Hi0
                        If SolapanMenores(a(ia), b(j)) Then
                            r.Add(New ParDeTerreno(a(ia).Indice, b(j).Indice))
                        End If
                        j += 1
                    End While
                    ia += 1
                Else
                    Dim j = ia
                    While j < a.Count AndAlso a(j).Lo0 < b(ib).Hi0
                        If SolapanMenores(b(ib), a(j)) Then
                            r.Add(New ParDeTerreno(a(j).Indice, b(ib).Indice))
                        End If
                        j += 1
                    End While
                    ib += 1
                End If
            End While
            Return r
        End Function

        ' -----------------------------------------------------------------------------------------
        ' El marco del triángulo — 0x141A15544-0x141A156B3
        ' -----------------------------------------------------------------------------------------

        ''' <summary>
        ''' ⛔ Las CUATRO longitudes al cuadrado se empaquetan en UN registro y se normalizan de una
        ''' sola vez con `rsqrt` + UNA Newton (`0x141A155D1`-`0x141A155E7`).
        ''' <para>Y las tres perpendiculares salen de tres cruz, con ESTOS argumentos y no otros
        ''' (`0x141A15646`/`31`/`5A`, `0x141A15642`/`62`/`6F`, `0x141A15666`/`81`/`90`):</para>
        ''' <para>`⊥0 = ê0 × n̂` · `⊥1 = n̂ × ê1` · `⊥2 = ê2 × n̂`</para>
        ''' </summary>
        Friend Function Marco(t As TrianguloDeTerreno) As MarcoDeTerreno
            Dim m As MarcoDeTerreno
            m.P0 = t.V0 : m.P1 = t.V1
            Dim e0 = Vector128.Subtract(t.V1, t.V0)                       ' 0x141A15554
            Dim e1 = Vector128.Subtract(t.V2, t.V0)                       ' 0x141A15558
            Dim e2 = Vector128.Subtract(t.V2, t.V1)                       ' 0x141A1554C
            Dim n = Polar.Cruz(e0, e2)                                    ' 0x141A15564-95
            Dim l2 = Vector128.Create(Simd.Lane0(Simd.Dot3(e0, e0)),
                                      Simd.Lane0(Simd.Dot3(e1, e1)),
                                      Simd.Lane0(Simd.Dot3(e2, e2)),
                                      Simd.Lane0(Simd.Dot3(n, n)))        ' 0x141A15586-CE
            Dim inv = Simd.RsqrtNewton(l2)                                ' 0x141A155D1-E7
            m.Largos = Vector128.Multiply(inv, l2)                        ' 0x141A15628
            m.E0 = Vector128.Multiply(e0, Simd.BcastX(inv))               ' 0x141A155F0/F4
            m.E1 = Vector128.Multiply(e1, Simd.BcastY(inv))               ' 0x141A155FB/FF
            m.E2 = Vector128.Multiply(e2, Simd.BcastZ(inv))               ' 0x141A15606/0A
            m.N = Vector128.Multiply(n, Simd.BcastW(inv))                 ' 0x141A15620/24
            m.Perp0 = Polar.Cruz(m.E0, m.N)                               ' 0x141A15646/31/5A
            m.Perp1 = Polar.Cruz(m.N, m.E1)                               ' 0x141A15642/62/6F
            m.Perp2 = Polar.Cruz(m.E2, m.N)                               ' 0x141A15666/81/90
            Return m
        End Function

        ' -----------------------------------------------------------------------------------------
        ' El punto más cercano — 0x141A156B7-0x141A15907
        ' -----------------------------------------------------------------------------------------

        ''' <summary>
        ''' Los CUATRO candidatos a la vez, y la elección por **TORNEO de dos niveles**.
        ''' <para>```
        ''' a = P − v0 ; b = P − v1
        ''' t = { a·ê0 , a·ê1 , b·ê2 , a·n̂ }
        ''' t = min( max(t, {0,0,0,−1000000}) , {|e0|, |e1|, |e2|, 0} )
        ''' c0 = v0 + t.x·ê0 · c1 = v0 + t.y·ê1 · c2 = v1 + t.z·ê2 · c3 = P − t.w·n̂
        ''' d² = { |c0−P|² , |c1−P|² , |c2−P|² , |c3−P|² }        ' 0x141A157C1-15832, en ESE orden
        ''' adentro = max(a·⊥0, a·⊥1, b·⊥2) &lt;= 0
        ''' si no adentro: d²[3] = 33                             ' 0x141A15885/89/90
        ''' r = min( min(d0,d1) , min(d2,d3) )                    ' 0x141A1586A / 94 / D5 — TORNEO
        ''' ```</para>
        ''' <para>⛔ No es una cadena `if d1 &lt; mejor / if d2 &lt; mejor / if d3 &lt; mejor`: con el
        ''' `33` de descarte y con empates no da lo mismo.</para>
        ''' </summary>
        Friend Sub PuntoMasCercano(m As MarcoDeTerreno, p As Vector128(Of Single),
                                   ByRef punto As Vector128(Of Single), ByRef dist2 As Single)
            Dim a = Vector128.Subtract(p, m.P0)                           ' 0x141A156CB
            Dim b = Vector128.Subtract(p, m.P1)                           ' 0x141A156D1
            Dim t = Vector128.Create(Simd.Lane0(Simd.Dot3(a, m.E0)),
                                     Simd.Lane0(Simd.Dot3(a, m.E1)),
                                     Simd.Lane0(Simd.Dot3(b, m.E2)),
                                     Simd.Lane0(Simd.Dot3(a, m.N)))       ' 0x141A156D5-39
            t = Vector128.Max(t, Vector128.Create(0.0F, 0.0F, 0.0F, PisoDeCara))  ' 0x141A15769
            t = Vector128.Min(t, m.Largos.WithElement(Simd.LaneW, 0.0F))  ' 0x141A15775

            Dim c0 = Vector128.Add(m.P0, Vector128.Multiply(m.E0, Simd.BcastX(t)))   ' 0x141A157B5/C8
            Dim c1 = Vector128.Add(m.P0, Vector128.Multiply(m.E1, Simd.BcastY(t)))   ' 0x141A1579C/B1
            Dim c2 = Vector128.Add(m.P1, Vector128.Multiply(m.E2, Simd.BcastZ(t)))   ' 0x141A15798/A8
            Dim c3 = Vector128.Subtract(p, Vector128.Multiply(m.N, Simd.BcastW(t)))  ' 0x141A157AD/B9

            Dim u0 = Simd.Lane0(Simd.Dot3(a, m.Perp0))                    ' 0x141A15711
            Dim u1 = Simd.Lane0(Simd.Dot3(a, m.Perp1))                    ' 0x141A15718
            Dim u2 = Simd.Lane0(Simd.Dot3(b, m.Perp2))                    ' 0x141A156EE
            Dim adentro = Math.Max(Math.Max(u0, u1), u2) <= 0.0F          ' 0x141A15827-50

            Dim d0 = Cuadrado(c0, p), d1 = Cuadrado(c1, p), d2 = Cuadrado(c2, p)
            Dim d3 = If(adentro, Cuadrado(c3, p), DistanciaDeDescarte)

            ' el torneo: (c0 vs c1) y (c2 vs c3), y después los dos ganadores
            Dim pA = c0, dA = d0
            If d1 < dA Then pA = c1 : dA = d1                             ' 0x141A1586A
            Dim pB = c2, dB = d2
            If d3 < dB Then pB = c3 : dB = d3                             ' 0x141A15894
            If dA < dB Then                                               ' 0x141A158D5
                punto = pA : dist2 = dA
            Else
                punto = pB : dist2 = dB
            End If
        End Sub

        Private Function Cuadrado(c As Vector128(Of Single),
                                  p As Vector128(Of Single)) As Single
            Dim d = Vector128.Subtract(c, p)
            Return Simd.Lane0(Simd.Dot3(d, d))
        End Function

        ' -----------------------------------------------------------------------------------------
        ' El peso gaussiano — 0x141A1590A-0x141A1593F
        ' -----------------------------------------------------------------------------------------

        ''' <summary>
        ''' `exp(x)` por bits.
        ''' <para>```
        ''' x = max(x, −87)                              ' 0x141A1591E, 0xC2AE0000
        ''' i = (int)( x · 12102203 + 1,0648073e9 )      ' 0x141A15925/2D/35
        ''' r = bitcast_float(i)                         ' 0x141A15951, lee el int COMO float
        ''' ```</para>
        ''' <para>⛔ No es `MathF.Exp`: es esta cuenta, con estas dos constantes y con el `max`
        ''' contra −87 delante.</para>
        ''' </summary>
        Friend Function ExpRapido(x As Single) As Single
            Dim v = Math.Max(x, PisoDelExponente)
            Dim d = CDbl(v) * CDbl(EscalaDeExp) + CDbl(SesgoDeExp)
            If Double.IsNaN(d) OrElse d >= 2147483648.0R OrElse d < -2147483648.0R Then
                Return BitConverter.Int32BitsToSingle(Integer.MinValue)   ' lo que da `cvttss2si`
            End If
            Return BitConverter.Int32BitsToSingle(CInt(Math.Truncate(d)))
        End Function

        ' -----------------------------------------------------------------------------------------
        ' El camino de TRIÁNGULOS — 0x141A152C0
        ' -----------------------------------------------------------------------------------------

        ''' <summary>
        ''' Acumula, por partícula, la suma gaussiana de `(n̂, −(a·n̂))`.
        ''' <para>```
        ''' peso = expRapido( d² · (−2 / (radio² + 1e-4)) )              ' 0x141A1590E-3F
        ''' esc  = min(|n|, 2·(radio²+1e-4)) · (1/(2·(radio²+1e-4)))    ' 0x141A1569D-B3
        ''' acum[p] += ( n̂.x, n̂.y, n̂.z, −(a·n̂) ) · (peso · esc)       ' 0x141A15942-6E
        ''' ```</para>
        ''' <para>⛔ La `w` es la proyección SIN recortar (`0x141A15832` toma la lane w de `t`
        ''' ANTES del `maxps`/`minps`), negada.</para>
        ''' </summary>
        Friend Sub ColisionarTriangulos(inst As Instancia, mundo As TerrenoDelMundo,
                                        radio As Single, pares As List(Of ParDeTerreno),
                                        acum As Single())
            Dim r2 = radio * radio + EpsilonDeRadio                       ' 0x141A15391/AA
            Dim dosR2 = 2.0F * r2                                         ' 0x141A153CB
            Dim invDosR2 = Simd.Lane0(Simd.RcpNewton(Vector128.Create(dosR2)))
            Dim menosDosSobreR2 = -2.0F * Simd.Lane0(Simd.RcpNewton(Vector128.Create(r2)))
            Dim ultimo = -1
            Dim m As MarcoDeTerreno = Nothing
            For Each par In pares
                Dim p = par.Particula, t = par.Objeto
                If t <> ultimo Then                                       ' 0x141A1553B cmp/je
                    m = Marco(mundo.Triangulos(t))
                    ultimo = t
                End If
                Dim pos = Simd.Leer(inst.Posiciones, p)
                Dim punto As Vector128(Of Single) = Nothing
                Dim d2 = 0.0F
                PuntoMasCercano(m, pos, punto, d2)
                Dim peso = ExpRapido(d2 * menosDosSobreR2)
                Dim esc = Math.Min(Vector128.GetElement(m.Largos, 3), dosR2) * invDosR2
                Dim proy = Simd.Lane0(Simd.Dot3(Vector128.Subtract(pos, m.P0), m.N))
                Dim con = m.N.WithElement(Simd.LaneW, -proy)
                Simd.Escribir(acum, p,
                    Vector128.Add(Simd.Leer(acum, p),
                                  Vector128.Multiply(con, Vector128.Create(peso * esc))))
            Next
        End Sub

        ' -----------------------------------------------------------------------------------------
        ' El camino de CONVEXOS — 0x141A14280 y sus ocho kernels
        ' -----------------------------------------------------------------------------------------

        ''' <summary>
        ''' `0x141A14280` despacha por `[[convexo]+0x10]`, el MISMO espacio de 12 tipos de
        ''' `Colision.TablaDeDespacho`, con la tabla de saltos en `0x141A14488`:
        ''' <para>0 esfera `0x141A18110` · 1 plano `0x141A179D0` · 2 cápsula `0x141A15AF0` ·
        ''' 3 cápsula cónica `0x141A18890` · 5 heightfield `0x141A16B90` ·
        ''' 9 convexGeometry `0x141A163E0` · 11 convexPlanes `0x141A17220`; los tipos 4, 6, 7, 8 y
        ''' **10** caen en el `return`.</para>
        ''' <para>⛔ El **10** es `hclPointContactPlanesShape`, o sea lo que este capítulo produce:
        ''' el terreno no se colisiona consigo mismo.</para>
        ''' <para>⛔ El filtro por AABB es **por CONVEXO, no por partícula**: está afuera, en
        ''' `0x14195DD50`-`0x14195DD73` (`inst[+0x90] &lt;= c[+0x60]` y `c[+0x50] &lt;= inst[+0xA0]`
        ''' en las tres lanes). Adentro, el kernel recorre TODAS las entradas sembradas, de a cuatro
        ''' (`0x141A182E0`, con `ceil(n/4)` vueltas).</para>
        ''' <para>⛔ Y acá el peso va **SIN escala**: los cuatro pesos salen del `exp` y se
        ''' multiplican directo (`0x141A1866E`-`0x141A186A8`). El factor de área del triángulo no
        ''' tiene equivalente.</para>
        ''' </summary>
        Friend Sub ColisionarConvexos(inst As Instancia, mundo As TerrenoDelMundo, radio As Single,
                                      entradas As List(Of EntradaDeBroadphase), acum As Single())
            Dim r2 = radio * radio + EpsilonDeRadio
            Dim menosDosSobreR2 = -2.0F * Simd.Lane0(Simd.RcpNewton(Vector128.Create(r2)))
            For Each c In mundo.Convexos
                If c Is Nothing OrElse c.Forma Is Nothing Then Continue For
                If Colision.NoHaceNada(c.Forma.Tipo) Then Continue For
                If c.Forma.Tipo = 10 Then Continue For                    ' el propio, 0x141A14488
                If Not SolapaCaja(inst.AabbMinMascara, inst.AabbMaxMascara,
                                  c.Minimo, c.Maximo) Then Continue For   ' 0x14195DD50-73
                Dim plano = TryCast(c.Forma, Plano)
                For Each e In entradas
                    Dim p = e.Indice
                    Dim pos = Simd.Leer(inst.Posiciones, p)
                    ' ⛔⛔ EL PLANO NO DELEGA: su distancia sale con OTRA asociación de sumas.
                    Dim con As Contacto
                    If plano IsNot Nothing Then
                        con.Normal = plano.Ecuacion
                        con.Distancia = DistanciaAlPlanoDelTerreno(pos, plano.Ecuacion)
                    Else
                        con = c.Forma.PuntoMasCercano(pos, p, 0.0F, False)
                    End If
                    Dim d = con.Distancia
                    Dim peso = ExpRapido(d * d * menosDosSobreR2)
                    Simd.Escribir(acum, p,
                        Vector128.Add(Simd.Leer(acum, p),
                                      Vector128.Multiply(con.Normal.WithElement(Simd.LaneW, -d),
                                                         Vector128.Create(peso))))
                Next
            Next
        End Sub

        ''' <summary>
        ''' `Dot3(P, n̂) + n̂.w` — la distancia al plano **del terreno**, que NO es la de
        ''' `Formas.Plano`.
        ''' <para>El terreno reduce con la red transpuesta de cuatro partículas
        ''' (`0x141A17C9C`-`0x141A17CAE`, `({y} + {x}) + {z}`, la asociación de `Simd.Dot3`) y
        ''' recién después le suma la `w` difundida con `orps` (`0x141A17CB8`/`BB`).
        ''' `Formas.Plano.DistanciaAlPlano` hace la suma horizontal de las CUATRO lanes con la `w`
        ''' ya metida en la lane 3 (`0x141A6E8C2` + `0x141A6E8CC`).</para>
        ''' <para>⛔ `((y+x)+z) + w` no es `(x+z) + (y+w)`: en punto flotante son otros bits, y
        ''' el propio comentario de `Formas.Plano` ya avisaba de esa diferencia. Delegar ahí era
        ''' un atajo, no una transcripción.</para>
        ''' </summary>
        Friend Function DistanciaAlPlanoDelTerreno(p As Vector128(Of Single),
                                                   ecuacion As Vector128(Of Single)) As Single
            Return Simd.Lane0(Simd.Dot3(p, ecuacion)) + Vector128.GetElement(ecuacion, 3)
        End Function

        ''' <summary>`aMin &lt;= bMax` y `bMin &lt;= aMax` en las TRES lanes — `0x14195DD50`-
        ''' `0x14195DD73`, dos `cmpleps` + `andps` + `movmskps` + `cmp al,7`.</summary>
        Friend Function SolapaCaja(aMin As Vector128(Of Single), aMax As Vector128(Of Single),
                                   bMin As Vector128(Of Single), bMax As Vector128(Of Single)) As Boolean
            Dim m = Vector128.BitwiseAnd(Vector128.LessThanOrEqual(aMin, bMax),
                                         Vector128.LessThanOrEqual(bMin, aMax)).AsUInt32()
            For k = 0 To 2
                If m.GetElement(k) = 0UI Then Return False
            Next
            Return True
        End Function

        ' -----------------------------------------------------------------------------------------
        ' La salida — 0x141A144C0
        ' -----------------------------------------------------------------------------------------

        ''' <summary>
        ''' Normaliza el acumulado y lo convierte en el `(n̂, w)` que consume
        ''' <see cref="PlanosPorParticula"/>.
        ''' <para>```
        ''' n2 = dot3(v, v)                                   ' 0x141A14533-42, la forma de `Dot3`
        ''' n̂  = n2 &gt; 0 ? v · rsqrtNewton(n2) : v            ' 0x141A14549-81, con guarda `<= 0`
        ''' si n2 &gt; 0:
        '''     d = P.x·n̂.x + P.y·n̂.y + P.z·n̂.z + n̂.w       ' 0x141A1458D-AB, hsum de CUATRO
        '''     plano[p] = (n̂.xyz, −(d + landscapeRadius))    ' 0x141A145B2-C2
        ''' ```</para>
        ''' <para>⛔⛔ La `w` del plano lleva el radio METIDO ADENTRO **y** la `w` del acumulado —que
        ''' es la distancia con signo promediada con el mismo peso gaussiano—. Eso es lo que le
        ''' permite a `PlanosPorParticula` cortar con `dist &lt; 0` sin restar el radio.</para>
        ''' </summary>
        Friend Sub PlanosFinales(inst As Instancia, radio As Single, acum As Single())
            For p = 0 To inst.NumParticulas - 1
                Dim v = Simd.Leer(acum, p)
                Dim n2 = Simd.Dot3(v, v)                                  ' 0x141A14533-42
                If Simd.Lane0(n2) <= 0.0F Then Continue For               ' 0x141A14584/86
                Dim nHat = Vector128.Multiply(v, Simd.RsqrtNewtonConGuarda(n2))
                Dim pos = Simd.Leer(inst.Posiciones, p)
                Dim d = Simd.Lane0(Simd.Dot3(pos, nHat)) + Vector128.GetElement(nHat, 3)
                Simd.Escribir(acum, p, nHat.WithElement(Simd.LaneW, -(d + radio)))
            Next
        End Sub

        ' -----------------------------------------------------------------------------------------
        ' La liberación de las pegadas — 0x14195DF00 y 0x141A14600
        ' -----------------------------------------------------------------------------------------

        ''' <summary>
        ''' El enlace estirado de más BORRA el plano de sus dos partículas.
        ''' <para>```
        ''' si !enableStuckParticleDetection (data+0x138) → nada           ' 0x14195DF14
        ''' por cada set de data.staticConstraintSets con type 1 o 13:
        '''     por cada enlace (particleA, particleB, restLength):
        '''         si restLength² · stuckParticlesStretchFactorSq &lt; |pos[b] − pos[a]|²
        '''             planos[a] = 0 ; planos[b] = 0                      ' 0x141A14689 / 0x141A1469D
        ''' ```</para>
        ''' <para>⛔ Va DESPUÉS de armar los planos, no antes: lo que borra es el resultado.</para>
        ''' </summary>
        Friend Sub LiberarPegadas(inst As Instancia, habilitado As Boolean, factor As Single,
                                  enlaces As List(Of EnlaceDeTerreno), acum As Single())
            If Not habilitado OrElse enlaces Is Nothing Then Return       ' 0x14195DF14/1B
            For Each e In enlaces
                If e.A < 0 OrElse e.B < 0 Then Continue For
                Dim d = Vector128.Subtract(Simd.Leer(inst.Posiciones, e.B),
                                           Simd.Leer(inst.Posiciones, e.A))   ' 0x141A1463D/42
                Dim d2 = Simd.Lane0(Simd.Dot3(d, d))                      ' 0x141A14647-5F
                If e.Reposo * e.Reposo * factor < d2 Then                 ' 0x141A14635/69/6D
                    Simd.Escribir(acum, e.A, Vector128(Of Single).Zero)
                    Simd.Escribir(acum, e.B, Vector128(Of Single).Zero)
                End If
            Next
        End Sub

        ' -----------------------------------------------------------------------------------------
        ' El orquestador — 0x14195DA70
        ' -----------------------------------------------------------------------------------------

        ''' <summary>
        ''' `hclSimClothInstance::computeContactPlanes`, en el orden del binario.
        ''' <para>⛔ NADIE LO LLAMA todavía: hace falta que alguien traiga la geometría del mundo.
        ''' Está escrito porque es de la lista cerrada del capítulo 16, no porque tenga
        ''' consumidor.</para>
        ''' </summary>
        Friend Function ComputarPlanosDeContacto(
                inst As Instancia, mundo As TerrenoDelMundo, numParticulasDeTerreno As Integer,
                radio As Single, detectarPegadas As Boolean, factorDePegado As Single,
                enlaces As List(Of EnlaceDeTerreno)) As Single()
            If Not Habilitado(inst, mundo, numParticulasDeTerreno, True) Then Return Nothing

            ' el scratch: un `vec4` por partícula MÁS CUATRO de guarda (`0x14195DB1C`, `esi + 4`)
            Dim acum((inst.NumParticulas + 4) * 4 - 1) As Single

            Dim cajaLo As Vector128(Of Single) = Nothing
            Dim cajaHi As Vector128(Of Single) = Nothing
            CajaDeTrabajo(mundo, cajaLo, cajaHi)
            ' 0x141A140B5/C4: la caja se ensancha por el margen global ANTES de filtrar partículas
            Dim m = Vector128.Create(mundo.Margen)
            Dim expLo = Vector128.Subtract(cajaLo, m)
            Dim expHi = Vector128.Add(cajaHi, m)

            Dim q = HacerCuantizador(inst.AabbMinMascara, inst.AabbMaxMascara)
            Dim ep = EntradasDeParticula(inst, q, expLo, expHi, mundo.Margen)
            If ep.Count = 0 Then Return acum                              ' 0x14195DC23/32
            Ordenar(ep)

            If mundo.Triangulos.Length > 0 Then
                Dim et = EntradasDeTriangulo(mundo.Triangulos, q)
                Ordenar(et)
                ColisionarTriangulos(inst, mundo, radio, Pares(ep, et), acum)
            End If
            If mundo.Convexos.Length > 0 Then
                ColisionarConvexos(inst, mundo, radio, ep, acum)
            End If

            PlanosFinales(inst, radio, acum)
            LiberarPegadas(inst, detectarPegadas, factorDePegado, enlaces, acum)
            Return acum
        End Function

    End Module

    ''' <summary>Un par `(partícula, triángulo)` que el sweep-and-prune dejó pasar.</summary>
    Friend Structure ParDeTerreno
        Friend ReadOnly Particula As Integer
        Friend ReadOnly Objeto As Integer
        Friend Sub New(particula As Integer, objeto As Integer)
            Me.Particula = particula
            Me.Objeto = objeto
        End Sub
    End Structure

    ''' <summary>Un enlace de `hclStandardLinkConstraintSetLink` visto por `releaseStuckParticles`:
    ''' `{particleA uint16 @0, particleB uint16 @2, restLength real @4}`, 12 B.</summary>
    Friend Structure EnlaceDeTerreno
        Friend ReadOnly A As Integer
        Friend ReadOnly B As Integer
        Friend ReadOnly Reposo As Single
        Friend Sub New(a As Integer, b As Integer, reposo As Single)
            Me.A = a
            Me.B = b
            Me.Reposo = reposo
        End Sub
    End Structure

End Namespace

#End If
