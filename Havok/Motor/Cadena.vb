Option Strict On
Option Explicit On

Imports System.Linq
Imports System.Runtime.Intrinsics

' =================================================================================================
' LA CADENA DE OPERADORES — `hclClothState.operators`, en el orden del archivo.
'
' ⛔⛔ NO ES UNA SECUENCIA FIJA. El motor recorre `hclClothState.operators` (+0x18, un arreglo de
' `uint32` con el ÍNDICE del operador en `hclClothData.operators`) y despacha por el `type` de cada
' uno. Una receta escrita a mano —skin, mover, simular, deformar— coincide con las 447 prendas de la
' cadena más común y falla en las otras cinco formas que el corpus trae:
'
'     447  Simulate :: ObjectSpaceSkinPN -> MoveParticles -> Simulate -> SimpleMeshBoneDeform
'     320  Animate  :: ObjectSpaceSkinPN -> CopyVertices -> SimpleMeshBoneDeform
'     127  Animate  :: ObjectSpaceSkinPN -> GatherAllVertices -> SimpleMeshBoneDeform
'       2  Animate  :: ObjectSpaceSkinPN -> ObjectSpaceSkinPN -> GatherSomeVertices -> GatherAll… -> Deform
'       2  Simulate :: ObjectSpaceSkinPN -> ObjectSpaceSkinPN -> MoveParticles -> Simulate -> Deform
'       2  Default  :: ObjectSpaceSkinPN -> MoveParticles -> Simulate -> SimpleMeshBoneDeform
'
' ⛔ El despacho es por CLASE y no por `hclOperator.type` (+0x18): ese campo está marcado
' `SERIALIZE_IGNORED` y viene en CERO en las 3.798 apariciones del corpus — lo asigna el constructor
' en runtime. Los `type` se citan igual, porque son la identidad del operador aunque el archivo no
' los traiga.
'
' ⭐⭐ Y LA TABLA DE `type` NO SALE DE UNA NOTA: SALE DEL DESPACHADOR `0x1418C5DE0`.
'
'     r10d = [rcx + 0x18]                  ' hclOperator.type
'     rbx  = [rdx + 0x10]                  ' los buffers vivos del contexto
'     si (type − 1) > 0x20: assert «Unknown operator …» (`0x1426B4B10`)
'     salta por la tabla de 33 RVAs de `0x1418C6390`, relativas a `0x140000000`
'
'      1 Simulate                 `0x14195C350`   ·  2 GatherAllVertices    `0x1418F9450`
'      3 GatherSomeVertices       `0x1418F9E80`   ·  4 CopyVertices         `0x1418FA860`
'      5 MeshMeshDeform           `0x1419529F0`   ·  8 SkinOperator         `0x141909760`
'     10 BlendSomeVertices        `0x1419516A0`   · 11 MoveParticles        `0x141952460`
'     12 UpdateAllVertexFrames    `0x1418FB3B0`   · 13 UpdateSomeVertexFrames `0x141902600`
'     14 InputConvert             `0x14195E540`   · 15 OutputConvert        `0x14195EA20`
'     16 MeshBoneDeform           `0x14195AA90`   · 17 SimpleMeshBoneDeform `0x14195B320`
'     18-21 BoneSpaceSkin P/PN/PNT/PNTB          · 22-25 ObjectSpaceSkin P/PN/PNT/PNTB
'     26-29 BoneSpaceMeshMeshDeform P/PN/PNT/PNTB · 30-33 ObjectSpaceMeshMeshDeform P/PN/PNT/PNTB
'     6, 7 y 9 caen en el assert: no existen.
'
' ⛔⛔ ESTO CORRIGIO CINCO DE LOS SIETE que este archivo tenia escritos: `MoveParticles` decia 2 y
' es 11, `CopyVertices` decia 3 y es 4, `GatherAllVertices` decia 4 y es 2, `GatherSomeVertices`
' decia 5 y es 3, y `SimpleMeshBoneDeform` decia 16 y es 17 (el 16 es `hclMeshBoneDeform`, que es
' OTRA clase). Ninguno cambiaba un pixel —el despacho es por clase— pero se publicaban en el log y
' en `--motorcenso` como «el type que el .exe le pone», y eso era falso.
'
' El censo del corpus (M1, 759 prendas) dice que estas SIETE clases son todo lo que hay:
'   23 hclObjectSpaceSkinPNOperator (762) · 1 hclSimulateOperator (759) ·
'   hclMoveParticlesOperator (759) · 16→hclSimpleMeshBoneDeformOperator (759) ·
'   hclCopyVerticesOperator (582) · hclGatherAllVerticesOperator (174) ·
'   hclGatherSomeVerticesOperator (3).
' Cualquier otra clase deja su lugar VACÍO y se dice en el log; no se aproxima con otra.
' =================================================================================================

