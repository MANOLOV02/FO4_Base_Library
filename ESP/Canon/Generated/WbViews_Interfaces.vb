' ============================================================================================
' ARCHIVO GENERADO — NO EDITAR A MANO.  Regenerar: Tools/CanonViewGen
'
' Interfaces: lo que los dos juegos declaran igual.
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
        ''' <summary>EDID\Editor ID</summary>
        Property EditorID As String
        ''' <summary>BOD2\Biped Body Template\First Person Flags</summary>
        Property BipedBodyTemplateFirstPersonFlags As UInteger
        ''' <summary>RNAM\Race  -&gt;  RACE. Referencia en el espacio del orden de carga.</summary>
        Property Race As UInteger
        ''' <summary>DNAM\Data\Male Priority</summary>
        Property DataMalePriority As Byte
        ''' <summary>DNAM\Data\Female Priority</summary>
        Property DataFemalePriority As Byte
        ''' <summary>DNAM\Data\Weight slider - Male</summary>
        Property DataWeightSliderMale As Byte
        ''' <summary>Bit 1 de DNAM\Data\Weight slider - Male: Enabled</summary>
        Property DataWeightSliderMaleEnabled As Boolean
        ''' <summary>DNAM\Data\Weight slider - Female</summary>
        Property DataWeightSliderFemale As Byte
        ''' <summary>Bit 1 de DNAM\Data\Weight slider - Female: Enabled</summary>
        Property DataWeightSliderFemaleEnabled As Boolean
        ''' <summary>DNAM\Data\Detection Sound Value</summary>
        Property DataDetectionSoundValue As Byte
        ''' <summary>DNAM\Data\Weapon Adjust</summary>
        Property DataWeaponAdjust As Single
        ''' <summary>Biped Model\Male\MOD2\Model Filename</summary>
        Property MaleModelFilename As String
        ''' <summary>Biped Model\Female\MOD3\Model Filename</summary>
        Property FemaleModelFilename As String
        ''' <summary>1st Person\Male\MOD4\Model Filename</summary>
        Property MaleModelFilename2 As String
        ''' <summary>1st Person\Female\MOD5\Model Filename</summary>
        Property FemaleModelFilename2 As String
        ''' <summary>NAM0\Male Skin Texture  -&gt;  TXST / NULL. Referencia en el espacio del orden de carga.</summary>
        Property MaleSkinTexture As UInteger
        ''' <summary>NAM1\Female Skin Texture  -&gt;  TXST / NULL. Referencia en el espacio del orden de carga.</summary>
        Property FemaleSkinTexture As UInteger
        ''' <summary>NAM2\Male Skin Texture Swap List  -&gt;  FLST / NULL. Referencia en el espacio del orden de carga.</summary>
        Property MaleSkinTextureSwapList As UInteger
        ''' <summary>NAM3\Female Skin Texture Swap List  -&gt;  FLST / NULL. Referencia en el espacio del orden de carga.</summary>
        Property FemaleSkinTextureSwapList As UInteger
        ''' <summary>SNDD\Footstep Sound  -&gt;  FSTS / NULL. Referencia en el espacio del orden de carga.</summary>
        Property FootstepSound As UInteger
        ''' <summary>ONAM\Art Object  -&gt;  ARTO. Referencia en el espacio del orden de carga.</summary>
        Property ArtObject As UInteger
        ''' <summary>Additional Races</summary>
        ReadOnly Property AdditionalRaces As IReadOnlyList(Of IArma_AdditionalRaces)
    End Interface

    ''' <summary>Un elemento de Additional Races, en lo que los dos juegos comparten.</summary>
    Public Interface IArma_AdditionalRaces
        ReadOnly Property Node As WbNode
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
        ''' <summary>EDID\Editor ID</summary>
        Property EditorID As String
        ''' <summary>VMAD\Virtual Machine Adapter\Version</summary>
        Property VirtualMachineAdapterVersion As Short
        ''' <summary>VMAD\Virtual Machine Adapter\Object Format</summary>
        Property VirtualMachineAdapterObjectFormat As Short
        ''' <summary>OBND\Object Bounds\X1</summary>
        Property ObjectBoundsX1 As Short
        ''' <summary>OBND\Object Bounds\Y1</summary>
        Property ObjectBoundsY1 As Short
        ''' <summary>OBND\Object Bounds\Z1</summary>
        Property ObjectBoundsZ1 As Short
        ''' <summary>OBND\Object Bounds\X2</summary>
        Property ObjectBoundsX2 As Short
        ''' <summary>OBND\Object Bounds\Y2</summary>
        Property ObjectBoundsY2 As Short
        ''' <summary>OBND\Object Bounds\Z2</summary>
        Property ObjectBoundsZ2 As Short
        ''' <summary>FULL\Name. Resuelto contra las tablas de texto si el archivo las usa.</summary>
        Property Name As String
        ''' <summary>EITM\Enchantment  -&gt;  ENCH. Referencia en el espacio del orden de carga.</summary>
        Property Enchantment As UInteger
        ''' <summary>Male\World Model\MOD2\Model Filename</summary>
        Property WorldModelModelFilename As String
        ''' <summary>Male\ICON\Icon Image</summary>
        Property MaleIconImage As String
        ''' <summary>Male\MICO\Message Icon</summary>
        Property MaleMessageIcon As String
        ''' <summary>Female\World Model\MOD4\Model Filename</summary>
        Property WorldModelModelFilename2 As String
        ''' <summary>Female\ICO2\Icon Image</summary>
        Property FemaleIconImage As String
        ''' <summary>Female\MIC2\Message Icon</summary>
        Property FemaleMessageIcon As String
        ''' <summary>BOD2\Biped Body Template\First Person Flags</summary>
        Property BipedBodyTemplateFirstPersonFlags As UInteger
        ''' <summary>Destructible\DEST\Header\Health</summary>
        Property HeaderHealth As Integer
        ''' <summary>Destructible\DEST\Header\DEST Count</summary>
        Property HeaderDESTCount As Byte
        ''' <summary>YNAM\Sound - Pick Up  -&gt;  SNDR. Referencia en el espacio del orden de carga.</summary>
        Property SoundPickUp As UInteger
        ''' <summary>ZNAM\Sound - Put Down  -&gt;  SNDR. Referencia en el espacio del orden de carga.</summary>
        Property SoundPutDown As UInteger
        ''' <summary>ETYP\Equipment Type  -&gt;  EQUP / NULL. Referencia en el espacio del orden de carga.</summary>
        Property EquipmentType As UInteger
        ''' <summary>BAMT\Alternate Block Material  -&gt;  MATT / NULL. Referencia en el espacio del orden de carga.</summary>
        Property AlternateBlockMaterial As UInteger
        ''' <summary>RNAM\Race  -&gt;  RACE. Referencia en el espacio del orden de carga.</summary>
        Property Race As UInteger
        ''' <summary>Keywords\KSIZ\Keyword Count</summary>
        Property KeywordsKeywordCount As UInteger
        ''' <summary>DESC\Description. Resuelto contra las tablas de texto si el archivo las usa.</summary>
        Property Description As String
        ''' <summary>TNAM\Template Armor  -&gt;  ARMO. Referencia en el espacio del orden de carga.</summary>
        Property TemplateArmor As UInteger
        ''' <summary>VMAD\Virtual Machine Adapter\Scripts</summary>
        ReadOnly Property Scripts As IReadOnlyList(Of IArmo_Scripts)
        ''' <summary>Destructible\Stages</summary>
        ReadOnly Property Stages As IReadOnlyList(Of IArmo_Stages)
        ''' <summary>Keywords\KWDA\Keywords</summary>
        ReadOnly Property Keywords As IReadOnlyList(Of IArmo_Keywords)
    End Interface

    ''' <summary>Un elemento de VMAD\Virtual Machine Adapter\Scripts, en lo que los dos juegos comparten.</summary>
    Public Interface IArmo_Scripts
        ReadOnly Property Node As WbNode
        ''' <summary>Script\ScriptName</summary>
        Property ScriptScriptName As String
        ''' <summary>Script\Flags</summary>
        Property ScriptFlags As Byte
        ''' <summary>Nombre del valor de Script\Flags.</summary>
        ReadOnly Property ScriptFlagsNombre As String
    End Interface

    ''' <summary>Un elemento de Destructible\Stages, en lo que los dos juegos comparten.</summary>
    Public Interface IArmo_Stages
        ReadOnly Property Node As WbNode
        ''' <summary>Stage\DSTD\Destruction Stage Data\Health %</summary>
        Property DestructionStageDataHealth As Byte
        ''' <summary>Stage\DSTD\Destruction Stage Data\Index</summary>
        Property DestructionStageDataIndex As Byte
        ''' <summary>Stage\DSTD\Destruction Stage Data\Model Damage Stage</summary>
        Property DestructionStageDataModelDamageStage As Byte
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
        ''' <summary>Stage\DSTD\Destruction Stage Data\Self Damage per Second</summary>
        Property DestructionStageDataSelfDamagePerSecond As Integer
        ''' <summary>Stage\DSTD\Destruction Stage Data\Explosion  -&gt;  EXPL / NULL. Referencia en el espacio del orden de carga.</summary>
        Property DestructionStageDataExplosion As UInteger
        ''' <summary>Stage\DSTD\Destruction Stage Data\Debris  -&gt;  DEBR / NULL. Referencia en el espacio del orden de carga.</summary>
        Property DestructionStageDataDebris As UInteger
        ''' <summary>Stage\DSTD\Destruction Stage Data\Debris Count</summary>
        Property DestructionStageDataDebrisCount As Integer
        ''' <summary>Stage\Model\DMDL\Model FileName</summary>
        Property ModelModelFileName As String
    End Interface

    ''' <summary>Un elemento de Keywords\KWDA\Keywords, en lo que los dos juegos comparten.</summary>
    Public Interface IArmo_Keywords
        ReadOnly Property Node As WbNode
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
        ''' <summary>EDID\Editor ID</summary>
        Property EditorID As String
        ''' <summary>Model\MODL\Model FileName</summary>
        Property ModelModelFileName As String
        ''' <summary>Body Parts</summary>
        ReadOnly Property BodyParts As IReadOnlyList(Of IBptd_BodyParts)
    End Interface

    ''' <summary>Un elemento de Body Parts, en lo que los dos juegos comparten.</summary>
    Public Interface IBptd_BodyParts
        ReadOnly Property Node As WbNode
        ''' <summary>Body Part\BPTN\Part Name. Resuelto contra las tablas de texto si el archivo las usa.</summary>
        Property BodyPartPartName As String
        ''' <summary>Body Part\BPNN\Part Node</summary>
        Property BodyPartPartNode As String
        ''' <summary>Body Part\BPNT\VATS Target</summary>
        Property BodyPartVATSTarget As String
        ''' <summary>Body Part\BPND\Node Data\Damage Mult</summary>
        Property NodeDataDamageMult As Single
        ''' <summary>Body Part\BPND\Node Data\Explodable - Debris  -&gt;  DEBR / NULL. Referencia en el espacio del orden de carga.</summary>
        Property NodeDataExplodableDebris As UInteger
        ''' <summary>Body Part\BPND\Node Data\Explodable - Explosion  -&gt;  EXPL / NULL. Referencia en el espacio del orden de carga.</summary>
        Property NodeDataExplodableExplosion As UInteger
        ''' <summary>Body Part\BPND\Node Data\Explodable - Debris Scale</summary>
        Property NodeDataExplodableDebrisScale As Single
        ''' <summary>Body Part\BPND\Node Data\Severable - Debris  -&gt;  DEBR / NULL. Referencia en el espacio del orden de carga.</summary>
        Property NodeDataSeverableDebris As UInteger
        ''' <summary>Body Part\BPND\Node Data\Severable - Explosion  -&gt;  EXPL / NULL. Referencia en el espacio del orden de carga.</summary>
        Property NodeDataSeverableExplosion As UInteger
        ''' <summary>Body Part\BPND\Node Data\Severable - Debris Scale</summary>
        Property NodeDataSeverableDebrisScale As Single
        ''' <summary>Body Part\BPND\Node Data\Severable - Impact DataSet  -&gt;  IPDS / NULL. Referencia en el espacio del orden de carga.</summary>
        Property NodeDataSeverableImpactDataSet As UInteger
        ''' <summary>Body Part\BPND\Node Data\Explodable - Impact DataSet  -&gt;  IPDS / NULL. Referencia en el espacio del orden de carga.</summary>
        Property NodeDataExplodableImpactDataSet As UInteger
        ''' <summary>Body Part\BPND\Node Data\Flags</summary>
        Property NodeDataFlags As Byte
        ''' <summary>Bit 0 de Body Part\BPND\Node Data\Flags: Severable</summary>
        Property NodeDataFlagsSeverable As Boolean
        ''' <summary>Bit 3 de Body Part\BPND\Node Data\Flags: Explodable</summary>
        Property NodeDataFlagsExplodable As Boolean
        ''' <summary>Body Part\BPND\Node Data\Part Type</summary>
        Property NodeDataPartType As Byte
        ''' <summary>Nombre del valor de Body Part\BPND\Node Data\Part Type.</summary>
        ReadOnly Property NodeDataPartTypeNombre As String
        ''' <summary>Body Part\BPND\Node Data\Health Percent</summary>
        Property NodeDataHealthPercent As Byte
        ''' <summary>Body Part\BPND\Node Data\To Hit Chance</summary>
        Property NodeDataToHitChance As Byte
        ''' <summary>Body Part\BPND\Node Data\Explodable - Explosion Chance %</summary>
        Property NodeDataExplodableExplosionChance As Byte
        ''' <summary>Body Part\BPND\Node Data\Severable - Decal Count</summary>
        Property NodeDataSeverableDecalCount As Byte
        ''' <summary>Body Part\BPND\Node Data\Explodable - Decal Count</summary>
        Property NodeDataExplodableDecalCount As Byte
        ''' <summary>Body Part\NAM1\Limb Replacement Model</summary>
        Property BodyPartLimbReplacementModel As String
        ''' <summary>Body Part\NAM4\Gore Effects - Target Bone</summary>
        Property BodyPartGoreEffectsTargetBone As String
    End Interface

    ''' <summary>Campos de un record CLFM que los dos juegos declaran igual.
    ''' <para>El codigo que solo usa estos no necesita saber de que juego se trata. El
    ''' que necesita algo propio de un juego tiene que nombrar la clase de ese juego, y
    ''' asi no puede leer sin querer un campo que alla significa otra cosa.</para></summary>
    Public Interface IClfm
        ReadOnly Property Node As WbNode
        ''' <summary>Identificador del record.</summary>
        ReadOnly Property FormID As UInteger
        ''' <summary>EDID\Editor ID</summary>
        Property EditorID As String
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
        ''' <summary>EDID\Editor ID</summary>
        Property EditorID As String
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
        ''' <summary>EDID\Editor ID</summary>
        Property EditorID As String
        ''' <summary>FormIDs</summary>
        ReadOnly Property FormIDs As IReadOnlyList(Of IFlst_FormIDs)
    End Interface

    ''' <summary>Un elemento de FormIDs, en lo que los dos juegos comparten.</summary>
    Public Interface IFlst_FormIDs
        ReadOnly Property Node As WbNode
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
        ''' <summary>EDID\Editor ID</summary>
        Property EditorID As String
        ''' <summary>FULL\Name. Resuelto contra las tablas de texto si el archivo las usa.</summary>
        Property Name As String
        ''' <summary>Model\MODL\Model FileName</summary>
        Property ModelModelFileName As String
        ''' <summary>DATA\Flags</summary>
        Property Flags As Byte
        ''' <summary>PNAM\Type</summary>
        Property Type As UInteger
        ''' <summary>TNAM\Texture Set  -&gt;  TXST. Referencia en el espacio del orden de carga.</summary>
        Property TextureSet As UInteger
        ''' <summary>CNAM\Color  -&gt;  CLFM. Referencia en el espacio del orden de carga.</summary>
        Property Color As UInteger
        ''' <summary>RNAM\Valid Races  -&gt;  FLST. Referencia en el espacio del orden de carga.</summary>
        Property ValidRaces As UInteger
        ''' <summary>Extra Parts</summary>
        ReadOnly Property ExtraParts As IReadOnlyList(Of IHdpt_ExtraParts)
        ''' <summary>Parts</summary>
        ReadOnly Property Parts As IReadOnlyList(Of IHdpt_Parts)
    End Interface

    ''' <summary>Un elemento de Extra Parts, en lo que los dos juegos comparten.</summary>
    Public Interface IHdpt_ExtraParts
        ReadOnly Property Node As WbNode
        ''' <summary>HNAM\Part  -&gt;  HDPT. Referencia en el espacio del orden de carga.</summary>
        Property Part As UInteger
    End Interface

    ''' <summary>Un elemento de Parts, en lo que los dos juegos comparten.</summary>
    Public Interface IHdpt_Parts
        ReadOnly Property Node As WbNode
        ''' <summary>Part\NAM0\Part Type</summary>
        Property PartPartType As UInteger
        ''' <summary>Part\NAM1\FileName</summary>
        Property PartFileName As String
    End Interface

    ''' <summary>Campos de un record LVLI que los dos juegos declaran igual.
    ''' <para>El codigo que solo usa estos no necesita saber de que juego se trata. El
    ''' que necesita algo propio de un juego tiene que nombrar la clase de ese juego, y
    ''' asi no puede leer sin querer un campo que alla significa otra cosa.</para></summary>
    Public Interface ILvli
        ReadOnly Property Node As WbNode
        ''' <summary>Identificador del record.</summary>
        ReadOnly Property FormID As UInteger
        ''' <summary>EDID\Editor ID</summary>
        Property EditorID As String
        ''' <summary>OBND\Object Bounds\X1</summary>
        Property ObjectBoundsX1 As Short
        ''' <summary>OBND\Object Bounds\Y1</summary>
        Property ObjectBoundsY1 As Short
        ''' <summary>OBND\Object Bounds\Z1</summary>
        Property ObjectBoundsZ1 As Short
        ''' <summary>OBND\Object Bounds\X2</summary>
        Property ObjectBoundsX2 As Short
        ''' <summary>OBND\Object Bounds\Y2</summary>
        Property ObjectBoundsY2 As Short
        ''' <summary>OBND\Object Bounds\Z2</summary>
        Property ObjectBoundsZ2 As Short
        ''' <summary>LVLD\Chance None</summary>
        Property ChanceNone As Byte
        ''' <summary>LVLF\Flags</summary>
        Property Flags As Byte
        ''' <summary>LLCT\Count</summary>
        Property Count As Byte
        ''' <summary>Leveled List Entries</summary>
        ReadOnly Property LeveledListEntries As IReadOnlyList(Of ILvli_LeveledListEntries)
    End Interface

    ''' <summary>Un elemento de Leveled List Entries, en lo que los dos juegos comparten.</summary>
    Public Interface ILvli_LeveledListEntries
        ReadOnly Property Node As WbNode
        ''' <summary>Leveled List Entry\LVLO\Level</summary>
        Property LeveledListEntryLevel As UShort
        ''' <summary>Leveled List Entry\LVLO\Item. Referencia en el espacio del orden de carga.</summary>
        Property LeveledListEntryItem As UInteger
        ''' <summary>Leveled List Entry\LVLO\Count</summary>
        Property LeveledListEntryCount As UShort
        ''' <summary>Leveled List Entry\COED\Extra Data\Owner  -&gt;  NPC_ / FACT / NULL. Referencia en el espacio del orden de carga.</summary>
        Property ExtraDataOwner As UInteger
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
        ''' <summary>EDID\Editor ID</summary>
        Property EditorID As String
        ''' <summary>OBND\Object Bounds\X1</summary>
        Property ObjectBoundsX1 As Short
        ''' <summary>OBND\Object Bounds\Y1</summary>
        Property ObjectBoundsY1 As Short
        ''' <summary>OBND\Object Bounds\Z1</summary>
        Property ObjectBoundsZ1 As Short
        ''' <summary>OBND\Object Bounds\X2</summary>
        Property ObjectBoundsX2 As Short
        ''' <summary>OBND\Object Bounds\Y2</summary>
        Property ObjectBoundsY2 As Short
        ''' <summary>OBND\Object Bounds\Z2</summary>
        Property ObjectBoundsZ2 As Short
        ''' <summary>LVLD\Chance None</summary>
        Property ChanceNone As Byte
        ''' <summary>LVLF\Flags</summary>
        Property Flags As Byte
        ''' <summary>LLCT\Count</summary>
        Property Count As Byte
        ''' <summary>Model\MODL\Model FileName</summary>
        Property ModelModelFileName As String
        ''' <summary>Leveled List Entries</summary>
        ReadOnly Property LeveledListEntries As IReadOnlyList(Of ILvln_LeveledListEntries)
    End Interface

    ''' <summary>Un elemento de Leveled List Entries, en lo que los dos juegos comparten.</summary>
    Public Interface ILvln_LeveledListEntries
        ReadOnly Property Node As WbNode
        ''' <summary>Leveled List Entry\LVLO\Level</summary>
        Property LeveledListEntryLevel As UShort
        ''' <summary>Leveled List Entry\LVLO\NPC  -&gt;  LVLN / NPC_. Referencia en el espacio del orden de carga.</summary>
        Property LeveledListEntryNPC As UInteger
        ''' <summary>Leveled List Entry\LVLO\Count</summary>
        Property LeveledListEntryCount As UShort
        ''' <summary>Leveled List Entry\COED\Extra Data\Owner  -&gt;  NPC_ / FACT / NULL. Referencia en el espacio del orden de carga.</summary>
        Property ExtraDataOwner As UInteger
        ''' <summary>Leveled List Entry\COED\Extra Data\Item Condition</summary>
        Property ExtraDataItemCondition As Single
    End Interface

    ''' <summary>Campos de un record MSWP que los dos juegos declaran igual.
    ''' <para>El codigo que solo usa estos no necesita saber de que juego se trata. El
    ''' que necesita algo propio de un juego tiene que nombrar la clase de ese juego, y
    ''' asi no puede leer sin querer un campo que alla significa otra cosa.</para></summary>
    Public Interface IMswp
        ReadOnly Property Node As WbNode
        ''' <summary>Identificador del record.</summary>
        ReadOnly Property FormID As UInteger
        ''' <summary>EDID\Editor ID</summary>
        Property EditorID As String
        ''' <summary>FNAM\Tree Folder</summary>
        Property TreeFolder As String
        ''' <summary>Material Substitutions</summary>
        ReadOnly Property MaterialSubstitutions As IReadOnlyList(Of IMswp_MaterialSubstitutions)
    End Interface

    ''' <summary>Un elemento de Material Substitutions, en lo que los dos juegos comparten.</summary>
    Public Interface IMswp_MaterialSubstitutions
        ReadOnly Property Node As WbNode
        ''' <summary>Substitution\BNAM\Original Material</summary>
        Property SubstitutionOriginalMaterial As String
        ''' <summary>Substitution\SNAM\Replacement Material</summary>
        Property SubstitutionReplacementMaterial As String
        ''' <summary>Substitution\FNAM\Tree Folder (obsolete)</summary>
        Property SubstitutionTreeFolderObsolete As String
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
        ''' <summary>EDID\Editor ID</summary>
        Property EditorID As String
        ''' <summary>VMAD\Virtual Machine Adapter\Version</summary>
        Property VirtualMachineAdapterVersion As Short
        ''' <summary>VMAD\Virtual Machine Adapter\Object Format</summary>
        Property VirtualMachineAdapterObjectFormat As Short
        ''' <summary>OBND\Object Bounds\X1</summary>
        Property ObjectBoundsX1 As Short
        ''' <summary>OBND\Object Bounds\Y1</summary>
        Property ObjectBoundsY1 As Short
        ''' <summary>OBND\Object Bounds\Z1</summary>
        Property ObjectBoundsZ1 As Short
        ''' <summary>OBND\Object Bounds\X2</summary>
        Property ObjectBoundsX2 As Short
        ''' <summary>OBND\Object Bounds\Y2</summary>
        Property ObjectBoundsY2 As Short
        ''' <summary>OBND\Object Bounds\Z2</summary>
        Property ObjectBoundsZ2 As Short
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
        ''' <summary>ACBS\Configuration\Level</summary>
        Property ConfigurationLevel As UShort
        ''' <summary>ACBS\Configuration\Calc min level</summary>
        Property ConfigurationCalcMinLevel As UShort
        ''' <summary>ACBS\Configuration\Calc max level</summary>
        Property ConfigurationCalcMaxLevel As UShort
        ''' <summary>ACBS\Configuration\Template Flags</summary>
        Property ConfigurationTemplateFlags As UShort
        ''' <summary>ACBS\Configuration\Bleedout Override</summary>
        Property ConfigurationBleedoutOverride As UShort
        ''' <summary>INAM\Death item  -&gt;  LVLI. Referencia en el espacio del orden de carga.</summary>
        Property DeathItem As UInteger
        ''' <summary>VTCK\Voice  -&gt;  VTYP. Referencia en el espacio del orden de carga.</summary>
        Property Voice As UInteger
        ''' <summary>RNAM\Race  -&gt;  RACE. Referencia en el espacio del orden de carga.</summary>
        Property Race As UInteger
        ''' <summary>SPCT\Count</summary>
        Property Count As UInteger
        ''' <summary>Destructible\DEST\Header\Health</summary>
        Property HeaderHealth As Integer
        ''' <summary>Destructible\DEST\Header\DEST Count</summary>
        Property HeaderDESTCount As Byte
        ''' <summary>WNAM\Skin  -&gt;  ARMO. Referencia en el espacio del orden de carga.</summary>
        Property Skin As UInteger
        ''' <summary>ANAM\Far away model  -&gt;  ARMO. Referencia en el espacio del orden de carga.</summary>
        Property FarAwayModel As UInteger
        ''' <summary>ATKR\Attack Race  -&gt;  RACE. Referencia en el espacio del orden de carga.</summary>
        Property AttackRace As UInteger
        ''' <summary>SPOR\Spectator Override Package List  -&gt;  FLST. Referencia en el espacio del orden de carga.</summary>
        Property SpectatorOverridePackageList As UInteger
        ''' <summary>OCOR\Observe Dead Body Override Package List  -&gt;  FLST. Referencia en el espacio del orden de carga.</summary>
        Property ObserveDeadBodyOverridePackageList As UInteger
        ''' <summary>GWOR\Guard Warn Override Package List  -&gt;  FLST. Referencia en el espacio del orden de carga.</summary>
        Property GuardWarnOverridePackageList As UInteger
        ''' <summary>ECOR\Combat Override Package List  -&gt;  FLST. Referencia en el espacio del orden de carga.</summary>
        Property CombatOverridePackageList As UInteger
        ''' <summary>PRKZ\Perk Count</summary>
        Property PerkCount As UInteger
        ''' <summary>COCT\Count</summary>
        Property Count2 As UInteger
        ''' <summary>AIDT\AI Data\Aggression</summary>
        Property AIDataAggression As Byte
        ''' <summary>AIDT\AI Data\Confidence</summary>
        Property AIDataConfidence As Byte
        ''' <summary>AIDT\AI Data\Energy Level</summary>
        Property AIDataEnergyLevel As Byte
        ''' <summary>AIDT\AI Data\Morality</summary>
        Property AIDataMorality As Byte
        ''' <summary>AIDT\AI Data\Mood</summary>
        Property AIDataMood As Byte
        ''' <summary>AIDT\AI Data\Assistance</summary>
        Property AIDataAssistance As Byte
        ''' <summary>AIDT\AI Data\Aggro\Aggro Radius Behavior</summary>
        Property AggroAggroRadiusBehavior As Byte
        ''' <summary>AIDT\AI Data\Aggro\Warn</summary>
        Property AggroWarn As UInteger
        ''' <summary>AIDT\AI Data\Aggro\Warn/Attack</summary>
        Property AggroWarnAttack As UInteger
        ''' <summary>AIDT\AI Data\Aggro\Attack</summary>
        Property AggroAttack As UInteger
        ''' <summary>Keywords\KSIZ\Keyword Count</summary>
        Property KeywordsKeywordCount As UInteger
        ''' <summary>CNAM\Class  -&gt;  CLAS. Referencia en el espacio del orden de carga.</summary>
        Property [Class] As UInteger
        ''' <summary>FULL\Name. Resuelto contra las tablas de texto si el archivo las usa.</summary>
        Property Name As String
        ''' <summary>SHRT\Short Name. Resuelto contra las tablas de texto si el archivo las usa.</summary>
        Property ShortName As String
        ''' <summary>HCLF\Hair Color  -&gt;  CLFM. Referencia en el espacio del orden de carga.</summary>
        Property HairColor As UInteger
        ''' <summary>ZNAM\Combat Style  -&gt;  CSTY. Referencia en el espacio del orden de carga.</summary>
        Property CombatStyle As UInteger
        ''' <summary>GNAM\Gift Filter  -&gt;  FLST. Referencia en el espacio del orden de carga.</summary>
        Property GiftFilter As UInteger
        ''' <summary>NAM8\Sound Level</summary>
        Property SoundLevel As UInteger
        ''' <summary>CSCR\Inherits Sounds From  -&gt;  NPC_. Referencia en el espacio del orden de carga.</summary>
        Property InheritsSoundsFrom As UInteger
        ''' <summary>DOFT\Default Outfit  -&gt;  OTFT. Referencia en el espacio del orden de carga.</summary>
        Property DefaultOutfit As UInteger
        ''' <summary>SOFT\Sleeping Outfit  -&gt;  OTFT. Referencia en el espacio del orden de carga.</summary>
        Property SleepingOutfit As UInteger
        ''' <summary>DPLT\Default Package List  -&gt;  FLST. Referencia en el espacio del orden de carga.</summary>
        Property DefaultPackageList As UInteger
        ''' <summary>CRIF\Crime Faction  -&gt;  FACT. Referencia en el espacio del orden de carga.</summary>
        Property CrimeFaction As UInteger
        ''' <summary>FTST\Head Texture  -&gt;  TXST. Referencia en el espacio del orden de carga.</summary>
        Property HeadTexture As UInteger
        ''' <summary>QNAM\Texture lighting\Red</summary>
        Property TextureLightingRed As Single
        ''' <summary>QNAM\Texture lighting\Green</summary>
        Property TextureLightingGreen As Single
        ''' <summary>QNAM\Texture lighting\Blue</summary>
        Property TextureLightingBlue As Single
        ''' <summary>VMAD\Virtual Machine Adapter\Scripts</summary>
        ReadOnly Property Scripts As IReadOnlyList(Of INpc_Scripts)
        ''' <summary>Factions</summary>
        ReadOnly Property Factions As IReadOnlyList(Of INpc_Factions)
        ''' <summary>Actor Effects</summary>
        ReadOnly Property ActorEffects As IReadOnlyList(Of INpc_ActorEffects)
        ''' <summary>Destructible\Stages</summary>
        ReadOnly Property Stages As IReadOnlyList(Of INpc_Stages)
        ''' <summary>Attacks</summary>
        ReadOnly Property Attacks As IReadOnlyList(Of INpc_Attacks)
        ''' <summary>Perks</summary>
        ReadOnly Property Perks As IReadOnlyList(Of INpc_Perks)
        ''' <summary>Items</summary>
        ReadOnly Property Items As IReadOnlyList(Of INpc_Items)
        ''' <summary>Packages</summary>
        ReadOnly Property Packages As IReadOnlyList(Of INpc_Packages)
        ''' <summary>Keywords\KWDA\Keywords</summary>
        ReadOnly Property Keywords As IReadOnlyList(Of INpc_Keywords)
        ''' <summary>Head Parts</summary>
        ReadOnly Property HeadParts As IReadOnlyList(Of INpc_HeadParts)
    End Interface

    ''' <summary>Un elemento de VMAD\Virtual Machine Adapter\Scripts, en lo que los dos juegos comparten.</summary>
    Public Interface INpc_Scripts
        ReadOnly Property Node As WbNode
        ''' <summary>Script\ScriptName</summary>
        Property ScriptScriptName As String
        ''' <summary>Script\Flags</summary>
        Property ScriptFlags As Byte
        ''' <summary>Nombre del valor de Script\Flags.</summary>
        ReadOnly Property ScriptFlagsNombre As String
    End Interface

    ''' <summary>Un elemento de Factions, en lo que los dos juegos comparten.</summary>
    Public Interface INpc_Factions
        ReadOnly Property Node As WbNode
        ''' <summary>SNAM\Faction\Faction  -&gt;  FACT. Referencia en el espacio del orden de carga.</summary>
        Property FactionFaction As UInteger
        ''' <summary>SNAM\Faction\Rank</summary>
        Property FactionRank As SByte
    End Interface

    ''' <summary>Un elemento de Actor Effects, en lo que los dos juegos comparten.</summary>
    Public Interface INpc_ActorEffects
        ReadOnly Property Node As WbNode
        ''' <summary>SPLO\Actor Effect  -&gt;  SPEL / LVSP. Referencia en el espacio del orden de carga.</summary>
        Property ActorEffect As UInteger
    End Interface

    ''' <summary>Un elemento de Destructible\Stages, en lo que los dos juegos comparten.</summary>
    Public Interface INpc_Stages
        ReadOnly Property Node As WbNode
        ''' <summary>Stage\DSTD\Destruction Stage Data\Health %</summary>
        Property DestructionStageDataHealth As Byte
        ''' <summary>Stage\DSTD\Destruction Stage Data\Index</summary>
        Property DestructionStageDataIndex As Byte
        ''' <summary>Stage\DSTD\Destruction Stage Data\Model Damage Stage</summary>
        Property DestructionStageDataModelDamageStage As Byte
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
        ''' <summary>Stage\DSTD\Destruction Stage Data\Self Damage per Second</summary>
        Property DestructionStageDataSelfDamagePerSecond As Integer
        ''' <summary>Stage\DSTD\Destruction Stage Data\Explosion  -&gt;  EXPL / NULL. Referencia en el espacio del orden de carga.</summary>
        Property DestructionStageDataExplosion As UInteger
        ''' <summary>Stage\DSTD\Destruction Stage Data\Debris  -&gt;  DEBR / NULL. Referencia en el espacio del orden de carga.</summary>
        Property DestructionStageDataDebris As UInteger
        ''' <summary>Stage\DSTD\Destruction Stage Data\Debris Count</summary>
        Property DestructionStageDataDebrisCount As Integer
        ''' <summary>Stage\Model\DMDL\Model FileName</summary>
        Property ModelModelFileName As String
    End Interface

    ''' <summary>Un elemento de Attacks, en lo que los dos juegos comparten.</summary>
    Public Interface INpc_Attacks
        ReadOnly Property Node As WbNode
        ''' <summary>Attack\ATKD\Attack Data\Damage Mult</summary>
        Property AttackDataDamageMult As Single
        ''' <summary>Attack\ATKD\Attack Data\Attack Chance</summary>
        Property AttackDataAttackChance As Single
        ''' <summary>Attack\ATKD\Attack Data\Attack Spell  -&gt;  SPEL / NULL. Referencia en el espacio del orden de carga.</summary>
        Property AttackDataAttackSpell As UInteger
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
        ''' <summary>Attack\ATKD\Attack Data\Attack Angle</summary>
        Property AttackDataAttackAngle As Single
        ''' <summary>Attack\ATKD\Attack Data\Strike Angle</summary>
        Property AttackDataStrikeAngle As Single
        ''' <summary>Attack\ATKD\Attack Data\Stagger</summary>
        Property AttackDataStagger As Single
        ''' <summary>Attack\ATKD\Attack Data\Knockdown</summary>
        Property AttackDataKnockdown As Single
        ''' <summary>Attack\ATKD\Attack Data\Recovery Time</summary>
        Property AttackDataRecoveryTime As Single
        ''' <summary>Attack\ATKE\Attack Event</summary>
        Property AttackAttackEvent As String
    End Interface

    ''' <summary>Un elemento de Perks, en lo que los dos juegos comparten.</summary>
    Public Interface INpc_Perks
        ReadOnly Property Node As WbNode
        ''' <summary>PRKR\Perk\Perk  -&gt;  PERK. Referencia en el espacio del orden de carga.</summary>
        Property PerkPerk As UInteger
        ''' <summary>PRKR\Perk\Rank</summary>
        Property PerkRank As Byte
    End Interface

    ''' <summary>Un elemento de Items, en lo que los dos juegos comparten.</summary>
    Public Interface INpc_Items
        ReadOnly Property Node As WbNode
        ''' <summary>Item\CNTO\Item\Item. Referencia en el espacio del orden de carga.</summary>
        Property ItemItem As UInteger
        ''' <summary>Item\CNTO\Item\Count</summary>
        Property ItemCount As Integer
        ''' <summary>Item\COED\Extra Data\Owner  -&gt;  NPC_ / FACT / NULL. Referencia en el espacio del orden de carga.</summary>
        Property ExtraDataOwner As UInteger
        ''' <summary>Item\COED\Extra Data\Item Condition</summary>
        Property ExtraDataItemCondition As Single
    End Interface

    ''' <summary>Un elemento de Packages, en lo que los dos juegos comparten.</summary>
    Public Interface INpc_Packages
        ReadOnly Property Node As WbNode
        ''' <summary>PKID\Package  -&gt;  PACK. Referencia en el espacio del orden de carga.</summary>
        Property Package As UInteger
    End Interface

    ''' <summary>Un elemento de Keywords\KWDA\Keywords, en lo que los dos juegos comparten.</summary>
    Public Interface INpc_Keywords
        ReadOnly Property Node As WbNode
        ''' <summary>Keyword  -&gt;  KYWD / NULL. Referencia en el espacio del orden de carga.</summary>
        Property Keyword As UInteger
    End Interface

    ''' <summary>Un elemento de Head Parts, en lo que los dos juegos comparten.</summary>
    Public Interface INpc_HeadParts
        ReadOnly Property Node As WbNode
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
        ''' <summary>EDID\Editor ID</summary>
        Property EditorID As String
        ''' <summary>FULL\Name. Resuelto contra las tablas de texto si el archivo las usa.</summary>
        Property Name As String
        ''' <summary>DESC\Description. Resuelto contra las tablas de texto si el archivo las usa.</summary>
        Property Description As String
        ''' <summary>Model\MODL\Model FileName</summary>
        Property ModelModelFileName As String
        ''' <summary>Model\MODC\Color Remapping Index</summary>
        Property ModelColorRemappingIndex As Single
        ''' <summary>Model\MODS\Material Swap  -&gt;  MSWP. Referencia en el espacio del orden de carga.</summary>
        Property ModelMaterialSwap As UInteger
        ''' <summary>Model\MODF\Flags</summary>
        Property ModelFlags As Byte
        ''' <summary>DATA\Data\Include Count</summary>
        Property DataIncludeCount As UInteger
        ''' <summary>DATA\Data\Property Count</summary>
        Property DataPropertyCount As UInteger
        ''' <summary>DATA\Data\Unknown Bool 1</summary>
        Property DataUnknownBool1 As Byte
        ''' <summary>DATA\Data\Unknown Bool 2</summary>
        Property DataUnknownBool2 As Byte
        ''' <summary>DATA\Data\Form Type</summary>
        Property DataFormType As UInteger
        ''' <summary>DATA\Data\Max Rank</summary>
        Property DataMaxRank As Byte
        ''' <summary>DATA\Data\Level Tier Scaled Offset</summary>
        Property DataLevelTierScaledOffset As Byte
        ''' <summary>DATA\Data\Attach Point  -&gt;  KYWD / NULL. Referencia en el espacio del orden de carga.</summary>
        Property DataAttachPoint As UInteger
        ''' <summary>LNAM\Loose Mod. Referencia en el espacio del orden de carga.</summary>
        Property LooseMod As UInteger
        ''' <summary>NAM1\Priority</summary>
        Property Priority As Byte
        ''' <summary>FLTR\Filter</summary>
        Property Filter As String
        ''' <summary>DATA\Data\Attach Parent Slots</summary>
        ReadOnly Property AttachParentSlots As IReadOnlyList(Of IOmod_AttachParentSlots)
        ''' <summary>DATA\Data\Items</summary>
        ReadOnly Property Items As IReadOnlyList(Of IOmod_Items)
        ''' <summary>DATA\Data\Includes</summary>
        ReadOnly Property Includes As IReadOnlyList(Of IOmod_Includes)
        ''' <summary>DATA\Data\Properties</summary>
        ReadOnly Property Properties As IReadOnlyList(Of IOmod_Properties)
        ''' <summary>MNAM\Target OMOD Keywords</summary>
        ReadOnly Property TargetOMODKeywords As IReadOnlyList(Of IOmod_TargetOMODKeywords)
        ''' <summary>FNAM\Filter Keywords</summary>
        ReadOnly Property FilterKeywords As IReadOnlyList(Of IOmod_FilterKeywords)
    End Interface

    ''' <summary>Un elemento de DATA\Data\Attach Parent Slots, en lo que los dos juegos comparten.</summary>
    Public Interface IOmod_AttachParentSlots
        ReadOnly Property Node As WbNode
        ''' <summary>Keyword  -&gt;  KYWD / NULL. Referencia en el espacio del orden de carga.</summary>
        Property Keyword As UInteger
    End Interface

    ''' <summary>Un elemento de DATA\Data\Items, en lo que los dos juegos comparten.</summary>
    Public Interface IOmod_Items
        ReadOnly Property Node As WbNode
        ''' <summary>Item\Value 1</summary>
        Property ItemValue1 As Byte()
        ''' <summary>Item\Value 2</summary>
        Property ItemValue2 As Byte()
    End Interface

    ''' <summary>Un elemento de DATA\Data\Includes, en lo que los dos juegos comparten.</summary>
    Public Interface IOmod_Includes
        ReadOnly Property Node As WbNode
        ''' <summary>Include\Mod  -&gt;  OMOD. Referencia en el espacio del orden de carga.</summary>
        Property IncludeMod As UInteger
        ''' <summary>Include\Minimum Level</summary>
        Property IncludeMinimumLevel As Byte
        ''' <summary>Include\Optional</summary>
        Property IncludeOptional As Byte
        ''' <summary>Include\Don't Use All</summary>
        Property IncludeDonTUseAll As Byte
    End Interface

    ''' <summary>Un elemento de DATA\Data\Properties, en lo que los dos juegos comparten.</summary>
    Public Interface IOmod_Properties
        ReadOnly Property Node As WbNode
        ''' <summary>Property\Value Type</summary>
        Property PropertyValueType As Byte
        ''' <summary>Nombre del valor de Property\Value Type.</summary>
        ReadOnly Property PropertyValueTypeNombre As String
        ''' <summary>Property\Function Type</summary>
        Property PropertyFunctionType As Byte
        ''' <summary>Nombre del valor de Property\Function Type.</summary>
        ReadOnly Property PropertyFunctionTypeNombre As String
        ''' <summary>Property\Property</summary>
        Property PropertyProperty As UShort
        ''' <summary>Property\Value 1 - Unknown</summary>
        Property PropertyValue1Unknown As Byte()
        ''' <summary>Property\Step</summary>
        Property PropertyStep As Single
    End Interface

    ''' <summary>Un elemento de MNAM\Target OMOD Keywords, en lo que los dos juegos comparten.</summary>
    Public Interface IOmod_TargetOMODKeywords
        ReadOnly Property Node As WbNode
        ''' <summary>Keyword  -&gt;  KYWD. Referencia en el espacio del orden de carga.</summary>
        Property Keyword As UInteger
    End Interface

    ''' <summary>Un elemento de FNAM\Filter Keywords, en lo que los dos juegos comparten.</summary>
    Public Interface IOmod_FilterKeywords
        ReadOnly Property Node As WbNode
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
        ''' <summary>EDID\Editor ID</summary>
        Property EditorID As String
        ''' <summary>INAM\Items</summary>
        ReadOnly Property Items As IReadOnlyList(Of IOtft_Items)
    End Interface

    ''' <summary>Un elemento de INAM\Items, en lo que los dos juegos comparten.</summary>
    Public Interface IOtft_Items
        ReadOnly Property Node As WbNode
        ''' <summary>Item  -&gt;  ARMO / LVLI. Referencia en el espacio del orden de carga.</summary>
        Property Item As UInteger
    End Interface

    ''' <summary>Campos de un record RACE que los dos juegos declaran igual.
    ''' <para>El codigo que solo usa estos no necesita saber de que juego se trata. El
    ''' que necesita algo propio de un juego tiene que nombrar la clase de ese juego, y
    ''' asi no puede leer sin querer un campo que alla significa otra cosa.</para></summary>
    Public Interface IRace
        ReadOnly Property Node As WbNode
        ''' <summary>Identificador del record.</summary>
        ReadOnly Property FormID As UInteger
        ''' <summary>EDID\Editor ID</summary>
        Property EditorID As String
        ''' <summary>FULL\Name. Resuelto contra las tablas de texto si el archivo las usa.</summary>
        Property Name As String
        ''' <summary>DESC\Description. Resuelto contra las tablas de texto si el archivo las usa.</summary>
        Property Description As String
        ''' <summary>SPCT\Count</summary>
        Property Count As UInteger
        ''' <summary>WNAM\Skin  -&gt;  ARMO / NULL. Referencia en el espacio del orden de carga.</summary>
        Property Skin As UInteger
        ''' <summary>BOD2\Biped Body Template\First Person Flags</summary>
        Property BipedBodyTemplateFirstPersonFlags As UInteger
        ''' <summary>Keywords\KSIZ\Keyword Count</summary>
        Property KeywordsKeywordCount As UInteger
        ''' <summary>ANAM\Male Skeletal Model</summary>
        Property MaleSkeletalModel As String
        ''' <summary>ANAM\Female Skeletal Model</summary>
        Property FemaleSkeletalModel As String
        ''' <summary>TINL\Total Number of Tints in List</summary>
        Property TotalNumberOfTintsInList As UShort
        ''' <summary>PNAM\FaceGen - Main clamp</summary>
        Property FaceGenMainClamp As Single
        ''' <summary>UNAM\FaceGen - Face clamp</summary>
        Property FaceGenFaceClamp As Single
        ''' <summary>ATKR\Attack Race  -&gt;  RACE. Referencia en el espacio del orden de carga.</summary>
        Property AttackRace As UInteger
        ''' <summary>GNAM\Body Part Data  -&gt;  BPTD. Referencia en el espacio del orden de carga.</summary>
        Property BodyPartData As UInteger
        ''' <summary>Male Behavior Graph\Model\MODL\Model FileName</summary>
        Property ModelModelFileName As String
        ''' <summary>Female Behavior Graph\Model\MODL\Model FileName</summary>
        Property ModelModelFileName2 As String
        ''' <summary>NAM5\Impact Data Set  -&gt;  IPDS. Referencia en el espacio del orden de carga.</summary>
        Property ImpactDataSet As UInteger
        ''' <summary>VNAM\Equipment Flags</summary>
        Property EquipmentFlags As UInteger
        ''' <summary>FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Property PhonemeTargetWeightAahLipBigAah As Single
        ''' <summary>FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Property PhonemeTargetWeightBigAahLipDST As Single
        ''' <summary>FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Property PhonemeTargetWeightBMPLipEee As Single
        ''' <summary>FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Property PhonemeTargetWeightChJshLipFV As Single
        ''' <summary>FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Property PhonemeTargetWeightDSTLipK As Single
        ''' <summary>FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Property PhonemeTargetWeightEeeLipL As Single
        ''' <summary>FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Property PhonemeTargetWeightEhLipR As Single
        ''' <summary>FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Property PhonemeTargetWeightFVLipTh As Single
        ''' <summary>FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\I</summary>
        Property PhonemeTargetWeightI As Single
        ''' <summary>FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\K</summary>
        Property PhonemeTargetWeightK As Single
        ''' <summary>FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\N</summary>
        Property PhonemeTargetWeightN As Single
        ''' <summary>FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\Oh</summary>
        Property PhonemeTargetWeightOh As Single
        ''' <summary>FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\OohQ</summary>
        Property PhonemeTargetWeightOohQ As Single
        ''' <summary>FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\R</summary>
        Property PhonemeTargetWeightR As Single
        ''' <summary>FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\TH</summary>
        Property PhonemeTargetWeightTH As Single
        ''' <summary>FaceFX Phonemes\IY\PHWT\Phoneme Target Weight\W</summary>
        Property PhonemeTargetWeightW As Single
        ''' <summary>FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Property PhonemeTargetWeightAahLipBigAah2 As Single
        ''' <summary>FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Property PhonemeTargetWeightBigAahLipDST2 As Single
        ''' <summary>FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Property PhonemeTargetWeightBMPLipEee2 As Single
        ''' <summary>FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Property PhonemeTargetWeightChJshLipFV2 As Single
        ''' <summary>FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Property PhonemeTargetWeightDSTLipK2 As Single
        ''' <summary>FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Property PhonemeTargetWeightEeeLipL2 As Single
        ''' <summary>FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Property PhonemeTargetWeightEhLipR2 As Single
        ''' <summary>FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Property PhonemeTargetWeightFVLipTh2 As Single
        ''' <summary>FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\I</summary>
        Property PhonemeTargetWeightI2 As Single
        ''' <summary>FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\K</summary>
        Property PhonemeTargetWeightK2 As Single
        ''' <summary>FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\N</summary>
        Property PhonemeTargetWeightN2 As Single
        ''' <summary>FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\Oh</summary>
        Property PhonemeTargetWeightOh2 As Single
        ''' <summary>FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\OohQ</summary>
        Property PhonemeTargetWeightOohQ2 As Single
        ''' <summary>FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\R</summary>
        Property PhonemeTargetWeightR2 As Single
        ''' <summary>FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\TH</summary>
        Property PhonemeTargetWeightTH2 As Single
        ''' <summary>FaceFX Phonemes\IH\PHWT\Phoneme Target Weight\W</summary>
        Property PhonemeTargetWeightW2 As Single
        ''' <summary>FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Property PhonemeTargetWeightAahLipBigAah3 As Single
        ''' <summary>FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Property PhonemeTargetWeightBigAahLipDST3 As Single
        ''' <summary>FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Property PhonemeTargetWeightBMPLipEee3 As Single
        ''' <summary>FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Property PhonemeTargetWeightChJshLipFV3 As Single
        ''' <summary>FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Property PhonemeTargetWeightDSTLipK3 As Single
        ''' <summary>FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Property PhonemeTargetWeightEeeLipL3 As Single
        ''' <summary>FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Property PhonemeTargetWeightEhLipR3 As Single
        ''' <summary>FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Property PhonemeTargetWeightFVLipTh3 As Single
        ''' <summary>FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\I</summary>
        Property PhonemeTargetWeightI3 As Single
        ''' <summary>FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\K</summary>
        Property PhonemeTargetWeightK3 As Single
        ''' <summary>FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\N</summary>
        Property PhonemeTargetWeightN3 As Single
        ''' <summary>FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\Oh</summary>
        Property PhonemeTargetWeightOh3 As Single
        ''' <summary>FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\OohQ</summary>
        Property PhonemeTargetWeightOohQ3 As Single
        ''' <summary>FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\R</summary>
        Property PhonemeTargetWeightR3 As Single
        ''' <summary>FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\TH</summary>
        Property PhonemeTargetWeightTH3 As Single
        ''' <summary>FaceFX Phonemes\EH\PHWT\Phoneme Target Weight\W</summary>
        Property PhonemeTargetWeightW3 As Single
        ''' <summary>FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Property PhonemeTargetWeightAahLipBigAah4 As Single
        ''' <summary>FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Property PhonemeTargetWeightBigAahLipDST4 As Single
        ''' <summary>FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Property PhonemeTargetWeightBMPLipEee4 As Single
        ''' <summary>FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Property PhonemeTargetWeightChJshLipFV4 As Single
        ''' <summary>FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Property PhonemeTargetWeightDSTLipK4 As Single
        ''' <summary>FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Property PhonemeTargetWeightEeeLipL4 As Single
        ''' <summary>FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Property PhonemeTargetWeightEhLipR4 As Single
        ''' <summary>FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Property PhonemeTargetWeightFVLipTh4 As Single
        ''' <summary>FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\I</summary>
        Property PhonemeTargetWeightI4 As Single
        ''' <summary>FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\K</summary>
        Property PhonemeTargetWeightK4 As Single
        ''' <summary>FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\N</summary>
        Property PhonemeTargetWeightN4 As Single
        ''' <summary>FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\Oh</summary>
        Property PhonemeTargetWeightOh4 As Single
        ''' <summary>FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\OohQ</summary>
        Property PhonemeTargetWeightOohQ4 As Single
        ''' <summary>FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\R</summary>
        Property PhonemeTargetWeightR4 As Single
        ''' <summary>FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\TH</summary>
        Property PhonemeTargetWeightTH4 As Single
        ''' <summary>FaceFX Phonemes\EY\PHWT\Phoneme Target Weight\W</summary>
        Property PhonemeTargetWeightW4 As Single
        ''' <summary>FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Property PhonemeTargetWeightAahLipBigAah5 As Single
        ''' <summary>FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Property PhonemeTargetWeightBigAahLipDST5 As Single
        ''' <summary>FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Property PhonemeTargetWeightBMPLipEee5 As Single
        ''' <summary>FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Property PhonemeTargetWeightChJshLipFV5 As Single
        ''' <summary>FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Property PhonemeTargetWeightDSTLipK5 As Single
        ''' <summary>FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Property PhonemeTargetWeightEeeLipL5 As Single
        ''' <summary>FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Property PhonemeTargetWeightEhLipR5 As Single
        ''' <summary>FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Property PhonemeTargetWeightFVLipTh5 As Single
        ''' <summary>FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\I</summary>
        Property PhonemeTargetWeightI5 As Single
        ''' <summary>FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\K</summary>
        Property PhonemeTargetWeightK5 As Single
        ''' <summary>FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\N</summary>
        Property PhonemeTargetWeightN5 As Single
        ''' <summary>FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\Oh</summary>
        Property PhonemeTargetWeightOh5 As Single
        ''' <summary>FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\OohQ</summary>
        Property PhonemeTargetWeightOohQ5 As Single
        ''' <summary>FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\R</summary>
        Property PhonemeTargetWeightR5 As Single
        ''' <summary>FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\TH</summary>
        Property PhonemeTargetWeightTH5 As Single
        ''' <summary>FaceFX Phonemes\AE\PHWT\Phoneme Target Weight\W</summary>
        Property PhonemeTargetWeightW5 As Single
        ''' <summary>FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Property PhonemeTargetWeightAahLipBigAah6 As Single
        ''' <summary>FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Property PhonemeTargetWeightBigAahLipDST6 As Single
        ''' <summary>FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Property PhonemeTargetWeightBMPLipEee6 As Single
        ''' <summary>FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Property PhonemeTargetWeightChJshLipFV6 As Single
        ''' <summary>FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Property PhonemeTargetWeightDSTLipK6 As Single
        ''' <summary>FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Property PhonemeTargetWeightEeeLipL6 As Single
        ''' <summary>FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Property PhonemeTargetWeightEhLipR6 As Single
        ''' <summary>FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Property PhonemeTargetWeightFVLipTh6 As Single
        ''' <summary>FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\I</summary>
        Property PhonemeTargetWeightI6 As Single
        ''' <summary>FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\K</summary>
        Property PhonemeTargetWeightK6 As Single
        ''' <summary>FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\N</summary>
        Property PhonemeTargetWeightN6 As Single
        ''' <summary>FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\Oh</summary>
        Property PhonemeTargetWeightOh6 As Single
        ''' <summary>FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\OohQ</summary>
        Property PhonemeTargetWeightOohQ6 As Single
        ''' <summary>FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\R</summary>
        Property PhonemeTargetWeightR6 As Single
        ''' <summary>FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\TH</summary>
        Property PhonemeTargetWeightTH6 As Single
        ''' <summary>FaceFX Phonemes\AA\PHWT\Phoneme Target Weight\W</summary>
        Property PhonemeTargetWeightW6 As Single
        ''' <summary>FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Property PhonemeTargetWeightAahLipBigAah7 As Single
        ''' <summary>FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Property PhonemeTargetWeightBigAahLipDST7 As Single
        ''' <summary>FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Property PhonemeTargetWeightBMPLipEee7 As Single
        ''' <summary>FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Property PhonemeTargetWeightChJshLipFV7 As Single
        ''' <summary>FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Property PhonemeTargetWeightDSTLipK7 As Single
        ''' <summary>FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Property PhonemeTargetWeightEeeLipL7 As Single
        ''' <summary>FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Property PhonemeTargetWeightEhLipR7 As Single
        ''' <summary>FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Property PhonemeTargetWeightFVLipTh7 As Single
        ''' <summary>FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\I</summary>
        Property PhonemeTargetWeightI7 As Single
        ''' <summary>FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\K</summary>
        Property PhonemeTargetWeightK7 As Single
        ''' <summary>FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\N</summary>
        Property PhonemeTargetWeightN7 As Single
        ''' <summary>FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\Oh</summary>
        Property PhonemeTargetWeightOh7 As Single
        ''' <summary>FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\OohQ</summary>
        Property PhonemeTargetWeightOohQ7 As Single
        ''' <summary>FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\R</summary>
        Property PhonemeTargetWeightR7 As Single
        ''' <summary>FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\TH</summary>
        Property PhonemeTargetWeightTH7 As Single
        ''' <summary>FaceFX Phonemes\AW\PHWT\Phoneme Target Weight\W</summary>
        Property PhonemeTargetWeightW7 As Single
        ''' <summary>FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Property PhonemeTargetWeightAahLipBigAah8 As Single
        ''' <summary>FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Property PhonemeTargetWeightBigAahLipDST8 As Single
        ''' <summary>FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Property PhonemeTargetWeightBMPLipEee8 As Single
        ''' <summary>FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Property PhonemeTargetWeightChJshLipFV8 As Single
        ''' <summary>FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Property PhonemeTargetWeightDSTLipK8 As Single
        ''' <summary>FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Property PhonemeTargetWeightEeeLipL8 As Single
        ''' <summary>FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Property PhonemeTargetWeightEhLipR8 As Single
        ''' <summary>FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Property PhonemeTargetWeightFVLipTh8 As Single
        ''' <summary>FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\I</summary>
        Property PhonemeTargetWeightI8 As Single
        ''' <summary>FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\K</summary>
        Property PhonemeTargetWeightK8 As Single
        ''' <summary>FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\N</summary>
        Property PhonemeTargetWeightN8 As Single
        ''' <summary>FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\Oh</summary>
        Property PhonemeTargetWeightOh8 As Single
        ''' <summary>FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\OohQ</summary>
        Property PhonemeTargetWeightOohQ8 As Single
        ''' <summary>FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\R</summary>
        Property PhonemeTargetWeightR8 As Single
        ''' <summary>FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\TH</summary>
        Property PhonemeTargetWeightTH8 As Single
        ''' <summary>FaceFX Phonemes\AY\PHWT\Phoneme Target Weight\W</summary>
        Property PhonemeTargetWeightW8 As Single
        ''' <summary>FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Property PhonemeTargetWeightAahLipBigAah9 As Single
        ''' <summary>FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Property PhonemeTargetWeightBigAahLipDST9 As Single
        ''' <summary>FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Property PhonemeTargetWeightBMPLipEee9 As Single
        ''' <summary>FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Property PhonemeTargetWeightChJshLipFV9 As Single
        ''' <summary>FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Property PhonemeTargetWeightDSTLipK9 As Single
        ''' <summary>FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Property PhonemeTargetWeightEeeLipL9 As Single
        ''' <summary>FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Property PhonemeTargetWeightEhLipR9 As Single
        ''' <summary>FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Property PhonemeTargetWeightFVLipTh9 As Single
        ''' <summary>FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\I</summary>
        Property PhonemeTargetWeightI9 As Single
        ''' <summary>FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\K</summary>
        Property PhonemeTargetWeightK9 As Single
        ''' <summary>FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\N</summary>
        Property PhonemeTargetWeightN9 As Single
        ''' <summary>FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\Oh</summary>
        Property PhonemeTargetWeightOh9 As Single
        ''' <summary>FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\OohQ</summary>
        Property PhonemeTargetWeightOohQ9 As Single
        ''' <summary>FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\R</summary>
        Property PhonemeTargetWeightR9 As Single
        ''' <summary>FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\TH</summary>
        Property PhonemeTargetWeightTH9 As Single
        ''' <summary>FaceFX Phonemes\AH\PHWT\Phoneme Target Weight\W</summary>
        Property PhonemeTargetWeightW9 As Single
        ''' <summary>FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Property PhonemeTargetWeightAahLipBigAah10 As Single
        ''' <summary>FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Property PhonemeTargetWeightBigAahLipDST10 As Single
        ''' <summary>FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Property PhonemeTargetWeightBMPLipEee10 As Single
        ''' <summary>FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Property PhonemeTargetWeightChJshLipFV10 As Single
        ''' <summary>FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Property PhonemeTargetWeightDSTLipK10 As Single
        ''' <summary>FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Property PhonemeTargetWeightEeeLipL10 As Single
        ''' <summary>FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Property PhonemeTargetWeightEhLipR10 As Single
        ''' <summary>FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Property PhonemeTargetWeightFVLipTh10 As Single
        ''' <summary>FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\I</summary>
        Property PhonemeTargetWeightI10 As Single
        ''' <summary>FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\K</summary>
        Property PhonemeTargetWeightK10 As Single
        ''' <summary>FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\N</summary>
        Property PhonemeTargetWeightN10 As Single
        ''' <summary>FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\Oh</summary>
        Property PhonemeTargetWeightOh10 As Single
        ''' <summary>FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\OohQ</summary>
        Property PhonemeTargetWeightOohQ10 As Single
        ''' <summary>FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\R</summary>
        Property PhonemeTargetWeightR10 As Single
        ''' <summary>FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\TH</summary>
        Property PhonemeTargetWeightTH10 As Single
        ''' <summary>FaceFX Phonemes\AO\PHWT\Phoneme Target Weight\W</summary>
        Property PhonemeTargetWeightW10 As Single
        ''' <summary>FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Property PhonemeTargetWeightAahLipBigAah11 As Single
        ''' <summary>FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Property PhonemeTargetWeightBigAahLipDST11 As Single
        ''' <summary>FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Property PhonemeTargetWeightBMPLipEee11 As Single
        ''' <summary>FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Property PhonemeTargetWeightChJshLipFV11 As Single
        ''' <summary>FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Property PhonemeTargetWeightDSTLipK11 As Single
        ''' <summary>FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Property PhonemeTargetWeightEeeLipL11 As Single
        ''' <summary>FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Property PhonemeTargetWeightEhLipR11 As Single
        ''' <summary>FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Property PhonemeTargetWeightFVLipTh11 As Single
        ''' <summary>FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\I</summary>
        Property PhonemeTargetWeightI11 As Single
        ''' <summary>FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\K</summary>
        Property PhonemeTargetWeightK11 As Single
        ''' <summary>FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\N</summary>
        Property PhonemeTargetWeightN11 As Single
        ''' <summary>FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\Oh</summary>
        Property PhonemeTargetWeightOh11 As Single
        ''' <summary>FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\OohQ</summary>
        Property PhonemeTargetWeightOohQ11 As Single
        ''' <summary>FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\R</summary>
        Property PhonemeTargetWeightR11 As Single
        ''' <summary>FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\TH</summary>
        Property PhonemeTargetWeightTH11 As Single
        ''' <summary>FaceFX Phonemes\OY\PHWT\Phoneme Target Weight\W</summary>
        Property PhonemeTargetWeightW11 As Single
        ''' <summary>FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Property PhonemeTargetWeightAahLipBigAah12 As Single
        ''' <summary>FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Property PhonemeTargetWeightBigAahLipDST12 As Single
        ''' <summary>FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Property PhonemeTargetWeightBMPLipEee12 As Single
        ''' <summary>FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Property PhonemeTargetWeightChJshLipFV12 As Single
        ''' <summary>FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Property PhonemeTargetWeightDSTLipK12 As Single
        ''' <summary>FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Property PhonemeTargetWeightEeeLipL12 As Single
        ''' <summary>FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Property PhonemeTargetWeightEhLipR12 As Single
        ''' <summary>FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Property PhonemeTargetWeightFVLipTh12 As Single
        ''' <summary>FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\I</summary>
        Property PhonemeTargetWeightI12 As Single
        ''' <summary>FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\K</summary>
        Property PhonemeTargetWeightK12 As Single
        ''' <summary>FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\N</summary>
        Property PhonemeTargetWeightN12 As Single
        ''' <summary>FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\Oh</summary>
        Property PhonemeTargetWeightOh12 As Single
        ''' <summary>FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\OohQ</summary>
        Property PhonemeTargetWeightOohQ12 As Single
        ''' <summary>FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\R</summary>
        Property PhonemeTargetWeightR12 As Single
        ''' <summary>FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\TH</summary>
        Property PhonemeTargetWeightTH12 As Single
        ''' <summary>FaceFX Phonemes\OW\PHWT\Phoneme Target Weight\W</summary>
        Property PhonemeTargetWeightW12 As Single
        ''' <summary>FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Property PhonemeTargetWeightAahLipBigAah13 As Single
        ''' <summary>FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Property PhonemeTargetWeightBigAahLipDST13 As Single
        ''' <summary>FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Property PhonemeTargetWeightBMPLipEee13 As Single
        ''' <summary>FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Property PhonemeTargetWeightChJshLipFV13 As Single
        ''' <summary>FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Property PhonemeTargetWeightDSTLipK13 As Single
        ''' <summary>FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Property PhonemeTargetWeightEeeLipL13 As Single
        ''' <summary>FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Property PhonemeTargetWeightEhLipR13 As Single
        ''' <summary>FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Property PhonemeTargetWeightFVLipTh13 As Single
        ''' <summary>FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\I</summary>
        Property PhonemeTargetWeightI13 As Single
        ''' <summary>FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\K</summary>
        Property PhonemeTargetWeightK13 As Single
        ''' <summary>FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\N</summary>
        Property PhonemeTargetWeightN13 As Single
        ''' <summary>FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\Oh</summary>
        Property PhonemeTargetWeightOh13 As Single
        ''' <summary>FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\OohQ</summary>
        Property PhonemeTargetWeightOohQ13 As Single
        ''' <summary>FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\R</summary>
        Property PhonemeTargetWeightR13 As Single
        ''' <summary>FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\TH</summary>
        Property PhonemeTargetWeightTH13 As Single
        ''' <summary>FaceFX Phonemes\UH\PHWT\Phoneme Target Weight\W</summary>
        Property PhonemeTargetWeightW13 As Single
        ''' <summary>FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Property PhonemeTargetWeightAahLipBigAah14 As Single
        ''' <summary>FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Property PhonemeTargetWeightBigAahLipDST14 As Single
        ''' <summary>FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Property PhonemeTargetWeightBMPLipEee14 As Single
        ''' <summary>FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Property PhonemeTargetWeightChJshLipFV14 As Single
        ''' <summary>FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Property PhonemeTargetWeightDSTLipK14 As Single
        ''' <summary>FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Property PhonemeTargetWeightEeeLipL14 As Single
        ''' <summary>FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Property PhonemeTargetWeightEhLipR14 As Single
        ''' <summary>FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Property PhonemeTargetWeightFVLipTh14 As Single
        ''' <summary>FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\I</summary>
        Property PhonemeTargetWeightI14 As Single
        ''' <summary>FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\K</summary>
        Property PhonemeTargetWeightK14 As Single
        ''' <summary>FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\N</summary>
        Property PhonemeTargetWeightN14 As Single
        ''' <summary>FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\Oh</summary>
        Property PhonemeTargetWeightOh14 As Single
        ''' <summary>FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\OohQ</summary>
        Property PhonemeTargetWeightOohQ14 As Single
        ''' <summary>FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\R</summary>
        Property PhonemeTargetWeightR14 As Single
        ''' <summary>FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\TH</summary>
        Property PhonemeTargetWeightTH14 As Single
        ''' <summary>FaceFX Phonemes\UW\PHWT\Phoneme Target Weight\W</summary>
        Property PhonemeTargetWeightW14 As Single
        ''' <summary>FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Property PhonemeTargetWeightAahLipBigAah15 As Single
        ''' <summary>FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Property PhonemeTargetWeightBigAahLipDST15 As Single
        ''' <summary>FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Property PhonemeTargetWeightBMPLipEee15 As Single
        ''' <summary>FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Property PhonemeTargetWeightChJshLipFV15 As Single
        ''' <summary>FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Property PhonemeTargetWeightDSTLipK15 As Single
        ''' <summary>FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Property PhonemeTargetWeightEeeLipL15 As Single
        ''' <summary>FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Property PhonemeTargetWeightEhLipR15 As Single
        ''' <summary>FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Property PhonemeTargetWeightFVLipTh15 As Single
        ''' <summary>FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\I</summary>
        Property PhonemeTargetWeightI15 As Single
        ''' <summary>FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\K</summary>
        Property PhonemeTargetWeightK15 As Single
        ''' <summary>FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\N</summary>
        Property PhonemeTargetWeightN15 As Single
        ''' <summary>FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\Oh</summary>
        Property PhonemeTargetWeightOh15 As Single
        ''' <summary>FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\OohQ</summary>
        Property PhonemeTargetWeightOohQ15 As Single
        ''' <summary>FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\R</summary>
        Property PhonemeTargetWeightR15 As Single
        ''' <summary>FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\TH</summary>
        Property PhonemeTargetWeightTH15 As Single
        ''' <summary>FaceFX Phonemes\ER\PHWT\Phoneme Target Weight\W</summary>
        Property PhonemeTargetWeightW15 As Single
        ''' <summary>FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Property PhonemeTargetWeightAahLipBigAah16 As Single
        ''' <summary>FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Property PhonemeTargetWeightBigAahLipDST16 As Single
        ''' <summary>FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Property PhonemeTargetWeightBMPLipEee16 As Single
        ''' <summary>FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Property PhonemeTargetWeightChJshLipFV16 As Single
        ''' <summary>FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Property PhonemeTargetWeightDSTLipK16 As Single
        ''' <summary>FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Property PhonemeTargetWeightEeeLipL16 As Single
        ''' <summary>FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Property PhonemeTargetWeightEhLipR16 As Single
        ''' <summary>FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Property PhonemeTargetWeightFVLipTh16 As Single
        ''' <summary>FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\I</summary>
        Property PhonemeTargetWeightI16 As Single
        ''' <summary>FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\K</summary>
        Property PhonemeTargetWeightK16 As Single
        ''' <summary>FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\N</summary>
        Property PhonemeTargetWeightN16 As Single
        ''' <summary>FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\Oh</summary>
        Property PhonemeTargetWeightOh16 As Single
        ''' <summary>FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\OohQ</summary>
        Property PhonemeTargetWeightOohQ16 As Single
        ''' <summary>FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\R</summary>
        Property PhonemeTargetWeightR16 As Single
        ''' <summary>FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\TH</summary>
        Property PhonemeTargetWeightTH16 As Single
        ''' <summary>FaceFX Phonemes\AX\PHWT\Phoneme Target Weight\W</summary>
        Property PhonemeTargetWeightW16 As Single
        ''' <summary>FaceFX Phonemes\S\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Property PhonemeTargetWeightAahLipBigAah17 As Single
        ''' <summary>FaceFX Phonemes\S\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Property PhonemeTargetWeightBigAahLipDST17 As Single
        ''' <summary>FaceFX Phonemes\S\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Property PhonemeTargetWeightBMPLipEee17 As Single
        ''' <summary>FaceFX Phonemes\S\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Property PhonemeTargetWeightChJshLipFV17 As Single
        ''' <summary>FaceFX Phonemes\S\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Property PhonemeTargetWeightDSTLipK17 As Single
        ''' <summary>FaceFX Phonemes\S\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Property PhonemeTargetWeightEeeLipL17 As Single
        ''' <summary>FaceFX Phonemes\S\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Property PhonemeTargetWeightEhLipR17 As Single
        ''' <summary>FaceFX Phonemes\S\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Property PhonemeTargetWeightFVLipTh17 As Single
        ''' <summary>FaceFX Phonemes\S\PHWT\Phoneme Target Weight\I</summary>
        Property PhonemeTargetWeightI17 As Single
        ''' <summary>FaceFX Phonemes\S\PHWT\Phoneme Target Weight\K</summary>
        Property PhonemeTargetWeightK17 As Single
        ''' <summary>FaceFX Phonemes\S\PHWT\Phoneme Target Weight\N</summary>
        Property PhonemeTargetWeightN17 As Single
        ''' <summary>FaceFX Phonemes\S\PHWT\Phoneme Target Weight\Oh</summary>
        Property PhonemeTargetWeightOh17 As Single
        ''' <summary>FaceFX Phonemes\S\PHWT\Phoneme Target Weight\OohQ</summary>
        Property PhonemeTargetWeightOohQ17 As Single
        ''' <summary>FaceFX Phonemes\S\PHWT\Phoneme Target Weight\R</summary>
        Property PhonemeTargetWeightR17 As Single
        ''' <summary>FaceFX Phonemes\S\PHWT\Phoneme Target Weight\TH</summary>
        Property PhonemeTargetWeightTH17 As Single
        ''' <summary>FaceFX Phonemes\S\PHWT\Phoneme Target Weight\W</summary>
        Property PhonemeTargetWeightW17 As Single
        ''' <summary>FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Property PhonemeTargetWeightAahLipBigAah18 As Single
        ''' <summary>FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Property PhonemeTargetWeightBigAahLipDST18 As Single
        ''' <summary>FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Property PhonemeTargetWeightBMPLipEee18 As Single
        ''' <summary>FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Property PhonemeTargetWeightChJshLipFV18 As Single
        ''' <summary>FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Property PhonemeTargetWeightDSTLipK18 As Single
        ''' <summary>FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Property PhonemeTargetWeightEeeLipL18 As Single
        ''' <summary>FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Property PhonemeTargetWeightEhLipR18 As Single
        ''' <summary>FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Property PhonemeTargetWeightFVLipTh18 As Single
        ''' <summary>FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\I</summary>
        Property PhonemeTargetWeightI18 As Single
        ''' <summary>FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\K</summary>
        Property PhonemeTargetWeightK18 As Single
        ''' <summary>FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\N</summary>
        Property PhonemeTargetWeightN18 As Single
        ''' <summary>FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\Oh</summary>
        Property PhonemeTargetWeightOh18 As Single
        ''' <summary>FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\OohQ</summary>
        Property PhonemeTargetWeightOohQ18 As Single
        ''' <summary>FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\R</summary>
        Property PhonemeTargetWeightR18 As Single
        ''' <summary>FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\TH</summary>
        Property PhonemeTargetWeightTH18 As Single
        ''' <summary>FaceFX Phonemes\SH\PHWT\Phoneme Target Weight\W</summary>
        Property PhonemeTargetWeightW18 As Single
        ''' <summary>FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Property PhonemeTargetWeightAahLipBigAah19 As Single
        ''' <summary>FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Property PhonemeTargetWeightBigAahLipDST19 As Single
        ''' <summary>FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Property PhonemeTargetWeightBMPLipEee19 As Single
        ''' <summary>FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Property PhonemeTargetWeightChJshLipFV19 As Single
        ''' <summary>FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Property PhonemeTargetWeightDSTLipK19 As Single
        ''' <summary>FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Property PhonemeTargetWeightEeeLipL19 As Single
        ''' <summary>FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Property PhonemeTargetWeightEhLipR19 As Single
        ''' <summary>FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Property PhonemeTargetWeightFVLipTh19 As Single
        ''' <summary>FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\I</summary>
        Property PhonemeTargetWeightI19 As Single
        ''' <summary>FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\K</summary>
        Property PhonemeTargetWeightK19 As Single
        ''' <summary>FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\N</summary>
        Property PhonemeTargetWeightN19 As Single
        ''' <summary>FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\Oh</summary>
        Property PhonemeTargetWeightOh19 As Single
        ''' <summary>FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\OohQ</summary>
        Property PhonemeTargetWeightOohQ19 As Single
        ''' <summary>FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\R</summary>
        Property PhonemeTargetWeightR19 As Single
        ''' <summary>FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\TH</summary>
        Property PhonemeTargetWeightTH19 As Single
        ''' <summary>FaceFX Phonemes\Z\PHWT\Phoneme Target Weight\W</summary>
        Property PhonemeTargetWeightW19 As Single
        ''' <summary>FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Property PhonemeTargetWeightAahLipBigAah20 As Single
        ''' <summary>FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Property PhonemeTargetWeightBigAahLipDST20 As Single
        ''' <summary>FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Property PhonemeTargetWeightBMPLipEee20 As Single
        ''' <summary>FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Property PhonemeTargetWeightChJshLipFV20 As Single
        ''' <summary>FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Property PhonemeTargetWeightDSTLipK20 As Single
        ''' <summary>FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Property PhonemeTargetWeightEeeLipL20 As Single
        ''' <summary>FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Property PhonemeTargetWeightEhLipR20 As Single
        ''' <summary>FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Property PhonemeTargetWeightFVLipTh20 As Single
        ''' <summary>FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\I</summary>
        Property PhonemeTargetWeightI20 As Single
        ''' <summary>FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\K</summary>
        Property PhonemeTargetWeightK20 As Single
        ''' <summary>FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\N</summary>
        Property PhonemeTargetWeightN20 As Single
        ''' <summary>FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\Oh</summary>
        Property PhonemeTargetWeightOh20 As Single
        ''' <summary>FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\OohQ</summary>
        Property PhonemeTargetWeightOohQ20 As Single
        ''' <summary>FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\R</summary>
        Property PhonemeTargetWeightR20 As Single
        ''' <summary>FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\TH</summary>
        Property PhonemeTargetWeightTH20 As Single
        ''' <summary>FaceFX Phonemes\ZH\PHWT\Phoneme Target Weight\W</summary>
        Property PhonemeTargetWeightW20 As Single
        ''' <summary>FaceFX Phonemes\F\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Property PhonemeTargetWeightAahLipBigAah21 As Single
        ''' <summary>FaceFX Phonemes\F\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Property PhonemeTargetWeightBigAahLipDST21 As Single
        ''' <summary>FaceFX Phonemes\F\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Property PhonemeTargetWeightBMPLipEee21 As Single
        ''' <summary>FaceFX Phonemes\F\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Property PhonemeTargetWeightChJshLipFV21 As Single
        ''' <summary>FaceFX Phonemes\F\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Property PhonemeTargetWeightDSTLipK21 As Single
        ''' <summary>FaceFX Phonemes\F\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Property PhonemeTargetWeightEeeLipL21 As Single
        ''' <summary>FaceFX Phonemes\F\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Property PhonemeTargetWeightEhLipR21 As Single
        ''' <summary>FaceFX Phonemes\F\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Property PhonemeTargetWeightFVLipTh21 As Single
        ''' <summary>FaceFX Phonemes\F\PHWT\Phoneme Target Weight\I</summary>
        Property PhonemeTargetWeightI21 As Single
        ''' <summary>FaceFX Phonemes\F\PHWT\Phoneme Target Weight\K</summary>
        Property PhonemeTargetWeightK21 As Single
        ''' <summary>FaceFX Phonemes\F\PHWT\Phoneme Target Weight\N</summary>
        Property PhonemeTargetWeightN21 As Single
        ''' <summary>FaceFX Phonemes\F\PHWT\Phoneme Target Weight\Oh</summary>
        Property PhonemeTargetWeightOh21 As Single
        ''' <summary>FaceFX Phonemes\F\PHWT\Phoneme Target Weight\OohQ</summary>
        Property PhonemeTargetWeightOohQ21 As Single
        ''' <summary>FaceFX Phonemes\F\PHWT\Phoneme Target Weight\R</summary>
        Property PhonemeTargetWeightR21 As Single
        ''' <summary>FaceFX Phonemes\F\PHWT\Phoneme Target Weight\TH</summary>
        Property PhonemeTargetWeightTH21 As Single
        ''' <summary>FaceFX Phonemes\F\PHWT\Phoneme Target Weight\W</summary>
        Property PhonemeTargetWeightW21 As Single
        ''' <summary>FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Property PhonemeTargetWeightAahLipBigAah22 As Single
        ''' <summary>FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Property PhonemeTargetWeightBigAahLipDST22 As Single
        ''' <summary>FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Property PhonemeTargetWeightBMPLipEee22 As Single
        ''' <summary>FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Property PhonemeTargetWeightChJshLipFV22 As Single
        ''' <summary>FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Property PhonemeTargetWeightDSTLipK22 As Single
        ''' <summary>FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Property PhonemeTargetWeightEeeLipL22 As Single
        ''' <summary>FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Property PhonemeTargetWeightEhLipR22 As Single
        ''' <summary>FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Property PhonemeTargetWeightFVLipTh22 As Single
        ''' <summary>FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\I</summary>
        Property PhonemeTargetWeightI22 As Single
        ''' <summary>FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\K</summary>
        Property PhonemeTargetWeightK22 As Single
        ''' <summary>FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\N</summary>
        Property PhonemeTargetWeightN22 As Single
        ''' <summary>FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\Oh</summary>
        Property PhonemeTargetWeightOh22 As Single
        ''' <summary>FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\OohQ</summary>
        Property PhonemeTargetWeightOohQ22 As Single
        ''' <summary>FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\R</summary>
        Property PhonemeTargetWeightR22 As Single
        ''' <summary>FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\TH</summary>
        Property PhonemeTargetWeightTH22 As Single
        ''' <summary>FaceFX Phonemes\TH\PHWT\Phoneme Target Weight\W</summary>
        Property PhonemeTargetWeightW22 As Single
        ''' <summary>FaceFX Phonemes\V\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Property PhonemeTargetWeightAahLipBigAah23 As Single
        ''' <summary>FaceFX Phonemes\V\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Property PhonemeTargetWeightBigAahLipDST23 As Single
        ''' <summary>FaceFX Phonemes\V\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Property PhonemeTargetWeightBMPLipEee23 As Single
        ''' <summary>FaceFX Phonemes\V\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Property PhonemeTargetWeightChJshLipFV23 As Single
        ''' <summary>FaceFX Phonemes\V\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Property PhonemeTargetWeightDSTLipK23 As Single
        ''' <summary>FaceFX Phonemes\V\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Property PhonemeTargetWeightEeeLipL23 As Single
        ''' <summary>FaceFX Phonemes\V\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Property PhonemeTargetWeightEhLipR23 As Single
        ''' <summary>FaceFX Phonemes\V\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Property PhonemeTargetWeightFVLipTh23 As Single
        ''' <summary>FaceFX Phonemes\V\PHWT\Phoneme Target Weight\I</summary>
        Property PhonemeTargetWeightI23 As Single
        ''' <summary>FaceFX Phonemes\V\PHWT\Phoneme Target Weight\K</summary>
        Property PhonemeTargetWeightK23 As Single
        ''' <summary>FaceFX Phonemes\V\PHWT\Phoneme Target Weight\N</summary>
        Property PhonemeTargetWeightN23 As Single
        ''' <summary>FaceFX Phonemes\V\PHWT\Phoneme Target Weight\Oh</summary>
        Property PhonemeTargetWeightOh23 As Single
        ''' <summary>FaceFX Phonemes\V\PHWT\Phoneme Target Weight\OohQ</summary>
        Property PhonemeTargetWeightOohQ23 As Single
        ''' <summary>FaceFX Phonemes\V\PHWT\Phoneme Target Weight\R</summary>
        Property PhonemeTargetWeightR23 As Single
        ''' <summary>FaceFX Phonemes\V\PHWT\Phoneme Target Weight\TH</summary>
        Property PhonemeTargetWeightTH23 As Single
        ''' <summary>FaceFX Phonemes\V\PHWT\Phoneme Target Weight\W</summary>
        Property PhonemeTargetWeightW23 As Single
        ''' <summary>FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Property PhonemeTargetWeightAahLipBigAah24 As Single
        ''' <summary>FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Property PhonemeTargetWeightBigAahLipDST24 As Single
        ''' <summary>FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Property PhonemeTargetWeightBMPLipEee24 As Single
        ''' <summary>FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Property PhonemeTargetWeightChJshLipFV24 As Single
        ''' <summary>FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Property PhonemeTargetWeightDSTLipK24 As Single
        ''' <summary>FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Property PhonemeTargetWeightEeeLipL24 As Single
        ''' <summary>FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Property PhonemeTargetWeightEhLipR24 As Single
        ''' <summary>FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Property PhonemeTargetWeightFVLipTh24 As Single
        ''' <summary>FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\I</summary>
        Property PhonemeTargetWeightI24 As Single
        ''' <summary>FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\K</summary>
        Property PhonemeTargetWeightK24 As Single
        ''' <summary>FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\N</summary>
        Property PhonemeTargetWeightN24 As Single
        ''' <summary>FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\Oh</summary>
        Property PhonemeTargetWeightOh24 As Single
        ''' <summary>FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\OohQ</summary>
        Property PhonemeTargetWeightOohQ24 As Single
        ''' <summary>FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\R</summary>
        Property PhonemeTargetWeightR24 As Single
        ''' <summary>FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\TH</summary>
        Property PhonemeTargetWeightTH24 As Single
        ''' <summary>FaceFX Phonemes\DH\PHWT\Phoneme Target Weight\W</summary>
        Property PhonemeTargetWeightW24 As Single
        ''' <summary>FaceFX Phonemes\M\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Property PhonemeTargetWeightAahLipBigAah25 As Single
        ''' <summary>FaceFX Phonemes\M\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Property PhonemeTargetWeightBigAahLipDST25 As Single
        ''' <summary>FaceFX Phonemes\M\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Property PhonemeTargetWeightBMPLipEee25 As Single
        ''' <summary>FaceFX Phonemes\M\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Property PhonemeTargetWeightChJshLipFV25 As Single
        ''' <summary>FaceFX Phonemes\M\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Property PhonemeTargetWeightDSTLipK25 As Single
        ''' <summary>FaceFX Phonemes\M\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Property PhonemeTargetWeightEeeLipL25 As Single
        ''' <summary>FaceFX Phonemes\M\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Property PhonemeTargetWeightEhLipR25 As Single
        ''' <summary>FaceFX Phonemes\M\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Property PhonemeTargetWeightFVLipTh25 As Single
        ''' <summary>FaceFX Phonemes\M\PHWT\Phoneme Target Weight\I</summary>
        Property PhonemeTargetWeightI25 As Single
        ''' <summary>FaceFX Phonemes\M\PHWT\Phoneme Target Weight\K</summary>
        Property PhonemeTargetWeightK25 As Single
        ''' <summary>FaceFX Phonemes\M\PHWT\Phoneme Target Weight\N</summary>
        Property PhonemeTargetWeightN25 As Single
        ''' <summary>FaceFX Phonemes\M\PHWT\Phoneme Target Weight\Oh</summary>
        Property PhonemeTargetWeightOh25 As Single
        ''' <summary>FaceFX Phonemes\M\PHWT\Phoneme Target Weight\OohQ</summary>
        Property PhonemeTargetWeightOohQ25 As Single
        ''' <summary>FaceFX Phonemes\M\PHWT\Phoneme Target Weight\R</summary>
        Property PhonemeTargetWeightR25 As Single
        ''' <summary>FaceFX Phonemes\M\PHWT\Phoneme Target Weight\TH</summary>
        Property PhonemeTargetWeightTH25 As Single
        ''' <summary>FaceFX Phonemes\M\PHWT\Phoneme Target Weight\W</summary>
        Property PhonemeTargetWeightW25 As Single
        ''' <summary>FaceFX Phonemes\N\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Property PhonemeTargetWeightAahLipBigAah26 As Single
        ''' <summary>FaceFX Phonemes\N\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Property PhonemeTargetWeightBigAahLipDST26 As Single
        ''' <summary>FaceFX Phonemes\N\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Property PhonemeTargetWeightBMPLipEee26 As Single
        ''' <summary>FaceFX Phonemes\N\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Property PhonemeTargetWeightChJshLipFV26 As Single
        ''' <summary>FaceFX Phonemes\N\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Property PhonemeTargetWeightDSTLipK26 As Single
        ''' <summary>FaceFX Phonemes\N\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Property PhonemeTargetWeightEeeLipL26 As Single
        ''' <summary>FaceFX Phonemes\N\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Property PhonemeTargetWeightEhLipR26 As Single
        ''' <summary>FaceFX Phonemes\N\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Property PhonemeTargetWeightFVLipTh26 As Single
        ''' <summary>FaceFX Phonemes\N\PHWT\Phoneme Target Weight\I</summary>
        Property PhonemeTargetWeightI26 As Single
        ''' <summary>FaceFX Phonemes\N\PHWT\Phoneme Target Weight\K</summary>
        Property PhonemeTargetWeightK26 As Single
        ''' <summary>FaceFX Phonemes\N\PHWT\Phoneme Target Weight\N</summary>
        Property PhonemeTargetWeightN26 As Single
        ''' <summary>FaceFX Phonemes\N\PHWT\Phoneme Target Weight\Oh</summary>
        Property PhonemeTargetWeightOh26 As Single
        ''' <summary>FaceFX Phonemes\N\PHWT\Phoneme Target Weight\OohQ</summary>
        Property PhonemeTargetWeightOohQ26 As Single
        ''' <summary>FaceFX Phonemes\N\PHWT\Phoneme Target Weight\R</summary>
        Property PhonemeTargetWeightR26 As Single
        ''' <summary>FaceFX Phonemes\N\PHWT\Phoneme Target Weight\TH</summary>
        Property PhonemeTargetWeightTH26 As Single
        ''' <summary>FaceFX Phonemes\N\PHWT\Phoneme Target Weight\W</summary>
        Property PhonemeTargetWeightW26 As Single
        ''' <summary>FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Property PhonemeTargetWeightAahLipBigAah27 As Single
        ''' <summary>FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Property PhonemeTargetWeightBigAahLipDST27 As Single
        ''' <summary>FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Property PhonemeTargetWeightBMPLipEee27 As Single
        ''' <summary>FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Property PhonemeTargetWeightChJshLipFV27 As Single
        ''' <summary>FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Property PhonemeTargetWeightDSTLipK27 As Single
        ''' <summary>FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Property PhonemeTargetWeightEeeLipL27 As Single
        ''' <summary>FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Property PhonemeTargetWeightEhLipR27 As Single
        ''' <summary>FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Property PhonemeTargetWeightFVLipTh27 As Single
        ''' <summary>FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\I</summary>
        Property PhonemeTargetWeightI27 As Single
        ''' <summary>FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\K</summary>
        Property PhonemeTargetWeightK27 As Single
        ''' <summary>FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\N</summary>
        Property PhonemeTargetWeightN27 As Single
        ''' <summary>FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\Oh</summary>
        Property PhonemeTargetWeightOh27 As Single
        ''' <summary>FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\OohQ</summary>
        Property PhonemeTargetWeightOohQ27 As Single
        ''' <summary>FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\R</summary>
        Property PhonemeTargetWeightR27 As Single
        ''' <summary>FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\TH</summary>
        Property PhonemeTargetWeightTH27 As Single
        ''' <summary>FaceFX Phonemes\NG\PHWT\Phoneme Target Weight\W</summary>
        Property PhonemeTargetWeightW27 As Single
        ''' <summary>FaceFX Phonemes\L\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Property PhonemeTargetWeightAahLipBigAah28 As Single
        ''' <summary>FaceFX Phonemes\L\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Property PhonemeTargetWeightBigAahLipDST28 As Single
        ''' <summary>FaceFX Phonemes\L\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Property PhonemeTargetWeightBMPLipEee28 As Single
        ''' <summary>FaceFX Phonemes\L\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Property PhonemeTargetWeightChJshLipFV28 As Single
        ''' <summary>FaceFX Phonemes\L\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Property PhonemeTargetWeightDSTLipK28 As Single
        ''' <summary>FaceFX Phonemes\L\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Property PhonemeTargetWeightEeeLipL28 As Single
        ''' <summary>FaceFX Phonemes\L\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Property PhonemeTargetWeightEhLipR28 As Single
        ''' <summary>FaceFX Phonemes\L\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Property PhonemeTargetWeightFVLipTh28 As Single
        ''' <summary>FaceFX Phonemes\L\PHWT\Phoneme Target Weight\I</summary>
        Property PhonemeTargetWeightI28 As Single
        ''' <summary>FaceFX Phonemes\L\PHWT\Phoneme Target Weight\K</summary>
        Property PhonemeTargetWeightK28 As Single
        ''' <summary>FaceFX Phonemes\L\PHWT\Phoneme Target Weight\N</summary>
        Property PhonemeTargetWeightN28 As Single
        ''' <summary>FaceFX Phonemes\L\PHWT\Phoneme Target Weight\Oh</summary>
        Property PhonemeTargetWeightOh28 As Single
        ''' <summary>FaceFX Phonemes\L\PHWT\Phoneme Target Weight\OohQ</summary>
        Property PhonemeTargetWeightOohQ28 As Single
        ''' <summary>FaceFX Phonemes\L\PHWT\Phoneme Target Weight\R</summary>
        Property PhonemeTargetWeightR28 As Single
        ''' <summary>FaceFX Phonemes\L\PHWT\Phoneme Target Weight\TH</summary>
        Property PhonemeTargetWeightTH28 As Single
        ''' <summary>FaceFX Phonemes\L\PHWT\Phoneme Target Weight\W</summary>
        Property PhonemeTargetWeightW28 As Single
        ''' <summary>FaceFX Phonemes\R\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Property PhonemeTargetWeightAahLipBigAah29 As Single
        ''' <summary>FaceFX Phonemes\R\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Property PhonemeTargetWeightBigAahLipDST29 As Single
        ''' <summary>FaceFX Phonemes\R\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Property PhonemeTargetWeightBMPLipEee29 As Single
        ''' <summary>FaceFX Phonemes\R\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Property PhonemeTargetWeightChJshLipFV29 As Single
        ''' <summary>FaceFX Phonemes\R\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Property PhonemeTargetWeightDSTLipK29 As Single
        ''' <summary>FaceFX Phonemes\R\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Property PhonemeTargetWeightEeeLipL29 As Single
        ''' <summary>FaceFX Phonemes\R\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Property PhonemeTargetWeightEhLipR29 As Single
        ''' <summary>FaceFX Phonemes\R\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Property PhonemeTargetWeightFVLipTh29 As Single
        ''' <summary>FaceFX Phonemes\R\PHWT\Phoneme Target Weight\I</summary>
        Property PhonemeTargetWeightI29 As Single
        ''' <summary>FaceFX Phonemes\R\PHWT\Phoneme Target Weight\K</summary>
        Property PhonemeTargetWeightK29 As Single
        ''' <summary>FaceFX Phonemes\R\PHWT\Phoneme Target Weight\N</summary>
        Property PhonemeTargetWeightN29 As Single
        ''' <summary>FaceFX Phonemes\R\PHWT\Phoneme Target Weight\Oh</summary>
        Property PhonemeTargetWeightOh29 As Single
        ''' <summary>FaceFX Phonemes\R\PHWT\Phoneme Target Weight\OohQ</summary>
        Property PhonemeTargetWeightOohQ29 As Single
        ''' <summary>FaceFX Phonemes\R\PHWT\Phoneme Target Weight\R</summary>
        Property PhonemeTargetWeightR29 As Single
        ''' <summary>FaceFX Phonemes\R\PHWT\Phoneme Target Weight\TH</summary>
        Property PhonemeTargetWeightTH29 As Single
        ''' <summary>FaceFX Phonemes\R\PHWT\Phoneme Target Weight\W</summary>
        Property PhonemeTargetWeightW29 As Single
        ''' <summary>FaceFX Phonemes\W\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Property PhonemeTargetWeightAahLipBigAah30 As Single
        ''' <summary>FaceFX Phonemes\W\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Property PhonemeTargetWeightBigAahLipDST30 As Single
        ''' <summary>FaceFX Phonemes\W\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Property PhonemeTargetWeightBMPLipEee30 As Single
        ''' <summary>FaceFX Phonemes\W\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Property PhonemeTargetWeightChJshLipFV30 As Single
        ''' <summary>FaceFX Phonemes\W\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Property PhonemeTargetWeightDSTLipK30 As Single
        ''' <summary>FaceFX Phonemes\W\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Property PhonemeTargetWeightEeeLipL30 As Single
        ''' <summary>FaceFX Phonemes\W\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Property PhonemeTargetWeightEhLipR30 As Single
        ''' <summary>FaceFX Phonemes\W\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Property PhonemeTargetWeightFVLipTh30 As Single
        ''' <summary>FaceFX Phonemes\W\PHWT\Phoneme Target Weight\I</summary>
        Property PhonemeTargetWeightI30 As Single
        ''' <summary>FaceFX Phonemes\W\PHWT\Phoneme Target Weight\K</summary>
        Property PhonemeTargetWeightK30 As Single
        ''' <summary>FaceFX Phonemes\W\PHWT\Phoneme Target Weight\N</summary>
        Property PhonemeTargetWeightN30 As Single
        ''' <summary>FaceFX Phonemes\W\PHWT\Phoneme Target Weight\Oh</summary>
        Property PhonemeTargetWeightOh30 As Single
        ''' <summary>FaceFX Phonemes\W\PHWT\Phoneme Target Weight\OohQ</summary>
        Property PhonemeTargetWeightOohQ30 As Single
        ''' <summary>FaceFX Phonemes\W\PHWT\Phoneme Target Weight\R</summary>
        Property PhonemeTargetWeightR30 As Single
        ''' <summary>FaceFX Phonemes\W\PHWT\Phoneme Target Weight\TH</summary>
        Property PhonemeTargetWeightTH30 As Single
        ''' <summary>FaceFX Phonemes\W\PHWT\Phoneme Target Weight\W</summary>
        Property PhonemeTargetWeightW30 As Single
        ''' <summary>FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Property PhonemeTargetWeightAahLipBigAah31 As Single
        ''' <summary>FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Property PhonemeTargetWeightBigAahLipDST31 As Single
        ''' <summary>FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Property PhonemeTargetWeightBMPLipEee31 As Single
        ''' <summary>FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Property PhonemeTargetWeightChJshLipFV31 As Single
        ''' <summary>FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Property PhonemeTargetWeightDSTLipK31 As Single
        ''' <summary>FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Property PhonemeTargetWeightEeeLipL31 As Single
        ''' <summary>FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Property PhonemeTargetWeightEhLipR31 As Single
        ''' <summary>FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Property PhonemeTargetWeightFVLipTh31 As Single
        ''' <summary>FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\I</summary>
        Property PhonemeTargetWeightI31 As Single
        ''' <summary>FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\K</summary>
        Property PhonemeTargetWeightK31 As Single
        ''' <summary>FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\N</summary>
        Property PhonemeTargetWeightN31 As Single
        ''' <summary>FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\Oh</summary>
        Property PhonemeTargetWeightOh31 As Single
        ''' <summary>FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\OohQ</summary>
        Property PhonemeTargetWeightOohQ31 As Single
        ''' <summary>FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\R</summary>
        Property PhonemeTargetWeightR31 As Single
        ''' <summary>FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\TH</summary>
        Property PhonemeTargetWeightTH31 As Single
        ''' <summary>FaceFX Phonemes\Y\PHWT\Phoneme Target Weight\W</summary>
        Property PhonemeTargetWeightW31 As Single
        ''' <summary>FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Property PhonemeTargetWeightAahLipBigAah32 As Single
        ''' <summary>FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Property PhonemeTargetWeightBigAahLipDST32 As Single
        ''' <summary>FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Property PhonemeTargetWeightBMPLipEee32 As Single
        ''' <summary>FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Property PhonemeTargetWeightChJshLipFV32 As Single
        ''' <summary>FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Property PhonemeTargetWeightDSTLipK32 As Single
        ''' <summary>FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Property PhonemeTargetWeightEeeLipL32 As Single
        ''' <summary>FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Property PhonemeTargetWeightEhLipR32 As Single
        ''' <summary>FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Property PhonemeTargetWeightFVLipTh32 As Single
        ''' <summary>FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\I</summary>
        Property PhonemeTargetWeightI32 As Single
        ''' <summary>FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\K</summary>
        Property PhonemeTargetWeightK32 As Single
        ''' <summary>FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\N</summary>
        Property PhonemeTargetWeightN32 As Single
        ''' <summary>FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\Oh</summary>
        Property PhonemeTargetWeightOh32 As Single
        ''' <summary>FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\OohQ</summary>
        Property PhonemeTargetWeightOohQ32 As Single
        ''' <summary>FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\R</summary>
        Property PhonemeTargetWeightR32 As Single
        ''' <summary>FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\TH</summary>
        Property PhonemeTargetWeightTH32 As Single
        ''' <summary>FaceFX Phonemes\HH\PHWT\Phoneme Target Weight\W</summary>
        Property PhonemeTargetWeightW32 As Single
        ''' <summary>FaceFX Phonemes\B\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Property PhonemeTargetWeightAahLipBigAah33 As Single
        ''' <summary>FaceFX Phonemes\B\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Property PhonemeTargetWeightBigAahLipDST33 As Single
        ''' <summary>FaceFX Phonemes\B\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Property PhonemeTargetWeightBMPLipEee33 As Single
        ''' <summary>FaceFX Phonemes\B\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Property PhonemeTargetWeightChJshLipFV33 As Single
        ''' <summary>FaceFX Phonemes\B\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Property PhonemeTargetWeightDSTLipK33 As Single
        ''' <summary>FaceFX Phonemes\B\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Property PhonemeTargetWeightEeeLipL33 As Single
        ''' <summary>FaceFX Phonemes\B\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Property PhonemeTargetWeightEhLipR33 As Single
        ''' <summary>FaceFX Phonemes\B\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Property PhonemeTargetWeightFVLipTh33 As Single
        ''' <summary>FaceFX Phonemes\B\PHWT\Phoneme Target Weight\I</summary>
        Property PhonemeTargetWeightI33 As Single
        ''' <summary>FaceFX Phonemes\B\PHWT\Phoneme Target Weight\K</summary>
        Property PhonemeTargetWeightK33 As Single
        ''' <summary>FaceFX Phonemes\B\PHWT\Phoneme Target Weight\N</summary>
        Property PhonemeTargetWeightN33 As Single
        ''' <summary>FaceFX Phonemes\B\PHWT\Phoneme Target Weight\Oh</summary>
        Property PhonemeTargetWeightOh33 As Single
        ''' <summary>FaceFX Phonemes\B\PHWT\Phoneme Target Weight\OohQ</summary>
        Property PhonemeTargetWeightOohQ33 As Single
        ''' <summary>FaceFX Phonemes\B\PHWT\Phoneme Target Weight\R</summary>
        Property PhonemeTargetWeightR33 As Single
        ''' <summary>FaceFX Phonemes\B\PHWT\Phoneme Target Weight\TH</summary>
        Property PhonemeTargetWeightTH33 As Single
        ''' <summary>FaceFX Phonemes\B\PHWT\Phoneme Target Weight\W</summary>
        Property PhonemeTargetWeightW33 As Single
        ''' <summary>FaceFX Phonemes\D\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Property PhonemeTargetWeightAahLipBigAah34 As Single
        ''' <summary>FaceFX Phonemes\D\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Property PhonemeTargetWeightBigAahLipDST34 As Single
        ''' <summary>FaceFX Phonemes\D\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Property PhonemeTargetWeightBMPLipEee34 As Single
        ''' <summary>FaceFX Phonemes\D\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Property PhonemeTargetWeightChJshLipFV34 As Single
        ''' <summary>FaceFX Phonemes\D\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Property PhonemeTargetWeightDSTLipK34 As Single
        ''' <summary>FaceFX Phonemes\D\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Property PhonemeTargetWeightEeeLipL34 As Single
        ''' <summary>FaceFX Phonemes\D\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Property PhonemeTargetWeightEhLipR34 As Single
        ''' <summary>FaceFX Phonemes\D\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Property PhonemeTargetWeightFVLipTh34 As Single
        ''' <summary>FaceFX Phonemes\D\PHWT\Phoneme Target Weight\I</summary>
        Property PhonemeTargetWeightI34 As Single
        ''' <summary>FaceFX Phonemes\D\PHWT\Phoneme Target Weight\K</summary>
        Property PhonemeTargetWeightK34 As Single
        ''' <summary>FaceFX Phonemes\D\PHWT\Phoneme Target Weight\N</summary>
        Property PhonemeTargetWeightN34 As Single
        ''' <summary>FaceFX Phonemes\D\PHWT\Phoneme Target Weight\Oh</summary>
        Property PhonemeTargetWeightOh34 As Single
        ''' <summary>FaceFX Phonemes\D\PHWT\Phoneme Target Weight\OohQ</summary>
        Property PhonemeTargetWeightOohQ34 As Single
        ''' <summary>FaceFX Phonemes\D\PHWT\Phoneme Target Weight\R</summary>
        Property PhonemeTargetWeightR34 As Single
        ''' <summary>FaceFX Phonemes\D\PHWT\Phoneme Target Weight\TH</summary>
        Property PhonemeTargetWeightTH34 As Single
        ''' <summary>FaceFX Phonemes\D\PHWT\Phoneme Target Weight\W</summary>
        Property PhonemeTargetWeightW34 As Single
        ''' <summary>FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Property PhonemeTargetWeightAahLipBigAah35 As Single
        ''' <summary>FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Property PhonemeTargetWeightBigAahLipDST35 As Single
        ''' <summary>FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Property PhonemeTargetWeightBMPLipEee35 As Single
        ''' <summary>FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Property PhonemeTargetWeightChJshLipFV35 As Single
        ''' <summary>FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Property PhonemeTargetWeightDSTLipK35 As Single
        ''' <summary>FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Property PhonemeTargetWeightEeeLipL35 As Single
        ''' <summary>FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Property PhonemeTargetWeightEhLipR35 As Single
        ''' <summary>FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Property PhonemeTargetWeightFVLipTh35 As Single
        ''' <summary>FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\I</summary>
        Property PhonemeTargetWeightI35 As Single
        ''' <summary>FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\K</summary>
        Property PhonemeTargetWeightK35 As Single
        ''' <summary>FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\N</summary>
        Property PhonemeTargetWeightN35 As Single
        ''' <summary>FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\Oh</summary>
        Property PhonemeTargetWeightOh35 As Single
        ''' <summary>FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\OohQ</summary>
        Property PhonemeTargetWeightOohQ35 As Single
        ''' <summary>FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\R</summary>
        Property PhonemeTargetWeightR35 As Single
        ''' <summary>FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\TH</summary>
        Property PhonemeTargetWeightTH35 As Single
        ''' <summary>FaceFX Phonemes\JH\PHWT\Phoneme Target Weight\W</summary>
        Property PhonemeTargetWeightW35 As Single
        ''' <summary>FaceFX Phonemes\G\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Property PhonemeTargetWeightAahLipBigAah36 As Single
        ''' <summary>FaceFX Phonemes\G\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Property PhonemeTargetWeightBigAahLipDST36 As Single
        ''' <summary>FaceFX Phonemes\G\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Property PhonemeTargetWeightBMPLipEee36 As Single
        ''' <summary>FaceFX Phonemes\G\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Property PhonemeTargetWeightChJshLipFV36 As Single
        ''' <summary>FaceFX Phonemes\G\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Property PhonemeTargetWeightDSTLipK36 As Single
        ''' <summary>FaceFX Phonemes\G\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Property PhonemeTargetWeightEeeLipL36 As Single
        ''' <summary>FaceFX Phonemes\G\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Property PhonemeTargetWeightEhLipR36 As Single
        ''' <summary>FaceFX Phonemes\G\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Property PhonemeTargetWeightFVLipTh36 As Single
        ''' <summary>FaceFX Phonemes\G\PHWT\Phoneme Target Weight\I</summary>
        Property PhonemeTargetWeightI36 As Single
        ''' <summary>FaceFX Phonemes\G\PHWT\Phoneme Target Weight\K</summary>
        Property PhonemeTargetWeightK36 As Single
        ''' <summary>FaceFX Phonemes\G\PHWT\Phoneme Target Weight\N</summary>
        Property PhonemeTargetWeightN36 As Single
        ''' <summary>FaceFX Phonemes\G\PHWT\Phoneme Target Weight\Oh</summary>
        Property PhonemeTargetWeightOh36 As Single
        ''' <summary>FaceFX Phonemes\G\PHWT\Phoneme Target Weight\OohQ</summary>
        Property PhonemeTargetWeightOohQ36 As Single
        ''' <summary>FaceFX Phonemes\G\PHWT\Phoneme Target Weight\R</summary>
        Property PhonemeTargetWeightR36 As Single
        ''' <summary>FaceFX Phonemes\G\PHWT\Phoneme Target Weight\TH</summary>
        Property PhonemeTargetWeightTH36 As Single
        ''' <summary>FaceFX Phonemes\G\PHWT\Phoneme Target Weight\W</summary>
        Property PhonemeTargetWeightW36 As Single
        ''' <summary>FaceFX Phonemes\P\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Property PhonemeTargetWeightAahLipBigAah37 As Single
        ''' <summary>FaceFX Phonemes\P\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Property PhonemeTargetWeightBigAahLipDST37 As Single
        ''' <summary>FaceFX Phonemes\P\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Property PhonemeTargetWeightBMPLipEee37 As Single
        ''' <summary>FaceFX Phonemes\P\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Property PhonemeTargetWeightChJshLipFV37 As Single
        ''' <summary>FaceFX Phonemes\P\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Property PhonemeTargetWeightDSTLipK37 As Single
        ''' <summary>FaceFX Phonemes\P\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Property PhonemeTargetWeightEeeLipL37 As Single
        ''' <summary>FaceFX Phonemes\P\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Property PhonemeTargetWeightEhLipR37 As Single
        ''' <summary>FaceFX Phonemes\P\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Property PhonemeTargetWeightFVLipTh37 As Single
        ''' <summary>FaceFX Phonemes\P\PHWT\Phoneme Target Weight\I</summary>
        Property PhonemeTargetWeightI37 As Single
        ''' <summary>FaceFX Phonemes\P\PHWT\Phoneme Target Weight\K</summary>
        Property PhonemeTargetWeightK37 As Single
        ''' <summary>FaceFX Phonemes\P\PHWT\Phoneme Target Weight\N</summary>
        Property PhonemeTargetWeightN37 As Single
        ''' <summary>FaceFX Phonemes\P\PHWT\Phoneme Target Weight\Oh</summary>
        Property PhonemeTargetWeightOh37 As Single
        ''' <summary>FaceFX Phonemes\P\PHWT\Phoneme Target Weight\OohQ</summary>
        Property PhonemeTargetWeightOohQ37 As Single
        ''' <summary>FaceFX Phonemes\P\PHWT\Phoneme Target Weight\R</summary>
        Property PhonemeTargetWeightR37 As Single
        ''' <summary>FaceFX Phonemes\P\PHWT\Phoneme Target Weight\TH</summary>
        Property PhonemeTargetWeightTH37 As Single
        ''' <summary>FaceFX Phonemes\P\PHWT\Phoneme Target Weight\W</summary>
        Property PhonemeTargetWeightW37 As Single
        ''' <summary>FaceFX Phonemes\T\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Property PhonemeTargetWeightAahLipBigAah38 As Single
        ''' <summary>FaceFX Phonemes\T\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Property PhonemeTargetWeightBigAahLipDST38 As Single
        ''' <summary>FaceFX Phonemes\T\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Property PhonemeTargetWeightBMPLipEee38 As Single
        ''' <summary>FaceFX Phonemes\T\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Property PhonemeTargetWeightChJshLipFV38 As Single
        ''' <summary>FaceFX Phonemes\T\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Property PhonemeTargetWeightDSTLipK38 As Single
        ''' <summary>FaceFX Phonemes\T\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Property PhonemeTargetWeightEeeLipL38 As Single
        ''' <summary>FaceFX Phonemes\T\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Property PhonemeTargetWeightEhLipR38 As Single
        ''' <summary>FaceFX Phonemes\T\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Property PhonemeTargetWeightFVLipTh38 As Single
        ''' <summary>FaceFX Phonemes\T\PHWT\Phoneme Target Weight\I</summary>
        Property PhonemeTargetWeightI38 As Single
        ''' <summary>FaceFX Phonemes\T\PHWT\Phoneme Target Weight\K</summary>
        Property PhonemeTargetWeightK38 As Single
        ''' <summary>FaceFX Phonemes\T\PHWT\Phoneme Target Weight\N</summary>
        Property PhonemeTargetWeightN38 As Single
        ''' <summary>FaceFX Phonemes\T\PHWT\Phoneme Target Weight\Oh</summary>
        Property PhonemeTargetWeightOh38 As Single
        ''' <summary>FaceFX Phonemes\T\PHWT\Phoneme Target Weight\OohQ</summary>
        Property PhonemeTargetWeightOohQ38 As Single
        ''' <summary>FaceFX Phonemes\T\PHWT\Phoneme Target Weight\R</summary>
        Property PhonemeTargetWeightR38 As Single
        ''' <summary>FaceFX Phonemes\T\PHWT\Phoneme Target Weight\TH</summary>
        Property PhonemeTargetWeightTH38 As Single
        ''' <summary>FaceFX Phonemes\T\PHWT\Phoneme Target Weight\W</summary>
        Property PhonemeTargetWeightW38 As Single
        ''' <summary>FaceFX Phonemes\K\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Property PhonemeTargetWeightAahLipBigAah39 As Single
        ''' <summary>FaceFX Phonemes\K\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Property PhonemeTargetWeightBigAahLipDST39 As Single
        ''' <summary>FaceFX Phonemes\K\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Property PhonemeTargetWeightBMPLipEee39 As Single
        ''' <summary>FaceFX Phonemes\K\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Property PhonemeTargetWeightChJshLipFV39 As Single
        ''' <summary>FaceFX Phonemes\K\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Property PhonemeTargetWeightDSTLipK39 As Single
        ''' <summary>FaceFX Phonemes\K\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Property PhonemeTargetWeightEeeLipL39 As Single
        ''' <summary>FaceFX Phonemes\K\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Property PhonemeTargetWeightEhLipR39 As Single
        ''' <summary>FaceFX Phonemes\K\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Property PhonemeTargetWeightFVLipTh39 As Single
        ''' <summary>FaceFX Phonemes\K\PHWT\Phoneme Target Weight\I</summary>
        Property PhonemeTargetWeightI39 As Single
        ''' <summary>FaceFX Phonemes\K\PHWT\Phoneme Target Weight\K</summary>
        Property PhonemeTargetWeightK39 As Single
        ''' <summary>FaceFX Phonemes\K\PHWT\Phoneme Target Weight\N</summary>
        Property PhonemeTargetWeightN39 As Single
        ''' <summary>FaceFX Phonemes\K\PHWT\Phoneme Target Weight\Oh</summary>
        Property PhonemeTargetWeightOh39 As Single
        ''' <summary>FaceFX Phonemes\K\PHWT\Phoneme Target Weight\OohQ</summary>
        Property PhonemeTargetWeightOohQ39 As Single
        ''' <summary>FaceFX Phonemes\K\PHWT\Phoneme Target Weight\R</summary>
        Property PhonemeTargetWeightR39 As Single
        ''' <summary>FaceFX Phonemes\K\PHWT\Phoneme Target Weight\TH</summary>
        Property PhonemeTargetWeightTH39 As Single
        ''' <summary>FaceFX Phonemes\K\PHWT\Phoneme Target Weight\W</summary>
        Property PhonemeTargetWeightW39 As Single
        ''' <summary>FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Property PhonemeTargetWeightAahLipBigAah40 As Single
        ''' <summary>FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Property PhonemeTargetWeightBigAahLipDST40 As Single
        ''' <summary>FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Property PhonemeTargetWeightBMPLipEee40 As Single
        ''' <summary>FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Property PhonemeTargetWeightChJshLipFV40 As Single
        ''' <summary>FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Property PhonemeTargetWeightDSTLipK40 As Single
        ''' <summary>FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Property PhonemeTargetWeightEeeLipL40 As Single
        ''' <summary>FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Property PhonemeTargetWeightEhLipR40 As Single
        ''' <summary>FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Property PhonemeTargetWeightFVLipTh40 As Single
        ''' <summary>FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\I</summary>
        Property PhonemeTargetWeightI40 As Single
        ''' <summary>FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\K</summary>
        Property PhonemeTargetWeightK40 As Single
        ''' <summary>FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\N</summary>
        Property PhonemeTargetWeightN40 As Single
        ''' <summary>FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\Oh</summary>
        Property PhonemeTargetWeightOh40 As Single
        ''' <summary>FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\OohQ</summary>
        Property PhonemeTargetWeightOohQ40 As Single
        ''' <summary>FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\R</summary>
        Property PhonemeTargetWeightR40 As Single
        ''' <summary>FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\TH</summary>
        Property PhonemeTargetWeightTH40 As Single
        ''' <summary>FaceFX Phonemes\CH\PHWT\Phoneme Target Weight\W</summary>
        Property PhonemeTargetWeightW40 As Single
        ''' <summary>FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Property PhonemeTargetWeightAahLipBigAah41 As Single
        ''' <summary>FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Property PhonemeTargetWeightBigAahLipDST41 As Single
        ''' <summary>FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Property PhonemeTargetWeightBMPLipEee41 As Single
        ''' <summary>FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Property PhonemeTargetWeightChJshLipFV41 As Single
        ''' <summary>FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Property PhonemeTargetWeightDSTLipK41 As Single
        ''' <summary>FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Property PhonemeTargetWeightEeeLipL41 As Single
        ''' <summary>FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Property PhonemeTargetWeightEhLipR41 As Single
        ''' <summary>FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Property PhonemeTargetWeightFVLipTh41 As Single
        ''' <summary>FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\I</summary>
        Property PhonemeTargetWeightI41 As Single
        ''' <summary>FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\K</summary>
        Property PhonemeTargetWeightK41 As Single
        ''' <summary>FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\N</summary>
        Property PhonemeTargetWeightN41 As Single
        ''' <summary>FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\Oh</summary>
        Property PhonemeTargetWeightOh41 As Single
        ''' <summary>FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\OohQ</summary>
        Property PhonemeTargetWeightOohQ41 As Single
        ''' <summary>FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\R</summary>
        Property PhonemeTargetWeightR41 As Single
        ''' <summary>FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\TH</summary>
        Property PhonemeTargetWeightTH41 As Single
        ''' <summary>FaceFX Phonemes\SIL\PHWT\Phoneme Target Weight\W</summary>
        Property PhonemeTargetWeightW41 As Single
        ''' <summary>FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Property PhonemeTargetWeightAahLipBigAah42 As Single
        ''' <summary>FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Property PhonemeTargetWeightBigAahLipDST42 As Single
        ''' <summary>FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Property PhonemeTargetWeightBMPLipEee42 As Single
        ''' <summary>FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Property PhonemeTargetWeightChJshLipFV42 As Single
        ''' <summary>FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Property PhonemeTargetWeightDSTLipK42 As Single
        ''' <summary>FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Property PhonemeTargetWeightEeeLipL42 As Single
        ''' <summary>FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Property PhonemeTargetWeightEhLipR42 As Single
        ''' <summary>FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Property PhonemeTargetWeightFVLipTh42 As Single
        ''' <summary>FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\I</summary>
        Property PhonemeTargetWeightI42 As Single
        ''' <summary>FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\K</summary>
        Property PhonemeTargetWeightK42 As Single
        ''' <summary>FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\N</summary>
        Property PhonemeTargetWeightN42 As Single
        ''' <summary>FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\Oh</summary>
        Property PhonemeTargetWeightOh42 As Single
        ''' <summary>FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\OohQ</summary>
        Property PhonemeTargetWeightOohQ42 As Single
        ''' <summary>FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\R</summary>
        Property PhonemeTargetWeightR42 As Single
        ''' <summary>FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\TH</summary>
        Property PhonemeTargetWeightTH42 As Single
        ''' <summary>FaceFX Phonemes\SHOTSIL\PHWT\Phoneme Target Weight\W</summary>
        Property PhonemeTargetWeightW42 As Single
        ''' <summary>FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\Aah / LipBigAah</summary>
        Property PhonemeTargetWeightAahLipBigAah43 As Single
        ''' <summary>FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\BigAah / LipDST</summary>
        Property PhonemeTargetWeightBigAahLipDST43 As Single
        ''' <summary>FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\BMP / LipEee</summary>
        Property PhonemeTargetWeightBMPLipEee43 As Single
        ''' <summary>FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\ChJsh / LipFV</summary>
        Property PhonemeTargetWeightChJshLipFV43 As Single
        ''' <summary>FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\DST / LipK</summary>
        Property PhonemeTargetWeightDSTLipK43 As Single
        ''' <summary>FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\Eee / LipL</summary>
        Property PhonemeTargetWeightEeeLipL43 As Single
        ''' <summary>FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\Eh / LipR</summary>
        Property PhonemeTargetWeightEhLipR43 As Single
        ''' <summary>FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\FV / LipTh</summary>
        Property PhonemeTargetWeightFVLipTh43 As Single
        ''' <summary>FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\I</summary>
        Property PhonemeTargetWeightI43 As Single
        ''' <summary>FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\K</summary>
        Property PhonemeTargetWeightK43 As Single
        ''' <summary>FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\N</summary>
        Property PhonemeTargetWeightN43 As Single
        ''' <summary>FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\Oh</summary>
        Property PhonemeTargetWeightOh43 As Single
        ''' <summary>FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\OohQ</summary>
        Property PhonemeTargetWeightOohQ43 As Single
        ''' <summary>FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\R</summary>
        Property PhonemeTargetWeightR43 As Single
        ''' <summary>FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\TH</summary>
        Property PhonemeTargetWeightTH43 As Single
        ''' <summary>FaceFX Phonemes\FLAP\PHWT\Phoneme Target Weight\W</summary>
        Property PhonemeTargetWeightW43 As Single
        ''' <summary>NAM8\Morph Race  -&gt;  RACE. Referencia en el espacio del orden de carga.</summary>
        Property MorphRace As UInteger
        ''' <summary>RNAM\Armor Race  -&gt;  RACE. Referencia en el espacio del orden de carga.</summary>
        Property ArmorRace As UInteger
        ''' <summary>Actor Effects</summary>
        ReadOnly Property ActorEffects As IReadOnlyList(Of IRace_ActorEffects)
        ''' <summary>Keywords\KWDA\Keywords</summary>
        ReadOnly Property Keywords As IReadOnlyList(Of IRace_Keywords)
        ''' <summary>Movement Type Names</summary>
        ReadOnly Property MovementTypeNames As IReadOnlyList(Of IRace_MovementTypeNames)
        ''' <summary>VTCK\Voices</summary>
        ReadOnly Property Voices As IReadOnlyList(Of IRace_Voices)
        ''' <summary>HCLF\Default Hair Colors</summary>
        ReadOnly Property DefaultHairColors As IReadOnlyList(Of IRace_DefaultHairColors)
        ''' <summary>Attacks</summary>
        ReadOnly Property Attacks As IReadOnlyList(Of IRace_Attacks)
        ''' <summary>Body Data\Male Body Data\Parts</summary>
        ReadOnly Property Parts As IReadOnlyList(Of IRace_Parts)
        ''' <summary>Body Data\Female Body Data\Parts</summary>
        ReadOnly Property Parts2 As IReadOnlyList(Of IRace_Parts2)
        ''' <summary>Biped Object Names</summary>
        ReadOnly Property BipedObjectNames As IReadOnlyList(Of IRace_BipedObjectNames)
        ''' <summary>Equip Slots</summary>
        ReadOnly Property EquipSlots As IReadOnlyList(Of IRace_EquipSlots)
        ''' <summary>Phoneme Target Names</summary>
        ReadOnly Property PhonemeTargetNames As IReadOnlyList(Of IRace_PhonemeTargetNames)
    End Interface

    ''' <summary>Un elemento de Actor Effects, en lo que los dos juegos comparten.</summary>
    Public Interface IRace_ActorEffects
        ReadOnly Property Node As WbNode
        ''' <summary>SPLO\Actor Effect  -&gt;  SPEL / LVSP. Referencia en el espacio del orden de carga.</summary>
        Property ActorEffect As UInteger
    End Interface

    ''' <summary>Un elemento de Keywords\KWDA\Keywords, en lo que los dos juegos comparten.</summary>
    Public Interface IRace_Keywords
        ReadOnly Property Node As WbNode
        ''' <summary>Keyword  -&gt;  KYWD / NULL. Referencia en el espacio del orden de carga.</summary>
        Property Keyword As UInteger
    End Interface

    ''' <summary>Un elemento de Movement Type Names, en lo que los dos juegos comparten.</summary>
    Public Interface IRace_MovementTypeNames
        ReadOnly Property Node As WbNode
        ''' <summary>MTNM\Name</summary>
        Property Name As String
    End Interface

    ''' <summary>Un elemento de VTCK\Voices, en lo que los dos juegos comparten.</summary>
    Public Interface IRace_Voices
        ReadOnly Property Node As WbNode
        ''' <summary>Voice  -&gt;  VTYP. Referencia en el espacio del orden de carga.</summary>
        Property Voice As UInteger
    End Interface

    ''' <summary>Un elemento de HCLF\Default Hair Colors, en lo que los dos juegos comparten.</summary>
    Public Interface IRace_DefaultHairColors
        ReadOnly Property Node As WbNode
        ''' <summary>Default Hair Color  -&gt;  NULL / CLFM. Referencia en el espacio del orden de carga.</summary>
        Property DefaultHairColor As UInteger
    End Interface

    ''' <summary>Un elemento de Attacks, en lo que los dos juegos comparten.</summary>
    Public Interface IRace_Attacks
        ReadOnly Property Node As WbNode
        ''' <summary>Attack\ATKD\Attack Data\Damage Mult</summary>
        Property AttackDataDamageMult As Single
        ''' <summary>Attack\ATKD\Attack Data\Attack Chance</summary>
        Property AttackDataAttackChance As Single
        ''' <summary>Attack\ATKD\Attack Data\Attack Spell  -&gt;  SPEL / NULL. Referencia en el espacio del orden de carga.</summary>
        Property AttackDataAttackSpell As UInteger
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
        ''' <summary>Attack\ATKD\Attack Data\Attack Angle</summary>
        Property AttackDataAttackAngle As Single
        ''' <summary>Attack\ATKD\Attack Data\Strike Angle</summary>
        Property AttackDataStrikeAngle As Single
        ''' <summary>Attack\ATKD\Attack Data\Stagger</summary>
        Property AttackDataStagger As Single
        ''' <summary>Attack\ATKD\Attack Data\Knockdown</summary>
        Property AttackDataKnockdown As Single
        ''' <summary>Attack\ATKD\Attack Data\Recovery Time</summary>
        Property AttackDataRecoveryTime As Single
        ''' <summary>Attack\ATKE\Attack Event</summary>
        Property AttackAttackEvent As String
    End Interface

    ''' <summary>Un elemento de Body Data\Male Body Data\Parts, en lo que los dos juegos comparten.</summary>
    Public Interface IRace_Parts
        ReadOnly Property Node As WbNode
        ''' <summary>Part\Model\MODL\Model FileName</summary>
        Property ModelModelFileName As String
    End Interface

    ''' <summary>Un elemento de Body Data\Female Body Data\Parts, en lo que los dos juegos comparten.</summary>
    Public Interface IRace_Parts2
        ReadOnly Property Node As WbNode
        ''' <summary>Part\Model\MODL\Model FileName</summary>
        Property ModelModelFileName As String
    End Interface

    ''' <summary>Un elemento de Biped Object Names, en lo que los dos juegos comparten.</summary>
    Public Interface IRace_BipedObjectNames
        ReadOnly Property Node As WbNode
        ''' <summary>NAME\Name</summary>
        Property Name As String
    End Interface

    ''' <summary>Un elemento de Equip Slots, en lo que los dos juegos comparten.</summary>
    Public Interface IRace_EquipSlots
        ReadOnly Property Node As WbNode
    End Interface

    ''' <summary>Un elemento de Phoneme Target Names, en lo que los dos juegos comparten.</summary>
    Public Interface IRace_PhonemeTargetNames
        ReadOnly Property Node As WbNode
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
        ''' <summary>EDID\Editor ID</summary>
        Property EditorID As String
        ''' <summary>OBND\Object Bounds\X1</summary>
        Property ObjectBoundsX1 As Short
        ''' <summary>OBND\Object Bounds\Y1</summary>
        Property ObjectBoundsY1 As Short
        ''' <summary>OBND\Object Bounds\Z1</summary>
        Property ObjectBoundsZ1 As Short
        ''' <summary>OBND\Object Bounds\X2</summary>
        Property ObjectBoundsX2 As Short
        ''' <summary>OBND\Object Bounds\Y2</summary>
        Property ObjectBoundsY2 As Short
        ''' <summary>OBND\Object Bounds\Z2</summary>
        Property ObjectBoundsZ2 As Short
        ''' <summary>Textures (RGB/A)\TX00\Diffuse</summary>
        Property TexturesRGBADiffuse As String
        ''' <summary>Textures (RGB/A)\TX01\Normal/Gloss</summary>
        Property TexturesRGBANormalGloss As String
        ''' <summary>Textures (RGB/A)\TX04\Height</summary>
        Property TexturesRGBAHeight As String
        ''' <summary>Textures (RGB/A)\TX05\Environment</summary>
        Property TexturesRGBAEnvironment As String
        ''' <summary>Textures (RGB/A)\TX06\Multilayer</summary>
        Property TexturesRGBAMultilayer As String
        ''' <summary>DODT\Decal Data\Min Width</summary>
        Property DecalDataMinWidth As Single
        ''' <summary>DODT\Decal Data\Max Width</summary>
        Property DecalDataMaxWidth As Single
        ''' <summary>DODT\Decal Data\Min Height</summary>
        Property DecalDataMinHeight As Single
        ''' <summary>DODT\Decal Data\Max Height</summary>
        Property DecalDataMaxHeight As Single
        ''' <summary>DODT\Decal Data\Depth</summary>
        Property DecalDataDepth As Single
        ''' <summary>DODT\Decal Data\Shininess</summary>
        Property DecalDataShininess As Single
        ''' <summary>DODT\Decal Data\Parallax\Scale</summary>
        Property ParallaxScale As Single
        ''' <summary>DODT\Decal Data\Parallax\Passes</summary>
        Property ParallaxPasses As Byte
        ''' <summary>DODT\Decal Data\Flags</summary>
        Property DecalDataFlags As Byte
        ''' <summary>Bit 1 de DODT\Decal Data\Flags: Alpha - Blending</summary>
        Property DecalDataFlagsAlphaBlending As Boolean
        ''' <summary>Bit 2 de DODT\Decal Data\Flags: Alpha - Testing</summary>
        Property DecalDataFlagsAlphaTesting As Boolean
        ''' <summary>Bit 3 de DODT\Decal Data\Flags: No Subtextures</summary>
        Property DecalDataFlagsNoSubtextures As Boolean
        ''' <summary>DODT\Decal Data\Color\Red</summary>
        Property ColorRed As Byte
        ''' <summary>DODT\Decal Data\Color\Green</summary>
        Property ColorGreen As Byte
        ''' <summary>DODT\Decal Data\Color\Blue</summary>
        Property ColorBlue As Byte
        ''' <summary>DNAM\Flags</summary>
        Property Flags As UShort
    End Interface

End Namespace
