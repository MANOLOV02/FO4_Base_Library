Imports NiflySharp
Imports NiflySharp.Blocks
Imports NiflySharp.Structs
Imports SysHalf = System.Half
Imports SysNumerics = System.Numerics

''' <summary>Adaptador IShapeGeometry para la familia BSTriShape (BSTriShape, BSSubIndexTriShape,
''' BSDynamicTriShape, BSSegmentedTriShape, BSMeshLODTriShape). Los vertices y triangulos viven inline en el
''' bloque (BSVertexData en FO4, BSVertexDataSSE en SSE) y la particion de skin la regenera NiflySharp al
''' guardar: este adaptador no la toca.
''' <para>⛔ TANGENTE Y BITANGENTE VAN CRUZADAS respecto de como las nombra el formato: la tangente del
''' renderer es la "Bitangent" del NIF y viceversa. Este adaptador ENCAPSULA el swap - los getters devuelven
''' del lado renderer y los setters rutean al campo opuesto -, asi que los callers nunca lo ven. No
''' "corregirlo" desde afuera.</para>
''' <para>⚠ï¸ En FO4 half-precision la bitangente hay que leerla directo del vertex data y no por el getter de
''' NiflySharp, que ahi tiene un decode mismatch. SSE y FO4 full-precision usan el getter normal.</para></summary>
Public Class BSTriShapeGeometry
    Implements IShapeGeometry

    Private ReadOnly _tri As BSTriShape
    Private ReadOnly _nif As Nifcontent_Class_Manolo

    ' Runtime synthetic skin override (see IRuntimeSkinOverride). When set, IsSkinned and
    ' GetSkinning return this data instead of reading from _tri's NIF block. Used for
    ' unskinned shapes that need to be anchored to a bone at render time without mutating
    ' the NIF on disk (e.g. LightPlane in BSConnectPoint chunks).
    Private _syntheticSkinning As ShapeSkinningData? = Nothing

    Public Sub New(tri As BSTriShape, nif As Nifcontent_Class_Manolo)
        ArgumentNullException.ThrowIfNull(tri)
        ArgumentNullException.ThrowIfNull(nif)
        _tri = tri
        _nif = nif
    End Sub

    Public ReadOnly Property HasSyntheticSkinning As Boolean Implements IShapeGeometry.HasSyntheticSkinning
        Get
            Return _syntheticSkinning.HasValue
        End Get
    End Property

    Public Sub SetSyntheticSkinning(data As ShapeSkinningData) Implements IShapeGeometry.SetSyntheticSkinning
        If data.VertexCount <> _tri.VertexCount Then
            Throw New ArgumentException($"SetSyntheticSkinning vertex count mismatch: shape has {_tri.VertexCount}, data has {data.VertexCount}")
        End If
        _syntheticSkinning = data
    End Sub

    ' ─────────────── Identity ───────────────
    Public ReadOnly Property BackingShape As INiShape Implements IShapeGeometry.BackingShape
        Get
            Return _tri
        End Get
    End Property

    Public ReadOnly Property Name As String Implements IShapeGeometry.Name
        Get
            Return If(_tri?.Name?.String, "")
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
            Return _tri.VertexCount
        End Get
    End Property

    Public ReadOnly Property TriangleCount As Integer Implements IShapeGeometry.TriangleCount
        Get
            Return _tri.TriangleCount
        End Get
    End Property

    Public ReadOnly Property HasNormals As Boolean Implements IShapeGeometry.HasNormals
        Get
            Return _tri.HasNormals
        End Get
    End Property

    Public ReadOnly Property HasTangents As Boolean Implements IShapeGeometry.HasTangents
        Get
            Return _tri.HasTangents
        End Get
    End Property

    Public ReadOnly Property HasUVs As Boolean Implements IShapeGeometry.HasUVs
        Get
            Return _tri.HasUVs
        End Get
    End Property

    Public ReadOnly Property HasVertexColors As Boolean Implements IShapeGeometry.HasVertexColors
        Get
            Return _tri.HasVertexColors
        End Get
    End Property

    Public ReadOnly Property HasEyeData As Boolean Implements IShapeGeometry.HasEyeData
        Get
            Return _tri.HasEyeData
        End Get
    End Property

    Public ReadOnly Property IsSkinned As Boolean Implements IShapeGeometry.IsSkinned
        Get
            ' Synthetic runtime override: shapes that are unskinned in the NIF but were
            ' fake-skinned at runtime via SetSyntheticSkinning report as skinned for the
            ' downstream pipeline (SkinningHelper, Render VBO upload).
            Return _syntheticSkinning.HasValue OrElse _tri.IsSkinned
        End Get
    End Property

    Public ReadOnly Property Bounds As BoundingSphere Implements IShapeGeometry.Bounds
        Get
            Return _tri.Bounds
        End Get
    End Property

    ''' <summary>Si: <c>BSVertexData.normal</c> son 3 sbyte, en los dos juegos. Ver la nota de la
    ''' interfaz — de esto depende contra que normal se ortogonaliza la base tangente.</summary>
    Public ReadOnly Property NormalsAreByteQuantized As Boolean Implements IShapeGeometry.NormalsAreByteQuantized
        Get
            Return True
        End Get
    End Property

    ''' <summary>Siempre. Ver la nota de la interfaz.</summary>
    Public ReadOnly Property UvsAreHalfPrecision As Boolean Implements IShapeGeometry.UvsAreHalfPrecision
        Get
            Return True
        End Get
    End Property

    ''' <summary>Indices de LOCKEDNORM; Nothing si la shape no trae ese extra data.</summary>
    Public Function GetLockedNormalIndices() As HashSet(Of Integer) Implements IShapeGeometry.GetLockedNormalIndices
        Dim lista = _tri?.ExtraDataList
        If lista Is Nothing OrElse lista.References Is Nothing Then Return Nothing
        Dim res As HashSet(Of Integer) = Nothing
        For Each ref In lista.References
            If ref.Index < 0 OrElse ref.Index >= _nif.Blocks.Count Then Continue For
            Dim ints = TryCast(_nif.Blocks(CInt(ref.Index)), NiIntegersExtraData)
            If ints Is Nothing OrElse ints.Data Is Nothing Then Continue For
            If Not String.Equals(ints.Name?.String, "LOCKEDNORM", StringComparison.Ordinal) Then Continue For
            If res Is Nothing Then res = New HashSet(Of Integer)()
            For Each v In ints.Data
                res.Add(CInt(v))
            Next
        Next
        Return res
    End Function

    ''' <summary>Ver <see cref="IShapeGeometry.RemapLockedNormalIndices"/> para la ley y la medición.</summary>
    Public Sub RemapLockedNormalIndices(oldToNew As Integer()) Implements IShapeGeometry.RemapLockedNormalIndices
        If oldToNew Is Nothing Then Exit Sub
        Dim lista = _tri?.ExtraDataList
        If lista Is Nothing OrElse lista.References Is Nothing Then Exit Sub
        For Each ref In lista.References
            If ref.Index < 0 OrElse ref.Index >= _nif.Blocks.Count Then Continue For
            Dim ints = TryCast(_nif.Blocks(CInt(ref.Index)), NiIntegersExtraData)
            If ints Is Nothing OrElse ints.Data Is Nothing Then Continue For
            If Not String.Equals(ints.Name?.String, "LOCKEDNORM", StringComparison.Ordinal) Then Continue For
            Dim nuevos As New List(Of UInteger)(ints.Data.Count)
            For Each v In ints.Data
                Dim viejo As Long = CLng(v)
                ' Fuera del espacio viejo = índice inválido; el canónico tampoco lo puede conservar (su
                ' rama `val > highestRemoved` presupone que todo lo borrado está por debajo). Sin este
                ' guard sería IndexOutOfRangeException, y no se puede acotar por el corpus: 117 de 118
                ' shapes declaran `numVertices = 0` en el bloque, que es lo normal en SSE con skin.
                If viejo < 0 OrElse viejo >= oldToNew.Length Then Continue For
                Dim nuevo As Integer = oldToNew(CInt(viejo))
                If nuevo >= 0 Then nuevos.Add(CUInt(nuevo))
            Next
            ' `NumIntegers` NO se toca: lo recalcula `Sync` desde `Data` (NiIntegersExtraData.g.cs:53).
            ints.Data = nuevos
        Next
    End Sub


    ' ─────────────── Read ───────────────
    Public Function GetVertexPositions() As List(Of SysNumerics.Vector3) Implements IShapeGeometry.GetVertexPositions
        Return If(_tri.VertexPositions?.ToList(), New List(Of SysNumerics.Vector3)())
    End Function

    Public Function GetNormals() As List(Of SysNumerics.Vector3) Implements IShapeGeometry.GetNormals
        If Not _tri.HasNormals Then Return New List(Of SysNumerics.Vector3)()
        Return If(_tri.Normals?.ToList(), New List(Of SysNumerics.Vector3)())
    End Function

    ''' <summary>
    ''' Renderer-convention tangent.  Sourced from NIF "Bitangent" fields (BitangentX/Y/Z)
    ''' due to the INVERTIDAS swap.  Routes by precision: SSE uses BSVertexDataSSE.BitangentX
    ''' (float) + BitangentY/Z (sbyte).  FO4 full-precision uses BSVertexData.BitangentX (float).
    ''' FO4 half-precision uses BSVertexData.BitangentXHalf (Half).  In every case
    ''' BitangentY/Z are sbyte values that decode as ((b And &HFF) / 255 * 2 - 1).
    ''' </summary>
    Public Function GetTangents() As List(Of SysNumerics.Vector3) Implements IShapeGeometry.GetTangents
        If Not _tri.HasTangents Then Return New List(Of SysNumerics.Vector3)()
        Dim n As Integer = _tri.VertexCount
        Dim result As New List(Of SysNumerics.Vector3)(n)

        If Version.IsSSE Then
            Dim vd = _tri.VertexDataSSE
            If vd Is Nothing OrElse vd.Count <> n Then
                ' Defensive: SSE shape with no vertex data — caller would have bigger problems but don't crash here.
                For i = 0 To n - 1 : result.Add(SysNumerics.Vector3.Zero) : Next
                Return result
            End If
            For i = 0 To n - 1
                result.Add(DecodeBitangent(CSng(vd(i).BitangentX), vd(i).BitangentY, vd(i).BitangentZ))
            Next
        Else
            Dim vd = _tri.VertexData
            If vd Is Nothing OrElse vd.Count <> n Then
                For i = 0 To n - 1 : result.Add(SysNumerics.Vector3.Zero) : Next
                Return result
            End If
            If _tri.IsFullPrecision Then
                For i = 0 To n - 1
                    result.Add(DecodeBitangent(vd(i).BitangentX, vd(i).BitangentY, vd(i).BitangentZ))
                Next
            Else
                For i = 0 To n - 1
                    result.Add(DecodeBitangent(CSng(vd(i).BitangentXHalf), vd(i).BitangentY, vd(i).BitangentZ))
                Next
            End If
        End If
        Return result
    End Function

    ''' <summary>
    ''' Renderer-convention bitangent.  Sourced from NIF "Tangent" field (BSVertexData.Tangent
    ''' ByteVector3) due to the INVERTIDAS swap.  For SSE and FO4 full-precision we route
    ''' through tri.Tangents (NiflySharp decodes ByteVector3 → Vector3 correctly there).  For
    ''' FO4 half-precision we read BSVertexData.Tangent directly with the byte normalization,
    ''' to bypass a known NiflySharp Vector3 getter mismatch on that path.
    ''' </summary>
    Public Function GetBitangents() As List(Of SysNumerics.Vector3) Implements IShapeGeometry.GetBitangents
        If Not _tri.HasTangents Then Return New List(Of SysNumerics.Vector3)()
        Dim n As Integer = _tri.VertexCount
        Dim result As New List(Of SysNumerics.Vector3)(n)

        If Version.IsSSE OrElse _tri.IsFullPrecision Then
            Dim src = _tri.Tangents
            If src Is Nothing OrElse src.Count <> n Then
                For i = 0 To n - 1 : result.Add(SysNumerics.Vector3.Zero) : Next
                Return result
            End If
            For i = 0 To n - 1
                Dim v = src(i)
                result.Add(NormalizeOrZero(v))
            Next
        Else
            ' FO4 half-precision: read BSVertexData.Tangent (ByteVector3 with sbyte fields) directly.
            Dim vd = _tri.VertexData
            If vd Is Nothing OrElse vd.Count <> n Then
                For i = 0 To n - 1 : result.Add(SysNumerics.Vector3.Zero) : Next
                Return result
            End If
            For i = 0 To n - 1
                Dim t = vd(i).Tangent
                result.Add(NormalizeOrZero(DecodeByteVector3(t.X, t.Y, t.Z)))
            Next
        End If
        Return result
    End Function

    Public Function GetUVs() As List(Of TexCoord) Implements IShapeGeometry.GetUVs
        If Not _tri.HasUVs Then Return New List(Of TexCoord)()
        Return If(_tri.UVs?.ToList(), New List(Of TexCoord)())
    End Function

    Public Function GetVertexColors() As List(Of Color4) Implements IShapeGeometry.GetVertexColors
        If Not _tri.HasVertexColors Then Return New List(Of Color4)()
        Return If(_tri.VertexColors?.ToList(), New List(Of Color4)())
    End Function

    Public Function GetEyeData() As List(Of Single) Implements IShapeGeometry.GetEyeData
        If Not _tri.HasEyeData Then Return New List(Of Single)()
        Return If(_tri.EyeData?.ToList(), New List(Of Single)())
    End Function

    Public Function GetTriangles() As List(Of Triangle) Implements IShapeGeometry.GetTriangles
        Return If(_tri.Triangles?.ToList(), New List(Of Triangle)())
    End Function

    ''' <summary>
    ''' Per-vertex bone indices/weights.  Always 4 slots per vertex (NiflySharp's BSVertexData
    ''' BoneWeights/BoneIndices arrays are length 4).  Unused slots have weight=0 and index=0.
    ''' Returns ShapeSkinningData.Empty when the shape is unskinned.
    ''' </summary>
    Public Function GetSkinning() As ShapeSkinningData Implements IShapeGeometry.GetSkinning
        ' Synthetic runtime override path: caller injected per-vertex skin data without
        ' mutating the NIF. Return that verbatim — the NIF vertex buffer may not have skin
        ' fields populated.
        If _syntheticSkinning.HasValue Then Return _syntheticSkinning.Value
        If Not _tri.IsSkinned Then Return ShapeSkinningData.Empty

        Dim n As Integer = _tri.VertexCount
        Const wpv As Integer = 4
        Dim outIdx(n * wpv - 1) As Byte
        Dim outWgt(n * wpv - 1) As SysHalf

        ' BSVertexData/BSVertexDataSSE hold BoneWeights/BoneIndices as [InlineArray(4)]
        ' structs. BoneInlineArrayExt copies the 4 struct slots straight into the
        ' destination slice - no intermediate buffer, one memcopy per field per vertex.
        If Version.IsSSE Then
            Dim vd = _tri.VertexDataSSE
            If vd Is Nothing OrElse vd.Count <> n Then Return ShapeSkinningData.Empty
            For i = 0 To n - 1
                Dim v = vd(i)
                v.BoneIndices.CopyTo(outIdx, i * wpv, wpv)
                v.BoneWeights.CopyTo(outWgt, i * wpv, wpv)
            Next
        Else
            Dim vd = _tri.VertexData
            If vd Is Nothing OrElse vd.Count <> n Then Return ShapeSkinningData.Empty
            For i = 0 To n - 1
                Dim v = vd(i)
                v.BoneIndices.CopyTo(outIdx, i * wpv, wpv)
                v.BoneWeights.CopyTo(outWgt, i * wpv, wpv)
            Next
        End If

        Return New ShapeSkinningData With {
            .BoneIndices = outIdx,
            .BoneWeights = outWgt,
            .WeightsPerVertex = wpv,
            .VertexCount = n,
            .BoneRefIndices = ResolveBonePalette()
        }
    End Function

    ' --------------- Write ---------------
    ''' <summary>Fija la cantidad de vertices del BSTriShape. No-op si ya es la pedida; si no, reemplaza la
    ''' lista empaquetada por una nueva inicializada en cero via SetVertexData/SetVertexDataSSE (que ajusta
    ''' _numVertices y redimensiona el buffer atomicamente). Despues de esto los setters por campo
    ''' (posiciones, normales, tangentes, UVs, colores, eye data, skinning) son validos.
    ''' <para>NiflySharp devuelve en silencio si la lista supera ushort.MaxValue, asi que se pre-chequea aca y
    ''' se tira excepcion explicita.</para></summary>
    Public Sub ResizeVertices(vertexCount As Integer) Implements IShapeGeometry.ResizeVertices
        If vertexCount < 0 Then Throw New ArgumentOutOfRangeException(NameOf(vertexCount), "Negative vertex count")
        If vertexCount > UShort.MaxValue Then
            Throw New InvalidOperationException(
                $"ResizeVertices: {vertexCount} exceeds NiflySharp BSTriShape _numVertices UShort limit " &
                "(65535).  BSTriShape cannot hold more than 65535 vertices per shape — split the shape first.")
        End If
        If _tri.VertexCount = vertexCount Then Return

        If Version.IsSSE Then
            Dim fresh As New List(Of BSVertexDataSSE)(vertexCount)
            For i = 0 To vertexCount - 1 : fresh.Add(New BSVertexDataSSE()) : Next
            _tri.SetVertexDataSSE(fresh)
        Else
            Dim fresh As New List(Of BSVertexData)(vertexCount)
            For i = 0 To vertexCount - 1 : fresh.Add(New BSVertexData()) : Next
            _tri.SetVertexData(fresh)
        End If
        SyncDynamicIfNeeded()
    End Sub

    Public Sub SetVertexPositions(positions As List(Of SysNumerics.Vector3)) Implements IShapeGeometry.SetVertexPositions
        _tri.SetVertexPositions(positions)
        SyncDynamicIfNeeded()
    End Sub

    Public Sub SetNormals(normals As List(Of SysNumerics.Vector3)) Implements IShapeGeometry.SetNormals
        _tri.SetNormals(normals)
    End Sub

    ''' <summary>
    ''' Writes renderer-convention tangents back to the NIF.  Per the INVERTIDAS swap, this
    ''' routes to NiflySharp's tri.SetBitangents (which writes BSVertexData.BitangentX/Y/Z,
    ''' selecting full-precision vs half-precision automatically).
    ''' </summary>
    Public Sub SetTangents(tangents As List(Of SysNumerics.Vector3)) Implements IShapeGeometry.SetTangents
        _tri.SetBitangents(tangents)
    End Sub

    ''' <summary>
    ''' Writes renderer-convention bitangents back to the NIF.  Per the INVERTIDAS swap, this
    ''' routes to NiflySharp's tri.SetTangents (which writes BSVertexData.Tangent ByteVector3).
    ''' </summary>
    Public Sub SetBitangents(bitangents As List(Of SysNumerics.Vector3)) Implements IShapeGeometry.SetBitangents
        _tri.SetTangents(bitangents)
    End Sub

    Public Sub SetUVs(uvs As List(Of TexCoord)) Implements IShapeGeometry.SetUVs
        ' NiflySharp BSTriShape.SetUVs takes List<Vector3> (U, V, ignored Z).  TexCoord is a
        ' 2-component struct so we project it into a Vector3 with Z=0 — same convention used
        ' in InjectToTrishape today.
        Dim asVec3 As New List(Of SysNumerics.Vector3)(uvs.Count)
        For Each uv In uvs
            asVec3.Add(New SysNumerics.Vector3(uv.U, uv.V, 0))
        Next
        _tri.SetUVs(asVec3)
    End Sub

    Public Sub SetVertexColors(colors As List(Of Color4)) Implements IShapeGeometry.SetVertexColors
        _tri.SetVertexColors(colors)
    End Sub

    Public Sub SetEyeData(eyeData As List(Of Single)) Implements IShapeGeometry.SetEyeData
        _tri.SetEyeData(eyeData)
    End Sub

    Public Sub SetTriangles(triangles As List(Of Triangle)) Implements IShapeGeometry.SetTriangles
        ' Delegates to the provenance-aware overload which handles SyncDynamicIfNeeded
        ' internally (called right after tri.SetTriangles, before any metadata redistribution).
        SetTriangles(triangles, Nothing)
    End Sub

    Public Sub SetTriangles(triangles As List(Of Triangle), provenance As TriangleRemap) Implements IShapeGeometry.SetTriangles
        ' Snapshot OLD metadata BEFORE writing — needed for redistribution because
        ' tri.SetTriangles updates _numTriangles and may invalidate offsets.
        Dim meshLod = TryCast(_tri, BSMeshLODTriShape)
        Dim subIndex = TryCast(_tri, BSSubIndexTriShape)
        Dim oldSegments As List(Of BSGeometrySegmentData) = Nothing
        Dim oldLOD0 As Integer = 0
        Dim oldLOD1 As Integer = 0
        If subIndex IsNot Nothing AndAlso subIndex.Segments IsNot Nothing Then
            oldSegments = subIndex.Segments.ToList()
        End If
        If meshLod IsNot Nothing Then
            oldLOD0 = CInt(meshLod.LOD0Size)
            oldLOD1 = CInt(meshLod.LOD1Size)
        End If

        ' ─── BSMeshLODTriShape: tier-preserving reorder (no longer lossy) ───
        ' Classify each new triangle by which old LOD tier its source fell in, then reorder
        ' the new triangle list so all LOD0 come first, then LOD1, then LOD2.  Write the
        ' reordered triangles to the block and set LOD sizes to the bucket counts.  Triangles
        ' with cross-shape or synthetic provenance default to LOD2 (the fallback "rendered at
        ' all distances" tier).  Preserves the LOD optimization across split/merge/zap instead
        ' of collapsing everything to LOD2 (previous canonical BS-OS behaviour).
        Dim writeTris As List(Of Triangle) = triangles
        Dim newLOD0Count As Integer = 0
        Dim newLOD1Count As Integer = 0
        Dim newLOD2Count As Integer = 0
        If meshLod IsNot Nothing AndAlso provenance IsNot Nothing Then
            Dim reordered = ReorderTrianglesByLODTier(triangles, provenance, oldLOD0, oldLOD1)
            writeTris = reordered.Triangles
            newLOD0Count = reordered.LOD0Count
            newLOD1Count = reordered.LOD1Count
            newLOD2Count = reordered.LOD2Count
        End If

        _tri.SetTriangles(Version, writeTris)
        SyncDynamicIfNeeded()

        ' Redistribute count-derived metadata when caller provided provenance.  Without
        ' provenance the metadata stays as-is (likely stale — caller's responsibility).
        If provenance Is Nothing Then Return

        If meshLod IsNot Nothing Then
            meshLod.LOD0Size = CUInt(newLOD0Count)
            meshLod.LOD1Size = CUInt(newLOD1Count)
            meshLod.LOD2Size = CUInt(newLOD2Count)
            Return
        End If

        ' ─── BSSubIndexTriShape: full Segments + SubSegmentDatas redistribution ───
        ' (BSSubIndex and BSMeshLOD are mutually exclusive — a shape is either one or the
        ' other, never both.  No LOD reorder applied to BSSubIndex triangles.)
        If subIndex IsNot Nothing AndAlso oldSegments IsNot Nothing AndAlso oldSegments.Count > 0 Then
            Dim newSegments = RedistributeSegments(oldSegments, provenance, triangles.Count)
            subIndex.Segments = newSegments
            ' Align the parallel _segmentData (SSF + PerSegmentData records) so the NIF
            ' block stays internally consistent.
            AlignSubIndexSegmentData(subIndex, newSegments)
        End If
    End Sub

    ''' <summary>
    ''' Result of <see cref="ReorderTrianglesByLODTier"/>: triangles reordered into
    ''' [LOD0 tier][LOD1 tier][LOD2 tier] order, plus the count per bucket so the caller can
    ''' set the corresponding <c>LOD0Size</c>/<c>LOD1Size</c>/<c>LOD2Size</c> fields.
    ''' </summary>
    Friend Structure LODReorderResult
        Public Triangles As List(Of Triangle)
        Public LOD0Count As Integer
        Public LOD1Count As Integer
        Public LOD2Count As Integer
    End Structure

    ''' <summary>Re-agrupa los triangulos por el nivel de LOD en que caia su indice VIEJO y los emite en orden
    ''' [LOD0][LOD1][LOD2], para que los campos LOD0Size/1/2Size referencien rangos contiguos. Lo llama
    ''' <see cref="SetTriangles"/> para BSMeshLODTriShape y lo reusa NiTriShapeGeometry para BSLODTriShape.
    ''' <para>Clasificacion: fuente no sintetica de la misma shape (OldIdx &gt;= 0) va por su rango viejo;
    ''' cross-shape (donante de un merge) y sintetico van a LOD2. LOD2 es el fallback porque se dibuja a todas
    ''' las distancias: un triangulo que no se puede ubicar queda visible siempre, preservando la correccion
    ''' geometrica a costa de algo de optimizacion.</para></summary>
    Friend Shared Function ReorderTrianglesByLODTier(
            triangles As List(Of Triangle),
            provenance As TriangleRemap,
            oldLOD0Size As Integer,
            oldLOD1Size As Integer) As LODReorderResult

        Dim oldLOD0End As Integer = oldLOD0Size
        Dim oldLOD1End As Integer = oldLOD0Size + oldLOD1Size

        Dim bucket0 As New List(Of Triangle)()
        Dim bucket1 As New List(Of Triangle)()
        Dim bucket2 As New List(Of Triangle)(triangles.Count)

        For i = 0 To triangles.Count - 1
            Dim src = provenance.Sources(i)
            Dim tier As Integer = 2  ' default: cross-shape / synthetic → LOD2
            If src.Shape Is Nothing AndAlso src.OldIdx >= 0 Then
                If src.OldIdx < oldLOD0End Then
                    tier = 0
                ElseIf src.OldIdx < oldLOD1End Then
                    tier = 1
                Else
                    tier = 2
                End If
            End If
            Select Case tier
                Case 0 : bucket0.Add(triangles(i))
                Case 1 : bucket1.Add(triangles(i))
                Case Else : bucket2.Add(triangles(i))
            End Select
        Next

        Dim reordered As New List(Of Triangle)(triangles.Count)
        reordered.AddRange(bucket0)
        reordered.AddRange(bucket1)
        reordered.AddRange(bucket2)

        Return New LODReorderResult With {
            .Triangles = reordered,
            .LOD0Count = bucket0.Count,
            .LOD1Count = bucket1.Count,
            .LOD2Count = bucket2.Count
        }
    End Function

    ''' <summary>
    ''' Semantic representation of a BSSubIndexTriShape sub-segment that survives
    ''' triangle-list mutations.  Mirrors nifly's NifSubSegmentInfo (Geometry.hpp).
    ''' </summary>
    Public Class NifSubSegmentInfo
        ''' <summary>
        ''' Unique non-negative integer identifying this sub-segment among all parents
        ''' and sub-segments of the shape.  Used as a value in triParts.  Not stored in
        ''' the file.
        ''' </summary>
        Public Property PartID As Integer = 0
        Public Property UserSlotID As UInteger = 0
        Public Property Material As UInteger = &HFFFFFFFFUI
        Public Property ExtraData As New List(Of Single)
    End Class

    ''' <summary>
    ''' Semantic representation of a BSSubIndexTriShape parent segment.  Mirrors nifly's
    ''' NifSegmentInfo (Geometry.hpp).
    ''' </summary>
    Public Class NifSegmentInfo
        Public Property PartID As Integer = 0
        Public Property Subs As New List(Of NifSubSegmentInfo)
    End Class

    ''' <summary>
    ''' Semantic representation of an entire BSSubIndexTriShape segmentation.  Mirrors
    ''' nifly's NifSegmentationInfo (Geometry.hpp).  The intent: extract segmentation
    ''' as semantic data with stable per-triangle partID assignments, mutate triangles
    ''' arbitrarily (split/merge/zap), then re-apply the segmentation by passing the new
    ''' triParts mapping.  Identical contract to nifly so cross-app behavior matches.
    ''' </summary>
    Public Class NifSegmentationInfo
        Public Property Segs As New List(Of NifSegmentInfo)
        Public Property SsfFile As String = ""
    End Class

    ''' <summary>
    ''' Result of <see cref="GetSegmentation(IShapeGeometry)"/>: semantic segmentation
    ''' info paired with the per-triangle partition map.  Returned as a record so callers
    ''' don't have to deal with VB.NET ByRef-Out parameter idioms.
    ''' </summary>
    Public Structure SegmentationSnapshot
        Public Info As NifSegmentationInfo
        Public TriParts As List(Of Integer)

        ''' <summary>True when the source shape had no segmentation data
        ''' (non-BSSubIndex, or BSSubIndex with zero parent segments).</summary>
        Public ReadOnly Property IsEmpty As Boolean
            Get
                Return Info Is Nothing OrElse Info.Segs.Count = 0
            End Get
        End Property
    End Structure

    ''' <summary>
    ''' Adapter-friendly extraction of BSSubIndexTriShape segmentation as semantic info +
    ''' per-triangle partID assignments.  Returns an empty snapshot if the shape isn't
    ''' BSSubIndex (NiTriShape, plain BSTriShape, BSMeshLOD without dismember).
    ''' </summary>
    Public Shared Function GetSegmentation(geom As IShapeGeometry) As SegmentationSnapshot
        Dim subIndex = TryCast(geom?.BackingShape, BSSubIndexTriShape)
        If subIndex Is Nothing Then
            Return New SegmentationSnapshot With {
                .Info = New NifSegmentationInfo(),
                .TriParts = New List(Of Integer)()
            }
        End If
        Return GetSegmentation(subIndex)
    End Function

    ''' <summary>
    ''' Adapter-friendly application of segmentation info + triParts to a shape.  No-op if
    ''' the shape isn't BSSubIndex.  Mutates Segments + SegmentData atomically — see the
    ''' BSSubIndexTriShape overload below for the full algorithm.
    ''' </summary>
    Public Shared Sub SetSegmentation(geom As IShapeGeometry,
                                       inf As NifSegmentationInfo,
                                       inTriParts As List(Of Integer))
        Dim subIndex = TryCast(geom?.BackingShape, BSSubIndexTriShape)
        If subIndex Is Nothing Then Return
        SetSegmentation(subIndex, inf, inTriParts, geom.Version)
    End Sub

    ''' <summary>Extrae la segmentacion de un BSSubIndexTriShape como info semantica + asignacion de partID por
    ''' triangulo. Replica linea por linea BSSubIndexTriShape::GetSegmentation de BS-OS.
    ''' <para>Camina Segments[] en orden: por cada segmento padre puebla triParts de sus triangulos con un
    ''' partID fresco y despues hace lo mismo con cada sub-segmento. arrayIndex sigue la posicion en el array
    ''' plano PerSegmentData (slot del padre + slots hijos) para poder traer userSlotID / material /
    ''' extraData.</para></summary>
    Public Shared Function GetSegmentation(subIndex As BSSubIndexTriShape) As SegmentationSnapshot
        Dim result As New SegmentationSnapshot With {
            .Info = New NifSegmentationInfo(),
            .TriParts = New List(Of Integer)()
        }

        Dim sd As BSGeometrySegmentSharedData = subIndex.SegmentData
        If sd.SSFFile IsNot Nothing Then result.Info.SsfFile = If(sd.SSFFile.Content, "")

        Dim segments = subIndex.Segments
        If segments IsNot Nothing Then
            For si = 0 To segments.Count - 1
                result.Info.Segs.Add(New NifSegmentInfo())
            Next
        End If

        Dim numTris As Integer = subIndex.TriangleCount
        result.TriParts.Capacity = numTris
        For k = 0 To numTris - 1 : result.TriParts.Add(-1) : Next

        If segments Is Nothing Then Return result

        Dim partID As Integer = 0
        Dim arrayIndex As Integer = 0

        For si = 0 To segments.Count - 1
            Dim seg = segments(si)
            Dim startIndex As Integer = CInt(seg.StartIndex \ 3UI)
            Dim endIndex As Integer = Math.Min(numTris, startIndex + CInt(seg.NumPrimitives))

            For id = startIndex To endIndex - 1
                result.TriParts(id) = partID
            Next

            result.Info.Segs(si).PartID = partID
            partID += 1

            Dim subs = seg.SubSegment
            If subs IsNot Nothing AndAlso subs.Count > 0 Then
                For j = 0 To subs.Count - 1
                    Dim sub_ = subs(j)
                    startIndex = CInt(sub_.StartIndex \ 3UI)
                    endIndex = Math.Min(numTris, startIndex + CInt(sub_.NumPrimitives))
                    For id = startIndex To endIndex - 1
                        result.TriParts(id) = partID
                    Next

                    Dim subInfo As New NifSubSegmentInfo() With {.PartID = partID}
                    partID += 1
                    arrayIndex += 1

                    ' Pull metadata from PerSegmentData at arrayIndex.  BS-OS
                    ' Geometry.cpp reads at arrayIndex (post-increment); we
                    ' replicate exactly.  If PerSegmentData is malformed (size inconsistent
                    ' with parent+child layout) the next line throws on bounds — corrupt
                    ' input must surface, not be hidden.
                    Dim rec = sd.PerSegmentData(arrayIndex)
                    ' BS-OS clamps userSlotID < 30 to 0 on read (Geometry.cpp).
                    ' Replicate the same clamp so round-trips preserve canonical values.
                    subInfo.UserSlotID = If(rec.UserIndex < 30UI, 0UI, rec.UserIndex)
                    subInfo.Material = rec.BoneID
                    If rec.CutOffsets IsNot Nothing Then
                        subInfo.ExtraData = New List(Of Single)(rec.CutOffsets)
                    End If
                    result.Info.Segs(si).Subs.Add(subInfo)
                Next
            End If
            arrayIndex += 1
        Next

        Return result
    End Function

    ''' <summary>Biped objects occupied by a BSSubIndexTriShape's segmentation. The "biped object"
    ''' (the slot index that drives head-part/headwear occlusion, like Skyrim's body part types) is
    ''' <c>ps.UserIndex</c> for each <c>PerSegmentData</c> record whose <c>BoneID != 0xFFFFFFFF</c>
    ''' (when BoneID IS 0xFFFFFFFF the UserIndex instead points at a parent segment, per nif.xml
    ''' BSGeometryPerSegmentSharedData). Returns an empty set if the shape has no segment data.
    ''' Pure read — no mutation. Shared by the render-path occlusion (MainForm.CandidateBiped30Only)
    ''' and the offline bake (FaceGenBuilder) so both compute the same biped slot footprint.</summary>
    Public Shared Function GetBipedObjects(subIndex As BSSubIndexTriShape) As HashSet(Of UInteger)
        Dim result As New HashSet(Of UInteger)()
        If subIndex Is Nothing Then Return result
        Dim sd As BSGeometrySegmentSharedData = subIndex.SegmentData
        If sd.PerSegmentData Is Nothing Then Return result
        For Each ps In sd.PerSegmentData
            If ps.BoneID <> &HFFFFFFFFUI Then result.Add(ps.UserIndex)
        Next
        Return result
    End Function

    ''' <summary>Arma los dos conjuntos de vertices de las particiones de pelo del FaceGen en UN solo recorrido:
    ''' <paramref name="v30"/> = vertices tocados por un sub-segmento biped-30 (Hair Top) y
    ''' <paramref name="v31"/> = por uno biped-31 (Hair Long). Los del anillo de borde aparecen en LOS DOS, y
    ''' los callers restan un conjunto del otro para descartarlo y no rasgar ninguna particion.
    ''' <para>Derivacion identica a <see cref="GetSegmentation(BSSubIndexTriShape)"/>: mapa partID -> UserSlotID
    ''' sobre cada sub-segmento y ruteo de los 3 vertices de cada triangulo segun ese slot. False (ambos
    ''' vacios) si la shape no tiene segmentacion o no es BSSubIndex. Lectura pura.</para></summary>
    Private Shared Function BuildHairPartitionVertexSets(subIndex As BSSubIndexTriShape,
                                                         ByRef v30 As HashSet(Of Integer),
                                                         ByRef v31 As HashSet(Of Integer)) As Boolean
        v30 = New HashSet(Of Integer)()
        v31 = New HashSet(Of Integer)()
        If subIndex Is Nothing Then Return False

        Dim snap = GetSegmentation(subIndex)
        If snap.IsEmpty Then Return False

        ' partID → UserSlotID. Only sub-segments carry a biped UserSlotID; parent segments do not
        ' (their partID maps to no biped slot). Triangles assigned to a parent partID therefore fall
        ' into neither set and are ignored, which is correct — they are not part of the 30/31 split.
        Dim partToSlot As New Dictionary(Of Integer, UInteger)()
        For Each seg In snap.Info.Segs
            For Each sub_ In seg.Subs
                partToSlot(sub_.PartID) = sub_.UserSlotID
            Next
        Next

        Dim tris = subIndex.Triangles
        If tris Is Nothing Then Return False

        Dim n As Integer = Math.Min(snap.TriParts.Count, tris.Count)
        For ti = 0 To n - 1
            Dim pid As Integer = snap.TriParts(ti)
            Dim slot As UInteger
            If pid < 0 OrElse Not partToSlot.TryGetValue(pid, slot) Then Continue For
            If slot <> 30UI AndAlso slot <> 31UI Then Continue For
            Dim t = tris(ti)
            Dim target = If(slot = 30UI, v30, v31)
            target.Add(CInt(t.V1))
            target.Add(CInt(t.V2))
            target.Add(CInt(t.V3))
        Next
        Return True
    End Function

    ''' <summary>Vertices de la particion "top" del pelo que NO comparte con la "long", o sea
    ''' <c>v(biped30) - v(biped31)</c>. El render/export lo usa para zapear solo la corona cuando el headwear
    ''' cubre el slot 30 pero no el 31: una gorra tapa la corona y las melenas siguen a la vista. Restar el
    ''' conjunto de 31 conserva el anillo de borde compartido, asi que la malla larga no se rasga.
    ''' <para>Es propiedad estable de la segmentacion (no depende de pose ni morph): el caller DEBERIA cachearlo
    ''' por shape. Conjunto vacio si no hay segmentacion, no hay particion 30 o no es BSSubIndex.</para></summary>
    Public Shared Function GetTopOnlyVertexIndices(subIndex As BSSubIndexTriShape) As HashSet(Of Integer)
        Dim v30 As HashSet(Of Integer) = Nothing
        Dim v31 As HashSet(Of Integer) = Nothing
        BuildHairPartitionVertexSets(subIndex, v30, v31)
        ' v30 − v31: drop the shared border ring so the long partition stays watertight.
        v30.ExceptWith(v31)
        Return v30
    End Function

    ''' <summary>Espejo de <see cref="GetTopOnlyVertexIndices(BSSubIndexTriShape)"/>: los vertices de la
    ''' particion "long" que no comparte con la "top", <c>v(biped31) - v(biped30)</c>. Se usa para zapear solo
    ''' las melenas del HAIRLINE (extra HNAM) cuando el headwear cubre el slot 30 y no el 31: el hairline es el
    ''' complemento inverso del principal, asi que bajo una gorra se esconde su parte larga y su borde de frente
    ''' sigue visible. Restar el conjunto de 30 mantiene el anillo compartido y la particion top estanca.
    ''' <para>Propiedad estable de la segmentacion: el caller DEBERIA cachearlo por shape. Vacio si no hay
    ''' segmentacion, no hay particion 31 o no es BSSubIndex.</para></summary>
    Public Shared Function GetLongOnlyVertexIndices(subIndex As BSSubIndexTriShape) As HashSet(Of Integer)
        Dim v30 As HashSet(Of Integer) = Nothing
        Dim v31 As HashSet(Of Integer) = Nothing
        BuildHairPartitionVertexSets(subIndex, v30, v31)
        ' v31 − v30: drop the shared border ring so the top partition stays watertight.
        v31.ExceptWith(v30)
        Return v31
    End Function

    ''' <summary>Huella de biped object por triangulo: <c>result(ti)</c> es el slot biped (el UserSlotID del
    ''' sub-segmento al que pertenece el triangulo) o <b>-1</b> cuando su particion es un segmento PADRE (los
    ''' padres no llevan slot) o la shape no tiene segmentacion. El largo es TriangleCount y todo triangulo
    ''' pasado TriParts.Count queda en -1.
    ''' <para>Derivacion identica a <see cref="BuildHairPartitionVertexSets"/>: mapa partID -> UserSlotID sobre
    ''' los sub-segmentos (solo ellos lo llevan) y luego el partID de cada triangulo. Totalmente general sobre
    ''' biped objects: ningun numero de slot esta hardcodeado, salen del record. Lectura pura y estable, asi que
    ''' los callers PUEDEN cachearla por shape; alimenta el filtro de oclusion per-segmento del render.</para></summary>
    Public Shared Function GetTriangleBipedObjects(subIndex As BSSubIndexTriShape) As Integer()
        If subIndex Is Nothing Then Return Array.Empty(Of Integer)()

        Dim snap = GetSegmentation(subIndex)
        If snap.IsEmpty Then Return Array.Empty(Of Integer)()

        Dim triCount As Integer = subIndex.TriangleCount
        If triCount <= 0 Then Return Array.Empty(Of Integer)()

        ' partID → UserSlotID. Only sub-segments carry a biped UserSlotID; parent segments do not,
        ' so triangles assigned to a parent partID stay -1 (no sub-segment biped slot).
        Dim partToSlot As New Dictionary(Of Integer, UInteger)()
        For Each seg In snap.Info.Segs
            For Each sub_ In seg.Subs
                partToSlot(sub_.PartID) = sub_.UserSlotID
            Next
        Next

        Dim result(triCount - 1) As Integer
        For ti = 0 To triCount - 1
            result(ti) = -1
        Next

        Dim n As Integer = Math.Min(snap.TriParts.Count, triCount)
        For ti = 0 To n - 1
            Dim pid As Integer = snap.TriParts(ti)
            Dim slot As UInteger
            If pid >= 0 AndAlso partToSlot.TryGetValue(pid, slot) Then
                result(ti) = CInt(slot)
            End If
        Next
        Return result
    End Function

    ''' <summary>Oclusion per-segmento SIMETRICA y engine-faithful: <c>result(ti)</c> es True (oculto) si el
    ''' slot biped del segmento del triangulo esta cubierto por un item equipado, segun el resolver
    ''' Fallout4.exe 0x14035E0B9. Un triangulo cuyo segmento no lleva slot (padre, o sin segmentacion) nunca se
    ''' oculta.
    ''' <para>Convencion de <paramref name="coveredSlotsMask"/>: <b>bit (N-30) = slot biped N</b>, la MISMA que
    ''' SlotConflictResolver.OccupiedSlots y el formato de biped objects. Los biped objects van de 30 a 61, asi
    ''' que solo esos pueden ocultar; un slot fuera de rango (incluido el -1) deja el triangulo visible.</para>
    ''' <para>Rango N+100 (130..161), la "variante ocupada": el resolver 0x14035E344 cambia a la geometria
    ''' con-item de un segmento cuando el slot base N esta ocupado. Ese triangulo es el INVERSO del base: se
    ''' MUESTRA solo cuando su slot base esta cubierto y se oculta si no. Es el swap del antebrazo del Pipboy.</para>
    ''' <para>Lectura pura; el resultado es funcion de (segmentacion, coveredSlotsMask), asi que se puede cachear
    ''' por ese par.</para></summary>
    Public Shared Function ComputeHiddenTriangles(subIndex As BSSubIndexTriShape, coveredSlotsMask As UInteger, Optional ownSlotsMask As UInteger = 0UI) As Boolean()
        Dim tb = GetTriangleBipedObjects(subIndex)
        If tb.Length = 0 Then Return Array.Empty(Of Boolean)()

        Dim result(tb.Length - 1) As Boolean
        For ti = 0 To tb.Length - 1
            Dim b As Integer = tb(ti)
            If b >= 30 AndAlso b <= 61 Then
                Dim bit As UInteger = 1UI << (b - 30)
                ' A segment tagged with a slot the item does NOT occupy (FOREIGN, and not the Pipboy-60
                ' occluder slot) goes through the engine coverage-key branch (resolver 0x14035E243-0x14035E289),
                ' NOT the self-exclude/occupancy branch. With the default coverage-key ("") that branch SHOWS
                ' the segment only when the foreign slot is OCCUPIED by another item and HIDES it when the slot
                ' is empty — the INVERSE of the occlusion polarity. <paramref name="ownSlotsMask"/>=0 (unknown
                ' owner) or slot 60 (Pipboy occluder-order) skip this and use the occlusion polarity.
                ' No-op on vanilla: measured 0 of 1267 vanilla ARMA meshes carry a foreign (tag not in own
                ' BOD2, !=60) segment, so this branch never fires on vanilla content.
                If ownSlotsMask <> 0UI AndAlso b <> 60 AndAlso (ownSlotsMask And bit) = 0UI Then
                    result(ti) = (coveredSlotsMask And bit) = 0UI          ' foreign: SHOW iff slot occupied
                Else
                    result(ti) = (coveredSlotsMask And bit) <> 0UI         ' self / Pipboy-60: HIDE iff covered
                End If
            ElseIf b >= 130 AndAlso b <= 161 Then
                ' N+100 occupied-variant (engine resolver 0x14035E344): the "with-item" geometry is SHOWN only
                ' when its base slot (b-100) is covered, HIDDEN otherwise. b-130 = (b-100)-30 = base slot bit.
                result(ti) = (coveredSlotsMask And (1UI << (b - 130))) = 0UI
            Else
                result(ti) = False
            End If
        Next
        Return result
    End Function

    ''' <summary>Reconstruye la segmentacion de un BSSubIndexTriShape desde la info semantica + los partID por
    ''' triangulo. Replica linea por linea BSSubIndexTriShape::SetSegmentation de BS-OS: renumera los partID de
    ''' forma monotona en el orden padre/hijo, ordena los triangulos por partID con un bucket sort ESTABLE
    ''' (el canonico usa `std::stable_sort`; ver el comentario del sitio), computa el triangulo
    ''' inicial de cada particion y arma segments + dataRecords + arrayIndices. Los triangulos se reordenan, asi
    ''' que los rangos StartIndex/NumPrimitives quedan contiguos por particion.
    ''' <para>Es el punto de entrada autoritativo: muta Segments y SegmentData atomicamente por los setters
    ''' publicos de NiflySharp, sin dejar una combinacion rancia que corromperia PerSegmentData cuando el Sync
    ''' redimensione contra un NumSegments/TotalSegments desactualizado.</para></summary>
    Public Shared Sub SetSegmentation(subIndex As BSSubIndexTriShape,
                                       inf As NifSegmentationInfo,
                                       inTriParts As List(Of Integer),
                                       nifVersion As NiVersion)
        Dim numTris As Integer = subIndex.TriangleCount
        If inTriParts.Count <> numTris Then
            Throw New ArgumentException(
                $"inTriParts size ({inTriParts.Count}) must match shape triangle count ({numTris}).")
        End If

        ' Renumber partition IDs so they're monotonically increasing in inf order
        ' (Geometry.cpp).
        Dim newPartID As Integer = 0
        Dim oldToNewPartIDs As New List(Of Integer)()
        For Each seg In inf.Segs
            EnsureCapacity(oldToNewPartIDs, seg.PartID + 1)
            oldToNewPartIDs(seg.PartID) = newPartID
            newPartID += 1
            For Each sub_ In seg.Subs
                EnsureCapacity(oldToNewPartIDs, sub_.PartID + 1)
                oldToNewPartIDs(sub_.PartID) = newPartID
                newPartID += 1
            Next
        Next

        Dim triParts As Integer() = New Integer(numTris - 1) {}
        For i = 0 To numTris - 1
            If inTriParts(i) >= 0 Then
                triParts(i) = oldToNewPartIDs(inTriParts(i))
            End If
        Next

        ' Sort triangle indices by partition ID — Geometry.cpp:1442-1449 usa `std::stable_sort`, o sea
        ' que hay que PRESERVAR el orden relativo de los triángulos DENTRO de cada partición.
        ' ⛔ ACÁ HABÍA UN `Array.Sort(triInds, Comparison)` con el comentario "(stable)": esa sobrecarga
        ' es introsort y está documentada como INESTABLE, así que el comentario decía lo contrario de
        ' lo que el código hacía.
        ' Bucket sort en vez de `OrderBy` (que sí sería estable): los partID son densos en
        ' [0, newPartID) por el renumerado de arriba, así que esto es O(n+k) y sin allocations de LINQ
        ' — `System.Linq` no está importado en este proyecto. Recorrer `i` ascendente y emitir en orden
        ' dentro de cada casillero ES la definición de estabilidad.
        Dim conteo As Integer() = New Integer(newPartID) {}
        For i = 0 To numTris - 1 : conteo(triParts(i)) += 1 : Next
        Dim inicio As Integer() = New Integer(newPartID) {}
        Dim acum As Integer = 0
        For p = 0 To newPartID - 1
            inicio(p) = acum
            acum += conteo(p)
        Next
        Dim triInds As Integer() = New Integer(numTris - 1) {}
        For i = 0 To numTris - 1
            Dim p = triParts(i)
            triInds(inicio(p)) = i
            inicio(p) += 1
        Next

        ' Reorder triangles in the shape to match the new partition order.
        Dim oldTris = subIndex.Triangles
        Dim newTris As New List(Of Triangle)(numTris)
        For i = 0 To numTris - 1
            newTris.Add(oldTris(triInds(i)))
        Next
        subIndex.SetTriangles(nifVersion, newTris)

        ' Find first triangle of each partition: partTriInds[p] = index of first triangle
        ' of partition p (Geometry.cpp).
        Dim partTriInds As Integer() = New Integer(newPartID) {}
        Dim nextPartID As Integer = 0
        For i = 0 To numTris - 1
            Do While triParts(triInds(i)) >= nextPartID
                partTriInds(nextPartID) = i
                nextPartID += 1
            Loop
        Next
        Do While nextPartID <= newPartID
            partTriInds(nextPartID) = numTris
            nextPartID += 1
        Loop

        ' Build segments + PerSegmentData + arrayIndices in canonical interleaved order
        ' (Geometry.cpp).
        Dim newSegments As New List(Of BSGeometrySegmentData)()
        Dim newPerSegmentData As New List(Of BSGeometryPerSegmentSharedData)()
        Dim arrayIndices As New List(Of UInteger)()
        Dim parentArrayIndex As UInteger = 0
        Dim segmentIndex As UInteger = 0
        Dim partID As Integer = 0

        For Each seg In inf.Segs
            Dim childCount As Integer = seg.Subs.Count
            Dim segNumPrim As Integer = partTriInds(partID + childCount + 1) - partTriInds(partID)
            Dim segStartIndex As UInteger = CUInt(partTriInds(partID)) * 3UI

            Dim newSeg As New BSGeometrySegmentData() With {
                .Flags = 0,
                .StartIndex = segStartIndex,
                .NumPrimitives = CUInt(segNumPrim),
                .ParentArrayIndex = &HFFFFFFFFUI,
                .NumSubSegments = CUInt(childCount),
                .SubSegment = New List(Of BSGeometrySubSegment)(childCount)
            }
            partID += 1

            ' Parent dataRecord — Geometry.cpp: userSlotID = segmentIndex,
            ' material = 0xFFFFFFFF (default), no extraData.
            newPerSegmentData.Add(New BSGeometryPerSegmentSharedData() With {
                .UserIndex = segmentIndex,
                .BoneID = &HFFFFFFFFUI,
                .NumCutOffsets = 0UI,
                .CutOffsets = New List(Of Single)()
            })
            arrayIndices.Add(parentArrayIndex)

            Dim subSegmentNumber As UInteger = 1UI
            For Each sub_ In seg.Subs
                Dim subNumPrim As Integer = partTriInds(partID + 1) - partTriInds(partID)
                Dim subStartIndex As UInteger = CUInt(partTriInds(partID)) * 3UI

                newSeg.SubSegment.Add(New BSGeometrySubSegment() With {
                    .StartIndex = subStartIndex,
                    .NumPrimitives = CUInt(subNumPrim),
                    .ParentArrayIndex = parentArrayIndex,
                    .Unused = 0UI
                })
                partID += 1

                ' Sub dataRecord — Geometry.cpp: userSlotID gets renumbered to
                ' subSegmentNumber if the original was < 30 (canonical body-part slot
                ' range), otherwise preserved as-is.
                Dim subUserSlot As UInteger
                If sub_.UserSlotID < 30UI Then
                    subUserSlot = subSegmentNumber
                    subSegmentNumber += 1UI
                Else
                    subUserSlot = sub_.UserSlotID
                End If

                Dim cutOffsets As New List(Of Single)(If(sub_.ExtraData, New List(Of Single)))
                newPerSegmentData.Add(New BSGeometryPerSegmentSharedData() With {
                    .UserIndex = subUserSlot,
                    .BoneID = sub_.Material,
                    .NumCutOffsets = CUInt(cutOffsets.Count),
                    .CutOffsets = cutOffsets
                })
            Next

            newSegments.Add(newSeg)
            parentArrayIndex += CUInt(childCount) + 1UI
            segmentIndex += 1UI
        Next

        ' Atomic write-back via NiflySharp's public SegmentData property
        ' (BSSubIndexTriShape.cs).  No reflection needed — the property's setter writes
        ' the struct value directly into _segmentData.
        Dim sd As BSGeometrySegmentSharedData = subIndex.SegmentData
        sd.NumSegments = CUInt(newSegments.Count)
        sd.TotalSegments = parentArrayIndex
        sd.SegmentStarts = arrayIndices
        sd.PerSegmentData = newPerSegmentData
        ' SSFFile preserved unless caller supplied one in inf.
        If Not String.IsNullOrEmpty(inf.SsfFile) Then
            If sd.SSFFile Is Nothing Then sd.SSFFile = New NiString2()
            sd.SSFFile.Content = inf.SsfFile
        End If
        subIndex.SegmentData = sd
        subIndex.Segments = newSegments
    End Sub

    ''' <summary>Actualizacion interna con politica de preservacion, usada por <see cref="SetTriangles"/> cuando
    ''' RedistributeSegments produce una lista con el mismo conteo estructural que la original (caminos de zap y
    ''' split, que preservan la cantidad de padres y sub-segmentos aunque algun NumPrimitives caiga a 0).
    ''' Actualiza solo los campos derivados del conteo y los SegmentStarts, y preserva PerSegmentData y SSFFile.
    ''' <para>Es el camino rapido donde el layout estructural es invariante; el merge usa el round-trip completo
    ''' GetSegmentation/SetSegmentation porque ahi los conteos crecen. Siempre reescribe SegmentStarts como
    ''' parentArrayIndex acumulado, segun BS-OS.</para></summary>
    Private Shared Sub AlignSubIndexSegmentData(subIndex As BSSubIndexTriShape, newSegments As List(Of BSGeometrySegmentData))
        Dim sd As BSGeometrySegmentSharedData = subIndex.SegmentData

        Dim totalSubSegments As UInteger = 0
        Dim starts As New List(Of UInteger)(newSegments.Count)
        Dim parentArrayIndex As UInteger = 0
        For Each seg In newSegments
            starts.Add(parentArrayIndex)
            Dim childCount As UInteger = CUInt(If(seg.SubSegment Is Nothing, 0, seg.SubSegment.Count))
            parentArrayIndex += childCount + 1UI
            totalSubSegments += childCount
        Next
        Dim newTotal As UInteger = CUInt(newSegments.Count) + totalSubSegments

        If sd.PerSegmentData IsNot Nothing AndAlso sd.PerSegmentData.Count <> CInt(newTotal) Then
            Throw New InvalidOperationException(
                $"AlignSubIndexSegmentData preserve-policy invariant violated: existing " &
                $"PerSegmentData count ({sd.PerSegmentData.Count}) differs from new total ({newTotal}).  " &
                "RedistributeSegments must preserve parent and sub-segment counts in this code path; " &
                "if counts change, route the update through SetSegmentation instead.")
        End If

        sd.NumSegments = CUInt(newSegments.Count)
        sd.TotalSegments = newTotal
        sd.SegmentStarts = starts

        subIndex.SegmentData = sd
    End Sub

    ''' <summary>
    ''' Grows <paramref name="list"/> with default values until its Count is at least
    ''' <paramref name="minCount"/>.  Local helper so SetSegmentation's partID renumber
    ''' loop reads cleaner than the Do/Loop While idiom that VB.NET requires for
    ''' incremental capacity expansion.
    ''' </summary>
    Private Shared Sub EnsureCapacity(list As List(Of Integer), minCount As Integer)
        Do While list.Count < minCount
            list.Add(0)
        Loop
    End Sub

    ''' <summary>
    ''' Friend visibility: NiTriShapeGeometry.BSSegmentedTriShape branch reuses this exact
    ''' redistribution logic (BSSegmented uses the same BSGeometrySegmentData struct, just
    ''' on a different host class).  Keeping a single implementation avoids drift between
    ''' the two shape families.
    ''' </summary>
    Friend Shared Function RedistributeSegments(oldSegments As List(Of BSGeometrySegmentData),
                                                  provenance As TriangleRemap,
                                                  newTriCount As Integer) As List(Of BSGeometrySegmentData)
        Dim result As New List(Of BSGeometrySegmentData)(oldSegments.Count)
        Dim cumulativeTriIdx As Integer = 0

        ' ─── Phase 1: index provenance by old triangle idx — O(N) ───
        ' Build a map oldTriIdx → count of new survivors coming from that old idx.  Also
        ' count synthetic entries (Shape=Nothing AND OldIdx<0) in one pass for the fallback
        ' segment at the end.  Previously the segment loop did O(N*M) — for each segment it
        ' rescanned all provenance entries.  Dict lookup drops it to O(N + sum of segment
        ' ranges) which is O(N + oldTotal) ≈ O(N + M) for typical NIFs.
        Dim survivorsByOld As New Dictionary(Of Integer, Integer)(provenance.Sources.Count)
        Dim syntheticUncovered As Integer = 0
        For i = 0 To provenance.Sources.Count - 1
            Dim src = provenance.Sources(i)
            If src.Shape IsNot Nothing Then Continue For  ' cross-shape: handled by MergeMetadataAfterApply
            If src.OldIdx < 0 Then
                syntheticUncovered += 1
                Continue For
            End If
            Dim existing As Integer
            If survivorsByOld.TryGetValue(src.OldIdx, existing) Then
                survivorsByOld(src.OldIdx) = existing + 1
            Else
                survivorsByOld(src.OldIdx) = 1
            End If
        Next

        ' ─── Phase 2: emit new segments by scanning each old range — O(oldTotal) ───
        For Each oldSeg In oldSegments
            Dim oldStartTri As Integer = CInt(oldSeg.StartIndex \ 3UI)
            Dim oldEndTri As Integer = oldStartTri + CInt(oldSeg.NumPrimitives)

            Dim survivors As Integer = 0
            For oldIdx = oldStartTri To oldEndTri - 1
                Dim cnt As Integer
                If survivorsByOld.TryGetValue(oldIdx, cnt) Then survivors += cnt
            Next

            Dim newSeg As New BSGeometrySegmentData() With {
                .Flags = oldSeg.Flags,
                .StartIndex = CUInt(cumulativeTriIdx) * 3UI,
                .NumPrimitives = CUInt(survivors),
                .ParentArrayIndex = oldSeg.ParentArrayIndex
            }

            ' SubSegments: same algorithm, scoped to the parent segment range, reuses the
            ' survivorsByOld dict (already built) so each sub-seg is O(subRange) — no N rescan.
            If oldSeg.SubSegment IsNot Nothing AndAlso oldSeg.SubSegment.Count > 0 Then
                newSeg.SubSegment = RedistributeSubSegments(oldSeg.SubSegment, survivorsByOld, CInt(newSeg.StartIndex \ 3UI))
                newSeg.NumSubSegments = CUInt(newSeg.SubSegment.Count)
            Else
                newSeg.SubSegment = New List(Of BSGeometrySubSegment)()
                newSeg.NumSubSegments = 0UI
            End If

            result.Add(newSeg)
            cumulativeTriIdx += survivors
        Next

        ' Runtime validation (was Debug.Assert — upgraded to hard throw so release builds
        ' also catch malformed provenance before the NIF gets written with inconsistent
        ' segment ranges that would make the game read out-of-bounds triangles).
        If cumulativeTriIdx > newTriCount Then
            Throw New InvalidOperationException(
                $"RedistributeSegments: cumulative survivors ({cumulativeTriIdx}) exceeds new triangle " &
                $"count ({newTriCount}).  Provenance map is malformed (duplicate oldIdx entries or " &
                "oldIdx values outside original triangle range).  Refusing to produce a segmented NIF " &
                "that would be read out-of-bounds by the dismember engine.")
        End If

        ' Synthetic-only fallback: new triangles with (Shape=Nothing AND OldIdx<0) are
        ' synthetic entries in same-shape provenance (e.g. a future caller that appends
        ' generated triangles to an existing shape).  Bundle those into a catch-all segment
        ' so the shape stays dismember-legal.  Cross-shape entries are NOT covered here —
        ' MergeShapesHelper.MergeMetadataAfterApply appends donor segments with proper
        ' offset; emitting a fallback + donor-append would double-count (PrimSum > TC bug
        ' caught by ShapeTypeValidator test C).
        If syntheticUncovered > 0 AndAlso cumulativeTriIdx + syntheticUncovered <= newTriCount Then
            Dim fallback As New BSGeometrySegmentData() With {
                .Flags = 0,
                .StartIndex = CUInt(cumulativeTriIdx) * 3UI,
                .NumPrimitives = CUInt(syntheticUncovered),
                .ParentArrayIndex = UInteger.MaxValue,  ' "no parent" sentinel (matches BS-OS SetDefaultSegments convention)
                .NumSubSegments = 0UI,
                .SubSegment = New List(Of BSGeometrySubSegment)()
            }
            result.Add(fallback)
        End If

        Return result
    End Function

    ''' <summary>
    ''' Sub-segment redistribution.  Signature differs from the public overload by accepting
    ''' the pre-built <paramref name="survivorsByOld"/> dictionary instead of walking
    ''' provenance again — the outer <see cref="RedistributeSegments"/> builds that dict
    ''' once and reuses it across all parent segments and their sub-segments.  Each sub-seg
    ''' is then O(subRange) instead of O(N).
    ''' </summary>
    Private Shared Function RedistributeSubSegments(oldSubSegments As List(Of BSGeometrySubSegment),
                                                     survivorsByOld As Dictionary(Of Integer, Integer),
                                                     newParentStartTri As Integer) As List(Of BSGeometrySubSegment)
        Dim result As New List(Of BSGeometrySubSegment)(oldSubSegments.Count)
        Dim cumulativeTriIdx As Integer = newParentStartTri

        For Each oldSub In oldSubSegments
            Dim oldStartTri As Integer = CInt(oldSub.StartIndex \ 3UI)
            Dim oldEndTri As Integer = oldStartTri + CInt(oldSub.NumPrimitives)

            Dim survivors As Integer = 0
            For oldIdx = oldStartTri To oldEndTri - 1
                Dim cnt As Integer
                If survivorsByOld.TryGetValue(oldIdx, cnt) Then survivors += cnt
            Next

            result.Add(New BSGeometrySubSegment() With {
                .StartIndex = CUInt(cumulativeTriIdx) * 3UI,
                .NumPrimitives = CUInt(survivors),
                .ParentArrayIndex = oldSub.ParentArrayIndex,
                .Unused = oldSub.Unused
            })
            cumulativeTriIdx += survivors
        Next

        Return result
    End Function

    Public Sub UpdateBounds() Implements IShapeGeometry.UpdateBounds
        _tri.UpdateBounds()
    End Sub

    ''' <summary>
    ''' Writes per-vertex bone influences into BSVertexData[].BoneIndices / BoneWeights
    ''' (FO4) or BSVertexDataSSE[].BoneIndices / BoneWeights (SSE).  Length of the packed
    ''' list and <paramref name="skinning"/>.VertexCount must match.  Each vertex's 4 slots
    ''' are written in order; missing slots in the input default to (idx=0, weight=0).
    ''' </summary>
    Public Sub SetSkinning(skinning As ShapeSkinningData) Implements IShapeGeometry.SetSkinning
        If skinning.BoneIndices Is Nothing OrElse skinning.BoneWeights Is Nothing Then Return
        Dim n As Integer = _tri.VertexCount
        ' Write-path failures must be detectable in RELEASE.  This used to be a Debug.Assert
        ' followed by a silent Return: in a Release build the assert is a no-op, so the caller
        ' believed the skin had been written while the NIF kept its OLD weights.  Refuse loudly
        ' instead — same policy as NiTriShapeGeometry.SetSkinning's validation block.
        If skinning.VertexCount <> n Then
            Throw New InvalidOperationException(
                $"SetSkinning: vertex count mismatch — shape has {n}, skinning has {skinning.VertexCount}.  " &
                "Refusing to write; the shape would silently retain its previous bone weights.")
        End If
        Const wpv As Integer = 4
        Dim inputWpv As Integer = If(skinning.WeightsPerVertex > 0, skinning.WeightsPerVertex, wpv)

        ' Copy directly from the input slice into each vertex's [InlineArray(4)] structs.
        ' Inputs wider than 4 are truncated; shorter inputs leave trailing slots at zero
        ' (the struct is reset to default before CopyFrom so prior contents don't bleed through).
        Dim copy As Integer = Math.Min(wpv, inputWpv)
        If Version.IsSSE Then
            Dim vd = _tri.VertexDataSSE
            ' Same reasoning as the VertexCount guard above: a silent Return here leaves the
            ' caller believing the skin was written.  Detectable in Release.
            If vd Is Nothing OrElse vd.Count <> n Then
                Throw New InvalidOperationException(
                    $"SetSkinning: BSVertexDataSSE unusable — expected {n} entries, got " &
                    $"{If(vd Is Nothing, "null", vd.Count.ToString())}.  Refusing to write.")
            End If
            For i = 0 To n - 1
                Dim v = vd(i)   ' struct copy (BSVertexDataSSE is a struct)
                ' Extract bone fields to direct locals before CopyFrom: VB.NET cannot guarantee
                ' that ByRef-Me extensions over a NESTED struct field of a local target the
                ' original field (silent temp-copy drops the write). With top-level locals the
                ' ByRef is unambiguous. The locals start zero-initialised so slots beyond `copy`
                ' (when inputWpv < 4) end up at zero without an extra padding pass.
                Dim bi As BoneIndices4
                Dim bw As BoneWeights4
                bi.CopyFrom(skinning.BoneIndices, i * inputWpv, copy)
                bw.CopyFrom(skinning.BoneWeights, i * inputWpv, copy)
                v.BoneIndices = bi
                v.BoneWeights = bw
                vd(i) = v   ' write back the modified struct
            Next
        Else
            Dim vd = _tri.VertexData
            ' See the SSE branch above — same silent-Return-on-a-write-path hazard.
            If vd Is Nothing OrElse vd.Count <> n Then
                Throw New InvalidOperationException(
                    $"SetSkinning: BSVertexData unusable — expected {n} entries, got " &
                    $"{If(vd Is Nothing, "null", vd.Count.ToString())}.  Refusing to write.")
            End If
            For i = 0 To n - 1
                Dim v = vd(i)
                ' See comment in the SSE branch above — same nested-ByRef caveat.
                Dim bi As BoneIndices4
                Dim bw As BoneWeights4
                bi.CopyFrom(skinning.BoneIndices, i * inputWpv, copy)
                bw.CopyFrom(skinning.BoneWeights, i * inputWpv, copy)
                v.BoneIndices = bi
                v.BoneWeights = bw
                vd(i) = v
            Next
        End If
        ' BSDynamicTriShape safety: CalcDynamicData() re-syncs the parallel Vector4 _vertices
        ' buffer (carries BitangentX in W).  SetSkinning only mutates BoneIndices/BoneWeights,
        ' which don't affect _vertices, but call SyncDynamicIfNeeded defensively so every
        ' write path on BSTriShapeGeometry leaves the dynamic buffer coherent.
        SyncDynamicIfNeeded()

        ' SSE-only: also rebuild NiSkinData from the SAME `skinning` so the inline skin and
        ' NiSkinData agree per-vertex (cpu_gpu_skinning_parity).  See RebuildNiSkinData.
        RebuildNiSkinData(skinning)
    End Sub

    ''' <summary>⛔ SYNC: las DOS codificaciones de skin de un BSTriShape de SSE tienen que quedar identicas. Un
    ''' BSTriShape de SSE lleva a la vez un skin INLINE y un <c>NiSkinData</c>; el render usa el inline, pero al
    ''' guardar NiflySharp reconstruye la <c>NiSkinPartition</c> desde el NiSkinData. Si solo se reescribe el
    ''' inline, el NiSkinData queda STALE (con indices de vertice previos a la compactacion), la particion lo
    ''' sigue y el NIF guardado skinnea mal in-game.
    ''' <para>El gate es por PRESENCIA DEL BLOQUE, no por version: FO4 no usa NiSkinInstance, asi que ahi esto
    ''' no corre y el camino inline-only queda igual. Los indices inline son LOCALES a skinInst.Bones y
    ''' BoneList[i] corresponde 1:1 a Bones[i], asi que el mapeo es directo.</para></summary>
    Private Sub RebuildNiSkinData(skinning As ShapeSkinningData)
        ' Gate on actual block presence (see method summary).  GetBlock(Of NiSkinInstance)
        ' returns Nothing for FO4's BSSkin_Instance → FO4 stays inline-only.
        Dim skinInst = _nif.GetBlock(Of NiSkinInstance)(_tri.SkinInstanceRef)
        If skinInst Is Nothing Then Return
        Dim skinData = _nif.GetBlock(skinInst.Data)
        If skinData Is Nothing OrElse skinData.BoneList Is Nothing Then Return

        ' OS-faithful: SetShapeBoneWeights (NifFile.cpp — sole writer of the flag)
        ' unconditionally sets skinData->hasVertWeights=true when it authors per-bone weights
        ' for an SSE edit (Anim.cpp, !isFO branch).  An SSE NiSkinData that loaded with
        ' HasVertexWeights=false (weights only in the partition) is normalized false→true by the
        ' edit-save.  Match it here: without the flag, NiSkinData.BeforeSync (NiSkinData.cs)
        ' forces NumVertices=0 and discards the weight arrays we populate below → weightless on
        ' disk.  Reached only for SSE (FO4 BSSkin_Instance returned early above).
        skinData.HasVertexWeights = True

        Dim n As Integer = skinning.VertexCount
        Dim wpv As Integer = If(skinning.WeightsPerVertex > 0, skinning.WeightsPerVertex, 4)

        ' Grow BoneList if a referenced local bone index is beyond the current palette.
        ' Mirrors MergeShapesHelper step 5b: add New BoneData() and re-init VertexWeights.
        ' (Normally numBones already matches skinInst.Bones, but a compaction step may have
        ' expanded the bone palette before the skin write reaches here.)
        Dim numBones As Integer = skinData.BoneList.Count
        Dim maxBoneIdx As Integer = -1
        Dim flatLen As Integer = n * wpv
        For i = 0 To flatLen - 1
            If i >= skinning.BoneIndices.Length OrElse i >= skinning.BoneWeights.Length Then Exit For
            Dim w As Single = CType(skinning.BoneWeights(i), Single)
            If w <= 0.0F Then Continue For
            Dim bIdx As Integer = CInt(skinning.BoneIndices(i))
            If bIdx > maxBoneIdx Then maxBoneIdx = bIdx
        Next
        Do While skinData.BoneList.Count <= maxBoneIdx
            ' CON su lista: un `New BoneData()` la trae en Nothing, y el rebuild de abajo limpia EN EL
            ' LUGAR. Sin esto, el primer hueso agregado revienta con NRE en el `.Clear()`.
            skinData.BoneList.Add(New BoneData() With {.VertexWeights = New List(Of BoneVertData)()})
        Loop
        numBones = skinData.BoneList.Count
        skinData.NumBones = CUInt(numBones)

        ' Limpia la lista de cada hueso EN EL LUGAR, igual que NiTriShapeGeometry.RebuildNiSkinData.
        ' Los dos encoders hacian lo contrario y el comentario de aca lo decia: "whichever is right,
        ' they should match". Unificados el 2026-08-22.
        '
        ' Por que `.Clear()` es seguro con `BoneData` siendo un STRUCT: el clon de un bloque de
        ' NiflySharp no copia la referencia, ALOCA UNA LISTA NUEVA
        ' (`BoneData.DeepClone()`: `copy.VertexWeights = new List<BoneVertData>(this.VertexWeights)`),
        ' asi que limpiar la de un shape clonado no toca la del original.
        ' ⛔ Los dos comentarios justificaban esto citando `DeepCopyHelper.IsValueTypeSelfContained`,
        ' que NO EXISTE: upstream borro ese archivo cuando reemplazo el deep-copy por reflexion por
        ' logica de clonado GENERADA (commit `f1f3404` del fork). La conclusion seguia siendo cierta,
        ' pero apoyada en un simbolo muerto — que es como una premisa se vuelve falsa sin que nadie
        ' se entere. La razon de arriba es la que se puede verificar hoy.
        For b = 0 To numBones - 1
            skinData.BoneList(b).VertexWeights.Clear()
        Next

        ' Pivot per-vertex 4-slot influences → per-bone (vertex, weight) entries.  Skip
        ' zero-weight slots so the per-bone lists stay sparse.  Half→Single weight conversion
        ' and CUShort vertex index match NiTriShapeGeometry's rebuild.
        For i = 0 To n - 1
            Dim vBase As Integer = i * wpv
            For j = 0 To wpv - 1
                Dim flatIdx As Integer = vBase + j
                If flatIdx >= skinning.BoneIndices.Length OrElse flatIdx >= skinning.BoneWeights.Length Then Continue For
                Dim w As Single = CType(skinning.BoneWeights(flatIdx), Single)
                If w <= 0.0F Then Continue For
                Dim bIdx As Integer = CInt(skinning.BoneIndices(flatIdx))
                ' Limite de la paleta. INALCANZABLE POR CONSTRUCCION hoy, y a proposito queda como throw y no
                ' como Continue For para que siga siendolo: bIdx < 0 es imposible porque BoneIndices es Byte(),
                ' y bIdx >= numBones tambien, porque el scan del principio del metodo recorre el MISMO espacio
                ' de indices con los MISMOS guards y hace crecer BoneList mas alla de maxBoneIdx.
                ' Con Continue For, si alguna de esas invariantes se rompiera aguas arriba, el peso se soltaria
                ' en silencio y el vertice renderizaria en su offset de bind pose. Los dos encoders comparten
                ' una politica: fallar, nunca degradar en silencio.
                If bIdx < 0 OrElse bIdx >= numBones Then
                    Throw New InvalidOperationException(
                        $"RebuildNiSkinData: vertex {i} slot {j} references bone palette index {bIdx} " &
                        $"outside the shape's BoneList (size {numBones}).  This would silently drop " &
                        "the weight and render the vertex at its bind-pose offset.")
                End If
                skinData.BoneList(bIdx).VertexWeights.Add(New BoneVertData() With {
                    .Index = CUShort(i),
                    .Weight = w
                })
            Next
        Next

        ' Update per-bone NumVertices to match the rebuilt lists (NiflySharp reads this on
        ' write; if stale, the binary output is truncated/corrupt).
        For b = 0 To numBones - 1
            Dim bd = skinData.BoneList(b)
            bd.NumVertices = CUShort(bd.VertexWeights.Count)
            skinData.BoneList(b) = bd
        Next
    End Sub

    ' ─────────────── Helpers ───────────────
    Private Shared Function DecodeBitangent(x As Single, by As SByte, bz As SByte) As SysNumerics.Vector3
        Dim yf As Single = (CInt(by) And &HFF) / 255.0F * 2.0F - 1.0F
        Dim zf As Single = (CInt(bz) And &HFF) / 255.0F * 2.0F - 1.0F
        Return NormalizeOrZero(New SysNumerics.Vector3(x, yf, zf))
    End Function

    Private Shared Function DecodeByteVector3(x As SByte, y As SByte, z As SByte) As SysNumerics.Vector3
        Dim xf As Single = (CInt(x) And &HFF) / 255.0F * 2.0F - 1.0F
        Dim yf As Single = (CInt(y) And &HFF) / 255.0F * 2.0F - 1.0F
        Dim zf As Single = (CInt(z) And &HFF) / 255.0F * 2.0F - 1.0F
        Return New SysNumerics.Vector3(xf, yf, zf)
    End Function

    Private Shared Function NormalizeOrZero(v As SysNumerics.Vector3) As SysNumerics.Vector3
        Dim len As Single = v.Length()
        If len > 0.000001F Then Return v / len
        Return SysNumerics.Vector3.Zero
    End Function

    ''' <summary>
    ''' BSDynamicTriShape carries a parallel List(Of Vector4) Vertices field (XYZ position + W
    ''' carrying BitangentX) that is read at runtime by the cloth/dynamic system.  After any
    ''' mutation of the inline BSVertexData positions, that parallel buffer must be re-synced
    ''' via CalcDynamicData() — otherwise the saved NIF carries stale dynamic data even though
    ''' the static positions are correct.  No-op for plain BSTriShape and other subclasses.
    ''' </summary>
    Private Sub SyncDynamicIfNeeded()
        Dim dyn = TryCast(_tri, BSDynamicTriShape)
        If dyn IsNot Nothing Then dyn.CalcDynamicData()
    End Sub

    ''' <summary>
    ''' Copies up to <paramref name="wpv"/> bone influence slots from a single BSVertexData
    ''' vertex into the flat output arrays at vertex offset <paramref name="vIdx"/>.  Pads
    ''' missing slots with index=0, weight=0 — same convention as ShapeSkinningData.Empty.
    ''' </summary>
    ' CopyVertexInfluences helper removed: the two callers (GetSkinning) now iterate
    ' directly over BoneIndices4/BoneWeights4 InlineArray structs without going through
    ' temporary Byte()/SysHalf() arrays.

    ''' <summary>
    ''' Reads the bone-palette block indices from the shape's skin instance Bones reference list.
    ''' Returns an empty array when the skin instance is missing or has no bones.  Pure
    ''' diagnostics — the renderer does not consume this; ShapeBones on IRenderableShape is the
    ''' authoritative source for matrices.
    ''' </summary>
    Private Function ResolveBonePalette() As Integer()
        If _tri.SkinInstanceRef Is Nothing OrElse _tri.SkinInstanceRef.Index = -1 Then Return Array.Empty(Of Integer)()
        If _tri.SkinInstanceRef.Index >= _nif.Blocks.Count Then Return Array.Empty(Of Integer)()
        Dim skinInst = TryCast(_nif.Blocks(_tri.SkinInstanceRef.Index), INiSkin)
        If skinInst Is Nothing OrElse skinInst.Bones Is Nothing Then Return Array.Empty(Of Integer)()
        Return skinInst.Bones.Indices.Select(Function(i) CInt(i)).ToArray()
    End Function
End Class
