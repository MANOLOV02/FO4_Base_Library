Imports SN = System.Numerics
Imports OpenTK.Mathematics

''' <summary>⭐ El kernel del SKINNING DE CPU: de matriz per-vertice a posicion, normal, tangente y
''' bitangente de MUNDO. Es el bucle que domina un frame de animacion con skinning por CPU (medido: 9,3 ms
''' de un frame de ~20 sobre 130.500 vertices, contra 1,3 ms de las cuatro subidas de VBO).
'''
''' <para>⛔⛔ LA LEY SE ESCRIBE UNA SOLA VEZ… PERO ESTA TRANSCRIPTA CUATRO VECES, y el
''' <see cref="SelfTest"/> compara las cuatro entre si: <see cref="TransformarDirecto"/> (produccion, AoS
''' sin staging), <see cref="UnVertice"/> (produccion, camino sparse del upload, un vertice por vez),
''' <see cref="BloqueEscalar"/> (referencia) y <see cref="BloqueVectorial"/>. Si tocas una, tocas las cuatro.
''' <para>⛔⛔ PERO ESO ES UN GATE DE CONSISTENCIA, NO DE CORRECCION, y confundirlos costo caro: comparar las
''' cuatro entre si prueba que nadie edito una sola, y NADA MAS. Un revisor lo demostro negando la matriz de
''' normales en las cuatro a la vez —el modelo iluminado del lado equivocado— y el gate quedo VERDE. Por eso
''' existe <see cref="OraculoDeLaLey"/>, que no conoce los cofactores ni el determinante ni el corte, y
''' verifica el INVARIANTE con la sola multiplicacion matriz-por-vector. Tres controles negativos que antes
''' pasaban —normal negada, corte subido a 1e-3, ley transpuesta— hoy fallan ahi.</para>
''' <para>La vectorial solo puede dar bit a bit lo mismo si cada lane
''' del vector ejecuta EXACTAMENTE la misma secuencia de operaciones que el escalar, en el mismo orden. Por
''' eso aca NO se llama a <c>Matrix3.Inverted()</c> ni a <c>Vector3.Normalize()</c>: son algoritmos ajenos,
''' con su propio orden y quiza con ramas, y entonces el gate estaria comparando dos LEYES en vez de dos
''' implementaciones de una. Ver <see cref="SelfTest"/>.</para>
''' <para>⚠️ "Cuatro transcripciones" vale para los COFACTORES. La rotacion y la normalizacion estan escritas
''' dos veces, no cuatro: <c>Rotar</c> la comparten los tres caminos escalares y <c>RotarV</c> es la vectorial.
''' O sea que un error en <c>Rotar</c> solo lo caza la comparacion contra la vectorial — y si la maquina no
''' acelera, esa comparacion no corre. Ese hueco lo tapa el oraculo, que no pasa por ninguna de las dos.</para>
'''
''' <para>⭐ LA MATRIZ DE NORMALES SALE POR COFACTORES, no invirtiendo y transponiendo. La transpuesta de la
''' inversa de una 3x3 es exactamente <c>cofactores / determinante</c> —la transposicion se cancela con la
''' del adjunto—, o sea 9 restas de productos, un producto punto y UNA reciproca: sin ramas y sin una
''' division por elemento, que es lo que la hace vectorizable. Medido por el check [cofactores] del arnes contra
''' el <c>Inverted().Transposed()</c> de OpenTK, sobre 24.927 normales reales y posadas del actor canonico:
''' desvio medio 0,0036 grados, peor caso 0,0296.
''' <para>⚠️ NO CONFUNDIR con el numero de [normal-single] (medio 0,000001, peor 0,000017), que contesta otra
''' pregunta —cuanto movio la normal evaluar la inversa en Single en vez de en Double— y es cuatro ordenes de
''' magnitud mas chico. Los dos vivian mezclados en estos docs.</para></para>
'''
''' <para>⛔ SE TRABAJA POR BLOQUES, y no es un detalle de estilo. Vectorizar exige los datos en SoA (todos
''' los M11 juntos, todos los M12 juntos...), y VB no puede reinterpretar un <c>Matrix4()</c> como
''' <c>Single()</c> —no hay Span ni MemoryMarshal—, asi que hay que COPIAR. Copiando la malla entera de una,
''' cada vertice toca 12 lineas de cache distintas y el staging cuesta 9,8 ms: MAS que toda la aritmetica
''' que se queria ahorrar. Con bloques de <see cref="Bloque"/> vertices las secciones entran en cache y el
''' mismo staging baja a 3,1 ms. Es la diferencia entre "no se puede vectorizar" y "no lo hice bien".</para>
''' </summary>
Friend Module FastSkin

    ''' <summary>Vertices por bloque. Con 1024, las 12 secciones de la matriz son 48 KB y entran en cache
    ''' junto con las de posicion y N/T/B. Bajarlo devuelve el problema de cache que el bloqueo resuelve;
    ''' subirlo no compra nada.</summary>
    Friend Const Bloque As Integer = 1024

    ''' <summary>Corte por determinante degenerado.
    ''' <para>⚠️ La CONSTANTE es la misma que usaba <c>NormalMatrixOrIdentity</c>, pero la CANTIDAD a la que
    ''' se aplica no: alla el determinante lo calculaba <c>OpenTK.Matrix3.Determinant</c> y aca sale de la
    ''' expansion por la primera fila. Son sumatorias distintas y discrepan en el borde — medido sobre un
    ''' corpus sintetico de matrices exactamente singulares, 5 de 28 se clasifican distinto. Por eso importa
    ''' que TODOS los caminos del render usen ESTA, y no que la constante coincida.</para>
    ''' <para>En Single, con entradas de orden 1, el ruido de redondeo del propio determinante es ~1e-7, asi
    ''' que este corte es en la practica "det exactamente 0" (una fila nula, o sea escala 0 en un eje). Para
    ''' una matriz con escala uniforme s el determinante es s^3 y el corte pide s &lt; 1e-4.</para></summary>
    Friend Const EpsDet As Single = 0.000000000001F

    ''' <summary>⛔ SOLO PARA MEDIR. Apaga el camino vectorial para poder compararlo contra el escalar EN EL
    ''' MISMO PROCESO: entre corridas esta maquina varia hasta 2x, asi que un A/B entre builds no puede
    ''' atribuir nada. No es un modo de la app — nadie lo escribe fuera del arnes.</summary>
    Friend Property ForzarEscalar As Boolean = False

    ''' <summary>⛔ SOLO PARA MEDIR. False = camino directo (el que gana, el default). True = el
    ''' camino con staging a SoA y vectorizacion, que se conserva como segunda implementacion del
    ''' gate. Nadie lo escribe fuera del arnes.</summary>
    Friend Property UsarStaging As Boolean = False

    Friend ReadOnly Property Acelerado As Boolean
        Get
            Return Not ForzarEscalar AndAlso SN.Vector.IsHardwareAccelerated AndAlso SN.Vector(Of Single).Count >= 4
        End Get
    End Property

    Friend ReadOnly Property AnchoInfo As String
        Get
            Return $"SN.Vector(Of Single).Count={SN.Vector(Of Single).Count} accelerated={SN.Vector.IsHardwareAccelerated}"
        End Get
    End Property

    ''' <summary>Las 24 secciones SoA de un bloque, UNA POR HILO y reusadas entre bloques y entre frames.
    ''' <para>⛔ <c>ThreadStatic</c> con construccion perezosa, no una fabrica que devuelve siempre el mismo
    ''' array: ese fue el bug de RecalcTBN, donde los "locales" no eran locales de nadie y el resultado
    ''' dependia de como cayeran los hilos.</para></summary>
    Private NotInheritable Class Scratch
        Public ReadOnly A(11)() As Single
        Public ReadOnly P(2)() As Single
        Public ReadOnly N(2)() As Single
        Public ReadOnly T(2)() As Single
        Public ReadOnly B(2)() As Single
        ''' <summary>⛔ Buffers de SALIDA del camino vectorial. Indexar un Vector(Of T) lane a lane
        ''' (`v(j)`) NO es una extraccion barata en .NET: pasa por un helper y se paga por lane. Con 4
        ''' salidas x 3 componentes eran 12 indexaciones por lane. `CopyTo` a un array y leer el array es
        ''' un store vectorial y lecturas contiguas.
        ''' <para>Medido: con indexacion lane a lane el vectorial rendia 1,18x sobre el escalar; con
        ''' `CopyTo`, 1,06x. No alcanzo igual — el bucle es memory-bound y el que gana es el camino DIRECTO,
        ''' sin staging. Ver el doc de <see cref="TransformarDirecto"/>.</para></summary>
        Public ReadOnly O(11)() As Single
        Public Sub New()
            For i = 0 To 11 : A(i) = New Single(Bloque - 1) {} : Next
            For i = 0 To 11 : O(i) = New Single(64) {} : Next   ' 64 >= cualquier Vector(Of Single).Count
            For i = 0 To 2
                P(i) = New Single(Bloque - 1) {}
                N(i) = New Single(Bloque - 1) {}
                T(i) = New Single(Bloque - 1) {}
                B(i) = New Single(Bloque - 1) {}
            Next
        End Sub
    End Class

    <ThreadStatic>
    Private _scratch As Scratch

    ''' <summary>⭐ LA LEY, sobre UN vertice. La usa el camino SPARSE del upload (el de <=60 % de vertices
    ''' sucios), que no puede llamar a <see cref="TransformarDirecto"/> porque escribe por
    ''' <c>Marshal.Copy</c> a un buffer mapeado, indice por indice.
    ''' <para>⚠️ RESPECTO DEL CODIGO QUE REEMPLAZA cambian dos precisiones, las dos en el camino del
    ''' render: la POSICION se acumula en Single (antes se ensanchaba la matriz a Matrix4d y se sumaba en
    ''' Double) y la NORMALIZACION es `1/MathF.Sqrt` en Single (antes `Vector3d.Normalize`, en Double). El
    ''' destino es un VBO de floats en los dos casos.</para>
    ''' <para>⛔⛔ EXISTE PARA QUE NO HAYA DOS LEYES EN EL MISMO RENDERER. Cuando el bucle denso se mudo
    ''' aca, el sparse quedo llamando al <c>Inverted().Transposed()</c> de OpenTK: la misma malla salia
    ''' con una ley u otra segun cuantos vecinos se hubieran ensuciado ese frame. La diferencia de valor
    ''' era despreciable, pero la decision de DEGENERACION no —los dos determinantes son sumatorias
    ''' distintas y discrepan— asi que un vertice podia salir con la normal identidad o con la
    ''' transformada segun el gesto del usuario.</para></summary>
    Friend Sub UnVertice(m As Matrix4, p As Vector3d, vn As Vector3, vt As Vector3, vb As Vector3,
                         msn As Boolean,
                         ByRef pos As Vector3, ByRef nrm As Vector3, ByRef tan As Vector3, ByRef bit As Vector3)
        Dim px = CSng(p.X), py = CSng(p.Y), pz = CSng(p.Z)
        pos = New Vector3(px * m.M11 + py * m.M21 + pz * m.M31 + m.M41,
                          px * m.M12 + py * m.M22 + pz * m.M32 + m.M42,
                          px * m.M13 + py * m.M23 + pz * m.M33 + m.M43)
        Dim c11 = m.M22 * m.M33 - m.M23 * m.M32
        Dim c12 = m.M23 * m.M31 - m.M21 * m.M33
        Dim c13 = m.M21 * m.M32 - m.M22 * m.M31
        Dim det = m.M11 * c11 + m.M12 * c12 + m.M13 * c13
        Dim c21 = m.M13 * m.M32 - m.M12 * m.M33
        Dim c22 = m.M11 * m.M33 - m.M13 * m.M31
        Dim c23 = m.M12 * m.M31 - m.M11 * m.M32
        Dim c31 = m.M12 * m.M23 - m.M13 * m.M22
        Dim c32 = m.M13 * m.M21 - m.M11 * m.M23
        Dim c33 = m.M11 * m.M22 - m.M12 * m.M21
        Dim r As Single = 1.0F / det
        If Math.Abs(det) < EpsDet Then
            c11 = 1.0F : c12 = 0.0F : c13 = 0.0F
            c21 = 0.0F : c22 = 1.0F : c23 = 0.0F
            c31 = 0.0F : c32 = 0.0F : c33 = 1.0F
            r = 1.0F
        End If
        Dim e11 = c11 * r, e12 = c12 * r, e13 = c13 * r
        Dim e21 = c21 * r, e22 = c22 * r, e23 = c23 * r
        Dim e31 = c31 * r, e32 = c32 * r, e33 = c33 * r
        If msn Then
            nrm = New Vector3(e11, e12, e13)
            tan = New Vector3(e21, e22, e23)
            bit = New Vector3(e31, e32, e33)
        Else
            nrm = Rotar(vn.X, vn.Y, vn.Z, e11, e12, e13, e21, e22, e23, e31, e32, e33)
            ' ⛔⛔ T Y B VAN CON LA MATRIZ CRUDA, NO CON LA DE NORMALES. Son direcciones SOBRE
            ' la superficie: se mueven como se mueve la geometria. La inversa-transpuesta es la
            ' ley de la NORMAL y solo de ella — aplicarsela a la tangente la saca del plano
            ' tangente en cuanto hay shear (blend de dos rotaciones distintas, o sea todo
            ' vertice de codo, rodilla u hombro).
            ' ⚠️ ESTO CAMBIA PIXELES DEL RENDER, y es la otra mitad del arreglo de
            ' SkinningHelper.PorMatriz3x3: alla se corrigio el bake y aca el render seguia con
            ' la ley vieja, con lo cual RENDER==BAKE quedaba roto en DOS canales en vez de uno.
            ' Los dos vertex shaders (FO4 y SSE) llevan el mismo cambio.
            tan = Rotar(vt.X, vt.Y, vt.Z, m.M11, m.M12, m.M13, m.M21, m.M22, m.M23, m.M31, m.M32, m.M33)
            bit = Rotar(vb.X, vb.Y, vb.Z, m.M11, m.M12, m.M13, m.M21, m.M22, m.M23, m.M31, m.M32, m.M33)
        End If
    End Sub

    ''' <summary>⭐ EL CAMINO QUE GANA, y no es el vectorial. Trabaja DIRECTO sobre los arrays AoS: sin
    ''' aplanar a SoA, sin scatter, sin vectores.
    ''' <para>⛔ Medido en el mismo proceso, alternando rep a rep para que la deriva termica caiga igual en
    ''' los dos lados: vectorizar da 1,06x. La razon es que este bucle es MEMORY-BOUND, no compute-bound —
    ''' el staging que la vectorizacion necesita (VB no puede reinterpretar un Matrix4() como Single())
    ''' cuesta tanto como la aritmetica que ahorra. Sin staging, la misma ley escalar corre sobre los datos
    ''' donde ya estan.</para>
    ''' <para>La version vectorial se conserva igual: es una de las implementaciones contra las que el gate
    ''' compara bit a bit, y es lo que prueba que la ley por cofactores esta bien escrita.</para>
    ''' <para>⚠️ LA POSICION TAMBIEN CAMBIO DE PRECISION, no solo la normal. El codigo anterior ensanchaba
    ''' la matriz a Matrix4d y sumaba los cuatro terminos en Double antes de redondear; aca se redondea la
    ''' posicion local a Single primero y se acumula en Single. El destino es un VBO de floats y el desvio
    ''' es del orden del ulp, pero es un cambio de bytes en el camino del render y no estaba declarado en
    ''' ningun lado.</para></summary>
    Friend Sub TransformarDirecto(mats As SkinMatricesSoA, lv() As Vector3d,
                                  ln() As Vector3, lt() As Vector3, lb() As Vector3,
                                  msn As Boolean, n As Integer,
                                  posOut() As Vector3, nrmOut() As Vector3,
                                  tanOut() As Vector3, bitOut() As Vector3)
        If n <= 0 Then Exit Sub
        ' ⭐ LAS 12 SECCIONES SE TOMAN UNA VEZ, FUERA DEL BUCLE. Cada `a0(i)` es entonces una lectura
        ' secuencial sobre un array plano: el prefetcher la ve venir. Con el `Matrix4()` anterior cada
        ' vertice reconstruia un struct de 64 B —y el indexador de SkinMatricesSoA lo sigue haciendo para
        ' los OTROS consumidores, que no estan en el camino caliente.
        Dim a0 = mats.Secciones(0), a1 = mats.Secciones(1), a2 = mats.Secciones(2)
        Dim a3 = mats.Secciones(3), a4 = mats.Secciones(4), a5 = mats.Secciones(5)
        Dim a6 = mats.Secciones(6), a7 = mats.Secciones(7), a8 = mats.Secciones(8)
        Dim a9 = mats.Secciones(9), a10 = mats.Secciones(10), a11 = mats.Secciones(11)
        Dim cuerpo As Action(Of Tuple(Of Integer, Integer)) =
            Sub(rango)
                For i = rango.Item1 To rango.Item2 - 1
                    Dim m11 = a0(i), m12 = a1(i), m13 = a2(i)
                    Dim m21 = a3(i), m22 = a4(i), m23 = a5(i)
                    Dim m31 = a6(i), m32 = a7(i), m33 = a8(i)
                    Dim m41 = a9(i), m42 = a10(i), m43 = a11(i)
                    Dim p = lv(i)
                    Dim px = CSng(p.X), py = CSng(p.Y), pz = CSng(p.Z)
                    posOut(i) = New Vector3(px * m11 + py * m21 + pz * m31 + m41,
                                            px * m12 + py * m22 + pz * m32 + m42,
                                            px * m13 + py * m23 + pz * m33 + m43)

                    Dim c11 = m22 * m33 - m23 * m32
                    Dim c12 = m23 * m31 - m21 * m33
                    Dim c13 = m21 * m32 - m22 * m31
                    Dim det = m11 * c11 + m12 * c12 + m13 * c13
                    Dim c21 = m13 * m32 - m12 * m33
                    Dim c22 = m11 * m33 - m13 * m31
                    Dim c23 = m12 * m31 - m11 * m32
                    Dim c31 = m12 * m23 - m13 * m22
                    Dim c32 = m13 * m21 - m11 * m23
                    Dim c33 = m11 * m22 - m12 * m21
                    Dim r As Single = 1.0F / det
                    If Math.Abs(det) < EpsDet Then
                        c11 = 1.0F : c12 = 0.0F : c13 = 0.0F
                        c21 = 0.0F : c22 = 1.0F : c23 = 0.0F
                        c31 = 0.0F : c32 = 0.0F : c33 = 1.0F
                        r = 1.0F
                    End If
                    Dim e11 = c11 * r, e12 = c12 * r, e13 = c13 * r
                    Dim e21 = c21 * r, e22 = c22 * r, e23 = c23 * r
                    Dim e31 = c31 * r, e32 = c32 * r, e33 = c33 * r

                    If msn Then
                        nrmOut(i) = New Vector3(e11, e12, e13)
                        tanOut(i) = New Vector3(e21, e22, e23)
                        bitOut(i) = New Vector3(e31, e32, e33)
                    Else
                        Dim vn = ln(i), vt = lt(i), vb = lb(i)
                        nrmOut(i) = Rotar(vn.X, vn.Y, vn.Z, e11, e12, e13, e21, e22, e23, e31, e32, e33)
                        ' ⛔⛔ T Y B VAN CON LA MATRIZ CRUDA, NO CON LA DE NORMALES. Son direcciones SOBRE
                        ' la superficie: se mueven como se mueve la geometria. La inversa-transpuesta es la
                        ' ley de la NORMAL y solo de ella — aplicarsela a la tangente la saca del plano
                        ' tangente en cuanto hay shear (blend de dos rotaciones distintas, o sea todo
                        ' vertice de codo, rodilla u hombro).
                        ' ⚠️ ESTO CAMBIA PIXELES DEL RENDER, y es la otra mitad del arreglo de
                        ' SkinningHelper.PorMatriz3x3: alla se corrigio el bake y aca el render seguia con
                        ' la ley vieja, con lo cual RENDER==BAKE quedaba roto en DOS canales en vez de uno.
                        ' Los dos vertex shaders (FO4 y SSE) llevan el mismo cambio.
                        tanOut(i) = Rotar(vt.X, vt.Y, vt.Z, m11, m12, m13, m21, m22, m23, m31, m32, m33)
                        bitOut(i) = Rotar(vb.X, vb.Y, vb.Z, m11, m12, m13, m21, m22, m23, m31, m32, m33)
                    End If
                Next
            End Sub
        If n >= 500 Then
            Parallel.ForEach(SkinningHelper.RangosDe(n), cuerpo)
        Else
            cuerpo(Tuple.Create(0, n))
        End If
    End Sub

    ''' <summary>⭐ PUNTO DE ENTRADA. Recorre la malla por bloques; cada bloque se aplana a SoA y se resuelve
    ''' con el camino vectorial si la maquina acelera, o con el escalar si no.
    ''' <para>El paralelismo es POR BLOQUE: cada uno escribe su propio rango de las salidas, asi que no hay
    ''' dos hilos tocando el mismo indice.</para></summary>
    Friend Sub Transformar(mats As SkinMatricesSoA, lv() As Vector3d,
                           ln() As Vector3, lt() As Vector3, lb() As Vector3,
                           msn As Boolean, n As Integer,
                           posOut() As Vector3, nrmOut() As Vector3,
                           tanOut() As Vector3, bitOut() As Vector3)
        If n <= 0 Then Exit Sub
        Dim nBloques = (n + Bloque - 1) \ Bloque
        Dim cuerpo As Action(Of Integer) =
            Sub(b)
                Dim desde = b * Bloque
                Dim cuantos = Math.Min(Bloque, n - desde)
                If _scratch Is Nothing Then _scratch = New Scratch()
                Dim sc = _scratch
                Aplanar(sc, desde, cuantos, mats, lv, ln, lt, lb, msn)
                Dim hechos As Integer = 0
                If Acelerado Then hechos = BloqueVectorial(sc, cuantos, msn, desde, posOut, nrmOut, tanOut, bitOut)
                ' La cola que no llena un vector entero (y TODO el bloque si no hay aceleracion) va por el
                ' escalar. Es la misma ley, asi que el resultado no depende de donde caiga el corte.
                BloqueEscalar(sc, hechos, cuantos, msn, desde, posOut, nrmOut, tanOut, bitOut)
            End Sub
        If nBloques >= 2 Then
            Parallel.For(0, nBloques, cuerpo)
        Else
            cuerpo(0)
        End If
    End Sub

    Private Sub Aplanar(sc As Scratch, desde As Integer, cuantos As Integer,
                        mats As SkinMatricesSoA, lv() As Vector3d,
                        ln() As Vector3, lt() As Vector3, lb() As Vector3, msn As Boolean)
        ' ⭐⭐ LA MATRIZ YA NO SE APLANA: `mats` ES SoA. Antes esto copiaba 12 floats por vertice desde un
        ' `Matrix4()` —24 escrituras dispersas contando posicion y TBN— y esa copia era LA razon por la que
        ' el camino vectorial perdia. Ahora se copian los bloques enteros con `Array.Copy`, que es un
        ' memmove contiguo, y a partir del refactor de layout ni siquiera haria falta: el kernel podria
        ' indexar `mats.Secciones(j)(desde + k)` directo. Se conserva la copia por bloque porque el scratch
        ' tiene tamano fijo `Bloque` y los indices del camino vectorial son locales al bloque.
        Dim a = sc.A
        For j = 0 To 11
            Array.Copy(mats.Secciones(j), desde, a(j), 0, cuantos)
        Next
        For k = 0 To cuantos - 1
            Dim i = desde + k
            Dim p = lv(i)
            sc.P(0)(k) = CSng(p.X) : sc.P(1)(k) = CSng(p.Y) : sc.P(2)(k) = CSng(p.Z)
            ' Con MSN no se copian: en esa rama ninguna de las cuatro implementaciones los lee, asi que
            ' aplanarlos seria mover 6 floats por vertice que nadie mira.
            ' ⚠️ Es una OPTIMIZACION, no una guarda contra Nothing. `ExtractSkinnedGeometry` — el unico
            ' constructor de SkinnedGeometry — aloca Tangents y Bitangents a largo completo tenga o no
            ' tangentes el NIF (SkinningHelper.vb:495-516, las DOS ramas del If HasTangents). Y el camino
            ' sparse de Render.vb los desreferencia por indice sea MSN o no. Si alguna vez pudieran venir
            ' en Nothing, el NRE explota alla, no aca, y esta guarda no lo tapa.
            If Not msn Then
                Dim nn = ln(i) : sc.N(0)(k) = nn.X : sc.N(1)(k) = nn.Y : sc.N(2)(k) = nn.Z
                Dim tt = lt(i) : sc.T(0)(k) = tt.X : sc.T(1)(k) = tt.Y : sc.T(2)(k) = tt.Z
                Dim bb = lb(i) : sc.B(0)(k) = bb.X : sc.B(1)(k) = bb.Y : sc.B(2)(k) = bb.Z
            End If
        Next
    End Sub

    ' =================================================================================================
    ' LA LEY, version escalar. La vectorial de abajo es su transcripcion operacion por operacion.
    ' =================================================================================================
    Private Sub BloqueEscalar(sc As Scratch, desdeK As Integer, cuantos As Integer, msn As Boolean,
                              baseIdx As Integer, posOut() As Vector3, nrmOut() As Vector3,
                              tanOut() As Vector3, bitOut() As Vector3)
        Dim a = sc.A
        For k = desdeK To cuantos - 1
            Dim m11 = a(0)(k), m12 = a(1)(k), m13 = a(2)(k)
            Dim m21 = a(3)(k), m22 = a(4)(k), m23 = a(5)(k)
            Dim m31 = a(6)(k), m32 = a(7)(k), m33 = a(8)(k)
            Dim m41 = a(9)(k), m42 = a(10)(k), m43 = a(11)(k)
            Dim px = sc.P(0)(k), py = sc.P(1)(k), pz = sc.P(2)(k)

            ' Posicion: convencion row-vector de OpenTK, igual que Vector3.TransformPosition.
            posOut(baseIdx + k) = New Vector3(px * m11 + py * m21 + pz * m31 + m41,
                                              px * m12 + py * m22 + pz * m32 + m42,
                                              px * m13 + py * m23 + pz * m33 + m43)

            Dim c11 = m22 * m33 - m23 * m32
            Dim c12 = m23 * m31 - m21 * m33
            Dim c13 = m21 * m32 - m22 * m31
            Dim det = m11 * c11 + m12 * c12 + m13 * c13
            Dim c21 = m13 * m32 - m12 * m33
            Dim c22 = m11 * m33 - m13 * m31
            Dim c23 = m12 * m31 - m11 * m32
            Dim c31 = m12 * m23 - m13 * m22
            Dim c32 = m13 * m21 - m11 * m23
            Dim c33 = m11 * m22 - m12 * m21

            Dim r As Single = 1.0F / det
            If Math.Abs(det) < EpsDet Then
                ' Degenerada -> Identidad, que es lo que devolvia NormalMatrixOrIdentity.
                c11 = 1.0F : c12 = 0.0F : c13 = 0.0F
                c21 = 0.0F : c22 = 1.0F : c23 = 0.0F
                c31 = 0.0F : c32 = 0.0F : c33 = 1.0F
                r = 1.0F
            End If

            ' ⛔ La escala entra en los cofactores ANTES del producto punto: el camino viejo construia la
            ' matriz ya escalada y despues multiplicaba. Aplicarla despues daria otro redondeo.
            Dim e11 = c11 * r, e12 = c12 * r, e13 = c13 * r
            Dim e21 = c21 * r, e22 = c22 * r, e23 = c23 * r
            Dim e31 = c31 * r, e32 = c32 * r, e33 = c33 * r

            If msn Then
                ' MSN: se empaquetan las FILAS de la matriz de normales, sin normalizar. El shader las lee
                ' como las columnas de mat3(m)^-1. Ver el call site.
                nrmOut(baseIdx + k) = New Vector3(e11, e12, e13)
                tanOut(baseIdx + k) = New Vector3(e21, e22, e23)
                bitOut(baseIdx + k) = New Vector3(e31, e32, e33)
            Else
                ' ⛔⛔ T y B con la matriz CRUDA: son direcciones sobre la superficie. Ver TransformarDirecto.
                nrmOut(baseIdx + k) = Rotar(sc.N(0)(k), sc.N(1)(k), sc.N(2)(k), e11, e12, e13, e21, e22, e23, e31, e32, e33)
                tanOut(baseIdx + k) = Rotar(sc.T(0)(k), sc.T(1)(k), sc.T(2)(k), m11, m12, m13, m21, m22, m23, m31, m32, m33)
                bitOut(baseIdx + k) = Rotar(sc.B(0)(k), sc.B(1)(k), sc.B(2)(k), m11, m12, m13, m21, m22, m23, m31, m32, m33)
            End If
        Next
    End Sub

    ''' <summary>v (vector fila) por la matriz de normales, normalizado. ⛔ La normalizacion se escribe aca y
    ''' no se delega a <c>Vector3.Normalize</c>: hay que poder transcribirla lane a lane.</summary>
    Private Function Rotar(vx0 As Single, vy0 As Single, vz0 As Single,
                           e11 As Single, e12 As Single, e13 As Single,
                           e21 As Single, e22 As Single, e23 As Single,
                           e31 As Single, e32 As Single, e33 As Single) As Vector3
        Dim vx = vx0 * e11 + vy0 * e21 + vz0 * e31
        Dim vy = vx0 * e12 + vy0 * e22 + vz0 * e32
        Dim vz = vx0 * e13 + vy0 * e23 + vz0 * e33
        Dim inv = 1.0F / MathF.Sqrt(vx * vx + vy * vy + vz * vz)
        Return New Vector3(vx * inv, vy * inv, vz * inv)
    End Function

    ' =================================================================================================
    ' LA MISMA LEY, en lanes. Devuelve cuantos vertices resolvio (multiplo del ancho del vector).
    ' =================================================================================================
    Private Function BloqueVectorial(sc As Scratch, cuantos As Integer, msn As Boolean,
                                     baseIdx As Integer, posOut() As Vector3, nrmOut() As Vector3,
                                     tanOut() As Vector3, bitOut() As Vector3) As Integer
        Dim W = SN.Vector(Of Single).Count
        If cuantos < W Then Return 0
        Dim a = sc.A
        Dim eps As New SN.Vector(Of Single)(EpsDet)
        Dim uno = SN.Vector(Of Single).One
        Dim cero = SN.Vector(Of Single).Zero
        Dim k = 0
        While k + W <= cuantos
            Dim m11 As New SN.Vector(Of Single)(a(0), k), m12 As New SN.Vector(Of Single)(a(1), k), m13 As New SN.Vector(Of Single)(a(2), k)
            Dim m21 As New SN.Vector(Of Single)(a(3), k), m22 As New SN.Vector(Of Single)(a(4), k), m23 As New SN.Vector(Of Single)(a(5), k)
            Dim m31 As New SN.Vector(Of Single)(a(6), k), m32 As New SN.Vector(Of Single)(a(7), k), m33 As New SN.Vector(Of Single)(a(8), k)
            Dim m41 As New SN.Vector(Of Single)(a(9), k), m42 As New SN.Vector(Of Single)(a(10), k), m43 As New SN.Vector(Of Single)(a(11), k)
            Dim px As New SN.Vector(Of Single)(sc.P(0), k), py As New SN.Vector(Of Single)(sc.P(1), k), pz As New SN.Vector(Of Single)(sc.P(2), k)

            Dim wpx = px * m11 + py * m21 + pz * m31 + m41
            Dim wpy = px * m12 + py * m22 + pz * m32 + m42
            Dim wpz = px * m13 + py * m23 + pz * m33 + m43

            Dim c11 = m22 * m33 - m23 * m32
            Dim c12 = m23 * m31 - m21 * m33
            Dim c13 = m21 * m32 - m22 * m31
            Dim det = m11 * c11 + m12 * c12 + m13 * c13
            Dim c21 = m13 * m32 - m12 * m33
            Dim c22 = m11 * m33 - m13 * m31
            Dim c23 = m12 * m31 - m11 * m32
            Dim c31 = m12 * m23 - m13 * m22
            Dim c32 = m13 * m21 - m11 * m23
            Dim c33 = m11 * m22 - m12 * m21

            ' ⛔ La rama del escalar se vuelve una MASCARA. La division corre igual en los lanes
            ' degenerados —da infinito— pero el select la descarta, y en punto flotante dividir por cero
            ' no lanza. Es lo que permite que no haya rama.
            Dim degen = SN.Vector.LessThan(SN.Vector.Abs(det), eps)
            Dim r = SN.Vector.ConditionalSelect(degen, uno, uno / det)
            c11 = SN.Vector.ConditionalSelect(degen, uno, c11)
            c12 = SN.Vector.ConditionalSelect(degen, cero, c12)
            c13 = SN.Vector.ConditionalSelect(degen, cero, c13)
            c21 = SN.Vector.ConditionalSelect(degen, cero, c21)
            c22 = SN.Vector.ConditionalSelect(degen, uno, c22)
            c23 = SN.Vector.ConditionalSelect(degen, cero, c23)
            c31 = SN.Vector.ConditionalSelect(degen, cero, c31)
            c32 = SN.Vector.ConditionalSelect(degen, cero, c32)
            c33 = SN.Vector.ConditionalSelect(degen, uno, c33)

            Dim e11 = c11 * r, e12 = c12 * r, e13 = c13 * r
            Dim e21 = c21 * r, e22 = c22 * r, e23 = c23 * r
            Dim e31 = c31 * r, e32 = c32 * r, e33 = c33 * r

            Dim nx, ny, nz, tx, ty, tz, bx, by, bz As SN.Vector(Of Single)
            If msn Then
                nx = e11 : ny = e12 : nz = e13
                tx = e21 : ty = e22 : tz = e23
                bx = e31 : by = e32 : bz = e33
            Else
                RotarV(New SN.Vector(Of Single)(sc.N(0), k), New SN.Vector(Of Single)(sc.N(1), k), New SN.Vector(Of Single)(sc.N(2), k),
                       e11, e12, e13, e21, e22, e23, e31, e32, e33, nx, ny, nz)
                ' ⛔⛔ T y B con la matriz CRUDA (m11..m33), no con la de normales (e11..e33). Ver
                ' TransformarDirecto: son direcciones SOBRE la superficie.
                RotarV(New SN.Vector(Of Single)(sc.T(0), k), New SN.Vector(Of Single)(sc.T(1), k), New SN.Vector(Of Single)(sc.T(2), k),
                       m11, m12, m13, m21, m22, m23, m31, m32, m33, tx, ty, tz)
                RotarV(New SN.Vector(Of Single)(sc.B(0), k), New SN.Vector(Of Single)(sc.B(1), k), New SN.Vector(Of Single)(sc.B(2), k),
                       m11, m12, m13, m21, m22, m23, m31, m32, m33, bx, by, bz)
            End If

            ' Salida a AoS: el VBO quiere xyz intercalado, asi que este scatter es inevitable. Lo que si
            ' se evita es indexar los vectores lane a lane — ver el doc de Scratch.O.
            Dim o0 = sc.O
            wpx.CopyTo(o0(0), 0) : wpy.CopyTo(o0(1), 0) : wpz.CopyTo(o0(2), 0)
            nx.CopyTo(o0(3), 0) : ny.CopyTo(o0(4), 0) : nz.CopyTo(o0(5), 0)
            tx.CopyTo(o0(6), 0) : ty.CopyTo(o0(7), 0) : tz.CopyTo(o0(8), 0)
            bx.CopyTo(o0(9), 0) : by.CopyTo(o0(10), 0) : bz.CopyTo(o0(11), 0)
            For j = 0 To W - 1
                Dim o = baseIdx + k + j
                posOut(o) = New Vector3(o0(0)(j), o0(1)(j), o0(2)(j))
                nrmOut(o) = New Vector3(o0(3)(j), o0(4)(j), o0(5)(j))
                tanOut(o) = New Vector3(o0(6)(j), o0(7)(j), o0(8)(j))
                bitOut(o) = New Vector3(o0(9)(j), o0(10)(j), o0(11)(j))
            Next
            k += W
        End While
        Return k
    End Function

    Private Sub RotarV(vx0 As SN.Vector(Of Single), vy0 As SN.Vector(Of Single), vz0 As SN.Vector(Of Single),
                       e11 As SN.Vector(Of Single), e12 As SN.Vector(Of Single), e13 As SN.Vector(Of Single),
                       e21 As SN.Vector(Of Single), e22 As SN.Vector(Of Single), e23 As SN.Vector(Of Single),
                       e31 As SN.Vector(Of Single), e32 As SN.Vector(Of Single), e33 As SN.Vector(Of Single),
                       ByRef rx As SN.Vector(Of Single), ByRef ry As SN.Vector(Of Single), ByRef rz As SN.Vector(Of Single))
        Dim vx = vx0 * e11 + vy0 * e21 + vz0 * e31
        Dim vy = vx0 * e12 + vy0 * e22 + vz0 * e32
        Dim vz = vx0 * e13 + vy0 * e23 + vz0 * e33
        Dim inv = SN.Vector(Of Single).One / SN.Vector.SquareRoot(vx * vx + vy * vy + vz * vz)
        rx = vx * inv : ry = vy * inv : rz = vz * inv
    End Sub

    ''' <summary>⭐ Corre LA FUNCION REAL por los dos caminos —vectorial y escalar— sobre los mismos datos
    ''' y compara BIT A BIT. Devuelve Nothing si pasa.
    ''' <para>⛔ Prueba la produccion, no una maqueta: un test que reimplementa el kernel al lado puede dar
    ''' verde mientras la funcion real diverge. Es la trampa #10 de 61-perf-simd-trampas.</para>
    ''' <para>⛔ Da veredicto AL ANCHO DE ESTA MAQUINA. Hay que correrlo TAMBIEN con
    ''' <c>DOTNET_MaxVectorTBitWidth=128</c>: un test que solo corre al ancho nativo no prueba nada del
    ''' otro (trampa #3).</para>
    ''' <para>El corpus incluye matrices DEGENERADAS a proposito: es donde el escalar tiene una rama y el
    ''' vectorial una mascara, o sea el unico lugar donde las dos formas pueden separarse.</para></summary>
    Friend Function SelfTest() As String
        ' ⛔ DOS TAMANOS, y los dos importan.
        '  N = Bloque + 3  -> obliga a Transformar a hacer DOS bloques, el segundo parcial: ejercita la
        '                     aritmetica de offsets (desde/cuantos/baseIdx) y el Parallel.For.
        '  MBlq = Bloque - 3  -> NO es multiplo de ningun ancho de vector (4, 8, 16), asi que el camino
        '                     vectorial deja cola y la COLA ESCALAR corre de verdad. Con M = 1024 no corria
        '                     nunca, y en produccion esa cola esta en el ultimo bloque de casi toda malla.
        ' El scratch es de Bloque, asi que las llamadas directas a los bloques van con MBlq, no con N.
        Const N As Integer = Bloque + 3
        Const MBlq As Integer = Bloque - 3
        Dim mats As New SkinMatricesSoA(N)
        Dim lv(N - 1) As Vector3d
        Dim ln(N - 1) As Vector3, lt(N - 1) As Vector3, lb(N - 1) As Vector3
        ' Generador propio y determinista: Random cambia entre runtimes y un corpus que cambia no sirve
        ' para comparar dos caminos.
        Dim est As ULong = &H243F6A8885A308D3UL
        ' ⛔ xorshift y no un congruencial: VB tiene el chequeo de desbordamiento ACTIVO, asi que el
        ' `est * 6364136223846793005UL` de un LCG lanza OverflowException. XOR y corrimientos no desbordan.
        Dim sig = Function() As Single
                      est = est Xor (est << 13)
                      est = est Xor (est >> 7)
                      est = est Xor (est << 17)
                      Return CSng((CDbl(est >> 11) / CDbl(1UL << 53)) * 4.0 - 2.0)
                  End Function
        For i = 0 To N - 1
            mats(i) = New Matrix4(sig(), sig(), sig(), 0.0F,
                                  sig(), sig(), sig(), 0.0F,
                                  sig(), sig(), sig(), 0.0F,
                                  sig(), sig(), sig(), 1.0F)
            ' Uno de cada 37 se fuerza DEGENERADO (fila 3 = fila 1 x 2): determinante exactamente 0.
            If i Mod 37 = 0 Then
                Dim m = mats(i)
                m.M31 = m.M11 * 2.0F : m.M32 = m.M12 * 2.0F : m.M33 = m.M13 * 2.0F
                mats(i) = m
            End If
            lv(i) = New Vector3d(sig(), sig(), sig())
            ln(i) = New Vector3(sig(), sig(), sig())
            lt(i) = New Vector3(sig(), sig(), sig())
            lb(i) = New Vector3(sig(), sig(), sig())
        Next

        For Each msn In New Boolean() {False, True}
            Dim pE(N - 1) As Vector3, nE(N - 1) As Vector3, tE(N - 1) As Vector3, bE(N - 1) As Vector3
            Dim pV(N - 1) As Vector3, nV(N - 1) As Vector3, tV(N - 1) As Vector3, bV(N - 1) As Vector3
            If _scratch Is Nothing Then _scratch = New Scratch()
            Dim sc = _scratch
            Aplanar(sc, 0, MBlq, mats, lv, ln, lt, lb, msn)
            BloqueEscalar(sc, 0, MBlq, msn, 0, pE, nE, tE, bE)
            Dim hechos = BloqueVectorial(sc, MBlq, msn, 0, pV, nV, tV, bV)
            If hechos = 0 AndAlso Acelerado Then
                Return $"el camino vectorial no proceso ni un vertice con {AnchoInfo}: el gate estaria " &
                       "comparando el escalar consigo mismo."
            End If
            If hechos >= MBlq Then
                Return $"la cola escalar no se ejercito: el camino vectorial resolvio {hechos} de {MBlq} con " &
                       $"{AnchoInfo}. MBlq tiene que NO ser multiplo del ancho."
            End If
            BloqueEscalar(sc, hechos, MBlq, msn, 0, pV, nV, tV, bV)
            ' Y el camino de PRODUCCION, que es el directo sin staging. Los tres tienen que coincidir: si
            ' alguien toca uno de los bucles, el gate lo agarra.
            Dim pD(N - 1) As Vector3, nD(N - 1) As Vector3, tD(N - 1) As Vector3, bD(N - 1) As Vector3
            TransformarDirecto(mats, lv, ln, lt, lb, msn, N, pD, nD, tD, bD)
            ' ⛔ Y el DISPATCHER completo, no solo los bloques sueltos: con N > Bloque hay un bloque
            ' parcial y un Parallel.For de verdad, o sea la aritmetica de offsets (desde, cuantos,
            ' baseIdx) que antes no miraba nadie. Ahi es donde un off-by-one escribe el indice de otro.
            Dim pT(N - 1) As Vector3, nT(N - 1) As Vector3, tT(N - 1) As Vector3, bT(N - 1) As Vector3
            Transformar(mats, lv, ln, lt, lb, msn, N, pT, nT, tT, bT)
            ' ⛔⛔ DOS RANGOS, Y CONFUNDIRLOS DABA VERDE FALSO. `pE`/`pV` solo estan llenos en [0, MBlq)
            ' —son las llamadas directas a los bloques— pero `pD` y `pT` cubren [0, N). Comparando todo
            ' contra MBlq, los indices 1021..1026 no se miraban contra NADA, y ahi adentro esta el bloque
            ' PARCIAL entero, que es justo lo que N = Bloque + 3 vino a crear.
            ' Verificado con controles negativos: saltear el bloque parcial (`Parallel.For(0, nBloques-1)`)
            ' daba PASS, y —peor— hacer que TransformarDirecto perdiera sus ultimos 6 vertices tambien daba
            ' PASS. Eso ultimo es el camino de PRODUCCION.
            For i = 0 To MBlq - 1
                Dim dd = PrimeraDiferencia(pE(i), pD(i))
                If dd Is Nothing Then dd = PrimeraDiferencia(nE(i), nD(i))
                If dd Is Nothing Then dd = PrimeraDiferencia(tE(i), tD(i))
                If dd Is Nothing Then dd = PrimeraDiferencia(bE(i), bD(i))
                If dd IsNot Nothing Then
                    Return $"msn={msn} vertice {i}: el camino DIRECTO (el de produccion) no da lo mismo que " &
                           $"la referencia por bloques ({dd})."
                End If
                ' Y la ley de UN vertice, que es la que usa el camino sparse del upload.
                Dim p1 As Vector3, n1 As Vector3, t1 As Vector3, b1 As Vector3
                UnVertice(mats(i), lv(i), ln(i), lt(i), lb(i), msn, p1, n1, t1, b1)
                Dim du = PrimeraDiferencia(pE(i), p1)
                If du Is Nothing Then du = PrimeraDiferencia(nE(i), n1)
                If du Is Nothing Then du = PrimeraDiferencia(tE(i), t1)
                If du Is Nothing Then du = PrimeraDiferencia(bE(i), b1)
                If du IsNot Nothing Then
                    Return $"msn={msn} vertice {i}: UnVertice (camino sparse del upload) no da lo mismo que " &
                           $"la referencia ({du})."
                End If
            Next

            ' ⭐ Y AHORA EL RANGO COMPLETO [0, N), con `pD` de referencia: es el unico que esta lleno en
            ' todo el rango. Aca entran el bloque parcial de `Transformar` y la cola de `TransformarDirecto`,
            ' o sea los dos lugares donde un off-by-one se come vertices sin que nada lo note.
            For i = 0 To N - 1
                Dim dt = PrimeraDiferencia(pD(i), pT(i))
                If dt Is Nothing Then dt = PrimeraDiferencia(nD(i), nT(i))
                If dt Is Nothing Then dt = PrimeraDiferencia(tD(i), tT(i))
                If dt Is Nothing Then dt = PrimeraDiferencia(bD(i), bT(i))
                If dt IsNot Nothing Then
                    Return $"msn={msn} vertice {i} de {N}: Transformar (bloques + cola + Parallel.For) no da " &
                           $"lo mismo que TransformarDirecto ({dt}). Revisar la aritmetica de offsets."
                End If
                ' Un vertice que ninguno de los dos escribio queda en (0,0,0) en LOS DOS y la comparacion de
                ' arriba pasa. Contra UnVertice, que no tiene bloques ni cola, eso no se puede esconder.
                Dim p2 As Vector3, n2 As Vector3, t2 As Vector3, b2 As Vector3
                UnVertice(mats(i), lv(i), ln(i), lt(i), lb(i), msn, p2, n2, t2, b2)
                Dim dv = PrimeraDiferencia(p2, pD(i))
                If dv Is Nothing Then dv = PrimeraDiferencia(n2, nD(i))
                If dv IsNot Nothing Then
                    Return $"msn={msn} vertice {i} de {N}: TransformarDirecto (PRODUCCION) no escribio lo que " &
                           $"corresponde ({dv}). Si el vertice quedo en cero, el bucle se lo salteo."
                End If
            Next
            ' ⛔ HASTA MBlq, no hasta N: `pE` y `pV` salen de llamar a los bloques DIRECTO con MBlq
            ' vertices, asi que de MBlq en adelante los dos estan en cero y comparar seria comparar ceros
            ' contra ceros. Es la misma confusion de rangos que el comentario de mas arriba dice haber
            ' arreglado, que habia sobrevivido en este bucle. Los indices [MBlq, N) los cubren las
            ' comparaciones contra `pD`/`pT`/UnVertice de arriba, que si estan llenas en todo el rango.
            For i = 0 To MBlq - 1
                Dim d = PrimeraDiferencia(pE(i), pV(i))
                If d Is Nothing Then d = PrimeraDiferencia(nE(i), nV(i))
                If d Is Nothing Then d = PrimeraDiferencia(tE(i), tV(i))
                If d Is Nothing Then d = PrimeraDiferencia(bE(i), bV(i))
                If d IsNot Nothing Then
                    Return $"msn={msn} vertice {i}: el camino vectorial y el escalar NO dan lo mismo ({d}). " &
                           "Los dos tienen que ser la MISMA ley operacion por operacion; si divergen, el " &
                           "render depende del ancho de vector de la maquina del usuario."
                End If
            Next

            ' ⛔⭐ TAMANOS CHICOS: las DOS ramas de despacho que N = Bloque + 3 no toca nunca.
            '   n < 500        -> `TransformarDirecto` corre SECUENCIAL (`cuerpo(Tuple.Create(0, n))`), sin
            '                     Parallel.ForEach. Es un punto de entrada distinto al medido arriba.
            '   n <= Bloque    -> `Transformar` toma la rama `nBloques = 1`, que llama `cuerpo(0)` directo
            '                     en vez del Parallel.For.
            ' No son hipoteticas: una malla de accesorio (un anillo, un boton, un mechon) tiene decenas de
            ' vertices, y el editor de cara sube shapes chicas todo el tiempo. Un off-by-one que solo viva
            ' en la rama secuencial saldria SOLO en esas mallas — el peor sintoma posible, porque el corpus
            ' pesado seguiria verde.
            For Each nChico In New Integer() {1, 7, 63, 499, 500, Bloque}
                Dim pc(N - 1) As Vector3, nc(N - 1) As Vector3, tc(N - 1) As Vector3, bc(N - 1) As Vector3
                Dim pq(N - 1) As Vector3, nq(N - 1) As Vector3, tq(N - 1) As Vector3, bq(N - 1) As Vector3
                TransformarDirecto(mats, lv, ln, lt, lb, msn, nChico, pc, nc, tc, bc)
                Transformar(mats, lv, ln, lt, lb, msn, nChico, pq, nq, tq, bq)
                For i = 0 To nChico - 1
                    ' Contra UnVertice, que no tiene ni bloques ni ramas por tamano: es el unico arbitro que
                    ' no comparte el mecanismo bajo prueba.
                    Dim pu As Vector3, nu As Vector3, tu As Vector3, bu As Vector3
                    UnVertice(mats(i), lv(i), ln(i), lt(i), lb(i), msn, pu, nu, tu, bu)
                    Dim dc = PrimeraDiferencia(pu, pc(i))
                    If dc Is Nothing Then dc = PrimeraDiferencia(nu, nc(i))
                    If dc Is Nothing Then dc = PrimeraDiferencia(tu, tc(i))
                    If dc Is Nothing Then dc = PrimeraDiferencia(bu, bc(i))
                    If dc IsNot Nothing Then
                        Return $"msn={msn} n={nChico} vertice {i}: TransformarDirecto (rama " &
                               $"{If(nChico < 500, "SECUENCIAL", "paralela")}) no da lo mismo que UnVertice ({dc})."
                    End If
                    Dim dq = PrimeraDiferencia(pu, pq(i))
                    If dq Is Nothing Then dq = PrimeraDiferencia(nu, nq(i))
                    If dq Is Nothing Then dq = PrimeraDiferencia(tu, tq(i))
                    If dq Is Nothing Then dq = PrimeraDiferencia(bu, bq(i))
                    If dq IsNot Nothing Then
                        Return $"msn={msn} n={nChico} vertice {i}: Transformar (rama nBloques=" &
                               $"{(nChico + Bloque - 1) \ Bloque}) no da lo mismo que UnVertice ({dq})."
                    End If
                Next
                ' Y que NO haya escrito de mas: el vertice nChico es de otro, y pisarlo en produccion
                ' significa corromper la malla vecina del mismo buffer.
                If nChico < N Then
                    If PrimeraDiferencia(pc(nChico), Vector3.Zero) IsNot Nothing OrElse
                       PrimeraDiferencia(pq(nChico), Vector3.Zero) IsNot Nothing Then
                        Return $"msn={msn} n={nChico}: se escribio el vertice {nChico}, que esta FUERA del rango pedido."
                    End If
                End If
            Next
        Next

        ' ⛔⛔⭐ Y AHORA EL ORACULO INDEPENDIENTE. Todo lo de arriba es un gate de CONSISTENCIA: prueba que
        ' las cuatro transcripciones digan lo mismo, no que digan lo CORRECTO. Un revisor lo demostro
        ' rompiendolo: cambiar `r = 1/det` por `r = -1/det` en las cuatro —o sea NEGAR la matriz de normales
        ' entera, que en pantalla es el modelo iluminado del lado equivocado— pasaba en VERDE. Lo mismo
        ' subiendo EpsDet de 1e-12 a 1e-3, que manda a Identidad a matrices perfectamente sanas.
        Dim oraculo = OraculoDeLaLey()
        If oraculo IsNot Nothing Then Return oraculo
        Return Nothing
    End Function

    ''' <summary>⭐⭐ Verifica el INVARIANTE de la matriz de normales, no su mecanismo. No conoce los
    ''' cofactores, ni el determinante, ni <c>EpsDet</c>: lo unico que usa es la multiplicacion
    ''' matriz-por-vector cruda, escrita a mano aca. Por eso puede contradecir a las cuatro transcripciones
    ''' a la vez.
    ''' <para>⭐ EL INVARIANTE. Si <c>G</c> es la matriz de normales de <c>M</c>, entonces por definicion
    ''' <c>G = (M⁻¹)ᵀ</c> y salen dos identidades EXACTAS en algebra, sin importar como se calcule G:
    ''' <list type="number">
    ''' <item><b>FORMA</b> — para todo <c>t</c> con <c>n·t = 0</c>: <c>(G n) · (M t) = nᵀ M⁻¹ M t = n·t = 0</c>.
    ''' O sea: la normal transformada sigue perpendicular a la superficie transformada. Es LA propiedad por
    ''' la que existe la matriz de normales, y una ley mal escrita la rompe.</item>
    ''' <item><b>ORIENTACION</b> — <c>(G n) · (M n) = nᵀ M⁻¹ M n = n·n &gt; 0</c>. Estrictamente positivo,
    ''' pase lo que pase con el signo del determinante. Esto es lo que caza la normal negada, que la
    ''' perpendicularidad sola no ve (−N tambien es perpendicular).</item>
    ''' </list></para>
    ''' <para>⛔ ES CON TOLERANCIA Y TIENE QUE SERLO: compara dos cuentas distintas en Single, no dos
    ''' transcripciones de una. Un umbral bit a bit aca daria rojo permanente. El umbral se fijo midiendo el
    ''' peor caso real del corpus y dejando ~10x de margen; el corpus reporta cuantas matrices evaluo para
    ''' que no pueda pasar en vacio.</para>
    ''' <para>⛔ Se saltean las MAL CONDICIONADAS. Con matrices aleatorias en [-2,2] las hay casi
    ''' singulares, y ahi el invariante se cumple en algebra exacta pero se pierde en el redondeo de Single:
    ''' rechazarlas por condicionamiento es correcto, taparlas con un umbral flojo no.</para></summary>
    ''' <summary>⛔ EL UMBRAL DE SANIDAD DEL ORACULO, y es a proposito que NO sea <c>EpsDet</c>. Es el
    ''' contrato que el oraculo afirma: toda matriz con determinante de este orden o mayor tiene que salir
    ''' TRANSFORMADA, no colapsada a Identidad. Esta 1000x por encima del corte vigente (1e-12), asi que hay
    ''' tres decadas de margen: bajar el corte no rompe nada y subirlo por encima de aca se caza.
    ''' <para>⛔ Si esto dijera <c>EpsDet</c>, el oraculo seria circular — subir el corte agrandaria a la vez
    ''' el conjunto de matrices maltratadas y el de matrices que el oraculo se saltea. Verificado: con el
    ''' corte en 1e-3 y este umbral atado a el, el control negativo pasaba en verde.</para></summary>
    Private Const DetSano As Single = 0.000000001F

    Private Function OraculoDeLaLey() As String
        Const N As Integer = 512
        Dim mats As New SkinMatricesSoA(N)
        Dim lv(N - 1) As Vector3d
        Dim ln(N - 1) As Vector3, lt(N - 1) As Vector3, lb(N - 1) As Vector3
        Dim est As ULong = &H13198A2E03707344UL
        Dim sig = Function() As Single
                      est = est Xor (est << 13)
                      est = est Xor (est >> 7)
                      est = est Xor (est << 17)
                      Return CSng((CDbl(est >> 11) / CDbl(1UL << 53)) * 4.0 - 2.0)
                  End Function
        ' ⛔⭐ EL CORPUS TIENE DOS MITADES, Y LA SEGUNDA NO ES DECORATIVA.
        ' La primera es aleatoria, y sirve para la forma general de la ley. Pero un corpus aleatorio en
        ' [-2,2] tiene determinantes de orden 1: NINGUNA de sus matrices se acerca al corte por degeneracion,
        ' asi que mover `EpsDet` no le cambia una sola normal. Verificado corriendolo: con `EpsDet` subido de
        ' 1e-12 a 1e-3 —que manda a Identidad a cualquier hueso con escala menor a 0,1— el oraculo con solo
        ' la mitad aleatoria daba PASS. El filtro de condicionamiento de mas abajo se comia exactamente la
        ' poblacion que el defecto mueve.
        ' La segunda mitad son ROTACION x ESCALA UNIFORME con la escala bajando por decadas. Estan
        ' PERFECTAMENTE condicionadas —el invariante se cumple exacto, la inversa es la traspuesta sobre s—
        ' pero su determinante es s^3, o sea que barren el corte de arriba a abajo. Y no son sinteticas de
        ' laboratorio: un hueso con Scale chico en el editor de transforms produce exactamente esto.
        Dim mitad As Integer = N \ 2
        For i = 0 To N - 1
            If i < mitad Then
                mats(i) = New Matrix4(sig(), sig(), sig(), 0.0F,
                                      sig(), sig(), sig(), 0.0F,
                                      sig(), sig(), sig(), 0.0F,
                                      sig(), sig(), sig(), 1.0F)
            Else
                ' Rotacion por (a, b, c) construida como tres giros de ejes, por escala uniforme s.
                Dim a = CSng(sig() * 1.5), b = CSng(sig() * 1.5), c = CSng(sig() * 1.5)
                Dim ca = MathF.Cos(a), sa = MathF.Sin(a)
                Dim cb = MathF.Cos(b), sb = MathF.Sin(b)
                Dim cc = MathF.Cos(c), sc2 = MathF.Sin(c)
                ' s recorre 1, 1e-1, 1e-2 y 1e-3, o sea determinantes de 1 a 1e-9. El tope de abajo lo fija
                ' `DetSano`: no se generan escalas que la ley vigente tenga derecho a llamar degeneradas.
                Dim s As Single = MathF.Pow(10.0F, -CSng((i - mitad) Mod 4))
                Dim r11 = cb * cc, r12 = cb * sc2, r13 = -sb
                Dim r21 = sa * sb * cc - ca * sc2, r22 = sa * sb * sc2 + ca * cc, r23 = sa * cb
                Dim r31 = ca * sb * cc + sa * sc2, r32 = ca * sb * sc2 - sa * cc, r33 = ca * cb
                mats(i) = New Matrix4(r11 * s, r12 * s, r13 * s, 0.0F,
                                      r21 * s, r22 * s, r23 * s, 0.0F,
                                      r31 * s, r32 * s, r33 * s, 0.0F,
                                      sig(), sig(), sig(), 1.0F)
            End If
            ' La normal se normaliza: el invariante habla de direcciones, y `n·n > 0` con |n|=1 da 1.
            Dim nx = sig(), ny = sig(), nz = sig()
            Dim l = MathF.Sqrt(nx * nx + ny * ny + nz * nz)
            If l < 0.001F Then nx = 1.0F : ny = 0.0F : nz = 0.0F : l = 1.0F
            ln(i) = New Vector3(nx / l, ny / l, nz / l)
            lv(i) = New Vector3d(sig(), sig(), sig())
            lt(i) = New Vector3(sig(), sig(), sig())
            lb(i) = New Vector3(sig(), sig(), sig())
        Next

        ' Multiplicacion matriz-por-vector CRUDA. Es el unico algebra que este oraculo conoce, y es
        ' deliberadamente trivial: tres productos punto contra las columnas.
        Dim porM = Function(m As Matrix4, x As Single, y As Single, z As Single) As Vector3
                       Return New Vector3(x * m.M11 + y * m.M21 + z * m.M31,
                                          x * m.M12 + y * m.M22 + z * m.M32,
                                          x * m.M13 + y * m.M23 + z * m.M33)
                   End Function
        Dim unitario = Function(v As Vector3) As Vector3
                           Dim l2 = MathF.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z)
                           If l2 < 0.0000001F Then Return Vector3.Zero
                           Return New Vector3(v.X / l2, v.Y / l2, v.Z / l2)
                       End Function

        ' Norma de Frobenius de la parte lineal, para juzgar condicionamiento contra el determinante.
        Dim peorPerp As Single = 0, peorOrient As Single = 1.0F, peorTan As Single = 1.0F
        Dim evaluadas As Integer = 0, salteadas As Integer = 0
        Dim iPeorPerp As Integer = -1, iPeorOrient As Integer = -1, iPeorTan As Integer = -1
        For i = 0 To N - 1
            Dim m = mats(i)
            Dim fro = MathF.Sqrt(m.M11 * m.M11 + m.M12 * m.M12 + m.M13 * m.M13 +
                                 m.M21 * m.M21 + m.M22 * m.M22 + m.M23 * m.M23 +
                                 m.M31 * m.M31 + m.M32 * m.M32 + m.M33 * m.M33)
            Dim det = m.M11 * (m.M22 * m.M33 - m.M23 * m.M32) +
                      m.M12 * (m.M23 * m.M31 - m.M21 * m.M33) +
                      m.M13 * (m.M21 * m.M32 - m.M22 * m.M31)
            ' ⛔⭐ DOS FILTROS, Y EL SEGUNDO NO PUEDE MENCIONAR `EpsDet`.
            '  (1) CONDICIONAMIENTO, relativo: casi singular respecto de su propia escala. Ahi el invariante
            '      se cumple en algebra exacta pero se pierde en el redondeo de Single, asi que juzgarla
            '      seria acusar al kernel del error del instrumento.
            '  (2) SANIDAD, absoluto y PROPIO DE ESTE ORACULO: `DetSano`. Si el oraculo se salteara todo lo
            '      que la ley llama degenerado —o sea si mirara `EpsDet`— seria circular: subir `EpsDet`
            '      agrandaria a la vez el conjunto de matrices mal tratadas y el de matrices que el oraculo
            '      no mira, y el defecto quedaria invisible por construccion. Con un umbral propio, el
            '      oraculo AFIRMA algo: "toda matriz con |det| >= DetSano tiene que salir transformada, no
            '      en Identidad", y esa afirmacion se rompe si alguien sube el corte.
            If Math.Abs(det) < 0.02F * fro * fro * fro OrElse Math.Abs(det) < DetSano Then
                salteadas += 1 : Continue For
            End If

            Dim nLocal = ln(i)
            ' El kernel de PRODUCCION, un vertice: `nK` es G·n ya normalizada.
            Dim pK As Vector3, nK As Vector3, tK As Vector3, bK As Vector3
            UnVertice(m, lv(i), nLocal, lt(i), lb(i), False, pK, nK, tK, bK)
            If Not (Single.IsFinite(nK.X) AndAlso Single.IsFinite(nK.Y) AndAlso Single.IsFinite(nK.Z)) Then
                Return $"vertice {i}: el kernel devolvio una normal no finita ({nK}) con una matriz bien " &
                       $"condicionada (det={det}, ||M||={fro})."
            End If

            ' (1) FORMA. Dos tangentes independientes, las dos perpendiculares a n por construccion
            ' (producto cruz contra dos ejes distintos, para que ninguna salga degenerada).
            Dim ejes = New Vector3() {New Vector3(1.0F, 0.0F, 0.0F), New Vector3(0.0F, 1.0F, 0.0F)}
            For Each eje In ejes
                Dim t = New Vector3(nLocal.Y * eje.Z - nLocal.Z * eje.Y,
                                    nLocal.Z * eje.X - nLocal.X * eje.Z,
                                    nLocal.X * eje.Y - nLocal.Y * eje.X)
                t = unitario(t)
                If t = Vector3.Zero Then Continue For   ' n paralela a este eje: la otra tangente sirve
                Dim mt = unitario(porM(m, t.X, t.Y, t.Z))
                If mt = Vector3.Zero Then Continue For
                Dim cos = Math.Abs(nK.X * mt.X + nK.Y * mt.Y + nK.Z * mt.Z)
                If cos > peorPerp Then peorPerp = cos : iPeorPerp = i
            Next

            ' (2) ORIENTACION. Con |n| = 1, (G n)·(M n) = 1 antes de normalizar G n; despues de normalizar
            ' sigue siendo estrictamente positivo. Un signo dado vuelta lo manda a -1.
            Dim mn = unitario(porM(m, nLocal.X, nLocal.Y, nLocal.Z))
            If mn <> Vector3.Zero Then
                Dim d = nK.X * mn.X + nK.Y * mn.Y + nK.Z * mn.Z
                If d < peorOrient Then peorOrient = d : iPeorOrient = i
            End If

            ' ⛔⛔ (3) LA TANGENTE VA CON LA MATRIZ CRUDA, y sin este chequeo el kernel estuvo aplicandole
            ' la matriz de NORMALES sin que nada lo notara — mientras el bake ya usaba la correcta, o sea
            ' con RENDER==BAKE roto en dos canales. Los chequeos (1) y (2) no lo veian: hablan solo de la
            ' normal, y la normal estaba bien.
            ' El invariante: la tangente transformada tiene que ser PARALELA a la tangente de la superficie
            ' transformada, que es `t · M`. Da 1 si es la misma direccion; con la matriz de normales en su
            ' lugar se separa apenas hay shear.
            Dim tSup = unitario(porM(m, lt(i).X, lt(i).Y, lt(i).Z))
            If tSup <> Vector3.Zero AndAlso Single.IsFinite(tK.X) Then
                Dim dt = tK.X * tSup.X + tK.Y * tSup.Y + tK.Z * tSup.Z
                If dt < peorTan Then peorTan = dt : iPeorTan = i
            End If
            evaluadas += 1
        Next

        ' ⛔ Sin esto el oraculo pasa en vacio: si el filtro de condicionamiento se comiera todo el corpus,
        ' los dos "peor caso" quedarian en su inicializacion y el chequeo daria verde sin haber mirado nada.
        If evaluadas < N \ 4 Then
            Return $"el oraculo evaluo solo {evaluadas} de {N} matrices ({salteadas} salteadas por " &
                   "condicionamiento): el corpus no alcanza para dar veredicto."
        End If

        ' Umbrales. Medidos sobre este corpus: peor perpendicularidad ~1e-3, peor orientacion ~+1.
        ' Se dejan con margen ~10x para no dar rojo por ruido de una maquina distinta, pero MUY lejos de
        ' los valores que producen los defectos reales (perpendicularidad 1,0 con la ley mal escrita;
        ' orientacion -1,0 con el signo negado).
        If peorPerp > 0.02F Then
            Return $"INVARIANTE DE FORMA ROTO (vertice {iPeorPerp}): la normal que devuelve el kernel NO es " &
                   $"perpendicular a la superficie transformada — |cos| = {peorPerp} donde tendria que ser ~0. " &
                   "La matriz de normales esta mal calculada, o EpsDet manda a Identidad matrices sanas. " &
                   "⛔ Esto NO lo ve la comparacion entre las cuatro transcripciones: si las cuatro dicen lo " &
                   "mismo y estan las cuatro mal, aquel gate pasa y este no."
        End If
        If peorOrient <= 0.1F Then
            Return $"INVARIANTE DE ORIENTACION ROTO (vertice {iPeorOrient}): (G·n)·(M·n) = {peorOrient}, " &
                   "y tiene que ser estrictamente positivo para cualquier matriz. Si dio ~-1, la matriz de " &
                   "normales esta NEGADA y el modelo se ilumina del lado equivocado."
        End If
        If peorTan < 0.999F Then
            Return $"INVARIANTE DE LA TANGENTE ROTO (vertice {iPeorTan}): la tangente que devuelve el kernel " &
                   $"no es paralela a la de la superficie transformada — cos = {peorTan}, tendria que ser 1. " &
                   "T y B son direcciones SOBRE la superficie y van con la matriz CRUDA; si se les aplica la " &
                   "matriz de normales, la base tangente sale girada en cuanto hay shear y el bake deja de " &
                   "coincidir con el render."
        End If
        Return Nothing
    End Function

    ''' <summary>Compara BIT a bit, no por tolerancia: dos NaN con distinto payload son distintos, y un
    ''' -0 no es un 0. Devuelve Nothing si son identicos.</summary>
    Private Function PrimeraDiferencia(a As Vector3, b As Vector3) As String
        If BitConverter.SingleToInt32Bits(a.X) <> BitConverter.SingleToInt32Bits(b.X) Then Return $"X: {a.X} vs {b.X}"
        If BitConverter.SingleToInt32Bits(a.Y) <> BitConverter.SingleToInt32Bits(b.Y) Then Return $"Y: {a.Y} vs {b.Y}"
        If BitConverter.SingleToInt32Bits(a.Z) <> BitConverter.SingleToInt32Bits(b.Z) Then Return $"Z: {a.Z} vs {b.Z}"
        Return Nothing
    End Function

End Module
