' Version Uploaded of Fo4Library 3.2.0
Imports NiflySharp
Imports NiflySharp.Blocks
Imports NiflySharp.Structs
Imports OpenTK.Mathematics
Imports FO4_Base_Library.RecalcTBN
Imports SysNumerics = System.Numerics
Imports System.Collections.Concurrent
Imports System.Threading.Tasks

' --- STRUCTURE PARA ALMACENAR GEOMETRÍA SKINEADA ---
'
' Fully polymorphic — no BSTriShape-specific fields.  The packed BSVertexData/BSVertexDataSSE
' buffers that used to be cached here were an optimization for BSTriShape in-place struct copy
' during RemoveZaps; that path was eliminated in favor of adapter.ResizeVertices + individual
' field setters + adapter.SetSkinning, which works uniformly across BSTriShape and NiTriShape
' families.
Public Structure SkinnedGeometry
    Public Vertices() As Vector3d
    Public BaseVertices() As Vector3d
    Public NifLocalVertices() As Vector3d      ' pre-skinning NIF local space — base for morph application
    Public PerVertexSkinMatrix() As Matrix4d   ' per-vertex blended Mtot = GlobalTransform * skin; filled once in ExtractSkinnedGeometry
    Public dirtyMaskIndices As HashSet(Of Integer)              ' Para dirty-tracking de máscara
    Public dirtyVertexIndices As HashSet(Of Integer)
    Public dirtyMaskFlags() As Boolean
    Public dirtyVertexFlags() As Boolean
    Public Normals() As Vector3d
    Public Tangents() As Vector3d
    Public Bitangents() As Vector3d
    Public Uvs_Weight() As Vector3
    Public Eyedata() As Single
    Public ParentGlobalTransform As Matrix4d
    Public BoneMatsBind() As Matrix4d   ' bind-pose matrices
    Public BoneMatsPose() As Matrix4d  ' pose matrices
    Public VertexColors() As Vector4
    Public VertexMask() As Single
    Public Indices() As UInteger
    Public Geometry As IShapeGeometry                 ' polymorphic adapter for the underlying shape (BSTriShape / NiTriShape / BSLODTriShape / BSSegmented / ...)
    Public Skinning As ShapeSkinningData              ' polymorphic per-vertex bone idx[4]+weight[4]; sourced from BSVertexData inline for BSTriShape or NiSkinPartition/NiSkinData for NiTriShape family
    Public TriangleProvenance As TriangleRemap        ' optional per-new-triangle source map; populated by zap/split/merge so InjectToTrishape can redistribute Segments/LOD sizes
    Public Boundingcenter As Vector3d
    Public Minv As Vector3d
    Public Maxv As Vector3d
    Public CachedTBN As TBNCache
    Public Version As NiVersion
    ' GPU Skinning: flat arrays for VBO upload
    Public GPUBoneIndices() As Byte        ' 4 bytes per vertex, flattened: [v0b0,v0b1,v0b2,v0b3, v1b0,...]
    Public GPUBoneWeights() As Single      ' 4 floats per vertex, flattened: [v0w0,v0w1,v0w2,v0w3, v1w0,...]
    Public GPUBoneMatrices() As Matrix4    ' one Matrix4 per bone in the palette for SSBO
    ' Lazy world-space cache (computed on demand, invalidated by pose/morph changes)
    Public CachedWorldVertices() As Vector3d
    Public CachedWorldNormals() As Vector3d
    Public WorldCacheValid As Boolean
End Structure
Public Structure MorphData
    Public index As UInteger
    Public PosDiff As Vector3
End Structure


