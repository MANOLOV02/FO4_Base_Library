' Version Uploaded of Fo4Library 3.2.0
Imports NiflySharp
Imports NiflySharp.Blocks

''' <summary>
''' Single bone in a <see cref="SkeletonInstance"/>'s hierarchy. Carries bind transform
''' (<see cref="OriginalLocaLTransform"/>) and an optional pose <see cref="DeltaTransform"/>
''' that gets folded into <see cref="LocaLTransform"/> when present.
''' </summary>
Public Class HierarchiBone_class
    Public ReadOnly Property LocaLTransform As Transform_Class
        Get
            If IsNothing(DeltaTransform) Then Return OriginalLocaLTransform
            Return OriginalLocaLTransform.ComposeTransforms(DeltaTransform)
        End Get
    End Property
    Public DeltaTransform As Transform_Class = Nothing
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
    Public ReadOnly Property OriginalGetGlobalTransform As Transform_Class
        Get
            If IsNothing(Parent) Then Return OriginalLocaLTransform
            Return Parent.OriginalGetGlobalTransform.ComposeTransforms(OriginalLocaLTransform)
        End Get
    End Property
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
    Public Property Pose As Poses_class
    Private ReadOnly _lock As New Object

    Public ReadOnly Property HasSkeleton As Boolean
        Get
            If Skeleton Is Nothing Then Return False
            If Skeleton.Blocks.Count = 0 Then Return False
            Return True
        End Get
    End Property

    Public Sub ApplyPose(pose As Poses_class)
        SyncLock _lock
            If Not HasSkeleton Then Exit Sub

            Reset()

            If pose Is Nothing Then
                Me.Pose = Nothing
                Exit Sub
            End If

            For Each posbon In pose.Transforms
                If Not SkeletonDictionary.ContainsKey(posbon.Key) Then Continue For
                Dim bon = SkeletonDictionary(posbon.Key)
                Dim bonetrans = bon.OriginalLocaLTransform
                Dim posetrans = New Transform_Class(posbon.Value, pose.Source)
                Dim trans As Transform_Class

                If pose.Source = Poses_class.Pose_Source_Enum.ScreenArcher Then
                    trans = bonetrans.Inverse.ComposeTransforms(posetrans)
                Else
                    trans = posetrans
                End If
                bon.DeltaTransform = trans
                SkeletonDictionary(posbon.Key) = bon
            Next

            Me.Pose = pose
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
                    Dim startNode = If(par IsNot Nothing, par, bon)
                    walkNode(startNode, Nothing)
                End If
            Next

            Return addedCount
        End SyncLock
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

    Public Sub Reset()
        SyncLock _lock
            For Each bon In SkeletonDictionary.Values
                bon.DeltaTransform = Nothing
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
