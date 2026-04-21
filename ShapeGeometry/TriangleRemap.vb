''' <summary>
''' Per-new-triangle provenance for operations that change triangle count (zap, split, merge).
''' When passed to IShapeGeometry.SetTriangles, the adapter uses this to redistribute count-
''' derived metadata (BSMeshLODTriShape / BSLODTriShape LOD0/1/2 sizes, BSSubIndexTriShape /
''' BSSegmentedTriShape Segments + SubSegmentDatas) without losing granularity.
'''
''' Layout: <see cref="Sources"/> is parallel to the new triangle list — Sources(i) describes
''' where the i-th new triangle came from.  For zap/split the source is always the same shape
''' as the target (Shape Is Nothing).  For merge each donor's triangles carry their donor
''' shape reference so the adapter can read the donor's segments/LODs and append them with
''' the right offset.
'''
''' Convention follows BodySlide-and-Outfit-Studio's Geometry.cpp:1248 BSSubIndexTriShape::
''' notifyVerticesDelete — count survivors per old-segment range, then realign cumulative
''' StartIndex.
''' </summary>
Public NotInheritable Class TriangleRemap

    ''' <summary>
    ''' Per-new-triangle provenance.  Length must equal the new triangle list count.  Each
    ''' entry says "this new triangle came from (Sources(i).Shape, oldIdx = Sources(i).OldIdx)".
    ''' </summary>
    Public ReadOnly Sources As IReadOnlyList(Of TriangleSource)

    Public Sub New(sources As IReadOnlyList(Of TriangleSource))
        If sources Is Nothing Then Throw New ArgumentNullException(NameOf(sources))
        Me.Sources = sources
    End Sub

    ''' <summary>
    ''' Convenience factory for the same-shape case (zap, split): a flat list mapping
    ''' newTriIdx → oldTriIdx in the same shape as the target.  Use -1 for synthetic
    ''' triangles with no provenance (rare; would skip metadata redistribution for that
    ''' triangle).
    ''' </summary>
    Public Shared Function SameShape(oldTriIndices As IReadOnlyList(Of Integer)) As TriangleRemap
        If oldTriIndices Is Nothing Then Throw New ArgumentNullException(NameOf(oldTriIndices))
        Dim arr(oldTriIndices.Count - 1) As TriangleSource
        For i = 0 To oldTriIndices.Count - 1
            arr(i) = New TriangleSource(Nothing, oldTriIndices(i))
        Next
        Return New TriangleRemap(arr)
    End Function

End Class

''' <summary>
''' One per new triangle: identifies the source.
'''
''' <para><c>Shape</c>: the source shape this triangle came from.  Nothing means "the same
''' shape as the target of SetTriangles" — i.e. zap/split where new triangles are derived
''' from the target's own pre-edit triangle list.  Non-null means "this triangle came from
''' another shape" — used by merge where donor triangles get appended into the target.</para>
'''
''' <para><c>OldIdx</c>: the triangle's index in the source shape's triangle list at the time
''' the operation started.  -1 means "synthetic, no provenance" — the adapter will skip
''' metadata redistribution for this triangle.</para>
''' </summary>
Public Structure TriangleSource

    Public ReadOnly Shape As IShapeGeometry
    Public ReadOnly OldIdx As Integer

    Public Sub New(shape As IShapeGeometry, oldIdx As Integer)
        Me.Shape = shape
        Me.OldIdx = oldIdx
    End Sub

End Structure
