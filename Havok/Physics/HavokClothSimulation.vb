Option Strict On
Option Explicit On

Imports System.Collections.Generic
Imports System.Linq
Imports System.Runtime.CompilerServices
Imports NiflySharp.Blocks
Imports OpenTK.Mathematics

' =================================================================================================
' FÍSICA DE CLOTH DE FALLOUT 4 — réplica del motor, no una aproximación.
'
' El motor no corre un solver propio de Bethesda: corre la **Havok Cloth Library (hcl)** completa
' (versión "gen9"), envuelta por `hclBSWorld` y por el job system de `bhkWorld`. Todo lo de abajo
' está sacado del desensamblado de `Fallout4.exe` (2026-08-18) y medido contra el corpus vanilla.
' El documento con las VAs y el detalle es Tools/re-docs/RE_FO4_CLOTH_PHYSICS.md.
'
' EL BUCLE DEL MOTOR (hclSimulateOperator::execute @0x14195C350):
'
'   dtEff = dt / simCloth.timeScale                       (1.0 salvo escalado de actor)
'   N     = simulationInfo.subSteps ? : hclSimulateOperator.subSteps
'   dtSub = dtEff / N
'
'   [una vez por frame]  "TtDrive Collidables": copia las cápsulas y les fija velocidad desde el
'                        transform-set del esqueleto.
'
'   [por substep]
'     1. integrar colisionables:  pos += linVel·dtSub ; q = normalize((ω·dtSub/2, w) ⊗ q)
'        con w = |v|·cot|v| por polinomio minimax ⇒ MAPA EXPONENCIAL EXACTO, no q += ½ωq·dt.
'     2. anclas: para cada índice de `fixedParticles`,
'           P[idx] = Pprev[idx] = lerp(prevSnapshot, curSnapshot, (substep+1)/N)
'        ⇒ el ancla CAMINA interpolada; no salta al destino. Es lo que evita el latigazo.
'     3. "TtIntegrate": F[]=0 ; cada hclAction acumula ; luego Verlet de posición
'           a     = (mass·gravity + F[i]) · invMass
'           v     = (P[i] − Pprev[i]) · damp
'           P'    = P[i] + v + a·dtSub²
'           damp  = si d>=1 -> 0 ; si d=0 -> 1 ; si no -> (1−d)^dtRef     (d = globalDampingPerSecond)
'     4. "TtSolve": for it in 0..numberOfSolveIterations-1:
'             for cs in staticConstraintSets: cs.solve(...)
'             CollideAndSolve  (contactos contra las cápsulas)
'
' CENSO DEL CORPUS VANILLA (342 hclSimClothData, `HkxLoadOrderAudit --clothengine`):
'   · globalDampingPerSecond ∈ [0,001 ; 0,999999] — NINGUNO llega a 1 ⇒ la rama dura del motor
'     (d>=1 ⇒ sin inercia) no se dispara en vanilla.
'   · gravity.Z: −686,7 (178 = ropa, 1 g a 70 u/m) · −1500 (161 = pelo) · −973,4 (2) · −9,81 (1, outlier
'     authored en m/s²).
'   · collisionTolerance = 13,998 en los 342 (constante).
'   · subSteps de simulationInfo = 0 en los 342 ⇒ SIEMPRE gana el del hclSimulateOperator (1..4).
'   · numberOfSolveIterations = 1 en los 342.
'   · actions authored: 0 ⇒ en vanilla NO hay viento en el archivo; lo pone el motor en runtime.
'
' INTEGRACIÓN CON POSES Y MORPHS — la parte que no se puede romper:
'   El resultado se escribe en `HierarchiBone_class.PhysicsDeltaTransform`, una capa NUEVA que va
'   DESPUÉS de la de pose:   OrigL × Mount × Morph × Delta × **Physics**.
'   · Apagar la física = poner esa capa en Nothing ⇒ el render vuelve a ser bit-idéntico.
'   · No pelea con `ApplyPose` (que escribe Delta) ni borra los morphs (que viven en Morph).
'   · Se escribe como DELTA:  Physics = inv(OrigL×Mount×Morph×Delta) × localDeseado, igual que el mount.
'   · Sólo se tocan los huesos que el `hclSimpleMeshBoneDeformOperator` declara. El resto, nunca.
' =================================================================================================

