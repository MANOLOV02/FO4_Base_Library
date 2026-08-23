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
        Public LocalRange As New List(Of LocalRangeConstraint)
        Public Capsules As New List(Of CapsuleCollider)
        ''' <summary>gatherMap(particleIdx) = VertexIndex del ObjectSpaceSkin.</summary>
        Public GatherMap As UShort()
        ''' <summary>Cuando el mapa viene de MoveParticles es PARCIAL: dice que entradas son validas.</summary>
        Public GatherMapHas As Boolean()
    End Class

    Friend Structure DistanceLink
        Public A As Integer
        Public B As Integer
        Public Rest As Single
        Public Stiffness As Single
    End Structure

    Friend Structure LocalRangeConstraint
        Public Particle As Integer
        ''' <summary>Indice de VERTICE de la malla skinneada (NO de particula). Difieren en el 13 % del corpus.</summary>
        Public ReferenceVertex As Integer
        Public MaxDistance As Single
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
                Dim skinned = BuildSkinnedByVertex(cfg.ObjectSpaceSkin, clothSkel, bindWorld, skeleton)

                EnsureState(st, sim, cfg, particleCount, dt)

                ' --- destino de cada partícula en ESTE frame (la piel posada) ---
                Dim target = BuildTargets(st, sim, cfg, skinned, particleCount)

                ' Las cápsulas ANTES de sembrar: el asentamiento inicial tiene que colisionar. Corriendo
                ' `Settle` con la lista vacía eran 10 substeps de caída libre (~19 u a −686,7 u/s²) que
                ' metían la tela dentro del cuerpo antes de que nada la frenara.
                If HavokPhysicsSettings.Mode = HavokPhysicsMode.FullSimulation Then
                    RebuildCapsules(st, sim, clothSkel, skeleton)
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
                        Settle(st, target, skinned)
                    End If
                End If

                If HavokPhysicsSettings.Mode = HavokPhysicsMode.FullSimulation Then
                    Simulate(st, target, skinned, dt)
                End If

                WriteBackDeform(cfg.SimpleMeshBoneDeform, st, skeleton)
            Next
        End Sub

        ' -----------------------------------------------------------------------------------------
        ' Estado: parámetros authored + topología de constraints (una vez por bloque)
        ' -----------------------------------------------------------------------------------------
        Private Shared Sub EnsureState(st As ClothSimState, sim As HclSimClothDataDetail_Class,
                                       cfg As HclClothConfigGraph_Class, particleCount As Integer, dt As Single)
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
            For i = 0 To particleCount - 1
                st.Mass(i) = sim.ParticleDatas(i).Mass
                st.InvMass(i) = sim.ParticleDatas(i).InverseMass
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

            BuildConstraints(st, sim, particleCount)
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

        Private Shared Sub BuildConstraints(st As ClothSimState, sim As HclSimClothDataDetail_Class, particleCount As Integer)
            st.Links.Clear()
            st.LocalRange.Clear()
            For Each detail In sim.ConstraintDetails
                Dim dist = TryCast(detail, HclStandardLinkConstraintSetDetail_Class)
                If dist IsNot Nothing Then
                    AddLinks(st, dist.LinkDetails, particleCount)
                    Continue For
                End If
                Dim stretch = TryCast(detail, HclStretchLinkConstraintSetDetail_Class)
                If stretch IsNot Nothing Then
                    AddLinks(st, stretch.LinkDetails, particleCount)
                    Continue For
                End If
                Dim lr = TryCast(detail, HclLocalRangeConstraintSetDetail_Class)
                If lr IsNot Nothing Then
                    For Each c In lr.ConstraintDetails
                        Dim pi = CInt(c.ParticleIndex)
                        If pi < 0 OrElse pi >= particleCount Then Continue For
                        st.LocalRange.Add(New LocalRangeConstraint With {
                            .Particle = pi,
                            .ReferenceVertex = CInt(c.ReferenceVertexIndex),
                            .MaxDistance = c.MaximumDistance})
                    Next
                End If
            Next
        End Sub

        Private Shared Sub AddLinks(st As ClothSimState, links As IEnumerable(Of HclDistanceConstraintGraph_Class), particleCount As Integer)
            If links Is Nothing Then Exit Sub
            For Each l In links
                Dim a = CInt(l.ParticleA), b = CInt(l.ParticleB)
                If a < 0 OrElse b < 0 OrElse a >= particleCount OrElse b >= particleCount OrElse a = b Then Continue For
                st.Links.Add(New DistanceLink With {.A = a, .B = b, .Rest = l.RestLength,
                                                    .Stiffness = If(l.Stiffness > 0.0F, Math.Min(1.0F, l.Stiffness), 1.0F)})
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
                    End If
                End If
                If Not got AndAlso pose IsNot Nothing AndAlso pose.Pose IsNot Nothing AndAlso i < pose.Pose.Count Then
                    Dim p = pose.Pose(i)
                    target(i) = New Vector3(CSng(p.X), CSng(p.Y), CSng(p.Z))
                    got = True
                End If
                If Not got Then target(i) = st.Positions(i)
            Next
            Return target
        End Function

        ' -----------------------------------------------------------------------------------------
        ' El bucle del motor
        ' -----------------------------------------------------------------------------------------
        Private Shared Sub Simulate(st As ClothSimState, target As Vector3(), skinned As Dictionary(Of Integer, Vector3), dt As Single)
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
                For it = 0 To st.SolveIterations - 1
                    SolveDistanceLinks(st)
                    If HavokPhysicsSettings.EnableLocalRange Then SolveLocalRange(st, skinned)
                    If HavokPhysicsSettings.EnableCollision Then SolveCapsules(st)
                Next
            Next
        End Sub

        ''' <summary>Asentamiento inicial: `uNumSimSettleSteps` = 10 pasos con gravedad, sin avanzar el reloj.</summary>
        Private Shared Sub Settle(st As ClothSimState, target As Vector3(), skinned As Dictionary(Of Integer, Vector3))
            Dim steps = Math.Max(0, HavokPhysicsSettings.SettleSteps)
            For s = 0 To steps - 1
                Simulate(st, target, skinned, HavokPhysicsSettings.FixedTimeStep)
            Next
        End Sub

        Private Shared Sub SolveDistanceLinks(st As ClothSimState)
            For Each l In st.Links
                Dim pa = st.Positions(l.A), pb = st.Positions(l.B)
                Dim d = pb - pa
                Dim len = d.Length
                If len <= 0.000001F Then Continue For
                Dim diff = (len - l.Rest) / len
                Dim wa = st.InvMass(l.A), wb = st.InvMass(l.B)
                Dim wsum = wa + wb
                If wsum <= 0.0F Then Continue For
                Dim corr = d * (diff * l.Stiffness)
                st.Positions(l.A) = pa + (corr * (wa / wsum))
                st.Positions(l.B) = pb - (corr * (wb / wsum))
            Next
        End Sub

        ''' <summary>
        ''' La "correa": `hclLocalRangeConstraintSet` limita cuánto puede alejarse la partícula de su
        ''' vértice de referencia SOBRE EL CUERPO SKINNEADO. Sin ella la tela sobre-cae (medido: los
        ''' cloths SIN local-range son exactamente los que sobre-caían, 79,8 % de libres contra 2,4 %).
        ''' </summary>
        Private Shared Sub SolveLocalRange(st As ClothSimState, skinned As Dictionary(Of Integer, Vector3))
            For Each c In st.LocalRange
                If st.InvMass(c.Particle) = 0.0F Then Continue For
                ' ⛔ `referenceVertex` indexa la malla SKINNEADA (el cuerpo), no el array de partículas.
                ' Yo había escrito acá que en el corpus los dos índices coinciden: es FALSO. MEDIDO con
                ' `--clothengine`: difieren en 2.671 de 20.002 constraints (13,35 %). Anclar la partícula
                ' a su propio destino la clava y le anula el grado de libertad que la autoría le dio.
                Dim ref As Vector3 = Nothing
                ' Sin referencia skinneada no hay correa. Saltear es correcto; inventar una la clavaría
                ' en el lugar equivocado, que es peor que no restringirla.
                If Not skinned.TryGetValue(c.ReferenceVertex, ref) Then Continue For
                Dim p = st.Positions(c.Particle)
                Dim d = p - ref
                Dim len = d.Length
                If len <= c.MaxDistance OrElse len <= 0.000001F Then Continue For
                st.Positions(c.Particle) = ref + (d * (c.MaxDistance / len))
            Next
        End Sub

        Private Shared Sub SolveCapsules(st As ClothSimState)
            If st.Capsules.Count = 0 Then Exit Sub
            For i = 0 To st.Positions.Length - 1
                If st.InvMass(i) = 0.0F Then Continue For
                Dim p = st.Positions(i)
                For Each c In st.Capsules
                    Dim t = 0.0F
                    Dim closest = ClosestPointOnSegment(p, c.A, c.B, t)
                    ' Radio INTERPOLADO por la posición sobre el eje: la cápsula es un cono truncado,
                    ' no un cilindro (602 de 602 tapered del corpus tienen los dos radios distintos).
                    Dim radius = c.Radius + ((c.RadiusB - c.Radius) * t)
                    Dim d = p - closest
                    Dim len = d.Length
                    If len >= radius Then Continue For
                    If len <= 0.000001F Then
                        p = closest + New Vector3(0.0F, 0.0F, radius)
                    Else
                        p = closest + (d * (radius / len))
                    End If
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
                                           clothSkel As HkaSkeletonGraph_Class, skeleton As SkeletonInstance)
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

            For Each cd In sim.CollidableDetails
                If cd Is Nothing OrElse cd.ShapeDetail Is Nothing Then Continue For
                If cd.ShapeDetail.EndpointA Is Nothing OrElse cd.ShapeDetail.EndpointB Is Nothing Then Continue For
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

            For Each map In deform.BoneMappings
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

                ' Physics = inv(OrigL × Mount × Morph × Delta) × desiredLocal  — un DELTA, como el mount.
                Dim baseLocal = bone.LocaLTransformWithoutPhysics
                If baseLocal Is Nothing Then Continue For
                bone.PhysicsDeltaTransform = baseLocal.Inverse().ComposeTransforms(desiredLocal)
                skeleton.MarkPhysicsLayerWritten()
                _touched.AddOrUpdate(skeleton, Nothing)
            Next
        End Sub

        Private Shared Function SafeNormalize(v As Vector3) As Vector3
            Dim l2 = v.LengthSquared
            If l2 <= 0.0F Then Return Vector3.Zero
            Return v / CSng(Math.Sqrt(l2))
        End Function

        ' -----------------------------------------------------------------------------------------
        ' ObjectSpaceSkin: la malla de sim skinneada al cuerpo POSADO
        ' -----------------------------------------------------------------------------------------
        Private Shared Function BuildSkinnedByVertex(skin As HclObjectSpaceSkinPNOperatorGraph_Class,
                                                     clothSkel As HkaSkeletonGraph_Class,
                                                     bindWorld As Matrix4(),
                                                     skeleton As SkeletonInstance) As Dictionary(Of Integer, Vector3)
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
            Next

            For Each blk In skin.SkinBlocks
                If blk Is Nothing OrElse blk.InfluenceBlock Is Nothing OrElse blk.VertexEntries Is Nothing Then Continue For
                For Each entry In blk.VertexEntries
                    If entry Is Nothing OrElse entry.Position Is Nothing Then Continue For
                    If entry.SlotIndex < 0 OrElse entry.SlotIndex >= blk.InfluenceBlock.VertexInfluences.Count Then Continue For
                    Dim lane = blk.InfluenceBlock.VertexInfluences(entry.SlotIndex)
                    If lane Is Nothing Then Continue For
                    Dim sp = SkinPoint(entry.Position, lane, slotMat, slotOk)
                    If sp.HasValue Then result(CInt(entry.VertexIndex)) = sp.Value
                Next
            Next
            Return result
        End Function

        ''' <summary>Σ_k (w_k/255) · localPos · M[k] — la fórmula derivada y validada a 0,0011 u de media.</summary>
        Private Shared Function SkinPoint(localPoint As HclObjectSpaceSkinQuantizedVectorGraph_Class,
                                          lane As HclObjectSpaceSkinVertexInfluenceGraph_Class,
                                          matrices As Matrix4(), valid As Boolean()) As Vector3?
            Dim x = 0.0R, y = 0.0R, z = 0.0R
            Dim any = False
            Dim lx = localPoint.X, ly = localPoint.Y, lz = localPoint.Z
            Dim count = Math.Min(lane.TransformIndices.Count, lane.WeightBytes.Count)
            For i = 0 To count - 1
                Dim ti = CInt(lane.TransformIndices(i))
                If ti < 0 OrElse ti >= matrices.Length OrElse Not valid(ti) Then Continue For
                Dim w = lane.WeightBytes(i) / 255.0R
                If w = 0.0R Then Continue For
                Dim m = matrices(ti)
                x += ((lx * m.M11) + (ly * m.M21) + (lz * m.M31) + m.M41) * w
                y += ((lx * m.M12) + (ly * m.M22) + (lz * m.M32) + m.M42) * w
                z += ((lx * m.M13) + (ly * m.M23) + (lz * m.M33) + m.M43) * w
                any = True
            Next
            If Not any Then Return Nothing
            Return New Vector3(CSng(x), CSng(y), CSng(z))
        End Function

        ''' <summary>Bind global de cada hueso del cloth-skeleton embebido (ReferencePose compuesto por padres).</summary>
        Private Shared Function ComputeEmbeddedBindWorld(skel As HkaSkeletonGraph_Class) As Matrix4()
            If skel?.Bones Is Nothing OrElse skel.ReferencePose Is Nothing Then Return New Matrix4() {}
            Dim n = skel.Bones.Count
            Dim world(Math.Max(0, n - 1)) As Matrix4
            Dim parents = skel.ParentIndices
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
                    world(i) = localM
                End If
            Next
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
