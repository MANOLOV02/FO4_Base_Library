Option Strict On
Option Explicit On

Imports System.Runtime.Intrinsics

' =================================================================================================
' LAS ANCLAS — el snapshot de partículas fijas y su interpolación por substep.
'
' Ley: RE_MOTOR_FISICA_CANONICO_2026-09-05.md, cap. 5, pasos (3d) y (4c).
' Snapshot: `0x14195C870`.  Interpolación: `0x14195CB88`.
'
' ⛔⛔ ES LO QUE ENGANCHA LA PRENDA AL ESQUELETO. Sin esto las partículas fijas no se mueven con el
' hueso y la tela se queda atrás del personaje — es el primer defecto que se ve en un PNG.
'
' ⛔ Y las dos cuentas son de BITS, no de álgebra:
'   · `α = (float)(s+1) · (1/N)`, con `1/N` calculado UNA vez (`0x14195C6E6`), no `(s+1)/N`.
'   · `P = (1−α)·previa + α·posicion`, NO `previa + α·(posicion − previa)`.
' Las dos formas son iguales en los reales y distintas en `float`, y esto corre por cada ancla de
' cada substep de cada frame.
' =================================================================================================

#If DEBUG Then

Namespace Havok.Motor

    ''' <summary>
    ''' El par `(previous, positions)` que se guarda de cada partícula fija ANTES del bucle de
    ''' substeps — el registro de `0x20` B de `0x14195C870`.
    ''' </summary>
    Friend Structure AnclaGuardada

        ''' <summary>`+0x00` — el `previous` del arranque del frame.</summary>
        Friend Previa As Vector128(Of Single)

        ''' <summary>`+0x10` — el `positions` del arranque del frame, que es a dónde la llevó
        ''' `MoveParticles` con el esqueleto ya animado.</summary>
        Friend Posicion As Vector128(Of Single)

    End Structure

    ' =============================================================================================

    Friend Module Anclas

        ''' <summary>
        ''' El SNAPSHOT del paso (3d) — `0x14195C870`. Se toma **una vez por frame**, antes del
        ''' primer substep.
        ''' <para>⛔ El par se guarda en el orden `(previous, positions)`, y ése es el orden en que
        ''' la interpolación los pesa: `previous` con `1−α` y `positions` con `α`. Al revés, la
        ''' partícula camina para atrás.</para>
        ''' <para>⭐ **Verificado, no supuesto**: `0x14195C7FB` carga `r12 = simCloth[+0x18]`
        ''' (positions) y `0x14195C805` carga `r13 = simCloth[+0x28]` (previous); el snapshot de
        ''' `0x14195C888`/`88E` lee `xmm0 = [r13]` y `xmm1 = [r12]`, y `0x14195C893`/`898` los
        ''' escribe en `+0x00` y `+0x10`. ⇒ `+0x00` es `previous`.</para>
        ''' </summary>
        Friend Function Guardar(inst As Instancia) As AnclaGuardada()
            Dim fijas = inst.ParticulasFijas
            If fijas Is Nothing OrElse fijas.Length = 0 Then Return Array.Empty(Of AnclaGuardada)()
            Dim r(fijas.Length - 1) As AnclaGuardada
            For i = 0 To fijas.Length - 1
                Dim p = fijas(i)
                r(i).Previa = Simd.Leer(inst.Previas, p)      ' +0x00
                r(i).Posicion = Simd.Leer(inst.Posiciones, p) ' +0x10
            Next
            Return r
        End Function

        ''' <summary>
        ''' La INTERPOLACIÓN del paso (4c) — `0x14195CB88`, entre la colisión y la integración.
        ''' <para>
        ''' ```
        ''' α = (float)(iSubstep + 1) · invN          ' 0x14195CB98 cvtsi2ss + 0x14195CBA0 mulss
        ''' β = 1,0 − α                               ' 0x14195CBA5 subss (1,0 en 0x142929458)
        ''' P = β·previa + α·posicion                 ' 0x14195CBD2 / 0x14195CBD5
        ''' positions[p] = previous[p] = P            ' ⬅ LAS DOS
        ''' ```
        ''' </para>
        ''' <para>⛔ `invN` entra por parámetro **a propósito**: el motor lo calcula una sola vez en
        ''' `0x14195C6E6` (`1,0 / numSubSteps`) y lo reusa para `α` y para `dtSub`. Calcularlo acá
        ''' por substep daría el mismo real y otro bit.</para>
        ''' <para>⭐ Escribe `previous` **igual que** `positions`, o sea que el ancla queda con
        ''' velocidad CERO en el substep. Es lo contrario de lo que hace `usaK` en los constraint
        ''' sets, y es lo correcto: un ancla no tiene inercia propia, la manda el hueso.</para>
        ''' </summary>
        Friend Sub Interpolar(inst As Instancia, guardadas As AnclaGuardada(),
                              iSubstep As Integer, invN As Single)
            If guardadas Is Nothing OrElse guardadas.Length = 0 Then Return
            Dim fijas = inst.ParticulasFijas
            Dim alfa = CSng(iSubstep + 1) * invN                  ' 0x14195CB98 + 0x14195CBA0
            Dim beta = 1.0F - alfa                                ' 0x14195CBA5
            Dim va = Vector128.Create(alfa)                       ' 0x14195CBAC shufps 0
            Dim vb = Vector128.Create(beta)                       ' 0x14195CBB0 shufps 0
            For i = 0 To guardadas.Length - 1
                Dim p = fijas(i)
                ' ⛔ el motor suma con el término de α PRIMERO (0x14195CBE9 `addps xmm1, xmm0`,
                ' con xmm1 = α·posicion de 0x14195CBD2 y xmm0 = β·previa de 0x14195CBD5). La suma de
                ' dos `float` es conmutativa y exacta, así que da el mismo bit — queda en el orden
                ' del motor igual, para que nadie tenga que volver a comprobarlo.
                Dim r = Vector128.Add(Vector128.Multiply(va, guardadas(i).Posicion),
                                      Vector128.Multiply(vb, guardadas(i).Previa))
                ' ⛔⛔ DOS escrituras del MISMO valor, verificadas: 0x14195CBEC a `positions` y
                ' 0x14195CBF2 a `previous`, con el mismo índice.
                Simd.Escribir(inst.Posiciones, p, r)
                Simd.Escribir(inst.Previas, p, r)
            Next
        End Sub

    End Module

End Namespace

#End If
