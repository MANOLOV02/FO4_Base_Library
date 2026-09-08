Option Strict On
Option Explicit On

Imports System.Runtime.Intrinsics
Imports FO4_Base_Library.Havok.Canon.Objects

' =================================================================================================
' LAS CINCO `*Mx` DE ENLACE — tipos 13, 14, 15, 16 y 21.
'
' ⛔ CERO apariciones en el corpus vanilla: la única `Mx` que el corpus trae es
' `hclVolumeConstraintMx` (38 sets, ya transcrita). Las cinco están igual porque un mod puede
' traerlas, y hasta hoy caían en el hueco.
'
' ⭐ LA LEY, cerrada en el RE (cap. 6.5bis) con `hclStandardLinkConstraintSetMx::solve`
' (`0x141A062D0`) como patrón de las seis:
'
'     si k <= 0: return
'     BatchedKernel(set, positions, &kVec)     ' lotes de 16
'     SingleKernel (set, positions, &kVec)     ' la cola suelta
'
' y la medición que cerró la duda de las dos rigideces: el kernel por lotes **no lee `particleDatas`
' ni una vez** (0 accesos en sus 408 instrucciones), mientras el escalar equivalente lo lee dos
' (`invMass[A]`, `invMass[B]`).
'
' ⇒ **`stiffnessA/B` ya traen `stiffness · invMass` premultiplicado** por el compilador HCL offline.
'
' ⚠️⚠️ PERO ESO NO VALE PARA LAS CINCO, y el RE no lo distingue. La reflexión dice cuáles traen las
' masas aparte:
'
'   · `StandardLinkMx`     — `restLength, stiffnessA, stiffnessB, pA, pB`         ⇒ premultiplicadas
'   · `CompressibleLinkMx` — `restLength, compressionLength, stiffnessA/B, pA, pB` ⇒ premultiplicadas
'   · `StretchLinkMx`      — `restLength, stiffness, pA, pB`                       ⇒ una sola, y su
'     gemelo escalar tampoco usa masa (mueve UNA partícula, `0x141A06E53`)
'   · `BendLinkMx`         — trae **`invMassA` e `invMassB` EXPLÍCITAS**
'   · `BendStiffnessMx`    — trae **`invMassA..D` EXPLÍCITAS** (son cuatro partículas)
'
' Las dos últimas NO llevan la masa horneada: la traen en el propio registro. Suponer lo contrario
' aplicaría la masa dos veces.
'
' ⛔ EL ORDEN: `Batches` primero y `Singles` después, que es como el motor los corre. Acá se aplanan
' los dos a la misma forma conservando ese orden — la ley es idéntica, sólo cambia el empaquetado.
'
' ⚠️ Lo que NO se transcribió instrucción por instrucción son los kernels SoA de los cinco (el RE
' avisa: «la equivalencia hay que exigirla con un A/B contra el escalar, no suponerla»). Lo que se
' aplica acá es la ley del GEMELO ESCALAR de cada uno, que sí está transcrita y probada, con las
' rigideces del registro `Mx`. El gate G22 hace ese A/B: con `invMass = 1` y la rigidez repartida
' igual, la `Mx` y su gemelo tienen que dar el MISMO número.
' =================================================================================================

#If DEBUG Then

