' Version Uploaded of Fo4Library 3.2.0
Imports NiflySharp.Blocks

''' <summary>
''' Single bone in a <see cref="SkeletonInstance"/>'s hierarchy. Carries bind transform
''' (<see cref="OriginalLocaLTransform"/>) and an optional pose <see cref="DeltaTransform"/>
''' that gets folded into <see cref="LocaLTransform"/> when present.
''' </summary>
Public Class HierarchiBone_class
    ''' <summary>Effective local transform of this bone, composed as 4 layers:
    ''' <c>OriginalLocaLTransform × MountDeltaTransform × MorphDeltaTransform × DeltaTransform</c>.
    ''' <para>
    ''' Capas (de bind hacia afuera):
    ''' <list type="bullet">
    ''' <item><c>OriginalLocaLTransform</c> — skeleton bind (jamás se muta).</item>
    ''' <item><c>MountDeltaTransform</c> — chunk-mount correction (delta multiplicativo
    ''' sobre el bind). Se computa como <c>MountDelta = inv(OrigL) × newLocal</c> donde
    ''' <c>newLocal = inv(parent.OriginalGetGlobalTransform) × desiredWorld</c>.</item>
    ''' <item><c>MorphDeltaTransform</c> — bone-morph de apariencia del NPC (race height,
    ''' body weight MWGT/NNAM/MRSV, face FMRI/FMRS, ARMA sculpt) escrito por
    ''' <c>ApplyBoneMorphPose</c>. Capa estructural como el mount: SOBREVIVE a un cambio de
    ''' pose/animación.</item>
    ''' <item><c>DeltaTransform</c> — pose/animación (HKX por frame, ScreenArcher, etc.)
    ''' escrito por <c>ApplyPose</c>. Aplica AL FINAL, libre para animar sin tocar el morph.</item>
    ''' </list>
    ''' </para>
    ''' <para>No-op vs pre-refactor (morph en Delta): con <c>DeltaTransform = Nothing</c>
    ''' (sin animación), <c>OrigL × Mount × Morph</c> es bit-idéntico al viejo
    ''' <c>OrigL × Mount × Delta(morph)</c> — el mismo Transform por hueso que iba a Delta
    ''' ahora va a Morph, y componer con Nothing es no-op.</para>
    ''' <para>Cascade: <c>GetGlobalTransform</c> compone parent chain con esta
    ''' <c>LocaLTransform</c>, propagando Mount/Morph/Delta del parent automáticamente.</para>
    ''' </summary>
    Public ReadOnly Property LocaLTransform As Transform_Class
        Get
            Dim r As Transform_Class = OriginalLocaLTransform
            If MountDeltaTransform IsNot Nothing Then r = r.ComposeTransforms(MountDeltaTransform)
            If MorphDeltaTransform IsNot Nothing Then r = r.ComposeTransforms(MorphDeltaTransform)
            If DeltaTransform IsNot Nothing Then r = r.ComposeTransforms(DeltaTransform)
            Return r
        End Get
    End Property
    ''' <summary>Capa de pose/animación únicamente (NULL = sin pose). Es la capa MÁS externa,
    ''' escrita por <c>ApplyPose</c>. Los morphs de apariencia del NPC ya NO viven aquí — van a
    ''' <see cref="MorphDeltaTransform"/> — para que la animación HKX pueda manejar esta capa
    ''' por frame sin borrarlos. Limpiada por <c>Reset()</c> y <c>ResetPose()</c>.</summary>
    Public DeltaTransform As Transform_Class = Nothing
    ''' <summary>Capa de bone-morph de apariencia del NPC (V3). NULL = sin morph. Va ENTRE
    ''' <see cref="MountDeltaTransform"/> y <see cref="DeltaTransform"/>: composición
    ''' <c>OrigL × MountDelta × Morph × Delta</c> ("skeleton + chunks + morph + pose").
    ''' Lleva race height, body weight (MWGT/NNAM/MRSV), face FMRI/FMRS y ARMA sculpt mergeados,
    ''' escritos por <c>ApplyBoneMorphPose</c>. Capa estructural: SOBREVIVE a un cambio de
    ''' pose/animación (igual que el mount). Excluida de <see cref="OriginalGetGlobalTransform"/>
    ''' (como <c>DeltaTransform</c>). Limpiada por <c>Reset()</c> y <c>ResetMorph()</c>.</summary>
    Public MorphDeltaTransform As Transform_Class = Nothing
    ''' <summary>Chunk-mount correction layer (V2). NULL = no mount correction.
    ''' Delta multiplicativo entre <c>OriginalLocaLTransform</c> y <c>MorphDeltaTransform</c>
    ''' (composición <c>OrigL × MountDelta × Morph × Delta</c>). Computed as
    ''' <c>MountDelta = inv(OrigL) × newLocal</c> donde
    ''' <c>newLocal = inv(parent.OriginalGetGlobalTransform) × desiredWorld</c>.
    ''' Limpiado por <c>Reset()</c>.</summary>
    Public MountDeltaTransform As Transform_Class = Nothing
    Public OriginalLocaLTransform As Transform_Class
    Public BoneName As String
    Public Parent As HierarchiBone_class
    Public Childrens As New List(Of HierarchiBone_class)
    Public ReadOnly Property GetGlobalTransform As Transform_Class
        Get
            If IsNothing(Parent) Then Return LocaLTransform
            Return Parent.GetGlobalTransform.ComposeTransforms(LocaLTransform)
        End Get
    End Property
    ''' <summary>Bind chain INCLUYENDO MountDeltaTransform pero EXCLUYENDO DeltaTransform
    ''' (pose). Replica la semántica pre-refactor de "bind chain con mutaciones V2 aplicadas":
    ''' antes V2 mutaba <c>OriginalLocaLTransform</c> a <c>newLocal</c>, ahora MountDelta
    ''' contiene la corrección equivalente y debe propagarse igual en la cadena parent.
    ''' Sin esto, children que computen <c>newLocal_child = inv(parent.OriginalGetGlobalTransform) × desiredWorld</c>
    ''' no verían la corrección V2 del parent y la cascade quedaría rota.</summary>
    Public ReadOnly Property OriginalGetGlobalTransform As Transform_Class
        Get
            Dim localBind As Transform_Class = OriginalLocaLTransform
            If MountDeltaTransform IsNot Nothing Then
                localBind = localBind.ComposeTransforms(MountDeltaTransform)
            End If
            If IsNothing(Parent) Then Return localBind
            Return Parent.OriginalGetGlobalTransform.ComposeTransforms(localBind)
        End Get
    End Property
