Option Strict On
Option Explicit On

Imports System.Runtime.Intrinsics

' =================================================================================================
' EL `hkQsTransform` Y SUS DOS CONVERSIONES CON LA MATRIZ DEL TRANSFORM SET.
'
' Lo usa la capa de Bethesda que llena y vacía `hclClothInstance.transformSets` (`BSTransformSet`,
' `Havok/Physics/ConjuntoDeTransforms.vb`): interpola la ENTRADA entre dos cuadros y extrapola la
' SALIDA del último paso, y las dos cosas las hace en forma (traslación, cuaternión, escala), no en
' matriz.
'
'   · Qs → matriz  — `0x141539460`
'   · matriz → Qs  — `0x141483D20`, que es `0x1414840D0` (la descomposición de Havok) + copia
'
' ⛔ La descomposición NO es «sacar la rotación normalizando filas». Es la polar ITERATIVA
' `M ← ½(M + M⁻ᵀ)` (hasta 30 vueltas, `0x14148421E`), después normaliza, corrige el signo del
' determinante y recién ahí saca el cuaternión. Para una matriz ortonormal converge en una vuelta y
' da lo mismo que normalizar, pero la ley es la del motor y se transcribe entera.
' =================================================================================================


Namespace Havok.Motor

    ''' <summary>
    ''' `hkQsTransform`: 48 B — traslación `+0x00`, cuaternión `(x, y, z, w)` `+0x10`, escala `+0x20`.
    ''' </summary>
    Friend Structure Qs
        Friend T As Vector128(Of Single)
        Friend R As Vector128(Of Single)
        Friend S As Vector128(Of Single)

        ''' <summary>La entrada nueva de la lista de salida — `0x1418A4D46`/`4A`/`4E`: traslación en
        ''' cero, cuaternión `0x142F3C730` = `(0,0,0,1)`, escala `0x142F3C560` = `(1,1,1,1)`.</summary>
        Friend Shared ReadOnly Property Identidad As Qs
            Get
                Dim q As Qs
                q.T = Vector128(Of Single).Zero
                q.R = Vector128.Create(0.0F, 0.0F, 0.0F, 1.0F)
                q.S = Vector128.Create(1.0F)
                Return q
            End Get
        End Property
    End Structure

    Friend Module Descomposicion

        ''' <summary>`0x1414840D0`: la polar corta a las 30 vueltas (`cmp ebx, 0x1E`).</summary>
        Friend Const MaxVueltasPolar As Integer = 30

        ''' <summary>La máscara que borra la lane `w` — `pslldq 4` + `psrldq 4`.</summary>
        Private Function SinW(v As Vector128(Of Single)) As Vector128(Of Single)
            Return v.WithElement(Simd.LaneW, 0.0F)
        End Function

        ''' <summary>`(v.xyz, 1)` — `unpckhps v, (1,1,1,1)` + `shufps 0xC4`.</summary>
        Private Function ConW1(v As Vector128(Of Single)) As Vector128(Of Single)
            Return v.WithElement(Simd.LaneW, 1.0F)
        End Function

        ''' <summary>`(y + x) + z`, difundido — el `shufps 0x55` / `shufps 0` / `shufps 0xAA` que el
        ''' motor usa para el producto escalar de tres lanes.</summary>
        Private Function Hsum3(p As Vector128(Of Single)) As Vector128(Of Single)
            Dim yx = Vector128.Add(Simd.BcastY(p), Simd.BcastX(p))
            Return Vector128.Add(yx, Simd.BcastZ(p))
        End Function

        ''' <summary>`(a.yzx·b − a·b.yzx).yzx` — el orden de operandos del motor.</summary>
        Private Function CruzYzx(a As Vector128(Of Single), b As Vector128(Of Single)) As Vector128(Of Single)
            Dim ay = Vector128.Shuffle(a, Vector128.Create(1, 2, 0, 3))
            Dim by = Vector128.Shuffle(b, Vector128.Create(1, 2, 0, 3))
            Dim u = Vector128.Subtract(Vector128.Multiply(ay, b), Vector128.Multiply(a, by))
            Return Vector128.Shuffle(u, Vector128.Create(1, 2, 0, 3))
        End Function

        ' -----------------------------------------------------------------------------------------
        ' Qs → matriz — 0x141539460
        ' -----------------------------------------------------------------------------------------

        ''' <summary>
        ''' `0x141539460`: `R = matrizDe(q)` (`0x141365A80`), cada fila por su lane de escala
        ''' (`shufps 0` / `0x55` / `0xAA` + `mulps`), `w` de las tres filas en cero
        ''' (`pslldq`/`psrldq`), y la fila 3 = `(t.xyz, 1)`.
        ''' </summary>
        Friend Function AMatriz(q As Qs) As Mat4
            Dim rot = Cuaternion.AMatriz(q.R)                                   ' 0x141539479
            Dim m As Mat4
            m.F0 = SinW(Vector128.Multiply(Simd.BcastX(q.S), rot.F0))          ' 0x141539488/8C/A3/AD
            m.F1 = SinW(Vector128.Multiply(Simd.BcastY(q.S), rot.F1))          ' 0x141539491/95/A8/B2
            m.F2 = SinW(Vector128.Multiply(Simd.BcastZ(q.S), rot.F2))          ' 0x14153949A/9E/BF/C4
            m.F3 = ConW1(q.T)                                                  ' 0x1415394D5…E3
            Return m
        End Function

        ' -----------------------------------------------------------------------------------------
        ' matriz → Qs — 0x141483D20 / 0x1414840D0
        ' -----------------------------------------------------------------------------------------

        ''' <summary>
        ''' La inversa 3×3 con tolerancia — `0x14135FE20(m, flag, eps)`, EN EL LUGAR.
        ''' <para>Cofactores `c0 = F1×F2`, `c1 = F2×F0`, `c2 = F0×F1` con el orden de operandos del
        ''' motor; `det = hsum3(F0·c0)`; si `|det| &gt; eps³` (`0x14135FEC6 ucomiss` / `jbe`) escribe
        ''' `c·(1/det)` con `rcpps` + Newton (`0x14135FED2`…`E3`) y TRASPONE (`0x14135FEFE`);
        ''' si no, deja la matriz como estaba y avisa (`0x14135FF0B`).</para>
        ''' </summary>
        Friend Function InversaConTolerancia(ByRef m As Mat3, eps As Single) As Boolean
            Dim c0 = CruzYzx(m.F2, m.F1)                         ' 0x14135FE34…7D  (= F1×F2)
            Dim c2 = CruzYzx(m.F1, m.F0)                         ' 0x14135FE63/82/DC (= F0×F1)
            Dim c1 = CruzYzx(m.F0, m.F2)                         ' 0x14135FE6A…89/D5 (= F2×F0)
            Dim det = Hsum3(Vector128.Multiply(m.F0, c0))        ' 0x14135FE85…B2
            Dim e3 = eps * eps * eps                             ' 0x14135FEAC…B5
            Dim absDet = Math.Abs(det.GetElement(0))             ' 0x14135FEB8 pslld/psrld
            If Not (absDet > e3) Then Return False               ' 0x14135FEC6 ucomiss / jbe
            Dim inv = Simd.RcpNewton(det)                        ' 0x14135FED2…E3
            Dim r As Mat3
            r.F0 = Vector128.Multiply(inv, c0)                   ' 0x14135FEEC
            r.F1 = Vector128.Multiply(c1, inv)                   ' 0x14135FEE9
            r.F2 = Vector128.Multiply(c2, inv)                   ' 0x14135FEF0
            m = Polar.Transponer(r)                              ' 0x14135FEFE
            Return True
        End Function

        ''' <summary>`0x14135FC80`: `|a − b| ≤ eps` en las lanes `xyz` de las tres filas
        ''' (`cmpleps` + `movmskps AND 7 == 7`).</summary>
        Friend Function Aproximadamente(a As Mat3, b As Mat3, eps As Single) As Boolean
            Return FilaCerca(a.F0, b.F0, eps) AndAlso FilaCerca(a.F1, b.F1, eps) AndAlso FilaCerca(a.F2, b.F2, eps)
        End Function

        Private Function FilaCerca(a As Vector128(Of Single), b As Vector128(Of Single), eps As Single) As Boolean
            Dim d = Vector128.Abs(Vector128.Subtract(a, b))
            Return d.GetElement(0) <= eps AndAlso d.GetElement(1) <= eps AndAlso d.GetElement(2) <= eps
        End Function

        ''' <summary>
        ''' ⭐ `hkMatrixDecomposition::decomposeMatrix` — `0x1414840D0`, lo que `0x141483D20` copia a
        ''' un `hkQsTransform`.
        ''' <para>```
        ''' T  = (F3.xyz, 1)                                        ' 0x141484132…187
        ''' M  = filas 0..2 con w = 0                               ' 0x141484136…168
        ''' repetir:                                                ' 0x1414841A0
        '''     viejo = M ; X = M ; X = inversa(X, eps) ; X = Xᵀ    ' 0x1414841C6 / 0x1414841CF
        '''     M = 0,5 · (viejo + X)                               ' 0x1414841F7 / 0x141484217
        '''     vueltas++ ; si vueltas ≥ 30 o M ≈ viejo (eps): salir ' 0x14148421E / 0x14148422F
        ''' Fk = normalizar(M.Fk) con rsqrt+Newton y guarda ≤0      ' 0x141484276…313
        ''' si hsum3((F1×F2)·F0) &lt; 0: F0 = −F0                     ' 0x14148435C…3C3
        ''' q  = normalizar(quatDe(F0,F1,F2))                       ' 0x1414843D1…41F
        ''' E  = filas(original) ∘ inversaRigida(matrizDe(q), T)    ' 0x141484423…573
        ''' S  = (E0.x, E1.y, E2.z, 1)                              ' 0x14148457E…5CE
        ''' ```</para>
        ''' </summary>
        Friend Function AQs(m As Mat4) As Qs
            Dim eps = Simd.FltEpsilon                                           ' 0x1414840F1 (0x142F3C760)
            Dim o0 = SinW(m.F0), o1 = SinW(m.F1), o2 = SinW(m.F2)               ' 0x141484158…168
            Dim o3 = ConW1(m.F3)                                                ' 0x141484132…160

            Dim cur As Mat3
            cur.F0 = o0 : cur.F1 = o1 : cur.F2 = o2
            Dim vueltas = 0
            Do
                Dim viejo = cur                                                 ' 0x1414841B6…BA
                Dim x = cur
                InversaConTolerancia(x, eps)                                    ' 0x1414841C6
                x = Polar.Transponer(x)                                         ' 0x1414841CF
                Dim suma As Mat3
                suma.F0 = Vector128.Add(x.F0, viejo.F0)                         ' 0x1414841F7 (0x1413606D0)
                suma.F1 = Vector128.Add(x.F1, viejo.F1)
                suma.F2 = Vector128.Add(x.F2, viejo.F2)
                Dim medio = Vector128.Create(0.5F)                              ' 0x1414841FC (0x142F3C650)
                cur.F0 = Vector128.Multiply(medio, suma.F0)                     ' 0x141484217 (0x1413604D0)
                cur.F1 = Vector128.Multiply(suma.F1, medio)
                cur.F2 = Vector128.Multiply(suma.F2, medio)
                vueltas += 1                                                    ' 0x14148421C
                If vueltas >= MaxVueltasPolar Then Exit Do                      ' 0x14148421E cmp / jge
                If Aproximadamente(cur, viejo, eps) Then Exit Do                ' 0x14148422F
            Loop

            Dim f0 = Vector128.Multiply(cur.F0, Simd.RsqrtNewtonConGuarda(Hsum3(Vector128.Multiply(cur.F0, cur.F0))))  ' 0x141484272…2D4
            Dim f1 = Vector128.Multiply(cur.F1, Simd.RsqrtNewtonConGuarda(Hsum3(Vector128.Multiply(cur.F1, cur.F1))))  ' 0x1414842CE…313
            Dim f2 = Vector128.Multiply(cur.F2, Simd.RsqrtNewtonConGuarda(Hsum3(Vector128.Multiply(cur.F2, cur.F2))))  ' 0x14148431D…362
            Dim det = Hsum3(Vector128.Multiply(CruzYzx(f2, f1), f0))           ' 0x141484365…3A4
            If det.GetElement(0) < 0.0F Then                                    ' 0x1414843A7 cmpltps
                ' ⛔ `xorps` con el bit de signo, no `0 − f0`: con una lane en +0 da −0.
                f0 = Vector128.Xor(f0, Vector128.Create(-0.0F))                 ' 0x1414843AF…BF xorps signo
            End If

            Dim rot As Mat3
            rot.F0 = f0 : rot.F1 = f1 : rot.F2 = f2
            Dim q = Cuaternion.DeMatriz(rot)                                    ' 0x1414843D1
            q = Vector128.Multiply(Simd.RsqrtNewton(Simd.Hsum4(Vector128.Multiply(q, q))), q)   ' 0x1414843D6…41F

            ' La matriz de estiramiento: las filas originales por la inversa rígida de (R, T).
            Dim rq = Cuaternion.AMatriz(q)                                      ' 0x141484423
            Dim rt As Mat4
            rt.F0 = rq.F0 : rt.F1 = rq.F1 : rt.F2 = rq.F2 : rt.F3 = o3          ' 0x141484428…434
            Dim inv = Mat4.InversaRigida(rt)                                    ' 0x141484438 (0x141298100)
            Dim i0 = SinW(inv.F0), i1 = SinW(inv.F1), i2 = SinW(inv.F2)         ' 0x14148447C…49B
            Dim i3 = ConW1(inv.F3)                                              ' 0x141484452…46A
            Dim e0 = FilaPorInversa(o0, i0, i1, i2, i3)                         ' 0x14148443D…4BE
            Dim e1 = FilaPorInversa(o1, i0, i1, i2, i3)                         ' 0x1414844C2…52C
            Dim e2 = FilaPorInversa(o2, i0, i1, i2, i3)                         ' 0x1414844CF…555
            Dim e3v = FilaPorInversa(o3, i0, i1, i2, i3)                        ' 0x14148451D…573

            ' S = E0·(1,0,0,0) + E1·(0,1,0,0) + E2·(0,0,1,0) + E3·(0,0,0,1), con w = 1   ' 0x14148457E…5CE
            Dim s = Vector128.Multiply(e0, Vector128.Create(1.0F, 0.0F, 0.0F, 0.0F))
            s = Vector128.Add(Vector128.Multiply(e1, Vector128.Create(0.0F, 1.0F, 0.0F, 0.0F)), s)
            s = Vector128.Add(Vector128.Multiply(e2, Vector128.Create(0.0F, 0.0F, 1.0F, 0.0F)), s)
            s = Vector128.Add(Vector128.Multiply(e3v, Vector128.Create(0.0F, 0.0F, 0.0F, 1.0F)), s)
            s = ConW1(s)

            Dim r As Qs
            r.T = o3
            r.R = q
            r.S = s
            Return r
        End Function

        ' -----------------------------------------------------------------------------------------
        ' El rebase de la tela al pasar de Animate a Simulate — 0x1418C97A0 / 0x1418C9CB0 / 0x14133A140
        ' -----------------------------------------------------------------------------------------

        ''' <summary>`2·((dot3(v,q)·q + (w²−½)·v) + cruzYzx(a,b)·w)` con los operandos de cada sitio.</summary>
        Private Function Rotar(v As Vector128(Of Single), q As Vector128(Of Single),
                               cruz As Vector128(Of Single)) As Vector128(Of Single)
            Dim w = Simd.BcastW(q)
            Dim r = Vector128.Multiply(Hsum3(Vector128.Multiply(q, v)), q)
            r = Vector128.Add(r, Vector128.Multiply(Vector128.Subtract(Vector128.Multiply(w, w), Vector128.Create(0.5F)), v))
            r = Vector128.Add(r, Vector128.Multiply(cruz, w))
            Return Vector128.Add(r, r)
        End Function

        ''' <summary>
        ''' `0x14133A140(out, qs, p)` — un punto por un `hkQsTransform`: `S·p` (`0x14133A14F`), rotado
        ''' por `q` (`2·((dot3·q + (w²−½)·Sp) + cruzYzx(Sp,q)·w)`) y `+ T` (`0x14133A1B7`). Escribe
        ''' las cuatro lanes.
        ''' </summary>
        Friend Function TransformarPuntoQs(t As Qs, p As Vector128(Of Single)) As Vector128(Of Single)
            Dim sp = Vector128.Multiply(t.S, p)                                  ' 0x14133A14F
            Dim r = Rotar(sp, t.R, CruzYzx(sp, t.R))                             ' 0x14133A14C…1B4
            Return Vector128.Add(r, t.T)                                         ' 0x14133A1B7
        End Function

        ''' <summary>
        ''' ⭐ El delta del rebase — `0x1418C9CB0(a, b, f1, f2)`.
        ''' <para>```
        ''' invA  = (−rotarPorConjugado(ta, qa), conj(qa), 1/Sa)          ' 0x1418C9D02…D87
        ''' rel   = invA ∘ b   (t con cruzYzx(tb,qac), q = qac ⊗ qb)       ' 0x1418C9D55…E46
        ''' M'.t  = f1 · rel.t                                           ' 0x1418C9E73…7E
        ''' M'.q  = |rel.q.xyz|² &gt; 1,42109e-14 (0x142F3C770) ?
        '''         ejeAngulo(±rel.q.xyz/|·|, f2 · angulo(rel.q)) : (0,0,0,1)   ' 0x1418C9E94…F0D
        ''' out   = a ∘ M' ∘ invA                                        ' 0x1418C9F20…CA136
        ''' ```</para>
        ''' <para>El job lo llama con `f1 = f2 = 1` (`0x1418790C4` + `0x1418795A1`/`A4`).</para>
        ''' </summary>
        Friend Function DeltaDeRebase(a As Qs, b As Qs, f1 As Single, f2 As Single) As Qs
            ' `0x14133A0C0(qa, ta)` y el `xorps` con el signo en las cuatro lanes
            Dim menosT = Vector128.Xor(RotarPorConjugadoDelMotor(a.T, a.R), Vector128.Create(-0.0F))   ' 0x1418C9D02 + 0x1418C9D30
            Dim qac = Vector128.Xor(a.R, Vector128.Create(-0.0F, -0.0F, -0.0F, 0.0F))     ' 0x1418C9D33/39
            Dim invS = SinW(Simd.RcpNewton(a.S))                                           ' 0x1418C9D46…87
            Dim w = Simd.BcastW(qac)                                                       ' 0x1418C9D59

            ' rel.t — 0x1418C9D55…DE7
            Dim relT = Rotar(b.T, qac, CruzYzx(b.T, qac))
            relT = Vector128.Add(relT, menosT)
            ' rel.q — 0x1418C9DCA…E46
            Dim vec = CruzYzx(b.R, qac)
            vec = Vector128.Add(vec, Vector128.Multiply(w, b.R))
            vec = Vector128.Add(vec, Vector128.Multiply(qac, Simd.BcastW(b.R)))
            Dim wq = Simd.BcastW(b.R).GetElement(0) * w.GetElement(0) - Hsum3(Vector128.Multiply(qac, b.R)).GetElement(0)
            Dim relQ = vec.WithElement(Simd.LaneW, wq)

            ' M'
            Dim tM = Vector128.Multiply(Vector128.Create(f1), relT)                        ' 0x1418C9E73…7E
            Dim qM = Vector128.Create(0.0F, 0.0F, 0.0F, 1.0F)                              ' 0x1418C9E60 (0x142F3C730)
            Dim n = Hsum3(Vector128.Multiply(relQ, relQ))                                  ' 0x1418C9E4E…91
            If n.GetElement(0) > 1.42108547E-14F Then                                      ' 0x1418C9E94 ucomiss / jbe
                Dim rg = Simd.RsqrtNewtonConGuarda(n)                                      ' 0x1418C9E9D…E1
                Dim signo = Vector128.BitwiseAnd(Vector128.LessThan(Simd.BcastW(relQ), Vector128(Of Single).Zero),
                                                 Vector128.Create(-0.0F))                  ' 0x1418C9EB5…D9
                Dim eje = Vector128.Xor(Vector128.Multiply(rg, relQ), signo)               ' 0x1418C9EE4/E7
                Dim ang = f2 * Cuaternion.Angulo(relQ)                                     ' 0x1418C9EEE + 0x1418C9EFB
                qM = Cuaternion.DeEjeAngulo(eje, ang)                                      ' 0x1418C9F04 (0x14135E700)
            End If

            ' out = a ∘ M' ∘ invA — 0x1418C9F20…0x1418CA136
            Dim qa = a.R, wA = Simd.BcastW(a.R)
            Dim s2 = Vector128.Multiply(Vector128.Create(1.0F), a.S)                       ' 0x1418C9F2C/34
            Dim t2 = Vector128.Add(Rotar(tM, qa, CruzYzx(tM, qa)), a.T)                    ' 0x1418C9F29…FD2
            Dim v2 = CruzYzx(qM, qa)
            v2 = Vector128.Add(v2, Vector128.Multiply(qM, wA))
            v2 = Vector128.Add(v2, Vector128.Multiply(Simd.BcastW(qM), qa))
            Dim w2 = Simd.BcastW(qM).GetElement(0) * wA.GetElement(0) - Hsum3(Vector128.Multiply(qM, qa)).GetElement(0)
            Dim q2 = v2.WithElement(Simd.LaneW, w2)                                        ' 0x1418C9F8D…CA00D

            Dim r As Qs
            r.S = Vector128.Multiply(s2, invS)                                             ' 0x1418CA052 → +0x20
            r.T = Vector128.Add(Rotar(menosT, q2, CruzYzx(menosT, q2)), t2)                ' 0x1418CA00A…FC → +0x00
            Dim v3 = CruzYzx(qac, q2)
            v3 = Vector128.Add(v3, Vector128.Multiply(qac, Simd.BcastW(q2)))
            v3 = Vector128.Add(v3, Vector128.Multiply(q2, Simd.BcastW(qac)))
            Dim w3 = Simd.BcastW(qac).GetElement(0) * Simd.BcastW(q2).GetElement(0) - Hsum3(Vector128.Multiply(q2, qac)).GetElement(0)
            r.R = v3.WithElement(Simd.LaneW, w3)                                           ' 0x1418CA0B1…136 → +0x10
            Return r
        End Function

        ' -----------------------------------------------------------------------------------------
        ' Composición de hkQsTransform — 0x1414920E0 (y los mismos kernels en línea del BSTransformSet)
        ' -----------------------------------------------------------------------------------------

        ''' <summary>
        ''' ⭐ `p ∘ h` — el cuerpo de `0x1414920E0` (`model[i] = model[padre] ∘ local[i]`):
        ''' <para>```
        ''' T = 2·((dot3(h.T,p.R)·p.R + (w²−½)·h.T) + cruzYzx(h.T,p.R)·w) + p.T   ' 0x14149212D…1AE
        ''' R = (cruzYzx(h.R,p.R) + h.R·p.w) + p.R·h.w ;  w = h.w·p.w − dot3(h.R,p.R)   ' 0x1414921B2…211
        ''' S = h.S · p.S                                                     ' 0x14149220B…218
        ''' ```</para>
        ''' <para>El mismo kernel, con los mismos operandos, está en línea en el camino sin nodo de
        ''' `BeginAccess` (`0x1418A64D5`-`0x1418A65B8`), en el `+0x100` del constructor
        ''' (`0x1418A4E40`-`0x1418A4F67`), en el rebase del teletransporte (`0x1418A5986`-`0x1418A5A81`)
        ''' y en el delta de `0x1418CA1E0`.</para>
        ''' </summary>
        Friend Function ComponerQs(p As Qs, h As Qs) As Qs
            Dim r As Qs
            r.T = Vector128.Add(Rotar(h.T, p.R, CruzYzx(h.T, p.R)), p.T)
            Dim pw = Simd.BcastW(p.R), hw = Simd.BcastW(h.R)
            Dim v = CruzYzx(h.R, p.R)
            v = Vector128.Add(v, Vector128.Multiply(h.R, pw))
            v = Vector128.Add(v, Vector128.Multiply(p.R, hw))
            Dim w = hw.GetElement(0) * pw.GetElement(0) - Hsum3(Vector128.Multiply(h.R, p.R)).GetElement(0)
            r.R = v.WithElement(Simd.LaneW, w)
            r.S = Vector128.Multiply(h.S, p.S)
            Return r
        End Function

        ''' <summary>
        ''' La inversa de un Qs como la arma el motor: `T = −rotarPorConjugado(T, R)` (`0x14133A0C0`
        ''' + `xorps` de signo en las cuatro lanes), `R = conj(R)` (`xorps (−0,−0,−0,0)`) y
        ''' `S = rcpps + Newton` con la `w` borrada (`pslldq`/`psrldq 4`).
        ''' <para>Sitios: `0x1418A4E40`-`0x1418A4EAB` (el `+0x100` del constructor) y
        ''' `0x1418CA218`-`0x1418CA28E` (el delta del teletransporte).</para>
        ''' </summary>
        Friend Function InversaQs(a As Qs) As Qs
            Dim r As Qs
            r.T = Vector128.Xor(RotarPorConjugadoDelMotor(a.T, a.R), Vector128.Create(-0.0F))
            r.R = Vector128.Xor(a.R, Vector128.Create(-0.0F, -0.0F, -0.0F, 0.0F))
            r.S = SinW(Simd.RcpNewton(a.S))
            Return r
        End Function

        ''' <summary>`0x1418CA1E0(a, b, out)`: `out = b ∘ a⁻¹` — lo que movió el hueso raíz entre la
        ''' pose guardada y la nueva.</summary>
        Friend Function DeltaDeTeletransporte(a As Qs, b As Qs) As Qs
            Return ComponerQs(b, InversaQs(a))
        End Function

        ''' <summary>
        ''' `0x1414920E0(n, parentIndices, identidad, local, model)` — la pose de MODELO desde la
        ''' local, en ORDEN DE ÍNDICE (el motor no ordena por profundidad). El padre −1 compone contra
        ''' `0x142F3DFB0` = `T (0,0,0,0) · R (0,0,0,1) · S (1,1,1,0)`.
        ''' </summary>
        Friend Function PoseDeModelo(locales As Qs(), padres As IList(Of Integer)) As Qs()
            Dim n = If(locales Is Nothing, 0, locales.Length)
            Dim r(Math.Max(0, n) - 1) As Qs
            Dim identidad As Qs
            identidad.T = Vector128(Of Single).Zero
            identidad.R = Vector128.Create(0.0F, 0.0F, 0.0F, 1.0F)
            identidad.S = Vector128.Create(1.0F, 1.0F, 1.0F, 0.0F)             ' 0x142F3DFB0 + 0x20
            For i = 0 To n - 1
                Dim p = If(padres IsNot Nothing AndAlso i < padres.Count, padres(i), -1)
                Dim padre = If(p = -1 OrElse p < 0 OrElse p >= n, identidad, r(p))   ' 0x141492118
                r(i) = ComponerQs(padre, locales(i))
            Next
            Return r
        End Function

        ''' <summary>
        ''' `0x141298260(out, qs, m)`: la matriz `m` llevada por el Qs.
        ''' <para>```
        ''' SR      = por(diag(S), matrizDe(q))          ' 0x141298288 (0x141365A80) + 0x1412982CD (0x141360290)
        ''' out.Fk  = (m.Fk.y·SR.F1 + m.Fk.x·SR.F0) + m.Fk.z·SR.F2   ' k = 0..3
        ''' out.F3 += qs.T                               ' 0x141298394
        ''' ```</para>
        ''' </summary>
        Friend Function QsPorMatriz(q As Qs, m As Mat4) As Mat4
            Dim sr = SrDelQs(q)
            Dim r As Mat4
            r.F0 = Mat4.FilaCompuesta(m.F0, sr)
            r.F1 = Mat4.FilaCompuesta(m.F1, sr)
            r.F2 = Mat4.FilaCompuesta(m.F2, sr)
            r.F3 = Vector128.Add(Mat4.FilaCompuesta(m.F3, sr), q.T)
            Return r
        End Function

        ''' <summary>`0x141483C00` + `0x1412985B0` + `0x1412986A0`: el Qs como matriz — las filas de
        ''' `por(diag(S), matrizDe(q))` con `w = 0` y la fila 3 = `(T.xyz, 1)`.</summary>
        Friend Function QsAMatrizPorProducto(q As Qs) As Mat4
            Dim sr = SrDelQs(q)
            Dim r As Mat4
            r.F0 = SinW(sr.F0)
            r.F1 = SinW(sr.F1)
            r.F2 = SinW(sr.F2)
            r.F3 = ConW1(q.T)
            Return r
        End Function

        ''' <summary>`0x141365A80` + `0x141360290` con las filas `S·(1,0,0,0)`, `S·(0,1,0,0)`,
        ''' `S·(0,0,1,0)` (`0x142F3C700`/`710`/`720`).</summary>
        Private Function SrDelQs(q As Qs) As Mat4
            Dim rot = Cuaternion.AMatriz(q.R)
            Dim diag As Mat3
            diag.F0 = Vector128.Multiply(q.S, Vector128.Create(1.0F, 0.0F, 0.0F, 0.0F))
            diag.F1 = Vector128.Multiply(q.S, Vector128.Create(0.0F, 1.0F, 0.0F, 0.0F))
            diag.F2 = Vector128.Multiply(q.S, Vector128.Create(0.0F, 0.0F, 1.0F, 0.0F))
            Dim p = Polar.Por(diag, rot)
            Dim r As Mat4
            r.F0 = p.F0 : r.F1 = p.F1 : r.F2 = p.F2
            Return r
        End Function

        ''' <summary>`0x1415397F0`: la traspuesta 4×4 EN EL LUGAR (`shufps 0x44/0xEE/0x88/0xDD`).</summary>
        Friend Function Transponer4(m As Mat4) As Mat4
            Dim r As Mat4
            r.F0 = Vector128.Create(m.F0.GetElement(0), m.F1.GetElement(0), m.F2.GetElement(0), m.F3.GetElement(0))   ' 0x141539818
            r.F1 = Vector128.Create(m.F0.GetElement(1), m.F1.GetElement(1), m.F2.GetElement(1), m.F3.GetElement(1))   ' 0x14153982E
            r.F2 = Vector128.Create(m.F0.GetElement(2), m.F1.GetElement(2), m.F2.GetElement(2), m.F3.GetElement(2))   ' 0x14153982A
            r.F3 = Vector128.Create(m.F0.GetElement(3), m.F1.GetElement(3), m.F2.GetElement(3), m.F3.GetElement(3))   ' 0x141539832
            Return r
        End Function

        ''' <summary>`0x14133A040(out, q, v)`: `2·((dot3(v,q)·q + (w²−½)·v) + cruzYzx(v,q)·w)`.</summary>
        Friend Function RotarVector(q As Vector128(Of Single), v As Vector128(Of Single)) As Vector128(Of Single)
            Return Rotar(v, q, CruzYzx(v, q))
        End Function

        ''' <summary>`0x14133A0C0(q, v)`: `2·((dot3(v,q)·q + (w²−½)·v) + cruzYzx(q,v)·w)` — rotar por
        ''' el CONJUGADO de `q`.</summary>
        Friend Function RotarPorConjugadoDelMotor(v As Vector128(Of Single), q As Vector128(Of Single)) As Vector128(Of Single)
            Return Rotar(v, q, CruzYzx(q, v))
        End Function

        ''' <summary>`(f.y·I1 + f.x·I0) + f.z·I2 + f.w·I3` — el orden de las sumas de
        ''' `0x14148448B`…`0x1414844BA`.</summary>
        Private Function FilaPorInversa(f As Vector128(Of Single), i0 As Vector128(Of Single),
                                        i1 As Vector128(Of Single), i2 As Vector128(Of Single),
                                        i3 As Vector128(Of Single)) As Vector128(Of Single)
            Dim a = Vector128.Multiply(Simd.BcastY(f), i1)
            a = Vector128.Add(a, Vector128.Multiply(Simd.BcastX(f), i0))
            a = Vector128.Add(a, Vector128.Multiply(Simd.BcastZ(f), i2))
            Return Vector128.Add(a, Vector128.Multiply(Simd.BcastW(f), i3))
        End Function

    End Module

End Namespace
