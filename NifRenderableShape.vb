Imports NiflySharp
Imports NiflySharp.Blocks
Imports NiflySharp.Structs

''' <summary>
''' Lightweight IRenderableShape implementation that wraps any supported NIF shape directly
''' from a loaded NIF file.  No SliderSet/OSP dependency.  Used by NPC Manager and any consumer
''' that renders raw NIFs.
'''
''' Polymorphism: the constructor takes INiShape (BSTriShape, BSSubIndexTriShape,
''' BSDynamicTriShape, BSSegmentedTriShape, BSMeshLODTriShape, NiTriShape, BSLODTriShape) and
''' instantiates the matching IShapeGeometry adapter via ShapeGeometryFactory.  The factory
''' filter mirrors Nifcontent_Class_Manolo.SupportedShape — anything that returns True there
''' will produce a working renderable here.
''' </summary>
Public Class NifRenderableShape
    Implements IRenderableShape

    Private ReadOnly _nif As Nifcontent_Class_Manolo
    Private ReadOnly _shape As INiShape
    Private ReadOnly _geometry As IShapeGeometry
    Private ReadOnly _index As Integer
    Private _bones As IReadOnlyList(Of NiNode)
    Private _boneTransforms As IReadOnlyList(Of Transform_Class)
    Private _material As Nifcontent_Class_Manolo.RelatedMaterial_Class

    Public Sub New(nif As Nifcontent_Class_Manolo, shape As INiShape, index As Integer)
        If nif Is Nothing Then Throw New ArgumentNullException(NameOf(nif))
        If shape Is Nothing Then Throw New ArgumentNullException(NameOf(shape))
        _nif = nif
        _shape = shape
        _geometry = ShapeGeometryFactory.[For](shape, nif)
        _index = index
        ResolveSkinData()
        _material = nif.GetRelatedMaterial(shape)
    End Sub

    ''' <summary>
    ''' Resolves the shape's bone palette (NiNode list) and the per-bone bind transforms.
    ''' Per-vertex bone weights/indices are NOT resolved here — those live in
    ''' IShapeGeometry.GetSkinning() (BSTriShape inline or NiSkinPartition expansion).
    ''' </summary>
    Private Sub ResolveSkinData()
        Dim skin = NifSkin
        If skin Is Nothing Then
            _bones = New List(Of NiNode)
            _boneTransforms = New List(Of Transform_Class)
            Return
        End If

        ' Resolve bones
        Dim boneNodes As New List(Of NiNode)
        If skin.Bones IsNot Nothing Then
            For Each boneIdx In skin.Bones.Indices
                If boneIdx >= 0 AndAlso boneIdx < _nif.Blocks.Count Then
                    Dim node = TryCast(_nif.Blocks(boneIdx), NiNode)
                    If node IsNot Nothing Then boneNodes.Add(node)
                End If
            Next
        End If
        _bones = boneNodes

        ' Resolve bone transforms — per-bone bind matrices live in different blocks depending
        ' on the skin instance type:
        '   BSSkin_Instance         → BSSkin_BoneData.BoneList[].BoneTransform   (FO4 BSTriShape)
        '   BSDismemberSkinInstance → NiSkinData.BoneList[].SkinTransform        (FO4/SSE NiTriShape and BSTriShape with BSDismember)
        '   NiSkinInstance          → NiSkinData.BoneList[].SkinTransform        (legacy NiTriShape)
        Dim transforms As New List(Of Transform_Class)
        Select Case skin.GetType
            Case GetType(BSSkin_Instance)
                Dim bsSkin = TryCast(skin, BSSkin_Instance)
                If bsSkin.Data IsNot Nothing AndAlso bsSkin.Data.Index >= 0 AndAlso bsSkin.Data.Index < _nif.Blocks.Count Then
                    Dim boneData = TryCast(_nif.Blocks(bsSkin.Data.Index), BSSkin_BoneData)
                    If boneData IsNot Nothing Then
                        For Each bon In boneData.BoneList
                            transforms.Add(New Transform_Class(bon))
                        Next
                    End If
                End If
            Case GetType(BSDismemberSkinInstance)
                Dim dismember = TryCast(skin, BSDismemberSkinInstance)
                If dismember.Data IsNot Nothing AndAlso dismember.Data.Index >= 0 AndAlso dismember.Data.Index < _nif.Blocks.Count Then
                    Dim skinData = TryCast(_nif.Blocks(dismember.Data.Index), NiSkinData)
                    If skinData IsNot Nothing Then
                        For Each bon In skinData.BoneList
                            transforms.Add(New Transform_Class(bon))
                        Next
                    End If
                End If
            Case GetType(NiSkinInstance)
                Dim niSkin = TryCast(skin, NiSkinInstance)
                If niSkin.Data IsNot Nothing AndAlso niSkin.Data.Index >= 0 AndAlso niSkin.Data.Index < _nif.Blocks.Count Then
                    Dim skinData = TryCast(_nif.Blocks(niSkin.Data.Index), NiSkinData)
                    If skinData IsNot Nothing Then
                        For Each bon In skinData.BoneList
                            transforms.Add(New Transform_Class(bon))
                        Next
                    End If
                End If
        End Select
        _boneTransforms = transforms
    End Sub

    ' --- IRenderableShape Implementation ---

    Public ReadOnly Property ShapeName As String Implements IRenderableShape.ShapeName
        Get
            Return If(_shape?.Name?.String, "")
        End Get
    End Property

    Public ReadOnly Property ShapeTarget As String Implements IRenderableShape.ShapeTarget
        Get
            Return ShapeName
        End Get
    End Property

    Public ReadOnly Property ShapeIndex As Integer Implements IRenderableShape.ShapeIndex
        Get
            Return _index
        End Get
    End Property

    Public ReadOnly Property NifContent As Nifcontent_Class_Manolo Implements IRenderableShape.NifContent
        Get
            Return _nif
        End Get
    End Property

    Public ReadOnly Property NifShape As INiShape Implements IRenderableShape.NifShape
        Get
            Return _shape
        End Get
    End Property

    Public ReadOnly Property Geometry As IShapeGeometry Implements IRenderableShape.Geometry
        Get
            Return _geometry
        End Get
    End Property

    Public ReadOnly Property NifSkin As INiSkin Implements IRenderableShape.NifSkin
        Get
            If _shape Is Nothing OrElse _shape.SkinInstanceRef Is Nothing OrElse _shape.SkinInstanceRef.Index = -1 Then Return Nothing
            If _shape.SkinInstanceRef.Index >= _nif.Blocks.Count Then Return Nothing
            Return TryCast(_nif.Blocks(_shape.SkinInstanceRef.Index), INiSkin)
        End Get
    End Property

    Public ReadOnly Property NifShader As INiShader Implements IRenderableShape.NifShader
        Get
            Return _nif.GetShader(_shape)
        End Get
    End Property

    Public ReadOnly Property ShapeBones As IReadOnlyList(Of NiNode) Implements IRenderableShape.ShapeBones
        Get
            Return _bones
        End Get
    End Property

    Public ReadOnly Property ShapeBoneTransforms As IReadOnlyList(Of Transform_Class) Implements IRenderableShape.ShapeBoneTransforms
        Get
            Return _boneTransforms
        End Get
    End Property

    Public ReadOnly Property ShapeMaterial As Nifcontent_Class_Manolo.RelatedMaterial_Class Implements IRenderableShape.ShapeMaterial
        Get
            Return _material
        End Get
    End Property

    Public ReadOnly Property IsSkinned As Boolean Implements IRenderableShape.IsSkinned
        Get
            Return _shape IsNot Nothing AndAlso _shape.IsSkinned
        End Get
    End Property

    Public ReadOnly Property HasPhysics As Boolean Implements IRenderableShape.HasPhysics
        Get
            If _nif Is Nothing OrElse _nif.Blocks Is Nothing Then Return False
            Return _nif.Blocks.Any(Function(b) TypeOf b Is BSClothExtraData)
        End Get
    End Property

    ' --- Display Flags (defaults for NPC rendering) ---
    Public Property ShowTexture As Boolean = True Implements IRenderableShape.ShowTexture
    Public Property ShowMask As Boolean = False Implements IRenderableShape.ShowMask
    Public Property ShowWeight As Boolean = False Implements IRenderableShape.ShowWeight
    Public Property ShowVertexColor As Boolean = True Implements IRenderableShape.ShowVertexColor
    Public Property RenderHide As Boolean = False Implements IRenderableShape.RenderHide
    Public Property Wireframe As Boolean = False Implements IRenderableShape.Wireframe
    Public Property Wirecolor As Color = Color.LightGray Implements IRenderableShape.Wirecolor
    Public Property WireAlpha As Single = 0.5F Implements IRenderableShape.WireAlpha
    Public Property TintColor As Color = Color.White Implements IRenderableShape.TintColor
    Public Property ApplyZaps As Boolean = False Implements IRenderableShape.ApplyZaps
    Public Property MaskedVertices As New HashSet(Of Integer)() Implements IRenderableShape.MaskedVertices

    ''' <summary>
    ''' Factory: instantiates a NifRenderableShape for every shape in <paramref name="nif"/>
    ''' that Nifcontent_Class_Manolo.SupportedShape and ShapeGeometryFactory both accept.
    ''' Previously this filtered to BSTriShape-only via a TryCast — that filter is gone, so
    ''' NiTriShape and BSLODTriShape are now rendered alongside the BSTriShape family.
    ''' </summary>
    Public Shared Function FromNif(nif As Nifcontent_Class_Manolo) As List(Of NifRenderableShape)
        Dim result As New List(Of NifRenderableShape)
        Dim idx = 0
        For Each shape In nif.GetShapes()
            If Nifcontent_Class_Manolo.SupportedShape(shape.GetType) AndAlso ShapeGeometryFactory.IsSupported(shape) Then
                result.Add(New NifRenderableShape(nif, shape, idx))
                idx += 1
            End If
        Next
        Return result
    End Function
End Class