End Class

''' <summary>Cache EFÍMERA (por pase de render) de los global transforms de un
''' <see cref="SkeletonInstance"/>. La construye <see cref="SkeletonInstance.BuildGlobalTransformCacheForRenderPass"/>
''' UNA vez por instancia antes del <c>Parallel.ForEach</c> de meshes, y se lee read-only durante el
''' loop. Dos mapas: <c>Display</c> (Original×Mount×Morph×Delta, = <c>GetGlobalTransform</c>) y
''' <c>BindMount</c> (Original×Mount, = <c>OriginalGetGlobalTransform</c>). NO persiste entre frames:
''' se reconstruye desde el estado actual de las capas → sin riesgo de invalidación stale (la
''' superficie de mutación incluye el mount escrito por la APP, así que un generation-counter sería
''' frágil). Si un hueso NO está en la cache (huérfano no alcanzable desde SkeletonStructure), el
''' caller cae al camino recursivo. Keyed por <see cref="HierarchiBone_class"/> (referencia) — el hot
''' path ya tiene el objeto bone.</summary>
Public NotInheritable Class SkeletonGlobalTransformCache
    Private ReadOnly _display As Dictionary(Of HierarchiBone_class, Transform_Class)
    Private ReadOnly _bindMount As Dictionary(Of HierarchiBone_class, Transform_Class)

    Friend Sub New(capacity As Integer)
        Dim cap = Math.Max(0, capacity)
        _display = New Dictionary(Of HierarchiBone_class, Transform_Class)(cap)
        _bindMount = New Dictionary(Of HierarchiBone_class, Transform_Class)(cap)
    End Sub

    Friend Sub Store(bone As HierarchiBone_class, display As Transform_Class, bindMount As Transform_Class)
        _display(bone) = display
        _bindMount(bone) = bindMount
    End Sub

    Friend Function TryGetDisplay(bone As HierarchiBone_class, ByRef value As Transform_Class) As Boolean
        Return _display.TryGetValue(bone, value)
    End Function

    Friend Function TryGetBindMount(bone As HierarchiBone_class, ByRef value As Transform_Class) As Boolean
        Return _bindMount.TryGetValue(bone, value)
    End Function
End Class

