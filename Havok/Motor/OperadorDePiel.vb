Option Strict On
Option Explicit On

Imports System.Runtime.Intrinsics
Imports FO4_Base_Library.Havok.Canon.Objects

' =================================================================================================
' `hclSkinOperator` — type **8**.
'
' Es la piel de PESOS del motor: la única que trae `boneInfluences` con peso propio, la única que
' tiene camino de CUATERNIÓN DUAL, y la única que compone las matrices de hueso con la matriz del
' buffer de salida. No se parece a `hclObjectSpaceSkin*` (22-25) ni a `hclBoneSpaceSkin*` (18-21):
' aquéllas traen la posición local horneada, ésta trae la matriz por hueso y el peso aparte.
'
' ⭐⭐ LA ENTRADA DEL DESPACHADOR — `0x1418C604D`-`0x1418C60B4`, leída entera:
'
'     cmp byte [op+0x75], 0                       ' dualQuaternionSkinning
'     entrada = buffers[ buffers[ op.inputBufferIndex  (+0x64) ].Ranura ]   ⬅ doble indirección
'     salida  = buffers[ buffers[ op.outputBufferIndex (+0x68) ].Ranura ]   ⬅ doble indirección
'     ts      = transformSets[ op.transformSetIndex (+0x6C) ]              ⬅ DIRECTA
'     je  0x1419090A0     ' == 0 → MEZCLA LINEAL   («TtBuild composite matrices» + «TtSkin»)
'     call 0x141909760    ' != 0 → CUATERNIÓN DUAL («… composite matrices» + «TtBuild dual
'                         '        quaternions» + «TtSkin»)
'
' ⛔ Los offsets salen de la reflexión, no de la simetría: la tabla de `Hk_HclSkinOperator` da
' `{8,16,24,32,48,64,80,96,97,98,99,100,104,108,112,114,116,117,118}` y en memoria el objeto es la
' misma tabla menos `0x20` a partir del primer `hkArray` — por eso `+0x64` es `inputBufferIndex`,
' `+0x75` es `dualQuaternionSkinning` y `+0x76` es `boneGroupSize`, que es lo que el `.exe` lee.
'
' ⭐⭐ FASE 1 — «TtBuild composite matrices» (`0x1419090A0`; en el camino dual, `0x1419098CE`).
' MEDIDO que los dos cuerpos son la MISMA aritmética: 46 operaciones SIMD en el mismo orden.
'
'     n = (usedBoneGroupIds.count > 0) ? boneGroupSize · usedBoneGroupIds.count      ' 0x1419090DC/E0
'                                     : transformSet.numTransforms                  ' 0x1419090E5
'     k = 0
'     por cada i de usedBoneGroupIds, por cada j de 0..boneGroupSize−1:             ' 0x1419091F0
'         t = usedBoneGroupIds[i] · boneGroupSize + j                               ' 0x14190923E/41
'         si t < transformSet.numTransforms:                                        ' 0x141909243
'             comp[k] = Componer( Componer( boneFromSkinMeshTransforms[k],
'                                           transformSet[t] ),
'                                 salida.DesdeEspacioDeSimulacion )                 ' 0x14190924C-0x1419093CA
'         k += 1                                                                    ' rsi/rbp += 0x40
'
' ⛔ Sin `usedBoneGroupIds` la rama es OTRA (`0x141909571`) pero la ley es la misma con el grupo
' identidad: recorre `transformSet` y `boneFromSkinMeshTransforms` en paralelo. Y `k` avanza también
' cuando el `t` se pasa del set: la entrada queda como estaba en el scratch.
'
' ⭐⭐ FASE 2 — «TtBuild dual quaternions» (`0x141909D95`-`0x141909FD3`), sólo en el camino dual:
'
'     q[k]  = cuaternionDeMatriz( comp[k].filas 0..2 )                    ' 0x14135EE20
'     si k = 0: si hsum4(q[0])          < 0 → q[0] = −q[0]                ' 0x141909D99-0x141909E24
'     si k > 0: si hsum4(q[k] · q[0])   < 0 → q[k] = −q[k]                ' 0x141909F12, xmm9 = q[0]
'     d[k]  = ( comp[k].fila3 · 0,5 , con w = 0 ) ⊗ q[k]                  ' 0x141909E2E-0x141909EB4
'
' ⛔ El signo se arregla ANTES del producto (el `movups [r13]` de `0x141909E24` y la relectura de
' `0x141909E47`), y el ancla es el cuaternión CERO del arreglo, no el `w` de cada uno.
'
' ⭐⭐ FASE 3 — «TtSkin» (`0x14190A500` lineal, `0x14190A160`→`0x14190A400` dual). Las dos despachan
' 12 kernels con la MISMA forma: cuatro combinaciones de canal × las variantes de ancho de acceso.
'
'     P = skinPositions  (+0x60) && entrada.pos != 0 && salida.pos != 0    ' 0x14190A566-0x14190A588
'     N = skinNormals    (+0x61) && entrada.nrm != 0 && salida.nrm != 0    ' 0x14190A592-0x14190A5A9
'     T = skinTangents   (+0x62) && entrada.tan != 0 && salida.tan != 0    ' 0x14190A5B0-0x14190A5C7
'     B = skinBiTangents (+0x63) && entrada.bit != 0 && salida.bit != 0    ' 0x14190A5CE-0x14190A5E5
'
' ⛔⛔ **LOS CANALES SON JERÁRQUICOS.** El despacho de `0x14190A75B` manda a la familia «P sola»
' apenas falta `N`, y de ahí `0x14190A83C` **no hace NADA** si falta `P`. O sea: sin posiciones no se
' escribe ni una normal, y sin normales no se escribe ni una tangente, por más que los `bool` del
' operador las pidan.
'
' ⛔ Las variantes de ancho (`test byte [buf+0x20],1` y hermanas sobre `+0x48`/`+0x60`/`+0x78`) son
' de ACCESO, no de aritmética: MEDIDO sobre los 12 kernels de cada camino que el multiconjunto de
' `mulps`/`shufps` es idéntico dentro de cada familia de canales (PNTB 187/82, PNT 184/79, PN 181/76,
' P 178/73) y que lo único que cambia son los `movhlps` del guardado de 12 B. Acá el acceso va por el
' stride del `Buffer`, que cubre los dos casos sin duplicar la ley — la misma decisión que
' `Operadores.CopiarVertices`.
'
' ⭐⭐ EL BUCLE POR VÉRTICE (`0x14191E440` para el lineal, `0x14190FBF0` para el dual):
'
'     el rango es `startVertex` (+0x70) .. `endVertex` (+0x72), y los punteros de ENTRADA y de
'     SALIDA arrancan corridos `startVertex · stride` (`0x14190A681`-`0x14190A748`)
'
'     ini = boneInfluenceStartPerVertex[v] ; fin = boneInfluenceStartPerVertex[v+1]
'     si fin <= ini: el vértice NO SE ESCRIBE                            ' 0x14191E5EA jbe
'     n = fin − ini
'
' ⛔⛔ **CON UNA SOLA INFLUENCIA EL PESO NO SE APLICA.** `0x14191E642` lee el `boneIndex`, saltea el
' byte del peso y carga las cuatro filas de la matriz TAL CUAL. No es `w/255 · M`: es `M`.
'
' ⛔⛔ **Y HAY UN TECHO DURO DE INFLUENCIAS, DISTINTO EN CADA CAMINO.** La tabla de salto de
' `0x14191F7C8` tiene OCHO entradas y `0x14191E62B` (`cmp eax,7` / `ja`) manda todo lo que pase de 8
' a aplicar con la matriz en CERO — el vértice se va al origen. El camino dual sólo desenrolla
' CUATRO (`0x14190FD66`-`0x14190FD84`) y con más de 4 aplica el dual-cuaternión en cero.
'
'     con n >= 2:  M = Σ_k (peso_k / 255) · comp[hueso_k]                ' 1/255 en `0x142492850`
'                  (suma de izquierda a derecha, en el orden del arreglo — 0x14191F63F-0x14191F66B)
'
'     posición  = ((P.x·M0 + M3) + P.y·M1) + P.z·M2                      ' 0x14191F68B-0x14191F6B5
'     normal    = (N.y·M1 + N.x·M0) + N.z·M2                             ' 0x1419173A7-0x1419173CD
'     tangente  y bitangente, la misma forma de dirección                 ' 0x1419173E3-0x14191742D
'
' ⛔ La traslación entra **enseguida del término en `x`**, igual que en `CopyVertices` y en
' `MoveParticles`: es <see cref="Operadores.TransformarPunto"/> y no otra.
'
' ⭐⭐ EL CAMINO DUAL (`0x1419100B4`-`0x141910174`, y las cuatro copias de `0x14190CEC9`-`0x14190D026`):
'
'     con n = 1:   q = real[hueso] ; d = dual[hueso]   — SIN peso y SIN normalizar
'     con n >= 2:  qb = Σ w_k·real_k ; db = Σ w_k·dual_k                 ' 0x141910028-0x141910090
'                  s  = rsqrt+Newton( hsum4(qb·qb) ) con guarda `<= 0`   ' 0x141910056-0x141910078
'                  q = qb·s ; d = db·s                                    ' 0x14191008C/94
'
'     rot   = P + 2·( q_v × (q_v × P + q_w·P) )                          ' 0x1419100DF-0x141910160
'     trans = 2·( q_v × d_v − d_w·q_v + q_w·d_v )                        ' 0x1419100D6-0x14191016A
'     posición = rot + trans                                             ' 0x141910171
'     normal / tangente / bitangente = X + 2·( q_v × (q_v × X + q_w·X) ) ' 0x14190CEE6-0x14190D00B
'
' ⛔ Las tres direcciones llevan SÓLO la rotación: la parte dual no entra. Y el `2` es el
' `{2,2,2,2}` de `0x1424DAC30`; la Newton, las mismas `3,0` de `0x142629510` y `0,5` de
' `0x142629520` que el resto del motor.
'
' ⛔ `partialSkinning` (+0x74) **no lo lee nadie** en todo el camino del type 8: `startVertex` y
' `endVertex` se aplican siempre. Medido sobre las cuatro funciones de la cadena (`0x1418C604D`,
' `0x1419090A0`, `0x141909760`, `0x14190A500`), donde el único `+0x74` que aparece es el stride de
' bitangentes del BUFFER, no un campo del operador.
'
' ⚠️ CERO apariciones en el corpus vanilla: el censo de las 759 prendas da siete clases de operador y
' `hclSkinOperator` no está entre ellas. Va igual, que es de la lista cerrada del despachador.
' =================================================================================================

