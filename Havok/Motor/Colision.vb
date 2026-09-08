Option Strict On
Option Explicit On

Imports System.Runtime.Intrinsics

' =================================================================================================
' LA COLISIÓN — `SolveContacts` `0x141A71610`, su tabla de 12 entradas `0x141A75DD0`, y LA RESPUESTA.
'
' Ley: RE_MOTOR_FISICA_CANONICO_2026-09-05.md, caps. 6.6, 6.6.2bis, 6.6.2ter y 6.6.3bis.
'
' ⛔⛔ LA RESPUESTA ES UNA SOLA, y está verificada en DOS kernels independientes del binario: la
' cápsula (`0x141A6A490`) y la esfera (`0x141A70120`). Por eso vive acá una sola vez y los shapes
' sólo aportan su punto más cercano — no es una abstracción mía, es el reparto que tiene el `.exe`
' (y el del cono lo hace explícito: `0x141A708E0` llama a `0x141A07C00` y después aplica esto).
'
' ⛔ `dtSub` **NO escala la corrección de posición**. La separación es una proyección exacta a la
' superficie; `dtSub` sólo convierte la velocidad del colisionable en el desplazamiento del substep.
'
' ⛔ La fricción se aplica sobre **`Previas`**. En Verlet la velocidad es `P − Pprev`, así que sumarle
' `vt·fricción` a `Pprev` **resta** velocidad tangencial. Aplicarla a `Posiciones` la sumaría.
' =================================================================================================

#If DEBUG Then