''' <summary>
''' Esqueleto cargado en memoria con su jerarquía, dict por nombre, bones inyectados (cloth)
''' y pose actual aplicada. Permite N esqueletos vivos simultáneos (multi-actor en una escena).
'''
''' <see cref="Default"/> es la instancia global usada por consumers que no necesitan
''' multi-actor. Apps que renderizan varios actores construyen sus propias instancias y las
''' entregan vía <see cref="SingleInstanceSkeletonResolver"/> o
''' <see cref="MultiInstanceSkeletonResolver"/>.
''' </summary>
Public Class SkeletonInstance

    ''' <summary>Singleton-esque default instance used by <see cref="DefaultSkeletonResolver"/>
    ''' and any consumer that does not need multi-actor isolation. Single-skeleton apps (WM)
    ''' use this exclusively. Multi-actor apps construct fresh <see cref="SkeletonInstance"/>
    ''' objects per actor.</summary>
    Public Shared ReadOnly [Default] As New SkeletonInstance()

    Public Property Skeleton As Nifcontent_Class_Manolo
    Public Property SkeletonStructure As New List(Of HierarchiBone_class)
    Public Property SkeletonDictionary As New Dictionary(Of String, HierarchiBone_class)(StringComparer.OrdinalIgnoreCase)
    Public ReadOnly Property InjectedBones As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
    ''' <summary>Pose/animación actualmente aplicada a la capa <c>DeltaTransform</c>
    ''' (source-of-truth de "¿hay pose?"). En NPC_Manager los morphs de apariencia NO van aquí
    ''' sino en <see cref="MorphPose"/>.</summary>
    Public Property Pose As Poses_class
    ''' <summary>Bone-morph de apariencia del NPC actualmente aplicado a la capa
    ''' <c>MorphDeltaTransform</c> (source-of-truth de "¿hay morph?"). Escrito por
    ''' <c>ApplyBoneMorphPose</c>. Independiente de <see cref="Pose"/>.</summary>
    Public Property MorphPose As Poses_class
    Private ReadOnly _lock As New Object

    Public ReadOnly Property HasSkeleton As Boolean
        Get
            If Skeleton Is Nothing Then Return False
            If Skeleton.Blocks.Count = 0 Then Return False
            Return True
        End Get
    End Property

    ''' <summary>Memoización #3: construye la cache de global transforms con un pase BFS parent-first
    ''' desde las raíces (<see cref="SkeletonStructure"/>). O(bones) en vez de O(bonesPalette ×
    ''' profundidad) por shape, y compartida por todos los shapes de la instancia.
    ''' <para><b>Bit-idéntico al recursivo</b>: misma asociatividad left-assoc desde la raíz
    ''' (<c>display = parent.display × LocaLTransform</c>, igual que <c>GetGlobalTransform</c>;
    ''' <c>bindMount = parent.bindMount × (Original×Mount)</c>, igual que
    ''' <c>OriginalGetGlobalTransform</c>). Las dos caches se construyen con locales DISTINTOS — bind
    ''' NO se deriva de display ni viceversa.</para>
    ''' <para><b>Precondición de orden</b>: llamar DESPUÉS de <c>ApplyPose</c> / <c>ApplyBoneMorphPose</c>
    ''' / el mount (<c>ApplyMountPlanForActor</c>, escrito por la app) / inyección de cloth-connect
    ''' bones — todos ocurren antes de <c>InvalidateRender</c> (render síncrono), así que al construir
    ''' la cache las capas ya están finales.</para>
    ''' <para>Huesos en <see cref="SkeletonDictionary"/> pero NO alcanzables desde
    ''' <see cref="SkeletonStructure"/> quedan fuera de la cache → el caller (RecomputeGPUBoneMatrices)
    ''' cae al camino recursivo para ellos (fallback seguro).</para></summary>
    Friend Function BuildGlobalTransformCacheForRenderPass() As SkeletonGlobalTransformCache
        SyncLock _lock
            Dim cache As New SkeletonGlobalTransformCache(SkeletonDictionary.Count)
            Dim queue As New Queue(Of HierarchiBone_class)
            For Each root In SkeletonStructure
                If root IsNot Nothing Then queue.Enqueue(root)
            Next
            While queue.Count > 0
                Dim bone = queue.Dequeue()

                ' Local bind+mount (igual que OriginalGetGlobalTransform usa por hueso).
                Dim localBind As Transform_Class = bone.OriginalLocaLTransform
                If bone.MountDeltaTransform IsNot Nothing Then localBind = localBind.ComposeTransforms(bone.MountDeltaTransform)
                ' Local display (las 4 capas; la propiedad ya las compone).
                Dim localDisplay As Transform_Class = bone.LocaLTransform

                Dim display As Transform_Class
                Dim bindMount As Transform_Class
                Dim pDisplay As Transform_Class = Nothing
                Dim pBind As Transform_Class = Nothing
                If bone.Parent Is Nothing Then
                    display = localDisplay
                    bindMount = localBind
                ElseIf cache.TryGetDisplay(bone.Parent, pDisplay) AndAlso cache.TryGetBindMount(bone.Parent, pBind) Then
                    ' BFS parent-first → el padre ya está cacheado.
                    display = pDisplay.ComposeTransforms(localDisplay)
                    bindMount = pBind.ComposeTransforms(localBind)
                Else
                    ' Defensivo (no debería pasar en BFS bien ordenado): recursivo.
                    display = bone.GetGlobalTransform
                    bindMount = bone.OriginalGetGlobalTransform
                End If

                cache.Store(bone, display, bindMount)
                For Each child In bone.Childrens
                    If child IsNot Nothing Then queue.Enqueue(child)
                Next
            End While
            Return cache
        End SyncLock
    End Function

    ''' <summary>Resuelve una entrada de <c>Poses_class.Transforms</c> al <c>Transform_Class</c>
    ''' que se guarda en el hueso, aplicando el manejo ScreenArcher (delta relativo al bind).
    ''' Compartido por <see cref="ApplyPose"/> y <see cref="ApplyBoneMorphPose"/>. Sin lock;
    ''' el caller debe tener <c>_lock</c>.</summary>
    Private Shared Function ResolvePoseTransform(bon As HierarchiBone_class,
                                                 value As PoseTransformData,
                                                 source As Poses_class.Pose_Source_Enum) As Transform_Class
        Dim posetrans = New Transform_Class(value, source)
        If source = Poses_class.Pose_Source_Enum.ScreenArcher Then
            Return bon.OriginalLocaLTransform.Inverse.ComposeTransforms(posetrans)
        End If
        Return posetrans
    End Function

    ''' <summary>Aplica una pose/animación a la capa <c>DeltaTransform</c> (la más externa).
    ''' Limpia SOLO esa capa (<see cref="ResetPose"/>): el bone-morph (<see cref="MorphPose"/>)
    ''' y el chunk-mount sobreviven, de modo que la animación HKX puede manejar esta capa por
    ''' frame sin recomputar la apariencia.</summary>
    Public Sub ApplyPose(pose As Poses_class)
        SyncLock _lock
            If Not HasSkeleton Then Exit Sub

            ResetPose()

            If pose Is Nothing Then
                Me.Pose = Nothing
                Exit Sub
            End If

            For Each posbon In pose.Transforms
                If Not SkeletonDictionary.ContainsKey(posbon.Key) Then Continue For
                Dim bon = SkeletonDictionary(posbon.Key)
                bon.DeltaTransform = ResolvePoseTransform(bon, posbon.Value, pose.Source)
                SkeletonDictionary(posbon.Key) = bon
            Next

            Me.Pose = pose
        End SyncLock
    End Sub

    ''' <summary>Aplica los bone-morphs de apariencia del NPC (race height + body weight +
    ''' face FMRI/FMRS + ARMA sculpt mergeados) a la capa <c>MorphDeltaTransform</c>. Limpia
    ''' SOLO esa capa (<see cref="ResetMorph"/>): la pose/animación (<c>DeltaTransform</c>) y el
    ''' chunk-mount sobreviven. Espejo de <see cref="ApplyPose"/> pero sobre la capa morph.
    ''' <para>Patrón NPC: <c>ApplyBoneMorphPose(morph)</c> + <c>ApplyMountPlanForActor</c>
    ''' + (opcional) <c>ApplyPose(frame)</c> por frame de animación.</para></summary>
    Public Sub ApplyBoneMorphPose(pose As Poses_class)
        SyncLock _lock
            If Not HasSkeleton Then Exit Sub

            ResetMorph()

            If pose Is Nothing Then
                Me.MorphPose = Nothing
                Exit Sub
            End If

            For Each posbon In pose.Transforms
                If Not SkeletonDictionary.ContainsKey(posbon.Key) Then Continue For
                Dim bon = SkeletonDictionary(posbon.Key)
                bon.MorphDeltaTransform = ResolvePoseTransform(bon, posbon.Value, pose.Source)
                SkeletonDictionary(posbon.Key) = bon
            Next

            Me.MorphPose = pose
        End SyncLock
    End Sub

    ''' <summary>Loads a skeleton from an explicit dictionary key. Falls back to the global skeleton path if the key is empty or not found.</summary>
    Public Function LoadFromKey(dictionaryKey As String) As Boolean
        If String.IsNullOrEmpty(dictionaryKey) Then Return LoadFromConfig(True, True)
        Dim loc As FilesDictionary_class.File_Location = Nothing
        If Not FilesDictionary_class.Dictionary.TryGetValue(dictionaryKey, loc) Then Return LoadFromConfig(True, True)
        Return LoadFromBytes(loc.GetBytes)
    End Function

    ''' <summary>Loads a skeleton from raw bytes.</summary>
    Public Function LoadFromBytes(data As Byte()) As Boolean
        If data Is Nothing OrElse data.Length = 0 Then Return False
        SyncLock _lock
            Try
                Skeleton = New Nifcontent_Class_Manolo
                SkeletonStructure.Clear()
                SkeletonDictionary.Clear()
                Skeleton.Load_Manolo(data)
                Return BuildSkeletonStructure()
            Catch ex As Exception
                Skeleton = Nothing
                InjectedBones.Clear()
                Return False
            End Try
        End SyncLock
    End Function

    ''' <summary>Loads the skeleton path configured in Config_App.Current. force=False reuses if already loaded.</summary>
    Public Function LoadFromConfig(force As Boolean, relative As Boolean) As Boolean
        SyncLock _lock
            Try
                If force = False AndAlso HasSkeleton Then Return True
                Skeleton = New Nifcontent_Class_Manolo
                SkeletonStructure.Clear()
                SkeletonDictionary.Clear()
                If relative = False Then
                    Skeleton.Load_Manolo(Config_App.Current.SkeletonFilePath)
                Else
                    Dim relativestr = IO.Path.GetRelativePath(Config_App.Current.DataPath, Config_App.Current.SkeletonFilePath)
                    Dim skel As FilesDictionary_class.File_Location = Nothing
                    If FilesDictionary_class.Dictionary.TryGetValue(relativestr, skel) Then
                        Skeleton.Load_Manolo(skel.GetBytes)
                    End If
                End If
                Return BuildSkeletonStructure()
            Catch ex As Exception
                Skeleton = Nothing
                InjectedBones.Clear()
                Return False
            End Try
        End SyncLock
    End Function

    Private Function BuildSkeletonStructure() As Boolean
        Dim parentMap As New Dictionary(Of Integer, NiNode)
        For Each block In Skeleton.Blocks.OfType(Of NiNode)()
            For Each childRef In block.Children.References
                If childRef.Index >= 0 Then parentMap(childRef.Index) = block
            Next
        Next

        For Each bon As NiNode In Skeleton.Blocks.Where(Function(pf) pf.GetType Is GetType(NiNode))
            Dim bonIndex As Integer
            Dim par As NiNode = Nothing
            If Skeleton.GetBlockIndex(bon, bonIndex) Then
                parentMap.TryGetValue(bonIndex, par)
            End If
            If IsNothing(par) OrElse par.GetType Is GetType(NiflySharp.Blocks.BSFadeNode) Then
                If IsNothing(par) Then
                    AddBone(Nothing, bon)
                Else
                    AddBone(Nothing, par)
                End If
            End If
        Next
        Return SkeletonDictionary.Count <> 0
    End Function

    ''' <summary>Merge bones from an additional skeleton NIF (face skel, robot extension, etc.) into this instance.
    ''' The additional NIF references existing bones (e.g. HEAD) as anchors and adds its own children.
    ''' Idempotent: bones already present are reused as anchors, not duplicated.</summary>
    ''' <returns>Number of new bones added (0 if file failed or all already present).</returns>
    Public Function MergeAdditionalSkeleton(extraSkelBytes As Byte()) As Integer
        If extraSkelBytes Is Nothing OrElse extraSkelBytes.Length = 0 Then Return 0

        SyncLock _lock
            If Not HasSkeleton Then Return 0

            Dim faceNif As New Nifcontent_Class_Manolo
            Try
                faceNif.Load_Manolo(extraSkelBytes)
            Catch
                Return 0
            End Try

            Dim parentMap As New Dictionary(Of Integer, NiNode)
            For Each block In faceNif.Blocks.OfType(Of NiNode)()
                For Each childRef In block.Children.References
                    If childRef.Index >= 0 Then parentMap(childRef.Index) = block
                Next
            Next

            Dim addedCount As Integer = 0

            Dim walkNode As Action(Of NiNode, HierarchiBone_class) = Nothing
            walkNode = Sub(node As NiNode, parentBone As HierarchiBone_class)
                           If node Is Nothing Then Return
                           Dim name = If(node.Name?.String, "")
                           Dim currentBone As HierarchiBone_class = Nothing

                           If name <> "" Then
                               If SkeletonDictionary.TryGetValue(name, currentBone) Then
                                   ' Existing bone — reuse as anchor; traverse its children
                               Else
                                   currentBone = New HierarchiBone_class With {
                                       .BoneName = name,
                                       .Parent = parentBone,
                                       .DeltaTransform = Nothing,
                                       .OriginalLocaLTransform = New Transform_Class(node)
                                   }
                                   If parentBone IsNot Nothing Then
                                       parentBone.Childrens.Add(currentBone)
                                   Else
                                       SkeletonStructure.Add(currentBone)
                                   End If
                                   SkeletonDictionary(name) = currentBone
                                   addedCount += 1
                               End If
                           End If

                           For Each childRef In node.Children.References
                               If childRef.Index < 0 OrElse childRef.Index >= faceNif.Blocks.Count Then Continue For
                               Dim childNode = TryCast(faceNif.Blocks(childRef.Index), NiNode)
                               If childNode IsNot Nothing Then walkNode(childNode, If(currentBone, parentBone))
                           Next
                       End Sub

            For Each bon As NiNode In faceNif.Blocks.Where(Function(pf) pf.GetType Is GetType(NiNode))
                Dim bonIndex As Integer
                Dim par As NiNode = Nothing
                If faceNif.GetBlockIndex(bon, bonIndex) Then
                    parentMap.TryGetValue(bonIndex, par)
                End If
                If IsNothing(par) OrElse par.GetType Is GetType(NiflySharp.Blocks.BSFadeNode) Then
                    Dim startNode = If(par, bon)
                    walkNode(startNode, Nothing)
                End If
            Next

            Return addedCount
        End SyncLock
    End Function

    ''' <summary>Hace AUTORITATIVO al HKX de animación (skeleton.hkx) sobre los huesos COMPARTIDOS del
    ''' skeleton base: cada hueso que existe en el render-NIF Y en el HKX se RE-POSICIONA al world del HKX
    ''' (no-op cuando NIF==HKX = 100/113 razas; flipea oddballs como Behemoth/Turret a la orientación del
    ''' HKX, que es la correcta). NO agrega los huesos solo-HKX: el dato (--mountvalidate) muestra que el
    ''' ensamblaje real del robot está 7–18u LEJOS de CreateABot ⇒ los chunk-bones del HKX NO son la posición
    ''' de render (la define el socket+mount); y Weapon/IK son inertes. hkxBytes Nothing/vacío → no-op
    ''' (fallback NIF puro, ej. Wardrobe Manager). El skeleton de animación se elige porque su root existe en
    ''' el NIF base (el de ragdoll, 'Ragdoll_*', nunca). Ver [[arch_race_behavior_resolution]] / [[arch_mountdelta]].</summary>
    ''' <returns>Cantidad de huesos compartidos re-posicionados al world del HKX (0 si no hay HKX/anim skel).</returns>
    Public Function MergeHkxSkeleton(hkxBytes As Byte()) As Integer
        If hkxBytes Is Nothing OrElse hkxBytes.Length = 0 Then Return 0

        SyncLock _lock
            If Not HasSkeleton Then Return 0

            Dim sk As HkaSkeletonGraph_Class = Nothing
            Try
                Dim sg = HkxObjectGraphParser_Class.BuildGraph(HkxPackfileParser_Class.Parse(hkxBytes))
                Dim cands = sg.GetObjectsByClassName("hkaSkeleton").Select(Function(o) sg.ParseSkeleton(o)).
                               Where(Function(s) s IsNot Nothing AndAlso s.Bones IsNot Nothing AndAlso
                                     s.ParentIndices IsNot Nothing AndAlso s.ReferencePose IsNot Nothing AndAlso
                                     s.ReferencePose.Count >= s.Bones.Count).ToList()
                ' Selección anim-vs-ragdoll: el root del de animación existe en el NIF base; sino, no-'Ragdoll'.
                sk = cands.FirstOrDefault(Function(s) SkeletonDictionary.ContainsKey(HkxRootBoneName(s)))
                If sk Is Nothing Then sk = cands.FirstOrDefault(Function(s) String.IsNullOrEmpty(s.Name) OrElse
                                                                   s.Name.IndexOf("Ragdoll", StringComparison.OrdinalIgnoreCase) < 0)
            Catch
                Return 0
            End Try
            If sk Is Nothing OrElse sk.Bones.Count = 0 Then Return 0

            ' World bind por hueso del HKX (compose ReferencePose vía ParentIndices; orden topológico).
            Dim nB = sk.Bones.Count
            Dim hWorld(nB - 1) As Transform_Class
            Dim hByName As New Dictionary(Of String, Transform_Class)(StringComparer.OrdinalIgnoreCase)
            For i = 0 To nB - 1
                Dim loc = HkxTransformConventionHelper.ToTransform(sk.ReferencePose(i))
                Dim p = If(i < sk.ParentIndices.Count, CInt(sk.ParentIndices(i)), -1)
                hWorld(i) = If(p < 0 OrElse p >= i, loc, hWorld(p).ComposeTransforms(loc))
                Dim nm0 = sk.Bones(i).Name
                If Not String.IsNullOrEmpty(nm0) AndAlso Not hByName.ContainsKey(nm0) Then hByName(nm0) = hWorld(i)
            Next

            Dim changed As Integer = 0

            ' Pass A — compartidos: override el world al del HKX, recorriendo la estructura NIF parent-first
            ' (así el world del parent ya está finalizado cuando computo el local del hijo).
            Dim stack As New Stack(Of HierarchiBone_class)(SkeletonStructure)
            While stack.Count > 0
                Dim b = stack.Pop()
                Dim hw As Transform_Class = Nothing
                If b.BoneName IsNot Nothing AndAlso hByName.TryGetValue(b.BoneName, hw) Then
                    Dim pw = If(b.Parent IsNot Nothing, b.Parent.OriginalGetGlobalTransform, New Transform_Class())
                    b.OriginalLocaLTransform = pw.Inverse().ComposeTransforms(hw)
                    changed += 1
                End If
                For Each c In b.Childrens : stack.Push(c) : Next
            End While

            ' Pass B — solo-HKX: agregar TODOS los bones del HKX que el NIF no tiene (Weapon/IK + chunk-bones de
            ' robot + connect-points C-), colgando de su padre HKX en el bind ensamblado de CreateABot. Son BASE
            ' (no injected). Los chunk-mesh se RE-BINDEAN aparte para encastrar en estas posiciones (ver render).
            For i = 0 To nB - 1
                Dim nm = sk.Bones(i).Name
                If String.IsNullOrEmpty(nm) OrElse SkeletonDictionary.ContainsKey(nm) Then Continue For
                Dim p = If(i < sk.ParentIndices.Count, CInt(sk.ParentIndices(i)), -1)
                Dim parentBone As HierarchiBone_class = Nothing
                If p >= 0 AndAlso p < nB Then SkeletonDictionary.TryGetValue(sk.Bones(p).Name, parentBone)
                Dim pw = If(parentBone IsNot Nothing, parentBone.OriginalGetGlobalTransform, New Transform_Class())
                Dim nbone As New HierarchiBone_class With {
                    .BoneName = nm,
                    .Parent = parentBone,
                    .OriginalLocaLTransform = pw.Inverse().ComposeTransforms(hWorld(i))
                }
                If parentBone IsNot Nothing Then parentBone.Childrens.Add(nbone) Else SkeletonStructure.Add(nbone)
                SkeletonDictionary(nm) = nbone
                changed += 1
            Next

            Return changed
        End SyncLock
    End Function

    ''' <summary>Nombre del root (bone con parent &lt; 0) de un hkaSkeleton parseado.</summary>
    Private Shared Function HkxRootBoneName(s As HkaSkeletonGraph_Class) As String
        For i = 0 To s.Bones.Count - 1
            Dim p = If(i < s.ParentIndices.Count, CInt(s.ParentIndices(i)), -1)
            If p < 0 OrElse p >= s.Bones.Count Then Return s.Bones(i).Name
        Next
        Return If(s.Bones.Count > 0, s.Bones(0).Name, "")
    End Function

    ''' <summary>Cloth-bone injection. If the caller passes a non-null <paramref name="pose"/>,
    ''' it is applied AFTER cloth-inject (correct order so transforms targeting cloth bones
    ''' land on the freshly re-injected instances). If <paramref name="pose"/> is Nothing the
    ''' caller is responsible for having already applied pose via <see cref="ApplyPose"/>;
    ''' this method does NOT auto-re-apply <see cref="Pose"/>, otherwise consumers that call
    ''' ApplyPose explicitly before invoking the render pipeline would pay double Reset+apply
    ''' work and could observe inconsistent intermediate state.</summary>
    Public Sub PrepareForShapes(shapes As IEnumerable(Of IRenderableShape), Optional pose As Poses_class = Nothing)
        SyncLock _lock
            If Not HasSkeleton Then Exit Sub

            ClearInjectedBones()
            Try
                Dim skeletonCache = shapes.
                    Where(Function(s) s.HasPhysics AndAlso s.NifContent IsNot Nothing).
                    Select(Function(s) s.NifContent).
                    Distinct().
                    ToDictionary(
                        Function(nif) nif,
                        Function(nif) SkeletonClothOverlayHelper_Class.ParseClothSkeleton(nif))

                For Each shape In shapes
                    Dim cached As HkaSkeletonGraph_Class = Nothing
                    If shape.NifContent IsNot Nothing Then
                        skeletonCache.TryGetValue(shape.NifContent, cached)
                    End If
                    SkeletonClothOverlayHelper_Class.InjectMissingBonesIntoLiveSkeleton(shape, Me, cached)
                Next
            Catch ex As Exception
                Debugger.Break()
                ClearInjectedBones()
            End Try

            If pose IsNot Nothing Then
                ApplyPose(pose)
            End If
        End SyncLock
    End Sub

    Public Function IsInjectedBone(boneName As String) As Boolean
        If String.IsNullOrWhiteSpace(boneName) Then Return False
        Return InjectedBones.Contains(boneName)
    End Function

    Private Sub ClearInjectedBones()
        If InjectedBones.Count = 0 Then Exit Sub

        Dim injectedNames As New List(Of String)(InjectedBones)
        For Each boneName In injectedNames
            Dim bone As HierarchiBone_class = Nothing
            If Not SkeletonDictionary.TryGetValue(boneName, bone) Then Continue For

            If IsNothing(bone.Parent) Then
                SkeletonStructure.Remove(bone)
            Else
                bone.Parent.Childrens.Remove(bone)
            End If

            SkeletonDictionary.Remove(boneName)
        Next

        InjectedBones.Clear()
    End Sub

    ''' <summary>Teardown total a bind: limpia las TRES capas
    ''' (<c>DeltaTransform</c> pose/animación, <c>MorphDeltaTransform</c> bone-morph,
    ''' <c>MountDeltaTransform</c> chunk mount). Tras esto, <c>GetGlobalTransform</c> devuelve
    ''' el bind puro. Lo usan los callers que quieren "render bind regardless of pose"
    ''' (p.ej. ShapeTypeValidator). Para limpiar una sola capa ver <see cref="ResetPose"/> /
    ''' <see cref="ResetMorph"/>.</summary>
    Public Sub Reset()
        SyncLock _lock
            For Each bon In SkeletonDictionary.Values
                bon.DeltaTransform = Nothing
                bon.MorphDeltaTransform = Nothing
                bon.MountDeltaTransform = Nothing
            Next
        End SyncLock
    End Sub

    ''' <summary>Limpia SOLO la capa de pose/animación (<c>DeltaTransform</c>). Deja intactas
    ''' las capas Morph y Mount. Llamado por <c>ApplyPose</c> antes de escribir el pose nuevo,
    ''' de modo que un cambio de pose/animación NO borra los morphs de apariencia ni el mount
    ''' de los chunks.</summary>
    Public Sub ResetPose()
        SyncLock _lock
            For Each bon In SkeletonDictionary.Values
                bon.DeltaTransform = Nothing
            Next
        End SyncLock
    End Sub

    ''' <summary>Limpia SOLO la capa de bone-morph (<c>MorphDeltaTransform</c>). Deja intactas
    ''' las capas Pose y Mount. Llamado por <c>ApplyBoneMorphPose</c> antes de reaplicar.</summary>
    Public Sub ResetMorph()
        SyncLock _lock
            For Each bon In SkeletonDictionary.Values
                bon.MorphDeltaTransform = Nothing
            Next
        End SyncLock
    End Sub

    Private Sub AddBone(parent As HierarchiBone_class, bone As NiNode)
        Dim donde As HierarchiBone_class
        Dim nuevo As HierarchiBone_class
        If IsNothing(parent) Then
            donde = New HierarchiBone_class
            SkeletonStructure.Add(donde)
            nuevo = donde
        Else
            nuevo = New HierarchiBone_class
            parent.Childrens.Add(nuevo)
        End If
        nuevo.Parent = parent
        nuevo.BoneName = bone.Name.String
        nuevo.DeltaTransform = Nothing
        nuevo.OriginalLocaLTransform = New Transform_Class(bone)
        SkeletonDictionary(bone.Name.String) = nuevo
        For Each chil In bone.Children.References
            If chil.Index >= 0 AndAlso chil.Index < Skeleton.Blocks.Count Then
                Dim childNode = TryCast(Skeleton.Blocks(chil.Index), NiNode)
                If childNode IsNot Nothing Then AddBone(nuevo, childNode)
            End If
        Next
    End Sub

    Public Function GetParentNodeNameSkeleton(boneName As String) As String
        Dim par = GetParentNodeSkeleton(boneName)
        If par Is Nothing Then Return ""
        Dim result = par.Name.String
        If IsNothing(result) Then Return ""
        Return result
    End Function

    Public Function GetParentNodeSkeleton(boneName As String) As NiNode
        If Not HasSkeleton Then Return Nothing
        Dim childIndex As Integer
        Dim child = Skeleton.FindBlockByName(Of NiNode)(boneName)

        If Not Skeleton.GetBlockIndex(child, childIndex) Then Return Nothing

        Dim nodes = Skeleton.Blocks.OfType(Of NiNode)().Where(Function(n) n IsNot child)
        Return nodes.FirstOrDefault(Function(n) n.Children.Indices.Contains(childIndex))
    End Function

End Class
