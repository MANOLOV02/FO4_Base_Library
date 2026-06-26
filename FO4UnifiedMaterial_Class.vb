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

<AttributeUsage(AttributeTargets.Property)>
Public Class BGSMOnlyAttribute
    Inherits Attribute
End Class

<AttributeUsage(AttributeTargets.Property)>
Public Class BGEMOnlyAttribute
    Inherits Attribute
End Class

' Which material type(s) a property applies to.
Public Enum FieldApplies
    Both = 0
    BGSM = 1
    BGEM = 2
End Enum

' Data-driven grid gate for one wrapper property. Replaces the old BGSMOnly/BGEMOnly-only filter:
'  - AppliesTo: hide the property when it does not apply to the current material type.
'  - Per-type persistence window (Bgsm/Bgem Min/MaxExclusive) from the spec's "Gate binario" column.
'    Persistible iff Version >= Min AND Version < MaxExclusive for the CURRENT material type. The two
'    pairs are separate because shared wrapper props (SpecularTexture/LightingTexture/GlowTexture/
'    EmittanceColor/Glowmap/AdaptativeEmissive_*) gate at DIFFERENT versions for BGSM vs BGEM.
'    When the current Version is outside the window the field is DISABLED (shown read-only) rather
'    than edited into a layout that won't serialize it. MaxExclusive = UInteger.MaxValue = no upper bound.
'  - EnabledWhen: optional predicate over the instance; when it returns False the field is DISABLED
'    (e.g. EmittanceColor when EmitEnabled=False; Glass* when GlassEnabled=False).
'  - VisibleWhen: optional predicate over the instance; when it returns False the field is HIDDEN
'    (e.g. SkinTintColor only when SkinTint=True).
'  - SkyrimOnly: only visible when Config_App.Current.Game = Skyrim (DetailMask/TintMask aliases).
Public Structure FieldGate
    Public AppliesTo As FieldApplies
    Public BgsmMinVersion As UInteger
    Public BgsmMaxExclusive As UInteger
    Public BgemMinVersion As UInteger
    Public BgemMaxExclusive As UInteger
    Public EnabledWhen As Func(Of FO4UnifiedMaterial_Class, Boolean)
    Public VisibleWhen As Func(Of FO4UnifiedMaterial_Class, Boolean)
    Public SkyrimOnly As Boolean
    ' Opción (b) Skyrim: el binario v2 no persiste el campo, pero el contenedor Skyrim SÍ
    ' (sidecar .bgsm.json + Save_To_Shader escribe los texset slots SSE 5/6). Cuando es True
    ' y el material es BGSM y Config_App.Current.Game = Skyrim, la ventana de versión NO
    ' deshabilita el campo. Solo aplica a BGSM (el sidecar es .bgsm.json; BGEM no lo necesita).
    Public BgsmSkyrimSidecarBypass As Boolean
End Structure

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
        Dim isBgsm = currentType Is GetType(BGSM)
        Dim isBgem = currentType Is GetType(BGEM)
        Dim version = instance.Underlying_Material.Version

        For Each prop As PropertyDescriptor In props
            Dim gate As FieldGate = Nothing
            Dim hasGate = FO4UnifiedMaterial_Class.FieldGates.TryGetValue(prop.Name, gate)

            ' Applies-to: table if present, else fall back to the BGSMOnly/BGEMOnly attributes.
            Dim appliesTo As FieldApplies
            If hasGate Then
                appliesTo = gate.AppliesTo
            ElseIf prop.Attributes(GetType(BGSMOnlyAttribute)) IsNot Nothing Then
                appliesTo = FieldApplies.BGSM
            ElseIf prop.Attributes(GetType(BGEMOnlyAttribute)) IsNot Nothing Then
                appliesTo = FieldApplies.BGEM
            Else
                appliesTo = FieldApplies.Both
            End If

            If appliesTo = FieldApplies.BGSM AndAlso Not isBgsm Then Continue For
            If appliesTo = FieldApplies.BGEM AndAlso Not isBgem Then Continue For

            Dim disabled As Boolean = False
            If hasGate Then
                If gate.SkyrimOnly AndAlso Config_App.Current.Game <> Config_App.Game_Enum.Skyrim Then Continue For
                If gate.VisibleWhen IsNot Nothing AndAlso Not gate.VisibleWhen(instance) Then Continue For
                ' Persistence window for the current material type → disable (read-only) when outside it.
                ' Excepción opción (b): en Skyrim el sidecar .bgsm.json + los slots SSE del texset
                ' persisten ciertos campos BGSM que la ventana v2 no cubre (Flow/Lighting).
                Dim sidecarBypass = gate.BgsmSkyrimSidecarBypass AndAlso isBgsm AndAlso
                                    Config_App.Current.Game = Config_App.Game_Enum.Skyrim
                Dim minVer = If(isBgem, gate.BgemMinVersion, gate.BgsmMinVersion)
                Dim maxExcl = If(isBgem, gate.BgemMaxExclusive, gate.BgsmMaxExclusive)
                If (version < minVer OrElse version >= maxExcl) AndAlso Not sidecarBypass Then disabled = True
                ' Capability gate → disable when the enabling flag is off.
                If gate.EnabledWhen IsNot Nothing AndAlso Not gate.EnabledWhen(instance) Then disabled = True
            End If

            If disabled Then
                filtered.Add(New ReadOnlyPropertyDescriptor(prop))
            Else
                filtered.Add(prop)
            End If
        Next

        Return New PropertyDescriptorCollection(filtered.ToArray())
    End Function
End Class

