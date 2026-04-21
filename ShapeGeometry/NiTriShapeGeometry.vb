Imports NiflySharp
Imports NiflySharp.Blocks
Imports NiflySharp.Helpers
Imports NiflySharp.Structs
Imports SysHalf = System.Half
Imports SysNumerics = System.Numerics

''' <summary>
''' IShapeGeometry adapter for the NiTriBasedGeom family (NiTriShape, BSLODTriShape,
''' NiTriStrips).  All three resolve geometry through DataRef → NiTriBasedGeomData
''' (NiTriShapeData for the first two, NiTriStripsData for strips), and all three rely on
''' NiSkinInstance / BSDismemberSkinInstance + NiSkinData + NiSkinPartition for skinning
''' instead of inline BSVertexData.
'''
''' Tangent / Bitangent semantics: applies the same INVERTIDAS swap as BSTriShape
''' (renderer Tangent ⇆ NIF Bitangent, renderer Bitangent ⇆ NIF Tangent).  The renderer
''' was tuned around BSTriShape's convention; Skyrim NiTriShape fields use the same naming
''' convention with the same effective swap relative to FO4 shader expectations, so the
''' adapter applies it uniformly to keep TBN consistent across families.
'''
''' Triangles vs strips: NiTriShapeData stores a triangle list directly.  NiTriStripsData
''' stores triangle strips (compact GPU-strip format).  GetTriangles() converts strips to
''' triangles via NiflySharp.Helpers.IndicesHelper.GenerateTrianglesFromStrips so callers
''' see a uniform triangle list.  SetTriangles() writes back as a triangle list for
''' NiTriShapeData; for NiTriStripsData it emits "degenerate strips" (one 3-point strip
''' per triangle), which is the simplest tri→strip encoding that survives round-trip
''' (no stripification heuristic, no GPU-batching benefit, but byte-perfect read after
''' write).  If the original NIF was carefully strip-optimized that batching information
''' is lost; for our edit/save use case this is acceptable.
'''
''' Skinning extraction strategy:
'''   1. Prefer NiSkinPartition (GPU-friendly per-vertex layout).  Walks each partition,
'''      maps partition-local vertex indices through VertexMap to global vertex indices and
'''      partition-local bone indices through Partitions[p].Bones to shape-level bone palette
'''      indices.
'''   2. Fallback to NiSkinData.BoneList[i].VertexWeights (per-bone {vertexIdx, weight} list)
'''      when no partition is present (legacy / unbuilt meshes).  Each vertex collects up to
'''      4 influences sorted by descending weight.
''' Output is the same flat 4-slot-per-vertex format the BSTriShape adapter emits, so the
''' downstream skinning math in SkinningHelper does not branch by adapter.
'''
''' Writeback: SetVertexPositions / SetTriangles / etc. mutate the NiTriBasedGeomData block.
''' The skin partition is NOT touched here — caller must invoke
''' Nifcontent_Class_Manolo.UpdateSkinPartitions(shape) after structural changes (same
''' contract as BSTriShape).  BSLODTriShape's LOD0/1/2 sizes are preserved verbatim; this
''' adapter does not regenerate them when the triangle list size changes (out of scope —
''' LOD redistribution would need a heuristic).
''' </summary>
Public Class NiTriShapeGeometry
    Implements IShapeGeometry

    Private ReadOnly _shape As INiShape
    Private ReadOnly _nif As Nifcontent_Class_Manolo

    Public Sub New(shape As INiShape, nif As Nifcontent_Class_Manolo)
        If shape Is Nothing Then Throw New ArgumentNullException(NameOf(shape))
        If nif Is Nothing Then Throw New ArgumentNullException(NameOf(nif))
        If TypeOf shape IsNot NiTriShape AndAlso TypeOf shape IsNot BSLODTriShape AndAlso TypeOf shape IsNot NiTriStrips Then
            Throw New ArgumentException(
                $"NiTriShapeGeometry only wraps NiTriShape / BSLODTriShape / NiTriStrips (got {shape.GetType().Name})",
                NameOf(shape))
        End If
        _shape = shape
        _nif = nif
    End Sub

    ''' <summary>
    ''' Resolves the geometry data block via DataRef.  Returns either a NiTriShapeData (for
    ''' NiTriShape / BSLODTriShape) or a NiTriStripsData (for NiTriStrips) — both inherit
    ''' NiTriBasedGeomData so the read code paths share the base.  Returns Nothing if the
    ''' DataRef is empty/invalid.
    ''' </summary>
    Private Function GetData() As NiTriBasedGeomData
        If _shape.DataRef Is Nothing OrElse _shape.DataRef.Index = -1 Then Return Nothing
        If _shape.DataRef.Index >= _nif.Blocks.Count Then Return Nothing
        Return TryCast(_nif.Blocks(CInt(_shape.DataRef.Index)), NiTriBasedGeomData)
    End Function

    ' ─────────────── Identity ───────────────
    Public ReadOnly Property BackingShape As INiShape Implements IShapeGeometry.BackingShape
        Get
            Return _shape
        End Get
    End Property

    Public ReadOnly Property Name As String Implements IShapeGeometry.Name
        Get
            Return If(_shape?.Name?.String, "")
        End Get
    End Property

    Public ReadOnly Property Version As NiVersion Implements IShapeGeometry.Version
        Get
            Return _nif.Header.Version
        End Get
    End Property

    ' ─────────────── Counts and presence ───────────────
    Public ReadOnly Property VertexCount As Integer Implements IShapeGeometry.VertexCount
        Get
            Dim d = GetData()
            Return If(d Is Nothing, 0, CInt(d.NumVertices))
        End Get
    End Property

    Public ReadOnly Property TriangleCount As Integer Implements IShapeGeometry.TriangleCount
        Get
            Dim d = GetData()
            Return If(d Is Nothing, 0, d.NumTriangles)
        End Get
    End Property

    Public ReadOnly Property HasNormals As Boolean Implements IShapeGeometry.HasNormals
        Get
            Dim d = GetData()
            Return d IsNot Nothing AndAlso d.HasNormals
        End Get
    End Property

    Public ReadOnly Property HasTangents As Boolean Implements IShapeGeometry.HasTangents
        Get
            Dim d = GetData()
            Return d IsNot Nothing AndAlso d.HasTangents
        End Get
    End Property

    Public ReadOnly Property HasUVs As Boolean Implements IShapeGeometry.HasUVs
        Get
            Dim d = GetData()
            Return d IsNot Nothing AndAlso d.HasUVs
        End Get
    End Property

    Public ReadOnly Property HasVertexColors As Boolean Implements IShapeGeometry.HasVertexColors
        Get
            Dim d = GetData()
            Return d IsNot Nothing AndAlso d.HasVertexColors
        End Get
    End Property

    ''' <summary>
    ''' Eye data is FO4 BSTriShape-specific (face shape eye scleral mask).  NiTriShape /
    ''' BSLODTriShape have no equivalent — always False.
    ''' </summary>
    Public ReadOnly Property HasEyeData As Boolean Implements IShapeGeometry.HasEyeData
        Get
            Return False
        End Get
    End Property

    Public ReadOnly Property IsSkinned As Boolean Implements IShapeGeometry.IsSkinned
        Get
            Return _shape.IsSkinned
        End Get
    End Property

    Public ReadOnly Property Bounds As BoundingSphere Implements IShapeGeometry.Bounds
        Get
            Dim d = GetData()
            If d Is Nothing Then Return New BoundingSphere(SysNumerics.Vector3.Zero, 0)
            Return d.Bounds
        End Get
    End Property

    ' ─────────────── Read ───────────────
    Public Function GetVertexPositions() As List(Of SysNumerics.Vector3) Implements IShapeGeometry.GetVertexPositions
        Dim d = GetData()
        If d Is Nothing OrElse d.Vertices Is Nothing Then Return New List(Of SysNumerics.Vector3)()
        Return d.Vertices.ToList()
    End Function

    Public Function GetNormals() As List(Of SysNumerics.Vector3) Implements IShapeGeometry.GetNormals
        Dim d = GetData()
        If d Is Nothing OrElse Not d.HasNormals OrElse d.Normals Is Nothing Then
            Return New List(Of SysNumerics.Vector3)()
        End If
        Return d.Normals.ToList()
    End Function

    ''' <summary>
    ''' Renderer-convention tangent.  Comes from NIF Bitangents (INVERTIDAS swap) — same
    ''' convention enforced for BSTriShape.
    ''' </summary>
    Public Function GetTangents() As List(Of SysNumerics.Vector3) Implements IShapeGeometry.GetTangents
        Dim d = GetData()
        If d Is Nothing OrElse Not d.HasTangents OrElse d.Bitangents Is Nothing Then
            Return New List(Of SysNumerics.Vector3)()
        End If
        Return d.Bitangents.ToList()
    End Function

    ''' <summary>
    ''' Renderer-convention bitangent.  Comes from NIF Tangents (INVERTIDAS swap) — same
    ''' convention enforced for BSTriShape.
    ''' </summary>
    Public Function GetBitangents() As List(Of SysNumerics.Vector3) Implements IShapeGeometry.GetBitangents
        Dim d = GetData()
        If d Is Nothing OrElse Not d.HasTangents OrElse d.Tangents Is Nothing Then
            Return New List(Of SysNumerics.Vector3)()
        End If
        Return d.Tangents.ToList()
    End Function

    Public Function GetUVs() As List(Of TexCoord) Implements IShapeGeometry.GetUVs
        Dim d = GetData()
        If d Is Nothing OrElse Not d.HasUVs OrElse d.UVSets Is Nothing Then
            Return New List(Of TexCoord)()
        End If
        Return d.UVSets.ToList()
    End Function

    Public Function GetVertexColors() As List(Of Color4) Implements IShapeGeometry.GetVertexColors
        Dim d = GetData()
        If d Is Nothing OrElse Not d.HasVertexColors OrElse d.VertexColors Is Nothing Then
            Return New List(Of Color4)()
        End If
        Return d.VertexColors.ToList()
    End Function

    Public Function GetEyeData() As List(Of Single) Implements IShapeGeometry.GetEyeData
        Return New List(Of Single)()
    End Function

    Public Function GetTriangles() As List(Of Triangle) Implements IShapeGeometry.GetTriangles
        Dim d = GetData()
        If d Is Nothing Then Return New List(Of Triangle)()

        ' NiTriShapeData / BSLODTriShape data: triangle list lives on the data block directly.
        Dim triData = TryCast(d, NiTriShapeData)
        If triData IsNot Nothing Then
            If triData.Triangles Is Nothing Then Return New List(Of Triangle)()
            Return triData.Triangles.ToList()
        End If

        ' NiTriStripsData: strips → triangle list via NiflySharp's helper.  Strip data lives
        ' in protected fields (_points / _stripLengths) — read via reflection (same pattern
        ' as NifContent_Class.GetTriParts on NiSkinPartition).
        Dim stripData = TryCast(d, NiTriStripsData)
        If stripData IsNot Nothing Then
            Dim strips = ReadStrips(stripData)
            If strips Is Nothing OrElse strips.Count = 0 Then Return New List(Of Triangle)()
            Return IndicesHelper.GenerateTrianglesFromStrips(strips)
        End If

        Return New List(Of Triangle)()
    End Function

    Public Function GetSkinning() As ShapeSkinningData Implements IShapeGeometry.GetSkinning
        If Not _shape.IsSkinned Then Return ShapeSkinningData.Empty

        Dim skinInst = ResolveSkinInstance()
        If skinInst Is Nothing Then Return ShapeSkinningData.Empty

        Dim n As Integer = VertexCount
        If n = 0 Then Return ShapeSkinningData.Empty

        Const wpv As Integer = 4
        Dim outIdx(n * wpv - 1) As Byte
        Dim outWgt(n * wpv - 1) As SysHalf

        ' Prefer the NiSkinPartition path (GPU layout).  Falls back to NiSkinData when missing.
        Dim skinPart = ResolvePartition(skinInst)
        If skinPart IsNot Nothing AndAlso skinPart.Partitions IsNot Nothing AndAlso skinPart.Partitions.Count > 0 Then
            FillFromPartition(skinPart, outIdx, outWgt, n, wpv)
        Else
            Dim skinData = ResolveSkinData(skinInst)
            If skinData Is Nothing OrElse skinData.BoneList Is Nothing Then Return ShapeSkinningData.Empty
            FillFromSkinData(skinData, outIdx, outWgt, n, wpv)
        End If

        Return New ShapeSkinningData With {
            .BoneIndices = outIdx,
            .BoneWeights = outWgt,
            .WeightsPerVertex = wpv,
            .VertexCount = n,
            .BoneRefIndices = If(skinInst.Bones?.Indices?.Select(Function(i) CInt(i))?.ToArray(), Array.Empty(Of Integer)())
        }
    End Function

    ' ─────────────── Write ───────────────
    ''' <summary>
    ''' Establishes the vertex count on the underlying NiTriShapeData / NiTriStripsData by
    ''' writing a resized Vertices list (NiGeometryData.Vertices setter sets _numVertices
    ''' and HasVertices atomically).  Subsequent per-field setters (SetNormals, SetTangents,
    ''' SetBitangents, SetUVs, SetVertexColors) resize their arrays lazily when their
    ''' setter is called.
    '''
    ''' No-op if already at the requested size.  NiSkinData.BoneList[].VertexWeights
    ''' indexes vertices — caller MUST also call SetSkinning post-resize to remap/rebuild
    ''' skin (otherwise BoneList references stale vertex indices and UpdateSkinPartitions
    ''' produces a corrupt partition).
    ''' </summary>
    Public Sub ResizeVertices(vertexCount As Integer) Implements IShapeGeometry.ResizeVertices
        If vertexCount < 0 Then Throw New ArgumentOutOfRangeException(NameOf(vertexCount), "Negative vertex count")
        If vertexCount > UShort.MaxValue Then
            Throw New InvalidOperationException(
                $"ResizeVertices: {vertexCount} exceeds NiGeometryData _numVertices UShort limit (65535).")
        End If
        Dim d = GetData()
        If d Is Nothing Then Return
        If CInt(d.NumVertices) = vertexCount Then Return

        ' Resize Vertices via the setter (auto-updates _numVertices, HasVertices).  Other
        ' per-vertex arrays (Normals, Tangents, Bitangents, UVSets, VertexColors) will be
        ' resized on their individual setters via NiGeometryData.HasNormals=true side
        ' effects.  See NiGeometryData.cs:53-77.
        Dim fresh As New List(Of SysNumerics.Vector3)(vertexCount)
        For i = 0 To vertexCount - 1 : fresh.Add(SysNumerics.Vector3.Zero) : Next
        d.Vertices = fresh
    End Sub

    ' NOTE on null guards: NiflySharp's NiGeometryData setters do `int count = value.Count`
    ' unconditionally and throw NullReferenceException when `value` is null (observed on a
    ' LE daedric gauntlet NIF with no vertex colors).  Our adapter guards against that —
    ' treating `Nothing` as "caller has no data for this field, don't write".  This matches
    ' ApplyShapeGeometry's own `IsNot Nothing AndAlso HasX` pattern so callers that already
    ' gate properly are unaffected.

    Public Sub SetVertexPositions(positions As List(Of SysNumerics.Vector3)) Implements IShapeGeometry.SetVertexPositions
        Dim d = GetData()
        If d Is Nothing OrElse positions Is Nothing Then Return
        d.Vertices = positions
    End Sub

    Public Sub SetNormals(normals As List(Of SysNumerics.Vector3)) Implements IShapeGeometry.SetNormals
        Dim d = GetData()
        If d Is Nothing OrElse normals Is Nothing Then Return
        d.Normals = normals
    End Sub

    ''' <summary>
    ''' Writes renderer-convention tangents back to NIF Bitangents (INVERTIDAS swap).
    ''' </summary>
    Public Sub SetTangents(tangents As List(Of SysNumerics.Vector3)) Implements IShapeGeometry.SetTangents
        Dim d = GetData()
        If d Is Nothing OrElse tangents Is Nothing Then Return
        d.Bitangents = tangents
    End Sub

    ''' <summary>
    ''' Writes renderer-convention bitangents back to NIF Tangents (INVERTIDAS swap).
    ''' </summary>
    Public Sub SetBitangents(bitangents As List(Of SysNumerics.Vector3)) Implements IShapeGeometry.SetBitangents
        Dim d = GetData()
        If d Is Nothing OrElse bitangents Is Nothing Then Return
        d.Tangents = bitangents
    End Sub

    Public Sub SetUVs(uvs As List(Of TexCoord)) Implements IShapeGeometry.SetUVs
        Dim d = GetData()
        If d Is Nothing OrElse uvs Is Nothing Then Return
        d.UVSets = uvs
    End Sub

    Public Sub SetVertexColors(colors As List(Of Color4)) Implements IShapeGeometry.SetVertexColors
        Dim d = GetData()
        If d Is Nothing OrElse colors Is Nothing Then Return
        d.VertexColors = colors
    End Sub

    ''' <summary>
    ''' No-op for NiTriShape / BSLODTriShape — neither supports per-vertex eye data.
    ''' Provided to satisfy the IShapeGeometry contract; callers should gate writes on
    ''' HasEyeData (which is always False here).
    ''' </summary>
    Public Sub SetEyeData(eyeData As List(Of Single)) Implements IShapeGeometry.SetEyeData
        ' intentional no-op
    End Sub

    Public Sub SetTriangles(triangles As List(Of Triangle)) Implements IShapeGeometry.SetTriangles
        SetTriangles(triangles, Nothing)
    End Sub

    Public Sub SetTriangles(triangles As List(Of Triangle), provenance As TriangleRemap) Implements IShapeGeometry.SetTriangles
        Dim d = GetData()
        If d Is Nothing Then Return

        ' Snapshot OLD metadata BEFORE writing — needed for redistribution.
        Dim bsLod = TryCast(_shape, BSLODTriShape)
        Dim bsSeg = TryCast(_shape, BSSegmentedTriShape)
        Dim oldSegments As List(Of BSGeometrySegmentData) = Nothing
        If bsSeg IsNot Nothing Then oldSegments = ReadSegmentedSegments(bsSeg)

        ' Write triangles to the underlying data block (NiTriShapeData triangle list or
        ' NiTriStripsData strip flatten).
        Dim triData = TryCast(d, NiTriShapeData)
        If triData IsNot Nothing Then
            triData.Triangles = triangles
        Else
            Dim stripData = TryCast(d, NiTriStripsData)
            If stripData IsNot Nothing Then
                ' NiTriStripsData round-trip: emit one 3-point strip per triangle (degenerate
                ' encoding — loses GPU strip-batching but preserves geometry exactly).
                Dim strips As New List(Of List(Of UShort))(triangles.Count)
                For Each t In triangles
                    strips.Add(New List(Of UShort) From {t.V1, t.V2, t.V3})
                Next
                WriteStrips(stripData, strips)
            End If
        End If

        ' Redistribute count-derived metadata when provenance was provided.
        If provenance Is Nothing Then Return

        ' ─── BSLODTriShape: lossy collapse to LOD2 ───
        ' Same canonical behaviour as BSMeshLODTriShape (BS-OS Geometry.cpp:1522).
        ' BSLODTriShape uses identical LOD0/1/2Size fields per nif.xml — sibling format
        ' targeted at Skyrim/SSE rather than FO4.
        If bsLod IsNot Nothing Then
            ' DEBUGGER.BREAK: TO TEST — first time a BSLODTriShape goes through provenance-
            ' aware SetTriangles, step through and verify LOD2Size equals new tri count.
            ' Remove after validation against an SSE LOD-mesh sample.  See memory:
            ' pending_tests_shape_metadata.md
            Debugger.Break()
            bsLod.LOD0Size = 0UI
            bsLod.LOD1Size = 0UI
            bsLod.LOD2Size = CUInt(triangles.Count)
            Return
        End If

        ' ─── BSSegmentedTriShape: full Segments + SubSegmentDatas redistribution ───
        ' FO3-era format (predecessor of BSSubIndexTriShape).  Same Segments structure
        ' (List(Of BSGeometrySegmentData)) but lives on a NiTriShape-derived class with
        ' protected _segment field (no public accessor in NiflySharp).  Write back via
        ' reflection — same pattern as the strips fields above.
        If bsSeg IsNot Nothing AndAlso oldSegments IsNot Nothing AndAlso oldSegments.Count > 0 Then
            ' DEBUGGER.BREAK: TO TEST — first time a BSSegmentedTriShape goes through this
            ' path, verify the redistributed Segments match BS-OS canonical behaviour
            ' (Geometry.cpp:1248).  Note: BSSegmented is FO3-era and rare in FO4 vanilla;
            ' WM filter currently blocks it from split/zap until the deeper NiTri-family
            ' refactor lands (see notes in memory).
            Debugger.Break()
            Dim newSegs = BSTriShapeGeometry.RedistributeSegments(oldSegments, provenance, triangles.Count)
            WriteSegmentedSegments(bsSeg, newSegs)
        End If
    End Sub

    Public Sub UpdateBounds() Implements IShapeGeometry.UpdateBounds
        Dim d = GetData()
        If d Is Nothing Then Return
        d.UpdateBounds()
    End Sub

    ''' <summary>
    ''' Rebuilds NiSkinData.BoneList[].VertexWeights from the per-vertex
    ''' <paramref name="skinning"/> data.  This is the inverse of GetSkinning's
    ''' partition-or-skindata read: it pivots from "per-vertex 4-slot" back to "per-bone
    ''' (vertex,weight) list" because that's where NiTriShape skin lives on disk.
    '''
    ''' Skin partition is NOT regenerated here — caller must invoke
    ''' Nifcontent_Class_Manolo.UpdateSkinPartitions(shape) afterwards.  That call reads
    ''' from NiSkinData (for NiTriShape family) to rebuild NiSkinPartition consistently
    ''' with the new per-vertex assignments.
    '''
    ''' Bone palette: skinning.BoneIndices entries are SHAPE-level (they index into
    ''' skinInst.Bones).  Each BoneList index in NiSkinData corresponds to the same shape
    ''' bone palette position.
    ''' </summary>
    Public Sub SetSkinning(skinning As ShapeSkinningData) Implements IShapeGeometry.SetSkinning
        If Not _shape.IsSkinned Then Return
        If skinning.BoneIndices Is Nothing OrElse skinning.BoneWeights Is Nothing Then Return

        Dim skinInst = ResolveSkinInstance()
        If skinInst Is Nothing Then Return
        Dim skinData = ResolveSkinData(skinInst)
        If skinData Is Nothing OrElse skinData.BoneList Is Nothing Then Return

        Dim n As Integer = skinning.VertexCount
        Dim wpv As Integer = If(skinning.WeightsPerVertex > 0, skinning.WeightsPerVertex, 4)
        Dim numBones As Integer = skinData.BoneList.Count
        Dim shapeVC As Integer = Me.VertexCount

        ' ─── Validation (data-corruption prevention) ───
        ' 1) VertexCount consistency: if skinning has more vertices than the shape, we'd
        '    write out-of-bounds BoneVertData.Index values that produce a structurally
        '    valid-looking but in-game-corrupt NIF.
        If n > shapeVC Then
            Throw New InvalidOperationException(
                $"SetSkinning: skinning.VertexCount ({n}) exceeds shape vertex count ({shapeVC}).  " &
                "Refusing to write to prevent NiSkinData.BoneList[].VertexWeights Index overflow.")
        End If
        ' 2) Array size consistency: flat arrays must match VertexCount × WeightsPerVertex.
        Dim expectedFlatLen As Integer = n * wpv
        If skinning.BoneIndices.Length < expectedFlatLen OrElse skinning.BoneWeights.Length < expectedFlatLen Then
            Throw New InvalidOperationException(
                $"SetSkinning: flat skinning arrays too short.  Expected ≥{expectedFlatLen} " &
                $"(VertexCount {n} × WeightsPerVertex {wpv}), got BoneIndices={skinning.BoneIndices.Length}, " &
                $"BoneWeights={skinning.BoneWeights.Length}.")
        End If
        ' 3) Palette bounds: collect out-of-range indices up front.  If ANY vertex references
        '    a bone outside the BoneList palette we'd silently drop that weight → in-game
        '    vertex renders at bind-pose offset (visible as "vertex pegged in air").  Refuse
        '    to write rather than degrade silently.
        For i = 0 To expectedFlatLen - 1
            Dim w As Single = CType(skinning.BoneWeights(i), Single)
            If w <= 0.0F Then Continue For
            Dim bIdx As Integer = CInt(skinning.BoneIndices(i))
            If bIdx < 0 OrElse bIdx >= numBones Then
                Throw New InvalidOperationException(
                    $"SetSkinning: vertex {i \ wpv} slot {i Mod wpv} references bone palette index " &
                    $"{bIdx} which is outside the shape's BoneList (size {numBones}).  This would " &
                    "silently drop the weight and render the vertex at its bind-pose offset.")
            End If
        Next

        ' ─── Rebuild ───
        ' Always allocate a fresh List<BoneVertData> per bone (defensive, not optimization).
        ' DeepCopyHelper has a value-type short-circuit (DeepCopyHelper.cs:37) that skips
        ' recursing into struct fields.  NiSkinData.BoneList is List<BoneData> where BoneData
        ' is a struct holding `public List<BoneVertData> VertexWeights`.  After NifFile.CloneShape,
        ' each BoneData struct in the clone's BoneList is value-copied but its VertexWeights
        ' reference ALIASES the source's list.  If we used `.Clear()` on the existing list
        ' we'd wipe the source shape's skinning; creating a new list here breaks the alias
        ' and guarantees this adapter only mutates its own shape's data.
        For b = 0 To numBones - 1
            Dim bone = skinData.BoneList(b)
            bone.VertexWeights = New List(Of BoneVertData)()
            skinData.BoneList(b) = bone   ' re-store the struct
        Next

        ' Pivot per-vertex slots → per-bone (vertex, weight) entries.  Skip slots with
        ' zero weight (no influence) so the per-bone lists stay sparse.
        '
        ' NiflySharp UpdateSkinPartitions workaround: NifFile.cs:2447 does
        '   foreach (var tb in vertBoneWeights[tri[i]])
        ' without TryGetValue — if a vertex has no entry in any BoneList[b].VertexWeights
        ' (e.g. orphan vertex with all-zero weights), this throws KeyNotFoundException at
        ' partition rebuild time.  Seen on LE daedric armor NIFs where the source has
        ' weight-zero verts.  To avoid the crash we record per-vertex "did we write at
        ' least one entry" and add a dummy (bone 0, weight 0) entry for any vertex that
        ' would otherwise be absent.  Weight 0 has no render effect but ensures the
        ' vertex key exists in the dict.
        Dim vertHasEntry(n - 1) As Boolean
        For i = 0 To n - 1
            Dim vBase As Integer = i * wpv
            For j = 0 To wpv - 1
                Dim w As Single = CType(skinning.BoneWeights(vBase + j), Single)
                If w <= 0.0F Then Continue For
                Dim bIdx As Integer = CInt(skinning.BoneIndices(vBase + j))
                ' Validated in bounds above — unchecked access safe here.
                skinData.BoneList(bIdx).VertexWeights.Add(New BoneVertData() With {
                    .Index = CUShort(i),
                    .Weight = w
                })
                vertHasEntry(i) = True
            Next
        Next

        ' Second pass: ensure every vertex has at least one entry in SOME bone's list.
        ' Dummy (bone 0, weight 0) is inert renderer-side but satisfies NiflySharp's
        ' Dictionary lookup in UpdateSkinPartitions.
        Dim missingCount As Integer = 0
        If numBones > 0 Then
            Dim bone0 = skinData.BoneList(0)
            Dim appended As Boolean = False
            For i = 0 To n - 1
                If Not vertHasEntry(i) Then
                    bone0.VertexWeights.Add(New BoneVertData() With {
                        .Index = CUShort(i),
                        .Weight = 0.0F
                    })
                    appended = True
                    missingCount += 1
                End If
            Next
            If appended Then skinData.BoneList(0) = bone0
        End If

        ' Update per-bone NumVertices to match the rebuilt list (NiflySharp reads this on
        ' write; if stale, the binary output will be truncated/corrupt).  Degenerate bones
        ' (NumVertices=0) are valid per nif.xml — some bones may carry no weights after a
        ' heavy zap and the engine tolerates this.
        For b = 0 To numBones - 1
            Dim bone = skinData.BoneList(b)
            bone.NumVertices = CUShort(bone.VertexWeights.Count)
            skinData.BoneList(b) = bone
        Next
    End Sub

    ' ─────────────── Skinning helpers ───────────────
    Private Function ResolveSkinInstance() As INiSkin
        If _shape.SkinInstanceRef Is Nothing OrElse _shape.SkinInstanceRef.Index = -1 Then Return Nothing
        If _shape.SkinInstanceRef.Index >= _nif.Blocks.Count Then Return Nothing
        Return TryCast(_nif.Blocks(CInt(_shape.SkinInstanceRef.Index)), INiSkin)
    End Function

    Private Function ResolvePartition(skinInst As INiSkin) As NiSkinPartition
        Dim partRef As NiBlockRef(Of NiSkinPartition) = Nothing
        Dim niSkin = TryCast(skinInst, NiSkinInstance)
        If niSkin IsNot Nothing Then partRef = niSkin.SkinPartition
        If partRef Is Nothing OrElse partRef.Index = -1 Then Return Nothing
        If partRef.Index >= _nif.Blocks.Count Then Return Nothing
        Return TryCast(_nif.Blocks(CInt(partRef.Index)), NiSkinPartition)
    End Function

    Private Function ResolveSkinData(skinInst As INiSkin) As NiSkinData
        Dim dataRef As NiBlockRef(Of NiSkinData) = Nothing
        Dim niSkin = TryCast(skinInst, NiSkinInstance)
        If niSkin IsNot Nothing Then dataRef = niSkin.Data
        If dataRef Is Nothing OrElse dataRef.Index = -1 Then Return Nothing
        If dataRef.Index >= _nif.Blocks.Count Then Return Nothing
        Return TryCast(_nif.Blocks(CInt(dataRef.Index)), NiSkinData)
    End Function

    ''' <summary>
    ''' Walks each NiSkinPartition partition and writes per-vertex bone influences into the
    ''' flat output arrays.  Partition-local bone indices are translated through
    ''' partition.Bones to shape-level bone palette indices.  Vertices not covered by any
    ''' partition keep the default (index 0, weight 0).
    '''
    ''' Bone palette overflow: shape-level indices are stored as Byte (max 255), matching
    ''' BSTriShape's on-disk encoding.  FO4/SSE skeletons fit comfortably; if a port has more
    ''' than 256 bones the high indices are truncated and a Debug.Assert fires.
    ''' </summary>
    Private Sub FillFromPartition(skinPart As NiSkinPartition, outIdx As Byte(), outWgt As SysHalf(), vertCount As Integer, wpv As Integer)
        For Each part In skinPart.Partitions
            If part.VertexMap Is Nothing OrElse part.VertexMap.Count = 0 Then Continue For
            Dim partWpv As Integer = If(part.NumWeightsPerVertex = 0, wpv, CInt(part.NumWeightsPerVertex))
            Dim copySlots As Integer = Math.Min(wpv, partWpv)
            Dim numPartVerts As Integer = part.VertexMap.Count
            Dim partBones = part.Bones

            For k = 0 To numPartVerts - 1
                Dim globalVert As Integer = part.VertexMap(k)
                If globalVert < 0 OrElse globalVert >= vertCount Then Continue For
                Dim outBase As Integer = globalVert * wpv
                Dim partBase As Integer = k * partWpv
                For j = 0 To copySlots - 1
                    Dim partBoneIdx As Byte = If(part.BoneIndices IsNot Nothing AndAlso (partBase + j) < part.BoneIndices.Count,
                                                 part.BoneIndices(partBase + j), CByte(0))
                    Dim weight As Single = If(part.VertexWeights IsNot Nothing AndAlso (partBase + j) < part.VertexWeights.Count,
                                              part.VertexWeights(partBase + j), 0.0F)
                    Dim shapeBoneIdx As Integer = 0
                    If partBones IsNot Nothing AndAlso partBoneIdx < partBones.Count Then
                        shapeBoneIdx = CInt(partBones(partBoneIdx))
                    End If
                    Debug.Assert(shapeBoneIdx <= 255, "Bone palette overflow: NiTriShape with >256 bones cannot be encoded as Byte")
                    outIdx(outBase + j) = CByte(shapeBoneIdx And &HFF)
                    outWgt(outBase + j) = CType(weight, SysHalf)
                Next
            Next
        Next
    End Sub

    ''' <summary>
    ''' Builds per-vertex influences from NiSkinData.BoneList[i].VertexWeights (per-bone
    ''' {vertexIdx, weight} pairs).  Each vertex collects all incoming influences then keeps
    ''' the top 4 by descending weight (matching the GPU-friendly 4-slot layout).  The bone
    ''' palette index for slot j is the BoneList index i (already shape-level).
    '''
    ''' Used only when no NiSkinPartition is present — rare for FO4/SSE shapes built for the
    ''' game, but possible for hand-edited or unfinished meshes.
    ''' </summary>
    Private Sub FillFromSkinData(skinData As NiSkinData, outIdx As Byte(), outWgt As SysHalf(), vertCount As Integer, wpv As Integer)
        ' Per vertex: collect (boneIdx, weight) pairs, then top-N by weight.
        Dim influences = New List(Of (boneIdx As Integer, weight As Single))(vertCount)
        Dim perVertex As New Dictionary(Of Integer, List(Of (boneIdx As Integer, weight As Single)))()

        For boneIdx = 0 To skinData.BoneList.Count - 1
            Dim bone = skinData.BoneList(boneIdx)
            If bone.VertexWeights Is Nothing Then Continue For
            For Each vw In bone.VertexWeights
                Dim vIdx As Integer = vw.Index
                If vIdx < 0 OrElse vIdx >= vertCount Then Continue For
                Dim list As List(Of (Integer, Single)) = Nothing
                If Not perVertex.TryGetValue(vIdx, list) Then
                    list = New List(Of (Integer, Single))(wpv + 1)
                    perVertex(vIdx) = list
                End If
                list.Add((boneIdx, vw.Weight))
            Next
        Next

        For Each kvp In perVertex
            Dim vIdx = kvp.Key
            Dim list = kvp.Value
            list.Sort(Function(a, b) b.weight.CompareTo(a.weight))
            Dim copy = Math.Min(wpv, list.Count)
            Dim outBase = vIdx * wpv
            Dim sumW As Single = 0
            For j = 0 To copy - 1 : sumW += list(j).weight : Next
            ' Renormalize so the kept weights still sum to ~1 (only matters when we truncate
            ' beyond slot 4 — which is rare).
            Dim renorm As Single = If(sumW > 0.0F AndAlso copy < list.Count, 1.0F / sumW, 1.0F)
            For j = 0 To copy - 1
                Dim shapeBoneIdx As Integer = list(j).boneIdx
                Debug.Assert(shapeBoneIdx <= 255, "Bone palette overflow: NiSkinData has >256 bones")
                outIdx(outBase + j) = CByte(shapeBoneIdx And &HFF)
                outWgt(outBase + j) = CType(list(j).weight * renorm, SysHalf)
            Next
        Next
    End Sub

    ' ─────────────── NiTriStripsData reflection helpers ───────────────
    '
    ' NiTriStripsData's strip storage (_points, _stripLengths, _hasPoints, _numStrips) is
    ' protected in the auto-generated NiflySharp class, with no public accessors.  We use
    ' reflection to read/write those fields directly — same pattern that
    ' Nifcontent_Class_Manolo.GetTriParts uses on NiSkinPartition.triParts (NifContent_Class.vb:702).
    ' This keeps NiflySharp untouched.
    '
    ' Field cache: looked up once per type (Static field caching) to avoid per-call
    ' MethodInfo overhead in the render hot path.

    ' Public visibility so ShapeTypeValidator (in Wardrobe_Manager, different assembly) can
    ' reuse these reflection helpers for round-trip comparisons without duplicating the
    ' FieldInfo lookup.  NiflySharp auto-gen names these fields protected; single source
    ' of truth across assemblies.
    Public Shared ReadOnly StripPointsField As Reflection.FieldInfo = ResolveField(GetType(NiTriStripsData), "_points")
    Public Shared ReadOnly StripLengthsField As Reflection.FieldInfo = ResolveField(GetType(NiTriStripsData), "_stripLengths")
    Public Shared ReadOnly StripHasPointsField As Reflection.FieldInfo = ResolveField(GetType(NiTriStripsData), "_hasPoints")
    Public Shared ReadOnly StripNumStripsField As Reflection.FieldInfo = ResolveField(GetType(NiTriStripsData), "_numStrips")

    ''' <summary>
    ''' Resolves a protected NiflySharp field via reflection.  Throws if the field doesn't
    ''' exist — NiflySharp version bump with renamed field would otherwise produce silent
    ''' NullReferenceException on first read/write, possibly leaving half-written NIFs.
    ''' </summary>
    Private Shared Function ResolveField(t As Type, name As String) As Reflection.FieldInfo
        Dim f = t.GetField(name, Reflection.BindingFlags.NonPublic Or Reflection.BindingFlags.Instance)
        If f Is Nothing Then
            Throw New TypeLoadException(
                $"NiflySharp reflection: field '{name}' not found on {t.FullName}.  NiflySharp " &
                "version may have renamed the field — update the corresponding field constant.")
        End If
        Return f
    End Function

    ''' <summary>
    ''' Reads the strip layout (one inner list per strip) from a NiTriStripsData by pulling
    ''' the protected _points (flat List(Of UShort)) and _stripLengths (List(Of UShort))
    ''' fields and slicing _points by the lengths.  Returns an empty list if either field
    ''' is null.
    ''' </summary>
    Private Shared Function ReadStrips(stripData As NiTriStripsData) As List(Of List(Of UShort))
        Dim result As New List(Of List(Of UShort))()
        Dim points = TryCast(StripPointsField.GetValue(stripData), List(Of UShort))
        Dim lengths = TryCast(StripLengthsField.GetValue(stripData), List(Of UShort))
        If points Is Nothing OrElse lengths Is Nothing Then Return result

        Dim offset As Integer = 0
        For Each stripLen In lengths
            Dim count As Integer = CInt(stripLen)
            If offset + count > points.Count Then Exit For
            result.Add(points.GetRange(offset, count))
            offset += count
        Next
        Return result
    End Function

    ''' <summary>
    ''' Writes a strip layout back to NiTriStripsData by flattening into _points + setting
    ''' _stripLengths to the per-strip counts.  Updates _numStrips and _hasPoints to keep
    ''' the block internally consistent (otherwise NiflySharp's Sync writes wrong sizes).
    ''' </summary>
    Private Shared Sub WriteStrips(stripData As NiTriStripsData, strips As List(Of List(Of UShort)))
        If strips Is Nothing Then strips = New List(Of List(Of UShort))()

        ' Guard against silent UShort overflow: NiTriStripsData._numStrips is UShort16 so
        ' CUShort(strips.Count) would wrap and lose data for large meshes.  A mesh big
        ' enough to hit this is very unusual for degenerate-strips (one strip per triangle)
        ' — would require >65535 triangles — but fail loud instead of corrupt silently.
        If strips.Count > UShort.MaxValue Then
            Throw New InvalidOperationException(
                $"WriteStrips: {strips.Count} strips exceeds NiTriStripsData._numStrips UShort16 " &
                "limit (65535).  Mesh is too large for the degenerate-strips (1 tri per strip) " &
                "encoding used by this adapter.  True stripification would consolidate triangles " &
                "into fewer longer strips — not implemented; see pending_tests_shape_metadata.md.")
        End If
        ' Per-strip length is also UShort16 but degenerate strips are always length 3 — safe.
        ' For future proper stripification this would need per-strip check too.

        Dim flatPoints As New List(Of UShort)(strips.Sum(Function(s) s.Count))
        Dim lengths As New List(Of UShort)(strips.Count)
        For Each strip In strips
            If strip.Count > UShort.MaxValue Then
                Throw New InvalidOperationException(
                    $"WriteStrips: strip of length {strip.Count} exceeds UShort16 limit.")
            End If
            lengths.Add(CUShort(strip.Count))
            flatPoints.AddRange(strip)
        Next

        StripPointsField.SetValue(stripData, flatPoints)
        StripLengthsField.SetValue(stripData, lengths)
        StripNumStripsField.SetValue(stripData, CUShort(strips.Count))
        StripHasPointsField.SetValue(stripData, CType(flatPoints.Count > 0, Boolean?))
    End Sub

    ' ─────────────── BSSegmentedTriShape Segments reflection helpers ───────────────
    '
    ' Same pattern as the strip fields above and as NifContent_Class.GetTriParts on
    ' NiSkinPartition.triParts: NiflySharp's auto-generated BSSegmentedTriShape exposes
    ' _segment as protected only.  No public partial extension exists (intentionally —
    ' "no toques NiflySharp" per user policy).  Reflection cached at type init.

    Public Shared ReadOnly SegmentedSegmentField As Reflection.FieldInfo =
        ResolveField(GetType(BSSegmentedTriShape), "_segment")

    Private Shared Function ReadSegmentedSegments(seg As BSSegmentedTriShape) As List(Of BSGeometrySegmentData)
        Dim raw = TryCast(SegmentedSegmentField.GetValue(seg), List(Of BSGeometrySegmentData))
        If raw Is Nothing Then Return Nothing
        Return raw.ToList()
    End Function

    Private Shared Sub WriteSegmentedSegments(seg As BSSegmentedTriShape, newList As List(Of BSGeometrySegmentData))
        SegmentedSegmentField.SetValue(seg, newList)
    End Sub
End Class
