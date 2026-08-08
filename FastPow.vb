Option Strict On

Imports System.Numerics
Imports System.Runtime.CompilerServices
Imports System.Runtime.Intrinsics

''' <summary><c>pow(x, k)</c> para un exponente CONSTANTE, sobre x en [0,1]. Reemplaza a <c>MathF.Pow</c> en
''' las transfer functions del compose (γ2.2, γ2.4 y sus inversas) y existe en CUATRO anchos —escalar,
''' Vector128, Vector256 y Vector(Of T) de ancho variable, que es el que usa PRODUCCION— todos BIT-IDENTICOS
''' entre si (lo verifica WidthParitySelfTest en cada bake).
'''
''' <para><b>⭐ CONTRATO DE PORTABILIDAD — UNA sola ley, los mismos bytes en TODA PC.</b> El peligro no era
''' "que no corra" sino que el bake diera bytes distintos segun la maquina. Por eso el fallback NO es
''' <c>MathF.Pow</c>: es ESTA MISMA ley evaluada de a un elemento. Cuatro reglas lo garantizan y NINGUNA es
''' opcional:</para>
''' <list type="number">
''' <item>Solo la API CROSS-PLATFORM <c>Vector256</c>/<c>Vector128</c>, JAMAS <c>Avx.*</c>/<c>Sse.*</c>. Con
'''   AVX2 el JIT emite AVX2; sin el, expande el MISMO vector elemento a elemento con las MISMAS ops IEEE.</item>
''' <item>⛔ CERO FMA. <c>a*b+c</c> fusionado NO da lo mismo que sin fusionar. .NET no contrae por su cuenta,
'''   asi que alcanza con no llamar a <c>Fma.*</c> — y hay que seguir sin llamarlo.</item>
''' <item>⛔ CERO intrinseco de redondeo. <c>n = round(s)</c> sale de la constante magica
'''   <c>(s + 1.5·2²³) − 1.5·2²³</c>, que es round-half-to-EVEN exacto con solo suma y resta.</item>
''' <item>Solo add/sub/mul/div/min/max y shift/and/or/convert enteros: todo exactamente especificado por
'''   IEEE-754 / ECMA-335 ⇒ escalar == Vector128 == Vector256.</item>
''' </list>
''' <para><b>VERIFICADO por enumeracion, no por argumento</b>: 0 violaciones en 4.261.412.868 comparaciones
''' (los 1.065.353.217 float32 de [0,1] × 4 exponentes), escalar vs V128 vs V256, bit a bit. Y contra
''' <c>MathF.Pow</c>: error absoluto MAXIMO 1,192e-7 ⇒ <b>|delta| de byte MAXIMO = 1</b>. Ver
''' <c>61-perf-simd-evaluacion</c> y el test <c>FastPowParity</c>.</para>
'''
''' <para><b>El truco de precision.</b> Sin el, pow en f32 pierde ~2,6e-6 relativo con x chico y el error se
''' acercaria al byte. <c>k·log2(x) = k·e + k·log2(m)</c>; <c>k·e</c> hace falta con ~1e-7 absoluto pero un
''' f32 en magnitud 280 tiene espaciado 3e-5. Se parte la CONSTANTE <c>k = kHi + kLo</c> con <c>kHi</c> en
''' grilla 2^-12, con lo que <c>kHi·e</c> (e entero chico) es EXACTO en f32 y la cancelacion <c>A − n</c>
''' tambien. Eso solo baja el error de 2,6e-6 a 1,2e-7.</para>
'''
''' <para>⛔ NO "simplificar" esto a <c>MathF.Pow</c> en el camino escalar: volveria a haber DOS leyes y la
''' salida dependeria de la CPU.</para></summary>
Public Module FastPow

    ''' <summary>Exponente ya partido (Dekker) para que <c>Hi*e</c> sea exacto en float32.</summary>
    Public Structure PowExp
        Public ReadOnly Hi As Single
        Public ReadOnly Lo As Single
        Public ReadOnly Full As Single
        Public Sub New(exact As Double)
            ' Hi en grilla 2^-12: con |k| < 2,5 son <= 20 bits significativos, y e <= 9 bits,
            ' asi que el producto Hi*e entra EXACTO en los 24 bits de mantisa de un Single.
            Dim h = CSng(Math.Round(exact * 4096.0) / 4096.0)
            Hi = h
            Lo = CSng(exact - CDbl(h))
            Full = CSng(exact)
        End Sub
    End Structure

    ' Los cuatro exponentes que usa el compose. Se calculan en Double y se angostan aca, igual que
    ' InvG22/InvG24 del compositor: escribirlos como literales daria OTRO float y por lo tanto otra imagen.
    Public ReadOnly G22 As New PowExp(2.2)
    Public ReadOnly InvG22 As New PowExp(1.0 / 2.2)
    Public ReadOnly G24 As New PowExp(2.4)
    Public ReadOnly InvG24 As New PowExp(1.0 / 2.4)

    Private Const L2E_2 As Single = 2.885390081777926814F   ' 2/ln2
    Private Const LN2 As Single = 0.6931471805599453F
    Private Const MAGIC As Single = 12582912.0F             ' 1.5 * 2^23
    Private Const SQRT2 As Single = 1.4142135F
    Private Const TINYF As Single = 1.17549435E-38F         ' menor normal
    Private Const SCALE24 As Single = 16777216.0F           ' 2^24

    ' Coeficientes de las dos series, con nombre, para que el cuerpo escalar y los vectoriales sean
    ' VISIBLEMENTE la misma secuencia de operaciones.
    Private Const L9 As Single = 1.0F / 9.0F, L7 As Single = 1.0F / 7.0F
    Private Const L5 As Single = 1.0F / 5.0F, L3 As Single = 1.0F / 3.0F
    Private Const E7 As Single = 1.0F / 5040.0F, E6 As Single = 1.0F / 720.0F, E5 As Single = 1.0F / 120.0F
    Private Const E4 As Single = 1.0F / 24.0F, E3 As Single = 1.0F / 6.0F, E2 As Single = 0.5F

    ''' <summary>¿Hay SIMD real de 256 bits? Elegir ancho SOLO cambia la VELOCIDAD: los tres caminos estan
    ''' probados bit-identicos, asi que despachar por ancho no puede mover un byte de la salida.</summary>
    Public ReadOnly Property Accelerated256 As Boolean
        Get
            Return Vector256.IsHardwareAccelerated
        End Get
    End Property

    Public ReadOnly Property Accelerated128 As Boolean
        Get
            Return Vector128.IsHardwareAccelerated
        End Get
    End Property

    ''' <summary>⭐ ANCHO VARIABLE — el que usan los compositores. <c>Vector(Of Single)</c> elige SOLO el ancho
    ''' que la máquina tiene: 256 bits (8 lanes) con AVX2, 128 (4 lanes) con SSE2, y en una CPU sin SIMD el
    ''' JIT lo expande elemento a elemento con las MISMAS ops IEEE.
    ''' <para><b>Por qué esto y no duplicar el código.</b> El compositor de FO4 estaba escrito contra
    ''' <c>Vector256</c> a secas, así que una CPU con SSE2 pero sin AVX2 caía HASTA EL ESCALAR — que es 1,54×
    ''' más lento que <c>MathF.Pow</c>, o sea que en esa máquina el trabajo de SIMD la dejaba MÁS lenta. La
    ''' alternativa era duplicar las 22 funciones espejo a Vector128 (lo que hizo <c>SseFaceGenBaker</c>: dos
    ''' juegos que hay que mantener en sincronía a mano). Con el ancho variable hay UNA sola escritura de cada
    ''' ley y el despacho lo hace el runtime.</para>
    ''' <para>⛔ Esto SÓLO es legítimo porque los anchos ya están probados BIT-IDÉNTICOS entre sí (0
    ''' violaciones en 4.261.412.868 comparaciones). Si no lo estuvieran, un binario daría bytes distintos
    ''' según la CPU — exactamente lo que este contrato existe para impedir.</para></summary>
    Public ReadOnly Property AcceleratedV As Boolean
        Get
            Return Vector.IsHardwareAccelerated
        End Get
    End Property

    ''' <summary>Lanes de un <c>Vector(Of Single)</c> en ESTA máquina (8 con AVX2, 4 con SSE2). Es el tamaño
    ''' de bloque que tienen que usar los loops: hardcodear 8 los rompe en una máquina de 4.</summary>
    Public ReadOnly Property LaneCount As Integer
        Get
            Return Vector(Of Single).Count
        End Get
    End Property

    ''' <summary>⭐ SELF-TEST DE PARIDAD ENTRE ANCHOS: escalar vs Vector128 vs Vector256 vs Vector(Of T), para
    ''' TODAS las funciones de este módulo (<c>Pow</c> en los 4 exponentes, <c>PowVar</c> y <c>Exp2</c>).
    ''' Devuelve "" si todo coincide BIT A BIT; si no, la primera divergencia con sus bits.
    '''
    ''' <para><b>Qué prueba y por qué importa.</b> Todo el contrato de este trabajo es "el MISMO binario da los
    ''' MISMOS bytes en cualquier CPU". Eso sólo se sostiene si los cuatro caminos son idénticos: el runtime
    ''' elige el ancho por ti y no hay forma de auditar esa elección desde el bake. Si alguna vez divergen, dos
    ''' máquinas hornean caras distintas con el mismo mod — y el gate de bytes de un solo equipo no lo vería.</para>
    '''
    ''' <para>Barre el dominio real con paso fino MÁS los bordes que rompen: 0, 1, el menor normal, denormales,
    ''' fuera de [0,1], NaN e infinitos, y —para <c>PowVar</c>— el exponente NaN, que es el que hacía
    ''' <c>OverflowException</c> en el escalar mientras el vector devolvía basura.</para>
    ''' <para>⚠️ No es la enumeración EXHAUSTIVA de los 1.065.353.217 float32 (esa vive en el arnés
    ''' <c>powgate/vbgate</c> y tarda minutos): acá el paso es fino pero acotado para poder correr en cada bake.</para></summary>
    Public Function WidthParitySelfTest() As String
        ' Bordes primero: es donde los cuatro caminos tienen ramas distintas (clamps, NaN, denormales).
        Dim edges As Single() = {0.0F, -0.0F, 1.0F, -1.0F, 0.5F, 1.5F, -0.5F,
                                 Single.Epsilon, TINYF, TINYF / 2.0F, 1.0F - 0.0000001F,
                                 Single.NaN, Single.PositiveInfinity, Single.NegativeInfinity}
        Dim exps = New (Name As String, K As PowExp)() {("2.2", G22), ("1/2.2", InvG22), ("2.4", G24), ("1/2.4", InvG24)}

        For Each ex In exps
            For Each x In edges
                Dim r = CheckPow(x, ex.K, ex.Name)
                If r.Length > 0 Then Return r
            Next
            ' barrido fino sobre [0,1] por PATRÓN DE BITS (no lineal en el valor): así entran también los
            ' exponentes chicos, que es donde el split de Dekker se gana su razón de ser.
            Dim step_ As Integer = 1543   ' primo: evita alinearse con las fronteras de exponente
            Dim bits As Integer = 0
            While bits <= &H3F800000
                Dim r = CheckPow(BitConverter.Int32BitsToSingle(bits), ex.K, ex.Name)
                If r.Length > 0 Then Return r
                bits += step_ * 256
            End While
        Next

        ' ---- Exp2 (la mitad exp2 suelta, la que usa el soft-light Illusions) ----
        For Each y In New Single() {0.0F, -0.0F, 1.0F, -1.0F, 0.5F, -0.5F, 2.0F, -126.5F, -127.0F, 127.5F, 128.0F,
                                    Single.NaN, Single.PositiveInfinity, Single.NegativeInfinity}
            Dim r = CheckExp2(y)
            If r.Length > 0 Then Return r
        Next
        Dim yy As Single = -130.0F
        While yy <= 130.0F
            Dim r = CheckExp2(yy)
            If r.Length > 0 Then Return r
            yy += 0.013F
        End While

        ' ---- PowVar: exponente VARIABLE. El rango real del Illusions es [0,5 , 2]; se barre más ancho.
        For Each x In edges
            For Each y In New Single() {0.5F, 1.0F, 2.0F, 0.25F, 4.0F, 0.0F, -1.0F,
                                        Single.NaN, Single.PositiveInfinity, Single.NegativeInfinity}
                Dim r = CheckPowVar(x, y)
                If r.Length > 0 Then Return r
            Next
        Next
        Dim xb As Integer = 0
        While xb <= &H3F800000
            Dim xv = BitConverter.Int32BitsToSingle(xb)
            For Each y In New Single() {0.5F, 0.9F, 1.3F, 2.0F}
                Dim r = CheckPowVar(xv, y)
                If r.Length > 0 Then Return r
            Next
            xb += 3079 * 1024
        End While
        Return ""
    End Function

    Private Function Bits(v As Single) As Integer
        Return BitConverter.SingleToInt32Bits(v)
    End Function

    ''' <summary>Un valor por los CUATRO caminos. El escalar es la LEY; los otros tres tienen que darle igual.
    ''' Los vectores se llenan con el MISMO x en todos los lanes y se lee el lane 0 — un lane que dependiera de
    ''' su posición se vería igual, pero eso ya lo cubren los self-tests de los compositores, que pasan
    ''' bloques con valores distintos por lane.</summary>
    Private Function CheckPow(x As Single, k As PowExp, name As String) As String
        Dim s = Pow1(x, k)
        Dim v128 = PowV128(Vector128.Create(x), k).GetElement(0)
        Dim v256 = PowV256(Vector256.Create(x), k).GetElement(0)
        Dim vv = PowV(New Vector(Of Single)(x), k)(0)
        If Bits(v128) <> Bits(s) Then Return $"WidthParity Pow({name}) x={x} [0x{Bits(x):X8}]: escalar=0x{Bits(s):X8} V128=0x{Bits(v128):X8}"
        If Bits(v256) <> Bits(s) Then Return $"WidthParity Pow({name}) x={x} [0x{Bits(x):X8}]: escalar=0x{Bits(s):X8} V256=0x{Bits(v256):X8}"
        If Bits(vv) <> Bits(s) Then Return $"WidthParity Pow({name}) x={x} [0x{Bits(x):X8}]: escalar=0x{Bits(s):X8} Vector(Of T)=0x{Bits(vv):X8}"
        Return ""
    End Function

    Private Function CheckExp2(y As Single) As String
        Dim s = Exp2_1(y)
        Dim v256 = Exp2V256(Vector256.Create(y)).GetElement(0)
        Dim vv = Exp2V(New Vector(Of Single)(y))(0)
        If Bits(v256) <> Bits(s) Then Return $"WidthParity Exp2 y={y} [0x{Bits(y):X8}]: escalar=0x{Bits(s):X8} V256=0x{Bits(v256):X8}"
        If Bits(vv) <> Bits(s) Then Return $"WidthParity Exp2 y={y} [0x{Bits(y):X8}]: escalar=0x{Bits(s):X8} Vector(Of T)=0x{Bits(vv):X8}"
        Return ""
    End Function

    Private Function CheckPowVar(x As Single, y As Single) As String
        Dim s As Single
        Try
            s = PowVar1(x, y)
        Catch ex As Exception
            Return $"WidthParity PowVar: el ESCALAR tiró {ex.GetType().Name} con x={x} y={y} — tiene que devolver un valor"
        End Try
        Dim v256 = PowVarV256(Vector256.Create(x), Vector256.Create(y)).GetElement(0)
        Dim vv = PowVarV(New Vector(Of Single)(x), New Vector(Of Single)(y))(0)
        If Bits(v256) <> Bits(s) Then Return $"WidthParity PowVar x={x} y={y}: escalar=0x{Bits(s):X8} V256=0x{Bits(v256):X8}"
        If Bits(vv) <> Bits(s) Then Return $"WidthParity PowVar x={x} y={y}: escalar=0x{Bits(s):X8} Vector(Of T)=0x{Bits(vv):X8}"
        Return ""
    End Function

    ' ============================ LAYOUT DE ANCHO VARIABLE ============================
    ' Helpers para los buffers AoS RGBA. ⛔ Reemplazan a los literales de 8 lanes
    ' (`Vector256.Create(-1,-1,-1,0,-1,-1,-1,0)`, `Create(3,3,3,3,7,7,7,7)`, …) y a `Vector256.Shuffle`,
    ' que NO existe en la API de ancho variable. Todos los patrones que usaban esos literales son de
    ' PERIODO 4 —el tamaño de un pixel RGBA— asi que se generan para el ancho de la maquina y siguen siendo
    ' el MISMO movimiento de datos, bit a bit. Un literal de 8 lanes en una maquina de 4 dejaria el patron
    ' corrido: no es una optimizacion, es correccion.

    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Public Function VBroadcastS(x As Single) As Vector(Of Single)
        Return New Vector(Of Single)(x)
    End Function

    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Public Function VBroadcastS(a As Single(), i As Integer) As Vector(Of Single)
        Return New Vector(Of Single)(a, i)
    End Function

    ''' <summary>Patrón por CANAL repetido en todos los píxeles del vector: (c0,c1,c2,c3, c0,c1,c2,c3, …).
    ''' Es lo que eran los literales de 8 lanes con período 4.</summary>
    Public Function VPerChannel(c0 As Single, c1 As Single, c2 As Single, c3 As Single) As Vector(Of Single)
        Dim n = Vector(Of Single).Count
        Dim v(n - 1) As Single
        For i = 0 To n - 1
            Select Case i And 3
                Case 0 : v(i) = c0
                Case 1 : v(i) = c1
                Case 2 : v(i) = c2
                Case Else : v(i) = c3
            End Select
        Next
        Return New Vector(Of Single)(v)
    End Function

    ''' <summary>Idem pero como MÁSCARA de bits (se arma en Integer y se reinterpreta): el uso típico es
    ''' (−1,−1,−1,0) = "toca RGB, no toques el alpha".</summary>
    ''' <summary>⛔ Devuelve la máscara en <c>Vector(Of Integer)</c>, NO en Single. Las comparaciones
    ''' vectoriales NO genéricas devuelven <c>Vector(Of Integer)</c>, y
    ''' <c>ConditionalSelect(Vector(Of Integer), Vector(Of Single), Vector(Of Single))</c> las consume
    ''' directamente. Tener la máscara en Single obligaba a usar la sobrecarga GENÉRICA de las
    ''' comparaciones — MEDIDO: 118,8 % más lenta (38 ms vs 17 ms / 40M iter), porque no está
    ''' intrinsificada igual. Fue la causa de una regresión real de ~8,8 % en el compose de SSE.</summary>
    Public Function VPerChannelMask(m0 As Integer, m1 As Integer, m2 As Integer, m3 As Integer) As Vector(Of Integer)
        Dim n = Vector(Of Integer).Count
        Dim v(n - 1) As Integer
        For i = 0 To n - 1
            Select Case i And 3
                Case 0 : v(i) = m0
                Case 1 : v(i) = m1
                Case 2 : v(i) = m2
                Case Else : v(i) = m3
            End Select
        Next
        Return New Vector(Of Integer)(v)
    End Function

    ''' <summary>Difunde el canal <paramref name="ch"/> de CADA píxel a los 4 lanes de ese píxel. Era
    ''' <c>Shuffle(v, Create(ch,ch,ch,ch, 4+ch,…))</c>.
    ''' <para><paramref name="scratch"/> es un scratch DEL HILO de largo <b>2×<see cref="LaneCount"/></b>:
    ''' la mitad baja es la copia de la entrada y la alta es el destino. ⛔ Hacen falta las DOS mitades: leer y
    ''' escribir sobre el mismo rango corrompe el resultado (el lane j lee una posición que otro lane ya pisó).
    ''' ⛔ No puede compartirse entre hilos.</para>
    ''' <para>⛔⭐ NO asignar acá adentro. La primera versión hacía <c>Dim outv(n-1)</c> por llamada: son 7
    ''' asignaciones Gen0 POR BLOQUE sólo en ColorMode, y otra por bloque en el compose de tint de SSE, dentro
    ''' de los loops calientes y encima ANTES del early-out. En net8.0 no hay stack-allocation de arrays, así
    ''' que era basura garantizada. Reemplazar UNA instrucción <c>Shuffle</c> por eso hacía más lenta justo a
    ''' la máquina con AVX2 — la trampa nº 1 del proyecto (optimizar un ancho rompiendo el otro).</para></summary>
    ' ============================ INDICES DE SHUFFLE, PRECALCULADOS ============================
    ' ⭐ `Vector.Shuffle` NO existe en la API de ancho variable, pero `Vector256.Shuffle` y
    ' `Vector128.Shuffle` SI. Y usarlos NO rompe el contrato de "una sola ley": un shuffle es MOVIMIENTO DE
    ' DATOS, no aritmetica — mueve los mismos bits que el fallback escalar, igual que un gather. Lo que la
    ' regla prohibe es lo que cambia el REDONDEO (FMA), no lo que reordena.
    '
    ' ⛔ POR QUE IMPORTA, MEDIDO: la primera version reemplazaba el shuffle por un viaje a memoria (CopyTo a
    ' un scratch + loop escalar + carga). Eso costo **SSE Textures 46,4 -> 52,4 s (+12,9 %)** entre dos
    ' corridas del corpus completo. Los 10 self-tests daban VERDE igual: la paridad NO ve regresiones de
    ' velocidad (es la trampa nº 1 de 61-perf-simd-trampas, y ya habia pasado antes en este repo).
    '
    ' Los indices se calculan UNA vez: armar un Vector256 de 8 enteros por llamada costaria lo mismo que el
    ' scratch que se quiere evitar. Todos los patrones son LANE-LOCAL (dentro de cada grupo de 4 = un pixel
    ' RGBA), asi que el JIT puede emitir un solo vpermilps/vshufps.
    Private ReadOnly BcastIdx256 As Vector256(Of Integer)() = BuildBcastIdx256()
    Private ReadOnly BcastIdx128 As Vector128(Of Integer)() = BuildBcastIdx128()
    Private ReadOnly SwapIdx256 As Vector256(Of Integer)() = BuildSwapIdx256()
    Private ReadOnly SwapIdx128 As Vector128(Of Integer)() = BuildSwapIdx128()

    Private Function BuildBcastIdx256() As Vector256(Of Integer)()
        Dim r(3) As Vector256(Of Integer)
        For ch = 0 To 3
            Dim ix(7) As Integer
            For j = 0 To 7 : ix(j) = (j And Not 3) + ch : Next
            r(ch) = Vector256.Create(ix, 0)
        Next
        Return r
    End Function

    Private Function BuildBcastIdx128() As Vector128(Of Integer)()
        Dim r(3) As Vector128(Of Integer)
        For ch = 0 To 3
            Dim ix(3) As Integer
            For j = 0 To 3 : ix(j) = (j And Not 3) + ch : Next
            r(ch) = Vector128.Create(ix, 0)
        Next
        Return r
    End Function

    Private Function BuildSwapIdx256() As Vector256(Of Integer)()
        Dim r(3) As Vector256(Of Integer)
        For m = 0 To 3
            Dim ix(7) As Integer
            For j = 0 To 7 : ix(j) = j Xor m : Next
            r(m) = Vector256.Create(ix, 0)
        Next
        Return r
    End Function

    Private Function BuildSwapIdx128() As Vector128(Of Integer)()
        Dim r(3) As Vector128(Of Integer)
        For m = 0 To 3
            Dim ix(3) As Integer
            For j = 0 To 3 : ix(j) = j Xor m : Next
            r(m) = Vector128.Create(ix, 0)
        Next
        Return r
    End Function

    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Public Function VBroadcastChannelV(v As Vector(Of Single), ch As Integer, scratch As Single()) As Vector(Of Single)
        Dim n = Vector(Of Single).Count
        ' Shuffle NATIVO cuando el ancho coincide: una instruccion en vez de un viaje a memoria.
        If n = Vector256(Of Single).Count AndAlso Vector256.IsHardwareAccelerated Then
            Return Vector256.Shuffle(v.AsVector256(), BcastIdx256(ch)).AsVector()
        ElseIf n = Vector128(Of Single).Count AndAlso Vector128.IsHardwareAccelerated Then
            Return Vector128.Shuffle(v.AsVector128(), BcastIdx128(ch)).AsVector()
        End If
        ' Fallback por scratch: cualquier otro ancho (incl. 512) y el caso sin SIMD. MISMO movimiento de datos.
        v.CopyTo(scratch, 0)
        For j = 0 To n - 1
            scratch(n + j) = scratch((j And Not 3) + ch)  ' (j And Not 3) = inicio del pixel de ese lane
        Next
        Return New Vector(Of Single)(scratch, n)
    End Function

    ''' <summary>Permuta DENTRO de cada píxel: el lane j toma el valor del lane <c>j Xor xorMask</c>. Con
    ''' xorMask=1 es el viejo <c>swapPairs</c> y con 2 el <c>swapHalves</c> — las dos mitades de la reducción
    ''' horizontal R+G+B. Movimiento de datos puro ⇒ bit-idéntico al shuffle que reemplaza.</summary>
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Public Function VSwapWithinPixel(v As Vector(Of Single), xorMask As Integer, scratch As Single()) As Vector(Of Single)
        Dim n = Vector(Of Single).Count
        ' Shuffle NATIVO cuando el ancho coincide — ver la nota de los indices precalculados.
        If n = Vector256(Of Single).Count AndAlso Vector256.IsHardwareAccelerated Then
            Return Vector256.Shuffle(v.AsVector256(), SwapIdx256(xorMask)).AsVector()
        ElseIf n = Vector128(Of Single).Count AndAlso Vector128.IsHardwareAccelerated Then
            Return Vector128.Shuffle(v.AsVector128(), SwapIdx128(xorMask)).AsVector()
        End If
        v.CopyTo(scratch, 0)
        For j = 0 To n - 1
            scratch(n + j) = scratch(j Xor xorMask)
        Next
        Return New Vector(Of Single)(scratch, n)
    End Function

    ' ============================ ANCHO VARIABLE (Vector(Of T)) ============================
    ' Transcripción 1:1 de PowV256, operación por operación y en el mismo orden. No es "equivalente":
    ' es la MISMA cuenta con el ancho que decida el runtime.

    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Public Function PowV(x As Vector(Of Single), kHi As Single, kLo As Single, kFull As Single) As Vector(Of Single)
        Dim zero = Vector(Of Single).Zero
        Dim one = New Vector(Of Single)(1.0F)

        Dim xin = x
        x = Vector.Min(Vector.Max(x, zero), one)

        Dim tiny = Vector.LessThan(x, New Vector(Of Single)(TINYF))
        Dim xs = Vector.ConditionalSelect(tiny, Vector.Multiply(x, New Vector(Of Single)(SCALE24)), x)
        Dim eAdj = Vector.ConditionalSelect(tiny, New Vector(Of Single)(-24.0F), zero)

        Dim bits = Vector.As(Of Single, Integer)(xs)
        Dim e = Vector.Add(
            Vector.ConvertToSingle(Vector.Subtract(
                Vector.BitwiseAnd(Vector.ShiftRightLogical(bits, 23), New Vector(Of Integer)(&HFF)),
                New Vector(Of Integer)(127))), eAdj)

        Dim m = Vector.As(Of Integer, Single)(Vector.BitwiseOr(
            Vector.BitwiseAnd(bits, New Vector(Of Integer)(&H7FFFFF)), New Vector(Of Integer)(&H3F800000)))
        Dim big = Vector.GreaterThan(m, New Vector(Of Single)(SQRT2))
        m = Vector.ConditionalSelect(big, Vector.Multiply(m, New Vector(Of Single)(0.5F)), m)
        e = Vector.Add(e, Vector.ConditionalSelect(big, one, zero))

        Dim t = Vector.Divide(Vector.Subtract(m, one), Vector.Add(m, one))
        Dim t2 = Vector.Multiply(t, t)
        Dim p = Vector.Multiply(t2, New Vector(Of Single)(L9))
        p = Vector.Multiply(t2, Vector.Add(p, New Vector(Of Single)(L7)))
        p = Vector.Multiply(t2, Vector.Add(p, New Vector(Of Single)(L5)))
        p = Vector.Multiply(t2, Vector.Add(p, New Vector(Of Single)(L3)))
        Dim log2m = Vector.Multiply(New Vector(Of Single)(L2E_2), Vector.Add(t, Vector.Multiply(t, p)))

        Dim a = Vector.Multiply(New Vector(Of Single)(kHi), e)
        Dim b = Vector.Add(Vector.Multiply(New Vector(Of Single)(kLo), e),
                           Vector.Multiply(New Vector(Of Single)(kFull), log2m))
        Dim mg = New Vector(Of Single)(MAGIC)
        Dim n = Vector.Subtract(Vector.Add(Vector.Add(a, b), mg), mg)
        Dim r = Vector.Add(Vector.Subtract(a, n), b)

        Dim ex = ExpSeriesV(r)

        Dim nc = Vector.Min(n, New Vector(Of Single)(127.0F))
        Dim scale = Vector.As(Of Integer, Single)(Vector.ShiftLeft(
            Vector.Add(Vector.ConvertToInt32(nc), New Vector(Of Integer)(127)), 23))
        Dim res = Vector.Multiply(ex, scale)

        res = Vector.ConditionalSelect(Vector.LessThan(n, New Vector(Of Single)(-126.0F)), zero, res)
        res = Vector.ConditionalSelect(Vector.GreaterThan(xin, zero), res, zero)
        res = Vector.ConditionalSelect(Vector.GreaterThanOrEqual(xin, one), one, res)
        res = Vector.ConditionalSelect(Vector.Equals(xin, xin), res, xin)
        Return res
    End Function

    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Public Function PowV(x As Vector(Of Single), k As PowExp) As Vector(Of Single)
        Return PowV(x, k.Hi, k.Lo, k.Full)
    End Function

    ''' <summary>La serie de exp2 compartida por <see cref="PowV"/>, <see cref="PowVarV"/> y
    ''' <see cref="Exp2V"/>: MISMO orden de operaciones en los tres, que es lo que hace que el espejo sea
    ''' exacto y no "equivalente".</summary>
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Private Function ExpSeriesV(r As Vector(Of Single)) As Vector(Of Single)
        Dim one = New Vector(Of Single)(1.0F)
        Dim z = Vector.Multiply(r, New Vector(Of Single)(LN2))
        Dim q = Vector.Multiply(z, New Vector(Of Single)(E7))
        q = Vector.Multiply(z, Vector.Add(q, New Vector(Of Single)(E6)))
        q = Vector.Multiply(z, Vector.Add(q, New Vector(Of Single)(E5)))
        q = Vector.Multiply(z, Vector.Add(q, New Vector(Of Single)(E4)))
        q = Vector.Multiply(z, Vector.Add(q, New Vector(Of Single)(E3)))
        q = Vector.Multiply(z, Vector.Add(q, New Vector(Of Single)(E2)))
        q = Vector.Multiply(z, Vector.Add(q, one))
        Return Vector.Add(one, q)
    End Function

    ''' <summary><c>2^y</c> de ancho variable. Espejo de <see cref="Exp2_1"/>, guard de NaN incluido.</summary>
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Public Function Exp2V(y As Vector(Of Single)) As Vector(Of Single)
        Dim mg = New Vector(Of Single)(MAGIC)
        Dim n = Vector.Subtract(Vector.Add(y, mg), mg)
        Dim r = Vector.Subtract(y, n)
        Dim ex = ExpSeriesV(r)
        Dim nc = Vector.Min(Vector.Max(n, New Vector(Of Single)(-126.0F)), New Vector(Of Single)(127.0F))
        Dim scale = Vector.As(Of Integer, Single)(Vector.ShiftLeft(
            Vector.Add(Vector.ConvertToInt32(nc), New Vector(Of Integer)(127)), 23))
        Dim res = Vector.Multiply(ex, scale)
        res = Vector.ConditionalSelect(Vector.LessThan(n, New Vector(Of Single)(-126.0F)), Vector(Of Single).Zero, res)
        res = Vector.ConditionalSelect(Vector.GreaterThan(n, New Vector(Of Single)(127.0F)),
                                       New Vector(Of Single)(Single.PositiveInfinity), res)
        Return Vector.ConditionalSelect(Vector.Equals(y, y), res, y)
    End Function

    ' =====================================================================================================
    ' RAIZ CUBICA — el primitivo que faltaba para vectorizar la inversa del soft-light W3C.
    ' =====================================================================================================
    ' POR QUE ESTA ACA Y NO SE USA MathF.Cbrt: la inversa de W3C con s >= 0,5 y d < 0,25 es una CUBICA, y su
    ' unica raiz real sale por Cardano, que pide cbrt. `MathF.Cbrt` no tiene contraparte en `Vector(Of T)` ⇒
    ' el espejo vectorial no podria ser BIT-IDENTICO al escalar, que es el contrato de este modulo.
    '
    ' ⛔ NO se puede resolver con PowVar(x, 1/3): PowVar ACOTA la base a [0,1] (devuelve 0 por debajo y 1 por
    ' encima) y el argumento de Cardano es de signo cualquiera y modulo arbitrario.
    '
    ' LA LEY: estimacion inicial por el truco de bits + CUATRO pasos de Newton sobre y³ = a. La estimacion NO
    ' necesita ser precisa —solo IDENTICA en los dos caminos—, asi que el `bits/3` se aproxima con SHIFTS
    ' (1/4 + 1/16 + 1/64 + 1/256 = 0,33203), que es lo unico que `Vector(Of Integer)` sabe hacer sin division.
    ' Con un error inicial peor que 30 % los cuatro pasos de Newton (convergencia cuadratica) llegan al ULP.
    ' Escalar y vectorial ejecutan LA MISMA secuencia de operaciones en el MISMO orden ⇒ bit-identicos por
    ' construccion, no por aproximacion. Lo verifica el gate de BUILD `softlight-inv` (Tools/ParityGate) a traves
    ' de la inversa de W3C, y el gate `baker` a traves del espejo escalar-vs-vectorial del unfold.

    ''' <summary>Sesgo del truco de bits: <c>(127 − 127/3)·2^23</c>, o sea el ancla del exponente cuando la
    ''' mantisa se divide por 3. Con la aproximacion por shifts el valor exacto no es critico (lo absorbe
    ''' Newton); se deja el analitico para que la estimacion arranque centrada.</summary>
    Private Const CBRT_MAGIC As Integer = 710235477
    Private Const ONE_THIRD As Single = 1.0F / 3.0F

    ''' <summary>Raiz cubica escalar. Espejo exacto de <see cref="CbrtV"/>. Preserva el signo (y el signo del
    ''' cero), propaga NaN e infinitos sin tocarlos.</summary>
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Public Function Cbrt1(x As Single) As Single
        If Single.IsNaN(x) Then Return x
        If Single.IsInfinity(x) Then Return x
        Dim a As Single = MathF.Abs(x)
        If a = 0.0F Then Return x
        Dim bits As Integer = BitConverter.SingleToInt32Bits(a)
        Dim est As Integer = (bits >> 2) + (bits >> 4) + (bits >> 6) + (bits >> 8) + CBRT_MAGIC
        Dim y As Single = BitConverter.Int32BitsToSingle(est)
        ' Newton sobre y³ = a: y <- (2y + a/y²)/3. CUATRO pasos, sin early-out (un early-out por lane no
        ' existe en el vectorial ⇒ el escalar tampoco puede tenerlo o dejan de ser el mismo calculo).
        y = (2.0F * y + a / (y * y)) * ONE_THIRD
        y = (2.0F * y + a / (y * y)) * ONE_THIRD
        y = (2.0F * y + a / (y * y)) * ONE_THIRD
        y = (2.0F * y + a / (y * y)) * ONE_THIRD
        Return If(x < 0.0F, -y, y)
    End Function

    ''' <summary>Raiz cubica de ancho variable. MISMA secuencia que <see cref="Cbrt1"/>, en el MISMO orden.</summary>
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Public Function CbrtV(x As Vector(Of Single)) As Vector(Of Single)
        Dim zero = Vector(Of Single).Zero
        Dim two = New Vector(Of Single)(2.0F)
        Dim third = New Vector(Of Single)(ONE_THIRD)
        Dim a = Vector.Abs(x)
        Dim bits = Vector.As(Of Single, Integer)(a)
        Dim est = Vector.Add(
            Vector.Add(Vector.ShiftRightArithmetic(bits, 2), Vector.ShiftRightArithmetic(bits, 4)),
            Vector.Add(Vector.Add(Vector.ShiftRightArithmetic(bits, 6), Vector.ShiftRightArithmetic(bits, 8)),
                       New Vector(Of Integer)(CBRT_MAGIC)))
        Dim y = Vector.As(Of Integer, Single)(est)
        y = Vector.Multiply(Vector.Add(Vector.Multiply(two, y), Vector.Divide(a, Vector.Multiply(y, y))), third)
        y = Vector.Multiply(Vector.Add(Vector.Multiply(two, y), Vector.Divide(a, Vector.Multiply(y, y))), third)
        y = Vector.Multiply(Vector.Add(Vector.Multiply(two, y), Vector.Divide(a, Vector.Multiply(y, y))), third)
        y = Vector.Multiply(Vector.Add(Vector.Multiply(two, y), Vector.Divide(a, Vector.Multiply(y, y))), third)
        y = Vector.ConditionalSelect(Vector.LessThan(x, zero), Vector.Negate(y), y)
        ' Casos que el escalar resuelve con returns tempranos, en el MISMO orden de prioridad: cero (devuelve
        ' x, que preserva el signo del cero), infinito y NaN.
        y = Vector.ConditionalSelect(Vector.Equals(a, zero), x, y)
        y = Vector.ConditionalSelect(Vector.Equals(a, New Vector(Of Single)(Single.PositiveInfinity)), x, y)
        Return Vector.ConditionalSelect(Vector.Equals(x, x), y, x)
    End Function

    ''' <summary><c>x^y</c> con exponente VARIABLE por lane (el soft-light Illusions). Split de Dekker en
    ''' runtime. Espejo de <see cref="PowVar1"/>, incluido el guard de NaN del exponente y su ORDEN.</summary>
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Public Function PowVarV(x As Vector(Of Single), y As Vector(Of Single)) As Vector(Of Single)
        Dim zero = Vector(Of Single).Zero
        Dim one = New Vector(Of Single)(1.0F)

        Dim xin = x
        x = Vector.Min(Vector.Max(x, zero), one)

        Dim tiny = Vector.LessThan(x, New Vector(Of Single)(TINYF))
        Dim xs = Vector.ConditionalSelect(tiny, Vector.Multiply(x, New Vector(Of Single)(SCALE24)), x)
        Dim eAdj = Vector.ConditionalSelect(tiny, New Vector(Of Single)(-24.0F), zero)

        Dim bits = Vector.As(Of Single, Integer)(xs)
        Dim e = Vector.Add(
            Vector.ConvertToSingle(Vector.Subtract(
                Vector.BitwiseAnd(Vector.ShiftRightLogical(bits, 23), New Vector(Of Integer)(&HFF)),
                New Vector(Of Integer)(127))), eAdj)

        Dim m = Vector.As(Of Integer, Single)(Vector.BitwiseOr(
            Vector.BitwiseAnd(bits, New Vector(Of Integer)(&H7FFFFF)), New Vector(Of Integer)(&H3F800000)))
        Dim big = Vector.GreaterThan(m, New Vector(Of Single)(SQRT2))
        m = Vector.ConditionalSelect(big, Vector.Multiply(m, New Vector(Of Single)(0.5F)), m)
        e = Vector.Add(e, Vector.ConditionalSelect(big, one, zero))

        Dim t = Vector.Divide(Vector.Subtract(m, one), Vector.Add(m, one))
        Dim t2 = Vector.Multiply(t, t)
        Dim p = Vector.Multiply(t2, New Vector(Of Single)(L9))
        p = Vector.Multiply(t2, Vector.Add(p, New Vector(Of Single)(L7)))
        p = Vector.Multiply(t2, Vector.Add(p, New Vector(Of Single)(L5)))
        p = Vector.Multiply(t2, Vector.Add(p, New Vector(Of Single)(L3)))
        Dim log2m = Vector.Multiply(New Vector(Of Single)(L2E_2), Vector.Add(t, Vector.Multiply(t, p)))

        ' Split de Dekker EN RUNTIME: yh = y redondeado a múltiplo de 2^-12 ⇒ yh*e vuelve a ser EXACTO.
        Dim g = New Vector(Of Single)(4096.0F)
        Dim mg = New Vector(Of Single)(MAGIC)
        Dim yh = Vector.Divide(Vector.Subtract(Vector.Add(Vector.Multiply(y, g), mg), mg), g)
        Dim yl = Vector.Subtract(y, yh)

        Dim a = Vector.Multiply(yh, e)
        Dim b = Vector.Add(Vector.Multiply(yl, e), Vector.Multiply(y, log2m))
        Dim n = Vector.Subtract(Vector.Add(Vector.Add(a, b), mg), mg)
        Dim r = Vector.Add(Vector.Subtract(a, n), b)

        Dim ex = ExpSeriesV(r)

        Dim nc = Vector.Min(n, New Vector(Of Single)(127.0F))
        Dim scale = Vector.As(Of Integer, Single)(Vector.ShiftLeft(
            Vector.Add(Vector.ConvertToInt32(nc), New Vector(Of Integer)(127)), 23))
        Dim res = Vector.Multiply(ex, scale)

        res = Vector.ConditionalSelect(Vector.LessThan(n, New Vector(Of Single)(-126.0F)), zero, res)
        ' ⛔ Exponente NaN -> NaN, y VA ANTES de los cortes por `x`: en una cadena de selects el ULTIMO gana, y
        ' el escalar corta por `x` primero (PowVar(0,NaN)=0, PowVar(1,NaN)=1). Ver la nota gemela en PowVarV256.
        res = Vector.ConditionalSelect(Vector.Equals(y, y), res, y)
        ' Infinitos del exponente: mismo problema y mismo limite que en el escalar (ver alli).
        Dim infP = New Vector(Of Single)(Single.PositiveInfinity)
        res = Vector.ConditionalSelect(Vector.Equals(y, infP), zero, res)
        res = Vector.ConditionalSelect(Vector.Equals(y, New Vector(Of Single)(Single.NegativeInfinity)), infP, res)
        res = Vector.ConditionalSelect(Vector.GreaterThan(xin, zero), res, zero)
        res = Vector.ConditionalSelect(Vector.GreaterThanOrEqual(xin, one), one, res)
        res = Vector.ConditionalSelect(Vector.Equals(xin, xin), res, xin)
        Return res
    End Function

    ' ============================ ESCALAR ============================
    ''' <summary>Camino escalar. Es la MISMA ley que los vectoriales (no un fallback distinto) y el oraculo
    ''' del test de paridad. NaN entra ⇒ NaN sale, igual que <c>MathF.Pow</c>.</summary>
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Public Function Pow1(x As Single, kHi As Single, kLo As Single, kFull As Single) As Single
        If Single.IsNaN(x) Then Return x
        If x <= 0.0F Then Return 0.0F
        If x >= 1.0F Then Return 1.0F

        Dim eAdj As Single = 0.0F
        If x < TINYF Then
            x = x * SCALE24
            eAdj = -24.0F
        End If

        Dim bits = BitConverter.SingleToInt32Bits(x)
        Dim e As Single = CSng(((bits >> 23) And &HFF) - 127) + eAdj
        Dim m = BitConverter.Int32BitsToSingle((bits And &H7FFFFF) Or &H3F800000)
        If m > SQRT2 Then
            m = m * 0.5F
            e = e + 1.0F
        End If

        Dim t = (m - 1.0F) / (m + 1.0F)
        Dim t2 = t * t
        Dim p = t2 * L9
        p = t2 * (p + L7)
        p = t2 * (p + L5)
        p = t2 * (p + L3)
        Dim log2m = L2E_2 * (t + t * p)

        Dim a = kHi * e
        Dim b = kLo * e + kFull * log2m
        Dim n = (a + b + MAGIC) - MAGIC
        Dim r = (a - n) + b

        Dim z = r * LN2
        Dim q = z * E7
        q = z * (q + E6)
        q = z * (q + E5)
        q = z * (q + E4)
        q = z * (q + E3)
        q = z * (q + E2)
        q = z * (q + 1.0F)
        Dim ex = 1.0F + q

        If n < -126.0F Then Return 0.0F
        If n > 127.0F Then n = 127.0F
        Return ex * BitConverter.Int32BitsToSingle((CInt(n) + 127) << 23)
    End Function

    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Public Function Pow1(x As Single, k As PowExp) As Single
        Return Pow1(x, k.Hi, k.Lo, k.Full)
    End Function

    ' ============================ Vector256 ============================
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Public Function PowV256(x As Vector256(Of Single), kHi As Single, kLo As Single, kFull As Single) As Vector256(Of Single)
        Dim zero = Vector256(Of Single).Zero
        Dim one = Vector256.Create(1.0F)

        Dim xin = x
        x = Vector256.Min(Vector256.Max(x, zero), one)

        Dim tiny = Vector256.LessThan(x, Vector256.Create(TINYF))
        Dim xs = Vector256.ConditionalSelect(tiny, Vector256.Multiply(x, Vector256.Create(SCALE24)), x)
        Dim eAdj = Vector256.ConditionalSelect(tiny, Vector256.Create(-24.0F), zero)

        Dim bits = xs.AsInt32()
        Dim e = Vector256.Add(
            Vector256.ConvertToSingle(Vector256.Subtract(
                Vector256.BitwiseAnd(Vector256.ShiftRightLogical(bits, 23), Vector256.Create(&HFF)),
                Vector256.Create(127))), eAdj)

        Dim m = Vector256.BitwiseOr(Vector256.BitwiseAnd(bits, Vector256.Create(&H7FFFFF)),
                                    Vector256.Create(&H3F800000)).AsSingle()
        Dim big = Vector256.GreaterThan(m, Vector256.Create(SQRT2))
        m = Vector256.ConditionalSelect(big, Vector256.Multiply(m, Vector256.Create(0.5F)), m)
        e = Vector256.Add(e, Vector256.ConditionalSelect(big, one, zero))

        Dim t = Vector256.Divide(Vector256.Subtract(m, one), Vector256.Add(m, one))
        Dim t2 = Vector256.Multiply(t, t)
        Dim p = Vector256.Multiply(t2, Vector256.Create(L9))
        p = Vector256.Multiply(t2, Vector256.Add(p, Vector256.Create(L7)))
        p = Vector256.Multiply(t2, Vector256.Add(p, Vector256.Create(L5)))
        p = Vector256.Multiply(t2, Vector256.Add(p, Vector256.Create(L3)))
        Dim log2m = Vector256.Multiply(Vector256.Create(L2E_2), Vector256.Add(t, Vector256.Multiply(t, p)))

        Dim a = Vector256.Multiply(Vector256.Create(kHi), e)
        Dim b = Vector256.Add(Vector256.Multiply(Vector256.Create(kLo), e),
                              Vector256.Multiply(Vector256.Create(kFull), log2m))
        Dim mg = Vector256.Create(MAGIC)
        Dim n = Vector256.Subtract(Vector256.Add(Vector256.Add(a, b), mg), mg)
        Dim r = Vector256.Add(Vector256.Subtract(a, n), b)

        Dim z = Vector256.Multiply(r, Vector256.Create(LN2))
        Dim q = Vector256.Multiply(z, Vector256.Create(E7))
        q = Vector256.Multiply(z, Vector256.Add(q, Vector256.Create(E6)))
        q = Vector256.Multiply(z, Vector256.Add(q, Vector256.Create(E5)))
        q = Vector256.Multiply(z, Vector256.Add(q, Vector256.Create(E4)))
        q = Vector256.Multiply(z, Vector256.Add(q, Vector256.Create(E3)))
        q = Vector256.Multiply(z, Vector256.Add(q, Vector256.Create(E2)))
        q = Vector256.Multiply(z, Vector256.Add(q, one))
        Dim ex = Vector256.Add(one, q)

        Dim nc = Vector256.Min(n, Vector256.Create(127.0F))
        Dim scale = Vector256.ShiftLeft(
            Vector256.Add(Vector256.ConvertToInt32(nc), Vector256.Create(127)), 23).AsSingle()
        Dim res = Vector256.Multiply(ex, scale)

        res = Vector256.ConditionalSelect(Vector256.LessThan(n, Vector256.Create(-126.0F)), zero, res)
        ' Endpoints y NaN, EN ESTE ORDEN: los dos primeros replican los guards del escalar (x<=0 ⇒ 0,
        ' x>=1 ⇒ 1) y el de NaN va ULTIMO porque las comparaciones de arriba son falsas para NaN.
        res = Vector256.ConditionalSelect(Vector256.GreaterThan(xin, zero), res, zero)
        res = Vector256.ConditionalSelect(Vector256.GreaterThanOrEqual(xin, one), one, res)
        res = Vector256.ConditionalSelect(Vector256.Equals(xin, xin), res, xin)
        Return res
    End Function

    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Public Function PowV256(x As Vector256(Of Single), k As PowExp) As Vector256(Of Single)
        Return PowV256(x, k.Hi, k.Lo, k.Full)
    End Function

    ''' <summary><c>pow(x, y)</c> con el exponente VARIABLE POR LANE. Misma ley que <see cref="PowV256"/>; lo
    ''' único que cambia es que el split de Dekker se hace en RUNTIME en vez de precomputado.
    ''' <para>⛔ Existía la idea de que el exponente variable "no se podía" porque el truco de precisión pedía
    ''' una constante. Es FALSO: lo que el truco necesita es que <c>yh·e</c> sea EXACTO, y eso se consigue
    ''' redondeando <c>y</c> a la grilla 2⁻¹² con la constante mágica — <c>yh</c> queda con ≤14 bits
    ''' significativos y <c>e</c> con ≤8, o sea ≤22 &lt; 24 bits de mantisa. Cuesta 5 ops de más, nada más.</para>
    ''' <para>Su consumidor es el soft-light "Illusions" (<c>d^(2^(2(0.5−s)))</c>), donde además el dominio es
    ''' benigno: <c>y ∈ [0,5 , 2]</c> y la base viene acotada a ≥1e-6 ⇒ <c>e ∈ [−20, 0]</c>.</para></summary>
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Public Function PowVarV256(x As Vector256(Of Single), y As Vector256(Of Single)) As Vector256(Of Single)
        Dim zero = Vector256(Of Single).Zero
        Dim one = Vector256.Create(1.0F)

        Dim xin = x
        x = Vector256.Min(Vector256.Max(x, zero), one)

        Dim tiny = Vector256.LessThan(x, Vector256.Create(TINYF))
        Dim xs = Vector256.ConditionalSelect(tiny, Vector256.Multiply(x, Vector256.Create(SCALE24)), x)
        Dim eAdj = Vector256.ConditionalSelect(tiny, Vector256.Create(-24.0F), zero)

        Dim bits = xs.AsInt32()
        Dim e = Vector256.Add(
            Vector256.ConvertToSingle(Vector256.Subtract(
                Vector256.BitwiseAnd(Vector256.ShiftRightLogical(bits, 23), Vector256.Create(&HFF)),
                Vector256.Create(127))), eAdj)

        Dim m = Vector256.BitwiseOr(Vector256.BitwiseAnd(bits, Vector256.Create(&H7FFFFF)),
                                    Vector256.Create(&H3F800000)).AsSingle()
        Dim big = Vector256.GreaterThan(m, Vector256.Create(SQRT2))
        m = Vector256.ConditionalSelect(big, Vector256.Multiply(m, Vector256.Create(0.5F)), m)
        e = Vector256.Add(e, Vector256.ConditionalSelect(big, one, zero))

        Dim t = Vector256.Divide(Vector256.Subtract(m, one), Vector256.Add(m, one))
        Dim t2 = Vector256.Multiply(t, t)
        Dim p = Vector256.Multiply(t2, Vector256.Create(L9))
        p = Vector256.Multiply(t2, Vector256.Add(p, Vector256.Create(L7)))
        p = Vector256.Multiply(t2, Vector256.Add(p, Vector256.Create(L5)))
        p = Vector256.Multiply(t2, Vector256.Add(p, Vector256.Create(L3)))
        Dim log2m = Vector256.Multiply(Vector256.Create(L2E_2), Vector256.Add(t, Vector256.Multiply(t, p)))

        ' ⭐ SPLIT DE DEKKER EN RUNTIME: yh = y redondeado a multiplo de 2^-12 (magic constant sobre y*4096),
        ' yl = y - yh (exacto). Con eso yh*e vuelve a ser EXACTO, que es lo unico que el truco necesitaba.
        Dim g = Vector256.Create(4096.0F)
        Dim mg = Vector256.Create(MAGIC)
        Dim yh = Vector256.Divide(Vector256.Subtract(Vector256.Add(Vector256.Multiply(y, g), mg), mg), g)
        Dim yl = Vector256.Subtract(y, yh)

        Dim a = Vector256.Multiply(yh, e)
        Dim b = Vector256.Add(Vector256.Multiply(yl, e), Vector256.Multiply(y, log2m))
        Dim n = Vector256.Subtract(Vector256.Add(Vector256.Add(a, b), mg), mg)
        Dim r = Vector256.Add(Vector256.Subtract(a, n), b)

        Dim z = Vector256.Multiply(r, Vector256.Create(LN2))
        Dim q = Vector256.Multiply(z, Vector256.Create(E7))
        q = Vector256.Multiply(z, Vector256.Add(q, Vector256.Create(E6)))
        q = Vector256.Multiply(z, Vector256.Add(q, Vector256.Create(E5)))
        q = Vector256.Multiply(z, Vector256.Add(q, Vector256.Create(E4)))
        q = Vector256.Multiply(z, Vector256.Add(q, Vector256.Create(E3)))
        q = Vector256.Multiply(z, Vector256.Add(q, Vector256.Create(E2)))
        q = Vector256.Multiply(z, Vector256.Add(q, one))
        Dim ex = Vector256.Add(one, q)

        Dim nc = Vector256.Min(n, Vector256.Create(127.0F))
        Dim scale = Vector256.ShiftLeft(
            Vector256.Add(Vector256.ConvertToInt32(nc), Vector256.Create(127)), 23).AsSingle()
        Dim res = Vector256.Multiply(ex, scale)

        res = Vector256.ConditionalSelect(Vector256.LessThan(n, Vector256.Create(-126.0F)), zero, res)
        ' ⛔ EXPONENTE NaN -> NaN. Espejo del `If Single.IsNaN(y) Then Return y` del escalar.
        ' ⛔⛔ VA ANTES de los cortes por `x`, NO despues. En una CADENA de selects el ULTIMO gana, y el
        ' escalar corta por `x` PRIMERO: `PowVar(0, NaN)` es 0 y `PowVar(1, NaN)` es 1, no NaN. Tenerlo al
        ' final invertia esa precedencia — y no es teorico: lo pesco WidthParitySelfTest con x=0, y=NaN.
        res = Vector256.ConditionalSelect(Vector256.Equals(y, y), res, y)
        ' Infinitos del exponente: mismo problema y mismo limite que en el escalar (ver alli).
        Dim infP = Vector256.Create(Single.PositiveInfinity)
        res = Vector256.ConditionalSelect(Vector256.Equals(y, infP), zero, res)
        res = Vector256.ConditionalSelect(Vector256.Equals(y, Vector256.Create(Single.NegativeInfinity)), infP, res)
        res = Vector256.ConditionalSelect(Vector256.GreaterThan(xin, zero), res, zero)
        res = Vector256.ConditionalSelect(Vector256.GreaterThanOrEqual(xin, one), one, res)
        res = Vector256.ConditionalSelect(Vector256.Equals(xin, xin), res, xin)
        Return res
    End Function

    ''' <summary><c>2^y</c> para y arbitrario. Es la MITAD exp2 del algoritmo (sin log), y hace falta aparte
    ''' porque <see cref="PowVar1"/>/<see cref="PowVarV256"/> clampean la BASE a [0,1] — pedirles <c>2^y</c>
    ''' devolveria 1. Su consumidor es el exponente interno del soft-light Illusions.</summary>
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Public Function Exp2_1(y As Single) As Single
        If Single.IsNaN(y) Then Return y
        Dim n = (y + MAGIC) - MAGIC
        Dim r = y - n
        Dim z = r * LN2
        Dim q = z * E7
        q = z * (q + E6)
        q = z * (q + E5)
        q = z * (q + E4)
        q = z * (q + E3)
        q = z * (q + E2)
        q = z * (q + 1.0F)
        Dim ex = 1.0F + q
        If n < -126.0F Then Return 0.0F
        If n > 127.0F Then Return Single.PositiveInfinity
        Return ex * BitConverter.Int32BitsToSingle((CInt(n) + 127) << 23)
    End Function

    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Public Function Exp2V256(y As Vector256(Of Single)) As Vector256(Of Single)
        Dim one = Vector256.Create(1.0F)
        Dim mg = Vector256.Create(MAGIC)
        Dim n = Vector256.Subtract(Vector256.Add(y, mg), mg)
        Dim r = Vector256.Subtract(y, n)
        Dim z = Vector256.Multiply(r, Vector256.Create(LN2))
        Dim q = Vector256.Multiply(z, Vector256.Create(E7))
        q = Vector256.Multiply(z, Vector256.Add(q, Vector256.Create(E6)))
        q = Vector256.Multiply(z, Vector256.Add(q, Vector256.Create(E5)))
        q = Vector256.Multiply(z, Vector256.Add(q, Vector256.Create(E4)))
        q = Vector256.Multiply(z, Vector256.Add(q, Vector256.Create(E3)))
        q = Vector256.Multiply(z, Vector256.Add(q, Vector256.Create(E2)))
        q = Vector256.Multiply(z, Vector256.Add(q, one))
        Dim ex = Vector256.Add(one, q)
        Dim nc = Vector256.Min(Vector256.Max(n, Vector256.Create(-126.0F)), Vector256.Create(127.0F))
        Dim scale = Vector256.ShiftLeft(
            Vector256.Add(Vector256.ConvertToInt32(nc), Vector256.Create(127)), 23).AsSingle()
        Dim res = Vector256.Multiply(ex, scale)
        res = Vector256.ConditionalSelect(Vector256.LessThan(n, Vector256.Create(-126.0F)), Vector256(Of Single).Zero, res)
        res = Vector256.ConditionalSelect(Vector256.GreaterThan(n, Vector256.Create(127.0F)),
                                          Vector256.Create(Single.PositiveInfinity), res)
        Return Vector256.ConditionalSelect(Vector256.Equals(y, y), res, y)
    End Function

    ''' <summary>Gemelo ESCALAR de <see cref="PowVarV256"/>: la MISMA secuencia de ops IEEE, para el fallback
    ''' y para que el test de paridad tenga oraculo. Ver el contrato de portabilidad de arriba.</summary>
    Public Function PowVar1(x As Single, y As Single) As Single
        If Single.IsNaN(x) Then Return x
        If x <= 0.0F Then Return 0.0F
        If x >= 1.0F Then Return 1.0F
        ' ⛔ EXPONENTE NaN. Sin este guard `n` sale NaN y el `CInt(n)` del final TIRA OverflowException — o sea
        ' que el escalar CRASHEA donde el vectorial devolvia un numero cualquiera (su ConvertToInt32 de un NaN
        ' no falla, solo da basura). Es ALCANZABLE: el soft-light Illusions pasa y = Exp2_1(2*(0.5-s)) y `s`
        ' sale de una textura, donde un NaN es posible; Clamp01 NO lo filtra (sus dos comparaciones son falsas).
        ' Se devuelve NaN = lo que da MathF.Pow(x, NaN) y la MISMA convencion que Exp2_1 ya tenia.
        ' ⭐ VA DESPUES de los cortes por `x`: asi PowVar1(0, NaN)=0 y PowVar1(1, NaN)=1 no cambian, y el
        ' espejo vectorial ubica su select en EXACTAMENTE ese lugar del orden.
        If Single.IsNaN(y) Then Return y
        ' ⛔ Y LOS INFINITOS TAMBIEN. Con y = ±Inf el split de Dekker da yl = Inf − Inf = NaN, `n` sale NaN y
        ' el `CInt(n)` de abajo vuelve a tirar OverflowException — el MISMO fallo que el guard de NaN, por otra
        ' puerta. Se devuelve el limite matematico, que para x en (0,1) es 0 con y=+Inf e Inf con y=−Inf (los
        ' bordes x=0 y x=1 ya salieron arriba). No es alcanzable desde el soft-light Illusions (su exponente
        ' vive en [0,5 , 2]) pero PowVar1 es Public: lo pesco WidthParitySelfTest con x=0,5 e y=Infinity.
        If Single.IsPositiveInfinity(y) Then Return 0.0F
        If Single.IsNegativeInfinity(y) Then Return Single.PositiveInfinity

        Dim eAdj As Single = 0.0F
        If x < TINYF Then
            x = x * SCALE24
            eAdj = -24.0F
        End If
        Dim bits = BitConverter.SingleToInt32Bits(x)
        Dim e As Single = CSng(((bits >> 23) And &HFF) - 127) + eAdj
        Dim m = BitConverter.Int32BitsToSingle((bits And &H7FFFFF) Or &H3F800000)
        If m > SQRT2 Then
            m = m * 0.5F
            e = e + 1.0F
        End If
        Dim t = (m - 1.0F) / (m + 1.0F)
        Dim t2 = t * t
        Dim p = t2 * L9
        p = t2 * (p + L7)
        p = t2 * (p + L5)
        p = t2 * (p + L3)
        Dim log2m = L2E_2 * (t + t * p)

        Dim yh = ((y * 4096.0F + MAGIC) - MAGIC) / 4096.0F
        Dim yl = y - yh
        Dim a = yh * e
        Dim b = yl * e + y * log2m
        Dim n = (a + b + MAGIC) - MAGIC
        Dim r = (a - n) + b

        Dim z = r * LN2
        Dim q = z * E7
        q = z * (q + E6)
        q = z * (q + E5)
        q = z * (q + E4)
        q = z * (q + E3)
        q = z * (q + E2)
        q = z * (q + 1.0F)
        Dim ex = 1.0F + q

        If n < -126.0F Then Return 0.0F
        If n > 127.0F Then n = 127.0F
        Return ex * BitConverter.Int32BitsToSingle((CInt(n) + 127) << 23)
    End Function

    ' ============================ Vector128 ============================
    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Public Function PowV128(x As Vector128(Of Single), kHi As Single, kLo As Single, kFull As Single) As Vector128(Of Single)
        Dim zero = Vector128(Of Single).Zero
        Dim one = Vector128.Create(1.0F)

        Dim xin = x
        x = Vector128.Min(Vector128.Max(x, zero), one)

        Dim tiny = Vector128.LessThan(x, Vector128.Create(TINYF))
        Dim xs = Vector128.ConditionalSelect(tiny, Vector128.Multiply(x, Vector128.Create(SCALE24)), x)
        Dim eAdj = Vector128.ConditionalSelect(tiny, Vector128.Create(-24.0F), zero)

        Dim bits = xs.AsInt32()
        Dim e = Vector128.Add(
            Vector128.ConvertToSingle(Vector128.Subtract(
                Vector128.BitwiseAnd(Vector128.ShiftRightLogical(bits, 23), Vector128.Create(&HFF)),
                Vector128.Create(127))), eAdj)

        Dim m = Vector128.BitwiseOr(Vector128.BitwiseAnd(bits, Vector128.Create(&H7FFFFF)),
                                    Vector128.Create(&H3F800000)).AsSingle()
        Dim big = Vector128.GreaterThan(m, Vector128.Create(SQRT2))
        m = Vector128.ConditionalSelect(big, Vector128.Multiply(m, Vector128.Create(0.5F)), m)
        e = Vector128.Add(e, Vector128.ConditionalSelect(big, one, zero))

        Dim t = Vector128.Divide(Vector128.Subtract(m, one), Vector128.Add(m, one))
        Dim t2 = Vector128.Multiply(t, t)
        Dim p = Vector128.Multiply(t2, Vector128.Create(L9))
        p = Vector128.Multiply(t2, Vector128.Add(p, Vector128.Create(L7)))
        p = Vector128.Multiply(t2, Vector128.Add(p, Vector128.Create(L5)))
        p = Vector128.Multiply(t2, Vector128.Add(p, Vector128.Create(L3)))
        Dim log2m = Vector128.Multiply(Vector128.Create(L2E_2), Vector128.Add(t, Vector128.Multiply(t, p)))

        Dim a = Vector128.Multiply(Vector128.Create(kHi), e)
        Dim b = Vector128.Add(Vector128.Multiply(Vector128.Create(kLo), e),
                              Vector128.Multiply(Vector128.Create(kFull), log2m))
        Dim mg = Vector128.Create(MAGIC)
        Dim n = Vector128.Subtract(Vector128.Add(Vector128.Add(a, b), mg), mg)
        Dim r = Vector128.Add(Vector128.Subtract(a, n), b)

        Dim z = Vector128.Multiply(r, Vector128.Create(LN2))
        Dim q = Vector128.Multiply(z, Vector128.Create(E7))
        q = Vector128.Multiply(z, Vector128.Add(q, Vector128.Create(E6)))
        q = Vector128.Multiply(z, Vector128.Add(q, Vector128.Create(E5)))
        q = Vector128.Multiply(z, Vector128.Add(q, Vector128.Create(E4)))
        q = Vector128.Multiply(z, Vector128.Add(q, Vector128.Create(E3)))
        q = Vector128.Multiply(z, Vector128.Add(q, Vector128.Create(E2)))
        q = Vector128.Multiply(z, Vector128.Add(q, one))
        Dim ex = Vector128.Add(one, q)

        Dim nc = Vector128.Min(n, Vector128.Create(127.0F))
        Dim scale = Vector128.ShiftLeft(
            Vector128.Add(Vector128.ConvertToInt32(nc), Vector128.Create(127)), 23).AsSingle()
        Dim res = Vector128.Multiply(ex, scale)

        res = Vector128.ConditionalSelect(Vector128.LessThan(n, Vector128.Create(-126.0F)), zero, res)
        res = Vector128.ConditionalSelect(Vector128.GreaterThan(xin, zero), res, zero)
        res = Vector128.ConditionalSelect(Vector128.GreaterThanOrEqual(xin, one), one, res)
        res = Vector128.ConditionalSelect(Vector128.Equals(xin, xin), res, xin)
        Return res
    End Function

    <MethodImpl(MethodImplOptions.AggressiveInlining)>
    Public Function PowV128(x As Vector128(Of Single), k As PowExp) As Vector128(Of Single)
        Return PowV128(x, k.Hi, k.Lo, k.Full)
    End Function

End Module
