Option Strict On
Option Explicit On

Imports System.Runtime.Intrinsics

' =================================================================================================
' LA INTEGRACIÓN — `0x141A12EE0` (`TtActions` + `TtIntegrate`).
'
' Ley: RE_MOTOR_FISICA_CANONICO_2026-09-05.md, cap. 6.1, verificado instrucción por instrucción
' sobre la cola escalar `0x141A132D0`-`0x141A1330E` (el bucle principal está desenrollado ×4 y hace
' lo mismo).
'
' ⛔ El orden de las cuentas NO es libre. El motor arma la aceleración así:
'       ((mass·gravity + F) · invMass) · dtSub²
' y la suma a  (pos + (pos − prev)·damping)  ya calculado. Reagrupar cambia el redondeo.
'
' ⛔ `previous[i]` recibe el `pos` **VIEJO**, no el nuevo. Escribirlo después de actualizar `pos`
' (o leerlo de la posición ya escrita) convierte el Verlet en un Euler y la tela pierde toda su
' inercia — sin dejar NaN ni nada que se note en los números.
' =================================================================================================


Namespace Havok.Motor

    Friend Module Integrador

        ''' <summary>
        ''' Un substep de integración de Verlet con damping — `0x141A12EE0`.
        ''' <para>Por partícula (`0x141A132D0`-`0x141A1330E`):</para>
        ''' <para>`pos' = pos + (pos − prev)·damping + (mass·gravity + F)·invMass·dtSub²`</para>
        ''' <para>`prev = pos` (el VIEJO)</para>
        ''' <para>`dtSub²` se calcula UNA vez fuera del bucle, escalar y después difundido
        ''' (`0x141A12F95`/`99`/`A1`: `movaps` + `mulss` + `shufps 0`), no `dt·dt` por partícula.</para>
        ''' <para>⛔ `gravity` es el `vec4` **completo** de `simulationInfo +0x00` — se multiplica con
        ''' `mulps`, así que la lane `w` también entra. En el corpus es 0, pero copiarlo como `vec3`
        ''' sería inventar.</para>
        ''' </summary>
        ''' <param name="inst">La instancia; se le escriben `Posiciones` y `Previas`.</param>
        ''' <param name="gravedad">`info.gravity`, `vec4` completo.</param>
        ''' <param name="fuerzas">El buffer de fuerzas del substep, `AnchoDeParticula` por partícula.
        ''' <para>⛔ En el motor, **asignarlo, cerarlo, correr las acciones e integrar son UNA sola
        ''' función** (`0x141A12EE0`): el cerado está en `0x141A13007`-`0x141A1302D` y las acciones
        ''' en `0x141A13080`-`0x141A130AB`, antes del bucle de integración. Acá va partido en
        ''' <see cref="CerarFuerzas"/> + <see cref="Integrar"/>, que es equivalente **si el
        ''' llamador respeta ese orden** — y por eso queda dicho acá (motor-58).</para></param>
        ''' <param name="dtSub">El paso del substep.</param>
        Friend Sub Integrar(inst As Instancia, gravedad As Vector128(Of Single),
                            fuerzas As Single(), dtSub As Single)
            Dim dt2 = Vector128.Create(dtSub * dtSub)               ' 0x141A12F99 mulss + 0x141A12FA1
            Dim damp = Vector128.Create(inst.DampingEfectivo)       ' 0x141A12FAB + 0x141A12FBC

            Dim pos = inst.Posiciones
            Dim prev = inst.Previas

            For i = 0 To inst.NumParticulas - 1
                Dim p = Simd.Leer(pos, i)                           ' 0x141A132D0 movups xmm2
                Dim masa = Vector128.Create(inst.Masa(i))           ' 0x141A132D5 movss + shufps 0
                Dim inv = Vector128.Create(inst.InvMasa(i))         ' 0x141A132DA movss + shufps 0

                ' (mass·gravity + F) · invMass · dtSub²   — EN ESE ORDEN
                Dim a = Vector128.Multiply(masa, gravedad)          ' 0x141A132E4 mulps
                a = Vector128.Add(a, Simd.Leer(fuerzas, i))         ' 0x141A132ED addps
                a = Vector128.Multiply(a, inv)                      ' 0x141A132F0 mulps
                a = Vector128.Multiply(a, dt2)                      ' 0x141A132FB mulps

                ' pos + (pos − prev)·damping
                Dim v = Vector128.Subtract(p, Simd.Leer(prev, i))   ' 0x141A132F6 subps
                v = Vector128.Multiply(v, damp)                     ' 0x141A132FF mulps
                v = Vector128.Add(v, p)                             ' 0x141A13303 addps

                Simd.Escribir(pos, i, Vector128.Add(a, v))          ' 0x141A13306 + 0x141A13309
                Simd.Escribir(prev, i, p)                           ' 0x141A1330E  ⬅ el pos VIEJO
            Next
        End Sub

        ''' <summary>
        ''' Cera el buffer de fuerzas. El motor lo hace **una vez por substep, antes de las
        ''' acciones** — las fuerzas no se acumulan de un substep al siguiente.
        ''' </summary>
        Friend Sub CerarFuerzas(fuerzas As Single())
            Array.Clear(fuerzas, 0, fuerzas.Length)
        End Sub

    End Module

End Namespace

