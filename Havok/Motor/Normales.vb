Option Strict On
Option Explicit On

Imports System.Runtime.Intrinsics

' =================================================================================================
' LAS NORMALES DE SIMULACION — `UpdateSimNormals` `0x14195CEB0`, bloque `"TtUpdate Sim Normals"`
' (`0x14270BC78`). 701 instrucciones, con el cuerpo desenrollado de a 8 triangulos.
'
' Corre al final del cuadro, y SOLO si `data.doNormals` (`+0x14C`).
'
' ⛔⛔ La normal de cada triangulo entra al acumulador **SIN normalizar**: pesa por el doble del
' area. Normalizarla antes daria un promedio distinto y una tela iluminada distinta.
' =================================================================================================

#If DEBUG Then

Namespace Havok.Motor

    Friend Module Normales

        ''' <summary>
        ''' ⭐⭐ `UpdateSimNormals` — `0x14195CEB0`.
        ''' <para>```
        ''' nTri = triangleIndices.count(+0x60) / 3        ' 0x14195CF27 imul 0x55555556 + sar 3
        ''' por cada particula: normales[i] = 0            ' 0x14195CF60…6E
        ''' por cada triangulo t:
        '''     i0 = idx[3t]   i1 = idx[3t+1]   i2 = idx[3t+2]     ' 0x14195D710/19/21, uint16
        '''     e1 = pos[i2] − pos[i0]                     ' 0x14195D735/3F
        '''     e2 = pos[i1] − pos[i0]                     ' 0x14195D744/4B
        '''     cl  = 7 − (t AND 7)                        ' 0x14195D714/2C/2F
        '''     bit = (flips[t &gt;&gt; 3] &gt;&gt; cl) AND 1         ' 0x14195D750/52/55
        '''     esc = 1,0 − bit·4,0                        ' 0x14195D77A/85/8C
        '''     n   = (e2 × e1) · esc                      ' 0x14195D75F…8F, cruz ROTADO
        '''     normales[i0] += n ; normales[i1] += n ; normales[i2] += n   ' 0x14195D798…B4
        ''' por cada particula:
        '''     n2 = (n.y² + n.x²) + n.z²
        '''     normales[i] = n · ((n2 &lt;= 0) ? 0 : rsqrtps(n2))    ' 0x14195D821/27/2B/2E — CRUDO
        ''' ```</para>
        ''' <para>⛔⛔ **`esc` es `1 − 4·bit`, o sea `{+1, −3}`** — NO `±1`. Las constantes son
        ''' `1,0` (`0x142F3C560`) y **`4,0`** (`0x142483EE0`), y estan asi en los DOS caminos: el
        ''' desenrollado de 8 y la cola. Sea intencional o un `2` que quedo en `4`, es lo que el
        ''' motor hace y es lo que se transcribe: un triangulo dado vuelta no cancela a uno
        ''' derecho, lo domina.</para>
        ''' <para>⛔ El bit del flip se lee **de arriba hacia abajo** dentro del byte: `cl` arranca
        ''' en 7 y baja (`0x14195D714`, y el `shr al, 7` / `shr al, 6` del desenrollado). No es
        ''' `t AND 7`.</para>
        ''' <para>⛔ La normalizacion final es `rsqrtps` **CRUDO** con guarda `cmpleps` contra 0
        ''' (`0x14195D821`/`27`/`2B`): sin Newton.</para>
        ''' <para>⛔ Y limpia **todas** las normales antes, incluidas las de particulas que no
        ''' pertenecen a ningun triangulo: esas quedan en cero, no en su valor anterior.</para>
        ''' </summary>
        Friend Sub ActualizarNormales(inst As Instancia, indices As Integer(), flips As Byte())
            If inst.Normales Is Nothing Then Return                          ' data.doNormals (+0x14C)

            For i = 0 To inst.NumParticulas - 1                              ' 0x14195CF60…6E
                Simd.Escribir(inst.Normales, i, Vector128(Of Single).Zero)
            Next

            Dim nTri = If(indices Is Nothing, 0, indices.Length \ 3)          ' 0x14195CF27/35/38
            For t = 0 To nTri - 1
                Dim i0 = indices(t * 3)                                      ' 0x14195D719 [r11-4]
                Dim i1 = indices(t * 3 + 1)                                  ' 0x14195D721 [r11-2]
                Dim i2 = indices(t * 3 + 2)                                  ' 0x14195D710 [r11]
                Dim p0 = inst.Pos(i0)
                Dim e1 = Vector128.Subtract(inst.Pos(i2), p0)                ' 0x14195D735/3F
                Dim e2 = Vector128.Subtract(inst.Pos(i1), p0)                ' 0x14195D744/4B

                ' ⛔ el bit, de ARRIBA hacia abajo dentro del byte
                Dim cl = 7 - (t And 7)                                       ' 0x14195D714/2C/2F
                Dim bit = If(flips Is Nothing, 0,
                             (CInt(flips(t >> 3)) >> cl) And 1)              ' 0x14195D750/52/55
                Dim esc = 1.0F - CSng(bit) * FactorDelFlip                   ' 0x14195D77A/85/8C

                ' el cruz ROTADO, la misma forma que `Cuaternion.Producto`
                Dim cr = Vector128.Subtract(
                    Vector128.Multiply(Vector128.Shuffle(e1, Vector128.Create(1, 2, 0, 3)), e2),
                    Vector128.Multiply(Vector128.Shuffle(e2, Vector128.Create(1, 2, 0, 3)), e1))
                cr = Vector128.Shuffle(cr, Vector128.Create(1, 2, 0, 3))     ' 0x14195D788
                Dim n = Vector128.Multiply(cr, Vector128.Create(esc))        ' 0x14195D78F

                Simd.Escribir(inst.Normales, i0, Vector128.Add(n, Simd.Leer(inst.Normales, i0)))
                Simd.Escribir(inst.Normales, i1, Vector128.Add(n, Simd.Leer(inst.Normales, i1)))
                Simd.Escribir(inst.Normales, i2, Vector128.Add(Simd.Leer(inst.Normales, i2), n))
            Next                                                             ' 0x14195D798…B4

            For i = 0 To inst.NumParticulas - 1
                Dim n = Simd.Leer(inst.Normales, i)
                Simd.Escribir(inst.Normales, i,
                              Vector128.Multiply(n, Simd.RsqrtConGuarda(Simd.Dot3(n, n))))  ' 0x14195D821…31
            Next
        End Sub

        ''' <summary>`4,0` — `0x40800000` en `0x142483EE0`. ⛔ Es un **4**, no un 2: el escalar del
        ''' flip es `1 − 4·bit` ∈ `{+1, −3}`. Medido en los dos caminos del kernel.</summary>
        Friend Const FactorDelFlip As Single = 4.0F

    End Module

End Namespace

#End If
