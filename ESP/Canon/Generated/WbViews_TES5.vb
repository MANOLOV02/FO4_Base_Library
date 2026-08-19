' ============================================================================================
' ARCHIVO GENERADO — NO EDITAR A MANO.  Regenerar: Tools/CanonViewGen
'
' Una propiedad por campo de cada tipo de record de Skyrim. El nombre de la
' propiedad ES el nombre del campo en el formato: no hay ninguna tabla de
' equivalencias que mantener, y si el formato cambia un campo el codigo que lo
' usaba deja de compilar.
' ============================================================================================

Namespace Canon

    ''' <summary>Campos de un record ARMA de Skyrim.</summary>
    Public NotInheritable Class ArmaSSE
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>EDID\Editor ID</summary>
        Public Property EditorID As String
            Get
                Return Txt("EDID\Editor ID")
            End Get
            Set(value As String)
                Escribir("EDID\Editor ID", value)
            End Set
        End Property

        ''' <summary>BOD2\Biped Body Template\First Person Flags</summary>
        Public Property BipedBodyTemplateFirstPersonFlags As UInteger
            Get
                Return CUInt(Entero("BOD2\Biped Body Template\First Person Flags"))
            End Get
            Set(value As UInteger)
                Escribir("BOD2\Biped Body Template\First Person Flags", CLng(value))
            End Set
        End Property

        ''' <summary>BOD2\Biped Body Template\General Flags</summary>
        Public Property BipedBodyTemplateGeneralFlags As Long
            Get
                Return CLng(Entero("BOD2\Biped Body Template\General Flags"))
            End Get
            Set(value As Long)
                Escribir("BOD2\Biped Body Template\General Flags", CLng(value))
            End Set
        End Property

        ''' <summary>Bit 0 de BOD2\Biped Body Template\General Flags: (ARMA)Modulates Voice</summary>
        Public Property BipedBodyTemplateGeneralFlagsARMAModulatesVoice As Boolean
            Get
                Return Bit("BOD2\Biped Body Template\General Flags", 0)
            End Get
            Set(value As Boolean)
                PonerBit("BOD2\Biped Body Template\General Flags", 0, value)
            End Set
        End Property
        ''' <summary>Bit 4 de BOD2\Biped Body Template\General Flags: (ARMO)Non-Playable</summary>
        Public Property BipedBodyTemplateGeneralFlagsARMONonPlayable As Boolean
            Get
                Return Bit("BOD2\Biped Body Template\General Flags", 4)
            End Get
            Set(value As Boolean)
                PonerBit("BOD2\Biped Body Template\General Flags", 4, value)
            End Set
        End Property

        ''' <summary>BOD2\Biped Body Template\Armor Type</summary>
        Public Property BipedBodyTemplateArmorType As UInteger
            Get
                Return CUInt(Entero("BOD2\Biped Body Template\Armor Type"))
            End Get
            Set(value As UInteger)
                Escribir("BOD2\Biped Body Template\Armor Type", CLng(value))
            End Set
        End Property

        ''' <summary>RNAM\Race  -&gt;  RACE. Referencia en el espacio del orden de carga.</summary>
        Public Property Race As UInteger
            Get
                Return Referencia("RNAM\Race")
            End Get
            Set(value As UInteger)
                PonerReferencia("RNAM\Race", value)
            End Set
        End Property

        ''' <summary>DNAM\Data\Male Priority</summary>
        Public Property DataMalePriority As Byte
            Get
                Return CByte(Entero("DNAM\Data\Male Priority"))
            End Get
            Set(value As Byte)
                Escribir("DNAM\Data\Male Priority", CLng(value))
            End Set
        End Property

        ''' <summary>DNAM\Data\Female Priority</summary>
        Public Property DataFemalePriority As Byte
            Get
                Return CByte(Entero("DNAM\Data\Female Priority"))
            End Get
            Set(value As Byte)
                Escribir("DNAM\Data\Female Priority", CLng(value))
            End Set
        End Property

        ''' <summary>DNAM\Data\Weight slider - Male</summary>
        Public Property DataWeightSliderMale As Byte
            Get
                Return CByte(Entero("DNAM\Data\Weight slider - Male"))
            End Get
            Set(value As Byte)
                Escribir("DNAM\Data\Weight slider - Male", CLng(value))
            End Set
        End Property

        ''' <summary>Bit 1 de DNAM\Data\Weight slider - Male: Enabled</summary>
        Public Property DataWeightSliderMaleEnabled As Boolean
            Get
                Return Bit("DNAM\Data\Weight slider - Male", 1)
            End Get
            Set(value As Boolean)
                PonerBit("DNAM\Data\Weight slider - Male", 1, value)
            End Set
        End Property

        ''' <summary>DNAM\Data\Weight slider - Female</summary>
        Public Property DataWeightSliderFemale As Byte
            Get
                Return CByte(Entero("DNAM\Data\Weight slider - Female"))
            End Get
            Set(value As Byte)
                Escribir("DNAM\Data\Weight slider - Female", CLng(value))
            End Set
        End Property

        ''' <summary>Bit 1 de DNAM\Data\Weight slider - Female: Enabled</summary>
        Public Property DataWeightSliderFemaleEnabled As Boolean
            Get
                Return Bit("DNAM\Data\Weight slider - Female", 1)
            End Get
            Set(value As Boolean)
                PonerBit("DNAM\Data\Weight slider - Female", 1, value)
            End Set
        End Property

        ''' <summary>DNAM\Data\Detection Sound Value</summary>
        Public Property DataDetectionSoundValue As Byte
            Get
                Return CByte(Entero("DNAM\Data\Detection Sound Value"))
            End Get
            Set(value As Byte)
                Escribir("DNAM\Data\Detection Sound Value", CLng(value))
            End Set
        End Property

        ''' <summary>DNAM\Data\Weapon Adjust</summary>
        Public Property DataWeaponAdjust As Single
            Get
                Return Flt("DNAM\Data\Weapon Adjust")
            End Get
            Set(value As Single)
                Escribir("DNAM\Data\Weapon Adjust", value)
            End Set
        End Property

        ''' <summary>Biped Model\Male\MOD2\Model Filename</summary>
        Public Property MaleModelFilename As String
            Get
                Return Txt("Biped Model\Male\MOD2\Model Filename")
            End Get
            Set(value As String)
                Escribir("Biped Model\Male\MOD2\Model Filename", value)
            End Set
        End Property

        ''' <summary>Biped Model\Female\MOD3\Model Filename</summary>
        Public Property FemaleModelFilename As String
            Get
                Return Txt("Biped Model\Female\MOD3\Model Filename")
            End Get
            Set(value As String)
                Escribir("Biped Model\Female\MOD3\Model Filename", value)
            End Set
        End Property

        ''' <summary>1st Person\Male\MOD4\Model Filename</summary>
        Public Property MaleModelFilename2 As String
            Get
                Return Txt("1st Person\Male\MOD4\Model Filename")
            End Get
            Set(value As String)
                Escribir("1st Person\Male\MOD4\Model Filename", value)
            End Set
        End Property

        ''' <summary>1st Person\Female\MOD5\Model Filename</summary>
        Public Property FemaleModelFilename2 As String
            Get
                Return Txt("1st Person\Female\MOD5\Model Filename")
            End Get
            Set(value As String)
                Escribir("1st Person\Female\MOD5\Model Filename", value)
            End Set
        End Property

        ''' <summary>NAM0\Male Skin Texture  -&gt;  TXST / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property MaleSkinTexture As UInteger
            Get
                Return Referencia("NAM0\Male Skin Texture")
            End Get
            Set(value As UInteger)
                PonerReferencia("NAM0\Male Skin Texture", value)
            End Set
        End Property

        ''' <summary>NAM1\Female Skin texture  -&gt;  TXST / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property FemaleSkinTexture As UInteger
            Get
                Return Referencia("NAM1\Female Skin texture")
            End Get
            Set(value As UInteger)
                PonerReferencia("NAM1\Female Skin texture", value)
            End Set
        End Property

        ''' <summary>NAM2\Male Skin Texture Swap List  -&gt;  FLST / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property MaleSkinTextureSwapList As UInteger
            Get
                Return Referencia("NAM2\Male Skin Texture Swap List")
            End Get
            Set(value As UInteger)
                PonerReferencia("NAM2\Male Skin Texture Swap List", value)
            End Set
        End Property

        ''' <summary>NAM3\Female Skin Texture Swap List  -&gt;  FLST / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property FemaleSkinTextureSwapList As UInteger
            Get
                Return Referencia("NAM3\Female Skin Texture Swap List")
            End Get
            Set(value As UInteger)
                PonerReferencia("NAM3\Female Skin Texture Swap List", value)
            End Set
        End Property

        ''' <summary>SNDD\Footstep Sound  -&gt;  FSTS / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property FootstepSound As UInteger
            Get
                Return Referencia("SNDD\Footstep Sound")
            End Get
            Set(value As UInteger)
                PonerReferencia("SNDD\Footstep Sound", value)
            End Set
        End Property

        ''' <summary>ONAM\Art Object  -&gt;  ARTO. Referencia en el espacio del orden de carga.</summary>
        Public Property ArtObject As UInteger
            Get
                Return Referencia("ONAM\Art Object")
            End Get
            Set(value As UInteger)
                PonerReferencia("ONAM\Art Object", value)
            End Set
        End Property

        ''' <summary>Biped Model\Male\MO2S\Alternate Textures</summary>
        Public ReadOnly Property AlternateTextures As IReadOnlyList(Of ArmaSSE_AlternateTextures)
            Get
                Return Elementos(Of ArmaSSE_AlternateTextures)("Biped Model\Male\MO2S\Alternate Textures", Function(n) New ArmaSSE_AlternateTextures(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Biped Model\Female\MO3S\Alternate Textures</summary>
        Public ReadOnly Property AlternateTextures2 As IReadOnlyList(Of ArmaSSE_AlternateTextures2)
            Get
                Return Elementos(Of ArmaSSE_AlternateTextures2)("Biped Model\Female\MO3S\Alternate Textures", Function(n) New ArmaSSE_AlternateTextures2(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>1st Person\Male\MO4S\Alternate Textures</summary>
        Public ReadOnly Property AlternateTextures3 As IReadOnlyList(Of ArmaSSE_AlternateTextures3)
            Get
                Return Elementos(Of ArmaSSE_AlternateTextures3)("1st Person\Male\MO4S\Alternate Textures", Function(n) New ArmaSSE_AlternateTextures3(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>1st Person\Female\MO5S\Alternate Textures</summary>
        Public ReadOnly Property AlternateTextures4 As IReadOnlyList(Of ArmaSSE_AlternateTextures4)
            Get
                Return Elementos(Of ArmaSSE_AlternateTextures4)("1st Person\Female\MO5S\Alternate Textures", Function(n) New ArmaSSE_AlternateTextures4(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Additional Races</summary>
        Public ReadOnly Property AdditionalRaces As IReadOnlyList(Of ArmaSSE_AdditionalRaces)
            Get
                Return Elementos(Of ArmaSSE_AdditionalRaces)("Additional Races", Function(n) New ArmaSSE_AdditionalRaces(n, Context, Resolver))
            End Get
        End Property

    End Class

    ''' <summary>Un elemento de Biped Model\Male\MO2S\Alternate Textures.</summary>
    Public NotInheritable Class ArmaSSE_AlternateTextures
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Alternate Texture\New Texture  -&gt;  TXST. Referencia en el espacio del orden de carga.</summary>
        Public Property AlternateTextureNewTexture As UInteger
            Get
                Return Referencia("Alternate Texture\New Texture")
            End Get
            Set(value As UInteger)
                PonerReferencia("Alternate Texture\New Texture", value)
            End Set
        End Property

        ''' <summary>Alternate Texture\3D Index</summary>
        Public Property AlternateTexture3DIndex As Integer
            Get
                Return CInt(Entero("Alternate Texture\3D Index"))
            End Get
            Set(value As Integer)
                Escribir("Alternate Texture\3D Index", CLng(value))
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Biped Model\Female\MO3S\Alternate Textures.</summary>
    Public NotInheritable Class ArmaSSE_AlternateTextures2
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Alternate Texture\New Texture  -&gt;  TXST. Referencia en el espacio del orden de carga.</summary>
        Public Property AlternateTextureNewTexture As UInteger
            Get
                Return Referencia("Alternate Texture\New Texture")
            End Get
            Set(value As UInteger)
                PonerReferencia("Alternate Texture\New Texture", value)
            End Set
        End Property

        ''' <summary>Alternate Texture\3D Index</summary>
        Public Property AlternateTexture3DIndex As Integer
            Get
                Return CInt(Entero("Alternate Texture\3D Index"))
            End Get
            Set(value As Integer)
                Escribir("Alternate Texture\3D Index", CLng(value))
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de 1st Person\Male\MO4S\Alternate Textures.</summary>
    Public NotInheritable Class ArmaSSE_AlternateTextures3
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Alternate Texture\New Texture  -&gt;  TXST. Referencia en el espacio del orden de carga.</summary>
        Public Property AlternateTextureNewTexture As UInteger
            Get
                Return Referencia("Alternate Texture\New Texture")
            End Get
            Set(value As UInteger)
                PonerReferencia("Alternate Texture\New Texture", value)
            End Set
        End Property

        ''' <summary>Alternate Texture\3D Index</summary>
        Public Property AlternateTexture3DIndex As Integer
            Get
                Return CInt(Entero("Alternate Texture\3D Index"))
            End Get
            Set(value As Integer)
                Escribir("Alternate Texture\3D Index", CLng(value))
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de 1st Person\Female\MO5S\Alternate Textures.</summary>
    Public NotInheritable Class ArmaSSE_AlternateTextures4
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Alternate Texture\New Texture  -&gt;  TXST. Referencia en el espacio del orden de carga.</summary>
        Public Property AlternateTextureNewTexture As UInteger
            Get
                Return Referencia("Alternate Texture\New Texture")
            End Get
            Set(value As UInteger)
                PonerReferencia("Alternate Texture\New Texture", value)
            End Set
        End Property

        ''' <summary>Alternate Texture\3D Index</summary>
        Public Property AlternateTexture3DIndex As Integer
            Get
                Return CInt(Entero("Alternate Texture\3D Index"))
            End Get
            Set(value As Integer)
                Escribir("Alternate Texture\3D Index", CLng(value))
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Additional Races.</summary>
    Public NotInheritable Class ArmaSSE_AdditionalRaces
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>MODL\Race  -&gt;  RACE / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property Race As UInteger
            Get
                Return Referencia("MODL\Race")
            End Get
            Set(value As UInteger)
                PonerReferencia("MODL\Race", value)
            End Set
        End Property

    End Class

    ''' <summary>Campos de un record ARMO de Skyrim.</summary>
    Public NotInheritable Class ArmoSSE
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>EDID\Editor ID</summary>
        Public Property EditorID As String
            Get
                Return Txt("EDID\Editor ID")
            End Get
            Set(value As String)
                Escribir("EDID\Editor ID", value)
            End Set
        End Property

        ''' <summary>VMAD\Virtual Machine Adapter\Version</summary>
        Public Property VirtualMachineAdapterVersion As Short
            Get
                Return CShort(Entero("VMAD\Virtual Machine Adapter\Version"))
            End Get
            Set(value As Short)
                Escribir("VMAD\Virtual Machine Adapter\Version", CLng(value))
            End Set
        End Property

        ''' <summary>VMAD\Virtual Machine Adapter\Object Format</summary>
        Public Property VirtualMachineAdapterObjectFormat As Short
            Get
                Return CShort(Entero("VMAD\Virtual Machine Adapter\Object Format"))
            End Get
            Set(value As Short)
                Escribir("VMAD\Virtual Machine Adapter\Object Format", CLng(value))
            End Set
        End Property

        ''' <summary>OBND\Object Bounds\X1</summary>
        Public Property ObjectBoundsX1 As Short
            Get
                Return CShort(Entero("OBND\Object Bounds\X1"))
            End Get
            Set(value As Short)
                Escribir("OBND\Object Bounds\X1", CLng(value))
            End Set
        End Property

        ''' <summary>OBND\Object Bounds\Y1</summary>
        Public Property ObjectBoundsY1 As Short
            Get
                Return CShort(Entero("OBND\Object Bounds\Y1"))
            End Get
            Set(value As Short)
                Escribir("OBND\Object Bounds\Y1", CLng(value))
            End Set
        End Property

        ''' <summary>OBND\Object Bounds\Z1</summary>
        Public Property ObjectBoundsZ1 As Short
            Get
                Return CShort(Entero("OBND\Object Bounds\Z1"))
            End Get
            Set(value As Short)
                Escribir("OBND\Object Bounds\Z1", CLng(value))
            End Set
        End Property

        ''' <summary>OBND\Object Bounds\X2</summary>
        Public Property ObjectBoundsX2 As Short
            Get
                Return CShort(Entero("OBND\Object Bounds\X2"))
            End Get
            Set(value As Short)
                Escribir("OBND\Object Bounds\X2", CLng(value))
            End Set
        End Property

        ''' <summary>OBND\Object Bounds\Y2</summary>
        Public Property ObjectBoundsY2 As Short
            Get
                Return CShort(Entero("OBND\Object Bounds\Y2"))
            End Get
            Set(value As Short)
                Escribir("OBND\Object Bounds\Y2", CLng(value))
            End Set
        End Property

        ''' <summary>OBND\Object Bounds\Z2</summary>
        Public Property ObjectBoundsZ2 As Short
            Get
                Return CShort(Entero("OBND\Object Bounds\Z2"))
            End Get
            Set(value As Short)
                Escribir("OBND\Object Bounds\Z2", CLng(value))
            End Set
        End Property

        ''' <summary>EITM\Enchantment  -&gt;  ENCH. Referencia en el espacio del orden de carga.</summary>
        Public Property Enchantment As UInteger
            Get
                Return Referencia("EITM\Enchantment")
            End Get
            Set(value As UInteger)
                PonerReferencia("EITM\Enchantment", value)
            End Set
        End Property

        ''' <summary>Male\World Model\MOD2\Model Filename</summary>
        Public Property WorldModelModelFilename As String
            Get
                Return Txt("Male\World Model\MOD2\Model Filename")
            End Get
            Set(value As String)
                Escribir("Male\World Model\MOD2\Model Filename", value)
            End Set
        End Property

        ''' <summary>Male\ICON\Icon Image</summary>
        Public Property MaleIconImage As String
            Get
                Return Txt("Male\ICON\Icon Image")
            End Get
            Set(value As String)
                Escribir("Male\ICON\Icon Image", value)
            End Set
        End Property

        ''' <summary>Male\MICO\Message Icon</summary>
        Public Property MaleMessageIcon As String
            Get
                Return Txt("Male\MICO\Message Icon")
            End Get
            Set(value As String)
                Escribir("Male\MICO\Message Icon", value)
            End Set
        End Property

        ''' <summary>Female\World Model\MOD4\Model Filename</summary>
        Public Property WorldModelModelFilename2 As String
            Get
                Return Txt("Female\World Model\MOD4\Model Filename")
            End Get
            Set(value As String)
                Escribir("Female\World Model\MOD4\Model Filename", value)
            End Set
        End Property

        ''' <summary>Female\ICO2\Icon Image</summary>
        Public Property FemaleIconImage As String
            Get
                Return Txt("Female\ICO2\Icon Image")
            End Get
            Set(value As String)
                Escribir("Female\ICO2\Icon Image", value)
            End Set
        End Property

        ''' <summary>Female\MIC2\Message Icon</summary>
        Public Property FemaleMessageIcon As String
            Get
                Return Txt("Female\MIC2\Message Icon")
            End Get
            Set(value As String)
                Escribir("Female\MIC2\Message Icon", value)
            End Set
        End Property

        ''' <summary>BOD2\Biped Body Template\First Person Flags</summary>
        Public Property BipedBodyTemplateFirstPersonFlags As UInteger
            Get
                Return CUInt(Entero("BOD2\Biped Body Template\First Person Flags"))
            End Get
            Set(value As UInteger)
                Escribir("BOD2\Biped Body Template\First Person Flags", CLng(value))
            End Set
        End Property

        ''' <summary>BOD2\Biped Body Template\General Flags</summary>
        Public Property BipedBodyTemplateGeneralFlags As Long
            Get
                Return CLng(Entero("BOD2\Biped Body Template\General Flags"))
            End Get
            Set(value As Long)
                Escribir("BOD2\Biped Body Template\General Flags", CLng(value))
            End Set
        End Property

        ''' <summary>Bit 0 de BOD2\Biped Body Template\General Flags: (ARMA)Modulates Voice</summary>
        Public Property BipedBodyTemplateGeneralFlagsARMAModulatesVoice As Boolean
            Get
                Return Bit("BOD2\Biped Body Template\General Flags", 0)
            End Get
            Set(value As Boolean)
                PonerBit("BOD2\Biped Body Template\General Flags", 0, value)
            End Set
        End Property
        ''' <summary>Bit 4 de BOD2\Biped Body Template\General Flags: (ARMO)Non-Playable</summary>
        Public Property BipedBodyTemplateGeneralFlagsARMONonPlayable As Boolean
            Get
                Return Bit("BOD2\Biped Body Template\General Flags", 4)
            End Get
            Set(value As Boolean)
                PonerBit("BOD2\Biped Body Template\General Flags", 4, value)
            End Set
        End Property

        ''' <summary>BOD2\Biped Body Template\Armor Type</summary>
        Public Property BipedBodyTemplateArmorType As UInteger
            Get
                Return CUInt(Entero("BOD2\Biped Body Template\Armor Type"))
            End Get
            Set(value As UInteger)
                Escribir("BOD2\Biped Body Template\Armor Type", CLng(value))
            End Set
        End Property

        ''' <summary>Destructible\DEST\Header\Health</summary>
        Public Property HeaderHealth As Integer
            Get
                Return CInt(Entero("Destructible\DEST\Header\Health"))
            End Get
            Set(value As Integer)
                Escribir("Destructible\DEST\Header\Health", CLng(value))
            End Set
        End Property

        ''' <summary>Destructible\DEST\Header\DEST Count</summary>
        Public Property HeaderDESTCount As Byte
            Get
                Return CByte(Entero("Destructible\DEST\Header\DEST Count"))
            End Get
            Set(value As Byte)
                Escribir("Destructible\DEST\Header\DEST Count", CLng(value))
            End Set
        End Property

        ''' <summary>Destructible\DEST\Header\VATS Targetable</summary>
        Public Property HeaderVATSTargetable As Byte
            Get
                Return CByte(Entero("Destructible\DEST\Header\VATS Targetable"))
            End Get
            Set(value As Byte)
                Escribir("Destructible\DEST\Header\VATS Targetable", CLng(value))
            End Set
        End Property

        ''' <summary>YNAM\Sound - Pick Up  -&gt;  SNDR. Referencia en el espacio del orden de carga.</summary>
        Public Property SoundPickUp As UInteger
            Get
                Return Referencia("YNAM\Sound - Pick Up")
            End Get
            Set(value As UInteger)
                PonerReferencia("YNAM\Sound - Pick Up", value)
            End Set
        End Property

        ''' <summary>ZNAM\Sound - Put Down  -&gt;  SNDR. Referencia en el espacio del orden de carga.</summary>
        Public Property SoundPutDown As UInteger
            Get
                Return Referencia("ZNAM\Sound - Put Down")
            End Get
            Set(value As UInteger)
                PonerReferencia("ZNAM\Sound - Put Down", value)
            End Set
        End Property

        ''' <summary>BMCT\Ragdoll Constraint Template</summary>
        Public Property RagdollConstraintTemplate As String
            Get
                Return Txt("BMCT\Ragdoll Constraint Template")
            End Get
            Set(value As String)
                Escribir("BMCT\Ragdoll Constraint Template", value)
            End Set
        End Property

        ''' <summary>ETYP\Equipment Type  -&gt;  EQUP / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property EquipmentType As UInteger
            Get
                Return Referencia("ETYP\Equipment Type")
            End Get
            Set(value As UInteger)
                PonerReferencia("ETYP\Equipment Type", value)
            End Set
        End Property

        ''' <summary>BIDS\Bash Impact Data Set  -&gt;  IPDS. Referencia en el espacio del orden de carga.</summary>
        Public Property BashImpactDataSet As UInteger
            Get
                Return Referencia("BIDS\Bash Impact Data Set")
            End Get
            Set(value As UInteger)
                PonerReferencia("BIDS\Bash Impact Data Set", value)
            End Set
        End Property

        ''' <summary>BAMT\Alternate Block Material  -&gt;  MATT. Referencia en el espacio del orden de carga.</summary>
        Public Property AlternateBlockMaterial As UInteger
            Get
                Return Referencia("BAMT\Alternate Block Material")
            End Get
            Set(value As UInteger)
                PonerReferencia("BAMT\Alternate Block Material", value)
            End Set
        End Property

        ''' <summary>RNAM\Race  -&gt;  RACE. Referencia en el espacio del orden de carga.</summary>
        Public Property Race As UInteger
            Get
                Return Referencia("RNAM\Race")
            End Get
            Set(value As UInteger)
                PonerReferencia("RNAM\Race", value)
            End Set
        End Property

        ''' <summary>Keywords\KSIZ\Keyword Count</summary>
        Public Property KeywordsKeywordCount As UInteger
            Get
                Return CUInt(Entero("Keywords\KSIZ\Keyword Count"))
            End Get
            Set(value As UInteger)
                Escribir("Keywords\KSIZ\Keyword Count", CLng(value))
            End Set
        End Property

        ''' <summary>DATA\Data\Value</summary>
        Public Property DataValue As Integer
            Get
                Return CInt(Entero("DATA\Data\Value"))
            End Get
            Set(value As Integer)
                Escribir("DATA\Data\Value", CLng(value))
            End Set
        End Property

        ''' <summary>DATA\Data\Weight</summary>
        Public Property DataWeight As Single
            Get
                Return Flt("DATA\Data\Weight")
            End Get
            Set(value As Single)
                Escribir("DATA\Data\Weight", value)
            End Set
        End Property

        ''' <summary>DNAM\Armor Rating</summary>
        Public Property ArmorRating As Integer
            Get
                Return CInt(Entero("DNAM\Armor Rating"))
            End Get
            Set(value As Integer)
                Escribir("DNAM\Armor Rating", CLng(value))
            End Set
        End Property

        ''' <summary>TNAM\Template Armor  -&gt;  ARMO. Referencia en el espacio del orden de carga.</summary>
        Public Property TemplateArmor As UInteger
            Get
                Return Referencia("TNAM\Template Armor")
            End Get
            Set(value As UInteger)
                PonerReferencia("TNAM\Template Armor", value)
            End Set
        End Property

        ''' <summary>VMAD\Virtual Machine Adapter\Scripts</summary>
        Public ReadOnly Property Scripts As IReadOnlyList(Of ArmoSSE_Scripts)
            Get
                Return Elementos(Of ArmoSSE_Scripts)("VMAD\Virtual Machine Adapter\Scripts", Function(n) New ArmoSSE_Scripts(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>VMAD\Virtual Machine Adapter\Scripts\Script\Properties</summary>
        Public ReadOnly Property Properties As IReadOnlyList(Of ArmoSSE_Properties)
            Get
                Return Elementos(Of ArmoSSE_Properties)("VMAD\Virtual Machine Adapter\Scripts\Script\Properties", Function(n) New ArmoSSE_Properties(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Male\World Model\MO2S\Alternate Textures</summary>
        Public ReadOnly Property AlternateTextures As IReadOnlyList(Of ArmoSSE_AlternateTextures)
            Get
                Return Elementos(Of ArmoSSE_AlternateTextures)("Male\World Model\MO2S\Alternate Textures", Function(n) New ArmoSSE_AlternateTextures(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Female\World Model\MO4S\Alternate Textures</summary>
        Public ReadOnly Property AlternateTextures2 As IReadOnlyList(Of ArmoSSE_AlternateTextures2)
            Get
                Return Elementos(Of ArmoSSE_AlternateTextures2)("Female\World Model\MO4S\Alternate Textures", Function(n) New ArmoSSE_AlternateTextures2(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Destructible\Stages</summary>
        Public ReadOnly Property Stages As IReadOnlyList(Of ArmoSSE_Stages)
            Get
                Return Elementos(Of ArmoSSE_Stages)("Destructible\Stages", Function(n) New ArmoSSE_Stages(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Destructible\Stages\Stage\Model\DMDS\Alternate Textures</summary>
        Public ReadOnly Property AlternateTextures3 As IReadOnlyList(Of ArmoSSE_AlternateTextures3)
            Get
                Return Elementos(Of ArmoSSE_AlternateTextures3)("Destructible\Stages\Stage\Model\DMDS\Alternate Textures", Function(n) New ArmoSSE_AlternateTextures3(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Keywords\KWDA\Keywords</summary>
        Public ReadOnly Property Keywords As IReadOnlyList(Of ArmoSSE_Keywords)
            Get
                Return Elementos(Of ArmoSSE_Keywords)("Keywords\KWDA\Keywords", Function(n) New ArmoSSE_Keywords(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Armature</summary>
        Public ReadOnly Property Armature As IReadOnlyList(Of ArmoSSE_Armature)
            Get
                Return Elementos(Of ArmoSSE_Armature)("Armature", Function(n) New ArmoSSE_Armature(n, Context, Resolver))
            End Get
        End Property

    End Class

    ''' <summary>Un elemento de VMAD\Virtual Machine Adapter\Scripts.</summary>
    Public NotInheritable Class ArmoSSE_Scripts
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Script\Flags</summary>
        Public Property ScriptFlags As Byte
            Get
                Return CByte(Entero("Script\Flags"))
            End Get
            Set(value As Byte)
                Escribir("Script\Flags", CLng(value))
            End Set
        End Property
        ''' <summary>Nombre del valor de Script\Flags.</summary>
        Public ReadOnly Property ScriptFlagsNombre As String
            Get
                Return NombreDeValor("Script\Flags")
            End Get
        End Property


    End Class

    ''' <summary>Un elemento de VMAD\Virtual Machine Adapter\Scripts\Script\Properties.</summary>
    Public NotInheritable Class ArmoSSE_Properties
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Property\Type</summary>
        Public Property PropertyType As Byte
            Get
                Return CByte(Entero("Property\Type"))
            End Get
            Set(value As Byte)
                Escribir("Property\Type", CLng(value))
            End Set
        End Property

        ''' <summary>Property\Flags</summary>
        Public Property PropertyFlags As Byte
            Get
                Return CByte(Entero("Property\Flags"))
            End Get
            Set(value As Byte)
                Escribir("Property\Flags", CLng(value))
            End Set
        End Property
        ''' <summary>Nombre del valor de Property\Flags.</summary>
        Public ReadOnly Property PropertyFlagsNombre As String
            Get
                Return NombreDeValor("Property\Flags")
            End Get
        End Property


        ''' <summary>Property\Object v2\Alias</summary>
        Public Property ObjectV2Alias As Short
            Get
                Return CShort(Entero("Property\Object v2\Alias"))
            End Get
            Set(value As Short)
                Escribir("Property\Object v2\Alias", CLng(value))
            End Set
        End Property

        ''' <summary>Property\Object v2\FormID. Referencia en el espacio del orden de carga.</summary>
        Public Property ObjectV2FormID As UInteger
            Get
                Return Referencia("Property\Object v2\FormID")
            End Get
            Set(value As UInteger)
                PonerReferencia("Property\Object v2\FormID", value)
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Male\World Model\MO2S\Alternate Textures.</summary>
    Public NotInheritable Class ArmoSSE_AlternateTextures
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Alternate Texture\New Texture  -&gt;  TXST. Referencia en el espacio del orden de carga.</summary>
        Public Property AlternateTextureNewTexture As UInteger
            Get
                Return Referencia("Alternate Texture\New Texture")
            End Get
            Set(value As UInteger)
                PonerReferencia("Alternate Texture\New Texture", value)
            End Set
        End Property

        ''' <summary>Alternate Texture\3D Index</summary>
        Public Property AlternateTexture3DIndex As Integer
            Get
                Return CInt(Entero("Alternate Texture\3D Index"))
            End Get
            Set(value As Integer)
                Escribir("Alternate Texture\3D Index", CLng(value))
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Female\World Model\MO4S\Alternate Textures.</summary>
    Public NotInheritable Class ArmoSSE_AlternateTextures2
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Alternate Texture\New Texture  -&gt;  TXST. Referencia en el espacio del orden de carga.</summary>
        Public Property AlternateTextureNewTexture As UInteger
            Get
                Return Referencia("Alternate Texture\New Texture")
            End Get
            Set(value As UInteger)
                PonerReferencia("Alternate Texture\New Texture", value)
            End Set
        End Property

        ''' <summary>Alternate Texture\3D Index</summary>
        Public Property AlternateTexture3DIndex As Integer
            Get
                Return CInt(Entero("Alternate Texture\3D Index"))
            End Get
            Set(value As Integer)
                Escribir("Alternate Texture\3D Index", CLng(value))
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Destructible\Stages.</summary>
    Public NotInheritable Class ArmoSSE_Stages
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Stage\DSTD\Destruction Stage Data\Health %</summary>
        Public Property DestructionStageDataHealth As Byte
            Get
                Return CByte(Entero("Stage\DSTD\Destruction Stage Data\Health %"))
            End Get
            Set(value As Byte)
                Escribir("Stage\DSTD\Destruction Stage Data\Health %", CLng(value))
            End Set
        End Property

        ''' <summary>Stage\DSTD\Destruction Stage Data\Index</summary>
        Public Property DestructionStageDataIndex As Byte
            Get
                Return CByte(Entero("Stage\DSTD\Destruction Stage Data\Index"))
            End Get
            Set(value As Byte)
                Escribir("Stage\DSTD\Destruction Stage Data\Index", CLng(value))
            End Set
        End Property

        ''' <summary>Stage\DSTD\Destruction Stage Data\Model Damage Stage</summary>
        Public Property DestructionStageDataModelDamageStage As Byte
            Get
                Return CByte(Entero("Stage\DSTD\Destruction Stage Data\Model Damage Stage"))
            End Get
            Set(value As Byte)
                Escribir("Stage\DSTD\Destruction Stage Data\Model Damage Stage", CLng(value))
            End Set
        End Property

        ''' <summary>Stage\DSTD\Destruction Stage Data\Flags</summary>
        Public Property DestructionStageDataFlags As Byte
            Get
                Return CByte(Entero("Stage\DSTD\Destruction Stage Data\Flags"))
            End Get
            Set(value As Byte)
                Escribir("Stage\DSTD\Destruction Stage Data\Flags", CLng(value))
            End Set
        End Property

        ''' <summary>Bit 0 de Stage\DSTD\Destruction Stage Data\Flags: Cap Damage</summary>
        Public Property DestructionStageDataFlagsCapDamage As Boolean
            Get
                Return Bit("Stage\DSTD\Destruction Stage Data\Flags", 0)
            End Get
            Set(value As Boolean)
                PonerBit("Stage\DSTD\Destruction Stage Data\Flags", 0, value)
            End Set
        End Property
        ''' <summary>Bit 1 de Stage\DSTD\Destruction Stage Data\Flags: Disable</summary>
        Public Property DestructionStageDataFlagsDisable As Boolean
            Get
                Return Bit("Stage\DSTD\Destruction Stage Data\Flags", 1)
            End Get
            Set(value As Boolean)
                PonerBit("Stage\DSTD\Destruction Stage Data\Flags", 1, value)
            End Set
        End Property
        ''' <summary>Bit 2 de Stage\DSTD\Destruction Stage Data\Flags: Destroy</summary>
        Public Property DestructionStageDataFlagsDestroy As Boolean
            Get
                Return Bit("Stage\DSTD\Destruction Stage Data\Flags", 2)
            End Get
            Set(value As Boolean)
                PonerBit("Stage\DSTD\Destruction Stage Data\Flags", 2, value)
            End Set
        End Property
        ''' <summary>Bit 3 de Stage\DSTD\Destruction Stage Data\Flags: Ignore External Dmg</summary>
        Public Property DestructionStageDataFlagsIgnoreExternalDmg As Boolean
            Get
                Return Bit("Stage\DSTD\Destruction Stage Data\Flags", 3)
            End Get
            Set(value As Boolean)
                PonerBit("Stage\DSTD\Destruction Stage Data\Flags", 3, value)
            End Set
        End Property

        ''' <summary>Stage\DSTD\Destruction Stage Data\Self Damage per Second</summary>
        Public Property DestructionStageDataSelfDamagePerSecond As Integer
            Get
                Return CInt(Entero("Stage\DSTD\Destruction Stage Data\Self Damage per Second"))
            End Get
            Set(value As Integer)
                Escribir("Stage\DSTD\Destruction Stage Data\Self Damage per Second", CLng(value))
            End Set
        End Property

        ''' <summary>Stage\DSTD\Destruction Stage Data\Explosion  -&gt;  EXPL / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property DestructionStageDataExplosion As UInteger
            Get
                Return Referencia("Stage\DSTD\Destruction Stage Data\Explosion")
            End Get
            Set(value As UInteger)
                PonerReferencia("Stage\DSTD\Destruction Stage Data\Explosion", value)
            End Set
        End Property

        ''' <summary>Stage\DSTD\Destruction Stage Data\Debris  -&gt;  DEBR / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property DestructionStageDataDebris As UInteger
            Get
                Return Referencia("Stage\DSTD\Destruction Stage Data\Debris")
            End Get
            Set(value As UInteger)
                PonerReferencia("Stage\DSTD\Destruction Stage Data\Debris", value)
            End Set
        End Property

        ''' <summary>Stage\DSTD\Destruction Stage Data\Debris Count</summary>
        Public Property DestructionStageDataDebrisCount As Integer
            Get
                Return CInt(Entero("Stage\DSTD\Destruction Stage Data\Debris Count"))
            End Get
            Set(value As Integer)
                Escribir("Stage\DSTD\Destruction Stage Data\Debris Count", CLng(value))
            End Set
        End Property

        ''' <summary>Stage\Model\DMDL\Model FileName</summary>
        Public Property ModelModelFileName As String
            Get
                Return Txt("Stage\Model\DMDL\Model FileName")
            End Get
            Set(value As String)
                Escribir("Stage\Model\DMDL\Model FileName", value)
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Destructible\Stages\Stage\Model\DMDS\Alternate Textures.</summary>
    Public NotInheritable Class ArmoSSE_AlternateTextures3
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Alternate Texture\New Texture  -&gt;  TXST. Referencia en el espacio del orden de carga.</summary>
        Public Property AlternateTextureNewTexture As UInteger
            Get
                Return Referencia("Alternate Texture\New Texture")
            End Get
            Set(value As UInteger)
                PonerReferencia("Alternate Texture\New Texture", value)
            End Set
        End Property

        ''' <summary>Alternate Texture\3D Index</summary>
        Public Property AlternateTexture3DIndex As Integer
            Get
                Return CInt(Entero("Alternate Texture\3D Index"))
            End Get
            Set(value As Integer)
                Escribir("Alternate Texture\3D Index", CLng(value))
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Keywords\KWDA\Keywords.</summary>
    Public NotInheritable Class ArmoSSE_Keywords
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Keyword  -&gt;  KYWD / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property Keyword As UInteger
            Get
                Return Referencia("Keyword")
            End Get
            Set(value As UInteger)
                PonerReferencia("Keyword", value)
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Armature.</summary>
    Public NotInheritable Class ArmoSSE_Armature
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>MODL\Model Filename  -&gt;  ARMA / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property ModelFilename As UInteger
            Get
                Return Referencia("MODL\Model Filename")
            End Get
            Set(value As UInteger)
                PonerReferencia("MODL\Model Filename", value)
            End Set
        End Property

    End Class

    ''' <summary>Campos de un record BPTD de Skyrim.</summary>
    Public NotInheritable Class BptdSSE
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>EDID\Editor ID</summary>
        Public Property EditorID As String
            Get
                Return Txt("EDID\Editor ID")
            End Get
            Set(value As String)
                Escribir("EDID\Editor ID", value)
            End Set
        End Property

        ''' <summary>Model\MODL\Model FileName</summary>
        Public Property ModelModelFileName As String
            Get
                Return Txt("Model\MODL\Model FileName")
            End Get
            Set(value As String)
                Escribir("Model\MODL\Model FileName", value)
            End Set
        End Property

        ''' <summary>Model\MODS\Alternate Textures</summary>
        Public ReadOnly Property AlternateTextures As IReadOnlyList(Of BptdSSE_AlternateTextures)
            Get
                Return Elementos(Of BptdSSE_AlternateTextures)("Model\MODS\Alternate Textures", Function(n) New BptdSSE_AlternateTextures(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Body Parts</summary>
        Public ReadOnly Property BodyParts As IReadOnlyList(Of BptdSSE_BodyParts)
            Get
                Return Elementos(Of BptdSSE_BodyParts)("Body Parts", Function(n) New BptdSSE_BodyParts(n, Context, Resolver))
            End Get
        End Property

    End Class

    ''' <summary>Un elemento de Model\MODS\Alternate Textures.</summary>
    Public NotInheritable Class BptdSSE_AlternateTextures
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Alternate Texture\New Texture  -&gt;  TXST. Referencia en el espacio del orden de carga.</summary>
        Public Property AlternateTextureNewTexture As UInteger
            Get
                Return Referencia("Alternate Texture\New Texture")
            End Get
            Set(value As UInteger)
                PonerReferencia("Alternate Texture\New Texture", value)
            End Set
        End Property

        ''' <summary>Alternate Texture\3D Index</summary>
        Public Property AlternateTexture3DIndex As Integer
            Get
                Return CInt(Entero("Alternate Texture\3D Index"))
            End Get
            Set(value As Integer)
                Escribir("Alternate Texture\3D Index", CLng(value))
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Body Parts.</summary>
    Public NotInheritable Class BptdSSE_BodyParts
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Body Part\PNAM\Pose Matching</summary>
        Public Property BodyPartPoseMatching As String
            Get
                Return Txt("Body Part\PNAM\Pose Matching")
            End Get
            Set(value As String)
                Escribir("Body Part\PNAM\Pose Matching", value)
            End Set
        End Property

        ''' <summary>Body Part\BPNN\Part Node</summary>
        Public Property BodyPartPartNode As String
            Get
                Return Txt("Body Part\BPNN\Part Node")
            End Get
            Set(value As String)
                Escribir("Body Part\BPNN\Part Node", value)
            End Set
        End Property

        ''' <summary>Body Part\BPNT\VATS Target</summary>
        Public Property BodyPartVATSTarget As String
            Get
                Return Txt("Body Part\BPNT\VATS Target")
            End Get
            Set(value As String)
                Escribir("Body Part\BPNT\VATS Target", value)
            End Set
        End Property

        ''' <summary>Body Part\BPNI\IK Data - Start Node</summary>
        Public Property BodyPartIKDataStartNode As String
            Get
                Return Txt("Body Part\BPNI\IK Data - Start Node")
            End Get
            Set(value As String)
                Escribir("Body Part\BPNI\IK Data - Start Node", value)
            End Set
        End Property

        ''' <summary>Body Part\BPND\Node Data\Damage Mult</summary>
        Public Property NodeDataDamageMult As Single
            Get
                Return Flt("Body Part\BPND\Node Data\Damage Mult")
            End Get
            Set(value As Single)
                Escribir("Body Part\BPND\Node Data\Damage Mult", value)
            End Set
        End Property

        ''' <summary>Body Part\BPND\Node Data\Flags</summary>
        Public Property NodeDataFlags As Byte
            Get
                Return CByte(Entero("Body Part\BPND\Node Data\Flags"))
            End Get
            Set(value As Byte)
                Escribir("Body Part\BPND\Node Data\Flags", CLng(value))
            End Set
        End Property

        ''' <summary>Bit 0 de Body Part\BPND\Node Data\Flags: Severable</summary>
        Public Property NodeDataFlagsSeverable As Boolean
            Get
                Return Bit("Body Part\BPND\Node Data\Flags", 0)
            End Get
            Set(value As Boolean)
                PonerBit("Body Part\BPND\Node Data\Flags", 0, value)
            End Set
        End Property
        ''' <summary>Bit 1 de Body Part\BPND\Node Data\Flags: IK Data</summary>
        Public Property NodeDataFlagsIKData As Boolean
            Get
                Return Bit("Body Part\BPND\Node Data\Flags", 1)
            End Get
            Set(value As Boolean)
                PonerBit("Body Part\BPND\Node Data\Flags", 1, value)
            End Set
        End Property
        ''' <summary>Bit 2 de Body Part\BPND\Node Data\Flags: IK Data - Biped Data</summary>
        Public Property NodeDataFlagsIKDataBipedData As Boolean
            Get
                Return Bit("Body Part\BPND\Node Data\Flags", 2)
            End Get
            Set(value As Boolean)
                PonerBit("Body Part\BPND\Node Data\Flags", 2, value)
            End Set
        End Property
        ''' <summary>Bit 3 de Body Part\BPND\Node Data\Flags: Explodable</summary>
        Public Property NodeDataFlagsExplodable As Boolean
            Get
                Return Bit("Body Part\BPND\Node Data\Flags", 3)
            End Get
            Set(value As Boolean)
                PonerBit("Body Part\BPND\Node Data\Flags", 3, value)
            End Set
        End Property
        ''' <summary>Bit 4 de Body Part\BPND\Node Data\Flags: IK Data - Is Head</summary>
        Public Property NodeDataFlagsIKDataIsHead As Boolean
            Get
                Return Bit("Body Part\BPND\Node Data\Flags", 4)
            End Get
            Set(value As Boolean)
                PonerBit("Body Part\BPND\Node Data\Flags", 4, value)
            End Set
        End Property
        ''' <summary>Bit 5 de Body Part\BPND\Node Data\Flags: IK Data - Headtracking</summary>
        Public Property NodeDataFlagsIKDataHeadtracking As Boolean
            Get
                Return Bit("Body Part\BPND\Node Data\Flags", 5)
            End Get
            Set(value As Boolean)
                PonerBit("Body Part\BPND\Node Data\Flags", 5, value)
            End Set
        End Property
        ''' <summary>Bit 6 de Body Part\BPND\Node Data\Flags: To Hit Chance - Absolute</summary>
        Public Property NodeDataFlagsToHitChanceAbsolute As Boolean
            Get
                Return Bit("Body Part\BPND\Node Data\Flags", 6)
            End Get
            Set(value As Boolean)
                PonerBit("Body Part\BPND\Node Data\Flags", 6, value)
            End Set
        End Property

        ''' <summary>Body Part\BPND\Node Data\Part Type</summary>
        Public Property NodeDataPartType As Byte
            Get
                Return CByte(Entero("Body Part\BPND\Node Data\Part Type"))
            End Get
            Set(value As Byte)
                Escribir("Body Part\BPND\Node Data\Part Type", CLng(value))
            End Set
        End Property
        ''' <summary>Nombre del valor de Body Part\BPND\Node Data\Part Type.</summary>
        Public ReadOnly Property NodeDataPartTypeNombre As String
            Get
                Return NombreDeValor("Body Part\BPND\Node Data\Part Type")
            End Get
        End Property


        ''' <summary>Body Part\BPND\Node Data\Health Percent</summary>
        Public Property NodeDataHealthPercent As Byte
            Get
                Return CByte(Entero("Body Part\BPND\Node Data\Health Percent"))
            End Get
            Set(value As Byte)
                Escribir("Body Part\BPND\Node Data\Health Percent", CLng(value))
            End Set
        End Property

        ''' <summary>Body Part\BPND\Node Data\Actor Value</summary>
        Public Property NodeDataActorValue As SByte
            Get
                Return CSByte(Entero("Body Part\BPND\Node Data\Actor Value"))
            End Get
            Set(value As SByte)
                Escribir("Body Part\BPND\Node Data\Actor Value", CLng(value))
            End Set
        End Property

        ''' <summary>Body Part\BPND\Node Data\To Hit Chance</summary>
        Public Property NodeDataToHitChance As Byte
            Get
                Return CByte(Entero("Body Part\BPND\Node Data\To Hit Chance"))
            End Get
            Set(value As Byte)
                Escribir("Body Part\BPND\Node Data\To Hit Chance", CLng(value))
            End Set
        End Property

        ''' <summary>Body Part\BPND\Node Data\Explodable - Explosion Chance %</summary>
        Public Property NodeDataExplodableExplosionChance As Byte
            Get
                Return CByte(Entero("Body Part\BPND\Node Data\Explodable - Explosion Chance %"))
            End Get
            Set(value As Byte)
                Escribir("Body Part\BPND\Node Data\Explodable - Explosion Chance %", CLng(value))
            End Set
        End Property

        ''' <summary>Body Part\BPND\Node Data\Explodable - Debris Count</summary>
        Public Property NodeDataExplodableDebrisCount As UShort
            Get
                Return CUShort(Entero("Body Part\BPND\Node Data\Explodable - Debris Count"))
            End Get
            Set(value As UShort)
                Escribir("Body Part\BPND\Node Data\Explodable - Debris Count", CLng(value))
            End Set
        End Property

        ''' <summary>Body Part\BPND\Node Data\Explodable - Debris  -&gt;  DEBR / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property NodeDataExplodableDebris As UInteger
            Get
                Return Referencia("Body Part\BPND\Node Data\Explodable - Debris")
            End Get
            Set(value As UInteger)
                PonerReferencia("Body Part\BPND\Node Data\Explodable - Debris", value)
            End Set
        End Property

        ''' <summary>Body Part\BPND\Node Data\Explodable - Explosion  -&gt;  EXPL / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property NodeDataExplodableExplosion As UInteger
            Get
                Return Referencia("Body Part\BPND\Node Data\Explodable - Explosion")
            End Get
            Set(value As UInteger)
                PonerReferencia("Body Part\BPND\Node Data\Explodable - Explosion", value)
            End Set
        End Property

        ''' <summary>Body Part\BPND\Node Data\Tracking Max Angle</summary>
        Public Property NodeDataTrackingMaxAngle As Single
            Get
                Return Flt("Body Part\BPND\Node Data\Tracking Max Angle")
            End Get
            Set(value As Single)
                Escribir("Body Part\BPND\Node Data\Tracking Max Angle", value)
            End Set
        End Property

        ''' <summary>Body Part\BPND\Node Data\Explodable - Debris Scale</summary>
        Public Property NodeDataExplodableDebrisScale As Single
            Get
                Return Flt("Body Part\BPND\Node Data\Explodable - Debris Scale")
            End Get
            Set(value As Single)
                Escribir("Body Part\BPND\Node Data\Explodable - Debris Scale", value)
            End Set
        End Property

        ''' <summary>Body Part\BPND\Node Data\Severable - Debris Count</summary>
        Public Property NodeDataSeverableDebrisCount As Integer
            Get
                Return CInt(Entero("Body Part\BPND\Node Data\Severable - Debris Count"))
            End Get
            Set(value As Integer)
                Escribir("Body Part\BPND\Node Data\Severable - Debris Count", CLng(value))
            End Set
        End Property

        ''' <summary>Body Part\BPND\Node Data\Severable - Debris  -&gt;  DEBR / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property NodeDataSeverableDebris As UInteger
            Get
                Return Referencia("Body Part\BPND\Node Data\Severable - Debris")
            End Get
            Set(value As UInteger)
                PonerReferencia("Body Part\BPND\Node Data\Severable - Debris", value)
            End Set
        End Property

        ''' <summary>Body Part\BPND\Node Data\Severable - Explosion  -&gt;  EXPL / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property NodeDataSeverableExplosion As UInteger
            Get
                Return Referencia("Body Part\BPND\Node Data\Severable - Explosion")
            End Get
            Set(value As UInteger)
                PonerReferencia("Body Part\BPND\Node Data\Severable - Explosion", value)
            End Set
        End Property

        ''' <summary>Body Part\BPND\Node Data\Severable - Debris Scale</summary>
        Public Property NodeDataSeverableDebrisScale As Single
            Get
                Return Flt("Body Part\BPND\Node Data\Severable - Debris Scale")
            End Get
            Set(value As Single)
                Escribir("Body Part\BPND\Node Data\Severable - Debris Scale", value)
            End Set
        End Property

        ''' <summary>Body Part\BPND\Node Data\Gore Effects Positioning\Position\X</summary>
        Public Property PositionX As Single
            Get
                Return Flt("Body Part\BPND\Node Data\Gore Effects Positioning\Position\X")
            End Get
            Set(value As Single)
                Escribir("Body Part\BPND\Node Data\Gore Effects Positioning\Position\X", value)
            End Set
        End Property

        ''' <summary>Body Part\BPND\Node Data\Gore Effects Positioning\Position\Y</summary>
        Public Property PositionY As Single
            Get
                Return Flt("Body Part\BPND\Node Data\Gore Effects Positioning\Position\Y")
            End Get
            Set(value As Single)
                Escribir("Body Part\BPND\Node Data\Gore Effects Positioning\Position\Y", value)
            End Set
        End Property

        ''' <summary>Body Part\BPND\Node Data\Gore Effects Positioning\Position\Z</summary>
        Public Property PositionZ As Single
            Get
                Return Flt("Body Part\BPND\Node Data\Gore Effects Positioning\Position\Z")
            End Get
            Set(value As Single)
                Escribir("Body Part\BPND\Node Data\Gore Effects Positioning\Position\Z", value)
            End Set
        End Property

        ''' <summary>Body Part\BPND\Node Data\Gore Effects Positioning\Rotation\X</summary>
        Public Property RotationX As Single
            Get
                Return Flt("Body Part\BPND\Node Data\Gore Effects Positioning\Rotation\X")
            End Get
            Set(value As Single)
                Escribir("Body Part\BPND\Node Data\Gore Effects Positioning\Rotation\X", value)
            End Set
        End Property

        ''' <summary>Body Part\BPND\Node Data\Gore Effects Positioning\Rotation\Y</summary>
        Public Property RotationY As Single
            Get
                Return Flt("Body Part\BPND\Node Data\Gore Effects Positioning\Rotation\Y")
            End Get
            Set(value As Single)
                Escribir("Body Part\BPND\Node Data\Gore Effects Positioning\Rotation\Y", value)
            End Set
        End Property

        ''' <summary>Body Part\BPND\Node Data\Gore Effects Positioning\Rotation\Z</summary>
        Public Property RotationZ As Single
            Get
                Return Flt("Body Part\BPND\Node Data\Gore Effects Positioning\Rotation\Z")
            End Get
            Set(value As Single)
                Escribir("Body Part\BPND\Node Data\Gore Effects Positioning\Rotation\Z", value)
            End Set
        End Property

        ''' <summary>Body Part\BPND\Node Data\Severable - Impact DataSet  -&gt;  IPDS / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property NodeDataSeverableImpactDataSet As UInteger
            Get
                Return Referencia("Body Part\BPND\Node Data\Severable - Impact DataSet")
            End Get
            Set(value As UInteger)
                PonerReferencia("Body Part\BPND\Node Data\Severable - Impact DataSet", value)
            End Set
        End Property

        ''' <summary>Body Part\BPND\Node Data\Explodable - Impact DataSet  -&gt;  IPDS / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property NodeDataExplodableImpactDataSet As UInteger
            Get
                Return Referencia("Body Part\BPND\Node Data\Explodable - Impact DataSet")
            End Get
            Set(value As UInteger)
                PonerReferencia("Body Part\BPND\Node Data\Explodable - Impact DataSet", value)
            End Set
        End Property

        ''' <summary>Body Part\BPND\Node Data\Severable - Decal Count</summary>
        Public Property NodeDataSeverableDecalCount As Byte
            Get
                Return CByte(Entero("Body Part\BPND\Node Data\Severable - Decal Count"))
            End Get
            Set(value As Byte)
                Escribir("Body Part\BPND\Node Data\Severable - Decal Count", CLng(value))
            End Set
        End Property

        ''' <summary>Body Part\BPND\Node Data\Explodable - Decal Count</summary>
        Public Property NodeDataExplodableDecalCount As Byte
            Get
                Return CByte(Entero("Body Part\BPND\Node Data\Explodable - Decal Count"))
            End Get
            Set(value As Byte)
                Escribir("Body Part\BPND\Node Data\Explodable - Decal Count", CLng(value))
            End Set
        End Property

        ''' <summary>Body Part\BPND\Node Data\Limb Replacement Scale</summary>
        Public Property NodeDataLimbReplacementScale As Single
            Get
                Return Flt("Body Part\BPND\Node Data\Limb Replacement Scale")
            End Get
            Set(value As Single)
                Escribir("Body Part\BPND\Node Data\Limb Replacement Scale", value)
            End Set
        End Property

        ''' <summary>Body Part\NAM1\Limb Replacement Model</summary>
        Public Property BodyPartLimbReplacementModel As String
            Get
                Return Txt("Body Part\NAM1\Limb Replacement Model")
            End Get
            Set(value As String)
                Escribir("Body Part\NAM1\Limb Replacement Model", value)
            End Set
        End Property

        ''' <summary>Body Part\NAM4\Gore Effects - Target Bone</summary>
        Public Property BodyPartGoreEffectsTargetBone As String
            Get
                Return Txt("Body Part\NAM4\Gore Effects - Target Bone")
            End Get
            Set(value As String)
                Escribir("Body Part\NAM4\Gore Effects - Target Bone", value)
            End Set
        End Property

    End Class

    ''' <summary>Campos de un record CLFM de Skyrim.</summary>
    Public NotInheritable Class ClfmSSE
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>EDID\Editor ID</summary>
        Public Property EditorID As String
            Get
                Return Txt("EDID\Editor ID")
            End Get
            Set(value As String)
                Escribir("EDID\Editor ID", value)
            End Set
        End Property

        ''' <summary>CNAM\Color\Red</summary>
        Public Property ColorRed As Byte
            Get
                Return CByte(Entero("CNAM\Color\Red"))
            End Get
            Set(value As Byte)
                Escribir("CNAM\Color\Red", CLng(value))
            End Set
        End Property

        ''' <summary>CNAM\Color\Green</summary>
        Public Property ColorGreen As Byte
            Get
                Return CByte(Entero("CNAM\Color\Green"))
            End Get
            Set(value As Byte)
                Escribir("CNAM\Color\Green", CLng(value))
            End Set
        End Property

        ''' <summary>CNAM\Color\Blue</summary>
        Public Property ColorBlue As Byte
            Get
                Return CByte(Entero("CNAM\Color\Blue"))
            End Get
            Set(value As Byte)
                Escribir("CNAM\Color\Blue", CLng(value))
            End Set
        End Property

        ''' <summary>CNAM\Color\Alpha</summary>
        Public Property ColorAlpha As Byte
            Get
                Return CByte(Entero("CNAM\Color\Alpha"))
            End Get
            Set(value As Byte)
                Escribir("CNAM\Color\Alpha", CLng(value))
            End Set
        End Property

        ''' <summary>FNAM\Playable</summary>
        Public Property Playable As UInteger
            Get
                Return CUInt(Entero("FNAM\Playable"))
            End Get
            Set(value As UInteger)
                Escribir("FNAM\Playable", CLng(value))
            End Set
        End Property

    End Class

    ''' <summary>Campos de un record FLST de Skyrim.</summary>
    Public NotInheritable Class FlstSSE
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>EDID\Editor ID</summary>
        Public Property EditorID As String
            Get
                Return Txt("EDID\Editor ID")
            End Get
            Set(value As String)
                Escribir("EDID\Editor ID", value)
            End Set
        End Property

        ''' <summary>FormIDs</summary>
        Public ReadOnly Property FormIDs As IReadOnlyList(Of FlstSSE_FormIDs)
            Get
                Return Elementos(Of FlstSSE_FormIDs)("FormIDs", Function(n) New FlstSSE_FormIDs(n, Context, Resolver))
            End Get
        End Property

    End Class

    ''' <summary>Un elemento de FormIDs.</summary>
    Public NotInheritable Class FlstSSE_FormIDs
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>LNAM\FormID. Referencia en el espacio del orden de carga.</summary>
        Public Property FormID As UInteger
            Get
                Return Referencia("LNAM\FormID")
            End Get
            Set(value As UInteger)
                PonerReferencia("LNAM\FormID", value)
            End Set
        End Property

    End Class

    ''' <summary>Campos de un record HDPT de Skyrim.</summary>
    Public NotInheritable Class HdptSSE
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>EDID\Editor ID</summary>
        Public Property EditorID As String
            Get
                Return Txt("EDID\Editor ID")
            End Get
            Set(value As String)
                Escribir("EDID\Editor ID", value)
            End Set
        End Property

        ''' <summary>Model\MODL\Model FileName</summary>
        Public Property ModelModelFileName As String
            Get
                Return Txt("Model\MODL\Model FileName")
            End Get
            Set(value As String)
                Escribir("Model\MODL\Model FileName", value)
            End Set
        End Property

        ''' <summary>DATA\Flags</summary>
        Public Property Flags As Byte
            Get
                Return CByte(Entero("DATA\Flags"))
            End Get
            Set(value As Byte)
                Escribir("DATA\Flags", CLng(value))
            End Set
        End Property

        ''' <summary>PNAM\Type</summary>
        Public Property Type As UInteger
            Get
                Return CUInt(Entero("PNAM\Type"))
            End Get
            Set(value As UInteger)
                Escribir("PNAM\Type", CLng(value))
            End Set
        End Property

        ''' <summary>TNAM\Texture Set  -&gt;  TXST / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property TextureSet As UInteger
            Get
                Return Referencia("TNAM\Texture Set")
            End Get
            Set(value As UInteger)
                PonerReferencia("TNAM\Texture Set", value)
            End Set
        End Property

        ''' <summary>CNAM\Color  -&gt;  CLFM / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property Color As UInteger
            Get
                Return Referencia("CNAM\Color")
            End Get
            Set(value As UInteger)
                PonerReferencia("CNAM\Color", value)
            End Set
        End Property

        ''' <summary>RNAM\Valid Races  -&gt;  FLST / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property ValidRaces As UInteger
            Get
                Return Referencia("RNAM\Valid Races")
            End Get
            Set(value As UInteger)
                PonerReferencia("RNAM\Valid Races", value)
            End Set
        End Property

        ''' <summary>Model\MODS\Alternate Textures</summary>
        Public ReadOnly Property AlternateTextures As IReadOnlyList(Of HdptSSE_AlternateTextures)
            Get
                Return Elementos(Of HdptSSE_AlternateTextures)("Model\MODS\Alternate Textures", Function(n) New HdptSSE_AlternateTextures(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Extra Parts</summary>
        Public ReadOnly Property ExtraParts As IReadOnlyList(Of HdptSSE_ExtraParts)
            Get
                Return Elementos(Of HdptSSE_ExtraParts)("Extra Parts", Function(n) New HdptSSE_ExtraParts(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Parts</summary>
        Public ReadOnly Property Parts As IReadOnlyList(Of HdptSSE_Parts)
            Get
                Return Elementos(Of HdptSSE_Parts)("Parts", Function(n) New HdptSSE_Parts(n, Context, Resolver))
            End Get
        End Property

    End Class

    ''' <summary>Un elemento de Model\MODS\Alternate Textures.</summary>
    Public NotInheritable Class HdptSSE_AlternateTextures
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Alternate Texture\New Texture  -&gt;  TXST. Referencia en el espacio del orden de carga.</summary>
        Public Property AlternateTextureNewTexture As UInteger
            Get
                Return Referencia("Alternate Texture\New Texture")
            End Get
            Set(value As UInteger)
                PonerReferencia("Alternate Texture\New Texture", value)
            End Set
        End Property

        ''' <summary>Alternate Texture\3D Index</summary>
        Public Property AlternateTexture3DIndex As Integer
            Get
                Return CInt(Entero("Alternate Texture\3D Index"))
            End Get
            Set(value As Integer)
                Escribir("Alternate Texture\3D Index", CLng(value))
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Extra Parts.</summary>
    Public NotInheritable Class HdptSSE_ExtraParts
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>HNAM\Part  -&gt;  HDPT. Referencia en el espacio del orden de carga.</summary>
        Public Property Part As UInteger
            Get
                Return Referencia("HNAM\Part")
            End Get
            Set(value As UInteger)
                PonerReferencia("HNAM\Part", value)
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Parts.</summary>
    Public NotInheritable Class HdptSSE_Parts
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Part\NAM0\Part Type</summary>
        Public Property PartPartType As UInteger
            Get
                Return CUInt(Entero("Part\NAM0\Part Type"))
            End Get
            Set(value As UInteger)
                Escribir("Part\NAM0\Part Type", CLng(value))
            End Set
        End Property

        ''' <summary>Part\NAM1\FileName</summary>
        Public Property PartFileName As String
            Get
                Return Txt("Part\NAM1\FileName")
            End Get
            Set(value As String)
                Escribir("Part\NAM1\FileName", value)
            End Set
        End Property

    End Class

    ''' <summary>Campos de un record LVLI de Skyrim.</summary>
    Public NotInheritable Class LvliSSE
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>EDID\Editor ID</summary>
        Public Property EditorID As String
            Get
                Return Txt("EDID\Editor ID")
            End Get
            Set(value As String)
                Escribir("EDID\Editor ID", value)
            End Set
        End Property

        ''' <summary>OBND\Object Bounds\X1</summary>
        Public Property ObjectBoundsX1 As Short
            Get
                Return CShort(Entero("OBND\Object Bounds\X1"))
            End Get
            Set(value As Short)
                Escribir("OBND\Object Bounds\X1", CLng(value))
            End Set
        End Property

        ''' <summary>OBND\Object Bounds\Y1</summary>
        Public Property ObjectBoundsY1 As Short
            Get
                Return CShort(Entero("OBND\Object Bounds\Y1"))
            End Get
            Set(value As Short)
                Escribir("OBND\Object Bounds\Y1", CLng(value))
            End Set
        End Property

        ''' <summary>OBND\Object Bounds\Z1</summary>
        Public Property ObjectBoundsZ1 As Short
            Get
                Return CShort(Entero("OBND\Object Bounds\Z1"))
            End Get
            Set(value As Short)
                Escribir("OBND\Object Bounds\Z1", CLng(value))
            End Set
        End Property

        ''' <summary>OBND\Object Bounds\X2</summary>
        Public Property ObjectBoundsX2 As Short
            Get
                Return CShort(Entero("OBND\Object Bounds\X2"))
            End Get
            Set(value As Short)
                Escribir("OBND\Object Bounds\X2", CLng(value))
            End Set
        End Property

        ''' <summary>OBND\Object Bounds\Y2</summary>
        Public Property ObjectBoundsY2 As Short
            Get
                Return CShort(Entero("OBND\Object Bounds\Y2"))
            End Get
            Set(value As Short)
                Escribir("OBND\Object Bounds\Y2", CLng(value))
            End Set
        End Property

        ''' <summary>OBND\Object Bounds\Z2</summary>
        Public Property ObjectBoundsZ2 As Short
            Get
                Return CShort(Entero("OBND\Object Bounds\Z2"))
            End Get
            Set(value As Short)
                Escribir("OBND\Object Bounds\Z2", CLng(value))
            End Set
        End Property

        ''' <summary>LVLD\Chance None</summary>
        Public Property ChanceNone As Byte
            Get
                Return CByte(Entero("LVLD\Chance None"))
            End Get
            Set(value As Byte)
                Escribir("LVLD\Chance None", CLng(value))
            End Set
        End Property

        ''' <summary>LVLF\Flags</summary>
        Public Property Flags As Byte
            Get
                Return CByte(Entero("LVLF\Flags"))
            End Get
            Set(value As Byte)
                Escribir("LVLF\Flags", CLng(value))
            End Set
        End Property

        ''' <summary>LVLG\Global  -&gt;  GLOB. Referencia en el espacio del orden de carga.</summary>
        Public Property [Global] As UInteger
            Get
                Return Referencia("LVLG\Global")
            End Get
            Set(value As UInteger)
                PonerReferencia("LVLG\Global", value)
            End Set
        End Property

        ''' <summary>LLCT\Count</summary>
        Public Property Count As Byte
            Get
                Return CByte(Entero("LLCT\Count"))
            End Get
            Set(value As Byte)
                Escribir("LLCT\Count", CLng(value))
            End Set
        End Property

        ''' <summary>Leveled List Entries</summary>
        Public ReadOnly Property LeveledListEntries As IReadOnlyList(Of LvliSSE_LeveledListEntries)
            Get
                Return Elementos(Of LvliSSE_LeveledListEntries)("Leveled List Entries", Function(n) New LvliSSE_LeveledListEntries(n, Context, Resolver))
            End Get
        End Property

    End Class

    ''' <summary>Un elemento de Leveled List Entries.</summary>
    Public NotInheritable Class LvliSSE_LeveledListEntries
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Leveled List Entry\LVLO\Level</summary>
        Public Property LeveledListEntryLevel As UShort
            Get
                Return CUShort(Entero("Leveled List Entry\LVLO\Level"))
            End Get
            Set(value As UShort)
                Escribir("Leveled List Entry\LVLO\Level", CLng(value))
            End Set
        End Property

        ''' <summary>Leveled List Entry\LVLO\Item  -&gt;  ALCH / AMMO / APPA / ARMO / BOOK / INGR / KEYM / LIGH / LVLI / MISC / SCRL / SLGM / WEAP. Referencia en el espacio del orden de carga.</summary>
        Public Property LeveledListEntryItem As UInteger
            Get
                Return Referencia("Leveled List Entry\LVLO\Item")
            End Get
            Set(value As UInteger)
                PonerReferencia("Leveled List Entry\LVLO\Item", value)
            End Set
        End Property

        ''' <summary>Leveled List Entry\LVLO\Count</summary>
        Public Property LeveledListEntryCount As UShort
            Get
                Return CUShort(Entero("Leveled List Entry\LVLO\Count"))
            End Get
            Set(value As UShort)
                Escribir("Leveled List Entry\LVLO\Count", CLng(value))
            End Set
        End Property

        ''' <summary>Leveled List Entry\COED\Extra Data\Owner  -&gt;  NPC_ / FACT / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property ExtraDataOwner As UInteger
            Get
                Return Referencia("Leveled List Entry\COED\Extra Data\Owner")
            End Get
            Set(value As UInteger)
                PonerReferencia("Leveled List Entry\COED\Extra Data\Owner", value)
            End Set
        End Property

        ''' <summary>Leveled List Entry\COED\Extra Data\Item Condition</summary>
        Public Property ExtraDataItemCondition As Single
            Get
                Return Flt("Leveled List Entry\COED\Extra Data\Item Condition")
            End Get
            Set(value As Single)
                Escribir("Leveled List Entry\COED\Extra Data\Item Condition", value)
            End Set
        End Property

    End Class

    ''' <summary>Campos de un record LVLN de Skyrim.</summary>
    Public NotInheritable Class LvlnSSE
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>EDID\Editor ID</summary>
        Public Property EditorID As String
            Get
                Return Txt("EDID\Editor ID")
            End Get
            Set(value As String)
                Escribir("EDID\Editor ID", value)
            End Set
        End Property

        ''' <summary>OBND\Object Bounds\X1</summary>
        Public Property ObjectBoundsX1 As Short
            Get
                Return CShort(Entero("OBND\Object Bounds\X1"))
            End Get
            Set(value As Short)
                Escribir("OBND\Object Bounds\X1", CLng(value))
            End Set
        End Property

        ''' <summary>OBND\Object Bounds\Y1</summary>
        Public Property ObjectBoundsY1 As Short
            Get
                Return CShort(Entero("OBND\Object Bounds\Y1"))
            End Get
            Set(value As Short)
                Escribir("OBND\Object Bounds\Y1", CLng(value))
            End Set
        End Property

        ''' <summary>OBND\Object Bounds\Z1</summary>
        Public Property ObjectBoundsZ1 As Short
            Get
                Return CShort(Entero("OBND\Object Bounds\Z1"))
            End Get
            Set(value As Short)
                Escribir("OBND\Object Bounds\Z1", CLng(value))
            End Set
        End Property

        ''' <summary>OBND\Object Bounds\X2</summary>
        Public Property ObjectBoundsX2 As Short
            Get
                Return CShort(Entero("OBND\Object Bounds\X2"))
            End Get
            Set(value As Short)
                Escribir("OBND\Object Bounds\X2", CLng(value))
            End Set
        End Property

        ''' <summary>OBND\Object Bounds\Y2</summary>
        Public Property ObjectBoundsY2 As Short
            Get
                Return CShort(Entero("OBND\Object Bounds\Y2"))
            End Get
            Set(value As Short)
                Escribir("OBND\Object Bounds\Y2", CLng(value))
            End Set
        End Property

        ''' <summary>OBND\Object Bounds\Z2</summary>
        Public Property ObjectBoundsZ2 As Short
            Get
                Return CShort(Entero("OBND\Object Bounds\Z2"))
            End Get
            Set(value As Short)
                Escribir("OBND\Object Bounds\Z2", CLng(value))
            End Set
        End Property

        ''' <summary>LVLD\Chance None</summary>
        Public Property ChanceNone As Byte
            Get
                Return CByte(Entero("LVLD\Chance None"))
            End Get
            Set(value As Byte)
                Escribir("LVLD\Chance None", CLng(value))
            End Set
        End Property

        ''' <summary>LVLF\Flags</summary>
        Public Property Flags As Byte
            Get
                Return CByte(Entero("LVLF\Flags"))
            End Get
            Set(value As Byte)
                Escribir("LVLF\Flags", CLng(value))
            End Set
        End Property

        ''' <summary>LVLG\Global  -&gt;  GLOB. Referencia en el espacio del orden de carga.</summary>
        Public Property [Global] As UInteger
            Get
                Return Referencia("LVLG\Global")
            End Get
            Set(value As UInteger)
                PonerReferencia("LVLG\Global", value)
            End Set
        End Property

        ''' <summary>LLCT\Count</summary>
        Public Property Count As Byte
            Get
                Return CByte(Entero("LLCT\Count"))
            End Get
            Set(value As Byte)
                Escribir("LLCT\Count", CLng(value))
            End Set
        End Property

        ''' <summary>Model\MODL\Model FileName</summary>
        Public Property ModelModelFileName As String
            Get
                Return Txt("Model\MODL\Model FileName")
            End Get
            Set(value As String)
                Escribir("Model\MODL\Model FileName", value)
            End Set
        End Property

        ''' <summary>Leveled List Entries</summary>
        Public ReadOnly Property LeveledListEntries As IReadOnlyList(Of LvlnSSE_LeveledListEntries)
            Get
                Return Elementos(Of LvlnSSE_LeveledListEntries)("Leveled List Entries", Function(n) New LvlnSSE_LeveledListEntries(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Model\MODS\Alternate Textures</summary>
        Public ReadOnly Property AlternateTextures As IReadOnlyList(Of LvlnSSE_AlternateTextures)
            Get
                Return Elementos(Of LvlnSSE_AlternateTextures)("Model\MODS\Alternate Textures", Function(n) New LvlnSSE_AlternateTextures(n, Context, Resolver))
            End Get
        End Property

    End Class

    ''' <summary>Un elemento de Leveled List Entries.</summary>
    Public NotInheritable Class LvlnSSE_LeveledListEntries
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Leveled List Entry\LVLO\Level</summary>
        Public Property LeveledListEntryLevel As UShort
            Get
                Return CUShort(Entero("Leveled List Entry\LVLO\Level"))
            End Get
            Set(value As UShort)
                Escribir("Leveled List Entry\LVLO\Level", CLng(value))
            End Set
        End Property

        ''' <summary>Leveled List Entry\LVLO\NPC  -&gt;  LVLN / NPC_. Referencia en el espacio del orden de carga.</summary>
        Public Property LeveledListEntryNPC As UInteger
            Get
                Return Referencia("Leveled List Entry\LVLO\NPC")
            End Get
            Set(value As UInteger)
                PonerReferencia("Leveled List Entry\LVLO\NPC", value)
            End Set
        End Property

        ''' <summary>Leveled List Entry\LVLO\Count</summary>
        Public Property LeveledListEntryCount As UShort
            Get
                Return CUShort(Entero("Leveled List Entry\LVLO\Count"))
            End Get
            Set(value As UShort)
                Escribir("Leveled List Entry\LVLO\Count", CLng(value))
            End Set
        End Property

        ''' <summary>Leveled List Entry\COED\Extra Data\Owner  -&gt;  NPC_ / FACT / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property ExtraDataOwner As UInteger
            Get
                Return Referencia("Leveled List Entry\COED\Extra Data\Owner")
            End Get
            Set(value As UInteger)
                PonerReferencia("Leveled List Entry\COED\Extra Data\Owner", value)
            End Set
        End Property

        ''' <summary>Leveled List Entry\COED\Extra Data\Item Condition</summary>
        Public Property ExtraDataItemCondition As Single
            Get
                Return Flt("Leveled List Entry\COED\Extra Data\Item Condition")
            End Get
            Set(value As Single)
                Escribir("Leveled List Entry\COED\Extra Data\Item Condition", value)
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Model\MODS\Alternate Textures.</summary>
    Public NotInheritable Class LvlnSSE_AlternateTextures
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Alternate Texture\New Texture  -&gt;  TXST. Referencia en el espacio del orden de carga.</summary>
        Public Property AlternateTextureNewTexture As UInteger
            Get
                Return Referencia("Alternate Texture\New Texture")
            End Get
            Set(value As UInteger)
                PonerReferencia("Alternate Texture\New Texture", value)
            End Set
        End Property

        ''' <summary>Alternate Texture\3D Index</summary>
        Public Property AlternateTexture3DIndex As Integer
            Get
                Return CInt(Entero("Alternate Texture\3D Index"))
            End Get
            Set(value As Integer)
                Escribir("Alternate Texture\3D Index", CLng(value))
            End Set
        End Property

    End Class

    ''' <summary>Campos de un record NPC_ de Skyrim.</summary>
    Public NotInheritable Class NpcSSE
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>EDID\Editor ID</summary>
        Public Property EditorID As String
            Get
                Return Txt("EDID\Editor ID")
            End Get
            Set(value As String)
                Escribir("EDID\Editor ID", value)
            End Set
        End Property

        ''' <summary>VMAD\Virtual Machine Adapter\Version</summary>
        Public Property VirtualMachineAdapterVersion As Short
            Get
                Return CShort(Entero("VMAD\Virtual Machine Adapter\Version"))
            End Get
            Set(value As Short)
                Escribir("VMAD\Virtual Machine Adapter\Version", CLng(value))
            End Set
        End Property

        ''' <summary>VMAD\Virtual Machine Adapter\Object Format</summary>
        Public Property VirtualMachineAdapterObjectFormat As Short
            Get
                Return CShort(Entero("VMAD\Virtual Machine Adapter\Object Format"))
            End Get
            Set(value As Short)
                Escribir("VMAD\Virtual Machine Adapter\Object Format", CLng(value))
            End Set
        End Property

        ''' <summary>OBND\Object Bounds\X1</summary>
        Public Property ObjectBoundsX1 As Short
            Get
                Return CShort(Entero("OBND\Object Bounds\X1"))
            End Get
            Set(value As Short)
                Escribir("OBND\Object Bounds\X1", CLng(value))
            End Set
        End Property

        ''' <summary>OBND\Object Bounds\Y1</summary>
        Public Property ObjectBoundsY1 As Short
            Get
                Return CShort(Entero("OBND\Object Bounds\Y1"))
            End Get
            Set(value As Short)
                Escribir("OBND\Object Bounds\Y1", CLng(value))
            End Set
        End Property

        ''' <summary>OBND\Object Bounds\Z1</summary>
        Public Property ObjectBoundsZ1 As Short
            Get
                Return CShort(Entero("OBND\Object Bounds\Z1"))
            End Get
            Set(value As Short)
                Escribir("OBND\Object Bounds\Z1", CLng(value))
            End Set
        End Property

        ''' <summary>OBND\Object Bounds\X2</summary>
        Public Property ObjectBoundsX2 As Short
            Get
                Return CShort(Entero("OBND\Object Bounds\X2"))
            End Get
            Set(value As Short)
                Escribir("OBND\Object Bounds\X2", CLng(value))
            End Set
        End Property

        ''' <summary>OBND\Object Bounds\Y2</summary>
        Public Property ObjectBoundsY2 As Short
            Get
                Return CShort(Entero("OBND\Object Bounds\Y2"))
            End Get
            Set(value As Short)
                Escribir("OBND\Object Bounds\Y2", CLng(value))
            End Set
        End Property

        ''' <summary>OBND\Object Bounds\Z2</summary>
        Public Property ObjectBoundsZ2 As Short
            Get
                Return CShort(Entero("OBND\Object Bounds\Z2"))
            End Get
            Set(value As Short)
                Escribir("OBND\Object Bounds\Z2", CLng(value))
            End Set
        End Property

        ''' <summary>ACBS\Configuration\Flags</summary>
        Public Property ConfigurationFlags As UInteger
            Get
                Return CUInt(Entero("ACBS\Configuration\Flags"))
            End Get
            Set(value As UInteger)
                Escribir("ACBS\Configuration\Flags", CLng(value))
            End Set
        End Property

        ''' <summary>Bit 0 de ACBS\Configuration\Flags: Female</summary>
        Public Property ConfigurationFlagsFemale As Boolean
            Get
                Return Bit("ACBS\Configuration\Flags", 0)
            End Get
            Set(value As Boolean)
                PonerBit("ACBS\Configuration\Flags", 0, value)
            End Set
        End Property
        ''' <summary>Bit 1 de ACBS\Configuration\Flags: Essential</summary>
        Public Property ConfigurationFlagsEssential As Boolean
            Get
                Return Bit("ACBS\Configuration\Flags", 1)
            End Get
            Set(value As Boolean)
                PonerBit("ACBS\Configuration\Flags", 1, value)
            End Set
        End Property
        ''' <summary>Bit 2 de ACBS\Configuration\Flags: Is CharGen Face Preset</summary>
        Public Property ConfigurationFlagsIsCharGenFacePreset As Boolean
            Get
                Return Bit("ACBS\Configuration\Flags", 2)
            End Get
            Set(value As Boolean)
                PonerBit("ACBS\Configuration\Flags", 2, value)
            End Set
        End Property
        ''' <summary>Bit 3 de ACBS\Configuration\Flags: Respawn</summary>
        Public Property ConfigurationFlagsRespawn As Boolean
            Get
                Return Bit("ACBS\Configuration\Flags", 3)
            End Get
            Set(value As Boolean)
                PonerBit("ACBS\Configuration\Flags", 3, value)
            End Set
        End Property
        ''' <summary>Bit 4 de ACBS\Configuration\Flags: Auto-calc stats</summary>
        Public Property ConfigurationFlagsAutoCalcStats As Boolean
            Get
                Return Bit("ACBS\Configuration\Flags", 4)
            End Get
            Set(value As Boolean)
                PonerBit("ACBS\Configuration\Flags", 4, value)
            End Set
        End Property
        ''' <summary>Bit 5 de ACBS\Configuration\Flags: Unique</summary>
        Public Property ConfigurationFlagsUnique As Boolean
            Get
                Return Bit("ACBS\Configuration\Flags", 5)
            End Get
            Set(value As Boolean)
                PonerBit("ACBS\Configuration\Flags", 5, value)
            End Set
        End Property
        ''' <summary>Bit 6 de ACBS\Configuration\Flags: Doesn't affect stealth meter</summary>
        Public Property ConfigurationFlagsDoesnTAffectStealthMeter As Boolean
            Get
                Return Bit("ACBS\Configuration\Flags", 6)
            End Get
            Set(value As Boolean)
                PonerBit("ACBS\Configuration\Flags", 6, value)
            End Set
        End Property
        ''' <summary>Bit 7 de ACBS\Configuration\Flags: PC Level Mult</summary>
        Public Property ConfigurationFlagsPCLevelMult As Boolean
            Get
                Return Bit("ACBS\Configuration\Flags", 7)
            End Get
            Set(value As Boolean)
                PonerBit("ACBS\Configuration\Flags", 7, value)
            End Set
        End Property
        ''' <summary>Bit 8 de ACBS\Configuration\Flags: Use Template?</summary>
        Public Property ConfigurationFlagsUseTemplate As Boolean
            Get
                Return Bit("ACBS\Configuration\Flags", 8)
            End Get
            Set(value As Boolean)
                PonerBit("ACBS\Configuration\Flags", 8, value)
            End Set
        End Property
        ''' <summary>Bit 11 de ACBS\Configuration\Flags: Protected</summary>
        Public Property ConfigurationFlagsProtected As Boolean
            Get
                Return Bit("ACBS\Configuration\Flags", 11)
            End Get
            Set(value As Boolean)
                PonerBit("ACBS\Configuration\Flags", 11, value)
            End Set
        End Property
        ''' <summary>Bit 14 de ACBS\Configuration\Flags: Summonable</summary>
        Public Property ConfigurationFlagsSummonable As Boolean
            Get
                Return Bit("ACBS\Configuration\Flags", 14)
            End Get
            Set(value As Boolean)
                PonerBit("ACBS\Configuration\Flags", 14, value)
            End Set
        End Property
        ''' <summary>Bit 16 de ACBS\Configuration\Flags: Doesn't bleed</summary>
        Public Property ConfigurationFlagsDoesnTBleed As Boolean
            Get
                Return Bit("ACBS\Configuration\Flags", 16)
            End Get
            Set(value As Boolean)
                PonerBit("ACBS\Configuration\Flags", 16, value)
            End Set
        End Property
        ''' <summary>Bit 18 de ACBS\Configuration\Flags: Bleedout Override</summary>
        Public Property ConfigurationFlagsBleedoutOverride As Boolean
            Get
                Return Bit("ACBS\Configuration\Flags", 18)
            End Get
            Set(value As Boolean)
                PonerBit("ACBS\Configuration\Flags", 18, value)
            End Set
        End Property
        ''' <summary>Bit 19 de ACBS\Configuration\Flags: Opposite Gender Anims</summary>
        Public Property ConfigurationFlagsOppositeGenderAnims As Boolean
            Get
                Return Bit("ACBS\Configuration\Flags", 19)
            End Get
            Set(value As Boolean)
                PonerBit("ACBS\Configuration\Flags", 19, value)
            End Set
        End Property
        ''' <summary>Bit 20 de ACBS\Configuration\Flags: Simple Actor</summary>
        Public Property ConfigurationFlagsSimpleActor As Boolean
            Get
                Return Bit("ACBS\Configuration\Flags", 20)
            End Get
            Set(value As Boolean)
                PonerBit("ACBS\Configuration\Flags", 20, value)
            End Set
        End Property
        ''' <summary>Bit 21 de ACBS\Configuration\Flags: looped script?</summary>
        Public Property ConfigurationFlagsLoopedScript As Boolean
            Get
                Return Bit("ACBS\Configuration\Flags", 21)
            End Get
            Set(value As Boolean)
                PonerBit("ACBS\Configuration\Flags", 21, value)
            End Set
        End Property
        ''' <summary>Bit 28 de ACBS\Configuration\Flags: looped audio?</summary>
        Public Property ConfigurationFlagsLoopedAudio As Boolean
            Get
                Return Bit("ACBS\Configuration\Flags", 28)
            End Get
            Set(value As Boolean)
                PonerBit("ACBS\Configuration\Flags", 28, value)
            End Set
        End Property
        ''' <summary>Bit 29 de ACBS\Configuration\Flags: Is Ghost</summary>
        Public Property ConfigurationFlagsIsGhost As Boolean
            Get
                Return Bit("ACBS\Configuration\Flags", 29)
            End Get
            Set(value As Boolean)
                PonerBit("ACBS\Configuration\Flags", 29, value)
            End Set
        End Property
        ''' <summary>Bit 31 de ACBS\Configuration\Flags: Invulnerable</summary>
        Public Property ConfigurationFlagsInvulnerable As Boolean
            Get
                Return Bit("ACBS\Configuration\Flags", 31)
            End Get
            Set(value As Boolean)
                PonerBit("ACBS\Configuration\Flags", 31, value)
            End Set
        End Property

        ''' <summary>ACBS\Configuration\Magicka Offset</summary>
        Public Property ConfigurationMagickaOffset As Short
            Get
                Return CShort(Entero("ACBS\Configuration\Magicka Offset"))
            End Get
            Set(value As Short)
                Escribir("ACBS\Configuration\Magicka Offset", CLng(value))
            End Set
        End Property

        ''' <summary>ACBS\Configuration\Stamina Offset</summary>
        Public Property ConfigurationStaminaOffset As Short
            Get
                Return CShort(Entero("ACBS\Configuration\Stamina Offset"))
            End Get
            Set(value As Short)
                Escribir("ACBS\Configuration\Stamina Offset", CLng(value))
            End Set
        End Property

        ''' <summary>ACBS\Configuration\Level</summary>
        Public Property ConfigurationLevel As UShort
            Get
                Return CUShort(Entero("ACBS\Configuration\Level"))
            End Get
            Set(value As UShort)
                Escribir("ACBS\Configuration\Level", CLng(value))
            End Set
        End Property

        ''' <summary>ACBS\Configuration\Calc min level</summary>
        Public Property ConfigurationCalcMinLevel As UShort
            Get
                Return CUShort(Entero("ACBS\Configuration\Calc min level"))
            End Get
            Set(value As UShort)
                Escribir("ACBS\Configuration\Calc min level", CLng(value))
            End Set
        End Property

        ''' <summary>ACBS\Configuration\Calc max level</summary>
        Public Property ConfigurationCalcMaxLevel As UShort
            Get
                Return CUShort(Entero("ACBS\Configuration\Calc max level"))
            End Get
            Set(value As UShort)
                Escribir("ACBS\Configuration\Calc max level", CLng(value))
            End Set
        End Property

        ''' <summary>ACBS\Configuration\Speed Multiplier</summary>
        Public Property ConfigurationSpeedMultiplier As UShort
            Get
                Return CUShort(Entero("ACBS\Configuration\Speed Multiplier"))
            End Get
            Set(value As UShort)
                Escribir("ACBS\Configuration\Speed Multiplier", CLng(value))
            End Set
        End Property

        ''' <summary>ACBS\Configuration\Disposition Base (unused)</summary>
        Public Property ConfigurationDispositionBaseUnused As Short
            Get
                Return CShort(Entero("ACBS\Configuration\Disposition Base (unused)"))
            End Get
            Set(value As Short)
                Escribir("ACBS\Configuration\Disposition Base (unused)", CLng(value))
            End Set
        End Property

        ''' <summary>ACBS\Configuration\Template Flags</summary>
        Public Property ConfigurationTemplateFlags As UShort
            Get
                Return CUShort(Entero("ACBS\Configuration\Template Flags"))
            End Get
            Set(value As UShort)
                Escribir("ACBS\Configuration\Template Flags", CLng(value))
            End Set
        End Property

        ''' <summary>ACBS\Configuration\Health Offset</summary>
        Public Property ConfigurationHealthOffset As Short
            Get
                Return CShort(Entero("ACBS\Configuration\Health Offset"))
            End Get
            Set(value As Short)
                Escribir("ACBS\Configuration\Health Offset", CLng(value))
            End Set
        End Property

        ''' <summary>ACBS\Configuration\Bleedout Override</summary>
        Public Property ConfigurationBleedoutOverride As UShort
            Get
                Return CUShort(Entero("ACBS\Configuration\Bleedout Override"))
            End Get
            Set(value As UShort)
                Escribir("ACBS\Configuration\Bleedout Override", CLng(value))
            End Set
        End Property

        ''' <summary>INAM\Death item  -&gt;  LVLI. Referencia en el espacio del orden de carga.</summary>
        Public Property DeathItem As UInteger
            Get
                Return Referencia("INAM\Death item")
            End Get
            Set(value As UInteger)
                PonerReferencia("INAM\Death item", value)
            End Set
        End Property

        ''' <summary>VTCK\Voice  -&gt;  VTYP. Referencia en el espacio del orden de carga.</summary>
        Public Property Voice As UInteger
            Get
                Return Referencia("VTCK\Voice")
            End Get
            Set(value As UInteger)
                PonerReferencia("VTCK\Voice", value)
            End Set
        End Property

        ''' <summary>TPLT\Template  -&gt;  LVLN / NPC_. Referencia en el espacio del orden de carga.</summary>
        Public Property Template As UInteger
            Get
                Return Referencia("TPLT\Template")
            End Get
            Set(value As UInteger)
                PonerReferencia("TPLT\Template", value)
            End Set
        End Property

        ''' <summary>RNAM\Race  -&gt;  RACE. Referencia en el espacio del orden de carga.</summary>
        Public Property Race As UInteger
            Get
                Return Referencia("RNAM\Race")
            End Get
            Set(value As UInteger)
                PonerReferencia("RNAM\Race", value)
            End Set
        End Property

        ''' <summary>SPCT\Count</summary>
        Public Property Count As UInteger
            Get
                Return CUInt(Entero("SPCT\Count"))
            End Get
            Set(value As UInteger)
                Escribir("SPCT\Count", CLng(value))
            End Set
        End Property

        ''' <summary>Destructible\DEST\Header\Health</summary>
        Public Property HeaderHealth As Integer
            Get
                Return CInt(Entero("Destructible\DEST\Header\Health"))
            End Get
            Set(value As Integer)
                Escribir("Destructible\DEST\Header\Health", CLng(value))
            End Set
        End Property

        ''' <summary>Destructible\DEST\Header\DEST Count</summary>
        Public Property HeaderDESTCount As Byte
            Get
                Return CByte(Entero("Destructible\DEST\Header\DEST Count"))
            End Get
            Set(value As Byte)
                Escribir("Destructible\DEST\Header\DEST Count", CLng(value))
            End Set
        End Property

        ''' <summary>Destructible\DEST\Header\VATS Targetable</summary>
        Public Property HeaderVATSTargetable As Byte
            Get
                Return CByte(Entero("Destructible\DEST\Header\VATS Targetable"))
            End Get
            Set(value As Byte)
                Escribir("Destructible\DEST\Header\VATS Targetable", CLng(value))
            End Set
        End Property

        ''' <summary>WNAM\Skin  -&gt;  ARMO. Referencia en el espacio del orden de carga.</summary>
        Public Property Skin As UInteger
            Get
                Return Referencia("WNAM\Skin")
            End Get
            Set(value As UInteger)
                PonerReferencia("WNAM\Skin", value)
            End Set
        End Property

        ''' <summary>ANAM\Far away model  -&gt;  ARMO. Referencia en el espacio del orden de carga.</summary>
        Public Property FarAwayModel As UInteger
            Get
                Return Referencia("ANAM\Far away model")
            End Get
            Set(value As UInteger)
                PonerReferencia("ANAM\Far away model", value)
            End Set
        End Property

        ''' <summary>ATKR\Attack Race  -&gt;  RACE. Referencia en el espacio del orden de carga.</summary>
        Public Property AttackRace As UInteger
            Get
                Return Referencia("ATKR\Attack Race")
            End Get
            Set(value As UInteger)
                PonerReferencia("ATKR\Attack Race", value)
            End Set
        End Property

        ''' <summary>SPOR\Spectator override package list  -&gt;  FLST. Referencia en el espacio del orden de carga.</summary>
        Public Property SpectatorOverridePackageList As UInteger
            Get
                Return Referencia("SPOR\Spectator override package list")
            End Get
            Set(value As UInteger)
                PonerReferencia("SPOR\Spectator override package list", value)
            End Set
        End Property

        ''' <summary>OCOR\Observe dead body override package list  -&gt;  FLST. Referencia en el espacio del orden de carga.</summary>
        Public Property ObserveDeadBodyOverridePackageList As UInteger
            Get
                Return Referencia("OCOR\Observe dead body override package list")
            End Get
            Set(value As UInteger)
                PonerReferencia("OCOR\Observe dead body override package list", value)
            End Set
        End Property

        ''' <summary>GWOR\Guard warn override package list  -&gt;  FLST. Referencia en el espacio del orden de carga.</summary>
        Public Property GuardWarnOverridePackageList As UInteger
            Get
                Return Referencia("GWOR\Guard warn override package list")
            End Get
            Set(value As UInteger)
                PonerReferencia("GWOR\Guard warn override package list", value)
            End Set
        End Property

        ''' <summary>ECOR\Combat override package list  -&gt;  FLST. Referencia en el espacio del orden de carga.</summary>
        Public Property CombatOverridePackageList As UInteger
            Get
                Return Referencia("ECOR\Combat override package list")
            End Get
            Set(value As UInteger)
                PonerReferencia("ECOR\Combat override package list", value)
            End Set
        End Property

        ''' <summary>PRKZ\Perk Count</summary>
        Public Property PerkCount As UInteger
            Get
                Return CUInt(Entero("PRKZ\Perk Count"))
            End Get
            Set(value As UInteger)
                Escribir("PRKZ\Perk Count", CLng(value))
            End Set
        End Property

        ''' <summary>COCT\Count</summary>
        Public Property Count2 As UInteger
            Get
                Return CUInt(Entero("COCT\Count"))
            End Get
            Set(value As UInteger)
                Escribir("COCT\Count", CLng(value))
            End Set
        End Property

        ''' <summary>AIDT\AI Data\Aggression</summary>
        Public Property AIDataAggression As Byte
            Get
                Return CByte(Entero("AIDT\AI Data\Aggression"))
            End Get
            Set(value As Byte)
                Escribir("AIDT\AI Data\Aggression", CLng(value))
            End Set
        End Property

        ''' <summary>AIDT\AI Data\Confidence</summary>
        Public Property AIDataConfidence As Byte
            Get
                Return CByte(Entero("AIDT\AI Data\Confidence"))
            End Get
            Set(value As Byte)
                Escribir("AIDT\AI Data\Confidence", CLng(value))
            End Set
        End Property

        ''' <summary>AIDT\AI Data\Energy Level</summary>
        Public Property AIDataEnergyLevel As Byte
            Get
                Return CByte(Entero("AIDT\AI Data\Energy Level"))
            End Get
            Set(value As Byte)
                Escribir("AIDT\AI Data\Energy Level", CLng(value))
            End Set
        End Property

        ''' <summary>AIDT\AI Data\Morality</summary>
        Public Property AIDataMorality As Byte
            Get
                Return CByte(Entero("AIDT\AI Data\Morality"))
            End Get
            Set(value As Byte)
                Escribir("AIDT\AI Data\Morality", CLng(value))
            End Set
        End Property

        ''' <summary>AIDT\AI Data\Mood</summary>
        Public Property AIDataMood As Byte
            Get
                Return CByte(Entero("AIDT\AI Data\Mood"))
            End Get
            Set(value As Byte)
                Escribir("AIDT\AI Data\Mood", CLng(value))
            End Set
        End Property

        ''' <summary>AIDT\AI Data\Assistance</summary>
        Public Property AIDataAssistance As Byte
            Get
                Return CByte(Entero("AIDT\AI Data\Assistance"))
            End Get
            Set(value As Byte)
                Escribir("AIDT\AI Data\Assistance", CLng(value))
            End Set
        End Property

        ''' <summary>AIDT\AI Data\Aggro\Aggro Radius Behavior</summary>
        Public Property AggroAggroRadiusBehavior As Byte
            Get
                Return CByte(Entero("AIDT\AI Data\Aggro\Aggro Radius Behavior"))
            End Get
            Set(value As Byte)
                Escribir("AIDT\AI Data\Aggro\Aggro Radius Behavior", CLng(value))
            End Set
        End Property

        ''' <summary>AIDT\AI Data\Aggro\Warn</summary>
        Public Property AggroWarn As UInteger
            Get
                Return CUInt(Entero("AIDT\AI Data\Aggro\Warn"))
            End Get
            Set(value As UInteger)
                Escribir("AIDT\AI Data\Aggro\Warn", CLng(value))
            End Set
        End Property

        ''' <summary>AIDT\AI Data\Aggro\Warn/Attack</summary>
        Public Property AggroWarnAttack As UInteger
            Get
                Return CUInt(Entero("AIDT\AI Data\Aggro\Warn/Attack"))
            End Get
            Set(value As UInteger)
                Escribir("AIDT\AI Data\Aggro\Warn/Attack", CLng(value))
            End Set
        End Property

        ''' <summary>AIDT\AI Data\Aggro\Attack</summary>
        Public Property AggroAttack As UInteger
            Get
                Return CUInt(Entero("AIDT\AI Data\Aggro\Attack"))
            End Get
            Set(value As UInteger)
                Escribir("AIDT\AI Data\Aggro\Attack", CLng(value))
            End Set
        End Property

        ''' <summary>Keywords\KSIZ\Keyword Count</summary>
        Public Property KeywordsKeywordCount As UInteger
            Get
                Return CUInt(Entero("Keywords\KSIZ\Keyword Count"))
            End Get
            Set(value As UInteger)
                Escribir("Keywords\KSIZ\Keyword Count", CLng(value))
            End Set
        End Property

        ''' <summary>CNAM\Class  -&gt;  CLAS. Referencia en el espacio del orden de carga.</summary>
        Public Property [Class] As UInteger
            Get
                Return Referencia("CNAM\Class")
            End Get
            Set(value As UInteger)
                PonerReferencia("CNAM\Class", value)
            End Set
        End Property

        ''' <summary>DNAM\Player Skills\Health</summary>
        Public Property PlayerSkillsHealth As UShort
            Get
                Return CUShort(Entero("DNAM\Player Skills\Health"))
            End Get
            Set(value As UShort)
                Escribir("DNAM\Player Skills\Health", CLng(value))
            End Set
        End Property

        ''' <summary>DNAM\Player Skills\Magicka</summary>
        Public Property PlayerSkillsMagicka As UShort
            Get
                Return CUShort(Entero("DNAM\Player Skills\Magicka"))
            End Get
            Set(value As UShort)
                Escribir("DNAM\Player Skills\Magicka", CLng(value))
            End Set
        End Property

        ''' <summary>DNAM\Player Skills\Stamina</summary>
        Public Property PlayerSkillsStamina As UShort
            Get
                Return CUShort(Entero("DNAM\Player Skills\Stamina"))
            End Get
            Set(value As UShort)
                Escribir("DNAM\Player Skills\Stamina", CLng(value))
            End Set
        End Property

        ''' <summary>DNAM\Player Skills\Far away model distance</summary>
        Public Property PlayerSkillsFarAwayModelDistance As Single
            Get
                Return Flt("DNAM\Player Skills\Far away model distance")
            End Get
            Set(value As Single)
                Escribir("DNAM\Player Skills\Far away model distance", value)
            End Set
        End Property

        ''' <summary>DNAM\Player Skills\Geared up weapons</summary>
        Public Property PlayerSkillsGearedUpWeapons As Byte
            Get
                Return CByte(Entero("DNAM\Player Skills\Geared up weapons"))
            End Get
            Set(value As Byte)
                Escribir("DNAM\Player Skills\Geared up weapons", CLng(value))
            End Set
        End Property

        ''' <summary>HCLF\Hair Color  -&gt;  CLFM. Referencia en el espacio del orden de carga.</summary>
        Public Property HairColor As UInteger
            Get
                Return Referencia("HCLF\Hair Color")
            End Get
            Set(value As UInteger)
                PonerReferencia("HCLF\Hair Color", value)
            End Set
        End Property

        ''' <summary>ZNAM\Combat Style  -&gt;  CSTY. Referencia en el espacio del orden de carga.</summary>
        Public Property CombatStyle As UInteger
            Get
                Return Referencia("ZNAM\Combat Style")
            End Get
            Set(value As UInteger)
                PonerReferencia("ZNAM\Combat Style", value)
            End Set
        End Property

        ''' <summary>GNAM\Gift Filter  -&gt;  FLST. Referencia en el espacio del orden de carga.</summary>
        Public Property GiftFilter As UInteger
            Get
                Return Referencia("GNAM\Gift Filter")
            End Get
            Set(value As UInteger)
                PonerReferencia("GNAM\Gift Filter", value)
            End Set
        End Property

        ''' <summary>NAM6\Height</summary>
        Public Property Height As Single
            Get
                Return Flt("NAM6\Height")
            End Get
            Set(value As Single)
                Escribir("NAM6\Height", value)
            End Set
        End Property

        ''' <summary>NAM7\Weight</summary>
        Public Property Weight As Single
            Get
                Return Flt("NAM7\Weight")
            End Get
            Set(value As Single)
                Escribir("NAM7\Weight", value)
            End Set
        End Property

        ''' <summary>NAM8\Sound Level</summary>
        Public Property SoundLevel As UInteger
            Get
                Return CUInt(Entero("NAM8\Sound Level"))
            End Get
            Set(value As UInteger)
                Escribir("NAM8\Sound Level", CLng(value))
            End Set
        End Property

        ''' <summary>CSCR\Inherits Sounds From  -&gt;  NPC_. Referencia en el espacio del orden de carga.</summary>
        Public Property InheritsSoundsFrom As UInteger
            Get
                Return Referencia("CSCR\Inherits Sounds From")
            End Get
            Set(value As UInteger)
                PonerReferencia("CSCR\Inherits Sounds From", value)
            End Set
        End Property

        ''' <summary>DOFT\Default outfit  -&gt;  OTFT. Referencia en el espacio del orden de carga.</summary>
        Public Property DefaultOutfit As UInteger
            Get
                Return Referencia("DOFT\Default outfit")
            End Get
            Set(value As UInteger)
                PonerReferencia("DOFT\Default outfit", value)
            End Set
        End Property

        ''' <summary>SOFT\Sleeping outfit  -&gt;  OTFT. Referencia en el espacio del orden de carga.</summary>
        Public Property SleepingOutfit As UInteger
            Get
                Return Referencia("SOFT\Sleeping outfit")
            End Get
            Set(value As UInteger)
                PonerReferencia("SOFT\Sleeping outfit", value)
            End Set
        End Property

        ''' <summary>DPLT\Default Package List  -&gt;  FLST. Referencia en el espacio del orden de carga.</summary>
        Public Property DefaultPackageList As UInteger
            Get
                Return Referencia("DPLT\Default Package List")
            End Get
            Set(value As UInteger)
                PonerReferencia("DPLT\Default Package List", value)
            End Set
        End Property

        ''' <summary>CRIF\Crime faction  -&gt;  FACT. Referencia en el espacio del orden de carga.</summary>
        Public Property CrimeFaction As UInteger
            Get
                Return Referencia("CRIF\Crime faction")
            End Get
            Set(value As UInteger)
                PonerReferencia("CRIF\Crime faction", value)
            End Set
        End Property

        ''' <summary>FTST\Head texture  -&gt;  TXST. Referencia en el espacio del orden de carga.</summary>
        Public Property HeadTexture As UInteger
            Get
                Return Referencia("FTST\Head texture")
            End Get
            Set(value As UInteger)
                PonerReferencia("FTST\Head texture", value)
            End Set
        End Property

        ''' <summary>QNAM\Texture lighting\Red</summary>
        Public Property TextureLightingRed As Single
            Get
                Return Flt("QNAM\Texture lighting\Red")
            End Get
            Set(value As Single)
                Escribir("QNAM\Texture lighting\Red", value)
            End Set
        End Property

        ''' <summary>QNAM\Texture lighting\Green</summary>
        Public Property TextureLightingGreen As Single
            Get
                Return Flt("QNAM\Texture lighting\Green")
            End Get
            Set(value As Single)
                Escribir("QNAM\Texture lighting\Green", value)
            End Set
        End Property

        ''' <summary>QNAM\Texture lighting\Blue</summary>
        Public Property TextureLightingBlue As Single
            Get
                Return Flt("QNAM\Texture lighting\Blue")
            End Get
            Set(value As Single)
                Escribir("QNAM\Texture lighting\Blue", value)
            End Set
        End Property

        ''' <summary>NAM9\Face morph\Nose Long/Short</summary>
        Public Property FaceMorphNoseLongShort As Single
            Get
                Return Flt("NAM9\Face morph\Nose Long/Short")
            End Get
            Set(value As Single)
                Escribir("NAM9\Face morph\Nose Long/Short", value)
            End Set
        End Property

        ''' <summary>NAM9\Face morph\Nose Up/Down</summary>
        Public Property FaceMorphNoseUpDown As Single
            Get
                Return Flt("NAM9\Face morph\Nose Up/Down")
            End Get
            Set(value As Single)
                Escribir("NAM9\Face morph\Nose Up/Down", value)
            End Set
        End Property

        ''' <summary>NAM9\Face morph\Jaw Up/Down</summary>
        Public Property FaceMorphJawUpDown As Single
            Get
                Return Flt("NAM9\Face morph\Jaw Up/Down")
            End Get
            Set(value As Single)
                Escribir("NAM9\Face morph\Jaw Up/Down", value)
            End Set
        End Property

        ''' <summary>NAM9\Face morph\Jaw Narrow/Wide</summary>
        Public Property FaceMorphJawNarrowWide As Single
            Get
                Return Flt("NAM9\Face morph\Jaw Narrow/Wide")
            End Get
            Set(value As Single)
                Escribir("NAM9\Face morph\Jaw Narrow/Wide", value)
            End Set
        End Property

        ''' <summary>NAM9\Face morph\Jaw Farward/Back</summary>
        Public Property FaceMorphJawFarwardBack As Single
            Get
                Return Flt("NAM9\Face morph\Jaw Farward/Back")
            End Get
            Set(value As Single)
                Escribir("NAM9\Face morph\Jaw Farward/Back", value)
            End Set
        End Property

        ''' <summary>NAM9\Face morph\Cheeks Up/Down</summary>
        Public Property FaceMorphCheeksUpDown As Single
            Get
                Return Flt("NAM9\Face morph\Cheeks Up/Down")
            End Get
            Set(value As Single)
                Escribir("NAM9\Face morph\Cheeks Up/Down", value)
            End Set
        End Property

        ''' <summary>NAM9\Face morph\Cheeks Farward/Back</summary>
        Public Property FaceMorphCheeksFarwardBack As Single
            Get
                Return Flt("NAM9\Face morph\Cheeks Farward/Back")
            End Get
            Set(value As Single)
                Escribir("NAM9\Face morph\Cheeks Farward/Back", value)
            End Set
        End Property

        ''' <summary>NAM9\Face morph\Eyes Up/Down</summary>
        Public Property FaceMorphEyesUpDown As Single
            Get
                Return Flt("NAM9\Face morph\Eyes Up/Down")
            End Get
            Set(value As Single)
                Escribir("NAM9\Face morph\Eyes Up/Down", value)
            End Set
        End Property

        ''' <summary>NAM9\Face morph\Eyes In/Out</summary>
        Public Property FaceMorphEyesInOut As Single
            Get
                Return Flt("NAM9\Face morph\Eyes In/Out")
            End Get
            Set(value As Single)
                Escribir("NAM9\Face morph\Eyes In/Out", value)
            End Set
        End Property

        ''' <summary>NAM9\Face morph\Brows Up/Down</summary>
        Public Property FaceMorphBrowsUpDown As Single
            Get
                Return Flt("NAM9\Face morph\Brows Up/Down")
            End Get
            Set(value As Single)
                Escribir("NAM9\Face morph\Brows Up/Down", value)
            End Set
        End Property

        ''' <summary>NAM9\Face morph\Brows In/Out</summary>
        Public Property FaceMorphBrowsInOut As Single
            Get
                Return Flt("NAM9\Face morph\Brows In/Out")
            End Get
            Set(value As Single)
                Escribir("NAM9\Face morph\Brows In/Out", value)
            End Set
        End Property

        ''' <summary>NAM9\Face morph\Brows Farward/Back</summary>
        Public Property FaceMorphBrowsFarwardBack As Single
            Get
                Return Flt("NAM9\Face morph\Brows Farward/Back")
            End Get
            Set(value As Single)
                Escribir("NAM9\Face morph\Brows Farward/Back", value)
            End Set
        End Property

        ''' <summary>NAM9\Face morph\Lips Up/Down</summary>
        Public Property FaceMorphLipsUpDown As Single
            Get
                Return Flt("NAM9\Face morph\Lips Up/Down")
            End Get
            Set(value As Single)
                Escribir("NAM9\Face morph\Lips Up/Down", value)
            End Set
        End Property

        ''' <summary>NAM9\Face morph\Lips In/Out</summary>
        Public Property FaceMorphLipsInOut As Single
            Get
                Return Flt("NAM9\Face morph\Lips In/Out")
            End Get
            Set(value As Single)
                Escribir("NAM9\Face morph\Lips In/Out", value)
            End Set
        End Property

        ''' <summary>NAM9\Face morph\Chin Narrow/Wide</summary>
        Public Property FaceMorphChinNarrowWide As Single
            Get
                Return Flt("NAM9\Face morph\Chin Narrow/Wide")
            End Get
            Set(value As Single)
                Escribir("NAM9\Face morph\Chin Narrow/Wide", value)
            End Set
        End Property

        ''' <summary>NAM9\Face morph\Chin Up/Down</summary>
        Public Property FaceMorphChinUpDown As Single
            Get
                Return Flt("NAM9\Face morph\Chin Up/Down")
            End Get
            Set(value As Single)
                Escribir("NAM9\Face morph\Chin Up/Down", value)
            End Set
        End Property

        ''' <summary>NAM9\Face morph\Chin Underbite/Overbite</summary>
        Public Property FaceMorphChinUnderbiteOverbite As Single
            Get
                Return Flt("NAM9\Face morph\Chin Underbite/Overbite")
            End Get
            Set(value As Single)
                Escribir("NAM9\Face morph\Chin Underbite/Overbite", value)
            End Set
        End Property

        ''' <summary>NAM9\Face morph\Eyes Farward/Back</summary>
        Public Property FaceMorphEyesFarwardBack As Single
            Get
                Return Flt("NAM9\Face morph\Eyes Farward/Back")
            End Get
            Set(value As Single)
                Escribir("NAM9\Face morph\Eyes Farward/Back", value)
            End Set
        End Property

        ''' <summary>NAM9\Face morph\VampireMorph</summary>
        Public Property FaceMorphVampireMorph As Single
            Get
                Return Flt("NAM9\Face morph\VampireMorph")
            End Get
            Set(value As Single)
                Escribir("NAM9\Face morph\VampireMorph", value)
            End Set
        End Property

        ''' <summary>NAMA\Face parts\Nose</summary>
        Public Property FacePartsNose As UInteger
            Get
                Return CUInt(Entero("NAMA\Face parts\Nose"))
            End Get
            Set(value As UInteger)
                Escribir("NAMA\Face parts\Nose", CLng(value))
            End Set
        End Property

        ''' <summary>NAMA\Face parts\Eyes</summary>
        Public Property FacePartsEyes As UInteger
            Get
                Return CUInt(Entero("NAMA\Face parts\Eyes"))
            End Get
            Set(value As UInteger)
                Escribir("NAMA\Face parts\Eyes", CLng(value))
            End Set
        End Property

        ''' <summary>NAMA\Face parts\Mouth</summary>
        Public Property FacePartsMouth As UInteger
            Get
                Return CUInt(Entero("NAMA\Face parts\Mouth"))
            End Get
            Set(value As UInteger)
                Escribir("NAMA\Face parts\Mouth", CLng(value))
            End Set
        End Property

        ''' <summary>VMAD\Virtual Machine Adapter\Scripts</summary>
        Public ReadOnly Property Scripts As IReadOnlyList(Of NpcSSE_Scripts)
            Get
                Return Elementos(Of NpcSSE_Scripts)("VMAD\Virtual Machine Adapter\Scripts", Function(n) New NpcSSE_Scripts(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>VMAD\Virtual Machine Adapter\Scripts\Script\Properties</summary>
        Public ReadOnly Property Properties As IReadOnlyList(Of NpcSSE_Properties)
            Get
                Return Elementos(Of NpcSSE_Properties)("VMAD\Virtual Machine Adapter\Scripts\Script\Properties", Function(n) New NpcSSE_Properties(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Factions</summary>
        Public ReadOnly Property Factions As IReadOnlyList(Of NpcSSE_Factions)
            Get
                Return Elementos(Of NpcSSE_Factions)("Factions", Function(n) New NpcSSE_Factions(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Actor Effects</summary>
        Public ReadOnly Property ActorEffects As IReadOnlyList(Of NpcSSE_ActorEffects)
            Get
                Return Elementos(Of NpcSSE_ActorEffects)("Actor Effects", Function(n) New NpcSSE_ActorEffects(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Destructible\Stages</summary>
        Public ReadOnly Property Stages As IReadOnlyList(Of NpcSSE_Stages)
            Get
                Return Elementos(Of NpcSSE_Stages)("Destructible\Stages", Function(n) New NpcSSE_Stages(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Destructible\Stages\Stage\Model\DMDS\Alternate Textures</summary>
        Public ReadOnly Property AlternateTextures As IReadOnlyList(Of NpcSSE_AlternateTextures)
            Get
                Return Elementos(Of NpcSSE_AlternateTextures)("Destructible\Stages\Stage\Model\DMDS\Alternate Textures", Function(n) New NpcSSE_AlternateTextures(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Attacks</summary>
        Public ReadOnly Property Attacks As IReadOnlyList(Of NpcSSE_Attacks)
            Get
                Return Elementos(Of NpcSSE_Attacks)("Attacks", Function(n) New NpcSSE_Attacks(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Perks</summary>
        Public ReadOnly Property Perks As IReadOnlyList(Of NpcSSE_Perks)
            Get
                Return Elementos(Of NpcSSE_Perks)("Perks", Function(n) New NpcSSE_Perks(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Items</summary>
        Public ReadOnly Property Items As IReadOnlyList(Of NpcSSE_Items)
            Get
                Return Elementos(Of NpcSSE_Items)("Items", Function(n) New NpcSSE_Items(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Packages</summary>
        Public ReadOnly Property Packages As IReadOnlyList(Of NpcSSE_Packages)
            Get
                Return Elementos(Of NpcSSE_Packages)("Packages", Function(n) New NpcSSE_Packages(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Keywords\KWDA\Keywords</summary>
        Public ReadOnly Property Keywords As IReadOnlyList(Of NpcSSE_Keywords)
            Get
                Return Elementos(Of NpcSSE_Keywords)("Keywords\KWDA\Keywords", Function(n) New NpcSSE_Keywords(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>DNAM\Player Skills\Skill Values</summary>
        Public ReadOnly Property SkillValues As IReadOnlyList(Of NpcSSE_SkillValues)
            Get
                Return Elementos(Of NpcSSE_SkillValues)("DNAM\Player Skills\Skill Values", Function(n) New NpcSSE_SkillValues(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>DNAM\Player Skills\Skill Offsets</summary>
        Public ReadOnly Property SkillOffsets As IReadOnlyList(Of NpcSSE_SkillOffsets)
            Get
                Return Elementos(Of NpcSSE_SkillOffsets)("DNAM\Player Skills\Skill Offsets", Function(n) New NpcSSE_SkillOffsets(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Head Parts</summary>
        Public ReadOnly Property HeadParts As IReadOnlyList(Of NpcSSE_HeadParts)
            Get
                Return Elementos(Of NpcSSE_HeadParts)("Head Parts", Function(n) New NpcSSE_HeadParts(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Sound Types</summary>
        Public ReadOnly Property SoundTypes As IReadOnlyList(Of NpcSSE_SoundTypes)
            Get
                Return Elementos(Of NpcSSE_SoundTypes)("Sound Types", Function(n) New NpcSSE_SoundTypes(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Sound Types\Sound Type\Sounds</summary>
        Public ReadOnly Property Sounds As IReadOnlyList(Of NpcSSE_Sounds)
            Get
                Return Elementos(Of NpcSSE_Sounds)("Sound Types\Sound Type\Sounds", Function(n) New NpcSSE_Sounds(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Tint Layers</summary>
        Public ReadOnly Property TintLayers As IReadOnlyList(Of NpcSSE_TintLayers)
            Get
                Return Elementos(Of NpcSSE_TintLayers)("Tint Layers", Function(n) New NpcSSE_TintLayers(n, Context, Resolver))
            End Get
        End Property

    End Class

    ''' <summary>Un elemento de VMAD\Virtual Machine Adapter\Scripts.</summary>
    Public NotInheritable Class NpcSSE_Scripts
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Script\Flags</summary>
        Public Property ScriptFlags As Byte
            Get
                Return CByte(Entero("Script\Flags"))
            End Get
            Set(value As Byte)
                Escribir("Script\Flags", CLng(value))
            End Set
        End Property
        ''' <summary>Nombre del valor de Script\Flags.</summary>
        Public ReadOnly Property ScriptFlagsNombre As String
            Get
                Return NombreDeValor("Script\Flags")
            End Get
        End Property


    End Class

    ''' <summary>Un elemento de VMAD\Virtual Machine Adapter\Scripts\Script\Properties.</summary>
    Public NotInheritable Class NpcSSE_Properties
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Property\Type</summary>
        Public Property PropertyType As Byte
            Get
                Return CByte(Entero("Property\Type"))
            End Get
            Set(value As Byte)
                Escribir("Property\Type", CLng(value))
            End Set
        End Property

        ''' <summary>Property\Flags</summary>
        Public Property PropertyFlags As Byte
            Get
                Return CByte(Entero("Property\Flags"))
            End Get
            Set(value As Byte)
                Escribir("Property\Flags", CLng(value))
            End Set
        End Property
        ''' <summary>Nombre del valor de Property\Flags.</summary>
        Public ReadOnly Property PropertyFlagsNombre As String
            Get
                Return NombreDeValor("Property\Flags")
            End Get
        End Property


        ''' <summary>Property\Object v2\Alias</summary>
        Public Property ObjectV2Alias As Short
            Get
                Return CShort(Entero("Property\Object v2\Alias"))
            End Get
            Set(value As Short)
                Escribir("Property\Object v2\Alias", CLng(value))
            End Set
        End Property

        ''' <summary>Property\Object v2\FormID. Referencia en el espacio del orden de carga.</summary>
        Public Property ObjectV2FormID As UInteger
            Get
                Return Referencia("Property\Object v2\FormID")
            End Get
            Set(value As UInteger)
                PonerReferencia("Property\Object v2\FormID", value)
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Factions.</summary>
    Public NotInheritable Class NpcSSE_Factions
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>SNAM\Faction\Faction  -&gt;  FACT. Referencia en el espacio del orden de carga.</summary>
        Public Property FactionFaction As UInteger
            Get
                Return Referencia("SNAM\Faction\Faction")
            End Get
            Set(value As UInteger)
                PonerReferencia("SNAM\Faction\Faction", value)
            End Set
        End Property

        ''' <summary>SNAM\Faction\Rank</summary>
        Public Property FactionRank As SByte
            Get
                Return CSByte(Entero("SNAM\Faction\Rank"))
            End Get
            Set(value As SByte)
                Escribir("SNAM\Faction\Rank", CLng(value))
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Actor Effects.</summary>
    Public NotInheritable Class NpcSSE_ActorEffects
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>SPLO\Actor Effect  -&gt;  SPEL / SHOU / LVSP. Referencia en el espacio del orden de carga.</summary>
        Public Property ActorEffect As UInteger
            Get
                Return Referencia("SPLO\Actor Effect")
            End Get
            Set(value As UInteger)
                PonerReferencia("SPLO\Actor Effect", value)
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Destructible\Stages.</summary>
    Public NotInheritable Class NpcSSE_Stages
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Stage\DSTD\Destruction Stage Data\Health %</summary>
        Public Property DestructionStageDataHealth As Byte
            Get
                Return CByte(Entero("Stage\DSTD\Destruction Stage Data\Health %"))
            End Get
            Set(value As Byte)
                Escribir("Stage\DSTD\Destruction Stage Data\Health %", CLng(value))
            End Set
        End Property

        ''' <summary>Stage\DSTD\Destruction Stage Data\Index</summary>
        Public Property DestructionStageDataIndex As Byte
            Get
                Return CByte(Entero("Stage\DSTD\Destruction Stage Data\Index"))
            End Get
            Set(value As Byte)
                Escribir("Stage\DSTD\Destruction Stage Data\Index", CLng(value))
            End Set
        End Property

        ''' <summary>Stage\DSTD\Destruction Stage Data\Model Damage Stage</summary>
        Public Property DestructionStageDataModelDamageStage As Byte
            Get
                Return CByte(Entero("Stage\DSTD\Destruction Stage Data\Model Damage Stage"))
            End Get
            Set(value As Byte)
                Escribir("Stage\DSTD\Destruction Stage Data\Model Damage Stage", CLng(value))
            End Set
        End Property

        ''' <summary>Stage\DSTD\Destruction Stage Data\Flags</summary>
        Public Property DestructionStageDataFlags As Byte
            Get
                Return CByte(Entero("Stage\DSTD\Destruction Stage Data\Flags"))
            End Get
            Set(value As Byte)
                Escribir("Stage\DSTD\Destruction Stage Data\Flags", CLng(value))
            End Set
        End Property

        ''' <summary>Bit 0 de Stage\DSTD\Destruction Stage Data\Flags: Cap Damage</summary>
        Public Property DestructionStageDataFlagsCapDamage As Boolean
            Get
                Return Bit("Stage\DSTD\Destruction Stage Data\Flags", 0)
            End Get
            Set(value As Boolean)
                PonerBit("Stage\DSTD\Destruction Stage Data\Flags", 0, value)
            End Set
        End Property
        ''' <summary>Bit 1 de Stage\DSTD\Destruction Stage Data\Flags: Disable</summary>
        Public Property DestructionStageDataFlagsDisable As Boolean
            Get
                Return Bit("Stage\DSTD\Destruction Stage Data\Flags", 1)
            End Get
            Set(value As Boolean)
                PonerBit("Stage\DSTD\Destruction Stage Data\Flags", 1, value)
            End Set
        End Property
        ''' <summary>Bit 2 de Stage\DSTD\Destruction Stage Data\Flags: Destroy</summary>
        Public Property DestructionStageDataFlagsDestroy As Boolean
            Get
                Return Bit("Stage\DSTD\Destruction Stage Data\Flags", 2)
            End Get
            Set(value As Boolean)
                PonerBit("Stage\DSTD\Destruction Stage Data\Flags", 2, value)
            End Set
        End Property
        ''' <summary>Bit 3 de Stage\DSTD\Destruction Stage Data\Flags: Ignore External Dmg</summary>
        Public Property DestructionStageDataFlagsIgnoreExternalDmg As Boolean
            Get
                Return Bit("Stage\DSTD\Destruction Stage Data\Flags", 3)
            End Get
            Set(value As Boolean)
                PonerBit("Stage\DSTD\Destruction Stage Data\Flags", 3, value)
            End Set
        End Property

        ''' <summary>Stage\DSTD\Destruction Stage Data\Self Damage per Second</summary>
        Public Property DestructionStageDataSelfDamagePerSecond As Integer
            Get
                Return CInt(Entero("Stage\DSTD\Destruction Stage Data\Self Damage per Second"))
            End Get
            Set(value As Integer)
                Escribir("Stage\DSTD\Destruction Stage Data\Self Damage per Second", CLng(value))
            End Set
        End Property

        ''' <summary>Stage\DSTD\Destruction Stage Data\Explosion  -&gt;  EXPL / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property DestructionStageDataExplosion As UInteger
            Get
                Return Referencia("Stage\DSTD\Destruction Stage Data\Explosion")
            End Get
            Set(value As UInteger)
                PonerReferencia("Stage\DSTD\Destruction Stage Data\Explosion", value)
            End Set
        End Property

        ''' <summary>Stage\DSTD\Destruction Stage Data\Debris  -&gt;  DEBR / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property DestructionStageDataDebris As UInteger
            Get
                Return Referencia("Stage\DSTD\Destruction Stage Data\Debris")
            End Get
            Set(value As UInteger)
                PonerReferencia("Stage\DSTD\Destruction Stage Data\Debris", value)
            End Set
        End Property

        ''' <summary>Stage\DSTD\Destruction Stage Data\Debris Count</summary>
        Public Property DestructionStageDataDebrisCount As Integer
            Get
                Return CInt(Entero("Stage\DSTD\Destruction Stage Data\Debris Count"))
            End Get
            Set(value As Integer)
                Escribir("Stage\DSTD\Destruction Stage Data\Debris Count", CLng(value))
            End Set
        End Property

        ''' <summary>Stage\Model\DMDL\Model FileName</summary>
        Public Property ModelModelFileName As String
            Get
                Return Txt("Stage\Model\DMDL\Model FileName")
            End Get
            Set(value As String)
                Escribir("Stage\Model\DMDL\Model FileName", value)
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Destructible\Stages\Stage\Model\DMDS\Alternate Textures.</summary>
    Public NotInheritable Class NpcSSE_AlternateTextures
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Alternate Texture\New Texture  -&gt;  TXST. Referencia en el espacio del orden de carga.</summary>
        Public Property AlternateTextureNewTexture As UInteger
            Get
                Return Referencia("Alternate Texture\New Texture")
            End Get
            Set(value As UInteger)
                PonerReferencia("Alternate Texture\New Texture", value)
            End Set
        End Property

        ''' <summary>Alternate Texture\3D Index</summary>
        Public Property AlternateTexture3DIndex As Integer
            Get
                Return CInt(Entero("Alternate Texture\3D Index"))
            End Get
            Set(value As Integer)
                Escribir("Alternate Texture\3D Index", CLng(value))
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Attacks.</summary>
    Public NotInheritable Class NpcSSE_Attacks
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Attack\ATKD\Attack Data\Damage Mult</summary>
        Public Property AttackDataDamageMult As Single
            Get
                Return Flt("Attack\ATKD\Attack Data\Damage Mult")
            End Get
            Set(value As Single)
                Escribir("Attack\ATKD\Attack Data\Damage Mult", value)
            End Set
        End Property

        ''' <summary>Attack\ATKD\Attack Data\Attack Chance</summary>
        Public Property AttackDataAttackChance As Single
            Get
                Return Flt("Attack\ATKD\Attack Data\Attack Chance")
            End Get
            Set(value As Single)
                Escribir("Attack\ATKD\Attack Data\Attack Chance", value)
            End Set
        End Property

        ''' <summary>Attack\ATKD\Attack Data\Attack Spell  -&gt;  SPEL / SHOU / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property AttackDataAttackSpell As UInteger
            Get
                Return Referencia("Attack\ATKD\Attack Data\Attack Spell")
            End Get
            Set(value As UInteger)
                PonerReferencia("Attack\ATKD\Attack Data\Attack Spell", value)
            End Set
        End Property

        ''' <summary>Attack\ATKD\Attack Data\Attack Flags</summary>
        Public Property AttackDataAttackFlags As UInteger
            Get
                Return CUInt(Entero("Attack\ATKD\Attack Data\Attack Flags"))
            End Get
            Set(value As UInteger)
                Escribir("Attack\ATKD\Attack Data\Attack Flags", CLng(value))
            End Set
        End Property

        ''' <summary>Bit 0 de Attack\ATKD\Attack Data\Attack Flags: Ignore Weapon</summary>
        Public Property AttackDataAttackFlagsIgnoreWeapon As Boolean
            Get
                Return Bit("Attack\ATKD\Attack Data\Attack Flags", 0)
            End Get
            Set(value As Boolean)
                PonerBit("Attack\ATKD\Attack Data\Attack Flags", 0, value)
            End Set
        End Property
        ''' <summary>Bit 1 de Attack\ATKD\Attack Data\Attack Flags: Bash Attack</summary>
        Public Property AttackDataAttackFlagsBashAttack As Boolean
            Get
                Return Bit("Attack\ATKD\Attack Data\Attack Flags", 1)
            End Get
            Set(value As Boolean)
                PonerBit("Attack\ATKD\Attack Data\Attack Flags", 1, value)
            End Set
        End Property
        ''' <summary>Bit 2 de Attack\ATKD\Attack Data\Attack Flags: Power Attack</summary>
        Public Property AttackDataAttackFlagsPowerAttack As Boolean
            Get
                Return Bit("Attack\ATKD\Attack Data\Attack Flags", 2)
            End Get
            Set(value As Boolean)
                PonerBit("Attack\ATKD\Attack Data\Attack Flags", 2, value)
            End Set
        End Property
        ''' <summary>Bit 3 de Attack\ATKD\Attack Data\Attack Flags: Left Attack</summary>
        Public Property AttackDataAttackFlagsLeftAttack As Boolean
            Get
                Return Bit("Attack\ATKD\Attack Data\Attack Flags", 3)
            End Get
            Set(value As Boolean)
                PonerBit("Attack\ATKD\Attack Data\Attack Flags", 3, value)
            End Set
        End Property
        ''' <summary>Bit 4 de Attack\ATKD\Attack Data\Attack Flags: Rotating Attack</summary>
        Public Property AttackDataAttackFlagsRotatingAttack As Boolean
            Get
                Return Bit("Attack\ATKD\Attack Data\Attack Flags", 4)
            End Get
            Set(value As Boolean)
                PonerBit("Attack\ATKD\Attack Data\Attack Flags", 4, value)
            End Set
        End Property
        ''' <summary>Bit 31 de Attack\ATKD\Attack Data\Attack Flags: Override Data</summary>
        Public Property AttackDataAttackFlagsOverrideData As Boolean
            Get
                Return Bit("Attack\ATKD\Attack Data\Attack Flags", 31)
            End Get
            Set(value As Boolean)
                PonerBit("Attack\ATKD\Attack Data\Attack Flags", 31, value)
            End Set
        End Property

        ''' <summary>Attack\ATKD\Attack Data\Attack Angle</summary>
        Public Property AttackDataAttackAngle As Single
            Get
                Return Flt("Attack\ATKD\Attack Data\Attack Angle")
            End Get
            Set(value As Single)
                Escribir("Attack\ATKD\Attack Data\Attack Angle", value)
            End Set
        End Property

        ''' <summary>Attack\ATKD\Attack Data\Strike Angle</summary>
        Public Property AttackDataStrikeAngle As Single
            Get
                Return Flt("Attack\ATKD\Attack Data\Strike Angle")
            End Get
            Set(value As Single)
                Escribir("Attack\ATKD\Attack Data\Strike Angle", value)
            End Set
        End Property

        ''' <summary>Attack\ATKD\Attack Data\Stagger</summary>
        Public Property AttackDataStagger As Single
            Get
                Return Flt("Attack\ATKD\Attack Data\Stagger")
            End Get
            Set(value As Single)
                Escribir("Attack\ATKD\Attack Data\Stagger", value)
            End Set
        End Property

        ''' <summary>Attack\ATKD\Attack Data\Attack Type  -&gt;  KYWD / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property AttackDataAttackType As UInteger
            Get
                Return Referencia("Attack\ATKD\Attack Data\Attack Type")
            End Get
            Set(value As UInteger)
                PonerReferencia("Attack\ATKD\Attack Data\Attack Type", value)
            End Set
        End Property

        ''' <summary>Attack\ATKD\Attack Data\Knockdown</summary>
        Public Property AttackDataKnockdown As Single
            Get
                Return Flt("Attack\ATKD\Attack Data\Knockdown")
            End Get
            Set(value As Single)
                Escribir("Attack\ATKD\Attack Data\Knockdown", value)
            End Set
        End Property

        ''' <summary>Attack\ATKD\Attack Data\Recovery Time</summary>
        Public Property AttackDataRecoveryTime As Single
            Get
                Return Flt("Attack\ATKD\Attack Data\Recovery Time")
            End Get
            Set(value As Single)
                Escribir("Attack\ATKD\Attack Data\Recovery Time", value)
            End Set
        End Property

        ''' <summary>Attack\ATKD\Attack Data\Stamina Mult</summary>
        Public Property AttackDataStaminaMult As Single
            Get
                Return Flt("Attack\ATKD\Attack Data\Stamina Mult")
            End Get
            Set(value As Single)
                Escribir("Attack\ATKD\Attack Data\Stamina Mult", value)
            End Set
        End Property

        ''' <summary>Attack\ATKE\Attack Event</summary>
        Public Property AttackAttackEvent As String
            Get
                Return Txt("Attack\ATKE\Attack Event")
            End Get
            Set(value As String)
                Escribir("Attack\ATKE\Attack Event", value)
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Perks.</summary>
    Public NotInheritable Class NpcSSE_Perks
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>PRKR\Perk\Perk  -&gt;  PERK. Referencia en el espacio del orden de carga.</summary>
        Public Property PerkPerk As UInteger
            Get
                Return Referencia("PRKR\Perk\Perk")
            End Get
            Set(value As UInteger)
                PonerReferencia("PRKR\Perk\Perk", value)
            End Set
        End Property

        ''' <summary>PRKR\Perk\Rank</summary>
        Public Property PerkRank As Byte
            Get
                Return CByte(Entero("PRKR\Perk\Rank"))
            End Get
            Set(value As Byte)
                Escribir("PRKR\Perk\Rank", CLng(value))
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Items.</summary>
    Public NotInheritable Class NpcSSE_Items
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Item\CNTO\Item\Item  -&gt;  ARMO / AMMO / APPA / MISC / WEAP / BOOK / LVLI / KEYM / ALCH / INGR / LIGH / SLGM / SCRL. Referencia en el espacio del orden de carga.</summary>
        Public Property ItemItem As UInteger
            Get
                Return Referencia("Item\CNTO\Item\Item")
            End Get
            Set(value As UInteger)
                PonerReferencia("Item\CNTO\Item\Item", value)
            End Set
        End Property

        ''' <summary>Item\CNTO\Item\Count</summary>
        Public Property ItemCount As Integer
            Get
                Return CInt(Entero("Item\CNTO\Item\Count"))
            End Get
            Set(value As Integer)
                Escribir("Item\CNTO\Item\Count", CLng(value))
            End Set
        End Property

        ''' <summary>Item\COED\Extra Data\Owner  -&gt;  NPC_ / FACT / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property ExtraDataOwner As UInteger
            Get
                Return Referencia("Item\COED\Extra Data\Owner")
            End Get
            Set(value As UInteger)
                PonerReferencia("Item\COED\Extra Data\Owner", value)
            End Set
        End Property

        ''' <summary>Item\COED\Extra Data\Item Condition</summary>
        Public Property ExtraDataItemCondition As Single
            Get
                Return Flt("Item\COED\Extra Data\Item Condition")
            End Get
            Set(value As Single)
                Escribir("Item\COED\Extra Data\Item Condition", value)
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Packages.</summary>
    Public NotInheritable Class NpcSSE_Packages
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>PKID\Package  -&gt;  PACK. Referencia en el espacio del orden de carga.</summary>
        Public Property Package As UInteger
            Get
                Return Referencia("PKID\Package")
            End Get
            Set(value As UInteger)
                PonerReferencia("PKID\Package", value)
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Keywords\KWDA\Keywords.</summary>
    Public NotInheritable Class NpcSSE_Keywords
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Keyword  -&gt;  KYWD / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property Keyword As UInteger
            Get
                Return Referencia("Keyword")
            End Get
            Set(value As UInteger)
                PonerReferencia("Keyword", value)
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de DNAM\Player Skills\Skill Values.</summary>
    Public NotInheritable Class NpcSSE_SkillValues
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Skill</summary>
        Public Property Skill As Byte
            Get
                Return CByte(Entero("Skill"))
            End Get
            Set(value As Byte)
                Escribir("Skill", CLng(value))
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de DNAM\Player Skills\Skill Offsets.</summary>
    Public NotInheritable Class NpcSSE_SkillOffsets
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Skill</summary>
        Public Property Skill As Byte
            Get
                Return CByte(Entero("Skill"))
            End Get
            Set(value As Byte)
                Escribir("Skill", CLng(value))
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Head Parts.</summary>
    Public NotInheritable Class NpcSSE_HeadParts
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>PNAM\Head Part  -&gt;  HDPT. Referencia en el espacio del orden de carga.</summary>
        Public Property HeadPart As UInteger
            Get
                Return Referencia("PNAM\Head Part")
            End Get
            Set(value As UInteger)
                PonerReferencia("PNAM\Head Part", value)
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Sound Types.</summary>
    Public NotInheritable Class NpcSSE_SoundTypes
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Sound Type\CSDT\Type</summary>
        Public Property SoundTypeType As UInteger
            Get
                Return CUInt(Entero("Sound Type\CSDT\Type"))
            End Get
            Set(value As UInteger)
                Escribir("Sound Type\CSDT\Type", CLng(value))
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Sound Types\Sound Type\Sounds.</summary>
    Public NotInheritable Class NpcSSE_Sounds
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Sound\CSDI\Sound  -&gt;  SNDR / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property SoundSound As UInteger
            Get
                Return Referencia("Sound\CSDI\Sound")
            End Get
            Set(value As UInteger)
                PonerReferencia("Sound\CSDI\Sound", value)
            End Set
        End Property

        ''' <summary>Sound\CSDC\Sound Chance</summary>
        Public Property SoundSoundChance As Byte
            Get
                Return CByte(Entero("Sound\CSDC\Sound Chance"))
            End Get
            Set(value As Byte)
                Escribir("Sound\CSDC\Sound Chance", CLng(value))
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Tint Layers.</summary>
    Public NotInheritable Class NpcSSE_TintLayers
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Layer\TINI\Tint Index</summary>
        Public Property LayerTintIndex As UShort
            Get
                Return CUShort(Entero("Layer\TINI\Tint Index"))
            End Get
            Set(value As UShort)
                Escribir("Layer\TINI\Tint Index", CLng(value))
            End Set
        End Property

        ''' <summary>Layer\TINC\Tint Color\Red</summary>
        Public Property TintColorRed As Byte
            Get
                Return CByte(Entero("Layer\TINC\Tint Color\Red"))
            End Get
            Set(value As Byte)
                Escribir("Layer\TINC\Tint Color\Red", CLng(value))
            End Set
        End Property

        ''' <summary>Layer\TINC\Tint Color\Green</summary>
        Public Property TintColorGreen As Byte
            Get
                Return CByte(Entero("Layer\TINC\Tint Color\Green"))
            End Get
            Set(value As Byte)
                Escribir("Layer\TINC\Tint Color\Green", CLng(value))
            End Set
        End Property

        ''' <summary>Layer\TINC\Tint Color\Blue</summary>
        Public Property TintColorBlue As Byte
            Get
                Return CByte(Entero("Layer\TINC\Tint Color\Blue"))
            End Get
            Set(value As Byte)
                Escribir("Layer\TINC\Tint Color\Blue", CLng(value))
            End Set
        End Property

        ''' <summary>Layer\TINC\Tint Color\Alpha</summary>
        Public Property TintColorAlpha As Byte
            Get
                Return CByte(Entero("Layer\TINC\Tint Color\Alpha"))
            End Get
            Set(value As Byte)
                Escribir("Layer\TINC\Tint Color\Alpha", CLng(value))
            End Set
        End Property

        ''' <summary>Layer\TINV\Interpolation Value</summary>
        Public Property LayerInterpolationValue As UInteger
            Get
                Return CUInt(Entero("Layer\TINV\Interpolation Value"))
            End Get
            Set(value As UInteger)
                Escribir("Layer\TINV\Interpolation Value", CLng(value))
            End Set
        End Property

        ''' <summary>Layer\TIAS\Preset</summary>
        Public Property LayerPreset As Short
            Get
                Return CShort(Entero("Layer\TIAS\Preset"))
            End Get
            Set(value As Short)
                Escribir("Layer\TIAS\Preset", CLng(value))
            End Set
        End Property

    End Class

    ''' <summary>Campos de un record OTFT de Skyrim.</summary>
    Public NotInheritable Class OtftSSE
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>EDID\Editor ID</summary>
        Public Property EditorID As String
            Get
                Return Txt("EDID\Editor ID")
            End Get
            Set(value As String)
                Escribir("EDID\Editor ID", value)
            End Set
        End Property

        ''' <summary>INAM\Items</summary>
        Public ReadOnly Property Items As IReadOnlyList(Of OtftSSE_Items)
            Get
                Return Elementos(Of OtftSSE_Items)("INAM\Items", Function(n) New OtftSSE_Items(n, Context, Resolver))
            End Get
        End Property

    End Class

    ''' <summary>Un elemento de INAM\Items.</summary>
    Public NotInheritable Class OtftSSE_Items
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Item  -&gt;  ARMO / LVLI. Referencia en el espacio del orden de carga.</summary>
        Public Property Item As UInteger
            Get
                Return Referencia("Item")
            End Get
            Set(value As UInteger)
                PonerReferencia("Item", value)
            End Set
        End Property

    End Class

    ''' <summary>Campos de un record RACE de Skyrim.</summary>
    Public NotInheritable Class RaceSSE
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>EDID\Editor ID</summary>
        Public Property EditorID As String
            Get
                Return Txt("EDID\Editor ID")
            End Get
            Set(value As String)
                Escribir("EDID\Editor ID", value)
            End Set
        End Property

        ''' <summary>SPCT\Count</summary>
        Public Property Count As UInteger
            Get
                Return CUInt(Entero("SPCT\Count"))
            End Get
            Set(value As UInteger)
                Escribir("SPCT\Count", CLng(value))
            End Set
        End Property

        ''' <summary>WNAM\Skin  -&gt;  ARMO / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property Skin As UInteger
            Get
                Return Referencia("WNAM\Skin")
            End Get
            Set(value As UInteger)
                PonerReferencia("WNAM\Skin", value)
            End Set
        End Property

        ''' <summary>BOD2\Biped Body Template\First Person Flags</summary>
        Public Property BipedBodyTemplateFirstPersonFlags As UInteger
            Get
                Return CUInt(Entero("BOD2\Biped Body Template\First Person Flags"))
            End Get
            Set(value As UInteger)
                Escribir("BOD2\Biped Body Template\First Person Flags", CLng(value))
            End Set
        End Property

        ''' <summary>BOD2\Biped Body Template\General Flags</summary>
        Public Property BipedBodyTemplateGeneralFlags As Long
            Get
                Return CLng(Entero("BOD2\Biped Body Template\General Flags"))
            End Get
            Set(value As Long)
                Escribir("BOD2\Biped Body Template\General Flags", CLng(value))
            End Set
        End Property

        ''' <summary>Bit 0 de BOD2\Biped Body Template\General Flags: (ARMA)Modulates Voice</summary>
        Public Property BipedBodyTemplateGeneralFlagsARMAModulatesVoice As Boolean
            Get
                Return Bit("BOD2\Biped Body Template\General Flags", 0)
            End Get
            Set(value As Boolean)
                PonerBit("BOD2\Biped Body Template\General Flags", 0, value)
            End Set
        End Property
        ''' <summary>Bit 4 de BOD2\Biped Body Template\General Flags: (ARMO)Non-Playable</summary>
        Public Property BipedBodyTemplateGeneralFlagsARMONonPlayable As Boolean
            Get
                Return Bit("BOD2\Biped Body Template\General Flags", 4)
            End Get
            Set(value As Boolean)
                PonerBit("BOD2\Biped Body Template\General Flags", 4, value)
            End Set
        End Property

        ''' <summary>BOD2\Biped Body Template\Armor Type</summary>
        Public Property BipedBodyTemplateArmorType As UInteger
            Get
                Return CUInt(Entero("BOD2\Biped Body Template\Armor Type"))
            End Get
            Set(value As UInteger)
                Escribir("BOD2\Biped Body Template\Armor Type", CLng(value))
            End Set
        End Property

        ''' <summary>Keywords\KSIZ\Keyword Count</summary>
        Public Property KeywordsKeywordCount As UInteger
            Get
                Return CUInt(Entero("Keywords\KSIZ\Keyword Count"))
            End Get
            Set(value As UInteger)
                Escribir("Keywords\KSIZ\Keyword Count", CLng(value))
            End Set
        End Property

        ''' <summary>DATA\Male Height</summary>
        Public Property MaleHeight As Single
            Get
                Return Flt("DATA\Male Height")
            End Get
            Set(value As Single)
                Escribir("DATA\Male Height", value)
            End Set
        End Property

        ''' <summary>DATA\Female Height</summary>
        Public Property FemaleHeight As Single
            Get
                Return Flt("DATA\Female Height")
            End Get
            Set(value As Single)
                Escribir("DATA\Female Height", value)
            End Set
        End Property

        ''' <summary>DATA\Male Weight</summary>
        Public Property MaleWeight As Single
            Get
                Return Flt("DATA\Male Weight")
            End Get
            Set(value As Single)
                Escribir("DATA\Male Weight", value)
            End Set
        End Property

        ''' <summary>DATA\Female Weight</summary>
        Public Property FemaleWeight As Single
            Get
                Return Flt("DATA\Female Weight")
            End Get
            Set(value As Single)
                Escribir("DATA\Female Weight", value)
            End Set
        End Property

        ''' <summary>DATA\Flags</summary>
        Public Property Flags As UInteger
            Get
                Return CUInt(Entero("DATA\Flags"))
            End Get
            Set(value As UInteger)
                Escribir("DATA\Flags", CLng(value))
            End Set
        End Property

        ''' <summary>Bit 0 de DATA\Flags: Playable</summary>
        Public Property FlagsPlayable As Boolean
            Get
                Return Bit("DATA\Flags", 0)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Flags", 0, value)
            End Set
        End Property
        ''' <summary>Bit 1 de DATA\Flags: FaceGen Head</summary>
        Public Property FlagsFaceGenHead As Boolean
            Get
                Return Bit("DATA\Flags", 1)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Flags", 1, value)
            End Set
        End Property
        ''' <summary>Bit 2 de DATA\Flags: Child</summary>
        Public Property FlagsChild As Boolean
            Get
                Return Bit("DATA\Flags", 2)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Flags", 2, value)
            End Set
        End Property
        ''' <summary>Bit 3 de DATA\Flags: Tilt Front/Back</summary>
        Public Property FlagsTiltFrontBack As Boolean
            Get
                Return Bit("DATA\Flags", 3)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Flags", 3, value)
            End Set
        End Property
        ''' <summary>Bit 4 de DATA\Flags: Tilt Left/Right</summary>
        Public Property FlagsTiltLeftRight As Boolean
            Get
                Return Bit("DATA\Flags", 4)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Flags", 4, value)
            End Set
        End Property
        ''' <summary>Bit 5 de DATA\Flags: No Shadow</summary>
        Public Property FlagsNoShadow As Boolean
            Get
                Return Bit("DATA\Flags", 5)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Flags", 5, value)
            End Set
        End Property
        ''' <summary>Bit 6 de DATA\Flags: Swims</summary>
        Public Property FlagsSwims As Boolean
            Get
                Return Bit("DATA\Flags", 6)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Flags", 6, value)
            End Set
        End Property
        ''' <summary>Bit 7 de DATA\Flags: Flies</summary>
        Public Property FlagsFlies As Boolean
            Get
                Return Bit("DATA\Flags", 7)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Flags", 7, value)
            End Set
        End Property
        ''' <summary>Bit 8 de DATA\Flags: Walks</summary>
        Public Property FlagsWalks As Boolean
            Get
                Return Bit("DATA\Flags", 8)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Flags", 8, value)
            End Set
        End Property
        ''' <summary>Bit 9 de DATA\Flags: Immobile</summary>
        Public Property FlagsImmobile As Boolean
            Get
                Return Bit("DATA\Flags", 9)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Flags", 9, value)
            End Set
        End Property
        ''' <summary>Bit 10 de DATA\Flags: Not Pushable</summary>
        Public Property FlagsNotPushable As Boolean
            Get
                Return Bit("DATA\Flags", 10)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Flags", 10, value)
            End Set
        End Property
        ''' <summary>Bit 11 de DATA\Flags: No Combat In Water</summary>
        Public Property FlagsNoCombatInWater As Boolean
            Get
                Return Bit("DATA\Flags", 11)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Flags", 11, value)
            End Set
        End Property
        ''' <summary>Bit 12 de DATA\Flags: No Rotating to Head-Track</summary>
        Public Property FlagsNoRotatingToHeadTrack As Boolean
            Get
                Return Bit("DATA\Flags", 12)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Flags", 12, value)
            End Set
        End Property
        ''' <summary>Bit 13 de DATA\Flags: Don't Show Blood Spray</summary>
        Public Property FlagsDonTShowBloodSpray As Boolean
            Get
                Return Bit("DATA\Flags", 13)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Flags", 13, value)
            End Set
        End Property
        ''' <summary>Bit 14 de DATA\Flags: Don't Show Blood Decal</summary>
        Public Property FlagsDonTShowBloodDecal As Boolean
            Get
                Return Bit("DATA\Flags", 14)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Flags", 14, value)
            End Set
        End Property
        ''' <summary>Bit 15 de DATA\Flags: Uses Head Track Anims</summary>
        Public Property FlagsUsesHeadTrackAnims As Boolean
            Get
                Return Bit("DATA\Flags", 15)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Flags", 15, value)
            End Set
        End Property
        ''' <summary>Bit 16 de DATA\Flags: Spells Align w/Magic Node</summary>
        Public Property FlagsSpellsAlignWMagicNode As Boolean
            Get
                Return Bit("DATA\Flags", 16)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Flags", 16, value)
            End Set
        End Property
        ''' <summary>Bit 17 de DATA\Flags: Use World Raycasts For FootIK</summary>
        Public Property FlagsUseWorldRaycastsForFootIK As Boolean
            Get
                Return Bit("DATA\Flags", 17)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Flags", 17, value)
            End Set
        End Property
        ''' <summary>Bit 18 de DATA\Flags: Allow Ragdoll Collision</summary>
        Public Property FlagsAllowRagdollCollision As Boolean
            Get
                Return Bit("DATA\Flags", 18)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Flags", 18, value)
            End Set
        End Property
        ''' <summary>Bit 19 de DATA\Flags: Regen HP In Combat</summary>
        Public Property FlagsRegenHPInCombat As Boolean
            Get
                Return Bit("DATA\Flags", 19)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Flags", 19, value)
            End Set
        End Property
        ''' <summary>Bit 20 de DATA\Flags: Can't Open Doors</summary>
        Public Property FlagsCanTOpenDoors As Boolean
            Get
                Return Bit("DATA\Flags", 20)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Flags", 20, value)
            End Set
        End Property
        ''' <summary>Bit 21 de DATA\Flags: Allow PC Dialogue</summary>
        Public Property FlagsAllowPCDialogue As Boolean
            Get
                Return Bit("DATA\Flags", 21)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Flags", 21, value)
            End Set
        End Property
        ''' <summary>Bit 22 de DATA\Flags: No Knockdowns</summary>
        Public Property FlagsNoKnockdowns As Boolean
            Get
                Return Bit("DATA\Flags", 22)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Flags", 22, value)
            End Set
        End Property
        ''' <summary>Bit 23 de DATA\Flags: Allow Pickpocket</summary>
        Public Property FlagsAllowPickpocket As Boolean
            Get
                Return Bit("DATA\Flags", 23)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Flags", 23, value)
            End Set
        End Property
        ''' <summary>Bit 24 de DATA\Flags: Always Use Proxy Controller</summary>
        Public Property FlagsAlwaysUseProxyController As Boolean
            Get
                Return Bit("DATA\Flags", 24)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Flags", 24, value)
            End Set
        End Property
        ''' <summary>Bit 25 de DATA\Flags: Don't Show Weapon Blood</summary>
        Public Property FlagsDonTShowWeaponBlood As Boolean
            Get
                Return Bit("DATA\Flags", 25)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Flags", 25, value)
            End Set
        End Property
        ''' <summary>Bit 26 de DATA\Flags: Overlay Head Part List</summary>
        Public Property FlagsOverlayHeadPartList As Boolean
            Get
                Return Bit("DATA\Flags", 26)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Flags", 26, value)
            End Set
        End Property
        ''' <summary>Bit 27 de DATA\Flags: Override Head Part List</summary>
        Public Property FlagsOverrideHeadPartList As Boolean
            Get
                Return Bit("DATA\Flags", 27)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Flags", 27, value)
            End Set
        End Property
        ''' <summary>Bit 28 de DATA\Flags: Can Pickup Items</summary>
        Public Property FlagsCanPickupItems As Boolean
            Get
                Return Bit("DATA\Flags", 28)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Flags", 28, value)
            End Set
        End Property
        ''' <summary>Bit 29 de DATA\Flags: Allow Multiple Membrane Shaders</summary>
        Public Property FlagsAllowMultipleMembraneShaders As Boolean
            Get
                Return Bit("DATA\Flags", 29)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Flags", 29, value)
            End Set
        End Property
        ''' <summary>Bit 30 de DATA\Flags: Can Dual Wield</summary>
        Public Property FlagsCanDualWield As Boolean
            Get
                Return Bit("DATA\Flags", 30)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Flags", 30, value)
            End Set
        End Property
        ''' <summary>Bit 31 de DATA\Flags: Avoids Roads</summary>
        Public Property FlagsAvoidsRoads As Boolean
            Get
                Return Bit("DATA\Flags", 31)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Flags", 31, value)
            End Set
        End Property

        ''' <summary>DATA\Starting Health</summary>
        Public Property StartingHealth As Single
            Get
                Return Flt("DATA\Starting Health")
            End Get
            Set(value As Single)
                Escribir("DATA\Starting Health", value)
            End Set
        End Property

        ''' <summary>DATA\Starting Magicka</summary>
        Public Property StartingMagicka As Single
            Get
                Return Flt("DATA\Starting Magicka")
            End Get
            Set(value As Single)
                Escribir("DATA\Starting Magicka", value)
            End Set
        End Property

        ''' <summary>DATA\Starting Stamina</summary>
        Public Property StartingStamina As Single
            Get
                Return Flt("DATA\Starting Stamina")
            End Get
            Set(value As Single)
                Escribir("DATA\Starting Stamina", value)
            End Set
        End Property

        ''' <summary>DATA\Base Carry Weight</summary>
        Public Property BaseCarryWeight As Single
            Get
                Return Flt("DATA\Base Carry Weight")
            End Get
            Set(value As Single)
                Escribir("DATA\Base Carry Weight", value)
            End Set
        End Property

        ''' <summary>DATA\Base Mass</summary>
        Public Property BaseMass As Single
            Get
                Return Flt("DATA\Base Mass")
            End Get
            Set(value As Single)
                Escribir("DATA\Base Mass", value)
            End Set
        End Property

        ''' <summary>DATA\Acceleration rate</summary>
        Public Property AccelerationRate As Single
            Get
                Return Flt("DATA\Acceleration rate")
            End Get
            Set(value As Single)
                Escribir("DATA\Acceleration rate", value)
            End Set
        End Property

        ''' <summary>DATA\Deceleration rate</summary>
        Public Property DecelerationRate As Single
            Get
                Return Flt("DATA\Deceleration rate")
            End Get
            Set(value As Single)
                Escribir("DATA\Deceleration rate", value)
            End Set
        End Property

        ''' <summary>DATA\Size</summary>
        Public Property Size As UInteger
            Get
                Return CUInt(Entero("DATA\Size"))
            End Get
            Set(value As UInteger)
                Escribir("DATA\Size", CLng(value))
            End Set
        End Property
        ''' <summary>Nombre del valor de DATA\Size.</summary>
        Public ReadOnly Property SizeNombre As String
            Get
                Return NombreDeValor("DATA\Size")
            End Get
        End Property


        ''' <summary>DATA\Head Biped Object</summary>
        Public Property HeadBipedObject As Integer
            Get
                Return CInt(Entero("DATA\Head Biped Object"))
            End Get
            Set(value As Integer)
                Escribir("DATA\Head Biped Object", CLng(value))
            End Set
        End Property

        ''' <summary>DATA\Hair Biped Object</summary>
        Public Property HairBipedObject As Integer
            Get
                Return CInt(Entero("DATA\Hair Biped Object"))
            End Get
            Set(value As Integer)
                Escribir("DATA\Hair Biped Object", CLng(value))
            End Set
        End Property

        ''' <summary>DATA\Injured Health Pct</summary>
        Public Property InjuredHealthPct As Single
            Get
                Return Flt("DATA\Injured Health Pct")
            End Get
            Set(value As Single)
                Escribir("DATA\Injured Health Pct", value)
            End Set
        End Property

        ''' <summary>DATA\Shield Biped Object</summary>
        Public Property ShieldBipedObject As Integer
            Get
                Return CInt(Entero("DATA\Shield Biped Object"))
            End Get
            Set(value As Integer)
                Escribir("DATA\Shield Biped Object", CLng(value))
            End Set
        End Property

        ''' <summary>DATA\Health Regen</summary>
        Public Property HealthRegen As Single
            Get
                Return Flt("DATA\Health Regen")
            End Get
            Set(value As Single)
                Escribir("DATA\Health Regen", value)
            End Set
        End Property

        ''' <summary>DATA\Magicka Regen</summary>
        Public Property MagickaRegen As Single
            Get
                Return Flt("DATA\Magicka Regen")
            End Get
            Set(value As Single)
                Escribir("DATA\Magicka Regen", value)
            End Set
        End Property

        ''' <summary>DATA\Stamina Regen</summary>
        Public Property StaminaRegen As Single
            Get
                Return Flt("DATA\Stamina Regen")
            End Get
            Set(value As Single)
                Escribir("DATA\Stamina Regen", value)
            End Set
        End Property

        ''' <summary>DATA\Unarmed Damage</summary>
        Public Property UnarmedDamage As Single
            Get
                Return Flt("DATA\Unarmed Damage")
            End Get
            Set(value As Single)
                Escribir("DATA\Unarmed Damage", value)
            End Set
        End Property

        ''' <summary>DATA\Unarmed Reach</summary>
        Public Property UnarmedReach As Single
            Get
                Return Flt("DATA\Unarmed Reach")
            End Get
            Set(value As Single)
                Escribir("DATA\Unarmed Reach", value)
            End Set
        End Property

        ''' <summary>DATA\Body Biped Object</summary>
        Public Property BodyBipedObject As Integer
            Get
                Return CInt(Entero("DATA\Body Biped Object"))
            End Get
            Set(value As Integer)
                Escribir("DATA\Body Biped Object", CLng(value))
            End Set
        End Property

        ''' <summary>DATA\Aim Angle Tolerance</summary>
        Public Property AimAngleTolerance As Single
            Get
                Return Flt("DATA\Aim Angle Tolerance")
            End Get
            Set(value As Single)
                Escribir("DATA\Aim Angle Tolerance", value)
            End Set
        End Property

        ''' <summary>DATA\Flight Radius</summary>
        Public Property FlightRadius As Single
            Get
                Return Flt("DATA\Flight Radius")
            End Get
            Set(value As Single)
                Escribir("DATA\Flight Radius", value)
            End Set
        End Property

        ''' <summary>DATA\Angular Acceleration Rate</summary>
        Public Property AngularAccelerationRate As Single
            Get
                Return Flt("DATA\Angular Acceleration Rate")
            End Get
            Set(value As Single)
                Escribir("DATA\Angular Acceleration Rate", value)
            End Set
        End Property

        ''' <summary>DATA\Angular Tolerance</summary>
        Public Property AngularTolerance As Single
            Get
                Return Flt("DATA\Angular Tolerance")
            End Get
            Set(value As Single)
                Escribir("DATA\Angular Tolerance", value)
            End Set
        End Property

        ''' <summary>DATA\Flags 2</summary>
        Public Property Flags2 As UInteger
            Get
                Return CUInt(Entero("DATA\Flags 2"))
            End Get
            Set(value As UInteger)
                Escribir("DATA\Flags 2", CLng(value))
            End Set
        End Property

        ''' <summary>Bit 0 de DATA\Flags 2: Use Advanced Avoidance</summary>
        Public Property Flags2UseAdvancedAvoidance As Boolean
            Get
                Return Bit("DATA\Flags 2", 0)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Flags 2", 0, value)
            End Set
        End Property
        ''' <summary>Bit 1 de DATA\Flags 2: Non-Hostile</summary>
        Public Property Flags2NonHostile As Boolean
            Get
                Return Bit("DATA\Flags 2", 1)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Flags 2", 1, value)
            End Set
        End Property
        ''' <summary>Bit 4 de DATA\Flags 2: Allow Mounted Combat</summary>
        Public Property Flags2AllowMountedCombat As Boolean
            Get
                Return Bit("DATA\Flags 2", 4)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Flags 2", 4, value)
            End Set
        End Property

        ''' <summary>DATA\Mount Data\Mount Offset X</summary>
        Public Property MountDataMountOffsetX As Single
            Get
                Return Flt("DATA\Mount Data\Mount Offset X")
            End Get
            Set(value As Single)
                Escribir("DATA\Mount Data\Mount Offset X", value)
            End Set
        End Property

        ''' <summary>DATA\Mount Data\Mount Offset Y</summary>
        Public Property MountDataMountOffsetY As Single
            Get
                Return Flt("DATA\Mount Data\Mount Offset Y")
            End Get
            Set(value As Single)
                Escribir("DATA\Mount Data\Mount Offset Y", value)
            End Set
        End Property

        ''' <summary>DATA\Mount Data\Mount Offset Z</summary>
        Public Property MountDataMountOffsetZ As Single
            Get
                Return Flt("DATA\Mount Data\Mount Offset Z")
            End Get
            Set(value As Single)
                Escribir("DATA\Mount Data\Mount Offset Z", value)
            End Set
        End Property

        ''' <summary>DATA\Mount Data\Dismount Offset X</summary>
        Public Property MountDataDismountOffsetX As Single
            Get
                Return Flt("DATA\Mount Data\Dismount Offset X")
            End Get
            Set(value As Single)
                Escribir("DATA\Mount Data\Dismount Offset X", value)
            End Set
        End Property

        ''' <summary>DATA\Mount Data\Dismount Offset Y</summary>
        Public Property MountDataDismountOffsetY As Single
            Get
                Return Flt("DATA\Mount Data\Dismount Offset Y")
            End Get
            Set(value As Single)
                Escribir("DATA\Mount Data\Dismount Offset Y", value)
            End Set
        End Property

        ''' <summary>DATA\Mount Data\Dismount Offset Z</summary>
        Public Property MountDataDismountOffsetZ As Single
            Get
                Return Flt("DATA\Mount Data\Dismount Offset Z")
            End Get
            Set(value As Single)
                Escribir("DATA\Mount Data\Dismount Offset Z", value)
            End Set
        End Property

        ''' <summary>DATA\Mount Data\Mount Camera Offset X</summary>
        Public Property MountDataMountCameraOffsetX As Single
            Get
                Return Flt("DATA\Mount Data\Mount Camera Offset X")
            End Get
            Set(value As Single)
                Escribir("DATA\Mount Data\Mount Camera Offset X", value)
            End Set
        End Property

        ''' <summary>DATA\Mount Data\Mount Camera Offset Y</summary>
        Public Property MountDataMountCameraOffsetY As Single
            Get
                Return Flt("DATA\Mount Data\Mount Camera Offset Y")
            End Get
            Set(value As Single)
                Escribir("DATA\Mount Data\Mount Camera Offset Y", value)
            End Set
        End Property

        ''' <summary>DATA\Mount Data\Mount Camera Offset Z</summary>
        Public Property MountDataMountCameraOffsetZ As Single
            Get
                Return Flt("DATA\Mount Data\Mount Camera Offset Z")
            End Get
            Set(value As Single)
                Escribir("DATA\Mount Data\Mount Camera Offset Z", value)
            End Set
        End Property

        ''' <summary>ANAM\Male Skeletal Model</summary>
        Public Property MaleSkeletalModel As String
            Get
                Return Txt("ANAM\Male Skeletal Model")
            End Get
            Set(value As String)
                Escribir("ANAM\Male Skeletal Model", value)
            End Set
        End Property

        ''' <summary>ANAM\Female Skeletal Model</summary>
        Public Property FemaleSkeletalModel As String
            Get
                Return Txt("ANAM\Female Skeletal Model")
            End Get
            Set(value As String)
                Escribir("ANAM\Female Skeletal Model", value)
            End Set
        End Property

        ''' <summary>TINL\Total Number of Tints in List</summary>
        Public Property TotalNumberOfTintsInList As UShort
            Get
                Return CUShort(Entero("TINL\Total Number of Tints in List"))
            End Get
            Set(value As UShort)
                Escribir("TINL\Total Number of Tints in List", CLng(value))
            End Set
        End Property

        ''' <summary>PNAM\FaceGen - Main clamp</summary>
        Public Property FaceGenMainClamp As Single
            Get
                Return Flt("PNAM\FaceGen - Main clamp")
            End Get
            Set(value As Single)
                Escribir("PNAM\FaceGen - Main clamp", value)
            End Set
        End Property

        ''' <summary>UNAM\FaceGen - Face clamp</summary>
        Public Property FaceGenFaceClamp As Single
            Get
                Return Flt("UNAM\FaceGen - Face clamp")
            End Get
            Set(value As Single)
                Escribir("UNAM\FaceGen - Face clamp", value)
            End Set
        End Property

        ''' <summary>ATKR\Attack Race  -&gt;  RACE. Referencia en el espacio del orden de carga.</summary>
        Public Property AttackRace As UInteger
            Get
                Return Referencia("ATKR\Attack Race")
            End Get
            Set(value As UInteger)
                PonerReferencia("ATKR\Attack Race", value)
            End Set
        End Property

        ''' <summary>GNAM\Body Part Data  -&gt;  BPTD / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property BodyPartData As UInteger
            Get
                Return Referencia("GNAM\Body Part Data")
            End Get
            Set(value As UInteger)
                PonerReferencia("GNAM\Body Part Data", value)
            End Set
        End Property

        ''' <summary>Male Behavior Graph\Model\MODL\Model FileName</summary>
        Public Property ModelModelFileName As String
            Get
                Return Txt("Male Behavior Graph\Model\MODL\Model FileName")
            End Get
            Set(value As String)
                Escribir("Male Behavior Graph\Model\MODL\Model FileName", value)
            End Set
        End Property

        ''' <summary>Female Behavior Graph\Model\MODL\Model FileName</summary>
        Public Property ModelModelFileName2 As String
            Get
                Return Txt("Female Behavior Graph\Model\MODL\Model FileName")
            End Get
            Set(value As String)
                Escribir("Female Behavior Graph\Model\MODL\Model FileName", value)
            End Set
        End Property

        ''' <summary>NAM4\Material Type  -&gt;  MATT / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property MaterialType As UInteger
            Get
                Return Referencia("NAM4\Material Type")
            End Get
            Set(value As UInteger)
                PonerReferencia("NAM4\Material Type", value)
            End Set
        End Property

        ''' <summary>NAM5\Impact Data Set  -&gt;  IPDS / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property ImpactDataSet As UInteger
            Get
                Return Referencia("NAM5\Impact Data Set")
            End Get
            Set(value As UInteger)
                PonerReferencia("NAM5\Impact Data Set", value)
            End Set
        End Property

        ''' <summary>NAM7\Decapitation FX  -&gt;  ARTO / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property DecapitationFX As UInteger
            Get
                Return Referencia("NAM7\Decapitation FX")
            End Get
            Set(value As UInteger)
                PonerReferencia("NAM7\Decapitation FX", value)
            End Set
        End Property

        ''' <summary>ONAM\Open Loot Sound  -&gt;  SNDR / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property OpenLootSound As UInteger
            Get
                Return Referencia("ONAM\Open Loot Sound")
            End Get
            Set(value As UInteger)
                PonerReferencia("ONAM\Open Loot Sound", value)
            End Set
        End Property

        ''' <summary>LNAM\Close Loot Sound  -&gt;  SNDR / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property CloseLootSound As UInteger
            Get
                Return Referencia("LNAM\Close Loot Sound")
            End Get
            Set(value As UInteger)
                PonerReferencia("LNAM\Close Loot Sound", value)
            End Set
        End Property

        ''' <summary>VNAM\Equipment Flags</summary>
        Public Property EquipmentFlags As UInteger
            Get
                Return CUInt(Entero("VNAM\Equipment Flags"))
            End Get
            Set(value As UInteger)
                Escribir("VNAM\Equipment Flags", CLng(value))
            End Set
        End Property

        ''' <summary>UNES\Unarmed Equip Slot  -&gt;  EQUP / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property UnarmedEquipSlot As UInteger
            Get
                Return Referencia("UNES\Unarmed Equip Slot")
            End Get
            Set(value As UInteger)
                PonerReferencia("UNES\Unarmed Equip Slot", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Public Property PhonemeTargetWeightAahLipBigAah As Single
            Get
                Return Flt("FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\Aah / LipBigAah")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\Aah / LipBigAah", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Public Property PhonemeTargetWeightBigAahLipDST As Single
            Get
                Return Flt("FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\BigAah / LipDST")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\BigAah / LipDST", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Public Property PhonemeTargetWeightBMPLipEee As Single
            Get
                Return Flt("FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\BMP / LipEee")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\BMP / LipEee", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Public Property PhonemeTargetWeightChJshLipFV As Single
            Get
                Return Flt("FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\ChJsh / LipFV")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\ChJsh / LipFV", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Public Property PhonemeTargetWeightDSTLipK As Single
            Get
                Return Flt("FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\DST / LipK")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\DST / LipK", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Public Property PhonemeTargetWeightEeeLipL As Single
            Get
                Return Flt("FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\Eee / LipL")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\Eee / LipL", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Public Property PhonemeTargetWeightEhLipR As Single
            Get
                Return Flt("FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\Eh / LipR")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\Eh / LipR", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Public Property PhonemeTargetWeightFVLipTh As Single
            Get
                Return Flt("FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\FV / LipTh")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\FV / LipTh", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\I</summary>
        Public Property PhonemeTargetWeightI As Single
            Get
                Return Flt("FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\I")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\I", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\K</summary>
        Public Property PhonemeTargetWeightK As Single
            Get
                Return Flt("FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\K")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\K", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\N</summary>
        Public Property PhonemeTargetWeightN As Single
            Get
                Return Flt("FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\N")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\N", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\Oh</summary>
        Public Property PhonemeTargetWeightOh As Single
            Get
                Return Flt("FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\Oh")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\Oh", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\OohQ</summary>
        Public Property PhonemeTargetWeightOohQ As Single
            Get
                Return Flt("FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\OohQ")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\OohQ", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\R</summary>
        Public Property PhonemeTargetWeightR As Single
            Get
                Return Flt("FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\R")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\R", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\TH</summary>
        Public Property PhonemeTargetWeightTH As Single
            Get
                Return Flt("FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\TH")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\TH", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\W</summary>
        Public Property PhonemeTargetWeightW As Single
            Get
                Return Flt("FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\W")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\W", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Public Property PhonemeTargetWeightAahLipBigAah2 As Single
            Get
                Return Flt("FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\Aah / LipBigAah")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\Aah / LipBigAah", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Public Property PhonemeTargetWeightBigAahLipDST2 As Single
            Get
                Return Flt("FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\BigAah / LipDST")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\BigAah / LipDST", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Public Property PhonemeTargetWeightBMPLipEee2 As Single
            Get
                Return Flt("FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\BMP / LipEee")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\BMP / LipEee", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Public Property PhonemeTargetWeightChJshLipFV2 As Single
            Get
                Return Flt("FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\ChJsh / LipFV")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\ChJsh / LipFV", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Public Property PhonemeTargetWeightDSTLipK2 As Single
            Get
                Return Flt("FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\DST / LipK")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\DST / LipK", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Public Property PhonemeTargetWeightEeeLipL2 As Single
            Get
                Return Flt("FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\Eee / LipL")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\Eee / LipL", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Public Property PhonemeTargetWeightEhLipR2 As Single
            Get
                Return Flt("FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\Eh / LipR")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\Eh / LipR", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Public Property PhonemeTargetWeightFVLipTh2 As Single
            Get
                Return Flt("FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\FV / LipTh")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\FV / LipTh", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\I</summary>
        Public Property PhonemeTargetWeightI2 As Single
            Get
                Return Flt("FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\I")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\I", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\K</summary>
        Public Property PhonemeTargetWeightK2 As Single
            Get
                Return Flt("FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\K")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\K", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\N</summary>
        Public Property PhonemeTargetWeightN2 As Single
            Get
                Return Flt("FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\N")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\N", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\Oh</summary>
        Public Property PhonemeTargetWeightOh2 As Single
            Get
                Return Flt("FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\Oh")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\Oh", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\OohQ</summary>
        Public Property PhonemeTargetWeightOohQ2 As Single
            Get
                Return Flt("FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\OohQ")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\OohQ", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\R</summary>
        Public Property PhonemeTargetWeightR2 As Single
            Get
                Return Flt("FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\R")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\R", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\TH</summary>
        Public Property PhonemeTargetWeightTH2 As Single
            Get
                Return Flt("FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\TH")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\TH", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\W</summary>
        Public Property PhonemeTargetWeightW2 As Single
            Get
                Return Flt("FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\W")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\W", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Public Property PhonemeTargetWeightAahLipBigAah3 As Single
            Get
                Return Flt("FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\Aah / LipBigAah")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\Aah / LipBigAah", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Public Property PhonemeTargetWeightBigAahLipDST3 As Single
            Get
                Return Flt("FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\BigAah / LipDST")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\BigAah / LipDST", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Public Property PhonemeTargetWeightBMPLipEee3 As Single
            Get
                Return Flt("FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\BMP / LipEee")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\BMP / LipEee", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Public Property PhonemeTargetWeightChJshLipFV3 As Single
            Get
                Return Flt("FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\ChJsh / LipFV")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\ChJsh / LipFV", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Public Property PhonemeTargetWeightDSTLipK3 As Single
            Get
                Return Flt("FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\DST / LipK")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\DST / LipK", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Public Property PhonemeTargetWeightEeeLipL3 As Single
            Get
                Return Flt("FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\Eee / LipL")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\Eee / LipL", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Public Property PhonemeTargetWeightEhLipR3 As Single
            Get
                Return Flt("FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\Eh / LipR")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\Eh / LipR", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Public Property PhonemeTargetWeightFVLipTh3 As Single
            Get
                Return Flt("FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\FV / LipTh")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\FV / LipTh", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\I</summary>
        Public Property PhonemeTargetWeightI3 As Single
            Get
                Return Flt("FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\I")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\I", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\K</summary>
        Public Property PhonemeTargetWeightK3 As Single
            Get
                Return Flt("FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\K")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\K", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\N</summary>
        Public Property PhonemeTargetWeightN3 As Single
            Get
                Return Flt("FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\N")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\N", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\Oh</summary>
        Public Property PhonemeTargetWeightOh3 As Single
            Get
                Return Flt("FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\Oh")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\Oh", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\OohQ</summary>
        Public Property PhonemeTargetWeightOohQ3 As Single
            Get
                Return Flt("FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\OohQ")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\OohQ", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\R</summary>
        Public Property PhonemeTargetWeightR3 As Single
            Get
                Return Flt("FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\R")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\R", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\TH</summary>
        Public Property PhonemeTargetWeightTH3 As Single
            Get
                Return Flt("FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\TH")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\TH", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\W</summary>
        Public Property PhonemeTargetWeightW3 As Single
            Get
                Return Flt("FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\W")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\W", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Public Property PhonemeTargetWeightAahLipBigAah4 As Single
            Get
                Return Flt("FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\Aah / LipBigAah")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\Aah / LipBigAah", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Public Property PhonemeTargetWeightBigAahLipDST4 As Single
            Get
                Return Flt("FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\BigAah / LipDST")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\BigAah / LipDST", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Public Property PhonemeTargetWeightBMPLipEee4 As Single
            Get
                Return Flt("FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\BMP / LipEee")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\BMP / LipEee", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Public Property PhonemeTargetWeightChJshLipFV4 As Single
            Get
                Return Flt("FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\ChJsh / LipFV")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\ChJsh / LipFV", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Public Property PhonemeTargetWeightDSTLipK4 As Single
            Get
                Return Flt("FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\DST / LipK")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\DST / LipK", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Public Property PhonemeTargetWeightEeeLipL4 As Single
            Get
                Return Flt("FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\Eee / LipL")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\Eee / LipL", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Public Property PhonemeTargetWeightEhLipR4 As Single
            Get
                Return Flt("FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\Eh / LipR")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\Eh / LipR", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Public Property PhonemeTargetWeightFVLipTh4 As Single
            Get
                Return Flt("FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\FV / LipTh")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\FV / LipTh", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\I</summary>
        Public Property PhonemeTargetWeightI4 As Single
            Get
                Return Flt("FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\I")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\I", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\K</summary>
        Public Property PhonemeTargetWeightK4 As Single
            Get
                Return Flt("FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\K")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\K", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\N</summary>
        Public Property PhonemeTargetWeightN4 As Single
            Get
                Return Flt("FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\N")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\N", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\Oh</summary>
        Public Property PhonemeTargetWeightOh4 As Single
            Get
                Return Flt("FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\Oh")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\Oh", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\OohQ</summary>
        Public Property PhonemeTargetWeightOohQ4 As Single
            Get
                Return Flt("FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\OohQ")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\OohQ", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\R</summary>
        Public Property PhonemeTargetWeightR4 As Single
            Get
                Return Flt("FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\R")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\R", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\TH</summary>
        Public Property PhonemeTargetWeightTH4 As Single
            Get
                Return Flt("FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\TH")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\TH", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\W</summary>
        Public Property PhonemeTargetWeightW4 As Single
            Get
                Return Flt("FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\W")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\W", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Public Property PhonemeTargetWeightAahLipBigAah5 As Single
            Get
                Return Flt("FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\Aah / LipBigAah")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\Aah / LipBigAah", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Public Property PhonemeTargetWeightBigAahLipDST5 As Single
            Get
                Return Flt("FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\BigAah / LipDST")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\BigAah / LipDST", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Public Property PhonemeTargetWeightBMPLipEee5 As Single
            Get
                Return Flt("FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\BMP / LipEee")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\BMP / LipEee", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Public Property PhonemeTargetWeightChJshLipFV5 As Single
            Get
                Return Flt("FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\ChJsh / LipFV")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\ChJsh / LipFV", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Public Property PhonemeTargetWeightDSTLipK5 As Single
            Get
                Return Flt("FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\DST / LipK")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\DST / LipK", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Public Property PhonemeTargetWeightEeeLipL5 As Single
            Get
                Return Flt("FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\Eee / LipL")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\Eee / LipL", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Public Property PhonemeTargetWeightEhLipR5 As Single
            Get
                Return Flt("FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\Eh / LipR")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\Eh / LipR", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Public Property PhonemeTargetWeightFVLipTh5 As Single
            Get
                Return Flt("FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\FV / LipTh")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\FV / LipTh", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\I</summary>
        Public Property PhonemeTargetWeightI5 As Single
            Get
                Return Flt("FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\I")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\I", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\K</summary>
        Public Property PhonemeTargetWeightK5 As Single
            Get
                Return Flt("FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\K")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\K", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\N</summary>
        Public Property PhonemeTargetWeightN5 As Single
            Get
                Return Flt("FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\N")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\N", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\Oh</summary>
        Public Property PhonemeTargetWeightOh5 As Single
            Get
                Return Flt("FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\Oh")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\Oh", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\OohQ</summary>
        Public Property PhonemeTargetWeightOohQ5 As Single
            Get
                Return Flt("FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\OohQ")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\OohQ", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\R</summary>
        Public Property PhonemeTargetWeightR5 As Single
            Get
                Return Flt("FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\R")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\R", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\TH</summary>
        Public Property PhonemeTargetWeightTH5 As Single
            Get
                Return Flt("FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\TH")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\TH", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\W</summary>
        Public Property PhonemeTargetWeightW5 As Single
            Get
                Return Flt("FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\W")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\W", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Public Property PhonemeTargetWeightAahLipBigAah6 As Single
            Get
                Return Flt("FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\Aah / LipBigAah")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\Aah / LipBigAah", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Public Property PhonemeTargetWeightBigAahLipDST6 As Single
            Get
                Return Flt("FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\BigAah / LipDST")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\BigAah / LipDST", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Public Property PhonemeTargetWeightBMPLipEee6 As Single
            Get
                Return Flt("FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\BMP / LipEee")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\BMP / LipEee", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Public Property PhonemeTargetWeightChJshLipFV6 As Single
            Get
                Return Flt("FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\ChJsh / LipFV")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\ChJsh / LipFV", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Public Property PhonemeTargetWeightDSTLipK6 As Single
            Get
                Return Flt("FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\DST / LipK")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\DST / LipK", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Public Property PhonemeTargetWeightEeeLipL6 As Single
            Get
                Return Flt("FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\Eee / LipL")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\Eee / LipL", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Public Property PhonemeTargetWeightEhLipR6 As Single
            Get
                Return Flt("FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\Eh / LipR")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\Eh / LipR", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Public Property PhonemeTargetWeightFVLipTh6 As Single
            Get
                Return Flt("FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\FV / LipTh")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\FV / LipTh", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\I</summary>
        Public Property PhonemeTargetWeightI6 As Single
            Get
                Return Flt("FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\I")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\I", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\K</summary>
        Public Property PhonemeTargetWeightK6 As Single
            Get
                Return Flt("FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\K")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\K", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\N</summary>
        Public Property PhonemeTargetWeightN6 As Single
            Get
                Return Flt("FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\N")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\N", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\Oh</summary>
        Public Property PhonemeTargetWeightOh6 As Single
            Get
                Return Flt("FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\Oh")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\Oh", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\OohQ</summary>
        Public Property PhonemeTargetWeightOohQ6 As Single
            Get
                Return Flt("FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\OohQ")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\OohQ", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\R</summary>
        Public Property PhonemeTargetWeightR6 As Single
            Get
                Return Flt("FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\R")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\R", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\TH</summary>
        Public Property PhonemeTargetWeightTH6 As Single
            Get
                Return Flt("FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\TH")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\TH", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\W</summary>
        Public Property PhonemeTargetWeightW6 As Single
            Get
                Return Flt("FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\W")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\W", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Public Property PhonemeTargetWeightAahLipBigAah7 As Single
            Get
                Return Flt("FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\Aah / LipBigAah")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\Aah / LipBigAah", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Public Property PhonemeTargetWeightBigAahLipDST7 As Single
            Get
                Return Flt("FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\BigAah / LipDST")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\BigAah / LipDST", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Public Property PhonemeTargetWeightBMPLipEee7 As Single
            Get
                Return Flt("FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\BMP / LipEee")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\BMP / LipEee", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Public Property PhonemeTargetWeightChJshLipFV7 As Single
            Get
                Return Flt("FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\ChJsh / LipFV")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\ChJsh / LipFV", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Public Property PhonemeTargetWeightDSTLipK7 As Single
            Get
                Return Flt("FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\DST / LipK")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\DST / LipK", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Public Property PhonemeTargetWeightEeeLipL7 As Single
            Get
                Return Flt("FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\Eee / LipL")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\Eee / LipL", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Public Property PhonemeTargetWeightEhLipR7 As Single
            Get
                Return Flt("FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\Eh / LipR")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\Eh / LipR", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Public Property PhonemeTargetWeightFVLipTh7 As Single
            Get
                Return Flt("FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\FV / LipTh")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\FV / LipTh", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\I</summary>
        Public Property PhonemeTargetWeightI7 As Single
            Get
                Return Flt("FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\I")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\I", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\K</summary>
        Public Property PhonemeTargetWeightK7 As Single
            Get
                Return Flt("FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\K")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\K", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\N</summary>
        Public Property PhonemeTargetWeightN7 As Single
            Get
                Return Flt("FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\N")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\N", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\Oh</summary>
        Public Property PhonemeTargetWeightOh7 As Single
            Get
                Return Flt("FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\Oh")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\Oh", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\OohQ</summary>
        Public Property PhonemeTargetWeightOohQ7 As Single
            Get
                Return Flt("FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\OohQ")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\OohQ", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\R</summary>
        Public Property PhonemeTargetWeightR7 As Single
            Get
                Return Flt("FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\R")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\R", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\TH</summary>
        Public Property PhonemeTargetWeightTH7 As Single
            Get
                Return Flt("FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\TH")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\TH", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\W</summary>
        Public Property PhonemeTargetWeightW7 As Single
            Get
                Return Flt("FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\W")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\W", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Public Property PhonemeTargetWeightAahLipBigAah8 As Single
            Get
                Return Flt("FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\Aah / LipBigAah")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\Aah / LipBigAah", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Public Property PhonemeTargetWeightBigAahLipDST8 As Single
            Get
                Return Flt("FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\BigAah / LipDST")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\BigAah / LipDST", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Public Property PhonemeTargetWeightBMPLipEee8 As Single
            Get
                Return Flt("FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\BMP / LipEee")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\BMP / LipEee", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Public Property PhonemeTargetWeightChJshLipFV8 As Single
            Get
                Return Flt("FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\ChJsh / LipFV")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\ChJsh / LipFV", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Public Property PhonemeTargetWeightDSTLipK8 As Single
            Get
                Return Flt("FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\DST / LipK")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\DST / LipK", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Public Property PhonemeTargetWeightEeeLipL8 As Single
            Get
                Return Flt("FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\Eee / LipL")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\Eee / LipL", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Public Property PhonemeTargetWeightEhLipR8 As Single
            Get
                Return Flt("FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\Eh / LipR")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\Eh / LipR", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Public Property PhonemeTargetWeightFVLipTh8 As Single
            Get
                Return Flt("FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\FV / LipTh")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\FV / LipTh", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\I</summary>
        Public Property PhonemeTargetWeightI8 As Single
            Get
                Return Flt("FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\I")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\I", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\K</summary>
        Public Property PhonemeTargetWeightK8 As Single
            Get
                Return Flt("FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\K")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\K", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\N</summary>
        Public Property PhonemeTargetWeightN8 As Single
            Get
                Return Flt("FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\N")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\N", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\Oh</summary>
        Public Property PhonemeTargetWeightOh8 As Single
            Get
                Return Flt("FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\Oh")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\Oh", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\OohQ</summary>
        Public Property PhonemeTargetWeightOohQ8 As Single
            Get
                Return Flt("FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\OohQ")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\OohQ", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\R</summary>
        Public Property PhonemeTargetWeightR8 As Single
            Get
                Return Flt("FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\R")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\R", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\TH</summary>
        Public Property PhonemeTargetWeightTH8 As Single
            Get
                Return Flt("FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\TH")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\TH", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\W</summary>
        Public Property PhonemeTargetWeightW8 As Single
            Get
                Return Flt("FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\W")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\W", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Public Property PhonemeTargetWeightAahLipBigAah9 As Single
            Get
                Return Flt("FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\Aah / LipBigAah")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\Aah / LipBigAah", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Public Property PhonemeTargetWeightBigAahLipDST9 As Single
            Get
                Return Flt("FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\BigAah / LipDST")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\BigAah / LipDST", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Public Property PhonemeTargetWeightBMPLipEee9 As Single
            Get
                Return Flt("FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\BMP / LipEee")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\BMP / LipEee", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Public Property PhonemeTargetWeightChJshLipFV9 As Single
            Get
                Return Flt("FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\ChJsh / LipFV")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\ChJsh / LipFV", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Public Property PhonemeTargetWeightDSTLipK9 As Single
            Get
                Return Flt("FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\DST / LipK")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\DST / LipK", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Public Property PhonemeTargetWeightEeeLipL9 As Single
            Get
                Return Flt("FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\Eee / LipL")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\Eee / LipL", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Public Property PhonemeTargetWeightEhLipR9 As Single
            Get
                Return Flt("FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\Eh / LipR")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\Eh / LipR", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Public Property PhonemeTargetWeightFVLipTh9 As Single
            Get
                Return Flt("FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\FV / LipTh")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\FV / LipTh", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\I</summary>
        Public Property PhonemeTargetWeightI9 As Single
            Get
                Return Flt("FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\I")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\I", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\K</summary>
        Public Property PhonemeTargetWeightK9 As Single
            Get
                Return Flt("FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\K")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\K", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\N</summary>
        Public Property PhonemeTargetWeightN9 As Single
            Get
                Return Flt("FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\N")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\N", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\Oh</summary>
        Public Property PhonemeTargetWeightOh9 As Single
            Get
                Return Flt("FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\Oh")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\Oh", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\OohQ</summary>
        Public Property PhonemeTargetWeightOohQ9 As Single
            Get
                Return Flt("FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\OohQ")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\OohQ", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\R</summary>
        Public Property PhonemeTargetWeightR9 As Single
            Get
                Return Flt("FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\R")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\R", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\TH</summary>
        Public Property PhonemeTargetWeightTH9 As Single
            Get
                Return Flt("FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\TH")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\TH", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\W</summary>
        Public Property PhonemeTargetWeightW9 As Single
            Get
                Return Flt("FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\W")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\W", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Public Property PhonemeTargetWeightAahLipBigAah10 As Single
            Get
                Return Flt("FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\Aah / LipBigAah")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\Aah / LipBigAah", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Public Property PhonemeTargetWeightBigAahLipDST10 As Single
            Get
                Return Flt("FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\BigAah / LipDST")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\BigAah / LipDST", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Public Property PhonemeTargetWeightBMPLipEee10 As Single
            Get
                Return Flt("FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\BMP / LipEee")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\BMP / LipEee", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Public Property PhonemeTargetWeightChJshLipFV10 As Single
            Get
                Return Flt("FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\ChJsh / LipFV")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\ChJsh / LipFV", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Public Property PhonemeTargetWeightDSTLipK10 As Single
            Get
                Return Flt("FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\DST / LipK")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\DST / LipK", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Public Property PhonemeTargetWeightEeeLipL10 As Single
            Get
                Return Flt("FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\Eee / LipL")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\Eee / LipL", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Public Property PhonemeTargetWeightEhLipR10 As Single
            Get
                Return Flt("FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\Eh / LipR")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\Eh / LipR", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Public Property PhonemeTargetWeightFVLipTh10 As Single
            Get
                Return Flt("FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\FV / LipTh")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\FV / LipTh", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\I</summary>
        Public Property PhonemeTargetWeightI10 As Single
            Get
                Return Flt("FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\I")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\I", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\K</summary>
        Public Property PhonemeTargetWeightK10 As Single
            Get
                Return Flt("FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\K")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\K", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\N</summary>
        Public Property PhonemeTargetWeightN10 As Single
            Get
                Return Flt("FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\N")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\N", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\Oh</summary>
        Public Property PhonemeTargetWeightOh10 As Single
            Get
                Return Flt("FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\Oh")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\Oh", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\OohQ</summary>
        Public Property PhonemeTargetWeightOohQ10 As Single
            Get
                Return Flt("FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\OohQ")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\OohQ", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\R</summary>
        Public Property PhonemeTargetWeightR10 As Single
            Get
                Return Flt("FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\R")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\R", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\TH</summary>
        Public Property PhonemeTargetWeightTH10 As Single
            Get
                Return Flt("FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\TH")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\TH", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\W</summary>
        Public Property PhonemeTargetWeightW10 As Single
            Get
                Return Flt("FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\W")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\W", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Public Property PhonemeTargetWeightAahLipBigAah11 As Single
            Get
                Return Flt("FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\Aah / LipBigAah")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\Aah / LipBigAah", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Public Property PhonemeTargetWeightBigAahLipDST11 As Single
            Get
                Return Flt("FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\BigAah / LipDST")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\BigAah / LipDST", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Public Property PhonemeTargetWeightBMPLipEee11 As Single
            Get
                Return Flt("FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\BMP / LipEee")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\BMP / LipEee", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Public Property PhonemeTargetWeightChJshLipFV11 As Single
            Get
                Return Flt("FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\ChJsh / LipFV")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\ChJsh / LipFV", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Public Property PhonemeTargetWeightDSTLipK11 As Single
            Get
                Return Flt("FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\DST / LipK")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\DST / LipK", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Public Property PhonemeTargetWeightEeeLipL11 As Single
            Get
                Return Flt("FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\Eee / LipL")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\Eee / LipL", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Public Property PhonemeTargetWeightEhLipR11 As Single
            Get
                Return Flt("FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\Eh / LipR")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\Eh / LipR", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Public Property PhonemeTargetWeightFVLipTh11 As Single
            Get
                Return Flt("FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\FV / LipTh")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\FV / LipTh", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\I</summary>
        Public Property PhonemeTargetWeightI11 As Single
            Get
                Return Flt("FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\I")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\I", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\K</summary>
        Public Property PhonemeTargetWeightK11 As Single
            Get
                Return Flt("FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\K")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\K", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\N</summary>
        Public Property PhonemeTargetWeightN11 As Single
            Get
                Return Flt("FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\N")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\N", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\Oh</summary>
        Public Property PhonemeTargetWeightOh11 As Single
            Get
                Return Flt("FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\Oh")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\Oh", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\OohQ</summary>
        Public Property PhonemeTargetWeightOohQ11 As Single
            Get
                Return Flt("FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\OohQ")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\OohQ", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\R</summary>
        Public Property PhonemeTargetWeightR11 As Single
            Get
                Return Flt("FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\R")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\R", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\TH</summary>
        Public Property PhonemeTargetWeightTH11 As Single
            Get
                Return Flt("FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\TH")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\TH", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\W</summary>
        Public Property PhonemeTargetWeightW11 As Single
            Get
                Return Flt("FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\W")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\W", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Public Property PhonemeTargetWeightAahLipBigAah12 As Single
            Get
                Return Flt("FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\Aah / LipBigAah")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\Aah / LipBigAah", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Public Property PhonemeTargetWeightBigAahLipDST12 As Single
            Get
                Return Flt("FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\BigAah / LipDST")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\BigAah / LipDST", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Public Property PhonemeTargetWeightBMPLipEee12 As Single
            Get
                Return Flt("FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\BMP / LipEee")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\BMP / LipEee", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Public Property PhonemeTargetWeightChJshLipFV12 As Single
            Get
                Return Flt("FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\ChJsh / LipFV")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\ChJsh / LipFV", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Public Property PhonemeTargetWeightDSTLipK12 As Single
            Get
                Return Flt("FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\DST / LipK")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\DST / LipK", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Public Property PhonemeTargetWeightEeeLipL12 As Single
            Get
                Return Flt("FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\Eee / LipL")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\Eee / LipL", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Public Property PhonemeTargetWeightEhLipR12 As Single
            Get
                Return Flt("FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\Eh / LipR")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\Eh / LipR", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Public Property PhonemeTargetWeightFVLipTh12 As Single
            Get
                Return Flt("FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\FV / LipTh")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\FV / LipTh", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\I</summary>
        Public Property PhonemeTargetWeightI12 As Single
            Get
                Return Flt("FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\I")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\I", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\K</summary>
        Public Property PhonemeTargetWeightK12 As Single
            Get
                Return Flt("FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\K")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\K", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\N</summary>
        Public Property PhonemeTargetWeightN12 As Single
            Get
                Return Flt("FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\N")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\N", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\Oh</summary>
        Public Property PhonemeTargetWeightOh12 As Single
            Get
                Return Flt("FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\Oh")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\Oh", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\OohQ</summary>
        Public Property PhonemeTargetWeightOohQ12 As Single
            Get
                Return Flt("FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\OohQ")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\OohQ", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\R</summary>
        Public Property PhonemeTargetWeightR12 As Single
            Get
                Return Flt("FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\R")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\R", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\TH</summary>
        Public Property PhonemeTargetWeightTH12 As Single
            Get
                Return Flt("FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\TH")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\TH", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\W</summary>
        Public Property PhonemeTargetWeightW12 As Single
            Get
                Return Flt("FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\W")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\W", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Public Property PhonemeTargetWeightAahLipBigAah13 As Single
            Get
                Return Flt("FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\Aah / LipBigAah")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\Aah / LipBigAah", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Public Property PhonemeTargetWeightBigAahLipDST13 As Single
            Get
                Return Flt("FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\BigAah / LipDST")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\BigAah / LipDST", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Public Property PhonemeTargetWeightBMPLipEee13 As Single
            Get
                Return Flt("FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\BMP / LipEee")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\BMP / LipEee", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Public Property PhonemeTargetWeightChJshLipFV13 As Single
            Get
                Return Flt("FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\ChJsh / LipFV")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\ChJsh / LipFV", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Public Property PhonemeTargetWeightDSTLipK13 As Single
            Get
                Return Flt("FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\DST / LipK")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\DST / LipK", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Public Property PhonemeTargetWeightEeeLipL13 As Single
            Get
                Return Flt("FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\Eee / LipL")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\Eee / LipL", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Public Property PhonemeTargetWeightEhLipR13 As Single
            Get
                Return Flt("FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\Eh / LipR")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\Eh / LipR", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Public Property PhonemeTargetWeightFVLipTh13 As Single
            Get
                Return Flt("FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\FV / LipTh")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\FV / LipTh", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\I</summary>
        Public Property PhonemeTargetWeightI13 As Single
            Get
                Return Flt("FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\I")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\I", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\K</summary>
        Public Property PhonemeTargetWeightK13 As Single
            Get
                Return Flt("FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\K")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\K", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\N</summary>
        Public Property PhonemeTargetWeightN13 As Single
            Get
                Return Flt("FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\N")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\N", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\Oh</summary>
        Public Property PhonemeTargetWeightOh13 As Single
            Get
                Return Flt("FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\Oh")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\Oh", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\OohQ</summary>
        Public Property PhonemeTargetWeightOohQ13 As Single
            Get
                Return Flt("FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\OohQ")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\OohQ", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\R</summary>
        Public Property PhonemeTargetWeightR13 As Single
            Get
                Return Flt("FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\R")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\R", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\TH</summary>
        Public Property PhonemeTargetWeightTH13 As Single
            Get
                Return Flt("FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\TH")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\TH", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\W</summary>
        Public Property PhonemeTargetWeightW13 As Single
            Get
                Return Flt("FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\W")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\W", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Public Property PhonemeTargetWeightAahLipBigAah14 As Single
            Get
                Return Flt("FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\Aah / LipBigAah")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\Aah / LipBigAah", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Public Property PhonemeTargetWeightBigAahLipDST14 As Single
            Get
                Return Flt("FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\BigAah / LipDST")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\BigAah / LipDST", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Public Property PhonemeTargetWeightBMPLipEee14 As Single
            Get
                Return Flt("FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\BMP / LipEee")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\BMP / LipEee", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Public Property PhonemeTargetWeightChJshLipFV14 As Single
            Get
                Return Flt("FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\ChJsh / LipFV")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\ChJsh / LipFV", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Public Property PhonemeTargetWeightDSTLipK14 As Single
            Get
                Return Flt("FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\DST / LipK")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\DST / LipK", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Public Property PhonemeTargetWeightEeeLipL14 As Single
            Get
                Return Flt("FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\Eee / LipL")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\Eee / LipL", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Public Property PhonemeTargetWeightEhLipR14 As Single
            Get
                Return Flt("FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\Eh / LipR")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\Eh / LipR", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Public Property PhonemeTargetWeightFVLipTh14 As Single
            Get
                Return Flt("FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\FV / LipTh")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\FV / LipTh", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\I</summary>
        Public Property PhonemeTargetWeightI14 As Single
            Get
                Return Flt("FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\I")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\I", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\K</summary>
        Public Property PhonemeTargetWeightK14 As Single
            Get
                Return Flt("FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\K")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\K", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\N</summary>
        Public Property PhonemeTargetWeightN14 As Single
            Get
                Return Flt("FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\N")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\N", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\Oh</summary>
        Public Property PhonemeTargetWeightOh14 As Single
            Get
                Return Flt("FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\Oh")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\Oh", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\OohQ</summary>
        Public Property PhonemeTargetWeightOohQ14 As Single
            Get
                Return Flt("FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\OohQ")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\OohQ", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\R</summary>
        Public Property PhonemeTargetWeightR14 As Single
            Get
                Return Flt("FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\R")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\R", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\TH</summary>
        Public Property PhonemeTargetWeightTH14 As Single
            Get
                Return Flt("FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\TH")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\TH", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\W</summary>
        Public Property PhonemeTargetWeightW14 As Single
            Get
                Return Flt("FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\W")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\W", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Public Property PhonemeTargetWeightAahLipBigAah15 As Single
            Get
                Return Flt("FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\Aah / LipBigAah")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\Aah / LipBigAah", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Public Property PhonemeTargetWeightBigAahLipDST15 As Single
            Get
                Return Flt("FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\BigAah / LipDST")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\BigAah / LipDST", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Public Property PhonemeTargetWeightBMPLipEee15 As Single
            Get
                Return Flt("FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\BMP / LipEee")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\BMP / LipEee", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Public Property PhonemeTargetWeightChJshLipFV15 As Single
            Get
                Return Flt("FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\ChJsh / LipFV")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\ChJsh / LipFV", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Public Property PhonemeTargetWeightDSTLipK15 As Single
            Get
                Return Flt("FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\DST / LipK")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\DST / LipK", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Public Property PhonemeTargetWeightEeeLipL15 As Single
            Get
                Return Flt("FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\Eee / LipL")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\Eee / LipL", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Public Property PhonemeTargetWeightEhLipR15 As Single
            Get
                Return Flt("FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\Eh / LipR")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\Eh / LipR", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Public Property PhonemeTargetWeightFVLipTh15 As Single
            Get
                Return Flt("FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\FV / LipTh")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\FV / LipTh", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\I</summary>
        Public Property PhonemeTargetWeightI15 As Single
            Get
                Return Flt("FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\I")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\I", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\K</summary>
        Public Property PhonemeTargetWeightK15 As Single
            Get
                Return Flt("FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\K")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\K", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\N</summary>
        Public Property PhonemeTargetWeightN15 As Single
            Get
                Return Flt("FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\N")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\N", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\Oh</summary>
        Public Property PhonemeTargetWeightOh15 As Single
            Get
                Return Flt("FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\Oh")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\Oh", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\OohQ</summary>
        Public Property PhonemeTargetWeightOohQ15 As Single
            Get
                Return Flt("FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\OohQ")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\OohQ", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\R</summary>
        Public Property PhonemeTargetWeightR15 As Single
            Get
                Return Flt("FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\R")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\R", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\TH</summary>
        Public Property PhonemeTargetWeightTH15 As Single
            Get
                Return Flt("FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\TH")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\TH", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\W</summary>
        Public Property PhonemeTargetWeightW15 As Single
            Get
                Return Flt("FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\W")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\W", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Public Property PhonemeTargetWeightAahLipBigAah16 As Single
            Get
                Return Flt("FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\Aah / LipBigAah")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\Aah / LipBigAah", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Public Property PhonemeTargetWeightBigAahLipDST16 As Single
            Get
                Return Flt("FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\BigAah / LipDST")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\BigAah / LipDST", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Public Property PhonemeTargetWeightBMPLipEee16 As Single
            Get
                Return Flt("FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\BMP / LipEee")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\BMP / LipEee", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Public Property PhonemeTargetWeightChJshLipFV16 As Single
            Get
                Return Flt("FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\ChJsh / LipFV")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\ChJsh / LipFV", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Public Property PhonemeTargetWeightDSTLipK16 As Single
            Get
                Return Flt("FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\DST / LipK")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\DST / LipK", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Public Property PhonemeTargetWeightEeeLipL16 As Single
            Get
                Return Flt("FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\Eee / LipL")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\Eee / LipL", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Public Property PhonemeTargetWeightEhLipR16 As Single
            Get
                Return Flt("FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\Eh / LipR")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\Eh / LipR", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Public Property PhonemeTargetWeightFVLipTh16 As Single
            Get
                Return Flt("FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\FV / LipTh")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\FV / LipTh", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\I</summary>
        Public Property PhonemeTargetWeightI16 As Single
            Get
                Return Flt("FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\I")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\I", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\K</summary>
        Public Property PhonemeTargetWeightK16 As Single
            Get
                Return Flt("FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\K")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\K", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\N</summary>
        Public Property PhonemeTargetWeightN16 As Single
            Get
                Return Flt("FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\N")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\N", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\Oh</summary>
        Public Property PhonemeTargetWeightOh16 As Single
            Get
                Return Flt("FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\Oh")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\Oh", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\OohQ</summary>
        Public Property PhonemeTargetWeightOohQ16 As Single
            Get
                Return Flt("FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\OohQ")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\OohQ", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\R</summary>
        Public Property PhonemeTargetWeightR16 As Single
            Get
                Return Flt("FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\R")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\R", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\TH</summary>
        Public Property PhonemeTargetWeightTH16 As Single
            Get
                Return Flt("FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\TH")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\TH", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\W</summary>
        Public Property PhonemeTargetWeightW16 As Single
            Get
                Return Flt("FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\W")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\W", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\S\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Public Property PhonemeTargetWeightAahLipBigAah17 As Single
            Get
                Return Flt("FaceFX Phonemes\S\PHWT\Phoneme Target Weight\Aah / LipBigAah")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\S\PHWT\Phoneme Target Weight\Aah / LipBigAah", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\S\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Public Property PhonemeTargetWeightBigAahLipDST17 As Single
            Get
                Return Flt("FaceFX Phonemes\S\PHWT\Phoneme Target Weight\BigAah / LipDST")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\S\PHWT\Phoneme Target Weight\BigAah / LipDST", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\S\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Public Property PhonemeTargetWeightBMPLipEee17 As Single
            Get
                Return Flt("FaceFX Phonemes\S\PHWT\Phoneme Target Weight\BMP / LipEee")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\S\PHWT\Phoneme Target Weight\BMP / LipEee", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\S\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Public Property PhonemeTargetWeightChJshLipFV17 As Single
            Get
                Return Flt("FaceFX Phonemes\S\PHWT\Phoneme Target Weight\ChJsh / LipFV")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\S\PHWT\Phoneme Target Weight\ChJsh / LipFV", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\S\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Public Property PhonemeTargetWeightDSTLipK17 As Single
            Get
                Return Flt("FaceFX Phonemes\S\PHWT\Phoneme Target Weight\DST / LipK")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\S\PHWT\Phoneme Target Weight\DST / LipK", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\S\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Public Property PhonemeTargetWeightEeeLipL17 As Single
            Get
                Return Flt("FaceFX Phonemes\S\PHWT\Phoneme Target Weight\Eee / LipL")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\S\PHWT\Phoneme Target Weight\Eee / LipL", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\S\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Public Property PhonemeTargetWeightEhLipR17 As Single
            Get
                Return Flt("FaceFX Phonemes\S\PHWT\Phoneme Target Weight\Eh / LipR")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\S\PHWT\Phoneme Target Weight\Eh / LipR", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\S\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Public Property PhonemeTargetWeightFVLipTh17 As Single
            Get
                Return Flt("FaceFX Phonemes\S\PHWT\Phoneme Target Weight\FV / LipTh")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\S\PHWT\Phoneme Target Weight\FV / LipTh", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\S\PHWT\Phoneme Target Weight\I</summary>
        Public Property PhonemeTargetWeightI17 As Single
            Get
                Return Flt("FaceFX Phonemes\S\PHWT\Phoneme Target Weight\I")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\S\PHWT\Phoneme Target Weight\I", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\S\PHWT\Phoneme Target Weight\K</summary>
        Public Property PhonemeTargetWeightK17 As Single
            Get
                Return Flt("FaceFX Phonemes\S\PHWT\Phoneme Target Weight\K")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\S\PHWT\Phoneme Target Weight\K", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\S\PHWT\Phoneme Target Weight\N</summary>
        Public Property PhonemeTargetWeightN17 As Single
            Get
                Return Flt("FaceFX Phonemes\S\PHWT\Phoneme Target Weight\N")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\S\PHWT\Phoneme Target Weight\N", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\S\PHWT\Phoneme Target Weight\Oh</summary>
        Public Property PhonemeTargetWeightOh17 As Single
            Get
                Return Flt("FaceFX Phonemes\S\PHWT\Phoneme Target Weight\Oh")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\S\PHWT\Phoneme Target Weight\Oh", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\S\PHWT\Phoneme Target Weight\OohQ</summary>
        Public Property PhonemeTargetWeightOohQ17 As Single
            Get
                Return Flt("FaceFX Phonemes\S\PHWT\Phoneme Target Weight\OohQ")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\S\PHWT\Phoneme Target Weight\OohQ", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\S\PHWT\Phoneme Target Weight\R</summary>
        Public Property PhonemeTargetWeightR17 As Single
            Get
                Return Flt("FaceFX Phonemes\S\PHWT\Phoneme Target Weight\R")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\S\PHWT\Phoneme Target Weight\R", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\S\PHWT\Phoneme Target Weight\TH</summary>
        Public Property PhonemeTargetWeightTH17 As Single
            Get
                Return Flt("FaceFX Phonemes\S\PHWT\Phoneme Target Weight\TH")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\S\PHWT\Phoneme Target Weight\TH", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\S\PHWT\Phoneme Target Weight\W</summary>
        Public Property PhonemeTargetWeightW17 As Single
            Get
                Return Flt("FaceFX Phonemes\S\PHWT\Phoneme Target Weight\W")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\S\PHWT\Phoneme Target Weight\W", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Public Property PhonemeTargetWeightAahLipBigAah18 As Single
            Get
                Return Flt("FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\Aah / LipBigAah")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\Aah / LipBigAah", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Public Property PhonemeTargetWeightBigAahLipDST18 As Single
            Get
                Return Flt("FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\BigAah / LipDST")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\BigAah / LipDST", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Public Property PhonemeTargetWeightBMPLipEee18 As Single
            Get
                Return Flt("FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\BMP / LipEee")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\BMP / LipEee", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Public Property PhonemeTargetWeightChJshLipFV18 As Single
            Get
                Return Flt("FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\ChJsh / LipFV")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\ChJsh / LipFV", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Public Property PhonemeTargetWeightDSTLipK18 As Single
            Get
                Return Flt("FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\DST / LipK")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\DST / LipK", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Public Property PhonemeTargetWeightEeeLipL18 As Single
            Get
                Return Flt("FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\Eee / LipL")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\Eee / LipL", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Public Property PhonemeTargetWeightEhLipR18 As Single
            Get
                Return Flt("FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\Eh / LipR")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\Eh / LipR", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Public Property PhonemeTargetWeightFVLipTh18 As Single
            Get
                Return Flt("FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\FV / LipTh")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\FV / LipTh", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\I</summary>
        Public Property PhonemeTargetWeightI18 As Single
            Get
                Return Flt("FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\I")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\I", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\K</summary>
        Public Property PhonemeTargetWeightK18 As Single
            Get
                Return Flt("FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\K")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\K", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\N</summary>
        Public Property PhonemeTargetWeightN18 As Single
            Get
                Return Flt("FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\N")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\N", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\Oh</summary>
        Public Property PhonemeTargetWeightOh18 As Single
            Get
                Return Flt("FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\Oh")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\Oh", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\OohQ</summary>
        Public Property PhonemeTargetWeightOohQ18 As Single
            Get
                Return Flt("FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\OohQ")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\OohQ", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\R</summary>
        Public Property PhonemeTargetWeightR18 As Single
            Get
                Return Flt("FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\R")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\R", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\TH</summary>
        Public Property PhonemeTargetWeightTH18 As Single
            Get
                Return Flt("FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\TH")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\TH", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\W</summary>
        Public Property PhonemeTargetWeightW18 As Single
            Get
                Return Flt("FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\W")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\W", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Public Property PhonemeTargetWeightAahLipBigAah19 As Single
            Get
                Return Flt("FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\Aah / LipBigAah")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\Aah / LipBigAah", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Public Property PhonemeTargetWeightBigAahLipDST19 As Single
            Get
                Return Flt("FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\BigAah / LipDST")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\BigAah / LipDST", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Public Property PhonemeTargetWeightBMPLipEee19 As Single
            Get
                Return Flt("FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\BMP / LipEee")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\BMP / LipEee", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Public Property PhonemeTargetWeightChJshLipFV19 As Single
            Get
                Return Flt("FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\ChJsh / LipFV")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\ChJsh / LipFV", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Public Property PhonemeTargetWeightDSTLipK19 As Single
            Get
                Return Flt("FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\DST / LipK")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\DST / LipK", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Public Property PhonemeTargetWeightEeeLipL19 As Single
            Get
                Return Flt("FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\Eee / LipL")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\Eee / LipL", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Public Property PhonemeTargetWeightEhLipR19 As Single
            Get
                Return Flt("FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\Eh / LipR")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\Eh / LipR", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Public Property PhonemeTargetWeightFVLipTh19 As Single
            Get
                Return Flt("FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\FV / LipTh")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\FV / LipTh", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\I</summary>
        Public Property PhonemeTargetWeightI19 As Single
            Get
                Return Flt("FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\I")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\I", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\K</summary>
        Public Property PhonemeTargetWeightK19 As Single
            Get
                Return Flt("FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\K")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\K", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\N</summary>
        Public Property PhonemeTargetWeightN19 As Single
            Get
                Return Flt("FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\N")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\N", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\Oh</summary>
        Public Property PhonemeTargetWeightOh19 As Single
            Get
                Return Flt("FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\Oh")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\Oh", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\OohQ</summary>
        Public Property PhonemeTargetWeightOohQ19 As Single
            Get
                Return Flt("FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\OohQ")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\OohQ", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\R</summary>
        Public Property PhonemeTargetWeightR19 As Single
            Get
                Return Flt("FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\R")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\R", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\TH</summary>
        Public Property PhonemeTargetWeightTH19 As Single
            Get
                Return Flt("FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\TH")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\TH", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\W</summary>
        Public Property PhonemeTargetWeightW19 As Single
            Get
                Return Flt("FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\W")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\W", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Public Property PhonemeTargetWeightAahLipBigAah20 As Single
            Get
                Return Flt("FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\Aah / LipBigAah")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\Aah / LipBigAah", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Public Property PhonemeTargetWeightBigAahLipDST20 As Single
            Get
                Return Flt("FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\BigAah / LipDST")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\BigAah / LipDST", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Public Property PhonemeTargetWeightBMPLipEee20 As Single
            Get
                Return Flt("FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\BMP / LipEee")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\BMP / LipEee", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Public Property PhonemeTargetWeightChJshLipFV20 As Single
            Get
                Return Flt("FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\ChJsh / LipFV")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\ChJsh / LipFV", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Public Property PhonemeTargetWeightDSTLipK20 As Single
            Get
                Return Flt("FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\DST / LipK")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\DST / LipK", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Public Property PhonemeTargetWeightEeeLipL20 As Single
            Get
                Return Flt("FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\Eee / LipL")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\Eee / LipL", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Public Property PhonemeTargetWeightEhLipR20 As Single
            Get
                Return Flt("FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\Eh / LipR")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\Eh / LipR", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Public Property PhonemeTargetWeightFVLipTh20 As Single
            Get
                Return Flt("FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\FV / LipTh")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\FV / LipTh", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\I</summary>
        Public Property PhonemeTargetWeightI20 As Single
            Get
                Return Flt("FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\I")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\I", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\K</summary>
        Public Property PhonemeTargetWeightK20 As Single
            Get
                Return Flt("FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\K")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\K", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\N</summary>
        Public Property PhonemeTargetWeightN20 As Single
            Get
                Return Flt("FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\N")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\N", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\Oh</summary>
        Public Property PhonemeTargetWeightOh20 As Single
            Get
                Return Flt("FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\Oh")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\Oh", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\OohQ</summary>
        Public Property PhonemeTargetWeightOohQ20 As Single
            Get
                Return Flt("FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\OohQ")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\OohQ", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\R</summary>
        Public Property PhonemeTargetWeightR20 As Single
            Get
                Return Flt("FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\R")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\R", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\TH</summary>
        Public Property PhonemeTargetWeightTH20 As Single
            Get
                Return Flt("FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\TH")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\TH", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\W</summary>
        Public Property PhonemeTargetWeightW20 As Single
            Get
                Return Flt("FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\W")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\W", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\F\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Public Property PhonemeTargetWeightAahLipBigAah21 As Single
            Get
                Return Flt("FaceFX Phonemes\F\PHWT\Phoneme Target Weight\Aah / LipBigAah")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\F\PHWT\Phoneme Target Weight\Aah / LipBigAah", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\F\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Public Property PhonemeTargetWeightBigAahLipDST21 As Single
            Get
                Return Flt("FaceFX Phonemes\F\PHWT\Phoneme Target Weight\BigAah / LipDST")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\F\PHWT\Phoneme Target Weight\BigAah / LipDST", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\F\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Public Property PhonemeTargetWeightBMPLipEee21 As Single
            Get
                Return Flt("FaceFX Phonemes\F\PHWT\Phoneme Target Weight\BMP / LipEee")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\F\PHWT\Phoneme Target Weight\BMP / LipEee", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\F\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Public Property PhonemeTargetWeightChJshLipFV21 As Single
            Get
                Return Flt("FaceFX Phonemes\F\PHWT\Phoneme Target Weight\ChJsh / LipFV")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\F\PHWT\Phoneme Target Weight\ChJsh / LipFV", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\F\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Public Property PhonemeTargetWeightDSTLipK21 As Single
            Get
                Return Flt("FaceFX Phonemes\F\PHWT\Phoneme Target Weight\DST / LipK")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\F\PHWT\Phoneme Target Weight\DST / LipK", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\F\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Public Property PhonemeTargetWeightEeeLipL21 As Single
            Get
                Return Flt("FaceFX Phonemes\F\PHWT\Phoneme Target Weight\Eee / LipL")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\F\PHWT\Phoneme Target Weight\Eee / LipL", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\F\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Public Property PhonemeTargetWeightEhLipR21 As Single
            Get
                Return Flt("FaceFX Phonemes\F\PHWT\Phoneme Target Weight\Eh / LipR")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\F\PHWT\Phoneme Target Weight\Eh / LipR", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\F\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Public Property PhonemeTargetWeightFVLipTh21 As Single
            Get
                Return Flt("FaceFX Phonemes\F\PHWT\Phoneme Target Weight\FV / LipTh")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\F\PHWT\Phoneme Target Weight\FV / LipTh", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\F\PHWT\Phoneme Target Weight\I</summary>
        Public Property PhonemeTargetWeightI21 As Single
            Get
                Return Flt("FaceFX Phonemes\F\PHWT\Phoneme Target Weight\I")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\F\PHWT\Phoneme Target Weight\I", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\F\PHWT\Phoneme Target Weight\K</summary>
        Public Property PhonemeTargetWeightK21 As Single
            Get
                Return Flt("FaceFX Phonemes\F\PHWT\Phoneme Target Weight\K")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\F\PHWT\Phoneme Target Weight\K", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\F\PHWT\Phoneme Target Weight\N</summary>
        Public Property PhonemeTargetWeightN21 As Single
            Get
                Return Flt("FaceFX Phonemes\F\PHWT\Phoneme Target Weight\N")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\F\PHWT\Phoneme Target Weight\N", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\F\PHWT\Phoneme Target Weight\Oh</summary>
        Public Property PhonemeTargetWeightOh21 As Single
            Get
                Return Flt("FaceFX Phonemes\F\PHWT\Phoneme Target Weight\Oh")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\F\PHWT\Phoneme Target Weight\Oh", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\F\PHWT\Phoneme Target Weight\OohQ</summary>
        Public Property PhonemeTargetWeightOohQ21 As Single
            Get
                Return Flt("FaceFX Phonemes\F\PHWT\Phoneme Target Weight\OohQ")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\F\PHWT\Phoneme Target Weight\OohQ", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\F\PHWT\Phoneme Target Weight\R</summary>
        Public Property PhonemeTargetWeightR21 As Single
            Get
                Return Flt("FaceFX Phonemes\F\PHWT\Phoneme Target Weight\R")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\F\PHWT\Phoneme Target Weight\R", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\F\PHWT\Phoneme Target Weight\TH</summary>
        Public Property PhonemeTargetWeightTH21 As Single
            Get
                Return Flt("FaceFX Phonemes\F\PHWT\Phoneme Target Weight\TH")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\F\PHWT\Phoneme Target Weight\TH", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\F\PHWT\Phoneme Target Weight\W</summary>
        Public Property PhonemeTargetWeightW21 As Single
            Get
                Return Flt("FaceFX Phonemes\F\PHWT\Phoneme Target Weight\W")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\F\PHWT\Phoneme Target Weight\W", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Public Property PhonemeTargetWeightAahLipBigAah22 As Single
            Get
                Return Flt("FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\Aah / LipBigAah")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\Aah / LipBigAah", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Public Property PhonemeTargetWeightBigAahLipDST22 As Single
            Get
                Return Flt("FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\BigAah / LipDST")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\BigAah / LipDST", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Public Property PhonemeTargetWeightBMPLipEee22 As Single
            Get
                Return Flt("FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\BMP / LipEee")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\BMP / LipEee", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Public Property PhonemeTargetWeightChJshLipFV22 As Single
            Get
                Return Flt("FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\ChJsh / LipFV")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\ChJsh / LipFV", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Public Property PhonemeTargetWeightDSTLipK22 As Single
            Get
                Return Flt("FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\DST / LipK")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\DST / LipK", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Public Property PhonemeTargetWeightEeeLipL22 As Single
            Get
                Return Flt("FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\Eee / LipL")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\Eee / LipL", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Public Property PhonemeTargetWeightEhLipR22 As Single
            Get
                Return Flt("FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\Eh / LipR")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\Eh / LipR", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Public Property PhonemeTargetWeightFVLipTh22 As Single
            Get
                Return Flt("FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\FV / LipTh")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\FV / LipTh", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\I</summary>
        Public Property PhonemeTargetWeightI22 As Single
            Get
                Return Flt("FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\I")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\I", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\K</summary>
        Public Property PhonemeTargetWeightK22 As Single
            Get
                Return Flt("FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\K")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\K", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\N</summary>
        Public Property PhonemeTargetWeightN22 As Single
            Get
                Return Flt("FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\N")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\N", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\Oh</summary>
        Public Property PhonemeTargetWeightOh22 As Single
            Get
                Return Flt("FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\Oh")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\Oh", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\OohQ</summary>
        Public Property PhonemeTargetWeightOohQ22 As Single
            Get
                Return Flt("FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\OohQ")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\OohQ", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\R</summary>
        Public Property PhonemeTargetWeightR22 As Single
            Get
                Return Flt("FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\R")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\R", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\TH</summary>
        Public Property PhonemeTargetWeightTH22 As Single
            Get
                Return Flt("FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\TH")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\TH", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\W</summary>
        Public Property PhonemeTargetWeightW22 As Single
            Get
                Return Flt("FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\W")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\W", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\V\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Public Property PhonemeTargetWeightAahLipBigAah23 As Single
            Get
                Return Flt("FaceFX Phonemes\V\PHWT\Phoneme Target Weight\Aah / LipBigAah")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\V\PHWT\Phoneme Target Weight\Aah / LipBigAah", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\V\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Public Property PhonemeTargetWeightBigAahLipDST23 As Single
            Get
                Return Flt("FaceFX Phonemes\V\PHWT\Phoneme Target Weight\BigAah / LipDST")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\V\PHWT\Phoneme Target Weight\BigAah / LipDST", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\V\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Public Property PhonemeTargetWeightBMPLipEee23 As Single
            Get
                Return Flt("FaceFX Phonemes\V\PHWT\Phoneme Target Weight\BMP / LipEee")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\V\PHWT\Phoneme Target Weight\BMP / LipEee", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\V\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Public Property PhonemeTargetWeightChJshLipFV23 As Single
            Get
                Return Flt("FaceFX Phonemes\V\PHWT\Phoneme Target Weight\ChJsh / LipFV")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\V\PHWT\Phoneme Target Weight\ChJsh / LipFV", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\V\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Public Property PhonemeTargetWeightDSTLipK23 As Single
            Get
                Return Flt("FaceFX Phonemes\V\PHWT\Phoneme Target Weight\DST / LipK")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\V\PHWT\Phoneme Target Weight\DST / LipK", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\V\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Public Property PhonemeTargetWeightEeeLipL23 As Single
            Get
                Return Flt("FaceFX Phonemes\V\PHWT\Phoneme Target Weight\Eee / LipL")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\V\PHWT\Phoneme Target Weight\Eee / LipL", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\V\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Public Property PhonemeTargetWeightEhLipR23 As Single
            Get
                Return Flt("FaceFX Phonemes\V\PHWT\Phoneme Target Weight\Eh / LipR")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\V\PHWT\Phoneme Target Weight\Eh / LipR", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\V\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Public Property PhonemeTargetWeightFVLipTh23 As Single
            Get
                Return Flt("FaceFX Phonemes\V\PHWT\Phoneme Target Weight\FV / LipTh")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\V\PHWT\Phoneme Target Weight\FV / LipTh", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\V\PHWT\Phoneme Target Weight\I</summary>
        Public Property PhonemeTargetWeightI23 As Single
            Get
                Return Flt("FaceFX Phonemes\V\PHWT\Phoneme Target Weight\I")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\V\PHWT\Phoneme Target Weight\I", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\V\PHWT\Phoneme Target Weight\K</summary>
        Public Property PhonemeTargetWeightK23 As Single
            Get
                Return Flt("FaceFX Phonemes\V\PHWT\Phoneme Target Weight\K")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\V\PHWT\Phoneme Target Weight\K", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\V\PHWT\Phoneme Target Weight\N</summary>
        Public Property PhonemeTargetWeightN23 As Single
            Get
                Return Flt("FaceFX Phonemes\V\PHWT\Phoneme Target Weight\N")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\V\PHWT\Phoneme Target Weight\N", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\V\PHWT\Phoneme Target Weight\Oh</summary>
        Public Property PhonemeTargetWeightOh23 As Single
            Get
                Return Flt("FaceFX Phonemes\V\PHWT\Phoneme Target Weight\Oh")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\V\PHWT\Phoneme Target Weight\Oh", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\V\PHWT\Phoneme Target Weight\OohQ</summary>
        Public Property PhonemeTargetWeightOohQ23 As Single
            Get
                Return Flt("FaceFX Phonemes\V\PHWT\Phoneme Target Weight\OohQ")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\V\PHWT\Phoneme Target Weight\OohQ", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\V\PHWT\Phoneme Target Weight\R</summary>
        Public Property PhonemeTargetWeightR23 As Single
            Get
                Return Flt("FaceFX Phonemes\V\PHWT\Phoneme Target Weight\R")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\V\PHWT\Phoneme Target Weight\R", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\V\PHWT\Phoneme Target Weight\TH</summary>
        Public Property PhonemeTargetWeightTH23 As Single
            Get
                Return Flt("FaceFX Phonemes\V\PHWT\Phoneme Target Weight\TH")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\V\PHWT\Phoneme Target Weight\TH", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\V\PHWT\Phoneme Target Weight\W</summary>
        Public Property PhonemeTargetWeightW23 As Single
            Get
                Return Flt("FaceFX Phonemes\V\PHWT\Phoneme Target Weight\W")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\V\PHWT\Phoneme Target Weight\W", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Public Property PhonemeTargetWeightAahLipBigAah24 As Single
            Get
                Return Flt("FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\Aah / LipBigAah")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\Aah / LipBigAah", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Public Property PhonemeTargetWeightBigAahLipDST24 As Single
            Get
                Return Flt("FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\BigAah / LipDST")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\BigAah / LipDST", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Public Property PhonemeTargetWeightBMPLipEee24 As Single
            Get
                Return Flt("FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\BMP / LipEee")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\BMP / LipEee", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Public Property PhonemeTargetWeightChJshLipFV24 As Single
            Get
                Return Flt("FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\ChJsh / LipFV")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\ChJsh / LipFV", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Public Property PhonemeTargetWeightDSTLipK24 As Single
            Get
                Return Flt("FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\DST / LipK")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\DST / LipK", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Public Property PhonemeTargetWeightEeeLipL24 As Single
            Get
                Return Flt("FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\Eee / LipL")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\Eee / LipL", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Public Property PhonemeTargetWeightEhLipR24 As Single
            Get
                Return Flt("FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\Eh / LipR")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\Eh / LipR", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Public Property PhonemeTargetWeightFVLipTh24 As Single
            Get
                Return Flt("FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\FV / LipTh")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\FV / LipTh", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\I</summary>
        Public Property PhonemeTargetWeightI24 As Single
            Get
                Return Flt("FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\I")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\I", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\K</summary>
        Public Property PhonemeTargetWeightK24 As Single
            Get
                Return Flt("FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\K")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\K", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\N</summary>
        Public Property PhonemeTargetWeightN24 As Single
            Get
                Return Flt("FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\N")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\N", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\Oh</summary>
        Public Property PhonemeTargetWeightOh24 As Single
            Get
                Return Flt("FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\Oh")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\Oh", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\OohQ</summary>
        Public Property PhonemeTargetWeightOohQ24 As Single
            Get
                Return Flt("FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\OohQ")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\OohQ", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\R</summary>
        Public Property PhonemeTargetWeightR24 As Single
            Get
                Return Flt("FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\R")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\R", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\TH</summary>
        Public Property PhonemeTargetWeightTH24 As Single
            Get
                Return Flt("FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\TH")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\TH", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\W</summary>
        Public Property PhonemeTargetWeightW24 As Single
            Get
                Return Flt("FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\W")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\W", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\M\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Public Property PhonemeTargetWeightAahLipBigAah25 As Single
            Get
                Return Flt("FaceFX Phonemes\M\PHWT\Phoneme Target Weight\Aah / LipBigAah")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\M\PHWT\Phoneme Target Weight\Aah / LipBigAah", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\M\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Public Property PhonemeTargetWeightBigAahLipDST25 As Single
            Get
                Return Flt("FaceFX Phonemes\M\PHWT\Phoneme Target Weight\BigAah / LipDST")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\M\PHWT\Phoneme Target Weight\BigAah / LipDST", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\M\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Public Property PhonemeTargetWeightBMPLipEee25 As Single
            Get
                Return Flt("FaceFX Phonemes\M\PHWT\Phoneme Target Weight\BMP / LipEee")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\M\PHWT\Phoneme Target Weight\BMP / LipEee", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\M\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Public Property PhonemeTargetWeightChJshLipFV25 As Single
            Get
                Return Flt("FaceFX Phonemes\M\PHWT\Phoneme Target Weight\ChJsh / LipFV")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\M\PHWT\Phoneme Target Weight\ChJsh / LipFV", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\M\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Public Property PhonemeTargetWeightDSTLipK25 As Single
            Get
                Return Flt("FaceFX Phonemes\M\PHWT\Phoneme Target Weight\DST / LipK")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\M\PHWT\Phoneme Target Weight\DST / LipK", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\M\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Public Property PhonemeTargetWeightEeeLipL25 As Single
            Get
                Return Flt("FaceFX Phonemes\M\PHWT\Phoneme Target Weight\Eee / LipL")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\M\PHWT\Phoneme Target Weight\Eee / LipL", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\M\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Public Property PhonemeTargetWeightEhLipR25 As Single
            Get
                Return Flt("FaceFX Phonemes\M\PHWT\Phoneme Target Weight\Eh / LipR")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\M\PHWT\Phoneme Target Weight\Eh / LipR", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\M\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Public Property PhonemeTargetWeightFVLipTh25 As Single
            Get
                Return Flt("FaceFX Phonemes\M\PHWT\Phoneme Target Weight\FV / LipTh")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\M\PHWT\Phoneme Target Weight\FV / LipTh", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\M\PHWT\Phoneme Target Weight\I</summary>
        Public Property PhonemeTargetWeightI25 As Single
            Get
                Return Flt("FaceFX Phonemes\M\PHWT\Phoneme Target Weight\I")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\M\PHWT\Phoneme Target Weight\I", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\M\PHWT\Phoneme Target Weight\K</summary>
        Public Property PhonemeTargetWeightK25 As Single
            Get
                Return Flt("FaceFX Phonemes\M\PHWT\Phoneme Target Weight\K")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\M\PHWT\Phoneme Target Weight\K", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\M\PHWT\Phoneme Target Weight\N</summary>
        Public Property PhonemeTargetWeightN25 As Single
            Get
                Return Flt("FaceFX Phonemes\M\PHWT\Phoneme Target Weight\N")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\M\PHWT\Phoneme Target Weight\N", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\M\PHWT\Phoneme Target Weight\Oh</summary>
        Public Property PhonemeTargetWeightOh25 As Single
            Get
                Return Flt("FaceFX Phonemes\M\PHWT\Phoneme Target Weight\Oh")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\M\PHWT\Phoneme Target Weight\Oh", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\M\PHWT\Phoneme Target Weight\OohQ</summary>
        Public Property PhonemeTargetWeightOohQ25 As Single
            Get
                Return Flt("FaceFX Phonemes\M\PHWT\Phoneme Target Weight\OohQ")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\M\PHWT\Phoneme Target Weight\OohQ", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\M\PHWT\Phoneme Target Weight\R</summary>
        Public Property PhonemeTargetWeightR25 As Single
            Get
                Return Flt("FaceFX Phonemes\M\PHWT\Phoneme Target Weight\R")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\M\PHWT\Phoneme Target Weight\R", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\M\PHWT\Phoneme Target Weight\TH</summary>
        Public Property PhonemeTargetWeightTH25 As Single
            Get
                Return Flt("FaceFX Phonemes\M\PHWT\Phoneme Target Weight\TH")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\M\PHWT\Phoneme Target Weight\TH", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\M\PHWT\Phoneme Target Weight\W</summary>
        Public Property PhonemeTargetWeightW25 As Single
            Get
                Return Flt("FaceFX Phonemes\M\PHWT\Phoneme Target Weight\W")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\M\PHWT\Phoneme Target Weight\W", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\N\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Public Property PhonemeTargetWeightAahLipBigAah26 As Single
            Get
                Return Flt("FaceFX Phonemes\N\PHWT\Phoneme Target Weight\Aah / LipBigAah")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\N\PHWT\Phoneme Target Weight\Aah / LipBigAah", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\N\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Public Property PhonemeTargetWeightBigAahLipDST26 As Single
            Get
                Return Flt("FaceFX Phonemes\N\PHWT\Phoneme Target Weight\BigAah / LipDST")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\N\PHWT\Phoneme Target Weight\BigAah / LipDST", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\N\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Public Property PhonemeTargetWeightBMPLipEee26 As Single
            Get
                Return Flt("FaceFX Phonemes\N\PHWT\Phoneme Target Weight\BMP / LipEee")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\N\PHWT\Phoneme Target Weight\BMP / LipEee", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\N\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Public Property PhonemeTargetWeightChJshLipFV26 As Single
            Get
                Return Flt("FaceFX Phonemes\N\PHWT\Phoneme Target Weight\ChJsh / LipFV")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\N\PHWT\Phoneme Target Weight\ChJsh / LipFV", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\N\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Public Property PhonemeTargetWeightDSTLipK26 As Single
            Get
                Return Flt("FaceFX Phonemes\N\PHWT\Phoneme Target Weight\DST / LipK")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\N\PHWT\Phoneme Target Weight\DST / LipK", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\N\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Public Property PhonemeTargetWeightEeeLipL26 As Single
            Get
                Return Flt("FaceFX Phonemes\N\PHWT\Phoneme Target Weight\Eee / LipL")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\N\PHWT\Phoneme Target Weight\Eee / LipL", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\N\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Public Property PhonemeTargetWeightEhLipR26 As Single
            Get
                Return Flt("FaceFX Phonemes\N\PHWT\Phoneme Target Weight\Eh / LipR")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\N\PHWT\Phoneme Target Weight\Eh / LipR", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\N\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Public Property PhonemeTargetWeightFVLipTh26 As Single
            Get
                Return Flt("FaceFX Phonemes\N\PHWT\Phoneme Target Weight\FV / LipTh")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\N\PHWT\Phoneme Target Weight\FV / LipTh", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\N\PHWT\Phoneme Target Weight\I</summary>
        Public Property PhonemeTargetWeightI26 As Single
            Get
                Return Flt("FaceFX Phonemes\N\PHWT\Phoneme Target Weight\I")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\N\PHWT\Phoneme Target Weight\I", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\N\PHWT\Phoneme Target Weight\K</summary>
        Public Property PhonemeTargetWeightK26 As Single
            Get
                Return Flt("FaceFX Phonemes\N\PHWT\Phoneme Target Weight\K")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\N\PHWT\Phoneme Target Weight\K", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\N\PHWT\Phoneme Target Weight\N</summary>
        Public Property PhonemeTargetWeightN26 As Single
            Get
                Return Flt("FaceFX Phonemes\N\PHWT\Phoneme Target Weight\N")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\N\PHWT\Phoneme Target Weight\N", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\N\PHWT\Phoneme Target Weight\Oh</summary>
        Public Property PhonemeTargetWeightOh26 As Single
            Get
                Return Flt("FaceFX Phonemes\N\PHWT\Phoneme Target Weight\Oh")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\N\PHWT\Phoneme Target Weight\Oh", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\N\PHWT\Phoneme Target Weight\OohQ</summary>
        Public Property PhonemeTargetWeightOohQ26 As Single
            Get
                Return Flt("FaceFX Phonemes\N\PHWT\Phoneme Target Weight\OohQ")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\N\PHWT\Phoneme Target Weight\OohQ", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\N\PHWT\Phoneme Target Weight\R</summary>
        Public Property PhonemeTargetWeightR26 As Single
            Get
                Return Flt("FaceFX Phonemes\N\PHWT\Phoneme Target Weight\R")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\N\PHWT\Phoneme Target Weight\R", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\N\PHWT\Phoneme Target Weight\TH</summary>
        Public Property PhonemeTargetWeightTH26 As Single
            Get
                Return Flt("FaceFX Phonemes\N\PHWT\Phoneme Target Weight\TH")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\N\PHWT\Phoneme Target Weight\TH", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\N\PHWT\Phoneme Target Weight\W</summary>
        Public Property PhonemeTargetWeightW26 As Single
            Get
                Return Flt("FaceFX Phonemes\N\PHWT\Phoneme Target Weight\W")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\N\PHWT\Phoneme Target Weight\W", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Public Property PhonemeTargetWeightAahLipBigAah27 As Single
            Get
                Return Flt("FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\Aah / LipBigAah")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\Aah / LipBigAah", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Public Property PhonemeTargetWeightBigAahLipDST27 As Single
            Get
                Return Flt("FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\BigAah / LipDST")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\BigAah / LipDST", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Public Property PhonemeTargetWeightBMPLipEee27 As Single
            Get
                Return Flt("FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\BMP / LipEee")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\BMP / LipEee", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Public Property PhonemeTargetWeightChJshLipFV27 As Single
            Get
                Return Flt("FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\ChJsh / LipFV")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\ChJsh / LipFV", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Public Property PhonemeTargetWeightDSTLipK27 As Single
            Get
                Return Flt("FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\DST / LipK")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\DST / LipK", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Public Property PhonemeTargetWeightEeeLipL27 As Single
            Get
                Return Flt("FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\Eee / LipL")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\Eee / LipL", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Public Property PhonemeTargetWeightEhLipR27 As Single
            Get
                Return Flt("FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\Eh / LipR")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\Eh / LipR", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Public Property PhonemeTargetWeightFVLipTh27 As Single
            Get
                Return Flt("FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\FV / LipTh")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\FV / LipTh", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\I</summary>
        Public Property PhonemeTargetWeightI27 As Single
            Get
                Return Flt("FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\I")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\I", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\K</summary>
        Public Property PhonemeTargetWeightK27 As Single
            Get
                Return Flt("FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\K")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\K", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\N</summary>
        Public Property PhonemeTargetWeightN27 As Single
            Get
                Return Flt("FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\N")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\N", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\Oh</summary>
        Public Property PhonemeTargetWeightOh27 As Single
            Get
                Return Flt("FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\Oh")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\Oh", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\OohQ</summary>
        Public Property PhonemeTargetWeightOohQ27 As Single
            Get
                Return Flt("FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\OohQ")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\OohQ", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\R</summary>
        Public Property PhonemeTargetWeightR27 As Single
            Get
                Return Flt("FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\R")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\R", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\TH</summary>
        Public Property PhonemeTargetWeightTH27 As Single
            Get
                Return Flt("FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\TH")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\TH", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\W</summary>
        Public Property PhonemeTargetWeightW27 As Single
            Get
                Return Flt("FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\W")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\W", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\L\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Public Property PhonemeTargetWeightAahLipBigAah28 As Single
            Get
                Return Flt("FaceFX Phonemes\L\PHWT\Phoneme Target Weight\Aah / LipBigAah")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\L\PHWT\Phoneme Target Weight\Aah / LipBigAah", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\L\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Public Property PhonemeTargetWeightBigAahLipDST28 As Single
            Get
                Return Flt("FaceFX Phonemes\L\PHWT\Phoneme Target Weight\BigAah / LipDST")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\L\PHWT\Phoneme Target Weight\BigAah / LipDST", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\L\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Public Property PhonemeTargetWeightBMPLipEee28 As Single
            Get
                Return Flt("FaceFX Phonemes\L\PHWT\Phoneme Target Weight\BMP / LipEee")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\L\PHWT\Phoneme Target Weight\BMP / LipEee", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\L\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Public Property PhonemeTargetWeightChJshLipFV28 As Single
            Get
                Return Flt("FaceFX Phonemes\L\PHWT\Phoneme Target Weight\ChJsh / LipFV")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\L\PHWT\Phoneme Target Weight\ChJsh / LipFV", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\L\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Public Property PhonemeTargetWeightDSTLipK28 As Single
            Get
                Return Flt("FaceFX Phonemes\L\PHWT\Phoneme Target Weight\DST / LipK")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\L\PHWT\Phoneme Target Weight\DST / LipK", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\L\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Public Property PhonemeTargetWeightEeeLipL28 As Single
            Get
                Return Flt("FaceFX Phonemes\L\PHWT\Phoneme Target Weight\Eee / LipL")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\L\PHWT\Phoneme Target Weight\Eee / LipL", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\L\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Public Property PhonemeTargetWeightEhLipR28 As Single
            Get
                Return Flt("FaceFX Phonemes\L\PHWT\Phoneme Target Weight\Eh / LipR")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\L\PHWT\Phoneme Target Weight\Eh / LipR", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\L\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Public Property PhonemeTargetWeightFVLipTh28 As Single
            Get
                Return Flt("FaceFX Phonemes\L\PHWT\Phoneme Target Weight\FV / LipTh")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\L\PHWT\Phoneme Target Weight\FV / LipTh", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\L\PHWT\Phoneme Target Weight\I</summary>
        Public Property PhonemeTargetWeightI28 As Single
            Get
                Return Flt("FaceFX Phonemes\L\PHWT\Phoneme Target Weight\I")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\L\PHWT\Phoneme Target Weight\I", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\L\PHWT\Phoneme Target Weight\K</summary>
        Public Property PhonemeTargetWeightK28 As Single
            Get
                Return Flt("FaceFX Phonemes\L\PHWT\Phoneme Target Weight\K")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\L\PHWT\Phoneme Target Weight\K", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\L\PHWT\Phoneme Target Weight\N</summary>
        Public Property PhonemeTargetWeightN28 As Single
            Get
                Return Flt("FaceFX Phonemes\L\PHWT\Phoneme Target Weight\N")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\L\PHWT\Phoneme Target Weight\N", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\L\PHWT\Phoneme Target Weight\Oh</summary>
        Public Property PhonemeTargetWeightOh28 As Single
            Get
                Return Flt("FaceFX Phonemes\L\PHWT\Phoneme Target Weight\Oh")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\L\PHWT\Phoneme Target Weight\Oh", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\L\PHWT\Phoneme Target Weight\OohQ</summary>
        Public Property PhonemeTargetWeightOohQ28 As Single
            Get
                Return Flt("FaceFX Phonemes\L\PHWT\Phoneme Target Weight\OohQ")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\L\PHWT\Phoneme Target Weight\OohQ", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\L\PHWT\Phoneme Target Weight\R</summary>
        Public Property PhonemeTargetWeightR28 As Single
            Get
                Return Flt("FaceFX Phonemes\L\PHWT\Phoneme Target Weight\R")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\L\PHWT\Phoneme Target Weight\R", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\L\PHWT\Phoneme Target Weight\TH</summary>
        Public Property PhonemeTargetWeightTH28 As Single
            Get
                Return Flt("FaceFX Phonemes\L\PHWT\Phoneme Target Weight\TH")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\L\PHWT\Phoneme Target Weight\TH", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\L\PHWT\Phoneme Target Weight\W</summary>
        Public Property PhonemeTargetWeightW28 As Single
            Get
                Return Flt("FaceFX Phonemes\L\PHWT\Phoneme Target Weight\W")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\L\PHWT\Phoneme Target Weight\W", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\R\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Public Property PhonemeTargetWeightAahLipBigAah29 As Single
            Get
                Return Flt("FaceFX Phonemes\R\PHWT\Phoneme Target Weight\Aah / LipBigAah")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\R\PHWT\Phoneme Target Weight\Aah / LipBigAah", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\R\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Public Property PhonemeTargetWeightBigAahLipDST29 As Single
            Get
                Return Flt("FaceFX Phonemes\R\PHWT\Phoneme Target Weight\BigAah / LipDST")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\R\PHWT\Phoneme Target Weight\BigAah / LipDST", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\R\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Public Property PhonemeTargetWeightBMPLipEee29 As Single
            Get
                Return Flt("FaceFX Phonemes\R\PHWT\Phoneme Target Weight\BMP / LipEee")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\R\PHWT\Phoneme Target Weight\BMP / LipEee", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\R\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Public Property PhonemeTargetWeightChJshLipFV29 As Single
            Get
                Return Flt("FaceFX Phonemes\R\PHWT\Phoneme Target Weight\ChJsh / LipFV")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\R\PHWT\Phoneme Target Weight\ChJsh / LipFV", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\R\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Public Property PhonemeTargetWeightDSTLipK29 As Single
            Get
                Return Flt("FaceFX Phonemes\R\PHWT\Phoneme Target Weight\DST / LipK")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\R\PHWT\Phoneme Target Weight\DST / LipK", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\R\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Public Property PhonemeTargetWeightEeeLipL29 As Single
            Get
                Return Flt("FaceFX Phonemes\R\PHWT\Phoneme Target Weight\Eee / LipL")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\R\PHWT\Phoneme Target Weight\Eee / LipL", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\R\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Public Property PhonemeTargetWeightEhLipR29 As Single
            Get
                Return Flt("FaceFX Phonemes\R\PHWT\Phoneme Target Weight\Eh / LipR")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\R\PHWT\Phoneme Target Weight\Eh / LipR", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\R\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Public Property PhonemeTargetWeightFVLipTh29 As Single
            Get
                Return Flt("FaceFX Phonemes\R\PHWT\Phoneme Target Weight\FV / LipTh")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\R\PHWT\Phoneme Target Weight\FV / LipTh", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\R\PHWT\Phoneme Target Weight\I</summary>
        Public Property PhonemeTargetWeightI29 As Single
            Get
                Return Flt("FaceFX Phonemes\R\PHWT\Phoneme Target Weight\I")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\R\PHWT\Phoneme Target Weight\I", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\R\PHWT\Phoneme Target Weight\K</summary>
        Public Property PhonemeTargetWeightK29 As Single
            Get
                Return Flt("FaceFX Phonemes\R\PHWT\Phoneme Target Weight\K")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\R\PHWT\Phoneme Target Weight\K", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\R\PHWT\Phoneme Target Weight\N</summary>
        Public Property PhonemeTargetWeightN29 As Single
            Get
                Return Flt("FaceFX Phonemes\R\PHWT\Phoneme Target Weight\N")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\R\PHWT\Phoneme Target Weight\N", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\R\PHWT\Phoneme Target Weight\Oh</summary>
        Public Property PhonemeTargetWeightOh29 As Single
            Get
                Return Flt("FaceFX Phonemes\R\PHWT\Phoneme Target Weight\Oh")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\R\PHWT\Phoneme Target Weight\Oh", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\R\PHWT\Phoneme Target Weight\OohQ</summary>
        Public Property PhonemeTargetWeightOohQ29 As Single
            Get
                Return Flt("FaceFX Phonemes\R\PHWT\Phoneme Target Weight\OohQ")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\R\PHWT\Phoneme Target Weight\OohQ", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\R\PHWT\Phoneme Target Weight\R</summary>
        Public Property PhonemeTargetWeightR29 As Single
            Get
                Return Flt("FaceFX Phonemes\R\PHWT\Phoneme Target Weight\R")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\R\PHWT\Phoneme Target Weight\R", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\R\PHWT\Phoneme Target Weight\TH</summary>
        Public Property PhonemeTargetWeightTH29 As Single
            Get
                Return Flt("FaceFX Phonemes\R\PHWT\Phoneme Target Weight\TH")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\R\PHWT\Phoneme Target Weight\TH", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\R\PHWT\Phoneme Target Weight\W</summary>
        Public Property PhonemeTargetWeightW29 As Single
            Get
                Return Flt("FaceFX Phonemes\R\PHWT\Phoneme Target Weight\W")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\R\PHWT\Phoneme Target Weight\W", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\W\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Public Property PhonemeTargetWeightAahLipBigAah30 As Single
            Get
                Return Flt("FaceFX Phonemes\W\PHWT\Phoneme Target Weight\Aah / LipBigAah")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\W\PHWT\Phoneme Target Weight\Aah / LipBigAah", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\W\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Public Property PhonemeTargetWeightBigAahLipDST30 As Single
            Get
                Return Flt("FaceFX Phonemes\W\PHWT\Phoneme Target Weight\BigAah / LipDST")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\W\PHWT\Phoneme Target Weight\BigAah / LipDST", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\W\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Public Property PhonemeTargetWeightBMPLipEee30 As Single
            Get
                Return Flt("FaceFX Phonemes\W\PHWT\Phoneme Target Weight\BMP / LipEee")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\W\PHWT\Phoneme Target Weight\BMP / LipEee", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\W\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Public Property PhonemeTargetWeightChJshLipFV30 As Single
            Get
                Return Flt("FaceFX Phonemes\W\PHWT\Phoneme Target Weight\ChJsh / LipFV")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\W\PHWT\Phoneme Target Weight\ChJsh / LipFV", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\W\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Public Property PhonemeTargetWeightDSTLipK30 As Single
            Get
                Return Flt("FaceFX Phonemes\W\PHWT\Phoneme Target Weight\DST / LipK")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\W\PHWT\Phoneme Target Weight\DST / LipK", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\W\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Public Property PhonemeTargetWeightEeeLipL30 As Single
            Get
                Return Flt("FaceFX Phonemes\W\PHWT\Phoneme Target Weight\Eee / LipL")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\W\PHWT\Phoneme Target Weight\Eee / LipL", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\W\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Public Property PhonemeTargetWeightEhLipR30 As Single
            Get
                Return Flt("FaceFX Phonemes\W\PHWT\Phoneme Target Weight\Eh / LipR")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\W\PHWT\Phoneme Target Weight\Eh / LipR", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\W\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Public Property PhonemeTargetWeightFVLipTh30 As Single
            Get
                Return Flt("FaceFX Phonemes\W\PHWT\Phoneme Target Weight\FV / LipTh")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\W\PHWT\Phoneme Target Weight\FV / LipTh", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\W\PHWT\Phoneme Target Weight\I</summary>
        Public Property PhonemeTargetWeightI30 As Single
            Get
                Return Flt("FaceFX Phonemes\W\PHWT\Phoneme Target Weight\I")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\W\PHWT\Phoneme Target Weight\I", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\W\PHWT\Phoneme Target Weight\K</summary>
        Public Property PhonemeTargetWeightK30 As Single
            Get
                Return Flt("FaceFX Phonemes\W\PHWT\Phoneme Target Weight\K")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\W\PHWT\Phoneme Target Weight\K", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\W\PHWT\Phoneme Target Weight\N</summary>
        Public Property PhonemeTargetWeightN30 As Single
            Get
                Return Flt("FaceFX Phonemes\W\PHWT\Phoneme Target Weight\N")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\W\PHWT\Phoneme Target Weight\N", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\W\PHWT\Phoneme Target Weight\Oh</summary>
        Public Property PhonemeTargetWeightOh30 As Single
            Get
                Return Flt("FaceFX Phonemes\W\PHWT\Phoneme Target Weight\Oh")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\W\PHWT\Phoneme Target Weight\Oh", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\W\PHWT\Phoneme Target Weight\OohQ</summary>
        Public Property PhonemeTargetWeightOohQ30 As Single
            Get
                Return Flt("FaceFX Phonemes\W\PHWT\Phoneme Target Weight\OohQ")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\W\PHWT\Phoneme Target Weight\OohQ", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\W\PHWT\Phoneme Target Weight\R</summary>
        Public Property PhonemeTargetWeightR30 As Single
            Get
                Return Flt("FaceFX Phonemes\W\PHWT\Phoneme Target Weight\R")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\W\PHWT\Phoneme Target Weight\R", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\W\PHWT\Phoneme Target Weight\TH</summary>
        Public Property PhonemeTargetWeightTH30 As Single
            Get
                Return Flt("FaceFX Phonemes\W\PHWT\Phoneme Target Weight\TH")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\W\PHWT\Phoneme Target Weight\TH", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\W\PHWT\Phoneme Target Weight\W</summary>
        Public Property PhonemeTargetWeightW30 As Single
            Get
                Return Flt("FaceFX Phonemes\W\PHWT\Phoneme Target Weight\W")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\W\PHWT\Phoneme Target Weight\W", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Public Property PhonemeTargetWeightAahLipBigAah31 As Single
            Get
                Return Flt("FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\Aah / LipBigAah")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\Aah / LipBigAah", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Public Property PhonemeTargetWeightBigAahLipDST31 As Single
            Get
                Return Flt("FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\BigAah / LipDST")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\BigAah / LipDST", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Public Property PhonemeTargetWeightBMPLipEee31 As Single
            Get
                Return Flt("FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\BMP / LipEee")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\BMP / LipEee", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Public Property PhonemeTargetWeightChJshLipFV31 As Single
            Get
                Return Flt("FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\ChJsh / LipFV")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\ChJsh / LipFV", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Public Property PhonemeTargetWeightDSTLipK31 As Single
            Get
                Return Flt("FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\DST / LipK")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\DST / LipK", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Public Property PhonemeTargetWeightEeeLipL31 As Single
            Get
                Return Flt("FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\Eee / LipL")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\Eee / LipL", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Public Property PhonemeTargetWeightEhLipR31 As Single
            Get
                Return Flt("FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\Eh / LipR")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\Eh / LipR", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Public Property PhonemeTargetWeightFVLipTh31 As Single
            Get
                Return Flt("FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\FV / LipTh")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\FV / LipTh", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\I</summary>
        Public Property PhonemeTargetWeightI31 As Single
            Get
                Return Flt("FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\I")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\I", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\K</summary>
        Public Property PhonemeTargetWeightK31 As Single
            Get
                Return Flt("FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\K")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\K", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\N</summary>
        Public Property PhonemeTargetWeightN31 As Single
            Get
                Return Flt("FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\N")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\N", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\Oh</summary>
        Public Property PhonemeTargetWeightOh31 As Single
            Get
                Return Flt("FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\Oh")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\Oh", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\OohQ</summary>
        Public Property PhonemeTargetWeightOohQ31 As Single
            Get
                Return Flt("FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\OohQ")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\OohQ", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\R</summary>
        Public Property PhonemeTargetWeightR31 As Single
            Get
                Return Flt("FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\R")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\R", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\TH</summary>
        Public Property PhonemeTargetWeightTH31 As Single
            Get
                Return Flt("FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\TH")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\TH", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\W</summary>
        Public Property PhonemeTargetWeightW31 As Single
            Get
                Return Flt("FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\W")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\W", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Public Property PhonemeTargetWeightAahLipBigAah32 As Single
            Get
                Return Flt("FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\Aah / LipBigAah")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\Aah / LipBigAah", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Public Property PhonemeTargetWeightBigAahLipDST32 As Single
            Get
                Return Flt("FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\BigAah / LipDST")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\BigAah / LipDST", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Public Property PhonemeTargetWeightBMPLipEee32 As Single
            Get
                Return Flt("FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\BMP / LipEee")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\BMP / LipEee", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Public Property PhonemeTargetWeightChJshLipFV32 As Single
            Get
                Return Flt("FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\ChJsh / LipFV")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\ChJsh / LipFV", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Public Property PhonemeTargetWeightDSTLipK32 As Single
            Get
                Return Flt("FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\DST / LipK")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\DST / LipK", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Public Property PhonemeTargetWeightEeeLipL32 As Single
            Get
                Return Flt("FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\Eee / LipL")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\Eee / LipL", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Public Property PhonemeTargetWeightEhLipR32 As Single
            Get
                Return Flt("FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\Eh / LipR")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\Eh / LipR", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Public Property PhonemeTargetWeightFVLipTh32 As Single
            Get
                Return Flt("FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\FV / LipTh")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\FV / LipTh", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\I</summary>
        Public Property PhonemeTargetWeightI32 As Single
            Get
                Return Flt("FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\I")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\I", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\K</summary>
        Public Property PhonemeTargetWeightK32 As Single
            Get
                Return Flt("FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\K")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\K", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\N</summary>
        Public Property PhonemeTargetWeightN32 As Single
            Get
                Return Flt("FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\N")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\N", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\Oh</summary>
        Public Property PhonemeTargetWeightOh32 As Single
            Get
                Return Flt("FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\Oh")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\Oh", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\OohQ</summary>
        Public Property PhonemeTargetWeightOohQ32 As Single
            Get
                Return Flt("FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\OohQ")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\OohQ", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\R</summary>
        Public Property PhonemeTargetWeightR32 As Single
            Get
                Return Flt("FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\R")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\R", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\TH</summary>
        Public Property PhonemeTargetWeightTH32 As Single
            Get
                Return Flt("FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\TH")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\TH", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\W</summary>
        Public Property PhonemeTargetWeightW32 As Single
            Get
                Return Flt("FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\W")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\W", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\B\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Public Property PhonemeTargetWeightAahLipBigAah33 As Single
            Get
                Return Flt("FaceFX Phonemes\B\PHWT\Phoneme Target Weight\Aah / LipBigAah")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\B\PHWT\Phoneme Target Weight\Aah / LipBigAah", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\B\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Public Property PhonemeTargetWeightBigAahLipDST33 As Single
            Get
                Return Flt("FaceFX Phonemes\B\PHWT\Phoneme Target Weight\BigAah / LipDST")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\B\PHWT\Phoneme Target Weight\BigAah / LipDST", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\B\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Public Property PhonemeTargetWeightBMPLipEee33 As Single
            Get
                Return Flt("FaceFX Phonemes\B\PHWT\Phoneme Target Weight\BMP / LipEee")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\B\PHWT\Phoneme Target Weight\BMP / LipEee", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\B\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Public Property PhonemeTargetWeightChJshLipFV33 As Single
            Get
                Return Flt("FaceFX Phonemes\B\PHWT\Phoneme Target Weight\ChJsh / LipFV")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\B\PHWT\Phoneme Target Weight\ChJsh / LipFV", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\B\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Public Property PhonemeTargetWeightDSTLipK33 As Single
            Get
                Return Flt("FaceFX Phonemes\B\PHWT\Phoneme Target Weight\DST / LipK")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\B\PHWT\Phoneme Target Weight\DST / LipK", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\B\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Public Property PhonemeTargetWeightEeeLipL33 As Single
            Get
                Return Flt("FaceFX Phonemes\B\PHWT\Phoneme Target Weight\Eee / LipL")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\B\PHWT\Phoneme Target Weight\Eee / LipL", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\B\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Public Property PhonemeTargetWeightEhLipR33 As Single
            Get
                Return Flt("FaceFX Phonemes\B\PHWT\Phoneme Target Weight\Eh / LipR")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\B\PHWT\Phoneme Target Weight\Eh / LipR", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\B\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Public Property PhonemeTargetWeightFVLipTh33 As Single
            Get
                Return Flt("FaceFX Phonemes\B\PHWT\Phoneme Target Weight\FV / LipTh")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\B\PHWT\Phoneme Target Weight\FV / LipTh", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\B\PHWT\Phoneme Target Weight\I</summary>
        Public Property PhonemeTargetWeightI33 As Single
            Get
                Return Flt("FaceFX Phonemes\B\PHWT\Phoneme Target Weight\I")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\B\PHWT\Phoneme Target Weight\I", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\B\PHWT\Phoneme Target Weight\K</summary>
        Public Property PhonemeTargetWeightK33 As Single
            Get
                Return Flt("FaceFX Phonemes\B\PHWT\Phoneme Target Weight\K")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\B\PHWT\Phoneme Target Weight\K", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\B\PHWT\Phoneme Target Weight\N</summary>
        Public Property PhonemeTargetWeightN33 As Single
            Get
                Return Flt("FaceFX Phonemes\B\PHWT\Phoneme Target Weight\N")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\B\PHWT\Phoneme Target Weight\N", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\B\PHWT\Phoneme Target Weight\Oh</summary>
        Public Property PhonemeTargetWeightOh33 As Single
            Get
                Return Flt("FaceFX Phonemes\B\PHWT\Phoneme Target Weight\Oh")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\B\PHWT\Phoneme Target Weight\Oh", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\B\PHWT\Phoneme Target Weight\OohQ</summary>
        Public Property PhonemeTargetWeightOohQ33 As Single
            Get
                Return Flt("FaceFX Phonemes\B\PHWT\Phoneme Target Weight\OohQ")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\B\PHWT\Phoneme Target Weight\OohQ", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\B\PHWT\Phoneme Target Weight\R</summary>
        Public Property PhonemeTargetWeightR33 As Single
            Get
                Return Flt("FaceFX Phonemes\B\PHWT\Phoneme Target Weight\R")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\B\PHWT\Phoneme Target Weight\R", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\B\PHWT\Phoneme Target Weight\TH</summary>
        Public Property PhonemeTargetWeightTH33 As Single
            Get
                Return Flt("FaceFX Phonemes\B\PHWT\Phoneme Target Weight\TH")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\B\PHWT\Phoneme Target Weight\TH", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\B\PHWT\Phoneme Target Weight\W</summary>
        Public Property PhonemeTargetWeightW33 As Single
            Get
                Return Flt("FaceFX Phonemes\B\PHWT\Phoneme Target Weight\W")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\B\PHWT\Phoneme Target Weight\W", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\D\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Public Property PhonemeTargetWeightAahLipBigAah34 As Single
            Get
                Return Flt("FaceFX Phonemes\D\PHWT\Phoneme Target Weight\Aah / LipBigAah")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\D\PHWT\Phoneme Target Weight\Aah / LipBigAah", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\D\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Public Property PhonemeTargetWeightBigAahLipDST34 As Single
            Get
                Return Flt("FaceFX Phonemes\D\PHWT\Phoneme Target Weight\BigAah / LipDST")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\D\PHWT\Phoneme Target Weight\BigAah / LipDST", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\D\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Public Property PhonemeTargetWeightBMPLipEee34 As Single
            Get
                Return Flt("FaceFX Phonemes\D\PHWT\Phoneme Target Weight\BMP / LipEee")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\D\PHWT\Phoneme Target Weight\BMP / LipEee", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\D\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Public Property PhonemeTargetWeightChJshLipFV34 As Single
            Get
                Return Flt("FaceFX Phonemes\D\PHWT\Phoneme Target Weight\ChJsh / LipFV")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\D\PHWT\Phoneme Target Weight\ChJsh / LipFV", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\D\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Public Property PhonemeTargetWeightDSTLipK34 As Single
            Get
                Return Flt("FaceFX Phonemes\D\PHWT\Phoneme Target Weight\DST / LipK")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\D\PHWT\Phoneme Target Weight\DST / LipK", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\D\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Public Property PhonemeTargetWeightEeeLipL34 As Single
            Get
                Return Flt("FaceFX Phonemes\D\PHWT\Phoneme Target Weight\Eee / LipL")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\D\PHWT\Phoneme Target Weight\Eee / LipL", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\D\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Public Property PhonemeTargetWeightEhLipR34 As Single
            Get
                Return Flt("FaceFX Phonemes\D\PHWT\Phoneme Target Weight\Eh / LipR")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\D\PHWT\Phoneme Target Weight\Eh / LipR", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\D\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Public Property PhonemeTargetWeightFVLipTh34 As Single
            Get
                Return Flt("FaceFX Phonemes\D\PHWT\Phoneme Target Weight\FV / LipTh")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\D\PHWT\Phoneme Target Weight\FV / LipTh", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\D\PHWT\Phoneme Target Weight\I</summary>
        Public Property PhonemeTargetWeightI34 As Single
            Get
                Return Flt("FaceFX Phonemes\D\PHWT\Phoneme Target Weight\I")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\D\PHWT\Phoneme Target Weight\I", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\D\PHWT\Phoneme Target Weight\K</summary>
        Public Property PhonemeTargetWeightK34 As Single
            Get
                Return Flt("FaceFX Phonemes\D\PHWT\Phoneme Target Weight\K")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\D\PHWT\Phoneme Target Weight\K", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\D\PHWT\Phoneme Target Weight\N</summary>
        Public Property PhonemeTargetWeightN34 As Single
            Get
                Return Flt("FaceFX Phonemes\D\PHWT\Phoneme Target Weight\N")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\D\PHWT\Phoneme Target Weight\N", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\D\PHWT\Phoneme Target Weight\Oh</summary>
        Public Property PhonemeTargetWeightOh34 As Single
            Get
                Return Flt("FaceFX Phonemes\D\PHWT\Phoneme Target Weight\Oh")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\D\PHWT\Phoneme Target Weight\Oh", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\D\PHWT\Phoneme Target Weight\OohQ</summary>
        Public Property PhonemeTargetWeightOohQ34 As Single
            Get
                Return Flt("FaceFX Phonemes\D\PHWT\Phoneme Target Weight\OohQ")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\D\PHWT\Phoneme Target Weight\OohQ", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\D\PHWT\Phoneme Target Weight\R</summary>
        Public Property PhonemeTargetWeightR34 As Single
            Get
                Return Flt("FaceFX Phonemes\D\PHWT\Phoneme Target Weight\R")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\D\PHWT\Phoneme Target Weight\R", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\D\PHWT\Phoneme Target Weight\TH</summary>
        Public Property PhonemeTargetWeightTH34 As Single
            Get
                Return Flt("FaceFX Phonemes\D\PHWT\Phoneme Target Weight\TH")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\D\PHWT\Phoneme Target Weight\TH", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\D\PHWT\Phoneme Target Weight\W</summary>
        Public Property PhonemeTargetWeightW34 As Single
            Get
                Return Flt("FaceFX Phonemes\D\PHWT\Phoneme Target Weight\W")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\D\PHWT\Phoneme Target Weight\W", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Public Property PhonemeTargetWeightAahLipBigAah35 As Single
            Get
                Return Flt("FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\Aah / LipBigAah")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\Aah / LipBigAah", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Public Property PhonemeTargetWeightBigAahLipDST35 As Single
            Get
                Return Flt("FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\BigAah / LipDST")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\BigAah / LipDST", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Public Property PhonemeTargetWeightBMPLipEee35 As Single
            Get
                Return Flt("FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\BMP / LipEee")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\BMP / LipEee", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Public Property PhonemeTargetWeightChJshLipFV35 As Single
            Get
                Return Flt("FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\ChJsh / LipFV")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\ChJsh / LipFV", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Public Property PhonemeTargetWeightDSTLipK35 As Single
            Get
                Return Flt("FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\DST / LipK")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\DST / LipK", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Public Property PhonemeTargetWeightEeeLipL35 As Single
            Get
                Return Flt("FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\Eee / LipL")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\Eee / LipL", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Public Property PhonemeTargetWeightEhLipR35 As Single
            Get
                Return Flt("FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\Eh / LipR")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\Eh / LipR", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Public Property PhonemeTargetWeightFVLipTh35 As Single
            Get
                Return Flt("FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\FV / LipTh")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\FV / LipTh", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\I</summary>
        Public Property PhonemeTargetWeightI35 As Single
            Get
                Return Flt("FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\I")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\I", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\K</summary>
        Public Property PhonemeTargetWeightK35 As Single
            Get
                Return Flt("FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\K")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\K", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\N</summary>
        Public Property PhonemeTargetWeightN35 As Single
            Get
                Return Flt("FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\N")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\N", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\Oh</summary>
        Public Property PhonemeTargetWeightOh35 As Single
            Get
                Return Flt("FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\Oh")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\Oh", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\OohQ</summary>
        Public Property PhonemeTargetWeightOohQ35 As Single
            Get
                Return Flt("FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\OohQ")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\OohQ", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\R</summary>
        Public Property PhonemeTargetWeightR35 As Single
            Get
                Return Flt("FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\R")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\R", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\TH</summary>
        Public Property PhonemeTargetWeightTH35 As Single
            Get
                Return Flt("FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\TH")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\TH", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\W</summary>
        Public Property PhonemeTargetWeightW35 As Single
            Get
                Return Flt("FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\W")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\W", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\G\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Public Property PhonemeTargetWeightAahLipBigAah36 As Single
            Get
                Return Flt("FaceFX Phonemes\G\PHWT\Phoneme Target Weight\Aah / LipBigAah")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\G\PHWT\Phoneme Target Weight\Aah / LipBigAah", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\G\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Public Property PhonemeTargetWeightBigAahLipDST36 As Single
            Get
                Return Flt("FaceFX Phonemes\G\PHWT\Phoneme Target Weight\BigAah / LipDST")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\G\PHWT\Phoneme Target Weight\BigAah / LipDST", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\G\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Public Property PhonemeTargetWeightBMPLipEee36 As Single
            Get
                Return Flt("FaceFX Phonemes\G\PHWT\Phoneme Target Weight\BMP / LipEee")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\G\PHWT\Phoneme Target Weight\BMP / LipEee", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\G\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Public Property PhonemeTargetWeightChJshLipFV36 As Single
            Get
                Return Flt("FaceFX Phonemes\G\PHWT\Phoneme Target Weight\ChJsh / LipFV")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\G\PHWT\Phoneme Target Weight\ChJsh / LipFV", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\G\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Public Property PhonemeTargetWeightDSTLipK36 As Single
            Get
                Return Flt("FaceFX Phonemes\G\PHWT\Phoneme Target Weight\DST / LipK")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\G\PHWT\Phoneme Target Weight\DST / LipK", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\G\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Public Property PhonemeTargetWeightEeeLipL36 As Single
            Get
                Return Flt("FaceFX Phonemes\G\PHWT\Phoneme Target Weight\Eee / LipL")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\G\PHWT\Phoneme Target Weight\Eee / LipL", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\G\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Public Property PhonemeTargetWeightEhLipR36 As Single
            Get
                Return Flt("FaceFX Phonemes\G\PHWT\Phoneme Target Weight\Eh / LipR")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\G\PHWT\Phoneme Target Weight\Eh / LipR", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\G\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Public Property PhonemeTargetWeightFVLipTh36 As Single
            Get
                Return Flt("FaceFX Phonemes\G\PHWT\Phoneme Target Weight\FV / LipTh")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\G\PHWT\Phoneme Target Weight\FV / LipTh", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\G\PHWT\Phoneme Target Weight\I</summary>
        Public Property PhonemeTargetWeightI36 As Single
            Get
                Return Flt("FaceFX Phonemes\G\PHWT\Phoneme Target Weight\I")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\G\PHWT\Phoneme Target Weight\I", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\G\PHWT\Phoneme Target Weight\K</summary>
        Public Property PhonemeTargetWeightK36 As Single
            Get
                Return Flt("FaceFX Phonemes\G\PHWT\Phoneme Target Weight\K")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\G\PHWT\Phoneme Target Weight\K", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\G\PHWT\Phoneme Target Weight\N</summary>
        Public Property PhonemeTargetWeightN36 As Single
            Get
                Return Flt("FaceFX Phonemes\G\PHWT\Phoneme Target Weight\N")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\G\PHWT\Phoneme Target Weight\N", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\G\PHWT\Phoneme Target Weight\Oh</summary>
        Public Property PhonemeTargetWeightOh36 As Single
            Get
                Return Flt("FaceFX Phonemes\G\PHWT\Phoneme Target Weight\Oh")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\G\PHWT\Phoneme Target Weight\Oh", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\G\PHWT\Phoneme Target Weight\OohQ</summary>
        Public Property PhonemeTargetWeightOohQ36 As Single
            Get
                Return Flt("FaceFX Phonemes\G\PHWT\Phoneme Target Weight\OohQ")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\G\PHWT\Phoneme Target Weight\OohQ", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\G\PHWT\Phoneme Target Weight\R</summary>
        Public Property PhonemeTargetWeightR36 As Single
            Get
                Return Flt("FaceFX Phonemes\G\PHWT\Phoneme Target Weight\R")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\G\PHWT\Phoneme Target Weight\R", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\G\PHWT\Phoneme Target Weight\TH</summary>
        Public Property PhonemeTargetWeightTH36 As Single
            Get
                Return Flt("FaceFX Phonemes\G\PHWT\Phoneme Target Weight\TH")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\G\PHWT\Phoneme Target Weight\TH", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\G\PHWT\Phoneme Target Weight\W</summary>
        Public Property PhonemeTargetWeightW36 As Single
            Get
                Return Flt("FaceFX Phonemes\G\PHWT\Phoneme Target Weight\W")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\G\PHWT\Phoneme Target Weight\W", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\P\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Public Property PhonemeTargetWeightAahLipBigAah37 As Single
            Get
                Return Flt("FaceFX Phonemes\P\PHWT\Phoneme Target Weight\Aah / LipBigAah")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\P\PHWT\Phoneme Target Weight\Aah / LipBigAah", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\P\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Public Property PhonemeTargetWeightBigAahLipDST37 As Single
            Get
                Return Flt("FaceFX Phonemes\P\PHWT\Phoneme Target Weight\BigAah / LipDST")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\P\PHWT\Phoneme Target Weight\BigAah / LipDST", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\P\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Public Property PhonemeTargetWeightBMPLipEee37 As Single
            Get
                Return Flt("FaceFX Phonemes\P\PHWT\Phoneme Target Weight\BMP / LipEee")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\P\PHWT\Phoneme Target Weight\BMP / LipEee", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\P\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Public Property PhonemeTargetWeightChJshLipFV37 As Single
            Get
                Return Flt("FaceFX Phonemes\P\PHWT\Phoneme Target Weight\ChJsh / LipFV")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\P\PHWT\Phoneme Target Weight\ChJsh / LipFV", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\P\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Public Property PhonemeTargetWeightDSTLipK37 As Single
            Get
                Return Flt("FaceFX Phonemes\P\PHWT\Phoneme Target Weight\DST / LipK")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\P\PHWT\Phoneme Target Weight\DST / LipK", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\P\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Public Property PhonemeTargetWeightEeeLipL37 As Single
            Get
                Return Flt("FaceFX Phonemes\P\PHWT\Phoneme Target Weight\Eee / LipL")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\P\PHWT\Phoneme Target Weight\Eee / LipL", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\P\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Public Property PhonemeTargetWeightEhLipR37 As Single
            Get
                Return Flt("FaceFX Phonemes\P\PHWT\Phoneme Target Weight\Eh / LipR")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\P\PHWT\Phoneme Target Weight\Eh / LipR", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\P\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Public Property PhonemeTargetWeightFVLipTh37 As Single
            Get
                Return Flt("FaceFX Phonemes\P\PHWT\Phoneme Target Weight\FV / LipTh")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\P\PHWT\Phoneme Target Weight\FV / LipTh", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\P\PHWT\Phoneme Target Weight\I</summary>
        Public Property PhonemeTargetWeightI37 As Single
            Get
                Return Flt("FaceFX Phonemes\P\PHWT\Phoneme Target Weight\I")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\P\PHWT\Phoneme Target Weight\I", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\P\PHWT\Phoneme Target Weight\K</summary>
        Public Property PhonemeTargetWeightK37 As Single
            Get
                Return Flt("FaceFX Phonemes\P\PHWT\Phoneme Target Weight\K")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\P\PHWT\Phoneme Target Weight\K", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\P\PHWT\Phoneme Target Weight\N</summary>
        Public Property PhonemeTargetWeightN37 As Single
            Get
                Return Flt("FaceFX Phonemes\P\PHWT\Phoneme Target Weight\N")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\P\PHWT\Phoneme Target Weight\N", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\P\PHWT\Phoneme Target Weight\Oh</summary>
        Public Property PhonemeTargetWeightOh37 As Single
            Get
                Return Flt("FaceFX Phonemes\P\PHWT\Phoneme Target Weight\Oh")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\P\PHWT\Phoneme Target Weight\Oh", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\P\PHWT\Phoneme Target Weight\OohQ</summary>
        Public Property PhonemeTargetWeightOohQ37 As Single
            Get
                Return Flt("FaceFX Phonemes\P\PHWT\Phoneme Target Weight\OohQ")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\P\PHWT\Phoneme Target Weight\OohQ", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\P\PHWT\Phoneme Target Weight\R</summary>
        Public Property PhonemeTargetWeightR37 As Single
            Get
                Return Flt("FaceFX Phonemes\P\PHWT\Phoneme Target Weight\R")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\P\PHWT\Phoneme Target Weight\R", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\P\PHWT\Phoneme Target Weight\TH</summary>
        Public Property PhonemeTargetWeightTH37 As Single
            Get
                Return Flt("FaceFX Phonemes\P\PHWT\Phoneme Target Weight\TH")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\P\PHWT\Phoneme Target Weight\TH", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\P\PHWT\Phoneme Target Weight\W</summary>
        Public Property PhonemeTargetWeightW37 As Single
            Get
                Return Flt("FaceFX Phonemes\P\PHWT\Phoneme Target Weight\W")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\P\PHWT\Phoneme Target Weight\W", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\T\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Public Property PhonemeTargetWeightAahLipBigAah38 As Single
            Get
                Return Flt("FaceFX Phonemes\T\PHWT\Phoneme Target Weight\Aah / LipBigAah")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\T\PHWT\Phoneme Target Weight\Aah / LipBigAah", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\T\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Public Property PhonemeTargetWeightBigAahLipDST38 As Single
            Get
                Return Flt("FaceFX Phonemes\T\PHWT\Phoneme Target Weight\BigAah / LipDST")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\T\PHWT\Phoneme Target Weight\BigAah / LipDST", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\T\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Public Property PhonemeTargetWeightBMPLipEee38 As Single
            Get
                Return Flt("FaceFX Phonemes\T\PHWT\Phoneme Target Weight\BMP / LipEee")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\T\PHWT\Phoneme Target Weight\BMP / LipEee", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\T\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Public Property PhonemeTargetWeightChJshLipFV38 As Single
            Get
                Return Flt("FaceFX Phonemes\T\PHWT\Phoneme Target Weight\ChJsh / LipFV")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\T\PHWT\Phoneme Target Weight\ChJsh / LipFV", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\T\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Public Property PhonemeTargetWeightDSTLipK38 As Single
            Get
                Return Flt("FaceFX Phonemes\T\PHWT\Phoneme Target Weight\DST / LipK")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\T\PHWT\Phoneme Target Weight\DST / LipK", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\T\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Public Property PhonemeTargetWeightEeeLipL38 As Single
            Get
                Return Flt("FaceFX Phonemes\T\PHWT\Phoneme Target Weight\Eee / LipL")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\T\PHWT\Phoneme Target Weight\Eee / LipL", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\T\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Public Property PhonemeTargetWeightEhLipR38 As Single
            Get
                Return Flt("FaceFX Phonemes\T\PHWT\Phoneme Target Weight\Eh / LipR")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\T\PHWT\Phoneme Target Weight\Eh / LipR", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\T\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Public Property PhonemeTargetWeightFVLipTh38 As Single
            Get
                Return Flt("FaceFX Phonemes\T\PHWT\Phoneme Target Weight\FV / LipTh")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\T\PHWT\Phoneme Target Weight\FV / LipTh", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\T\PHWT\Phoneme Target Weight\I</summary>
        Public Property PhonemeTargetWeightI38 As Single
            Get
                Return Flt("FaceFX Phonemes\T\PHWT\Phoneme Target Weight\I")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\T\PHWT\Phoneme Target Weight\I", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\T\PHWT\Phoneme Target Weight\K</summary>
        Public Property PhonemeTargetWeightK38 As Single
            Get
                Return Flt("FaceFX Phonemes\T\PHWT\Phoneme Target Weight\K")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\T\PHWT\Phoneme Target Weight\K", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\T\PHWT\Phoneme Target Weight\N</summary>
        Public Property PhonemeTargetWeightN38 As Single
            Get
                Return Flt("FaceFX Phonemes\T\PHWT\Phoneme Target Weight\N")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\T\PHWT\Phoneme Target Weight\N", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\T\PHWT\Phoneme Target Weight\Oh</summary>
        Public Property PhonemeTargetWeightOh38 As Single
            Get
                Return Flt("FaceFX Phonemes\T\PHWT\Phoneme Target Weight\Oh")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\T\PHWT\Phoneme Target Weight\Oh", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\T\PHWT\Phoneme Target Weight\OohQ</summary>
        Public Property PhonemeTargetWeightOohQ38 As Single
            Get
                Return Flt("FaceFX Phonemes\T\PHWT\Phoneme Target Weight\OohQ")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\T\PHWT\Phoneme Target Weight\OohQ", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\T\PHWT\Phoneme Target Weight\R</summary>
        Public Property PhonemeTargetWeightR38 As Single
            Get
                Return Flt("FaceFX Phonemes\T\PHWT\Phoneme Target Weight\R")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\T\PHWT\Phoneme Target Weight\R", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\T\PHWT\Phoneme Target Weight\TH</summary>
        Public Property PhonemeTargetWeightTH38 As Single
            Get
                Return Flt("FaceFX Phonemes\T\PHWT\Phoneme Target Weight\TH")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\T\PHWT\Phoneme Target Weight\TH", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\T\PHWT\Phoneme Target Weight\W</summary>
        Public Property PhonemeTargetWeightW38 As Single
            Get
                Return Flt("FaceFX Phonemes\T\PHWT\Phoneme Target Weight\W")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\T\PHWT\Phoneme Target Weight\W", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\K\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Public Property PhonemeTargetWeightAahLipBigAah39 As Single
            Get
                Return Flt("FaceFX Phonemes\K\PHWT\Phoneme Target Weight\Aah / LipBigAah")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\K\PHWT\Phoneme Target Weight\Aah / LipBigAah", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\K\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Public Property PhonemeTargetWeightBigAahLipDST39 As Single
            Get
                Return Flt("FaceFX Phonemes\K\PHWT\Phoneme Target Weight\BigAah / LipDST")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\K\PHWT\Phoneme Target Weight\BigAah / LipDST", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\K\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Public Property PhonemeTargetWeightBMPLipEee39 As Single
            Get
                Return Flt("FaceFX Phonemes\K\PHWT\Phoneme Target Weight\BMP / LipEee")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\K\PHWT\Phoneme Target Weight\BMP / LipEee", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\K\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Public Property PhonemeTargetWeightChJshLipFV39 As Single
            Get
                Return Flt("FaceFX Phonemes\K\PHWT\Phoneme Target Weight\ChJsh / LipFV")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\K\PHWT\Phoneme Target Weight\ChJsh / LipFV", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\K\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Public Property PhonemeTargetWeightDSTLipK39 As Single
            Get
                Return Flt("FaceFX Phonemes\K\PHWT\Phoneme Target Weight\DST / LipK")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\K\PHWT\Phoneme Target Weight\DST / LipK", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\K\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Public Property PhonemeTargetWeightEeeLipL39 As Single
            Get
                Return Flt("FaceFX Phonemes\K\PHWT\Phoneme Target Weight\Eee / LipL")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\K\PHWT\Phoneme Target Weight\Eee / LipL", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\K\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Public Property PhonemeTargetWeightEhLipR39 As Single
            Get
                Return Flt("FaceFX Phonemes\K\PHWT\Phoneme Target Weight\Eh / LipR")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\K\PHWT\Phoneme Target Weight\Eh / LipR", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\K\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Public Property PhonemeTargetWeightFVLipTh39 As Single
            Get
                Return Flt("FaceFX Phonemes\K\PHWT\Phoneme Target Weight\FV / LipTh")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\K\PHWT\Phoneme Target Weight\FV / LipTh", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\K\PHWT\Phoneme Target Weight\I</summary>
        Public Property PhonemeTargetWeightI39 As Single
            Get
                Return Flt("FaceFX Phonemes\K\PHWT\Phoneme Target Weight\I")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\K\PHWT\Phoneme Target Weight\I", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\K\PHWT\Phoneme Target Weight\K</summary>
        Public Property PhonemeTargetWeightK39 As Single
            Get
                Return Flt("FaceFX Phonemes\K\PHWT\Phoneme Target Weight\K")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\K\PHWT\Phoneme Target Weight\K", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\K\PHWT\Phoneme Target Weight\N</summary>
        Public Property PhonemeTargetWeightN39 As Single
            Get
                Return Flt("FaceFX Phonemes\K\PHWT\Phoneme Target Weight\N")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\K\PHWT\Phoneme Target Weight\N", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\K\PHWT\Phoneme Target Weight\Oh</summary>
        Public Property PhonemeTargetWeightOh39 As Single
            Get
                Return Flt("FaceFX Phonemes\K\PHWT\Phoneme Target Weight\Oh")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\K\PHWT\Phoneme Target Weight\Oh", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\K\PHWT\Phoneme Target Weight\OohQ</summary>
        Public Property PhonemeTargetWeightOohQ39 As Single
            Get
                Return Flt("FaceFX Phonemes\K\PHWT\Phoneme Target Weight\OohQ")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\K\PHWT\Phoneme Target Weight\OohQ", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\K\PHWT\Phoneme Target Weight\R</summary>
        Public Property PhonemeTargetWeightR39 As Single
            Get
                Return Flt("FaceFX Phonemes\K\PHWT\Phoneme Target Weight\R")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\K\PHWT\Phoneme Target Weight\R", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\K\PHWT\Phoneme Target Weight\TH</summary>
        Public Property PhonemeTargetWeightTH39 As Single
            Get
                Return Flt("FaceFX Phonemes\K\PHWT\Phoneme Target Weight\TH")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\K\PHWT\Phoneme Target Weight\TH", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\K\PHWT\Phoneme Target Weight\W</summary>
        Public Property PhonemeTargetWeightW39 As Single
            Get
                Return Flt("FaceFX Phonemes\K\PHWT\Phoneme Target Weight\W")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\K\PHWT\Phoneme Target Weight\W", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Public Property PhonemeTargetWeightAahLipBigAah40 As Single
            Get
                Return Flt("FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\Aah / LipBigAah")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\Aah / LipBigAah", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Public Property PhonemeTargetWeightBigAahLipDST40 As Single
            Get
                Return Flt("FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\BigAah / LipDST")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\BigAah / LipDST", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Public Property PhonemeTargetWeightBMPLipEee40 As Single
            Get
                Return Flt("FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\BMP / LipEee")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\BMP / LipEee", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Public Property PhonemeTargetWeightChJshLipFV40 As Single
            Get
                Return Flt("FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\ChJsh / LipFV")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\ChJsh / LipFV", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Public Property PhonemeTargetWeightDSTLipK40 As Single
            Get
                Return Flt("FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\DST / LipK")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\DST / LipK", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Public Property PhonemeTargetWeightEeeLipL40 As Single
            Get
                Return Flt("FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\Eee / LipL")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\Eee / LipL", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Public Property PhonemeTargetWeightEhLipR40 As Single
            Get
                Return Flt("FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\Eh / LipR")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\Eh / LipR", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Public Property PhonemeTargetWeightFVLipTh40 As Single
            Get
                Return Flt("FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\FV / LipTh")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\FV / LipTh", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\I</summary>
        Public Property PhonemeTargetWeightI40 As Single
            Get
                Return Flt("FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\I")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\I", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\K</summary>
        Public Property PhonemeTargetWeightK40 As Single
            Get
                Return Flt("FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\K")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\K", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\N</summary>
        Public Property PhonemeTargetWeightN40 As Single
            Get
                Return Flt("FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\N")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\N", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\Oh</summary>
        Public Property PhonemeTargetWeightOh40 As Single
            Get
                Return Flt("FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\Oh")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\Oh", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\OohQ</summary>
        Public Property PhonemeTargetWeightOohQ40 As Single
            Get
                Return Flt("FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\OohQ")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\OohQ", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\R</summary>
        Public Property PhonemeTargetWeightR40 As Single
            Get
                Return Flt("FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\R")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\R", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\TH</summary>
        Public Property PhonemeTargetWeightTH40 As Single
            Get
                Return Flt("FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\TH")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\TH", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\W</summary>
        Public Property PhonemeTargetWeightW40 As Single
            Get
                Return Flt("FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\W")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\W", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Public Property PhonemeTargetWeightAahLipBigAah41 As Single
            Get
                Return Flt("FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\Aah / LipBigAah")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\Aah / LipBigAah", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Public Property PhonemeTargetWeightBigAahLipDST41 As Single
            Get
                Return Flt("FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\BigAah / LipDST")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\BigAah / LipDST", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Public Property PhonemeTargetWeightBMPLipEee41 As Single
            Get
                Return Flt("FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\BMP / LipEee")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\BMP / LipEee", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Public Property PhonemeTargetWeightChJshLipFV41 As Single
            Get
                Return Flt("FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\ChJsh / LipFV")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\ChJsh / LipFV", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Public Property PhonemeTargetWeightDSTLipK41 As Single
            Get
                Return Flt("FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\DST / LipK")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\DST / LipK", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Public Property PhonemeTargetWeightEeeLipL41 As Single
            Get
                Return Flt("FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\Eee / LipL")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\Eee / LipL", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Public Property PhonemeTargetWeightEhLipR41 As Single
            Get
                Return Flt("FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\Eh / LipR")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\Eh / LipR", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Public Property PhonemeTargetWeightFVLipTh41 As Single
            Get
                Return Flt("FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\FV / LipTh")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\FV / LipTh", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\I</summary>
        Public Property PhonemeTargetWeightI41 As Single
            Get
                Return Flt("FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\I")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\I", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\K</summary>
        Public Property PhonemeTargetWeightK41 As Single
            Get
                Return Flt("FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\K")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\K", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\N</summary>
        Public Property PhonemeTargetWeightN41 As Single
            Get
                Return Flt("FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\N")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\N", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\Oh</summary>
        Public Property PhonemeTargetWeightOh41 As Single
            Get
                Return Flt("FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\Oh")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\Oh", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\OohQ</summary>
        Public Property PhonemeTargetWeightOohQ41 As Single
            Get
                Return Flt("FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\OohQ")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\OohQ", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\R</summary>
        Public Property PhonemeTargetWeightR41 As Single
            Get
                Return Flt("FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\R")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\R", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\TH</summary>
        Public Property PhonemeTargetWeightTH41 As Single
            Get
                Return Flt("FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\TH")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\TH", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\W</summary>
        Public Property PhonemeTargetWeightW41 As Single
            Get
                Return Flt("FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\W")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\W", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Public Property PhonemeTargetWeightAahLipBigAah42 As Single
            Get
                Return Flt("FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\Aah / LipBigAah")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\Aah / LipBigAah", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Public Property PhonemeTargetWeightBigAahLipDST42 As Single
            Get
                Return Flt("FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\BigAah / LipDST")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\BigAah / LipDST", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Public Property PhonemeTargetWeightBMPLipEee42 As Single
            Get
                Return Flt("FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\BMP / LipEee")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\BMP / LipEee", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Public Property PhonemeTargetWeightChJshLipFV42 As Single
            Get
                Return Flt("FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\ChJsh / LipFV")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\ChJsh / LipFV", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Public Property PhonemeTargetWeightDSTLipK42 As Single
            Get
                Return Flt("FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\DST / LipK")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\DST / LipK", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Public Property PhonemeTargetWeightEeeLipL42 As Single
            Get
                Return Flt("FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\Eee / LipL")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\Eee / LipL", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Public Property PhonemeTargetWeightEhLipR42 As Single
            Get
                Return Flt("FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\Eh / LipR")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\Eh / LipR", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Public Property PhonemeTargetWeightFVLipTh42 As Single
            Get
                Return Flt("FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\FV / LipTh")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\FV / LipTh", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\I</summary>
        Public Property PhonemeTargetWeightI42 As Single
            Get
                Return Flt("FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\I")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\I", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\K</summary>
        Public Property PhonemeTargetWeightK42 As Single
            Get
                Return Flt("FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\K")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\K", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\N</summary>
        Public Property PhonemeTargetWeightN42 As Single
            Get
                Return Flt("FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\N")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\N", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\Oh</summary>
        Public Property PhonemeTargetWeightOh42 As Single
            Get
                Return Flt("FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\Oh")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\Oh", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\OohQ</summary>
        Public Property PhonemeTargetWeightOohQ42 As Single
            Get
                Return Flt("FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\OohQ")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\OohQ", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\R</summary>
        Public Property PhonemeTargetWeightR42 As Single
            Get
                Return Flt("FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\R")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\R", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\TH</summary>
        Public Property PhonemeTargetWeightTH42 As Single
            Get
                Return Flt("FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\TH")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\TH", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\W</summary>
        Public Property PhonemeTargetWeightW42 As Single
            Get
                Return Flt("FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\W")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\W", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Public Property PhonemeTargetWeightAahLipBigAah43 As Single
            Get
                Return Flt("FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\Aah / LipBigAah")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\Aah / LipBigAah", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Public Property PhonemeTargetWeightBigAahLipDST43 As Single
            Get
                Return Flt("FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\BigAah / LipDST")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\BigAah / LipDST", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Public Property PhonemeTargetWeightBMPLipEee43 As Single
            Get
                Return Flt("FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\BMP / LipEee")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\BMP / LipEee", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Public Property PhonemeTargetWeightChJshLipFV43 As Single
            Get
                Return Flt("FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\ChJsh / LipFV")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\ChJsh / LipFV", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Public Property PhonemeTargetWeightDSTLipK43 As Single
            Get
                Return Flt("FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\DST / LipK")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\DST / LipK", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Public Property PhonemeTargetWeightEeeLipL43 As Single
            Get
                Return Flt("FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\Eee / LipL")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\Eee / LipL", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Public Property PhonemeTargetWeightEhLipR43 As Single
            Get
                Return Flt("FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\Eh / LipR")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\Eh / LipR", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Public Property PhonemeTargetWeightFVLipTh43 As Single
            Get
                Return Flt("FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\FV / LipTh")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\FV / LipTh", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\I</summary>
        Public Property PhonemeTargetWeightI43 As Single
            Get
                Return Flt("FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\I")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\I", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\K</summary>
        Public Property PhonemeTargetWeightK43 As Single
            Get
                Return Flt("FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\K")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\K", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\N</summary>
        Public Property PhonemeTargetWeightN43 As Single
            Get
                Return Flt("FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\N")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\N", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\Oh</summary>
        Public Property PhonemeTargetWeightOh43 As Single
            Get
                Return Flt("FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\Oh")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\Oh", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\OohQ</summary>
        Public Property PhonemeTargetWeightOohQ43 As Single
            Get
                Return Flt("FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\OohQ")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\OohQ", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\R</summary>
        Public Property PhonemeTargetWeightR43 As Single
            Get
                Return Flt("FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\R")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\R", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\TH</summary>
        Public Property PhonemeTargetWeightTH43 As Single
            Get
                Return Flt("FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\TH")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\TH", value)
            End Set
        End Property

        ''' <summary>FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\W</summary>
        Public Property PhonemeTargetWeightW43 As Single
            Get
                Return Flt("FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\W")
            End Get
            Set(value As Single)
                Escribir("FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\W", value)
            End Set
        End Property

        ''' <summary>WKMV\Base Movement Default - Walk  -&gt;  MOVT / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property BaseMovementDefaultWalk As UInteger
            Get
                Return Referencia("WKMV\Base Movement Default - Walk")
            End Get
            Set(value As UInteger)
                PonerReferencia("WKMV\Base Movement Default - Walk", value)
            End Set
        End Property

        ''' <summary>RNMV\Base Movement Default - Run  -&gt;  MOVT / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property BaseMovementDefaultRun As UInteger
            Get
                Return Referencia("RNMV\Base Movement Default - Run")
            End Get
            Set(value As UInteger)
                PonerReferencia("RNMV\Base Movement Default - Run", value)
            End Set
        End Property

        ''' <summary>SWMV\Base Movement Default - Swim  -&gt;  MOVT / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property BaseMovementDefaultSwim As UInteger
            Get
                Return Referencia("SWMV\Base Movement Default - Swim")
            End Get
            Set(value As UInteger)
                PonerReferencia("SWMV\Base Movement Default - Swim", value)
            End Set
        End Property

        ''' <summary>FLMV\Base Movement Default - Fly  -&gt;  MOVT / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property BaseMovementDefaultFly As UInteger
            Get
                Return Referencia("FLMV\Base Movement Default - Fly")
            End Get
            Set(value As UInteger)
                PonerReferencia("FLMV\Base Movement Default - Fly", value)
            End Set
        End Property

        ''' <summary>SNMV\Base Movement Default - Sneak  -&gt;  MOVT / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property BaseMovementDefaultSneak As UInteger
            Get
                Return Referencia("SNMV\Base Movement Default - Sneak")
            End Get
            Set(value As UInteger)
                PonerReferencia("SNMV\Base Movement Default - Sneak", value)
            End Set
        End Property

        ''' <summary>SPMV\Base Movement Default - Sprint  -&gt;  MOVT / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property BaseMovementDefaultSprint As UInteger
            Get
                Return Referencia("SPMV\Base Movement Default - Sprint")
            End Get
            Set(value As UInteger)
                PonerReferencia("SPMV\Base Movement Default - Sprint", value)
            End Set
        End Property

        ''' <summary>Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags</summary>
        Public Property NoseVariantsNoseMorphFlags As UInteger
            Get
                Return CUInt(Entero("Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags"))
            End Get
            Set(value As UInteger)
                Escribir("Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", CLng(value))
            End Set
        End Property

        ''' <summary>Bit 0 de Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags: NoseType0</summary>
        Public Property NoseVariantsNoseMorphFlagsNoseType0 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 0)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 0, value)
            End Set
        End Property
        ''' <summary>Bit 1 de Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags: NoseType1</summary>
        Public Property NoseVariantsNoseMorphFlagsNoseType1 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 1)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 1, value)
            End Set
        End Property
        ''' <summary>Bit 2 de Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags: NoseType2</summary>
        Public Property NoseVariantsNoseMorphFlagsNoseType2 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 2)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 2, value)
            End Set
        End Property
        ''' <summary>Bit 3 de Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags: NoseType3</summary>
        Public Property NoseVariantsNoseMorphFlagsNoseType3 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 3)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 3, value)
            End Set
        End Property
        ''' <summary>Bit 4 de Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags: NoseType4</summary>
        Public Property NoseVariantsNoseMorphFlagsNoseType4 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 4)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 4, value)
            End Set
        End Property
        ''' <summary>Bit 5 de Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags: NoseType5</summary>
        Public Property NoseVariantsNoseMorphFlagsNoseType5 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 5)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 5, value)
            End Set
        End Property
        ''' <summary>Bit 6 de Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags: NoseType6</summary>
        Public Property NoseVariantsNoseMorphFlagsNoseType6 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 6)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 6, value)
            End Set
        End Property
        ''' <summary>Bit 7 de Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags: NoseType7</summary>
        Public Property NoseVariantsNoseMorphFlagsNoseType7 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 7)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 7, value)
            End Set
        End Property
        ''' <summary>Bit 8 de Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags: NoseType8</summary>
        Public Property NoseVariantsNoseMorphFlagsNoseType8 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 8)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 8, value)
            End Set
        End Property
        ''' <summary>Bit 9 de Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags: NoseType9</summary>
        Public Property NoseVariantsNoseMorphFlagsNoseType9 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 9)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 9, value)
            End Set
        End Property
        ''' <summary>Bit 10 de Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags: NoseType10</summary>
        Public Property NoseVariantsNoseMorphFlagsNoseType10 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 10)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 10, value)
            End Set
        End Property
        ''' <summary>Bit 11 de Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags: NoseType11</summary>
        Public Property NoseVariantsNoseMorphFlagsNoseType11 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 11)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 11, value)
            End Set
        End Property
        ''' <summary>Bit 12 de Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags: NoseType12</summary>
        Public Property NoseVariantsNoseMorphFlagsNoseType12 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 12)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 12, value)
            End Set
        End Property
        ''' <summary>Bit 13 de Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags: NoseType13</summary>
        Public Property NoseVariantsNoseMorphFlagsNoseType13 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 13)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 13, value)
            End Set
        End Property
        ''' <summary>Bit 14 de Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags: NoseType14</summary>
        Public Property NoseVariantsNoseMorphFlagsNoseType14 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 14)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 14, value)
            End Set
        End Property
        ''' <summary>Bit 15 de Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags: NoseType15</summary>
        Public Property NoseVariantsNoseMorphFlagsNoseType15 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 15)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 15, value)
            End Set
        End Property
        ''' <summary>Bit 16 de Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags: NoseType16</summary>
        Public Property NoseVariantsNoseMorphFlagsNoseType16 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 16)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 16, value)
            End Set
        End Property
        ''' <summary>Bit 17 de Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags: NoseType17</summary>
        Public Property NoseVariantsNoseMorphFlagsNoseType17 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 17)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 17, value)
            End Set
        End Property
        ''' <summary>Bit 18 de Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags: NoseType18</summary>
        Public Property NoseVariantsNoseMorphFlagsNoseType18 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 18)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 18, value)
            End Set
        End Property
        ''' <summary>Bit 19 de Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags: NoseType19</summary>
        Public Property NoseVariantsNoseMorphFlagsNoseType19 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 19)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 19, value)
            End Set
        End Property
        ''' <summary>Bit 20 de Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags: NoseType20</summary>
        Public Property NoseVariantsNoseMorphFlagsNoseType20 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 20)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 20, value)
            End Set
        End Property
        ''' <summary>Bit 21 de Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags: NoseType21</summary>
        Public Property NoseVariantsNoseMorphFlagsNoseType21 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 21)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 21, value)
            End Set
        End Property
        ''' <summary>Bit 22 de Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags: NoseType22</summary>
        Public Property NoseVariantsNoseMorphFlagsNoseType22 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 22)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 22, value)
            End Set
        End Property
        ''' <summary>Bit 23 de Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags: NoseType23</summary>
        Public Property NoseVariantsNoseMorphFlagsNoseType23 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 23)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 23, value)
            End Set
        End Property
        ''' <summary>Bit 24 de Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags: NoseType24</summary>
        Public Property NoseVariantsNoseMorphFlagsNoseType24 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 24)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 24, value)
            End Set
        End Property
        ''' <summary>Bit 25 de Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags: NoseType25</summary>
        Public Property NoseVariantsNoseMorphFlagsNoseType25 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 25)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 25, value)
            End Set
        End Property
        ''' <summary>Bit 26 de Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags: NoseType26</summary>
        Public Property NoseVariantsNoseMorphFlagsNoseType26 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 26)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 26, value)
            End Set
        End Property
        ''' <summary>Bit 27 de Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags: NoseType27</summary>
        Public Property NoseVariantsNoseMorphFlagsNoseType27 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 27)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 27, value)
            End Set
        End Property
        ''' <summary>Bit 28 de Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags: NoseType28</summary>
        Public Property NoseVariantsNoseMorphFlagsNoseType28 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 28)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 28, value)
            End Set
        End Property
        ''' <summary>Bit 29 de Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags: NoseType29</summary>
        Public Property NoseVariantsNoseMorphFlagsNoseType29 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 29)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 29, value)
            End Set
        End Property
        ''' <summary>Bit 30 de Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags: NoseType30</summary>
        Public Property NoseVariantsNoseMorphFlagsNoseType30 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 30)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 30, value)
            End Set
        End Property
        ''' <summary>Bit 31 de Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags: NoseType31</summary>
        Public Property NoseVariantsNoseMorphFlagsNoseType31 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 31)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 31, value)
            End Set
        End Property

        ''' <summary>Head Data\Male Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags</summary>
        Public Property BrowVariantsBrowMorphFlags As UInteger
            Get
                Return CUInt(Entero("Head Data\Male Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags"))
            End Get
            Set(value As UInteger)
                Escribir("Head Data\Male Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags", CLng(value))
            End Set
        End Property

        ''' <summary>Bit 0 de Head Data\Male Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags: BrowType0</summary>
        Public Property BrowVariantsBrowMorphFlagsBrowType0 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags", 0)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags", 0, value)
            End Set
        End Property
        ''' <summary>Bit 1 de Head Data\Male Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags: BrowType1</summary>
        Public Property BrowVariantsBrowMorphFlagsBrowType1 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags", 1)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags", 1, value)
            End Set
        End Property
        ''' <summary>Bit 2 de Head Data\Male Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags: BrowType2</summary>
        Public Property BrowVariantsBrowMorphFlagsBrowType2 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags", 2)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags", 2, value)
            End Set
        End Property
        ''' <summary>Bit 3 de Head Data\Male Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags: BrowType3</summary>
        Public Property BrowVariantsBrowMorphFlagsBrowType3 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags", 3)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags", 3, value)
            End Set
        End Property
        ''' <summary>Bit 4 de Head Data\Male Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags: BrowType4</summary>
        Public Property BrowVariantsBrowMorphFlagsBrowType4 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags", 4)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags", 4, value)
            End Set
        End Property
        ''' <summary>Bit 5 de Head Data\Male Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags: BrowType5</summary>
        Public Property BrowVariantsBrowMorphFlagsBrowType5 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags", 5)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags", 5, value)
            End Set
        End Property
        ''' <summary>Bit 6 de Head Data\Male Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags: BrowType6</summary>
        Public Property BrowVariantsBrowMorphFlagsBrowType6 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags", 6)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags", 6, value)
            End Set
        End Property
        ''' <summary>Bit 7 de Head Data\Male Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags: BrowType7</summary>
        Public Property BrowVariantsBrowMorphFlagsBrowType7 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags", 7)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags", 7, value)
            End Set
        End Property
        ''' <summary>Bit 8 de Head Data\Male Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags: BrowType8</summary>
        Public Property BrowVariantsBrowMorphFlagsBrowType8 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags", 8)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags", 8, value)
            End Set
        End Property
        ''' <summary>Bit 9 de Head Data\Male Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags: BrowType9</summary>
        Public Property BrowVariantsBrowMorphFlagsBrowType9 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags", 9)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags", 9, value)
            End Set
        End Property
        ''' <summary>Bit 10 de Head Data\Male Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags: BrowType10</summary>
        Public Property BrowVariantsBrowMorphFlagsBrowType10 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags", 10)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags", 10, value)
            End Set
        End Property
        ''' <summary>Bit 11 de Head Data\Male Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags: BrowType11</summary>
        Public Property BrowVariantsBrowMorphFlagsBrowType11 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags", 11)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags", 11, value)
            End Set
        End Property
        ''' <summary>Bit 12 de Head Data\Male Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags: BrowType12</summary>
        Public Property BrowVariantsBrowMorphFlagsBrowType12 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags", 12)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags", 12, value)
            End Set
        End Property
        ''' <summary>Bit 13 de Head Data\Male Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags: BrowType13</summary>
        Public Property BrowVariantsBrowMorphFlagsBrowType13 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags", 13)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags", 13, value)
            End Set
        End Property
        ''' <summary>Bit 14 de Head Data\Male Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags: BrowType14</summary>
        Public Property BrowVariantsBrowMorphFlagsBrowType14 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags", 14)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags", 14, value)
            End Set
        End Property
        ''' <summary>Bit 15 de Head Data\Male Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags: BrowType15</summary>
        Public Property BrowVariantsBrowMorphFlagsBrowType15 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags", 15)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags", 15, value)
            End Set
        End Property
        ''' <summary>Bit 16 de Head Data\Male Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags: BrowType16</summary>
        Public Property BrowVariantsBrowMorphFlagsBrowType16 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags", 16)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags", 16, value)
            End Set
        End Property
        ''' <summary>Bit 17 de Head Data\Male Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags: BrowType17</summary>
        Public Property BrowVariantsBrowMorphFlagsBrowType17 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags", 17)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags", 17, value)
            End Set
        End Property
        ''' <summary>Bit 18 de Head Data\Male Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags: BrowType18</summary>
        Public Property BrowVariantsBrowMorphFlagsBrowType18 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags", 18)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags", 18, value)
            End Set
        End Property
        ''' <summary>Bit 19 de Head Data\Male Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags: BrowType19</summary>
        Public Property BrowVariantsBrowMorphFlagsBrowType19 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags", 19)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags", 19, value)
            End Set
        End Property
        ''' <summary>Bit 20 de Head Data\Male Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags: BrowType20</summary>
        Public Property BrowVariantsBrowMorphFlagsBrowType20 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags", 20)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags", 20, value)
            End Set
        End Property

        ''' <summary>Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1</summary>
        Public Property EyeVariantsEyeMorphFlags1 As UInteger
            Get
                Return CUInt(Entero("Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1"))
            End Get
            Set(value As UInteger)
                Escribir("Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", CLng(value))
            End Set
        End Property

        ''' <summary>Bit 0 de Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1: EyesType0</summary>
        Public Property EyeVariantsEyeMorphFlags1EyesType0 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 0)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 0, value)
            End Set
        End Property
        ''' <summary>Bit 1 de Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1: EyesType1</summary>
        Public Property EyeVariantsEyeMorphFlags1EyesType1 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 1)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 1, value)
            End Set
        End Property
        ''' <summary>Bit 2 de Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1: EyesType2</summary>
        Public Property EyeVariantsEyeMorphFlags1EyesType2 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 2)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 2, value)
            End Set
        End Property
        ''' <summary>Bit 3 de Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1: EyesType3</summary>
        Public Property EyeVariantsEyeMorphFlags1EyesType3 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 3)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 3, value)
            End Set
        End Property
        ''' <summary>Bit 4 de Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1: EyesType4</summary>
        Public Property EyeVariantsEyeMorphFlags1EyesType4 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 4)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 4, value)
            End Set
        End Property
        ''' <summary>Bit 5 de Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1: EyesType5</summary>
        Public Property EyeVariantsEyeMorphFlags1EyesType5 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 5)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 5, value)
            End Set
        End Property
        ''' <summary>Bit 6 de Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1: EyesType6</summary>
        Public Property EyeVariantsEyeMorphFlags1EyesType6 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 6)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 6, value)
            End Set
        End Property
        ''' <summary>Bit 7 de Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1: EyesType7</summary>
        Public Property EyeVariantsEyeMorphFlags1EyesType7 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 7)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 7, value)
            End Set
        End Property
        ''' <summary>Bit 8 de Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1: EyesType8</summary>
        Public Property EyeVariantsEyeMorphFlags1EyesType8 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 8)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 8, value)
            End Set
        End Property
        ''' <summary>Bit 9 de Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1: EyesType9</summary>
        Public Property EyeVariantsEyeMorphFlags1EyesType9 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 9)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 9, value)
            End Set
        End Property
        ''' <summary>Bit 10 de Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1: EyesType10</summary>
        Public Property EyeVariantsEyeMorphFlags1EyesType10 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 10)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 10, value)
            End Set
        End Property
        ''' <summary>Bit 11 de Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1: EyesType11</summary>
        Public Property EyeVariantsEyeMorphFlags1EyesType11 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 11)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 11, value)
            End Set
        End Property
        ''' <summary>Bit 12 de Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1: EyesType12</summary>
        Public Property EyeVariantsEyeMorphFlags1EyesType12 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 12)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 12, value)
            End Set
        End Property
        ''' <summary>Bit 13 de Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1: EyesType13</summary>
        Public Property EyeVariantsEyeMorphFlags1EyesType13 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 13)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 13, value)
            End Set
        End Property
        ''' <summary>Bit 14 de Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1: EyesType14</summary>
        Public Property EyeVariantsEyeMorphFlags1EyesType14 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 14)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 14, value)
            End Set
        End Property
        ''' <summary>Bit 15 de Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1: EyesType15</summary>
        Public Property EyeVariantsEyeMorphFlags1EyesType15 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 15)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 15, value)
            End Set
        End Property
        ''' <summary>Bit 16 de Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1: EyesType16</summary>
        Public Property EyeVariantsEyeMorphFlags1EyesType16 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 16)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 16, value)
            End Set
        End Property
        ''' <summary>Bit 17 de Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1: EyesType17</summary>
        Public Property EyeVariantsEyeMorphFlags1EyesType17 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 17)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 17, value)
            End Set
        End Property
        ''' <summary>Bit 18 de Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1: EyesType18</summary>
        Public Property EyeVariantsEyeMorphFlags1EyesType18 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 18)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 18, value)
            End Set
        End Property
        ''' <summary>Bit 19 de Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1: EyesType19</summary>
        Public Property EyeVariantsEyeMorphFlags1EyesType19 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 19)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 19, value)
            End Set
        End Property
        ''' <summary>Bit 20 de Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1: EyesType20</summary>
        Public Property EyeVariantsEyeMorphFlags1EyesType20 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 20)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 20, value)
            End Set
        End Property
        ''' <summary>Bit 21 de Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1: EyesType21</summary>
        Public Property EyeVariantsEyeMorphFlags1EyesType21 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 21)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 21, value)
            End Set
        End Property
        ''' <summary>Bit 22 de Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1: EyesType22</summary>
        Public Property EyeVariantsEyeMorphFlags1EyesType22 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 22)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 22, value)
            End Set
        End Property
        ''' <summary>Bit 23 de Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1: EyesType23</summary>
        Public Property EyeVariantsEyeMorphFlags1EyesType23 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 23)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 23, value)
            End Set
        End Property
        ''' <summary>Bit 24 de Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1: EyesType24</summary>
        Public Property EyeVariantsEyeMorphFlags1EyesType24 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 24)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 24, value)
            End Set
        End Property
        ''' <summary>Bit 25 de Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1: EyesType25</summary>
        Public Property EyeVariantsEyeMorphFlags1EyesType25 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 25)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 25, value)
            End Set
        End Property
        ''' <summary>Bit 26 de Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1: EyesType26</summary>
        Public Property EyeVariantsEyeMorphFlags1EyesType26 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 26)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 26, value)
            End Set
        End Property
        ''' <summary>Bit 27 de Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1: EyesType27</summary>
        Public Property EyeVariantsEyeMorphFlags1EyesType27 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 27)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 27, value)
            End Set
        End Property
        ''' <summary>Bit 28 de Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1: EyesType28</summary>
        Public Property EyeVariantsEyeMorphFlags1EyesType28 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 28)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 28, value)
            End Set
        End Property
        ''' <summary>Bit 29 de Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1: EyesType29</summary>
        Public Property EyeVariantsEyeMorphFlags1EyesType29 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 29)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 29, value)
            End Set
        End Property
        ''' <summary>Bit 30 de Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1: EyesType30</summary>
        Public Property EyeVariantsEyeMorphFlags1EyesType30 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 30)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 30, value)
            End Set
        End Property
        ''' <summary>Bit 31 de Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1: EyesType31</summary>
        Public Property EyeVariantsEyeMorphFlags1EyesType31 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 31)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 31, value)
            End Set
        End Property

        ''' <summary>Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 2</summary>
        Public Property EyeVariantsEyeMorphFlags2 As Byte
            Get
                Return CByte(Entero("Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 2"))
            End Get
            Set(value As Byte)
                Escribir("Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 2", CLng(value))
            End Set
        End Property

        ''' <summary>Bit 0 de Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 2: EyesType32</summary>
        Public Property EyeVariantsEyeMorphFlags2EyesType32 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 2", 0)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 2", 0, value)
            End Set
        End Property
        ''' <summary>Bit 1 de Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 2: EyesType33</summary>
        Public Property EyeVariantsEyeMorphFlags2EyesType33 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 2", 1)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 2", 1, value)
            End Set
        End Property
        ''' <summary>Bit 2 de Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 2: EyesType34</summary>
        Public Property EyeVariantsEyeMorphFlags2EyesType34 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 2", 2)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 2", 2, value)
            End Set
        End Property
        ''' <summary>Bit 3 de Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 2: EyesType35</summary>
        Public Property EyeVariantsEyeMorphFlags2EyesType35 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 2", 3)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 2", 3, value)
            End Set
        End Property
        ''' <summary>Bit 4 de Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 2: EyesType36</summary>
        Public Property EyeVariantsEyeMorphFlags2EyesType36 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 2", 4)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 2", 4, value)
            End Set
        End Property
        ''' <summary>Bit 5 de Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 2: EyesType37</summary>
        Public Property EyeVariantsEyeMorphFlags2EyesType37 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 2", 5)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 2", 5, value)
            End Set
        End Property
        ''' <summary>Bit 6 de Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 2: EyesType38</summary>
        Public Property EyeVariantsEyeMorphFlags2EyesType38 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 2", 6)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 2", 6, value)
            End Set
        End Property

        ''' <summary>Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags</summary>
        Public Property LipVariantsLipMorphFlags As UInteger
            Get
                Return CUInt(Entero("Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags"))
            End Get
            Set(value As UInteger)
                Escribir("Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", CLng(value))
            End Set
        End Property

        ''' <summary>Bit 0 de Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags: LipType0</summary>
        Public Property LipVariantsLipMorphFlagsLipType0 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 0)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 0, value)
            End Set
        End Property
        ''' <summary>Bit 1 de Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags: LipType1</summary>
        Public Property LipVariantsLipMorphFlagsLipType1 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 1)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 1, value)
            End Set
        End Property
        ''' <summary>Bit 2 de Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags: LipType2</summary>
        Public Property LipVariantsLipMorphFlagsLipType2 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 2)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 2, value)
            End Set
        End Property
        ''' <summary>Bit 3 de Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags: LipType3</summary>
        Public Property LipVariantsLipMorphFlagsLipType3 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 3)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 3, value)
            End Set
        End Property
        ''' <summary>Bit 4 de Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags: LipType4</summary>
        Public Property LipVariantsLipMorphFlagsLipType4 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 4)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 4, value)
            End Set
        End Property
        ''' <summary>Bit 5 de Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags: LipType5</summary>
        Public Property LipVariantsLipMorphFlagsLipType5 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 5)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 5, value)
            End Set
        End Property
        ''' <summary>Bit 6 de Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags: LipType6</summary>
        Public Property LipVariantsLipMorphFlagsLipType6 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 6)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 6, value)
            End Set
        End Property
        ''' <summary>Bit 7 de Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags: LipType7</summary>
        Public Property LipVariantsLipMorphFlagsLipType7 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 7)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 7, value)
            End Set
        End Property
        ''' <summary>Bit 8 de Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags: LipType8</summary>
        Public Property LipVariantsLipMorphFlagsLipType8 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 8)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 8, value)
            End Set
        End Property
        ''' <summary>Bit 9 de Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags: LipType9</summary>
        Public Property LipVariantsLipMorphFlagsLipType9 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 9)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 9, value)
            End Set
        End Property
        ''' <summary>Bit 10 de Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags: LipType10</summary>
        Public Property LipVariantsLipMorphFlagsLipType10 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 10)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 10, value)
            End Set
        End Property
        ''' <summary>Bit 11 de Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags: LipType11</summary>
        Public Property LipVariantsLipMorphFlagsLipType11 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 11)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 11, value)
            End Set
        End Property
        ''' <summary>Bit 12 de Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags: LipType12</summary>
        Public Property LipVariantsLipMorphFlagsLipType12 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 12)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 12, value)
            End Set
        End Property
        ''' <summary>Bit 13 de Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags: LipType13</summary>
        Public Property LipVariantsLipMorphFlagsLipType13 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 13)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 13, value)
            End Set
        End Property
        ''' <summary>Bit 14 de Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags: LipType14</summary>
        Public Property LipVariantsLipMorphFlagsLipType14 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 14)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 14, value)
            End Set
        End Property
        ''' <summary>Bit 15 de Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags: LipType15</summary>
        Public Property LipVariantsLipMorphFlagsLipType15 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 15)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 15, value)
            End Set
        End Property
        ''' <summary>Bit 16 de Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags: LipType16</summary>
        Public Property LipVariantsLipMorphFlagsLipType16 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 16)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 16, value)
            End Set
        End Property
        ''' <summary>Bit 17 de Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags: LipType17</summary>
        Public Property LipVariantsLipMorphFlagsLipType17 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 17)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 17, value)
            End Set
        End Property
        ''' <summary>Bit 18 de Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags: LipType18</summary>
        Public Property LipVariantsLipMorphFlagsLipType18 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 18)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 18, value)
            End Set
        End Property
        ''' <summary>Bit 19 de Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags: LipType19</summary>
        Public Property LipVariantsLipMorphFlagsLipType19 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 19)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 19, value)
            End Set
        End Property
        ''' <summary>Bit 20 de Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags: LipType20</summary>
        Public Property LipVariantsLipMorphFlagsLipType20 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 20)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 20, value)
            End Set
        End Property
        ''' <summary>Bit 21 de Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags: LipType21</summary>
        Public Property LipVariantsLipMorphFlagsLipType21 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 21)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 21, value)
            End Set
        End Property
        ''' <summary>Bit 22 de Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags: LipType22</summary>
        Public Property LipVariantsLipMorphFlagsLipType22 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 22)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 22, value)
            End Set
        End Property
        ''' <summary>Bit 23 de Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags: LipType23</summary>
        Public Property LipVariantsLipMorphFlagsLipType23 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 23)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 23, value)
            End Set
        End Property
        ''' <summary>Bit 24 de Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags: LipType24</summary>
        Public Property LipVariantsLipMorphFlagsLipType24 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 24)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 24, value)
            End Set
        End Property
        ''' <summary>Bit 25 de Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags: LipType25</summary>
        Public Property LipVariantsLipMorphFlagsLipType25 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 25)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 25, value)
            End Set
        End Property
        ''' <summary>Bit 26 de Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags: LipType26</summary>
        Public Property LipVariantsLipMorphFlagsLipType26 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 26)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 26, value)
            End Set
        End Property
        ''' <summary>Bit 27 de Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags: LipType27</summary>
        Public Property LipVariantsLipMorphFlagsLipType27 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 27)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 27, value)
            End Set
        End Property
        ''' <summary>Bit 28 de Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags: LipType28</summary>
        Public Property LipVariantsLipMorphFlagsLipType28 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 28)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 28, value)
            End Set
        End Property
        ''' <summary>Bit 29 de Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags: LipType29</summary>
        Public Property LipVariantsLipMorphFlagsLipType29 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 29)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 29, value)
            End Set
        End Property
        ''' <summary>Bit 30 de Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags: LipType30</summary>
        Public Property LipVariantsLipMorphFlagsLipType30 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 30)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 30, value)
            End Set
        End Property
        ''' <summary>Bit 31 de Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags: LipType31</summary>
        Public Property LipVariantsLipMorphFlagsLipType31 As Boolean
            Get
                Return Bit("Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 31)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Male Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 31, value)
            End Set
        End Property

        ''' <summary>Head Data\Male Head Data\DFTM\Default Face Texture Male  -&gt;  TXST / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property MaleHeadDataDefaultFaceTextureMale As UInteger
            Get
                Return Referencia("Head Data\Male Head Data\DFTM\Default Face Texture Male")
            End Get
            Set(value As UInteger)
                PonerReferencia("Head Data\Male Head Data\DFTM\Default Face Texture Male", value)
            End Set
        End Property

        ''' <summary>Head Data\Male Head Data\Model\MODL\Model FileName</summary>
        Public Property ModelModelFileName3 As String
            Get
                Return Txt("Head Data\Male Head Data\Model\MODL\Model FileName")
            End Get
            Set(value As String)
                Escribir("Head Data\Male Head Data\Model\MODL\Model FileName", value)
            End Set
        End Property

        ''' <summary>Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags</summary>
        Public Property NoseVariantsNoseMorphFlags2 As UInteger
            Get
                Return CUInt(Entero("Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags"))
            End Get
            Set(value As UInteger)
                Escribir("Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", CLng(value))
            End Set
        End Property

        ''' <summary>Bit 0 de Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags: NoseType0</summary>
        Public Property NoseVariantsNoseMorphFlags2NoseType0 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 0)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 0, value)
            End Set
        End Property
        ''' <summary>Bit 1 de Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags: NoseType1</summary>
        Public Property NoseVariantsNoseMorphFlags2NoseType1 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 1)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 1, value)
            End Set
        End Property
        ''' <summary>Bit 2 de Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags: NoseType2</summary>
        Public Property NoseVariantsNoseMorphFlags2NoseType2 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 2)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 2, value)
            End Set
        End Property
        ''' <summary>Bit 3 de Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags: NoseType3</summary>
        Public Property NoseVariantsNoseMorphFlags2NoseType3 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 3)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 3, value)
            End Set
        End Property
        ''' <summary>Bit 4 de Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags: NoseType4</summary>
        Public Property NoseVariantsNoseMorphFlags2NoseType4 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 4)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 4, value)
            End Set
        End Property
        ''' <summary>Bit 5 de Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags: NoseType5</summary>
        Public Property NoseVariantsNoseMorphFlags2NoseType5 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 5)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 5, value)
            End Set
        End Property
        ''' <summary>Bit 6 de Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags: NoseType6</summary>
        Public Property NoseVariantsNoseMorphFlags2NoseType6 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 6)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 6, value)
            End Set
        End Property
        ''' <summary>Bit 7 de Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags: NoseType7</summary>
        Public Property NoseVariantsNoseMorphFlags2NoseType7 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 7)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 7, value)
            End Set
        End Property
        ''' <summary>Bit 8 de Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags: NoseType8</summary>
        Public Property NoseVariantsNoseMorphFlags2NoseType8 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 8)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 8, value)
            End Set
        End Property
        ''' <summary>Bit 9 de Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags: NoseType9</summary>
        Public Property NoseVariantsNoseMorphFlags2NoseType9 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 9)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 9, value)
            End Set
        End Property
        ''' <summary>Bit 10 de Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags: NoseType10</summary>
        Public Property NoseVariantsNoseMorphFlags2NoseType10 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 10)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 10, value)
            End Set
        End Property
        ''' <summary>Bit 11 de Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags: NoseType11</summary>
        Public Property NoseVariantsNoseMorphFlags2NoseType11 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 11)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 11, value)
            End Set
        End Property
        ''' <summary>Bit 12 de Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags: NoseType12</summary>
        Public Property NoseVariantsNoseMorphFlags2NoseType12 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 12)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 12, value)
            End Set
        End Property
        ''' <summary>Bit 13 de Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags: NoseType13</summary>
        Public Property NoseVariantsNoseMorphFlags2NoseType13 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 13)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 13, value)
            End Set
        End Property
        ''' <summary>Bit 14 de Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags: NoseType14</summary>
        Public Property NoseVariantsNoseMorphFlags2NoseType14 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 14)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 14, value)
            End Set
        End Property
        ''' <summary>Bit 15 de Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags: NoseType15</summary>
        Public Property NoseVariantsNoseMorphFlags2NoseType15 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 15)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 15, value)
            End Set
        End Property
        ''' <summary>Bit 16 de Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags: NoseType16</summary>
        Public Property NoseVariantsNoseMorphFlags2NoseType16 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 16)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 16, value)
            End Set
        End Property
        ''' <summary>Bit 17 de Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags: NoseType17</summary>
        Public Property NoseVariantsNoseMorphFlags2NoseType17 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 17)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 17, value)
            End Set
        End Property
        ''' <summary>Bit 18 de Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags: NoseType18</summary>
        Public Property NoseVariantsNoseMorphFlags2NoseType18 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 18)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 18, value)
            End Set
        End Property
        ''' <summary>Bit 19 de Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags: NoseType19</summary>
        Public Property NoseVariantsNoseMorphFlags2NoseType19 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 19)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 19, value)
            End Set
        End Property
        ''' <summary>Bit 20 de Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags: NoseType20</summary>
        Public Property NoseVariantsNoseMorphFlags2NoseType20 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 20)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 20, value)
            End Set
        End Property
        ''' <summary>Bit 21 de Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags: NoseType21</summary>
        Public Property NoseVariantsNoseMorphFlags2NoseType21 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 21)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 21, value)
            End Set
        End Property
        ''' <summary>Bit 22 de Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags: NoseType22</summary>
        Public Property NoseVariantsNoseMorphFlags2NoseType22 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 22)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 22, value)
            End Set
        End Property
        ''' <summary>Bit 23 de Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags: NoseType23</summary>
        Public Property NoseVariantsNoseMorphFlags2NoseType23 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 23)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 23, value)
            End Set
        End Property
        ''' <summary>Bit 24 de Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags: NoseType24</summary>
        Public Property NoseVariantsNoseMorphFlags2NoseType24 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 24)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 24, value)
            End Set
        End Property
        ''' <summary>Bit 25 de Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags: NoseType25</summary>
        Public Property NoseVariantsNoseMorphFlags2NoseType25 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 25)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 25, value)
            End Set
        End Property
        ''' <summary>Bit 26 de Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags: NoseType26</summary>
        Public Property NoseVariantsNoseMorphFlags2NoseType26 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 26)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 26, value)
            End Set
        End Property
        ''' <summary>Bit 27 de Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags: NoseType27</summary>
        Public Property NoseVariantsNoseMorphFlags2NoseType27 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 27)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 27, value)
            End Set
        End Property
        ''' <summary>Bit 28 de Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags: NoseType28</summary>
        Public Property NoseVariantsNoseMorphFlags2NoseType28 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 28)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 28, value)
            End Set
        End Property
        ''' <summary>Bit 29 de Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags: NoseType29</summary>
        Public Property NoseVariantsNoseMorphFlags2NoseType29 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 29)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 29, value)
            End Set
        End Property
        ''' <summary>Bit 30 de Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags: NoseType30</summary>
        Public Property NoseVariantsNoseMorphFlags2NoseType30 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 30)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 30, value)
            End Set
        End Property
        ''' <summary>Bit 31 de Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags: NoseType31</summary>
        Public Property NoseVariantsNoseMorphFlags2NoseType31 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 31)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Nose Variants\Nose Morph Flags", 31, value)
            End Set
        End Property

        ''' <summary>Head Data\Female Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags</summary>
        Public Property BrowVariantsBrowMorphFlags2 As UInteger
            Get
                Return CUInt(Entero("Head Data\Female Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags"))
            End Get
            Set(value As UInteger)
                Escribir("Head Data\Female Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags", CLng(value))
            End Set
        End Property

        ''' <summary>Bit 0 de Head Data\Female Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags: BrowType0</summary>
        Public Property BrowVariantsBrowMorphFlags2BrowType0 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags", 0)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags", 0, value)
            End Set
        End Property
        ''' <summary>Bit 1 de Head Data\Female Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags: BrowType1</summary>
        Public Property BrowVariantsBrowMorphFlags2BrowType1 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags", 1)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags", 1, value)
            End Set
        End Property
        ''' <summary>Bit 2 de Head Data\Female Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags: BrowType2</summary>
        Public Property BrowVariantsBrowMorphFlags2BrowType2 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags", 2)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags", 2, value)
            End Set
        End Property
        ''' <summary>Bit 3 de Head Data\Female Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags: BrowType3</summary>
        Public Property BrowVariantsBrowMorphFlags2BrowType3 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags", 3)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags", 3, value)
            End Set
        End Property
        ''' <summary>Bit 4 de Head Data\Female Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags: BrowType4</summary>
        Public Property BrowVariantsBrowMorphFlags2BrowType4 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags", 4)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags", 4, value)
            End Set
        End Property
        ''' <summary>Bit 5 de Head Data\Female Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags: BrowType5</summary>
        Public Property BrowVariantsBrowMorphFlags2BrowType5 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags", 5)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags", 5, value)
            End Set
        End Property
        ''' <summary>Bit 6 de Head Data\Female Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags: BrowType6</summary>
        Public Property BrowVariantsBrowMorphFlags2BrowType6 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags", 6)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags", 6, value)
            End Set
        End Property
        ''' <summary>Bit 7 de Head Data\Female Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags: BrowType7</summary>
        Public Property BrowVariantsBrowMorphFlags2BrowType7 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags", 7)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags", 7, value)
            End Set
        End Property
        ''' <summary>Bit 8 de Head Data\Female Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags: BrowType8</summary>
        Public Property BrowVariantsBrowMorphFlags2BrowType8 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags", 8)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags", 8, value)
            End Set
        End Property
        ''' <summary>Bit 9 de Head Data\Female Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags: BrowType9</summary>
        Public Property BrowVariantsBrowMorphFlags2BrowType9 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags", 9)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags", 9, value)
            End Set
        End Property
        ''' <summary>Bit 10 de Head Data\Female Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags: BrowType10</summary>
        Public Property BrowVariantsBrowMorphFlags2BrowType10 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags", 10)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags", 10, value)
            End Set
        End Property
        ''' <summary>Bit 11 de Head Data\Female Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags: BrowType11</summary>
        Public Property BrowVariantsBrowMorphFlags2BrowType11 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags", 11)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags", 11, value)
            End Set
        End Property
        ''' <summary>Bit 12 de Head Data\Female Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags: BrowType12</summary>
        Public Property BrowVariantsBrowMorphFlags2BrowType12 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags", 12)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags", 12, value)
            End Set
        End Property
        ''' <summary>Bit 13 de Head Data\Female Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags: BrowType13</summary>
        Public Property BrowVariantsBrowMorphFlags2BrowType13 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags", 13)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags", 13, value)
            End Set
        End Property
        ''' <summary>Bit 14 de Head Data\Female Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags: BrowType14</summary>
        Public Property BrowVariantsBrowMorphFlags2BrowType14 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags", 14)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags", 14, value)
            End Set
        End Property
        ''' <summary>Bit 15 de Head Data\Female Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags: BrowType15</summary>
        Public Property BrowVariantsBrowMorphFlags2BrowType15 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags", 15)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags", 15, value)
            End Set
        End Property
        ''' <summary>Bit 16 de Head Data\Female Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags: BrowType16</summary>
        Public Property BrowVariantsBrowMorphFlags2BrowType16 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags", 16)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags", 16, value)
            End Set
        End Property
        ''' <summary>Bit 17 de Head Data\Female Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags: BrowType17</summary>
        Public Property BrowVariantsBrowMorphFlags2BrowType17 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags", 17)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags", 17, value)
            End Set
        End Property
        ''' <summary>Bit 18 de Head Data\Female Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags: BrowType18</summary>
        Public Property BrowVariantsBrowMorphFlags2BrowType18 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags", 18)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags", 18, value)
            End Set
        End Property
        ''' <summary>Bit 19 de Head Data\Female Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags: BrowType19</summary>
        Public Property BrowVariantsBrowMorphFlags2BrowType19 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags", 19)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags", 19, value)
            End Set
        End Property
        ''' <summary>Bit 20 de Head Data\Female Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags: BrowType20</summary>
        Public Property BrowVariantsBrowMorphFlags2BrowType20 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags", 20)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Brow Variants\Brow Morph Flags", 20, value)
            End Set
        End Property

        ''' <summary>Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1</summary>
        Public Property EyeVariantsEyeMorphFlags12 As UInteger
            Get
                Return CUInt(Entero("Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1"))
            End Get
            Set(value As UInteger)
                Escribir("Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", CLng(value))
            End Set
        End Property

        ''' <summary>Bit 0 de Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1: EyesType0</summary>
        Public Property EyeVariantsEyeMorphFlags12EyesType0 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 0)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 0, value)
            End Set
        End Property
        ''' <summary>Bit 1 de Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1: EyesType1</summary>
        Public Property EyeVariantsEyeMorphFlags12EyesType1 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 1)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 1, value)
            End Set
        End Property
        ''' <summary>Bit 2 de Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1: EyesType2</summary>
        Public Property EyeVariantsEyeMorphFlags12EyesType2 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 2)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 2, value)
            End Set
        End Property
        ''' <summary>Bit 3 de Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1: EyesType3</summary>
        Public Property EyeVariantsEyeMorphFlags12EyesType3 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 3)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 3, value)
            End Set
        End Property
        ''' <summary>Bit 4 de Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1: EyesType4</summary>
        Public Property EyeVariantsEyeMorphFlags12EyesType4 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 4)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 4, value)
            End Set
        End Property
        ''' <summary>Bit 5 de Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1: EyesType5</summary>
        Public Property EyeVariantsEyeMorphFlags12EyesType5 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 5)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 5, value)
            End Set
        End Property
        ''' <summary>Bit 6 de Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1: EyesType6</summary>
        Public Property EyeVariantsEyeMorphFlags12EyesType6 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 6)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 6, value)
            End Set
        End Property
        ''' <summary>Bit 7 de Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1: EyesType7</summary>
        Public Property EyeVariantsEyeMorphFlags12EyesType7 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 7)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 7, value)
            End Set
        End Property
        ''' <summary>Bit 8 de Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1: EyesType8</summary>
        Public Property EyeVariantsEyeMorphFlags12EyesType8 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 8)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 8, value)
            End Set
        End Property
        ''' <summary>Bit 9 de Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1: EyesType9</summary>
        Public Property EyeVariantsEyeMorphFlags12EyesType9 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 9)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 9, value)
            End Set
        End Property
        ''' <summary>Bit 10 de Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1: EyesType10</summary>
        Public Property EyeVariantsEyeMorphFlags12EyesType10 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 10)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 10, value)
            End Set
        End Property
        ''' <summary>Bit 11 de Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1: EyesType11</summary>
        Public Property EyeVariantsEyeMorphFlags12EyesType11 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 11)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 11, value)
            End Set
        End Property
        ''' <summary>Bit 12 de Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1: EyesType12</summary>
        Public Property EyeVariantsEyeMorphFlags12EyesType12 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 12)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 12, value)
            End Set
        End Property
        ''' <summary>Bit 13 de Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1: EyesType13</summary>
        Public Property EyeVariantsEyeMorphFlags12EyesType13 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 13)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 13, value)
            End Set
        End Property
        ''' <summary>Bit 14 de Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1: EyesType14</summary>
        Public Property EyeVariantsEyeMorphFlags12EyesType14 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 14)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 14, value)
            End Set
        End Property
        ''' <summary>Bit 15 de Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1: EyesType15</summary>
        Public Property EyeVariantsEyeMorphFlags12EyesType15 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 15)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 15, value)
            End Set
        End Property
        ''' <summary>Bit 16 de Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1: EyesType16</summary>
        Public Property EyeVariantsEyeMorphFlags12EyesType16 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 16)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 16, value)
            End Set
        End Property
        ''' <summary>Bit 17 de Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1: EyesType17</summary>
        Public Property EyeVariantsEyeMorphFlags12EyesType17 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 17)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 17, value)
            End Set
        End Property
        ''' <summary>Bit 18 de Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1: EyesType18</summary>
        Public Property EyeVariantsEyeMorphFlags12EyesType18 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 18)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 18, value)
            End Set
        End Property
        ''' <summary>Bit 19 de Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1: EyesType19</summary>
        Public Property EyeVariantsEyeMorphFlags12EyesType19 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 19)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 19, value)
            End Set
        End Property
        ''' <summary>Bit 20 de Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1: EyesType20</summary>
        Public Property EyeVariantsEyeMorphFlags12EyesType20 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 20)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 20, value)
            End Set
        End Property
        ''' <summary>Bit 21 de Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1: EyesType21</summary>
        Public Property EyeVariantsEyeMorphFlags12EyesType21 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 21)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 21, value)
            End Set
        End Property
        ''' <summary>Bit 22 de Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1: EyesType22</summary>
        Public Property EyeVariantsEyeMorphFlags12EyesType22 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 22)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 22, value)
            End Set
        End Property
        ''' <summary>Bit 23 de Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1: EyesType23</summary>
        Public Property EyeVariantsEyeMorphFlags12EyesType23 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 23)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 23, value)
            End Set
        End Property
        ''' <summary>Bit 24 de Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1: EyesType24</summary>
        Public Property EyeVariantsEyeMorphFlags12EyesType24 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 24)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 24, value)
            End Set
        End Property
        ''' <summary>Bit 25 de Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1: EyesType25</summary>
        Public Property EyeVariantsEyeMorphFlags12EyesType25 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 25)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 25, value)
            End Set
        End Property
        ''' <summary>Bit 26 de Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1: EyesType26</summary>
        Public Property EyeVariantsEyeMorphFlags12EyesType26 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 26)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 26, value)
            End Set
        End Property
        ''' <summary>Bit 27 de Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1: EyesType27</summary>
        Public Property EyeVariantsEyeMorphFlags12EyesType27 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 27)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 27, value)
            End Set
        End Property
        ''' <summary>Bit 28 de Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1: EyesType28</summary>
        Public Property EyeVariantsEyeMorphFlags12EyesType28 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 28)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 28, value)
            End Set
        End Property
        ''' <summary>Bit 29 de Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1: EyesType29</summary>
        Public Property EyeVariantsEyeMorphFlags12EyesType29 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 29)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 29, value)
            End Set
        End Property
        ''' <summary>Bit 30 de Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1: EyesType30</summary>
        Public Property EyeVariantsEyeMorphFlags12EyesType30 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 30)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 30, value)
            End Set
        End Property
        ''' <summary>Bit 31 de Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1: EyesType31</summary>
        Public Property EyeVariantsEyeMorphFlags12EyesType31 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 31)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 1", 31, value)
            End Set
        End Property

        ''' <summary>Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 2</summary>
        Public Property EyeVariantsEyeMorphFlags22 As Byte
            Get
                Return CByte(Entero("Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 2"))
            End Get
            Set(value As Byte)
                Escribir("Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 2", CLng(value))
            End Set
        End Property

        ''' <summary>Bit 0 de Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 2: EyesType32</summary>
        Public Property EyeVariantsEyeMorphFlags22EyesType32 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 2", 0)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 2", 0, value)
            End Set
        End Property
        ''' <summary>Bit 1 de Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 2: EyesType33</summary>
        Public Property EyeVariantsEyeMorphFlags22EyesType33 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 2", 1)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 2", 1, value)
            End Set
        End Property
        ''' <summary>Bit 2 de Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 2: EyesType34</summary>
        Public Property EyeVariantsEyeMorphFlags22EyesType34 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 2", 2)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 2", 2, value)
            End Set
        End Property
        ''' <summary>Bit 3 de Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 2: EyesType35</summary>
        Public Property EyeVariantsEyeMorphFlags22EyesType35 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 2", 3)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 2", 3, value)
            End Set
        End Property
        ''' <summary>Bit 4 de Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 2: EyesType36</summary>
        Public Property EyeVariantsEyeMorphFlags22EyesType36 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 2", 4)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 2", 4, value)
            End Set
        End Property
        ''' <summary>Bit 5 de Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 2: EyesType37</summary>
        Public Property EyeVariantsEyeMorphFlags22EyesType37 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 2", 5)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 2", 5, value)
            End Set
        End Property
        ''' <summary>Bit 6 de Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 2: EyesType38</summary>
        Public Property EyeVariantsEyeMorphFlags22EyesType38 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 2", 6)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Eye Variants\Eye Morph Flags 2", 6, value)
            End Set
        End Property

        ''' <summary>Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags</summary>
        Public Property LipVariantsLipMorphFlags2 As UInteger
            Get
                Return CUInt(Entero("Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags"))
            End Get
            Set(value As UInteger)
                Escribir("Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", CLng(value))
            End Set
        End Property

        ''' <summary>Bit 0 de Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags: LipType0</summary>
        Public Property LipVariantsLipMorphFlags2LipType0 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 0)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 0, value)
            End Set
        End Property
        ''' <summary>Bit 1 de Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags: LipType1</summary>
        Public Property LipVariantsLipMorphFlags2LipType1 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 1)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 1, value)
            End Set
        End Property
        ''' <summary>Bit 2 de Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags: LipType2</summary>
        Public Property LipVariantsLipMorphFlags2LipType2 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 2)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 2, value)
            End Set
        End Property
        ''' <summary>Bit 3 de Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags: LipType3</summary>
        Public Property LipVariantsLipMorphFlags2LipType3 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 3)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 3, value)
            End Set
        End Property
        ''' <summary>Bit 4 de Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags: LipType4</summary>
        Public Property LipVariantsLipMorphFlags2LipType4 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 4)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 4, value)
            End Set
        End Property
        ''' <summary>Bit 5 de Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags: LipType5</summary>
        Public Property LipVariantsLipMorphFlags2LipType5 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 5)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 5, value)
            End Set
        End Property
        ''' <summary>Bit 6 de Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags: LipType6</summary>
        Public Property LipVariantsLipMorphFlags2LipType6 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 6)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 6, value)
            End Set
        End Property
        ''' <summary>Bit 7 de Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags: LipType7</summary>
        Public Property LipVariantsLipMorphFlags2LipType7 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 7)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 7, value)
            End Set
        End Property
        ''' <summary>Bit 8 de Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags: LipType8</summary>
        Public Property LipVariantsLipMorphFlags2LipType8 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 8)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 8, value)
            End Set
        End Property
        ''' <summary>Bit 9 de Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags: LipType9</summary>
        Public Property LipVariantsLipMorphFlags2LipType9 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 9)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 9, value)
            End Set
        End Property
        ''' <summary>Bit 10 de Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags: LipType10</summary>
        Public Property LipVariantsLipMorphFlags2LipType10 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 10)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 10, value)
            End Set
        End Property
        ''' <summary>Bit 11 de Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags: LipType11</summary>
        Public Property LipVariantsLipMorphFlags2LipType11 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 11)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 11, value)
            End Set
        End Property
        ''' <summary>Bit 12 de Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags: LipType12</summary>
        Public Property LipVariantsLipMorphFlags2LipType12 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 12)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 12, value)
            End Set
        End Property
        ''' <summary>Bit 13 de Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags: LipType13</summary>
        Public Property LipVariantsLipMorphFlags2LipType13 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 13)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 13, value)
            End Set
        End Property
        ''' <summary>Bit 14 de Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags: LipType14</summary>
        Public Property LipVariantsLipMorphFlags2LipType14 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 14)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 14, value)
            End Set
        End Property
        ''' <summary>Bit 15 de Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags: LipType15</summary>
        Public Property LipVariantsLipMorphFlags2LipType15 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 15)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 15, value)
            End Set
        End Property
        ''' <summary>Bit 16 de Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags: LipType16</summary>
        Public Property LipVariantsLipMorphFlags2LipType16 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 16)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 16, value)
            End Set
        End Property
        ''' <summary>Bit 17 de Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags: LipType17</summary>
        Public Property LipVariantsLipMorphFlags2LipType17 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 17)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 17, value)
            End Set
        End Property
        ''' <summary>Bit 18 de Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags: LipType18</summary>
        Public Property LipVariantsLipMorphFlags2LipType18 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 18)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 18, value)
            End Set
        End Property
        ''' <summary>Bit 19 de Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags: LipType19</summary>
        Public Property LipVariantsLipMorphFlags2LipType19 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 19)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 19, value)
            End Set
        End Property
        ''' <summary>Bit 20 de Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags: LipType20</summary>
        Public Property LipVariantsLipMorphFlags2LipType20 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 20)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 20, value)
            End Set
        End Property
        ''' <summary>Bit 21 de Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags: LipType21</summary>
        Public Property LipVariantsLipMorphFlags2LipType21 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 21)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 21, value)
            End Set
        End Property
        ''' <summary>Bit 22 de Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags: LipType22</summary>
        Public Property LipVariantsLipMorphFlags2LipType22 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 22)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 22, value)
            End Set
        End Property
        ''' <summary>Bit 23 de Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags: LipType23</summary>
        Public Property LipVariantsLipMorphFlags2LipType23 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 23)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 23, value)
            End Set
        End Property
        ''' <summary>Bit 24 de Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags: LipType24</summary>
        Public Property LipVariantsLipMorphFlags2LipType24 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 24)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 24, value)
            End Set
        End Property
        ''' <summary>Bit 25 de Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags: LipType25</summary>
        Public Property LipVariantsLipMorphFlags2LipType25 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 25)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 25, value)
            End Set
        End Property
        ''' <summary>Bit 26 de Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags: LipType26</summary>
        Public Property LipVariantsLipMorphFlags2LipType26 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 26)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 26, value)
            End Set
        End Property
        ''' <summary>Bit 27 de Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags: LipType27</summary>
        Public Property LipVariantsLipMorphFlags2LipType27 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 27)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 27, value)
            End Set
        End Property
        ''' <summary>Bit 28 de Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags: LipType28</summary>
        Public Property LipVariantsLipMorphFlags2LipType28 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 28)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 28, value)
            End Set
        End Property
        ''' <summary>Bit 29 de Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags: LipType29</summary>
        Public Property LipVariantsLipMorphFlags2LipType29 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 29)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 29, value)
            End Set
        End Property
        ''' <summary>Bit 30 de Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags: LipType30</summary>
        Public Property LipVariantsLipMorphFlags2LipType30 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 30)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 30, value)
            End Set
        End Property
        ''' <summary>Bit 31 de Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags: LipType31</summary>
        Public Property LipVariantsLipMorphFlags2LipType31 As Boolean
            Get
                Return Bit("Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 31)
            End Get
            Set(value As Boolean)
                PonerBit("Head Data\Female Head Data\Available Morphs\MPAV\Lip Variants\Lip Morph Flags", 31, value)
            End Set
        End Property

        ''' <summary>Head Data\Female Head Data\DFTF\Default Face Texture Female  -&gt;  TXST / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property FemaleHeadDataDefaultFaceTextureFemale As UInteger
            Get
                Return Referencia("Head Data\Female Head Data\DFTF\Default Face Texture Female")
            End Get
            Set(value As UInteger)
                PonerReferencia("Head Data\Female Head Data\DFTF\Default Face Texture Female", value)
            End Set
        End Property

        ''' <summary>Head Data\Female Head Data\Model\MODL\Model FileName</summary>
        Public Property ModelModelFileName4 As String
            Get
                Return Txt("Head Data\Female Head Data\Model\MODL\Model FileName")
            End Get
            Set(value As String)
                Escribir("Head Data\Female Head Data\Model\MODL\Model FileName", value)
            End Set
        End Property

        ''' <summary>NAM8\Morph race  -&gt;  RACE / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property MorphRace As UInteger
            Get
                Return Referencia("NAM8\Morph race")
            End Get
            Set(value As UInteger)
                PonerReferencia("NAM8\Morph race", value)
            End Set
        End Property

        ''' <summary>RNAM\Armor race  -&gt;  RACE / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property ArmorRace As UInteger
            Get
                Return Referencia("RNAM\Armor race")
            End Get
            Set(value As UInteger)
                PonerReferencia("RNAM\Armor race", value)
            End Set
        End Property

        ''' <summary>Actor Effects</summary>
        Public ReadOnly Property ActorEffects As IReadOnlyList(Of RaceSSE_ActorEffects)
            Get
                Return Elementos(Of RaceSSE_ActorEffects)("Actor Effects", Function(n) New RaceSSE_ActorEffects(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Keywords\KWDA\Keywords</summary>
        Public ReadOnly Property Keywords As IReadOnlyList(Of RaceSSE_Keywords)
            Get
                Return Elementos(Of RaceSSE_Keywords)("Keywords\KWDA\Keywords", Function(n) New RaceSSE_Keywords(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>DATA\Skill Boosts</summary>
        Public ReadOnly Property SkillBoosts As IReadOnlyList(Of RaceSSE_SkillBoosts)
            Get
                Return Elementos(Of RaceSSE_SkillBoosts)("DATA\Skill Boosts", Function(n) New RaceSSE_SkillBoosts(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Movement Type Names</summary>
        Public ReadOnly Property MovementTypeNames As IReadOnlyList(Of RaceSSE_MovementTypeNames)
            Get
                Return Elementos(Of RaceSSE_MovementTypeNames)("Movement Type Names", Function(n) New RaceSSE_MovementTypeNames(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>VTCK\Voices</summary>
        Public ReadOnly Property Voices As IReadOnlyList(Of RaceSSE_Voices)
            Get
                Return Elementos(Of RaceSSE_Voices)("VTCK\Voices", Function(n) New RaceSSE_Voices(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>DNAM\Decapitate Armors</summary>
        Public ReadOnly Property DecapitateArmors As IReadOnlyList(Of RaceSSE_DecapitateArmors)
            Get
                Return Elementos(Of RaceSSE_DecapitateArmors)("DNAM\Decapitate Armors", Function(n) New RaceSSE_DecapitateArmors(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>HCLF\Default Hair Colors</summary>
        Public ReadOnly Property DefaultHairColors As IReadOnlyList(Of RaceSSE_DefaultHairColors)
            Get
                Return Elementos(Of RaceSSE_DefaultHairColors)("HCLF\Default Hair Colors", Function(n) New RaceSSE_DefaultHairColors(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Attacks</summary>
        Public ReadOnly Property Attacks As IReadOnlyList(Of RaceSSE_Attacks)
            Get
                Return Elementos(Of RaceSSE_Attacks)("Attacks", Function(n) New RaceSSE_Attacks(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Body Data\Male Body Data\Parts</summary>
        Public ReadOnly Property Parts As IReadOnlyList(Of RaceSSE_Parts)
            Get
                Return Elementos(Of RaceSSE_Parts)("Body Data\Male Body Data\Parts", Function(n) New RaceSSE_Parts(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Body Data\Male Body Data\Parts\Part\Model\MODS\Alternate Textures</summary>
        Public ReadOnly Property AlternateTextures As IReadOnlyList(Of RaceSSE_AlternateTextures)
            Get
                Return Elementos(Of RaceSSE_AlternateTextures)("Body Data\Male Body Data\Parts\Part\Model\MODS\Alternate Textures", Function(n) New RaceSSE_AlternateTextures(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Body Data\Female Body Data\Parts</summary>
        Public ReadOnly Property Parts2 As IReadOnlyList(Of RaceSSE_Parts2)
            Get
                Return Elementos(Of RaceSSE_Parts2)("Body Data\Female Body Data\Parts", Function(n) New RaceSSE_Parts2(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Body Data\Female Body Data\Parts\Part\Model\MODS\Alternate Textures</summary>
        Public ReadOnly Property AlternateTextures2 As IReadOnlyList(Of RaceSSE_AlternateTextures2)
            Get
                Return Elementos(Of RaceSSE_AlternateTextures2)("Body Data\Female Body Data\Parts\Part\Model\MODS\Alternate Textures", Function(n) New RaceSSE_AlternateTextures2(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>HNAM\Hairs</summary>
        Public ReadOnly Property Hairs As IReadOnlyList(Of RaceSSE_Hairs)
            Get
                Return Elementos(Of RaceSSE_Hairs)("HNAM\Hairs", Function(n) New RaceSSE_Hairs(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>ENAM\Eyes</summary>
        Public ReadOnly Property Eyes As IReadOnlyList(Of RaceSSE_Eyes)
            Get
                Return Elementos(Of RaceSSE_Eyes)("ENAM\Eyes", Function(n) New RaceSSE_Eyes(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Male Behavior Graph\Model\MODS\Alternate Textures</summary>
        Public ReadOnly Property AlternateTextures3 As IReadOnlyList(Of RaceSSE_AlternateTextures3)
            Get
                Return Elementos(Of RaceSSE_AlternateTextures3)("Male Behavior Graph\Model\MODS\Alternate Textures", Function(n) New RaceSSE_AlternateTextures3(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Female Behavior Graph\Model\MODS\Alternate Textures</summary>
        Public ReadOnly Property AlternateTextures4 As IReadOnlyList(Of RaceSSE_AlternateTextures4)
            Get
                Return Elementos(Of RaceSSE_AlternateTextures4)("Female Behavior Graph\Model\MODS\Alternate Textures", Function(n) New RaceSSE_AlternateTextures4(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Biped Object Names</summary>
        Public ReadOnly Property BipedObjectNames As IReadOnlyList(Of RaceSSE_BipedObjectNames)
            Get
                Return Elementos(Of RaceSSE_BipedObjectNames)("Biped Object Names", Function(n) New RaceSSE_BipedObjectNames(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Movement Types</summary>
        Public ReadOnly Property MovementTypes As IReadOnlyList(Of RaceSSE_MovementTypes)
            Get
                Return Elementos(Of RaceSSE_MovementTypes)("Movement Types", Function(n) New RaceSSE_MovementTypes(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Equip Slots</summary>
        Public ReadOnly Property EquipSlots As IReadOnlyList(Of RaceSSE_EquipSlots)
            Get
                Return Elementos(Of RaceSSE_EquipSlots)("Equip Slots", Function(n) New RaceSSE_EquipSlots(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Phoneme Target Names</summary>
        Public ReadOnly Property PhonemeTargetNames As IReadOnlyList(Of RaceSSE_PhonemeTargetNames)
            Get
                Return Elementos(Of RaceSSE_PhonemeTargetNames)("Phoneme Target Names", Function(n) New RaceSSE_PhonemeTargetNames(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Head Data\Male Head Data\Head Parts</summary>
        Public ReadOnly Property HeadParts As IReadOnlyList(Of RaceSSE_HeadParts)
            Get
                Return Elementos(Of RaceSSE_HeadParts)("Head Data\Male Head Data\Head Parts", Function(n) New RaceSSE_HeadParts(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Head Data\Male Head Data\Race Presets Male</summary>
        Public ReadOnly Property RacePresetsMale As IReadOnlyList(Of RaceSSE_RacePresetsMale)
            Get
                Return Elementos(Of RaceSSE_RacePresetsMale)("Head Data\Male Head Data\Race Presets Male", Function(n) New RaceSSE_RacePresetsMale(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Head Data\Male Head Data\Available Hair Colors Male</summary>
        Public ReadOnly Property AvailableHairColorsMale As IReadOnlyList(Of RaceSSE_AvailableHairColorsMale)
            Get
                Return Elementos(Of RaceSSE_AvailableHairColorsMale)("Head Data\Male Head Data\Available Hair Colors Male", Function(n) New RaceSSE_AvailableHairColorsMale(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Head Data\Male Head Data\Face Details Texture Set List Male</summary>
        Public ReadOnly Property FaceDetailsTextureSetListMale As IReadOnlyList(Of RaceSSE_FaceDetailsTextureSetListMale)
            Get
                Return Elementos(Of RaceSSE_FaceDetailsTextureSetListMale)("Head Data\Male Head Data\Face Details Texture Set List Male", Function(n) New RaceSSE_FaceDetailsTextureSetListMale(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Head Data\Male Head Data\Tint Masks</summary>
        Public ReadOnly Property TintMasks As IReadOnlyList(Of RaceSSE_TintMasks)
            Get
                Return Elementos(Of RaceSSE_TintMasks)("Head Data\Male Head Data\Tint Masks", Function(n) New RaceSSE_TintMasks(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Head Data\Male Head Data\Tint Masks\Tint Assets\Presets</summary>
        Public ReadOnly Property Presets As IReadOnlyList(Of RaceSSE_Presets)
            Get
                Return Elementos(Of RaceSSE_Presets)("Head Data\Male Head Data\Tint Masks\Tint Assets\Presets", Function(n) New RaceSSE_Presets(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Head Data\Male Head Data\Model\MODS\Alternate Textures</summary>
        Public ReadOnly Property AlternateTextures5 As IReadOnlyList(Of RaceSSE_AlternateTextures5)
            Get
                Return Elementos(Of RaceSSE_AlternateTextures5)("Head Data\Male Head Data\Model\MODS\Alternate Textures", Function(n) New RaceSSE_AlternateTextures5(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Head Data\Female Head Data\Head Parts</summary>
        Public ReadOnly Property HeadParts2 As IReadOnlyList(Of RaceSSE_HeadParts2)
            Get
                Return Elementos(Of RaceSSE_HeadParts2)("Head Data\Female Head Data\Head Parts", Function(n) New RaceSSE_HeadParts2(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Head Data\Female Head Data\Race Presets Female</summary>
        Public ReadOnly Property RacePresetsFemale As IReadOnlyList(Of RaceSSE_RacePresetsFemale)
            Get
                Return Elementos(Of RaceSSE_RacePresetsFemale)("Head Data\Female Head Data\Race Presets Female", Function(n) New RaceSSE_RacePresetsFemale(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Head Data\Female Head Data\Available Hair Colors Female</summary>
        Public ReadOnly Property AvailableHairColorsFemale As IReadOnlyList(Of RaceSSE_AvailableHairColorsFemale)
            Get
                Return Elementos(Of RaceSSE_AvailableHairColorsFemale)("Head Data\Female Head Data\Available Hair Colors Female", Function(n) New RaceSSE_AvailableHairColorsFemale(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Head Data\Female Head Data\Face Details Texture Set List Female</summary>
        Public ReadOnly Property FaceDetailsTextureSetListFemale As IReadOnlyList(Of RaceSSE_FaceDetailsTextureSetListFemale)
            Get
                Return Elementos(Of RaceSSE_FaceDetailsTextureSetListFemale)("Head Data\Female Head Data\Face Details Texture Set List Female", Function(n) New RaceSSE_FaceDetailsTextureSetListFemale(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Head Data\Female Head Data\Tint Masks</summary>
        Public ReadOnly Property TintMasks2 As IReadOnlyList(Of RaceSSE_TintMasks2)
            Get
                Return Elementos(Of RaceSSE_TintMasks2)("Head Data\Female Head Data\Tint Masks", Function(n) New RaceSSE_TintMasks2(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Head Data\Female Head Data\Tint Masks\Tint Assets\Presets</summary>
        Public ReadOnly Property Presets2 As IReadOnlyList(Of RaceSSE_Presets2)
            Get
                Return Elementos(Of RaceSSE_Presets2)("Head Data\Female Head Data\Tint Masks\Tint Assets\Presets", Function(n) New RaceSSE_Presets2(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Head Data\Female Head Data\Model\MODS\Alternate Textures</summary>
        Public ReadOnly Property AlternateTextures6 As IReadOnlyList(Of RaceSSE_AlternateTextures6)
            Get
                Return Elementos(Of RaceSSE_AlternateTextures6)("Head Data\Female Head Data\Model\MODS\Alternate Textures", Function(n) New RaceSSE_AlternateTextures6(n, Context, Resolver))
            End Get
        End Property

    End Class

    ''' <summary>Un elemento de Actor Effects.</summary>
    Public NotInheritable Class RaceSSE_ActorEffects
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>SPLO\Actor Effect  -&gt;  SPEL / SHOU / LVSP. Referencia en el espacio del orden de carga.</summary>
        Public Property ActorEffect As UInteger
            Get
                Return Referencia("SPLO\Actor Effect")
            End Get
            Set(value As UInteger)
                PonerReferencia("SPLO\Actor Effect", value)
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Keywords\KWDA\Keywords.</summary>
    Public NotInheritable Class RaceSSE_Keywords
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Keyword  -&gt;  KYWD / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property Keyword As UInteger
            Get
                Return Referencia("Keyword")
            End Get
            Set(value As UInteger)
                PonerReferencia("Keyword", value)
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de DATA\Skill Boosts.</summary>
    Public NotInheritable Class RaceSSE_SkillBoosts
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Skill Boost\Skill</summary>
        Public Property SkillBoostSkill As SByte
            Get
                Return CSByte(Entero("Skill Boost\Skill"))
            End Get
            Set(value As SByte)
                Escribir("Skill Boost\Skill", CLng(value))
            End Set
        End Property

        ''' <summary>Skill Boost\Boost</summary>
        Public Property SkillBoostBoost As SByte
            Get
                Return CSByte(Entero("Skill Boost\Boost"))
            End Get
            Set(value As SByte)
                Escribir("Skill Boost\Boost", CLng(value))
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Movement Type Names.</summary>
    Public NotInheritable Class RaceSSE_MovementTypeNames
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>MTNM\Name</summary>
        Public Property Name As String
            Get
                Return Txt("MTNM\Name")
            End Get
            Set(value As String)
                Escribir("MTNM\Name", value)
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de VTCK\Voices.</summary>
    Public NotInheritable Class RaceSSE_Voices
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Voice  -&gt;  VTYP. Referencia en el espacio del orden de carga.</summary>
        Public Property Voice As UInteger
            Get
                Return Referencia("Voice")
            End Get
            Set(value As UInteger)
                PonerReferencia("Voice", value)
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de DNAM\Decapitate Armors.</summary>
    Public NotInheritable Class RaceSSE_DecapitateArmors
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Decapitate Armor  -&gt;  NULL / ARMO. Referencia en el espacio del orden de carga.</summary>
        Public Property DecapitateArmor As UInteger
            Get
                Return Referencia("Decapitate Armor")
            End Get
            Set(value As UInteger)
                PonerReferencia("Decapitate Armor", value)
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de HCLF\Default Hair Colors.</summary>
    Public NotInheritable Class RaceSSE_DefaultHairColors
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Default Hair Color  -&gt;  NULL / CLFM. Referencia en el espacio del orden de carga.</summary>
        Public Property DefaultHairColor As UInteger
            Get
                Return Referencia("Default Hair Color")
            End Get
            Set(value As UInteger)
                PonerReferencia("Default Hair Color", value)
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Attacks.</summary>
    Public NotInheritable Class RaceSSE_Attacks
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Attack\ATKD\Attack Data\Damage Mult</summary>
        Public Property AttackDataDamageMult As Single
            Get
                Return Flt("Attack\ATKD\Attack Data\Damage Mult")
            End Get
            Set(value As Single)
                Escribir("Attack\ATKD\Attack Data\Damage Mult", value)
            End Set
        End Property

        ''' <summary>Attack\ATKD\Attack Data\Attack Chance</summary>
        Public Property AttackDataAttackChance As Single
            Get
                Return Flt("Attack\ATKD\Attack Data\Attack Chance")
            End Get
            Set(value As Single)
                Escribir("Attack\ATKD\Attack Data\Attack Chance", value)
            End Set
        End Property

        ''' <summary>Attack\ATKD\Attack Data\Attack Spell  -&gt;  SPEL / SHOU / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property AttackDataAttackSpell As UInteger
            Get
                Return Referencia("Attack\ATKD\Attack Data\Attack Spell")
            End Get
            Set(value As UInteger)
                PonerReferencia("Attack\ATKD\Attack Data\Attack Spell", value)
            End Set
        End Property

        ''' <summary>Attack\ATKD\Attack Data\Attack Flags</summary>
        Public Property AttackDataAttackFlags As UInteger
            Get
                Return CUInt(Entero("Attack\ATKD\Attack Data\Attack Flags"))
            End Get
            Set(value As UInteger)
                Escribir("Attack\ATKD\Attack Data\Attack Flags", CLng(value))
            End Set
        End Property

        ''' <summary>Bit 0 de Attack\ATKD\Attack Data\Attack Flags: Ignore Weapon</summary>
        Public Property AttackDataAttackFlagsIgnoreWeapon As Boolean
            Get
                Return Bit("Attack\ATKD\Attack Data\Attack Flags", 0)
            End Get
            Set(value As Boolean)
                PonerBit("Attack\ATKD\Attack Data\Attack Flags", 0, value)
            End Set
        End Property
        ''' <summary>Bit 1 de Attack\ATKD\Attack Data\Attack Flags: Bash Attack</summary>
        Public Property AttackDataAttackFlagsBashAttack As Boolean
            Get
                Return Bit("Attack\ATKD\Attack Data\Attack Flags", 1)
            End Get
            Set(value As Boolean)
                PonerBit("Attack\ATKD\Attack Data\Attack Flags", 1, value)
            End Set
        End Property
        ''' <summary>Bit 2 de Attack\ATKD\Attack Data\Attack Flags: Power Attack</summary>
        Public Property AttackDataAttackFlagsPowerAttack As Boolean
            Get
                Return Bit("Attack\ATKD\Attack Data\Attack Flags", 2)
            End Get
            Set(value As Boolean)
                PonerBit("Attack\ATKD\Attack Data\Attack Flags", 2, value)
            End Set
        End Property
        ''' <summary>Bit 3 de Attack\ATKD\Attack Data\Attack Flags: Left Attack</summary>
        Public Property AttackDataAttackFlagsLeftAttack As Boolean
            Get
                Return Bit("Attack\ATKD\Attack Data\Attack Flags", 3)
            End Get
            Set(value As Boolean)
                PonerBit("Attack\ATKD\Attack Data\Attack Flags", 3, value)
            End Set
        End Property
        ''' <summary>Bit 4 de Attack\ATKD\Attack Data\Attack Flags: Rotating Attack</summary>
        Public Property AttackDataAttackFlagsRotatingAttack As Boolean
            Get
                Return Bit("Attack\ATKD\Attack Data\Attack Flags", 4)
            End Get
            Set(value As Boolean)
                PonerBit("Attack\ATKD\Attack Data\Attack Flags", 4, value)
            End Set
        End Property
        ''' <summary>Bit 31 de Attack\ATKD\Attack Data\Attack Flags: Override Data</summary>
        Public Property AttackDataAttackFlagsOverrideData As Boolean
            Get
                Return Bit("Attack\ATKD\Attack Data\Attack Flags", 31)
            End Get
            Set(value As Boolean)
                PonerBit("Attack\ATKD\Attack Data\Attack Flags", 31, value)
            End Set
        End Property

        ''' <summary>Attack\ATKD\Attack Data\Attack Angle</summary>
        Public Property AttackDataAttackAngle As Single
            Get
                Return Flt("Attack\ATKD\Attack Data\Attack Angle")
            End Get
            Set(value As Single)
                Escribir("Attack\ATKD\Attack Data\Attack Angle", value)
            End Set
        End Property

        ''' <summary>Attack\ATKD\Attack Data\Strike Angle</summary>
        Public Property AttackDataStrikeAngle As Single
            Get
                Return Flt("Attack\ATKD\Attack Data\Strike Angle")
            End Get
            Set(value As Single)
                Escribir("Attack\ATKD\Attack Data\Strike Angle", value)
            End Set
        End Property

        ''' <summary>Attack\ATKD\Attack Data\Stagger</summary>
        Public Property AttackDataStagger As Single
            Get
                Return Flt("Attack\ATKD\Attack Data\Stagger")
            End Get
            Set(value As Single)
                Escribir("Attack\ATKD\Attack Data\Stagger", value)
            End Set
        End Property

        ''' <summary>Attack\ATKD\Attack Data\Attack Type  -&gt;  KYWD / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property AttackDataAttackType As UInteger
            Get
                Return Referencia("Attack\ATKD\Attack Data\Attack Type")
            End Get
            Set(value As UInteger)
                PonerReferencia("Attack\ATKD\Attack Data\Attack Type", value)
            End Set
        End Property

        ''' <summary>Attack\ATKD\Attack Data\Knockdown</summary>
        Public Property AttackDataKnockdown As Single
            Get
                Return Flt("Attack\ATKD\Attack Data\Knockdown")
            End Get
            Set(value As Single)
                Escribir("Attack\ATKD\Attack Data\Knockdown", value)
            End Set
        End Property

        ''' <summary>Attack\ATKD\Attack Data\Recovery Time</summary>
        Public Property AttackDataRecoveryTime As Single
            Get
                Return Flt("Attack\ATKD\Attack Data\Recovery Time")
            End Get
            Set(value As Single)
                Escribir("Attack\ATKD\Attack Data\Recovery Time", value)
            End Set
        End Property

        ''' <summary>Attack\ATKD\Attack Data\Stamina Mult</summary>
        Public Property AttackDataStaminaMult As Single
            Get
                Return Flt("Attack\ATKD\Attack Data\Stamina Mult")
            End Get
            Set(value As Single)
                Escribir("Attack\ATKD\Attack Data\Stamina Mult", value)
            End Set
        End Property

        ''' <summary>Attack\ATKE\Attack Event</summary>
        Public Property AttackAttackEvent As String
            Get
                Return Txt("Attack\ATKE\Attack Event")
            End Get
            Set(value As String)
                Escribir("Attack\ATKE\Attack Event", value)
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Body Data\Male Body Data\Parts.</summary>
    Public NotInheritable Class RaceSSE_Parts
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Part\Model\MODL\Model FileName</summary>
        Public Property ModelModelFileName As String
            Get
                Return Txt("Part\Model\MODL\Model FileName")
            End Get
            Set(value As String)
                Escribir("Part\Model\MODL\Model FileName", value)
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Body Data\Male Body Data\Parts\Part\Model\MODS\Alternate Textures.</summary>
    Public NotInheritable Class RaceSSE_AlternateTextures
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Alternate Texture\New Texture  -&gt;  TXST. Referencia en el espacio del orden de carga.</summary>
        Public Property AlternateTextureNewTexture As UInteger
            Get
                Return Referencia("Alternate Texture\New Texture")
            End Get
            Set(value As UInteger)
                PonerReferencia("Alternate Texture\New Texture", value)
            End Set
        End Property

        ''' <summary>Alternate Texture\3D Index</summary>
        Public Property AlternateTexture3DIndex As Integer
            Get
                Return CInt(Entero("Alternate Texture\3D Index"))
            End Get
            Set(value As Integer)
                Escribir("Alternate Texture\3D Index", CLng(value))
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Body Data\Female Body Data\Parts.</summary>
    Public NotInheritable Class RaceSSE_Parts2
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Part\Model\MODL\Model FileName</summary>
        Public Property ModelModelFileName As String
            Get
                Return Txt("Part\Model\MODL\Model FileName")
            End Get
            Set(value As String)
                Escribir("Part\Model\MODL\Model FileName", value)
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Body Data\Female Body Data\Parts\Part\Model\MODS\Alternate Textures.</summary>
    Public NotInheritable Class RaceSSE_AlternateTextures2
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Alternate Texture\New Texture  -&gt;  TXST. Referencia en el espacio del orden de carga.</summary>
        Public Property AlternateTextureNewTexture As UInteger
            Get
                Return Referencia("Alternate Texture\New Texture")
            End Get
            Set(value As UInteger)
                PonerReferencia("Alternate Texture\New Texture", value)
            End Set
        End Property

        ''' <summary>Alternate Texture\3D Index</summary>
        Public Property AlternateTexture3DIndex As Integer
            Get
                Return CInt(Entero("Alternate Texture\3D Index"))
            End Get
            Set(value As Integer)
                Escribir("Alternate Texture\3D Index", CLng(value))
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de HNAM\Hairs.</summary>
    Public NotInheritable Class RaceSSE_Hairs
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Hair  -&gt;  HDPT / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property Hair As UInteger
            Get
                Return Referencia("Hair")
            End Get
            Set(value As UInteger)
                PonerReferencia("Hair", value)
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de ENAM\Eyes.</summary>
    Public NotInheritable Class RaceSSE_Eyes
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Eye  -&gt;  EYES / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property Eye As UInteger
            Get
                Return Referencia("Eye")
            End Get
            Set(value As UInteger)
                PonerReferencia("Eye", value)
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Male Behavior Graph\Model\MODS\Alternate Textures.</summary>
    Public NotInheritable Class RaceSSE_AlternateTextures3
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Alternate Texture\New Texture  -&gt;  TXST. Referencia en el espacio del orden de carga.</summary>
        Public Property AlternateTextureNewTexture As UInteger
            Get
                Return Referencia("Alternate Texture\New Texture")
            End Get
            Set(value As UInteger)
                PonerReferencia("Alternate Texture\New Texture", value)
            End Set
        End Property

        ''' <summary>Alternate Texture\3D Index</summary>
        Public Property AlternateTexture3DIndex As Integer
            Get
                Return CInt(Entero("Alternate Texture\3D Index"))
            End Get
            Set(value As Integer)
                Escribir("Alternate Texture\3D Index", CLng(value))
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Female Behavior Graph\Model\MODS\Alternate Textures.</summary>
    Public NotInheritable Class RaceSSE_AlternateTextures4
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Alternate Texture\New Texture  -&gt;  TXST. Referencia en el espacio del orden de carga.</summary>
        Public Property AlternateTextureNewTexture As UInteger
            Get
                Return Referencia("Alternate Texture\New Texture")
            End Get
            Set(value As UInteger)
                PonerReferencia("Alternate Texture\New Texture", value)
            End Set
        End Property

        ''' <summary>Alternate Texture\3D Index</summary>
        Public Property AlternateTexture3DIndex As Integer
            Get
                Return CInt(Entero("Alternate Texture\3D Index"))
            End Get
            Set(value As Integer)
                Escribir("Alternate Texture\3D Index", CLng(value))
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Biped Object Names.</summary>
    Public NotInheritable Class RaceSSE_BipedObjectNames
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>NAME\Name</summary>
        Public Property Name As String
            Get
                Return Txt("NAME\Name")
            End Get
            Set(value As String)
                Escribir("NAME\Name", value)
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Movement Types.</summary>
    Public NotInheritable Class RaceSSE_MovementTypes
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Movement Types\MTYP\Movement Type  -&gt;  MOVT / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property MovementTypesMovementType As UInteger
            Get
                Return Referencia("Movement Types\MTYP\Movement Type")
            End Get
            Set(value As UInteger)
                PonerReferencia("Movement Types\MTYP\Movement Type", value)
            End Set
        End Property

        ''' <summary>Movement Types\SPED\Override Values\Left - Walk</summary>
        Public Property OverrideValuesLeftWalk As Single
            Get
                Return Flt("Movement Types\SPED\Override Values\Left - Walk")
            End Get
            Set(value As Single)
                Escribir("Movement Types\SPED\Override Values\Left - Walk", value)
            End Set
        End Property

        ''' <summary>Movement Types\SPED\Override Values\Left - Run</summary>
        Public Property OverrideValuesLeftRun As Single
            Get
                Return Flt("Movement Types\SPED\Override Values\Left - Run")
            End Get
            Set(value As Single)
                Escribir("Movement Types\SPED\Override Values\Left - Run", value)
            End Set
        End Property

        ''' <summary>Movement Types\SPED\Override Values\Right - Walk</summary>
        Public Property OverrideValuesRightWalk As Single
            Get
                Return Flt("Movement Types\SPED\Override Values\Right - Walk")
            End Get
            Set(value As Single)
                Escribir("Movement Types\SPED\Override Values\Right - Walk", value)
            End Set
        End Property

        ''' <summary>Movement Types\SPED\Override Values\Right - Run</summary>
        Public Property OverrideValuesRightRun As Single
            Get
                Return Flt("Movement Types\SPED\Override Values\Right - Run")
            End Get
            Set(value As Single)
                Escribir("Movement Types\SPED\Override Values\Right - Run", value)
            End Set
        End Property

        ''' <summary>Movement Types\SPED\Override Values\Forward - Walk</summary>
        Public Property OverrideValuesForwardWalk As Single
            Get
                Return Flt("Movement Types\SPED\Override Values\Forward - Walk")
            End Get
            Set(value As Single)
                Escribir("Movement Types\SPED\Override Values\Forward - Walk", value)
            End Set
        End Property

        ''' <summary>Movement Types\SPED\Override Values\Forward - Run</summary>
        Public Property OverrideValuesForwardRun As Single
            Get
                Return Flt("Movement Types\SPED\Override Values\Forward - Run")
            End Get
            Set(value As Single)
                Escribir("Movement Types\SPED\Override Values\Forward - Run", value)
            End Set
        End Property

        ''' <summary>Movement Types\SPED\Override Values\Back - Walk</summary>
        Public Property OverrideValuesBackWalk As Single
            Get
                Return Flt("Movement Types\SPED\Override Values\Back - Walk")
            End Get
            Set(value As Single)
                Escribir("Movement Types\SPED\Override Values\Back - Walk", value)
            End Set
        End Property

        ''' <summary>Movement Types\SPED\Override Values\Back - Run</summary>
        Public Property OverrideValuesBackRun As Single
            Get
                Return Flt("Movement Types\SPED\Override Values\Back - Run")
            End Get
            Set(value As Single)
                Escribir("Movement Types\SPED\Override Values\Back - Run", value)
            End Set
        End Property

        ''' <summary>Movement Types\SPED\Override Values\Rotate - Walk</summary>
        Public Property OverrideValuesRotateWalk As Single
            Get
                Return Flt("Movement Types\SPED\Override Values\Rotate - Walk")
            End Get
            Set(value As Single)
                Escribir("Movement Types\SPED\Override Values\Rotate - Walk", value)
            End Set
        End Property

        ''' <summary>Movement Types\SPED\Override Values\Rotate - Walk</summary>
        Public Property OverrideValuesRotateWalk2 As Single
            Get
                Return Flt("Movement Types\SPED\Override Values\Rotate - Walk")
            End Get
            Set(value As Single)
                Escribir("Movement Types\SPED\Override Values\Rotate - Walk", value)
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Equip Slots.</summary>
    Public NotInheritable Class RaceSSE_EquipSlots
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>QNAM\Equip Slot  -&gt;  EQUP / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property EquipSlot As UInteger
            Get
                Return Referencia("QNAM\Equip Slot")
            End Get
            Set(value As UInteger)
                PonerReferencia("QNAM\Equip Slot", value)
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Phoneme Target Names.</summary>
    Public NotInheritable Class RaceSSE_PhonemeTargetNames
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>PHTN\Name</summary>
        Public Property Name As String
            Get
                Return Txt("PHTN\Name")
            End Get
            Set(value As String)
                Escribir("PHTN\Name", value)
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Head Data\Male Head Data\Head Parts.</summary>
    Public NotInheritable Class RaceSSE_HeadParts
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Head Part\INDX\Head Part Number</summary>
        Public Property HeadPartHeadPartNumber As UInteger
            Get
                Return CUInt(Entero("Head Part\INDX\Head Part Number"))
            End Get
            Set(value As UInteger)
                Escribir("Head Part\INDX\Head Part Number", CLng(value))
            End Set
        End Property

        ''' <summary>Head Part\HEAD\Head  -&gt;  HDPT / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property HeadPartHead As UInteger
            Get
                Return Referencia("Head Part\HEAD\Head")
            End Get
            Set(value As UInteger)
                PonerReferencia("Head Part\HEAD\Head", value)
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Head Data\Male Head Data\Race Presets Male.</summary>
    Public NotInheritable Class RaceSSE_RacePresetsMale
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>RPRM\Preset NPC  -&gt;  NPC_ / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property PresetNPC As UInteger
            Get
                Return Referencia("RPRM\Preset NPC")
            End Get
            Set(value As UInteger)
                PonerReferencia("RPRM\Preset NPC", value)
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Head Data\Male Head Data\Available Hair Colors Male.</summary>
    Public NotInheritable Class RaceSSE_AvailableHairColorsMale
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>AHCM\Hair Color  -&gt;  CLFM / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property HairColor As UInteger
            Get
                Return Referencia("AHCM\Hair Color")
            End Get
            Set(value As UInteger)
                PonerReferencia("AHCM\Hair Color", value)
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Head Data\Male Head Data\Face Details Texture Set List Male.</summary>
    Public NotInheritable Class RaceSSE_FaceDetailsTextureSetListMale
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>FTSM\Texture Set  -&gt;  TXST / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property TextureSet As UInteger
            Get
                Return Referencia("FTSM\Texture Set")
            End Get
            Set(value As UInteger)
                PonerReferencia("FTSM\Texture Set", value)
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Head Data\Male Head Data\Tint Masks.</summary>
    Public NotInheritable Class RaceSSE_TintMasks
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Tint Assets\Tint Layer\TINI\Index</summary>
        Public Property TintLayerIndex As UShort
            Get
                Return CUShort(Entero("Tint Assets\Tint Layer\TINI\Index"))
            End Get
            Set(value As UShort)
                Escribir("Tint Assets\Tint Layer\TINI\Index", CLng(value))
            End Set
        End Property

        ''' <summary>Tint Assets\Tint Layer\TINT\File Name</summary>
        Public Property TintLayerFileName As String
            Get
                Return Txt("Tint Assets\Tint Layer\TINT\File Name")
            End Get
            Set(value As String)
                Escribir("Tint Assets\Tint Layer\TINT\File Name", value)
            End Set
        End Property

        ''' <summary>Tint Assets\Tint Layer\TINP\Mask Type</summary>
        Public Property TintLayerMaskType As UShort
            Get
                Return CUShort(Entero("Tint Assets\Tint Layer\TINP\Mask Type"))
            End Get
            Set(value As UShort)
                Escribir("Tint Assets\Tint Layer\TINP\Mask Type", CLng(value))
            End Set
        End Property

        ''' <summary>Tint Assets\Tint Layer\TIND\Preset Default  -&gt;  CLFM / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property TintLayerPresetDefault As UInteger
            Get
                Return Referencia("Tint Assets\Tint Layer\TIND\Preset Default")
            End Get
            Set(value As UInteger)
                PonerReferencia("Tint Assets\Tint Layer\TIND\Preset Default", value)
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Head Data\Male Head Data\Tint Masks\Tint Assets\Presets.</summary>
    Public NotInheritable Class RaceSSE_Presets
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Preset\TINC\Color  -&gt;  CLFM / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property PresetColor As UInteger
            Get
                Return Referencia("Preset\TINC\Color")
            End Get
            Set(value As UInteger)
                PonerReferencia("Preset\TINC\Color", value)
            End Set
        End Property

        ''' <summary>Preset\TINV\Default Value</summary>
        Public Property PresetDefaultValue As Single
            Get
                Return Flt("Preset\TINV\Default Value")
            End Get
            Set(value As Single)
                Escribir("Preset\TINV\Default Value", value)
            End Set
        End Property

        ''' <summary>Preset\TIRS\Index</summary>
        Public Property PresetIndex As UShort
            Get
                Return CUShort(Entero("Preset\TIRS\Index"))
            End Get
            Set(value As UShort)
                Escribir("Preset\TIRS\Index", CLng(value))
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Head Data\Male Head Data\Model\MODS\Alternate Textures.</summary>
    Public NotInheritable Class RaceSSE_AlternateTextures5
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Alternate Texture\New Texture  -&gt;  TXST. Referencia en el espacio del orden de carga.</summary>
        Public Property AlternateTextureNewTexture As UInteger
            Get
                Return Referencia("Alternate Texture\New Texture")
            End Get
            Set(value As UInteger)
                PonerReferencia("Alternate Texture\New Texture", value)
            End Set
        End Property

        ''' <summary>Alternate Texture\3D Index</summary>
        Public Property AlternateTexture3DIndex As Integer
            Get
                Return CInt(Entero("Alternate Texture\3D Index"))
            End Get
            Set(value As Integer)
                Escribir("Alternate Texture\3D Index", CLng(value))
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Head Data\Female Head Data\Head Parts.</summary>
    Public NotInheritable Class RaceSSE_HeadParts2
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Head Part\INDX\Head Part Number</summary>
        Public Property HeadPartHeadPartNumber As UInteger
            Get
                Return CUInt(Entero("Head Part\INDX\Head Part Number"))
            End Get
            Set(value As UInteger)
                Escribir("Head Part\INDX\Head Part Number", CLng(value))
            End Set
        End Property

        ''' <summary>Head Part\HEAD\Head  -&gt;  HDPT / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property HeadPartHead As UInteger
            Get
                Return Referencia("Head Part\HEAD\Head")
            End Get
            Set(value As UInteger)
                PonerReferencia("Head Part\HEAD\Head", value)
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Head Data\Female Head Data\Race Presets Female.</summary>
    Public NotInheritable Class RaceSSE_RacePresetsFemale
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>RPRF\Preset NPC  -&gt;  NPC_ / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property PresetNPC As UInteger
            Get
                Return Referencia("RPRF\Preset NPC")
            End Get
            Set(value As UInteger)
                PonerReferencia("RPRF\Preset NPC", value)
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Head Data\Female Head Data\Available Hair Colors Female.</summary>
    Public NotInheritable Class RaceSSE_AvailableHairColorsFemale
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>AHCF\Hair Color  -&gt;  CLFM / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property HairColor As UInteger
            Get
                Return Referencia("AHCF\Hair Color")
            End Get
            Set(value As UInteger)
                PonerReferencia("AHCF\Hair Color", value)
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Head Data\Female Head Data\Face Details Texture Set List Female.</summary>
    Public NotInheritable Class RaceSSE_FaceDetailsTextureSetListFemale
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>FTSF\Texture Set  -&gt;  TXST / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property TextureSet As UInteger
            Get
                Return Referencia("FTSF\Texture Set")
            End Get
            Set(value As UInteger)
                PonerReferencia("FTSF\Texture Set", value)
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Head Data\Female Head Data\Tint Masks.</summary>
    Public NotInheritable Class RaceSSE_TintMasks2
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Tint Assets\Tint Layer\TINI\Index</summary>
        Public Property TintLayerIndex As UShort
            Get
                Return CUShort(Entero("Tint Assets\Tint Layer\TINI\Index"))
            End Get
            Set(value As UShort)
                Escribir("Tint Assets\Tint Layer\TINI\Index", CLng(value))
            End Set
        End Property

        ''' <summary>Tint Assets\Tint Layer\TINT\File Name</summary>
        Public Property TintLayerFileName As String
            Get
                Return Txt("Tint Assets\Tint Layer\TINT\File Name")
            End Get
            Set(value As String)
                Escribir("Tint Assets\Tint Layer\TINT\File Name", value)
            End Set
        End Property

        ''' <summary>Tint Assets\Tint Layer\TINP\Mask Type</summary>
        Public Property TintLayerMaskType As UShort
            Get
                Return CUShort(Entero("Tint Assets\Tint Layer\TINP\Mask Type"))
            End Get
            Set(value As UShort)
                Escribir("Tint Assets\Tint Layer\TINP\Mask Type", CLng(value))
            End Set
        End Property

        ''' <summary>Tint Assets\Tint Layer\TIND\Preset Default  -&gt;  CLFM / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property TintLayerPresetDefault As UInteger
            Get
                Return Referencia("Tint Assets\Tint Layer\TIND\Preset Default")
            End Get
            Set(value As UInteger)
                PonerReferencia("Tint Assets\Tint Layer\TIND\Preset Default", value)
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Head Data\Female Head Data\Tint Masks\Tint Assets\Presets.</summary>
    Public NotInheritable Class RaceSSE_Presets2
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Preset\TINC\Color  -&gt;  CLFM / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property PresetColor As UInteger
            Get
                Return Referencia("Preset\TINC\Color")
            End Get
            Set(value As UInteger)
                PonerReferencia("Preset\TINC\Color", value)
            End Set
        End Property

        ''' <summary>Preset\TINV\Default Value</summary>
        Public Property PresetDefaultValue As Single
            Get
                Return Flt("Preset\TINV\Default Value")
            End Get
            Set(value As Single)
                Escribir("Preset\TINV\Default Value", value)
            End Set
        End Property

        ''' <summary>Preset\TIRS\Index</summary>
        Public Property PresetIndex As UShort
            Get
                Return CUShort(Entero("Preset\TIRS\Index"))
            End Get
            Set(value As UShort)
                Escribir("Preset\TIRS\Index", CLng(value))
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Head Data\Female Head Data\Model\MODS\Alternate Textures.</summary>
    Public NotInheritable Class RaceSSE_AlternateTextures6
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Alternate Texture\New Texture  -&gt;  TXST. Referencia en el espacio del orden de carga.</summary>
        Public Property AlternateTextureNewTexture As UInteger
            Get
                Return Referencia("Alternate Texture\New Texture")
            End Get
            Set(value As UInteger)
                PonerReferencia("Alternate Texture\New Texture", value)
            End Set
        End Property

        ''' <summary>Alternate Texture\3D Index</summary>
        Public Property AlternateTexture3DIndex As Integer
            Get
                Return CInt(Entero("Alternate Texture\3D Index"))
            End Get
            Set(value As Integer)
                Escribir("Alternate Texture\3D Index", CLng(value))
            End Set
        End Property

    End Class

    ''' <summary>Campos de un record TXST de Skyrim.</summary>
    Public NotInheritable Class TxstSSE
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>EDID\Editor ID</summary>
        Public Property EditorID As String
            Get
                Return Txt("EDID\Editor ID")
            End Get
            Set(value As String)
                Escribir("EDID\Editor ID", value)
            End Set
        End Property

        ''' <summary>OBND\Object Bounds\X1</summary>
        Public Property ObjectBoundsX1 As Short
            Get
                Return CShort(Entero("OBND\Object Bounds\X1"))
            End Get
            Set(value As Short)
                Escribir("OBND\Object Bounds\X1", CLng(value))
            End Set
        End Property

        ''' <summary>OBND\Object Bounds\Y1</summary>
        Public Property ObjectBoundsY1 As Short
            Get
                Return CShort(Entero("OBND\Object Bounds\Y1"))
            End Get
            Set(value As Short)
                Escribir("OBND\Object Bounds\Y1", CLng(value))
            End Set
        End Property

        ''' <summary>OBND\Object Bounds\Z1</summary>
        Public Property ObjectBoundsZ1 As Short
            Get
                Return CShort(Entero("OBND\Object Bounds\Z1"))
            End Get
            Set(value As Short)
                Escribir("OBND\Object Bounds\Z1", CLng(value))
            End Set
        End Property

        ''' <summary>OBND\Object Bounds\X2</summary>
        Public Property ObjectBoundsX2 As Short
            Get
                Return CShort(Entero("OBND\Object Bounds\X2"))
            End Get
            Set(value As Short)
                Escribir("OBND\Object Bounds\X2", CLng(value))
            End Set
        End Property

        ''' <summary>OBND\Object Bounds\Y2</summary>
        Public Property ObjectBoundsY2 As Short
            Get
                Return CShort(Entero("OBND\Object Bounds\Y2"))
            End Get
            Set(value As Short)
                Escribir("OBND\Object Bounds\Y2", CLng(value))
            End Set
        End Property

        ''' <summary>OBND\Object Bounds\Z2</summary>
        Public Property ObjectBoundsZ2 As Short
            Get
                Return CShort(Entero("OBND\Object Bounds\Z2"))
            End Get
            Set(value As Short)
                Escribir("OBND\Object Bounds\Z2", CLng(value))
            End Set
        End Property

        ''' <summary>Textures (RGB/A)\TX00\Diffuse</summary>
        Public Property TexturesRGBADiffuse As String
            Get
                Return Txt("Textures (RGB/A)\TX00\Diffuse")
            End Get
            Set(value As String)
                Escribir("Textures (RGB/A)\TX00\Diffuse", value)
            End Set
        End Property

        ''' <summary>Textures (RGB/A)\TX01\Normal/Gloss</summary>
        Public Property TexturesRGBANormalGloss As String
            Get
                Return Txt("Textures (RGB/A)\TX01\Normal/Gloss")
            End Get
            Set(value As String)
                Escribir("Textures (RGB/A)\TX01\Normal/Gloss", value)
            End Set
        End Property

        ''' <summary>Textures (RGB/A)\TX02\Environment Mask/Subsurface Tint</summary>
        Public Property TexturesRGBAEnvironmentMaskSubsurfaceTint As String
            Get
                Return Txt("Textures (RGB/A)\TX02\Environment Mask/Subsurface Tint")
            End Get
            Set(value As String)
                Escribir("Textures (RGB/A)\TX02\Environment Mask/Subsurface Tint", value)
            End Set
        End Property

        ''' <summary>Textures (RGB/A)\TX03\Glow/Detail Map</summary>
        Public Property TexturesRGBAGlowDetailMap As String
            Get
                Return Txt("Textures (RGB/A)\TX03\Glow/Detail Map")
            End Get
            Set(value As String)
                Escribir("Textures (RGB/A)\TX03\Glow/Detail Map", value)
            End Set
        End Property

        ''' <summary>Textures (RGB/A)\TX04\Height</summary>
        Public Property TexturesRGBAHeight As String
            Get
                Return Txt("Textures (RGB/A)\TX04\Height")
            End Get
            Set(value As String)
                Escribir("Textures (RGB/A)\TX04\Height", value)
            End Set
        End Property

        ''' <summary>Textures (RGB/A)\TX05\Environment</summary>
        Public Property TexturesRGBAEnvironment As String
            Get
                Return Txt("Textures (RGB/A)\TX05\Environment")
            End Get
            Set(value As String)
                Escribir("Textures (RGB/A)\TX05\Environment", value)
            End Set
        End Property

        ''' <summary>Textures (RGB/A)\TX06\Multilayer</summary>
        Public Property TexturesRGBAMultilayer As String
            Get
                Return Txt("Textures (RGB/A)\TX06\Multilayer")
            End Get
            Set(value As String)
                Escribir("Textures (RGB/A)\TX06\Multilayer", value)
            End Set
        End Property

        ''' <summary>Textures (RGB/A)\TX07\Backlight Mask/Specular</summary>
        Public Property TexturesRGBABacklightMaskSpecular As String
            Get
                Return Txt("Textures (RGB/A)\TX07\Backlight Mask/Specular")
            End Get
            Set(value As String)
                Escribir("Textures (RGB/A)\TX07\Backlight Mask/Specular", value)
            End Set
        End Property

        ''' <summary>DODT\Decal Data\Min Width</summary>
        Public Property DecalDataMinWidth As Single
            Get
                Return Flt("DODT\Decal Data\Min Width")
            End Get
            Set(value As Single)
                Escribir("DODT\Decal Data\Min Width", value)
            End Set
        End Property

        ''' <summary>DODT\Decal Data\Max Width</summary>
        Public Property DecalDataMaxWidth As Single
            Get
                Return Flt("DODT\Decal Data\Max Width")
            End Get
            Set(value As Single)
                Escribir("DODT\Decal Data\Max Width", value)
            End Set
        End Property

        ''' <summary>DODT\Decal Data\Min Height</summary>
        Public Property DecalDataMinHeight As Single
            Get
                Return Flt("DODT\Decal Data\Min Height")
            End Get
            Set(value As Single)
                Escribir("DODT\Decal Data\Min Height", value)
            End Set
        End Property

        ''' <summary>DODT\Decal Data\Max Height</summary>
        Public Property DecalDataMaxHeight As Single
            Get
                Return Flt("DODT\Decal Data\Max Height")
            End Get
            Set(value As Single)
                Escribir("DODT\Decal Data\Max Height", value)
            End Set
        End Property

        ''' <summary>DODT\Decal Data\Depth</summary>
        Public Property DecalDataDepth As Single
            Get
                Return Flt("DODT\Decal Data\Depth")
            End Get
            Set(value As Single)
                Escribir("DODT\Decal Data\Depth", value)
            End Set
        End Property

        ''' <summary>DODT\Decal Data\Shininess</summary>
        Public Property DecalDataShininess As Single
            Get
                Return Flt("DODT\Decal Data\Shininess")
            End Get
            Set(value As Single)
                Escribir("DODT\Decal Data\Shininess", value)
            End Set
        End Property

        ''' <summary>DODT\Decal Data\Parallax\Scale</summary>
        Public Property ParallaxScale As Single
            Get
                Return Flt("DODT\Decal Data\Parallax\Scale")
            End Get
            Set(value As Single)
                Escribir("DODT\Decal Data\Parallax\Scale", value)
            End Set
        End Property

        ''' <summary>DODT\Decal Data\Parallax\Passes</summary>
        Public Property ParallaxPasses As Byte
            Get
                Return CByte(Entero("DODT\Decal Data\Parallax\Passes"))
            End Get
            Set(value As Byte)
                Escribir("DODT\Decal Data\Parallax\Passes", CLng(value))
            End Set
        End Property

        ''' <summary>DODT\Decal Data\Flags</summary>
        Public Property DecalDataFlags As Byte
            Get
                Return CByte(Entero("DODT\Decal Data\Flags"))
            End Get
            Set(value As Byte)
                Escribir("DODT\Decal Data\Flags", CLng(value))
            End Set
        End Property

        ''' <summary>Bit 0 de DODT\Decal Data\Flags: Parallax</summary>
        Public Property DecalDataFlagsParallax As Boolean
            Get
                Return Bit("DODT\Decal Data\Flags", 0)
            End Get
            Set(value As Boolean)
                PonerBit("DODT\Decal Data\Flags", 0, value)
            End Set
        End Property
        ''' <summary>Bit 1 de DODT\Decal Data\Flags: Alpha - Blending</summary>
        Public Property DecalDataFlagsAlphaBlending As Boolean
            Get
                Return Bit("DODT\Decal Data\Flags", 1)
            End Get
            Set(value As Boolean)
                PonerBit("DODT\Decal Data\Flags", 1, value)
            End Set
        End Property
        ''' <summary>Bit 2 de DODT\Decal Data\Flags: Alpha - Testing</summary>
        Public Property DecalDataFlagsAlphaTesting As Boolean
            Get
                Return Bit("DODT\Decal Data\Flags", 2)
            End Get
            Set(value As Boolean)
                PonerBit("DODT\Decal Data\Flags", 2, value)
            End Set
        End Property
        ''' <summary>Bit 3 de DODT\Decal Data\Flags: No Subtextures</summary>
        Public Property DecalDataFlagsNoSubtextures As Boolean
            Get
                Return Bit("DODT\Decal Data\Flags", 3)
            End Get
            Set(value As Boolean)
                PonerBit("DODT\Decal Data\Flags", 3, value)
            End Set
        End Property

        ''' <summary>DODT\Decal Data\Color\Red</summary>
        Public Property ColorRed As Byte
            Get
                Return CByte(Entero("DODT\Decal Data\Color\Red"))
            End Get
            Set(value As Byte)
                Escribir("DODT\Decal Data\Color\Red", CLng(value))
            End Set
        End Property

        ''' <summary>DODT\Decal Data\Color\Green</summary>
        Public Property ColorGreen As Byte
            Get
                Return CByte(Entero("DODT\Decal Data\Color\Green"))
            End Get
            Set(value As Byte)
                Escribir("DODT\Decal Data\Color\Green", CLng(value))
            End Set
        End Property

        ''' <summary>DODT\Decal Data\Color\Blue</summary>
        Public Property ColorBlue As Byte
            Get
                Return CByte(Entero("DODT\Decal Data\Color\Blue"))
            End Get
            Set(value As Byte)
                Escribir("DODT\Decal Data\Color\Blue", CLng(value))
            End Set
        End Property

        ''' <summary>DNAM\Flags</summary>
        Public Property Flags As UShort
            Get
                Return CUShort(Entero("DNAM\Flags"))
            End Get
            Set(value As UShort)
                Escribir("DNAM\Flags", CLng(value))
            End Set
        End Property

    End Class

End Namespace
