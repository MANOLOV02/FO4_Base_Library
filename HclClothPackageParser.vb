' Version Uploaded of Fo4Library 3.2.0
Option Strict On
Option Explicit On

' =============================================================================
' ESTADO: DEBUG / EN REVISIÓN — NO CERRADO
' -----------------------------------------------------------------------------
' Orquestador del parseo completo de un HKX de tela embebido en un NIF:
' skeleton, collidables, capsule shapes, cloth data, operators, states.
'
' BUILT BUT NOT CONNECTED AL RENDER.
' Ningún caller activo en el proyecto. La ruta del render usa directamente
' HkxPackfileParser + HkxObjectGraphParser + SkeletonClothOverlayHelper,
' sin pasar por este parser completo.
'
' PENDIENTES CONOCIDOS:
'  - Heredará todos los problemas de offsets de HclStructuredGraphParser y
'    HclRenderGraphParser (empíricos, FO4 64-bit only).
'  - PopulateSkinBoneNames, PopulateStateOperatorLinks, PopulateResolvedCollidableBindings,
'    etc.: lógica de resolución de referencias cruzadas a revisar cuando se conecte
'    al render.
'  - Sin soporte para Skyrim SSE (PointerSize=4).
' =============================================================================

Imports System.Collections.Generic
Imports System.Linq
Imports System.Numerics

