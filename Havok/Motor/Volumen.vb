Option Strict On
Option Explicit On

Imports System.Runtime.Intrinsics
Imports FO4_Base_Library.Havok.Canon.Objects

' =================================================================================================
' `hclVolumeConstraintMx` — tipo 17, `solve` en `0x141A0A4D0`. RE cap. 6q.3 y 6q.4.
'
' ⛔ El corpus trae `hclVolumeConstraintMx` (38 sets), NO `hclVolumeConstraint` (tipo 6,
' `0x141A0A130`), del que hay CERO instancias. La ley es la misma; las citas son las del camino VIVO.
'
' Es SHAPE MATCHING en tres fases, leído del binario:
'
'   (0) estado = buscarPorId(inst[+0xB0], idDelSet)     ' 0x141A0A528, entradas de 16 B: {id@0, ptr@+8}
'       estado[+0x40] = 0                                ' 0x141A0A5AA, el acumulador del centroide
'
'   (1) CENTROIDE PONDERADO — la suma CRUDA, sin normalizar:
'         batch  (0x141A0AB60): estado[+0x40]  = Σ w·P[idx]     ' PISA  (0x141A0AC8D movups)
'         single (0x141A675C0): estado[+0x40] += Σ w·P[idx]     ' ACUMULA (0x141A67601-08)
'       ⇒ el orden importa: batch primero, single después.
'
'   (2) COVARIANZA y MARCO:
'         A = 3 filas que arrancan en FLT_EPSILON (`0x142F3C760`, verificado: las 12 lanes)
'         A.filaK += bcast(frameVector[K]) · (P[idx] − centroide) · w
'         ⇒ A = Σ w · outer(frameVector, P − C), y después la DESCOMPOSICIÓN POLAR.
'
'   (3) APLICAR (`0x141A0A890` / `0x141A67790`), con `k` difundido a las 4 lanes (`0x141A0A65E`).
'
' ⭐⭐ EL MARCO VIVE ENTRE CUADROS (`estado+0x50`, `0x141A0A630`): entra como SEMILLA EN CALIENTE del
' eigensolver, que arranca de `M = V·A·Vᵀ`. Si la semilla ya es la base propia, converge en cero
' iteraciones. Tratarlo como temporal deja el marco ortonormal igual, pero con autovalores repetidos
' —una tela plana— elige otro cada cuadro: eso es TEMBLOR.
'
' ⛔ La guarda de singularidad de la inversa (`0x141360010`) devuelve la matriz CERO cuando
' `‖f0‖₁·‖f1‖₁·‖f2‖₁ · FLT_EPSILON >= |det|`. Un marco degenerado colapsa todo al centroide — eso ES
' el motor, no un caso a «arreglar».
' =================================================================================================


