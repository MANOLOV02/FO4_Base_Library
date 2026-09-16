Option Strict On
Option Explicit On

Imports System.Runtime.Intrinsics

' =================================================================================================
' LOS OPERADORES DE TRANSFERENCIA — los que mueven vértices entre buffers y espacios.
'
' Ley: RE_MOTOR_FISICA_CANONICO_2026-09-05.md, caps. 4.2, 6bis, 6q.1 y 6q.2.
'
' ⛔⛔ LA MATRIZ ES SIEMPRE LA MISMA COMPOSICIÓN, y no es la identidad por decreto:
'       M = bufEntrada.AEspacioDeSimulacion × bufSalida.DesdeEspacioDeSimulacion
' Cada buffer tiene su propio espacio (cap. 4.2). Que en reposo las dos sean la identidad lo
' **mide** G20 sobre el corpus; no se supone.
'
' ⛔ La NORMAL se transforma con **la misma `M`**, sin traslación, y **NO se renormaliza**
' (`0x1418FAB10`). Usar la inversa traspuesta o renormalizar es «arreglar» el motor.
' =================================================================================================


Namespace Havok.Motor

    Friend Module Operadores

        ''' <summary>
        ''' `M = M_entrada × M_salida⁻¹` — la composición que usan `CopyVertices` (`0x1418FA860`),
        ''' `GatherAll` (`0x1418F96F0`) y `GatherSome` (`0x1418F9E80`).
        ''' </summary>
        Friend Function MatrizEntreBuffers(entrada As Buffer, salida As Buffer) As Mat4
            Return Componer(entrada.AEspacioDeSimulacion, salida.DesdeEspacioDeSimulacion)
        End Function

        ''' <summary>`out.filaₖ = a.filaₖ · b`, convención de fila-vector, con la fila 3 llevando la
        ''' traslación.</summary>
        Friend Function Componer(a As Mat4, b As Mat4) As Mat4
            Dim r As Mat4
            r.F0 = TransformarDireccion(a.F0, b)
            r.F1 = TransformarDireccion(a.F1, b)
            r.F2 = TransformarDireccion(a.F2, b)
            r.F3 = TransformarPunto(a.F3, b)
            Return r
        End Function

        ''' <summary>
        ''' `((v.x·M0 + M3) + v.y·M1) + v.z·M2` — **con** traslación, y **en ese orden**.
        ''' <para>⛔ La traslación se suma **enseguida del término en `x`**, no al final: `CopyVertices`
        ''' (`0x1418FAC7F`, antes del `+ y·M1` de `0x1418FAC86`), `MoveParticles` (`0x141952714`,
        ''' antes del `0x141952726`) y `GatherAll` (`0x1418F985C`) lo hacen los tres así. Sumarla al
        ''' final se va **1 ulp** por vértice copiado y por partícula movida por un ancla — y
        ''' `MoveParticles` escribe `positions` directo, así que ese ulp entra al estado y se
        ''' compone (motor-55).</para>
        ''' <para>⚠️ **No es el mismo orden en todos lados**: la aplicación de `Volume`
        ''' (`0x141339F90`) suma `(y·M1 + x·M0) + z·M2` y la traslación **al final**. Cuando se
        ''' transcriba, va con su propia forma, no con ésta.</para>
        ''' </summary>
        Friend Function TransformarPunto(v As Vector128(Of Single), m As Mat4) As Vector128(Of Single)
            Dim r = Vector128.Multiply(Simd.BcastX(v), m.F0)
            r = Vector128.Add(r, m.F3)                                      ' 0x1418FAC7F / 0x141952714
            r = Vector128.Add(r, Vector128.Multiply(Simd.BcastY(v), m.F1))  ' 0x1418FAC86 / 0x141952726
            Return Vector128.Add(r, Vector128.Multiply(Simd.BcastZ(v), m.F2))
        End Function

        ''' <summary>`v.x·M0 + v.y·M1 + v.z·M2` — **sin** traslación. Es lo que se le aplica a las
        ''' normales, con la MISMA matriz que a las posiciones.</summary>
        Friend Function TransformarDireccion(v As Vector128(Of Single), m As Mat4) As Vector128(Of Single)
            Dim r = Vector128.Multiply(Simd.BcastX(v), m.F0)
            r = Vector128.Add(r, Vector128.Multiply(Simd.BcastY(v), m.F1))
            Return Vector128.Add(r, Vector128.Multiply(Simd.BcastZ(v), m.F2))
        End Function

        ' -----------------------------------------------------------------------------------------

        ''' <summary>
        ''' `hclCopyVerticesOperator` (tipo 4) — despacho de **cuatro** kernels en `0x1418FA860`.
        ''' <para>Los cuatro son **la misma ley**: lo que cambia es si el elemento se mueve entero de
        ''' 16 B (bit 0 de `+0x20` y `+0x48`) o con tres `movss` (float3 apretado). ⛔ Que el elemento
        ''' vaya entero NO es sólo acceso: la lane `w` de la salida recibe la `w` transformada
        ''' (`0x1418FAACD`), así que la elección sale de <see cref="KernelSimple"/>.</para>
        ''' <para>⚠️ Los cuatro strides son **`uint8`** en el motor (`movzx …, byte ptr [buf+0x1C]`).</para>
        ''' </summary>
        Friend Sub CopiarVertices(entrada As Buffer, salida As Buffer,
                                  inicioEntrada As Integer, inicioSalida As Integer,
                                  numVertices As Integer, copiarNormales As Boolean)
            Dim m = MatrizEntreBuffers(entrada, salida)
            ' El motor exige que los DOS buffers tengan normales para entrar en la rama que las copia
            ' (0x1418FA860): `copyNormals` sola no alcanza.
            Dim conNormales = copiarNormales AndAlso
                              entrada.Normales IsNot Nothing AndAlso salida.Normales IsNot Nothing
            Dim entero = KernelSimple(entrada, salida, conNormales)
            For i = 0 To numVertices - 1
                Dim p = TransformarPunto(entrada.Vertice(inicioEntrada + i), m)
                If entero Then
                    salida.SetVerticeEntero(inicioSalida + i, p)            ' 0x1418FAACD / 0x1418FAC8C movups
                Else
                    salida.SetVertice(inicioSalida + i, p)                  ' 0x1418FAE77-0x1418FAE8B / 0x1418FB058-0x1418FB06C
                End If
                If conNormales Then
                    ' ⛔ misma `M`, sin traslación, y SIN renormalizar
                    Dim nr = TransformarDireccion(entrada.Normal(inicioEntrada + i), m)
                    If entero Then
                        salida.SetNormalEntera(inicioSalida + i, nr)        ' 0x1418FACBA movups
                    Else
                        salida.SetNormal(inicioSalida + i, nr)              ' 0x1418FB0B5-0x1418FB0C9
                    End If
                End If
            Next
        End Sub

        ''' <summary>
        ''' El despacho de los tres operadores de transferencia — `CopyVertices` `0x1418FA8BD`-`0x1418FA93B`,
        ''' `GatherAll` `0x1418F94A4`-`0x1418F9515`, `GatherSome` `0x1418F9ED4`-`0x1418F9F45`, los tres
        ''' con la MISMA forma:
        ''' <para>· con normales (el bool del operador Y `+0x38` en los dos buffers): el kernel simple
        ''' pide el bit 0 de `+0x20` y de `+0x48` en la entrada Y en la salida.</para>
        ''' <para>· sin normales: el bit 0 de `+0x20` en la entrada y en la salida.</para>
        ''' <para>El kernel simple lee y escribe el elemento de 16 B entero (`movups`): la lane `w` de la
        ''' salida recibe la `w` transformada. El otro, tres `movss` (12 B).</para>
        ''' </summary>
        Friend Function KernelSimple(entrada As Buffer, salida As Buffer, conNormales As Boolean) As Boolean
            If conNormales Then
                Return entrada.LayoutSimple AndAlso entrada.LayoutSimpleNormales AndAlso
                       salida.LayoutSimple AndAlso salida.LayoutSimpleNormales          ' 0x1418FA8D2-0x1418FA8E8
            End If
            Return entrada.LayoutSimple AndAlso salida.LayoutSimple                     ' 0x1418FA910-0x1418FA91A
        End Function

        ''' <summary>
        ''' `hclGatherAllVerticesOperator` (tipo 2) — `0x1418F96F0` (y `0x1418F9450` con normales).
        ''' <para>⛔ El mapa `vertexInputFromVertexOutput` es **SALIDA → ENTRADA**, y sus entradas son
        ''' `int16`. Recorrerlo al revés escribe cada vértice en el lugar equivocado.</para>
        ''' </summary>
        Friend Sub JuntarTodos(entrada As Buffer, salida As Buffer,
                               entradaDesdeSalida As Short(), juntarNormales As Boolean)
            Dim m = MatrizEntreBuffers(entrada, salida)
            Dim conNormales = juntarNormales AndAlso
                              entrada.Normales IsNot Nothing AndAlso salida.Normales IsNot Nothing
            Dim entero = KernelSimple(entrada, salida, conNormales)           ' 0x1418F94A4-0x1418F9515
            For i = 0 To entradaDesdeSalida.Length - 1
                Dim src = CInt(entradaDesdeSalida(i))
                If src < 0 Then Continue For
                Dim p = TransformarPunto(entrada.Vertice(src), m)
                If entero Then
                    salida.SetVerticeEntero(i, p)                              ' 0x1418F96B5 / 0x1418F9870 movups
                Else
                    salida.SetVertice(i, p)                                    ' 0x1418F9A61-0x1418F9A74
                End If
                If conNormales Then
                    Dim nr = TransformarDireccion(entrada.Normal(src), m)
                    If entero Then
                        salida.SetNormalEntera(i, nr)                          ' 0x1418F989A movups
                    Else
                        salida.SetNormal(i, nr)
                    End If
                End If
            Next
        End Sub

        ''' <summary>
        ''' `hclGatherSomeVerticesOperator` (tipo 3) — `0x1418F9E80`, mismo despacho de cuatro.
        ''' <para>Recorre `vertexPairs` (4 B: `indexInput:u16@0`, `indexOutput:u16@2`), no un
        ''' rango.</para>
        ''' </summary>
        Friend Sub JuntarAlgunos(entrada As Buffer, salida As Buffer,
                                 paresEntradaSalida As Integer()(), juntarNormales As Boolean)
            Dim m = MatrizEntreBuffers(entrada, salida)
            Dim conNormales = juntarNormales AndAlso
                              entrada.Normales IsNot Nothing AndAlso salida.Normales IsNot Nothing
            Dim entero = KernelSimple(entrada, salida, conNormales)           ' 0x1418F9ED4-0x1418F9F45
            For Each par In paresEntradaSalida
                Dim src = par(0), dst = par(1)
                Dim p = TransformarPunto(entrada.Vertice(src), m)
                If entero Then
                    salida.SetVerticeEntero(dst, p)                            ' 0x1418FA4F8 / 0x1418FA6AC movups
                Else
                    salida.SetVertice(dst, p)
                End If
                If conNormales Then
                    Dim nr = TransformarDireccion(entrada.Normal(src), m)
                    If entero Then
                        salida.SetNormalEntera(dst, nr)                        ' 0x1418FA6E3 movups
                    Else
                        salida.SetNormal(dst, nr)
                    End If
                End If
            Next
        End Sub

        ' -----------------------------------------------------------------------------------------

        ''' <summary>
        ''' `hclMoveParticlesOperator` (tipo 11) — escalar `0x141952660`, SIMD `0x141952480`.
        ''' <para>✅ **Los dos kernels son la MISMA ley** (verificado en el cap. 6bis): lo único que
        ''' cambia es el layout de buffer que asumen (`bufferReal[+0x20] & 1`).</para>
        ''' <para>⛔⛔ **Las DOS ramas son distintas y la que corre en las prendas reales es la B**:</para>
        ''' <para>· sin partículas fijas ⇒ `positions[p] = previous[p] = q`</para>
        ''' <para>· con partículas fijas ⇒ `previous[p] = positions[p]` (el VIEJO) y `positions[p] = q`</para>
        ''' <para>⭐ La rama B deja el par `(dónde estaba, a dónde va)`, que es exactamente lo que el
        ''' snapshot de anclas del cap. 5 guarda y lo que `lerp(prev, pos, (s+1)/N)` camina. Colapsar
        ''' las dos ramas mata la interpolación de anclas sin dejar rastro en los números.</para>
        ''' </summary>
        Friend Sub MoverParticulas(inst As Instancia, buf As Buffer,
                                   paresVerticeParticula As Integer()())
            Dim m = buf.AEspacioDeSimulacion
            Dim hayFijas = inst.ParticulasFijas IsNot Nothing AndAlso inst.ParticulasFijas.Length > 0
            For Each par In paresVerticeParticula
                Dim iv = par(0), ip = par(1)
                Dim q = TransformarPunto(buf.Vertice(iv), m)
                If hayFijas Then
                    Simd.Escribir(inst.Previas, ip, Simd.Leer(inst.Posiciones, ip))   ' ⬅ el VIEJO
                    Simd.Escribir(inst.Posiciones, ip, q)
                Else
                    Simd.Escribir(inst.Posiciones, ip, q)
                    Simd.Escribir(inst.Previas, ip, q)
                End If
            Next
        End Sub

        ' -----------------------------------------------------------------------------------------

        ''' <summary>
        ''' ⭐ El deform de UN hueso desde su triangulo — el mismo kernel que
        ''' <see cref="DeformarHuesosSimple"/> corre por lote (`0x14195AA90`), para un caso suelto.
        ''' <para>El marco del triangulo es `(a, b, n, c)` con `c` el centroide, `a = p0 - c`,
        ''' `b = p1 - c` y `n = a × b` CRUDA, sin normalizar. Sobre el se pasan las filas del
        ''' `localBoneTransform` con la forma de CUATRO terminos, y las tres primeras salen
        ''' ortonormalizadas.</para>
        ''' <para>Existe para que los arneses midan con la aritmetica del motor y no con una copia.</para>
        ''' </summary>
        Friend Function DeformarUnHueso(local As Mat4, p0 As Vector128(Of Single),
                                        p1 As Vector128(Of Single), p2 As Vector128(Of Single)) As Mat4
            Dim unTercio = Vector128.Create(0.333333343F)          ' 0x142F3C660
            Dim c = Vector128.Multiply(Vector128.Add(Vector128.Add(p0, p1), p2), unTercio)
            Dim a = Vector128.Subtract(p0, c)
            Dim b = Vector128.Subtract(p1, c)
            Dim n = Polar.Cruz(a, b)                                ' CRUDO, sin normalizar

            Dim mTri As Mat4
            mTri.F0 = a : mTri.F1 = b : mTri.F2 = n
            mTri.F3 = c.WithElement(Simd.LaneW, 1.0F)

            Dim r0 = FilaPorMarco(local.F0, mTri)
            Dim r2 = FilaPorMarco(local.F2, mTri)                   ' ⛔ local.F1 NO se lee
            Dim tt = FilaPorMarco(local.F3, mTri)
            Dim u = Polar.Cruz(r2, r0)
            Dim vv = Polar.Cruz(u, r2)

            Dim salida As Mat4
            salida.F0 = ConWCero(Simd.NormalizarCrudo(vv))
            salida.F1 = ConWCero(Simd.NormalizarCrudo(u))
            salida.F2 = ConWCero(Simd.NormalizarCrudo(r2))
            salida.F3 = tt.WithElement(Simd.LaneW, 1.0F)
            Return salida
        End Function

        ''' <summary>
        ''' `hclSimpleMeshBoneDeformOperator` (tipo 17) — `0x14195B320`. **El writeback que ve el
        ''' render**: convierte los triángulos simulados en transforms de hueso.
        ''' <para>Por cada `triangleBonePair` (`{boneOffset:u16, triangleOffset:u16}`, 4 B):</para>
        ''' <para>⛔ `triangleOffset` y `boneOffset` están **en BYTES** (64 por hueso), no en índices.</para>
        ''' <para>⛔ `L.fila1` (el `+0x10` del `localBoneTransform`) **NO SE LEE NUNCA**: es la fila
        ''' que se reconstruye con los dos productos cruz.</para>
        ''' <para>**Prueba de identidad**: con `[R0;R1;R2]` ortonormal a derechas, `U = R2×R0 = R1` y
        ''' `V = U×R2 = R0` ⇒ devuelve la misma terna. Toda transcripción tiene que pasarla — es la
        ''' rutina que ya falló una vez en este árbol, con dos filas intercambiadas.</para>
        ''' <para>⭐⭐ **EL MARCO ESCALA CON EL ÁREA.** `n = a × b` va **crudo**: el cruz de dos
        ''' `shufps` de `0x14195B415`-`0x14195B43D` no tiene ni un `rsqrt`. Como la traslación lleva
        ''' el término `L.z · n`, un estirón del triángulo entra **al cuadrado**.</para>
        ''' <para>⭐ MEDIDO, y AISLADO de cualquier prenda — `GO4f` de `MotorFisicaGate`: con
        ''' `L.F3 = (0, 0, 1, 0)` para que sólo quede el término `L.z·n`, escalar el triángulo por
        ''' **3** multiplica la traslación por **9** (0,33333325 → 3), no por 3.</para>
        ''' <para>⛔ NO es un defecto de la transcripción: bajo giro **rígido** el mismo camino da
        ''' x1,008 y 0,006 u de movimiento (ley `L6a` de `ClothPhysicsGate`). ⚠️ Eso descarta las
        ''' matrices mal compuestas **que fallan bajo rotación**; NO descarta una influencia que
        ''' resuelva al hueso equivocado cuando dos huesos coinciden en bind, que también daría
        ''' bind exacto y giro rígido exacto.</para>
        ''' <para>OBSERVADO (no es prueba, es el síntoma que llevó a mirar): en `ClothPhysicsGate`
        ''' con `PrewarDress` y el clip `Stand_to_Run_L180`, en el estado `Animate`
        ''' (`ObjectSpaceSkinPN → CopyVertices → SimpleMeshBoneDeform`, sin solver), el desvío del
        ''' peor cloth-bone recorre `10,5 · 34,2 · 54,7 · 116,3 · 120,6 · 97,2 u` siguiendo el paso,
        ''' con pico en `Bone_Cloth_H_007`; con solver queda acotado en `7,9`-`36,3 u`.</para>
        ''' <para>⛔⛔ Acá antes decía que eso prueba el mecanismo porque `8,24² × 1,8 ≈ 120`, y esa
        ''' cuenta es CIRCULAR (motor-84): el x8,24 se mide sobre la malla que estos mismos
        ''' cloth-bones deforman, así que el efecto explicaba la causa — y el «brazo de 1,8 u» no
        ''' estaba medido en ninguna parte. También decía que «es la razón de que el motor
        ''' SIMULE»: eso es una conjetura sobre el diseño de Havok, sin cita, y sale.</para>
        ''' </summary>
        Friend Sub DeformarHuesosSimple(buf As Buffer, transforms As Mat4(),
                                        pares As Integer()(), localBoneTransforms As Mat4())
            Dim unTercio = Vector128.Create(0.333333343F)          ' 0x142F3C660
            For k = 0 To pares.Length - 1
                Dim boneOffset = pares(k)(0)                        ' en BYTES
                Dim triangleOffset = pares(k)(1)                    ' en BYTES
                Dim t0 = triangleOffset \ 2                         ' índice del primer u16
                Dim i0 = CInt(buf.IndicesDeTriangulo(t0))
                Dim i1 = CInt(buf.IndicesDeTriangulo(t0 + 1))
                Dim i2 = CInt(buf.IndicesDeTriangulo(t0 + 2))

                Dim p0 = buf.Vertice(i0), p1 = buf.Vertice(i1), p2 = buf.Vertice(i2)
                Dim c = Vector128.Multiply(Vector128.Add(Vector128.Add(p0, p1), p2), unTercio)
                Dim a = Vector128.Subtract(p0, c)
                Dim b = Vector128.Subtract(p1, c)
                Dim n = Polar.Cruz(a, b)                            ' CRUDO, sin normalizar

                Dim mTri As Mat4
                mTri.F0 = a : mTri.F1 = b : mTri.F2 = n
                mTri.F3 = c.WithElement(Simd.LaneW, 1.0F)

                ' ⛔⛔ LAS TRES FILAS SE TRANSFORMAN CON **CUATRO** TÉRMINOS, incluida la `w` de `L`:
                '   r = ((L.y·b + L.x·a) + L.z·n) + L.w·c        con  c.w = 1
                ' El `shufps 0xFF` de `0x14195B458` difunde `L.w` y `0x14195B45C` lo multiplica por
                ' `c`; la suma entra en `0x14195B476`. Usar `TransformarDireccion` (descarta `L.w`) y
                ' `TransformarPunto` (asume `L3.w = 1`) coincide **sólo si** el archivo trae
                ' `w = (0, ·, 0, 1)` en esas filas, y eso NO está medido (motor-56).
                Dim l = localBoneTransforms(k)
                Dim r0 = FilaPorMarco(l.F0, mTri)
                Dim r2 = FilaPorMarco(l.F2, mTri)                   ' ⛔ l.F1 NO se lee
                Dim tt = FilaPorMarco(l.F3, mTri)

                Dim u = Polar.Cruz(r2, r0)
                Dim vv = Polar.Cruz(u, r2)

                Dim salida As Mat4
                salida.F0 = ConWCero(Simd.NormalizarCrudo(vv))
                salida.F1 = ConWCero(Simd.NormalizarCrudo(u))
                salida.F2 = ConWCero(Simd.NormalizarCrudo(r2))
                salida.F3 = tt.WithElement(Simd.LaneW, 1.0F)
                transforms(boneOffset \ 64) = salida                ' boneOffset en BYTES, 64 por hueso
            Next
        End Sub

        ''' <summary>
        ''' `((v.y·M1 + v.x·M0) + v.z·M2) + v.w·M3` — la forma de **cuatro términos** del deform
        ''' (`0x14195B451`-`0x14195B476`), donde `M3` es `(centroide, 1)`.
        ''' <para>⭐ Con `v.w = 0` da la dirección y con `v.w = 1` el punto, así que cubre las tres
        ''' filas y la traslación **sin suponer nada** del archivo.</para>
        ''' </summary>
        Friend Function FilaPorMarco(v As Vector128(Of Single), m As Mat4) As Vector128(Of Single)
            Dim r = Vector128.Multiply(Simd.BcastY(v), m.F1)                 ' 0x14195B44D
            r = Vector128.Add(r, Vector128.Multiply(Simd.BcastX(v), m.F0))   ' 0x14195B455/45F
            r = Vector128.Add(r, Vector128.Multiply(Simd.BcastZ(v), m.F2))   ' 0x14195B470/473
            Return Vector128.Add(r, Vector128.Multiply(Simd.BcastW(v), m.F3))  ' 0x14195B45C/476
        End Function

        ''' <summary>Fuerza la `w` a cero — el `pslldq 4` + `psrldq 4` del motor.
        ''' <para>⭐ Los vectores `a`, `b` y `n` del marco ya llegan con `w = 0` porque
        ''' <see cref="Buffer.Vertice"/> lo pone: eso es lo que reemplaza a los `pslldq`/`psrldq` del
        ''' motor (`0x14195B466`/`0x14195B46B`).</para></summary>
        Private Function ConWCero(v As Vector128(Of Single)) As Vector128(Of Single)
            Return v.WithElement(Simd.LaneW, 0.0F)
        End Function

    End Module

End Namespace

