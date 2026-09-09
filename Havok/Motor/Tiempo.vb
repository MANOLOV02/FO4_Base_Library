Option Strict On
Option Explicit On

Imports System.Runtime.Intrinsics

' =================================================================================================
' EL MODELO DE TIEMPO: `prepare` (`0x14195B7B0`) y el factor de rigidez `k` (`0x1418C6420`).
'
' Ley: RE_MOTOR_FISICA_CANONICO_2026-09-05.md, caps. 6.2, 6.2ter (nuevo), 6.3 y 6.3bis.
'
' ⛔⛔ `0x14195B7B0` NO es «el damping». Hace CUATRO cosas, y la que faltaba en la primera lectura
' del RE es la más importante para que la tela no pegue un tirón: **cuando el paso cambia, re-escala
' `previous` para conservar la velocidad de Verlet** (la cadena `TtUpdate Particles Time Step`,
' `0x14195BA1C`-`0x14195BA69`). Sin eso, cambiar de LOD o de framerate multiplica la velocidad de
' cada partícula por el cociente de los pasos.
' =================================================================================================

#If DEBUG Then

Namespace Havok.Motor

    Friend Module Tiempo

        ''' <summary>`−1,72500002` — `0x1426B4C80` (`0xBFDCCCCD`), el exponente de la base de `k`.</summary>
        Friend ReadOnly ExponenteRigidez As Single = BitConverter.Int32BitsToSingle(&HBFDCCCCD)

        ''' <summary>`hclConstraintSet` de tipo `hclLocalRangeConstraintSet`. Su `k` es 1 en el
        ''' ÚLTIMO substep y 0 en los demás (`0x1418C64B8`).</summary>
        Friend Const TipoLocalRange As Integer = 5

        ''' <summary>`hclTransitionConstraintSet`: su `k` es una rampa `(i+1)/n` (`0x1418C64C4`).</summary>
        Friend Const TipoTransition As Integer = 8

        ''' <summary>`hclBonePlanesConstraintSet`: como `LocalRange`, 1 o 0 (`0x1418C64B8`).</summary>
        Friend Const TipoBonePlanes As Integer = 10

        ' -----------------------------------------------------------------------------------------
        ' El paso
        ' -----------------------------------------------------------------------------------------

        ''' <summary>
        ''' El `dtSub` que calcula `prepare`: `dtFrame / (numSubSteps · sExp)` — `0x14195B834`
        ''' (`mulss`) y `0x14195B83E` (`divss`, EXACTO).
        ''' <para>⛔ `sExp` **no** es `s1` a secas: es `1,0` si `modo == 1` y `s1` si no
        ''' (`0x14195B7F6`). Y `execute` divide el `dt` por `s1` **siempre** (`0x14195C3EF`), sin ese
        ''' `if`. La asimetría es del motor; con el modo 2 que pone Bethesda coinciden.</para>
        ''' </summary>
        Friend Function DtSubDePrepare(dtFrame As Single, numSubSteps As Integer,
                                       modo As Integer, s1 As Single) As Single
            Dim sExp = If(modo = 1, 1.0F, s1)                 ' 0x14195B7F6 cmp / jne
            Return dtFrame / (CSng(numSubSteps) * sExp)       ' 0x14195B834 + 0x14195B83E
        End Function

        ''' <summary>
        ''' ⭐⭐ **La ley que faltaba.** Re-escala `previous` para que la velocidad de Verlet
        ''' `(pos − prev)` quede multiplicada por `dtNuevo/dtViejo` — `0x14195BA1C`-`0x14195BA69`,
        ''' la cadena de perfilado `TtUpdate Particles Time Step`.
        ''' <para>`r = 1 − dtNuevo/dtViejo` (`0x14195BA29` `divss` + `0x14195BA2E` `subss`, los dos
        ''' EXACTOS), difundido a las cuatro lanes (`0x14195BA32` `shufps 0`), y después, partícula
        ''' por partícula de a 16 B: `previous[i] += (positions[i] − previous[i]) · r`.</para>
        ''' <para>⛔ Con `r = dtNuevo/dtViejo` en vez de `1 − …` la velocidad sale invertida —
        ''' salvo justo cuando el cociente vale `0,5`, donde las dos formas coinciden. Por eso el
        ''' gate mide con un cociente que **no** es `0,5`.</para>
        ''' <para>Este método es el bucle **puro**: las ramas (`dtViejo == dtNuevo`, primera vez)
        ''' viven en <see cref="Preparar"/>, que es donde el motor las tiene.</para>
        ''' </summary>
        Friend Sub ReescalarPrevias(inst As Instancia, dtViejo As Single, dtNuevo As Single)
            Dim r = Vector128.Create(1.0F - dtNuevo / dtViejo)          ' 0x14195BA29 + 0x14195BA2E/32
            For i = 0 To inst.NumParticulas - 1
                Dim p = Simd.Leer(inst.Posiciones, i)                   ' 0x14195BA51
                Dim q = Simd.Leer(inst.Previas, i)
                Dim d = Vector128.Subtract(p, q)                        ' 0x14195BA55
                Simd.Escribir(inst.Previas, i,
                              Vector128.Add(Vector128.Multiply(d, r), q))   ' 0x14195BA59 + 0x14195BA5C
            Next
        End Sub

        ''' <summary>
        ''' `hclSimulateOperator::prepare` — `0x14195B7B0`, la parte del tiempo.
        ''' <para>⛔ **La rama de arriba corta TODO**: si el `dtSub` no cambió, el motor no re-escala
        ''' `previous` **y tampoco recalcula el damping** (`0x14195B90F` `ucomiss` / `je` salta
        ''' directo al final). Eso es lo que la hace medible: con el mismo paso y un
        ''' `globalDampingPerSecond` distinto, el damping efectivo **se queda con el viejo**.</para>
        ''' <para>La segunda rama es la primera vez (`dtViejo == 0`, `0x14195B919`): cachea y calcula
        ''' el damping, pero **no** re-escala — no hay velocidad previa que conservar.</para>
        ''' <para>⛔ LA SIEMBRA DEL TRANSFORM DE TRANSFERENCIA VIVE ACÁ (`0x14195B848`-`0x14195B8FF`),
        ''' no en `execute`. Su señal de «primera vez» es `dtSubCacheado == 0` (`0x14195B827
        ''' ucomiss` + `0x14195B842 jne`) — la MISMA que usa el resto de este método — y encima
        ''' pide `transferMotionEnabled` (+0x1E, `0x14195B850`). Estaba en `Motor.Simular` con un
        ''' booleano propio: dos dueños para una señal, y en la fase equivocada (motor-125).</para>
        ''' <para>⚠️ Falta acá la reconstrucción de la lista de acciones
        ''' (`0x14195BB52`-`0x14195BE1B`): necesita piezas que todavía no existen y va con ellas,
        ''' no adivinada acá.</para>
        ''' </summary>
        Friend Sub Preparar(inst As Instancia, dtFrame As Single, numSubSteps As Integer,
                            dampingPorSegundo As Single,
                            transferenciaHabilitada As Boolean, transformDeTransferencia As Mat4)
            Dim dtNuevo = DtSubDePrepare(dtFrame, numSubSteps, inst.Modo, inst.S1)
            Dim dtViejo = inst.DtSubCacheado

            ' ⛔ LA SIEMBRA VA PRIMERO y con la senal del motor, no con un booleano aparte:
            ' `dtSubCacheado == 0` es la primera vez (0x14195B827/42) y ademas tiene que estar
            ' habilitada la transferencia (0x14195B850). Sin esto el primer cuadro ve la
            ' diferencia entre la identidad y la pose real del hueso y la tela sale disparada.
            If dtViejo = 0.0F AndAlso transferenciaHabilitada Then
                inst.TransformPrevioDeTransferMotion = transformDeTransferencia
            End If

            If dtViejo = dtNuevo Then Return                             ' 0x14195B90F ucomiss / je
            If dtViejo <> 0.0F Then ReescalarPrevias(inst, dtViejo, dtNuevo)   ' 0x14195B919 jne

            inst.DtSubCacheado = dtNuevo                                 ' 0x14195B923 / 0x14195BA69
            inst.DampingEfectivo = DampingEfectivo(dampingPorSegundo, dtNuevo)
        End Sub

        ''' <summary>
        ''' El damping efectivo — `0x14195B96F`-`0x14195B99D` (y su gemelo `0x14195BAEC`).
        ''' <para>Tres tramos, con los bordes exactos: `d &gt;= 1` ⇒ **0** (`comiss`/`jb`: si NO es
        ''' menor que 1, cero) · `d == 0` ⇒ **1,0** (`0x3F800000` escrito literal) · si no,
        ''' `powf(1 − d, dtSub)` (`0x1422C47F8`, el `powf` del CRT).</para>
        ''' <para>⚠️ `MathF.Pow` de .NET no promete ser bit-idéntico al `powf` de MSVC. La diferencia
        ''' es de redondeo (≤ 1 ulp) y entra multiplicando `(pos − prev)`; queda declarada, no
        ''' escondida. Ver <see cref="Simd.PowCrt"/>.</para>
        ''' </summary>
        Friend Function DampingEfectivo(dampingPorSegundo As Single, dtSub As Single) As Single
            If Not (dampingPorSegundo < 1.0F) Then Return 0.0F          ' 0x14195B96F comiss / jb
            If dampingPorSegundo = 0.0F Then Return 1.0F                ' 0x14195B97C ucomiss / jne
            Return Simd.PowCrt(1.0F - dampingPorSegundo, dtSub)         ' 0x14195B98E + 0x14195B998
        End Function

        ' -----------------------------------------------------------------------------------------
        ' El factor de rigidez `k` — 0x1418C6420
        ' -----------------------------------------------------------------------------------------

        ''' <summary>
        ''' `StiffnessFactor(set, modo, iSubstep, numSubSteps, s1, s2)` — `0x1418C6420`.
        ''' <para>`modo != 2` ⇒ **1,0** (`0x1418C6438`). `numSubSteps == 1` y `s1 == 1` y `s2 == 1`
        ''' ⇒ **1,0** (`0x1418C646E`-`0x1418C647B`, las tres condiciones encadenadas).</para>
        ''' <para>Si no: `base = powf(numSubSteps · s1 · s2, −1,72500002)` y después el `switch` por
        ''' `set.type` (`[set+0x18]`, `0x1418C64A6`):</para>
        ''' <para>· **5** (`LocalRange`) o **10** (`BonePlanes`) ⇒ `1,0` en el ÚLTIMO substep y
        ''' **`0,0`** en los demás — nunca `base` (`0x1418C64BF` / `0x1418C64D4` `xorps`).</para>
        ''' <para>· **8** (`Transition`) ⇒ `(iSubstep + 1) / numSubSteps` (`divss` exacto).</para>
        ''' <para>· el resto ⇒ `base`.</para>
        ''' <para>⚠️ El motor evalúa el `powf` **antes** del `switch`, o sea que lo paga también para
        ''' los tipos 5, 8 y 10 que no lo usan. Acá se evalúa perezosamente: mismo número, menos
        ''' costo. Es la única diferencia deliberada con el `.exe` en esta función.</para>
        ''' </summary>
        Friend Function FactorDeRigidez(tipoDeSet As Integer, modo As Integer,
                                        iSubstep As Integer, numSubSteps As Integer,
                                        s1 As Single, s2 As Single) As Single
            If modo <> 2 Then Return 1.0F                               ' 0x1418C6438
            If numSubSteps = 1 AndAlso s1 = 1.0F AndAlso s2 = 1.0F Then Return 1.0F   ' 0x1418C646E…

            Select Case tipoDeSet
                Case TipoLocalRange, TipoBonePlanes                     ' 0x1418C64A9 / 0x1418C64B3
                    Return If(iSubstep = numSubSteps - 1, 1.0F, 0.0F)   ' 0x1418C64B8/BF/D4
                Case TipoTransition                                     ' 0x1418C64AE
                    Return CSng(iSubstep + 1) / CSng(numSubSteps)       ' 0x1418C64C4…CE divss
                Case Else
                    Return Simd.PowCrt(CSng(numSubSteps) * s1 * s2, ExponenteRigidez)  ' 0x1418C64A1
            End Select
        End Function

        ''' <summary>
        ''' Si el bucle de solve usa `k` o lo ignora — `0x141A133E0`.
        ''' <para>`usaK = NO( modo == 1 ó (numSubSteps == 1 y s1 == 1 y s2 == 1) )`. Con `usaK`
        ''' falso el set recibe la señal de que **no** escale nada; no es «pasarle k = 1».</para>
        ''' </summary>
        Friend Function UsaK(modo As Integer, numSubSteps As Integer, s1 As Single, s2 As Single) As Boolean
            If modo = 1 Then Return False
            If numSubSteps = 1 AndAlso s1 = 1.0F AndAlso s2 = 1.0F Then Return False
            Return True
        End Function

    End Module

End Namespace

#End If
