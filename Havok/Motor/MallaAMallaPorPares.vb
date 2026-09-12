Option Strict On
Option Explicit On

Imports System.Runtime.Intrinsics
Imports FO4_Base_Library.Havok.Canon.Objects

' =================================================================================================
' `hclMeshMeshDeformOperator` — type **5**, kernel `0x1419529F0` («TtMesh Mesh Deform»).
'
' ⛔⛔ NO es la familia `hclObjectSpaceMeshMeshDeform*` (30-33) ni `hclBoneSpaceMeshMeshDeform*`
' (26-29), que transcribe `DeformarMallaAMalla.vb`. Ésta es una clase APARTE: cuelga de
' `hclOperator` directo, mide `0x68` y tiene sus propios campos. Sus dos anclas de identidad:
' el ctor `0x141952860` nombra la instancia **«Mesh Mesh Deform»** (`0x142675D50`) y el kernel del
' type 5 se perfila **«TtMesh Mesh Deform»** (`0x14270B4F0`).
'
' ⚠️ CERO apariciones en el corpus vanilla. Va igual: la lista de operadores es CERRADA y sale de
' la reflexión, igual que `hclMeshBoneDeformOperator` (type 16), que tampoco aparece.
'
' ⛔ Cómo apareció el hueco: `HavokLayoutGate` decía «operadores sin ejecutar = 5» y un comentario
' mío afirmaba que los cinco eran clases BASE. Cuatro lo son; ésta NO —padre `hclOperator`, cero
' hijos—, y `Cadena.vb` YA tenía su `type` en la tabla del despachador. El `type` estaba leído
' hacía tandas y el operador nunca se cableó.
'
' ---------------------------------------------------------------------------------------------
' EL LAYOUT, de la reflexión
'
'     hclMeshMeshDeformOperator : hclOperator, tam 0x68
'       +0x20  inputTrianglesSubset          array uint16
'       +0x30  triangleVertexPairs           array struct (0x30 B c/u)
'       +0x40  triangleVertexStartForVertex  array uint16
'       +0x50  inputBufferIdx                uint32
'       +0x54  outputBufferIdx               uint32
'       +0x58  startVertex                   uint16
'       +0x5A  endVertex                     uint16
'       +0x5C  scaleNormalBehaviour          enum uint32
'       +0x60  deformNormals                 bool
'       +0x61  partialDeform                 bool
'
'     hclMeshMeshDeformOperatorTriangleVertexPair, tam 0x30
'       +0x00  localPosition vector4 · +0x10 localNormal vector4
'       +0x20  triangleIndex uint16  · +0x24 weight real
'
' ⛔ LA LLAMADA, del caso `0x1418C5EEF` del despachador — con DOBLE indirección por la Ranura:
'
'     kernel( op,  buffers[ buffers[inputBufferIdx][+0x100] ],
'                  buffers[ buffers[outputBufferIdx][+0x100] ] )
'
' ---------------------------------------------------------------------------------------------
' ⭐⭐ FASE 1 — LOS MARCOS DE TRIÁNGULO, uno por triángulo, 64 B cada uno
'
' `0x141952AB8`-`0x141952AC4` reserva `((n << 6) + 0x7F) & ~0x7F` — un `Mat4` por triángulo. Y
' `0x141952A4C`-`0x141952A58`: si `inputTrianglesSubset` está VACÍO, `n` es
' `entrada.numTriangulos`, o sea **todos**.
'
' Los tres constructores, que el motor elige por `scaleNormalBehaviour` (`0x141952B19`-`0x141952B30`):
'
'     0      → `0x1419533E0`   F2 = n̂ NORMALIZADA, con guarda `cmpleps(|n|², 0)` + `andnps`
'     2      → `0x1419536F0`   F2 = n · rcpps(|n|²)  ⛔ SIN guarda: un degenerado da +inf
'     resto  → `0x141953140`   F2 = n CRUDA, escala con el ÁREA
'
' Lo común a los tres:
'
'     c  = ((p0 + p1) + p2) · 1/3         ' 0x142F3C660, la misma constante del type 16 y del 17
'     a  = p0 − c ; b = p1 − c
'     n  = a × b                          ' dos `shufps 0xC9`
'     F0 = a con w = 0 ; F1 = b con w = 0 ; F2 = <según behaviour> con w = 0
'     F3 = c con w = 1                    ' unpckhps con {1,1,1,1} de 0x142F3C560 + shufps 0xC4
'
' ⛔ El vértice se lee con paso **16 fijo** (`[rsi + idx*16]`), y los índices del triángulo son
' tres `uint16` seguidos (`t·3` entradas de 2 B).
'
' ⭐⭐ FASE 2 — LOS MARCOS PASAN AL ESPACIO DE SALIDA (`0x141952C10`-`0x141952CCD`)
'
'     M = Componer( entrada.AEspacioDeSimulacion , salida.DesdeEspacioDeSimulacion )
'     marco[t] = Componer( marco[t] , M )
'
' o sea las filas 0-2 como DIRECCIÓN y la 3 con `TransformarPunto` — la traslación entra enseguida
' del término en `x` (`0x141952CB7 addps xmm1, xmm9` antes de los términos en `y` y `z`).
'
' ⭐⭐ FASE 3 — EL DEFORM (`0x141953D60` / `0x1419539D0` / `0x141953F70` / `0x141953C20`)
'
' El motor despacha cuatro kernels por `deformNormals` × «layout simple» (`0x141952DEF`-`0x141952E6F`).
' ✅ MEDIDO que los cuatro son la MISMA ley: multiconjunto de instrucciones aritméticas idéntico
' salvo **un `xorps` de más** en las variantes de layout general, y los `xorps` son todos
' `xorps xmmN, xmmN` (poner un registro en cero), no cambios de signo. Lo que cambia es cómo se
' guarda el elemento, y eso lo cubre el stride del `Buffer` — igual que en `CopiarVertices`.
'
'     por cada vértice v de [startVertex, endVertex]:
'         ini = startForVertex[v] ; fin = startForVertex[v+1]
'         si fin − ini <= 0 → NO SE ESCRIBE NADA (ni posición ni normal)   ' 0x141953DFC jle
'         accP = 0 ; accN = 0
'         por cada par p de [ini, fin):
'             marco = marcos[p.triangleIndex]
'             peso  = p.weight difundido a las 4 lanes
'             accP += (((lp.x·F0 + F3) + lp.y·F1) + lp.z·F2) · peso
'             accN += ((ln.y·F1 + ln.x·F0) + ln.z·F2) · peso      ' sin F3: es DIRECCIÓN
'         salida.pos[v] = accP
'         si deformNormals: salida.nrm[v] = accN · guarda(rsqrtps(|accN|²))   ' 0x141953EEC-EFA
'
' ⛔ El peso se difunde con `andps` contra `{0xFFFFFFFF,0,0,0}` (`0x14270B3C0`) y DOS `orps` con
' shuffles `0x4E` y `0xB1` (`0x141953E52`-`0x141953E6E`). Es un broadcast por BITS: funciona
' porque el `andps` dejó las otras tres lanes en `+0.0` exacto. El resultado es idéntico a
' difundir la lane 0, y así se transcribe.
'
' ⛔ Un vértice sin pares NO se toca: no se pone en cero, se deja como estaba. Es la diferencia
' entre «escribir 0» y «no escribir», y se ve en el buffer de salida.
' =================================================================================================


