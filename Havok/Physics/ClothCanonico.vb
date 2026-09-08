Option Strict On
Option Explicit On

Imports System.Collections.Concurrent
Imports NiflySharp.Blocks
Imports OpenTK.Mathematics

' =================================================================================================
' EL CABLEADO — de las shapes del render al motor canónico, y de vuelta.
'
' Esto es TODO lo que la app necesita para tener física de tela. No hay solver acá: el solver es
' `Havok.Motor`, transcrito del `.exe`. Acá sólo se hace lo que el motor no puede saber solo:
'
'   1. qué shapes tienen un `BSClothExtraData` y qué paquete `hcl` trae cada una;
'   2. dónde está cada hueso VIVO este frame (`poseDe`), que es lo que mueve los colisionables;
'   3. qué `hclClothState` correr;
'   4. volcar el `transformSet` de salida del deform a `HierarchiBone_class.PhysicsDeltaTransform`.
'
' ⛔⛔ SÓLO DEBUG. `Havok.Motor` entero vive dentro de `#If DEBUG`; en Release este archivo no
' compila nada y `HavokPhysicsSettings.Enabled` queda en False por el gate de `Config_Class`.
'
' ⛔ El estado VIVE entre frames y por eso hay un diccionario: `Instancia.Posiciones`/`Previas` son
' el estado del Verlet, los `Colisionable` guardan la pose vieja de la que sale su velocidad, y el
' buffer de tipo 1 comparte el array de partículas. Rearmar la prenda cada frame es no simular.
' =================================================================================================

#If DEBUG Then

