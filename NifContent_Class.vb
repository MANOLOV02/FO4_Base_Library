' Version Uploaded of Fo4Library 3.2.0
Imports System.IO
Imports System.Security.Cryptography.X509Certificates
Imports MaterialLib
Imports NiflySharp
Imports NiflySharp.Blocks
Imports NiflySharp.Enums
Imports NiflySharp.Structs



Public Class Nifcontent_Class_Manolo
    Inherits NiflySharp.NifFile

    Sub New()
    End Sub
    Public BaseMaterials As New SortedDictionary(Of String, RelatedMaterial_Class)
    Public Class RelatedMaterial_Class
        Public path As String
        Public material As FO4UnifiedMaterial_Class
    End Class
    Public Sub Load_Manolo(Filename As String)
        Try
            Using fs As New FileStream(Filename, FileMode.Open, FileAccess.Read, FileShare.Read)
                Load_Manolo(fs)
            End Using
        Catch ex As Exception
            Throw New Exception(ex.Message)
        End Try
    End Sub

    Public Sub Load_Manolo(FileBytes As Byte())
        Try
            Using ms As New MemoryStream(FileBytes, False)
                Load_Manolo(ms)
            End Using
        Catch ex As Exception
            Throw New Exception(ex.Message)
        End Try
    End Sub

    Private Sub Load_Manolo(input As Stream)
        Try
            input.Position = 0
            MyBase.Load(input)
        Catch ex As Exception
            Throw New Exception(ex.Message)
        End Try

        Read_MaterialsDictionary()

        If HasUnknownBlocks Then
            Debugger.Break()
            Throw New Exception("Unknown blocks")
        End If
    End Sub
    Private Sub Read_MaterialsDictionary()
        BaseMaterials.Clear()
        For Each shap In Me.GetShapes
            If SupportedShape(shap.GetType) Then
                BaseMaterials(shap.Name.String) = GetRelatedMaterial(shap)
            End If
        Next
    End Sub
    Public Function Optimize(Game As Config_App.Game_Enum) As NifFileOptimizeResult
        Dim opt As NifFileOptimizeOptions
        Select Case Game
            Case Config_App.Game_Enum.Fallout4
                opt = New NifFileOptimizeOptions With {.TargetVersion = NiVersion.GetFO4}
            Case Config_App.Game_Enum.Skyrim
                opt = New NifFileOptimizeOptions With {.TargetVersion = NiVersion.GetSSE}
            Case Else
                Debugger.Break()
                Throw New Exception
        End Select
        Dim result = Me.OptimizeFor(opt)

        ' Rebuild BaseMaterials only when OptimizeFor actually renamed duplicates.
        ' `result.DuplicatesRenamed = true` means `RenameDuplicateShapes` appended
        ' "_1"/"_2" etc. to shape names; the dict built in Load_Manolo is keyed by
        ' the original names and would KeyNotFoundException on subsequent
        ' `BaseMaterials(shape.Name.String)` lookups (e.g. via RelatedNifMaterial).
        ' When no rename happened, the dict keys are still valid.
        If Not IsNothing(result) AndAlso result.DuplicatesRenamed Then
            Read_MaterialsDictionary()
        End If

        Return result
    End Function

    Public Shared Function SupportedShape(shapetype As Type) As Boolean
        Select Case shapetype
            Case GetType(NiParticles)
                Return False
            Case GetType(BSStripParticleSystem)
                Return False
            Case GetType(NiParticleSystem)
                Return False
            Case GetType(BSSubIndexTriShape)
                Return True
            Case GetType(BSTriShape)
                Return True
            Case GetType(BSLODTriShape)
                Return True
            Case GetType(BSSegmentedTriShape)
                Return True
            Case GetType(BSMeshLODTriShape)
                Return True
            Case GetType(BSDynamicTriShape)
                Return True
            Case GetType(NiTriShape)
                Return True
            Case GetType(NiTriStrips)
                Return True
            Case Else
                Debugger.Break()
                Throw New Exception
        End Select
        Return False
    End Function
    Public Sub AddTriData(shapeName As String, triPath As String, toRoot As Boolean)
        Dim target As NiAVObject
        If toRoot Then
            target = GetRootNode()
        Else
            target = FindBlockByName(Of INiShape)(shapeName)
        End If

        If target IsNot Nothing Then
            AssignExtraData(target, triPath)
        End If
    End Sub
    Public Sub RemoveTriData(shapeName As String, toRoot As Boolean)
        Dim target As NiAVObject
        If toRoot Then
            target = GetRootNode()
        Else
            target = FindBlockByName(Of INiShape)(shapeName)
        End If

        If Not IsNothing(target) AndAlso Not IsNothing(target.ExtraDataList) Then
            For Each ref As NiRef In target.ExtraDataList.References
                Dim ed As NiStringExtraData
                ed = TryCast(Blocks(ref.Index), NiStringExtraData)
                If Not IsNothing(ed) Then
                    'AssignExtraData()
                    If ed.Name.String = "BODYTRI" Then
                        target.ExtraDataList.RemoveBlockRef(ref.Index)
                        RemoveBlock(ed)
                        RemoveUnreferencedBlocks()
                        Exit Sub
                    End If
                End If
            Next
        End If
    End Sub
    Public Function AssignExtraData(target As NiAVObject, triPath As String) As UInteger
        If Not IsNothing(target.ExtraDataList) Then
            For Each ref As NiRef In target.ExtraDataList.References
                Dim ed As NiStringExtraData = TryCast(Blocks(ref.Index), NiStringExtraData)
                If Not IsNothing(ed) AndAlso ed.Name.String = "BODYTRI" Then
                    ed.StringData.String = triPath
                    Return ref.Index
                End If
            Next
        End If

        Dim triExtraData As New NiStringExtraData With {
            .Name = New NiStringRef("BODYTRI"),
            .StringData = New NiStringRef(triPath)
        }
        Dim extraDataId As UInteger = AddBlock(triExtraData)

        If IsNothing(target.ExtraDataList) Then
            target.ExtraDataList = New NiBlockRefArray(Of NiExtraData)
        End If
        target.ExtraDataList.AddBlockRef(extraDataId)

        Return extraDataId
    End Function

    Public Function GetRelatedMaterial(shap As INiShape) As RelatedMaterial_Class
        Dim prefix = MaterialsPrefix
        Dim shad = GetShader(shap)

        ' Sin shader: material vacío desde shader nulo
        If IsNothing(shad) Then
            Dim mat As New FO4UnifiedMaterial_Class
            mat.Create_From_Shader(Me, shap, New BSLightingShaderProperty)
            Return New RelatedMaterial_Class With {.material = mat, .path = ""}
        End If

        ' Extraer solo lo que difiere entre tipos de shader
        Dim shadName As String
        Dim matType As Type
        Dim createFromShader As Action(Of FO4UnifiedMaterial_Class)

        Select Case shad.GetType
            Case GetType(BSLightingShaderProperty)
                Dim typed = CType(shad, BSLightingShaderProperty)
                shadName = typed.Name.String
                matType = GetType(BGSM)
                createFromShader = Sub(m) m.Create_From_Shader(Me, shap, typed)
            Case GetType(BSEffectShaderProperty)
                Dim typed = CType(shad, BSEffectShaderProperty)
                shadName = typed.Name.String
                matType = GetType(BGEM)
                createFromShader = Sub(m) m.Create_From_Shader(Me, shap, typed)
            Case Else
                Debugger.Break()
                Throw New Exception
        End Select

        ' Lógica común (antes duplicada en cada Case)
        Dim fullpath = shadName.Correct_Path_Separator
        If fullpath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) Then
            fullpath = fullpath.Substring(prefix.Length)
        End If

        Dim material As New FO4UnifiedMaterial_Class
        If fullpath = "" Then
            createFromShader(material)
        Else
            material.Deserialize(prefix & fullpath, matType)
            ' ShaderType is not stored in BGSM files — read from NIF shader
            If matType Is GetType(BGSM) Then
                Dim bslsp = TryCast(shad, BSLightingShaderProperty)
                If bslsp IsNot Nothing Then
                    material.NifShaderType = bslsp.ShaderType_SK_FO4
                End If
            End If
        End If

        Return New RelatedMaterial_Class With {.material = material, .path = fullpath}
    End Function

    Public Sub SetRelatedMaterial(shap As INiShape, MatPath As String, mat As FO4UnifiedMaterial_Class)
        MatPath = MatPath.Correct_Path_Separator
        Dim prefix = MaterialsPrefix
        MatPath = MatPath.StripPrefix(prefix)

        Dim shad = GetShader(shap)

        Select Case Config_App.Current.Game
            Case Config_App.Game_Enum.Fallout4
                Select Case shad.GetType
                    Case GetType(BSLightingShaderProperty)
                        DirectCast(shad, BSShaderProperty).Name.String = MatPath
                    Case GetType(BSEffectShaderProperty)
                        DirectCast(shad, BSEffectShaderProperty).Name.String = MatPath
                    Case Else
                        Debugger.Break()
                        Throw New Exception
                End Select
            Case Config_App.Game_Enum.Skyrim
                Dim saveAction As Action
                Select Case shad.GetType
                    Case GetType(BSLightingShaderProperty)
                        Dim typed = CType(shad, BSLightingShaderProperty)
                        saveAction = Sub() FO4UnifiedMaterial_Class.Save_To_Shader(Me, shap, typed, mat.Underlying_Material, mat.NifShaderType)
                    Case GetType(BSEffectShaderProperty)
                        Dim typed = CType(shad, BSEffectShaderProperty)
                        saveAction = Sub() FO4UnifiedMaterial_Class.Save_To_Shader(Me, shap, typed, mat.Underlying_Material)
                    Case Else
                        Debugger.Break()
                        Throw New Exception
                End Select
                saveAction()
                DirectCast(shad, BSShaderProperty).Name.String = MatPath   ' común a ambos cases
        End Select
    End Sub

    Public Sub Save_As_Manolo(Filename As String, Overwrite As Boolean)
        If IO.File.Exists(Filename) AndAlso Overwrite = False Then
            If MsgBox("NIF File already exists, replace?", vbYesNo, "Warning") = MsgBoxResult.No Then
                Exit Sub
            End If
        End If
        If MyBase.Save(Filename) <> 0 Then
            Throw New Exception("Error saving NIF")
        End If
    End Sub

    Public ReadOnly Property NifShapes As IEnumerable(Of NiflySharp.INiShape)
        Get
            Return Me.GetShapes
        End Get
    End Property

    Public Sub RemoveShape_Manolo(Shape As INiShape)
        Me.RemoveBlock(Shape)
        Me.RemoveUnreferencedBlocks()
    End Sub
    Public Shared Sub Merge_Shapes_Original(DestNif As Nifcontent_Class_Manolo, SrcNif As Nifcontent_Class_Manolo, MergeClothesData As Boolean)
        SrcNif.GetRootNode().Name.String = "Scene Root"
        ' BSClothExtraData is used by both FO4 and SSE (vanilla Havok); sidecar XML (HDT-SMP) is handled separately by SliderSet_Class
        If Not MergeClothesData Then SrcNif.RemoveBlocksOfType(Of BSClothExtraData)()
        SrcNif.RemoveUnreferencedBlocks()
        For Each shap In SrcNif.GetShapes.ToList
            DestNif.CloneShape_Original(shap, shap.Name.String, SrcNif)
        Next
        If MergeClothesData Then CloneRootClothExtraData(DestNif, SrcNif)

    End Sub

    Private Shared Sub CloneRootClothExtraData(DestNif As Nifcontent_Class_Manolo, SrcNif As Nifcontent_Class_Manolo)
        Dim destRoot = DestNif.GetRootNode()
        Dim srcRoot = SrcNif.GetRootNode()
        If IsNothing(destRoot) OrElse IsNothing(srcRoot) Then Exit Sub

        Dim sourceCloth = GetRootExtraData(srcRoot, SrcNif).OfType(Of BSClothExtraData).ToList()
        If sourceCloth.Count = 0 Then
            sourceCloth = SrcNif.Blocks.OfType(Of BSClothExtraData).ToList()
        End If
        If sourceCloth.Count = 0 Then Exit Sub

        Dim destCloth = GetRootExtraData(destRoot, DestNif).OfType(Of BSClothExtraData).ToList()
        If destCloth.Count > 0 Then
            MsgBox("The destination mesh already has physics. Physics from the merged mesh will be omitted.", vbInformation, "Merge Physics")
            Exit Sub
        End If

        If IsNothing(destRoot.ExtraDataList) Then destRoot.ExtraDataList = New NiBlockRefArray(Of NiExtraData)

        For Each srcCloth In sourceCloth
            Dim cloned = TryCast(srcCloth.Clone(), BSClothExtraData)
            If IsNothing(cloned) Then Continue For
            If Not IsNothing(cloned.NextExtraData) Then cloned.NextExtraData.Clear()

            Dim blockId = DestNif.AddBlock(cloned)
            destRoot.ExtraDataList.AddBlockRef(blockId)

            If IsNothing(destRoot.ExtraData) Then
                destRoot.ExtraData = New NiBlockRef(Of NiExtraData) With {
                    .Index = blockId
                }
            End If
        Next
    End Sub

    Private Shared Iterator Function GetRootExtraData(root As NiNode, nif As Nifcontent_Class_Manolo) As IEnumerable(Of NiExtraData)
        If IsNothing(root) OrElse IsNothing(nif) Then Return

        Dim visited As New HashSet(Of Integer)
        If Not IsNothing(root.ExtraData) Then
            Dim current = nif.GetBlock(Of NiExtraData)(root.ExtraData)
            Do While Not IsNothing(current)
                Dim idx = nif.Blocks.IndexOf(current)
                If idx <> -1 AndAlso visited.Add(idx) = False Then Exit Do
                Yield current
                If IsNothing(current.NextExtraData) Then Exit Do
                current = nif.GetBlock(Of NiExtraData)(current.NextExtraData)
            Loop
        End If

        If IsNothing(root.ExtraDataList) Then Return

        For Each reference In root.ExtraDataList.References
            Dim extra = nif.GetBlock(Of NiExtraData)(reference)
            If IsNothing(extra) Then Continue For

            Dim idx = nif.Blocks.IndexOf(extra)
            If idx = -1 OrElse visited.Add(idx) Then Yield extra
        Next
    End Function

    Public Function CloneShape_Original(srcShape As INiShape, destShapeName As String, srcNif As Nifcontent_Class_Manolo) As INiShape
        If srcShape.GetType Is GetType(BSDynamicTriShape) Then
            ' TESTEAR QUE ANDA !!!!!
            Debugger.Break()
        End If

        Dim destShape = Me.CloneShape(srcShape, destShapeName, srcNif)

        ' NiflySharp DeepCopyHelper bug workaround: structs short-circuit at DeepCopy
        ' (DeepCopyHelper.cs:37 `type.IsValueType → return original`), so reference-type
        ' FIELDS inside struct elements of cloned lists stay aliased with the source.
        ' Specifically for NiSkinData: BoneList is List<BoneData> (struct); cloned fine
        ' element-wise, but each struct's VertexWeights (List<BoneVertData>) is SHARED
        ' between source and clone.  Subsequent SetSkinning on the clone mutates the
        ' source's BoneList too → UpdateSkinPartitions(source) reads stale/overwritten
        ' weights and throws KeyNotFound on vertex indices that no longer exist in the
        ' aliased list.  Same category of bug we handle for BSGeometrySegmentData.SubSegment
        ' in SplitShapeHelper — fix by manually deep-cloning the nested reference fields.
        If destShape IsNot Nothing Then
            Dim destSkinInst = TryCast(GetBlock(Of NiSkinInstance)(destShape.SkinInstanceRef), NiSkinInstance)
            If destSkinInst IsNot Nothing Then
                Dim destSkinData = GetBlock(destSkinInst.Data)
                If destSkinData IsNot Nothing AndAlso destSkinData.BoneList IsNot Nothing Then
                    Dim newBoneList As New List(Of NiflySharp.Structs.BoneData)(destSkinData.BoneList.Count)
                    For Each bone In destSkinData.BoneList
                        Dim copy = bone   ' struct copy
                        If bone.VertexWeights IsNot Nothing Then
                            copy.VertexWeights = New List(Of NiflySharp.Structs.BoneVertData)(bone.VertexWeights)
                        End If
                        newBoneList.Add(copy)
                    Next
                    destSkinData.BoneList = newBoneList
                End If
            End If
        End If

        Return destShape
    End Function


    ''' Returns the internal triParts list from a NiSkinPartition via reflection.
    ''' triParts is internal to NiflySharp; reflection lets us read/write it without
    ''' adding any public API to that library.
    Private Shared Function GetTriParts(skinPart As NiflySharp.Blocks.NiSkinPartition) As List(Of Integer)
        Static field As Reflection.FieldInfo = GetType(NiflySharp.Blocks.NiSkinPartition).GetField(
            "triParts", Reflection.BindingFlags.NonPublic Or Reflection.BindingFlags.Instance)
        Return CType(field.GetValue(skinPart), List(Of Integer))
    End Function

    ''' Removes partitions with no triangle assignments from NiSkinPartition.Partitions
    ''' and BSDismemberSkinInstance.Partitions, remapping triParts accordingly.
    ''' Prevents the NiflySharp null-partBones crash for empty partitions.
    Private Sub CompactEmptyPartitions(shape As INiShape)
        Dim skinInst = GetBlock(Of NiSkinInstance)(shape.SkinInstanceRef)
        If skinInst Is Nothing Then Return
        Dim skinPart = GetBlock(skinInst.SkinPartition)
        If skinPart Is Nothing OrElse skinPart.Partitions Is Nothing OrElse skinPart.Partitions.Count = 0 Then Return
        Dim triPartsField = GetTriParts(skinPart)

        Dim triCount(skinPart.Partitions.Count - 1) As Integer
        If triPartsField.Count > 0 Then
            ' triParts is populated — use it to count triangles per partition.
            For Each partIdx In triPartsField
                If partIdx >= 0 AndAlso partIdx < triCount.Length Then triCount(partIdx) += 1
            Next
        ElseIf skinPart.Partitions.Any(Function(p) p.TrianglesCopy IsNot Nothing) Then
            ' triParts was cleared (e.g., after RemapSkinPartitionTriangles) but TrianglesCopy
            ' is set — use TrianglesCopy counts directly.
            For i As Integer = 0 To skinPart.Partitions.Count - 1
                Dim p = skinPart.Partitions(i)
                triCount(i) = If(p.TrianglesCopy IsNot Nothing, p.TrianglesCopy.Count, 0)
            Next
        Else
            Return  ' truly fresh load; triParts not yet computed — let base handle
        End If

        Dim oldToNew(triCount.Length - 1) As Integer
        Dim newIdx As Integer = 0
        For i As Integer = 0 To triCount.Length - 1
            oldToNew(i) = If(triCount(i) > 0, newIdx, -1)
            If triCount(i) > 0 Then newIdx += 1
        Next
        If newIdx = triCount.Length Then Return  ' nothing to compact

        For i As Integer = 0 To triPartsField.Count - 1
            Dim p = triPartsField(i)
            If p >= 0 AndAlso p < oldToNew.Length Then triPartsField(i) = oldToNew(p)
        Next

        Dim newParts As New List(Of SkinPartition)(newIdx)
        For i As Integer = 0 To skinPart.Partitions.Count - 1
            If oldToNew(i) >= 0 Then newParts.Add(skinPart.Partitions(i))
        Next
        skinPart.Partitions = newParts
        skinPart.NumPartitions = CUInt(newParts.Count)

        Dim bsdSkinInst = TryCast(skinInst, BSDismemberSkinInstance)
        If bsdSkinInst?.Partitions IsNot Nothing Then
            Dim newBsdParts As New List(Of BodyPartList)(newIdx)
            For i As Integer = 0 To Math.Min(bsdSkinInst.Partitions.Count, oldToNew.Length) - 1
                If oldToNew(i) >= 0 Then newBsdParts.Add(bsdSkinInst.Partitions(i))
            Next
            bsdSkinInst.Partitions = newBsdParts
            bsdSkinInst.NumPartitions = CUInt(newBsdParts.Count)
        End If
    End Sub

    ''' <summary>
    ''' Shadows NifFile.UpdateSkinPartitions: compacts empty partitions first so the
    ''' unmodified NiflySharp code never encounters a null partBones entry.
    '''
    ''' ORDER CONTRACT (critical for correctness, especially NiTriShape family):
    '''   1. Any caller mutating per-vertex data (positions, per-vertex skin) MUST do so
    '''      via the IShapeGeometry adapter (SetVertexPositions, SetSkinning, SetTriangles)
    '''      BEFORE calling this.
    '''   2. For the NiTriShape family, the partition is regenerated from NiSkinData by
    '''      NiflySharp's UpdateSkinPartitions.  If the caller calls UpdateSkinPartitions
    '''      BEFORE SetSkinning, the partition is built from stale NiSkinData → saved NIF
    '''      has partition inconsistent with NiSkinData → skinning in-game corrupt.
    '''   3. SkinningHelper.InjectToTrishape + Wardrobe_Manager.BuildingForm follow this
    '''      order correctly (InjectToTrishape calls adapter.SetSkinning;
    '''      UpdateSkinPartitions is called later by BuildingForm).  Any new caller that
    '''      batches these operations must respect the same order.
    ''' </summary>
    Public Shadows Sub UpdateSkinPartitions(shape As INiShape)
        CompactEmptyPartitions(shape)
        MyBase.UpdateSkinPartitions(shape)
    End Sub

    ''' <summary>
    ''' Returns the BSDismemberBodyPartType value (cast to Integer) for each triangle in
    ''' the shape's skin partition, in triangle-list order.  Returns -1 for unassigned
    ''' triangles or when there is no BSDismemberSkinInstance.  Returns Nothing when the
    ''' shape has no NiSkinInstance or no NiSkinPartition (e.g. FO4 shapes).
    ''' </summary>
    Public Function GetTriangleBodyParts(shape As INiShape) As List(Of Integer)
        Dim skinInst = GetBlock(Of NiSkinInstance)(shape.SkinInstanceRef)
        If skinInst Is Nothing Then Return Nothing
        Dim skinPart = GetBlock(skinInst.SkinPartition)
        If skinPart Is Nothing Then Return Nothing

        Dim tris = shape.Triangles.ToList()

        ' Guard against the NiflySharp Count>0 bug in PrepareTrueTriangles:
        ' only call it when TrianglesCopy is null (fresh load); skip if already set.
        Dim triPartsField = GetTriParts(skinPart)
        If triPartsField.Count <> tris.Count Then
            If skinPart.Partitions.Any(Function(p) p.TrianglesCopy Is Nothing) Then
                skinPart.PrepareTrueTriangles()
            End If
            skinPart.GenerateTriPartsFromTrueTriangles(tris)
        End If

        Dim bsdSkinInst = TryCast(skinInst, BSDismemberSkinInstance)
        Dim bsdParts = bsdSkinInst?.Partitions
        Dim result As New List(Of Integer)(tris.Count)
        For Each partInd In triPartsField
            If bsdParts IsNot Nothing AndAlso partInd >= 0 AndAlso partInd < bsdParts.Count Then
                result.Add(CInt(bsdParts(partInd).BodyPart))
            Else
                result.Add(-1)
            End If
        Next
        Return result
    End Function

    ''' <summary>
    ''' Pre-sets body-part assignments per triangle before calling UpdateSkinPartitions.
    ''' triangleBodyParts(i) is the BSDismemberBodyPartType value (cast to Integer) for
    ''' triangle i; -1 means partition 0.  Missing body-part partitions are added to
    ''' BSDismemberSkinInstance automatically.  No-op for FO4 shapes.
    ''' </summary>
    Public Sub SetTriangleBodyParts(shape As INiShape, triangleBodyParts As IReadOnlyList(Of Integer))
        Dim skinInst = GetBlock(Of NiSkinInstance)(shape.SkinInstanceRef)
        If skinInst Is Nothing Then Return
        Dim skinPart = GetBlock(skinInst.SkinPartition)
        If skinPart Is Nothing Then Return

        Dim bsdSkinInst = TryCast(skinInst, BSDismemberSkinInstance)
        Dim bsdParts = bsdSkinInst?.Partitions

        ' Build body-part value → partition index map from existing partitions.
        Dim bpToPartIndex As New Dictionary(Of Integer, Integer)()
        If bsdParts IsNot Nothing Then
            For i As Integer = 0 To bsdParts.Count - 1
                bpToPartIndex(CInt(bsdParts(i).BodyPart)) = i
            Next
        End If

        ' Build TriParts, adding new partitions to bsdSkinInst as needed.
        Dim newTriParts As New List(Of Integer)(triangleBodyParts.Count)
        For Each bp In triangleBodyParts
            If bp < 0 Then
                newTriParts.Add(0)
                Continue For
            End If
            Dim partIdx As Integer
            If Not bpToPartIndex.TryGetValue(bp, partIdx) Then
                partIdx = If(bsdParts IsNot Nothing, bsdParts.Count, 0)
                bpToPartIndex(bp) = partIdx
                If bsdSkinInst IsNot Nothing Then
                    If bsdParts Is Nothing Then
                        bsdSkinInst.Partitions = New List(Of BodyPartList)()
                        bsdParts = bsdSkinInst.Partitions
                    End If
                    bsdParts.Add(New BodyPartList() With {
                        .PartFlag = BSPartFlag.PF_EDITOR_VISIBLE,
                        .BodyPart = CType(bp, BSDismemberBodyPartType)
                    })
                    bsdSkinInst.NumPartitions = CUInt(bsdParts.Count)
                End If
            End If
            newTriParts.Add(partIdx)
        Next

        ' Sync NiSkinPartition.Partitions count to match BSDismemberSkinInstance.
        Dim numParts As Integer
        If bsdParts IsNot Nothing Then
            numParts = bsdParts.Count
        Else
            numParts = Math.Max(1, If(newTriParts.Count > 0, newTriParts.Max() + 1, 1))
        End If
        If skinPart.Partitions Is Nothing Then skinPart.Partitions = New List(Of SkinPartition)()
        Do While skinPart.Partitions.Count < numParts
            skinPart.Partitions.Add(New SkinPartition())
        Loop
        skinPart.NumPartitions = CUInt(skinPart.Partitions.Count)

        ' Set TriParts directly so UpdateSkinPartitions skips PrepareTriParts.
        GetTriParts(skinPart).Clear()
        GetTriParts(skinPart).AddRange(newTriParts)
    End Sub

    ''' <summary>
    ''' Remaps vertex indices in the shape's skin partition TrianglesCopy using oldToNew.
    ''' Triangles whose vertices are absent from the map are dropped.
    ''' Call before UpdateSkinPartitions whenever vertex compaction changes indices
    ''' (e.g. zap removal or shape splitting).
    ''' </summary>
    Public Sub RemapSkinPartitionTriangles(shape As INiShape, oldToNew As IReadOnlyDictionary(Of Integer, Integer))
        Dim skinInst = GetBlock(Of NiSkinInstance)(shape.SkinInstanceRef)
        If skinInst Is Nothing Then Return
        Dim skinPart = GetBlock(skinInst.SkinPartition)
        If skinPart Is Nothing Then Return

        ' Guard against the NiflySharp Count>0 bug: only call PrepareTrueTriangles
        ' when TrianglesCopy is null (fresh load); empty [] means intentionally zapped.
        If skinPart.Partitions.Any(Function(p) p.TrianglesCopy Is Nothing) Then
            skinPart.PrepareTrueTriangles()
        End If

        For i As Integer = 0 To skinPart.Partitions.Count - 1
            Dim p = skinPart.Partitions(i)
            If p.TrianglesCopy Is Nothing OrElse p.TrianglesCopy.Count = 0 Then Continue For
            Dim remapped As New List(Of Triangle)(p.TrianglesCopy.Count)
            For Each t In p.TrianglesCopy
                Dim nv1 As Integer, nv2 As Integer, nv3 As Integer
                If oldToNew.TryGetValue(CInt(t.V1), nv1) AndAlso
                   oldToNew.TryGetValue(CInt(t.V2), nv2) AndAlso
                   oldToNew.TryGetValue(CInt(t.V3), nv3) Then
                    remapped.Add(New Triangle(CUShort(nv1), CUShort(nv2), CUShort(nv3)))
                End If
            Next
            p.TrianglesCopy = remapped
            p.NumTriangles = CUShort(remapped.Count)
            skinPart.Partitions(i) = p
        Next
        GetTriParts(skinPart).Clear()
    End Sub

End Class
