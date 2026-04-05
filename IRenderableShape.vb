Imports NiflySharp
Imports NiflySharp.Blocks

Public Interface IRenderableShape
    ' Identity
    ReadOnly Property ShapeName As String
    ReadOnly Property ShapeTarget As String
    ReadOnly Property ShapeIndex As Integer

    ' NIF data
    ReadOnly Property NifContent As Nifcontent_Class_Manolo
    ReadOnly Property NifShape As BSTriShape
    ReadOnly Property NifSkin As INiSkin
    ReadOnly Property NifShader As INiShader
    ReadOnly Property ShapeBones As IReadOnlyList(Of NiNode)
    ReadOnly Property ShapeBoneTransforms As IReadOnlyList(Of Transform_Class)
    ReadOnly Property ShapeMaterial As Nifcontent_Class_Manolo.RelatedMaterial_Class
    ReadOnly Property IsSkinned As Boolean
    ReadOnly Property HasPhysics As Boolean

    ' Display flags
    Property ShowTexture As Boolean
    Property ShowMask As Boolean
    Property ShowWeight As Boolean
    Property ShowVertexColor As Boolean
    Property RenderHide As Boolean
    Property Wireframe As Boolean
    Property Wirecolor As Color
    Property WireAlpha As Single
    Property TintColor As Color
    Property ApplyZaps As Boolean
    Property MaskedVertices As HashSet(Of Integer)
End Interface
