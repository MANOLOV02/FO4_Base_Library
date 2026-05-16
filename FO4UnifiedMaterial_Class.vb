' Version Uploaded of Fo4Library 3.2.0
Imports System.Collections.Concurrent
Imports System.ComponentModel
Imports System.Drawing.Design
Imports System.IO
Imports System.Reflection
Imports System.Text.Json
Imports MaterialLib
Imports MaterialLib.BaseMaterialFile
Imports NiflySharp
Imports NiflySharp.Blocks
Imports NiflySharp.Helpers
Imports NiflySharp.Structs
Imports OpenTK.Graphics.ES11
Imports OpenTK.Graphics.OpenGL

<AttributeUsage(AttributeTargets.Property)>
Public Class BGSMOnlyAttribute
    Inherits Attribute
End Class

<AttributeUsage(AttributeTargets.Property)>
Public Class BGEMOnlyAttribute
    Inherits Attribute
End Class

Public Class FO4UnifiedMaterialDescriptor
    Inherits CustomTypeDescriptor

    Private ReadOnly instance As FO4UnifiedMaterial_Class

    Public Sub New(parent As ICustomTypeDescriptor, instance As FO4UnifiedMaterial_Class)
        MyBase.New(parent)
        Me.instance = instance
    End Sub

    Public Overrides Function GetProperties(attributes As Attribute()) As PropertyDescriptorCollection
        Dim props As PropertyDescriptorCollection = MyBase.GetProperties(attributes)
        Return FilterProperties(props)
    End Function

    Public Overrides Function GetProperties() As PropertyDescriptorCollection
        Dim props As PropertyDescriptorCollection = MyBase.GetProperties()
        Return FilterProperties(props)
    End Function

    Private Function FilterProperties(props As PropertyDescriptorCollection) As PropertyDescriptorCollection
        Dim filtered As New List(Of PropertyDescriptor)()
        Dim currentType As Type = instance.Underlying_Material.GetType()

        For Each prop As PropertyDescriptor In props
            If prop.Attributes(GetType(BGSMOnlyAttribute)) IsNot Nothing AndAlso currentType IsNot GetType(BGSM) Then
                Continue For
            End If
            If prop.Attributes(GetType(BGEMOnlyAttribute)) IsNot Nothing AndAlso currentType IsNot GetType(BGEM) Then
                Continue For
            End If
            filtered.Add(prop)
        Next

        Return New PropertyDescriptorCollection(filtered.ToArray())
    End Function
End Class

Public Class FO4UnifiedMaterialProvider
    Inherits TypeDescriptionProvider

    Private ReadOnly baseProvider As TypeDescriptionProvider

    Public Sub New()
        MyBase.New(TypeDescriptor.GetProvider(GetType(FO4UnifiedMaterial_Class)))
        baseProvider = TypeDescriptor.GetProvider(GetType(FO4UnifiedMaterial_Class))
    End Sub

    Public Overrides Function GetTypeDescriptor(objectType As Type, instance As Object) As ICustomTypeDescriptor
        Dim defaultDescriptor As ICustomTypeDescriptor = baseProvider.GetTypeDescriptor(objectType, instance)
        Return New FO4UnifiedMaterialDescriptor(defaultDescriptor, CType(instance, FO4UnifiedMaterial_Class))
    End Function
End Class

