Imports System.Numerics
Imports OpenTK.Mathematics

''' <summary>
''' Espejo vectorial del blend de matrices de skin. Es el gemelo de <see cref="FastPow"/> para
''' geometria: mismo criterio de ancho variable (<c>Vector(Of T)</c> elige 256 / 128 / escalar segun
''' la maquina) y mismo contrato de bit-exactitud.
'''
''' <para>POR QUE ACA SI Y EN EL RECALCULO DE TBN NO. El kernel del skinning es
''' <c>acc += palette(idx) * w</c> sobre una matriz 4x4, o sea <b>16 doubles CONTIGUOS</b>: un unico
''' indice indirecto lleva a un bloque contiguo ⇒ cargas vectoriales seguidas, sin gather, sin
''' transposicion AoS↔SoA y <b>sin cola</b> (16 es divisible por 8, 4, 2 y 1, o sea por CUALQUIER
''' <c>Vector(Of Double).Count</c> posible). Ademas la paleta son 20-60 matrices = 2,5-7,5 KB, o sea
''' que vive en L1. El TBN, en cambio, trabaja sobre <c>Vector3d</c> en AoS con indices de triangulo
''' dispersos: ahi el ancho se va en transponer y no queda ganancia.</para>
'''
''' <para>Y POR ESO EL FALLBACK ANGOSTO ACA SI GANA. Con 2 lanes (SSE2) el cuerpo sigue siendo
''' la mitad de las operaciones del escalar, porque no hay transposicion que amortizar. En el TBN,
''' 2 lanes de double con transposicion queda POR DEBAJO del escalar — que es exactamente el modo de
''' falla que ya se pago una vez con FastPow antes de pasarlo a ancho variable.</para>
'''
''' <para><b>ESTE MODULO NO ES UNA LEY NUEVA.</b> A diferencia de <see cref="FastPow"/>, que PASO
''' A SER la ley y movio 2.881 bytes, aca el camino vectorial es un espejo EXACTO del escalar y no
''' puede mover un bit. Lo que lo garantiza:</para>
''' <list type="bullet">
''' <item>La lane <c>k</c> siempre acumula el elemento <c>k</c> ⇒ mismo orden de sumas por elemento,
''' cero reasociacion, cero reduccion entre lanes.</item>
''' <item><b>NUNCA <c>Vector.FusedMultiplyAdd</c>.</b> El JIT de .NET no contrae <c>a + b*c</c>
''' por su cuenta, asi que <c>acc + vm * vw</c> es multiply-then-add igual que el escalar. Un FMA
''' "de optimizacion" redondea UNA sola vez y cambia los bits en silencio.</item>
''' <item>Las guardas (peso &gt; 0, indice en rango) se quedan ESCALARES en el llamador, afuera del
''' cuerpo vectorial. No hay una sola mascara por lane ⇒ las trampas del orden de los
''' <c>ConditionalSelect</c> y de NaN (61-perf-simd-trampas #4 y #5) NO APLICAN acá.</item>
''' </list>
'''
''' <para><b>POR QUE TODO PASA POR ARRAYS PLANOS DE <c>Double</c> Y NO POR <c>Matrix4d</c>.</b>
''' VB.NET no soporta ref structs en NINGUNA posicion — ni parametro, ni retorno, ni variable local
''' (BC30668 / BC30643) — asi que <c>Span(Of T)</c> y todo <c>MemoryMarshal</c> estan fuera de
''' alcance y no hay forma de ver un <c>Matrix4d</c> como 16 doubles sin copiarlo. Lo que si tiene
''' <c>Vector(Of T)</c> son constructor y <c>CopyTo</c> <b>sobre arrays</b>, y sobre eso se apoya
''' todo esto. La paleta plana se arma UNA vez por shape (20-60 matrices), no por vertice, asi que
''' esa copia no esta en el camino caliente.</para>
''' </summary>
Public Module FastGeom

    ''' <summary>Doubles que ocupa una matriz 4x4. Es constante de FORMATO, no de ancho SIMD.</summary>
    Public Const MatDoubles As Integer = 16

    ''' <summary>Lanes de double del ancho que eligio el runtime (8 = AVX-512, 4 = AVX2, 2 = SSE2,
    ''' 1 = sin aceleracion). Todos dividen a 16 ⇒ ningun loop de matriz lleva cola.</summary>
    Public ReadOnly Property LaneCountD As Integer
        Get
            Return Vector(Of Double).Count
        End Get
    End Property

    ''' <summary>True cuando conviene el camino vectorial. Con <c>Count = 1</c> el "vector" es un
    ''' double suelto y el escalar directo es mejor (menos overhead, mismo resultado). La guarda de
    ''' divisibilidad no es paranoia gratuita: si alguna vez <c>Count</c> superara 16, el loop
    ''' <c>e += n</c> leeria fuera del bloque de la matriz.</summary>
    Public ReadOnly Property Accelerated As Boolean
        Get
            Dim n = Vector(Of Double).Count
            Return Vector.IsHardwareAccelerated AndAlso n >= 2 AndAlso n <= MatDoubles AndAlso (MatDoubles Mod n) = 0
        End Get
    End Property

    ''' <summary>Descripcion del ancho activo, para el mensaje del gate.</summary>
    Public ReadOnly Property WidthInfo As String
        Get
            Return $"Vector(Of Double).Count={Vector(Of Double).Count} accelerated={Vector.IsHardwareAccelerated}"
        End Get
    End Property

    ' ============================================================================================
    '  Conversion Matrix4d <-> plano. Escalar A PROPOSITO: corre una vez por HUESO (20-60) al armar
    '  la paleta, y una vez por vertice al devolver el resultado. No esta en el bucle de slots.
    ' ============================================================================================

    ''' <summary>Vuelca una <see cref="Matrix4d"/> en <paramref name="dst"/> a partir de
    ''' <paramref name="off"/>, en orden row-major (M11..M14, M21..M24, M31..M34, M41..M44).</summary>
    Public Sub StoreMatrix(m As Matrix4d, dst As Double(), off As Integer)
        dst(off + 0) = m.M11 : dst(off + 1) = m.M12 : dst(off + 2) = m.M13 : dst(off + 3) = m.M14
        dst(off + 4) = m.M21 : dst(off + 5) = m.M22 : dst(off + 6) = m.M23 : dst(off + 7) = m.M24
        dst(off + 8) = m.M31 : dst(off + 9) = m.M32 : dst(off + 10) = m.M33 : dst(off + 11) = m.M34
        dst(off + 12) = m.M41 : dst(off + 13) = m.M42 : dst(off + 14) = m.M43 : dst(off + 15) = m.M44
    End Sub

    ''' <summary>Reconstruye una <see cref="Matrix4d"/> desde el bloque plano.</summary>
    Public Function LoadMatrix(src As Double(), off As Integer) As Matrix4d
        Return New Matrix4d(
            src(off + 0), src(off + 1), src(off + 2), src(off + 3),
            src(off + 4), src(off + 5), src(off + 6), src(off + 7),
            src(off + 8), src(off + 9), src(off + 10), src(off + 11),
            src(off + 12), src(off + 13), src(off + 14), src(off + 15))
    End Function

    ''' <summary>Arma la paleta plana de una lista de matrices. UNA vez por shape.</summary>
    ''' <summary>Elementos por matriz en la paleta de SINGLE. Son 16 y no 12 a proposito: 12 no es
    ''' divisible por 8 (el ancho de <c>Vector(Of Single)</c> con AVX2) y el bucle llevaria cola, que
    ''' es justo lo que este layout existe para evitar. 16 divide a 16/8/4/2/1.</summary>
    Public Const MatSingles As Integer = 16

    ''' <summary>Lanes de SINGLE del ancho que eligio el runtime. El doble que en Double.</summary>
    Public ReadOnly Property LaneCountS As Integer
        Get
            Return Vector(Of Single).Count
        End Get
    End Property

    ''' <summary>La paleta de huesos en SINGLE y plana. Es la version que usa el blend.
    ''' <para>POR QUE SINGLE. Con <c>Matrix4d</c> la paleta ocupa 128 B por hueso; el Serena Battle
    ''' Suit tiene 293 huesos = <b>37,5 KB</b>, o sea que NO ENTRA EN L1 (32 KB). Cada vertice hace 4
    ''' accesos dispersos y cada uno cruza dos lineas de cache: el blend termina limitado por memoria
    ''' y no por aritmetica. En Single son 64 B por hueso = <b>18,7 KB</b>, entra holgado, y ademas el
    ''' vector procesa 8 lanes en vez de 4.</para>
    ''' <para>CAMBIA BYTES HORNEADOS. La paleta se redondea a Single ANTES del blend y la
    ''' acumulacion pasa a Single, asi que el resultado difiere en el ultimo bit de la mantisa. El
    ''' destino final siempre fue un Single —<c>SkinMatricesSoA</c> guarda floats y el VBO tambien—
    ''' pero el redondeo ahora ocurre antes. Hecho con autorizacion expresa del usuario.</para>
    ''' </summary>
    Public Function BuildFlatPaletteS(mats As Matrix4d()) As Single()
        If mats Is Nothing OrElse mats.Length = 0 Then Return Array.Empty(Of Single)()
        Dim flat(mats.Length * MatSingles - 1) As Single
        For k As Integer = 0 To mats.Length - 1
            Dim m = mats(k)
            Dim o = k * MatSingles
            flat(o) = CSng(m.M11) : flat(o + 1) = CSng(m.M12) : flat(o + 2) = CSng(m.M13) : flat(o + 3) = CSng(m.M14)
            flat(o + 4) = CSng(m.M21) : flat(o + 5) = CSng(m.M22) : flat(o + 6) = CSng(m.M23) : flat(o + 7) = CSng(m.M24)
            flat(o + 8) = CSng(m.M31) : flat(o + 9) = CSng(m.M32) : flat(o + 10) = CSng(m.M33) : flat(o + 11) = CSng(m.M34)
            flat(o + 12) = CSng(m.M41) : flat(o + 13) = CSng(m.M42) : flat(o + 14) = CSng(m.M43) : flat(o + 15) = CSng(m.M44)
        Next
        Return flat
    End Function

    ''' <summary>El kernel en SINGLE. Misma ley que <see cref="BlendInto"/> — chunk afuera, slot
    ''' adentro— con el doble de lanes.</summary>
    ''' <param name="escala">Se aplica al acumulador ANTES de guardarlo. 1 = sin escalar.
    ''' <para>ESTA FUSIONADO A PROPOSITO. Antes el caller llamaba <c>ScaleAccS</c> despues, y eso era una
    ''' SEGUNDA PASADA completa sobre el acumulador: releer 16 floats, multiplicar, reescribir 16. MEDIDO
    ''' sobre el Serena Battle Suit: ese paso solo costaba <b>0,68 ms</b> de un blend de 5,2, o sea el 13 %,
    ''' por multiplicar 16 numeros. Fusionado, el escalado ocurre con el acumulador todavia en registro.</para>
    ''' <para>MISMOS BITS: la suma se completa igual y recien despues se multiplica, exactamente como
    ''' hacia el par BlendInto+ScaleAcc. No es un FMA ni un reordenamiento.</para></param>
    <Runtime.CompilerServices.MethodImpl(Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>
    Public Sub BlendIntoS(flatPal As Single(), idxBuf As Integer(), wBuf As Single(), nUsed As Integer, accBuf As Single(),
                          Optional escala As Single = 1.0F)
        If Not AcceleratedS Then
            BlendIntoScalarS(flatPal, idxBuf, wBuf, nUsed, accBuf, escala)
            Return
        End If
        Dim n As Integer = Vector(Of Single).Count
        Dim vs As New Vector(Of Single)(escala)
        Dim escalar As Boolean = (escala <> 1.0F)
        Dim e As Integer = 0
        Do While e < MatSingles
            Dim acc As Vector(Of Single) = Vector(Of Single).Zero
            For j As Integer = 0 To nUsed - 1
                Dim vm As New Vector(Of Single)(flatPal, idxBuf(j) * MatSingles + e)
                Dim vw As New Vector(Of Single)(wBuf(j))
                ' multiply-then-add EXPLICITO. No cambiar por Vector.FusedMultiplyAdd: el JIT no
                ' contrae `a + b*c` solo, asi que esto es bit a bit lo mismo que el escalar de abajo.
                acc = acc + vm * vw
            Next
            If escalar Then acc = acc * vs
            acc.CopyTo(accBuf, e)
            e += n
        Loop
    End Sub

    ''' <summary>Referencia escalar de <see cref="BlendIntoS"/>. Es el otro lado del gate.</summary>
    Public Sub BlendIntoScalarS(flatPal As Single(), idxBuf As Integer(), wBuf As Single(), nUsed As Integer, accBuf As Single(),
                                Optional escala As Single = 1.0F)
        Dim escalar As Boolean = (escala <> 1.0F)
        For e As Integer = 0 To MatSingles - 1
            Dim acc As Single = 0.0F
            For j As Integer = 0 To nUsed - 1
                acc += flatPal(idxBuf(j) * MatSingles + e) * wBuf(j)
            Next
            If escalar Then acc = acc * escala
            accBuf(e) = acc
        Next
    End Sub

    ''' <summary><c>accBuf *= s</c> en Single.</summary>
    <Runtime.CompilerServices.MethodImpl(Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>
    Public Sub ScaleAccS(accBuf As Single(), s As Single)
        If Not AcceleratedS Then
            For q As Integer = 0 To MatSingles - 1
                accBuf(q) = accBuf(q) * s
            Next
            Return
        End If
        Dim n As Integer = Vector(Of Single).Count
        Dim vs As New Vector(Of Single)(s)
        Dim e As Integer = 0
        Do While e < MatSingles
            Dim va As New Vector(Of Single)(accBuf, e)
            Dim vr As Vector(Of Single) = va * vs
            vr.CopyTo(accBuf, e)
            e += n
        Loop
    End Sub

    ''' <summary>SOLO PARA EL GATE: apaga el camino vectorial del blend. Antes el self-test forzaba el
    ''' escalar pasando <c>flatPal:=Nothing</c>, lo que hacia caer en un camino que acumulaba en Matrix4d
    ''' (Double) — o sea que comparaba dos LEYES distintas, no dos implementaciones de una. Con la paleta en
    ''' Single ese contraste dejo de ser valido y el toggle es la forma correcta: los dos caminos leen los
    ''' MISMOS datos y tienen que dar bit a bit lo mismo.</summary>
    ''' <summary>Fuerza el camino escalar del kernel de Single. SOLO PARA COMPARAR el vectorial contra
    ''' el escalar; no es un modo de la app.
    '''
    ''' <para>ES POR HILO (<c>ThreadStatic</c>), Y ESO NO ES UN DETALLE. Quien la enciende es
    ''' <c>SkinningHelper.SkinningSimdSelfTest</c>, que corre EN EL PROCESO DEL USUARIO: el gate
    ''' <c>skin-blend</c> antes de un bake de FaceGen y <c>EnsureSkinSimdGate</c> antes del primer NIF de
    ''' Wardrobe Manager. Son ~1500 encendidos y apagados. Siendo una global de proceso, cualquier OTRO
    ''' hilo que estuviera blendeando en esa ventana —el <c>Parallel.ForEach</c> de
    ''' <c>FillPerVertexSkinMatrix</c>, o el hilo de UI dibujando el preview mientras el bake corre en
    ''' background— se iba al camino escalar sin que nadie se lo pidiera. Por hilo, el self-test solo se
    ''' afecta a si mismo.</para>
    '''
    ''' <para>Funciona porque el self-test llama a <c>BlendBoneMatrices</c> DIRECTO, un vertice por vez, en
    ''' su propio hilo: no hay pool que tenga que ver el flag. Si algun dia alguien la enciende
    ''' alrededor de un bucle paralelo, los workers NO la van a ver — y eso es lo correcto, pero hay que
    ''' saberlo.</para></summary>
    <ThreadStatic>
    Private _forzarEscalarS As Boolean

    Public Property ForzarEscalarS As Boolean
        Get
            Return _forzarEscalarS
        End Get
        Set(value As Boolean)
            _forzarEscalarS = value
        End Set
    End Property

    ''' <summary>Igual que <see cref="Accelerated"/> pero para el ancho de Single.</summary>
    Public ReadOnly Property AcceleratedS As Boolean
        Get
            Dim n = Vector(Of Single).Count
            Return Not ForzarEscalarS AndAlso Vector.IsHardwareAccelerated AndAlso n >= 2 AndAlso
                   n <= MatSingles AndAlso (MatSingles Mod n) = 0
        End Get
    End Property

    ''' <summary>Lee una Matrix4d desde un acumulador de Single.</summary>
    Public Function LoadMatrixS(buf As Single(), o As Integer) As Matrix4d
        Return New Matrix4d(buf(o), buf(o + 1), buf(o + 2), buf(o + 3),
                            buf(o + 4), buf(o + 5), buf(o + 6), buf(o + 7),
                            buf(o + 8), buf(o + 9), buf(o + 10), buf(o + 11),
                            buf(o + 12), buf(o + 13), buf(o + 14), buf(o + 15))
    End Function

    Public Function BuildFlatPalette(mats As Matrix4d()) As Double()
        If mats Is Nothing OrElse mats.Length = 0 Then Return Array.Empty(Of Double)()
        Dim flat(mats.Length * MatDoubles - 1) As Double
        For k As Integer = 0 To mats.Length - 1
            StoreMatrix(mats(k), flat, k * MatDoubles)
        Next
        Return flat
    End Function

    ' ============================================================================================
    '  EL KERNEL
    ' ============================================================================================

    ''' <summary>
    ''' <c>accBuf(0..15) = Σ_j flatPal(idxBuf(j)*16 + e) * wBuf(j)</c>, partiendo de cero.
    '''
    ''' <para>El recorrido es CHUNK AFUERA / SLOT ADENTRO: para cada bloque de <c>LaneCount</c>
    ''' elementos de la matriz se recorren los 4 slots de hueso. Asi el acumulador se queda en un
    ''' REGISTRO vectorial durante los 4 slots y toca memoria una sola vez, al final del chunk. Al
    ''' reves (slot afuera) habria que releer y reescribir el acumulador entero 4 veces.</para>
    '''
    ''' <para>El orden de suma por elemento es el mismo que el del escalar —
    ''' <c>0 + p0·w0 + p1·w1 + …</c>, de izquierda a derecha— asi que el resultado es bit-identico.
    ''' Los pares (indice, peso) llegan YA FILTRADOS por el llamador, en el mismo orden en que el
    ''' escalar los recorreria: las guardas no viven aca.</para>
    ''' </summary>
    ''' <param name="flatPal">Paleta plana, <c>16 × cantidadDeMatrices</c> doubles.</param>
    ''' <param name="idxBuf">Indices de matriz ya validados contra el largo de la paleta.</param>
    ''' <param name="wBuf">Pesos, alineados con <paramref name="idxBuf"/>.</param>
    ''' <param name="nUsed">Cuantas entradas de los dos buffers son validas.</param>
    ''' <param name="accBuf">Destino, al menos 16 doubles. Se sobrescribe entero.</param>
    Public Sub BlendInto(flatPal As Double(), idxBuf As Integer(), wBuf As Double(), nUsed As Integer, accBuf As Double())
        If Not Accelerated Then
            BlendIntoScalar(flatPal, idxBuf, wBuf, nUsed, accBuf)
            Return
        End If
        Dim n As Integer = Vector(Of Double).Count
        Dim e As Integer = 0
        Do While e < MatDoubles
            Dim acc As Vector(Of Double) = Vector(Of Double).Zero
            For j As Integer = 0 To nUsed - 1
                Dim vm As New Vector(Of Double)(flatPal, idxBuf(j) * MatDoubles + e)
                Dim vw As New Vector(Of Double)(wBuf(j))
                ' multiply-then-add EXPLICITO. No cambiar por Vector.FusedMultiplyAdd.
                acc = acc + vm * vw
            Next
            acc.CopyTo(accBuf, e)
            e += n
        Loop
    End Sub

    ''' <summary>Referencia escalar de <see cref="BlendInto"/> — la ley, elemento por elemento.
    ''' Produccion solo la usa cuando no hay SIMD; su razon de existir es que
    ''' <see cref="VectorParitySelfTest"/> pueda comparar los dos caminos EN EL MISMO PROCESO.
    ''' No borrar por "codigo duplicado": es el gate.</summary>
    Public Sub BlendIntoScalar(flatPal As Double(), idxBuf As Integer(), wBuf As Double(), nUsed As Integer, accBuf As Double())
        For e As Integer = 0 To MatDoubles - 1
            Dim acc As Double = 0.0
            For j As Integer = 0 To nUsed - 1
                acc += flatPal(idxBuf(j) * MatDoubles + e) * wBuf(j)
            Next
            accBuf(e) = acc
        Next
    End Sub

    ''' <summary><c>accBuf *= s</c> (el <c>result * (1.0 / sumW)</c> del blend).</summary>
    Public Sub ScaleAcc(accBuf As Double(), s As Double)
        If Not Accelerated Then
            ScaleAccScalar(accBuf, s)
            Return
        End If
        Dim n As Integer = Vector(Of Double).Count
        Dim vs As New Vector(Of Double)(s)
        Dim e As Integer = 0
        Do While e < MatDoubles
            Dim va As New Vector(Of Double)(accBuf, e)
            Dim vr As Vector(Of Double) = va * vs
            vr.CopyTo(accBuf, e)
            e += n
        Loop
    End Sub

    ''' <summary>Referencia escalar de <see cref="ScaleAcc"/>. Ver la nota de <see cref="BlendIntoScalar"/>.</summary>
    Public Sub ScaleAccScalar(accBuf As Double(), s As Double)
        For e As Integer = 0 To MatDoubles - 1
            accBuf(e) *= s
        Next
    End Sub

    ' ============================================================================================
    '  SELF-TEST
    ' ============================================================================================

    ''' <summary>PRNG determinista propio: el veredicto del gate no puede cambiar entre corridas
    ''' (00-reglas-app-distribuida). xorshift64.</summary>
    Private _rng As ULong

    Private Function NextBits() As ULong
        _rng = _rng Xor (_rng << 13)
        _rng = _rng Xor (_rng >> 7)
        _rng = _rng Xor (_rng << 17)
        Return _rng
    End Function

    ''' <summary>Rango amplio, con signo y con exponentes variados: el objetivo es sacudir el
    ''' redondeo del ultimo bit, y un [0,1) no lo hace.</summary>
    Private Function NextDouble() As Double
        Dim b = NextBits()
        Dim mant As Double = CDbl(b And &H1FFFFFFFFFFFFFUL) / CDbl(&H20000000000000UL)
        Dim expo As Integer = CInt(b >> 58) - 16
        Dim sgn As Double = If((b And &H100000000UL) = 0UL, 1.0, -1.0)
        Return sgn * (0.5 + mant) * Math.Pow(2.0, expo)
    End Function

    ''' <summary>Indice del primer elemento que difiere EN BITS, o -1 si son identicos.
    ''' Compara bits, no valores: <c>=</c> daria por iguales a +0.0 y -0.0, y el signo del cero
    ''' es un bit que despues sale escrito al NIF.</summary>
    Private Function FirstBitDiff(a As Double(), b As Double()) As Integer
        For e As Integer = 0 To MatDoubles - 1
            If BitConverter.DoubleToInt64Bits(a(e)) <> BitConverter.DoubleToInt64Bits(b(e)) Then Return e
        Next
        Return -1
    End Function

    ''' <summary>
    ''' Vectorial == escalar, BIT A BIT, para el blend y el escalado, incluyendo la cadena completa
    ''' de 4 slots + normalizacion (que es la forma REAL del kernel, no una operacion suelta).
    ''' Devuelve "" si pasa.
    '''
    ''' <para>Compara los dos caminos EN EL MISMO PROCESO, asi que da veredicto al ancho que tenga
    ''' la maquina. Para cubrir la portabilidad hay que correrlo TAMBIEN con
    ''' <c>DOTNET_MaxVectorTBitWidth=128</c> (o <c>DOTNET_EnableAVX2=0 DOTNET_EnableAVX=0</c>): un
    ''' test que solo corre al ancho nativo no prueba nada del otro (61-perf-simd-trampas #3).</para>
    ''' </summary>
    Public Function VectorParitySelfTest() As String
        _rng = &H9E3779B97F4A7C15UL   ' semilla FIJA

        Const nMats As Integer = 8
        Dim mats(nMats - 1) As Matrix4d
        For k As Integer = 0 To nMats - 1
            Dim m As Matrix4d = Matrix4d.Zero
            m.M11 = NextDouble() : m.M12 = NextDouble() : m.M13 = NextDouble() : m.M14 = NextDouble()
            m.M21 = NextDouble() : m.M22 = NextDouble() : m.M23 = NextDouble() : m.M24 = NextDouble()
            m.M31 = NextDouble() : m.M32 = NextDouble() : m.M33 = NextDouble() : m.M34 = NextDouble()
            m.M41 = NextDouble() : m.M42 = NextDouble() : m.M43 = NextDouble() : m.M44 = NextDouble()
            mats(k) = m
        Next
        Dim pal = BuildFlatPalette(mats)

        ' Round-trip Store/Load: si esto se rompe, TODO lo de abajo compara basura contra basura.
        For k As Integer = 0 To nMats - 1
            Dim back = LoadMatrix(pal, k * MatDoubles)
            Dim a(MatDoubles - 1) As Double
            Dim b(MatDoubles - 1) As Double
            StoreMatrix(mats(k), a, 0)
            StoreMatrix(back, b, 0)
            Dim bad0 = FirstBitDiff(a, b)
            If bad0 >= 0 Then Return $"[geom-roundtrip] matriz {k}: elemento {bad0} no sobrevive Store/Load"
        Next

        Dim idxBuf(3) As Integer
        Dim wBuf(3) As Double
        Dim accV(MatDoubles - 1) As Double
        Dim accS(MatDoubles - 1) As Double

        ' nUsed 0..4 cubre el vertice sin huesos validos (0) y el caso lleno (4).
        For iter As Integer = 0 To 499
            Dim nUsed As Integer = CInt(NextBits() Mod 5UL)
            For j As Integer = 0 To nUsed - 1
                idxBuf(j) = CInt(NextBits() Mod CULng(nMats))
                wBuf(j) = NextDouble()
            Next
            BlendInto(pal, idxBuf, wBuf, nUsed, accV)
            BlendIntoScalar(pal, idxBuf, wBuf, nUsed, accS)
            Dim bad = FirstBitDiff(accV, accS)
            If bad >= 0 Then Return $"[geom-blend] iter {iter} nUsed={nUsed}: elemento {bad} difiere ({WidthInfo})"

            ' Cadena completa: blend + normalizacion, que es lo que hace BlendBoneMatrices.
            Dim sumW As Double = 0
            For j As Integer = 0 To nUsed - 1
                sumW += wBuf(j)
            Next
            If sumW <> 0 Then
                ScaleAcc(accV, 1.0 / sumW)
                ScaleAccScalar(accS, 1.0 / sumW)
                bad = FirstBitDiff(accV, accS)
                If bad >= 0 Then Return $"[geom-blendchain] iter {iter}: elemento {bad} difiere ({WidthInfo})"
            End If
        Next

        ' Casos con valores especiales: el signo del cero, pesos cero y pesos que se cancelan.
        ' Un peso 0 sobre una matriz con -0.0 no da lo mismo por los dos caminos si algun dia se
        ' colara un FMA o un reordenamiento, asi que va explicito.
        Dim esp(nMats - 1) As Matrix4d
        Dim vals As Double() = {0.0, -0.0, 1.0, -1.0, Double.Epsilon, -Double.Epsilon, 1.0E+300, 1.0E-300}
        For k As Integer = 0 To nMats - 1
            Dim m As Matrix4d = Matrix4d.Zero
            Dim tmp(MatDoubles - 1) As Double
            For e As Integer = 0 To MatDoubles - 1
                tmp(e) = vals((e + k) Mod vals.Length)
            Next
            m = LoadMatrix(tmp, 0)
            esp(k) = m
        Next
        Dim palEsp = BuildFlatPalette(esp)
        Dim pesos As Double() = {0.0, -0.0, 1.0, -1.0, 0.5, 1.0E-300, 1.0E+300, 3.0}
        For iter As Integer = 0 To 199
            Dim nUsed As Integer = CInt(NextBits() Mod 5UL)
            For j As Integer = 0 To nUsed - 1
                idxBuf(j) = CInt(NextBits() Mod CULng(nMats))
                wBuf(j) = pesos(CInt(NextBits() Mod CULng(pesos.Length)))
            Next
            BlendInto(palEsp, idxBuf, wBuf, nUsed, accV)
            BlendIntoScalar(palEsp, idxBuf, wBuf, nUsed, accS)
            Dim bad = FirstBitDiff(accV, accS)
            If bad >= 0 Then Return $"[geom-blend-especiales] iter {iter} nUsed={nUsed}: elemento {bad} difiere ({WidthInfo})"
        Next

        ' ===========================================================================================
        ' EL MISMO BARRIDO, SOBRE EL KERNEL DE SINGLE — QUE ES EL QUE SE HORNEA.
        '
        ' TODO LO DE ARRIBA PRUEBA CODIGO QUE YA NO TIENE CONSUMIDOR. `BuildFlatPalette`,
        ' `BlendInto`, `BlendIntoScalar` y `ScaleAcc` son el kernel de DOUBLE, y desde que la paleta paso
        ' a Single produccion va por `BuildFlatPaletteS` -> `BlendEnScratch` -> `BlendIntoS`. O sea que el
        ' corpus de valores especiales de arriba —el cero NEGATIVO, los denormales, el overflow— no tocaba
        ' NI UNA VEZ el kernel que de verdad escribe bytes en un NIF. El gate quedaba en verde probando
        ' otra cosa.
        '
        ' Lo que se perdia no era simetria: era COBERTURA. Justamente en esos valores es donde un FMA
        ' colado por el JIT, o un reordenamiento de la suma, separa el camino vectorial del escalar — y
        ' este self-test viaja en el binario y ABORTA UN BAKE cuando falla, asi que su veredicto tiene que
        ' ser sobre el kernel que hornea.
        ' ===========================================================================================
        Dim valsS As Single() = {0.0F, -0.0F, 1.0F, -1.0F, Single.Epsilon, -Single.Epsilon, 1.0E+38F, 1.0E-38F}
        Dim pesosS As Single() = {0.0F, -0.0F, 1.0F, -1.0F, 0.5F, 1.0E-38F, 1.0E+38F, 3.0F}

        Dim palEspS(nMats * MatSingles - 1) As Single
        For k As Integer = 0 To nMats - 1
            For e As Integer = 0 To MatSingles - 1
                palEspS(k * MatSingles + e) = valsS((e + k) Mod valsS.Length)
            Next
        Next

        Dim accVS(MatSingles - 1) As Single
        Dim accSS(MatSingles - 1) As Single
        Dim wBufS(7) As Single
        ' Buffer de indices PROPIO: el `idxBuf` de mas arriba es de 4 elementos y estos dos bucles
        ' piden hasta 5 huesos. Reusarlo daba IndexOutOfRange en la primera corrida.
        Dim idxBufS(7) As Integer
        For iter As Integer = 0 To 199
            Dim nUsed As Integer = CInt(NextBits() Mod 5UL)
            For j As Integer = 0 To nUsed - 1
                idxBufS(j) = CInt(NextBits() Mod CULng(nMats))
                wBufS(j) = pesosS(CInt(NextBits() Mod CULng(pesosS.Length)))
            Next
            ' Los dos con la MISMA escala, para que la unica diferencia posible sea el camino.
            BlendIntoS(palEspS, idxBufS, wBufS, nUsed, accVS)
            BlendIntoScalarS(palEspS, idxBufS, wBufS, nUsed, accSS)
            For e As Integer = 0 To MatSingles - 1
                If BitConverter.SingleToInt32Bits(accVS(e)) <> BitConverter.SingleToInt32Bits(accSS(e)) Then
                    Return $"[geom-blendS-especiales] iter {iter} nUsed={nUsed}: elemento {e} difiere " &
                           $"(vectorial {accVS(e)} contra escalar {accSS(e)}) ({WidthInfo})"
                End If
            Next
        Next

        ' Y con una ESCALA fusionada distinta de 1, que es como corre produccion (`BlendIntoS(..., escala)`
        ' con escala = 1/sumW). El escalado va DENTRO del kernel, asi que es parte de la ley a comparar.
        For iter As Integer = 0 To 199
            Dim nUsed As Integer = CInt(NextBits() Mod 5UL) + 1
            Dim sumW As Single = 0.0F
            For j As Integer = 0 To nUsed - 1
                idxBufS(j) = CInt(NextBits() Mod CULng(nMats))
                wBufS(j) = pesosS(CInt(NextBits() Mod CULng(pesosS.Length)))
                sumW += wBufS(j)
            Next
            If sumW = 0.0F OrElse Single.IsNaN(sumW) OrElse Single.IsInfinity(sumW) Then Continue For
            Dim escala As Single = 1.0F / sumW
            BlendIntoS(palEspS, idxBufS, wBufS, nUsed, accVS, escala)
            BlendIntoScalarS(palEspS, idxBufS, wBufS, nUsed, accSS)
            ScaleAccS(accSS, escala)
            For e As Integer = 0 To MatSingles - 1
                If BitConverter.SingleToInt32Bits(accVS(e)) <> BitConverter.SingleToInt32Bits(accSS(e)) Then
                    Return $"[geom-blendS-escala] iter {iter} nUsed={nUsed}: elemento {e} difiere " &
                           $"(fusionado {accVS(e)} contra escalar+ScaleAccS {accSS(e)}) ({WidthInfo}). " &
                           "La escala fusionada en BlendIntoS tiene que dar EXACTAMENTE lo mismo que " &
                           "escalar despues: la suma se completa y recien ahi se multiplica."
                End If
            Next
        Next

        Return ""
    End Function

End Module
