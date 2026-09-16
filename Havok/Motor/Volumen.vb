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

        ''' <summary>Cuántas de las entradas de marco / de aplicar vinieron en LOTES (múltiplo de 16,
        ''' `hclVolumeConstraintMx+0x28`/`+0x48` × 16). Las de después son las sueltas.</summary>
        Friend ReadOnly MarcoLotes As Integer
        Friend ReadOnly AplicarLotes As Integer

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
                    ExigirLoteDe16(b.FrameVector?.Count, b.ParticleIndex?.Count, b.Weight?.Count, "frameBatchData")
                    For i = 0 To 15
                        VolcarUno(mv, b.FrameVector(i))
                        mp.Add(b.ParticleIndex(i))
                        mw.Add(b.Weight(i))
                    Next
                Next
            End If
            MarcoLotes = mp.Count
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
                    ExigirLoteDe16(b.FrameVector?.Count, b.ParticleIndex?.Count, b.Stiffness?.Count, "applyBatchData")
                    For i = 0 To 15
                        VolcarUno(av, b.FrameVector(i))
                        ap.Add(b.ParticleIndex(i))
                        ar.Add(b.Stiffness(i))
                    Next
                Next
            End If
            AplicarLotes = ap.Count
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
                                        peso As IList(Of Single), rigidez As IList(Of Single),
                                        Optional nLotes As Integer = 0) As Volumen
            Return New Volumen(frameVector, particula, peso, rigidez, nLotes)
        End Function

        ''' <summary>`nLotes`: las primeras `16·nLotes` entradas van en lotes, en las dos listas.</summary>
        Private Sub New(frameVector As IList(Of Single()), particula As IList(Of Integer),
                        peso As IList(Of Single), rigidez As IList(Of Single), nLotes As Integer)
            MyBase.New(17, "(prueba)")
            If nLotes < 0 OrElse 16 * nLotes > particula.Count Then
                Throw New ArgumentOutOfRangeException(NameOf(nLotes))
            End If
            MarcoLotes = 16 * nLotes
            AplicarLotes = 16 * nLotes
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

        ''' <summary>Un lote es `T[16]` por reflexión (`0x160` B, leído por `0x141A0AB60`/`ACC0`/`A890`
        ''' sin cuenta propia). Uno que no traiga 16 no es un lote que el motor sepa leer.</summary>
        Private Shared Sub ExigirLoteDe16(nVec As Integer?, nIdx As Integer?, nEsc As Integer?, que As String)
            If nVec.GetValueOrDefault() <> 16 OrElse nIdx.GetValueOrDefault() <> 16 OrElse nEsc.GetValueOrDefault() <> 16 Then
                Throw New InvalidOperationException(
                    $"Volumen: {que} con {nVec}/{nIdx}/{nEsc} entradas; el lote del motor es de 16.")
            End If
        End Sub

        Private Shared Sub VolcarUno(destino As List(Of Single), v As Single())
            For k = 0 To 3
                destino.Add(If(v Is Nothing OrElse k >= v.Length, 0.0F, v(k)))
            Next
        End Sub

        ''' <summary>
        ''' ⛔ El `k` de este set NO corta en cero como el de los enlaces: `0x141A0A4EF comiss` +
        ''' `jbe 0x141A0A6D9` sale con `k <= 0`, que es lo mismo, pero la base ya lo hace.
        ''' <para>⛔⛔ **Los lotes NO son sólo empaquetado.** Un lote de `0x160` B son 4 grupos de 4
        ''' lanes (`frameVector @0x00+0x10·j`, `particleIndex u16 @0x100+2·j`, `weight/stiffness
        ''' @0x120+4·j`), y los tres kernels de lotes llevan UN ACUMULADOR POR LANE que recién se
        ''' combinan al final. En `float` esa suma no es la suma en fila de los sueltos: medido contra
        ''' la emulación de `0x141A0A4D0` (GDFr4).</para>
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
            Dim cero = Vector128(Of Single).Zero
            Dim nMarco = MarcoParticula.Length, nAplicar = AplicarParticula.Length

            ' ---- (1) CENTROIDE PONDERADO. `estado+0x40 = 0` (`0x141A0A5AA`).
            Dim c = cero
            If MarcoLotes > 0 Then
                ' `0x141A0AB60`: cuatro acumuladores en cero (`0x141A0AB7B`-`87`), uno por lane, que
                ' cruzan todos los lotes; `Sj += w_j · P[idx_j]` (`0x141A0AC43`/`4F`/`57`/`5A`), y el
                ' cierre `(S2 + S3) + (S1 + S0)` (`0x141A0AC75`/`7D`/`8A`) se ESCRIBE (`0x141A0AC8D`).
                Dim sLane(3) As Vector128(Of Single)
                For e = 0 To MarcoLotes - 1
                    Dim j = e And 3
                    sLane(j) = Vector128.Add(sLane(j), Vector128.Multiply(
                        Vector128.Create(MarcoPeso(e)), LeerParticula(inst, MarcoParticula(e))))
                Next
                c = Vector128.Add(Vector128.Add(sLane(2), sLane(3)), Vector128.Add(sLane(1), sLane(0)))
            End If
            If nMarco > MarcoLotes Then
                ' `0x141A675C0`: la suma de los sueltos arranca en cero (`0x141A675C3`) y se le SUMA a
                ' lo que dejaron los lotes (`0x141A67601`-`08`).
                Dim sSueltos = cero
                For e = MarcoLotes To nMarco - 1
                    sSueltos = Vector128.Add(sSueltos, Vector128.Multiply(
                        Vector128.Create(MarcoPeso(e)), LeerParticula(inst, MarcoParticula(e))))
                Next
                c = Vector128.Add(c, sSueltos)
            End If
            est.CentroidePonderado = c

            ' ---- (2) COVARIANZA. La matriz del envoltorio arranca en CERO (`0x141A0A5DC`-`F1`) y cada
            ' kernel le suma sus filas con `0x1413606D0` (`filas + A`).
            ' ⛔ Las filas de cada kernel arrancan en `FLT_EPSILON` (`0x142F3C760`). ⭐ MEDIDO (GVE1 y
            ' GVE2): con la covarianza degenerada es lo que evita que el marco colapse.
            ' ⛔⛔ Y el término agrupa `(fv · d)` primero (`0x141A676F0`, `0x141A0AE5E`).
            Dim eps = Vector128.Create(Simd.FltEpsilon)
            Dim a As Mat3
            a.F0 = cero : a.F1 = cero : a.F2 = cero
            If MarcoLotes > 0 Then
                ' `0x141A0ACC0`: DOCE acumuladores —fila × lane—, todos en eps (`0x141A0ACCF`-`AD2D`).
                Dim r(2, 3) As Vector128(Of Single)
                For f = 0 To 2
                    For j = 0 To 3
                        r(f, j) = eps
                    Next
                Next
                For e = 0 To MarcoLotes - 1
                    Dim j = e And 3
                    Dim d = Vector128.Subtract(LeerParticula(inst, MarcoParticula(e)), c)   ' 0x141A0ADE8…AE1B
                    Dim w = Vector128.Create(MarcoPeso(e))
                    For f = 0 To 2
                        r(f, j) = Vector128.Add(r(f, j), Vector128.Multiply(w,
                            Vector128.Multiply(Vector128.Create(MarcoVector(e * 4 + f)), d)))
                    Next
                Next
                ' el cierre por fila: `(r3 + r2) + (r1 + r0)` (`0x141A0B017`-`0x141A0B083`)
                Dim filas As Mat3
                filas.F0 = Vector128.Add(Vector128.Add(r(0, 3), r(0, 2)), Vector128.Add(r(0, 1), r(0, 0)))
                filas.F1 = Vector128.Add(Vector128.Add(r(1, 3), r(1, 2)), Vector128.Add(r(1, 1), r(1, 0)))
                filas.F2 = Vector128.Add(Vector128.Add(r(2, 3), r(2, 2)), Vector128.Add(r(2, 1), r(2, 0)))
                a = SumarFilas(filas, a)                                          ' 0x141A0B0C3
            End If
            If nMarco > MarcoLotes Then
                ' `0x141A67620`: tres filas en eps (`0x141A67636`-`4A`), en fila por entrada.
                Dim filas As Mat3
                filas.F0 = eps : filas.F1 = eps : filas.F2 = eps
                For e = MarcoLotes To nMarco - 1
                    Dim d = Vector128.Subtract(LeerParticula(inst, MarcoParticula(e)), c)   ' 0x141A676BC
                    Dim w = Vector128.Create(MarcoPeso(e))                                   ' 0x141A676D7
                    filas.F0 = Vector128.Add(filas.F0, Vector128.Multiply(
                        Vector128.Multiply(Vector128.Create(MarcoVector(e * 4)), d), w))      ' 0x141A676F0/F3/F6
                    filas.F1 = Vector128.Add(filas.F1, Vector128.Multiply(
                        Vector128.Multiply(Vector128.Create(MarcoVector(e * 4 + 1)), d), w))  ' 0x141A67703/06/09
                    filas.F2 = Vector128.Add(filas.F2, Vector128.Multiply(
                        Vector128.Multiply(Vector128.Create(MarcoVector(e * 4 + 2)), d), w))  ' 0x141A67716/19/1C
                Next
                a = SumarFilas(filas, a)                                          ' 0x141A6775D
            End If

            ' el marco, por descomposición polar (`0x141A0A6F0`). ⛔ `BasePropia` entra como SEMILLA
            ' y sale actualizada: es el `r14+0x50` que `0x141A0A630` le pasa al cierre.
            est.Marco = Polar.FactorOrtogonal(a, est.BasePropia)
            est.BaseSembrada = True
            Dim m = est.Marco

            ' ---- (3) APLICAR — `k` difundido a las 4 lanes (`0x141A0A65E shufps 0`)
            Dim kVec = Vector128.Create(Simd.Lane0(k))
            If AplicarLotes > 0 Then
                ' `0x141A0A890`, por grupo de 4: ⛔ LEE las cuatro posiciones antes de escribir ninguna
                ' (`0x141A0A981`-`990` contra `0x141A0AAB8`-`ACF`), así que dos lanes con la misma
                ' partícula NO se encadenan. Objetivo `((x·M0 + C) + y·M1) + z·M2` (`0x141A0A9E6`…
                ' `0x141A0AA1A`) y `s_j = (rigidez · k)_j` (`0x141A0A9AC`).
                Dim pj(3) As Vector128(Of Single)
                For g = 0 To AplicarLotes \ 4 - 1
                    Dim b = g * 4
                    For j = 0 To 3
                        pj(j) = LeerParticula(inst, AplicarParticula(b + j))
                    Next
                    For j = 0 To 3
                        Dim e = b + j
                        Dim t = Vector128.Add(Vector128.Multiply(Vector128.Create(AplicarVector(e * 4)), m.F0), c)
                        t = Vector128.Add(t, Vector128.Multiply(Vector128.Create(AplicarVector(e * 4 + 1)), m.F1))
                        t = Vector128.Add(t, Vector128.Multiply(Vector128.Create(AplicarVector(e * 4 + 2)), m.F2))
                        Dim sj = Vector128.Multiply(Vector128.Create(AplicarRigidez(e)), kVec)
                        Simd.Escribir(pos, AplicarParticula(e),
                                      Vector128.Add(pj(j), Vector128.Multiply(Vector128.Subtract(t, pj(j)), sj)))
                    Next
                Next
            End If
            For e = AplicarLotes To nAplicar - 1
                ' `0x141A67790`: objetivo por `0x141339F90` = `((y·M1 + x·M0) + z·M2) + C`, y
                ' `s = (k₀ · rigidez)` difundido (`0x141A677DB` mulss + `0x141A677ED` shufps 0).
                Dim t = Vector128.Add(Vector128.Multiply(Vector128.Create(AplicarVector(e * 4 + 1)), m.F1),
                                      Vector128.Multiply(Vector128.Create(AplicarVector(e * 4)), m.F0))
                t = Vector128.Add(t, Vector128.Multiply(Vector128.Create(AplicarVector(e * 4 + 2)), m.F2))
                t = Vector128.Add(t, c)
                Dim actual = LeerParticula(inst, AplicarParticula(e))
                Dim s = Vector128.Create(Simd.Lane0(k) * AplicarRigidez(e))
                Simd.Escribir(pos, AplicarParticula(e),
                              Vector128.Add(Vector128.Multiply(s, Vector128.Subtract(t, actual)), actual))
            Next
        End Sub

        ''' <summary>`0x1413606D0`: `destino.Fk = fuente.Fk + destino.Fk`, fila por fila.</summary>
        Private Shared Function SumarFilas(fuente As Mat3, destino As Mat3) As Mat3
            Dim r As Mat3
            r.F0 = Vector128.Add(fuente.F0, destino.F0)
            r.F1 = Vector128.Add(fuente.F1, destino.F1)
            r.F2 = Vector128.Add(fuente.F2, destino.F2)
            Return r
        End Function

        ''' <summary>El motor indexa sin guarda (`[rsi + idx*16]`). Acá un índice fuera de rango
        ''' revienta con mensaje en vez de leer memoria ajena (motor-61).</summary>
        Private Shared Function LeerParticula(inst As Instancia, p As Integer) As Vector128(Of Single)
            If p < 0 OrElse p >= inst.NumParticulas Then
                Throw New InvalidOperationException($"Volumen: particleIndex {p} fuera de [0, {inst.NumParticulas - 1}].")
            End If
            Return Simd.Leer(inst.Posiciones, p)
        End Function

    End Class

End Namespace

