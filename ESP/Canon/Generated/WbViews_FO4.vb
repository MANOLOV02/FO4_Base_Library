' ============================================================================================
' ARCHIVO GENERADO — NO EDITAR A MANO.  Regenerar: Tools/CanonViewGen
'
' Una propiedad por campo de cada tipo de record de Fallout 4. El nombre de la
' propiedad ES el nombre del campo en el formato: no hay ninguna tabla de
' equivalencias que mantener, y si el formato cambia un campo el codigo que lo
' usaba deja de compilar.
' ============================================================================================

Namespace Canon

    ''' <summary>Campos de un record ARMA de Fallout 4.</summary>
    Public NotInheritable Class ArmaFO4
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

        ''' <summary>Biped Model\Male\MO2C\Color Remapping Index</summary>
        Public Property MaleColorRemappingIndex As Single
            Get
                Return Flt("Biped Model\Male\MO2C\Color Remapping Index")
            End Get
            Set(value As Single)
                Escribir("Biped Model\Male\MO2C\Color Remapping Index", value)
            End Set
        End Property

        ''' <summary>Biped Model\Male\MO2S\Material Swap  -&gt;  MSWP. Referencia en el espacio del orden de carga.</summary>
        Public Property MaleMaterialSwap As UInteger
            Get
                Return Referencia("Biped Model\Male\MO2S\Material Swap")
            End Get
            Set(value As UInteger)
                PonerReferencia("Biped Model\Male\MO2S\Material Swap", value)
            End Set
        End Property

        ''' <summary>Biped Model\Male\MO2F\Flags</summary>
        Public Property MaleFlags As Byte
            Get
                Return CByte(Entero("Biped Model\Male\MO2F\Flags"))
            End Get
            Set(value As Byte)
                Escribir("Biped Model\Male\MO2F\Flags", CLng(value))
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

        ''' <summary>Biped Model\Female\MO3C\Color Remapping Index</summary>
        Public Property FemaleColorRemappingIndex As Single
            Get
                Return Flt("Biped Model\Female\MO3C\Color Remapping Index")
            End Get
            Set(value As Single)
                Escribir("Biped Model\Female\MO3C\Color Remapping Index", value)
            End Set
        End Property

        ''' <summary>Biped Model\Female\MO3S\Material Swap  -&gt;  MSWP. Referencia en el espacio del orden de carga.</summary>
        Public Property FemaleMaterialSwap As UInteger
            Get
                Return Referencia("Biped Model\Female\MO3S\Material Swap")
            End Get
            Set(value As UInteger)
                PonerReferencia("Biped Model\Female\MO3S\Material Swap", value)
            End Set
        End Property

        ''' <summary>Biped Model\Female\MO3F\Flags</summary>
        Public Property FemaleFlags As Byte
            Get
                Return CByte(Entero("Biped Model\Female\MO3F\Flags"))
            End Get
            Set(value As Byte)
                Escribir("Biped Model\Female\MO3F\Flags", CLng(value))
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

        ''' <summary>1st Person\Male\MO4C\Color Remapping Index</summary>
        Public Property MaleColorRemappingIndex2 As Single
            Get
                Return Flt("1st Person\Male\MO4C\Color Remapping Index")
            End Get
            Set(value As Single)
                Escribir("1st Person\Male\MO4C\Color Remapping Index", value)
            End Set
        End Property

        ''' <summary>1st Person\Male\MO4S\Material Swap  -&gt;  MSWP. Referencia en el espacio del orden de carga.</summary>
        Public Property MaleMaterialSwap2 As UInteger
            Get
                Return Referencia("1st Person\Male\MO4S\Material Swap")
            End Get
            Set(value As UInteger)
                PonerReferencia("1st Person\Male\MO4S\Material Swap", value)
            End Set
        End Property

        ''' <summary>1st Person\Male\MO4F\Flags</summary>
        Public Property MaleFlags2 As Byte
            Get
                Return CByte(Entero("1st Person\Male\MO4F\Flags"))
            End Get
            Set(value As Byte)
                Escribir("1st Person\Male\MO4F\Flags", CLng(value))
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

        ''' <summary>1st Person\Female\MO5C\Color Remapping Index</summary>
        Public Property FemaleColorRemappingIndex2 As Single
            Get
                Return Flt("1st Person\Female\MO5C\Color Remapping Index")
            End Get
            Set(value As Single)
                Escribir("1st Person\Female\MO5C\Color Remapping Index", value)
            End Set
        End Property

        ''' <summary>1st Person\Female\MO5S\Material Swap  -&gt;  MSWP. Referencia en el espacio del orden de carga.</summary>
        Public Property FemaleMaterialSwap2 As UInteger
            Get
                Return Referencia("1st Person\Female\MO5S\Material Swap")
            End Get
            Set(value As UInteger)
                PonerReferencia("1st Person\Female\MO5S\Material Swap", value)
            End Set
        End Property

        ''' <summary>1st Person\Female\MO5F\Flags</summary>
        Public Property FemaleFlags2 As Byte
            Get
                Return CByte(Entero("1st Person\Female\MO5F\Flags"))
            End Get
            Set(value As Byte)
                Escribir("1st Person\Female\MO5F\Flags", CLng(value))
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

        ''' <summary>NAM1\Female Skin Texture  -&gt;  TXST / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property FemaleSkinTexture As UInteger
            Get
                Return Referencia("NAM1\Female Skin Texture")
            End Get
            Set(value As UInteger)
                PonerReferencia("NAM1\Female Skin Texture", value)
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

        ''' <summary>Additional Races</summary>
        Public ReadOnly Property AdditionalRaces As IReadOnlyList(Of ArmaFO4_AdditionalRaces)
            Get
                Return Elementos(Of ArmaFO4_AdditionalRaces)("Additional Races", Function(n) New ArmaFO4_AdditionalRaces(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Sculpt Data</summary>
        Public ReadOnly Property SculptData As IReadOnlyList(Of ArmaFO4_SculptData)
            Get
                Return Elementos(Of ArmaFO4_SculptData)("Sculpt Data", Function(n) New ArmaFO4_SculptData(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Sculpt Data\Bone Scale Modifier Set\Bone Scale Modifiers</summary>
        Public ReadOnly Property BoneScaleModifiers As IReadOnlyList(Of ArmaFO4_BoneScaleModifiers)
            Get
                Return Elementos(Of ArmaFO4_BoneScaleModifiers)("Sculpt Data\Bone Scale Modifier Set\Bone Scale Modifiers", Function(n) New ArmaFO4_BoneScaleModifiers(n, Context, Resolver))
            End Get
        End Property

    End Class

    ''' <summary>Un elemento de Additional Races.</summary>
    Public NotInheritable Class ArmaFO4_AdditionalRaces
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

    ''' <summary>Un elemento de Sculpt Data.</summary>
    Public NotInheritable Class ArmaFO4_SculptData
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Bone Scale Modifier Set\BSMP\Target Gender</summary>
        Public Property BoneScaleModifierSetTargetGender As UInteger
            Get
                Return CUInt(Entero("Bone Scale Modifier Set\BSMP\Target Gender"))
            End Get
            Set(value As UInteger)
                Escribir("Bone Scale Modifier Set\BSMP\Target Gender", CLng(value))
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Sculpt Data\Bone Scale Modifier Set\Bone Scale Modifiers.</summary>
    Public NotInheritable Class ArmaFO4_BoneScaleModifiers
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Bone Scale Modifier\BSMB\Bone Name</summary>
        Public Property BoneScaleModifierBoneName As String
            Get
                Return Txt("Bone Scale Modifier\BSMB\Bone Name")
            End Get
            Set(value As String)
                Escribir("Bone Scale Modifier\BSMB\Bone Name", value)
            End Set
        End Property

        ''' <summary>Bone Scale Modifier\BSMS\Bone Scale Delta\X</summary>
        Public Property BoneScaleDeltaX As Single
            Get
                Return Flt("Bone Scale Modifier\BSMS\Bone Scale Delta\X")
            End Get
            Set(value As Single)
                Escribir("Bone Scale Modifier\BSMS\Bone Scale Delta\X", value)
            End Set
        End Property

        ''' <summary>Bone Scale Modifier\BSMS\Bone Scale Delta\Y</summary>
        Public Property BoneScaleDeltaY As Single
            Get
                Return Flt("Bone Scale Modifier\BSMS\Bone Scale Delta\Y")
            End Get
            Set(value As Single)
                Escribir("Bone Scale Modifier\BSMS\Bone Scale Delta\Y", value)
            End Set
        End Property

        ''' <summary>Bone Scale Modifier\BSMS\Bone Scale Delta\Z</summary>
        Public Property BoneScaleDeltaZ As Single
            Get
                Return Flt("Bone Scale Modifier\BSMS\Bone Scale Delta\Z")
            End Get
            Set(value As Single)
                Escribir("Bone Scale Modifier\BSMS\Bone Scale Delta\Z", value)
            End Set
        End Property

    End Class

    ''' <summary>Campos de un record ARMO de Fallout 4.</summary>
    Public NotInheritable Class ArmoFO4
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

        ''' <summary>PTRN\Preview Transform  -&gt;  TRNS. Referencia en el espacio del orden de carga.</summary>
        Public Property PreviewTransform As UInteger
            Get
                Return Referencia("PTRN\Preview Transform")
            End Get
            Set(value As UInteger)
                PonerReferencia("PTRN\Preview Transform", value)
            End Set
        End Property

        ''' <summary>FULL\Name. Resuelto contra las tablas de texto si el archivo las usa.</summary>
        Public Property Name As String
            Get
                Return TextoTraducible("FULL\Name")
            End Get
            Set(value As String)
                EscribirTextoTraducible("FULL\Name", value)
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

        ''' <summary>Male\World Model\MODC\Color Remapping Index</summary>
        Public Property WorldModelColorRemappingIndex As Single
            Get
                Return Flt("Male\World Model\MODC\Color Remapping Index")
            End Get
            Set(value As Single)
                Escribir("Male\World Model\MODC\Color Remapping Index", value)
            End Set
        End Property

        ''' <summary>Male\World Model\MO2S\Material Swap  -&gt;  MSWP. Referencia en el espacio del orden de carga.</summary>
        Public Property WorldModelMaterialSwap As UInteger
            Get
                Return Referencia("Male\World Model\MO2S\Material Swap")
            End Get
            Set(value As UInteger)
                PonerReferencia("Male\World Model\MO2S\Material Swap", value)
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

        ''' <summary>Female\World Model\MODC\Color Remapping Index</summary>
        Public Property WorldModelColorRemappingIndex2 As Single
            Get
                Return Flt("Female\World Model\MODC\Color Remapping Index")
            End Get
            Set(value As Single)
                Escribir("Female\World Model\MODC\Color Remapping Index", value)
            End Set
        End Property

        ''' <summary>Female\World Model\MO4S\Material Swap  -&gt;  MSWP. Referencia en el espacio del orden de carga.</summary>
        Public Property WorldModelMaterialSwap2 As UInteger
            Get
                Return Referencia("Female\World Model\MO4S\Material Swap")
            End Get
            Set(value As UInteger)
                PonerReferencia("Female\World Model\MO4S\Material Swap", value)
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

        ''' <summary>Destructible\DEST\Header\Flags</summary>
        Public Property HeaderFlags As Byte
            Get
                Return CByte(Entero("Destructible\DEST\Header\Flags"))
            End Get
            Set(value As Byte)
                Escribir("Destructible\DEST\Header\Flags", CLng(value))
            End Set
        End Property

        ''' <summary>Bit 0 de Destructible\DEST\Header\Flags: VATS Targetable</summary>
        Public Property HeaderFlagsVATSTargetable As Boolean
            Get
                Return Bit("Destructible\DEST\Header\Flags", 0)
            End Get
            Set(value As Boolean)
                PonerBit("Destructible\DEST\Header\Flags", 0, value)
            End Set
        End Property
        ''' <summary>Bit 1 de Destructible\DEST\Header\Flags: Large Actor Destroys</summary>
        Public Property HeaderFlagsLargeActorDestroys As Boolean
            Get
                Return Bit("Destructible\DEST\Header\Flags", 1)
            End Get
            Set(value As Boolean)
                PonerBit("Destructible\DEST\Header\Flags", 1, value)
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

        ''' <summary>ETYP\Equipment Type  -&gt;  EQUP / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property EquipmentType As UInteger
            Get
                Return Referencia("ETYP\Equipment Type")
            End Get
            Set(value As UInteger)
                PonerReferencia("ETYP\Equipment Type", value)
            End Set
        End Property

        ''' <summary>BIDS\Block Bash Impact Data Set  -&gt;  IPDS / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property BlockBashImpactDataSet As UInteger
            Get
                Return Referencia("BIDS\Block Bash Impact Data Set")
            End Get
            Set(value As UInteger)
                PonerReferencia("BIDS\Block Bash Impact Data Set", value)
            End Set
        End Property

        ''' <summary>BAMT\Alternate Block Material  -&gt;  MATT / NULL. Referencia en el espacio del orden de carga.</summary>
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

        ''' <summary>DESC\Description. Resuelto contra las tablas de texto si el archivo las usa.</summary>
        Public Property Description As String
            Get
                Return TextoTraducible("DESC\Description")
            End Get
            Set(value As String)
                EscribirTextoTraducible("DESC\Description", value)
            End Set
        End Property

        ''' <summary>INRD\Instance Naming  -&gt;  INNR. Referencia en el espacio del orden de carga.</summary>
        Public Property InstanceNaming As UInteger
            Get
                Return Referencia("INRD\Instance Naming")
            End Get
            Set(value As UInteger)
                PonerReferencia("INRD\Instance Naming", value)
            End Set
        End Property

        ''' <summary>DATA\Value</summary>
        Public Property Value As Integer
            Get
                Return CInt(Entero("DATA\Value"))
            End Get
            Set(value As Integer)
                Escribir("DATA\Value", CLng(value))
            End Set
        End Property

        ''' <summary>DATA\Weight</summary>
        Public Property Weight As Single
            Get
                Return Flt("DATA\Weight")
            End Get
            Set(value As Single)
                Escribir("DATA\Weight", value)
            End Set
        End Property

        ''' <summary>DATA\Health</summary>
        Public Property Health As UInteger
            Get
                Return CUInt(Entero("DATA\Health"))
            End Get
            Set(value As UInteger)
                Escribir("DATA\Health", CLng(value))
            End Set
        End Property

        ''' <summary>FNAM\Armor Rating</summary>
        Public Property ArmorRating As UShort
            Get
                Return CUShort(Entero("FNAM\Armor Rating"))
            End Get
            Set(value As UShort)
                Escribir("FNAM\Armor Rating", CLng(value))
            End Set
        End Property

        ''' <summary>FNAM\Base Addon Index</summary>
        Public Property BaseAddonIndex As UShort
            Get
                Return CUShort(Entero("FNAM\Base Addon Index"))
            End Get
            Set(value As UShort)
                Escribir("FNAM\Base Addon Index", CLng(value))
            End Set
        End Property

        ''' <summary>FNAM\Stagger Rating</summary>
        Public Property StaggerRating As Byte
            Get
                Return CByte(Entero("FNAM\Stagger Rating"))
            End Get
            Set(value As Byte)
                Escribir("FNAM\Stagger Rating", CLng(value))
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

        ''' <summary>Object Template\OBTE\Count</summary>
        Public Property ObjectTemplateCount As UInteger
            Get
                Return CUInt(Entero("Object Template\OBTE\Count"))
            End Get
            Set(value As UInteger)
                Escribir("Object Template\OBTE\Count", CLng(value))
            End Set
        End Property

        ''' <summary>VMAD\Virtual Machine Adapter\Scripts</summary>
        Public ReadOnly Property Scripts As IReadOnlyList(Of ArmoFO4_Scripts)
            Get
                Return Elementos(Of ArmoFO4_Scripts)("VMAD\Virtual Machine Adapter\Scripts", Function(n) New ArmoFO4_Scripts(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>VMAD\Virtual Machine Adapter\Scripts\Script\Properties</summary>
        Public ReadOnly Property Properties As IReadOnlyList(Of ArmoFO4_Properties)
            Get
                Return Elementos(Of ArmoFO4_Properties)("VMAD\Virtual Machine Adapter\Scripts\Script\Properties", Function(n) New ArmoFO4_Properties(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Destructible\DAMC\Resistances</summary>
        Public ReadOnly Property Resistances As IReadOnlyList(Of ArmoFO4_Resistances)
            Get
                Return Elementos(Of ArmoFO4_Resistances)("Destructible\DAMC\Resistances", Function(n) New ArmoFO4_Resistances(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Destructible\Stages</summary>
        Public ReadOnly Property Stages As IReadOnlyList(Of ArmoFO4_Stages)
            Get
                Return Elementos(Of ArmoFO4_Stages)("Destructible\Stages", Function(n) New ArmoFO4_Stages(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Keywords\KWDA\Keywords</summary>
        Public ReadOnly Property Keywords As IReadOnlyList(Of ArmoFO4_Keywords)
            Get
                Return Elementos(Of ArmoFO4_Keywords)("Keywords\KWDA\Keywords", Function(n) New ArmoFO4_Keywords(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Models</summary>
        Public ReadOnly Property Models As IReadOnlyList(Of ArmoFO4_Models)
            Get
                Return Elementos(Of ArmoFO4_Models)("Models", Function(n) New ArmoFO4_Models(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>DAMA</summary>
        Public ReadOnly Property DAMA As IReadOnlyList(Of ArmoFO4_DAMA)
            Get
                Return Elementos(Of ArmoFO4_DAMA)("DAMA", Function(n) New ArmoFO4_DAMA(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>APPR\Attach Parent Slots</summary>
        Public ReadOnly Property AttachParentSlots As IReadOnlyList(Of ArmoFO4_AttachParentSlots)
            Get
                Return Elementos(Of ArmoFO4_AttachParentSlots)("APPR\Attach Parent Slots", Function(n) New ArmoFO4_AttachParentSlots(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Object Template\Combinations</summary>
        Public ReadOnly Property Combinations As IReadOnlyList(Of ArmoFO4_Combinations)
            Get
                Return Elementos(Of ArmoFO4_Combinations)("Object Template\Combinations", Function(n) New ArmoFO4_Combinations(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Object Template\Combinations\Combination\OBTS\Object Mod Template Item\Keywords</summary>
        Public ReadOnly Property Keywords2 As IReadOnlyList(Of ArmoFO4_Keywords2)
            Get
                Return Elementos(Of ArmoFO4_Keywords2)("Object Template\Combinations\Combination\OBTS\Object Mod Template Item\Keywords", Function(n) New ArmoFO4_Keywords2(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Object Template\Combinations\Combination\OBTS\Object Mod Template Item\Includes</summary>
        Public ReadOnly Property Includes As IReadOnlyList(Of ArmoFO4_Includes)
            Get
                Return Elementos(Of ArmoFO4_Includes)("Object Template\Combinations\Combination\OBTS\Object Mod Template Item\Includes", Function(n) New ArmoFO4_Includes(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Object Template\Combinations\Combination\OBTS\Object Mod Template Item\Properties</summary>
        Public ReadOnly Property Properties2 As IReadOnlyList(Of ArmoFO4_Properties2)
            Get
                Return Elementos(Of ArmoFO4_Properties2)("Object Template\Combinations\Combination\OBTS\Object Mod Template Item\Properties", Function(n) New ArmoFO4_Properties2(n, Context, Resolver))
            End Get
        End Property

    End Class

    ''' <summary>Un elemento de VMAD\Virtual Machine Adapter\Scripts.</summary>
    Public NotInheritable Class ArmoFO4_Scripts
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Script\ScriptName</summary>
        Public Property ScriptScriptName As String
            Get
                Return Txt("Script\ScriptName")
            End Get
            Set(value As String)
                Escribir("Script\ScriptName", value)
            End Set
        End Property

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
    Public NotInheritable Class ArmoFO4_Properties
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Property\propertyName</summary>
        Public Property PropertyPropertyName As String
            Get
                Return Txt("Property\propertyName")
            End Get
            Set(value As String)
                Escribir("Property\propertyName", value)
            End Set
        End Property

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

    ''' <summary>Un elemento de Destructible\DAMC\Resistances.</summary>
    Public NotInheritable Class ArmoFO4_Resistances
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Resistance\Damage Type  -&gt;  DMGT. Referencia en el espacio del orden de carga.</summary>
        Public Property ResistanceDamageType As UInteger
            Get
                Return Referencia("Resistance\Damage Type")
            End Get
            Set(value As UInteger)
                PonerReferencia("Resistance\Damage Type", value)
            End Set
        End Property

        ''' <summary>Resistance\Value</summary>
        Public Property ResistanceValue As UInteger
            Get
                Return CUInt(Entero("Resistance\Value"))
            End Get
            Set(value As UInteger)
                Escribir("Resistance\Value", CLng(value))
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Destructible\Stages.</summary>
    Public NotInheritable Class ArmoFO4_Stages
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
        ''' <summary>Bit 4 de Stage\DSTD\Destruction Stage Data\Flags: Becomes Dynamic</summary>
        Public Property DestructionStageDataFlagsBecomesDynamic As Boolean
            Get
                Return Bit("Stage\DSTD\Destruction Stage Data\Flags", 4)
            End Get
            Set(value As Boolean)
                PonerBit("Stage\DSTD\Destruction Stage Data\Flags", 4, value)
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

        ''' <summary>Stage\DSTA\Sequence Name</summary>
        Public Property StageSequenceName As String
            Get
                Return Txt("Stage\DSTA\Sequence Name")
            End Get
            Set(value As String)
                Escribir("Stage\DSTA\Sequence Name", value)
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

        ''' <summary>Stage\Model\DMDC\Color Remapping Index</summary>
        Public Property ModelColorRemappingIndex As Single
            Get
                Return Flt("Stage\Model\DMDC\Color Remapping Index")
            End Get
            Set(value As Single)
                Escribir("Stage\Model\DMDC\Color Remapping Index", value)
            End Set
        End Property

        ''' <summary>Stage\Model\DMDS\Material Swap  -&gt;  MSWP. Referencia en el espacio del orden de carga.</summary>
        Public Property ModelMaterialSwap As UInteger
            Get
                Return Referencia("Stage\Model\DMDS\Material Swap")
            End Get
            Set(value As UInteger)
                PonerReferencia("Stage\Model\DMDS\Material Swap", value)
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Keywords\KWDA\Keywords.</summary>
    Public NotInheritable Class ArmoFO4_Keywords
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

    ''' <summary>Un elemento de Models.</summary>
    Public NotInheritable Class ArmoFO4_Models
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Model\INDX\Addon Index</summary>
        Public Property ModelAddonIndex As UShort
            Get
                Return CUShort(Entero("Model\INDX\Addon Index"))
            End Get
            Set(value As UShort)
                Escribir("Model\INDX\Addon Index", CLng(value))
            End Set
        End Property

        ''' <summary>Model\MODL\Armor Addon  -&gt;  ARMA. Referencia en el espacio del orden de carga.</summary>
        Public Property ModelArmorAddon As UInteger
            Get
                Return Referencia("Model\MODL\Armor Addon")
            End Get
            Set(value As UInteger)
                PonerReferencia("Model\MODL\Armor Addon", value)
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de DAMA.</summary>
    Public NotInheritable Class ArmoFO4_DAMA
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Resistance\Type  -&gt;  DMGT. Referencia en el espacio del orden de carga.</summary>
        Public Property ResistanceType As UInteger
            Get
                Return Referencia("Resistance\Type")
            End Get
            Set(value As UInteger)
                PonerReferencia("Resistance\Type", value)
            End Set
        End Property

        ''' <summary>Resistance\Amount</summary>
        Public Property ResistanceAmount As UInteger
            Get
                Return CUInt(Entero("Resistance\Amount"))
            End Get
            Set(value As UInteger)
                Escribir("Resistance\Amount", CLng(value))
            End Set
        End Property

        ''' <summary>Resistance\Curve Table  -&gt;  CURV / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property ResistanceCurveTable As UInteger
            Get
                Return Referencia("Resistance\Curve Table")
            End Get
            Set(value As UInteger)
                PonerReferencia("Resistance\Curve Table", value)
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de APPR\Attach Parent Slots.</summary>
    Public NotInheritable Class ArmoFO4_AttachParentSlots
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Keyword  -&gt;  KYWD. Referencia en el espacio del orden de carga.</summary>
        Public Property Keyword As UInteger
            Get
                Return Referencia("Keyword")
            End Get
            Set(value As UInteger)
                PonerReferencia("Keyword", value)
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Object Template\Combinations.</summary>
    Public NotInheritable Class ArmoFO4_Combinations
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Combination\FULL\Name. Resuelto contra las tablas de texto si el archivo las usa.</summary>
        Public Property CombinationName As String
            Get
                Return TextoTraducible("Combination\FULL\Name")
            End Get
            Set(value As String)
                EscribirTextoTraducible("Combination\FULL\Name", value)
            End Set
        End Property

        ''' <summary>Combination\OBTS\Object Mod Template Item\Include Count</summary>
        Public Property ObjectModTemplateItemIncludeCount As UInteger
            Get
                Return CUInt(Entero("Combination\OBTS\Object Mod Template Item\Include Count"))
            End Get
            Set(value As UInteger)
                Escribir("Combination\OBTS\Object Mod Template Item\Include Count", CLng(value))
            End Set
        End Property

        ''' <summary>Combination\OBTS\Object Mod Template Item\Property Count</summary>
        Public Property ObjectModTemplateItemPropertyCount As UInteger
            Get
                Return CUInt(Entero("Combination\OBTS\Object Mod Template Item\Property Count"))
            End Get
            Set(value As UInteger)
                Escribir("Combination\OBTS\Object Mod Template Item\Property Count", CLng(value))
            End Set
        End Property

        ''' <summary>Combination\OBTS\Object Mod Template Item\Level Min</summary>
        Public Property ObjectModTemplateItemLevelMin As Byte
            Get
                Return CByte(Entero("Combination\OBTS\Object Mod Template Item\Level Min"))
            End Get
            Set(value As Byte)
                Escribir("Combination\OBTS\Object Mod Template Item\Level Min", CLng(value))
            End Set
        End Property

        ''' <summary>Combination\OBTS\Object Mod Template Item\Level Max</summary>
        Public Property ObjectModTemplateItemLevelMax As Byte
            Get
                Return CByte(Entero("Combination\OBTS\Object Mod Template Item\Level Max"))
            End Get
            Set(value As Byte)
                Escribir("Combination\OBTS\Object Mod Template Item\Level Max", CLng(value))
            End Set
        End Property

        ''' <summary>Combination\OBTS\Object Mod Template Item\Parent Combination Index</summary>
        Public Property ObjectModTemplateItemParentCombinationIndex As Short
            Get
                Return CShort(Entero("Combination\OBTS\Object Mod Template Item\Parent Combination Index"))
            End Get
            Set(value As Short)
                Escribir("Combination\OBTS\Object Mod Template Item\Parent Combination Index", CLng(value))
            End Set
        End Property

        ''' <summary>Combination\OBTS\Object Mod Template Item\Default</summary>
        Public Property ObjectModTemplateItemDefault As Byte
            Get
                Return CByte(Entero("Combination\OBTS\Object Mod Template Item\Default"))
            End Get
            Set(value As Byte)
                Escribir("Combination\OBTS\Object Mod Template Item\Default", CLng(value))
            End Set
        End Property

        ''' <summary>Combination\OBTS\Object Mod Template Item\Min Level For Ranks</summary>
        Public Property ObjectModTemplateItemMinLevelForRanks As Byte
            Get
                Return CByte(Entero("Combination\OBTS\Object Mod Template Item\Min Level For Ranks"))
            End Get
            Set(value As Byte)
                Escribir("Combination\OBTS\Object Mod Template Item\Min Level For Ranks", CLng(value))
            End Set
        End Property

        ''' <summary>Combination\OBTS\Object Mod Template Item\Alt Levels Per Tier</summary>
        Public Property ObjectModTemplateItemAltLevelsPerTier As Byte
            Get
                Return CByte(Entero("Combination\OBTS\Object Mod Template Item\Alt Levels Per Tier"))
            End Get
            Set(value As Byte)
                Escribir("Combination\OBTS\Object Mod Template Item\Alt Levels Per Tier", CLng(value))
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Object Template\Combinations\Combination\OBTS\Object Mod Template Item\Keywords.</summary>
    Public NotInheritable Class ArmoFO4_Keywords2
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

    ''' <summary>Un elemento de Object Template\Combinations\Combination\OBTS\Object Mod Template Item\Includes.</summary>
    Public NotInheritable Class ArmoFO4_Includes
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Include\Mod  -&gt;  OMOD. Referencia en el espacio del orden de carga.</summary>
        Public Property IncludeMod As UInteger
            Get
                Return Referencia("Include\Mod")
            End Get
            Set(value As UInteger)
                PonerReferencia("Include\Mod", value)
            End Set
        End Property

        ''' <summary>Include\Attach Point Index</summary>
        Public Property IncludeAttachPointIndex As Byte
            Get
                Return CByte(Entero("Include\Attach Point Index"))
            End Get
            Set(value As Byte)
                Escribir("Include\Attach Point Index", CLng(value))
            End Set
        End Property

        ''' <summary>Include\Optional</summary>
        Public Property IncludeOptional As Byte
            Get
                Return CByte(Entero("Include\Optional"))
            End Get
            Set(value As Byte)
                Escribir("Include\Optional", CLng(value))
            End Set
        End Property

        ''' <summary>Include\Don't Use All</summary>
        Public Property IncludeDonTUseAll As Byte
            Get
                Return CByte(Entero("Include\Don't Use All"))
            End Get
            Set(value As Byte)
                Escribir("Include\Don't Use All", CLng(value))
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Object Template\Combinations\Combination\OBTS\Object Mod Template Item\Properties.</summary>
    Public NotInheritable Class ArmoFO4_Properties2
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Property\Value Type</summary>
        Public Property PropertyValueType As Byte
            Get
                Return CByte(Entero("Property\Value Type"))
            End Get
            Set(value As Byte)
                Escribir("Property\Value Type", CLng(value))
            End Set
        End Property
        ''' <summary>Nombre del valor de Property\Value Type.</summary>
        Public ReadOnly Property PropertyValueTypeNombre As String
            Get
                Return NombreDeValor("Property\Value Type")
            End Get
        End Property


        ''' <summary>Property\Function Type</summary>
        Public Property PropertyFunctionType As Byte
            Get
                Return CByte(Entero("Property\Function Type"))
            End Get
            Set(value As Byte)
                Escribir("Property\Function Type", CLng(value))
            End Set
        End Property
        ''' <summary>Nombre del valor de Property\Function Type.</summary>
        Public ReadOnly Property PropertyFunctionTypeNombre As String
            Get
                Return NombreDeValor("Property\Function Type")
            End Get
        End Property


        ''' <summary>Property\Property</summary>
        Public Property PropertyProperty As UShort
            Get
                Return CUShort(Entero("Property\Property"))
            End Get
            Set(value As UShort)
                Escribir("Property\Property", CLng(value))
            End Set
        End Property

        ''' <summary>Property\Value 1 - Unknown</summary>
        Public Property PropertyValue1Unknown As Byte()
            Get
                Return Bytes("Property\Value 1 - Unknown")
            End Get
            Set(value As Byte())
                Escribir("Property\Value 1 - Unknown", value)
            End Set
        End Property

        ''' <summary>Property\Step</summary>
        Public Property PropertyStep As Single
            Get
                Return Flt("Property\Step")
            End Get
            Set(value As Single)
                Escribir("Property\Step", value)
            End Set
        End Property

    End Class

    ''' <summary>Campos de un record BPTD de Fallout 4.</summary>
    Public NotInheritable Class BptdFO4
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

        ''' <summary>Model\MODC\Color Remapping Index</summary>
        Public Property ModelColorRemappingIndex As Single
            Get
                Return Flt("Model\MODC\Color Remapping Index")
            End Get
            Set(value As Single)
                Escribir("Model\MODC\Color Remapping Index", value)
            End Set
        End Property

        ''' <summary>Model\MODS\Material Swap  -&gt;  MSWP. Referencia en el espacio del orden de carga.</summary>
        Public Property ModelMaterialSwap As UInteger
            Get
                Return Referencia("Model\MODS\Material Swap")
            End Get
            Set(value As UInteger)
                PonerReferencia("Model\MODS\Material Swap", value)
            End Set
        End Property

        ''' <summary>Model\MODF\Flags</summary>
        Public Property ModelFlags As Byte
            Get
                Return CByte(Entero("Model\MODF\Flags"))
            End Get
            Set(value As Byte)
                Escribir("Model\MODF\Flags", CLng(value))
            End Set
        End Property

        ''' <summary>Body Parts</summary>
        Public ReadOnly Property BodyParts As IReadOnlyList(Of BptdFO4_BodyParts)
            Get
                Return Elementos(Of BptdFO4_BodyParts)("Body Parts", Function(n) New BptdFO4_BodyParts(n, Context, Resolver))
            End Get
        End Property

    End Class

    ''' <summary>Un elemento de Body Parts.</summary>
    Public NotInheritable Class BptdFO4_BodyParts
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Body Part\BPTN\Part Name. Resuelto contra las tablas de texto si el archivo las usa.</summary>
        Public Property BodyPartPartName As String
            Get
                Return TextoTraducible("Body Part\BPTN\Part Name")
            End Get
            Set(value As String)
                EscribirTextoTraducible("Body Part\BPTN\Part Name", value)
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

        ''' <summary>Body Part\BPND\Node Data\Damage Mult</summary>
        Public Property NodeDataDamageMult As Single
            Get
                Return Flt("Body Part\BPND\Node Data\Damage Mult")
            End Get
            Set(value As Single)
                Escribir("Body Part\BPND\Node Data\Damage Mult", value)
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

        ''' <summary>Body Part\BPND\Node Data\Explodable - Debris Scale</summary>
        Public Property NodeDataExplodableDebrisScale As Single
            Get
                Return Flt("Body Part\BPND\Node Data\Explodable - Debris Scale")
            End Get
            Set(value As Single)
                Escribir("Body Part\BPND\Node Data\Explodable - Debris Scale", value)
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

        ''' <summary>Body Part\BPND\Node Data\Cut - Min</summary>
        Public Property NodeDataCutMin As Single
            Get
                Return Flt("Body Part\BPND\Node Data\Cut - Min")
            End Get
            Set(value As Single)
                Escribir("Body Part\BPND\Node Data\Cut - Min", value)
            End Set
        End Property

        ''' <summary>Body Part\BPND\Node Data\Cut - Max</summary>
        Public Property NodeDataCutMax As Single
            Get
                Return Flt("Body Part\BPND\Node Data\Cut - Max")
            End Get
            Set(value As Single)
                Escribir("Body Part\BPND\Node Data\Cut - Max", value)
            End Set
        End Property

        ''' <summary>Body Part\BPND\Node Data\Cut - Radius</summary>
        Public Property NodeDataCutRadius As Single
            Get
                Return Flt("Body Part\BPND\Node Data\Cut - Radius")
            End Get
            Set(value As Single)
                Escribir("Body Part\BPND\Node Data\Cut - Radius", value)
            End Set
        End Property

        ''' <summary>Body Part\BPND\Node Data\Gore Effects - Local Rotate X</summary>
        Public Property NodeDataGoreEffectsLocalRotateX As Single
            Get
                Return Flt("Body Part\BPND\Node Data\Gore Effects - Local Rotate X")
            End Get
            Set(value As Single)
                Escribir("Body Part\BPND\Node Data\Gore Effects - Local Rotate X", value)
            End Set
        End Property

        ''' <summary>Body Part\BPND\Node Data\Gore Effects - Local Rotate Y</summary>
        Public Property NodeDataGoreEffectsLocalRotateY As Single
            Get
                Return Flt("Body Part\BPND\Node Data\Gore Effects - Local Rotate Y")
            End Get
            Set(value As Single)
                Escribir("Body Part\BPND\Node Data\Gore Effects - Local Rotate Y", value)
            End Set
        End Property

        ''' <summary>Body Part\BPND\Node Data\Cut - Tesselation</summary>
        Public Property NodeDataCutTesselation As Single
            Get
                Return Flt("Body Part\BPND\Node Data\Cut - Tesselation")
            End Get
            Set(value As Single)
                Escribir("Body Part\BPND\Node Data\Cut - Tesselation", value)
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

        ''' <summary>Body Part\BPND\Node Data\Explodable - Limb Replacement Scale</summary>
        Public Property NodeDataExplodableLimbReplacementScale As Single
            Get
                Return Flt("Body Part\BPND\Node Data\Explodable - Limb Replacement Scale")
            End Get
            Set(value As Single)
                Escribir("Body Part\BPND\Node Data\Explodable - Limb Replacement Scale", value)
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
        ''' <summary>Bit 1 de Body Part\BPND\Node Data\Flags: Hit Reaction</summary>
        Public Property NodeDataFlagsHitReaction As Boolean
            Get
                Return Bit("Body Part\BPND\Node Data\Flags", 1)
            End Get
            Set(value As Boolean)
                PonerBit("Body Part\BPND\Node Data\Flags", 1, value)
            End Set
        End Property
        ''' <summary>Bit 2 de Body Part\BPND\Node Data\Flags: Hit Reaction - Default</summary>
        Public Property NodeDataFlagsHitReactionDefault As Boolean
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
        ''' <summary>Bit 4 de Body Part\BPND\Node Data\Flags: Cut - Meat Cap Sever</summary>
        Public Property NodeDataFlagsCutMeatCapSever As Boolean
            Get
                Return Bit("Body Part\BPND\Node Data\Flags", 4)
            End Get
            Set(value As Boolean)
                PonerBit("Body Part\BPND\Node Data\Flags", 4, value)
            End Set
        End Property
        ''' <summary>Bit 5 de Body Part\BPND\Node Data\Flags: On Cripple</summary>
        Public Property NodeDataFlagsOnCripple As Boolean
            Get
                Return Bit("Body Part\BPND\Node Data\Flags", 5)
            End Get
            Set(value As Boolean)
                PonerBit("Body Part\BPND\Node Data\Flags", 5, value)
            End Set
        End Property
        ''' <summary>Bit 6 de Body Part\BPND\Node Data\Flags: Explodable - Absolute Chance</summary>
        Public Property NodeDataFlagsExplodableAbsoluteChance As Boolean
            Get
                Return Bit("Body Part\BPND\Node Data\Flags", 6)
            End Get
            Set(value As Boolean)
                PonerBit("Body Part\BPND\Node Data\Flags", 6, value)
            End Set
        End Property
        ''' <summary>Bit 7 de Body Part\BPND\Node Data\Flags: Show Cripple Geometry</summary>
        Public Property NodeDataFlagsShowCrippleGeometry As Boolean
            Get
                Return Bit("Body Part\BPND\Node Data\Flags", 7)
            End Get
            Set(value As Boolean)
                PonerBit("Body Part\BPND\Node Data\Flags", 7, value)
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

        ''' <summary>Body Part\BPND\Node Data\Actor Value  -&gt;  AVIF / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property NodeDataActorValue As UInteger
            Get
                Return Referencia("Body Part\BPND\Node Data\Actor Value")
            End Get
            Set(value As UInteger)
                PonerReferencia("Body Part\BPND\Node Data\Actor Value", value)
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

        ''' <summary>Body Part\BPND\Node Data\Non-Lethal Dismemberment Chance</summary>
        Public Property NodeDataNonLethalDismembermentChance As Byte
            Get
                Return CByte(Entero("Body Part\BPND\Node Data\Non-Lethal Dismemberment Chance"))
            End Get
            Set(value As Byte)
                Escribir("Body Part\BPND\Node Data\Non-Lethal Dismemberment Chance", CLng(value))
            End Set
        End Property

        ''' <summary>Body Part\BPND\Node Data\Severable - Debris Count</summary>
        Public Property NodeDataSeverableDebrisCount As Byte
            Get
                Return CByte(Entero("Body Part\BPND\Node Data\Severable - Debris Count"))
            End Get
            Set(value As Byte)
                Escribir("Body Part\BPND\Node Data\Severable - Debris Count", CLng(value))
            End Set
        End Property

        ''' <summary>Body Part\BPND\Node Data\Explodable - Debris Count</summary>
        Public Property NodeDataExplodableDebrisCount As Byte
            Get
                Return CByte(Entero("Body Part\BPND\Node Data\Explodable - Debris Count"))
            End Get
            Set(value As Byte)
                Escribir("Body Part\BPND\Node Data\Explodable - Debris Count", CLng(value))
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

        ''' <summary>Body Part\BPND\Node Data\Geometry Segment Index</summary>
        Public Property NodeDataGeometrySegmentIndex As Byte
            Get
                Return CByte(Entero("Body Part\BPND\Node Data\Geometry Segment Index"))
            End Get
            Set(value As Byte)
                Escribir("Body Part\BPND\Node Data\Geometry Segment Index", CLng(value))
            End Set
        End Property

        ''' <summary>Body Part\BPND\Node Data\On Cripple - Art Object  -&gt;  ARTO / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property NodeDataOnCrippleArtObject As UInteger
            Get
                Return Referencia("Body Part\BPND\Node Data\On Cripple - Art Object")
            End Get
            Set(value As UInteger)
                PonerReferencia("Body Part\BPND\Node Data\On Cripple - Art Object", value)
            End Set
        End Property

        ''' <summary>Body Part\BPND\Node Data\On Cripple - Debris  -&gt;  DEBR / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property NodeDataOnCrippleDebris As UInteger
            Get
                Return Referencia("Body Part\BPND\Node Data\On Cripple - Debris")
            End Get
            Set(value As UInteger)
                PonerReferencia("Body Part\BPND\Node Data\On Cripple - Debris", value)
            End Set
        End Property

        ''' <summary>Body Part\BPND\Node Data\On Cripple - Explosion  -&gt;  EXPL / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property NodeDataOnCrippleExplosion As UInteger
            Get
                Return Referencia("Body Part\BPND\Node Data\On Cripple - Explosion")
            End Get
            Set(value As UInteger)
                PonerReferencia("Body Part\BPND\Node Data\On Cripple - Explosion", value)
            End Set
        End Property

        ''' <summary>Body Part\BPND\Node Data\On Cripple - Impact DataSet  -&gt;  IPDS / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property NodeDataOnCrippleImpactDataSet As UInteger
            Get
                Return Referencia("Body Part\BPND\Node Data\On Cripple - Impact DataSet")
            End Get
            Set(value As UInteger)
                PonerReferencia("Body Part\BPND\Node Data\On Cripple - Impact DataSet", value)
            End Set
        End Property

        ''' <summary>Body Part\BPND\Node Data\On Cripple - Debris Scale</summary>
        Public Property NodeDataOnCrippleDebrisScale As Single
            Get
                Return Flt("Body Part\BPND\Node Data\On Cripple - Debris Scale")
            End Get
            Set(value As Single)
                Escribir("Body Part\BPND\Node Data\On Cripple - Debris Scale", value)
            End Set
        End Property

        ''' <summary>Body Part\BPND\Node Data\On Cripple - Debris Count</summary>
        Public Property NodeDataOnCrippleDebrisCount As Byte
            Get
                Return CByte(Entero("Body Part\BPND\Node Data\On Cripple - Debris Count"))
            End Get
            Set(value As Byte)
                Escribir("Body Part\BPND\Node Data\On Cripple - Debris Count", CLng(value))
            End Set
        End Property

        ''' <summary>Body Part\BPND\Node Data\On Cripple - Decal Count</summary>
        Public Property NodeDataOnCrippleDecalCount As Byte
            Get
                Return CByte(Entero("Body Part\BPND\Node Data\On Cripple - Decal Count"))
            End Get
            Set(value As Byte)
                Escribir("Body Part\BPND\Node Data\On Cripple - Decal Count", CLng(value))
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

        ''' <summary>Body Part\ENAM\Hit Reaction - Start</summary>
        Public Property BodyPartHitReactionStart As String
            Get
                Return Txt("Body Part\ENAM\Hit Reaction - Start")
            End Get
            Set(value As String)
                Escribir("Body Part\ENAM\Hit Reaction - Start", value)
            End Set
        End Property

        ''' <summary>Body Part\FNAM\Hit Reaction - End</summary>
        Public Property BodyPartHitReactionEnd As String
            Get
                Return Txt("Body Part\FNAM\Hit Reaction - End")
            End Get
            Set(value As String)
                Escribir("Body Part\FNAM\Hit Reaction - End", value)
            End Set
        End Property

        ''' <summary>Body Part\BNAM\Gore Effects - Dismember Blood Art  -&gt;  ARTO. Referencia en el espacio del orden de carga.</summary>
        Public Property BodyPartGoreEffectsDismemberBloodArt As UInteger
            Get
                Return Referencia("Body Part\BNAM\Gore Effects - Dismember Blood Art")
            End Get
            Set(value As UInteger)
                PonerReferencia("Body Part\BNAM\Gore Effects - Dismember Blood Art", value)
            End Set
        End Property

        ''' <summary>Body Part\INAM\Gore Effects - Blood Impact Material Type  -&gt;  MATT. Referencia en el espacio del orden de carga.</summary>
        Public Property BodyPartGoreEffectsBloodImpactMaterialType As UInteger
            Get
                Return Referencia("Body Part\INAM\Gore Effects - Blood Impact Material Type")
            End Get
            Set(value As UInteger)
                PonerReferencia("Body Part\INAM\Gore Effects - Blood Impact Material Type", value)
            End Set
        End Property

        ''' <summary>Body Part\JNAM\On Cripple - Blood Impact Material Type  -&gt;  MATT. Referencia en el espacio del orden de carga.</summary>
        Public Property BodyPartOnCrippleBloodImpactMaterialType As UInteger
            Get
                Return Referencia("Body Part\JNAM\On Cripple - Blood Impact Material Type")
            End Get
            Set(value As UInteger)
                PonerReferencia("Body Part\JNAM\On Cripple - Blood Impact Material Type", value)
            End Set
        End Property

        ''' <summary>Body Part\CNAM\Meat Cap TextureSet  -&gt;  TXST. Referencia en el espacio del orden de carga.</summary>
        Public Property BodyPartMeatCapTextureSet As UInteger
            Get
                Return Referencia("Body Part\CNAM\Meat Cap TextureSet")
            End Get
            Set(value As UInteger)
                PonerReferencia("Body Part\CNAM\Meat Cap TextureSet", value)
            End Set
        End Property

        ''' <summary>Body Part\NAM2\Collar TextureSet  -&gt;  TXST. Referencia en el espacio del orden de carga.</summary>
        Public Property BodyPartCollarTextureSet As UInteger
            Get
                Return Referencia("Body Part\NAM2\Collar TextureSet")
            End Get
            Set(value As UInteger)
                PonerReferencia("Body Part\NAM2\Collar TextureSet", value)
            End Set
        End Property

        ''' <summary>Body Part\DNAM\Twist Variable Prefix</summary>
        Public Property BodyPartTwistVariablePrefix As String
            Get
                Return Txt("Body Part\DNAM\Twist Variable Prefix")
            End Get
            Set(value As String)
                Escribir("Body Part\DNAM\Twist Variable Prefix", value)
            End Set
        End Property

    End Class

    ''' <summary>Campos de un record CLFM de Fallout 4.</summary>
    Public NotInheritable Class ClfmFO4
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

        ''' <summary>FULL\Name. Resuelto contra las tablas de texto si el archivo las usa.</summary>
        Public Property Name As String
            Get
                Return TextoTraducible("FULL\Name")
            End Get
            Set(value As String)
                EscribirTextoTraducible("FULL\Name", value)
            End Set
        End Property

        ''' <summary>CNAM\Color/Index</summary>
        Public Property ColorIndex As UInteger
            Get
                Return CUInt(Entero("CNAM\Color/Index"))
            End Get
            Set(value As UInteger)
                Escribir("CNAM\Color/Index", CLng(value))
            End Set
        End Property

        ''' <summary>FNAM\Flags</summary>
        Public Property Flags As UInteger
            Get
                Return CUInt(Entero("FNAM\Flags"))
            End Get
            Set(value As UInteger)
                Escribir("FNAM\Flags", CLng(value))
            End Set
        End Property

        ''' <summary>Conditions</summary>
        Public ReadOnly Property Conditions As IReadOnlyList(Of ClfmFO4_Conditions)
            Get
                Return Elementos(Of ClfmFO4_Conditions)("Conditions", Function(n) New ClfmFO4_Conditions(n, Context, Resolver))
            End Get
        End Property

    End Class

    ''' <summary>Un elemento de Conditions.</summary>
    Public NotInheritable Class ClfmFO4_Conditions
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Condition\CTDA\Type</summary>
        Public Property ConditionType As Byte
            Get
                Return CByte(Entero("Condition\CTDA\Type"))
            End Get
            Set(value As Byte)
                Escribir("Condition\CTDA\Type", CLng(value))
            End Set
        End Property

        ''' <summary>Condition\CTDA\Comparison Value - Float</summary>
        Public Property ConditionComparisonValueFloat As Single
            Get
                Return Flt("Condition\CTDA\Comparison Value - Float")
            End Get
            Set(value As Single)
                Escribir("Condition\CTDA\Comparison Value - Float", value)
            End Set
        End Property

        ''' <summary>Condition\CTDA\Function</summary>
        Public Property ConditionFunction As UShort
            Get
                Return CUShort(Entero("Condition\CTDA\Function"))
            End Get
            Set(value As UShort)
                Escribir("Condition\CTDA\Function", CLng(value))
            End Set
        End Property

        ''' <summary>Condition\CTDA\Run On</summary>
        Public Property ConditionRunOn As UInteger
            Get
                Return CUInt(Entero("Condition\CTDA\Run On"))
            End Get
            Set(value As UInteger)
                Escribir("Condition\CTDA\Run On", CLng(value))
            End Set
        End Property
        ''' <summary>Nombre del valor de Condition\CTDA\Run On.</summary>
        Public ReadOnly Property ConditionRunOnNombre As String
            Get
                Return NombreDeValor("Condition\CTDA\Run On")
            End Get
        End Property


        ''' <summary>Condition\CTDA\Parameter #3</summary>
        Public Property ConditionParameter3 As Integer
            Get
                Return CInt(Entero("Condition\CTDA\Parameter #3"))
            End Get
            Set(value As Integer)
                Escribir("Condition\CTDA\Parameter #3", CLng(value))
            End Set
        End Property

        ''' <summary>Condition\CIS1\Parameter #1</summary>
        Public Property ConditionParameter1 As String
            Get
                Return Txt("Condition\CIS1\Parameter #1")
            End Get
            Set(value As String)
                Escribir("Condition\CIS1\Parameter #1", value)
            End Set
        End Property

        ''' <summary>Condition\CIS2\Parameter #2</summary>
        Public Property ConditionParameter2 As String
            Get
                Return Txt("Condition\CIS2\Parameter #2")
            End Get
            Set(value As String)
                Escribir("Condition\CIS2\Parameter #2", value)
            End Set
        End Property

    End Class

    ''' <summary>Campos de un record DFOB de Fallout 4.</summary>
    Public NotInheritable Class DfobFO4
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

        ''' <summary>DATA\Object. Referencia en el espacio del orden de carga.</summary>
        Public Property [Object] As UInteger
            Get
                Return Referencia("DATA\Object")
            End Get
            Set(value As UInteger)
                PonerReferencia("DATA\Object", value)
            End Set
        End Property

    End Class

    ''' <summary>Campos de un record FLST de Fallout 4.</summary>
    Public NotInheritable Class FlstFO4
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

        ''' <summary>FULL\Name. Resuelto contra las tablas de texto si el archivo las usa.</summary>
        Public Property Name As String
            Get
                Return TextoTraducible("FULL\Name")
            End Get
            Set(value As String)
                EscribirTextoTraducible("FULL\Name", value)
            End Set
        End Property

        ''' <summary>FormIDs</summary>
        Public ReadOnly Property FormIDs As IReadOnlyList(Of FlstFO4_FormIDs)
            Get
                Return Elementos(Of FlstFO4_FormIDs)("FormIDs", Function(n) New FlstFO4_FormIDs(n, Context, Resolver))
            End Get
        End Property

    End Class

    ''' <summary>Un elemento de FormIDs.</summary>
    Public NotInheritable Class FlstFO4_FormIDs
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

    ''' <summary>Campos de un record HDPT de Fallout 4.</summary>
    Public NotInheritable Class HdptFO4
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

        ''' <summary>FULL\Name. Resuelto contra las tablas de texto si el archivo las usa.</summary>
        Public Property Name As String
            Get
                Return TextoTraducible("FULL\Name")
            End Get
            Set(value As String)
                EscribirTextoTraducible("FULL\Name", value)
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

        ''' <summary>Model\MODC\Color Remapping Index</summary>
        Public Property ModelColorRemappingIndex As Single
            Get
                Return Flt("Model\MODC\Color Remapping Index")
            End Get
            Set(value As Single)
                Escribir("Model\MODC\Color Remapping Index", value)
            End Set
        End Property

        ''' <summary>Model\MODS\Material Swap  -&gt;  MSWP. Referencia en el espacio del orden de carga.</summary>
        Public Property ModelMaterialSwap As UInteger
            Get
                Return Referencia("Model\MODS\Material Swap")
            End Get
            Set(value As UInteger)
                PonerReferencia("Model\MODS\Material Swap", value)
            End Set
        End Property

        ''' <summary>Model\MODF\Flags</summary>
        Public Property ModelFlags As Byte
            Get
                Return CByte(Entero("Model\MODF\Flags"))
            End Get
            Set(value As Byte)
                Escribir("Model\MODF\Flags", CLng(value))
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

        ''' <summary>TNAM\Texture Set  -&gt;  TXST. Referencia en el espacio del orden de carga.</summary>
        Public Property TextureSet As UInteger
            Get
                Return Referencia("TNAM\Texture Set")
            End Get
            Set(value As UInteger)
                PonerReferencia("TNAM\Texture Set", value)
            End Set
        End Property

        ''' <summary>CNAM\Color  -&gt;  CLFM. Referencia en el espacio del orden de carga.</summary>
        Public Property Color As UInteger
            Get
                Return Referencia("CNAM\Color")
            End Get
            Set(value As UInteger)
                PonerReferencia("CNAM\Color", value)
            End Set
        End Property

        ''' <summary>RNAM\Valid Races  -&gt;  FLST. Referencia en el espacio del orden de carga.</summary>
        Public Property ValidRaces As UInteger
            Get
                Return Referencia("RNAM\Valid Races")
            End Get
            Set(value As UInteger)
                PonerReferencia("RNAM\Valid Races", value)
            End Set
        End Property

        ''' <summary>Extra Parts</summary>
        Public ReadOnly Property ExtraParts As IReadOnlyList(Of HdptFO4_ExtraParts)
            Get
                Return Elementos(Of HdptFO4_ExtraParts)("Extra Parts", Function(n) New HdptFO4_ExtraParts(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Parts</summary>
        Public ReadOnly Property Parts As IReadOnlyList(Of HdptFO4_Parts)
            Get
                Return Elementos(Of HdptFO4_Parts)("Parts", Function(n) New HdptFO4_Parts(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Conditions</summary>
        Public ReadOnly Property Conditions As IReadOnlyList(Of HdptFO4_Conditions)
            Get
                Return Elementos(Of HdptFO4_Conditions)("Conditions", Function(n) New HdptFO4_Conditions(n, Context, Resolver))
            End Get
        End Property

    End Class

    ''' <summary>Un elemento de Extra Parts.</summary>
    Public NotInheritable Class HdptFO4_ExtraParts
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
    Public NotInheritable Class HdptFO4_Parts
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

    ''' <summary>Un elemento de Conditions.</summary>
    Public NotInheritable Class HdptFO4_Conditions
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Condition\CTDA\Type</summary>
        Public Property ConditionType As Byte
            Get
                Return CByte(Entero("Condition\CTDA\Type"))
            End Get
            Set(value As Byte)
                Escribir("Condition\CTDA\Type", CLng(value))
            End Set
        End Property

        ''' <summary>Condition\CTDA\Comparison Value - Float</summary>
        Public Property ConditionComparisonValueFloat As Single
            Get
                Return Flt("Condition\CTDA\Comparison Value - Float")
            End Get
            Set(value As Single)
                Escribir("Condition\CTDA\Comparison Value - Float", value)
            End Set
        End Property

        ''' <summary>Condition\CTDA\Function</summary>
        Public Property ConditionFunction As UShort
            Get
                Return CUShort(Entero("Condition\CTDA\Function"))
            End Get
            Set(value As UShort)
                Escribir("Condition\CTDA\Function", CLng(value))
            End Set
        End Property

        ''' <summary>Condition\CTDA\Run On</summary>
        Public Property ConditionRunOn As UInteger
            Get
                Return CUInt(Entero("Condition\CTDA\Run On"))
            End Get
            Set(value As UInteger)
                Escribir("Condition\CTDA\Run On", CLng(value))
            End Set
        End Property
        ''' <summary>Nombre del valor de Condition\CTDA\Run On.</summary>
        Public ReadOnly Property ConditionRunOnNombre As String
            Get
                Return NombreDeValor("Condition\CTDA\Run On")
            End Get
        End Property


        ''' <summary>Condition\CTDA\Parameter #3</summary>
        Public Property ConditionParameter3 As Integer
            Get
                Return CInt(Entero("Condition\CTDA\Parameter #3"))
            End Get
            Set(value As Integer)
                Escribir("Condition\CTDA\Parameter #3", CLng(value))
            End Set
        End Property

        ''' <summary>Condition\CIS1\Parameter #1</summary>
        Public Property ConditionParameter1 As String
            Get
                Return Txt("Condition\CIS1\Parameter #1")
            End Get
            Set(value As String)
                Escribir("Condition\CIS1\Parameter #1", value)
            End Set
        End Property

        ''' <summary>Condition\CIS2\Parameter #2</summary>
        Public Property ConditionParameter2 As String
            Get
                Return Txt("Condition\CIS2\Parameter #2")
            End Get
            Set(value As String)
                Escribir("Condition\CIS2\Parameter #2", value)
            End Set
        End Property

    End Class

    ''' <summary>Campos de un record LVLI de Fallout 4.</summary>
    Public NotInheritable Class LvliFO4
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

        ''' <summary>LVLM\Max Count</summary>
        Public Property MaxCount As Byte
            Get
                Return CByte(Entero("LVLM\Max Count"))
            End Get
            Set(value As Byte)
                Escribir("LVLM\Max Count", CLng(value))
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

        ''' <summary>LVLG\Use Global  -&gt;  GLOB. Referencia en el espacio del orden de carga.</summary>
        Public Property UseGlobal As UInteger
            Get
                Return Referencia("LVLG\Use Global")
            End Get
            Set(value As UInteger)
                PonerReferencia("LVLG\Use Global", value)
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

        ''' <summary>LVSG\Epic Loot Chance  -&gt;  GLOB. Referencia en el espacio del orden de carga.</summary>
        Public Property EpicLootChance As UInteger
            Get
                Return Referencia("LVSG\Epic Loot Chance")
            End Get
            Set(value As UInteger)
                PonerReferencia("LVSG\Epic Loot Chance", value)
            End Set
        End Property

        ''' <summary>ONAM\Override Name. Resuelto contra las tablas de texto si el archivo las usa.</summary>
        Public Property OverrideName As String
            Get
                Return TextoTraducible("ONAM\Override Name")
            End Get
            Set(value As String)
                EscribirTextoTraducible("ONAM\Override Name", value)
            End Set
        End Property

        ''' <summary>Leveled List Entries</summary>
        Public ReadOnly Property LeveledListEntries As IReadOnlyList(Of LvliFO4_LeveledListEntries)
            Get
                Return Elementos(Of LvliFO4_LeveledListEntries)("Leveled List Entries", Function(n) New LvliFO4_LeveledListEntries(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>LLKC\Filter Keyword Chances</summary>
        Public ReadOnly Property FilterKeywordChances As IReadOnlyList(Of LvliFO4_FilterKeywordChances)
            Get
                Return Elementos(Of LvliFO4_FilterKeywordChances)("LLKC\Filter Keyword Chances", Function(n) New LvliFO4_FilterKeywordChances(n, Context, Resolver))
            End Get
        End Property

    End Class

    ''' <summary>Un elemento de Leveled List Entries.</summary>
    Public NotInheritable Class LvliFO4_LeveledListEntries
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

        ''' <summary>Leveled List Entry\LVLO\Item. Referencia en el espacio del orden de carga.</summary>
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

        ''' <summary>Leveled List Entry\LVLO\Chance None</summary>
        Public Property LeveledListEntryChanceNone As Byte
            Get
                Return CByte(Entero("Leveled List Entry\LVLO\Chance None"))
            End Get
            Set(value As Byte)
                Escribir("Leveled List Entry\LVLO\Chance None", CLng(value))
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

    ''' <summary>Un elemento de LLKC\Filter Keyword Chances.</summary>
    Public NotInheritable Class LvliFO4_FilterKeywordChances
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Filter\Keyword  -&gt;  KYWD. Referencia en el espacio del orden de carga.</summary>
        Public Property FilterKeyword As UInteger
            Get
                Return Referencia("Filter\Keyword")
            End Get
            Set(value As UInteger)
                PonerReferencia("Filter\Keyword", value)
            End Set
        End Property

        ''' <summary>Filter\Chance</summary>
        Public Property FilterChance As UInteger
            Get
                Return CUInt(Entero("Filter\Chance"))
            End Get
            Set(value As UInteger)
                Escribir("Filter\Chance", CLng(value))
            End Set
        End Property

    End Class

    ''' <summary>Campos de un record LVLN de Fallout 4.</summary>
    Public NotInheritable Class LvlnFO4
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

        ''' <summary>LVLM\Max Count</summary>
        Public Property MaxCount As Byte
            Get
                Return CByte(Entero("LVLM\Max Count"))
            End Get
            Set(value As Byte)
                Escribir("LVLM\Max Count", CLng(value))
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

        ''' <summary>LVLG\Use Global  -&gt;  GLOB. Referencia en el espacio del orden de carga.</summary>
        Public Property UseGlobal As UInteger
            Get
                Return Referencia("LVLG\Use Global")
            End Get
            Set(value As UInteger)
                PonerReferencia("LVLG\Use Global", value)
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

        ''' <summary>Model\MODC\Color Remapping Index</summary>
        Public Property ModelColorRemappingIndex As Single
            Get
                Return Flt("Model\MODC\Color Remapping Index")
            End Get
            Set(value As Single)
                Escribir("Model\MODC\Color Remapping Index", value)
            End Set
        End Property

        ''' <summary>Model\MODS\Material Swap  -&gt;  MSWP. Referencia en el espacio del orden de carga.</summary>
        Public Property ModelMaterialSwap As UInteger
            Get
                Return Referencia("Model\MODS\Material Swap")
            End Get
            Set(value As UInteger)
                PonerReferencia("Model\MODS\Material Swap", value)
            End Set
        End Property

        ''' <summary>Model\MODF\Flags</summary>
        Public Property ModelFlags As Byte
            Get
                Return CByte(Entero("Model\MODF\Flags"))
            End Get
            Set(value As Byte)
                Escribir("Model\MODF\Flags", CLng(value))
            End Set
        End Property

        ''' <summary>Leveled List Entries</summary>
        Public ReadOnly Property LeveledListEntries As IReadOnlyList(Of LvlnFO4_LeveledListEntries)
            Get
                Return Elementos(Of LvlnFO4_LeveledListEntries)("Leveled List Entries", Function(n) New LvlnFO4_LeveledListEntries(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>LLKC\Filter Keyword Chances</summary>
        Public ReadOnly Property FilterKeywordChances As IReadOnlyList(Of LvlnFO4_FilterKeywordChances)
            Get
                Return Elementos(Of LvlnFO4_FilterKeywordChances)("LLKC\Filter Keyword Chances", Function(n) New LvlnFO4_FilterKeywordChances(n, Context, Resolver))
            End Get
        End Property

    End Class

    ''' <summary>Un elemento de Leveled List Entries.</summary>
    Public NotInheritable Class LvlnFO4_LeveledListEntries
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

        ''' <summary>Leveled List Entry\LVLO\Chance None</summary>
        Public Property LeveledListEntryChanceNone As Byte
            Get
                Return CByte(Entero("Leveled List Entry\LVLO\Chance None"))
            End Get
            Set(value As Byte)
                Escribir("Leveled List Entry\LVLO\Chance None", CLng(value))
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

    ''' <summary>Un elemento de LLKC\Filter Keyword Chances.</summary>
    Public NotInheritable Class LvlnFO4_FilterKeywordChances
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Filter\Keyword  -&gt;  KYWD. Referencia en el espacio del orden de carga.</summary>
        Public Property FilterKeyword As UInteger
            Get
                Return Referencia("Filter\Keyword")
            End Get
            Set(value As UInteger)
                PonerReferencia("Filter\Keyword", value)
            End Set
        End Property

        ''' <summary>Filter\Chance</summary>
        Public Property FilterChance As UInteger
            Get
                Return CUInt(Entero("Filter\Chance"))
            End Get
            Set(value As UInteger)
                Escribir("Filter\Chance", CLng(value))
            End Set
        End Property

    End Class

    ''' <summary>Campos de un record MSWP de Fallout 4.</summary>
    Public NotInheritable Class MswpFO4
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

        ''' <summary>FNAM\Tree Folder</summary>
        Public Property TreeFolder As String
            Get
                Return Txt("FNAM\Tree Folder")
            End Get
            Set(value As String)
                Escribir("FNAM\Tree Folder", value)
            End Set
        End Property

        ''' <summary>Material Substitutions</summary>
        Public ReadOnly Property MaterialSubstitutions As IReadOnlyList(Of MswpFO4_MaterialSubstitutions)
            Get
                Return Elementos(Of MswpFO4_MaterialSubstitutions)("Material Substitutions", Function(n) New MswpFO4_MaterialSubstitutions(n, Context, Resolver))
            End Get
        End Property

    End Class

    ''' <summary>Un elemento de Material Substitutions.</summary>
    Public NotInheritable Class MswpFO4_MaterialSubstitutions
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Substitution\BNAM\Original Material</summary>
        Public Property SubstitutionOriginalMaterial As String
            Get
                Return Txt("Substitution\BNAM\Original Material")
            End Get
            Set(value As String)
                Escribir("Substitution\BNAM\Original Material", value)
            End Set
        End Property

        ''' <summary>Substitution\SNAM\Replacement Material</summary>
        Public Property SubstitutionReplacementMaterial As String
            Get
                Return Txt("Substitution\SNAM\Replacement Material")
            End Get
            Set(value As String)
                Escribir("Substitution\SNAM\Replacement Material", value)
            End Set
        End Property

        ''' <summary>Substitution\FNAM\Tree Folder (obsolete)</summary>
        Public Property SubstitutionTreeFolderObsolete As String
            Get
                Return Txt("Substitution\FNAM\Tree Folder (obsolete)")
            End Get
            Set(value As String)
                Escribir("Substitution\FNAM\Tree Folder (obsolete)", value)
            End Set
        End Property

        ''' <summary>Substitution\CNAM\Color Remapping Index</summary>
        Public Property SubstitutionColorRemappingIndex As Single
            Get
                Return Flt("Substitution\CNAM\Color Remapping Index")
            End Get
            Set(value As Single)
                Escribir("Substitution\CNAM\Color Remapping Index", value)
            End Set
        End Property

    End Class

    ''' <summary>Campos de un record NPC_ de Fallout 4.</summary>
    Public NotInheritable Class NpcFO4
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

        ''' <summary>PTRN\Preview Transform  -&gt;  TRNS. Referencia en el espacio del orden de carga.</summary>
        Public Property PreviewTransform As UInteger
            Get
                Return Referencia("PTRN\Preview Transform")
            End Get
            Set(value As UInteger)
                PonerReferencia("PTRN\Preview Transform", value)
            End Set
        End Property

        ''' <summary>STCP\Animation Sound  -&gt;  STAG. Referencia en el espacio del orden de carga.</summary>
        Public Property AnimationSound As UInteger
            Get
                Return Referencia("STCP\Animation Sound")
            End Get
            Set(value As UInteger)
                PonerReferencia("STCP\Animation Sound", value)
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
        ''' <summary>Bit 9 de ACBS\Configuration\Flags: Calc For Each Template</summary>
        Public Property ConfigurationFlagsCalcForEachTemplate As Boolean
            Get
                Return Bit("ACBS\Configuration\Flags", 9)
            End Get
            Set(value As Boolean)
                PonerBit("ACBS\Configuration\Flags", 9, value)
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
        ''' <summary>Bit 23 de ACBS\Configuration\Flags: No Activation/Hellos</summary>
        Public Property ConfigurationFlagsNoActivationHellos As Boolean
            Get
                Return Bit("ACBS\Configuration\Flags", 23)
            End Get
            Set(value As Boolean)
                PonerBit("ACBS\Configuration\Flags", 23, value)
            End Set
        End Property
        ''' <summary>Bit 24 de ACBS\Configuration\Flags: Diffuse Alpha Test</summary>
        Public Property ConfigurationFlagsDiffuseAlphaTest As Boolean
            Get
                Return Bit("ACBS\Configuration\Flags", 24)
            End Get
            Set(value As Boolean)
                PonerBit("ACBS\Configuration\Flags", 24, value)
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

        ''' <summary>ACBS\Configuration\XP Value Offset</summary>
        Public Property ConfigurationXPValueOffset As Short
            Get
                Return CShort(Entero("ACBS\Configuration\XP Value Offset"))
            End Get
            Set(value As Short)
                Escribir("ACBS\Configuration\XP Value Offset", CLng(value))
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

        ''' <summary>ACBS\Configuration\Disposition Base</summary>
        Public Property ConfigurationDispositionBase As Short
            Get
                Return CShort(Entero("ACBS\Configuration\Disposition Base"))
            End Get
            Set(value As Short)
                Escribir("ACBS\Configuration\Disposition Base", CLng(value))
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

        ''' <summary>TPLT\Default Template  -&gt;  LVLN / NPC_. Referencia en el espacio del orden de carga.</summary>
        Public Property DefaultTemplate As UInteger
            Get
                Return Referencia("TPLT\Default Template")
            End Get
            Set(value As UInteger)
                PonerReferencia("TPLT\Default Template", value)
            End Set
        End Property

        ''' <summary>LTPT\Legendary Template  -&gt;  LVLN / NPC_. Referencia en el espacio del orden de carga.</summary>
        Public Property LegendaryTemplate As UInteger
            Get
                Return Referencia("LTPT\Legendary Template")
            End Get
            Set(value As UInteger)
                PonerReferencia("LTPT\Legendary Template", value)
            End Set
        End Property

        ''' <summary>LTPC\Legendary Chance  -&gt;  GLOB. Referencia en el espacio del orden de carga.</summary>
        Public Property LegendaryChance As UInteger
            Get
                Return Referencia("LTPC\Legendary Chance")
            End Get
            Set(value As UInteger)
                PonerReferencia("LTPC\Legendary Chance", value)
            End Set
        End Property

        ''' <summary>TPTA\Template Actors\Traits  -&gt;  BMMO / LVLN / NPC_ / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property TemplateActorsTraits As UInteger
            Get
                Return Referencia("TPTA\Template Actors\Traits")
            End Get
            Set(value As UInteger)
                PonerReferencia("TPTA\Template Actors\Traits", value)
            End Set
        End Property

        ''' <summary>TPTA\Template Actors\Stats  -&gt;  BMMO / LVLN / NPC_ / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property TemplateActorsStats As UInteger
            Get
                Return Referencia("TPTA\Template Actors\Stats")
            End Get
            Set(value As UInteger)
                PonerReferencia("TPTA\Template Actors\Stats", value)
            End Set
        End Property

        ''' <summary>TPTA\Template Actors\Factions  -&gt;  BMMO / LVLN / NPC_ / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property TemplateActorsFactions As UInteger
            Get
                Return Referencia("TPTA\Template Actors\Factions")
            End Get
            Set(value As UInteger)
                PonerReferencia("TPTA\Template Actors\Factions", value)
            End Set
        End Property

        ''' <summary>TPTA\Template Actors\Spell List  -&gt;  BMMO / LVLN / NPC_ / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property TemplateActorsSpellList As UInteger
            Get
                Return Referencia("TPTA\Template Actors\Spell List")
            End Get
            Set(value As UInteger)
                PonerReferencia("TPTA\Template Actors\Spell List", value)
            End Set
        End Property

        ''' <summary>TPTA\Template Actors\AI Data  -&gt;  BMMO / LVLN / NPC_ / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property TemplateActorsAIData As UInteger
            Get
                Return Referencia("TPTA\Template Actors\AI Data")
            End Get
            Set(value As UInteger)
                PonerReferencia("TPTA\Template Actors\AI Data", value)
            End Set
        End Property

        ''' <summary>TPTA\Template Actors\AI Packages  -&gt;  BMMO / LVLN / NPC_ / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property TemplateActorsAIPackages As UInteger
            Get
                Return Referencia("TPTA\Template Actors\AI Packages")
            End Get
            Set(value As UInteger)
                PonerReferencia("TPTA\Template Actors\AI Packages", value)
            End Set
        End Property

        ''' <summary>TPTA\Template Actors\Model/Animation  -&gt;  BMMO / LVLN / NPC_ / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property TemplateActorsModelAnimation As UInteger
            Get
                Return Referencia("TPTA\Template Actors\Model/Animation")
            End Get
            Set(value As UInteger)
                PonerReferencia("TPTA\Template Actors\Model/Animation", value)
            End Set
        End Property

        ''' <summary>TPTA\Template Actors\Base Data  -&gt;  BMMO / LVLN / NPC_ / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property TemplateActorsBaseData As UInteger
            Get
                Return Referencia("TPTA\Template Actors\Base Data")
            End Get
            Set(value As UInteger)
                PonerReferencia("TPTA\Template Actors\Base Data", value)
            End Set
        End Property

        ''' <summary>TPTA\Template Actors\Inventory  -&gt;  BMMO / LVLN / NPC_ / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property TemplateActorsInventory As UInteger
            Get
                Return Referencia("TPTA\Template Actors\Inventory")
            End Get
            Set(value As UInteger)
                PonerReferencia("TPTA\Template Actors\Inventory", value)
            End Set
        End Property

        ''' <summary>TPTA\Template Actors\Script  -&gt;  BMMO / LVLN / NPC_ / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property TemplateActorsScript As UInteger
            Get
                Return Referencia("TPTA\Template Actors\Script")
            End Get
            Set(value As UInteger)
                PonerReferencia("TPTA\Template Actors\Script", value)
            End Set
        End Property

        ''' <summary>TPTA\Template Actors\Def Package List  -&gt;  BMMO / LVLN / NPC_ / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property TemplateActorsDefPackageList As UInteger
            Get
                Return Referencia("TPTA\Template Actors\Def Package List")
            End Get
            Set(value As UInteger)
                PonerReferencia("TPTA\Template Actors\Def Package List", value)
            End Set
        End Property

        ''' <summary>TPTA\Template Actors\Attack Data  -&gt;  BMMO / LVLN / NPC_ / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property TemplateActorsAttackData As UInteger
            Get
                Return Referencia("TPTA\Template Actors\Attack Data")
            End Get
            Set(value As UInteger)
                PonerReferencia("TPTA\Template Actors\Attack Data", value)
            End Set
        End Property

        ''' <summary>TPTA\Template Actors\Keywords  -&gt;  BMMO / LVLN / NPC_ / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property TemplateActorsKeywords As UInteger
            Get
                Return Referencia("TPTA\Template Actors\Keywords")
            End Get
            Set(value As UInteger)
                PonerReferencia("TPTA\Template Actors\Keywords", value)
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

        ''' <summary>Destructible\DEST\Header\Flags</summary>
        Public Property HeaderFlags As Byte
            Get
                Return CByte(Entero("Destructible\DEST\Header\Flags"))
            End Get
            Set(value As Byte)
                Escribir("Destructible\DEST\Header\Flags", CLng(value))
            End Set
        End Property

        ''' <summary>Bit 0 de Destructible\DEST\Header\Flags: VATS Targetable</summary>
        Public Property HeaderFlagsVATSTargetable As Boolean
            Get
                Return Bit("Destructible\DEST\Header\Flags", 0)
            End Get
            Set(value As Boolean)
                PonerBit("Destructible\DEST\Header\Flags", 0, value)
            End Set
        End Property
        ''' <summary>Bit 1 de Destructible\DEST\Header\Flags: Large Actor Destroys</summary>
        Public Property HeaderFlagsLargeActorDestroys As Boolean
            Get
                Return Bit("Destructible\DEST\Header\Flags", 1)
            End Get
            Set(value As Boolean)
                PonerBit("Destructible\DEST\Header\Flags", 1, value)
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

        ''' <summary>SPOR\Spectator Override Package List  -&gt;  FLST. Referencia en el espacio del orden de carga.</summary>
        Public Property SpectatorOverridePackageList As UInteger
            Get
                Return Referencia("SPOR\Spectator Override Package List")
            End Get
            Set(value As UInteger)
                PonerReferencia("SPOR\Spectator Override Package List", value)
            End Set
        End Property

        ''' <summary>OCOR\Observe Dead Body Override Package List  -&gt;  FLST. Referencia en el espacio del orden de carga.</summary>
        Public Property ObserveDeadBodyOverridePackageList As UInteger
            Get
                Return Referencia("OCOR\Observe Dead Body Override Package List")
            End Get
            Set(value As UInteger)
                PonerReferencia("OCOR\Observe Dead Body Override Package List", value)
            End Set
        End Property

        ''' <summary>GWOR\Guard Warn Override Package List  -&gt;  FLST. Referencia en el espacio del orden de carga.</summary>
        Public Property GuardWarnOverridePackageList As UInteger
            Get
                Return Referencia("GWOR\Guard Warn Override Package List")
            End Get
            Set(value As UInteger)
                PonerReferencia("GWOR\Guard Warn Override Package List", value)
            End Set
        End Property

        ''' <summary>ECOR\Combat Override Package List  -&gt;  FLST. Referencia en el espacio del orden de carga.</summary>
        Public Property CombatOverridePackageList As UInteger
            Get
                Return Referencia("ECOR\Combat Override Package List")
            End Get
            Set(value As UInteger)
                PonerReferencia("ECOR\Combat Override Package List", value)
            End Set
        End Property

        ''' <summary>FCPL\Follower Command Package List  -&gt;  FLST. Referencia en el espacio del orden de carga.</summary>
        Public Property FollowerCommandPackageList As UInteger
            Get
                Return Referencia("FCPL\Follower Command Package List")
            End Get
            Set(value As UInteger)
                PonerReferencia("FCPL\Follower Command Package List", value)
            End Set
        End Property

        ''' <summary>RCLR\Follower Elevator Package List  -&gt;  FLST. Referencia en el espacio del orden de carga.</summary>
        Public Property FollowerElevatorPackageList As UInteger
            Get
                Return Referencia("RCLR\Follower Elevator Package List")
            End Get
            Set(value As UInteger)
                PonerReferencia("RCLR\Follower Elevator Package List", value)
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

        ''' <summary>FTYP\Forced Loc Ref Type  -&gt;  LCRT. Referencia en el espacio del orden de carga.</summary>
        Public Property ForcedLocRefType As UInteger
            Get
                Return Referencia("FTYP\Forced Loc Ref Type")
            End Get
            Set(value As UInteger)
                PonerReferencia("FTYP\Forced Loc Ref Type", value)
            End Set
        End Property

        ''' <summary>NTRM\Native Terminal  -&gt;  TERM. Referencia en el espacio del orden de carga.</summary>
        Public Property NativeTerminal As UInteger
            Get
                Return Referencia("NTRM\Native Terminal")
            End Get
            Set(value As UInteger)
                PonerReferencia("NTRM\Native Terminal", value)
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

        ''' <summary>AIDT\AI Data\No Slow Approach</summary>
        Public Property AIDataNoSlowApproach As Byte
            Get
                Return CByte(Entero("AIDT\AI Data\No Slow Approach"))
            End Get
            Set(value As Byte)
                Escribir("AIDT\AI Data\No Slow Approach", CLng(value))
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

        ''' <summary>Object Template\OBTE\Count</summary>
        Public Property ObjectTemplateCount As UInteger
            Get
                Return CUInt(Entero("Object Template\OBTE\Count"))
            End Get
            Set(value As UInteger)
                Escribir("Object Template\OBTE\Count", CLng(value))
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

        ''' <summary>FULL\Name. Resuelto contra las tablas de texto si el archivo las usa.</summary>
        Public Property Name As String
            Get
                Return TextoTraducible("FULL\Name")
            End Get
            Set(value As String)
                EscribirTextoTraducible("FULL\Name", value)
            End Set
        End Property

        ''' <summary>SHRT\Short Name. Resuelto contra las tablas de texto si el archivo las usa.</summary>
        Public Property ShortName As String
            Get
                Return TextoTraducible("SHRT\Short Name")
            End Get
            Set(value As String)
                EscribirTextoTraducible("SHRT\Short Name", value)
            End Set
        End Property

        ''' <summary>DNAM\Calculated Health</summary>
        Public Property CalculatedHealth As UShort
            Get
                Return CUShort(Entero("DNAM\Calculated Health"))
            End Get
            Set(value As UShort)
                Escribir("DNAM\Calculated Health", CLng(value))
            End Set
        End Property

        ''' <summary>DNAM\Calculated Action Points</summary>
        Public Property CalculatedActionPoints As UShort
            Get
                Return CUShort(Entero("DNAM\Calculated Action Points"))
            End Get
            Set(value As UShort)
                Escribir("DNAM\Calculated Action Points", CLng(value))
            End Set
        End Property

        ''' <summary>DNAM\Far Away Model Distance</summary>
        Public Property FarAwayModelDistance As UShort
            Get
                Return CUShort(Entero("DNAM\Far Away Model Distance"))
            End Get
            Set(value As UShort)
                Escribir("DNAM\Far Away Model Distance", CLng(value))
            End Set
        End Property

        ''' <summary>DNAM\Geared Up Weapons</summary>
        Public Property GearedUpWeapons As Byte
            Get
                Return CByte(Entero("DNAM\Geared Up Weapons"))
            End Get
            Set(value As Byte)
                Escribir("DNAM\Geared Up Weapons", CLng(value))
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

        ''' <summary>BCLF\Facial Hair Color  -&gt;  CLFM. Referencia en el espacio del orden de carga.</summary>
        Public Property FacialHairColor As UInteger
            Get
                Return Referencia("BCLF\Facial Hair Color")
            End Get
            Set(value As UInteger)
                PonerReferencia("BCLF\Facial Hair Color", value)
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

        ''' <summary>NAM6\Height Min</summary>
        Public Property HeightMin As Single
            Get
                Return Flt("NAM6\Height Min")
            End Get
            Set(value As Single)
                Escribir("NAM6\Height Min", value)
            End Set
        End Property

        ''' <summary>NAM4\Height Max</summary>
        Public Property HeightMax As Single
            Get
                Return Flt("NAM4\Height Max")
            End Get
            Set(value As Single)
                Escribir("NAM4\Height Max", value)
            End Set
        End Property

        ''' <summary>MWGT\Weight\Thin</summary>
        Public Property WeightThin As Single
            Get
                Return Flt("MWGT\Weight\Thin")
            End Get
            Set(value As Single)
                Escribir("MWGT\Weight\Thin", value)
            End Set
        End Property

        ''' <summary>MWGT\Weight\Muscular</summary>
        Public Property WeightMuscular As Single
            Get
                Return Flt("MWGT\Weight\Muscular")
            End Get
            Set(value As Single)
                Escribir("MWGT\Weight\Muscular", value)
            End Set
        End Property

        ''' <summary>MWGT\Weight\Fat</summary>
        Public Property WeightFat As Single
            Get
                Return Flt("MWGT\Weight\Fat")
            End Get
            Set(value As Single)
                Escribir("MWGT\Weight\Fat", value)
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

        ''' <summary>Actor Sounds\CS2H\Count</summary>
        Public Property ActorSoundsCount As UInteger
            Get
                Return CUInt(Entero("Actor Sounds\CS2H\Count"))
            End Get
            Set(value As UInteger)
                Escribir("Actor Sounds\CS2H\Count", CLng(value))
            End Set
        End Property

        ''' <summary>Actor Sounds\CS2F\Finalize</summary>
        Public Property ActorSoundsFinalize As Byte()
            Get
                Return Bytes("Actor Sounds\CS2F\Finalize")
            End Get
            Set(value As Byte())
                Escribir("Actor Sounds\CS2F\Finalize", value)
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

        ''' <summary>PFRN\Power Armor Stand  -&gt;  FURN. Referencia en el espacio del orden de carga.</summary>
        Public Property PowerArmorStand As UInteger
            Get
                Return Referencia("PFRN\Power Armor Stand")
            End Get
            Set(value As UInteger)
                PonerReferencia("PFRN\Power Armor Stand", value)
            End Set
        End Property

        ''' <summary>DOFT\Default Outfit  -&gt;  OTFT. Referencia en el espacio del orden de carga.</summary>
        Public Property DefaultOutfit As UInteger
            Get
                Return Referencia("DOFT\Default Outfit")
            End Get
            Set(value As UInteger)
                PonerReferencia("DOFT\Default Outfit", value)
            End Set
        End Property

        ''' <summary>SOFT\Sleeping Outfit  -&gt;  OTFT. Referencia en el espacio del orden de carga.</summary>
        Public Property SleepingOutfit As UInteger
            Get
                Return Referencia("SOFT\Sleeping Outfit")
            End Get
            Set(value As UInteger)
                PonerReferencia("SOFT\Sleeping Outfit", value)
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

        ''' <summary>CRIF\Crime Faction  -&gt;  FACT. Referencia en el espacio del orden de carga.</summary>
        Public Property CrimeFaction As UInteger
            Get
                Return Referencia("CRIF\Crime Faction")
            End Get
            Set(value As UInteger)
                PonerReferencia("CRIF\Crime Faction", value)
            End Set
        End Property

        ''' <summary>FTST\Head Texture  -&gt;  TXST. Referencia en el espacio del orden de carga.</summary>
        Public Property HeadTexture As UInteger
            Get
                Return Referencia("FTST\Head Texture")
            End Get
            Set(value As UInteger)
                PonerReferencia("FTST\Head Texture", value)
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

        ''' <summary>QNAM\Texture lighting\Alpha</summary>
        Public Property TextureLightingAlpha As Single
            Get
                Return Flt("QNAM\Texture lighting\Alpha")
            End Get
            Set(value As Single)
                Escribir("QNAM\Texture lighting\Alpha", value)
            End Set
        End Property

        ''' <summary>MRSV\Body Morph Region Values\Head</summary>
        Public Property BodyMorphRegionValuesHead As Single
            Get
                Return Flt("MRSV\Body Morph Region Values\Head")
            End Get
            Set(value As Single)
                Escribir("MRSV\Body Morph Region Values\Head", value)
            End Set
        End Property

        ''' <summary>MRSV\Body Morph Region Values\Upper Torso</summary>
        Public Property BodyMorphRegionValuesUpperTorso As Single
            Get
                Return Flt("MRSV\Body Morph Region Values\Upper Torso")
            End Get
            Set(value As Single)
                Escribir("MRSV\Body Morph Region Values\Upper Torso", value)
            End Set
        End Property

        ''' <summary>MRSV\Body Morph Region Values\Arms</summary>
        Public Property BodyMorphRegionValuesArms As Single
            Get
                Return Flt("MRSV\Body Morph Region Values\Arms")
            End Get
            Set(value As Single)
                Escribir("MRSV\Body Morph Region Values\Arms", value)
            End Set
        End Property

        ''' <summary>MRSV\Body Morph Region Values\Lower Torso</summary>
        Public Property BodyMorphRegionValuesLowerTorso As Single
            Get
                Return Flt("MRSV\Body Morph Region Values\Lower Torso")
            End Get
            Set(value As Single)
                Escribir("MRSV\Body Morph Region Values\Lower Torso", value)
            End Set
        End Property

        ''' <summary>MRSV\Body Morph Region Values\Legs</summary>
        Public Property BodyMorphRegionValuesLegs As Single
            Get
                Return Flt("MRSV\Body Morph Region Values\Legs")
            End Get
            Set(value As Single)
                Escribir("MRSV\Body Morph Region Values\Legs", value)
            End Set
        End Property

        ''' <summary>FMIN\Facial Morph Intensity</summary>
        Public Property FacialMorphIntensity As Single
            Get
                Return Flt("FMIN\Facial Morph Intensity")
            End Get
            Set(value As Single)
                Escribir("FMIN\Facial Morph Intensity", value)
            End Set
        End Property

        ''' <summary>ATTX\Activate Text Override. Resuelto contra las tablas de texto si el archivo las usa.</summary>
        Public Property ActivateTextOverride As String
            Get
                Return TextoTraducible("ATTX\Activate Text Override")
            End Get
            Set(value As String)
                EscribirTextoTraducible("ATTX\Activate Text Override", value)
            End Set
        End Property

        ''' <summary>VMAD\Virtual Machine Adapter\Scripts</summary>
        Public ReadOnly Property Scripts As IReadOnlyList(Of NpcFO4_Scripts)
            Get
                Return Elementos(Of NpcFO4_Scripts)("VMAD\Virtual Machine Adapter\Scripts", Function(n) New NpcFO4_Scripts(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>VMAD\Virtual Machine Adapter\Scripts\Script\Properties</summary>
        Public ReadOnly Property Properties As IReadOnlyList(Of NpcFO4_Properties)
            Get
                Return Elementos(Of NpcFO4_Properties)("VMAD\Virtual Machine Adapter\Scripts\Script\Properties", Function(n) New NpcFO4_Properties(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Factions</summary>
        Public ReadOnly Property Factions As IReadOnlyList(Of NpcFO4_Factions)
            Get
                Return Elementos(Of NpcFO4_Factions)("Factions", Function(n) New NpcFO4_Factions(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Actor Effects</summary>
        Public ReadOnly Property ActorEffects As IReadOnlyList(Of NpcFO4_ActorEffects)
            Get
                Return Elementos(Of NpcFO4_ActorEffects)("Actor Effects", Function(n) New NpcFO4_ActorEffects(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Destructible\DAMC\Resistances</summary>
        Public ReadOnly Property Resistances As IReadOnlyList(Of NpcFO4_Resistances)
            Get
                Return Elementos(Of NpcFO4_Resistances)("Destructible\DAMC\Resistances", Function(n) New NpcFO4_Resistances(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Destructible\Stages</summary>
        Public ReadOnly Property Stages As IReadOnlyList(Of NpcFO4_Stages)
            Get
                Return Elementos(Of NpcFO4_Stages)("Destructible\Stages", Function(n) New NpcFO4_Stages(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Attacks</summary>
        Public ReadOnly Property Attacks As IReadOnlyList(Of NpcFO4_Attacks)
            Get
                Return Elementos(Of NpcFO4_Attacks)("Attacks", Function(n) New NpcFO4_Attacks(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Perks</summary>
        Public ReadOnly Property Perks As IReadOnlyList(Of NpcFO4_Perks)
            Get
                Return Elementos(Of NpcFO4_Perks)("Perks", Function(n) New NpcFO4_Perks(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>PRPS\Properties</summary>
        Public ReadOnly Property Properties2 As IReadOnlyList(Of NpcFO4_Properties2)
            Get
                Return Elementos(Of NpcFO4_Properties2)("PRPS\Properties", Function(n) New NpcFO4_Properties2(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Items</summary>
        Public ReadOnly Property Items As IReadOnlyList(Of NpcFO4_Items)
            Get
                Return Elementos(Of NpcFO4_Items)("Items", Function(n) New NpcFO4_Items(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Packages</summary>
        Public ReadOnly Property Packages As IReadOnlyList(Of NpcFO4_Packages)
            Get
                Return Elementos(Of NpcFO4_Packages)("Packages", Function(n) New NpcFO4_Packages(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Keywords\KWDA\Keywords</summary>
        Public ReadOnly Property Keywords As IReadOnlyList(Of NpcFO4_Keywords)
            Get
                Return Elementos(Of NpcFO4_Keywords)("Keywords\KWDA\Keywords", Function(n) New NpcFO4_Keywords(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>APPR\Attach Parent Slots</summary>
        Public ReadOnly Property AttachParentSlots As IReadOnlyList(Of NpcFO4_AttachParentSlots)
            Get
                Return Elementos(Of NpcFO4_AttachParentSlots)("APPR\Attach Parent Slots", Function(n) New NpcFO4_AttachParentSlots(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Object Template\Combinations</summary>
        Public ReadOnly Property Combinations As IReadOnlyList(Of NpcFO4_Combinations)
            Get
                Return Elementos(Of NpcFO4_Combinations)("Object Template\Combinations", Function(n) New NpcFO4_Combinations(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Object Template\Combinations\Combination\OBTS\Object Mod Template Item\Keywords</summary>
        Public ReadOnly Property Keywords2 As IReadOnlyList(Of NpcFO4_Keywords2)
            Get
                Return Elementos(Of NpcFO4_Keywords2)("Object Template\Combinations\Combination\OBTS\Object Mod Template Item\Keywords", Function(n) New NpcFO4_Keywords2(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Object Template\Combinations\Combination\OBTS\Object Mod Template Item\Includes</summary>
        Public ReadOnly Property Includes As IReadOnlyList(Of NpcFO4_Includes)
            Get
                Return Elementos(Of NpcFO4_Includes)("Object Template\Combinations\Combination\OBTS\Object Mod Template Item\Includes", Function(n) New NpcFO4_Includes(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Object Template\Combinations\Combination\OBTS\Object Mod Template Item\Properties</summary>
        Public ReadOnly Property Properties3 As IReadOnlyList(Of NpcFO4_Properties3)
            Get
                Return Elementos(Of NpcFO4_Properties3)("Object Template\Combinations\Combination\OBTS\Object Mod Template Item\Properties", Function(n) New NpcFO4_Properties3(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Head Parts</summary>
        Public ReadOnly Property HeadParts As IReadOnlyList(Of NpcFO4_HeadParts)
            Get
                Return Elementos(Of NpcFO4_HeadParts)("Head Parts", Function(n) New NpcFO4_HeadParts(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Actor Sounds\Sounds</summary>
        Public ReadOnly Property Sounds As IReadOnlyList(Of NpcFO4_Sounds)
            Get
                Return Elementos(Of NpcFO4_Sounds)("Actor Sounds\Sounds", Function(n) New NpcFO4_Sounds(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>MSDK\Morph Keys</summary>
        Public ReadOnly Property MorphKeys As IReadOnlyList(Of NpcFO4_MorphKeys)
            Get
                Return Elementos(Of NpcFO4_MorphKeys)("MSDK\Morph Keys", Function(n) New NpcFO4_MorphKeys(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>MSDV\Morph Values</summary>
        Public ReadOnly Property MorphValues As IReadOnlyList(Of NpcFO4_MorphValues)
            Get
                Return Elementos(Of NpcFO4_MorphValues)("MSDV\Morph Values", Function(n) New NpcFO4_MorphValues(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Face Tinting Layers</summary>
        Public ReadOnly Property FaceTintingLayers As IReadOnlyList(Of NpcFO4_FaceTintingLayers)
            Get
                Return Elementos(Of NpcFO4_FaceTintingLayers)("Face Tinting Layers", Function(n) New NpcFO4_FaceTintingLayers(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Face Morphs</summary>
        Public ReadOnly Property FaceMorphs As IReadOnlyList(Of NpcFO4_FaceMorphs)
            Get
                Return Elementos(Of NpcFO4_FaceMorphs)("Face Morphs", Function(n) New NpcFO4_FaceMorphs(n, Context, Resolver))
            End Get
        End Property

    End Class

    ''' <summary>Un elemento de VMAD\Virtual Machine Adapter\Scripts.</summary>
    Public NotInheritable Class NpcFO4_Scripts
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Script\ScriptName</summary>
        Public Property ScriptScriptName As String
            Get
                Return Txt("Script\ScriptName")
            End Get
            Set(value As String)
                Escribir("Script\ScriptName", value)
            End Set
        End Property

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
    Public NotInheritable Class NpcFO4_Properties
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Property\propertyName</summary>
        Public Property PropertyPropertyName As String
            Get
                Return Txt("Property\propertyName")
            End Get
            Set(value As String)
                Escribir("Property\propertyName", value)
            End Set
        End Property

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
    Public NotInheritable Class NpcFO4_Factions
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
    Public NotInheritable Class NpcFO4_ActorEffects
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>SPLO\Actor Effect  -&gt;  SPEL / LVSP. Referencia en el espacio del orden de carga.</summary>
        Public Property ActorEffect As UInteger
            Get
                Return Referencia("SPLO\Actor Effect")
            End Get
            Set(value As UInteger)
                PonerReferencia("SPLO\Actor Effect", value)
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Destructible\DAMC\Resistances.</summary>
    Public NotInheritable Class NpcFO4_Resistances
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Resistance\Damage Type  -&gt;  DMGT. Referencia en el espacio del orden de carga.</summary>
        Public Property ResistanceDamageType As UInteger
            Get
                Return Referencia("Resistance\Damage Type")
            End Get
            Set(value As UInteger)
                PonerReferencia("Resistance\Damage Type", value)
            End Set
        End Property

        ''' <summary>Resistance\Value</summary>
        Public Property ResistanceValue As UInteger
            Get
                Return CUInt(Entero("Resistance\Value"))
            End Get
            Set(value As UInteger)
                Escribir("Resistance\Value", CLng(value))
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Destructible\Stages.</summary>
    Public NotInheritable Class NpcFO4_Stages
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
        ''' <summary>Bit 4 de Stage\DSTD\Destruction Stage Data\Flags: Becomes Dynamic</summary>
        Public Property DestructionStageDataFlagsBecomesDynamic As Boolean
            Get
                Return Bit("Stage\DSTD\Destruction Stage Data\Flags", 4)
            End Get
            Set(value As Boolean)
                PonerBit("Stage\DSTD\Destruction Stage Data\Flags", 4, value)
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

        ''' <summary>Stage\DSTA\Sequence Name</summary>
        Public Property StageSequenceName As String
            Get
                Return Txt("Stage\DSTA\Sequence Name")
            End Get
            Set(value As String)
                Escribir("Stage\DSTA\Sequence Name", value)
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

        ''' <summary>Stage\Model\DMDC\Color Remapping Index</summary>
        Public Property ModelColorRemappingIndex As Single
            Get
                Return Flt("Stage\Model\DMDC\Color Remapping Index")
            End Get
            Set(value As Single)
                Escribir("Stage\Model\DMDC\Color Remapping Index", value)
            End Set
        End Property

        ''' <summary>Stage\Model\DMDS\Material Swap  -&gt;  MSWP. Referencia en el espacio del orden de carga.</summary>
        Public Property ModelMaterialSwap As UInteger
            Get
                Return Referencia("Stage\Model\DMDS\Material Swap")
            End Get
            Set(value As UInteger)
                PonerReferencia("Stage\Model\DMDS\Material Swap", value)
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Attacks.</summary>
    Public NotInheritable Class NpcFO4_Attacks
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

        ''' <summary>Attack\ATKD\Attack Data\Attack Spell  -&gt;  SPEL / NULL. Referencia en el espacio del orden de carga.</summary>
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
        ''' <summary>Bit 3 de Attack\ATKD\Attack Data\Attack Flags: Charge Attack</summary>
        Public Property AttackDataAttackFlagsChargeAttack As Boolean
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
        ''' <summary>Bit 5 de Attack\ATKD\Attack Data\Attack Flags: Continuous Attack</summary>
        Public Property AttackDataAttackFlagsContinuousAttack As Boolean
            Get
                Return Bit("Attack\ATKD\Attack Data\Attack Flags", 5)
            End Get
            Set(value As Boolean)
                PonerBit("Attack\ATKD\Attack Data\Attack Flags", 5, value)
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

        ''' <summary>Attack\ATKD\Attack Data\Action Points Mult</summary>
        Public Property AttackDataActionPointsMult As Single
            Get
                Return Flt("Attack\ATKD\Attack Data\Action Points Mult")
            End Get
            Set(value As Single)
                Escribir("Attack\ATKD\Attack Data\Action Points Mult", value)
            End Set
        End Property

        ''' <summary>Attack\ATKD\Attack Data\Stagger Offset</summary>
        Public Property AttackDataStaggerOffset As Integer
            Get
                Return CInt(Entero("Attack\ATKD\Attack Data\Stagger Offset"))
            End Get
            Set(value As Integer)
                Escribir("Attack\ATKD\Attack Data\Stagger Offset", CLng(value))
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

        ''' <summary>Attack\ATKW\Weapon Slot  -&gt;  EQUP. Referencia en el espacio del orden de carga.</summary>
        Public Property AttackWeaponSlot As UInteger
            Get
                Return Referencia("Attack\ATKW\Weapon Slot")
            End Get
            Set(value As UInteger)
                PonerReferencia("Attack\ATKW\Weapon Slot", value)
            End Set
        End Property

        ''' <summary>Attack\ATKS\Required Slot  -&gt;  EQUP. Referencia en el espacio del orden de carga.</summary>
        Public Property AttackRequiredSlot As UInteger
            Get
                Return Referencia("Attack\ATKS\Required Slot")
            End Get
            Set(value As UInteger)
                PonerReferencia("Attack\ATKS\Required Slot", value)
            End Set
        End Property

        ''' <summary>Attack\ATKT\Description</summary>
        Public Property AttackDescription As String
            Get
                Return Txt("Attack\ATKT\Description")
            End Get
            Set(value As String)
                Escribir("Attack\ATKT\Description", value)
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Perks.</summary>
    Public NotInheritable Class NpcFO4_Perks
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

    ''' <summary>Un elemento de PRPS\Properties.</summary>
    Public NotInheritable Class NpcFO4_Properties2
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Property\Actor Value  -&gt;  AVIF / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property PropertyActorValue As UInteger
            Get
                Return Referencia("Property\Actor Value")
            End Get
            Set(value As UInteger)
                PonerReferencia("Property\Actor Value", value)
            End Set
        End Property

        ''' <summary>Property\Value</summary>
        Public Property PropertyValue As Single
            Get
                Return Flt("Property\Value")
            End Get
            Set(value As Single)
                Escribir("Property\Value", value)
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Items.</summary>
    Public NotInheritable Class NpcFO4_Items
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Item\CNTO\Item\Item. Referencia en el espacio del orden de carga.</summary>
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
    Public NotInheritable Class NpcFO4_Packages
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
    Public NotInheritable Class NpcFO4_Keywords
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

    ''' <summary>Un elemento de APPR\Attach Parent Slots.</summary>
    Public NotInheritable Class NpcFO4_AttachParentSlots
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Keyword  -&gt;  KYWD. Referencia en el espacio del orden de carga.</summary>
        Public Property Keyword As UInteger
            Get
                Return Referencia("Keyword")
            End Get
            Set(value As UInteger)
                PonerReferencia("Keyword", value)
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Object Template\Combinations.</summary>
    Public NotInheritable Class NpcFO4_Combinations
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Combination\FULL\Name. Resuelto contra las tablas de texto si el archivo las usa.</summary>
        Public Property CombinationName As String
            Get
                Return TextoTraducible("Combination\FULL\Name")
            End Get
            Set(value As String)
                EscribirTextoTraducible("Combination\FULL\Name", value)
            End Set
        End Property

        ''' <summary>Combination\OBTS\Object Mod Template Item\Include Count</summary>
        Public Property ObjectModTemplateItemIncludeCount As UInteger
            Get
                Return CUInt(Entero("Combination\OBTS\Object Mod Template Item\Include Count"))
            End Get
            Set(value As UInteger)
                Escribir("Combination\OBTS\Object Mod Template Item\Include Count", CLng(value))
            End Set
        End Property

        ''' <summary>Combination\OBTS\Object Mod Template Item\Property Count</summary>
        Public Property ObjectModTemplateItemPropertyCount As UInteger
            Get
                Return CUInt(Entero("Combination\OBTS\Object Mod Template Item\Property Count"))
            End Get
            Set(value As UInteger)
                Escribir("Combination\OBTS\Object Mod Template Item\Property Count", CLng(value))
            End Set
        End Property

        ''' <summary>Combination\OBTS\Object Mod Template Item\Level Min</summary>
        Public Property ObjectModTemplateItemLevelMin As Byte
            Get
                Return CByte(Entero("Combination\OBTS\Object Mod Template Item\Level Min"))
            End Get
            Set(value As Byte)
                Escribir("Combination\OBTS\Object Mod Template Item\Level Min", CLng(value))
            End Set
        End Property

        ''' <summary>Combination\OBTS\Object Mod Template Item\Level Max</summary>
        Public Property ObjectModTemplateItemLevelMax As Byte
            Get
                Return CByte(Entero("Combination\OBTS\Object Mod Template Item\Level Max"))
            End Get
            Set(value As Byte)
                Escribir("Combination\OBTS\Object Mod Template Item\Level Max", CLng(value))
            End Set
        End Property

        ''' <summary>Combination\OBTS\Object Mod Template Item\Parent Combination Index</summary>
        Public Property ObjectModTemplateItemParentCombinationIndex As Short
            Get
                Return CShort(Entero("Combination\OBTS\Object Mod Template Item\Parent Combination Index"))
            End Get
            Set(value As Short)
                Escribir("Combination\OBTS\Object Mod Template Item\Parent Combination Index", CLng(value))
            End Set
        End Property

        ''' <summary>Combination\OBTS\Object Mod Template Item\Default</summary>
        Public Property ObjectModTemplateItemDefault As Byte
            Get
                Return CByte(Entero("Combination\OBTS\Object Mod Template Item\Default"))
            End Get
            Set(value As Byte)
                Escribir("Combination\OBTS\Object Mod Template Item\Default", CLng(value))
            End Set
        End Property

        ''' <summary>Combination\OBTS\Object Mod Template Item\Min Level For Ranks</summary>
        Public Property ObjectModTemplateItemMinLevelForRanks As Byte
            Get
                Return CByte(Entero("Combination\OBTS\Object Mod Template Item\Min Level For Ranks"))
            End Get
            Set(value As Byte)
                Escribir("Combination\OBTS\Object Mod Template Item\Min Level For Ranks", CLng(value))
            End Set
        End Property

        ''' <summary>Combination\OBTS\Object Mod Template Item\Alt Levels Per Tier</summary>
        Public Property ObjectModTemplateItemAltLevelsPerTier As Byte
            Get
                Return CByte(Entero("Combination\OBTS\Object Mod Template Item\Alt Levels Per Tier"))
            End Get
            Set(value As Byte)
                Escribir("Combination\OBTS\Object Mod Template Item\Alt Levels Per Tier", CLng(value))
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Object Template\Combinations\Combination\OBTS\Object Mod Template Item\Keywords.</summary>
    Public NotInheritable Class NpcFO4_Keywords2
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

    ''' <summary>Un elemento de Object Template\Combinations\Combination\OBTS\Object Mod Template Item\Includes.</summary>
    Public NotInheritable Class NpcFO4_Includes
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Include\Mod  -&gt;  OMOD. Referencia en el espacio del orden de carga.</summary>
        Public Property IncludeMod As UInteger
            Get
                Return Referencia("Include\Mod")
            End Get
            Set(value As UInteger)
                PonerReferencia("Include\Mod", value)
            End Set
        End Property

        ''' <summary>Include\Attach Point Index</summary>
        Public Property IncludeAttachPointIndex As Byte
            Get
                Return CByte(Entero("Include\Attach Point Index"))
            End Get
            Set(value As Byte)
                Escribir("Include\Attach Point Index", CLng(value))
            End Set
        End Property

        ''' <summary>Include\Optional</summary>
        Public Property IncludeOptional As Byte
            Get
                Return CByte(Entero("Include\Optional"))
            End Get
            Set(value As Byte)
                Escribir("Include\Optional", CLng(value))
            End Set
        End Property

        ''' <summary>Include\Don't Use All</summary>
        Public Property IncludeDonTUseAll As Byte
            Get
                Return CByte(Entero("Include\Don't Use All"))
            End Get
            Set(value As Byte)
                Escribir("Include\Don't Use All", CLng(value))
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Object Template\Combinations\Combination\OBTS\Object Mod Template Item\Properties.</summary>
    Public NotInheritable Class NpcFO4_Properties3
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Property\Value Type</summary>
        Public Property PropertyValueType As Byte
            Get
                Return CByte(Entero("Property\Value Type"))
            End Get
            Set(value As Byte)
                Escribir("Property\Value Type", CLng(value))
            End Set
        End Property
        ''' <summary>Nombre del valor de Property\Value Type.</summary>
        Public ReadOnly Property PropertyValueTypeNombre As String
            Get
                Return NombreDeValor("Property\Value Type")
            End Get
        End Property


        ''' <summary>Property\Function Type</summary>
        Public Property PropertyFunctionType As Byte
            Get
                Return CByte(Entero("Property\Function Type"))
            End Get
            Set(value As Byte)
                Escribir("Property\Function Type", CLng(value))
            End Set
        End Property
        ''' <summary>Nombre del valor de Property\Function Type.</summary>
        Public ReadOnly Property PropertyFunctionTypeNombre As String
            Get
                Return NombreDeValor("Property\Function Type")
            End Get
        End Property


        ''' <summary>Property\Property</summary>
        Public Property PropertyProperty As UShort
            Get
                Return CUShort(Entero("Property\Property"))
            End Get
            Set(value As UShort)
                Escribir("Property\Property", CLng(value))
            End Set
        End Property

        ''' <summary>Property\Value 1 - Unknown</summary>
        Public Property PropertyValue1Unknown As Byte()
            Get
                Return Bytes("Property\Value 1 - Unknown")
            End Get
            Set(value As Byte())
                Escribir("Property\Value 1 - Unknown", value)
            End Set
        End Property

        ''' <summary>Property\Step</summary>
        Public Property PropertyStep As Single
            Get
                Return Flt("Property\Step")
            End Get
            Set(value As Single)
                Escribir("Property\Step", value)
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Head Parts.</summary>
    Public NotInheritable Class NpcFO4_HeadParts
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

    ''' <summary>Un elemento de Actor Sounds\Sounds.</summary>
    Public NotInheritable Class NpcFO4_Sounds
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Sound\CS2K\Keyword  -&gt;  KYWD. Referencia en el espacio del orden de carga.</summary>
        Public Property SoundKeyword As UInteger
            Get
                Return Referencia("Sound\CS2K\Keyword")
            End Get
            Set(value As UInteger)
                PonerReferencia("Sound\CS2K\Keyword", value)
            End Set
        End Property

        ''' <summary>Sound\CS2D\Sound  -&gt;  SNDR. Referencia en el espacio del orden de carga.</summary>
        Public Property SoundSound As UInteger
            Get
                Return Referencia("Sound\CS2D\Sound")
            End Get
            Set(value As UInteger)
                PonerReferencia("Sound\CS2D\Sound", value)
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de MSDK\Morph Keys.</summary>
    Public NotInheritable Class NpcFO4_MorphKeys
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Key</summary>
        Public Property Key As UInteger
            Get
                Return CUInt(Entero("Key"))
            End Get
            Set(value As UInteger)
                Escribir("Key", CLng(value))
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de MSDV\Morph Values.</summary>
    Public NotInheritable Class NpcFO4_MorphValues
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Value</summary>
        Public Property Value As Single
            Get
                Return Flt("Value")
            End Get
            Set(value As Single)
                Escribir("Value", value)
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Face Tinting Layers.</summary>
    Public NotInheritable Class NpcFO4_FaceTintingLayers
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Layer\TETI\Index\Data Type</summary>
        Public Property IndexDataType As UShort
            Get
                Return CUShort(Entero("Layer\TETI\Index\Data Type"))
            End Get
            Set(value As UShort)
                Escribir("Layer\TETI\Index\Data Type", CLng(value))
            End Set
        End Property
        ''' <summary>Nombre del valor de Layer\TETI\Index\Data Type.</summary>
        Public ReadOnly Property IndexDataTypeNombre As String
            Get
                Return NombreDeValor("Layer\TETI\Index\Data Type")
            End Get
        End Property


        ''' <summary>Layer\TETI\Index\Index</summary>
        Public Property IndexIndex As UShort
            Get
                Return CUShort(Entero("Layer\TETI\Index\Index"))
            End Get
            Set(value As UShort)
                Escribir("Layer\TETI\Index\Index", CLng(value))
            End Set
        End Property

        ''' <summary>Layer\TEND\Data\Value</summary>
        Public Property DataValue As Byte
            Get
                Return CByte(Entero("Layer\TEND\Data\Value"))
            End Get
            Set(value As Byte)
                Escribir("Layer\TEND\Data\Value", CLng(value))
            End Set
        End Property

        ''' <summary>Layer\TEND\Data\Color\Red</summary>
        Public Property ColorRed As Byte
            Get
                Return CByte(Entero("Layer\TEND\Data\Color\Red"))
            End Get
            Set(value As Byte)
                Escribir("Layer\TEND\Data\Color\Red", CLng(value))
            End Set
        End Property

        ''' <summary>Layer\TEND\Data\Color\Green</summary>
        Public Property ColorGreen As Byte
            Get
                Return CByte(Entero("Layer\TEND\Data\Color\Green"))
            End Get
            Set(value As Byte)
                Escribir("Layer\TEND\Data\Color\Green", CLng(value))
            End Set
        End Property

        ''' <summary>Layer\TEND\Data\Color\Blue</summary>
        Public Property ColorBlue As Byte
            Get
                Return CByte(Entero("Layer\TEND\Data\Color\Blue"))
            End Get
            Set(value As Byte)
                Escribir("Layer\TEND\Data\Color\Blue", CLng(value))
            End Set
        End Property

        ''' <summary>Layer\TEND\Data\Template Color Index</summary>
        Public Property DataTemplateColorIndex As Short
            Get
                Return CShort(Entero("Layer\TEND\Data\Template Color Index"))
            End Get
            Set(value As Short)
                Escribir("Layer\TEND\Data\Template Color Index", CLng(value))
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Face Morphs.</summary>
    Public NotInheritable Class NpcFO4_FaceMorphs
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Face Morph\FMRI\Index</summary>
        Public Property FaceMorphIndex As UInteger
            Get
                Return CUInt(Entero("Face Morph\FMRI\Index"))
            End Get
            Set(value As UInteger)
                Escribir("Face Morph\FMRI\Index", CLng(value))
            End Set
        End Property

        ''' <summary>Face Morph\FMRS\Values\Position - X</summary>
        Public Property ValuesPositionX As Single
            Get
                Return Flt("Face Morph\FMRS\Values\Position - X")
            End Get
            Set(value As Single)
                Escribir("Face Morph\FMRS\Values\Position - X", value)
            End Set
        End Property

        ''' <summary>Face Morph\FMRS\Values\Position - Y</summary>
        Public Property ValuesPositionY As Single
            Get
                Return Flt("Face Morph\FMRS\Values\Position - Y")
            End Get
            Set(value As Single)
                Escribir("Face Morph\FMRS\Values\Position - Y", value)
            End Set
        End Property

        ''' <summary>Face Morph\FMRS\Values\Position - Z</summary>
        Public Property ValuesPositionZ As Single
            Get
                Return Flt("Face Morph\FMRS\Values\Position - Z")
            End Get
            Set(value As Single)
                Escribir("Face Morph\FMRS\Values\Position - Z", value)
            End Set
        End Property

        ''' <summary>Face Morph\FMRS\Values\Rotation - X</summary>
        Public Property ValuesRotationX As Single
            Get
                Return Flt("Face Morph\FMRS\Values\Rotation - X")
            End Get
            Set(value As Single)
                Escribir("Face Morph\FMRS\Values\Rotation - X", value)
            End Set
        End Property

        ''' <summary>Face Morph\FMRS\Values\Rotation - Y</summary>
        Public Property ValuesRotationY As Single
            Get
                Return Flt("Face Morph\FMRS\Values\Rotation - Y")
            End Get
            Set(value As Single)
                Escribir("Face Morph\FMRS\Values\Rotation - Y", value)
            End Set
        End Property

        ''' <summary>Face Morph\FMRS\Values\Rotation - Z</summary>
        Public Property ValuesRotationZ As Single
            Get
                Return Flt("Face Morph\FMRS\Values\Rotation - Z")
            End Get
            Set(value As Single)
                Escribir("Face Morph\FMRS\Values\Rotation - Z", value)
            End Set
        End Property

        ''' <summary>Face Morph\FMRS\Values\Scale</summary>
        Public Property ValuesScale As Single
            Get
                Return Flt("Face Morph\FMRS\Values\Scale")
            End Get
            Set(value As Single)
                Escribir("Face Morph\FMRS\Values\Scale", value)
            End Set
        End Property

    End Class

    ''' <summary>Campos de un record OMOD de Fallout 4.</summary>
    Public NotInheritable Class OmodFO4
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

        ''' <summary>FULL\Name. Resuelto contra las tablas de texto si el archivo las usa.</summary>
        Public Property Name As String
            Get
                Return TextoTraducible("FULL\Name")
            End Get
            Set(value As String)
                EscribirTextoTraducible("FULL\Name", value)
            End Set
        End Property

        ''' <summary>DESC\Description. Resuelto contra las tablas de texto si el archivo las usa.</summary>
        Public Property Description As String
            Get
                Return TextoTraducible("DESC\Description")
            End Get
            Set(value As String)
                EscribirTextoTraducible("DESC\Description", value)
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

        ''' <summary>Model\MODC\Color Remapping Index</summary>
        Public Property ModelColorRemappingIndex As Single
            Get
                Return Flt("Model\MODC\Color Remapping Index")
            End Get
            Set(value As Single)
                Escribir("Model\MODC\Color Remapping Index", value)
            End Set
        End Property

        ''' <summary>Model\MODS\Material Swap  -&gt;  MSWP. Referencia en el espacio del orden de carga.</summary>
        Public Property ModelMaterialSwap As UInteger
            Get
                Return Referencia("Model\MODS\Material Swap")
            End Get
            Set(value As UInteger)
                PonerReferencia("Model\MODS\Material Swap", value)
            End Set
        End Property

        ''' <summary>Model\MODF\Flags</summary>
        Public Property ModelFlags As Byte
            Get
                Return CByte(Entero("Model\MODF\Flags"))
            End Get
            Set(value As Byte)
                Escribir("Model\MODF\Flags", CLng(value))
            End Set
        End Property

        ''' <summary>DATA\Data\Include Count</summary>
        Public Property DataIncludeCount As UInteger
            Get
                Return CUInt(Entero("DATA\Data\Include Count"))
            End Get
            Set(value As UInteger)
                Escribir("DATA\Data\Include Count", CLng(value))
            End Set
        End Property

        ''' <summary>DATA\Data\Property Count</summary>
        Public Property DataPropertyCount As UInteger
            Get
                Return CUInt(Entero("DATA\Data\Property Count"))
            End Get
            Set(value As UInteger)
                Escribir("DATA\Data\Property Count", CLng(value))
            End Set
        End Property

        ''' <summary>DATA\Data\Unknown Bool 1</summary>
        Public Property DataUnknownBool1 As Byte
            Get
                Return CByte(Entero("DATA\Data\Unknown Bool 1"))
            End Get
            Set(value As Byte)
                Escribir("DATA\Data\Unknown Bool 1", CLng(value))
            End Set
        End Property

        ''' <summary>DATA\Data\Unknown Bool 2</summary>
        Public Property DataUnknownBool2 As Byte
            Get
                Return CByte(Entero("DATA\Data\Unknown Bool 2"))
            End Get
            Set(value As Byte)
                Escribir("DATA\Data\Unknown Bool 2", CLng(value))
            End Set
        End Property

        ''' <summary>DATA\Data\Form Type</summary>
        Public Property DataFormType As UInteger
            Get
                Return CUInt(Entero("DATA\Data\Form Type"))
            End Get
            Set(value As UInteger)
                Escribir("DATA\Data\Form Type", CLng(value))
            End Set
        End Property

        ''' <summary>DATA\Data\Max Rank</summary>
        Public Property DataMaxRank As Byte
            Get
                Return CByte(Entero("DATA\Data\Max Rank"))
            End Get
            Set(value As Byte)
                Escribir("DATA\Data\Max Rank", CLng(value))
            End Set
        End Property

        ''' <summary>DATA\Data\Level Tier Scaled Offset</summary>
        Public Property DataLevelTierScaledOffset As Byte
            Get
                Return CByte(Entero("DATA\Data\Level Tier Scaled Offset"))
            End Get
            Set(value As Byte)
                Escribir("DATA\Data\Level Tier Scaled Offset", CLng(value))
            End Set
        End Property

        ''' <summary>DATA\Data\Attach Point  -&gt;  KYWD / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property DataAttachPoint As UInteger
            Get
                Return Referencia("DATA\Data\Attach Point")
            End Get
            Set(value As UInteger)
                PonerReferencia("DATA\Data\Attach Point", value)
            End Set
        End Property

        ''' <summary>LNAM\Loose Mod. Referencia en el espacio del orden de carga.</summary>
        Public Property LooseMod As UInteger
            Get
                Return Referencia("LNAM\Loose Mod")
            End Get
            Set(value As UInteger)
                PonerReferencia("LNAM\Loose Mod", value)
            End Set
        End Property

        ''' <summary>NAM1\Priority</summary>
        Public Property Priority As Byte
            Get
                Return CByte(Entero("NAM1\Priority"))
            End Get
            Set(value As Byte)
                Escribir("NAM1\Priority", CLng(value))
            End Set
        End Property

        ''' <summary>FLTR\Filter</summary>
        Public Property Filter As String
            Get
                Return Txt("FLTR\Filter")
            End Get
            Set(value As String)
                Escribir("FLTR\Filter", value)
            End Set
        End Property

        ''' <summary>DATA\Data\Attach Parent Slots</summary>
        Public ReadOnly Property AttachParentSlots As IReadOnlyList(Of OmodFO4_AttachParentSlots)
            Get
                Return Elementos(Of OmodFO4_AttachParentSlots)("DATA\Data\Attach Parent Slots", Function(n) New OmodFO4_AttachParentSlots(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>DATA\Data\Items</summary>
        Public ReadOnly Property Items As IReadOnlyList(Of OmodFO4_Items)
            Get
                Return Elementos(Of OmodFO4_Items)("DATA\Data\Items", Function(n) New OmodFO4_Items(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>DATA\Data\Includes</summary>
        Public ReadOnly Property Includes As IReadOnlyList(Of OmodFO4_Includes)
            Get
                Return Elementos(Of OmodFO4_Includes)("DATA\Data\Includes", Function(n) New OmodFO4_Includes(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>DATA\Data\Properties</summary>
        Public ReadOnly Property Properties As IReadOnlyList(Of OmodFO4_Properties)
            Get
                Return Elementos(Of OmodFO4_Properties)("DATA\Data\Properties", Function(n) New OmodFO4_Properties(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>MNAM\Target OMOD Keywords</summary>
        Public ReadOnly Property TargetOMODKeywords As IReadOnlyList(Of OmodFO4_TargetOMODKeywords)
            Get
                Return Elementos(Of OmodFO4_TargetOMODKeywords)("MNAM\Target OMOD Keywords", Function(n) New OmodFO4_TargetOMODKeywords(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>FNAM\Filter Keywords</summary>
        Public ReadOnly Property FilterKeywords As IReadOnlyList(Of OmodFO4_FilterKeywords)
            Get
                Return Elementos(Of OmodFO4_FilterKeywords)("FNAM\Filter Keywords", Function(n) New OmodFO4_FilterKeywords(n, Context, Resolver))
            End Get
        End Property

    End Class

    ''' <summary>Un elemento de DATA\Data\Attach Parent Slots.</summary>
    Public NotInheritable Class OmodFO4_AttachParentSlots
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

    ''' <summary>Un elemento de DATA\Data\Items.</summary>
    Public NotInheritable Class OmodFO4_Items
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Item\Value 1</summary>
        Public Property ItemValue1 As Byte()
            Get
                Return Bytes("Item\Value 1")
            End Get
            Set(value As Byte())
                Escribir("Item\Value 1", value)
            End Set
        End Property

        ''' <summary>Item\Value 2</summary>
        Public Property ItemValue2 As Byte()
            Get
                Return Bytes("Item\Value 2")
            End Get
            Set(value As Byte())
                Escribir("Item\Value 2", value)
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de DATA\Data\Includes.</summary>
    Public NotInheritable Class OmodFO4_Includes
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Include\Mod  -&gt;  OMOD. Referencia en el espacio del orden de carga.</summary>
        Public Property IncludeMod As UInteger
            Get
                Return Referencia("Include\Mod")
            End Get
            Set(value As UInteger)
                PonerReferencia("Include\Mod", value)
            End Set
        End Property

        ''' <summary>Include\Minimum Level</summary>
        Public Property IncludeMinimumLevel As Byte
            Get
                Return CByte(Entero("Include\Minimum Level"))
            End Get
            Set(value As Byte)
                Escribir("Include\Minimum Level", CLng(value))
            End Set
        End Property

        ''' <summary>Include\Optional</summary>
        Public Property IncludeOptional As Byte
            Get
                Return CByte(Entero("Include\Optional"))
            End Get
            Set(value As Byte)
                Escribir("Include\Optional", CLng(value))
            End Set
        End Property

        ''' <summary>Include\Don't Use All</summary>
        Public Property IncludeDonTUseAll As Byte
            Get
                Return CByte(Entero("Include\Don't Use All"))
            End Get
            Set(value As Byte)
                Escribir("Include\Don't Use All", CLng(value))
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de DATA\Data\Properties.</summary>
    Public NotInheritable Class OmodFO4_Properties
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Property\Value Type</summary>
        Public Property PropertyValueType As Byte
            Get
                Return CByte(Entero("Property\Value Type"))
            End Get
            Set(value As Byte)
                Escribir("Property\Value Type", CLng(value))
            End Set
        End Property
        ''' <summary>Nombre del valor de Property\Value Type.</summary>
        Public ReadOnly Property PropertyValueTypeNombre As String
            Get
                Return NombreDeValor("Property\Value Type")
            End Get
        End Property


        ''' <summary>Property\Function Type</summary>
        Public Property PropertyFunctionType As Byte
            Get
                Return CByte(Entero("Property\Function Type"))
            End Get
            Set(value As Byte)
                Escribir("Property\Function Type", CLng(value))
            End Set
        End Property
        ''' <summary>Nombre del valor de Property\Function Type.</summary>
        Public ReadOnly Property PropertyFunctionTypeNombre As String
            Get
                Return NombreDeValor("Property\Function Type")
            End Get
        End Property


        ''' <summary>Property\Property</summary>
        Public Property PropertyProperty As UShort
            Get
                Return CUShort(Entero("Property\Property"))
            End Get
            Set(value As UShort)
                Escribir("Property\Property", CLng(value))
            End Set
        End Property

        ''' <summary>Property\Value 1 - Unknown</summary>
        Public Property PropertyValue1Unknown As Byte()
            Get
                Return Bytes("Property\Value 1 - Unknown")
            End Get
            Set(value As Byte())
                Escribir("Property\Value 1 - Unknown", value)
            End Set
        End Property

        ''' <summary>Property\Step</summary>
        Public Property PropertyStep As Single
            Get
                Return Flt("Property\Step")
            End Get
            Set(value As Single)
                Escribir("Property\Step", value)
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de MNAM\Target OMOD Keywords.</summary>
    Public NotInheritable Class OmodFO4_TargetOMODKeywords
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Keyword  -&gt;  KYWD. Referencia en el espacio del orden de carga.</summary>
        Public Property Keyword As UInteger
            Get
                Return Referencia("Keyword")
            End Get
            Set(value As UInteger)
                PonerReferencia("Keyword", value)
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de FNAM\Filter Keywords.</summary>
    Public NotInheritable Class OmodFO4_FilterKeywords
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Keyword  -&gt;  KYWD. Referencia en el espacio del orden de carga.</summary>
        Public Property Keyword As UInteger
            Get
                Return Referencia("Keyword")
            End Get
            Set(value As UInteger)
                PonerReferencia("Keyword", value)
            End Set
        End Property

    End Class

    ''' <summary>Campos de un record OTFT de Fallout 4.</summary>
    Public NotInheritable Class OtftFO4
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
        Public ReadOnly Property Items As IReadOnlyList(Of OtftFO4_Items)
            Get
                Return Elementos(Of OtftFO4_Items)("INAM\Items", Function(n) New OtftFO4_Items(n, Context, Resolver))
            End Get
        End Property

    End Class

    ''' <summary>Un elemento de INAM\Items.</summary>
    Public NotInheritable Class OtftFO4_Items
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

    ''' <summary>Campos de un record RACE de Fallout 4.</summary>
    Public NotInheritable Class RaceFO4
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

        ''' <summary>STCP\Animation Sound  -&gt;  STAG. Referencia en el espacio del orden de carga.</summary>
        Public Property AnimationSound As UInteger
            Get
                Return Referencia("STCP\Animation Sound")
            End Get
            Set(value As UInteger)
                PonerReferencia("STCP\Animation Sound", value)
            End Set
        End Property

        ''' <summary>FULL\Name. Resuelto contra las tablas de texto si el archivo las usa.</summary>
        Public Property Name As String
            Get
                Return TextoTraducible("FULL\Name")
            End Get
            Set(value As String)
                EscribirTextoTraducible("FULL\Name", value)
            End Set
        End Property

        ''' <summary>DESC\Description. Resuelto contra las tablas de texto si el archivo las usa.</summary>
        Public Property Description As String
            Get
                Return TextoTraducible("DESC\Description")
            End Get
            Set(value As String)
                EscribirTextoTraducible("DESC\Description", value)
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

        ''' <summary>Keywords\KSIZ\Keyword Count</summary>
        Public Property KeywordsKeywordCount As UInteger
            Get
                Return CUInt(Entero("Keywords\KSIZ\Keyword Count"))
            End Get
            Set(value As UInteger)
                Escribir("Keywords\KSIZ\Keyword Count", CLng(value))
            End Set
        End Property

        ''' <summary>DATA\Data\Male Height</summary>
        Public Property DataMaleHeight As Single
            Get
                Return Flt("DATA\Data\Male Height")
            End Get
            Set(value As Single)
                Escribir("DATA\Data\Male Height", value)
            End Set
        End Property

        ''' <summary>DATA\Data\Female Height</summary>
        Public Property DataFemaleHeight As Single
            Get
                Return Flt("DATA\Data\Female Height")
            End Get
            Set(value As Single)
                Escribir("DATA\Data\Female Height", value)
            End Set
        End Property

        ''' <summary>DATA\Data\Male Default Weight\Thin</summary>
        Public Property MaleDefaultWeightThin As Single
            Get
                Return Flt("DATA\Data\Male Default Weight\Thin")
            End Get
            Set(value As Single)
                Escribir("DATA\Data\Male Default Weight\Thin", value)
            End Set
        End Property

        ''' <summary>DATA\Data\Male Default Weight\Muscular</summary>
        Public Property MaleDefaultWeightMuscular As Single
            Get
                Return Flt("DATA\Data\Male Default Weight\Muscular")
            End Get
            Set(value As Single)
                Escribir("DATA\Data\Male Default Weight\Muscular", value)
            End Set
        End Property

        ''' <summary>DATA\Data\Male Default Weight\Fat</summary>
        Public Property MaleDefaultWeightFat As Single
            Get
                Return Flt("DATA\Data\Male Default Weight\Fat")
            End Get
            Set(value As Single)
                Escribir("DATA\Data\Male Default Weight\Fat", value)
            End Set
        End Property

        ''' <summary>DATA\Data\Female Default Weight\Thin</summary>
        Public Property FemaleDefaultWeightThin As Single
            Get
                Return Flt("DATA\Data\Female Default Weight\Thin")
            End Get
            Set(value As Single)
                Escribir("DATA\Data\Female Default Weight\Thin", value)
            End Set
        End Property

        ''' <summary>DATA\Data\Female Default Weight\Muscular</summary>
        Public Property FemaleDefaultWeightMuscular As Single
            Get
                Return Flt("DATA\Data\Female Default Weight\Muscular")
            End Get
            Set(value As Single)
                Escribir("DATA\Data\Female Default Weight\Muscular", value)
            End Set
        End Property

        ''' <summary>DATA\Data\Female Default Weight\Fat</summary>
        Public Property FemaleDefaultWeightFat As Single
            Get
                Return Flt("DATA\Data\Female Default Weight\Fat")
            End Get
            Set(value As Single)
                Escribir("DATA\Data\Female Default Weight\Fat", value)
            End Set
        End Property

        ''' <summary>DATA\Data\Flags</summary>
        Public Property DataFlags As UInteger
            Get
                Return CUInt(Entero("DATA\Data\Flags"))
            End Get
            Set(value As UInteger)
                Escribir("DATA\Data\Flags", CLng(value))
            End Set
        End Property

        ''' <summary>Bit 0 de DATA\Data\Flags: Playable</summary>
        Public Property DataFlagsPlayable As Boolean
            Get
                Return Bit("DATA\Data\Flags", 0)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Data\Flags", 0, value)
            End Set
        End Property
        ''' <summary>Bit 1 de DATA\Data\Flags: FaceGen Head</summary>
        Public Property DataFlagsFaceGenHead As Boolean
            Get
                Return Bit("DATA\Data\Flags", 1)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Data\Flags", 1, value)
            End Set
        End Property
        ''' <summary>Bit 2 de DATA\Data\Flags: Child</summary>
        Public Property DataFlagsChild As Boolean
            Get
                Return Bit("DATA\Data\Flags", 2)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Data\Flags", 2, value)
            End Set
        End Property
        ''' <summary>Bit 3 de DATA\Data\Flags: Tilt Front/Back</summary>
        Public Property DataFlagsTiltFrontBack As Boolean
            Get
                Return Bit("DATA\Data\Flags", 3)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Data\Flags", 3, value)
            End Set
        End Property
        ''' <summary>Bit 4 de DATA\Data\Flags: Tilt Left/Right</summary>
        Public Property DataFlagsTiltLeftRight As Boolean
            Get
                Return Bit("DATA\Data\Flags", 4)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Data\Flags", 4, value)
            End Set
        End Property
        ''' <summary>Bit 5 de DATA\Data\Flags: No Shadow</summary>
        Public Property DataFlagsNoShadow As Boolean
            Get
                Return Bit("DATA\Data\Flags", 5)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Data\Flags", 5, value)
            End Set
        End Property
        ''' <summary>Bit 6 de DATA\Data\Flags: Swims</summary>
        Public Property DataFlagsSwims As Boolean
            Get
                Return Bit("DATA\Data\Flags", 6)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Data\Flags", 6, value)
            End Set
        End Property
        ''' <summary>Bit 7 de DATA\Data\Flags: Flies</summary>
        Public Property DataFlagsFlies As Boolean
            Get
                Return Bit("DATA\Data\Flags", 7)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Data\Flags", 7, value)
            End Set
        End Property
        ''' <summary>Bit 8 de DATA\Data\Flags: Walks</summary>
        Public Property DataFlagsWalks As Boolean
            Get
                Return Bit("DATA\Data\Flags", 8)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Data\Flags", 8, value)
            End Set
        End Property
        ''' <summary>Bit 9 de DATA\Data\Flags: Immobile</summary>
        Public Property DataFlagsImmobile As Boolean
            Get
                Return Bit("DATA\Data\Flags", 9)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Data\Flags", 9, value)
            End Set
        End Property
        ''' <summary>Bit 10 de DATA\Data\Flags: Not Pushable</summary>
        Public Property DataFlagsNotPushable As Boolean
            Get
                Return Bit("DATA\Data\Flags", 10)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Data\Flags", 10, value)
            End Set
        End Property
        ''' <summary>Bit 11 de DATA\Data\Flags: No Combat In Water</summary>
        Public Property DataFlagsNoCombatInWater As Boolean
            Get
                Return Bit("DATA\Data\Flags", 11)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Data\Flags", 11, value)
            End Set
        End Property
        ''' <summary>Bit 12 de DATA\Data\Flags: No Rotating to Head-Track</summary>
        Public Property DataFlagsNoRotatingToHeadTrack As Boolean
            Get
                Return Bit("DATA\Data\Flags", 12)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Data\Flags", 12, value)
            End Set
        End Property
        ''' <summary>Bit 13 de DATA\Data\Flags: Don't Show Blood Spray</summary>
        Public Property DataFlagsDonTShowBloodSpray As Boolean
            Get
                Return Bit("DATA\Data\Flags", 13)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Data\Flags", 13, value)
            End Set
        End Property
        ''' <summary>Bit 14 de DATA\Data\Flags: Don't Show Blood Decal</summary>
        Public Property DataFlagsDonTShowBloodDecal As Boolean
            Get
                Return Bit("DATA\Data\Flags", 14)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Data\Flags", 14, value)
            End Set
        End Property
        ''' <summary>Bit 15 de DATA\Data\Flags: Uses Head Track Anims</summary>
        Public Property DataFlagsUsesHeadTrackAnims As Boolean
            Get
                Return Bit("DATA\Data\Flags", 15)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Data\Flags", 15, value)
            End Set
        End Property
        ''' <summary>Bit 16 de DATA\Data\Flags: Spells Align w/Magic Node</summary>
        Public Property DataFlagsSpellsAlignWMagicNode As Boolean
            Get
                Return Bit("DATA\Data\Flags", 16)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Data\Flags", 16, value)
            End Set
        End Property
        ''' <summary>Bit 17 de DATA\Data\Flags: Use World Raycasts For FootIK</summary>
        Public Property DataFlagsUseWorldRaycastsForFootIK As Boolean
            Get
                Return Bit("DATA\Data\Flags", 17)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Data\Flags", 17, value)
            End Set
        End Property
        ''' <summary>Bit 18 de DATA\Data\Flags: Allow Ragdoll Collision</summary>
        Public Property DataFlagsAllowRagdollCollision As Boolean
            Get
                Return Bit("DATA\Data\Flags", 18)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Data\Flags", 18, value)
            End Set
        End Property
        ''' <summary>Bit 19 de DATA\Data\Flags: Regen HP In Combat</summary>
        Public Property DataFlagsRegenHPInCombat As Boolean
            Get
                Return Bit("DATA\Data\Flags", 19)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Data\Flags", 19, value)
            End Set
        End Property
        ''' <summary>Bit 20 de DATA\Data\Flags: Can't Open Doors</summary>
        Public Property DataFlagsCanTOpenDoors As Boolean
            Get
                Return Bit("DATA\Data\Flags", 20)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Data\Flags", 20, value)
            End Set
        End Property
        ''' <summary>Bit 21 de DATA\Data\Flags: Allow PC Dialogue</summary>
        Public Property DataFlagsAllowPCDialogue As Boolean
            Get
                Return Bit("DATA\Data\Flags", 21)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Data\Flags", 21, value)
            End Set
        End Property
        ''' <summary>Bit 22 de DATA\Data\Flags: No Knockdowns</summary>
        Public Property DataFlagsNoKnockdowns As Boolean
            Get
                Return Bit("DATA\Data\Flags", 22)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Data\Flags", 22, value)
            End Set
        End Property
        ''' <summary>Bit 23 de DATA\Data\Flags: Allow Pickpocket</summary>
        Public Property DataFlagsAllowPickpocket As Boolean
            Get
                Return Bit("DATA\Data\Flags", 23)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Data\Flags", 23, value)
            End Set
        End Property
        ''' <summary>Bit 24 de DATA\Data\Flags: Always Use Proxy Controller</summary>
        Public Property DataFlagsAlwaysUseProxyController As Boolean
            Get
                Return Bit("DATA\Data\Flags", 24)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Data\Flags", 24, value)
            End Set
        End Property
        ''' <summary>Bit 25 de DATA\Data\Flags: Don't Show Weapon Blood</summary>
        Public Property DataFlagsDonTShowWeaponBlood As Boolean
            Get
                Return Bit("DATA\Data\Flags", 25)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Data\Flags", 25, value)
            End Set
        End Property
        ''' <summary>Bit 26 de DATA\Data\Flags: Overlay Head Part List</summary>
        Public Property DataFlagsOverlayHeadPartList As Boolean
            Get
                Return Bit("DATA\Data\Flags", 26)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Data\Flags", 26, value)
            End Set
        End Property
        ''' <summary>Bit 27 de DATA\Data\Flags: Override Head Part List</summary>
        Public Property DataFlagsOverrideHeadPartList As Boolean
            Get
                Return Bit("DATA\Data\Flags", 27)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Data\Flags", 27, value)
            End Set
        End Property
        ''' <summary>Bit 28 de DATA\Data\Flags: Can Pickup Items</summary>
        Public Property DataFlagsCanPickupItems As Boolean
            Get
                Return Bit("DATA\Data\Flags", 28)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Data\Flags", 28, value)
            End Set
        End Property
        ''' <summary>Bit 29 de DATA\Data\Flags: Allow Multiple Membrane Shaders</summary>
        Public Property DataFlagsAllowMultipleMembraneShaders As Boolean
            Get
                Return Bit("DATA\Data\Flags", 29)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Data\Flags", 29, value)
            End Set
        End Property
        ''' <summary>Bit 30 de DATA\Data\Flags: Can Dual Wield</summary>
        Public Property DataFlagsCanDualWield As Boolean
            Get
                Return Bit("DATA\Data\Flags", 30)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Data\Flags", 30, value)
            End Set
        End Property
        ''' <summary>Bit 31 de DATA\Data\Flags: Avoids Roads</summary>
        Public Property DataFlagsAvoidsRoads As Boolean
            Get
                Return Bit("DATA\Data\Flags", 31)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Data\Flags", 31, value)
            End Set
        End Property

        ''' <summary>DATA\Data\Acceleration Rate</summary>
        Public Property DataAccelerationRate As Single
            Get
                Return Flt("DATA\Data\Acceleration Rate")
            End Get
            Set(value As Single)
                Escribir("DATA\Data\Acceleration Rate", value)
            End Set
        End Property

        ''' <summary>DATA\Data\Deceleration Rate</summary>
        Public Property DataDecelerationRate As Single
            Get
                Return Flt("DATA\Data\Deceleration Rate")
            End Get
            Set(value As Single)
                Escribir("DATA\Data\Deceleration Rate", value)
            End Set
        End Property

        ''' <summary>DATA\Data\Size</summary>
        Public Property DataSize As UInteger
            Get
                Return CUInt(Entero("DATA\Data\Size"))
            End Get
            Set(value As UInteger)
                Escribir("DATA\Data\Size", CLng(value))
            End Set
        End Property
        ''' <summary>Nombre del valor de DATA\Data\Size.</summary>
        Public ReadOnly Property DataSizeNombre As String
            Get
                Return NombreDeValor("DATA\Data\Size")
            End Get
        End Property


        ''' <summary>DATA\Data\Unknown Bytes1</summary>
        Public Property DataUnknownBytes1 As Byte()
            Get
                Return Bytes("DATA\Data\Unknown Bytes1")
            End Get
            Set(value As Byte())
                Escribir("DATA\Data\Unknown Bytes1", value)
            End Set
        End Property

        ''' <summary>DATA\Data\Unknown Bytes2</summary>
        Public Property DataUnknownBytes2 As Byte()
            Get
                Return Bytes("DATA\Data\Unknown Bytes2")
            End Get
            Set(value As Byte())
                Escribir("DATA\Data\Unknown Bytes2", value)
            End Set
        End Property

        ''' <summary>DATA\Data\Injured Health Pct</summary>
        Public Property DataInjuredHealthPct As Single
            Get
                Return Flt("DATA\Data\Injured Health Pct")
            End Get
            Set(value As Single)
                Escribir("DATA\Data\Injured Health Pct", value)
            End Set
        End Property

        ''' <summary>DATA\Data\Shield Biped Object</summary>
        Public Property DataShieldBipedObject As Integer
            Get
                Return CInt(Entero("DATA\Data\Shield Biped Object"))
            End Get
            Set(value As Integer)
                Escribir("DATA\Data\Shield Biped Object", CLng(value))
            End Set
        End Property

        ''' <summary>DATA\Data\Beard Biped Object</summary>
        Public Property DataBeardBipedObject As Integer
            Get
                Return CInt(Entero("DATA\Data\Beard Biped Object"))
            End Get
            Set(value As Integer)
                Escribir("DATA\Data\Beard Biped Object", CLng(value))
            End Set
        End Property

        ''' <summary>DATA\Data\Body Biped Object</summary>
        Public Property DataBodyBipedObject As Integer
            Get
                Return CInt(Entero("DATA\Data\Body Biped Object"))
            End Get
            Set(value As Integer)
                Escribir("DATA\Data\Body Biped Object", CLng(value))
            End Set
        End Property

        ''' <summary>DATA\Data\Aim Angle Tolerance</summary>
        Public Property DataAimAngleTolerance As Single
            Get
                Return Flt("DATA\Data\Aim Angle Tolerance")
            End Get
            Set(value As Single)
                Escribir("DATA\Data\Aim Angle Tolerance", value)
            End Set
        End Property

        ''' <summary>DATA\Data\Flight Radius</summary>
        Public Property DataFlightRadius As Single
            Get
                Return Flt("DATA\Data\Flight Radius")
            End Get
            Set(value As Single)
                Escribir("DATA\Data\Flight Radius", value)
            End Set
        End Property

        ''' <summary>DATA\Data\Angular Acceleration Rate</summary>
        Public Property DataAngularAccelerationRate As Single
            Get
                Return Flt("DATA\Data\Angular Acceleration Rate")
            End Get
            Set(value As Single)
                Escribir("DATA\Data\Angular Acceleration Rate", value)
            End Set
        End Property

        ''' <summary>DATA\Data\Angular Tolerance</summary>
        Public Property DataAngularTolerance As Single
            Get
                Return Flt("DATA\Data\Angular Tolerance")
            End Get
            Set(value As Single)
                Escribir("DATA\Data\Angular Tolerance", value)
            End Set
        End Property

        ''' <summary>DATA\Data\Flags 2</summary>
        Public Property DataFlags2 As UInteger
            Get
                Return CUInt(Entero("DATA\Data\Flags 2"))
            End Get
            Set(value As UInteger)
                Escribir("DATA\Data\Flags 2", CLng(value))
            End Set
        End Property

        ''' <summary>Bit 0 de DATA\Data\Flags 2: Use Advanced Avoidance</summary>
        Public Property DataFlags2UseAdvancedAvoidance As Boolean
            Get
                Return Bit("DATA\Data\Flags 2", 0)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Data\Flags 2", 0, value)
            End Set
        End Property
        ''' <summary>Bit 1 de DATA\Data\Flags 2: Non-Hostile</summary>
        Public Property DataFlags2NonHostile As Boolean
            Get
                Return Bit("DATA\Data\Flags 2", 1)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Data\Flags 2", 1, value)
            End Set
        End Property
        ''' <summary>Bit 2 de DATA\Data\Flags 2: Floats</summary>
        Public Property DataFlags2Floats As Boolean
            Get
                Return Bit("DATA\Data\Flags 2", 2)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Data\Flags 2", 2, value)
            End Set
        End Property
        ''' <summary>Bit 5 de DATA\Data\Flags 2: Head Axis Bit 0</summary>
        Public Property DataFlags2HeadAxisBit0 As Boolean
            Get
                Return Bit("DATA\Data\Flags 2", 5)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Data\Flags 2", 5, value)
            End Set
        End Property
        ''' <summary>Bit 6 de DATA\Data\Flags 2: Head Axis Bit 1</summary>
        Public Property DataFlags2HeadAxisBit1 As Boolean
            Get
                Return Bit("DATA\Data\Flags 2", 6)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Data\Flags 2", 6, value)
            End Set
        End Property
        ''' <summary>Bit 7 de DATA\Data\Flags 2: Can Melee When Knocked Down</summary>
        Public Property DataFlags2CanMeleeWhenKnockedDown As Boolean
            Get
                Return Bit("DATA\Data\Flags 2", 7)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Data\Flags 2", 7, value)
            End Set
        End Property
        ''' <summary>Bit 8 de DATA\Data\Flags 2: Use Idle Chatter During Combat</summary>
        Public Property DataFlags2UseIdleChatterDuringCombat As Boolean
            Get
                Return Bit("DATA\Data\Flags 2", 8)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Data\Flags 2", 8, value)
            End Set
        End Property
        ''' <summary>Bit 9 de DATA\Data\Flags 2: Ungendered</summary>
        Public Property DataFlags2Ungendered As Boolean
            Get
                Return Bit("DATA\Data\Flags 2", 9)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Data\Flags 2", 9, value)
            End Set
        End Property
        ''' <summary>Bit 10 de DATA\Data\Flags 2: Can Move When Knocked Down</summary>
        Public Property DataFlags2CanMoveWhenKnockedDown As Boolean
            Get
                Return Bit("DATA\Data\Flags 2", 10)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Data\Flags 2", 10, value)
            End Set
        End Property
        ''' <summary>Bit 11 de DATA\Data\Flags 2: Use Large Actor Pathing</summary>
        Public Property DataFlags2UseLargeActorPathing As Boolean
            Get
                Return Bit("DATA\Data\Flags 2", 11)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Data\Flags 2", 11, value)
            End Set
        End Property
        ''' <summary>Bit 12 de DATA\Data\Flags 2: Use Subsegmented Damage</summary>
        Public Property DataFlags2UseSubsegmentedDamage As Boolean
            Get
                Return Bit("DATA\Data\Flags 2", 12)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Data\Flags 2", 12, value)
            End Set
        End Property
        ''' <summary>Bit 13 de DATA\Data\Flags 2: Flight - Defer Kill</summary>
        Public Property DataFlags2FlightDeferKill As Boolean
            Get
                Return Bit("DATA\Data\Flags 2", 13)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Data\Flags 2", 13, value)
            End Set
        End Property
        ''' <summary>Bit 15 de DATA\Data\Flags 2: Flight - Allow Procedural Crash Land</summary>
        Public Property DataFlags2FlightAllowProceduralCrashLand As Boolean
            Get
                Return Bit("DATA\Data\Flags 2", 15)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Data\Flags 2", 15, value)
            End Set
        End Property
        ''' <summary>Bit 16 de DATA\Data\Flags 2: Disable Weapon Culling</summary>
        Public Property DataFlags2DisableWeaponCulling As Boolean
            Get
                Return Bit("DATA\Data\Flags 2", 16)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Data\Flags 2", 16, value)
            End Set
        End Property
        ''' <summary>Bit 17 de DATA\Data\Flags 2: Use Optimal Speeds</summary>
        Public Property DataFlags2UseOptimalSpeeds As Boolean
            Get
                Return Bit("DATA\Data\Flags 2", 17)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Data\Flags 2", 17, value)
            End Set
        End Property
        ''' <summary>Bit 18 de DATA\Data\Flags 2: Has Facial Rig</summary>
        Public Property DataFlags2HasFacialRig As Boolean
            Get
                Return Bit("DATA\Data\Flags 2", 18)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Data\Flags 2", 18, value)
            End Set
        End Property
        ''' <summary>Bit 19 de DATA\Data\Flags 2: Can Use Crippled Limbs</summary>
        Public Property DataFlags2CanUseCrippledLimbs As Boolean
            Get
                Return Bit("DATA\Data\Flags 2", 19)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Data\Flags 2", 19, value)
            End Set
        End Property
        ''' <summary>Bit 20 de DATA\Data\Flags 2: Use Quadruped Controller</summary>
        Public Property DataFlags2UseQuadrupedController As Boolean
            Get
                Return Bit("DATA\Data\Flags 2", 20)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Data\Flags 2", 20, value)
            End Set
        End Property
        ''' <summary>Bit 21 de DATA\Data\Flags 2: Low Priority Pushable</summary>
        Public Property DataFlags2LowPriorityPushable As Boolean
            Get
                Return Bit("DATA\Data\Flags 2", 21)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Data\Flags 2", 21, value)
            End Set
        End Property
        ''' <summary>Bit 22 de DATA\Data\Flags 2: Cannot Use Playable Items</summary>
        Public Property DataFlags2CannotUsePlayableItems As Boolean
            Get
                Return Bit("DATA\Data\Flags 2", 22)
            End Get
            Set(value As Boolean)
                PonerBit("DATA\Data\Flags 2", 22, value)
            End Set
        End Property

        ''' <summary>DATA\Data\Unknown Float1</summary>
        Public Property DataUnknownFloat1 As Single
            Get
                Return Flt("DATA\Data\Unknown Float1")
            End Get
            Set(value As Single)
                Escribir("DATA\Data\Unknown Float1", value)
            End Set
        End Property

        ''' <summary>DATA\Data\Unknown Float2</summary>
        Public Property DataUnknownFloat2 As Single
            Get
                Return Flt("DATA\Data\Unknown Float2")
            End Get
            Set(value As Single)
                Escribir("DATA\Data\Unknown Float2", value)
            End Set
        End Property

        ''' <summary>DATA\Data\Unknown Bytes3</summary>
        Public Property DataUnknownBytes3 As Byte()
            Get
                Return Bytes("DATA\Data\Unknown Bytes3")
            End Get
            Set(value As Byte())
                Escribir("DATA\Data\Unknown Bytes3", value)
            End Set
        End Property

        ''' <summary>DATA\Data\Unknown Bytes4</summary>
        Public Property DataUnknownBytes4 As Byte()
            Get
                Return Bytes("DATA\Data\Unknown Bytes4")
            End Get
            Set(value As Byte())
                Escribir("DATA\Data\Unknown Bytes4", value)
            End Set
        End Property

        ''' <summary>DATA\Data\Unknown Bytes5</summary>
        Public Property DataUnknownBytes5 As Byte()
            Get
                Return Bytes("DATA\Data\Unknown Bytes5")
            End Get
            Set(value As Byte())
                Escribir("DATA\Data\Unknown Bytes5", value)
            End Set
        End Property

        ''' <summary>DATA\Data\Unknown Bytes6</summary>
        Public Property DataUnknownBytes6 As Byte()
            Get
                Return Bytes("DATA\Data\Unknown Bytes6")
            End Get
            Set(value As Byte())
                Escribir("DATA\Data\Unknown Bytes6", value)
            End Set
        End Property

        ''' <summary>DATA\Data\Unknown Bytes7</summary>
        Public Property DataUnknownBytes7 As Byte()
            Get
                Return Bytes("DATA\Data\Unknown Bytes7")
            End Get
            Set(value As Byte())
                Escribir("DATA\Data\Unknown Bytes7", value)
            End Set
        End Property

        ''' <summary>DATA\Data\Unknown Float3</summary>
        Public Property DataUnknownFloat3 As Single
            Get
                Return Flt("DATA\Data\Unknown Float3")
            End Get
            Set(value As Single)
                Escribir("DATA\Data\Unknown Float3", value)
            End Set
        End Property

        ''' <summary>DATA\Data\Unknown Bytes8</summary>
        Public Property DataUnknownBytes8 As Byte()
            Get
                Return Bytes("DATA\Data\Unknown Bytes8")
            End Get
            Set(value As Byte())
                Escribir("DATA\Data\Unknown Bytes8", value)
            End Set
        End Property

        ''' <summary>DATA\Data\Pipboy Biped Object</summary>
        Public Property DataPipboyBipedObject As Integer
            Get
                Return CInt(Entero("DATA\Data\Pipboy Biped Object"))
            End Get
            Set(value As Integer)
                Escribir("DATA\Data\Pipboy Biped Object", CLng(value))
            End Set
        End Property

        ''' <summary>DATA\Data\XP Value</summary>
        Public Property DataXPValue As Short
            Get
                Return CShort(Entero("DATA\Data\XP Value"))
            End Get
            Set(value As Short)
                Escribir("DATA\Data\XP Value", CLng(value))
            End Set
        End Property

        ''' <summary>DATA\Data\Severable - Debris Scale</summary>
        Public Property DataSeverableDebrisScale As Single
            Get
                Return Flt("DATA\Data\Severable - Debris Scale")
            End Get
            Set(value As Single)
                Escribir("DATA\Data\Severable - Debris Scale", value)
            End Set
        End Property

        ''' <summary>DATA\Data\Severable - Debris Count</summary>
        Public Property DataSeverableDebrisCount As Byte
            Get
                Return CByte(Entero("DATA\Data\Severable - Debris Count"))
            End Get
            Set(value As Byte)
                Escribir("DATA\Data\Severable - Debris Count", CLng(value))
            End Set
        End Property

        ''' <summary>DATA\Data\Severable - Decal Count</summary>
        Public Property DataSeverableDecalCount As Byte
            Get
                Return CByte(Entero("DATA\Data\Severable - Decal Count"))
            End Get
            Set(value As Byte)
                Escribir("DATA\Data\Severable - Decal Count", CLng(value))
            End Set
        End Property

        ''' <summary>DATA\Data\Explodable - Debris Scale</summary>
        Public Property DataExplodableDebrisScale As Single
            Get
                Return Flt("DATA\Data\Explodable - Debris Scale")
            End Get
            Set(value As Single)
                Escribir("DATA\Data\Explodable - Debris Scale", value)
            End Set
        End Property

        ''' <summary>DATA\Data\Explodable - Debris Count</summary>
        Public Property DataExplodableDebrisCount As Byte
            Get
                Return CByte(Entero("DATA\Data\Explodable - Debris Count"))
            End Get
            Set(value As Byte)
                Escribir("DATA\Data\Explodable - Debris Count", CLng(value))
            End Set
        End Property

        ''' <summary>DATA\Data\Explodable - Decal Count</summary>
        Public Property DataExplodableDecalCount As Byte
            Get
                Return CByte(Entero("DATA\Data\Explodable - Decal Count"))
            End Get
            Set(value As Byte)
                Escribir("DATA\Data\Explodable - Decal Count", CLng(value))
            End Set
        End Property

        ''' <summary>DATA\Data\Severable - Explosion  -&gt;  EXPL / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property DataSeverableExplosion As UInteger
            Get
                Return Referencia("DATA\Data\Severable - Explosion")
            End Get
            Set(value As UInteger)
                PonerReferencia("DATA\Data\Severable - Explosion", value)
            End Set
        End Property

        ''' <summary>DATA\Data\Severable - Debris  -&gt;  DEBR / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property DataSeverableDebris As UInteger
            Get
                Return Referencia("DATA\Data\Severable - Debris")
            End Get
            Set(value As UInteger)
                PonerReferencia("DATA\Data\Severable - Debris", value)
            End Set
        End Property

        ''' <summary>DATA\Data\Severable - Impact DataSet  -&gt;  IPDS / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property DataSeverableImpactDataSet As UInteger
            Get
                Return Referencia("DATA\Data\Severable - Impact DataSet")
            End Get
            Set(value As UInteger)
                PonerReferencia("DATA\Data\Severable - Impact DataSet", value)
            End Set
        End Property

        ''' <summary>DATA\Data\Explodable - Explosion  -&gt;  EXPL / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property DataExplodableExplosion As UInteger
            Get
                Return Referencia("DATA\Data\Explodable - Explosion")
            End Get
            Set(value As UInteger)
                PonerReferencia("DATA\Data\Explodable - Explosion", value)
            End Set
        End Property

        ''' <summary>DATA\Data\Explodable - Debris  -&gt;  DEBR / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property DataExplodableDebris As UInteger
            Get
                Return Referencia("DATA\Data\Explodable - Debris")
            End Get
            Set(value As UInteger)
                PonerReferencia("DATA\Data\Explodable - Debris", value)
            End Set
        End Property

        ''' <summary>DATA\Data\Explodable - Impact DataSet  -&gt;  IPDS / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property DataExplodableImpactDataSet As UInteger
            Get
                Return Referencia("DATA\Data\Explodable - Impact DataSet")
            End Get
            Set(value As UInteger)
                PonerReferencia("DATA\Data\Explodable - Impact DataSet", value)
            End Set
        End Property

        ''' <summary>DATA\Data\OnCripple\Debris Scale</summary>
        Public Property OnCrippleDebrisScale As Single
            Get
                Return Flt("DATA\Data\OnCripple\Debris Scale")
            End Get
            Set(value As Single)
                Escribir("DATA\Data\OnCripple\Debris Scale", value)
            End Set
        End Property

        ''' <summary>DATA\Data\OnCripple\Debris Count</summary>
        Public Property OnCrippleDebrisCount As Byte
            Get
                Return CByte(Entero("DATA\Data\OnCripple\Debris Count"))
            End Get
            Set(value As Byte)
                Escribir("DATA\Data\OnCripple\Debris Count", CLng(value))
            End Set
        End Property

        ''' <summary>DATA\Data\OnCripple\Decal Count</summary>
        Public Property OnCrippleDecalCount As Byte
            Get
                Return CByte(Entero("DATA\Data\OnCripple\Decal Count"))
            End Get
            Set(value As Byte)
                Escribir("DATA\Data\OnCripple\Decal Count", CLng(value))
            End Set
        End Property

        ''' <summary>DATA\Data\OnCripple\Explosion  -&gt;  EXPL / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property OnCrippleExplosion As UInteger
            Get
                Return Referencia("DATA\Data\OnCripple\Explosion")
            End Get
            Set(value As UInteger)
                PonerReferencia("DATA\Data\OnCripple\Explosion", value)
            End Set
        End Property

        ''' <summary>DATA\Data\OnCripple\Debris  -&gt;  DEBR / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property OnCrippleDebris As UInteger
            Get
                Return Referencia("DATA\Data\OnCripple\Debris")
            End Get
            Set(value As UInteger)
                PonerReferencia("DATA\Data\OnCripple\Debris", value)
            End Set
        End Property

        ''' <summary>DATA\Data\OnCripple\Impact DataSet  -&gt;  IPDS / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property OnCrippleImpactDataSet As UInteger
            Get
                Return Referencia("DATA\Data\OnCripple\Impact DataSet")
            End Get
            Set(value As UInteger)
                PonerReferencia("DATA\Data\OnCripple\Impact DataSet", value)
            End Set
        End Property

        ''' <summary>DATA\Data\Explodable - Subsegment Explosion  -&gt;  EXPL / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property DataExplodableSubsegmentExplosion As UInteger
            Get
                Return Referencia("DATA\Data\Explodable - Subsegment Explosion")
            End Get
            Set(value As UInteger)
                PonerReferencia("DATA\Data\Explodable - Subsegment Explosion", value)
            End Set
        End Property

        ''' <summary>DATA\Data\Orientation Limits - Pitch</summary>
        Public Property DataOrientationLimitsPitch As Single
            Get
                Return Flt("DATA\Data\Orientation Limits - Pitch")
            End Get
            Set(value As Single)
                Escribir("DATA\Data\Orientation Limits - Pitch", value)
            End Set
        End Property

        ''' <summary>DATA\Data\Orientation Limits - Roll</summary>
        Public Property DataOrientationLimitsRoll As Single
            Get
                Return Flt("DATA\Data\Orientation Limits - Roll")
            End Get
            Set(value As Single)
                Escribir("DATA\Data\Orientation Limits - Roll", value)
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

        ''' <summary>GNAM\Body Part Data  -&gt;  BPTD. Referencia en el espacio del orden de carga.</summary>
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

        ''' <summary>Male Behavior Graph\Model\MODC\Color Remapping Index</summary>
        Public Property ModelColorRemappingIndex As Single
            Get
                Return Flt("Male Behavior Graph\Model\MODC\Color Remapping Index")
            End Get
            Set(value As Single)
                Escribir("Male Behavior Graph\Model\MODC\Color Remapping Index", value)
            End Set
        End Property

        ''' <summary>Male Behavior Graph\Model\MODS\Material Swap  -&gt;  MSWP. Referencia en el espacio del orden de carga.</summary>
        Public Property ModelMaterialSwap As UInteger
            Get
                Return Referencia("Male Behavior Graph\Model\MODS\Material Swap")
            End Get
            Set(value As UInteger)
                PonerReferencia("Male Behavior Graph\Model\MODS\Material Swap", value)
            End Set
        End Property

        ''' <summary>Male Behavior Graph\Model\MODF\Flags</summary>
        Public Property ModelFlags As Byte
            Get
                Return CByte(Entero("Male Behavior Graph\Model\MODF\Flags"))
            End Get
            Set(value As Byte)
                Escribir("Male Behavior Graph\Model\MODF\Flags", CLng(value))
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

        ''' <summary>Female Behavior Graph\Model\MODC\Color Remapping Index</summary>
        Public Property ModelColorRemappingIndex2 As Single
            Get
                Return Flt("Female Behavior Graph\Model\MODC\Color Remapping Index")
            End Get
            Set(value As Single)
                Escribir("Female Behavior Graph\Model\MODC\Color Remapping Index", value)
            End Set
        End Property

        ''' <summary>Female Behavior Graph\Model\MODS\Material Swap  -&gt;  MSWP. Referencia en el espacio del orden de carga.</summary>
        Public Property ModelMaterialSwap2 As UInteger
            Get
                Return Referencia("Female Behavior Graph\Model\MODS\Material Swap")
            End Get
            Set(value As UInteger)
                PonerReferencia("Female Behavior Graph\Model\MODS\Material Swap", value)
            End Set
        End Property

        ''' <summary>Female Behavior Graph\Model\MODF\Flags</summary>
        Public Property ModelFlags2 As Byte
            Get
                Return CByte(Entero("Female Behavior Graph\Model\MODF\Flags"))
            End Get
            Set(value As Byte)
                Escribir("Female Behavior Graph\Model\MODF\Flags", CLng(value))
            End Set
        End Property

        ''' <summary>NAM4\Impact Material Type  -&gt;  MATT. Referencia en el espacio del orden de carga.</summary>
        Public Property ImpactMaterialType As UInteger
            Get
                Return Referencia("NAM4\Impact Material Type")
            End Get
            Set(value As UInteger)
                PonerReferencia("NAM4\Impact Material Type", value)
            End Set
        End Property

        ''' <summary>NAM5\Impact Data Set  -&gt;  IPDS. Referencia en el espacio del orden de carga.</summary>
        Public Property ImpactDataSet As UInteger
            Get
                Return Referencia("NAM5\Impact Data Set")
            End Get
            Set(value As UInteger)
                PonerReferencia("NAM5\Impact Data Set", value)
            End Set
        End Property

        ''' <summary>NAM7\Dismember Blood Art  -&gt;  ARTO. Referencia en el espacio del orden de carga.</summary>
        Public Property DismemberBloodArt As UInteger
            Get
                Return Referencia("NAM7\Dismember Blood Art")
            End Get
            Set(value As UInteger)
                PonerReferencia("NAM7\Dismember Blood Art", value)
            End Set
        End Property

        ''' <summary>CNAM\Meat Cap TextureSet  -&gt;  TXST. Referencia en el espacio del orden de carga.</summary>
        Public Property MeatCapTextureSet As UInteger
            Get
                Return Referencia("CNAM\Meat Cap TextureSet")
            End Get
            Set(value As UInteger)
                PonerReferencia("CNAM\Meat Cap TextureSet", value)
            End Set
        End Property

        ''' <summary>NAM2\Collar TextureSet  -&gt;  TXST. Referencia en el espacio del orden de carga.</summary>
        Public Property CollarTextureSet As UInteger
            Get
                Return Referencia("NAM2\Collar TextureSet")
            End Get
            Set(value As UInteger)
                PonerReferencia("NAM2\Collar TextureSet", value)
            End Set
        End Property

        ''' <summary>ONAM\Sound - Open Corpse  -&gt;  SNDR. Referencia en el espacio del orden de carga.</summary>
        Public Property SoundOpenCorpse As UInteger
            Get
                Return Referencia("ONAM\Sound - Open Corpse")
            End Get
            Set(value As UInteger)
                PonerReferencia("ONAM\Sound - Open Corpse", value)
            End Set
        End Property

        ''' <summary>LNAM\Sound - Close Corpse  -&gt;  SNDR. Referencia en el espacio del orden de carga.</summary>
        Public Property SoundCloseCorpse As UInteger
            Get
                Return Referencia("LNAM\Sound - Close Corpse")
            End Get
            Set(value As UInteger)
                PonerReferencia("LNAM\Sound - Close Corpse", value)
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

        ''' <summary>UNWP\Unarmed Weapon  -&gt;  WEAP. Referencia en el espacio del orden de carga.</summary>
        Public Property UnarmedWeapon As UInteger
            Get
                Return Referencia("UNWP\Unarmed Weapon")
            End Get
            Set(value As UInteger)
                PonerReferencia("UNWP\Unarmed Weapon", value)
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

        ''' <summary>WKMV\Base Movement Defaults - Default  -&gt;  MOVT. Referencia en el espacio del orden de carga.</summary>
        Public Property BaseMovementDefaultsDefault As UInteger
            Get
                Return Referencia("WKMV\Base Movement Defaults - Default")
            End Get
            Set(value As UInteger)
                PonerReferencia("WKMV\Base Movement Defaults - Default", value)
            End Set
        End Property

        ''' <summary>SWMV\Base Movement Defaults - Swim  -&gt;  MOVT. Referencia en el espacio del orden de carga.</summary>
        Public Property BaseMovementDefaultsSwim As UInteger
            Get
                Return Referencia("SWMV\Base Movement Defaults - Swim")
            End Get
            Set(value As UInteger)
                PonerReferencia("SWMV\Base Movement Defaults - Swim", value)
            End Set
        End Property

        ''' <summary>FLMV\Base Movement Defaults - Fly  -&gt;  MOVT. Referencia en el espacio del orden de carga.</summary>
        Public Property BaseMovementDefaultsFly As UInteger
            Get
                Return Referencia("FLMV\Base Movement Defaults - Fly")
            End Get
            Set(value As UInteger)
                PonerReferencia("FLMV\Base Movement Defaults - Fly", value)
            End Set
        End Property

        ''' <summary>SNMV\Base Movement Defaults - Sneak  -&gt;  MOVT. Referencia en el espacio del orden de carga.</summary>
        Public Property BaseMovementDefaultsSneak As UInteger
            Get
                Return Referencia("SNMV\Base Movement Defaults - Sneak")
            End Get
            Set(value As UInteger)
                PonerReferencia("SNMV\Base Movement Defaults - Sneak", value)
            End Set
        End Property

        ''' <summary>NNAM\Male Neck Fat Adjustments Scale\X</summary>
        Public Property MaleNeckFatAdjustmentsScaleX As Single
            Get
                Return Flt("NNAM\Male Neck Fat Adjustments Scale\X")
            End Get
            Set(value As Single)
                Escribir("NNAM\Male Neck Fat Adjustments Scale\X", value)
            End Set
        End Property

        ''' <summary>NNAM\Male Neck Fat Adjustments Scale\Y</summary>
        Public Property MaleNeckFatAdjustmentsScaleY As Single
            Get
                Return Flt("NNAM\Male Neck Fat Adjustments Scale\Y")
            End Get
            Set(value As Single)
                Escribir("NNAM\Male Neck Fat Adjustments Scale\Y", value)
            End Set
        End Property

        ''' <summary>DFTM\Male Default Face Texture  -&gt;  TXST. Referencia en el espacio del orden de carga.</summary>
        Public Property MaleDefaultFaceTexture As UInteger
            Get
                Return Referencia("DFTM\Male Default Face Texture")
            End Get
            Set(value As UInteger)
                PonerReferencia("DFTM\Male Default Face Texture", value)
            End Set
        End Property

        ''' <summary>WMAP\Male Wrinkle Map Path</summary>
        Public Property MaleWrinkleMapPath As String
            Get
                Return Txt("WMAP\Male Wrinkle Map Path")
            End Get
            Set(value As String)
                Escribir("WMAP\Male Wrinkle Map Path", value)
            End Set
        End Property

        ''' <summary>NNAM\Female Neck Fat Adjustments Scale\X</summary>
        Public Property FemaleNeckFatAdjustmentsScaleX As Single
            Get
                Return Flt("NNAM\Female Neck Fat Adjustments Scale\X")
            End Get
            Set(value As Single)
                Escribir("NNAM\Female Neck Fat Adjustments Scale\X", value)
            End Set
        End Property

        ''' <summary>NNAM\Female Neck Fat Adjustments Scale\Y</summary>
        Public Property FemaleNeckFatAdjustmentsScaleY As Single
            Get
                Return Flt("NNAM\Female Neck Fat Adjustments Scale\Y")
            End Get
            Set(value As Single)
                Escribir("NNAM\Female Neck Fat Adjustments Scale\Y", value)
            End Set
        End Property

        ''' <summary>DFTF\Female Default Face Texture  -&gt;  TXST. Referencia en el espacio del orden de carga.</summary>
        Public Property FemaleDefaultFaceTexture As UInteger
            Get
                Return Referencia("DFTF\Female Default Face Texture")
            End Get
            Set(value As UInteger)
                PonerReferencia("DFTF\Female Default Face Texture", value)
            End Set
        End Property

        ''' <summary>WMAP\Female Wrinkle Map Path</summary>
        Public Property FemaleWrinkleMapPath As String
            Get
                Return Txt("WMAP\Female Wrinkle Map Path")
            End Get
            Set(value As String)
                Escribir("WMAP\Female Wrinkle Map Path", value)
            End Set
        End Property

        ''' <summary>NAM8\Morph Race  -&gt;  RACE. Referencia en el espacio del orden de carga.</summary>
        Public Property MorphRace As UInteger
            Get
                Return Referencia("NAM8\Morph Race")
            End Get
            Set(value As UInteger)
                PonerReferencia("NAM8\Morph Race", value)
            End Set
        End Property

        ''' <summary>RNAM\Armor Race  -&gt;  RACE. Referencia en el espacio del orden de carga.</summary>
        Public Property ArmorRace As UInteger
            Get
                Return Referencia("RNAM\Armor Race")
            End Get
            Set(value As UInteger)
                PonerReferencia("RNAM\Armor Race", value)
            End Set
        End Property

        ''' <summary>SRAC\Subgraph Template Race  -&gt;  RACE. Referencia en el espacio del orden de carga.</summary>
        Public Property SubgraphTemplateRace As UInteger
            Get
                Return Referencia("SRAC\Subgraph Template Race")
            End Get
            Set(value As UInteger)
                PonerReferencia("SRAC\Subgraph Template Race", value)
            End Set
        End Property

        ''' <summary>SADD\Subgraph Additive Race  -&gt;  RACE. Referencia en el espacio del orden de carga.</summary>
        Public Property SubgraphAdditiveRace As UInteger
            Get
                Return Referencia("SADD\Subgraph Additive Race")
            End Get
            Set(value As UInteger)
                PonerReferencia("SADD\Subgraph Additive Race", value)
            End Set
        End Property

        ''' <summary>PTOP\Idle Chatter Time Min</summary>
        Public Property IdleChatterTimeMin As Single
            Get
                Return Flt("PTOP\Idle Chatter Time Min")
            End Get
            Set(value As Single)
                Escribir("PTOP\Idle Chatter Time Min", value)
            End Set
        End Property

        ''' <summary>NTOP\Idle Chatter Time Max</summary>
        Public Property IdleChatterTimeMax As Single
            Get
                Return Flt("NTOP\Idle Chatter Time Max")
            End Get
            Set(value As Single)
                Escribir("NTOP\Idle Chatter Time Max", value)
            End Set
        End Property

        ''' <summary>MLSI\Morph Last Index</summary>
        Public Property MorphLastIndex As UInteger
            Get
                Return CUInt(Entero("MLSI\Morph Last Index"))
            End Get
            Set(value As UInteger)
                Escribir("MLSI\Morph Last Index", CLng(value))
            End Set
        End Property

        ''' <summary>HNAM\Hair Color Lookup Texture</summary>
        Public Property HairColorLookupTexture As String
            Get
                Return Txt("HNAM\Hair Color Lookup Texture")
            End Get
            Set(value As String)
                Escribir("HNAM\Hair Color Lookup Texture", value)
            End Set
        End Property

        ''' <summary>HLTX\Hair Color Extended Lookup Texture</summary>
        Public Property HairColorExtendedLookupTexture As String
            Get
                Return Txt("HLTX\Hair Color Extended Lookup Texture")
            End Get
            Set(value As String)
                Escribir("HLTX\Hair Color Extended Lookup Texture", value)
            End Set
        End Property

        ''' <summary>QSTI\Dialogue Quest  -&gt;  QUST. Referencia en el espacio del orden de carga.</summary>
        Public Property DialogueQuest As UInteger
            Get
                Return Referencia("QSTI\Dialogue Quest")
            End Get
            Set(value As UInteger)
                PonerReferencia("QSTI\Dialogue Quest", value)
            End Set
        End Property

        ''' <summary>Actor Effects</summary>
        Public ReadOnly Property ActorEffects As IReadOnlyList(Of RaceFO4_ActorEffects)
            Get
                Return Elementos(Of RaceFO4_ActorEffects)("Actor Effects", Function(n) New RaceFO4_ActorEffects(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Keywords\KWDA\Keywords</summary>
        Public ReadOnly Property Keywords As IReadOnlyList(Of RaceFO4_Keywords)
            Get
                Return Elementos(Of RaceFO4_Keywords)("Keywords\KWDA\Keywords", Function(n) New RaceFO4_Keywords(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>PRPS\Properties</summary>
        Public ReadOnly Property Properties As IReadOnlyList(Of RaceFO4_Properties)
            Get
                Return Elementos(Of RaceFO4_Properties)("PRPS\Properties", Function(n) New RaceFO4_Properties(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>APPR\Attach Parent Slots</summary>
        Public ReadOnly Property AttachParentSlots As IReadOnlyList(Of RaceFO4_AttachParentSlots)
            Get
                Return Elementos(Of RaceFO4_AttachParentSlots)("APPR\Attach Parent Slots", Function(n) New RaceFO4_AttachParentSlots(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Movement Type Names</summary>
        Public ReadOnly Property MovementTypeNames As IReadOnlyList(Of RaceFO4_MovementTypeNames)
            Get
                Return Elementos(Of RaceFO4_MovementTypeNames)("Movement Type Names", Function(n) New RaceFO4_MovementTypeNames(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>VTCK\Voices</summary>
        Public ReadOnly Property Voices As IReadOnlyList(Of RaceFO4_Voices)
            Get
                Return Elementos(Of RaceFO4_Voices)("VTCK\Voices", Function(n) New RaceFO4_Voices(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>HCLF\Default Hair Colors</summary>
        Public ReadOnly Property DefaultHairColors As IReadOnlyList(Of RaceFO4_DefaultHairColors)
            Get
                Return Elementos(Of RaceFO4_DefaultHairColors)("HCLF\Default Hair Colors", Function(n) New RaceFO4_DefaultHairColors(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Attacks</summary>
        Public ReadOnly Property Attacks As IReadOnlyList(Of RaceFO4_Attacks)
            Get
                Return Elementos(Of RaceFO4_Attacks)("Attacks", Function(n) New RaceFO4_Attacks(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Body Data\Male Body Data\Parts</summary>
        Public ReadOnly Property Parts As IReadOnlyList(Of RaceFO4_Parts)
            Get
                Return Elementos(Of RaceFO4_Parts)("Body Data\Male Body Data\Parts", Function(n) New RaceFO4_Parts(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Body Data\Female Body Data\Parts</summary>
        Public ReadOnly Property Parts2 As IReadOnlyList(Of RaceFO4_Parts2)
            Get
                Return Elementos(Of RaceFO4_Parts2)("Body Data\Female Body Data\Parts", Function(n) New RaceFO4_Parts2(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Biped Object Names</summary>
        Public ReadOnly Property BipedObjectNames As IReadOnlyList(Of RaceFO4_BipedObjectNames)
            Get
                Return Elementos(Of RaceFO4_BipedObjectNames)("Biped Object Names", Function(n) New RaceFO4_BipedObjectNames(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>RBPC\Biped Object Conditions</summary>
        Public ReadOnly Property BipedObjectConditions As IReadOnlyList(Of RaceFO4_BipedObjectConditions)
            Get
                Return Elementos(Of RaceFO4_BipedObjectConditions)("RBPC\Biped Object Conditions", Function(n) New RaceFO4_BipedObjectConditions(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Movement Data Overrides</summary>
        Public ReadOnly Property MovementDataOverrides As IReadOnlyList(Of RaceFO4_MovementDataOverrides)
            Get
                Return Elementos(Of RaceFO4_MovementDataOverrides)("Movement Data Overrides", Function(n) New RaceFO4_MovementDataOverrides(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Equip Slots</summary>
        Public ReadOnly Property EquipSlots As IReadOnlyList(Of RaceFO4_EquipSlots)
            Get
                Return Elementos(Of RaceFO4_EquipSlots)("Equip Slots", Function(n) New RaceFO4_EquipSlots(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Phoneme Target Names</summary>
        Public ReadOnly Property PhonemeTargetNames As IReadOnlyList(Of RaceFO4_PhonemeTargetNames)
            Get
                Return Elementos(Of RaceFO4_PhonemeTargetNames)("Phoneme Target Names", Function(n) New RaceFO4_PhonemeTargetNames(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Male Head Parts</summary>
        Public ReadOnly Property MaleHeadParts As IReadOnlyList(Of RaceFO4_MaleHeadParts)
            Get
                Return Elementos(Of RaceFO4_MaleHeadParts)("Male Head Parts", Function(n) New RaceFO4_MaleHeadParts(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Male Race Presets</summary>
        Public ReadOnly Property MaleRacePresets As IReadOnlyList(Of RaceFO4_MaleRacePresets)
            Get
                Return Elementos(Of RaceFO4_MaleRacePresets)("Male Race Presets", Function(n) New RaceFO4_MaleRacePresets(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Male Hair Colors</summary>
        Public ReadOnly Property MaleHairColors As IReadOnlyList(Of RaceFO4_MaleHairColors)
            Get
                Return Elementos(Of RaceFO4_MaleHairColors)("Male Hair Colors", Function(n) New RaceFO4_MaleHairColors(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Male Face Details</summary>
        Public ReadOnly Property MaleFaceDetails As IReadOnlyList(Of RaceFO4_MaleFaceDetails)
            Get
                Return Elementos(Of RaceFO4_MaleFaceDetails)("Male Face Details", Function(n) New RaceFO4_MaleFaceDetails(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Male Tint Layers</summary>
        Public ReadOnly Property MaleTintLayers As IReadOnlyList(Of RaceFO4_MaleTintLayers)
            Get
                Return Elementos(Of RaceFO4_MaleTintLayers)("Male Tint Layers", Function(n) New RaceFO4_MaleTintLayers(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Male Tint Layers\Group\Options</summary>
        Public ReadOnly Property Options As IReadOnlyList(Of RaceFO4_Options)
            Get
                Return Elementos(Of RaceFO4_Options)("Male Tint Layers\Group\Options", Function(n) New RaceFO4_Options(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Male Tint Layers\Group\Options\Option\Conditions</summary>
        Public ReadOnly Property Conditions As IReadOnlyList(Of RaceFO4_Conditions)
            Get
                Return Elementos(Of RaceFO4_Conditions)("Male Tint Layers\Group\Options\Option\Conditions", Function(n) New RaceFO4_Conditions(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Male Tint Layers\Group\Options\Option\Textures</summary>
        Public ReadOnly Property Textures As IReadOnlyList(Of RaceFO4_Textures)
            Get
                Return Elementos(Of RaceFO4_Textures)("Male Tint Layers\Group\Options\Option\Textures", Function(n) New RaceFO4_Textures(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Male Tint Layers\Group\Options\Option\TTEC\Template Colors</summary>
        Public ReadOnly Property TemplateColors As IReadOnlyList(Of RaceFO4_TemplateColors)
            Get
                Return Elementos(Of RaceFO4_TemplateColors)("Male Tint Layers\Group\Options\Option\TTEC\Template Colors", Function(n) New RaceFO4_TemplateColors(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Male Morph Groups</summary>
        Public ReadOnly Property MaleMorphGroups As IReadOnlyList(Of RaceFO4_MaleMorphGroups)
            Get
                Return Elementos(Of RaceFO4_MaleMorphGroups)("Male Morph Groups", Function(n) New RaceFO4_MaleMorphGroups(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Male Morph Groups\Morph Group\Morph Presets</summary>
        Public ReadOnly Property MorphPresets As IReadOnlyList(Of RaceFO4_MorphPresets)
            Get
                Return Elementos(Of RaceFO4_MorphPresets)("Male Morph Groups\Morph Group\Morph Presets", Function(n) New RaceFO4_MorphPresets(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Male Morph Groups\Morph Group\MPGS\Morph Group Sliders</summary>
        Public ReadOnly Property MorphGroupSliders As IReadOnlyList(Of RaceFO4_MorphGroupSliders)
            Get
                Return Elementos(Of RaceFO4_MorphGroupSliders)("Male Morph Groups\Morph Group\MPGS\Morph Group Sliders", Function(n) New RaceFO4_MorphGroupSliders(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Male Face Morphs</summary>
        Public ReadOnly Property MaleFaceMorphs As IReadOnlyList(Of RaceFO4_MaleFaceMorphs)
            Get
                Return Elementos(Of RaceFO4_MaleFaceMorphs)("Male Face Morphs", Function(n) New RaceFO4_MaleFaceMorphs(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Female Head Parts</summary>
        Public ReadOnly Property FemaleHeadParts As IReadOnlyList(Of RaceFO4_FemaleHeadParts)
            Get
                Return Elementos(Of RaceFO4_FemaleHeadParts)("Female Head Parts", Function(n) New RaceFO4_FemaleHeadParts(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Female Race Presets</summary>
        Public ReadOnly Property FemaleRacePresets As IReadOnlyList(Of RaceFO4_FemaleRacePresets)
            Get
                Return Elementos(Of RaceFO4_FemaleRacePresets)("Female Race Presets", Function(n) New RaceFO4_FemaleRacePresets(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Female Hair Colors</summary>
        Public ReadOnly Property FemaleHairColors As IReadOnlyList(Of RaceFO4_FemaleHairColors)
            Get
                Return Elementos(Of RaceFO4_FemaleHairColors)("Female Hair Colors", Function(n) New RaceFO4_FemaleHairColors(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Female Face Details</summary>
        Public ReadOnly Property FemaleFaceDetails As IReadOnlyList(Of RaceFO4_FemaleFaceDetails)
            Get
                Return Elementos(Of RaceFO4_FemaleFaceDetails)("Female Face Details", Function(n) New RaceFO4_FemaleFaceDetails(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Female Tint Layers</summary>
        Public ReadOnly Property FemaleTintLayers As IReadOnlyList(Of RaceFO4_FemaleTintLayers)
            Get
                Return Elementos(Of RaceFO4_FemaleTintLayers)("Female Tint Layers", Function(n) New RaceFO4_FemaleTintLayers(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Female Tint Layers\Group\Options</summary>
        Public ReadOnly Property Options2 As IReadOnlyList(Of RaceFO4_Options2)
            Get
                Return Elementos(Of RaceFO4_Options2)("Female Tint Layers\Group\Options", Function(n) New RaceFO4_Options2(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Female Tint Layers\Group\Options\Option\Conditions</summary>
        Public ReadOnly Property Conditions2 As IReadOnlyList(Of RaceFO4_Conditions2)
            Get
                Return Elementos(Of RaceFO4_Conditions2)("Female Tint Layers\Group\Options\Option\Conditions", Function(n) New RaceFO4_Conditions2(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Female Tint Layers\Group\Options\Option\Textures</summary>
        Public ReadOnly Property Textures2 As IReadOnlyList(Of RaceFO4_Textures2)
            Get
                Return Elementos(Of RaceFO4_Textures2)("Female Tint Layers\Group\Options\Option\Textures", Function(n) New RaceFO4_Textures2(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Female Tint Layers\Group\Options\Option\TTEC\Template Colors</summary>
        Public ReadOnly Property TemplateColors2 As IReadOnlyList(Of RaceFO4_TemplateColors2)
            Get
                Return Elementos(Of RaceFO4_TemplateColors2)("Female Tint Layers\Group\Options\Option\TTEC\Template Colors", Function(n) New RaceFO4_TemplateColors2(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Female Morph Groups</summary>
        Public ReadOnly Property FemaleMorphGroups As IReadOnlyList(Of RaceFO4_FemaleMorphGroups)
            Get
                Return Elementos(Of RaceFO4_FemaleMorphGroups)("Female Morph Groups", Function(n) New RaceFO4_FemaleMorphGroups(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Female Morph Groups\Morph Group\Morph Presets</summary>
        Public ReadOnly Property MorphPresets2 As IReadOnlyList(Of RaceFO4_MorphPresets2)
            Get
                Return Elementos(Of RaceFO4_MorphPresets2)("Female Morph Groups\Morph Group\Morph Presets", Function(n) New RaceFO4_MorphPresets2(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Female Morph Groups\Morph Group\MPGS\Morph Group Sliders</summary>
        Public ReadOnly Property MorphGroupSliders2 As IReadOnlyList(Of RaceFO4_MorphGroupSliders2)
            Get
                Return Elementos(Of RaceFO4_MorphGroupSliders2)("Female Morph Groups\Morph Group\MPGS\Morph Group Sliders", Function(n) New RaceFO4_MorphGroupSliders2(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Female Face Morphs</summary>
        Public ReadOnly Property FemaleFaceMorphs As IReadOnlyList(Of RaceFO4_FemaleFaceMorphs)
            Get
                Return Elementos(Of RaceFO4_FemaleFaceMorphs)("Female Face Morphs", Function(n) New RaceFO4_FemaleFaceMorphs(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Subgraph Data\Data\Actor Keywords</summary>
        Public ReadOnly Property ActorKeywords As IReadOnlyList(Of RaceFO4_ActorKeywords)
            Get
                Return Elementos(Of RaceFO4_ActorKeywords)("Subgraph Data\Data\Actor Keywords", Function(n) New RaceFO4_ActorKeywords(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Subgraph Data</summary>
        Public ReadOnly Property SubgraphData As IReadOnlyList(Of RaceFO4_SubgraphData)
            Get
                Return Elementos(Of RaceFO4_SubgraphData)("Subgraph Data", Function(n) New RaceFO4_SubgraphData(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Subgraph Data\Data\Animation Paths</summary>
        Public ReadOnly Property AnimationPaths As IReadOnlyList(Of RaceFO4_AnimationPaths)
            Get
                Return Elementos(Of RaceFO4_AnimationPaths)("Subgraph Data\Data\Animation Paths", Function(n) New RaceFO4_AnimationPaths(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Subgraph Data\Data\Target Keywords</summary>
        Public ReadOnly Property TargetKeywords As IReadOnlyList(Of RaceFO4_TargetKeywords)
            Get
                Return Elementos(Of RaceFO4_TargetKeywords)("Subgraph Data\Data\Target Keywords", Function(n) New RaceFO4_TargetKeywords(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Morph Values</summary>
        Public ReadOnly Property MorphValues As IReadOnlyList(Of RaceFO4_MorphValues)
            Get
                Return Elementos(Of RaceFO4_MorphValues)("Morph Values", Function(n) New RaceFO4_MorphValues(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Bone Scale Data</summary>
        Public ReadOnly Property BoneScaleData As IReadOnlyList(Of RaceFO4_BoneScaleData)
            Get
                Return Elementos(Of RaceFO4_BoneScaleData)("Bone Scale Data", Function(n) New RaceFO4_BoneScaleData(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Bone Scale Data\Bone Data Set\Bone Weight Scale Data\Bone Weight Scales</summary>
        Public ReadOnly Property BoneWeightScales As IReadOnlyList(Of RaceFO4_BoneWeightScales)
            Get
                Return Elementos(Of RaceFO4_BoneWeightScales)("Bone Scale Data\Bone Data Set\Bone Weight Scale Data\Bone Weight Scales", Function(n) New RaceFO4_BoneWeightScales(n, Context, Resolver))
            End Get
        End Property

        ''' <summary>Bone Scale Data\Bone Data Set\Bone Range Modifier Data\Bone Range Modifiers</summary>
        Public ReadOnly Property BoneRangeModifiers As IReadOnlyList(Of RaceFO4_BoneRangeModifiers)
            Get
                Return Elementos(Of RaceFO4_BoneRangeModifiers)("Bone Scale Data\Bone Data Set\Bone Range Modifier Data\Bone Range Modifiers", Function(n) New RaceFO4_BoneRangeModifiers(n, Context, Resolver))
            End Get
        End Property

    End Class

    ''' <summary>Un elemento de Actor Effects.</summary>
    Public NotInheritable Class RaceFO4_ActorEffects
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>SPLO\Actor Effect  -&gt;  SPEL / LVSP. Referencia en el espacio del orden de carga.</summary>
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
    Public NotInheritable Class RaceFO4_Keywords
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

    ''' <summary>Un elemento de PRPS\Properties.</summary>
    Public NotInheritable Class RaceFO4_Properties
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Property\Actor Value  -&gt;  AVIF / NULL. Referencia en el espacio del orden de carga.</summary>
        Public Property PropertyActorValue As UInteger
            Get
                Return Referencia("Property\Actor Value")
            End Get
            Set(value As UInteger)
                PonerReferencia("Property\Actor Value", value)
            End Set
        End Property

        ''' <summary>Property\Value</summary>
        Public Property PropertyValue As Single
            Get
                Return Flt("Property\Value")
            End Get
            Set(value As Single)
                Escribir("Property\Value", value)
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de APPR\Attach Parent Slots.</summary>
    Public NotInheritable Class RaceFO4_AttachParentSlots
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Keyword  -&gt;  KYWD. Referencia en el espacio del orden de carga.</summary>
        Public Property Keyword As UInteger
            Get
                Return Referencia("Keyword")
            End Get
            Set(value As UInteger)
                PonerReferencia("Keyword", value)
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Movement Type Names.</summary>
    Public NotInheritable Class RaceFO4_MovementTypeNames
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
    Public NotInheritable Class RaceFO4_Voices
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

    ''' <summary>Un elemento de HCLF\Default Hair Colors.</summary>
    Public NotInheritable Class RaceFO4_DefaultHairColors
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
    Public NotInheritable Class RaceFO4_Attacks
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

        ''' <summary>Attack\ATKD\Attack Data\Attack Spell  -&gt;  SPEL / NULL. Referencia en el espacio del orden de carga.</summary>
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
        ''' <summary>Bit 3 de Attack\ATKD\Attack Data\Attack Flags: Charge Attack</summary>
        Public Property AttackDataAttackFlagsChargeAttack As Boolean
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
        ''' <summary>Bit 5 de Attack\ATKD\Attack Data\Attack Flags: Continuous Attack</summary>
        Public Property AttackDataAttackFlagsContinuousAttack As Boolean
            Get
                Return Bit("Attack\ATKD\Attack Data\Attack Flags", 5)
            End Get
            Set(value As Boolean)
                PonerBit("Attack\ATKD\Attack Data\Attack Flags", 5, value)
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

        ''' <summary>Attack\ATKD\Attack Data\Action Points Mult</summary>
        Public Property AttackDataActionPointsMult As Single
            Get
                Return Flt("Attack\ATKD\Attack Data\Action Points Mult")
            End Get
            Set(value As Single)
                Escribir("Attack\ATKD\Attack Data\Action Points Mult", value)
            End Set
        End Property

        ''' <summary>Attack\ATKD\Attack Data\Stagger Offset</summary>
        Public Property AttackDataStaggerOffset As Integer
            Get
                Return CInt(Entero("Attack\ATKD\Attack Data\Stagger Offset"))
            End Get
            Set(value As Integer)
                Escribir("Attack\ATKD\Attack Data\Stagger Offset", CLng(value))
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

        ''' <summary>Attack\ATKW\Weapon Slot  -&gt;  EQUP. Referencia en el espacio del orden de carga.</summary>
        Public Property AttackWeaponSlot As UInteger
            Get
                Return Referencia("Attack\ATKW\Weapon Slot")
            End Get
            Set(value As UInteger)
                PonerReferencia("Attack\ATKW\Weapon Slot", value)
            End Set
        End Property

        ''' <summary>Attack\ATKS\Required Slot  -&gt;  EQUP. Referencia en el espacio del orden de carga.</summary>
        Public Property AttackRequiredSlot As UInteger
            Get
                Return Referencia("Attack\ATKS\Required Slot")
            End Get
            Set(value As UInteger)
                PonerReferencia("Attack\ATKS\Required Slot", value)
            End Set
        End Property

        ''' <summary>Attack\ATKT\Description</summary>
        Public Property AttackDescription As String
            Get
                Return Txt("Attack\ATKT\Description")
            End Get
            Set(value As String)
                Escribir("Attack\ATKT\Description", value)
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Body Data\Male Body Data\Parts.</summary>
    Public NotInheritable Class RaceFO4_Parts
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

        ''' <summary>Part\Model\MODC\Color Remapping Index</summary>
        Public Property ModelColorRemappingIndex As Single
            Get
                Return Flt("Part\Model\MODC\Color Remapping Index")
            End Get
            Set(value As Single)
                Escribir("Part\Model\MODC\Color Remapping Index", value)
            End Set
        End Property

        ''' <summary>Part\Model\MODS\Material Swap  -&gt;  MSWP. Referencia en el espacio del orden de carga.</summary>
        Public Property ModelMaterialSwap As UInteger
            Get
                Return Referencia("Part\Model\MODS\Material Swap")
            End Get
            Set(value As UInteger)
                PonerReferencia("Part\Model\MODS\Material Swap", value)
            End Set
        End Property

        ''' <summary>Part\Model\MODF\Flags</summary>
        Public Property ModelFlags As Byte
            Get
                Return CByte(Entero("Part\Model\MODF\Flags"))
            End Get
            Set(value As Byte)
                Escribir("Part\Model\MODF\Flags", CLng(value))
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Body Data\Female Body Data\Parts.</summary>
    Public NotInheritable Class RaceFO4_Parts2
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

        ''' <summary>Part\Model\MODC\Color Remapping Index</summary>
        Public Property ModelColorRemappingIndex As Single
            Get
                Return Flt("Part\Model\MODC\Color Remapping Index")
            End Get
            Set(value As Single)
                Escribir("Part\Model\MODC\Color Remapping Index", value)
            End Set
        End Property

        ''' <summary>Part\Model\MODS\Material Swap  -&gt;  MSWP. Referencia en el espacio del orden de carga.</summary>
        Public Property ModelMaterialSwap As UInteger
            Get
                Return Referencia("Part\Model\MODS\Material Swap")
            End Get
            Set(value As UInteger)
                PonerReferencia("Part\Model\MODS\Material Swap", value)
            End Set
        End Property

        ''' <summary>Part\Model\MODF\Flags</summary>
        Public Property ModelFlags As Byte
            Get
                Return CByte(Entero("Part\Model\MODF\Flags"))
            End Get
            Set(value As Byte)
                Escribir("Part\Model\MODF\Flags", CLng(value))
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Biped Object Names.</summary>
    Public NotInheritable Class RaceFO4_BipedObjectNames
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

    ''' <summary>Un elemento de RBPC\Biped Object Conditions.</summary>
    Public NotInheritable Class RaceFO4_BipedObjectConditions
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Slot 30+</summary>
        Public Property Slot30 As UInteger
            Get
                Return CUInt(Entero("Slot 30+"))
            End Get
            Set(value As UInteger)
                Escribir("Slot 30+", CLng(value))
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Movement Data Overrides.</summary>
    Public NotInheritable Class RaceFO4_MovementDataOverrides
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Override\MTYP\Movement Type  -&gt;  MOVT. Referencia en el espacio del orden de carga.</summary>
        Public Property OverrideMovementType As UInteger
            Get
                Return Referencia("Override\MTYP\Movement Type")
            End Get
            Set(value As UInteger)
                PonerReferencia("Override\MTYP\Movement Type", value)
            End Set
        End Property

        ''' <summary>Override\SPED\Left\Walk</summary>
        Public Property LeftWalk As Single
            Get
                Return Flt("Override\SPED\Left\Walk")
            End Get
            Set(value As Single)
                Escribir("Override\SPED\Left\Walk", value)
            End Set
        End Property

        ''' <summary>Override\SPED\Left\Run</summary>
        Public Property LeftRun As Single
            Get
                Return Flt("Override\SPED\Left\Run")
            End Get
            Set(value As Single)
                Escribir("Override\SPED\Left\Run", value)
            End Set
        End Property

        ''' <summary>Override\SPED\Right\Walk</summary>
        Public Property RightWalk As Single
            Get
                Return Flt("Override\SPED\Right\Walk")
            End Get
            Set(value As Single)
                Escribir("Override\SPED\Right\Walk", value)
            End Set
        End Property

        ''' <summary>Override\SPED\Right\Run</summary>
        Public Property RightRun As Single
            Get
                Return Flt("Override\SPED\Right\Run")
            End Get
            Set(value As Single)
                Escribir("Override\SPED\Right\Run", value)
            End Set
        End Property

        ''' <summary>Override\SPED\Forward\Walk</summary>
        Public Property ForwardWalk As Single
            Get
                Return Flt("Override\SPED\Forward\Walk")
            End Get
            Set(value As Single)
                Escribir("Override\SPED\Forward\Walk", value)
            End Set
        End Property

        ''' <summary>Override\SPED\Forward\Run</summary>
        Public Property ForwardRun As Single
            Get
                Return Flt("Override\SPED\Forward\Run")
            End Get
            Set(value As Single)
                Escribir("Override\SPED\Forward\Run", value)
            End Set
        End Property

        ''' <summary>Override\SPED\Back\Walk</summary>
        Public Property BackWalk As Single
            Get
                Return Flt("Override\SPED\Back\Walk")
            End Get
            Set(value As Single)
                Escribir("Override\SPED\Back\Walk", value)
            End Set
        End Property

        ''' <summary>Override\SPED\Back\Run</summary>
        Public Property BackRun As Single
            Get
                Return Flt("Override\SPED\Back\Run")
            End Get
            Set(value As Single)
                Escribir("Override\SPED\Back\Run", value)
            End Set
        End Property

        ''' <summary>Override\SPED\Pitch, Roll, Yaw\Walk</summary>
        Public Property PitchRollYawWalk As Single
            Get
                Return Flt("Override\SPED\Pitch, Roll, Yaw\Walk")
            End Get
            Set(value As Single)
                Escribir("Override\SPED\Pitch, Roll, Yaw\Walk", value)
            End Set
        End Property

        ''' <summary>Override\SPED\Pitch, Roll, Yaw\Run</summary>
        Public Property PitchRollYawRun As Single
            Get
                Return Flt("Override\SPED\Pitch, Roll, Yaw\Run")
            End Get
            Set(value As Single)
                Escribir("Override\SPED\Pitch, Roll, Yaw\Run", value)
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Equip Slots.</summary>
    Public NotInheritable Class RaceFO4_EquipSlots
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Equip Slot\QNAM\Equip Slot  -&gt;  EQUP. Referencia en el espacio del orden de carga.</summary>
        Public Property EquipSlotEquipSlot As UInteger
            Get
                Return Referencia("Equip Slot\QNAM\Equip Slot")
            End Get
            Set(value As UInteger)
                PonerReferencia("Equip Slot\QNAM\Equip Slot", value)
            End Set
        End Property

        ''' <summary>Equip Slot\ZNAM\Node</summary>
        Public Property EquipSlotNode As String
            Get
                Return Txt("Equip Slot\ZNAM\Node")
            End Get
            Set(value As String)
                Escribir("Equip Slot\ZNAM\Node", value)
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Phoneme Target Names.</summary>
    Public NotInheritable Class RaceFO4_PhonemeTargetNames
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

    ''' <summary>Un elemento de Male Head Parts.</summary>
    Public NotInheritable Class RaceFO4_MaleHeadParts
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

    ''' <summary>Un elemento de Male Race Presets.</summary>
    Public NotInheritable Class RaceFO4_MaleRacePresets
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

    ''' <summary>Un elemento de Male Hair Colors.</summary>
    Public NotInheritable Class RaceFO4_MaleHairColors
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

    ''' <summary>Un elemento de Male Face Details.</summary>
    Public NotInheritable Class RaceFO4_MaleFaceDetails
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

    ''' <summary>Un elemento de Male Tint Layers.</summary>
    Public NotInheritable Class RaceFO4_MaleTintLayers
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Group\TTGP\Group Name. Resuelto contra las tablas de texto si el archivo las usa.</summary>
        Public Property GroupGroupName As String
            Get
                Return TextoTraducible("Group\TTGP\Group Name")
            End Get
            Set(value As String)
                EscribirTextoTraducible("Group\TTGP\Group Name", value)
            End Set
        End Property

        ''' <summary>Group\TTGE\Category Index</summary>
        Public Property GroupCategoryIndex As UInteger
            Get
                Return CUInt(Entero("Group\TTGE\Category Index"))
            End Get
            Set(value As UInteger)
                Escribir("Group\TTGE\Category Index", CLng(value))
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Male Tint Layers\Group\Options.</summary>
    Public NotInheritable Class RaceFO4_Options
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Option\TETI\Index\Slot</summary>
        Public Property IndexSlot As UShort
            Get
                Return CUShort(Entero("Option\TETI\Index\Slot"))
            End Get
            Set(value As UShort)
                Escribir("Option\TETI\Index\Slot", CLng(value))
            End Set
        End Property

        ''' <summary>Option\TETI\Index\Index</summary>
        Public Property IndexIndex As UShort
            Get
                Return CUShort(Entero("Option\TETI\Index\Index"))
            End Get
            Set(value As UShort)
                Escribir("Option\TETI\Index\Index", CLng(value))
            End Set
        End Property

        ''' <summary>Option\TTGP\Name. Resuelto contra las tablas de texto si el archivo las usa.</summary>
        Public Property OptionName As String
            Get
                Return TextoTraducible("Option\TTGP\Name")
            End Get
            Set(value As String)
                EscribirTextoTraducible("Option\TTGP\Name", value)
            End Set
        End Property

        ''' <summary>Option\TTEF\Flags</summary>
        Public Property OptionFlags As UShort
            Get
                Return CUShort(Entero("Option\TTEF\Flags"))
            End Get
            Set(value As UShort)
                Escribir("Option\TTEF\Flags", CLng(value))
            End Set
        End Property

        ''' <summary>Option\TTEB\Blend Operation</summary>
        Public Property OptionBlendOperation As UInteger
            Get
                Return CUInt(Entero("Option\TTEB\Blend Operation"))
            End Get
            Set(value As UInteger)
                Escribir("Option\TTEB\Blend Operation", CLng(value))
            End Set
        End Property

        ''' <summary>Option\TTED\Default</summary>
        Public Property OptionDefault As Single
            Get
                Return Flt("Option\TTED\Default")
            End Get
            Set(value As Single)
                Escribir("Option\TTED\Default", value)
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Male Tint Layers\Group\Options\Option\Conditions.</summary>
    Public NotInheritable Class RaceFO4_Conditions
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Condition\CTDA\Type</summary>
        Public Property ConditionType As Byte
            Get
                Return CByte(Entero("Condition\CTDA\Type"))
            End Get
            Set(value As Byte)
                Escribir("Condition\CTDA\Type", CLng(value))
            End Set
        End Property

        ''' <summary>Condition\CTDA\Comparison Value - Float</summary>
        Public Property ConditionComparisonValueFloat As Single
            Get
                Return Flt("Condition\CTDA\Comparison Value - Float")
            End Get
            Set(value As Single)
                Escribir("Condition\CTDA\Comparison Value - Float", value)
            End Set
        End Property

        ''' <summary>Condition\CTDA\Function</summary>
        Public Property ConditionFunction As UShort
            Get
                Return CUShort(Entero("Condition\CTDA\Function"))
            End Get
            Set(value As UShort)
                Escribir("Condition\CTDA\Function", CLng(value))
            End Set
        End Property

        ''' <summary>Condition\CTDA\Run On</summary>
        Public Property ConditionRunOn As UInteger
            Get
                Return CUInt(Entero("Condition\CTDA\Run On"))
            End Get
            Set(value As UInteger)
                Escribir("Condition\CTDA\Run On", CLng(value))
            End Set
        End Property
        ''' <summary>Nombre del valor de Condition\CTDA\Run On.</summary>
        Public ReadOnly Property ConditionRunOnNombre As String
            Get
                Return NombreDeValor("Condition\CTDA\Run On")
            End Get
        End Property


        ''' <summary>Condition\CTDA\Parameter #3</summary>
        Public Property ConditionParameter3 As Integer
            Get
                Return CInt(Entero("Condition\CTDA\Parameter #3"))
            End Get
            Set(value As Integer)
                Escribir("Condition\CTDA\Parameter #3", CLng(value))
            End Set
        End Property

        ''' <summary>Condition\CIS1\Parameter #1</summary>
        Public Property ConditionParameter1 As String
            Get
                Return Txt("Condition\CIS1\Parameter #1")
            End Get
            Set(value As String)
                Escribir("Condition\CIS1\Parameter #1", value)
            End Set
        End Property

        ''' <summary>Condition\CIS2\Parameter #2</summary>
        Public Property ConditionParameter2 As String
            Get
                Return Txt("Condition\CIS2\Parameter #2")
            End Get
            Set(value As String)
                Escribir("Condition\CIS2\Parameter #2", value)
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Male Tint Layers\Group\Options\Option\Textures.</summary>
    Public NotInheritable Class RaceFO4_Textures
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>TTET\Texture</summary>
        Public Property Texture As String
            Get
                Return Txt("TTET\Texture")
            End Get
            Set(value As String)
                Escribir("TTET\Texture", value)
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Male Tint Layers\Group\Options\Option\TTEC\Template Colors.</summary>
    Public NotInheritable Class RaceFO4_TemplateColors
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Template Color\Color  -&gt;  CLFM. Referencia en el espacio del orden de carga.</summary>
        Public Property TemplateColorColor As UInteger
            Get
                Return Referencia("Template Color\Color")
            End Get
            Set(value As UInteger)
                PonerReferencia("Template Color\Color", value)
            End Set
        End Property

        ''' <summary>Template Color\Alpha</summary>
        Public Property TemplateColorAlpha As Single
            Get
                Return Flt("Template Color\Alpha")
            End Get
            Set(value As Single)
                Escribir("Template Color\Alpha", value)
            End Set
        End Property

        ''' <summary>Template Color\Template Index</summary>
        Public Property TemplateColorTemplateIndex As UShort
            Get
                Return CUShort(Entero("Template Color\Template Index"))
            End Get
            Set(value As UShort)
                Escribir("Template Color\Template Index", CLng(value))
            End Set
        End Property

        ''' <summary>Template Color\Blend Operation</summary>
        Public Property TemplateColorBlendOperation As UInteger
            Get
                Return CUInt(Entero("Template Color\Blend Operation"))
            End Get
            Set(value As UInteger)
                Escribir("Template Color\Blend Operation", CLng(value))
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Male Morph Groups.</summary>
    Public NotInheritable Class RaceFO4_MaleMorphGroups
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Morph Group\MPGN\Name</summary>
        Public Property MorphGroupName As String
            Get
                Return Txt("Morph Group\MPGN\Name")
            End Get
            Set(value As String)
                Escribir("Morph Group\MPGN\Name", value)
            End Set
        End Property

        ''' <summary>Morph Group\MPPC\Count</summary>
        Public Property MorphGroupCount As UInteger
            Get
                Return CUInt(Entero("Morph Group\MPPC\Count"))
            End Get
            Set(value As UInteger)
                Escribir("Morph Group\MPPC\Count", CLng(value))
            End Set
        End Property

        ''' <summary>Morph Group\MPPK\Mask</summary>
        Public Property MorphGroupMask As UShort
            Get
                Return CUShort(Entero("Morph Group\MPPK\Mask"))
            End Get
            Set(value As UShort)
                Escribir("Morph Group\MPPK\Mask", CLng(value))
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Male Morph Groups\Morph Group\Morph Presets.</summary>
    Public NotInheritable Class RaceFO4_MorphPresets
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Morph Preset\MPPI\Index</summary>
        Public Property MorphPresetIndex As UInteger
            Get
                Return CUInt(Entero("Morph Preset\MPPI\Index"))
            End Get
            Set(value As UInteger)
                Escribir("Morph Preset\MPPI\Index", CLng(value))
            End Set
        End Property

        ''' <summary>Morph Preset\MPPN\Name. Resuelto contra las tablas de texto si el archivo las usa.</summary>
        Public Property MorphPresetName As String
            Get
                Return TextoTraducible("Morph Preset\MPPN\Name")
            End Get
            Set(value As String)
                EscribirTextoTraducible("Morph Preset\MPPN\Name", value)
            End Set
        End Property

        ''' <summary>Morph Preset\MPPM\Morph</summary>
        Public Property MorphPresetMorph As String
            Get
                Return Txt("Morph Preset\MPPM\Morph")
            End Get
            Set(value As String)
                Escribir("Morph Preset\MPPM\Morph", value)
            End Set
        End Property

        ''' <summary>Morph Preset\MPPT\Texture  -&gt;  TXST. Referencia en el espacio del orden de carga.</summary>
        Public Property MorphPresetTexture As UInteger
            Get
                Return Referencia("Morph Preset\MPPT\Texture")
            End Get
            Set(value As UInteger)
                PonerReferencia("Morph Preset\MPPT\Texture", value)
            End Set
        End Property

        ''' <summary>Morph Preset\MPPF\Playable</summary>
        Public Property MorphPresetPlayable As Byte
            Get
                Return CByte(Entero("Morph Preset\MPPF\Playable"))
            End Get
            Set(value As Byte)
                Escribir("Morph Preset\MPPF\Playable", CLng(value))
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Male Morph Groups\Morph Group\MPGS\Morph Group Sliders.</summary>
    Public NotInheritable Class RaceFO4_MorphGroupSliders
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Index</summary>
        Public Property Index As UInteger
            Get
                Return CUInt(Entero("Index"))
            End Get
            Set(value As UInteger)
                Escribir("Index", CLng(value))
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Male Face Morphs.</summary>
    Public NotInheritable Class RaceFO4_MaleFaceMorphs
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Face Morph\FMRI\Index</summary>
        Public Property FaceMorphIndex As UInteger
            Get
                Return CUInt(Entero("Face Morph\FMRI\Index"))
            End Get
            Set(value As UInteger)
                Escribir("Face Morph\FMRI\Index", CLng(value))
            End Set
        End Property

        ''' <summary>Face Morph\FMRN\Name. Resuelto contra las tablas de texto si el archivo las usa.</summary>
        Public Property FaceMorphName As String
            Get
                Return TextoTraducible("Face Morph\FMRN\Name")
            End Get
            Set(value As String)
                EscribirTextoTraducible("Face Morph\FMRN\Name", value)
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Female Head Parts.</summary>
    Public NotInheritable Class RaceFO4_FemaleHeadParts
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

    ''' <summary>Un elemento de Female Race Presets.</summary>
    Public NotInheritable Class RaceFO4_FemaleRacePresets
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

    ''' <summary>Un elemento de Female Hair Colors.</summary>
    Public NotInheritable Class RaceFO4_FemaleHairColors
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

    ''' <summary>Un elemento de Female Face Details.</summary>
    Public NotInheritable Class RaceFO4_FemaleFaceDetails
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

    ''' <summary>Un elemento de Female Tint Layers.</summary>
    Public NotInheritable Class RaceFO4_FemaleTintLayers
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Group\TTGP\Group Name. Resuelto contra las tablas de texto si el archivo las usa.</summary>
        Public Property GroupGroupName As String
            Get
                Return TextoTraducible("Group\TTGP\Group Name")
            End Get
            Set(value As String)
                EscribirTextoTraducible("Group\TTGP\Group Name", value)
            End Set
        End Property

        ''' <summary>Group\TTGE\Category Index</summary>
        Public Property GroupCategoryIndex As UInteger
            Get
                Return CUInt(Entero("Group\TTGE\Category Index"))
            End Get
            Set(value As UInteger)
                Escribir("Group\TTGE\Category Index", CLng(value))
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Female Tint Layers\Group\Options.</summary>
    Public NotInheritable Class RaceFO4_Options2
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Option\TETI\Index\Slot</summary>
        Public Property IndexSlot As UShort
            Get
                Return CUShort(Entero("Option\TETI\Index\Slot"))
            End Get
            Set(value As UShort)
                Escribir("Option\TETI\Index\Slot", CLng(value))
            End Set
        End Property

        ''' <summary>Option\TETI\Index\Index</summary>
        Public Property IndexIndex As UShort
            Get
                Return CUShort(Entero("Option\TETI\Index\Index"))
            End Get
            Set(value As UShort)
                Escribir("Option\TETI\Index\Index", CLng(value))
            End Set
        End Property

        ''' <summary>Option\TTGP\Name. Resuelto contra las tablas de texto si el archivo las usa.</summary>
        Public Property OptionName As String
            Get
                Return TextoTraducible("Option\TTGP\Name")
            End Get
            Set(value As String)
                EscribirTextoTraducible("Option\TTGP\Name", value)
            End Set
        End Property

        ''' <summary>Option\TTEF\Flags</summary>
        Public Property OptionFlags As UShort
            Get
                Return CUShort(Entero("Option\TTEF\Flags"))
            End Get
            Set(value As UShort)
                Escribir("Option\TTEF\Flags", CLng(value))
            End Set
        End Property

        ''' <summary>Option\TTEB\Blend Operation</summary>
        Public Property OptionBlendOperation As UInteger
            Get
                Return CUInt(Entero("Option\TTEB\Blend Operation"))
            End Get
            Set(value As UInteger)
                Escribir("Option\TTEB\Blend Operation", CLng(value))
            End Set
        End Property

        ''' <summary>Option\TTED\Default</summary>
        Public Property OptionDefault As Single
            Get
                Return Flt("Option\TTED\Default")
            End Get
            Set(value As Single)
                Escribir("Option\TTED\Default", value)
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Female Tint Layers\Group\Options\Option\Conditions.</summary>
    Public NotInheritable Class RaceFO4_Conditions2
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Condition\CTDA\Type</summary>
        Public Property ConditionType As Byte
            Get
                Return CByte(Entero("Condition\CTDA\Type"))
            End Get
            Set(value As Byte)
                Escribir("Condition\CTDA\Type", CLng(value))
            End Set
        End Property

        ''' <summary>Condition\CTDA\Comparison Value - Float</summary>
        Public Property ConditionComparisonValueFloat As Single
            Get
                Return Flt("Condition\CTDA\Comparison Value - Float")
            End Get
            Set(value As Single)
                Escribir("Condition\CTDA\Comparison Value - Float", value)
            End Set
        End Property

        ''' <summary>Condition\CTDA\Function</summary>
        Public Property ConditionFunction As UShort
            Get
                Return CUShort(Entero("Condition\CTDA\Function"))
            End Get
            Set(value As UShort)
                Escribir("Condition\CTDA\Function", CLng(value))
            End Set
        End Property

        ''' <summary>Condition\CTDA\Run On</summary>
        Public Property ConditionRunOn As UInteger
            Get
                Return CUInt(Entero("Condition\CTDA\Run On"))
            End Get
            Set(value As UInteger)
                Escribir("Condition\CTDA\Run On", CLng(value))
            End Set
        End Property
        ''' <summary>Nombre del valor de Condition\CTDA\Run On.</summary>
        Public ReadOnly Property ConditionRunOnNombre As String
            Get
                Return NombreDeValor("Condition\CTDA\Run On")
            End Get
        End Property


        ''' <summary>Condition\CTDA\Parameter #3</summary>
        Public Property ConditionParameter3 As Integer
            Get
                Return CInt(Entero("Condition\CTDA\Parameter #3"))
            End Get
            Set(value As Integer)
                Escribir("Condition\CTDA\Parameter #3", CLng(value))
            End Set
        End Property

        ''' <summary>Condition\CIS1\Parameter #1</summary>
        Public Property ConditionParameter1 As String
            Get
                Return Txt("Condition\CIS1\Parameter #1")
            End Get
            Set(value As String)
                Escribir("Condition\CIS1\Parameter #1", value)
            End Set
        End Property

        ''' <summary>Condition\CIS2\Parameter #2</summary>
        Public Property ConditionParameter2 As String
            Get
                Return Txt("Condition\CIS2\Parameter #2")
            End Get
            Set(value As String)
                Escribir("Condition\CIS2\Parameter #2", value)
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Female Tint Layers\Group\Options\Option\Textures.</summary>
    Public NotInheritable Class RaceFO4_Textures2
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>TTET\Texture</summary>
        Public Property Texture As String
            Get
                Return Txt("TTET\Texture")
            End Get
            Set(value As String)
                Escribir("TTET\Texture", value)
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Female Tint Layers\Group\Options\Option\TTEC\Template Colors.</summary>
    Public NotInheritable Class RaceFO4_TemplateColors2
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Template Color\Color  -&gt;  CLFM. Referencia en el espacio del orden de carga.</summary>
        Public Property TemplateColorColor As UInteger
            Get
                Return Referencia("Template Color\Color")
            End Get
            Set(value As UInteger)
                PonerReferencia("Template Color\Color", value)
            End Set
        End Property

        ''' <summary>Template Color\Alpha</summary>
        Public Property TemplateColorAlpha As Single
            Get
                Return Flt("Template Color\Alpha")
            End Get
            Set(value As Single)
                Escribir("Template Color\Alpha", value)
            End Set
        End Property

        ''' <summary>Template Color\Template Index</summary>
        Public Property TemplateColorTemplateIndex As UShort
            Get
                Return CUShort(Entero("Template Color\Template Index"))
            End Get
            Set(value As UShort)
                Escribir("Template Color\Template Index", CLng(value))
            End Set
        End Property

        ''' <summary>Template Color\Blend Operation</summary>
        Public Property TemplateColorBlendOperation As UInteger
            Get
                Return CUInt(Entero("Template Color\Blend Operation"))
            End Get
            Set(value As UInteger)
                Escribir("Template Color\Blend Operation", CLng(value))
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Female Morph Groups.</summary>
    Public NotInheritable Class RaceFO4_FemaleMorphGroups
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Morph Group\MPGN\Name</summary>
        Public Property MorphGroupName As String
            Get
                Return Txt("Morph Group\MPGN\Name")
            End Get
            Set(value As String)
                Escribir("Morph Group\MPGN\Name", value)
            End Set
        End Property

        ''' <summary>Morph Group\MPPC\Count</summary>
        Public Property MorphGroupCount As UInteger
            Get
                Return CUInt(Entero("Morph Group\MPPC\Count"))
            End Get
            Set(value As UInteger)
                Escribir("Morph Group\MPPC\Count", CLng(value))
            End Set
        End Property

        ''' <summary>Morph Group\MPPK\Mask</summary>
        Public Property MorphGroupMask As UShort
            Get
                Return CUShort(Entero("Morph Group\MPPK\Mask"))
            End Get
            Set(value As UShort)
                Escribir("Morph Group\MPPK\Mask", CLng(value))
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Female Morph Groups\Morph Group\Morph Presets.</summary>
    Public NotInheritable Class RaceFO4_MorphPresets2
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Morph Preset\MPPI\Index</summary>
        Public Property MorphPresetIndex As UInteger
            Get
                Return CUInt(Entero("Morph Preset\MPPI\Index"))
            End Get
            Set(value As UInteger)
                Escribir("Morph Preset\MPPI\Index", CLng(value))
            End Set
        End Property

        ''' <summary>Morph Preset\MPPN\Name. Resuelto contra las tablas de texto si el archivo las usa.</summary>
        Public Property MorphPresetName As String
            Get
                Return TextoTraducible("Morph Preset\MPPN\Name")
            End Get
            Set(value As String)
                EscribirTextoTraducible("Morph Preset\MPPN\Name", value)
            End Set
        End Property

        ''' <summary>Morph Preset\MPPM\Morph</summary>
        Public Property MorphPresetMorph As String
            Get
                Return Txt("Morph Preset\MPPM\Morph")
            End Get
            Set(value As String)
                Escribir("Morph Preset\MPPM\Morph", value)
            End Set
        End Property

        ''' <summary>Morph Preset\MPPT\Texture  -&gt;  TXST. Referencia en el espacio del orden de carga.</summary>
        Public Property MorphPresetTexture As UInteger
            Get
                Return Referencia("Morph Preset\MPPT\Texture")
            End Get
            Set(value As UInteger)
                PonerReferencia("Morph Preset\MPPT\Texture", value)
            End Set
        End Property

        ''' <summary>Morph Preset\MPPF\Playable</summary>
        Public Property MorphPresetPlayable As Byte
            Get
                Return CByte(Entero("Morph Preset\MPPF\Playable"))
            End Get
            Set(value As Byte)
                Escribir("Morph Preset\MPPF\Playable", CLng(value))
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Female Morph Groups\Morph Group\MPGS\Morph Group Sliders.</summary>
    Public NotInheritable Class RaceFO4_MorphGroupSliders2
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Index</summary>
        Public Property Index As UInteger
            Get
                Return CUInt(Entero("Index"))
            End Get
            Set(value As UInteger)
                Escribir("Index", CLng(value))
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Female Face Morphs.</summary>
    Public NotInheritable Class RaceFO4_FemaleFaceMorphs
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Face Morph\FMRI\Index</summary>
        Public Property FaceMorphIndex As UInteger
            Get
                Return CUInt(Entero("Face Morph\FMRI\Index"))
            End Get
            Set(value As UInteger)
                Escribir("Face Morph\FMRI\Index", CLng(value))
            End Set
        End Property

        ''' <summary>Face Morph\FMRN\Name. Resuelto contra las tablas de texto si el archivo las usa.</summary>
        Public Property FaceMorphName As String
            Get
                Return TextoTraducible("Face Morph\FMRN\Name")
            End Get
            Set(value As String)
                EscribirTextoTraducible("Face Morph\FMRN\Name", value)
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Subgraph Data\Data\Actor Keywords.</summary>
    Public NotInheritable Class RaceFO4_ActorKeywords
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>SAKD\Keyword  -&gt;  KYWD. Referencia en el espacio del orden de carga.</summary>
        Public Property Keyword As UInteger
            Get
                Return Referencia("SAKD\Keyword")
            End Get
            Set(value As UInteger)
                PonerReferencia("SAKD\Keyword", value)
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Subgraph Data.</summary>
    Public NotInheritable Class RaceFO4_SubgraphData
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Data\SGNM\Behaviour Graph</summary>
        Public Property DataBehaviourGraph As String
            Get
                Return Txt("Data\SGNM\Behaviour Graph")
            End Get
            Set(value As String)
                Escribir("Data\SGNM\Behaviour Graph", value)
            End Set
        End Property

        ''' <summary>Data\SRAF\Flags\Role</summary>
        Public Property FlagsRole As UShort
            Get
                Return CUShort(Entero("Data\SRAF\Flags\Role"))
            End Get
            Set(value As UShort)
                Escribir("Data\SRAF\Flags\Role", CLng(value))
            End Set
        End Property
        ''' <summary>Nombre del valor de Data\SRAF\Flags\Role.</summary>
        Public ReadOnly Property FlagsRoleNombre As String
            Get
                Return NombreDeValor("Data\SRAF\Flags\Role")
            End Get
        End Property


        ''' <summary>Data\SRAF\Flags\Perspective</summary>
        Public Property FlagsPerspective As UShort
            Get
                Return CUShort(Entero("Data\SRAF\Flags\Perspective"))
            End Get
            Set(value As UShort)
                Escribir("Data\SRAF\Flags\Perspective", CLng(value))
            End Set
        End Property
        ''' <summary>Nombre del valor de Data\SRAF\Flags\Perspective.</summary>
        Public ReadOnly Property FlagsPerspectiveNombre As String
            Get
                Return NombreDeValor("Data\SRAF\Flags\Perspective")
            End Get
        End Property


    End Class

    ''' <summary>Un elemento de Subgraph Data\Data\Animation Paths.</summary>
    Public NotInheritable Class RaceFO4_AnimationPaths
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>SAPT\Path</summary>
        Public Property Path As String
            Get
                Return Txt("SAPT\Path")
            End Get
            Set(value As String)
                Escribir("SAPT\Path", value)
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Subgraph Data\Data\Target Keywords.</summary>
    Public NotInheritable Class RaceFO4_TargetKeywords
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>STKD\Keyword  -&gt;  KYWD. Referencia en el espacio del orden de carga.</summary>
        Public Property Keyword As UInteger
            Get
                Return Referencia("STKD\Keyword")
            End Get
            Set(value As UInteger)
                PonerReferencia("STKD\Keyword", value)
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Morph Values.</summary>
    Public NotInheritable Class RaceFO4_MorphValues
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Value\MSID\Index</summary>
        Public Property ValueIndex As UInteger
            Get
                Return CUInt(Entero("Value\MSID\Index"))
            End Get
            Set(value As UInteger)
                Escribir("Value\MSID\Index", CLng(value))
            End Set
        End Property

        ''' <summary>Value\MSM0\Min Name</summary>
        Public Property ValueMinName As String
            Get
                Return Txt("Value\MSM0\Min Name")
            End Get
            Set(value As String)
                Escribir("Value\MSM0\Min Name", value)
            End Set
        End Property

        ''' <summary>Value\MSM1\Max Name</summary>
        Public Property ValueMaxName As String
            Get
                Return Txt("Value\MSM1\Max Name")
            End Get
            Set(value As String)
                Escribir("Value\MSM1\Max Name", value)
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Bone Scale Data.</summary>
    Public NotInheritable Class RaceFO4_BoneScaleData
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Bone Data Set\Bone Weight Scale Data\BSMP\Weight Scale Target Gender</summary>
        Public Property BoneWeightScaleDataWeightScaleTargetGender As UInteger
            Get
                Return CUInt(Entero("Bone Data Set\Bone Weight Scale Data\BSMP\Weight Scale Target Gender"))
            End Get
            Set(value As UInteger)
                Escribir("Bone Data Set\Bone Weight Scale Data\BSMP\Weight Scale Target Gender", CLng(value))
            End Set
        End Property

        ''' <summary>Bone Data Set\Bone Range Modifier Data\BMMP\Range Modifier Target Gender</summary>
        Public Property BoneRangeModifierDataRangeModifierTargetGender As UInteger
            Get
                Return CUInt(Entero("Bone Data Set\Bone Range Modifier Data\BMMP\Range Modifier Target Gender"))
            End Get
            Set(value As UInteger)
                Escribir("Bone Data Set\Bone Range Modifier Data\BMMP\Range Modifier Target Gender", CLng(value))
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Bone Scale Data\Bone Data Set\Bone Weight Scale Data\Bone Weight Scales.</summary>
    Public NotInheritable Class RaceFO4_BoneWeightScales
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Bone Weight Scale Set\BSMB\Name</summary>
        Public Property BoneWeightScaleSetName As String
            Get
                Return Txt("Bone Weight Scale Set\BSMB\Name")
            End Get
            Set(value As String)
                Escribir("Bone Weight Scale Set\BSMB\Name", value)
            End Set
        End Property

        ''' <summary>Bone Weight Scale Set\BSMS\Scale Set\Thin\X</summary>
        Public Property ThinX As Single
            Get
                Return Flt("Bone Weight Scale Set\BSMS\Scale Set\Thin\X")
            End Get
            Set(value As Single)
                Escribir("Bone Weight Scale Set\BSMS\Scale Set\Thin\X", value)
            End Set
        End Property

        ''' <summary>Bone Weight Scale Set\BSMS\Scale Set\Thin\Y</summary>
        Public Property ThinY As Single
            Get
                Return Flt("Bone Weight Scale Set\BSMS\Scale Set\Thin\Y")
            End Get
            Set(value As Single)
                Escribir("Bone Weight Scale Set\BSMS\Scale Set\Thin\Y", value)
            End Set
        End Property

        ''' <summary>Bone Weight Scale Set\BSMS\Scale Set\Thin\Z</summary>
        Public Property ThinZ As Single
            Get
                Return Flt("Bone Weight Scale Set\BSMS\Scale Set\Thin\Z")
            End Get
            Set(value As Single)
                Escribir("Bone Weight Scale Set\BSMS\Scale Set\Thin\Z", value)
            End Set
        End Property

        ''' <summary>Bone Weight Scale Set\BSMS\Scale Set\Muscular\X</summary>
        Public Property MuscularX As Single
            Get
                Return Flt("Bone Weight Scale Set\BSMS\Scale Set\Muscular\X")
            End Get
            Set(value As Single)
                Escribir("Bone Weight Scale Set\BSMS\Scale Set\Muscular\X", value)
            End Set
        End Property

        ''' <summary>Bone Weight Scale Set\BSMS\Scale Set\Muscular\Y</summary>
        Public Property MuscularY As Single
            Get
                Return Flt("Bone Weight Scale Set\BSMS\Scale Set\Muscular\Y")
            End Get
            Set(value As Single)
                Escribir("Bone Weight Scale Set\BSMS\Scale Set\Muscular\Y", value)
            End Set
        End Property

        ''' <summary>Bone Weight Scale Set\BSMS\Scale Set\Muscular\Z</summary>
        Public Property MuscularZ As Single
            Get
                Return Flt("Bone Weight Scale Set\BSMS\Scale Set\Muscular\Z")
            End Get
            Set(value As Single)
                Escribir("Bone Weight Scale Set\BSMS\Scale Set\Muscular\Z", value)
            End Set
        End Property

        ''' <summary>Bone Weight Scale Set\BSMS\Scale Set\Fat\X</summary>
        Public Property FatX As Single
            Get
                Return Flt("Bone Weight Scale Set\BSMS\Scale Set\Fat\X")
            End Get
            Set(value As Single)
                Escribir("Bone Weight Scale Set\BSMS\Scale Set\Fat\X", value)
            End Set
        End Property

        ''' <summary>Bone Weight Scale Set\BSMS\Scale Set\Fat\Y</summary>
        Public Property FatY As Single
            Get
                Return Flt("Bone Weight Scale Set\BSMS\Scale Set\Fat\Y")
            End Get
            Set(value As Single)
                Escribir("Bone Weight Scale Set\BSMS\Scale Set\Fat\Y", value)
            End Set
        End Property

        ''' <summary>Bone Weight Scale Set\BSMS\Scale Set\Fat\Z</summary>
        Public Property FatZ As Single
            Get
                Return Flt("Bone Weight Scale Set\BSMS\Scale Set\Fat\Z")
            End Get
            Set(value As Single)
                Escribir("Bone Weight Scale Set\BSMS\Scale Set\Fat\Z", value)
            End Set
        End Property

    End Class

    ''' <summary>Un elemento de Bone Scale Data\Bone Data Set\Bone Range Modifier Data\Bone Range Modifiers.</summary>
    Public NotInheritable Class RaceFO4_BoneRangeModifiers
        Inherits CanonView

        Public Sub New(node As WbNode, ctx As WbContext, resolver As CanonResolver)
            MyBase.New(node, ctx, resolver)
        End Sub

        ''' <summary>Bone Range Modifier\BSMB\Name</summary>
        Public Property BoneRangeModifierName As String
            Get
                Return Txt("Bone Range Modifier\BSMB\Name")
            End Get
            Set(value As String)
                Escribir("Bone Range Modifier\BSMB\Name", value)
            End Set
        End Property

        ''' <summary>Bone Range Modifier\BSMS\Range\Min Y</summary>
        Public Property RangeMinY As Single
            Get
                Return Flt("Bone Range Modifier\BSMS\Range\Min Y")
            End Get
            Set(value As Single)
                Escribir("Bone Range Modifier\BSMS\Range\Min Y", value)
            End Set
        End Property

        ''' <summary>Bone Range Modifier\BSMS\Range\Min Z</summary>
        Public Property RangeMinZ As Single
            Get
                Return Flt("Bone Range Modifier\BSMS\Range\Min Z")
            End Get
            Set(value As Single)
                Escribir("Bone Range Modifier\BSMS\Range\Min Z", value)
            End Set
        End Property

        ''' <summary>Bone Range Modifier\BSMS\Range\Max Y</summary>
        Public Property RangeMaxY As Single
            Get
                Return Flt("Bone Range Modifier\BSMS\Range\Max Y")
            End Get
            Set(value As Single)
                Escribir("Bone Range Modifier\BSMS\Range\Max Y", value)
            End Set
        End Property

        ''' <summary>Bone Range Modifier\BSMS\Range\Max Z</summary>
        Public Property RangeMaxZ As Single
            Get
                Return Flt("Bone Range Modifier\BSMS\Range\Max Z")
            End Get
            Set(value As Single)
                Escribir("Bone Range Modifier\BSMS\Range\Max Z", value)
            End Set
        End Property

    End Class

    ''' <summary>Campos de un record TXST de Fallout 4.</summary>
    Public NotInheritable Class TxstFO4
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

        ''' <summary>Textures (RGB/A)\TX03\Glow</summary>
        Public Property TexturesRGBAGlow As String
            Get
                Return Txt("Textures (RGB/A)\TX03\Glow")
            End Get
            Set(value As String)
                Escribir("Textures (RGB/A)\TX03\Glow", value)
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

        ''' <summary>Textures (RGB/A)\TX02\Wrinkles</summary>
        Public Property TexturesRGBAWrinkles As String
            Get
                Return Txt("Textures (RGB/A)\TX02\Wrinkles")
            End Get
            Set(value As String)
                Escribir("Textures (RGB/A)\TX02\Wrinkles", value)
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

        ''' <summary>Textures (RGB/A)\TX07\Smooth Spec</summary>
        Public Property TexturesRGBASmoothSpec As String
            Get
                Return Txt("Textures (RGB/A)\TX07\Smooth Spec")
            End Get
            Set(value As String)
                Escribir("Textures (RGB/A)\TX07\Smooth Spec", value)
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

        ''' <summary>Bit 0 de DODT\Decal Data\Flags: POM Shadows</summary>
        Public Property DecalDataFlagsPOMShadows As Boolean
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
        ''' <summary>Bit 4 de DODT\Decal Data\Flags: Multiplicative Blending</summary>
        Public Property DecalDataFlagsMultiplicativeBlending As Boolean
            Get
                Return Bit("DODT\Decal Data\Flags", 4)
            End Get
            Set(value As Boolean)
                PonerBit("DODT\Decal Data\Flags", 4, value)
            End Set
        End Property

        ''' <summary>DODT\Decal Data\Alpha Threshold?</summary>
        Public Property DecalDataAlphaThreshold As UShort
            Get
                Return CUShort(Entero("DODT\Decal Data\Alpha Threshold?"))
            End Get
            Set(value As UShort)
                Escribir("DODT\Decal Data\Alpha Threshold?", CLng(value))
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

        ''' <summary>MNAM\Material</summary>
        Public Property Material As String
            Get
                Return Txt("MNAM\Material")
            End Get
            Set(value As String)
                Escribir("MNAM\Material", value)
            End Set
        End Property

    End Class

End Namespace
