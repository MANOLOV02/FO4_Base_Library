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
        ''' <summary>Índices (en el espacio de PARTÍCULA) de las partículas fijas.</summary>
        Public Fixed As Integer()
        Public Gravity As Vector3
        ''' <summary>Factor de inercia por paso, ya elevado: (1−d)^dtSub.</summary>
        Public Damping As Single
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
    End Structure

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
    End Structure

    Friend Structure CapsuleCollider
        Public A As Vector3
        Public B As Vector3
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

                ' --- posiciones SKINNEADAS en la pose ACTUAL: son la referencia de todo ---
                Dim normalesRef As New Dictionary(Of Integer, Vector3)
                Dim skinned = BuildSkinnedByVertex(cfg.ObjectSpaceSkin, clothSkel, bindWorld, skeleton, normalesRef)

                EnsureState(st, sim, cfg, particleCount, dt, clothSkel)

                ' --- destino de cada partícula en ESTE frame (la piel posada) ---
                Dim target = BuildTargets(st, sim, cfg, skinned, particleCount)

                ' Las cápsulas ANTES de sembrar: el asentamiento inicial tiene que colisionar. Corriendo
                ' `Settle` con la lista vacía eran 10 substeps de caída libre (~19 u a −686,7 u/s²) que
                ' metían la tela dentro del cuerpo antes de que nada la frenara.
                If HavokPhysicsSettings.Mode = HavokPhysicsMode.FullSimulation Then
                    RebuildCapsules(st, sim, clothSkel, skeleton)
                End If

                If Logger.Enabled Then
                    ' Censo de lo que la simulacion tiene realmente para trabajar. Sin links de
                    ' distancia las particulas caen libres y las aristas se estiran sin limite: es la
                    ' diferencia entre "la fisica esta mal calibrada" y "la fisica no tiene constraints".
                    Dim nLinks = If(st.Links Is Nothing, -1, st.Links.Count)
                    Dim nRange = If(st.LocalRange Is Nothing, -1, st.LocalRange.Count)
                    Dim nFixed = If(st.Fixed Is Nothing, -1, st.Fixed.Length)
                    Dim nCaps = If(st.Capsules Is Nothing, -1, st.Capsules.Count)
                    Dim nPart = particleCount
                    ' Que CLASES de constraint trae el archivo. Es la lista de lo que el motor
                    ' ejecuta; todo lo que no este implementado es tela que nadie sujeta.
                    Dim clases As New List(Of String)
                    If sim.ConstraintDetails IsNot Nothing Then
                        For Each cdet In sim.ConstraintDetails
                            If cdet Is Nothing Then Continue For
                            clases.Add(cdet.GetType().Name)
                        Next
                    End If
                    Dim resumen = String.Join(",", clases.GroupBy(Function(x) x).Select(Function(g) $"{g.Key}x{g.Count()}"))
                    ' ⛔ ANTI-PINCH. El motor, DESPUES de resolver los contactos, recorre
                    ' `hclSimClothData.antiPinchConstraintSets` (+0xC8, cuenta en +0xD0) y llama al
                    ' `solve` virtual (+0x48) de cada uno con k = 1.0 — leido de CollideAndSolve
                    ' (0x141A69730, el bucle de 0x141A699B0 a 0x141A699E5, constante en 0x142929458).
                    ' Es el unico paso que puede REPARAR lo que la colision rompio: medido, la violacion
                    ' de links pasa de 0,2 % a 38,7 % en cuanto entran las capsulas.
                    Dim nap = If(sim.AntiPinchConstraintSets Is Nothing, -1, sim.AntiPinchConstraintSets.Count)
                    ' Los dos numeros que deciden CUANTAS veces corre el bucle. El motor:
                    '   por substep: integrar Verlet, luego `numberOfSolveIterations` x (constraints + colision)
                    ' Con pocas iteraciones la colision rompe los links y nada los vuelve a resolver.
                    Dim nss = st.SubSteps, nit = st.SolveIterations
                    Logger.LogLazy(Function() $"[CLOTH-CONSTR] particulas={nPart} links={nLinks} localRange={nRange} fijas={nFixed} capsulas={nCaps} antiPinch={nap} substeps={nss} iteraciones={nit} sets=[{resumen}]")
                End If

                If Logger.Enabled AndAlso st.Links IsNot Nothing AndAlso st.Links.Count > 0 Then
                    ' ⭐ CONTROL DEL PARSEO DE LOS LINKS: el `restLength` authored tiene que coincidir con
                    ' la distancia REAL entre las dos particulas en la pose sembrada. Si no coincide, el
                    ' solver esta tirando de la tela hacia una longitud inventada y la estira o la
                    ' encoge por construccion, por bien que este el resto.
                    Dim peor = 0.0F, peorIdx = -1
                    Dim suma = 0.0R, n2 = 0
                    For li = 0 To st.Links.Count - 1
                        Dim lk = st.Links(li)
                        If lk.A < 0 OrElse lk.B < 0 OrElse lk.A >= particleCount OrElse lk.B >= particleCount Then Continue For
                        Dim real = (target(lk.A) - target(lk.B)).Length
                        If real <= 0.0001F Then Continue For
                        Dim rel = Math.Abs(lk.Rest - real) / real
                        suma += rel : n2 += 1
                        If rel > peor Then peor = rel : peorIdx = li
                    Next
                    Dim pe2 = peor, pi2 = peorIdx, med = If(n2 > 0, suma / n2, 0.0R), nn = st.Links.Count
                    Dim r0 = If(peorIdx >= 0, st.Links(peorIdx).Rest, 0.0F)
                    Dim d0 = If(peorIdx >= 0, (target(st.Links(peorIdx).A) - target(st.Links(peorIdx).B)).Length, 0.0F)
                    Logger.LogLazy(Function() $"[CLOTH-REST] links={nn} desvio medio={med:P1} peor={pe2:P1} (link {pi2}: rest={r0:F4} real={d0:F4})")
                End If

                If Logger.Enabled Then
                    ' ⛔ La CORREA (`hclLocalRangeConstraintSet`) es lo unico que impide que una particula
                    ' libre se vaya lejos de su referencia en la piel. Si su vertice de referencia no
                    ' esta en el diccionario skinneado, el constraint se saltea EN SILENCIO y la
                    ' particula queda suelta. Contar cuantas resuelven separa "la correa esta floja" de
                    ' "la correa no existe".
                    Dim lrOk = 0, lrTot = 0
                    Dim fijasReales = 0
                    For Each c In st.LocalRange
                        lrTot += 1
                        Dim tmp As Vector3 = Nothing
                        If skinned.TryGetValue(c.ReferenceVertex, tmp) Then lrOk += 1
                    Next
                    For iq = 0 To st.InvMass.Length - 1
                        If st.InvMass(iq) = 0.0F Then fijasReales += 1
                    Next
                    Dim a1 = lrOk, a2 = lrTot, a3 = fijasReales, a4 = skinned.Count
                    Logger.LogLazy(Function() $"[CLOTH-LR] correa resuelta {a1}/{a2} · particulas con invMass=0: {a3} · vertices skinneados: {a4}")
                End If

                Dim teleported = DetectTeleport(st, skeleton)
                If (Not st.Seeded) OrElse teleported OrElse HavokPhysicsSettings.Mode = HavokPhysicsMode.DeformOnly Then
                    ' Sembrar = poner TODO en la piel posada, con velocidad cero.
                    ' El motor hace lo mismo al activar una prenda (y después corre uNumSimSettleSteps).
                    For i = 0 To particleCount - 1
                        st.Positions(i) = target(i)
                        st.Previous(i) = target(i)
                    Next
                    st.Seeded = True
                    If HavokPhysicsSettings.Mode = HavokPhysicsMode.FullSimulation AndAlso Not teleported Then
                        Settle(st, target, skinned, normalesRef, skeleton)
                    End If
                End If

                If HavokPhysicsSettings.Mode = HavokPhysicsMode.FullSimulation Then
                    Simulate(st, target, skinned, normalesRef, skeleton, dt)
                End If

                If Logger.Enabled AndAlso st.Links IsNot Nothing AndAlso st.Links.Count > 0 Then
                    ' Violacion de los links DESPUES de simular. Separa dos culpables que se ven igual
                    ' en pantalla: "la malla de simulacion se estiro" (esto da alto) vs "la malla de
                    ' simulacion esta bien y lo que esta mal es el frame que le doy al hueso" (esto da
                    ' bajo y el render igual sale roto).
                    Dim peor2 = 0.0F
                    For Each lk In st.Links
                        If lk.A < 0 OrElse lk.B < 0 OrElse lk.A >= st.Positions.Length OrElse lk.B >= st.Positions.Length Then Continue For
                        If lk.Rest <= 0.0001F Then Continue For
                        Dim dd = (st.Positions(lk.A) - st.Positions(lk.B)).Length
                        Dim rr = Math.Abs(dd - lk.Rest) / lk.Rest
                        If rr > peor2 Then peor2 = rr
                    Next
                    Dim pp2 = peor2
                    Logger.LogLazy(Function() $"[CLOTH-VIOL] peor violacion de link tras simular: {pp2:P1}")
                End If

                WriteBackDeform(cfg.SimpleMeshBoneDeform, st, skeleton)
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
            ReDim st.Friction(particleCount - 1)
            For i = 0 To particleCount - 1
                st.Mass(i) = sim.ParticleDatas(i).Mass
                st.InvMass(i) = sim.ParticleDatas(i).InverseMass
                st.Radius(i) = sim.ParticleDatas(i).Radius
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
            st.SubSteps = Math.Max(1, Math.Min(16, subs))

            Dim iters = If(cfg.Simulate IsNot Nothing, cfg.Simulate.SolveIterationCount, 1)
            If HavokPhysicsSettings.SolveIterationOverride > 0 Then iters = HavokPhysicsSettings.SolveIterationOverride
            st.SolveIterations = Math.Max(1, Math.Min(32, iters))

            If sim.Gravity IsNot Nothing Then
                st.Gravity = New Vector3(CSng(sim.Gravity.X), CSng(sim.Gravity.Y), CSng(sim.Gravity.Z))
            Else
                st.Gravity = New Vector3(0.0F, 0.0F, -686.7F)   ' 1 g a 70 u/m, lo que trae la ropa vanilla
            End If

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

            BuildConstraints(st, sim, particleCount, clothSkel)
            RecomputeDamping(st, sim, dt)
        End Sub

        ''' <summary>
        ''' `damp = (1 − globalDampingPerSecond) ^ dtSub`, con las DOS ramas duras del motor
        ''' (0x1418C75B0): d &gt;= 1 ⇒ 0 (la tela no hereda nada de velocidad) y d = 0 ⇒ 1.
        ''' </summary>
        Private Shared Sub RecomputeDamping(st As ClothSimState, sim As HclSimClothDataDetail_Class, dt As Single)
            Dim d = sim.GlobalDampingPerSecond
            Dim dtSub = dt / Math.Max(1, st.SubSteps)
            If d >= 1.0F Then
                st.Damping = 0.0F
            ElseIf d = 0.0F Then
                st.Damping = 1.0F
            Else
                st.Damping = CSng(Math.Pow(1.0R - d, dtSub))
            End If
        End Sub

        Private Shared Sub BuildConstraints(st As ClothSimState, sim As HclSimClothDataDetail_Class, particleCount As Integer,
                                            clothSkel As Havok.Canon.Objects.HkObj_HkaSkeleton)
            st.Links.Clear()
            st.Stretch.Clear()
            st.BendLinks.Clear()
            st.Compressible.Clear()
            st.BonePlanes.Clear()
            st.LocalRange.Clear()
            st.Bend.Clear()
            st.Blocks.Clear()
            ' ⛔ `sim.ConstraintDetails` YA viene en el orden del archivo (se arma recorriendo
            ' `staticConstraintSets`), asi que recorrerlo en orden y anotar un bloque por set es todo
            ' lo que hace falta para que el solver respete la declaracion.
            For Each detail In sim.ConstraintDetails
                Dim dist = TryCast(detail, HclStandardLinkConstraintSetDetail_Class)
                If dist IsNot Nothing Then
                    Dim ini = st.Links.Count
                    AddLinks(st, dist.LinkDetails, particleCount, st.Links)
                    st.Blocks.Add(New ConstraintBlock With {.Kind = ConstraintKind.Distance, .Start = ini, .Count = st.Links.Count - ini})
                    Continue For
                End If
                Dim stretch = TryCast(detail, HclStretchLinkConstraintSetDetail_Class)
                If stretch IsNot Nothing Then
                    Dim ini = st.Stretch.Count
                    AddLinks(st, stretch.LinkDetails, particleCount, st.Stretch)
                    st.Blocks.Add(New ConstraintBlock With {.Kind = ConstraintKind.Stretch, .Start = ini, .Count = st.Stretch.Count - ini})
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
                    st.Blocks.Add(New ConstraintBlock With {.Kind = ConstraintKind.BonePlane, .Start = ini, .Count = st.BonePlanes.Count - ini})
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
                    st.Blocks.Add(New ConstraintBlock With {.Kind = ConstraintKind.BendLink, .Start = ini, .Count = st.BendLinks.Count - ini})
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
                    st.Blocks.Add(New ConstraintBlock With {.Kind = ConstraintKind.Compressible, .Start = ini, .Count = st.Compressible.Count - ini})
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
                    st.Blocks.Add(New ConstraintBlock With {.Kind = ConstraintKind.Bend, .Start = iniBend, .Count = st.Bend.Count - iniBend})
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
                            .UsaNormal = lr.ApplyNormalComponent})
                    Next
                    st.Blocks.Add(New ConstraintBlock With {.Kind = ConstraintKind.LocalRange, .Start = iniLr, .Count = st.LocalRange.Count - iniLr})
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
                        Logger.LogLazy(Function() $"[CLOTH-CORREA] {n2} constraints · applyNormal={an} stiffness={sf:F4} shapeType={shp} · maxDist=[{mn:F3}..{mx:F3}] minNormal={mnn:F3} maxNormal={mxn:F3}")
                    End If
                End If
            Next
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
        ''' <summary>
        ''' Transformacion RIGIDA (rotacion + traslacion, sin escala) que lleva las posiciones de BIND
        ''' del `DefaultClothPose` al espacio posado de AHORA, ajustada sobre las particulas que SI
        ''' tienen destino skinneado.
        '''
        ''' <para>Es el algoritmo de Kabsch: se centran las dos nubes, se arma la matriz de covarianza
        ''' y se saca la rotacion. Aca se resuelve por iteracion de la raiz cuadrada de matriz
        ''' (Newton–Schulz sobre <c>R ← ½(R + R⁻ᵀ)</c>), que converge a la parte ortogonal de la
        ''' covarianza en pocas vueltas y no necesita descomposicion SVD.</para>
        '''
        ''' <para>⛔ Devuelve la IDENTIDAD si hay menos de tres anclas o si la nube esta degenerada:
        ''' con dos puntos la rotacion alrededor del eje que los une queda indeterminada, y elegir una
        ''' cualquiera seria inventar. En ese caso se conserva el comportamiento anterior.</para>
        '''
        ''' <para>⚠️ EN REPOSO DEVUELVE LA IDENTIDAD EXACTA, porque el destino skinneado de cada ancla
        ''' cae sobre su propia posicion de bind (eso es justo lo que verifica el control
        ''' <c>[CLOTH-TARGET]</c>: <c>peor|skin-pose| = 0,00</c>). Por eso este cambio no puede mover el
        ''' gate estatico, y si lo moviera seria que el control estaba mintiendo.</para>
        ''' </summary>
        Private Shared Function AjusteRigidoDeAnclas(st As ClothSimState,
                                                     pose As HclSimClothPoseGraph_Class,
                                                     skinned As Dictionary(Of Integer, Vector3),
                                                     particleCount As Integer) As Matrix4
            If pose Is Nothing OrElse pose.Pose Is Nothing OrElse st.GatherMap Is Nothing Then Return Matrix4.Identity

            Dim origen As New List(Of Vector3)
            Dim destino As New List(Of Vector3)
            For i = 0 To particleCount - 1
                If i >= st.GatherMap.Length OrElse i >= pose.Pose.Count Then Continue For
                If st.GatherMapHas IsNot Nothing AndAlso (i >= st.GatherMapHas.Length OrElse Not st.GatherMapHas(i)) Then Continue For
                Dim v As Vector3 = Nothing
                If Not skinned.TryGetValue(CInt(st.GatherMap(i)), v) Then Continue For
                Dim p = pose.Pose(i)
                origen.Add(New Vector3(CSng(p.X), CSng(p.Y), CSng(p.Z)))
                destino.Add(v)
            Next
            If origen.Count < 3 Then Return Matrix4.Identity

            Dim co As Vector3 = Vector3.Zero, cd As Vector3 = Vector3.Zero
            For k = 0 To origen.Count - 1
                co += origen(k)
                cd += destino(k)
            Next
            co /= origen.Count
            cd /= origen.Count

            ' Covarianza H = Σ (o−co)ᵀ (d−cd), en convencion de FILA.
            Dim h11 = 0.0F, h12 = 0.0F, h13 = 0.0F
            Dim h21 = 0.0F, h22 = 0.0F, h23 = 0.0F
            Dim h31 = 0.0F, h32 = 0.0F, h33 = 0.0F
            For k = 0 To origen.Count - 1
                Dim a = origen(k) - co
                Dim b = destino(k) - cd
                h11 += a.X * b.X : h12 += a.X * b.Y : h13 += a.X * b.Z
                h21 += a.Y * b.X : h22 += a.Y * b.Y : h23 += a.Y * b.Z
                h31 += a.Z * b.X : h32 += a.Z * b.Y : h33 += a.Z * b.Z
            Next

            Dim r As New Matrix4(h11, h12, h13, 0.0F,
                                 h21, h22, h23, 0.0F,
                                 h31, h32, h33, 0.0F,
                                 0.0F, 0.0F, 0.0F, 1.0F)
            If Math.Abs(r.Determinant) < 0.000001F Then Return Matrix4.Identity

            ' Newton–Schulz: R ← ½(R + R⁻ᵀ). Converge a la parte ortogonal. 12 vueltas alcanzan de
            ' sobra para la precision de Single; si en el medio se vuelve singular, se abandona.
            For it = 0 To 11
                Dim inv As Matrix4
                Try
                    inv = r.Inverted()
                Catch
                    Return Matrix4.Identity
                End Try
                Dim it2 = Matrix4.Transpose(inv)
                r = New Matrix4((r.M11 + it2.M11) * 0.5F, (r.M12 + it2.M12) * 0.5F, (r.M13 + it2.M13) * 0.5F, 0.0F,
                                (r.M21 + it2.M21) * 0.5F, (r.M22 + it2.M22) * 0.5F, (r.M23 + it2.M23) * 0.5F, 0.0F,
                                (r.M31 + it2.M31) * 0.5F, (r.M32 + it2.M32) * 0.5F, (r.M33 + it2.M33) * 0.5F, 0.0F,
                                0.0F, 0.0F, 0.0F, 1.0F)
            Next
            ' Una reflexion (det < 0) no es una rotacion: se abandona en vez de espejar la prenda.
            If r.Determinant < 0.0F Then Return Matrix4.Identity
            For Each c In New Single() {r.M11, r.M12, r.M13, r.M21, r.M22, r.M23, r.M31, r.M32, r.M33}
                If Single.IsNaN(c) OrElse Single.IsInfinity(c) Then Return Matrix4.Identity
            Next

            ' t = cd − co·R   (convencion de fila: el punto multiplica por la IZQUIERDA)
            Dim rot = New Vector3(co.X * r.M11 + co.Y * r.M21 + co.Z * r.M31,
                                  co.X * r.M12 + co.Y * r.M22 + co.Z * r.M32,
                                  co.X * r.M13 + co.Y * r.M23 + co.Z * r.M33)
            Dim t = cd - rot
            r.M41 = t.X : r.M42 = t.Y : r.M43 = t.Z
            r.M44 = 1.0F
            Return r
        End Function

        Private Shared Function BuildTargets(st As ClothSimState, sim As HclSimClothDataDetail_Class,
                                             cfg As HclClothConfigGraph_Class,
                                             skinned As Dictionary(Of Integer, Vector3),
                                             particleCount As Integer) As Vector3()
            Dim target(particleCount - 1) As Vector3
            Dim pose = sim.DefaultClothPoseDetails.FirstOrDefault()
            If st.TargetFromSkin Is Nothing OrElse st.TargetFromSkin.Length <> particleCount Then
                ReDim st.TargetFromSkin(Math.Max(0, particleCount - 1))
            End If

            ' ⛔⛔ EL AJUSTE RIGIDO DE LAS PARTICULAS SIN MAPEO.
            '
            ' `hclMoveParticlesOperator` se llama "move SOME particles" y eso es literal: en el pelo
            ' vanilla ancla 22 de 113 particulas. Las otras 91 el motor NO las coloca — las SIMULA.
            ' Esta funcion, para esas, usaba la posicion del `DefaultClothPose` TAL CUAL, que esta en
            ' el espacio de BIND del archivo. En reposo eso coincide y el gate estatico da verde; bajo
            ' pose, esas 91 se quedan clavadas en el bind mientras las 22 ancladas siguen al cuerpo, y
            ' el triangulo que une unas con otras se estira sin limite. MEDIDO: aristas de ~3 u que
            ' pasan a 8,3 u, y el gate de animacion en x49 con la fisica APAGADA (DeformOnly).
            '
            ' La correccion es llevar el bind al espacio de AHORA con la transformacion RIGIDA que
            ' llevan las particulas que SI estan ancladas: se emparejan sus posiciones de bind con sus
            ' destinos skinneados y se resuelve la rotacion+traslacion que mejor las lleva (Kabsch).
            '
            ' ⚠️ ES UNA REGLA DE LA APP, NO DEL MOTOR, y esta marcada como tal: el motor no la necesita
            ' porque simula esas particulas. Lo que reemplaza es una regla que estaba MAL (dejarlas en
            ' otro espacio). En REPOSO el ajuste da la identidad EXACTA, asi que no puede mover el gate
            ' estatico — que es la comprobacion que hay que exigirle.
            Dim ajuste = AjusteRigidoDeAnclas(st, pose, skinned, particleCount)

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
                If Not got AndAlso pose IsNot Nothing AndAlso pose.Pose IsNot Nothing AndAlso i < pose.Pose.Count Then
                    Dim p = pose.Pose(i)
                    Dim enBind As New Vector3(CSng(p.X), CSng(p.Y), CSng(p.Z))
                    ' Del espacio de bind del archivo al de AHORA. Sin anclas suficientes el ajuste es
                    ' la identidad y queda el comportamiento viejo, que es lo unico que se puede hacer.
                    ' Convencion de FILA, igual que el resto del modulo: el punto multiplica por la
                    ' izquierda y la traslacion vive en la fila 3.
                    target(i) = New Vector3(
                        enBind.X * ajuste.M11 + enBind.Y * ajuste.M21 + enBind.Z * ajuste.M31 + ajuste.M41,
                        enBind.X * ajuste.M12 + enBind.Y * ajuste.M22 + enBind.Z * ajuste.M32 + ajuste.M42,
                        enBind.X * ajuste.M13 + enBind.Y * ajuste.M23 + enBind.Z * ajuste.M33 + ajuste.M43)
                    got = True
                End If
                If Not got Then target(i) = st.Positions(i)
                If Not st.TargetFromSkin(i) Then st.TargetFromSkin(i) = False
            Next

            ' ⛔ CONTROL: en REPOSO el destino skinneado de CADA particula tiene que caer sobre su
            ' posicion del DefaultClothPose (que es el bind de la malla de simulacion). Una particula
            ' que se aparta decenas de unidades delata que el puente particula↔vertice la mando al
            ' vertice equivocado — y con UNA sola alcanza para que el triangulo de un cloth-bone quede
            ' convertido en una astilla de 100 unidades.
            If Logger.Enabled Then
                Dim fuente = If(cfg.GatherAllVertices IsNot Nothing AndAlso cfg.GatherAllVertices.GatheredVertexIndices.Count > 0,
                                $"GatherAllVertices({cfg.GatherAllVertices.GatheredVertexIndices.Count})",
                                If(cfg.MoveParticles IsNot Nothing AndAlso cfg.MoveParticles.Pairs IsNot Nothing,
                                   $"MoveParticles({cfg.MoveParticles.Pairs.Count})", "NINGUNA"))
                Dim poseN = If(pose Is Nothing OrElse pose.Pose Is Nothing, -1, pose.Pose.Count)
                Dim nPoses = sim.DefaultClothPoseDetails.Count
                Logger.LogLazy(Function() $"[CLOTH-MAP] fuente={fuente} posesEnElArchivo={nPoses} pose.Count={poseN}")
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
        Private Shared Sub Simulate(st As ClothSimState, target As Vector3(), skinned As Dictionary(Of Integer, Vector3),
                                    normalesRef As Dictionary(Of Integer, Vector3), skeleton As SkeletonInstance, dt As Single)
            Dim n = st.Positions.Length
            Dim substeps = st.SubSteps
            Dim dtSub = dt / substeps
            Dim dtSub2 = dtSub * dtSub
            Dim gravity = st.Gravity * HavokPhysicsSettings.GravityScale

            ' Snapshot de las anclas ANTES del bucle: el motor interpola desde donde estaban hasta el
            ' destino, un tramo por substep. Tomar el snapshot adentro haría que el ancla saltara.
            Dim anchorFrom(Math.Max(0, st.Fixed.Length - 1)) As Vector3
            For k = 0 To st.Fixed.Length - 1
                anchorFrom(k) = st.Positions(st.Fixed(k))
            Next

            For s = 0 To substeps - 1
                Dim alpha = CSng(s + 1) / substeps

                ' (2) anclas kinemáticas interpoladas — en LOS DOS buffers, como el motor.
                For k = 0 To st.Fixed.Length - 1
                    Dim idx = st.Fixed(k)
                    Dim p = Vector3.Lerp(anchorFrom(k), target(idx), alpha)
                    st.Positions(idx) = p
                    st.Previous(idx) = p
                Next

                ' (3) Verlet de posición. invMass = 0 (ancla) ⇒ aceleración 0.
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
                For it = 0 To st.SolveIterations - 1
                    For Each blk In st.Blocks
                        Select Case blk.Kind
                            Case ConstraintKind.Distance
                                SolveDistanceLinks(st, blk.Start, blk.Count)
                            Case ConstraintKind.Stretch
                                SolveStretchLinks(st, blk.Start, blk.Count)
                            Case ConstraintKind.Bend
                                If HavokPhysicsSettings.EnableBend Then SolveBend(st, blk.Start, blk.Count)
                            Case ConstraintKind.BendLink
                                SolveBendLinks(st, blk.Start, blk.Count)
                            Case ConstraintKind.Compressible
                                SolveCompressibleLinks(st, blk.Start, blk.Count)
                            Case ConstraintKind.BonePlane
                                SolveBonePlanes(st, skeleton, blk.Start, blk.Count)
                            Case ConstraintKind.LocalRange
                                If HavokPhysicsSettings.EnableLocalRange Then SolveLocalRange(st, skinned, normalesRef, blk.Start, blk.Count)
                        End Select
                    Next
                    If HavokPhysicsSettings.EnableCollision Then SolveCapsules(st)
                Next
            Next
        End Sub

        ''' <summary>Asentamiento inicial: `uNumSimSettleSteps` = 10 pasos con gravedad, sin avanzar el reloj.</summary>
        Private Shared Sub Settle(st As ClothSimState, target As Vector3(), skinned As Dictionary(Of Integer, Vector3),
                                  normalesRef As Dictionary(Of Integer, Vector3), skeleton As SkeletonInstance)
            Dim steps = Math.Max(0, HavokPhysicsSettings.SettleSteps)
            For s = 0 To steps - 1
                Simulate(st, target, skinned, normalesRef, skeleton, HavokPhysicsSettings.FixedTimeStep)
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
        Private Shared Sub SolveDistanceLinks(st As ClothSimState, start As Integer, count As Integer)
            Dim P = st.Positions
            Dim inv = st.InvMass
            For iL = start To start + count - 1
                Dim l = st.Links(iL)
                Dim d = P(l.B) - P(l.A)
                Dim d2 = d.LengthSquared()
                If d2 <= 0.0F Then Continue For
                Dim len = CSng(Math.Sqrt(d2))
                Dim dir = d / len
                Dim c = dir * ((len - l.Rest) * l.Stiffness)
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
                                           start As Integer, count As Integer)
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
                P(c.Particle) += n * (-sdist * c.Stiffness)
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
        Private Shared Sub SolveBendLinks(st As ClothSimState, start As Integer, count As Integer)
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
                Dim c = dir * ((estiron * l.StiffMax) - (compres * l.StiffMin))
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
        Private Shared Sub SolveCompressibleLinks(st As ClothSimState, start As Integer, count As Integer)
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
                Dim c = (d / len) * ((len - objetivo) * l.StiffMin)
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
        Private Shared Sub SolveStretchLinks(st As ClothSimState, start As Integer, count As Integer)
            Dim P = st.Positions
            For iL = start To start + count - 1
                Dim l = st.Stretch(iL)
                Dim d = P(l.B) - P(l.A)
                Dim d2 = d.LengthSquared()
                If d2 <= 0.0F Then Continue For
                Dim len = CSng(Math.Sqrt(d2))
                Dim err = l.Rest - len
                If err > 0.0F Then err = 0.0F          ' min(err, 0) — la parte unilateral
                P(l.B) += (d / len) * (err * l.Stiffness)
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
        Private Shared Sub SolveBend(st As ClothSimState, start As Integer, count As Integer)
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

                ' `k` (el factor por-set de `StiffnessFactor`, 0x1418C6420) vale 1.0 salvo en el modo
                ' adaptativo, que este simulador no implementa: se omite el producto por 1.
                ' ⚠️ Las cuatro escrituras usan el MISMO `w`, calculado antes de tocar ninguna posicion
                ' (Jacobi dentro del link). El motor hace exactamente eso.
                Dim s = b.Stiffness
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
                                           start As Integer, count As Integer)
            Dim P = st.Positions
            For iC = start To start + count - 1
                Dim c = st.LocalRange(iC)
                If st.InvMass(c.Particle) = 0.0F Then Continue For
                ' ⛔ `referenceVertex` indexa la malla SKINNEADA (el cuerpo), no el array de particulas.
                Dim refPos As Vector3 = Nothing
                If Not skinned.TryGetValue(c.ReferenceVertex, refPos) Then Continue For

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
                Dim er = (c.MaxDistance - len) * c.Stiffness
                If er > 0.0F Then er = 0.0F
                Dim nueva = P(c.Particle) + dir * er

                ' (2) LOS DOS TOPES DEL EJE NORMAL. Sin esto la correa es una esfera, y una pollera
                ' puede hundirse en la pierna o despegarse sin que nada la frene: son justo los dos
                ' limites que el motor pone por separado del radial.
                If c.UsaNormal AndAlso normales IsNot Nothing Then
                    Dim refNrm As Vector3 = Nothing
                    If normales.TryGetValue(c.ReferenceVertex, refNrm) Then
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
        Private Shared Sub SolveCapsules(st As ClothSimState)
            If st.Capsules.Count = 0 Then Exit Sub
            For i = 0 To st.Positions.Length - 1
                If st.InvMass(i) = 0.0F Then Continue For
                Dim p = st.Positions(i)
                Dim pr = st.Radius(i)
                For Each c In st.Capsules
                    Dim t = 0.0F
                    Dim closest = ClosestPointOnSegment(p, c.A, c.B, t)
                    ' Radio INTERPOLADO por la posicion sobre el eje: la capsula es un cono truncado,
                    ' no un cilindro (602 de 602 tapered del corpus tienen los dos radios distintos).
                    Dim radius = c.Radius + ((c.RadiusB - c.Radius) * t)
                    ' El plano de contacto es la superficie de la capsula; el motor separa la
                    ' particula de ESE plano por su propio radio.
                    Dim objetivo = radius + pr
                    Dim d = p - closest
                    Dim len = d.Length
                    If len >= objetivo Then Continue For
                    Dim n As Vector3
                    If len <= 0.000001F Then
                        n = New Vector3(0.0F, 0.0F, 1.0F)
                    Else
                        n = d / len
                    End If
                    Dim nueva = closest + n * objetivo
                    ' Friccion: la componente TANGENCIAL de la velocidad Verlet se lleva hacia el
                    ' contacto. Con friction = 0 es no-op, o sea exactamente lo que habia antes.
                    Dim fr = st.Friction(i)
                    If fr <> 0.0F Then
                        Dim v = nueva - st.Previous(i)
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

        Private Shared Sub RebuildCapsules(st As ClothSimState, sim As HclSimClothDataDetail_Class,
                                           clothSkel As Havok.Canon.Objects.HkObj_HkaSkeleton, skeleton As SkeletonInstance)
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
            Dim boneByCollidable As New Dictionary(Of HclCollidableDetail_Class, String)()
            For Each bnd In sim.CollidableBindings
                If bnd Is Nothing OrElse bnd.Collidable Is Nothing Then Continue For
                If String.IsNullOrWhiteSpace(bnd.BoneName) Then Continue For
                boneByCollidable(bnd.Collidable) = bnd.BoneName.Trim()
            Next

            ' ⛔ POR QUE SE CUENTAN LOS DESCARTES. `capsulas=0` puede significar dos cosas opuestas —
            ' "el archivo no declara colisionables" o "los declara y nosotros no los sabemos leer" — y
            ' el sintoma en pantalla es el mismo: la pierna atraviesa la falda. Sin este desglose las
            ' dos hipotesis son indistinguibles.
            Dim nDecl = If(sim.CollidableDetails Is Nothing, 0, sim.CollidableDetails.Count)
            Dim sinShape = 0, sinExtremos = 0
            For Each cd In sim.CollidableDetails
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
                Dim m = MatrixOf(cd.TransformMatrix)

                ' poseDelta = inv(bindVivo) × actualVivo ⇒ IDENTIDAD en reposo (no-op estricto), y el
                ' desplazamiento real del hueso en cuanto hay pose, mount o morph.
                Dim boneName As String = Nothing
                If boneByCollidable.TryGetValue(cd, boneName) Then
                    Dim live As HierarchiBone_class = Nothing
                    If skeleton.SkeletonDictionary.TryGetValue(boneName, live) AndAlso live IsNot Nothing Then
                        Dim bindLive = live.OriginalGetGlobalTransform
                        Dim curLive = live.GetGlobalTransform
                        If bindLive IsNot Nothing AndAlso curLive IsNot Nothing Then
                            m = Matrix4.Mult(m, Matrix4.Mult(bindLive.Inverse().ToMatrix4(), curLive.ToMatrix4()))
                        End If
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
                st.Capsules.Add(New CapsuleCollider With {.A = a, .B = b, .Radius = rA, .RadiusB = rB})
            Next
            If Logger.Enabled Then
                Dim a=nDecl, b=sinShape, c2=sinExtremos, d2=st.Capsules.Count
                Logger.LogLazy(Function() $"[CLOTH-COLL] colisionables declarados={a} · sin shape={b} · sin extremos={c2} · capsulas construidas={d2}")
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

            For Each par In ordenados
                Dim map = par.Map
                If map Is Nothing OrElse map.ResolvedTriangle Is Nothing OrElse map.BindMatrix Is Nothing Then Continue For
                Dim i0 = CInt(map.ResolvedTriangle.Value0), i1 = CInt(map.ResolvedTriangle.Value1), i2 = CInt(map.ResolvedTriangle.Value2)
                If i0 < 0 OrElse i1 < 0 OrElse i2 < 0 Then Continue For
                If i0 >= st.Positions.Length OrElse i1 >= st.Positions.Length OrElse i2 >= st.Positions.Length Then Continue For

                Dim p0 = st.Positions(i0), p1 = st.Positions(i1), p2 = st.Positions(i2)
                Dim c = (p0 + p1 + p2) / 3.0F
                Dim a = p0 - c
                Dim b = p1 - c
                Dim nrm = Vector3.Cross(a, b)     ' CRUDO, sin normalizar — así lo arma el motor

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
                        Logger.LogLazy(Function() $"[CLOTH-DEFORM] '{nm}' dT={dt:F4} dAng={ang:F3}" &
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
                skeleton.MarkPhysicsLayerWritten()
                _touched.AddOrUpdate(skeleton, Nothing)
            Next
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