Namespace Havok.Physics

    ''' <summary>Estado vivo de una prenda entre frames. Se cachea por bloque BSClothExtraData.</summary>
    Friend NotInheritable Class ClothSimState
        Public Positions As Vector3()
        Public Previous As Vector3()
        Public InvMass As Single()
        Public Mass As Single()
        ''' <summary>`particleDatas[i].radius` (+0x08). El motor exige que la particula quede SEPARADA
        ''' del plano de contacto por SU radio, no pegada a la superficie del colisionable.</summary>
        Public Radius As Single()
        ''' <summary>`particleDatas[i].friction` (+0x0C). Escala la componente TANGENCIAL de la
        ''' velocidad Verlet en el contacto.</summary>
        Public Friction As Single()
        ''' <summary>`hclSimClothData.staticCollisionMasks` (+0xF8): UNA mascara por particula, con un
        ''' bit por colisionable. El motor la consulta ANTES de armar el par particula/colisionable
        ''' (<c>test dword [r8], r12d</c> en 0x141A71893), asi que una particula con el bit apagado NO
        ''' colisiona con ese cuerpo por cerca que este. Sin esto la app empujaba particulas que el
        ''' autor dejo pasar a proposito. MEDIDO: declarado en los 1.854 sim-cloth del corpus.</summary>
        Public MascaraColision As UInteger()
        ''' <summary>Normales de la MALLA DE SIMULACION, recalculadas por frame cuando el archivo
        ''' declara `doNormals`. Son las de la tela, no las de la piel.</summary>
        Public NormalesSim As Vector3()
        ''' <summary>`hclSimClothData.doNormals` (+0x14C).</summary>
        Public HaceNormales As Boolean
        ''' <summary>Triangulos de la malla de simulacion, aplanados de a 3 indices de particula.</summary>
        Public TrisSim As Integer()
        ''' <summary>
        ''' `triangleFlips` (+0x68) — ⛔ es un **BITSET**, un BIT por triangulo, no un byte.
        ''' <para>MEDIDO sobre el corpus: en los 1.854 sim-cloth la cuenta del array es EXACTAMENTE
        ''' <c>ceil(triangulos / 8)</c> — 70 entradas para 559 triangulos, 22 para 170 — y en ninguno
        ''' es igual a la cantidad de triangulos. Leerlo como un byte por triangulo hacia que el 87 %
        ''' de los triangulos cayera fuera del array y que los pocos que entraban se leyeran de la
        ''' posicion equivocada. 25 prendas traen algun bit prendido.</para>
        ''' <para>Con el bit puesto, la cara esta enrollada al reves y su normal entra RESTANDO.</para>
        ''' </summary>
        Public FlipsSim As Byte()
        ''' <summary>Normal de cada triangulo de la malla de simulacion EN LA SIEMBRA. Es la referencia
        ''' de signo: un triangulo cuya normal se da vuelta respecto de esta es un agujero.</summary>
        ''' <summary>`antiPinchConstraintSets` (+0xC8), ya aplanados igual que los estaticos.</summary>
        Public BloquesAntiPinch As New List(Of ConstraintBlock)
        ''' <summary>`perParticlePinchDetectionEnabledFlags` (+0x108): un byte por particula.</summary>
        Public PinchPorParticula As Byte()
        ''' <summary>Rango de particulas que el pellizco puede tocar (+0x128/+0x12A).</summary>
        Public PinchMin As Integer
        Public PinchMax As Integer
        ''' <summary>`simulationInfo.pinchDetectionEnabled` (+0x1C del info).</summary>
        Public PinchActivo As Boolean
        ''' <summary>`collidablePinchingDatas` (+0x118), alineado con `Capsules` por indice.</summary>
        Public PinchDeCapsula As HclCollidablePinchingData_Class()
        ''' <summary>Terreno: `landscapeCollisionEnabled`, `landscapeRadius` y cuantas particulas
        ''' participan (`numLandscapeCollidableParticles`, +0x148).</summary>
        Public TerrenoActivo As Boolean
        Public TerrenoRadio As Single
        Public TerrenoParticulas As Integer
        Public TerrenoDetectaEnganche As Boolean
        Public TerrenoFactorEngancheSq As Single
        ''' <summary>Altura del suelo en el mundo. El motor la saca de la geometria del terreno; esta
        ''' app no tiene terreno, asi que usa el punto mas bajo del esqueleto vivo (los pies).</summary>
        Public TerrenoAltura As Single
        ''' <summary>Índices (en el espacio de PARTÍCULA) de las partículas fijas.</summary>
        Public Fixed As Integer()
        Public Gravity As Vector3
        ''' <summary>Factor de inercia por paso, ya elevado: (1−d)^dtSub.</summary>
        Public Damping As Single
        ''' <summary>`hclSimulateOperator.adaptConstraintStiffness` (+0x40). Es lo UNICO serializado
        ''' que decide si `StiffnessFactor` devuelve 1 o la potencia: el otro termino del `and` es
        ''' `simCloth[+0x1CC]`, que el motor inicializa en 1 (`0x1418C66F4`).</summary>
        Public AdaptStiffness As Boolean
        ''' <summary>`dt / subSteps` — el paso de tiempo de UN substep. Es el `k` que el motor le pasa
        ''' a `CollideAndSolve` (0x14195C6C1) y lo unico que ese `k` hace es convertir la velocidad del
        ''' colisionable en el desplazamiento que hizo durante el substep.</summary>
        Public DtSub As Single
        ''' <summary>El transform de cada colisionable tal como quedo el frame anterior, por indice.
        ''' Es lo que el motor tiene guardado en `hclCollidable.transform` cuando entra `Drive
        ''' Collidables`: ese campo es de RUNTIME y arranca el frame con el valor viejo.</summary>
        Public MatricesColPrev As New Dictionary(Of Integer, Matrix4)
        ''' <summary>
        ''' Los buffers de vertices que declara `hclClothData.bufferDefinitions`, en la POSICION del
        ''' archivo. Los operadores se referencian entre si por estos indices — no por orden de
        ''' aparicion ni por clase.
        ''' <para>⛔ El buffer que el `hclSimpleMeshBoneDeformOperator` lee ES el de particulas: su
        ''' `numVertices` coincide con la cantidad de particulas del sim-cloth (321 = 321 en el vestido,
        ''' 113 = 113 en el pelo). Por eso `Buffers(BufSim)` APUNTA al mismo array que `Positions`: lo
        ''' que un `gather`/`copy` escriba ahi ES la posicion de la particula, igual que en el motor.</para>
        ''' </summary>
        Public Buffers As Vector3()()
        Public NormalesBuf As Vector3()()
        ''' <summary>Indice del buffer que es el de particulas (el que lee el deform).</summary>
        Public BufSim As Integer = -1
        ''' <summary>Lo que dejo el ultimo `hclObjectSpaceSkinPNOperator` de la cadena: la malla
        ''' skinneada por vertice y sus normales. Es la referencia de `hclLocalRangeConstraintSet`.</summary>
        Public Skinned As Dictionary(Of Integer, Vector3)
        Public NormalesRef As Dictionary(Of Integer, Vector3)
        ''' <summary>El transform MUNDO del hueso de referencia tal como estaba el frame pasado. Es la
        ''' mitad del `transferMotion`: sin el no hay delta que transferir.</summary>
        Public RefAnterior As Matrix4
        Public TieneRefAnterior As Boolean
        Public SubSteps As Integer = 1
        Public SolveIterations As Integer = 1
        Public Seeded As Boolean = False
        ''' <summary>Transform del hueso raíz en el frame anterior, para detectar teleport.</summary>
        Public LastRootTranslation As Vector3
        Public LastRootForward As Vector3
        Public HasLastRoot As Boolean = False
        Public Links As New List(Of DistanceLink)
        ''' <summary>`hclStretchLinkConstraintSet`. Lista APARTE porque su ley es OTRA, no un
        ''' parametro distinto de la misma: ver <see cref="HavokClothSimulation.SolveStretchLinks"/>.</summary>
        Public Stretch As New List(Of DistanceLink)
        ''' <summary>`hclBendLinkConstraintSet`: un link de RANGO con dos topes y dos rigideces.</summary>
        Public BendLinks As New List(Of RangoLink)
        ''' <summary>`hclCompressibleLinkConstraintSet`: la pareja confinada a [min, max].</summary>
        Public Compressible As New List(Of RangoLink)
        ''' <summary>`hclBonePlanesConstraintSet`: un plano pegado a un hueso por particula.</summary>
        Public BonePlanes As New List(Of BonePlaneConstraint)
        Public LocalRange As New List(Of LocalRangeConstraint)
        Public Bend As New List(Of BendLink)
        ''' <summary>Los constraint sets EN EL ORDEN QUE LOS DECLARA EL ARCHIVO. Ver
        ''' <see cref="ConstraintBlock"/>.</summary>
        Public Blocks As New List(Of ConstraintBlock)
        Public Capsules As New List(Of CapsuleCollider)
        ''' <summary>gatherMap(particleIdx) = VertexIndex del ObjectSpaceSkin.</summary>
        Public GatherMap As UShort()
        ''' <summary>Cuando el mapa viene de MoveParticles es PARCIAL: dice que entradas son validas.</summary>
        Public GatherMapHas As Boolean()
        ''' <summary>DIAGNOSTICO: de donde salio el destino de cada particula. True = de la piel
        ''' skinneada; False = del DefaultClothPose del archivo, que esta en OTRO espacio.</summary>
        Public TargetFromSkin As Boolean()
    End Class

    ''' <summary>Que clase de constraint set es un bloque.</summary>
    Friend Enum ConstraintKind
        Distance
        Stretch
        Bend
        LocalRange
        BendLink
        Compressible
        BonePlane
        ''' <summary>El `-1` de `constraintExecution`: aca va la colision, y va DONDE LO DICE LA LISTA.</summary>
        Colision
    End Enum

    ''' <summary>
    ''' Un constraint set del archivo, como un TRAMO de la lista aplanada que le corresponde.
    '''
    ''' <para>⛔ EXISTE PORQUE EL ORDEN IMPORTA Y LO DECLARA EL ARCHIVO. El motor resuelve
    ''' (<c>TtSolve</c>, 0x141A133E0):</para>
    ''' <code>
    '''     for it in 0 .. numberOfSolveIterations − 1:
    '''         for cs in simClothData.staticConstraintSets:      ' ← EL ORDEN DEL ARCHIVO
    '''             cs->solve(...)
    '''         CollideAndSolve(...)
    ''' </code>
    ''' <para>Este simulador los aplanaba por TIPO y los corria en un orden fijo suyo
    ''' (distancia → bend → correa). MEDIDO sobre `FemaleHair04.nif`, el archivo declara
    ''' <c>Standard → LocalRange → Stretch → Bend</c>: la correa va ENTRE los dos sets de links, no
    ''' despues del bend. Gauss-Seidel no conmuta, asi que ese no es un detalle cosmetico.</para>
    ''' </summary>
    Friend Structure ConstraintBlock
        Public Kind As ConstraintKind
        Public Start As Integer
        Public Count As Integer
        ''' <summary>`hclConstraintSet.type` que el motor le asigna EN RUNTIME a esta clase. No esta
        ''' serializado (viene 0 en los 1.248 sets del corpus): sale del constructor de cada clase,
        ''' que llama al base con el tipo como segundo argumento. Ver <see cref="TipoDeMotor"/>.</summary>
        Public TipoMotor As Integer
    End Structure

    ''' <summary>
    ''' `hclConstraintSet.type`, leido del CONSTRUCTOR de cada clase en el .exe.
    '''
    ''' <para>⛔ NO se puede leer del archivo: igual que `hclOperator.type`, el motor lo asigna en
    ''' runtime y en el corpus viene SIEMPRE 0. La forma de sacarlo es el ctor: cada clase llama al
    ''' base <c>0x1419F7D00</c> con el tipo en <c>edx</c> y despues escribe su vtable, asi que
    ''' emparejando "call al ctor base" con "vtable que se escribe justo despues" sale la tabla
    ''' entera. Verificado sobre los 16 sitios de llamada del binario.</para>
    '''
    ''' <para>Importa porque <c>StiffnessFactor</c> (0x1418C6420) hace un <c>switch</c> sobre el:
    ''' los tipos 5 y 10 solo actuan en el ULTIMO substep, el 8 rampea, y el resto usa la potencia.</para>
    ''' </summary>
    Friend Enum TipoDeMotor
        StandardLink = 1
        StretchLink = 2
        BendLink = 3
        BendStiffness = 4
        LocalRange = 5
        Volume = 6
        Transition = 8
        BonePlanes = 10
        StandardLinkMx = 13
        BendStiffnessMx = 16
        VolumeMx = 17
        AntiPinch = 19
        ''' <summary>⚠️ `hclCompressibleLinkConstraintSet` NO aparece entre los 16 sitios que llaman al
        ''' ctor base, asi que su tipo NO se pudo leer. Cae en la rama por defecto de
        ''' `StiffnessFactor`, que es donde caen tambien Standard/Stretch/Bend; queda anotado como
        ''' DESCONOCIDO en vez de inventarle un numero.</summary>
        Desconocido = 0
    End Enum

    Friend Structure DistanceLink
        Public A As Integer
        Public B As Integer
        Public Rest As Single
        Public Stiffness As Single
    End Structure

    ''' <summary>Un link de <c>hclBendStiffnessConstraintSet</c>: cuatro particulas con sus pesos.
    ''' <para>⛔ NO lleva <c>restCurvature</c> a proposito: el solver del motor NO LO LEE en la rama
    ''' que usa el corpus (ver <see cref="HavokClothSimulation.SolveBend"/>).</para></summary>
    Friend Structure BendLink
        Public A As Integer
        Public B As Integer
        Public C As Integer
        Public D As Integer
        Public WA As Single
        Public WB As Single
        Public WC As Single
        Public WD As Single
        ''' <summary>`bendStiffness` del archivo. En el corpus viene NEGATIVO, y tiene que serlo: el
        ''' paso es `P += S·w·invMass·v`, asi que con S &lt; 0 el empuje va CONTRA la curvatura.</summary>
        Public Stiffness As Single
        ''' <summary>`restCurvature`. Solo lo usa la rama de rest-pose; en la otra el motor NO LO LEE.</summary>
        Public RestCurvature As Single
        ''' <summary>Que ley le toca a ESTE link. Viene del `useRestPoseConfig` de su set.</summary>
        Public UseRestPose As Boolean
    End Structure

    ''' <summary>
    ''' Una constraint de `hclLocalRangeConstraintSet`. ⛔ NO es una correa esferica: el motor confina
    ''' la particula con TRES limites — uno radial y dos sobre el eje de la NORMAL de referencia.
    ''' Ver <see cref="HavokClothSimulation.SolveLocalRange"/>.
    ''' </summary>
    ''' <summary>
    ''' Un link con DOS topes. Lo comparten `hclBendLinkConstraintSet` y
    ''' `hclCompressibleLinkConstraintSet`, que tienen la misma forma de dato pero LEYES DISTINTAS
    ''' — ver los dos solvers.
    ''' </summary>
    Friend Structure RangoLink
        Public A As Integer
        Public B As Integer
        ''' <summary>Tope inferior: `bendMinLength` / `compressionLength`.</summary>
        Public Min As Single
        ''' <summary>Tope superior: `stretchMaxLength` / `restLength`.</summary>
        Public Max As Single
        ''' <summary>Rigidez del tope inferior (`bendStiffness`). En el compresible es la unica.</summary>
        Public StiffMin As Single
        ''' <summary>Rigidez del tope superior (`stretchStiffness`).</summary>
        Public StiffMax As Single
    End Structure

    ''' <summary>Un `hclBonePlanesConstraintSetBonePlane` (32 bytes) ya resuelto a hueso vivo.</summary>
    Friend Structure BonePlaneConstraint
        Public Particle As Integer
        ''' <summary>Nombre del hueso cuya matriz define el plano. Se resuelve por nombre porque la app
        ''' no modela el transform-set del motor: lo aproxima con el esqueleto vivo.</summary>
        Public BoneName As String
        ''' <summary>`planeEquationBone`: (nx, ny, nz) en el espacio DEL HUESO, y w = la distancia.</summary>
        Public Nx As Single
        Public Ny As Single
        Public Nz As Single
        Public D As Single
        Public Stiffness As Single
    End Structure

    Friend Structure LocalRangeConstraint
        Public Particle As Integer
        ''' <summary>Indice de VERTICE de la malla skinneada (NO de particula). Difieren en el 13 % del corpus.</summary>
        Public ReferenceVertex As Integer
        Public MaxDistance As Single
        ''' <summary>`maxNormalDistance` (+0x08). Cuanto puede ALEJARSE por delante de la superficie.</summary>
        Public MaxNormal As Single
        ''' <summary>`minNormalDistance` (+0x0C). Cuanto puede HUNDIRSE por detras.</summary>
        Public MinNormal As Single
        ''' <summary>`stiffness` del SET (+0x34), no de la constraint. Multiplica SOLO al termino radial.</summary>
        Public Stiffness As Single
        ''' <summary>`applyNormalComponent` del SET (+0x3C): si no, los dos limites normales no corren.</summary>
        Public UsaNormal As Boolean
        ''' <summary>`referenceMeshBufferIdx` (+0x30). El motor lo lee en 0x141A01F50 y con el INDEXA
        ''' EL ARRAY DE BUFFERS (0x141A01F5A): la malla de referencia es un buffer de la cadena, no
        ''' "la piel" en abstracto.</summary>
        Public BufferRef As Integer
    End Structure

    Friend Structure CapsuleCollider
        Public A As Vector3
        Public B As Vector3
        ''' <summary>Bit de este colisionable dentro de `staticCollisionMasks`. MEDIDO: el bit i es el
        ''' colisionable i — en las prendas del corpus el OR de todas las mascaras da exactamente
        ''' <c>(1 &lt;&lt; cantidadDeColisionables) − 1</c>, y el array trae UNA entrada por particula.</summary>
        Public Bit As UInteger
        ''' <summary>`hclCollidable.linearVelocity` (+0x60) y `angularVelocity` (+0x70), en el espacio
        ''' del mundo. El motor las usa para la friccion: lo que frena a la particula es la velocidad
        ''' RELATIVA al cuerpo, no su velocidad absoluta. Con el cuerpo quieto son cero y no cambian
        ''' nada; con el cuerpo moviendose, ignorarlas hace que la tela patine sobre una pierna que en
        ''' realidad la esta arrastrando.</summary>
        Public VelLineal As Vector3
        Public VelAngular As Vector3
        ''' <summary>Origen del transform del colisionable: el punto respecto del cual gira.</summary>
        Public Origen As Vector3
        ''' <summary>Los extremos EN EL ESPACIO DEL SHAPE, sin transformar. `A`/`B` son estos mismos
        ''' llevados al mundo por el transform del substep que se este corriendo.</summary>
        Public LocalA As Vector3
        Public LocalB As Vector3
        ''' <summary>El transform del colisionable al empezar el frame (el del frame anterior) y el que
        ''' le toca al terminarlo. El substep camina del primero al segundo.</summary>
        Public MPrev As Matrix4
        Public MCur As Matrix4
        ''' <summary>`hclCollidable.pinchDetectionEnabled/Priority/Radius` (+0x80/+0x81/+0x84). Es la
        ''' fuente que consulta el motor para contar cuantos cuerpos pellizcan a una particula
        ''' (0x141A697F0 recorre los colisionables con stride 0x90 y mira el byte de +0x80).</summary>
        Public PinchHabilitado As Boolean
        Public PinchPrioridad As Integer
        Public PinchRadio As Single
        ''' <summary>Radio en el extremo A (`smallRadius` de la tapered).</summary>
        Public Radius As Single
        ''' <summary>Radio en el extremo B (`bigRadius`). Distinto del A en las 602 tapered del corpus.</summary>
        Public RadiusB As Single
    End Structure

    ''' <summary>El simulador. Estático: el estado vivo va en la tabla débil por bloque.</summary>
    Public NotInheritable Class HavokClothSimulation

        Private Sub New()
        End Sub

        ' Clave DÉBIL por bloque, igual que hace SkeletonClothOverlayHelper con el cloth-skeleton:
        ' cuando la shape se descarga, el estado se evacúa solo por GC. Sin clear manual, sin leak.
        Private Shared ReadOnly _state As New ConditionalWeakTable(Of BSClothExtraData, ClothSimState())
        Private Shared ReadOnly _package As New ConditionalWeakTable(Of BSClothExtraData, HclClothPackageGraph_Class)

        ''' <summary>
        ''' Esqueletos a los que esta simulación le escribió la capa alguna vez. Clave DÉBIL: no los
        ''' mantiene vivos. Sirve para que apagar el interruptor pueda limpiarlos a TODOS de una, sin
        ''' depender de que llegue otro frame de render.
        ''' </summary>
        Private Shared ReadOnly _touched As New ConditionalWeakTable(Of SkeletonInstance, Object)

        ''' <summary>Tira todo el estado vivo: la próxima pasada vuelve a sembrar desde la piel posada.</summary>
        Public Shared Sub ResetAll()
            _state.Clear()
        End Sub

        ''' <summary>Limpia la capa de física en TODO esqueleto que esta simulación haya tocado.</summary>
        Friend Shared Sub ClearAllTouchedSkeletons()
            For Each kv In _touched
                kv.Key?.ResetPhysics()
            Next
            _touched.Clear()
        End Sub

        ''' <summary>
        ''' Corre un paso de física sobre las shapes con `HasPhysics` y escribe la capa
        ''' `PhysicsDeltaTransform` de los cloth-bones. Llamar DESPUÉS de `ApplyPose` y ANTES del skinning.
        ''' <para>Con `HavokPhysicsSettings.Enabled = False` LIMPIA la capa y sale: el render queda
        ''' exactamente como sin este módulo.</para>
        ''' </summary>
        Public Shared Sub StepShapes(shapes As IEnumerable(Of IRenderableShape),
                                     skeleton As SkeletonInstance,
                                     Optional deltaSeconds As Single = -1.0F)
            If skeleton Is Nothing OrElse Not skeleton.HasSkeleton Then Exit Sub

            If Not HavokPhysicsSettings.Enabled OrElse HavokPhysicsSettings.Mode = HavokPhysicsMode.Off Then
                ClearPhysicsLayer(skeleton)
                Exit Sub
            End If
            If shapes Is Nothing Then Exit Sub

            Dim dt = If(deltaSeconds > 0.0F, deltaSeconds, HavokPhysicsSettings.FixedTimeStep)

            ' ⛔ UN PASO POR BLOQUE, no por shape. `IRenderableShape.HasPhysics` es a nivel NIF, así que
            ' TODAS las shapes de un NIF con cloth entran acá, y `ResolveClothBlockForShape` cae al
            ' primer bloque para las que no lo tienen en su ExtraDataList. Sin deduplicar, un NIF de 12
            ' shapes con UN solo BSClothExtraData avanzaba la simulación 12 veces por frame sobre el
            ' MISMO estado: gravedad integrada 12×, damping aplicado 12×, anclas caminando 12 tramos.
            ' MEDIDO en el corpus vanilla: 262 de 309 NIF con cloth tienen más de una shape y
            ' exactamente un bloque (histograma: 11 shapes ×123, 10 ×22, 12 ×18…).
            Dim seen As New HashSet(Of BSClothExtraData)()
            For Each shape In shapes
                If shape Is Nothing OrElse Not shape.HasPhysics OrElse shape.NifContent Is Nothing Then Continue For
                Dim block = SkeletonClothOverlayHelper_Class.ResolveClothBlockForShape(shape)
                If block Is Nothing OrElse Not seen.Add(block) Then Continue For
                Try
                    StepBlock(block, skeleton, dt)
                Catch ex As Exception
                    ' Un fallo de física NO puede tumbar el render, pero TAMPOCO puede ser mudo:
                    ' una prenda que deja de simular sin decir nada es indistinguible de "no tiene física".
                    Dim exL = ex
                    Logger.LogLazy(Function() $"[HAVOK-PHYS] la prenda falló y queda SIN física: {exL.GetType().Name}: {exL.Message}")
                End Try
            Next
        End Sub

        ''' <summary>Pone la capa de física en Nothing en todo el esqueleto (= apagar).</summary>
        Public Shared Sub ClearPhysicsLayer(skeleton As SkeletonInstance)
            If skeleton Is Nothing Then Exit Sub
            ' Delega en el propio SkeletonInstance: la limpieza va bajo su lock, como las otras capas.
            skeleton.ResetPhysics()
        End Sub

        ' -----------------------------------------------------------------------------------------

        Private Shared Sub StepBlock(block As BSClothExtraData, skeleton As SkeletonInstance, dt As Single)
            Dim pkg As HclClothPackageGraph_Class = Nothing
            If Not _package.TryGetValue(block, pkg) Then
                pkg = HclClothPackageParser_Class.Parse(HkxPackfileParser_Class.Parse(block))
                If pkg Is Nothing Then Exit Sub
                _package.AddOrUpdate(block, pkg)
            End If

            Dim clothSkel = SkeletonClothOverlayHelper_Class.ParseClothSkeletonForBlock(block)
            If clothSkel Is Nothing Then Exit Sub
            Dim bindWorld = ComputeEmbeddedBindWorld(clothSkel)

            ' ⛔ UN ESTADO POR CONFIG, no uno por bloque. Un package puede traer varios ClothConfig y
            ' MEDIDO en vanilla: 23 packages tienen más de uno, y en LOS 23 cada config tiene distinta
            ' cantidad de partículas (PiperBodyF: 320/49/6). Con un solo estado compartido, el segundo
            ' config re-dimensionaba los arrays a CEROS, `Seeded` seguía en True, la siembra se salteaba
            ' y las anclas caminaban desde el origen del mundo. Se rompían los dos configs, no uno.
            Dim states As ClothSimState() = Nothing
            If Not _state.TryGetValue(block, states) OrElse states Is Nothing OrElse states.Length <> pkg.ClothConfigs.Count Then
                states = New ClothSimState(Math.Max(0, pkg.ClothConfigs.Count - 1)) {}
                _state.Remove(block)
                _state.Add(block, states)
            End If

            For ci = 0 To pkg.ClothConfigs.Count - 1
                Dim cfg = pkg.ClothConfigs(ci)
                If cfg Is Nothing OrElse cfg.SimpleMeshBoneDeform Is Nothing Then Continue For
                Dim sim = cfg.SimClothDatas.FirstOrDefault()
                If sim Is Nothing Then Continue For
                Dim particleCount = sim.ParticleDatas.Count
                If particleCount = 0 Then Continue For

                If states(ci) Is Nothing Then states(ci) = New ClothSimState()
                Dim st = states(ci)

                ' ⛔⛔ LA CADENA, NO UNA SECUENCIA FIJA. Aca habia una receta hardcodeada — skin,
                ' destinos, capsulas, sembrar, simular, writeback — que era una INVENCION de la app.
                ' El motor recorre `hclClothState.operators` y despacha lo que el archivo declara, en
                ' el orden que el archivo declara. `EjecutarCadena` hace eso; cada operador sabe de que
                ' buffer lee y a cual escribe porque lo dice el propio operador.
                EnsureState(st, sim, cfg, particleCount, dt, clothSkel)
                EjecutarCadena(cfg, st, sim, clothSkel, bindWorld, skeleton, particleCount, dt)
            Next
        End Sub

        ' -----------------------------------------------------------------------------------------
        ' Estado: parámetros authored + topología de constraints (una vez por bloque)
        ' -----------------------------------------------------------------------------------------
        Private Shared Sub EnsureState(st As ClothSimState, sim As HclSimClothDataDetail_Class,
                                       cfg As HclClothConfigGraph_Class, particleCount As Integer, dt As Single,
                                       clothSkel As Havok.Canon.Objects.HkObj_HkaSkeleton)
            If st.Positions IsNot Nothing AndAlso st.Positions.Length = particleCount Then
                RecomputeDamping(st, sim, dt)
                Exit Sub
            End If

            ' Re-dimensionar deja los arrays en CEROS: si `Seeded` quedara en True, la siembra se
            ' saltearia y la sim arrancaria desde el origen del mundo.
            st.Seeded = False
            ReDim st.Positions(particleCount - 1)
            ReDim st.Previous(particleCount - 1)
            ReDim st.InvMass(particleCount - 1)
            ReDim st.Mass(particleCount - 1)
            ReDim st.Radius(particleCount - 1)
            ReDim st.MascaraColision(particleCount - 1)
            ReDim st.NormalesSim(particleCount - 1)

            ' Los buffers, con el tamaño que declara cada `hclBufferDefinition` y en su posicion.
            Dim nBuf = If(cfg.BufferDefinitions Is Nothing, 0, cfg.BufferDefinitions.Count)
            ReDim st.Buffers(Math.Max(0, nBuf - 1))
            ReDim st.NormalesBuf(Math.Max(0, nBuf - 1))
            st.BufSim = If(cfg.SimpleMeshBoneDeform Is Nothing, -1, cfg.SimpleMeshBoneDeform.InputBufferIndex)
            For b = 0 To nBuf - 1
                ' El tamaño sale del `hclBufferDefinition` o, si esa posicion es un scratch, del
                ' `hclScratchBufferDefinition`. Los dos viven en el MISMO array del archivo.
                Dim def = cfg.BufferDefinitions(b)
                Dim nv = If(def Is Nothing, 0, def.ParticleCount)
                If nv <= 0 AndAlso cfg.ScratchBufferDefinitions IsNot Nothing AndAlso b < cfg.ScratchBufferDefinitions.Count Then
                    Dim scr = cfg.ScratchBufferDefinitions(b)
                    If scr IsNot Nothing Then nv = scr.ParticleCount
                End If
                If b = st.BufSim Then
                    ' ⛔ ALIAS, no copia: el buffer del deform ES el array de particulas.
                    st.Buffers(b) = st.Positions
                    st.NormalesBuf(b) = st.NormalesSim
                ElseIf nv > 0 Then
                    st.Buffers(b) = New Vector3(nv - 1) {}
                    st.NormalesBuf(b) = New Vector3(nv - 1) {}
                End If
            Next
            ' `triangleIndices` (+0x58) y `triangleFlips` (+0x68) del sim-cloth: la topologia con la que
            ' se arman las normales de la TELA. Sin los flips, una cara invertida aporta su normal al
            ' reves y el promedio del vertice sale girado.
            st.HaceNormales = sim.DoNormals
            Dim tri = sim.Triangles
            If tri IsNot Nothing AndAlso tri.Count > 0 Then
                ReDim st.TrisSim(tri.Count * 3 - 1)
                For q = 0 To tri.Count - 1
                    st.TrisSim(q * 3) = CInt(tri(q).Value0)
                    st.TrisSim(q * 3 + 1) = CInt(tri(q).Value1)
                    st.TrisSim(q * 3 + 2) = CInt(tri(q).Value2)
                Next
            Else
                st.TrisSim = New Integer() {}
            End If
            st.FlipsSim = If(sim.TriangleFlips Is Nothing, New Byte() {}, sim.TriangleFlips.ToArray())

            ' ⛔ PELLIZCO. `perParticlePinchDetectionEnabledFlags` (+0x108) dice QUE particulas
            ' participan, `collidablePinchingDatas` (+0x118) dice con que prioridad y radio lo hace
            ' CADA colisionable, y `minPinchedParticleIndex`/`maxPinchedParticleIndex` (+0x128/+0x12A)
            ' acotan el barrido. Los cuatro estaban declarados y ninguno llegaba al solver.
            st.PinchActivo = sim.PinchDetectionEnabled
            st.PinchMin = sim.MinPinchedParticleIndex
            st.PinchMax = sim.MaxPinchedParticleIndex
            st.PinchPorParticula = If(sim.PinchDetectionFlags Is Nothing, New Byte() {}, sim.PinchDetectionFlags.ToArray())
            st.PinchDeCapsula = If(sim.CollidablePinchingDatas Is Nothing, New HclCollidablePinchingData_Class() {},
                                   sim.CollidablePinchingDatas.ToArray())

            st.TerrenoActivo = sim.LandscapeCollisionEnabled
            st.TerrenoRadio = sim.LandscapeRadius
            st.TerrenoParticulas = sim.NumLandscapeCollidableParticles
            st.TerrenoDetectaEnganche = sim.EnableStuckParticleDetection
            st.TerrenoFactorEngancheSq = sim.StuckParticlesStretchFactorSq
            ReDim st.Friction(particleCount - 1)
            For i = 0 To particleCount - 1
                st.Mass(i) = sim.ParticleDatas(i).Mass
                st.InvMass(i) = sim.ParticleDatas(i).InverseMass
                st.Radius(i) = sim.ParticleDatas(i).Radius
                ' Sin mascara declarada, TODO colisiona: es lo que habia antes y lo unico que no
                ' inventa una restriccion que el archivo no puso.
                st.MascaraColision(i) = If(sim.StaticCollisionMasks IsNot Nothing AndAlso i < sim.StaticCollisionMasks.Count,
                                           sim.StaticCollisionMasks(i), UInteger.MaxValue)
                st.NormalesSim(i) = Vector3.Zero
                st.Friction(i) = sim.ParticleDatas(i).Friction
                _radMin = Math.Min(_radMin, st.Radius(i)) : _radMax = Math.Max(_radMax, st.Radius(i))
                _friMin = Math.Min(_friMin, st.Friction(i)) : _friMax = Math.Max(_friMax, st.Friction(i))
            Next
            st.Fixed = sim.FixedParticleIndices.Where(Function(x) x >= 0 AndAlso x < particleCount).ToArray()

            ' subSteps: el motor usa el de simulationInfo y, si es 0 (el 100 % del corpus vanilla),
            ' cae al del hclSimulateOperator.
            Dim subs = sim.SubSteps
            If subs <= 0 AndAlso cfg.Simulate IsNot Nothing Then subs = cfg.Simulate.SubstepCount
            If subs <= 0 Then subs = 1
            If HavokPhysicsSettings.SubstepOverride > 0 Then subs = HavokPhysicsSettings.SubstepOverride
            st.SubSteps = Math.Max(1, subs)
            st.AdaptStiffness = cfg.Simulate IsNot Nothing AndAlso cfg.Simulate.AdaptConstraintStiffness

            Dim iters = If(cfg.Simulate IsNot Nothing, cfg.Simulate.SolveIterationCount, 1)
            If HavokPhysicsSettings.SolveIterationOverride > 0 Then iters = HavokPhysicsSettings.SolveIterationOverride
            ' Sin tope: el motor usa `numberOfSolveIterations` tal cual (0x141A13756).
            st.SolveIterations = Math.Max(1, iters)

            ' ⛔ La gravedad sale de `simulationInfo.gravity` (+0x00) y de ningun otro lado. Habia
            ' una gravedad inventada de respaldo (-686,7): si el campo no se pudo leer, el problema es
            ' el parseo y taparlo con un numero elegido a mano hace que todo lo que se mida despues
            ' sea contra ese invento. Se deja en cero y se avisa.
            If sim.Gravity IsNot Nothing Then
                st.Gravity = New Vector3(CSng(sim.Gravity.X), CSng(sim.Gravity.Y), CSng(sim.Gravity.Z))
            Else
                st.Gravity = Vector3.Zero
                Logger.Log("[CLOTH] simulationInfo.gravity no se pudo leer: la prenda simula SIN gravedad.")
            End If

            Dim _g = st.Gravity, _it = st.SolveIterations, _ss = st.SubSteps, _ad = st.AdaptStiffness
            Dim _dmp = sim.GlobalDampingPerSecond
            Dim _invMin = Single.MaxValue, _invMax = Single.MinValue, _masMin = Single.MaxValue, _masMax = Single.MinValue
            For i = 0 To particleCount - 1
                _invMin = Math.Min(_invMin, st.InvMass(i)) : _invMax = Math.Max(_invMax, st.InvMass(i))
                _masMin = Math.Min(_masMin, st.Mass(i)) : _masMax = Math.Max(_masMax, st.Mass(i))
            Next
            Logger.LogLazy(Function() $"[CLOTH-CFG] grav=({_g.X:F1},{_g.Y:F1},{_g.Z:F1})x{HavokPhysicsSettings.GravityScale:F2} subSteps={_ss} iters={_it} adapt={_ad} damping/s={_dmp:F4} landscape={sim.LandscapeCollisionEnabled} stuck={sim.EnableStuckParticleDetection}/{sim.StuckParticlesStretchFactorSq:F3} pinch={sim.PinchDetectionEnabled} transfer={sim.TransferMotionEnabled} doNormals={sim.DoNormals} tol={sim.CollisionTolerance:F3} invMass=[{_invMin:F3}..{_invMax:F3}] masa=[{_masMin:F3}..{_masMax:F3}]")

            ' Puente particula -> vertice del ObjectSpaceSkin. Dos fuentes, y hacen falta LAS DOS:
            '   - `hclGatherAllVerticesOperator` (posicion = particula, valor = VertexIndex) es completo,
            '     pero MEDIDO: solo 105 de los 342 configs vanilla lo traen.
            '   - `hclMoveParticlesOperator.Pairs` (VertexIndex <-> ParticleIndex) lo traen los otros 237.
            ' Sin el segundo, 25.469 de 37.764 particulas (69 %) caian al `DefaultClothPose`, que es la
            ' pose de BIND del archivo: las anclas tiraban de los cloth-bones hacia la A-pose en cuanto
            ' habia pose o body-weight. En `DeformOnly` eso es PEOR que tener la fisica apagada.
            st.GatherMap = Nothing
            st.GatherMapHas = Nothing
            If cfg.GatherAllVertices IsNot Nothing AndAlso cfg.GatherAllVertices.GatheredVertexIndices.Count > 0 Then
                st.GatherMap = cfg.GatherAllVertices.GatheredVertexIndices.ToArray()
            ElseIf cfg.CopyVertices IsNot Nothing AndAlso cfg.CopyVertices.NumberOfVertices > 0 Then
                ' ⛔⛔ `hclCopyVerticesOperator` ES EL PUENTE, no un no-op.
                '
                ' Se lo daba por inocuo porque en el corpus es la identidad (875/875 con los dos
                ' `start` en 0). Pero el estado SIN fisica de una prenda usa `copy` o `gatherAll` para
                ' traer TODAS las particulas del buffer skinneado — el `moveParticles` del estado con
                ' fisica solo mueve algunas (22 de 113 en el pelo). Ignorar la copia dejaba 91
                ' particulas SIN destino: en `DeformOnly` se sembraban con el array recien
                ' redimensionado, o sea en el ORIGEN DEL MUNDO, y el pelo se caia al piso.
                '
                '     out[startVertexOut + i] = in[startVertexIn + i]   para i en [0, numberOfVertices)
                Dim map(particleCount - 1) As UShort
                Dim has(particleCount - 1) As Boolean
                For iq = 0 To cfg.CopyVertices.NumberOfVertices - 1
                    Dim destino = cfg.CopyVertices.StartVertexOut + iq
                    Dim origen = cfg.CopyVertices.StartVertexIn + iq
                    If destino < 0 OrElse destino >= particleCount OrElse origen < 0 OrElse origen > UShort.MaxValue Then Continue For
                    map(destino) = CUShort(origen)
                    has(destino) = True
                Next
                st.GatherMap = map
                st.GatherMapHas = has
            ElseIf cfg.GatherSomeVertices IsNot Nothing AndAlso cfg.GatherSomeVertices.Pairs IsNot Nothing AndAlso
                   cfg.GatherSomeVertices.Pairs.Count > 0 Then
                ' `hclGatherSomeVerticesOperator` — el MISMO puente que el GatherAll, pero con los pares
                ' explicitos `{uint16 indexInput; uint16 indexOutput;}` (+0x20, stride 4) en vez de un
                ' array indexado por particula. Su layout lo declara la reflexion y el parser ya lo leia;
                ' lo unico que faltaba era conectarlo. Es PARCIAL por definicion — "some" —, asi que
                ' lleva mascara de validez igual que el de MoveParticles.
                Dim map(particleCount - 1) As UShort
                Dim has(particleCount - 1) As Boolean
                For Each pr In cfg.GatherSomeVertices.Pairs
                    Dim pi = CInt(pr.Target)
                    If pi < 0 OrElse pi >= particleCount Then Continue For
                    map(pi) = CUShort(pr.Source)
                    has(pi) = True
                Next
                st.GatherMap = map
                st.GatherMapHas = has
            ElseIf cfg.MoveParticles IsNot Nothing AndAlso cfg.MoveParticles.Pairs IsNot Nothing Then
                Dim map(particleCount - 1) As UShort
                Dim has(particleCount - 1) As Boolean
                For Each pr In cfg.MoveParticles.Pairs
                    Dim pi = CInt(pr.ParticleIndex)
                    If pi < 0 OrElse pi >= particleCount Then Continue For
                    map(pi) = pr.VertexIndex
                    has(pi) = True
                Next
                st.GatherMap = map
                st.GatherMapHas = has
            End If

            If Logger.Enabled Then
                ' ⛔ EL DATO, ANTES DE CULPAR A LA LEY. La friccion escala una correccion de velocidad:
                ' con valores > 1 el paso INVIERTE la velocidad tangencial en vez de frenarla, y eso
                ' mete energia. Saber el rango real del corpus separa "la ley esta mal transcripta" de
                ' "la ley esta bien y el dato pide otra cosa".
                Dim a = _radMin, b = _radMax, cq = _friMin, dq = _friMax
                Logger.LogLazy(Function() $"[CLOTH-PART] radio=[{a:F4}..{b:F4}] friccion=[{cq:F4}..{dq:F4}]")
                _radMin = Single.MaxValue : _radMax = Single.MinValue
                _friMin = Single.MaxValue : _friMax = Single.MinValue
            End If

            BuildConstraints(st, sim, particleCount, clothSkel, If(cfg Is Nothing OrElse cfg.Simulate Is Nothing, Nothing, cfg.Simulate.Configs))
            RecomputeDamping(st, sim, dt)
        End Sub

        ''' <summary>
        ''' `damp = (1 − globalDampingPerSecond) ^ dtSub`, con las DOS ramas duras del motor
        ''' (0x1418C75B0): d &gt;= 1 ⇒ 0 (la tela no hereda nada de velocidad) y d = 0 ⇒ 1.
        ''' </summary>
        Private Shared Sub RecomputeDamping(st As ClothSimState, sim As HclSimClothDataDetail_Class, dt As Single)
            Dim d = sim.GlobalDampingPerSecond
            ' ⛔ EL EXPONENTE ES EL dt DEL FRAME, NO EL DEL SUBSTEP. El factor vive en
            ' `simCloth[+0x10c]` y se recalcula solo cuando cambia `simCloth[+0x108]`, que es el dt con
            ' el que se lo calculo (0x14195B904: si `[+0x108]` ya vale el dt de este frame no recalcula
            ' nada; si vale 0 lo guarda y sigue). Ese campo guarda el dt que recibe `Simulate`, o sea
            ' el del FRAME. Aca estaba `dt / subSteps`, que con `d = 0,5` y 3 substeps da 0,9963 en vez
            ' de 0,9889: la tela conserva mas velocidad de la que le corresponde.
            ' Con `d >= 1` (el caso del vestido) las dos ramas dan 0 y no se nota; se ve en el pelo.
            If d >= 1.0F Then
                st.Damping = 0.0F
            ElseIf d = 0.0F Then
                st.Damping = 1.0F
            Else
                st.Damping = CSng(Math.Pow(1.0R - d, dt))
            End If
        End Sub

        Private Shared Sub BuildConstraints(st As ClothSimState, sim As HclSimClothDataDetail_Class, particleCount As Integer,
                                            clothSkel As Havok.Canon.Objects.HkObj_HkaSkeleton,
                                            ejecucion As List(Of HclSimulateOperatorConfigGraph_Class))
            st.Links.Clear()
            st.Stretch.Clear()
            st.BendLinks.Clear()
            st.Compressible.Clear()
            st.BonePlanes.Clear()
            st.LocalRange.Clear()
            st.Bend.Clear()
            st.Blocks.Clear()
            ' Un bloque por CONSTRAINT SET, en el orden del array. La lista authored los referencia por
            ' INDICE y puede repetirlos, asi que primero se arman y despues se ORDENAN.
            Dim porSet As New Dictionary(Of Integer, ConstraintBlock)
            Dim idxSet = -1
            ' ⛔ `sim.ConstraintDetails` YA viene en el orden del archivo (se arma recorriendo
            ' `staticConstraintSets`), asi que recorrerlo en orden y anotar un bloque por set es todo
            ' lo que hace falta para que el solver respete la declaracion.
            ' ⛔ LOS ESTATICOS Y LOS ANTI-PINCH SE INGIEREN CON EL MISMO CODIGO. Son la misma
            ' familia de clases (`hclConstraintSet`) y el motor los resuelve con el mismo `solve`
            ' virtual: lo unico que cambia es CUANDO corren y con que `k`. Duplicar la ingesta seria
            ' garantizar que un dia queden distintas.
            IngerirSets(st, sim.ConstraintDetails, particleCount, clothSkel, st.Blocks, porSet)
            st.BloquesAntiPinch.Clear()
            IngerirSets(st, sim.AntiPinchDetails, particleCount, clothSkel, st.BloquesAntiPinch, Nothing)

            ' ⛔⛔ EL ORDEN LO DECLARA EL ARCHIVO, Y LA COLISION VA ADENTRO DE LA LISTA.
            '
            ' `hclSimulateOperator.constraintExecution` (+0x30) no es metadata: DECIDE QUE FUNCION DE
            ' SOLVE CORRE EL MOTOR. Con el array vacio va por `TtSolve` (0x141A133E0), un Gauss-Seidel
            ' que recorre `staticConstraintSets` una vez y colisiona al final. Con entradas va por
            ' `0x141A13650`, que recorre LA LISTA:
            '
            '     for it in 0 .. numberOfSolveIterations - 1:
            '         for e in constraintExecution:
            '             si e = -1:  CollideAndSolve(...)                 ' la colision va DONDE DIGA
            '             si no:      staticConstraintSets[e]->solve(...)  ' y un set puede REPETIRSE
            '
            ' MEDIDO: los 1.248 operadores del corpus traen `constraintExecution` con 2 a 7 entradas,
            ' o sea que el motor NUNCA usa el camino simple para estas prendas — y este simulador
            ' estaba corriendo justo ese.
            '
            ' Por que importa: MEDIDO tambien, los links estandar traen stiffness ~0,05 y
            ' `numberOfSolveIterations` = 1. Un link que corrige el 5 % de su error UNA sola vez no
            ' sostiene nada, y la violacion de links de la malla de simulacion llegaba al 540 %
            ' animando. Repetir un set es lo que lo hace converger, y quien decide cuantas veces es
            ' el archivo.
            If ejecucion IsNot Nothing AndAlso ejecucion.Count > 0 Then
                Dim ordenados As New List(Of ConstraintBlock)
                For Each e In ejecucion
                    If e Is Nothing Then Continue For
                    If e.IsTerminator Then
                        ' La colision no pasa por `StiffnessFactor`: el motor le pasa el `k` crudo
                        ' del operador (0x141A1379D), no el factor por set.
                        ordenados.Add(New ConstraintBlock With {.Kind = ConstraintKind.Colision, .TipoMotor = TipoDeMotor.Desconocido, .Start = 0, .Count = 0})
                        Continue For
                    End If
                    Dim b As ConstraintBlock = Nothing
                    If e.ConstraintIndex >= 0 AndAlso porSet.TryGetValue(e.ConstraintIndex, b) Then ordenados.Add(b)
                Next
                ' Solo se pisa si la lista resolvio ALGO. Una lista que no resuelve ningun set dejaria
                ' la tela sin una sola constraint, que es peor que el orden del array.
                If ordenados.Count > 0 Then
                    st.Blocks.Clear()
                    st.Blocks.AddRange(ordenados)
                End If
            End If

            If Logger.Enabled Then
                ' ⛔ QUE QUEDO REALMENTE EN EL BUCLE. Un reordenamiento que no resuelve un indice
                ' DESCARTA ese set en silencio, y una tela sin constraints se ve igual que una tela
                ' con la fisica mal calibrada — salvo por un sintoma: no mejora aunque se multipliquen
                ' los substeps. Se imprime la lista authored y los bloques finales para poder
                ' compararlos de un vistazo.
                Dim listaTxt = If(ejecucion Is Nothing, "(sin lista)",
                                  String.Join(",", ejecucion.Select(Function(e) If(e Is Nothing, "?", If(e.IsTerminator, "-1", CStr(e.ConstraintIndex))))))
                Dim bloquesTxt = String.Join(" ", st.Blocks.Select(Function(b) $"{b.Kind}({b.Count})"))
                ' ⛔ La rigidez POR LINK decide si el solve converge o rebota: el motor corrige
                ' `P[A] += invA·c` y `P[B] -= invB·c` con `c = (|d|−rest)·s·k·d̂` y NO normaliza por
                ' masa (0x141A06170). El error queda multiplicado por `1 − (invA+invB)·s·k`: si eso
                ' cae por debajo de −1 el link OSCILA y no converge nunca, por mas substeps que se le
                ' den. Con `s` y `k` a la vista se sabe de un vistazo de que lado esta.
                Dim _lMin = Single.MaxValue, _lMax = Single.MinValue
                For Each lk In st.Links
                    _lMin = Math.Min(_lMin, lk.Stiffness) : _lMax = Math.Max(_lMax, lk.Stiffness)
                Next
                Dim _iMax = 0.0F
                For i = 0 To st.InvMass.Length - 1
                    _iMax = Math.Max(_iMax, st.InvMass(i))
                Next
                Dim _k = FactorDeRigidez(0, st.AdaptStiffness, st.SubSteps, st.SubSteps - 1)
                Dim _nlk = st.Links.Count
                ' ⛔ UNA PARTICULA SIN NINGUNA CONSTRAINT CAE LIBRE. Con `globalDampingPerSecond`
                ' en 1 el damping es 0 y la particula no acumula velocidad, pero igual baja
                ' `gravedad * dtSub^2` por substep sin que nada la frene: en un clip de 30 frames se va
                ' varias unidades y arrastra a sus vecinas por los links que SI existen. Es la
                ' diferencia entre "el solver no llega" y "a esa particula no la agarra nadie".
                Dim _tocada(st.Positions.Length - 1) As Integer
                For Each lk2 In st.Links
                    If lk2.A >= 0 AndAlso lk2.A < _tocada.Length Then _tocada(lk2.A) += 1
                    If lk2.B >= 0 AndAlso lk2.B < _tocada.Length Then _tocada(lk2.B) += 1
                Next
                For Each bl In st.Blocks
                    If bl.Kind <> ConstraintKind.Bend Then Continue For
                    For q2 = bl.Start To bl.Start + bl.Count - 1
                        If q2 < 0 OrElse q2 >= st.Bend.Count Then Continue For
                        Dim bd = st.Bend(q2)
                        For Each idx2 In New Integer() {bd.A, bd.B, bd.C, bd.D}
                            If idx2 >= 0 AndAlso idx2 < _tocada.Length Then _tocada(idx2) += 1
                        Next
                    Next
                Next
                Dim _huerfanas = 0, _flojas = 0
                For q3 = 0 To _tocada.Length - 1
                    If st.InvMass(q3) = 0.0F Then Continue For
                    If _tocada(q3) = 0 Then
                        _huerfanas += 1
                    ElseIf _tocada(q3) <= 2 Then
                        _flojas += 1
                    End If
                Next
                Dim _hh = _huerfanas, _ff = _flojas, _nn = st.Positions.Length
                Logger.LogLazy(Function() $"[CLOTH-HUERFANAS] libres sin NINGUNA constraint={_hh} · con 1 o 2={_ff} · de {_nn} particulas")
                Logger.LogLazy(Function() $"[CLOTH-LINKS] n={_nlk} stiffness=[{_lMin:F4}..{_lMax:F4}] invMassMax={_iMax:F3} k={_k:F4} ⇒ factor de error por pasada = 1−(invA+invB)·s·k ∈ [{1.0F - 2.0F * _iMax * _lMax * _k:F3}..{1.0F - 2.0F * _iMax * _lMin * _k:F3}]")
                Logger.LogLazy(Function() $"[CLOTH-ORDEN] authored=[{listaTxt}] ⇒ bloques: {bloquesTxt}")
            End If
        End Sub

        ''' <summary>Aplana una lista de `hclConstraintSet` en los arrays del estado y anota UN bloque
        ''' por set en `destino`. `porSet` (opcional) indexa los bloques por su posicion en el array,
        ''' que es como los referencia `constraintExecution`.</summary>
        Private Shared Sub IngerirSets(st As ClothSimState, detalles As IEnumerable(Of Object),
                                       particleCount As Integer,
                                       clothSkel As Havok.Canon.Objects.HkObj_HkaSkeleton,
                                       destino As List(Of ConstraintBlock),
                                       porSet As Dictionary(Of Integer, ConstraintBlock))
            If detalles Is Nothing Then Exit Sub
            Dim idxSet = -1
            For Each detail In detalles
                idxSet += 1
                Dim dist = TryCast(detail, HclStandardLinkConstraintSetDetail_Class)
                If dist IsNot Nothing Then
                    Dim ini = st.Links.Count
                    AddLinks(st, dist.LinkDetails, particleCount, st.Links)
                    AgregarBloque(destino, porSet, idxSet, New ConstraintBlock With {.Kind = ConstraintKind.Distance, .TipoMotor = TipoDeMotor.StandardLink, .Start = ini, .Count = st.Links.Count - ini})
                    Continue For
                End If
                Dim stretch = TryCast(detail, HclStretchLinkConstraintSetDetail_Class)
                If stretch IsNot Nothing Then
                    Dim ini = st.Stretch.Count
                    AddLinks(st, stretch.LinkDetails, particleCount, st.Stretch)
                    AgregarBloque(destino, porSet, idxSet, New ConstraintBlock With {.Kind = ConstraintKind.Stretch, .TipoMotor = TipoDeMotor.StretchLink, .Start = ini, .Count = st.Stretch.Count - ini})
                    Continue For
                End If
                Dim bp = TryCast(detail, HclBonePlanesConstraintSetDetail_Class)
                If bp IsNot Nothing Then
                    Dim ini = st.BonePlanes.Count
                    For Each pl In bp.Constraints
                        Dim pi = CInt(pl.ParticleIndex)
                        If pi < 0 OrElse pi >= particleCount Then Continue For
                        ' ⛔ `transformIndex` indexa el TRANSFORM SET del motor. La app no lo modela, y
                        ' la unica correspondencia que puede sostener es la del esqueleto de la prenda:
                        ' se resuelve a NOMBRE de hueso y se comprueba. Si el indice se sale, la
                        ' constraint se SALTEA y se loguea — antes que aplicar un plano de otro hueso.
                        Dim ti = CInt(pl.TransformIndex)
                        Dim nm As String = Nothing
                        If clothSkel IsNot Nothing AndAlso clothSkel.Bones IsNot Nothing AndAlso
                           ti >= 0 AndAlso ti < clothSkel.Bones.Count Then
                            nm = clothSkel.Bones(ti)?.Name
                        End If
                        If String.IsNullOrWhiteSpace(nm) Then
                            If Logger.Enabled Then
                                Dim tq = ti
                                Logger.LogLazy(Function() $"[CLOTH-PLANO] transformIndex={tq} fuera del esqueleto de la prenda ⇒ constraint SALTEADA")
                            End If
                            Continue For
                        End If
                        st.BonePlanes.Add(New BonePlaneConstraint With {
                            .Particle = pi, .BoneName = nm.Trim(),
                            .Nx = pl.NormalX, .Ny = pl.NormalY, .Nz = pl.NormalZ, .D = pl.PlaneDistance,
                            .Stiffness = pl.Stiffness})
                    Next
                    AgregarBloque(destino, porSet, idxSet, New ConstraintBlock With {.Kind = ConstraintKind.BonePlane, .TipoMotor = TipoDeMotor.BonePlanes, .Start = ini, .Count = st.BonePlanes.Count - ini})
                    Continue For
                End If
                Dim blk2 = TryCast(detail, HclBendLinkConstraintSetDetail_Class)
                If blk2 IsNot Nothing Then
                    Dim ini = st.BendLinks.Count
                    For Each l In blk2.Links
                        Dim a = CInt(l.ParticleA), b = CInt(l.ParticleB)
                        If a < 0 OrElse b < 0 OrElse a >= particleCount OrElse b >= particleCount OrElse a = b Then Continue For
                        st.BendLinks.Add(New RangoLink With {.A = a, .B = b,
                                                             .Min = l.BendMinLength, .Max = l.StretchMaxLength,
                                                             .StiffMin = l.BendStiffness, .StiffMax = l.StretchStiffness})
                    Next
                    AgregarBloque(destino, porSet, idxSet, New ConstraintBlock With {.Kind = ConstraintKind.BendLink, .TipoMotor = TipoDeMotor.BendLink, .Start = ini, .Count = st.BendLinks.Count - ini})
                    Continue For
                End If
                Dim cl = TryCast(detail, HclCompressibleLinkConstraintSetDetail_Class)
                If cl IsNot Nothing Then
                    Dim ini = st.Compressible.Count
                    For Each l In cl.Links
                        Dim a = CInt(l.ParticleA), b = CInt(l.ParticleB)
                        If a < 0 OrElse b < 0 OrElse a >= particleCount OrElse b >= particleCount OrElse a = b Then Continue For
                        st.Compressible.Add(New RangoLink With {.A = a, .B = b,
                                                                .Min = l.CompressionLength, .Max = l.RestLength,
                                                                .StiffMin = l.Stiffness, .StiffMax = l.Stiffness})
                    Next
                    AgregarBloque(destino, porSet, idxSet, New ConstraintBlock With {.Kind = ConstraintKind.Compressible, .TipoMotor = TipoDeMotor.Desconocido, .Start = ini, .Count = st.Compressible.Count - ini})
                    Continue For
                End If
                Dim bend = TryCast(detail, HclBendStiffnessConstraintSetDetail_Class)
                If bend IsNot Nothing Then
                    ' ⛔ EL FLAG SE PROPAGA AL LINK. `useRestPoseConfig` elige entre DOS LEYES del
                    ' motor, no entre dos parametros: ver `SolveBend`. Las dos estan implementadas, pero
                    ' cual le toca a cada link lo decide el archivo, no una constante nuestra.
                    If Logger.Enabled Then
                        Dim nb = If(bend.LinkDetails Is Nothing, 0, bend.LinkDetails.Count)
                        Dim urp = bend.UseRestPoseConfig
                        Logger.LogLazy(Function() $"[CLOTH-BEND] set con {nb} links, useRestPoseConfig={urp} ⇒ ley {If(urp, "rest-pose (0x1419F9CF0)", "lineal (0x1419F9B50)")}")
                    End If
                    Dim iniBend = st.Bend.Count
                    For Each bl In bend.LinkDetails
                        Dim ia = CInt(bl.ParticleA), ib = CInt(bl.ParticleB)
                        Dim ic = CInt(bl.ParticleC), id_ = CInt(bl.ParticleD)
                        If ia < 0 OrElse ib < 0 OrElse ic < 0 OrElse id_ < 0 Then Continue For
                        If ia >= particleCount OrElse ib >= particleCount OrElse
                           ic >= particleCount OrElse id_ >= particleCount Then Continue For
                        st.Bend.Add(New BendLink With {
                            .A = ia, .B = ib, .C = ic, .D = id_,
                            .WA = bl.WeightA, .WB = bl.WeightB, .WC = bl.WeightC, .WD = bl.WeightD,
                            .Stiffness = bl.BendStiffness,
                            .RestCurvature = bl.RestCurvature,
                            .UseRestPose = bend.UseRestPoseConfig})
                    Next
                    AgregarBloque(destino, porSet, idxSet, New ConstraintBlock With {.Kind = ConstraintKind.Bend, .TipoMotor = TipoDeMotor.BendStiffness, .Start = iniBend, .Count = st.Bend.Count - iniBend})
                    Continue For
                End If

                Dim lr = TryCast(detail, HclLocalRangeConstraintSetDetail_Class)
                If lr IsNot Nothing Then
                    Dim iniLr = st.LocalRange.Count
                    For Each c In lr.ConstraintDetails
                        Dim pi = CInt(c.ParticleIndex)
                        If pi < 0 OrElse pi >= particleCount Then Continue For
                        st.LocalRange.Add(New LocalRangeConstraint With {
                            .Particle = pi,
                            .ReferenceVertex = CInt(c.ReferenceVertexIndex),
                            .MaxDistance = c.MaximumDistance,
                            .MaxNormal = c.MaximumNormalDistance,
                            .MinNormal = c.MinimumNormalDistance,
                            .Stiffness = lr.Stiffness,
                            .BufferRef = lr.ReferenceMeshBufferIdx,
                            .UsaNormal = lr.ApplyNormalComponent})
                    Next
                    AgregarBloque(destino, porSet, idxSet, New ConstraintBlock With {.Kind = ConstraintKind.LocalRange, .TipoMotor = TipoDeMotor.LocalRange, .Start = iniLr, .Count = st.LocalRange.Count - iniLr})
                    ' ⛔ LOS PARAMETROS REALES DE LA CORREA. Sin esto, "el termino normal empeoro el
                    ' pelo" es una impresion: puede ser que la ley este mal, o que esa prenda declare
                    ' `applyNormalComponent = False` y no le toque, o que los limites sean absurdos.
                    If Logger.Enabled Then
                        Dim n2 = st.LocalRange.Count - iniLr
                        Dim mn = Single.MaxValue, mx = Single.MinValue
                        Dim mnn = Single.MaxValue, mxn = Single.MinValue
                        For q = iniLr To st.LocalRange.Count - 1
                            mn = Math.Min(mn, st.LocalRange(q).MaxDistance)
                            mx = Math.Max(mx, st.LocalRange(q).MaxDistance)
                            mnn = Math.Min(mnn, st.LocalRange(q).MinNormal)
                            mxn = Math.Max(mxn, st.LocalRange(q).MaxNormal)
                        Next
                        Dim an = lr.ApplyNormalComponent, sf = lr.Stiffness, shp = lr.ShapeType
                        Logger.LogLazy(Function() $"[CLOTH-CORREA] {n2} constraints · buffer de referencia={lr.ReferenceMeshBufferIdx} · applyNormal={an} stiffness={sf:F4} shapeType={shp} · maxDist=[{mn:F3}..{mx:F3}] minNormal={mnn:F3} maxNormal={mxn:F3}")
                    End If
                End If
            Next
        End Sub

        ''' <summary>Registra un bloque y lo indexa por su posicion en `staticConstraintSets`, para que
        ''' la lista authored pueda referenciarlo — y repetirlo.</summary>
        Private Shared Sub AgregarBloque(destino As List(Of ConstraintBlock), porSet As Dictionary(Of Integer, ConstraintBlock),
                                         idxSet As Integer, b As ConstraintBlock)
            destino.Add(b)
            If porSet IsNot Nothing AndAlso idxSet >= 0 Then porSet(idxSet) = b
        End Sub

        ''' <summary>
        ''' ⛔ EL `stiffness` VA COMO VIENE. Antes se hacia
        ''' <c>If(l.Stiffness &gt; 0, Math.Min(1, l.Stiffness), 1)</c> — un clamp a 1 y un default de 1
        ''' que NO estan en el motor: los dos solvers (0x141A06170 y 0x141A06DB0) multiplican por el
        ''' float del archivo tal cual, sin tocarlo, y despues por el factor por-set `k`. Ese "arreglo"
        ''' defensivo convertia en 1.0 cualquier link con stiffness 0 — es decir, ponia a la maxima
        ''' rigidez justo los links que el autor habia desactivado.
        ''' </summary>
        Private Shared Sub AddLinks(st As ClothSimState, links As IEnumerable(Of HclDistanceConstraintGraph_Class), particleCount As Integer, destino As List(Of DistanceLink))
            If links Is Nothing Then Exit Sub
            For Each l In links
                Dim a = CInt(l.ParticleA), b = CInt(l.ParticleB)
                If a < 0 OrElse b < 0 OrElse a >= particleCount OrElse b >= particleCount OrElse a = b Then Continue For
                destino.Add(New DistanceLink With {.A = a, .B = b, .Rest = l.RestLength,
                                                   .Stiffness = l.Stiffness})
            Next
        End Sub

        ' -----------------------------------------------------------------------------------------
        ' Destinos: dónde tiene que estar cada partícula en este frame según la piel POSADA
        ' -----------------------------------------------------------------------------------------

        Private Shared Function BuildTargets(st As ClothSimState, sim As HclSimClothDataDetail_Class,
                                             cfg As HclClothConfigGraph_Class,
                                             skinned As Dictionary(Of Integer, Vector3),
                                             particleCount As Integer) As Vector3()
            Dim target(particleCount - 1) As Vector3
            Dim pose = sim.DefaultClothPoseDetails.FirstOrDefault()
            If st.TargetFromSkin Is Nothing OrElse st.TargetFromSkin.Length <> particleCount Then
                ReDim st.TargetFromSkin(Math.Max(0, particleCount - 1))
            End If

            ' ⛔⛔ A LA PARTICULA SIN MAPEO NO SE LE INVENTA UN DESTINO.
            '
            ' Aca habia una REGLA DE LA APP: un ajuste rigido (Kabsch) que emparejaba las particulas
            ' ancladas con sus destinos skinneados y arrastraba con esa transformacion a las que el
            ' archivo NO mapea. Servia para tapar un sintoma, pero el motor no hace nada parecido.
            '
            ' Lo que hace el motor: `hclMoveParticlesOperator` se llama "move SOME particles" y eso es
            ' literal — coloca SOLO las particulas que el archivo lista (33 de 321 en el vestido, 22 de
            ' 113 en el pelo) y a las demas las SIMULA. No tienen destino porque no lo necesitan: su
            ' posicion sale de la fisica.
            '
            ' Asi que una particula sin mapeo conserva su posicion actual, y el `target` solo lo usan
            ' las ancladas — que es exactamente para lo que existe.

            For i = 0 To particleCount - 1
                Dim got = False
                ' ⭐ El puente partícula↔vértice-de-skin es `hclGatherAllVerticesOperator`:
                ' gatherMap(particleIdx) = VertexIndex del ObjectSpaceSkin. NO es la identidad
                ' (medido: partícula 293 -> vértice 297; suponer identidad daba 43 u de error).
                If st.GatherMap IsNot Nothing AndAlso i < st.GatherMap.Length AndAlso
                   (st.GatherMapHas Is Nothing OrElse (i < st.GatherMapHas.Length AndAlso st.GatherMapHas(i))) Then
                    Dim v As Vector3 = Nothing
                    If skinned.TryGetValue(CInt(st.GatherMap(i)), v) Then
                        target(i) = v
                        got = True
                        st.TargetFromSkin(i) = True
                    End If
                End If
                ' ⛔ Y TAMPOCO SE LA LLEVA AL BIND. La rama que ponia la particula sin mapeo en su
                ' posicion del `DefaultClothPose` estaba en el espacio de BIND del archivo: en reposo
                ' coincide y el gate estatico da verde, pero bajo pose deja la particula clavada
                ' mientras las ancladas siguen al cuerpo. El motor no la coloca en ningun lado.
                If Not got Then target(i) = st.Positions(i)   ' sin mapeo: la simula, no la coloca
                If Not st.TargetFromSkin(i) Then st.TargetFromSkin(i) = False
            Next

            ' ⛔ CONTROL: en REPOSO el destino skinneado de CADA particula tiene que caer sobre su
            ' posicion del DefaultClothPose (que es el bind de la malla de simulacion). Una particula
            ' que se aparta decenas de unidades delata que el puente particula↔vertice la mando al
            ' vertice equivocado — y con UNA sola alcanza para que el triangulo de un cloth-bone quede
            ' convertido en una astilla de 100 unidades.
            If Logger.Enabled Then
                ' ⛔ EL DIAGNOSTICO TIENE QUE NOMBRAR LA FUENTE QUE SE USO DE VERDAD. Esta linea enumeraba
                ' solo GatherAll y MoveParticles: con el puente resuelto por `copy` decia
                ' "MoveParticles(22)" mientras el mapa real tenia 113 entradas. Un instrumento que
                ' nombra mal la fuente manda a buscar el defecto al lugar equivocado.
                Dim cubiertas = 0
                If st.GatherMap IsNot Nothing Then
                    For iq = 0 To Math.Min(particleCount, st.GatherMap.Length) - 1
                        If st.GatherMapHas Is Nothing OrElse (iq < st.GatherMapHas.Length AndAlso st.GatherMapHas(iq)) Then cubiertas += 1
                    Next
                End If
                Dim fuente As String
                If cfg.GatherAllVertices IsNot Nothing AndAlso cfg.GatherAllVertices.GatheredVertexIndices.Count > 0 Then
                    fuente = $"GatherAllVertices({cfg.GatherAllVertices.GatheredVertexIndices.Count})"
                ElseIf cfg.CopyVertices IsNot Nothing AndAlso cfg.CopyVertices.NumberOfVertices > 0 Then
                    fuente = $"CopyVertices({cfg.CopyVertices.NumberOfVertices})"
                ElseIf cfg.GatherSomeVertices IsNot Nothing AndAlso cfg.GatherSomeVertices.Pairs IsNot Nothing AndAlso cfg.GatherSomeVertices.Pairs.Count > 0 Then
                    fuente = $"GatherSomeVertices({cfg.GatherSomeVertices.Pairs.Count})"
                ElseIf cfg.MoveParticles IsNot Nothing AndAlso cfg.MoveParticles.Pairs IsNot Nothing Then
                    fuente = $"MoveParticles({cfg.MoveParticles.Pairs.Count})"
                Else
                    fuente = "NINGUNA"
                End If
                Dim cub = cubiertas
                Dim poseN = If(pose Is Nothing OrElse pose.Pose Is Nothing, -1, pose.Pose.Count)
                Dim nPoses = sim.DefaultClothPoseDetails.Count
                Logger.LogLazy(Function() $"[CLOTH-MAP] fuente={fuente} · particulas con destino={cub}/{particleCount} · posesEnElArchivo={nPoses} pose.Count={poseN}")
            End If
            If Logger.Enabled AndAlso pose IsNot Nothing AndAlso pose.Pose IsNot Nothing Then
                Dim malas As New List(Of String)
                Dim peor = 0.0F
                For i = 0 To particleCount - 1
                    If i >= pose.Pose.Count Then Exit For
                    Dim pp = pose.Pose(i)
                    Dim d = (target(i) - New Vector3(CSng(pp.X), CSng(pp.Y), CSng(pp.Z))).Length
                    If d > peor Then peor = d
                    If d > 5.0F AndAlso malas.Count < 8 Then
                        Dim dv = target(i) - New Vector3(CSng(pp.X), CSng(pp.Y), CSng(pp.Z))
                        malas.Add($"{i}:gm={GatherOf(st, i)} skin=({target(i).X:F1},{target(i).Y:F1},{target(i).Z:F1})" &
                                  $" pose=({pp.X:F1},{pp.Y:F1},{pp.Z:F1}) d=({dv.X:F1},{dv.Y:F1},{dv.Z:F1})")
                    End If
                Next
                Dim pe = peor
                Dim ml = malas
                Dim n = particleCount
                Logger.LogLazy(Function() $"[CLOTH-TARGET] particulas={n} peor|skin-pose|={pe:F2} fuera(>5u)={ml.Count}: {String.Join(" ", ml)}")
            End If
            Return target
        End Function

        ' -----------------------------------------------------------------------------------------
        ' El bucle del motor
        ' -----------------------------------------------------------------------------------------
        ''' <summary>
        ''' `hclSimulateOperator` — un operador mas de la cadena. Siembra si hace falta, transfiere el
        ''' movimiento del actor y corre `subSteps` × (integrar + `numberOfSolveIterations` × solve).
        ''' </summary>
        Private Shared Sub OpSimulate(st As ClothSimState, sim As HclSimClothDataDetail_Class,
                                      cfg As HclClothConfigGraph_Class,
                                      clothSkel As Havok.Canon.Objects.HkObj_HkaSkeleton,
                                      skeleton As SkeletonInstance, particleCount As Integer, dt As Single)
            ' Las capsulas siguen al esqueleto: se rearman antes de simular.
            RebuildCapsules(st, sim, clothSkel, skeleton, dt)
            st.TerrenoAltura = AlturaDelSuelo(skeleton)

            ' ⛔ La siembra: la PRIMERA vez y cuando el actor teleporta. Fuera de eso el estado es
            ' continuo — que es lo que hace que la tela tenga memoria de un frame al siguiente.
            Dim teleported = DetectTeleport(st, skeleton)
            If (Not st.Seeded) OrElse teleported Then
                ' Las particulas ya estan donde las dejo la cadena (skin -> move/gather/copy): sembrar
                ' es fijar la posicion previa a la actual, o sea arrancar con velocidad cero.
                For i = 0 To particleCount - 1
                    st.Previous(i) = st.Positions(i)
                Next
                st.Seeded = True
                st.TieneRefAnterior = False
                If Not teleported Then Settle(st, skeleton, dt)
            End If

            TransferirMovimiento(st, sim, clothSkel, skeleton, dt)
            Simulate(st, skeleton, dt)
            DiagnosticoDeLaMalla(st)
        End Sub

        ''' <summary>
        ''' El estado de la MALLA DE SIMULACION despues de simular. Separa dos culpables que en
        ''' pantalla se ven igual: "la tela se estiro" (violacion de links alta) y "la tela esta bien y
        ''' lo que esta mal es el frame que le doy al hueso" (violacion baja y el render igual roto).
        ''' </summary>
        Private Shared Sub DiagnosticoDeLaMalla(st As ClothSimState)
            If Not Logger.Enabled OrElse st.Links Is Nothing OrElse st.Links.Count = 0 Then Exit Sub
            Dim peor = 0.0F, med = 0.0R, n = 0, fuera = 0
            Dim est = 0.0F, apl = 0.0F
            Dim pA = -1, pB = -1, pD = 0.0F, pRest = 0.0F
            For Each lk In st.Links
                If lk.A < 0 OrElse lk.B < 0 OrElse lk.A >= st.Positions.Length OrElse lk.B >= st.Positions.Length Then Continue For
                If lk.Rest <= 0.0001F Then Continue For
                Dim d = (st.Positions(lk.A) - st.Positions(lk.B)).Length
                Dim r = Math.Abs(d - lk.Rest) / lk.Rest
                n += 1 : med += r
                If r > 0.25F Then fuera += 1
                If d > lk.Rest Then est = Math.Max(est, d / lk.Rest) Else apl = Math.Max(apl, lk.Rest / Math.Max(d, 0.0001F))
                If r > peor Then
                    peor = r : pA = lk.A : pB = lk.B : pD = d : pRest = lk.Rest
                End If
            Next
            Dim pen = 0, penPeor = 0.0F
            For i = 0 To st.Positions.Length - 1
                If st.InvMass(i) = 0.0F Then Continue For
                Dim hond = 0.0F
                For Each c In st.Capsules
                    If c.Bit <> 0UI AndAlso (st.MascaraColision(i) And c.Bit) = 0UI Then Continue For
                    Dim nn As Vector3 = Nothing
                    Dim prof = ProfundidadEnConoRedondeado(st.Positions(i), c.A, c.B, c.Radius, c.RadiusB, st.Radius(i), nn)
                    If prof > hond Then hond = prof
                Next
                If hond > 0.001F Then pen += 1 : penPeor = Math.Max(penPeor, hond)
            Next
            Dim p1 = peor, m1 = If(n > 0, med / n, 0.0R), f1 = fuera, n1 = n, e1 = est, a1 = apl
            Dim q1 = pen, q2 = penPeor
            ' ⛔ QUIEN es el peor link, no solo cuanto. Un link a 4x el reposo entre dos particulas
            ' LIBRES es "el solver no llega"; entre una FIJA y una libre es "el ancla la esta
            ' arrastrando"; y si la fija esta lejos de donde la puso el skin, el destino esta mal.
            ' ⭐ CONTROL LIMPIO: los links con LOS DOS extremos fijos. El solver no los toca nunca
            ' (las dos particulas las coloca `hclMoveParticlesOperator` y la integracion las saltea),
            ' asi que su violacion NO puede venir del solver: sale entera de donde quedaron las anclas,
            ' o sea del skin y del mapeo vertice->particula. Si esto da ≈ 0 el problema esta aguas
            ' abajo; si da alto, esta aguas arriba y no tiene sentido tocar el solver.
            ' ⭐⭐ EL CONTROL QUE FALTABA: la MISMA cuenta sobre la malla SKINNEADA.
            '
            ' El buffer que deja `hclObjectSpaceSkinPNOperator` es la prenda pegada al cuerpo, sin una
            ' sola linea de fisica. Si ESE mesh ya viola los `restLength`, entonces los restLength no
            ' describen la prenda sobre este cuerpo y la "violacion" no es un defecto del solver: es la
            ' diferencia normal entre el reposo de la tela y la piel. Sin este control, cualquier
            ' numero de violacion se puede leer como un bug del solver — que es justo lo que hice.
            Dim skN = 0, skPeor = 0.0F
            Dim skMed = 0.0R
            Dim bufSkin = If(st.BufSim >= 0 AndAlso st.Buffers IsNot Nothing, BufferDe(st, If(st.BufSim = 0, 1, 0)), Nothing)
            If bufSkin IsNot Nothing Then
                For Each lk4 In st.Links
                    If lk4.A < 0 OrElse lk4.B < 0 OrElse lk4.Rest <= 0.0001F Then Continue For
                    Dim va = GatherOf(st, lk4.A), vb = GatherOf(st, lk4.B)
                    If va < 0 OrElse vb < 0 OrElse va >= bufSkin.Length OrElse vb >= bufSkin.Length Then Continue For
                    Dim dd2 = (bufSkin(va) - bufSkin(vb)).Length
                    Dim rr2 = Math.Abs(dd2 - lk4.Rest) / lk4.Rest
                    skN += 1 : skMed += rr2
                    skPeor = Math.Max(skPeor, rr2)
                Next
            End If
            Dim sk1 = skN, sk2 = skPeor, sk3 = If(skN > 0, skMed / skN, 0.0R)
            Logger.LogLazy(Function() $"[CLOTH-CONTROL] los MISMOS links sobre la malla SKINNEADA (sin fisica): n={sk1} media={sk3:P1} peor={sk2:P1}")

            Dim ffN = 0, ffPeor = 0.0F
            Dim ffMed = 0.0R
            For Each lk3 In st.Links
                If lk3.A < 0 OrElse lk3.B < 0 OrElse lk3.A >= st.Positions.Length OrElse lk3.B >= st.Positions.Length Then Continue For
                If lk3.Rest <= 0.0001F Then Continue For
                If st.InvMass(lk3.A) <> 0.0F OrElse st.InvMass(lk3.B) <> 0.0F Then Continue For
                Dim dd = (st.Positions(lk3.A) - st.Positions(lk3.B)).Length
                Dim rr = Math.Abs(dd - lk3.Rest) / lk3.Rest
                ffN += 1 : ffMed += rr
                ffPeor = Math.Max(ffPeor, rr)
            Next
            ' ⭐ LAS ANCLAS CONTRA LAS CAPSULAS. El motor NO colisiona las particulas fijas (invMass=0):
            ' las coloca el ancla y punto. Pero si el ancla queda DENTRO de una capsula, su vecina
            ' LIBRE si es empujada afuera, y el link entre las dos no puede cerrar nunca por mucho que
            ' se itere. Es la firma de "el peor link es FIJA-libre y no baja".
            Dim ancDentro = 0
            Dim ancHondo = 0.0F
            For q4 = 0 To st.Fixed.Length - 1
                Dim ia = st.Fixed(q4)
                If ia < 0 OrElse ia >= st.Positions.Length Then Continue For
                For Each cc In st.Capsules
                    If cc.Bit <> 0UI AndAlso (st.MascaraColision(ia) And cc.Bit) = 0UI Then Continue For
                    Dim nn2 As Vector3 = Nothing
                    Dim pf = ProfundidadEnConoRedondeado(st.Positions(ia), cc.A, cc.B, cc.Radius, cc.RadiusB, st.Radius(ia), nn2)
                    If pf > ancHondo Then ancHondo = pf
                Next
                If ancHondo > 0.001F Then ancDentro += 1
            Next
            Dim ad1 = ancDentro, ad2 = ancHondo, ad3 = st.Fixed.Length
            Logger.LogLazy(Function() $"[CLOTH-ANCCAP] anclas DENTRO de una capsula: {ad1}/{ad3} · la mas hundida: {ad2:F2} u")

            Dim ff1 = ffN, ff2 = ffPeor, ff3 = If(ffN > 0, ffMed / ffN, 0.0R)
            Logger.LogLazy(Function() $"[CLOTH-FIJAS] links FIJA-FIJA={ff1} · violacion media={ff3:P1} peor={ff2:P1}")

            Dim fA = pA >= 0 AndAlso st.InvMass(pA) = 0.0F
            Dim fB = pB >= 0 AndAlso st.InvMass(pB) = 0.0F
            Dim tipo = If(fA AndAlso fB, "FIJA-FIJA", If(fA OrElse fB, "FIJA-libre", "libre-libre"))
            Dim pA2 = pA, pB2 = pB, pD2 = pD, pR2 = pRest, tipo2 = tipo
            Logger.LogLazy(Function() $"[CLOTH-PEOR] link {pA2}-{pB2} ({tipo2}) d={pD2:F2} rest={pR2:F2} ⇒ x{(pD2 / Math.Max(pR2, 0.0001F)):F2}")
            Logger.LogLazy(Function() $"[CLOTH-VIOL] malla de sim: peor={p1:P1} media={m1:P1} estiron=x{e1:F2} aplaste=x{a1:F2} links>25%={f1}/{n1} · dentro del cuerpo={q1} (hasta {q2:F2} u)")
        End Sub

        Private Shared Sub Simulate(st As ClothSimState, skeleton As SkeletonInstance, dt As Single)
            Dim skinned = If(st.Skinned, New Dictionary(Of Integer, Vector3))
            Dim normalesRef = If(st.NormalesRef, New Dictionary(Of Integer, Vector3))
            Dim n = st.Positions.Length
            Dim substeps = st.SubSteps
            Dim dtSub = dt / substeps
            st.DtSub = dtSub
            Dim dtSub2 = dtSub * dtSub
            Dim gravity = st.Gravity * HavokPhysicsSettings.GravityScale

            ' ⭐ LAS ANCLAS SE INTERPOLAN A LO LARGO DE LOS SUBSTEPS — `0x14195C870` / `0x14195CB88`.
            ' Antes del bucle el motor guarda, por cada particula fija, el PAR (previa, actual): la
            ' previa es donde estaba el ancla el frame pasado y la actual es donde la acaba de poner
            ' `hclMoveParticlesOperator`. Despues, en CADA substep, escribe en las DOS
            '     P = previa·(1−α) + actual·α,   α = (s+1)/subSteps
            ' asi que en el ultimo substep α=1 y el ancla termina exactamente en su destino.
            ' ⛔ Por que importa: sin esto el ancla salta al destino en el primer substep. El solver
            ' corrige una fraccion por pasada, no llega a repartir ese estiron entre los vecinos, y
            ' la malla se ABRE — que es el agujero que se ve cuando el actor gira o choca.
            Dim ancPrev(Math.Max(0, st.Fixed.Length - 1)) As Vector3
            Dim ancPos(Math.Max(0, st.Fixed.Length - 1)) As Vector3
            Dim ancSalto = 0.0F
            For q = 0 To st.Fixed.Length - 1
                ancPrev(q) = st.Previous(st.Fixed(q))
                ancPos(q) = st.Positions(st.Fixed(q))
                ancSalto = Math.Max(ancSalto, (ancPos(q) - ancPrev(q)).Length)
            Next
            If Logger.Enabled Then
                Dim aj = ancSalto, nf = st.Fixed.Length
                Logger.LogLazy(Function() $"[CLOTH-ANCLA] {nf} anclas · la que mas se movio en este frame: {aj:F3} u")
            End If

            For s = 0 To substeps - 1

                Dim alpha = CSng(s + 1) / substeps

                ' (0) los colisionables se mueven ANTES que nada en el substep (0x14195C9B0).
                ColocarColisionables(st, alpha)

                For q = 0 To st.Fixed.Length - 1
                    Dim idx = st.Fixed(q)
                    Dim pa = Vector3.Lerp(ancPrev(q), ancPos(q), alpha)
                    st.Positions(idx) = pa
                    st.Previous(idx) = pa
                Next

                ' (1) Verlet de posición. invMass = 0 (ancla) ⇒ aceleración 0.
                For i = 0 To n - 1
                    Dim inv = st.InvMass(i)
                    If inv = 0.0F Then Continue For
                    Dim cur = st.Positions(i)
                    Dim vel = (cur - st.Previous(i)) * st.Damping
                    Dim acc = gravity * (st.Mass(i) * inv)
                    st.Previous(i) = cur
                    st.Positions(i) = cur + vel + (acc * dtSub2)
                Next

                ' (4) solve: N iteraciones × (constraints, después colisión)
                ' Gauss-Seidel: N iteraciones y, DENTRO de cada una, los constraint sets en el orden
                ' que declara el archivo y despues la colision. Ver `ConstraintBlock`.
                ' Sin lista authored el motor colisiona al FINAL de cada iteracion; con lista, la
                ' colision ya esta adentro y aca no va nada.
                Dim sinLista = Not st.Blocks.Any(Function(x) x.Kind = ConstraintKind.Colision)
                For it = 0 To st.SolveIterations - 1
                    For Each blk In st.Blocks
                        ' ⛔ UN `k` POR SET Y POR SUBSTEP, no uno global: `StiffnessFactor` mira el TIPO
                        ' del set y el indice del substep. La correa y los planos de hueso, por ejemplo,
                        ' solo actuan en el ULTIMO substep.
                        ' ⛔ Sin atajo: el motor llama al solve del set SIEMPRE (`call [rax+0x48]`,
                        ' 0x141A13573) aunque `StiffnessFactor` devuelva 0. Saltearlo asume que todos
                        ' los solvers son lineales en k, y no lo verifique para todos.
                        Dim k = FactorDeRigidez(blk.TipoMotor, st.AdaptStiffness, st.SubSteps, s)
                        Select Case blk.Kind
                            Case ConstraintKind.Distance
                                SolveDistanceLinks(st, blk.Start, blk.Count, k)
                            Case ConstraintKind.Stretch
                                SolveStretchLinks(st, blk.Start, blk.Count, k)
                            Case ConstraintKind.Bend
                                If HavokPhysicsSettings.EnableBend Then SolveBend(st, blk.Start, blk.Count, k)
                            Case ConstraintKind.BendLink
                                SolveBendLinks(st, blk.Start, blk.Count, k)
                            Case ConstraintKind.Compressible
                                SolveCompressibleLinks(st, blk.Start, blk.Count, k)
                            Case ConstraintKind.BonePlane
                                SolveBonePlanes(st, skeleton, blk.Start, blk.Count, k)
                            Case ConstraintKind.LocalRange
                                If HavokPhysicsSettings.EnableLocalRange Then SolveLocalRange(st, skinned, normalesRef, blk.Start, blk.Count, k)
                            Case ConstraintKind.Colision
                                ' El `-1` de la lista authored. La colision NO va al final: va donde el
                                ' archivo la puso, y puede aparecer mas de una vez.
                                If HavokPhysicsSettings.EnableCollision Then
                                    SolveCapsules(st)
                                    SolveTerreno(st)
                                    SolvePellizco(st)
                                    SolveAntiPinch(st, skeleton, skinned, normalesRef)
                                End If
                        End Select
                    Next
                    If sinLista AndAlso HavokPhysicsSettings.EnableCollision Then
                        SolveCapsules(st)
                        SolveTerreno(st)
                        SolvePellizco(st)
                        SolveAntiPinch(st, skeleton, skinned, normalesRef)
                    End If
                Next
            Next
        End Sub

        ''' <summary>
        ''' `transferMotion` — <c>0x141A13950</c>, al que `Simulate` (<c>0x14195C350</c>) llama antes del
        ''' bucle de substeps cuando <c>simulationInfo.transferMotionEnabled</c> esta prendido.
        '''
        ''' <para>⛔⛔ QUE PROBLEMA RESUELVE, Y POR QUE SIN EL LA TELA SE ABRE. Las particulas viven en
        ''' el MUNDO. Cuando el actor camina o gira, las anclas se van con el cuerpo y las libres se
        ''' quedan donde estaban: los links tienen que estirarse para cubrir esa diferencia, y como el
        ''' solver corrige una fraccion por pasada, la malla queda abierta y no se recupera.
        ''' MEDIDO con el gate rigido: con la pose QUIETA despues de un giro de 40°, la distorsion
        ''' crecia 1,79 -> 2,33 en 60 pasos en vez de asentarse. Este operador es el que mueve TODO el
        ''' conjunto con el actor para que el solver no tenga que inventar esa diferencia.</para>
        '''
        ''' <para>La ley, leida del binario:</para>
        ''' <code>
        '''     M       = cur · inv(prev)                      ' el movimiento del hueso de referencia
        '''     vLin    = |M.traslacion| / dt                  ' unidades por segundo
        '''     vAng    = angulo(M) / dt · 180/π               ' GRADOS por segundo (const 57,29578)
        '''     bTras   = rampa(vLin, minTranslationSpeed, maxTranslationSpeed,
        '''                           minTranslationBlend, maxTranslationBlend)
        '''     bRot    = rampa(vAng, minRotationSpeed, maxRotationSpeed,
        '''                           minRotationBlend, maxRotationBlend)
        '''     si bTras &lt;= 0 y bRot &lt;= 0: no hace nada
        '''     Mb      = rotacion(eje(M), bRot · angulo(M)) con traslacion = bTras · M.traslacion
        '''     T       = inv(prev) · Mb · prev                ' el delta mezclado, llevado al mundo
        '''     para cada particula con masa &lt;&gt; 0:  P = P·T   y   Pprev = Pprev·T
        ''' </code>
        ''' <para>La "rampa" es literal: por debajo del minimo devuelve el blend minimo, por encima del
        ''' maximo el maximo, y en el medio interpola lineal (0x141A13AA3..0x141A13B08).</para>
        ''' <para>⚠️ Las particulas FIJAS quedan afuera: el motor arma una mascara con
        ''' <c>mass == 0</c> (<c>cmpeqps</c> contra cero en 0x141A13DA6) porque a esas las coloca el
        ''' ancla, y moverlas ademas seria contarlo dos veces.</para>
        ''' <para>⚠️ LIMITE DECLARADO: el motor toma la referencia de
        ''' <c>transformSets[transformSetIndex][transformIndex]</c>. Esta app no modela el transform-set,
        ''' asi que resuelve `transformIndex` contra el esqueleto de la prenda y usa el hueso VIVO —
        ''' la misma aproximacion que ya usa `SolveBonePlanes`. Si el indice no cae ahi, NO se
        ''' transfiere nada y se loguea, en vez de mover la tela con el hueso equivocado.</para>
        ''' </summary>
        Private Shared Sub TransferirMovimiento(st As ClothSimState, sim As HclSimClothDataDetail_Class,
                                                clothSkel As Havok.Canon.Objects.HkObj_HkaSkeleton,
                                                skeleton As SkeletonInstance, dt As Single)
            If sim Is Nothing OrElse Not sim.TransferMotionEnabled Then Exit Sub
            If skeleton Is Nothing OrElse dt <= 0.0F Then Exit Sub

            Dim ti = sim.TransferMotionTransformIndex
            Dim nm As String = Nothing
            If clothSkel IsNot Nothing AndAlso clothSkel.Bones IsNot Nothing AndAlso
               ti >= 0 AndAlso ti < clothSkel.Bones.Count Then
                nm = clothSkel.Bones(ti)?.Name
            End If
            If String.IsNullOrWhiteSpace(nm) Then
                If Logger.Enabled Then
                    Dim tq = ti
                    Logger.LogLazy(Function() $"[CLOTH-TRANSFER] transformIndex={tq} no cae en el esqueleto de la prenda ⇒ NO se transfiere movimiento")
                End If
                Exit Sub
            End If
            Dim hueso As HierarchiBone_class = Nothing
            If Not skeleton.SkeletonDictionary.TryGetValue(nm.Trim(), hueso) OrElse hueso Is Nothing Then Exit Sub
            Dim g = hueso.GetGlobalTransform
            If g Is Nothing Then Exit Sub
            Dim cur = g.ToMatrix4()

            If Not st.TieneRefAnterior Then
                st.RefAnterior = cur
                st.TieneRefAnterior = True
                Exit Sub
            End If
            Dim prev = st.RefAnterior
            st.RefAnterior = cur

            Dim invPrev As Matrix4 = Nothing
            Try
                invPrev = prev.Inverted()
            Catch
                Exit Sub
            End Try

            ' M = cur · inv(prev): el movimiento del hueso, en convencion de FILA como todo el modulo.
            Dim m = Matrix4.Mult(cur, invPrev)
            Dim trasl As New Vector3(m.M41, m.M42, m.M43)

            ' El angulo del giro, por la traza de la parte 3x3.
            Dim traza = m.M11 + m.M22 + m.M33
            Dim cosA = Math.Max(-1.0R, Math.Min(1.0R, (traza - 1.0R) / 2.0R))
            Dim ang = CSng(Math.Acos(cosA))

            Dim bTras = 0.0F, bRot = 0.0F
            If sim.TransferTranslationMotion Then
                bTras = Rampa(trasl.Length() / dt, sim.MinTranslationSpeed, sim.MaxTranslationSpeed,
                              sim.MinTranslationBlend, sim.MaxTranslationBlend)
            End If
            If sim.TransferRotationMotion Then
                ' ⛔ GRADOS por segundo: el motor multiplica por 57,29578 (0x142467810) antes de comparar.
                bRot = Rampa(ang / dt * 57.2957764F, sim.MinRotationSpeed, sim.MaxRotationSpeed,
                             sim.MinRotationBlend, sim.MaxRotationBlend)
            End If
            If bTras <= 0.0F AndAlso bRot <= 0.0F Then Exit Sub

            ' Mb: la MISMA rotacion pero con el angulo escalado, y la traslacion escalada.
            Dim mb = Matrix4.Identity
            If bRot > 0.0F AndAlso ang > 1.0E-6F Then
                Dim eje = EjeDeRotacion(m, ang)
                If eje.LengthSquared > 0.0F Then
                    mb = Matrix4.CreateFromAxisAngle(Vector3.Normalize(eje), ang * bRot)
                End If
            End If
            mb.M41 = trasl.X * bTras
            mb.M42 = trasl.Y * bTras
            mb.M43 = trasl.Z * bTras

            ' T = inv(prev) · Mb · prev — el delta local llevado al mundo. Con mezcla 1 da inv(prev)·cur,
            ' que es "seguir al hueso" exacto: ese es el control que tiene que cumplir la formula.
            Dim t = Matrix4.Mult(Matrix4.Mult(invPrev, mb), prev)

            Dim P = st.Positions
            Dim Q = st.Previous
            For i = 0 To P.Length - 1
                If st.Mass(i) = 0.0F Then Continue For   ' fijas: las coloca el ancla
                P(i) = Vector3.TransformPosition(P(i), t)
                Q(i) = Vector3.TransformPosition(Q(i), t)
            Next

            If Logger.Enabled Then
                Dim bt = bTras, br = bRot, an = ang, tl = trasl.Length()
                Logger.LogLazy(Function() $"[CLOTH-TRANSFER] hueso='{nm}' |dTrasl|={tl:F3} ang={an * 57.2957764F:F2}° ⇒ blend traslacion={bt:F3} rotacion={br:F3}")
            End If
        End Sub

        ''' <summary>La rampa de `transferMotion`: plana abajo del minimo, plana arriba del maximo,
        ''' lineal en el medio. Con `max = min` el motor devuelve el blend MINIMO (division guardada
        ''' contra un epsilon en 0x141A13AD9), no una division por cero.</summary>
        Private Shared Function Rampa(v As Single, vMin As Single, vMax As Single,
                                      bMin As Single, bMax As Single) As Single
            If v >= vMax Then Return bMax
            If v <= vMin Then Return bMin
            Dim span = vMax - vMin
            If Math.Abs(span) <= 1.0E-7F Then Return bMin
            Return bMin + (bMax - bMin) * ((v - vMin) / span)
        End Function

        ''' <summary>Eje de la rotacion de una matriz de fila, sacado de la parte antisimetrica. Se usa
        ''' solo para re-escalar el angulo, asi que no hace falta normalizar aca (el llamador lo hace).</summary>
        Private Shared Function EjeDeRotacion(m As Matrix4, ang As Single) As Vector3
            ' Cerca de 180° la parte antisimetrica se anula y hay que sacarlo de la diagonal.
            If ang < 3.13F Then
                Return New Vector3(m.M23 - m.M32, m.M31 - m.M13, m.M12 - m.M21)
            End If
            Dim x = Math.Sqrt(Math.Max(0.0R, (m.M11 + 1.0R) / 2.0R))
            Dim y = Math.Sqrt(Math.Max(0.0R, (m.M22 + 1.0R) / 2.0R))
            Dim z = Math.Sqrt(Math.Max(0.0R, (m.M33 + 1.0R) / 2.0R))
            Return New Vector3(CSng(x), CSng(y), CSng(z))
        End Function

        ''' <summary>
        ''' Normales de la malla de SIMULACION (`hclSimClothData.doNormals`, +0x14C), con el signo por
        ''' cara de `triangleFlips` (+0x68).
        '''
        ''' <para>La normal de un vertice es la suma de las de sus caras incidentes, SIN normalizar
        ''' cada una — asi cada cara pesa por su area, que es la convencion de todo el resto del
        ''' modulo. `triangleFlips[t] &lt;&gt; 0` significa que esa cara esta enrollada al reves respecto
        ''' de la convencion de la malla, y entonces su aporte va RESTANDO: sin eso una cara invertida
        ''' cancela a sus vecinas y el vertice queda con una normal girada o nula.</para>
        '''
        ''' <para>⚠️ Se recalculan DESPUES del solve y ANTES del writeback, que es donde el motor las
        ''' deja listas para los operadores que las consumen.</para>
        ''' </summary>
        Private Shared Sub ActualizarNormalesDeLaTela(st As ClothSimState)
            If Not st.HaceNormales Then Exit Sub
            If st.NormalesSim Is Nothing OrElse st.TrisSim Is Nothing OrElse st.TrisSim.Length < 3 Then Exit Sub
            Dim n = st.NormalesSim.Length
            For i = 0 To n - 1
                st.NormalesSim(i) = Vector3.Zero
            Next
            Dim P = st.Positions
            Dim nTri = st.TrisSim.Length \ 3
            For t = 0 To nTri - 1
                Dim a = st.TrisSim(t * 3), b = st.TrisSim(t * 3 + 1), c = st.TrisSim(t * 3 + 2)
                If a < 0 OrElse b < 0 OrElse c < 0 OrElse a >= n OrElse b >= n OrElse c >= n Then Continue For
                Dim cara = Vector3.Cross(P(b) - P(a), P(c) - P(a))
                If CaraInvertida(st, t) Then cara = -cara
                st.NormalesSim(a) += cara
                st.NormalesSim(b) += cara
                st.NormalesSim(c) += cara
            Next
            For i = 0 To n - 1
                Dim l = st.NormalesSim(i).Length
                If l > 0.000001F Then st.NormalesSim(i) /= l
            Next
        End Sub

        ''' <summary>
        ''' Colision con el TERRENO — `simulationInfo.landscapeCollisionEnabled` (+0x1D del info) mas
        ''' `landscapeCollisionData` (+0x134) y `numLandscapeCollidableParticles` (+0x148).
        '''
        ''' <para>El motor colisiona SOLO las primeras <c>numLandscapeCollidableParticles</c>
        ''' particulas — el array esta ordenado para que sean las de abajo — contra la superficie del
        ''' terreno, separandolas por <c>landscapeRadius</c>. MEDIDO: 33 sim-cloth del corpus lo
        ''' declaran, y ninguno llegaba al solver.</para>
        '''
        ''' <para>⚠️ LIMITE DECLARADO: el motor consulta la GEOMETRIA del terreno; esta app no la
        ''' tiene. Lo unico que puede sostener es un plano horizontal a la altura del punto mas bajo
        ''' del esqueleto vivo (los pies), que es donde el terreno esta cuando el actor esta parado.
        ''' Con el actor en el aire, esto NO es el terreno y por eso el plano se recalcula por frame en
        ''' vez de fijarse una vez.</para>
        ''' <para>`enableStuckParticleDetection` + `stuckParticlesStretchFactorSq`: una particula cuyo
        ''' link se estiro mas que ese factor (al cuadrado) se considera ENGANCHADA y el motor la
        ''' suelta del contacto en vez de seguir sosteniendola.</para>
        ''' </summary>
        Private Shared Sub SolveTerreno(st As ClothSimState)
            If Not st.TerrenoActivo Then Exit Sub
            Dim n = Math.Min(st.TerrenoParticulas, st.Positions.Length)
            If n <= 0 Then Exit Sub
            Dim piso = st.TerrenoAltura + st.TerrenoRadio
            For i = 0 To n - 1
                If st.InvMass(i) = 0.0F Then Continue For
                Dim pq = st.Positions(i)
                If pq.Z >= piso Then Continue For
                If st.TerrenoDetectaEnganche AndAlso EstaEnganchada(st, i) Then Continue For
                pq.Z = piso
                st.Positions(i) = pq
            Next
        End Sub

        ''' <summary>Una particula esta ENGANCHADA si alguno de sus links supera el factor de estiron
        ''' declarado. El campo viene al CUADRADO, asi que se compara contra el cuadrado del cociente y
        ''' no hace falta ninguna raiz.</summary>
        Private Shared Function EstaEnganchada(st As ClothSimState, particula As Integer) As Boolean
            If st.TerrenoFactorEngancheSq <= 0.0F OrElse st.Links Is Nothing Then Return False
            For Each lk In st.Links
                If lk.A <> particula AndAlso lk.B <> particula Then Continue For
                If lk.Rest <= 0.0001F Then Continue For
                Dim d2 = (st.Positions(lk.A) - st.Positions(lk.B)).LengthSquared
                If d2 > st.TerrenoFactorEngancheSq * lk.Rest * lk.Rest Then Return True
            Next
            Return False
        End Function

        ''' <summary>
        ''' DETECCION DE PELLIZCO — `simulationInfo.pinchDetectionEnabled` (+0x1C del info),
        ''' `perParticlePinchDetectionEnabledFlags` (+0x108), `collidablePinchingDatas` (+0x118) y el
        ''' rango `minPinchedParticleIndex`..`maxPinchedParticleIndex` (+0x128/+0x12A).
        '''
        ''' <para>El problema que resuelve: una particula atrapada ENTRE DOS colisionables recibe dos
        ''' correcciones opuestas y cada una la mete dentro del otro. El motor lo detecta contando
        ''' cuantos colisionables con pellizco habilitado la tienen dentro de su
        ''' <c>pinchDetectionRadius</c>; si son dos o mas, deja de repartir y la resuelve contra UNO
        ''' SOLO: el de <c>pinchDetectionPriority</c> mas alta. Asi la particula sale por un lado en vez
        ''' de quedar vibrando entre los dos.</para>
        '''
        ''' <para>El armado de la lista se ve literal en <c>0x141A71893</c>: por cada colisionable
        ''' recorre TODAS las particulas, filtra por `staticCollisionMasks` y despues separa en DOS
        ''' listas segun el byte de `perParticlePinchDetectionEnabledFlags` — una para el camino con
        ''' pellizco y otra para el normal. Y <c>CollideAndSolve</c> (0x141A697BF) solo entra al camino
        ''' con pellizco si hay MAS DE UN colisionable habilitado, que es la misma condicion de aca.</para>
        ''' </summary>
        Private Shared Sub SolvePellizco(st As ClothSimState)
            If Not st.PinchActivo OrElse st.Capsules.Count = 0 Then Exit Sub
            ' "mas de un colisionable con pellizco habilitado", igual que el motor.
            Dim habilitados = st.Capsules.Where(Function(x) x.PinchHabilitado).Count()
            If habilitados <= 1 Then Exit Sub

            Dim desde = Math.Max(0, st.PinchMin)
            Dim hasta = Math.Min(st.Positions.Length - 1, If(st.PinchMax > 0, st.PinchMax, st.Positions.Length - 1))
            For i = desde To hasta
                If st.InvMass(i) = 0.0F Then Continue For
                If st.PinchPorParticula IsNot Nothing AndAlso i < st.PinchPorParticula.Length AndAlso
                   st.PinchPorParticula(i) = 0 Then Continue For
                Dim pq = st.Positions(i)
                Dim mask = st.MascaraColision(i)
                Dim cuantos = 0, mejorPrio = Integer.MinValue, mejor = -1
                Dim mejorObjetivo = 0.0F
                For ci = 0 To st.Capsules.Count - 1
                    Dim cap = st.Capsules(ci)
                    If Not cap.PinchHabilitado Then Continue For
                    If cap.Bit <> 0UI AndAlso (mask And cap.Bit) = 0UI Then Continue For
                    Dim tq = 0.0F
                    Dim cl = ClosestPointOnSegment(pq, cap.A, cap.B, tq)
                    Dim rr = cap.Radius + ((cap.RadiusB - cap.Radius) * tq) + cap.PinchRadio
                    If (pq - cl).Length >= rr Then Continue For
                    cuantos += 1
                    If cap.PinchPrioridad > mejorPrio Then
                        mejorPrio = cap.PinchPrioridad
                        mejor = ci
                        mejorObjetivo = cap.Radius + ((cap.RadiusB - cap.Radius) * tq) + st.Radius(i)
                    End If
                Next
                If cuantos < 2 OrElse mejor < 0 Then Continue For
                ' Pellizcada: se resuelve contra el de mayor prioridad y NADA MAS.
                Dim capG = st.Capsules(mejor)
                Dim tg = 0.0F
                Dim clg = ClosestPointOnSegment(pq, capG.A, capG.B, tg)
                Dim dg = pq - clg
                Dim lg = dg.Length
                Dim ng = If(lg <= 0.000001F, New Vector3(0.0F, 0.0F, 1.0F), dg / lg)
                st.Positions(i) = clg + ng * mejorObjetivo
            Next
        End Sub

        ''' <summary>
        ''' `antiPinchConstraintSets` (+0xC8). El motor los resuelve DESPUES de los contactos, con
        ''' k = 1.0 fijo — leido de `CollideAndSolve` (0x141A69730): recorre el array y llama al `solve`
        ''' virtual (+0x48) de cada uno con la constante 1.0 de 0x142929458, sin pasar por
        ''' `StiffnessFactor`. Es el unico paso que puede REPARAR lo que la colision rompio.
        ''' <para>⚠️ MEDIDO: el corpus vanilla de FO4 no declara ninguno (0 de 1.854 sim-cloth), asi que
        ''' hoy este bucle no corre. Se implementa igual porque el campo existe y un mod puede traerlos:
        ''' la alternativa era seguir ignorando un array que el motor SI ejecuta.</para>
        ''' </summary>
        Private Shared Sub SolveAntiPinch(st As ClothSimState, skeleton As SkeletonInstance,
                                          skinned As Dictionary(Of Integer, Vector3),
                                          normalesRef As Dictionary(Of Integer, Vector3))
            ' ⛔⛔ LA MISMA PUERTA QUE EL PELLIZCO. `antiPinchConstraintSets` (+0xC8) es un array
            ' APARTE de `staticConstraintSets` (+0xB8): el bucle del solve recorre el segundo y no lo
            ' toca nunca. Al primero se llega SOLO por el camino de pellizco de `0x141A69730`, que
            ' arranca con dos condiciones duras:
            '     0x141A697BF : si `simulationInfo.pinchDetectionEnabled` esta apagado, no hace nada
            '     0x141A69808 : si hay UNO o CERO colisionables con `pinchDetectionEnabled`, tampoco
            ' Esto corria SIEMPRE, y encima con `k = 1,0` fijo: una fuerza que el archivo no pidio,
            ' a rigidez plena, sobre una malla que ya venia justa. MEDIDO: el vestido y el pelo traen
            ' `pinchDetectionEnabled = False`, o sea que en el motor estos sets estan INERTES.
            If Not st.PinchActivo OrElse st.BloquesAntiPinch Is Nothing OrElse st.BloquesAntiPinch.Count = 0 Then Exit Sub
            If st.Capsules.Where(Function(x) x.PinchHabilitado).Count() <= 1 Then Exit Sub
            For Each blk In st.BloquesAntiPinch
                Select Case blk.Kind
                    Case ConstraintKind.Distance : SolveDistanceLinks(st, blk.Start, blk.Count, 1.0F)
                    Case ConstraintKind.Stretch : SolveStretchLinks(st, blk.Start, blk.Count, 1.0F)
                    Case ConstraintKind.Bend : SolveBend(st, blk.Start, blk.Count, 1.0F)
                    Case ConstraintKind.BendLink : SolveBendLinks(st, blk.Start, blk.Count, 1.0F)
                    Case ConstraintKind.Compressible : SolveCompressibleLinks(st, blk.Start, blk.Count, 1.0F)
                    Case ConstraintKind.BonePlane : SolveBonePlanes(st, skeleton, blk.Start, blk.Count, 1.0F)
                    Case ConstraintKind.LocalRange : SolveLocalRange(st, skinned, normalesRef, blk.Start, blk.Count, 1.0F)
                End Select
            Next
        End Sub


        ''' <summary>
        ''' ⛔⛔ EL MOTOR NO CORRE UNA SECUENCIA FIJA: CORRE LA CADENA QUE DECLARA EL ARCHIVO.
        '''
        ''' <para>`hclClothState.operators` es una lista de INDICES a `hclClothData.operators`, y
        ''' `Execute Operators` los despacha uno por uno en ese orden a traves de
        ''' `hclOperatorDispatcherCpu`. Esta app tenia una secuencia hardcodeada —
        ''' skin → destinos → simular → writeback — que era una INVENCION: coincide con la cadena de
        ''' algunas prendas y no con la de otras, pierde el orden y pierde los operadores repetidos.</para>
        '''
        ''' <para>Las 5 formas que declara el corpus FO4 (censadas sobre 989 prendas / 3.704 estados):</para>
        ''' <code>
        '''   1852x  skinPN -> moveParticles -> simulate -> meshBoneDeform      (estado 'Simulate')
        '''   1298x  skinPN -> copyVertices  -> meshBoneDeform                  (estado 'Animate')
        '''    546x  skinPN -> gatherAll     -> meshBoneDeform                  (estado 'Animate')
        '''      4x  skinPN -> skinPN -> moveParticles -> simulate -> meshBoneDeform
        '''      4x  skinPN -> skinPN -> gatherSome -> gatherAll -> meshBoneDeform
        ''' </code>
        ''' <para>Los estados se llaman literalmente `Simulate` y `Animate`, y el motor elige entre
        ''' ellos por LOD y por estado del actor (`bAnimClothLOD:Cloth`, `bAnimateClothOnDead:Cloth`,
        ''' `bAnimateClothOnSitSleep:Cloth`, …). Aca: `FullSimulation` corre el estado que TIENE
        ''' `hclSimulateOperator`; `DeformOnly` corre el que NO lo tiene, que es exactamente lo que el
        ''' motor hace con la simulacion apagada.</para>
        ''' </summary>
        Private Shared Sub EjecutarCadena(cfg As HclClothConfigGraph_Class, st As ClothSimState,
                                          sim As HclSimClothDataDetail_Class,
                                          clothSkel As Havok.Canon.Objects.HkObj_HkaSkeleton,
                                          bindWorld As Matrix4(), skeleton As SkeletonInstance,
                                          particleCount As Integer, dt As Single)
            Dim estado = ElegirEstado(cfg)
            If estado Is Nothing Then Exit Sub

            If Logger.Enabled Then
                Dim nm = estado.Name
                Dim clases = String.Join(" -> ", estado.OperatorIndices.
                    Select(Function(i) If(i >= 0 AndAlso i < cfg.OperadoresEnOrden.Count AndAlso cfg.OperadoresEnOrden(i) IsNot Nothing,
                                          cfg.OperadoresEnOrden(i).GetType().Name.Replace("Detail_Class", "").Replace("Graph_Class", ""),
                                          "<no implementado>")))
                Dim bufs = If(st.Buffers Is Nothing, "(sin buffers)",
                              String.Join(" ", st.Buffers.Select(Function(b, k) $"[{k}]{If(b Is Nothing, "nulo", CStr(b.Length))}{If(k = st.BufSim, "*", "")}")))
                Logger.LogLazy(Function() $"[CLOTH-CADENA] estado '{nm}': {clases}")
                Logger.LogLazy(Function() $"[CLOTH-BUFS] bufSim={st.BufSim} · {bufs}   (* = alias del array de particulas)")
            End If

            ' ⛔⛔ DE DONDE SALEN LAS PARTICULAS LA PRIMERA VEZ.
            '
            ' La cadena de `Simulate` NO llena todas las particulas: `hclMoveParticlesOperator` coloca
            ' solo las ancladas (33 de 321 en el vestido, 22 de 113 en el pelo) y el resto las simula
            ' desde donde ya estaban. En el motor "donde ya estaban" es real: la prenda venia corriendo
            ' el estado `Animate`, que con `gatherAll`/`copyVertices` llena las 321 desde el buffer
            ' skinneado, y al cambiar de estado (`TtSwitchClothState`) el buffer se conserva.
            '
            ' Asi que sembrar = correr la cadena del estado SIN simulacion una vez. No hace falta
            ' inventar una posicion inicial: el archivo ya dice como se llena el buffer.
            If Not st.Seeded Then
                Dim previo = EstadoSinSimulacion(cfg)
                If previo IsNot Nothing AndAlso Not ReferenceEquals(previo, estado) Then
                    For Each idx0 In previo.OperatorIndices
                        If idx0 < 0 OrElse idx0 >= cfg.OperadoresEnOrden.Count Then Continue For
                        Dim op0 = cfg.OperadoresEnOrden(idx0)
                        ' El deform no: sembrar no escribe huesos.
                        If op0 Is Nothing OrElse TypeOf op0 Is HclSimpleMeshBoneDeformOperatorGraph_Class Then Continue For
                        EjecutarOperador(op0, cfg, st, sim, clothSkel, bindWorld, skeleton, particleCount, dt)
                    Next
                End If
            End If

            For Each idx In estado.OperatorIndices
                If idx < 0 OrElse idx >= cfg.OperadoresEnOrden.Count Then Continue For
                Dim op = cfg.OperadoresEnOrden(idx)
                If op Is Nothing Then Continue For
                EjecutarOperador(op, cfg, st, sim, clothSkel, bindWorld, skeleton, particleCount, dt)
            Next
        End Sub

        ''' <summary>El estado que NO declara `hclSimulateOperator` — el que llena todas las particulas
        ''' desde la piel. Es la fuente de la siembra.</summary>
        Private Shared Function EstadoSinSimulacion(cfg As HclClothConfigGraph_Class) As HclClothStateDetail_Class
            If cfg.ClothStates Is Nothing Then Return Nothing
            For Each e In cfg.ClothStates
                If e Is Nothing Then Continue For
                Dim tiene = e.OperatorIndices.Any(
                    Function(i) i >= 0 AndAlso i < cfg.OperadoresEnOrden.Count AndAlso
                                TypeOf cfg.OperadoresEnOrden(i) Is HclSimulateOperatorDetail_Class)
                If Not tiene Then Return e
            Next
            Return Nothing
        End Function

        ''' <summary>
        ''' El estado a correr. `FullSimulation` ⇒ el que declara `hclSimulateOperator`; `DeformOnly`
        ''' ⇒ el que no. Si la prenda declara uno solo, ese.
        ''' </summary>
        Private Shared Function ElegirEstado(cfg As HclClothConfigGraph_Class) As HclClothStateDetail_Class
            If cfg.ClothStates Is Nothing OrElse cfg.ClothStates.Count = 0 Then Return Nothing
            Dim quiereSim = HavokPhysicsSettings.Mode = HavokPhysicsMode.FullSimulation
            Dim conSim As HclClothStateDetail_Class = Nothing
            Dim sinSim As HclClothStateDetail_Class = Nothing
            For Each estado In cfg.ClothStates
                If estado Is Nothing Then Continue For
                Dim tiene = estado.OperatorIndices.Any(
                    Function(i) i >= 0 AndAlso i < cfg.OperadoresEnOrden.Count AndAlso
                                TypeOf cfg.OperadoresEnOrden(i) Is HclSimulateOperatorDetail_Class)
                If tiene Then
                    If conSim Is Nothing Then conSim = estado
                ElseIf sinSim Is Nothing Then
                    sinSim = estado
                End If
            Next
            If quiereSim Then Return If(conSim, sinSim)
            Return If(sinSim, conSim)
        End Function

        ''' <summary>Un operador de la cadena. El despacho es por CLASE porque `hclOperator.type` viene
        ''' en cero en el archivo (el motor lo asigna en runtime desde su registro).</summary>
        Private Shared Sub EjecutarOperador(op As Object, cfg As HclClothConfigGraph_Class, st As ClothSimState,
                                            sim As HclSimClothDataDetail_Class,
                                            clothSkel As Havok.Canon.Objects.HkObj_HkaSkeleton,
                                            bindWorld As Matrix4(), skeleton As SkeletonInstance,
                                            particleCount As Integer, dt As Single)
            Dim skin = TryCast(op, HclObjectSpaceSkinPNOperatorGraph_Class)
            If skin IsNot Nothing Then
                OpSkinPN(skin, st, clothSkel, bindWorld, skeleton)
                Exit Sub
            End If
            Dim copia = TryCast(op, HclCopyVerticesOperatorDetail_Class)
            If copia IsNot Nothing Then
                OpCopyVertices(copia, st)
                Exit Sub
            End If
            Dim gAll = TryCast(op, HclGatherAllVerticesOperatorDetail_Class)
            If gAll IsNot Nothing Then
                OpGatherAll(gAll, st)
                Exit Sub
            End If
            Dim gSome = TryCast(op, HclGatherSomeVerticesOperatorDetail_Class)
            If gSome IsNot Nothing Then
                OpGatherSome(gSome, st)
                Exit Sub
            End If
            Dim mover = TryCast(op, HclMoveParticlesOperatorDetail_Class)
            If mover IsNot Nothing Then
                OpMoveParticles(mover, st)
                Exit Sub
            End If
            Dim simu = TryCast(op, HclSimulateOperatorDetail_Class)
            If simu IsNot Nothing Then
                OpSimulate(st, sim, cfg, clothSkel, skeleton, particleCount, dt)
                Exit Sub
            End If
            Dim deform = TryCast(op, HclSimpleMeshBoneDeformOperatorGraph_Class)
            If deform IsNot Nothing Then
                ActualizarNormalesDeLaTela(st)
                If Logger.Enabled Then
                    Dim nm2 = If(deform.BoneMappings Is Nothing, -1, deform.BoneMappings.Count)
                    Dim nb2 = If(deform.BindMatrices Is Nothing, -1, deform.BindMatrices.Count)
                    Logger.LogLazy(Function() $"[CLOTH-OPDEF] deform: pares={nm2} bindMatrices={nb2} bufferIn={deform.InputBufferIndex}")
                End If
                WriteBackDeform(deform, st, skeleton)
                Exit Sub
            End If
            If Logger.Enabled Then
                ' ⛔ Un operador de la cadena que NO matchea ninguna clase es un hueco: el archivo lo
                ' declara y aca no pasa nada. Callarlo es la forma de que la cadena parezca completa.
                Dim tn = op.GetType().Name
                Logger.LogLazy(Function() $"[CLOTH-OPDESC] operador de la cadena SIN ejecutar: {tn}")
            End If
        End Sub

        ''' <summary>`hclObjectSpaceSkinPNOperator`: skinnea la malla y ESCRIBE su `outputBufferIndex`.
        ''' Una prenda puede declarar dos, a buffers distintos — por eso el destino sale del operador y
        ''' no de una propiedad singular.</summary>
        Private Shared Sub OpSkinPN(skin As HclObjectSpaceSkinPNOperatorGraph_Class, st As ClothSimState,
                                    clothSkel As Havok.Canon.Objects.HkObj_HkaSkeleton,
                                    bindWorld As Matrix4(), skeleton As SkeletonInstance)
            Dim normales As New Dictionary(Of Integer, Vector3)
            Dim skinned = BuildSkinnedByVertex(skin, clothSkel, bindWorld, skeleton, normales)
            Dim b = skin.OutputBufferIndex
            If st.Buffers Is Nothing OrElse b < 0 OrElse b >= st.Buffers.Length OrElse st.Buffers(b) Is Nothing Then Exit Sub
            Dim dst = st.Buffers(b)
            Dim dstN = st.NormalesBuf(b)
            For Each kv In skinned
                If kv.Key < 0 OrElse kv.Key >= dst.Length Then Continue For
                dst(kv.Key) = kv.Value
            Next
            If dstN IsNot Nothing Then
                For Each kv In normales
                    If kv.Key < 0 OrElse kv.Key >= dstN.Length Then Continue For
                    dstN(kv.Key) = kv.Value
                Next
            End If
            ' La piel tambien es la referencia de la correa y de las anclas.
            st.Skinned = skinned
            st.NormalesRef = normales
        End Sub

        ''' <summary>`hclCopyVerticesOperator`: <c>out[startOut + i] = in[startIn + i]</c>.</summary>
        Private Shared Sub OpCopyVertices(c As HclCopyVerticesOperatorDetail_Class, st As ClothSimState)
            Dim src = BufferDe(st, c.InputBufferIdx)
            Dim dst = BufferDe(st, c.OutputBufferIdx)
            If src Is Nothing OrElse dst Is Nothing Then Exit Sub
            Dim srcN = NormalDe(st, c.InputBufferIdx), dstN = NormalDe(st, c.OutputBufferIdx)
            For i = 0 To c.NumberOfVertices - 1
                Dim a = c.StartVertexIn + i, b = c.StartVertexOut + i
                If a < 0 OrElse b < 0 OrElse a >= src.Length OrElse b >= dst.Length Then Continue For
                dst(b) = src(a)
                If c.CopyNormals AndAlso srcN IsNot Nothing AndAlso dstN IsNot Nothing Then dstN(b) = srcN(a)
            Next
        End Sub

        ''' <summary>`hclGatherAllVerticesOperator`: <c>out[i] = in[vertexInputFromVertexOutput[i]]</c>,
        ''' saltando los indices negativos. El array se indexa por vertice de SALIDA.</summary>
        Private Shared Sub OpGatherAll(g As HclGatherAllVerticesOperatorDetail_Class, st As ClothSimState)
            Dim src = BufferDe(st, g.InputBufferIdx)
            Dim dst = BufferDe(st, g.OutputBufferIdx)
            If src Is Nothing OrElse dst Is Nothing Then Exit Sub
            Dim srcN = NormalDe(st, g.InputBufferIdx), dstN = NormalDe(st, g.OutputBufferIdx)
            Dim map = g.GatheredVertexIndices
            For i = 0 To Math.Min(map.Count, dst.Length) - 1
                Dim j = CInt(map(i))
                If j < 0 OrElse j >= src.Length Then Continue For
                dst(i) = src(j)
                If g.GatherNormals AndAlso srcN IsNot Nothing AndAlso dstN IsNot Nothing Then dstN(i) = srcN(j)
            Next
        End Sub

        ''' <summary>`hclGatherSomeVerticesOperator`: los mismos pares, pero explicitos.</summary>
        Private Shared Sub OpGatherSome(g As HclGatherSomeVerticesOperatorDetail_Class, st As ClothSimState)
            Dim src = BufferDe(st, g.InputBufferIdx)
            Dim dst = BufferDe(st, g.OutputBufferIdx)
            If src Is Nothing OrElse dst Is Nothing OrElse g.Pairs Is Nothing Then Exit Sub
            For Each pr In g.Pairs
                Dim a = CInt(pr.Source), b = CInt(pr.Target)
                If a < 0 OrElse b < 0 OrElse a >= src.Length OrElse b >= dst.Length Then Continue For
                dst(b) = src(a)
            Next
        End Sub

        ''' <summary>
        ''' `hclMoveParticlesOperator` — "move SOME particles" es literal: coloca SOLO las particulas
        ''' que el archivo lista, leyendo del buffer `refBufferIdx`. Las demas NO se tocan: las simula.
        ''' <para>MEDIDO: 33 de 321 en el vestido, 22 de 113 en el pelo.</para>
        ''' </summary>
        Private Shared Sub OpMoveParticles(m As HclMoveParticlesOperatorDetail_Class, st As ClothSimState)
            Dim src = BufferDe(st, m.RefBufferIdx)
            If src Is Nothing OrElse m.Pairs Is Nothing Then Exit Sub
            For Each pr In m.Pairs
                Dim v = CInt(pr.VertexIndex), q = CInt(pr.ParticleIndex)
                If v < 0 OrElse v >= src.Length OrElse q < 0 OrElse q >= st.Positions.Length Then Continue For
                ' ⛔ SOLO la posicion. Escribir tambien `Previous` borra donde estaba el ancla el
                ' frame anterior, y eso es justo lo que `hclSimulateOperator` necesita para
                ' interpolarla a lo largo de los substeps (0x14195C870 la guarda, 0x14195CB88 la
                ' interpola). Si las dos son iguales esa interpolacion es identidad y el ancla salta
                ' de golpe: el solver no llega a repartir el estiron y la malla se abre.
                st.Positions(q) = src(v)
            Next
        End Sub

        Private Shared Function BufferDe(st As ClothSimState, i As Integer) As Vector3()
            If st.Buffers Is Nothing OrElse i < 0 OrElse i >= st.Buffers.Length Then Return Nothing
            Return st.Buffers(i)
        End Function

        Private Shared Function NormalDe(st As ClothSimState, i As Integer) As Vector3()
            If st.NormalesBuf Is Nothing OrElse i < 0 OrElse i >= st.NormalesBuf.Length Then Return Nothing
            Return st.NormalesBuf(i)
        End Function

        ''' <summary>Asentamiento inicial: `uNumSimSettleSteps` = 10 pasos con gravedad, sin avanzar el reloj.</summary>
        Private Shared Sub Settle(st As ClothSimState, skeleton As SkeletonInstance, dt As Single)
            Dim steps = Math.Max(0, HavokPhysicsSettings.SettleSteps)
            For s = 0 To steps - 1
                Simulate(st, skeleton, dt)
            Next
        End Sub

        ''' <summary>
        ''' `hclStandardLinkConstraintSet` — el link de distancia. Ley leida de <c>0x141A06170</c>
        ''' (se llega por la cadena <c>"TtSolve Links"</c> en <c>0x142718DF0</c>). El struct del link
        ''' mide <b>12 bytes</b>: <c>uint16 particleA, uint16 particleB, real restLength, real stiffness</c>.
        ''' <code>
        '''     d  = P[B] − P[A]
        '''     c  = (|d| − restLength) · stiffness · k · d̂
        '''     P[A] += invMass[A] · c
        '''     P[B] −= invMass[B] · c
        ''' </code>
        '''
        ''' <para>⛔⛔ NO HAY NORMALIZACION POR MASA. Esta implementacion hacia el reparto clasico de
        ''' PBD — <c>wa/(wa+wb)</c> y <c>wb/(wa+wb)</c> — que es lo que dice cualquier paper y NO es lo
        ''' que hace el motor: cada particula se mueve por SU PROPIA <c>invMass</c>, sin dividir. Con
        ''' dos particulas libres de masa 1 el PBD reparte medio y medio y el motor mueve una unidad
        ''' entera a cada una: el doble de correccion por iteracion.</para>
        '''
        ''' <para>La guarda de <c>|d|² &lt;= 0</c> es la del motor (<c>cmpleps</c> + <c>andnps</c> sobre
        ''' el <c>rsqrt</c>): con longitud cero la direccion queda en cero y el link no aporta, en vez
        ''' de dividir por cero.</para>
        ''' </summary>
        ''' <summary>
        ''' `StiffnessFactor` — <c>0x1418C6420</c>. El multiplicador que el motor le pasa al `solve` de
        ''' CADA constraint set, y que esta app venia dando por 1,0 sin serlo.
        '''
        ''' <para>⛔⛔ POR QUE ESTABA MAL Y COMO SE VEIA. La transcripcion anterior leyo el registro
        ''' <c>ebx</c> de esa funcion como `numberOfSolveIterations` (que vale 1 en todo el corpus) y
        ''' concluyo que las tres ramas daban 1,0. <c>ebx</c> es <b>`subSteps`</b>: el llamador
        ''' (<c>0x141A137B3</c>) carga ahi <c>[rsp+0xC0]</c>, que es <c>hclSimulateOperator.subSteps</c>.
        ''' Con `subSteps` = 3 la rama por defecto da <c>3^-1,725 = 0,1503</c>, no 1.</para>
        '''
        ''' <para>La diferencia no es de matiz. El `solve` del motor NO normaliza por masa: escribe
        ''' <c>P[A] += invMass[A]·(|d|−rest)·stiffness·k</c>. MEDIDO en el vestido vanilla:
        ''' <c>invMass = 14,4</c> y <c>stiffness ∈ {0,03472, 0,06944} = {½·masa, 1·masa}</c>, o sea
        ''' <c>invMass × stiffness ∈ {0,5, 1,0}</c>. Con k = 1 el error de un link queda multiplicado
        ''' por <c>1 − (invA+invB)·s = 1 − 2 = −1</c> en cada pasada: <b>cambia de signo y no se
        ''' achica nunca</b>. Ese es el sintoma que lo delato — la violacion media de links se quedaba
        ''' clavada en 28 % con substeps 3, 12 y 48 por igual, y un Gauss-Seidel que no mejora con 16×
        ''' de trabajo no esta convergiendo, esta oscilando. Con k = 0,1503 el factor es
        ''' <c>1 − 0,30 = 0,70</c> y converge.</para>
        '''
        ''' <para>La funcion, literal:</para>
        ''' <code>
        '''     si mode &lt;&gt; 2                          -> 1.0
        '''     si subSteps = 1 y s1 = 1 y s2 = 1        -> 1.0
        '''     segun cs->type:
        '''         5 (LocalRange) o 10 (BonePlanes) -> 1.0 SOLO en el ultimo substep, 0.0 en los demas
        '''         8 (Transition)                   -> (substep + 1) / subSteps
        '''         resto                            -> (subSteps · s1 · s2) ^ -1,725
        ''' </code>
        ''' <para>`mode` sale de <c>simCloth[+0x1CC]</c>, que el motor inicializa en <b>1</b>
        ''' (<c>0x1418C66F4</c>, junto con <c>s1 = s2 = 1,0</c> en +0x1D0/+0x1D4), y pasa a 2 cuando el
        ''' operador declara `adaptConstraintStiffness`. MEDIDO: 846 de los 1.248 operadores del corpus
        ''' lo declaran.</para>
        ''' <para>⚠️ `s1`/`s2` NO estan serializados: los pone el motor con la ESCALA DEL ACTOR cuando
        ''' `bEnableScaling:Cloth` esta en 1. El preview renderiza a escala 1, asi que valen 1; si algun
        ''' dia se renderiza un actor escalado hay que traerlos de ahi y NO dejarlos fijos.</para>
        ''' </summary>
        Friend Shared Function FactorDeRigidez(tipo As Integer, adapt As Boolean,
                                               subSteps As Integer, substep As Integer) As Single
            ' s1 y s2 valen 1,0 mientras no se simule un actor escalado (ver el aviso de arriba).
            Const S1 As Single = 1.0F
            Const S2 As Single = 1.0F
            If Not adapt Then Return 1.0F
            If subSteps = 1 AndAlso S1 = 1.0F AndAlso S2 = 1.0F Then Return 1.0F
            Select Case tipo
                Case TipoDeMotor.LocalRange, TipoDeMotor.BonePlanes
                    Return If(substep = subSteps - 1, 1.0F, 0.0F)
                Case TipoDeMotor.Transition
                    Return CSng(substep + 1) / CSng(Math.Max(1, subSteps))
                Case Else
                    Return CSng(Math.Pow(CDbl(subSteps) * S1 * S2, -1.725R))
            End Select
        End Function

        Private Shared Sub SolveDistanceLinks(st As ClothSimState, start As Integer, count As Integer, k As Single)
            Dim P = st.Positions
            Dim inv = st.InvMass
            For iL = start To start + count - 1
                Dim l = st.Links(iL)
                Dim d = P(l.B) - P(l.A)
                Dim d2 = d.LengthSquared()
                If d2 <= 0.0F Then Continue For
                Dim len = CSng(Math.Sqrt(d2))
                Dim dir = d / len
                Dim c = dir * ((len - l.Rest) * l.Stiffness * k)
                P(l.A) += c * inv(l.A)
                P(l.B) -= c * inv(l.B)
            Next
        End Sub

        ''' <summary>
        ''' `hclBonePlanesConstraintSet` — un plano pegado a un hueso, por particula. Ley leida de
        ''' <c>0x1419FCB80</c>, al que se llega por RTTI: el nombre <c>.?AVhclBonePlanesConstraintSet@@</c>
        ''' da el TypeDescriptor, su referencia da el CompleteObjectLocator, y el puntero al COL esta
        ''' justo ANTES de la vtable (<c>0x1426FE950</c>), cuyo <c>+0x48</c> es el <c>solve</c>. Hizo
        ''' falta ese camino porque este set NO tiene cadena de temporizador propia.
        '''
        ''' <para>El struct mide <b>32 bytes</b>: <c>vector4 planeEquationBone; uint16 particleIndex,
        ''' transformIndex; real stiffness</c>. El cuerpo (rama <c>0x1419FCBD0</c>):</para>
        ''' <code>
        '''     M = transformSet[transformIndex]                       ' matriz 4x4 del hueso
        '''     n = plane.x·M.fila0 + plane.y·M.fila1 + plane.z·M.fila2 ' la normal al mundo, SIN traslacion
        '''     o = M.fila3                                            ' el origen del hueso
        '''     s = dot(P − o, n) + plane.w                            ' distancia con signo al plano
        '''     si s &lt; 0:  P += (−s) · stiffness · k · n              ' UNILATERAL
        ''' </code>
        ''' <para>El "si s &lt; 0" en el binario no es un salto: es una mascara de signo
        ''' (<c>psrad xmm3, 0x1F</c>) y un <c>andps</c>/<c>andnps</c>/<c>orps</c> que elige entre la
        ''' posicion corregida y la original. Sin rama, mismo resultado.</para>
        ''' <para>⚠️ La app no modela el transform-set del motor: resuelve <c>transformIndex</c> contra
        ''' el esqueleto de la prenda y usa el hueso vivo. Si el indice no cae ahi, la constraint se
        ''' saltea y se loguea, en vez de aplicar el plano de OTRO hueso.</para>
        ''' </summary>
        Private Shared Sub SolveBonePlanes(st As ClothSimState, skeleton As SkeletonInstance,
                                           start As Integer, count As Integer, k As Single)
            If skeleton Is Nothing Then Exit Sub
            Dim P = st.Positions
            For i = start To start + count - 1
                Dim c = st.BonePlanes(i)
                If st.InvMass(c.Particle) = 0.0F Then Continue For
                Dim hueso As HierarchiBone_class = Nothing
                If Not skeleton.SkeletonDictionary.TryGetValue(c.BoneName, hueso) OrElse hueso Is Nothing Then Continue For
                Dim g = hueso.GetGlobalTransform
                If g Is Nothing Then Continue For
                Dim m = g.ToMatrix4()
                ' La normal al mundo: SOLO la parte de rotacion, sin la fila de traslacion.
                Dim n As New Vector3(
                    c.Nx * m.M11 + c.Ny * m.M21 + c.Nz * m.M31,
                    c.Nx * m.M12 + c.Ny * m.M22 + c.Nz * m.M32,
                    c.Nx * m.M13 + c.Ny * m.M23 + c.Nz * m.M33)
                Dim o As New Vector3(m.M41, m.M42, m.M43)
                Dim sdist = Vector3.Dot(P(c.Particle) - o, n) + c.D
                If sdist >= 0.0F Then Continue For
                P(c.Particle) += n * (-sdist * c.Stiffness * k)
            Next
        End Sub

        ''' <summary>
        ''' `hclBendLinkConstraintSet` — un link de RANGO con dos topes y dos rigideces. Ley leida de
        ''' <c>0x1419F8F70</c> (cadena <c>"TtSolve Bend Links"</c> en <c>0x142717810</c>). El struct
        ''' mide <b>20 bytes</b>: <c>uint16 particleA, particleB; real bendMinLength, stretchMaxLength,
        ''' bendStiffness, stretchStiffness</c>.
        ''' <code>
        '''     d = P[B] − P[A] ; L = |d|
        '''     c = ( max(0, L − stretchMaxLength) · stretchStiffness
        '''         − max(0, bendMinLength − L)   · bendStiffness ) · k · d̂
        '''     P[A] += invMass[A] · c
        '''     P[B] −= invMass[B] · c
        ''' </code>
        ''' <para>Los dos términos son unilaterales (<c>maxps</c> contra cero) y de SIGNO OPUESTO: uno
        ''' frena el estirón por arriba de <c>stretchMaxLength</c> y el otro empuja hacia afuera por
        ''' debajo de <c>bendMinLength</c>. Entre los dos topes no hace nada.</para>
        ''' </summary>
        Private Shared Sub SolveBendLinks(st As ClothSimState, start As Integer, count As Integer, k As Single)
            Dim P = st.Positions
            Dim inv = st.InvMass
            For i = start To start + count - 1
                Dim l = st.BendLinks(i)
                Dim d = P(l.B) - P(l.A)
                Dim d2 = d.LengthSquared()
                If d2 <= 0.0F Then Continue For
                Dim len = CSng(Math.Sqrt(d2))
                Dim dir = d / len
                Dim estiron = len - l.Max
                If estiron < 0.0F Then estiron = 0.0F
                Dim compres = l.Min - len
                If compres < 0.0F Then compres = 0.0F
                Dim c = dir * (((estiron * l.StiffMax) - (compres * l.StiffMin)) * k)
                P(l.A) += c * inv(l.A)
                P(l.B) -= c * inv(l.B)
            Next
        End Sub

        ''' <summary>
        ''' `hclCompressibleLinkConstraintSet` — la pareja confinada a un INTERVALO. Ley leida de
        ''' <c>0x1419FE850</c> (cadena <c>"TtSolve Compressible Links"</c> en <c>0x142718270</c>).
        ''' Struct de <b>16 bytes</b>: <c>uint16 particleA, particleB; real restLength,
        ''' compressionLength, stiffness</c>.
        ''' <code>
        '''     L = |P[B] − P[A]|
        '''     objetivo = L
        '''     si L &gt; restLength         ⇒ objetivo = restLength
        '''     si compressionLength &gt; L  ⇒ objetivo = compressionLength
        '''     si objetivo = L           ⇒ no hace nada
        '''     c = (L − objetivo) · stiffness · k · d̂
        ''' </code>
        ''' <para>⛔ El orden de las dos comparaciones es el del binario (<c>ucomiss</c> +
        ''' <c>seta</c> + dos saltos): la de COMPRESION gana si las dos aplican, que solo puede pasar
        ''' con datos incoherentes (<c>compressionLength &gt; restLength</c>). Se replica en vez de
        ''' "corregirlo", porque corregirlo seria inventar otra ley.</para>
        ''' </summary>
        Private Shared Sub SolveCompressibleLinks(st As ClothSimState, start As Integer, count As Integer, k As Single)
            Dim P = st.Positions
            Dim inv = st.InvMass
            For i = start To start + count - 1
                Dim l = st.Compressible(i)
                Dim d = P(l.B) - P(l.A)
                Dim d2 = d.LengthSquared()
                If d2 <= 0.0F Then Continue For
                Dim len = CSng(Math.Sqrt(d2))
                Dim objetivo = len
                If len > l.Max Then objetivo = l.Max
                If l.Min > len Then objetivo = l.Min
                If objetivo = len Then Continue For
                Dim c = (d / len) * ((len - objetivo) * l.StiffMin * k)
                P(l.A) += c * inv(l.A)
                P(l.B) -= c * inv(l.B)
            Next
        End Sub

        ''' <summary>
        ''' `hclStretchLinkConstraintSet` — y NO es un link de distancia con otro nombre. Ley leida de
        ''' <c>0x141A06DB0</c> (cadena <c>"TtSolve Stretch Links"</c> en <c>0x142719040</c>), mismo
        ''' struct de 12 bytes:
        ''' <code>
        '''     d   = P[B] − P[A]
        '''     err = min(restLength − |d|, 0)        ' ⛔ UNILATERAL
        '''     P[B] += err · stiffness · k · d̂        ' ⛔ SOLO B, y SIN invMass
        ''' </code>
        '''
        ''' <para>Las tres diferencias contra el link estandar son deliberadas y estaban las tres mal
        ''' acá, porque los dos sets se metian en la misma lista y se resolvian con la misma funcion:</para>
        ''' <list type="number">
        ''' <item><b>Es unilateral</b> (<c>minps</c> contra cero): solo actua cuando la arista se ESTIRO
        ''' de mas. Comprimida no hace nada. Tratarla como bilateral le mete a la tela una rigidez a la
        ''' compresion que el motor no le pone.</item>
        ''' <item><b>Mueve solo B.</b> A no se toca. Es asimetrico a proposito.</item>
        ''' <item><b>No multiplica por <c>invMass</c>.</b> Ni por la de B.</item>
        ''' </list>
        ''' </summary>
        Private Shared Sub SolveStretchLinks(st As ClothSimState, start As Integer, count As Integer, k As Single)
            Dim P = st.Positions
            For iL = start To start + count - 1
                Dim l = st.Stretch(iL)
                Dim d = P(l.B) - P(l.A)
                Dim d2 = d.LengthSquared()
                If d2 <= 0.0F Then Continue For
                Dim len = CSng(Math.Sqrt(d2))
                Dim err = l.Rest - len
                If err > 0.0F Then err = 0.0F          ' min(err, 0) — la parte unilateral
                P(l.B) += (d / len) * (err * l.Stiffness * k)
            Next
        End Sub

        ''' <summary>
        ''' `hclBendStiffnessConstraintSet` — la rigidez de flexión. Es el CUARTO constraint set del
        ''' motor, y el único que faltaba: el corpus de Fallout declara 1.170 objetos, tantos como
        ''' links estándar, y sin él una mecha de pelo no tiene nada que le impida enroscarse.
        '''
        ''' <para>⛔ LA LEY NO ESTÁ INFERIDA: sale de desensamblar el motor.
        ''' <c>hclBendStiffnessConstraintSet::solve</c> vive en <c>0x1419F9A62</c> (se lo ubica por la
        ''' cadena del temporizador <c>"TtSolve Bend Stiffness"</c> en <c>0x142717A68</c>), lee
        ''' <c>useRestPoseConfig</c> de <c>[this+0x30]</c> y despacha a dos implementaciones:
        ''' <c>0x1419F9B50</c> (flag apagado, la que se replica acá) y <c>0x1419F9CF0</c> (flag
        ''' prendido, con ángulo diedro y normales — NO implementada, ver la ingesta).</para>
        '''
        ''' <para>El cuerpo de <c>0x1419F9B50</c>, literal: avanza el puntero de links de <b>32 bytes</b>
        ''' por vuelta y lee <c>wA..wD</c> de <c>+0x00..+0x0C</c>, <c>bendStiffness</c> de <c>+0x10</c> y
        ''' las cuatro <c>uint16</c> de <c>+0x18..+0x1E</c>. Después:</para>
        ''' <code>
        '''     v = wA·P[A] + wB·P[B] + wC·P[C] + wD·P[D]      ' un solo v, calculado ANTES de escribir
        '''     S = bendStiffness × k                          ' k = el factor por-set del motor
        '''     P[i] += S · wᵢ · invMassᵢ · v                   ' para i en {A, B, C, D}
        ''' </code>
        '''
        ''' <para>⛔⛔ <c>restCurvature</c> (<c>+0x14</c>) <b>NO SE LEE</b> en esa rama. Eso no es un
        ''' detalle: se probaron ocho hipótesis sobre qué cantidad geométrica reproducía ese campo
        ''' contra 272.533 links reales del corpus y NINGUNA pasaba del 1,3 % de aciertos. La razón no
        ''' era que faltara la fórmula linda: era que <b>el corpus usa la OTRA rama</b> — las dos
        ''' prendas vanilla de prueba traen <c>useRestPoseConfig = True</c> (189 y 834 links). Ahí sí
        ''' se lee, y la ley está abajo. Es la diferencia entre leer el binario y adivinar.</para>
        '''
        ''' <para>La rama de rest-pose (<c>0x1419F9CF0</c>) tiene la MISMA forma; lo único que cambia es
        ''' que a <c>v</c> se le suma un offset construido con la geometría de la bisagra:</para>
        ''' <code>
        '''     a = A−C ; b = B−C ; d = D−C
        '''     n1 = d × a ; n2 = b × d          ' las normales de los dos triángulos que comparten C-D
        '''     ŝ = normalize(n̂1 + n̂2)
        '''     w = v + restCurvature · (|n1|·|n2| / |d|²) · ŝ
        ''' </code>
        ''' <para>Con <c>restCurvature = 0</c> colapsa exactamente en la rama lineal, que es la
        ''' comprobación de que las dos leyes son la misma familia.</para>
        '''
        ''' <para>Es lineal y sin normalizar: <c>v</c> es el vector de curvatura discreta y el paso es
        ''' un descenso de gradiente sobre <c>½|v|²</c>. Por eso <c>bendStiffness</c> viene NEGATIVO en
        ''' el archivo — con <c>S &lt; 0</c> el empuje va contra la curvatura. Si alguien "arregla" el
        ''' signo, la tela EXPLOTA en vez de aplanarse.</para>
        '''
        ''' <para>⚠️ El <c>v</c> se calcula UNA vez y las cuatro escrituras usan ESE valor (Jacobi
        ''' dentro del link, no Gauss-Seidel). El motor hace exactamente eso: computa <c>xmm11</c> y
        ''' recién después escribe las cuatro posiciones.</para>
        ''' </summary>
        Private Shared Sub SolveBend(st As ClothSimState, start As Integer, count As Integer, k As Single)
            Dim P = st.Positions
            Dim inv = st.InvMass
            For iB = start To start + count - 1
                Dim b = st.Bend(iB)
                ' `v` es el vector de curvatura discreta, y es COMUN a las dos ramas.
                Dim w = P(b.A) * b.WA + P(b.B) * b.WB + P(b.C) * b.WC + P(b.D) * b.WD

                If b.UseRestPose Then
                    ' --- rama 0x1419F9CF0: la curvatura de reposo entra como un OFFSET de `v` ---
                    ' El motor arma las normales de los dos triangulos que comparten la arista C-D:
                    '     a = A−C   b = B−C   d = D−C
                    '     n1 = d × a          n2 = b × d
                    ' (el orden sale del patron `shufps 0xC9`, que produce el cross NEGADO; ver la
                    ' derivacion en el doc de RE). Despues:
                    '     ŝ      = normalize(n̂1 + n̂2)          ' la bisectriz de las dos normales
                    '     factor = |n1|·|n2| / |d|²
                    '     w      = v + restCurvature · factor · ŝ
                    Dim pa = P(b.A), pb = P(b.B), pc = P(b.C), pd = P(b.D)
                    Dim ea = pa - pc
                    Dim eb = pb - pc
                    Dim ed = pd - pc
                    Dim n1 = Vector3.Cross(ed, ea)
                    Dim n2 = Vector3.Cross(eb, ed)
                    Dim l1 = n1.Length()
                    Dim l2 = n2.Length()
                    ' El motor calcula 1/|n| con `rsqrtps` y lo ANULA si |n|² <= 0 (`cmpleps`+`andnps`),
                    ' asi que una normal degenerada aporta el vector cero en vez de un infinito.
                    Dim u1 = If(l1 > 0.0F, n1 / l1, Vector3.Zero)
                    Dim u2 = If(l2 > 0.0F, n2 / l2, Vector3.Zero)
                    Dim suma = u1 + u2
                    Dim ls = suma.Length()
                    Dim bisec = If(ls > 0.0F, suma / ls, Vector3.Zero)   ' misma guarda, sobre |ŝ|²
                    Dim d2 = ed.LengthSquared()
                    ' ⚠️ El motor hace `rcpps` + UN paso de Newton (la constante 2.0 de 0x142629500) y
                    ' NO guarda el caso |d|² = 0: ahi produce infinito. Aca se guarda, porque una arista
                    ' degenerada de un solo triangulo envenenaria con NaN la prenda entera y el resto
                    ' del simulador no lo podria distinguir de una explosion real de la fisica.
                    If d2 > 0.0F Then
                        ' La constante que multiplica al factor es 1.0 (leida de 0x142929850), o sea
                        ' que no hay escala escondida: el factor es exactamente |n1|·|n2|/|d|².
                        w += bisec * (b.RestCurvature * (l1 * l2 / d2))
                    End If
                End If

                ' ⛔ EL FACTOR `k`, Y POR QUE VALE 1.0 — no es un supuesto, es una cuenta cerrada.
                ' `StiffnessFactor` (0x1418C6420), literal:
                '     si mode <> 2                      -> 1.0
                '     si N = 1 y s1 = 1 y s2 = 1        -> 1.0
                '     segun cs->type:  5 o 0xA -> 1.0 en la ULTIMA iteracion, 0.0 en las demas
                '                      8       -> (iteracion + 1) / N
                '                      resto   -> (N*s1*s2) ^ (-1,725)
                ' MEDIDO en el corpus: `numberOfSolveIterations` = 1 en los 1.248 operadores. Con N = 1
                ' la iteracion 0 ES la ultima, asi que la rama 5/0xA da 1.0; la 8 da (0+1)/1 = 1.0; y la
                ' default da (1*s1*s2)^(-1,725) = 1.0 cuando s1 = s2 = 1. Las tres coinciden.
                ' ⚠️ LIMITE DECLARADO: `s1`/`s2` (+0x1D0/+0x1D4 del sim-cloth) NO estan serializados —
                ' los pone el motor con la ESCALA DEL ACTOR cuando `bEnableScaling:Cloth` esta en 1, que
                ' es el default. Con un actor escalado dejan de valer 1 y la rama default deja de dar
                ' 1.0. El preview renderiza a escala 1, asi que hoy no aplica; si algun dia se renderiza
                ' un actor escalado, ESTO hay que implementarlo.
                ' Tampoco se implementa el modo adaptativo en si: 846 de 1.248 operadores declaran
                ' `adaptConstraintStiffness`, pero con N = 1 no cambia el resultado.
                ' ⚠️ Las cuatro escrituras usan el MISMO `w`, calculado antes de tocar ninguna posicion
                ' (Jacobi dentro del link). El motor hace exactamente eso.
                Dim s = b.Stiffness * k
                P(b.A) += w * (s * b.WA * inv(b.A))
                P(b.B) += w * (s * b.WB * inv(b.B))
                P(b.C) += w * (s * b.WC * inv(b.C))
                P(b.D) += w * (s * b.WD * inv(b.D))
            Next
        End Sub

        ''' <summary>
        ''' La "correa": `hclLocalRangeConstraintSet` limita cuánto puede alejarse la partícula de su
        ''' vértice de referencia SOBRE EL CUERPO SKINNEADO. Sin ella la tela sobre-cae (medido: los
        ''' cloths SIN local-range son exactamente los que sobre-caían, 79,8 % de libres contra 2,4 %).
        ''' </summary>
        Private Shared Sub SolveLocalRange(st As ClothSimState, skinned As Dictionary(Of Integer, Vector3),
                                           normales As Dictionary(Of Integer, Vector3),
                                           start As Integer, count As Integer, k As Single)
            Dim P = st.Positions
            For iC = start To start + count - 1
                Dim c = st.LocalRange(iC)
                If st.InvMass(c.Particle) = 0.0F Then Continue For
                ' ⛔ `referenceVertex` indexa la malla SKINNEADA (el cuerpo), no el array de particulas.
                Dim refPos As Vector3 = Nothing
                ' ⛔ LA MALLA DE REFERENCIA ES UN BUFFER. `referenceMeshBufferIdx` (+0x30) se lee en
                ' 0x141A01F50 y se usa para indexar el array de buffers en 0x141A01F5A. Antes esto
                ' tomaba "la piel" — el diccionario del ultimo operador de skin —, que coincide solo si
                ' ese operador escribio JUSTO ese buffer.
                Dim buf = BufferDe(st, c.BufferRef)
                If buf IsNot Nothing Then
                    If c.ReferenceVertex < 0 OrElse c.ReferenceVertex >= buf.Length Then Continue For
                    refPos = buf(c.ReferenceVertex)
                ElseIf Not skinned.TryGetValue(c.ReferenceVertex, refPos) Then
                    Continue For
                End If

                ' d = P − ref + eps. El epsilon (FLT_EPSILON, 0x142F3C760) lo suma el motor COMPONENTE
                ' A COMPONENTE, y no es cosmetico: con la particula exactamente sobre la referencia la
                ' direccion queda indefinida, y asi sale un vector chiquito pero valido.
                Const EPS As Single = 0.00000011920929F
                Dim d = P(c.Particle) - refPos
                d = New Vector3(d.X + EPS, d.Y + EPS, d.Z + EPS)
                Dim d2 = d.LengthSquared()
                If d2 <= 0.0F Then Continue For
                Dim len = CSng(Math.Sqrt(d2))
                Dim dir = d / len

                ' (1) RADIAL, unilateral: solo si se paso de `maximumDistance`. La rigidez del SET
                ' multiplica ESTE termino y solo este — en el binario, `[rax]` entra en `xmm4` y no en
                ' los dos terminos normales.
                Dim er = (c.MaxDistance - len) * c.Stiffness * k
                If er > 0.0F Then er = 0.0F
                Dim nueva = P(c.Particle) + dir * er

                ' (2) LOS DOS TOPES DEL EJE NORMAL. Sin esto la correa es una esfera, y una pollera
                ' puede hundirse en la pierna o despegarse sin que nada la frene: son justo los dos
                ' limites que el motor pone por separado del radial.
                If c.UsaNormal AndAlso normales IsNot Nothing Then
                    Dim refNrm As Vector3 = Nothing
                    Dim bufN = NormalDe(st, c.BufferRef)
                    Dim hayN = bufN IsNot Nothing AndAlso c.ReferenceVertex >= 0 AndAlso c.ReferenceVertex < bufN.Length
                    If hayN Then refNrm = bufN(c.ReferenceVertex)
                    If hayN OrElse normales.TryGetValue(c.ReferenceVertex, refNrm) Then
                        ' La componente normal se mide sobre la distancia YA corregida por el radial.
                        Dim nd = Vector3.Dot(dir, refNrm) * (er + len)
                        Dim lo = nd - c.MinNormal
                        If lo > 0.0F Then lo = 0.0F
                        Dim hi = c.MaxNormal - nd
                        If hi > 0.0F Then hi = 0.0F
                        nueva = nueva - refNrm * lo + refNrm * hi
                    End If
                End If

                P(c.Particle) = nueva
            Next
        End Sub

        ''' <summary>
        ''' Colision contra las capsulas. La ley sale de `TtSolve Contacts` (cadena en 0x14270C160,
        ''' funcion 0x141960214), que resuelve una lista de CONTACTOS de 48 bytes cada uno — punto del
        ''' plano, normal y el movimiento del colisionable:
        ''' <code>
        '''     d = dot(P - contactPos, n) - particleRadius
        '''     si d &lt; 0:  P += (-d)*n                       ' solo penetrando
        '''     ' friccion, sobre Pprev:
        '''     v  = (P - Pprev) - movimientoDelColisionable
        '''     vt = v - n*dot(n, v)                          ' componente tangencial
        '''     Pprev += vt * particleFriction
        ''' </code>
        '''
        ''' <para>⛔⛔ EL RADIO DE LA PARTICULA. Esto empujaba la particula hasta la SUPERFICIE de la
        ''' capsula e ignoraba <c>particleDatas[i].radius</c>. El motor pide
        ''' <c>dot(P - contacto, n) &gt;= particleRadius</c>: la particula queda SEPARADA de la capsula
        ''' por su propio radio. Es exactamente el sintoma de «la pierna asoma por el vestido» — la
        ''' tela quedaba pegada a la capsula de COLISION, y la malla VISIBLE de la pierna sobresale de
        ''' esa capsula.</para>
        '''
        ''' <para>⚠️ DECLARADO: la lista de contactos del motor la arma una fase aparte
        ''' (<c>TtiterateCollidables</c> → <c>computeContactPlanes</c>/<c>collideConvexes</c>,
        ''' 0x14195DAB1) que aca NO esta replicada; el contacto se deriva de la capsula en el momento.
        ''' Esa es la aproximacion que queda, y el termino de movimiento del colisionable no entra.</para>
        ''' </summary>
        ''' <summary>
        ''' Cuanto esta HUNDIDA la particula en la envolvente convexa de las dos esferas de la
        ''' `hclTaperedCapsuleShape` (un "cono redondeado"), y en que direccion hay que sacarla.
        '''
        ''' <para>La superficie es la union de las dos esferas y el tronco de cono TANGENTE a las dos.
        ''' Cual de las tres regiones toca a la particula lo decide la proyeccion sobre el eje
        ''' comparada con la pendiente del tangente, que es exactamente lo que el archivo trae
        ''' precomputado en `cosTheta`/`sinTheta`/`tanTheta`. Aca se recalcula desde `small`, `big`,
        ''' `smallRadius` y `bigRadius`, que son los mismos datos sin la optimizacion.</para>
        '''
        ''' <para>La `hclCapsuleShape` normal es el caso degenerado con los dos radios iguales: la
        ''' pendiente se anula, las dos regiones de tapa se juntan y queda la capsula de siempre. Por
        ''' eso no hace falta un camino aparte.</para>
        '''
        ''' <para>Devuelve la profundidad (&gt; 0 si hay penetracion) y deja en <paramref name="normal"/>
        ''' la direccion de salida, ya normalizada.</para>
        ''' </summary>
        Private Shared Function ProfundidadEnConoRedondeado(punto As Vector3, a As Vector3, b As Vector3,
                                                            radioA As Single, radioB As Single,
                                                            radioParticula As Single,
                                                            ByRef normal As Vector3) As Single
            Dim eje = b - a
            Dim l2 = eje.LengthSquared
            Dim v = punto - a
            If l2 <= 0.000001F Then
                ' Los dos extremos en el mismo punto: es una esfera.
                Dim ls = v.Length
                normal = If(ls <= 0.000001F, New Vector3(0.0F, 0.0F, 1.0F), v / ls)
                Return (Math.Max(radioA, radioB) + radioParticula) - ls
            End If
            Dim l = CSng(Math.Sqrt(l2))
            Dim u = eje / l
            Dim y = Vector3.Dot(v, u)              ' avance sobre el eje
            Dim radial = v - u * y
            Dim x = radial.Length                  ' distancia al eje
            Dim rA = radioA + radioParticula
            Dim rB = radioB + radioParticula
            Dim dr = rB - rA

            ' Pendiente del tangente a las dos esferas. |dr| > l significa que una esfera CONTIENE a la
            ' otra: ahi la envolvente es la esfera grande y no hay tronco.
            If Math.Abs(dr) >= l Then
                Dim usaB = rB > rA
                Dim cen = If(usaB, b, a)
                Dim rr = If(usaB, rB, rA)
                Dim dd = punto - cen
                Dim ll = dd.Length
                normal = If(ll <= 0.000001F, New Vector3(0.0F, 0.0F, 1.0F), dd / ll)
                Return rr - ll
            End If

            Dim sinT = dr / l                       ' = sinTheta del archivo
            Dim cosT = CSng(Math.Sqrt(Math.Max(0.0F, 1.0F - sinT * sinT)))

            ' La proyeccion sobre la RECTA TANGENTE decide la region. Fuera de [0, l/cosT] toca una tapa.
            Dim proy = (y * cosT) + (x * sinT)
            Dim tope = l / Math.Max(cosT, 0.000001F)

            If proy <= 0.0F Then
                Dim dd = punto - a
                Dim ll = dd.Length
                normal = If(ll <= 0.000001F, New Vector3(0.0F, 0.0F, 1.0F), dd / ll)
                Return rA - ll
            End If
            If proy >= tope Then
                Dim dd = punto - b
                Dim ll = dd.Length
                normal = If(ll <= 0.000001F, New Vector3(0.0F, 0.0F, 1.0F), dd / ll)
                Return rB - ll
            End If

            ' Tronco de cono: la distancia a la superficie es la distancia al eje menos el radio del
            ' tangente en ese punto, todo medido PERPENDICULAR AL TANGENTE.
            Dim dist = (x * cosT) - (y * sinT)      ' distancia con signo a la recta tangente
            Dim radialU = If(x <= 0.000001F, PerpendicularA(u), radial / x)
            ' La normal del tronco no es radial: se inclina con el cono.
            normal = Vector3.Normalize((radialU * cosT) - (u * sinT))
            Return rA - dist
        End Function

        ''' <summary>Un `hkVector4` del grafo a `Vector3`, con Nothing = cero. Las velocidades del
        ''' colisionable vienen asi y en el corpus suelen estar en cero, pero un mod puede traerlas.</summary>
        Private Shared Function VectorDe(v As HkxVector4Graph_Class) As Vector3
            If v Is Nothing Then Return Vector3.Zero
            Return New Vector3(CSng(v.X), CSng(v.Y), CSng(v.Z))
        End Function

        ''' <summary>El bit de `triangleFlips` del triangulo `t`. Un BIT, no un byte: byte `t \ 8`,
        ''' bit `t Mod 8`.</summary>
        Private Shared Function CaraInvertida(st As ClothSimState, t As Integer) As Boolean
            If st.FlipsSim Is Nothing OrElse t < 0 Then Return False
            Dim b = t >> 3
            If b >= st.FlipsSim.Length Then Return False
            Return (st.FlipsSim(b) And (CByte(1) << (t And 7))) <> 0
        End Function

        ''' <summary>Un vector unitario perpendicular a `u`. Solo se usa cuando la particula cae EXACTO
        ''' sobre el eje, donde la direccion radial no esta definida y hay que elegir alguna.</summary>
        Private Shared Function PerpendicularA(u As Vector3) As Vector3
            Dim aux = If(Math.Abs(u.Z) < 0.9F, New Vector3(0.0F, 0.0F, 1.0F), New Vector3(1.0F, 0.0F, 0.0F))
            Return Vector3.Normalize(Vector3.Cross(u, aux))
        End Function

        Private Shared Sub SolveCapsules(st As ClothSimState)
            If st.Capsules.Count = 0 Then Exit Sub
            For i = 0 To st.Positions.Length - 1
                If st.InvMass(i) = 0.0F Then Continue For
                Dim p = st.Positions(i)
                Dim pr = st.Radius(i)
                Dim mask = st.MascaraColision(i)
                For Each c In st.Capsules
                    ' ⛔ La mascara PRIMERO: el motor ni arma el par si el bit esta apagado.
                    If c.Bit <> 0UI AndAlso (mask And c.Bit) = 0UI Then Continue For
                    ' ⛔⛔ LA SUPERFICIE ES UN CONO, NO UN RADIO INTERPOLADO.
                    '
                    ' `hclTaperedCapsuleShape` no guarda "dos radios y ya": guarda la geometria del
                    ' CONO — `coneApex`, `coneAxis`, `cosTheta`, `sinTheta`, `tanTheta`, `tanThetaSqr`,
                    ' `l`, `d`, `lVec`, `dVec`, `tanThetaVecNeg`. Todos esos campos son la envolvente
                    ' convexa de las DOS ESFERAS, que es la superficie real.
                    '
                    ' Lo que habia era otra cosa: se buscaba el punto mas cercano del SEGMENTO y se
                    ' interpolaba el radio con el parametro `t` de ese punto. No es la misma superficie.
                    ' El punto de contacto de un cono esta CORRIDO a lo largo del eje respecto del pie
                    ' de la perpendicular — tanto mas cuanto mas se abre el cono — asi que el radio
                    ' interpolado se evalua en el lugar equivocado y da un colisionador MAS FLACO que el
                    ' real cerca de las tapas. En una pierna, que es justo un cono, ahi es donde la
                    ' pollera se hunde.
                    Dim n As Vector3 = Nothing
                    Dim prof = ProfundidadEnConoRedondeado(p, c.A, c.B, c.Radius, c.RadiusB, pr, n)
                    If prof <= 0.0F Then Continue For
                    Dim nueva = p + n * prof
                    ' Friccion: la componente TANGENCIAL de la velocidad Verlet se lleva hacia el
                    ' contacto. Con friction = 0 es no-op, o sea exactamente lo que habia antes.
                    Dim fr = st.Friction(i)
                    If fr <> 0.0F Then
                        ' ⛔ VELOCIDAD RELATIVA AL CUERPO. La friccion frena el deslizamiento contra la
                        ' SUPERFICIE, y la superficie se mueve: la velocidad del punto de contacto es
                        ' `linearVelocity + angularVelocity x (contacto − origen)`. Restarla es lo que
                        ' hace que la tela sea ARRASTRADA por una pierna que avanza en vez de patinar
                        ' sobre ella. Con el cuerpo quieto ambas son cero y esto es el no-op de antes.
                        ' ⛔ LA VELOCIDAD DEL CUERPO SE CONVIERTE EN DESPLAZAMIENTO: × dtSub.
                        '
                        ' Leido del kernel de contacto (0x141A6A93D): `mulps xmm14, xmm15` donde xmm14
                        ' es `linearVelocity + angularVelocity × r` y xmm15 es el `k` que `Simulate` le
                        ' pasa a `CollideAndSolve` — que NO es 1,0 sino `dt / subSteps` (0x14195C6C1:
                        ' `xmm7 = (1/subSteps) · dt`). Recien despues resta: `xmm3 = (nueva − previa) −
                        ' xmm14`. O sea que los dos terminos son DESPLAZAMIENTOS del substep.
                        '
                        ' Restar la velocidad cruda a un desplazamiento mezcla unidades y, con un
                        ' cuerpo en movimiento, mete un termino ~dt veces mas grande de lo que
                        ' corresponde.
                        '
                        ' ⭐ Y de paso queda REFUTADO lo que yo suponia: ese `k` NO escala la correccion
                        ' de posicion. La separacion es una PROYECCION DURA a la superficie
                        ' (0x141A6A923: `xmm4 = (objetivo − dot(P,n))·n + P`, y se escribe tal cual).
                        Dim contacto = nueva - n * (st.Radius(i))
                        Dim vCuerpo = c.VelLineal + Vector3.Cross(c.VelAngular, contacto - c.Origen)
                        Dim v = (nueva - st.Previous(i)) - vCuerpo * st.DtSub
                        Dim vt = v - n * Vector3.Dot(n, v)
                        st.Previous(i) += vt * fr
                    End If
                    p = nueva
                Next
                st.Positions(i) = p
            Next
        End Sub

        Private Shared Function ClosestPointOnSegment(p As Vector3, a As Vector3, b As Vector3,
                                                      <Runtime.InteropServices.Out> ByRef t As Single) As Vector3
            t = 0.0F
            Dim ab = b - a
            Dim denom = Vector3.Dot(ab, ab)
            If denom <= 0.000001F Then Return a
            t = Vector3.Dot(p - a, ab) / denom
            t = Math.Max(0.0F, Math.Min(1.0F, t))
            Return a + (ab * t)
        End Function

        ''' <summary>La parte 3x3 de la matriz, con la traslacion en cero.</summary>
        Private Shared Function SoloRotacion(m As Matrix4) As Matrix4
            Dim r = m
            r.M41 = 0.0F : r.M42 = 0.0F : r.M43 = 0.0F : r.M44 = 1.0F
            r.M14 = 0.0F : r.M24 = 0.0F : r.M34 = 0.0F
            Return r
        End Function

        ''' <summary>Traspuesta de la parte 3x3 - la inversa de una rotacion.</summary>
        Private Shared Function Transpuesta3(m As Matrix4) As Matrix4
            Dim r = Matrix4.Identity
            r.M11 = m.M11 : r.M12 = m.M21 : r.M13 = m.M31
            r.M21 = m.M12 : r.M22 = m.M22 : r.M23 = m.M32
            r.M31 = m.M13 : r.M32 = m.M23 : r.M33 = m.M33
            Return r
        End Function

        ''' <summary>
        ''' `Substep Collidables` (0x14195C9B0). Coloca CADA colisionable donde le toca en el substep:
        ''' camina desde el transform que tenia al empezar el frame hasta el que le corresponde, con su
        ''' velocidad lineal y angular, que es lo que hace el motor al principio de cada substep.
        ''' <para>`alpha = (s+1)/subSteps`: en el ultimo substep vale 1 y la capsula queda exactamente
        ''' en la pose nueva.</para>
        ''' </summary>
        Private Shared Sub ColocarColisionables(st As ClothSimState, alpha As Single)
            For k = 0 To st.Capsules.Count - 1
                Dim c = st.Capsules(k)
                Dim tP As New Vector3(c.MPrev.M41, c.MPrev.M42, c.MPrev.M43)
                Dim tC As New Vector3(c.MCur.M41, c.MCur.M42, c.MCur.M43)

                Dim rP = SoloRotacion(c.MPrev)
                Dim rD = Matrix4.Mult(Transpuesta3(rP), SoloRotacion(c.MCur))
                Dim trz = rD.M11 + rD.M22 + rD.M33
                Dim ang = CSng(Math.Acos(Math.Max(-1.0R, Math.Min(1.0R, (trz - 1.0R) / 2.0R))))
                Dim mm = rP
                If ang > 0.000001F Then
                    Dim eje = EjeDeRotacion(rD, ang)
                    If eje.LengthSquared > 0.0F Then
                        mm = Matrix4.Mult(rP, Matrix4.CreateFromAxisAngle(Vector3.Normalize(eje), ang * alpha))
                    End If
                End If
                Dim tt = tP + (tC - tP) * alpha
                mm.M41 = tt.X : mm.M42 = tt.Y : mm.M43 = tt.Z

                c.A = Vector3.TransformPosition(c.LocalA, mm)
                c.B = Vector3.TransformPosition(c.LocalB, mm)
                c.Origen = tt
                st.Capsules(k) = c
            Next
        End Sub

        Private Shared Sub RebuildCapsules(st As ClothSimState, sim As HclSimClothDataDetail_Class,
                                           clothSkel As Havok.Canon.Objects.HkObj_HkaSkeleton,
                                           skeleton As SkeletonInstance, dt As Single)
            st.Capsules.Clear()
            ' ⛔ LAS CÁPSULAS TIENEN QUE SEGUIR AL ESQUELETO VIVO. El motor lo hace una vez por frame
            ' ("TtDrive Collidables": copia los colisionables y les fija transform y velocidad desde el
            ' transform-set). Antes esta rutina transformaba los extremos SÓLO por el hkTransform
            ' AUTHORED, así que con cualquier pose —o con body-weight, que mueve los huesos por la capa
            ' Morph— las cápsulas quedaban congeladas en el bind: la falda atravesaba los muslos y
            ' colisionaba contra aire.
            ' El puente hueso↔colisionable ya estaba parseado y sin usar: `sim.CollidableBindings`, que
            ' sale de `collidableTransformMap.transformIndices`. MEDIDO: resuelve nombre de hueso en
            ' 1005 de 1005 bindings del corpus (Head ×360, Neck ×201, RLeg_Thigh ×78, …).
            ' ⛔⛔ EL COLISIONABLE SE COLOCA COMO LO COLOCA EL MOTOR.
            '
            ' `Drive Collidables` (dentro de `Simulate`, 0x14195C515) reescribe el transform de CADA
            ' colisionable UNA VEZ POR FRAME, y la cuenta es literal:
            '
            '     collidable.transform = collidableTransformMap.offsets[i] x transformSet[ transformIndices[i] ]
            '
            ' El `addps xmm7, [rcx+0x30]` de 0x14195C5BE suma la traslacion de la matriz del hueso: es
            ' un producto AFIN completo, no solo rotacion.
            '
            ' Lo que habia era otra cosa: se tomaba `hclCollidable.transform` SERIALIZADO y se le
            ' aplicaba un `poseDelta = inv(bindVivo) x actualVivo` calculado por la app. Eso equivale a
            ' suponer que el transform guardado es `offsets[i] x bindVivo` — o sea, que el archivo se
            ' grabo con el actor exactamente en bind. `hclCollidable.transform` es un campo de RUNTIME:
            ' el motor lo pisa todos los frames, y lo serializado es el ultimo valor que tuvo.
            '
            ' MIRADO: con la colision prendida la pollera salia con un pliegue grande cruzandole el
            ' frente en el PRIMER frame, PARADA; con `--nocoll` el pliegue desaparecia. La capsula
            ' estaba en el lugar equivocado y empujaba una banda de tela.
            '
            ' `offsets` ya estaba parseado (`Field98Matrices`) y emparejado con su hueso en
            ' `CollidableBindings.Matrix` — se leia y no se usaba.
            Dim bindingDe As New Dictionary(Of HclCollidableDetail_Class, HclSimCollidableBinding_Class)()
            For Each bnd In sim.CollidableBindings
                If bnd Is Nothing OrElse bnd.Collidable Is Nothing Then Continue For
                bindingDe(bnd.Collidable) = bnd
            Next

            Dim nDecl = If(sim.CollidableDetails Is Nothing, 0, sim.CollidableDetails.Count)
            Dim sinShape = 0, sinExtremos = 0
            Dim idxCol = -1
            For Each cd In sim.CollidableDetails
                idxCol += 1
                If cd Is Nothing OrElse cd.ShapeDetail Is Nothing Then
                    sinShape += 1
                    Continue For
                End If
                If cd.ShapeDetail.EndpointA Is Nothing OrElse cd.ShapeDetail.EndpointB Is Nothing Then
                    sinExtremos += 1
                    Continue For
                End If
                ' El transform del hclCollidable lleva la cápsula del espacio de su hueso al de la malla.
                ' ⛔ Sólo es correcto desde que ParseCollidable lee +0x20 y no +0x18: hasta 2026-08-22 la
                ' matriz salía corrida 8 bytes y la cápsula quedaba en cualquier lado.
                ' `offsets[i] x transformSet[hueso]`, igual que `Drive Collidables`. Sin binding no hay
                ' hueso al que seguir: queda el transform serializado, que es lo unico que trae el
                ' archivo, y se avisa — esa capsula NO va a acompañar la pose.
                Dim bnd2 As HclSimCollidableBinding_Class = Nothing
                Dim m As Matrix4
                Dim vivo As HierarchiBone_class = Nothing
                If bindingDe.TryGetValue(cd, bnd2) AndAlso bnd2 IsNot Nothing AndAlso
                   Not String.IsNullOrWhiteSpace(bnd2.BoneName) AndAlso
                   skeleton.SkeletonDictionary.TryGetValue(bnd2.BoneName.Trim(), vivo) AndAlso vivo IsNot Nothing AndAlso
                   vivo.GetGlobalTransform IsNot Nothing Then
                    m = Matrix4.Mult(MatrixOf(bnd2.Matrix), vivo.GetGlobalTransform.ToMatrix4())
                Else
                    m = MatrixOf(cd.TransformMatrix)
                    If Logger.Enabled Then
                        Dim nq = If(bnd2 Is Nothing, "(sin binding)", bnd2.BoneName)
                        Logger.LogLazy(Function() $"[CLOTH-COLL] colisionable sin hueso vivo ('{nq}') ⇒ queda en el transform serializado y NO sigue la pose")
                    End If
                End If

                Dim a = Vector3.TransformPosition(New Vector3(CSng(cd.ShapeDetail.EndpointA.X), CSng(cd.ShapeDetail.EndpointA.Y), CSng(cd.ShapeDetail.EndpointA.Z)), m)
                Dim b = Vector3.TransformPosition(New Vector3(CSng(cd.ShapeDetail.EndpointB.X), CSng(cd.ShapeDetail.EndpointB.Y), CSng(cd.ShapeDetail.EndpointB.Z)), m)
                ' Cápsula CÓNICA: `hclTaperedCapsuleShape` trae dos radios y no son iguales nunca.
                ' MEDIDO: 602 de 602 tapered del corpus tienen bigRadius ≠ smallRadius (delta hasta 2,83 u,
                ' ratio hasta 1,95×). Usar sólo el chico dejaba el colisionador flaco justo donde el autor
                ' puso más volumen, y la tela penetraba ahí.
                Dim rA = cd.ShapeDetail.Radius
                Dim rB = If(cd.ShapeDetail.AuxiliaryRadius > 0.0F, cd.ShapeDetail.AuxiliaryRadius, rA)
                ' La velocidad y el pinch vienen del PROPIO colisionable. `collidablePinchingDatas`
                ' del sim-cloth es el override por prenda: si trae entrada para este indice, manda esa.
                Dim pinEn = cd.PinchDetectionEnabled
                Dim pinPri = cd.PinchDetectionPriority
                Dim pinRad = cd.PinchDetectionRadius
                If sim.CollidablePinchingDatas IsNot Nothing AndAlso idxCol >= 0 AndAlso
                   idxCol < sim.CollidablePinchingDatas.Count AndAlso sim.CollidablePinchingDatas(idxCol) IsNot Nothing Then
                    Dim ov = sim.CollidablePinchingDatas(idxCol)
                    pinEn = ov.Enabled
                    pinPri = ov.Priority
                    pinRad = ov.Radius
                End If
                ' ⛔⛔ EL COLISIONABLE NO SALTA A LA POSE NUEVA: LA CAMINA.
                '
                ' `hclCollidable::setTransform` (0x1419605F0) recibe el transform VIEJO (el que el
                ' colisionable ya tiene, +0x20) y el NUEVO, y NO pisa el viejo: lo unico que escribe es
                '     linearVelocity  (+0x60) = (nuevo.traslacion - viejo.traslacion) / dt   [0x141960625, 0x141960822]
                '     angularVelocity (+0x70) = eje*angulo(viejo^-1 * nuevo) / dt            [0x1419607E7]
                ' y despues `Substep Collidables` (0x14195C9BC), al principio de CADA substep, hace
                '     traslacion += linearVelocity * dtSub
                '     rotacion    = exp(angularVelocity * dtSub) x rotacion
                ' o sea que el colisionable arranca el frame donde estaba y llega a la pose nueva
                ' recien en el ULTIMO substep.
                '
                ' Lo que habia era el salto: la capsula se colocaba en la pose FINAL desde el primer
                ' substep, con lo cual toda la penetracion de un frame aparecia de golpe y la
                ' proyeccion la empujaba entera en una sola pasada. MEDIDO: con la colision apagada la
                ' violacion media de links bajaba de 27,0 % a 15,7 %, o sea que la mitad del estiron lo
                ' metia la propia colision.
                '
                ' ⛔ Y las velocidades salian de `cd.LinearVelocity`/`cd.AngularVelocity` SERIALIZADAS.
                ' Esos dos campos son de runtime igual que el transform: el motor los reescribe todos
                ' los frames, y lo que trae el archivo es el ultimo valor que tuvieron cuando se grabo.
                Dim mPrev As Matrix4 = m
                If st.MatricesColPrev.ContainsKey(idxCol) Then mPrev = st.MatricesColPrev(idxCol)
                st.MatricesColPrev(idxCol) = m

                Dim tPrev As New Vector3(mPrev.M41, mPrev.M42, mPrev.M43)
                Dim tCur As New Vector3(m.M41, m.M42, m.M43)
                Dim invDt = If(dt > 0.0F, 1.0F / dt, 0.0F)
                Dim vLin = (tCur - tPrev) * invDt

                Dim rPrev = SoloRotacion(mPrev)
                Dim rDelta = Matrix4.Mult(Transpuesta3(rPrev), SoloRotacion(m))
                Dim trz = rDelta.M11 + rDelta.M22 + rDelta.M33
                Dim angD = CSng(Math.Acos(Math.Max(-1.0R, Math.Min(1.0R, (trz - 1.0R) / 2.0R))))
                Dim vAng = Vector3.Zero
                If angD > 0.000001F Then
                    Dim ejeD = EjeDeRotacion(rDelta, angD)
                    If ejeD.LengthSquared > 0.0F Then vAng = Vector3.Normalize(ejeD) * (angD * invDt)
                End If

                Dim locA As New Vector3(CSng(cd.ShapeDetail.EndpointA.X), CSng(cd.ShapeDetail.EndpointA.Y), CSng(cd.ShapeDetail.EndpointA.Z))
                Dim locB As New Vector3(CSng(cd.ShapeDetail.EndpointB.X), CSng(cd.ShapeDetail.EndpointB.Y), CSng(cd.ShapeDetail.EndpointB.Z))
                st.Capsules.Add(New CapsuleCollider With {.A = a, .B = b, .Radius = rA, .RadiusB = rB,
                                                          .Bit = If(idxCol >= 0 AndAlso idxCol < 32, 1UI << idxCol, 0UI),
                                                          .LocalA = locA, .LocalB = locB,
                                                          .MPrev = mPrev, .MCur = m,
                                                          .VelLineal = vLin,
                                                          .VelAngular = vAng,
                                                          .Origen = New Vector3(m.M41, m.M42, m.M43),
                                                          .PinchHabilitado = pinEn,
                                                          .PinchPrioridad = pinPri,
                                                          .PinchRadio = pinRad})
            Next
            If Logger.Enabled Then
                Dim a=nDecl, b=sinShape, c2=sinExtremos, d2=st.Capsules.Count
                Dim nombres = String.Join(" ", st.Capsules.Select(Function(x, k) $"{k}:r{x.Radius:F1}-{x.RadiusB:F1}"))
                Dim huesos = String.Join(" ", sim.CollidableBindings.Where(Function(x) x IsNot Nothing).Select(Function(x) x.BoneName))
                Logger.LogLazy(Function() $"[CLOTH-COLL] colisionables declarados={a} · sin shape={b} · sin extremos={c2} · capsulas construidas={d2} · huesos=[{huesos}] · radios=[{nombres}]")
            End If
        End Sub

        ' -----------------------------------------------------------------------------------------
        ' Teleport: el motor lo DECLARA, no lo adivina (fMaxRootDistanceBeforeTeleport / …Angle)
        ' -----------------------------------------------------------------------------------------
        Private Shared Function DetectTeleport(st As ClothSimState, skeleton As SkeletonInstance) As Boolean
            Dim root As HierarchiBone_class = Nothing
            For Each candidate In {"Root", "COM", "Bip01"}
                If skeleton.SkeletonDictionary.TryGetValue(candidate, root) AndAlso root IsNot Nothing Then Exit For
            Next
            If root Is Nothing Then Return False
            Dim g = root.GetGlobalTransform
            If g Is Nothing Then Return False
            Dim m = g.ToMatrix4()
            Dim pos = New Vector3(m.M41, m.M42, m.M43)
            Dim fwd = Vector3.Normalize(New Vector3(m.M21, m.M22, m.M23))
            If Not st.HasLastRoot Then
                st.LastRootTranslation = pos
                st.LastRootForward = fwd
                st.HasLastRoot = True
                Return False
            End If
            Dim moved = (pos - st.LastRootTranslation).Length
            Dim dot = Math.Max(-1.0F, Math.Min(1.0F, Vector3.Dot(fwd, st.LastRootForward)))
            Dim angle = CSng(Math.Acos(dot))
            st.LastRootTranslation = pos
            st.LastRootForward = fwd
            Return moved > HavokPhysicsSettings.MaxRootDistanceBeforeTeleport OrElse
                   angle > HavokPhysicsSettings.MaxRootAngleBeforeTeleport
        End Function

        ' -----------------------------------------------------------------------------------------
        ' hclSimpleMeshBoneDeformOperator — de partículas a huesos, con la ortonormalización del motor
        ' -----------------------------------------------------------------------------------------
        Private Shared Sub WriteBackDeform(deform As HclSimpleMeshBoneDeformOperatorGraph_Class,
                                           st As ClothSimState,
                                           skeleton As SkeletonInstance)
            If deform Is Nothing OrElse deform.BoneMappings Is Nothing Then Exit Sub

            ' ⛔⛔ ORDEN TOPOLOGICO: PADRES ANTES QUE HIJOS.
            '
            ' El motor escribe el transform de cada hueso en un TRANSFORM SET plano: cada salida es
            ' ABSOLUTA y no depende de las otras. Nuestro esqueleto es JERARQUICO, asi que para que el
            ' mundo del hueso termine siendo el que calculo el operador hay que escribir un LOCAL
            ' relativo al padre: `desiredLocal = inv(parentWorld) x world`. Y eso solo es correcto si
            ' `parentWorld` ya es el DEFINITIVO de este frame.
            '
            ' `deform.BoneMappings` viene en el orden del archivo (`triangleBonePairs`), que no es
            ' topologico. Los cloth-bones SI forman cadenas (medido en HouseDress\Dress.nif: 12 cadenas
            ' A..L de 6 huesos cada una, Bone_Cloth_X_001..006). Con el orden del archivo, un hijo
            ' procesado antes que su padre lee el `parentWorld` del FRAME ANTERIOR y el error se acumula
            ' hacia la punta de la cadena.
            '
            ' ⛔ POR QUE NO SE VEIA EN REPOSO: sin pose, TODOS los mundos son el bind y todos los deltas
            ' dan identidad, asi que el orden es irrelevante y el gate estatico pasaba. Aparece solo en
            ' ANIMACION, que es exactamente donde el usuario lo vio: la pollera se desgarraba con
            ' DeformOnly, que ni siquiera simula.
            Dim ordenados = deform.BoneMappings.
                Select(Function(m) New With {.Map = m, .Depth = DepthOf(skeleton, m?.BoneName)}).
                OrderBy(Function(x) x.Depth).ToList()

            Dim sinTri = 0, sinBind = 0, escritos = 0, fueraRango = 0
            For Each par In ordenados
                Dim map = par.Map
                If map Is Nothing Then Continue For
                If map.ResolvedTriangle Is Nothing Then sinTri += 1 : Continue For
                If map.BindMatrix Is Nothing Then sinBind += 1 : Continue For
                Dim i0 = CInt(map.ResolvedTriangle.Value0), i1 = CInt(map.ResolvedTriangle.Value1), i2 = CInt(map.ResolvedTriangle.Value2)
                If i0 < 0 OrElse i1 < 0 OrElse i2 < 0 Then fueraRango += 1 : Continue For
                If i0 >= st.Positions.Length OrElse i1 >= st.Positions.Length OrElse i2 >= st.Positions.Length Then fueraRango += 1 : Continue For


                Dim p0 = st.Positions(i0), p1 = st.Positions(i1), p2 = st.Positions(i2)
                Dim c = (p0 + p1 + p2) / 3.0F
                Dim a = p0 - c
                Dim b = p1 - c
                Dim nrm = Vector3.Cross(a, b)     ' CRUDO, sin normalizar — así lo arma el motor
                ' ⛔ MEDICION: ¿los huesos que salen girados 180° son los de triangulos con `flip`=1?
                ' El motor lee los indices del BUFFER; esta app usa `hclSimClothData.triangleIndices`.
                ' `triangleFlips` es, por definicion, donde esas dos listas discrepan en el winding.
                ' Si la correlacion existe, el flip va aplicado; si no, la causa es otra y no se toca.
                Dim flipDeEste = If(CaraInvertida(st, map.TriangleIndex), 1, 0)

                ' M = filas [ a ; b ; a×b ; (c,1) ]  y  T = bind × M   (convención fila-vector)
                Dim mM As New Matrix4(a.X, a.Y, a.Z, 0.0F,
                                      b.X, b.Y, b.Z, 0.0F,
                                      nrm.X, nrm.Y, nrm.Z, 0.0F,
                                      c.X, c.Y, c.Z, 1.0F)
                Dim t = Matrix4.Mult(MatrixOf(map.BindMatrix), mM)

                ' ⭐⛔ EL MOTOR RE-ORTONORMALIZA (0x14195B320) — Y EL ANCLA ES LA FILA 2, NO LA 0.
                '
                ' Leído instrucción por instrucción del binario: la función carga SÓLO TRES filas del
                ' bind — `[rdx+0x00]`, `[rdx+0x20]` y `[rdx+0x30]`. **`[rdx+0x10]` no se lee nunca**,
                ' porque la fila 1 no se usa: se RECONSTRUYE. Esa ausencia es la prueba de cuál es el
                ' ancla, y es lo que fija el orden entero:
                '
                '     n  = T.row2                  ' la NORMAL del triángulo, transformada. Ancla.
                '     t1 = cross(T.row2, T.row0)    -> sale en la fila 1
                '     t0 = cross(t1, T.row2)        -> sale en la fila 0
                '     salida = [ norm(t0) ; norm(t1) ; norm(n) ; (T.row3.xyz, 1) ]
                '
                ' Los dos `cross` salen de los idiomas SSE `shufps 0xC9` de 0x14195B4E8..0x14195B54C,
                ' y los tres `movdqu` de 0x14195B5CD/0x14195B5DF/0x14195B5D6 fijan qué va en qué fila.
                '
                ' ⛔ CONTRAEJEMPLO — la ley que hay que poder falsificar: con una terna ORTONORMAL
                ' [r0;r1;r2] esto tiene que devolver [r0;r1;r2] EXACTO (n=r2, t1=r2×r0=r1, t0=r1×r2=r0).
                ' La versión anterior anclaba en la fila 1 y devolvía [-r0; r2; r1], que es un giro de
                ' **180° exactos**: traslación perfecta y la malla partida en triángulos en pantalla.
                ' El gate no lo vio porque medía sólo la traslación (ver PhysicsGateMode.MaxAngleDelta).
                '
                ' Descarta shear y escala: guardar la afín completa nos ALEJARÍA del motor.
                Dim r0 = New Vector3(t.M11, t.M12, t.M13)
                Dim r2 = New Vector3(t.M31, t.M32, t.M33)
                Dim tr = New Vector3(t.M41, t.M42, t.M43)
                Dim axis1 = Vector3.Cross(r2, r0)
                Dim axis0 = Vector3.Cross(axis1, r2)
                Dim u0 = SafeNormalize(axis0)
                Dim u1 = SafeNormalize(axis1)
                Dim u2 = SafeNormalize(r2)
                ' Eje degenerado ⇒ el motor escribe CERO (guarda cmpleps/andnps), no NaN.
                ' Basta con que UNO de los ejes salga degenerado para que la matriz sea singular:
                ' `Transform_Class(Matrix4)` la acepta (colapsa la escala 0 a 1) y el `Inverse()` del hijo
                ' revienta. Y NaN no se detecta con `= 0.0F` (NaN = 0 da False), asi que se comprueba
                ' aparte: un NaN que entrara a la capa se quedaria ahi para siempre.
                If u0.LengthSquared = 0.0F OrElse u1.LengthSquared = 0.0F OrElse u2.LengthSquared = 0.0F _
                   OrElse Single.IsNaN(tr.X) OrElse Single.IsNaN(tr.Y) OrElse Single.IsNaN(tr.Z) Then Continue For

                Dim world As New Matrix4(u0.X, u0.Y, u0.Z, 0.0F,
                                         u1.X, u1.Y, u1.Z, 0.0F,
                                         u2.X, u2.Y, u2.Z, 0.0F,
                                         tr.X, tr.Y, tr.Z, 1.0F)

                Dim boneName = map.BoneName
                If String.IsNullOrWhiteSpace(boneName) Then Continue For
                Dim bone As HierarchiBone_class = Nothing
                If Not skeleton.SkeletonDictionary.TryGetValue(boneName.Trim(), bone) OrElse bone Is Nothing Then Continue For

                ' desiredLocal = inv(parentWorld) × world     (la cascade del parent ya lleva su física)
                Dim desiredLocal As Transform_Class
                If bone.Parent Is Nothing Then
                    desiredLocal = New Transform_Class(world)
                Else
                    Dim parentWorld = bone.Parent.GetGlobalTransform
                    If parentWorld Is Nothing Then Continue For
                    desiredLocal = parentWorld.Inverse().ComposeTransforms(New Transform_Class(world))
                End If

                ' ⛔ DIAGNOSTICO (solo Debug, solo con el Logger encendido): cuanto se aparta el frame
                ' RECONSTRUIDO del bind del hueso. En REPOSO y sin pose tiene que dar ~0 en los dos
                ' numeros; cualquier otra cosa dice QUE hueso esta mal y CUANTO, que es lo unico que
                ' distingue "el ancla esta mal" de "las particulas de origen estan mal".
                If Logger.Enabled Then
                    ' ⛔ LA REFERENCIA CORRECTA BAJO POSE. Comparar contra el BIND sirve en reposo y NO
                    ' SIRVE animando: el cuerpo se movio, asi que apartarse del bind es lo ESPERADO y el
                    ' numero no distingue "el deform esta bien y la prenda drapea" de "el deform esta
                    ' roto". La referencia que si distingue es donde estaria el hueso POSADO SIN FISICA:
                    ' la desviacion contra eso es el drape, y tiene que ser del orden de unas unidades.
                    Dim sinFis = WorldSinFisica(bone)
                    If sinFis IsNot Nothing Then
                        Dim sm = sinFis.ToMatrix4()
                        Dim dTp = Math.Sqrt(((sm.M41 - world.M41) ^ 2) + ((sm.M42 - world.M42) ^ 2) + ((sm.M43 - world.M43) ^ 2))
                        Dim tp = (sm.M11 * world.M11) + (sm.M12 * world.M12) + (sm.M13 * world.M13) +
                                 (sm.M21 * world.M21) + (sm.M22 * world.M22) + (sm.M23 * world.M23) +
                                 (sm.M31 * world.M31) + (sm.M32 * world.M32) + (sm.M33 * world.M33)
                        Dim angp = Math.Acos(Math.Max(-1.0R, Math.Min(1.0R, (tp - 1.0R) / 2.0R))) * 180.0R / Math.PI
                        Dim nm4 = boneName
                        Logger.LogLazy(Function() $"[CLOTH-DRAPE] '{nm4}' vs POSADO-SIN-FISICA: dT={dTp:F3} dAng={angp:F2}")
                    End If

                    Dim bindW = bone.OriginalGetGlobalTransform
                    If bindW IsNot Nothing Then
                        Dim bm = bindW.ToMatrix4()
                        Dim dt = Math.Sqrt(((bm.M41 - world.M41) ^ 2) + ((bm.M42 - world.M42) ^ 2) + ((bm.M43 - world.M43) ^ 2))
                        Dim tr3 = (bm.M11 * world.M11) + (bm.M12 * world.M12) + (bm.M13 * world.M13) +
                                  (bm.M21 * world.M21) + (bm.M22 * world.M22) + (bm.M23 * world.M23) +
                                  (bm.M31 * world.M31) + (bm.M32 * world.M32) + (bm.M33 * world.M33)
                        Dim cs = Math.Max(-1.0R, Math.Min(1.0R, (tr3 - 1.0R) / 2.0R))
                        Dim ang = Math.Acos(cs) * 180.0R / Math.PI
                        Dim nm = boneName
                        ' Se vuelca TAMBIEN el dato crudo del mapeo y la forma del triangulo de
                        ' particulas: un frame malo puede venir de un indice mal empacado o de un
                        ' triangulo degenerado, y sin estos numeros las dos hipotesis son indistinguibles.
                        Dim e01 = (p1 - p0).Length, e12 = (p2 - p1).Length, e20 = (p0 - p2).Length
                        Dim nl = nrm.Length
                        ' ⭐ EL TRIANGULO QUE EL AUTOR HORNEO. De `T = bind × M` sale `M = inv(bind) × T`, y
                        ' en el bind `T` es el mundo del hueso: o sea `M_bind = inv(bind) × bindWorld`,
                        ' cuyas filas 0 y 1 son los vectores `a` y `b` ORIGINALES. Comparar su largo con
                        ' el actual separa dos causas que se ven igual: "mis particulas estan mal" (el
                        ' authored es chico y el mio gigante) de "el indice apunta a otro triangulo"
                        ' (el authored ya era gigante).
                        Dim mb = Matrix4.Mult(MatrixOf(map.BindMatrix).Inverted(), bm)
                        Dim ea = New Vector3(mb.M11, mb.M12, mb.M13).Length
                        Dim eb = New Vector3(mb.M21, mb.M22, mb.M23).Length
                        Dim mp = map
                        Dim fq = flipDeEste
                        Logger.LogLazy(Function() $"[CLOTH-DEFORM] flip={fq} '{nm}' dT={dt:F4} dAng={ang:F3}" &
                                       $" tri={mp.TriangleIndex} idx=({i0},{i1},{i2})" &
                                       $" packBone={mp.PackedBoneValue} flags={mp.PackedBoneFlags}" &
                                       $" packVal={mp.PackedValue} mod6={mp.PackedValueFlags}" &
                                       $" e=({e01:F3},{e12:F3},{e20:F3}) |n|={nl:F5}" &
                                       $" src=({SrcOf(st, i0)},{SrcOf(st, i1)},{SrcOf(st, i2)})" &
                                       $" bind_e=({ea:F3},{eb:F3})")
                    End If
                End If

                ' Physics = inv(OrigL × Mount × Morph × Delta) × desiredLocal  — un DELTA, como el mount.
                Dim baseLocal = bone.LocaLTransformWithoutPhysics
                If baseLocal Is Nothing Then Continue For
                bone.PhysicsDeltaTransform = baseLocal.Inverse().ComposeTransforms(desiredLocal)
                escritos += 1
                skeleton.MarkPhysicsLayerWritten()
                _touched.AddOrUpdate(skeleton, Nothing)
            Next
            If Logger.Enabled Then
                ' ⛔ QUE ESCRIBIO Y QUE SE SALTEO. "0 huesos con capa" puede venir de tres motivos muy
                ' distintos y el sintoma es el mismo: sin desglose hay que adivinar cual.
                Dim e1 = escritos, s1 = sinTri, s2 = sinBind, s3 = fueraRango, t1 = ordenados.Count
                Logger.LogLazy(Function() $"[CLOTH-WB] writeback: {e1} de {t1} · sin triangulo={s1} · sin bind={s2} · fuera de rango={s3}")
            End If
        End Sub

        ''' <summary>DIAGNOSTICO: a que VERTICE mapea el puente esa particula (-1 = sin entrada).</summary>
        Private Shared Function GatherOf(st As ClothSimState, i As Integer) As Integer
            If st.GatherMap Is Nothing OrElse i < 0 OrElse i >= st.GatherMap.Length Then Return -1
            If st.GatherMapHas IsNot Nothing AndAlso i < st.GatherMapHas.Length AndAlso Not st.GatherMapHas(i) Then Return -1
            Return CInt(st.GatherMap(i))
        End Function

        ''' <summary>DIAGNOSTICO: 1 = el destino de esa particula vino de la piel skinneada,
        ''' 0 = vino del DefaultClothPose del archivo. Mezclar los dos en UN triangulo es lo que
        ''' fabrica aristas de 100 unidades donde la tela mide 4.</summary>
        Private Shared Function SrcOf(st As ClothSimState, i As Integer) As Integer
            If st.TargetFromSkin Is Nothing OrElse i < 0 OrElse i >= st.TargetFromSkin.Length Then Return -1
            Return If(st.TargetFromSkin(i), 1, 0)
        End Function

        ''' <summary>Profundidad del hueso en la jerarquia VIVA (raiz = 0). Un nombre que no resuelve
        ''' devuelve <see cref="Integer.MaxValue"/> para que caiga al final y no se cuele delante de un
        ''' padre real.</summary>
        ''' <summary>World del hueso con las cuatro capas de pose pero SIN la de fisica: es donde
        ''' estaria si la fisica no existiera. Se recorre la cadena de padres componiendo
        ''' <c>LocaLTransformWithoutPhysics</c>, porque `GetGlobalTransform` arrastra la fisica de los
        ''' ancestros y con eso la comparacion se muerde la cola.</summary>
        Private Shared Function WorldSinFisica(bone As HierarchiBone_class) As Transform_Class
            If bone Is Nothing Then Return Nothing
            Dim cadena As New List(Of HierarchiBone_class)
            Dim b = bone
            Dim guarda = 0
            While b IsNot Nothing AndAlso guarda < 256
                cadena.Add(b)
                b = b.Parent
                guarda += 1
            End While
            cadena.Reverse()
            Dim acc As Transform_Class = Nothing
            For Each x In cadena
                Dim l = x.LocaLTransformWithoutPhysics
                If l Is Nothing Then Return Nothing
                acc = If(acc Is Nothing, l, acc.ComposeTransforms(l))
            Next
            Return acc
        End Function

        Private Shared Function DepthOf(skeleton As SkeletonInstance, boneName As String) As Integer
            If skeleton Is Nothing OrElse String.IsNullOrWhiteSpace(boneName) Then Return Integer.MaxValue
            Dim bone As HierarchiBone_class = Nothing
            If Not skeleton.SkeletonDictionary.TryGetValue(boneName.Trim(), bone) OrElse bone Is Nothing Then Return Integer.MaxValue
            Dim d = 0
            Dim cur = bone.Parent
            ' Tope defensivo: un ciclo en la jerarquia colgaria el render entero.
            While cur IsNot Nothing AndAlso d < 512
                d += 1
                cur = cur.Parent
            End While
            Return d
        End Function

        Private Shared Function SafeNormalize(v As Vector3) As Vector3
            Dim l2 = v.LengthSquared
            If l2 <= 0.0F Then Return Vector3.Zero
            Return v / CSng(Math.Sqrt(l2))
        End Function

        ' -----------------------------------------------------------------------------------------
        ' ObjectSpaceSkin: la malla de sim skinneada al cuerpo POSADO
        ' -----------------------------------------------------------------------------------------
        Private Shared Function BuildSkinnedByVertex(skin As HclObjectSpaceSkinPNOperatorGraph_Class,
                                                     clothSkel As Havok.Canon.Objects.HkObj_HkaSkeleton,
                                                     bindWorld As Matrix4(),
                                                     skeleton As SkeletonInstance,
                                                     normales As Dictionary(Of Integer, Vector3)) As Dictionary(Of Integer, Vector3)
            Dim result As New Dictionary(Of Integer, Vector3)
            If skin Is Nothing OrElse skin.BoneTransforms Is Nothing Then Return result

            ' M[slot] = BoneTransforms[slot] × bindEmbebido[bone] × poseDelta[bone]
            ' donde poseDelta = inv(bindVivo) × actualVivo ⇒ IDENTIDAD en reposo (no-op estricto).
            Dim boneIndices = If(skin.BoneIndices, New List(Of UShort)())
            Dim slotMat(Math.Max(0, skin.BoneTransforms.Count - 1)) As Matrix4
            Dim slotOk(Math.Max(0, skin.BoneTransforms.Count - 1)) As Boolean
            For slot = 0 To skin.BoneTransforms.Count - 1
                slotMat(slot) = Matrix4.Identity
                slotOk(slot) = False
                If slot >= boneIndices.Count Then Continue For
                Dim ci = CInt(boneIndices(slot))
                If ci < 0 OrElse ci >= clothSkel.Bones.Count OrElse ci >= bindWorld.Length Then Continue For
                Dim nm = clothSkel.Bones(ci)?.Name
                If String.IsNullOrWhiteSpace(nm) Then Continue For
                Dim live As HierarchiBone_class = Nothing
                If Not skeleton.SkeletonDictionary.TryGetValue(nm, live) OrElse live Is Nothing Then Continue For
                Dim bindLive = live.OriginalGetGlobalTransform
                Dim curLive = live.GetGlobalTransform
                If bindLive Is Nothing OrElse curLive Is Nothing Then Continue For
                Dim poseDelta = Matrix4.Mult(bindLive.Inverse().ToMatrix4(), curLive.ToMatrix4())
                Dim composed = Matrix4.Mult(MatrixOf(skin.BoneTransforms(slot)), bindWorld(ci))
                slotMat(slot) = Matrix4.Mult(composed, poseDelta)
                slotOk(slot) = True

                ' ⛔ DIAGNOSTICO DEL BIND. El motor compone `boneFromSkinMeshTransforms[slot] x
                ' transformSet[hueso]`, donde `transformSet` es el world POSADO del hueso
                ' (hclObjectSpaceSkinPNOperator 0x14193BBD0 -> 0x14193BCE0 -> 0x141A0EEB0). Lo de aca
                ' equivale a eso SOLO SI `bindWorld(ci)` (el bind EMBEBIDO en el HKX de la prenda) es
                ' igual a `bindLive` (el bind del esqueleto vivo): la cadena queda
                '     BoneTransforms x bindEmbebido x inv(bindVivo) x actualVivo
                ' y los dos del medio solo se cancelan si son el mismo.
                ' ⚠️ EN REPOSO ESTO NO SE PUEDE VER: con la pose en identidad `poseDelta` es la
                ' identidad y la app queda consistente CONSIGO MISMA aunque los binds difieran. Por eso
                ' se mide la diferencia explicitamente, y no se confia en que el gate estatico este verde.
                If Logger.Enabled Then
                    Dim be = bindWorld(ci)
                    Dim bl = bindLive.ToMatrix4()
                    Dim dT = Math.Sqrt(((be.M41 - bl.M41) ^ 2) + ((be.M42 - bl.M42) ^ 2) + ((be.M43 - bl.M43) ^ 2))
                    Dim tr3 = (be.M11 * bl.M11) + (be.M12 * bl.M12) + (be.M13 * bl.M13) +
                              (be.M21 * bl.M21) + (be.M22 * bl.M22) + (be.M23 * bl.M23) +
                              (be.M31 * bl.M31) + (be.M32 * bl.M32) + (be.M33 * bl.M33)
                        Dim ang = Math.Acos(Math.Max(-1.0R, Math.Min(1.0R, (tr3 - 1.0R) / 2.0R))) * 180.0R / Math.PI
                    If dT > 0.001R OrElse ang > 0.05R Then
                        Dim nm3 = nm
                        Logger.LogLazy(Function() $"[CLOTH-BIND] '{nm3}' bindEmbebido vs bindVivo: dT={dT:F4} dAng={ang:F3} ⇒ la cadena NO se cancela bajo pose")
                    End If
                End If
            Next

            If Logger.Enabled Then
                Dim filas As New List(Of String)
                For slot = 0 To skin.BoneTransforms.Count - 1
                    Dim nm2 = "?"
                    If slot < boneIndices.Count Then
                        Dim ci2 = CInt(boneIndices(slot))
                        If ci2 >= 0 AndAlso ci2 < clothSkel.Bones.Count Then nm2 = clothSkel.Bones(ci2).Name
                    End If
                    filas.Add($"{slot}:{nm2}{If(slotOk(slot), "", "[NO-RESUELTO]")}")
                Next
                Dim ff = filas
                Dim nb = boneIndices.Count
                Dim nt = skin.BoneTransforms.Count
                Logger.LogLazy(Function() $"[CLOTH-SLOTS] boneTransforms={nt} boneIndices={nb} :: {String.Join(" ", ff)}")
            End If

            For Each blk In skin.SkinBlocks
                If blk Is Nothing OrElse blk.InfluenceBlock Is Nothing OrElse blk.VertexEntries Is Nothing Then Continue For
                For Each entry In blk.VertexEntries
                    If entry Is Nothing OrElse entry.Position Is Nothing Then Continue For
                    If entry.SlotIndex < 0 OrElse entry.SlotIndex >= blk.InfluenceBlock.VertexInfluences.Count Then Continue For
                    Dim lane = blk.InfluenceBlock.VertexInfluences(entry.SlotIndex)
                    If lane Is Nothing Then Continue For
                    Dim sp = SkinPoint(entry.Position, lane, slotMat, slotOk)
                    If sp.HasValue Then result(CInt(entry.VertexIndex)) = sp.Value
                    ' La NORMAL del vertice de referencia. La necesita la correa: sus dos limites
                    ' normales se miden sobre este eje, y sin el la correa degenera en una esfera.
                    ' El operador es "PN" — posicion Y normal — asi que el dato ya viene en el archivo.
                    If normales IsNot Nothing AndAlso entry.Normal IsNot Nothing Then
                        Dim sn = SkinDirection(entry.Normal, lane, slotMat, slotOk)
                        If sn.HasValue Then normales(CInt(entry.VertexIndex)) = sn.Value
                    End If
                    ' ⛔ CONTROL DE LA NORMAL. El motor NO la re-normaliza, asi que `minNormalDistance`
                    ' y `maxNormalDistance` se comparan contra una proyeccion ESCALADA por |n|. Si el
                    ' decodificado no diera ~1, los dos limites quedarian medidos en otra unidad y la
                    ' correa empujaria cualquier cosa. Es la unica forma de saber si la ley esta bien
                    ' implementada o si el dato de entrada esta mal.
                    If Logger.Enabled AndAlso normales IsNot Nothing Then
                        Dim nv As Vector3 = Nothing
                        If normales.TryGetValue(CInt(entry.VertexIndex), nv) Then
                            _normMin = Math.Min(_normMin, nv.Length)
                            _normMax = Math.Max(_normMax, nv.Length)
                        End If
                    End If
                Next
            Next
            If Logger.Enabled AndAlso _normMax > 0.0F Then
                Dim a = _normMin, b = _normMax
                Logger.LogLazy(Function() $"[CLOTH-NORMAL] |normal de referencia| en [{a:F4}..{b:F4}] (tiene que ser ~1)")
                _normMin = Single.MaxValue
                _normMax = 0.0F
            End If
            Return result
        End Function

        Private Shared _radMin As Single = Single.MaxValue
        Private Shared _radMax As Single = Single.MinValue
        Private Shared _friMin As Single = Single.MaxValue
        Private Shared _friMax As Single = Single.MinValue
        Private Shared _normMin As Single = Single.MaxValue
        Private Shared _normMax As Single = 0.0F

        ''' <summary>
        ''' Igual que <see cref="SkinPoint"/> pero para una DIRECCION: la traslacion de la matriz no
        ''' entra. El motor transforma la normal de referencia con las tres filas de rotacion y nada
        ''' mas (0x141A03170: usa `[r9+0x80]`, `[r9+0x90]` y `[r9+0xA0]`, y NO suma `[r9+0xB0]`).
        ''' <para>⛔ Tampoco la re-normaliza. Se replica: normalizar aca cambiaria la magnitud con la
        ''' que se comparan `minNormalDistance` y `maxNormalDistance`.</para>
        ''' </summary>
        Private Shared Function SkinDirection(localDir As HclObjectSpaceSkinQuantizedVectorGraph_Class,
                                              lane As HclObjectSpaceSkinVertexInfluenceGraph_Class,
                                              matrices As Matrix4(), valid As Boolean()) As Vector3?
            Dim x = 0.0R, y = 0.0R, z = 0.0R
            Dim any = False
            Dim lx = localDir.X, ly = localDir.Y, lz = localDir.Z
            Dim count = Math.Min(lane.TransformIndices.Count, lane.WeightBytes.Count)
            For i = 0 To count - 1
                Dim ti = CInt(lane.TransformIndices(i))
                If ti < 0 OrElse ti >= matrices.Length OrElse Not valid(ti) Then Continue For
                Dim w = lane.WeightBytes(i) / 255.0R
                If w = 0.0R Then Continue For
                Dim m = matrices(ti)
                x += ((lx * m.M11) + (ly * m.M21) + (lz * m.M31)) * w
                y += ((lx * m.M12) + (ly * m.M22) + (lz * m.M32)) * w
                z += ((lx * m.M13) + (ly * m.M23) + (lz * m.M33)) * w
                any = True
            Next
            If Not any Then Return Nothing
            Return New Vector3(CSng(x), CSng(y), CSng(z))
        End Function

        ''' <summary>Σ_k (w_k/255) · localPos · M[k] — la fórmula derivada y validada a 0,0011 u de media.</summary>
        Private Shared Function SkinPoint(localPoint As HclObjectSpaceSkinQuantizedVectorGraph_Class,
                                          lane As HclObjectSpaceSkinVertexInfluenceGraph_Class,
                                          matrices As Matrix4(), valid As Boolean()) As Vector3?
            Dim x = 0.0R, y = 0.0R, z = 0.0R
            Dim any = False
            Dim lx = localPoint.X, ly = localPoint.Y, lz = localPoint.Z
            Dim count = Math.Min(lane.TransformIndices.Count, lane.WeightBytes.Count)
            Dim wsum = 0.0R, wdropped = 0.0R
            For i = 0 To count - 1
                Dim ti = CInt(lane.TransformIndices(i))
                If ti < 0 OrElse ti >= matrices.Length OrElse Not valid(ti) Then
                    ' ⛔ Saltear una influencia SIN renormalizar encoge el punto hacia el ORIGEN en
                    ' proporcion al peso perdido. Se contabiliza para poder MEDIRLO.
                    If i < lane.WeightBytes.Count Then wdropped += lane.WeightBytes(i) / 255.0R
                    Continue For
                End If
                Dim w = lane.WeightBytes(i) / 255.0R
                If w = 0.0R Then Continue For
                wsum += w
                Dim m = matrices(ti)
                x += ((lx * m.M11) + (ly * m.M21) + (lz * m.M31) + m.M41) * w
                y += ((lx * m.M12) + (ly * m.M22) + (lz * m.M32) + m.M42) * w
                z += ((lx * m.M13) + (ly * m.M23) + (lz * m.M33) + m.M43) * w
                any = True
            Next
            If Not any Then Return Nothing
            If Logger.Enabled AndAlso wdropped > 0.001R Then
                Dim ws = wsum, wd = wdropped
                Logger.LogLazy(Function() $"[CLOTH-SKINW] peso perdido={wd:F3} (suma usada={ws:F3}) ⇒ el punto se encoge hacia el origen")
            End If
            Return New Vector3(CSng(x), CSng(y), CSng(z))
        End Function

        ''' <summary>El punto mas bajo del esqueleto vivo. Es lo unico que esta app puede ofrecer como
        ''' "suelo" para `landscapeCollision`: el motor consulta la geometria del terreno y aca no hay
        ''' terreno. Se recalcula por frame a proposito — con el actor en el aire el suelo NO esta bajo
        ''' sus pies, y congelarlo seria peor que no tenerlo.</summary>
        Private Shared Function AlturaDelSuelo(skeleton As SkeletonInstance) As Single
            If skeleton Is Nothing Then Return 0.0F
            Dim minZ = Single.MaxValue
            For Each bon In skeleton.SkeletonDictionary.Values
                If bon Is Nothing Then Continue For
                Dim g = bon.GetGlobalTransform
                If g Is Nothing Then Continue For
                Dim z = g.ToMatrix4().M43
                If z < minZ Then minZ = z
            Next
            Return If(minZ = Single.MaxValue, 0.0F, minZ)
        End Function

        ''' <summary>Bind global de cada hueso del cloth-skeleton embebido (ReferencePose compuesto por padres).</summary>
        Private Shared Function ComputeEmbeddedBindWorld(skel As Havok.Canon.Objects.HkObj_HkaSkeleton) As Matrix4()
            If skel?.Bones Is Nothing OrElse skel.ReferencePose Is Nothing Then Return New Matrix4() {}
            Dim n = skel.Bones.Count
            Dim world(Math.Max(0, n - 1)) As Matrix4
            Dim parents = skel.ParentIndices
            Dim fueraDeOrden = 0
            For i = 0 To n - 1
                Dim localM = Matrix4.Identity
                If i < skel.ReferencePose.Count Then
                    Dim tr = HkxTransformConventionHelper.ToTransform(skel.ReferencePose(i))
                    If tr IsNot Nothing Then localM = tr.ToMatrix4()
                End If
                Dim p As Integer = -1
                If parents IsNot Nothing AndAlso i < parents.Count Then p = parents(i)
                If p >= 0 AndAlso p < i Then
                    world(i) = Matrix4.Mult(localM, world(p))
                Else
                    If p >= i Then fueraDeOrden += 1
                    world(i) = localM
                End If
            Next
            If Logger.Enabled Then
                Dim f = fueraDeOrden
                Dim tot = n
                Logger.LogLazy(Function() $"[CLOTH-BINDW] huesos={tot} con padre FUERA DE ORDEN (p>=i, tratados como raiz)={f}")
            End If
            Return world
        End Function

        Private Shared Function MatrixOf(m As HkxMatrix4Graph_Class) As Matrix4
            If m Is Nothing OrElse m.Values Is Nothing OrElse m.Values.Length < 16 Then Return Matrix4.Identity
            Dim v = m.Values
            Return New Matrix4(v(0), v(1), v(2), v(3),
                               v(4), v(5), v(6), v(7),
                               v(8), v(9), v(10), v(11),
                               v(12), v(13), v(14), v(15))
        End Function

    End Class

End Namespace