Public NotInheritable Class HclClothPackageParser_Class
    Public Shared Function Parse(packfile As HkxPackfile_Class) As HclClothPackageGraph_Class
        If IsNothing(packfile) Then Throw New ArgumentNullException(NameOf(packfile))
        Return Parse(HkxObjectGraphParser_Class.BuildGraph(packfile))
    End Function

    Public Shared Function Parse(graph As HkxObjectGraph_Class) As HclClothPackageGraph_Class
        If IsNothing(graph) Then Throw New ArgumentNullException(NameOf(graph))

        Dim result As New HclClothPackageGraph_Class With {
            .Graph = graph,
            .RootContainer = graph.ParseRootLevelContainer()
        }

        Dim skeletonObject = graph.GetObjectsByClassName("hkaSkeleton").FirstOrDefault()
        If Not IsNothing(skeletonObject) Then
            result.Skeleton = graph.ParseSkeleton(skeletonObject)
        End If

        Dim collidables = graph.GetObjectsByClassName("hclCollidable").
            Select(Function(obj) HclStructuredGraphParser_Class.ParseCollidable(graph, obj)).
            Where(Function(detail) Not IsNothing(detail)).
            ToList()
        result.Collidables.AddRange(collidables)

        Dim capsuleShapes = graph.GetObjectsByClassName("hclCapsuleShape").
            Select(Function(obj) HclStructuredGraphParser_Class.ParseCapsuleShape(graph, obj)).
            Where(Function(detail) Not IsNothing(detail)).
            ToList()
        result.CapsuleShapes.AddRange(capsuleShapes)

        For Each clothObject In graph.GetObjectsByClassName("hclClothData")
            Dim clothData = graph.ParseClothData(clothObject)
            If IsNothing(clothData) Then Continue For

            Dim clothConfig As New HclClothConfigGraph_Class With {
                .ClothData = clothData
            }

            clothConfig.SimClothDatas.AddRange(
                clothData.SimClothDatas.
                    Select(Function(obj) HclStructuredGraphParser_Class.ParseSimClothData(graph, obj)).
                    Where(Function(detail) Not IsNothing(detail)))

            clothConfig.BufferDefinitions.AddRange(
                clothData.BufferDefinitions.
                    Select(Function(obj)
                               If obj.ClassName.Equals("hclScratchBufferDefinition", StringComparison.OrdinalIgnoreCase) Then
                                   Return Nothing
                               End If
                               Return HclStructuredGraphParser_Class.ParseBufferDefinition(graph, obj)
                           End Function).
                    Where(Function(detail) Not IsNothing(detail)))

            clothConfig.ScratchBufferDefinitions.AddRange(
                clothData.BufferDefinitions.
                    Select(Function(obj) HclStructuredGraphParser_Class.ParseScratchBufferDefinition(graph, obj)).
                    Where(Function(detail) Not IsNothing(detail)))

            clothConfig.TransformSets.AddRange(
                clothData.TransformSetDefinitions.
                    Select(Function(obj) HclRenderGraphParser_Class.ParseTransformSetDefinition(graph, obj)).
                    Where(Function(detail) Not IsNothing(detail)))

            For Each op In clothData.Operators
                Select Case NormalizeOperatorClassName(op.ClassName)
                    Case "hclobjectspaceskinpnoperator"
                        clothConfig.ObjectSpaceSkin = HclRenderGraphParser_Class.ParseObjectSpaceSkinPNOperator(graph, op)
                        PopulateSkinBoneNames(clothConfig.ObjectSpaceSkin, result.Skeleton)
                    Case "hclmoveparticlesoperator"
                        clothConfig.MoveParticles = HclStructuredGraphParser_Class.ParseMoveParticlesOperator(graph, op)
                    Case "hclsimulateoperator"
                        clothConfig.Simulate = HclStructuredGraphParser_Class.ParseSimulateOperator(graph, op)
                    Case "hclsimplemeshbonedeformoperator"
                        clothConfig.SimpleMeshBoneDeform = HclRenderGraphParser_Class.ParseSimpleMeshBoneDeformOperator(graph, op, result.Skeleton)
                    Case "hclcopyverticesoperator"
                        clothConfig.CopyVertices = HclStructuredGraphParser_Class.ParseCopyVerticesOperator(graph, op)
                    Case "hclgatherallverticesoperator"
                        clothConfig.GatherAllVertices = HclStructuredGraphParser_Class.ParseGatherAllVerticesOperator(graph, op)
                    Case Else
                        clothConfig.UnknownOperators.Add(op)
                End Select
            Next

            clothConfig.ClothStates.AddRange(
                clothData.ClothStates.
                    Select(Function(obj) HclStructuredGraphParser_Class.ParseClothState(graph, obj)).
                    Where(Function(detail) Not IsNothing(detail)))
            PopulateStateOperatorLinks(clothConfig)
            PopulateStateAccessLinks(clothConfig, result.Skeleton)
            PopulateResolvedMoveParticles(clothConfig)
            PopulateResolvedCollidableBindings(clothConfig, result.Skeleton)
            PopulateResolvedSimulateConfigs(clothConfig)
            PopulateResolvedBendTopology(clothConfig)
            PopulateResolvedTriangles(clothConfig)
            PopulateSkinCoverage(clothConfig)
            PopulateSkinDefaultPoseMatches(clothConfig)
            PopulateResolvedVolumeConstraintLinks(clothConfig)

            result.ClothConfigs.Add(clothConfig)
        Next

        result.ConstraintSets.AddRange(
            graph.GetObjectsByClassName("hclStandardLinkConstraintSet").
                Select(Function(obj) CType(HclStructuredGraphParser_Class.ParseStandardLinkConstraintSet(graph, obj), Object)))
        result.ConstraintSets.AddRange(
            graph.GetObjectsByClassName("hclStretchLinkConstraintSet").
                Select(Function(obj) CType(HclStructuredGraphParser_Class.ParseStretchLinkConstraintSet(graph, obj), Object)))
        result.ConstraintSets.AddRange(
            graph.GetObjectsByClassName("hclBendStiffnessConstraintSet").
                Select(Function(obj) CType(HclStructuredGraphParser_Class.ParseBendStiffnessConstraintSet(graph, obj), Object)))
        result.ConstraintSets.AddRange(
            graph.GetObjectsByClassName("hclLocalRangeConstraintSet").
                Select(Function(obj) CType(HclStructuredGraphParser_Class.ParseLocalRangeConstraintSet(graph, obj), Object)))

        Return result
    End Function

    Private Shared Function NormalizeOperatorClassName(className As String) As String
        If String.IsNullOrWhiteSpace(className) Then Return String.Empty
        Return className.Replace(ChrW(0), String.Empty).Trim().ToLowerInvariant()
    End Function

    Private Shared Sub PopulateSkinBoneNames(skin As HclObjectSpaceSkinPNOperatorGraph_Class, skeleton As HkaSkeletonGraph_Class)
        If IsNothing(skin) OrElse IsNothing(skeleton?.Bones) Then Return

        For Each boneIndex In skin.BoneIndices
            If boneIndex >= 0 AndAlso boneIndex < skeleton.Bones.Count Then
                skin.ResolvedBoneNames.Add(skeleton.Bones(CInt(boneIndex)).Name)
            Else
                skin.ResolvedBoneNames.Add("#" & boneIndex.ToString())
            End If
        Next

        For Each subset In skin.TransformSubsets
            PopulateSkinSubsetBoneNames(skin, subset, skeleton)
        Next

        For Each subset In skin.ThreeBlendSubsets
            PopulateSkinSubsetBoneNames(skin, subset, skeleton)
        Next

        For Each subset In skin.TwoBlendSubsets
            PopulateSkinSubsetBoneNames(skin, subset, skeleton)
        Next
    End Sub

    Private Shared Sub PopulateSkinSubsetBoneNames(skin As HclObjectSpaceSkinPNOperatorGraph_Class,
                                                   subset As HclObjectSpaceSkinTransformSubsetGraph_Class,
                                                   skeleton As HkaSkeletonGraph_Class)
        If IsNothing(skin) OrElse IsNothing(subset) OrElse IsNothing(skeleton?.Bones) Then Return

        For Each lane In subset.VertexInfluences
            If lane Is Nothing Then Continue For
            lane.ResolvedSkeletonIndices.Clear()
            lane.ResolvedBoneNames.Clear()
            For Each transformIndex In lane.TransformIndices
                If transformIndex < 0 OrElse transformIndex >= skin.BoneIndices.Count Then Continue For
                Dim skeletonIndex = CInt(skin.BoneIndices(CInt(transformIndex)))
                lane.ResolvedSkeletonIndices.Add(skeletonIndex)
                If skeletonIndex >= 0 AndAlso skeletonIndex < skeleton.Bones.Count Then
                    lane.ResolvedBoneNames.Add(skeleton.Bones(skeletonIndex).Name)
                Else
                    lane.ResolvedBoneNames.Add("#" & skeletonIndex.ToString())
                End If
            Next

        Next
    End Sub

    Private Shared Sub PopulateResolvedMoveParticles(config As HclClothConfigGraph_Class)
        If IsNothing(config?.MoveParticles) Then Return

        For Each sim In config.SimClothDatas
            sim.Field48MatchesMoveParticles = sim.Field48UInt16.SequenceEqual(config.MoveParticles.Pairs.Select(Function(pair) pair.ParticleIndex))
            If sim.Field48MatchesMoveParticles Then
                sim.ResolvedMoveParticlePairs.AddRange(config.MoveParticles.Pairs)
            End If
        Next
    End Sub

    Private Shared Sub PopulateResolvedCollidableBindings(config As HclClothConfigGraph_Class, skeleton As HkaSkeletonGraph_Class)
        If IsNothing(config) Then Return

        For Each sim In config.SimClothDatas
            sim.CollidableBindings.Clear()

            For i = 0 To sim.CollidableDetails.Count - 1
                Dim binding As New HclSimCollidableBinding_Class With {
                    .EntryIndex = i,
                    .Collidable = sim.CollidableDetails(i),
                    .Matrix = If(i < sim.Field98Matrices.Count, sim.Field98Matrices(i), Nothing)
                }

                If i < sim.Field88UInt32.Count Then
                    binding.BoneIndex = CInt(sim.Field88UInt32(i))
                    If Not IsNothing(skeleton?.Bones) AndAlso binding.BoneIndex >= 0 AndAlso binding.BoneIndex < skeleton.Bones.Count Then
                        binding.BoneName = skeleton.Bones(binding.BoneIndex).Name
                    End If
                End If

                If i < sim.Field118Pairs.Count Then
                    binding.TransformSetIndex = sim.Field118Pairs(i).FirstValue
                    binding.ParameterRaw = sim.Field118Pairs(i).SecondValue
                    binding.ParameterSingle = BitConverter.ToSingle(BitConverter.GetBytes(binding.ParameterRaw), 0)
                End If

                PopulateCollidableBindingDiagnostics(binding)
                sim.CollidableBindings.Add(binding)
            Next

        Next
    End Sub

    Private Shared Sub PopulateResolvedVolumeConstraintLinks(config As HclClothConfigGraph_Class)
        If IsNothing(config) Then Return

        For Each sim In config.SimClothDatas
            If IsNothing(sim) Then Continue For

            sim.CollidableBindingUniformParameter = ResolveUniformSingle(sim.CollidableBindings.Where(Function(binding) binding IsNot Nothing).Select(Function(binding) binding.ParameterSingle))
            sim.CollidableBindingParametersUniform = sim.CollidableBindingUniformParameter.HasValue
            sim.CollidableBindingsAllMatrixIdentity = sim.CollidableBindings.All(Function(binding) binding Is Nothing OrElse binding.MatrixIsIdentity)

            Dim volumeConstraints = sim.ConstraintDetails.OfType(Of HclVolumeConstraintMxDetail_Class).ToList()
            sim.VolumeConstraintCount = volumeConstraints.Count
            sim.VolumeConstraintField30MatchesBindingParameter = False
            sim.VolumeConstraintField50MatchesBindingParameter = False

            If volumeConstraints.Count = 0 OrElse Not sim.CollidableBindingUniformParameter.HasValue Then Continue For

            Dim bindingParam = sim.CollidableBindingUniformParameter.Value
            sim.VolumeConstraintField30MatchesBindingParameter = volumeConstraints.All(Function(detail) detail IsNot Nothing AndAlso detail.Field30UniformParameter.HasValue AndAlso Math.Abs(CDbl(detail.Field30UniformParameter.Value - bindingParam)) <= 0.0001R)
            sim.VolumeConstraintField50MatchesBindingParameter = volumeConstraints.All(Function(detail) detail IsNot Nothing AndAlso detail.Field50UniformParameter.HasValue AndAlso Math.Abs(CDbl(detail.Field50UniformParameter.Value - bindingParam)) <= 0.0001R)
        Next
    End Sub

    Private Shared Function ResolveUniformSingle(values As IEnumerable(Of Single)) As Single?
        If IsNothing(values) Then Return Nothing

        Dim items = values.ToList()
        If items.Count = 0 Then Return Nothing

        Dim firstValue = items(0)
        For Each item In items
            If Math.Abs(CDbl(item - firstValue)) > 0.0001R Then Return Nothing
        Next

        Return firstValue
    End Function
    Private Shared Sub PopulateCollidableBindingDiagnostics(binding As HclSimCollidableBinding_Class)
        If IsNothing(binding) Then Return

        Dim bindMatrix = ToMatrix4x4(binding.Matrix)
        Dim collMatrix = ToMatrix4x4(If(binding.Collidable IsNot Nothing, binding.Collidable.TransformMatrix, Nothing))
        binding.MatrixIdentityDelta = MaxMatrixDelta(bindMatrix, Matrix4x4.Identity)
        binding.CollidableTransformIdentityDelta = MaxMatrixDelta(collMatrix, Matrix4x4.Identity)
        binding.BindTimesCollidableIdentityDelta = MaxMatrixDelta(Matrix4x4.Multiply(bindMatrix, collMatrix), Matrix4x4.Identity)
        binding.CollidableTimesBindIdentityDelta = MaxMatrixDelta(Matrix4x4.Multiply(collMatrix, bindMatrix), Matrix4x4.Identity)

        Dim invColl As Matrix4x4 = Matrix4x4.Identity
        binding.MatrixIsIdentity = (binding.MatrixIdentityDelta <= 0.001R)
        binding.CollidableTransformIsIdentity = (binding.CollidableTransformIdentityDelta <= 0.001R)
        binding.BindingMatchesInverseCollidable = False
        If Matrix4x4.Invert(collMatrix, invColl) Then
            binding.BindingInverseCollidableDelta = MaxMatrixDelta(bindMatrix, invColl)
            binding.BindingMatchesInverseCollidable = (binding.BindingInverseCollidableDelta <= 0.001R)
        Else
            binding.BindingInverseCollidableDelta = Double.PositiveInfinity
        End If
    End Sub

    Private Shared Function ToMatrix4x4(source As HkxMatrix4Graph_Class) As Matrix4x4
        If source Is Nothing OrElse source.Values Is Nothing OrElse source.Values.Length < 16 Then Return Matrix4x4.Identity
        Return New Matrix4x4(source.Values(0), source.Values(1), source.Values(2), source.Values(3),
                             source.Values(4), source.Values(5), source.Values(6), source.Values(7),
                             source.Values(8), source.Values(9), source.Values(10), source.Values(11),
                             source.Values(12), source.Values(13), source.Values(14), source.Values(15))
    End Function

    Private Shared Function MaxMatrixDelta(left As Matrix4x4, right As Matrix4x4) As Double
        Dim maxDelta = 0.0R
        maxDelta = Math.Max(maxDelta, Math.Abs(left.M11 - right.M11))
        maxDelta = Math.Max(maxDelta, Math.Abs(left.M12 - right.M12))
        maxDelta = Math.Max(maxDelta, Math.Abs(left.M13 - right.M13))
        maxDelta = Math.Max(maxDelta, Math.Abs(left.M14 - right.M14))
        maxDelta = Math.Max(maxDelta, Math.Abs(left.M21 - right.M21))
        maxDelta = Math.Max(maxDelta, Math.Abs(left.M22 - right.M22))
        maxDelta = Math.Max(maxDelta, Math.Abs(left.M23 - right.M23))
        maxDelta = Math.Max(maxDelta, Math.Abs(left.M24 - right.M24))
        maxDelta = Math.Max(maxDelta, Math.Abs(left.M31 - right.M31))
        maxDelta = Math.Max(maxDelta, Math.Abs(left.M32 - right.M32))
        maxDelta = Math.Max(maxDelta, Math.Abs(left.M33 - right.M33))
        maxDelta = Math.Max(maxDelta, Math.Abs(left.M34 - right.M34))
        maxDelta = Math.Max(maxDelta, Math.Abs(left.M41 - right.M41))
        maxDelta = Math.Max(maxDelta, Math.Abs(left.M42 - right.M42))
        maxDelta = Math.Max(maxDelta, Math.Abs(left.M43 - right.M43))
        maxDelta = Math.Max(maxDelta, Math.Abs(left.M44 - right.M44))
        Return maxDelta
    End Function

    Private Shared Sub PopulateResolvedSimulateConfigs(config As HclClothConfigGraph_Class)
        If IsNothing(config?.Simulate) Then Return

        Dim sim = config.SimClothDatas.FirstOrDefault()
        Dim constraintDetails = sim?.ConstraintDetails
        For Each cfg In config.Simulate.Configs
            If cfg Is Nothing Then Continue For
            cfg.ResolvedConstraint = Nothing
            cfg.ResolvedConstraintName = Nothing
            cfg.ResolvedConstraintType = Nothing
            cfg.ConstraintIndex = -1
            cfg.IsTerminator = (cfg.Value = UInteger.MaxValue)
            If cfg.IsTerminator OrElse constraintDetails Is Nothing Then Continue For

            Dim index = CInt(cfg.Value)
            cfg.ConstraintIndex = index
            If index < 0 OrElse index >= constraintDetails.Count Then Continue For

            Dim constraint = constraintDetails(index)
            cfg.ResolvedConstraint = constraint
            cfg.ResolvedConstraintType = constraint.GetType().Name
            cfg.ResolvedConstraintName = ExtractConstraintName(constraint)
        Next
    End Sub

    Private Shared Function ExtractConstraintName(constraint As Object) As String
        If constraint Is Nothing Then Return String.Empty
        If TypeOf constraint Is HclStandardLinkConstraintSetDetail_Class Then Return DirectCast(constraint, HclStandardLinkConstraintSetDetail_Class).Name
        If TypeOf constraint Is HclStretchLinkConstraintSetDetail_Class Then Return DirectCast(constraint, HclStretchLinkConstraintSetDetail_Class).Name
        If TypeOf constraint Is HclBendStiffnessConstraintSetDetail_Class Then Return DirectCast(constraint, HclBendStiffnessConstraintSetDetail_Class).Name
        If TypeOf constraint Is HclLocalRangeConstraintSetDetail_Class Then Return DirectCast(constraint, HclLocalRangeConstraintSetDetail_Class).Name
        If TypeOf constraint Is HclVolumeConstraintMxDetail_Class Then Return DirectCast(constraint, HclVolumeConstraintMxDetail_Class).Name
        Return String.Empty
    End Function

    Private Shared Sub PopulateResolvedBendTopology(config As HclClothConfigGraph_Class)
        Dim sim = config?.SimClothDatas?.FirstOrDefault()
        If IsNothing(sim) OrElse sim.Triangles.Count = 0 Then Return

        Dim edgeToTriangles As New Dictionary(Of String, List(Of Integer))(StringComparer.Ordinal)
        For Each triangle In sim.Triangles
            MapBendEdge(edgeToTriangles, CInt(triangle.Value0), CInt(triangle.Value1), triangle.TriangleIndex)
            MapBendEdge(edgeToTriangles, CInt(triangle.Value1), CInt(triangle.Value2), triangle.TriangleIndex)
            MapBendEdge(edgeToTriangles, CInt(triangle.Value2), CInt(triangle.Value0), triangle.TriangleIndex)
        Next

        For Each setDetail In sim.ConstraintDetails.OfType(Of HclBendStiffnessConstraintSetDetail_Class)
            For Each link In setDetail.LinkDetails
                If link Is Nothing Then Continue For
                link.WeightSum = link.WeightA + link.WeightB + link.WeightC + link.WeightD
                link.HasZeroWeightSum = (Math.Abs(CDbl(link.WeightSum)) <= 0.0001R)
                link.PositiveWeightPairSum = link.WeightA + link.WeightB
                link.NegativeWeightPairSum = link.WeightC + link.WeightD
                link.FirstPairFormsUnit = (Math.Abs(CDbl(link.PositiveWeightPairSum - 1.0F)) <= 0.0001R)
                link.SecondPairFormsNegativeUnit = (Math.Abs(CDbl(link.NegativeWeightPairSum + 1.0F)) <= 0.0001R)
                link.WeightPairsFormSignedUnit = (link.FirstPairFormsUnit AndAlso link.SecondPairFormsNegativeUnit)
                PopulateResolvedBendTopology(link, sim, edgeToTriangles)
                PopulateResolvedBendRestGeometry(link, sim)
            Next

            setDetail.ResolvedTopologyCount = setDetail.LinkDetails.Where(Function(link) link IsNot Nothing AndAlso link.HasResolvedTopology).Count()
            setDetail.ResolvedRestGeometryCount = setDetail.LinkDetails.Where(Function(link) link IsNot Nothing AndAlso link.HasResolvedRestGeometry).Count()
            setDetail.SignedUnitCount = setDetail.LinkDetails.Where(Function(link) link IsNot Nothing AndAlso link.WeightPairsFormSignedUnit).Count()
            setDetail.OppOppEdgeEdgeOrderCount = setDetail.LinkDetails.Where(Function(link) link IsNot Nothing AndAlso link.ParticleOrderMatchesOppOppEdgeEdge).Count()
            Dim bendEdgeSamples = setDetail.LinkDetails.Where(Function(link) link IsNot Nothing AndAlso link.HasResolvedRestGeometry).Select(Function(link) link.RestEdgeLength).ToList()
            setDetail.AverageRestEdgeLength = If(bendEdgeSamples.Count > 0, CSng(bendEdgeSamples.Average()), CType(Nothing, Single?))
            Dim bendDeltaSamples = setDetail.LinkDetails.Where(Function(link) link IsNot Nothing AndAlso link.HasResolvedRestGeometry).Select(Function(link) CSng(Math.Abs(CDbl(link.RestCurvatureMinusDihedralOverEdge)))).ToList()
            setDetail.AverageAbsRestCurvatureMinusDihedralOverEdge = If(bendDeltaSamples.Count > 0, CSng(bendDeltaSamples.Average()), CType(Nothing, Single?))
        Next
    End Sub

    Private Shared Sub PopulateResolvedBendTopology(link As HclBendConstraintGraph_Class,
                                                    sim As HclSimClothDataDetail_Class,
                                                    edgeToTriangles As Dictionary(Of String, List(Of Integer)))
        link.HasResolvedTopology = False
        link.SharedEdgeParticleA = -1
        link.SharedEdgeParticleB = -1
        link.OppositeParticleA = -1
        link.OppositeParticleB = -1
        link.TriangleIndexA = -1
        link.TriangleIndexB = -1

        Dim particles = New Integer() {CInt(link.ParticleA), CInt(link.ParticleB), CInt(link.ParticleC), CInt(link.ParticleD)}
        Dim distinct = particles.Distinct().OrderBy(Function(value) value).ToArray()
        If distinct.Length <> 4 Then Return

        For i = 0 To distinct.Length - 2
            For j = i + 1 To distinct.Length - 1
                Dim edgeA = distinct(i)
                Dim edgeB = distinct(j)
                Dim edgeKey = MakeBendEdgeKey(edgeA, edgeB)
                If Not edgeToTriangles.ContainsKey(edgeKey) Then Continue For

                Dim tris = edgeToTriangles(edgeKey)
                If tris.Count <> 2 Then Continue For

                Dim tri0 = sim.Triangles(tris(0))
                Dim tri1 = sim.Triangles(tris(1))
                Dim quadSet As New HashSet(Of Integer) From {
                    CInt(tri0.Value0), CInt(tri0.Value1), CInt(tri0.Value2),
                    CInt(tri1.Value0), CInt(tri1.Value1), CInt(tri1.Value2)
                }
                If quadSet.Count <> 4 Then Continue For
                If Not distinct.All(Function(value) quadSet.Contains(value)) Then Continue For

                Dim tri0Verts = New Integer() {CInt(tri0.Value0), CInt(tri0.Value1), CInt(tri0.Value2)}
                Dim tri1Verts = New Integer() {CInt(tri1.Value0), CInt(tri1.Value1), CInt(tri1.Value2)}
                link.SharedEdgeParticleA = edgeA
                link.SharedEdgeParticleB = edgeB
                link.OppositeParticleA = tri0Verts.First(Function(value) value <> edgeA AndAlso value <> edgeB)
                link.OppositeParticleB = tri1Verts.First(Function(value) value <> edgeA AndAlso value <> edgeB)
                link.TriangleIndexA = tri0.TriangleIndex
                link.TriangleIndexB = tri1.TriangleIndex
                Dim abSet As New HashSet(Of Integer) From {CInt(link.ParticleA), CInt(link.ParticleB)}
                Dim cdSet As New HashSet(Of Integer) From {CInt(link.ParticleC), CInt(link.ParticleD)}
                Dim oppSet As New HashSet(Of Integer) From {link.OppositeParticleA, link.OppositeParticleB}
                Dim edgeSet As New HashSet(Of Integer) From {link.SharedEdgeParticleA, link.SharedEdgeParticleB}
                link.ParticleOrderMatchesOppOppEdgeEdge = abSet.SetEquals(oppSet) AndAlso cdSet.SetEquals(edgeSet)
                link.HasResolvedTopology = True
                Return
            Next

        Next
    End Sub

    Private Shared Sub PopulateResolvedBendRestGeometry(link As HclBendConstraintGraph_Class,
                                                        sim As HclSimClothDataDetail_Class)
        link.HasResolvedRestGeometry = False
        link.RestEdgeLength = 0.0F
        link.RestDihedralAngle = 0.0F
        link.RestDihedralOverEdge = 0.0F
        link.RestCurvatureMinusDihedral = 0.0F
        link.RestCurvatureMinusDihedralOverEdge = 0.0F

        If IsNothing(link) OrElse IsNothing(sim) OrElse Not link.HasResolvedTopology Then Return
        Dim pose = sim.DefaultClothPoseDetails.FirstOrDefault()
        If IsNothing(pose) OrElse IsNothing(pose.Pose) Then Return

        Dim maxIndex = pose.Pose.Count - 1
        Dim indices = New Integer() {link.OppositeParticleA, link.OppositeParticleB, link.SharedEdgeParticleA, link.SharedEdgeParticleB}
        If indices.Any(Function(value) value < 0 OrElse value > maxIndex) Then Return

        Dim oppA = ToVector3(pose.Pose(link.OppositeParticleA))
        Dim oppB = ToVector3(pose.Pose(link.OppositeParticleB))
        Dim sharedA = ToVector3(pose.Pose(link.SharedEdgeParticleA))
        Dim sharedB = ToVector3(pose.Pose(link.SharedEdgeParticleB))

        Dim edge = sharedB - sharedA
        Dim edgeLength = edge.Length()
        If edgeLength <= 0.000001F Then Return

        Dim dihedral = ComputeSignedDihedral(oppA, oppB, sharedA, sharedB)

        link.HasResolvedRestGeometry = True
        link.RestEdgeLength = edgeLength
        link.RestDihedralAngle = CSng(dihedral)
        link.RestDihedralOverEdge = CSng(dihedral / edgeLength)
        link.RestCurvatureMinusDihedral = link.RestCurvature - link.RestDihedralAngle
        link.RestCurvatureMinusDihedralOverEdge = link.RestCurvature - link.RestDihedralOverEdge
    End Sub

    Private Shared Function ComputeSignedDihedral(oppA As Vector3,
                                                  oppB As Vector3,
                                                  sharedA As Vector3,
                                                  sharedB As Vector3) As Double
        Dim edge = sharedB - sharedA
        If edge.LengthSquared() <= 0.000001F Then Return 0.0R
        edge = Vector3.Normalize(edge)

        Dim tri0Normal = NormalizeOrZero(Vector3.Cross(sharedA - oppA, sharedB - oppA))
        Dim tri1Normal = NormalizeOrZero(Vector3.Cross(sharedB - oppB, sharedA - oppB))
        If tri0Normal.LengthSquared() <= 0.000001F OrElse tri1Normal.LengthSquared() <= 0.000001F Then Return 0.0R

        Dim clampedDot = Math.Max(-1.0R, Math.Min(1.0R, CDbl(Vector3.Dot(tri0Normal, tri1Normal))))
        Dim angle = Math.Acos(clampedDot)
        Dim sign = Math.Sign(CDbl(Vector3.Dot(edge, Vector3.Cross(tri0Normal, tri1Normal))))
        If sign = 0 Then sign = 1
        Return angle * sign
    End Function

    Private Shared Function NormalizeOrZero(value As Vector3) As Vector3
        Dim length = value.Length()
        If length <= 0.000001F Then Return Vector3.Zero
        Return value / length
    End Function

    Private Shared Function ToVector3(value As HkxVector4Graph_Class) As Vector3
        If IsNothing(value) Then Return Vector3.Zero
        Return New Vector3(CSng(value.X), CSng(value.Y), CSng(value.Z))
    End Function
    Private Shared Sub MapBendEdge(edgeToTriangles As Dictionary(Of String, List(Of Integer)), a As Integer, b As Integer, triangleIndex As Integer)
        Dim key = MakeBendEdgeKey(a, b)
        Dim list As List(Of Integer) = Nothing
        If Not edgeToTriangles.TryGetValue(key, list) Then
            list = New List(Of Integer)()
            edgeToTriangles(key) = list
        End If
        list.Add(triangleIndex)
    End Sub

    Private Shared Function MakeBendEdgeKey(a As Integer, b As Integer) As String
        Dim low = Math.Min(a, b)
        Dim high = Math.Max(a, b)
        Return low.ToString() & ":" & high.ToString()
    End Function

    Private Shared Sub PopulateResolvedTriangles(config As HclClothConfigGraph_Class)
        If IsNothing(config?.SimpleMeshBoneDeform) Then Return

        Dim sim = config.SimClothDatas.FirstOrDefault()
        If IsNothing(sim) Then Return

        For Each mapping In config.SimpleMeshBoneDeform.BoneMappings
            If mapping.TriangleIndex < 0 OrElse mapping.TriangleIndex >= sim.Triangles.Count Then Continue For
            mapping.ResolvedTriangle = sim.Triangles(mapping.TriangleIndex)
        Next
    End Sub

    Private Shared Sub PopulateSkinCoverage(config As HclClothConfigGraph_Class)
        Dim skin = config?.ObjectSpaceSkin
        If IsNothing(skin) Then Return

        skin.CoveredVertexIndices.Clear()
        skin.CoveredVertexIndices.AddRange(
            skin.SkinBlocks.
                Where(Function(block) block.InfluenceBlock IsNot Nothing).
                SelectMany(Function(block) block.InfluenceBlock.VertexIndices).
                Where(Function(index) index <> UShort.MaxValue).
                Select(Function(index) CInt(index)).
                Distinct().
                OrderBy(Function(index) index))
        skin.CoveredVertexCount = skin.CoveredVertexIndices.Count

        Dim sim = config.SimClothDatas.FirstOrDefault()
        If IsNothing(sim) Then
            skin.SimParticleCount = Nothing
            skin.CoversSimParticles = Nothing
            Return
        End If

        skin.SimParticleCount = sim.Field38Vectors.Count
        skin.CoversSimParticles = (skin.CoveredVertexCount = skin.SimParticleCount.Value)
    End Sub

    Private Shared Sub PopulateSkinDefaultPoseMatches(config As HclClothConfigGraph_Class)
        Dim skin = config?.ObjectSpaceSkin
        Dim sim = config?.SimClothDatas?.FirstOrDefault()
        Dim pose = sim?.DefaultClothPoseDetails?.FirstOrDefault()
        If IsNothing(skin) OrElse IsNothing(sim) OrElse IsNothing(pose) Then Return

        Const epsilon As Double = (1.0R / 256.0R) + 0.00001R

        For Each block In skin.SkinBlocks
            If IsNothing(block) Then Continue For

            Dim matchCount = 0
            Dim totalCount = 0

            For Each entry In block.VertexEntries
                If IsNothing(entry?.Position) Then Continue For
                If entry.VertexIndex < 0 OrElse entry.VertexIndex >= pose.Pose.Count Then Continue For

                Dim expected = pose.Pose(CInt(entry.VertexIndex))
                entry.ExpectedPositionX = expected.X
                entry.ExpectedPositionY = expected.Y
                entry.ExpectedPositionZ = expected.Z
                entry.ExpectedPositionW = expected.W

                Dim dx = entry.Position.X - expected.X
                Dim dy = entry.Position.Y - expected.Y
                Dim dz = entry.Position.Z - expected.Z
                Dim positionErrorValue = Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz))
                entry.PositionError = positionErrorValue
                entry.MatchesDefaultPosePosition = (positionErrorValue <= epsilon)

                totalCount += 1
                If entry.MatchesDefaultPosePosition.Value Then matchCount += 1
            Next

            block.MatchedDefaultPosePositions = matchCount
            If totalCount = 0 Then
                block.AllPositionsMatchDefaultPose = Nothing
            Else
                block.AllPositionsMatchDefaultPose = (matchCount = totalCount)
            End If
        Next
    End Sub

    Private Shared Sub PopulateStateAccessLinks(config As HclClothConfigGraph_Class, skeleton As HkaSkeletonGraph_Class)
        If IsNothing(config) Then Return

        Dim bufferNames As New List(Of String)
        bufferNames.AddRange(config.BufferDefinitions.Select(Function(detail) detail.Name))
        bufferNames.AddRange(config.ScratchBufferDefinitions.Select(Function(detail) detail.Name))

        For Each state In config.ClothStates
            For Each access In state.BufferAccesses.Concat(state.AuxiliaryBufferAccesses)
                If access Is Nothing Then Continue For
                If access.BufferIndex >= 0 AndAlso access.BufferIndex < bufferNames.Count Then
                    access.ResolvedBufferName = bufferNames(access.BufferIndex)
                End If
            Next

            If skeleton?.Bones Is Nothing Then Continue For

            For Each transformAccess In state.TransformSetAccesses
                If transformAccess Is Nothing Then Continue For
                For Each component In transformAccess.ComponentAccesses
                    If component Is Nothing Then Continue For
                    component.ResolvedBoneNames.Clear()
                    component.MatchingSkinPaletteIndices.Clear()
                    component.MatchingSkinBoneNames.Clear()
                    For Each boneIndex In component.MaskIndices
                        If boneIndex < 0 OrElse boneIndex >= skeleton.Bones.Count Then Continue For
                        component.ResolvedBoneNames.Add(skeleton.Bones(boneIndex).Name)
                        If config.ObjectSpaceSkin Is Nothing Then Continue For
                        Dim skinPaletteIndex = config.ObjectSpaceSkin.BoneIndices.IndexOf(CUShort(boneIndex))
                        If skinPaletteIndex < 0 Then Continue For
                        component.MatchingSkinPaletteIndices.Add(skinPaletteIndex)
                        If skinPaletteIndex < config.ObjectSpaceSkin.ResolvedBoneNames.Count Then
                            component.MatchingSkinBoneNames.Add(config.ObjectSpaceSkin.ResolvedBoneNames(skinPaletteIndex))
                        Else
                            component.MatchingSkinBoneNames.Add(skeleton.Bones(boneIndex).Name)
                        End If
                    Next
                Next
            Next

        Next
    End Sub

    Private Shared Sub PopulateStateOperatorLinks(config As HclClothConfigGraph_Class)
        If IsNothing(config?.ClothData?.Operators) Then Return

        For Each state In config.ClothStates
            If IsNothing(state) Then Continue For

            For Each opIndex In state.OperatorIndices
                If opIndex < 0 OrElse opIndex >= config.ClothData.Operators.Count Then Continue For
                Dim op = config.ClothData.Operators(opIndex)
                state.ResolvedOperators.Add(op)
                state.ResolvedOperatorNames.Add(If(String.IsNullOrWhiteSpace(op.ClassName), "#" & opIndex.ToString(), op.ClassName))
            Next

        Next
    End Sub
