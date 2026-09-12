Option Strict On
Option Explicit On

Imports System.Runtime.CompilerServices
Imports System.Runtime.Intrinsics

' =================================================================================================
' La matriz 3×3 del motor y la DESCOMPOSICIÓN POLAR de la fase 2 de `hclVolumeConstraint(Mx)`.
'
' Ley: Tools/re-docs/RE_MOTOR_FISICA_CANONICO_2026-09-05.md, caps. 6q.3, 6q.4 y 6q.5.
'
' ⛔⛔ ESTA ES UNA DE LAS DOS RUTINAS QUE YA FALLARON UNA VEZ EN ESTE ÁRBOL. La ortonormalización
' del deform salió con dos filas intercambiadas — 180° exactos — y el gate que sólo medía traslación
' no lo vio. Por eso cada función de acá viene con la invariante que la falsea, y `PolarGate` las
' corre TODAS: sin ese gate, este archivo no se acepta.
'
' ⛔ El eigensolver NO es «un Jacobi cíclico». Se transcribió instrucción por instrucción de
' `0x141360C70`, y tiene tres cosas que un Jacobi de libro hace distinto (ver `EigenSimetrico`).
' =================================================================================================


Namespace Havok.Motor

    ''' <summary>
    ''' Una matriz 3×3 con el layout del motor: **tres filas de 16 B**, la `w` de cada fila sin usar.
    ''' Convención de fila-vector, igual que Havok: `v · M` transforma la fila `v`.
    ''' </summary>
    Friend Structure Mat3
        Friend F0 As Vector128(Of Single)
        Friend F1 As Vector128(Of Single)
        Friend F2 As Vector128(Of Single)

        Friend Shared ReadOnly Property Identidad As Mat3
            Get
                Dim m As Mat3
                m.F0 = Vector128.Create(1.0F, 0.0F, 0.0F, 0.0F)
                m.F1 = Vector128.Create(0.0F, 1.0F, 0.0F, 0.0F)
                m.F2 = Vector128.Create(0.0F, 0.0F, 1.0F, 0.0F)
                Return m
            End Get
        End Property

        Friend Shared ReadOnly Property Cero As Mat3
            Get
                Dim m As Mat3
                m.F0 = Vector128(Of Single).Zero
                m.F1 = Vector128(Of Single).Zero
                m.F2 = Vector128(Of Single).Zero
                Return m
            End Get
        End Property

        ''' <summary>El elemento (fila, columna).</summary>
        Friend Function E(fila As Integer, col As Integer) As Single
            Select Case fila
                Case 0 : Return F0.GetElement(col)
                Case 1 : Return F1.GetElement(col)
                Case Else : Return F2.GetElement(col)
            End Select
        End Function

        Friend Sub SetE(fila As Integer, col As Integer, v As Single)
            Select Case fila
                Case 0 : F0 = F0.WithElement(col, v)
                Case 1 : F1 = F1.WithElement(col, v)
                Case Else : F2 = F2.WithElement(col, v)
            End Select
        End Sub
    End Structure

    Friend Module Polar

        ''' <summary>`FLT_EPSILON` = 1,1920929e-07. En el motor: `0x142F3C760` (el arranque de los
        ''' acumuladores de la fase 2) y `0x142468470` (la tolerancia del eigensolver).</summary>
        Friend Const FltEpsilon As Single = 1.1920929E-07F

        ''' <summary>
        ''' El tope de iteraciones del eigensolver. **No es una constante del eigensolver**: se lo
        ''' pasa el llamador como 5.º argumento de pila. El camino VIVO lo pone en `0x141A0A774`
        ''' (`mov dword ptr [rsp+0x20], 0x14`); el gemelo muerto, en `0x141A67497`. El bucle lo lee
        ''' en `0x141360DDE` (`mov edi, [rbp+0x80]`) y lo compara en `0x141360E25`.
        ''' <para>⚠️ CORRECCIÓN (motor-38): la primera redacción citaba `0x141360F2C` (`mov eax, 0x14`)
        ''' como «el bucle lo lee». **Falso**: ese `0x14` es el **desplazamiento en bytes** de
        ''' `J[1][1]` respecto de `J[0][0]` (16 + 4), y lo usa el `movss [rsp + rax + 0x20]` de
        ''' `0x141360F55` para escribir `J[p][p]`. El valor 20 coincidía por casualidad.</para>
        ''' </summary>
        Friend Const MaxIteracionesEigen As Integer = 20

        ' -----------------------------------------------------------------------------------------
        ' Los helpers de matriz del motor
        ' -----------------------------------------------------------------------------------------

        ''' <summary>Traspuesta EN EL LUGAR — `0x141360210`.</summary>
        Friend Function Transponer(m As Mat3) As Mat3
            Dim r As Mat3
            r.F0 = Vector128.Create(m.F0.GetElement(0), m.F1.GetElement(0), m.F2.GetElement(0), 0.0F)
            r.F1 = Vector128.Create(m.F0.GetElement(1), m.F1.GetElement(1), m.F2.GetElement(1), 0.0F)
            r.F2 = Vector128.Create(m.F0.GetElement(2), m.F1.GetElement(2), m.F2.GetElement(2), 0.0F)
            Return r
        End Function

        ''' <summary>
        ''' El producto del motor — `0x141360290(out, A, B)` produce `out.fila_k = B.fila_k · A`,
        ''' o sea **`out = B × A`** en convención de fila-vector.
        ''' <para>⛔ El orden importa y es al revés de lo que sugiere el orden de los argumentos del
        ''' `.exe`: acá se escribe explícito `Por(B, A)` = «cada fila de B, transformada por A».</para>
        ''' </summary>
        Friend Function Por(b As Mat3, a As Mat3) As Mat3
            Dim r As Mat3
            r.F0 = FilaPor(b.F0, a)
            r.F1 = FilaPor(b.F1, a)
            r.F2 = FilaPor(b.F2, a)
            Return r
        End Function

        ''' <summary>`v · M` con la misma forma que el motor: broadcast de cada componente por su
        ''' fila y suma en el orden `x·F0 + y·F1 + z·F2`.</summary>
        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Friend Function FilaPor(v As Vector128(Of Single), m As Mat3) As Vector128(Of Single)
            Dim r = Vector128.Multiply(Simd.BcastX(v), m.F0)
            r = Vector128.Add(r, Vector128.Multiply(Simd.BcastY(v), m.F1))
            r = Vector128.Add(r, Vector128.Multiply(Simd.BcastZ(v), m.F2))
            Return r
        End Function

        ''' <summary>
        ''' La inversa por adjunta/determinante, **con la guarda de singularidad del motor** —
        ''' `0x141360010`.
        ''' <para>⭐ Cuando NO se cumple `‖f0‖₁·‖f1‖₁·‖f2‖₁ · FLT_EPSILON &lt; |det|`, el motor devuelve
        ''' la matriz **CERO** (`0x141360148 cmpltps` + `0x14136014F andps`). Un marco degenerado
        ''' colapsa todo al centroide: eso es el motor, no un caso a «arreglar».</para>
        ''' <para>El determinante se calcula con `rcpps` + una Newton (`0x14136012B`); acá es división
        ''' exacta por la decisión SIMD, y la diferencia es de redondeo.</para>
        ''' <para>⚠️⚠️ **No devuelve la inversa: devuelve `(M⁻¹)ᵀ`** para una `M` cualquiera. Las tres
        ''' filas son los productos cruz de las otras dos, o sea `C · Mᵀ = det·I`, que es la adjunta
        ''' **sin trasponer**. El motor hace exactamente eso (`0x141360010`). Para la `S` simétrica de
        ''' la descomposición polar da lo mismo, y por eso pasa inadvertido; un kernel futuro que la
        ''' use sobre una matriz general saldría traspuesto. Medido en `GS1g` (motor-46).</para>
        ''' </summary>
        Friend Function Inversa(m As Mat3) As Mat3
            Dim c0 = Cruz(m.F1, m.F2)          ' adjunta: fila0 = f1 × f2
            Dim c1 = Cruz(m.F2, m.F0)
            Dim c2 = Cruz(m.F0, m.F1)
            Dim det = Simd.Lane0(Simd.Dot3(c0, m.F0))
            ' La guarda del motor, con las normas L1 de las tres filas.
            Dim n0 = NormaL1(m.F0), n1 = NormaL1(m.F1), n2 = NormaL1(m.F2)
            If Not (n0 * n1 * n2 * FltEpsilon < Math.Abs(det)) Then Return Mat3.Cero
            Dim inv = Vector128.Create(1.0F / det)
            Dim r As Mat3
            r.F0 = Vector128.Multiply(c0, inv)
            r.F1 = Vector128.Multiply(c1, inv)
            r.F2 = Vector128.Multiply(c2, inv)
            Return r
        End Function

        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Private Function NormaL1(v As Vector128(Of Single)) As Single
            Return Math.Abs(v.GetElement(0)) + Math.Abs(v.GetElement(1)) + Math.Abs(v.GetElement(2))
        End Function

        ''' <summary>
        ''' El producto cruz con la forma del motor: `shuf(b)·a − shuf(a)·b`, y el `shufps 0xC9`
        ''' final que lo endereza — `0x14136003E`-`0x14136007E` (la adjunta de la inversa) y
        ''' `0x14195CA58`-`0x14195CA94` (el cuaternión). Las dos formas del `.exe` son ésta.
        ''' <para>⛔⛔ **Estuvo AL REVÉS y nadie lo vio.** La primera versión hacía
        ''' `shuf(a)·b − shuf(b)·a`, que es `−cross(a,b)`. Pasó los 21 casos del gate porque el
        ''' único consumidor era <see cref="Inversa"/>, donde el signo **se cancela solo**: las tres
        ''' filas de la adjunta salen invertidas y el determinante `c0·F0` también, así que
        ''' `c/det` queda bien. Lo cazó la revisión adversarial leyendo el binario, no el gate
        ''' (motor-34). El primer kernel que lo usara para una NORMAL o un EJE —bend, deform, el
        ''' cono de la cápsula— habría salido espejado.</para>
        ''' <para>Control que lo mata hoy: `Cruz(x̂, ŷ) = ẑ` (GS1f).</para>
        ''' </summary>
        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Friend Function Cruz(a As Vector128(Of Single), b As Vector128(Of Single)) As Vector128(Of Single)
            Dim a1 = Vector128.Shuffle(a, Vector128.Create(1, 2, 0, 3))   ' 0x14136006F shufps 0xC9
            Dim b1 = Vector128.Shuffle(b, Vector128.Create(1, 2, 0, 3))   ' 0x141360039 shufps 0xC9
            Dim t = Vector128.Subtract(Vector128.Multiply(b1, a), Vector128.Multiply(a1, b))
            Return Vector128.Shuffle(t, Vector128.Create(1, 2, 0, 3))     ' 0x14136007E shufps 0xC9
        End Function

        ''' <summary>
        ''' La norma de Frobenius al cuadrado de una 3x3, **con el orden de sumas del motor** —
        ''' `0x141360D17`-`0x141360DB7`.
        ''' <para>Cada fila se suma con la forma de `Dot3` (`(y + x) + z`) y las tres filas se suman
        ''' **fila1 + fila0 + fila2**: `addps xmm11, xmm1` en `0x141360DA1` (la 0) y
        ''' `addps xmm11, xmm2` en `0x141360DB7` (la 2), sobre el acumulador que ya traía la 1
        ''' (`0x141360D37`-`0x141360D6B`).</para>
        ''' <para>⚠️ Es `Friend` para poder medirla DIRECTO. Su único consumidor es el umbral de
        ''' convergencia del eigensolver, y ahí el orden de las sumas sólo se nota si la diferencia
        ''' de 1 ulp llega a **dar vuelta la comparación** `umbral &gt; off2` — que en un barrido de
        ''' 24 matrices no pasó ni una vez. Sin medirla aparte, cambiar el orden pasa en verde
        ''' (mutación M41). Es el mismo caso que <see cref="RaizConGuarda"/>.</para>
        ''' </summary>
        Friend Function FrobeniusAlCuadrado(m As Mat3) As Single
            Dim f0 = Simd.Lane0(Simd.Dot3(m.F0, m.F0))
            Dim f1 = Simd.Lane0(Simd.Dot3(m.F1, m.F1))
            Dim f2 = Simd.Lane0(Simd.Dot3(m.F2, m.F2))
            Return (f1 + f0) + f2                          ' 0x141360DA1 + 0x141360DB7
        End Function

        ' -----------------------------------------------------------------------------------------
        ' El eigensolver simétrico — 0x141360C70
        ' -----------------------------------------------------------------------------------------

        ''' <summary>
        ''' Eigen de una matriz **simetrica** 3x3, transcrito de `0x141360C70`.
        ''' <para>NO es "un Jacobi ciclico", y tampoco arranca de la identidad. Cinco cosas que un
        ''' Jacobi de libro hace distinto y que aca NO se pueden cambiar:</para>
        ''' <para>1. ARRAN'UE EN CALIENTE. El tercer argumento (`r8`) es de **entrada y salida**:
        ''' entra un marco previo `V0` y la rutina empieza por conjugar, `A0 = V0 * M * V0t`
        ''' (`0x141360CB6` copia `[r8]`, `0x141360CE9` la traspone, `0x141360CFC` hace `M x V0t` y
        ''' `0x141360D09` premultiplica por `V0`). Con `V0 = I` queda el Jacobi de siempre; el motor
        ''' **no** le pasa la identidad (ver <see cref="FactorOrtogonal"/>).</para>
        ''' <para>2. El pivote es el elemento **fuera de la diagonal de mayor magnitud** (Jacobi
        ''' CLASICO), leido del triangulo **INFERIOR** - `a10` (`rsp+0x60`), `a20` (`rsp+0x70`) y
        ''' `a21` (`rsp+0x74`), `0x141360E2D`-`0x141360E90`.</para>
        ''' <para>3. El test de convergencia usa la norma de Frobenius de la **M ORIGINAL**, calculada
        ''' UNA vez sobre `[rbx]` = el 1.er argumento (`0x141360D17`-`0x141360DBE`), **no** sobre la
        ''' `A0` ya conjugada: sale cuando `frob2*tol^2 &gt; 2*(a10^2 + a20^2 + a21^2)`.</para>
        ''' <para>4. `theta` **no** se toma en valor absoluto: hay dos ramas, `1/(theta+r)` si
        ''' `theta &gt;= 0` y `1/(theta-r)` si no (`0x141360ED9` `jb`), y eso fija la **orientacion** de
        ''' los autovectores.</para>
        ''' <para>5. Los signos de la rotacion son `J[p][q] = -s` y `J[q][p] = +s` (`0x141360F6C`
        ''' `xorps xmm2, -0.0` justo antes de escribir `J[p][q]`), y la actualizacion es
        ''' `A &lt;- J*A*Jt` con `V &lt;- J*V` (`0x141360FA4` / `0x141360FB3` / `0x141360FC6`, los tres a
        ''' traves de `0x141360730`, que hace `X &lt;- Y x X`). **Con los signos al reves la rotacion
        ''' AGRANDA el elemento en vez de anularlo** y el solver agota las 20 vueltas: es exactamente
        ''' el defecto que cazo `G18b2` la primera vez que corrio este archivo.</para>
        ''' <para>Y `sqrt`/`div` son **exactos** en todo el bucle (`sqrtss`/`divss`); la unica
        ''' aproximacion del motor es la normalizacion final de las tres filas de `V` (`0x141361096`,
        ''' `rsqrtps` + UNA Newton, con guarda `cmpleps`).</para>
        ''' </summary>
        ''' <param name="m">La matriz simetrica de entrada (`rcx`).</param>
        ''' <param name="v">**Entra** el marco previo y **sale** con los autovectores por fila,
        ''' normalizados (`r8`). Es estado que vive entre cuadros: ver <see cref="FactorOrtogonal"/>.</param>
        ''' <param name="lambda">Sale: `(a00, a11, a22, a22)` de la `A` final (`r15`, `0x141361184`).</param>
        ''' <param name="noConvergio">Sale **1** si agoto `maxIter` sin bajar del umbral, **0** si
        ''' convergio (`rdx`, `0x1413611A5` / `0x1413611B7`). Es una BANDERA, no un contador.</param>
        ''' <param name="rotaciones">Instrumento del GATE, no del motor: cuantas rotaciones hizo. El
        ''' `.exe` no lo publica; se expone solo para poder congelarlo como golden.</param>
        Friend Sub EigenSimetrico(m As Mat3, ByRef v As Mat3, ByRef lambda As Vector128(Of Single),
                                  ByRef noConvergio As Integer, ByRef rotaciones As Integer,
                                  Optional tol As Single = FltEpsilon,
                                  Optional maxIter As Integer = MaxIteracionesEigen)
            'A0 = V0 * M * V0t   - el arranque en caliente (0x141360CB6 ... 0x141360D09)
            Dim a = Por(v, Por(m, Transponer(v)))
            rotaciones = 0
            noConvergio = 0

            Dim frob2 = FrobeniusAlCuadrado(m)
            Dim umbral = frob2 * tol * tol          ' 0x141360DBE (mulps xmm11, tol^2)

            Dim iter As Integer = 0
            Do
                'off2 = 2*(a10^2 + a20^2 + a21^2) - triangulo INFERIOR; el 2 en 0x142F3C570
                Dim a10 = a.E(1, 0), a20 = a.E(2, 0), a21 = a.E(2, 1)
                Dim off2 = 2.0F * (a20 * a20 + a10 * a10 + a21 * a21)
                If umbral > off2 Then Exit Do       ' 0x141360DCC/DD0 y 0x141361012/1016

                If iter >= maxIter Then             ' 0x141360E25 `cmp ebx, edi` / `jge`
                    noConvergio = 1                 ' 0x1413611A5
                    Exit Do
                End If

                'pivote = el mayor fuera de la diagonal (0x141360E45-0x141360E90)
                Dim m10 = Math.Abs(a10), m20 = Math.Abs(a20), m21 = Math.Abs(a21)
                Dim mx = Math.Max(m20, m10)         ' maxss xmm5, xmm2
                Dim p As Integer = 0
                Dim q As Integer = If(m20 > m10, 2, 1)
                If m21 > mx Then p = 1 : q = 2

                Dim aqp = a.E(q, p)                 ' 0x141360E94: [rsp + (p + q*4)*4 + 0x50]
                Dim c As Single, s As Single
                If aqp = 0.0F Then
                    c = 1.0F : s = 0.0F                                   ' 0x141360F14
                Else
                    ' ⛔ EL ORDEN ES DEL MOTOR: `divss xmm0(0,5), aqp` (0x141360EB8) y DESPUÉS
                    ' `mulss (aqq − app), xmm0` (0x141360EBF). O sea (aqq − app) · (0,5/aqp),
                    ' NO (0,5·(aqq − app))/aqp — se van 1 ulp y con él una rotación de más.
                    Dim theta = (a.E(q, q) - a.E(p, p)) * (0.5F / aqp)   ' 0x141360EB8 + 0x141360EBF
                    Dim r = Simd.SqrtExacta(theta * theta + 1.0F)         ' sqrtss exacto, 0x141360ED5
                    Dim t As Single
                    If theta >= 0.0F Then
                        t = 1.0F / (r + theta)                            ' 0x141360EDB/EDF
                    Else
                        t = 1.0F / (theta - r)                            ' 0x141360EE5/EE9
                    End If
                    c = 1.0F / Simd.SqrtExacta(t * t + 1.0F)              ' 0x141360F06/F0A
                    s = t * c                                             ' 0x141360F0E
                End If

                'J = I con J[p][p] = J[q][q] = c, J[q][p] = +s, J[p][q] = -s  (0x141360F55...F7E)
                Dim j = Mat3.Identidad
                j.SetE(p, p, c) : j.SetE(q, q, c)
                j.SetE(q, p, s) : j.SetE(p, q, -s)

                a = Por(Por(j, a), Transponer(j))   ' A <- J*A*Jt   (0x141360FA4 + 0x141360FB3)
                v = Por(j, v)                       ' V <- J*V      (0x141360FC6)
                rotaciones += 1
                iter += 1
            Loop

            'Cola: normalizar las tres filas de V - rsqrtps + UNA Newton, con guarda (0x141361096)
            v.F0 = Simd.NormalizarNewton(v.F0)
            v.F1 = Simd.NormalizarNewton(v.F1)
            v.F2 = Simd.NormalizarNewton(v.F2)

            'lambda = (a00, a11, a22, a22) - si, la 4a lane REPITE a22 (0x14136115E...0x141361184)
            lambda = Vector128.Create(a.E(0, 0), a.E(1, 1), a.E(2, 2), a.E(2, 2))
        End Sub

        ' -----------------------------------------------------------------------------------------
        ' La descomposición polar — la fase 2 de Volume: 0x141A0A6F0 (VIVO) / 0x141A672F0 (gemelo muerto)
        ' -----------------------------------------------------------------------------------------

        ''' <summary>
        ''' Devuelve el factor **ortogonal** `R` de la descomposicion polar izquierda `A = S * R`,
        ''' con `S = (A*At)^(1/2)` - es decir `R = S^-1 * A`. Transcrito de **`0x141A0A6F0`**,
        ''' que es el camino VIVO (`hclVolumeConstraintMx`, 38 sets en el corpus).
        ''' <para>⚠️ El `.exe` tiene un GEMELO, `0x141A672F0`, con el mismo cuerpo instrucción
        ''' por instrucción, al que llama `hclVolumeConstraint` (tipo 6) — del que el corpus
        ''' **no instancia ni uno**. La primera redacción citaba ése (motor-39). La ley no
        ''' cambia; las direcciones sí, y una cita a código muerto no se puede volver a
        ''' verificar contra lo que el juego corre.</para>
        ''' <para>`M = A*At` (`0x141A0A752`) . eigen (`0x141A0A780`, 20 iteraciones desde `0x141A0A774`, `FLT_EPSILON` desde `0x141A0A757`)
        ''' . `d = raiz(|lambda|) + FLT_EPSILON` (`0x141A0A79D`-`0x141A0A7CF`) . `S = Vt*diag(d)*V`
        ''' (`0x141A0A823` + `0x141A0A833`) . `S^-1` (`0x141A0A83C`) . `R = S^-1 * A`
        ''' (`0x141A0A84C`).</para>
        ''' <para>⭐⭐ `v` es **estado que vive entre cuadros**, no un temporal. El unico llamador es el
        ''' kernel de `hclVolumeConstraintMx` (`0x141A0A4D0`), y en `0x141A0A630` le pasa
        ''' `r14 + 0x50` - un campo de la instancia, no una pila. Adentro, `0x141A0A719` lo
        ''' guarda y `0x141A0A771` se lo pasa al eigensolver como 3.er argumento. El marco
        ''' de la vuelta anterior entra como semilla del eigensolver y sale actualizado. Con `I` como
        ''' semilla el resultado sigue siendo correcto, pero **no es el del motor**: cambia cuantas
        ''' rotaciones hace y, con autovalores repetidos, cual de los infinitos marcos elige.</para>
        ''' <para>⚠️ Si `S` es singular, <see cref="Inversa"/> devuelve **cero** y `R` sale cero: el
        ''' marco colapsa al centroide. Es la ley del motor, no un caso a "arreglar".</para>
        ''' </summary>
        ''' <param name="a">La matriz acumulada de la fase 1. En el camino vivo llega en `r8`
        ''' de `0x141A0A6F0` (el llamador la tiene en `rsp+0x30`, `0x141A0A637`).</param>
        ''' <param name="v">El marco de la vuelta anterior; sale con el nuevo. Llega en `r9`
        ''' (`0x141A0A630`: `lea r9, [r14 + 0x50]`) y de ahí a `rbx` (`0x141A0A719`) y a `r8` del
        ''' eigensolver (`0x141A0A771`).
        ''' <para>⛔ `r14` **no es la instancia**: es el bloque de estado POR SET, que
        ''' `0x141A0A528`-`0x141A0A554` busca por id en el arreglo de `[inst + 0xB0]` (entradas de
        ''' 16 B `{setIndex, _, ptr}`, y `+8` es el puntero). El `+0x50` es un desplazamiento
        ''' DENTRO de ese bloque, cuyo `+0x10` es el marco de salida y cuyo `+0x40` es el
        ''' centroide (`0x141A0A598`, `0x141A0A5AA`).</para></param>
        Friend Function FactorOrtogonal(a As Mat3, ByRef v As Mat3) As Mat3
            'M = A * At      (0x141A0A752: el motor traspone una copia y pasa At como 2.o y A como 3.o)
            Dim mm = Por(a, Transponer(a))

            Dim lambda As Vector128(Of Single)
            Dim noConv As Integer, rot As Integer
            EigenSimetrico(mm, v, lambda, noConv, rot)

            'd = raiz(|lambda|) + FLT_EPSILON, con el termino de la raiz en CERO donde |lambda| <= 0.
            'El motor calcula 1/raiz(|l|) con rsqrtps + UNA Newton y DESPUES lo multiplica por |l| -
            'eso da raiz(|l|), no su reciproca. Confundirlos invierte la raiz y R sale del otro lado.
            '(0x141A0A793 pslld/psrld = valor absoluto; 0x141A0A7A3 cmpleps contra CERO; 0x141A0A7C9
            'mulps por |l|; 0x141A0A7CC andnps; 0x141A0A7CF addps del epsilon, que se suma SIEMPRE.)
            Dim d0 = RaizConGuarda(lambda.GetElement(0))
            Dim d1 = RaizConGuarda(lambda.GetElement(1))
            Dim d2 = RaizConGuarda(lambda.GetElement(2))

            Dim diag = Mat3.Cero
            diag.SetE(0, 0, d0) : diag.SetE(1, 1, d1) : diag.SetE(2, 2, d2)

            'S = Vt * diag(d) * V      (0x141A0A823: T = Vt x D ; 0x141A0A833: T = T x V)
            Dim ss = Por(Por(Transponer(v), diag), v)

            'S^-1, con la guarda de singularidad del motor (0x141A0A83C -> 0x141360010)
            Dim sInv = Inversa(ss)

            'R = S^-1 * A              (0x141A0A84C)
            Return Por(sInv, a)
        End Function

        ''' <summary>
        ''' `raiz(|lambda|) + FLT_EPSILON`, con el termino de la raiz anulado donde `|lambda| &lt;= 0`.
        ''' <para>⛔ La guarda es sobre el **valor absoluto** (`0x141A0A793` `pslld`/`psrld` borra el
        ''' bit de signo ANTES del `cmpleps` de `0x141A0A7A3`), asi que sólo se dispara con
        ''' `lambda = 0` exacto: un autovalor **negativo** NO se anula, se le toma la raiz del modulo.
        ''' Poner la guarda en `lambda &lt;= 0` es un invento que apaga un eje entero del marco.</para>
        ''' <para>El `+ epsilon` de `0x141A0A7CF` es incondicional: el caso degenerado devuelve
        ''' `FLT_EPSILON`, no `0`.</para>
        ''' <para>⚠️ Es `Friend` y no `Private` a proposito: por el camino de `FactorOrtogonal`
        ''' esta ley NO SE PUEDE FALSEAR — `A*At` es semidefinida positiva, asi que ningun
        ''' `lambda` negativo llega nunca. El gate la mide DIRECTO (`G18h`); sin eso, cambiar la
        ''' guarda a `lambda &lt;= 0` pasaria en verde.</para>
        ''' </summary>
        <MethodImpl(MethodImplOptions.AggressiveInlining)>
        Friend Function RaizConGuarda(lam As Single) As Single
            Dim aL = Math.Abs(lam)                                       ' 0x141A0A793/BB
            If Not (aL > 0.0F) Then Return FltEpsilon                    ' 0x141A0A7A3 cmpleps + andnps
            Dim r = Simd.Lane0(Simd.RsqrtNewton(Vector128.Create(aL)))   ' 0x141A0A79D + Newton
            Return aL * r + FltEpsilon                                   ' 0x141A0A7C9 + 0x141A0A7CF
        End Function

    End Module

End Namespace

