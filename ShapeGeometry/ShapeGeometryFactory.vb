Imports NiflySharp
Imports NiflySharp.Blocks

''' <summary>
''' Single dispatch point that wraps any supported shape type in its concrete IShapeGeometry
''' adapter.  Mirrors Nifcontent_Class_Manolo.SupportedShape — whatever returns True there
''' must have a branch here.
''' </summary>
Public Module ShapeGeometryFactory

    ''' <summary>
    ''' Creates the appropriate adapter for the given shape, or throws NotSupportedException
    ''' for unsupported types.  Throws ArgumentNullException for null inputs.
    '''
    ''' Subclass dispatch: BSSubIndexTriShape, BSDynamicTriShape, BSSegmentedTriShape and
    ''' BSMeshLODTriShape all derive from BSTriShape and use the BSTriShapeGeometry adapter.
    ''' BSLODTriShape derives from NiTriBasedGeom (same family as NiTriShape) and uses
    ''' NiTriShapeGeometry.  The TypeOf checks below order the BS family first so the BSTriShape
    ''' branch catches all derived classes via inheritance.
    ''' </summary>
    Public Function [For](shape As INiShape, nif As Nifcontent_Class_Manolo) As IShapeGeometry
        If shape Is Nothing Then Throw New ArgumentNullException(NameOf(shape))
        If nif Is Nothing Then Throw New ArgumentNullException(NameOf(nif))

        ' BSTriShape and any subclass (BSSubIndexTriShape, BSDynamicTriShape, BSSegmentedTriShape,
        ' BSMeshLODTriShape).  Must come before the NiTriShape branch — ordering by inheritance
        ' specificity does not matter here because BSTriShape and NiTriShape live in disjoint
        ' subtrees, but explicit check keeps the dispatch readable.
        Dim bs = TryCast(shape, BSTriShape)
        If bs IsNot Nothing Then Return New BSTriShapeGeometry(bs, nif)

        ' BSLODTriShape inherits NiTriBasedGeom alongside NiTriShape — same data layout,
        ' shared adapter.
        If TypeOf shape Is BSLODTriShape Then Return New NiTriShapeGeometry(shape, nif)

        ' NiTriStrips also inherits NiTriBasedGeom — same vertex/normal/uv layout via
        ' NiTriBasedGeomData; only the triangle data is stored as strips that the adapter
        ' converts to a triangle list for the renderer.
        If TypeOf shape Is NiTriStrips Then Return New NiTriShapeGeometry(shape, nif)

        ' Plain NiTriShape last (anything else that derives from NiTriBasedGeom would also
        ' match here — there are no other supported subclasses today).
        If TypeOf shape Is NiTriShape Then Return New NiTriShapeGeometry(shape, nif)

        Throw New NotSupportedException(
            $"No IShapeGeometry adapter registered for {shape.GetType().FullName}.  " &
            "Update Nifcontent_Class_Manolo.SupportedShape and ShapeGeometryFactory together.")
    End Function

    ''' <summary>
    ''' Cheap pre-check that mirrors Nifcontent_Class_Manolo.SupportedShape but without
    ''' allocating an adapter.  Use this when you only need to know whether the shape will
    ''' yield a valid IShapeGeometry (e.g. during enumeration filters).
    ''' </summary>
    Public Function IsSupported(shape As INiShape) As Boolean
        If shape Is Nothing Then Return False
        Return TypeOf shape Is BSTriShape _
            OrElse TypeOf shape Is BSLODTriShape _
            OrElse TypeOf shape Is NiTriShape _
            OrElse TypeOf shape Is NiTriStrips
    End Function

End Module