<TypeDescriptionProvider(GetType(FO4UnifiedMaterialProvider))>
Public Class FO4UnifiedMaterial_Class
    <Browsable(False)>
    Public Property Underlying_Material As MaterialLib.BaseMaterialFile = New BGEM

    ' Dirty tracking via snapshot comparison. ClearDirty() captures a Clone of the current
    ' state; IsDirty() reports whether the wrapper has diverged from that snapshot via
    ' GetDifferences (which honors AreEquivalentGrayscaleScale tolerance for slot-equivalent
    ' palette scales). Net-zero round-trips (user edits then reverts) report clean because
    ' the comparison is against the snapshot, not a one-way "was ever touched" flag.
    ' Snapshot captured by the load/save paths (Deserialize / Save_To_* / Create_From_Shader);
    ' the editor doesn't need to mark dirty on user edits — the next IsDirty() check detects
    ' the diff automatically.
    Private _cleanSnapshot As FO4UnifiedMaterial_Class = Nothing

    Public Sub ClearDirty()
        _cleanSnapshot = Clone()
    End Sub

    Public Function IsDirty() As Boolean
        If _cleanSnapshot Is Nothing Then Return False
        Return GetDifferences(Me, _cleanSnapshot).Count > 0
    End Function

    ''' <summary>Deep-copy the wrapper: serialize the Underlying_Material to bytes and
    ''' deserialize into a fresh BGSM/BGEM, plus copy the transient wrapper fields that
    ''' aren't persisted in the binary (NIF ShaderType, sidecar envmap path, the three
    ''' alpha-blend fields). Used by ClearDirty to take a comparison snapshot.</summary>
    Public Function Clone() As FO4UnifiedMaterial_Class
        Dim copy As New FO4UnifiedMaterial_Class()
        If Underlying_Material IsNot Nothing Then
            Dim t = Underlying_Material.GetType()
            Dim newMat As MaterialLib.BaseMaterialFile = Nothing
            If t Is GetType(BGSM) Then
                newMat = New BGSM()
            ElseIf t Is GetType(BGEM) Then
                newMat = New BGEM()
            End If
            If newMat IsNot Nothing Then
                Using ms As New MemoryStream()
                    Using writer As New BinaryWriter(ms)
                        Underlying_Material.Serialize(writer)
                    End Using
                    Dim bytes = ms.ToArray()
                    Using ms2 As New MemoryStream(bytes)
                        Using reader As New BinaryReader(ms2)
                            newMat.Deserialize(reader)
                        End Using
                    End Using
                End Using
                copy.Underlying_Material = newMat
            End If
        End If
        copy._NifShaderType = _NifShaderType
        copy._EnvmapMaskPath = _EnvmapMaskPath
        copy._alphaBlendEnabled = _alphaBlendEnabled
        copy._blendFunctionSource = _blendFunctionSource
        copy._blendFunctionDest = _blendFunctionDest
        Return copy
    End Function

    ' NIF ShaderType — not part of BGSM/BGEM file format, stored here as runtime field
    Private _NifShaderType As NiflySharp.Enums.BSLightingShaderType = NiflySharp.Enums.BSLightingShaderType.Default
    ' Env mask path for BGSM — NIF texture set slot 5. Not serialized in the .bgsm binary
    ' (BGSM has no envmapMaskTexture field; path lives only in the NIF texture set).
    ' Evidence: BodySlide MaterialFile.cpp:91-113 (BGSM binary has 9 strings, none for envmask),
    ' PreviewWindow.cpp:434-449 (BodySlide never assigns texFiles[5] from BGSM).
    ' Roundtrip across .bgsm Save/Load is preserved via a sidecar `.bgsm.json` written next
    ' to the .bgsm — see Save_To_Bgsm and Deserialize(Diccionario,...).
    Private _EnvmapMaskPath As String = ""
    Private Shared ReadOnly GrayscaleTextureWidthCache As New ConcurrentDictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)

    <Browsable(False)>
    Public Property MaskWrites As MaskWriteFlags
        Get
            Return Underlying_Material.MaskWrites
        End Get
        Set(value As MaskWriteFlags)
            Underlying_Material.MaskWrites = value
        End Set
    End Property

    <Category("(Type)")>
    Public ReadOnly Property MaterialType As Type
        Get
            Return Underlying_Material.GetType
        End Get
    End Property

    <Category("(Type)")>
    <DefaultValue(NiflySharp.Enums.BSLightingShaderType.Default)>
    <TypeConverter(GetType(ShaderTypeConverter))>
    Public Property NifShaderType As NiflySharp.Enums.BSLightingShaderType
        Get
            Return _NifShaderType
        End Get
        Set(value As NiflySharp.Enums.BSLightingShaderType)
            _NifShaderType = value
            ApplyShaderTypeToBgsm(TryCast(Underlying_Material, BGSM), value)
        End Set
    End Property

    ' Alpha-blend state model:
    ' The four canonical AlphaBlendMode values (None/Standard/Additive/Multiplicative) each
    ' fix a tuple (enabled, src, dst). Unknown is the escape hatch for combinations that
    ' don't match any canonical — when Unknown, the three fields below (AlphaBlendEnabled,
    ' BlendFunctionSource, BlendFunctionDest) are the authoritative state and come from the
    ' NIF's NiAlphaProperty, not from the BGSM file (whose serializer hardcodes (0,6,7) for
    ' Unknown and can't carry arbitrary tuples). Setters of the three fields auto-classify
    ' the resulting tuple against the 5 canonical patterns and promote/degrade the enum
    ' reactively. The setter of AlphaBlendMode derives the three fields when the value is
    ' canonical; when Unknown, it leaves them alone so caller-provided values survive.
    ' Renderers (OutfitStudio GLSurface.cpp:1349, NifSkope glproperty.cpp:255) consume
    ' NiAlphaProperty.Flags from the NIF, so persistence into the NIF is the source of truth.
    Private _alphaBlendEnabled As Boolean = False
    Private _blendFunctionSource As NiflySharp.Enums.AlphaFunction = NiflySharp.Enums.AlphaFunction.SRC_ALPHA
    Private _blendFunctionDest As NiflySharp.Enums.AlphaFunction = NiflySharp.Enums.AlphaFunction.INV_SRC_ALPHA
    Private _suppressAutoPromotion As Boolean = False

    <Category("Opacity")>
    <DefaultValue(AlphaBlendModeType.Unknown)>
    Public Property AlphaBlendMode As MaterialLib.BaseMaterialFile.AlphaBlendModeType
        Get
            Return Underlying_Material.AlphaBlendMode
        End Get
        Set(value As MaterialLib.BaseMaterialFile.AlphaBlendModeType)
            Underlying_Material.AlphaBlendMode = value
            If value <> AlphaBlendModeType.Unknown Then
                Dim t = CanonicalTuple(value)
                _suppressAutoPromotion = True
                Try
                    _alphaBlendEnabled = t.Enabled
                    _blendFunctionSource = t.Src
                    _blendFunctionDest = t.Dst
                Finally
                    _suppressAutoPromotion = False
                End Try
            End If
        End Set
    End Property

    <Category("Opacity")>
    <DefaultValue(False)>
    Public Property AlphaBlendEnabled As Boolean
        Get
            Return _alphaBlendEnabled
        End Get
        Set(value As Boolean)
            _alphaBlendEnabled = value
            If Not _suppressAutoPromotion Then RecomputeAlphaBlendModeFromFields()
        End Set
    End Property

    <Category("Opacity")>
    <DefaultValue(NiflySharp.Enums.AlphaFunction.SRC_ALPHA)>
    Public Property BlendFunctionSource As NiflySharp.Enums.AlphaFunction
        Get
            Return _blendFunctionSource
        End Get
        Set(value As NiflySharp.Enums.AlphaFunction)
            _blendFunctionSource = value
            If Not _suppressAutoPromotion Then RecomputeAlphaBlendModeFromFields()
        End Set
    End Property

    <Category("Opacity")>
    <DefaultValue(NiflySharp.Enums.AlphaFunction.INV_SRC_ALPHA)>
    Public Property BlendFunctionDest As NiflySharp.Enums.AlphaFunction
        Get
            Return _blendFunctionDest
        End Get
        Set(value As NiflySharp.Enums.AlphaFunction)
            _blendFunctionDest = value
            If Not _suppressAutoPromotion Then RecomputeAlphaBlendModeFromFields()
        End Set
    End Property

    <Category("Textures")>
    <Editor(GetType(DictionaryFilePickerEditor), GetType(UITypeEditor))>
    Public Property NormalTexture As String
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return CType(Underlying_Material, BGSM).NormalTexture
                Case GetType(BGEM)
                    Return CType(Underlying_Material, BGEM).NormalTexture
            End Select
            Throw New Exception("Unsupported material type")
        End Get
        Set(value As String)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).NormalTexture = value
                Case GetType(BGEM)
                    CType(Underlying_Material, BGEM).NormalTexture = value
            End Select
        End Set
    End Property
    Public Function IsBGEM() As Boolean
        Select Case Underlying_Material.GetType
            Case GetType(BGEM)
                Return True
            Case Else
                Return False
        End Select
    End Function
    Public Function IsBGSM() As Boolean
        Select Case Underlying_Material.GetType
            Case GetType(BGSM)
                Return True
            Case Else
                Return False
        End Select
    End Function

    <Category("Textures")>
    <Editor(GetType(DictionaryFilePickerEditor), GetType(UITypeEditor))>
    Public Property Diffuse_or_Base_Texture As String
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return CType(Underlying_Material, BGSM).DiffuseTexture
                Case GetType(BGEM)
                    Return CType(Underlying_Material, BGEM).BaseTexture
            End Select
            Throw New Exception("Unsupported material type")
        End Get
        Set(value As String)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).DiffuseTexture = value
                Case GetType(BGEM)
                    CType(Underlying_Material, BGEM).BaseTexture = value
            End Select
        End Set
    End Property

    <Category("Textures")>
    <BGSMOnly()>
    <Editor(GetType(DictionaryFilePickerEditor), GetType(UITypeEditor))>
    Public Property SmoothSpecTexture As String
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return CType(Underlying_Material, BGSM).SmoothSpecTexture
                Case GetType(BGEM)
                    Return ""
            End Select
            Throw New Exception("Unsupported material type")
        End Get
        Set(value As String)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).SmoothSpecTexture = value
                Case GetType(BGEM)
                    ' No operation
            End Select
        End Set
    End Property

    <Category("Textures")>
    <Editor(GetType(DictionaryFilePickerEditor), GetType(UITypeEditor))>
    Public Property GreyscaleTexture As String
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return CType(Underlying_Material, BGSM).GreyscaleTexture
                Case GetType(BGEM)
                    Return CType(Underlying_Material, BGEM).GrayscaleTexture
            End Select
            Throw New Exception("Unsupported material type")
        End Get
        Set(value As String)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).GreyscaleTexture = value
                Case GetType(BGEM)
                    CType(Underlying_Material, BGEM).GrayscaleTexture = value
            End Select
        End Set
    End Property

    <Category("Textures")>
    <Editor(GetType(DictionaryFilePickerEditor), GetType(UITypeEditor))>
    Public Property GlowTexture As String
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return CType(Underlying_Material, BGSM).GlowTexture
                Case GetType(BGEM)
                    Return CType(Underlying_Material, BGEM).GlowTexture
            End Select
            Throw New Exception("Unsupported material type")
        End Get
        Set(value As String)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).GlowTexture = value
                Case GetType(BGEM)
                    CType(Underlying_Material, BGEM).GlowTexture = value
            End Select
        End Set
    End Property
    <Category("Textures")>
    <Editor(GetType(DictionaryFilePickerEditor), GetType(UITypeEditor))>
    Public Property EnvmapTexture As String
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return CType(Underlying_Material, BGSM).EnvmapTexture
                Case GetType(BGEM)
                    Return CType(Underlying_Material, BGEM).EnvmapTexture
            End Select
            Throw New Exception("Unsupported material type")
        End Get
        Set(value As String)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).EnvmapTexture = value
                Case GetType(BGEM)
                    CType(Underlying_Material, BGEM).EnvmapTexture = value
            End Select
        End Set
    End Property

    <Category("Textures")>
    <Editor(GetType(DictionaryFilePickerEditor), GetType(UITypeEditor))>
    Public Property SpecularTexture As String
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return CType(Underlying_Material, BGSM).SpecularTexture
                Case GetType(BGEM)
                    Return CType(Underlying_Material, BGEM).SpecularTexture
            End Select
            Throw New Exception("Unsupported material type")
        End Get
        Set(value As String)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).SpecularTexture = value
                Case GetType(BGEM)
                    CType(Underlying_Material, BGEM).SpecularTexture = value
            End Select
        End Set
    End Property

    <Category("Textures")>
    <Editor(GetType(DictionaryFilePickerEditor), GetType(UITypeEditor))>
    Public Property LightingTexture As String
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return CType(Underlying_Material, BGSM).LightingTexture
                Case GetType(BGEM)
                    Return CType(Underlying_Material, BGEM).LightingTexture
            End Select
            Throw New Exception("Unsupported material type")
        End Get
        Set(value As String)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).LightingTexture = value
                Case GetType(BGEM)
                    CType(Underlying_Material, BGEM).LightingTexture = value
            End Select
        End Set
    End Property

    <Category("Textures")>
    <BGSMOnly()>
    <Editor(GetType(DictionaryFilePickerEditor), GetType(UITypeEditor))>
    Public Property FlowTexture As String
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return CType(Underlying_Material, BGSM).FlowTexture
                Case GetType(BGEM)
                    Return ""
            End Select
            Throw New Exception("Unsupported material type")
        End Get
        Set(value As String)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).FlowTexture = value
                Case GetType(BGEM)
                    ' No operation
            End Select
        End Set
    End Property

    <Category("Textures")>
    <BGSMOnly()>
    <Editor(GetType(DictionaryFilePickerEditor), GetType(UITypeEditor))>
    Public Property DisplacementTexture As String
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return CType(Underlying_Material, BGSM).DisplacementTexture
                Case GetType(BGEM)
                    Return ""
            End Select
            Throw New Exception("Unsupported material type")
        End Get
        Set(value As String)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).DisplacementTexture = value
                Case GetType(BGEM)
                    ' No operation
            End Select
        End Set
    End Property

    <Category("Textures")>
    <BGSMOnly()>
    <Editor(GetType(DictionaryFilePickerEditor), GetType(UITypeEditor))>
    Public Property InnerLayerTexture As String
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return CType(Underlying_Material, BGSM).InnerLayerTexture
                Case GetType(BGEM)
                    Return ""
            End Select
            Throw New Exception("Unsupported material type")
        End Get
        Set(value As String)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).InnerLayerTexture = value
                Case GetType(BGEM)
                    ' No operation
            End Select
        End Set
    End Property

    <Category("Textures")>
    <BGSMOnly()>
    <Editor(GetType(DictionaryFilePickerEditor), GetType(UITypeEditor))>
    Public Property WrinklesTexture As String
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return CType(Underlying_Material, BGSM).WrinklesTexture
                Case GetType(BGEM)
                    Return ""
            End Select
            Throw New Exception("Unsupported material type")
        End Get
        Set(value As String)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).WrinklesTexture = value
                Case GetType(BGEM)
                    ' No operation
            End Select
        End Set
    End Property

    <Category("Textures")>
    <BGSMOnly()>
    <Editor(GetType(DictionaryFilePickerEditor), GetType(UITypeEditor))>
    Public Property DistanceFieldAlphaTexture As String
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return CType(Underlying_Material, BGSM).DistanceFieldAlphaTexture
                Case GetType(BGEM)
                    Return ""
            End Select
            Throw New Exception("Unsupported material type")
        End Get
        Set(value As String)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).DistanceFieldAlphaTexture = value
                Case GetType(BGEM)
                    ' No operation
            End Select
        End Set
    End Property

    <Category("Textures")>
    <Editor(GetType(DictionaryFilePickerEditor), GetType(UITypeEditor))>
    Public Property EnvmapMaskTexture As String
        Get
            ' BGSM has no native envmapMaskTexture field in its binary.
            ' Path lives runtime in _EnvmapMaskPath (populated from NIF texture set slot 5
            ' on load, or from sidecar `.bgsm.json` when deserializing from disk).
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return If(_EnvmapMaskPath, "")
                Case GetType(BGEM)
                    Return CType(Underlying_Material, BGEM).EnvmapMaskTexture
            End Select
            Throw New Exception("Unsupported material type")
        End Get
        Set(value As String)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    _EnvmapMaskPath = value
                Case GetType(BGEM)
                    CType(Underlying_Material, BGEM).EnvmapMaskTexture = value
            End Select
        End Set
    End Property

    <Category("Textures")>
    <BGSMOnly()>
    <Editor(GetType(DictionaryFilePickerEditor), GetType(UITypeEditor))>
    Public Property DetailMaskTexture As String
        Get
            ' BGSM: shares slot 3 with DisplacementTexture (FO4=displacement, SSE FaceTint=detail mask)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return CType(Underlying_Material, BGSM).DisplacementTexture
                Case GetType(BGEM)
                    Return ""
            End Select
            Throw New Exception("Unsupported material type")
        End Get
        Set(value As String)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).DisplacementTexture = value
                Case GetType(BGEM)
                    ' No operation
            End Select
        End Set
    End Property

    <Category("Textures")>
    <BGSMOnly()>
    <Editor(GetType(DictionaryFilePickerEditor), GetType(UITypeEditor))>
    Public Property TintMaskTexture As String
        Get
            ' BGSM: shares slot 6 with LightingTexture (SSE FaceTint=tint mask)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return CType(Underlying_Material, BGSM).LightingTexture
                Case GetType(BGEM)
                    Return ""
            End Select
            Throw New Exception("Unsupported material type")
        End Get
        Set(value As String)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).LightingTexture = value
                Case GetType(BGEM)
                    ' No operation
            End Select
        End Set
    End Property

    <Category("Opacity")>
    <DefaultValue(1.0F)>
    Public Property Alpha As Single
        Get
            Return Underlying_Material.Alpha
        End Get
        Set(value As Single)
            Underlying_Material.Alpha = value
        End Set
    End Property

    <Category("UVs")>
    <DefaultValue(0F)>
    Public Property UOffset As Single
        Get
            Return Underlying_Material.UOffset
        End Get
        Set(value As Single)
            Underlying_Material.UOffset = value
        End Set
    End Property

    <Category("UVs")>
    <DefaultValue(0F)>
    Public Property VOffset As Single
        Get
            Return Underlying_Material.VOffset
        End Get
        Set(value As Single)
            Underlying_Material.VOffset = value
        End Set
    End Property

    <Category("UVs")>
    <DefaultValue(1.0F)>
    Public Property UScale As Single
        Get
            Return Underlying_Material.UScale
        End Get
        Set(value As Single)
            Underlying_Material.UScale = value
        End Set
    End Property

    <Category("UVs")>
    <DefaultValue(1.0F)>
    Public Property VScale As Single
        Get
            Return Underlying_Material.VScale
        End Get
        Set(value As Single)
            Underlying_Material.VScale = value
        End Set
    End Property

    <Category("Opacity")>
    <DefaultValue(False)>
    Public Property AlphaTest As Boolean
        Get
            Return Underlying_Material.AlphaTest
        End Get
        Set(value As Boolean)
            Underlying_Material.AlphaTest = value
        End Set
    End Property

    <Category("Opacity")>
    <DefaultValue(CType(128, Byte))>
    Public Property AlphaTestRef As Byte
        Get
            Return Underlying_Material.AlphaTestRef
        End Get
        Set(value As Byte)
            Underlying_Material.AlphaTestRef = value
        End Set
    End Property

    <Category("Rendering")>
    <DefaultValue(False)>
    Public Property Decal As Boolean
        Get
            Return Underlying_Material.Decal
        End Get
        Set(value As Boolean)
            Underlying_Material.Decal = value
        End Set
    End Property


    <Category("Rendering")>
    <DefaultValue(False)>
    Public Property DecalNoFade As Boolean
        Get
            Return Underlying_Material.DecalNoFade
        End Get
        Set(value As Boolean)
            Underlying_Material.DecalNoFade = value
        End Set
    End Property

    <Category("Rendering")>
    <DefaultValue(False)>
    Public Property DepthBias As Boolean
        Get
            Return Underlying_Material.DepthBias
        End Get
        Set(value As Boolean)
            Underlying_Material.DepthBias = value
        End Set
    End Property

    <Category("Coloring")>
    <DefaultValue(False)>
    Public Property GrayscaleToPaletteColor As Boolean
        Get
            Return Underlying_Material.GrayscaleToPaletteColor
        End Get
        Set(value As Boolean)
            Underlying_Material.GrayscaleToPaletteColor = value
        End Set
    End Property

    <Category("Rendering")>
    <DefaultValue(False)>
    Public Property TwoSided As Boolean
        Get
            Return Underlying_Material.TwoSided
        End Get
        Set(value As Boolean)
            Underlying_Material.TwoSided = value
        End Set
    End Property


    <Category("Specular")>
    <DefaultValue(False)>
    Public Property EnvironmentMapping As Boolean
        Get
            Return Underlying_Material.EnvironmentMapping
        End Get
        Set(value As Boolean)
            Underlying_Material.EnvironmentMapping = value
        End Set
    End Property

    <Category("Specular")>
    <DefaultValue(1.0F)>
    Public Property EnvironmentMappingMaskScale As Single
        Get
            Return Underlying_Material.EnvironmentMappingMaskScale
        End Get
        Set(value As Single)
            Underlying_Material.EnvironmentMappingMaskScale = value
        End Set
    End Property

    <Category("Rendering")>
    <DefaultValue(False)>
    Public Property NonOccluder As Boolean
        Get
            Return Underlying_Material.NonOccluder
        End Get
        Set(value As Boolean)
            Underlying_Material.NonOccluder = value
        End Set
    End Property

    <Category("Opacity")>
    <DefaultValue(False)>
    Public Property Refraction As Boolean
        Get
            Return Underlying_Material.Refraction
        End Get
        Set(value As Boolean)
            Underlying_Material.Refraction = value
        End Set
    End Property

    <Category("Opacity")>
    <DefaultValue(False)>
    Public Property RefractionFalloff As Boolean
        Get
            Return Underlying_Material.RefractionFalloff
        End Get
        Set(value As Boolean)
            Underlying_Material.RefractionFalloff = value
        End Set
    End Property

    <Category("Opacity")>
    <DefaultValue(0F)>
    Public Property RefractionPower As Single
        Get
            Return Underlying_Material.RefractionPower
        End Get
        Set(value As Single)
            Underlying_Material.RefractionPower = value
        End Set
    End Property

    <Category("UVs")>
    <DefaultValue(True)>
    Public Property TileU As Boolean
        Get
            Return Underlying_Material.TileU
        End Get
        Set(value As Boolean)
            Underlying_Material.TileU = value
        End Set
    End Property

    <Category("UVs")>
    <DefaultValue(True)>
    Public Property TileV As Boolean
        Get
            Return Underlying_Material.TileV
        End Get
        Set(value As Boolean)
            Underlying_Material.TileV = value
        End Set
    End Property

    <Category("(Type)")>
    Public Property Version As UInteger
        Get
            Return Underlying_Material.Version
        End Get
        Set(value As UInteger)
            Underlying_Material.Version = value
        End Set
    End Property

    <Category("Rendering")>
    <DefaultValue(False)>
    Public Property WetnessControlScreenSpaceReflections As Boolean
        Get
            Return Underlying_Material.WetnessControlScreenSpaceReflections
        End Get
        Set(value As Boolean)
            Underlying_Material.WetnessControlScreenSpaceReflections = value
        End Set
    End Property

    <Category("Rendering")>
    <DefaultValue(True)>
    Public Property ZBufferTest As Boolean
        Get
            Return Underlying_Material.ZBufferTest
        End Get
        Set(value As Boolean)
            Underlying_Material.ZBufferTest = value
        End Set
    End Property

    <Category("Rendering")>
    <DefaultValue(True)>
    Public Property ZBufferWrite As Boolean
        Get
            Return Underlying_Material.ZBufferWrite
        End Get
        Set(value As Boolean)
            Underlying_Material.ZBufferWrite = value
        End Set
    End Property
    <Category("Coloring")>
    <DefaultValue(1.0F)>
    Public Property GrayscaleToPaletteScale As Single
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return CType(Underlying_Material, BGSM).GrayscaleToPaletteScale
                Case GetType(BGEM)
                    Return CType(Underlying_Material, BGEM).BaseColorScale
            End Select
            Throw New Exception
        End Get
        Set(value As Single)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).GrayscaleToPaletteScale = value
                Case GetType(BGEM)
                    CType(Underlying_Material, BGEM).BaseColorScale = value
            End Select
        End Set
    End Property

    <Category("Specular")>
    <BGSMOnly()>
    <DefaultValue(False)>
    Public Property SpecularEnabled As Boolean
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return CType(Underlying_Material, BGSM).SpecularEnabled
                Case GetType(BGEM)
                    Return False
            End Select
            Throw New Exception
        End Get
        Set(value As Boolean)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).SpecularEnabled = value
                Case GetType(BGEM)
            End Select
        End Set
    End Property

    <Category("UVs")>
    <BGSMOnly()>
    <DefaultValue(False)>
    Public Property ModelSpaceNormals As Boolean
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return CType(Underlying_Material, BGSM).ModelSpaceNormals
                Case GetType(BGEM)
                    Return False
            End Select
            Throw New Exception
        End Get
        Set(value As Boolean)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).ModelSpaceNormals = value
                Case GetType(BGEM)
            End Select
        End Set
    End Property

    <Category("Emissive")>
    <BGSMOnly()>
    <DefaultValue(False)>
    Public Property EmitEnabled As Boolean
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return CType(Underlying_Material, BGSM).EmitEnabled
                Case GetType(BGEM)
                    Return False
            End Select
            Throw New Exception
        End Get
        Set(value As Boolean)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).EmitEnabled = value
                Case GetType(BGEM)
            End Select
        End Set
    End Property

    <Category("Lighting")>
    <BGSMOnly()>
    <DefaultValue(False)>
    Public Property SubsurfaceLighting As Boolean
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return CType(Underlying_Material, BGSM).SubsurfaceLighting
                Case GetType(BGEM)
                    Return False
            End Select
            Throw New Exception
        End Get
        Set(value As Boolean)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).SubsurfaceLighting = value
                Case GetType(BGEM)
            End Select
        End Set
    End Property
    <Category("Lighting")>
    <BGSMOnly()>
    <DefaultValue(False)>
    Public Property BackLighting As Boolean
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return CType(Underlying_Material, BGSM).BackLighting
                Case GetType(BGEM)
                    Return False
            End Select
            Throw New Exception
        End Get
        Set(value As Boolean)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).BackLighting = value
                Case GetType(BGEM)
            End Select
        End Set
    End Property
    <Category("Lighting")>
    <BGSMOnly()>
    <DefaultValue(0F)>
    Public Property BackLightPower As Single
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return CType(Underlying_Material, BGSM).BackLightPower
                Case GetType(BGEM)
                    Return 0
            End Select
            Throw New Exception
        End Get
        Set(value As Single)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).BackLightPower = value
                Case GetType(BGEM)
            End Select
        End Set
    End Property
    <Category("Lighting")>
    <BGSMOnly()>
    <DefaultValue(False)>
    Public Property RimLighting As Boolean
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return CType(Underlying_Material, BGSM).RimLighting
                Case GetType(BGEM)
                    Return False
            End Select
            Throw New Exception
        End Get
        Set(value As Boolean)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).RimLighting = value
                Case GetType(BGEM)
            End Select
        End Set
    End Property
    <Category("Lighting")>
    <BGSMOnly()>
    <DefaultValue(2.0F)>
    Public Property RimPower As Single
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return CType(Underlying_Material, BGSM).RimPower
                Case GetType(BGEM)
                    Return 0
            End Select
            Throw New Exception
        End Get
        Set(value As Single)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).RimPower = value
                Case GetType(BGEM)
            End Select
        End Set
    End Property


    <Category("Emissive")>
    <DefaultValue(False)>
    Public Property Glowmap As Boolean
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return CType(Underlying_Material, BGSM).Glowmap
                Case GetType(BGEM)
                    Return CType(Underlying_Material, BGEM).Glowmap
            End Select
            Throw New Exception
        End Get
        Set(value As Boolean)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).Glowmap = value
                Case GetType(BGEM)
                    CType(Underlying_Material, BGEM).Glowmap = value
            End Select
        End Set
    End Property
    <Category("Specular")>
    <BGSMOnly()>
    <DefaultValue(1.0F)>
    Public Property Smoothness As Single
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return CType(Underlying_Material, BGSM).Smoothness
                Case GetType(BGEM)
                    Return 0
            End Select
            Throw New Exception
        End Get
        Set(value As Single)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).Smoothness = value
                Case GetType(BGEM)
            End Select
        End Set
    End Property


    <Category("Specular")>
    <BGSMOnly()>
    Public Property SpecularColor As Color
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return UIntegerToColor(CType(Underlying_Material, BGSM).SpecularColor)
                Case GetType(BGEM)
                    Return System.Drawing.Color.FromArgb(255, 255, 255, 255)
            End Select
            Throw New Exception
        End Get
        Set(value As Color)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).SpecularColor = ColorToUInteger(value)
                Case GetType(BGEM)
            End Select
        End Set
    End Property

    <Category("Emissive")>
    Public Property EmittanceColor As Color
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return UIntegerToColor(CType(Underlying_Material, BGSM).EmittanceColor)
                Case GetType(BGEM)
                    Return UIntegerToColor(CType(Underlying_Material, BGEM).EmittanceColor)
            End Select
            Throw New Exception
        End Get
        Set(value As Color)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).EmittanceColor = ColorToUInteger(value)
                Case GetType(BGEM)
                    CType(Underlying_Material, BGEM).EmittanceColor = ColorToUInteger(value)
            End Select
        End Set
    End Property

    <Category("Specular")>
    <BGSMOnly()>
    <DefaultValue(1.0F)>
    Public Property SpecularMult As Single
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return CType(Underlying_Material, BGSM).SpecularMult
                Case GetType(BGEM)
                    Return 1
            End Select
            Throw New Exception
        End Get
        Set(value As Single)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).SpecularMult = value
                Case GetType(BGEM)
            End Select
        End Set
    End Property

    <Category("Emissive")>
    <BGSMOnly()>
    <DefaultValue(1.0F)>
    Public Property EmittanceMult As Single
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return CType(Underlying_Material, BGSM).EmittanceMult
                Case GetType(BGEM)
                    Return 0
            End Select
            Throw New Exception
        End Get
        Set(value As Single)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).EmittanceMult = value
                Case GetType(BGEM)
            End Select
        End Set
    End Property

    <Category("Specular")>
    <BGSMOnly()>
    <DefaultValue(5.0F)>
    Public Property FresnelPower As Single
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return CType(Underlying_Material, BGSM).FresnelPower
                Case GetType(BGEM)
                    Return 0
            End Select
            Throw New Exception
        End Get
        Set(value As Single)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).FresnelPower = value
                Case GetType(BGEM)
            End Select
        End Set
    End Property

    <Category("Lighting")>
    <BGSMOnly()>
    <DefaultValue(0.3F)>
    Public Property SubsurfaceLightingRolloff As Single
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return CType(Underlying_Material, BGSM).SubsurfaceLightingRolloff
                Case GetType(BGEM)
                    Return 0
            End Select
            Throw New Exception
        End Get
        Set(value As Single)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).SubsurfaceLightingRolloff = value
                Case GetType(BGEM)
            End Select
        End Set
    End Property

    ' Propiedades exclusivas BGSM

    <Category("Textures")>
    <BGSMOnly()>
    <DefaultValue("")>
    Public Property RootMaterialPath As String
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return CType(Underlying_Material, BGSM).RootMaterialPath
                Case GetType(BGEM)
                    Return ""
                Case Else
                    Throw New Exception
            End Select
        End Get
        Set(value As String)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).RootMaterialPath = CorrectMaterialPath(value).StripPrefix(MaterialsPrefix)
                Case GetType(BGEM)
                    ' No action
                Case Else
                    Throw New Exception
            End Select
        End Set
    End Property

    <Category("Coloring")>
    <BGSMOnly()>
    <DefaultValue(False)>
    Public Property PBR As Boolean
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return CType(Underlying_Material, BGSM).PBR
                Case GetType(BGEM)
                    Return False
                Case Else
                    Throw New Exception
            End Select
        End Get
        Set(value As Boolean)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).PBR = value
                Case GetType(BGEM)
                    ' No action
                Case Else
                    Throw New Exception
            End Select
        End Set
    End Property

    <Category("Coloring")>
    <BGSMOnly()>
    <DefaultValue(False)>
    Public Property CustomPorosity As Boolean
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return CType(Underlying_Material, BGSM).CustomPorosity
                Case GetType(BGEM)
                    Return False
                Case Else
                    Throw New Exception
            End Select
        End Get
        Set(value As Boolean)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).CustomPorosity = value
                Case GetType(BGEM)
                    ' No action
                Case Else
                    Throw New Exception
            End Select
        End Set
    End Property

    <Category("Coloring")>
    <BGSMOnly()>
    <DefaultValue(0F)>
    Public Property PorosityValue As Single
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return CType(Underlying_Material, BGSM).PorosityValue
                Case GetType(BGEM)
                    Return 0
                Case Else
                    Throw New Exception
            End Select
        End Get
        Set(value As Single)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).PorosityValue = value
                Case GetType(BGEM)
                    ' No action
                Case Else
                    Throw New Exception
            End Select
        End Set
    End Property

    <Category("Coloring")>
    <BGSMOnly()>
    <DefaultValue(False)>
    Public Property Hair As Boolean
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return CType(Underlying_Material, BGSM).Hair
                Case GetType(BGEM)
                    Return False
                Case Else
                    Throw New Exception
            End Select
        End Get
        Set(value As Boolean)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).Hair = value
                Case GetType(BGEM)
                    ' No action
                Case Else
                    Throw New Exception
            End Select
        End Set
    End Property

    <Category("Coloring")>
    <BGSMOnly()>
    Public Property HairTintColor As Color
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return UIntegerToColor(CType(Underlying_Material, BGSM).HairTintColor)
                Case GetType(BGEM)
                    Return Color.Black
                Case Else
                    Throw New Exception
            End Select
        End Get
        Set(value As Color)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).HairTintColor = ColorToUInteger(value)
                Case GetType(BGEM)
                    ' No action
                Case Else
                    Throw New Exception
            End Select
        End Set
    End Property

    <Category("Coloring")>
    <BGSMOnly()>
    Public Property SkinTintColor As Color
        Get
            ' BGSM uses HairTintColor field for both hair and skin tint
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return UIntegerToColor(CType(Underlying_Material, BGSM).HairTintColor)
                Case GetType(BGEM)
                    Return Color.White
                Case Else
                    Throw New Exception
            End Select
        End Get
        Set(value As Color)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).HairTintColor = ColorToUInteger(value)
                Case GetType(BGEM)
                    ' No action
                Case Else
                    Throw New Exception
            End Select
        End Set
    End Property

    <Category("Coloring")>
    <BGSMOnly()>
    <DefaultValue(False)>
    Public Property Tree As Boolean
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return CType(Underlying_Material, BGSM).Tree
                Case GetType(BGEM)
                    Return False
                Case Else
                    Throw New Exception
            End Select
        End Get
        Set(value As Boolean)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).Tree = value
                Case GetType(BGEM)
                    ' No action
                Case Else
                    Throw New Exception
            End Select
        End Set
    End Property

    <Category("Coloring")>
    <BGSMOnly()>
    <DefaultValue(False)>
    Public Property Facegen As Boolean
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return CType(Underlying_Material, BGSM).Facegen
                Case GetType(BGEM)
                    Return False
                Case Else
                    Throw New Exception
            End Select
        End Get
        Set(value As Boolean)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).Facegen = value
                Case GetType(BGEM)
                    ' No action
                Case Else
                    Throw New Exception
            End Select
        End Set
    End Property

    <Category("Coloring")>
    <BGSMOnly()>
    <DefaultValue(False)>
    Public Property SkinTint As Boolean
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return CType(Underlying_Material, BGSM).SkinTint
                Case GetType(BGEM)
                    Return False
                Case Else
                    Throw New Exception
            End Select
        End Get
        Set(value As Boolean)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).SkinTint = value
                Case GetType(BGEM)
                    ' No action
                Case Else
                    Throw New Exception
            End Select
        End Set
    End Property
    ' Propiedades exclusivas BGEM

    <Category("Effect (BGEM)")>
    <BGEMOnly>
    <DefaultValue(False)>
    Public Property FalloffEnabled As Boolean
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return False
                Case GetType(BGEM)
                    Return CType(Underlying_Material, BGEM).FalloffEnabled
                Case Else
                    Throw New Exception
            End Select
        End Get
        Set(value As Boolean)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    ' No action
                Case GetType(BGEM)
                    CType(Underlying_Material, BGEM).FalloffEnabled = value
                Case Else
                    Throw New Exception
            End Select
        End Set
    End Property

    <Category("Effect (BGEM)")>
    <BGEMOnly>
    <DefaultValue(False)>
    Public Property FalloffColorEnabled As Boolean
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return False
                Case GetType(BGEM)
                    Return CType(Underlying_Material, BGEM).FalloffColorEnabled
                Case Else
                    Throw New Exception
            End Select
        End Get
        Set(value As Boolean)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    ' No action
                Case GetType(BGEM)
                    CType(Underlying_Material, BGEM).FalloffColorEnabled = value
                Case Else
                    Throw New Exception
            End Select
        End Set
    End Property

    <Category("Effect (BGEM)")>
    <BGEMOnly>
    <DefaultValue(1.0F)>
    Public Property FalloffStartAngle As Single
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return 0
                Case GetType(BGEM)
                    Return CType(Underlying_Material, BGEM).FalloffStartAngle
                Case Else
                    Throw New Exception
            End Select
        End Get
        Set(value As Single)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    ' No action
                Case GetType(BGEM)
                    CType(Underlying_Material, BGEM).FalloffStartAngle = value
                Case Else
                    Throw New Exception
            End Select
        End Set
    End Property

    <Category("Effect (BGEM)")>
    <BGEMOnly>
    <DefaultValue(1.0F)>
    Public Property FalloffStopAngle As Single
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return 0
                Case GetType(BGEM)
                    Return CType(Underlying_Material, BGEM).FalloffStopAngle
                Case Else
                    Throw New Exception
            End Select
        End Get
        Set(value As Single)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    ' No action
                Case GetType(BGEM)
                    CType(Underlying_Material, BGEM).FalloffStopAngle = value
                Case Else
                    Throw New Exception
            End Select
        End Set
    End Property

    <Category("Effect (BGEM)")>
    <BGEMOnly>
    <DefaultValue(0F)>
    Public Property FalloffStartOpacity As Single
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return 0
                Case GetType(BGEM)
                    Return CType(Underlying_Material, BGEM).FalloffStartOpacity
                Case Else
                    Throw New Exception
            End Select
        End Get
        Set(value As Single)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    ' No action
                Case GetType(BGEM)
                    CType(Underlying_Material, BGEM).FalloffStartOpacity = value
                Case Else
                    Throw New Exception
            End Select
        End Set
    End Property

    <Category("Effect (BGEM)")>
    <BGEMOnly>
    <DefaultValue(0F)>
    Public Property FalloffStopOpacity As Single
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return 0
                Case GetType(BGEM)
                    Return CType(Underlying_Material, BGEM).FalloffStopOpacity
                Case Else
                    Throw New Exception
            End Select
        End Get
        Set(value As Single)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    ' No action
                Case GetType(BGEM)
                    CType(Underlying_Material, BGEM).FalloffStopOpacity = value
                Case Else
                    Throw New Exception
            End Select
        End Set
    End Property

    <Category("Effect (BGEM)")>
    <BGEMOnly>
    <DefaultValue(1.0F)>
    Public Property LightingInfluence As Single
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return 0
                Case GetType(BGEM)
                    Return CType(Underlying_Material, BGEM).LightingInfluence
                Case Else
                    Throw New Exception
            End Select
        End Get
        Set(value As Single)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    ' No action
                Case GetType(BGEM)
                    CType(Underlying_Material, BGEM).LightingInfluence = value
                Case Else
                    Throw New Exception
            End Select
        End Set
    End Property

    <Category("Coloring")>
    <BGEMOnly>
    <DefaultValue(False)>
    Public Property GrayscaleToPaletteAlpha As Boolean
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return False
                Case GetType(BGEM)
                    Return CType(Underlying_Material, BGEM).GrayscaleToPaletteAlpha
                Case Else
                    Throw New Exception
            End Select
        End Get
        Set(value As Boolean)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    ' No action
                Case GetType(BGEM)
                    CType(Underlying_Material, BGEM).GrayscaleToPaletteAlpha = value
                Case Else
                    Throw New Exception
            End Select
        End Set
    End Property

    <Category("Coloring")>
    <BGEMOnly>
    Public Property BaseColor As Color
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return System.Drawing.Color.FromArgb(255, 255, 255, 255)
                Case GetType(BGEM)
                    Return UIntegerToColor(CType(Underlying_Material, BGEM).BaseColor)
                Case Else
                    Throw New Exception
            End Select
        End Get
        Set(value As Color)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    ' No action
                Case GetType(BGEM)
                    CType(Underlying_Material, BGEM).BaseColor = ColorToUInteger(value)
                Case Else
                    Throw New Exception
            End Select
        End Set
    End Property

    <Category("Coloring")>
    <BGEMOnly>
    <DefaultValue(1.0F)>
    Public Property BaseColorScale As Single
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return 0
                Case GetType(BGEM)
                    Return CType(Underlying_Material, BGEM).BaseColorScale
                Case Else
                    Throw New Exception
            End Select
        End Get
        Set(value As Single)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    ' No action
                Case GetType(BGEM)
                    CType(Underlying_Material, BGEM).BaseColorScale = value
                Case Else
                    Throw New Exception
            End Select
        End Set
    End Property

    <Category("Effect (BGEM)")>
    <BGEMOnly>
    <DefaultValue(False)>
    Public Property BloodEnabled As Boolean
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return False
                Case GetType(BGEM)
                    Return CType(Underlying_Material, BGEM).BloodEnabled
                Case Else
                    Throw New Exception
            End Select
        End Get
        Set(value As Boolean)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    ' No action
                Case GetType(BGEM)
                    CType(Underlying_Material, BGEM).BloodEnabled = value
                Case Else
                    Throw New Exception
            End Select
        End Set
    End Property


    <Category("Effect (BGEM)")>
    <BGEMOnly>
    <DefaultValue(False)>
    Public Property EffectLightingEnabled As Boolean
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return False
                Case GetType(BGEM)
                    Return CType(Underlying_Material, BGEM).EffectLightingEnabled
                Case Else
                    Throw New Exception
            End Select
        End Get
        Set(value As Boolean)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    ' No action
                Case GetType(BGEM)
                    CType(Underlying_Material, BGEM).EffectLightingEnabled = value
                Case Else
                    Throw New Exception
            End Select
        End Set
    End Property

    <Category("Effect (BGEM)")>
    <BGEMOnly>
    <DefaultValue(False)>
    Public Property SoftEnabled As Boolean
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return False
                Case GetType(BGEM)
                    Return CType(Underlying_Material, BGEM).SoftEnabled
                Case Else
                    Throw New Exception
            End Select
        End Get
        Set(value As Boolean)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    ' No action
                Case GetType(BGEM)
                    CType(Underlying_Material, BGEM).SoftEnabled = value
                Case Else
                    Throw New Exception
            End Select
        End Set
    End Property

    <Category("Effect (BGEM)")>
    <BGEMOnly>
    <DefaultValue(100.0F)>
    Public Property SoftDepth As Single
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return 0
                Case GetType(BGEM)
                    Return CType(Underlying_Material, BGEM).SoftDepth
                Case Else
                    Throw New Exception
            End Select
        End Get
        Set(value As Single)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    ' No action
                Case GetType(BGEM)
                    CType(Underlying_Material, BGEM).SoftDepth = value
                Case Else
                    Throw New Exception
            End Select
        End Set
    End Property

    <Category("Effect (BGEM)")>
    <BGEMOnly>
    <DefaultValue(False)>
    Public Property EffectPbrSpecular As Boolean
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return False
                Case GetType(BGEM)
                    Return CType(Underlying_Material, BGEM).EffectPbrSpecular
                Case Else
                    Throw New Exception
            End Select
        End Get
        Set(value As Boolean)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    ' No action
                Case GetType(BGEM)
                    CType(Underlying_Material, BGEM).EffectPbrSpecular = value
                Case Else
                    Throw New Exception
            End Select
        End Set
    End Property

    <Category("Emissive")>
    <DefaultValue(0F)>
    Public Property AdaptativeEmissive_ExposureOffset As Single
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return CType(Underlying_Material, BGSM).AdaptativeEmissive_ExposureOffset
                Case GetType(BGEM)
                    Return CType(Underlying_Material, BGEM).AdaptativeEmissive_ExposureOffset
                Case Else
                    Throw New Exception
            End Select
        End Get
        Set(value As Single)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).AdaptativeEmissive_ExposureOffset = value
                Case GetType(BGEM)
                    CType(Underlying_Material, BGEM).AdaptativeEmissive_ExposureOffset = value
                Case Else
                    Throw New Exception
            End Select
        End Set
    End Property

    <Category("Emissive")>
    <DefaultValue(0F)>
    Public Property AdaptativeEmissive_FinalExposureMin As Single
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return CType(Underlying_Material, BGSM).AdaptativeEmissive_FinalExposureMin
                Case GetType(BGEM)
                    Return CType(Underlying_Material, BGEM).AdaptativeEmissive_FinalExposureMin
                Case Else
                    Throw New Exception
            End Select
        End Get
        Set(value As Single)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).AdaptativeEmissive_FinalExposureMin = value
                Case GetType(BGEM)
                    CType(Underlying_Material, BGEM).AdaptativeEmissive_FinalExposureMin = value
                Case Else
                    Throw New Exception
            End Select
        End Set
    End Property

    <Category("Emissive")>
    <DefaultValue(0F)>
    Public Property AdaptativeEmissive_FinalExposureMax As Single
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return CType(Underlying_Material, BGSM).AdaptativeEmissive_FinalExposureMax
                Case GetType(BGEM)
                    Return CType(Underlying_Material, BGEM).AdaptativeEmissive_FinalExposureMax
                Case Else
                    Throw New Exception
            End Select
        End Get
        Set(value As Single)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).AdaptativeEmissive_FinalExposureMax = value
                Case GetType(BGEM)
                    CType(Underlying_Material, BGEM).AdaptativeEmissive_FinalExposureMax = value
                Case Else
                    Throw New Exception
            End Select
        End Set
    End Property
    ''' <summary>
    ''' Heuristic best-guess: derive a BSLightingShaderType from BGSM flags when no
    ''' NIF info is available (e.g. standalone .bgsm load).
    '''
    ''' Per BodySlide wiki Shape-Properties + Fallout wiki Materials Files Basics,
    ''' "the shader type is still relevant" meaning ShaderType lives in the NIF
    ''' and is not stored in the BGSM binary directly. The NIF shader always wins
    ''' (callers should preserve a non-Default _NifShaderType).
    '''
    ''' BUT: per nifly C++ Shaders.cpp:597-602, the engine evaluates feature flags
    ''' as AND with ShaderType — `HasGlowmap = ShaderType==GlowShader AND flag` and
    ''' `HasEnvironmentMapping = ShaderType==EnvironmentMap AND flag`. So a
    ''' well-formed BGSM that has Glowmap=True implies the original NIF had
    ''' ShaderType=GlowShader (otherwise the flag would have been inert and CK
    ''' wouldn't write it). Same logic for EnvironmentMap.
    '''
    ''' Precedence Glowmap > Facegen > SkinTint > Hair > Tree > Terrain > EnvironmentMap.
    ''' EnvironmentMap is last because the heuristic can't know whether the cubemap
    ''' (slot 4) is actually populated; if it isn't, Save_To_Shader degrades back to
    ''' Default (see effectiveShaderType logic at the BGSM Save_To_Shader entry).
    ''' </summary>
    Public Shared Function DeriveShaderTypeFromBgsm(bgsm As BGSM) As NiflySharp.Enums.BSLightingShaderType
        If bgsm Is Nothing Then Return NiflySharp.Enums.BSLightingShaderType.Default
        If bgsm.Glowmap Then Return NiflySharp.Enums.BSLightingShaderType.GlowShader
        If bgsm.Facegen Then Return NiflySharp.Enums.BSLightingShaderType.FaceTint
        If bgsm.SkinTint Then Return NiflySharp.Enums.BSLightingShaderType.SkinTint
        If bgsm.Hair Then Return NiflySharp.Enums.BSLightingShaderType.HairTint
        If bgsm.Tree Then Return NiflySharp.Enums.BSLightingShaderType.TreeAnim
        If bgsm.Terrain Then Return NiflySharp.Enums.BSLightingShaderType.MultitextureLandscape
        If bgsm.EnvironmentMapping Then Return NiflySharp.Enums.BSLightingShaderType.EnvironmentMap
        Return NiflySharp.Enums.BSLightingShaderType.Default
    End Function

    ''' <summary>
    ''' When the user assigns a ShaderType to the unified material, sync the BGSM
    ''' boolean flags that the engine evaluates as AND with the ShaderType (see
    ''' nifly C++ Shaders.cpp:597-602): Glowmap, EnvironmentMapping, plus the five
    ''' `IsType*` flags that have a 1:1 enum counterpart (Facegen, SkinTint, Hair,
    ''' Tree, Terrain).
    '''
    ''' Engine semantics (from nifly C++):
    '''   HasGlowmap()            = (ShaderType == GlowShader)    AND (SF2 bit 6)
    '''   HasEnvironmentMapping() = (ShaderType == EnvironmentMap) AND (SF1 bit 7)
    ''' So a BGSM with Glowmap=True but ShaderType=HairTint is INCONSISTENT — the
    ''' engine ignores Glowmap because the ShaderType vetoes it. Clearing Glowmap
    ''' here when ShaderType ≠ GlowShader keeps the BGSM consistent with what the
    ''' render will actually do, and avoids a flag that lies about the shape.
    '''
    ''' ShaderTypes without a BGSM flag (Parallax, EyeEnvmap, MultiLayerParallax,
    ''' etc.) clear all 7 toggled flags — there is no engine-faithful BGSM flag for
    ''' them and the heuristic best-guess returns Default for those.
    '''
    ''' BGEM is a no-op (passing Nothing): BSEffectShaderProperty has no ShaderType
    ''' enum, so there is nothing to mirror.
    ''' </summary>
    Public Shared Sub ApplyShaderTypeToBgsm(bgsm As BGSM, type As NiflySharp.Enums.BSLightingShaderType)
        If bgsm Is Nothing Then Exit Sub
        bgsm.Glowmap = (type = NiflySharp.Enums.BSLightingShaderType.GlowShader)
        bgsm.EnvironmentMapping = (type = NiflySharp.Enums.BSLightingShaderType.EnvironmentMap)
        bgsm.Facegen = (type = NiflySharp.Enums.BSLightingShaderType.FaceTint)
        bgsm.SkinTint = (type = NiflySharp.Enums.BSLightingShaderType.SkinTint)
        bgsm.Hair = (type = NiflySharp.Enums.BSLightingShaderType.HairTint)
        bgsm.Tree = (type = NiflySharp.Enums.BSLightingShaderType.TreeAnim)
        bgsm.Terrain = (type = NiflySharp.Enums.BSLightingShaderType.MultitextureLandscape)
    End Sub

    ''' <summary>
    ''' Default BGSM/BGEM binary Version to assign when creating a material from a
    ''' NIF shader (no .bgsm/.bgem of origin to preserve). Derived from the NIF
    ''' stream version, mirroring NiflySharp's BeforeSync game detection
    ''' (BSLightingShaderProperty.cs / BSEffectShaderProperty.cs).
    '''
    ''' SSE (StreamVersion &lt; 130) → v20: covers every field NiflySharp's
    ''' BSLightingShaderProperty / BSEffectShaderProperty actually exposes
    ''' (DepthBias, MaskWrites, LumEmittance via Luminance, AdaptativeEmissive,
    ''' DistanceFieldAlphaTexture, etc.). Does NOT default to v22 because the
    ''' v21+ Glass block and v20+ EffectPbrSpecular have no shader counterpart
    ''' in NiflySharp; defaulting higher would persist "dead" defaults.
    '''
    ''' FO4 (StreamVersion ≥ 130, ≠ 155) → v2: vanilla CK FO4 emission. Mods may
    ''' carry v17 (DistanceFieldAlpha); when loading from disk the audited
    ''' Deserialize path (BaseMaterialFile.Deserialize) preserves the original
    ''' Version, so v17 .bgsm survive a round-trip even though we default to v2
    ''' for shader-only origins.
    '''
    ''' FO76 (StreamVersion = 155) is out of scope; we still return v2 to stay
    ''' in the "safe minimum" range — the unified does not target FO76.
    ''' </summary>
    Public Shared Function DefaultMaterialVersionForNif(Nif As Nifcontent_Class_Manolo) As UInteger
        If Nif Is Nothing OrElse Nif.Header Is Nothing Then Return 2UI
        Dim streamVer = Nif.Header.Version.StreamVersion
        If streamVer = 155 Then Return 2UI               ' FO76 — out of scope, safe min
        If streamVer >= 130 Then Return 2UI              ' FO4 vanilla
        Return 20UI                                       ' SSE / SK
    End Function

    ''' <summary>
    ''' Tuple of the three independent alpha-blend bytes the NIF/BGSM binary format carries:
    ''' the enable flag and the two blend factors. Used to canonicalize the four named modes
    ''' (None/Standard/Additive/Multiplicative) and to classify a free tuple back into one of
    ''' those four (or Unknown).
    ''' </summary>
    Public Structure AlphaBlendTuple
        Public Enabled As Boolean
        Public Src As NiflySharp.Enums.AlphaFunction
        Public Dst As NiflySharp.Enums.AlphaFunction
    End Structure

    ''' <summary>
    ''' Canonical (enabled, src, dst) tuple for each named mode. Mirrors the byte tuples
    ''' MaterialLib serializes (BaseMaterialFile.cs:389-422): None=(0,0,0), Standard=(1,6,7),
    ''' Additive=(1,6,0), Multiplicative=(1,4,1). Unknown has no canonical tuple — its three
    ''' values are caller-provided (read from the NIF's NiAlphaProperty).
    ''' </summary>
    Public Shared Function CanonicalTuple(mode As AlphaBlendModeType) As AlphaBlendTuple
        Select Case mode
            Case AlphaBlendModeType.None
                Return New AlphaBlendTuple With {.Enabled = False, .Src = NiflySharp.Enums.AlphaFunction.ONE, .Dst = NiflySharp.Enums.AlphaFunction.ZERO}
            Case AlphaBlendModeType.Standard
                Return New AlphaBlendTuple With {.Enabled = True, .Src = NiflySharp.Enums.AlphaFunction.SRC_ALPHA, .Dst = NiflySharp.Enums.AlphaFunction.INV_SRC_ALPHA}
            Case AlphaBlendModeType.Additive
                Return New AlphaBlendTuple With {.Enabled = True, .Src = NiflySharp.Enums.AlphaFunction.SRC_ALPHA, .Dst = NiflySharp.Enums.AlphaFunction.ONE}
            Case AlphaBlendModeType.Multiplicative
                Return New AlphaBlendTuple With {.Enabled = True, .Src = NiflySharp.Enums.AlphaFunction.DEST_COLOR, .Dst = NiflySharp.Enums.AlphaFunction.ZERO}
            Case Else
                ' Unknown: no canonical tuple. Caller should not invoke for Unknown.
                Return New AlphaBlendTuple With {.Enabled = False, .Src = NiflySharp.Enums.AlphaFunction.SRC_ALPHA, .Dst = NiflySharp.Enums.AlphaFunction.INV_SRC_ALPHA}
        End Select
    End Function

    ''' <summary>
    ''' Inverse of <see cref="CanonicalTuple"/>: classify a free (enabled, src, dst) tuple
    ''' against the four canonical patterns. Anything that doesn't match returns Unknown.
    ''' </summary>
    Public Shared Function ClassifyTuple(enabled As Boolean, src As NiflySharp.Enums.AlphaFunction, dst As NiflySharp.Enums.AlphaFunction) As AlphaBlendModeType
        If Not enabled AndAlso src = NiflySharp.Enums.AlphaFunction.ONE AndAlso dst = NiflySharp.Enums.AlphaFunction.ZERO Then
            Return AlphaBlendModeType.None
        End If
        If enabled AndAlso src = NiflySharp.Enums.AlphaFunction.SRC_ALPHA AndAlso dst = NiflySharp.Enums.AlphaFunction.INV_SRC_ALPHA Then
            Return AlphaBlendModeType.Standard
        End If
        If enabled AndAlso src = NiflySharp.Enums.AlphaFunction.SRC_ALPHA AndAlso dst = NiflySharp.Enums.AlphaFunction.ONE Then
            Return AlphaBlendModeType.Additive
        End If
        If enabled AndAlso src = NiflySharp.Enums.AlphaFunction.DEST_COLOR AndAlso dst = NiflySharp.Enums.AlphaFunction.ZERO Then
            Return AlphaBlendModeType.Multiplicative
        End If
        Return AlphaBlendModeType.Unknown
    End Function

    ''' <summary>
    ''' Reactive auto-promotion / degradation: after any of the three blend fields changes
    ''' (AlphaBlendEnabled / BlendFunctionSource / BlendFunctionDest), reclassify the tuple
    ''' and update AlphaBlendMode. Writes directly to Underlying_Material to bypass the public
    ''' setter (which would re-derive the three fields and break the reactive flow).
    ''' </summary>
    Private Sub RecomputeAlphaBlendModeFromFields()
        Underlying_Material.AlphaBlendMode = ClassifyTuple(_alphaBlendEnabled, _blendFunctionSource, _blendFunctionDest)
    End Sub

    ''' <summary>
    ''' Centralized write of the blend-related bits of a NiAlphaProperty.Flags. Called from
    ''' both Save_To_Shader overloads and from SetRelatedMaterial (FO4 path) so all writers
    ''' stay in lockstep. Writes the three independent fields verbatim — no enum derivation —
    ''' so Unknown round-trips its arbitrary (src, dst) factors faithfully to the NIF.
    ''' </summary>
    Private Sub WriteBlendFlagsToAlphaProperty(alp As NiAlphaProperty)
        alp.Flags.AlphaBlend = _alphaBlendEnabled
        alp.Flags.SourceBlendMode = _blendFunctionSource
        alp.Flags.DestinationBlendMode = _blendFunctionDest
    End Sub

    ''' <summary>
    ''' Sync the shape's NiAlphaProperty with the wrapper's full alpha state (blend bit +
    ''' factors + AlphaTest + Threshold). Used by Save_To_Shader (BSLighting / BSEffect) when
    ''' writing the full shader, and by SetRelatedMaterial (FO4 path) which otherwise only
    ''' updates the BGSM path string and would leave the NIF alpha state stale.
    '''
    ''' Removes the NiAlphaProperty block via Nif.RemoveBlock if neither blend nor test are
    ''' required, matching CK's "no NiAlphaProperty when not needed" behavior. NiflySharp
    ''' NifFile.RemoveBlock re-indexes refs/pointers automatically (NifFile.cs:1157-1198).
    ''' </summary>
    Friend Sub WriteAlphaPropertyToShape(shap As INiShape, Nif As Nifcontent_Class_Manolo)
        Dim needAlphaProperty = _alphaBlendEnabled OrElse Underlying_Material.AlphaTest
        If needAlphaProperty Then
            If IsNothing(shap.AlphaPropertyRef) OrElse shap.AlphaPropertyRef.Index = -1 Then
                shap.AlphaPropertyRef = New NiBlockRef(Of NiAlphaProperty) With {.Index = Nif.AddBlock(New NiAlphaProperty)}
            End If
            Dim alp = CType(Nif.Blocks(shap.AlphaPropertyRef.Index), NiAlphaProperty)
            alp.Flags.AlphaTest = Underlying_Material.AlphaTest
            alp.Threshold = Underlying_Material.AlphaTestRef
            WriteBlendFlagsToAlphaProperty(alp)
        Else
            If shap.AlphaPropertyRef IsNot Nothing AndAlso shap.AlphaPropertyRef.Index <> -1 Then
                Nif.RemoveBlock(shap.AlphaPropertyRef.Index)
            End If
        End If
    End Sub
    Public Sub Create_From_Shader(Nif As Nifcontent_Class_Manolo, shap As INiShape, shad As BSLightingShaderProperty)
        If Nif.Valid = False Then Exit Sub

        Dim mat As BGSM

        If Not IsNothing(shad) Then

            mat = New BGSM With {
                .TwoSided = shad.DoubleSided,
                .UOffset = shad.UVOffset.U,
                .VOffset = shad.UVOffset.V,
                .UScale = shad.UVScale.U,
                .VScale = shad.UVScale.V,
                .EmitEnabled = shad.Emissive,
                .EmittanceColor = NifColorColorToUInteger(shad.EmissiveColor),
                .EmittanceMult = shad.EmissiveMultiple,
                .Alpha = shad.Alpha,
                .EnvironmentMapping = shad.HasEnvironmentMapping,
                .EnvironmentMappingMaskScale = shad.EnvironmentMapScale,
                .ModelSpaceNormals = shad.ModelSpace,
                .Facegen = shad.IsTypeFaceTint,
                .Hair = shad.IsTypeHairTint,
                .SkinTint = shad.IsTypeSkinTint,
                .BackLighting = shad.HasBacklight,
                .BackLightPower = shad.BacklightPower,
                .SpecularEnabled = shad.HasSpecular,
                .SpecularColor = ColorToUInteger(NifColorToColor(shad.SpecularColor)),
                .SpecularMult = shad.SpecularStrength,
                .Glowmap = shad.HasGlowmap,
                .Tree = shad.HasTreeAnim,
                .SubsurfaceLighting = shad.HasSoftlight,
                .RimLighting = shad.HasRimlight,
                .RimPower = shad.RimlightPower,
                .GrayscaleToPaletteColor = shad.HasGreyscaleToPaletteColor,
                .GrayscaleToPaletteScale = shad.GrayscaleToPaletteScale,
                .FresnelPower = shad.FresnelPower,
                .HairTintColor = If(shad.IsTypeSkinTint,
                                    ColorToUInteger(NifColorToColor(shad.SkinTintColor)),
                                    If(shad.IsTypeHairTint,
                                        ColorToUInteger(NifColorToColor(shad.HairTintColor)),
                                        CUInt(&H808080UI))),
                .Smoothness = If(Nif.Header.Version.IsSSE,
                                  CSng(Math.Max(0.0, (Math.Log(Math.Max(CDbl(shad.Glossiness), 2.0), 2.0) - 1.0) / 10.0)),
                                  shad.Smoothness),
                .SubsurfaceLightingRolloff = shad.SubsurfaceRolloff,
                .ExternalEmittance = shad.HasExternalEmittance,
                .EnvironmentMappingEye = shad.HasEyeEnvironmentMapping,
                .RootMaterialPath = If(shad.RootMaterialName, ""),
                .ScreenSpaceReflections = shad.UseScreenSpaceReflections,
                .WetnessControlScreenSpaceReflections = shad.WetnessControl_UseSSR,
                .RefractionPower = shad.RefractionStrength,
                .WetnessControlSpecScale = shad.Wetness.SpecScale,
                .WetnessControlSpecPowerScale = shad.Wetness.SpecPower,
                .WetnessControlSpecMinvar = shad.Wetness.MinVar,
                .WetnessControlEnvMapScale = shad.Wetness.EnvMapScale,
                .WetnessControlFresnelPower = shad.Wetness.FresnelPower,
                .WetnessControlMetalness = shad.Wetness.Metalness,
                .CastShadows = ShaderHelper.HasFlagSF1(shad, CUInt(NiflySharp.Enums.Fallout4ShaderPropertyFlags1.Cast_Shadows)),
                .HideSecret = ShaderHelper.HasFlagSF1(shad, CUInt(NiflySharp.Enums.Fallout4ShaderPropertyFlags1.Localmap_Hide_Secret)),
                .Decal = ShaderHelper.HasFlagSF1(shad, CUInt(NiflySharp.Enums.Fallout4ShaderPropertyFlags1.Decal)),
                .DecalNoFade = ShaderHelper.HasFlagSF2(shad, CUInt(NiflySharp.Enums.Fallout4ShaderPropertyFlags2.No_Fade)),
                .ZBufferWrite = ShaderHelper.HasFlagSF2(shad, CUInt(NiflySharp.Enums.Fallout4ShaderPropertyFlags2.ZBuffer_Write)),
                .ZBufferTest = ShaderHelper.HasFlagSF1(shad, CUInt(NiflySharp.Enums.Fallout4ShaderPropertyFlags1.ZBuffer_Test)),
                .Refraction = ShaderHelper.HasFlagSF1(shad, CUInt(NiflySharp.Enums.Fallout4ShaderPropertyFlags1.Refraction)),
                .AnisoLighting = ShaderHelper.HasFlagSF2(shad, CUInt(NiflySharp.Enums.Fallout4ShaderPropertyFlags2.Anisotropic_Lighting)),
                .Tessellate = (Not Nif.Header.Version.IsSSE) AndAlso ((shad.ShaderFlags_F4SPF1 And NiflySharp.Enums.Fallout4ShaderPropertyFlags1.Tessellate) <> 0),
                .SkewSpecularAlpha = (Not Nif.Header.Version.IsSSE) AndAlso ((shad.ShaderFlags_F4SPF2 And NiflySharp.Enums.Fallout4ShaderPropertyFlags2.Skew_Specular_Alpha) <> 0)
            }
            If Not IsNothing(shad.TextureSetRef) AndAlso shad.TextureSetRef.Index <> -1 Then
                Dim texset = TryCast(Nif.Blocks(shad.TextureSetRef.Index), BSShaderTextureSet)
                ReadBgsmTexturesFromTextureSet(mat, texset, Nif.Header.Version.IsSSE, _EnvmapMaskPath)
            End If
        Else
            mat = New BGSM
        End If
        mat.Version = DefaultMaterialVersionForNif(Nif)
        mat.AlphaTest = False
        mat.AlphaTestRef = 128
        mat.AlphaBlendMode = AlphaBlendModeType.None
        Underlying_Material = mat
        _NifShaderType = shad.ShaderType_SK_FO4
        ' Embedded shader: no BGSM file, so NiAlphaProperty is the only source of alpha state.
        ' This is the ONE load path that legitimately reads NiAlphaProperty. Resolve to a
        ' deterministic AlphaBlendMode AND deterministic BlendFunctionSource/Dest here so the
        ' wrapper carries everything afterwards — render/save never re-read the NIF.
        ApplyAlphaPropertyFromNif(shap, Nif)
        ClearDirty()
    End Sub

    ''' <summary>
    ''' Read the shape's NiAlphaProperty and project the three independent fields
    ''' (AlphaBlendEnabled / BlendFunctionSource / BlendFunctionDest) plus AlphaTest /
    ''' Threshold onto the wrapper. The enum is then classified from the resulting tuple —
    ''' Unknown is preserved verbatim (no Standard-promotion) so callers and renderers see
    ''' the actual NIF state. Shared between the two Create_From_Shader overloads
    ''' (BSLighting / BSEffect) and used by GetRelatedMaterial when the BGSM enum is Unknown.
    ''' </summary>
    Private Sub ApplyAlphaPropertyFromNif(shap As INiShape, Nif As Nifcontent_Class_Manolo)
        Dim alp As NiAlphaProperty = Nothing
        If shap IsNot Nothing AndAlso Not IsNothing(shap.AlphaPropertyRef) AndAlso shap.AlphaPropertyRef.Index <> -1 Then
            alp = TryCast(Nif.Blocks(shap.AlphaPropertyRef.Index), NiAlphaProperty)
        End If
        If alp Is Nothing Then
            Underlying_Material.AlphaTest = False
            Underlying_Material.AlphaTestRef = 128
            _suppressAutoPromotion = True
            Try
                _alphaBlendEnabled = False
                _blendFunctionSource = NiflySharp.Enums.AlphaFunction.INV_SRC_ALPHA
                _blendFunctionDest = NiflySharp.Enums.AlphaFunction.SRC_ALPHA
            Finally
                _suppressAutoPromotion = False
            End Try
            Underlying_Material.AlphaBlendMode = AlphaBlendModeType.None
            Return
        End If
        Underlying_Material.AlphaTest = alp.Flags.AlphaTest
        Underlying_Material.AlphaTestRef = alp.Threshold
        _suppressAutoPromotion = True
        Try
            _alphaBlendEnabled = alp.Flags.AlphaBlend
            _blendFunctionSource = alp.Flags.SourceBlendMode
            _blendFunctionDest = alp.Flags.DestinationBlendMode
        Finally
            _suppressAutoPromotion = False
        End Try
        Underlying_Material.AlphaBlendMode = ClassifyTuple(_alphaBlendEnabled, _blendFunctionSource, _blendFunctionDest)
    End Sub

    Public Sub Create_From_Shader(Nif As Nifcontent_Class_Manolo, shap As INiShape, shad As BSEffectShaderProperty)
        If Nif.Valid = False Then Exit Sub
        Dim mat As BGEM
        If Not IsNothing(shad) Then
            mat = New BGEM With {
            .TwoSided = shad.DoubleSided,
            .BaseTexture = If(shad.SourceTexture?.Content, String.Empty),
            .GrayscaleTexture = If(shad.GreyscaleTexture?.Content, String.Empty),
            .NormalTexture = If(shad.NormalTexture?.Content, String.Empty),
            .EnvmapMaskTexture = If(shad.EnvMaskTexture?.Content, String.Empty),
            .EnvmapTexture = If(shad.EnvMapTexture?.Content, String.Empty),
            .LightingTexture = If(shad.LightingTexture?.Content, String.Empty),
            .SpecularTexture = If(shad.ReflectanceTexture?.Content, String.Empty),
            .GlowTexture = If(shad.EmitGradientTexture?.Content, String.Empty),
            .UOffset = shad.UVOffset.U,
            .VOffset = shad.UVOffset.V,
            .UScale = shad.UVScale.U,
            .VScale = shad.UVScale.V,
            .EnvironmentMapping = shad.HasEnvironmentMapping,
            .EnvironmentMappingMaskScale = shad.EnvironmentMapScale,
            .EmittanceColor = ColorToUInteger(NifColorToColor(shad.EmittanceColor)),
            .FalloffEnabled = ShaderHelper.HasFlagSF1(shad, ShaderHelper.FalloffFlagValue(shad)),
            .FalloffColorEnabled = Not Nif.Header.Version.IsSSE AndAlso (shad.ShaderFlags_F4SPF1 And NiflySharp.Enums.Fallout4ShaderPropertyFlags1.RGB_Falloff) <> 0,
            .GrayscaleToPaletteColor = shad.HasGreyscaleToPaletteColor,
            .GrayscaleToPaletteAlpha = shad.HasGreyscaleToPaletteAlpha,
            .EffectLightingEnabled = (If(Nif.Header.Version.IsSSE,
                                        (shad.ShaderFlags_SSPF2 And NiflySharp.Enums.SkyrimShaderPropertyFlags2.Effect_Lighting) <> 0,
                                        (shad.ShaderFlags_F4SPF2 And NiflySharp.Enums.Fallout4ShaderPropertyFlags2.Effect_Lighting) <> 0)),
            .BaseColor = NifColorColorToUInteger(shad.BaseColor),
            .BaseColorScale = shad.BaseColorScale,
            .FalloffStartAngle = shad.FalloffStartAngle,
            .FalloffStopAngle = shad.FalloffStopAngle,
            .FalloffStartOpacity = shad.FalloffStartOpacity,
            .FalloffStopOpacity = shad.FalloffStopOpacity,
            .LightingInfluence = shad.LightingInfluence / 255.0F,
            .SoftDepth = shad.SoftFalloffDepth,
            .Glowmap = shad.HasGlowmap,
            .EnvmapMinLOD = shad.EnvMapMinLOD,
            .BloodEnabled = ShaderHelper.HasFlagSF2(shad, CUInt(NiflySharp.Enums.Fallout4ShaderPropertyFlags2.Weapon_Blood)),
            .SoftEnabled = ShaderHelper.HasFlagSF1(shad, CUInt(NiflySharp.Enums.Fallout4ShaderPropertyFlags1.Soft_Effect)),
            .Decal = ShaderHelper.HasFlagSF1(shad, CUInt(NiflySharp.Enums.Fallout4ShaderPropertyFlags1.Decal)),
            .DecalNoFade = ShaderHelper.HasFlagSF2(shad, CUInt(NiflySharp.Enums.Fallout4ShaderPropertyFlags2.No_Fade)),
            .ZBufferWrite = ShaderHelper.HasFlagSF2(shad, CUInt(NiflySharp.Enums.Fallout4ShaderPropertyFlags2.ZBuffer_Write)),
            .ZBufferTest = ShaderHelper.HasFlagSF1(shad, CUInt(NiflySharp.Enums.Fallout4ShaderPropertyFlags1.ZBuffer_Test)),
            .Refraction = ShaderHelper.HasFlagSF1(shad, CUInt(NiflySharp.Enums.Fallout4ShaderPropertyFlags1.Refraction))
                       }
        Else
            mat = New BGEM
        End If
        mat.Version = DefaultMaterialVersionForNif(Nif)
        mat.AlphaTest = False
        mat.AlphaTestRef = 128
        mat.AlphaBlendMode = AlphaBlendModeType.None
        Underlying_Material = mat
        ' Same alpha-state binding as the BSLightingShaderProperty overload — shared via
        ' ApplyAlphaPropertyFromNif (embedded path is the only legitimate NIF AlphaProperty read).
        ApplyAlphaPropertyFromNif(shap, Nif)
        ClearDirty()
    End Sub
    Public Sub Save_To_Shader(Nif As Nifcontent_Class_Manolo, shap As INiShape, shad As BSEffectShaderProperty)
        If Nif.Valid = False Then Exit Sub
        Dim Mat = DirectCast(Underlying_Material, BGEM)
        ' Force shader.Type from NIF version when None (cloned/in-memory shaders).
        ' Same rationale as BSLightingShaderProperty Save_To_Shader.
        If shad.Type = NiflySharp.Helpers.ShaderHelper.ShaderGameType.None Then
            Dim streamVer = Nif.Header.Version.StreamVersion
            If streamVer = 155 Then
                shad.Type = NiflySharp.Helpers.ShaderHelper.ShaderGameType.FO76SF
            ElseIf streamVer >= 130 Then
                shad.Type = NiflySharp.Helpers.ShaderHelper.ShaderGameType.FO4
            Else
                shad.Type = NiflySharp.Helpers.ShaderHelper.ShaderGameType.SK
            End If
        End If
        shad.DoubleSided = Mat.TwoSided
        shad.UVOffset = New TexCoord(Mat.UOffset, Mat.VOffset)
        shad.UVScale = New TexCoord(Mat.UScale, Mat.VScale)
        shad.EnvironmentMapScale = Mat.EnvironmentMappingMaskScale
        shad.EmittanceColor = UIntegerToNifColor3(Mat.EmittanceColor)
        EnsureNiString4(shad.SourceTexture, Mat.BaseTexture)
        EnsureNiString4(shad.NormalTexture, Mat.NormalTexture)
        EnsureNiString4(shad.GreyscaleTexture, Mat.GrayscaleTexture)
        EnsureNiString4(shad.EnvMapTexture, Mat.EnvmapTexture)
        EnsureNiString4(shad.EnvMaskTexture, Mat.EnvmapMaskTexture)
        EnsureNiString4(shad.LightingTexture, Mat.LightingTexture)
        EnsureNiString4(shad.ReflectanceTexture, Mat.SpecularTexture)
        EnsureNiString4(shad.EmitGradientTexture, Mat.GlowTexture)

        ' Effect-specific properties (BaseColor, Falloff, Lighting Influence, Greyscale flags)
        shad.BaseColor = UIntegerToNifColor4(Mat.BaseColor)
        shad.BaseColorScale = Mat.BaseColorScale
        shad.FalloffStartAngle = Mat.FalloffStartAngle
        shad.FalloffStopAngle = Mat.FalloffStopAngle
        shad.FalloffStartOpacity = Mat.FalloffStartOpacity
        shad.FalloffStopOpacity = Mat.FalloffStopOpacity
        shad.LightingInfluence = CByte(Math.Min(255, Math.Max(0, CInt(Mat.LightingInfluence * 255.0F))))
        shad.HasGreyscaleToPaletteAlpha = Mat.GrayscaleToPaletteAlpha
        shad.HasGreyscaleToPaletteColor = Mat.GrayscaleToPaletteColor
        If Mat.SoftEnabled Then shad.SoftFalloffDepth = Mat.SoftDepth
        shad.HasGlowmap = Mat.Glowmap
        shad.EnvMapMinLOD = Mat.EnvmapMinLOD

        ShaderHelper.SetFlagSF2(shad, CUInt(NiflySharp.Enums.Fallout4ShaderPropertyFlags2.Weapon_Blood), Mat.BloodEnabled)
        ShaderHelper.SetFlagSF1(shad, CUInt(NiflySharp.Enums.Fallout4ShaderPropertyFlags1.Soft_Effect), Mat.SoftEnabled)
        ShaderHelper.SetFlagSF1(shad, CUInt(NiflySharp.Enums.Fallout4ShaderPropertyFlags1.Decal), Mat.Decal)
        ShaderHelper.SetFlagSF2(shad, CUInt(NiflySharp.Enums.Fallout4ShaderPropertyFlags2.No_Fade), Mat.DecalNoFade)
        ShaderHelper.SetFlagSF2(shad, CUInt(NiflySharp.Enums.Fallout4ShaderPropertyFlags2.ZBuffer_Write), Mat.ZBufferWrite)
        ShaderHelper.SetFlagSF1(shad, CUInt(NiflySharp.Enums.Fallout4ShaderPropertyFlags1.ZBuffer_Test), Mat.ZBufferTest)
        ShaderHelper.SetFlagSF1(shad, CUInt(NiflySharp.Enums.Fallout4ShaderPropertyFlags1.Refraction), Mat.Refraction)

        ' Shader flags for Falloff and EffectLighting
        ShaderHelper.SetFlagSF1(shad, ShaderHelper.FalloffFlagValue(shad), Mat.FalloffEnabled)
        If Nif.Header.Version.IsSSE Then
            ' SSE: EffectLighting in SF2
            If Mat.EffectLightingEnabled Then
                shad.ShaderFlags_SSPF2 = shad.ShaderFlags_SSPF2 Or NiflySharp.Enums.SkyrimShaderPropertyFlags2.Effect_Lighting
            Else
                shad.ShaderFlags_SSPF2 = shad.ShaderFlags_SSPF2 And Not NiflySharp.Enums.SkyrimShaderPropertyFlags2.Effect_Lighting
            End If
        Else
            ' FO4: EffectLighting in SF2, FalloffColor via RGB_Falloff in SF1
            If Mat.EffectLightingEnabled Then
                shad.ShaderFlags_F4SPF2 = shad.ShaderFlags_F4SPF2 Or NiflySharp.Enums.Fallout4ShaderPropertyFlags2.Effect_Lighting
            Else
                shad.ShaderFlags_F4SPF2 = shad.ShaderFlags_F4SPF2 And Not NiflySharp.Enums.Fallout4ShaderPropertyFlags2.Effect_Lighting
            End If
            If Mat.FalloffColorEnabled Then
                shad.ShaderFlags_F4SPF1 = shad.ShaderFlags_F4SPF1 Or NiflySharp.Enums.Fallout4ShaderPropertyFlags1.RGB_Falloff
            Else
                shad.ShaderFlags_F4SPF1 = shad.ShaderFlags_F4SPF1 And Not NiflySharp.Enums.Fallout4ShaderPropertyFlags1.RGB_Falloff
            End If
        End If

        WriteAlphaPropertyToShape(shap, Nif)
    End Sub
    Public Sub Save_To_Shader(Nif As Nifcontent_Class_Manolo, shap As INiShape, shad As BSLightingShaderProperty, Optional shaderType As NiflySharp.Enums.BSLightingShaderType = NiflySharp.Enums.BSLightingShaderType.Default, Optional envmapMaskPath As String = "")
        If Nif.Valid = False Then Exit Sub
        Dim Mat = DirectCast(Underlying_Material, BGSM)
        ' NiflySharp only sets shad.Type during BeforeSync (deserialize). A shader created
        ' or cloned in memory keeps Type=None, which makes the flag helpers (HasGlowmap,
        ' HasGreyscaleToPaletteColor, etc.) silently no-op because GlowmapFlagValue and
        ' SetFlagSF2 fall through to `_ => 0` for any non-matching Type.
        ' Derive the game from the NIF stream version (mirrors NiflySharp's BeforeSync
        ' logic at BSLightingShaderProperty.cs:286-309): version <130 → SK, >=130 → FO4,
        ' ==155 → FO76SF. Multi-game safe.
        ' Verified empirically Alijo 2026-05-07: HairFemale03 cloned from base NIF had
        ' shad.Type=None, Save_To_Shader's `shad.HasGlowmap = True` left SF2 unchanged.
        If shad.Type = NiflySharp.Helpers.ShaderHelper.ShaderGameType.None Then
            Dim streamVer = Nif.Header.Version.StreamVersion
            If streamVer = 155 Then
                shad.Type = NiflySharp.Helpers.ShaderHelper.ShaderGameType.FO76SF
            ElseIf streamVer >= 130 Then
                shad.Type = NiflySharp.Helpers.ShaderHelper.ShaderGameType.FO4
            Else
                shad.Type = NiflySharp.Helpers.ShaderHelper.ShaderGameType.SK
            End If
        End If
        shad.DoubleSided = Mat.TwoSided
        shad.UVOffset = New TexCoord(Mat.UOffset, Mat.VOffset)
        shad.UVScale = New TexCoord(Mat.UScale, Mat.VScale)
        shad.Emissive = Mat.EmitEnabled
        shad.EmissiveColor = UIntegerToNifColor4(Mat.EmittanceColor)
        shad.EmissiveMultiple = Mat.EmittanceMult
        shad.Alpha = Mat.Alpha

        ' Empty-cubemap degradation at bake time. When the shader claimed EnvironmentMap
        ' but the BGSM has no EnvmapTexture (cubemap, NIF slot 4), the env-map shader
        ' cannot do anything — write Default + HasEnvironmentMapping=False so the bake
        ' matches what CK emits (verified Alijo 2026-05-07 across 3 NPCs: Lashes BGSM
        ' has no cubemap and CK degrades to Default; EyesHazel/Wet keep EnvironmentMap
        ' because their BGSM does carry shared/cubemaps/eyecubemap.dds).
        Dim effectiveShaderType = shaderType
        Dim effectiveEnvMapping = Mat.EnvironmentMapping
        If shaderType = NiflySharp.Enums.BSLightingShaderType.EnvironmentMap _
           AndAlso String.IsNullOrEmpty(Mat.EnvmapTexture) Then
            effectiveShaderType = NiflySharp.Enums.BSLightingShaderType.Default
            effectiveEnvMapping = False
        End If

        shad.HasEnvironmentMapping = effectiveEnvMapping
        shad.EnvironmentMapScale = If(effectiveEnvMapping, Mat.EnvironmentMappingMaskScale, 1.0F)
        If Nif.Header.Version.IsSSE Then
            shad.Glossiness = CSng(Math.Pow(2.0, CDbl(Mat.Smoothness) * 10.0 + 1.0))
        Else
            shad.Smoothness = Mat.Smoothness
        End If
        ' SubsurfaceRolloff centinela: CK only writes the BGSM Rolloff value when
        ' SubsurfaceLighting=True; otherwise it normalizes the inline shader to 0
        ' regardless of what the BGSM file carries (verified empirically against CK
        ' bakes 2026-05-09 across 8 NPCs: every shape with BGSM SubsurfaceLighting=False
        ' bakes inline SubsurfaceRolloff=0, even when the BGSM stores 0.3/0.5/etc.).
        ' Without this gate the round-trip mat.SubsurfaceLightingRolloff diverges from CK
        ' on hair/beard/lashes/wet/head where the BGSM ships a non-zero default but the
        ' material does not actually opt into softlight.
        shad.SubsurfaceRolloff = If(Mat.SubsurfaceLighting, Mat.SubsurfaceLightingRolloff, 0.0F)
        shad.ModelSpace = Mat.ModelSpaceNormals
        shad.ShaderType_SK_FO4 = effectiveShaderType
        ' BGSM stores both hair tint and skin tint in the single HairTintColor field
        ' (Material-Editor BGSM.cs:43, default 0x808080). Mirror it to BOTH shader
        ' fields unconditionally so the shader's HairTintColor and SkinTintColor stay
        ' in sync with the material — previous code only wrote one of them depending
        ' on Mat.SkinTint, which left the other shader field with stale data when the
        ' shader was reused/cloned.
        Dim hairTintNifColor = UIntegerToNifColor3(Mat.HairTintColor)
        shad.HairTintColor = hairTintNifColor
        If Mat.SkinTint Then shad.SkinTintColor = hairTintNifColor
        shad.HasBacklight = Mat.BackLighting
        shad.BacklightPower = Mat.BackLightPower
        ' HasSpecular centinela: SpecularMult=0 means the specular contribution is
        ' multiplicatively zero — engine renders no specular regardless of the bool flag.
        ' CK normalizes the inline shader flag to False in that case (verified vs Lashes
        ' bake: BGSM SpecularEnabled=True but SpecularMult=0, BAKED-CK HasSpecular=False).
        shad.HasSpecular = Mat.SpecularEnabled AndAlso Mat.SpecularMult <> 0.0F
        shad.SpecularColor = UIntegerToNifColor3(Mat.SpecularColor)
        shad.SpecularStrength = Mat.SpecularMult
        shad.HasGlowmap = Mat.Glowmap
        shad.HasTreeAnim = Mat.Tree
        shad.HasSoftlight = Mat.SubsurfaceLighting
        shad.HasRimlight = Mat.RimLighting
        ' RimlightPower in the FO4-era NIF schema (StreamVersion 130-139) is the centinela that
        ' gates BacklightPower serialization: BacklightPower is only written to disk when
        ' rimlightPower2 >= Single.MaxValue (nifly Shaders.cpp:474-478, NiflySharp generated
        ' BSMain.BSLightingShaderProperty.g.cs:474-478). CK bakes always emit MaxValue because
        ' it's the C++ default of the FO4-specific rimlightPower2 field. If we wrote
        ' Mat.RimPower verbatim, the centinela disarms and BacklightPower is dropped → reads
        ' back as 0. Force MaxValue for the entire FO4-era stream range so BacklightPower
        ' round-trips. Mat.RimPower is preserved by the BGSM file on disk, not by the inline
        ' shader — engine consults the BGSM at load.
        Dim streamVerForRim = Nif.Header.Version.StreamVersion
        If streamVerForRim >= 130 AndAlso streamVerForRim <= 139 Then
            shad.RimlightPower = Single.MaxValue
        Else
            shad.RimlightPower = Mat.RimPower
        End If
        shad.HasGreyscaleToPaletteColor = Mat.GrayscaleToPaletteColor
        shad.GrayscaleToPaletteScale = Mat.GrayscaleToPaletteScale
        shad.FresnelPower = Mat.FresnelPower

        shad.HasExternalEmittance = Mat.ExternalEmittance
        shad.HasEyeEnvironmentMapping = Mat.EnvironmentMappingEye
        shad.RootMaterialName = Mat.RootMaterialPath
        shad.UseScreenSpaceReflections = Mat.ScreenSpaceReflections
        shad.WetnessControl_UseSSR = Mat.WetnessControlScreenSpaceReflections
        shad.RefractionStrength = Mat.RefractionPower

        Dim wet = shad.Wetness
        wet.SpecScale = Mat.WetnessControlSpecScale
        wet.SpecPower = Mat.WetnessControlSpecPowerScale
        wet.MinVar = Mat.WetnessControlSpecMinvar
        wet.EnvMapScale = Mat.WetnessControlEnvMapScale
        wet.FresnelPower = Mat.WetnessControlFresnelPower
        wet.Metalness = Mat.WetnessControlMetalness
        shad.Wetness = wet

        ShaderHelper.SetFlagSF1(shad, CUInt(NiflySharp.Enums.Fallout4ShaderPropertyFlags1.Cast_Shadows), Mat.CastShadows)
        ShaderHelper.SetFlagSF1(shad, CUInt(NiflySharp.Enums.Fallout4ShaderPropertyFlags1.Localmap_Hide_Secret), Mat.HideSecret)
        ShaderHelper.SetFlagSF1(shad, CUInt(NiflySharp.Enums.Fallout4ShaderPropertyFlags1.Decal), Mat.Decal)
        ShaderHelper.SetFlagSF2(shad, CUInt(NiflySharp.Enums.Fallout4ShaderPropertyFlags2.No_Fade), Mat.DecalNoFade)
        ShaderHelper.SetFlagSF2(shad, CUInt(NiflySharp.Enums.Fallout4ShaderPropertyFlags2.ZBuffer_Write), Mat.ZBufferWrite)
        ShaderHelper.SetFlagSF1(shad, CUInt(NiflySharp.Enums.Fallout4ShaderPropertyFlags1.ZBuffer_Test), Mat.ZBufferTest)
        ShaderHelper.SetFlagSF1(shad, CUInt(NiflySharp.Enums.Fallout4ShaderPropertyFlags1.Refraction), Mat.Refraction)
        ShaderHelper.SetFlagSF2(shad, CUInt(NiflySharp.Enums.Fallout4ShaderPropertyFlags2.Anisotropic_Lighting), Mat.AnisoLighting)

        If Not Nif.Header.Version.IsSSE Then
            If Mat.Tessellate Then
                shad.ShaderFlags_F4SPF1 = shad.ShaderFlags_F4SPF1 Or NiflySharp.Enums.Fallout4ShaderPropertyFlags1.Tessellate
            Else
                shad.ShaderFlags_F4SPF1 = shad.ShaderFlags_F4SPF1 And Not NiflySharp.Enums.Fallout4ShaderPropertyFlags1.Tessellate
            End If
            If Mat.SkewSpecularAlpha Then
                shad.ShaderFlags_F4SPF2 = shad.ShaderFlags_F4SPF2 Or NiflySharp.Enums.Fallout4ShaderPropertyFlags2.Skew_Specular_Alpha
            Else
                shad.ShaderFlags_F4SPF2 = shad.ShaderFlags_F4SPF2 And Not NiflySharp.Enums.Fallout4ShaderPropertyFlags2.Skew_Specular_Alpha
            End If
        End If

        If IsNothing(shad.TextureSetRef) OrElse shad.TextureSetRef.Index = -1 Then
            Dim texset1 = New BSShaderTextureSet
            shad.TextureSetRef = New NiBlockRef(Of BSShaderTextureSet) With {.Index = Nif.AddBlock(texset1)}
            texset1.Textures = New List(Of NiString4)
        End If

        Dim texset = CType(Nif.Blocks(shad.TextureSetRef.Index), BSShaderTextureSet)
        WriteBgsmTexturesToTextureSet(Mat, texset, Nif.Header.Version.IsSSE, envmapMaskPath)

        ' NiAlphaProperty sync via shared helper. Removes the block when neither blend nor
        ' test are needed, matching CK's "no NiAlphaProperty when not needed" behavior — see
        ' BAKED-CK observations on FemaleHeadHuman/Hazel/MouthDirty/HeadRear vs shapes like
        ' Eyes/Hair/NeckGore that do require it. NiflySharp NifFile.RemoveBlock re-indexes
        ' refs/pointers automatically (NifFile.cs:1157-1198).
        WriteAlphaPropertyToShape(shap, Nif)
    End Sub
    ''' <summary>
    ''' Sync a shader's NiString4 texture slot with a target content string. If the
    ''' slot is null (typical for in-memory cloned shaders that never went through
    ''' BeforeSync), allocate a new NiString4; otherwise just update its Content.
    ''' Used to deduplicate the 8 identical IsNothing/New/Else/Content blocks in
    ''' Save_To_Shader(BGEM) for SourceTexture, NormalTexture, GreyscaleTexture,
    ''' EnvMapTexture, EnvMaskTexture, LightingTexture, ReflectanceTexture and
    ''' EmitGradientTexture.
    ''' </summary>
    Private Shared Sub EnsureNiString4(ByRef target As NiString4, content As String)
        If target Is Nothing Then
            target = New NiString4(content)
        Else
            target.Content = content
        End If
    End Sub

    Private Shared Sub EnsureShaderTextureSetSlots(texset As BSShaderTextureSet)
        If IsNothing(texset) Then Exit Sub

        If IsNothing(texset.Textures) Then
            texset.Textures = New List(Of NiString4)
        End If

        While texset.Textures.Count < 8
            texset.Textures.Add(New NiString4 With {.Content = ""})
        End While

        For i As Integer = 0 To 7
            If IsNothing(texset.Textures(i)) Then
                texset.Textures(i) = New NiString4 With {.Content = ""}
            ElseIf IsNothing(texset.Textures(i).Content) Then
                texset.Textures(i).Content = ""
            End If
        Next
    End Sub

    Private Const textset_dDiffuseTexture As Integer = 0
    Private Const textset_NormalTexture As Integer = 1
    Private Const textset_GlowTexture As Integer = 2
    Private Const textset_DisplacementTexture As Integer = 3
    Private Const textset_EnvmapTexture As Integer = 4
    Private Const textset_FlowTexture As Integer = 5
    Private Const textset_LightingTexture As Integer = 6
    Private Const textset_SmoothSpecTextureAs As Integer = 7
    Private Shared Sub ReadBgsmTexturesFromTextureSet(mat As BGSM, texset As BSShaderTextureSet, isSSE As Boolean, ByRef envmapMaskPath As String)
        If IsNothing(mat) OrElse IsNothing(texset) Then Exit Sub

        EnsureShaderTextureSetSlots(texset)

        mat.DiffuseTexture = texset.Textures(textset_dDiffuseTexture).Content
        mat.NormalTexture = texset.Textures(textset_NormalTexture).Content
        ' Slot 3: FO4 = Greyscale palette (BodySlide GLMaterial.cpp:70, PreviewWindow.cpp:439, fo4_default.frag:15).
        ' SSE legacy kept on DisplacementTexture until evidence says otherwise.
        If isSSE Then
            mat.DisplacementTexture = texset.Textures(textset_DisplacementTexture).Content
        Else
            mat.GreyscaleTexture = texset.Textures(textset_DisplacementTexture).Content
        End If
        mat.EnvmapTexture = texset.Textures(textset_EnvmapTexture).Content
        ' Slot 5: FO4 = EnvMask (lives only in NIF texture set, not in BGSM binary).
        ' SSE legacy kept on FlowTexture.
        If isSSE Then
            mat.FlowTexture = texset.Textures(textset_FlowTexture).Content
        Else
            envmapMaskPath = texset.Textures(textset_FlowTexture).Content
        End If
        mat.SmoothSpecTexture = texset.Textures(textset_SmoothSpecTextureAs).Content

        ' Slot 2: glow OR lightmask (SSE dual-purpose)
        Dim slot2 = texset.Textures(textset_GlowTexture).Content
        If isSSE AndAlso Not mat.Glowmap AndAlso (mat.SubsurfaceLighting OrElse mat.RimLighting) Then
            mat.LightingTexture = slot2
            mat.GlowTexture = ""
        Else
            mat.GlowTexture = slot2
            ' Slot 6: FO4 has no sampler here; BGSM.LightingTexture is the 8th binary string,
            ' not slot 6. Do not cross-assign. SSE dual-purpose handled below.
        End If

        ' Slot 6: lightmask OR tintmask (SSE dual-purpose)
        If isSSE AndAlso String.IsNullOrEmpty(mat.LightingTexture) Then
            mat.LightingTexture = texset.Textures(textset_LightingTexture).Content
        End If
    End Sub

    Private Shared Sub WriteBgsmTexturesToTextureSet(mat As BGSM, texset As BSShaderTextureSet, isSSE As Boolean, envmapMaskPath As String)
        If IsNothing(mat) OrElse IsNothing(texset) Then Exit Sub

        EnsureShaderTextureSetSlots(texset)

        texset.Textures(textset_dDiffuseTexture).Content = mat.DiffuseTexture
        texset.Textures(textset_NormalTexture).Content = mat.NormalTexture
        ' Slot 3: FO4 = Greyscale palette (BodySlide GLMaterial.cpp:70, PreviewWindow.cpp:439, fo4_default.frag:15).
        ' SSE legacy kept on DisplacementTexture until evidence says otherwise.
        If isSSE Then
            texset.Textures(textset_DisplacementTexture).Content = mat.DisplacementTexture
        Else
            texset.Textures(textset_DisplacementTexture).Content = mat.GreyscaleTexture
        End If
        texset.Textures(textset_EnvmapTexture).Content = mat.EnvmapTexture
        ' Slot 5: FO4 = EnvMask (runtime path, no BGSM binary field).
        ' SSE legacy writes FlowTexture here.
        If isSSE Then
            texset.Textures(textset_FlowTexture).Content = mat.FlowTexture
        Else
            texset.Textures(textset_FlowTexture).Content = If(envmapMaskPath, "")
        End If
        texset.Textures(textset_SmoothSpecTextureAs).Content = mat.SmoothSpecTexture

        ' Slot 2/6: glow vs lightmask remapping (SSE dual-purpose)
        If isSSE AndAlso Not mat.Glowmap AndAlso (mat.SubsurfaceLighting OrElse mat.RimLighting) Then
            texset.Textures(textset_GlowTexture).Content = mat.LightingTexture
            texset.Textures(textset_LightingTexture).Content = ""
        Else
            texset.Textures(textset_GlowTexture).Content = mat.GlowTexture
            ' Slot 6: SSE writes LightingTexture; FO4 has no sampler here, so we
            ' clear slot 6 explicitly. Without this, reusing a texset from a SSE
            ' source NIF on a FO4 save would leave stale data behind.
            If isSSE Then
                texset.Textures(textset_LightingTexture).Content = mat.LightingTexture
            Else
                texset.Textures(textset_LightingTexture).Content = ""
            End If
        End If
    End Sub
    Public Sub Deserialize(Memory As Byte(), type As Type, shap As INiShape, Nif As Nifcontent_Class_Manolo)
        If Memory.Length = 0 Then Exit Sub
        ' Step 1: seed the three independent alpha fields from the NIF's NiAlphaProperty.
        ' Required so the Unknown branch below can preserve the NIF state (BGSM Unknown can't
        ' carry the alpha state independently — the byte tuple is hardcoded to (0,6,7) by ME).
        ApplyAlphaPropertyFromNif(shap, Nif)
        ' Step 2: deserialize the BGSM/BGEM payload. Reassigns Underlying_Material — anything
        ' Step 1 wrote into Underlying_Material (AlphaTest, AlphaBlendMode, etc.) is discarded;
        ' only the three private backing fields survive.
        Using ms As New MemoryStream(Memory)
            Using reader As New BinaryReader(ms)
                Select Case type
                    Case GetType(BGSM)
                        Underlying_Material = New BGSM
                    Case GetType(BGEM)
                        Underlying_Material = New BGEM
                    Case Else
                        Throw New Exception("Tipo no soportado en Deserialize.")
                End Select
                Underlying_Material.Deserialize(reader)
                reader.Close()
            End Using
            ms.Close()
        End Using
        ' Step 3: apply the canonical-vs-Unknown rule:
        '   - Canonical (None/Standard/Additive/Multiplicative): BGSM wins. Overwrite the three
        '     fields with the canonical tuple (discards what Step 1 read from the NIF).
        '   - Unknown: BGSM serializer hardcodes (0,6,7) and can't carry the actual state, so
        '     the NIF wins. Leave the three fields as Step 1 set them (or as a prior caller did).
        '     Round-trip preservation: Underlying_Material.AlphaBlendMode stays Unknown, so a
        '     subsequent Save round-trips the BGSM disk byte tuple verbatim.
        Dim mode = Underlying_Material.AlphaBlendMode
        If mode <> AlphaBlendModeType.Unknown Then
            Dim t = CanonicalTuple(mode)
            _suppressAutoPromotion = True
            Try
                _alphaBlendEnabled = t.Enabled
                _blendFunctionSource = t.Src
                _blendFunctionDest = t.Dst
            Finally
                _suppressAutoPromotion = False
            End Try
        End If
        If type Is GetType(BGSM) Then
            ' ShaderType resolution. Two sources, in priority order:
            '   1. BGSM-derived (DeriveShaderTypeFromBgsm): maps BGSM flags Facegen/SkinTint/
            '      Hair/Tree/Terrain/Glowmap/EnvironmentMapping to ShaderType. Covers 5+ enum
            '      values. WINS when non-Default — authoring tool's explicit intent.
            '   2. NIF shader ShaderType_SK_FO4: covers all 21 enum values (Parallax,
            '      MultilayerParallax, etc. that BGSM flags can't express). Used as fallback
            '      when BGSM-derived stayed Default — covers the 16 non-flagged enum values.
            ' Empirical motivation: HairFemale03_Hairline's NIF shader is Default but the BGSM
            ' (hairshort_lgrad_8bit.bgsm) carries Hair=True; the promotion to HairTint must
            ' survive — overwriting with NIF Default loses what CK relies on at bake time.
            If _NifShaderType = NiflySharp.Enums.BSLightingShaderType.Default Then
                _NifShaderType = DeriveShaderTypeFromBgsm(CType(Underlying_Material, BGSM))
                If _NifShaderType = NiflySharp.Enums.BSLightingShaderType.Default Then
                    Dim bslsp = TryCast(Nif.GetShader(shap), BSLightingShaderProperty)
                    If bslsp IsNot Nothing Then _NifShaderType = bslsp.ShaderType_SK_FO4
                End If
            End If
        End If
        ' Fresh load → clean state. Any subsequent user edit through the PropertyGrid (via
        ' DirtyAwarePropertyDescriptor) or explicit MarkDirty() will flip IsDirty back to True.
        ClearDirty()
    End Sub

    Public Sub Deserialize(Diccionario As String, type As Type, shap As INiShape, Nif As Nifcontent_Class_Manolo)
        Deserialize(FilesDictionary_class.GetBytes(Diccionario), type, shap, Nif)
        ' Sidecar JSON resolution: `<file>.bgsm.json` (or `.bgem.json`) lives next to the
        ' material, resolvable via FilesDictionary the same way (loose or BA2/BSA). Carries
        ' fields the binary BGSM/BGEM cannot persist — today: envmapMaskTexture for BGSM.
        ' Missing or invalid sidecar is silent (regla Q3=a): _EnvmapMaskPath stays "".
        Try
            Dim sidecarKey = Diccionario & ".json"
            Dim sidecarBytes = FilesDictionary_class.GetBytes(sidecarKey)
            If sidecarBytes IsNot Nothing AndAlso sidecarBytes.Length > 0 Then
                Try
                    Using doc = JsonDocument.Parse(sidecarBytes, New JsonDocumentOptions With {.CommentHandling = JsonCommentHandling.Skip, .AllowTrailingCommas = True})
                        Dim root = doc.RootElement
                        If type Is GetType(BGSM) Then
                            Dim envmask As JsonElement = Nothing
                            If root.TryGetProperty("envmapMaskTexture", envmask) AndAlso envmask.ValueKind = JsonValueKind.String Then
                                _EnvmapMaskPath = If(envmask.GetString(), "")
                            End If
                        End If
                    End Using
                Catch jsonEx As Exception
                    ' Q3=a: ignore invalid JSON silently.
                End Try
            End If
        Catch ex As Exception
            ' Q3=a: any failure (read, dictionary miss, etc.) is silent.
        End Try
        ' Fresh load (including sidecar) → clean state.
        ClearDirty()
    End Sub

    ''' <summary>Serialize the underlying BGSM to disk and write a `.bgsm.json` sidecar
    ''' next to it carrying the envmapMaskTexture path (which the BGSM binary cannot
    ''' persist). If _EnvmapMaskPath is empty, no sidecar is written and any existing
    ''' loose sidecar is removed so stale state doesn't survive.</summary>
    Public Sub Save_To_Bgsm(filePath As String)
        Dim bgsm = TryCast(Underlying_Material, BGSM)
        If bgsm Is Nothing Then
            Throw New InvalidOperationException("Save_To_Bgsm: Underlying_Material is not a BGSM (" & Underlying_Material?.GetType().Name & ")")
        End If
        Using stream = File.Open(filePath, FileMode.Create)
            bgsm.Save(stream)
        End Using
        WriteEnvmapMaskSidecar(filePath, _EnvmapMaskPath)
        ' Just persisted to disk → state in memory matches the file → clean.
        ClearDirty()
    End Sub

    ''' <summary>Serialize the underlying BGEM to disk. BGEM has a native envmapMaskTexture
    ''' field, so no sidecar is required — mirrors Save_To_Bgsm for symmetry.</summary>
    Public Sub Save_To_Bgem(filePath As String)
        Dim bgem = TryCast(Underlying_Material, BGEM)
        If bgem Is Nothing Then
            Throw New InvalidOperationException("Save_To_Bgem: Underlying_Material is not a BGEM (" & Underlying_Material?.GetType().Name & ")")
        End If
        Using stream = File.Open(filePath, FileMode.Create)
            bgem.Save(stream)
        End Using
        ClearDirty()
    End Sub

    Private Shared Sub WriteEnvmapMaskSidecar(materialPath As String, envmapMask As String)
        Dim sidecarPath = materialPath & ".json"
        If String.IsNullOrEmpty(envmapMask) Then
            ' Nothing to persist — remove a previous sidecar so we don't leak stale state.
            Try
                If File.Exists(sidecarPath) Then File.Delete(sidecarPath)
            Catch
                ' Best-effort delete; ignore.
            End Try
            Return
        End If
        Dim payload = New Dictionary(Of String, String) From {{"envmapMaskTexture", envmapMask}}
        Dim opts = New JsonSerializerOptions With {.WriteIndented = True}
        File.WriteAllText(sidecarPath, JsonSerializer.Serialize(payload, opts))
    End Sub

    Private Shared Function UIntegerToColor(color As UInteger) As Color
        Dim r = ((color >> 16) And &HFF)
        Dim g = ((color >> 8) And &HFF)
        Dim b = (color And &HFF)
        Return System.Drawing.Color.FromArgb(255, r, g, b)
    End Function
    Private Shared Function UIntegerToNifColor3(color As UInteger) As NiflySharp.Structs.Color3
        Dim r = ((color >> 16) And &HFF)
        Dim g = ((color >> 8) And &HFF)
        Dim b = (color And &HFF)
        Return New Color3(r / 255, g / 255, b / 255)
    End Function
    Private Shared Function UIntegerToNifColor4(color As UInteger) As NiflySharp.Structs.Color4
        Dim r = ((color >> 16) And &HFF)
        Dim g = ((color >> 8) And &HFF)
        Dim b = (color And &HFF)
        Return New Color4(r / 255, g / 255, b / 255, 1)
    End Function
    Private Shared Function ClampByte(value As Single) As Integer
        Return Math.Min(255, Math.Max(0, CInt(value)))
    End Function
    Public Shared Function NifColorColorToUInteger(color As NiflySharp.Structs.Color4) As UInteger
        Return ColorToUInteger(System.Drawing.Color.FromArgb(ClampByte(color.A * 255), ClampByte(color.R * 255), ClampByte(color.G * 255), ClampByte(color.B * 255)))
    End Function
    Public Shared Function NifColorToColor(color As NiflySharp.Structs.Color4) As Color
        Return System.Drawing.Color.FromArgb(ClampByte(color.A * 255), ClampByte(color.R * 255), ClampByte(color.G * 255), ClampByte(color.B * 255))
    End Function
    Public Shared Function NifColorToColor(color As NiflySharp.Structs.Color3) As Color
        Return System.Drawing.Color.FromArgb(255, ClampByte(color.R * 255), ClampByte(color.G * 255), ClampByte(color.B * 255))
    End Function

    Public Shared Function ColorToUInteger(c As Color) As UInteger
        Return CType((CUInt(c.R) << 16) Or (CUInt(c.G) << 8) Or CUInt(c.B), UInteger)
    End Function
    Private Shared Function NormalizeGameRelativePath(rawPath As String, rootPrefix As String) As String
        If String.IsNullOrWhiteSpace(rawPath) Then Return ""

        Dim normalized As String = rawPath.Correct_Path_Separator.Trim().Trim(""""c)
        Dim rootLower As String = rootPrefix.ToLowerInvariant()
        Dim lowered As String = normalized.ToLowerInvariant()

        ' Caso 1: viene absoluto o con prefijo extra, por ejemplo:
        ' C:\...\Data\materials\foo.bgsm
        ' D:\mods\textures\bar.dds
        Dim idx As Integer = lowered.IndexOf("\" & rootLower, StringComparison.OrdinalIgnoreCase)
        If idx >= 0 Then
            normalized = normalized.Substring(idx + 1)
        Else
            ' Caso 2: ya viene relativo pero con slash inicial: \materials\foo.bgsm
            normalized = normalized.TrimStart("\"c)

            ' Caso 3: viene relativo sin prefijo: foo\bar.bgsm
            If Not normalized.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase) Then
                normalized = rootPrefix & normalized
            End If
        End If

        normalized = normalized.TrimStart("\"c)

        If Not normalized.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase) Then
            normalized = rootPrefix & normalized.StripPrefix(rootPrefix)
        End If

        Return normalized.ToLowerInvariant()
    End Function

    Public Shared Function CorrectTexturePath(Texture As String) As String
        Return NormalizeGameRelativePath(Texture, TexturesPrefix)
    End Function

    Public Shared Function CorrectMaterialPath(Texture As String) As String
        Return NormalizeGameRelativePath(Texture, MaterialsPrefix)
    End Function


    Shared Sub New()

    End Sub
    Public Shared Function AreEquivalentGrayscaleScale(currentScale As Single, newScale As Single, texturePath As String) As Boolean
        If currentScale = newScale Then Return True

        Dim width = GetGrayscaleTextureWidth(texturePath)
        If width <= 0 Then Return False

        Return ScaleToGrayscaleSlot(currentScale, width) = ScaleToGrayscaleSlot(newScale, width)
    End Function
    Public Shared Function ScaleToGrayscaleSlot(scale As Single, width As Integer) As Integer
        If width <= 0 Then Return 0
        Return Math.Max(0, Math.Min(width, CInt(width * scale)))
    End Function
    Private Shared Function GetGrayscaleTextureWidth(texturePath As String) As Integer
        If String.IsNullOrWhiteSpace(texturePath) Then Return 0

        Dim normalized = CorrectTexturePath(texturePath)
        If String.IsNullOrWhiteSpace(normalized) Then Return 0

        Return GrayscaleTextureWidthCache.GetOrAdd(normalized,
            Function(key)
                Dim bmp As Bitmap = Nothing
                Try
                    bmp = CreateBitmapFromDDS(FilesDictionary_class.GetBytes(key))
                    Return If(bmp IsNot Nothing, bmp.Width, 0)
                Catch
                    Return 0
                Finally
                    If bmp IsNot Nothing Then bmp.Dispose()
                End Try
            End Function)
    End Function
    ''' <summary>One difference found by <see cref="GetDifferences"/>: the public property
    ''' name and the two values that did not compare equal. Either value may be Nothing.</summary>
    Public Class MaterialDifference
        Public Property PropertyName As String
        Public Property ValueA As Object
        Public Property ValueB As Object
        Public Sub New(name As String, a As Object, b As Object)
            PropertyName = name
            ValueA = a
            ValueB = b
        End Sub
    End Class

    ''' <summary>
    ''' Inspecciona cada propiedad pública de instancia y devuelve la lista de las que
    ''' no comparan iguales, usando el mismo set de reglas por tipo que <see cref="AreEqualWithTrace"/>:
    ''' Single → igualdad (con la excepción <see cref="GrayscaleToPaletteScale"/> que delega
    ''' a <see cref="AreEquivalentGrayscaleScale"/>); String → OrdinalIgnoreCase; Type →
    ''' Equals (Nothing-safe); <see cref="MaterialLib.BaseMaterialFile"/> → siempre igual
    ''' (es el subyacente, no se compara aquí); resto → Object.Equals.
    '''
    ''' Si <paramref name="a"/> o <paramref name="b"/> es Nothing devuelve lista vacía sólo
    ''' cuando ambos lo son; si uno es Nothing y el otro no, devuelve un único entry
    ''' "&lt;instance&gt;" reflejando esa asimetría — equivalente al `a Is b` que
    ''' AreEqualWithTrace devuelve para el caso degenerado.
    '''
    ''' Diferencia con AreEqualWithTrace: éste NO excluye <c>NifShaderType</c>. La exclusión
    ''' que hace AreEqualWithTrace asume que NifShaderType es redundante con los flags
    ''' Facegen/SkinTint/Hair del BGSM (ver helpers DeriveShaderTypeFromBgsm /
    ''' ApplyShaderTypeToBgsm y los call-sites en el setter NifShaderType y Deserialize);
    ''' bajo ese supuesto, ignorarlo evita reportar el mismo dato dos veces. Pero cuando el
    ''' material entra desde un shader embedded en un NIF (Create_From_Shader BGSM) la
    ''' derivación va shader→propiedad sin pasar por los flags del BGSM, y la simetría
    ''' puede romperse. Para fines diagnósticos (NPC_Manager.FaceGenComparator validando un
    ''' bake contra el FaceGen de CK) preferimos ver ese campo si difiere; si en realidad
    ''' es ruido derivado, aparecerá junto con el flag fuente y el lector lo identifica.
    '''
    ''' Diseño: este método existe para que call sites que necesitan reportar QUÉ difiere
    ''' (no sólo si difiere algo) no tengan que duplicar el bucle de reflexión. AreEqualWithTrace
    ''' delega a este método (con la salvedad de NifShaderType arriba); agregar nuevas
    ''' propiedades a FO4UnifiedMaterial_Class las cubre automáticamente en los dos call paths.
    ''' </summary>
    Public Shared Function GetDifferences(a As FO4UnifiedMaterial_Class, b As FO4UnifiedMaterial_Class) As List(Of MaterialDifference)
        Dim diffs As New List(Of MaterialDifference)

        ' Edge case nulls — replica el contrato de AreEqualWithTrace ("a Is b").
        If a Is Nothing AndAlso b Is Nothing Then Return diffs
        If a Is Nothing OrElse b Is Nothing Then
            diffs.Add(New MaterialDifference("<instance>", a, b))
            Return diffs
        End If

        Dim tipo As Type = GetType(FO4UnifiedMaterial_Class)
        Dim props = tipo.GetProperties(BindingFlags.Public Or BindingFlags.Instance) _
                       .Where(Function(p) p.GetIndexParameters().Length = 0)

        For Each prop In props
            Dim valA = prop.GetValue(a, Nothing)
            Dim valB = prop.GetValue(b, Nothing)
            Dim equal As Boolean
            Select Case prop.PropertyType
                Case GetType(Single)
                    If prop.Name.Equals(NameOf(GrayscaleToPaletteScale), StringComparison.Ordinal) Then
                        Dim texturePath = If(String.IsNullOrWhiteSpace(a.GreyscaleTexture), b.GreyscaleTexture, a.GreyscaleTexture)
                        equal = AreEquivalentGrayscaleScale(CType(valA, Single), CType(valB, Single), texturePath)
                    Else
                        equal = CType(valA, Single) = CType(valB, Single)
                    End If
                Case GetType(String)
                    equal = String.Equals(TryCast(valA, String), TryCast(valB, String), StringComparison.OrdinalIgnoreCase)
                Case GetType(Type)
                    If valA Is Nothing OrElse valB Is Nothing Then
                        equal = valA Is valB
                    Else
                        equal = valA.Equals(valB)
                    End If
                Case GetType(MaterialLib.BaseMaterialFile)
                    equal = True
                Case Else
                    equal = Object.Equals(valA, valB)
            End Select

            If Not equal Then
                diffs.Add(New MaterialDifference(prop.Name, valA, valB))
            End If
        Next

        Return diffs
    End Function

    ''' <summary>
    ''' Compara dos instancias de FO4UnifiedMaterial_Class inspeccionando
    ''' cada propiedad y trazando su valor y resultado.
    ''' Nothing vs Nothing = True; Nothing vs objeto real = False.
    '''
    ''' Implementado en términos de <see cref="GetDifferences"/>: cualquier propiedad nueva
    ''' se cubre automáticamente, incluyendo <c>NifShaderType</c> (preservado por el
    ''' setter para los 5 flags BGSM y por el shader NIF para los 16 valores no-flag).
    ''' </summary>
    Public Shared Function AreEqualWithTrace(a As FO4UnifiedMaterial_Class, b As FO4UnifiedMaterial_Class) As Boolean
        ' Edge case nulls — preservar contrato histórico exacto.
        If a Is Nothing OrElse b Is Nothing Then
            Return a Is b
        End If
        Return GetDifferences(a, b).Count = 0
    End Function

    ''' <summary>
    ''' Compara dos instancias de FO4UnifiedMaterial_Class usando el comparador generado.
    ''' </summary>
    Public Function AreEqualTo(b As FO4UnifiedMaterial_Class) As Boolean
        If Me Is Nothing OrElse b Is Nothing Then Return Me Is b
        Return AreEqualWithTrace(Me, b)
    End Function

    ' --- ShouldSerialize / Reset for Color properties (PropertyGrid bold detection) ---
    Private Shared ReadOnly DefaultWhite As Color = Color.FromArgb(255, 255, 255, 255)
    Private Shared ReadOnly DefaultGray As Color = Color.FromArgb(255, 128, 128, 128)

    Public Function ShouldSerializeSpecularColor() As Boolean
        Return SpecularColor <> DefaultWhite
    End Function
    Public Sub ResetSpecularColor()
        SpecularColor = DefaultWhite
    End Sub

    Public Function ShouldSerializeEmittanceColor() As Boolean
        Return EmittanceColor <> DefaultWhite
    End Function
    Public Sub ResetEmittanceColor()
        EmittanceColor = DefaultWhite
    End Sub

    Public Function ShouldSerializeHairTintColor() As Boolean
        Return HairTintColor <> DefaultGray
    End Function
    Public Sub ResetHairTintColor()
        HairTintColor = DefaultGray
    End Sub

    Public Function ShouldSerializeSkinTintColor() As Boolean
        Return SkinTintColor <> DefaultGray
    End Function
    Public Sub ResetSkinTintColor()
        SkinTintColor = DefaultGray
    End Sub

    Public Function ShouldSerializeBaseColor() As Boolean
        Return BaseColor <> DefaultWhite
    End Function
    Public Sub ResetBaseColor()
        BaseColor = DefaultWhite
    End Sub

End Class

Public Class ShaderTypeConverter
    Inherits ComponentModel.EnumConverter

    Public Sub New()
        MyBase.New(GetType(NiflySharp.Enums.BSLightingShaderType))
    End Sub
End Class

Public Class DictionaryFilePickerEditor
    Inherits UITypeEditor
    Public Overrides Function GetEditStyle(context As ITypeDescriptorContext) As UITypeEditorEditStyle
        Return UITypeEditorEditStyle.Modal
    End Function
    Public Overrides Function EditValue(context As ITypeDescriptorContext, provider As IServiceProvider, value As Object) As Object
        Dim dictProvider = FilesDictionary_class.TexturesDictionary_Filter.DictionaryProvider
        If dictProvider Is Nothing Then
            MessageBox.Show("Set DictionaryFilePickerConfig.DictionaryProvider before using.", "Dictionary Selector", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return value
        End If

        Dim filtered = FilesDictionary_class.GetFilteredKeys(FilesDictionary_class.TexturesDictionary_Filter)
        Dim initialKey As String = FO4UnifiedMaterial_Class.CorrectTexturePath(TryCast(value, String))

        Using frm As New DictionaryFilePicker_Form(filtered, FilesDictionary_class.TexturesDictionary_Filter.RootPrefix, FilesDictionary_class.TexturesDictionary_Filter.AllowedExtensions, initialKey)
            If frm.ShowDialog() = DialogResult.OK Then
                Dim sel = frm.DictionaryPicker_Control1.SelectedKey
                If Not String.IsNullOrEmpty(sel) Then Return IO.Path.GetRelativePath(TexturesPrefix, sel)
            End If
        End Using

        Return value
    End Function


End Class

Public Class InhertedBGEMShader
    Inherits BSEffectShaderProperty
End Class
Public Class InhertedBGSMshader
    Inherits BSLightingShaderProperty
End Class