' Wraps a PropertyDescriptor to force it read-only in the grid (the "disabled" presentation for a
' field that the current Version cannot persist, or whose enabling flag is off). The value is still
' shown; it just can't be edited.
Public Class ReadOnlyPropertyDescriptor
    Inherits PropertyDescriptor

    Private ReadOnly inner As PropertyDescriptor

    Public Sub New(inner As PropertyDescriptor)
        MyBase.New(inner)
        Me.inner = inner
    End Sub

    Public Overrides ReadOnly Property IsReadOnly As Boolean
        Get
            Return True
        End Get
    End Property

    Public Overrides ReadOnly Property ComponentType As Type
        Get
            Return inner.ComponentType
        End Get
    End Property

    Public Overrides ReadOnly Property PropertyType As Type
        Get
            Return inner.PropertyType
        End Get
    End Property

    Public Overrides Function CanResetValue(component As Object) As Boolean
        Return inner.CanResetValue(component)
    End Function

    Public Overrides Function GetValue(component As Object) As Object
        Return inner.GetValue(component)
    End Function

    Public Overrides Sub ResetValue(component As Object)
        inner.ResetValue(component)
    End Sub

    Public Overrides Sub SetValue(component As Object, value As Object)
        ' Read-only: ignore writes from the grid.
    End Sub

    Public Overrides Function ShouldSerializeValue(component As Object) As Boolean
        Return inner.ShouldSerializeValue(component)
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
    Public Property Underlying_Material As MaterialLib.BaseMaterialFile = NewBgemNormalized()

    ' Requisito L: al CONSTRUIR el wrapper, normalizar a "" los 3 strings de BGEM que históricamente
    ' podían quedar sin inicializar (SpecularTexture/LightingTexture/GlowTexture). No se toca MaterialLib;
    ' la normalización vive aquí. (El SetDefaults actual ya los pone en "", esto es la garantía explícita.)
    Private Shared Function NewBgemNormalized() As BGEM
        Dim bgem As New BGEM()
        If bgem.SpecularTexture Is Nothing Then bgem.SpecularTexture = ""
        If bgem.LightingTexture Is Nothing Then bgem.LightingTexture = ""
        If bgem.GlowTexture Is Nothing Then bgem.GlowTexture = ""
        Return bgem
    End Function

    ' Dirty tracking via snapshot comparison. ClearDirty() captures a Clone of the current
    ' state; IsDirty() reports whether the wrapper has diverged from that snapshot via
    ' GetDifferences (which honors AreEquivalentGrayscaleScale tolerance for slot-equivalent
    ' palette scales). Net-zero round-trips (user edits then reverts) report clean because
    ' the comparison is against the snapshot, not a one-way "was ever touched" flag.
    ' Snapshot captured by the load/save paths (Deserialize / Save_To_* / Create_From_Shader);
    ' the editor doesn't need to mark dirty on user edits — the next IsDirty() check detects
    ' the diff automatically.
    Private _cleanSnapshot As FO4UnifiedMaterial_Class = Nothing

    ' Propiedades que NO viven en el archivo de material (.bgsm/.bgem) sino en el shader del NIF:
    ' Save_To_Bgsm/Bgem nunca las escribe (NifShaderType es campo transitorio, ver más abajo). Un
    ' cambio en ellas NO es una "modificación del material a guardar a archivo" — se persiste con el
    ' NIF en el guardado normal del proyecto. El gating de los botones "Save/Save As material" y de
    ' Revisa_Material (WM Editor_Form) las ignora vía IsMaterialFileDirty/AreEqualToMaterialFile, así
    ' que cambiar el shader type a mano NO marca el material como sucio ni exige grabar un .bgsm.
    ' GetDifferences / AreEqualTo (FaceGenComparator, diagnóstico de bake) SÍ las comparan — esa
    ' comparación quiere ver el tipo de shader. Hoy solo el shader type; ampliar con evidencia.
    Public Shared ReadOnly NifShaderOnlyPropertyNames As String() = {NameOf(NifShaderType)}

    Public Sub ClearDirty()
        _cleanSnapshot = Clone()
    End Sub

    Public Function IsDirty() As Boolean
        If _cleanSnapshot Is Nothing Then Return False
        Return GetDifferences(Me, _cleanSnapshot).Count > 0
    End Function

    ''' <summary>Dirty restringido al estado del ARCHIVO de material (lo que Save_To_Bgsm/Bgem
    ''' persiste): ignora los campos solo-NIF de <see cref="NifShaderOnlyPropertyNames"/>. Es el que
    ''' debe gatear los botones "Save/Save As material" — cambiar el shader type (que va al shader del
    ''' NIF, no al .bgsm) no debe encenderlos.</summary>
    Public Function IsMaterialFileDirty() As Boolean
        If _cleanSnapshot Is Nothing Then Return False
        Return GetDifferences(Me, _cleanSnapshot, NifShaderOnlyPropertyNames).Count > 0
    End Function

    ''' <summary>Diagnóstico: la lista COMPLETA de propiedades que difieren del snapshot de carga
    ''' (las que hacen True a <see cref="IsDirty"/>), sin filtrar campos solo-NIF. Sirve para
    ''' instrumentar "por qué se marca modificado" sin adivinar: el call site loguea los nombres.
    ''' Vacía si no hay snapshot (material nunca cargado/limpiado).</summary>
    Public Function GetDirtyDifferences() As List(Of MaterialDifference)
        If _cleanSnapshot Is Nothing Then Return New List(Of MaterialDifference)
        Return GetDifferences(Me, _cleanSnapshot)
    End Function

    ''' <summary>Deep-copy the wrapper by a FIELD-WISE copy of the concrete BGSM/BGEM (NOT a
    ''' binary round-trip). The binary serializer only writes the fields the current Version
    ''' persists, so a round-trip would silently drop every field gated above v2 (Translucency,
    ''' Terrain, Glass, LumEmittance, …) and corrupt the dirty-tracking snapshot with false
    ''' clean/dirty. Reflection copies every public settable property of the material, so the
    ''' snapshot is a true copy regardless of Version. Transient wrapper fields not stored in the
    ''' material (NIF ShaderType, sidecar envmap path, the three alpha-blend fields, skin-tint
    ''' alpha) are copied explicitly.</summary>
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
                ' Version is not a [DataMember] (no public setter exclusion needed) but it IS a public
                ' settable property; the reflection loop copies it like any other field.
                For Each p In t.GetProperties(BindingFlags.Public Or BindingFlags.Instance)
                    If Not p.CanRead OrElse Not p.CanWrite Then Continue For
                    If p.GetIndexParameters().Length <> 0 Then Continue For
                    p.SetValue(newMat, p.GetValue(Underlying_Material, Nothing), Nothing)
                Next
                copy.Underlying_Material = newMat
            End If
        End If
        copy._NifShaderType = _NifShaderType
        copy._EnvmapMaskPath = _EnvmapMaskPath
        copy._alphaBlendEnabled = _alphaBlendEnabled
        copy._blendFunctionSource = _blendFunctionSource
        copy._blendFunctionDest = _blendFunctionDest
        copy._skinTintAlpha = _skinTintAlpha
        copy._NifGlossiness = _NifGlossiness
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

    ' Requisito C: MaskWrites pasa a ser visible en el grid pero deshabilitado en v2 (gate v>=6,
    ' no persistible en FO4 v2; lo aplica FieldGates). Antes era Browsable(False).
    <Category("Rendering")>
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
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return _NifShaderType
                Case GetType(BGEM)
                    Return NiflySharp.Enums.BSLightingShaderType.Default
            End Select
            Throw New Exception("Unsupported material type")
        End Get
        Set(value As NiflySharp.Enums.BSLightingShaderType)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    ' Desacoplado: setear el tipo NO toca flags. Tipo y flags son ejes
                    ' ortogonales; los flags se editan por separado en el grid.
                    _NifShaderType = value
                Case GetType(BGEM)
            End Select
        End Set
    End Property

    ' Skin tint strength → shader SkinTintAlpha (FO4 shaderType=5 SkinTint). NOT a BGSM field:
    ' the BGSM carries no skin-tint-alpha; it is an NPC-level value (QNAM TextureLighting's A /
    ' the SkinTone tint-layer opacity) that the engine/CK bakes inline. Transient runtime field,
    ' like NifShaderType: read from the shader in Create_From_Shader, written back in
    ' Save_To_Shader (gated on SkinTint), never serialized to the .bgsm (disk round-trip unaffected).
    ' The app sets it from the NPC's TextureLightingFloats.A before baking — same split as the skin
    ' tone COLOR (app resolves it into HairTintColor, the library writes it). Defaults to 1.0 (full)
    ' for materials with no NPC context (render / WM editing).
    Private _skinTintAlpha As Single = 1.0F
    <Category("Coloring")>
    <BGSMOnly()>
    Public Property SkinTintAlpha As Single
        Get
            Return _skinTintAlpha
        End Get
        Set(value As Single)
            _skinTintAlpha = value
        End Set
    End Property

    ' Glossiness CRUDA del shader SSE (shad.Glossiness, exponente ~2..2048). NO es campo BGSM:
    ' el BGSM guarda la Smoothness normalizada (0..1). Se transporta para que el shader SSE use
    ' el exponente crudo directo (pow(NdotH, glossiness)) en vez de reconstruirlo con exp2 desde
    ' Smoothness. Friend para que GetDifferences/AreEqualWithTrace (reflexion solo Public) no lo
    ' dupliquen con Smoothness. Solo se usa en el path SSE; en FO4 el shader deriva de Smoothness.
    Private _NifGlossiness As Single = 1.0F
    Friend ReadOnly Property NifGlossiness As Single
        Get
            Return _NifGlossiness
        End Get
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

    ''' <summary>Tipo efectivo de BSLightingShader que el engine resuelve por prioridad del factory
    ''' (Fallout4.exe FUN_142163BE0): Eye&gt;Envmap&gt;Glowmap&gt;Face&gt;SkinTint&gt;HairTint&gt;Default.
    ''' MultiLayer se omite (no hay PS forward en Shaders011.fxp block 6). Sirve para rutear el GLSL
    ''' engine-faithful de forma MUTUAMENTE EXCLUYENTE (el engine corre UN shader por tipo).</summary>
    Public Enum EffectiveLightingType
        [Default] = 0
        Envmap = 1
        Glowmap = 2
        Face = 3
        SkinTint = 4
        HairTint = 5
        Eye = 6
    End Enum

    ''' <summary>True si el slot diffuse es una textura de COLOR (sRGB), no datos. Excluye
    ''' greyscale-to-palette (usa baseMap.g como indice de LUT) y BGEM. Gatea el decode
    ''' sRGB-&gt;lineal in-shader (C1; el engine samplea t0 como SRV sRGB).</summary>
    Public Function IsColorDiffuse() As Boolean
        Return Not GrayscaleToPaletteColor AndAlso Not IsBGEM()
    End Function

    ''' <summary>Tipo efectivo por la prioridad del factory del engine (FUN_142163BE0).
    ''' BGEM devuelve Default (su render va por el path bIsEffectShader, no por este tipo).</summary>
    Public Function ResolveEffectiveType() As EffectiveLightingType
        If IsBGEM() Then Return EffectiveLightingType.[Default]
        If EyeEnvironmentMapping Then Return EffectiveLightingType.Eye
        If EnvironmentMapping Then Return EffectiveLightingType.Envmap
        If Glowmap Then Return EffectiveLightingType.Glowmap
        If Facegen Then Return EffectiveLightingType.Face
        If SkinTint Then Return EffectiveLightingType.SkinTint
        If Hair Then Return EffectiveLightingType.HairTint
        Return EffectiveLightingType.[Default]
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

    ' Hidden from the grid: SEMANTIC ALIAS of DisplacementTexture (slot 3, SSE FaceTint detail mask).
    ' Same backing slot -> showing both duplicated it. Kept as code accessor; edit via DisplacementTexture.
    <Browsable(False)>
    <BGSMOnly()>
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

    ' Hidden from the grid: SEMANTIC ALIAS of LightingTexture (slot 6). Same backing slot -> showing both
    ' duplicated it. The old tint-overlay use was removed; kept as code accessor; edit via LightingTexture.
    <Browsable(False)>
    <BGSMOnly()>
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

    ' P1: the version drives the binary layout, so editing it freely in the grid would silently
    ' switch which fields serialize. It stays readable (and the public setter is preserved for the
    ' frozen API and the roundtrip-preserve path), but the grid shows it read-only.
    <Category("(Type)")>
    <ComponentModel.ReadOnly(True)>
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
    Public Property EyeEnvironmentMapping As Boolean
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return CType(Underlying_Material, BGSM).EnvironmentMappingEye
                Case GetType(BGEM)
                    Return False
            End Select
            Throw New Exception
        End Get
        Set(value As Boolean)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).EnvironmentMappingEye = value
                Case GetType(BGEM)
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
                    Return MaterialRgbToColor(CType(Underlying_Material, BGSM).SpecularColor)
                Case GetType(BGEM)
                    Return System.Drawing.Color.FromArgb(255, 255, 255, 255)
            End Select
            Throw New Exception
        End Get
        Set(value As Color)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).SpecularColor = ColorToMaterialRgb(value)
                Case GetType(BGEM)
            End Select
        End Set
    End Property

    <Category("Emissive")>
    Public Property EmittanceColor As Color
        Get
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return MaterialRgbToColor(CType(Underlying_Material, BGSM).EmittanceColor)
                Case GetType(BGEM)
                    Return MaterialRgbToColor(CType(Underlying_Material, BGEM).EmittanceColor)
            End Select
            Throw New Exception
        End Get
        Set(value As Color)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    ' P3: no side-effect. The binary only persists EmittanceColor when EmitEnabled is set
                    ' (BGSM.cs:576-579), and the grid disables this field when EmitEnabled=False, so the
                    ' setter must not silently force EmitEnabled on a color change.
                    CType(Underlying_Material, BGSM).EmittanceColor = ColorToMaterialRgb(value)
                Case GetType(BGEM)
                    CType(Underlying_Material, BGEM).EmittanceColor = ColorToMaterialRgb(value)
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
                    Return MaterialRgbToColor(CType(Underlying_Material, BGSM).HairTintColor)
                Case GetType(BGEM)
                    Return Color.Black
                Case Else
                    Throw New Exception
            End Select
        End Get
        Set(value As Color)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).HairTintColor = ColorToMaterialRgb(value)
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
                    Return MaterialRgbToColor(CType(Underlying_Material, BGSM).HairTintColor)
                Case GetType(BGEM)
                    Return Color.White
                Case Else
                    Throw New Exception
            End Select
        End Get
        Set(value As Color)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    CType(Underlying_Material, BGSM).HairTintColor = ColorToMaterialRgb(value)
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
            ' BGEM.BaseColor is RGB-only; BGEM.Alpha carries opacity separately.
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    Return System.Drawing.Color.FromArgb(255, 255, 255, 255)
                Case GetType(BGEM)
                    Dim bgem = CType(Underlying_Material, BGEM)
                    Return MaterialRgbToColor(bgem.BaseColor, ClampByte(bgem.Alpha * 255))
                Case Else
                    Throw New Exception
            End Select
        End Get
        Set(value As Color)
            Select Case Underlying_Material.GetType
                Case GetType(BGSM)
                    ' No action
                Case GetType(BGEM)
                    Dim bgem = CType(Underlying_Material, BGEM)
                    bgem.BaseColor = ColorToMaterialRgb(value)
                    bgem.Alpha = value.A / 255.0F
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

    ' ====================================================================================
    ' Campos antes NO expuestos (requisito F). El gating por (tipo, versión) lo aplica
    ' FieldGates / FilterProperties; estas propiedades solo exponen el campo del material.
    ' Los que ya tienen mapeo en Create/Save (flags) no necesitan tocar el bridge aquí.
    ' ====================================================================================

    ' --- BaseMaterialFile ---
    <Category("Rendering")>
    <DefaultValue(False)>
    Public Property ScreenSpaceReflections As Boolean
        Get
            Return Underlying_Material.ScreenSpaceReflections
        End Get
        Set(value As Boolean)
            Underlying_Material.ScreenSpaceReflections = value
        End Set
    End Property

    ' --- BGSM-only ---
    <Category("Rendering")>
    <BGSMOnly()>
    <DefaultValue(False)>
    Public Property EnableEditorAlphaRef As Boolean
        Get
            Return If(TryCast(Underlying_Material, BGSM)?.EnableEditorAlphaRef, False)
        End Get
        Set(value As Boolean)
            Dim bgsm = TryCast(Underlying_Material, BGSM)
            If bgsm IsNot Nothing Then bgsm.EnableEditorAlphaRef = value
        End Set
    End Property

    <Category("Lighting")>
    <BGSMOnly()>
    <DefaultValue(False)>
    Public Property AnisoLighting As Boolean
        Get
            Return If(TryCast(Underlying_Material, BGSM)?.AnisoLighting, False)
        End Get
        Set(value As Boolean)
            Dim bgsm = TryCast(Underlying_Material, BGSM)
            If bgsm IsNot Nothing Then bgsm.AnisoLighting = value
        End Set
    End Property

    <Category("Emissive")>
    <BGSMOnly()>
    <DefaultValue(False)>
    Public Property ExternalEmittance As Boolean
        Get
            Return If(TryCast(Underlying_Material, BGSM)?.ExternalEmittance, False)
        End Get
        Set(value As Boolean)
            Dim bgsm = TryCast(Underlying_Material, BGSM)
            If bgsm IsNot Nothing Then bgsm.ExternalEmittance = value
        End Set
    End Property

    <Category("Rendering")>
    <BGSMOnly()>
    <DefaultValue(False)>
    Public Property ReceiveShadows As Boolean
        Get
            Return If(TryCast(Underlying_Material, BGSM)?.ReceiveShadows, False)
        End Get
        Set(value As Boolean)
            Dim bgsm = TryCast(Underlying_Material, BGSM)
            If bgsm IsNot Nothing Then bgsm.ReceiveShadows = value
        End Set
    End Property

    <Category("Rendering")>
    <BGSMOnly()>
    <DefaultValue(False)>
    Public Property HideSecret As Boolean
        Get
            Return If(TryCast(Underlying_Material, BGSM)?.HideSecret, False)
        End Get
        Set(value As Boolean)
            Dim bgsm = TryCast(Underlying_Material, BGSM)
            If bgsm IsNot Nothing Then bgsm.HideSecret = value
        End Set
    End Property

    <Category("Rendering")>
    <BGSMOnly()>
    <DefaultValue(False)>
    Public Property CastShadows As Boolean
        Get
            Return If(TryCast(Underlying_Material, BGSM)?.CastShadows, False)
        End Get
        Set(value As Boolean)
            Dim bgsm = TryCast(Underlying_Material, BGSM)
            If bgsm IsNot Nothing Then bgsm.CastShadows = value
        End Set
    End Property

    <Category("Rendering")>
    <BGSMOnly()>
    <DefaultValue(False)>
    Public Property DissolveFade As Boolean
        Get
            Return If(TryCast(Underlying_Material, BGSM)?.DissolveFade, False)
        End Get
        Set(value As Boolean)
            Dim bgsm = TryCast(Underlying_Material, BGSM)
            If bgsm IsNot Nothing Then bgsm.DissolveFade = value
        End Set
    End Property

    <Category("Rendering")>
    <BGSMOnly()>
    <DefaultValue(False)>
    Public Property AssumeShadowmask As Boolean
        Get
            Return If(TryCast(Underlying_Material, BGSM)?.AssumeShadowmask, False)
        End Get
        Set(value As Boolean)
            Dim bgsm = TryCast(Underlying_Material, BGSM)
            If bgsm IsNot Nothing Then bgsm.AssumeShadowmask = value
        End Set
    End Property

    <Category("Specular")>
    <BGSMOnly()>
    <DefaultValue(False)>
    Public Property EnvironmentMappingWindow As Boolean
        Get
            Return If(TryCast(Underlying_Material, BGSM)?.EnvironmentMappingWindow, False)
        End Get
        Set(value As Boolean)
            Dim bgsm = TryCast(Underlying_Material, BGSM)
            If bgsm IsNot Nothing Then bgsm.EnvironmentMappingWindow = value
        End Set
    End Property

    <Category("Rendering")>
    <BGSMOnly()>
    <DefaultValue(False)>
    Public Property Tessellate As Boolean
        Get
            Return If(TryCast(Underlying_Material, BGSM)?.Tessellate, False)
        End Get
        Set(value As Boolean)
            Dim bgsm = TryCast(Underlying_Material, BGSM)
            If bgsm IsNot Nothing Then bgsm.Tessellate = value
        End Set
    End Property

    <Category("Specular")>
    <BGSMOnly()>
    <DefaultValue(False)>
    Public Property SkewSpecularAlpha As Boolean
        Get
            Return If(TryCast(Underlying_Material, BGSM)?.SkewSpecularAlpha, False)
        End Get
        Set(value As Boolean)
            Dim bgsm = TryCast(Underlying_Material, BGSM)
            If bgsm IsNot Nothing Then bgsm.SkewSpecularAlpha = value
        End Set
    End Property

    <Category("Coloring")>
    <BGSMOnly()>
    <DefaultValue(False)>
    Public Property Terrain As Boolean
        ' BGSM.Terrain flag (BSLightingShaderType MultitextureLandscape). Gate v>=3 → no persistible en FO4 v2.
        Get
            Return If(TryCast(Underlying_Material, BGSM)?.Terrain, False)
        End Get
        Set(value As Boolean)
            Dim bgsm = TryCast(Underlying_Material, BGSM)
            If bgsm IsNot Nothing Then bgsm.Terrain = value
        End Set
    End Property

    <Category("Coloring")>
    <BGSMOnly()>
    <DefaultValue(0F)>
    Public Property TerrainThresholdFalloff As Single
        Get
            Return If(TryCast(Underlying_Material, BGSM)?.TerrainThresholdFalloff, 0.0F)
        End Get
        Set(value As Single)
            Dim bgsm = TryCast(Underlying_Material, BGSM)
            If bgsm IsNot Nothing Then bgsm.TerrainThresholdFalloff = value
        End Set
    End Property

    <Category("Coloring")>
    <BGSMOnly()>
    <DefaultValue(0F)>
    Public Property TerrainTilingDistance As Single
        Get
            Return If(TryCast(Underlying_Material, BGSM)?.TerrainTilingDistance, 0.0F)
        End Get
        Set(value As Single)
            Dim bgsm = TryCast(Underlying_Material, BGSM)
            If bgsm IsNot Nothing Then bgsm.TerrainTilingDistance = value
        End Set
    End Property

    <Category("Coloring")>
    <BGSMOnly()>
    <DefaultValue(0F)>
    Public Property TerrainRotationAngle As Single
        Get
            Return If(TryCast(Underlying_Material, BGSM)?.TerrainRotationAngle, 0.0F)
        End Get
        Set(value As Single)
            Dim bgsm = TryCast(Underlying_Material, BGSM)
            If bgsm IsNot Nothing Then bgsm.TerrainRotationAngle = value
        End Set
    End Property

    <Category("Coloring")>
    <BGSMOnly()>
    Public Property TerrainUnkInt1 As UInteger
        Get
            Return If(TryCast(Underlying_Material, BGSM)?.UnkInt1, 0UI)
        End Get
        Set(value As UInteger)
            Dim bgsm = TryCast(Underlying_Material, BGSM)
            If bgsm IsNot Nothing Then bgsm.UnkInt1 = value
        End Set
    End Property

    <Category("UVs")>
    <BGSMOnly()>
    <DefaultValue(-0.5F)>
    Public Property DisplacementTextureBias As Single
        Get
            Return If(TryCast(Underlying_Material, BGSM)?.DisplacementTextureBias, -0.5F)
        End Get
        Set(value As Single)
            Dim bgsm = TryCast(Underlying_Material, BGSM)
            If bgsm IsNot Nothing Then bgsm.DisplacementTextureBias = value
        End Set
    End Property

    <Category("UVs")>
    <BGSMOnly()>
    <DefaultValue(10.0F)>
    Public Property DisplacementTextureScale As Single
        Get
            Return If(TryCast(Underlying_Material, BGSM)?.DisplacementTextureScale, 10.0F)
        End Get
        Set(value As Single)
            Dim bgsm = TryCast(Underlying_Material, BGSM)
            If bgsm IsNot Nothing Then bgsm.DisplacementTextureScale = value
        End Set
    End Property

    <Category("UVs")>
    <BGSMOnly()>
    <DefaultValue(1.0F)>
    Public Property TessellationPnScale As Single
        Get
            Return If(TryCast(Underlying_Material, BGSM)?.TessellationPnScale, 1.0F)
        End Get
        Set(value As Single)
            Dim bgsm = TryCast(Underlying_Material, BGSM)
            If bgsm IsNot Nothing Then bgsm.TessellationPnScale = value
        End Set
    End Property

    <Category("UVs")>
    <BGSMOnly()>
    <DefaultValue(1.0F)>
    Public Property TessellationBaseFactor As Single
        Get
            Return If(TryCast(Underlying_Material, BGSM)?.TessellationBaseFactor, 1.0F)
        End Get
        Set(value As Single)
            Dim bgsm = TryCast(Underlying_Material, BGSM)
            If bgsm IsNot Nothing Then bgsm.TessellationBaseFactor = value
        End Set
    End Property

    <Category("UVs")>
    <BGSMOnly()>
    <DefaultValue(0F)>
    Public Property TessellationFadeDistance As Single
        Get
            Return If(TryCast(Underlying_Material, BGSM)?.TessellationFadeDistance, 0.0F)
        End Get
        Set(value As Single)
            Dim bgsm = TryCast(Underlying_Material, BGSM)
            If bgsm IsNot Nothing Then bgsm.TessellationFadeDistance = value
        End Set
    End Property

    <Category("Lighting")>
    <BGSMOnly()>
    <DefaultValue(False)>
    Public Property Translucency As Boolean
        Get
            Return If(TryCast(Underlying_Material, BGSM)?.Translucency, False)
        End Get
        Set(value As Boolean)
            Dim bgsm = TryCast(Underlying_Material, BGSM)
            If bgsm IsNot Nothing Then bgsm.Translucency = value
        End Set
    End Property

    <Category("Lighting")>
    <BGSMOnly()>
    <DefaultValue(False)>
    Public Property TranslucencyThickObject As Boolean
        Get
            Return If(TryCast(Underlying_Material, BGSM)?.TranslucencyThickObject, False)
        End Get
        Set(value As Boolean)
            Dim bgsm = TryCast(Underlying_Material, BGSM)
            If bgsm IsNot Nothing Then bgsm.TranslucencyThickObject = value
        End Set
    End Property

    <Category("Lighting")>
    <BGSMOnly()>
    <DefaultValue(False)>
    Public Property TranslucencyMixAlbedoWithSubsurfaceColor As Boolean
        Get
            Return If(TryCast(Underlying_Material, BGSM)?.TranslucencyMixAlbedoWithSubsurfaceColor, False)
        End Get
        Set(value As Boolean)
            Dim bgsm = TryCast(Underlying_Material, BGSM)
            If bgsm IsNot Nothing Then bgsm.TranslucencyMixAlbedoWithSubsurfaceColor = value
        End Set
    End Property

    <Category("Lighting")>
    <BGSMOnly()>
    Public Property TranslucencySubsurfaceColor As Color
        Get
            Dim bgsm = TryCast(Underlying_Material, BGSM)
            Return If(bgsm IsNot Nothing, MaterialRgbToColor(bgsm.TranslucencySubsurfaceColor), Color.Black)
        End Get
        Set(value As Color)
            Dim bgsm = TryCast(Underlying_Material, BGSM)
            If bgsm IsNot Nothing Then bgsm.TranslucencySubsurfaceColor = ColorToMaterialRgb(value)
        End Set
    End Property

    <Category("Lighting")>
    <BGSMOnly()>
    <DefaultValue(0F)>
    Public Property TranslucencyTransmissiveScale As Single
        Get
            Return If(TryCast(Underlying_Material, BGSM)?.TranslucencyTransmissiveScale, 0.0F)
        End Get
        Set(value As Single)
            Dim bgsm = TryCast(Underlying_Material, BGSM)
            If bgsm IsNot Nothing Then bgsm.TranslucencyTransmissiveScale = value
        End Set
    End Property

    <Category("Lighting")>
    <BGSMOnly()>
    <DefaultValue(0F)>
    Public Property TranslucencyTurbulence As Single
        Get
            Return If(TryCast(Underlying_Material, BGSM)?.TranslucencyTurbulence, 0.0F)
        End Get
        Set(value As Single)
            Dim bgsm = TryCast(Underlying_Material, BGSM)
            If bgsm IsNot Nothing Then bgsm.TranslucencyTurbulence = value
        End Set
    End Property

    <Category("Emissive")>
    <BGSMOnly()>
    <DefaultValue(0F)>
    Public Property LumEmittance As Single
        Get
            Return If(TryCast(Underlying_Material, BGSM)?.LumEmittance, 0.0F)
        End Get
        Set(value As Single)
            Dim bgsm = TryCast(Underlying_Material, BGSM)
            If bgsm IsNot Nothing Then bgsm.LumEmittance = value
        End Set
    End Property

    <Category("Emissive")>
    <BGSMOnly()>
    <DefaultValue(False)>
    Public Property UseAdaptativeEmissive As Boolean
        Get
            Return If(TryCast(Underlying_Material, BGSM)?.UseAdaptativeEmissive, False)
        End Get
        Set(value As Boolean)
            Dim bgsm = TryCast(Underlying_Material, BGSM)
            If bgsm IsNot Nothing Then bgsm.UseAdaptativeEmissive = value
        End Set
    End Property

    ' Wetness raw (6 campos) editables. El sentinel -1 = heredar de RootMaterialPath/template
    ' (la cadena la resuelve ResolveEffectiveWetness; los Resolved* Browsable(False) los consume el render).
    <Category("Rendering")>
    <BGSMOnly()>
    <DefaultValue(-1.0F)>
    Public Property WetnessControlSpecScale As Single
        Get
            Return If(TryCast(Underlying_Material, BGSM)?.WetnessControlSpecScale, -1.0F)
        End Get
        Set(value As Single)
            Dim bgsm = TryCast(Underlying_Material, BGSM)
            If bgsm IsNot Nothing Then bgsm.WetnessControlSpecScale = value
        End Set
    End Property

    <Category("Rendering")>
    <BGSMOnly()>
    <DefaultValue(-1.0F)>
    Public Property WetnessControlSpecPowerScale As Single
        Get
            Return If(TryCast(Underlying_Material, BGSM)?.WetnessControlSpecPowerScale, -1.0F)
        End Get
        Set(value As Single)
            Dim bgsm = TryCast(Underlying_Material, BGSM)
            If bgsm IsNot Nothing Then bgsm.WetnessControlSpecPowerScale = value
        End Set
    End Property

    <Category("Rendering")>
    <BGSMOnly()>
    <DefaultValue(-1.0F)>
    Public Property WetnessControlSpecMinvar As Single
        Get
            Return If(TryCast(Underlying_Material, BGSM)?.WetnessControlSpecMinvar, -1.0F)
        End Get
        Set(value As Single)
            Dim bgsm = TryCast(Underlying_Material, BGSM)
            If bgsm IsNot Nothing Then bgsm.WetnessControlSpecMinvar = value
        End Set
    End Property

    <Category("Rendering")>
    <BGSMOnly()>
    <DefaultValue(-1.0F)>
    Public Property WetnessControlEnvMapScale As Single
        Get
            Return If(TryCast(Underlying_Material, BGSM)?.WetnessControlEnvMapScale, -1.0F)
        End Get
        Set(value As Single)
            Dim bgsm = TryCast(Underlying_Material, BGSM)
            If bgsm IsNot Nothing Then bgsm.WetnessControlEnvMapScale = value
        End Set
    End Property

    <Category("Rendering")>
    <BGSMOnly()>
    <DefaultValue(-1.0F)>
    Public Property WetnessControlFresnelPower As Single
        Get
            Return If(TryCast(Underlying_Material, BGSM)?.WetnessControlFresnelPower, -1.0F)
        End Get
        Set(value As Single)
            Dim bgsm = TryCast(Underlying_Material, BGSM)
            If bgsm IsNot Nothing Then bgsm.WetnessControlFresnelPower = value
        End Set
    End Property

    <Category("Rendering")>
    <BGSMOnly()>
    <DefaultValue(-1.0F)>
    Public Property WetnessControlMetalness As Single
        Get
            Return If(TryCast(Underlying_Material, BGSM)?.WetnessControlMetalness, -1.0F)
        End Get
        Set(value As Single)
            Dim bgsm = TryCast(Underlying_Material, BGSM)
            If bgsm IsNot Nothing Then bgsm.WetnessControlMetalness = value
        End Set
    End Property

    ' --- BGEM-only ---
    <Category("Effect (BGEM)")>
    <BGEMOnly>
    Public Property EnvmapMinLOD As Byte
        Get
            Return If(TryCast(Underlying_Material, BGEM)?.EnvmapMinLOD, CByte(0))
        End Get
        Set(value As Byte)
            Dim bgem = TryCast(Underlying_Material, BGEM)
            If bgem IsNot Nothing Then bgem.EnvmapMinLOD = value
        End Set
    End Property

    <Category("Effect (BGEM)")>
    <BGEMOnly>
    <DefaultValue(False)>
    Public Property GlassEnabled As Boolean
        Get
            Return If(TryCast(Underlying_Material, BGEM)?.GlassEnabled, False)
        End Get
        Set(value As Boolean)
            Dim bgem = TryCast(Underlying_Material, BGEM)
            If bgem IsNot Nothing Then bgem.GlassEnabled = value
        End Set
    End Property

    <Category("Effect (BGEM)")>
    <BGEMOnly>
    Public Property GlassFresnelColor As Color
        Get
            Dim bgem = TryCast(Underlying_Material, BGEM)
            Return If(bgem IsNot Nothing, MaterialRgbToColor(bgem.GlassFresnelColor), DefaultWhite)
        End Get
        Set(value As Color)
            Dim bgem = TryCast(Underlying_Material, BGEM)
            If bgem IsNot Nothing Then bgem.GlassFresnelColor = ColorToMaterialRgb(value)
        End Set
    End Property

    <Category("Effect (BGEM)")>
    <BGEMOnly>
    <DefaultValue(0.05F)>
    Public Property GlassRefractionScaleBase As Single
        Get
            Return If(TryCast(Underlying_Material, BGEM)?.GlassRefractionScaleBase, 0.05F)
        End Get
        Set(value As Single)
            Dim bgem = TryCast(Underlying_Material, BGEM)
            If bgem IsNot Nothing Then bgem.GlassRefractionScaleBase = value
        End Set
    End Property

    <Category("Effect (BGEM)")>
    <BGEMOnly>
    <DefaultValue(0.4F)>
    Public Property GlassBlurScaleBase As Single
        Get
            Return If(TryCast(Underlying_Material, BGEM)?.GlassBlurScaleBase, 0.4F)
        End Get
        Set(value As Single)
            Dim bgem = TryCast(Underlying_Material, BGEM)
            If bgem IsNot Nothing Then bgem.GlassBlurScaleBase = value
        End Set
    End Property

    <Category("Effect (BGEM)")>
    <BGEMOnly>
    <DefaultValue(1.0F)>
    Public Property GlassBlurScaleFactor As Single
        Get
            Return If(TryCast(Underlying_Material, BGEM)?.GlassBlurScaleFactor, 1.0F)
        End Get
        Set(value As Single)
            Dim bgem = TryCast(Underlying_Material, BGEM)
            If bgem IsNot Nothing Then bgem.GlassBlurScaleFactor = value
        End Set
    End Property

    <Category("Effect (BGEM)")>
    <BGEMOnly>
    <DefaultValue("")>
    <Editor(GetType(DictionaryFilePickerEditor), GetType(UITypeEditor))>
    Public Property GlassRoughnessScratch As String
        Get
            Return If(TryCast(Underlying_Material, BGEM)?.GlassRoughnessScratch, "")
        End Get
        Set(value As String)
            Dim bgem = TryCast(Underlying_Material, BGEM)
            If bgem IsNot Nothing Then bgem.GlassRoughnessScratch = value
        End Set
    End Property

    <Category("Effect (BGEM)")>
    <BGEMOnly>
    <DefaultValue("")>
    <Editor(GetType(DictionaryFilePickerEditor), GetType(UITypeEditor))>
    Public Property GlassDirtOverlay As String
        Get
            Return If(TryCast(Underlying_Material, BGEM)?.GlassDirtOverlay, "")
        End Get
        Set(value As String)
            Dim bgem = TryCast(Underlying_Material, BGEM)
            If bgem IsNot Nothing Then bgem.GlassDirtOverlay = value
        End Set
    End Property

    ' Helpers DeriveShaderTypeFromBgsm / ApplyShaderTypeToBgsm ELIMINADOS (2026-06): tipo y flags
    ' desacoplados. El tipo se lee/escribe fiel del shader (Create/Save) y los flags se editan
    ' independientes en el grid; no se deriva el tipo desde los flags ni viceversa.

    ' P1 — Versiones. El probe (25.082 materiales reales) solo observó v1/v2; vanilla = v2.
    ' Crear material (FO4 y contenedor Skyrim opción (b)) ⇒ siempre v2. El roundtrip preserva
    ' la versión leída (v1 parsea idéntico a v2 en MaterialLib). FO76 (v>2) está fuera de alcance,
    ' por eso se eliminaron MaterialVersionSSE=20 / MaterialVersionFO76=21 y el escalado especulativo
    ' ResolveBgemVersionForShader (v10-22), que no tenían respaldo en datos reales.
    Private Const MaterialVersionFO4 As UInteger = 2UI

    Public Shared Function DefaultMaterialVersionForNif(Nif As Nifcontent_Class_Manolo) As UInteger
        Return MaterialVersionFO4
    End Function

    ' ====================================================================================
    ' FieldGates — tabla estática data-driven (requisito B/C). Una entrada por propiedad del
    ' grid que necesita gating MÁS ALLÁ del simple applies-to por tipo. Las propiedades que
    ' SOLO necesitan applies-to siguen gateadas por los atributos BGSMOnly/BGEMOnly (fallback
    ' en FilterProperties). Cada ventana de versión sale de la columna "Gate binario" del spec.
    ' Reglas:
    '  - Persistible iff Version >= Min AND Version < MaxExcl para el tipo actual. Fuera de la
    '    ventana ⇒ DISABLED (read-only). En FO4 / contenedor Skyrim (b) la Version es 2.
    '  - EnabledWhen ⇒ DISABLED cuando el flag habilitador está off.
    '  - VisibleWhen ⇒ HIDDEN cuando el predicado es falso.
    '  - SkyrimOnly ⇒ HIDDEN salvo Config_App.Current.Game = Skyrim.
    ' ====================================================================================
    Private Const NoMaxVersion As UInteger = UInteger.MaxValue

    Private Shared Function Gate(applies As FieldApplies,
                                 Optional bgsmMin As UInteger = 0UI, Optional bgsmMaxExcl As UInteger = NoMaxVersion,
                                 Optional bgemMin As UInteger = 0UI, Optional bgemMaxExcl As UInteger = NoMaxVersion,
                                 Optional enabledWhen As Func(Of FO4UnifiedMaterial_Class, Boolean) = Nothing,
                                 Optional visibleWhen As Func(Of FO4UnifiedMaterial_Class, Boolean) = Nothing,
                                 Optional skyrimOnly As Boolean = False,
                                 Optional bgsmSkyrimSidecarBypass As Boolean = False) As FieldGate
        Return New FieldGate With {
            .AppliesTo = applies,
            .BgsmMinVersion = bgsmMin, .BgsmMaxExclusive = bgsmMaxExcl,
            .BgemMinVersion = bgemMin, .BgemMaxExclusive = bgemMaxExcl,
            .EnabledWhen = enabledWhen, .VisibleWhen = visibleWhen, .SkyrimOnly = skyrimOnly,
            .BgsmSkyrimSidecarBypass = bgsmSkyrimSidecarBypass}
    End Function

    Public Shared ReadOnly FieldGates As Dictionary(Of String, FieldGate) = BuildFieldGates()

    Private Shared Function BuildFieldGates() As Dictionary(Of String, FieldGate)
        Dim g As New Dictionary(Of String, FieldGate)(StringComparer.Ordinal)

        ' --- BaseMaterialFile-level version gates ---
        g(NameOf(DepthBias)) = Gate(FieldApplies.Both, bgsmMin:=10UI, bgemMin:=10UI)         ' v>=10
        ' MaskWrites: v>=6. Hoy <Browsable(False)>; se expone visible-disabled en v2 (requisito C).
        g(NameOf(MaskWrites)) = Gate(FieldApplies.Both, bgsmMin:=6UI, bgemMin:=6UI)

        ' --- Texturas BGSM con gate de versión (v>2 ⇒ no persistible en FO4 v2) ---
        ' SpecularTexture/LightingTexture: BGSM gate v>2 (Min=3); BGEM gate v>=11. GlowTexture:
        ' BGSM incondicional; BGEM v>=11. Por eso ventanas separadas por tipo.
        g(NameOf(SpecularTexture)) = Gate(FieldApplies.Both, bgsmMin:=3UI, bgemMin:=11UI)
        ' LightingTexture/FlowTexture BGSM: v>2 en binario, PERO en Skyrim (opción b) son los
        ' texset slots SSE 6/5 — Save_To_Shader los persiste al NIF y Save_To_Bgsm al sidecar,
        ' así que el bypass los mantiene editables en modo Skyrim a v2.
        g(NameOf(LightingTexture)) = Gate(FieldApplies.Both, bgsmMin:=3UI, bgemMin:=11UI, bgsmSkyrimSidecarBypass:=True)
        g(NameOf(GlowTexture)) = Gate(FieldApplies.Both, bgsmMin:=0UI, bgemMin:=11UI)
        g(NameOf(FlowTexture)) = Gate(FieldApplies.BGSM, bgsmMin:=3UI, bgsmSkyrimSidecarBypass:=True) ' BGSM v>2
        g(NameOf(DistanceFieldAlphaTexture)) = Gate(FieldApplies.BGSM, bgsmMin:=17UI)          ' BGSM v>=17

        ' --- BGSM flags/scalars con gate de versión ---
        g(NameOf(PBR)) = Gate(FieldApplies.BGSM, bgsmMin:=3UI)                                 ' v>2
        g(NameOf(CustomPorosity)) = Gate(FieldApplies.BGSM, bgsmMin:=9UI)                      ' v>=9
        g(NameOf(PorosityValue)) = Gate(FieldApplies.BGSM, bgsmMin:=9UI)                       ' v>=9
        g(NameOf(LumEmittance)) = Gate(FieldApplies.BGSM, bgsmMin:=12UI)                       ' v>=12
        g(NameOf(UseAdaptativeEmissive)) = Gate(FieldApplies.BGSM, bgsmMin:=13UI)              ' v>=13
        g(NameOf(Translucency)) = Gate(FieldApplies.BGSM, bgsmMin:=8UI)                        ' v>=8
        g(NameOf(TranslucencyThickObject)) = Gate(FieldApplies.BGSM, bgsmMin:=8UI)
        g(NameOf(TranslucencyMixAlbedoWithSubsurfaceColor)) = Gate(FieldApplies.BGSM, bgsmMin:=8UI)
        g(NameOf(TranslucencySubsurfaceColor)) = Gate(FieldApplies.BGSM, bgsmMin:=8UI)
        g(NameOf(TranslucencyTransmissiveScale)) = Gate(FieldApplies.BGSM, bgsmMin:=8UI)
        g(NameOf(TranslucencyTurbulence)) = Gate(FieldApplies.BGSM, bgsmMin:=8UI)
        ' RimLighting/RimPower/BackLightPower/SubsurfaceLighting*/BackLighting: v<8 (presentes en FO4 v2).
        g(NameOf(RimLighting)) = Gate(FieldApplies.BGSM, bgsmMaxExcl:=8UI)
        g(NameOf(RimPower)) = Gate(FieldApplies.BGSM, bgsmMaxExcl:=8UI)
        g(NameOf(BackLightPower)) = Gate(FieldApplies.BGSM, bgsmMaxExcl:=8UI)
        g(NameOf(SubsurfaceLighting)) = Gate(FieldApplies.BGSM, bgsmMaxExcl:=8UI)
        g(NameOf(SubsurfaceLightingRolloff)) = Gate(FieldApplies.BGSM, bgsmMaxExcl:=8UI)
        g(NameOf(BackLighting)) = Gate(FieldApplies.BGSM, bgsmMaxExcl:=8UI)
        ' EnvironmentMappingWindow/EyeEnvironmentMapping: v<7 (presentes en FO4 v2).
        g(NameOf(EnvironmentMappingWindow)) = Gate(FieldApplies.BGSM, bgsmMaxExcl:=7UI)
        g(NameOf(EyeEnvironmentMapping)) = Gate(FieldApplies.BGSM, bgsmMaxExcl:=7UI)
        ' Displacement/Tessellation*: v<3 (presentes en FO4 v2).
        g(NameOf(DisplacementTextureBias)) = Gate(FieldApplies.BGSM, bgsmMaxExcl:=3UI)
        g(NameOf(DisplacementTextureScale)) = Gate(FieldApplies.BGSM, bgsmMaxExcl:=3UI)
        g(NameOf(TessellationPnScale)) = Gate(FieldApplies.BGSM, bgsmMaxExcl:=3UI)
        g(NameOf(TessellationBaseFactor)) = Gate(FieldApplies.BGSM, bgsmMaxExcl:=3UI)
        g(NameOf(TessellationFadeDistance)) = Gate(FieldApplies.BGSM, bgsmMaxExcl:=3UI)
        ' SkewSpecularAlpha: v>=1 ⇒ persistible en FO4 v2 (sin gate de disable).
        ' Terrain + campos: v>=3 (no persistible en FO4 v2); además los 4 escalares solo con Terrain=True.
        g(NameOf(Terrain)) = Gate(FieldApplies.BGSM, bgsmMin:=3UI)
        g(NameOf(TerrainThresholdFalloff)) = Gate(FieldApplies.BGSM, bgsmMin:=3UI, enabledWhen:=Function(m) m.Terrain)
        g(NameOf(TerrainTilingDistance)) = Gate(FieldApplies.BGSM, bgsmMin:=3UI, enabledWhen:=Function(m) m.Terrain)
        g(NameOf(TerrainRotationAngle)) = Gate(FieldApplies.BGSM, bgsmMin:=3UI, enabledWhen:=Function(m) m.Terrain)
        ' UnkInt1: solo v==3 AND Terrain.
        g(NameOf(TerrainUnkInt1)) = Gate(FieldApplies.BGSM, bgsmMin:=3UI, bgsmMaxExcl:=4UI, enabledWhen:=Function(m) m.Terrain)

        ' --- BGSM/BGEM compartidas con gate distinto por tipo ---
        ' EmittanceColor: BGSM condicional-por-valor EmitEnabled (persistible en v2 si EmitEnabled);
        ' BGEM gate v>=11 (no persistible en FO4 v2). En BGEM no hay EmitEnabled (P3 no aplica).
        g(NameOf(EmittanceColor)) = Gate(FieldApplies.Both, bgemMin:=11UI,
                                         enabledWhen:=Function(m) (Not m.IsBGSM()) OrElse m.EmitEnabled)
        ' Glowmap: BGSM incondicional; BGEM v>=16.
        g(NameOf(Glowmap)) = Gate(FieldApplies.Both, bgemMin:=16UI)
        ' AdaptativeEmissive_*: BGSM v>=13; BGEM v>=15.
        g(NameOf(AdaptativeEmissive_ExposureOffset)) = Gate(FieldApplies.Both, bgsmMin:=13UI, bgemMin:=15UI)
        g(NameOf(AdaptativeEmissive_FinalExposureMin)) = Gate(FieldApplies.Both, bgsmMin:=13UI, bgemMin:=15UI)
        g(NameOf(AdaptativeEmissive_FinalExposureMax)) = Gate(FieldApplies.Both, bgsmMin:=13UI, bgemMin:=15UI)

        ' --- BGEM-only con gate de versión ---
        g(NameOf(EffectPbrSpecular)) = Gate(FieldApplies.BGEM, bgemMin:=20UI)                  ' v>=20
        g(NameOf(GlassEnabled)) = Gate(FieldApplies.BGEM, bgemMin:=21UI)                       ' v>=21
        g(NameOf(GlassRoughnessScratch)) = Gate(FieldApplies.BGEM, bgemMin:=21UI)
        g(NameOf(GlassDirtOverlay)) = Gate(FieldApplies.BGEM, bgemMin:=21UI)
        ' Glass color/escala: v>=21 AND GlassEnabled (BlurScaleFactor v>=22).
        g(NameOf(GlassFresnelColor)) = Gate(FieldApplies.BGEM, bgemMin:=21UI, enabledWhen:=Function(m) m.GlassEnabled)
        g(NameOf(GlassBlurScaleBase)) = Gate(FieldApplies.BGEM, bgemMin:=21UI, enabledWhen:=Function(m) m.GlassEnabled)
        g(NameOf(GlassRefractionScaleBase)) = Gate(FieldApplies.BGEM, bgemMin:=21UI, enabledWhen:=Function(m) m.GlassEnabled)
        g(NameOf(GlassBlurScaleFactor)) = Gate(FieldApplies.BGEM, bgemMin:=22UI, enabledWhen:=Function(m) m.GlassEnabled)

        ' --- Aliases / visibilidad ---
        ' GrayscaleToPaletteScale: solo BGSM en el grid (en BGEM el storage es BaseColorScale, expuesto aparte).
        g(NameOf(GrayscaleToPaletteScale)) = Gate(FieldApplies.BGSM)
        ' BaseColorScale: solo BGEM (ya BGEMOnly, redundante pero explícito).
        g(NameOf(BaseColorScale)) = Gate(FieldApplies.BGEM)
        ' SkinTintColor: visible solo si SkinTint=True (alias del mismo storage que HairTintColor).
        g(NameOf(SkinTintColor)) = Gate(FieldApplies.BGSM, visibleWhen:=Function(m) m.SkinTint)
        ' DetailMaskTexture / TintMaskTexture: aliases de slot SSE; visibles solo en Skyrim.
        g(NameOf(DetailMaskTexture)) = Gate(FieldApplies.BGSM, skyrimOnly:=True)
        g(NameOf(TintMaskTexture)) = Gate(FieldApplies.BGSM, skyrimOnly:=True)

        Return g
    End Function

    Public Structure AlphaBlendTuple
        Public Enabled As Boolean
        Public Src As NiflySharp.Enums.AlphaFunction
        Public Dst As NiflySharp.Enums.AlphaFunction
    End Structure

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

    ' P4 — Flags shader SIEMPRE game-aware. HasFlagSF1/SF2 y SetFlagSF1/SF2 (ShaderHelper.cs:198-298)
    ' YA hacen el branch por shad.Type, pero castean el valor que se les pasa al enum del juego en uso;
    ' pasar un Fallout4ShaderPropertyFlags* incondicional escribe/lee el BIT correcto solo si SK comparte
    ' esa posición de bit. Estos helpers devuelven, para el shader dado, el valor del flag correcto por
    ' juego (o 0 = no-op cuando el flag NO existe en SK; verificado contra los enums generados
    ' Bitflags.SkyrimShaderPropertyFlags{1,2}.g.cs y Fallout4ShaderPropertyFlags{1,2}.g.cs).
    Private Shared Function IsSkShader(shad As INiShader) As Boolean
        Return shad IsNot Nothing AndAlso shad.Type = NiflySharp.Helpers.ShaderHelper.ShaderGameType.SK
    End Function

    Private Shared Function CastShadowsFlagValue(shad As INiShader) As UInteger
        ' SK Cast_Shadows (1<<9) == FO4 Cast_Shadows (1<<9).
        Return If(IsSkShader(shad),
                  CUInt(NiflySharp.Enums.SkyrimShaderPropertyFlags1.Cast_Shadows),
                  CUInt(NiflySharp.Enums.Fallout4ShaderPropertyFlags1.Cast_Shadows))
    End Function

    Private Shared Function HideSecretFlagValue(shad As INiShader) As UInteger
        ' SK Localmap_Hide_Secret (1<<20) == FO4 Localmap_Hide_Secret (1<<20).
        Return If(IsSkShader(shad),
                  CUInt(NiflySharp.Enums.SkyrimShaderPropertyFlags1.Localmap_Hide_Secret),
                  CUInt(NiflySharp.Enums.Fallout4ShaderPropertyFlags1.Localmap_Hide_Secret))
    End Function

    Private Shared Function DecalFlagValue(shad As INiShader) As UInteger
        ' SK Decal (1<<26) == FO4 Decal (1<<26).
        Return If(IsSkShader(shad),
                  CUInt(NiflySharp.Enums.SkyrimShaderPropertyFlags1.Decal),
                  CUInt(NiflySharp.Enums.Fallout4ShaderPropertyFlags1.Decal))
    End Function

    Private Shared Function ZBufferTestFlagValue(shad As INiShader) As UInteger
        ' SK ZBuffer_Test (1<<31) == FO4 ZBuffer_Test (1<<31).
        Return If(IsSkShader(shad),
                  CUInt(NiflySharp.Enums.SkyrimShaderPropertyFlags1.ZBuffer_Test),
                  CUInt(NiflySharp.Enums.Fallout4ShaderPropertyFlags1.ZBuffer_Test))
    End Function

    Private Shared Function RefractionFlagValue(shad As INiShader) As UInteger
        ' SK Refraction (1<<15) == FO4 Refraction (1<<15).
        Return If(IsSkShader(shad),
                  CUInt(NiflySharp.Enums.SkyrimShaderPropertyFlags1.Refraction),
                  CUInt(NiflySharp.Enums.Fallout4ShaderPropertyFlags1.Refraction))
    End Function

    Private Shared Function SoftEffectFlagValue(shad As INiShader) As UInteger
        ' SK Soft_Effect (1<<30) == FO4 Soft_Effect (1<<30).
        Return If(IsSkShader(shad),
                  CUInt(NiflySharp.Enums.SkyrimShaderPropertyFlags1.Soft_Effect),
                  CUInt(NiflySharp.Enums.Fallout4ShaderPropertyFlags1.Soft_Effect))
    End Function

    Private Shared Function EyeEnvironmentMappingFlagValue(shad As INiShader) As UInteger
        ' SK Eye_Environment_Mapping (1<<17) == FO4 Eye_Environment_Mapping (1<<17).
        Return If(IsSkShader(shad),
                  CUInt(NiflySharp.Enums.SkyrimShaderPropertyFlags1.Eye_Environment_Mapping),
                  CUInt(NiflySharp.Enums.Fallout4ShaderPropertyFlags1.Eye_Environment_Mapping))
    End Function

    Private Shared Function HairFlagValue(shad As INiShader) As UInteger
        ' FO4-only: Fallout4ShaderPropertyFlags1.Hair (1<<18). SK bit 1<<18 = Hair_Soft_Lighting
        ' (distinto significado), por eso en SK es no-op (0).
        Return If(IsSkShader(shad), 0UI, CUInt(NiflySharp.Enums.Fallout4ShaderPropertyFlags1.Hair))
    End Function

    Private Shared Function ZBufferWriteFlagValue(shad As INiShader) As UInteger
        ' SK ZBuffer_Write (1<<0) == FO4 ZBuffer_Write (1<<0).
        Return If(IsSkShader(shad),
                  CUInt(NiflySharp.Enums.SkyrimShaderPropertyFlags2.ZBuffer_Write),
                  CUInt(NiflySharp.Enums.Fallout4ShaderPropertyFlags2.ZBuffer_Write))
    End Function

    Private Shared Function NoFadeFlagValue(shad As INiShader) As UInteger
        ' SK No_Fade (1<<3) == FO4 No_Fade (1<<3).
        Return If(IsSkShader(shad),
                  CUInt(NiflySharp.Enums.SkyrimShaderPropertyFlags2.No_Fade),
                  CUInt(NiflySharp.Enums.Fallout4ShaderPropertyFlags2.No_Fade))
    End Function

    Private Shared Function AnisotropicLightingFlagValue(shad As INiShader) As UInteger
        ' SK Anisotropic_Lighting (1<<21) == FO4 Anisotropic_Lighting (1<<21).
        Return If(IsSkShader(shad),
                  CUInt(NiflySharp.Enums.SkyrimShaderPropertyFlags2.Anisotropic_Lighting),
                  CUInt(NiflySharp.Enums.Fallout4ShaderPropertyFlags2.Anisotropic_Lighting))
    End Function

    Private Shared Function WeaponBloodFlagValue(shad As INiShader) As UInteger
        ' SK Weapon_Blood (1<<17) == FO4 Weapon_Blood (1<<17).
        Return If(IsSkShader(shad),
                  CUInt(NiflySharp.Enums.SkyrimShaderPropertyFlags2.Weapon_Blood),
                  CUInt(NiflySharp.Enums.Fallout4ShaderPropertyFlags2.Weapon_Blood))
    End Function

    Private Sub RecomputeAlphaBlendModeFromFields()
        Underlying_Material.AlphaBlendMode = ClassifyTuple(_alphaBlendEnabled, _blendFunctionSource, _blendFunctionDest)
    End Sub

    Private Sub WriteBlendFlagsToAlphaProperty(alp As NiAlphaProperty)
        alp.Flags.AlphaBlend = _alphaBlendEnabled
        alp.Flags.SourceBlendMode = _blendFunctionSource
        alp.Flags.DestinationBlendMode = _blendFunctionDest
    End Sub

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
                .EmittanceColor = NifColor4ToMaterialRgb(shad.EmissiveColor),
                .EmittanceMult = shad.EmissiveMultiple,
                .Alpha = shad.Alpha,
                .EnvironmentMapping = shad.HasEnvironmentMapping,
                .EnvironmentMappingMaskScale = If(shad.IsTypeEyeEnvironmentMap, shad.EyeCubemapScale, shad.EnvironmentMapScale),
                .ModelSpaceNormals = shad.ModelSpace,
                .Facegen = If(Nif.Header.Version.IsSSE, shad.IsTypeFaceTint, (shad.ShaderFlags_F4SPF1 And NiflySharp.Enums.Fallout4ShaderPropertyFlags1.Face) <> 0),
                .Hair = If(Nif.Header.Version.IsSSE, shad.IsTypeHairTint, (shad.ShaderFlags_F4SPF1 And NiflySharp.Enums.Fallout4ShaderPropertyFlags1.Hair) <> 0),
                .SkinTint = If(Nif.Header.Version.IsSSE, shad.IsTypeSkinTint, (shad.ShaderFlags_F4SPF1 And NiflySharp.Enums.Fallout4ShaderPropertyFlags1.Skin_Tint) <> 0),
                .BackLighting = shad.HasBacklight,
                .BackLightPower = shad.BacklightPower,
                .SpecularEnabled = shad.HasSpecular,
                .SpecularColor = NifColor3ToMaterialRgb(shad.SpecularColor),
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
                                    NifColor3ToMaterialRgb(shad.SkinTintColor),
                                    If(shad.IsTypeHairTint,
                                        NifColor3ToMaterialRgb(shad.HairTintColor),
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
                .CastShadows = ShaderHelper.HasFlagSF1(shad, CastShadowsFlagValue(shad)),
                .HideSecret = ShaderHelper.HasFlagSF1(shad, HideSecretFlagValue(shad)),
                .Decal = ShaderHelper.HasFlagSF1(shad, DecalFlagValue(shad)),
                .DecalNoFade = ShaderHelper.HasFlagSF2(shad, NoFadeFlagValue(shad)),
                .ZBufferWrite = ShaderHelper.HasFlagSF2(shad, ZBufferWriteFlagValue(shad)),
                .ZBufferTest = ShaderHelper.HasFlagSF1(shad, ZBufferTestFlagValue(shad)),
                .Refraction = ShaderHelper.HasFlagSF1(shad, RefractionFlagValue(shad)),
                .AnisoLighting = ShaderHelper.HasFlagSF2(shad, AnisotropicLightingFlagValue(shad)),
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
        ' Guard: a null shader (NIF without a BSLightingShaderProperty) must not NRE here.
        ' ShaderType_SK_FO4 is the unified accessor — same backing field as ShaderType
        ' (NiObjectNET.g.cs:22), used consistently with Save_To_Shader (vb:2616).
        If Not IsNothing(shad) Then
            _NifShaderType = shad.ShaderType_SK_FO4
            _skinTintAlpha = shad.SkinTintAlpha
            _NifGlossiness = shad.Glossiness
        Else
            _NifShaderType = NiflySharp.Enums.BSLightingShaderType.Default
            _skinTintAlpha = 1.0F
            _NifGlossiness = 1.0F
        End If
        ApplyAlphaPropertyFromNif(shap, Nif)
        ' NO llamar ApplyShaderTypeToBgsm acá: los flags ya vienen FIELES del shader (Facegen/
        ' SkinTint/Hair/Tree/Glowmap/EnvMapping leídos arriba, incluido .Hair desde el flag F4 y
        ' .Tree desde HasTreeAnim). La llamada con semántica de asignación PISABA esos flags
        ' cuando el tipo no era el correspondiente (humanhair01 perdía bHair; 118.996 tree shapes
        ' perdían bTree) — probe typeflags 2026-06-11.
        ClearDirty()
    End Sub

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
                _blendFunctionSource = NiflySharp.Enums.AlphaFunction.SRC_ALPHA
                _blendFunctionDest = NiflySharp.Enums.AlphaFunction.INV_SRC_ALPHA
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
            .EmittanceColor = NifColor3ToMaterialRgb(shad.EmittanceColor),
            .FalloffEnabled = ShaderHelper.HasFlagSF1(shad, ShaderHelper.FalloffFlagValue(shad)),
            .FalloffColorEnabled = Not Nif.Header.Version.IsSSE AndAlso (shad.ShaderFlags_F4SPF1 And NiflySharp.Enums.Fallout4ShaderPropertyFlags1.RGB_Falloff) <> 0,
            .GrayscaleToPaletteColor = shad.HasGreyscaleToPaletteColor,
            .GrayscaleToPaletteAlpha = shad.HasGreyscaleToPaletteAlpha,
            .EffectLightingEnabled = (If(Nif.Header.Version.IsSSE,
                                        (shad.ShaderFlags_SSPF2 And NiflySharp.Enums.SkyrimShaderPropertyFlags2.Effect_Lighting) <> 0,
                                        (shad.ShaderFlags_F4SPF2 And NiflySharp.Enums.Fallout4ShaderPropertyFlags2.Effect_Lighting) <> 0)),
            .BaseColor = NifColor4ToMaterialRgb(shad.BaseColor),
            .BaseColorScale = shad.BaseColorScale,
            .FalloffStartAngle = shad.FalloffStartAngle,
            .FalloffStopAngle = shad.FalloffStopAngle,
            .FalloffStartOpacity = shad.FalloffStartOpacity,
            .FalloffStopOpacity = shad.FalloffStopOpacity,
            .LightingInfluence = shad.LightingInfluence / 255.0F,
            .SoftDepth = shad.SoftFalloffDepth,
            .Glowmap = shad.HasGlowmap,
            .EnvmapMinLOD = shad.EnvMapMinLOD,
            .BloodEnabled = ShaderHelper.HasFlagSF2(shad, WeaponBloodFlagValue(shad)),
            .SoftEnabled = ShaderHelper.HasFlagSF1(shad, SoftEffectFlagValue(shad)),
            .Decal = ShaderHelper.HasFlagSF1(shad, DecalFlagValue(shad)),
            .DecalNoFade = ShaderHelper.HasFlagSF2(shad, NoFadeFlagValue(shad)),
            .ZBufferWrite = ShaderHelper.HasFlagSF2(shad, ZBufferWriteFlagValue(shad)),
            .ZBufferTest = ShaderHelper.HasFlagSF1(shad, ZBufferTestFlagValue(shad)),
            .Refraction = ShaderHelper.HasFlagSF1(shad, RefractionFlagValue(shad))
                       }
        Else
            mat = New BGEM
        End If
        mat.Version = DefaultMaterialVersionForNif(Nif)
        mat.AlphaTest = False
        mat.AlphaTestRef = 128
        mat.AlphaBlendMode = AlphaBlendModeType.None
        If shad IsNot Nothing Then
            mat.Alpha = shad.BaseColor.A
        End If
        Underlying_Material = mat
        ApplyAlphaPropertyFromNif(shap, Nif)
        ClearDirty()
    End Sub
    Public Sub Save_To_Shader(Nif As Nifcontent_Class_Manolo, shap As INiShape, shad As BSEffectShaderProperty)
        If Nif.Valid = False Then Exit Sub
        Dim Mat = DirectCast(Underlying_Material, BGEM)
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
        shad.HasEnvironmentMapping = Mat.EnvironmentMapping
        shad.EnvironmentMapScale = Mat.EnvironmentMappingMaskScale
        shad.EmittanceColor = MaterialRgbToNifColor3(Mat.EmittanceColor)
        EnsureNiString4(shad.SourceTexture, Mat.BaseTexture)
        EnsureNiString4(shad.NormalTexture, Mat.NormalTexture)
        EnsureNiString4(shad.GreyscaleTexture, Mat.GrayscaleTexture)
        EnsureNiString4(shad.EnvMapTexture, Mat.EnvmapTexture)
        EnsureNiString4(shad.EnvMaskTexture, Mat.EnvmapMaskTexture)
        EnsureNiString4(shad.LightingTexture, Mat.LightingTexture)
        EnsureNiString4(shad.ReflectanceTexture, Mat.SpecularTexture)
        EnsureNiString4(shad.EmitGradientTexture, Mat.GlowTexture)

        Dim bcColor = Me.BaseColor
        shad.BaseColor = New NiflySharp.Structs.Color4(
            bcColor.R / 255.0F,
            bcColor.G / 255.0F,
            bcColor.B / 255.0F,
            Mat.Alpha)
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

        ShaderHelper.SetFlagSF2(shad, WeaponBloodFlagValue(shad), Mat.BloodEnabled)
        ShaderHelper.SetFlagSF1(shad, SoftEffectFlagValue(shad), Mat.SoftEnabled)
        ShaderHelper.SetFlagSF1(shad, DecalFlagValue(shad), Mat.Decal)
        ShaderHelper.SetFlagSF2(shad, NoFadeFlagValue(shad), Mat.DecalNoFade)
        ShaderHelper.SetFlagSF2(shad, ZBufferWriteFlagValue(shad), Mat.ZBufferWrite)
        ShaderHelper.SetFlagSF1(shad, ZBufferTestFlagValue(shad), Mat.ZBufferTest)
        ShaderHelper.SetFlagSF1(shad, RefractionFlagValue(shad), Mat.Refraction)
        ShaderHelper.SetFlagSF1(shad, ShaderHelper.FalloffFlagValue(shad), Mat.FalloffEnabled)

        If Nif.Header.Version.IsSSE Then
            If Mat.EffectLightingEnabled Then
                shad.ShaderFlags_SSPF2 = shad.ShaderFlags_SSPF2 Or NiflySharp.Enums.SkyrimShaderPropertyFlags2.Effect_Lighting
            Else
                shad.ShaderFlags_SSPF2 = shad.ShaderFlags_SSPF2 And Not NiflySharp.Enums.SkyrimShaderPropertyFlags2.Effect_Lighting
            End If
        Else
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

    <Browsable(False)>
    Public ReadOnly Property ResolvedWetnessControlSpecScale As Single
        Get
            Return ResolvedWetness()(0)
        End Get
    End Property
    <Browsable(False)>
    Public ReadOnly Property ResolvedWetnessControlSpecPowerScale As Single
        Get
            Return ResolvedWetness()(1)
        End Get
    End Property
    <Browsable(False)>
    Public ReadOnly Property ResolvedWetnessControlSpecMinvar As Single
        Get
            Return ResolvedWetness()(2)
        End Get
    End Property
    <Browsable(False)>
    Public ReadOnly Property ResolvedWetnessControlEnvMapScale As Single
        Get
            Return ResolvedWetness()(3)
        End Get
    End Property
    <Browsable(False)>
    Public ReadOnly Property ResolvedWetnessControlFresnelPower As Single
        Get
            Return ResolvedWetness()(4)
        End Get
    End Property
    <Browsable(False)>
    Public ReadOnly Property ResolvedWetnessControlMetalness As Single
        Get
            Return ResolvedWetness()(5)
        End Get
    End Property
    Private _resolvedWetnessCache As Single()
    Private _resolvedWetnessRaw As Single()
    ' Entradas NO-crudas de la resolución (eligen el template del fallback / la cadena). El cache
    ' debe invalidarse si cambian, no solo la wetness cruda — si no, queda stale: el valor cacheado
    ' en el primer render refleja el estado de ESE momento aunque luego cambien flags/root.
    Private _resolvedWetnessRootPath As String = Nothing
    Private _resolvedWetnessFacegen As Boolean
    Private _resolvedWetnessSkinTint As Boolean
    Private _resolvedWetnessValid As Boolean = False

    Private Function ResolvedWetness() As Single()
        Dim bgsm = TryCast(Underlying_Material, BGSM)
        ' No BGSM yet → -1 across the board, uncached: a wrapper can be constructed with the default
        ' BGEM and have its BGSM assigned later, and that first real read must still resolve.
        If bgsm Is Nothing Then Return New Single() {-1.0F, -1.0F, -1.0F, -1.0F, -1.0F, -1.0F}
        Dim raw() As Single = {bgsm.WetnessControlSpecScale, bgsm.WetnessControlSpecPowerScale,
                               bgsm.WetnessControlSpecMinvar, bgsm.WetnessControlEnvMapScale,
                               bgsm.WetnessControlFresnelPower, bgsm.WetnessControlMetalness}
        ' Cache válido solo si TODAS las entradas de ResolveEffectiveWetness coinciden con las
        ' cacheadas: wetness cruda + RootMaterialPath + Facegen + SkinTint. (Medido 2026-06-21:
        ' keyear solo por la cruda dejaba el Resolved* stale y producía falso "material modificado"
        ' en WM Revisa_Material — live cacheado 0.8 vs recomputado 0.6 con entradas idénticas.)
        If Not _resolvedWetnessValid _
           OrElse Not SameRawWetness(raw, _resolvedWetnessRaw) _
           OrElse Not String.Equals(If(bgsm.RootMaterialPath, ""), If(_resolvedWetnessRootPath, ""), StringComparison.OrdinalIgnoreCase) _
           OrElse bgsm.Facegen <> _resolvedWetnessFacegen _
           OrElse bgsm.SkinTint <> _resolvedWetnessSkinTint Then
            _resolvedWetnessCache = ResolveEffectiveWetness(bgsm)
            _resolvedWetnessRaw = raw
            _resolvedWetnessRootPath = bgsm.RootMaterialPath
            _resolvedWetnessFacegen = bgsm.Facegen
            _resolvedWetnessSkinTint = bgsm.SkinTint
            _resolvedWetnessValid = True
        End If
        Return _resolvedWetnessCache
    End Function
    Private Shared Function SameRawWetness(a As Single(), b As Single()) As Boolean
        If a Is Nothing OrElse b Is Nothing Then Return False
        For i = 0 To 5
            If a(i) <> b(i) Then Return False
        Next
        Return True
    End Function

    Private Const DefaultWetTemplate As String = "template/defaultTemplate_wet.bgsm"
    Private Const SkinWetTemplate As String = "template/SkinTemplate_Wet.bgsm"

    Private Function ResolveEffectiveWetness(bgsm As BGSM) As Single()
        Const SENTINEL As Single = -1.0F
        Dim eff() As Single = {bgsm.WetnessControlSpecScale, bgsm.WetnessControlSpecPowerScale, bgsm.WetnessControlSpecMinvar,
                               bgsm.WetnessControlEnvMapScale, bgsm.WetnessControlFresnelPower, bgsm.WetnessControlMetalness}
        Dim rootPath = bgsm.RootMaterialPath
        Dim seen As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        Dim defaultApplied = False
        Dim depth = 0
        While depth < 16 AndAlso Array.IndexOf(eff, SENTINEL) >= 0
            If String.IsNullOrEmpty(rootPath) Then
                ' Explicit chain ended but fields remain -1 → inherit from the engine template, once.
                ' CK resuelve materiales de PIEL/CARA (Facegen/SkinTint) desde SkinTemplate_Wet (0.6...),
                ' los genéricos desde defaultTemplate_wet (0.8...). Las shapes de cabeza ghoul embeben el
                ' shader con RootMaterialName='' (matPath='') → caen acá; CK las bakea 0.6 (SkinTemplate),
                ' no 0.8. Las cabezas humanas referencian su .bgsm externo (root=SkinTemplate explícito) y
                ' nunca llegan a este fallback. Verificado: GhoulMatProbe + log SHAPEMAT-FINAL (root='')
                ' 2026-06-13. SkinTemplate_Wet a su vez encadena a defaultTemplate_wet para lo que no cubra.
                If defaultApplied Then Exit While
                rootPath = If(bgsm.Facegen OrElse bgsm.SkinTint, SkinWetTemplate, DefaultWetTemplate)
                defaultApplied = True
            End If
            Dim key = MaterialsPrefix & CorrectMaterialPath(rootPath).StripPrefix(MaterialsPrefix)
            If Not seen.Add(key.ToLowerInvariant()) Then Exit While   ' cycle guard (incl. default self-ref)
            Dim parent = LoadBgsmByKey(key)
            If parent Is Nothing Then Exit While
            Dim pv() As Single = {parent.WetnessControlSpecScale, parent.WetnessControlSpecPowerScale, parent.WetnessControlSpecMinvar,
                                  parent.WetnessControlEnvMapScale, parent.WetnessControlFresnelPower, parent.WetnessControlMetalness}
            For i = 0 To 5
                If eff(i) = SENTINEL AndAlso pv(i) <> SENTINEL Then eff(i) = pv(i)
            Next
            rootPath = parent.RootMaterialPath
            depth += 1
        End While
        Return eff
    End Function
    Private Shared Function LoadBgsmByKey(key As String) As BGSM
        Try
            Dim bytes = FilesDictionary_class.GetBytes(key)
            If bytes Is Nothing OrElse bytes.Length = 0 Then Return Nothing
            Dim m As New BGSM
            Using ms As New MemoryStream(bytes)
                Using rd As New BinaryReader(ms)
                    m.Deserialize(rd)
                End Using
            End Using
            Return m
        Catch
            Return Nothing
        End Try
    End Function

    Public Sub Save_To_Shader(Nif As Nifcontent_Class_Manolo, shap As INiShape, shad As BSLightingShaderProperty, Optional shaderType As NiflySharp.Enums.BSLightingShaderType = NiflySharp.Enums.BSLightingShaderType.Default, Optional envmapMaskPath As String = "")
        If Nif.Valid = False Then Exit Sub
        Dim Mat = DirectCast(Underlying_Material, BGSM)
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
        shad.EmissiveColor = MaterialRgbToNifColor4(Mat.EmittanceColor, 0.0F)
        shad.EmissiveMultiple = Mat.EmittanceMult
        shad.Alpha = Mat.Alpha
        ' Sin downgrade EnvironmentMap→Default cuando falta la textura: refutado por vanilla
        ' (50.303 shapes EnvironmentMap sin textura conservan el tipo — probe invariantes
        ' 2026-06-11; el origen del downgrade no estaba documentado). La invariante confirmada
        ' que SÍ se conserva: EnvironmentMapScale = 1.0 cuando env mapping está OFF (882k shapes).
        Dim effectiveShaderType = shaderType
        Dim effectiveEnvMapping = Mat.EnvironmentMapping
        shad.HasEnvironmentMapping = effectiveEnvMapping
        If effectiveShaderType = NiflySharp.Enums.BSLightingShaderType.EyeEnvmap Then
            ' Simétrico a Create (lee EyeCubemapScale para IsTypeEyeEnvironmentMap).
            shad.EyeCubemapScale = Mat.EnvironmentMappingMaskScale
        Else
            shad.EnvironmentMapScale = If(effectiveEnvMapping, Mat.EnvironmentMappingMaskScale, 1.0F)
        End If
        If Nif.Header.Version.IsSSE Then
            shad.Glossiness = CSng(Math.Pow(2.0, CDbl(Mat.Smoothness) * 10.0 + 1.0))
        Else
            shad.Smoothness = Mat.Smoothness
        End If
        shad.SubsurfaceRolloff = If(Mat.SubsurfaceLighting, Mat.SubsurfaceLightingRolloff, 0.0F)
        shad.ModelSpace = Mat.ModelSpaceNormals
        shad.ShaderType_SK_FO4 = effectiveShaderType
        ' Convenciones tipo→flag de Skyrim (sweep SSE 2026-06-11, ~99-100% en 73.128 shapes
        ' vanilla): sin su flag el engine SK no aplica el comportamiento del tipo (un SkinTint
        ' sin FaceGen_RGB_Tint no tintea). Assert-on solamente — nunca se apaga nada. Miembros
        ' verificados en Bitflags.SkyrimShaderPropertyFlags{1,2}.g.cs. Path FO4 intocado.
        If shad.Type = NiflySharp.Helpers.ShaderHelper.ShaderGameType.SK Then
            Select Case effectiveShaderType
                Case NiflySharp.Enums.BSLightingShaderType.FaceTint
                    shad.ShaderFlags_SSPF1 = shad.ShaderFlags_SSPF1 Or NiflySharp.Enums.SkyrimShaderPropertyFlags1.Facegen_Detail_Map
                    shad.ShaderFlags_SSPF2 = shad.ShaderFlags_SSPF2 Or NiflySharp.Enums.SkyrimShaderPropertyFlags2.Soft_Lighting
                Case NiflySharp.Enums.BSLightingShaderType.SkinTint
                    shad.ShaderFlags_SSPF1 = shad.ShaderFlags_SSPF1 Or NiflySharp.Enums.SkyrimShaderPropertyFlags1.FaceGen_RGB_Tint
                Case NiflySharp.Enums.BSLightingShaderType.HairTint
                    shad.ShaderFlags_SSPF1 = shad.ShaderFlags_SSPF1 Or NiflySharp.Enums.SkyrimShaderPropertyFlags1.Hair_Soft_Lighting
                Case NiflySharp.Enums.BSLightingShaderType.GlowShader
                    shad.ShaderFlags_SSPF2 = shad.ShaderFlags_SSPF2 Or NiflySharp.Enums.SkyrimShaderPropertyFlags2.Glow_Map
                Case NiflySharp.Enums.BSLightingShaderType.EnvironmentMap
                    shad.ShaderFlags_SSPF1 = shad.ShaderFlags_SSPF1 Or NiflySharp.Enums.SkyrimShaderPropertyFlags1.Environment_Mapping
                Case NiflySharp.Enums.BSLightingShaderType.EyeEnvmap
                    shad.ShaderFlags_SSPF1 = shad.ShaderFlags_SSPF1 Or NiflySharp.Enums.SkyrimShaderPropertyFlags1.Eye_Environment_Mapping
            End Select
        End If
        Dim hairTintNifColor = MaterialRgbToNifColor3(Mat.HairTintColor)
        shad.HairTintColor = hairTintNifColor
        If Mat.SkinTint Then
            shad.SkinTintColor = hairTintNifColor
            shad.SkinTintAlpha = Me.SkinTintAlpha
        End If
        shad.HasBacklight = Mat.BackLighting
        shad.BacklightPower = Mat.BackLightPower
        shad.HasSpecular = Mat.SpecularEnabled AndAlso Mat.SpecularMult <> 0.0F
        shad.SpecularColor = MaterialRgbToNifColor3(Mat.SpecularColor)
        shad.SpecularStrength = Mat.SpecularMult
        shad.HasGlowmap = Mat.Glowmap
        shad.HasTreeAnim = Mat.Tree
        shad.HasSoftlight = Mat.SubsurfaceLighting
        shad.HasRimlight = Mat.RimLighting
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
        wet.SpecScale = Me.ResolvedWetnessControlSpecScale
        wet.SpecPower = Me.ResolvedWetnessControlSpecPowerScale
        wet.MinVar = Me.ResolvedWetnessControlSpecMinvar
        wet.EnvMapScale = Me.ResolvedWetnessControlEnvMapScale
        wet.FresnelPower = Me.ResolvedWetnessControlFresnelPower
        wet.Metalness = Me.ResolvedWetnessControlMetalness
        shad.Wetness = wet

        ' P4: game-aware. For FO4 (the byte-faithful FaceGen path) these helpers return the same
        ' Fallout4ShaderPropertyFlags* values as before, so the FO4 output is byte-identical. For SK
        ' they return the homonymous SK bit, or 0 (safe no-op in SetFlagSF*) when SK has no equivalent
        ' (Hair is FO4-only).
        ShaderHelper.SetFlagSF1(shad, CastShadowsFlagValue(shad), Mat.CastShadows)
        ShaderHelper.SetFlagSF1(shad, HideSecretFlagValue(shad), Mat.HideSecret)
        ShaderHelper.SetFlagSF1(shad, DecalFlagValue(shad), Mat.Decal)
        ShaderHelper.SetFlagSF2(shad, NoFadeFlagValue(shad), Mat.DecalNoFade)
        ShaderHelper.SetFlagSF2(shad, ZBufferWriteFlagValue(shad), Mat.ZBufferWrite)
        ShaderHelper.SetFlagSF1(shad, ZBufferTestFlagValue(shad), Mat.ZBufferTest)
        ShaderHelper.SetFlagSF1(shad, RefractionFlagValue(shad), Mat.Refraction)
        ShaderHelper.SetFlagSF2(shad, AnisotropicLightingFlagValue(shad), Mat.AnisoLighting)
        ShaderHelper.SetFlagSF1(shad, HairFlagValue(shad), Mat.Hair)
        ShaderHelper.SetFlagSF1(shad, EyeEnvironmentMappingFlagValue(shad), Mat.EnvironmentMappingEye)

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

        ' Slot 2: glow OR lightmask/subsurface (SSE dual-purpose). FaceTint (technique 4) does subsurface
        ' UNCONDITIONALLY in the engine -- the facegen PS has NO Soft_Lighting gate (verified
        ' sse_facegen_skin.asm) -- so include Facegen: the _sk lands on LightingTexture even when the
        ' head's Soft_Lighting flag is not set (it often isn't).
        Dim slot2 = texset.Textures(textset_GlowTexture).Content
        If isSSE AndAlso Not mat.Glowmap AndAlso (mat.SubsurfaceLighting OrElse mat.RimLighting OrElse mat.Facegen) Then
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

        ' Slot 2/6: glow vs lightmask/subsurface remapping (SSE dual-purpose). Mirror the read: facegen
        ' ignores the Soft_Lighting flag (engine does subsurface unconditionally for technique 4).
        If isSSE AndAlso Not mat.Glowmap AndAlso (mat.SubsurfaceLighting OrElse mat.RimLighting OrElse mat.Facegen) Then
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
        ' P5 — JSON payload guard. A handful of vanilla materials (5 BGEM in Fallout4 - Startup.ba2)
        ' are stored as JSON text, not the binary BGSM/BGEM layout. MaterialLib's binary Deserialize
        ' would throw on them. If the first non-whitespace byte is '{', degrade with grace: leave a
        ' fresh default instance of the requested type and return cleanly (no throw). The three alpha
        ' fields keep their constructor defaults; the caller's ClearDirty (Deserialize(Diccionario))
        ' still runs.
        Dim firstByteIdx = 0
        While firstByteIdx < Memory.Length AndAlso (Memory(firstByteIdx) = AscW(" "c) OrElse Memory(firstByteIdx) = AscW(vbTab) OrElse Memory(firstByteIdx) = AscW(vbCr) OrElse Memory(firstByteIdx) = AscW(vbLf))
            firstByteIdx += 1
        End While
        If firstByteIdx < Memory.Length AndAlso Memory(firstByteIdx) = AscW("{"c) Then
            Select Case type
                Case GetType(BGSM)
                    Underlying_Material = New BGSM
                Case GetType(BGEM)
                    Underlying_Material = NewBgemNormalized()
                Case Else
                    Throw New Exception("Tipo no soportado en Deserialize.")
            End Select
            Return
        End If
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
            If _NifShaderType = NiflySharp.Enums.BSLightingShaderType.Default Then
                ' Desacoplado: el tipo NO se deriva de los flags del .bgsm (el .bgsm no lleva
                ' tipo). Se preserva el tipo real del shader inline del NIF. Tipo y flags son
                ' ejes ortogonales.
                Dim bslsp = TryCast(Nif.GetShader(shap), BSLightingShaderProperty)
                If bslsp IsNot Nothing Then _NifShaderType = bslsp.ShaderType_SK_FO4
            End If
        End If
        ' NOTE: ClearDirty() is NOT called here. The single snapshot is taken by the caller
        ' Deserialize(Diccionario,...) AFTER it loads the sidecar, so the sidecar-sourced
        ' _EnvmapMaskPath/flow/lighting state is part of the clean baseline (was a double
        ' ClearDirty before). Direct callers of this byte overload would need to ClearDirty
        ' themselves — but no external caller uses it (all go through the Diccionario overload).
    End Sub

    Public Sub Deserialize(Diccionario As String, type As Type, shap As INiShape, Nif As Nifcontent_Class_Manolo)
        Deserialize(FilesDictionary_class.GetBytes(Diccionario), type, shap, Nif)
        ResolveSidecarJson(Diccionario, type)
        ' Fresh load (including sidecar) → clean state.
        ClearDirty()
    End Sub

    ''' <summary>Byte-source overload that mirrors <see cref="Deserialize(String, Type, INiShape, Nifcontent_Class_Manolo)"/>
    ''' exactly: deserialize from the already-fetched <paramref name="memory"/> bytes, then resolve the
    ''' `&lt;file&gt;.bgsm.json` / `.bgem.json` sidecar via <paramref name="diccionarioForSidecar"/>, then
    ''' ClearDirty. Lets a caller that has already located the dictionary entry pass the bytes directly
    ''' (avoiding a redundant dictionary lookup) without dropping the sidecar. The extra String parameter
    ''' disambiguates this from the <c>(Byte(), Type, …)</c> overload.</summary>
    Public Sub Deserialize(memory As Byte(), diccionarioForSidecar As String, type As Type, shap As INiShape, Nif As Nifcontent_Class_Manolo)
        Deserialize(memory, type, shap, Nif)
        ResolveSidecarJson(diccionarioForSidecar, type)
        ' Fresh load (including sidecar) → clean state.
        ClearDirty()
    End Sub

    ''' <summary>Resolve the `&lt;file&gt;.bgsm.json` (or `.bgem.json`) sidecar that lives next to the
    ''' material, resolvable via FilesDictionary the same way (loose or BA2/BSA). Carries fields the
    ''' binary BGSM/BGEM cannot persist — today: envmapMaskTexture for BGSM, plus the Skyrim-container
    ''' flow/lighting textures. Missing or invalid sidecar is silent (regla Q3=a): _EnvmapMaskPath stays "".
    ''' Extracted verbatim from the Diccionario overload so both byte and string entry points share it.</summary>
    Private Sub ResolveSidecarJson(diccionario As String, type As Type)
        Try
            Dim sidecarKey = diccionario & ".json"
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
                            ' Skyrim container (option (b)): the v2 binary has no slot for the SSE
                            ' flow (slot 5) / lighting (slot 6) textures, so they live in the sidecar.
                            ' Only present when non-empty; absent key leaves the field as deserialized.
                            Dim bgsm = TryCast(Underlying_Material, BGSM)
                            If bgsm IsNot Nothing Then
                                Dim flow As JsonElement = Nothing
                                If root.TryGetProperty("flowTexture", flow) AndAlso flow.ValueKind = JsonValueKind.String Then
                                    bgsm.FlowTexture = If(flow.GetString(), "")
                                End If
                                Dim lighting As JsonElement = Nothing
                                If root.TryGetProperty("lightingTexture", lighting) AndAlso lighting.ValueKind = JsonValueKind.String Then
                                    bgsm.LightingTexture = If(lighting.GetString(), "")
                                End If
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
        ' Sidecar carries fields the v2 binary cannot persist: the runtime envmap-mask path
        ' (NIF slot 5, FO4) plus the Skyrim-container (option (b)) flow (slot 5) / lighting
        ' (slot 6) textures. Each key is written only when non-empty.
        WriteMaterialSidecar(filePath, _EnvmapMaskPath, bgsm.FlowTexture, bgsm.LightingTexture)
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

    Private Shared Sub WriteMaterialSidecar(materialPath As String, envmapMask As String, flowTexture As String, lightingTexture As String)
        Dim sidecarPath = materialPath & ".json"
        Dim payload As New Dictionary(Of String, String)
        If Not String.IsNullOrEmpty(envmapMask) Then payload("envmapMaskTexture") = envmapMask
        If Not String.IsNullOrEmpty(flowTexture) Then payload("flowTexture") = flowTexture
        If Not String.IsNullOrEmpty(lightingTexture) Then payload("lightingTexture") = lightingTexture
        If payload.Count = 0 Then
            ' Nothing to persist — remove a previous sidecar so we don't leak stale state.
            Try
                If File.Exists(sidecarPath) Then File.Delete(sidecarPath)
            Catch
                ' Best-effort delete; ignore.
            End Try
            Return
        End If
        Dim opts = New JsonSerializerOptions With {.WriteIndented = True}
        File.WriteAllText(sidecarPath, JsonSerializer.Serialize(payload, opts))
    End Sub

    ' MaterialLib stores BGSM/BGEM colors as RGB-only uint values: 0x00RRGGBB.
    ' Alpha belongs either to UI-only System.Drawing.Color, NIF Color4, or a
    ' separate material field such as BGEM.Alpha. Do not treat material uints as ARGB.
    Private Shared Function MaterialRgbToColor(rgb As UInteger, Optional alpha As Integer = 255) As Color
        rgb = rgb And &HFFFFFFUI
        Dim r = ((rgb >> 16) And &HFF)
        Dim g = ((rgb >> 8) And &HFF)
        Dim b = (rgb And &HFF)
        Return System.Drawing.Color.FromArgb(ClampByte(alpha), r, g, b)
    End Function

    Private Shared Function ColorToMaterialRgb(c As Color) As UInteger
        Return CType((CUInt(c.R) << 16) Or (CUInt(c.G) << 8) Or CUInt(c.B), UInteger)
    End Function

    Private Shared Function MaterialRgbToNifColor3(rgb As UInteger) As NiflySharp.Structs.Color3
        rgb = rgb And &HFFFFFFUI
        Dim r = ((rgb >> 16) And &HFF)
        Dim g = ((rgb >> 8) And &HFF)
        Dim b = (rgb And &HFF)
        Return New Color3(r / 255, g / 255, b / 255)
    End Function

    Private Shared Function MaterialRgbToNifColor4(rgb As UInteger, alpha As Single) As NiflySharp.Structs.Color4
        rgb = rgb And &HFFFFFFUI
        Dim r = ((rgb >> 16) And &HFF)
        Dim g = ((rgb >> 8) And &HFF)
        Dim b = (rgb And &HFF)
        Return New Color4(r / 255, g / 255, b / 255, Math.Min(1.0F, Math.Max(0.0F, alpha)))
    End Function
    Private Shared Function ClampByte(value As Single) As Integer
        Return Math.Min(255, Math.Max(0, CInt(value)))
    End Function
    Private Shared Function NifColor3ToMaterialRgb(color As NiflySharp.Structs.Color3) As UInteger
        Return CType((CUInt(ClampByte(color.R * 255)) << 16) Or
                     (CUInt(ClampByte(color.G * 255)) << 8) Or
                     CUInt(ClampByte(color.B * 255)), UInteger)
    End Function

    Private Shared Function NifColor4ToMaterialRgb(color As NiflySharp.Structs.Color4) As UInteger
        Return CType((CUInt(ClampByte(color.R * 255)) << 16) Or
                     (CUInt(ClampByte(color.G * 255)) << 8) Or
                     CUInt(ClampByte(color.B * 255)), UInteger)
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

    Public Shared Function CorrectMeshPath(Mesh As String) As String
        Return NormalizeGameRelativePath(Mesh, MeshesPrefix)
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
    ''' Cobertura de <c>NifShaderType</c>: GetDifferences itera TODAS las propiedades públicas
    ''' por reflexión, así que NifShaderType (propiedad pública) se compara como cualquier otra,
    ''' y AreEqualWithTrace delega aquí — NINGUNO de los dos lo excluye. Tipo y flags están
    ''' desacoplados (el tipo se lee/escribe fiel del shader, no se deriva de los flags), por lo
    ''' que comparar el tipo es legítimo y no es ruido redundante. Para diagnóstico
    ''' (NPC_Manager.FaceGenComparator validando un bake contra el FaceGen de CK) preferimos ver
    ''' ese campo si difiere.
    '''
    ''' Diseño: este método existe para que call sites que necesitan reportar QUÉ difiere
    ''' (no sólo si difiere algo) no tengan que duplicar el bucle de reflexión. AreEqualWithTrace
    ''' delega a este método; agregar nuevas propiedades a FO4UnifiedMaterial_Class las cubre
    ''' automáticamente en los dos call paths.
    ''' </summary>
    ''' <param name="ignoreNames">Nombres de propiedad a OMITIR de la comparación. Default Nothing =
    ''' compara todo (contrato histórico; lo usan FaceGenComparator y AreEqualTo). El gating del
    ''' editor pasa <see cref="NifShaderOnlyPropertyNames"/> para excluir el estado solo-NIF.</param>
    Public Shared Function GetDifferences(a As FO4UnifiedMaterial_Class, b As FO4UnifiedMaterial_Class,
                                          Optional ignoreNames As ICollection(Of String) = Nothing) As List(Of MaterialDifference)
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
            If ignoreNames IsNot Nothing AndAlso ignoreNames.Contains(prop.Name) Then Continue For
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
    ''' se cubre automáticamente, incluyendo <c>NifShaderType</c>, que ahora es un campo
    ''' independiente (tipo y flags desacoplados; el tipo no se deriva de los flags).
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

    ''' <summary>Igualdad restringida al estado del ARCHIVO de material: ignora los campos solo-NIF
    ''' de <see cref="NifShaderOnlyPropertyNames"/>. La usa Revisa_Material (WM) para no exigir
    ''' "grabar el material" cuando el único cambio es el shader type (que vive en el NIF).</summary>
    Public Function AreEqualToMaterialFile(b As FO4UnifiedMaterial_Class) As Boolean
        If Me Is Nothing OrElse b Is Nothing Then Return Me Is b
        Return GetDifferences(Me, b, NifShaderOnlyPropertyNames).Count = 0
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


