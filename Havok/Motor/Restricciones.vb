Option Strict On
Option Explicit On

Imports System.Runtime.Intrinsics
Imports FO4_Base_Library.Havok.Canon.Objects

' =================================================================================================
' LOS CONJUNTOS DE RESTRICCIONES DE TIPO «ENLACE» — los cuatro que el corpus usa de a cientos.
'
' Ley: RE_MOTOR_FISICA_CANONICO_2026-09-05.md, cap. 6.5, verificado instrucción por instrucción:
'   · `hclStandardLinkConstraintSet`     0x141A06170  (envoltorio 0x141A06080, "TtSolve Links")
'   · `hclStretchLinkConstraintSet`      0x141A06DB0  (envoltorio 0x141A06CC0)
'   · `hclBendLinkConstraintSet`         0x1419F8F70
'   · `hclCompressibleLinkConstraintSet` 0x1419FE850
'
' ⛔⛔ TRES COSAS QUE NO SON LO QUE PARECEN, y que hay que respetar:
'
' 1. **No hay reparto por masa combinada.** Ningún enlace usa `wa/(wa+wb)`: cada partícula se mueve
'    por SU propia `invMass`. Meter el reparto «correcto» de PBD cambia toda la tela.
' 2. **`Stretch` mueve UNA sola partícula y sin `invMass`.** `P[B] += c`, y nada más (0x141A06E53).
'    No es un enlace simétrico a medias: es así.
' 3. **El `rsqrtps` de los cuatro va CRUDO**, sin Newton (`0x141A061E7`, `0x141A06E1F`,
'    `0x1419F9020`, `0x1419FE8E8`). Refinarlo cambia el resultado del motor.
'
' ⭐ El orden de escritura es Gauss-Seidel dentro del bucle: `P[A]` se escribe ANTES de leer `P[B]`.
' Con `A == B` eso importa, y el motor no lo evita.
' =================================================================================================


