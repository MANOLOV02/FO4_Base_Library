' ============================================================================================
' ARCHIVO GENERADO — NO EDITAR A MANO.  Regenerar: Tools/CanonViewGen
'
' Se genera desde el esquema, que a su vez deriva de las declaraciones de formato
' de xEdit, bajo Mozilla Public License 2.0. Es, por lo tanto, obra derivada.
'
' This Source Code Form is subject to the terms of the Mozilla Public License,
' v. 2.0. If a copy of the MPL was not distributed with this file, You can obtain
' one at https://mozilla.org/MPL/2.0/
'
' Interfaces: lo que se declara igual, entre juegos y entre records.
' El nombre de cada propiedad ES el nombre del campo en el formato: no hay ninguna
' tabla de equivalencias que mantener, y si el formato cambia un campo el codigo que
' lo usaba deja de compilar.
' ============================================================================================

Namespace Canon

    ''' <summary>Campos de un record ARMA que los dos juegos declaran igual.
    ''' <para>El codigo que solo usa estos no necesita saber de que juego se trata. El
    ''' que necesita algo propio de un juego tiene que nombrar la clase de ese juego, y
    ''' asi no puede leer sin querer un campo que alla significa otra cosa.</para></summary>
    Public Interface IArma
        ReadOnly Property Node As WbNode
        ''' <summary>Identificador del record.</summary>
        ReadOnly Property FormID As UInteger
        ''' <summary>El record trae EDID\Editor ID. Distinto de que el campo valga cero.</summary>
        Property EditorIDPresente As Boolean
        ''' <summary>EDID\Editor ID</summary>
        Property EditorID As String
        ''' <summary>El record trae BOD2\Biped Body Template\First Person Flags. Distinto de que el campo valga cero.</summary>
        Property BipedBodyTemplateFirstPersonFlagsPresente As Boolean
        ''' <summary>BOD2\Biped Body Template\First Person Flags</summary>
        Property BipedBodyTemplateFirstPersonFlags As UInteger
        ''' <summary>Bit 24 de BOD2\Biped Body Template\First Person Flags: 54 - Unnamed</summary>
        Property BipedBodyTemplateFirstPersonFlags54Unnamed As Boolean
        ''' <summary>Bit 25 de BOD2\Biped Body Template\First Person Flags: 55 - Unnamed</summary>
        Property BipedBodyTemplateFirstPersonFlags55Unnamed As Boolean
        ''' <summary>Bit 26 de BOD2\Biped Body Template\First Person Flags: 56 - Unnamed</summary>
        Property BipedBodyTemplateFirstPersonFlags56Unnamed As Boolean
        ''' <summary>Bit 27 de BOD2\Biped Body Template\First Person Flags: 57 - Unnamed</summary>
        Property BipedBodyTemplateFirstPersonFlags57Unnamed As Boolean
        ''' <summary>Bit 28 de BOD2\Biped Body Template\First Person Flags: 58 - Unnamed</summary>
        Property BipedBodyTemplateFirstPersonFlags58Unnamed As Boolean
        ''' <summary>El record trae RNAM\Race. Distinto de que el campo valga cero.</summary>
        Property RacePresente As Boolean
        ''' <summary>RNAM\Race  -&gt;  RACE. Referencia en el espacio del orden de carga.</summary>
        Property Race As UInteger
        ''' <summary>El record trae DNAM\Data\Male Priority. Distinto de que el campo valga cero.</summary>
        Property DataMalePriorityPresente As Boolean
        ''' <summary>DNAM\Data\Male Priority</summary>
        Property DataMalePriority As Byte
        ''' <summary>El record trae DNAM\Data\Female Priority. Distinto de que el campo valga cero.</summary>
        Property DataFemalePriorityPresente As Boolean
        ''' <summary>DNAM\Data\Female Priority</summary>
        Property DataFemalePriority As Byte
        ''' <summary>El record trae DNAM\Data\Weight slider - Male. Distinto de que el campo valga cero.</summary>
        Property DataWeightSliderMalePresente As Boolean
        ''' <summary>DNAM\Data\Weight slider - Male</summary>
        Property DataWeightSliderMale As Byte
        ''' <summary>Bit 1 de DNAM\Data\Weight slider - Male: Enabled</summary>
        Property DataWeightSliderMaleEnabled As Boolean
        ''' <summary>El record trae DNAM\Data\Weight slider - Female. Distinto de que el campo valga cero.</summary>
        Property DataWeightSliderFemalePresente As Boolean
        ''' <summary>DNAM\Data\Weight slider - Female</summary>
        Property DataWeightSliderFemale As Byte
        ''' <summary>Bit 1 de DNAM\Data\Weight slider - Female: Enabled</summary>
        Property DataWeightSliderFemaleEnabled As Boolean
        ''' <summary>El record trae DNAM\Data\Unknown. Distinto de que el campo valga cero.</summary>
        Property DataUnknownPresente As Boolean
        ''' <summary>DNAM\Data\Unknown</summary>
        Property DataUnknown As Byte()
        ''' <summary>El record trae DNAM\Data\Detection Sound Value. Distinto de que el campo valga cero.</summary>
        Property DataDetectionSoundValuePresente As Boolean
        ''' <summary>DNAM\Data\Detection Sound Value</summary>
        Property DataDetectionSoundValue As Byte
        ''' <summary>El record trae DNAM\Data\Weapon Adjust. Distinto de que el campo valga cero.</summary>
        Property DataWeaponAdjustPresente As Boolean
        ''' <summary>DNAM\Data\Weapon Adjust</summary>
        Property DataWeaponAdjust As Single
        ''' <summary>El record trae Biped Model\Male\MOD2\Model Filename. Distinto de que el campo valga cero.</summary>
        Property MaleModelFilenamePresente As Boolean
        ''' <summary>Biped Model\Male\MOD2\Model Filename</summary>
        Property MaleModelFilename As String
        ''' <summary>El record trae Biped Model\Male\MO2T\Model Information\ERROR. Distinto de que el campo valga cero.</summary>
        Property ModelInformationERRORPresente As Boolean
        ''' <summary>Biped Model\Male\MO2T\Model Information\ERROR</summary>
        Property ModelInformationERROR As Byte()
        ''' <summary>El record trae Biped Model\Female\MOD3\Model Filename. Distinto de que el campo valga cero.</summary>
        Property FemaleModelFilenamePresente As Boolean
        ''' <summary>Biped Model\Female\MOD3\Model Filename</summary>
        Property FemaleModelFilename As String
        ''' <summary>El record trae Biped Model\Female\MO3T\Model Information\ERROR. Distinto de que el campo valga cero.</summary>
        Property ModelInformationERROR2Presente As Boolean
        ''' <summary>Biped Model\Female\MO3T\Model Information\ERROR</summary>
        Property ModelInformationERROR2 As Byte()
        ''' <summary>El record trae 1st Person\Male\MOD4\Model Filename. Distinto de que el campo valga cero.</summary>
        Property MaleModelFilename2Presente As Boolean
        ''' <summary>1st Person\Male\MOD4\Model Filename</summary>
        Property MaleModelFilename2 As String
        ''' <summary>El record trae 1st Person\Male\MO4T\Model Information\ERROR. Distinto de que el campo valga cero.</summary>
        Property ModelInformationERROR3Presente As Boolean
        ''' <summary>1st Person\Male\MO4T\Model Information\ERROR</summary>
        Property ModelInformationERROR3 As Byte()
        ''' <summary>El record trae 1st Person\Female\MOD5\Model Filename. Distinto de que el campo valga cero.</summary>
        Property FemaleModelFilename2Presente As Boolean
        ''' <summary>1st Person\Female\MOD5\Model Filename</summary>
        Property FemaleModelFilename2 As String
        ''' <summary>El record trae 1st Person\Female\MO5T\Model Information\ERROR. Distinto de que el campo valga cero.</summary>
        Property ModelInformationERROR4Presente As Boolean
        ''' <summary>1st Person\Female\MO5T\Model Information\ERROR</summary>
        Property ModelInformationERROR4 As Byte()
        ''' <summary>El record trae NAM0\Male Skin Texture. Distinto de que el campo valga cero.</summary>
        Property MaleSkinTexturePresente As Boolean
        ''' <summary>NAM0\Male Skin Texture  -&gt;  TXST / NULL. Referencia en el espacio del orden de carga.</summary>
        Property MaleSkinTexture As UInteger
        ''' <summary>El record trae NAM1\Female Skin Texture. Distinto de que el campo valga cero.</summary>
        Property FemaleSkinTexturePresente As Boolean
        ''' <summary>NAM1\Female Skin Texture  -&gt;  TXST / NULL. Referencia en el espacio del orden de carga.</summary>
        Property FemaleSkinTexture As UInteger
        ''' <summary>El record trae NAM2\Male Skin Texture Swap List. Distinto de que el campo valga cero.</summary>
        Property MaleSkinTextureSwapListPresente As Boolean
        ''' <summary>NAM2\Male Skin Texture Swap List  -&gt;  FLST / NULL. Referencia en el espacio del orden de carga.</summary>
        Property MaleSkinTextureSwapList As UInteger
        ''' <summary>El record trae NAM3\Female Skin Texture Swap List. Distinto de que el campo valga cero.</summary>
        Property FemaleSkinTextureSwapListPresente As Boolean
        ''' <summary>NAM3\Female Skin Texture Swap List  -&gt;  FLST / NULL. Referencia en el espacio del orden de carga.</summary>
        Property FemaleSkinTextureSwapList As UInteger
        ''' <summary>El record trae SNDD\Footstep Sound. Distinto de que el campo valga cero.</summary>
        Property FootstepSoundPresente As Boolean
        ''' <summary>SNDD\Footstep Sound  -&gt;  FSTS / NULL. Referencia en el espacio del orden de carga.</summary>
        Property FootstepSound As UInteger
        ''' <summary>El record trae ONAM\Art Object. Distinto de que el campo valga cero.</summary>
        Property ArtObjectPresente As Boolean
        ''' <summary>ONAM\Art Object  -&gt;  ARTO. Referencia en el espacio del orden de carga.</summary>
        Property ArtObject As UInteger
        ''' <summary>Biped Model\Male\MO2T\Model Information\Textures</summary>
        ReadOnly Property Textures As IReadOnlyList(Of IArma_Textures)
        Function AgregarTextures() As IArma_Textures
        Function QuitarTextures(indice As Integer) As Boolean
        Function ReordenarTextures(permutacion As IList(Of Integer)) As Boolean
        Function QuitarTextures(elemento As IArma_Textures) As Boolean
        ''' <summary>Biped Model\Male\MO2T\Model Information\Counters</summary>
        ReadOnly Property Counters As IReadOnlyList(Of IArma_Counters)
        Function AgregarCounters() As IArma_Counters
        Function QuitarCounters(indice As Integer) As Boolean
        Function ReordenarCounters(permutacion As IList(Of Integer)) As Boolean
        Function QuitarCounters(elemento As IArma_Counters) As Boolean
        ''' <summary>Biped Model\Male\MO2T\Model Information\Addon Nodes</summary>
        ReadOnly Property AddonNodes As IReadOnlyList(Of IArma_AddonNodes)
        Function AgregarAddonNodes() As IArma_AddonNodes
        Function QuitarAddonNodes(indice As Integer) As Boolean
        Function ReordenarAddonNodes(permutacion As IList(Of Integer)) As Boolean
        Function QuitarAddonNodes(elemento As IArma_AddonNodes) As Boolean
        ''' <summary>Biped Model\Female\MO3T\Model Information\Textures</summary>
        ReadOnly Property Textures2 As IReadOnlyList(Of IArma_Textures2)
        Function AgregarTextures2() As IArma_Textures2
        Function QuitarTextures2(indice As Integer) As Boolean
        Function ReordenarTextures2(permutacion As IList(Of Integer)) As Boolean
        Function QuitarTextures2(elemento As IArma_Textures2) As Boolean
        ''' <summary>Biped Model\Female\MO3T\Model Information\Counters</summary>
        ReadOnly Property Counters2 As IReadOnlyList(Of IArma_Counters2)
        Function AgregarCounters2() As IArma_Counters2
        Function QuitarCounters2(indice As Integer) As Boolean
        Function ReordenarCounters2(permutacion As IList(Of Integer)) As Boolean
        Function QuitarCounters2(elemento As IArma_Counters2) As Boolean
        ''' <summary>Biped Model\Female\MO3T\Model Information\Addon Nodes</summary>
        ReadOnly Property AddonNodes2 As IReadOnlyList(Of IArma_AddonNodes2)
        Function AgregarAddonNodes2() As IArma_AddonNodes2
        Function QuitarAddonNodes2(indice As Integer) As Boolean
        Function ReordenarAddonNodes2(permutacion As IList(Of Integer)) As Boolean
        Function QuitarAddonNodes2(elemento As IArma_AddonNodes2) As Boolean
        ''' <summary>1st Person\Male\MO4T\Model Information\Textures</summary>
        ReadOnly Property Textures3 As IReadOnlyList(Of IArma_Textures3)
        Function AgregarTextures3() As IArma_Textures3
        Function QuitarTextures3(indice As Integer) As Boolean
        Function ReordenarTextures3(permutacion As IList(Of Integer)) As Boolean
        Function QuitarTextures3(elemento As IArma_Textures3) As Boolean
        ''' <summary>1st Person\Male\MO4T\Model Information\Counters</summary>
        ReadOnly Property Counters3 As IReadOnlyList(Of IArma_Counters3)
        Function AgregarCounters3() As IArma_Counters3
        Function QuitarCounters3(indice As Integer) As Boolean
        Function ReordenarCounters3(permutacion As IList(Of Integer)) As Boolean
        Function QuitarCounters3(elemento As IArma_Counters3) As Boolean
        ''' <summary>1st Person\Male\MO4T\Model Information\Addon Nodes</summary>
        ReadOnly Property AddonNodes3 As IReadOnlyList(Of IArma_AddonNodes3)
        Function AgregarAddonNodes3() As IArma_AddonNodes3
        Function QuitarAddonNodes3(indice As Integer) As Boolean
        Function ReordenarAddonNodes3(permutacion As IList(Of Integer)) As Boolean
        Function QuitarAddonNodes3(elemento As IArma_AddonNodes3) As Boolean
        ''' <summary>1st Person\Female\MO5T\Model Information\Textures</summary>
        ReadOnly Property Textures4 As IReadOnlyList(Of IArma_Textures4)
        Function AgregarTextures4() As IArma_Textures4
        Function QuitarTextures4(indice As Integer) As Boolean
        Function ReordenarTextures4(permutacion As IList(Of Integer)) As Boolean
        Function QuitarTextures4(elemento As IArma_Textures4) As Boolean
        ''' <summary>1st Person\Female\MO5T\Model Information\Counters</summary>
        ReadOnly Property Counters4 As IReadOnlyList(Of IArma_Counters4)
        Function AgregarCounters4() As IArma_Counters4
        Function QuitarCounters4(indice As Integer) As Boolean
        Function ReordenarCounters4(permutacion As IList(Of Integer)) As Boolean
        Function QuitarCounters4(elemento As IArma_Counters4) As Boolean
        ''' <summary>1st Person\Female\MO5T\Model Information\Addon Nodes</summary>
        ReadOnly Property AddonNodes4 As IReadOnlyList(Of IArma_AddonNodes4)
        Function AgregarAddonNodes4() As IArma_AddonNodes4
        Function QuitarAddonNodes4(indice As Integer) As Boolean
        Function ReordenarAddonNodes4(permutacion As IList(Of Integer)) As Boolean
        Function QuitarAddonNodes4(elemento As IArma_AddonNodes4) As Boolean
        ''' <summary>Additional Races</summary>
        ReadOnly Property AdditionalRaces As IReadOnlyList(Of IArma_AdditionalRaces)
        Function AgregarAdditionalRaces() As IArma_AdditionalRaces
        Function QuitarAdditionalRaces(indice As Integer) As Boolean
        Function ReordenarAdditionalRaces(permutacion As IList(Of Integer)) As Boolean
        Function QuitarAdditionalRaces(elemento As IArma_AdditionalRaces) As Boolean
    End Interface

    ''' <summary>Un elemento de Biped Model\Male\MO2T\Model Information\Textures, en lo que los dos juegos comparten.</summary>
    Public Interface IArma_Textures
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Texture\File Hash. Distinto de que el campo valga cero.</summary>
        Property TextureFileHashPresente As Boolean
        ''' <summary>Texture\File Hash</summary>
        Property TextureFileHash As UInteger
        ''' <summary>El record trae Texture\Extension. Distinto de que el campo valga cero.</summary>
        Property TextureExtensionPresente As Boolean
        ''' <summary>Texture\Extension</summary>
        Property TextureExtension As String
        ''' <summary>El record trae Texture\Folder Hash. Distinto de que el campo valga cero.</summary>
        Property TextureFolderHashPresente As Boolean
        ''' <summary>Texture\Folder Hash</summary>
        Property TextureFolderHash As UInteger
    End Interface

    ''' <summary>Un elemento de Biped Model\Male\MO2T\Model Information\Counters, en lo que los dos juegos comparten.</summary>
    Public Interface IArma_Counters
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Counter. Distinto de que el campo valga cero.</summary>
        Property CounterPresente As Boolean
        ''' <summary>Counter</summary>
        Property Counter As UInteger
    End Interface

    ''' <summary>Un elemento de Biped Model\Male\MO2T\Model Information\Addon Nodes, en lo que los dos juegos comparten.</summary>
    Public Interface IArma_AddonNodes
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Addon Node. Distinto de que el campo valga cero.</summary>
        Property AddonNodePresente As Boolean
        ''' <summary>Addon Node</summary>
        Property AddonNode As UInteger
    End Interface

    ''' <summary>Un elemento de Biped Model\Female\MO3T\Model Information\Textures, en lo que los dos juegos comparten.</summary>
    Public Interface IArma_Textures2
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Texture\File Hash. Distinto de que el campo valga cero.</summary>
        Property TextureFileHashPresente As Boolean
        ''' <summary>Texture\File Hash</summary>
        Property TextureFileHash As UInteger
        ''' <summary>El record trae Texture\Extension. Distinto de que el campo valga cero.</summary>
        Property TextureExtensionPresente As Boolean
        ''' <summary>Texture\Extension</summary>
        Property TextureExtension As String
        ''' <summary>El record trae Texture\Folder Hash. Distinto de que el campo valga cero.</summary>
        Property TextureFolderHashPresente As Boolean
        ''' <summary>Texture\Folder Hash</summary>
        Property TextureFolderHash As UInteger
    End Interface

    ''' <summary>Un elemento de Biped Model\Female\MO3T\Model Information\Counters, en lo que los dos juegos comparten.</summary>
    Public Interface IArma_Counters2
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Counter. Distinto de que el campo valga cero.</summary>
        Property CounterPresente As Boolean
        ''' <summary>Counter</summary>
        Property Counter As UInteger
    End Interface

    ''' <summary>Un elemento de Biped Model\Female\MO3T\Model Information\Addon Nodes, en lo que los dos juegos comparten.</summary>
    Public Interface IArma_AddonNodes2
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Addon Node. Distinto de que el campo valga cero.</summary>
        Property AddonNodePresente As Boolean
        ''' <summary>Addon Node</summary>
        Property AddonNode As UInteger
    End Interface

    ''' <summary>Un elemento de 1st Person\Male\MO4T\Model Information\Textures, en lo que los dos juegos comparten.</summary>
    Public Interface IArma_Textures3
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Texture\File Hash. Distinto de que el campo valga cero.</summary>
        Property TextureFileHashPresente As Boolean
        ''' <summary>Texture\File Hash</summary>
        Property TextureFileHash As UInteger
        ''' <summary>El record trae Texture\Extension. Distinto de que el campo valga cero.</summary>
        Property TextureExtensionPresente As Boolean
        ''' <summary>Texture\Extension</summary>
        Property TextureExtension As String
        ''' <summary>El record trae Texture\Folder Hash. Distinto de que el campo valga cero.</summary>
        Property TextureFolderHashPresente As Boolean
        ''' <summary>Texture\Folder Hash</summary>
        Property TextureFolderHash As UInteger
    End Interface

    ''' <summary>Un elemento de 1st Person\Male\MO4T\Model Information\Counters, en lo que los dos juegos comparten.</summary>
    Public Interface IArma_Counters3
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Counter. Distinto de que el campo valga cero.</summary>
        Property CounterPresente As Boolean
        ''' <summary>Counter</summary>
        Property Counter As UInteger
    End Interface

    ''' <summary>Un elemento de 1st Person\Male\MO4T\Model Information\Addon Nodes, en lo que los dos juegos comparten.</summary>
    Public Interface IArma_AddonNodes3
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Addon Node. Distinto de que el campo valga cero.</summary>
        Property AddonNodePresente As Boolean
        ''' <summary>Addon Node</summary>
        Property AddonNode As UInteger
    End Interface

    ''' <summary>Un elemento de 1st Person\Female\MO5T\Model Information\Textures, en lo que los dos juegos comparten.</summary>
    Public Interface IArma_Textures4
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Texture\File Hash. Distinto de que el campo valga cero.</summary>
        Property TextureFileHashPresente As Boolean
        ''' <summary>Texture\File Hash</summary>
        Property TextureFileHash As UInteger
        ''' <summary>El record trae Texture\Extension. Distinto de que el campo valga cero.</summary>
        Property TextureExtensionPresente As Boolean
        ''' <summary>Texture\Extension</summary>
        Property TextureExtension As String
        ''' <summary>El record trae Texture\Folder Hash. Distinto de que el campo valga cero.</summary>
        Property TextureFolderHashPresente As Boolean
        ''' <summary>Texture\Folder Hash</summary>
        Property TextureFolderHash As UInteger
    End Interface

    ''' <summary>Un elemento de 1st Person\Female\MO5T\Model Information\Counters, en lo que los dos juegos comparten.</summary>
    Public Interface IArma_Counters4
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Counter. Distinto de que el campo valga cero.</summary>
        Property CounterPresente As Boolean
        ''' <summary>Counter</summary>
        Property Counter As UInteger
    End Interface

    ''' <summary>Un elemento de 1st Person\Female\MO5T\Model Information\Addon Nodes, en lo que los dos juegos comparten.</summary>
    Public Interface IArma_AddonNodes4
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Addon Node. Distinto de que el campo valga cero.</summary>
        Property AddonNodePresente As Boolean
        ''' <summary>Addon Node</summary>
        Property AddonNode As UInteger
    End Interface

    ''' <summary>Un elemento de Additional Races, en lo que los dos juegos comparten.</summary>
    Public Interface IArma_AdditionalRaces
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae MODL\Race. Distinto de que el campo valga cero.</summary>
        Property RacePresente As Boolean
        ''' <summary>MODL\Race  -&gt;  RACE / NULL. Referencia en el espacio del orden de carga.</summary>
        Property Race As UInteger
    End Interface

    ''' <summary>Campos de un record ARMO que los dos juegos declaran igual.
    ''' <para>El codigo que solo usa estos no necesita saber de que juego se trata. El
    ''' que necesita algo propio de un juego tiene que nombrar la clase de ese juego, y
    ''' asi no puede leer sin querer un campo que alla significa otra cosa.</para></summary>
    Public Interface IArmo
        ReadOnly Property Node As WbNode
        ''' <summary>Identificador del record.</summary>
        ReadOnly Property FormID As UInteger
        ''' <summary>Bit 2 de las banderas de la cabecera del record: Non-Playable.</summary>
        Property NonPlayable As Boolean
        ''' <summary>Bit 6 de las banderas de la cabecera del record: Shield.</summary>
        Property Shield As Boolean
        ''' <summary>El record trae EDID\Editor ID. Distinto de que el campo valga cero.</summary>
        Property EditorIDPresente As Boolean
        ''' <summary>EDID\Editor ID</summary>
        Property EditorID As String
        ''' <summary>El record trae VMAD\Virtual Machine Adapter\Version. Distinto de que el campo valga cero.</summary>
        Property VirtualMachineAdapterVersionPresente As Boolean
        ''' <summary>VMAD\Virtual Machine Adapter\Version</summary>
        Property VirtualMachineAdapterVersion As Short
        ''' <summary>El record trae VMAD\Virtual Machine Adapter\Object Format. Distinto de que el campo valga cero.</summary>
        Property VirtualMachineAdapterObjectFormatPresente As Boolean
        ''' <summary>VMAD\Virtual Machine Adapter\Object Format</summary>
        Property VirtualMachineAdapterObjectFormat As Short
        ''' <summary>El record trae OBND\Object Bounds\X1. Distinto de que el campo valga cero.</summary>
        Property ObjectBoundsX1Presente As Boolean
        ''' <summary>OBND\Object Bounds\X1</summary>
        Property ObjectBoundsX1 As Short
        ''' <summary>El record trae OBND\Object Bounds\Y1. Distinto de que el campo valga cero.</summary>
        Property ObjectBoundsY1Presente As Boolean
        ''' <summary>OBND\Object Bounds\Y1</summary>
        Property ObjectBoundsY1 As Short
        ''' <summary>El record trae OBND\Object Bounds\Z1. Distinto de que el campo valga cero.</summary>
        Property ObjectBoundsZ1Presente As Boolean
        ''' <summary>OBND\Object Bounds\Z1</summary>
        Property ObjectBoundsZ1 As Short
        ''' <summary>El record trae OBND\Object Bounds\X2. Distinto de que el campo valga cero.</summary>
        Property ObjectBoundsX2Presente As Boolean
        ''' <summary>OBND\Object Bounds\X2</summary>
        Property ObjectBoundsX2 As Short
        ''' <summary>El record trae OBND\Object Bounds\Y2. Distinto de que el campo valga cero.</summary>
        Property ObjectBoundsY2Presente As Boolean
        ''' <summary>OBND\Object Bounds\Y2</summary>
        Property ObjectBoundsY2 As Short
        ''' <summary>El record trae OBND\Object Bounds\Z2. Distinto de que el campo valga cero.</summary>
        Property ObjectBoundsZ2Presente As Boolean
        ''' <summary>OBND\Object Bounds\Z2</summary>
        Property ObjectBoundsZ2 As Short
        ''' <summary>El record trae FULL\Name. Distinto de que el campo valga cero.</summary>
        Property NamePresente As Boolean
        ''' <summary>FULL\Name. Resuelto contra las tablas de texto si el archivo las usa.</summary>
        Property Name As String
        ''' <summary>El record trae EITM\Enchantment. Distinto de que el campo valga cero.</summary>
        Property EnchantmentPresente As Boolean
        ''' <summary>EITM\Enchantment  -&gt;  ENCH. Referencia en el espacio del orden de carga.</summary>
        Property Enchantment As UInteger
        ''' <summary>El record trae Male\World Model\MOD2\Model Filename. Distinto de que el campo valga cero.</summary>
        Property WorldModelModelFilenamePresente As Boolean
        ''' <summary>Male\World Model\MOD2\Model Filename</summary>
        Property WorldModelModelFilename As String
        ''' <summary>El record trae Male\World Model\MO2T\Model Information\ERROR. Distinto de que el campo valga cero.</summary>
        Property ModelInformationERRORPresente As Boolean
        ''' <summary>Male\World Model\MO2T\Model Information\ERROR</summary>
        Property ModelInformationERROR As Byte()
        ''' <summary>El record trae Male\ICON\Icon Image. Distinto de que el campo valga cero.</summary>
        Property MaleIconImagePresente As Boolean
        ''' <summary>Male\ICON\Icon Image</summary>
        Property MaleIconImage As String
        ''' <summary>El record trae Male\MICO\Message Icon. Distinto de que el campo valga cero.</summary>
        Property MaleMessageIconPresente As Boolean
        ''' <summary>Male\MICO\Message Icon</summary>
        Property MaleMessageIcon As String
        ''' <summary>El record trae Female\World Model\MOD4\Model Filename. Distinto de que el campo valga cero.</summary>
        Property WorldModelModelFilename2Presente As Boolean
        ''' <summary>Female\World Model\MOD4\Model Filename</summary>
        Property WorldModelModelFilename2 As String
        ''' <summary>El record trae Female\World Model\MO4T\Model Information\ERROR. Distinto de que el campo valga cero.</summary>
        Property ModelInformationERROR2Presente As Boolean
        ''' <summary>Female\World Model\MO4T\Model Information\ERROR</summary>
        Property ModelInformationERROR2 As Byte()
        ''' <summary>El record trae Female\ICO2\Icon Image. Distinto de que el campo valga cero.</summary>
        Property FemaleIconImagePresente As Boolean
        ''' <summary>Female\ICO2\Icon Image</summary>
        Property FemaleIconImage As String
        ''' <summary>El record trae Female\MIC2\Message Icon. Distinto de que el campo valga cero.</summary>
        Property FemaleMessageIconPresente As Boolean
        ''' <summary>Female\MIC2\Message Icon</summary>
        Property FemaleMessageIcon As String
        ''' <summary>El record trae BOD2\Biped Body Template\First Person Flags. Distinto de que el campo valga cero.</summary>
        Property BipedBodyTemplateFirstPersonFlagsPresente As Boolean
        ''' <summary>BOD2\Biped Body Template\First Person Flags</summary>
        Property BipedBodyTemplateFirstPersonFlags As UInteger
        ''' <summary>Bit 24 de BOD2\Biped Body Template\First Person Flags: 54 - Unnamed</summary>
        Property BipedBodyTemplateFirstPersonFlags54Unnamed As Boolean
        ''' <summary>Bit 25 de BOD2\Biped Body Template\First Person Flags: 55 - Unnamed</summary>
        Property BipedBodyTemplateFirstPersonFlags55Unnamed As Boolean
        ''' <summary>Bit 26 de BOD2\Biped Body Template\First Person Flags: 56 - Unnamed</summary>
        Property BipedBodyTemplateFirstPersonFlags56Unnamed As Boolean
        ''' <summary>Bit 27 de BOD2\Biped Body Template\First Person Flags: 57 - Unnamed</summary>
        Property BipedBodyTemplateFirstPersonFlags57Unnamed As Boolean
        ''' <summary>Bit 28 de BOD2\Biped Body Template\First Person Flags: 58 - Unnamed</summary>
        Property BipedBodyTemplateFirstPersonFlags58Unnamed As Boolean
        ''' <summary>El record trae Destructible\DEST\Header\Health. Distinto de que el campo valga cero.</summary>
        Property HeaderHealthPresente As Boolean
        ''' <summary>Destructible\DEST\Header\Health</summary>
        Property HeaderHealth As Integer
        ''' <summary>El record trae Destructible\DEST\Header\DEST Count. Distinto de que el campo valga cero.</summary>
        Property HeaderDESTCountPresente As Boolean
        ''' <summary>Destructible\DEST\Header\DEST Count</summary>
        Property HeaderDESTCount As Byte
        ''' <summary>El record trae Destructible\DEST\Header\Unknown. Distinto de que el campo valga cero.</summary>
        Property HeaderUnknownPresente As Boolean
        ''' <summary>Destructible\DEST\Header\Unknown</summary>
        Property HeaderUnknown As Byte()
        ''' <summary>El record trae YNAM\Sound - Pick Up. Distinto de que el campo valga cero.</summary>
        Property SoundPickUpPresente As Boolean
        ''' <summary>YNAM\Sound - Pick Up  -&gt;  SNDR. Referencia en el espacio del orden de carga.</summary>
        Property SoundPickUp As UInteger
        ''' <summary>El record trae ZNAM\Sound - Put Down. Distinto de que el campo valga cero.</summary>
        Property SoundPutDownPresente As Boolean
        ''' <summary>ZNAM\Sound - Put Down  -&gt;  SNDR. Referencia en el espacio del orden de carga.</summary>
        Property SoundPutDown As UInteger
        ''' <summary>El record trae ETYP\Equipment Type. Distinto de que el campo valga cero.</summary>
        Property EquipmentTypePresente As Boolean
        ''' <summary>ETYP\Equipment Type  -&gt;  EQUP / NULL. Referencia en el espacio del orden de carga.</summary>
        Property EquipmentType As UInteger
        ''' <summary>El record trae BAMT\Alternate Block Material. Distinto de que el campo valga cero.</summary>
        Property AlternateBlockMaterialPresente As Boolean
        ''' <summary>BAMT\Alternate Block Material  -&gt;  MATT / NULL. Referencia en el espacio del orden de carga.</summary>
        Property AlternateBlockMaterial As UInteger
        ''' <summary>El record trae RNAM\Race. Distinto de que el campo valga cero.</summary>
        Property RacePresente As Boolean
        ''' <summary>RNAM\Race  -&gt;  RACE. Referencia en el espacio del orden de carga.</summary>
        Property Race As UInteger
        ''' <summary>El record trae Keywords\KSIZ\Keyword Count. Distinto de que el campo valga cero.</summary>
        Property KeywordsKeywordCountPresente As Boolean
        ''' <summary>Keywords\KSIZ\Keyword Count</summary>
        Property KeywordsKeywordCount As UInteger
        ''' <summary>El record trae DESC\Description. Distinto de que el campo valga cero.</summary>
        Property DescriptionPresente As Boolean
        ''' <summary>DESC\Description. Resuelto contra las tablas de texto si el archivo las usa.</summary>
        Property Description As String
        ''' <summary>El record trae FNAM\Armor Rating. Distinto de que el campo valga cero.</summary>
        Property ArmorRatingPresente As Boolean
        ''' <summary>El record trae TNAM\Template Armor. Distinto de que el campo valga cero.</summary>
        Property TemplateArmorPresente As Boolean
        ''' <summary>TNAM\Template Armor  -&gt;  ARMO. Referencia en el espacio del orden de carga.</summary>
        Property TemplateArmor As UInteger
        ''' <summary>VMAD\Virtual Machine Adapter\Scripts</summary>
        ReadOnly Property Scripts As IReadOnlyList(Of IArmo_Scripts)
        Function AgregarScripts() As IArmo_Scripts
        Function QuitarScripts(indice As Integer) As Boolean
        Function ReordenarScripts(permutacion As IList(Of Integer)) As Boolean
        Function QuitarScripts(elemento As IArmo_Scripts) As Boolean
        ''' <summary>Male\World Model\MO2T\Model Information\Textures</summary>
        ReadOnly Property Textures As IReadOnlyList(Of IArmo_Textures)
        Function AgregarTextures() As IArmo_Textures
        Function QuitarTextures(indice As Integer) As Boolean
        Function ReordenarTextures(permutacion As IList(Of Integer)) As Boolean
        Function QuitarTextures(elemento As IArmo_Textures) As Boolean
        ''' <summary>Male\World Model\MO2T\Model Information\Counters</summary>
        ReadOnly Property Counters As IReadOnlyList(Of IArmo_Counters)
        Function AgregarCounters() As IArmo_Counters
        Function QuitarCounters(indice As Integer) As Boolean
        Function ReordenarCounters(permutacion As IList(Of Integer)) As Boolean
        Function QuitarCounters(elemento As IArmo_Counters) As Boolean
        ''' <summary>Male\World Model\MO2T\Model Information\Addon Nodes</summary>
        ReadOnly Property AddonNodes As IReadOnlyList(Of IArmo_AddonNodes)
        Function AgregarAddonNodes() As IArmo_AddonNodes
        Function QuitarAddonNodes(indice As Integer) As Boolean
        Function ReordenarAddonNodes(permutacion As IList(Of Integer)) As Boolean
        Function QuitarAddonNodes(elemento As IArmo_AddonNodes) As Boolean
        ''' <summary>Female\World Model\MO4T\Model Information\Textures</summary>
        ReadOnly Property Textures2 As IReadOnlyList(Of IArmo_Textures2)
        Function AgregarTextures2() As IArmo_Textures2
        Function QuitarTextures2(indice As Integer) As Boolean
        Function ReordenarTextures2(permutacion As IList(Of Integer)) As Boolean
        Function QuitarTextures2(elemento As IArmo_Textures2) As Boolean
        ''' <summary>Female\World Model\MO4T\Model Information\Counters</summary>
        ReadOnly Property Counters2 As IReadOnlyList(Of IArmo_Counters2)
        Function AgregarCounters2() As IArmo_Counters2
        Function QuitarCounters2(indice As Integer) As Boolean
        Function ReordenarCounters2(permutacion As IList(Of Integer)) As Boolean
        Function QuitarCounters2(elemento As IArmo_Counters2) As Boolean
        ''' <summary>Female\World Model\MO4T\Model Information\Addon Nodes</summary>
        ReadOnly Property AddonNodes2 As IReadOnlyList(Of IArmo_AddonNodes2)
        Function AgregarAddonNodes2() As IArmo_AddonNodes2
        Function QuitarAddonNodes2(indice As Integer) As Boolean
        Function ReordenarAddonNodes2(permutacion As IList(Of Integer)) As Boolean
        Function QuitarAddonNodes2(elemento As IArmo_AddonNodes2) As Boolean
        ''' <summary>Destructible\Stages</summary>
        ReadOnly Property Stages As IReadOnlyList(Of IArmo_Stages)
        Function AgregarStages() As IArmo_Stages
        Function QuitarStages(indice As Integer) As Boolean
        Function ReordenarStages(permutacion As IList(Of Integer)) As Boolean
        Function QuitarStages(elemento As IArmo_Stages) As Boolean
        ''' <summary>Keywords\KWDA\Keywords</summary>
        ReadOnly Property Keywords As IReadOnlyList(Of IArmo_Keywords)
        Function AgregarKeywords() As IArmo_Keywords
        Function QuitarKeywords(indice As Integer) As Boolean
        Function ReordenarKeywords(permutacion As IList(Of Integer)) As Boolean
        Function QuitarKeywords(elemento As IArmo_Keywords) As Boolean
    End Interface

    ''' <summary>Un elemento de VMAD\Virtual Machine Adapter\Scripts, en lo que los dos juegos comparten.</summary>
    Public Interface IArmo_Scripts
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Script\ScriptName. Distinto de que el campo valga cero.</summary>
        Property ScriptNamePresente As Boolean
        ''' <summary>Script\ScriptName</summary>
        Property ScriptName As String
        ''' <summary>El record trae Script\Flags. Distinto de que el campo valga cero.</summary>
        Property ScriptFlagsPresente As Boolean
        ''' <summary>Script\Flags</summary>
        Property ScriptFlags As Byte
        ''' <summary>Nombre del valor de Script\Flags.</summary>
        ReadOnly Property ScriptFlagsNombre As String
    End Interface

    ''' <summary>Un elemento de Male\World Model\MO2T\Model Information\Textures, en lo que los dos juegos comparten.</summary>
    Public Interface IArmo_Textures
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Texture\File Hash. Distinto de que el campo valga cero.</summary>
        Property TextureFileHashPresente As Boolean
        ''' <summary>Texture\File Hash</summary>
        Property TextureFileHash As UInteger
        ''' <summary>El record trae Texture\Extension. Distinto de que el campo valga cero.</summary>
        Property TextureExtensionPresente As Boolean
        ''' <summary>Texture\Extension</summary>
        Property TextureExtension As String
        ''' <summary>El record trae Texture\Folder Hash. Distinto de que el campo valga cero.</summary>
        Property TextureFolderHashPresente As Boolean
        ''' <summary>Texture\Folder Hash</summary>
        Property TextureFolderHash As UInteger
    End Interface

    ''' <summary>Un elemento de Male\World Model\MO2T\Model Information\Counters, en lo que los dos juegos comparten.</summary>
    Public Interface IArmo_Counters
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Counter. Distinto de que el campo valga cero.</summary>
        Property CounterPresente As Boolean
        ''' <summary>Counter</summary>
        Property Counter As UInteger
    End Interface

    ''' <summary>Un elemento de Male\World Model\MO2T\Model Information\Addon Nodes, en lo que los dos juegos comparten.</summary>
    Public Interface IArmo_AddonNodes
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Addon Node. Distinto de que el campo valga cero.</summary>
        Property AddonNodePresente As Boolean
        ''' <summary>Addon Node</summary>
        Property AddonNode As UInteger
    End Interface

    ''' <summary>Un elemento de Female\World Model\MO4T\Model Information\Textures, en lo que los dos juegos comparten.</summary>
    Public Interface IArmo_Textures2
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Texture\File Hash. Distinto de que el campo valga cero.</summary>
        Property TextureFileHashPresente As Boolean
        ''' <summary>Texture\File Hash</summary>
        Property TextureFileHash As UInteger
        ''' <summary>El record trae Texture\Extension. Distinto de que el campo valga cero.</summary>
        Property TextureExtensionPresente As Boolean
        ''' <summary>Texture\Extension</summary>
        Property TextureExtension As String
        ''' <summary>El record trae Texture\Folder Hash. Distinto de que el campo valga cero.</summary>
        Property TextureFolderHashPresente As Boolean
        ''' <summary>Texture\Folder Hash</summary>
        Property TextureFolderHash As UInteger
    End Interface

    ''' <summary>Un elemento de Female\World Model\MO4T\Model Information\Counters, en lo que los dos juegos comparten.</summary>
    Public Interface IArmo_Counters2
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Counter. Distinto de que el campo valga cero.</summary>
        Property CounterPresente As Boolean
        ''' <summary>Counter</summary>
        Property Counter As UInteger
    End Interface

    ''' <summary>Un elemento de Female\World Model\MO4T\Model Information\Addon Nodes, en lo que los dos juegos comparten.</summary>
    Public Interface IArmo_AddonNodes2
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Addon Node. Distinto de que el campo valga cero.</summary>
        Property AddonNodePresente As Boolean
        ''' <summary>Addon Node</summary>
        Property AddonNode As UInteger
    End Interface

    ''' <summary>Un elemento de Destructible\Stages, en lo que los dos juegos comparten.</summary>
    Public Interface IArmo_Stages
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Stage\DSTD\Destruction Stage Data\Health %. Distinto de que el campo valga cero.</summary>
        Property DestructionStageDataHealthPresente As Boolean
        ''' <summary>Stage\DSTD\Destruction Stage Data\Health %</summary>
        Property DestructionStageDataHealth As Byte
        ''' <summary>El record trae Stage\DSTD\Destruction Stage Data\Index. Distinto de que el campo valga cero.</summary>
        Property DestructionStageDataIndexPresente As Boolean
        ''' <summary>Stage\DSTD\Destruction Stage Data\Index</summary>
        Property DestructionStageDataIndex As Byte
        ''' <summary>El record trae Stage\DSTD\Destruction Stage Data\Model Damage Stage. Distinto de que el campo valga cero.</summary>
        Property DestructionStageDataModelDamageStagePresente As Boolean
        ''' <summary>Stage\DSTD\Destruction Stage Data\Model Damage Stage</summary>
        Property DestructionStageDataModelDamageStage As Byte
        ''' <summary>El record trae Stage\DSTD\Destruction Stage Data\Flags. Distinto de que el campo valga cero.</summary>
        Property DestructionStageDataFlagsPresente As Boolean
        ''' <summary>Stage\DSTD\Destruction Stage Data\Flags</summary>
        Property DestructionStageDataFlags As Byte
        ''' <summary>Bit 0 de Stage\DSTD\Destruction Stage Data\Flags: Cap Damage</summary>
        Property DestructionStageDataFlagsCapDamage As Boolean
        ''' <summary>Bit 1 de Stage\DSTD\Destruction Stage Data\Flags: Disable</summary>
        Property DestructionStageDataFlagsDisable As Boolean
        ''' <summary>Bit 2 de Stage\DSTD\Destruction Stage Data\Flags: Destroy</summary>
        Property DestructionStageDataFlagsDestroy As Boolean
        ''' <summary>Bit 3 de Stage\DSTD\Destruction Stage Data\Flags: Ignore External Dmg</summary>
        Property DestructionStageDataFlagsIgnoreExternalDmg As Boolean
        ''' <summary>El record trae Stage\DSTD\Destruction Stage Data\Self Damage per Second. Distinto de que el campo valga cero.</summary>
        Property DestructionStageDataSelfDamagePerSecondPresente As Boolean
        ''' <summary>Stage\DSTD\Destruction Stage Data\Self Damage per Second</summary>
        Property DestructionStageDataSelfDamagePerSecond As Integer
        ''' <summary>El record trae Stage\DSTD\Destruction Stage Data\Explosion. Distinto de que el campo valga cero.</summary>
        Property DestructionStageDataExplosionPresente As Boolean
        ''' <summary>Stage\DSTD\Destruction Stage Data\Explosion  -&gt;  EXPL / NULL. Referencia en el espacio del orden de carga.</summary>
        Property DestructionStageDataExplosion As UInteger
        ''' <summary>El record trae Stage\DSTD\Destruction Stage Data\Debris. Distinto de que el campo valga cero.</summary>
        Property DestructionStageDataDebrisPresente As Boolean
        ''' <summary>Stage\DSTD\Destruction Stage Data\Debris  -&gt;  DEBR / NULL. Referencia en el espacio del orden de carga.</summary>
        Property DestructionStageDataDebris As UInteger
        ''' <summary>El record trae Stage\DSTD\Destruction Stage Data\Debris Count. Distinto de que el campo valga cero.</summary>
        Property DestructionStageDataDebrisCountPresente As Boolean
        ''' <summary>Stage\DSTD\Destruction Stage Data\Debris Count</summary>
        Property DestructionStageDataDebrisCount As Integer
        ''' <summary>El record trae Stage\Model\DMDL\Model FileName. Distinto de que el campo valga cero.</summary>
        Property StageModelFileNamePresente As Boolean
        ''' <summary>Stage\Model\DMDL\Model FileName</summary>
        Property StageModelFileName As String
        ''' <summary>El record trae Stage\Model\DMDT\Model Information\ERROR. Distinto de que el campo valga cero.</summary>
        Property ModelInformationERRORPresente As Boolean
        ''' <summary>Stage\Model\DMDT\Model Information\ERROR</summary>
        Property ModelInformationERROR As Byte()
        ''' <summary>El record trae el marcador Stage\DSTF\End Marker. No lleva valor: el dato es que esté, así que ponerlo en verdadero lo crea y en falso lo saca.</summary>
        Property StageEndMarker As Boolean
    End Interface

    ''' <summary>Un elemento de Keywords\KWDA\Keywords, en lo que los dos juegos comparten.</summary>
    Public Interface IArmo_Keywords
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Keyword. Distinto de que el campo valga cero.</summary>
        Property KeywordPresente As Boolean
        ''' <summary>Keyword  -&gt;  KYWD / NULL. Referencia en el espacio del orden de carga.</summary>
        Property Keyword As UInteger
    End Interface

    ''' <summary>Campos de un record BPTD que los dos juegos declaran igual.
    ''' <para>El codigo que solo usa estos no necesita saber de que juego se trata. El
    ''' que necesita algo propio de un juego tiene que nombrar la clase de ese juego, y
    ''' asi no puede leer sin querer un campo que alla significa otra cosa.</para></summary>
    Public Interface IBptd
        ReadOnly Property Node As WbNode
        ''' <summary>Identificador del record.</summary>
        ReadOnly Property FormID As UInteger
        ''' <summary>El record trae EDID\Editor ID. Distinto de que el campo valga cero.</summary>
        Property EditorIDPresente As Boolean
        ''' <summary>EDID\Editor ID</summary>
        Property EditorID As String
        ''' <summary>El record trae Model\MODL\Model FileName. Distinto de que el campo valga cero.</summary>
        Property ModelFileNamePresente As Boolean
        ''' <summary>Model\MODL\Model FileName</summary>
        Property ModelFileName As String
        ''' <summary>El record trae Model\MODT\Model Information\ERROR. Distinto de que el campo valga cero.</summary>
        Property ModelInformationERRORPresente As Boolean
        ''' <summary>Model\MODT\Model Information\ERROR</summary>
        Property ModelInformationERROR As Byte()
        ''' <summary>Model\MODT\Model Information\Textures</summary>
        ReadOnly Property Textures As IReadOnlyList(Of IBptd_Textures)
        Function AgregarTextures() As IBptd_Textures
        Function QuitarTextures(indice As Integer) As Boolean
        Function ReordenarTextures(permutacion As IList(Of Integer)) As Boolean
        Function QuitarTextures(elemento As IBptd_Textures) As Boolean
        ''' <summary>Model\MODT\Model Information\Counters</summary>
        ReadOnly Property Counters As IReadOnlyList(Of IBptd_Counters)
        Function AgregarCounters() As IBptd_Counters
        Function QuitarCounters(indice As Integer) As Boolean
        Function ReordenarCounters(permutacion As IList(Of Integer)) As Boolean
        Function QuitarCounters(elemento As IBptd_Counters) As Boolean
        ''' <summary>Model\MODT\Model Information\Addon Nodes</summary>
        ReadOnly Property AddonNodes As IReadOnlyList(Of IBptd_AddonNodes)
        Function AgregarAddonNodes() As IBptd_AddonNodes
        Function QuitarAddonNodes(indice As Integer) As Boolean
        Function ReordenarAddonNodes(permutacion As IList(Of Integer)) As Boolean
        Function QuitarAddonNodes(elemento As IBptd_AddonNodes) As Boolean
        ''' <summary>Body Parts</summary>
        ReadOnly Property BodyParts As IReadOnlyList(Of IBptd_BodyParts)
        Function AgregarBodyParts() As IBptd_BodyParts
        Function QuitarBodyParts(indice As Integer) As Boolean
        Function ReordenarBodyParts(permutacion As IList(Of Integer)) As Boolean
        Function QuitarBodyParts(elemento As IBptd_BodyParts) As Boolean
    End Interface

    ''' <summary>Un elemento de Model\MODT\Model Information\Textures, en lo que los dos juegos comparten.</summary>
    Public Interface IBptd_Textures
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Texture\File Hash. Distinto de que el campo valga cero.</summary>
        Property TextureFileHashPresente As Boolean
        ''' <summary>Texture\File Hash</summary>
        Property TextureFileHash As UInteger
        ''' <summary>El record trae Texture\Extension. Distinto de que el campo valga cero.</summary>
        Property TextureExtensionPresente As Boolean
        ''' <summary>Texture\Extension</summary>
        Property TextureExtension As String
        ''' <summary>El record trae Texture\Folder Hash. Distinto de que el campo valga cero.</summary>
        Property TextureFolderHashPresente As Boolean
        ''' <summary>Texture\Folder Hash</summary>
        Property TextureFolderHash As UInteger
    End Interface

    ''' <summary>Un elemento de Model\MODT\Model Information\Counters, en lo que los dos juegos comparten.</summary>
    Public Interface IBptd_Counters
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Counter. Distinto de que el campo valga cero.</summary>
        Property CounterPresente As Boolean
        ''' <summary>Counter</summary>
        Property Counter As UInteger
    End Interface

    ''' <summary>Un elemento de Model\MODT\Model Information\Addon Nodes, en lo que los dos juegos comparten.</summary>
    Public Interface IBptd_AddonNodes
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Addon Node. Distinto de que el campo valga cero.</summary>
        Property AddonNodePresente As Boolean
        ''' <summary>Addon Node</summary>
        Property AddonNode As UInteger
    End Interface

    ''' <summary>Un elemento de Body Parts, en lo que los dos juegos comparten.</summary>
    Public Interface IBptd_BodyParts
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Body Part\BPTN\Part Name. Distinto de que el campo valga cero.</summary>
        Property BodyPartPartNamePresente As Boolean
        ''' <summary>Body Part\BPTN\Part Name. Resuelto contra las tablas de texto si el archivo las usa.</summary>
        Property BodyPartPartName As String
        ''' <summary>El record trae Body Part\BPNN\Part Node. Distinto de que el campo valga cero.</summary>
        Property BodyPartPartNodePresente As Boolean
        ''' <summary>Body Part\BPNN\Part Node</summary>
        Property BodyPartPartNode As String
        ''' <summary>El record trae Body Part\BPNT\VATS Target. Distinto de que el campo valga cero.</summary>
        Property BodyPartVATSTargetPresente As Boolean
        ''' <summary>Body Part\BPNT\VATS Target</summary>
        Property BodyPartVATSTarget As String
        ''' <summary>El record trae Body Part\BPND\Node Data\Damage Mult. Distinto de que el campo valga cero.</summary>
        Property NodeDataDamageMultPresente As Boolean
        ''' <summary>Body Part\BPND\Node Data\Damage Mult</summary>
        Property NodeDataDamageMult As Single
        ''' <summary>El record trae Body Part\BPND\Node Data\Explodable - Debris. Distinto de que el campo valga cero.</summary>
        Property NodeDataExplodableDebrisPresente As Boolean
        ''' <summary>Body Part\BPND\Node Data\Explodable - Debris  -&gt;  DEBR / NULL. Referencia en el espacio del orden de carga.</summary>
        Property NodeDataExplodableDebris As UInteger
        ''' <summary>El record trae Body Part\BPND\Node Data\Explodable - Explosion. Distinto de que el campo valga cero.</summary>
        Property NodeDataExplodableExplosionPresente As Boolean
        ''' <summary>Body Part\BPND\Node Data\Explodable - Explosion  -&gt;  EXPL / NULL. Referencia en el espacio del orden de carga.</summary>
        Property NodeDataExplodableExplosion As UInteger
        ''' <summary>El record trae Body Part\BPND\Node Data\Explodable - Debris Scale. Distinto de que el campo valga cero.</summary>
        Property NodeDataExplodableDebrisScalePresente As Boolean
        ''' <summary>Body Part\BPND\Node Data\Explodable - Debris Scale</summary>
        Property NodeDataExplodableDebrisScale As Single
        ''' <summary>El record trae Body Part\BPND\Node Data\Severable - Debris. Distinto de que el campo valga cero.</summary>
        Property NodeDataSeverableDebrisPresente As Boolean
        ''' <summary>Body Part\BPND\Node Data\Severable - Debris  -&gt;  DEBR / NULL. Referencia en el espacio del orden de carga.</summary>
        Property NodeDataSeverableDebris As UInteger
        ''' <summary>El record trae Body Part\BPND\Node Data\Severable - Explosion. Distinto de que el campo valga cero.</summary>
        Property NodeDataSeverableExplosionPresente As Boolean
        ''' <summary>Body Part\BPND\Node Data\Severable - Explosion  -&gt;  EXPL / NULL. Referencia en el espacio del orden de carga.</summary>
        Property NodeDataSeverableExplosion As UInteger
        ''' <summary>El record trae Body Part\BPND\Node Data\Severable - Debris Scale. Distinto de que el campo valga cero.</summary>
        Property NodeDataSeverableDebrisScalePresente As Boolean
        ''' <summary>Body Part\BPND\Node Data\Severable - Debris Scale</summary>
        Property NodeDataSeverableDebrisScale As Single
        ''' <summary>El record trae Body Part\BPND\Node Data\Severable - Impact DataSet. Distinto de que el campo valga cero.</summary>
        Property NodeDataSeverableImpactDataSetPresente As Boolean
        ''' <summary>Body Part\BPND\Node Data\Severable - Impact DataSet  -&gt;  IPDS / NULL. Referencia en el espacio del orden de carga.</summary>
        Property NodeDataSeverableImpactDataSet As UInteger
        ''' <summary>El record trae Body Part\BPND\Node Data\Explodable - Impact DataSet. Distinto de que el campo valga cero.</summary>
        Property NodeDataExplodableImpactDataSetPresente As Boolean
        ''' <summary>Body Part\BPND\Node Data\Explodable - Impact DataSet  -&gt;  IPDS / NULL. Referencia en el espacio del orden de carga.</summary>
        Property NodeDataExplodableImpactDataSet As UInteger
        ''' <summary>El record trae Body Part\BPND\Node Data\Flags. Distinto de que el campo valga cero.</summary>
        Property NodeDataFlagsPresente As Boolean
        ''' <summary>Body Part\BPND\Node Data\Flags</summary>
        Property NodeDataFlags As Byte
        ''' <summary>Bit 0 de Body Part\BPND\Node Data\Flags: Severable</summary>
        Property NodeDataFlagsSeverable As Boolean
        ''' <summary>Bit 3 de Body Part\BPND\Node Data\Flags: Explodable</summary>
        Property NodeDataFlagsExplodable As Boolean
        ''' <summary>El record trae Body Part\BPND\Node Data\Part Type. Distinto de que el campo valga cero.</summary>
        Property NodeDataPartTypePresente As Boolean
        ''' <summary>Body Part\BPND\Node Data\Part Type</summary>
        Property NodeDataPartType As Byte
        ''' <summary>Nombre del valor de Body Part\BPND\Node Data\Part Type.</summary>
        ReadOnly Property NodeDataPartTypeNombre As String
        ''' <summary>El record trae Body Part\BPND\Node Data\Health Percent. Distinto de que el campo valga cero.</summary>
        Property NodeDataHealthPercentPresente As Boolean
        ''' <summary>Body Part\BPND\Node Data\Health Percent</summary>
        Property NodeDataHealthPercent As Byte
        ''' <summary>El record trae Body Part\BPND\Node Data\Actor Value. Distinto de que el campo valga cero.</summary>
        Property NodeDataActorValuePresente As Boolean
        ''' <summary>El record trae Body Part\BPND\Node Data\To Hit Chance. Distinto de que el campo valga cero.</summary>
        Property NodeDataToHitChancePresente As Boolean
        ''' <summary>Body Part\BPND\Node Data\To Hit Chance</summary>
        Property NodeDataToHitChance As Byte
        ''' <summary>El record trae Body Part\BPND\Node Data\Explodable - Explosion Chance %. Distinto de que el campo valga cero.</summary>
        Property NodeDataExplodableExplosionChancePresente As Boolean
        ''' <summary>Body Part\BPND\Node Data\Explodable - Explosion Chance %</summary>
        Property NodeDataExplodableExplosionChance As Byte
        ''' <summary>El record trae Body Part\BPND\Node Data\Severable - Debris Count. Distinto de que el campo valga cero.</summary>
        Property NodeDataSeverableDebrisCountPresente As Boolean
        ''' <summary>El record trae Body Part\BPND\Node Data\Explodable - Debris Count. Distinto de que el campo valga cero.</summary>
        Property NodeDataExplodableDebrisCountPresente As Boolean
        ''' <summary>El record trae Body Part\BPND\Node Data\Severable - Decal Count. Distinto de que el campo valga cero.</summary>
        Property NodeDataSeverableDecalCountPresente As Boolean
        ''' <summary>Body Part\BPND\Node Data\Severable - Decal Count</summary>
        Property NodeDataSeverableDecalCount As Byte
        ''' <summary>El record trae Body Part\BPND\Node Data\Explodable - Decal Count. Distinto de que el campo valga cero.</summary>
        Property NodeDataExplodableDecalCountPresente As Boolean
        ''' <summary>Body Part\BPND\Node Data\Explodable - Decal Count</summary>
        Property NodeDataExplodableDecalCount As Byte
        ''' <summary>El record trae Body Part\NAM1\Limb Replacement Model. Distinto de que el campo valga cero.</summary>
        Property BodyPartLimbReplacementModelPresente As Boolean
        ''' <summary>Body Part\NAM1\Limb Replacement Model</summary>
        Property BodyPartLimbReplacementModel As String
        ''' <summary>El record trae Body Part\NAM4\Gore Effects - Target Bone. Distinto de que el campo valga cero.</summary>
        Property BodyPartGoreEffectsTargetBonePresente As Boolean
        ''' <summary>Body Part\NAM4\Gore Effects - Target Bone</summary>
        Property BodyPartGoreEffectsTargetBone As String
        ''' <summary>El record trae Body Part\NAM5\Model Information\ERROR. Distinto de que el campo valga cero.</summary>
        Property ModelInformationERRORPresente As Boolean
        ''' <summary>Body Part\NAM5\Model Information\ERROR</summary>
        Property ModelInformationERROR As Byte()
    End Interface

    ''' <summary>Campos de un record CLFM que los dos juegos declaran igual.
    ''' <para>El codigo que solo usa estos no necesita saber de que juego se trata. El
    ''' que necesita algo propio de un juego tiene que nombrar la clase de ese juego, y
    ''' asi no puede leer sin querer un campo que alla significa otra cosa.</para></summary>
    Public Interface IClfm
        ReadOnly Property Node As WbNode
        ''' <summary>Identificador del record.</summary>
        ReadOnly Property FormID As UInteger
        ''' <summary>Bit 2 de las banderas de la cabecera del record: Non-Playable.</summary>
        Property NonPlayable As Boolean
        ''' <summary>El record trae EDID\Editor ID. Distinto de que el campo valga cero.</summary>
        Property EditorIDPresente As Boolean
        ''' <summary>EDID\Editor ID</summary>
        Property EditorID As String
        ''' <summary>El record trae FULL\Name. Distinto de que el campo valga cero.</summary>
        Property NamePresente As Boolean
        ''' <summary>FULL\Name. Resuelto contra las tablas de texto si el archivo las usa.</summary>
        Property Name As String
    End Interface

    ''' <summary>Campos de un record DFOB que los dos juegos declaran igual.
    ''' <para>El codigo que solo usa estos no necesita saber de que juego se trata. El
    ''' que necesita algo propio de un juego tiene que nombrar la clase de ese juego, y
    ''' asi no puede leer sin querer un campo que alla significa otra cosa.</para></summary>
    Public Interface IDfob
        ReadOnly Property Node As WbNode
        ''' <summary>Identificador del record.</summary>
        ReadOnly Property FormID As UInteger
        ''' <summary>El record trae EDID\Editor ID. Distinto de que el campo valga cero.</summary>
        Property EditorIDPresente As Boolean
        ''' <summary>EDID\Editor ID</summary>
        Property EditorID As String
        ''' <summary>El record trae DATA\Object. Distinto de que el campo valga cero.</summary>
        Property ObjectPresente As Boolean
        ''' <summary>DATA\Object. Referencia en el espacio del orden de carga.</summary>
        Property [Object] As UInteger
    End Interface

    ''' <summary>Campos de un record FLST que los dos juegos declaran igual.
    ''' <para>El codigo que solo usa estos no necesita saber de que juego se trata. El
    ''' que necesita algo propio de un juego tiene que nombrar la clase de ese juego, y
    ''' asi no puede leer sin querer un campo que alla significa otra cosa.</para></summary>
    Public Interface IFlst
        ReadOnly Property Node As WbNode
        ''' <summary>Identificador del record.</summary>
        ReadOnly Property FormID As UInteger
        ''' <summary>El record trae EDID\Editor ID. Distinto de que el campo valga cero.</summary>
        Property EditorIDPresente As Boolean
        ''' <summary>EDID\Editor ID</summary>
        Property EditorID As String
        ''' <summary>FormIDs</summary>
        ReadOnly Property FormIDs As IReadOnlyList(Of IFlst_FormIDs)
        Function AgregarFormIDs() As IFlst_FormIDs
        Function QuitarFormIDs(indice As Integer) As Boolean
        Function ReordenarFormIDs(permutacion As IList(Of Integer)) As Boolean
        Function QuitarFormIDs(elemento As IFlst_FormIDs) As Boolean
    End Interface

    ''' <summary>Un elemento de FormIDs, en lo que los dos juegos comparten.</summary>
    Public Interface IFlst_FormIDs
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae LNAM\FormID. Distinto de que el campo valga cero.</summary>
        Property FormIDPresente As Boolean
        ''' <summary>LNAM\FormID. Referencia en el espacio del orden de carga.</summary>
        Property FormID As UInteger
    End Interface

    ''' <summary>Campos de un record HDPT que los dos juegos declaran igual.
    ''' <para>El codigo que solo usa estos no necesita saber de que juego se trata. El
    ''' que necesita algo propio de un juego tiene que nombrar la clase de ese juego, y
    ''' asi no puede leer sin querer un campo que alla significa otra cosa.</para></summary>
    Public Interface IHdpt
        ReadOnly Property Node As WbNode
        ''' <summary>Identificador del record.</summary>
        ReadOnly Property FormID As UInteger
        ''' <summary>Bit 2 de las banderas de la cabecera del record: Non-Playable.</summary>
        Property NonPlayable As Boolean
        ''' <summary>El record trae EDID\Editor ID. Distinto de que el campo valga cero.</summary>
        Property EditorIDPresente As Boolean
        ''' <summary>EDID\Editor ID</summary>
        Property EditorID As String
        ''' <summary>El record trae FULL\Name. Distinto de que el campo valga cero.</summary>
        Property NamePresente As Boolean
        ''' <summary>FULL\Name. Resuelto contra las tablas de texto si el archivo las usa.</summary>
        Property Name As String
        ''' <summary>El record trae Model\MODL\Model FileName. Distinto de que el campo valga cero.</summary>
        Property ModelFileNamePresente As Boolean
        ''' <summary>Model\MODL\Model FileName</summary>
        Property ModelFileName As String
        ''' <summary>El record trae Model\MODT\Model Information\ERROR. Distinto de que el campo valga cero.</summary>
        Property ModelInformationERRORPresente As Boolean
        ''' <summary>Model\MODT\Model Information\ERROR</summary>
        Property ModelInformationERROR As Byte()
        ''' <summary>El record trae DATA\Flags. Distinto de que el campo valga cero.</summary>
        Property FlagsPresente As Boolean
        ''' <summary>DATA\Flags</summary>
        Property Flags As Byte
        ''' <summary>Bit 0 de DATA\Flags: Playable</summary>
        Property FlagsPlayable As Boolean
        ''' <summary>Bit 1 de DATA\Flags: Male</summary>
        Property FlagsMale As Boolean
        ''' <summary>Bit 2 de DATA\Flags: Female</summary>
        Property FlagsFemale As Boolean
        ''' <summary>Bit 3 de DATA\Flags: Is Extra Part</summary>
        Property FlagsIsExtraPart As Boolean
        ''' <summary>Bit 4 de DATA\Flags: Use Solid Tint</summary>
        Property FlagsUseSolidTint As Boolean
        ''' <summary>El record trae PNAM\Type. Distinto de que el campo valga cero.</summary>
        Property TypePresente As Boolean
        ''' <summary>PNAM\Type</summary>
        Property Type As UInteger
        ''' <summary>Nombre del valor de PNAM\Type.</summary>
        ReadOnly Property TypeNombre As String
        ''' <summary>El record trae TNAM\Texture Set. Distinto de que el campo valga cero.</summary>
        Property TextureSetPresente As Boolean
        ''' <summary>TNAM\Texture Set  -&gt;  TXST. Referencia en el espacio del orden de carga.</summary>
        Property TextureSet As UInteger
        ''' <summary>El record trae CNAM\Color. Distinto de que el campo valga cero.</summary>
        Property ColorPresente As Boolean
        ''' <summary>CNAM\Color  -&gt;  CLFM. Referencia en el espacio del orden de carga.</summary>
        Property Color As UInteger
        ''' <summary>El record trae RNAM\Valid Races. Distinto de que el campo valga cero.</summary>
        Property ValidRacesPresente As Boolean
        ''' <summary>RNAM\Valid Races  -&gt;  FLST. Referencia en el espacio del orden de carga.</summary>
        Property ValidRaces As UInteger
        ''' <summary>Model\MODT\Model Information\Textures</summary>
        ReadOnly Property Textures As IReadOnlyList(Of IHdpt_Textures)
        Function AgregarTextures() As IHdpt_Textures
        Function QuitarTextures(indice As Integer) As Boolean
        Function ReordenarTextures(permutacion As IList(Of Integer)) As Boolean
        Function QuitarTextures(elemento As IHdpt_Textures) As Boolean
        ''' <summary>Model\MODT\Model Information\Counters</summary>
        ReadOnly Property Counters As IReadOnlyList(Of IHdpt_Counters)
        Function AgregarCounters() As IHdpt_Counters
        Function QuitarCounters(indice As Integer) As Boolean
        Function ReordenarCounters(permutacion As IList(Of Integer)) As Boolean
        Function QuitarCounters(elemento As IHdpt_Counters) As Boolean
        ''' <summary>Model\MODT\Model Information\Addon Nodes</summary>
        ReadOnly Property AddonNodes As IReadOnlyList(Of IHdpt_AddonNodes)
        Function AgregarAddonNodes() As IHdpt_AddonNodes
        Function QuitarAddonNodes(indice As Integer) As Boolean
        Function ReordenarAddonNodes(permutacion As IList(Of Integer)) As Boolean
        Function QuitarAddonNodes(elemento As IHdpt_AddonNodes) As Boolean
        ''' <summary>Extra Parts</summary>
        ReadOnly Property ExtraParts As IReadOnlyList(Of IHdpt_ExtraParts)
        Function AgregarExtraParts() As IHdpt_ExtraParts
        Function QuitarExtraParts(indice As Integer) As Boolean
        Function ReordenarExtraParts(permutacion As IList(Of Integer)) As Boolean
        Function QuitarExtraParts(elemento As IHdpt_ExtraParts) As Boolean
        ''' <summary>Parts</summary>
        ReadOnly Property Parts As IReadOnlyList(Of IHdpt_Parts)
        Function AgregarParts() As IHdpt_Parts
        Function QuitarParts(indice As Integer) As Boolean
        Function ReordenarParts(permutacion As IList(Of Integer)) As Boolean
        Function QuitarParts(elemento As IHdpt_Parts) As Boolean
    End Interface

    ''' <summary>Un elemento de Model\MODT\Model Information\Textures, en lo que los dos juegos comparten.</summary>
    Public Interface IHdpt_Textures
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Texture\File Hash. Distinto de que el campo valga cero.</summary>
        Property TextureFileHashPresente As Boolean
        ''' <summary>Texture\File Hash</summary>
        Property TextureFileHash As UInteger
        ''' <summary>El record trae Texture\Extension. Distinto de que el campo valga cero.</summary>
        Property TextureExtensionPresente As Boolean
        ''' <summary>Texture\Extension</summary>
        Property TextureExtension As String
        ''' <summary>El record trae Texture\Folder Hash. Distinto de que el campo valga cero.</summary>
        Property TextureFolderHashPresente As Boolean
        ''' <summary>Texture\Folder Hash</summary>
        Property TextureFolderHash As UInteger
    End Interface

    ''' <summary>Un elemento de Model\MODT\Model Information\Counters, en lo que los dos juegos comparten.</summary>
    Public Interface IHdpt_Counters
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Counter. Distinto de que el campo valga cero.</summary>
        Property CounterPresente As Boolean
        ''' <summary>Counter</summary>
        Property Counter As UInteger
    End Interface

    ''' <summary>Un elemento de Model\MODT\Model Information\Addon Nodes, en lo que los dos juegos comparten.</summary>
    Public Interface IHdpt_AddonNodes
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Addon Node. Distinto de que el campo valga cero.</summary>
        Property AddonNodePresente As Boolean
        ''' <summary>Addon Node</summary>
        Property AddonNode As UInteger
    End Interface

    ''' <summary>Un elemento de Extra Parts, en lo que los dos juegos comparten.</summary>
    Public Interface IHdpt_ExtraParts
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae HNAM\Part. Distinto de que el campo valga cero.</summary>
        Property PartPresente As Boolean
        ''' <summary>HNAM\Part  -&gt;  HDPT. Referencia en el espacio del orden de carga.</summary>
        Property Part As UInteger
    End Interface

    ''' <summary>Un elemento de Parts, en lo que los dos juegos comparten.</summary>
    Public Interface IHdpt_Parts
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Part\NAM0\Part Type. Distinto de que el campo valga cero.</summary>
        Property PartTypePresente As Boolean
        ''' <summary>Part\NAM0\Part Type</summary>
        Property PartType As UInteger
        ''' <summary>Nombre del valor de Part\NAM0\Part Type.</summary>
        ReadOnly Property PartTypeNombre As String
        ''' <summary>El record trae Part\NAM1\FileName. Distinto de que el campo valga cero.</summary>
        Property PartFileNamePresente As Boolean
        ''' <summary>Part\NAM1\FileName</summary>
        Property PartFileName As String
    End Interface

    ''' <summary>Campos de un record IDLE que los dos juegos declaran igual.
    ''' <para>El codigo que solo usa estos no necesita saber de que juego se trata. El
    ''' que necesita algo propio de un juego tiene que nombrar la clase de ese juego, y
    ''' asi no puede leer sin querer un campo que alla significa otra cosa.</para></summary>
    Public Interface IIdle
        ReadOnly Property Node As WbNode
        ''' <summary>Identificador del record.</summary>
        ReadOnly Property FormID As UInteger
        ''' <summary>El record trae EDID\Editor ID. Distinto de que el campo valga cero.</summary>
        Property EditorIDPresente As Boolean
        ''' <summary>EDID\Editor ID</summary>
        Property EditorID As String
        ''' <summary>El record trae ENAM\Animation Event. Distinto de que el campo valga cero.</summary>
        Property AnimationEventPresente As Boolean
        ''' <summary>ENAM\Animation Event</summary>
        Property AnimationEvent As String
        ''' <summary>El record trae ANAM\Animations\Parent. Distinto de que el campo valga cero.</summary>
        Property AnimationsParentPresente As Boolean
        ''' <summary>ANAM\Animations\Parent  -&gt;  AACT / IDLE / NULL. Referencia en el espacio del orden de carga.</summary>
        Property AnimationsParent As UInteger
        ''' <summary>El record trae ANAM\Animations\Previous. Distinto de que el campo valga cero.</summary>
        Property AnimationsPreviousPresente As Boolean
        ''' <summary>ANAM\Animations\Previous  -&gt;  AACT / IDLE / NULL. Referencia en el espacio del orden de carga.</summary>
        Property AnimationsPrevious As UInteger
        ''' <summary>El record trae DATA\Looping seconds (both 255 forever)\Min. Distinto de que el campo valga cero.</summary>
        Property LoopingSecondsBoth255ForeverMinPresente As Boolean
        ''' <summary>DATA\Looping seconds (both 255 forever)\Min</summary>
        Property LoopingSecondsBoth255ForeverMin As Byte
        ''' <summary>El record trae DATA\Looping seconds (both 255 forever)\Max. Distinto de que el campo valga cero.</summary>
        Property LoopingSecondsBoth255ForeverMaxPresente As Boolean
        ''' <summary>DATA\Looping seconds (both 255 forever)\Max</summary>
        Property LoopingSecondsBoth255ForeverMax As Byte
        ''' <summary>Conditions</summary>
        ReadOnly Property Conditions As IReadOnlyList(Of IIdle_Conditions)
        Function AgregarConditions() As IIdle_Conditions
        Function QuitarConditions(indice As Integer) As Boolean
        Function ReordenarConditions(permutacion As IList(Of Integer)) As Boolean
        Function QuitarConditions(elemento As IIdle_Conditions) As Boolean
    End Interface

    ''' <summary>Un elemento de Conditions, en lo que los dos juegos comparten.</summary>
    Public Interface IIdle_Conditions
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Condition\CTDA\Type. Distinto de que el campo valga cero.</summary>
        Property ConditionTypePresente As Boolean
        ''' <summary>Condition\CTDA\Type</summary>
        Property ConditionType As Byte
        ''' <summary>El record trae Condition\CTDA\Comparison Value\Comparison Value - Float. Distinto de que el campo valga cero.</summary>
        Property ConditionComparisonValueFloatPresente As Boolean
        ''' <summary>Condition\CTDA\Comparison Value\Comparison Value - Float</summary>
        Property ConditionComparisonValueFloat As Single
        ''' <summary>El record trae Condition\CTDA\Comparison Value\Comparison Value - Global. Distinto de que el campo valga cero.</summary>
        Property ConditionComparisonValueGlobalPresente As Boolean
        ''' <summary>Condition\CTDA\Comparison Value\Comparison Value - Global  -&gt;  GLOB. Referencia en el espacio del orden de carga.</summary>
        Property ConditionComparisonValueGlobal As UInteger
        ''' <summary>El record trae Condition\CTDA\Function. Distinto de que el campo valga cero.</summary>
        Property ConditionFunctionPresente As Boolean
        ''' <summary>Condition\CTDA\Function</summary>
        Property ConditionFunction As UShort
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Unknown. Distinto de que el campo valga cero.</summary>
        Property Parameter1UnknownPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Unknown</summary>
        Property Parameter1Unknown As Byte()
        ''' <summary>El record trae Condition\CTDA\Parameter #1\None. Distinto de que el campo valga cero.</summary>
        Property Parameter1NonePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\None</summary>
        Property Parameter1None As Byte()
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Float. Distinto de que el campo valga cero.</summary>
        Property Parameter1FloatPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Float</summary>
        Property Parameter1Float As Single
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Integer. Distinto de que el campo valga cero.</summary>
        Property Parameter1IntegerPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Integer</summary>
        Property Parameter1Integer As Integer
        ''' <summary>El record trae Condition\CTDA\Parameter #1\String. Distinto de que el campo valga cero.</summary>
        Property Parameter1StringPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\String</summary>
        Property Parameter1String As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Alias. Distinto de que el campo valga cero.</summary>
        Property Parameter1AliasPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Alias</summary>
        Property Parameter1Alias As Integer
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Event. Distinto de que el campo valga cero.</summary>
        Property Parameter1EventPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Event</summary>
        Property Parameter1Event As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Packdata ID. Distinto de que el campo valga cero.</summary>
        Property Parameter1PackdataIDPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Packdata ID</summary>
        Property Parameter1PackdataID As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Quest Stage. Distinto de que el campo valga cero.</summary>
        Property Parameter1QuestStagePresente As Boolean
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Alignment. Distinto de que el campo valga cero.</summary>
        Property Parameter1AlignmentPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Alignment</summary>
        Property Parameter1Alignment As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #1\Alignment.</summary>
        ReadOnly Property Parameter1AlignmentNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Axis. Distinto de que el campo valga cero.</summary>
        Property Parameter1AxisPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Axis</summary>
        Property Parameter1Axis As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #1\Axis.</summary>
        ReadOnly Property Parameter1AxisNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Crime Type. Distinto de que el campo valga cero.</summary>
        Property Parameter1CrimeTypePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Crime Type</summary>
        Property Parameter1CrimeType As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #1\Crime Type.</summary>
        ReadOnly Property Parameter1CrimeTypeNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Critical Stage. Distinto de que el campo valga cero.</summary>
        Property Parameter1CriticalStagePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Critical Stage</summary>
        Property Parameter1CriticalStage As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #1\Critical Stage.</summary>
        ReadOnly Property Parameter1CriticalStageNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Form Type. Distinto de que el campo valga cero.</summary>
        Property Parameter1FormTypePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Form Type</summary>
        Property Parameter1FormType As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #1\Form Type.</summary>
        ReadOnly Property Parameter1FormTypeNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Misc Stat. Distinto de que el campo valga cero.</summary>
        Property Parameter1MiscStatPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Misc Stat</summary>
        Property Parameter1MiscStat As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #1\Misc Stat.</summary>
        ReadOnly Property Parameter1MiscStatNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Sex. Distinto de que el campo valga cero.</summary>
        Property Parameter1SexPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Sex</summary>
        Property Parameter1Sex As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #1\Sex.</summary>
        ReadOnly Property Parameter1SexNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Ward State. Distinto de que el campo valga cero.</summary>
        Property Parameter1WardStatePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Ward State</summary>
        Property Parameter1WardState As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #1\Ward State.</summary>
        ReadOnly Property Parameter1WardStateNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Actor. Distinto de que el campo valga cero.</summary>
        Property Parameter1ActorPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Actor  -&gt;  ACHR / PLYR / REFR / TRGT. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Actor As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Actor Base. Distinto de que el campo valga cero.</summary>
        Property Parameter1ActorBasePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Actor Base  -&gt;  NPC_. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1ActorBase As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Actor Value. Distinto de que el campo valga cero.</summary>
        Property Parameter1ActorValuePresente As Boolean
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Association Type. Distinto de que el campo valga cero.</summary>
        Property Parameter1AssociationTypePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Association Type  -&gt;  ASTP. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1AssociationType As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Base Object. Distinto de que el campo valga cero.</summary>
        Property Parameter1BaseObjectPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Base Object. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1BaseObject As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Cell. Distinto de que el campo valga cero.</summary>
        Property Parameter1CellPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Cell  -&gt;  CELL. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Cell As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Class. Distinto de que el campo valga cero.</summary>
        Property Parameter1ClassPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Class  -&gt;  CLAS. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Class As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Effect Item. Distinto de que el campo valga cero.</summary>
        Property Parameter1EffectItemPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Effect Item  -&gt;  ALCH / ENCH / INGR / SCRL / SPEL. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1EffectItem As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Encounter Zone. Distinto de que el campo valga cero.</summary>
        Property Parameter1EncounterZonePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Encounter Zone  -&gt;  ECZN. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1EncounterZone As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Equip Type. Distinto de que el campo valga cero.</summary>
        Property Parameter1EquipTypePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Equip Type  -&gt;  EQUP. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1EquipType As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Event Data. Distinto de que el campo valga cero.</summary>
        Property Parameter1EventDataPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Event Data  -&gt;  FLST / KYWD / LCTN. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1EventData As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Faction. Distinto de que el campo valga cero.</summary>
        Property Parameter1FactionPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Faction  -&gt;  FACT. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Faction As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Form List. Distinto de que el campo valga cero.</summary>
        Property Parameter1FormListPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Form List  -&gt;  FLST. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1FormList As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Furniture. Distinto de que el campo valga cero.</summary>
        Property Parameter1FurniturePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Furniture  -&gt;  FLST / FURN. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Furniture As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Global. Distinto de que el campo valga cero.</summary>
        Property Parameter1GlobalPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Global  -&gt;  GLOB. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Global As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Idle. Distinto de que el campo valga cero.</summary>
        Property Parameter1IdlePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Idle  -&gt;  IDLE. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Idle As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Keyword. Distinto de que el campo valga cero.</summary>
        Property Parameter1KeywordPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Keyword  -&gt;  FLST / KYWD / NULL. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Keyword As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Location. Distinto de que el campo valga cero.</summary>
        Property Parameter1LocationPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Location  -&gt;  LCTN. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Location As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Location Ref Type. Distinto de que el campo valga cero.</summary>
        Property Parameter1LocationRefTypePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Location Ref Type  -&gt;  LCRT. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1LocationRefType As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Owner. Distinto de que el campo valga cero.</summary>
        Property Parameter1OwnerPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Owner  -&gt;  FACT / NPC_. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Owner As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Package. Distinto de que el campo valga cero.</summary>
        Property Parameter1PackagePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Package  -&gt;  PACK. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Package As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Perk. Distinto de que el campo valga cero.</summary>
        Property Parameter1PerkPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Perk  -&gt;  PERK. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Perk As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Quest. Distinto de que el campo valga cero.</summary>
        Property Parameter1QuestPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Quest  -&gt;  QUST. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Quest As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Race. Distinto de que el campo valga cero.</summary>
        Property Parameter1RacePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Race  -&gt;  RACE. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Race As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Reference. Distinto de que el campo valga cero.</summary>
        Property Parameter1ReferencePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Reference  -&gt;  ACHR / PARW / PBAR / PBEA / PCON / PFLA / PGRE / PHZD / PLYR / PMIS / REFR / TRGT. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Reference As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Region. Distinto de que el campo valga cero.</summary>
        Property Parameter1RegionPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Region  -&gt;  REGN. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Region As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Scene. Distinto de que el campo valga cero.</summary>
        Property Parameter1ScenePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Scene  -&gt;  SCEN. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Scene As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Voice Type. Distinto de que el campo valga cero.</summary>
        Property Parameter1VoiceTypePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Voice Type  -&gt;  VTYP / FLST. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1VoiceType As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Weather. Distinto de que el campo valga cero.</summary>
        Property Parameter1WeatherPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Weather  -&gt;  WTHR. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Weather As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Worldspace. Distinto de que el campo valga cero.</summary>
        Property Parameter1WorldspacePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Worldspace  -&gt;  WRLD / FLST. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Worldspace As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Unknown. Distinto de que el campo valga cero.</summary>
        Property Parameter2UnknownPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Unknown</summary>
        Property Parameter2Unknown As Byte()
        ''' <summary>El record trae Condition\CTDA\Parameter #2\None. Distinto de que el campo valga cero.</summary>
        Property Parameter2NonePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\None</summary>
        Property Parameter2None As Byte()
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Float. Distinto de que el campo valga cero.</summary>
        Property Parameter2FloatPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Float</summary>
        Property Parameter2Float As Single
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Integer. Distinto de que el campo valga cero.</summary>
        Property Parameter2IntegerPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Integer</summary>
        Property Parameter2Integer As Integer
        ''' <summary>El record trae Condition\CTDA\Parameter #2\String. Distinto de que el campo valga cero.</summary>
        Property Parameter2StringPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\String</summary>
        Property Parameter2String As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Alias. Distinto de que el campo valga cero.</summary>
        Property Parameter2AliasPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Alias</summary>
        Property Parameter2Alias As Integer
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Event. Distinto de que el campo valga cero.</summary>
        Property Parameter2EventPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Event</summary>
        Property Parameter2Event As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Packdata ID. Distinto de que el campo valga cero.</summary>
        Property Parameter2PackdataIDPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Packdata ID</summary>
        Property Parameter2PackdataID As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Quest Stage. Distinto de que el campo valga cero.</summary>
        Property Parameter2QuestStagePresente As Boolean
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Alignment. Distinto de que el campo valga cero.</summary>
        Property Parameter2AlignmentPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Alignment</summary>
        Property Parameter2Alignment As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #2\Alignment.</summary>
        ReadOnly Property Parameter2AlignmentNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Axis. Distinto de que el campo valga cero.</summary>
        Property Parameter2AxisPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Axis</summary>
        Property Parameter2Axis As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #2\Axis.</summary>
        ReadOnly Property Parameter2AxisNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Crime Type. Distinto de que el campo valga cero.</summary>
        Property Parameter2CrimeTypePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Crime Type</summary>
        Property Parameter2CrimeType As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #2\Crime Type.</summary>
        ReadOnly Property Parameter2CrimeTypeNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Critical Stage. Distinto de que el campo valga cero.</summary>
        Property Parameter2CriticalStagePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Critical Stage</summary>
        Property Parameter2CriticalStage As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #2\Critical Stage.</summary>
        ReadOnly Property Parameter2CriticalStageNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Form Type. Distinto de que el campo valga cero.</summary>
        Property Parameter2FormTypePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Form Type</summary>
        Property Parameter2FormType As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #2\Form Type.</summary>
        ReadOnly Property Parameter2FormTypeNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Misc Stat. Distinto de que el campo valga cero.</summary>
        Property Parameter2MiscStatPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Misc Stat</summary>
        Property Parameter2MiscStat As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #2\Misc Stat.</summary>
        ReadOnly Property Parameter2MiscStatNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Sex. Distinto de que el campo valga cero.</summary>
        Property Parameter2SexPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Sex</summary>
        Property Parameter2Sex As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #2\Sex.</summary>
        ReadOnly Property Parameter2SexNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Ward State. Distinto de que el campo valga cero.</summary>
        Property Parameter2WardStatePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Ward State</summary>
        Property Parameter2WardState As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #2\Ward State.</summary>
        ReadOnly Property Parameter2WardStateNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Actor. Distinto de que el campo valga cero.</summary>
        Property Parameter2ActorPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Actor  -&gt;  ACHR / PLYR / REFR / TRGT. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Actor As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Actor Base. Distinto de que el campo valga cero.</summary>
        Property Parameter2ActorBasePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Actor Base  -&gt;  NPC_. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2ActorBase As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Actor Value. Distinto de que el campo valga cero.</summary>
        Property Parameter2ActorValuePresente As Boolean
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Association Type. Distinto de que el campo valga cero.</summary>
        Property Parameter2AssociationTypePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Association Type  -&gt;  ASTP. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2AssociationType As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Base Object. Distinto de que el campo valga cero.</summary>
        Property Parameter2BaseObjectPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Base Object. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2BaseObject As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Cell. Distinto de que el campo valga cero.</summary>
        Property Parameter2CellPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Cell  -&gt;  CELL. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Cell As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Class. Distinto de que el campo valga cero.</summary>
        Property Parameter2ClassPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Class  -&gt;  CLAS. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Class As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Effect Item. Distinto de que el campo valga cero.</summary>
        Property Parameter2EffectItemPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Effect Item  -&gt;  ALCH / ENCH / INGR / SCRL / SPEL. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2EffectItem As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Encounter Zone. Distinto de que el campo valga cero.</summary>
        Property Parameter2EncounterZonePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Encounter Zone  -&gt;  ECZN. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2EncounterZone As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Equip Type. Distinto de que el campo valga cero.</summary>
        Property Parameter2EquipTypePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Equip Type  -&gt;  EQUP. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2EquipType As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Event Data. Distinto de que el campo valga cero.</summary>
        Property Parameter2EventDataPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Event Data  -&gt;  FLST / KYWD / LCTN. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2EventData As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Faction. Distinto de que el campo valga cero.</summary>
        Property Parameter2FactionPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Faction  -&gt;  FACT. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Faction As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Form List. Distinto de que el campo valga cero.</summary>
        Property Parameter2FormListPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Form List  -&gt;  FLST. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2FormList As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Furniture. Distinto de que el campo valga cero.</summary>
        Property Parameter2FurniturePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Furniture  -&gt;  FLST / FURN. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Furniture As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Global. Distinto de que el campo valga cero.</summary>
        Property Parameter2GlobalPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Global  -&gt;  GLOB. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Global As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Idle. Distinto de que el campo valga cero.</summary>
        Property Parameter2IdlePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Idle  -&gt;  IDLE. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Idle As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Keyword. Distinto de que el campo valga cero.</summary>
        Property Parameter2KeywordPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Keyword  -&gt;  FLST / KYWD / NULL. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Keyword As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Location. Distinto de que el campo valga cero.</summary>
        Property Parameter2LocationPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Location  -&gt;  LCTN. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Location As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Location Ref Type. Distinto de que el campo valga cero.</summary>
        Property Parameter2LocationRefTypePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Location Ref Type  -&gt;  LCRT. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2LocationRefType As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Owner. Distinto de que el campo valga cero.</summary>
        Property Parameter2OwnerPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Owner  -&gt;  FACT / NPC_. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Owner As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Package. Distinto de que el campo valga cero.</summary>
        Property Parameter2PackagePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Package  -&gt;  PACK. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Package As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Perk. Distinto de que el campo valga cero.</summary>
        Property Parameter2PerkPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Perk  -&gt;  PERK. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Perk As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Quest. Distinto de que el campo valga cero.</summary>
        Property Parameter2QuestPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Quest  -&gt;  QUST. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Quest As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Race. Distinto de que el campo valga cero.</summary>
        Property Parameter2RacePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Race  -&gt;  RACE. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Race As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Reference. Distinto de que el campo valga cero.</summary>
        Property Parameter2ReferencePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Reference  -&gt;  ACHR / PARW / PBAR / PBEA / PCON / PFLA / PGRE / PHZD / PLYR / PMIS / REFR / TRGT. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Reference As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Region. Distinto de que el campo valga cero.</summary>
        Property Parameter2RegionPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Region  -&gt;  REGN. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Region As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Scene. Distinto de que el campo valga cero.</summary>
        Property Parameter2ScenePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Scene  -&gt;  SCEN. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Scene As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Voice Type. Distinto de que el campo valga cero.</summary>
        Property Parameter2VoiceTypePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Voice Type  -&gt;  VTYP / FLST. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2VoiceType As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Weather. Distinto de que el campo valga cero.</summary>
        Property Parameter2WeatherPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Weather  -&gt;  WTHR. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Weather As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Worldspace. Distinto de que el campo valga cero.</summary>
        Property Parameter2WorldspacePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Worldspace  -&gt;  WRLD / FLST. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Worldspace As UInteger
        ''' <summary>El record trae Condition\CTDA\Run On. Distinto de que el campo valga cero.</summary>
        Property ConditionRunOnPresente As Boolean
        ''' <summary>Condition\CTDA\Run On</summary>
        Property ConditionRunOn As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Run On.</summary>
        ReadOnly Property ConditionRunOnNombre As String
        ''' <summary>El record trae Condition\CTDA\Reference\Reference. Distinto de que el campo valga cero.</summary>
        Property ConditionReferencePresente As Boolean
        ''' <summary>Condition\CTDA\Reference\Reference. Referencia en el espacio del orden de carga.</summary>
        Property ConditionReference As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #3\Parameter #3. Distinto de que el campo valga cero.</summary>
        Property ConditionParameter3Presente As Boolean
        ''' <summary>Condition\CTDA\Parameter #3\Parameter #3</summary>
        Property ConditionParameter3 As Integer
        ''' <summary>El record trae Condition\CTDA\Parameter #3\Quest Alias. Distinto de que el campo valga cero.</summary>
        Property Parameter3QuestAliasPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #3\Quest Alias</summary>
        Property Parameter3QuestAlias As Integer
        ''' <summary>El record trae Condition\CTDA\Parameter #3\Event Data. Distinto de que el campo valga cero.</summary>
        Property Parameter3EventDataPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #3\Event Data</summary>
        Property Parameter3EventData As Integer
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #3\Event Data.</summary>
        ReadOnly Property Parameter3EventDataNombre As String
        ''' <summary>El record trae Condition\CIS1\Parameter #1. Distinto de que el campo valga cero.</summary>
        Property ConditionParameter1Presente As Boolean
        ''' <summary>Condition\CIS1\Parameter #1</summary>
        Property ConditionParameter1 As String
        ''' <summary>El record trae Condition\CIS2\Parameter #2. Distinto de que el campo valga cero.</summary>
        Property ConditionParameter2Presente As Boolean
        ''' <summary>Condition\CIS2\Parameter #2</summary>
        Property ConditionParameter2 As String
    End Interface

    ''' <summary>Campos de un record LVLI que los dos juegos declaran igual.
    ''' <para>El codigo que solo usa estos no necesita saber de que juego se trata. El
    ''' que necesita algo propio de un juego tiene que nombrar la clase de ese juego, y
    ''' asi no puede leer sin querer un campo que alla significa otra cosa.</para></summary>
    Public Interface ILvli
        ReadOnly Property Node As WbNode
        ''' <summary>Identificador del record.</summary>
        ReadOnly Property FormID As UInteger
        ''' <summary>El record trae EDID\Editor ID. Distinto de que el campo valga cero.</summary>
        Property EditorIDPresente As Boolean
        ''' <summary>EDID\Editor ID</summary>
        Property EditorID As String
        ''' <summary>El record trae OBND\Object Bounds\X1. Distinto de que el campo valga cero.</summary>
        Property ObjectBoundsX1Presente As Boolean
        ''' <summary>OBND\Object Bounds\X1</summary>
        Property ObjectBoundsX1 As Short
        ''' <summary>El record trae OBND\Object Bounds\Y1. Distinto de que el campo valga cero.</summary>
        Property ObjectBoundsY1Presente As Boolean
        ''' <summary>OBND\Object Bounds\Y1</summary>
        Property ObjectBoundsY1 As Short
        ''' <summary>El record trae OBND\Object Bounds\Z1. Distinto de que el campo valga cero.</summary>
        Property ObjectBoundsZ1Presente As Boolean
        ''' <summary>OBND\Object Bounds\Z1</summary>
        Property ObjectBoundsZ1 As Short
        ''' <summary>El record trae OBND\Object Bounds\X2. Distinto de que el campo valga cero.</summary>
        Property ObjectBoundsX2Presente As Boolean
        ''' <summary>OBND\Object Bounds\X2</summary>
        Property ObjectBoundsX2 As Short
        ''' <summary>El record trae OBND\Object Bounds\Y2. Distinto de que el campo valga cero.</summary>
        Property ObjectBoundsY2Presente As Boolean
        ''' <summary>OBND\Object Bounds\Y2</summary>
        Property ObjectBoundsY2 As Short
        ''' <summary>El record trae OBND\Object Bounds\Z2. Distinto de que el campo valga cero.</summary>
        Property ObjectBoundsZ2Presente As Boolean
        ''' <summary>OBND\Object Bounds\Z2</summary>
        Property ObjectBoundsZ2 As Short
        ''' <summary>El record trae LVLD\Chance None. Distinto de que el campo valga cero.</summary>
        Property ChanceNonePresente As Boolean
        ''' <summary>LVLD\Chance None</summary>
        Property ChanceNone As Byte
        ''' <summary>El record trae LVLF\Flags. Distinto de que el campo valga cero.</summary>
        Property FlagsPresente As Boolean
        ''' <summary>LVLF\Flags</summary>
        Property Flags As Byte
        ''' <summary>Bit 0 de LVLF\Flags: Calculate from all levels &lt;= player's level</summary>
        Property FlagsCalculateFromAllLevelsPlayerSLevel As Boolean
        ''' <summary>Bit 1 de LVLF\Flags: Calculate for each item in count</summary>
        Property FlagsCalculateForEachItemInCount As Boolean
        ''' <summary>Bit 2 de LVLF\Flags: Use All</summary>
        Property FlagsUseAll As Boolean
        ''' <summary>El record trae LLCT\Count. Distinto de que el campo valga cero.</summary>
        Property CountPresente As Boolean
        ''' <summary>LLCT\Count</summary>
        Property Count As Byte
        ''' <summary>Leveled List Entries</summary>
        ReadOnly Property LeveledListEntries As IReadOnlyList(Of ILvli_LeveledListEntries)
        Function AgregarLeveledListEntries() As ILvli_LeveledListEntries
        Function QuitarLeveledListEntries(indice As Integer) As Boolean
        Function ReordenarLeveledListEntries(permutacion As IList(Of Integer)) As Boolean
        Function QuitarLeveledListEntries(elemento As ILvli_LeveledListEntries) As Boolean
    End Interface

    ''' <summary>Un elemento de Leveled List Entries, en lo que los dos juegos comparten.</summary>
    Public Interface ILvli_LeveledListEntries
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Leveled List Entry\LVLO\Level. Distinto de que el campo valga cero.</summary>
        Property LeveledListEntryLevelPresente As Boolean
        ''' <summary>Leveled List Entry\LVLO\Level</summary>
        Property LeveledListEntryLevel As UShort
        ''' <summary>El record trae Leveled List Entry\LVLO\Item. Distinto de que el campo valga cero.</summary>
        Property LeveledListEntryItemPresente As Boolean
        ''' <summary>Leveled List Entry\LVLO\Item. Referencia en el espacio del orden de carga.</summary>
        Property LeveledListEntryItem As UInteger
        ''' <summary>El record trae Leveled List Entry\LVLO\Count. Distinto de que el campo valga cero.</summary>
        Property LeveledListEntryCountPresente As Boolean
        ''' <summary>Leveled List Entry\LVLO\Count</summary>
        Property LeveledListEntryCount As UShort
        ''' <summary>El record trae Leveled List Entry\COED\Extra Data\Owner. Distinto de que el campo valga cero.</summary>
        Property ExtraDataOwnerPresente As Boolean
        ''' <summary>Leveled List Entry\COED\Extra Data\Owner  -&gt;  NPC_ / FACT / NULL. Referencia en el espacio del orden de carga.</summary>
        Property ExtraDataOwner As UInteger
        ''' <summary>El record trae Leveled List Entry\COED\Extra Data\Global Variable / Required Rank\Global Variable. Distinto de que el campo valga cero.</summary>
        Property GlobalVariableRequiredRankGlobalVariablePresente As Boolean
        ''' <summary>Leveled List Entry\COED\Extra Data\Global Variable / Required Rank\Global Variable  -&gt;  GLOB / NULL. Referencia en el espacio del orden de carga.</summary>
        Property GlobalVariableRequiredRankGlobalVariable As UInteger
        ''' <summary>El record trae Leveled List Entry\COED\Extra Data\Global Variable / Required Rank\Required Rank. Distinto de que el campo valga cero.</summary>
        Property GlobalVariableRequiredRankRequiredRankPresente As Boolean
        ''' <summary>Leveled List Entry\COED\Extra Data\Global Variable / Required Rank\Required Rank</summary>
        Property GlobalVariableRequiredRankRequiredRank As Integer
        ''' <summary>El record trae Leveled List Entry\COED\Extra Data\Item Condition. Distinto de que el campo valga cero.</summary>
        Property ExtraDataItemConditionPresente As Boolean
        ''' <summary>Leveled List Entry\COED\Extra Data\Item Condition</summary>
        Property ExtraDataItemCondition As Single
    End Interface

    ''' <summary>Campos de un record LVLN que los dos juegos declaran igual.
    ''' <para>El codigo que solo usa estos no necesita saber de que juego se trata. El
    ''' que necesita algo propio de un juego tiene que nombrar la clase de ese juego, y
    ''' asi no puede leer sin querer un campo que alla significa otra cosa.</para></summary>
    Public Interface ILvln
        ReadOnly Property Node As WbNode
        ''' <summary>Identificador del record.</summary>
        ReadOnly Property FormID As UInteger
        ''' <summary>El record trae EDID\Editor ID. Distinto de que el campo valga cero.</summary>
        Property EditorIDPresente As Boolean
        ''' <summary>EDID\Editor ID</summary>
        Property EditorID As String
        ''' <summary>El record trae OBND\Object Bounds\X1. Distinto de que el campo valga cero.</summary>
        Property ObjectBoundsX1Presente As Boolean
        ''' <summary>OBND\Object Bounds\X1</summary>
        Property ObjectBoundsX1 As Short
        ''' <summary>El record trae OBND\Object Bounds\Y1. Distinto de que el campo valga cero.</summary>
        Property ObjectBoundsY1Presente As Boolean
        ''' <summary>OBND\Object Bounds\Y1</summary>
        Property ObjectBoundsY1 As Short
        ''' <summary>El record trae OBND\Object Bounds\Z1. Distinto de que el campo valga cero.</summary>
        Property ObjectBoundsZ1Presente As Boolean
        ''' <summary>OBND\Object Bounds\Z1</summary>
        Property ObjectBoundsZ1 As Short
        ''' <summary>El record trae OBND\Object Bounds\X2. Distinto de que el campo valga cero.</summary>
        Property ObjectBoundsX2Presente As Boolean
        ''' <summary>OBND\Object Bounds\X2</summary>
        Property ObjectBoundsX2 As Short
        ''' <summary>El record trae OBND\Object Bounds\Y2. Distinto de que el campo valga cero.</summary>
        Property ObjectBoundsY2Presente As Boolean
        ''' <summary>OBND\Object Bounds\Y2</summary>
        Property ObjectBoundsY2 As Short
        ''' <summary>El record trae OBND\Object Bounds\Z2. Distinto de que el campo valga cero.</summary>
        Property ObjectBoundsZ2Presente As Boolean
        ''' <summary>OBND\Object Bounds\Z2</summary>
        Property ObjectBoundsZ2 As Short
        ''' <summary>El record trae LVLD\Chance None. Distinto de que el campo valga cero.</summary>
        Property ChanceNonePresente As Boolean
        ''' <summary>LVLD\Chance None</summary>
        Property ChanceNone As Byte
        ''' <summary>El record trae LVLF\Flags. Distinto de que el campo valga cero.</summary>
        Property FlagsPresente As Boolean
        ''' <summary>LVLF\Flags</summary>
        Property Flags As Byte
        ''' <summary>Bit 0 de LVLF\Flags: Calculate from all levels &lt;= player's level</summary>
        Property FlagsCalculateFromAllLevelsPlayerSLevel As Boolean
        ''' <summary>Bit 1 de LVLF\Flags: Calculate for each item in count</summary>
        Property FlagsCalculateForEachItemInCount As Boolean
        ''' <summary>El record trae LLCT\Count. Distinto de que el campo valga cero.</summary>
        Property CountPresente As Boolean
        ''' <summary>LLCT\Count</summary>
        Property Count As Byte
        ''' <summary>El record trae Model\MODL\Model FileName. Distinto de que el campo valga cero.</summary>
        Property ModelFileNamePresente As Boolean
        ''' <summary>Model\MODL\Model FileName</summary>
        Property ModelFileName As String
        ''' <summary>El record trae Model\MODT\Model Information\ERROR. Distinto de que el campo valga cero.</summary>
        Property ModelInformationERRORPresente As Boolean
        ''' <summary>Model\MODT\Model Information\ERROR</summary>
        Property ModelInformationERROR As Byte()
        ''' <summary>Leveled List Entries</summary>
        ReadOnly Property LeveledListEntries As IReadOnlyList(Of ILvln_LeveledListEntries)
        Function AgregarLeveledListEntries() As ILvln_LeveledListEntries
        Function QuitarLeveledListEntries(indice As Integer) As Boolean
        Function ReordenarLeveledListEntries(permutacion As IList(Of Integer)) As Boolean
        Function QuitarLeveledListEntries(elemento As ILvln_LeveledListEntries) As Boolean
        ''' <summary>Model\MODT\Model Information\Textures</summary>
        ReadOnly Property Textures As IReadOnlyList(Of ILvln_Textures)
        Function AgregarTextures() As ILvln_Textures
        Function QuitarTextures(indice As Integer) As Boolean
        Function ReordenarTextures(permutacion As IList(Of Integer)) As Boolean
        Function QuitarTextures(elemento As ILvln_Textures) As Boolean
        ''' <summary>Model\MODT\Model Information\Counters</summary>
        ReadOnly Property Counters As IReadOnlyList(Of ILvln_Counters)
        Function AgregarCounters() As ILvln_Counters
        Function QuitarCounters(indice As Integer) As Boolean
        Function ReordenarCounters(permutacion As IList(Of Integer)) As Boolean
        Function QuitarCounters(elemento As ILvln_Counters) As Boolean
        ''' <summary>Model\MODT\Model Information\Addon Nodes</summary>
        ReadOnly Property AddonNodes As IReadOnlyList(Of ILvln_AddonNodes)
        Function AgregarAddonNodes() As ILvln_AddonNodes
        Function QuitarAddonNodes(indice As Integer) As Boolean
        Function ReordenarAddonNodes(permutacion As IList(Of Integer)) As Boolean
        Function QuitarAddonNodes(elemento As ILvln_AddonNodes) As Boolean
    End Interface

    ''' <summary>Un elemento de Leveled List Entries, en lo que los dos juegos comparten.</summary>
    Public Interface ILvln_LeveledListEntries
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Leveled List Entry\LVLO\Level. Distinto de que el campo valga cero.</summary>
        Property LeveledListEntryLevelPresente As Boolean
        ''' <summary>Leveled List Entry\LVLO\Level</summary>
        Property LeveledListEntryLevel As UShort
        ''' <summary>El record trae Leveled List Entry\LVLO\NPC. Distinto de que el campo valga cero.</summary>
        Property LeveledListEntryNPCPresente As Boolean
        ''' <summary>Leveled List Entry\LVLO\NPC  -&gt;  LVLN / NPC_. Referencia en el espacio del orden de carga.</summary>
        Property LeveledListEntryNPC As UInteger
        ''' <summary>El record trae Leveled List Entry\LVLO\Count. Distinto de que el campo valga cero.</summary>
        Property LeveledListEntryCountPresente As Boolean
        ''' <summary>Leveled List Entry\LVLO\Count</summary>
        Property LeveledListEntryCount As UShort
        ''' <summary>El record trae Leveled List Entry\COED\Extra Data\Owner. Distinto de que el campo valga cero.</summary>
        Property ExtraDataOwnerPresente As Boolean
        ''' <summary>Leveled List Entry\COED\Extra Data\Owner  -&gt;  NPC_ / FACT / NULL. Referencia en el espacio del orden de carga.</summary>
        Property ExtraDataOwner As UInteger
        ''' <summary>El record trae Leveled List Entry\COED\Extra Data\Global Variable / Required Rank\Global Variable. Distinto de que el campo valga cero.</summary>
        Property GlobalVariableRequiredRankGlobalVariablePresente As Boolean
        ''' <summary>Leveled List Entry\COED\Extra Data\Global Variable / Required Rank\Global Variable  -&gt;  GLOB / NULL. Referencia en el espacio del orden de carga.</summary>
        Property GlobalVariableRequiredRankGlobalVariable As UInteger
        ''' <summary>El record trae Leveled List Entry\COED\Extra Data\Global Variable / Required Rank\Required Rank. Distinto de que el campo valga cero.</summary>
        Property GlobalVariableRequiredRankRequiredRankPresente As Boolean
        ''' <summary>Leveled List Entry\COED\Extra Data\Global Variable / Required Rank\Required Rank</summary>
        Property GlobalVariableRequiredRankRequiredRank As Integer
        ''' <summary>El record trae Leveled List Entry\COED\Extra Data\Item Condition. Distinto de que el campo valga cero.</summary>
        Property ExtraDataItemConditionPresente As Boolean
        ''' <summary>Leveled List Entry\COED\Extra Data\Item Condition</summary>
        Property ExtraDataItemCondition As Single
    End Interface

    ''' <summary>Un elemento de Model\MODT\Model Information\Textures, en lo que los dos juegos comparten.</summary>
    Public Interface ILvln_Textures
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Texture\File Hash. Distinto de que el campo valga cero.</summary>
        Property TextureFileHashPresente As Boolean
        ''' <summary>Texture\File Hash</summary>
        Property TextureFileHash As UInteger
        ''' <summary>El record trae Texture\Extension. Distinto de que el campo valga cero.</summary>
        Property TextureExtensionPresente As Boolean
        ''' <summary>Texture\Extension</summary>
        Property TextureExtension As String
        ''' <summary>El record trae Texture\Folder Hash. Distinto de que el campo valga cero.</summary>
        Property TextureFolderHashPresente As Boolean
        ''' <summary>Texture\Folder Hash</summary>
        Property TextureFolderHash As UInteger
    End Interface

    ''' <summary>Un elemento de Model\MODT\Model Information\Counters, en lo que los dos juegos comparten.</summary>
    Public Interface ILvln_Counters
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Counter. Distinto de que el campo valga cero.</summary>
        Property CounterPresente As Boolean
        ''' <summary>Counter</summary>
        Property Counter As UInteger
    End Interface

    ''' <summary>Un elemento de Model\MODT\Model Information\Addon Nodes, en lo que los dos juegos comparten.</summary>
    Public Interface ILvln_AddonNodes
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Addon Node. Distinto de que el campo valga cero.</summary>
        Property AddonNodePresente As Boolean
        ''' <summary>Addon Node</summary>
        Property AddonNode As UInteger
    End Interface

    ''' <summary>Campos de un record MSWP que los dos juegos declaran igual.
    ''' <para>El codigo que solo usa estos no necesita saber de que juego se trata. El
    ''' que necesita algo propio de un juego tiene que nombrar la clase de ese juego, y
    ''' asi no puede leer sin querer un campo que alla significa otra cosa.</para></summary>
    Public Interface IMswp
        ReadOnly Property Node As WbNode
        ''' <summary>Identificador del record.</summary>
        ReadOnly Property FormID As UInteger
        ''' <summary>Bit 16 de las banderas de la cabecera del record: Custom Swap.</summary>
        Property CustomSwap As Boolean
        ''' <summary>El record trae EDID\Editor ID. Distinto de que el campo valga cero.</summary>
        Property EditorIDPresente As Boolean
        ''' <summary>EDID\Editor ID</summary>
        Property EditorID As String
        ''' <summary>El record trae FNAM\Tree Folder. Distinto de que el campo valga cero.</summary>
        Property TreeFolderPresente As Boolean
        ''' <summary>FNAM\Tree Folder</summary>
        Property TreeFolder As String
        ''' <summary>Material Substitutions</summary>
        ReadOnly Property MaterialSubstitutions As IReadOnlyList(Of IMswp_MaterialSubstitutions)
        Function AgregarMaterialSubstitutions() As IMswp_MaterialSubstitutions
        Function QuitarMaterialSubstitutions(indice As Integer) As Boolean
        Function ReordenarMaterialSubstitutions(permutacion As IList(Of Integer)) As Boolean
        Function QuitarMaterialSubstitutions(elemento As IMswp_MaterialSubstitutions) As Boolean
    End Interface

    ''' <summary>Un elemento de Material Substitutions, en lo que los dos juegos comparten.</summary>
    Public Interface IMswp_MaterialSubstitutions
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Substitution\BNAM\Original Material. Distinto de que el campo valga cero.</summary>
        Property SubstitutionOriginalMaterialPresente As Boolean
        ''' <summary>Substitution\BNAM\Original Material</summary>
        Property SubstitutionOriginalMaterial As String
        ''' <summary>El record trae Substitution\SNAM\Replacement Material. Distinto de que el campo valga cero.</summary>
        Property SubstitutionReplacementMaterialPresente As Boolean
        ''' <summary>Substitution\SNAM\Replacement Material</summary>
        Property SubstitutionReplacementMaterial As String
        ''' <summary>El record trae Substitution\FNAM\Tree Folder (obsolete). Distinto de que el campo valga cero.</summary>
        Property SubstitutionTreeFolderObsoletePresente As Boolean
        ''' <summary>Substitution\FNAM\Tree Folder (obsolete)</summary>
        Property SubstitutionTreeFolderObsolete As String
        ''' <summary>El record trae Substitution\CNAM\Color Remapping Index. Distinto de que el campo valga cero.</summary>
        Property SubstitutionColorRemappingIndexPresente As Boolean
        ''' <summary>Substitution\CNAM\Color Remapping Index</summary>
        Property SubstitutionColorRemappingIndex As Single
    End Interface

    ''' <summary>Campos de un record NPC_ que los dos juegos declaran igual.
    ''' <para>El codigo que solo usa estos no necesita saber de que juego se trata. El
    ''' que necesita algo propio de un juego tiene que nombrar la clase de ese juego, y
    ''' asi no puede leer sin querer un campo que alla significa otra cosa.</para></summary>
    Public Interface INpc
        ReadOnly Property Node As WbNode
        ''' <summary>Identificador del record.</summary>
        ReadOnly Property FormID As UInteger
        ''' <summary>Bit 18 de las banderas de la cabecera del record: Compressed.</summary>
        Property Compressed As Boolean
        ''' <summary>Bit 29 de las banderas de la cabecera del record: Bleedout Override.</summary>
        Property BleedoutOverride As Boolean
        ''' <summary>El record trae EDID\Editor ID. Distinto de que el campo valga cero.</summary>
        Property EditorIDPresente As Boolean
        ''' <summary>EDID\Editor ID</summary>
        Property EditorID As String
        ''' <summary>El record trae VMAD\Virtual Machine Adapter\Version. Distinto de que el campo valga cero.</summary>
        Property VirtualMachineAdapterVersionPresente As Boolean
        ''' <summary>VMAD\Virtual Machine Adapter\Version</summary>
        Property VirtualMachineAdapterVersion As Short
        ''' <summary>El record trae VMAD\Virtual Machine Adapter\Object Format. Distinto de que el campo valga cero.</summary>
        Property VirtualMachineAdapterObjectFormatPresente As Boolean
        ''' <summary>VMAD\Virtual Machine Adapter\Object Format</summary>
        Property VirtualMachineAdapterObjectFormat As Short
        ''' <summary>El record trae OBND\Object Bounds\X1. Distinto de que el campo valga cero.</summary>
        Property ObjectBoundsX1Presente As Boolean
        ''' <summary>OBND\Object Bounds\X1</summary>
        Property ObjectBoundsX1 As Short
        ''' <summary>El record trae OBND\Object Bounds\Y1. Distinto de que el campo valga cero.</summary>
        Property ObjectBoundsY1Presente As Boolean
        ''' <summary>OBND\Object Bounds\Y1</summary>
        Property ObjectBoundsY1 As Short
        ''' <summary>El record trae OBND\Object Bounds\Z1. Distinto de que el campo valga cero.</summary>
        Property ObjectBoundsZ1Presente As Boolean
        ''' <summary>OBND\Object Bounds\Z1</summary>
        Property ObjectBoundsZ1 As Short
        ''' <summary>El record trae OBND\Object Bounds\X2. Distinto de que el campo valga cero.</summary>
        Property ObjectBoundsX2Presente As Boolean
        ''' <summary>OBND\Object Bounds\X2</summary>
        Property ObjectBoundsX2 As Short
        ''' <summary>El record trae OBND\Object Bounds\Y2. Distinto de que el campo valga cero.</summary>
        Property ObjectBoundsY2Presente As Boolean
        ''' <summary>OBND\Object Bounds\Y2</summary>
        Property ObjectBoundsY2 As Short
        ''' <summary>El record trae OBND\Object Bounds\Z2. Distinto de que el campo valga cero.</summary>
        Property ObjectBoundsZ2Presente As Boolean
        ''' <summary>OBND\Object Bounds\Z2</summary>
        Property ObjectBoundsZ2 As Short
        ''' <summary>El record trae ACBS\Configuration\Flags. Distinto de que el campo valga cero.</summary>
        Property ConfigurationFlagsPresente As Boolean
        ''' <summary>ACBS\Configuration\Flags</summary>
        Property ConfigurationFlags As UInteger
        ''' <summary>Bit 0 de ACBS\Configuration\Flags: Female</summary>
        Property ConfigurationFlagsFemale As Boolean
        ''' <summary>Bit 1 de ACBS\Configuration\Flags: Essential</summary>
        Property ConfigurationFlagsEssential As Boolean
        ''' <summary>Bit 2 de ACBS\Configuration\Flags: Is CharGen Face Preset</summary>
        Property ConfigurationFlagsIsCharGenFacePreset As Boolean
        ''' <summary>Bit 3 de ACBS\Configuration\Flags: Respawn</summary>
        Property ConfigurationFlagsRespawn As Boolean
        ''' <summary>Bit 4 de ACBS\Configuration\Flags: Auto-calc stats</summary>
        Property ConfigurationFlagsAutoCalcStats As Boolean
        ''' <summary>Bit 5 de ACBS\Configuration\Flags: Unique</summary>
        Property ConfigurationFlagsUnique As Boolean
        ''' <summary>Bit 6 de ACBS\Configuration\Flags: Doesn't affect stealth meter</summary>
        Property ConfigurationFlagsDoesnTAffectStealthMeter As Boolean
        ''' <summary>Bit 7 de ACBS\Configuration\Flags: PC Level Mult</summary>
        Property ConfigurationFlagsPCLevelMult As Boolean
        ''' <summary>Bit 11 de ACBS\Configuration\Flags: Protected</summary>
        Property ConfigurationFlagsProtected As Boolean
        ''' <summary>Bit 14 de ACBS\Configuration\Flags: Summonable</summary>
        Property ConfigurationFlagsSummonable As Boolean
        ''' <summary>Bit 16 de ACBS\Configuration\Flags: Doesn't bleed</summary>
        Property ConfigurationFlagsDoesnTBleed As Boolean
        ''' <summary>Bit 18 de ACBS\Configuration\Flags: Bleedout Override</summary>
        Property ConfigurationFlagsBleedoutOverride As Boolean
        ''' <summary>Bit 19 de ACBS\Configuration\Flags: Opposite Gender Anims</summary>
        Property ConfigurationFlagsOppositeGenderAnims As Boolean
        ''' <summary>Bit 20 de ACBS\Configuration\Flags: Simple Actor</summary>
        Property ConfigurationFlagsSimpleActor As Boolean
        ''' <summary>Bit 29 de ACBS\Configuration\Flags: Is Ghost</summary>
        Property ConfigurationFlagsIsGhost As Boolean
        ''' <summary>Bit 31 de ACBS\Configuration\Flags: Invulnerable</summary>
        Property ConfigurationFlagsInvulnerable As Boolean
        ''' <summary>El record trae ACBS\Configuration\Level\Level. Distinto de que el campo valga cero.</summary>
        Property ConfigurationLevelPresente As Boolean
        ''' <summary>ACBS\Configuration\Level\Level</summary>
        Property ConfigurationLevel As UShort
        ''' <summary>El record trae ACBS\Configuration\Level\Level Mult. Distinto de que el campo valga cero.</summary>
        Property ConfigurationLevelMultPresente As Boolean
        ''' <summary>ACBS\Configuration\Level\Level Mult</summary>
        Property ConfigurationLevelMult As UShort
        ''' <summary>El record trae ACBS\Configuration\Calc min level. Distinto de que el campo valga cero.</summary>
        Property ConfigurationCalcMinLevelPresente As Boolean
        ''' <summary>ACBS\Configuration\Calc min level</summary>
        Property ConfigurationCalcMinLevel As UShort
        ''' <summary>El record trae ACBS\Configuration\Calc max level. Distinto de que el campo valga cero.</summary>
        Property ConfigurationCalcMaxLevelPresente As Boolean
        ''' <summary>ACBS\Configuration\Calc max level</summary>
        Property ConfigurationCalcMaxLevel As UShort
        ''' <summary>El record trae ACBS\Configuration\Template Flags. Distinto de que el campo valga cero.</summary>
        Property ConfigurationTemplateFlagsPresente As Boolean
        ''' <summary>ACBS\Configuration\Template Flags</summary>
        Property ConfigurationTemplateFlags As UShort
        ''' <summary>Bit 0 de ACBS\Configuration\Template Flags: Traits</summary>
        Property ConfigurationTemplateFlagsTraits As Boolean
        ''' <summary>Bit 1 de ACBS\Configuration\Template Flags: Stats</summary>
        Property ConfigurationTemplateFlagsStats As Boolean
        ''' <summary>Bit 2 de ACBS\Configuration\Template Flags: Factions</summary>
        Property ConfigurationTemplateFlagsFactions As Boolean
        ''' <summary>Bit 4 de ACBS\Configuration\Template Flags: AI Data</summary>
        Property ConfigurationTemplateFlagsAIData As Boolean
        ''' <summary>Bit 5 de ACBS\Configuration\Template Flags: AI Packages</summary>
        Property ConfigurationTemplateFlagsAIPackages As Boolean
        ''' <summary>Bit 6 de ACBS\Configuration\Template Flags: Model/Animation</summary>
        Property ConfigurationTemplateFlagsModelAnimation As Boolean
        ''' <summary>Bit 7 de ACBS\Configuration\Template Flags: Base Data</summary>
        Property ConfigurationTemplateFlagsBaseData As Boolean
        ''' <summary>Bit 8 de ACBS\Configuration\Template Flags: Inventory</summary>
        Property ConfigurationTemplateFlagsInventory As Boolean
        ''' <summary>Bit 9 de ACBS\Configuration\Template Flags: Script</summary>
        Property ConfigurationTemplateFlagsScript As Boolean
        ''' <summary>El record trae ACBS\Configuration\Bleedout Override. Distinto de que el campo valga cero.</summary>
        Property ConfigurationBleedoutOverridePresente As Boolean
        ''' <summary>ACBS\Configuration\Bleedout Override</summary>
        Property ConfigurationBleedoutOverride As UShort
        ''' <summary>El record trae INAM\Death item. Distinto de que el campo valga cero.</summary>
        Property DeathItemPresente As Boolean
        ''' <summary>INAM\Death item  -&gt;  LVLI. Referencia en el espacio del orden de carga.</summary>
        Property DeathItem As UInteger
        ''' <summary>El record trae VTCK\Voice. Distinto de que el campo valga cero.</summary>
        Property VoicePresente As Boolean
        ''' <summary>VTCK\Voice  -&gt;  VTYP. Referencia en el espacio del orden de carga.</summary>
        Property Voice As UInteger
        ''' <summary>El record trae RNAM\Race. Distinto de que el campo valga cero.</summary>
        Property RacePresente As Boolean
        ''' <summary>RNAM\Race  -&gt;  RACE. Referencia en el espacio del orden de carga.</summary>
        Property Race As UInteger
        ''' <summary>El record trae SPCT\Count. Distinto de que el campo valga cero.</summary>
        Property CountPresente As Boolean
        ''' <summary>SPCT\Count</summary>
        Property Count As UInteger
        ''' <summary>El record trae Destructible\DEST\Header\Health. Distinto de que el campo valga cero.</summary>
        Property HeaderHealthPresente As Boolean
        ''' <summary>Destructible\DEST\Header\Health</summary>
        Property HeaderHealth As Integer
        ''' <summary>El record trae Destructible\DEST\Header\DEST Count. Distinto de que el campo valga cero.</summary>
        Property HeaderDESTCountPresente As Boolean
        ''' <summary>Destructible\DEST\Header\DEST Count</summary>
        Property HeaderDESTCount As Byte
        ''' <summary>El record trae Destructible\DEST\Header\Unknown. Distinto de que el campo valga cero.</summary>
        Property HeaderUnknownPresente As Boolean
        ''' <summary>Destructible\DEST\Header\Unknown</summary>
        Property HeaderUnknown As Byte()
        ''' <summary>El record trae WNAM\Skin. Distinto de que el campo valga cero.</summary>
        Property SkinPresente As Boolean
        ''' <summary>WNAM\Skin  -&gt;  ARMO. Referencia en el espacio del orden de carga.</summary>
        Property Skin As UInteger
        ''' <summary>El record trae ANAM\Far away model. Distinto de que el campo valga cero.</summary>
        Property FarAwayModelPresente As Boolean
        ''' <summary>ANAM\Far away model  -&gt;  ARMO. Referencia en el espacio del orden de carga.</summary>
        Property FarAwayModel As UInteger
        ''' <summary>El record trae ATKR\Attack Race. Distinto de que el campo valga cero.</summary>
        Property AttackRacePresente As Boolean
        ''' <summary>ATKR\Attack Race  -&gt;  RACE. Referencia en el espacio del orden de carga.</summary>
        Property AttackRace As UInteger
        ''' <summary>El record trae SPOR\Spectator Override Package List. Distinto de que el campo valga cero.</summary>
        Property SpectatorOverridePackageListPresente As Boolean
        ''' <summary>SPOR\Spectator Override Package List  -&gt;  FLST. Referencia en el espacio del orden de carga.</summary>
        Property SpectatorOverridePackageList As UInteger
        ''' <summary>El record trae OCOR\Observe Dead Body Override Package List. Distinto de que el campo valga cero.</summary>
        Property ObserveDeadBodyOverridePackageListPresente As Boolean
        ''' <summary>OCOR\Observe Dead Body Override Package List  -&gt;  FLST. Referencia en el espacio del orden de carga.</summary>
        Property ObserveDeadBodyOverridePackageList As UInteger
        ''' <summary>El record trae GWOR\Guard Warn Override Package List. Distinto de que el campo valga cero.</summary>
        Property GuardWarnOverridePackageListPresente As Boolean
        ''' <summary>GWOR\Guard Warn Override Package List  -&gt;  FLST. Referencia en el espacio del orden de carga.</summary>
        Property GuardWarnOverridePackageList As UInteger
        ''' <summary>El record trae ECOR\Combat Override Package List. Distinto de que el campo valga cero.</summary>
        Property CombatOverridePackageListPresente As Boolean
        ''' <summary>ECOR\Combat Override Package List  -&gt;  FLST. Referencia en el espacio del orden de carga.</summary>
        Property CombatOverridePackageList As UInteger
        ''' <summary>El record trae PRKZ\Perk Count. Distinto de que el campo valga cero.</summary>
        Property PerkCountPresente As Boolean
        ''' <summary>PRKZ\Perk Count</summary>
        Property PerkCount As UInteger
        ''' <summary>El record trae COCT\Count. Distinto de que el campo valga cero.</summary>
        Property Count2Presente As Boolean
        ''' <summary>COCT\Count</summary>
        Property Count2 As UInteger
        ''' <summary>El record trae AIDT\AI Data\Aggression. Distinto de que el campo valga cero.</summary>
        Property AIDataAggressionPresente As Boolean
        ''' <summary>AIDT\AI Data\Aggression</summary>
        Property AIDataAggression As Byte
        ''' <summary>Nombre del valor de AIDT\AI Data\Aggression.</summary>
        ReadOnly Property AIDataAggressionNombre As String
        ''' <summary>El record trae AIDT\AI Data\Confidence. Distinto de que el campo valga cero.</summary>
        Property AIDataConfidencePresente As Boolean
        ''' <summary>AIDT\AI Data\Confidence</summary>
        Property AIDataConfidence As Byte
        ''' <summary>Nombre del valor de AIDT\AI Data\Confidence.</summary>
        ReadOnly Property AIDataConfidenceNombre As String
        ''' <summary>El record trae AIDT\AI Data\Energy Level. Distinto de que el campo valga cero.</summary>
        Property AIDataEnergyLevelPresente As Boolean
        ''' <summary>AIDT\AI Data\Energy Level</summary>
        Property AIDataEnergyLevel As Byte
        ''' <summary>El record trae AIDT\AI Data\Morality. Distinto de que el campo valga cero.</summary>
        Property AIDataMoralityPresente As Boolean
        ''' <summary>AIDT\AI Data\Morality</summary>
        Property AIDataMorality As Byte
        ''' <summary>Nombre del valor de AIDT\AI Data\Morality.</summary>
        ReadOnly Property AIDataMoralityNombre As String
        ''' <summary>El record trae AIDT\AI Data\Mood. Distinto de que el campo valga cero.</summary>
        Property AIDataMoodPresente As Boolean
        ''' <summary>AIDT\AI Data\Mood</summary>
        Property AIDataMood As Byte
        ''' <summary>Nombre del valor de AIDT\AI Data\Mood.</summary>
        ReadOnly Property AIDataMoodNombre As String
        ''' <summary>El record trae AIDT\AI Data\Assistance. Distinto de que el campo valga cero.</summary>
        Property AIDataAssistancePresente As Boolean
        ''' <summary>AIDT\AI Data\Assistance</summary>
        Property AIDataAssistance As Byte
        ''' <summary>Nombre del valor de AIDT\AI Data\Assistance.</summary>
        ReadOnly Property AIDataAssistanceNombre As String
        ''' <summary>El record trae AIDT\AI Data\Aggro\Aggro Radius Behavior. Distinto de que el campo valga cero.</summary>
        Property AIDataAggroRadiusBehaviorPresente As Boolean
        ''' <summary>AIDT\AI Data\Aggro\Aggro Radius Behavior. Ponerlo en verdadero conserva el valor previo si no era cero.</summary>
        Property AIDataAggroRadiusBehavior As Boolean
        ''' <summary>El record trae AIDT\AI Data\Aggro\Warn. Distinto de que el campo valga cero.</summary>
        Property AggroWarnPresente As Boolean
        ''' <summary>AIDT\AI Data\Aggro\Warn</summary>
        Property AggroWarn As UInteger
        ''' <summary>El record trae AIDT\AI Data\Aggro\Warn/Attack. Distinto de que el campo valga cero.</summary>
        Property AggroWarnAttackPresente As Boolean
        ''' <summary>AIDT\AI Data\Aggro\Warn/Attack</summary>
        Property AggroWarnAttack As UInteger
        ''' <summary>El record trae AIDT\AI Data\Aggro\Attack. Distinto de que el campo valga cero.</summary>
        Property AggroAttackPresente As Boolean
        ''' <summary>AIDT\AI Data\Aggro\Attack</summary>
        Property AggroAttack As UInteger
        ''' <summary>El record trae Keywords\KSIZ\Keyword Count. Distinto de que el campo valga cero.</summary>
        Property KeywordsKeywordCountPresente As Boolean
        ''' <summary>Keywords\KSIZ\Keyword Count</summary>
        Property KeywordsKeywordCount As UInteger
        ''' <summary>El record trae CNAM\Class. Distinto de que el campo valga cero.</summary>
        Property ClassPresente As Boolean
        ''' <summary>CNAM\Class  -&gt;  CLAS. Referencia en el espacio del orden de carga.</summary>
        Property [Class] As UInteger
        ''' <summary>El record trae FULL\Name. Distinto de que el campo valga cero.</summary>
        Property NamePresente As Boolean
        ''' <summary>FULL\Name. Resuelto contra las tablas de texto si el archivo las usa.</summary>
        Property Name As String
        ''' <summary>El record trae SHRT\Short Name. Distinto de que el campo valga cero.</summary>
        Property ShortNamePresente As Boolean
        ''' <summary>SHRT\Short Name. Resuelto contra las tablas de texto si el archivo las usa.</summary>
        Property ShortName As String
        ''' <summary>El record trae el marcador DATA\Marker. No lleva valor: el dato es que esté, así que ponerlo en verdadero lo crea y en falso lo saca.</summary>
        Property Marker As Boolean
        ''' <summary>El record trae HCLF\Hair Color. Distinto de que el campo valga cero.</summary>
        Property HairColorPresente As Boolean
        ''' <summary>HCLF\Hair Color  -&gt;  CLFM. Referencia en el espacio del orden de carga.</summary>
        Property HairColor As UInteger
        ''' <summary>El record trae ZNAM\Combat Style. Distinto de que el campo valga cero.</summary>
        Property CombatStylePresente As Boolean
        ''' <summary>ZNAM\Combat Style  -&gt;  CSTY. Referencia en el espacio del orden de carga.</summary>
        Property CombatStyle As UInteger
        ''' <summary>El record trae GNAM\Gift Filter. Distinto de que el campo valga cero.</summary>
        Property GiftFilterPresente As Boolean
        ''' <summary>GNAM\Gift Filter  -&gt;  FLST. Referencia en el espacio del orden de carga.</summary>
        Property GiftFilter As UInteger
        ''' <summary>El record trae NAM5\Unknown. Distinto de que el campo valga cero.</summary>
        Property UnknownPresente As Boolean
        ''' <summary>NAM5\Unknown</summary>
        Property Unknown As Byte()
        ''' <summary>El record trae NAM8\Sound Level. Distinto de que el campo valga cero.</summary>
        Property SoundLevelPresente As Boolean
        ''' <summary>NAM8\Sound Level</summary>
        Property SoundLevel As UInteger
        ''' <summary>Nombre del valor de NAM8\Sound Level.</summary>
        ReadOnly Property SoundLevelNombre As String
        ''' <summary>El record trae CSCR\Inherits Sounds From. Distinto de que el campo valga cero.</summary>
        Property InheritsSoundsFromPresente As Boolean
        ''' <summary>CSCR\Inherits Sounds From  -&gt;  NPC_. Referencia en el espacio del orden de carga.</summary>
        Property InheritsSoundsFrom As UInteger
        ''' <summary>El record trae DOFT\Default Outfit. Distinto de que el campo valga cero.</summary>
        Property DefaultOutfitPresente As Boolean
        ''' <summary>DOFT\Default Outfit  -&gt;  OTFT. Referencia en el espacio del orden de carga.</summary>
        Property DefaultOutfit As UInteger
        ''' <summary>El record trae SOFT\Sleeping Outfit. Distinto de que el campo valga cero.</summary>
        Property SleepingOutfitPresente As Boolean
        ''' <summary>SOFT\Sleeping Outfit  -&gt;  OTFT. Referencia en el espacio del orden de carga.</summary>
        Property SleepingOutfit As UInteger
        ''' <summary>El record trae DPLT\Default Package List. Distinto de que el campo valga cero.</summary>
        Property DefaultPackageListPresente As Boolean
        ''' <summary>DPLT\Default Package List  -&gt;  FLST. Referencia en el espacio del orden de carga.</summary>
        Property DefaultPackageList As UInteger
        ''' <summary>El record trae CRIF\Crime Faction. Distinto de que el campo valga cero.</summary>
        Property CrimeFactionPresente As Boolean
        ''' <summary>CRIF\Crime Faction  -&gt;  FACT. Referencia en el espacio del orden de carga.</summary>
        Property CrimeFaction As UInteger
        ''' <summary>El record trae FTST\Head Texture. Distinto de que el campo valga cero.</summary>
        Property HeadTexturePresente As Boolean
        ''' <summary>FTST\Head Texture  -&gt;  TXST. Referencia en el espacio del orden de carga.</summary>
        Property HeadTexture As UInteger
        ''' <summary>El record trae QNAM\Texture lighting\Red. Distinto de que el campo valga cero.</summary>
        Property TextureLightingRedPresente As Boolean
        ''' <summary>QNAM\Texture lighting\Red</summary>
        Property TextureLightingRed As Single
        ''' <summary>El record trae QNAM\Texture lighting\Green. Distinto de que el campo valga cero.</summary>
        Property TextureLightingGreenPresente As Boolean
        ''' <summary>QNAM\Texture lighting\Green</summary>
        Property TextureLightingGreen As Single
        ''' <summary>El record trae QNAM\Texture lighting\Blue. Distinto de que el campo valga cero.</summary>
        Property TextureLightingBluePresente As Boolean
        ''' <summary>QNAM\Texture lighting\Blue</summary>
        Property TextureLightingBlue As Single
        ''' <summary>VMAD\Virtual Machine Adapter\Scripts</summary>
        ReadOnly Property Scripts As IReadOnlyList(Of INpc_Scripts)
        Function AgregarScripts() As INpc_Scripts
        Function QuitarScripts(indice As Integer) As Boolean
        Function ReordenarScripts(permutacion As IList(Of Integer)) As Boolean
        Function QuitarScripts(elemento As INpc_Scripts) As Boolean
        ''' <summary>Factions</summary>
        ReadOnly Property Factions As IReadOnlyList(Of INpc_Factions)
        Function AgregarFactions() As INpc_Factions
        Function QuitarFactions(indice As Integer) As Boolean
        Function ReordenarFactions(permutacion As IList(Of Integer)) As Boolean
        Function QuitarFactions(elemento As INpc_Factions) As Boolean
        ''' <summary>Actor Effects</summary>
        ReadOnly Property ActorEffects As IReadOnlyList(Of INpc_ActorEffects)
        Function AgregarActorEffects() As INpc_ActorEffects
        Function QuitarActorEffects(indice As Integer) As Boolean
        Function ReordenarActorEffects(permutacion As IList(Of Integer)) As Boolean
        Function QuitarActorEffects(elemento As INpc_ActorEffects) As Boolean
        ''' <summary>Destructible\Stages</summary>
        ReadOnly Property Stages As IReadOnlyList(Of INpc_Stages)
        Function AgregarStages() As INpc_Stages
        Function QuitarStages(indice As Integer) As Boolean
        Function ReordenarStages(permutacion As IList(Of Integer)) As Boolean
        Function QuitarStages(elemento As INpc_Stages) As Boolean
        ''' <summary>Attacks</summary>
        ReadOnly Property Attacks As IReadOnlyList(Of INpc_Attacks)
        Function AgregarAttacks() As INpc_Attacks
        Function QuitarAttacks(indice As Integer) As Boolean
        Function ReordenarAttacks(permutacion As IList(Of Integer)) As Boolean
        Function QuitarAttacks(elemento As INpc_Attacks) As Boolean
        ''' <summary>Perks</summary>
        ReadOnly Property Perks As IReadOnlyList(Of INpc_Perks)
        Function AgregarPerks() As INpc_Perks
        Function QuitarPerks(indice As Integer) As Boolean
        Function ReordenarPerks(permutacion As IList(Of Integer)) As Boolean
        Function QuitarPerks(elemento As INpc_Perks) As Boolean
        ''' <summary>Items</summary>
        ReadOnly Property Items As IReadOnlyList(Of INpc_Items)
        Function AgregarItems() As INpc_Items
        Function QuitarItems(indice As Integer) As Boolean
        Function ReordenarItems(permutacion As IList(Of Integer)) As Boolean
        Function QuitarItems(elemento As INpc_Items) As Boolean
        ''' <summary>Packages</summary>
        ReadOnly Property Packages As IReadOnlyList(Of INpc_Packages)
        Function AgregarPackages() As INpc_Packages
        Function QuitarPackages(indice As Integer) As Boolean
        Function ReordenarPackages(permutacion As IList(Of Integer)) As Boolean
        Function QuitarPackages(elemento As INpc_Packages) As Boolean
        ''' <summary>Keywords\KWDA\Keywords</summary>
        ReadOnly Property Keywords As IReadOnlyList(Of INpc_Keywords)
        Function AgregarKeywords() As INpc_Keywords
        Function QuitarKeywords(indice As Integer) As Boolean
        Function ReordenarKeywords(permutacion As IList(Of Integer)) As Boolean
        Function QuitarKeywords(elemento As INpc_Keywords) As Boolean
        ''' <summary>Head Parts</summary>
        ReadOnly Property HeadParts As IReadOnlyList(Of INpc_HeadParts)
        Function AgregarHeadParts() As INpc_HeadParts
        Function QuitarHeadParts(indice As Integer) As Boolean
        Function ReordenarHeadParts(permutacion As IList(Of Integer)) As Boolean
        Function QuitarHeadParts(elemento As INpc_HeadParts) As Boolean
    End Interface

    ''' <summary>Un elemento de VMAD\Virtual Machine Adapter\Scripts, en lo que los dos juegos comparten.</summary>
    Public Interface INpc_Scripts
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Script\ScriptName. Distinto de que el campo valga cero.</summary>
        Property ScriptNamePresente As Boolean
        ''' <summary>Script\ScriptName</summary>
        Property ScriptName As String
        ''' <summary>El record trae Script\Flags. Distinto de que el campo valga cero.</summary>
        Property ScriptFlagsPresente As Boolean
        ''' <summary>Script\Flags</summary>
        Property ScriptFlags As Byte
        ''' <summary>Nombre del valor de Script\Flags.</summary>
        ReadOnly Property ScriptFlagsNombre As String
    End Interface

    ''' <summary>Un elemento de Factions, en lo que los dos juegos comparten.</summary>
    Public Interface INpc_Factions
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae SNAM\Faction\Faction. Distinto de que el campo valga cero.</summary>
        Property FactionPresente As Boolean
        ''' <summary>SNAM\Faction\Faction  -&gt;  FACT. Referencia en el espacio del orden de carga.</summary>
        Property Faction As UInteger
        ''' <summary>El record trae SNAM\Faction\Rank. Distinto de que el campo valga cero.</summary>
        Property FactionRankPresente As Boolean
        ''' <summary>SNAM\Faction\Rank</summary>
        Property FactionRank As SByte
    End Interface

    ''' <summary>Un elemento de Actor Effects, en lo que los dos juegos comparten.</summary>
    Public Interface INpc_ActorEffects
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae SPLO\Actor Effect. Distinto de que el campo valga cero.</summary>
        Property ActorEffectPresente As Boolean
        ''' <summary>SPLO\Actor Effect  -&gt;  SPEL / LVSP. Referencia en el espacio del orden de carga.</summary>
        Property ActorEffect As UInteger
    End Interface

    ''' <summary>Un elemento de Destructible\Stages, en lo que los dos juegos comparten.</summary>
    Public Interface INpc_Stages
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Stage\DSTD\Destruction Stage Data\Health %. Distinto de que el campo valga cero.</summary>
        Property DestructionStageDataHealthPresente As Boolean
        ''' <summary>Stage\DSTD\Destruction Stage Data\Health %</summary>
        Property DestructionStageDataHealth As Byte
        ''' <summary>El record trae Stage\DSTD\Destruction Stage Data\Index. Distinto de que el campo valga cero.</summary>
        Property DestructionStageDataIndexPresente As Boolean
        ''' <summary>Stage\DSTD\Destruction Stage Data\Index</summary>
        Property DestructionStageDataIndex As Byte
        ''' <summary>El record trae Stage\DSTD\Destruction Stage Data\Model Damage Stage. Distinto de que el campo valga cero.</summary>
        Property DestructionStageDataModelDamageStagePresente As Boolean
        ''' <summary>Stage\DSTD\Destruction Stage Data\Model Damage Stage</summary>
        Property DestructionStageDataModelDamageStage As Byte
        ''' <summary>El record trae Stage\DSTD\Destruction Stage Data\Flags. Distinto de que el campo valga cero.</summary>
        Property DestructionStageDataFlagsPresente As Boolean
        ''' <summary>Stage\DSTD\Destruction Stage Data\Flags</summary>
        Property DestructionStageDataFlags As Byte
        ''' <summary>Bit 0 de Stage\DSTD\Destruction Stage Data\Flags: Cap Damage</summary>
        Property DestructionStageDataFlagsCapDamage As Boolean
        ''' <summary>Bit 1 de Stage\DSTD\Destruction Stage Data\Flags: Disable</summary>
        Property DestructionStageDataFlagsDisable As Boolean
        ''' <summary>Bit 2 de Stage\DSTD\Destruction Stage Data\Flags: Destroy</summary>
        Property DestructionStageDataFlagsDestroy As Boolean
        ''' <summary>Bit 3 de Stage\DSTD\Destruction Stage Data\Flags: Ignore External Dmg</summary>
        Property DestructionStageDataFlagsIgnoreExternalDmg As Boolean
        ''' <summary>El record trae Stage\DSTD\Destruction Stage Data\Self Damage per Second. Distinto de que el campo valga cero.</summary>
        Property DestructionStageDataSelfDamagePerSecondPresente As Boolean
        ''' <summary>Stage\DSTD\Destruction Stage Data\Self Damage per Second</summary>
        Property DestructionStageDataSelfDamagePerSecond As Integer
        ''' <summary>El record trae Stage\DSTD\Destruction Stage Data\Explosion. Distinto de que el campo valga cero.</summary>
        Property DestructionStageDataExplosionPresente As Boolean
        ''' <summary>Stage\DSTD\Destruction Stage Data\Explosion  -&gt;  EXPL / NULL. Referencia en el espacio del orden de carga.</summary>
        Property DestructionStageDataExplosion As UInteger
        ''' <summary>El record trae Stage\DSTD\Destruction Stage Data\Debris. Distinto de que el campo valga cero.</summary>
        Property DestructionStageDataDebrisPresente As Boolean
        ''' <summary>Stage\DSTD\Destruction Stage Data\Debris  -&gt;  DEBR / NULL. Referencia en el espacio del orden de carga.</summary>
        Property DestructionStageDataDebris As UInteger
        ''' <summary>El record trae Stage\DSTD\Destruction Stage Data\Debris Count. Distinto de que el campo valga cero.</summary>
        Property DestructionStageDataDebrisCountPresente As Boolean
        ''' <summary>Stage\DSTD\Destruction Stage Data\Debris Count</summary>
        Property DestructionStageDataDebrisCount As Integer
        ''' <summary>El record trae Stage\Model\DMDL\Model FileName. Distinto de que el campo valga cero.</summary>
        Property StageModelFileNamePresente As Boolean
        ''' <summary>Stage\Model\DMDL\Model FileName</summary>
        Property StageModelFileName As String
        ''' <summary>El record trae Stage\Model\DMDT\Model Information\ERROR. Distinto de que el campo valga cero.</summary>
        Property ModelInformationERRORPresente As Boolean
        ''' <summary>Stage\Model\DMDT\Model Information\ERROR</summary>
        Property ModelInformationERROR As Byte()
        ''' <summary>El record trae el marcador Stage\DSTF\End Marker. No lleva valor: el dato es que esté, así que ponerlo en verdadero lo crea y en falso lo saca.</summary>
        Property StageEndMarker As Boolean
    End Interface

    ''' <summary>Un elemento de Attacks, en lo que los dos juegos comparten.</summary>
    Public Interface INpc_Attacks
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Attack\ATKD\Attack Data\Damage Mult. Distinto de que el campo valga cero.</summary>
        Property AttackDataDamageMultPresente As Boolean
        ''' <summary>Attack\ATKD\Attack Data\Damage Mult</summary>
        Property AttackDataDamageMult As Single
        ''' <summary>El record trae Attack\ATKD\Attack Data\Attack Chance. Distinto de que el campo valga cero.</summary>
        Property AttackDataAttackChancePresente As Boolean
        ''' <summary>Attack\ATKD\Attack Data\Attack Chance</summary>
        Property AttackDataAttackChance As Single
        ''' <summary>El record trae Attack\ATKD\Attack Data\Attack Spell. Distinto de que el campo valga cero.</summary>
        Property AttackDataAttackSpellPresente As Boolean
        ''' <summary>Attack\ATKD\Attack Data\Attack Spell  -&gt;  SPEL / NULL. Referencia en el espacio del orden de carga.</summary>
        Property AttackDataAttackSpell As UInteger
        ''' <summary>El record trae Attack\ATKD\Attack Data\Attack Flags. Distinto de que el campo valga cero.</summary>
        Property AttackDataAttackFlagsPresente As Boolean
        ''' <summary>Attack\ATKD\Attack Data\Attack Flags</summary>
        Property AttackDataAttackFlags As UInteger
        ''' <summary>Bit 0 de Attack\ATKD\Attack Data\Attack Flags: Ignore Weapon</summary>
        Property AttackDataAttackFlagsIgnoreWeapon As Boolean
        ''' <summary>Bit 1 de Attack\ATKD\Attack Data\Attack Flags: Bash Attack</summary>
        Property AttackDataAttackFlagsBashAttack As Boolean
        ''' <summary>Bit 2 de Attack\ATKD\Attack Data\Attack Flags: Power Attack</summary>
        Property AttackDataAttackFlagsPowerAttack As Boolean
        ''' <summary>Bit 4 de Attack\ATKD\Attack Data\Attack Flags: Rotating Attack</summary>
        Property AttackDataAttackFlagsRotatingAttack As Boolean
        ''' <summary>Bit 31 de Attack\ATKD\Attack Data\Attack Flags: Override Data</summary>
        Property AttackDataAttackFlagsOverrideData As Boolean
        ''' <summary>El record trae Attack\ATKD\Attack Data\Attack Angle. Distinto de que el campo valga cero.</summary>
        Property AttackDataAttackAnglePresente As Boolean
        ''' <summary>Attack\ATKD\Attack Data\Attack Angle</summary>
        Property AttackDataAttackAngle As Single
        ''' <summary>El record trae Attack\ATKD\Attack Data\Strike Angle. Distinto de que el campo valga cero.</summary>
        Property AttackDataStrikeAnglePresente As Boolean
        ''' <summary>Attack\ATKD\Attack Data\Strike Angle</summary>
        Property AttackDataStrikeAngle As Single
        ''' <summary>El record trae Attack\ATKD\Attack Data\Stagger. Distinto de que el campo valga cero.</summary>
        Property AttackDataStaggerPresente As Boolean
        ''' <summary>Attack\ATKD\Attack Data\Stagger</summary>
        Property AttackDataStagger As Single
        ''' <summary>El record trae Attack\ATKD\Attack Data\Knockdown. Distinto de que el campo valga cero.</summary>
        Property AttackDataKnockdownPresente As Boolean
        ''' <summary>Attack\ATKD\Attack Data\Knockdown</summary>
        Property AttackDataKnockdown As Single
        ''' <summary>El record trae Attack\ATKD\Attack Data\Recovery Time. Distinto de que el campo valga cero.</summary>
        Property AttackDataRecoveryTimePresente As Boolean
        ''' <summary>Attack\ATKD\Attack Data\Recovery Time</summary>
        Property AttackDataRecoveryTime As Single
        ''' <summary>El record trae Attack\ATKE\Attack Event. Distinto de que el campo valga cero.</summary>
        Property AttackEventPresente As Boolean
        ''' <summary>Attack\ATKE\Attack Event</summary>
        Property AttackEvent As String
    End Interface

    ''' <summary>Un elemento de Perks, en lo que los dos juegos comparten.</summary>
    Public Interface INpc_Perks
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae PRKR\Perk\Perk. Distinto de que el campo valga cero.</summary>
        Property PerkPresente As Boolean
        ''' <summary>PRKR\Perk\Perk  -&gt;  PERK. Referencia en el espacio del orden de carga.</summary>
        Property Perk As UInteger
        ''' <summary>El record trae PRKR\Perk\Rank. Distinto de que el campo valga cero.</summary>
        Property PerkRankPresente As Boolean
        ''' <summary>PRKR\Perk\Rank</summary>
        Property PerkRank As Byte
    End Interface

    ''' <summary>Un elemento de Items, en lo que los dos juegos comparten.</summary>
    Public Interface INpc_Items
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Item\CNTO\Item\Item. Distinto de que el campo valga cero.</summary>
        Property ItemPresente As Boolean
        ''' <summary>Item\CNTO\Item\Item. Referencia en el espacio del orden de carga.</summary>
        Property Item As UInteger
        ''' <summary>El record trae Item\CNTO\Item\Count. Distinto de que el campo valga cero.</summary>
        Property ItemCountPresente As Boolean
        ''' <summary>Item\CNTO\Item\Count</summary>
        Property ItemCount As Integer
        ''' <summary>El record trae Item\COED\Extra Data\Owner. Distinto de que el campo valga cero.</summary>
        Property ExtraDataOwnerPresente As Boolean
        ''' <summary>Item\COED\Extra Data\Owner  -&gt;  NPC_ / FACT / NULL. Referencia en el espacio del orden de carga.</summary>
        Property ExtraDataOwner As UInteger
        ''' <summary>El record trae Item\COED\Extra Data\Global Variable / Required Rank\Global Variable. Distinto de que el campo valga cero.</summary>
        Property GlobalVariableRequiredRankGlobalVariablePresente As Boolean
        ''' <summary>Item\COED\Extra Data\Global Variable / Required Rank\Global Variable  -&gt;  GLOB / NULL. Referencia en el espacio del orden de carga.</summary>
        Property GlobalVariableRequiredRankGlobalVariable As UInteger
        ''' <summary>El record trae Item\COED\Extra Data\Global Variable / Required Rank\Required Rank. Distinto de que el campo valga cero.</summary>
        Property GlobalVariableRequiredRankRequiredRankPresente As Boolean
        ''' <summary>Item\COED\Extra Data\Global Variable / Required Rank\Required Rank</summary>
        Property GlobalVariableRequiredRankRequiredRank As Integer
        ''' <summary>El record trae Item\COED\Extra Data\Item Condition. Distinto de que el campo valga cero.</summary>
        Property ExtraDataItemConditionPresente As Boolean
        ''' <summary>Item\COED\Extra Data\Item Condition</summary>
        Property ExtraDataItemCondition As Single
    End Interface

    ''' <summary>Un elemento de Packages, en lo que los dos juegos comparten.</summary>
    Public Interface INpc_Packages
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae PKID\Package. Distinto de que el campo valga cero.</summary>
        Property PackagePresente As Boolean
        ''' <summary>PKID\Package  -&gt;  PACK. Referencia en el espacio del orden de carga.</summary>
        Property Package As UInteger
    End Interface

    ''' <summary>Un elemento de Keywords\KWDA\Keywords, en lo que los dos juegos comparten.</summary>
    Public Interface INpc_Keywords
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Keyword. Distinto de que el campo valga cero.</summary>
        Property KeywordPresente As Boolean
        ''' <summary>Keyword  -&gt;  KYWD / NULL. Referencia en el espacio del orden de carga.</summary>
        Property Keyword As UInteger
    End Interface

    ''' <summary>Un elemento de Head Parts, en lo que los dos juegos comparten.</summary>
    Public Interface INpc_HeadParts
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae PNAM\Head Part. Distinto de que el campo valga cero.</summary>
        Property HeadPartPresente As Boolean
        ''' <summary>PNAM\Head Part  -&gt;  HDPT. Referencia en el espacio del orden de carga.</summary>
        Property HeadPart As UInteger
    End Interface

    ''' <summary>Campos de un record OMOD que los dos juegos declaran igual.
    ''' <para>El codigo que solo usa estos no necesita saber de que juego se trata. El
    ''' que necesita algo propio de un juego tiene que nombrar la clase de ese juego, y
    ''' asi no puede leer sin querer un campo que alla significa otra cosa.</para></summary>
    Public Interface IOmod
        ReadOnly Property Node As WbNode
        ''' <summary>Identificador del record.</summary>
        ReadOnly Property FormID As UInteger
        ''' <summary>Bit 4 de las banderas de la cabecera del record: Legendary Mod.</summary>
        Property LegendaryMod As Boolean
        ''' <summary>Bit 7 de las banderas de la cabecera del record: Mod Collection.</summary>
        Property ModCollection As Boolean
        ''' <summary>El record trae EDID\Editor ID. Distinto de que el campo valga cero.</summary>
        Property EditorIDPresente As Boolean
        ''' <summary>EDID\Editor ID</summary>
        Property EditorID As String
        ''' <summary>El record trae FULL\Name. Distinto de que el campo valga cero.</summary>
        Property NamePresente As Boolean
        ''' <summary>FULL\Name. Resuelto contra las tablas de texto si el archivo las usa.</summary>
        Property Name As String
        ''' <summary>El record trae DESC\Description. Distinto de que el campo valga cero.</summary>
        Property DescriptionPresente As Boolean
        ''' <summary>DESC\Description. Resuelto contra las tablas de texto si el archivo las usa.</summary>
        Property Description As String
        ''' <summary>El record trae Model\MODL\Model FileName. Distinto de que el campo valga cero.</summary>
        Property ModelFileNamePresente As Boolean
        ''' <summary>Model\MODL\Model FileName</summary>
        Property ModelFileName As String
        ''' <summary>El record trae Model\MODT\Model Information\ERROR. Distinto de que el campo valga cero.</summary>
        Property ModelInformationERRORPresente As Boolean
        ''' <summary>Model\MODT\Model Information\ERROR</summary>
        Property ModelInformationERROR As Byte()
        ''' <summary>El record trae Model\MODC\Color Remapping Index. Distinto de que el campo valga cero.</summary>
        Property ModelColorRemappingIndexPresente As Boolean
        ''' <summary>Model\MODC\Color Remapping Index</summary>
        Property ModelColorRemappingIndex As Single
        ''' <summary>El record trae Model\MODS\Material Swap. Distinto de que el campo valga cero.</summary>
        Property ModelMaterialSwapPresente As Boolean
        ''' <summary>Model\MODS\Material Swap  -&gt;  MSWP. Referencia en el espacio del orden de carga.</summary>
        Property ModelMaterialSwap As UInteger
        ''' <summary>El record trae Model\MODF\Flags. Distinto de que el campo valga cero.</summary>
        Property ModelFlagsPresente As Boolean
        ''' <summary>Model\MODF\Flags</summary>
        Property ModelFlags As Byte
        ''' <summary>Bit 0 de Model\MODF\Flags: Has FaceBones Model</summary>
        Property ModelFlagsHasFaceBonesModel As Boolean
        ''' <summary>Bit 1 de Model\MODF\Flags: Has 1stPerson Model</summary>
        Property ModelFlagsHas1stPersonModel As Boolean
        ''' <summary>El record trae DATA\Data\Include Count. Distinto de que el campo valga cero.</summary>
        Property DataIncludeCountPresente As Boolean
        ''' <summary>DATA\Data\Include Count</summary>
        Property DataIncludeCount As UInteger
        ''' <summary>El record trae DATA\Data\Property Count. Distinto de que el campo valga cero.</summary>
        Property DataPropertyCountPresente As Boolean
        ''' <summary>DATA\Data\Property Count</summary>
        Property DataPropertyCount As UInteger
        ''' <summary>El record trae DATA\Data\Unknown Bool 1. Distinto de que el campo valga cero.</summary>
        Property DataUnknownBool1Presente As Boolean
        ''' <summary>DATA\Data\Unknown Bool 1. Ponerlo en verdadero conserva el valor previo si no era cero.</summary>
        Property DataUnknownBool1 As Boolean
        ''' <summary>El record trae DATA\Data\Unknown Bool 2. Distinto de que el campo valga cero.</summary>
        Property DataUnknownBool2Presente As Boolean
        ''' <summary>DATA\Data\Unknown Bool 2. Ponerlo en verdadero conserva el valor previo si no era cero.</summary>
        Property DataUnknownBool2 As Boolean
        ''' <summary>El record trae DATA\Data\Form Type. Distinto de que el campo valga cero.</summary>
        Property DataFormTypePresente As Boolean
        ''' <summary>DATA\Data\Form Type</summary>
        Property DataFormType As UInteger
        ''' <summary>El record trae DATA\Data\Max Rank. Distinto de que el campo valga cero.</summary>
        Property DataMaxRankPresente As Boolean
        ''' <summary>DATA\Data\Max Rank</summary>
        Property DataMaxRank As Byte
        ''' <summary>El record trae DATA\Data\Level Tier Scaled Offset. Distinto de que el campo valga cero.</summary>
        Property DataLevelTierScaledOffsetPresente As Boolean
        ''' <summary>DATA\Data\Level Tier Scaled Offset</summary>
        Property DataLevelTierScaledOffset As Byte
        ''' <summary>El record trae DATA\Data\Attach Point. Distinto de que el campo valga cero.</summary>
        Property DataAttachPointPresente As Boolean
        ''' <summary>DATA\Data\Attach Point  -&gt;  KYWD / NULL. Referencia en el espacio del orden de carga.</summary>
        Property DataAttachPoint As UInteger
        ''' <summary>El record trae LNAM\Loose Mod. Distinto de que el campo valga cero.</summary>
        Property LooseModPresente As Boolean
        ''' <summary>LNAM\Loose Mod. Referencia en el espacio del orden de carga.</summary>
        Property LooseMod As UInteger
        ''' <summary>El record trae NAM1\Priority. Distinto de que el campo valga cero.</summary>
        Property PriorityPresente As Boolean
        ''' <summary>NAM1\Priority</summary>
        Property Priority As Byte
        ''' <summary>El record trae FLTR\Filter. Distinto de que el campo valga cero.</summary>
        Property FilterPresente As Boolean
        ''' <summary>FLTR\Filter</summary>
        Property Filter As String
        ''' <summary>Model\MODT\Model Information\Textures</summary>
        ReadOnly Property Textures As IReadOnlyList(Of IOmod_Textures)
        Function AgregarTextures() As IOmod_Textures
        Function QuitarTextures(indice As Integer) As Boolean
        Function ReordenarTextures(permutacion As IList(Of Integer)) As Boolean
        Function QuitarTextures(elemento As IOmod_Textures) As Boolean
        ''' <summary>Model\MODT\Model Information\Counters</summary>
        ReadOnly Property Counters As IReadOnlyList(Of IOmod_Counters)
        Function AgregarCounters() As IOmod_Counters
        Function QuitarCounters(indice As Integer) As Boolean
        Function ReordenarCounters(permutacion As IList(Of Integer)) As Boolean
        Function QuitarCounters(elemento As IOmod_Counters) As Boolean
        ''' <summary>Model\MODT\Model Information\Addon Nodes</summary>
        ReadOnly Property AddonNodes As IReadOnlyList(Of IOmod_AddonNodes)
        Function AgregarAddonNodes() As IOmod_AddonNodes
        Function QuitarAddonNodes(indice As Integer) As Boolean
        Function ReordenarAddonNodes(permutacion As IList(Of Integer)) As Boolean
        Function QuitarAddonNodes(elemento As IOmod_AddonNodes) As Boolean
        ''' <summary>Model\MODT\Model Information\Materials</summary>
        ReadOnly Property Materials As IReadOnlyList(Of IOmod_Materials)
        Function AgregarMaterials() As IOmod_Materials
        Function QuitarMaterials(indice As Integer) As Boolean
        Function ReordenarMaterials(permutacion As IList(Of Integer)) As Boolean
        Function QuitarMaterials(elemento As IOmod_Materials) As Boolean
        ''' <summary>DATA\Data\Attach Parent Slots</summary>
        ReadOnly Property AttachParentSlots As IReadOnlyList(Of IOmod_AttachParentSlots)
        Function AgregarAttachParentSlots() As IOmod_AttachParentSlots
        Function QuitarAttachParentSlots(indice As Integer) As Boolean
        Function ReordenarAttachParentSlots(permutacion As IList(Of Integer)) As Boolean
        Function QuitarAttachParentSlots(elemento As IOmod_AttachParentSlots) As Boolean
        ''' <summary>DATA\Data\Items</summary>
        ReadOnly Property Items As IReadOnlyList(Of IOmod_Items)
        Function AgregarItems() As IOmod_Items
        Function QuitarItems(indice As Integer) As Boolean
        Function ReordenarItems(permutacion As IList(Of Integer)) As Boolean
        Function QuitarItems(elemento As IOmod_Items) As Boolean
        ''' <summary>DATA\Data\Includes</summary>
        ReadOnly Property Includes As IReadOnlyList(Of IOmod_Includes)
        Function AgregarIncludes() As IOmod_Includes
        Function QuitarIncludes(indice As Integer) As Boolean
        Function ReordenarIncludes(permutacion As IList(Of Integer)) As Boolean
        Function QuitarIncludes(elemento As IOmod_Includes) As Boolean
        ''' <summary>DATA\Data\Properties</summary>
        ReadOnly Property Properties As IReadOnlyList(Of IOmod_Properties)
        Function AgregarProperties() As IOmod_Properties
        Function QuitarProperties(indice As Integer) As Boolean
        Function ReordenarProperties(permutacion As IList(Of Integer)) As Boolean
        Function QuitarProperties(elemento As IOmod_Properties) As Boolean
        ''' <summary>MNAM\Target OMOD Keywords</summary>
        ReadOnly Property TargetOMODKeywords As IReadOnlyList(Of IOmod_TargetOMODKeywords)
        Function AgregarTargetOMODKeywords() As IOmod_TargetOMODKeywords
        Function QuitarTargetOMODKeywords(indice As Integer) As Boolean
        Function ReordenarTargetOMODKeywords(permutacion As IList(Of Integer)) As Boolean
        Function QuitarTargetOMODKeywords(elemento As IOmod_TargetOMODKeywords) As Boolean
        ''' <summary>FNAM\Filter Keywords</summary>
        ReadOnly Property FilterKeywords As IReadOnlyList(Of IOmod_FilterKeywords)
        Function AgregarFilterKeywords() As IOmod_FilterKeywords
        Function QuitarFilterKeywords(indice As Integer) As Boolean
        Function ReordenarFilterKeywords(permutacion As IList(Of Integer)) As Boolean
        Function QuitarFilterKeywords(elemento As IOmod_FilterKeywords) As Boolean
    End Interface

    ''' <summary>Un elemento de Model\MODT\Model Information\Textures, en lo que los dos juegos comparten.</summary>
    Public Interface IOmod_Textures
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Texture\File Hash. Distinto de que el campo valga cero.</summary>
        Property TextureFileHashPresente As Boolean
        ''' <summary>Texture\File Hash</summary>
        Property TextureFileHash As UInteger
        ''' <summary>El record trae Texture\Extension. Distinto de que el campo valga cero.</summary>
        Property TextureExtensionPresente As Boolean
        ''' <summary>Texture\Extension</summary>
        Property TextureExtension As String
        ''' <summary>El record trae Texture\Folder Hash. Distinto de que el campo valga cero.</summary>
        Property TextureFolderHashPresente As Boolean
        ''' <summary>Texture\Folder Hash</summary>
        Property TextureFolderHash As UInteger
    End Interface

    ''' <summary>Un elemento de Model\MODT\Model Information\Counters, en lo que los dos juegos comparten.</summary>
    Public Interface IOmod_Counters
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Counter. Distinto de que el campo valga cero.</summary>
        Property CounterPresente As Boolean
        ''' <summary>Counter</summary>
        Property Counter As UInteger
    End Interface

    ''' <summary>Un elemento de Model\MODT\Model Information\Addon Nodes, en lo que los dos juegos comparten.</summary>
    Public Interface IOmod_AddonNodes
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Addon Node. Distinto de que el campo valga cero.</summary>
        Property AddonNodePresente As Boolean
        ''' <summary>Addon Node</summary>
        Property AddonNode As UInteger
    End Interface

    ''' <summary>Un elemento de Model\MODT\Model Information\Materials, en lo que los dos juegos comparten.</summary>
    Public Interface IOmod_Materials
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Material\File Hash. Distinto de que el campo valga cero.</summary>
        Property MaterialFileHashPresente As Boolean
        ''' <summary>Material\File Hash</summary>
        Property MaterialFileHash As UInteger
        ''' <summary>El record trae Material\Extension. Distinto de que el campo valga cero.</summary>
        Property MaterialExtensionPresente As Boolean
        ''' <summary>Material\Extension</summary>
        Property MaterialExtension As String
        ''' <summary>El record trae Material\Folder Hash. Distinto de que el campo valga cero.</summary>
        Property MaterialFolderHashPresente As Boolean
        ''' <summary>Material\Folder Hash</summary>
        Property MaterialFolderHash As UInteger
    End Interface

    ''' <summary>Un elemento de DATA\Data\Attach Parent Slots, en lo que los dos juegos comparten.</summary>
    Public Interface IOmod_AttachParentSlots
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Keyword. Distinto de que el campo valga cero.</summary>
        Property KeywordPresente As Boolean
        ''' <summary>Keyword  -&gt;  KYWD / NULL. Referencia en el espacio del orden de carga.</summary>
        Property Keyword As UInteger
    End Interface

    ''' <summary>Un elemento de DATA\Data\Items, en lo que los dos juegos comparten.</summary>
    Public Interface IOmod_Items
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Item\Value 1. Distinto de que el campo valga cero.</summary>
        Property ItemValue1Presente As Boolean
        ''' <summary>Item\Value 1</summary>
        Property ItemValue1 As Byte()
        ''' <summary>El record trae Item\Value 2. Distinto de que el campo valga cero.</summary>
        Property ItemValue2Presente As Boolean
        ''' <summary>Item\Value 2</summary>
        Property ItemValue2 As Byte()
    End Interface

    ''' <summary>Un elemento de DATA\Data\Includes, en lo que los dos juegos comparten.</summary>
    Public Interface IOmod_Includes
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Include\Mod. Distinto de que el campo valga cero.</summary>
        Property IncludeModPresente As Boolean
        ''' <summary>Include\Mod  -&gt;  OMOD. Referencia en el espacio del orden de carga.</summary>
        Property IncludeMod As UInteger
        ''' <summary>El record trae Include\Minimum Level. Distinto de que el campo valga cero.</summary>
        Property IncludeMinimumLevelPresente As Boolean
        ''' <summary>Include\Minimum Level</summary>
        Property IncludeMinimumLevel As Byte
        ''' <summary>El record trae Include\Optional. Distinto de que el campo valga cero.</summary>
        Property IncludeOptionalPresente As Boolean
        ''' <summary>Include\Optional. Ponerlo en verdadero conserva el valor previo si no era cero.</summary>
        Property IncludeOptional As Boolean
        ''' <summary>El record trae Include\Don't Use All. Distinto de que el campo valga cero.</summary>
        Property IncludeDonTUseAllPresente As Boolean
        ''' <summary>Include\Don't Use All. Ponerlo en verdadero conserva el valor previo si no era cero.</summary>
        Property IncludeDonTUseAll As Boolean
    End Interface

    ''' <summary>Un elemento de DATA\Data\Properties, en lo que los dos juegos comparten.</summary>
    Public Interface IOmod_Properties
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Property\Value Type. Distinto de que el campo valga cero.</summary>
        Property PropertyValueTypePresente As Boolean
        ''' <summary>Property\Value Type</summary>
        Property PropertyValueType As Byte
        ''' <summary>Nombre del valor de Property\Value Type.</summary>
        ReadOnly Property PropertyValueTypeNombre As String
        ''' <summary>El record trae Property\Function Type\Function Type. Distinto de que el campo valga cero.</summary>
        Property PropertyFunctionTypePresente As Boolean
        ''' <summary>Property\Function Type\Function Type</summary>
        Property PropertyFunctionType As Byte
        ''' <summary>Nombre del valor de Property\Function Type\Function Type.</summary>
        ReadOnly Property PropertyFunctionTypeNombre As String
        ''' <summary>El record trae Property\Property. Distinto de que el campo valga cero.</summary>
        Property PropertyPresente As Boolean
        ''' <summary>Property\Property</summary>
        Property [Property] As UShort
        ''' <summary>El record trae Property\Value 1\Value 1 - Unknown. Distinto de que el campo valga cero.</summary>
        Property PropertyValue1UnknownPresente As Boolean
        ''' <summary>Property\Value 1\Value 1 - Unknown</summary>
        Property PropertyValue1Unknown As Byte()
        ''' <summary>El record trae Property\Value 1\Value 1 - Int. Distinto de que el campo valga cero.</summary>
        Property PropertyValue1IntPresente As Boolean
        ''' <summary>Property\Value 1\Value 1 - Int</summary>
        Property PropertyValue1Int As UInteger
        ''' <summary>El record trae Property\Value 1\Value 1 - Float. Distinto de que el campo valga cero.</summary>
        Property PropertyValue1FloatPresente As Boolean
        ''' <summary>Property\Value 1\Value 1 - Float</summary>
        Property PropertyValue1Float As Single
        ''' <summary>El record trae Property\Value 1\Value 1 - Bool. Distinto de que el campo valga cero.</summary>
        Property PropertyValue1BoolPresente As Boolean
        ''' <summary>Property\Value 1\Value 1 - Bool. Ponerlo en verdadero conserva el valor previo si no era cero.</summary>
        Property PropertyValue1Bool As Boolean
        ''' <summary>El record trae Property\Value 1\Value 1 - FormID. Distinto de que el campo valga cero.</summary>
        Property PropertyValue1FormIDPresente As Boolean
        ''' <summary>Property\Value 1\Value 1 - FormID. Referencia en el espacio del orden de carga.</summary>
        Property PropertyValue1FormID As UInteger
        ''' <summary>El record trae Property\Value 1\Value 1 - Enum. Distinto de que el campo valga cero.</summary>
        Property PropertyValue1EnumPresente As Boolean
        ''' <summary>Property\Value 1\Value 1 - Enum</summary>
        Property PropertyValue1Enum As UInteger
        ''' <summary>El record trae Property\Value 1\Sound Level. Distinto de que el campo valga cero.</summary>
        Property Value1SoundLevelPresente As Boolean
        ''' <summary>Property\Value 1\Sound Level</summary>
        Property Value1SoundLevel As UInteger
        ''' <summary>Nombre del valor de Property\Value 1\Sound Level.</summary>
        ReadOnly Property Value1SoundLevelNombre As String
        ''' <summary>El record trae Property\Value 1\Stagger Value. Distinto de que el campo valga cero.</summary>
        Property Value1StaggerValuePresente As Boolean
        ''' <summary>Property\Value 1\Stagger Value</summary>
        Property Value1StaggerValue As UInteger
        ''' <summary>Nombre del valor de Property\Value 1\Stagger Value.</summary>
        ReadOnly Property Value1StaggerValueNombre As String
        ''' <summary>El record trae Property\Value 1\Hit Behaviour. Distinto de que el campo valga cero.</summary>
        Property Value1HitBehaviourPresente As Boolean
        ''' <summary>Property\Value 1\Hit Behaviour</summary>
        Property Value1HitBehaviour As UInteger
        ''' <summary>Nombre del valor de Property\Value 1\Hit Behaviour.</summary>
        ReadOnly Property Value1HitBehaviourNombre As String
        ''' <summary>El record trae Property\Value 2\Value 2 - Int. Distinto de que el campo valga cero.</summary>
        Property PropertyValue2IntPresente As Boolean
        ''' <summary>Property\Value 2\Value 2 - Int</summary>
        Property PropertyValue2Int As UInteger
        ''' <summary>El record trae Property\Value 2\Value 2 - Float. Distinto de que el campo valga cero.</summary>
        Property PropertyValue2FloatPresente As Boolean
        ''' <summary>Property\Value 2\Value 2 - Float</summary>
        Property PropertyValue2Float As Single
        ''' <summary>El record trae Property\Value 2\Value 2 - Bool. Distinto de que el campo valga cero.</summary>
        Property PropertyValue2BoolPresente As Boolean
        ''' <summary>Property\Value 2\Value 2 - Bool. Ponerlo en verdadero conserva el valor previo si no era cero.</summary>
        Property PropertyValue2Bool As Boolean
        ''' <summary>El record trae Property\Step. Distinto de que el campo valga cero.</summary>
        Property PropertyStepPresente As Boolean
        ''' <summary>Property\Step</summary>
        Property PropertyStep As Single
    End Interface

    ''' <summary>Un elemento de MNAM\Target OMOD Keywords, en lo que los dos juegos comparten.</summary>
    Public Interface IOmod_TargetOMODKeywords
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Keyword. Distinto de que el campo valga cero.</summary>
        Property KeywordPresente As Boolean
        ''' <summary>Keyword  -&gt;  KYWD. Referencia en el espacio del orden de carga.</summary>
        Property Keyword As UInteger
    End Interface

    ''' <summary>Un elemento de FNAM\Filter Keywords, en lo que los dos juegos comparten.</summary>
    Public Interface IOmod_FilterKeywords
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Keyword. Distinto de que el campo valga cero.</summary>
        Property KeywordPresente As Boolean
        ''' <summary>Keyword  -&gt;  KYWD. Referencia en el espacio del orden de carga.</summary>
        Property Keyword As UInteger
    End Interface

    ''' <summary>Campos de un record OTFT que los dos juegos declaran igual.
    ''' <para>El codigo que solo usa estos no necesita saber de que juego se trata. El
    ''' que necesita algo propio de un juego tiene que nombrar la clase de ese juego, y
    ''' asi no puede leer sin querer un campo que alla significa otra cosa.</para></summary>
    Public Interface IOtft
        ReadOnly Property Node As WbNode
        ''' <summary>Identificador del record.</summary>
        ReadOnly Property FormID As UInteger
        ''' <summary>El record trae EDID\Editor ID. Distinto de que el campo valga cero.</summary>
        Property EditorIDPresente As Boolean
        ''' <summary>EDID\Editor ID</summary>
        Property EditorID As String
        ''' <summary>INAM\Items</summary>
        ReadOnly Property Items As IReadOnlyList(Of IOtft_Items)
        Function AgregarItems() As IOtft_Items
        Function QuitarItems(indice As Integer) As Boolean
        Function ReordenarItems(permutacion As IList(Of Integer)) As Boolean
        Function QuitarItems(elemento As IOtft_Items) As Boolean
    End Interface

    ''' <summary>Un elemento de INAM\Items, en lo que los dos juegos comparten.</summary>
    Public Interface IOtft_Items
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Item. Distinto de que el campo valga cero.</summary>
        Property ItemPresente As Boolean
        ''' <summary>Item  -&gt;  ARMO / LVLI. Referencia en el espacio del orden de carga.</summary>
        Property Item As UInteger
    End Interface

    ''' <summary>Campos de un record QUST que los dos juegos declaran igual.
    ''' <para>El codigo que solo usa estos no necesita saber de que juego se trata. El
    ''' que necesita algo propio de un juego tiene que nombrar la clase de ese juego, y
    ''' asi no puede leer sin querer un campo que alla significa otra cosa.</para></summary>
    Public Interface IQust
        ReadOnly Property Node As WbNode
        ''' <summary>Identificador del record.</summary>
        ReadOnly Property FormID As UInteger
        ''' <summary>El record trae EDID\Editor ID. Distinto de que el campo valga cero.</summary>
        Property EditorIDPresente As Boolean
        ''' <summary>EDID\Editor ID</summary>
        Property EditorID As String
        ''' <summary>El record trae VMAD\Virtual Machine Adapter\Version. Distinto de que el campo valga cero.</summary>
        Property VirtualMachineAdapterVersionPresente As Boolean
        ''' <summary>VMAD\Virtual Machine Adapter\Version</summary>
        Property VirtualMachineAdapterVersion As Short
        ''' <summary>El record trae VMAD\Virtual Machine Adapter\Object Format. Distinto de que el campo valga cero.</summary>
        Property VirtualMachineAdapterObjectFormatPresente As Boolean
        ''' <summary>VMAD\Virtual Machine Adapter\Object Format</summary>
        Property VirtualMachineAdapterObjectFormat As Short
        ''' <summary>El record trae VMAD\Virtual Machine Adapter\Script Fragments\Extra bind data version. Distinto de que el campo valga cero.</summary>
        Property ScriptFragmentsExtraBindDataVersionPresente As Boolean
        ''' <summary>VMAD\Virtual Machine Adapter\Script Fragments\Extra bind data version</summary>
        Property ScriptFragmentsExtraBindDataVersion As SByte
        ''' <summary>El record trae VMAD\Virtual Machine Adapter\Script Fragments\FragmentCount. Distinto de que el campo valga cero.</summary>
        Property ScriptFragmentsFragmentCountPresente As Boolean
        ''' <summary>VMAD\Virtual Machine Adapter\Script Fragments\FragmentCount</summary>
        Property ScriptFragmentsFragmentCount As UShort
        ''' <summary>El record trae FULL\Name. Distinto de que el campo valga cero.</summary>
        Property NamePresente As Boolean
        ''' <summary>FULL\Name. Resuelto contra las tablas de texto si el archivo las usa.</summary>
        Property Name As String
        ''' <summary>El record trae DNAM\General\Flags. Distinto de que el campo valga cero.</summary>
        Property GeneralFlagsPresente As Boolean
        ''' <summary>DNAM\General\Flags</summary>
        Property GeneralFlags As UShort
        ''' <summary>Bit 0 de DNAM\General\Flags: Start Game Enabled</summary>
        Property GeneralFlagsStartGameEnabled As Boolean
        ''' <summary>Bit 1 de DNAM\General\Flags: Completed</summary>
        Property GeneralFlagsCompleted As Boolean
        ''' <summary>Bit 2 de DNAM\General\Flags: Add Idle topic to Hello</summary>
        Property GeneralFlagsAddIdleTopicToHello As Boolean
        ''' <summary>Bit 3 de DNAM\General\Flags: Allow repeated stages</summary>
        Property GeneralFlagsAllowRepeatedStages As Boolean
        ''' <summary>Bit 4 de DNAM\General\Flags: Starts Enabled</summary>
        Property GeneralFlagsStartsEnabled As Boolean
        ''' <summary>Bit 5 de DNAM\General\Flags: Displayed In HUD</summary>
        Property GeneralFlagsDisplayedInHUD As Boolean
        ''' <summary>Bit 6 de DNAM\General\Flags: Failed</summary>
        Property GeneralFlagsFailed As Boolean
        ''' <summary>Bit 7 de DNAM\General\Flags: Stage Wait</summary>
        Property GeneralFlagsStageWait As Boolean
        ''' <summary>Bit 8 de DNAM\General\Flags: Run Once</summary>
        Property GeneralFlagsRunOnce As Boolean
        ''' <summary>Bit 9 de DNAM\General\Flags: Exclude from dialogue export</summary>
        Property GeneralFlagsExcludeFromDialogueExport As Boolean
        ''' <summary>Bit 10 de DNAM\General\Flags: Warn on alias fill failure</summary>
        Property GeneralFlagsWarnOnAliasFillFailure As Boolean
        ''' <summary>Bit 11 de DNAM\General\Flags: Active</summary>
        Property GeneralFlagsActive As Boolean
        ''' <summary>Bit 12 de DNAM\General\Flags: Repeats Conditions</summary>
        Property GeneralFlagsRepeatsConditions As Boolean
        ''' <summary>Bit 13 de DNAM\General\Flags: Keep Instance</summary>
        Property GeneralFlagsKeepInstance As Boolean
        ''' <summary>Bit 14 de DNAM\General\Flags: Want Dormant</summary>
        Property GeneralFlagsWantDormant As Boolean
        ''' <summary>Bit 15 de DNAM\General\Flags: Has Dialogue Data</summary>
        Property GeneralFlagsHasDialogueData As Boolean
        ''' <summary>El record trae DNAM\General\Priority. Distinto de que el campo valga cero.</summary>
        Property GeneralPriorityPresente As Boolean
        ''' <summary>DNAM\General\Priority</summary>
        Property GeneralPriority As Byte
        ''' <summary>El record trae DNAM\General\Type. Distinto de que el campo valga cero.</summary>
        Property GeneralTypePresente As Boolean
        ''' <summary>Nombre del valor de DNAM\General\Type.</summary>
        ReadOnly Property GeneralTypeNombre As String
        ''' <summary>El record trae ENAM\Event. Distinto de que el campo valga cero.</summary>
        Property EventPresente As Boolean
        ''' <summary>ENAM\Event</summary>
        Property [Event] As UInteger
        ''' <summary>El record trae el marcador NEXT\Marker. No lleva valor: el dato es que esté, así que ponerlo en verdadero lo crea y en falso lo saca.</summary>
        Property Marker As Boolean
        ''' <summary>El record trae ANAM\Next Alias ID. Distinto de que el campo valga cero.</summary>
        Property NextAliasIDPresente As Boolean
        ''' <summary>ANAM\Next Alias ID</summary>
        Property NextAliasID As UInteger
        ''' <summary>El record trae NNAM\Description. Distinto de que el campo valga cero.</summary>
        Property DescriptionPresente As Boolean
        ''' <summary>NNAM\Description</summary>
        Property Description As String
        ''' <summary>VMAD\Virtual Machine Adapter\Scripts</summary>
        ReadOnly Property Scripts As IReadOnlyList(Of IQust_Scripts)
        Function AgregarScripts() As IQust_Scripts
        Function QuitarScripts(indice As Integer) As Boolean
        Function ReordenarScripts(permutacion As IList(Of Integer)) As Boolean
        Function QuitarScripts(elemento As IQust_Scripts) As Boolean
        ''' <summary>VMAD\Virtual Machine Adapter\Script Fragments\Fragments</summary>
        ReadOnly Property Fragments As IReadOnlyList(Of IQust_Fragments)
        Function AgregarFragments() As IQust_Fragments
        Function QuitarFragments(indice As Integer) As Boolean
        Function ReordenarFragments(permutacion As IList(Of Integer)) As Boolean
        Function QuitarFragments(elemento As IQust_Fragments) As Boolean
        ''' <summary>VMAD\Virtual Machine Adapter\Aliases</summary>
        ReadOnly Property Aliases As IReadOnlyList(Of IQust_Aliases)
        Function AgregarAliases() As IQust_Aliases
        Function QuitarAliases(indice As Integer) As Boolean
        Function ReordenarAliases(permutacion As IList(Of Integer)) As Boolean
        Function QuitarAliases(elemento As IQust_Aliases) As Boolean
        ''' <summary>Text Display Globals</summary>
        ReadOnly Property TextDisplayGlobals As IReadOnlyList(Of IQust_TextDisplayGlobals)
        Function AgregarTextDisplayGlobals() As IQust_TextDisplayGlobals
        Function QuitarTextDisplayGlobals(indice As Integer) As Boolean
        Function ReordenarTextDisplayGlobals(permutacion As IList(Of Integer)) As Boolean
        Function QuitarTextDisplayGlobals(elemento As IQust_TextDisplayGlobals) As Boolean
        ''' <summary>Quest Dialogue Conditions\Conditions</summary>
        ReadOnly Property Conditions As IReadOnlyList(Of IQust_Conditions)
        Function AgregarConditions() As IQust_Conditions
        Function QuitarConditions(indice As Integer) As Boolean
        Function ReordenarConditions(permutacion As IList(Of Integer)) As Boolean
        Function QuitarConditions(elemento As IQust_Conditions) As Boolean
        ''' <summary>Story Manager Conditions\Conditions</summary>
        ReadOnly Property Conditions2 As IReadOnlyList(Of IQust_Conditions2)
        Function AgregarConditions2() As IQust_Conditions2
        Function QuitarConditions2(indice As Integer) As Boolean
        Function ReordenarConditions2(permutacion As IList(Of Integer)) As Boolean
        Function QuitarConditions2(elemento As IQust_Conditions2) As Boolean
        ''' <summary>Stages</summary>
        ReadOnly Property Stages As IReadOnlyList(Of IQust_Stages)
        Function AgregarStages() As IQust_Stages
        Function QuitarStages(indice As Integer) As Boolean
        Function ReordenarStages(permutacion As IList(Of Integer)) As Boolean
        Function QuitarStages(elemento As IQust_Stages) As Boolean
        ''' <summary>Objectives</summary>
        ReadOnly Property Objectives As IReadOnlyList(Of IQust_Objectives)
        Function AgregarObjectives() As IQust_Objectives
        Function QuitarObjectives(indice As Integer) As Boolean
        Function ReordenarObjectives(permutacion As IList(Of Integer)) As Boolean
        Function QuitarObjectives(elemento As IQust_Objectives) As Boolean
        ''' <summary>Aliases</summary>
        ReadOnly Property Aliases2 As IReadOnlyList(Of IQust_Aliases2)
        Function AgregarAliases2() As IQust_Aliases2
        Function QuitarAliases2(indice As Integer) As Boolean
        Function ReordenarAliases2(permutacion As IList(Of Integer)) As Boolean
        Function QuitarAliases2(elemento As IQust_Aliases2) As Boolean
    End Interface

    ''' <summary>Un elemento de VMAD\Virtual Machine Adapter\Scripts, en lo que los dos juegos comparten.</summary>
    Public Interface IQust_Scripts
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Script\ScriptName. Distinto de que el campo valga cero.</summary>
        Property ScriptNamePresente As Boolean
        ''' <summary>Script\ScriptName</summary>
        Property ScriptName As String
        ''' <summary>El record trae Script\Flags. Distinto de que el campo valga cero.</summary>
        Property ScriptFlagsPresente As Boolean
        ''' <summary>Script\Flags</summary>
        Property ScriptFlags As Byte
        ''' <summary>Nombre del valor de Script\Flags.</summary>
        ReadOnly Property ScriptFlagsNombre As String
    End Interface

    ''' <summary>Un elemento de VMAD\Virtual Machine Adapter\Script Fragments\Fragments, en lo que los dos juegos comparten.</summary>
    Public Interface IQust_Fragments
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Fragment\Quest Stage. Distinto de que el campo valga cero.</summary>
        Property FragmentQuestStagePresente As Boolean
        ''' <summary>Fragment\Quest Stage</summary>
        Property FragmentQuestStage As UShort
        ''' <summary>El record trae Fragment\Unknown. Distinto de que el campo valga cero.</summary>
        Property FragmentUnknownPresente As Boolean
        ''' <summary>Fragment\Unknown</summary>
        Property FragmentUnknown As Short
        ''' <summary>El record trae Fragment\Quest Stage Index. Distinto de que el campo valga cero.</summary>
        Property FragmentQuestStageIndexPresente As Boolean
        ''' <summary>Fragment\Quest Stage Index</summary>
        Property FragmentQuestStageIndex As Integer
        ''' <summary>El record trae Fragment\ScriptName. Distinto de que el campo valga cero.</summary>
        Property FragmentScriptNamePresente As Boolean
        ''' <summary>Fragment\ScriptName</summary>
        Property FragmentScriptName As String
        ''' <summary>El record trae Fragment\FragmentName. Distinto de que el campo valga cero.</summary>
        Property FragmentNamePresente As Boolean
        ''' <summary>Fragment\FragmentName</summary>
        Property FragmentName As String
    End Interface

    ''' <summary>Un elemento de VMAD\Virtual Machine Adapter\Aliases, en lo que los dos juegos comparten.</summary>
    Public Interface IQust_Aliases
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Alias\Object Union\Object v2\Alias. Distinto de que el campo valga cero.</summary>
        Property ObjectV2AliasPresente As Boolean
        ''' <summary>Alias\Object Union\Object v2\Alias</summary>
        Property ObjectV2Alias As Short
        ''' <summary>El record trae Alias\Object Union\Object v2\FormID. Distinto de que el campo valga cero.</summary>
        Property ObjectV2FormIDPresente As Boolean
        ''' <summary>Alias\Object Union\Object v2\FormID. Referencia en el espacio del orden de carga.</summary>
        Property ObjectV2FormID As UInteger
        ''' <summary>El record trae Alias\Object Union\Object v1\FormID. Distinto de que el campo valga cero.</summary>
        Property ObjectV1FormIDPresente As Boolean
        ''' <summary>Alias\Object Union\Object v1\FormID. Referencia en el espacio del orden de carga.</summary>
        Property ObjectV1FormID As UInteger
        ''' <summary>El record trae Alias\Object Union\Object v1\Alias. Distinto de que el campo valga cero.</summary>
        Property ObjectV1AliasPresente As Boolean
        ''' <summary>Alias\Object Union\Object v1\Alias</summary>
        Property ObjectV1Alias As Short
        ''' <summary>El record trae Alias\Version. Distinto de que el campo valga cero.</summary>
        Property AliasVersionPresente As Boolean
        ''' <summary>Alias\Version</summary>
        Property AliasVersion As Short
        ''' <summary>El record trae Alias\Object Format. Distinto de que el campo valga cero.</summary>
        Property AliasObjectFormatPresente As Boolean
        ''' <summary>Alias\Object Format</summary>
        Property AliasObjectFormat As Short
    End Interface

    ''' <summary>Un elemento de Text Display Globals, en lo que los dos juegos comparten.</summary>
    Public Interface IQust_TextDisplayGlobals
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae QTGL\Global. Distinto de que el campo valga cero.</summary>
        Property GlobalPresente As Boolean
        ''' <summary>QTGL\Global  -&gt;  GLOB. Referencia en el espacio del orden de carga.</summary>
        Property [Global] As UInteger
    End Interface

    ''' <summary>Un elemento de Quest Dialogue Conditions\Conditions, en lo que los dos juegos comparten.</summary>
    Public Interface IQust_Conditions
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Condition\CTDA\Type. Distinto de que el campo valga cero.</summary>
        Property ConditionTypePresente As Boolean
        ''' <summary>Condition\CTDA\Type</summary>
        Property ConditionType As Byte
        ''' <summary>El record trae Condition\CTDA\Comparison Value\Comparison Value - Float. Distinto de que el campo valga cero.</summary>
        Property ConditionComparisonValueFloatPresente As Boolean
        ''' <summary>Condition\CTDA\Comparison Value\Comparison Value - Float</summary>
        Property ConditionComparisonValueFloat As Single
        ''' <summary>El record trae Condition\CTDA\Comparison Value\Comparison Value - Global. Distinto de que el campo valga cero.</summary>
        Property ConditionComparisonValueGlobalPresente As Boolean
        ''' <summary>Condition\CTDA\Comparison Value\Comparison Value - Global  -&gt;  GLOB. Referencia en el espacio del orden de carga.</summary>
        Property ConditionComparisonValueGlobal As UInteger
        ''' <summary>El record trae Condition\CTDA\Function. Distinto de que el campo valga cero.</summary>
        Property ConditionFunctionPresente As Boolean
        ''' <summary>Condition\CTDA\Function</summary>
        Property ConditionFunction As UShort
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Unknown. Distinto de que el campo valga cero.</summary>
        Property Parameter1UnknownPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Unknown</summary>
        Property Parameter1Unknown As Byte()
        ''' <summary>El record trae Condition\CTDA\Parameter #1\None. Distinto de que el campo valga cero.</summary>
        Property Parameter1NonePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\None</summary>
        Property Parameter1None As Byte()
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Float. Distinto de que el campo valga cero.</summary>
        Property Parameter1FloatPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Float</summary>
        Property Parameter1Float As Single
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Integer. Distinto de que el campo valga cero.</summary>
        Property Parameter1IntegerPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Integer</summary>
        Property Parameter1Integer As Integer
        ''' <summary>El record trae Condition\CTDA\Parameter #1\String. Distinto de que el campo valga cero.</summary>
        Property Parameter1StringPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\String</summary>
        Property Parameter1String As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Alias. Distinto de que el campo valga cero.</summary>
        Property Parameter1AliasPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Alias</summary>
        Property Parameter1Alias As Integer
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Event. Distinto de que el campo valga cero.</summary>
        Property Parameter1EventPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Event</summary>
        Property Parameter1Event As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Packdata ID. Distinto de que el campo valga cero.</summary>
        Property Parameter1PackdataIDPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Packdata ID</summary>
        Property Parameter1PackdataID As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Quest Stage. Distinto de que el campo valga cero.</summary>
        Property Parameter1QuestStagePresente As Boolean
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Alignment. Distinto de que el campo valga cero.</summary>
        Property Parameter1AlignmentPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Alignment</summary>
        Property Parameter1Alignment As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #1\Alignment.</summary>
        ReadOnly Property Parameter1AlignmentNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Axis. Distinto de que el campo valga cero.</summary>
        Property Parameter1AxisPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Axis</summary>
        Property Parameter1Axis As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #1\Axis.</summary>
        ReadOnly Property Parameter1AxisNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Crime Type. Distinto de que el campo valga cero.</summary>
        Property Parameter1CrimeTypePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Crime Type</summary>
        Property Parameter1CrimeType As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #1\Crime Type.</summary>
        ReadOnly Property Parameter1CrimeTypeNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Critical Stage. Distinto de que el campo valga cero.</summary>
        Property Parameter1CriticalStagePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Critical Stage</summary>
        Property Parameter1CriticalStage As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #1\Critical Stage.</summary>
        ReadOnly Property Parameter1CriticalStageNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Form Type. Distinto de que el campo valga cero.</summary>
        Property Parameter1FormTypePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Form Type</summary>
        Property Parameter1FormType As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #1\Form Type.</summary>
        ReadOnly Property Parameter1FormTypeNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Misc Stat. Distinto de que el campo valga cero.</summary>
        Property Parameter1MiscStatPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Misc Stat</summary>
        Property Parameter1MiscStat As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #1\Misc Stat.</summary>
        ReadOnly Property Parameter1MiscStatNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Sex. Distinto de que el campo valga cero.</summary>
        Property Parameter1SexPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Sex</summary>
        Property Parameter1Sex As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #1\Sex.</summary>
        ReadOnly Property Parameter1SexNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Ward State. Distinto de que el campo valga cero.</summary>
        Property Parameter1WardStatePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Ward State</summary>
        Property Parameter1WardState As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #1\Ward State.</summary>
        ReadOnly Property Parameter1WardStateNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Actor. Distinto de que el campo valga cero.</summary>
        Property Parameter1ActorPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Actor  -&gt;  ACHR / PLYR / REFR / TRGT. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Actor As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Actor Base. Distinto de que el campo valga cero.</summary>
        Property Parameter1ActorBasePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Actor Base  -&gt;  NPC_. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1ActorBase As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Actor Value. Distinto de que el campo valga cero.</summary>
        Property Parameter1ActorValuePresente As Boolean
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Association Type. Distinto de que el campo valga cero.</summary>
        Property Parameter1AssociationTypePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Association Type  -&gt;  ASTP. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1AssociationType As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Base Object. Distinto de que el campo valga cero.</summary>
        Property Parameter1BaseObjectPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Base Object. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1BaseObject As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Cell. Distinto de que el campo valga cero.</summary>
        Property Parameter1CellPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Cell  -&gt;  CELL. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Cell As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Class. Distinto de que el campo valga cero.</summary>
        Property Parameter1ClassPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Class  -&gt;  CLAS. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Class As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Effect Item. Distinto de que el campo valga cero.</summary>
        Property Parameter1EffectItemPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Effect Item  -&gt;  ALCH / ENCH / INGR / SCRL / SPEL. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1EffectItem As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Encounter Zone. Distinto de que el campo valga cero.</summary>
        Property Parameter1EncounterZonePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Encounter Zone  -&gt;  ECZN. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1EncounterZone As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Equip Type. Distinto de que el campo valga cero.</summary>
        Property Parameter1EquipTypePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Equip Type  -&gt;  EQUP. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1EquipType As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Event Data. Distinto de que el campo valga cero.</summary>
        Property Parameter1EventDataPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Event Data  -&gt;  FLST / KYWD / LCTN. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1EventData As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Faction. Distinto de que el campo valga cero.</summary>
        Property Parameter1FactionPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Faction  -&gt;  FACT. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Faction As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Form List. Distinto de que el campo valga cero.</summary>
        Property Parameter1FormListPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Form List  -&gt;  FLST. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1FormList As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Furniture. Distinto de que el campo valga cero.</summary>
        Property Parameter1FurniturePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Furniture  -&gt;  FLST / FURN. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Furniture As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Global. Distinto de que el campo valga cero.</summary>
        Property Parameter1GlobalPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Global  -&gt;  GLOB. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Global As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Idle. Distinto de que el campo valga cero.</summary>
        Property Parameter1IdlePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Idle  -&gt;  IDLE. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Idle As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Keyword. Distinto de que el campo valga cero.</summary>
        Property Parameter1KeywordPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Keyword  -&gt;  FLST / KYWD / NULL. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Keyword As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Location. Distinto de que el campo valga cero.</summary>
        Property Parameter1LocationPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Location  -&gt;  LCTN. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Location As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Location Ref Type. Distinto de que el campo valga cero.</summary>
        Property Parameter1LocationRefTypePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Location Ref Type  -&gt;  LCRT. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1LocationRefType As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Owner. Distinto de que el campo valga cero.</summary>
        Property Parameter1OwnerPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Owner  -&gt;  FACT / NPC_. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Owner As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Package. Distinto de que el campo valga cero.</summary>
        Property Parameter1PackagePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Package  -&gt;  PACK. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Package As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Perk. Distinto de que el campo valga cero.</summary>
        Property Parameter1PerkPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Perk  -&gt;  PERK. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Perk As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Quest. Distinto de que el campo valga cero.</summary>
        Property Parameter1QuestPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Quest  -&gt;  QUST. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Quest As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Race. Distinto de que el campo valga cero.</summary>
        Property Parameter1RacePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Race  -&gt;  RACE. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Race As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Reference. Distinto de que el campo valga cero.</summary>
        Property Parameter1ReferencePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Reference  -&gt;  ACHR / PARW / PBAR / PBEA / PCON / PFLA / PGRE / PHZD / PLYR / PMIS / REFR / TRGT. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Reference As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Region. Distinto de que el campo valga cero.</summary>
        Property Parameter1RegionPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Region  -&gt;  REGN. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Region As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Scene. Distinto de que el campo valga cero.</summary>
        Property Parameter1ScenePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Scene  -&gt;  SCEN. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Scene As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Voice Type. Distinto de que el campo valga cero.</summary>
        Property Parameter1VoiceTypePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Voice Type  -&gt;  VTYP / FLST. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1VoiceType As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Weather. Distinto de que el campo valga cero.</summary>
        Property Parameter1WeatherPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Weather  -&gt;  WTHR. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Weather As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Worldspace. Distinto de que el campo valga cero.</summary>
        Property Parameter1WorldspacePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Worldspace  -&gt;  WRLD / FLST. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Worldspace As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Unknown. Distinto de que el campo valga cero.</summary>
        Property Parameter2UnknownPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Unknown</summary>
        Property Parameter2Unknown As Byte()
        ''' <summary>El record trae Condition\CTDA\Parameter #2\None. Distinto de que el campo valga cero.</summary>
        Property Parameter2NonePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\None</summary>
        Property Parameter2None As Byte()
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Float. Distinto de que el campo valga cero.</summary>
        Property Parameter2FloatPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Float</summary>
        Property Parameter2Float As Single
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Integer. Distinto de que el campo valga cero.</summary>
        Property Parameter2IntegerPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Integer</summary>
        Property Parameter2Integer As Integer
        ''' <summary>El record trae Condition\CTDA\Parameter #2\String. Distinto de que el campo valga cero.</summary>
        Property Parameter2StringPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\String</summary>
        Property Parameter2String As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Alias. Distinto de que el campo valga cero.</summary>
        Property Parameter2AliasPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Alias</summary>
        Property Parameter2Alias As Integer
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Event. Distinto de que el campo valga cero.</summary>
        Property Parameter2EventPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Event</summary>
        Property Parameter2Event As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Packdata ID. Distinto de que el campo valga cero.</summary>
        Property Parameter2PackdataIDPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Packdata ID</summary>
        Property Parameter2PackdataID As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Quest Stage. Distinto de que el campo valga cero.</summary>
        Property Parameter2QuestStagePresente As Boolean
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Alignment. Distinto de que el campo valga cero.</summary>
        Property Parameter2AlignmentPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Alignment</summary>
        Property Parameter2Alignment As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #2\Alignment.</summary>
        ReadOnly Property Parameter2AlignmentNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Axis. Distinto de que el campo valga cero.</summary>
        Property Parameter2AxisPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Axis</summary>
        Property Parameter2Axis As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #2\Axis.</summary>
        ReadOnly Property Parameter2AxisNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Crime Type. Distinto de que el campo valga cero.</summary>
        Property Parameter2CrimeTypePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Crime Type</summary>
        Property Parameter2CrimeType As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #2\Crime Type.</summary>
        ReadOnly Property Parameter2CrimeTypeNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Critical Stage. Distinto de que el campo valga cero.</summary>
        Property Parameter2CriticalStagePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Critical Stage</summary>
        Property Parameter2CriticalStage As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #2\Critical Stage.</summary>
        ReadOnly Property Parameter2CriticalStageNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Form Type. Distinto de que el campo valga cero.</summary>
        Property Parameter2FormTypePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Form Type</summary>
        Property Parameter2FormType As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #2\Form Type.</summary>
        ReadOnly Property Parameter2FormTypeNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Misc Stat. Distinto de que el campo valga cero.</summary>
        Property Parameter2MiscStatPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Misc Stat</summary>
        Property Parameter2MiscStat As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #2\Misc Stat.</summary>
        ReadOnly Property Parameter2MiscStatNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Sex. Distinto de que el campo valga cero.</summary>
        Property Parameter2SexPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Sex</summary>
        Property Parameter2Sex As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #2\Sex.</summary>
        ReadOnly Property Parameter2SexNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Ward State. Distinto de que el campo valga cero.</summary>
        Property Parameter2WardStatePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Ward State</summary>
        Property Parameter2WardState As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #2\Ward State.</summary>
        ReadOnly Property Parameter2WardStateNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Actor. Distinto de que el campo valga cero.</summary>
        Property Parameter2ActorPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Actor  -&gt;  ACHR / PLYR / REFR / TRGT. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Actor As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Actor Base. Distinto de que el campo valga cero.</summary>
        Property Parameter2ActorBasePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Actor Base  -&gt;  NPC_. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2ActorBase As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Actor Value. Distinto de que el campo valga cero.</summary>
        Property Parameter2ActorValuePresente As Boolean
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Association Type. Distinto de que el campo valga cero.</summary>
        Property Parameter2AssociationTypePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Association Type  -&gt;  ASTP. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2AssociationType As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Base Object. Distinto de que el campo valga cero.</summary>
        Property Parameter2BaseObjectPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Base Object. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2BaseObject As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Cell. Distinto de que el campo valga cero.</summary>
        Property Parameter2CellPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Cell  -&gt;  CELL. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Cell As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Class. Distinto de que el campo valga cero.</summary>
        Property Parameter2ClassPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Class  -&gt;  CLAS. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Class As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Effect Item. Distinto de que el campo valga cero.</summary>
        Property Parameter2EffectItemPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Effect Item  -&gt;  ALCH / ENCH / INGR / SCRL / SPEL. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2EffectItem As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Encounter Zone. Distinto de que el campo valga cero.</summary>
        Property Parameter2EncounterZonePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Encounter Zone  -&gt;  ECZN. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2EncounterZone As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Equip Type. Distinto de que el campo valga cero.</summary>
        Property Parameter2EquipTypePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Equip Type  -&gt;  EQUP. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2EquipType As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Event Data. Distinto de que el campo valga cero.</summary>
        Property Parameter2EventDataPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Event Data  -&gt;  FLST / KYWD / LCTN. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2EventData As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Faction. Distinto de que el campo valga cero.</summary>
        Property Parameter2FactionPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Faction  -&gt;  FACT. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Faction As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Form List. Distinto de que el campo valga cero.</summary>
        Property Parameter2FormListPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Form List  -&gt;  FLST. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2FormList As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Furniture. Distinto de que el campo valga cero.</summary>
        Property Parameter2FurniturePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Furniture  -&gt;  FLST / FURN. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Furniture As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Global. Distinto de que el campo valga cero.</summary>
        Property Parameter2GlobalPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Global  -&gt;  GLOB. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Global As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Idle. Distinto de que el campo valga cero.</summary>
        Property Parameter2IdlePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Idle  -&gt;  IDLE. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Idle As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Keyword. Distinto de que el campo valga cero.</summary>
        Property Parameter2KeywordPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Keyword  -&gt;  FLST / KYWD / NULL. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Keyword As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Location. Distinto de que el campo valga cero.</summary>
        Property Parameter2LocationPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Location  -&gt;  LCTN. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Location As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Location Ref Type. Distinto de que el campo valga cero.</summary>
        Property Parameter2LocationRefTypePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Location Ref Type  -&gt;  LCRT. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2LocationRefType As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Owner. Distinto de que el campo valga cero.</summary>
        Property Parameter2OwnerPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Owner  -&gt;  FACT / NPC_. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Owner As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Package. Distinto de que el campo valga cero.</summary>
        Property Parameter2PackagePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Package  -&gt;  PACK. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Package As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Perk. Distinto de que el campo valga cero.</summary>
        Property Parameter2PerkPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Perk  -&gt;  PERK. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Perk As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Quest. Distinto de que el campo valga cero.</summary>
        Property Parameter2QuestPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Quest  -&gt;  QUST. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Quest As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Race. Distinto de que el campo valga cero.</summary>
        Property Parameter2RacePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Race  -&gt;  RACE. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Race As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Reference. Distinto de que el campo valga cero.</summary>
        Property Parameter2ReferencePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Reference  -&gt;  ACHR / PARW / PBAR / PBEA / PCON / PFLA / PGRE / PHZD / PLYR / PMIS / REFR / TRGT. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Reference As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Region. Distinto de que el campo valga cero.</summary>
        Property Parameter2RegionPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Region  -&gt;  REGN. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Region As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Scene. Distinto de que el campo valga cero.</summary>
        Property Parameter2ScenePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Scene  -&gt;  SCEN. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Scene As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Voice Type. Distinto de que el campo valga cero.</summary>
        Property Parameter2VoiceTypePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Voice Type  -&gt;  VTYP / FLST. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2VoiceType As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Weather. Distinto de que el campo valga cero.</summary>
        Property Parameter2WeatherPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Weather  -&gt;  WTHR. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Weather As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Worldspace. Distinto de que el campo valga cero.</summary>
        Property Parameter2WorldspacePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Worldspace  -&gt;  WRLD / FLST. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Worldspace As UInteger
        ''' <summary>El record trae Condition\CTDA\Run On. Distinto de que el campo valga cero.</summary>
        Property ConditionRunOnPresente As Boolean
        ''' <summary>Condition\CTDA\Run On</summary>
        Property ConditionRunOn As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Run On.</summary>
        ReadOnly Property ConditionRunOnNombre As String
        ''' <summary>El record trae Condition\CTDA\Reference\Reference. Distinto de que el campo valga cero.</summary>
        Property ConditionReferencePresente As Boolean
        ''' <summary>Condition\CTDA\Reference\Reference. Referencia en el espacio del orden de carga.</summary>
        Property ConditionReference As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #3\Parameter #3. Distinto de que el campo valga cero.</summary>
        Property ConditionParameter3Presente As Boolean
        ''' <summary>Condition\CTDA\Parameter #3\Parameter #3</summary>
        Property ConditionParameter3 As Integer
        ''' <summary>El record trae Condition\CTDA\Parameter #3\Quest Alias. Distinto de que el campo valga cero.</summary>
        Property Parameter3QuestAliasPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #3\Quest Alias</summary>
        Property Parameter3QuestAlias As Integer
        ''' <summary>El record trae Condition\CTDA\Parameter #3\Event Data. Distinto de que el campo valga cero.</summary>
        Property Parameter3EventDataPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #3\Event Data</summary>
        Property Parameter3EventData As Integer
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #3\Event Data.</summary>
        ReadOnly Property Parameter3EventDataNombre As String
        ''' <summary>El record trae Condition\CIS1\Parameter #1. Distinto de que el campo valga cero.</summary>
        Property ConditionParameter1Presente As Boolean
        ''' <summary>Condition\CIS1\Parameter #1</summary>
        Property ConditionParameter1 As String
        ''' <summary>El record trae Condition\CIS2\Parameter #2. Distinto de que el campo valga cero.</summary>
        Property ConditionParameter2Presente As Boolean
        ''' <summary>Condition\CIS2\Parameter #2</summary>
        Property ConditionParameter2 As String
    End Interface

    ''' <summary>Un elemento de Story Manager Conditions\Conditions, en lo que los dos juegos comparten.</summary>
    Public Interface IQust_Conditions2
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Condition\CTDA\Type. Distinto de que el campo valga cero.</summary>
        Property ConditionTypePresente As Boolean
        ''' <summary>Condition\CTDA\Type</summary>
        Property ConditionType As Byte
        ''' <summary>El record trae Condition\CTDA\Comparison Value\Comparison Value - Float. Distinto de que el campo valga cero.</summary>
        Property ConditionComparisonValueFloatPresente As Boolean
        ''' <summary>Condition\CTDA\Comparison Value\Comparison Value - Float</summary>
        Property ConditionComparisonValueFloat As Single
        ''' <summary>El record trae Condition\CTDA\Comparison Value\Comparison Value - Global. Distinto de que el campo valga cero.</summary>
        Property ConditionComparisonValueGlobalPresente As Boolean
        ''' <summary>Condition\CTDA\Comparison Value\Comparison Value - Global  -&gt;  GLOB. Referencia en el espacio del orden de carga.</summary>
        Property ConditionComparisonValueGlobal As UInteger
        ''' <summary>El record trae Condition\CTDA\Function. Distinto de que el campo valga cero.</summary>
        Property ConditionFunctionPresente As Boolean
        ''' <summary>Condition\CTDA\Function</summary>
        Property ConditionFunction As UShort
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Unknown. Distinto de que el campo valga cero.</summary>
        Property Parameter1UnknownPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Unknown</summary>
        Property Parameter1Unknown As Byte()
        ''' <summary>El record trae Condition\CTDA\Parameter #1\None. Distinto de que el campo valga cero.</summary>
        Property Parameter1NonePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\None</summary>
        Property Parameter1None As Byte()
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Float. Distinto de que el campo valga cero.</summary>
        Property Parameter1FloatPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Float</summary>
        Property Parameter1Float As Single
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Integer. Distinto de que el campo valga cero.</summary>
        Property Parameter1IntegerPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Integer</summary>
        Property Parameter1Integer As Integer
        ''' <summary>El record trae Condition\CTDA\Parameter #1\String. Distinto de que el campo valga cero.</summary>
        Property Parameter1StringPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\String</summary>
        Property Parameter1String As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Alias. Distinto de que el campo valga cero.</summary>
        Property Parameter1AliasPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Alias</summary>
        Property Parameter1Alias As Integer
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Event. Distinto de que el campo valga cero.</summary>
        Property Parameter1EventPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Event</summary>
        Property Parameter1Event As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Packdata ID. Distinto de que el campo valga cero.</summary>
        Property Parameter1PackdataIDPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Packdata ID</summary>
        Property Parameter1PackdataID As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Quest Stage. Distinto de que el campo valga cero.</summary>
        Property Parameter1QuestStagePresente As Boolean
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Alignment. Distinto de que el campo valga cero.</summary>
        Property Parameter1AlignmentPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Alignment</summary>
        Property Parameter1Alignment As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #1\Alignment.</summary>
        ReadOnly Property Parameter1AlignmentNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Axis. Distinto de que el campo valga cero.</summary>
        Property Parameter1AxisPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Axis</summary>
        Property Parameter1Axis As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #1\Axis.</summary>
        ReadOnly Property Parameter1AxisNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Crime Type. Distinto de que el campo valga cero.</summary>
        Property Parameter1CrimeTypePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Crime Type</summary>
        Property Parameter1CrimeType As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #1\Crime Type.</summary>
        ReadOnly Property Parameter1CrimeTypeNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Critical Stage. Distinto de que el campo valga cero.</summary>
        Property Parameter1CriticalStagePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Critical Stage</summary>
        Property Parameter1CriticalStage As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #1\Critical Stage.</summary>
        ReadOnly Property Parameter1CriticalStageNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Form Type. Distinto de que el campo valga cero.</summary>
        Property Parameter1FormTypePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Form Type</summary>
        Property Parameter1FormType As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #1\Form Type.</summary>
        ReadOnly Property Parameter1FormTypeNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Misc Stat. Distinto de que el campo valga cero.</summary>
        Property Parameter1MiscStatPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Misc Stat</summary>
        Property Parameter1MiscStat As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #1\Misc Stat.</summary>
        ReadOnly Property Parameter1MiscStatNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Sex. Distinto de que el campo valga cero.</summary>
        Property Parameter1SexPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Sex</summary>
        Property Parameter1Sex As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #1\Sex.</summary>
        ReadOnly Property Parameter1SexNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Ward State. Distinto de que el campo valga cero.</summary>
        Property Parameter1WardStatePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Ward State</summary>
        Property Parameter1WardState As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #1\Ward State.</summary>
        ReadOnly Property Parameter1WardStateNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Actor. Distinto de que el campo valga cero.</summary>
        Property Parameter1ActorPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Actor  -&gt;  ACHR / PLYR / REFR / TRGT. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Actor As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Actor Base. Distinto de que el campo valga cero.</summary>
        Property Parameter1ActorBasePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Actor Base  -&gt;  NPC_. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1ActorBase As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Actor Value. Distinto de que el campo valga cero.</summary>
        Property Parameter1ActorValuePresente As Boolean
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Association Type. Distinto de que el campo valga cero.</summary>
        Property Parameter1AssociationTypePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Association Type  -&gt;  ASTP. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1AssociationType As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Base Object. Distinto de que el campo valga cero.</summary>
        Property Parameter1BaseObjectPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Base Object. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1BaseObject As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Cell. Distinto de que el campo valga cero.</summary>
        Property Parameter1CellPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Cell  -&gt;  CELL. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Cell As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Class. Distinto de que el campo valga cero.</summary>
        Property Parameter1ClassPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Class  -&gt;  CLAS. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Class As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Effect Item. Distinto de que el campo valga cero.</summary>
        Property Parameter1EffectItemPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Effect Item  -&gt;  ALCH / ENCH / INGR / SCRL / SPEL. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1EffectItem As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Encounter Zone. Distinto de que el campo valga cero.</summary>
        Property Parameter1EncounterZonePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Encounter Zone  -&gt;  ECZN. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1EncounterZone As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Equip Type. Distinto de que el campo valga cero.</summary>
        Property Parameter1EquipTypePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Equip Type  -&gt;  EQUP. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1EquipType As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Event Data. Distinto de que el campo valga cero.</summary>
        Property Parameter1EventDataPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Event Data  -&gt;  FLST / KYWD / LCTN. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1EventData As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Faction. Distinto de que el campo valga cero.</summary>
        Property Parameter1FactionPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Faction  -&gt;  FACT. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Faction As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Form List. Distinto de que el campo valga cero.</summary>
        Property Parameter1FormListPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Form List  -&gt;  FLST. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1FormList As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Furniture. Distinto de que el campo valga cero.</summary>
        Property Parameter1FurniturePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Furniture  -&gt;  FLST / FURN. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Furniture As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Global. Distinto de que el campo valga cero.</summary>
        Property Parameter1GlobalPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Global  -&gt;  GLOB. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Global As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Idle. Distinto de que el campo valga cero.</summary>
        Property Parameter1IdlePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Idle  -&gt;  IDLE. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Idle As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Keyword. Distinto de que el campo valga cero.</summary>
        Property Parameter1KeywordPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Keyword  -&gt;  FLST / KYWD / NULL. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Keyword As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Location. Distinto de que el campo valga cero.</summary>
        Property Parameter1LocationPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Location  -&gt;  LCTN. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Location As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Location Ref Type. Distinto de que el campo valga cero.</summary>
        Property Parameter1LocationRefTypePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Location Ref Type  -&gt;  LCRT. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1LocationRefType As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Owner. Distinto de que el campo valga cero.</summary>
        Property Parameter1OwnerPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Owner  -&gt;  FACT / NPC_. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Owner As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Package. Distinto de que el campo valga cero.</summary>
        Property Parameter1PackagePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Package  -&gt;  PACK. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Package As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Perk. Distinto de que el campo valga cero.</summary>
        Property Parameter1PerkPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Perk  -&gt;  PERK. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Perk As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Quest. Distinto de que el campo valga cero.</summary>
        Property Parameter1QuestPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Quest  -&gt;  QUST. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Quest As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Race. Distinto de que el campo valga cero.</summary>
        Property Parameter1RacePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Race  -&gt;  RACE. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Race As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Reference. Distinto de que el campo valga cero.</summary>
        Property Parameter1ReferencePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Reference  -&gt;  ACHR / PARW / PBAR / PBEA / PCON / PFLA / PGRE / PHZD / PLYR / PMIS / REFR / TRGT. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Reference As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Region. Distinto de que el campo valga cero.</summary>
        Property Parameter1RegionPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Region  -&gt;  REGN. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Region As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Scene. Distinto de que el campo valga cero.</summary>
        Property Parameter1ScenePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Scene  -&gt;  SCEN. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Scene As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Voice Type. Distinto de que el campo valga cero.</summary>
        Property Parameter1VoiceTypePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Voice Type  -&gt;  VTYP / FLST. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1VoiceType As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Weather. Distinto de que el campo valga cero.</summary>
        Property Parameter1WeatherPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Weather  -&gt;  WTHR. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Weather As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Worldspace. Distinto de que el campo valga cero.</summary>
        Property Parameter1WorldspacePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Worldspace  -&gt;  WRLD / FLST. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Worldspace As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Unknown. Distinto de que el campo valga cero.</summary>
        Property Parameter2UnknownPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Unknown</summary>
        Property Parameter2Unknown As Byte()
        ''' <summary>El record trae Condition\CTDA\Parameter #2\None. Distinto de que el campo valga cero.</summary>
        Property Parameter2NonePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\None</summary>
        Property Parameter2None As Byte()
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Float. Distinto de que el campo valga cero.</summary>
        Property Parameter2FloatPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Float</summary>
        Property Parameter2Float As Single
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Integer. Distinto de que el campo valga cero.</summary>
        Property Parameter2IntegerPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Integer</summary>
        Property Parameter2Integer As Integer
        ''' <summary>El record trae Condition\CTDA\Parameter #2\String. Distinto de que el campo valga cero.</summary>
        Property Parameter2StringPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\String</summary>
        Property Parameter2String As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Alias. Distinto de que el campo valga cero.</summary>
        Property Parameter2AliasPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Alias</summary>
        Property Parameter2Alias As Integer
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Event. Distinto de que el campo valga cero.</summary>
        Property Parameter2EventPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Event</summary>
        Property Parameter2Event As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Packdata ID. Distinto de que el campo valga cero.</summary>
        Property Parameter2PackdataIDPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Packdata ID</summary>
        Property Parameter2PackdataID As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Quest Stage. Distinto de que el campo valga cero.</summary>
        Property Parameter2QuestStagePresente As Boolean
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Alignment. Distinto de que el campo valga cero.</summary>
        Property Parameter2AlignmentPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Alignment</summary>
        Property Parameter2Alignment As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #2\Alignment.</summary>
        ReadOnly Property Parameter2AlignmentNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Axis. Distinto de que el campo valga cero.</summary>
        Property Parameter2AxisPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Axis</summary>
        Property Parameter2Axis As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #2\Axis.</summary>
        ReadOnly Property Parameter2AxisNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Crime Type. Distinto de que el campo valga cero.</summary>
        Property Parameter2CrimeTypePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Crime Type</summary>
        Property Parameter2CrimeType As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #2\Crime Type.</summary>
        ReadOnly Property Parameter2CrimeTypeNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Critical Stage. Distinto de que el campo valga cero.</summary>
        Property Parameter2CriticalStagePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Critical Stage</summary>
        Property Parameter2CriticalStage As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #2\Critical Stage.</summary>
        ReadOnly Property Parameter2CriticalStageNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Form Type. Distinto de que el campo valga cero.</summary>
        Property Parameter2FormTypePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Form Type</summary>
        Property Parameter2FormType As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #2\Form Type.</summary>
        ReadOnly Property Parameter2FormTypeNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Misc Stat. Distinto de que el campo valga cero.</summary>
        Property Parameter2MiscStatPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Misc Stat</summary>
        Property Parameter2MiscStat As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #2\Misc Stat.</summary>
        ReadOnly Property Parameter2MiscStatNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Sex. Distinto de que el campo valga cero.</summary>
        Property Parameter2SexPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Sex</summary>
        Property Parameter2Sex As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #2\Sex.</summary>
        ReadOnly Property Parameter2SexNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Ward State. Distinto de que el campo valga cero.</summary>
        Property Parameter2WardStatePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Ward State</summary>
        Property Parameter2WardState As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #2\Ward State.</summary>
        ReadOnly Property Parameter2WardStateNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Actor. Distinto de que el campo valga cero.</summary>
        Property Parameter2ActorPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Actor  -&gt;  ACHR / PLYR / REFR / TRGT. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Actor As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Actor Base. Distinto de que el campo valga cero.</summary>
        Property Parameter2ActorBasePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Actor Base  -&gt;  NPC_. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2ActorBase As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Actor Value. Distinto de que el campo valga cero.</summary>
        Property Parameter2ActorValuePresente As Boolean
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Association Type. Distinto de que el campo valga cero.</summary>
        Property Parameter2AssociationTypePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Association Type  -&gt;  ASTP. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2AssociationType As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Base Object. Distinto de que el campo valga cero.</summary>
        Property Parameter2BaseObjectPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Base Object. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2BaseObject As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Cell. Distinto de que el campo valga cero.</summary>
        Property Parameter2CellPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Cell  -&gt;  CELL. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Cell As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Class. Distinto de que el campo valga cero.</summary>
        Property Parameter2ClassPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Class  -&gt;  CLAS. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Class As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Effect Item. Distinto de que el campo valga cero.</summary>
        Property Parameter2EffectItemPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Effect Item  -&gt;  ALCH / ENCH / INGR / SCRL / SPEL. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2EffectItem As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Encounter Zone. Distinto de que el campo valga cero.</summary>
        Property Parameter2EncounterZonePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Encounter Zone  -&gt;  ECZN. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2EncounterZone As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Equip Type. Distinto de que el campo valga cero.</summary>
        Property Parameter2EquipTypePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Equip Type  -&gt;  EQUP. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2EquipType As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Event Data. Distinto de que el campo valga cero.</summary>
        Property Parameter2EventDataPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Event Data  -&gt;  FLST / KYWD / LCTN. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2EventData As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Faction. Distinto de que el campo valga cero.</summary>
        Property Parameter2FactionPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Faction  -&gt;  FACT. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Faction As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Form List. Distinto de que el campo valga cero.</summary>
        Property Parameter2FormListPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Form List  -&gt;  FLST. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2FormList As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Furniture. Distinto de que el campo valga cero.</summary>
        Property Parameter2FurniturePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Furniture  -&gt;  FLST / FURN. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Furniture As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Global. Distinto de que el campo valga cero.</summary>
        Property Parameter2GlobalPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Global  -&gt;  GLOB. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Global As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Idle. Distinto de que el campo valga cero.</summary>
        Property Parameter2IdlePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Idle  -&gt;  IDLE. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Idle As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Keyword. Distinto de que el campo valga cero.</summary>
        Property Parameter2KeywordPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Keyword  -&gt;  FLST / KYWD / NULL. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Keyword As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Location. Distinto de que el campo valga cero.</summary>
        Property Parameter2LocationPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Location  -&gt;  LCTN. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Location As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Location Ref Type. Distinto de que el campo valga cero.</summary>
        Property Parameter2LocationRefTypePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Location Ref Type  -&gt;  LCRT. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2LocationRefType As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Owner. Distinto de que el campo valga cero.</summary>
        Property Parameter2OwnerPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Owner  -&gt;  FACT / NPC_. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Owner As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Package. Distinto de que el campo valga cero.</summary>
        Property Parameter2PackagePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Package  -&gt;  PACK. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Package As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Perk. Distinto de que el campo valga cero.</summary>
        Property Parameter2PerkPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Perk  -&gt;  PERK. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Perk As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Quest. Distinto de que el campo valga cero.</summary>
        Property Parameter2QuestPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Quest  -&gt;  QUST. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Quest As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Race. Distinto de que el campo valga cero.</summary>
        Property Parameter2RacePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Race  -&gt;  RACE. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Race As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Reference. Distinto de que el campo valga cero.</summary>
        Property Parameter2ReferencePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Reference  -&gt;  ACHR / PARW / PBAR / PBEA / PCON / PFLA / PGRE / PHZD / PLYR / PMIS / REFR / TRGT. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Reference As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Region. Distinto de que el campo valga cero.</summary>
        Property Parameter2RegionPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Region  -&gt;  REGN. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Region As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Scene. Distinto de que el campo valga cero.</summary>
        Property Parameter2ScenePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Scene  -&gt;  SCEN. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Scene As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Voice Type. Distinto de que el campo valga cero.</summary>
        Property Parameter2VoiceTypePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Voice Type  -&gt;  VTYP / FLST. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2VoiceType As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Weather. Distinto de que el campo valga cero.</summary>
        Property Parameter2WeatherPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Weather  -&gt;  WTHR. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Weather As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Worldspace. Distinto de que el campo valga cero.</summary>
        Property Parameter2WorldspacePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Worldspace  -&gt;  WRLD / FLST. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Worldspace As UInteger
        ''' <summary>El record trae Condition\CTDA\Run On. Distinto de que el campo valga cero.</summary>
        Property ConditionRunOnPresente As Boolean
        ''' <summary>Condition\CTDA\Run On</summary>
        Property ConditionRunOn As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Run On.</summary>
        ReadOnly Property ConditionRunOnNombre As String
        ''' <summary>El record trae Condition\CTDA\Reference\Reference. Distinto de que el campo valga cero.</summary>
        Property ConditionReferencePresente As Boolean
        ''' <summary>Condition\CTDA\Reference\Reference. Referencia en el espacio del orden de carga.</summary>
        Property ConditionReference As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #3\Parameter #3. Distinto de que el campo valga cero.</summary>
        Property ConditionParameter3Presente As Boolean
        ''' <summary>Condition\CTDA\Parameter #3\Parameter #3</summary>
        Property ConditionParameter3 As Integer
        ''' <summary>El record trae Condition\CTDA\Parameter #3\Quest Alias. Distinto de que el campo valga cero.</summary>
        Property Parameter3QuestAliasPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #3\Quest Alias</summary>
        Property Parameter3QuestAlias As Integer
        ''' <summary>El record trae Condition\CTDA\Parameter #3\Event Data. Distinto de que el campo valga cero.</summary>
        Property Parameter3EventDataPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #3\Event Data</summary>
        Property Parameter3EventData As Integer
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #3\Event Data.</summary>
        ReadOnly Property Parameter3EventDataNombre As String
        ''' <summary>El record trae Condition\CIS1\Parameter #1. Distinto de que el campo valga cero.</summary>
        Property ConditionParameter1Presente As Boolean
        ''' <summary>Condition\CIS1\Parameter #1</summary>
        Property ConditionParameter1 As String
        ''' <summary>El record trae Condition\CIS2\Parameter #2. Distinto de que el campo valga cero.</summary>
        Property ConditionParameter2Presente As Boolean
        ''' <summary>Condition\CIS2\Parameter #2</summary>
        Property ConditionParameter2 As String
    End Interface

    ''' <summary>Un elemento de Stages, en lo que los dos juegos comparten.</summary>
    Public Interface IQust_Stages
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Stage\INDX\Stage Index\Stage Index. Distinto de que el campo valga cero.</summary>
        Property StageIndexPresente As Boolean
        ''' <summary>Stage\INDX\Stage Index\Stage Index</summary>
        Property StageIndex As UShort
        ''' <summary>El record trae Stage\INDX\Stage Index\Flags. Distinto de que el campo valga cero.</summary>
        Property StageIndexFlagsPresente As Boolean
        ''' <summary>Stage\INDX\Stage Index\Flags</summary>
        Property StageIndexFlags As Byte
        ''' <summary>Bit 3 de Stage\INDX\Stage Index\Flags: Keep Instance Data From Here On</summary>
        Property StageIndexFlagsKeepInstanceDataFromHereOn As Boolean
        ''' <summary>El record trae Stage\INDX\Stage Index\Unknown. Distinto de que el campo valga cero.</summary>
        Property StageIndexUnknownPresente As Boolean
    End Interface

    ''' <summary>Un elemento de Objectives, en lo que los dos juegos comparten.</summary>
    Public Interface IQust_Objectives
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Objective\QOBJ\Objective Index. Distinto de que el campo valga cero.</summary>
        Property ObjectiveIndexPresente As Boolean
        ''' <summary>Objective\QOBJ\Objective Index</summary>
        Property ObjectiveIndex As UShort
        ''' <summary>El record trae Objective\NNAM\Display Text. Distinto de que el campo valga cero.</summary>
        Property ObjectiveDisplayTextPresente As Boolean
        ''' <summary>Objective\NNAM\Display Text. Resuelto contra las tablas de texto si el archivo las usa.</summary>
        Property ObjectiveDisplayText As String
    End Interface

    ''' <summary>Un elemento de Aliases, en lo que los dos juegos comparten.</summary>
    Public Interface IQust_Aliases2
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Alias\Location Alias Reference\ALFA\Alias. Distinto de que el campo valga cero.</summary>
        Property LocationAliasReferenceAliasPresente As Boolean
        ''' <summary>Alias\Location Alias Reference\ALFA\Alias</summary>
        Property LocationAliasReferenceAlias As Integer
        ''' <summary>El record trae Alias\Location Alias Reference\KNAM\Keyword. Distinto de que el campo valga cero.</summary>
        Property LocationAliasReferenceKeywordPresente As Boolean
        ''' <summary>Alias\Location Alias Reference\KNAM\Keyword  -&gt;  KYWD. Referencia en el espacio del orden de carga.</summary>
        Property LocationAliasReferenceKeyword As UInteger
        ''' <summary>El record trae Alias\Location Alias Reference\ALRT\Ref Type. Distinto de que el campo valga cero.</summary>
        Property LocationAliasReferenceRefTypePresente As Boolean
        ''' <summary>Alias\Location Alias Reference\ALRT\Ref Type  -&gt;  LCRT. Referencia en el espacio del orden de carga.</summary>
        Property LocationAliasReferenceRefType As UInteger
        ''' <summary>El record trae Alias\External Alias Reference\ALEQ\Quest. Distinto de que el campo valga cero.</summary>
        Property ExternalAliasReferenceQuestPresente As Boolean
        ''' <summary>Alias\External Alias Reference\ALEQ\Quest  -&gt;  QUST. Referencia en el espacio del orden de carga.</summary>
        Property ExternalAliasReferenceQuest As UInteger
        ''' <summary>El record trae Alias\External Alias Reference\ALEA\Alias. Distinto de que el campo valga cero.</summary>
        Property ExternalAliasReferenceAliasPresente As Boolean
        ''' <summary>Alias\External Alias Reference\ALEA\Alias</summary>
        Property ExternalAliasReferenceAlias As Integer
        ''' <summary>El record trae Alias\Create Reference to Object\ALCO\Object. Distinto de que el campo valga cero.</summary>
        Property CreateReferenceToObjectObjectPresente As Boolean
        ''' <summary>Alias\Create Reference to Object\ALCO\Object. Referencia en el espacio del orden de carga.</summary>
        Property CreateReferenceToObjectObject As UInteger
        ''' <summary>El record trae Alias\Create Reference to Object\ALCA\Alias\Alias. Distinto de que el campo valga cero.</summary>
        Property CreateReferenceToObjectAliasPresente As Boolean
        ''' <summary>Alias\Create Reference to Object\ALCA\Alias\Alias</summary>
        Property CreateReferenceToObjectAlias As Short
        ''' <summary>El record trae Alias\Create Reference to Object\ALCA\Alias\Create. Distinto de que el campo valga cero.</summary>
        Property AliasCreatePresente As Boolean
        ''' <summary>Alias\Create Reference to Object\ALCA\Alias\Create</summary>
        Property AliasCreate As UShort
        ''' <summary>Nombre del valor de Alias\Create Reference to Object\ALCA\Alias\Create.</summary>
        ReadOnly Property AliasCreateNombre As String
        ''' <summary>El record trae Alias\Create Reference to Object\ALCL\Level. Distinto de que el campo valga cero.</summary>
        Property CreateReferenceToObjectLevelPresente As Boolean
        ''' <summary>Alias\Create Reference to Object\ALCL\Level</summary>
        Property CreateReferenceToObjectLevel As UInteger
        ''' <summary>Nombre del valor de Alias\Create Reference to Object\ALCL\Level.</summary>
        ReadOnly Property CreateReferenceToObjectLevelNombre As String
        ''' <summary>El record trae Alias\Find Matching Reference Near Alias\ALNA\Alias. Distinto de que el campo valga cero.</summary>
        Property FindMatchingReferenceNearAliasAliasPresente As Boolean
        ''' <summary>Alias\Find Matching Reference Near Alias\ALNA\Alias</summary>
        Property FindMatchingReferenceNearAliasAlias As Integer
        ''' <summary>El record trae Alias\Find Matching Reference Near Alias\ALNT\Type. Distinto de que el campo valga cero.</summary>
        Property FindMatchingReferenceNearAliasTypePresente As Boolean
        ''' <summary>Alias\Find Matching Reference Near Alias\ALNT\Type</summary>
        Property FindMatchingReferenceNearAliasType As UInteger
        ''' <summary>Nombre del valor de Alias\Find Matching Reference Near Alias\ALNT\Type.</summary>
        ReadOnly Property FindMatchingReferenceNearAliasTypeNombre As String
        ''' <summary>El record trae Alias\Find Matching Reference From Event\ALFE\From Event. Distinto de que el campo valga cero.</summary>
        Property FindMatchingReferenceFromEventFromEventPresente As Boolean
        ''' <summary>Alias\Find Matching Reference From Event\ALFE\From Event</summary>
        Property FindMatchingReferenceFromEventFromEvent As UInteger
        ''' <summary>El record trae Alias\Find Matching Reference From Event\ALFD\Event Data. Distinto de que el campo valga cero.</summary>
        Property FindMatchingReferenceFromEventEventDataPresente As Boolean
        ''' <summary>Alias\Find Matching Reference From Event\ALFD\Event Data</summary>
        Property FindMatchingReferenceFromEventEventData As UInteger
        ''' <summary>Nombre del valor de Alias\Find Matching Reference From Event\ALFD\Event Data.</summary>
        ReadOnly Property FindMatchingReferenceFromEventEventDataNombre As String
        ''' <summary>El record trae Alias\Keywords\KSIZ\Keyword Count. Distinto de que el campo valga cero.</summary>
        Property KeywordsKeywordCountPresente As Boolean
        ''' <summary>Alias\Keywords\KSIZ\Keyword Count</summary>
        Property KeywordsKeywordCount As UInteger
        ''' <summary>El record trae Alias\Reference Alias Location\ALFA\Alias. Distinto de que el campo valga cero.</summary>
        Property ReferenceAliasLocationAliasPresente As Boolean
        ''' <summary>Alias\Reference Alias Location\ALFA\Alias</summary>
        Property ReferenceAliasLocationAlias As Integer
        ''' <summary>El record trae Alias\Reference Alias Location\KNAM\Keyword. Distinto de que el campo valga cero.</summary>
        Property ReferenceAliasLocationKeywordPresente As Boolean
        ''' <summary>Alias\Reference Alias Location\KNAM\Keyword  -&gt;  KYWD. Referencia en el espacio del orden de carga.</summary>
        Property ReferenceAliasLocationKeyword As UInteger
        ''' <summary>El record trae Alias\External Alias Location\ALEQ\Quest. Distinto de que el campo valga cero.</summary>
        Property ExternalAliasLocationQuestPresente As Boolean
        ''' <summary>Alias\External Alias Location\ALEQ\Quest  -&gt;  QUST. Referencia en el espacio del orden de carga.</summary>
        Property ExternalAliasLocationQuest As UInteger
        ''' <summary>El record trae Alias\External Alias Location\ALEA\Alias. Distinto de que el campo valga cero.</summary>
        Property ExternalAliasLocationAliasPresente As Boolean
        ''' <summary>Alias\External Alias Location\ALEA\Alias</summary>
        Property ExternalAliasLocationAlias As Integer
    End Interface

    ''' <summary>Campos de un record RACE que los dos juegos declaran igual.
    ''' <para>El codigo que solo usa estos no necesita saber de que juego se trata. El
    ''' que necesita algo propio de un juego tiene que nombrar la clase de ese juego, y
    ''' asi no puede leer sin querer un campo que alla significa otra cosa.</para></summary>
    Public Interface IRace
        ReadOnly Property Node As WbNode
        ''' <summary>Identificador del record.</summary>
        ReadOnly Property FormID As UInteger
        ''' <summary>El record trae EDID\Editor ID. Distinto de que el campo valga cero.</summary>
        Property EditorIDPresente As Boolean
        ''' <summary>EDID\Editor ID</summary>
        Property EditorID As String
        ''' <summary>El record trae FULL\Name. Distinto de que el campo valga cero.</summary>
        Property NamePresente As Boolean
        ''' <summary>FULL\Name. Resuelto contra las tablas de texto si el archivo las usa.</summary>
        Property Name As String
        ''' <summary>El record trae DESC\Description. Distinto de que el campo valga cero.</summary>
        Property DescriptionPresente As Boolean
        ''' <summary>DESC\Description. Resuelto contra las tablas de texto si el archivo las usa.</summary>
        Property Description As String
        ''' <summary>El record trae SPCT\Count. Distinto de que el campo valga cero.</summary>
        Property CountPresente As Boolean
        ''' <summary>SPCT\Count</summary>
        Property Count As UInteger
        ''' <summary>El record trae WNAM\Skin. Distinto de que el campo valga cero.</summary>
        Property SkinPresente As Boolean
        ''' <summary>WNAM\Skin  -&gt;  ARMO / NULL. Referencia en el espacio del orden de carga.</summary>
        Property Skin As UInteger
        ''' <summary>El record trae BOD2\Biped Body Template\First Person Flags. Distinto de que el campo valga cero.</summary>
        Property BipedBodyTemplateFirstPersonFlagsPresente As Boolean
        ''' <summary>BOD2\Biped Body Template\First Person Flags</summary>
        Property BipedBodyTemplateFirstPersonFlags As UInteger
        ''' <summary>Bit 24 de BOD2\Biped Body Template\First Person Flags: 54 - Unnamed</summary>
        Property BipedBodyTemplateFirstPersonFlags54Unnamed As Boolean
        ''' <summary>Bit 25 de BOD2\Biped Body Template\First Person Flags: 55 - Unnamed</summary>
        Property BipedBodyTemplateFirstPersonFlags55Unnamed As Boolean
        ''' <summary>Bit 26 de BOD2\Biped Body Template\First Person Flags: 56 - Unnamed</summary>
        Property BipedBodyTemplateFirstPersonFlags56Unnamed As Boolean
        ''' <summary>Bit 27 de BOD2\Biped Body Template\First Person Flags: 57 - Unnamed</summary>
        Property BipedBodyTemplateFirstPersonFlags57Unnamed As Boolean
        ''' <summary>Bit 28 de BOD2\Biped Body Template\First Person Flags: 58 - Unnamed</summary>
        Property BipedBodyTemplateFirstPersonFlags58Unnamed As Boolean
        ''' <summary>El record trae Keywords\KSIZ\Keyword Count. Distinto de que el campo valga cero.</summary>
        Property KeywordsKeywordCountPresente As Boolean
        ''' <summary>Keywords\KSIZ\Keyword Count</summary>
        Property KeywordsKeywordCount As UInteger
        ''' <summary>El record trae el marcador MNAM\Male Marker. No lleva valor: el dato es que esté, así que ponerlo en verdadero lo crea y en falso lo saca.</summary>
        Property MaleMarker As Boolean
        ''' <summary>El record trae ANAM\Male Skeletal Model. Distinto de que el campo valga cero.</summary>
        Property MaleSkeletalModelPresente As Boolean
        ''' <summary>ANAM\Male Skeletal Model</summary>
        Property MaleSkeletalModel As String
        ''' <summary>El record trae MODT\Model Information\ERROR. Distinto de que el campo valga cero.</summary>
        Property ModelInformationERRORPresente As Boolean
        ''' <summary>MODT\Model Information\ERROR</summary>
        Property ModelInformationERROR As Byte()
        ''' <summary>El record trae el marcador FNAM\Female Marker. No lleva valor: el dato es que esté, así que ponerlo en verdadero lo crea y en falso lo saca.</summary>
        Property FemaleMarker As Boolean
        ''' <summary>El record trae ANAM\Female Skeletal Model. Distinto de que el campo valga cero.</summary>
        Property FemaleSkeletalModelPresente As Boolean
        ''' <summary>ANAM\Female Skeletal Model</summary>
        Property FemaleSkeletalModel As String
        ''' <summary>El record trae el marcador NAM2\Marker NAM2 #1. No lleva valor: el dato es que esté, así que ponerlo en verdadero lo crea y en falso lo saca.</summary>
        Property MarkerNAM21 As Boolean
        ''' <summary>El record trae TINL\Total Number of Tints in List. Distinto de que el campo valga cero.</summary>
        Property TotalNumberOfTintsInListPresente As Boolean
        ''' <summary>TINL\Total Number of Tints in List</summary>
        Property TotalNumberOfTintsInList As UShort
        ''' <summary>El record trae PNAM\FaceGen - Main clamp. Distinto de que el campo valga cero.</summary>
        Property FaceGenMainClampPresente As Boolean
        ''' <summary>PNAM\FaceGen - Main clamp</summary>
        Property FaceGenMainClamp As Single
        ''' <summary>El record trae UNAM\FaceGen - Face clamp. Distinto de que el campo valga cero.</summary>
        Property FaceGenFaceClampPresente As Boolean
        ''' <summary>UNAM\FaceGen - Face clamp</summary>
        Property FaceGenFaceClamp As Single
        ''' <summary>El record trae ATKR\Attack Race. Distinto de que el campo valga cero.</summary>
        Property AttackRacePresente As Boolean
        ''' <summary>ATKR\Attack Race  -&gt;  RACE. Referencia en el espacio del orden de carga.</summary>
        Property AttackRace As UInteger
        ''' <summary>El record trae el marcador Body Data\NAM1\Body Data Marker. No lleva valor: el dato es que esté, así que ponerlo en verdadero lo crea y en falso lo saca.</summary>
        Property BodyDataMarker As Boolean
        ''' <summary>El record trae el marcador Body Data\Male Body Data\MNAM\Male Data Marker. No lleva valor: el dato es que esté, así que ponerlo en verdadero lo crea y en falso lo saca.</summary>
        Property MaleBodyDataMaleDataMarker As Boolean
        ''' <summary>El record trae el marcador Body Data\Female Body Data\FNAM\Female Data Marker. No lleva valor: el dato es que esté, así que ponerlo en verdadero lo crea y en falso lo saca.</summary>
        Property FemaleBodyDataFemaleDataMarker As Boolean
        ''' <summary>El record trae GNAM\Body Part Data. Distinto de que el campo valga cero.</summary>
        Property BodyPartDataPresente As Boolean
        ''' <summary>GNAM\Body Part Data  -&gt;  BPTD. Referencia en el espacio del orden de carga.</summary>
        Property BodyPartData As UInteger
        ''' <summary>El record trae el marcador NAM2\Marker NAM2 #2. No lleva valor: el dato es que esté, así que ponerlo en verdadero lo crea y en falso lo saca.</summary>
        Property MarkerNAM22 As Boolean
        ''' <summary>El record trae el marcador NAM3\Marker NAM3 #3. No lleva valor: el dato es que esté, así que ponerlo en verdadero lo crea y en falso lo saca.</summary>
        Property MarkerNAM33 As Boolean
        ''' <summary>El record trae el marcador Male Behavior Graph\MNAM\Male Data Marker. No lleva valor: el dato es que esté, así que ponerlo en verdadero lo crea y en falso lo saca.</summary>
        Property MaleBehaviorGraphMaleDataMarker As Boolean
        ''' <summary>El record trae Male Behavior Graph\Model\MODL\Model FileName. Distinto de que el campo valga cero.</summary>
        Property MaleBehaviorGraphModelFileNamePresente As Boolean
        ''' <summary>Male Behavior Graph\Model\MODL\Model FileName</summary>
        Property MaleBehaviorGraphModelFileName As String
        ''' <summary>El record trae Male Behavior Graph\Model\MODT\Model Information\ERROR. Distinto de que el campo valga cero.</summary>
        Property ModelInformationERROR2Presente As Boolean
        ''' <summary>Male Behavior Graph\Model\MODT\Model Information\ERROR</summary>
        Property ModelInformationERROR2 As Byte()
        ''' <summary>El record trae el marcador Female Behavior Graph\FNAM\Female Data Marker. No lleva valor: el dato es que esté, así que ponerlo en verdadero lo crea y en falso lo saca.</summary>
        Property FemaleBehaviorGraphFemaleDataMarker As Boolean
        ''' <summary>El record trae Female Behavior Graph\Model\MODL\Model FileName. Distinto de que el campo valga cero.</summary>
        Property FemaleBehaviorGraphModelFileNamePresente As Boolean
        ''' <summary>Female Behavior Graph\Model\MODL\Model FileName</summary>
        Property FemaleBehaviorGraphModelFileName As String
        ''' <summary>El record trae Female Behavior Graph\Model\MODT\Model Information\ERROR. Distinto de que el campo valga cero.</summary>
        Property ModelInformationERROR3Presente As Boolean
        ''' <summary>Female Behavior Graph\Model\MODT\Model Information\ERROR</summary>
        Property ModelInformationERROR3 As Byte()
        ''' <summary>El record trae NAM5\Impact Data Set. Distinto de que el campo valga cero.</summary>
        Property ImpactDataSetPresente As Boolean
        ''' <summary>NAM5\Impact Data Set  -&gt;  IPDS. Referencia en el espacio del orden de carga.</summary>
        Property ImpactDataSet As UInteger
        ''' <summary>El record trae VNAM\Equipment Flags. Distinto de que el campo valga cero.</summary>
        Property EquipmentFlagsPresente As Boolean
        ''' <summary>VNAM\Equipment Flags</summary>
        Property EquipmentFlags As UInteger
        ''' <summary>Bit 0 de VNAM\Equipment Flags: Hand To Hand Melee</summary>
        Property EquipmentFlagsHandToHandMelee As Boolean
        ''' <summary>Bit 1 de VNAM\Equipment Flags: One Hand Sword</summary>
        Property EquipmentFlagsOneHandSword As Boolean
        ''' <summary>Bit 2 de VNAM\Equipment Flags: One Hand Dagger</summary>
        Property EquipmentFlagsOneHandDagger As Boolean
        ''' <summary>Bit 3 de VNAM\Equipment Flags: One Hand Axe</summary>
        Property EquipmentFlagsOneHandAxe As Boolean
        ''' <summary>Bit 4 de VNAM\Equipment Flags: One Hand Mace</summary>
        Property EquipmentFlagsOneHandMace As Boolean
        ''' <summary>Bit 5 de VNAM\Equipment Flags: Two Hand Sword</summary>
        Property EquipmentFlagsTwoHandSword As Boolean
        ''' <summary>Bit 6 de VNAM\Equipment Flags: Two Hand Axe</summary>
        Property EquipmentFlagsTwoHandAxe As Boolean
        ''' <summary>Bit 7 de VNAM\Equipment Flags: Bow</summary>
        Property EquipmentFlagsBow As Boolean
        ''' <summary>Bit 8 de VNAM\Equipment Flags: Staff</summary>
        Property EquipmentFlagsStaff As Boolean
        ''' <summary>Bit 12 de VNAM\Equipment Flags: Spell</summary>
        Property EquipmentFlagsSpell As Boolean
        ''' <summary>Bit 13 de VNAM\Equipment Flags: Shield</summary>
        Property EquipmentFlagsShield As Boolean
        ''' <summary>Bit 14 de VNAM\Equipment Flags: Torch</summary>
        Property EquipmentFlagsTorch As Boolean
        ''' <summary>El record trae FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\Aah / LipBigAah. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightAahLipBigAahPresente As Boolean
        ''' <summary>FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Property PhonemeTargetWeightAahLipBigAah As Single
        ''' <summary>El record trae FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\BigAah / LipDST. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightBigAahLipDSTPresente As Boolean
        ''' <summary>FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Property PhonemeTargetWeightBigAahLipDST As Single
        ''' <summary>El record trae FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\BMP / LipEee. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightBMPLipEeePresente As Boolean
        ''' <summary>FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Property PhonemeTargetWeightBMPLipEee As Single
        ''' <summary>El record trae FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\ChJsh / LipFV. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightChJshLipFVPresente As Boolean
        ''' <summary>FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Property PhonemeTargetWeightChJshLipFV As Single
        ''' <summary>El record trae FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\DST / LipK. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightDSTLipKPresente As Boolean
        ''' <summary>FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Property PhonemeTargetWeightDSTLipK As Single
        ''' <summary>El record trae FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\Eee / LipL. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightEeeLipLPresente As Boolean
        ''' <summary>FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Property PhonemeTargetWeightEeeLipL As Single
        ''' <summary>El record trae FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\Eh / LipR. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightEhLipRPresente As Boolean
        ''' <summary>FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Property PhonemeTargetWeightEhLipR As Single
        ''' <summary>El record trae FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\FV / LipTh. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightFVLipThPresente As Boolean
        ''' <summary>FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Property PhonemeTargetWeightFVLipTh As Single
        ''' <summary>El record trae FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\I. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightIPresente As Boolean
        ''' <summary>FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\I</summary>
        Property PhonemeTargetWeightI As Single
        ''' <summary>El record trae FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\K. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightKPresente As Boolean
        ''' <summary>FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\K</summary>
        Property PhonemeTargetWeightK As Single
        ''' <summary>El record trae FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\N. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightNPresente As Boolean
        ''' <summary>FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\N</summary>
        Property PhonemeTargetWeightN As Single
        ''' <summary>El record trae FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\Oh. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightOhPresente As Boolean
        ''' <summary>FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\Oh</summary>
        Property PhonemeTargetWeightOh As Single
        ''' <summary>El record trae FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\OohQ. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightOohQPresente As Boolean
        ''' <summary>FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\OohQ</summary>
        Property PhonemeTargetWeightOohQ As Single
        ''' <summary>El record trae FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\R. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightRPresente As Boolean
        ''' <summary>FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\R</summary>
        Property PhonemeTargetWeightR As Single
        ''' <summary>El record trae FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\TH. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightTHPresente As Boolean
        ''' <summary>FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\TH</summary>
        Property PhonemeTargetWeightTH As Single
        ''' <summary>El record trae FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\W. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightWPresente As Boolean
        ''' <summary>FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\W</summary>
        Property PhonemeTargetWeightW As Single
        ''' <summary>El record trae FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\Aah / LipBigAah. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightAahLipBigAah2Presente As Boolean
        ''' <summary>FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Property PhonemeTargetWeightAahLipBigAah2 As Single
        ''' <summary>El record trae FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\BigAah / LipDST. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightBigAahLipDST2Presente As Boolean
        ''' <summary>FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Property PhonemeTargetWeightBigAahLipDST2 As Single
        ''' <summary>El record trae FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\BMP / LipEee. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightBMPLipEee2Presente As Boolean
        ''' <summary>FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Property PhonemeTargetWeightBMPLipEee2 As Single
        ''' <summary>El record trae FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\ChJsh / LipFV. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightChJshLipFV2Presente As Boolean
        ''' <summary>FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Property PhonemeTargetWeightChJshLipFV2 As Single
        ''' <summary>El record trae FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\DST / LipK. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightDSTLipK2Presente As Boolean
        ''' <summary>FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Property PhonemeTargetWeightDSTLipK2 As Single
        ''' <summary>El record trae FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\Eee / LipL. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightEeeLipL2Presente As Boolean
        ''' <summary>FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Property PhonemeTargetWeightEeeLipL2 As Single
        ''' <summary>El record trae FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\Eh / LipR. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightEhLipR2Presente As Boolean
        ''' <summary>FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Property PhonemeTargetWeightEhLipR2 As Single
        ''' <summary>El record trae FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\FV / LipTh. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightFVLipTh2Presente As Boolean
        ''' <summary>FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Property PhonemeTargetWeightFVLipTh2 As Single
        ''' <summary>El record trae FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\I. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightI2Presente As Boolean
        ''' <summary>FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\I</summary>
        Property PhonemeTargetWeightI2 As Single
        ''' <summary>El record trae FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\K. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightK2Presente As Boolean
        ''' <summary>FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\K</summary>
        Property PhonemeTargetWeightK2 As Single
        ''' <summary>El record trae FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\N. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightN2Presente As Boolean
        ''' <summary>FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\N</summary>
        Property PhonemeTargetWeightN2 As Single
        ''' <summary>El record trae FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\Oh. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightOh2Presente As Boolean
        ''' <summary>FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\Oh</summary>
        Property PhonemeTargetWeightOh2 As Single
        ''' <summary>El record trae FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\OohQ. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightOohQ2Presente As Boolean
        ''' <summary>FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\OohQ</summary>
        Property PhonemeTargetWeightOohQ2 As Single
        ''' <summary>El record trae FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\R. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightR2Presente As Boolean
        ''' <summary>FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\R</summary>
        Property PhonemeTargetWeightR2 As Single
        ''' <summary>El record trae FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\TH. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightTH2Presente As Boolean
        ''' <summary>FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\TH</summary>
        Property PhonemeTargetWeightTH2 As Single
        ''' <summary>El record trae FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\W. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightW2Presente As Boolean
        ''' <summary>FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\W</summary>
        Property PhonemeTargetWeightW2 As Single
        ''' <summary>El record trae FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\Aah / LipBigAah. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightAahLipBigAah3Presente As Boolean
        ''' <summary>FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Property PhonemeTargetWeightAahLipBigAah3 As Single
        ''' <summary>El record trae FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\BigAah / LipDST. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightBigAahLipDST3Presente As Boolean
        ''' <summary>FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Property PhonemeTargetWeightBigAahLipDST3 As Single
        ''' <summary>El record trae FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\BMP / LipEee. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightBMPLipEee3Presente As Boolean
        ''' <summary>FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Property PhonemeTargetWeightBMPLipEee3 As Single
        ''' <summary>El record trae FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\ChJsh / LipFV. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightChJshLipFV3Presente As Boolean
        ''' <summary>FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Property PhonemeTargetWeightChJshLipFV3 As Single
        ''' <summary>El record trae FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\DST / LipK. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightDSTLipK3Presente As Boolean
        ''' <summary>FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Property PhonemeTargetWeightDSTLipK3 As Single
        ''' <summary>El record trae FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\Eee / LipL. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightEeeLipL3Presente As Boolean
        ''' <summary>FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Property PhonemeTargetWeightEeeLipL3 As Single
        ''' <summary>El record trae FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\Eh / LipR. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightEhLipR3Presente As Boolean
        ''' <summary>FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Property PhonemeTargetWeightEhLipR3 As Single
        ''' <summary>El record trae FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\FV / LipTh. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightFVLipTh3Presente As Boolean
        ''' <summary>FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Property PhonemeTargetWeightFVLipTh3 As Single
        ''' <summary>El record trae FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\I. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightI3Presente As Boolean
        ''' <summary>FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\I</summary>
        Property PhonemeTargetWeightI3 As Single
        ''' <summary>El record trae FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\K. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightK3Presente As Boolean
        ''' <summary>FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\K</summary>
        Property PhonemeTargetWeightK3 As Single
        ''' <summary>El record trae FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\N. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightN3Presente As Boolean
        ''' <summary>FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\N</summary>
        Property PhonemeTargetWeightN3 As Single
        ''' <summary>El record trae FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\Oh. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightOh3Presente As Boolean
        ''' <summary>FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\Oh</summary>
        Property PhonemeTargetWeightOh3 As Single
        ''' <summary>El record trae FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\OohQ. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightOohQ3Presente As Boolean
        ''' <summary>FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\OohQ</summary>
        Property PhonemeTargetWeightOohQ3 As Single
        ''' <summary>El record trae FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\R. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightR3Presente As Boolean
        ''' <summary>FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\R</summary>
        Property PhonemeTargetWeightR3 As Single
        ''' <summary>El record trae FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\TH. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightTH3Presente As Boolean
        ''' <summary>FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\TH</summary>
        Property PhonemeTargetWeightTH3 As Single
        ''' <summary>El record trae FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\W. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightW3Presente As Boolean
        ''' <summary>FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\W</summary>
        Property PhonemeTargetWeightW3 As Single
        ''' <summary>El record trae FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\Aah / LipBigAah. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightAahLipBigAah4Presente As Boolean
        ''' <summary>FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Property PhonemeTargetWeightAahLipBigAah4 As Single
        ''' <summary>El record trae FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\BigAah / LipDST. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightBigAahLipDST4Presente As Boolean
        ''' <summary>FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Property PhonemeTargetWeightBigAahLipDST4 As Single
        ''' <summary>El record trae FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\BMP / LipEee. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightBMPLipEee4Presente As Boolean
        ''' <summary>FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Property PhonemeTargetWeightBMPLipEee4 As Single
        ''' <summary>El record trae FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\ChJsh / LipFV. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightChJshLipFV4Presente As Boolean
        ''' <summary>FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Property PhonemeTargetWeightChJshLipFV4 As Single
        ''' <summary>El record trae FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\DST / LipK. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightDSTLipK4Presente As Boolean
        ''' <summary>FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Property PhonemeTargetWeightDSTLipK4 As Single
        ''' <summary>El record trae FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\Eee / LipL. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightEeeLipL4Presente As Boolean
        ''' <summary>FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Property PhonemeTargetWeightEeeLipL4 As Single
        ''' <summary>El record trae FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\Eh / LipR. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightEhLipR4Presente As Boolean
        ''' <summary>FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Property PhonemeTargetWeightEhLipR4 As Single
        ''' <summary>El record trae FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\FV / LipTh. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightFVLipTh4Presente As Boolean
        ''' <summary>FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Property PhonemeTargetWeightFVLipTh4 As Single
        ''' <summary>El record trae FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\I. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightI4Presente As Boolean
        ''' <summary>FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\I</summary>
        Property PhonemeTargetWeightI4 As Single
        ''' <summary>El record trae FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\K. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightK4Presente As Boolean
        ''' <summary>FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\K</summary>
        Property PhonemeTargetWeightK4 As Single
        ''' <summary>El record trae FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\N. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightN4Presente As Boolean
        ''' <summary>FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\N</summary>
        Property PhonemeTargetWeightN4 As Single
        ''' <summary>El record trae FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\Oh. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightOh4Presente As Boolean
        ''' <summary>FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\Oh</summary>
        Property PhonemeTargetWeightOh4 As Single
        ''' <summary>El record trae FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\OohQ. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightOohQ4Presente As Boolean
        ''' <summary>FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\OohQ</summary>
        Property PhonemeTargetWeightOohQ4 As Single
        ''' <summary>El record trae FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\R. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightR4Presente As Boolean
        ''' <summary>FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\R</summary>
        Property PhonemeTargetWeightR4 As Single
        ''' <summary>El record trae FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\TH. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightTH4Presente As Boolean
        ''' <summary>FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\TH</summary>
        Property PhonemeTargetWeightTH4 As Single
        ''' <summary>El record trae FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\W. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightW4Presente As Boolean
        ''' <summary>FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\W</summary>
        Property PhonemeTargetWeightW4 As Single
        ''' <summary>El record trae FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\Aah / LipBigAah. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightAahLipBigAah5Presente As Boolean
        ''' <summary>FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Property PhonemeTargetWeightAahLipBigAah5 As Single
        ''' <summary>El record trae FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\BigAah / LipDST. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightBigAahLipDST5Presente As Boolean
        ''' <summary>FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Property PhonemeTargetWeightBigAahLipDST5 As Single
        ''' <summary>El record trae FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\BMP / LipEee. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightBMPLipEee5Presente As Boolean
        ''' <summary>FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Property PhonemeTargetWeightBMPLipEee5 As Single
        ''' <summary>El record trae FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\ChJsh / LipFV. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightChJshLipFV5Presente As Boolean
        ''' <summary>FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Property PhonemeTargetWeightChJshLipFV5 As Single
        ''' <summary>El record trae FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\DST / LipK. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightDSTLipK5Presente As Boolean
        ''' <summary>FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Property PhonemeTargetWeightDSTLipK5 As Single
        ''' <summary>El record trae FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\Eee / LipL. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightEeeLipL5Presente As Boolean
        ''' <summary>FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Property PhonemeTargetWeightEeeLipL5 As Single
        ''' <summary>El record trae FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\Eh / LipR. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightEhLipR5Presente As Boolean
        ''' <summary>FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Property PhonemeTargetWeightEhLipR5 As Single
        ''' <summary>El record trae FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\FV / LipTh. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightFVLipTh5Presente As Boolean
        ''' <summary>FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Property PhonemeTargetWeightFVLipTh5 As Single
        ''' <summary>El record trae FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\I. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightI5Presente As Boolean
        ''' <summary>FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\I</summary>
        Property PhonemeTargetWeightI5 As Single
        ''' <summary>El record trae FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\K. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightK5Presente As Boolean
        ''' <summary>FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\K</summary>
        Property PhonemeTargetWeightK5 As Single
        ''' <summary>El record trae FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\N. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightN5Presente As Boolean
        ''' <summary>FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\N</summary>
        Property PhonemeTargetWeightN5 As Single
        ''' <summary>El record trae FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\Oh. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightOh5Presente As Boolean
        ''' <summary>FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\Oh</summary>
        Property PhonemeTargetWeightOh5 As Single
        ''' <summary>El record trae FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\OohQ. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightOohQ5Presente As Boolean
        ''' <summary>FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\OohQ</summary>
        Property PhonemeTargetWeightOohQ5 As Single
        ''' <summary>El record trae FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\R. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightR5Presente As Boolean
        ''' <summary>FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\R</summary>
        Property PhonemeTargetWeightR5 As Single
        ''' <summary>El record trae FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\TH. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightTH5Presente As Boolean
        ''' <summary>FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\TH</summary>
        Property PhonemeTargetWeightTH5 As Single
        ''' <summary>El record trae FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\W. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightW5Presente As Boolean
        ''' <summary>FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\W</summary>
        Property PhonemeTargetWeightW5 As Single
        ''' <summary>El record trae FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\Aah / LipBigAah. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightAahLipBigAah6Presente As Boolean
        ''' <summary>FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Property PhonemeTargetWeightAahLipBigAah6 As Single
        ''' <summary>El record trae FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\BigAah / LipDST. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightBigAahLipDST6Presente As Boolean
        ''' <summary>FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Property PhonemeTargetWeightBigAahLipDST6 As Single
        ''' <summary>El record trae FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\BMP / LipEee. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightBMPLipEee6Presente As Boolean
        ''' <summary>FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Property PhonemeTargetWeightBMPLipEee6 As Single
        ''' <summary>El record trae FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\ChJsh / LipFV. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightChJshLipFV6Presente As Boolean
        ''' <summary>FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Property PhonemeTargetWeightChJshLipFV6 As Single
        ''' <summary>El record trae FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\DST / LipK. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightDSTLipK6Presente As Boolean
        ''' <summary>FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Property PhonemeTargetWeightDSTLipK6 As Single
        ''' <summary>El record trae FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\Eee / LipL. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightEeeLipL6Presente As Boolean
        ''' <summary>FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Property PhonemeTargetWeightEeeLipL6 As Single
        ''' <summary>El record trae FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\Eh / LipR. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightEhLipR6Presente As Boolean
        ''' <summary>FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Property PhonemeTargetWeightEhLipR6 As Single
        ''' <summary>El record trae FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\FV / LipTh. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightFVLipTh6Presente As Boolean
        ''' <summary>FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Property PhonemeTargetWeightFVLipTh6 As Single
        ''' <summary>El record trae FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\I. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightI6Presente As Boolean
        ''' <summary>FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\I</summary>
        Property PhonemeTargetWeightI6 As Single
        ''' <summary>El record trae FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\K. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightK6Presente As Boolean
        ''' <summary>FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\K</summary>
        Property PhonemeTargetWeightK6 As Single
        ''' <summary>El record trae FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\N. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightN6Presente As Boolean
        ''' <summary>FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\N</summary>
        Property PhonemeTargetWeightN6 As Single
        ''' <summary>El record trae FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\Oh. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightOh6Presente As Boolean
        ''' <summary>FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\Oh</summary>
        Property PhonemeTargetWeightOh6 As Single
        ''' <summary>El record trae FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\OohQ. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightOohQ6Presente As Boolean
        ''' <summary>FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\OohQ</summary>
        Property PhonemeTargetWeightOohQ6 As Single
        ''' <summary>El record trae FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\R. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightR6Presente As Boolean
        ''' <summary>FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\R</summary>
        Property PhonemeTargetWeightR6 As Single
        ''' <summary>El record trae FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\TH. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightTH6Presente As Boolean
        ''' <summary>FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\TH</summary>
        Property PhonemeTargetWeightTH6 As Single
        ''' <summary>El record trae FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\W. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightW6Presente As Boolean
        ''' <summary>FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\W</summary>
        Property PhonemeTargetWeightW6 As Single
        ''' <summary>El record trae FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\Aah / LipBigAah. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightAahLipBigAah7Presente As Boolean
        ''' <summary>FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Property PhonemeTargetWeightAahLipBigAah7 As Single
        ''' <summary>El record trae FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\BigAah / LipDST. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightBigAahLipDST7Presente As Boolean
        ''' <summary>FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Property PhonemeTargetWeightBigAahLipDST7 As Single
        ''' <summary>El record trae FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\BMP / LipEee. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightBMPLipEee7Presente As Boolean
        ''' <summary>FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Property PhonemeTargetWeightBMPLipEee7 As Single
        ''' <summary>El record trae FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\ChJsh / LipFV. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightChJshLipFV7Presente As Boolean
        ''' <summary>FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Property PhonemeTargetWeightChJshLipFV7 As Single
        ''' <summary>El record trae FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\DST / LipK. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightDSTLipK7Presente As Boolean
        ''' <summary>FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Property PhonemeTargetWeightDSTLipK7 As Single
        ''' <summary>El record trae FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\Eee / LipL. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightEeeLipL7Presente As Boolean
        ''' <summary>FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Property PhonemeTargetWeightEeeLipL7 As Single
        ''' <summary>El record trae FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\Eh / LipR. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightEhLipR7Presente As Boolean
        ''' <summary>FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Property PhonemeTargetWeightEhLipR7 As Single
        ''' <summary>El record trae FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\FV / LipTh. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightFVLipTh7Presente As Boolean
        ''' <summary>FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Property PhonemeTargetWeightFVLipTh7 As Single
        ''' <summary>El record trae FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\I. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightI7Presente As Boolean
        ''' <summary>FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\I</summary>
        Property PhonemeTargetWeightI7 As Single
        ''' <summary>El record trae FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\K. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightK7Presente As Boolean
        ''' <summary>FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\K</summary>
        Property PhonemeTargetWeightK7 As Single
        ''' <summary>El record trae FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\N. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightN7Presente As Boolean
        ''' <summary>FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\N</summary>
        Property PhonemeTargetWeightN7 As Single
        ''' <summary>El record trae FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\Oh. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightOh7Presente As Boolean
        ''' <summary>FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\Oh</summary>
        Property PhonemeTargetWeightOh7 As Single
        ''' <summary>El record trae FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\OohQ. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightOohQ7Presente As Boolean
        ''' <summary>FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\OohQ</summary>
        Property PhonemeTargetWeightOohQ7 As Single
        ''' <summary>El record trae FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\R. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightR7Presente As Boolean
        ''' <summary>FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\R</summary>
        Property PhonemeTargetWeightR7 As Single
        ''' <summary>El record trae FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\TH. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightTH7Presente As Boolean
        ''' <summary>FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\TH</summary>
        Property PhonemeTargetWeightTH7 As Single
        ''' <summary>El record trae FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\W. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightW7Presente As Boolean
        ''' <summary>FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\W</summary>
        Property PhonemeTargetWeightW7 As Single
        ''' <summary>El record trae FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\Aah / LipBigAah. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightAahLipBigAah8Presente As Boolean
        ''' <summary>FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Property PhonemeTargetWeightAahLipBigAah8 As Single
        ''' <summary>El record trae FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\BigAah / LipDST. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightBigAahLipDST8Presente As Boolean
        ''' <summary>FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Property PhonemeTargetWeightBigAahLipDST8 As Single
        ''' <summary>El record trae FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\BMP / LipEee. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightBMPLipEee8Presente As Boolean
        ''' <summary>FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Property PhonemeTargetWeightBMPLipEee8 As Single
        ''' <summary>El record trae FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\ChJsh / LipFV. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightChJshLipFV8Presente As Boolean
        ''' <summary>FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Property PhonemeTargetWeightChJshLipFV8 As Single
        ''' <summary>El record trae FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\DST / LipK. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightDSTLipK8Presente As Boolean
        ''' <summary>FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Property PhonemeTargetWeightDSTLipK8 As Single
        ''' <summary>El record trae FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\Eee / LipL. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightEeeLipL8Presente As Boolean
        ''' <summary>FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Property PhonemeTargetWeightEeeLipL8 As Single
        ''' <summary>El record trae FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\Eh / LipR. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightEhLipR8Presente As Boolean
        ''' <summary>FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Property PhonemeTargetWeightEhLipR8 As Single
        ''' <summary>El record trae FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\FV / LipTh. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightFVLipTh8Presente As Boolean
        ''' <summary>FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Property PhonemeTargetWeightFVLipTh8 As Single
        ''' <summary>El record trae FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\I. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightI8Presente As Boolean
        ''' <summary>FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\I</summary>
        Property PhonemeTargetWeightI8 As Single
        ''' <summary>El record trae FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\K. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightK8Presente As Boolean
        ''' <summary>FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\K</summary>
        Property PhonemeTargetWeightK8 As Single
        ''' <summary>El record trae FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\N. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightN8Presente As Boolean
        ''' <summary>FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\N</summary>
        Property PhonemeTargetWeightN8 As Single
        ''' <summary>El record trae FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\Oh. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightOh8Presente As Boolean
        ''' <summary>FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\Oh</summary>
        Property PhonemeTargetWeightOh8 As Single
        ''' <summary>El record trae FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\OohQ. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightOohQ8Presente As Boolean
        ''' <summary>FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\OohQ</summary>
        Property PhonemeTargetWeightOohQ8 As Single
        ''' <summary>El record trae FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\R. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightR8Presente As Boolean
        ''' <summary>FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\R</summary>
        Property PhonemeTargetWeightR8 As Single
        ''' <summary>El record trae FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\TH. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightTH8Presente As Boolean
        ''' <summary>FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\TH</summary>
        Property PhonemeTargetWeightTH8 As Single
        ''' <summary>El record trae FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\W. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightW8Presente As Boolean
        ''' <summary>FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\W</summary>
        Property PhonemeTargetWeightW8 As Single
        ''' <summary>El record trae FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\Aah / LipBigAah. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightAahLipBigAah9Presente As Boolean
        ''' <summary>FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Property PhonemeTargetWeightAahLipBigAah9 As Single
        ''' <summary>El record trae FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\BigAah / LipDST. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightBigAahLipDST9Presente As Boolean
        ''' <summary>FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Property PhonemeTargetWeightBigAahLipDST9 As Single
        ''' <summary>El record trae FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\BMP / LipEee. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightBMPLipEee9Presente As Boolean
        ''' <summary>FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Property PhonemeTargetWeightBMPLipEee9 As Single
        ''' <summary>El record trae FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\ChJsh / LipFV. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightChJshLipFV9Presente As Boolean
        ''' <summary>FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Property PhonemeTargetWeightChJshLipFV9 As Single
        ''' <summary>El record trae FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\DST / LipK. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightDSTLipK9Presente As Boolean
        ''' <summary>FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Property PhonemeTargetWeightDSTLipK9 As Single
        ''' <summary>El record trae FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\Eee / LipL. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightEeeLipL9Presente As Boolean
        ''' <summary>FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Property PhonemeTargetWeightEeeLipL9 As Single
        ''' <summary>El record trae FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\Eh / LipR. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightEhLipR9Presente As Boolean
        ''' <summary>FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Property PhonemeTargetWeightEhLipR9 As Single
        ''' <summary>El record trae FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\FV / LipTh. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightFVLipTh9Presente As Boolean
        ''' <summary>FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Property PhonemeTargetWeightFVLipTh9 As Single
        ''' <summary>El record trae FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\I. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightI9Presente As Boolean
        ''' <summary>FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\I</summary>
        Property PhonemeTargetWeightI9 As Single
        ''' <summary>El record trae FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\K. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightK9Presente As Boolean
        ''' <summary>FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\K</summary>
        Property PhonemeTargetWeightK9 As Single
        ''' <summary>El record trae FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\N. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightN9Presente As Boolean
        ''' <summary>FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\N</summary>
        Property PhonemeTargetWeightN9 As Single
        ''' <summary>El record trae FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\Oh. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightOh9Presente As Boolean
        ''' <summary>FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\Oh</summary>
        Property PhonemeTargetWeightOh9 As Single
        ''' <summary>El record trae FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\OohQ. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightOohQ9Presente As Boolean
        ''' <summary>FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\OohQ</summary>
        Property PhonemeTargetWeightOohQ9 As Single
        ''' <summary>El record trae FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\R. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightR9Presente As Boolean
        ''' <summary>FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\R</summary>
        Property PhonemeTargetWeightR9 As Single
        ''' <summary>El record trae FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\TH. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightTH9Presente As Boolean
        ''' <summary>FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\TH</summary>
        Property PhonemeTargetWeightTH9 As Single
        ''' <summary>El record trae FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\W. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightW9Presente As Boolean
        ''' <summary>FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\W</summary>
        Property PhonemeTargetWeightW9 As Single
        ''' <summary>El record trae FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\Aah / LipBigAah. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightAahLipBigAah10Presente As Boolean
        ''' <summary>FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Property PhonemeTargetWeightAahLipBigAah10 As Single
        ''' <summary>El record trae FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\BigAah / LipDST. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightBigAahLipDST10Presente As Boolean
        ''' <summary>FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Property PhonemeTargetWeightBigAahLipDST10 As Single
        ''' <summary>El record trae FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\BMP / LipEee. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightBMPLipEee10Presente As Boolean
        ''' <summary>FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Property PhonemeTargetWeightBMPLipEee10 As Single
        ''' <summary>El record trae FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\ChJsh / LipFV. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightChJshLipFV10Presente As Boolean
        ''' <summary>FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Property PhonemeTargetWeightChJshLipFV10 As Single
        ''' <summary>El record trae FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\DST / LipK. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightDSTLipK10Presente As Boolean
        ''' <summary>FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Property PhonemeTargetWeightDSTLipK10 As Single
        ''' <summary>El record trae FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\Eee / LipL. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightEeeLipL10Presente As Boolean
        ''' <summary>FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Property PhonemeTargetWeightEeeLipL10 As Single
        ''' <summary>El record trae FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\Eh / LipR. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightEhLipR10Presente As Boolean
        ''' <summary>FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Property PhonemeTargetWeightEhLipR10 As Single
        ''' <summary>El record trae FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\FV / LipTh. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightFVLipTh10Presente As Boolean
        ''' <summary>FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Property PhonemeTargetWeightFVLipTh10 As Single
        ''' <summary>El record trae FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\I. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightI10Presente As Boolean
        ''' <summary>FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\I</summary>
        Property PhonemeTargetWeightI10 As Single
        ''' <summary>El record trae FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\K. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightK10Presente As Boolean
        ''' <summary>FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\K</summary>
        Property PhonemeTargetWeightK10 As Single
        ''' <summary>El record trae FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\N. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightN10Presente As Boolean
        ''' <summary>FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\N</summary>
        Property PhonemeTargetWeightN10 As Single
        ''' <summary>El record trae FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\Oh. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightOh10Presente As Boolean
        ''' <summary>FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\Oh</summary>
        Property PhonemeTargetWeightOh10 As Single
        ''' <summary>El record trae FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\OohQ. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightOohQ10Presente As Boolean
        ''' <summary>FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\OohQ</summary>
        Property PhonemeTargetWeightOohQ10 As Single
        ''' <summary>El record trae FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\R. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightR10Presente As Boolean
        ''' <summary>FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\R</summary>
        Property PhonemeTargetWeightR10 As Single
        ''' <summary>El record trae FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\TH. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightTH10Presente As Boolean
        ''' <summary>FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\TH</summary>
        Property PhonemeTargetWeightTH10 As Single
        ''' <summary>El record trae FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\W. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightW10Presente As Boolean
        ''' <summary>FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\W</summary>
        Property PhonemeTargetWeightW10 As Single
        ''' <summary>El record trae FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\Aah / LipBigAah. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightAahLipBigAah11Presente As Boolean
        ''' <summary>FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Property PhonemeTargetWeightAahLipBigAah11 As Single
        ''' <summary>El record trae FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\BigAah / LipDST. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightBigAahLipDST11Presente As Boolean
        ''' <summary>FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Property PhonemeTargetWeightBigAahLipDST11 As Single
        ''' <summary>El record trae FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\BMP / LipEee. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightBMPLipEee11Presente As Boolean
        ''' <summary>FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Property PhonemeTargetWeightBMPLipEee11 As Single
        ''' <summary>El record trae FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\ChJsh / LipFV. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightChJshLipFV11Presente As Boolean
        ''' <summary>FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Property PhonemeTargetWeightChJshLipFV11 As Single
        ''' <summary>El record trae FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\DST / LipK. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightDSTLipK11Presente As Boolean
        ''' <summary>FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Property PhonemeTargetWeightDSTLipK11 As Single
        ''' <summary>El record trae FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\Eee / LipL. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightEeeLipL11Presente As Boolean
        ''' <summary>FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Property PhonemeTargetWeightEeeLipL11 As Single
        ''' <summary>El record trae FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\Eh / LipR. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightEhLipR11Presente As Boolean
        ''' <summary>FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Property PhonemeTargetWeightEhLipR11 As Single
        ''' <summary>El record trae FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\FV / LipTh. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightFVLipTh11Presente As Boolean
        ''' <summary>FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Property PhonemeTargetWeightFVLipTh11 As Single
        ''' <summary>El record trae FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\I. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightI11Presente As Boolean
        ''' <summary>FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\I</summary>
        Property PhonemeTargetWeightI11 As Single
        ''' <summary>El record trae FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\K. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightK11Presente As Boolean
        ''' <summary>FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\K</summary>
        Property PhonemeTargetWeightK11 As Single
        ''' <summary>El record trae FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\N. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightN11Presente As Boolean
        ''' <summary>FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\N</summary>
        Property PhonemeTargetWeightN11 As Single
        ''' <summary>El record trae FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\Oh. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightOh11Presente As Boolean
        ''' <summary>FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\Oh</summary>
        Property PhonemeTargetWeightOh11 As Single
        ''' <summary>El record trae FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\OohQ. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightOohQ11Presente As Boolean
        ''' <summary>FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\OohQ</summary>
        Property PhonemeTargetWeightOohQ11 As Single
        ''' <summary>El record trae FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\R. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightR11Presente As Boolean
        ''' <summary>FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\R</summary>
        Property PhonemeTargetWeightR11 As Single
        ''' <summary>El record trae FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\TH. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightTH11Presente As Boolean
        ''' <summary>FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\TH</summary>
        Property PhonemeTargetWeightTH11 As Single
        ''' <summary>El record trae FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\W. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightW11Presente As Boolean
        ''' <summary>FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\W</summary>
        Property PhonemeTargetWeightW11 As Single
        ''' <summary>El record trae FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\Aah / LipBigAah. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightAahLipBigAah12Presente As Boolean
        ''' <summary>FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Property PhonemeTargetWeightAahLipBigAah12 As Single
        ''' <summary>El record trae FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\BigAah / LipDST. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightBigAahLipDST12Presente As Boolean
        ''' <summary>FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Property PhonemeTargetWeightBigAahLipDST12 As Single
        ''' <summary>El record trae FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\BMP / LipEee. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightBMPLipEee12Presente As Boolean
        ''' <summary>FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Property PhonemeTargetWeightBMPLipEee12 As Single
        ''' <summary>El record trae FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\ChJsh / LipFV. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightChJshLipFV12Presente As Boolean
        ''' <summary>FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Property PhonemeTargetWeightChJshLipFV12 As Single
        ''' <summary>El record trae FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\DST / LipK. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightDSTLipK12Presente As Boolean
        ''' <summary>FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Property PhonemeTargetWeightDSTLipK12 As Single
        ''' <summary>El record trae FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\Eee / LipL. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightEeeLipL12Presente As Boolean
        ''' <summary>FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Property PhonemeTargetWeightEeeLipL12 As Single
        ''' <summary>El record trae FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\Eh / LipR. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightEhLipR12Presente As Boolean
        ''' <summary>FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Property PhonemeTargetWeightEhLipR12 As Single
        ''' <summary>El record trae FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\FV / LipTh. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightFVLipTh12Presente As Boolean
        ''' <summary>FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Property PhonemeTargetWeightFVLipTh12 As Single
        ''' <summary>El record trae FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\I. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightI12Presente As Boolean
        ''' <summary>FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\I</summary>
        Property PhonemeTargetWeightI12 As Single
        ''' <summary>El record trae FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\K. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightK12Presente As Boolean
        ''' <summary>FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\K</summary>
        Property PhonemeTargetWeightK12 As Single
        ''' <summary>El record trae FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\N. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightN12Presente As Boolean
        ''' <summary>FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\N</summary>
        Property PhonemeTargetWeightN12 As Single
        ''' <summary>El record trae FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\Oh. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightOh12Presente As Boolean
        ''' <summary>FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\Oh</summary>
        Property PhonemeTargetWeightOh12 As Single
        ''' <summary>El record trae FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\OohQ. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightOohQ12Presente As Boolean
        ''' <summary>FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\OohQ</summary>
        Property PhonemeTargetWeightOohQ12 As Single
        ''' <summary>El record trae FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\R. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightR12Presente As Boolean
        ''' <summary>FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\R</summary>
        Property PhonemeTargetWeightR12 As Single
        ''' <summary>El record trae FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\TH. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightTH12Presente As Boolean
        ''' <summary>FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\TH</summary>
        Property PhonemeTargetWeightTH12 As Single
        ''' <summary>El record trae FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\W. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightW12Presente As Boolean
        ''' <summary>FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\W</summary>
        Property PhonemeTargetWeightW12 As Single
        ''' <summary>El record trae FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\Aah / LipBigAah. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightAahLipBigAah13Presente As Boolean
        ''' <summary>FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Property PhonemeTargetWeightAahLipBigAah13 As Single
        ''' <summary>El record trae FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\BigAah / LipDST. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightBigAahLipDST13Presente As Boolean
        ''' <summary>FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Property PhonemeTargetWeightBigAahLipDST13 As Single
        ''' <summary>El record trae FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\BMP / LipEee. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightBMPLipEee13Presente As Boolean
        ''' <summary>FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Property PhonemeTargetWeightBMPLipEee13 As Single
        ''' <summary>El record trae FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\ChJsh / LipFV. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightChJshLipFV13Presente As Boolean
        ''' <summary>FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Property PhonemeTargetWeightChJshLipFV13 As Single
        ''' <summary>El record trae FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\DST / LipK. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightDSTLipK13Presente As Boolean
        ''' <summary>FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Property PhonemeTargetWeightDSTLipK13 As Single
        ''' <summary>El record trae FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\Eee / LipL. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightEeeLipL13Presente As Boolean
        ''' <summary>FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Property PhonemeTargetWeightEeeLipL13 As Single
        ''' <summary>El record trae FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\Eh / LipR. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightEhLipR13Presente As Boolean
        ''' <summary>FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Property PhonemeTargetWeightEhLipR13 As Single
        ''' <summary>El record trae FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\FV / LipTh. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightFVLipTh13Presente As Boolean
        ''' <summary>FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Property PhonemeTargetWeightFVLipTh13 As Single
        ''' <summary>El record trae FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\I. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightI13Presente As Boolean
        ''' <summary>FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\I</summary>
        Property PhonemeTargetWeightI13 As Single
        ''' <summary>El record trae FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\K. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightK13Presente As Boolean
        ''' <summary>FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\K</summary>
        Property PhonemeTargetWeightK13 As Single
        ''' <summary>El record trae FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\N. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightN13Presente As Boolean
        ''' <summary>FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\N</summary>
        Property PhonemeTargetWeightN13 As Single
        ''' <summary>El record trae FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\Oh. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightOh13Presente As Boolean
        ''' <summary>FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\Oh</summary>
        Property PhonemeTargetWeightOh13 As Single
        ''' <summary>El record trae FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\OohQ. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightOohQ13Presente As Boolean
        ''' <summary>FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\OohQ</summary>
        Property PhonemeTargetWeightOohQ13 As Single
        ''' <summary>El record trae FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\R. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightR13Presente As Boolean
        ''' <summary>FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\R</summary>
        Property PhonemeTargetWeightR13 As Single
        ''' <summary>El record trae FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\TH. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightTH13Presente As Boolean
        ''' <summary>FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\TH</summary>
        Property PhonemeTargetWeightTH13 As Single
        ''' <summary>El record trae FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\W. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightW13Presente As Boolean
        ''' <summary>FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\W</summary>
        Property PhonemeTargetWeightW13 As Single
        ''' <summary>El record trae FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\Aah / LipBigAah. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightAahLipBigAah14Presente As Boolean
        ''' <summary>FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Property PhonemeTargetWeightAahLipBigAah14 As Single
        ''' <summary>El record trae FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\BigAah / LipDST. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightBigAahLipDST14Presente As Boolean
        ''' <summary>FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Property PhonemeTargetWeightBigAahLipDST14 As Single
        ''' <summary>El record trae FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\BMP / LipEee. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightBMPLipEee14Presente As Boolean
        ''' <summary>FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Property PhonemeTargetWeightBMPLipEee14 As Single
        ''' <summary>El record trae FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\ChJsh / LipFV. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightChJshLipFV14Presente As Boolean
        ''' <summary>FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Property PhonemeTargetWeightChJshLipFV14 As Single
        ''' <summary>El record trae FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\DST / LipK. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightDSTLipK14Presente As Boolean
        ''' <summary>FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Property PhonemeTargetWeightDSTLipK14 As Single
        ''' <summary>El record trae FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\Eee / LipL. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightEeeLipL14Presente As Boolean
        ''' <summary>FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Property PhonemeTargetWeightEeeLipL14 As Single
        ''' <summary>El record trae FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\Eh / LipR. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightEhLipR14Presente As Boolean
        ''' <summary>FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Property PhonemeTargetWeightEhLipR14 As Single
        ''' <summary>El record trae FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\FV / LipTh. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightFVLipTh14Presente As Boolean
        ''' <summary>FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Property PhonemeTargetWeightFVLipTh14 As Single
        ''' <summary>El record trae FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\I. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightI14Presente As Boolean
        ''' <summary>FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\I</summary>
        Property PhonemeTargetWeightI14 As Single
        ''' <summary>El record trae FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\K. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightK14Presente As Boolean
        ''' <summary>FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\K</summary>
        Property PhonemeTargetWeightK14 As Single
        ''' <summary>El record trae FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\N. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightN14Presente As Boolean
        ''' <summary>FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\N</summary>
        Property PhonemeTargetWeightN14 As Single
        ''' <summary>El record trae FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\Oh. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightOh14Presente As Boolean
        ''' <summary>FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\Oh</summary>
        Property PhonemeTargetWeightOh14 As Single
        ''' <summary>El record trae FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\OohQ. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightOohQ14Presente As Boolean
        ''' <summary>FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\OohQ</summary>
        Property PhonemeTargetWeightOohQ14 As Single
        ''' <summary>El record trae FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\R. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightR14Presente As Boolean
        ''' <summary>FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\R</summary>
        Property PhonemeTargetWeightR14 As Single
        ''' <summary>El record trae FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\TH. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightTH14Presente As Boolean
        ''' <summary>FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\TH</summary>
        Property PhonemeTargetWeightTH14 As Single
        ''' <summary>El record trae FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\W. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightW14Presente As Boolean
        ''' <summary>FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\W</summary>
        Property PhonemeTargetWeightW14 As Single
        ''' <summary>El record trae FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\Aah / LipBigAah. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightAahLipBigAah15Presente As Boolean
        ''' <summary>FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Property PhonemeTargetWeightAahLipBigAah15 As Single
        ''' <summary>El record trae FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\BigAah / LipDST. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightBigAahLipDST15Presente As Boolean
        ''' <summary>FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Property PhonemeTargetWeightBigAahLipDST15 As Single
        ''' <summary>El record trae FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\BMP / LipEee. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightBMPLipEee15Presente As Boolean
        ''' <summary>FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Property PhonemeTargetWeightBMPLipEee15 As Single
        ''' <summary>El record trae FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\ChJsh / LipFV. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightChJshLipFV15Presente As Boolean
        ''' <summary>FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Property PhonemeTargetWeightChJshLipFV15 As Single
        ''' <summary>El record trae FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\DST / LipK. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightDSTLipK15Presente As Boolean
        ''' <summary>FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Property PhonemeTargetWeightDSTLipK15 As Single
        ''' <summary>El record trae FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\Eee / LipL. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightEeeLipL15Presente As Boolean
        ''' <summary>FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Property PhonemeTargetWeightEeeLipL15 As Single
        ''' <summary>El record trae FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\Eh / LipR. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightEhLipR15Presente As Boolean
        ''' <summary>FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Property PhonemeTargetWeightEhLipR15 As Single
        ''' <summary>El record trae FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\FV / LipTh. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightFVLipTh15Presente As Boolean
        ''' <summary>FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Property PhonemeTargetWeightFVLipTh15 As Single
        ''' <summary>El record trae FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\I. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightI15Presente As Boolean
        ''' <summary>FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\I</summary>
        Property PhonemeTargetWeightI15 As Single
        ''' <summary>El record trae FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\K. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightK15Presente As Boolean
        ''' <summary>FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\K</summary>
        Property PhonemeTargetWeightK15 As Single
        ''' <summary>El record trae FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\N. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightN15Presente As Boolean
        ''' <summary>FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\N</summary>
        Property PhonemeTargetWeightN15 As Single
        ''' <summary>El record trae FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\Oh. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightOh15Presente As Boolean
        ''' <summary>FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\Oh</summary>
        Property PhonemeTargetWeightOh15 As Single
        ''' <summary>El record trae FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\OohQ. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightOohQ15Presente As Boolean
        ''' <summary>FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\OohQ</summary>
        Property PhonemeTargetWeightOohQ15 As Single
        ''' <summary>El record trae FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\R. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightR15Presente As Boolean
        ''' <summary>FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\R</summary>
        Property PhonemeTargetWeightR15 As Single
        ''' <summary>El record trae FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\TH. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightTH15Presente As Boolean
        ''' <summary>FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\TH</summary>
        Property PhonemeTargetWeightTH15 As Single
        ''' <summary>El record trae FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\W. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightW15Presente As Boolean
        ''' <summary>FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\W</summary>
        Property PhonemeTargetWeightW15 As Single
        ''' <summary>El record trae FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\Aah / LipBigAah. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightAahLipBigAah16Presente As Boolean
        ''' <summary>FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Property PhonemeTargetWeightAahLipBigAah16 As Single
        ''' <summary>El record trae FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\BigAah / LipDST. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightBigAahLipDST16Presente As Boolean
        ''' <summary>FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Property PhonemeTargetWeightBigAahLipDST16 As Single
        ''' <summary>El record trae FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\BMP / LipEee. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightBMPLipEee16Presente As Boolean
        ''' <summary>FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Property PhonemeTargetWeightBMPLipEee16 As Single
        ''' <summary>El record trae FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\ChJsh / LipFV. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightChJshLipFV16Presente As Boolean
        ''' <summary>FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Property PhonemeTargetWeightChJshLipFV16 As Single
        ''' <summary>El record trae FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\DST / LipK. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightDSTLipK16Presente As Boolean
        ''' <summary>FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Property PhonemeTargetWeightDSTLipK16 As Single
        ''' <summary>El record trae FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\Eee / LipL. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightEeeLipL16Presente As Boolean
        ''' <summary>FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Property PhonemeTargetWeightEeeLipL16 As Single
        ''' <summary>El record trae FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\Eh / LipR. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightEhLipR16Presente As Boolean
        ''' <summary>FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Property PhonemeTargetWeightEhLipR16 As Single
        ''' <summary>El record trae FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\FV / LipTh. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightFVLipTh16Presente As Boolean
        ''' <summary>FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Property PhonemeTargetWeightFVLipTh16 As Single
        ''' <summary>El record trae FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\I. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightI16Presente As Boolean
        ''' <summary>FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\I</summary>
        Property PhonemeTargetWeightI16 As Single
        ''' <summary>El record trae FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\K. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightK16Presente As Boolean
        ''' <summary>FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\K</summary>
        Property PhonemeTargetWeightK16 As Single
        ''' <summary>El record trae FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\N. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightN16Presente As Boolean
        ''' <summary>FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\N</summary>
        Property PhonemeTargetWeightN16 As Single
        ''' <summary>El record trae FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\Oh. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightOh16Presente As Boolean
        ''' <summary>FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\Oh</summary>
        Property PhonemeTargetWeightOh16 As Single
        ''' <summary>El record trae FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\OohQ. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightOohQ16Presente As Boolean
        ''' <summary>FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\OohQ</summary>
        Property PhonemeTargetWeightOohQ16 As Single
        ''' <summary>El record trae FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\R. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightR16Presente As Boolean
        ''' <summary>FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\R</summary>
        Property PhonemeTargetWeightR16 As Single
        ''' <summary>El record trae FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\TH. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightTH16Presente As Boolean
        ''' <summary>FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\TH</summary>
        Property PhonemeTargetWeightTH16 As Single
        ''' <summary>El record trae FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\W. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightW16Presente As Boolean
        ''' <summary>FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\W</summary>
        Property PhonemeTargetWeightW16 As Single
        ''' <summary>El record trae FaceFX Phonemes\S\PHWT\Phoneme Target Weight\Aah / LipBigAah. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightAahLipBigAah17Presente As Boolean
        ''' <summary>FaceFX Phonemes\S\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Property PhonemeTargetWeightAahLipBigAah17 As Single
        ''' <summary>El record trae FaceFX Phonemes\S\PHWT\Phoneme Target Weight\BigAah / LipDST. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightBigAahLipDST17Presente As Boolean
        ''' <summary>FaceFX Phonemes\S\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Property PhonemeTargetWeightBigAahLipDST17 As Single
        ''' <summary>El record trae FaceFX Phonemes\S\PHWT\Phoneme Target Weight\BMP / LipEee. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightBMPLipEee17Presente As Boolean
        ''' <summary>FaceFX Phonemes\S\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Property PhonemeTargetWeightBMPLipEee17 As Single
        ''' <summary>El record trae FaceFX Phonemes\S\PHWT\Phoneme Target Weight\ChJsh / LipFV. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightChJshLipFV17Presente As Boolean
        ''' <summary>FaceFX Phonemes\S\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Property PhonemeTargetWeightChJshLipFV17 As Single
        ''' <summary>El record trae FaceFX Phonemes\S\PHWT\Phoneme Target Weight\DST / LipK. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightDSTLipK17Presente As Boolean
        ''' <summary>FaceFX Phonemes\S\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Property PhonemeTargetWeightDSTLipK17 As Single
        ''' <summary>El record trae FaceFX Phonemes\S\PHWT\Phoneme Target Weight\Eee / LipL. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightEeeLipL17Presente As Boolean
        ''' <summary>FaceFX Phonemes\S\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Property PhonemeTargetWeightEeeLipL17 As Single
        ''' <summary>El record trae FaceFX Phonemes\S\PHWT\Phoneme Target Weight\Eh / LipR. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightEhLipR17Presente As Boolean
        ''' <summary>FaceFX Phonemes\S\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Property PhonemeTargetWeightEhLipR17 As Single
        ''' <summary>El record trae FaceFX Phonemes\S\PHWT\Phoneme Target Weight\FV / LipTh. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightFVLipTh17Presente As Boolean
        ''' <summary>FaceFX Phonemes\S\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Property PhonemeTargetWeightFVLipTh17 As Single
        ''' <summary>El record trae FaceFX Phonemes\S\PHWT\Phoneme Target Weight\I. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightI17Presente As Boolean
        ''' <summary>FaceFX Phonemes\S\PHWT\Phoneme Target Weight\I</summary>
        Property PhonemeTargetWeightI17 As Single
        ''' <summary>El record trae FaceFX Phonemes\S\PHWT\Phoneme Target Weight\K. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightK17Presente As Boolean
        ''' <summary>FaceFX Phonemes\S\PHWT\Phoneme Target Weight\K</summary>
        Property PhonemeTargetWeightK17 As Single
        ''' <summary>El record trae FaceFX Phonemes\S\PHWT\Phoneme Target Weight\N. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightN17Presente As Boolean
        ''' <summary>FaceFX Phonemes\S\PHWT\Phoneme Target Weight\N</summary>
        Property PhonemeTargetWeightN17 As Single
        ''' <summary>El record trae FaceFX Phonemes\S\PHWT\Phoneme Target Weight\Oh. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightOh17Presente As Boolean
        ''' <summary>FaceFX Phonemes\S\PHWT\Phoneme Target Weight\Oh</summary>
        Property PhonemeTargetWeightOh17 As Single
        ''' <summary>El record trae FaceFX Phonemes\S\PHWT\Phoneme Target Weight\OohQ. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightOohQ17Presente As Boolean
        ''' <summary>FaceFX Phonemes\S\PHWT\Phoneme Target Weight\OohQ</summary>
        Property PhonemeTargetWeightOohQ17 As Single
        ''' <summary>El record trae FaceFX Phonemes\S\PHWT\Phoneme Target Weight\R. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightR17Presente As Boolean
        ''' <summary>FaceFX Phonemes\S\PHWT\Phoneme Target Weight\R</summary>
        Property PhonemeTargetWeightR17 As Single
        ''' <summary>El record trae FaceFX Phonemes\S\PHWT\Phoneme Target Weight\TH. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightTH17Presente As Boolean
        ''' <summary>FaceFX Phonemes\S\PHWT\Phoneme Target Weight\TH</summary>
        Property PhonemeTargetWeightTH17 As Single
        ''' <summary>El record trae FaceFX Phonemes\S\PHWT\Phoneme Target Weight\W. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightW17Presente As Boolean
        ''' <summary>FaceFX Phonemes\S\PHWT\Phoneme Target Weight\W</summary>
        Property PhonemeTargetWeightW17 As Single
        ''' <summary>El record trae FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\Aah / LipBigAah. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightAahLipBigAah18Presente As Boolean
        ''' <summary>FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Property PhonemeTargetWeightAahLipBigAah18 As Single
        ''' <summary>El record trae FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\BigAah / LipDST. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightBigAahLipDST18Presente As Boolean
        ''' <summary>FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Property PhonemeTargetWeightBigAahLipDST18 As Single
        ''' <summary>El record trae FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\BMP / LipEee. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightBMPLipEee18Presente As Boolean
        ''' <summary>FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Property PhonemeTargetWeightBMPLipEee18 As Single
        ''' <summary>El record trae FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\ChJsh / LipFV. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightChJshLipFV18Presente As Boolean
        ''' <summary>FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Property PhonemeTargetWeightChJshLipFV18 As Single
        ''' <summary>El record trae FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\DST / LipK. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightDSTLipK18Presente As Boolean
        ''' <summary>FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Property PhonemeTargetWeightDSTLipK18 As Single
        ''' <summary>El record trae FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\Eee / LipL. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightEeeLipL18Presente As Boolean
        ''' <summary>FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Property PhonemeTargetWeightEeeLipL18 As Single
        ''' <summary>El record trae FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\Eh / LipR. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightEhLipR18Presente As Boolean
        ''' <summary>FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Property PhonemeTargetWeightEhLipR18 As Single
        ''' <summary>El record trae FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\FV / LipTh. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightFVLipTh18Presente As Boolean
        ''' <summary>FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Property PhonemeTargetWeightFVLipTh18 As Single
        ''' <summary>El record trae FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\I. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightI18Presente As Boolean
        ''' <summary>FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\I</summary>
        Property PhonemeTargetWeightI18 As Single
        ''' <summary>El record trae FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\K. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightK18Presente As Boolean
        ''' <summary>FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\K</summary>
        Property PhonemeTargetWeightK18 As Single
        ''' <summary>El record trae FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\N. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightN18Presente As Boolean
        ''' <summary>FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\N</summary>
        Property PhonemeTargetWeightN18 As Single
        ''' <summary>El record trae FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\Oh. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightOh18Presente As Boolean
        ''' <summary>FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\Oh</summary>
        Property PhonemeTargetWeightOh18 As Single
        ''' <summary>El record trae FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\OohQ. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightOohQ18Presente As Boolean
        ''' <summary>FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\OohQ</summary>
        Property PhonemeTargetWeightOohQ18 As Single
        ''' <summary>El record trae FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\R. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightR18Presente As Boolean
        ''' <summary>FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\R</summary>
        Property PhonemeTargetWeightR18 As Single
        ''' <summary>El record trae FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\TH. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightTH18Presente As Boolean
        ''' <summary>FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\TH</summary>
        Property PhonemeTargetWeightTH18 As Single
        ''' <summary>El record trae FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\W. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightW18Presente As Boolean
        ''' <summary>FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\W</summary>
        Property PhonemeTargetWeightW18 As Single
        ''' <summary>El record trae FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\Aah / LipBigAah. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightAahLipBigAah19Presente As Boolean
        ''' <summary>FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Property PhonemeTargetWeightAahLipBigAah19 As Single
        ''' <summary>El record trae FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\BigAah / LipDST. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightBigAahLipDST19Presente As Boolean
        ''' <summary>FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Property PhonemeTargetWeightBigAahLipDST19 As Single
        ''' <summary>El record trae FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\BMP / LipEee. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightBMPLipEee19Presente As Boolean
        ''' <summary>FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Property PhonemeTargetWeightBMPLipEee19 As Single
        ''' <summary>El record trae FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\ChJsh / LipFV. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightChJshLipFV19Presente As Boolean
        ''' <summary>FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Property PhonemeTargetWeightChJshLipFV19 As Single
        ''' <summary>El record trae FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\DST / LipK. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightDSTLipK19Presente As Boolean
        ''' <summary>FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Property PhonemeTargetWeightDSTLipK19 As Single
        ''' <summary>El record trae FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\Eee / LipL. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightEeeLipL19Presente As Boolean
        ''' <summary>FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Property PhonemeTargetWeightEeeLipL19 As Single
        ''' <summary>El record trae FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\Eh / LipR. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightEhLipR19Presente As Boolean
        ''' <summary>FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Property PhonemeTargetWeightEhLipR19 As Single
        ''' <summary>El record trae FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\FV / LipTh. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightFVLipTh19Presente As Boolean
        ''' <summary>FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Property PhonemeTargetWeightFVLipTh19 As Single
        ''' <summary>El record trae FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\I. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightI19Presente As Boolean
        ''' <summary>FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\I</summary>
        Property PhonemeTargetWeightI19 As Single
        ''' <summary>El record trae FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\K. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightK19Presente As Boolean
        ''' <summary>FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\K</summary>
        Property PhonemeTargetWeightK19 As Single
        ''' <summary>El record trae FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\N. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightN19Presente As Boolean
        ''' <summary>FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\N</summary>
        Property PhonemeTargetWeightN19 As Single
        ''' <summary>El record trae FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\Oh. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightOh19Presente As Boolean
        ''' <summary>FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\Oh</summary>
        Property PhonemeTargetWeightOh19 As Single
        ''' <summary>El record trae FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\OohQ. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightOohQ19Presente As Boolean
        ''' <summary>FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\OohQ</summary>
        Property PhonemeTargetWeightOohQ19 As Single
        ''' <summary>El record trae FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\R. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightR19Presente As Boolean
        ''' <summary>FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\R</summary>
        Property PhonemeTargetWeightR19 As Single
        ''' <summary>El record trae FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\TH. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightTH19Presente As Boolean
        ''' <summary>FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\TH</summary>
        Property PhonemeTargetWeightTH19 As Single
        ''' <summary>El record trae FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\W. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightW19Presente As Boolean
        ''' <summary>FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\W</summary>
        Property PhonemeTargetWeightW19 As Single
        ''' <summary>El record trae FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\Aah / LipBigAah. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightAahLipBigAah20Presente As Boolean
        ''' <summary>FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Property PhonemeTargetWeightAahLipBigAah20 As Single
        ''' <summary>El record trae FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\BigAah / LipDST. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightBigAahLipDST20Presente As Boolean
        ''' <summary>FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Property PhonemeTargetWeightBigAahLipDST20 As Single
        ''' <summary>El record trae FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\BMP / LipEee. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightBMPLipEee20Presente As Boolean
        ''' <summary>FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Property PhonemeTargetWeightBMPLipEee20 As Single
        ''' <summary>El record trae FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\ChJsh / LipFV. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightChJshLipFV20Presente As Boolean
        ''' <summary>FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Property PhonemeTargetWeightChJshLipFV20 As Single
        ''' <summary>El record trae FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\DST / LipK. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightDSTLipK20Presente As Boolean
        ''' <summary>FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Property PhonemeTargetWeightDSTLipK20 As Single
        ''' <summary>El record trae FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\Eee / LipL. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightEeeLipL20Presente As Boolean
        ''' <summary>FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Property PhonemeTargetWeightEeeLipL20 As Single
        ''' <summary>El record trae FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\Eh / LipR. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightEhLipR20Presente As Boolean
        ''' <summary>FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Property PhonemeTargetWeightEhLipR20 As Single
        ''' <summary>El record trae FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\FV / LipTh. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightFVLipTh20Presente As Boolean
        ''' <summary>FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Property PhonemeTargetWeightFVLipTh20 As Single
        ''' <summary>El record trae FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\I. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightI20Presente As Boolean
        ''' <summary>FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\I</summary>
        Property PhonemeTargetWeightI20 As Single
        ''' <summary>El record trae FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\K. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightK20Presente As Boolean
        ''' <summary>FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\K</summary>
        Property PhonemeTargetWeightK20 As Single
        ''' <summary>El record trae FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\N. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightN20Presente As Boolean
        ''' <summary>FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\N</summary>
        Property PhonemeTargetWeightN20 As Single
        ''' <summary>El record trae FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\Oh. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightOh20Presente As Boolean
        ''' <summary>FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\Oh</summary>
        Property PhonemeTargetWeightOh20 As Single
        ''' <summary>El record trae FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\OohQ. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightOohQ20Presente As Boolean
        ''' <summary>FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\OohQ</summary>
        Property PhonemeTargetWeightOohQ20 As Single
        ''' <summary>El record trae FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\R. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightR20Presente As Boolean
        ''' <summary>FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\R</summary>
        Property PhonemeTargetWeightR20 As Single
        ''' <summary>El record trae FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\TH. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightTH20Presente As Boolean
        ''' <summary>FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\TH</summary>
        Property PhonemeTargetWeightTH20 As Single
        ''' <summary>El record trae FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\W. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightW20Presente As Boolean
        ''' <summary>FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\W</summary>
        Property PhonemeTargetWeightW20 As Single
        ''' <summary>El record trae FaceFX Phonemes\F\PHWT\Phoneme Target Weight\Aah / LipBigAah. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightAahLipBigAah21Presente As Boolean
        ''' <summary>FaceFX Phonemes\F\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Property PhonemeTargetWeightAahLipBigAah21 As Single
        ''' <summary>El record trae FaceFX Phonemes\F\PHWT\Phoneme Target Weight\BigAah / LipDST. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightBigAahLipDST21Presente As Boolean
        ''' <summary>FaceFX Phonemes\F\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Property PhonemeTargetWeightBigAahLipDST21 As Single
        ''' <summary>El record trae FaceFX Phonemes\F\PHWT\Phoneme Target Weight\BMP / LipEee. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightBMPLipEee21Presente As Boolean
        ''' <summary>FaceFX Phonemes\F\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Property PhonemeTargetWeightBMPLipEee21 As Single
        ''' <summary>El record trae FaceFX Phonemes\F\PHWT\Phoneme Target Weight\ChJsh / LipFV. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightChJshLipFV21Presente As Boolean
        ''' <summary>FaceFX Phonemes\F\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Property PhonemeTargetWeightChJshLipFV21 As Single
        ''' <summary>El record trae FaceFX Phonemes\F\PHWT\Phoneme Target Weight\DST / LipK. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightDSTLipK21Presente As Boolean
        ''' <summary>FaceFX Phonemes\F\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Property PhonemeTargetWeightDSTLipK21 As Single
        ''' <summary>El record trae FaceFX Phonemes\F\PHWT\Phoneme Target Weight\Eee / LipL. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightEeeLipL21Presente As Boolean
        ''' <summary>FaceFX Phonemes\F\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Property PhonemeTargetWeightEeeLipL21 As Single
        ''' <summary>El record trae FaceFX Phonemes\F\PHWT\Phoneme Target Weight\Eh / LipR. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightEhLipR21Presente As Boolean
        ''' <summary>FaceFX Phonemes\F\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Property PhonemeTargetWeightEhLipR21 As Single
        ''' <summary>El record trae FaceFX Phonemes\F\PHWT\Phoneme Target Weight\FV / LipTh. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightFVLipTh21Presente As Boolean
        ''' <summary>FaceFX Phonemes\F\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Property PhonemeTargetWeightFVLipTh21 As Single
        ''' <summary>El record trae FaceFX Phonemes\F\PHWT\Phoneme Target Weight\I. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightI21Presente As Boolean
        ''' <summary>FaceFX Phonemes\F\PHWT\Phoneme Target Weight\I</summary>
        Property PhonemeTargetWeightI21 As Single
        ''' <summary>El record trae FaceFX Phonemes\F\PHWT\Phoneme Target Weight\K. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightK21Presente As Boolean
        ''' <summary>FaceFX Phonemes\F\PHWT\Phoneme Target Weight\K</summary>
        Property PhonemeTargetWeightK21 As Single
        ''' <summary>El record trae FaceFX Phonemes\F\PHWT\Phoneme Target Weight\N. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightN21Presente As Boolean
        ''' <summary>FaceFX Phonemes\F\PHWT\Phoneme Target Weight\N</summary>
        Property PhonemeTargetWeightN21 As Single
        ''' <summary>El record trae FaceFX Phonemes\F\PHWT\Phoneme Target Weight\Oh. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightOh21Presente As Boolean
        ''' <summary>FaceFX Phonemes\F\PHWT\Phoneme Target Weight\Oh</summary>
        Property PhonemeTargetWeightOh21 As Single
        ''' <summary>El record trae FaceFX Phonemes\F\PHWT\Phoneme Target Weight\OohQ. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightOohQ21Presente As Boolean
        ''' <summary>FaceFX Phonemes\F\PHWT\Phoneme Target Weight\OohQ</summary>
        Property PhonemeTargetWeightOohQ21 As Single
        ''' <summary>El record trae FaceFX Phonemes\F\PHWT\Phoneme Target Weight\R. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightR21Presente As Boolean
        ''' <summary>FaceFX Phonemes\F\PHWT\Phoneme Target Weight\R</summary>
        Property PhonemeTargetWeightR21 As Single
        ''' <summary>El record trae FaceFX Phonemes\F\PHWT\Phoneme Target Weight\TH. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightTH21Presente As Boolean
        ''' <summary>FaceFX Phonemes\F\PHWT\Phoneme Target Weight\TH</summary>
        Property PhonemeTargetWeightTH21 As Single
        ''' <summary>El record trae FaceFX Phonemes\F\PHWT\Phoneme Target Weight\W. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightW21Presente As Boolean
        ''' <summary>FaceFX Phonemes\F\PHWT\Phoneme Target Weight\W</summary>
        Property PhonemeTargetWeightW21 As Single
        ''' <summary>El record trae FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\Aah / LipBigAah. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightAahLipBigAah22Presente As Boolean
        ''' <summary>FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Property PhonemeTargetWeightAahLipBigAah22 As Single
        ''' <summary>El record trae FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\BigAah / LipDST. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightBigAahLipDST22Presente As Boolean
        ''' <summary>FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Property PhonemeTargetWeightBigAahLipDST22 As Single
        ''' <summary>El record trae FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\BMP / LipEee. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightBMPLipEee22Presente As Boolean
        ''' <summary>FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Property PhonemeTargetWeightBMPLipEee22 As Single
        ''' <summary>El record trae FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\ChJsh / LipFV. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightChJshLipFV22Presente As Boolean
        ''' <summary>FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Property PhonemeTargetWeightChJshLipFV22 As Single
        ''' <summary>El record trae FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\DST / LipK. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightDSTLipK22Presente As Boolean
        ''' <summary>FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Property PhonemeTargetWeightDSTLipK22 As Single
        ''' <summary>El record trae FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\Eee / LipL. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightEeeLipL22Presente As Boolean
        ''' <summary>FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Property PhonemeTargetWeightEeeLipL22 As Single
        ''' <summary>El record trae FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\Eh / LipR. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightEhLipR22Presente As Boolean
        ''' <summary>FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Property PhonemeTargetWeightEhLipR22 As Single
        ''' <summary>El record trae FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\FV / LipTh. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightFVLipTh22Presente As Boolean
        ''' <summary>FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Property PhonemeTargetWeightFVLipTh22 As Single
        ''' <summary>El record trae FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\I. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightI22Presente As Boolean
        ''' <summary>FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\I</summary>
        Property PhonemeTargetWeightI22 As Single
        ''' <summary>El record trae FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\K. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightK22Presente As Boolean
        ''' <summary>FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\K</summary>
        Property PhonemeTargetWeightK22 As Single
        ''' <summary>El record trae FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\N. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightN22Presente As Boolean
        ''' <summary>FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\N</summary>
        Property PhonemeTargetWeightN22 As Single
        ''' <summary>El record trae FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\Oh. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightOh22Presente As Boolean
        ''' <summary>FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\Oh</summary>
        Property PhonemeTargetWeightOh22 As Single
        ''' <summary>El record trae FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\OohQ. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightOohQ22Presente As Boolean
        ''' <summary>FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\OohQ</summary>
        Property PhonemeTargetWeightOohQ22 As Single
        ''' <summary>El record trae FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\R. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightR22Presente As Boolean
        ''' <summary>FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\R</summary>
        Property PhonemeTargetWeightR22 As Single
        ''' <summary>El record trae FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\TH. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightTH22Presente As Boolean
        ''' <summary>FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\TH</summary>
        Property PhonemeTargetWeightTH22 As Single
        ''' <summary>El record trae FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\W. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightW22Presente As Boolean
        ''' <summary>FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\W</summary>
        Property PhonemeTargetWeightW22 As Single
        ''' <summary>El record trae FaceFX Phonemes\V\PHWT\Phoneme Target Weight\Aah / LipBigAah. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightAahLipBigAah23Presente As Boolean
        ''' <summary>FaceFX Phonemes\V\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Property PhonemeTargetWeightAahLipBigAah23 As Single
        ''' <summary>El record trae FaceFX Phonemes\V\PHWT\Phoneme Target Weight\BigAah / LipDST. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightBigAahLipDST23Presente As Boolean
        ''' <summary>FaceFX Phonemes\V\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Property PhonemeTargetWeightBigAahLipDST23 As Single
        ''' <summary>El record trae FaceFX Phonemes\V\PHWT\Phoneme Target Weight\BMP / LipEee. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightBMPLipEee23Presente As Boolean
        ''' <summary>FaceFX Phonemes\V\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Property PhonemeTargetWeightBMPLipEee23 As Single
        ''' <summary>El record trae FaceFX Phonemes\V\PHWT\Phoneme Target Weight\ChJsh / LipFV. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightChJshLipFV23Presente As Boolean
        ''' <summary>FaceFX Phonemes\V\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Property PhonemeTargetWeightChJshLipFV23 As Single
        ''' <summary>El record trae FaceFX Phonemes\V\PHWT\Phoneme Target Weight\DST / LipK. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightDSTLipK23Presente As Boolean
        ''' <summary>FaceFX Phonemes\V\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Property PhonemeTargetWeightDSTLipK23 As Single
        ''' <summary>El record trae FaceFX Phonemes\V\PHWT\Phoneme Target Weight\Eee / LipL. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightEeeLipL23Presente As Boolean
        ''' <summary>FaceFX Phonemes\V\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Property PhonemeTargetWeightEeeLipL23 As Single
        ''' <summary>El record trae FaceFX Phonemes\V\PHWT\Phoneme Target Weight\Eh / LipR. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightEhLipR23Presente As Boolean
        ''' <summary>FaceFX Phonemes\V\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Property PhonemeTargetWeightEhLipR23 As Single
        ''' <summary>El record trae FaceFX Phonemes\V\PHWT\Phoneme Target Weight\FV / LipTh. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightFVLipTh23Presente As Boolean
        ''' <summary>FaceFX Phonemes\V\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Property PhonemeTargetWeightFVLipTh23 As Single
        ''' <summary>El record trae FaceFX Phonemes\V\PHWT\Phoneme Target Weight\I. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightI23Presente As Boolean
        ''' <summary>FaceFX Phonemes\V\PHWT\Phoneme Target Weight\I</summary>
        Property PhonemeTargetWeightI23 As Single
        ''' <summary>El record trae FaceFX Phonemes\V\PHWT\Phoneme Target Weight\K. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightK23Presente As Boolean
        ''' <summary>FaceFX Phonemes\V\PHWT\Phoneme Target Weight\K</summary>
        Property PhonemeTargetWeightK23 As Single
        ''' <summary>El record trae FaceFX Phonemes\V\PHWT\Phoneme Target Weight\N. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightN23Presente As Boolean
        ''' <summary>FaceFX Phonemes\V\PHWT\Phoneme Target Weight\N</summary>
        Property PhonemeTargetWeightN23 As Single
        ''' <summary>El record trae FaceFX Phonemes\V\PHWT\Phoneme Target Weight\Oh. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightOh23Presente As Boolean
        ''' <summary>FaceFX Phonemes\V\PHWT\Phoneme Target Weight\Oh</summary>
        Property PhonemeTargetWeightOh23 As Single
        ''' <summary>El record trae FaceFX Phonemes\V\PHWT\Phoneme Target Weight\OohQ. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightOohQ23Presente As Boolean
        ''' <summary>FaceFX Phonemes\V\PHWT\Phoneme Target Weight\OohQ</summary>
        Property PhonemeTargetWeightOohQ23 As Single
        ''' <summary>El record trae FaceFX Phonemes\V\PHWT\Phoneme Target Weight\R. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightR23Presente As Boolean
        ''' <summary>FaceFX Phonemes\V\PHWT\Phoneme Target Weight\R</summary>
        Property PhonemeTargetWeightR23 As Single
        ''' <summary>El record trae FaceFX Phonemes\V\PHWT\Phoneme Target Weight\TH. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightTH23Presente As Boolean
        ''' <summary>FaceFX Phonemes\V\PHWT\Phoneme Target Weight\TH</summary>
        Property PhonemeTargetWeightTH23 As Single
        ''' <summary>El record trae FaceFX Phonemes\V\PHWT\Phoneme Target Weight\W. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightW23Presente As Boolean
        ''' <summary>FaceFX Phonemes\V\PHWT\Phoneme Target Weight\W</summary>
        Property PhonemeTargetWeightW23 As Single
        ''' <summary>El record trae FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\Aah / LipBigAah. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightAahLipBigAah24Presente As Boolean
        ''' <summary>FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Property PhonemeTargetWeightAahLipBigAah24 As Single
        ''' <summary>El record trae FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\BigAah / LipDST. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightBigAahLipDST24Presente As Boolean
        ''' <summary>FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Property PhonemeTargetWeightBigAahLipDST24 As Single
        ''' <summary>El record trae FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\BMP / LipEee. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightBMPLipEee24Presente As Boolean
        ''' <summary>FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Property PhonemeTargetWeightBMPLipEee24 As Single
        ''' <summary>El record trae FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\ChJsh / LipFV. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightChJshLipFV24Presente As Boolean
        ''' <summary>FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Property PhonemeTargetWeightChJshLipFV24 As Single
        ''' <summary>El record trae FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\DST / LipK. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightDSTLipK24Presente As Boolean
        ''' <summary>FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Property PhonemeTargetWeightDSTLipK24 As Single
        ''' <summary>El record trae FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\Eee / LipL. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightEeeLipL24Presente As Boolean
        ''' <summary>FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Property PhonemeTargetWeightEeeLipL24 As Single
        ''' <summary>El record trae FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\Eh / LipR. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightEhLipR24Presente As Boolean
        ''' <summary>FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Property PhonemeTargetWeightEhLipR24 As Single
        ''' <summary>El record trae FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\FV / LipTh. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightFVLipTh24Presente As Boolean
        ''' <summary>FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Property PhonemeTargetWeightFVLipTh24 As Single
        ''' <summary>El record trae FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\I. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightI24Presente As Boolean
        ''' <summary>FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\I</summary>
        Property PhonemeTargetWeightI24 As Single
        ''' <summary>El record trae FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\K. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightK24Presente As Boolean
        ''' <summary>FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\K</summary>
        Property PhonemeTargetWeightK24 As Single
        ''' <summary>El record trae FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\N. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightN24Presente As Boolean
        ''' <summary>FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\N</summary>
        Property PhonemeTargetWeightN24 As Single
        ''' <summary>El record trae FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\Oh. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightOh24Presente As Boolean
        ''' <summary>FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\Oh</summary>
        Property PhonemeTargetWeightOh24 As Single
        ''' <summary>El record trae FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\OohQ. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightOohQ24Presente As Boolean
        ''' <summary>FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\OohQ</summary>
        Property PhonemeTargetWeightOohQ24 As Single
        ''' <summary>El record trae FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\R. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightR24Presente As Boolean
        ''' <summary>FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\R</summary>
        Property PhonemeTargetWeightR24 As Single
        ''' <summary>El record trae FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\TH. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightTH24Presente As Boolean
        ''' <summary>FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\TH</summary>
        Property PhonemeTargetWeightTH24 As Single
        ''' <summary>El record trae FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\W. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightW24Presente As Boolean
        ''' <summary>FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\W</summary>
        Property PhonemeTargetWeightW24 As Single
        ''' <summary>El record trae FaceFX Phonemes\M\PHWT\Phoneme Target Weight\Aah / LipBigAah. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightAahLipBigAah25Presente As Boolean
        ''' <summary>FaceFX Phonemes\M\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Property PhonemeTargetWeightAahLipBigAah25 As Single
        ''' <summary>El record trae FaceFX Phonemes\M\PHWT\Phoneme Target Weight\BigAah / LipDST. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightBigAahLipDST25Presente As Boolean
        ''' <summary>FaceFX Phonemes\M\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Property PhonemeTargetWeightBigAahLipDST25 As Single
        ''' <summary>El record trae FaceFX Phonemes\M\PHWT\Phoneme Target Weight\BMP / LipEee. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightBMPLipEee25Presente As Boolean
        ''' <summary>FaceFX Phonemes\M\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Property PhonemeTargetWeightBMPLipEee25 As Single
        ''' <summary>El record trae FaceFX Phonemes\M\PHWT\Phoneme Target Weight\ChJsh / LipFV. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightChJshLipFV25Presente As Boolean
        ''' <summary>FaceFX Phonemes\M\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Property PhonemeTargetWeightChJshLipFV25 As Single
        ''' <summary>El record trae FaceFX Phonemes\M\PHWT\Phoneme Target Weight\DST / LipK. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightDSTLipK25Presente As Boolean
        ''' <summary>FaceFX Phonemes\M\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Property PhonemeTargetWeightDSTLipK25 As Single
        ''' <summary>El record trae FaceFX Phonemes\M\PHWT\Phoneme Target Weight\Eee / LipL. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightEeeLipL25Presente As Boolean
        ''' <summary>FaceFX Phonemes\M\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Property PhonemeTargetWeightEeeLipL25 As Single
        ''' <summary>El record trae FaceFX Phonemes\M\PHWT\Phoneme Target Weight\Eh / LipR. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightEhLipR25Presente As Boolean
        ''' <summary>FaceFX Phonemes\M\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Property PhonemeTargetWeightEhLipR25 As Single
        ''' <summary>El record trae FaceFX Phonemes\M\PHWT\Phoneme Target Weight\FV / LipTh. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightFVLipTh25Presente As Boolean
        ''' <summary>FaceFX Phonemes\M\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Property PhonemeTargetWeightFVLipTh25 As Single
        ''' <summary>El record trae FaceFX Phonemes\M\PHWT\Phoneme Target Weight\I. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightI25Presente As Boolean
        ''' <summary>FaceFX Phonemes\M\PHWT\Phoneme Target Weight\I</summary>
        Property PhonemeTargetWeightI25 As Single
        ''' <summary>El record trae FaceFX Phonemes\M\PHWT\Phoneme Target Weight\K. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightK25Presente As Boolean
        ''' <summary>FaceFX Phonemes\M\PHWT\Phoneme Target Weight\K</summary>
        Property PhonemeTargetWeightK25 As Single
        ''' <summary>El record trae FaceFX Phonemes\M\PHWT\Phoneme Target Weight\N. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightN25Presente As Boolean
        ''' <summary>FaceFX Phonemes\M\PHWT\Phoneme Target Weight\N</summary>
        Property PhonemeTargetWeightN25 As Single
        ''' <summary>El record trae FaceFX Phonemes\M\PHWT\Phoneme Target Weight\Oh. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightOh25Presente As Boolean
        ''' <summary>FaceFX Phonemes\M\PHWT\Phoneme Target Weight\Oh</summary>
        Property PhonemeTargetWeightOh25 As Single
        ''' <summary>El record trae FaceFX Phonemes\M\PHWT\Phoneme Target Weight\OohQ. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightOohQ25Presente As Boolean
        ''' <summary>FaceFX Phonemes\M\PHWT\Phoneme Target Weight\OohQ</summary>
        Property PhonemeTargetWeightOohQ25 As Single
        ''' <summary>El record trae FaceFX Phonemes\M\PHWT\Phoneme Target Weight\R. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightR25Presente As Boolean
        ''' <summary>FaceFX Phonemes\M\PHWT\Phoneme Target Weight\R</summary>
        Property PhonemeTargetWeightR25 As Single
        ''' <summary>El record trae FaceFX Phonemes\M\PHWT\Phoneme Target Weight\TH. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightTH25Presente As Boolean
        ''' <summary>FaceFX Phonemes\M\PHWT\Phoneme Target Weight\TH</summary>
        Property PhonemeTargetWeightTH25 As Single
        ''' <summary>El record trae FaceFX Phonemes\M\PHWT\Phoneme Target Weight\W. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightW25Presente As Boolean
        ''' <summary>FaceFX Phonemes\M\PHWT\Phoneme Target Weight\W</summary>
        Property PhonemeTargetWeightW25 As Single
        ''' <summary>El record trae FaceFX Phonemes\N\PHWT\Phoneme Target Weight\Aah / LipBigAah. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightAahLipBigAah26Presente As Boolean
        ''' <summary>FaceFX Phonemes\N\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Property PhonemeTargetWeightAahLipBigAah26 As Single
        ''' <summary>El record trae FaceFX Phonemes\N\PHWT\Phoneme Target Weight\BigAah / LipDST. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightBigAahLipDST26Presente As Boolean
        ''' <summary>FaceFX Phonemes\N\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Property PhonemeTargetWeightBigAahLipDST26 As Single
        ''' <summary>El record trae FaceFX Phonemes\N\PHWT\Phoneme Target Weight\BMP / LipEee. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightBMPLipEee26Presente As Boolean
        ''' <summary>FaceFX Phonemes\N\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Property PhonemeTargetWeightBMPLipEee26 As Single
        ''' <summary>El record trae FaceFX Phonemes\N\PHWT\Phoneme Target Weight\ChJsh / LipFV. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightChJshLipFV26Presente As Boolean
        ''' <summary>FaceFX Phonemes\N\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Property PhonemeTargetWeightChJshLipFV26 As Single
        ''' <summary>El record trae FaceFX Phonemes\N\PHWT\Phoneme Target Weight\DST / LipK. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightDSTLipK26Presente As Boolean
        ''' <summary>FaceFX Phonemes\N\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Property PhonemeTargetWeightDSTLipK26 As Single
        ''' <summary>El record trae FaceFX Phonemes\N\PHWT\Phoneme Target Weight\Eee / LipL. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightEeeLipL26Presente As Boolean
        ''' <summary>FaceFX Phonemes\N\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Property PhonemeTargetWeightEeeLipL26 As Single
        ''' <summary>El record trae FaceFX Phonemes\N\PHWT\Phoneme Target Weight\Eh / LipR. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightEhLipR26Presente As Boolean
        ''' <summary>FaceFX Phonemes\N\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Property PhonemeTargetWeightEhLipR26 As Single
        ''' <summary>El record trae FaceFX Phonemes\N\PHWT\Phoneme Target Weight\FV / LipTh. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightFVLipTh26Presente As Boolean
        ''' <summary>FaceFX Phonemes\N\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Property PhonemeTargetWeightFVLipTh26 As Single
        ''' <summary>El record trae FaceFX Phonemes\N\PHWT\Phoneme Target Weight\I. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightI26Presente As Boolean
        ''' <summary>FaceFX Phonemes\N\PHWT\Phoneme Target Weight\I</summary>
        Property PhonemeTargetWeightI26 As Single
        ''' <summary>El record trae FaceFX Phonemes\N\PHWT\Phoneme Target Weight\K. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightK26Presente As Boolean
        ''' <summary>FaceFX Phonemes\N\PHWT\Phoneme Target Weight\K</summary>
        Property PhonemeTargetWeightK26 As Single
        ''' <summary>El record trae FaceFX Phonemes\N\PHWT\Phoneme Target Weight\N. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightN26Presente As Boolean
        ''' <summary>FaceFX Phonemes\N\PHWT\Phoneme Target Weight\N</summary>
        Property PhonemeTargetWeightN26 As Single
        ''' <summary>El record trae FaceFX Phonemes\N\PHWT\Phoneme Target Weight\Oh. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightOh26Presente As Boolean
        ''' <summary>FaceFX Phonemes\N\PHWT\Phoneme Target Weight\Oh</summary>
        Property PhonemeTargetWeightOh26 As Single
        ''' <summary>El record trae FaceFX Phonemes\N\PHWT\Phoneme Target Weight\OohQ. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightOohQ26Presente As Boolean
        ''' <summary>FaceFX Phonemes\N\PHWT\Phoneme Target Weight\OohQ</summary>
        Property PhonemeTargetWeightOohQ26 As Single
        ''' <summary>El record trae FaceFX Phonemes\N\PHWT\Phoneme Target Weight\R. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightR26Presente As Boolean
        ''' <summary>FaceFX Phonemes\N\PHWT\Phoneme Target Weight\R</summary>
        Property PhonemeTargetWeightR26 As Single
        ''' <summary>El record trae FaceFX Phonemes\N\PHWT\Phoneme Target Weight\TH. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightTH26Presente As Boolean
        ''' <summary>FaceFX Phonemes\N\PHWT\Phoneme Target Weight\TH</summary>
        Property PhonemeTargetWeightTH26 As Single
        ''' <summary>El record trae FaceFX Phonemes\N\PHWT\Phoneme Target Weight\W. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightW26Presente As Boolean
        ''' <summary>FaceFX Phonemes\N\PHWT\Phoneme Target Weight\W</summary>
        Property PhonemeTargetWeightW26 As Single
        ''' <summary>El record trae FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\Aah / LipBigAah. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightAahLipBigAah27Presente As Boolean
        ''' <summary>FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Property PhonemeTargetWeightAahLipBigAah27 As Single
        ''' <summary>El record trae FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\BigAah / LipDST. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightBigAahLipDST27Presente As Boolean
        ''' <summary>FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Property PhonemeTargetWeightBigAahLipDST27 As Single
        ''' <summary>El record trae FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\BMP / LipEee. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightBMPLipEee27Presente As Boolean
        ''' <summary>FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Property PhonemeTargetWeightBMPLipEee27 As Single
        ''' <summary>El record trae FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\ChJsh / LipFV. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightChJshLipFV27Presente As Boolean
        ''' <summary>FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Property PhonemeTargetWeightChJshLipFV27 As Single
        ''' <summary>El record trae FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\DST / LipK. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightDSTLipK27Presente As Boolean
        ''' <summary>FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Property PhonemeTargetWeightDSTLipK27 As Single
        ''' <summary>El record trae FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\Eee / LipL. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightEeeLipL27Presente As Boolean
        ''' <summary>FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Property PhonemeTargetWeightEeeLipL27 As Single
        ''' <summary>El record trae FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\Eh / LipR. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightEhLipR27Presente As Boolean
        ''' <summary>FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Property PhonemeTargetWeightEhLipR27 As Single
        ''' <summary>El record trae FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\FV / LipTh. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightFVLipTh27Presente As Boolean
        ''' <summary>FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Property PhonemeTargetWeightFVLipTh27 As Single
        ''' <summary>El record trae FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\I. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightI27Presente As Boolean
        ''' <summary>FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\I</summary>
        Property PhonemeTargetWeightI27 As Single
        ''' <summary>El record trae FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\K. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightK27Presente As Boolean
        ''' <summary>FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\K</summary>
        Property PhonemeTargetWeightK27 As Single
        ''' <summary>El record trae FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\N. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightN27Presente As Boolean
        ''' <summary>FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\N</summary>
        Property PhonemeTargetWeightN27 As Single
        ''' <summary>El record trae FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\Oh. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightOh27Presente As Boolean
        ''' <summary>FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\Oh</summary>
        Property PhonemeTargetWeightOh27 As Single
        ''' <summary>El record trae FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\OohQ. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightOohQ27Presente As Boolean
        ''' <summary>FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\OohQ</summary>
        Property PhonemeTargetWeightOohQ27 As Single
        ''' <summary>El record trae FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\R. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightR27Presente As Boolean
        ''' <summary>FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\R</summary>
        Property PhonemeTargetWeightR27 As Single
        ''' <summary>El record trae FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\TH. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightTH27Presente As Boolean
        ''' <summary>FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\TH</summary>
        Property PhonemeTargetWeightTH27 As Single
        ''' <summary>El record trae FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\W. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightW27Presente As Boolean
        ''' <summary>FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\W</summary>
        Property PhonemeTargetWeightW27 As Single
        ''' <summary>El record trae FaceFX Phonemes\L\PHWT\Phoneme Target Weight\Aah / LipBigAah. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightAahLipBigAah28Presente As Boolean
        ''' <summary>FaceFX Phonemes\L\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Property PhonemeTargetWeightAahLipBigAah28 As Single
        ''' <summary>El record trae FaceFX Phonemes\L\PHWT\Phoneme Target Weight\BigAah / LipDST. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightBigAahLipDST28Presente As Boolean
        ''' <summary>FaceFX Phonemes\L\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Property PhonemeTargetWeightBigAahLipDST28 As Single
        ''' <summary>El record trae FaceFX Phonemes\L\PHWT\Phoneme Target Weight\BMP / LipEee. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightBMPLipEee28Presente As Boolean
        ''' <summary>FaceFX Phonemes\L\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Property PhonemeTargetWeightBMPLipEee28 As Single
        ''' <summary>El record trae FaceFX Phonemes\L\PHWT\Phoneme Target Weight\ChJsh / LipFV. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightChJshLipFV28Presente As Boolean
        ''' <summary>FaceFX Phonemes\L\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Property PhonemeTargetWeightChJshLipFV28 As Single
        ''' <summary>El record trae FaceFX Phonemes\L\PHWT\Phoneme Target Weight\DST / LipK. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightDSTLipK28Presente As Boolean
        ''' <summary>FaceFX Phonemes\L\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Property PhonemeTargetWeightDSTLipK28 As Single
        ''' <summary>El record trae FaceFX Phonemes\L\PHWT\Phoneme Target Weight\Eee / LipL. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightEeeLipL28Presente As Boolean
        ''' <summary>FaceFX Phonemes\L\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Property PhonemeTargetWeightEeeLipL28 As Single
        ''' <summary>El record trae FaceFX Phonemes\L\PHWT\Phoneme Target Weight\Eh / LipR. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightEhLipR28Presente As Boolean
        ''' <summary>FaceFX Phonemes\L\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Property PhonemeTargetWeightEhLipR28 As Single
        ''' <summary>El record trae FaceFX Phonemes\L\PHWT\Phoneme Target Weight\FV / LipTh. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightFVLipTh28Presente As Boolean
        ''' <summary>FaceFX Phonemes\L\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Property PhonemeTargetWeightFVLipTh28 As Single
        ''' <summary>El record trae FaceFX Phonemes\L\PHWT\Phoneme Target Weight\I. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightI28Presente As Boolean
        ''' <summary>FaceFX Phonemes\L\PHWT\Phoneme Target Weight\I</summary>
        Property PhonemeTargetWeightI28 As Single
        ''' <summary>El record trae FaceFX Phonemes\L\PHWT\Phoneme Target Weight\K. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightK28Presente As Boolean
        ''' <summary>FaceFX Phonemes\L\PHWT\Phoneme Target Weight\K</summary>
        Property PhonemeTargetWeightK28 As Single
        ''' <summary>El record trae FaceFX Phonemes\L\PHWT\Phoneme Target Weight\N. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightN28Presente As Boolean
        ''' <summary>FaceFX Phonemes\L\PHWT\Phoneme Target Weight\N</summary>
        Property PhonemeTargetWeightN28 As Single
        ''' <summary>El record trae FaceFX Phonemes\L\PHWT\Phoneme Target Weight\Oh. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightOh28Presente As Boolean
        ''' <summary>FaceFX Phonemes\L\PHWT\Phoneme Target Weight\Oh</summary>
        Property PhonemeTargetWeightOh28 As Single
        ''' <summary>El record trae FaceFX Phonemes\L\PHWT\Phoneme Target Weight\OohQ. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightOohQ28Presente As Boolean
        ''' <summary>FaceFX Phonemes\L\PHWT\Phoneme Target Weight\OohQ</summary>
        Property PhonemeTargetWeightOohQ28 As Single
        ''' <summary>El record trae FaceFX Phonemes\L\PHWT\Phoneme Target Weight\R. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightR28Presente As Boolean
        ''' <summary>FaceFX Phonemes\L\PHWT\Phoneme Target Weight\R</summary>
        Property PhonemeTargetWeightR28 As Single
        ''' <summary>El record trae FaceFX Phonemes\L\PHWT\Phoneme Target Weight\TH. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightTH28Presente As Boolean
        ''' <summary>FaceFX Phonemes\L\PHWT\Phoneme Target Weight\TH</summary>
        Property PhonemeTargetWeightTH28 As Single
        ''' <summary>El record trae FaceFX Phonemes\L\PHWT\Phoneme Target Weight\W. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightW28Presente As Boolean
        ''' <summary>FaceFX Phonemes\L\PHWT\Phoneme Target Weight\W</summary>
        Property PhonemeTargetWeightW28 As Single
        ''' <summary>El record trae FaceFX Phonemes\R\PHWT\Phoneme Target Weight\Aah / LipBigAah. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightAahLipBigAah29Presente As Boolean
        ''' <summary>FaceFX Phonemes\R\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Property PhonemeTargetWeightAahLipBigAah29 As Single
        ''' <summary>El record trae FaceFX Phonemes\R\PHWT\Phoneme Target Weight\BigAah / LipDST. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightBigAahLipDST29Presente As Boolean
        ''' <summary>FaceFX Phonemes\R\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Property PhonemeTargetWeightBigAahLipDST29 As Single
        ''' <summary>El record trae FaceFX Phonemes\R\PHWT\Phoneme Target Weight\BMP / LipEee. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightBMPLipEee29Presente As Boolean
        ''' <summary>FaceFX Phonemes\R\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Property PhonemeTargetWeightBMPLipEee29 As Single
        ''' <summary>El record trae FaceFX Phonemes\R\PHWT\Phoneme Target Weight\ChJsh / LipFV. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightChJshLipFV29Presente As Boolean
        ''' <summary>FaceFX Phonemes\R\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Property PhonemeTargetWeightChJshLipFV29 As Single
        ''' <summary>El record trae FaceFX Phonemes\R\PHWT\Phoneme Target Weight\DST / LipK. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightDSTLipK29Presente As Boolean
        ''' <summary>FaceFX Phonemes\R\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Property PhonemeTargetWeightDSTLipK29 As Single
        ''' <summary>El record trae FaceFX Phonemes\R\PHWT\Phoneme Target Weight\Eee / LipL. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightEeeLipL29Presente As Boolean
        ''' <summary>FaceFX Phonemes\R\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Property PhonemeTargetWeightEeeLipL29 As Single
        ''' <summary>El record trae FaceFX Phonemes\R\PHWT\Phoneme Target Weight\Eh / LipR. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightEhLipR29Presente As Boolean
        ''' <summary>FaceFX Phonemes\R\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Property PhonemeTargetWeightEhLipR29 As Single
        ''' <summary>El record trae FaceFX Phonemes\R\PHWT\Phoneme Target Weight\FV / LipTh. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightFVLipTh29Presente As Boolean
        ''' <summary>FaceFX Phonemes\R\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Property PhonemeTargetWeightFVLipTh29 As Single
        ''' <summary>El record trae FaceFX Phonemes\R\PHWT\Phoneme Target Weight\I. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightI29Presente As Boolean
        ''' <summary>FaceFX Phonemes\R\PHWT\Phoneme Target Weight\I</summary>
        Property PhonemeTargetWeightI29 As Single
        ''' <summary>El record trae FaceFX Phonemes\R\PHWT\Phoneme Target Weight\K. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightK29Presente As Boolean
        ''' <summary>FaceFX Phonemes\R\PHWT\Phoneme Target Weight\K</summary>
        Property PhonemeTargetWeightK29 As Single
        ''' <summary>El record trae FaceFX Phonemes\R\PHWT\Phoneme Target Weight\N. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightN29Presente As Boolean
        ''' <summary>FaceFX Phonemes\R\PHWT\Phoneme Target Weight\N</summary>
        Property PhonemeTargetWeightN29 As Single
        ''' <summary>El record trae FaceFX Phonemes\R\PHWT\Phoneme Target Weight\Oh. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightOh29Presente As Boolean
        ''' <summary>FaceFX Phonemes\R\PHWT\Phoneme Target Weight\Oh</summary>
        Property PhonemeTargetWeightOh29 As Single
        ''' <summary>El record trae FaceFX Phonemes\R\PHWT\Phoneme Target Weight\OohQ. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightOohQ29Presente As Boolean
        ''' <summary>FaceFX Phonemes\R\PHWT\Phoneme Target Weight\OohQ</summary>
        Property PhonemeTargetWeightOohQ29 As Single
        ''' <summary>El record trae FaceFX Phonemes\R\PHWT\Phoneme Target Weight\R. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightR29Presente As Boolean
        ''' <summary>FaceFX Phonemes\R\PHWT\Phoneme Target Weight\R</summary>
        Property PhonemeTargetWeightR29 As Single
        ''' <summary>El record trae FaceFX Phonemes\R\PHWT\Phoneme Target Weight\TH. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightTH29Presente As Boolean
        ''' <summary>FaceFX Phonemes\R\PHWT\Phoneme Target Weight\TH</summary>
        Property PhonemeTargetWeightTH29 As Single
        ''' <summary>El record trae FaceFX Phonemes\R\PHWT\Phoneme Target Weight\W. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightW29Presente As Boolean
        ''' <summary>FaceFX Phonemes\R\PHWT\Phoneme Target Weight\W</summary>
        Property PhonemeTargetWeightW29 As Single
        ''' <summary>El record trae FaceFX Phonemes\W\PHWT\Phoneme Target Weight\Aah / LipBigAah. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightAahLipBigAah30Presente As Boolean
        ''' <summary>FaceFX Phonemes\W\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Property PhonemeTargetWeightAahLipBigAah30 As Single
        ''' <summary>El record trae FaceFX Phonemes\W\PHWT\Phoneme Target Weight\BigAah / LipDST. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightBigAahLipDST30Presente As Boolean
        ''' <summary>FaceFX Phonemes\W\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Property PhonemeTargetWeightBigAahLipDST30 As Single
        ''' <summary>El record trae FaceFX Phonemes\W\PHWT\Phoneme Target Weight\BMP / LipEee. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightBMPLipEee30Presente As Boolean
        ''' <summary>FaceFX Phonemes\W\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Property PhonemeTargetWeightBMPLipEee30 As Single
        ''' <summary>El record trae FaceFX Phonemes\W\PHWT\Phoneme Target Weight\ChJsh / LipFV. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightChJshLipFV30Presente As Boolean
        ''' <summary>FaceFX Phonemes\W\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Property PhonemeTargetWeightChJshLipFV30 As Single
        ''' <summary>El record trae FaceFX Phonemes\W\PHWT\Phoneme Target Weight\DST / LipK. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightDSTLipK30Presente As Boolean
        ''' <summary>FaceFX Phonemes\W\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Property PhonemeTargetWeightDSTLipK30 As Single
        ''' <summary>El record trae FaceFX Phonemes\W\PHWT\Phoneme Target Weight\Eee / LipL. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightEeeLipL30Presente As Boolean
        ''' <summary>FaceFX Phonemes\W\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Property PhonemeTargetWeightEeeLipL30 As Single
        ''' <summary>El record trae FaceFX Phonemes\W\PHWT\Phoneme Target Weight\Eh / LipR. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightEhLipR30Presente As Boolean
        ''' <summary>FaceFX Phonemes\W\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Property PhonemeTargetWeightEhLipR30 As Single
        ''' <summary>El record trae FaceFX Phonemes\W\PHWT\Phoneme Target Weight\FV / LipTh. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightFVLipTh30Presente As Boolean
        ''' <summary>FaceFX Phonemes\W\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Property PhonemeTargetWeightFVLipTh30 As Single
        ''' <summary>El record trae FaceFX Phonemes\W\PHWT\Phoneme Target Weight\I. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightI30Presente As Boolean
        ''' <summary>FaceFX Phonemes\W\PHWT\Phoneme Target Weight\I</summary>
        Property PhonemeTargetWeightI30 As Single
        ''' <summary>El record trae FaceFX Phonemes\W\PHWT\Phoneme Target Weight\K. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightK30Presente As Boolean
        ''' <summary>FaceFX Phonemes\W\PHWT\Phoneme Target Weight\K</summary>
        Property PhonemeTargetWeightK30 As Single
        ''' <summary>El record trae FaceFX Phonemes\W\PHWT\Phoneme Target Weight\N. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightN30Presente As Boolean
        ''' <summary>FaceFX Phonemes\W\PHWT\Phoneme Target Weight\N</summary>
        Property PhonemeTargetWeightN30 As Single
        ''' <summary>El record trae FaceFX Phonemes\W\PHWT\Phoneme Target Weight\Oh. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightOh30Presente As Boolean
        ''' <summary>FaceFX Phonemes\W\PHWT\Phoneme Target Weight\Oh</summary>
        Property PhonemeTargetWeightOh30 As Single
        ''' <summary>El record trae FaceFX Phonemes\W\PHWT\Phoneme Target Weight\OohQ. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightOohQ30Presente As Boolean
        ''' <summary>FaceFX Phonemes\W\PHWT\Phoneme Target Weight\OohQ</summary>
        Property PhonemeTargetWeightOohQ30 As Single
        ''' <summary>El record trae FaceFX Phonemes\W\PHWT\Phoneme Target Weight\R. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightR30Presente As Boolean
        ''' <summary>FaceFX Phonemes\W\PHWT\Phoneme Target Weight\R</summary>
        Property PhonemeTargetWeightR30 As Single
        ''' <summary>El record trae FaceFX Phonemes\W\PHWT\Phoneme Target Weight\TH. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightTH30Presente As Boolean
        ''' <summary>FaceFX Phonemes\W\PHWT\Phoneme Target Weight\TH</summary>
        Property PhonemeTargetWeightTH30 As Single
        ''' <summary>El record trae FaceFX Phonemes\W\PHWT\Phoneme Target Weight\W. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightW30Presente As Boolean
        ''' <summary>FaceFX Phonemes\W\PHWT\Phoneme Target Weight\W</summary>
        Property PhonemeTargetWeightW30 As Single
        ''' <summary>El record trae FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\Aah / LipBigAah. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightAahLipBigAah31Presente As Boolean
        ''' <summary>FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Property PhonemeTargetWeightAahLipBigAah31 As Single
        ''' <summary>El record trae FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\BigAah / LipDST. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightBigAahLipDST31Presente As Boolean
        ''' <summary>FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Property PhonemeTargetWeightBigAahLipDST31 As Single
        ''' <summary>El record trae FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\BMP / LipEee. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightBMPLipEee31Presente As Boolean
        ''' <summary>FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Property PhonemeTargetWeightBMPLipEee31 As Single
        ''' <summary>El record trae FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\ChJsh / LipFV. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightChJshLipFV31Presente As Boolean
        ''' <summary>FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Property PhonemeTargetWeightChJshLipFV31 As Single
        ''' <summary>El record trae FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\DST / LipK. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightDSTLipK31Presente As Boolean
        ''' <summary>FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Property PhonemeTargetWeightDSTLipK31 As Single
        ''' <summary>El record trae FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\Eee / LipL. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightEeeLipL31Presente As Boolean
        ''' <summary>FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Property PhonemeTargetWeightEeeLipL31 As Single
        ''' <summary>El record trae FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\Eh / LipR. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightEhLipR31Presente As Boolean
        ''' <summary>FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Property PhonemeTargetWeightEhLipR31 As Single
        ''' <summary>El record trae FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\FV / LipTh. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightFVLipTh31Presente As Boolean
        ''' <summary>FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Property PhonemeTargetWeightFVLipTh31 As Single
        ''' <summary>El record trae FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\I. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightI31Presente As Boolean
        ''' <summary>FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\I</summary>
        Property PhonemeTargetWeightI31 As Single
        ''' <summary>El record trae FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\K. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightK31Presente As Boolean
        ''' <summary>FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\K</summary>
        Property PhonemeTargetWeightK31 As Single
        ''' <summary>El record trae FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\N. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightN31Presente As Boolean
        ''' <summary>FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\N</summary>
        Property PhonemeTargetWeightN31 As Single
        ''' <summary>El record trae FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\Oh. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightOh31Presente As Boolean
        ''' <summary>FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\Oh</summary>
        Property PhonemeTargetWeightOh31 As Single
        ''' <summary>El record trae FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\OohQ. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightOohQ31Presente As Boolean
        ''' <summary>FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\OohQ</summary>
        Property PhonemeTargetWeightOohQ31 As Single
        ''' <summary>El record trae FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\R. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightR31Presente As Boolean
        ''' <summary>FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\R</summary>
        Property PhonemeTargetWeightR31 As Single
        ''' <summary>El record trae FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\TH. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightTH31Presente As Boolean
        ''' <summary>FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\TH</summary>
        Property PhonemeTargetWeightTH31 As Single
        ''' <summary>El record trae FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\W. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightW31Presente As Boolean
        ''' <summary>FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\W</summary>
        Property PhonemeTargetWeightW31 As Single
        ''' <summary>El record trae FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\Aah / LipBigAah. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightAahLipBigAah32Presente As Boolean
        ''' <summary>FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Property PhonemeTargetWeightAahLipBigAah32 As Single
        ''' <summary>El record trae FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\BigAah / LipDST. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightBigAahLipDST32Presente As Boolean
        ''' <summary>FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Property PhonemeTargetWeightBigAahLipDST32 As Single
        ''' <summary>El record trae FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\BMP / LipEee. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightBMPLipEee32Presente As Boolean
        ''' <summary>FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Property PhonemeTargetWeightBMPLipEee32 As Single
        ''' <summary>El record trae FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\ChJsh / LipFV. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightChJshLipFV32Presente As Boolean
        ''' <summary>FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Property PhonemeTargetWeightChJshLipFV32 As Single
        ''' <summary>El record trae FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\DST / LipK. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightDSTLipK32Presente As Boolean
        ''' <summary>FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Property PhonemeTargetWeightDSTLipK32 As Single
        ''' <summary>El record trae FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\Eee / LipL. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightEeeLipL32Presente As Boolean
        ''' <summary>FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Property PhonemeTargetWeightEeeLipL32 As Single
        ''' <summary>El record trae FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\Eh / LipR. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightEhLipR32Presente As Boolean
        ''' <summary>FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Property PhonemeTargetWeightEhLipR32 As Single
        ''' <summary>El record trae FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\FV / LipTh. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightFVLipTh32Presente As Boolean
        ''' <summary>FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Property PhonemeTargetWeightFVLipTh32 As Single
        ''' <summary>El record trae FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\I. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightI32Presente As Boolean
        ''' <summary>FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\I</summary>
        Property PhonemeTargetWeightI32 As Single
        ''' <summary>El record trae FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\K. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightK32Presente As Boolean
        ''' <summary>FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\K</summary>
        Property PhonemeTargetWeightK32 As Single
        ''' <summary>El record trae FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\N. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightN32Presente As Boolean
        ''' <summary>FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\N</summary>
        Property PhonemeTargetWeightN32 As Single
        ''' <summary>El record trae FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\Oh. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightOh32Presente As Boolean
        ''' <summary>FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\Oh</summary>
        Property PhonemeTargetWeightOh32 As Single
        ''' <summary>El record trae FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\OohQ. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightOohQ32Presente As Boolean
        ''' <summary>FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\OohQ</summary>
        Property PhonemeTargetWeightOohQ32 As Single
        ''' <summary>El record trae FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\R. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightR32Presente As Boolean
        ''' <summary>FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\R</summary>
        Property PhonemeTargetWeightR32 As Single
        ''' <summary>El record trae FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\TH. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightTH32Presente As Boolean
        ''' <summary>FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\TH</summary>
        Property PhonemeTargetWeightTH32 As Single
        ''' <summary>El record trae FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\W. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightW32Presente As Boolean
        ''' <summary>FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\W</summary>
        Property PhonemeTargetWeightW32 As Single
        ''' <summary>El record trae FaceFX Phonemes\B\PHWT\Phoneme Target Weight\Aah / LipBigAah. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightAahLipBigAah33Presente As Boolean
        ''' <summary>FaceFX Phonemes\B\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Property PhonemeTargetWeightAahLipBigAah33 As Single
        ''' <summary>El record trae FaceFX Phonemes\B\PHWT\Phoneme Target Weight\BigAah / LipDST. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightBigAahLipDST33Presente As Boolean
        ''' <summary>FaceFX Phonemes\B\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Property PhonemeTargetWeightBigAahLipDST33 As Single
        ''' <summary>El record trae FaceFX Phonemes\B\PHWT\Phoneme Target Weight\BMP / LipEee. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightBMPLipEee33Presente As Boolean
        ''' <summary>FaceFX Phonemes\B\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Property PhonemeTargetWeightBMPLipEee33 As Single
        ''' <summary>El record trae FaceFX Phonemes\B\PHWT\Phoneme Target Weight\ChJsh / LipFV. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightChJshLipFV33Presente As Boolean
        ''' <summary>FaceFX Phonemes\B\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Property PhonemeTargetWeightChJshLipFV33 As Single
        ''' <summary>El record trae FaceFX Phonemes\B\PHWT\Phoneme Target Weight\DST / LipK. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightDSTLipK33Presente As Boolean
        ''' <summary>FaceFX Phonemes\B\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Property PhonemeTargetWeightDSTLipK33 As Single
        ''' <summary>El record trae FaceFX Phonemes\B\PHWT\Phoneme Target Weight\Eee / LipL. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightEeeLipL33Presente As Boolean
        ''' <summary>FaceFX Phonemes\B\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Property PhonemeTargetWeightEeeLipL33 As Single
        ''' <summary>El record trae FaceFX Phonemes\B\PHWT\Phoneme Target Weight\Eh / LipR. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightEhLipR33Presente As Boolean
        ''' <summary>FaceFX Phonemes\B\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Property PhonemeTargetWeightEhLipR33 As Single
        ''' <summary>El record trae FaceFX Phonemes\B\PHWT\Phoneme Target Weight\FV / LipTh. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightFVLipTh33Presente As Boolean
        ''' <summary>FaceFX Phonemes\B\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Property PhonemeTargetWeightFVLipTh33 As Single
        ''' <summary>El record trae FaceFX Phonemes\B\PHWT\Phoneme Target Weight\I. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightI33Presente As Boolean
        ''' <summary>FaceFX Phonemes\B\PHWT\Phoneme Target Weight\I</summary>
        Property PhonemeTargetWeightI33 As Single
        ''' <summary>El record trae FaceFX Phonemes\B\PHWT\Phoneme Target Weight\K. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightK33Presente As Boolean
        ''' <summary>FaceFX Phonemes\B\PHWT\Phoneme Target Weight\K</summary>
        Property PhonemeTargetWeightK33 As Single
        ''' <summary>El record trae FaceFX Phonemes\B\PHWT\Phoneme Target Weight\N. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightN33Presente As Boolean
        ''' <summary>FaceFX Phonemes\B\PHWT\Phoneme Target Weight\N</summary>
        Property PhonemeTargetWeightN33 As Single
        ''' <summary>El record trae FaceFX Phonemes\B\PHWT\Phoneme Target Weight\Oh. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightOh33Presente As Boolean
        ''' <summary>FaceFX Phonemes\B\PHWT\Phoneme Target Weight\Oh</summary>
        Property PhonemeTargetWeightOh33 As Single
        ''' <summary>El record trae FaceFX Phonemes\B\PHWT\Phoneme Target Weight\OohQ. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightOohQ33Presente As Boolean
        ''' <summary>FaceFX Phonemes\B\PHWT\Phoneme Target Weight\OohQ</summary>
        Property PhonemeTargetWeightOohQ33 As Single
        ''' <summary>El record trae FaceFX Phonemes\B\PHWT\Phoneme Target Weight\R. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightR33Presente As Boolean
        ''' <summary>FaceFX Phonemes\B\PHWT\Phoneme Target Weight\R</summary>
        Property PhonemeTargetWeightR33 As Single
        ''' <summary>El record trae FaceFX Phonemes\B\PHWT\Phoneme Target Weight\TH. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightTH33Presente As Boolean
        ''' <summary>FaceFX Phonemes\B\PHWT\Phoneme Target Weight\TH</summary>
        Property PhonemeTargetWeightTH33 As Single
        ''' <summary>El record trae FaceFX Phonemes\B\PHWT\Phoneme Target Weight\W. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightW33Presente As Boolean
        ''' <summary>FaceFX Phonemes\B\PHWT\Phoneme Target Weight\W</summary>
        Property PhonemeTargetWeightW33 As Single
        ''' <summary>El record trae FaceFX Phonemes\D\PHWT\Phoneme Target Weight\Aah / LipBigAah. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightAahLipBigAah34Presente As Boolean
        ''' <summary>FaceFX Phonemes\D\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Property PhonemeTargetWeightAahLipBigAah34 As Single
        ''' <summary>El record trae FaceFX Phonemes\D\PHWT\Phoneme Target Weight\BigAah / LipDST. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightBigAahLipDST34Presente As Boolean
        ''' <summary>FaceFX Phonemes\D\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Property PhonemeTargetWeightBigAahLipDST34 As Single
        ''' <summary>El record trae FaceFX Phonemes\D\PHWT\Phoneme Target Weight\BMP / LipEee. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightBMPLipEee34Presente As Boolean
        ''' <summary>FaceFX Phonemes\D\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Property PhonemeTargetWeightBMPLipEee34 As Single
        ''' <summary>El record trae FaceFX Phonemes\D\PHWT\Phoneme Target Weight\ChJsh / LipFV. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightChJshLipFV34Presente As Boolean
        ''' <summary>FaceFX Phonemes\D\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Property PhonemeTargetWeightChJshLipFV34 As Single
        ''' <summary>El record trae FaceFX Phonemes\D\PHWT\Phoneme Target Weight\DST / LipK. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightDSTLipK34Presente As Boolean
        ''' <summary>FaceFX Phonemes\D\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Property PhonemeTargetWeightDSTLipK34 As Single
        ''' <summary>El record trae FaceFX Phonemes\D\PHWT\Phoneme Target Weight\Eee / LipL. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightEeeLipL34Presente As Boolean
        ''' <summary>FaceFX Phonemes\D\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Property PhonemeTargetWeightEeeLipL34 As Single
        ''' <summary>El record trae FaceFX Phonemes\D\PHWT\Phoneme Target Weight\Eh / LipR. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightEhLipR34Presente As Boolean
        ''' <summary>FaceFX Phonemes\D\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Property PhonemeTargetWeightEhLipR34 As Single
        ''' <summary>El record trae FaceFX Phonemes\D\PHWT\Phoneme Target Weight\FV / LipTh. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightFVLipTh34Presente As Boolean
        ''' <summary>FaceFX Phonemes\D\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Property PhonemeTargetWeightFVLipTh34 As Single
        ''' <summary>El record trae FaceFX Phonemes\D\PHWT\Phoneme Target Weight\I. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightI34Presente As Boolean
        ''' <summary>FaceFX Phonemes\D\PHWT\Phoneme Target Weight\I</summary>
        Property PhonemeTargetWeightI34 As Single
        ''' <summary>El record trae FaceFX Phonemes\D\PHWT\Phoneme Target Weight\K. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightK34Presente As Boolean
        ''' <summary>FaceFX Phonemes\D\PHWT\Phoneme Target Weight\K</summary>
        Property PhonemeTargetWeightK34 As Single
        ''' <summary>El record trae FaceFX Phonemes\D\PHWT\Phoneme Target Weight\N. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightN34Presente As Boolean
        ''' <summary>FaceFX Phonemes\D\PHWT\Phoneme Target Weight\N</summary>
        Property PhonemeTargetWeightN34 As Single
        ''' <summary>El record trae FaceFX Phonemes\D\PHWT\Phoneme Target Weight\Oh. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightOh34Presente As Boolean
        ''' <summary>FaceFX Phonemes\D\PHWT\Phoneme Target Weight\Oh</summary>
        Property PhonemeTargetWeightOh34 As Single
        ''' <summary>El record trae FaceFX Phonemes\D\PHWT\Phoneme Target Weight\OohQ. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightOohQ34Presente As Boolean
        ''' <summary>FaceFX Phonemes\D\PHWT\Phoneme Target Weight\OohQ</summary>
        Property PhonemeTargetWeightOohQ34 As Single
        ''' <summary>El record trae FaceFX Phonemes\D\PHWT\Phoneme Target Weight\R. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightR34Presente As Boolean
        ''' <summary>FaceFX Phonemes\D\PHWT\Phoneme Target Weight\R</summary>
        Property PhonemeTargetWeightR34 As Single
        ''' <summary>El record trae FaceFX Phonemes\D\PHWT\Phoneme Target Weight\TH. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightTH34Presente As Boolean
        ''' <summary>FaceFX Phonemes\D\PHWT\Phoneme Target Weight\TH</summary>
        Property PhonemeTargetWeightTH34 As Single
        ''' <summary>El record trae FaceFX Phonemes\D\PHWT\Phoneme Target Weight\W. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightW34Presente As Boolean
        ''' <summary>FaceFX Phonemes\D\PHWT\Phoneme Target Weight\W</summary>
        Property PhonemeTargetWeightW34 As Single
        ''' <summary>El record trae FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\Aah / LipBigAah. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightAahLipBigAah35Presente As Boolean
        ''' <summary>FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Property PhonemeTargetWeightAahLipBigAah35 As Single
        ''' <summary>El record trae FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\BigAah / LipDST. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightBigAahLipDST35Presente As Boolean
        ''' <summary>FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Property PhonemeTargetWeightBigAahLipDST35 As Single
        ''' <summary>El record trae FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\BMP / LipEee. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightBMPLipEee35Presente As Boolean
        ''' <summary>FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Property PhonemeTargetWeightBMPLipEee35 As Single
        ''' <summary>El record trae FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\ChJsh / LipFV. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightChJshLipFV35Presente As Boolean
        ''' <summary>FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Property PhonemeTargetWeightChJshLipFV35 As Single
        ''' <summary>El record trae FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\DST / LipK. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightDSTLipK35Presente As Boolean
        ''' <summary>FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Property PhonemeTargetWeightDSTLipK35 As Single
        ''' <summary>El record trae FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\Eee / LipL. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightEeeLipL35Presente As Boolean
        ''' <summary>FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Property PhonemeTargetWeightEeeLipL35 As Single
        ''' <summary>El record trae FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\Eh / LipR. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightEhLipR35Presente As Boolean
        ''' <summary>FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Property PhonemeTargetWeightEhLipR35 As Single
        ''' <summary>El record trae FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\FV / LipTh. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightFVLipTh35Presente As Boolean
        ''' <summary>FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Property PhonemeTargetWeightFVLipTh35 As Single
        ''' <summary>El record trae FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\I. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightI35Presente As Boolean
        ''' <summary>FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\I</summary>
        Property PhonemeTargetWeightI35 As Single
        ''' <summary>El record trae FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\K. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightK35Presente As Boolean
        ''' <summary>FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\K</summary>
        Property PhonemeTargetWeightK35 As Single
        ''' <summary>El record trae FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\N. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightN35Presente As Boolean
        ''' <summary>FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\N</summary>
        Property PhonemeTargetWeightN35 As Single
        ''' <summary>El record trae FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\Oh. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightOh35Presente As Boolean
        ''' <summary>FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\Oh</summary>
        Property PhonemeTargetWeightOh35 As Single
        ''' <summary>El record trae FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\OohQ. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightOohQ35Presente As Boolean
        ''' <summary>FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\OohQ</summary>
        Property PhonemeTargetWeightOohQ35 As Single
        ''' <summary>El record trae FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\R. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightR35Presente As Boolean
        ''' <summary>FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\R</summary>
        Property PhonemeTargetWeightR35 As Single
        ''' <summary>El record trae FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\TH. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightTH35Presente As Boolean
        ''' <summary>FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\TH</summary>
        Property PhonemeTargetWeightTH35 As Single
        ''' <summary>El record trae FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\W. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightW35Presente As Boolean
        ''' <summary>FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\W</summary>
        Property PhonemeTargetWeightW35 As Single
        ''' <summary>El record trae FaceFX Phonemes\G\PHWT\Phoneme Target Weight\Aah / LipBigAah. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightAahLipBigAah36Presente As Boolean
        ''' <summary>FaceFX Phonemes\G\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Property PhonemeTargetWeightAahLipBigAah36 As Single
        ''' <summary>El record trae FaceFX Phonemes\G\PHWT\Phoneme Target Weight\BigAah / LipDST. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightBigAahLipDST36Presente As Boolean
        ''' <summary>FaceFX Phonemes\G\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Property PhonemeTargetWeightBigAahLipDST36 As Single
        ''' <summary>El record trae FaceFX Phonemes\G\PHWT\Phoneme Target Weight\BMP / LipEee. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightBMPLipEee36Presente As Boolean
        ''' <summary>FaceFX Phonemes\G\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Property PhonemeTargetWeightBMPLipEee36 As Single
        ''' <summary>El record trae FaceFX Phonemes\G\PHWT\Phoneme Target Weight\ChJsh / LipFV. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightChJshLipFV36Presente As Boolean
        ''' <summary>FaceFX Phonemes\G\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Property PhonemeTargetWeightChJshLipFV36 As Single
        ''' <summary>El record trae FaceFX Phonemes\G\PHWT\Phoneme Target Weight\DST / LipK. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightDSTLipK36Presente As Boolean
        ''' <summary>FaceFX Phonemes\G\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Property PhonemeTargetWeightDSTLipK36 As Single
        ''' <summary>El record trae FaceFX Phonemes\G\PHWT\Phoneme Target Weight\Eee / LipL. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightEeeLipL36Presente As Boolean
        ''' <summary>FaceFX Phonemes\G\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Property PhonemeTargetWeightEeeLipL36 As Single
        ''' <summary>El record trae FaceFX Phonemes\G\PHWT\Phoneme Target Weight\Eh / LipR. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightEhLipR36Presente As Boolean
        ''' <summary>FaceFX Phonemes\G\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Property PhonemeTargetWeightEhLipR36 As Single
        ''' <summary>El record trae FaceFX Phonemes\G\PHWT\Phoneme Target Weight\FV / LipTh. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightFVLipTh36Presente As Boolean
        ''' <summary>FaceFX Phonemes\G\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Property PhonemeTargetWeightFVLipTh36 As Single
        ''' <summary>El record trae FaceFX Phonemes\G\PHWT\Phoneme Target Weight\I. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightI36Presente As Boolean
        ''' <summary>FaceFX Phonemes\G\PHWT\Phoneme Target Weight\I</summary>
        Property PhonemeTargetWeightI36 As Single
        ''' <summary>El record trae FaceFX Phonemes\G\PHWT\Phoneme Target Weight\K. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightK36Presente As Boolean
        ''' <summary>FaceFX Phonemes\G\PHWT\Phoneme Target Weight\K</summary>
        Property PhonemeTargetWeightK36 As Single
        ''' <summary>El record trae FaceFX Phonemes\G\PHWT\Phoneme Target Weight\N. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightN36Presente As Boolean
        ''' <summary>FaceFX Phonemes\G\PHWT\Phoneme Target Weight\N</summary>
        Property PhonemeTargetWeightN36 As Single
        ''' <summary>El record trae FaceFX Phonemes\G\PHWT\Phoneme Target Weight\Oh. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightOh36Presente As Boolean
        ''' <summary>FaceFX Phonemes\G\PHWT\Phoneme Target Weight\Oh</summary>
        Property PhonemeTargetWeightOh36 As Single
        ''' <summary>El record trae FaceFX Phonemes\G\PHWT\Phoneme Target Weight\OohQ. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightOohQ36Presente As Boolean
        ''' <summary>FaceFX Phonemes\G\PHWT\Phoneme Target Weight\OohQ</summary>
        Property PhonemeTargetWeightOohQ36 As Single
        ''' <summary>El record trae FaceFX Phonemes\G\PHWT\Phoneme Target Weight\R. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightR36Presente As Boolean
        ''' <summary>FaceFX Phonemes\G\PHWT\Phoneme Target Weight\R</summary>
        Property PhonemeTargetWeightR36 As Single
        ''' <summary>El record trae FaceFX Phonemes\G\PHWT\Phoneme Target Weight\TH. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightTH36Presente As Boolean
        ''' <summary>FaceFX Phonemes\G\PHWT\Phoneme Target Weight\TH</summary>
        Property PhonemeTargetWeightTH36 As Single
        ''' <summary>El record trae FaceFX Phonemes\G\PHWT\Phoneme Target Weight\W. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightW36Presente As Boolean
        ''' <summary>FaceFX Phonemes\G\PHWT\Phoneme Target Weight\W</summary>
        Property PhonemeTargetWeightW36 As Single
        ''' <summary>El record trae FaceFX Phonemes\P\PHWT\Phoneme Target Weight\Aah / LipBigAah. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightAahLipBigAah37Presente As Boolean
        ''' <summary>FaceFX Phonemes\P\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Property PhonemeTargetWeightAahLipBigAah37 As Single
        ''' <summary>El record trae FaceFX Phonemes\P\PHWT\Phoneme Target Weight\BigAah / LipDST. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightBigAahLipDST37Presente As Boolean
        ''' <summary>FaceFX Phonemes\P\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Property PhonemeTargetWeightBigAahLipDST37 As Single
        ''' <summary>El record trae FaceFX Phonemes\P\PHWT\Phoneme Target Weight\BMP / LipEee. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightBMPLipEee37Presente As Boolean
        ''' <summary>FaceFX Phonemes\P\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Property PhonemeTargetWeightBMPLipEee37 As Single
        ''' <summary>El record trae FaceFX Phonemes\P\PHWT\Phoneme Target Weight\ChJsh / LipFV. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightChJshLipFV37Presente As Boolean
        ''' <summary>FaceFX Phonemes\P\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Property PhonemeTargetWeightChJshLipFV37 As Single
        ''' <summary>El record trae FaceFX Phonemes\P\PHWT\Phoneme Target Weight\DST / LipK. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightDSTLipK37Presente As Boolean
        ''' <summary>FaceFX Phonemes\P\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Property PhonemeTargetWeightDSTLipK37 As Single
        ''' <summary>El record trae FaceFX Phonemes\P\PHWT\Phoneme Target Weight\Eee / LipL. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightEeeLipL37Presente As Boolean
        ''' <summary>FaceFX Phonemes\P\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Property PhonemeTargetWeightEeeLipL37 As Single
        ''' <summary>El record trae FaceFX Phonemes\P\PHWT\Phoneme Target Weight\Eh / LipR. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightEhLipR37Presente As Boolean
        ''' <summary>FaceFX Phonemes\P\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Property PhonemeTargetWeightEhLipR37 As Single
        ''' <summary>El record trae FaceFX Phonemes\P\PHWT\Phoneme Target Weight\FV / LipTh. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightFVLipTh37Presente As Boolean
        ''' <summary>FaceFX Phonemes\P\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Property PhonemeTargetWeightFVLipTh37 As Single
        ''' <summary>El record trae FaceFX Phonemes\P\PHWT\Phoneme Target Weight\I. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightI37Presente As Boolean
        ''' <summary>FaceFX Phonemes\P\PHWT\Phoneme Target Weight\I</summary>
        Property PhonemeTargetWeightI37 As Single
        ''' <summary>El record trae FaceFX Phonemes\P\PHWT\Phoneme Target Weight\K. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightK37Presente As Boolean
        ''' <summary>FaceFX Phonemes\P\PHWT\Phoneme Target Weight\K</summary>
        Property PhonemeTargetWeightK37 As Single
        ''' <summary>El record trae FaceFX Phonemes\P\PHWT\Phoneme Target Weight\N. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightN37Presente As Boolean
        ''' <summary>FaceFX Phonemes\P\PHWT\Phoneme Target Weight\N</summary>
        Property PhonemeTargetWeightN37 As Single
        ''' <summary>El record trae FaceFX Phonemes\P\PHWT\Phoneme Target Weight\Oh. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightOh37Presente As Boolean
        ''' <summary>FaceFX Phonemes\P\PHWT\Phoneme Target Weight\Oh</summary>
        Property PhonemeTargetWeightOh37 As Single
        ''' <summary>El record trae FaceFX Phonemes\P\PHWT\Phoneme Target Weight\OohQ. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightOohQ37Presente As Boolean
        ''' <summary>FaceFX Phonemes\P\PHWT\Phoneme Target Weight\OohQ</summary>
        Property PhonemeTargetWeightOohQ37 As Single
        ''' <summary>El record trae FaceFX Phonemes\P\PHWT\Phoneme Target Weight\R. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightR37Presente As Boolean
        ''' <summary>FaceFX Phonemes\P\PHWT\Phoneme Target Weight\R</summary>
        Property PhonemeTargetWeightR37 As Single
        ''' <summary>El record trae FaceFX Phonemes\P\PHWT\Phoneme Target Weight\TH. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightTH37Presente As Boolean
        ''' <summary>FaceFX Phonemes\P\PHWT\Phoneme Target Weight\TH</summary>
        Property PhonemeTargetWeightTH37 As Single
        ''' <summary>El record trae FaceFX Phonemes\P\PHWT\Phoneme Target Weight\W. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightW37Presente As Boolean
        ''' <summary>FaceFX Phonemes\P\PHWT\Phoneme Target Weight\W</summary>
        Property PhonemeTargetWeightW37 As Single
        ''' <summary>El record trae FaceFX Phonemes\T\PHWT\Phoneme Target Weight\Aah / LipBigAah. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightAahLipBigAah38Presente As Boolean
        ''' <summary>FaceFX Phonemes\T\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Property PhonemeTargetWeightAahLipBigAah38 As Single
        ''' <summary>El record trae FaceFX Phonemes\T\PHWT\Phoneme Target Weight\BigAah / LipDST. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightBigAahLipDST38Presente As Boolean
        ''' <summary>FaceFX Phonemes\T\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Property PhonemeTargetWeightBigAahLipDST38 As Single
        ''' <summary>El record trae FaceFX Phonemes\T\PHWT\Phoneme Target Weight\BMP / LipEee. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightBMPLipEee38Presente As Boolean
        ''' <summary>FaceFX Phonemes\T\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Property PhonemeTargetWeightBMPLipEee38 As Single
        ''' <summary>El record trae FaceFX Phonemes\T\PHWT\Phoneme Target Weight\ChJsh / LipFV. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightChJshLipFV38Presente As Boolean
        ''' <summary>FaceFX Phonemes\T\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Property PhonemeTargetWeightChJshLipFV38 As Single
        ''' <summary>El record trae FaceFX Phonemes\T\PHWT\Phoneme Target Weight\DST / LipK. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightDSTLipK38Presente As Boolean
        ''' <summary>FaceFX Phonemes\T\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Property PhonemeTargetWeightDSTLipK38 As Single
        ''' <summary>El record trae FaceFX Phonemes\T\PHWT\Phoneme Target Weight\Eee / LipL. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightEeeLipL38Presente As Boolean
        ''' <summary>FaceFX Phonemes\T\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Property PhonemeTargetWeightEeeLipL38 As Single
        ''' <summary>El record trae FaceFX Phonemes\T\PHWT\Phoneme Target Weight\Eh / LipR. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightEhLipR38Presente As Boolean
        ''' <summary>FaceFX Phonemes\T\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Property PhonemeTargetWeightEhLipR38 As Single
        ''' <summary>El record trae FaceFX Phonemes\T\PHWT\Phoneme Target Weight\FV / LipTh. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightFVLipTh38Presente As Boolean
        ''' <summary>FaceFX Phonemes\T\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Property PhonemeTargetWeightFVLipTh38 As Single
        ''' <summary>El record trae FaceFX Phonemes\T\PHWT\Phoneme Target Weight\I. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightI38Presente As Boolean
        ''' <summary>FaceFX Phonemes\T\PHWT\Phoneme Target Weight\I</summary>
        Property PhonemeTargetWeightI38 As Single
        ''' <summary>El record trae FaceFX Phonemes\T\PHWT\Phoneme Target Weight\K. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightK38Presente As Boolean
        ''' <summary>FaceFX Phonemes\T\PHWT\Phoneme Target Weight\K</summary>
        Property PhonemeTargetWeightK38 As Single
        ''' <summary>El record trae FaceFX Phonemes\T\PHWT\Phoneme Target Weight\N. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightN38Presente As Boolean
        ''' <summary>FaceFX Phonemes\T\PHWT\Phoneme Target Weight\N</summary>
        Property PhonemeTargetWeightN38 As Single
        ''' <summary>El record trae FaceFX Phonemes\T\PHWT\Phoneme Target Weight\Oh. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightOh38Presente As Boolean
        ''' <summary>FaceFX Phonemes\T\PHWT\Phoneme Target Weight\Oh</summary>
        Property PhonemeTargetWeightOh38 As Single
        ''' <summary>El record trae FaceFX Phonemes\T\PHWT\Phoneme Target Weight\OohQ. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightOohQ38Presente As Boolean
        ''' <summary>FaceFX Phonemes\T\PHWT\Phoneme Target Weight\OohQ</summary>
        Property PhonemeTargetWeightOohQ38 As Single
        ''' <summary>El record trae FaceFX Phonemes\T\PHWT\Phoneme Target Weight\R. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightR38Presente As Boolean
        ''' <summary>FaceFX Phonemes\T\PHWT\Phoneme Target Weight\R</summary>
        Property PhonemeTargetWeightR38 As Single
        ''' <summary>El record trae FaceFX Phonemes\T\PHWT\Phoneme Target Weight\TH. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightTH38Presente As Boolean
        ''' <summary>FaceFX Phonemes\T\PHWT\Phoneme Target Weight\TH</summary>
        Property PhonemeTargetWeightTH38 As Single
        ''' <summary>El record trae FaceFX Phonemes\T\PHWT\Phoneme Target Weight\W. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightW38Presente As Boolean
        ''' <summary>FaceFX Phonemes\T\PHWT\Phoneme Target Weight\W</summary>
        Property PhonemeTargetWeightW38 As Single
        ''' <summary>El record trae FaceFX Phonemes\K\PHWT\Phoneme Target Weight\Aah / LipBigAah. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightAahLipBigAah39Presente As Boolean
        ''' <summary>FaceFX Phonemes\K\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Property PhonemeTargetWeightAahLipBigAah39 As Single
        ''' <summary>El record trae FaceFX Phonemes\K\PHWT\Phoneme Target Weight\BigAah / LipDST. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightBigAahLipDST39Presente As Boolean
        ''' <summary>FaceFX Phonemes\K\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Property PhonemeTargetWeightBigAahLipDST39 As Single
        ''' <summary>El record trae FaceFX Phonemes\K\PHWT\Phoneme Target Weight\BMP / LipEee. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightBMPLipEee39Presente As Boolean
        ''' <summary>FaceFX Phonemes\K\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Property PhonemeTargetWeightBMPLipEee39 As Single
        ''' <summary>El record trae FaceFX Phonemes\K\PHWT\Phoneme Target Weight\ChJsh / LipFV. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightChJshLipFV39Presente As Boolean
        ''' <summary>FaceFX Phonemes\K\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Property PhonemeTargetWeightChJshLipFV39 As Single
        ''' <summary>El record trae FaceFX Phonemes\K\PHWT\Phoneme Target Weight\DST / LipK. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightDSTLipK39Presente As Boolean
        ''' <summary>FaceFX Phonemes\K\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Property PhonemeTargetWeightDSTLipK39 As Single
        ''' <summary>El record trae FaceFX Phonemes\K\PHWT\Phoneme Target Weight\Eee / LipL. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightEeeLipL39Presente As Boolean
        ''' <summary>FaceFX Phonemes\K\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Property PhonemeTargetWeightEeeLipL39 As Single
        ''' <summary>El record trae FaceFX Phonemes\K\PHWT\Phoneme Target Weight\Eh / LipR. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightEhLipR39Presente As Boolean
        ''' <summary>FaceFX Phonemes\K\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Property PhonemeTargetWeightEhLipR39 As Single
        ''' <summary>El record trae FaceFX Phonemes\K\PHWT\Phoneme Target Weight\FV / LipTh. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightFVLipTh39Presente As Boolean
        ''' <summary>FaceFX Phonemes\K\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Property PhonemeTargetWeightFVLipTh39 As Single
        ''' <summary>El record trae FaceFX Phonemes\K\PHWT\Phoneme Target Weight\I. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightI39Presente As Boolean
        ''' <summary>FaceFX Phonemes\K\PHWT\Phoneme Target Weight\I</summary>
        Property PhonemeTargetWeightI39 As Single
        ''' <summary>El record trae FaceFX Phonemes\K\PHWT\Phoneme Target Weight\K. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightK39Presente As Boolean
        ''' <summary>FaceFX Phonemes\K\PHWT\Phoneme Target Weight\K</summary>
        Property PhonemeTargetWeightK39 As Single
        ''' <summary>El record trae FaceFX Phonemes\K\PHWT\Phoneme Target Weight\N. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightN39Presente As Boolean
        ''' <summary>FaceFX Phonemes\K\PHWT\Phoneme Target Weight\N</summary>
        Property PhonemeTargetWeightN39 As Single
        ''' <summary>El record trae FaceFX Phonemes\K\PHWT\Phoneme Target Weight\Oh. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightOh39Presente As Boolean
        ''' <summary>FaceFX Phonemes\K\PHWT\Phoneme Target Weight\Oh</summary>
        Property PhonemeTargetWeightOh39 As Single
        ''' <summary>El record trae FaceFX Phonemes\K\PHWT\Phoneme Target Weight\OohQ. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightOohQ39Presente As Boolean
        ''' <summary>FaceFX Phonemes\K\PHWT\Phoneme Target Weight\OohQ</summary>
        Property PhonemeTargetWeightOohQ39 As Single
        ''' <summary>El record trae FaceFX Phonemes\K\PHWT\Phoneme Target Weight\R. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightR39Presente As Boolean
        ''' <summary>FaceFX Phonemes\K\PHWT\Phoneme Target Weight\R</summary>
        Property PhonemeTargetWeightR39 As Single
        ''' <summary>El record trae FaceFX Phonemes\K\PHWT\Phoneme Target Weight\TH. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightTH39Presente As Boolean
        ''' <summary>FaceFX Phonemes\K\PHWT\Phoneme Target Weight\TH</summary>
        Property PhonemeTargetWeightTH39 As Single
        ''' <summary>El record trae FaceFX Phonemes\K\PHWT\Phoneme Target Weight\W. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightW39Presente As Boolean
        ''' <summary>FaceFX Phonemes\K\PHWT\Phoneme Target Weight\W</summary>
        Property PhonemeTargetWeightW39 As Single
        ''' <summary>El record trae FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\Aah / LipBigAah. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightAahLipBigAah40Presente As Boolean
        ''' <summary>FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Property PhonemeTargetWeightAahLipBigAah40 As Single
        ''' <summary>El record trae FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\BigAah / LipDST. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightBigAahLipDST40Presente As Boolean
        ''' <summary>FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Property PhonemeTargetWeightBigAahLipDST40 As Single
        ''' <summary>El record trae FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\BMP / LipEee. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightBMPLipEee40Presente As Boolean
        ''' <summary>FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Property PhonemeTargetWeightBMPLipEee40 As Single
        ''' <summary>El record trae FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\ChJsh / LipFV. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightChJshLipFV40Presente As Boolean
        ''' <summary>FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Property PhonemeTargetWeightChJshLipFV40 As Single
        ''' <summary>El record trae FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\DST / LipK. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightDSTLipK40Presente As Boolean
        ''' <summary>FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Property PhonemeTargetWeightDSTLipK40 As Single
        ''' <summary>El record trae FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\Eee / LipL. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightEeeLipL40Presente As Boolean
        ''' <summary>FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Property PhonemeTargetWeightEeeLipL40 As Single
        ''' <summary>El record trae FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\Eh / LipR. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightEhLipR40Presente As Boolean
        ''' <summary>FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Property PhonemeTargetWeightEhLipR40 As Single
        ''' <summary>El record trae FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\FV / LipTh. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightFVLipTh40Presente As Boolean
        ''' <summary>FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Property PhonemeTargetWeightFVLipTh40 As Single
        ''' <summary>El record trae FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\I. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightI40Presente As Boolean
        ''' <summary>FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\I</summary>
        Property PhonemeTargetWeightI40 As Single
        ''' <summary>El record trae FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\K. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightK40Presente As Boolean
        ''' <summary>FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\K</summary>
        Property PhonemeTargetWeightK40 As Single
        ''' <summary>El record trae FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\N. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightN40Presente As Boolean
        ''' <summary>FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\N</summary>
        Property PhonemeTargetWeightN40 As Single
        ''' <summary>El record trae FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\Oh. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightOh40Presente As Boolean
        ''' <summary>FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\Oh</summary>
        Property PhonemeTargetWeightOh40 As Single
        ''' <summary>El record trae FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\OohQ. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightOohQ40Presente As Boolean
        ''' <summary>FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\OohQ</summary>
        Property PhonemeTargetWeightOohQ40 As Single
        ''' <summary>El record trae FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\R. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightR40Presente As Boolean
        ''' <summary>FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\R</summary>
        Property PhonemeTargetWeightR40 As Single
        ''' <summary>El record trae FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\TH. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightTH40Presente As Boolean
        ''' <summary>FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\TH</summary>
        Property PhonemeTargetWeightTH40 As Single
        ''' <summary>El record trae FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\W. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightW40Presente As Boolean
        ''' <summary>FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\W</summary>
        Property PhonemeTargetWeightW40 As Single
        ''' <summary>El record trae FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\Aah / LipBigAah. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightAahLipBigAah41Presente As Boolean
        ''' <summary>FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Property PhonemeTargetWeightAahLipBigAah41 As Single
        ''' <summary>El record trae FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\BigAah / LipDST. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightBigAahLipDST41Presente As Boolean
        ''' <summary>FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Property PhonemeTargetWeightBigAahLipDST41 As Single
        ''' <summary>El record trae FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\BMP / LipEee. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightBMPLipEee41Presente As Boolean
        ''' <summary>FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Property PhonemeTargetWeightBMPLipEee41 As Single
        ''' <summary>El record trae FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\ChJsh / LipFV. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightChJshLipFV41Presente As Boolean
        ''' <summary>FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Property PhonemeTargetWeightChJshLipFV41 As Single
        ''' <summary>El record trae FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\DST / LipK. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightDSTLipK41Presente As Boolean
        ''' <summary>FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Property PhonemeTargetWeightDSTLipK41 As Single
        ''' <summary>El record trae FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\Eee / LipL. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightEeeLipL41Presente As Boolean
        ''' <summary>FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Property PhonemeTargetWeightEeeLipL41 As Single
        ''' <summary>El record trae FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\Eh / LipR. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightEhLipR41Presente As Boolean
        ''' <summary>FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Property PhonemeTargetWeightEhLipR41 As Single
        ''' <summary>El record trae FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\FV / LipTh. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightFVLipTh41Presente As Boolean
        ''' <summary>FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Property PhonemeTargetWeightFVLipTh41 As Single
        ''' <summary>El record trae FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\I. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightI41Presente As Boolean
        ''' <summary>FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\I</summary>
        Property PhonemeTargetWeightI41 As Single
        ''' <summary>El record trae FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\K. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightK41Presente As Boolean
        ''' <summary>FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\K</summary>
        Property PhonemeTargetWeightK41 As Single
        ''' <summary>El record trae FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\N. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightN41Presente As Boolean
        ''' <summary>FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\N</summary>
        Property PhonemeTargetWeightN41 As Single
        ''' <summary>El record trae FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\Oh. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightOh41Presente As Boolean
        ''' <summary>FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\Oh</summary>
        Property PhonemeTargetWeightOh41 As Single
        ''' <summary>El record trae FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\OohQ. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightOohQ41Presente As Boolean
        ''' <summary>FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\OohQ</summary>
        Property PhonemeTargetWeightOohQ41 As Single
        ''' <summary>El record trae FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\R. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightR41Presente As Boolean
        ''' <summary>FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\R</summary>
        Property PhonemeTargetWeightR41 As Single
        ''' <summary>El record trae FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\TH. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightTH41Presente As Boolean
        ''' <summary>FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\TH</summary>
        Property PhonemeTargetWeightTH41 As Single
        ''' <summary>El record trae FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\W. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightW41Presente As Boolean
        ''' <summary>FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\W</summary>
        Property PhonemeTargetWeightW41 As Single
        ''' <summary>El record trae FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\Aah / LipBigAah. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightAahLipBigAah42Presente As Boolean
        ''' <summary>FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Property PhonemeTargetWeightAahLipBigAah42 As Single
        ''' <summary>El record trae FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\BigAah / LipDST. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightBigAahLipDST42Presente As Boolean
        ''' <summary>FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Property PhonemeTargetWeightBigAahLipDST42 As Single
        ''' <summary>El record trae FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\BMP / LipEee. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightBMPLipEee42Presente As Boolean
        ''' <summary>FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Property PhonemeTargetWeightBMPLipEee42 As Single
        ''' <summary>El record trae FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\ChJsh / LipFV. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightChJshLipFV42Presente As Boolean
        ''' <summary>FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Property PhonemeTargetWeightChJshLipFV42 As Single
        ''' <summary>El record trae FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\DST / LipK. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightDSTLipK42Presente As Boolean
        ''' <summary>FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Property PhonemeTargetWeightDSTLipK42 As Single
        ''' <summary>El record trae FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\Eee / LipL. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightEeeLipL42Presente As Boolean
        ''' <summary>FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Property PhonemeTargetWeightEeeLipL42 As Single
        ''' <summary>El record trae FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\Eh / LipR. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightEhLipR42Presente As Boolean
        ''' <summary>FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Property PhonemeTargetWeightEhLipR42 As Single
        ''' <summary>El record trae FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\FV / LipTh. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightFVLipTh42Presente As Boolean
        ''' <summary>FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Property PhonemeTargetWeightFVLipTh42 As Single
        ''' <summary>El record trae FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\I. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightI42Presente As Boolean
        ''' <summary>FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\I</summary>
        Property PhonemeTargetWeightI42 As Single
        ''' <summary>El record trae FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\K. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightK42Presente As Boolean
        ''' <summary>FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\K</summary>
        Property PhonemeTargetWeightK42 As Single
        ''' <summary>El record trae FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\N. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightN42Presente As Boolean
        ''' <summary>FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\N</summary>
        Property PhonemeTargetWeightN42 As Single
        ''' <summary>El record trae FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\Oh. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightOh42Presente As Boolean
        ''' <summary>FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\Oh</summary>
        Property PhonemeTargetWeightOh42 As Single
        ''' <summary>El record trae FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\OohQ. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightOohQ42Presente As Boolean
        ''' <summary>FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\OohQ</summary>
        Property PhonemeTargetWeightOohQ42 As Single
        ''' <summary>El record trae FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\R. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightR42Presente As Boolean
        ''' <summary>FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\R</summary>
        Property PhonemeTargetWeightR42 As Single
        ''' <summary>El record trae FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\TH. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightTH42Presente As Boolean
        ''' <summary>FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\TH</summary>
        Property PhonemeTargetWeightTH42 As Single
        ''' <summary>El record trae FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\W. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightW42Presente As Boolean
        ''' <summary>FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\W</summary>
        Property PhonemeTargetWeightW42 As Single
        ''' <summary>El record trae FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\Aah / LipBigAah. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightAahLipBigAah43Presente As Boolean
        ''' <summary>FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Property PhonemeTargetWeightAahLipBigAah43 As Single
        ''' <summary>El record trae FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\BigAah / LipDST. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightBigAahLipDST43Presente As Boolean
        ''' <summary>FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Property PhonemeTargetWeightBigAahLipDST43 As Single
        ''' <summary>El record trae FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\BMP / LipEee. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightBMPLipEee43Presente As Boolean
        ''' <summary>FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Property PhonemeTargetWeightBMPLipEee43 As Single
        ''' <summary>El record trae FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\ChJsh / LipFV. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightChJshLipFV43Presente As Boolean
        ''' <summary>FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Property PhonemeTargetWeightChJshLipFV43 As Single
        ''' <summary>El record trae FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\DST / LipK. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightDSTLipK43Presente As Boolean
        ''' <summary>FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Property PhonemeTargetWeightDSTLipK43 As Single
        ''' <summary>El record trae FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\Eee / LipL. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightEeeLipL43Presente As Boolean
        ''' <summary>FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Property PhonemeTargetWeightEeeLipL43 As Single
        ''' <summary>El record trae FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\Eh / LipR. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightEhLipR43Presente As Boolean
        ''' <summary>FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Property PhonemeTargetWeightEhLipR43 As Single
        ''' <summary>El record trae FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\FV / LipTh. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightFVLipTh43Presente As Boolean
        ''' <summary>FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Property PhonemeTargetWeightFVLipTh43 As Single
        ''' <summary>El record trae FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\I. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightI43Presente As Boolean
        ''' <summary>FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\I</summary>
        Property PhonemeTargetWeightI43 As Single
        ''' <summary>El record trae FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\K. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightK43Presente As Boolean
        ''' <summary>FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\K</summary>
        Property PhonemeTargetWeightK43 As Single
        ''' <summary>El record trae FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\N. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightN43Presente As Boolean
        ''' <summary>FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\N</summary>
        Property PhonemeTargetWeightN43 As Single
        ''' <summary>El record trae FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\Oh. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightOh43Presente As Boolean
        ''' <summary>FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\Oh</summary>
        Property PhonemeTargetWeightOh43 As Single
        ''' <summary>El record trae FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\OohQ. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightOohQ43Presente As Boolean
        ''' <summary>FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\OohQ</summary>
        Property PhonemeTargetWeightOohQ43 As Single
        ''' <summary>El record trae FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\R. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightR43Presente As Boolean
        ''' <summary>FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\R</summary>
        Property PhonemeTargetWeightR43 As Single
        ''' <summary>El record trae FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\TH. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightTH43Presente As Boolean
        ''' <summary>FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\TH</summary>
        Property PhonemeTargetWeightTH43 As Single
        ''' <summary>El record trae FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\W. Distinto de que el campo valga cero.</summary>
        Property PhonemeTargetWeightW43Presente As Boolean
        ''' <summary>FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\W</summary>
        Property PhonemeTargetWeightW43 As Single
        ''' <summary>El record trae el marcador NAM0\Head Data Marker. No lleva valor: el dato es que esté, así que ponerlo en verdadero lo crea y en falso lo saca.</summary>
        Property HeadDataMarker As Boolean
        ''' <summary>El record trae NAM8\Morph Race. Distinto de que el campo valga cero.</summary>
        Property MorphRacePresente As Boolean
        ''' <summary>NAM8\Morph Race  -&gt;  RACE. Referencia en el espacio del orden de carga.</summary>
        Property MorphRace As UInteger
        ''' <summary>El record trae RNAM\Armor Race. Distinto de que el campo valga cero.</summary>
        Property ArmorRacePresente As Boolean
        ''' <summary>RNAM\Armor Race  -&gt;  RACE. Referencia en el espacio del orden de carga.</summary>
        Property ArmorRace As UInteger
        ''' <summary>Actor Effects</summary>
        ReadOnly Property ActorEffects As IReadOnlyList(Of IRace_ActorEffects)
        Function AgregarActorEffects() As IRace_ActorEffects
        Function QuitarActorEffects(indice As Integer) As Boolean
        Function ReordenarActorEffects(permutacion As IList(Of Integer)) As Boolean
        Function QuitarActorEffects(elemento As IRace_ActorEffects) As Boolean
        ''' <summary>Keywords\KWDA\Keywords</summary>
        ReadOnly Property Keywords As IReadOnlyList(Of IRace_Keywords)
        Function AgregarKeywords() As IRace_Keywords
        Function QuitarKeywords(indice As Integer) As Boolean
        Function ReordenarKeywords(permutacion As IList(Of Integer)) As Boolean
        Function QuitarKeywords(elemento As IRace_Keywords) As Boolean
        ''' <summary>MODT\Model Information\Textures</summary>
        ReadOnly Property Textures As IReadOnlyList(Of IRace_Textures)
        Function AgregarTextures() As IRace_Textures
        Function QuitarTextures(indice As Integer) As Boolean
        Function ReordenarTextures(permutacion As IList(Of Integer)) As Boolean
        Function QuitarTextures(elemento As IRace_Textures) As Boolean
        ''' <summary>MODT\Model Information\Counters</summary>
        ReadOnly Property Counters As IReadOnlyList(Of IRace_Counters)
        Function AgregarCounters() As IRace_Counters
        Function QuitarCounters(indice As Integer) As Boolean
        Function ReordenarCounters(permutacion As IList(Of Integer)) As Boolean
        Function QuitarCounters(elemento As IRace_Counters) As Boolean
        ''' <summary>MODT\Model Information\Addon Nodes</summary>
        ReadOnly Property AddonNodes As IReadOnlyList(Of IRace_AddonNodes)
        Function AgregarAddonNodes() As IRace_AddonNodes
        Function QuitarAddonNodes(indice As Integer) As Boolean
        Function ReordenarAddonNodes(permutacion As IList(Of Integer)) As Boolean
        Function QuitarAddonNodes(elemento As IRace_AddonNodes) As Boolean
        ''' <summary>Movement Type Names</summary>
        ReadOnly Property MovementTypeNames As IReadOnlyList(Of IRace_MovementTypeNames)
        Function AgregarMovementTypeNames() As IRace_MovementTypeNames
        Function QuitarMovementTypeNames(indice As Integer) As Boolean
        Function ReordenarMovementTypeNames(permutacion As IList(Of Integer)) As Boolean
        Function QuitarMovementTypeNames(elemento As IRace_MovementTypeNames) As Boolean
        ''' <summary>VTCK\Voices</summary>
        ReadOnly Property Voices As IReadOnlyList(Of IRace_Voices)
        Function AgregarVoices() As IRace_Voices
        Function QuitarVoices(indice As Integer) As Boolean
        Function ReordenarVoices(permutacion As IList(Of Integer)) As Boolean
        Function QuitarVoices(elemento As IRace_Voices) As Boolean
        ''' <summary>HCLF\Default Hair Colors</summary>
        ReadOnly Property DefaultHairColors As IReadOnlyList(Of IRace_DefaultHairColors)
        Function AgregarDefaultHairColors() As IRace_DefaultHairColors
        Function QuitarDefaultHairColors(indice As Integer) As Boolean
        Function ReordenarDefaultHairColors(permutacion As IList(Of Integer)) As Boolean
        Function QuitarDefaultHairColors(elemento As IRace_DefaultHairColors) As Boolean
        ''' <summary>Attacks</summary>
        ReadOnly Property Attacks As IReadOnlyList(Of IRace_Attacks)
        Function AgregarAttacks() As IRace_Attacks
        Function QuitarAttacks(indice As Integer) As Boolean
        Function ReordenarAttacks(permutacion As IList(Of Integer)) As Boolean
        Function QuitarAttacks(elemento As IRace_Attacks) As Boolean
        ''' <summary>Body Data\Male Body Data\Parts</summary>
        ReadOnly Property Parts As IReadOnlyList(Of IRace_Parts)
        Function AgregarParts() As IRace_Parts
        Function QuitarParts(indice As Integer) As Boolean
        Function ReordenarParts(permutacion As IList(Of Integer)) As Boolean
        Function QuitarParts(elemento As IRace_Parts) As Boolean
        ''' <summary>Body Data\Female Body Data\Parts</summary>
        ReadOnly Property Parts2 As IReadOnlyList(Of IRace_Parts2)
        Function AgregarParts2() As IRace_Parts2
        Function QuitarParts2(indice As Integer) As Boolean
        Function ReordenarParts2(permutacion As IList(Of Integer)) As Boolean
        Function QuitarParts2(elemento As IRace_Parts2) As Boolean
        ''' <summary>Male Behavior Graph\Model\MODT\Model Information\Textures</summary>
        ReadOnly Property Textures4 As IReadOnlyList(Of IRace_Textures4)
        Function AgregarTextures4() As IRace_Textures4
        Function QuitarTextures4(indice As Integer) As Boolean
        Function ReordenarTextures4(permutacion As IList(Of Integer)) As Boolean
        Function QuitarTextures4(elemento As IRace_Textures4) As Boolean
        ''' <summary>Male Behavior Graph\Model\MODT\Model Information\Counters</summary>
        ReadOnly Property Counters4 As IReadOnlyList(Of IRace_Counters4)
        Function AgregarCounters4() As IRace_Counters4
        Function QuitarCounters4(indice As Integer) As Boolean
        Function ReordenarCounters4(permutacion As IList(Of Integer)) As Boolean
        Function QuitarCounters4(elemento As IRace_Counters4) As Boolean
        ''' <summary>Male Behavior Graph\Model\MODT\Model Information\Addon Nodes</summary>
        ReadOnly Property AddonNodes4 As IReadOnlyList(Of IRace_AddonNodes4)
        Function AgregarAddonNodes4() As IRace_AddonNodes4
        Function QuitarAddonNodes4(indice As Integer) As Boolean
        Function ReordenarAddonNodes4(permutacion As IList(Of Integer)) As Boolean
        Function QuitarAddonNodes4(elemento As IRace_AddonNodes4) As Boolean
        ''' <summary>Female Behavior Graph\Model\MODT\Model Information\Textures</summary>
        ReadOnly Property Textures5 As IReadOnlyList(Of IRace_Textures5)
        Function AgregarTextures5() As IRace_Textures5
        Function QuitarTextures5(indice As Integer) As Boolean
        Function ReordenarTextures5(permutacion As IList(Of Integer)) As Boolean
        Function QuitarTextures5(elemento As IRace_Textures5) As Boolean
        ''' <summary>Female Behavior Graph\Model\MODT\Model Information\Counters</summary>
        ReadOnly Property Counters5 As IReadOnlyList(Of IRace_Counters5)
        Function AgregarCounters5() As IRace_Counters5
        Function QuitarCounters5(indice As Integer) As Boolean
        Function ReordenarCounters5(permutacion As IList(Of Integer)) As Boolean
        Function QuitarCounters5(elemento As IRace_Counters5) As Boolean
        ''' <summary>Female Behavior Graph\Model\MODT\Model Information\Addon Nodes</summary>
        ReadOnly Property AddonNodes5 As IReadOnlyList(Of IRace_AddonNodes5)
        Function AgregarAddonNodes5() As IRace_AddonNodes5
        Function QuitarAddonNodes5(indice As Integer) As Boolean
        Function ReordenarAddonNodes5(permutacion As IList(Of Integer)) As Boolean
        Function QuitarAddonNodes5(elemento As IRace_AddonNodes5) As Boolean
        ''' <summary>Biped Object Names</summary>
        ReadOnly Property BipedObjectNames As IReadOnlyList(Of IRace_BipedObjectNames)
        Function AgregarBipedObjectNames() As IRace_BipedObjectNames
        Function QuitarBipedObjectNames(indice As Integer) As Boolean
        Function ReordenarBipedObjectNames(permutacion As IList(Of Integer)) As Boolean
        Function QuitarBipedObjectNames(elemento As IRace_BipedObjectNames) As Boolean
        ''' <summary>Equip Slots</summary>
        ReadOnly Property EquipSlots As IReadOnlyList(Of IRace_EquipSlots)
        Function AgregarEquipSlots() As IRace_EquipSlots
        Function QuitarEquipSlots(indice As Integer) As Boolean
        Function ReordenarEquipSlots(permutacion As IList(Of Integer)) As Boolean
        Function QuitarEquipSlots(elemento As IRace_EquipSlots) As Boolean
        ''' <summary>Phoneme Target Names</summary>
        ReadOnly Property PhonemeTargetNames As IReadOnlyList(Of IRace_PhonemeTargetNames)
        Function AgregarPhonemeTargetNames() As IRace_PhonemeTargetNames
        Function QuitarPhonemeTargetNames(indice As Integer) As Boolean
        Function ReordenarPhonemeTargetNames(permutacion As IList(Of Integer)) As Boolean
        Function QuitarPhonemeTargetNames(elemento As IRace_PhonemeTargetNames) As Boolean
    End Interface

    ''' <summary>Un elemento de Actor Effects, en lo que los dos juegos comparten.</summary>
    Public Interface IRace_ActorEffects
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae SPLO\Actor Effect. Distinto de que el campo valga cero.</summary>
        Property ActorEffectPresente As Boolean
        ''' <summary>SPLO\Actor Effect  -&gt;  SPEL / LVSP. Referencia en el espacio del orden de carga.</summary>
        Property ActorEffect As UInteger
    End Interface

    ''' <summary>Un elemento de Keywords\KWDA\Keywords, en lo que los dos juegos comparten.</summary>
    Public Interface IRace_Keywords
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Keyword. Distinto de que el campo valga cero.</summary>
        Property KeywordPresente As Boolean
        ''' <summary>Keyword  -&gt;  KYWD / NULL. Referencia en el espacio del orden de carga.</summary>
        Property Keyword As UInteger
    End Interface

    ''' <summary>Un elemento de MODT\Model Information\Textures, en lo que los dos juegos comparten.</summary>
    Public Interface IRace_Textures
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Texture\File Hash. Distinto de que el campo valga cero.</summary>
        Property TextureFileHashPresente As Boolean
        ''' <summary>Texture\File Hash</summary>
        Property TextureFileHash As UInteger
        ''' <summary>El record trae Texture\Extension. Distinto de que el campo valga cero.</summary>
        Property TextureExtensionPresente As Boolean
        ''' <summary>Texture\Extension</summary>
        Property TextureExtension As String
        ''' <summary>El record trae Texture\Folder Hash. Distinto de que el campo valga cero.</summary>
        Property TextureFolderHashPresente As Boolean
        ''' <summary>Texture\Folder Hash</summary>
        Property TextureFolderHash As UInteger
    End Interface

    ''' <summary>Un elemento de MODT\Model Information\Counters, en lo que los dos juegos comparten.</summary>
    Public Interface IRace_Counters
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Counter. Distinto de que el campo valga cero.</summary>
        Property CounterPresente As Boolean
        ''' <summary>Counter</summary>
        Property Counter As UInteger
    End Interface

    ''' <summary>Un elemento de MODT\Model Information\Addon Nodes, en lo que los dos juegos comparten.</summary>
    Public Interface IRace_AddonNodes
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Addon Node. Distinto de que el campo valga cero.</summary>
        Property AddonNodePresente As Boolean
        ''' <summary>Addon Node</summary>
        Property AddonNode As UInteger
    End Interface

    ''' <summary>Un elemento de Movement Type Names, en lo que los dos juegos comparten.</summary>
    Public Interface IRace_MovementTypeNames
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae MTNM\Name. Distinto de que el campo valga cero.</summary>
        Property NamePresente As Boolean
        ''' <summary>MTNM\Name</summary>
        Property Name As String
    End Interface

    ''' <summary>Un elemento de VTCK\Voices, en lo que los dos juegos comparten.</summary>
    Public Interface IRace_Voices
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Voice. Distinto de que el campo valga cero.</summary>
        Property VoicePresente As Boolean
        ''' <summary>Voice  -&gt;  VTYP. Referencia en el espacio del orden de carga.</summary>
        Property Voice As UInteger
    End Interface

    ''' <summary>Un elemento de HCLF\Default Hair Colors, en lo que los dos juegos comparten.</summary>
    Public Interface IRace_DefaultHairColors
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Default Hair Color. Distinto de que el campo valga cero.</summary>
        Property DefaultHairColorPresente As Boolean
        ''' <summary>Default Hair Color  -&gt;  NULL / CLFM. Referencia en el espacio del orden de carga.</summary>
        Property DefaultHairColor As UInteger
    End Interface

    ''' <summary>Un elemento de Attacks, en lo que los dos juegos comparten.</summary>
    Public Interface IRace_Attacks
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Attack\ATKD\Attack Data\Damage Mult. Distinto de que el campo valga cero.</summary>
        Property AttackDataDamageMultPresente As Boolean
        ''' <summary>Attack\ATKD\Attack Data\Damage Mult</summary>
        Property AttackDataDamageMult As Single
        ''' <summary>El record trae Attack\ATKD\Attack Data\Attack Chance. Distinto de que el campo valga cero.</summary>
        Property AttackDataAttackChancePresente As Boolean
        ''' <summary>Attack\ATKD\Attack Data\Attack Chance</summary>
        Property AttackDataAttackChance As Single
        ''' <summary>El record trae Attack\ATKD\Attack Data\Attack Spell. Distinto de que el campo valga cero.</summary>
        Property AttackDataAttackSpellPresente As Boolean
        ''' <summary>Attack\ATKD\Attack Data\Attack Spell  -&gt;  SPEL / NULL. Referencia en el espacio del orden de carga.</summary>
        Property AttackDataAttackSpell As UInteger
        ''' <summary>El record trae Attack\ATKD\Attack Data\Attack Flags. Distinto de que el campo valga cero.</summary>
        Property AttackDataAttackFlagsPresente As Boolean
        ''' <summary>Attack\ATKD\Attack Data\Attack Flags</summary>
        Property AttackDataAttackFlags As UInteger
        ''' <summary>Bit 0 de Attack\ATKD\Attack Data\Attack Flags: Ignore Weapon</summary>
        Property AttackDataAttackFlagsIgnoreWeapon As Boolean
        ''' <summary>Bit 1 de Attack\ATKD\Attack Data\Attack Flags: Bash Attack</summary>
        Property AttackDataAttackFlagsBashAttack As Boolean
        ''' <summary>Bit 2 de Attack\ATKD\Attack Data\Attack Flags: Power Attack</summary>
        Property AttackDataAttackFlagsPowerAttack As Boolean
        ''' <summary>Bit 4 de Attack\ATKD\Attack Data\Attack Flags: Rotating Attack</summary>
        Property AttackDataAttackFlagsRotatingAttack As Boolean
        ''' <summary>Bit 31 de Attack\ATKD\Attack Data\Attack Flags: Override Data</summary>
        Property AttackDataAttackFlagsOverrideData As Boolean
        ''' <summary>El record trae Attack\ATKD\Attack Data\Attack Angle. Distinto de que el campo valga cero.</summary>
        Property AttackDataAttackAnglePresente As Boolean
        ''' <summary>Attack\ATKD\Attack Data\Attack Angle</summary>
        Property AttackDataAttackAngle As Single
        ''' <summary>El record trae Attack\ATKD\Attack Data\Strike Angle. Distinto de que el campo valga cero.</summary>
        Property AttackDataStrikeAnglePresente As Boolean
        ''' <summary>Attack\ATKD\Attack Data\Strike Angle</summary>
        Property AttackDataStrikeAngle As Single
        ''' <summary>El record trae Attack\ATKD\Attack Data\Stagger. Distinto de que el campo valga cero.</summary>
        Property AttackDataStaggerPresente As Boolean
        ''' <summary>Attack\ATKD\Attack Data\Stagger</summary>
        Property AttackDataStagger As Single
        ''' <summary>El record trae Attack\ATKD\Attack Data\Knockdown. Distinto de que el campo valga cero.</summary>
        Property AttackDataKnockdownPresente As Boolean
        ''' <summary>Attack\ATKD\Attack Data\Knockdown</summary>
        Property AttackDataKnockdown As Single
        ''' <summary>El record trae Attack\ATKD\Attack Data\Recovery Time. Distinto de que el campo valga cero.</summary>
        Property AttackDataRecoveryTimePresente As Boolean
        ''' <summary>Attack\ATKD\Attack Data\Recovery Time</summary>
        Property AttackDataRecoveryTime As Single
        ''' <summary>El record trae Attack\ATKE\Attack Event. Distinto de que el campo valga cero.</summary>
        Property AttackEventPresente As Boolean
        ''' <summary>Attack\ATKE\Attack Event</summary>
        Property AttackEvent As String
    End Interface

    ''' <summary>Un elemento de Body Data\Male Body Data\Parts, en lo que los dos juegos comparten.</summary>
    Public Interface IRace_Parts
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Part\Model\MODL\Model FileName. Distinto de que el campo valga cero.</summary>
        Property PartModelFileNamePresente As Boolean
        ''' <summary>Part\Model\MODL\Model FileName</summary>
        Property PartModelFileName As String
        ''' <summary>El record trae Part\Model\MODT\Model Information\ERROR. Distinto de que el campo valga cero.</summary>
        Property ModelInformationERRORPresente As Boolean
        ''' <summary>Part\Model\MODT\Model Information\ERROR</summary>
        Property ModelInformationERROR As Byte()
    End Interface

    ''' <summary>Un elemento de Body Data\Female Body Data\Parts, en lo que los dos juegos comparten.</summary>
    Public Interface IRace_Parts2
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Part\Model\MODL\Model FileName. Distinto de que el campo valga cero.</summary>
        Property PartModelFileNamePresente As Boolean
        ''' <summary>Part\Model\MODL\Model FileName</summary>
        Property PartModelFileName As String
        ''' <summary>El record trae Part\Model\MODT\Model Information\ERROR. Distinto de que el campo valga cero.</summary>
        Property ModelInformationERRORPresente As Boolean
        ''' <summary>Part\Model\MODT\Model Information\ERROR</summary>
        Property ModelInformationERROR As Byte()
    End Interface

    ''' <summary>Un elemento de Male Behavior Graph\Model\MODT\Model Information\Textures, en lo que los dos juegos comparten.</summary>
    Public Interface IRace_Textures4
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Texture\File Hash. Distinto de que el campo valga cero.</summary>
        Property TextureFileHashPresente As Boolean
        ''' <summary>Texture\File Hash</summary>
        Property TextureFileHash As UInteger
        ''' <summary>El record trae Texture\Extension. Distinto de que el campo valga cero.</summary>
        Property TextureExtensionPresente As Boolean
        ''' <summary>Texture\Extension</summary>
        Property TextureExtension As String
        ''' <summary>El record trae Texture\Folder Hash. Distinto de que el campo valga cero.</summary>
        Property TextureFolderHashPresente As Boolean
        ''' <summary>Texture\Folder Hash</summary>
        Property TextureFolderHash As UInteger
    End Interface

    ''' <summary>Un elemento de Male Behavior Graph\Model\MODT\Model Information\Counters, en lo que los dos juegos comparten.</summary>
    Public Interface IRace_Counters4
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Counter. Distinto de que el campo valga cero.</summary>
        Property CounterPresente As Boolean
        ''' <summary>Counter</summary>
        Property Counter As UInteger
    End Interface

    ''' <summary>Un elemento de Male Behavior Graph\Model\MODT\Model Information\Addon Nodes, en lo que los dos juegos comparten.</summary>
    Public Interface IRace_AddonNodes4
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Addon Node. Distinto de que el campo valga cero.</summary>
        Property AddonNodePresente As Boolean
        ''' <summary>Addon Node</summary>
        Property AddonNode As UInteger
    End Interface

    ''' <summary>Un elemento de Female Behavior Graph\Model\MODT\Model Information\Textures, en lo que los dos juegos comparten.</summary>
    Public Interface IRace_Textures5
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Texture\File Hash. Distinto de que el campo valga cero.</summary>
        Property TextureFileHashPresente As Boolean
        ''' <summary>Texture\File Hash</summary>
        Property TextureFileHash As UInteger
        ''' <summary>El record trae Texture\Extension. Distinto de que el campo valga cero.</summary>
        Property TextureExtensionPresente As Boolean
        ''' <summary>Texture\Extension</summary>
        Property TextureExtension As String
        ''' <summary>El record trae Texture\Folder Hash. Distinto de que el campo valga cero.</summary>
        Property TextureFolderHashPresente As Boolean
        ''' <summary>Texture\Folder Hash</summary>
        Property TextureFolderHash As UInteger
    End Interface

    ''' <summary>Un elemento de Female Behavior Graph\Model\MODT\Model Information\Counters, en lo que los dos juegos comparten.</summary>
    Public Interface IRace_Counters5
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Counter. Distinto de que el campo valga cero.</summary>
        Property CounterPresente As Boolean
        ''' <summary>Counter</summary>
        Property Counter As UInteger
    End Interface

    ''' <summary>Un elemento de Female Behavior Graph\Model\MODT\Model Information\Addon Nodes, en lo que los dos juegos comparten.</summary>
    Public Interface IRace_AddonNodes5
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Addon Node. Distinto de que el campo valga cero.</summary>
        Property AddonNodePresente As Boolean
        ''' <summary>Addon Node</summary>
        Property AddonNode As UInteger
    End Interface

    ''' <summary>Un elemento de Biped Object Names, en lo que los dos juegos comparten.</summary>
    Public Interface IRace_BipedObjectNames
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae NAME\Name. Distinto de que el campo valga cero.</summary>
        Property NamePresente As Boolean
        ''' <summary>NAME\Name</summary>
        Property Name As String
    End Interface

    ''' <summary>Un elemento de Equip Slots, en lo que los dos juegos comparten.</summary>
    Public Interface IRace_EquipSlots
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Equip Slot\QNAM\Equip Slot. Distinto de que el campo valga cero.</summary>
        Property EquipSlotPresente As Boolean
        ''' <summary>Equip Slot\QNAM\Equip Slot  -&gt;  EQUP. Referencia en el espacio del orden de carga.</summary>
        Property EquipSlot As UInteger
    End Interface

    ''' <summary>Un elemento de Phoneme Target Names, en lo que los dos juegos comparten.</summary>
    Public Interface IRace_PhonemeTargetNames
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae PHTN\Name. Distinto de que el campo valga cero.</summary>
        Property NamePresente As Boolean
        ''' <summary>PHTN\Name</summary>
        Property Name As String
    End Interface

    ''' <summary>Campos de un record TXST que los dos juegos declaran igual.
    ''' <para>El codigo que solo usa estos no necesita saber de que juego se trata. El
    ''' que necesita algo propio de un juego tiene que nombrar la clase de ese juego, y
    ''' asi no puede leer sin querer un campo que alla significa otra cosa.</para></summary>
    Public Interface ITxst
        ReadOnly Property Node As WbNode
        ''' <summary>Identificador del record.</summary>
        ReadOnly Property FormID As UInteger
        ''' <summary>El record trae EDID\Editor ID. Distinto de que el campo valga cero.</summary>
        Property EditorIDPresente As Boolean
        ''' <summary>EDID\Editor ID</summary>
        Property EditorID As String
        ''' <summary>El record trae OBND\Object Bounds\X1. Distinto de que el campo valga cero.</summary>
        Property ObjectBoundsX1Presente As Boolean
        ''' <summary>OBND\Object Bounds\X1</summary>
        Property ObjectBoundsX1 As Short
        ''' <summary>El record trae OBND\Object Bounds\Y1. Distinto de que el campo valga cero.</summary>
        Property ObjectBoundsY1Presente As Boolean
        ''' <summary>OBND\Object Bounds\Y1</summary>
        Property ObjectBoundsY1 As Short
        ''' <summary>El record trae OBND\Object Bounds\Z1. Distinto de que el campo valga cero.</summary>
        Property ObjectBoundsZ1Presente As Boolean
        ''' <summary>OBND\Object Bounds\Z1</summary>
        Property ObjectBoundsZ1 As Short
        ''' <summary>El record trae OBND\Object Bounds\X2. Distinto de que el campo valga cero.</summary>
        Property ObjectBoundsX2Presente As Boolean
        ''' <summary>OBND\Object Bounds\X2</summary>
        Property ObjectBoundsX2 As Short
        ''' <summary>El record trae OBND\Object Bounds\Y2. Distinto de que el campo valga cero.</summary>
        Property ObjectBoundsY2Presente As Boolean
        ''' <summary>OBND\Object Bounds\Y2</summary>
        Property ObjectBoundsY2 As Short
        ''' <summary>El record trae OBND\Object Bounds\Z2. Distinto de que el campo valga cero.</summary>
        Property ObjectBoundsZ2Presente As Boolean
        ''' <summary>OBND\Object Bounds\Z2</summary>
        Property ObjectBoundsZ2 As Short
        ''' <summary>El record trae Textures (RGB/A)\TX00\Diffuse. Distinto de que el campo valga cero.</summary>
        Property TexturesRGBADiffusePresente As Boolean
        ''' <summary>Textures (RGB/A)\TX00\Diffuse</summary>
        Property TexturesRGBADiffuse As String
        ''' <summary>El record trae Textures (RGB/A)\TX01\Normal/Gloss. Distinto de que el campo valga cero.</summary>
        Property TexturesRGBANormalGlossPresente As Boolean
        ''' <summary>Textures (RGB/A)\TX01\Normal/Gloss</summary>
        Property TexturesRGBANormalGloss As String
        ''' <summary>El record trae Textures (RGB/A)\TX04\Height. Distinto de que el campo valga cero.</summary>
        Property TexturesRGBAHeightPresente As Boolean
        ''' <summary>Textures (RGB/A)\TX04\Height</summary>
        Property TexturesRGBAHeight As String
        ''' <summary>El record trae Textures (RGB/A)\TX05\Environment. Distinto de que el campo valga cero.</summary>
        Property TexturesRGBAEnvironmentPresente As Boolean
        ''' <summary>Textures (RGB/A)\TX05\Environment</summary>
        Property TexturesRGBAEnvironment As String
        ''' <summary>El record trae Textures (RGB/A)\TX06\Multilayer. Distinto de que el campo valga cero.</summary>
        Property TexturesRGBAMultilayerPresente As Boolean
        ''' <summary>Textures (RGB/A)\TX06\Multilayer</summary>
        Property TexturesRGBAMultilayer As String
        ''' <summary>El record trae DODT\Decal Data\Min Width. Distinto de que el campo valga cero.</summary>
        Property DecalDataMinWidthPresente As Boolean
        ''' <summary>DODT\Decal Data\Min Width</summary>
        Property DecalDataMinWidth As Single
        ''' <summary>El record trae DODT\Decal Data\Max Width. Distinto de que el campo valga cero.</summary>
        Property DecalDataMaxWidthPresente As Boolean
        ''' <summary>DODT\Decal Data\Max Width</summary>
        Property DecalDataMaxWidth As Single
        ''' <summary>El record trae DODT\Decal Data\Min Height. Distinto de que el campo valga cero.</summary>
        Property DecalDataMinHeightPresente As Boolean
        ''' <summary>DODT\Decal Data\Min Height</summary>
        Property DecalDataMinHeight As Single
        ''' <summary>El record trae DODT\Decal Data\Max Height. Distinto de que el campo valga cero.</summary>
        Property DecalDataMaxHeightPresente As Boolean
        ''' <summary>DODT\Decal Data\Max Height</summary>
        Property DecalDataMaxHeight As Single
        ''' <summary>El record trae DODT\Decal Data\Depth. Distinto de que el campo valga cero.</summary>
        Property DecalDataDepthPresente As Boolean
        ''' <summary>DODT\Decal Data\Depth</summary>
        Property DecalDataDepth As Single
        ''' <summary>El record trae DODT\Decal Data\Shininess. Distinto de que el campo valga cero.</summary>
        Property DecalDataShininessPresente As Boolean
        ''' <summary>DODT\Decal Data\Shininess</summary>
        Property DecalDataShininess As Single
        ''' <summary>El record trae DODT\Decal Data\Parallax\Scale. Distinto de que el campo valga cero.</summary>
        Property ParallaxScalePresente As Boolean
        ''' <summary>DODT\Decal Data\Parallax\Scale</summary>
        Property ParallaxScale As Single
        ''' <summary>El record trae DODT\Decal Data\Parallax\Passes. Distinto de que el campo valga cero.</summary>
        Property ParallaxPassesPresente As Boolean
        ''' <summary>DODT\Decal Data\Parallax\Passes</summary>
        Property ParallaxPasses As Byte
        ''' <summary>El record trae DODT\Decal Data\Flags. Distinto de que el campo valga cero.</summary>
        Property DecalDataFlagsPresente As Boolean
        ''' <summary>DODT\Decal Data\Flags</summary>
        Property DecalDataFlags As Byte
        ''' <summary>Bit 1 de DODT\Decal Data\Flags: Alpha - Blending</summary>
        Property DecalDataFlagsAlphaBlending As Boolean
        ''' <summary>Bit 2 de DODT\Decal Data\Flags: Alpha - Testing</summary>
        Property DecalDataFlagsAlphaTesting As Boolean
        ''' <summary>Bit 3 de DODT\Decal Data\Flags: No Subtextures</summary>
        Property DecalDataFlagsNoSubtextures As Boolean
        ''' <summary>El record trae DODT\Decal Data\Color\Red. Distinto de que el campo valga cero.</summary>
        Property ColorRedPresente As Boolean
        ''' <summary>DODT\Decal Data\Color\Red</summary>
        Property ColorRed As Byte
        ''' <summary>El record trae DODT\Decal Data\Color\Green. Distinto de que el campo valga cero.</summary>
        Property ColorGreenPresente As Boolean
        ''' <summary>DODT\Decal Data\Color\Green</summary>
        Property ColorGreen As Byte
        ''' <summary>El record trae DODT\Decal Data\Color\Blue. Distinto de que el campo valga cero.</summary>
        Property ColorBluePresente As Boolean
        ''' <summary>DODT\Decal Data\Color\Blue</summary>
        Property ColorBlue As Byte
        ''' <summary>El record trae DNAM\Flags. Distinto de que el campo valga cero.</summary>
        Property FlagsPresente As Boolean
        ''' <summary>DNAM\Flags</summary>
        Property Flags As UShort
        ''' <summary>Bit 0 de DNAM\Flags: No Specular Map</summary>
        Property FlagsNoSpecularMap As Boolean
        ''' <summary>Bit 1 de DNAM\Flags: Facegen Textures</summary>
        Property FlagsFacegenTextures As Boolean
        ''' <summary>Bit 2 de DNAM\Flags: Has Model Space Normal Map</summary>
        Property FlagsHasModelSpaceNormalMap As Boolean
    End Interface

    ' ----------------------------------------------------------------------------------------
    ' Bloques que se declaran igual en mas de un lugar. Cada uno tiene
    ' su interfaz para que el codigo que los junta no necesite copiarlos
    ' a una estructura paralela escrita a mano.
    ' ----------------------------------------------------------------------------------------

    ''' <summary>Un bloque ActorEffects, venga del record que venga.
    ''' <para>Lo declaran igual: NpcFO4_ActorEffects, NpcSSE_ActorEffects, RaceFO4_ActorEffects, RaceSSE_ActorEffects.</para></summary>
    Public Interface IBloque_ActorEffects
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae SPLO\Actor Effect. Distinto de que el campo valga cero.</summary>
        Property ActorEffectPresente As Boolean
        ''' <summary>SPLO\Actor Effect  -&gt;  SPEL / LVSP. Referencia en el espacio del orden de carga.</summary>
        Property ActorEffect As UInteger
    End Interface

    ''' <summary>Un bloque AddonNodes, venga del record que venga.
    ''' <para>Lo declaran igual: ArmaFO4_AddonNodes, ArmaFO4_AddonNodes2, ArmaFO4_AddonNodes3, ArmaFO4_AddonNodes4, ArmaSSE_AddonNodes, ArmaSSE_AddonNodes2, ArmaSSE_AddonNodes3, ArmaSSE_AddonNodes4, ArmoFO4_AddonNodes, ArmoFO4_AddonNodes2, ArmoFO4_AddonNodes3, ArmoSSE_AddonNodes, ArmoSSE_AddonNodes2, ArmoSSE_AddonNodes3, BptdFO4_AddonNodes, BptdFO4_AddonNodes2, BptdSSE_AddonNodes, BptdSSE_AddonNodes2, HdptFO4_AddonNodes, HdptSSE_AddonNodes, LvlnFO4_AddonNodes, LvlnSSE_AddonNodes, NpcFO4_AddonNodes, NpcSSE_AddonNodes, OmodFO4_AddonNodes, RaceFO4_AddonNodes, RaceFO4_AddonNodes2, RaceFO4_AddonNodes3, RaceFO4_AddonNodes4, RaceFO4_AddonNodes5, RaceSSE_AddonNodes, RaceSSE_AddonNodes2, RaceSSE_AddonNodes3, RaceSSE_AddonNodes4, RaceSSE_AddonNodes5, RaceSSE_AddonNodes6, RaceSSE_AddonNodes7.</para></summary>
    Public Interface IBloque_AddonNodes
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Addon Node. Distinto de que el campo valga cero.</summary>
        Property AddonNodePresente As Boolean
        ''' <summary>Addon Node</summary>
        Property AddonNode As UInteger
    End Interface

    ''' <summary>Un bloque AliasFactions, venga del record que venga.
    ''' <para>Lo declaran igual: QustFO4_AliasFactions, QustSSE_AliasFactions.</para></summary>
    Public Interface IBloque_AliasFactions
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae ALFC\Faction. Distinto de que el campo valga cero.</summary>
        Property FactionPresente As Boolean
        ''' <summary>ALFC\Faction  -&gt;  FACT. Referencia en el espacio del orden de carga.</summary>
        Property Faction As UInteger
    End Interface

    ''' <summary>Un bloque AliasPackageData, venga del record que venga.
    ''' <para>Lo declaran igual: QustFO4_AliasPackageData, QustSSE_AliasPackageData.</para></summary>
    Public Interface IBloque_AliasPackageData
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae ALPC\Package. Distinto de que el campo valga cero.</summary>
        Property PackagePresente As Boolean
        ''' <summary>ALPC\Package  -&gt;  PACK. Referencia en el espacio del orden de carga.</summary>
        Property Package As UInteger
    End Interface

    ''' <summary>Un bloque AliasSpells, venga del record que venga.
    ''' <para>Lo declaran igual: QustFO4_AliasSpells, QustSSE_AliasSpells.</para></summary>
    Public Interface IBloque_AliasSpells
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae ALSP\Spell. Distinto de que el campo valga cero.</summary>
        Property SpellPresente As Boolean
        ''' <summary>ALSP\Spell  -&gt;  SPEL. Referencia en el espacio del orden de carga.</summary>
        Property Spell As UInteger
    End Interface

    ''' <summary>Un bloque AlternateTextures, venga del record que venga.
    ''' <para>Lo declaran igual: ArmaSSE_AlternateTextures, ArmaSSE_AlternateTextures2, ArmaSSE_AlternateTextures3, ArmaSSE_AlternateTextures4, ArmoSSE_AlternateTextures, ArmoSSE_AlternateTextures2, ArmoSSE_AlternateTextures3, BptdSSE_AlternateTextures, HdptSSE_AlternateTextures, LvlnSSE_AlternateTextures, NpcSSE_AlternateTextures, RaceSSE_AlternateTextures, RaceSSE_AlternateTextures2, RaceSSE_AlternateTextures3, RaceSSE_AlternateTextures4, RaceSSE_AlternateTextures5, RaceSSE_AlternateTextures6.</para></summary>
    Public Interface IBloque_AlternateTextures
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Alternate Texture\3D Name. Distinto de que el campo valga cero.</summary>
        Property AlternateTexture3DNamePresente As Boolean
        ''' <summary>Alternate Texture\3D Name</summary>
        Property AlternateTexture3DName As String
        ''' <summary>El record trae Alternate Texture\New Texture. Distinto de que el campo valga cero.</summary>
        Property AlternateTextureNewTexturePresente As Boolean
        ''' <summary>Alternate Texture\New Texture  -&gt;  TXST. Referencia en el espacio del orden de carga.</summary>
        Property AlternateTextureNewTexture As UInteger
        ''' <summary>El record trae Alternate Texture\3D Index. Distinto de que el campo valga cero.</summary>
        Property AlternateTexture3DIndexPresente As Boolean
        ''' <summary>Alternate Texture\3D Index</summary>
        Property AlternateTexture3DIndex As Integer
    End Interface

    ''' <summary>Un bloque ArrayOfBool, venga del record que venga.
    ''' <para>Lo declaran igual: ArmoFO4_ArrayOfBool, ArmoFO4_ArrayOfBool2, ArmoFO4_ArrayOfBool3, ArmoSSE_ArrayOfBool, NpcFO4_ArrayOfBool, NpcFO4_ArrayOfBool2, NpcFO4_ArrayOfBool3, NpcSSE_ArrayOfBool, QustFO4_ArrayOfBool, QustFO4_ArrayOfBool2, QustFO4_ArrayOfBool3, QustFO4_ArrayOfBool4, QustFO4_ArrayOfBool5, QustFO4_ArrayOfBool6, QustFO4_ArrayOfBool7, QustFO4_ArrayOfBool8, QustFO4_ArrayOfBool9, QustSSE_ArrayOfBool, QustSSE_ArrayOfBool2.</para></summary>
    Public Interface IBloque_ArrayOfBool
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Element. Distinto de que el campo valga cero.</summary>
        Property ElementPresente As Boolean
        ''' <summary>Element. Ponerlo en verdadero conserva el valor previo si no era cero.</summary>
        Property Element As Boolean
    End Interface

    ''' <summary>Un bloque ArrayOfFloat, venga del record que venga.
    ''' <para>Lo declaran igual: ArmoFO4_ArrayOfFloat, ArmoFO4_ArrayOfFloat2, ArmoFO4_ArrayOfFloat3, ArmoSSE_ArrayOfFloat, NpcFO4_ArrayOfFloat, NpcFO4_ArrayOfFloat2, NpcFO4_ArrayOfFloat3, NpcSSE_ArrayOfFloat, QustFO4_ArrayOfFloat, QustFO4_ArrayOfFloat2, QustFO4_ArrayOfFloat3, QustFO4_ArrayOfFloat4, QustFO4_ArrayOfFloat5, QustFO4_ArrayOfFloat6, QustFO4_ArrayOfFloat7, QustFO4_ArrayOfFloat8, QustFO4_ArrayOfFloat9, QustSSE_ArrayOfFloat, QustSSE_ArrayOfFloat2.</para></summary>
    Public Interface IBloque_ArrayOfFloat
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Element. Distinto de que el campo valga cero.</summary>
        Property ElementPresente As Boolean
        ''' <summary>Element</summary>
        Property Element As Single
    End Interface

    ''' <summary>Un bloque ArrayOfInt32, venga del record que venga.
    ''' <para>Lo declaran igual: ArmoFO4_ArrayOfInt32, ArmoFO4_ArrayOfInt322, ArmoFO4_ArrayOfInt323, ArmoSSE_ArrayOfInt32, NpcFO4_ArrayOfInt32, NpcFO4_ArrayOfInt322, NpcFO4_ArrayOfInt323, NpcSSE_ArrayOfInt32, QustFO4_ArrayOfInt32, QustFO4_ArrayOfInt322, QustFO4_ArrayOfInt323, QustFO4_ArrayOfInt324, QustFO4_ArrayOfInt325, QustFO4_ArrayOfInt326, QustFO4_ArrayOfInt327, QustFO4_ArrayOfInt328, QustFO4_ArrayOfInt329, QustSSE_ArrayOfInt32, QustSSE_ArrayOfInt322.</para></summary>
    Public Interface IBloque_ArrayOfInt32
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Element. Distinto de que el campo valga cero.</summary>
        Property ElementPresente As Boolean
        ''' <summary>Element</summary>
        Property Element As Integer
    End Interface

    ''' <summary>Un bloque ArrayOfObject, venga del record que venga.
    ''' <para>Lo declaran igual: ArmoFO4_ArrayOfObject, ArmoFO4_ArrayOfObject2, ArmoFO4_ArrayOfObject3, ArmoSSE_ArrayOfObject, NpcFO4_ArrayOfObject, NpcFO4_ArrayOfObject2, NpcFO4_ArrayOfObject3, NpcSSE_ArrayOfObject, QustFO4_ArrayOfObject, QustFO4_ArrayOfObject2, QustFO4_ArrayOfObject3, QustFO4_ArrayOfObject4, QustFO4_ArrayOfObject5, QustFO4_ArrayOfObject6, QustFO4_ArrayOfObject7, QustFO4_ArrayOfObject8, QustFO4_ArrayOfObject9, QustSSE_ArrayOfObject, QustSSE_ArrayOfObject2.</para></summary>
    Public Interface IBloque_ArrayOfObject
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Object Union\Object v2\Alias. Distinto de que el campo valga cero.</summary>
        Property ObjectV2AliasPresente As Boolean
        ''' <summary>Object Union\Object v2\Alias</summary>
        Property ObjectV2Alias As Short
        ''' <summary>El record trae Object Union\Object v2\FormID. Distinto de que el campo valga cero.</summary>
        Property ObjectV2FormIDPresente As Boolean
        ''' <summary>Object Union\Object v2\FormID. Referencia en el espacio del orden de carga.</summary>
        Property ObjectV2FormID As UInteger
        ''' <summary>El record trae Object Union\Object v1\FormID. Distinto de que el campo valga cero.</summary>
        Property ObjectV1FormIDPresente As Boolean
        ''' <summary>Object Union\Object v1\FormID. Referencia en el espacio del orden de carga.</summary>
        Property ObjectV1FormID As UInteger
        ''' <summary>El record trae Object Union\Object v1\Alias. Distinto de que el campo valga cero.</summary>
        Property ObjectV1AliasPresente As Boolean
        ''' <summary>Object Union\Object v1\Alias</summary>
        Property ObjectV1Alias As Short
    End Interface

    ''' <summary>Un bloque ArrayOfString, venga del record que venga.
    ''' <para>Lo declaran igual: ArmoFO4_ArrayOfString, ArmoFO4_ArrayOfString2, ArmoFO4_ArrayOfString3, ArmoSSE_ArrayOfString, NpcFO4_ArrayOfString, NpcFO4_ArrayOfString2, NpcFO4_ArrayOfString3, NpcSSE_ArrayOfString, QustFO4_ArrayOfString, QustFO4_ArrayOfString2, QustFO4_ArrayOfString3, QustFO4_ArrayOfString4, QustFO4_ArrayOfString5, QustFO4_ArrayOfString6, QustFO4_ArrayOfString7, QustFO4_ArrayOfString8, QustFO4_ArrayOfString9, QustSSE_ArrayOfString, QustSSE_ArrayOfString2.</para></summary>
    Public Interface IBloque_ArrayOfString
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Element. Distinto de que el campo valga cero.</summary>
        Property ElementPresente As Boolean
        ''' <summary>Element</summary>
        Property Element As String
    End Interface

    ''' <summary>Un bloque AttachParentSlots, venga del record que venga.
    ''' <para>Lo declaran igual: ArmoFO4_AttachParentSlots, NpcFO4_AttachParentSlots, OmodFO4_AttachParentSlots, RaceFO4_AttachParentSlots.</para></summary>
    Public Interface IBloque_AttachParentSlots
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Keyword. Distinto de que el campo valga cero.</summary>
        Property KeywordPresente As Boolean
        ''' <summary>Keyword  -&gt;  KYWD. Referencia en el espacio del orden de carga.</summary>
        Property Keyword As UInteger
    End Interface

    ''' <summary>Un bloque Attacks, venga del record que venga.
    ''' <para>Lo declaran igual: NpcFO4_Attacks, RaceFO4_Attacks.</para></summary>
    Public Interface IBloque_Attacks
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Attack\ATKD\Attack Data\Damage Mult. Distinto de que el campo valga cero.</summary>
        Property AttackDataDamageMultPresente As Boolean
        ''' <summary>Attack\ATKD\Attack Data\Damage Mult</summary>
        Property AttackDataDamageMult As Single
        ''' <summary>El record trae Attack\ATKD\Attack Data\Attack Chance. Distinto de que el campo valga cero.</summary>
        Property AttackDataAttackChancePresente As Boolean
        ''' <summary>Attack\ATKD\Attack Data\Attack Chance</summary>
        Property AttackDataAttackChance As Single
        ''' <summary>El record trae Attack\ATKD\Attack Data\Attack Spell. Distinto de que el campo valga cero.</summary>
        Property AttackDataAttackSpellPresente As Boolean
        ''' <summary>Attack\ATKD\Attack Data\Attack Spell  -&gt;  SPEL / NULL. Referencia en el espacio del orden de carga.</summary>
        Property AttackDataAttackSpell As UInteger
        ''' <summary>El record trae Attack\ATKD\Attack Data\Attack Flags. Distinto de que el campo valga cero.</summary>
        Property AttackDataAttackFlagsPresente As Boolean
        ''' <summary>Attack\ATKD\Attack Data\Attack Flags</summary>
        Property AttackDataAttackFlags As UInteger
        ''' <summary>Bit 0 de Attack\ATKD\Attack Data\Attack Flags: Ignore Weapon</summary>
        Property AttackDataAttackFlagsIgnoreWeapon As Boolean
        ''' <summary>Bit 1 de Attack\ATKD\Attack Data\Attack Flags: Bash Attack</summary>
        Property AttackDataAttackFlagsBashAttack As Boolean
        ''' <summary>Bit 2 de Attack\ATKD\Attack Data\Attack Flags: Power Attack</summary>
        Property AttackDataAttackFlagsPowerAttack As Boolean
        ''' <summary>Bit 3 de Attack\ATKD\Attack Data\Attack Flags: Charge Attack</summary>
        Property AttackDataAttackFlagsChargeAttack As Boolean
        ''' <summary>Bit 4 de Attack\ATKD\Attack Data\Attack Flags: Rotating Attack</summary>
        Property AttackDataAttackFlagsRotatingAttack As Boolean
        ''' <summary>Bit 5 de Attack\ATKD\Attack Data\Attack Flags: Continuous Attack</summary>
        Property AttackDataAttackFlagsContinuousAttack As Boolean
        ''' <summary>Bit 31 de Attack\ATKD\Attack Data\Attack Flags: Override Data</summary>
        Property AttackDataAttackFlagsOverrideData As Boolean
        ''' <summary>El record trae Attack\ATKD\Attack Data\Attack Angle. Distinto de que el campo valga cero.</summary>
        Property AttackDataAttackAnglePresente As Boolean
        ''' <summary>Attack\ATKD\Attack Data\Attack Angle</summary>
        Property AttackDataAttackAngle As Single
        ''' <summary>El record trae Attack\ATKD\Attack Data\Strike Angle. Distinto de que el campo valga cero.</summary>
        Property AttackDataStrikeAnglePresente As Boolean
        ''' <summary>Attack\ATKD\Attack Data\Strike Angle</summary>
        Property AttackDataStrikeAngle As Single
        ''' <summary>El record trae Attack\ATKD\Attack Data\Stagger. Distinto de que el campo valga cero.</summary>
        Property AttackDataStaggerPresente As Boolean
        ''' <summary>Attack\ATKD\Attack Data\Stagger</summary>
        Property AttackDataStagger As Single
        ''' <summary>El record trae Attack\ATKD\Attack Data\Knockdown. Distinto de que el campo valga cero.</summary>
        Property AttackDataKnockdownPresente As Boolean
        ''' <summary>Attack\ATKD\Attack Data\Knockdown</summary>
        Property AttackDataKnockdown As Single
        ''' <summary>El record trae Attack\ATKD\Attack Data\Recovery Time. Distinto de que el campo valga cero.</summary>
        Property AttackDataRecoveryTimePresente As Boolean
        ''' <summary>Attack\ATKD\Attack Data\Recovery Time</summary>
        Property AttackDataRecoveryTime As Single
        ''' <summary>El record trae Attack\ATKD\Attack Data\Action Points Mult. Distinto de que el campo valga cero.</summary>
        Property AttackDataActionPointsMultPresente As Boolean
        ''' <summary>Attack\ATKD\Attack Data\Action Points Mult</summary>
        Property AttackDataActionPointsMult As Single
        ''' <summary>El record trae Attack\ATKD\Attack Data\Stagger Offset. Distinto de que el campo valga cero.</summary>
        Property AttackDataStaggerOffsetPresente As Boolean
        ''' <summary>Attack\ATKD\Attack Data\Stagger Offset</summary>
        Property AttackDataStaggerOffset As Integer
        ''' <summary>El record trae Attack\ATKE\Attack Event. Distinto de que el campo valga cero.</summary>
        Property AttackEventPresente As Boolean
        ''' <summary>Attack\ATKE\Attack Event</summary>
        Property AttackEvent As String
        ''' <summary>El record trae Attack\ATKW\Weapon Slot. Distinto de que el campo valga cero.</summary>
        Property AttackWeaponSlotPresente As Boolean
        ''' <summary>Attack\ATKW\Weapon Slot  -&gt;  EQUP. Referencia en el espacio del orden de carga.</summary>
        Property AttackWeaponSlot As UInteger
        ''' <summary>El record trae Attack\ATKS\Required Slot. Distinto de que el campo valga cero.</summary>
        Property AttackRequiredSlotPresente As Boolean
        ''' <summary>Attack\ATKS\Required Slot  -&gt;  EQUP. Referencia en el espacio del orden de carga.</summary>
        Property AttackRequiredSlot As UInteger
        ''' <summary>El record trae Attack\ATKT\Description. Distinto de que el campo valga cero.</summary>
        Property AttackDescriptionPresente As Boolean
        ''' <summary>Attack\ATKT\Description</summary>
        Property AttackDescription As String
    End Interface

    ''' <summary>Un bloque Attacks2, venga del record que venga.
    ''' <para>Lo declaran igual: NpcSSE_Attacks, RaceSSE_Attacks.</para></summary>
    Public Interface IBloque_Attacks2
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Attack\ATKD\Attack Data\Damage Mult. Distinto de que el campo valga cero.</summary>
        Property AttackDataDamageMultPresente As Boolean
        ''' <summary>Attack\ATKD\Attack Data\Damage Mult</summary>
        Property AttackDataDamageMult As Single
        ''' <summary>El record trae Attack\ATKD\Attack Data\Attack Chance. Distinto de que el campo valga cero.</summary>
        Property AttackDataAttackChancePresente As Boolean
        ''' <summary>Attack\ATKD\Attack Data\Attack Chance</summary>
        Property AttackDataAttackChance As Single
        ''' <summary>El record trae Attack\ATKD\Attack Data\Attack Spell. Distinto de que el campo valga cero.</summary>
        Property AttackDataAttackSpellPresente As Boolean
        ''' <summary>Attack\ATKD\Attack Data\Attack Spell  -&gt;  SPEL / SHOU / NULL. Referencia en el espacio del orden de carga.</summary>
        Property AttackDataAttackSpell As UInteger
        ''' <summary>El record trae Attack\ATKD\Attack Data\Attack Flags. Distinto de que el campo valga cero.</summary>
        Property AttackDataAttackFlagsPresente As Boolean
        ''' <summary>Attack\ATKD\Attack Data\Attack Flags</summary>
        Property AttackDataAttackFlags As UInteger
        ''' <summary>Bit 0 de Attack\ATKD\Attack Data\Attack Flags: Ignore Weapon</summary>
        Property AttackDataAttackFlagsIgnoreWeapon As Boolean
        ''' <summary>Bit 1 de Attack\ATKD\Attack Data\Attack Flags: Bash Attack</summary>
        Property AttackDataAttackFlagsBashAttack As Boolean
        ''' <summary>Bit 2 de Attack\ATKD\Attack Data\Attack Flags: Power Attack</summary>
        Property AttackDataAttackFlagsPowerAttack As Boolean
        ''' <summary>Bit 3 de Attack\ATKD\Attack Data\Attack Flags: Left Attack</summary>
        Property AttackDataAttackFlagsLeftAttack As Boolean
        ''' <summary>Bit 4 de Attack\ATKD\Attack Data\Attack Flags: Rotating Attack</summary>
        Property AttackDataAttackFlagsRotatingAttack As Boolean
        ''' <summary>Bit 31 de Attack\ATKD\Attack Data\Attack Flags: Override Data</summary>
        Property AttackDataAttackFlagsOverrideData As Boolean
        ''' <summary>El record trae Attack\ATKD\Attack Data\Attack Angle. Distinto de que el campo valga cero.</summary>
        Property AttackDataAttackAnglePresente As Boolean
        ''' <summary>Attack\ATKD\Attack Data\Attack Angle</summary>
        Property AttackDataAttackAngle As Single
        ''' <summary>El record trae Attack\ATKD\Attack Data\Strike Angle. Distinto de que el campo valga cero.</summary>
        Property AttackDataStrikeAnglePresente As Boolean
        ''' <summary>Attack\ATKD\Attack Data\Strike Angle</summary>
        Property AttackDataStrikeAngle As Single
        ''' <summary>El record trae Attack\ATKD\Attack Data\Stagger. Distinto de que el campo valga cero.</summary>
        Property AttackDataStaggerPresente As Boolean
        ''' <summary>Attack\ATKD\Attack Data\Stagger</summary>
        Property AttackDataStagger As Single
        ''' <summary>El record trae Attack\ATKD\Attack Data\Attack Type. Distinto de que el campo valga cero.</summary>
        Property AttackDataAttackTypePresente As Boolean
        ''' <summary>Attack\ATKD\Attack Data\Attack Type  -&gt;  KYWD / NULL. Referencia en el espacio del orden de carga.</summary>
        Property AttackDataAttackType As UInteger
        ''' <summary>El record trae Attack\ATKD\Attack Data\Knockdown. Distinto de que el campo valga cero.</summary>
        Property AttackDataKnockdownPresente As Boolean
        ''' <summary>Attack\ATKD\Attack Data\Knockdown</summary>
        Property AttackDataKnockdown As Single
        ''' <summary>El record trae Attack\ATKD\Attack Data\Recovery Time. Distinto de que el campo valga cero.</summary>
        Property AttackDataRecoveryTimePresente As Boolean
        ''' <summary>Attack\ATKD\Attack Data\Recovery Time</summary>
        Property AttackDataRecoveryTime As Single
        ''' <summary>El record trae Attack\ATKD\Attack Data\Stamina Mult. Distinto de que el campo valga cero.</summary>
        Property AttackDataStaminaMultPresente As Boolean
        ''' <summary>Attack\ATKD\Attack Data\Stamina Mult</summary>
        Property AttackDataStaminaMult As Single
        ''' <summary>El record trae Attack\ATKE\Attack Event. Distinto de que el campo valga cero.</summary>
        Property AttackEventPresente As Boolean
        ''' <summary>Attack\ATKE\Attack Event</summary>
        Property AttackEvent As String
    End Interface

    ''' <summary>Un bloque Combinations, venga del record que venga.
    ''' <para>Lo declaran igual: ArmoFO4_Combinations, NpcFO4_Combinations.</para></summary>
    Public Interface IBloque_Combinations
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae el marcador Combination\OBTF\Editor Only. No lleva valor: el dato es que esté, así que ponerlo en verdadero lo crea y en falso lo saca.</summary>
        Property CombinationEditorOnly As Boolean
        ''' <summary>El record trae Combination\FULL\Name. Distinto de que el campo valga cero.</summary>
        Property CombinationNamePresente As Boolean
        ''' <summary>Combination\FULL\Name. Resuelto contra las tablas de texto si el archivo las usa.</summary>
        Property CombinationName As String
        ''' <summary>El record trae Combination\OBTS\Object Mod Template Item\Include Count. Distinto de que el campo valga cero.</summary>
        Property ObjectModTemplateItemIncludeCountPresente As Boolean
        ''' <summary>Combination\OBTS\Object Mod Template Item\Include Count</summary>
        Property ObjectModTemplateItemIncludeCount As UInteger
        ''' <summary>El record trae Combination\OBTS\Object Mod Template Item\Property Count. Distinto de que el campo valga cero.</summary>
        Property ObjectModTemplateItemPropertyCountPresente As Boolean
        ''' <summary>Combination\OBTS\Object Mod Template Item\Property Count</summary>
        Property ObjectModTemplateItemPropertyCount As UInteger
        ''' <summary>El record trae Combination\OBTS\Object Mod Template Item\Level Min. Distinto de que el campo valga cero.</summary>
        Property ObjectModTemplateItemLevelMinPresente As Boolean
        ''' <summary>Combination\OBTS\Object Mod Template Item\Level Min</summary>
        Property ObjectModTemplateItemLevelMin As Byte
        ''' <summary>El record trae Combination\OBTS\Object Mod Template Item\Level Max. Distinto de que el campo valga cero.</summary>
        Property ObjectModTemplateItemLevelMaxPresente As Boolean
        ''' <summary>Combination\OBTS\Object Mod Template Item\Level Max</summary>
        Property ObjectModTemplateItemLevelMax As Byte
        ''' <summary>El record trae Combination\OBTS\Object Mod Template Item\Parent Combination Index. Distinto de que el campo valga cero.</summary>
        Property ObjectModTemplateItemParentCombinationIndexPresente As Boolean
        ''' <summary>Combination\OBTS\Object Mod Template Item\Parent Combination Index</summary>
        Property ObjectModTemplateItemParentCombinationIndex As Short
        ''' <summary>El record trae Combination\OBTS\Object Mod Template Item\Default. Distinto de que el campo valga cero.</summary>
        Property ObjectModTemplateItemDefaultPresente As Boolean
        ''' <summary>Combination\OBTS\Object Mod Template Item\Default. Ponerlo en verdadero conserva el valor previo si no era cero.</summary>
        Property ObjectModTemplateItemDefault As Boolean
        ''' <summary>El record trae Combination\OBTS\Object Mod Template Item\Min Level For Ranks. Distinto de que el campo valga cero.</summary>
        Property ObjectModTemplateItemMinLevelForRanksPresente As Boolean
        ''' <summary>Combination\OBTS\Object Mod Template Item\Min Level For Ranks</summary>
        Property ObjectModTemplateItemMinLevelForRanks As Byte
        ''' <summary>El record trae Combination\OBTS\Object Mod Template Item\Alt Levels Per Tier. Distinto de que el campo valga cero.</summary>
        Property ObjectModTemplateItemAltLevelsPerTierPresente As Boolean
        ''' <summary>Combination\OBTS\Object Mod Template Item\Alt Levels Per Tier</summary>
        Property ObjectModTemplateItemAltLevelsPerTier As Byte
        ''' <summary>Object Template\Combinations\Combination\OBTS\Object Mod Template Item\Keywords</summary>
        ReadOnly Property Keywords As IReadOnlyList(Of IBloque_Keywords)
        ''' <summary>Object Template\Combinations\Combination\OBTS\Object Mod Template Item\Includes</summary>
        ReadOnly Property Includes As IReadOnlyList(Of IBloque_Includes)
        ''' <summary>Object Template\Combinations\Combination\OBTS\Object Mod Template Item\Properties</summary>
        ReadOnly Property Properties As IReadOnlyList(Of IBloque_Properties4)
    End Interface

    ''' <summary>Un bloque Conditions, venga del record que venga.
    ''' <para>Lo declaran igual: IdleSSE_Conditions, QustSSE_Conditions, QustSSE_Conditions2, QustSSE_Conditions3, QustSSE_Conditions4, QustSSE_Conditions5, QustSSE_Conditions6, QustSSE_Conditions7.</para></summary>
    Public Interface IBloque_Conditions
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Condition\CTDA\Type. Distinto de que el campo valga cero.</summary>
        Property ConditionTypePresente As Boolean
        ''' <summary>Condition\CTDA\Type</summary>
        Property ConditionType As Byte
        ''' <summary>El record trae Condition\CTDA\Comparison Value\Comparison Value - Float. Distinto de que el campo valga cero.</summary>
        Property ConditionComparisonValueFloatPresente As Boolean
        ''' <summary>Condition\CTDA\Comparison Value\Comparison Value - Float</summary>
        Property ConditionComparisonValueFloat As Single
        ''' <summary>El record trae Condition\CTDA\Comparison Value\Comparison Value - Global. Distinto de que el campo valga cero.</summary>
        Property ConditionComparisonValueGlobalPresente As Boolean
        ''' <summary>Condition\CTDA\Comparison Value\Comparison Value - Global  -&gt;  GLOB. Referencia en el espacio del orden de carga.</summary>
        Property ConditionComparisonValueGlobal As UInteger
        ''' <summary>El record trae Condition\CTDA\Function. Distinto de que el campo valga cero.</summary>
        Property ConditionFunctionPresente As Boolean
        ''' <summary>Condition\CTDA\Function</summary>
        Property ConditionFunction As UShort
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Unknown. Distinto de que el campo valga cero.</summary>
        Property Parameter1UnknownPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Unknown</summary>
        Property Parameter1Unknown As Byte()
        ''' <summary>El record trae Condition\CTDA\Parameter #1\None. Distinto de que el campo valga cero.</summary>
        Property Parameter1NonePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\None</summary>
        Property Parameter1None As Byte()
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Float. Distinto de que el campo valga cero.</summary>
        Property Parameter1FloatPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Float</summary>
        Property Parameter1Float As Single
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Integer. Distinto de que el campo valga cero.</summary>
        Property Parameter1IntegerPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Integer</summary>
        Property Parameter1Integer As Integer
        ''' <summary>El record trae Condition\CTDA\Parameter #1\String. Distinto de que el campo valga cero.</summary>
        Property Parameter1StringPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\String</summary>
        Property Parameter1String As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Alias. Distinto de que el campo valga cero.</summary>
        Property Parameter1AliasPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Alias</summary>
        Property Parameter1Alias As Integer
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Event. Distinto de que el campo valga cero.</summary>
        Property Parameter1EventPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Event</summary>
        Property Parameter1Event As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Packdata ID. Distinto de que el campo valga cero.</summary>
        Property Parameter1PackdataIDPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Packdata ID</summary>
        Property Parameter1PackdataID As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Quest Stage. Distinto de que el campo valga cero.</summary>
        Property Parameter1QuestStagePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Quest Stage</summary>
        Property Parameter1QuestStage As Integer
        ''' <summary>El record trae Condition\CTDA\Parameter #1\VATS Value Param\Weapon. Distinto de que el campo valga cero.</summary>
        Property VATSValueParamWeaponPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\VATS Value Param\Weapon  -&gt;  WEAP. Referencia en el espacio del orden de carga.</summary>
        Property VATSValueParamWeapon As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\VATS Value Param\Weapon List. Distinto de que el campo valga cero.</summary>
        Property VATSValueParamWeaponListPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\VATS Value Param\Weapon List  -&gt;  FLST. Referencia en el espacio del orden de carga.</summary>
        Property VATSValueParamWeaponList As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\VATS Value Param\Target. Distinto de que el campo valga cero.</summary>
        Property VATSValueParamTargetPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\VATS Value Param\Target  -&gt;  NPC_. Referencia en el espacio del orden de carga.</summary>
        Property VATSValueParamTarget As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\VATS Value Param\Target List. Distinto de que el campo valga cero.</summary>
        Property VATSValueParamTargetListPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\VATS Value Param\Target List  -&gt;  FLST. Referencia en el espacio del orden de carga.</summary>
        Property VATSValueParamTargetList As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\VATS Value Param\Unknown. Distinto de que el campo valga cero.</summary>
        Property VATSValueParamUnknownPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\VATS Value Param\Unknown</summary>
        Property VATSValueParamUnknown As Byte()
        ''' <summary>El record trae Condition\CTDA\Parameter #1\VATS Value Param\Target Part. Distinto de que el campo valga cero.</summary>
        Property VATSValueParamTargetPartPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\VATS Value Param\Target Part</summary>
        Property VATSValueParamTargetPart As Integer
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #1\VATS Value Param\Target Part.</summary>
        ReadOnly Property VATSValueParamTargetPartNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #1\VATS Value Param\VATS Action. Distinto de que el campo valga cero.</summary>
        Property VATSValueParamVATSActionPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\VATS Value Param\VATS Action</summary>
        Property VATSValueParamVATSAction As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #1\VATS Value Param\VATS Action.</summary>
        ReadOnly Property VATSValueParamVATSActionNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #1\VATS Value Param\Critical Effect. Distinto de que el campo valga cero.</summary>
        Property VATSValueParamCriticalEffectPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\VATS Value Param\Critical Effect  -&gt;  SPEL. Referencia en el espacio del orden de carga.</summary>
        Property VATSValueParamCriticalEffect As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\VATS Value Param\Critical Effect List. Distinto de que el campo valga cero.</summary>
        Property VATSValueParamCriticalEffectListPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\VATS Value Param\Critical Effect List  -&gt;  FLST. Referencia en el espacio del orden de carga.</summary>
        Property VATSValueParamCriticalEffectList As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\VATS Value Param\Weapon Type. Distinto de que el campo valga cero.</summary>
        Property VATSValueParamWeaponTypePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\VATS Value Param\Weapon Type</summary>
        Property VATSValueParamWeaponType As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #1\VATS Value Param\Weapon Type.</summary>
        ReadOnly Property VATSValueParamWeaponTypeNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #1\VATS Value Param\Projectile Type. Distinto de que el campo valga cero.</summary>
        Property VATSValueParamProjectileTypePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\VATS Value Param\Projectile Type</summary>
        Property VATSValueParamProjectileType As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #1\VATS Value Param\Projectile Type.</summary>
        ReadOnly Property VATSValueParamProjectileTypeNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #1\VATS Value Param\Delivery. Distinto de que el campo valga cero.</summary>
        Property VATSValueParamDeliveryPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\VATS Value Param\Delivery</summary>
        Property VATSValueParamDelivery As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #1\VATS Value Param\Delivery.</summary>
        ReadOnly Property VATSValueParamDeliveryNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #1\VATS Value Param\Casting Type. Distinto de que el campo valga cero.</summary>
        Property VATSValueParamCastingTypePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\VATS Value Param\Casting Type</summary>
        Property VATSValueParamCastingType As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #1\VATS Value Param\Casting Type.</summary>
        ReadOnly Property VATSValueParamCastingTypeNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Actor Value. Distinto de que el campo valga cero.</summary>
        Property Parameter1ActorValuePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Actor Value</summary>
        Property Parameter1ActorValue As Integer
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #1\Actor Value.</summary>
        ReadOnly Property Parameter1ActorValueNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Alignment. Distinto de que el campo valga cero.</summary>
        Property Parameter1AlignmentPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Alignment</summary>
        Property Parameter1Alignment As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #1\Alignment.</summary>
        ReadOnly Property Parameter1AlignmentNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Axis. Distinto de que el campo valga cero.</summary>
        Property Parameter1AxisPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Axis</summary>
        Property Parameter1Axis As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #1\Axis.</summary>
        ReadOnly Property Parameter1AxisNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Casting Source. Distinto de que el campo valga cero.</summary>
        Property Parameter1CastingSourcePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Casting Source</summary>
        Property Parameter1CastingSource As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #1\Casting Source.</summary>
        ReadOnly Property Parameter1CastingSourceNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Crime Type. Distinto de que el campo valga cero.</summary>
        Property Parameter1CrimeTypePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Crime Type</summary>
        Property Parameter1CrimeType As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #1\Crime Type.</summary>
        ReadOnly Property Parameter1CrimeTypeNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Critical Stage. Distinto de que el campo valga cero.</summary>
        Property Parameter1CriticalStagePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Critical Stage</summary>
        Property Parameter1CriticalStage As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #1\Critical Stage.</summary>
        ReadOnly Property Parameter1CriticalStageNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Form Type. Distinto de que el campo valga cero.</summary>
        Property Parameter1FormTypePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Form Type</summary>
        Property Parameter1FormType As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #1\Form Type.</summary>
        ReadOnly Property Parameter1FormTypeNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Furniture Anim. Distinto de que el campo valga cero.</summary>
        Property Parameter1FurnitureAnimPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Furniture Anim</summary>
        Property Parameter1FurnitureAnim As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #1\Furniture Anim.</summary>
        ReadOnly Property Parameter1FurnitureAnimNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Furniture Entry. Distinto de que el campo valga cero.</summary>
        Property Parameter1FurnitureEntryPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Furniture Entry</summary>
        Property Parameter1FurnitureEntry As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #1\Furniture Entry.</summary>
        ReadOnly Property Parameter1FurnitureEntryNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Misc Stat. Distinto de que el campo valga cero.</summary>
        Property Parameter1MiscStatPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Misc Stat</summary>
        Property Parameter1MiscStat As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #1\Misc Stat.</summary>
        ReadOnly Property Parameter1MiscStatNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Player Action. Distinto de que el campo valga cero.</summary>
        Property Parameter1PlayerActionPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Player Action</summary>
        Property Parameter1PlayerAction As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #1\Player Action.</summary>
        ReadOnly Property Parameter1PlayerActionNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Sex. Distinto de que el campo valga cero.</summary>
        Property Parameter1SexPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Sex</summary>
        Property Parameter1Sex As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #1\Sex.</summary>
        ReadOnly Property Parameter1SexNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #1\VATS Value Function. Distinto de que el campo valga cero.</summary>
        Property Parameter1VATSValueFunctionPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\VATS Value Function</summary>
        Property Parameter1VATSValueFunction As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Ward State. Distinto de que el campo valga cero.</summary>
        Property Parameter1WardStatePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Ward State</summary>
        Property Parameter1WardState As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #1\Ward State.</summary>
        ReadOnly Property Parameter1WardStateNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Actor. Distinto de que el campo valga cero.</summary>
        Property Parameter1ActorPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Actor  -&gt;  ACHR / PLYR / REFR / TRGT. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Actor As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Actor Base. Distinto de que el campo valga cero.</summary>
        Property Parameter1ActorBasePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Actor Base  -&gt;  NPC_. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1ActorBase As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Association Type. Distinto de que el campo valga cero.</summary>
        Property Parameter1AssociationTypePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Association Type  -&gt;  ASTP. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1AssociationType As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Base Object. Distinto de que el campo valga cero.</summary>
        Property Parameter1BaseObjectPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Base Object. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1BaseObject As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Cell. Distinto de que el campo valga cero.</summary>
        Property Parameter1CellPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Cell  -&gt;  CELL. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Cell As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Class. Distinto de que el campo valga cero.</summary>
        Property Parameter1ClassPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Class  -&gt;  CLAS. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Class As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Effect Item. Distinto de que el campo valga cero.</summary>
        Property Parameter1EffectItemPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Effect Item  -&gt;  ALCH / ENCH / INGR / SCRL / SPEL. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1EffectItem As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Encounter Zone. Distinto de que el campo valga cero.</summary>
        Property Parameter1EncounterZonePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Encounter Zone  -&gt;  ECZN. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1EncounterZone As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Equip Type. Distinto de que el campo valga cero.</summary>
        Property Parameter1EquipTypePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Equip Type  -&gt;  EQUP. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1EquipType As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Event Data. Distinto de que el campo valga cero.</summary>
        Property Parameter1EventDataPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Event Data. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1EventData As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Faction. Distinto de que el campo valga cero.</summary>
        Property Parameter1FactionPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Faction  -&gt;  FACT. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Faction As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Form List. Distinto de que el campo valga cero.</summary>
        Property Parameter1FormListPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Form List  -&gt;  FLST. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1FormList As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Furniture. Distinto de que el campo valga cero.</summary>
        Property Parameter1FurniturePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Furniture  -&gt;  FLST / FURN. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Furniture As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Global. Distinto de que el campo valga cero.</summary>
        Property Parameter1GlobalPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Global  -&gt;  GLOB. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Global As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Idle. Distinto de que el campo valga cero.</summary>
        Property Parameter1IdlePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Idle  -&gt;  IDLE. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Idle As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Inventory Object. Distinto de que el campo valga cero.</summary>
        Property Parameter1InventoryObjectPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Inventory Object  -&gt;  ALCH / AMMO / ARMO / BOOK / COBJ / FLST / INGR / KEYM / LIGH / LVLI / MISC / SCRL / SLGM / WEAP. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1InventoryObject As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Keyword. Distinto de que el campo valga cero.</summary>
        Property Parameter1KeywordPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Keyword  -&gt;  KYWD / NULL. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Keyword As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Knowable. Distinto de que el campo valga cero.</summary>
        Property Parameter1KnowablePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Knowable  -&gt;  ENCH / MGEF / WOOP. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Knowable As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Location. Distinto de que el campo valga cero.</summary>
        Property Parameter1LocationPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Location  -&gt;  LCTN. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Location As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Location Ref Type. Distinto de que el campo valga cero.</summary>
        Property Parameter1LocationRefTypePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Location Ref Type  -&gt;  LCRT. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1LocationRefType As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Magic Effect. Distinto de que el campo valga cero.</summary>
        Property Parameter1MagicEffectPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Magic Effect  -&gt;  MGEF. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1MagicEffect As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Owner. Distinto de que el campo valga cero.</summary>
        Property Parameter1OwnerPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Owner  -&gt;  FACT / NPC_. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Owner As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Package. Distinto de que el campo valga cero.</summary>
        Property Parameter1PackagePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Package  -&gt;  PACK. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Package As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Perk. Distinto de que el campo valga cero.</summary>
        Property Parameter1PerkPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Perk  -&gt;  PERK. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Perk As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Quest. Distinto de que el campo valga cero.</summary>
        Property Parameter1QuestPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Quest  -&gt;  QUST. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Quest As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Race. Distinto de que el campo valga cero.</summary>
        Property Parameter1RacePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Race  -&gt;  RACE. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Race As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Reference. Distinto de que el campo valga cero.</summary>
        Property Parameter1ReferencePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Reference  -&gt;  ACHR / PARW / PBAR / PBEA / PCON / PFLA / PGRE / PHZD / PLYR / PMIS / REFR / TRGT. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Reference As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Region. Distinto de que el campo valga cero.</summary>
        Property Parameter1RegionPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Region  -&gt;  REGN. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Region As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Scene. Distinto de que el campo valga cero.</summary>
        Property Parameter1ScenePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Scene  -&gt;  SCEN. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Scene As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Shout. Distinto de que el campo valga cero.</summary>
        Property Parameter1ShoutPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Shout  -&gt;  SHOU. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Shout As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Voice Type. Distinto de que el campo valga cero.</summary>
        Property Parameter1VoiceTypePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Voice Type  -&gt;  FLST / VTYP. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1VoiceType As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Weather. Distinto de que el campo valga cero.</summary>
        Property Parameter1WeatherPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Weather  -&gt;  WTHR. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Weather As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Worldspace. Distinto de que el campo valga cero.</summary>
        Property Parameter1WorldspacePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Worldspace  -&gt;  FLST / WRLD. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Worldspace As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Unknown. Distinto de que el campo valga cero.</summary>
        Property Parameter2UnknownPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Unknown</summary>
        Property Parameter2Unknown As Byte()
        ''' <summary>El record trae Condition\CTDA\Parameter #2\None. Distinto de que el campo valga cero.</summary>
        Property Parameter2NonePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\None</summary>
        Property Parameter2None As Byte()
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Float. Distinto de que el campo valga cero.</summary>
        Property Parameter2FloatPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Float</summary>
        Property Parameter2Float As Single
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Integer. Distinto de que el campo valga cero.</summary>
        Property Parameter2IntegerPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Integer</summary>
        Property Parameter2Integer As Integer
        ''' <summary>El record trae Condition\CTDA\Parameter #2\String. Distinto de que el campo valga cero.</summary>
        Property Parameter2StringPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\String</summary>
        Property Parameter2String As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Alias. Distinto de que el campo valga cero.</summary>
        Property Parameter2AliasPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Alias</summary>
        Property Parameter2Alias As Integer
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Event. Distinto de que el campo valga cero.</summary>
        Property Parameter2EventPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Event</summary>
        Property Parameter2Event As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Packdata ID. Distinto de que el campo valga cero.</summary>
        Property Parameter2PackdataIDPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Packdata ID</summary>
        Property Parameter2PackdataID As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Quest Stage. Distinto de que el campo valga cero.</summary>
        Property Parameter2QuestStagePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Quest Stage</summary>
        Property Parameter2QuestStage As Integer
        ''' <summary>El record trae Condition\CTDA\Parameter #2\VATS Value Param\Weapon. Distinto de que el campo valga cero.</summary>
        Property VATSValueParamWeapon2Presente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\VATS Value Param\Weapon  -&gt;  WEAP. Referencia en el espacio del orden de carga.</summary>
        Property VATSValueParamWeapon2 As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\VATS Value Param\Weapon List. Distinto de que el campo valga cero.</summary>
        Property VATSValueParamWeaponList2Presente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\VATS Value Param\Weapon List  -&gt;  FLST. Referencia en el espacio del orden de carga.</summary>
        Property VATSValueParamWeaponList2 As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\VATS Value Param\Target. Distinto de que el campo valga cero.</summary>
        Property VATSValueParamTarget2Presente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\VATS Value Param\Target  -&gt;  NPC_. Referencia en el espacio del orden de carga.</summary>
        Property VATSValueParamTarget2 As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\VATS Value Param\Target List. Distinto de que el campo valga cero.</summary>
        Property VATSValueParamTargetList2Presente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\VATS Value Param\Target List  -&gt;  FLST. Referencia en el espacio del orden de carga.</summary>
        Property VATSValueParamTargetList2 As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\VATS Value Param\Unknown. Distinto de que el campo valga cero.</summary>
        Property VATSValueParamUnknown2Presente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\VATS Value Param\Unknown</summary>
        Property VATSValueParamUnknown2 As Byte()
        ''' <summary>El record trae Condition\CTDA\Parameter #2\VATS Value Param\Target Part. Distinto de que el campo valga cero.</summary>
        Property VATSValueParamTargetPart2Presente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\VATS Value Param\Target Part</summary>
        Property VATSValueParamTargetPart2 As Integer
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #2\VATS Value Param\Target Part.</summary>
        ReadOnly Property VATSValueParamTargetPart2Nombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #2\VATS Value Param\VATS Action. Distinto de que el campo valga cero.</summary>
        Property VATSValueParamVATSAction2Presente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\VATS Value Param\VATS Action</summary>
        Property VATSValueParamVATSAction2 As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #2\VATS Value Param\VATS Action.</summary>
        ReadOnly Property VATSValueParamVATSAction2Nombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #2\VATS Value Param\Critical Effect. Distinto de que el campo valga cero.</summary>
        Property VATSValueParamCriticalEffect2Presente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\VATS Value Param\Critical Effect  -&gt;  SPEL. Referencia en el espacio del orden de carga.</summary>
        Property VATSValueParamCriticalEffect2 As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\VATS Value Param\Critical Effect List. Distinto de que el campo valga cero.</summary>
        Property VATSValueParamCriticalEffectList2Presente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\VATS Value Param\Critical Effect List  -&gt;  FLST. Referencia en el espacio del orden de carga.</summary>
        Property VATSValueParamCriticalEffectList2 As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\VATS Value Param\Weapon Type. Distinto de que el campo valga cero.</summary>
        Property VATSValueParamWeaponType2Presente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\VATS Value Param\Weapon Type</summary>
        Property VATSValueParamWeaponType2 As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #2\VATS Value Param\Weapon Type.</summary>
        ReadOnly Property VATSValueParamWeaponType2Nombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #2\VATS Value Param\Projectile Type. Distinto de que el campo valga cero.</summary>
        Property VATSValueParamProjectileType2Presente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\VATS Value Param\Projectile Type</summary>
        Property VATSValueParamProjectileType2 As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #2\VATS Value Param\Projectile Type.</summary>
        ReadOnly Property VATSValueParamProjectileType2Nombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #2\VATS Value Param\Delivery. Distinto de que el campo valga cero.</summary>
        Property VATSValueParamDelivery2Presente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\VATS Value Param\Delivery</summary>
        Property VATSValueParamDelivery2 As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #2\VATS Value Param\Delivery.</summary>
        ReadOnly Property VATSValueParamDelivery2Nombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #2\VATS Value Param\Casting Type. Distinto de que el campo valga cero.</summary>
        Property VATSValueParamCastingType2Presente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\VATS Value Param\Casting Type</summary>
        Property VATSValueParamCastingType2 As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #2\VATS Value Param\Casting Type.</summary>
        ReadOnly Property VATSValueParamCastingType2Nombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Actor Value. Distinto de que el campo valga cero.</summary>
        Property Parameter2ActorValuePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Actor Value</summary>
        Property Parameter2ActorValue As Integer
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #2\Actor Value.</summary>
        ReadOnly Property Parameter2ActorValueNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Alignment. Distinto de que el campo valga cero.</summary>
        Property Parameter2AlignmentPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Alignment</summary>
        Property Parameter2Alignment As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #2\Alignment.</summary>
        ReadOnly Property Parameter2AlignmentNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Axis. Distinto de que el campo valga cero.</summary>
        Property Parameter2AxisPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Axis</summary>
        Property Parameter2Axis As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #2\Axis.</summary>
        ReadOnly Property Parameter2AxisNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Casting Source. Distinto de que el campo valga cero.</summary>
        Property Parameter2CastingSourcePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Casting Source</summary>
        Property Parameter2CastingSource As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #2\Casting Source.</summary>
        ReadOnly Property Parameter2CastingSourceNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Crime Type. Distinto de que el campo valga cero.</summary>
        Property Parameter2CrimeTypePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Crime Type</summary>
        Property Parameter2CrimeType As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #2\Crime Type.</summary>
        ReadOnly Property Parameter2CrimeTypeNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Critical Stage. Distinto de que el campo valga cero.</summary>
        Property Parameter2CriticalStagePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Critical Stage</summary>
        Property Parameter2CriticalStage As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #2\Critical Stage.</summary>
        ReadOnly Property Parameter2CriticalStageNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Form Type. Distinto de que el campo valga cero.</summary>
        Property Parameter2FormTypePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Form Type</summary>
        Property Parameter2FormType As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #2\Form Type.</summary>
        ReadOnly Property Parameter2FormTypeNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Furniture Anim. Distinto de que el campo valga cero.</summary>
        Property Parameter2FurnitureAnimPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Furniture Anim</summary>
        Property Parameter2FurnitureAnim As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #2\Furniture Anim.</summary>
        ReadOnly Property Parameter2FurnitureAnimNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Furniture Entry. Distinto de que el campo valga cero.</summary>
        Property Parameter2FurnitureEntryPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Furniture Entry</summary>
        Property Parameter2FurnitureEntry As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #2\Furniture Entry.</summary>
        ReadOnly Property Parameter2FurnitureEntryNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Misc Stat. Distinto de que el campo valga cero.</summary>
        Property Parameter2MiscStatPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Misc Stat</summary>
        Property Parameter2MiscStat As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #2\Misc Stat.</summary>
        ReadOnly Property Parameter2MiscStatNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Player Action. Distinto de que el campo valga cero.</summary>
        Property Parameter2PlayerActionPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Player Action</summary>
        Property Parameter2PlayerAction As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #2\Player Action.</summary>
        ReadOnly Property Parameter2PlayerActionNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Sex. Distinto de que el campo valga cero.</summary>
        Property Parameter2SexPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Sex</summary>
        Property Parameter2Sex As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #2\Sex.</summary>
        ReadOnly Property Parameter2SexNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #2\VATS Value Function. Distinto de que el campo valga cero.</summary>
        Property Parameter2VATSValueFunctionPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\VATS Value Function</summary>
        Property Parameter2VATSValueFunction As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Ward State. Distinto de que el campo valga cero.</summary>
        Property Parameter2WardStatePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Ward State</summary>
        Property Parameter2WardState As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #2\Ward State.</summary>
        ReadOnly Property Parameter2WardStateNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Actor. Distinto de que el campo valga cero.</summary>
        Property Parameter2ActorPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Actor  -&gt;  ACHR / PLYR / REFR / TRGT. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Actor As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Actor Base. Distinto de que el campo valga cero.</summary>
        Property Parameter2ActorBasePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Actor Base  -&gt;  NPC_. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2ActorBase As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Association Type. Distinto de que el campo valga cero.</summary>
        Property Parameter2AssociationTypePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Association Type  -&gt;  ASTP. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2AssociationType As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Base Object. Distinto de que el campo valga cero.</summary>
        Property Parameter2BaseObjectPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Base Object. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2BaseObject As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Cell. Distinto de que el campo valga cero.</summary>
        Property Parameter2CellPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Cell  -&gt;  CELL. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Cell As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Class. Distinto de que el campo valga cero.</summary>
        Property Parameter2ClassPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Class  -&gt;  CLAS. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Class As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Effect Item. Distinto de que el campo valga cero.</summary>
        Property Parameter2EffectItemPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Effect Item  -&gt;  ALCH / ENCH / INGR / SCRL / SPEL. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2EffectItem As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Encounter Zone. Distinto de que el campo valga cero.</summary>
        Property Parameter2EncounterZonePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Encounter Zone  -&gt;  ECZN. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2EncounterZone As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Equip Type. Distinto de que el campo valga cero.</summary>
        Property Parameter2EquipTypePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Equip Type  -&gt;  EQUP. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2EquipType As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Event Data. Distinto de que el campo valga cero.</summary>
        Property Parameter2EventDataPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Event Data. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2EventData As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Faction. Distinto de que el campo valga cero.</summary>
        Property Parameter2FactionPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Faction  -&gt;  FACT. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Faction As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Form List. Distinto de que el campo valga cero.</summary>
        Property Parameter2FormListPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Form List  -&gt;  FLST. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2FormList As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Furniture. Distinto de que el campo valga cero.</summary>
        Property Parameter2FurniturePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Furniture  -&gt;  FLST / FURN. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Furniture As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Global. Distinto de que el campo valga cero.</summary>
        Property Parameter2GlobalPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Global  -&gt;  GLOB. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Global As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Idle. Distinto de que el campo valga cero.</summary>
        Property Parameter2IdlePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Idle  -&gt;  IDLE. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Idle As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Inventory Object. Distinto de que el campo valga cero.</summary>
        Property Parameter2InventoryObjectPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Inventory Object  -&gt;  ALCH / AMMO / ARMO / BOOK / COBJ / FLST / INGR / KEYM / LIGH / LVLI / MISC / SCRL / SLGM / WEAP. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2InventoryObject As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Keyword. Distinto de que el campo valga cero.</summary>
        Property Parameter2KeywordPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Keyword  -&gt;  KYWD / NULL. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Keyword As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Knowable. Distinto de que el campo valga cero.</summary>
        Property Parameter2KnowablePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Knowable  -&gt;  ENCH / MGEF / WOOP. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Knowable As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Location. Distinto de que el campo valga cero.</summary>
        Property Parameter2LocationPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Location  -&gt;  LCTN. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Location As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Location Ref Type. Distinto de que el campo valga cero.</summary>
        Property Parameter2LocationRefTypePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Location Ref Type  -&gt;  LCRT. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2LocationRefType As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Magic Effect. Distinto de que el campo valga cero.</summary>
        Property Parameter2MagicEffectPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Magic Effect  -&gt;  MGEF. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2MagicEffect As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Owner. Distinto de que el campo valga cero.</summary>
        Property Parameter2OwnerPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Owner  -&gt;  FACT / NPC_. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Owner As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Package. Distinto de que el campo valga cero.</summary>
        Property Parameter2PackagePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Package  -&gt;  PACK. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Package As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Perk. Distinto de que el campo valga cero.</summary>
        Property Parameter2PerkPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Perk  -&gt;  PERK. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Perk As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Quest. Distinto de que el campo valga cero.</summary>
        Property Parameter2QuestPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Quest  -&gt;  QUST. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Quest As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Race. Distinto de que el campo valga cero.</summary>
        Property Parameter2RacePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Race  -&gt;  RACE. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Race As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Reference. Distinto de que el campo valga cero.</summary>
        Property Parameter2ReferencePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Reference  -&gt;  ACHR / PARW / PBAR / PBEA / PCON / PFLA / PGRE / PHZD / PLYR / PMIS / REFR / TRGT. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Reference As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Region. Distinto de que el campo valga cero.</summary>
        Property Parameter2RegionPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Region  -&gt;  REGN. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Region As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Scene. Distinto de que el campo valga cero.</summary>
        Property Parameter2ScenePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Scene  -&gt;  SCEN. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Scene As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Shout. Distinto de que el campo valga cero.</summary>
        Property Parameter2ShoutPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Shout  -&gt;  SHOU. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Shout As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Voice Type. Distinto de que el campo valga cero.</summary>
        Property Parameter2VoiceTypePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Voice Type  -&gt;  FLST / VTYP. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2VoiceType As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Weather. Distinto de que el campo valga cero.</summary>
        Property Parameter2WeatherPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Weather  -&gt;  WTHR. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Weather As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Worldspace. Distinto de que el campo valga cero.</summary>
        Property Parameter2WorldspacePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Worldspace  -&gt;  FLST / WRLD. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Worldspace As UInteger
        ''' <summary>El record trae Condition\CTDA\Run On. Distinto de que el campo valga cero.</summary>
        Property ConditionRunOnPresente As Boolean
        ''' <summary>Condition\CTDA\Run On</summary>
        Property ConditionRunOn As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Run On.</summary>
        ReadOnly Property ConditionRunOnNombre As String
        ''' <summary>El record trae Condition\CTDA\Reference\Reference. Distinto de que el campo valga cero.</summary>
        Property ConditionReferencePresente As Boolean
        ''' <summary>Condition\CTDA\Reference\Reference  -&gt;  NULL / PLYR / ACHR / REFR / PGRE / PHZD / PMIS / PARW / PBAR / PBEA / PCON / PFLA. Referencia en el espacio del orden de carga.</summary>
        Property ConditionReference As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #3\Parameter #3. Distinto de que el campo valga cero.</summary>
        Property ConditionParameter3Presente As Boolean
        ''' <summary>Condition\CTDA\Parameter #3\Parameter #3</summary>
        Property ConditionParameter3 As Integer
        ''' <summary>El record trae Condition\CTDA\Parameter #3\Quest Alias. Distinto de que el campo valga cero.</summary>
        Property Parameter3QuestAliasPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #3\Quest Alias</summary>
        Property Parameter3QuestAlias As Integer
        ''' <summary>El record trae Condition\CTDA\Parameter #3\Event Data. Distinto de que el campo valga cero.</summary>
        Property Parameter3EventDataPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #3\Event Data</summary>
        Property Parameter3EventData As Integer
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #3\Event Data.</summary>
        ReadOnly Property Parameter3EventDataNombre As String
        ''' <summary>El record trae Condition\CIS1\Parameter #1. Distinto de que el campo valga cero.</summary>
        Property ConditionParameter1Presente As Boolean
        ''' <summary>Condition\CIS1\Parameter #1</summary>
        Property ConditionParameter1 As String
        ''' <summary>El record trae Condition\CIS2\Parameter #2. Distinto de que el campo valga cero.</summary>
        Property ConditionParameter2Presente As Boolean
        ''' <summary>Condition\CIS2\Parameter #2</summary>
        Property ConditionParameter2 As String
    End Interface

    ''' <summary>Un bloque Conditions2, venga del record que venga.
    ''' <para>Lo declaran igual: ClfmFO4_Conditions, HdptFO4_Conditions, IdleFO4_Conditions, QustFO4_Conditions, QustFO4_Conditions2, QustFO4_Conditions3, QustFO4_Conditions4, QustFO4_Conditions5, RaceFO4_Conditions, RaceFO4_Conditions2.</para></summary>
    Public Interface IBloque_Conditions2
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Condition\CTDA\Type. Distinto de que el campo valga cero.</summary>
        Property ConditionTypePresente As Boolean
        ''' <summary>Condition\CTDA\Type</summary>
        Property ConditionType As Byte
        ''' <summary>El record trae Condition\CTDA\Comparison Value\Comparison Value - Float. Distinto de que el campo valga cero.</summary>
        Property ConditionComparisonValueFloatPresente As Boolean
        ''' <summary>Condition\CTDA\Comparison Value\Comparison Value - Float</summary>
        Property ConditionComparisonValueFloat As Single
        ''' <summary>El record trae Condition\CTDA\Comparison Value\Comparison Value - Global. Distinto de que el campo valga cero.</summary>
        Property ConditionComparisonValueGlobalPresente As Boolean
        ''' <summary>Condition\CTDA\Comparison Value\Comparison Value - Global  -&gt;  GLOB. Referencia en el espacio del orden de carga.</summary>
        Property ConditionComparisonValueGlobal As UInteger
        ''' <summary>El record trae Condition\CTDA\Function. Distinto de que el campo valga cero.</summary>
        Property ConditionFunctionPresente As Boolean
        ''' <summary>Condition\CTDA\Function</summary>
        Property ConditionFunction As UShort
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Unknown. Distinto de que el campo valga cero.</summary>
        Property Parameter1UnknownPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Unknown</summary>
        Property Parameter1Unknown As Byte()
        ''' <summary>El record trae Condition\CTDA\Parameter #1\None. Distinto de que el campo valga cero.</summary>
        Property Parameter1NonePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\None</summary>
        Property Parameter1None As Byte()
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Float. Distinto de que el campo valga cero.</summary>
        Property Parameter1FloatPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Float</summary>
        Property Parameter1Float As Single
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Integer. Distinto de que el campo valga cero.</summary>
        Property Parameter1IntegerPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Integer</summary>
        Property Parameter1Integer As Integer
        ''' <summary>El record trae Condition\CTDA\Parameter #1\String. Distinto de que el campo valga cero.</summary>
        Property Parameter1StringPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\String</summary>
        Property Parameter1String As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Alias. Distinto de que el campo valga cero.</summary>
        Property Parameter1AliasPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Alias</summary>
        Property Parameter1Alias As Integer
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Event. Distinto de que el campo valga cero.</summary>
        Property Parameter1EventPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Event</summary>
        Property Parameter1Event As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Packdata ID. Distinto de que el campo valga cero.</summary>
        Property Parameter1PackdataIDPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Packdata ID</summary>
        Property Parameter1PackdataID As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Quest Stage. Distinto de que el campo valga cero.</summary>
        Property Parameter1QuestStagePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Quest Stage</summary>
        Property Parameter1QuestStage As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Alignment. Distinto de que el campo valga cero.</summary>
        Property Parameter1AlignmentPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Alignment</summary>
        Property Parameter1Alignment As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #1\Alignment.</summary>
        ReadOnly Property Parameter1AlignmentNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Axis. Distinto de que el campo valga cero.</summary>
        Property Parameter1AxisPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Axis</summary>
        Property Parameter1Axis As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #1\Axis.</summary>
        ReadOnly Property Parameter1AxisNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Casting Type. Distinto de que el campo valga cero.</summary>
        Property Parameter1CastingTypePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Casting Type</summary>
        Property Parameter1CastingType As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #1\Casting Type.</summary>
        ReadOnly Property Parameter1CastingTypeNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Crime Type. Distinto de que el campo valga cero.</summary>
        Property Parameter1CrimeTypePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Crime Type</summary>
        Property Parameter1CrimeType As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #1\Crime Type.</summary>
        ReadOnly Property Parameter1CrimeTypeNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Critical Stage. Distinto de que el campo valga cero.</summary>
        Property Parameter1CriticalStagePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Critical Stage</summary>
        Property Parameter1CriticalStage As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #1\Critical Stage.</summary>
        ReadOnly Property Parameter1CriticalStageNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Form Type. Distinto de que el campo valga cero.</summary>
        Property Parameter1FormTypePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Form Type</summary>
        Property Parameter1FormType As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #1\Form Type.</summary>
        ReadOnly Property Parameter1FormTypeNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Misc Stat. Distinto de que el campo valga cero.</summary>
        Property Parameter1MiscStatPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Misc Stat</summary>
        Property Parameter1MiscStat As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #1\Misc Stat.</summary>
        ReadOnly Property Parameter1MiscStatNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Sex. Distinto de que el campo valga cero.</summary>
        Property Parameter1SexPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Sex</summary>
        Property Parameter1Sex As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #1\Sex.</summary>
        ReadOnly Property Parameter1SexNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Ward State. Distinto de que el campo valga cero.</summary>
        Property Parameter1WardStatePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Ward State</summary>
        Property Parameter1WardState As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #1\Ward State.</summary>
        ReadOnly Property Parameter1WardStateNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Actor. Distinto de que el campo valga cero.</summary>
        Property Parameter1ActorPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Actor  -&gt;  ACHR / PLYR / REFR / TRGT. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Actor As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Actor Base. Distinto de que el campo valga cero.</summary>
        Property Parameter1ActorBasePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Actor Base  -&gt;  NPC_. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1ActorBase As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Actor Value. Distinto de que el campo valga cero.</summary>
        Property Parameter1ActorValuePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Actor Value  -&gt;  AVIF. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1ActorValue As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Association Type. Distinto de que el campo valga cero.</summary>
        Property Parameter1AssociationTypePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Association Type  -&gt;  ASTP. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1AssociationType As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Base Effect. Distinto de que el campo valga cero.</summary>
        Property Parameter1BaseEffectPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Base Effect  -&gt;  MGEF. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1BaseEffect As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Base Object. Distinto de que el campo valga cero.</summary>
        Property Parameter1BaseObjectPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Base Object. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1BaseObject As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Cell. Distinto de que el campo valga cero.</summary>
        Property Parameter1CellPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Cell  -&gt;  CELL. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Cell As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Class. Distinto de que el campo valga cero.</summary>
        Property Parameter1ClassPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Class  -&gt;  CLAS. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Class As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Damage Type. Distinto de que el campo valga cero.</summary>
        Property Parameter1DamageTypePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Damage Type  -&gt;  DMGT / FLST. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1DamageType As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Effect Item. Distinto de que el campo valga cero.</summary>
        Property Parameter1EffectItemPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Effect Item  -&gt;  ALCH / ENCH / INGR / SCRL / SPEL. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1EffectItem As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Encounter Zone. Distinto de que el campo valga cero.</summary>
        Property Parameter1EncounterZonePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Encounter Zone  -&gt;  ECZN. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1EncounterZone As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Equip Type. Distinto de que el campo valga cero.</summary>
        Property Parameter1EquipTypePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Equip Type  -&gt;  EQUP. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1EquipType As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Event Data. Distinto de que el campo valga cero.</summary>
        Property Parameter1EventDataPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Event Data  -&gt;  FLST / KYWD / LCTN. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1EventData As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Faction. Distinto de que el campo valga cero.</summary>
        Property Parameter1FactionPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Faction  -&gt;  FACT. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Faction As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Form List. Distinto de que el campo valga cero.</summary>
        Property Parameter1FormListPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Form List  -&gt;  FLST. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1FormList As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Furniture. Distinto de que el campo valga cero.</summary>
        Property Parameter1FurniturePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Furniture  -&gt;  FLST / FURN. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Furniture As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Global. Distinto de que el campo valga cero.</summary>
        Property Parameter1GlobalPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Global  -&gt;  GLOB. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Global As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Idle. Distinto de que el campo valga cero.</summary>
        Property Parameter1IdlePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Idle  -&gt;  IDLE. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Idle As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Keyword. Distinto de que el campo valga cero.</summary>
        Property Parameter1KeywordPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Keyword  -&gt;  FLST / KYWD / NULL. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Keyword As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Location. Distinto de que el campo valga cero.</summary>
        Property Parameter1LocationPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Location  -&gt;  LCTN. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Location As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Location Ref Type. Distinto de que el campo valga cero.</summary>
        Property Parameter1LocationRefTypePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Location Ref Type  -&gt;  LCRT. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1LocationRefType As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Owner. Distinto de que el campo valga cero.</summary>
        Property Parameter1OwnerPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Owner  -&gt;  FACT / NPC_. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Owner As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Package. Distinto de que el campo valga cero.</summary>
        Property Parameter1PackagePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Package  -&gt;  PACK. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Package As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Perk. Distinto de que el campo valga cero.</summary>
        Property Parameter1PerkPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Perk  -&gt;  PERK. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Perk As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Quest. Distinto de que el campo valga cero.</summary>
        Property Parameter1QuestPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Quest  -&gt;  QUST. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Quest As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Race. Distinto de que el campo valga cero.</summary>
        Property Parameter1RacePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Race  -&gt;  RACE. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Race As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Reference. Distinto de que el campo valga cero.</summary>
        Property Parameter1ReferencePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Reference  -&gt;  ACHR / PARW / PBAR / PBEA / PCON / PFLA / PGRE / PHZD / PLYR / PMIS / REFR / TRGT. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Reference As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Region. Distinto de que el campo valga cero.</summary>
        Property Parameter1RegionPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Region  -&gt;  REGN. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Region As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Scene. Distinto de que el campo valga cero.</summary>
        Property Parameter1ScenePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Scene  -&gt;  SCEN. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Scene As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Voice Type. Distinto de que el campo valga cero.</summary>
        Property Parameter1VoiceTypePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Voice Type  -&gt;  VTYP / FLST. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1VoiceType As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Weather. Distinto de que el campo valga cero.</summary>
        Property Parameter1WeatherPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Weather  -&gt;  WTHR. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Weather As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #1\Worldspace. Distinto de que el campo valga cero.</summary>
        Property Parameter1WorldspacePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #1\Worldspace  -&gt;  WRLD / FLST. Referencia en el espacio del orden de carga.</summary>
        Property Parameter1Worldspace As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Unknown. Distinto de que el campo valga cero.</summary>
        Property Parameter2UnknownPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Unknown</summary>
        Property Parameter2Unknown As Byte()
        ''' <summary>El record trae Condition\CTDA\Parameter #2\None. Distinto de que el campo valga cero.</summary>
        Property Parameter2NonePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\None</summary>
        Property Parameter2None As Byte()
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Float. Distinto de que el campo valga cero.</summary>
        Property Parameter2FloatPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Float</summary>
        Property Parameter2Float As Single
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Integer. Distinto de que el campo valga cero.</summary>
        Property Parameter2IntegerPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Integer</summary>
        Property Parameter2Integer As Integer
        ''' <summary>El record trae Condition\CTDA\Parameter #2\String. Distinto de que el campo valga cero.</summary>
        Property Parameter2StringPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\String</summary>
        Property Parameter2String As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Alias. Distinto de que el campo valga cero.</summary>
        Property Parameter2AliasPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Alias</summary>
        Property Parameter2Alias As Integer
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Event. Distinto de que el campo valga cero.</summary>
        Property Parameter2EventPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Event</summary>
        Property Parameter2Event As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Packdata ID. Distinto de que el campo valga cero.</summary>
        Property Parameter2PackdataIDPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Packdata ID</summary>
        Property Parameter2PackdataID As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Quest Stage. Distinto de que el campo valga cero.</summary>
        Property Parameter2QuestStagePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Quest Stage</summary>
        Property Parameter2QuestStage As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Alignment. Distinto de que el campo valga cero.</summary>
        Property Parameter2AlignmentPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Alignment</summary>
        Property Parameter2Alignment As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #2\Alignment.</summary>
        ReadOnly Property Parameter2AlignmentNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Axis. Distinto de que el campo valga cero.</summary>
        Property Parameter2AxisPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Axis</summary>
        Property Parameter2Axis As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #2\Axis.</summary>
        ReadOnly Property Parameter2AxisNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Casting Type. Distinto de que el campo valga cero.</summary>
        Property Parameter2CastingTypePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Casting Type</summary>
        Property Parameter2CastingType As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #2\Casting Type.</summary>
        ReadOnly Property Parameter2CastingTypeNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Crime Type. Distinto de que el campo valga cero.</summary>
        Property Parameter2CrimeTypePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Crime Type</summary>
        Property Parameter2CrimeType As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #2\Crime Type.</summary>
        ReadOnly Property Parameter2CrimeTypeNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Critical Stage. Distinto de que el campo valga cero.</summary>
        Property Parameter2CriticalStagePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Critical Stage</summary>
        Property Parameter2CriticalStage As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #2\Critical Stage.</summary>
        ReadOnly Property Parameter2CriticalStageNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Form Type. Distinto de que el campo valga cero.</summary>
        Property Parameter2FormTypePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Form Type</summary>
        Property Parameter2FormType As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #2\Form Type.</summary>
        ReadOnly Property Parameter2FormTypeNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Misc Stat. Distinto de que el campo valga cero.</summary>
        Property Parameter2MiscStatPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Misc Stat</summary>
        Property Parameter2MiscStat As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #2\Misc Stat.</summary>
        ReadOnly Property Parameter2MiscStatNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Sex. Distinto de que el campo valga cero.</summary>
        Property Parameter2SexPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Sex</summary>
        Property Parameter2Sex As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #2\Sex.</summary>
        ReadOnly Property Parameter2SexNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Ward State. Distinto de que el campo valga cero.</summary>
        Property Parameter2WardStatePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Ward State</summary>
        Property Parameter2WardState As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #2\Ward State.</summary>
        ReadOnly Property Parameter2WardStateNombre As String
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Actor. Distinto de que el campo valga cero.</summary>
        Property Parameter2ActorPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Actor  -&gt;  ACHR / PLYR / REFR / TRGT. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Actor As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Actor Base. Distinto de que el campo valga cero.</summary>
        Property Parameter2ActorBasePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Actor Base  -&gt;  NPC_. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2ActorBase As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Actor Value. Distinto de que el campo valga cero.</summary>
        Property Parameter2ActorValuePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Actor Value  -&gt;  AVIF. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2ActorValue As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Association Type. Distinto de que el campo valga cero.</summary>
        Property Parameter2AssociationTypePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Association Type  -&gt;  ASTP. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2AssociationType As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Base Effect. Distinto de que el campo valga cero.</summary>
        Property Parameter2BaseEffectPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Base Effect  -&gt;  MGEF. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2BaseEffect As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Base Object. Distinto de que el campo valga cero.</summary>
        Property Parameter2BaseObjectPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Base Object. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2BaseObject As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Cell. Distinto de que el campo valga cero.</summary>
        Property Parameter2CellPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Cell  -&gt;  CELL. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Cell As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Class. Distinto de que el campo valga cero.</summary>
        Property Parameter2ClassPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Class  -&gt;  CLAS. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Class As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Damage Type. Distinto de que el campo valga cero.</summary>
        Property Parameter2DamageTypePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Damage Type  -&gt;  DMGT / FLST. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2DamageType As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Effect Item. Distinto de que el campo valga cero.</summary>
        Property Parameter2EffectItemPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Effect Item  -&gt;  ALCH / ENCH / INGR / SCRL / SPEL. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2EffectItem As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Encounter Zone. Distinto de que el campo valga cero.</summary>
        Property Parameter2EncounterZonePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Encounter Zone  -&gt;  ECZN. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2EncounterZone As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Equip Type. Distinto de que el campo valga cero.</summary>
        Property Parameter2EquipTypePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Equip Type  -&gt;  EQUP. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2EquipType As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Event Data. Distinto de que el campo valga cero.</summary>
        Property Parameter2EventDataPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Event Data  -&gt;  FLST / KYWD / LCTN. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2EventData As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Faction. Distinto de que el campo valga cero.</summary>
        Property Parameter2FactionPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Faction  -&gt;  FACT. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Faction As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Form List. Distinto de que el campo valga cero.</summary>
        Property Parameter2FormListPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Form List  -&gt;  FLST. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2FormList As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Furniture. Distinto de que el campo valga cero.</summary>
        Property Parameter2FurniturePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Furniture  -&gt;  FLST / FURN. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Furniture As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Global. Distinto de que el campo valga cero.</summary>
        Property Parameter2GlobalPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Global  -&gt;  GLOB. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Global As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Idle. Distinto de que el campo valga cero.</summary>
        Property Parameter2IdlePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Idle  -&gt;  IDLE. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Idle As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Keyword. Distinto de que el campo valga cero.</summary>
        Property Parameter2KeywordPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Keyword  -&gt;  FLST / KYWD / NULL. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Keyword As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Location. Distinto de que el campo valga cero.</summary>
        Property Parameter2LocationPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Location  -&gt;  LCTN. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Location As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Location Ref Type. Distinto de que el campo valga cero.</summary>
        Property Parameter2LocationRefTypePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Location Ref Type  -&gt;  LCRT. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2LocationRefType As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Owner. Distinto de que el campo valga cero.</summary>
        Property Parameter2OwnerPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Owner  -&gt;  FACT / NPC_. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Owner As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Package. Distinto de que el campo valga cero.</summary>
        Property Parameter2PackagePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Package  -&gt;  PACK. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Package As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Perk. Distinto de que el campo valga cero.</summary>
        Property Parameter2PerkPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Perk  -&gt;  PERK. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Perk As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Quest. Distinto de que el campo valga cero.</summary>
        Property Parameter2QuestPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Quest  -&gt;  QUST. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Quest As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Race. Distinto de que el campo valga cero.</summary>
        Property Parameter2RacePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Race  -&gt;  RACE. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Race As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Reference. Distinto de que el campo valga cero.</summary>
        Property Parameter2ReferencePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Reference  -&gt;  ACHR / PARW / PBAR / PBEA / PCON / PFLA / PGRE / PHZD / PLYR / PMIS / REFR / TRGT. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Reference As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Region. Distinto de que el campo valga cero.</summary>
        Property Parameter2RegionPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Region  -&gt;  REGN. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Region As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Scene. Distinto de que el campo valga cero.</summary>
        Property Parameter2ScenePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Scene  -&gt;  SCEN. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Scene As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Voice Type. Distinto de que el campo valga cero.</summary>
        Property Parameter2VoiceTypePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Voice Type  -&gt;  VTYP / FLST. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2VoiceType As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Weather. Distinto de que el campo valga cero.</summary>
        Property Parameter2WeatherPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Weather  -&gt;  WTHR. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Weather As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #2\Worldspace. Distinto de que el campo valga cero.</summary>
        Property Parameter2WorldspacePresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #2\Worldspace  -&gt;  WRLD / FLST. Referencia en el espacio del orden de carga.</summary>
        Property Parameter2Worldspace As UInteger
        ''' <summary>El record trae Condition\CTDA\Run On. Distinto de que el campo valga cero.</summary>
        Property ConditionRunOnPresente As Boolean
        ''' <summary>Condition\CTDA\Run On</summary>
        Property ConditionRunOn As UInteger
        ''' <summary>Nombre del valor de Condition\CTDA\Run On.</summary>
        ReadOnly Property ConditionRunOnNombre As String
        ''' <summary>El record trae Condition\CTDA\Reference\Reference. Distinto de que el campo valga cero.</summary>
        Property ConditionReferencePresente As Boolean
        ''' <summary>Condition\CTDA\Reference\Reference. Referencia en el espacio del orden de carga.</summary>
        Property ConditionReference As UInteger
        ''' <summary>El record trae Condition\CTDA\Parameter #3\Parameter #3. Distinto de que el campo valga cero.</summary>
        Property ConditionParameter3Presente As Boolean
        ''' <summary>Condition\CTDA\Parameter #3\Parameter #3</summary>
        Property ConditionParameter3 As Integer
        ''' <summary>El record trae Condition\CTDA\Parameter #3\Quest Alias. Distinto de que el campo valga cero.</summary>
        Property Parameter3QuestAliasPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #3\Quest Alias</summary>
        Property Parameter3QuestAlias As Integer
        ''' <summary>El record trae Condition\CTDA\Parameter #3\Event Data. Distinto de que el campo valga cero.</summary>
        Property Parameter3EventDataPresente As Boolean
        ''' <summary>Condition\CTDA\Parameter #3\Event Data</summary>
        Property Parameter3EventData As Integer
        ''' <summary>Nombre del valor de Condition\CTDA\Parameter #3\Event Data.</summary>
        ReadOnly Property Parameter3EventDataNombre As String
        ''' <summary>El record trae Condition\CIS1\Parameter #1. Distinto de que el campo valga cero.</summary>
        Property ConditionParameter1Presente As Boolean
        ''' <summary>Condition\CIS1\Parameter #1</summary>
        Property ConditionParameter1 As String
        ''' <summary>El record trae Condition\CIS2\Parameter #2. Distinto de que el campo valga cero.</summary>
        Property ConditionParameter2Presente As Boolean
        ''' <summary>Condition\CIS2\Parameter #2</summary>
        Property ConditionParameter2 As String
    End Interface

    ''' <summary>Un bloque Counters, venga del record que venga.
    ''' <para>Lo declaran igual: ArmaFO4_Counters, ArmaFO4_Counters2, ArmaFO4_Counters3, ArmaFO4_Counters4, ArmaSSE_Counters, ArmaSSE_Counters2, ArmaSSE_Counters3, ArmaSSE_Counters4, ArmoFO4_Counters, ArmoFO4_Counters2, ArmoFO4_Counters3, ArmoSSE_Counters, ArmoSSE_Counters2, ArmoSSE_Counters3, BptdFO4_Counters, BptdFO4_Counters2, BptdSSE_Counters, BptdSSE_Counters2, HdptFO4_Counters, HdptSSE_Counters, LvlnFO4_Counters, LvlnSSE_Counters, NpcFO4_Counters, NpcSSE_Counters, OmodFO4_Counters, RaceFO4_Counters, RaceFO4_Counters2, RaceFO4_Counters3, RaceFO4_Counters4, RaceFO4_Counters5, RaceSSE_Counters, RaceSSE_Counters2, RaceSSE_Counters3, RaceSSE_Counters4, RaceSSE_Counters5, RaceSSE_Counters6, RaceSSE_Counters7.</para></summary>
    Public Interface IBloque_Counters
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Counter. Distinto de que el campo valga cero.</summary>
        Property CounterPresente As Boolean
        ''' <summary>Counter</summary>
        Property Counter As UInteger
    End Interface

    ''' <summary>Un bloque FaceDetails, venga del record que venga.
    ''' <para>Lo declaran igual: RaceFO4_FemaleFaceDetails, RaceFO4_MaleFaceDetails, RaceSSE_FaceDetailsTextureSetListFemale, RaceSSE_FaceDetailsTextureSetListMale.</para></summary>
    Public Interface IBloque_FaceDetails
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae FTSF\Texture Set. Distinto de que el campo valga cero.</summary>
        Property TextureSetPresente As Boolean
        ''' <summary>FTSF\Texture Set  -&gt;  TXST / NULL. Referencia en el espacio del orden de carga.</summary>
        Property TextureSet As UInteger
    End Interface

    ''' <summary>Un bloque FaceMorphs, venga del record que venga.
    ''' <para>Lo declaran igual: RaceFO4_FemaleFaceMorphs, RaceFO4_MaleFaceMorphs.</para></summary>
    Public Interface IBloque_FaceMorphs
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Face Morph\FMRI\Index. Distinto de que el campo valga cero.</summary>
        Property FaceMorphIndexPresente As Boolean
        ''' <summary>Face Morph\FMRI\Index</summary>
        Property FaceMorphIndex As UInteger
        ''' <summary>El record trae Face Morph\FMRN\Name. Distinto de que el campo valga cero.</summary>
        Property FaceMorphNamePresente As Boolean
        ''' <summary>Face Morph\FMRN\Name. Resuelto contra las tablas de texto si el archivo las usa.</summary>
        Property FaceMorphName As String
    End Interface

    ''' <summary>Un bloque FilterKeywordChances, venga del record que venga.
    ''' <para>Lo declaran igual: LvliFO4_FilterKeywordChances, LvlnFO4_FilterKeywordChances.</para></summary>
    Public Interface IBloque_FilterKeywordChances
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Filter\Keyword. Distinto de que el campo valga cero.</summary>
        Property FilterKeywordPresente As Boolean
        ''' <summary>Filter\Keyword  -&gt;  KYWD. Referencia en el espacio del orden de carga.</summary>
        Property FilterKeyword As UInteger
        ''' <summary>El record trae Filter\Chance. Distinto de que el campo valga cero.</summary>
        Property FilterChancePresente As Boolean
        ''' <summary>Filter\Chance</summary>
        Property FilterChance As UInteger
    End Interface

    ''' <summary>Un bloque HairColors, venga del record que venga.
    ''' <para>Lo declaran igual: RaceFO4_FemaleHairColors, RaceFO4_MaleHairColors, RaceSSE_AvailableHairColorsFemale, RaceSSE_AvailableHairColorsMale.</para></summary>
    Public Interface IBloque_HairColors
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae AHCF\Hair Color. Distinto de que el campo valga cero.</summary>
        Property HairColorPresente As Boolean
        ''' <summary>AHCF\Hair Color  -&gt;  CLFM / NULL. Referencia en el espacio del orden de carga.</summary>
        Property HairColor As UInteger
    End Interface

    ''' <summary>Un bloque HeadParts, venga del record que venga.
    ''' <para>Lo declaran igual: RaceFO4_FemaleHeadParts, RaceFO4_MaleHeadParts, RaceSSE_HeadParts, RaceSSE_HeadParts2.</para></summary>
    Public Interface IBloque_HeadParts
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Head Part\INDX\Head Part Number. Distinto de que el campo valga cero.</summary>
        Property HeadPartNumberPresente As Boolean
        ''' <summary>Head Part\INDX\Head Part Number</summary>
        Property HeadPartNumber As UInteger
        ''' <summary>El record trae Head Part\HEAD\Head. Distinto de que el campo valga cero.</summary>
        Property HeadPartHeadPresente As Boolean
        ''' <summary>Head Part\HEAD\Head  -&gt;  HDPT / NULL. Referencia en el espacio del orden de carga.</summary>
        Property HeadPartHead As UInteger
    End Interface

    ''' <summary>Un bloque Includes, venga del record que venga.
    ''' <para>Lo declaran igual: ArmoFO4_Includes, NpcFO4_Includes.</para></summary>
    Public Interface IBloque_Includes
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Include\Mod. Distinto de que el campo valga cero.</summary>
        Property IncludeModPresente As Boolean
        ''' <summary>Include\Mod  -&gt;  OMOD. Referencia en el espacio del orden de carga.</summary>
        Property IncludeMod As UInteger
        ''' <summary>El record trae Include\Attach Point Index. Distinto de que el campo valga cero.</summary>
        Property IncludeAttachPointIndexPresente As Boolean
        ''' <summary>Include\Attach Point Index</summary>
        Property IncludeAttachPointIndex As Byte
        ''' <summary>El record trae Include\Optional. Distinto de que el campo valga cero.</summary>
        Property IncludeOptionalPresente As Boolean
        ''' <summary>Include\Optional. Ponerlo en verdadero conserva el valor previo si no era cero.</summary>
        Property IncludeOptional As Boolean
        ''' <summary>El record trae Include\Don't Use All. Distinto de que el campo valga cero.</summary>
        Property IncludeDonTUseAllPresente As Boolean
        ''' <summary>Include\Don't Use All. Ponerlo en verdadero conserva el valor previo si no era cero.</summary>
        Property IncludeDonTUseAll As Boolean
    End Interface

    ''' <summary>Un bloque Items, venga del record que venga.
    ''' <para>Lo declaran igual: NpcFO4_Items, NpcSSE_Items, QustFO4_Items, QustSSE_Items.</para></summary>
    Public Interface IBloque_Items
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Item\CNTO\Item\Item. Distinto de que el campo valga cero.</summary>
        Property ItemPresente As Boolean
        ''' <summary>Item\CNTO\Item\Item. Referencia en el espacio del orden de carga.</summary>
        Property Item As UInteger
        ''' <summary>El record trae Item\CNTO\Item\Count. Distinto de que el campo valga cero.</summary>
        Property ItemCountPresente As Boolean
        ''' <summary>Item\CNTO\Item\Count</summary>
        Property ItemCount As Integer
        ''' <summary>El record trae Item\COED\Extra Data\Owner. Distinto de que el campo valga cero.</summary>
        Property ExtraDataOwnerPresente As Boolean
        ''' <summary>Item\COED\Extra Data\Owner  -&gt;  NPC_ / FACT / NULL. Referencia en el espacio del orden de carga.</summary>
        Property ExtraDataOwner As UInteger
        ''' <summary>El record trae Item\COED\Extra Data\Global Variable / Required Rank\Global Variable. Distinto de que el campo valga cero.</summary>
        Property GlobalVariableRequiredRankGlobalVariablePresente As Boolean
        ''' <summary>Item\COED\Extra Data\Global Variable / Required Rank\Global Variable  -&gt;  GLOB / NULL. Referencia en el espacio del orden de carga.</summary>
        Property GlobalVariableRequiredRankGlobalVariable As UInteger
        ''' <summary>El record trae Item\COED\Extra Data\Global Variable / Required Rank\Required Rank. Distinto de que el campo valga cero.</summary>
        Property GlobalVariableRequiredRankRequiredRankPresente As Boolean
        ''' <summary>Item\COED\Extra Data\Global Variable / Required Rank\Required Rank</summary>
        Property GlobalVariableRequiredRankRequiredRank As Integer
        ''' <summary>El record trae Item\COED\Extra Data\Item Condition. Distinto de que el campo valga cero.</summary>
        Property ExtraDataItemConditionPresente As Boolean
        ''' <summary>Item\COED\Extra Data\Item Condition</summary>
        Property ExtraDataItemCondition As Single
    End Interface

    ''' <summary>Un bloque Keywords, venga del record que venga.
    ''' <para>Lo declaran igual: ArmoFO4_Keywords, ArmoFO4_Keywords2, ArmoSSE_Keywords, NpcFO4_Keywords, NpcFO4_Keywords2, NpcSSE_Keywords, QustFO4_Keywords, QustSSE_Keywords, RaceFO4_Keywords, RaceSSE_Keywords.</para></summary>
    Public Interface IBloque_Keywords
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Keyword. Distinto de que el campo valga cero.</summary>
        Property KeywordPresente As Boolean
        ''' <summary>Keyword  -&gt;  KYWD / NULL. Referencia en el espacio del orden de carga.</summary>
        Property Keyword As UInteger
    End Interface

    ''' <summary>Un bloque Materials, venga del record que venga.
    ''' <para>Lo declaran igual: ArmaFO4_Materials, ArmaFO4_Materials2, ArmaFO4_Materials3, ArmaFO4_Materials4, ArmoFO4_Materials, ArmoFO4_Materials2, ArmoFO4_Materials3, BptdFO4_Materials, BptdFO4_Materials2, HdptFO4_Materials, LvlnFO4_Materials, NpcFO4_Materials, OmodFO4_Materials, RaceFO4_Materials, RaceFO4_Materials2, RaceFO4_Materials3, RaceFO4_Materials4, RaceFO4_Materials5.</para></summary>
    Public Interface IBloque_Materials
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Material\File Hash. Distinto de que el campo valga cero.</summary>
        Property MaterialFileHashPresente As Boolean
        ''' <summary>Material\File Hash</summary>
        Property MaterialFileHash As UInteger
        ''' <summary>El record trae Material\Extension. Distinto de que el campo valga cero.</summary>
        Property MaterialExtensionPresente As Boolean
        ''' <summary>Material\Extension</summary>
        Property MaterialExtension As String
        ''' <summary>El record trae Material\Folder Hash. Distinto de que el campo valga cero.</summary>
        Property MaterialFolderHashPresente As Boolean
        ''' <summary>Material\Folder Hash</summary>
        Property MaterialFolderHash As UInteger
    End Interface

    ''' <summary>Un bloque MorphGroupSliders, venga del record que venga.
    ''' <para>Lo declaran igual: RaceFO4_MorphGroupSliders, RaceFO4_MorphGroupSliders2.</para></summary>
    Public Interface IBloque_MorphGroupSliders
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Index. Distinto de que el campo valga cero.</summary>
        Property IndexPresente As Boolean
        ''' <summary>Index</summary>
        Property Index As UInteger
    End Interface

    ''' <summary>Un bloque MorphGroups, venga del record que venga.
    ''' <para>Lo declaran igual: RaceFO4_FemaleMorphGroups, RaceFO4_MaleMorphGroups.</para></summary>
    Public Interface IBloque_MorphGroups
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Morph Group\MPGN\Name. Distinto de que el campo valga cero.</summary>
        Property MorphGroupNamePresente As Boolean
        ''' <summary>Morph Group\MPGN\Name</summary>
        Property MorphGroupName As String
        ''' <summary>El record trae Morph Group\MPPC\Count. Distinto de que el campo valga cero.</summary>
        Property MorphGroupCountPresente As Boolean
        ''' <summary>Morph Group\MPPC\Count</summary>
        Property MorphGroupCount As UInteger
        ''' <summary>El record trae Morph Group\MPPK\Mask. Distinto de que el campo valga cero.</summary>
        Property MorphGroupMaskPresente As Boolean
        ''' <summary>Morph Group\MPPK\Mask</summary>
        Property MorphGroupMask As UShort
        ''' <summary>Nombre del valor de Morph Group\MPPK\Mask.</summary>
        ReadOnly Property MorphGroupMaskNombre As String
        ''' <summary>Female Morph Groups\Morph Group\Morph Presets</summary>
        ReadOnly Property MorphPresets As IReadOnlyList(Of IBloque_MorphPresets)
        ''' <summary>Female Morph Groups\Morph Group\MPGS\Morph Group Sliders</summary>
        ReadOnly Property MorphGroupSliders As IReadOnlyList(Of IBloque_MorphGroupSliders)
    End Interface

    ''' <summary>Un bloque MorphPresets, venga del record que venga.
    ''' <para>Lo declaran igual: RaceFO4_MorphPresets, RaceFO4_MorphPresets2.</para></summary>
    Public Interface IBloque_MorphPresets
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Morph Preset\MPPI\Index. Distinto de que el campo valga cero.</summary>
        Property MorphPresetIndexPresente As Boolean
        ''' <summary>Morph Preset\MPPI\Index</summary>
        Property MorphPresetIndex As UInteger
        ''' <summary>El record trae Morph Preset\MPPN\Name. Distinto de que el campo valga cero.</summary>
        Property MorphPresetNamePresente As Boolean
        ''' <summary>Morph Preset\MPPN\Name. Resuelto contra las tablas de texto si el archivo las usa.</summary>
        Property MorphPresetName As String
        ''' <summary>El record trae Morph Preset\MPPM\Morph. Distinto de que el campo valga cero.</summary>
        Property MorphPresetMorphPresente As Boolean
        ''' <summary>Morph Preset\MPPM\Morph</summary>
        Property MorphPresetMorph As String
        ''' <summary>El record trae Morph Preset\MPPT\Texture. Distinto de que el campo valga cero.</summary>
        Property MorphPresetTexturePresente As Boolean
        ''' <summary>Morph Preset\MPPT\Texture  -&gt;  TXST. Referencia en el espacio del orden de carga.</summary>
        Property MorphPresetTexture As UInteger
        ''' <summary>El record trae Morph Preset\MPPF\Playable. Distinto de que el campo valga cero.</summary>
        Property MorphPresetPlayablePresente As Boolean
        ''' <summary>Morph Preset\MPPF\Playable. Ponerlo en verdadero conserva el valor previo si no era cero.</summary>
        Property MorphPresetPlayable As Boolean
    End Interface

    ''' <summary>Un bloque Options, venga del record que venga.
    ''' <para>Lo declaran igual: RaceFO4_Options, RaceFO4_Options2.</para></summary>
    Public Interface IBloque_Options
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Option\TETI\Index\Slot. Distinto de que el campo valga cero.</summary>
        Property IndexSlotPresente As Boolean
        ''' <summary>Option\TETI\Index\Slot</summary>
        Property IndexSlot As UShort
        ''' <summary>El record trae Option\TETI\Index\Index. Distinto de que el campo valga cero.</summary>
        Property OptionIndexPresente As Boolean
        ''' <summary>Option\TETI\Index\Index</summary>
        Property OptionIndex As UShort
        ''' <summary>El record trae Option\TTGP\Name. Distinto de que el campo valga cero.</summary>
        Property OptionNamePresente As Boolean
        ''' <summary>Option\TTGP\Name. Resuelto contra las tablas de texto si el archivo las usa.</summary>
        Property OptionName As String
        ''' <summary>El record trae Option\TTEF\Flags. Distinto de que el campo valga cero.</summary>
        Property OptionFlagsPresente As Boolean
        ''' <summary>Option\TTEF\Flags</summary>
        Property OptionFlags As UShort
        ''' <summary>El record trae Option\TTEB\Blend Operation. Distinto de que el campo valga cero.</summary>
        Property OptionBlendOperationPresente As Boolean
        ''' <summary>Option\TTEB\Blend Operation</summary>
        Property OptionBlendOperation As UInteger
        ''' <summary>El record trae Option\TTED\Default. Distinto de que el campo valga cero.</summary>
        Property OptionDefaultPresente As Boolean
        ''' <summary>Option\TTED\Default</summary>
        Property OptionDefault As Single
        ''' <summary>Male Tint Layers\Group\Options\Option\Conditions</summary>
        ReadOnly Property Conditions As IReadOnlyList(Of IBloque_Conditions2)
        ''' <summary>Male Tint Layers\Group\Options\Option\Textures</summary>
        ReadOnly Property Textures As IReadOnlyList(Of IBloque_Textures2)
        ''' <summary>Male Tint Layers\Group\Options\Option\TTEC\Template Colors</summary>
        ReadOnly Property TemplateColors As IReadOnlyList(Of IBloque_TemplateColors)
    End Interface

    ''' <summary>Un bloque Parts, venga del record que venga.
    ''' <para>Lo declaran igual: RaceFO4_Parts, RaceFO4_Parts2.</para></summary>
    Public Interface IBloque_Parts
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Part\Model\MODL\Model FileName. Distinto de que el campo valga cero.</summary>
        Property PartModelFileNamePresente As Boolean
        ''' <summary>Part\Model\MODL\Model FileName</summary>
        Property PartModelFileName As String
        ''' <summary>El record trae Part\Model\MODT\Model Information\ERROR. Distinto de que el campo valga cero.</summary>
        Property ModelInformationERRORPresente As Boolean
        ''' <summary>Part\Model\MODT\Model Information\ERROR</summary>
        Property ModelInformationERROR As Byte()
        ''' <summary>El record trae Part\Model\MODC\Color Remapping Index. Distinto de que el campo valga cero.</summary>
        Property ModelColorRemappingIndexPresente As Boolean
        ''' <summary>Part\Model\MODC\Color Remapping Index</summary>
        Property ModelColorRemappingIndex As Single
        ''' <summary>El record trae Part\Model\MODS\Material Swap. Distinto de que el campo valga cero.</summary>
        Property ModelMaterialSwapPresente As Boolean
        ''' <summary>Part\Model\MODS\Material Swap  -&gt;  MSWP. Referencia en el espacio del orden de carga.</summary>
        Property ModelMaterialSwap As UInteger
        ''' <summary>El record trae Part\Model\MODF\Flags. Distinto de que el campo valga cero.</summary>
        Property ModelFlagsPresente As Boolean
        ''' <summary>Part\Model\MODF\Flags</summary>
        Property ModelFlags As Byte
        ''' <summary>Bit 0 de Part\Model\MODF\Flags: Has FaceBones Model</summary>
        Property ModelFlagsHasFaceBonesModel As Boolean
        ''' <summary>Bit 1 de Part\Model\MODF\Flags: Has 1stPerson Model</summary>
        Property ModelFlagsHas1stPersonModel As Boolean
        ''' <summary>Body Data\Male Body Data\Parts\Part\Model\MODT\Model Information\Textures</summary>
        ReadOnly Property Textures As IReadOnlyList(Of IBloque_Textures)
        ''' <summary>Body Data\Male Body Data\Parts\Part\Model\MODT\Model Information\Counters</summary>
        ReadOnly Property Counters As IReadOnlyList(Of IBloque_Counters)
        ''' <summary>Body Data\Male Body Data\Parts\Part\Model\MODT\Model Information\Addon Nodes</summary>
        ReadOnly Property AddonNodes As IReadOnlyList(Of IBloque_AddonNodes)
        ''' <summary>Body Data\Male Body Data\Parts\Part\Model\MODT\Model Information\Materials</summary>
        ReadOnly Property Materials As IReadOnlyList(Of IBloque_Materials)
    End Interface

    ''' <summary>Un bloque Parts2, venga del record que venga.
    ''' <para>Lo declaran igual: RaceSSE_Parts, RaceSSE_Parts2.</para></summary>
    Public Interface IBloque_Parts2
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Part\Model\MODL\Model FileName. Distinto de que el campo valga cero.</summary>
        Property PartModelFileNamePresente As Boolean
        ''' <summary>Part\Model\MODL\Model FileName</summary>
        Property PartModelFileName As String
        ''' <summary>El record trae Part\Model\MODT\Model Information\ERROR. Distinto de que el campo valga cero.</summary>
        Property ModelInformationERRORPresente As Boolean
        ''' <summary>Part\Model\MODT\Model Information\ERROR</summary>
        Property ModelInformationERROR As Byte()
        ''' <summary>Body Data\Male Body Data\Parts\Part\Model\MODT\Model Information\Textures</summary>
        ReadOnly Property Textures As IReadOnlyList(Of IBloque_Textures)
        ''' <summary>Body Data\Male Body Data\Parts\Part\Model\MODT\Model Information\Counters</summary>
        ReadOnly Property Counters As IReadOnlyList(Of IBloque_Counters)
        ''' <summary>Body Data\Male Body Data\Parts\Part\Model\MODT\Model Information\Addon Nodes</summary>
        ReadOnly Property AddonNodes As IReadOnlyList(Of IBloque_AddonNodes)
        ''' <summary>Body Data\Male Body Data\Parts\Part\Model\MODS\Alternate Textures</summary>
        ReadOnly Property AlternateTextures As IReadOnlyList(Of IBloque_AlternateTextures)
    End Interface

    ''' <summary>Un bloque Presets, venga del record que venga.
    ''' <para>Lo declaran igual: RaceSSE_Presets, RaceSSE_Presets2.</para></summary>
    Public Interface IBloque_Presets
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Preset\TINC\Color. Distinto de que el campo valga cero.</summary>
        Property PresetColorPresente As Boolean
        ''' <summary>Preset\TINC\Color  -&gt;  CLFM / NULL. Referencia en el espacio del orden de carga.</summary>
        Property PresetColor As UInteger
        ''' <summary>El record trae Preset\TINV\Default Value. Distinto de que el campo valga cero.</summary>
        Property PresetDefaultValuePresente As Boolean
        ''' <summary>Preset\TINV\Default Value</summary>
        Property PresetDefaultValue As Single
        ''' <summary>El record trae Preset\TIRS\Index. Distinto de que el campo valga cero.</summary>
        Property PresetIndexPresente As Boolean
        ''' <summary>Preset\TIRS\Index</summary>
        Property PresetIndex As UShort
    End Interface

    ''' <summary>Un bloque Properties, venga del record que venga.
    ''' <para>Lo declaran igual: NpcFO4_Properties2, RaceFO4_Properties.</para></summary>
    Public Interface IBloque_Properties
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Property\Actor Value. Distinto de que el campo valga cero.</summary>
        Property PropertyActorValuePresente As Boolean
        ''' <summary>Property\Actor Value  -&gt;  AVIF / NULL. Referencia en el espacio del orden de carga.</summary>
        Property PropertyActorValue As UInteger
        ''' <summary>El record trae Property\Value. Distinto de que el campo valga cero.</summary>
        Property PropertyValuePresente As Boolean
        ''' <summary>Property\Value</summary>
        Property PropertyValue As Single
    End Interface

    ''' <summary>Un bloque Properties2, venga del record que venga.
    ''' <para>Lo declaran igual: ArmoSSE_Properties, NpcSSE_Properties, QustSSE_Properties, QustSSE_Properties2.</para></summary>
    Public Interface IBloque_Properties2
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Property\propertyName. Distinto de que el campo valga cero.</summary>
        Property PropertyNamePresente As Boolean
        ''' <summary>Property\propertyName</summary>
        Property PropertyName As String
        ''' <summary>El record trae Property\Type. Distinto de que el campo valga cero.</summary>
        Property PropertyTypePresente As Boolean
        ''' <summary>Property\Type</summary>
        Property PropertyType As Byte
        ''' <summary>El record trae Property\Flags. Distinto de que el campo valga cero.</summary>
        Property PropertyFlagsPresente As Boolean
        ''' <summary>Property\Flags</summary>
        Property PropertyFlags As Byte
        ''' <summary>Nombre del valor de Property\Flags.</summary>
        ReadOnly Property PropertyFlagsNombre As String
        ''' <summary>El record trae Property\Value\Object Union\Object v2\Alias. Distinto de que el campo valga cero.</summary>
        Property ObjectV2AliasPresente As Boolean
        ''' <summary>Property\Value\Object Union\Object v2\Alias</summary>
        Property ObjectV2Alias As Short
        ''' <summary>El record trae Property\Value\Object Union\Object v2\FormID. Distinto de que el campo valga cero.</summary>
        Property ObjectV2FormIDPresente As Boolean
        ''' <summary>Property\Value\Object Union\Object v2\FormID. Referencia en el espacio del orden de carga.</summary>
        Property ObjectV2FormID As UInteger
        ''' <summary>El record trae Property\Value\Object Union\Object v1\FormID. Distinto de que el campo valga cero.</summary>
        Property ObjectV1FormIDPresente As Boolean
        ''' <summary>Property\Value\Object Union\Object v1\FormID. Referencia en el espacio del orden de carga.</summary>
        Property ObjectV1FormID As UInteger
        ''' <summary>El record trae Property\Value\Object Union\Object v1\Alias. Distinto de que el campo valga cero.</summary>
        Property ObjectV1AliasPresente As Boolean
        ''' <summary>Property\Value\Object Union\Object v1\Alias</summary>
        Property ObjectV1Alias As Short
        ''' <summary>El record trae Property\Value\String. Distinto de que el campo valga cero.</summary>
        Property ValueStringPresente As Boolean
        ''' <summary>Property\Value\String</summary>
        Property ValueString As String
        ''' <summary>El record trae Property\Value\Int32. Distinto de que el campo valga cero.</summary>
        Property ValueInt32Presente As Boolean
        ''' <summary>Property\Value\Int32</summary>
        Property ValueInt32 As Integer
        ''' <summary>El record trae Property\Value\Float. Distinto de que el campo valga cero.</summary>
        Property ValueFloatPresente As Boolean
        ''' <summary>Property\Value\Float</summary>
        Property ValueFloat As Single
        ''' <summary>El record trae Property\Value\Bool. Distinto de que el campo valga cero.</summary>
        Property ValueBoolPresente As Boolean
        ''' <summary>Property\Value\Bool. Ponerlo en verdadero conserva el valor previo si no era cero.</summary>
        Property ValueBool As Boolean
        ''' <summary>VMAD\Virtual Machine Adapter\Scripts\Script\Properties\Property\Value\Array of Object</summary>
        ReadOnly Property ArrayOfObject As IReadOnlyList(Of IBloque_ArrayOfObject)
        ''' <summary>VMAD\Virtual Machine Adapter\Scripts\Script\Properties\Property\Value\Array of String</summary>
        ReadOnly Property ArrayOfString As IReadOnlyList(Of IBloque_ArrayOfString)
        ''' <summary>VMAD\Virtual Machine Adapter\Scripts\Script\Properties\Property\Value\Array of Int32</summary>
        ReadOnly Property ArrayOfInt32 As IReadOnlyList(Of IBloque_ArrayOfInt32)
        ''' <summary>VMAD\Virtual Machine Adapter\Scripts\Script\Properties\Property\Value\Array of Float</summary>
        ReadOnly Property ArrayOfFloat As IReadOnlyList(Of IBloque_ArrayOfFloat)
        ''' <summary>VMAD\Virtual Machine Adapter\Scripts\Script\Properties\Property\Value\Array of Bool</summary>
        ReadOnly Property ArrayOfBool As IReadOnlyList(Of IBloque_ArrayOfBool)
    End Interface

    ''' <summary>Un bloque Properties3, venga del record que venga.
    ''' <para>Lo declaran igual: ArmoFO4_Properties, NpcFO4_Properties, QustFO4_Properties, QustFO4_Properties2, QustFO4_Properties3.</para></summary>
    Public Interface IBloque_Properties3
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Property\propertyName. Distinto de que el campo valga cero.</summary>
        Property PropertyNamePresente As Boolean
        ''' <summary>Property\propertyName</summary>
        Property PropertyName As String
        ''' <summary>El record trae Property\Type. Distinto de que el campo valga cero.</summary>
        Property PropertyTypePresente As Boolean
        ''' <summary>Property\Type</summary>
        Property PropertyType As Byte
        ''' <summary>Nombre del valor de Property\Type.</summary>
        ReadOnly Property PropertyTypeNombre As String
        ''' <summary>El record trae Property\Flags. Distinto de que el campo valga cero.</summary>
        Property PropertyFlagsPresente As Boolean
        ''' <summary>Property\Flags</summary>
        Property PropertyFlags As Byte
        ''' <summary>Nombre del valor de Property\Flags.</summary>
        ReadOnly Property PropertyFlagsNombre As String
        ''' <summary>El record trae Property\Value\Object Union\Object v2\Alias. Distinto de que el campo valga cero.</summary>
        Property ObjectV2AliasPresente As Boolean
        ''' <summary>Property\Value\Object Union\Object v2\Alias</summary>
        Property ObjectV2Alias As Short
        ''' <summary>El record trae Property\Value\Object Union\Object v2\FormID. Distinto de que el campo valga cero.</summary>
        Property ObjectV2FormIDPresente As Boolean
        ''' <summary>Property\Value\Object Union\Object v2\FormID. Referencia en el espacio del orden de carga.</summary>
        Property ObjectV2FormID As UInteger
        ''' <summary>El record trae Property\Value\Object Union\Object v1\FormID. Distinto de que el campo valga cero.</summary>
        Property ObjectV1FormIDPresente As Boolean
        ''' <summary>Property\Value\Object Union\Object v1\FormID. Referencia en el espacio del orden de carga.</summary>
        Property ObjectV1FormID As UInteger
        ''' <summary>El record trae Property\Value\Object Union\Object v1\Alias. Distinto de que el campo valga cero.</summary>
        Property ObjectV1AliasPresente As Boolean
        ''' <summary>Property\Value\Object Union\Object v1\Alias</summary>
        Property ObjectV1Alias As Short
        ''' <summary>El record trae Property\Value\String. Distinto de que el campo valga cero.</summary>
        Property ValueStringPresente As Boolean
        ''' <summary>Property\Value\String</summary>
        Property ValueString As String
        ''' <summary>El record trae Property\Value\Int32. Distinto de que el campo valga cero.</summary>
        Property ValueInt32Presente As Boolean
        ''' <summary>Property\Value\Int32</summary>
        Property ValueInt32 As Integer
        ''' <summary>El record trae Property\Value\Float. Distinto de que el campo valga cero.</summary>
        Property ValueFloatPresente As Boolean
        ''' <summary>Property\Value\Float</summary>
        Property ValueFloat As Single
        ''' <summary>El record trae Property\Value\Bool. Distinto de que el campo valga cero.</summary>
        Property ValueBoolPresente As Boolean
        ''' <summary>Property\Value\Bool. Ponerlo en verdadero conserva el valor previo si no era cero.</summary>
        Property ValueBool As Boolean
        ''' <summary>El record trae Property\Value\Array of Variable\Element Count. Distinto de que el campo valga cero.</summary>
        Property ArrayOfVariableElementCountPresente As Boolean
        ''' <summary>Property\Value\Array of Variable\Element Count</summary>
        Property ArrayOfVariableElementCount As UInteger
        ''' <summary>VMAD\Virtual Machine Adapter\Scripts\Script\Properties\Property\Value\Struct</summary>
        ReadOnly Property Struct As IReadOnlyList(Of IBloque_Struct)
        ''' <summary>VMAD\Virtual Machine Adapter\Scripts\Script\Properties\Property\Value\Array of Object</summary>
        ReadOnly Property ArrayOfObject As IReadOnlyList(Of IBloque_ArrayOfObject)
        ''' <summary>VMAD\Virtual Machine Adapter\Scripts\Script\Properties\Property\Value\Array of String</summary>
        ReadOnly Property ArrayOfString As IReadOnlyList(Of IBloque_ArrayOfString)
        ''' <summary>VMAD\Virtual Machine Adapter\Scripts\Script\Properties\Property\Value\Array of Int32</summary>
        ReadOnly Property ArrayOfInt32 As IReadOnlyList(Of IBloque_ArrayOfInt32)
        ''' <summary>VMAD\Virtual Machine Adapter\Scripts\Script\Properties\Property\Value\Array of Float</summary>
        ReadOnly Property ArrayOfFloat As IReadOnlyList(Of IBloque_ArrayOfFloat)
        ''' <summary>VMAD\Virtual Machine Adapter\Scripts\Script\Properties\Property\Value\Array of Bool</summary>
        ReadOnly Property ArrayOfBool As IReadOnlyList(Of IBloque_ArrayOfBool)
        ''' <summary>VMAD\Virtual Machine Adapter\Scripts\Script\Properties\Property\Value\Array of Struct\Struct</summary>
        ReadOnly Property Struct2 As IReadOnlyList(Of IBloque_Struct)
    End Interface

    ''' <summary>Un bloque Properties4, venga del record que venga.
    ''' <para>Lo declaran igual: ArmoFO4_Properties2, NpcFO4_Properties3, OmodFO4_Properties.</para></summary>
    Public Interface IBloque_Properties4
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Property\Value Type. Distinto de que el campo valga cero.</summary>
        Property PropertyValueTypePresente As Boolean
        ''' <summary>Property\Value Type</summary>
        Property PropertyValueType As Byte
        ''' <summary>Nombre del valor de Property\Value Type.</summary>
        ReadOnly Property PropertyValueTypeNombre As String
        ''' <summary>El record trae Property\Function Type\Function Type. Distinto de que el campo valga cero.</summary>
        Property PropertyFunctionTypePresente As Boolean
        ''' <summary>Property\Function Type\Function Type</summary>
        Property PropertyFunctionType As Byte
        ''' <summary>Nombre del valor de Property\Function Type\Function Type.</summary>
        ReadOnly Property PropertyFunctionTypeNombre As String
        ''' <summary>El record trae Property\Property. Distinto de que el campo valga cero.</summary>
        Property PropertyPresente As Boolean
        ''' <summary>Property\Property</summary>
        Property [Property] As UShort
        ''' <summary>El record trae Property\Value 1\Value 1 - Unknown. Distinto de que el campo valga cero.</summary>
        Property PropertyValue1UnknownPresente As Boolean
        ''' <summary>Property\Value 1\Value 1 - Unknown</summary>
        Property PropertyValue1Unknown As Byte()
        ''' <summary>El record trae Property\Value 1\Value 1 - Int. Distinto de que el campo valga cero.</summary>
        Property PropertyValue1IntPresente As Boolean
        ''' <summary>Property\Value 1\Value 1 - Int</summary>
        Property PropertyValue1Int As UInteger
        ''' <summary>El record trae Property\Value 1\Value 1 - Float. Distinto de que el campo valga cero.</summary>
        Property PropertyValue1FloatPresente As Boolean
        ''' <summary>Property\Value 1\Value 1 - Float</summary>
        Property PropertyValue1Float As Single
        ''' <summary>El record trae Property\Value 1\Value 1 - Bool. Distinto de que el campo valga cero.</summary>
        Property PropertyValue1BoolPresente As Boolean
        ''' <summary>Property\Value 1\Value 1 - Bool. Ponerlo en verdadero conserva el valor previo si no era cero.</summary>
        Property PropertyValue1Bool As Boolean
        ''' <summary>El record trae Property\Value 1\Value 1 - FormID. Distinto de que el campo valga cero.</summary>
        Property PropertyValue1FormIDPresente As Boolean
        ''' <summary>Property\Value 1\Value 1 - FormID. Referencia en el espacio del orden de carga.</summary>
        Property PropertyValue1FormID As UInteger
        ''' <summary>El record trae Property\Value 1\Value 1 - Enum. Distinto de que el campo valga cero.</summary>
        Property PropertyValue1EnumPresente As Boolean
        ''' <summary>Property\Value 1\Value 1 - Enum</summary>
        Property PropertyValue1Enum As UInteger
        ''' <summary>El record trae Property\Value 1\Sound Level. Distinto de que el campo valga cero.</summary>
        Property Value1SoundLevelPresente As Boolean
        ''' <summary>Property\Value 1\Sound Level</summary>
        Property Value1SoundLevel As UInteger
        ''' <summary>Nombre del valor de Property\Value 1\Sound Level.</summary>
        ReadOnly Property Value1SoundLevelNombre As String
        ''' <summary>El record trae Property\Value 1\Stagger Value. Distinto de que el campo valga cero.</summary>
        Property Value1StaggerValuePresente As Boolean
        ''' <summary>Property\Value 1\Stagger Value</summary>
        Property Value1StaggerValue As UInteger
        ''' <summary>Nombre del valor de Property\Value 1\Stagger Value.</summary>
        ReadOnly Property Value1StaggerValueNombre As String
        ''' <summary>El record trae Property\Value 1\Hit Behaviour. Distinto de que el campo valga cero.</summary>
        Property Value1HitBehaviourPresente As Boolean
        ''' <summary>Property\Value 1\Hit Behaviour</summary>
        Property Value1HitBehaviour As UInteger
        ''' <summary>Nombre del valor de Property\Value 1\Hit Behaviour.</summary>
        ReadOnly Property Value1HitBehaviourNombre As String
        ''' <summary>El record trae Property\Value 2\Value 2 - Int. Distinto de que el campo valga cero.</summary>
        Property PropertyValue2IntPresente As Boolean
        ''' <summary>Property\Value 2\Value 2 - Int</summary>
        Property PropertyValue2Int As UInteger
        ''' <summary>El record trae Property\Value 2\Value 2 - Float. Distinto de que el campo valga cero.</summary>
        Property PropertyValue2FloatPresente As Boolean
        ''' <summary>Property\Value 2\Value 2 - Float</summary>
        Property PropertyValue2Float As Single
        ''' <summary>El record trae Property\Value 2\Value 2 - Bool. Distinto de que el campo valga cero.</summary>
        Property PropertyValue2BoolPresente As Boolean
        ''' <summary>Property\Value 2\Value 2 - Bool. Ponerlo en verdadero conserva el valor previo si no era cero.</summary>
        Property PropertyValue2Bool As Boolean
        ''' <summary>El record trae Property\Step. Distinto de que el campo valga cero.</summary>
        Property PropertyStepPresente As Boolean
        ''' <summary>Property\Step</summary>
        Property PropertyStep As Single
    End Interface

    ''' <summary>Un bloque RacePresets, venga del record que venga.
    ''' <para>Lo declaran igual: RaceFO4_FemaleRacePresets, RaceFO4_MaleRacePresets, RaceSSE_RacePresetsFemale, RaceSSE_RacePresetsMale.</para></summary>
    Public Interface IBloque_RacePresets
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae RPRF\Preset NPC. Distinto de que el campo valga cero.</summary>
        Property PresetNPCPresente As Boolean
        ''' <summary>RPRF\Preset NPC  -&gt;  NPC_ / NULL. Referencia en el espacio del orden de carga.</summary>
        Property PresetNPC As UInteger
    End Interface

    ''' <summary>Un bloque Resistances, venga del record que venga.
    ''' <para>Lo declaran igual: ArmoFO4_Resistances, NpcFO4_Resistances.</para></summary>
    Public Interface IBloque_Resistances
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Resistance\Damage Type. Distinto de que el campo valga cero.</summary>
        Property ResistanceDamageTypePresente As Boolean
        ''' <summary>Resistance\Damage Type  -&gt;  DMGT. Referencia en el espacio del orden de carga.</summary>
        Property ResistanceDamageType As UInteger
        ''' <summary>El record trae Resistance\Value. Distinto de que el campo valga cero.</summary>
        Property ResistanceValuePresente As Boolean
        ''' <summary>Resistance\Value</summary>
        Property ResistanceValue As UInteger
    End Interface

    ''' <summary>Un bloque Scripts, venga del record que venga.
    ''' <para>Lo declaran igual: ArmoSSE_Scripts, NpcSSE_Scripts, QustSSE_Scripts.</para></summary>
    Public Interface IBloque_Scripts
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Script\ScriptName. Distinto de que el campo valga cero.</summary>
        Property ScriptNamePresente As Boolean
        ''' <summary>Script\ScriptName</summary>
        Property ScriptName As String
        ''' <summary>El record trae Script\Flags. Distinto de que el campo valga cero.</summary>
        Property ScriptFlagsPresente As Boolean
        ''' <summary>Script\Flags</summary>
        Property ScriptFlags As Byte
        ''' <summary>Nombre del valor de Script\Flags.</summary>
        ReadOnly Property ScriptFlagsNombre As String
        ''' <summary>VMAD\Virtual Machine Adapter\Scripts\Script\Properties</summary>
        ReadOnly Property Properties As IReadOnlyList(Of IBloque_Properties2)
    End Interface

    ''' <summary>Un bloque Scripts2, venga del record que venga.
    ''' <para>Lo declaran igual: ArmoFO4_Scripts, NpcFO4_Scripts, QustFO4_Scripts.</para></summary>
    Public Interface IBloque_Scripts2
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Script\ScriptName. Distinto de que el campo valga cero.</summary>
        Property ScriptNamePresente As Boolean
        ''' <summary>Script\ScriptName</summary>
        Property ScriptName As String
        ''' <summary>El record trae Script\Flags. Distinto de que el campo valga cero.</summary>
        Property ScriptFlagsPresente As Boolean
        ''' <summary>Script\Flags</summary>
        Property ScriptFlags As Byte
        ''' <summary>Nombre del valor de Script\Flags.</summary>
        ReadOnly Property ScriptFlagsNombre As String
        ''' <summary>VMAD\Virtual Machine Adapter\Scripts\Script\Properties</summary>
        ReadOnly Property Properties As IReadOnlyList(Of IBloque_Properties3)
    End Interface

    ''' <summary>Un bloque Stages, venga del record que venga.
    ''' <para>Lo declaran igual: ArmoFO4_Stages, NpcFO4_Stages.</para></summary>
    Public Interface IBloque_Stages
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Stage\DSTD\Destruction Stage Data\Health %. Distinto de que el campo valga cero.</summary>
        Property DestructionStageDataHealthPresente As Boolean
        ''' <summary>Stage\DSTD\Destruction Stage Data\Health %</summary>
        Property DestructionStageDataHealth As Byte
        ''' <summary>El record trae Stage\DSTD\Destruction Stage Data\Index. Distinto de que el campo valga cero.</summary>
        Property DestructionStageDataIndexPresente As Boolean
        ''' <summary>Stage\DSTD\Destruction Stage Data\Index</summary>
        Property DestructionStageDataIndex As Byte
        ''' <summary>El record trae Stage\DSTD\Destruction Stage Data\Model Damage Stage. Distinto de que el campo valga cero.</summary>
        Property DestructionStageDataModelDamageStagePresente As Boolean
        ''' <summary>Stage\DSTD\Destruction Stage Data\Model Damage Stage</summary>
        Property DestructionStageDataModelDamageStage As Byte
        ''' <summary>El record trae Stage\DSTD\Destruction Stage Data\Flags. Distinto de que el campo valga cero.</summary>
        Property DestructionStageDataFlagsPresente As Boolean
        ''' <summary>Stage\DSTD\Destruction Stage Data\Flags</summary>
        Property DestructionStageDataFlags As Byte
        ''' <summary>Bit 0 de Stage\DSTD\Destruction Stage Data\Flags: Cap Damage</summary>
        Property DestructionStageDataFlagsCapDamage As Boolean
        ''' <summary>Bit 1 de Stage\DSTD\Destruction Stage Data\Flags: Disable</summary>
        Property DestructionStageDataFlagsDisable As Boolean
        ''' <summary>Bit 2 de Stage\DSTD\Destruction Stage Data\Flags: Destroy</summary>
        Property DestructionStageDataFlagsDestroy As Boolean
        ''' <summary>Bit 3 de Stage\DSTD\Destruction Stage Data\Flags: Ignore External Dmg</summary>
        Property DestructionStageDataFlagsIgnoreExternalDmg As Boolean
        ''' <summary>Bit 4 de Stage\DSTD\Destruction Stage Data\Flags: Becomes Dynamic</summary>
        Property DestructionStageDataFlagsBecomesDynamic As Boolean
        ''' <summary>El record trae Stage\DSTD\Destruction Stage Data\Self Damage per Second. Distinto de que el campo valga cero.</summary>
        Property DestructionStageDataSelfDamagePerSecondPresente As Boolean
        ''' <summary>Stage\DSTD\Destruction Stage Data\Self Damage per Second</summary>
        Property DestructionStageDataSelfDamagePerSecond As Integer
        ''' <summary>El record trae Stage\DSTD\Destruction Stage Data\Explosion. Distinto de que el campo valga cero.</summary>
        Property DestructionStageDataExplosionPresente As Boolean
        ''' <summary>Stage\DSTD\Destruction Stage Data\Explosion  -&gt;  EXPL / NULL. Referencia en el espacio del orden de carga.</summary>
        Property DestructionStageDataExplosion As UInteger
        ''' <summary>El record trae Stage\DSTD\Destruction Stage Data\Debris. Distinto de que el campo valga cero.</summary>
        Property DestructionStageDataDebrisPresente As Boolean
        ''' <summary>Stage\DSTD\Destruction Stage Data\Debris  -&gt;  DEBR / NULL. Referencia en el espacio del orden de carga.</summary>
        Property DestructionStageDataDebris As UInteger
        ''' <summary>El record trae Stage\DSTD\Destruction Stage Data\Debris Count. Distinto de que el campo valga cero.</summary>
        Property DestructionStageDataDebrisCountPresente As Boolean
        ''' <summary>Stage\DSTD\Destruction Stage Data\Debris Count</summary>
        Property DestructionStageDataDebrisCount As Integer
        ''' <summary>El record trae Stage\DSTA\Sequence Name. Distinto de que el campo valga cero.</summary>
        Property StageSequenceNamePresente As Boolean
        ''' <summary>Stage\DSTA\Sequence Name</summary>
        Property StageSequenceName As String
        ''' <summary>El record trae Stage\Model\DMDL\Model FileName. Distinto de que el campo valga cero.</summary>
        Property StageModelFileNamePresente As Boolean
        ''' <summary>Stage\Model\DMDL\Model FileName</summary>
        Property StageModelFileName As String
        ''' <summary>El record trae Stage\Model\DMDT\Model Information\ERROR. Distinto de que el campo valga cero.</summary>
        Property ModelInformationERRORPresente As Boolean
        ''' <summary>Stage\Model\DMDT\Model Information\ERROR</summary>
        Property ModelInformationERROR As Byte()
        ''' <summary>El record trae Stage\Model\DMDC\Color Remapping Index. Distinto de que el campo valga cero.</summary>
        Property ModelColorRemappingIndexPresente As Boolean
        ''' <summary>Stage\Model\DMDC\Color Remapping Index</summary>
        Property ModelColorRemappingIndex As Single
        ''' <summary>El record trae Stage\Model\DMDS\Material Swap. Distinto de que el campo valga cero.</summary>
        Property ModelMaterialSwapPresente As Boolean
        ''' <summary>Stage\Model\DMDS\Material Swap  -&gt;  MSWP. Referencia en el espacio del orden de carga.</summary>
        Property ModelMaterialSwap As UInteger
        ''' <summary>El record trae el marcador Stage\DSTF\End Marker. No lleva valor: el dato es que esté, así que ponerlo en verdadero lo crea y en falso lo saca.</summary>
        Property StageEndMarker As Boolean
        ''' <summary>Destructible\Stages\Stage\Model\DMDT\Model Information\Textures</summary>
        ReadOnly Property Textures As IReadOnlyList(Of IBloque_Textures)
        ''' <summary>Destructible\Stages\Stage\Model\DMDT\Model Information\Counters</summary>
        ReadOnly Property Counters As IReadOnlyList(Of IBloque_Counters)
        ''' <summary>Destructible\Stages\Stage\Model\DMDT\Model Information\Addon Nodes</summary>
        ReadOnly Property AddonNodes As IReadOnlyList(Of IBloque_AddonNodes)
        ''' <summary>Destructible\Stages\Stage\Model\DMDT\Model Information\Materials</summary>
        ReadOnly Property Materials As IReadOnlyList(Of IBloque_Materials)
    End Interface

    ''' <summary>Un bloque Stages2, venga del record que venga.
    ''' <para>Lo declaran igual: ArmoSSE_Stages, NpcSSE_Stages.</para></summary>
    Public Interface IBloque_Stages2
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Stage\DSTD\Destruction Stage Data\Health %. Distinto de que el campo valga cero.</summary>
        Property DestructionStageDataHealthPresente As Boolean
        ''' <summary>Stage\DSTD\Destruction Stage Data\Health %</summary>
        Property DestructionStageDataHealth As Byte
        ''' <summary>El record trae Stage\DSTD\Destruction Stage Data\Index. Distinto de que el campo valga cero.</summary>
        Property DestructionStageDataIndexPresente As Boolean
        ''' <summary>Stage\DSTD\Destruction Stage Data\Index</summary>
        Property DestructionStageDataIndex As Byte
        ''' <summary>El record trae Stage\DSTD\Destruction Stage Data\Model Damage Stage. Distinto de que el campo valga cero.</summary>
        Property DestructionStageDataModelDamageStagePresente As Boolean
        ''' <summary>Stage\DSTD\Destruction Stage Data\Model Damage Stage</summary>
        Property DestructionStageDataModelDamageStage As Byte
        ''' <summary>El record trae Stage\DSTD\Destruction Stage Data\Flags. Distinto de que el campo valga cero.</summary>
        Property DestructionStageDataFlagsPresente As Boolean
        ''' <summary>Stage\DSTD\Destruction Stage Data\Flags</summary>
        Property DestructionStageDataFlags As Byte
        ''' <summary>Bit 0 de Stage\DSTD\Destruction Stage Data\Flags: Cap Damage</summary>
        Property DestructionStageDataFlagsCapDamage As Boolean
        ''' <summary>Bit 1 de Stage\DSTD\Destruction Stage Data\Flags: Disable</summary>
        Property DestructionStageDataFlagsDisable As Boolean
        ''' <summary>Bit 2 de Stage\DSTD\Destruction Stage Data\Flags: Destroy</summary>
        Property DestructionStageDataFlagsDestroy As Boolean
        ''' <summary>Bit 3 de Stage\DSTD\Destruction Stage Data\Flags: Ignore External Dmg</summary>
        Property DestructionStageDataFlagsIgnoreExternalDmg As Boolean
        ''' <summary>El record trae Stage\DSTD\Destruction Stage Data\Self Damage per Second. Distinto de que el campo valga cero.</summary>
        Property DestructionStageDataSelfDamagePerSecondPresente As Boolean
        ''' <summary>Stage\DSTD\Destruction Stage Data\Self Damage per Second</summary>
        Property DestructionStageDataSelfDamagePerSecond As Integer
        ''' <summary>El record trae Stage\DSTD\Destruction Stage Data\Explosion. Distinto de que el campo valga cero.</summary>
        Property DestructionStageDataExplosionPresente As Boolean
        ''' <summary>Stage\DSTD\Destruction Stage Data\Explosion  -&gt;  EXPL / NULL. Referencia en el espacio del orden de carga.</summary>
        Property DestructionStageDataExplosion As UInteger
        ''' <summary>El record trae Stage\DSTD\Destruction Stage Data\Debris. Distinto de que el campo valga cero.</summary>
        Property DestructionStageDataDebrisPresente As Boolean
        ''' <summary>Stage\DSTD\Destruction Stage Data\Debris  -&gt;  DEBR / NULL. Referencia en el espacio del orden de carga.</summary>
        Property DestructionStageDataDebris As UInteger
        ''' <summary>El record trae Stage\DSTD\Destruction Stage Data\Debris Count. Distinto de que el campo valga cero.</summary>
        Property DestructionStageDataDebrisCountPresente As Boolean
        ''' <summary>Stage\DSTD\Destruction Stage Data\Debris Count</summary>
        Property DestructionStageDataDebrisCount As Integer
        ''' <summary>El record trae Stage\Model\DMDL\Model FileName. Distinto de que el campo valga cero.</summary>
        Property StageModelFileNamePresente As Boolean
        ''' <summary>Stage\Model\DMDL\Model FileName</summary>
        Property StageModelFileName As String
        ''' <summary>El record trae Stage\Model\DMDT\Model Information\ERROR. Distinto de que el campo valga cero.</summary>
        Property ModelInformationERRORPresente As Boolean
        ''' <summary>Stage\Model\DMDT\Model Information\ERROR</summary>
        Property ModelInformationERROR As Byte()
        ''' <summary>El record trae el marcador Stage\DSTF\End Marker. No lleva valor: el dato es que esté, así que ponerlo en verdadero lo crea y en falso lo saca.</summary>
        Property StageEndMarker As Boolean
        ''' <summary>Destructible\Stages\Stage\Model\DMDT\Model Information\Textures</summary>
        ReadOnly Property Textures As IReadOnlyList(Of IBloque_Textures)
        ''' <summary>Destructible\Stages\Stage\Model\DMDT\Model Information\Counters</summary>
        ReadOnly Property Counters As IReadOnlyList(Of IBloque_Counters)
        ''' <summary>Destructible\Stages\Stage\Model\DMDT\Model Information\Addon Nodes</summary>
        ReadOnly Property AddonNodes As IReadOnlyList(Of IBloque_AddonNodes)
        ''' <summary>Destructible\Stages\Stage\Model\DMDS\Alternate Textures</summary>
        ReadOnly Property AlternateTextures As IReadOnlyList(Of IBloque_AlternateTextures)
    End Interface

    ''' <summary>Un bloque Struct, venga del record que venga.
    ''' <para>Lo declaran igual: ArmoFO4_Struct, ArmoFO4_Struct2, NpcFO4_Struct, NpcFO4_Struct2, QustFO4_Struct, QustFO4_Struct2, QustFO4_Struct3, QustFO4_Struct4, QustFO4_Struct5, QustFO4_Struct6.</para></summary>
    Public Interface IBloque_Struct
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Member\memberName. Distinto de que el campo valga cero.</summary>
        Property MemberNamePresente As Boolean
        ''' <summary>Member\memberName</summary>
        Property MemberName As String
        ''' <summary>El record trae Member\Type. Distinto de que el campo valga cero.</summary>
        Property MemberTypePresente As Boolean
        ''' <summary>Member\Type</summary>
        Property MemberType As Byte
        ''' <summary>Nombre del valor de Member\Type.</summary>
        ReadOnly Property MemberTypeNombre As String
        ''' <summary>El record trae Member\Flags. Distinto de que el campo valga cero.</summary>
        Property MemberFlagsPresente As Boolean
        ''' <summary>Member\Flags</summary>
        Property MemberFlags As Byte
        ''' <summary>Nombre del valor de Member\Flags.</summary>
        ReadOnly Property MemberFlagsNombre As String
        ''' <summary>El record trae Member\Value\Object Union\Object v2\Alias. Distinto de que el campo valga cero.</summary>
        Property ObjectV2AliasPresente As Boolean
        ''' <summary>Member\Value\Object Union\Object v2\Alias</summary>
        Property ObjectV2Alias As Short
        ''' <summary>El record trae Member\Value\Object Union\Object v2\FormID. Distinto de que el campo valga cero.</summary>
        Property ObjectV2FormIDPresente As Boolean
        ''' <summary>Member\Value\Object Union\Object v2\FormID. Referencia en el espacio del orden de carga.</summary>
        Property ObjectV2FormID As UInteger
        ''' <summary>El record trae Member\Value\Object Union\Object v1\FormID. Distinto de que el campo valga cero.</summary>
        Property ObjectV1FormIDPresente As Boolean
        ''' <summary>Member\Value\Object Union\Object v1\FormID. Referencia en el espacio del orden de carga.</summary>
        Property ObjectV1FormID As UInteger
        ''' <summary>El record trae Member\Value\Object Union\Object v1\Alias. Distinto de que el campo valga cero.</summary>
        Property ObjectV1AliasPresente As Boolean
        ''' <summary>Member\Value\Object Union\Object v1\Alias</summary>
        Property ObjectV1Alias As Short
        ''' <summary>El record trae Member\Value\String. Distinto de que el campo valga cero.</summary>
        Property ValueStringPresente As Boolean
        ''' <summary>Member\Value\String</summary>
        Property ValueString As String
        ''' <summary>El record trae Member\Value\Int32. Distinto de que el campo valga cero.</summary>
        Property ValueInt32Presente As Boolean
        ''' <summary>Member\Value\Int32</summary>
        Property ValueInt32 As Integer
        ''' <summary>El record trae Member\Value\Float. Distinto de que el campo valga cero.</summary>
        Property ValueFloatPresente As Boolean
        ''' <summary>Member\Value\Float</summary>
        Property ValueFloat As Single
        ''' <summary>El record trae Member\Value\Bool. Distinto de que el campo valga cero.</summary>
        Property ValueBoolPresente As Boolean
        ''' <summary>Member\Value\Bool. Ponerlo en verdadero conserva el valor previo si no era cero.</summary>
        Property ValueBool As Boolean
        ''' <summary>El record trae Member\Value\Array of Variable\Element Count. Distinto de que el campo valga cero.</summary>
        Property ArrayOfVariableElementCountPresente As Boolean
        ''' <summary>Member\Value\Array of Variable\Element Count</summary>
        Property ArrayOfVariableElementCount As UInteger
        ''' <summary>VMAD\Virtual Machine Adapter\Scripts\Script\Properties\Property\Value\Struct\Member\Value\Array of Object</summary>
        ReadOnly Property ArrayOfObject As IReadOnlyList(Of IBloque_ArrayOfObject)
        ''' <summary>VMAD\Virtual Machine Adapter\Scripts\Script\Properties\Property\Value\Struct\Member\Value\Array of String</summary>
        ReadOnly Property ArrayOfString As IReadOnlyList(Of IBloque_ArrayOfString)
        ''' <summary>VMAD\Virtual Machine Adapter\Scripts\Script\Properties\Property\Value\Struct\Member\Value\Array of Int32</summary>
        ReadOnly Property ArrayOfInt32 As IReadOnlyList(Of IBloque_ArrayOfInt32)
        ''' <summary>VMAD\Virtual Machine Adapter\Scripts\Script\Properties\Property\Value\Struct\Member\Value\Array of Float</summary>
        ReadOnly Property ArrayOfFloat As IReadOnlyList(Of IBloque_ArrayOfFloat)
        ''' <summary>VMAD\Virtual Machine Adapter\Scripts\Script\Properties\Property\Value\Struct\Member\Value\Array of Bool</summary>
        ReadOnly Property ArrayOfBool As IReadOnlyList(Of IBloque_ArrayOfBool)
    End Interface

    ''' <summary>Un bloque TemplateColors, venga del record que venga.
    ''' <para>Lo declaran igual: RaceFO4_TemplateColors, RaceFO4_TemplateColors2.</para></summary>
    Public Interface IBloque_TemplateColors
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Template Color\Color. Distinto de que el campo valga cero.</summary>
        Property TemplateColorColorPresente As Boolean
        ''' <summary>Template Color\Color  -&gt;  CLFM. Referencia en el espacio del orden de carga.</summary>
        Property TemplateColorColor As UInteger
        ''' <summary>El record trae Template Color\Alpha. Distinto de que el campo valga cero.</summary>
        Property TemplateColorAlphaPresente As Boolean
        ''' <summary>Template Color\Alpha</summary>
        Property TemplateColorAlpha As Single
        ''' <summary>El record trae Template Color\Template Index. Distinto de que el campo valga cero.</summary>
        Property TemplateColorTemplateIndexPresente As Boolean
        ''' <summary>Template Color\Template Index</summary>
        Property TemplateColorTemplateIndex As UShort
        ''' <summary>El record trae Template Color\Blend Operation. Distinto de que el campo valga cero.</summary>
        Property TemplateColorBlendOperationPresente As Boolean
        ''' <summary>Template Color\Blend Operation</summary>
        Property TemplateColorBlendOperation As UInteger
    End Interface

    ''' <summary>Un bloque Textures, venga del record que venga.
    ''' <para>Lo declaran igual: ArmaFO4_Textures, ArmaFO4_Textures2, ArmaFO4_Textures3, ArmaFO4_Textures4, ArmaSSE_Textures, ArmaSSE_Textures2, ArmaSSE_Textures3, ArmaSSE_Textures4, ArmoFO4_Textures, ArmoFO4_Textures2, ArmoFO4_Textures3, ArmoSSE_Textures, ArmoSSE_Textures2, ArmoSSE_Textures3, BptdFO4_Textures, BptdFO4_Textures2, BptdSSE_Textures, BptdSSE_Textures2, HdptFO4_Textures, HdptSSE_Textures, LvlnFO4_Textures, LvlnSSE_Textures, NpcFO4_Textures, NpcSSE_Textures, OmodFO4_Textures, RaceFO4_Textures, RaceFO4_Textures2, RaceFO4_Textures3, RaceFO4_Textures4, RaceFO4_Textures5, RaceSSE_Textures, RaceSSE_Textures2, RaceSSE_Textures3, RaceSSE_Textures4, RaceSSE_Textures5, RaceSSE_Textures6, RaceSSE_Textures7.</para></summary>
    Public Interface IBloque_Textures
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Texture\File Hash. Distinto de que el campo valga cero.</summary>
        Property TextureFileHashPresente As Boolean
        ''' <summary>Texture\File Hash</summary>
        Property TextureFileHash As UInteger
        ''' <summary>El record trae Texture\Extension. Distinto de que el campo valga cero.</summary>
        Property TextureExtensionPresente As Boolean
        ''' <summary>Texture\Extension</summary>
        Property TextureExtension As String
        ''' <summary>El record trae Texture\Folder Hash. Distinto de que el campo valga cero.</summary>
        Property TextureFolderHashPresente As Boolean
        ''' <summary>Texture\Folder Hash</summary>
        Property TextureFolderHash As UInteger
    End Interface

    ''' <summary>Un bloque Textures2, venga del record que venga.
    ''' <para>Lo declaran igual: RaceFO4_Textures6, RaceFO4_Textures7.</para></summary>
    Public Interface IBloque_Textures2
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae TTET\Texture. Distinto de que el campo valga cero.</summary>
        Property TexturePresente As Boolean
        ''' <summary>TTET\Texture</summary>
        Property Texture As String
    End Interface

    ''' <summary>Un bloque TintLayers, venga del record que venga.
    ''' <para>Lo declaran igual: RaceFO4_FemaleTintLayers, RaceFO4_MaleTintLayers.</para></summary>
    Public Interface IBloque_TintLayers
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Group\TTGP\Group Name. Distinto de que el campo valga cero.</summary>
        Property GroupNamePresente As Boolean
        ''' <summary>Group\TTGP\Group Name. Resuelto contra las tablas de texto si el archivo las usa.</summary>
        Property GroupName As String
        ''' <summary>El record trae Group\TTGE\Category Index. Distinto de que el campo valga cero.</summary>
        Property GroupCategoryIndexPresente As Boolean
        ''' <summary>Group\TTGE\Category Index</summary>
        Property GroupCategoryIndex As UInteger
        ''' <summary>Female Tint Layers\Group\Options</summary>
        ReadOnly Property Options As IReadOnlyList(Of IBloque_Options)
    End Interface

    ''' <summary>Un bloque TintMasks, venga del record que venga.
    ''' <para>Lo declaran igual: RaceSSE_TintMasks, RaceSSE_TintMasks2.</para></summary>
    Public Interface IBloque_TintMasks
        ReadOnly Property Node As WbNode
        ''' <summary>El record trae Tint Assets\Tint Layer\TINI\Index. Distinto de que el campo valga cero.</summary>
        Property TintLayerIndexPresente As Boolean
        ''' <summary>Tint Assets\Tint Layer\TINI\Index</summary>
        Property TintLayerIndex As UShort
        ''' <summary>El record trae Tint Assets\Tint Layer\TINT\File Name. Distinto de que el campo valga cero.</summary>
        Property TintLayerFileNamePresente As Boolean
        ''' <summary>Tint Assets\Tint Layer\TINT\File Name</summary>
        Property TintLayerFileName As String
        ''' <summary>El record trae Tint Assets\Tint Layer\TINP\Mask Type. Distinto de que el campo valga cero.</summary>
        Property TintLayerMaskTypePresente As Boolean
        ''' <summary>Tint Assets\Tint Layer\TINP\Mask Type</summary>
        Property TintLayerMaskType As UShort
        ''' <summary>Nombre del valor de Tint Assets\Tint Layer\TINP\Mask Type.</summary>
        ReadOnly Property TintLayerMaskTypeNombre As String
        ''' <summary>El record trae Tint Assets\Tint Layer\TIND\Preset Default. Distinto de que el campo valga cero.</summary>
        Property TintLayerPresetDefaultPresente As Boolean
        ''' <summary>Tint Assets\Tint Layer\TIND\Preset Default  -&gt;  CLFM / NULL. Referencia en el espacio del orden de carga.</summary>
        Property TintLayerPresetDefault As UInteger
        ''' <summary>Head Data\Male Head Data\Tint Masks\Tint Assets\Presets</summary>
        ReadOnly Property Presets As IReadOnlyList(Of IBloque_Presets)
    End Interface

End Namespace