Namespace Havok.Motor

    ''' <summary>
    ''' `hclMeshMeshDeformOperatorTriangleVertexPair` — 0x30 B.
    ''' <para>`localPosition` @0x00 · `localNormal` @0x10 · `triangleIndex` @0x20 · `weight` @0x24.</para>
    ''' </summary>
    Friend Structure ParDeVerticeDeTriangulo
        Friend PosicionLocal As Vector128(Of Single)
        Friend NormalLocal As Vector128(Of Single)
        Friend Triangulo As Integer
        Friend Peso As Single
    End Structure

    ''' <summary>
    ''' `hclMeshMeshDeformOperator` (type 5) ya compilado: los arreglos del archivo, resueltos.
    ''' </summary>
    Friend NotInheritable Class MallaAMallaPorParesCompilada

        ''' <summary>`inputTrianglesSubset` (+0x20). **Vacío significa TODOS** los triángulos del
        ''' buffer de entrada (`0x141952A4C`-`0x141952A58`).</summary>
        Friend ReadOnly SubconjuntoDeTriangulos As Integer()

        ''' <summary>`triangleVertexPairs` (+0x30).</summary>
        Friend ReadOnly Pares As ParDeVerticeDeTriangulo()

        ''' <summary>`triangleVertexStartForVertex` (+0x40): el vértice `v` usa los pares
        ''' `[start(v), start(v+1))`. Tiene una entrada MÁS que vértices.</summary>
        Friend ReadOnly InicioPorVertice As Integer()

        Friend ReadOnly BufferDeEntrada As Integer
        Friend ReadOnly BufferDeSalida As Integer
        Friend ReadOnly VerticeInicial As Integer
        Friend ReadOnly VerticeFinal As Integer

        ''' <summary>`scaleNormalBehaviour` (+0x5C). Ver <see cref="MallaAMallaPorPares.Marcos"/>.</summary>
        Friend ReadOnly EscalaDeNormal As Integer

        Friend ReadOnly DeformarNormales As Boolean

        ''' <summary>`partialDeform` (+0x61). ⚠️ El kernel del type 5 **no lo lee**: el rango ya
        ''' viene dado por `startVertex`/`endVertex`. Se transcribe porque está en la clase.</summary>
        Friend ReadOnly DeformParcial As Boolean

        Friend Sub New(subconjunto As Integer(), pares As ParDeVerticeDeTriangulo(),
                       inicioPorVertice As Integer(), bufferDeEntrada As Integer,
                       bufferDeSalida As Integer, verticeInicial As Integer,
                       verticeFinal As Integer, escalaDeNormal As Integer,
                       deformarNormales As Boolean, deformParcial As Boolean)
            Me.SubconjuntoDeTriangulos = If(subconjunto, Array.Empty(Of Integer)())
            Me.Pares = If(pares, Array.Empty(Of ParDeVerticeDeTriangulo)())
            Me.InicioPorVertice = If(inicioPorVertice, Array.Empty(Of Integer)())
            Me.BufferDeEntrada = bufferDeEntrada
            Me.BufferDeSalida = bufferDeSalida
            Me.VerticeInicial = verticeInicial
            Me.VerticeFinal = verticeFinal
            Me.EscalaDeNormal = escalaDeNormal
            Me.DeformarNormales = deformarNormales
            Me.DeformParcial = deformParcial
        End Sub
    End Class

    ' =============================================================================================

    ''' <summary>Lo que el metodo `+0x38` (`0x141952920`) declara: que buffer lee, cual escribe,
    ''' cuantos vertices y si toca el canal de normales.</summary>
    Friend Structure UsoDeBuffersDelDeform
        Friend BufferLeido As Integer
        Friend BufferEscrito As Integer
        Friend VerticesEscritos As Integer
        Friend EscribeNormales As Boolean
    End Structure

    Friend Module MallaAMallaPorPares

        ''' <summary>`scaleNormalBehaviour = 0` — la normal del marco sale UNITARIA.</summary>
        Friend Const NormalUnitaria As Integer = 0

        ''' <summary>`scaleNormalBehaviour = 2` — la normal se escala por la INVERSA del área.</summary>
        Friend Const NormalInversa As Integer = 2

        ''' <summary>
        ''' Los marcos de triángulo — `0x1419533E0` (behaviour 0), `0x1419536F0` (2) y
        ''' `0x141953140` (el resto). Los tres comparten todo menos la fila 2.
        ''' <para>```
        ''' c  = ((p0 + p1) + p2) · 1/3        ' 0x142F3C660
        ''' a  = p0 − c ; b = p1 − c ; n = a × b
        ''' F0 = a|w=0 ; F1 = b|w=0 ; F3 = c|w=1
        ''' F2 = 0 → n · guarda(rsqrt(|n|²))   ' 0x141953519-26, cmpleps + andnps
        '''      2 → n · rcpps(|n|²)           ' 0x141953819-1C, ⛔ SIN guarda
        '''      _ → n                         ' 0x14195324B, cruda
        ''' ```</para>
        ''' <para>⛔ El `|n|²` se reduce con `((y + x) + z)`, la asociación de
        ''' <see cref="Simd.Dot3"/> (`0x141953513`/`16`).</para>
        ''' </summary>
        ''' <summary>
        ''' La SALIDA TEMPRANA del kernel — `0x141952A6A test ecx, ecx` + `je 0x141952F2C`.
        ''' <para>⛔ `ecx` es `entrada.numTriangles` (`[r13+0x30]`, leido en `0x141952A50`) y el
        ''' corte es ANTES del scratch, de los marcos y del deform. Con el buffer de entrada
        ''' declarando cero triangulos el operador **no toca la salida**, tenga o no
        ''' subconjunto: la sustitucion del subconjunto vacio (`0x141952A58`) pasa antes pero no
        ''' cambia esta condicion.</para>
        ''' </summary>
        Friend Function HayQueDeformar(entrada As Buffer) As Boolean
            Return entrada IsNot Nothing AndAlso entrada.NumTriangulos <> 0
        End Function

        ''' <summary>
        ''' El metodo de vtable `+0x20` — `0x1419528D0`. Un predicado de TAMAÑO:
        ''' <para><code>
        ''' n = op.inputTrianglesSubset.size            ' [op+0x28], 0x1419528D0
        ''' si n &gt; 0x400                        → False  ' 0x1419528D4/DB
        ''' buf = buffers[op.inputBufferIdx]            ' [op+0x50], 0x1419528DD-E5
        ''' si buf.numVertices &gt; 0x400 (sin signo) → False  ' 0x1419528E9/F1
        ''' si n &lt;&gt; 0                         → True   ' 0x1419528F3/F6/09
        ''' devuelve buf.numTriangles &lt;= 0x400          ' 0x1419528F8/0x141952900 setbe
        ''' </code></para>
        ''' <para>⛔⛔ EL CAMINO DE EJECUCION NO LO CONSULTA, y esto esta MEDIDO, no supuesto: el
        ''' despachador `0x1418C5DE0` salta por su tabla de RVAs y para el type 5 cae en
        ''' `0x1418C5F24 jmp 0x1419529F0`, un salto DIRECTO al kernel sin ninguna llamada por
        ''' vtable en el medio; y ese `jmp` es el UNICO xref al kernel en todo el `.text`. O sea
        ''' que en FO4 el operador corre sin que nadie pregunte esto.</para>
        ''' <para>⛔ Se transcribe igual porque es de la CLASE, no del kernel — la lista de
        ''' metodos es cerrada y sale de la vtable `0x142701ED8`. El `0x400` no es un umbral mio:
        ''' es el inmediato que compara `0x1419528D4`.</para>
        ''' </summary>
        Friend Function CabeEnUnLote(op As MallaAMallaPorParesCompilada, entrada As Buffer) As Boolean
            If op Is Nothing OrElse entrada Is Nothing Then Return False
            Dim n = op.SubconjuntoDeTriangulos.Length
            If n > &H400 Then Return False                             ' 0x1419528D4/DB
            If CUInt(entrada.Cuenta) > &H400UI Then Return False       ' 0x1419528E9/F1, `ja` sin signo
            If n <> 0 Then Return True                                 ' 0x1419528F3/F6 -> 0x141952909
            Return entrada.NumTriangulos <= &H400                      ' 0x1419528F8 + setbe
        End Function

        ''' <summary>
        ''' El metodo de vtable `+0x38` — `0x141952920`: QUE buffers usa el operador, y como.
        ''' <para>Es lo que alimenta el `hclClothStateBufferAccess` / `usedBuffers` del estado: el
        ''' motor lo pregunta para saber que puede correr en paralelo y que no.</para>
        ''' <para><code>
        ''' lee   inputBufferIdx                        ' 0x141952940 (0x141A0D7A0)
        ''' lee   inputBufferIdx, canal 0               ' 0x14195294E (0x141A0D5E0)
        ''' cuantos = endVertex - startVertex + 1       ' 0x141952953-62
        ''' por cada i LOCAL con pares:                 ' 0x141952970-84
        '''     escribe outputBufferIdx, vertice i, canal 0        ' 0x141952992 (0x141A0D750)
        '''     si deformNormals: idem canal 1                     ' 0x1419529AC
        ''' </code></para>
        ''' <para>⛔ El vertice que registra es el LOCAL (`r8d = ebx`, `0x14195298C`), no el
        ''' global — igual que el indice con el que se lee `startForVertex`.</para>
        ''' <para>⛔ Y el «sin pares no se toca» aparece tambien aca (`0x141952984 jle`): un
        ''' vertice sin pares no figura como escrito. Es la misma ley vista desde el registro.</para>
        ''' </summary>
        Friend Function UsoDeBuffers(op As MallaAMallaPorParesCompilada) As UsoDeBuffersDelDeform
            Dim r As UsoDeBuffersDelDeform
            r.BufferLeido = -1
            r.BufferEscrito = -1
            r.VerticesEscritos = 0
            r.EscribeNormales = False
            If op Is Nothing Then Return r
            r.BufferLeido = op.BufferDeEntrada                         ' 0x14195293A
            Dim cuantos = op.VerticeFinal - op.VerticeInicial + 1      ' 0x141952953-62
            ' ⛔ `je` a la salida con la cuenta en CERO (0x141952965), antes del bucle.
            If cuantos = 0 Then Return r
            For i = 0 To cuantos - 1
                If i + 1 >= op.InicioPorVertice.Length Then Exit For
                If op.InicioPorVertice(i + 1) - op.InicioPorVertice(i) <= 0 Then Continue For  ' 0x141952984
                r.BufferEscrito = op.BufferDeSalida                    ' 0x141952986
                r.VerticesEscritos += 1
                If op.DeformarNormales Then r.EscribeNormales = True   ' 0x141952997/9B
            Next
            Return r
        End Function

        Friend Function Marcos(op As MallaAMallaPorParesCompilada, entrada As Buffer) As Mat4()
            Dim cuantos = op.SubconjuntoDeTriangulos.Length
            If cuantos = 0 Then cuantos = entrada.NumTriangulos       ' 0x141952A58
            ' ⛔ VACIO, NO «UNO NULO». `Dim r(cuantos - 1)` con `cuantos = 0` declara `r(0)`, que
            ' en VB es un arreglo de UN elemento — y ese Mat4 en cero llegaba a `Deformar`, que
            ' lo aceptaba (`0 < marcos.Length`) y escribia CERO en el vertice. El motor no
            ' escribe nada (0x141952A6C).
            If cuantos = 0 Then Return Array.Empty(Of Mat4)()
            Dim r(cuantos - 1) As Mat4

            Dim unTercio = Vector128.Create(0.333333343F)             ' 0x142F3C660
            For k = 0 To cuantos - 1
                ' ⛔ El subconjunto, cuando existe, dice CUÁL triángulo; si no, es el k-ésimo.
                Dim t = If(op.SubconjuntoDeTriangulos.Length = 0, k, op.SubconjuntoDeTriangulos(k))
                Dim t0 = t * 3
                If entrada.IndicesDeTriangulo Is Nothing OrElse
                   t0 + 2 >= entrada.IndicesDeTriangulo.Length Then Continue For
                ' ⛔⛔ PASO 16 FIJO, NO EL STRIDE DEL BUFFER. `0x141953495` hace
                ' `movups xmm5, [rsi + rcx*8]` con `rcx = idx*2`, o sea `rsi + idx*16`: el 16 está
                ' HORNEADO en el kernel. Leer con `Buffer.Vertice` (que va por `StrideBytes`) coincide
                ' sólo mientras el buffer tenga stride 16, y eso no es una ley: es una coincidencia.
                Dim p0 = VerticeDePaso16(entrada, CInt(entrada.IndicesDeTriangulo(t0)))
                Dim p1 = VerticeDePaso16(entrada, CInt(entrada.IndicesDeTriangulo(t0 + 1)))
                Dim p2 = VerticeDePaso16(entrada, CInt(entrada.IndicesDeTriangulo(t0 + 2)))

                Dim c = Vector128.Multiply(Vector128.Add(Vector128.Add(p0, p1), p2), unTercio)
                Dim a = Vector128.Subtract(p0, c)
                Dim b = Vector128.Subtract(p1, c)
                Dim n = Polar.Cruz(a, b)

                Dim f2 As Vector128(Of Single)
                Select Case op.EscalaDeNormal
                    Case NormalUnitaria
                        ' 0x141953519 rsqrtps + 0x14195351F cmpleps + 0x141953523 andnps
                        f2 = Vector128.Multiply(n, Simd.RsqrtConGuarda(Simd.Dot3(n, n)))
                    Case NormalInversa
                        ' ⛔ 0x141953819 rcpps CRUDO: no hay `cmpleps`/`andnps` en esta rama.
                        f2 = Vector128.Multiply(n, Simd.RcpCrudo(Simd.Dot3(n, n)))
                    Case Else
                        f2 = n                                        ' 0x14195324B, cruda
                End Select

                Dim m As Mat4
                m.F0 = a.WithElement(Simd.LaneW, 0.0F)                ' pslldq/psrldq
                m.F1 = b.WithElement(Simd.LaneW, 0.0F)
                m.F2 = f2.WithElement(Simd.LaneW, 0.0F)
                m.F3 = c.WithElement(Simd.LaneW, 1.0F)                ' unpckhps con 0x142F3C560
                r(k) = m
            Next
            Return r
        End Function

        ''' <summary>
        ''' El vértice `i` con **paso 16 fijo**, que es lo que hornea el kernel de los marcos
        ''' (`0x141953495`: `movups xmm5, [rsi + rcx*8]` con `rcx = idx*2`).
        ''' <para>⛔ No es <see cref="Buffer.Vertice"/>: aquel va por `StrideBytes` y coincide
        ''' sólo mientras el buffer tenga 16. Acá el 16 es la ley.</para>
        ''' </summary>
        Friend Function VerticeDePaso16(buf As Buffer, i As Integer) As Vector128(Of Single)
            Dim off = i * 4
            If buf Is Nothing OrElse buf.Datos Is Nothing OrElse off + 2 >= buf.Datos.Length Then
                Return Vector128(Of Single).Zero
            End If
            Return Vector128.Create(buf.Datos(off), buf.Datos(off + 1), buf.Datos(off + 2), 0.0F)
        End Function

        ''' <summary>
        ''' `marco[t] = Componer(marco[t], M)` con
        ''' `M = Componer(entrada.AEspacioDeSimulacion, salida.DesdeEspacioDeSimulacion)` —
        ''' `0x141952B35`-`0x141952CCD`.
        ''' </summary>
        Friend Sub AEspacioDeSalida(marcos As Mat4(), entrada As Buffer, salida As Buffer)
            If marcos Is Nothing Then Return
            Dim m = Operadores.MatrizEntreBuffers(entrada, salida)
            For k = 0 To marcos.Length - 1
                marcos(k) = Operadores.Componer(marcos(k), m)
            Next
        End Sub

        ''' <summary>
        ''' El deform — la ley que comparten los cuatro kernels de `0x141953D60`, `0x1419539D0`,
        ''' `0x141953F70` y `0x141953C20`.
        ''' <para>⛔ Un vértice SIN pares no se toca (`0x141953DFC jle`): no se pone en cero.</para>
        ''' </summary>
        Friend Sub Deformar(op As MallaAMallaPorParesCompilada, marcos As Mat4(), salida As Buffer)
            If op Is Nothing OrElse marcos Is Nothing OrElse salida Is Nothing Then Return
            Dim cuantos = op.VerticeFinal - op.VerticeInicial + 1     ' 0x141952A5A-67
            For i = 0 To cuantos - 1
                Dim v = op.VerticeInicial + i
                If v < 0 OrElse v >= salida.Cuenta Then Continue For
                If i + 1 >= op.InicioPorVertice.Length Then Exit For
                Dim ini = op.InicioPorVertice(i)
                Dim fin = op.InicioPorVertice(i + 1)
                If fin - ini <= 0 Then Continue For                   ' 0x141953DFC, NO escribe

                Dim accP = Vector128(Of Single).Zero
                Dim accN = Vector128(Of Single).Zero
                For q = ini To fin - 1
                    If q < 0 OrElse q >= op.Pares.Length Then Exit For
                    Dim par = op.Pares(q)
                    If par.Triangulo < 0 OrElse par.Triangulo >= marcos.Length Then Continue For
                    Dim marco = marcos(par.Triangulo)
                    ' ⛔ El peso se difunde por BITS (`andps` + dos `orps`, 0x141953E4B-6E); con las
                    ' otras lanes en `+0.0` exacto eso ES un broadcast de la lane 0.
                    Dim peso = Vector128.Create(par.Peso)
                    accP = Vector128.Add(accP,
                        Vector128.Multiply(Operadores.TransformarPunto(par.PosicionLocal, marco), peso))
                    accN = Vector128.Add(accN,
                        Vector128.Multiply(Operadores.TransformarDireccion(par.NormalLocal, marco), peso))
                Next

                salida.SetVertice(v, accP)                            ' 0x141953ECC
                If op.DeformarNormales AndAlso salida.Normales IsNot Nothing Then
                    ' 0x141953EEC rsqrtps + 0x141953EF2 cmpleps + 0x141953EF7 andnps
                    salida.SetNormal(v, Vector128.Multiply(accN, Simd.RsqrtConGuarda(Simd.Dot3(accN, accN))))
                End If
            Next
        End Sub

    End Module

End Namespace