Namespace Havok.Motor

    ''' <summary>
    ''' Lo que el motor le pasa a `solve` de un `hclConstraintSet`. No es un envoltorio de
    ''' conveniencia: los tres campos salen de la firma real, leída en
    ''' `hclLocalRangeConstraintSet::solve` (`0x141A01F10`).
    ''' <para>⛔ Los sets de enlace no miran ni `Buffers` ni `UsaK`, pero
    ''' `LocalRange`, `Transition` y `AntiPinch` sí, y los tres los reciben por la MISMA firma
    ''' virtual. Darles a unos una firma y a otros otra sería inventar dos contratos donde el
    ''' motor tiene uno.</para>
    ''' </summary>
    Friend Structure ContextoDeSolve

        ''' <summary>La instancia: `positions` en `+0x18`, `previous` en `+0x28`
        ''' (`0x141A01F3C` / `0x141A01F48`).</summary>
        Friend Instancia As Instancia

        ''' <summary>Los buffers vivos de la **instancia de tela** (el 3.er argumento del motor):
        ''' `[r8+0x30]` en `0x141A01F34`. `Nothing` para los sets que no los usan.</summary>
        Friend Buffers As Buffer()

        ''' <summary>
        ''' `hclClothInstance.transformSets` (`+0x40`) — un arreglo de **conjuntos** de matrices de
        ''' hueso, cada uno con sus matrices de 64 B.
        ''' <para>Citas: `0x14195C412` / `0x14195C515` para el campo;
        ''' `hclBonePlanesConstraintSet` lo usa en `0x1419FCBD6`/`0x1419FCBEA` y elige el conjunto
        ''' con `set.transformSetIndex` (`+0x30`).</para>
        ''' <para>⛔ Es el otro miembro del 3.er argumento de `solve`, que es la **instancia de
        ''' tela**: buffers en `+0x30` y transform sets en `+0x40`. Modelar sólo los buffers dejaba
        ''' a `BonePlanes` sin cómo llegar a sus matrices (motor-66).</para>
        ''' </summary>
        Friend TransformSets As Mat4()()

        ''' <summary>
        ''' El índice `j` del set dentro del `hclClothState` — el **4.º argumento**
        ''' (`0x141A1355A mov r9d, edi`, `0x141A137F6 mov r9d, [rsi+rax]`).
        ''' <para>Hoy no lo lee ninguno de los sets transcritos, pero es parte de la firma y
        ''' `Transition`/`AntiPinch`/`Volume` **lo necesitan**: con él buscan su bloque de estado
        ''' por id en `[inst+0xB0]` / `[inst+0xC0]` (`0x141A08CBE`, `0x1419F81D0`,
        ''' `0x141A0A528`). Va desde ahora para no volver a mover la firma (motor-66).</para>
        ''' </summary>
        Friend IndiceDelSet As Integer

        ''' <summary>
        ''' El **6.º argumento** de `solve` — el `usaK` de <see cref="Tiempo.UsaK"/>
        ''' (`0x141A133E0`), consultado en `0x141A01FCD`, `0x141A02036`, `0x141A02057`,
        ''' `0x141A0206B`, `0x141A020A3` y `0x1419FCB92`.
        ''' <para>⛔ **No es un parámetro libre**: sale de `Tiempo.UsaK(modo, numSubSteps, s1, s2)`
        ''' y el cableado lo tiene que tomar de ahí. Ponerlo a mano sería inventar (motor-66).</para>
        ''' <para>Qué hace: duplica cada kernel. Con él puesto, la corrección se le aplica
        ''' **también** a `previous`, o sea que la restricción mueve la partícula **sin cambiarle
        ''' la velocidad**. MEDIDO: las hojas con este bit tienen exactamente **dos `subps` más** y
        ''' ninguna otra diferencia aritmética, y lo mismo pasa en `hclBonePlanesConstraintSet`
        ''' (`0x1419FCBD0` 95 instrucciones contra `0x1419FCD60` 105) — dos clases independientes.</para>
        ''' </summary>
        Friend UsaK As Boolean

        ''' <summary>Sólo la instancia — para los sets de enlace, que no leen buffers.</summary>
        Friend Sub New(inst As Instancia)
            Me.Instancia = inst
            Me.Buffers = Nothing
            Me.TransformSets = Nothing
            Me.IndiceDelSet = 0
            Me.UsaK = False
        End Sub

        Friend Sub New(inst As Instancia, buffers As Buffer(), usaK As Boolean,
                       Optional indiceDelSet As Integer = 0,
                       Optional transformSets As Mat4()() = Nothing)
            Me.Instancia = inst
            Me.Buffers = buffers
            Me.TransformSets = transformSets
            Me.IndiceDelSet = indiceDelSet
            Me.UsaK = usaK
        End Sub

    End Structure

    ' =============================================================================================

    ''' <summary>
    ''' Un conjunto de restricciones ya **compilado**: los índices y los parámetros sacados del grafo
    ''' una sola vez, en arreglos planos.
    ''' <para>⛔ No se resuelve contra el grafo tipado en cada substep: el motor tiene los enlaces en
    ''' un array contiguo y los recorre con `movzx`/`movss`. Bajar al packfile por cada enlace, cada
    ''' substep, cada iteración, sería otro orden de magnitud de costo — y este motor corre en Debug,
    ''' donde ya se paga bastante.</para>
    ''' </summary>
    Friend MustInherit Class SetCompilado

        ''' <summary>El `type` del `.exe` (censo `Havok.Canon.CensoDeClasesHcl`), **no** el campo del
        ''' archivo: ese está marcado `SERIALIZE_IGNORED` y no se serializa.</summary>
        Friend ReadOnly Tipo As Integer

        ''' <summary>El `name` del objeto, para el log y para los gates.</summary>
        Friend ReadOnly Nombre As String

        Protected Sub New(tipo As Integer, nombre As String)
            Me.Tipo = tipo
            Me.Nombre = nombre
        End Sub

        ''' <summary>Cuántos enlaces/entradas tiene. Un set vacío no se ejecuta.</summary>
        Friend MustOverride ReadOnly Property Cuenta As Integer

        ''' <summary>
        ''' Si el envoltorio corta con `k &lt;= 0`. ⛔ **NO es universal**: cada `solve` es su propia
        ''' función y su propia puerta.
        ''' <para>· Los cuatro enlaces, `BendStiffness`, `LocalRange`, `BonePlanes`: sí
        ''' (`0x141A0609A`, `0x1419F9A4A`, `0x141A01F2B`, `0x1419FCB8D`, todos `comiss`/`jbe`).</para>
        ''' <para>· `hclTransitionConstraintSet`: **NO** — lo sobreescribe. Ver su
        ''' <see cref="Transicion"/>.</para>
        ''' <para>Poner la puerta de los enlaces en la base y no dejar salirse era imponerle a todos
        ''' una ley de algunos; lo cazó `GR11k`.</para>
        ''' </summary>
        Protected Overridable ReadOnly Property CortaConKNoPositivo As Boolean
            Get
                Return True
            End Get
        End Property

        ''' <summary>
        ''' Aplica el set. `k` ya viene difundido en las cuatro lanes, como el `vec4` que el motor
        ''' arma en `0x141A06101`-`0x141A0610E` y pasa por puntero.
        ''' <para>⛔ El envoltorio del motor corta con **`k &lt;= 0`** (`0x141A0609A` `comiss` / `jbe`),
        ''' y ése es el mecanismo por el que `LocalRange` y `BonePlanes` sólo corren en el último
        ''' substep: su `k` es `0,0` en los demás. La guarda vive en <see cref="Aplicar"/>, no en el
        ''' kernel.</para>
        ''' </summary>
        Friend Sub Aplicar(ctx As ContextoDeSolve, k As Single)
            If CortaConKNoPositivo AndAlso Not (k > 0.0F) Then Return   ' 0x141A0609A comiss / jbe
            If Cuenta = 0 Then Return                  ' 0x141A0617A test ecx, ecx / jle
            Kernel(ctx, Vector128.Create(k))           ' 0x141A06101 shufps 0 (k difundido)
        End Sub

        ''' <summary>El kernel, ya con `k` difundido y con la garantía de que hay al menos un enlace.</summary>
        Protected MustOverride Sub Kernel(ctx As ContextoDeSolve, k As Vector128(Of Single))

    End Class

    ' =============================================================================================

    ''' <summary>
    ''' `hclStandardLinkConstraintSet` — kernel `0x141A06170`. **340 de 451** en el corpus.
    ''' <para>Enlace de 12 B: `{particleA:u16@0, particleB:u16@2, restLength:f32@4, stiffness:f32@8}`.</para>
    ''' </summary>
    Friend NotInheritable Class EnlaceEstandar
        Inherits SetCompilado

        Private ReadOnly _a As Integer()
        Private ReadOnly _b As Integer()
        Private ReadOnly _restLength As Single()
        Private ReadOnly _stiffness As Single()
        Private ReadOnly _n As Integer

        ''' <summary>Los dos extremos y la longitud de reposo de un enlace. ⛔ Los publica para
        ''' que `Cobertura` pueda medir los `restLength` contra el `DefaultClothPose`, que es de donde
        ''' el autor los saco: una ley del DATO con valor esperado CERO.</summary>
        Friend Function ParticulaA(k As Integer) As Integer
            Return _a(k)
        End Function

        Friend Function ParticulaB(k As Integer) As Integer
            Return _b(k)
        End Function

        Friend Function LongitudDeReposo(k As Integer) As Single
            Return _restLength(k)
        End Function

        Friend Sub New(src As HkObj_HclStandardLinkConstraintSet, tipo As Integer)
            MyBase.New(tipo, src.Name)
            Dim ls = src.Links
            Dim n = If(ls Is Nothing, 0, ls.Count)
            ReDim _a(Math.Max(1, n) - 1) : ReDim _b(_a.Length - 1)
            ReDim _restLength(_a.Length - 1) : ReDim _stiffness(_a.Length - 1)
            For i = 0 To n - 1
                _a(i) = ls(i).ParticleA : _b(i) = ls(i).ParticleB
                _restLength(i) = ls(i).RestLength : _stiffness(i) = ls(i).Stiffness
            Next
            _n = n
        End Sub

        ''' <summary>Un solo enlace, sin archivo: para los gates de la ley.</summary>
        Friend Sub New(a As Integer, b As Integer, restLength As Single, stiffness As Single)
            MyBase.New(1, "prueba")
            _a = {a} : _b = {b} : _restLength = {restLength} : _stiffness = {stiffness}
            _n = 1
        End Sub

        Friend Overrides ReadOnly Property Cuenta As Integer
            Get
                Return _n
            End Get
        End Property

        ''' <summary>
        ''' ```
        ''' d      = P[B] − P[A]
        ''' len2   = dot3(d, d)
        ''' invLen = (len2 &lt;= 0) ? 0 : rsqrtps(len2)          ' CRUDO, sin Newton (0x141A061E7)
        ''' c      = (((len2·invLen − restLength) · stiffness) · k) · (d·invLen)
        ''' P[A] += invMass[A]·c   ;   P[B] −= invMass[B]·c
        ''' ```
        ''' <para>⛔ `len2·invLen` **no** es `|d|` exacto: es `|d|` con el error de `rsqrtps` crudo,
        ''' y el motor lo usa así. Calcular `|d|` con una raíz exacta es «arreglarlo» y cambia la
        ''' tela.</para>
        ''' </summary>
        Protected Overrides Sub Kernel(ctx As ContextoDeSolve, k As Vector128(Of Single))
            Dim inst = ctx.Instancia
            Dim pos = inst.Posiciones
            Dim invM = inst.InvMasa
            For i = 0 To _n - 1
                Dim ia = _a(i), ib = _b(i)
                Dim d = Vector128.Subtract(Simd.Leer(pos, ib), Simd.Leer(pos, ia))   ' 0x141A061B5/BA
                Dim len2 = Simd.Dot3(d, d)                                           ' 0x141A061C5…DA
                Dim invLen = Simd.RsqrtConGuarda(len2)                               ' 0x141A061E7…F1
                Dim c = Vector128.Subtract(Vector128.Multiply(len2, invLen),
                                           Vector128.Create(_restLength(i)))         ' 0x141A06201/07
                c = Vector128.Multiply(c, Vector128.Create(_stiffness(i)))           ' 0x141A06215
                c = Vector128.Multiply(c, k)                                         ' 0x141A06223
                c = Vector128.Multiply(c, Vector128.Multiply(invLen, d))             ' 0x141A06204/26
                ' ⛔ P[A] se escribe ANTES de leer P[B]: Gauss-Seidel, y con A == B importa.
                Simd.Escribir(pos, ia, Vector128.Add(Simd.Leer(pos, ia),
                                                     Vector128.Multiply(Vector128.Create(invM(ia)), c)))
                Simd.Escribir(pos, ib, Vector128.Subtract(Simd.Leer(pos, ib),
                                                          Vector128.Multiply(Vector128.Create(invM(ib)), c)))
            Next
        End Sub

    End Class

    ' =============================================================================================

    ''' <summary>
    ''' `hclStretchLinkConstraintSet` — kernel `0x141A06DB0`. **335 de 451** en el corpus.
    ''' <para>Mismo enlace de 12 B que el estándar, **otra ley**.</para>
    ''' </summary>
    Friend NotInheritable Class EnlaceDeEstiramiento
        Inherits SetCompilado

        Private ReadOnly _a As Integer()
        Private ReadOnly _b As Integer()
        Private ReadOnly _restLength As Single()
        Private ReadOnly _stiffness As Single()
        Private ReadOnly _n As Integer

        ''' <summary>Un solo enlace, sin archivo: para los gates de la ley.</summary>
        Friend Sub New(a As Integer, b As Integer, restLength As Single, stiffness As Single)
            MyBase.New(2, "prueba")
            _a = {a} : _b = {b} : _restLength = {restLength} : _stiffness = {stiffness}
            _n = 1
        End Sub

        Friend Sub New(src As HkObj_HclStretchLinkConstraintSet, tipo As Integer)
            MyBase.New(tipo, src.Name)
            Dim ls = src.Links
            _n = If(ls Is Nothing, 0, ls.Count)
            ReDim _a(Math.Max(1, _n) - 1) : ReDim _b(_a.Length - 1)
            ReDim _restLength(_a.Length - 1) : ReDim _stiffness(_a.Length - 1)
            For i = 0 To _n - 1
                _a(i) = ls(i).ParticleA : _b(i) = ls(i).ParticleB
                _restLength(i) = ls(i).RestLength : _stiffness(i) = ls(i).Stiffness
            Next
        End Sub

        Friend Overrides ReadOnly Property Cuenta As Integer
            Get
                Return _n
            End Get
        End Property

        ''' <summary>
        ''' ```
        ''' c = min(restLength − |d|, 0) · stiffness · k · d̂
        ''' P[B] += c                     ' ⬅ SÓLO B, y SIN invMass
        ''' ```
        ''' <para>⛔⛔ Las dos rarezas están medidas: `0x141A06E53` suma sobre `[rdx + rcx*8]` con
        ''' `rcx = 2·B`, y en toda la función **no hay ninguna lectura de `particleDatas`**. El
        ''' `minps` contra cero (`0x141A06E46`) lo hace unilateral: sólo tira cuando el enlace está
        ''' estirado, nunca empuja.</para>
        ''' </summary>
        Protected Overrides Sub Kernel(ctx As ContextoDeSolve, k As Vector128(Of Single))
            Dim inst = ctx.Instancia
            Dim pos = inst.Posiciones
            Dim cero = Vector128(Of Single).Zero
            For i = 0 To _n - 1
                Dim ia = _a(i), ib = _b(i)
                Dim d = Vector128.Subtract(Simd.Leer(pos, ib), Simd.Leer(pos, ia))   ' 0x141A06DF0/F4
                Dim len2 = Simd.Dot3(d, d)                                           ' 0x141A06DF8…E13
                Dim invLen = Simd.RsqrtConGuarda(len2)                               ' 0x141A06E1F…29
                Dim c = Vector128.Subtract(Vector128.Create(_restLength(i)),
                                           Vector128.Multiply(invLen, len2))         ' 0x141A06E32/35
                c = Vector128.Min(c, cero)                                           ' 0x141A06E46 minps
                c = Vector128.Multiply(c, Vector128.Create(_stiffness(i)))           ' 0x141A06E49
                c = Vector128.Multiply(c, k)                                         ' 0x141A06E4C
                c = Vector128.Multiply(c, Vector128.Multiply(invLen, d))             ' 0x141A06E2F/50
                Simd.Escribir(pos, ib, Vector128.Add(Simd.Leer(pos, ib), c))         ' 0x141A06E53/57
            Next
        End Sub

    End Class

    ' =============================================================================================

    ''' <summary>
    ''' `hclBendLinkConstraintSet` — kernel `0x1419F8F70`. **2 de 451** en el corpus.
    ''' <para>Enlace de 20 B: `{particleA:u16@0, particleB:u16@2, bendMinLength:f32@4,
    ''' stretchMaxLength:f32@8, bendStiffness:f32@0xC, stretchStiffness:f32@0x10}`.</para>
    ''' </summary>
    Friend NotInheritable Class EnlaceDeDoblez
        Inherits SetCompilado

        Private ReadOnly _a As Integer()
        Private ReadOnly _b As Integer()
        Private ReadOnly _bendMin As Single()
        Private ReadOnly _stretchMax As Single()
        Private ReadOnly _bendK As Single()
        Private ReadOnly _stretchK As Single()
        Private ReadOnly _n As Integer

        ''' <summary>Un solo enlace, sin archivo: para los gates de la ley.</summary>
        Friend Sub New(a As Integer, b As Integer, bendMin As Single, stretchMax As Single,
                       bendK As Single, stretchK As Single)
            MyBase.New(3, "prueba")
            _a = {a} : _b = {b}
            _bendMin = {bendMin} : _stretchMax = {stretchMax}
            _bendK = {bendK} : _stretchK = {stretchK}
            _n = 1
        End Sub

        Friend Sub New(src As HkObj_HclBendLinkConstraintSet, tipo As Integer)
            MyBase.New(tipo, src.Name)
            Dim ls = src.Links
            _n = If(ls Is Nothing, 0, ls.Count)
            ReDim _a(Math.Max(1, _n) - 1) : ReDim _b(_a.Length - 1)
            ReDim _bendMin(_a.Length - 1) : ReDim _stretchMax(_a.Length - 1)
            ReDim _bendK(_a.Length - 1) : ReDim _stretchK(_a.Length - 1)
            For i = 0 To _n - 1
                _a(i) = ls(i).ParticleA : _b(i) = ls(i).ParticleB
                _bendMin(i) = ls(i).BendMinLength : _stretchMax(i) = ls(i).StretchMaxLength
                _bendK(i) = ls(i).BendStiffness : _stretchK(i) = ls(i).StretchStiffness
            Next
        End Sub

        Friend Overrides ReadOnly Property Cuenta As Integer
            Get
                Return _n
            End Get
        End Property

        ''' <summary>
        ''' ```
        ''' c = [ max(0, |d| − stretchMaxLength)·stretchStiffness
        '''     − max(0, bendMinLength − |d|)·bendStiffness ] · k · d̂
        ''' P[A] += invMass[A]·c   ;   P[B] −= invMass[B]·c
        ''' ```
        ''' <para>⛔ El orden de la resta es `estiramiento − doblez` (`0x1419F90C5` `subps xmm3, xmm2`),
        ''' y los dos `maxps` arrancan de un **cero cargado de memoria** (`0x142F3C550`), no de un
        ''' `xorps`: da lo mismo, pero deja claro que el cero es el segundo operando.</para>
        ''' <para>⭐ `invMass` se difunde con `andps` + dos `orps` (`0x1419F903B`-`0x1419F908B`) en vez
        ''' de `movss` + `shufps`: es un broadcast sin rama, y el resultado es idéntico.</para>
        ''' </summary>
        Protected Overrides Sub Kernel(ctx As ContextoDeSolve, k As Vector128(Of Single))
            Dim inst = ctx.Instancia
            Dim pos = inst.Posiciones
            Dim invM = inst.InvMasa
            Dim cero = Vector128(Of Single).Zero
            For i = 0 To _n - 1
                Dim ia = _a(i), ib = _b(i)
                Dim d = Vector128.Subtract(Simd.Leer(pos, ib), Simd.Leer(pos, ia))   ' 0x1419F8FF6/FB
                Dim len2 = Simd.Dot3(d, d)                                           ' 0x1419F9000…1D
                Dim invLen = Simd.RsqrtConGuarda(len2)                               ' 0x1419F9020…2B
                Dim largo = Vector128.Multiply(invLen, len2)                         ' 0x1419F9045
                Dim dHat = Vector128.Multiply(invLen, d)                             ' 0x1419F9037

                Dim est = Vector128.Max(cero,
                              Vector128.Subtract(largo, Vector128.Create(_stretchMax(i))))  ' 0x1419F9091/9E
                est = Vector128.Multiply(est, Vector128.Create(_stretchK(i)))               ' 0x1419F90A7
                Dim dob = Vector128.Max(cero,
                              Vector128.Subtract(Vector128.Create(_bendMin(i)), largo))     ' 0x1419F90BC/BF
                dob = Vector128.Multiply(dob, Vector128.Create(_bendK(i)))                  ' 0x1419F90C2

                Dim c = Vector128.Subtract(est, dob)                                  ' 0x1419F90C5
                c = Vector128.Multiply(c, k)                                          ' 0x1419F90C8
                c = Vector128.Multiply(c, dHat)                                       ' 0x1419F90CB

                Simd.Escribir(pos, ia, Vector128.Add(Simd.Leer(pos, ia),
                                                     Vector128.Multiply(c, Vector128.Create(invM(ia)))))
                Simd.Escribir(pos, ib, Vector128.Subtract(Simd.Leer(pos, ib),
                                                          Vector128.Multiply(c, Vector128.Create(invM(ib)))))
            Next
        End Sub

    End Class

    ' =============================================================================================

    ''' <summary>
    ''' `hclCompressibleLinkConstraintSet` — kernel `0x1419FE850`. **2 de 451** en el corpus.
    ''' <para>Enlace de 16 B: `{particleA:u16@0, particleB:u16@2, restLength:f32@4,
    ''' compressionLength:f32@8, stiffness:f32@0xC}`.</para>
    ''' </summary>
    Friend NotInheritable Class EnlaceCompresible
        Inherits SetCompilado

        Private ReadOnly _a As Integer()
        Private ReadOnly _b As Integer()
        Private ReadOnly _rest As Single()
        Private ReadOnly _compresion As Single()
        Private ReadOnly _stiffness As Single()
        Private ReadOnly _n As Integer

        ''' <summary>Un solo enlace, sin archivo: para los gates de la ley.</summary>
        Friend Sub New(a As Integer, b As Integer, restLength As Single, compresion As Single,
                       stiffness As Single)
            MyBase.New(20, "prueba")
            _a = {a} : _b = {b}
            _rest = {restLength} : _compresion = {compresion} : _stiffness = {stiffness}
            _n = 1
        End Sub

        Friend Sub New(src As HkObj_HclCompressibleLinkConstraintSet, tipo As Integer)
            MyBase.New(tipo, src.Name)
            Dim ls = src.Links
            _n = If(ls Is Nothing, 0, ls.Count)
            ReDim _a(Math.Max(1, _n) - 1) : ReDim _b(_a.Length - 1)
            ReDim _rest(_a.Length - 1) : ReDim _compresion(_a.Length - 1)
            ReDim _stiffness(_a.Length - 1)
            For i = 0 To _n - 1
                _a(i) = ls(i).ParticleA : _b(i) = ls(i).ParticleB
                _rest(i) = ls(i).RestLength : _compresion(i) = ls(i).CompressionLength
                _stiffness(i) = ls(i).Stiffness
            Next
        End Sub

        Friend Overrides ReadOnly Property Cuenta As Integer
            Get
                Return _n
            End Get
        End Property

        ''' <summary>
        ''' Una **banda muerta** entre las dos longitudes:
        ''' ```
        ''' comprimido = (compressionLength &gt; |d|)      ' 0x1419FE90C ucomiss + seta
        ''' estirado   = (|d| &gt; restLength)             ' 0x1419FE917 ucomiss + ja
        '''
        '''  estirado y  comprimido -> objetivo = compressionLength     ' 0x1419FE923 → 0x1419FE928
        '''  estirado y !comprimido -> objetivo = restLength            ' 0x1419FE923 → 0x1419FE92B
        ''' !estirado y  comprimido -> objetivo = compressionLength     ' 0x1419FE91C → 0x1419FE928
        ''' !estirado y !comprimido -> NO HACE NADA                     ' 0x1419FE91F → 0x1419FE954
        '''
        ''' c = (|d| − objetivo) · stiffness · k · d̂
        ''' P[A] += invMass[A]·c   ;   P[B] −= invMass[B]·c
        ''' ```
        ''' <para>⚠️ **El cap. 6.5 del RE se comía el primer caso.** `estirado y comprimido` sólo puede
        ''' pasar si `compressionLength &gt; restLength`, y ahí el objetivo es `compressionLength`. Está
        ''' en el binario (`0x1419FE923` `test r8b` / `jne` cae en `0x1419FE928`, que es
        ''' `movaps xmm0, xmm1` = la longitud de compresión), y acá se implementa.</para>
        ''' </summary>
        Protected Overrides Sub Kernel(ctx As ContextoDeSolve, k As Vector128(Of Single))
            Dim inst = ctx.Instancia
            Dim pos = inst.Posiciones
            Dim invM = inst.InvMasa
            For i = 0 To _n - 1
                Dim ia = _a(i), ib = _b(i)
                Dim pa = Simd.Leer(pos, ia)
                Dim d = Vector128.Subtract(Simd.Leer(pos, ib), pa)                   ' 0x1419FE8A9/AE/B8
                Dim len2 = Simd.Dot3(d, d)                                           ' 0x1419FE8CA…E5
                Dim invLen = Simd.RsqrtConGuarda(len2)                               ' 0x1419FE8E8…F3
                Dim largo = Simd.Lane0(Vector128.Multiply(len2, invLen))             ' 0x1419FE8FC
                Dim dHat = Vector128.Multiply(d, invLen)                             ' 0x1419FE8FF

                Dim rest = _rest(i), comp = _compresion(i)
                Dim comprimido = comp > largo                                        ' 0x1419FE90C seta
                Dim estirado = largo > rest                                          ' 0x1419FE917 ja
                If Not estirado AndAlso Not comprimido Then Continue For             ' 0x1419FE91F je
                Dim objetivo = If(comprimido, comp, rest)                            ' 0x1419FE928 / 92B

                Dim c = Vector128.Create(largo - objetivo)                           ' 0x1419FE92B subps
                c = Vector128.Multiply(c, Vector128.Create(_stiffness(i)))           ' 0x1419FE92E
                c = Vector128.Multiply(c, k)                                         ' 0x1419FE931
                c = Vector128.Multiply(c, dHat)                                      ' 0x1419FE934

                Simd.Escribir(pos, ia, Vector128.Add(pa, Vector128.Multiply(c, Vector128.Create(invM(ia)))))
                Simd.Escribir(pos, ib, Vector128.Subtract(Simd.Leer(pos, ib),
                                                          Vector128.Multiply(c, Vector128.Create(invM(ib)))))
            Next
        End Sub

    End Class

    ' =============================================================================================

    ''' <summary>
    ''' `hclBendStiffnessConstraintSet` — envoltorio `0x1419F9A30` (`"TtSolve Bend Stiffness"`,
    ''' `0x142717A60`). **721 de 759** en el corpus, y las 721 con `useRestPoseConfig = 1`.
    ''' <para>Enlace de `0x20` B, y los diez campos salen de la reflexión, no de la lectura:
    ''' `weightA..D` en `+0x00..+0x0C`, `bendStiffness` en `+0x10`, `restCurvature` en `+0x14`,
    ''' `particleA..D` en `+0x18..+0x1E`.</para>
    ''' <para>⛔ **Dos ramas, elegidas por `useRestPoseConfig` (+0x30, un byte)**:
    ''' `0x1419F9B50` cuando es 0 y `0x1419F9CF0` cuando no (`0x1419F9AA5` `cmp byte` +
    ''' `0x1419F9ACE`/`0x1419F9AD5`).</para>
    ''' <para>⭐ La simple es **exactamente** la de reposo con `restCurvature = 0`: la rama de
    ''' reposo suma `v + (…·restCurvature)`, que se anula. Ése es el control G11 y no depende de mi
    ''' lectura — lo mide GR8f.</para>
    ''' </summary>
    Friend NotInheritable Class RigidezDeDoblez
        Inherits SetCompilado

        Private ReadOnly _a As Integer(), _b As Integer(), _c As Integer(), _d As Integer()
        Private ReadOnly _wA As Single(), _wB As Single(), _wC As Single(), _wD As Single()
        Private ReadOnly _bend As Single(), _restCurvatura As Single()
        Private ReadOnly _usaReposo As Boolean
        Private ReadOnly _n As Integer

        Friend Sub New(src As HkObj_HclBendStiffnessConstraintSet, tipo As Integer)
            MyBase.New(tipo, src.Name)
            Dim ls = src.Links
            Dim n = If(ls Is Nothing, 0, ls.Count)
            Dim m = Math.Max(1, n)
            ReDim _a(m - 1) : ReDim _b(m - 1) : ReDim _c(m - 1) : ReDim _d(m - 1)
            ReDim _wA(m - 1) : ReDim _wB(m - 1) : ReDim _wC(m - 1) : ReDim _wD(m - 1)
            ReDim _bend(m - 1) : ReDim _restCurvatura(m - 1)
            For i = 0 To n - 1
                Dim l = ls(i)
                _a(i) = l.ParticleA : _b(i) = l.ParticleB
                _c(i) = l.ParticleC : _d(i) = l.ParticleD
                _wA(i) = l.WeightA : _wB(i) = l.WeightB
                _wC(i) = l.WeightC : _wD(i) = l.WeightD
                _bend(i) = l.BendStiffness : _restCurvatura(i) = l.RestCurvature
            Next
            _usaReposo = src.UseRestPoseConfig
            _n = n
        End Sub

        ''' <summary>Un solo enlace, sin archivo: para los gates de la ley.</summary>
        Friend Sub New(a As Integer, b As Integer, c As Integer, d As Integer,
                       wA As Single, wB As Single, wC As Single, wD As Single,
                       bendStiffness As Single, restCurvature As Single, usaReposo As Boolean)
            MyBase.New(5, "prueba")
            _a = {a} : _b = {b} : _c = {c} : _d = {d}
            _wA = {wA} : _wB = {wB} : _wC = {wC} : _wD = {wD}
            _bend = {bendStiffness} : _restCurvatura = {restCurvature}
            _usaReposo = usaReposo
            _n = 1
        End Sub

        Friend Overrides ReadOnly Property Cuenta As Integer
            Get
                Return _n
            End Get
        End Property

        ''' <summary>
        ''' ```
        ''' kb  = bcast(bendStiffness) · k                       ' 0x1419F9BFE/C03, UNA vez por enlace
        ''' v   = ((wA·P[A] + wB·P[B]) + wC·P[C]) + wD·P[D]
        ''' si useRestPoseConfig:
        '''     a = P[A]−P[C] ; b = P[B]−P[C] ; d = P[D]−P[C]    ' 0x1419F9D84…D8A
        '''     n1 = cross(d, a) ; n2 = cross(b, d)              ' 0x1419F9D90…DBE
        '''     ŝ  = normalizar( n̂2 + n̂1 )                     ' rsqrtps CRUDO ×3
        '''     w   = v + ((((|n2|·|n1|)·invD2)·ŝ)·restCurvature)
        ''' si no:
        '''     w   = v
        ''' P[i] += invMass[i] · (kb · w_i) · w                   ' i ∈ {A,B,C,D}
        ''' ```
        ''' <para>⛔ Las tres normalizaciones son `rsqrtps` **CRUDO** con guarda
        ''' (`0x1419F9DE7`-`DFD`, `0x1419F9E14`-`E21`, `0x1419F9EA8`/`EB9`), y `invD2` es `rcpps`
        ''' **con** una Newton (`0x1419F9E73`-`E9E`). No son la misma clase: refinar la primera o
        ''' no refinar la segunda es «arreglar» el motor.</para>
        ''' <para>⛔ El producto de la corrección de curvatura **no es asociativo**: va
        ''' `((|n2|·|n1|)·invD2)·ŝ)·restCurvature`, en ese agrupamiento
        ''' (`0x1419F9EFC`/`F00`/`F0E`/`F1B` + `0x1419F9F49`).</para>
        ''' <para>⚠️ El orden de la suma de `v` difiere entre las dos ramas del motor —`(A+B)+C+D`
        ''' en la simple, `(B+A)+C+D` en la de reposo— y el de los productos del writeback también.
        ''' Son intercambios de los DOS operandos de `+` y `·`, que en IEEE-754 son exactos ⇒ bit a
        ''' bit lo mismo. Queda una sola forma acá a propósito.</para>
        ''' </summary>
        Protected Overrides Sub Kernel(ctx As ContextoDeSolve, k As Vector128(Of Single))
            Dim inst = ctx.Instancia
            Dim pos = inst.Posiciones
            Dim invM = inst.InvMasa
            For i = 0 To _n - 1
                Dim ia = _a(i), ib = _b(i), ic = _c(i), id = _d(i)
                Dim pA = Simd.Leer(pos, ia), pB = Simd.Leer(pos, ib)
                Dim pC = Simd.Leer(pos, ic), pD = Simd.Leer(pos, id)
                Dim wA = Vector128.Create(_wA(i)), wB = Vector128.Create(_wB(i))
                Dim wC = Vector128.Create(_wC(i)), wD = Vector128.Create(_wD(i))

                Dim kb = Vector128.Multiply(Vector128.Create(_bend(i)), k)   ' 0x1419F9BFE/C03

                ' ⛔ EL NUCLEO ES COMPARTIDO con `RigidezDeDoblezMx`: dos copias de esta
                ' aritmetica —dos cruces y tres `rsqrt` CRUDOS— podrian divergir sin que nada lo
                ' viera, y el RE avisa de que la equivalencia hay que exigirla, no suponerla.
                Dim w = VectorDeDoblez(pA, pB, pC, pD, wA, wB, wC, wD, _usaReposo, _restCurvatura(i))

                Escribir4(pos, invM, ia, kb, wA, w)
                Escribir4(pos, invM, ib, kb, wB, w)
                Escribir4(pos, invM, ic, kb, wC, w)
                Escribir4(pos, invM, id, kb, wD, w)
            Next
        End Sub

        ''' <summary>`P[i] += invMass[i] · (kb · peso) · w` — `0x1419F9C5B`/`C65`/`C6C`.</summary>
        Private Shared Sub Escribir4(pos As Single(), invM As Single(), i As Integer,
                                     kb As Vector128(Of Single), peso As Vector128(Of Single),
                                     w As Vector128(Of Single))
            Escribir4ConMasa(pos, i, invM(i), kb, peso, w)
        End Sub

        ''' <summary>La escritura de una de las cuatro particulas, con la masa inversa DADA.
        ''' <para>⛔ La comparte `hclBendStiffnessConstraintSetMx`, que trae sus `invMass` en el
        ''' registro en vez de leerlas de la instancia. Compartir el cuerpo hace que las dos no
        ''' puedan divergir — que es lo que el RE pide exigir y no suponer.</para></summary>
        Friend Shared Sub Escribir4ConMasa(pos As Single(), i As Integer, invMasa As Single,
                                           kb As Vector128(Of Single), peso As Vector128(Of Single),
                                           w As Vector128(Of Single))
            Dim f = Vector128.Multiply(Vector128.Create(invMasa), Vector128.Multiply(kb, peso))
            Simd.Escribir(pos, i, Vector128.Add(Simd.Leer(pos, i), Vector128.Multiply(f, w)))
        End Sub

        ''' <summary>
        ''' ⭐ El nucleo de la ley: el vector de correccion `w` a partir de los cuatro pesos y, si el
        ''' set lo pide, el termino de la CURVATURA DE REPOSO.
        ''' <para>Lo comparten `RigidezDeDoblez` y su `Mx`: son dos empaquetados del mismo dato, y con
        ''' dos copias de esta aritmetica —dos cruces y tres `rsqrt` CRUDOS— la equivalencia seria una
        ''' suposicion en vez de una propiedad.</para>
        ''' </summary>
        Friend Shared Function VectorDeDoblez(pA As Vector128(Of Single), pB As Vector128(Of Single),
                                              pC As Vector128(Of Single), pD As Vector128(Of Single),
                                              wA As Vector128(Of Single), wB As Vector128(Of Single),
                                              wC As Vector128(Of Single), wD As Vector128(Of Single),
                                              usaReposo As Boolean, restCurvatura As Single) As Vector128(Of Single)
            Dim v = Vector128.Add(Vector128.Multiply(wA, pA), Vector128.Multiply(wB, pB))
            v = Vector128.Add(v, Vector128.Multiply(wC, pC))             ' 0x1419F9C42
            v = Vector128.Add(v, Vector128.Multiply(wD, pD))             ' 0x1419F9C4D
            If Not usaReposo Then Return v

            Dim va = Vector128.Subtract(pA, pC)                          ' 0x1419F9D84
            Dim vb = Vector128.Subtract(pB, pC)
            Dim vd = Vector128.Subtract(pD, pC)                          ' 0x1419F9D8A
            Dim n1 = Polar.Cruz(vd, va)                                  ' 0x1419F9D90…DA1
            Dim n2 = Polar.Cruz(vb, vd)                                  ' 0x1419F9DA7…DBE
            Dim l1 = Simd.Dot3(n1, n1), l2 = Simd.Dot3(n2, n2)
            Dim inv1 = Simd.RsqrtConGuarda(l1)                           ' CRUDO, 0x1419F9DE7…DFD
            Dim inv2 = Simd.RsqrtConGuarda(l2)                           ' CRUDO, 0x1419F9E14…E21
            Dim sv = Vector128.Add(Vector128.Multiply(inv2, n2), Vector128.Multiply(inv1, n1))
            Dim invS = Simd.RsqrtConGuarda(Simd.Dot3(sv, sv))            ' CRUDO, 0x1419F9EA8/B9
            Dim sHat = Vector128.Multiply(invS, sv)                      ' 0x1419F9EC2
            Dim invD2 = Simd.RcpNewton(Simd.Dot3(vd, vd))
            Dim len1 = Vector128.Multiply(inv1, l1)                      ' 0x1419F9EEF
            Dim len2b = Vector128.Multiply(inv2, l2)                     ' 0x1419F9E9A
            Dim t = Vector128.Multiply(len2b, len1)
            t = Vector128.Multiply(t, invD2)
            t = Vector128.Multiply(t, sHat)
            t = Vector128.Multiply(t, Vector128.Create(restCurvatura))
            Return Vector128.Add(v, t)                                   ' 0x1419F9F49
        End Function

    End Class

    ' =============================================================================================

    ''' <summary>
    ''' `hclLocalRangeConstraintSet` — despacho `0x141A01F10` (`"TtLocal Range Constraints"`,
    ''' `0x142718800`). **583** en el corpus.
    ''' <para>⭐⭐ El motor tiene **doce** kernels y cada uno vuelve a despachar por `shapeType`:
    ''' veinte hojas. **Están MEDIDAS, no supuestas**: el multiconjunto de instrucciones
    ''' aritméticas SIMD de las veinte se agrupa en **seis**, y los dos ejes que no cambian ni una
    ''' operación son `bufferReal[+0x20] &amp; 1` (layout de posiciones) y `bufferReal[+0x48] &amp; 1`
    ''' (layout de normales) — o sea, puro **direccionamiento**, que acá lo absorbe el stride de
    ''' <see cref="Buffer.Vertice"/>. Lo que sí cambia la cuenta es `shapeType` (+1 `subps`) y
    ''' `UsaK` (+2 `subps`).</para>
    ''' <para>⛔ El buffer se resuelve con la **doble indirección** `buffers[buffers[idx].Ranura]`
    ''' (`0x141A01F5A` → `+0x100` → `0x141A01F65`), no con el índice directo.</para>
    ''' <para>⛔ `set.stiffness` (+0x34) **no lo lee el despacho** (cero ocurrencias de
    ''' `[rbx+0x34]`): el factor llega por `k`, como en los enlaces.</para>
    ''' </summary>
    Friend NotInheritable Class RangoLocal
        Inherits SetCompilado

        Private ReadOnly _particula As Integer(), _refVertice As Integer()
        Private ReadOnly _maxDist As Single()
        Private ReadOnly _maxNormalDist As Single(), _minNormalDist As Single()
        Private ReadOnly _bufferIdx As Integer
        Private ReadOnly _aplicaNormal As Boolean
        ''' <summary>
        ''' `shapeType` (+0x38) **tal cual viene**, porque tiene TRES casos, no dos:
        ''' <para>· `0` ⇒ mide contra el **punto** de referencia (`0x141A0273F` `test ecx,ecx` /
        ''' `je` → la hoja `0x141A034E0`);</para>
        ''' <para>· `1` ⇒ mide contra la **línea** que pasa por el punto en la dirección de la
        ''' normal (`0x141A02741` `cmp ecx,1` → la hoja `0x141A04280`);</para>
        ''' <para>· ⛔ **cualquier otro valor ⇒ el set NO CORRE**: el `jne 0x141A027BC` salta al
        ''' epílogo sin tocar una partícula. No es «se trata como punto» (motor-62).</para>
        ''' <para>⚠️ El corpus mide `shapeType = 0` en las **583**, así que ninguna de las otras dos
        ''' ramas se ejerce hoy. Van igual.</para>
        ''' </summary>
        Private ReadOnly _shapeType As Integer

        ''' <summary>
        ''' `stiffness` (+0x34). ⛔⛔ **NO es un campo muerto.** El despacho `0x141A01F10` no lo
        ''' lee —de ahí venía el error—, pero los **doce kernels intermedios** sí: hacen
        ''' `mulss xmm1, [rcx+0x34]` sobre el `k` que reciben y le pasan a la hoja el vector YA
        ''' escalado (`0x141A0216A`, `0x141A0272B`, `0x141A0280B`, después `shufps 0` y
        ''' `lea rax, [rsp+0x30]`).
        ''' <para>⛔ Y pesa: el corpus lo mide en `[0,5 ; 1]`, con **502 de 583** estrictamente
        ''' menores que 1. Sin este factor la tela se ata a la piel con el doble de fuerza
        ''' (motor-63).</para>
        ''' </summary>
        Private ReadOnly _stiffness As Single
        Private ReadOnly _n As Integer

        Friend Sub New(src As HkObj_HclLocalRangeConstraintSet, tipo As Integer)
            MyBase.New(tipo, src.Name)
            Dim cs = src.LocalConstraints
            Dim n = If(cs Is Nothing, 0, cs.Count)
            Dim m = Math.Max(1, n)
            ReDim _particula(m - 1) : ReDim _refVertice(m - 1) : ReDim _maxDist(m - 1)
            ReDim _maxNormalDist(m - 1) : ReDim _minNormalDist(m - 1)
            For i = 0 To n - 1
                Dim c = cs(i)
                _particula(i) = c.ParticleIndex : _refVertice(i) = c.ReferenceVertex
                _maxDist(i) = c.MaximumDistance
                _maxNormalDist(i) = c.MaxNormalDistance : _minNormalDist(i) = c.MinNormalDistance
            Next
            _bufferIdx = CInt(src.ReferenceMeshBufferIdx)
            _aplicaNormal = src.ApplyNormalComponent
            _shapeType = src.ShapeType
            _stiffness = src.Stiffness
            _n = n
        End Sub

        ''' <summary>Una sola restricción, sin archivo: para los gates de la ley.</summary>
        Friend Sub New(particula As Integer, refVertice As Integer, maxDist As Single,
                       maxNormalDist As Single, minNormalDist As Single,
                       bufferIdx As Integer, aplicaNormal As Boolean, shapeType As Integer,
                       stiffness As Single)
            MyBase.New(6, "prueba")
            _particula = {particula} : _refVertice = {refVertice} : _maxDist = {maxDist}
            _maxNormalDist = {maxNormalDist} : _minNormalDist = {minNormalDist}
            _bufferIdx = bufferIdx
            _aplicaNormal = aplicaNormal
            _shapeType = shapeType
            _stiffness = stiffness
            _n = 1
        End Sub

        Friend Overrides ReadOnly Property Cuenta As Integer
            Get
                Return _n
            End Get
        End Property

        ''' <summary>
        ''' ```
        ''' k'   = k · set.stiffness(+0x34)                                ' 0x141A0216A, EN LOS DOCE INTERMEDIOS
        ''' q    = TransformarPunto( buf.Vertice(refVertice), buf.AEspacioDeSimulacion )
        ''' n    = TransformarDireccion( buf.Normal(refVertice), … )       ' sólo si aplicaNormal
        '''
        ''' shapeType 0 (contra el PUNTO, hoja 0x141A034E0):
        '''     d  = (P − q) + FLT_EPSILON                                 ' 0x141A02AE4
        '''     t  = dot3(n, d̂) · (cMax + |d|)                            ' 0x141A02F18…F40
        ''' shapeType 1 (contra la LÍNEA (q,n), hoja 0x141A04280):
        '''     t  = dot3(P − q, n)                                        ' 0x141A03C83…CA7  ⬅ OTRO `t`
        '''     d  = (P − (q + t·n)) + FLT_EPSILON                         ' 0x141A03CAD/CB1/CB4/CB7
        '''
        ''' inv  = (|d|² &lt;= 0) ? 0 : rsqrtps(|d|²)                        ' CRUDO, 0x141A02B0C
        ''' cMax = min( (maximumDistance − |d|) · k' , 0 )                 ' 0x141A02B32 minps
        ''' P   += cMax · d̂
        ''' si aplicaNormal:
        '''     P −= min(t − minNormalDistance, 0) · n                     ' 0x141A02F8B / 0x141A03D4C
        '''     P += min(maxNormalDistance − t, 0) · n                     ' 0x141A02F8E / 0x141A03D4F
        ''' si usaK:  Prev = Pnuevo − (Pviejo − Prev)
        ''' ```
        ''' <para>⛔⛔ **LAS DOS FORMAS NO USAN EL MISMO `t`, y ése fue el peor error de la
        ''' transcripción (motor-64).** La hoja de la línea ya calculó `dot3(P − q, n)` para
        ''' proyectar, y **reusa ese escalar** para los dos topes normales: `0x141A03D0B`
        ''' `movaps xmm1, xmm7` y `0x141A03D27` `subps xmm0, xmm7`, donde `xmm7` es la proyección
        ''' de `0x141A03C98`-`0x141A03CA7`. En esa hoja **no hay ningún `dot3(n, d̂)`**.
        ''' Y no es un ulp: con la línea, `d ⊥ n` por construcción, así que el `t` de la otra forma
        ''' da ≈0 y **los dos topes normales quedan muertos**.</para>
        ''' <para>⛔⛔ **`k` viene multiplicado por `set.stiffness`** antes de llegar al kernel
        ''' (`mulss xmm1, [rcx+0x34]` en los doce intermedios, `0x141A0216A`/`0x141A0272B`/
        ''' `0x141A0280B`). El corpus lo mide en `[0,5 ; 1]` con **502 de 583** por debajo de 1
        ''' (motor-63).</para>
        ''' <para>⛔ **`k'` multiplica SÓLO el término de `maximumDistance`.** Los dos términos
        ''' normales no lo llevan: los únicos `mulps …, [kVec]` del kernel son `0x141A02B2F` y
        ''' `0x141A02F15`.</para>
        ''' <para>⛔ Los tres recortes son `min(·, 0)` — la restricción es **unilateral en los tres
        ''' ejes** y nunca empuja hacia afuera del rango.</para>
        ''' <para>⛔ `Prev` se reescribe como `Pnuevo − (Pviejo − Prev)`, **no** como `Prev + Δ`:
        ''' el motor guarda la velocidad ANTES (`subps xmm5, [previous]`) y se la resta al final
        ''' (`0x141A02B50`). En `float` las dos formas no dan el mismo bit.</para>
        ''' <para>⭐ La corrección se le aplica a `previous` ENTERA ⇒ la partícula se mueve **sin
        ''' cambiar de velocidad**. Es lo que hace que un rango local no inyecte energía.</para>
        ''' </summary>
        Protected Overrides Sub Kernel(ctx As ContextoDeSolve, k As Vector128(Of Single))
            ' ⛔ shapeType ∉ {0,1} ⇒ el motor SALE sin tocar nada (`jne 0x141A027BC`), no lo
            ' trata como punto (motor-62).
            If _shapeType <> 0 AndAlso _shapeType <> 1 Then Return
            Dim mideContraLaLinea = (_shapeType = 1)

            Dim inst = ctx.Instancia
            ' ⛔ la doble indirección, como en 0x141A01F5A…F65. `Buffers.Real` revienta si no se
            ' puede resolver: el motor no tiene camino de escape y acá tampoco (motor-61).
            Dim buf = Buffers.Real(ctx.Buffers, _bufferIdx)   ' 0x141A01F5A…F65, LocalRange
            Dim m = buf.AEspacioDeSimulacion
            ' 0x141A01FA0 + 0x141A01FA6: el `applyNormalComponent` NO alcanza — el buffer tiene
            ' que traer normales. (El `& 1` de +0x48 sólo elige el direccionamiento: medido que
            ' no cambia ni una operación aritmética.)
            Dim usaNormal = _aplicaNormal AndAlso buf.Normales IsNot Nothing
            Dim pos = inst.Posiciones
            Dim prev = inst.Previas
            Dim cero = Vector128(Of Single).Zero

            ' ⛔ el `k` que ve la hoja ya viene escalado por `stiffness` (motor-63)
            Dim kEfectivo = Vector128.Multiply(k, Vector128.Create(_stiffness))

            For i = 0 To _n - 1
                Dim ci = _particula(i), ri = _refVertice(i)
                Dim p = Simd.Leer(pos, ci)
                Dim q = Operadores.TransformarPunto(buf.Vertice(ri), m)
                Dim nrm = cero
                If usaNormal Then nrm = Operadores.TransformarDireccion(buf.Normal(ri), m)

                Dim origen = q
                Dim t = cero
                If usaNormal AndAlso mideContraLaLinea Then
                    ' 0x141A03C83…CB1: la proyección sobre la línea (q, n). ⭐ Y este MISMO
                    ' escalar es el que después miden los dos topes normales (motor-64).
                    t = Simd.Dot3(Vector128.Subtract(p, q), nrm)
                    origen = Vector128.Add(q, Vector128.Multiply(t, nrm))
                End If

                Dim d = Vector128.Add(Vector128.Subtract(p, origen),
                                      Vector128.Create(Simd.FltEpsilon))     ' 0x141A02AE4
                Dim len2 = Simd.Dot3(d, d)
                Dim inv = Simd.RsqrtConGuarda(len2)                          ' CRUDO, 0x141A02B0C
                Dim dHat = Vector128.Multiply(inv, d)                        ' 0x141A02B29
                Dim largo = Vector128.Multiply(inv, len2)                    ' 0x141A02B26

                Dim cMax = Vector128.Min(Vector128.Multiply(
                               Vector128.Subtract(Vector128.Create(_maxDist(i)), largo), kEfectivo),
                               cero)                                         ' 0x141A02B2C/B2F/B32
                Dim pNuevo = Vector128.Add(Vector128.Multiply(cMax, dHat), p)  ' 0x141A02B39/B3C

                If usaNormal Then
                    If Not mideContraLaLinea Then
                        ' la hoja del PUNTO fabrica su propio `t` con la longitud YA recortada
                        t = Vector128.Multiply(Simd.Dot3(nrm, dHat),
                                               Vector128.Add(cMax, largo))    ' 0x141A02F3A/F40
                    End If
                    Dim bajo = Vector128.Min(Vector128.Subtract(
                                   t, Vector128.Create(_minNormalDist(i))), cero)   ' 0x141A02F50/F7A
                    Dim alto = Vector128.Min(Vector128.Subtract(
                                   Vector128.Create(_maxNormalDist(i)), t), cero)   ' 0x141A02F72/F7D
                    pNuevo = Vector128.Subtract(pNuevo, Vector128.Multiply(bajo, nrm))  ' 0x141A02F8B
                    pNuevo = Vector128.Add(pNuevo, Vector128.Multiply(alto, nrm))       ' 0x141A02F8E
                End If

                If ctx.UsaK Then
                    ' ⛔ la velocidad se guarda ANTES y se resta al final: NO es `Prev + Δ`
                    Dim vel = Vector128.Subtract(p, Simd.Leer(prev, ci))
                    Simd.Escribir(pos, ci, pNuevo)
                    Simd.Escribir(prev, ci, Vector128.Subtract(pNuevo, vel))
                Else
                    Simd.Escribir(pos, ci, pNuevo)
                End If
            Next
        End Sub

    End Class

    ' =============================================================================================

    ''' <summary>
    ''' `hclBonePlanesConstraintSet` — envoltorio `0x1419FCB80`, kernels `0x1419FCBD0` (sin `usaK`)
    ''' y `0x1419FCD60` (con). **6 en el corpus.**
    ''' <para>⭐ Es el único constraint set que **no abre scope de perfilado**, así que el mapa por
    ''' cadenas `Tt…` no lo encuentra. Su `solve` salió del **RTTI**: descriptor
    ''' `.?AVhclBonePlanesConstraintSet@@` (`0x1430ADD80`) → COL `0x142AB4E08` → vtable
    ''' `0x1426FE950`, slot `+0x48`.</para>
    ''' <para>`bonePlane` de `0x20` B, los cuatro campos por reflexión:
    ''' `planeEquationBone:vec4@0`, `particleIndex:u16@0x10`, `transformIndex:u16@0x12`,
    ''' `stiffness:f32@0x14`.</para>
    ''' <para>⛔ Y `hclClothState` le da `k = 1,0` **sólo en el último substep** (tipo 10, cap. 6.3),
    ''' con `k = 0` en los demás y la puerta `k &gt; 0` cortando ⇒ actúa **una vez por frame**.</para>
    ''' </summary>
    Friend NotInheritable Class PlanosDeHueso
        Inherits SetCompilado

        Private ReadOnly _plano As Vector128(Of Single)()
        Private ReadOnly _particula As Integer(), _transform As Integer()
        Private ReadOnly _stiffness As Single()
        Private ReadOnly _transformSetIndex As Integer
        Private ReadOnly _n As Integer

        Friend Sub New(src As HkObj_HclBonePlanesConstraintSet, tipo As Integer)
            MyBase.New(tipo, src.Name)
            Dim ps = src.BonePlanes
            Dim n = If(ps Is Nothing, 0, ps.Count)
            Dim m = Math.Max(1, n)
            ReDim _plano(m - 1) : ReDim _particula(m - 1)
            ReDim _transform(m - 1) : ReDim _stiffness(m - 1)
            For i = 0 To n - 1
                Dim bp = ps(i)
                ' `planeEquationBone` llega del parser como `Single()` de 4
                Dim e = bp.PlaneEquationBone
                _plano(i) = If(e Is Nothing OrElse e.Length < 4,
                               Vector128(Of Single).Zero,
                               Vector128.Create(e(0), e(1), e(2), e(3)))
                _particula(i) = bp.ParticleIndex
                _transform(i) = bp.TransformIndex
                _stiffness(i) = bp.Stiffness
            Next
            _transformSetIndex = CInt(src.TransformSetIndex)
            _n = n
        End Sub

        ''' <summary>Un solo plano, sin archivo: para los gates de la ley.</summary>
        Friend Sub New(plano As Vector128(Of Single), particula As Integer, transform As Integer,
                       stiffness As Single, transformSetIndex As Integer)
            MyBase.New(10, "prueba")
            _plano = {plano} : _particula = {particula}
            _transform = {transform} : _stiffness = {stiffness}
            _transformSetIndex = transformSetIndex
            _n = 1
        End Sub

        Friend Overrides ReadOnly Property Cuenta As Integer
            Get
                Return _n
            End Get
        End Property

        ''' <summary>
        ''' ```
        ''' M      = transformSets[set.transformSetIndex][ bp.transformIndex ]   ' 0x1419FCC63 shl rcx,6
        ''' n      = TransformarDireccion( bp.planeEquationBone, M )   ' 0x1419FCC76/6C/71/7B/9B
        ''' origen = TransformarPunto( 0, M ) = M.F3                   ' 0x1419FCC93…CC3
        ''' dist   = dot3( P[ci] − origen, n ) + bcast_w(plano)        ' 0x1419FCCC6/D7/F1/FE + D05
        ''' si dist &lt; 0:                                              ' 0x1419FCD0F psrad xmm3, 31
        '''     P[ci] += (k · stiffness) · (−dist) · n                 ' 0x1419FCCCD/D14/D17/D1B
        ''' si usaK:  Prev[ci] = Pnuevo − (Pviejo − Prev[ci])          ' 0x1419FCE0C/E10 + CED9/CEDD
        ''' ```
        ''' <para>⛔ **La normal NO se normaliza.** Sale de multiplicar `planeEquationBone.xyz` por
        ''' la matriz del hueso y se usa cruda: no hay un solo `rsqrt` en el kernel. Normalizarla
        ''' sería «arreglar» el motor.</para>
        ''' <para>⛔ **Unilateral con máscara de signo, no con salto.** El motor hace
        ''' `psrad xmm3, 31` sobre los bits de `dist` y después `andps`/`andnps`/`orps`
        ''' (`0x1419FCD1F`/`D22`/`D26`). Acá va con `ShiftRightArithmetic` sobre los mismos bits,
        ''' que es literalmente `psrad`: un `LessThan(dist, 0)` **no** es lo mismo con `−0,0` ni con
        ''' `NaN`.</para>
        ''' <para>⛔⛔ **La variante `usaK` PRESERVA la velocidad**, no la mata: guarda
        ''' `Pviejo − Prev` ANTES (`0x1419FCE0C`/`E10`) y escribe `Prev = Pnuevo − eso`. Es la misma
        ''' ley que `RangoLocal`, confirmada en dos clases independientes. (El RE decía lo
        ''' contrario y estaba mal; corregido el 06-sep.)</para>
        ''' <para>⭐ El `bcast_w(plano)` del motor es el **broadcast sin rama**: máscara
        ''' `0x142717C40 = (0,0,0,FFFFFFFF)` + `shufps 0x4E`/`orps` + `shufps 0xB1`/`orps`
        ''' (`0x1419FCC86`…`9F` y `0x1419FCCF4`…`FB`). Da lo mismo que difundir la lane 3.</para>
        ''' </summary>
        Protected Overrides Sub Kernel(ctx As ContextoDeSolve, k As Vector128(Of Single))
            Dim inst = ctx.Instancia
            If ctx.TransformSets Is Nothing Then
                Throw New InvalidOperationException(
                    "PlanosDeHueso: el contexto no trae `TransformSets` (hclClothInstance+0x40).")
            End If
            If _transformSetIndex < 0 OrElse _transformSetIndex >= ctx.TransformSets.Length Then
                Throw New InvalidOperationException(
                    $"PlanosDeHueso: transformSetIndex {_transformSetIndex} fuera de " &
                    $"[0, {ctx.TransformSets.Length - 1}].")
            End If
            Dim ts = ctx.TransformSets(_transformSetIndex)
            Dim pos = inst.Posiciones
            Dim prev = inst.Previas
            Dim kEscalar = Simd.Lane0(k)

            For i = 0 To _n - 1
                Dim ci = _particula(i)
                Dim m = ts(_transform(i))
                Dim plano = _plano(i)
                Dim n = Operadores.TransformarDireccion(plano, m)    ' CRUDA, sin normalizar
                Dim p = Simd.Leer(pos, ci)
                Dim vel = Vector128.Subtract(p, Simd.Leer(prev, ci)) ' 0x1419FCE0C/E10, ANTES

                ' `dist = dot3(P − M.F3, n) + plano.w`
                Dim dist = Vector128.Add(Simd.Dot3(Vector128.Subtract(p, m.F3), n),
                                         Simd.BcastW(plano))
                ' ⛔ el `mulss` + `shufps 0` del motor: `k` por lane 0, no el vector entero
                Dim f = Vector128.Create(kEscalar * _stiffness(i))
                Dim corr = Vector128.Multiply(
                    Vector128.Multiply(f, Vector128.Subtract(Vector128(Of Single).Zero, dist)), n)
                Dim pNuevo = Vector128.Add(corr, p)                  ' 0x1419FCD1B

                ' ⛔ `psrad` sobre los BITS: todo unos donde el bit de signo de `dist` está puesto
                Dim mascara = Vector128.ShiftRightArithmetic(dist.AsInt32(), 31).AsSingle()
                Dim elegido = Vector128.ConditionalSelect(mascara, pNuevo, p)

                Simd.Escribir(pos, ci, elegido)
                If ctx.UsaK Then
                    Simd.Escribir(prev, ci, Vector128.Subtract(elegido, vel))
                End If
            Next
        End Sub

    End Class
    ' =============================================================================================

    ''' <summary>
    ''' El bloque de estado de `hclAntiPinchConstraintSet` — `inst[+0xC0]`, buscado por id.
    ''' <para>⛔ ES OTRO TIPO que <see cref="EstadoDeSet"/>, y por dos razones medidas: vive en otra
    ''' lista de la instancia, y su FASE y su RELOJ son POR PARTICULA (`byte[inst+0x30 + i]` y
    ''' `float[inst+0x20 + i*4]`, `0x1419F83D3`/`0x1419F83E3`) en vez de escalares del set. Una
    ''' particula puede estar volviendo a animacion mientras la de al lado sigue simulando — lo que
    ''' se pellizca es UNA particula, no la prenda.</para>
    ''' </summary>
    Friend NotInheritable Class EstadoPorParticula

        ''' <summary>El id del set, que es como el motor lo encuentra en la lista.</summary>
        Friend ReadOnly Id As Integer

        ''' <summary>`inst[+0x30] + i` — la fase de cada particula.</summary>
        Friend ReadOnly Fase As Integer()

        ''' <summary>`inst[+0x20] + i*4` — el reloj de cada particula.</summary>
        Friend ReadOnly Reloj As Single()

        ''' <summary>`inst[+0x10] + i*4` — la distancia con la que arranco la rampa.</summary>
        Friend ReadOnly Distancia As Single()

        Friend Sub New(id As Integer, n As Integer)
            Me.Id = id
            Dim m = Math.Max(1, n)
            ReDim Fase(m - 1)
            ReDim Reloj(m - 1)
            ReDim Distancia(m - 1)
        End Sub

    End Class

    ''' <summary>
    ''' El bloque de estado de **un** `hclTransitionConstraintSet`, que vive en la instancia y
    ''' **persiste entre frames**.
    ''' <para>⛔ Que este set no sea puro es una ley del motor, no una comodidad: sin el bloque no
    ''' hay transición, porque la rampa se mide contra la distancia que había cuando arrancó el
    ''' retardo.</para>
    ''' </summary>
    Friend NotInheritable Class EstadoDeSet

        ''' <summary>El id con el que la búsqueda lineal lo encuentra (`u32` en `+0` del registro
        ''' de `0x10` B). Es el 4.º argumento de `solve`, o sea el índice del set.</summary>
        Friend ReadOnly Id As Integer

        ''' <summary>`+0x10` — un `float` **por partícula**, indexado por `particleIndex`
        ''' (`0x141A09068` `movss [rax + r10*4]`, con `r10` = el `particleIndex` del registro).
        ''' Guarda la distancia que tenía la partícula cuando arrancó el retardo hacia animación.</summary>
        Friend ReadOnly DistanciaDeArranque As Single()

        ''' <summary>`+0x20` — la fase. `0` no corre; `1` animación pura; `2` hacia animación;
        ''' `3` hacia simulación; `≥4` no hace nada (`0x141A08E30` `cmp esi,1 / jne`).</summary>
        Friend Fase As Integer

        ''' <summary>`+0x24` — el tiempo acumulado de la transición (`0x141A08DFE`).</summary>
        Friend Tiempo As Single

        ''' <summary>⭐⭐ `estado+0x40` — la SUMA PONDERADA de posiciones que deja la fase 1 de
        ''' `hclVolumeConstraintMx` (`0x141A0AC8D` la escribe cruda, sin normalizar), y de la que la
        ''' fase 2 resta para armar la covarianza.</summary>
        Friend CentroidePonderado As Vector128(Of Single)

        ''' <summary>
        ''' `estado+0x10..0x30` — el marco **R de ESTE cuadro**, el que la fase 3 usa para saber
        ''' dónde va cada partícula: `objetivo = frameVector · R + centroide` (`0x141339F90`, con
        ''' `rdx = estado+0x10` y la traslación en `+0x40`).
        ''' <para>⛔ NO es la semilla: la semilla es <see cref="BasePropia"/>. Son dos matrices y el
        ''' motor las guarda en dos sitios.</para>
        ''' </summary>
        Friend Marco As Mat3

        ''' <summary>
        ''' ⭐⭐ `estado+0x50` — la BASE PROPIA, **de un cuadro para el otro**.
        ''' <para>Es el `lea r9, [r14+0x50]` de `0x141A0A630`, y entra como **semilla en caliente** del
        ''' eigensolver (`0x141360C70`): el solver arranca de `M = V·A·Vᵀ`, asi que si la semilla ya
        ''' es la base propia converge en cero iteraciones.</para>
        ''' <para>⛔ Tratarlo como temporal deja el marco ortonormal igual, pero con autovalores
        ''' repetidos —una tela plana— elige otro cada cuadro, y eso es TEMBLOR (cap. 6q.4bis).</para>
        ''' </summary>
        ''' <para>⛔ ARRANCA EN IDENTIDAD, y esto es lo que esta medido y lo que no. Con la semilla
        ''' en CERO el eigensolver sale en cero — `a = V·M·Vᵀ = 0`, el `off2` da 0 y el umbral
        ''' `‖M‖²·tol²` es mayor, asi que converge de inmediato — y `FactorOrtogonal` devuelve el
        ''' marco CERO por la guarda de singularidad. Como `V` sale igual que entro, **queda en cero
        ''' para siempre**: la prenda colapsaria al centroide y no saldria nunca de ahi.</para>
        ''' <para>⚠ Lo que NO esta leido: donde el motor inicializa `estado+0x50`. El bloque se crea
        ''' fuera de `solve`. El RE (cap. 6q.4bis) dice que con `I` de semilla «el resultado sigue
        ''' siendo correcto, pero no es el del motor: cambia cuantas rotaciones hace». O sea que si
        ''' algun dia se lee el inicializador y es otro, lo que cambia es el numero de rotaciones del
        ''' PRIMER cuadro; de ahi en adelante manda la semilla en caliente.</para>
        Friend BasePropia As Mat3 = Mat3.Identidad

        ''' <summary>¿La base propia ya trae un valor del cuadro anterior?</summary>
        Friend BaseSembrada As Boolean

        Friend Sub New(id As Integer, numParticulas As Integer)
            Me.Id = id
            ReDim DistanciaDeArranque(Math.Max(1, numParticulas) - 1)
        End Sub

    End Class

    ''' <summary>
    ''' `hclTransitionConstraintSet` — envoltorio `0x141A08C90`
    ''' (`"TtTransition Constraints"`, `0x142719528`), cuatro kernels por
    ''' `bufferReal[+0x20]&amp;1` × `usaK`: `0x141A08D60`, `0x141A091C0`, `0x141A09650`, `0x141A09AB0`.
    ''' ⛔ **0 en el corpus** — va igual.
    ''' <para>⛔⛔ **La puerta NO es la de los enlaces.** `0x141A08DA7` deja pasar si `k &gt; 0`, y si
    ''' no, sólo corta cuando la fase está en `{2, 3}` (`0x141A08DAD` `lea eax,[rsi-2]` /
    ''' `cmp eax,1` / `jbe`). O sea que con `k = 0` la transición **igual avanza** en las fases 1 y
    ''' ≥4. Copiarle `k &lt;= 0 → return` la habría congelado en todos los substeps menos el
    ''' último.</para>
    ''' <para>⭐ Y la puerta se explica sola: las fases 2 y 3 **dividen por `k`** (`0x141A08EF7`,
    ''' `0x141A09097`), así que es exactamente la que garantiza que el divisor no sea cero.</para>
    ''' <para>⭐⭐ **Los cuatro kernels son DOS leyes, y está MEDIDO** (no supuesto: es el atajo que
    ''' motor-65 marcó). `0x141A08D60` y `0x141A09650` tienen 266 instrucciones y **4 `subps`**;
    ''' `0x141A091C0` y `0x141A09AB0`, 279 y **8**. ⇒ el eje `bufferReal[+0x20] &amp; 1` **no cambia
    ''' ni una operación** (puro direccionamiento, como en `LocalRange`), y el que sí la cambia es
    ''' **`usaK`**: `+4 subps` y `+2 stores`, dos por cada sitio de recorte.</para>
    ''' <para>⛔ Y sólo el **RECORTE** escribe `previous`: las tres «clavadas» (`0x141A0931E` fase 3,
    ''' `0x141A09507` fase 2, `0x141A095CD` fase 1) son `movups` sueltos aun en la variante `usaK`.
    ''' Cuando la partícula se clava a la malla **no** se le conserva la velocidad; cuando se la
    ''' recorta, sí.</para>
    ''' </summary>
    Friend NotInheritable Class Transicion
        Inherits SetCompilado

        Private ReadOnly _particula As Integer(), _refVertice As Integer()
        Private ReadOnly _retardoAAnim As Single(), _retardoASim As Single()
        Private ReadOnly _distMaxASim As Single()
        Private ReadOnly _periodoAAnim As Single, _periodoASim As Single
        Private ReadOnly _bufferIdx As Integer
        Private ReadOnly _n As Integer

        Friend Sub New(src As HkObj_HclTransitionConstraintSet, tipo As Integer)
            MyBase.New(tipo, src.Name)
            Dim ds = src.PerParticleData
            Dim n = If(ds Is Nothing, 0, ds.Count)
            Dim m = Math.Max(1, n)
            ReDim _particula(m - 1) : ReDim _refVertice(m - 1)
            ReDim _retardoAAnim(m - 1) : ReDim _retardoASim(m - 1) : ReDim _distMaxASim(m - 1)
            For i = 0 To n - 1
                Dim d = ds(i)
                _particula(i) = d.ParticleIndex : _refVertice(i) = d.ReferenceVertex
                _retardoAAnim(i) = d.ToAnimDelay : _retardoASim(i) = d.ToSimDelay
                _distMaxASim(i) = d.ToSimMaxDistance
            Next
            _periodoAAnim = src.ToAnimPeriod        ' +0x30, 0x141A09070
            _periodoASim = src.ToSimPeriod          ' +0x38, 0x141A08EC7
            _bufferIdx = CInt(src.ReferenceMeshBufferIdx)
            _n = n
        End Sub

        ''' <summary>Una sola partícula, sin archivo: para los gates de la ley.</summary>
        Friend Sub New(particula As Integer, refVertice As Integer,
                       retardoAAnim As Single, retardoASim As Single, distMaxASim As Single,
                       periodoAAnim As Single, periodoASim As Single, bufferIdx As Integer)
            MyBase.New(8, "prueba")
            _particula = {particula} : _refVertice = {refVertice}
            _retardoAAnim = {retardoAAnim} : _retardoASim = {retardoASim}
            _distMaxASim = {distMaxASim}
            _periodoAAnim = periodoAAnim : _periodoASim = periodoASim
            _bufferIdx = bufferIdx
            _n = 1
        End Sub

        ''' <summary>Varias partículas, sin archivo: hace falta para medir que la fase 3 SALE DEL
        ''' BUCLE (`Exit For`) en vez de saltear la partícula — con una sola las dos cosas dan lo
        ''' mismo.</summary>
        Friend Sub New(particulas As Integer(), refVertices As Integer(),
                       retardosAAnim As Single(), retardosASim As Single(),
                       distsMaxASim As Single(),
                       periodoAAnim As Single, periodoASim As Single, bufferIdx As Integer)
            MyBase.New(8, "prueba")
            _particula = particulas : _refVertice = refVertices
            _retardoAAnim = retardosAAnim : _retardoASim = retardosASim
            _distMaxASim = distsMaxASim
            _periodoAAnim = periodoAAnim : _periodoASim = periodoASim
            _bufferIdx = bufferIdx
            _n = particulas.Length
        End Sub

        Friend Overrides ReadOnly Property Cuenta As Integer
            Get
                Return _n
            End Get
        End Property

        ''' <summary>
        ''' ⛔⛔ **Este set NO corta con `k &lt;= 0`.** Su envoltorio `0x141A08C90` reenvía el `k` sin
        ''' mirarlo; la puerta vive DENTRO del kernel (`0x141A08DA7` `comiss`/`ja`, y si no pasa,
        ''' `0x141A08DAD`-`0x141A08DB3` sólo corta cuando la fase está en `{2,3}`). Con `k = 0` la
        ''' fase 1 y la ≥4 **igual corren**.
        ''' <para>Heredar la puerta de los enlaces congelaba la transición en todos los substeps
        ''' menos el último, que es justo lo contrario de lo que una transición tiene que hacer.</para>
        ''' </summary>
        Protected Overrides ReadOnly Property CortaConKNoPositivo As Boolean
            Get
                Return False
            End Get
        End Property

        ''' <summary>
        ''' La máquina de cuatro fases de `0x141A08D60`.
        ''' <para>⚠️ **`toAnimPlusDelayPeriod` (+0x34) y `toSimPlusDelayPeriod` (+0x3C) NO los lee
        ''' este kernel.** Los únicos períodos que aparecen son `[rdi+0x30]` y `[rdi+0x38]`. Quedan
        ''' anotados como no-leídos-acá, no como campos muertos: falta mirar los otros tres kernels
        ''' y `prepare`.</para>
        ''' <para>⛔ Las cinco divisiones son `divss` **EXACTAS** (`0x141A08EE1`, `0x141A08EF7`,
        ''' `0x141A09085`, `0x141A09097`, `0x141A090A5`), y las dos longitudes son `rsqrtps` +
        ''' **UNA** Newton (`3,0` en `0x142629510`, `0,5` en `0x142629520`).</para>
        ''' <para>⛔ El recorte **no** es `P = q + d̂·f`: es `P += ((f − |d|)/|d|)·(P − q)`, que da
        ''' el mismo punto por otro camino de redondeo.</para>
        ''' </summary>
        Protected Overrides Sub Kernel(ctx As ContextoDeSolve, k As Vector128(Of Single))
            Dim inst = ctx.Instancia
            Dim est = BuscarEstado(inst, ctx.IndiceDelSet)
            If est.Fase = 0 Then Return                                   ' 0x141A08D85/87

            Dim kEsc = Simd.Lane0(k)
            ' ⛔ la puerta rara: con `k <= 0` sólo corta si la fase divide por `k`
            If Not (kEsc > 0.0F) Then
                If est.Fase = 2 OrElse est.Fase = 3 Then Return           ' 0x141A08DAD…B3
            End If
            If est.Fase <> 1 AndAlso est.Fase <> 2 AndAlso est.Fase <> 3 Then Return  ' 0x141A08E30

            Dim buf = Buffers.Real(ctx.Buffers, _bufferIdx)
            Dim m = buf.AEspacioDeSimulacion
            Dim pos = inst.Posiciones
            Dim prev = inst.Previas

            For i = 0 To _n - 1
                Dim ci = _particula(i)
                Dim q = Operadores.TransformarPunto(buf.Vertice(_refVertice(i)), m)

                If est.Fase = 1 Then
                    ' animación pura: clavada a la malla, sin condición (0x141A090E0…09145)
                    ' ⛔ UNA sola escritura, incluso con `usaK` (0x141A095CD)
                    Simd.Escribir(pos, ci, q)
                    Continue For
                End If

                Dim p = Simd.Leer(pos, ci)
                ' ⛔ la velocidad se captura ANTES de tocar nada (`0x141A0935F` en la fase 3 y
                ' `0x141A09474` en la 2, los dos `subps` contra `[previous]`), y sólo la usa el
                ' camino de RECORTE.
                Dim vel = If(ctx.UsaK, Vector128.Subtract(p, Simd.Leer(prev, ci)),
                             Vector128(Of Single).Zero)
                Dim d = Vector128.Subtract(p, q)
                Dim len2 = Simd.Dot3(d, d)
                ' `rsqrtps` + UNA Newton, y el largo sale de `r·|d|²` (0x141A08F1D…F47)
                Dim largo = Simd.Lane0(Vector128.Multiply(len2, Simd.RsqrtNewtonConGuarda(len2)))

                Dim f As Single
                If est.Fase = 2 Then
                    Dim t = est.Tiempo - _retardoAAnim(i)                 ' 0x141A09057
                    If Not (t > 0.0F) Then
                        ' todavía no arrancó: se guarda la distancia de partida y se sigue
                        est.DistanciaDeArranque(ci) = largo               ' 0x141A09064/68
                        Continue For
                    End If
                    If Not (t < _periodoAAnim) Then
                        ' ⛔ la CLAVADA escribe sólo `positions`, aun con `usaK`: `0x141A09507` es
                        ' un `movups` suelto, contra los tres de `0x141A09540`/`45`/`49`
                        Simd.Escribir(pos, ci, q)                         ' 0x141A0907A, llegó
                        Continue For
                    End If
                    f = (1.0F - t / _periodoAAnim) * est.DistanciaDeArranque(ci)  ' 0x141A09085/89/8D/91
                    f = f / kEsc                                          ' 0x141A09097 divss
                Else
                    Dim t = est.Tiempo - _retardoASim(i)                  ' 0x141A08E62
                    If Not (t > 0.0F) Then
                        ' ⛔ ídem: una sola escritura (`0x141A0931E`), aun con `usaK`
                        Simd.Escribir(pos, ci, q)                         ' 0x141A08EBE, clavada
                        Continue For
                    End If
                    ' ⛔ ACÁ el motor SALE DEL BUCLE ENTERO (`jae 0x141A09147`, el epílogo), no
                    ' saltea la partícula. En la fase 2 el caso análogo sí sigue. La asimetría
                    ' está medida, no interpretada.
                    If t >= _periodoASim Then Exit For                    ' 0x141A08ECF jae
                    f = (t / _periodoASim) * _distMaxASim(i)              ' 0x141A08EE1/E9
                    f = f / kEsc                                          ' 0x141A08EF7 divss
                End If

                ' ⛔ `Not (largo > f)`, NO `largo <= f` (motor-74). El motor hace
                ' `comiss xmm5, xmm4` / `jbe` (`0x141A08F4A`, `0x141A0909C`), y `jbe` salta
                ' también cuando la comparación es **unordered** — o sea con NaN **no recorta**.
                ' `largo <= f` en VB da `False` con NaN y sí recortaría, escribiendo NaN.
                If Not (largo > f) Then Continue For                      ' 0x141A08F4A / 0x141A0909C
                ' ⛔ `f − largo` (negativo), NO `largo − f`
                Dim g = (f - largo) / largo                               ' 0x141A08F4F/F53
                Dim pNuevo = Vector128.Add(p, Vector128.Multiply(Vector128.Create(g), d))
                Simd.Escribir(pos, ci, pNuevo)                            ' 0x141A09540 / 0x141A093D2
                If ctx.UsaK Then
                    ' ⛔⛔ **Sólo el RECORTE toca `previous`**, y con la misma forma que
                    ' `RangoLocal` y `PlanosDeHueso`: `Pnuevo − (Pviejo − Prev)`
                    ' (`0x141A09545` `subps xmm0, xmm8` + `0x141A09549` `movups`). **Tercera**
                    ' clase independiente con la MISMA ley de `usaK` — ya no es casualidad de una
                    ' clase, es la ley del solver.
                    Simd.Escribir(prev, ci, Vector128.Subtract(pNuevo, vel))
                End If
            Next
        End Sub

        ''' <summary>La búsqueda LINEAL por id de `0x141A08CBE`-`0x141A08CFB`.
        ''' <para>⛔ Si no está, el motor llama al kernel con el puntero en cero y revienta en
        ''' `mov esi, [r9+0x20]`. Acá revienta con un mensaje, que es lo mismo pero legible: un
        ''' `Return` silencioso dejaría la prenda sin transición y sin rastro (motor-61).</para></summary>
        Private Shared Function BuscarEstado(inst As Instancia, id As Integer) As EstadoDeSet
            For Each e In inst.EstadosPorSet
                If e.Id = id Then Return e
            Next
            Throw New InvalidOperationException(
                $"Transicion: no hay bloque de estado con id {id} en `inst[+0xB0]`.")
        End Function

    End Class

End Namespace

