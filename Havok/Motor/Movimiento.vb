Option Strict On
Option Explicit On

Imports System.Runtime.Intrinsics

' =================================================================================================
' TRANSFER MOTION — `0x141A13950` (RE 6.10), bloque `"TtTransfer Motion"` (`0x14271A1B0`).
'
' Es el paso 1 del cuadro: le pasa a la tela una PARTE del movimiento del hueso raiz, para que la
' prenda «acompañe» al personaje en vez de quedarse atras. La parte se decide con dos rampas
' independientes —una por velocidad lineal y otra por velocidad angular— entre un minimo y un
' maximo declarados en `hclSimClothData.transferMotionData` (`+0x150`).
'
' ⛔⛔ Los dos blends **arrancan en CERO**, no en su minimo: con el flag de su eje apagado, la
' transferencia queda apagada de verdad. El `min*Blend` se carga DENTRO del `if`.
' =================================================================================================


Namespace Havok.Motor

    ''' <summary>
    ''' `hclSimClothDataTransferMotionData` — `hclSimClothData+0x150`, `0x30` B.
    ''' <para>Campos de la reflexion, la lista es cerrada: `transformSetIndex` (`+0x00`),
    ''' `transformIndex` (`+0x04`), `transferTranslationMotion` (`+0x08`, bool),
    ''' `minTranslationSpeed` (`+0x0C`), `maxTranslationSpeed` (`+0x10`),
    ''' `minTranslationBlend` (`+0x14`), `maxTranslationBlend` (`+0x18`),
    ''' `transferRotationMotion` (`+0x1C`, bool), `minRotationSpeed` (`+0x20`),
    ''' `maxRotationSpeed` (`+0x24`), `minRotationBlend` (`+0x28`),
    ''' `maxRotationBlend` (`+0x2C`).</para>
    ''' </summary>
    Friend Structure DatosDeTransferencia
        Friend IndiceDelSet As Integer
        Friend IndiceDelTransform As Integer
        Friend TransfiereTraslacion As Boolean
        Friend VelMinTraslacion As Single
        Friend VelMaxTraslacion As Single
        Friend BlendMinTraslacion As Single
        Friend BlendMaxTraslacion As Single
        Friend TransfiereRotacion As Boolean
        Friend VelMinRotacion As Single
        Friend VelMaxRotacion As Single
        Friend BlendMinRotacion As Single
        Friend BlendMaxRotacion As Single
    End Structure

    Friend Module Movimiento

        ''' <summary>
        ''' ⭐⭐ `TransferMotion` — `0x141A13950`.
        ''' <para>```
        ''' invDt = 1/dt                                  ' 0x141A139E1 divss EXACTO
        ''' inv   = inversaRigida(previo)                 ' 0x141A139EC → 0x141298100
        ''' M     = componer(A = inv, B = nuevo)          ' 0x141A13A02 → 0x141298180
        ''' blendT = 0 ; si transfiereTraslacion: rampa por |M.fila3·invDt|
        ''' blendR = 0 ; si transfiereRotacion:   rampa por angulo(q(M))·invDt·57,2957764
        ''' si NO (blendT &gt; 0) y NO (blendR &gt; 0): return  ' 0x141A13BCE…D8
        ''' Mb = identidad ; Mb.fila3 = blendT · M.fila3   ' 0x141A13C34/38/42
        ''' si |q.xyz|² &gt; 1,42108547e-14:                  ' 0x141A13C49 (0x142F3C770)
        '''     eje = normalizar(q.xyz) con el signo de q.w
        '''     Mb.filas0-2 = AMatriz(deEjeAngulo(eje, blendR · angulo(q)))
        ''' T = componer(componer(previo, Mb), inv)        ' 0x141A13CE7 + 0x141A13CFE
        ''' por cada particula con mass ≠ 0: pos = T(pos) ; prev = T(prev)
        ''' ```</para>
        ''' <para>⛔ La velocidad angular se compara en **GRADOS por segundo**: el
        ''' `57,2957764` de `0x142467810` (`0x141A13B50`). Los campos `min/maxRotationSpeed` del
        ''' archivo estan en grados/s, no en radianes.</para>
        ''' <para>⛔ La rampa es `t = v/den − min/den`, **dos divisiones separadas**
        ''' (`0x141A13AE6`/`EA`), no `(v − min)/den`. Y si `NO (9,99999975e-06 &lt; |den|)` el `t` es
        ''' **cero** (`0x141A13AC8`/`D9`, cte `0x142632150`), con el `|·|` sacado por
        ''' `pslld`/`psrld`.</para>
        ''' <para>⛔ `Mb.fila3` se escribe **antes** de `AMatriz`, que solo pisa tres filas: por eso
        ''' la traslacion mezclada sobrevive.</para>
        ''' <para>⛔ El filtro de particulas es `mass == 0` con `cmpeqps` de **cuatro masas
        ''' consecutivas** (`0x141A13DA6`-`B5`) y seleccion `andps`/`andnps`/`orps`: las de masa
        ''' cero se dejan **como estaban**.</para>
        ''' </summary>
        ''' <param name="previo">`simCloth+0x120`, el transform del cuadro anterior.</param>
        ''' <param name="nuevo">El transform de este cuadro.</param>
        ''' <param name="dt">`dt/s1`, el del cuadro.</param>
        Friend Sub TransferMotion(inst As Instancia, datos As DatosDeTransferencia,
                                  previo As Mat4, nuevo As Mat4, dt As Single)
            Dim invDt = Simd.Lane0(Simd.DivExacta(Vector128.Create(1.0F), Vector128.Create(dt)))  ' 0x141A139E1
            Dim inv = Mat4.InversaRigida(previo)                            ' 0x141A139EC
            Dim m = Mat4.Componer(inv, nuevo)                               ' 0x141A13A02

            ' ---- la rampa de TRASLACION
            Dim blendT = 0.0F                                               ' 0x141A13A12 xorps
            If datos.TransfiereTraslacion Then                              ' 0x141A13A07
                blendT = datos.BlendMinTraslacion                           ' 0x141A13A27
                Dim d = Vector128.Multiply(Vector128.Create(invDt), m.F3)   ' 0x141A13A34
                Dim v2 = Simd.Dot3(d, d)                                    ' 0x141A13A3B…56
                Dim v = Simd.Lane0(Vector128.Multiply(v2, Simd.RsqrtNewtonConGuarda(v2)))  ' 0x141A13A59…83
                If v >= datos.VelMaxTraslacion Then                         ' 0x141A13A86 comiss / jb
                    blendT = datos.BlendMaxTraslacion
                ElseIf v > datos.VelMinTraslacion Then                      ' 0x141A13A9E comiss / jbe
                    Dim t = Rampa(v, datos.VelMinTraslacion, datos.VelMaxTraslacion)
                    blendT = datos.BlendMinTraslacion +
                             (datos.BlendMaxTraslacion - datos.BlendMinTraslacion) * t   ' 0x141A13AEE…0x141A13B03
                End If
            End If

            ' ---- la rampa de ROTACION
            Dim q = Cuaternion.DeMatriz(Colisionables.TresDe(m))            ' 0x141A13B18
            Dim blendR = 0.0F                                               ' 0x141A13B24 xorps
            If datos.TransfiereRotacion Then                                ' 0x141A13B1D
                Dim ang = Cuaternion.Angulo(q)                              ' 0x141A13B37
                blendR = datos.BlendMinRotacion                             ' 0x141A13B44
                Dim w = (invDt * ang) * GradosPorRadian                     ' 0x141A13B4C/50
                If w >= datos.VelMaxRotacion Then                           ' 0x141A13B58 comiss / jb
                    blendR = datos.BlendMaxRotacion
                ElseIf w > datos.VelMinRotacion Then                        ' 0x141A13B6F comiss / jbe
                    ' ⛔ NO es la rampa de la traslación: acá hay UNA división, `1/den`
                    ' (`0x141A13BA9`, sobre el 1,0 de xmm10 cargado en `0x141A139C9`), y el orden es
                    ' `((bMax − bMin)·(w − min))·inv + bMin` (`0x141A13BB6`…`0x141A13BC7`).
                    Dim den = datos.VelMaxRotacion - datos.VelMinRotacion    ' 0x141A13B7B
                    Dim invDen = If(DenominadorDespreciable(den), 0.0F,
                                 Simd.Lane0(Simd.DivExacta(Vector128.Create(1.0F), Vector128.Create(den))))
                    blendR = ((datos.BlendMaxRotacion - datos.BlendMinRotacion) *
                              (w - datos.VelMinRotacion)) * invDen + datos.BlendMinRotacion
                End If
            End If

            ' ---- ⛔ con los dos blends en cero NO HACE NADA
            If Not (blendT > 0.0F) AndAlso Not (blendR > 0.0F) Then Return  ' 0x141A13BCE…D8

            ' ---- Mb: la traslacion mezclada y, si hay giro, la rotacion mezclada
            Dim mb = Mat4.Identidad                                         ' 0x142F3C700/710/720
            mb.F3 = Vector128.Multiply(Vector128.Create(blendT), m.F3)      ' 0x141A13C34/38/42
            Dim n2 = Simd.Lane0(Simd.Dot3(q, q))                            ' 0x141A13C1F…3F
            If n2 > EpsilonDelEjeTransferido Then                           ' 0x141A13C49 (0x142F3C770)
                Dim eje = Vector128.Multiply(q, Simd.RsqrtNewtonConGuarda(Simd.Dot3(q, q)))  ' 0x141A13C56…9C
                Dim menor = Vector128.LessThan(Simd.BcastW(q), Vector128(Of Single).Zero)     ' 0x141A13C73/77
                Dim signo = Vector128.ShiftLeft(
                    Vector128.ShiftRightLogical(menor.AsUInt32(), 31), 31)                    ' 0x141A13C8C/94
                eje = Vector128.Xor(eje.AsUInt32(), signo).AsSingle()                         ' 0x141A13CA2
                Dim ang = Cuaternion.Angulo(q)                                                ' 0x141A13CAA
                Dim r = Cuaternion.AMatriz(Cuaternion.DeEjeAngulo(eje, blendR * ang))          ' 0x141A13CB9/C0/D1
                mb.F0 = r.F0
                mb.F1 = r.F1
                mb.F2 = r.F2                                                ' ⬅ y la F3 NO se toca
            End If

            ' ---- T = previo ∘ Mb ∘ previo⁻¹
            Dim t1 = Mat4.Componer(previo, mb)                              ' 0x141A13CE7
            Dim tt = Mat4.Componer(t1, inv)                                 ' 0x141A13CFE

            ' ---- y las particulas con masa
            For i = 0 To inst.NumParticulas - 1
                If inst.Masa(i) = 0.0F Then Continue For                    ' 0x141A13DA6 cmpeqps
                Simd.Escribir(inst.Posiciones, i, AplicarTransform(inst.Pos(i), tt))   ' 0x141A13DBA
                Simd.Escribir(inst.Previas, i, AplicarTransform(inst.Prev(i), tt))
            Next
        End Sub

        ''' <summary>
        ''' La rampa comun de las dos velocidades — `0x141A13AA3`-`0x141A13AF6`.
        ''' <para>`t = v/den − min/den` con `den = max − min`; si `NO (9,99999975e-06 &lt; |den|)`
        ''' el resultado es **cero**.</para>
        ''' <para>⛔ Son **dos divisiones separadas**, no `(v − min)/den`: en `float` no es lo
        ''' mismo. Y el `|den|` sale de borrar el bit de signo (`pslld`/`psrld`,
        ''' `0x141A13ACF`/`D4`), no de `Abs`.</para>
        ''' </summary>
        Friend Function Rampa(v As Single, minimo As Single, maximo As Single) As Single
            Dim den = maximo - minimo                                       ' 0x141A13AA3 subss
            If DenominadorDespreciable(den) Then Return 0.0F
            Return Simd.Lane0(Simd.DivExacta(Vector128.Create(v), Vector128.Create(den))) -
                   Simd.Lane0(Simd.DivExacta(Vector128.Create(minimo), Vector128.Create(den)))  ' 0x141A13AE6/EA/F6
        End Function

        ''' <summary>
        ''' La guarda de las dos rampas: `|den − 0|` con el bit de signo borrado (`pslld`/`psrld`,
        ''' `0x141A13ACF`/`D4` y `0x141A13B94`/`99`), y `ucomiss eps, |den|` / `jb` a dividir
        ''' (`0x141A13AD9`/`DC`, `0x141A13B9E`/`BA1`). ⛔ `jb` salta también con unordered: con un
        ''' `den` NaN el motor DIVIDE. Sólo es despreciable `eps >= |den|` ordenado.
        ''' </summary>
        Private Function DenominadorDespreciable(den As Single) As Boolean
            Dim absDen = Vector128.ShiftRightLogical(
                Vector128.ShiftLeft(Vector128.Create(den - 0.0F).AsInt32(), 1), 1).AsSingle()
            Return EpsilonDelDenominador >= Simd.Lane0(absDen)
        End Function

        ''' <summary>`((v.y·M1 + v.x·M0) + v.z·M2) + M3` — `0x141339F90`, el punto por el
        ''' transform con la traslacion AL FINAL.</summary>
        Friend Function AplicarTransform(v As Vector128(Of Single), m As Mat4) As Vector128(Of Single)
            Dim r = Vector128.Add(Vector128.Multiply(Simd.BcastY(v), m.F1),
                                  Vector128.Multiply(Simd.BcastX(v), m.F0))   ' 0x141339F9A/9E + 0x141339FB1
            r = Vector128.Add(r, Vector128.Multiply(Simd.BcastZ(v), m.F2))    ' 0x141339FA9/AD + 0x141339FB4
            Return Vector128.Add(r, m.F3)                                     ' 0x141339FB7
        End Function

        ''' <summary>`57,2957764` — `0x42652EE0` en `0x142467810`. ⛔ Convierte a **grados por
        ''' segundo**: los `min/maxRotationSpeed` del archivo estan en grados.</summary>
        Friend Const GradosPorRadian As Single = 57.2957764F

        ''' <summary>`9,99999975e-06` — `0x3727C5AC` en `0x142632150`. El piso del denominador de
        ''' la rampa.</summary>
        Friend Const EpsilonDelDenominador As Single = 9.99999975E-06F

        ''' <summary>`1,42108547e-14` — `0x28800000` en `0x142F3C770`. El piso de `|q.xyz|²` bajo el
        ''' cual no hay giro que transferir. ⛔ **No** es el mismo que el de `setTransform`
        ''' (`1,1920929e-07`): son dos numeros distintos en dos sitios distintos.</summary>
        Friend Const EpsilonDelEjeTransferido As Single = 1.42108547E-14F

    End Module

End Namespace