End Class

Public Class HclClothPackageGraph_Class
    Public Property Graph As HkxObjectGraph_Class
    Public Property RootContainer As HkxRootLevelContainerGraph_Class
    Public Property Skeleton As HkaSkeletonGraph_Class
    Public ReadOnly Property ClothConfigs As New List(Of HclClothConfigGraph_Class)
    Public ReadOnly Property Collidables As New List(Of HclCollidableDetail_Class)
    Public ReadOnly Property CapsuleShapes As New List(Of HclCapsuleShapeDetail_Class)
    Public ReadOnly Property ConstraintSets As New List(Of Object)
End Class

Public Class HclClothConfigGraph_Class
    Public Property ClothData As HclClothDataGraph_Class
    Public ReadOnly Property SimClothDatas As New List(Of HclSimClothDataDetail_Class)
    Public ReadOnly Property BufferDefinitions As New List(Of HclBufferDefinitionDetail_Class)
    Public ReadOnly Property ScratchBufferDefinitions As New List(Of HclScratchBufferDefinitionDetail_Class)
    Public ReadOnly Property TransformSets As New List(Of HclTransformSetDefinitionGraph_Class)
    Public Property ObjectSpaceSkin As HclObjectSpaceSkinPNOperatorGraph_Class
    Public Property MoveParticles As HclMoveParticlesOperatorDetail_Class
    Public Property Simulate As HclSimulateOperatorDetail_Class
    Public Property SimpleMeshBoneDeform As HclSimpleMeshBoneDeformOperatorGraph_Class
    Public Property CopyVertices As HclCopyVerticesOperatorDetail_Class
    Public Property GatherAllVertices As HclGatherAllVerticesOperatorDetail_Class
    Public ReadOnly Property UnknownOperators As New List(Of HkxVirtualObjectGraph_Class)
    Public ReadOnly Property ClothStates As New List(Of HclClothStateDetail_Class)
End Class