Namespace Havok.Physics

    ''' <summary>El cableado del motor canónico de tela. Sin política de solver: sólo el puente.</summary>
    Public NotInheritable Class ClothCanonico

        Private Sub New()
        End Sub

        ''' <summary>El paquete `hcl` parseado de cada bloque. Cachear es obligatorio: parsear un
        ''' packfile por frame no es una opción.</summary>
        Private Shared ReadOnly _paquete As New Dictionary(Of BSClothExtraData, HclClothPackageGraph_Class)

        ''' <summary>El estado vivo por bloque. ⛔ Persiste entre frames — ver la cabecera.</summary>
        Private Shared ReadOnly _prendas As New Dictionary(Of BSClothExtraData, Havok.Motor.PrendaSimulada())

        ''' <summary>
        ''' Las prendas simuladas vivas, para que el ARNÉS pueda medir adentro.
        ''' <para>⛔ Sólo lectura y sólo Debug: no cambia ninguna ley, expone lo que ya existe. Hace
        ''' falta porque medir el marco de triángulo de un cloth-bone concreto —el `|a × b|` en el
        ''' bind contra el `|a × b|` en el frame que da la púa— requiere el buffer de simulación y
        ''' los pares del deform, y los dos viven acá adentro.</para>
        ''' </summary>
        Friend Shared Function PrendasVivas() As List(Of Havok.Motor.PrendaSimulada)
            Dim r As New List(Of Havok.Motor.PrendaSimulada)()
            SyncLock _prendas
                For Each kv In _prendas
                    If kv.Value Is Nothing Then Continue For
                    For Each p In kv.Value
                        If p IsNot Nothing Then r.Add(p)
                    Next
                Next
            End SyncLock
            Return r
        End Function

        ''' <summary>Los esqueletos a los que esta simulación le escribió la capa, para poder
        ''' limpiarla cuando se apaga.</summary>
        Private Shared ReadOnly _tocados As New ConcurrentDictionary(Of SkeletonInstance, Object)

        ''' <summary>
        ''' Corre un frame de física sobre las shapes con `HasPhysics` y escribe la capa
        ''' `PhysicsDeltaTransform` de los cloth-bones. Llamar DESPUÉS de `ApplyPose` y ANTES del
        ''' skinning.
        ''' </summary>
        Public Shared Sub StepShapes(shapes As IEnumerable(Of IRenderableShape),
                                     skeleton As SkeletonInstance,
                                     Optional deltaSeconds As Single = -1.0F)
            If skeleton Is Nothing OrElse Not skeleton.HasSkeleton Then Exit Sub
            If Not HavokPhysicsSettings.Enabled OrElse HavokPhysicsSettings.Mode = HavokPhysicsMode.Off Then
                LimpiarCapa(skeleton)
                Exit Sub
            End If
            If shapes Is Nothing Then Exit Sub

            Dim dt = If(deltaSeconds > 0.0F, deltaSeconds, HavokPhysicsSettings.FixedTimeStep)

            ' ⛔ UN bloque por prenda aunque lo compartan varias shapes: el `BSClothExtraData` es el
            ' dueño del estado, y simular dos veces el mismo bloque le duplica el dt.
            Dim vistos As New HashSet(Of BSClothExtraData)()
            Dim bloques As New List(Of BSClothExtraData)()
            For Each shape In shapes
                If shape Is Nothing OrElse Not shape.HasPhysics OrElse shape.NifContent Is Nothing Then Continue For
                Dim bl = SkeletonClothOverlayHelper_Class.ResolveClothBlockForShape(shape)
                If bl Is Nothing OrElse Not vistos.Add(bl) Then Continue For
                bloques.Add(bl)
            Next

            For Each bq In bloques
                Try
                    CorrerBloque(bq, skeleton, dt)
                Catch ex As Exception
                    Dim exL = ex
                    Logger.LogLazy(Function() $"[CLOTH-CANON] la prenda fallo y queda SIN fisica: {exL}")
                End Try
            Next
        End Sub

        ''' <summary>Limpia la capa de física del esqueleto.</summary>
        Public Shared Sub LimpiarCapa(skeleton As SkeletonInstance)
            If skeleton Is Nothing Then Exit Sub
            skeleton.ResetPhysics()
        End Sub

        ''' <summary>Limpia la capa en TODO esqueleto que esta simulación haya tocado.</summary>
        Friend Shared Sub LimpiarTodos()
            For Each kv In _tocados
                kv.Key?.ResetPhysics()
            Next
            _tocados.Clear()
        End Sub

        ''' <summary>Tira el estado vivo. Lo usan los arneses entre corridas: sin esto, la segunda
        ''' corrida arranca de las partículas que dejó la primera y el A/A no compara nada.</summary>
        Public Shared Sub ResetAll()
            SyncLock _prendas
                _prendas.Clear()
            End SyncLock
        End Sub

        Private Shared Sub CorrerBloque(bloque As BSClothExtraData, skeleton As SkeletonInstance, dt As Single)
            Dim pkg As HclClothPackageGraph_Class = Nothing
            SyncLock _paquete
                If Not _paquete.TryGetValue(bloque, pkg) Then
                    pkg = HclClothPackageParser_Class.Parse(HkxPackfileParser_Class.Parse(bloque))
                    If pkg Is Nothing Then Exit Sub
                    _paquete(bloque) = pkg
                End If
            End SyncLock

            Dim clothSkel = SkeletonClothOverlayHelper_Class.ParseClothSkeletonForBlock(bloque)
            If clothSkel Is Nothing OrElse clothSkel.Bones Is Nothing Then Exit Sub

            Dim nombres As New List(Of String)()
            For Each bn In clothSkel.Bones
                nombres.Add(If(bn Is Nothing, Nothing, bn.Name))
            Next

            Dim prendas As Havok.Motor.PrendaSimulada() = Nothing
            SyncLock _prendas
                If Not _prendas.TryGetValue(bloque, prendas) OrElse prendas Is Nothing OrElse
                   prendas.Length <> pkg.ClothConfigs.Count Then
                    prendas = New Havok.Motor.PrendaSimulada(Math.Max(0, pkg.ClothConfigs.Count - 1)) {}
                    _prendas(bloque) = prendas
                End If
            End SyncLock

            ' ⛔⛔ LA POSE VA **SIN LA CAPA DE FISICA**. `GetGlobalTransform` ya trae compuesto el
            ' `PhysicsDeltaTransform` del frame anterior: alimentar los colisionables y el skin con eso
            ' es una realimentacion — la tela mueve el hueso y el hueso mueve la tela. Medido: con la
            ' pose completa el estiron contra el reposo salta de x9,8 a x40,3 y el A/A deja de dar el
            ' mismo numero en los TRES modos.
            Dim poseDe = Function(nm As String) As Single()
                             Dim vivo As HierarchiBone_class = Nothing
                             If Not skeleton.SkeletonDictionary.TryGetValue(nm, vivo) Then Return Nothing
                             Dim t = PoseSinFisica(vivo)
                             If t Is Nothing Then Return Nothing
                             Dim m = t.ToMatrix4()
                             Return New Single() {m.M11, m.M12, m.M13, m.M14,
                                                  m.M21, m.M22, m.M23, m.M24,
                                                  m.M31, m.M32, m.M33, m.M34,
                                                  m.M41, m.M42, m.M43, m.M44}
                         End Function

            For ci = 0 To pkg.ClothConfigs.Count - 1
                Dim cfg = pkg.ClothConfigs(ci)
                If cfg Is Nothing OrElse cfg.ClothData Is Nothing Then Continue For

                If prendas(ci) Is Nothing Then
                    prendas(ci) = Havok.Motor.PrendaSimulada.Crear(cfg.ClothData, EstadoACorrer(cfg), nombres)
                    If prendas(ci) Is Nothing Then Continue For
                    ' ⛔ LA SIEMBRA: las partículas arrancan en la pose de reposo que declara el
                    ' archivo (`hclSimClothData.simClothPoses[0]`) y las previas iguales, o sea
                    ' velocidad cero. Después van los `uNumSimSettleSteps` del motor.
                    Sembrar(prendas(ci), dt, poseDe)
                End If

                prendas(ci).Cuadro(dt, 0, poseDe)
                EscribirCapa(prendas(ci), cfg, skeleton, nombres)
            Next
        End Sub

        ''' <summary>
        ''' El `hclClothState` a correr. `FullSimulation` ⇒ el que declara el
        ''' `hclSimulateOperator`; `DeformOnly` ⇒ el que no. Si la prenda declara uno solo, ese.
        ''' </summary>
        Private Shared Function EstadoACorrer(cfg As HclClothConfigGraph_Class) As Havok.Canon.Objects.HkObj_HclClothState
            Dim estados = cfg.ClothData.ClothStateDatas
            If estados Is Nothing OrElse estados.Count = 0 Then Return Nothing
            Dim quiereSim = HavokPhysicsSettings.CorreSimulacion
            Dim conSim As Havok.Canon.Objects.HkObj_HclClothState = Nothing
            Dim sinSim As Havok.Canon.Objects.HkObj_HclClothState = Nothing
            For Each est In estados
                If est Is Nothing OrElse est.Operators Is Nothing Then Continue For
                Dim tiene = False
                For Each iu In est.Operators
                    Dim idx = CInt(iu)
                    If idx < 0 OrElse idx >= cfg.ClothData.Raw.OperatorsCount Then Continue For
                    Dim crudo = cfg.ClothData.Raw.OperatorsRef(idx)
                    If crudo IsNot Nothing AndAlso
                       Havok.Canon.Objects.HkObj_HclSimulateOperator.Leer(cfg.ClothData.Graph, crudo) IsNot Nothing Then
                        tiene = True
                        Exit For
                    End If
                Next
                If tiene Then
                    If conSim Is Nothing Then conSim = est
                ElseIf sinSim Is Nothing Then
                    sinSim = est
                End If
            Next
            If quiereSim Then Return If(conSim, sinSim)
            Return If(sinSim, conSim)
        End Function

        ''' <summary>
        ''' La siembra y el asentamiento — `uNumSimSettleSteps` = 10, el default compilado en
        ''' `Fallout4.exe`.
        ''' <para>⛔ Las previas arrancan IGUALES a las posiciones: eso es velocidad cero. Dejarlas
        ''' en los ceros del array hace que la velocidad implícita del Verlet sea la posición
        ''' absoluta de la partícula, y la tela sale disparada.</para>
        ''' </summary>
        Private Shared Sub Sembrar(prenda As Havok.Motor.PrendaSimulada, dt As Single,
                                   poseDe As Func(Of String, Single()))
            Dim poses = prenda.Sim.SimClothPoses
            If poses IsNot Nothing AndAlso poses.Count > 0 AndAlso poses(0) IsNot Nothing AndAlso
               poses(0).Positions IsNot Nothing Then
                Dim pp = poses(0).Positions
                For i = 0 To Math.Min(pp.Count, prenda.Estado.NumParticulas) - 1
                    Dim v = Havok.Motor.Fachada.V4(pp(i))
                    Havok.Motor.Simd.Escribir(prenda.Estado.Posiciones, i, v)
                    Havok.Motor.Simd.Escribir(prenda.Estado.Previas, i, v)
                Next
            End If
            For s = 1 To Math.Max(0, HavokPhysicsSettings.SettleSteps)
                prenda.Cuadro(dt, 0, poseDe)
            Next
        End Sub

        ''' <summary>
        ''' ⭐ El `transformSet` de salida del deform a la capa de física del esqueleto.
        ''' <para>`hclSimpleMeshBoneDeformOperator` escribe la pose GLOBAL de cada cloth-bone en
        ''' `transformSets[op.outputTransformSetIdx]`, en el orden de `hkaSkeleton.bones`. Acá esa
        ''' pose se pasa a local contra el padre vivo y se guarda como DELTA sobre la pose sin
        ''' física, igual que la capa de mount.</para>
        ''' <para>⛔ Es un delta y no una pose: `Physics = inv(base local) × local deseada`. Escribir
        ''' la pose directa pisaría el mount y el morph.</para>
        ''' </summary>
        Private Shared Sub EscribirCapa(prenda As Havok.Motor.PrendaSimulada,
                                        cfg As HclClothConfigGraph_Class,
                                        skeleton As SkeletonInstance,
                                        nombres As List(Of String))
            Dim ts = prenda.TransformSetDeSalida
            If ts Is Nothing Then Exit Sub
            Dim escritos = 0, degenerados = 0, sinHueso = 0, sinBase = 0
            ' ⛔⛔ SOLO LOS HUESOS QUE EL DEFORM ESCRIBIO. El `transformSet` tiene una entrada por hueso
            ' del `hkaSkeleton` (277 en el vestido) y el deform solo toca los que sus
            ' `triangleBonePairs` nombran (91). Escribir la capa en los otros 186 mete un delta en
            ' huesos que la tela no simula: medido, 212 huesos con capa y un estiron de x107.
            ' ⛔ EN ORDEN DE PROFUNDIDAD. Cada hijo se compone contra su padre YA ESCRITO; por
            ' indice de hueso, un hijo podia componerse contra el delta del cuadro anterior.
            Dim porProfundidad = New List(Of Integer)(prenda.HuesosConCapa)
            porProfundidad.Sort(Function(x, y) Profundidad(skeleton, nombres, x).CompareTo(Profundidad(skeleton, nombres, y)))

            For Each i In porProfundidad
                If i < 0 OrElse i >= ts.Length OrElse i >= nombres.Count Then Continue For
                Dim nm = nombres(i)
                If String.IsNullOrWhiteSpace(nm) Then Continue For
                Dim bone As HierarchiBone_class = Nothing
                If Not skeleton.SkeletonDictionary.TryGetValue(nm.Trim(), bone) OrElse bone Is Nothing Then
                    sinHueso += 1
                    Continue For
                End If

                Dim world = AMatrix4(ts(i))
                ' ⛔ UN HUESO DEGENERADO NO PUEDE MATAR LA PRENDA. El deform del motor produce filas
                ' nulas cuando el triangulo es degenerado (`NormalizarCrudo` de un vector nulo), y esa
                ' matriz no se puede invertir. Se saltea ese hueso — y se CUENTA, porque un salteo
                ' mudo es por donde entran los defectos que despues nadie encuentra.
                If Not EsInvertible(world) Then
                    degenerados += 1
                    Continue For
                End If

                Dim deseadaLocal As Transform_Class
                If bone.Parent Is Nothing Then
                    deseadaLocal = New Transform_Class(world)
                Else
                    ' ⛔⛔ EL PADRE, CON SU POSE COMPLETA. `PhysicsDeltaTransform` es un delta sobre la
                    ' pose LOCAL, y la local se saca contra el padre tal como esta — y el padre de un
                    ' cloth-bone es casi siempre otro cloth-bone, que ya tiene su delta. Con la pose
                    ' del padre SIN fisica se mezclan dos espacios (el `world` del deform esta en la
                    ' pose viva, el padre en la del bind) y la malla se estira entre los dos.
                    ' ⚠ No es simetrico con `poseDe`: la ENTRADA del motor va sin la capa para que la
                    ' tela no se realimente; la SALIDA se compone contra la pose completa.
                    Dim padre = bone.Parent.GetGlobalTransform
                    If padre Is Nothing Then Continue For
                    deseadaLocal = padre.Inverse().ComposeTransforms(New Transform_Class(world))
                End If

                Dim baseLocal = bone.LocaLTransformWithoutPhysics
                If baseLocal Is Nothing Then
                    sinBase += 1
                    Continue For
                End If
                bone.PhysicsDeltaTransform = baseLocal.Inverse().ComposeTransforms(deseadaLocal)
                escritos += 1
            Next
            If escritos > 0 Then
                skeleton.MarkPhysicsLayerWritten()
                _tocados(skeleton) = Nothing
            End If
            If Logger.Enabled Then
                Dim eq = escritos, nq = prenda.HuesosConCapa.Length, dq = degenerados, hq = sinHueso, bq2 = sinBase
                Logger.LogLazy(Function() $"[CLOTH-CANONCAPA] escritos={eq} de los {nq} que el deform toca · degenerados={dq} · sin hueso vivo={hq} · sin base={bq2}")
            End If
        End Sub

        ''' <summary>Profundidad del hueso en la jerarquia viva (raiz = 0). Un nombre que no
        ''' resuelve devuelve <see cref="Integer.MaxValue"/> para que caiga al final y no se cuele
        ''' delante de un padre real.</summary>
        Private Shared Function Profundidad(skeleton As SkeletonInstance, nombres As List(Of String),
                                            i As Integer) As Integer
            If i < 0 OrElse i >= nombres.Count Then Return Integer.MaxValue
            Dim nm = nombres(i)
            If String.IsNullOrWhiteSpace(nm) Then Return Integer.MaxValue
            Dim b As HierarchiBone_class = Nothing
            If Not skeleton.SkeletonDictionary.TryGetValue(nm.Trim(), b) OrElse b Is Nothing Then Return Integer.MaxValue
            Dim d = 0
            While b.Parent IsNot Nothing AndAlso d < 256
                b = b.Parent
                d += 1
            End While
            Return d
        End Function

        ''' <summary>
        ''' La pose GLOBAL del hueso **sin la capa de fisica** — la cadena de padres compuesta con
        ''' `LocaLTransformWithoutPhysics`.
        ''' <para>⛔ Es la entrada del motor. Con `GetGlobalTransform` (que trae la capa del frame
        ''' anterior) el sistema se realimenta y el error crece frame a frame.</para>
        ''' <para>La guarda de 256 no es un umbral: es el corte de un ciclo en la jerarquia, que
        ''' colgaria el render.</para>
        ''' </summary>
        Private Shared Function PoseSinFisica(bone As HierarchiBone_class) As Transform_Class
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

        ''' <summary>¿Esta matriz se puede invertir? La `Transform_Class` TIRA al invertir una
        ''' singular, y con NaN adentro ni siquiera llega a decirlo.</summary>
        Private Shared Function EsInvertible(m As Matrix4) As Boolean
            Dim d = m.Determinant
            Return Not Single.IsNaN(d) AndAlso Not Single.IsInfinity(d) AndAlso Math.Abs(d) > 1.0E-12F
        End Function

        ''' <summary>Un `Mat4` del motor a la `Matrix4` de OpenTK que usa el esqueleto.</summary>
        Private Shared Function AMatrix4(m As Havok.Motor.Mat4) As Matrix4
            Return New Matrix4(
                System.Runtime.Intrinsics.Vector128.GetElement(m.F0, 0), System.Runtime.Intrinsics.Vector128.GetElement(m.F0, 1),
                System.Runtime.Intrinsics.Vector128.GetElement(m.F0, 2), System.Runtime.Intrinsics.Vector128.GetElement(m.F0, 3),
                System.Runtime.Intrinsics.Vector128.GetElement(m.F1, 0), System.Runtime.Intrinsics.Vector128.GetElement(m.F1, 1),
                System.Runtime.Intrinsics.Vector128.GetElement(m.F1, 2), System.Runtime.Intrinsics.Vector128.GetElement(m.F1, 3),
                System.Runtime.Intrinsics.Vector128.GetElement(m.F2, 0), System.Runtime.Intrinsics.Vector128.GetElement(m.F2, 1),
                System.Runtime.Intrinsics.Vector128.GetElement(m.F2, 2), System.Runtime.Intrinsics.Vector128.GetElement(m.F2, 3),
                System.Runtime.Intrinsics.Vector128.GetElement(m.F3, 0), System.Runtime.Intrinsics.Vector128.GetElement(m.F3, 1),
                System.Runtime.Intrinsics.Vector128.GetElement(m.F3, 2), System.Runtime.Intrinsics.Vector128.GetElement(m.F3, 3))
        End Function

    End Class

End Namespace

#End If