#If DEBUG Then

Namespace Havok.Motor

    ''' <summary>Un cuaternión dual — 32 B en el motor: la parte REAL en `+0x00` y la DUAL en
    ''' `+0x10` (`0x141909DD1` y `0x141909EB4`).</summary>
    Friend Structure CuaternionDual
        Friend Real As Vector128(Of Single)
        Friend Dual As Vector128(Of Single)
    End Structure

    ''' <summary>`hclSkinOperator` — type 8, ya compilado desde el archivo.</summary>
    Friend NotInheritable Class PielConPesosCompilada

        ''' <summary>`inputBufferIndex` (+0x64).</summary>
        Friend ReadOnly BufferDeEntrada As Integer

        ''' <summary>`outputBufferIndex` (+0x68).</summary>
        Friend ReadOnly BufferDeSalida As Integer

        ''' <summary>`transformSetIndex` (+0x6C).</summary>
        Friend ReadOnly IndiceDelTransformSet As Integer

        ''' <summary>`boneInfluences[k].boneIndex` — `uint8` en el archivo (la reflexión da la
        ''' estructura `{boneIndex@0, weight@1}`, 2 B por entrada).</summary>
        Friend ReadOnly Huesos As Integer()

        ''' <summary>`boneInfluences[k].weight` — `uint8`. ⛔ Se desquantiza con `1/255`
        ''' (`0x142492850`), no con `1/256` ni con `1/128`.</summary>
        Friend ReadOnly Pesos As Byte()

        ''' <summary>`boneInfluenceStartPerVertex` — `uint16[numVertices + 1]`: las influencias del
        ''' vértice `v` son `[start[v], start[v+1])`.</summary>
        Friend ReadOnly ComienzoPorVertice As Integer()

        ''' <summary>`boneFromSkinMeshTransforms` — una matriz por entrada compuesta.</summary>
        Friend ReadOnly HuesoDesdeMalla As Mat4()

        ''' <summary>`usedBoneGroupIds` — `uint16`. Vacío = se usan todas las del transformSet.</summary>
        Friend ReadOnly GruposUsados As Integer()

        ''' <summary>`boneGroupSize` (+0x76) — `uint8`.</summary>
        Friend ReadOnly TamanoDeGrupo As Integer

        ''' <summary>`startVertex` (+0x70) y `endVertex` (+0x72), `uint16` los dos.</summary>
        Friend ReadOnly VerticeInicial As Integer
        Friend ReadOnly VerticeFinal As Integer

        ''' <summary>`skinPositions` / `skinNormals` / `skinTangents` / `skinBiTangents`
        ''' (+0x60 a +0x63).</summary>
        Friend ReadOnly Posiciones As Boolean
        Friend ReadOnly Normales As Boolean
        Friend ReadOnly Tangentes As Boolean
        Friend ReadOnly Bitangentes As Boolean

        ''' <summary>`dualQuaternionSkinning` (+0x75) — el bit que parte el operador en dos motores
        ''' distintos (`0x1418C604D`).</summary>
        Friend ReadOnly CuaternionesDuales As Boolean

        Friend Sub New(bufferDeEntrada As Integer, bufferDeSalida As Integer,
                       indiceDelTransformSet As Integer, huesos As Integer(), pesos As Byte(),
                       comienzoPorVertice As Integer(), huesoDesdeMalla As Mat4(),
                       gruposUsados As Integer(), tamanoDeGrupo As Integer,
                       verticeInicial As Integer, verticeFinal As Integer,
                       posiciones As Boolean, normales As Boolean, tangentes As Boolean,
                       bitangentes As Boolean, cuaternionesDuales As Boolean)
            Me.BufferDeEntrada = bufferDeEntrada
            Me.BufferDeSalida = bufferDeSalida
            Me.IndiceDelTransformSet = indiceDelTransformSet
            Me.Huesos = If(huesos, Array.Empty(Of Integer)())
            Me.Pesos = If(pesos, Array.Empty(Of Byte)())
            Me.ComienzoPorVertice = If(comienzoPorVertice, Array.Empty(Of Integer)())
            Me.HuesoDesdeMalla = If(huesoDesdeMalla, Array.Empty(Of Mat4)())
            Me.GruposUsados = If(gruposUsados, Array.Empty(Of Integer)())
            Me.TamanoDeGrupo = tamanoDeGrupo
            Me.VerticeInicial = verticeInicial
            Me.VerticeFinal = verticeFinal
            Me.Posiciones = posiciones
            Me.Normales = normales
            Me.Tangentes = tangentes
            Me.Bitangentes = bitangentes
            Me.CuaternionesDuales = cuaternionesDuales
        End Sub

    End Class

    ''' <summary>`hclSkinOperator` — type 8.</summary>
    Friend NotInheritable Class OpPielConPesos
        Inherits OperadorCompilado

        Friend ReadOnly Piel As PielConPesosCompilada

        Friend Sub New(piel As PielConPesosCompilada, nombre As String)
            MyBase.New(8, nombre)                       ' la tabla de `0x1418C6390`
            Me.Piel = piel
        End Sub

        Friend Overrides Sub Ejecutar(ByRef ctx As ContextoDeCadena)
            Dim p = Piel
            If p Is Nothing Then Return
            Dim entrada = Buffers.Real(ctx.Buffers, p.BufferDeEntrada)     ' 0x1418C6056-0x1418C6065
            Dim salida = Buffers.Real(ctx.Buffers, p.BufferDeSalida)       ' 0x1418C6069-0x1418C607C
            If entrada Is Nothing OrElse salida Is Nothing Then Return
            If ctx.TransformSets Is Nothing Then Return
            If p.IndiceDelTransformSet < 0 OrElse
               p.IndiceDelTransformSet >= ctx.TransformSets.Length Then Return
            Dim ts = ctx.TransformSets(p.IndiceDelTransformSet)            ' 0x1418C6080/84, DIRECTA
            If ts Is Nothing Then Return

            Dim comp = PielConPesos.Compuestas(p, ts, salida)              ' fase 1
            If p.CuaternionesDuales Then                                   ' 0x1418C604D cmp/je
                PielConPesos.DeformarDual(p, entrada, salida, PielConPesos.Duales(comp))
            Else
                PielConPesos.DeformarLineal(p, entrada, salida, comp)
            End If
        End Sub

    End Class

    ''' <summary>Las tres fases del type 8, cada una con su cita.</summary>
    Friend Module PielConPesos

        ''' <summary>El techo de influencias del camino LINEAL: la tabla de salto de `0x14191F7C8`
        ''' tiene ocho entradas y `0x14191E62B` manda el resto a la matriz en cero.</summary>
        Friend Const MaxInfluenciasLineal As Integer = 8

        ''' <summary>El techo del camino DUAL: `0x14190FD66`-`0x14190FD84` desenrolla cuatro y nada
        ''' más.</summary>
        Friend Const MaxInfluenciasDual As Integer = 4

        ''' <summary>`1/255` — `0x142492850`, el vector `{0,00392156886 ×4}` que los 24 kernels
        ''' cargan para desquantizar el peso de un byte.</summary>
        Friend Const EscalaDePeso As Single = 0.00392156886F

        ' -----------------------------------------------------------------------------------------
        ' FASE 1 — «TtBuild composite matrices»
        ' -----------------------------------------------------------------------------------------

        ''' <summary>
        ''' `0x1419090A0` (y `0x1419098CE`, MEDIDO idéntico instrucción por instrucción).
        ''' <para>```
        ''' n = grupos ? boneGroupSize · grupos.count : transformSet.count
        ''' comp[k] = Componer( Componer(boneFromSkinMeshTransforms[k], transformSet[t]),
        '''                     salida.DesdeEspacioDeSimulacion )
        ''' ```</para>
        ''' <para>⛔ Cuando `t` se pasa del transformSet el motor SALTEA la escritura y sigue
        ''' avanzando el `k` (`0x141909243` `jae` → `0x1419093D3`): la casilla queda con lo que el
        ''' scratch traía. Acá queda en CERO, que es lo que da una asignación fresca; no hay forma de
        ''' replicar memoria sin inicializar y no se inventa una identidad para taparlo.</para>
        ''' </summary>
        Friend Function Compuestas(p As PielConPesosCompilada, ts As Mat4(), salida As Buffer) As Mat4()
            Dim cuantasDelSet = If(ts Is Nothing, 0, ts.Length)
            Dim grupos = If(p.GruposUsados Is Nothing, 0, p.GruposUsados.Length)
            Dim tam = p.TamanoDeGrupo

            ' 0x1419090D5-0x1419090E9: el conteo sale de los grupos si los hay, del set si no
            Dim n = If(grupos > 0, tam * grupos, cuantasDelSet)
            If n <= 0 Then Return Array.Empty(Of Mat4)()

            Dim r(n - 1) As Mat4
            Dim m = salida.DesdeEspacioDeSimulacion              ' `[salida+0xC0…0xF0]`
            Dim k = 0
            If grupos > 0 Then
                For i = 0 To grupos - 1
                    Dim gid = p.GruposUsados(i)                  ' 0x1419091FB movzx word
                    For j = 0 To tam - 1
                        If k >= n Then Exit For
                        Dim t = gid * tam + j                    ' 0x14190923E/41
                        If t >= 0 AndAlso t < cuantasDelSet AndAlso
                           k < p.HuesoDesdeMalla.Length Then     ' 0x141909243 cmp/jae
                            r(k) = Operadores.Componer(
                                Operadores.Componer(p.HuesoDesdeMalla(k), ts(t)), m)
                        End If
                        k += 1                                   ' rsi/rbp += 0x40 igual
                    Next
                Next
                Return r
            End If

            ' 0x141909571-0x141909726: sin grupos, el set entero en su orden
            For t = 0 To n - 1
                If t >= p.HuesoDesdeMalla.Length Then Exit For
                r(t) = Operadores.Componer(
                    Operadores.Componer(p.HuesoDesdeMalla(t), ts(t)), m)
            Next
            Return r
        End Function

        ' -----------------------------------------------------------------------------------------
        ' FASE 2 — «TtBuild dual quaternions»
        ' -----------------------------------------------------------------------------------------

        ''' <summary>
        ''' `0x141909D95`-`0x141909FD3`: cada matriz compuesta de 64 B se convierte en un cuaternión
        ''' dual de 32 B.
        ''' <para>```
        ''' q[k] = cuaternionDeMatriz(comp[k].filas 0..2)          ' 0x14135EE20
        ''' q[0] se voltea si hsum4(q[0])        &lt; 0            ' 0x141909D99-0x141909E24
        ''' q[k] se voltea si hsum4(q[k]·q[0])   &lt; 0            ' 0x141909F12, xmm9 = q[0]
        ''' d[k] = ConWCero(comp[k].fila3 · 0,5) ⊗ q[k]            ' 0x141909E2E-0x141909EB4
        ''' ```</para>
        ''' <para>⛔ El ancla del signo es el cuaternión CERO ya volteado, no el `w` de cada uno; y
        ''' el volteo pasa ANTES del producto dual.</para>
        ''' </summary>
        Friend Function Duales(comp As Mat4()) As CuaternionDual()
            Dim n = If(comp Is Nothing, 0, comp.Length)
            If n = 0 Then Return Array.Empty(Of CuaternionDual)()
            Dim r(n - 1) As CuaternionDual
            Dim q0 = Vector128(Of Single).Zero
            For k = 0 To n - 1
                Dim rot As Mat3
                rot.F0 = comp(k).F0
                rot.F1 = comp(k).F1
                rot.F2 = comp(k).F2
                Dim q = Cuaternion.DeMatriz(rot)                       ' 0x14135EE20

                ' el ancla: el primero contra {1,1,1,1} (0x141909D95), el resto contra el primero
                Dim ancla = If(k = 0, Vector128.Create(1.0F), q0)      ' 0x141909DF9 / 0x141909F12
                Dim h = Hsum4(Vector128.Multiply(q, ancla))
                Dim negativo = Vector128.LessThan(h, Vector128(Of Single).Zero)   ' 0x141909E17 cmpltps
                q = Vector128.ConditionalSelect(negativo, Negar(q), q)            ' 0x141909E1B/1E/21
                If k = 0 Then q0 = q

                ' la parte dual: (traslación · 0,5, con w = 0) ⊗ q
                Dim t = Vector128.Multiply(comp(k).F3, Vector128.Create(0.5F))    ' 0x141909E2E
                t = t.WithElement(Simd.LaneW, 0.0F)                               ' 0x141909E35/3A
                r(k).Real = q
                r(k).Dual = Cuaternion.Producto(t, q)                             ' 0x141909E47-0x141909EB4
            Next
            Return r
        End Function

        ' -----------------------------------------------------------------------------------------
        ' FASE 3 — «TtSkin»
        ' -----------------------------------------------------------------------------------------

        ''' <summary>
        ''' Los cuatro canales, con la puerta de `0x14190A566`-`0x14190A5E5` y la JERARQUÍA de
        ''' `0x14190A75B`/`0x14190A83C`: sin `P` no se escribe nada, sin `N` no se escriben `T` ni
        ''' `B`, y sin `T` no se escribe `B`.
        ''' </summary>
        Private Function CanalesVivos(p As PielConPesosCompilada, entrada As Buffer,
                                      salida As Buffer) As Integer
            Dim cp = p.Posiciones AndAlso entrada.Datos IsNot Nothing AndAlso salida.Datos IsNot Nothing
            If Not cp Then Return 0
            Dim cn = p.Normales AndAlso entrada.Normales IsNot Nothing AndAlso
                     salida.Normales IsNot Nothing
            If Not cn Then Return 1
            Dim ct = p.Tangentes AndAlso entrada.Tangentes IsNot Nothing AndAlso
                     salida.Tangentes IsNot Nothing
            If Not ct Then Return 2
            Dim cb = p.Bitangentes AndAlso entrada.Bitangentes IsNot Nothing AndAlso
                     salida.Bitangentes IsNot Nothing
            Return If(cb, 4, 3)
        End Function

        ''' <summary>
        ''' El camino de MEZCLA LINEAL — `0x14191E440` y sus once hermanos.
        ''' <para>```
        ''' n = 0        → el vértice no se toca                        ' 0x14191E5EA jbe
        ''' n = 1        → M = comp[hueso]      ⛔ SIN el peso           ' 0x14191E642
        ''' 2 &lt;= n &lt;= 8 → M = Σ (peso/255)·comp[hueso], izq. a der.   ' 0x14191E66E-0x14191F66B
        ''' n &gt; 8       → M = CERO                                     ' 0x14191E62B cmp 7 / ja
        ''' ```</para>
        ''' </summary>
        Friend Sub DeformarLineal(p As PielConPesosCompilada, entrada As Buffer, salida As Buffer,
                                  comp As Mat4())
            Dim canales = CanalesVivos(p, entrada, salida)
            If canales = 0 Then Return                                   ' 0x14190A83C test/je
            Dim cuantos = p.VerticeFinal - p.VerticeInicial + 1          ' 0x14190A664/6E
            For i = 0 To cuantos - 1
                Dim v = p.VerticeInicial + i                             ' punteros corridos, 0x14190A695
                If v < 0 OrElse v >= entrada.Cuenta OrElse v >= salida.Cuenta Then Continue For
                If i + 1 >= p.ComienzoPorVertice.Length Then Exit For
                Dim ini = p.ComienzoPorVertice(i)
                Dim fin = p.ComienzoPorVertice(i + 1)
                If fin <= ini Then Continue For                          ' 0x14191E5EA: NO se escribe
                Dim m = MezclaLineal(p, comp, ini, fin)

                salida.SetVertice(v, Operadores.TransformarPunto(entrada.Vertice(v), m))
                If canales >= 2 Then
                    salida.SetNormal(v, Operadores.TransformarDireccion(entrada.Normal(v), m))
                End If
                If canales >= 3 Then
                    salida.SetTangente(v, Operadores.TransformarDireccion(entrada.Tangente(v), m))
                End If
                If canales >= 4 Then
                    salida.SetBitangente(v, Operadores.TransformarDireccion(entrada.Bitangente(v), m))
                End If
            Next
        End Sub

        ''' <summary>La matriz mezclada de un vértice — las tres ramas de `0x14191E615`-`0x14191F66B`.</summary>
        Private Function MezclaLineal(p As PielConPesosCompilada, comp As Mat4(),
                                      ini As Integer, fin As Integer) As Mat4
            Dim n = fin - ini
            If n > MaxInfluenciasLineal Then Return Nothing              ' 0x14191E62E ja → acumuladores en cero
            If n = 1 Then                                          ' 0x14191E642
                ' ⛔ 0x14191E642: se lee el hueso, se SALTEA el peso y la matriz entra tal cual
                Dim b1 = HuesoDe(p, ini)
                If b1 < 0 OrElse b1 >= comp.Length Then Return Nothing
                Return comp(b1)
            End If

            Dim m As Mat4
            For k = ini To fin - 1
                Dim b = HuesoDe(p, k)
                If b < 0 OrElse b >= comp.Length Then Continue For
                Dim w = Vector128.Create(CSng(p.Pesos(k)) * EscalaDePeso)   ' cvtdq2ps + mulps 1/255
                m.F0 = Vector128.Add(m.F0, Vector128.Multiply(comp(b).F0, w))
                m.F1 = Vector128.Add(m.F1, Vector128.Multiply(comp(b).F1, w))
                m.F2 = Vector128.Add(m.F2, Vector128.Multiply(comp(b).F2, w))
                m.F3 = Vector128.Add(m.F3, Vector128.Multiply(comp(b).F3, w))
            Next
            Return m
        End Function

        ''' <summary>
        ''' El camino de CUATERNIÓN DUAL — `0x14190FBF0` y sus once hermanos.
        ''' <para>```
        ''' n = 1        → (q, d) = dq[hueso]     ⛔ SIN peso y SIN normalizar  ' 0x14191009A
        ''' 2 &lt;= n &lt;= 4 → qb = Σ w·real ; db = Σ w·dual ; s = rsqrtN(hsum4(qb·qb))
        '''                (q, d) = (qb·s, db·s)                                ' 0x141910056-0x141910094
        ''' n &gt; 4       → (q, d) = CERO                                        ' 0x14190FD84 jne
        ''' ```</para>
        ''' </summary>
        Friend Sub DeformarDual(p As PielConPesosCompilada, entrada As Buffer, salida As Buffer,
                                dq As CuaternionDual())
            Dim canales = CanalesVivos(p, entrada, salida)
            If canales = 0 Then Return
            Dim cuantos = p.VerticeFinal - p.VerticeInicial + 1
            For i = 0 To cuantos - 1
                Dim v = p.VerticeInicial + i
                If v < 0 OrElse v >= entrada.Cuenta OrElse v >= salida.Cuenta Then Continue For
                If i + 1 >= p.ComienzoPorVertice.Length Then Exit For
                Dim ini = p.ComienzoPorVertice(i)
                Dim fin = p.ComienzoPorVertice(i + 1)
                If fin <= ini Then Continue For
                Dim mezcla = MezclaDual(p, dq, ini, fin)
                Dim q = mezcla.Real, d = mezcla.Dual

                salida.SetVertice(v, PuntoPorDual(entrada.Vertice(v), q, d))
                If canales >= 2 Then salida.SetNormal(v, DireccionPorDual(entrada.Normal(v), q))
                If canales >= 3 Then salida.SetTangente(v, DireccionPorDual(entrada.Tangente(v), q))
                If canales >= 4 Then salida.SetBitangente(v, DireccionPorDual(entrada.Bitangente(v), q))
            Next
        End Sub

        ''' <summary>El dual mezclado de un vértice — `0x14190FD66`-`0x141910098`.</summary>
        Private Function MezclaDual(p As PielConPesosCompilada, dq As CuaternionDual(),
                                    ini As Integer, fin As Integer) As CuaternionDual
            Dim r As CuaternionDual
            Dim n = fin - ini
            If n > MaxInfluenciasDual Then Return r                      ' 0x14190FD84: los dos en cero
            If n = 1 Then                                          ' 0x14191009A
                Dim b1 = HuesoDe(p, ini)
                If b1 < 0 OrElse b1 >= dq.Length Then Return r
                Return dq(b1)                                            ' 0x14191009A: crudo
            End If

            For k = ini To fin - 1
                Dim b = HuesoDe(p, k)
                If b < 0 OrElse b >= dq.Length Then Continue For
                Dim w = Vector128.Create(CSng(p.Pesos(k)) * EscalaDePeso)
                r.Real = Vector128.Add(r.Real, Vector128.Multiply(dq(b).Real, w))
                r.Dual = Vector128.Add(r.Dual, Vector128.Multiply(dq(b).Dual, w))
            Next

            ' ⛔ Las DOS partes se escalan con el MISMO factor, el de la norma de la REAL
            ' (`0x14191008C` y `0x141910094` con el mismo `xmm3`). Nada de normalizar la dual aparte.
            Dim s = Simd.RsqrtNewtonConGuarda(Hsum4(Vector128.Multiply(r.Real, r.Real)))
            r.Real = Vector128.Multiply(r.Real, s)
            r.Dual = Vector128.Multiply(r.Dual, s)
            Return r
        End Function

        ''' <summary>
        ''' `v' = v + 2·( q_v × (q_v × v + q_w·v) ) + 2·( q_v × d_v − d_w·q_v + q_w·d_v )` —
        ''' `0x1419100B4`-`0x141910174`, con el `{2,2,2,2}` de `0x1424DAC30`.
        ''' </summary>
        Private Function PuntoPorDual(v As Vector128(Of Single), q As Vector128(Of Single),
                                      d As Vector128(Of Single)) As Vector128(Of Single)
            Dim dos = Vector128.Create(2.0F)
            Dim rot = Vector128.Add(v,
                Vector128.Multiply(dos, Polar.Cruz(q, Giro(v, q))))          ' 0x14190CF55-0x141910160
            Dim tr = Vector128.Subtract(Polar.Cruz(q, d),
                                        Vector128.Multiply(Simd.BcastW(d), q))   ' 0x141910164
            tr = Vector128.Add(tr, Vector128.Multiply(Simd.BcastW(q), d))        ' 0x141910167
            Return Vector128.Add(Vector128.Multiply(tr, dos), rot)               ' 0x14191016A/71
        End Function

        ''' <summary>`x' = x + 2·( q_v × (q_v × x + q_w·x) )` — sólo la rotación
        ''' (`0x14190CEE6`-`0x14190D00B`). La parte dual NO entra en las direcciones.</summary>
        Private Function DireccionPorDual(x As Vector128(Of Single),
                                          q As Vector128(Of Single)) As Vector128(Of Single)
            Return Vector128.Add(x, Vector128.Multiply(Vector128.Create(2.0F),
                                                       Polar.Cruz(q, Giro(x, q))))
        End Function

        ''' <summary>`q_v × x + q_w·x` — el término de adentro del giro (`0x1419100DF`-`0x14191011B`).</summary>
        Private Function Giro(x As Vector128(Of Single), q As Vector128(Of Single)) As Vector128(Of Single)
            Return Vector128.Add(Polar.Cruz(q, x), Vector128.Multiply(Simd.BcastW(q), x))
        End Function

        ''' <summary>El hueso de la influencia `k`, con la guarda de rango del arreglo.</summary>
        Private Function HuesoDe(p As PielConPesosCompilada, k As Integer) As Integer
            If k < 0 OrElse k >= p.Huesos.Length OrElse k >= p.Pesos.Length Then Return -1
            Return p.Huesos(k)
        End Function

        ''' <summary>
        ''' La suma horizontal de las CUATRO lanes, con los dos `shufps` del motor: `0x4E` cruza las
        ''' mitades y `0xB1` cruza dentro de cada mitad — `((p0+p2) + (p1+p3))`.
        ''' <para>⛔ No es <see cref="Simd.Dot3"/> ni su orden de sumas. Sitios: el ancla de signo de
        ''' la fase 2 (`0x141909DFF`/`0x141909E10`) y la norma del dual mezclado
        ''' (`0x14191003E`/`0x14191004F`).</para>
        ''' </summary>
        Friend Function Hsum4(p As Vector128(Of Single)) As Vector128(Of Single)
            ' ⭐ LA LEY VIVE EN `Simd.Hsum4`. Acá quedó el nombre local porque es el que usan los
            ' dos sitios de este archivo; el cuerpo estaba DUPLICADO con `Formas` y con el terreno.
            Return Simd.Hsum4(p)
        End Function

        ''' <summary>El `xorps` contra `{0x80000000 ×4}` de `0x141909E0A` — voltea las CUATRO
        ''' componentes, la `w` incluida.</summary>
        Private Function Negar(q As Vector128(Of Single)) As Vector128(Of Single)
            Return Vector128.Xor(q.AsUInt32(), Vector128.Create(2147483648UI)).AsSingle()
        End Function

    End Module

End Namespace

#End If
