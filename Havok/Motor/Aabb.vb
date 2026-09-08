Option Strict On
Option Explicit On

Imports System.Runtime.Intrinsics

' =================================================================================================
' LOS AABB — el paso 5 del cuadro, con sus DOS variantes.
'
' `Simulate` elige por `simCloth[+0x1C8]`: si es 0 corre `UpdateParticlesAABB` (`0x1418C7300`,
' bloque `"TtUpdate Particles AABB"`), si no `UpdateAABBs` (`0x1418C73C0`, `"TtUpdate AABBs"`).
'
' ⛔ No son la misma cuenta con mas o menos detalle: la primera saca UN aabb de las posiciones
' crudas; la segunda saca CUATRO —el de posiciones, el «ancho» y el de las particulas con mascara
' negativa— y los tres ultimos van EXTRAPOLADOS en el tiempo y con el margen de
' `collisionTolerance`.
' =================================================================================================

#If DEBUG Then

Namespace Havok.Motor

    Friend Module Aabb

        ''' <summary>
        ''' `UpdateParticlesAABB` — `0x1418C7300`, **49 instrucciones**.
        ''' <para>El AABB de las posiciones crudas, sin margen y sin extrapolar:
        ''' `min = minps(...)`, `max = maxps(...)` sobre `positions`, a `inst[+0x50]`/`[+0x60]`.</para>
        ''' <para>⛔ Recorre **las cuatro lanes**, la `w` incluida (`0x1418C7370`/`73` son `minps`
        ''' y `maxps` de 128 bits sobre el `movups` entero).</para>
        ''' <para>⛔ Los extremos arrancan en `±3,40282002e+38` (`0x142F3C740`), que **no** es
        ''' `FLT_MAX` (`0x7F7FFFFF`): es `0x7F7FFFEE`. Y el maximo sale de **negar** ese valor con un
        ''' `xorps` de `0x80000000` difundido por `pinsrw`+`pshufd` (`0x1418C7353`/`5C`/`61`).</para>
        ''' </summary>
        Friend Sub ActualizarAabbDeParticulas(inst As Instancia)
            Dim mn = Vector128.Create(Simd.CasiFltMax)             ' 0x1418C7349
            Dim mx = Vector128.Create(-Simd.CasiFltMax)            ' 0x1418C7353/5C/61 xorps
            For i = 0 To inst.NumParticulas - 1                              ' 0x1418C7341 [rbx+0x20]
                Dim p = inst.Pos(i)
                mn = Vector128.Min(mn, p)                                    ' 0x1418C7370
                mx = Vector128.Max(mx, p)                                    ' 0x1418C7373
            Next
            inst.AabbMinParticulas = mn                                      ' 0x1418C7380 [rbx+0x50]
            inst.AabbMaxParticulas = mx                                      ' 0x1418C7384 [rbx+0x60]
        End Sub

        ''' <summary>
        ''' ⭐⭐ `UpdateAABBs` — `0x1418C73C0`, **118 instrucciones**. Saca **tres** AABB de una
        ''' pasada.
        ''' <para>```
        ''' por cada particula i:
        '''     p = positions[i]
        '''     aabbP = min/max con p                          ' 0x1418C74A8/AC — SIN extrapolar
        '''     si mascaras[i] == 0 Y invMass[i] == 0: saltea   ' 0x1418C74B0/B3/B5/B8
        '''     q = previous[i] ; d = p − q
        '''     a = d·1,5 + q                                   ' 0x1426B4E10 = 1,5
        '''     b = d·0,5 + q                                   ' 0x142486120 = 0,5
        '''     aabbAncho = min/max con b y despues con a       ' 0x1418C74CF…DB
        '''     si mascaras[i] &lt; 0:                             ' 0x1418C74DF/E2 test / jns
        '''         aabbMascara = min/max con b y con a         ' 0x1418C74E4…ED
        ''' r = getSimulationInfo(inst).collisionTolerance      ' 0x1418C7505 → 0x1418C7730, +0x14
        ''' inst[+0x50] = aabbPmin        ; inst[+0x60] = aabbPmax          ' SIN margen
        ''' inst[+0x70] = aabbAnchoMin−r  ; inst[+0x80] = aabbAnchoMax+r
        ''' inst[+0x90] = aabbMascMin−r   ; inst[+0xA0] = aabbMascMax+r
        ''' ```</para>
        ''' <para>⛔⛔ El `1,5` y el `0,5` **extrapolan el movimiento**: `q + 1,5·d` es medio paso
        ''' mas alla de `p` y `q + 0,5·d` es el punto medio. El AABB cubre de medio paso atras a
        ''' medio paso adelante — no es el de las posiciones actuales.</para>
        ''' <para>⛔ El filtro es `mascara == 0 **Y** invMass == 0` (las dos, `0x1418C74B0` y
        ''' `0x1418C74B5`): con cualquiera de las dos distinta de cero la particula entra.</para>
        ''' <para>⛔ El margen es `collisionTolerance` (`simulationInfo+0x14`), y **solo** va en los
        ''' dos ultimos: el de posiciones queda crudo.</para>
        ''' </summary>
        Friend Sub ActualizarAabbs(inst As Instancia)
            Dim mnP = Vector128.Create(Simd.CasiFltMax)
            Dim mxP = Vector128.Create(-Simd.CasiFltMax)
            Dim mnA = mnP, mxA = mxP
            Dim mnM = mnP, mxM = mxP

            For i = 0 To inst.NumParticulas - 1
                Dim p = inst.Pos(i)
                mnP = Vector128.Min(mnP, p)                                  ' 0x1418C74A8
                mxP = Vector128.Max(mxP, p)                                  ' 0x1418C74AC

                ' ⛔ El motor lee la mascara como `int32` (`mov r9d, [rdx]`) y despues la mira
                ' con `test`/`jns`, o sea `== 0` y `bit 31`. Se hace igual SIN convertir: un
                ' `CInt` de 0x80000000 desborda en VB, y el motor no convierte, reinterpreta.
                Dim masc = If(inst.MascarasDeColision Is Nothing, 0UI, inst.MascarasDeColision(i))
                Dim mascCero = (masc = 0UI)                                  ' 0x1418C74B0 test
                Dim mascNegativa = ((masc And &H80000000UI) <> 0UI)          ' 0x1418C74E2 jns
                If mascCero AndAlso inst.InvMasa(i) = 0.0F Then Continue For ' 0x1418C74B0…B8

                Dim q = inst.Prev(i)
                Dim d = Vector128.Subtract(p, q)                             ' 0x1418C74BD
                Dim a = Vector128.Add(Vector128.Multiply(d, Vector128.Create(1.5F)), q)   ' 0x1418C74C3/C9
                Dim b = Vector128.Add(Vector128.Multiply(d, Vector128.Create(0.5F)), q)   ' 0x1418C74C6/CC
                mnA = Vector128.Min(mnA, b) : mxA = Vector128.Max(mxA, b)    ' 0x1418C74CF/D3
                mnA = Vector128.Min(mnA, a) : mxA = Vector128.Max(mxA, a)    ' 0x1418C74D7/DB

                If mascNegativa Then                                             ' 0x1418C74DF/E2 jns
                    mnM = Vector128.Min(mnM, b) : mxM = Vector128.Max(mxM, b)
                    mnM = Vector128.Min(mnM, a) : mxM = Vector128.Max(mxM, a)
                End If
            Next

            Dim r = Vector128.Create(inst.ToleranciaDeColision)              ' 0x1418C750A [rax+0x14]
            inst.AabbMinParticulas = mnP                                     ' 0x1418C750F
            inst.AabbMaxParticulas = mxP                                     ' 0x1418C7514
            inst.AabbMinAncho = Vector128.Subtract(mnA, r)                   ' 0x1418C7527/41
            inst.AabbMaxAncho = Vector128.Add(mxA, r)                        ' 0x1418C7523/39
            inst.AabbMinMascara = Vector128.Subtract(mnM, r)                 ' 0x1418C7520/32
            inst.AabbMaxMascara = Vector128.Add(mxM, r)                      ' 0x1418C751D/2B
        End Sub

    End Module

End Namespace

#End If
