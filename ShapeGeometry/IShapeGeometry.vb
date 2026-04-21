Imports NiflySharp
Imports NiflySharp.Structs
Imports SysNumerics = System.Numerics

''' <summary>
''' Polymorphic adapter for the geometry of a NIF shape, hiding the layout differences between
''' the BSTriShape family (FO4/SSE — vertex/triangle data inline in BSVertexData/BSVertexDataSSE)
''' and the NiTriBasedGeom family (NiTriShape, BSLODTriShape — vertex/triangle data in a separate
''' NiTriShapeData block via DataRef).
'''
''' Read methods always return new lists/arrays (caller can mutate without aliasing the underlying
''' NIF block).  Write methods commit the change to the underlying NIF block immediately, but DO
''' NOT regenerate the skin partition — for SSE-skinned shapes the caller must invoke
''' Nifcontent_Class_Manolo.UpdateSkinPartitions(shape) before saving, exactly like today.  This
''' contract matches what BSTriShape consumers (BuildingForm, MorphingHelper.RemoveZaps,
''' SplitShapeHelper, MergeShapesHelper) already do.
'''
''' Tangent / Bitangent semantics: getters return values in the rendering convention used by
''' SkinningHelper (the "INVERTIDAS swap" applied to BSTriShape is encapsulated inside the BS
''' adapter).  Setters expect values in the same convention and translate back to the on-disk
''' representation if the underlying block uses a different one.
''' </summary>
Public Interface IShapeGeometry

    ' ─────────────── Identity ───────────────
    ''' <summary>Underlying NIF block (BSTriShape / BSSubIndexTriShape / NiTriShape / BSLODTriShape / ...).</summary>
    ReadOnly Property BackingShape As INiShape

    ''' <summary>Shape name as stored in the NIF (BackingShape.Name.String).</summary>
    ReadOnly Property Name As String

    ''' <summary>NIF version of the parent file — needed to choose between BSVertexData and BSVertexDataSSE paths.</summary>
    ReadOnly Property Version As NiVersion

    ' ─────────────── Counts and presence flags ───────────────
    ReadOnly Property VertexCount As Integer
    ReadOnly Property TriangleCount As Integer
    ReadOnly Property HasNormals As Boolean
    ReadOnly Property HasTangents As Boolean
    ReadOnly Property HasUVs As Boolean
    ReadOnly Property HasVertexColors As Boolean
    ReadOnly Property HasEyeData As Boolean
    ReadOnly Property IsSkinned As Boolean
    ReadOnly Property Bounds As BoundingSphere

    ' ─────────────── Read ───────────────
    ''' <summary>Vertex positions in shape-local space.  Always returns VertexCount entries.</summary>
    Function GetVertexPositions() As List(Of SysNumerics.Vector3)

    ''' <summary>Unit normals; empty list when HasNormals = False.</summary>
    Function GetNormals() As List(Of SysNumerics.Vector3)

    ''' <summary>Tangents in renderer convention (post-INVERTIDAS for BSTriShape).  Empty when HasTangents = False.</summary>
    Function GetTangents() As List(Of SysNumerics.Vector3)

    ''' <summary>Bitangents in renderer convention (post-INVERTIDAS for BSTriShape).  Empty when HasTangents = False.</summary>
    Function GetBitangents() As List(Of SysNumerics.Vector3)

    ''' <summary>UV coordinates; empty when HasUVs = False.</summary>
    Function GetUVs() As List(Of TexCoord)

    ''' <summary>Per-vertex colors; empty when HasVertexColors = False.</summary>
    Function GetVertexColors() As List(Of Color4)

    ''' <summary>Per-vertex eye data scalar (FO4 face shapes); empty when HasEyeData = False.</summary>
    Function GetEyeData() As List(Of Single)

    ''' <summary>Triangle list (V1,V2,V3 are vertex indices into the position array).</summary>
    Function GetTriangles() As List(Of Triangle)

    ''' <summary>
    ''' Per-vertex bone influences in flat layout (4 slots / vertex padded with zeros).
    ''' Returns ShapeSkinningData.Empty when IsSkinned = False.
    ''' </summary>
    Function GetSkinning() As ShapeSkinningData

    ' ─────────────── Write (commits to the backing block; does not touch the skin partition) ───────────────

    ''' <summary>
    ''' Establishes a new vertex count on the underlying block, allocating/resizing the
    ''' per-vertex storage accordingly.  Call this BEFORE SetVertexPositions / SetNormals /
    ''' SetSkinning / etc. when the vertex count changes (zap, split, merge).  When the
    ''' count is unchanged the call is a no-op.
    '''
    ''' BSTriShape family: replaces the inline packed buffer (BSVertexData/BSVertexDataSSE
    ''' list) with a zero-initialised list of the new size.  The subsequent per-field
    ''' setters (SetVertexPositions, SetNormals, SetTangents, SetBitangents, SetUVs,
    ''' SetVertexColors, SetEyeData, SetSkinning) populate each field.  Fields not touched
    ''' by any setter remain zero — currently all fields of BSVertexData/SSE are covered.
    '''
    ''' NiTriShape family: resizes the NiTriBasedGeomData.Vertices list to the new count;
    ''' NumVertices auto-updates via NiGeometryData.Vertices setter.  Other per-vertex
    ''' arrays (Normals, Tangents, UVs, VertexColors) are resized lazily when their
    ''' individual setter is called.
    ''' </summary>
    Sub ResizeVertices(vertexCount As Integer)

    Sub SetVertexPositions(positions As List(Of SysNumerics.Vector3))
    Sub SetNormals(normals As List(Of SysNumerics.Vector3))
    Sub SetTangents(tangents As List(Of SysNumerics.Vector3))
    Sub SetBitangents(bitangents As List(Of SysNumerics.Vector3))
    Sub SetUVs(uvs As List(Of TexCoord))
    Sub SetVertexColors(colors As List(Of Color4))
    Sub SetEyeData(eyeData As List(Of Single))
    Sub SetTriangles(triangles As List(Of Triangle))

    ''' <summary>
    ''' Provenance-aware triangle write.  When <paramref name="provenance"/> is provided
    ''' (length must equal <paramref name="triangles"/>.Count), the adapter redistributes
    ''' count-derived metadata using the per-new-triangle source map:
    '''   - BSMeshLODTriShape / BSLODTriShape LOD0/1/2 sizes: tier-preserving reorder.
    '''     Triangles are bucketed by their old-tier source (via provenance) and rewritten
    '''     in [LOD0][LOD1][LOD2] order; LOD sizes reflect the new bucket counts.  Triangles
    '''     with cross-shape or synthetic sources fall into LOD2 (the "always visible" tier).
    '''     Previously collapsed everything to LOD2 (BS-OS Geometry.cpp:1522 canonical
    '''     behaviour); the new approach keeps the LOD optimization across split/merge/zap.
    '''   - BSSubIndexTriShape / BSSegmentedTriShape Segments and SubSegmentDatas:
    '''     full redistribution preserving per-segment metadata (ParentArrayIndex,
    '''     SegmentSharedData, SubSegmentDatas).  Algorithm follows BS-OS Geometry.cpp:1248
    '''     notifyVerticesDelete: count survivors per old-segment range, realign cumulative
    '''     StartIndex.
    ''' When <paramref name="provenance"/> is Nothing this overload behaves identically to
    ''' <see cref="SetTriangles(List(Of Triangle))"/>; metadata-bearing subclasses leave their
    ''' metadata untouched (likely stale — caller is responsible).
    ''' </summary>
    Sub SetTriangles(triangles As List(Of Triangle), provenance As TriangleRemap)

    ''' <summary>
    ''' Writes per-vertex bone influences back to the underlying skin storage.
    ''' Polymorphic: BSTriShape adapter writes into the inline BSVertexData[].BoneIndices /
    ''' BoneWeights of each vertex; NiTriShape adapter rebuilds NiSkinData.BoneList[].
    ''' VertexWeights from the per-vertex slots (one entry per (bone, vertex) pair where
    ''' weight > 0).  In both cases the caller-supplied <paramref name="skinning"/> must have
    ''' VertexCount equal to the shape's current vertex count and bone indices that fit the
    ''' shape's bone palette.  Skin partition is NOT regenerated here — caller must invoke
    ''' Nifcontent_Class_Manolo.UpdateSkinPartitions(shape) afterwards (existing contract).
    ''' </summary>
    Sub SetSkinning(skinning As ShapeSkinningData)

    ''' <summary>
    ''' Recomputes the bounding sphere of the shape from current vertex positions.
    ''' Mirror of NiflySharp's BSTriShape.UpdateBounds / BSTriShape.UpdateBounds.
    ''' </summary>
    Sub UpdateBounds()

End Interface
