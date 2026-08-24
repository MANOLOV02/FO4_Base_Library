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

    ''' <summary>
    ''' True si el bloque guarda la normal CUANTIZADA A BYTE (3 sbyte por vertice) en vez de en float.
    '''
    ''' No es un detalle de serializacion: <c>BSTriShape::CalcTangentSpace</c> llama
    ''' <c>UpdateRawNormals()</c> en su primera linea (nifly Geometry.cpp), y esa funcion
    ''' RE-DECODIFICA las normales desde esos bytes (Geometry.cpp), pisando la copia de plena
    ''' precision. O sea que el canonico ortogonaliza la base tangente contra la normal cuantizada, y
    ''' medio paso de cuantizacion son 0,45 grados. En el PRIMARIO eso es un sesgo chico; en el
    ''' SECUNDARIO de un shell de UV espejado —donde el residuo del Gram-Schmidt es mas chico que ese
    ''' error— decide el SIGNO. Sin replicarlo no hay paridad posible en esos vertices.
    ''' </summary>
    ReadOnly Property NormalsAreByteQuantized As Boolean

    ''' <summary>
    ''' True si el bloque guarda las UV en HALF (16 bits). <c>BSTriShape</c> lo hace SIEMPRE, en los
    ''' dos juegos y sin condicional de precision (nifly Geometry.cpp).
    ''' Importa porque <c>CalcTangentSpace</c> deriva s1/s2/t1/t2 de <c>vertData[i].uv</c>, o sea
    ''' de las UV ya redondeadas. El paso de half en [0,1] es ~5e-4: contra float es enorme, y en una
    ''' costura de UV espejado —donde los aportes de los dos lados casi se cancelan— es lo unico que
    ''' queda del acumulado, asi que define el SIGNO de la bitangente.
    ''' </summary>
    ReadOnly Property UvsAreHalfPrecision As Boolean

    ''' <summary>
    ''' Indices cuya normal NO se recalcula: el <c>NiIntegersExtraData</c> llamado <c>LOCKEDNORM</c>.
    ''' El canonico los saltea en <c>NifFile::CalcNormalsForShape</c>. Nothing si la shape no lo trae.
    ''' </summary>
    Function GetLockedNormalIndices() As HashSet(Of Integer)

    ''' <summary>Renumera el <c>LOCKEDNORM</c> al espacio de vértices NUEVO tras compactar, y descarta
    ''' las entradas cuyo vértice se fue.
    ''' <para>⛔ SYNC: <c>nifly\src\NifFile.cpp:4328-4353</c>, dentro de <c>DeleteVertsForShape</c>: el
    ''' canónico re-mapea con <c>GenerateIndexCollapseMap</c> y BORRA lo que no sobrevive. Hasta ahora la
    ''' app tenía LECTORES de esa lista y ningún ESCRITOR, así que después de un zap quedaba apuntando a
    ''' números del espacio VIEJO — o sea a otros vértices. <c>RemoveZaps</c> compacta vértices,
    ''' triángulos, skin, UVs, colores y particiones, pero no tocaba el extra data.</para>
    ''' <para><paramref name="oldToNew"/> es el mapa de <c>MorphingHelper.RemoveZaps</c>, que ES el
    ''' <c>indexCollapse</c> del canónico: se dimensiona sobre el espacio viejo completo y arranca todo en
    ''' −1, y sólo los sobrevivientes reciben índice. Por eso cubre gratis el caso
    ''' <c>val &gt; highestRemoved</c> que el canónico trata aparte.</para>
    ''' <para>MEDIDO: 114 NIF de SSE traen la lista (0 en FO4) y 57 sliderSets la zapean; uno de ellos
    ''' —<c>UBE SE 2.0 Release Brows UV map sliders</c>— con un zap que viene en 100 por defecto en el
    ''' propio .osp, o sea que se dispara sin que el usuario toque nada.</para></summary>
    Sub RemapLockedNormalIndices(oldToNew As Integer())

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
    '''     Previously collapsed everything to LOD2 (BS-OS Geometry.cpp canonical
    '''     behaviour); the new approach keeps the LOD optimization across split/merge/zap.
    '''   - BSSubIndexTriShape / BSSegmentedTriShape Segments and SubSegmentDatas:
    '''     full redistribution preserving per-segment metadata (ParentArrayIndex,
    '''     SegmentSharedData, SubSegmentDatas).  Algorithm follows BS-OS Geometry.cpp
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

    ' ─────────────── Runtime synthetic skinning ───────────────
    ''' <summary>True once SetSyntheticSkinning has been called. When True, GetSkinning
    ''' returns the synthetic data instead of reading from the NIF block, and IsSkinned
    ''' returns True regardless of the backing block's state.</summary>
    ReadOnly Property HasSyntheticSkinning As Boolean

    ''' <summary>
    ''' Inject runtime synthetic per-vertex skin data. data.VertexCount must equal this
    ''' shape's VertexCount. Does NOT write to the NIF block — purely in-memory override.
    ''' Used by IRuntimeSkinOverride.ApplySyntheticAnchorSkin to make an unskinned shape
    ''' behave as skinned to a single anchor bone for rendering.
    ''' </summary>
    Sub SetSyntheticSkinning(data As ShapeSkinningData)

End Interface