Namespace Havok.Motor

    ''' <summary>
    ''' Una entrada del **buffer de trabajo** de colisionables — `0x90` B por entrada en el motor
    ''' (`simCloth[+0x1C0]`, cuenta en `+0x1B8`).
    ''' <para>⛔ Es una **copia** del `hclCollidable`, no el objeto del archivo: el constructor clona
    ''' cada colisionable y le sobreescribe los campos de pellizco desde `collidablePinchingDatas`
    ''' (cap. 2q.8). Y el transform **camina** de la pose vieja a la nueva a lo largo de los substeps
    ''' (cap. 6.9), así que esto cambia dentro del cuadro.</para>
    ''' </summary>
    Friend NotInheritable Class Colisionable

        ''' <summary>El transform, 4 filas de 4. La fila 3 (`+0x50` en el motor) es la traslación.</summary>
        Friend Transform As Mat4

        ''' <summary>`+0x60` — velocidad lineal, **derivada** por `setTransform` (cap. 6.9). El valor
        ''' serializado en el archivo es basura.</summary>
        Friend VelLineal As Vector128(Of Single)

        ''' <summary>`+0x70` — velocidad angular, ídem.</summary>
        Friend VelAngular As Vector128(Of Single)

        ''' <summary>`+0x80` — el byte de pellizco del buffer de trabajo, el que filtra
        ''' `SolveContacts` (`0x141A716D7`). Lo escribe el constructor, no el archivo.</summary>
        Friend PellizcoActivo As Boolean

        ''' <summary>`+0x88` — el shape, ya con sus datos derivados al mundo.</summary>
        Friend Forma As Forma

    End Class

    ''' <summary>Un transform de 4 filas de 16 B, como el del motor.</summary>
    Friend Structure Mat4
        Friend F0 As Vector128(Of Single)
        Friend F1 As Vector128(Of Single)
        Friend F2 As Vector128(Of Single)
        ''' <summary>La traslación — `coll.transform + 0x50`.</summary>
        Friend F3 As Vector128(Of Single)



        ''' <summary>
        ''' `A ∘ B`, o sea `p ↦ A(B(p))` — `0x141298180`.
        ''' <para>```
        ''' out.Fk = (B.Fk.y·A.F1 + B.Fk.x·A.F0) + B.Fk.z·A.F2   ' k = 3,2,1,0
        ''' out.F3 = out.F3 + A.F3                               ' 0x14129823D/41/45, AL FINAL
        ''' ```</para>
        ''' <para>⛔ No confundir con <see cref="ComponerConInversa"/> (`0x141298780`), que compone
        ''' con la **inversa** del segundo. Son dos funciones distintas del motor y se usan en
        ''' sitios distintos: esta en `TransferMotion`, la otra en el setup del campo de
        ''' alturas.</para>
        ''' <para>⛔ Las filas salen en orden **3, 2, 1, 0** y la suma de `A.F3` a la fila 3 va
        ''' **despues** de escribir las cuatro. Reasociar cambia bits.</para>
        ''' </summary>
        Friend Shared Function Componer(a As Mat4, b As Mat4) As Mat4
            Dim r As Mat4
            r.F3 = FilaCompuesta(b.F3, a)                                 ' 0x141298184…B9
            r.F2 = FilaCompuesta(b.F2, a)                                 ' 0x1412981BD…E3
            r.F1 = FilaCompuesta(b.F1, a)                                 ' 0x1412981E7…0D
            r.F0 = FilaCompuesta(b.F0, a)                                 ' 0x141298211…3A
            r.F3 = Vector128.Add(a.F3, r.F3)                              ' 0x14129823D/41/45
            Return r
        End Function

        ''' <summary>
        ''' `A ∘ inversaRigida(B)` **fundido** — `0x141298780`, o sea `p ↦ A(B⁻¹(p))`.
        ''' <para>```
        ''' out.Fk = ((Bᵀk.y·A.F1 + Bᵀk.x·A.F0) + Bᵀk.z·A.F2)     ' 0x1412987AD…0x141298848
        ''' out.F3 = A.F3 − ((B.F3.y·out.F1 + B.F3.x·out.F0) + B.F3.z·out.F2)  ' 0x14129882C…77
        ''' ```</para>
        ''' <para>⛔ Nunca materializa `B⁻¹`: transpone `B` con `unpck`/`movlhps`/`movhlps` y usa
        ''' `B.F3` sin negar, restando al final. Reconstruirlo como
        ''' `Por(A, InversaRigida(B))` daria otro orden de sumas.</para>
        ''' </summary>
        Friend Shared Function ComponerConInversa(a As Mat4, b As Mat4) As Mat4
            Dim bt0 = Vector128.Create(b.F0.GetElement(0), b.F1.GetElement(0), b.F2.GetElement(0), 0.0F)
            Dim bt1 = Vector128.Create(b.F0.GetElement(1), b.F1.GetElement(1), b.F2.GetElement(1), 0.0F)
            Dim bt2 = Vector128.Create(b.F0.GetElement(2), b.F1.GetElement(2), b.F2.GetElement(2), 0.0F)
            Dim r As Mat4
            r.F0 = FilaCompuesta(bt0, a)
            r.F1 = FilaCompuesta(bt1, a)
            r.F2 = FilaCompuesta(bt2, a)
            r.F3 = Vector128.Subtract(a.F3, FilaCompuesta(b.F3, r))       ' 0x14129885A/77
            Return r
        End Function

        ''' <summary>`(v.y·M1 + v.x·M0) + v.z·M2` — sin traslacion, con la `y` primero
        ''' (`0x1412987DA`/`DF` antes que `0x1412987B0`/`D7`).</summary>
        Private Shared Function FilaCompuesta(v As Vector128(Of Single), m As Mat4) As Vector128(Of Single)
            Dim r = Vector128.Add(Vector128.Multiply(Simd.BcastY(v), m.F1),
                                  Vector128.Multiply(Simd.BcastX(v), m.F0))
            Return Vector128.Add(r, Vector128.Multiply(Simd.BcastZ(v), m.F2))
        End Function

        ''' <summary>
        ''' La inversa de un transform **rigido** — `0x141298100`: transpuesta de la 3×3 y
        ''' `(−t)` rotado por ella.
        ''' <para>```
        ''' out.F0..F2 = transpuesta(in.F0..F2)      ' 0x141298113…0x141298131 (unpck + movlhps/movhlps)
        ''' out.F3     = ((−t).y·R1 + (−t).x·R0) + (−t).z·R2    ' 0x141298152…0x14129816A
        ''' ```</para>
        ''' <para>⛔ El `−t` sale de un `xorps` con `0x80000000` difundido
        ''' (`0x141298126 pinsrw` + `0x141298135 pshufd`), no de un `0 − t`: con `t = 0` da `−0`,
        ''' y `−0` y `+0` no son el mismo patron de bits.</para>
        ''' <para>⛔ Y la suma arranca por **`y`** (`0x141298152 shufps 0x55` antes que el
        ''' `0x141298156 shufps 0`), sin traslacion al final.</para>
        ''' <para>⛔ Supone que la 3×3 es **ortonormal**. El motor no comprueba nada: si el
        ''' transform trae escala, esta «inversa» no lo es. Asi es el motor.</para>
        ''' </summary>
        Friend Shared Function InversaRigida(m As Mat4) As Mat4
            Dim r As Mat4
            r.F0 = Vector128.Create(m.F0.GetElement(0), m.F1.GetElement(0), m.F2.GetElement(0), 0.0F)
            r.F1 = Vector128.Create(m.F0.GetElement(1), m.F1.GetElement(1), m.F2.GetElement(1), 0.0F)
            r.F2 = Vector128.Create(m.F0.GetElement(2), m.F1.GetElement(2), m.F2.GetElement(2), 0.0F)
            ' −t por XOR del bit 31 — 0x141298149
            Dim nt = Vector128.Xor(m.F3.AsUInt32(), Vector128.Create(2147483648UI)).AsSingle()
            Dim t = Vector128.Add(Vector128.Multiply(Simd.BcastY(nt), r.F1),
                                  Vector128.Multiply(Simd.BcastX(nt), r.F0))   ' 0x14129815A/5D + 0x141298167
            r.F3 = Vector128.Add(t, Vector128.Multiply(Simd.BcastZ(nt), r.F2)) ' 0x141298164 + 0x14129816A
            Return r
        End Function

        Friend Shared ReadOnly Property Identidad As Mat4
            Get
                Dim m As Mat4
                m.F0 = Vector128.Create(1.0F, 0.0F, 0.0F, 0.0F)
                m.F1 = Vector128.Create(0.0F, 1.0F, 0.0F, 0.0F)
                m.F2 = Vector128.Create(0.0F, 0.0F, 1.0F, 0.0F)
                m.F3 = Vector128.Create(0.0F, 0.0F, 0.0F, 1.0F)
                Return m
            End Get
        End Property
    End Structure

    ' =============================================================================================

    Friend Module Colision

        ''' <summary>
        ''' La tabla de despacho `0x141A75DD0` — **leída del binario**, 12 entradas de RVA de 4 B.
        ''' <para>⛔ Los tipos **4, 6, 7 y 8** apuntan los cuatro al mismo `return` (`0x141A75D5C`).
        ''' No es que «no estén implementados»: el motor **decide** no hacer nada con ellos, y hay que
        ''' replicar esa decisión — no un `else` que caiga en el shape más parecido.</para>
        ''' </summary>
        Friend ReadOnly TablaDeDespacho As ULong() = {
            &H141A74416UL,   ' 0  hclSphereShape
            &H141A73B76UL,   ' 1  hclPlaneShape
            &H141A71718UL,   ' 2  hclCapsuleShape
            &H141A71FB6UL,   ' 3  hclTaperedCapsuleShape → respuesta 0x141A70FD0, caché 0x141A708E0
            &H141A75D5CUL,   ' 4  — el `return`
            &H141A72924UL,   ' 5  hclConvexHeightFieldShape
            &H141A75D5CUL,   ' 6  — el `return`
            &H141A75D5CUL,   ' 7  — el `return`
            &H141A75D5CUL,   ' 8  — el `return`
            &H141A732D4UL,   ' 9  hclConvexGeometryShape
            &H141A74CB6UL,   ' 10 hclPointContactPlanesShape (el del terreno)
            &H141A754BCUL    ' 11 hclConvexPlanesShape
        }

        ''' <summary>La VA del `return` común de los tipos 4, 6, 7 y 8.</summary>
        Friend Const VaDelReturn As ULong = &H141A75D5CUL

        ''' <summary>`True` si ese `type` no hace nada: fuera de rango (`0x141A71702`: `si t > 11`) o
        ''' uno de los cuatro que apuntan al `return`.</summary>
        Friend Function NoHaceNada(tipo As Integer) As Boolean
            If tipo < 0 OrElse tipo > 11 Then Return True
            Return TablaDeDespacho(tipo) = VaDelReturn
        End Function

        ''' <summary>
        ''' El bit de `staticCollisionMasks` que le toca al colisionable `ci` — `1 &lt;&lt; min(ci, 30)`.
        ''' <para>Citas reales: `0x141A71744`-`0x141A71754` (cápsula) y `0x141A73BA2`-`0x141A73BB2`
        ''' (plano). ⚠️ La primera redacción citaba `0x141A71641`, que es un `movzx r11d, r9b` y no
        ''' tiene nada que ver (motor-57).</para>
        ''' <para>⛔ El `min(·, 30)` no es un detalle: con 31 colisionables o más, **todos los que
        ''' pasan de 30 comparten el bit 30**. Es del motor.</para>
        ''' </summary>
        Friend Function BitDeMascara(ci As Integer) As UInteger
            Return 1UI << Math.Min(ci, 30)
        End Function

        ''' <summary>
        ''' `SolveContacts` — `0x141A71610`.
        ''' <para>Recorre el buffer de trabajo, aplica el **filtro de pellizco** y despacha por
        ''' `shape.type`:</para>
        ''' <para>· `buf[+0x80] != 0` ⇒ sólo si `conPellizco` (`0x141A716D7`-`0x141A716E7`)</para>
        ''' <para>· `buf[+0x80] == 0` ⇒ sólo si `sinPellizco` (`0x141A716EE`/`F1`)</para>
        ''' <para>⚠️ En la ruta NORMAL de FO4 los dos booleanos van en `True` (cap. 6.6.1): el camino
        ''' de pellizco no abre nunca en el corpus, pero el filtro se implementa igual porque el
        ''' usuario pidió el motor completo.</para>
        ''' </summary>
        Friend Sub ResolverContactos(inst As Instancia, colisionables As Colisionable(),
                                     dtSub As Single, conPellizco As Boolean, sinPellizco As Boolean)
            If colisionables Is Nothing Then Return
            For ci = 0 To colisionables.Length - 1                  ' 0x141A7165F: si n <= 0, nada
                Dim c = colisionables(ci)
                If c Is Nothing OrElse c.Forma Is Nothing Then Continue For
                If c.PellizcoActivo Then
                    If Not conPellizco Then Continue For            ' 0x141A716DF/E2/E7
                Else
                    If Not sinPellizco Then Continue For            ' 0x141A716EE/F1
                End If
                If NoHaceNada(c.Forma.Tipo) Then Continue For       ' 0x141A71702/05 + la tabla
                ResolverUnColisionable(inst, c, ci, dtSub)
            Next
        End Sub

        ''' <summary>
        ''' La respuesta, para un colisionable y las partículas que su máscara habilita.
        ''' <para>La lista de partículas sale de `staticCollisionMasks`: el motor la arma antes de
        ''' llamar al kernel (`0x141A71893`: `test dword [r8], r12d`). Si el dato no trae máscaras,
        ''' entran **todas** — que es lo que hace el motor cuando el arreglo no existe.</para>
        ''' </summary>
        Private Sub ResolverUnColisionable(inst As Instancia, c As Colisionable, ci As Integer,
                                           dtSub As Single)
            Dim dt = Vector128.Create(dtSub)

            ' ⛔⛔ EL SHAPE SE DERIVA **UNA VEZ POR COLISIONABLE**, acá, antes del bucle de
            ' partículas — no por contacto (motor-75). El kernel `0x141A70FD0` reserva un scratch
            ' de pila (`0x141A71031 lea r8, [rbp+0x70]`), lo llena con `0x141A7103A` y **recién
            ' después** (`0x141A71049…`) recorre las partículas.
            ' ⛔ Y hace falta porque el shape del archivo está en espacio LOCAL y el colisionable
            ' **se mueve en cada substep** (paso 4a del cap. 5): el RE titula sus dos secciones
            ' «recálculo **por substep**». El corpus mide `subSteps > 1` en **401 de 451** prendas,
            ' así que guardarlo derivado y no recalcularlo hace colisionar contra la pose del
            ' arranque del frame.
            Dim forma = c.Forma.Derivar(c.Transform)

            ' ⛔ LA MÁSCARA SÓLO SE APLICA SI `ci < data.perInstanceCollidables.count` (`data+0xB0`):
            ' `0x141A7173F cmp ebx, r9d / jge` y `0x141A71C6D jge`. Para `ci >= count` la lista se arma
            ' con **todas** las partículas. En FO4 no se ejerce (el buffer de trabajo son los clones
            ' per-instance, cap. 2ter.2), pero la ley es ésa y se implementa entera (motor-57).
            Dim masc = If(ci < inst.NumColisionablesPorInstancia, inst.MascarasDeColision, Nothing)
            Dim bit = BitDeMascara(ci)

            ' ⛔⛔ EL REPARTO BLOQUE / RESTO. El motor arma la lista de partículas del colisionable y
            ' la recorre en bloques de 4 (`sar r13d, 2`) **más un bucle de resto** (`and cx, 3`), y
            ' **el resto NO corre la misma ley** (ver `Forma.PuntoMasCercano`). Así que primero hay
            ' que saber cuántas entran, y recién después cuáles caen en las últimas `n mod 4`.
            Dim lista As New List(Of Integer)()
            For i = 0 To inst.NumParticulas - 1
                If masc IsNot Nothing AndAlso i < masc.Length Then
                    If (masc(i) And bit) = 0UI Then Continue For    ' 0x141A71893 test
                End If
                lista.Add(i)
            Next
            Dim nBloque = (lista.Count \ 4) * 4                    ' 0x141A6A5C5 sar r13d, 2

            For k = 0 To lista.Count - 1
                Dim i = lista(k)
                Dim enResto = (k >= nBloque)                        ' 0x141A6ABF9 and cx, 3

                Dim p = Simd.Leer(inst.Posiciones, i)
                Dim radioP = inst.Radio(i)
                Dim con = forma.PuntoMasCercano(p, i, radioP, enResto)
                If Not (con.Distancia - radioP < 0.0F) Then Continue For   ' cmpltps contra 0

                ' --- separación: proyección EXACTA, sin escalar por dtSub
                Dim n = con.Normal
                Dim corr = radioP - Simd.Lane0(Simd.Dot3(Vector128.Subtract(p, con.Superficie), n))
                p = Vector128.Add(p, Vector128.Multiply(Vector128.Create(corr), n))
                Simd.Escribir(inst.Posiciones, i, p)

                ' --- fricción: sobre PREVIAS, con la velocidad relativa al colisionable
                Dim r = Vector128.Subtract(con.Superficie, c.Transform.F3)
                Dim vC = Vector128.Multiply(
                    Vector128.Add(c.VelLineal, Polar.Cruz(c.VelAngular, r)), dt)
                Dim prev = Simd.Leer(inst.Previas, i)
                Dim v = Vector128.Subtract(Vector128.Subtract(p, prev), vC)
                Dim vt = Vector128.Subtract(v, Vector128.Multiply(Simd.Dot3(v, n), n))
                Simd.Escribir(inst.Previas, i,
                              Vector128.Add(prev, Vector128.Multiply(vt, Vector128.Create(inst.Friccion(i)))))
            Next
        End Sub

    End Module

End Namespace

#End If
