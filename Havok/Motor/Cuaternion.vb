Option Strict On
Option Explicit On

Imports System.Runtime.CompilerServices
Imports System.Runtime.Intrinsics

' =================================================================================================
' LOS CUATERNIONES DEL MOTOR, y el MAPA EXPONENCIAL con el que gira los colisionables.
'
' Ley: Tools/re-docs/RE_MOTOR_FISICA_CANONICO_2026-09-05.md, cap. 6.7bis (y su vuelta 2, 6.7ter).
'
' ⛔ Un cuaternión acá es un `Vector128(Of Single)` con el layout del motor: **(x, y, z, w)**, la
' parte escalar en la lane 3. Se ve directo en `0x14135EEAE` (`shufps 0x39` deja la `w` última) y en
' `0x141365AA2` (`shufps 0xFE` toma la `w` de la lane 3 tres veces).
'
' ⛔ Las cuatro rutinas de acá salen de tres sitios distintos del `.exe` y NO comparten convención de
' normalización: el mapa exponencial normaliza las CUATRO componentes SIN guarda (0x14195CADD),
' mientras que el eigensolver normaliza filas de 3 CON guarda (0x141361096). No unificarlas.
' =================================================================================================


Namespace Havok.Motor

    Friend Module Cuaternion

        ' -----------------------------------------------------------------------------------------
        ' Los tres coeficientes del polinomio de W, cargados FUERA del bucle en `Simulate`
        ' -----------------------------------------------------------------------------------------

        ''' <summary>`0,333528221` — `0x14270BDE4`, cargada en `xmm13` en `0x14195C920`.</summary>
        Friend ReadOnly C1 As Single = BitConverter.Int32BitsToSingle(&H3EAAC436)

        ''' <summary>`0,0214401316` — `0x14270BDE0`, cargada en `xmm14` en `0x14195C932`.</summary>
        Friend ReadOnly C2 As Single = BitConverter.Int32BitsToSingle(&H3CAFA337)

        ''' <summary>`0,00295625487` — `0x14270BDDC`, cargada en `xmm15` en `0x14195C944`.</summary>
        Friend ReadOnly C3 As Single = BitConverter.Int32BitsToSingle(&H3B41BDBA)

        ' -----------------------------------------------------------------------------------------
        ' El mapa exponencial — 0x14195C9DF … 0x14195CB09
        ' -----------------------------------------------------------------------------------------

        ''' <summary>
        ''' El giro que el motor aplica a un colisionable en un substep: parte del cuaternión `q` del
        ''' transform y le premultiplica el incremento `dq` que sale de la velocidad angular.
        ''' <para>`v = angVel · (dtSub/2)` (`0x14195C9E9`, con `dtSub/2` ya difundido en `xmm9`) ·
        ''' `t = |v|²` (`0x14195C9F5`-`0x14195CA0D`, la misma forma de `Dot3`) ·
        ''' `W = 1 − C1·t − C2·t² − C3·t³` (`0x14195CA16`-`0x14195CA44`) ·
        ''' `dq = (v.x, v.y, v.z, W)` (`0x14195CA37` `shufps 0x93`, `movss`, `0x14195CA51`
        ''' `shufps 0x39`) · `q' = dq ⊗ q` (`0x14195CA55`-`0x14195CAC2`) ·
        ''' `q' = q'/|q'|` sobre las CUATRO componentes (`0x14195CAC3`-`0x14195CB06`).</para>
        ''' <para>⭐ La forma cerrada es `W(t) = θ·cot θ` con `t = θ²` y `θ = |ω|·dtSub/2`: el motor
        ''' deja la parte vectorial **sin escalar por `sin θ/θ`** y lo compensa con el escalar más la
        ''' normalización, que sale más barato que evaluar `sin` y `cos`. Por eso el resultado tiene
        ''' que coincidir con `(ω̂·sin θ, cos θ)` — y ésa es la invariante que lo falsea (G17).</para>
        ''' <para>⛔ La normalización de acá es de **4 componentes y SIN guarda** de norma nula: es
        ''' `rsqrtps` + UNA Newton y nada más (`0x14195CADD`). No tiene el `cmpleps` del eigensolver.
        ''' Ponérselo sería inventar; con `q'` nulo el motor produce NaN y ése es el motor.</para>
        ''' </summary>
        ''' <param name="q">El cuaternión actual del colisionable, `(x, y, z, w)`.</param>
        ''' <param name="angVel">La velocidad angular del buffer (`+0x70`), en el mismo `vec4`.</param>
        ''' <param name="medioDt">`dtSub/2`, ya difundido en las cuatro lanes (`xmm9`).</param>
        Friend Function MapaExponencial(q As Vector128(Of Single), angVel As Vector128(Of Single),
                                        medioDt As Vector128(Of Single)) As Vector128(Of Single)
            Dim v = Vector128.Multiply(angVel, medioDt)          ' 0x14195C9E9 mulps xmm1, xmm9

            ' t = |v|² con la forma exacta del motor: (y + x) + z, difundido en las 4 lanes
            Dim t = Simd.Dot3(v, v)                              ' 0x14195C9F5…0x14195CA0D
            Dim t1 = Simd.Lane0(t)

            ' W = 1 − C1·t − C2·t² − C3·t³, restando UNO POR VEZ y en ese orden (0x14195CA1F/40/44)
            Dim t2 = t1 * t1                                     ' 0x14195CA1B mulss xmm2, xmm4
            Dim t3 = t1 * t2                                     ' 0x14195CA23 mulss xmm4, xmm2
            Dim w = 1.0F - t1 * C1                               ' 0x14195CA16 + 0x14195CA1F
            w -= t2 * C2                                         ' 0x14195CA32 + 0x14195CA40
            w -= t3 * C3                                         ' 0x14195CA3B + 0x14195CA44

            ' dq = (v.x, v.y, v.z, W) — el motor lo arma rotando la lane de W hasta la 3
            Dim dq = v.WithElement(Simd.LaneW, w)                ' 0x14195CA37/48/51

            Return NormalizarSinGuarda(Producto(dq, q))
        End Function

        ''' <summary>
        ''' El producto de cuaterniones tal como está inline en `Simulate`
        ''' (`0x14195CA55`-`0x14195CAC2`): `(a ⊗ b).v = a.w·b.v + b.w·a.v + a.v × b.v` y
        ''' `(a ⊗ b).w = a.w·b.w − a.v · b.v`.
        ''' <para>⛔ El motor calcula el cruz **rotado**: el `mulps`/`subps` de `0x14195CA6C`/`82` deja
        ''' `(cruz.z, cruz.x, cruz.y, 0)` y recién el `shufps 0xC9` de `0x14195CA94` lo endereza. Se
        ''' transcribe con esa forma; escribir el cruz «derecho» cambia el orden de las restas.</para>
        ''' <para>El escalar sale por el camino largo —`a·b` componente a componente y después tres
        ''' difusiones y dos sumas (`0x14195CA9B`-`0x14195CAB6`)— que es **la misma forma que
        ''' <see cref="Simd.Dot3"/>**: `(y + x) + z`.</para>
        ''' </summary>
        Friend Function Producto(a As Vector128(Of Single), b As Vector128(Of Single)) As Vector128(Of Single)
            ' cruz rotado: (a.x·b.y − a.y·b.x, a.y·b.z − a.z·b.y, a.z·b.x − a.x·b.z, 0)
            Dim bYzx = Vector128.Shuffle(b, Vector128.Create(1, 2, 0, 3))   ' 0x14195CA58 shufps 0xC9
            Dim aYzx = Vector128.Shuffle(a, Vector128.Create(1, 2, 0, 3))   ' 0x14195CA5F shufps 0xC9
            Dim cr = Vector128.Subtract(Vector128.Multiply(bYzx, a),
                                        Vector128.Multiply(aYzx, b))        ' 0x14195CA6C/66/82
            cr = Vector128.Shuffle(cr, Vector128.Create(1, 2, 0, 3))        ' 0x14195CA94 shufps 0xC9

            Dim aW = Simd.BcastW(a)                                         ' 0x14195CA6F shufps 0xFF
            Dim bW = Simd.BcastW(b)                                         ' 0x14195CA73 shufps 0xFF

            ' parte vectorial: (a.v × b.v) + a.w·b + b.w·a   — en ESE orden de sumas
            Dim vec = Vector128.Add(cr, Vector128.Multiply(b, aW))          ' 0x14195CA8E + 0x14195CA98
            vec = Vector128.Add(vec, Vector128.Multiply(bW, a))             ' 0x14195CA85 + 0x14195CAA2

            ' parte escalar: a.w·b.w − dot3(a, b)
            Dim esc = Vector128.Subtract(Vector128.Multiply(bW, aW), Simd.Dot3(a, b))  ' 0x14195CA8B + 0x14195CAB9

            ' (vec.x, vec.y, vec.z, esc)  — 0x14195CABC unpckhps + 0x14195CABF shufps 0xC4
            Return vec.WithElement(Simd.LaneW, Simd.Lane0(esc))
        End Function

        ''' <summary>
        ''' ⭐ `a ⊗ conj(b)` **fundido**, tal como lo tiene inline `hclCollidable::setTransform`
        ''' (`0x1419606D2`-`0x141960734`). Es el giro que lleva de la pose vieja a la nueva.
        ''' <para>⛔⛔ **NO es `Producto(a, Conjugado(b))`.** La parte vectorial sí coincide bit a
        ''' bit —negar y restar son exactos en IEEE— pero la **escalar no**: acá sale de la suma
        ''' horizontal de las **cuatro** lanes de `a·b` (`0x141960718` `shufps 0x4E` y
        ''' `0x141960722` `shufps 0xB1`, o sea `(p0+p2) + (p1+p3)`), mientras que
        ''' <see cref="Producto"/> hace `a.w·b.w − ((y + x) + z)`. Distinto orden de sumas,
        ''' distintos bits. G17n lo mide.</para>
        ''' <para>```
        ''' t  = shuf(a,0xC9)·b − shuf(b,0xC9)·a       ' 0x1419606D5/D9/DF + E5/E9 + F1
        ''' t  = shuf(t,0xC9)                          ' 0x1419606FE
        ''' t  = t − a.w·b                             ' 0x1419606F7/FB + 0x141960702
        ''' t  = t + b.w·a                             ' 0x141960708/0C + 0x141960712
        ''' w  = hsum4(a·b)                            ' 0x14196070F + 0x141960718…26
        ''' ```</para>
        ''' </summary>
        Friend Function ProductoPorConjugado(a As Vector128(Of Single),
                                             b As Vector128(Of Single)) As Vector128(Of Single)
            Dim aYzx = Vector128.Shuffle(a, Vector128.Create(1, 2, 0, 3))   ' 0x1419606D5 shufps 0xC9
            Dim bYzx = Vector128.Shuffle(b, Vector128.Create(1, 2, 0, 3))   ' 0x1419606E5 shufps 0xC9
            Dim t = Vector128.Subtract(Vector128.Multiply(aYzx, b),
                                       Vector128.Multiply(bYzx, a))        ' 0x1419606DF/E9 + 0x1419606F1
            t = Vector128.Shuffle(t, Vector128.Create(1, 2, 0, 3))          ' 0x1419606FE shufps 0xC9
            t = Vector128.Subtract(t, Vector128.Multiply(Simd.BcastW(a), b))  ' 0x1419606F7/FB + 0x141960702
            t = Vector128.Add(t, Vector128.Multiply(Simd.BcastW(b), a))       ' 0x141960708/0C + 0x141960712

            ' la escalar: suma horizontal de las CUATRO lanes de a·b — 0x14196070F…0x141960726
            Dim p = Vector128.Multiply(a, b)                                ' 0x14196070F mulps
            Dim h = Vector128.Add(Vector128.Shuffle(p, Vector128.Create(2, 3, 0, 1)), p)   ' 0x141960718/1C
            h = Vector128.Add(Vector128.Shuffle(h, Vector128.Create(1, 0, 3, 2)), h)       ' 0x141960722/26

            ' (t.x, t.y, t.z, h) — 0x14196072C unpckhps + 0x141960734 shufps 0xC4
            Return t.WithElement(Simd.LaneW, Simd.Lane0(h))
        End Function

        ''' <summary>
        ''' Normaliza las **CUATRO** componentes con `rsqrtps` + UNA Newton y **sin guarda**
        ''' (`0x14195CAC3`-`0x14195CB06`; ctes `3,0` en `0x142629510` y `0,5` en `0x142629520`).
        ''' <para>⛔ La suma horizontal es de las cuatro lanes, con `shufps 0x4E` (cruza las mitades) y
        ''' después `shufps 0xB1` (cruza dentro de cada mitad): `((p0+p2) + (p1+p3))`. No es el
        ''' `Dot3` de tres componentes ni su orden de sumas.</para>
        ''' </summary>
        Friend Function NormalizarSinGuarda(q As Vector128(Of Single)) As Vector128(Of Single)
            Dim p = Vector128.Multiply(q, q)                                    ' 0x14195CAC6
            Dim s = Vector128.Add(Vector128.Shuffle(p, Vector128.Create(2, 3, 0, 1)), p)   ' 0x14195CACC/D0
            s = Vector128.Add(Vector128.Shuffle(s, Vector128.Create(1, 0, 3, 2)), s)       ' 0x14195CAD6/DA
            Return Vector128.Multiply(q, Simd.RsqrtNewton(s))                   ' 0x14195CADD…0x14195CB06
        End Function

        ' -----------------------------------------------------------------------------------------
        ' Cuaternión ⇄ matriz — 0x14135EE20 y 0x141365A80
        ' -----------------------------------------------------------------------------------------


        ''' <summary>
        ''' ⭐ El cuaternion de un eje y un angulo — `0x14135E700`, que difunde el angulo y llama a
        ''' `0x14135E730`, el **`sincos` SIMD de Cephes** que el motor lleva adentro.
        ''' <para>```
        ''' x    = angulo · 0,5                                  ' 0x14135E778 (0x142F3C650) — MEDIO angulo
        ''' sgn  = bit de signo de x  ;  a = |x|                 ' 0x14135E788/8C (0x142629470)
        ''' j    = trunc(a · 4/π)                                ' 0x14135E794/97 (0x1426294B0)
        ''' j    = (j + 1) AND ~1                                ' 0x14135E7A2/A7
        ''' sSen = ((j AND 4) &lt;&lt; 29) XOR sgn                    ' 0x14135E7C4/D5/E1
        ''' sCos = (~(j − 2) AND 4) &lt;&lt; 29                       ' 0x14135E7CB/D0/F4
        ''' sel  = j AND 2                                       ' 0x14135E7E8
        ''' y    = ((a + C1·j) + C2·j) + C3·j                    ' 0x14135E7DA…800 — Payne-Hanek en 3 pasos
        ''' z    = y²
        ''' sen  = ((S1·z + S2)·z + S3)·z·y + y                  ' 0x14135E80F…55
        ''' cos  = ((C4·z + C5)·z + C6)·z·z − z·0,5 + 1          ' 0x14135E819…6D
        ''' si sel = 0: se intercambian seno y coseno            ' 0x14135E85E…7F, con and/andn/or
        ''' sen = min(sen, 1) ; cos = min(cos, 1)                ' 0x14135E882/85
        ''' q   = ( (sen XOR sSen) · eje , cos XOR sCos )        ' 0x14135E888/8C/90/98/9B
        ''' ```</para>
        ''' <para>⛔ Los dos `minps` contra `1` (`0x14135E882`/`85`) **estan** y no son adorno: sin
        ''' ellos el polinomio puede pasarse de 1 y el cuaternion sale con norma &gt; 1.</para>
        ''' <para>⛔ La reduccion resta **tres** constantes que juntas son `π/4` en triple
        ''' precision. Con una sola el error crece con el angulo.</para>
        ''' <para>⛔ El eje NO se normaliza aca: el llamador lo trae unitario.</para>
        ''' </summary>
        Friend Function DeEjeAngulo(eje As Vector128(Of Single), angulo As Single) As Vector128(Of Single)
            Dim x = Vector128.Multiply(Vector128.Create(angulo), Vector128.Create(0.5F))  ' 0x14135E778
            Dim mascaraSigno = Vector128.Create(Integer.MinValue)
            Dim sgn = Vector128.BitwiseAnd(x.AsInt32(), mascaraSigno)                     ' 0x14135E78C
            Dim a = Vector128.AndNot(x, mascaraSigno.AsSingle())                          ' 0x14135E788

            Dim j = Vector128.ConvertToInt32(Vector128.Multiply(Vector128.Create(CuatroSobrePi), a))  ' 0x14135E794/97
            j = Vector128.BitwiseAnd(Vector128.Add(j, Vector128.Create(1)),
                                     Vector128.OnesComplement(Vector128.Create(1)))       ' 0x14135E7A2/A7
            Dim fj = Vector128.ConvertToSingle(j)                                         ' 0x14135E7B3

            Dim sSen = Vector128.Xor(
                Vector128.ShiftLeft(Vector128.BitwiseAnd(j, Vector128.Create(4)), 29), sgn)  ' 0x14135E7C4/D5/E1
            Dim sCos = Vector128.ShiftLeft(
                Vector128.AndNot(Vector128.Create(4), Vector128.Subtract(j, Vector128.Create(2))), 29)  ' 0x14135E7CB/D0/F4
            Dim sel = Vector128.BitwiseAnd(j, Vector128.Create(2))                        ' 0x14135E7E8

            Dim y = Vector128.Add(a, Vector128.Multiply(Vector128.Create(R1), fj))         ' 0x14135E7DA/E5/FA
            y = Vector128.Add(y, Vector128.Multiply(Vector128.Create(R2), fj))            ' 0x14135E79B/C1 + 0x14135E7FD
            y = Vector128.Add(y, Vector128.Multiply(Vector128.Create(R3), fj))            ' 0x14135E7AC/C8 + 0x14135E800

            Dim z = Vector128.Multiply(y, y)                                              ' 0x14135E806
            Dim medioZ = Vector128.Multiply(z, Vector128.Create(0.5F))                    ' 0x14135E820
            Dim sen = Vector128.Multiply(z, Vector128.Create(S1))                         ' 0x14135E80F
            sen = Vector128.Multiply(Vector128.Add(sen, Vector128.Create(S2)), z)         ' 0x14135E827/35
            sen = Vector128.Add(sen, Vector128.Create(S3))                                ' 0x14135E83B
            sen = Vector128.Add(Vector128.Multiply(Vector128.Multiply(sen, z), y), y)     ' 0x14135E849/4F/55
            Dim cos = Vector128.Multiply(z, Vector128.Create(C4))                         ' 0x14135E819
            cos = Vector128.Multiply(Vector128.Add(cos, Vector128.Create(C5)), z)         ' 0x14135E82E/38
            cos = Vector128.Add(cos, Vector128.Create(C6))                                ' 0x14135E842
            cos = Vector128.Subtract(Vector128.Multiply(Vector128.Multiply(cos, z), z), medioZ)  ' 0x14135E84C/52/58
            cos = Vector128.Add(cos, Vector128.Create(1.0F))                              ' 0x14135E86D

            ' el intercambio por cuadrante, con and/andnot/or — 0x14135E85E…0x14135E87F
            Dim m = Vector128.Equals(sel, Vector128(Of Integer).Zero).AsSingle()
            Dim t1 = Vector128.BitwiseAnd(m, sen)
            sen = Vector128.Subtract(sen, t1)
            Dim t2 = Vector128.AndNot(cos, m)
            cos = Vector128.Subtract(cos, t2)
            Dim senoFinal = Vector128.Add(t2, t1)                                         ' 0x14135E87C
            Dim cosFinal = Vector128.Add(sen, cos)                                        ' 0x14135E87F

            senoFinal = Vector128.Min(senoFinal, Vector128.Create(1.0F))                  ' 0x14135E882
            cosFinal = Vector128.Min(cosFinal, Vector128.Create(1.0F))                    ' 0x14135E885

            Dim v = Vector128.Multiply(
                Vector128.Xor(senoFinal.AsInt32(), sSen).AsSingle(), eje)                 ' 0x14135E888/8C
            Dim w = Vector128.Xor(cosFinal.AsInt32(), sCos).AsSingle()                    ' 0x14135E890
            Return v.WithElement(Simd.LaneW, Simd.Lane0(w))                               ' 0x14135E898/9B
        End Function

        ''' <summary>`4/π = 1,27323949` — `0x3FA2F983` en `0x1426294B0`.</summary>
        Private Const CuatroSobrePi As Single = 1.27323949F
        ''' <summary>`−0,78515625` — `0xBF490000` en `0x1426294C0`. La primera de las tres de la
        ''' reduccion; juntas suman `−π/4` en triple precision.</summary>
        Private Const R1 As Single = -0.78515625F
        ''' <summary>`−0,000241875648` — `0xB97DA000` en `0x1426294D0`.</summary>
        Private Const R2 As Single = -0.000241875648F
        ''' <summary>`−3,77489506e-08` — `0xB3222169` en `0x1426294E0`.</summary>
        Private Const R3 As Single = -3.77489506E-08F
        ''' <summary>`−0,000195152956` — `0xB94CA1F9` en `0x142629410`.</summary>
        Private Const S1 As Single = -0.000195152956F
        ''' <summary>`0,00833216123` — `0x3C08839E` en `0x142629420`.</summary>
        Private Const S2 As Single = 0.00833216123F
        ''' <summary>`−0,166666552` — `0xBE2AAAA3` en `0x142629430`.</summary>
        Private Const S3 As Single = -0.166666552F
        ''' <summary>`2,44331568e-05` — `0x37CCF5CE` en `0x142629440`.</summary>
        Private Const C4 As Single = 2.44331568E-05F
        ''' <summary>`−0,00138873165` — `0xBAB6061A` en `0x142629450`.</summary>
        Private Const C5 As Single = -0.00138873165F
        ''' <summary>`0,0416666456` — `0x3D2AAAA5` en `0x142629460`.</summary>
        Private Const C6 As Single = 0.0416666456F

        ''' <summary>
        ''' El cuaternión de una matriz de rotación — `0x14135EE20`, con sus **dos ramas**.
        ''' <para>Rama de la traza (`traza &gt; 0`, `0x14135EE48` `comiss`/`jbe`):
        ''' `s = √(traza+1)` (`sqrtss` EXACTO), `w = s/2`, `k = 0,5/s` (`divss` EXACTO) y
        ''' `(x, y, z) = ((m12−m21), (m20−m02), (m01−m10)) · k`.</para>
        ''' <para>Rama del mayor de la diagonal (`0x14135EEBB`): elige `i` = argmax de la diagonal con
        ''' **dos `cmova` encadenados** (`m11 &gt; m00`, después `m22 &gt; m_ii`), toma `j = (i+1) mod 3`
        ''' y `k = (j+1) mod 3` de una tabla `{1,2,0}` armada en la pila (`0x14135EEC2`), y hace
        ''' `s = √(m_ii − m_jj − m_kk + 1)`, `q[i] = s/2`, `q[3] = (m_jk − m_kj)·(0,5/s)`,
        ''' `q[j] = (m_ij + m_ji)·(0,5/s)`, `q[k] = (m_ki + m_ik)·(0,5/s)`.</para>
        ''' <para>⛔ `√` y `/` son **exactos** en las dos ramas: `sqrtss` en `0x14135EE74` y
        ''' `0x14135EF28`, `divss` en `0x14135EE82` y `0x14135EF2F`. Nada de `rsqrt` acá.</para>
        ''' </summary>
        Friend Function DeMatriz(m As Mat3) As Vector128(Of Single)
            Dim traza = m.E(0, 0) + m.E(1, 1) + m.E(2, 2)       ' 0x14135EE3A + 0x14135EE44
            If traza > 0.0F Then                                ' 0x14135EE48 comiss / jbe
                Dim s = Simd.SqrtExacta(traza + 1.0F)           ' 0x14135EE4D + 0x14135EE74
                Dim k = 0.5F / s                                ' 0x14135EE82 divss
                Return Vector128.Create((m.E(1, 2) - m.E(2, 1)) * k,
                                        (m.E(2, 0) - m.E(0, 2)) * k,
                                        (m.E(0, 1) - m.E(1, 0)) * k,
                                        s * 0.5F)               ' 0x14135EE86 mulss xmm4, 0.5
            End If

            ' i = argmax de la diagonal, con los dos `cmova` en ese orden exacto
            Dim i As Integer = 0                                ' 0x14135EEBB xor edx, edx
            If m.E(1, 1) > m.E(0, 0) Then i = 1                 ' 0x14135EED0/D3 comiss + cmova
            If m.E(2, 2) > m.E(i, i) Then i = 2                 ' 0x14135EEDA/DF comiss + cmova
            Dim j = Siguiente(i)                                ' tabla {1,2,0} en 0x14135EEC2
            Dim k2 = Siguiente(j)

            Dim s2 = Simd.SqrtExacta(m.E(i, i) - (m.E(k2, k2) + m.E(j, j)) + 1.0F)   ' 0x14135EF28
            Dim inv = 0.5F / s2                                 ' 0x14135EF2F divss

            Dim r(3) As Single
            r(i) = s2 * 0.5F                                    ' 0x14135EF33 mulss xmm2, 0.5
            r(3) = (m.E(j, k2) - m.E(k2, j)) * inv              ' 0x14135EF41/51/55  ⬅ la w
            r(j) = (m.E(i, j) + m.E(j, i)) * inv                ' 0x14135EF65/6F/73
            r(k2) = (m.E(k2, i) + m.E(i, k2)) * inv             ' 0x14135EF7F/85/89
            Return Vector128.Create(r(0), r(1), r(2), r(3))
        End Function

        ''' <summary>`(i + 1) mod 3` — la tabla `{1, 2, 0}` que `0x14135EEC2` arma en la pila.</summary>
        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Private Function Siguiente(i As Integer) As Integer
            Return If(i = 2, 0, i + 1)
        End Function

        ''' <summary>
        ''' La matriz de rotación de un cuaternión — `0x141365A80`, en convención de **fila-vector**
        ''' (la de Havok): la fila 0 es la imagen del eje X.
        ''' <para>El motor lo hace con dos máscaras de signo (`0x142629E60` = `(0,−0,−0,−0)` y
        ''' `0x142629E70` = `(−0,0,−0,−0)`) sobre dos productos difundidos, y arma las tres filas con
        ''' `shufps 0xB1`, `movhlps` y `shufps 0x8D`. Acá se escribe la matriz que sale de esa
        ''' cuenta, elemento por elemento, porque el orden de las sumas es el mismo y la forma SIMD
        ''' no aporta nada legible.</para>
        ''' <para>⛔⛔ La `w` de cada fila NO es cero: el motor escribe las 16 B de cada fila y
        ''' las lanes w salen de la misma cuenta (derivadas lane por lane):
        '''   fila 0: (−(w·2x)) − (−(2z·y))  — `0x141365ACC` xorps + `0x141365ADE` subps
        '''   fila 1: (w·2y) − (−(2z·x))     — `0x141365AEF` xorps + `0x141365AF6` subps + `0x141365AF9` shufps 0xB1
        '''   fila 2: 0                      — `0x141365B00` shufps 0x8D toma la lane 2 de (1,0,0,0)
        ''' Hay quien las SUMA (`0x141339F90`) y quien las compone: medido contra la emulación de
        ''' `SubstepCollidables` (GDFi4bw). Por eso hay UNA sola `AMatriz` y trae las w.</para>
        ''' </summary>
        Friend Function AMatriz(q As Vector128(Of Single)) As Mat3
            ' ⛔⛔ TRANSCRIPCIÓN INSTRUCCIÓN POR INSTRUCCIÓN de `0x141365A80`. La forma escalar daba los
            ' mismos números finitos pero NO el mismo NaN: el motor niega con `xorps` contra máscaras
            ' y un NaN sale con otro bit de signo (GDFt3b/3c). Las restas de la diagonal quedan en el
            ' orden del motor por construcción (motor-37):
            '   fila 0: (−2z² + 1) − 2y²  · fila 1: (−2z² + 1) − 2x²  · fila 2: (1 − 2x²) − 2y²
            Dim m60 = Vector128.Create(0UI, &H80000000UI, &H80000000UI, &H80000000UI).AsSingle()   ' 0x142629E60
            Dim m70 = Vector128.Create(&H80000000UI, 0UI, &H80000000UI, &H80000000UI).AsSingle()   ' 0x142629E70
            Dim x3 = Sse(q, q, 0)                                        ' 0x141365A9C addps
            Dim x0 = Shufps(q, q, &H41)                                         ' 0x141365A98
            Dim x4 = Vector128.WithElement(q, 0, Sse(q, x3, 2).GetElement(0))   ' 0x141365AA6 mulss
            Dim x6 = Shufps(q, q, &HFE)                                         ' 0x141365AA2
            Dim x1 = Sse(Shufps(x3, x3, &HA5), x0, 2)               ' 0x141365AAD + 0x141365AB1
            Dim uno = Vector128.Create(1.0F, 0.0F, 0.0F, 0.0F)                  ' 0x142F3C700
            x3 = Shufps(x3, x3, &H1A)                                           ' 0x141365ABE
            x6 = Sse(x6, x3, 2)                                     ' 0x141365AC2
            Dim x5 = Vector128.WithElement(uno, 0, Sse(uno, x4, 1).GetElement(0))   ' 0x141365AC5 subss
            Dim x2 = Vector128.Xor(m60, x1)                                     ' 0x141365AC9
            x6 = Vector128.Xor(x6, m70)                                         ' 0x141365ACC
            x5 = Vector128.WithElement(x5, 0, Sse(x5, x1, 1).GetElement(0))   ' 0x141365AD3 subss
            x6 = Vector128.WithElement(x6, 0, Sse(x6, uno, 0).GetElement(0))  ' 0x141365AD7 addss
            Dim f0 = Sse(x6, x2, 1)                                 ' 0x141365ADE → [rcx]
            x2 = Vector128.WithElement(x2, 0, x4.GetElement(0))                 ' 0x141365AE1 movss
            Dim y0 = Sse(Vector128.Xor(m60, x6), x2, 1)             ' 0x141365AEF xorps + 0x141365AF6 subps
            Dim f1 = Shufps(y0, y0, &HB1)                                       ' 0x141365AF9 → [rcx+0x10]
            Dim hl = Vector128.Create(f1.GetElement(2), f1.GetElement(3),
                                      f0.GetElement(2), f0.GetElement(3))       ' 0x141365AFD movhlps
            Dim r As Mat3
            r.F0 = f0
            r.F1 = f1
            r.F2 = Shufps(hl, x5, &H8D)                                         ' 0x141365B00 → [rcx+0x20]
            Return r
        End Function

        ''' <summary>La aritmética de `addps`/`subps`/`mulps` (op 0/1/2) con el NaN que sale en el
        ''' procesador: si el primer operando es NaN sale ése (silenciado), si no el del segundo.
        ''' Escrito lane por lane para que el orden de los operandos no dependa del JIT.
        ''' <para>⛔ MEDIDO en la CPU, no supuesto: los bytes de `0x141365A80` ejecutados nativos con
        ''' q = NaN dan la fila 0 `(7FC, FFC, 7FC, 7FC)…`, lo mismo que esto; unicorn da otro signo
        ''' (su regla de NaN es la de QEMU). Los fixtures de GDFt3 corren esos leaf en la CPU
        ''' (`emu.leaf_nativo`).</para></summary>
        Private Function Sse(a As Vector128(Of Single), b As Vector128(Of Single), op As Integer) As Vector128(Of Single)
            Dim r As Vector128(Of Single)
            For i = 0 To 3
                Dim x = a.GetElement(i), y = b.GetElement(i), v As Single
                If Single.IsNaN(x) Then
                    v = Silenciar(x)
                ElseIf Single.IsNaN(y) Then
                    v = Silenciar(y)
                Else
                    v = If(op = 0, x + y, If(op = 1, x - y, x * y))
                    ' un NaN nuevo (inf − inf, 0 · inf) es el «indefinido» de x86: 0xFFC00000
                    If Single.IsNaN(v) Then v = BitConverter.Int32BitsToSingle(&HFFC00000)
                End If
                r = r.WithElement(i, v)
            Next
            Return r
        End Function

        Private Function Silenciar(x As Single) As Single
            Return BitConverter.Int32BitsToSingle(BitConverter.SingleToInt32Bits(x) Or &H400000)
        End Function

        ''' <summary>`shufps a, b, imm`: lanes 0-1 de `a`, lanes 2-3 de `b`, por los pares de bits de
        ''' `imm`. Copia bits, no hace aritmética.</summary>
        Private Function Shufps(a As Vector128(Of Single), b As Vector128(Of Single), imm As Integer) As Vector128(Of Single)
            Return Vector128.Create(a.GetElement(imm And 3), a.GetElement((imm >> 2) And 3),
                                    b.GetElement((imm >> 4) And 3), b.GetElement((imm >> 6) And 3))
        End Function

        ''' <summary>
        ''' ⭐⭐ `2·acos(|q.w|)` con el **`asin` minimax de cinco coeficientes y SIN RAMAS** del
        ''' motor — `0x14135FA20`, 69 instrucciones, todas con máscaras.
        ''' <para>Lo usa `hclCollidable::setTransform` (`0x1419605F0`, RE 6.9) para sacar el
        ''' eje-ángulo de `qN ⊗ qV⁻¹`, o sea **la velocidad angular de cada colisionable de cada
        ''' frame**. Sin esto el paso 4a no tiene qué integrar.</para>
        ''' <para>```
        ''' c      = (w &lt;&lt; 1) &gt;&gt; 1                          ' 0x14135FA62/68 — |w| por BITS
        ''' signo  = c AND 0x80000000  = SIEMPRE 0          ' 0x14135FA72 — sobre c, no sobre w
        ''' c      = min(1,0 , c)                           ' 0x14135FA7B minps — CLAMP
        ''' chico  = (c &lt; 9,99999975e-05)                   ' 0x14135FA84
        ''' grande = (0,5 &lt; c)                              ' 0x14135FA8F
        ''' s      = grande ? sqrt((1−c)·0,5) : c           ' 0x14135FAA2 sqrtps EXACTO
        ''' x      = s²                                     ' 0x14135FAAE/B1/B4
        ''' p      = ((((K1·x + K2)·x + K3)·x + K4)·x + K5)·x    ' 0x14135FABA…E6
        ''' p      = p·s + s                                ' 0x14135FAF3/F6
        ''' ang    = grande ? (π/2 − 2·p) : p               ' 0x14135FAF9…FF
        ''' ang    = chico  ? c : ang                       ' 0x14135FB05/16/19
        ''' ang    = ang XOR signo                          ' 0x14135FB21
        ''' return (π/2 − ang) · 2                          ' 0x14135FB2B/2E/31
        ''' ```</para>
        ''' <para>⛔ **No es `MathF.Acos`**: es una aproximación con su error propio, y ese error
        ''' entra en la velocidad angular de todos los colisionables. Reemplazarla por la exacta es
        ''' «arreglar» el motor.</para>
        ''' <para>⛔ Y `|w|` sale de **borrar el bit de signo** (`pslld`/`psrld`), no de `Abs`: con
        ''' un `−NaN` las dos cosas no dan lo mismo.</para>
        ''' <para>⛔⛔ **El signo de `w` NO se aplica**, y no es una simplificacion mia: el
        ''' `andps xmm8, xmm9` de `0x14135FA72` corre **despues** del `pslld`/`psrld` que le borro
        ''' el bit 31 a `xmm9`, asi que `xmm8` llega en cero al `xorps` de `0x14135FB21` — medido
        ''' sobre los **4.294.967.296** patrones de 32 bits. El resultado esta siempre en
        ''' `[0, π]`.</para>
        ''' <para>⭐ Y por eso `setTransform` sale bien: con `qd.w &lt; 0` el angulo corto es
        ''' `2π − φ` y el motor le da vuelta el **eje** (`0x1419607DA`); la composicion de las dos
        ''' cosas es el giro `φ` original.</para>
        ''' </summary>
        Friend Function Angulo(q As Vector128(Of Single)) As Single
            Dim w = Simd.BcastW(q)                                     ' 0x14135FA59/5D
            ' |w| borrando el bit de signo: pslld 1 / psrld 1        ' 0x14135FA62/68
            Dim absBits = Vector128.ShiftRightLogical(Vector128.ShiftLeft(w.AsInt32(), 1), 1)
            ' ⛔⛔ EL SIGNO SALE DEL VALOR YA ENMASCARADO. `0x14135FA72 andps xmm8, xmm9` corre
            ' DESPUES del pslld/psrld, asi que `signo` es CERO para los 2^32 patrones de bits
            ' (medido, los 4.294.967.296) y el `xorps` de 0x14135FB21 es CODIGO MUERTO.
            ' Se transcribe igual, con la forma del motor, para que la muerte se vea.
            Dim signo = Vector128.BitwiseAnd(absBits, Vector128.Create(Integer.MinValue))
            Dim c = absBits.AsSingle()
            c = Vector128.Min(Vector128.Create(1.0F), c)               ' 0x14135FA7B
            Dim uno = Vector128.Create(1.0F)
            Dim medio = Vector128.Create(0.5F)

            Dim chico = Vector128.LessThan(c, Vector128.Create(9.99999975E-05F))   ' 0x14135FA84
            Dim grande = Vector128.LessThan(medio, c)                              ' 0x14135FA8F

            Dim mitad = Vector128.Multiply(Vector128.Subtract(uno, c), medio)      ' 0x14135FA7E/8C
            Dim s = Vector128.ConditionalSelect(grande, Simd.SqrtPacked(mitad), c) ' 0x14135FAA2/A5/A8
            Dim x = Vector128.ConditionalSelect(grande, mitad, Vector128.Multiply(c, c))

            Dim p = Vector128.Multiply(x, Vector128.Create(K1))                    ' 0x14135FABA
            p = Vector128.Multiply(Vector128.Add(p, Vector128.Create(K2)), x)      ' 0x14135FAC1/C8
            p = Vector128.Multiply(Vector128.Add(p, Vector128.Create(K3)), x)      ' 0x14135FACB/D2
            p = Vector128.Multiply(Vector128.Add(p, Vector128.Create(K4)), x)      ' 0x14135FAD5/DC
            p = Vector128.Multiply(Vector128.Add(p, Vector128.Create(K5)), x)      ' 0x14135FADF/E6
            p = Vector128.Add(Vector128.Multiply(p, s), s)                         ' 0x14135FAF3/F6

            Dim vMedioPi = Vector128.Create(MedioPi)
            Dim conGrande = Vector128.Subtract(vMedioPi, Vector128.Add(p, p))       ' 0x14135FAFC/FF
            Dim ang = Vector128.ConditionalSelect(grande, conGrande, p)            ' 0x14135FB0D/10/13
            ang = Vector128.ConditionalSelect(chico, c, ang)                       ' 0x14135FB05/16/19
            ang = Vector128.Xor(ang.AsInt32(), signo).AsSingle()                   ' 0x14135FB21
            Dim r = Vector128.Subtract(vMedioPi, ang)                               ' 0x14135FB2B
            Return Simd.Lane0(Vector128.Add(r, r))                                 ' 0x14135FB2E
        End Function

        ''' <summary>`0,0421632007` — `0x3D2CB352` en `0x14262FC20`.</summary>
        Private Const K1 As Single = 0.0421632007F
        ''' <summary>`0,024181312` — `0x3CC617E3` en `0x14262FC30`.</summary>
        Private Const K2 As Single = 0.024181312F
        ''' <summary>`0,0454700254` — `0x3D3A3EC7` en `0x14262FC40`.</summary>
        Private Const K3 As Single = 0.0454700254F
        ''' <summary>`0,0749530047` — `0x3D9980F6` en `0x14262FC50`.</summary>
        Private Const K4 As Single = 0.0749530047F
        ''' <summary>`0,166667521` — `0x3E2AAAE4` en `0x14262FC60`. ⭐ Es `1/6` con el último dígito
        ''' corrido: el término líder de la serie de `asin`.</summary>
        Private Const K5 As Single = 0.166667521F
        ''' <summary>`π/2 = 1,57079637` — `0x3FC90FDB` en `0x1426294A0`.</summary>
        Private Const MedioPi As Single = 1.57079637F


    End Module

End Namespace