Public Class SkinningHelper

    ' ┌─────────────────────────────────────────────────────────────────────────┐
    ' │ CPU SKINNING — SYNC CONTRACT                                          │
    ' │                                                                       │
    ' │ This function is the CPU-side bone blend (double precision).           │
    ' │ The GPU equivalent is the vertex shader skinning block in             │
    ' │ Shader_Class.vb (both FO4 and SSE variants).                          │
    ' │                                                                       │
    ' │ Formula: skinMatrix = Σ(bones[idx[j]] * weight[j]) / sumW             │
    ' │ GPU version: same sum but weights are pre-normalized (sumW=1).        │
    ' │ Fallback (sumW=0): bones[idx[0]] — same in both.                      │
    ' │                                                                       │
    ' │ If you change the blend logic, fallback, or weight handling here,     │
    ' │ you MUST update the vertex shader skinning block to match.            │
    ' │ See also: RecomputeGPUBoneMatrices, ExtractSkinnedGeometry.           │
    ' └─────────────────────────────────────────────────────────────────────────┘
    Private Shared Function BlendBoneMatrices(boneWeights As System.Half(), boneIndices As Byte(), precomputed() As Matrix4d) As Matrix4d
        If boneWeights Is Nothing OrElse boneIndices Is Nothing OrElse precomputed.Length = 0 Then Return If(precomputed.Length > 0, precomputed(0), Matrix4d.Identity)
        Dim result As Matrix4d = Matrix4d.Zero
        Dim sumW As Double = 0
        Dim cnt = Math.Min(boneWeights.Length, boneIndices.Length) - 1
        ' Single pass: accumulate weighted matrices and sum of weights simultaneously
        For j = 0 To cnt
            Dim w = CType(boneWeights(j), Double)
            sumW += w
            Dim idx = boneIndices(j)
            If idx >= 0 AndAlso idx < precomputed.Length Then result += precomputed(idx) * w
        Next
        If sumW = 0 Then
            Dim idx0 = If(boneIndices.Length > 0, boneIndices(0), 0)
            Return precomputed(Math.Max(0, Math.Min(idx0, precomputed.Length - 1)))
        End If
        Return result * (1.0 / sumW)
    End Function

    ''' <summary>
    ''' Flat-array overload of BlendBoneMatrices that reads <paramref name="wpv"/> bone slots
    ''' starting at <paramref name="baseIdx"/> in the flat <paramref name="boneWeights"/> /
    ''' <paramref name="boneIndices"/> arrays.  Same semantics and fallback as the per-vertex
    ''' overload but avoids per-vertex slice allocation, which matters in the inner skinning
    ''' loop (called once per vertex per Extract/Bake call).
    ''' </summary>
    Private Shared Function BlendBoneMatrices(boneWeights As System.Half(), boneIndices As Byte(), baseIdx As Integer, wpv As Integer, precomputed() As Matrix4d) As Matrix4d
        If boneWeights Is Nothing OrElse boneIndices Is Nothing OrElse precomputed.Length = 0 OrElse wpv <= 0 Then
            Return If(precomputed.Length > 0, precomputed(0), Matrix4d.Identity)
        End If
        Dim result As Matrix4d = Matrix4d.Zero
        Dim sumW As Double = 0
        Dim available As Integer = Math.Min(wpv, Math.Min(boneWeights.Length - baseIdx, boneIndices.Length - baseIdx))
        For j = 0 To available - 1
            Dim w = CType(boneWeights(baseIdx + j), Double)
            sumW += w
            Dim idx = boneIndices(baseIdx + j)
            If idx >= 0 AndAlso idx < precomputed.Length Then result += precomputed(idx) * w
        Next
        If sumW = 0 Then
            Dim idx0 As Byte = If(available > 0, boneIndices(baseIdx), CByte(0))
            Return precomputed(Math.Max(0, Math.Min(CInt(idx0), precomputed.Length - 1)))
        End If
        Return result * (1.0 / sumW)
    End Function

    ''' <summary>
    ''' Extrae vértices, normales, tangentes y bitangentes del shape,
    ''' aplicando el mismo skinning que LoadShapeSafe.
    ''' </summary>
    ''' <param name="skeleton">SkeletonInstance to read bind/pose transforms from. If Nothing,
    ''' falls back to <see cref="SkeletonInstance.Default"/>. Pose application is implicit:
    ''' bones whose <see cref="HierarchiBone_class.DeltaTransform"/> is set get pose-folded;
    ''' bones with DeltaTransform=Nothing collapse to bind. Callers that want a "render bind
    ''' regardless of stored pose" call <see cref="SkeletonInstance.Reset"/> on the instance
    ''' first (they are responsible for the side effect on shared instances).</param>
    Public Shared Function ExtractSkinnedGeometry(shape As IRenderableShape, singleboneskinning As Boolean, RecalculateNormals As Boolean, Optional skeleton As SkeletonInstance = Nothing) As SkinnedGeometry
        Dim effectiveSkel As SkeletonInstance = If(skeleton, SkeletonInstance.Default)
        Dim shapeGeom = shape.Geometry
        If shapeGeom Is Nothing Then Throw New InvalidOperationException("IRenderableShape.Geometry is null")
        Dim backing = shapeGeom.BackingShape
        Dim bones = shape.ShapeBones.ToArray()
        Dim boneTrans = shape.ShapeBoneTransforms.ToArray()

        If boneTrans.Length <> bones.Length Then Throw New Exception("BonesTransform y Bones desincronizados")
        Dim Nifversion = shape.NifContent.Header.Version
        ' 1) Transformación global del shape
        Dim shapeNode = TryCast(shape.NifContent.GetParentNode(backing), NiNode)
        If IsNothing(shapeNode) Then
            Debugger.Break()
            shapeNode = shape.NifContent.GetRootNode()
        End If

        Dim GlobalTransform = If(shapeNode IsNot Nothing, Transform_Class.GetGlobalTransform(shapeNode, shape.NifContent).ToMatrix4d(), Matrix4d.Identity)

        ' 2) Datos brutos — la INVERTIDAS swap y el byte-decode de TBN viven en el adapter:
        '    GetTangents/GetBitangents devuelven ya en convención del renderer.
        Dim srcVertexPositions = shapeGeom.GetVertexPositions()
        Dim rawVerts(srcVertexPositions.Count - 1) As Vector3d
        For i = 0 To srcVertexPositions.Count - 1
            Dim v = srcVertexPositions(i)
            rawVerts(i) = New Vector3d(v.X, v.Y, v.Z)
        Next
        Dim rawNormals() As Vector3d
        Dim rawTangents() As Vector3d
        Dim rawBitangs() As Vector3d

        If shapeGeom.HasNormals Then
            Dim srcNormals = shapeGeom.GetNormals()
            rawNormals = New Vector3d(rawVerts.Length - 1) {}
            Parallel.For(0, rawVerts.Length, Sub(i)
                                                 Dim v As New Vector3d(srcNormals(i).X, srcNormals(i).Y, srcNormals(i).Z)
                                                 Dim l = v.Length
                                                 rawNormals(i) = If(l > 0.000001, v / l, Vector3d.Zero)
                                             End Sub)
        Else
            rawNormals = New Vector3d(rawVerts.Length - 1) {}
        End If

        If shapeGeom.HasTangents Then
            Dim srcTan = shapeGeom.GetTangents()
            Dim srcBit = shapeGeom.GetBitangents()
            rawTangents = New Vector3d(rawVerts.Length - 1) {}
            rawBitangs = New Vector3d(rawVerts.Length - 1) {}
            Parallel.For(0, rawVerts.Length, Sub(i)
                                                 Dim t = srcTan(i)
                                                 Dim b = srcBit(i)
                                                 Dim tv As New Vector3d(t.X, t.Y, t.Z)
                                                 Dim bv As New Vector3d(b.X, b.Y, b.Z)
                                                 Dim tl = tv.Length
                                                 Dim bl = bv.Length
                                                 rawTangents(i) = If(tl > 0.000001, tv / tl, Vector3d.Zero)
                                                 rawBitangs(i) = If(bl > 0.000001, bv / bl, Vector3d.Zero)
                                             End Sub)
        Else
            rawTangents = Enumerable.Repeat(New Vector3d(0.0F, 0.0F, 0.0F), rawVerts.Length).ToArray()
            rawBitangs = Enumerable.Repeat(New Vector3d(0.0F, 0.0F, 0.0F), rawVerts.Length).ToArray()
        End If

        ' Polymorphic per-vertex skinning data (BSTriShape inline, NiTriShape NiSkinPartition).
        Dim shapeSkin As ShapeSkinningData = shapeGeom.GetSkinning()

        Dim vertexCount As Integer = rawVerts.Length
        Dim vertexColorsList = If(shapeGeom.HasVertexColors, shapeGeom.GetVertexColors(), Nothing)
        Dim uvsList = If(shapeGeom.HasUVs, shapeGeom.GetUVs(), Nothing)
        If Not ((rawNormals.Length = vertexCount OrElse Not shapeGeom.HasNormals) AndAlso
                (rawTangents.Length = vertexCount OrElse Not shapeGeom.HasNormals) AndAlso
                (rawBitangs.Length = vertexCount OrElse Not shapeGeom.HasNormals) AndAlso
                (Not shapeGeom.HasVertexColors OrElse vertexColorsList.Count = vertexCount) AndAlso
                (Not shapeGeom.HasUVs OrElse uvsList.Count = vertexCount)) Then
            Debugger.Break()
            Throw New Exception("¡Los atributos de los vértices no tienen la misma longitud!")
        End If


        ' 3) Calcular matrices bind-pose y pose actual
        Dim matsBind(bones.Length - 1) As Matrix4d
        Dim matsPose(bones.Length - 1) As Matrix4d
        For k = 0 To bones.Length - 1
            Dim localT = boneTrans(k)
            Dim boneName = bones(k).Name.String
            Dim bindT As Transform_Class
            Dim poseT As Transform_Class
            Dim SkeletonBone As HierarchiBone_class = Nothing

            If effectiveSkel.SkeletonDictionary.TryGetValue(boneName, SkeletonBone) Then
                bindT = SkeletonBone.OriginalGetGlobalTransform
            Else
                bindT = Transform_Class.GetGlobalTransform(bones(k), shape.NifContent)
            End If

            matsBind(k) = bindT.ComposeTransforms(localT).ToMatrix4d()

            If Not singleboneskinning AndAlso Not IsNothing(SkeletonBone) Then
                poseT = SkeletonBone.GetGlobalTransform()
                matsPose(k) = poseT.ComposeTransforms(localT).ToMatrix4d()
            Else
                poseT = bindT
                matsPose(k) = matsBind(k)
            End If

        Next

        ' 4) Aplicar skinning CPU
        ' Save NIF-local vertices BEFORE skinning (needed for correct morph-space application)
        Dim nifLocalVerts = rawVerts.ToArray()
        Dim perVertexMtot(vertexCount - 1) As Matrix4d

        ' O2.4: Parallel options — use regular For for small meshes, bound parallelism for large ones
        Dim useParallel As Boolean = vertexCount >= 500
        Dim parallelOpts As New ParallelOptions With {.MaxDegreeOfParallelism = Environment.ProcessorCount}

        ' GPU Skinning: allocate flat arrays for per-vertex bone data
        Dim gpuBoneIdx(vertexCount * 4 - 1) As Byte
        Dim gpuBoneWgt(vertexCount * 4 - 1) As Single
        Dim gpuBoneMats() As Matrix4 = Nothing

        Select Case True
            Case Not singleboneskinning AndAlso bones.Length > 0
                ' Pre-compute bone matrices (shapeGlobalTransform * matsPose(k))
                Dim precomputedBoneMatrices(bones.Length - 1) As Matrix4d
                For k = 0 To bones.Length - 1
                    precomputedBoneMatrices(k) = GlobalTransform * matsPose(k)
                Next

                ' GPU Skinning: compute float-precision bone matrices for SSBO upload
                gpuBoneMats = New Matrix4(bones.Length - 1) {}
                For k = 0 To bones.Length - 1
                    Dim m = precomputedBoneMatrices(k)
                    gpuBoneMats(k) = New Matrix4(
                        CSng(m.M11), CSng(m.M12), CSng(m.M13), CSng(m.M14),
                        CSng(m.M21), CSng(m.M22), CSng(m.M23), CSng(m.M24),
                        CSng(m.M31), CSng(m.M32), CSng(m.M33), CSng(m.M34),
                        CSng(m.M41), CSng(m.M42), CSng(m.M43), CSng(m.M44))
                Next

                ' Multibone skinning inner loop — GPU path: store perVertexMtot + extract bone data, do NOT transform rawVerts/N/T/B.
                ' Per-vertex bone influences come from the polymorphic ShapeSkinningData (BSTriShape inline or NiSkinPartition).
                Dim skinFlatIdx = shapeSkin.BoneIndices
                Dim skinFlatWgt = shapeSkin.BoneWeights
                Dim skinWpv = If(shapeSkin.WeightsPerVertex > 0, shapeSkin.WeightsPerVertex, 4)
                Dim hasSkin = (skinFlatIdx IsNot Nothing AndAlso skinFlatWgt IsNot Nothing AndAlso shapeSkin.VertexCount = vertexCount)

                Dim skinningBody As Action(Of Integer) = Sub(i)
                                                             Dim Mtot As Matrix4d
                                                             Dim baseIdx = i * 4

                                                             If hasSkin Then
                                                                 Dim baseSkin = i * skinWpv
                                                                 Mtot = BlendBoneMatrices(skinFlatWgt, skinFlatIdx, baseSkin, skinWpv, precomputedBoneMatrices)

                                                                 ' GPU arrays: copy up to 4 slots, normalize weights to sum=1.
                                                                 Dim copySlots = Math.Min(4, skinWpv)
                                                                 Dim localSumW As Double = 0
                                                                 For j = 0 To copySlots - 1
                                                                     localSumW += CType(skinFlatWgt(baseSkin + j), Double)
                                                                 Next
                                                                 For j = 0 To 3
                                                                     If j < copySlots Then
                                                                         gpuBoneIdx(baseIdx + j) = skinFlatIdx(baseSkin + j)
                                                                         gpuBoneWgt(baseIdx + j) = CSng(If(localSumW > 0, CType(skinFlatWgt(baseSkin + j), Double) / localSumW, 0))
                                                                     Else
                                                                         gpuBoneIdx(baseIdx + j) = 0
                                                                         gpuBoneWgt(baseIdx + j) = 0.0F
                                                                     End If
                                                                 Next
                                                             Else
                                                                 ' No per-vertex skin data — bind to bone 0 with full weight (same fallback as before).
                                                                 Mtot = If(precomputedBoneMatrices.Length > 0, precomputedBoneMatrices(0), Matrix4d.Identity)
                                                                 gpuBoneIdx(baseIdx) = 0 : gpuBoneWgt(baseIdx) = 1.0F
                                                                 gpuBoneIdx(baseIdx + 1) = 0 : gpuBoneWgt(baseIdx + 1) = 0.0F
                                                                 gpuBoneIdx(baseIdx + 2) = 0 : gpuBoneWgt(baseIdx + 2) = 0.0F
                                                                 gpuBoneIdx(baseIdx + 3) = 0 : gpuBoneWgt(baseIdx + 3) = 0.0F
                                                             End If

                                                             ' Store double-precision Mtot for world-space cache / bake
                                                             perVertexMtot(i) = Mtot
                                                         End Sub

                If useParallel Then
                    Parallel.For(0, vertexCount, parallelOpts, skinningBody)
                Else
                    For i As Integer = 0 To vertexCount - 1
                        skinningBody(i)
                    Next
                End If

            Case singleboneskinning AndAlso bones.Length > 0
                ' Single-bone: pre-compute once — GPU path: do NOT transform rawVerts/N/T/B
                Dim Mtot = GlobalTransform * matsPose(0)
                Array.Fill(perVertexMtot, Mtot)

                ' GPU Skinning: single bone matrix for SSBO
                gpuBoneMats = New Matrix4(0) {}
                gpuBoneMats(0) = New Matrix4(
                    CSng(Mtot.M11), CSng(Mtot.M12), CSng(Mtot.M13), CSng(Mtot.M14),
                    CSng(Mtot.M21), CSng(Mtot.M22), CSng(Mtot.M23), CSng(Mtot.M24),
                    CSng(Mtot.M31), CSng(Mtot.M32), CSng(Mtot.M33), CSng(Mtot.M34),
                    CSng(Mtot.M41), CSng(Mtot.M42), CSng(Mtot.M43), CSng(Mtot.M44))

                ' All vertices reference bone 0 with weight 1.0
                For i As Integer = 0 To vertexCount - 1
                    Dim baseIdx = i * 4
                    gpuBoneIdx(baseIdx) = 0 : gpuBoneWgt(baseIdx) = 1.0F
                    gpuBoneIdx(baseIdx + 1) = 0 : gpuBoneWgt(baseIdx + 1) = 0.0F
                    gpuBoneIdx(baseIdx + 2) = 0 : gpuBoneWgt(baseIdx + 2) = 0.0F
                    gpuBoneIdx(baseIdx + 3) = 0 : gpuBoneWgt(baseIdx + 3) = 0.0F
                Next

            Case Else
                ' Sin huesos — usar shape transform + padres, como Outfit Studio
                Dim Mtot = Transform_Class.GetGlobalTransform(backing, shape.NifContent).ToMatrix4d()

                Array.Fill(perVertexMtot, Mtot)

                ' GPU Skinning: single bone matrix (GlobalTransform) for SSBO
                gpuBoneMats = New Matrix4(0) {}
                gpuBoneMats(0) = New Matrix4(
                    CSng(Mtot.M11), CSng(Mtot.M12), CSng(Mtot.M13), CSng(Mtot.M14),
                    CSng(Mtot.M21), CSng(Mtot.M22), CSng(Mtot.M23), CSng(Mtot.M24),
                    CSng(Mtot.M31), CSng(Mtot.M32), CSng(Mtot.M33), CSng(Mtot.M34),
                    CSng(Mtot.M41), CSng(Mtot.M42), CSng(Mtot.M43), CSng(Mtot.M44))

                ' All vertices reference bone 0 with weight 1.0
                For i As Integer = 0 To vertexCount - 1
                    Dim baseIdx = i * 4
                    gpuBoneIdx(baseIdx) = 0 : gpuBoneWgt(baseIdx) = 1.0F
                    gpuBoneIdx(baseIdx + 1) = 0 : gpuBoneWgt(baseIdx + 1) = 0.0F
                    gpuBoneIdx(baseIdx + 2) = 0 : gpuBoneWgt(baseIdx + 2) = 0.0F
                    gpuBoneIdx(baseIdx + 3) = 0 : gpuBoneWgt(baseIdx + 3) = 0.0F
                Next
        End Select
        ' 7) Bounding center — rawVerts is now local-space, compute world-space bounds via PerVertexSkinMatrix
        Dim minV As New Vector3d(Double.MaxValue)
        Dim maxV As New Vector3d(Double.MinValue)
        For i As Integer = 0 To rawVerts.Length - 1
            Dim wv = Vector3d.TransformPosition(rawVerts(i), perVertexMtot(i))
            If wv.X < minV.X Then minV.X = wv.X
            If wv.Y < minV.Y Then minV.Y = wv.Y
            If wv.Z < minV.Z Then minV.Z = wv.Z

            If wv.X > maxV.X Then maxV.X = wv.X
            If wv.Y > maxV.Y Then maxV.Y = wv.Y
            If wv.Z > maxV.Z Then maxV.Z = wv.Z
        Next
        Dim center = (minV + maxV) * 0.5

        ' Pre-compute indices (avoid SelectMany creating thousands of temp arrays)
        Dim trianglesList = shapeGeom.GetTriangles()
        Dim flatIndices As UInteger()
        If trianglesList IsNot Nothing AndAlso trianglesList.Count > 0 Then
            flatIndices = New UInteger(trianglesList.Count * 3 - 1) {}
            For ti = 0 To trianglesList.Count - 1
                flatIndices(ti * 3) = trianglesList(ti).V1
                flatIndices(ti * 3 + 1) = trianglesList(ti).V2
                flatIndices(ti * 3 + 2) = trianglesList(ti).V3
            Next
        Else
            flatIndices = Array.Empty(Of UInteger)()
        End If

        ' Pre-compute vertex colors
        Dim vtxColors As Vector4()
        If shapeGeom.HasVertexColors Then
            vtxColors = New Vector4(vertexCount - 1) {}
            Parallel.For(0, vertexCount, Sub(i)
                                             vtxColors(i) = New Vector4(vertexColorsList(i).R, vertexColorsList(i).G, vertexColorsList(i).B, vertexColorsList(i).A)
                                         End Sub)
        Else
            vtxColors = New Vector4(vertexCount - 1) {}
            Array.Fill(vtxColors, New Vector4(1.0F, 1.0F, 1.0F, 1.0F))
        End If

        Dim vtxMask = New Single(vertexCount - 1) {}
        Dim dirtyVFlags = New Boolean(vertexCount - 1) {}
        Dim dirtyMFlags = New Boolean(vertexCount - 1) {}
        Array.Fill(dirtyVFlags, True)
        Array.Fill(dirtyMFlags, True)

        Dim geo = New SkinnedGeometry With {
            .Vertices = rawVerts,
            .BaseVertices = rawVerts.ToArray,
            .NifLocalVertices = nifLocalVerts,
            .PerVertexSkinMatrix = perVertexMtot,
            .Normals = rawNormals,
            .Tangents = rawTangents,
            .Bitangents = rawBitangs,
            .ParentGlobalTransform = GlobalTransform,
            .BoneMatsBind = matsBind,
            .BoneMatsPose = matsPose,
            .Indices = flatIndices,
            .VertexColors = vtxColors,
            .Eyedata = If(shapeGeom.HasEyeData, shapeGeom.GetEyeData().ToArray(), New Single(vertexCount - 1) {}),
            .Geometry = shapeGeom,
            .Skinning = shapeSkin,
            .VertexMask = vtxMask,
            .dirtyVertexIndices = New HashSet(Of Integer)(Enumerable.Range(0, vertexCount)),
            .dirtyMaskIndices = New HashSet(Of Integer)(Enumerable.Range(0, vertexCount)),
            .dirtyMaskFlags = dirtyMFlags,
            .dirtyVertexFlags = dirtyVFlags,
             .Boundingcenter = center,
             .Minv = minV,
             .Maxv = maxV,
             .CachedTBN = Nothing,
             .Version = Nifversion,
             .GPUBoneIndices = gpuBoneIdx,
             .GPUBoneWeights = gpuBoneWgt,
             .GPUBoneMatrices = gpuBoneMats,
             .WorldCacheValid = False
        }

        ' Uvs_Weight packs per-vertex UV (X,Y) and the first bone weight (Z) — used by the
        ' shader weight-paint visualization.  Sourced from the polymorphic skinning data so it
        ' works for both BSTriShape (BoneWeights inline) and NiTriShape (NiSkinPartition).
        Dim uvsWeight(vertexCount - 1) As Vector3
        Dim wpvForUv = If(shapeSkin.WeightsPerVertex > 0, shapeSkin.WeightsPerVertex, 4)
        Dim hasSkinForUv = (shapeSkin.BoneWeights IsNot Nothing AndAlso shapeSkin.VertexCount = vertexCount)
        For i As Integer = 0 To vertexCount - 1
            Dim u As Single = 0
            Dim v As Single = 0
            If shapeGeom.HasUVs AndAlso uvsList IsNot Nothing AndAlso i < uvsList.Count Then
                u = uvsList(i).U
                v = uvsList(i).V
            End If
            Dim w0 As Single = 0
            If hasSkinForUv Then w0 = CType(shapeSkin.BoneWeights(i * wpvForUv), Single)
            uvsWeight(i) = New Vector3(u, v, w0)
        Next
        geo.Uvs_Weight = uvsWeight

        If RecalculateNormals OrElse Not shapeGeom.HasNormals OrElse Not shapeGeom.HasTangents Then
            Dim opts = Config_App.Current.Setting_TBN
            RecalculateNormalsTangentsBitangents(geo, opts)
        End If
        Return geo
    End Function
    ''' <summary>
    ''' Converts an OpenTK Matrix4d (double, row-major) to a System.Numerics.Matrix4x4 (float, row-major SIMD).
    ''' Both use row-vector convention so this is a direct element-wise cast.
    ''' </summary>
    Private Shared Function ToNumericsMatrix(m As Matrix4d) As SysNumerics.Matrix4x4
        Return New SysNumerics.Matrix4x4(
            CSng(m.M11), CSng(m.M12), CSng(m.M13), CSng(m.M14),
            CSng(m.M21), CSng(m.M22), CSng(m.M23), CSng(m.M24),
            CSng(m.M31), CSng(m.M32), CSng(m.M33), CSng(m.M34),
            CSng(m.M41), CSng(m.M42), CSng(m.M43), CSng(m.M44))
    End Function

    ''' <summary>
    ''' Computes the normal matrix (inverse-transpose of upper-left 3x3) using SIMD-accelerated System.Numerics.
    ''' Returns a 4x4 with the 3x3 normal matrix in the upper-left and zero translation.
    ''' </summary>
    Private Shared Function CreateNormalMatrix_SIMD(mtot As SysNumerics.Matrix4x4) As SysNumerics.Matrix4x4
        Dim success As Boolean
        Dim inv As SysNumerics.Matrix4x4
        success = SysNumerics.Matrix4x4.Invert(mtot, inv)
        If Not success Then Return SysNumerics.Matrix4x4.Identity
        ' Transpose the 3x3 part, zero out translation
        Return New SysNumerics.Matrix4x4(
            inv.M11, inv.M21, inv.M31, 0,
            inv.M12, inv.M22, inv.M32, 0,
            inv.M13, inv.M23, inv.M33, 0,
            0, 0, 0, 1)
    End Function

    Private Shared Function Create_Normal_Matrix(Origen As Matrix4d) As Matrix4d
        Dim L As New Matrix3d(Origen)
        Dim nm3 = L.Inverted().Transposed()

        ' Reinyectar nm3 en una 4×4 sin traslación
        Dim nm4 As Matrix4d = Matrix4d.Identity
        nm4.M11 = nm3.M11 : nm4.M12 = nm3.M12 : nm4.M13 = nm3.M13
        nm4.M21 = nm3.M21 : nm4.M22 = nm3.M22 : nm4.M23 = nm3.M23
        nm4.M31 = nm3.M31 : nm4.M32 = nm3.M32 : nm4.M33 = nm3.M33
        Return nm4
    End Function
    ''' <summary>
    ''' Bake current pose into geometry: vertices/normals/tangents/bitangents are transformed
    ''' by the per-bone skin matrices stored in <paramref name="geom"/>. If the underlying
    ''' SkeletonInstance has no DeltaTransforms, matsBind == matsPose and the bake collapses
    ''' to identity (no-op outcome, callers paid the parallel-loop cost). Callers that want
    ''' "bake skipped when no pose" must check upstream and avoid invoking this method.
    ''' </summary>
    Public Shared Sub BakeFromMemoryUsingOriginal(Shape As IRenderableShape, ByRef geom As SkinnedGeometry, inverse As Boolean, ApplyMorph As Boolean, RemoveZaps As Boolean, singleBoneSkinning As Boolean,
                                                   Optional geometryModifier As IGeometryModifier = Nothing)
        ' 2) Matrices calculadas en ExtractSkinnedGeometry
        Dim matsBind() As Matrix4d = geom.BoneMatsBind
        Dim matsPose() As Matrix4d = geom.BoneMatsPose

        ' 3) Transformación global e inversa
        Dim GlobalTransform As Matrix4d = geom.ParentGlobalTransform
        Dim InverseGlobal As Matrix4d = GlobalTransform
        InverseGlobal.Invert()

        ' 4) Vértices resultantes de ExtractSkinnedGeometry (now local-space with GPU skinning)
        Dim worldV() As Vector3d

        ' 4b) Apply geometry modifier (e.g. zap removal) if provided
        If RemoveZaps AndAlso geometryModifier IsNot Nothing Then geometryModifier.Apply(Shape, geom)

        If ApplyMorph Then
            worldV = geom.Vertices.ToArray
        Else
            worldV = geom.BaseVertices.ToArray
        End If

        Dim worldN() As Vector3d = geom.Normals
        Dim worldT() As Vector3d = geom.Tangents
        Dim worldB() As Vector3d = geom.Bitangents

        ' 5) Datos de skinning por vértice — polimórficos via ShapeSkinningData
        '    (BSTriShape inline o NiSkinPartition expandido).
        Dim skinFlatIdx = geom.Skinning.BoneIndices
        Dim skinFlatWgt = geom.Skinning.BoneWeights
        Dim skinWpv = If(geom.Skinning.WeightsPerVertex > 0, geom.Skinning.WeightsPerVertex, 4)
        Dim hasSkin = (skinFlatIdx IsNot Nothing AndAlso skinFlatWgt IsNot Nothing AndAlso geom.Skinning.VertexCount = worldV.Length)

        'A - REVIERTE Skinning y Bakea
        ' Per-vertex linear blend (arithmetic mean) of matsBind y matsPose — coincide
        ' EXACTAMENTE con la fórmula del shader (Σw·bone[k]). La versión anterior calculaba
        ' Mskin = Σw·(matsBind·invMatsPose) e invertía: ése es la "media armónica" de
        ' matrices y NO equivale a Σw·matsPose para vértices con peso repartido entre
        ' huesos. Como resultado, render-with-bind(v_baked) ≠ render-with-pose(v_orig)
        ' cuando wpv>1 (típico en bodies). Round-trip seguía siendo identidad porque la
        ' fórmula es invertible consigo misma, pero no preservaba la pose visualmente.

        Select Case True
            Case Not singleBoneSkinning AndAlso matsBind.Length > 0
                ' Multibone — vertices in shape-local; transform per-vertex con blend lineal
                Parallel.For(0, worldV.Length, Sub(i)
                                                   Dim MposeBlend As Matrix4d = Matrix4d.Zero
                                                   Dim MbindBlend As Matrix4d = Matrix4d.Zero
                                                   Dim sumW As Double = 0

                                                   If hasSkin Then
                                                       Dim baseIdx = i * skinWpv
                                                       Dim cnt = Math.Min(skinWpv, Math.Min(skinFlatWgt.Length - baseIdx, skinFlatIdx.Length - baseIdx)) - 1
                                                       For j = 0 To cnt
                                                           sumW += CType(skinFlatWgt(baseIdx + j), Double)
                                                       Next
                                                       If sumW = 0F Then
                                                           Dim idx0 = If(cnt >= 0, skinFlatIdx(baseIdx), CByte(0))
                                                           Dim idx0c = Math.Max(0, Math.Min(CInt(idx0), matsBind.Length - 1))
                                                           MposeBlend = matsPose(idx0c)
                                                           MbindBlend = matsBind(idx0c)
                                                       Else
                                                           For j = 0 To cnt
                                                               Dim w = CType(skinFlatWgt(baseIdx + j), Double) / sumW
                                                               Dim idx = skinFlatIdx(baseIdx + j)
                                                               If idx >= 0 AndAlso idx < matsBind.Length Then
                                                                   MposeBlend += matsPose(idx) * w
                                                                   MbindBlend += matsBind(idx) * w
                                                               End If
                                                           Next
                                                       End If
                                                   Else
                                                       MposeBlend = matsPose(0)
                                                       MbindBlend = matsBind(0)
                                                   End If

                                                   ' v_baked tal que v_baked·MbindBlend = v_orig·MposeBlend
                                                   '   ⇒ v_baked = v_orig · MposeBlend · inv(MbindBlend)
                                                   ' Inverse=True invierte la dirección (unbake).
                                                   Dim skinMat As Matrix4d
                                                   If Not inverse Then
                                                       skinMat = MposeBlend * Matrix4d.Invert(MbindBlend)
                                                   Else
                                                       skinMat = MbindBlend * Matrix4d.Invert(MposeBlend)
                                                   End If
                                                   Dim totalSkinMat As Matrix4d = InverseGlobal * skinMat * GlobalTransform
                                                   Dim NormalsMat = Create_Normal_Matrix(totalSkinMat)

                                                   ' Bake (local -> new-local)
                                                   worldV(i) = Vector3d.TransformPosition(worldV(i), totalSkinMat)
                                                   worldN(i) = Vector3d.Normalize(Vector3d.TransformNormal(worldN(i), NormalsMat))
                                                   worldT(i) = Vector3d.Normalize(Vector3d.TransformNormal(worldT(i), NormalsMat))
                                                   worldB(i) = Vector3d.Normalize(Vector3d.TransformNormal(worldB(i), NormalsMat))
                                               End Sub)

            Case singleBoneSkinning AndAlso matsBind.Length > 0
                ' Single-bone — vertices are already in local space, transform per-vertex.
                ' Por diseño matsPose(0)=matsBind(0) en single-bone (no aplica pose), así que
                ' skinMat colapsa a identidad. Mantenemos la fórmula explícita para que la
                ' lógica sea legible (no es un caso optimizado, sólo correcto).
                Dim skinMat As Matrix4d
                If Not inverse Then
                    skinMat = matsPose(0) * Matrix4d.Invert(matsBind(0))
                Else
                    skinMat = matsBind(0) * Matrix4d.Invert(matsPose(0))
                End If
                Dim totalSkinMat As Matrix4d = InverseGlobal * skinMat * GlobalTransform
                Dim NormalsMat = Create_Normal_Matrix(totalSkinMat)

                Parallel.For(0, worldV.Length, Sub(i)
                                                   ' Bake (local -> new-local)
                                                   worldV(i) = Vector3d.TransformPosition(worldV(i), totalSkinMat)
                                                   worldN(i) = Vector3d.Normalize(Vector3d.TransformNormal(worldN(i), NormalsMat))
                                                   worldT(i) = Vector3d.Normalize(Vector3d.TransformNormal(worldT(i), NormalsMat))
                                                   worldB(i) = Vector3d.Normalize(Vector3d.TransformNormal(worldB(i), NormalsMat))
                                               End Sub)

            Case Else
                ' Sin huesos:
                ' no hay skin/pose que bakear. La geometria ya esta en espacio local del shape.
                ' Mantener identidad evita meter el transform del shape/padres dentro de los vertices.
                Dim totalSkinMat As Matrix4d = Matrix4d.Identity
                Dim NormalsMat = Create_Normal_Matrix(totalSkinMat)

                Parallel.For(0, worldV.Length, Sub(i)
                                                   worldV(i) = Vector3d.TransformPosition(worldV(i), totalSkinMat)
                                                   worldN(i) = Vector3d.Normalize(Vector3d.TransformNormal(worldN(i), NormalsMat))
                                                   worldT(i) = Vector3d.Normalize(Vector3d.TransformNormal(worldT(i), NormalsMat))
                                                   worldB(i) = Vector3d.Normalize(Vector3d.TransformNormal(worldB(i), NormalsMat))
                                               End Sub)

        End Select

        If ApplyMorph Then
            geom.Vertices = worldV
            geom.BaseVertices = CType(worldV.Clone(), Vector3d())
        Else
            geom.Vertices = worldV
        End If

        InjectToTrishape(geom)

    End Sub
    ''' <summary>
    ''' Writes the SkinnedGeometry contents back to the underlying NIF shape.  Fully
    ''' polymorphic via geom.Geometry — works identically for BSTriShape family and
    ''' NiTriShape family.
    '''
    ''' Flow: ResizeVertices(newCount) establishes the target size on the underlying block
    ''' (BSTriShape replaces its packed BSVertexData/SSE list, NiTriShape resizes
    ''' NiTriShapeData.Vertices).  Then each per-field setter writes into the already-sized
    ''' buffer.  SetSkinning rebuilds per-vertex bone data (BSTriShape inline,
    ''' NiTriShape NiSkinData.BoneList).
    '''
    ''' Skin partition regeneration is NOT performed here — caller must call
    ''' Nifcontent_Class_Manolo.UpdateSkinPartitions(geom.Geometry.BackingShape) before
    ''' saving (BuildingForm, MorphingHelper.RemoveZaps callers and SplitShapeHelper already
    ''' follow that contract).  See UpdateSkinPartitions docstring for the order contract.
    ''' </summary>
    Public Shared Sub InjectToTrishape(ByRef geom As SkinnedGeometry)
        Dim nNew As Integer = geom.Vertices.Length
        Dim shapeGeom = geom.Geometry
        If shapeGeom Is Nothing Then Exit Sub

        Dim posN As New List(Of System.Numerics.Vector3)(nNew)
        Dim uvN As New List(Of TexCoord)(nNew)
        Dim colN As New List(Of NiflySharp.Structs.Color4)(nNew)

        For i As Integer = 0 To nNew - 1
            Dim v1 = geom.Vertices(i) : posN.Add(New System.Numerics.Vector3(CSng(v1.X), CSng(v1.Y), CSng(v1.Z)))
            Dim uv = geom.Uvs_Weight(i) : uvN.Add(New TexCoord(CSng(uv.X), CSng(uv.Y)))
            Dim c = geom.VertexColors(i) : colN.Add(New NiflySharp.Structs.Color4(CSng(c.X), CSng(c.Y), CSng(c.Z), CSng(c.W)))
        Next

        Dim idxArr = geom.Indices
        Dim tmpTris As New List(Of Triangle)(idxArr.Length \ 3)
        For tr As Integer = 0 To idxArr.Length - 3 Step 3
            tmpTris.Add(New Triangle(CInt(idxArr(tr)), CInt(idxArr(tr + 1)), CInt(idxArr(tr + 2))))
        Next

        ' Establish new vertex count on the block (no-op if unchanged).  For BSTriShape
        ' this allocates a fresh zero-init packed list of the new size; for NiTriShape it
        ' resizes NiTriShapeData.Vertices.  Subsequent Set* calls write into the sized
        ' storage.
        shapeGeom.ResizeVertices(nNew)

        ' Per-field writes.  Order matters slightly: positions before normals/tangents so
        ' adapter-internal TBN recalc (if any) has correct positions; skinning + triangles
        ' last since they reference the established vertex/triangle space.
        shapeGeom.SetVertexPositions(posN)
        If shapeGeom.HasNormals OrElse shapeGeom.HasTangents Then
            InjectNormalsToTrishape(geom)
        End If
        If shapeGeom.HasUVs Then shapeGeom.SetUVs(uvN)
        If shapeGeom.HasVertexColors Then shapeGeom.SetVertexColors(colN)
        If shapeGeom.HasEyeData Then shapeGeom.SetEyeData(geom.Eyedata.ToList())

        ' Polymorphic skin write-back.  For BSTriShape writes BoneIndices/BoneWeights inline
        ' into BSVertexData.  For NiTriShape rebuilds NiSkinData.BoneList[].VertexWeights
        ' from the per-vertex Skinning data.  Critical for the NiTriShape family:
        ' UpdateSkinPartitions later reads from NiSkinData to regenerate the partition.
        If geom.Skinning.VertexCount = nNew Then
            shapeGeom.SetSkinning(geom.Skinning)
        End If

        ' Provenance-aware triangle write — redistributes BSMeshLOD/BSLOD LOD sizes and
        ' BSSubIndex/BSSegmented Segments via the adapter when geom.TriangleProvenance is
        ' present (zap/split populate it).  Must come AFTER SetSkinning because the
        ' segment redistribution reads the post-write triangle list.
        shapeGeom.SetTriangles(tmpTris, geom.TriangleProvenance)

        ' Edge case: empty shape (all vertices zapped).  BSTriShape exposes writable flag
        ' properties for this; NiTriShape handles empty state via the Has* setters on
        ' NiGeometryData (automatically triggered when lists are empty).  Only BSTriShape
        ' needs the explicit flag flip here.
        If nNew = 0 Then
            Dim bsTri = TryCast(shapeGeom.BackingShape, BSTriShape)
            If bsTri IsNot Nothing Then
                bsTri.HasVertices = False
                bsTri.HasNormals = False
                bsTri.HasTangents = False
                bsTri.HasVertexColors = False
                bsTri.HasEyeData = False
                bsTri.HasUVs = False
            End If
        End If
    End Sub

    ''' <summary>
    ''' Injects only normals, tangents and bitangents from geo into the underlying shape via
    ''' the polymorphic adapter.  The INVERTIDAS swap (renderer Tangent/Bitangent ⇆ NIF
    ''' Bitangent/Tangent) is encapsulated in IShapeGeometry.SetTangents/SetBitangents — this
    ''' method passes geom.Tangents and geom.Bitangents straight through.
    ''' </summary>
    Public Shared Sub InjectNormalsToTrishape(ByRef geom As SkinnedGeometry)
        Dim shapeGeom = geom.Geometry
        If shapeGeom Is Nothing Then Exit Sub
        Dim nNew = geom.Vertices.Length
        If nNew = 0 Then Exit Sub

        Dim norN As New List(Of System.Numerics.Vector3)(nNew)
        Dim tanN As New List(Of System.Numerics.Vector3)(nNew)
        Dim bitN As New List(Of System.Numerics.Vector3)(nNew)
        For i = 0 To nNew - 1
            Dim n1 = geom.Normals(i) : norN.Add(New System.Numerics.Vector3(CSng(n1.X), CSng(n1.Y), CSng(n1.Z)))
            Dim t1 = geom.Tangents(i) : tanN.Add(New System.Numerics.Vector3(CSng(t1.X), CSng(t1.Y), CSng(t1.Z)))
            Dim b1 = geom.Bitangents(i) : bitN.Add(New System.Numerics.Vector3(CSng(b1.X), CSng(b1.Y), CSng(b1.Z)))
        Next
        shapeGeom.SetNormals(norN)
        shapeGeom.SetTangents(tanN)
        shapeGeom.SetBitangents(bitN)
    End Sub

    ''' <summary>
    ''' Snapshots the per-vertex separate arrays from a shape via the polymorphic adapter.
    ''' UVs are converted from TexCoord to Vector3(U,V,0) — that packing is what
    ''' ApplyShapeGeometry / WM merge/split helpers expect when concatenating arrays from
    ''' multiple shapes before re-injecting.  The adapter takes care of the INVERTIDAS swap
    ''' for BSTriShape, so the snapshot is in renderer convention regardless of family.
    ''' </summary>
    Public Shared Function SnapshotSeparateArrays(shape As IShapeGeometry) As ShapeArrays
        If shape Is Nothing Then Return New ShapeArrays()
        Dim snap As New ShapeArrays With {
            .Positions = shape.GetVertexPositions()
        }
        If shape.HasNormals Then snap.Normals = shape.GetNormals()
        If shape.HasTangents Then
            snap.Tangents = shape.GetTangents()
            snap.Bitangents = shape.GetBitangents()
        End If
        If shape.HasUVs Then snap.UVs = shape.GetUVs().Select(
            Function(u) New System.Numerics.Vector3(u.U, u.V, 0)).ToList()
        If shape.HasVertexColors Then snap.VertexColors = shape.GetVertexColors()
        If shape.HasEyeData Then snap.EyeData = shape.GetEyeData()
        ' Capture per-vertex skin uniformly for all families.  After the unified
        ' InjectToTrishape / ApplyShapeGeometry refactor there's no packed-buffer fast
        ' path for BSTriShape — skin always travels via ShapeArrays.Skinning and the
        ' adapter's SetSkinning writes it back (inline BSVertexData for BS, NiSkinData
        ' rebuild for NiTri).
        If shape.IsSkinned Then snap.Skinning = shape.GetSkinning()
        Return snap
    End Function

    ''' <summary>
    ''' Applies separate per-vertex arrays + triangles + optional skinning to the underlying
    ''' shape via the polymorphic adapter.  Single authoritative point for updating shape
    ''' geometry when vertex count changes.  Fully polymorphic — no BSTriShape-specific
    ''' packed buffer parameters; the adapter internally handles BSTriShape packed resize
    ''' via ResizeVertices and the per-field setters write into the established storage.
    '''
    ''' Skin partition update remains caller's responsibility (same as InjectToTrishape).
    ''' </summary>
    Public Shared Sub ApplyShapeGeometry(
            shape As IShapeGeometry,
            triangles As List(Of Triangle),
            arrays As ShapeArrays,
            Optional provenance As TriangleRemap = Nothing)
        If shape Is Nothing Then Return

        ' Establish new vertex count on the backing block before any per-field write.
        ' Uses positions count as the new vertex count (canonical per the BSTriShape /
        ' NiTriShape setters — SetVertexPositions silently no-ops if count doesn't match).
        Dim newVc As Integer = If(arrays IsNot Nothing AndAlso arrays.Positions IsNot Nothing,
                                   arrays.Positions.Count, shape.VertexCount)
        shape.ResizeVertices(newVc)

        If arrays IsNot Nothing Then
            If arrays.Positions IsNot Nothing Then shape.SetVertexPositions(arrays.Positions)
            If arrays.Normals IsNot Nothing AndAlso shape.HasNormals Then shape.SetNormals(arrays.Normals)
            If arrays.Tangents IsNot Nothing AndAlso shape.HasTangents Then shape.SetTangents(arrays.Tangents)
            If arrays.Bitangents IsNot Nothing AndAlso shape.HasTangents Then shape.SetBitangents(arrays.Bitangents)
            If arrays.UVs IsNot Nothing AndAlso shape.HasUVs Then shape.SetUVs(arrays.UVs.Select(Function(v) New TexCoord(v.X, v.Y)).ToList())
            If arrays.VertexColors IsNot Nothing AndAlso shape.HasVertexColors Then shape.SetVertexColors(arrays.VertexColors)
            If arrays.EyeData IsNot Nothing AndAlso shape.HasEyeData Then shape.SetEyeData(arrays.EyeData)

            ' Polymorphic skin write-back when caller populated it.  For BSTriShape this
            ' writes BoneIndices/BoneWeights into the packed buffer that ResizeVertices
            ' established; for NiTriShape it rebuilds NiSkinData.BoneList[].VertexWeights.
            If arrays.Skinning.HasValue Then shape.SetSkinning(arrays.Skinning.Value)
        End If

        ' Provenance-aware triangle write: redistribute Segments / LOD sizes when caller
        ' supplied a per-new-triangle source map (split / merge populate this).  Without
        ' provenance the adapter leaves metadata stale.
        shape.SetTriangles(triangles, provenance)
    End Sub

    ' =========================================================================
    ' World-space cache functions (GPU skinning: vertices are local-space,
    ' world-space is computed lazily on demand)
    ' =========================================================================

    ''' <summary>
    ''' Lazily computes and caches world-space vertex positions from local-space + PerVertexSkinMatrix.
    ''' </summary>
    Public Shared Function GetWorldVertices(ByRef geo As SkinnedGeometry) As Vector3d()
        If geo.WorldCacheValid AndAlso geo.CachedWorldVertices IsNot Nothing Then Return geo.CachedWorldVertices
        ComputeWorldSpaceCache(geo)
        Return geo.CachedWorldVertices
    End Function

    Public Shared Function GetWorldNormals(ByRef geo As SkinnedGeometry) As Vector3d()
        If geo.WorldCacheValid AndAlso geo.CachedWorldNormals IsNot Nothing Then Return geo.CachedWorldNormals
        ComputeWorldSpaceCache(geo)
        Return geo.CachedWorldNormals
    End Function

    Public Shared Sub ComputeWorldSpaceCache(ByRef geo As SkinnedGeometry)
        Dim count = geo.Vertices.Length
        ' Capture arrays as locals — VB.NET cannot capture ByRef params in lambdas
        Dim localVerts = geo.Vertices
        Dim localNorms = geo.Normals
        Dim localMats = geo.PerVertexSkinMatrix
        Dim wv(count - 1) As Vector3d
        Dim wn(count - 1) As Vector3d
        Parallel.For(0, count, Sub(i)
                                   wv(i) = Vector3d.TransformPosition(localVerts(i), localMats(i))
                                   Dim nm = Create_Normal_Matrix(localMats(i))
                                   wn(i) = Vector3d.Normalize(Vector3d.TransformNormal(localNorms(i), nm))
                               End Sub)
        geo.CachedWorldVertices = wv
        geo.CachedWorldNormals = wn
        geo.WorldCacheValid = True
    End Sub

    Public Shared Sub InvalidateWorldCache(ByRef geo As SkinnedGeometry)
        geo.WorldCacheValid = False
        geo.CachedWorldVertices = Nothing
        geo.CachedWorldNormals = Nothing
    End Sub

    ''' <summary>
    ''' Computes world-space bounding box from the world-space cache.
    ''' </summary>
    Public Shared Sub ComputeWorldBounds(ByRef geo As SkinnedGeometry)
        Dim wv = GetWorldVertices(geo)
        Dim minV As New Vector3d(Double.MaxValue)
        Dim maxV As New Vector3d(Double.MinValue)
        For Each v In wv
            If v.X < minV.X Then minV.X = v.X
            If v.Y < minV.Y Then minV.Y = v.Y
            If v.Z < minV.Z Then minV.Z = v.Z
            If v.X > maxV.X Then maxV.X = v.X
            If v.Y > maxV.Y Then maxV.Y = v.Y
            If v.Z > maxV.Z Then maxV.Z = v.Z
        Next
        geo.Boundingcenter = (minV + maxV) * 0.5
        geo.Minv = minV
        geo.Maxv = maxV
    End Sub

    ' ┌─────────────────────────────────────────────────────────────────────────┐
    ' │ GPU BONE MATRIX RECOMPUTATION — SYNC CONTRACT                         │
    ' │                                                                       │
    ' │ Recomputes GPUBoneMatrices (SSBO data) for a new pose.                │
    ' │ Matrix composition: GlobalTransform * poseT.ComposeTransforms(localT) │
    ' │ This MUST match the composition in ExtractSkinnedGeometry.            │
    ' │ The resulting matrices are uploaded to the SSBO and consumed by the   │
    ' │ vertex shader's bone blend loop. See Shader_Class.vb sync contract.   │
    ' └─────────────────────────────────────────────────────────────────────────┘
    ''' <param name="skeleton">SkeletonInstance to read bind/pose transforms from. Same fallback
    ''' contract as <see cref="ExtractSkinnedGeometry"/>: Nothing → SkeletonInstance.Default.
    ''' Pose application is implicit via DeltaTransforms; callers wanting bind call
    ''' <see cref="SkeletonInstance.Reset"/> first.</param>
    Public Shared Sub RecomputeGPUBoneMatrices(shape As IRenderableShape, ByRef geo As SkinnedGeometry, singleboneskinning As Boolean, Optional skeleton As SkeletonInstance = Nothing)
        If geo.GPUBoneMatrices Is Nothing Then Exit Sub
        Dim effectiveSkel As SkeletonInstance = If(skeleton, SkeletonInstance.Default)

        Dim bones = shape.ShapeBones.ToArray()
        Dim boneTrans = shape.ShapeBoneTransforms.ToArray()
        If boneTrans.Length <> bones.Length Then Exit Sub

        ' Recompute GlobalTransform
        Dim backing = shape.Geometry?.BackingShape
        Dim shapeNode = TryCast(shape.NifContent.GetParentNode(backing), NiNode)
        If IsNothing(shapeNode) Then shapeNode = shape.NifContent.GetRootNode()
        Dim GlobalTransform = If(shapeNode IsNot Nothing, Transform_Class.GetGlobalTransform(shapeNode, shape.NifContent).ToMatrix4d(), Matrix4d.Identity)

        If Not singleboneskinning AndAlso bones.Length > 0 Then
            ' Multi-bone path: recompute bone matrices once, use for both SSBO and per-vertex blending.
            ' Keep geo.BoneMatsBind/BoneMatsPose in sync too — BakeFromMemoryUsingOriginal reads
            ' them to compute bindTimesInvPose, and if they're stale from the previous Extract
            ' the first bake after a pose change collapses to identity.
            Dim precomputedBoneMatrices(bones.Length - 1) As Matrix4d
            If geo.BoneMatsBind Is Nothing OrElse geo.BoneMatsBind.Length <> bones.Length Then
                ReDim geo.BoneMatsBind(bones.Length - 1)
            End If
            If geo.BoneMatsPose Is Nothing OrElse geo.BoneMatsPose.Length <> bones.Length Then
                ReDim geo.BoneMatsPose(bones.Length - 1)
            End If
            For k = 0 To bones.Length - 1
                Dim localT = boneTrans(k)
                Dim boneName = bones(k).Name.String
                Dim SkeletonBone As HierarchiBone_class = Nothing
                Dim poseT As Transform_Class
                Dim bindT As Transform_Class

                If effectiveSkel.SkeletonDictionary.TryGetValue(boneName, SkeletonBone) Then
                    bindT = SkeletonBone.OriginalGetGlobalTransform
                Else
                    bindT = Transform_Class.GetGlobalTransform(bones(k), shape.NifContent)
                End If

                If Not IsNothing(SkeletonBone) Then
                    poseT = SkeletonBone.GetGlobalTransform()
                Else
                    poseT = bindT
                End If

                Dim matBind = bindT.ComposeTransforms(localT).ToMatrix4d()
                Dim matPose = poseT.ComposeTransforms(localT).ToMatrix4d()
                geo.BoneMatsBind(k) = matBind
                geo.BoneMatsPose(k) = matPose

                Dim m = GlobalTransform * matPose
                precomputedBoneMatrices(k) = m
                geo.GPUBoneMatrices(k) = New Matrix4(
                    CSng(m.M11), CSng(m.M12), CSng(m.M13), CSng(m.M14),
                    CSng(m.M21), CSng(m.M22), CSng(m.M23), CSng(m.M24),
                    CSng(m.M31), CSng(m.M32), CSng(m.M33), CSng(m.M34),
                    CSng(m.M41), CSng(m.M42), CSng(m.M43), CSng(m.M44))
            Next

            ' Also update perVertexSkinMatrix for world-space cache
            ' (Recompute per-vertex blended matrices using the same precomputed bone matrices)

            Dim vertexCount = geo.Vertices.Length
            ' Capture arrays as locals for safe parallel access (geo is ByRef).
            ' Reuse the polymorphic skin data filled by ExtractSkinnedGeometry — no need to
            ' re-snapshot tri.VertexData/VertexDataSSE here, those arrays are already encoded
            ' in geo.Skinning (and they're empty for NiTriShape, where the partition path was used).
            Dim perVertexSkinMatrix = geo.PerVertexSkinMatrix
            Dim localFlatIdx = geo.Skinning.BoneIndices
            Dim localFlatWgt = geo.Skinning.BoneWeights
            Dim localWpv = If(geo.Skinning.WeightsPerVertex > 0, geo.Skinning.WeightsPerVertex, 4)
            Dim localHasSkin = (localFlatIdx IsNot Nothing AndAlso localFlatWgt IsNot Nothing AndAlso geo.Skinning.VertexCount = vertexCount)
            Dim localPrecomputed = precomputedBoneMatrices

            Dim skinBody As Action(Of Integer) = Sub(i)
                                                     If localHasSkin Then
                                                         perVertexSkinMatrix(i) = BlendBoneMatrices(localFlatWgt, localFlatIdx, i * localWpv, localWpv, localPrecomputed)
                                                     Else
                                                         perVertexSkinMatrix(i) = If(localPrecomputed.Length > 0, localPrecomputed(0), Matrix4d.Identity)
                                                     End If
                                                 End Sub

            If vertexCount >= 500 Then
                Parallel.For(0, vertexCount, skinBody)
            Else
                For i = 0 To vertexCount - 1
                    skinBody(i)
                Next
            End If
        Else
            ' Single-bone or no-bone path: ignora pose animada por diseño (single-bone
            ' es un modo de preview rigido), pero debe respetar bindT(0) * localT(0)
            ' del hueso 0. Si no lo hiciera, el shape salta cada vez que cambia la pose
            ' porque se pierde el transform del hueso. Esta composicion tiene que
            ' coincidir con lo que computa ExtractSkinnedGeometry en el caso single-bone.
            Dim Mtot As Matrix4d
            If bones.Length > 0 Then
                Dim localT = boneTrans(0)
                Dim boneName = bones(0).Name.String
                Dim SkeletonBone As HierarchiBone_class = Nothing
                Dim bindT As Transform_Class
                If effectiveSkel.SkeletonDictionary.TryGetValue(boneName, SkeletonBone) Then
                    bindT = SkeletonBone.OriginalGetGlobalTransform
                Else
                    bindT = Transform_Class.GetGlobalTransform(bones(0), shape.NifContent)
                End If
                Mtot = GlobalTransform * bindT.ComposeTransforms(localT).ToMatrix4d()
            Else
                ' Sin huesos: shape transform + padres, igual que en ExtractSkinnedGeometry
                Mtot = Transform_Class.GetGlobalTransform(backing, shape.NifContent).ToMatrix4d()
            End If

            geo.GPUBoneMatrices(0) = New Matrix4(
                CSng(Mtot.M11), CSng(Mtot.M12), CSng(Mtot.M13), CSng(Mtot.M14),
                CSng(Mtot.M21), CSng(Mtot.M22), CSng(Mtot.M23), CSng(Mtot.M24),
                CSng(Mtot.M31), CSng(Mtot.M32), CSng(Mtot.M33), CSng(Mtot.M34),
                CSng(Mtot.M41), CSng(Mtot.M42), CSng(Mtot.M43), CSng(Mtot.M44))
            Array.Fill(geo.PerVertexSkinMatrix, Mtot)
        End If

        ' Invalidate world-space cache so it gets recomputed on next access
        InvalidateWorldCache(geo)
        ' Recompute world bounds from new pose
        ComputeWorldBounds(geo)
    End Sub