Namespace Havok.Motor

    ''' <summary>
    ''' `hclStandardLinkConstraintSetMx` (tipo 13) — `0x141A062D0`, el patrón de los **seis** `Mx`.
    ''' <para>El envoltorio hace `si k &lt;= 0: return`, difunde `k` y llama a DOS kernels: el de
    ''' **lotes de 16** (`0x141A063B0`, "TtBatched Standard Links") y el de la **cola suelta**
    ''' (`0x141A66F90`, "TtSingle Standard Links").</para>
    ''' <para>⭐⭐ **La duda de las dos rigideces está cerrada por medición**: el kernel por lotes
    ''' **no lee `particleDatas` ni una vez** (0 accesos con `scale 8` y `disp 4/8/0xC` en sus 408
    ''' instrucciones), mientras el escalar equivalente (`0x141A06170`) lo lee dos veces. Y el
    ''' `MxSingle` lee el registro de 16 B `{restLength@0, stiffnessA@4, stiffnessB@8,
    ''' particleA@0xC, particleB@0xE}` sin tocar masas — leído en `0x141A67025`-`0x141A6702F`.</para>
    ''' <para>⇒ La ley `Mx` **no es otra ley**: es la misma con la masa horneada.</para>
    ''' <para>```
    ''' d̂ = normalizar(P[B] − P[A])            ' 0x141A6706D/67092, rsqrt CRUDO con guarda
    ''' c  = (|d| − restLength) · k              ' 0x141A670C8/CB
    ''' P[A] += (c · stiffnessA) · d̂            ' 0x141A670D5/DB → 0x141A670DE **addps**
    ''' P[B] += (c · stiffnessB) · d̂            ' 0x141A670D2/D8 → 0x141A670E6 **addps**
    ''' ```</para>
    ''' <para>⛔⛔ **LAS DOS ESCRITURAS SUMAN.** El escalar resta en B; acá NO, porque el signo lo
    ''' trae `stiffnessB`. El batched hace lo mismo en sus ocho (`0x141A068B1`, `068ED`, `068F6`,
    ''' `068FE`, `0690A`, `06917`, `06928`, `06939`). Copiar el `Subtract` del gemelo escalar —que es
    ''' lo que hizo la primera versión de esta clase— invierte la fuerza sobre B.</para>
    ''' <para>⚠️ HIPÓTESIS DECLARADA: que el compilador HCL hornee el signo negativo en `stiffnessB`
    ''' NO se puede medir — el censo trae **cero** `hclStandardLinkConstraintSetMx`. Lo medido es
    ''' que el motor SUMA, y eso es lo que se replica (motor-53).</para>
    ''' <para>⛔ El agrupamiento `(c · stiffness) · d̂` NO es el del gemelo escalar
    ''' (`c · d̂ · stiffness`): el producto flotante no es asociativo. Con `k = 1` coinciden, que es
    ''' la condición con la que G22 exige la equivalencia.</para>
    ''' </summary>
    Friend NotInheritable Class EnlaceEstandarMx
        Inherits SetCompilado

        Private ReadOnly _a As Integer(), _b As Integer()
        Private ReadOnly _restLength As Single()
        Private ReadOnly _stiffA As Single(), _stiffB As Single()
        Private ReadOnly _n As Integer

        ''' <summary>Un solo enlace, sin archivo: para los gates de la ley.</summary>
        Friend Sub New(a As Integer, b As Integer, restLength As Single,
                       stiffA As Single, stiffB As Single)
            MyBase.New(13, "prueba")
            _a = {a} : _b = {b} : _restLength = {restLength}
            _stiffA = {stiffA} : _stiffB = {stiffB}
            _n = 1
        End Sub

        Friend Overrides ReadOnly Property Cuenta As Integer
            Get
                Return _n
            End Get
        End Property

        Friend Sub New(src As HkObj_HclStandardLinkConstraintSetMx, tipo As Integer)
            MyBase.New(tipo, src.Name)
            Dim a As New List(Of Integer)(), b As New List(Of Integer)()
            Dim r As New List(Of Single)(), sa As New List(Of Single)(), sb As New List(Of Single)()
            If src.Batches IsNot Nothing Then
                For Each t In src.Batches
                    If t Is Nothing OrElse t.ParticlesA Is Nothing Then Continue For
                    For i = 0 To t.ParticlesA.Count - 1
                        a.Add(t.ParticlesA(i))
                        b.Add(Lista(t.ParticlesB, i, 0))
                        r.Add(ListaF(t.RestLengths, i))
                        sa.Add(ListaF(t.StiffnessesA, i))
                        sb.Add(ListaF(t.StiffnessesB, i))
                    Next
                Next
            End If
            If src.Singles IsNot Nothing Then
                For Each e In src.Singles
                    If e Is Nothing Then Continue For
                    a.Add(e.ParticleA) : b.Add(e.ParticleB)
                    r.Add(e.RestLength) : sa.Add(e.StiffnessA) : sb.Add(e.StiffnessB)
                Next
            End If
            _a = a.ToArray() : _b = b.ToArray() : _restLength = r.ToArray()
            _stiffA = sa.ToArray() : _stiffB = sb.ToArray()
            _n = _a.Length
        End Sub

        ''' <summary>
        ''' La ley de `EnlaceEstandar` con las rigideces del registro `Mx`.
        ''' <para>⛔ NO se multiplica por `invMass`: viene horneada en `stiffnessA/B`. Hacerlo la
        ''' aplicaría dos veces.</para>
        ''' <para>⛔⛔ Y LAS DOS ESCRITURAS SUMAN (`0x141A670DE` y `0x141A670E6`).</para>
        ''' </summary>
        Protected Overrides Sub Kernel(ctx As ContextoDeSolve, k As Vector128(Of Single))
            Dim pos = ctx.Instancia.Posiciones
            For i = 0 To _n - 1
                Dim ia = _a(i), ib = _b(i)
                Dim d = Vector128.Subtract(Simd.Leer(pos, ib), Simd.Leer(pos, ia))   ' 0x141A67092
                Dim len2 = Simd.Dot3(d, d)
                Dim invLen = Simd.RsqrtConGuarda(len2)                               ' 0x141A670B4…BF
                Dim dHat = Vector128.Multiply(invLen, d)                             ' 0x141A670C2
                Dim c = Vector128.Subtract(Vector128.Multiply(len2, invLen),
                                           Vector128.Create(_restLength(i)))         ' 0x141A670C5/C8
                c = Vector128.Multiply(c, k)                                         ' 0x141A670CB
                Dim fa = Vector128.Multiply(Vector128.Multiply(c, Vector128.Create(_stiffA(i))), dHat)
                Dim fb = Vector128.Multiply(Vector128.Multiply(c, Vector128.Create(_stiffB(i))), dHat)
                Simd.Escribir(pos, ia, Vector128.Add(Simd.Leer(pos, ia), fa))        ' 0x141A670DE addps
                Simd.Escribir(pos, ib, Vector128.Add(Simd.Leer(pos, ib), fb))        ' 0x141A670E6 addps
            Next
        End Sub

        Friend Shared Function Lista(l As IList(Of Integer), i As Integer, porDefecto As Integer) As Integer
            Return If(l Is Nothing OrElse i >= l.Count, porDefecto, l(i))
        End Function

        Friend Shared Function ListaF(l As IList(Of Single), i As Integer) As Single
            Return If(l Is Nothing OrElse i >= l.Count, 0.0F, l(i))
        End Function

    End Class

    ''' <summary>`hclStretchLinkConstraintSetMx` — tipo 15, `0x141A06EF0`.</summary>
    Friend NotInheritable Class EnlaceDeEstiramientoMx
        Inherits SetCompilado

        Private ReadOnly _a As Integer(), _b As Integer()
        Private ReadOnly _restLength As Single(), _stiffness As Single()
        Private ReadOnly _n As Integer

        Friend Overrides ReadOnly Property Cuenta As Integer
            Get
                Return _n
            End Get
        End Property

        ''' <summary>Un solo registro, sin archivo: para el A/B de G22.</summary>
        Friend Sub New(a As Integer, b As Integer, restLength As Single, stiffness As Single)
            MyBase.New(15, "prueba")
            _a = {a} : _b = {b} : _restLength = {restLength} : _stiffness = {stiffness}
            _n = 1
        End Sub

        Friend Sub New(src As HkObj_HclStretchLinkConstraintSetMx, tipo As Integer)
            MyBase.New(tipo, src.Name)
            Dim a As New List(Of Integer)(), b As New List(Of Integer)()
            Dim r As New List(Of Single)(), st As New List(Of Single)()
            If src.Batches IsNot Nothing Then
                For Each t In src.Batches
                    If t Is Nothing OrElse t.ParticlesA Is Nothing Then Continue For
                    For i = 0 To t.ParticlesA.Count - 1
                        a.Add(t.ParticlesA(i))
                        b.Add(EnlaceEstandarMx.Lista(t.ParticlesB, i, 0))
                        r.Add(EnlaceEstandarMx.ListaF(t.RestLengths, i))
                        st.Add(EnlaceEstandarMx.ListaF(t.Stiffnesses, i))
                    Next
                Next
            End If
            If src.Singles IsNot Nothing Then
                For Each e In src.Singles
                    If e Is Nothing Then Continue For
                    a.Add(CInt(e.ParticleA)) : b.Add(CInt(e.ParticleB))
                    r.Add(e.RestLength) : st.Add(e.Stiffness)
                Next
            End If
            _a = a.ToArray() : _b = b.ToArray()
            _restLength = r.ToArray() : _stiffness = st.ToArray()
            _n = _a.Length
        End Sub

        ''' <summary>
        ''' La ley de `EnlaceDeEstiramiento`: mueve UNA sola partícula y no usa masa.
        ''' <para>⛔ `min(c, 0)` — sólo corrige cuando el enlace está ESTIRADO más allá del reposo
        ''' (`0x141A06E46 minps`). Y escribe sólo en `B` (`0x141A06E53`). No es un enlace simétrico a
        ''' medias: es así.</para>
        ''' </summary>
        Protected Overrides Sub Kernel(ctx As ContextoDeSolve, k As Vector128(Of Single))
            Dim pos = ctx.Instancia.Posiciones
            Dim cero = Vector128(Of Single).Zero
            For i = 0 To _n - 1
                Dim ia = _a(i), ib = _b(i)
                Dim d = Vector128.Subtract(Simd.Leer(pos, ib), Simd.Leer(pos, ia))
                Dim len2 = Simd.Dot3(d, d)
                Dim invLen = Simd.RsqrtConGuarda(len2)
                Dim c = Vector128.Subtract(Vector128.Create(_restLength(i)),
                                           Vector128.Multiply(invLen, len2))
                c = Vector128.Min(c, cero)
                c = Vector128.Multiply(c, Vector128.Create(_stiffness(i)))
                c = Vector128.Multiply(c, k)
                c = Vector128.Multiply(c, Vector128.Multiply(invLen, d))
                Simd.Escribir(pos, ib, Vector128.Add(Simd.Leer(pos, ib), c))
            Next
        End Sub

    End Class

    ''' <summary>`hclCompressibleLinkConstraintSetMx` — tipo 21, `0x1419FEA00`.</summary>
    Friend NotInheritable Class EnlaceCompresibleMx
        Inherits SetCompilado

        Private ReadOnly _a As Integer(), _b As Integer()
        Private ReadOnly _restLength As Single(), _compresion As Single()
        Private ReadOnly _stiffA As Single(), _stiffB As Single()
        Private ReadOnly _n As Integer

        Friend Overrides ReadOnly Property Cuenta As Integer
            Get
                Return _n
            End Get
        End Property

        ''' <summary>Un solo registro, sin archivo: para el A/B de G22.</summary>
        Friend Sub New(a As Integer, b As Integer, restLength As Single, compresion As Single,
                       stiffA As Single, stiffB As Single)
            MyBase.New(21, "prueba")
            _a = {a} : _b = {b} : _restLength = {restLength} : _compresion = {compresion}
            _stiffA = {stiffA} : _stiffB = {stiffB}
            _n = 1
        End Sub

        Friend Sub New(src As HkObj_HclCompressibleLinkConstraintSetMx, tipo As Integer)
            MyBase.New(tipo, src.Name)
            Dim a As New List(Of Integer)(), b As New List(Of Integer)()
            Dim r As New List(Of Single)(), cp As New List(Of Single)()
            Dim sa As New List(Of Single)(), sb As New List(Of Single)()
            If src.Batches IsNot Nothing Then
                For Each t In src.Batches
                    If t Is Nothing OrElse t.ParticlesA Is Nothing Then Continue For
                    For i = 0 To t.ParticlesA.Count - 1
                        a.Add(t.ParticlesA(i))
                        b.Add(EnlaceEstandarMx.Lista(t.ParticlesB, i, 0))
                        r.Add(EnlaceEstandarMx.ListaF(t.RestLengths, i))
                        cp.Add(EnlaceEstandarMx.ListaF(t.CompressionLengths, i))
                        sa.Add(EnlaceEstandarMx.ListaF(t.StiffnessesA, i))
                        sb.Add(EnlaceEstandarMx.ListaF(t.StiffnessesB, i))
                    Next
                Next
            End If
            If src.Singles IsNot Nothing Then
                For Each e In src.Singles
                    If e Is Nothing Then Continue For
                    a.Add(e.ParticleA) : b.Add(e.ParticleB)
                    r.Add(e.RestLength) : cp.Add(e.CompressionLength)
                    sa.Add(e.StiffnessA) : sb.Add(e.StiffnessB)
                Next
            End If
            _a = a.ToArray() : _b = b.ToArray()
            _restLength = r.ToArray() : _compresion = cp.ToArray()
            _stiffA = sa.ToArray() : _stiffB = sb.ToArray()
            _n = _a.Length
        End Sub

        ''' <summary>
        ''' La banda muerta entre `compressionLength` y `restLength`, LEÍDA en `0x141A66A50`…`B35`.
        ''' <para>```
        ''' ksA = k · stiffnessA ; ksB = k · stiffnessB     ' 0x141A66A88/A92, mulss ANTES de todo
        ''' d̂   = normalizar(P[B] − P[A])                  ' 0x141A66A99/AAB, rsqrt CRUDO
        ''' estirado   = (restLength ≤ |d|)                ' 0x141A66AF1 cmpleps
        ''' comprimido = (|d| ≤ compressionLength)         ' 0x141A66AF8 cmpleps
        ''' c = comprimido ? (|d| − compressionLength)
        '''     : estirado ? (|d| − restLength) : 0         ' 0x141A66AFF…B16, sin saltos
        ''' P[A] += (ksA · c) · d̂                          ' 0x141A66B19/B20 → 0x141A66B28 **addps**
        ''' P[B] += (ksB · c) · d̂                          ' 0x141A66B1C/B24 → 0x141A66B30 **addps**
        ''' ```</para>
        ''' <para>⛔⛔ **LAS DOS ESCRITURAS SUMAN**, como en `StandardLinkMx`: el signo lo trae
        ''' `stiffnessB`. El gemelo escalar RESTA en B (`0x1419FE954`), y copiarle esa resta —que es
        ''' lo que hizo la primera versión de esta clase— invierte la fuerza sobre B.</para>
        ''' <para>⛔ La prioridad es del COMPRIMIDO: el `andnps` de `0x141A66B09` tira el término de
        ''' estiramiento donde la máscara de compresión vale. Sólo se notan las dos a la vez si
        ''' `compressionLength &gt; restLength`, y ahí gana la de compresión.</para>
        ''' <para>⚠️ Los bordes son `≤` en las DOS comparaciones, al revés que el gemelo escalar
        ''' (`comp &gt; largo` / `largo &gt; rest`, `0x1419FE90C`/`917`). En `|d| = comp` con
        ''' `comp &gt; rest` los dos motores difieren, y es el binario el que manda en cada clase.</para>
        ''' </summary>
        Protected Overrides Sub Kernel(ctx As ContextoDeSolve, k As Vector128(Of Single))
            Dim pos = ctx.Instancia.Posiciones
            Dim cero = Vector128(Of Single).Zero
            For i = 0 To _n - 1
                Dim ia = _a(i), ib = _b(i)
                Dim pa = Simd.Leer(pos, ia)
                ' ⛔ `k · stiffness` PRIMERO: el binario lo hace con `mulss` antes del bucle de
                ' aritmética (`0x141A66A88`/`A92`), y el producto flotante no es asociativo.
                Dim ksA = Vector128.Multiply(k, Vector128.Create(_stiffA(i)))
                Dim ksB = Vector128.Multiply(k, Vector128.Create(_stiffB(i)))
                Dim d = Vector128.Subtract(Simd.Leer(pos, ib), pa)                   ' 0x141A66AAB
                Dim len2 = Simd.Dot3(d, d)
                Dim invLen = Simd.RsqrtConGuarda(len2)                               ' 0x141A66AD3…DE
                Dim dHat = Vector128.Multiply(invLen, d)                             ' 0x141A66AE1
                Dim largo = Simd.Lane0(Vector128.Multiply(invLen, len2))             ' 0x141A66AE5

                Dim rest = _restLength(i), comp = _compresion(i)
                Dim estirado = rest <= largo                                         ' 0x141A66AF1
                Dim comprimido = largo <= comp                                       ' 0x141A66AF8
                Dim c As Vector128(Of Single)
                If comprimido Then
                    c = Vector128.Create(largo - comp)                               ' 0x141A66AF5
                ElseIf estirado Then
                    c = Vector128.Create(largo - rest)                               ' 0x141A66AEE
                Else
                    c = cero                                                         ' 0x141A66B02 andnps
                End If

                Simd.Escribir(pos, ia, Vector128.Add(pa,
                    Vector128.Multiply(Vector128.Multiply(ksA, c), dHat)))           ' 0x141A66B28 addps
                Simd.Escribir(pos, ib, Vector128.Add(Simd.Leer(pos, ib),
                    Vector128.Multiply(Vector128.Multiply(ksB, c), dHat)))           ' 0x141A66B30 addps
            Next
        End Sub

    End Class

    ''' <summary>
    ''' `hclBendLinkConstraintSetMx` — tipo 14, `0x1419F91A0`.
    ''' <para>⛔ Esta `Mx` trae las `invMass` EXPLÍCITAS en su registro, así que la masa NO está
    ''' horneada en las rigideces. Tratarla como `StandardLinkMx` la aplicaría dos veces.</para>
    ''' </summary>
    Friend NotInheritable Class EnlaceDeDoblezMx
        Inherits SetCompilado

        Private ReadOnly _a As Integer(), _b As Integer()
        Private ReadOnly _bendMin As Single(), _stretchMax As Single()
        Private ReadOnly _stretchK As Single(), _bendK As Single()
        Private ReadOnly _invA As Single(), _invB As Single()
        Private ReadOnly _n As Integer

        Friend Overrides ReadOnly Property Cuenta As Integer
            Get
                Return _n
            End Get
        End Property

        ''' <summary>Un solo registro, sin archivo: para el A/B de G22.</summary>
        Friend Sub New(a As Integer, b As Integer, bendMin As Single, stretchMax As Single,
                       bendK As Single, stretchK As Single, invA As Single, invB As Single)
            MyBase.New(14, "prueba")
            _a = {a} : _b = {b} : _bendMin = {bendMin} : _stretchMax = {stretchMax}
            _stretchK = {stretchK} : _bendK = {bendK} : _invA = {invA} : _invB = {invB}
            _n = 1
        End Sub

        Friend Sub New(src As HkObj_HclBendLinkConstraintSetMx, tipo As Integer)
            MyBase.New(tipo, src.Name)
            Dim a As New List(Of Integer)(), b As New List(Of Integer)()
            Dim bm As New List(Of Single)(), sm As New List(Of Single)()
            Dim sk As New List(Of Single)(), bk As New List(Of Single)()
            Dim ma As New List(Of Single)(), mb As New List(Of Single)()
            If src.Batches IsNot Nothing Then
                For Each t In src.Batches
                    If t Is Nothing OrElse t.ParticlesA Is Nothing Then Continue For
                    For i = 0 To t.ParticlesA.Count - 1
                        a.Add(t.ParticlesA(i))
                        b.Add(EnlaceEstandarMx.Lista(t.ParticlesB, i, 0))
                        bm.Add(EnlaceEstandarMx.ListaF(t.BendMinLengths, i))
                        sm.Add(EnlaceEstandarMx.ListaF(t.StretchMaxLengths, i))
                        sk.Add(EnlaceEstandarMx.ListaF(t.StretchStiffnesses, i))
                        bk.Add(EnlaceEstandarMx.ListaF(t.BendStiffnesses, i))
                        ma.Add(EnlaceEstandarMx.ListaF(t.InvMassesA, i))
                        mb.Add(EnlaceEstandarMx.ListaF(t.InvMassesB, i))
                    Next
                Next
            End If
            If src.Singles IsNot Nothing Then
                For Each e In src.Singles
                    If e Is Nothing Then Continue For
                    a.Add(e.ParticleA) : b.Add(e.ParticleB)
                    bm.Add(e.BendMinLength) : sm.Add(e.StretchMaxLength)
                    sk.Add(e.StretchStiffness) : bk.Add(e.BendStiffness)
                    ma.Add(e.InvMassA) : mb.Add(e.InvMassB)
                Next
            End If
            _a = a.ToArray() : _b = b.ToArray()
            _bendMin = bm.ToArray() : _stretchMax = sm.ToArray()
            _stretchK = sk.ToArray() : _bendK = bk.ToArray()
            _invA = ma.ToArray() : _invB = mb.ToArray()
            _n = _a.Length
        End Sub

        ''' <summary>La ley de `EnlaceDeDoblez`: dos bandas, la de estiramiento y la de doblez, con
        ''' `max(0, …)` cada una y la corrección como su DIFERENCIA.</summary>
        Protected Overrides Sub Kernel(ctx As ContextoDeSolve, k As Vector128(Of Single))
            Dim pos = ctx.Instancia.Posiciones
            Dim cero = Vector128(Of Single).Zero
            For i = 0 To _n - 1
                Dim ia = _a(i), ib = _b(i)
                Dim d = Vector128.Subtract(Simd.Leer(pos, ib), Simd.Leer(pos, ia))
                Dim len2 = Simd.Dot3(d, d)
                Dim invLen = Simd.RsqrtConGuarda(len2)
                Dim largo = Vector128.Multiply(invLen, len2)
                Dim dHat = Vector128.Multiply(invLen, d)

                Dim est = Vector128.Max(cero, Vector128.Subtract(largo, Vector128.Create(_stretchMax(i))))
                est = Vector128.Multiply(est, Vector128.Create(_stretchK(i)))
                Dim dob = Vector128.Max(cero, Vector128.Subtract(Vector128.Create(_bendMin(i)), largo))
                dob = Vector128.Multiply(dob, Vector128.Create(_bendK(i)))

                Dim c = Vector128.Subtract(est, dob)
                c = Vector128.Multiply(c, k)
                c = Vector128.Multiply(c, dHat)
                Simd.Escribir(pos, ia, Vector128.Add(Simd.Leer(pos, ia),
                    Vector128.Multiply(c, Vector128.Create(_invA(i)))))
                Simd.Escribir(pos, ib, Vector128.Subtract(Simd.Leer(pos, ib),
                    Vector128.Multiply(c, Vector128.Create(_invB(i)))))
            Next
        End Sub

    End Class

    ''' <summary>
    ''' `hclBendStiffnessConstraintSetMx` — tipo 16, `0x1419FA080`.
    ''' <para>⛔ Como `BendLinkMx`, trae las `invMass` EXPLICITAS — aca cuatro, una por particula —,
    ''' asi que la masa NO esta horneada en la rigidez.</para>
    ''' <para>⭐ El nucleo de la ley es el MISMO objeto que usa el gemelo escalar
    ''' (`RigidezDeDoblez.VectorDeDoblez`): dos copias de esa aritmetica —dos productos cruzados y
    ''' tres `rsqrt` crudos— podrian divergir sin que nada lo viera.</para>
    ''' </summary>
    Friend NotInheritable Class RigidezDeDoblezMx
        Inherits SetCompilado

        Private ReadOnly _a As Integer(), _b As Integer(), _c As Integer(), _d As Integer()
        Private ReadOnly _wA As Single(), _wB As Single(), _wC As Single(), _wD As Single()
        Private ReadOnly _bend As Single(), _restCurvatura As Single()
        Private ReadOnly _invA As Single(), _invB As Single(), _invC As Single(), _invD As Single()
        Private ReadOnly _usaReposo As Boolean
        Private ReadOnly _n As Integer

        Friend Overrides ReadOnly Property Cuenta As Integer
            Get
                Return _n
            End Get
        End Property

        ''' <summary>Un solo registro, sin archivo: para el A/B de G22.</summary>
        Friend Sub New(a As Integer, b As Integer, c As Integer, d As Integer,
                       wA As Single, wB As Single, wC As Single, wD As Single,
                       bend As Single, restCurvatura As Single, usaReposo As Boolean,
                       invA As Single, invB As Single, invC As Single, invD As Single)
            MyBase.New(16, "prueba")
            _a = {a} : _b = {b} : _c = {c} : _d = {d}
            _wA = {wA} : _wB = {wB} : _wC = {wC} : _wD = {wD}
            _bend = {bend} : _restCurvatura = {restCurvatura} : _usaReposo = usaReposo
            _invA = {invA} : _invB = {invB} : _invC = {invC} : _invD = {invD}
            _n = 1
        End Sub

        Friend Sub New(src As HkObj_HclBendStiffnessConstraintSetMx, tipo As Integer)
            MyBase.New(tipo, src.Name)
            _usaReposo = src.UseRestPoseConfig
            Dim a As New List(Of Integer)(), b As New List(Of Integer)()
            Dim c As New List(Of Integer)(), d As New List(Of Integer)()
            Dim wa As New List(Of Single)(), wb As New List(Of Single)()
            Dim wc As New List(Of Single)(), wd As New List(Of Single)()
            Dim bk As New List(Of Single)(), rc As New List(Of Single)()
            Dim ma As New List(Of Single)(), mb As New List(Of Single)()
            Dim mc As New List(Of Single)(), md As New List(Of Single)()
            If src.Batches IsNot Nothing Then
                For Each t In src.Batches
                    If t Is Nothing OrElse t.ParticlesA Is Nothing Then Continue For
                    For i = 0 To t.ParticlesA.Count - 1
                        a.Add(t.ParticlesA(i))
                        b.Add(EnlaceEstandarMx.Lista(t.ParticlesB, i, 0))
                        c.Add(EnlaceEstandarMx.Lista(t.ParticlesC, i, 0))
                        d.Add(EnlaceEstandarMx.Lista(t.ParticlesD, i, 0))
                        wa.Add(EnlaceEstandarMx.ListaF(t.WeightsA, i))
                        wb.Add(EnlaceEstandarMx.ListaF(t.WeightsB, i))
                        wc.Add(EnlaceEstandarMx.ListaF(t.WeightsC, i))
                        wd.Add(EnlaceEstandarMx.ListaF(t.WeightsD, i))
                        bk.Add(EnlaceEstandarMx.ListaF(t.BendStiffnesses, i))
                        rc.Add(EnlaceEstandarMx.ListaF(t.RestCurvatures, i))
                        ma.Add(EnlaceEstandarMx.ListaF(t.InvMassesA, i))
                        mb.Add(EnlaceEstandarMx.ListaF(t.InvMassesB, i))
                        mc.Add(EnlaceEstandarMx.ListaF(t.InvMassesC, i))
                        md.Add(EnlaceEstandarMx.ListaF(t.InvMassesD, i))
                    Next
                Next
            End If
            If src.Singles IsNot Nothing Then
                For Each e In src.Singles
                    If e Is Nothing Then Continue For
                    a.Add(e.ParticleA) : b.Add(e.ParticleB)
                    c.Add(e.ParticleC) : d.Add(e.ParticleD)
                    wa.Add(e.WeightA) : wb.Add(e.WeightB) : wc.Add(e.WeightC) : wd.Add(e.WeightD)
                    bk.Add(e.BendStiffness) : rc.Add(e.RestCurvature)
                    ma.Add(e.InvMassA) : mb.Add(e.InvMassB) : mc.Add(e.InvMassC) : md.Add(e.InvMassD)
                Next
            End If
            _a = a.ToArray() : _b = b.ToArray() : _c = c.ToArray() : _d = d.ToArray()
            _wA = wa.ToArray() : _wB = wb.ToArray() : _wC = wc.ToArray() : _wD = wd.ToArray()
            _bend = bk.ToArray() : _restCurvatura = rc.ToArray()
            _invA = ma.ToArray() : _invB = mb.ToArray()
            _invC = mc.ToArray() : _invD = md.ToArray()
            _n = _a.Length
        End Sub

        Protected Overrides Sub Kernel(ctx As ContextoDeSolve, k As Vector128(Of Single))
            Dim pos = ctx.Instancia.Posiciones
            For i = 0 To _n - 1
                Dim ia = _a(i), ib = _b(i), ic = _c(i), id = _d(i)
                Dim pA = Simd.Leer(pos, ia), pB = Simd.Leer(pos, ib)
                Dim pC = Simd.Leer(pos, ic), pD = Simd.Leer(pos, id)
                Dim wA = Vector128.Create(_wA(i)), wB = Vector128.Create(_wB(i))
                Dim wC = Vector128.Create(_wC(i)), wD = Vector128.Create(_wD(i))
                Dim kb = Vector128.Multiply(Vector128.Create(_bend(i)), k)

                Dim w = RigidezDeDoblez.VectorDeDoblez(pA, pB, pC, pD, wA, wB, wC, wD,
                                                       _usaReposo, _restCurvatura(i))

                RigidezDeDoblez.Escribir4ConMasa(pos, ia, _invA(i), kb, wA, w)
                RigidezDeDoblez.Escribir4ConMasa(pos, ib, _invB(i), kb, wB, w)
                RigidezDeDoblez.Escribir4ConMasa(pos, ic, _invC(i), kb, wC, w)
                RigidezDeDoblez.Escribir4ConMasa(pos, id, _invD(i), kb, wD, w)
            Next
        End Sub

    End Class

End Namespace

#End If