Namespace Havok.Motor

    ''' <summary>
    ''' Un `hclVolumeConstraintMx` compilado: las cuatro listas del archivo en arreglos planos.
    ''' <para>Las variantes `batch` y `single` son el MISMO dato con distinto empaquetado — 16 por
    ''' lote contra uno suelto — y el motor las corre con dos kernels. Acá se aplanan las dos a la
    ''' misma forma, y lo que se conserva es el ORDEN: batch primero, single después, porque la
    ''' fase 1 batch PISA y la single ACUMULA.</para>
    ''' </summary>
    Friend NotInheritable Class Volumen
        Inherits SetCompilado

        ''' <summary>Los `frameData`: `frameVector` (4 floats), partícula y peso.</summary>
        Friend ReadOnly MarcoVector As Single()
        Friend ReadOnly MarcoParticula As Integer()
        Friend ReadOnly MarcoPeso As Single()


        ''' <summary>Los `applyData`: `frameVector`, partícula y rigidez.</summary>
        Friend ReadOnly AplicarVector As Single()
        Friend ReadOnly AplicarParticula As Integer()
        Friend ReadOnly AplicarRigidez As Single()

        Private ReadOnly _n As Integer

        Friend Overrides ReadOnly Property Cuenta As Integer
            Get
                Return _n
            End Get
        End Property

        Friend Sub New(src As HkObj_HclVolumeConstraintMx, tipo As Integer)
            MyBase.New(tipo, src.Name)

            ' ---- las dos listas de marco, en orden: LOTE y después SUELTOS ----
            Dim mv As New List(Of Single)(), mp As New List(Of Integer)(), mw As New List(Of Single)()
            If src.FrameBatchDatas IsNot Nothing Then
                For Each b In src.FrameBatchDatas
                    If b Is Nothing Then Continue For
                    Dim n = If(b.ParticleIndex Is Nothing, 0, b.ParticleIndex.Count)
                    For i = 0 To n - 1
                        Volcar(mv, b.FrameVector, i)
                        mp.Add(b.ParticleIndex(i))
                        mw.Add(If(b.Weight Is Nothing OrElse i >= b.Weight.Count, 0.0F, b.Weight(i)))
                    Next
                Next
            End If
            If src.FrameSingleDatas IsNot Nothing Then
                For Each e In src.FrameSingleDatas
                    If e Is Nothing Then Continue For
                    VolcarUno(mv, e.FrameVector)
                    mp.Add(e.ParticleIndex)
                    mw.Add(e.Weight)
                Next
            End If
            MarcoVector = mv.ToArray() : MarcoParticula = mp.ToArray() : MarcoPeso = mw.ToArray()

            ' ---- las dos de aplicar ----
            Dim av As New List(Of Single)(), ap As New List(Of Integer)(), ar As New List(Of Single)()
            If src.ApplyBatchDatas IsNot Nothing Then
                For Each b In src.ApplyBatchDatas
                    If b Is Nothing Then Continue For
                    Dim n = If(b.ParticleIndex Is Nothing, 0, b.ParticleIndex.Count)
                    For i = 0 To n - 1
                        Volcar(av, b.FrameVector, i)
                        ap.Add(b.ParticleIndex(i))
                        ar.Add(If(b.Stiffness Is Nothing OrElse i >= b.Stiffness.Count, 0.0F, b.Stiffness(i)))
                    Next
                Next
            End If
            If src.ApplySingleDatas IsNot Nothing Then
                For Each e In src.ApplySingleDatas
                    If e Is Nothing Then Continue For
                    VolcarUno(av, e.FrameVector)
                    ap.Add(e.ParticleIndex)
                    ar.Add(e.Stiffness)
                Next
            End If
            AplicarVector = av.ToArray() : AplicarParticula = ap.ToArray() : AplicarRigidez = ar.ToArray()
            _n = AplicarParticula.Length
        End Sub

        ''' <summary>
        ''' `hclVolumeConstraint` — tipo 6, `0x141A0A130` (`TtVolume Constraints`).
        ''' <para>⭐ **Es la misma ley que el `Mx`, con el dato sin empaquetar.** El envoltorio llama
        ''' a los mismos tres kernels en el mismo orden (`0x141A672A0` centroide, `0x141A672F0`
        ''' covarianza, `0x141A0A280` aplicar), y el del centroide recorre entradas de `0x20` bytes
        ''' con `{frameVector @0x00, particleIndex u16 @0x10, weight @0x14}` — que es campo por campo
        ''' `hclVolumeConstraintFrameData` de la reflexión.</para>
        ''' <para>⛔ Por eso NO se escribe un segundo kernel: se aplanan las dos listas a los mismos
        ''' arreglos y corre el cuerpo de esta clase. Dos copias de una descomposición polar con
        ''' semilla en caliente podrían divergir sin que nada lo viera. G23 lo exige con un A/B.</para>
        ''' </summary>
        Friend Sub New(src As HkObj_HclVolumeConstraint, tipo As Integer)
            MyBase.New(tipo, src.Name)

            Dim mv As New List(Of Single)(), mp As New List(Of Integer)(), mw As New List(Of Single)()
            If src.FrameDatas IsNot Nothing Then
                For Each e In src.FrameDatas
                    If e Is Nothing Then Continue For
                    VolcarUno(mv, e.FrameVector)
                    mp.Add(e.ParticleIndex)
                    mw.Add(e.Weight)
                Next
            End If
            MarcoVector = mv.ToArray() : MarcoParticula = mp.ToArray() : MarcoPeso = mw.ToArray()

            Dim av As New List(Of Single)(), ap As New List(Of Integer)(), ar As New List(Of Single)()
            If src.ApplyDatas IsNot Nothing Then
                For Each e In src.ApplyDatas
                    If e Is Nothing Then Continue For
                    VolcarUno(av, e.FrameVector)
                    ap.Add(e.ParticleIndex)
                    ar.Add(e.Stiffness)
                Next
            End If
            AplicarVector = av.ToArray() : AplicarParticula = ap.ToArray() : AplicarRigidez = ar.ToArray()
            _n = AplicarParticula.Length
        End Sub

        ''' <summary>Un set armado a mano, sin archivo: para los gates de la ley. ⛔ Las cuatro
        ''' listas del archivo se aplanan igual que en el ctor de arriba, y `frameVector` sirve para
        ''' las dos fases porque en el dato real tambien coinciden.</summary>
        Friend Shared Function DePrueba(frameVector As IList(Of Single()), particula As IList(Of Integer),
                                        peso As IList(Of Single), rigidez As IList(Of Single)) As Volumen
            Return New Volumen(frameVector, particula, peso, rigidez)
        End Function

        Private Sub New(frameVector As IList(Of Single()), particula As IList(Of Integer),
                        peso As IList(Of Single), rigidez As IList(Of Single))
            MyBase.New(17, "(prueba)")
            Dim mv As New List(Of Single)()
            For i = 0 To particula.Count - 1
                VolcarUno(mv, frameVector(i))
            Next
            MarcoVector = mv.ToArray()
            MarcoParticula = particula.ToArray()
            MarcoPeso = peso.ToArray()
            AplicarVector = mv.ToArray()
            AplicarParticula = particula.ToArray()
            AplicarRigidez = rigidez.ToArray()
            _n = AplicarParticula.Length
        End Sub

        Private Shared Sub Volcar(destino As List(Of Single), fuente As IList(Of Single()), i As Integer)
            Dim v = If(fuente Is Nothing OrElse i >= fuente.Count, Nothing, fuente(i))
            VolcarUno(destino, v)
        End Sub

        Private Shared Sub VolcarUno(destino As List(Of Single), v As Single())
            For k = 0 To 3
                destino.Add(If(v Is Nothing OrElse k >= v.Length, 0.0F, v(k)))
            Next
        End Sub

        ''' <summary>
        ''' ⛔ El `k` de este set NO corta en cero como el de los enlaces: `0x141A0A4EF comiss` +
        ''' `jbe 0x141A0A6D9` sale con `k <= 0`, que es lo mismo, pero la base ya lo hace.
        ''' </summary>
        Protected Overrides Sub Kernel(ctx As ContextoDeSolve, k As Vector128(Of Single))
            Dim inst = ctx.Instancia
            ' (0) el bloque de estado del set, buscado POR ID (`0x141A0A528`)
            Dim est = inst.EstadoDelSet(ctx.IndiceDelSet)
            If est Is Nothing Then
                est = New EstadoDeSet(ctx.IndiceDelSet, inst.NumParticulas)
                inst.EstadosPorSet.Add(est)
            End If

            Dim pos = inst.Posiciones

            ' ---- (1) CENTROIDE PONDERADO — LA SUMA LLANA DE TODAS LAS ENTRADAS.
            ' ⭐ MEDIDO, no supuesto: el kernel de lotes (`0x141A0AB60`) arranca sus cuatro
            ' acumuladores en cero (`0x141A0AB7B`-`87`) y hace un STORE (`0x141A0AC8D`); el de
            ' sueltos (`0x141A675C0`) arma su suma aparte y hace un ADD sobre lo que aquel dejo
            ' (`0x141A67601`-`08`); y el envoltorio los llama en ese orden (`0x141A0A5C4`,
            ' `0x141A0A5D7`). O sea que el empaquetado en lotes no cambia ningun numero, y por
            ' eso no hay una frontera `MarcoDeLote` que respetar — la habia como campo, escrita
            ' en tres sitios y leida en NINGUNO.
            Dim c = Vector128(Of Single).Zero
            For i = 0 To MarcoParticula.Length - 1
                Dim p = MarcoParticula(i)
                If p < 0 OrElse p >= inst.NumParticulas Then Continue For
                c = Vector128.Add(c, Vector128.Multiply(Simd.Leer(pos, p),
                                                        Vector128.Create(MarcoPeso(i))))
            Next
            est.CentroidePonderado = c

            ' ---- (2) COVARIANZA — `0x141A672F0`, el kernel escalar, leído entero.
            ' ⛔ Las tres filas arrancan en `FLT_EPSILON` (`0x142F3C760` → xmm6 en `0x141A6731C`, y
            ' `0x141A67328`/`30`/`39` lo copian a xmm7, xmm8 y xmm9).
            ' ⭐ QUÉ HACE, MEDIDO (GVE1 y GVE2): con la covarianza DEGENERADA —los `frameVector` en
            ' cero— lo que le llega a la descomposición polar es esta matriz de `eps`, de rango 1, y
            ' el marco sale con suma de |entradas| = 2,2499998. Arrancando las filas en cero, el
            ' marco queda NULO. O sea: es lo que evita que el marco colapse cuando la covarianza no
            ' aporta nada. (Antes acá decía «se transcribe porque está ahí»: ahora está medido.)
            ' ⛔⛔ Y EL AGRUPAMIENTO ES `(fv · d) · w`, NO `fv · (d · w)`: `0x141A673E0` multiplica el
            ' `frameVector` difundido por `d` y recién `0x141A673E3` por el peso. El producto flotante
            ' no es asociativo, así que los dos agrupamientos dan bits distintos.
            Dim eps = Vector128.Create(Simd.FltEpsilon)
            Dim a As Mat3
            a.F0 = eps : a.F1 = eps : a.F2 = eps
            For i = 0 To MarcoParticula.Length - 1
                Dim p = MarcoParticula(i)
                If p < 0 OrElse p >= inst.NumParticulas Then Continue For
                Dim d = Vector128.Subtract(Simd.Leer(pos, p), c)          ' 0x141A673AC
                Dim w = Vector128.Create(MarcoPeso(i))                    ' 0x141A673BE/C7
                a.F0 = Vector128.Add(a.F0, Vector128.Multiply(
                    Vector128.Multiply(Vector128.Create(MarcoVector(i * 4)), d), w))      ' 0x141A673E0/E3/E6
                a.F1 = Vector128.Add(a.F1, Vector128.Multiply(
                    Vector128.Multiply(Vector128.Create(MarcoVector(i * 4 + 1)), d), w))  ' 0x141A673F3/F6/F9
                a.F2 = Vector128.Add(a.F2, Vector128.Multiply(
                    Vector128.Multiply(Vector128.Create(MarcoVector(i * 4 + 2)), d), w))  ' 0x141A67407/0A/0D
            Next

            ' el marco, por descomposición polar. ⛔ `BasePropia` entra como SEMILLA y sale
            ' actualizada: es el `r14+0x50` que `0x141A0A630` le pasa al cierre.
            est.Marco = Polar.FactorOrtogonal(a, est.BasePropia)
            est.BaseSembrada = True

            ' ---- (3) APLICAR — `k` difundido a las 4 lanes (`0x141A0A65E shufps 0`)
            Dim kEsc = Vector128.Create(Simd.Lane0(k))
            For i = 0 To AplicarParticula.Length - 1
                Dim p = AplicarParticula(i)
                If p < 0 OrElse p >= inst.NumParticulas Then Continue For
                ' el punto que el marco dicta: centroide + frameVector · marco
                Dim fv = Vector128.Create(AplicarVector(i * 4), AplicarVector(i * 4 + 1),
                                          AplicarVector(i * 4 + 2), 0.0F)
                Dim objetivo = Vector128.Add(c, Polar.FilaPor(fv, est.Marco))
                Dim actual = Simd.Leer(pos, p)
                Dim s = Vector128.Multiply(kEsc, Vector128.Create(AplicarRigidez(i)))
                Simd.Escribir(pos, p, Vector128.Add(actual,
                    Vector128.Multiply(Vector128.Subtract(objetivo, actual), s)))
            Next
        End Sub

    End Class

End Namespace

