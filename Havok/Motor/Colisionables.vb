Option Strict On
Option Explicit On

Imports System.Runtime.Intrinsics

' =================================================================================================
' LOS COLISIONABLES EN EL TIEMPO — de donde salen sus velocidades y como CAMINAN dentro del cuadro.
'
' Ley: `hclCollidable::setTransform` `0x1419605F0` (RE 6.9), desensamblada entera.
'
' ⛔⛔ LA CONSECUENCIA ESTRUCTURAL: `setTransform` **no pisa** el transform con la pose nueva. Lo
' deja en la VIEJA y guarda la diferencia como velocidad. El colisionable arranca el cuadro donde
' estaba y camina hasta la pose nueva a lo largo de los substeps. Si uno le pusiera la pose nueva
' de entrada, la tela colisionaria contra el destino en vez de contra el recorrido, y atravesaria
' todo lo que el hueso barrio en el medio.
'
' ⛔ Por eso `hclCollidable.linearVelocity` (+0x60) y `angularVelocity` (+0x70) **serializados en
' el archivo son basura**: son campos de runtime que esta funcion deriva de (viejo, nuevo, dt).
' =================================================================================================

Namespace Havok.Motor

    ''' <summary>
    ''' `hclSimClothDataCollidableTransformMap` — `hclSimClothData+0x80`, `0x28` B.
    ''' <para>Campos de la reflexion, la lista es cerrada: `transformSetIndex` (`+0x00`,
    ''' **`int32` CON SIGNO**), `transformIndices` (`+0x08`, array de `uint32`) y `offsets`
    ''' (`+0x18`, array de `matrix4`).</para>
    ''' <para>⛔ Que el indice sea con signo no es un detalle: **negativo** es lo que apaga los
    ''' pasos 2 y 5b enteros (`0x14195C4B0` y `0x14195CD28`, los dos con `jl`).</para>
    ''' </summary>
    Friend Structure MapaDeColisionables
        ''' <summary>`transformSetIndex` (+0x00). Negativo = no hay mapa.</summary>
        Friend IndiceDelSet As Integer
        ''' <summary>`transformIndices` (+0x08): que transform del set le toca a cada colisionable.</summary>
        Friend Indices As Integer()
        ''' <summary>`offsets` (+0x18): el offset local de cada colisionable respecto de su hueso.</summary>
        Friend Offsets As Mat4()
    End Structure

    Friend Module Colisionables

        ''' <summary>
        ''' ⭐⭐ `hclCollidable::setTransform` — `0x1419605F0`. De `(pose vieja, pose nueva, dt)`
        ''' saca las **dos velocidades** del colisionable y deja el transform en la pose VIEJA.
        ''' <para>```
        ''' coll.transform  = Mviejo                             ' 0x1419605FF…0x141960621
        ''' coll.linVel     = Mnuevo.fila3 − Mviejo.fila3        ' 0x141960625/29/3E
        ''' qN = normalizar4(DeMatriz(Mnuevo))                   ' 0x141960646 + …65E…698
        ''' qV = normalizar4(DeMatriz(Mviejo))                   ' 0x141960654 + …69B…6DC
        ''' qd = qN ⊗ conj(qV)                                   ' 0x1419606D2…0x141960734
        ''' ang = Angulo(qd)                                     ' 0x141960742
        ''' si NO (ang &gt; 0):        coll.angVel = 0             ' 0x14196074E comiss / jbe
        ''' n2 = |qd.xyz|²                                       ' 0x14196075D…78  (orden (y+x)+z)
        ''' si NO (n2 &gt; 1,1920929e-07): coll.angVel = 0         ' 0x14196077B ucomiss / jbe
        ''' si no: eje = qd.xyz · rsqrtNewtonConGuarda(n2)       ' 0x14196078F…0x1419607D2
        '''        eje = eje XOR signo(qd.w &lt; 0)                ' 0x14196079F/A3/BF/C4/DA
        '''        coll.angVel = eje · ((1/dt) · ang)            ' 0x1419607D5/E0/E4/E7
        ''' coll.linVel = coll.linVel · (1/dt)                   ' 0x14196081E/22/26
        ''' ```</para>
        ''' <para>⛔ `1/dt` sale de un `divss` (`0x1419607B8` y `0x1419607FC`, en **las dos**
        ''' ramas): division EXACTA, nada de `rcpps`. Y se aplica **al final**, sobre el delta
        ''' crudo — no es `(nuevo − viejo)/dt` calculado componente a componente antes.</para>
        ''' <para>⛔ El signo sale de `qd.w &lt; 0` y se aplica al **eje ya normalizado** con un
        ''' `xorps` del bit 31 (`cmpltps` → `psrld 31` → `pslld 31`), no al angulo ni al
        ''' resultado. Es lo que elige el giro corto: un cuaternion y su negado son la misma
        ''' rotacion, pero `Angulo` devuelve `2·acos(|w|)` y el eje hay que darlo vuelta.</para>
        ''' <para>⛔ `rsqrtps` + UNA Newton **con guarda** `cmpleps`/`andnps` (`0x141960795` y
        ''' `0x1419607CC`): con `n2 &lt;= 0` el eje queda en cero. Pero ese caso ya lo ataja el
        ''' `ucomiss` de arriba — la guarda es la del motor igual, y va.</para>
        ''' <para>⛔ Las dos comparaciones son **estrictas y en ese sentido**: `jbe` salta con NaN,
        ''' asi que un `ang` o un `n2` NaN dejan la velocidad angular en cero en vez de
        ''' propagarlo.</para>
        ''' </summary>
        ''' <param name="coll">El colisionable del buffer de trabajo (`rcx`).</param>
        ''' <param name="nueva">La pose de este cuadro (`rdx`), la que el juego acaba de calcular.</param>
        ''' <param name="vieja">La pose con la que arranca el cuadro (`r8`).</param>
        ''' <param name="dt">El paso del cuadro entero, **no** el del substep (`xmm3`).</param>
        Friend Sub SetTransform(coll As Colisionable, nueva As Mat4, vieja As Mat4, dt As Single)
            ' (1) el transform queda en la pose VIEJA — 0x1419605FF…0x141960621
            coll.Transform = vieja

            ' (2) el delta CRUDO de traslacion; recien al final se divide por dt — 0x141960625/29
            coll.VelLineal = Vector128.Subtract(nueva.F3, vieja.F3)          ' 0x141960629 subps

            ' (3) los dos cuaterniones, normalizados en las CUATRO componentes con rsqrt+Newton
            Dim qN = Cuaternion.NormalizarSinGuarda(Cuaternion.DeMatriz(TresDe(nueva)))   ' 0x141960646
            Dim qV = Cuaternion.NormalizarSinGuarda(Cuaternion.DeMatriz(TresDe(vieja)))   ' 0x141960654

            ' (4) el giro que lleva de la vieja a la nueva
            Dim qd = Cuaternion.ProductoPorConjugado(qN, qV)                 ' 0x1419606D2…734
            Dim ang = Cuaternion.Angulo(qd)                                  ' 0x141960742

            Dim invDt = Simd.Lane0(Simd.DivExacta(Vector128.Create(1.0F), Vector128.Create(dt)))

            If Not (ang > 0.0F) Then                                          ' 0x14196074E comiss / jbe
                coll.VelAngular = Vector128(Of Single).Zero                   ' 0x1419607F8
            Else
                Dim n2 = Simd.Dot3(qd, qd)                                    ' 0x14196075D…78
                If Not (Simd.Lane0(n2) > EpsilonDelEje) Then                  ' 0x14196077B ucomiss / jbe
                    coll.VelAngular = Vector128(Of Single).Zero
                Else
                    Dim eje = Vector128.Multiply(qd, Simd.RsqrtNewtonConGuarda(n2))   ' 0x14196078F…D2
                    ' el signo de qd.w, como BIT 31, sobre el eje ya normalizado
                    Dim menor = Vector128.LessThan(Simd.BcastW(qd), Vector128(Of Single).Zero)   ' 0x1419607A3
                    Dim signo = Vector128.ShiftLeft(
                        Vector128.ShiftRightLogical(menor.AsUInt32(), 31), 31)         ' 0x1419607BF/C4
                    eje = Vector128.Xor(eje.AsUInt32(), signo).AsSingle()              ' 0x1419607DA
                    coll.VelAngular = Vector128.Multiply(eje, Vector128.Create(invDt * ang))
                End If                                                        ' 0x1419607D5/E0/E4/E7
            End If

            ' (5) y recien ahora la lineal pasa a ser velocidad — 0x14196081E/22/26
            coll.VelLineal = Vector128.Multiply(coll.VelLineal, Vector128.Create(invDt))
        End Sub


        ' -----------------------------------------------------------------------------------------
        ' (2) DRIVE COLLIDABLES — 0x14195C4B0
        ' -----------------------------------------------------------------------------------------

        ''' <summary>
        ''' ⭐⭐ El paso 2 del cuadro: compone el offset de cada colisionable con el transform del
        ''' hueso y le pasa el resultado a <see cref="SetTransform"/> — `0x14195C4B0`.
        ''' <para>`M = A × B`, con `A = mapa.Offsets(i)` (`hclSimClothDataCollidableTransformMap.
        ''' offsets`, `matrix4`) y `B = transformSet(mapa.Indices(i))`.</para>
        ''' <para>⛔⛔ **Las filas 0-2 y la fila 3 no usan el mismo orden de sumas**, y en `float`
        ''' eso son bits distintos:
        ''' <list type="bullet">
        ''' <item>filas 0-2: `(x·B0 + y·B1) + z·B2` — `0x14195C59E`…`0x14195C63F`, **sin** `B3`.</item>
        ''' <item>fila 3: `((x·B0 + B3) + y·B1) + z·B2` — `0x14195C5AD`…`0x14195C5DA`, con la
        ''' traslacion sumada **en el medio**, no al final (`0x14195C5BE addps xmm7, [rcx+0x30]`
        ''' va ANTES del `addps xmm7, xmm0` de la componente `y`).</item>
        ''' </list></para>
        ''' <para>⛔ El gate `0x14195C4B0 cmp / jl` es sobre `transformSetIndex`, que la reflexion
        ''' declara **`int32` con signo** (`hclSimClothDataCollidableTransformMap`): negativo
        ''' significa «no hay mapa» y el paso entero no corre.</para>
        ''' <para>⛔ `dt` es el del CUADRO (`dt/s1`), no el del substep: los colisionables se
        ''' manejan una vez por cuadro y despues caminan.</para>
        ''' </summary>
        Friend Sub DriveCollidables(colls As Colisionable(), mapa As MapaDeColisionables,
                                    transformSet As Mat4(), dt As Single)
            If mapa.IndiceDelSet < 0 Then Return                     ' 0x14195C4B0 cmp / jl
            If colls Is Nothing Then Return
            For i = 0 To colls.Length - 1                            ' 0x14195C526 contra [rsi+0xD8]
                Dim b = transformSet(mapa.Indices(i))                ' 0x14195C560/6B/6F — idx << 6
                Dim a = mapa.Offsets(i)                              ' 0x14195C579/94/99 — rbx += 0x40
                Dim m As Mat4
                m.F0 = FilaPorMat(a.F0, b)                           ' 0x14195C59E…0x14195C605
                m.F1 = FilaPorMat(a.F1, b)                           ' 0x14195C5E1…0x14195C637
                m.F2 = FilaPorMat(a.F2, b)                           ' 0x14195C60A…0x14195C642
                m.F3 = FilaPorMatConTraslacion(a.F3, b)              ' 0x14195C5AD…0x14195C5EB
                SetTransform(colls(i), m, colls(i).Transform, dt)    ' 0x14195C64A
            Next
        End Sub

        ''' <summary>`(x·B0 + y·B1) + z·B2` — las filas 0-2, **sin** traslacion
        ''' (`0x14195C5E1`-`0x14195C625`: `mulps` de `x`, de `y`, `addps`, y recien despues el
        ''' termino de `z`).</summary>
        Friend Function FilaPorMat(v As Vector128(Of Single), b As Mat4) As Vector128(Of Single)
            Dim r = Vector128.Add(Vector128.Multiply(Simd.BcastX(v), b.F0),
                                  Vector128.Multiply(Simd.BcastY(v), b.F1))
            Return Vector128.Add(r, Vector128.Multiply(Simd.BcastZ(v), b.F2))
        End Function

        ''' <summary>
        ''' `((x·B0 + B3) + y·B1) + z·B2` — la fila 3, con `B3` sumado **en el medio**.
        ''' <para>⛔ No es `FilaPorMat(v, b) + b.F3`: el motor suma `B3` justo despues del termino
        ''' de `x` (`0x14195C5BE addps xmm7, [rcx+0x30]`) y antes del de `y`
        ''' (`0x14195C5C5 addps xmm7, xmm0`). Reasociado da otros bits, y esta traslacion es la
        ''' posicion del colisionable, que despues entra en cada contacto.</para>
        ''' </summary>
        Friend Function FilaPorMatConTraslacion(v As Vector128(Of Single), b As Mat4) As Vector128(Of Single)
            Dim r = Vector128.Multiply(Simd.BcastX(v), b.F0)               ' 0x14195C5AD/B2
            r = Vector128.Add(r, b.F3)                                     ' 0x14195C5BE ⬅ EN EL MEDIO
            r = Vector128.Add(r, Vector128.Multiply(Simd.BcastY(v), b.F1)) ' 0x14195C5A2/A7 + 0x14195C5C5
            Return Vector128.Add(Vector128.Multiply(Simd.BcastZ(v), b.F2), r)  ' 0x14195C5B5/BA + 0x14195C5DA
        End Function

        ' -----------------------------------------------------------------------------------------
        ' (4a) SUBSTEP COLLIDABLES — 0x14195C9B0
        ' -----------------------------------------------------------------------------------------

        ''' <summary>
        ''' ⭐⭐ El colisionable CAMINA un substep — `0x14195C9B0`, bloque `"TtSubstep Collidables"`
        ''' (`0x14270BC60`).
        ''' <para>```
        ''' buf.pos(+0x50) += buf.linVel(+0x60) · dtSub      ' 0x14195C9BC/C6/CE/D4
        ''' q  = DeMatriz(buf.transform+0x20)                ' 0x14195C9DA
        ''' q' = normalizar4(expMap(angVel·dtSub/2) ⊗ q)     ' 0x14195C9DF…0x14195CB06
        ''' buf.transform(+0x20..+0x40) = AMatriz(q')        ' 0x14195CB0E
        ''' ```</para>
        ''' <para>⛔ `AMatriz` (`0x141365A80`) escribe **tres** filas (`[rcx]`, `[rcx+0x10]`,
        ''' `[rcx+0x20]`): la traslacion que acaba de moverse **no se pisa**.</para>
        ''' <para>⛔ `buf.pos` **es** `transform.fila3` (`+0x50`): la posicion no vive aparte.</para>
        ''' <para>⛔ Esto corre para TODOS los colisionables del buffer, tenga o no `linVel` — no
        ''' hay filtro por «se movio». Con las dos velocidades en cero es una identidad cara, y asi
        ''' es el motor.</para>
        ''' </summary>
        ''' <param name="dtSub">`(1/N)·(dt/s1)`, ya difundido — `xmm8`.</param>
        ''' <param name="medioDtSub">`dtSub·0,5`, ya difundido — `xmm9`.</param>
        Friend Sub SubstepColisionables(colls As Colisionable(), dtSub As Vector128(Of Single),
                                        medioDtSub As Vector128(Of Single))
            If colls Is Nothing Then Return
            For Each c In colls
                ' la traslacion, que es la fila 3 del propio transform
                c.Transform.F3 = Vector128.Add(Vector128.Multiply(c.VelLineal, dtSub),
                                               c.Transform.F3)          ' 0x14195C9C6/CE
                Dim q = Cuaternion.DeMatriz(TresDe(c.Transform))        ' 0x14195C9DA
                q = Cuaternion.MapaExponencial(q, c.VelAngular, medioDtSub)
                Dim r = Cuaternion.AMatriz(q)                           ' 0x14195CB0E
                c.Transform.F0 = r.F0
                c.Transform.F1 = r.F1
                c.Transform.F2 = r.F2                                   ' ⬅ y F3 NO se toca
            Next
        End Sub

        ' -----------------------------------------------------------------------------------------
        ' (5b) WRITE-BACK — 0x14195CD28
        ' -----------------------------------------------------------------------------------------

        ''' <summary>
        ''' Devuelve el transform del buffer de trabajo a cada `hclCollidable` de verdad —
        ''' `0x14195CD28`-`0x14195CDA0`, cuatro `movups` de `+0x20`, `+0x30`, `+0x40` y `+0x50`.
        ''' <para>⛔ Tiene **la misma puerta** que `DriveCollidables`: `cmp dword [rbp+0x80], 0` /
        ''' `jl` (`0x14195CD28`). Sin mapa de transforms no se maneja **ni se devuelve** nada, y
        ''' el colisionable se queda con la pose del archivo.</para>
        ''' <para>⛔ Copia **solo el transform**, no las velocidades: `linVel`/`angVel` mueren con
        ''' el buffer y se vuelven a derivar el cuadro que viene.</para>
        ''' <para>⭐ Y por esto el paso 2 puede leer `coll.transform` como «la pose vieja»: lo que
        ''' hay ahi es donde termino el cuadro anterior.</para>
        ''' </summary>
        Friend Sub EscribirTransforms(buffer As Colisionable(), reales As Colisionable(),
                                      indiceDelSet As Integer)
            If indiceDelSet < 0 Then Return                          ' 0x14195CD28 cmp / jl
            If buffer Is Nothing OrElse reales Is Nothing Then Return
            For i = 0 To Math.Min(buffer.Length, reales.Length) - 1  ' 0x14195CD3C contra [rsi+0xD8]
                reales(i).Transform = buffer(i).Transform            ' 0x14195CD6C…0x14195CD95
            Next
        End Sub

        ''' <summary>`1,1920929e-07` — `0x34000000` en `0x142F3C760`. Es el piso de `|qd.xyz|²` bajo
        ''' el cual el motor declara que no hubo giro. ⛔ No es `FLT_EPSILON` de casualidad: es el
        ''' mismo numero, pero comparado contra el CUADRADO de la norma.</summary>
        Friend Const EpsilonDelEje As Single = 0.00000011920929F

        ''' <summary>La parte 3×3 de un transform: `quatDeMatriz` (`0x14135EE20`) lee tres filas y
        ''' la traslacion no entra.</summary>
        Friend Function TresDe(m As Mat4) As Mat3
            Dim r As Mat3
            r.F0 = m.F0
            r.F1 = m.F1
            r.F2 = m.F2
            Return r
        End Function

    End Module

End Namespace