End Class

''' <summary>
''' Holds per-vertex arrays in the types expected by IShapeGeometry.Set* methods.
''' Skinning is optional — populated by SnapshotSeparateArrays for round-trip on the
''' NiTriShape family (where per-vertex skin lives in NiSkinData rather than inline);
''' BSTriShape consumers can ignore it because their per-vertex skin travels inside
''' BSVertexData/SSE structs already.
''' </summary>
Public Class ShapeArrays
    Public Positions As List(Of System.Numerics.Vector3)
    Public Normals As List(Of System.Numerics.Vector3)
    Public Tangents As List(Of System.Numerics.Vector3)
    Public Bitangents As List(Of System.Numerics.Vector3)
    Public UVs As List(Of System.Numerics.Vector3)
    Public VertexColors As List(Of NiflySharp.Structs.Color4)
    Public EyeData As List(Of Single)
    Public Skinning As ShapeSkinningData?

    ''' <summary>Returns a new ShapeArrays containing only elements at the given original indices.</summary>
    Public Function FilterByIndices(indices As HashSet(Of Integer)) As ShapeArrays
        Dim r As New ShapeArrays()
        If Positions IsNot Nothing Then r.Positions = Positions.Where(Function(x, i) indices.Contains(i)).ToList()
        If Normals IsNot Nothing Then r.Normals = Normals.Where(Function(x, i) indices.Contains(i)).ToList()
        If Tangents IsNot Nothing Then r.Tangents = Tangents.Where(Function(x, i) indices.Contains(i)).ToList()
        If Bitangents IsNot Nothing Then r.Bitangents = Bitangents.Where(Function(x, i) indices.Contains(i)).ToList()
        If UVs IsNot Nothing Then r.UVs = UVs.Where(Function(x, i) indices.Contains(i)).ToList()
        If VertexColors IsNot Nothing Then r.VertexColors = VertexColors.Where(Function(x, i) indices.Contains(i)).ToList()
        If EyeData IsNot Nothing Then r.EyeData = EyeData.Where(Function(x, i) indices.Contains(i)).ToList()

        ' Per-vertex skinning compaction: keep only slots for surviving vertex indices,
        ' preserve WeightsPerVertex layout (default 4).  Bone palette unchanged.
        If Skinning.HasValue AndAlso Skinning.Value.BoneIndices IsNot Nothing Then
            Dim sk = Skinning.Value
            Dim wpv As Integer = If(sk.WeightsPerVertex > 0, sk.WeightsPerVertex, 4)
            Dim ordered = indices.OrderBy(Function(x) x).ToList()
            Dim newCount As Integer = ordered.Count
            Dim newIdx(newCount * wpv - 1) As Byte
            Dim newWgt(newCount * wpv - 1) As System.Half
            For i As Integer = 0 To newCount - 1
                Dim oldVert As Integer = ordered(i)
                Dim oldBase As Integer = oldVert * wpv
                Dim newBase As Integer = i * wpv
                For j As Integer = 0 To wpv - 1
                    newIdx(newBase + j) = sk.BoneIndices(oldBase + j)
                    newWgt(newBase + j) = sk.BoneWeights(oldBase + j)
                Next
            Next
            r.Skinning = New ShapeSkinningData() With {
                .BoneIndices = newIdx,
                .BoneWeights = newWgt,
                .WeightsPerVertex = wpv,
                .VertexCount = newCount,
                .BoneRefIndices = sk.BoneRefIndices
            }
        End If
        Return r
    End Function

    ''' <summary>Appends all arrays from another ShapeArrays (for merge/concatenation).</summary>
    Public Sub Append(other As ShapeArrays)
        If other Is Nothing Then Return
        If other.Positions IsNot Nothing Then
            If Positions Is Nothing Then Positions = New List(Of System.Numerics.Vector3)()
            Positions.AddRange(other.Positions)
        End If
        If other.Normals IsNot Nothing Then
            If Normals Is Nothing Then Normals = New List(Of System.Numerics.Vector3)()
            Normals.AddRange(other.Normals)
        End If
        If other.Tangents IsNot Nothing Then
            If Tangents Is Nothing Then Tangents = New List(Of System.Numerics.Vector3)()
            Tangents.AddRange(other.Tangents)
        End If
        If other.Bitangents IsNot Nothing Then
            If Bitangents Is Nothing Then Bitangents = New List(Of System.Numerics.Vector3)()
            Bitangents.AddRange(other.Bitangents)
        End If
        If other.UVs IsNot Nothing Then
            If UVs Is Nothing Then UVs = New List(Of System.Numerics.Vector3)()
            UVs.AddRange(other.UVs)
        End If
        If other.VertexColors IsNot Nothing Then
            If VertexColors Is Nothing Then VertexColors = New List(Of NiflySharp.Structs.Color4)()
            VertexColors.AddRange(other.VertexColors)
        End If
        If other.EyeData IsNot Nothing Then
            If EyeData Is Nothing Then EyeData = New List(Of Single)()
            EyeData.AddRange(other.EyeData)
        End If
        ' Skinning concat: flat BoneIndices + BoneWeights arrays concatenated with aligned
        ' WeightsPerVertex.  Caller is responsible for bone-palette remap on the donor's
        ' BoneIndices BEFORE Append (see MergeShapesHelper).  Both sides must agree on
        ' WeightsPerVertex; if not, throw loud — a 4-wpv target + 5-wpv donor merge is
        ' undefined behaviour in the NIF schema.
        If other.Skinning.HasValue Then
            If Not Skinning.HasValue Then
                ' Target had no skinning; adopt donor's as the start.
                Skinning = other.Skinning
            Else
                Dim a = Skinning.Value
                Dim b = other.Skinning.Value
                If a.WeightsPerVertex <> b.WeightsPerVertex AndAlso
                   a.WeightsPerVertex > 0 AndAlso b.WeightsPerVertex > 0 Then
                    Throw New NotSupportedException(
                        $"ShapeArrays.Append: WeightsPerVertex mismatch ({a.WeightsPerVertex} vs " &
                        $"{b.WeightsPerVertex}).  Cannot merge per-vertex skin with different slot " &
                        "counts without re-padding.")
                End If
                Dim wpv As Integer = If(a.WeightsPerVertex > 0, a.WeightsPerVertex, b.WeightsPerVertex)
                Dim aCount = a.VertexCount
                Dim bCount = b.VertexCount
                Dim combined As Integer = aCount + bCount
                Dim newIdx(combined * wpv - 1) As Byte
                Dim newWgt(combined * wpv - 1) As System.Half
                If a.BoneIndices IsNot Nothing Then Array.Copy(a.BoneIndices, 0, newIdx, 0, aCount * wpv)
                If a.BoneWeights IsNot Nothing Then Array.Copy(a.BoneWeights, 0, newWgt, 0, aCount * wpv)
                If b.BoneIndices IsNot Nothing Then Array.Copy(b.BoneIndices, 0, newIdx, aCount * wpv, bCount * wpv)
                If b.BoneWeights IsNot Nothing Then Array.Copy(b.BoneWeights, 0, newWgt, aCount * wpv, bCount * wpv)
                Skinning = New ShapeSkinningData() With {
                    .BoneIndices = newIdx,
                    .BoneWeights = newWgt,
                    .WeightsPerVertex = wpv,
                    .VertexCount = combined,
                    .BoneRefIndices = a.BoneRefIndices   ' target's palette reference wins
                }
            End If
        End If
    End Sub
End Class


Public Class RecalcTBN
    Public Structure TBNCache
        ' Copia/Referencia de índices del mesh (no se modifica aquí)
        Public Indices As UInteger()
        ' Cantidad de triángulos
        Public TriCount As Integer
        ' Adjacencia: por cada vértice -> lista de triángulos incidentes (ID de tri: [0..TriCount-1])
        Public VertexToTriangles As List(Of Integer)()
        ' Derivadas UV precomputadas por triángulo (dependen SOLO de UV)
        Public Tri_du1 As Double()
        Public Tri_dv1 As Double()
        Public Tri_du2 As Double()
        Public Tri_dv2 As Double()
        Public Tri_det As Double()
    End Structure

    ' -------------------------------
    ' Opciones de calidad / robustez
    ' -------------------------------
    Public Enum NormalWeightMode
        AreaOnly = 0
        AngleOnly = 1
        AreaTimesAngle = 2   ' recomendado (por defecto)
    End Enum

    Public Structure TBNOptions
        Public Property WeightMode As NormalWeightMode          ' cómo pesar contribuciones de caras
        Public Property EpsilonPos As Double                    ' umbral para degenerados geométricos
        Public Property EpsilonUV As Double                     ' umbral para degenerados en UV (det≈0)
        Public Property NormalizeOutputs As Boolean             ' normalizar N/T/B al final
        Public Property ForceOrthogonalBitangent As Boolean     ' si True: B := normalize(N × T)
        Public Property RepairNaNs As Boolean                   ' si True: reemplaza NaN por vectores seguros

        ' --- Welding (opcional) ---
        Public Property EnableWelding As Boolean                ' activa agrupación por posición+UV
        Public Property WeldPosEpsilon As Double                ' tolerancia para posición (en unidades del modelo)
        Public Property WeldUVEpsilon As Double                 ' tolerancia para UV (u,v)
        Public Property WeldByPositionOnly As Boolean           ' Only positions or positions + UV
    End Structure

    Public Shared Function DefaultTBNOptions() As TBNOptions
        Return New TBNOptions With {
                .WeightMode = NormalWeightMode.AreaTimesAngle,
                .EpsilonPos = 0.000000000001,
                .EpsilonUV = 0.000000000001,
                .NormalizeOutputs = True,
                .ForceOrthogonalBitangent = True,     ' false preserva B acumulada si es válida
                .RepairNaNs = True,
                .EnableWelding = False,                ' desactivado por defecto
                .WeldPosEpsilon = 0.000000000001,
                .WeldUVEpsilon = 0.000000000001,
                .WeldByPositionOnly = False           ' Positions + UV
            }
    End Function

    ' =========================================================================
    ' BUILD CACHE (llamar una sola vez al cargar o cuando cambien UV o índices)
    ' - Precomputa:
    '   * VertexToTriangles (adjacencia)
    '   * Derivadas UV por triángulo (du1,dv1,du2,dv2,det)
    ' =========================================================================
    Public Shared Function BuildTBNCache(ByRef Uvs_Weight() As Vector3, ByVal indices As UInteger()) As TBNCache
        Dim nVerts As Integer = Uvs_Weight.Length
        Dim triCount As Integer = indices.Length \ 3
        Dim v2t As List(Of Integer)() = New List(Of Integer)(nVerts - 1) {}
        For v = 0 To nVerts - 1
            v2t(v) = New List(Of Integer)(8)
        Next

        ' Derivadas UV por tri
        Dim du1(triCount - 1) As Double
        Dim dv1(triCount - 1) As Double
        Dim du2(triCount - 1) As Double
        Dim dv2(triCount - 1) As Double
        Dim det(triCount - 1) As Double

        For t As Integer = 0 To triCount - 1
            Dim i0 As Integer = CInt(indices(3 * t + 0))
            Dim i1 As Integer = CInt(indices(3 * t + 1))
            Dim i2 As Integer = CInt(indices(3 * t + 2))

            If i0 >= nVerts OrElse i1 >= nVerts OrElse i2 >= nVerts Then Continue For
            v2t(i0).Add(t)
            v2t(i1).Add(t)
            v2t(i2).Add(t)


            ' UV del tri
            Dim uv0 As Vector3 = Uvs_Weight(i0)
            Dim uv1 As Vector3 = Uvs_Weight(i1)
            Dim uv2 As Vector3 = Uvs_Weight(i2)

            Dim _du1 As Double = uv1.X - uv0.X
            Dim _dv1 As Double = uv1.Y - uv0.Y
            Dim _du2 As Double = uv2.X - uv0.X
            Dim _dv2 As Double = uv2.Y - uv0.Y

            du1(t) = _du1 : dv1(t) = _dv1
            du2(t) = _du2 : dv2(t) = _dv2
            det(t) = _du1 * _dv2 - _du2 * _dv1
        Next

        Return New TBNCache With {
            .Indices = indices,
            .TriCount = triCount,
            .VertexToTriangles = v2t,
            .Tri_du1 = du1, .Tri_dv1 = dv1,
            .Tri_du2 = du2, .Tri_dv2 = dv2,
            .Tri_det = det
        }
    End Function

    ' ===========================================================================================
    ' API PÚBLICA: Recalcular N/T/B SOLO para la clausura afectada (dirty + sus triángulos)
    ' - Usa el cache (adjacencia + UV-derivs). Welding opcional (NO cacheado).
    ' ===========================================================================================
    Public Shared Function RecalculateNormalsTangentsBitangents(ByRef geo As SkinnedGeometry, ByVal opts As TBNOptions) As HashSet(Of Integer)
        If IsNothing(geo.CachedTBN.Indices) Then
            geo.CachedTBN = BuildTBNCache(geo.Uvs_Weight, geo.Indices)
        End If
        Dim nVerts As Integer = geo.Vertices.Length

        Dim Vertices_Adicionales As New HashSet(Of Integer)
        If nVerts = 0 OrElse geo.dirtyVertexIndices Is Nothing OrElse geo.dirtyVertexIndices.Count = 0 Then
            Return Vertices_Adicionales ' nada que hacer; si querés todo, pasá todos los índices como dirty
        End If

        ' -------- (Opcional) Welding lógico por posición+UV (NO cacheado) --------
        Dim masterOf() As Integer = Nothing
        Dim membersOf As Dictionary(Of Integer, List(Of Integer)) = Nothing
        If opts.EnableWelding Then
            Vertices_Adicionales.UnionWith(BuildWeldGroups(geo, opts.WeldPosEpsilon, opts.WeldUVEpsilon, opts.WeldByPositionOnly, masterOf, membersOf))
        Else
            masterOf = New Integer(nVerts - 1) {}
            membersOf = New Dictionary(Of Integer, List(Of Integer))(nVerts)
            For i As Integer = 0 To nVerts - 1
                masterOf(i) = i
                membersOf(i) = New List(Of Integer)(1) From {i}
            Next
        End If

        ' -------- 1) Triángulos afectados via adjacencia --------
        Dim affectedTris As New HashSet(Of Integer)()
        For Each vi In geo.dirtyVertexIndices
            If vi < 0 OrElse vi >= nVerts Then Continue For
            Dim triList As List(Of Integer) = geo.CachedTBN.VertexToTriangles(vi)
            For Each t In triList
                affectedTris.Add(t)
            Next
        Next
        If affectedTris.Count = 0 Then Return Vertices_Adicionales

        ' -------- 2) Clausura de vértices a actualizar (incluye grupos por maestro si hay welding) --------
        Dim affectedVerts As New HashSet(Of Integer)(geo.dirtyVertexIndices)
        For Each t In affectedTris
            Dim i0 As Integer = CInt(geo.CachedTBN.Indices(3 * t + 0))
            Dim i1 As Integer = CInt(geo.CachedTBN.Indices(3 * t + 1))
            Dim i2 As Integer = CInt(geo.CachedTBN.Indices(3 * t + 2))
            affectedVerts.Add(i0) : affectedVerts.Add(i1) : affectedVerts.Add(i2)
            affectedVerts.Add(masterOf(i0)) : affectedVerts.Add(masterOf(i1)) : affectedVerts.Add(masterOf(i2))
            Vertices_Adicionales.Add(i0)
            Vertices_Adicionales.Add(i1)
            Vertices_Adicionales.Add(i2)
            Vertices_Adicionales.Add(masterOf(i0))
            Vertices_Adicionales.Add(masterOf(i1))
            Vertices_Adicionales.Add(masterOf(i2))
        Next

        ' -------- 3) Acumuladores: sparse cuando el update es parcial, full cuando es masivo --------
        Dim useFullArrays As Boolean = (affectedTris.Count > geo.CachedTBN.TriCount * 0.4)
        Dim nAccum() As Vector3d = Nothing
        Dim tAccum() As Vector3d = Nothing
        Dim bAccum() As Vector3d = Nothing
        Dim sparseN As Dictionary(Of Integer, Vector3d) = Nothing
        Dim sparseT As Dictionary(Of Integer, Vector3d) = Nothing
        Dim sparseB As Dictionary(Of Integer, Vector3d) = Nothing

        If useFullArrays Then
            nAccum = New Vector3d(nVerts - 1) {}
            tAccum = New Vector3d(nVerts - 1) {}
            bAccum = New Vector3d(nVerts - 1) {}
        Else
            Dim capacity = affectedVerts.Count
            sparseN = New Dictionary(Of Integer, Vector3d)(capacity)
            sparseT = New Dictionary(Of Integer, Vector3d)(capacity)
            sparseB = New Dictionary(Of Integer, Vector3d)(capacity)
        End If

        ' -------- 4) Accumulate per-face contributions --------
        ' Parallel when triangle count is large enough to amortize overhead.
        ' Each thread accumulates into thread-local dictionaries, then merged.
        Dim triArray = affectedTris.ToArray()
        Dim useAngle As Boolean = (opts.WeightMode <> NormalWeightMode.AreaOnly)
        Dim epsPos As Double = opts.EpsilonPos
        Dim epsUV As Double = opts.EpsilonUV
        Dim wMode As NormalWeightMode = opts.WeightMode
        Dim localIndices = geo.CachedTBN.Indices
        Dim localVerts = geo.Vertices
        Dim localDu1 = geo.CachedTBN.Tri_du1
        Dim localDv1 = geo.CachedTBN.Tri_dv1
        Dim localDu2 = geo.CachedTBN.Tri_du2
        Dim localDv2 = geo.CachedTBN.Tri_dv2
        Dim localDet = geo.CachedTBN.Tri_det
        Dim localMasterOf = masterOf

        If useFullArrays AndAlso triArray.Length >= 2000 Then
            ' Parallel path: per-thread local arrays via ThreadLocal, merge at end
            Dim x1 = New Vector3d(nVerts - 1) {}
            Dim x2 = New Vector3d(nVerts - 1) {}
            Dim x3 = New Vector3d(nVerts - 1) {}
            Dim threadLocalN As New Threading.ThreadLocal(Of Vector3d())(Function() x1, trackAllValues:=True)
            Dim threadLocalT As New Threading.ThreadLocal(Of Vector3d())(Function() x2, trackAllValues:=True)
            Dim threadLocalB As New Threading.ThreadLocal(Of Vector3d())(Function() x3, trackAllValues:=True)

            Parallel.ForEach(Partitioner.Create(0, triArray.Length),
                Sub(range As Tuple(Of Integer, Integer))
                    Dim tlN = threadLocalN.Value
                    Dim tlT = threadLocalT.Value
                    Dim tlB = threadLocalB.Value
                    For ti = range.Item1 To range.Item2 - 1
                        AccumulateTriangle(triArray(ti), localIndices, localVerts, localMasterOf,
                                           localDu1, localDv1, localDu2, localDv2, localDet,
                                           useAngle, wMode, epsPos, epsUV,
                                           tlN, tlT, tlB)
                    Next
                End Sub)

            ' Merge thread-local arrays into nAccum — only touch affected master vertices
            For Each tlN In threadLocalN.Values
                For Each vi In affectedVerts
                    Dim m = localMasterOf(vi)
                    nAccum(m) += tlN(m)
                Next
            Next
            For Each tlT In threadLocalT.Values
                For Each vi In affectedVerts
                    Dim m = localMasterOf(vi)
                    tAccum(m) += tlT(m)
                Next
            Next
            For Each tlB In threadLocalB.Values
                For Each vi In affectedVerts
                    Dim m = localMasterOf(vi)
                    bAccum(m) += tlB(m)
                Next
            Next

            threadLocalN.Dispose()
            threadLocalT.Dispose()
            threadLocalB.Dispose()
        Else
            ' Sequential path: direct accumulation (full arrays or sparse)
            For Each t In triArray
                If useFullArrays Then
                    AccumulateTriangle(t, localIndices, localVerts, localMasterOf,
                                       localDu1, localDv1, localDu2, localDv2, localDet,
                                       useAngle, wMode, epsPos, epsUV,
                                       nAccum, tAccum, bAccum)
                Else
                    AccumulateTriangleSparse(t, localIndices, localVerts, localMasterOf,
                                             localDu1, localDv1, localDu2, localDv2, localDet,
                                             useAngle, wMode, epsPos, epsUV,
                                             sparseN, sparseT, sparseB)
                End If
            Next
        End If

        ' -------- 5) Finalize masters and propagate to all group members --------
        Dim candidates As New HashSet(Of Integer)()
        For Each vi In affectedVerts
            candidates.Add(localMasterOf(vi))
        Next

        For Each m As Integer In candidates
            Dim NX As Vector3d = Nothing
            Dim TX As Vector3d = Nothing
            Dim Tb As Vector3d = Nothing
            If useFullArrays = False Then If sparseN.TryGetValue(m, NX) = False Then NX = Vector3d.Zero
            If useFullArrays = False Then If sparseT.TryGetValue(m, TX) = False Then TX = Vector3d.Zero
            If useFullArrays = False Then If sparseB.TryGetValue(m, Tb) = False Then Tb = Vector3d.Zero

            Dim N As Vector3d = If(useFullArrays, nAccum(m), NX)
            Dim T As Vector3d = If(useFullArrays, tAccum(m), TX)
            Dim B As Vector3d = If(useFullArrays, bAccum(m), Tb)

            ' Normal
            If N.LengthSquared <= epsPos OrElse HasNaN(N) Then
                N = New Vector3d(0, 0, 1)
            ElseIf opts.NormalizeOutputs Then
                N = Vector3d.Normalize(N)
            End If

            ' Tangent: Gram-Schmidt orthogonalization against N
            T -= N * Vector3d.Dot(N, T)
            If T.LengthSquared <= epsPos OrElse HasNaN(T) Then
                T = OrthonormalTangentFromNormal(N)
            ElseIf opts.NormalizeOutputs Then
                T = Vector3d.Normalize(T)
            End If

            ' Bitangent: preserve handedness from accumulated B
            Dim Bcross As Vector3d = Vector3d.Cross(N, T)
            Dim s As Double = 1.0
            Dim Bproj As Vector3d = B - N * Vector3d.Dot(N, B)
            If Not HasNaN(Bproj) AndAlso Bproj.LengthSquared > epsPos Then
                If Vector3d.Dot(Bcross, Bproj) < 0.0 Then s = -1.0
            End If

            If opts.ForceOrthogonalBitangent Then
                B = Bcross * s
            Else
                B -= N * Vector3d.Dot(N, B)
                If B.LengthSquared <= epsPos OrElse HasNaN(B) Then
                    B = Bcross * s
                End If
            End If

            If opts.NormalizeOutputs AndAlso B.LengthSquared > epsPos Then
                B = Vector3d.Normalize(B)
            End If

            If opts.RepairNaNs Then
                If HasNaN(B) Then B = Bcross * s
            End If

            ' Propagate to all members of the weld group
            ' FO4 convention (uniform for both FO4 and SSE): T->geo.Tangents, B->geo.Bitangents.
            ' T/B swap for SSE NIF format is handled at ExtractSkinnedGeometry / InjectToTrishape boundaries.
            Dim members As List(Of Integer) = Nothing
            If membersOf.TryGetValue(m, members) Then
                For Each vi As Integer In members
                    geo.Normals(vi) = N
                    geo.Tangents(vi) = T
                    geo.Bitangents(vi) = B
                Next
            Else
                geo.Normals(m) = N
                geo.Tangents(m) = T
                geo.Bitangents(m) = B
            End If
        Next
        Return Vertices_Adicionales
    End Function

    ' -----------------------
    ' Utilitarios privados
    ' -----------------------

    ' Welding lógico por posición+UV con tolerancias (NO cacheado)
    Private Shared Function BuildWeldGroups(ByRef geo As SkinnedGeometry, ByVal weldPosEpsOrig As Double, ByVal weldUVEps As Double, ByVal byPosOnly As Boolean, ByRef masterOf() As Integer, ByRef membersOf As Dictionary(Of Integer, List(Of Integer))) As HashSet(Of Integer)
        Dim n As Integer = geo.Vertices.Length
        Dim vertices_adicionales As New HashSet(Of Integer)
        masterOf = New Integer(n - 1) {}
        membersOf = New Dictionary(Of Integer, List(Of Integer))(n)
        Dim extent As Vector3d = geo.Maxv - geo.Minv
        Dim diag As Double = extent.Length
        Dim maxSpan As Double = Math.Max(Math.Max(Math.Abs(extent.X), Math.Abs(extent.Y)), Math.Abs(extent.Z))
        ' Heurística de epsilon relativo (elegí uno de los dos L)
        Dim L As Double = If(diag > 0.0, diag, maxSpan)
        ' Parámetros de control (ajustables)
        Dim k As Double = weldPosEpsOrig     ' fracción de la escala de la malla
        Dim floorEps As Double = 0.000000000001
        Dim ceilEps As Double = 0.001   ' evita sobre-soldar en mallas gigantes

        Dim weldPosEps As Double
        If L <= 0.0 Then
            weldPosEps = floorEps
        Else
            weldPosEps = Math.Max(floorEps, Math.Min(ceilEps, k * L))
        End If

        If weldPosEps <= 0 OrElse (Not byPosOnly AndAlso weldUVEps <= 0) OrElse n = 0 Then
            For i As Integer = 0 To n - 1
                masterOf(i) = i
                membersOf(i) = New List(Of Integer)(1) From {i}
            Next
            Return vertices_adicionales
        End If

        ' Hash buckets por celda cuantizada
        Dim buckets As New Dictionary(Of WeldKey, List(Of Integer))(n)

        For i As Integer = 0 To n - 1
            Dim p As Vector3d = geo.Vertices(i)
            Dim uv As Vector3 = geo.Uvs_Weight(i)

            ' Clave cuantizada por tolerancia (redondeo a celda)
            Dim key As WeldKey = WeldKey.From(p, uv, weldPosEps, weldUVEps, byPosOnly)

            Dim list As List(Of Integer) = Nothing
            If Not buckets.TryGetValue(key, list) Then
                list = New List(Of Integer)()
                buckets(key) = list
            End If

            ' Buscar en el bucket si ya existe un maestro compatible (chequeo fino)
            Dim assigned As Boolean = False
            For Each cand As Integer In list.ToList
                Dim posOk As Boolean = ClosePos(geo.Vertices(cand), p, weldPosEps)
                Dim uvOk As Boolean = byPosOnly OrElse CloseUV(geo.Uvs_Weight(cand), uv, weldUVEps)
                If posOk AndAlso uvOk Then
                    masterOf(i) = masterOf(cand)
                    membersOf(masterOf(cand)).Add(i)
                    list.Add(i)
                    vertices_adicionales.Add(i)
                    assigned = True
                    Exit For
                End If
            Next

            If Not assigned Then
                ' Nuevo grupo con i como maestro
                masterOf(i) = i
                list.Add(i)
                membersOf(i) = New List(Of Integer)(4) From {i}
            End If
        Next
        Return vertices_adicionales
    End Function


    ' Clave de bucket (cuantización por eps)
    Private Structure WeldKey
        Public qx As Long, qy As Long, qz As Long
        Public qu As Long, qv As Long

        Public Shared Function From(p As Vector3d, uv As Vector3, posEps As Double, uvEps As Double, byPosOnly As Boolean) As WeldKey
            Dim invPos As Double = If(posEps > 0.0, 1.0 / posEps, 0.0)
            Dim invUV As Double = If(uvEps > 0.0, 1.0 / uvEps, 0.0)

            Dim k As WeldKey
            k.qx = QuantizeToLong(p.X, invPos)
            k.qy = QuantizeToLong(p.Y, invPos)
            k.qz = QuantizeToLong(p.Z, invPos)
            If byPosOnly Then
                k.qu = 0 : k.qv = 0
            Else
                k.qu = QuantizeToLong(uv.X, invUV)
                k.qv = QuantizeToLong(uv.Y, invUV)
            End If
            Return k
        End Function

        Private Shared Function QuantizeToLong(val As Double, invStep As Double) As Long
            If invStep <= 0.0 Then Return 0
            If Double.IsNaN(val) OrElse Double.IsInfinity(val) Then Return 0
            Dim q As Double = Math.Round(val * invStep)
            Const LMAX As Double = 9.2233720368547758E+18
            Const LMIN As Double = -9.2233720368547758E+18
            If q > LMAX Then Return Long.MaxValue
            If q < LMIN Then Return Long.MinValue
            Return CLng(q)
        End Function

        Public Overrides Function GetHashCode() As Integer
            ' versión segura (sin overflow)
            Dim hc As New HashCode()
            hc.Add(qx) : hc.Add(qy) : hc.Add(qz) : hc.Add(qu) : hc.Add(qv)
            Return hc.ToHashCode()
        End Function

        Public Overrides Function Equals(obj As Object) As Boolean
            If TypeOf obj IsNot WeldKey Then Return False
            Dim o As WeldKey = CType(obj, WeldKey)
            Return qx = o.qx AndAlso qy = o.qy AndAlso qz = o.qz AndAlso qu = o.qu AndAlso qv = o.qv
        End Function
    End Structure

    ' Comparación fina por componente (posición)
    Private Shared Function ClosePos(a As Vector3d, b As Vector3d, eps As Double) As Boolean
        Return Math.Abs(a.X - b.X) <= eps AndAlso Math.Abs(a.Y - b.Y) <= eps AndAlso Math.Abs(a.Z - b.Z) <= eps
    End Function

    ' Comparación fina por componente (UV)
    Private Shared Function CloseUV(a As Vector3, b As Vector3, eps As Double) As Boolean
        Return Math.Abs(a.X - b.X) <= eps AndAlso Math.Abs(a.Y - b.Y) <= eps
    End Function

    ' Ángulo seguro entre a y b (radianes); 0 si degenerado.
    ' Uses Atan2(|cross|, dot) instead of Acos(dot) for better numerical stability near 0° and 180°.
    Private Shared Function AngleBetweenSafe(a As Vector3d, b As Vector3d, eps As Double) As Double
        Dim crossVec = Vector3d.Cross(a, b)
        Dim sinVal = crossVec.Length
        Dim cosVal = Vector3d.Dot(a, b)
        If sinVal <= eps AndAlso Math.Abs(cosVal) <= eps Then Return 0.0
        Return Math.Atan2(sinVal, cosVal)
    End Function

    ' Core per-triangle accumulation logic — extracted to avoid duplication between sequential/parallel paths.
    Private Shared Sub AccumulateTriangle(t As Integer,
                                          indices As UInteger(), verts As Vector3d(), masterOf As Integer(),
                                          du1 As Double(), dv1 As Double(), du2 As Double(), dv2 As Double(), det As Double(),
                                          useAngle As Boolean, wMode As NormalWeightMode, epsPos As Double, epsUV As Double,
                                          nAcc As Vector3d(), tAcc As Vector3d(), bAcc As Vector3d())
        Dim i0 As Integer = CInt(indices(3 * t)), i1 As Integer = CInt(indices(3 * t + 1)), i2 As Integer = CInt(indices(3 * t + 2))
        Dim m0 = masterOf(i0), m1 = masterOf(i1), m2 = masterOf(i2)
        Dim p0 = verts(i0), p1 = verts(i1), p2 = verts(i2)
        Dim e1 = p1 - p0, e2 = p2 - p0
        Dim fn = Vector3d.Cross(e1, e2)
        Dim area2 = fn.Length
        If area2 <= epsPos Then Exit Sub

        Dim wn0 As Double, wn1 As Double, wn2 As Double
        If useAngle Then
            Dim w0 = AngleBetweenSafe(e1, e2, epsPos)
            Dim w1 = AngleBetweenSafe(p0 - p1, p2 - p1, epsPos)
            Dim w2 = AngleBetweenSafe(p0 - p2, p1 - p2, epsPos)
            If wMode = NormalWeightMode.AngleOnly Then
                wn0 = w0 : wn1 = w1 : wn2 = w2
            Else ' AreaTimesAngle
                wn0 = area2 * w0 : wn1 = area2 * w1 : wn2 = area2 * w2
            End If
        Else ' AreaOnly
            wn0 = area2 : wn1 = area2 : wn2 = area2
        End If

        Dim tFace As Vector3d, bFace As Vector3d
        ComputeFaceTB(fn, e1, e2, du1(t), dv1(t), du2(t), dv2(t), det(t), epsPos, epsUV, tFace, bFace)

        nAcc(m0) += fn * wn0 : nAcc(m1) += fn * wn1 : nAcc(m2) += fn * wn2
        tAcc(m0) += tFace * wn0 : tAcc(m1) += tFace * wn1 : tAcc(m2) += tFace * wn2
        bAcc(m0) += bFace * wn0 : bAcc(m1) += bFace * wn1 : bAcc(m2) += bFace * wn2
    End Sub

    ' Sparse variant for small partial updates — avoids allocating full-size arrays.
    Private Shared Sub AccumulateTriangleSparse(t As Integer,
                                                indices As UInteger(), verts As Vector3d(), masterOf As Integer(),
                                                du1 As Double(), dv1 As Double(), du2 As Double(), dv2 As Double(), det As Double(),
                                                useAngle As Boolean, wMode As NormalWeightMode, epsPos As Double, epsUV As Double,
                                                nAcc As Dictionary(Of Integer, Vector3d),
                                                tAcc As Dictionary(Of Integer, Vector3d),
                                                bAcc As Dictionary(Of Integer, Vector3d))
        Dim i0 As Integer = CInt(indices(3 * t)), i1 As Integer = CInt(indices(3 * t + 1)), i2 As Integer = CInt(indices(3 * t + 2))
        Dim m0 = masterOf(i0), m1 = masterOf(i1), m2 = masterOf(i2)
        Dim p0 = verts(i0), p1 = verts(i1), p2 = verts(i2)
        Dim e1 = p1 - p0, e2 = p2 - p0
        Dim fn = Vector3d.Cross(e1, e2)
        Dim area2 = fn.Length
        If area2 <= epsPos Then Exit Sub

        Dim wn0 As Double, wn1 As Double, wn2 As Double
        If useAngle Then
            Dim w0 = AngleBetweenSafe(e1, e2, epsPos)
            Dim w1 = AngleBetweenSafe(p0 - p1, p2 - p1, epsPos)
            Dim w2 = AngleBetweenSafe(p0 - p2, p1 - p2, epsPos)
            If wMode = NormalWeightMode.AngleOnly Then
                wn0 = w0 : wn1 = w1 : wn2 = w2
            Else
                wn0 = area2 * w0 : wn1 = area2 * w1 : wn2 = area2 * w2
            End If
        Else
            wn0 = area2 : wn1 = area2 : wn2 = area2
        End If

        Dim tFace As Vector3d, bFace As Vector3d
        ComputeFaceTB(fn, e1, e2, du1(t), dv1(t), du2(t), dv2(t), det(t), epsPos, epsUV, tFace, bFace)

        Dim vn0 As Vector3d, vn1 As Vector3d, vn2 As Vector3d
        nAcc.TryGetValue(m0, vn0) : nAcc(m0) = vn0 + fn * wn0
        nAcc.TryGetValue(m1, vn1) : nAcc(m1) = vn1 + fn * wn1
        nAcc.TryGetValue(m2, vn2) : nAcc(m2) = vn2 + fn * wn2
        Dim vt0 As Vector3d, vt1 As Vector3d, vt2 As Vector3d
        tAcc.TryGetValue(m0, vt0) : tAcc(m0) = vt0 + tFace * wn0
        tAcc.TryGetValue(m1, vt1) : tAcc(m1) = vt1 + tFace * wn1
        tAcc.TryGetValue(m2, vt2) : tAcc(m2) = vt2 + tFace * wn2
        Dim vb0 As Vector3d, vb1 As Vector3d, vb2 As Vector3d
        bAcc.TryGetValue(m0, vb0) : bAcc(m0) = vb0 + bFace * wn0
        bAcc.TryGetValue(m1, vb1) : bAcc(m1) = vb1 + bFace * wn1
        bAcc.TryGetValue(m2, vb2) : bAcc(m2) = vb2 + bFace * wn2
    End Sub

    ' Computes per-face tangent and bitangent from edges + cached UV derivatives.
    Private Shared Sub ComputeFaceTB(fn As Vector3d, e1 As Vector3d, e2 As Vector3d,
                                      _du1 As Double, _dv1 As Double, _du2 As Double, _dv2 As Double, _det As Double,
                                      epsPos As Double, epsUV As Double,
                                      ByRef tFace As Vector3d, ByRef bFace As Vector3d)
        If Math.Abs(_det) <= epsUV Then
            ' Degenerate UV: stable fallback in face-normal plane
            Dim nf = Vector3d.Normalize(fn)
            Dim e1p = e1 - nf * Vector3d.Dot(nf, e1)
            If e1p.LengthSquared <= epsPos Then e1p = e2 - nf * Vector3d.Dot(nf, e2)
            If e1p.LengthSquared <= epsPos Then
                tFace = Vector3d.Zero
                bFace = Vector3d.Zero
            Else
                tFace = Vector3d.Normalize(e1p)
                bFace = Vector3d.Normalize(Vector3d.Cross(nf, tFace))
            End If
        Else
            Dim r As Double = 1.0 / _det
            tFace = (e1 * _dv2 - e2 * _dv1) * r
            bFace = (e2 * _du1 - e1 * _du2) * r
        End If
    End Sub

    ' Tangente ortonormal a partir de una normal: elige un eje auxiliar poco alineado
    Private Shared Function OrthonormalTangentFromNormal(n As Vector3d) As Vector3d
        Dim ax As Vector3d = If(Math.Abs(n.X) < 0.9, New Vector3d(1, 0, 0), New Vector3d(0, 1, 0))
        Dim t As Vector3d = Vector3d.Cross(ax, n)
        If t.LengthSquared <= 1.0E-20 Then t = Vector3d.Cross(New Vector3d(0, 0, 1), n)
        If t.LengthSquared <= 1.0E-20 Then Return New Vector3d(1, 0, 0)
        Return Vector3d.Normalize(t)
    End Function

    Private Shared Function HasNaN(v As Vector3d) As Boolean
        Return Double.IsNaN(v.X) OrElse Double.IsNaN(v.Y) OrElse Double.IsNaN(v.Z)
    End Function

End Class
