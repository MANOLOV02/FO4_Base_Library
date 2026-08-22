Imports SN = System.Numerics
Imports OpenTK.Mathematics
Imports System.Runtime.CompilerServices

''' <summary>El kernel del SKINNING DE CPU: de matriz per-vertice a posicion, normal, tangente y
''' bitangente de MUNDO. Es el bucle que domina un frame de animacion con skinning por CPU (medido: 9,3 ms
''' de un frame de ~20 sobre 130.500 vertices, contra 1,3 ms de las cuatro subidas de VBO).
'''
''' <para>LA LEY SE ESCRIBE UNA SOLA VEZ… PERO ESTA TRANSCRIPTA CUATRO VECES, y el
''' <see cref="SelfTest"/> compara las cuatro entre si: <see cref="TransformarDirecto"/> (produccion, AoS
''' sin staging), <see cref="UnVertice"/> (produccion, camino sparse del upload, un vertice por vez),
''' <see cref="BloqueEscalar"/> (referencia) y <see cref="BloqueVectorial"/>. Si tocas una, tocas las cuatro.
''' <para>⛔ PERO ESO ES UN GATE DE CONSISTENCIA, NO DE CORRECCION: comparar las cuatro entre si prueba que
''' nadie edito una sola, y NADA MAS. Negar la matriz de normales en las cuatro a la vez —el modelo
''' iluminado del lado equivocado— lo deja VERDE. Por eso existe <see cref="OraculoDeLaLey"/>, que no conoce
''' los cofactores ni el determinante ni el corte, y verifica el INVARIANTE con la sola multiplicacion
''' matriz-por-vector: ahi fallan los tres controles negativos —normal negada, corte subido a 1e-3, ley
''' transpuesta.</para>
''' <para>La vectorial solo puede dar bit a bit lo mismo si cada lane
''' del vector ejecuta EXACTAMENTE la misma secuencia de operaciones que el escalar, en el mismo orden. Por
''' eso aca NO se llama a <c>Matrix3.Inverted()</c> ni a <c>Vector3.Normalize()</c>: son algoritmos ajenos,
''' con su propio orden y quiza con ramas, y entonces el gate estaria comparando dos LEYES en vez de dos
''' implementaciones de una. Ver <see cref="SelfTest"/>.</para>
''' <para>"Cuatro transcripciones" vale para los COFACTORES. La rotacion y la normalizacion estan escritas
''' dos veces, no cuatro: <c>Rotar</c> la comparten los tres caminos escalares y <c>RotarV</c> es la vectorial.
''' O sea que un error en <c>Rotar</c> solo lo caza la comparacion contra la vectorial — y si la maquina no
''' acelera, esa comparacion no corre. Ese hueco lo tapa el oraculo, que no pasa por ninguna de las dos.</para>
'''
''' <para>LA MATRIZ DE NORMALES SALE POR COFACTORES, no invirtiendo y transponiendo. La transpuesta de la
''' inversa de una 3x3 es exactamente <c>cofactores / determinante</c> —la transposicion se cancela con la
''' del adjunto—, o sea 9 restas de productos, un producto punto y UNA reciproca: sin ramas y sin una
''' division por elemento, que es lo que la hace vectorizable. Medido por el check [cofactores] del arnes contra
''' el <c>Inverted().Transposed()</c> de OpenTK, sobre 24.927 normales reales y posadas del actor canonico:
''' desvio medio 0,0036 grados, peor caso 0,0296.
''' <para>NO CONFUNDIR con el numero de [normal-single] (medio 0,000001, peor 0,000017), que contesta otra
''' pregunta —cuanto movio la normal evaluar la inversa en Single en vez de en Double— y es cuatro ordenes de
''' magnitud mas chico.</para></para>
'''
''' <para>SE TRABAJA POR BLOQUES, y no es un detalle de estilo. Vectorizar exige los datos en SoA (todos
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

    ''' <summary>Corte por determinante degenerado, RELATIVO a la escala de la matriz. Un
    ''' determinante no se puede juzgar con un numero absoluto: <c>det</c> escala como el CUBO de la
    ''' matriz, asi que el mismo umbral significa cosas distintas segun las unidades.
    '''
    ''' <para><b>Por que NO puede ser absoluto.</b> Con un corte absoluto de <c>1e-12</c> —que venia de
    ''' cuando el determinante se calculaba en <b>Double</b>— el umbral queda ~5 decadas POR DEBAJO del
    ''' ruido de redondeo del determinante EN SINGLE (~1e-7 con entradas de orden 1), o sea que degenera
    ''' a "det exactamente 0" y deja de detectar lo que tiene que detectar. MEDIDO sobre 2000 matrices
    ''' <b>exactamente singulares</b> (fila 3 = 2 x fila 1): en Double se detectan las 2000; en Single
    ''' solo 713. Las otras <b>1287 pasaban la guarda</b>, tomaban <c>r = 1/det</c> con |det| ~7,5e-8
    ''' —o sea r ~1,3e7— y salian con una matriz de normales multiplicada por eso.</para>
    '''
    ''' <para><b>Y lo que este corte NO explica.</b> Sobre el corpus real (SG172_Serena_BattleSuit,
    ''' 130.500 vertices) el criterio relativo no mueve NADA respecto del absoluto: las mismas <b>59</b>
    ''' matrices degeneradas. Las degeneradas del contenido real son del tipo que el absoluto tambien
    ''' atrapaba (fila nula / escala 0 en un eje), no del tipo "una fila es combinacion lineal de las
    ''' otras". ⛔ Y los 59 valores NO FINITOS que reportaba el arnes NO son de este corte: son la normal
    ''' de ENTRADA nula, y los ataja la guarda de <see cref="Rotar"/>. No atribuirle a este umbral un
    ''' defecto que no es suyo — ese error ya se cometio una vez.</para>
    '''
    ''' <para>⇒ Este criterio es de ROBUSTEZ: cubre un caso que el corpus no ejercita, y por eso mismo su
    ''' riesgo de mover bytes horneados sobre el contenido medido es NULO.</para>
    '''
    ''' <para><b>Por que no alcanza con subir la constante.</b> Se probo: con el corte absoluto en
    ''' 1e-3 se mandan a Identidad matrices perfectamente sanas (esta anotado mas abajo, en el oraculo).
    ''' Con escala uniforme s el determinante es s^3, asi que CUALQUIER absoluto confunde "matriz chica"
    ''' con "matriz degenerada": una rotacion pura escalada por 0,001 tiene det = 1e-9 y es sana.</para>
    '''
    ''' <para><b>El criterio.</b> Por Hadamard |det| &lt;= producto de las normas de fila, y esa cota vale
    ''' a lo sumo <c>(F2/3)^(3/2)</c> con <c>F2</c> = suma de los 9 cuadrados. El cociente
    ''' <c>|det| / (F2/3)^(3/2)</c> es ADIMENSIONAL, vale 1 para una matriz ortogonal y 0 para una
    ''' singular, y en ALGEBRA no cambia si se reescala la matriz. Se compara contra <c>EpsDetRel</c>.</para>
    '''
    ''' <para>LA INVARIANCIA DE ESCALA ES DEL ALGEBRA, NO DE SINGLE, y se rompe en las dos puntas. No
    ''' enunciarla sin reservas: es lo que autoriza al que sigue a no probar el regimen chico.</para>
    ''' <list type="bullet">
    ''' <item><c>t &lt; 1,4e-35</c> (entradas RMS &lt; ~2,2e-18): <c>eps^2 * t</c> hace underflow a 0 y el
    ''' predicado degenera a <c>q*q &lt;= 0</c>, o sea al criterio ABSOLUTO "det = 0 exacto" que este cambio
    ''' vino a matar. Se degrada en silencio.</item>
    ''' <item><c>|det| &lt; 2,94e-39</c>: la reciproca desborda. Lo cubre la tercera guarda de
    ''' <see cref="EsDegenerada"/>, con los numeros medidos.</item>
    ''' </list>
    ''' <para>Las dos bandas viven en escalas ≲1e-13, o sea fuera de cualquier rig real; lo que importa es
    ''' que estan MEDIDAS y acotadas, y que el barrido del gate ahora baja hasta ahi.</para>
    '''
    ''' <para>El valor 1e-5 son ~10 veces el error relativo esperable del determinante en Single (unas
    ''' 10 operaciones a 1,2e-7 cada una). Una matriz con cociente menor tiene numero de condicion
    ''' &gt;1e5: invertirla en Single da basura igual, asi que mandarla a Identidad es lo correcto.</para>
    '''
    ''' <para>Verificado sobre 3000 matrices de cada clase: el criterio relativo clasifica bien las
    ''' seis (singular por combinacion lineal, singular por fila nula, rotacion pura, rotacion x 0,001,
    ''' rotacion x 100, y shear fuerte); el absoluto se equivoca en 1970 de 3000 de la primera.</para>
    '''
    ''' <para>⛔ TODOS los caminos tienen que usar ESTE predicado, no una transcripcion suya: es el
    ''' predicado el que decide, y dos transcripciones que se separen parten la malla en dos leyes.
    ''' <para>El DETERMINANTE, en cambio, NO es un punto de divergencia, y no hay que inventarle uno:
    ''' <see cref="DetPorPrimeraFila"/> y <c>OpenTK.Matrix3.Determinant</c> son la MISMA expansion con los
    ''' signos redistribuidos (<c>-m12*(m21*m33 - m23*m31)</c> contra <c>+m12*(m23*m31 - m21*m33)</c>), y
    ''' en IEEE-754 la negacion es exacta. MEDIDO sobre 200.000 matrices perturbadas al borde del corte:
    ''' <b>cero</b> diferencias, ni siquiera de bits. Comparar un determinante en Double contra uno en
    ''' Single mide otra cosa y da un falso positivo.</para></para></summary>
    Friend Const EpsDetRel As Single = 0.00001F

    ''' <summary>El determinante EXACTAMENTE como lo calcula el kernel: expansion por la PRIMERA fila.
    ''' <para>EXISTE PARA QUE NO HAYA DOS. El valor coincide bit a bit con
    ''' <c>OpenTK.Matrix3.Determinant</c> (ver <see cref="EpsDetRel"/>), pero tenerlo escrito aca saca la
    ''' dependencia de un detalle interno de OpenTK y deja UN solo sitio que tocar.</para>
    ''' <para>⛔ Y el gate tiene que alimentarse de ESTA funcion, no de una transcripcion propia: un gate
    ''' que se escribe su propio determinante no puede ver una divergencia entre los dos.</para></summary>
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Friend Function DetPorPrimeraFila(m11 As Single, m12 As Single, m13 As Single,
                                      m21 As Single, m22 As Single, m23 As Single,
                                      m31 As Single, m32 As Single, m33 As Single) As Single
        Dim c11 = m22 * m33 - m23 * m32
        Dim c12 = m23 * m31 - m21 * m33
        Dim c13 = m21 * m32 - m22 * m31
        Return m11 * c11 + m12 * c12 + m13 * c13
    End Function

    ''' <summary>El predicado de degeneracion, escrito UNA vez. Ver <see cref="EpsDetRel"/>.
    ''' <para>Sin raiz cuadrada a proposito: se comparan los CUADRADOS, que es lo mismo porque los dos
    ''' lados son no negativos, y asi el camino caliente no paga un <c>sqrt</c> por vertice.</para>
    ''' <para>La comparacion es <c>&lt;=</c> y no <c>&lt;</c> para que la matriz NULA (F2 = 0, det = 0,
    ''' cota = 0) caiga del lado degenerado. Con <c>&lt;</c> se escaparia justo el caso mas degenerado
    ''' que existe.</para></summary>
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Friend Function EsDegenerada(det As Single,
                                 m11 As Single, m12 As Single, m13 As Single,
                                 m21 As Single, m22 As Single, m23 As Single,
                                 m31 As Single, m32 As Single, m33 As Single) As Boolean
        Dim f2 = m11 * m11 + m12 * m12 + m13 * m13 +
                 m21 * m21 + m22 * m22 + m23 * m23 +
                 m31 * m31 + m32 * m32 + m33 * m33
        Dim t = f2 * (1.0F / 3.0F)
        ' TRES GUARDAS, Y LAS TRES SON NECESARIAS. Escritas como negaciones a proposito: asi un NaN
        ' —que pierde TODA comparacion— cae del lado DEGENERADO, que es el correcto.
        '  · `t` no positivo: matriz nula (t = 0) o con algun NaN. La division daria NaN y el predicado
        '    diria "sana", que es justo el caso mas degenerado que existe.
        '  · `det` no finito: con una rotacion escalada por ~7e12 el DETERMINANTE (que va como s^3)
        '    desborda a +Inf cuatro decadas ANTES que f2 (que va como s^2), asi que la guarda de `t` no
        '    lo ve. Sin esto: q = Inf, `Inf <= algo` es False ⇒ "sana" ⇒ r = 1/Inf = 0 ⇒ los nueve
        '    cofactores en CERO ⇒ la normal sale (0,0,0) o NaN al normalizar. Tiene que dar Identidad.
        '  · la RECIPROCA no finita. La guarda de arriba mira `det` HACIA ARRIBA; esta lo mira hacia
        '    ABAJO, que es el otro lado del MISMO `s^3`. Con escala uniforme chica el determinante se
        '    hunde y lo que desborda no es el, es `1/det` — y `1/det` es el numero que despues multiplica
        '    los nueve cofactores. MEDIDO (Single, el mismo orden de operaciones del kernel):
        '        s = 1,44e-13 -> det = 2,99e-39 -> 1/det = 3,35e38   ultimo sano
        '        s = 1,43e-13 -> det = 2,92e-39 -> 1/det = +Inf      <- y las dos guardas decian SANA
        '        s = 1e-15    -> det = 1,40e-45 -> 1/det = +Inf
        '    En esa banda —s entre ~1e-15 y 1,43e-13— los cofactores nulos daban `0 * Inf = NaN` en las
        '    seis entradas de fuera de la diagonal, `Rotar` sacaba `len2 = NaN`, la guarda de longitud
        '    cero no muerde con NaN, y la normal salia (NaN,NaN,NaN) AL VBO. La matriz de ese ejemplo es
        '    una rotacion pura escalada: numero de condicion 1, perfectamente sana en algebra — pero no
        '    invertible EN SINGLE. Es un test de FINITUD de la reciproca, NO de buen condicionamiento:
        '    entre s = 2,28e-13 y s = 1,43e-13 el determinante ya es SUBNORMAL (pierde mantisa) y el
        '    predicado todavia dice SANA. Inofensivo en magnitud y fuera de cualquier rig real, pero no
        '    prometer mas de lo que hace.
        '    Se testea `1/det` y no un umbral porque `1/det` ES la cantidad que desborda: cualquier
        '    constante que eligiera seria una discusion, y esto es una medicion.
        ' ⛔ El oraculo NO puede cubrir estas bandas: se saltea por construccion todo |det| < DetSano
        ' (1e-9). Las cubre SOLO el corpus de `SelfTest`, en sus clases "MATRIZ ENORME" y "MATRIZ
        ' DIMINUTA" — si se tocan alla, estas tres guardas quedan sin barrido.
        If Not (t > 0.0F) Then Return True
        If Not (Math.Abs(det) <= Single.MaxValue) Then Return True
        If Not (Math.Abs(1.0F / det) <= Single.MaxValue) Then Return True
        ' SE DIVIDE, NO SE ELEVA AL CUBO. La forma natural del criterio es
        ' `det^2 <= epsRel^2 * (F2/3)^3`, y esa DESBORDA en Single: con una rotacion pura escalada por
        ' 3e6, `t^3` = 7,3e38 > Single.MaxValue = 3,4e38 ⇒ +Inf en LOS DOS lados, y `Inf <= Inf` es True.
        ' O sea que toda matriz con entradas RMS por encima de ~1,5e6 se clasificaba como DEGENERADA
        ' fuera cual fuera su determinante, y su normal salia sin transformar. Dividiendo primero, el
        ' cociente `det/t` tiene la magnitud de la escala (no de su cubo) y no hay nada que desborde.
        ' Es algebraicamente lo mismo —dividir los dos lados por t^2— y sigue sin pagar un `sqrt` por
        ' vertice. Verificado sobre las ocho clases de matriz, incluidas las dos que desbordaban.
        Dim q = det / t
        Return q * q <= (EpsDetRel * EpsDetRel) * t
    End Function

    ''' <summary>SOLO PARA MEDIR. Apaga el camino vectorial para poder compararlo contra el escalar EN EL
    ''' MISMO PROCESO: entre corridas esta maquina varia hasta 2x, asi que un A/B entre builds no puede
    ''' atribuir nada. No es un modo de la app — nadie lo escribe fuera del arnes.</summary>
    Friend Property ForzarEscalar As Boolean = False

    ''' <summary>SOLO PARA MEDIR. False = camino directo (el que gana, el default). True = el
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
    ''' <para><c>ThreadStatic</c> con construccion perezosa, no una fabrica que devuelve siempre el mismo
    ''' array: ese fue el bug de RecalcTBN, donde los "locales" no eran locales de nadie y el resultado
    ''' dependia de como cayeran los hilos.</para></summary>
    Private NotInheritable Class Scratch
        Public ReadOnly A(11)() As Single
        Public ReadOnly P(2)() As Single
        Public ReadOnly N(2)() As Single
        Public ReadOnly T(2)() As Single
        Public ReadOnly B(2)() As Single
        ''' <summary>Buffers de SALIDA del camino vectorial. Indexar un Vector(Of T) lane a lane
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

    ''' <summary>LA LEY, sobre UN vertice. La usa el camino SPARSE del upload (el de <=60 % de vertices
    ''' sucios), que no puede llamar a <see cref="TransformarDirecto"/> porque escribe por
    ''' <c>Marshal.Copy</c> a un buffer mapeado, indice por indice.
    ''' <para>PRECISION: la POSICION se acumula en Single y la NORMALIZACION es <c>1/MathF.Sqrt</c>, tambien
    ''' en Single. Ensanchar a Double aca mueve bytes del render sin cambiar el destino, que es un VBO de
    ''' floats.</para>
    ''' <para>⛔ EXISTE PARA QUE NO HAYA DOS LEYES EN EL MISMO RENDERER. El camino sparse NO puede llamar al
    ''' <c>Inverted().Transposed()</c> de OpenTK aunque el valor se parezca: la decision de DEGENERACION la
    ''' toma <see cref="EsDegenerada"/>, y si el sparse usara otra, el MISMO vertice saldria con la normal
    ''' Identidad o con la transformada segun cuantos vecinos se hubieran ensuciado ese frame — o sea segun
    ''' el gesto del usuario.</para></summary>
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
        If EsDegenerada(det, m.M11, m.M12, m.M13, m.M21, m.M22, m.M23, m.M31, m.M32, m.M33) Then
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
            ' T Y B VAN CON LA MATRIZ CRUDA, NO CON LA DE NORMALES. Son direcciones SOBRE
            ' la superficie: se mueven como se mueve la geometria. La inversa-transpuesta es la
            ' ley de la NORMAL y solo de ella — aplicarsela a la tangente la saca del plano
            ' tangente en cuanto hay shear (blend de dos rotaciones distintas, o sea todo
            ' vertice de codo, rodilla u hombro).
            ' SITIO GEMELO de SkinningHelper.PorMatriz3x3 (el bake) y de los DOS vertex shaders
            ' (FO4 y SSE). Si se separan, RENDER==BAKE se rompe en DOS canales: tangente Y bitangente.
            tan = Rotar(vt.X, vt.Y, vt.Z, m.M11, m.M12, m.M13, m.M21, m.M22, m.M23, m.M31, m.M32, m.M33)
            bit = Rotar(vb.X, vb.Y, vb.Z, m.M11, m.M12, m.M13, m.M21, m.M22, m.M23, m.M31, m.M32, m.M33)
        End If
    End Sub

    ''' <summary>EL CAMINO QUE GANA, y no es el vectorial. Trabaja DIRECTO sobre los arrays AoS: sin
    ''' aplanar a SoA, sin scatter, sin vectores.
    ''' <para>Medido en el mismo proceso, alternando rep a rep para que la deriva termica caiga igual en
    ''' los dos lados: vectorizar da 1,06x. La razon es que este bucle es MEMORY-BOUND, no compute-bound —
    ''' el staging que la vectorizacion necesita (VB no puede reinterpretar un Matrix4() como Single())
    ''' cuesta tanto como la aritmetica que ahorra. Sin staging, la misma ley escalar corre sobre los datos
    ''' donde ya estan.</para>
    ''' <para>La version vectorial se conserva igual: es una de las implementaciones contra las que el gate
    ''' compara bit a bit, y es lo que prueba que la ley por cofactores esta bien escrita.</para>
    ''' <para>LA POSICION TAMBIEN CAMBIO DE PRECISION, no solo la normal. El codigo anterior ensanchaba
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
        ' LAS 12 SECCIONES SE TOMAN UNA VEZ, FUERA DEL BUCLE. Cada `a0(i)` es entonces una lectura
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
                    If EsDegenerada(det, m11, m12, m13, m21, m22, m23, m31, m32, m33) Then
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
                        ' T Y B VAN CON LA MATRIZ CRUDA, NO CON LA DE NORMALES. Son direcciones SOBRE
                        ' la superficie: se mueven como se mueve la geometria. La inversa-transpuesta es la
                        ' ley de la NORMAL y solo de ella — aplicarsela a la tangente la saca del plano
                        ' tangente en cuanto hay shear (blend de dos rotaciones distintas, o sea todo
                        ' vertice de codo, rodilla u hombro).
                        ' SITIO GEMELO de SkinningHelper.PorMatriz3x3 (el bake) y de los DOS vertex shaders
                        ' (FO4 y SSE). Si se separan, RENDER==BAKE se rompe en DOS canales: tangente Y bitangente.
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

    ''' <summary>PUNTO DE ENTRADA. Recorre la malla por bloques; cada bloque se aplana a SoA y se resuelve
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
        ' LA MATRIZ NO SE APLANA: `mats` ya ES SoA, asi que esto son 12 `Array.Copy` de bloque entero
        ' (memmove contiguo), no 12 floats por vertice. La copia podria evitarse indexando
        ' `mats.Secciones(j)(desde + k)` directo; se conserva porque el scratch tiene tamano fijo `Bloque`
        ' y los indices del camino vectorial son locales al bloque.
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
            ' Es una OPTIMIZACION, no una guarda contra Nothing. `SkinningHelper.ExtractSkinnedGeometry`
            ' —el unico constructor de SkinnedGeometry— aloca Tangents y Bitangents a largo completo tenga
            ' o no tangentes el NIF, en las DOS ramas de su `If HasTangents`. Y el camino sparse de
            ' Render.vb los desreferencia por indice sea MSN o no: si alguna vez pudieran venir en
            ' Nothing, el NRE explota alla, y esta guarda no lo tapa.
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
            If EsDegenerada(det, m11, m12, m13, m21, m22, m23, m31, m32, m33) Then
                ' Degenerada -> Identidad, que es lo que devolvia NormalMatrixOrIdentity.
                c11 = 1.0F : c12 = 0.0F : c13 = 0.0F
                c21 = 0.0F : c22 = 1.0F : c23 = 0.0F
                c31 = 0.0F : c32 = 0.0F : c33 = 1.0F
                r = 1.0F
            End If

            ' La escala entra en los cofactores ANTES del producto punto: el camino viejo construia la
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
                ' T y B con la matriz CRUDA: son direcciones sobre la superficie. Ver TransformarDirecto.
                nrmOut(baseIdx + k) = Rotar(sc.N(0)(k), sc.N(1)(k), sc.N(2)(k), e11, e12, e13, e21, e22, e23, e31, e32, e33)
                tanOut(baseIdx + k) = Rotar(sc.T(0)(k), sc.T(1)(k), sc.T(2)(k), m11, m12, m13, m21, m22, m23, m31, m32, m33)
                bitOut(baseIdx + k) = Rotar(sc.B(0)(k), sc.B(1)(k), sc.B(2)(k), m11, m12, m13, m21, m22, m23, m31, m32, m33)
            End If
        Next
    End Sub

    ''' <summary>v (vector fila) por la matriz de normales, normalizado. La normalizacion se escribe aca y
    ''' no se delega a <c>Vector3.Normalize</c>: hay que poder transcribirla lane a lane.</summary>
    Private Function Rotar(vx0 As Single, vy0 As Single, vz0 As Single,
                           e11 As Single, e12 As Single, e13 As Single,
                           e21 As Single, e22 As Single, e23 As Single,
                           e31 As Single, e32 As Single, e33 As Single) As Vector3
        Dim vx = vx0 * e11 + vy0 * e21 + vz0 * e31
        Dim vy = vx0 * e12 + vy0 * e22 + vz0 * e32
        Dim vz = vx0 * e13 + vy0 * e23 + vz0 * e33
        ' LONGITUD CERO: SE DEVUELVE EL VECTOR TAL CUAL, igual que el canonico. `1/sqrt(0)` es +Inf
        ' y `0 * Inf` es NaN, asi que sin esta guarda un vertice con normal NULA salia con la normal en
        ' NaN al VBO. MEDIDO sobre el Serena Battle Suit: los 59 valores no finitos que el arnes venia
        ' reportando eran EXACTAMENTE esto —59 de 59, causa "normal de ENTRADA nula"— y no el corte por
        ' determinante, que es a lo que se los habia atribuido sin medir.
        ' Y ES RENDER != BAKE, con el bake del lado correcto: `SkinningHelper.NormalizaComoNifly` YA
        ' hace esto (`If l = 0 Then Return v`) porque nifly lo hace, y su doc lo explica: una normal nula
        ' no es basura ni un caso imposible —el CBBEBody.nif de CBBE trae 14— y el canonico la CONSERVA.
        ' El render era el unico que la convertia en NaN.
        Dim len2 = vx * vx + vy * vy + vz * vz
        If len2 = 0.0F Then Return New Vector3(vx, vy, vz)
        Dim inv = 1.0F / MathF.Sqrt(len2)
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
        ' El mismo predicado que EsDegenerada, en lanes: se comparan los CUADRADOS contra la cota de
        ' Hadamard escalada. `epsRel2` y `untercio` son los dos escalares que hacen falta.
        Dim epsRel2 As New SN.Vector(Of Single)(EpsDetRel * EpsDetRel)
        Dim untercio As New SN.Vector(Of Single)(1.0F / 3.0F)
        Dim uno = SN.Vector(Of Single).One
        Dim cero = SN.Vector(Of Single).Zero
        Dim maxV As New SN.Vector(Of Single)(Single.MaxValue)
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

            ' La rama del escalar se vuelve una MASCARA. La division corre igual en los lanes
            ' degenerados —da infinito— pero el select la descarta, y en punto flotante dividir por cero
            ' no lanza. Es lo que permite que no haya rama.
            ' TRANSCRIPCION LANE A LANE DE `EsDegenerada`. Si se toca alla, se toca aca: el
            ' SelfTest compara las dos formas justo sobre matrices degeneradas, que es donde se separan.
            Dim f2 = m11 * m11 + m12 * m12 + m13 * m13 +
                     m21 * m21 + m22 * m22 + m23 * m23 +
                     m31 * m31 + m32 * m32 + m33 * m33
            Dim tt = f2 * untercio
            ' TRANSCRIPCION EXACTA DE `EsDegenerada`, INCLUIDAS LAS TRES GUARDAS. ⛔ Van como NEGACION de
            ' un `GreaterThan`, NO como `LessThanOrEqual`: con NaN las dos comparaciones dan False, asi que
            ' `LessThanOrEqual(NaN, 0)` clasifica el lane como SANO mientras el escalar —que usa
            ' `Not (t > 0)`— lo da DEGENERADO. Y esa divergencia no es teorica: `Transformar` manda el
            ' prefijo alineado al vectorial y la COLA del mismo bloque al escalar, o sea que dentro de la
            ' MISMA malla el vertice se clasificaria distinto segun su indice mod W; y en una maquina sin
            ' aceleracion va todo por el escalar ⇒ RENDER != BAKE segun el hardware del usuario.
            Dim qq = det / tt
            Dim rec = uno / det
            Dim tSano = SN.Vector.GreaterThan(tt, SN.Vector(Of Single).Zero)
            Dim detSano = SN.Vector.LessThanOrEqual(SN.Vector.Abs(det), maxV)
            ' LA TERCERA GUARDA: `1/det` no finito. Ver la nota larga en `EsDegenerada` — mira el `s^3`
            ' hacia ABAJO, donde el que desborda no es `det` sino su reciproca, o sea justo el factor de
            ' los nueve cofactores. Va como un tercer `AndAlso` de la conjuncion sana, con la misma forma
            ' negada que las otras dos para que el NaN caiga del lado degenerado.
            Dim recSano = SN.Vector.LessThanOrEqual(SN.Vector.Abs(rec), maxV)
            Dim degen = SN.Vector.BitwiseOr(SN.Vector.LessThanOrEqual(qq * qq, epsRel2 * tt),
                                            SN.Vector.OnesComplement(
                                                SN.Vector.BitwiseAnd(SN.Vector.BitwiseAnd(tSano, detSano), recSano)))
            Dim r = SN.Vector.ConditionalSelect(degen, uno, rec)
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
                ' T y B con la matriz CRUDA (m11..m33), no con la de normales (e11..e33). Ver
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
        ' Misma guarda que el escalar: con longitud CERO el vector se devuelve tal cual. Sin el
        ' ConditionalSelect los lanes nulos salen NaN y divergen del escalar, que los conserva.
        Dim len2 = vx * vx + vy * vy + vz * vz
        Dim nulo = SN.Vector.Equals(len2, SN.Vector(Of Single).Zero)
        Dim inv = SN.Vector(Of Single).One / SN.Vector.SquareRoot(len2)
        rx = SN.Vector.ConditionalSelect(nulo, vx, vx * inv)
        ry = SN.Vector.ConditionalSelect(nulo, vy, vy * inv)
        rz = SN.Vector.ConditionalSelect(nulo, vz, vz * inv)
    End Sub

    ''' <summary>Corre LA FUNCION REAL por los dos caminos —vectorial y escalar— sobre los mismos datos
    ''' y compara BIT A BIT. Devuelve Nothing si pasa.
    ''' <para>Prueba la produccion, no una maqueta: un test que reimplementa el kernel al lado puede dar
    ''' verde mientras la funcion real diverge. Es la trampa #10 de 61-perf-simd-trampas.</para>
    ''' <para>Da veredicto AL ANCHO DE ESTA MAQUINA. Hay que correrlo TAMBIEN con
    ''' <c>DOTNET_MaxVectorTBitWidth=128</c>: un test que solo corre al ancho nativo no prueba nada del
    ''' otro (trampa #3).</para>
    ''' <para>El corpus incluye matrices DEGENERADAS a proposito: es donde el escalar tiene una rama y el
    ''' vectorial una mascara, o sea el unico lugar donde las dos formas pueden separarse.</para></summary>
    Friend Function SelfTest() As String
        ' DOS TAMANOS, y los dos importan.
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
        ' xorshift y no un congruencial: VB tiene el chequeo de desbordamiento ACTIVO, asi que el
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

            ' VALORES QUE NO SON NUMEROS NORMALES. ⛔ SIN ESTAS CLASES EL GATE PASA EN VACIO: un corpus de
            ' entradas solo en [-2, 2] no ejercita NUNCA la guarda de normal NULA de `Rotar` ni las de
            ' NaN / no-finito de `EsDegenerada`, que es justo donde el escalar y el vectorial se separan.
            ' Ese hueco ya dejo pasar una divergencia de NaN entre las dos guardas.
            ' Van cada 53 y cada 71 —primos entre si y con 37— para que las tres clases se crucen y ningun
            ' indice quede siempre en el mismo camino.
            If i Mod 53 = 0 Then
                ' NORMAL NULA: la fuente las trae de verdad (el CBBEBody.nif de CBBE tiene 14). Sin la
                ' guarda de `Rotar`, `1/sqrt(0)` = Inf y la normal sale NaN.
                ln(i) = New Vector3(0.0F, 0.0F, 0.0F)
            ElseIf i Mod 71 = 0 Then
                ' MATRIZ CON NaN: prueba que las dos guardas de EsDegenerada coinciden lane a lane.
                Dim mn = mats(i)
                mn.M22 = Single.NaN
                mats(i) = mn
            ElseIf i Mod 71 = 1 Then
                ' MATRIZ ENORME: el determinante desborda a +Inf mucho antes que f2.
                Dim mg = mats(i)
                mg.M11 = 10000000000000.0F : mg.M22 = 10000000000000.0F : mg.M33 = 10000000000000.0F
                mg.M12 = 0.0F : mg.M13 = 0.0F : mg.M21 = 0.0F : mg.M23 = 0.0F : mg.M31 = 0.0F : mg.M32 = 0.0F
                mats(i) = mg
            ElseIf i Mod 71 = 2 Then
                ' MATRIZ DIMINUTA: el SIMETRICO de la de arriba, y hace falta. Cubre la banda donde
                ' desborda la RECIPROCA —s entre ~1e-15 y 1,43e-13—, que sin esto no la ejercita nadie:
                ' con s = 1e-13 el det es 1e-39, las dos primeras guardas dicen SANA y `1/det` da +Inf.
                ' DOS escalas para las dos formas de romperse: 1e-13 desborda la reciproca con det todavia
                ' representable, y 1e-19 ademas hace underflow en `eps^2 * t`.
                Dim mp = mats(i)
                Dim sChica = If(i Mod 142 = 2, 0.0000000000001F, 0.0000000000000000001F)
                mp.M11 = sChica : mp.M22 = sChica : mp.M33 = sChica
                mp.M12 = 0.0F : mp.M13 = 0.0F : mp.M21 = 0.0F : mp.M23 = 0.0F : mp.M31 = 0.0F : mp.M32 = 0.0F
                mats(i) = mp
            End If
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
            ' Y el DISPATCHER completo, no solo los bloques sueltos: con N > Bloque hay un bloque parcial y
            ' un Parallel.For de verdad, o sea la aritmetica de offsets (desde, cuantos, baseIdx). Ahi es
            ' donde un off-by-one escribe el indice de otro.
            Dim pT(N - 1) As Vector3, nT(N - 1) As Vector3, tT(N - 1) As Vector3, bT(N - 1) As Vector3
            Transformar(mats, lv, ln, lt, lb, msn, N, pT, nT, tT, bT)
            ' ⛔ DOS RANGOS, Y CONFUNDIRLOS DA VERDE FALSO. `pE`/`pV` solo estan llenos en [0, MBlq) —son
            ' las llamadas directas a los bloques— pero `pD` y `pT` cubren [0, N). Comparando todo contra
            ' MBlq, los indices [MBlq, N) no se miran contra NADA, y ahi adentro esta el bloque PARCIAL
            ' entero, que es justo lo que N = Bloque + 3 vino a crear.
            ' Controles negativos que ese recorte dejaba pasar en VERDE: saltear el bloque parcial
            ' (`Parallel.For(0, nBloques-1)`), y hacer que TransformarDirecto —el camino de PRODUCCION—
            ' perdiera sus ultimos 6 vertices.
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

            ' ORACULO INDEPENDIENTE: ENTRADA FINITA ⇒ SALIDA FINITA.
            ' Todo lo de arriba compara los CUATRO caminos entre si, y por construccion no puede ver un
            ' defecto que los cuatro comparten. Asi se escapo el desborde de `1/det`: escalar y vectorial
            ' coincidian perfectamente... los dos en NaN. Un gate que solo cruza implementaciones mide
            ' consistencia, no correccion; hace falta al menos una afirmacion sobre el RESULTADO.
            ' La ley es la mas debil que igual muerde: si la matriz y los vectores de entrada son finitos,
            ' la normal, la tangente y la bitangente tienen que serlo. Con una matriz degenerada el kernel
            ' cae a Identidad, y con una normal nula `Rotar` la devuelve tal cual — las dos salidas son
            ' finitas, asi que la ley vale para TODO el corpus salvo los indices con NaN de entrada, donde
            ' propagar el NaN es lo correcto.
            For i = 0 To N - 1
                ' LA POSICION ENTRA A LOS DOS LADOS, no solo la normal/tangente/bitangente: se escribe al
                ' MISMO VBO y un +Inf ahi manda el vertice al infinito. Con este corpus no es alcanzable
                ' —la traslacion (m41..m43) se sortea en [-2,2), igual que `lv`, asi que el peor caso es la
                ' clase de escala 1e13: 2*1e13 + 2 ~ 2e13, comodamente finito— pero es gratis cubrirlo
                ' antes de que alguien amplie el corpus.
                ' ⛔ Y m41..m43 NO son cero: el ctor de Matrix4 pone tres `sig()` en la cuarta fila.
                If Not (FinitaM(mats(i)) AndAlso Finito3d(lv(i)) AndAlso Finito3(ln(i)) AndAlso
                        Finito3(lt(i)) AndAlso Finito3(lb(i))) Then Continue For
                If Finito3(pD(i)) AndAlso Finito3(nD(i)) AndAlso Finito3(tD(i)) AndAlso Finito3(bD(i)) Then Continue For
                Return $"msn={msn} vertice {i}: con matriz y vectores de ENTRADA finitos, la salida NO es " &
                       $"finita (p={pD(i)} n={nD(i)} t={tD(i)} b={bD(i)}). La matriz era " &
                       $"[{mats(i).M11};{mats(i).M12};{mats(i).M13} | {mats(i).M21};{mats(i).M22};{mats(i).M23} | " &
                       $"{mats(i).M31};{mats(i).M32};{mats(i).M33} | T {mats(i).M41};{mats(i).M42};{mats(i).M43}], det={DetPorPrimeraFila(mats(i).M11, mats(i).M12, mats(i).M13, mats(i).M21, mats(i).M22, mats(i).M23, mats(i).M31, mats(i).M32, mats(i).M33)}."
            Next

            ' Y AHORA EL RANGO COMPLETO [0, N), con `pD` de referencia: es el unico que esta lleno en
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
            ' HASTA MBlq, no hasta N: `pE` y `pV` salen de llamar a los bloques DIRECTO con MBlq vertices,
            ' asi que de MBlq en adelante los dos estan en cero y comparar seria comparar ceros contra
            ' ceros. Los indices [MBlq, N) los cubren las comparaciones contra `pD`/`pT`/UnVertice de
            ' arriba, que si estan llenas en todo el rango.
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

            ' TAMANOS CHICOS: las DOS ramas de despacho que N = Bloque + 3 no toca nunca.
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

        ' Y AHORA EL ORACULO INDEPENDIENTE. Todo lo de arriba es un gate de CONSISTENCIA: prueba que las
        ' cuatro transcripciones digan lo mismo, no que digan lo CORRECTO. Dos controles negativos que
        ' pasan en VERDE por este lado y solo caza el oraculo: cambiar `r = 1/det` por `r = -1/det` en las
        ' cuatro —o sea NEGAR la matriz de normales, que en pantalla es el modelo iluminado del lado
        ' equivocado—, y subir el corte a 1e-3, que manda a Identidad matrices perfectamente sanas.
        Dim oraculo = OraculoDeLaLey()
        If oraculo IsNot Nothing Then Return oraculo
        Return Nothing
    End Function

    ''' <summary>EL UMBRAL DE SANIDAD DEL ORACULO, y es a proposito que NO sea <see cref="EpsDetRel"/>. Es el
    ''' contrato que el oraculo afirma: toda matriz con determinante de este orden o mayor tiene que salir
    ''' TRANSFORMADA, no colapsada a Identidad.
    ''' <para>⛔ Si esto dijera <c>EpsDetRel</c>, el oraculo seria CIRCULAR — subir el corte agrandaria a la
    ''' vez el conjunto de matrices maltratadas y el de matrices que el oraculo se saltea. Verificado: con
    ''' el corte en 1e-3 y este umbral atado a el, el control negativo pasaba en verde.</para></summary>
    Private Const DetSano As Single = 0.000000001F

    ''' <summary>Verifica el INVARIANTE de la matriz de normales, no su mecanismo. No conoce los
    ''' cofactores, ni el determinante, ni el corte: lo unico que usa es la multiplicacion
    ''' matriz-por-vector cruda, escrita a mano aca. Por eso puede contradecir a las cuatro transcripciones
    ''' a la vez.
    ''' <para>EL INVARIANTE. Si <c>G</c> es la matriz de normales de <c>M</c>, entonces por definicion
    ''' <c>G = (M⁻¹)ᵀ</c> y salen dos identidades EXACTAS en algebra, sin importar como se calcule G:
    ''' <list type="number">
    ''' <item><b>FORMA</b> — para todo <c>t</c> con <c>n·t = 0</c>: <c>(G n) · (M t) = nᵀ M⁻¹ M t = n·t = 0</c>.
    ''' O sea: la normal transformada sigue perpendicular a la superficie transformada. Es LA propiedad por
    ''' la que existe la matriz de normales, y una ley mal escrita la rompe.</item>
    ''' <item><b>ORIENTACION</b> — <c>(G n) · (M n) = nᵀ M⁻¹ M n = n·n &gt; 0</c>. Estrictamente positivo,
    ''' pase lo que pase con el signo del determinante. Esto es lo que caza la normal negada, que la
    ''' perpendicularidad sola no ve (−N tambien es perpendicular).</item>
    ''' </list></para>
    ''' <para>ES CON TOLERANCIA Y TIENE QUE SERLO: compara dos cuentas distintas en Single, no dos
    ''' transcripciones de una. Un umbral bit a bit aca daria rojo permanente. El umbral se fijo midiendo el
    ''' peor caso real del corpus y dejando ~10x de margen; el corpus reporta cuantas matrices evaluo para
    ''' que no pueda pasar en vacio.</para>
    ''' <para>Se saltean las MAL CONDICIONADAS. Con matrices aleatorias en [-2,2] las hay casi
    ''' singulares, y ahi el invariante se cumple en algebra exacta pero se pierde en el redondeo de Single:
    ''' rechazarlas por condicionamiento es correcto, taparlas con un umbral flojo no.</para></summary>
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
        ' EL CORPUS TIENE DOS MITADES, Y LA SEGUNDA NO ES DECORATIVA.
        ' La primera es aleatoria, y sirve para la forma general de la ley. Pero un corpus aleatorio en
        ' [-2,2] tiene determinantes de orden 1: NINGUNA de sus matrices se acerca al corte por degeneracion,
        ' asi que mover el corte no le cambia una sola normal. Verificado corriendolo: con el corte subido a
        ' 1e-3 —que manda a Identidad a cualquier hueso con escala menor a 0,1— el oraculo con solo la mitad
        ' aleatoria daba PASS, porque el filtro de condicionamiento de mas abajo se come exactamente la
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
            ' DOS FILTROS, Y EL SEGUNDO NO PUEDE MENCIONAR `EpsDetRel`.
            '  (1) CONDICIONAMIENTO, relativo: casi singular respecto de su propia escala. Ahi el invariante
            '      se cumple en algebra exacta pero se pierde en el redondeo de Single, asi que juzgarla
            '      seria acusar al kernel del error del instrumento.
            '  (2) SANIDAD, absoluto y PROPIO DE ESTE ORACULO: `DetSano`. Si el oraculo se salteara todo lo
            '      que la ley llama degenerado —o sea si mirara `EpsDetRel`— seria circular: subirlo
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

            ' (3) LA TANGENTE VA CON LA MATRIZ CRUDA, y sin este chequeo el kernel estuvo aplicandole
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

        ' Sin esto el oraculo pasa en vacio: si el filtro de condicionamiento se comiera todo el corpus,
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

    ''' <summary>Las tres componentes finitas. Para el oraculo "entrada finita ⇒ salida finita" del
    ''' <see cref="SelfTest"/>.</summary>
    Private Function Finito3(v As Vector3) As Boolean
        Return Single.IsFinite(v.X) AndAlso Single.IsFinite(v.Y) AndAlso Single.IsFinite(v.Z)
    End Function

    ''' <summary>Idem para la posicion, que viene en Double.</summary>
    Private Function Finito3d(v As Vector3d) As Boolean
        Return Double.IsFinite(v.X) AndAlso Double.IsFinite(v.Y) AndAlso Double.IsFinite(v.Z)
    End Function

    ''' <summary>Las DOCE entradas que el kernel lee: la 3x3 y la TRASLACION.
    ''' <para>Miraba solo nueve. La precondicion del oraculo dice "matriz de entrada finita" y la
    ''' posicion usa `m41..m43`, asi que un `M41 = +Inf` en el corpus daba "entrada finita", posicion `+Inf`
    ''' y ROJO FALSO — y el mensaje de diagnostico, que imprime solo M11..M33, ni siquiera mostraba la
    ''' causa. Es exactamente el escenario que el cambio dice anticipar ("antes de que alguien amplie el
    ''' corpus"), con la guarda incompleta para recibirlo.</para></summary>
    Private Function FinitaM(m As Matrix4) As Boolean
        Return Single.IsFinite(m.M11) AndAlso Single.IsFinite(m.M12) AndAlso Single.IsFinite(m.M13) AndAlso
               Single.IsFinite(m.M21) AndAlso Single.IsFinite(m.M22) AndAlso Single.IsFinite(m.M23) AndAlso
               Single.IsFinite(m.M31) AndAlso Single.IsFinite(m.M32) AndAlso Single.IsFinite(m.M33) AndAlso
               Single.IsFinite(m.M41) AndAlso Single.IsFinite(m.M42) AndAlso Single.IsFinite(m.M43)
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
