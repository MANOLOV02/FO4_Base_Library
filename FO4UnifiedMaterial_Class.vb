' Version Uploaded of Fo4Library 3.2.0
Imports System.Collections.Concurrent
Imports System.ComponentModel
Imports System.Drawing.Design
Imports System.IO
Imports System.Reflection
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

    ' NIF ShaderType — not part of BGSM/BGEM file format, stored here as runtime field
    Private _NifShaderType As NiflySharp.Enums.BSLightingShaderType = NiflySharp.Enums.BSLightingShaderType.Default
    ' Env mask path for BGSM — NIF texture set slot 5. Not serialized in the .bgsm binary
    ' (BGSM has no envmapMaskTexture field; path lives only in the NIF texture set).
    ' Evidence: BodySlide MaterialFile.cpp:91-113 (BGSM binary has 9 strings, none for envmask),
    ' PreviewWindow.cpp:434-449 (BodySlide never assigns texFiles[5] from BGSM).
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
            ' Sync BGSM boolean flags with ShaderType
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Dim mat = CType(Underlying_Material, BGSM)
                    mat.Facegen = (value = NiflySharp.Enums.BSLightingShaderType.FaceTint)
                    mat.SkinTint = (value = NiflySharp.Enums.BSLightingShaderType.SkinTint)
                    mat.Hair = (value = NiflySharp.Enums.BSLightingShaderType.HairTint)
                Case GetType(BGEM)
                    ' do nothing
            End Select
        End Set
    End Property

    <Category("Opacity")>
    <DefaultValue(AlphaBlendModeType.Unknown)>
    Public Property AlphaBlendMode As MaterialLib.BaseMaterialFile.AlphaBlendModeType
        Get
            Return Underlying_Material.AlphaBlendMode
        End Get
        Set(value As MaterialLib.BaseMaterialFile.AlphaBlendModeType)
            Underlying_Material.AlphaBlendMode = value
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
            ' Two runtime sources: (a) NIF texture set slot 5 captured in _EnvmapMaskPath,
            ' (b) BGSM.GlowTexture used as on-disk container for roundtrip save/load
            '     (WM SSE embedded convention — sacrifices glow field to persist envmask
            '     across .bgsm Save/Deserialize; works on every BGSM version).
            ' Prefer (a) when populated; fallback to (b) to survive .bgsm serialization.
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    If Not String.IsNullOrEmpty(_EnvmapMaskPath) Then Return _EnvmapMaskPath
                    Return CType(Underlying_Material, BGSM).GlowTexture
                Case GetType(BGEM)
                    Return CType(Underlying_Material, BGEM).EnvmapMaskTexture
            End Select
            Throw New Exception("Unsupported material type")
        End Get
        Set(value As String)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    ' Write to both storages so the roundtrip preserves the path:
                    ' runtime source for render, BGSM.GlowTexture as on-disk container.
                    ' Tradeoff: overwrites glow map field. Accepted convention in WM SSE embedded.
                    _EnvmapMaskPath = value
                    CType(Underlying_Material, BGSM).GlowTexture = value
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
    Public ReadOnly Property Version As UInteger
        Get
            Return Underlying_Material.Version
        End Get
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
    Public Shared Function Determine_Alphablend(Source As NiflySharp.Enums.AlphaFunction, dest As NiflySharp.Enums.AlphaFunction) As AlphaBlendModeType

        If Source = NiflySharp.Enums.AlphaFunction.SRC_ALPHA AndAlso dest = NiflySharp.Enums.AlphaFunction.INV_SRC_ALPHA Then Return AlphaBlendModeType.Standard

        If Source = NiflySharp.Enums.AlphaFunction.SRC_ALPHA AndAlso dest = NiflySharp.Enums.AlphaFunction.ONE Then Return AlphaBlendModeType.Additive

        If Source = NiflySharp.Enums.AlphaFunction.DEST_COLOR AndAlso dest = NiflySharp.Enums.AlphaFunction.ZERO Then Return AlphaBlendModeType.Multiplicative

        If Source = NiflySharp.Enums.AlphaFunction.ONE AndAlso dest = NiflySharp.Enums.AlphaFunction.ZERO Then Return AlphaBlendModeType.None

        Return AlphaBlendModeType.Unknown
    End Function
    Public Shared Function Determine_AlphaFlags(Mode As AlphaBlendModeType) As NiflySharp.Enums.AlphaFunction()
        If Mode = AlphaBlendModeType.Standard Then Return {NiflySharp.Enums.AlphaFunction.SRC_ALPHA, NiflySharp.Enums.AlphaFunction.INV_SRC_ALPHA}
        If Mode = AlphaBlendModeType.Additive Then Return {NiflySharp.Enums.AlphaFunction.SRC_ALPHA, NiflySharp.Enums.AlphaFunction.ONE}
        If Mode = AlphaBlendModeType.Multiplicative Then Return {NiflySharp.Enums.AlphaFunction.DEST_COLOR, NiflySharp.Enums.AlphaFunction.ZERO}
        If Mode = AlphaBlendModeType.Unknown Then Return {NiflySharp.Enums.AlphaFunction.SRC_ALPHA, NiflySharp.Enums.AlphaFunction.INV_SRC_ALPHA}
        If Mode = AlphaBlendModeType.None Then Return {NiflySharp.Enums.AlphaFunction.ONE, NiflySharp.Enums.AlphaFunction.ZERO}
        Return {NiflySharp.Enums.AlphaFunction.SRC_ALPHA, NiflySharp.Enums.AlphaFunction.INV_SRC_ALPHA}
    End Function
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
                                        CUInt(&HFFFFFFUI))),
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
        mat.AlphaTest = False
        mat.AlphaTestRef = 128
        mat.AlphaBlendMode = AlphaBlendModeType.None
        If Not IsNothing(shap.AlphaPropertyRef) AndAlso shap.AlphaPropertyRef.Index <> -1 Then
            Dim alp = CType(Nif.Blocks(shap.AlphaPropertyRef.Index), NiAlphaProperty)
            mat.AlphaTest = alp.Flags.AlphaTest
            mat.AlphaTestRef = alp.Threshold
            If alp.Flags.AlphaBlend Then
                mat.AlphaBlendMode = Determine_Alphablend(alp.Flags.SourceBlendMode, alp.Flags.DestinationBlendMode)
                If alp.Flags.TestFunc = Enums.TestFunction.TEST_NEVER OrElse
                   (alp.Flags.TestFunc = Enums.TestFunction.TEST_GREATER AndAlso mat.AlphaTestRef = 0) Then
                    mat.AlphaBlendMode = AlphaBlendModeType.None
                End If
            Else
                mat.AlphaBlendMode = AlphaBlendModeType.None
            End If
        End If
        Underlying_Material = mat
        _NifShaderType = shad.ShaderType_SK_FO4
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
            .EnvironmentMapping = Not String.IsNullOrEmpty(shad.EnvMapTexture?.Content),
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
        mat.AlphaTest = False
        mat.AlphaTestRef = 128
        mat.AlphaBlendMode = AlphaBlendModeType.None
        If Not IsNothing(shap.AlphaPropertyRef) AndAlso shap.AlphaPropertyRef.Index <> -1 Then
            Dim alp = CType(Nif.Blocks(shap.AlphaPropertyRef.Index), NiAlphaProperty)
            mat.AlphaTest = alp.Flags.AlphaTest
            mat.AlphaTestRef = alp.Threshold
            If alp.Flags.AlphaBlend Then
                mat.AlphaBlendMode = Determine_Alphablend(alp.Flags.SourceBlendMode, alp.Flags.DestinationBlendMode)
                If alp.Flags.TestFunc = Enums.TestFunction.TEST_NEVER Or (alp.Flags.TestFunc = Enums.TestFunction.TEST_GREATER And mat.AlphaTestRef = 0) Then
                    mat.AlphaBlendMode = AlphaBlendModeType.None
                End If
            Else
                mat.AlphaBlendMode = AlphaBlendModeType.None
            End If
        End If
        Underlying_Material = mat

    End Sub
    Public Shared Sub Save_To_Shader(Nif As Nifcontent_Class_Manolo, shap As INiShape, shad As BSEffectShaderProperty, Mat As BGEM)
        If Nif.Valid = False Then Exit Sub
        shad.DoubleSided = Mat.TwoSided
        shad.UVOffset = New TexCoord(Mat.UOffset, Mat.VOffset)
        shad.UVScale = New TexCoord(Mat.UScale, Mat.VScale)
        shad.EnvironmentMapScale = Mat.EnvironmentMappingMaskScale
        shad.EmittanceColor = UIntegerToNifColor3(Mat.EmittanceColor)
        shad.EnvironmentMapScale = Mat.EnvironmentMappingMaskScale
        If IsNothing(shad.SourceTexture) Then
            shad.SourceTexture = New NiString4(Mat.BaseTexture)
        Else
            shad.SourceTexture.Content = Mat.BaseTexture
        End If


        If IsNothing(shad.NormalTexture) Then
            shad.NormalTexture = New NiString4(Mat.NormalTexture)
        Else
            shad.NormalTexture.Content = Mat.NormalTexture
        End If

        If IsNothing(shad.GreyscaleTexture) Then
            shad.GreyscaleTexture = New NiString4(Mat.GrayscaleTexture)
        Else
            shad.GreyscaleTexture.Content = Mat.GrayscaleTexture
        End If

        If IsNothing(shad.EnvMapTexture) Then
            shad.EnvMapTexture = New NiString4(Mat.EnvmapTexture)
        Else
            shad.EnvMapTexture.Content = Mat.EnvmapTexture
        End If

        If IsNothing(shad.EnvMaskTexture) Then
            shad.EnvMaskTexture = New NiString4(Mat.EnvmapMaskTexture)
        Else
            shad.EnvMaskTexture.Content = Mat.EnvmapMaskTexture
        End If

        If IsNothing(shad.LightingTexture) Then
            shad.LightingTexture = New NiString4(Mat.LightingTexture)
        Else
            shad.LightingTexture.Content = Mat.LightingTexture
        End If

        If IsNothing(shad.ReflectanceTexture) Then
            shad.ReflectanceTexture = New NiString4(Mat.SpecularTexture)
        Else
            shad.ReflectanceTexture.Content = Mat.SpecularTexture
        End If

        If IsNothing(shad.EmitGradientTexture) Then
            shad.EmitGradientTexture = New NiString4(Mat.GlowTexture)
        Else
            shad.EmitGradientTexture.Content = Mat.GlowTexture
        End If

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

        If IsNothing(shap.AlphaPropertyRef) OrElse shap.AlphaPropertyRef.Index = -1 Then
            shap.AlphaPropertyRef = New NiBlockRef(Of NiAlphaProperty) With {.Index = Nif.AddBlock(New NiAlphaProperty)}
        End If
        Dim alp = CType(Nif.Blocks(shap.AlphaPropertyRef.Index), NiAlphaProperty)
        alp.Flags.AlphaTest = Mat.AlphaTest
        alp.Threshold = Mat.AlphaTestRef
        alp.Flags.AlphaBlend = Not Mat.AlphaBlendMode = AlphaBlendModeType.None
        Dim func = Determine_AlphaFlags(Mat.AlphaBlendMode)
        alp.Flags.SourceBlendMode = func(0)
        alp.Flags.DestinationBlendMode = func(1)
    End Sub
    Public Shared Sub Save_To_Shader(Nif As Nifcontent_Class_Manolo, shap As INiShape, shad As BSLightingShaderProperty, Mat As BGSM, Optional shaderType As NiflySharp.Enums.BSLightingShaderType = NiflySharp.Enums.BSLightingShaderType.Default, Optional envmapMaskPath As String = "")
        If Nif.Valid = False Then Exit Sub
        shad.DoubleSided = Mat.TwoSided
        shad.UVOffset = New TexCoord(Mat.UOffset, Mat.VOffset)
        shad.UVScale = New TexCoord(Mat.UScale, Mat.VScale)
        shad.Emissive = Mat.EmitEnabled
        shad.EmissiveColor = UIntegerToNifColor4(Mat.EmittanceColor)
        shad.EmissiveMultiple = Mat.EmittanceMult
        shad.Alpha = Mat.Alpha
        shad.HasEnvironmentMapping = Mat.EnvironmentMapping
        shad.EnvironmentMapScale = Mat.EnvironmentMappingMaskScale
        If Nif.Header.Version.IsSSE Then
            shad.Glossiness = CSng(Math.Pow(2.0, CDbl(Mat.Smoothness) * 10.0 + 1.0))
        Else
            shad.Smoothness = Mat.Smoothness
        End If
        shad.SubsurfaceRolloff = Mat.SubsurfaceLightingRolloff
        shad.ModelSpace = Mat.ModelSpaceNormals
        shad.ShaderType_SK_FO4 = shaderType
        ' BGSM uses HairTintColor for both hair and skin tint — write to the correct NIF field
        If Mat.SkinTint Then
            shad.SkinTintColor = UIntegerToNifColor3(Mat.HairTintColor)
        Else
            shad.HairTintColor = UIntegerToNifColor3(Mat.HairTintColor)
        End If
        shad.HasBacklight = Mat.BackLighting
        shad.BacklightPower = Mat.BackLightPower
        shad.HasSpecular = Mat.SpecularEnabled
        shad.SpecularColor = UIntegerToNifColor3(Mat.SpecularColor)
        shad.SpecularStrength = Mat.SpecularMult
        shad.HasGlowmap = Mat.Glowmap
        shad.HasTreeAnim = Mat.Tree
        shad.HasSoftlight = Mat.SubsurfaceLighting
        shad.HasRimlight = Mat.RimLighting
        shad.RimlightPower = Mat.RimPower
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

        If IsNothing(shap.AlphaPropertyRef) OrElse shap.AlphaPropertyRef.Index = -1 Then
            shap.AlphaPropertyRef = New NiBlockRef(Of NiAlphaProperty) With {.Index = Nif.AddBlock(New NiAlphaProperty)}
        End If

        Dim alp = CType(Nif.Blocks(shap.AlphaPropertyRef.Index), NiAlphaProperty)
        alp.Flags.AlphaTest = Mat.AlphaTest
        alp.Threshold = Mat.AlphaTestRef
        alp.Flags.AlphaBlend = Not Mat.AlphaBlendMode = AlphaBlendModeType.None
        Dim func = Determine_AlphaFlags(Mat.AlphaBlendMode)
        alp.Flags.SourceBlendMode = func(0)
        alp.Flags.DestinationBlendMode = func(1)
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
            ' Slot 6: FO4 has no sampler here. Only SSE writes LightingTexture to slot 6.
            If isSSE Then
                texset.Textures(textset_LightingTexture).Content = mat.LightingTexture
            End If
        End If
    End Sub
    Public Sub Deserialize(Memory As Byte(), type As Type)
        If Memory.Length = 0 Then Exit Sub
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
        ' Sync envmask runtime source from the on-disk container (see EnvmapMaskTexture getter comment).
        If type Is GetType(BGSM) Then
            Dim bgsm = CType(Underlying_Material, BGSM)
            _EnvmapMaskPath = If(bgsm.GlowTexture, "")

            ' Derive _NifShaderType from BGSM semantic flags. Symmetric to the setter at line
            ' 118-130 (which goes shader→flags). The flags Facegen/SkinTint/Hair are written by
            ' BGSM.Deserialize from disk bytes (Material-Editor-master/MaterialLib/BGSM.cs:448-453);
            ' without this derivation _NifShaderType stays at the class default (Default), even
            ' when the BGSM declares the material as a Facegen/SkinTint/Hair material. That
            ' default-pinning is the bug behind the Ghoul/Child face shapes losing their FaceTint
            ' classification when ApplyTextureSetOverrides replaces the NIF-derived material with
            ' a freshly-deserialized BGSM (NPC_Manager MainForm.vb:7843).
            If bgsm.Facegen Then
                _NifShaderType = NiflySharp.Enums.BSLightingShaderType.FaceTint
            ElseIf bgsm.SkinTint Then
                _NifShaderType = NiflySharp.Enums.BSLightingShaderType.SkinTint
            ElseIf bgsm.Hair Then
                _NifShaderType = NiflySharp.Enums.BSLightingShaderType.HairTint
            Else
                _NifShaderType = NiflySharp.Enums.BSLightingShaderType.Default
            End If
        End If
    End Sub

    Public Sub Deserialize(Diccionario As String, type As Type)
        Deserialize(FilesDictionary_class.GetBytes(Diccionario), type)
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
    ''' <summary>
    ''' Compara dos instancias de FO4UnifiedMaterial_Class inspeccionando
    ''' cada propiedad y trazando su valor y resultado.
    ''' Nothing vs Nothing = True; Nothing vs objeto real = False
    ''' </summary>
    Public Shared Function AreEqualWithTrace(a As FO4UnifiedMaterial_Class, b As FO4UnifiedMaterial_Class) As Boolean
        ' Caso de nulos
        If a Is Nothing OrElse b Is Nothing Then
            Return a Is b
        End If

        Dim tipo As Type = GetType(FO4UnifiedMaterial_Class)
        Dim props = tipo.GetProperties(BindingFlags.Public Or BindingFlags.Instance) _
                       .Where(Function(p) p.GetIndexParameters().Length = 0 AndAlso
                                          Not p.Name.Equals("NifShaderType", StringComparison.Ordinal))

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
                If prop.Name.Equals(NameOf(GrayscaleToPaletteScale), StringComparison.Ordinal) Then
                    Dim texturePath = If(String.IsNullOrWhiteSpace(a.GreyscaleTexture), b.GreyscaleTexture, a.GreyscaleTexture)
                    Dim width = GetGrayscaleTextureWidth(texturePath)
                End If
                Return False
            End If
        Next

        Return True
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