Namespace Havok.Motor

    ''' <summary>Lo que un operador de la cadena necesita del cuadro.</summary>
    Friend Structure ContextoDeCadena
        ''' <summary>Los buffers vivos — `hclClothInstance.buffers` (+0x30).</summary>
        Friend Buffers As Buffer()
        ''' <summary>`hclClothInstance.transformSets` (+0x40).</summary>
        Friend TransformSets As Mat4()()
        ''' <summary>La instancia de tela que el `hclSimulateOperator` y `MoveParticles` tocan.</summary>
        Friend Instancia As Instancia
        ''' <summary>Los colisionables vivos de esa instancia.</summary>
        Friend Colisionadores As Colisionable()
        ''' <summary>Lo que el paso de simulación necesita y no vive en la instancia.</summary>
        Friend Cuadro As EntradaDelCuadro
    End Structure

    ''' <summary>Un operador de `hclClothState.operators`, ya compilado.</summary>
    Friend MustInherit Class OperadorCompilado

        ''' <summary>El `type` que el `.exe` le pone a esta clase en su constructor (cap. 2.1).
        ''' ⛔ NO es el campo del archivo: ese viene en cero.</summary>
        Friend ReadOnly Tipo As Integer

        ''' <summary>El `name` del objeto, para el log y los gates.</summary>
        Friend ReadOnly Nombre As String

        Protected Sub New(tipo As Integer, nombre As String)
            Me.Tipo = tipo
            Me.Nombre = nombre
        End Sub

        ''' <summary>
        ''' El `prepare` del operador — slot `+0x30` de su vtable. Por defecto no hace nada.
        ''' <para>⛔⛔ CORREN TODOS ANTES QUE TODOS LOS `execute`: `0x1418C8E60`-`8C` recorre los
        ''' operadores del estado llamando al `+0x30` de cada uno, dentro de `0x1418C8B70`, que
        ''' los ejecutores invocan ANTES del lazo de `execute` (motor-103).</para>
        ''' </summary>
        Friend Overridable Sub Preparar(ByRef ctx As ContextoDeCadena)
        End Sub

        Friend MustOverride Sub Ejecutar(ByRef ctx As ContextoDeCadena)

    End Class

    ''' <summary>
    ''' `hclObjectSpaceSkin*Operator` — types **22 a 25** (`P`, `PN`, `PNT`, `PNTB`), con la
    ''' entrada compartida `0x1418C6134` en la tabla de `0x1418C6390`.
    ''' <para>⛔ El `type` sale de la VARIANTE, no fijo en 23: el operador que corre es el que el
    ''' archivo declara, y el numero es su identidad.</para>
    ''' </summary>
    Friend NotInheritable Class OpPiel
        Inherits OperadorCompilado

        ''' <summary>La piel compilada. La lee `Cobertura` para medir que cubre el buffer.</summary>
        Friend ReadOnly Piel As PielCompilada

        Friend Sub New(piel As PielCompilada, nombre As String)
            ' 22 = P, 23 = PN, 24 = PNT, 25 = PNTB — la tabla de `0x1418C6390`
            MyBase.New(21 + Math.Max(1, Math.Min(4, piel.Canales)), nombre)
            Me.Piel = piel
        End Sub

        Friend Overrides Sub Ejecutar(ByRef ctx As ContextoDeCadena)
            Havok.Motor.Piel.Ejecutar(Piel, ctx.Buffers, ctx.TransformSets)
        End Sub

    End Class

    ''' <summary>`hclSimulateOperator` — type 1, `0x14195C350`. El cuadro entero de física.</summary>
    Friend NotInheritable Class OpSimular
        Inherits OperadorCompilado

        ''' <summary>`op.subSteps` (+0x24). Sólo se usa si `info.subSteps` es 0 (`0x14195C6A8`).
        ''' <para>'La cita decia +0x28, que es `numberOfSolveIterations`. El codigo leia bien
        ''' (`oSim.SubSteps`, por la reflexion); la que estaba corrida era la direccion. El binario
        ''' lo confirma: `0x14195C6B6 mov eax, dword ptr [r15 + 0x24]`.</para></summary>
        Friend ReadOnly SubSteps As Integer

        ''' <summary>`op.numberOfSolveIterations` (+0x28) — el LAZO EXTERNO del solve.
        ''' <para>'' NO es un adorno: el paso 4e entero va adentro de este lazo, en las DOS
        ''' ramas (`0x141A134F7`/`0x141A135B1` sin `constraintExecution`, `0x141A13756`/`0x141A13825`
        ''' con). Y la colision va adentro tambien, asi que con N iteraciones la tela colisiona N
        ''' veces por substep, no una.</para>
        ''' <para>' EN CERO NO CORRE NADA — ni sets ni colision (`jle` a la salida). Se transcribe
        ''' tal cual: poner un minimo de 1 seria inventar. El corpus vanilla mide 1 en los 342.</para>
        ''' </summary>
        Friend ReadOnly IteracionesDeSolve As Integer

        ''' <summary>`op.adaptConstraintStiffness` (+0x40) — PROMUEVE el modo 1 a 2.
        ''' <para>`0x141A1349B` (sin `constraintExecution`) y `0x141A136F1` (con): si el modo de la
        ''' instancia es 1 y esto esta prendido, el modo pasa a 2 **antes** de calcular `k`
        ''' (`0x141A13538 mov edx, ebp`) y `usaK` (`0x141A134BC`). Cambia las dos.</para></summary>
        Friend ReadOnly AdaptaRigidez As Boolean

        ''' <summary>`op.constraintExecution` (+0x30, cuenta en +0x38) — el orden EXPLICITO.
        ''' <para>Vacia, el motor recorre `staticConstraintSets` en orden y colisiona al final
        ''' (`0x141A133E0`). No vacia, manda esta lista (`0x141A13650`), y **`-1` significa
        ''' «aca va la colision»** (`0x141A13798`): sirve para INTERCALAR colision entre sets.</para>
        ''' </summary>
        Friend ReadOnly EjecucionDeRestricciones As Integer()

        ''' <summary>`op.simClothIndex` (+0x20). El motor lo usa literal:
        ''' `simCloth = clothInstance.simCloths[op.simClothIndex]` (`0x14195C3E0`).</summary>
        Friend ReadOnly IndiceDelSimCloth As Integer

        Friend Sub New(subSteps As Integer, indiceDelSimCloth As Integer,
                       iteracionesDeSolve As Integer, adaptaRigidez As Boolean,
                       ejecucionDeRestricciones As Integer(), nombre As String)
            MyBase.New(1, nombre)
            ' ⛔ `Me.` EN LOS SEIS. VB no distingue mayusculas: `SubSteps = subSteps` sin `Me.`
            ' resuelve los dos lados al PARAMETRO y no asigna nada, en silencio.
            Me.SubSteps = subSteps
            Me.IndiceDelSimCloth = indiceDelSimCloth
            Me.IteracionesDeSolve = iteracionesDeSolve
            Me.AdaptaRigidez = adaptaRigidez
            Me.EjecucionDeRestricciones = ejecucionDeRestricciones
        End Sub

        ''' <summary>
        ''' El `prepare` del `hclSimulateOperator` — slot `+0x30` de su vtable (`0x142704338`).
        ''' <para>⛔⛔ CORRE ANTES QUE TODOS LOS `execute`, no adentro del suyo. `0x1418C8E60`-`8C`
        ''' recorre los operadores del estado y llama al `+0x30` de cada uno, y ese bucle esta
        ''' dentro de `0x1418C8B70`, que los ejecutores invocan ANTES del lazo de `execute`.
        ''' Tenerlo adentro de `Simular` hacia que `ReescalarPrevias` corriera despues de que
        ''' `MoveParticles` ya habia movido las previas (motor-103).</para>
        ''' </summary>
        Friend Overrides Sub Preparar(ByRef ctx As ContextoDeCadena)
            If ctx.Instancia Is Nothing Then Return
            Dim e = ctx.Cuadro
            Dim n = Motor.SubStepsDelCuadro(ctx.Instancia, SubSteps)
            ' ⛔ la siembra del transform de transferencia va acá adentro, con la señal del motor
            ' (`dtSubCacheado == 0`, `0x14195B827`) y su puerta `+0x1E` (`0x14195B850`) — motor-125.
            Tiempo.Preparar(ctx.Instancia, e.Dt, n, e.DampingPorSegundo,
                            e.TransferenciaHabilitada, e.TransformDeTransferencia)
        End Sub

        Friend Overrides Sub Ejecutar(ByRef ctx As ContextoDeCadena)
            If ctx.Instancia Is Nothing Then Return
            ' ⛔ el `subSteps` del OPERADOR viaja en la entrada; el de `simulationInfo` gana adentro
            ' de `Simular`, que es donde el motor decide (`0x14195C6A8`).
            Dim e = ctx.Cuadro
            e.SubStepsDelOperador = SubSteps
            e.IteracionesDeSolve = IteracionesDeSolve
            e.AdaptaRigidez = AdaptaRigidez
            e.EjecucionDeRestricciones = EjecucionDeRestricciones
            e.Buffers = ctx.Buffers
            e.TransformSets = ctx.TransformSets
            Motor.Simular(ctx.Instancia, ctx.Colisionadores, e)
        End Sub

    End Class

    ''' <summary>`hclMoveParticlesOperator` — `0x1418C6134` lo resuelve con `refBufferIdx`.</summary>
    Friend NotInheritable Class OpMoverParticulas
        Inherits OperadorCompilado

        Private ReadOnly _bufferDeReferencia As Integer

        ''' <summary>El puente vertice-particula. Lo lee `Cobertura` para medir que toda ancla lo
        ''' tenga: sin entrada en el puente, el ancla no recibe la pose de la piel.</summary>
        Friend ReadOnly Pares As Integer()()

        Friend Sub New(bufferDeReferencia As Integer, pares As Integer()(), nombre As String)
            MyBase.New(11, nombre)
            _bufferDeReferencia = bufferDeReferencia
            Me.Pares = pares
        End Sub

        Friend Overrides Sub Ejecutar(ByRef ctx As ContextoDeCadena)
            If ctx.Instancia Is Nothing OrElse Pares Is Nothing Then Return
            Operadores.MoverParticulas(ctx.Instancia,
                                       Buffers.Real(ctx.Buffers, _bufferDeReferencia), Pares)
        End Sub

    End Class

    ''' <summary>`hclSimpleMeshBoneDeformOperator` — `0x1418C6302`: `real = buffers[…inputBufferIdx…]`,
    ''' `ts = transformSets[op.outputTransformSetIdx]`.</summary>
    Friend NotInheritable Class OpDeformar
        Inherits OperadorCompilado

        ''' <summary>El buffer de simulación del que salen los triángulos. **Sólo lectura**, para
        ''' que el arnés pueda medir el marco de un cloth-bone concreto (motor-84).</summary>
        Friend ReadOnly BufferDeEntrada As Integer

        Private ReadOnly _bufferDeEntrada As Integer

        ''' <summary>`op.outputTransformSetIdx` (+0x24) — donde el deform deja la pose de los
        ''' cloth-bones. Lo lee el cableado para escribir la capa de fisica.</summary>
        Friend ReadOnly TransformSetDeSalida As Integer
        ''' <summary>Los `triangleBonePairs` `{boneOffset, triangleOffset}`. **Sólo lectura**, para
        ''' el diagnóstico del arnés.</summary>
        Friend ReadOnly Pares As Integer()()

        Private ReadOnly _pares As Integer()()
        Private ReadOnly _localBoneTransforms As Mat4()

        ''' <summary>
        ''' ⭐ Los huesos a los que este deform ESCRIBE, derivados de sus propios
        ''' `triangleBonePairs`: el kernel hace `transforms(boneOffset \ 64) = …` y `boneOffset` viene
        ''' en BYTES, 64 por hueso.
        ''' <para>⛔ Los demas huesos del `transformSet` el deform NO los toca, y por lo tanto **no
        ''' tienen capa de fisica**. Escribirsela igual (aunque sea la identidad) es inventar.</para>
        ''' </summary>
        Friend ReadOnly HuesosQueEscribe As Integer()

        Friend Sub New(bufferDeEntrada As Integer, transformSetDeSalida As Integer,
                       pares As Integer()(), localBoneTransforms As Mat4(), nombre As String)
            MyBase.New(17, nombre)
            _bufferDeEntrada = bufferDeEntrada
            Me.BufferDeEntrada = bufferDeEntrada
            Me.TransformSetDeSalida = transformSetDeSalida
            _pares = pares
            Me.Pares = pares
            _localBoneTransforms = localBoneTransforms
            Dim vistos As New HashSet(Of Integer)()
            If pares IsNot Nothing Then
                For k = 0 To pares.Length - 1
                    If pares(k) IsNot Nothing AndAlso pares(k).Length > 0 Then vistos.Add(pares(k)(0) \ 64)
                Next
            End If
            HuesosQueEscribe = vistos.ToArray()
            Array.Sort(HuesosQueEscribe)
        End Sub

        Friend Overrides Sub Ejecutar(ByRef ctx As ContextoDeCadena)
            If _pares Is Nothing OrElse ctx.TransformSets Is Nothing Then Return
            If TransformSetDeSalida < 0 OrElse TransformSetDeSalida >= ctx.TransformSets.Length Then Return
            Dim buf = Buffers.Real(ctx.Buffers, _bufferDeEntrada)
            Operadores.DeformarHuesosSimple(buf, ctx.TransformSets(TransformSetDeSalida),
                                            _pares, _localBoneTransforms)
        End Sub

    End Class

    ''' <summary>`hclCopyVerticesOperator` — type 4, `0x1418FA860`.</summary>
    Friend NotInheritable Class OpCopiarVertices
        Inherits OperadorCompilado

        Private ReadOnly _entrada As Integer, _salida As Integer
        Private ReadOnly _inicioEntrada As Integer, _inicioSalida As Integer
        Private ReadOnly _numVertices As Integer, _copiarNormales As Boolean

        Friend Sub New(entrada As Integer, salida As Integer, inicioEntrada As Integer,
                       inicioSalida As Integer, numVertices As Integer,
                       copiarNormales As Boolean, nombre As String)
            MyBase.New(4, nombre)
            _entrada = entrada : _salida = salida
            _inicioEntrada = inicioEntrada : _inicioSalida = inicioSalida
            _numVertices = numVertices : _copiarNormales = copiarNormales
        End Sub

        Friend Overrides Sub Ejecutar(ByRef ctx As ContextoDeCadena)
            Operadores.CopiarVertices(Buffers.Real(ctx.Buffers, _entrada),
                                      Buffers.Real(ctx.Buffers, _salida),
                                      _inicioEntrada, _inicioSalida, _numVertices, _copiarNormales)
        End Sub

    End Class

    ''' <summary>
    ''' `hclMeshMeshDeformOperator` — type **5**, `0x1419529F0` («TtMesh Mesh Deform»).
    ''' <para>⛔ Los DOS buffers entran con **doble indirección** (`0x1418C5EEF`-`0x1418C5F18`:
    ''' `buffers[ buffers[idx][+0x100] ]`), como `LocalRange` y a diferencia de `Convertir`.</para>
    ''' <para>La cadena es: marcos de triángulo → al espacio de salida → deform. Ver
    ''' <see cref="MallaAMallaPorPares"/>.</para>
    ''' </summary>
    Friend NotInheritable Class OpMallaAMallaPorPares
        Inherits OperadorCompilado

        Private ReadOnly _op As MallaAMallaPorParesCompilada

        Friend Sub New(op As MallaAMallaPorParesCompilada, nombre As String)
            MyBase.New(5, nombre)
            _op = op
        End Sub

        Friend Overrides Sub Ejecutar(ByRef ctx As ContextoDeCadena)
            If _op Is Nothing Then Return
            Dim entrada = Buffers.Real(ctx.Buffers, _op.BufferDeEntrada)
            Dim salida = Buffers.Real(ctx.Buffers, _op.BufferDeSalida)
            If entrada Is Nothing OrElse salida Is Nothing Then Return
            ' ⛔ LA SALIDA TEMPRANA DEL KERNEL — 0x141952A6A. Con el buffer de entrada declarando
            ' cero triángulos el operador no toca la salida, tenga subconjunto o no.
            If Not MallaAMallaPorPares.HayQueDeformar(entrada) Then Return
            Dim marcos = MallaAMallaPorPares.Marcos(_op, entrada)     ' 0x141952B22/29/30
            MallaAMallaPorPares.AEspacioDeSalida(marcos, entrada, salida)   ' 0x141952B35-CCD
            MallaAMallaPorPares.Deformar(_op, marcos, salida)         ' 0x141952E38-6F
        End Sub

    End Class

    ''' <summary>`hclGatherAllVerticesOperator` — type 2, `0x1418F9450`.</summary>
    Friend NotInheritable Class OpJuntarTodos
        Inherits OperadorCompilado

        Private ReadOnly _entrada As Integer, _salida As Integer
        Private ReadOnly _entradaDesdeSalida As Short()
        Private ReadOnly _juntarNormales As Boolean

        Friend Sub New(entrada As Integer, salida As Integer, entradaDesdeSalida As Short(),
                       juntarNormales As Boolean, nombre As String)
            MyBase.New(2, nombre)
            _entrada = entrada : _salida = salida
            _entradaDesdeSalida = entradaDesdeSalida
            _juntarNormales = juntarNormales
        End Sub

        Friend Overrides Sub Ejecutar(ByRef ctx As ContextoDeCadena)
            If _entradaDesdeSalida Is Nothing Then Return
            Operadores.JuntarTodos(Buffers.Real(ctx.Buffers, _entrada),
                                   Buffers.Real(ctx.Buffers, _salida),
                                   _entradaDesdeSalida, _juntarNormales)
        End Sub

    End Class

    ''' <summary>`hclGatherSomeVerticesOperator` — type 3, `0x1418F9E80`.</summary>
    Friend NotInheritable Class OpJuntarAlgunos
        Inherits OperadorCompilado

        Private ReadOnly _entrada As Integer, _salida As Integer
        Private ReadOnly _pares As Integer()()
        Private ReadOnly _juntarNormales As Boolean

        Friend Sub New(entrada As Integer, salida As Integer, pares As Integer()(),
                       juntarNormales As Boolean, nombre As String)
            MyBase.New(3, nombre)
            _entrada = entrada : _salida = salida
            _pares = pares
            _juntarNormales = juntarNormales
        End Sub

        Friend Overrides Sub Ejecutar(ByRef ctx As ContextoDeCadena)
            If _pares Is Nothing Then Return
            Operadores.JuntarAlgunos(Buffers.Real(ctx.Buffers, _entrada),
                                     Buffers.Real(ctx.Buffers, _salida),
                                     _pares, _juntarNormales)
        End Sub

    End Class

    Friend Module Cadena

        ''' <summary>
        ''' Corre la cadena en el orden del archivo. Un hueco (clase sin transcribir) se saltea y ya
        ''' se dijo al compilarla.
        ''' </summary>
        Friend Sub Ejecutar(ops As OperadorCompilado(), ByRef ctx As ContextoDeCadena)
            If ops Is Nothing Then Return
            ' ⛔⛔ TODOS LOS `prepare` PRIMERO — 0x1418C8E60-8C, dentro de 0x1418C8B70, que los
            ' dos ejecutores llaman ANTES del lazo de `execute` (0x1418BF075, 0x1418F3054).
            For i = 0 To ops.Length - 1
                If ops(i) Is Nothing Then Continue For
                ops(i).Preparar(ctx)
            Next
            For i = 0 To ops.Length - 1
                If ops(i) Is Nothing Then Continue For
                ops(i).Ejecutar(ctx)
            Next
        End Sub

    End Module

End Namespace

