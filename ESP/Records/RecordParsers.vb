Imports System.Drawing
Imports System.Text

Public Enum NPC_TemplateCategory As Integer
    Traits = 0
    Stats = 1
    Factions = 2
    SpellList = 3
    AIData = 4
    AIPackages = 5
    ModelAnimation = 6
    BaseData = 7
    Inventory = 8
    Script = 9
    DefaultPackageList = 10
    AttackData = 11
    Keywords = 12
End Enum

Public Class NPC_FaceTintLayerData
    ''' <summary>TETI offset 0 (U16) — entry subclass discriminator (verified empirically on FO4 vanilla NPCs,
    ''' 30+ layers across 3 NPCs): 1 = Palette, 2 = TextureSet. TEND layout depends on this value.</summary>
    Public Discriminator As UShort
    ''' <summary>TETI offset 2 (U16) — index into the RACE tint template option.</summary>
    Public Index As UShort
    ''' <summary>TEND offset 0 — intensity / slider value (0..100).</summary>
    Public Value As Integer
    ''' <summary>Palette entries only — TEND bytes 1..3 hold the final applied RGB color. Stays Color.Empty for TextureSet.</summary>
    Public Color As Color = Color.Empty
    ''' <summary>Palette entries only — TEND bytes 5..6 (signed int16) index into the RACE TTEC TemplateColors
    ''' array POSITIONALLY. -1 means "use the TEND RGB directly, no CLFM lookup" (the author picked a custom
    ''' colour not in the palette or the TEND is a cached value). >= 0 means "look up TemplateColors[index]
    ''' and use its CLFM colour + its authored BlendOperation" (the author picked a preset from the palette).
    ''' Verified from wbDefinitionsFO4.pas TEND struct definition.</summary>
    Public TemplateColorIndex As Integer = -1
    ''' <summary>Raw bytes of the TETI subrecord, kept for diagnostic dumps.</summary>
    Public RawTetiBytes As Byte() = Nothing
    ''' <summary>Raw bytes of the TEND subrecord, kept for diagnostic dumps.</summary>
    Public RawTendBytes As Byte() = Nothing
End Class

Public Class NPC_FaceMorphData
    Public Index As UInteger
    ''' <summary>Raw float values from FMRS. Typically: PosX, PosY, PosZ, RotX, RotY, RotZ, Scale, plus optional unknowns.</summary>
    Public Values As New List(Of Single)
    ''' <summary>Raw FMRI bytes (should be 4 = UInt32 index).</summary>
    Public RawFmriBytes As Byte() = Nothing
    ''' <summary>Raw FMRS bytes. xEdit says 7 floats + trailing "Unknown" byte array. Mod overrides may
    ''' include different layouts — keeping the raw bytes lets us verify the structure.</summary>
    Public RawFmrsBytes As Byte() = Nothing
    ''' <summary>Name of the plugin that provides the WINNING FMRI entry, for diagnosing override issues.</summary>
    Public SourcePlugin As String = ""

    Public ReadOnly Property PositionX As Single
        Get
            Return If(Values.Count > 0, Values(0), 0.0F)
        End Get
    End Property
    Public ReadOnly Property PositionY As Single
        Get
            Return If(Values.Count > 1, Values(1), 0.0F)
        End Get
    End Property
    Public ReadOnly Property PositionZ As Single
        Get
            Return If(Values.Count > 2, Values(2), 0.0F)
        End Get
    End Property
    Public ReadOnly Property RotationX As Single
        Get
            Return If(Values.Count > 3, Values(3), 0.0F)
        End Get
    End Property
    Public ReadOnly Property RotationY As Single
        Get
            Return If(Values.Count > 4, Values(4), 0.0F)
        End Get
    End Property
    Public ReadOnly Property RotationZ As Single
        Get
            Return If(Values.Count > 5, Values(5), 0.0F)
        End Get
    End Property
    Public ReadOnly Property Scale As Single
        Get
            Return If(Values.Count > 6, Values(6), 0.0F)
        End Get
    End Property
End Class

Public Class NPC_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public FullName As String = ""
    Public RaceFormID As UInteger
    Public SkinFormID As UInteger
    Public DefaultOutfitFormID As UInteger
    Public SleepOutfitFormID As UInteger
    Public HeadTextureFormID As UInteger
    Public HairColorFormID As UInteger
    Public FacialHairColorFormID As UInteger
    Public HasTextureLighting As Boolean
    Public TextureLightingColor As Color = Color.Empty
    Public HeadPartFormIDs As New List(Of UInteger)
    Public IsFemale As Boolean
    Public WeightThin As Single = 0
    Public WeightMuscular As Single = 0
    Public WeightFat As Single = 0
    Public TemplateFormID As UInteger
    Public TemplateFlags As UShort
    ''' <summary>Raw ACBS Flags (uint32). Bit 2 (0x04) = Is CharGen Face Preset.</summary>
    Public AcbsFlags As UInteger
    Public TemplateActorFormIDs As New Dictionary(Of NPC_TemplateCategory, UInteger)
    Public MorphValues As New Dictionary(Of UInteger, Single)
    Public FaceTintLayers As New List(Of NPC_FaceTintLayerData)
    Public FaceMorphs As New List(Of NPC_FaceMorphData)
    Public BodyMorphRegionValues As New List(Of Single)
    Public FacialMorphIntensity As Single = 1.0F
    Public PluginName As String = ""
    ''' <summary>OMOD FormIDs from the FIRST combination of NPC_.ObjectTemplate (OBTS block).
    ''' Used for robot rendering (Assaultron, Mr Handy, etc.) — vanilla robots declare no mesh
    ''' in ARMO/ARMA; their mesh parts come from OMOD records referenced here. One OMOD per
    ''' body part (Bot_TorsoAssaultron, Bot_ArmLeft, Bot_ArmRight, Bot_Legs, armor mods, etc).
    ''' Spec: wbDefinitionsFO4.pas:5867-5898 (OBTS struct + ObjectTemplate RStruct).
    ''' Future: expose multiple combinations as outfit-like variants (see
    ''' project_robot_rendering_combinations.md). First iteration reads only combination #0.</summary>
    Public ObjectTemplateOMODFormIDs As New List(Of UInteger)

    Public Overrides Function ToString() As String
        If FullName <> "" Then Return $"{FullName} [{EditorID}]"
        Return EditorID
    End Function
End Class

''' <summary>RACE face morph definition (FMRI index -> FMRN name).</summary>
Public Class RACE_FaceMorphDef
    Public Index As UInteger
    Public Name As String = ""
End Class

''' <summary>RACE morph value definition (MSID -> MSM0 min name / MSM1 max name).
''' These map MSDK keys to TriHead morph names for chargen sliders.</summary>
Public Class RACE_MorphValueDef
    Public Index As UInteger   ' MSID = same as MSDK key in NPC record
    Public MinName As String = ""  ' MSM0 = morph name when value is negative (e.g. "BrowDown")
    Public MaxName As String = ""  ' MSM1 = morph name when value is positive (e.g. "BrowUp")
End Class

''' <summary>RACE morph group preset (MPPI -> MPPM morph name).
''' Maps MSDK preset keys to chargen TRI morph names.</summary>
Public Class RACE_MorphPresetDef
    Public Index As UInteger      ' MPPI = same as MSDK key in NPC record
    Public PresetName As String = ""  ' MPPN = display name (localized)
    Public MorphName As String = ""   ' MPPM = morph name in Chargen.tri
    ''' <summary>MPPT = FormID reference to a TXST (TextureSet) record that holds the
    ''' diffuse / normal / wrinkles / specular for this preset. When the NPC's MSDV
    ''' selects this preset via its MPPI hash, the engine uses this TXST's textures
    ''' to replace the corresponding region of the base face, gated by the parent
    ''' Morph Group's MPPK mask. This is how Bethesda implements per-region texture
    ''' swaps (e.g. "Arrugado" forehead -> SkinHeadFemaleOld TXST gated by Female
    ''' Forehead Mask). Verified from wbDefinitionsFO4.pas:3535.</summary>
    Public TextureFormID As UInteger = 0UI   ' MPPT -> TXST
    Public Playable As Boolean = True        ' MPPF
End Class

''' <summary>RACE Morph Group - contains a list of MorphPresetDefs sharing a common
''' face region mask (MPPK) and a group name (MPGN). Each group represents a face
''' region (Forehead, Eyes, Nose, Ears, Cheeks, Mouth, Neck). The NPC picks at most
''' one preset per group via its MSDV values, and the chosen preset's MPPT texture
''' set overrides the base face textures in the region defined by MPPK.
''' Verified from wbDefinitionsFO4.pas:3523 wbMorphGroups.</summary>
Public Class RACE_MorphGroup
    Public Name As String = ""           ' MPGN = "Forehead", "Eyes", etc.
    Public MaskEnum As UShort = 0US      ' MPPK = u16 enum from wbDefinitionsFO4.pas:3538.
    '   Male:   1171..1177 = Forehead..Neck Mask
    '   Female: 1221..1227 = Forehead..Neck Mask
    '   65535 = None
    ' Bethesda comment: "Maps to Faceregion tint groups".
    ' These are SEMANTIC IDs, not array indices. Each value
    ' maps by convention to a Slot (0..6) in TETI.Slot of the
    ' RACE's tint template options. Use TryGetMaskSlot below.
    Public Presets As New List(Of RACE_MorphPresetDef)
    Public SliderIndices As New List(Of UInteger)  ' MPGS = additional slider MSDK keys

    ''' <summary>Translate the MPPK u16 enum to a TintSlot (0..6) for the region masks.
    ''' Returns False if the value is 65535 (None) or out of range.
    ''' MPPK base values from wbDefinitionsFO4.pas:3541,3549 — region masks are stored as a
    ''' contiguous u16 range starting at MaleMaskBase / FemaleMaskBase, in the order
    ''' Forehead, Eyes, Nose, Ears, Cheeks, Mouth, Neck = TintSlot 0..6.</summary>
    Public Function TryGetMaskSlot(ByRef slot As TintSlot) As Boolean
        Const MaleMaskBase As UShort = 1171US
        Const FemaleMaskBase As UShort = 1221US
        Const RegionCount As UShort = 7US
        Dim offset As Integer
        If MaskEnum >= MaleMaskBase AndAlso MaskEnum < MaleMaskBase + RegionCount Then
            offset = CInt(MaskEnum) - CInt(MaleMaskBase)
        ElseIf MaskEnum >= FemaleMaskBase AndAlso MaskEnum < FemaleMaskBase + RegionCount Then
            offset = CInt(MaskEnum) - CInt(FemaleMaskBase)
        Else
            Return False
        End If
        slot = CType(offset, TintSlot)
        Return True
    End Function
End Class

Public Enum TintSlot As UShort
    ForeheadMask = 0
    EyesMask = 1
    NoseMask = 2
    EarsMask = 3
    CheeksMask = 4
    MouthMask = 5
    NeckMask = 6
    LipColor = 7
    CheekColor = 8
    Eyeliner = 9
    EyeSocketUpper = 10
    EyeSocketLower = 11
    SkinTone = 12
    Paint = 13
    LaughLines = 14
    CheekColorLower = 15
    Nose = 16
    Chin = 17
    Neck = 18
    Forehead = 19
    Dirt = 20
    Scars = 21
    FaceDetail = 22
    Brows = 23
    Wrinkles = 24
    Beards = 25
End Enum

''' <summary>Per-bone body weight morph data from a RACE record. Combines two sections from the
''' xEdit Bone Data Set (wbDefinitionsFO4.pas:5901): the Weight Scale Data (BSMS 9 floats = 3 Vec3
''' for Thin/Muscular/Fat absolute scales around 1.0) and the Bone Range Modifier Data (BSMS 4
''' floats = MinY, MinZ, MaxY, MaxZ delta clamps around 0.0). A given bone typically appears in
''' BOTH sections — we merge them into a single entry per bone name.
'''
''' X is always 1.0 in the scale section and absent from the range section, so the body weight
''' morph affects only Y/Z (bone-local). X is the bone's long axis and does not deform.
'''
''' "Range Modifier" interpretation (evidence-based): the Y/Z range is a CLAMP on the weighted
''' scale DELTA (scale - 1). Bethesda authors aggressive raw scale values in BSMS (e.g. Belly Fat
''' Y=1.895) but the range modifier limits how far the delta can actually go (e.g. MaxY=0.15 caps
''' growth at 15%). This keeps the weight morph within anatomically reasonable bounds regardless
''' of how extreme the archetype values are.</summary>
Public Class RACE_BoneData
    Public BoneName As String = ""
    ' --- Weight Scale section (BSMS with 9 floats, absolute scales around 1.0) ---
    Public HasWeightScale As Boolean = False
    Public ThinX As Single = 1.0F
    Public ThinY As Single = 1.0F
    Public ThinZ As Single = 1.0F
    Public MuscularX As Single = 1.0F
    Public MuscularY As Single = 1.0F
    Public MuscularZ As Single = 1.0F
    Public FatX As Single = 1.0F
    Public FatY As Single = 1.0F
    Public FatZ As Single = 1.0F
    ' --- Range Modifier section (BSMS with 4 floats, delta clamps around 0.0) ---
    Public HasRangeModifier As Boolean = False
    Public MinY As Single = 0.0F
    Public MinZ As Single = 0.0F
    Public MaxY As Single = 0.0F
    Public MaxZ As Single = 0.0F
End Class

''' <summary>Per-gender bone data block from a RACE record. A "Bone Data Set" in xEdit terms
''' contains TWO sub-sections for the same gender (Weight Scale Data opened by BSMP, and Bone
''' Range Modifier Data opened by BMMP). Both sections list the same bones with different payload
''' layouts. We merge entries by bone name into a single List(Of RACE_BoneData).</summary>
Public Class RACE_BoneDataGender
    Public Gender As UInteger   ' 0 = Male, 1 = Female per xEdit wbSexEnum
    Public Bones As New List(Of RACE_BoneData)

    ''' <summary>Get existing bone by name or create it. Used by the parser to merge Weight Scale
    ''' and Range Modifier sections into a single per-bone entry.</summary>
    Public Function GetOrCreateBone(name As String) As RACE_BoneData
        For Each b In Bones
            If String.Equals(b.BoneName, name, StringComparison.Ordinal) Then Return b
        Next
        Dim nb As New RACE_BoneData With {.BoneName = name}
        Bones.Add(nb)
        Return nb
    End Function
End Class

Public Enum RACE_BoneDataSection
    None = 0
    WeightScale = 1     ' opened by BSMP, BSMS payload is 9 floats (Thin/Muscular/Fat Vec3)
    RangeModifier = 2   ' opened by BMMP, BSMS payload is 4 floats (MinY, MinZ, MaxY, MaxZ)
End Enum

Public Class RACE_TintTemplateColor
    Public ColorFormID As UInteger
    Public Alpha As Single
    Public TemplateIndex As UShort
    Public BlendOperation As UInteger
End Class

Public Enum RACE_TintEntryType
    ''' <summary>Single texture + blendOp — grayscale mask tinted by a uniform color from TEND.</summary>
    Mask = 0
    ''' <summary>Gradient/lookup texture + CLFM color array — color chosen by template index.</summary>
    Palette = 1
    ''' <summary>Full material (diffuse+normal+specular) pre-colored — TEND carries only intensity.</summary>
    TextureSet = 2
End Enum

Public Class RACE_TintTemplateOption
    Public Slot As UShort
    Public Index As UShort
    Public Name As String = ""
    Public Flags As UShort
    Public Textures As New List(Of String)
    Public BlendOperation As UInteger
    Public HasBlendOperation As Boolean
    Public BlendOpRawBytes As Byte() = Nothing
    Public TemplateColors As New List(Of RACE_TintTemplateColor)
    Public DefaultValue As Single
    Public HasDefaultValue As Boolean

    ''' <summary>Classify by subrecord structure. Verified empirically on HumanRace (162 female options,
    ''' 131 male): the clusters are perfectly separable by Textures.Count + TemplateColors.Count alone:
    '''   T=3  C=0   → TextureSet (diffuse+normal+specular triples, FaceDetail/Scars/Brow slots)
    '''   T=1  C>0   → Palette    (1 gradient mask + TTEC color array, makeup/paint slots)
    '''   T=1  C=0   → Mask       (anatomical region selectors, slots 0..6)
    ''' HasDefaultValue is NOT a reliable discriminator — all Palette entries in HumanRace also have TTED.</summary>
    Public ReadOnly Property EntryType As RACE_TintEntryType
        Get
            If Textures.Count >= 2 Then Return RACE_TintEntryType.TextureSet
            If TemplateColors.Count > 0 Then Return RACE_TintEntryType.Palette
            Return RACE_TintEntryType.Mask
        End Get
    End Property
End Class

Public Class RACE_TintTemplateGroup
    Public GroupName As String = ""
    Public Options As New List(Of RACE_TintTemplateOption)
    Public CategoryIndex As UInteger
End Class

Public Class RACE_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public FullName As String = ""
    Public SkinFormID As UInteger
    ''' <summary>GNAM - Body Part Data FormID (BPTD record). Vanilla HumanRace = HumanRaceBodyPartData
    ''' (0x0003279F). Maps bone names → part types (Torso/Head1/LeftArm1/etc per BPND.PartType enum).
    ''' See TES5Edit wbDefinitionsFO4.pas:11594 for the RACE.GNAM → BPTD reference.</summary>
    Public BodyPartDataFormID As UInteger
    Public MaleSkeletonPath As String = ""
    Public FemaleSkeletonPath As String = ""
    Public MaleBodyMeshes As New List(Of String)
    Public FemaleBodyMeshes As New List(Of String)
    Public MaleHeadPartFormIDs As New List(Of UInteger)
    Public FemaleHeadPartFormIDs As New List(Of UInteger)
    Public MaleFaceDetailTextureFormIDs As New List(Of UInteger)
    Public FemaleFaceDetailTextureFormIDs As New List(Of UInteger)
    Public MaleDefaultFaceTextureFormID As UInteger
    Public FemaleDefaultFaceTextureFormID As UInteger
    Public HairColorLookupTexture As String = ""
    Public HairColorExtendedLookupTexture As String = ""
    ''' <summary>PNAM - FaceGen Main clamp. Limits the effective range of face morph deltas.
    ''' HumanRace default = 5.0. Exact usage TBD — possibly clamps slider*FMIN or output delta.</summary>
    Public FaceGenMainClamp As Single = 0.0F
    ''' <summary>UNAM - FaceGen Face clamp. Possibly a tighter clamp specific to face region morphs.
    ''' HumanRace default = 3.0. Exact usage TBD.</summary>
    Public FaceGenFaceClamp As Single = 0.0F
    Public MaleFaceMorphs As New List(Of RACE_FaceMorphDef)
    Public FemaleFaceMorphs As New List(Of RACE_FaceMorphDef)
    ''' <summary>Morph Values (MSID/MSM0/MSM1) - maps MSDK slider keys to TriHead morph names.</summary>
    Public MorphValues As New List(Of RACE_MorphValueDef)
    ''' <summary>Morph Presets (MPPI/MPPM) from Morph Groups - FLAT list of every preset, kept for
    ''' backward compatibility with NpcMorphResolver which matches MSDV keys to preset defs by Index.
    ''' Each entry is ALSO present inside its owning MaleMorphGroups/FemaleMorphGroups entry.</summary>
    Public MaleMorphPresets As New List(Of RACE_MorphPresetDef)
    Public FemaleMorphPresets As New List(Of RACE_MorphPresetDef)
    ''' <summary>Morph Groups (MPGN/MPPC/MPPK/MPGS wrapping a list of MPPI/MPPN/MPPM/MPPT/MPPF presets)
    ''' — hierarchical structure that ties each preset to its owning face region (via MPPK mask enum)
    ''' and to its per-preset TXST texture (via MPPT). This is how Bethesda implements per-region
    ''' texture swaps (e.g. "Arrugado" forehead -> SkinHeadFemaleOld TXST masked by Female Forehead
    ''' Mask). Parsed alongside the flat MorphPresets lists.</summary>
    Public MaleMorphGroups As New List(Of RACE_MorphGroup)
    Public FemaleMorphGroups As New List(Of RACE_MorphGroup)
    ''' <summary>Morph Group Slider indices (MPGS) - additional MSDK keys per group (flat list).</summary>
    Public MaleMorphGroupSliders As New List(Of UInteger)
    Public FemaleMorphGroupSliders As New List(Of UInteger)
    Public MaleTintTemplateGroups As New List(Of RACE_TintTemplateGroup)
    Public FemaleTintTemplateGroups As New List(Of RACE_TintTemplateGroup)

    ''' <summary>Per-gender bone data from the RACE's BSMP/BSMB/BSMS sequence (xEdit's wbBSMPSequence).
    ''' Each entry has a list of bones, each with a name + a raw array of floats. The float array
    ''' contents are UNDOCUMENTED by xEdit but this is the most likely source of the real per-bone
    ''' morph data (min/max/default positions per slider axis). xEdit marks BMMP as wbUnknown.</summary>
    Public BoneData As New List(Of RACE_BoneDataGender)

    ''' <summary>NNAM subrecord — xEdit spec at wbDefinitionsFO4.pas:11639/11657:
    ''' struct { byte[4] Unknown, float X, float Y }. xEdit calls it "Neck Fat Adjustments Scale"
    ''' but the 4-byte Unknown + the empirical symptom (Neck_skin residual diff in verts even when
    ''' Fat=0) suggests the semantics are not purely fat-weighted. Hypothesis under investigation:
    ''' the 4 bytes may be (thin, muscular, fat, pad) weights that blend the X/Y scale against
    ''' MWGT triangle, so the scale applies regardless of which weight axis is dominant.
    ''' Raw bytes kept alongside X/Y to allow diagnostic interpretation.</summary>
    Public MaleNeckNNAMRaw As Byte() = Nothing
    Public MaleNeckNNAMX As Single = 0.0F
    Public MaleNeckNNAMY As Single = 0.0F
    Public FemaleNeckNNAMRaw As Byte() = Nothing
    Public FemaleNeckNNAMX As Single = 0.0F
    Public FemaleNeckNNAMY As Single = 0.0F

    ''' <summary>RACE.DATA struct per xEdit spec (wbDefinitionsFO4.pas:11439): first 2 floats are
    ''' Male Height and Female Height — scale multipliers applied by the engine to the actor.
    ''' HumanRace vanilla is suspected to have Female Height ≈ 0.98, which would explain the
    ''' systematic 0.98 Z-ratio we observed between our skeleton dict (no height applied) and
    ''' CK's FaceGen bake (height applied at bake time).</summary>
    Public MaleHeight As Single = 1.0F
    Public FemaleHeight As Single = 1.0F

    ''' <summary>Find a tint template option by its TETI index for the given gender.</summary>
    Public Function FindTintOption(index As UShort, isFemale As Boolean) As RACE_TintTemplateOption
        Dim groups = If(isFemale, FemaleTintTemplateGroups, MaleTintTemplateGroups)
        For Each grp In groups
            For Each opt In grp.Options
                If opt.Index = index Then Return opt
            Next
        Next
        Return Nothing
    End Function

    ''' <summary>Collect every tint template option whose TETI.Slot matches the given slot for
    ''' the given gender. Used to resolve a Morph Group's MPPK (region enum) to its actual mask
    ''' textures: MPPK 1221 -> TintSlot.ForeheadMask -> all options with Slot=0 in female tints.
    ''' Each returned option's TTET[0] is a path to the region mask DDS in face UV space.</summary>
    Public Function FindTintOptionsBySlot(slot As TintSlot, isFemale As Boolean) As List(Of RACE_TintTemplateOption)
        Dim result As New List(Of RACE_TintTemplateOption)
        Dim groups = If(isFemale, FemaleTintTemplateGroups, MaleTintTemplateGroups)
        For Each grp In groups
            For Each opt In grp.Options
                If opt.Slot = CUShort(slot) Then result.Add(opt)
            Next
        Next
        Return result
    End Function
End Class

''' <summary>Pareja (Addon Index, ARMA FormID) preservando el INDX que xEdit
''' (wbDefinitionsFO4.pas:6187-6192) reporta. El AddonIndex es la clave que los OMODs usan en
''' su Property "AddonIndex" (wbArmorPropertyEnum idx 7) para seleccionar QUÉ addon de esta lista
''' renderizar — Lite/Mid/Heavy típicamente. ParseARMO conserva la pareja para que el resolver
''' pueda buscar por índice.</summary>
Public Class ARMO_AddonEntry
    Public AddonIndex As UShort
    Public ArmaFormID As UInteger
End Class

''' <summary>Combination del Object Template del ARMO (wbDefinitionsFO4.pas:5867 wbOBTSReq dentro
''' de wbObjectTemplate 5888-5898). Cada Combination tiene Includes (referencias a OMOD records),
''' una lista de keywords que filtra cuándo se aplica esta combination, y un flag Default.
''' El engine selecciona la combination matcheando los keywords del LVLI.LLKC contra esta lista.</summary>
Public Class ARMO_Combination
    Public IsDefault As Boolean
    ''' <summary>Parent Combination Index (s16 @ offset 12 del OBTS payload, wbDefinitionsFO4.pas:5874).
    ''' Display name de xEdit es "Parent Combination Index" pero el handler `wbOBTEAddonIndexToStr` indica
    ''' que el valor representa un AddonIndex selector. Default -1 = "esta combination no fuerza addon
    ''' index — el override viene del OMOD include vía AddonIndex Property". ≥0 = la combination directamente
    ''' selecciona ese AddonIndex group de los Models de la ARMO sin necesidad de OMOD Property.</summary>
    Public ParentCombinationIndex As Integer = -1
    Public Keywords As New List(Of UInteger)
    Public IncludeOMODFormIDs As New List(Of UInteger)
End Class

Public Class ARMO_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public FullName As String = ""
    Public RaceFormID As UInteger
    Public SlotMask As UInteger
    Public TemplateArmorFormID As UInteger
    ''' <summary>Lista plana de FormIDs ARMA — derivada de Models, en orden de aparición.
    ''' Mantenida por compat con consumers existentes; los nuevos consumers deben usar
    ''' <see cref="ArmorAddons"/> para conocer el AddonIndex (INDX) asociado a cada FormID.</summary>
    Public ArmorAddonFormIDs As New List(Of UInteger)
    ''' <summary>Lista de (AddonIndex, ARMA FormID) preservando el INDX del Models array.
    ''' wbDefinitionsFO4.pas:6187-6192. Necesario para resolver OMOD AddonIndex Property override.</summary>
    Public ArmorAddons As New List(Of ARMO_AddonEntry)
    ''' <summary>Base addon index (FNAM byte 2-3 per wbDefinitionsFO4.pas:6198-6202).
    ''' Si el FNAM no está o el valor es 0xFFFF (-1 unsigned), queda en -1 → fallback a 0.</summary>
    Public BaseAddonIndex As Integer = -1
    ''' <summary>Combinations del Object Template (OBTE/OBTS). Permite resolver qué addon
    ''' renderizar via OMOD AddonIndex Property cuando hay keyword match con el LVLI contextual.</summary>
    Public Combinations As New List(Of ARMO_Combination)
    ''' <summary>Male 'World Model' mesh filename (ARMO.MOD2 subrecord per wbDefinitionsFO4.pas:6164).
    ''' Empty for most humanoid armors (mesh lives in ARMA.MaleMeshPath instead), but populated for
    ''' robots / special armors where the mesh is authored at the ARMO level (e.g. Assaultron skin).</summary>
    Public MaleWorldModelPath As String = ""
    ''' <summary>Female 'World Model' mesh filename (ARMO.MOD4 subrecord). Analogous to the male path.</summary>
    Public FemaleWorldModelPath As String = ""
End Class

''' <summary>Per-bone scale delta from an ARMA record's BSMS subrecord. Per TES5Edit
''' wbArmorAddonBoneDataItem: each ARMA can ship its own "Bone Scale Modifier Set" with
''' per-gender per-bone Vec3 deltas that the engine adds on top of RACE.BSMS scaling.
''' Used to shape outfits around the body (e.g. cinched waist, wider hip extension).</summary>
Public Class ARMA_BoneScaleDelta
    Public BoneName As String = ""
    Public DeltaX As Single = 0.0F
    Public DeltaY As Single = 0.0F
    Public DeltaZ As Single = 0.0F
End Class

''' <summary>Per-gender ARMA bone scale modifier block (opened by BSMP in ARMA record).</summary>
Public Class ARMA_BoneScaleGender
    Public Gender As UInteger   ' 0 = Male, 1 = Female per xEdit wbSexEnum
    Public Bones As New List(Of ARMA_BoneScaleDelta)
End Class

Public Class ARMA_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public RaceFormID As UInteger
    Public SlotMask As UInteger
    Public MalePriority As Integer
    Public FemalePriority As Integer
    Public MaleMeshPath As String = ""
    Public FemaleMeshPath As String = ""
    Public MaleFPMeshPath As String = ""
    Public FemaleFPMeshPath As String = ""
    Public MaleSkinTextureFormID As UInteger
    Public FemaleSkinTextureFormID As UInteger
    Public MaleSkinTextureSwapListFormID As UInteger
    Public FemaleSkinTextureSwapListFormID As UInteger
    Public MaleMaterialSwapFormID As UInteger
    Public FemaleMaterialSwapFormID As UInteger
    Public MaleColorRemapIndex As Nullable(Of Single)
    Public FemaleColorRemapIndex As Nullable(Of Single)
    Public AdditionalRaces As New List(Of UInteger)
    ''' <summary>Per-gender bone scale modifier blocks from the ARMA's BSMP/BSMB/BSMS sequence
    ''' (wbArmorAddonBoneDataItem in TES5Edit). Added on top of RACE.BSMS scaling by the engine.</summary>
    Public BoneScaleData As New List(Of ARMA_BoneScaleGender)

    ' --- DNAM trailing fields (byte 2+ of the priorities struct) ---
    ''' <summary>DNAM[2] Male Weight Slider flags. Bit 0x02 = "Enabled" (armor ships separate
    ''' weight morph variants / uses body weight scaling).</summary>
    Public MaleWeightSliderFlags As Byte = 0
    ''' <summary>DNAM[3] Female Weight Slider flags. Bit 0x02 = "Enabled".</summary>
    Public FemaleWeightSliderFlags As Byte = 0
    ''' <summary>DNAM[6] Detection Sound Value.</summary>
    Public DetectionSoundValue As Byte = 0
    ''' <summary>DNAM[8..11] Weapon Adjust (float).</summary>
    Public WeaponAdjust As Single = 0.0F

    ' --- ARMA record HEADER flags (per TES5Edit wbRecord(ARMA, ...)) ---
    ''' <summary>Bit 6 of header flags: "No Underarmor Scaling". When set, the engine does NOT
    ''' apply body weight scaling (RACE.BSMS) to the body under this armor. Parse consumers
    ''' must honor this — applying RACE scaling under an armor that has this flag double-shapes
    ''' the body relative to in-game.</summary>
    Public NoUnderarmorScaling As Boolean = False
    ''' <summary>Bit 9: "Has Sculpt Data". Indicates the armor mesh carries vertex-level sculpt
    ''' morphs (NIF extra data). TBD on how to consume.</summary>
    Public HasSculptData As Boolean = False
    ''' <summary>Bit 30: "Hi-Res 1st Person Only".</summary>
    Public HiRes1stPersonOnly As Boolean = False
End Class

Public Class OTFT_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public ItemFormIDs As New List(Of UInteger)
End Class

Public Class HDPT_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public FullName As String = ""
    Public MeshPath As String = ""
    Public Flags As Byte
    Public PartType As Integer = -1
    Public ColorFormID As UInteger
    Public TextureSetFormID As UInteger
    Public UsesBodyTexture As Boolean
    Public ExtraPartFormIDs As New List(Of UInteger)
    ' NAM0/NAM1 "Parts" array. NAM0 enum: 0=Race Morph, 1=Tri, 2=Chargen Morph.
    ' NAM1 is the .tri file path. Multiple parts may exist per HDPT.
    Public RaceMorphTriPath As String = ""    ' NAM0 = 0 (expression/race morphs, e.g. BaseFemaleHead.tri)
    Public TriPath As String = ""             ' NAM0 = 1 (rarely used)
    Public ChargenMorphTriPath As String = "" ' NAM0 = 2 (chargen sculpting, e.g. BaseFemaleHeadChargen.tri)
End Class

Public Class CLFM_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public FullName As String = ""
    Public Flags As UInteger
    Public HasColor As Boolean
    Public Color As Color = Color.Empty
    Public HasRemappingIndex As Boolean
    Public RemappingIndex As Single
End Class

Public Class TXST_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public DiffuseTexture As String = ""
    Public NormalTexture As String = ""
    Public WrinklesTexture As String = ""
    Public GlowTexture As String = ""
    Public HeightTexture As String = ""
    Public EnvironmentTexture As String = ""
    Public MultilayerTexture As String = ""
    Public SmoothSpecTexture As String = ""
    Public MaterialPath As String = ""
    Public Flags As UShort

    Public ReadOnly Property IsFacegenTextures As Boolean
        Get
            Return (Flags And 2US) <> 0US
        End Get
    End Property
End Class

Public Class LVLN_Entry
    Public Level As UShort
    Public FormID As UInteger
    Public Count As UShort = 1US
    Public ChanceNone As Byte
End Class

Public Class LVLN_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public ChanceNone As Byte
    Public Flags As Byte
    Public Entries As New List(Of LVLN_Entry)
End Class

Public Class LVLI_Entry
    Public Level As UShort
    Public FormID As UInteger
    Public Count As UShort = 1US
    Public ChanceNone As Byte
End Class

''' <summary>Filter Keyword Chance entry from LVLI.LLKC subrecord (wbDefinitionsFO4.pas:10322-10327).
''' Used by LVLIs that resolve to ARMO with multi-addon (Lite/Mid/Heavy) to declare which
''' OBTE/OBTS Combination keyword to match. The engine "tags" the resolved ARMO with the keyword
''' having Chance > 0, then ARMO.Combinations is searched for that keyword to apply OMOD swaps.</summary>
Public Class LVLI_FilterKeyword
    Public KeywordFormID As UInteger
    Public Chance As UInteger
End Class

Public Class LVLI_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public ChanceNone As Byte
    Public Flags As Byte
    Public Entries As New List(Of LVLI_Entry)
    ''' <summary>LLKC entries — keywords con chance que el engine usa para enriquecer el item
    ''' resuelto. Caso típico: outfit Gunner Boss → LVLI con LLKC `if_tmp_armor_Heavy chance=100`
    ''' → ARMO recibe ese keyword → busca su OBTS combination con keyword match → aplica
    ''' OMOD AddonIndex swap → renderiza addon Heavy.</summary>
    Public FilterKeywords As New List(Of LVLI_FilterKeyword)

    Public ReadOnly Property UseAll As Boolean
        Get
            Return (Flags And &H4) <> 0
        End Get
    End Property

    Public ReadOnly Property CalculateEachItemInCount As Boolean
        Get
            Return (Flags And &H2) <> 0
        End Get
    End Property
End Class

Public Class FLST_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public ItemFormIDs As New List(Of UInteger)
End Class

Public Class MSWP_Substitution
    Public OriginalMaterial As String = ""
    Public ReplacementMaterial As String = ""
    Public TreeFolder As String = ""
    Public HasColorRemapIndex As Boolean
    Public ColorRemapIndex As Single
End Class

Public Class MSWP_Data
    Public FormID As UInteger
    Public EditorID As String = ""
    Public TreeFolder As String = ""
    Public Substitutions As New List(Of MSWP_Substitution)
End Class

Public Module RecordParsers

    Private Function ResolveDisplayString(rec As PluginRecord, sr As SubrecordData, pluginManager As PluginManager, Optional kind As LocalizedStringTableKind = LocalizedStringTableKind.Strings) As String
        If pluginManager Is Nothing Then Return sr.AsString
        Return pluginManager.ResolveFieldString(rec, sr, kind)
    End Function

    Private Function ResolveFormIDReference(rec As PluginRecord, rawFormID As UInteger, pluginManager As PluginManager) As UInteger
        If pluginManager Is Nothing OrElse rec Is Nothing Then Return rawFormID
        Return pluginManager.ResolveReferencedFormID(rec.SourcePluginName, rawFormID)
    End Function

    Private Function ResolveFormIDReference(rec As PluginRecord, sr As SubrecordData, pluginManager As PluginManager) As UInteger
        If sr.Data Is Nothing OrElse sr.Data.Length < 4 Then Return 0UI
        Return ResolveFormIDReference(rec, sr.AsUInt32, pluginManager)
    End Function

    ''' <summary>Parse the OBTS payload (Object Mod Template Item, wbOBTSReq @ wbDefinitionsFO4.pas:5867).
    ''' Layout (offsets verified against xEdit wbInterface.pas:13933-13948 prefix rules:
    ''' arCount=-1→u32 prefix, arCount=-2→u16 prefix, arCount=-4→u8 prefix):
    '''   u32 IncludeCount @0
    '''   u32 PropertyCount @4
    '''   u8  LevelMin @8, u8 pad, u8 LevelMax @10, u8 pad
    '''   s16 ParentCombinationIndex @12, u8 Default @14
    '''   u8  KeywordCount @15 (wbArray(..., -4) = 1-byte prefix)
    '''   KeywordCount × u32 @16+ (Keyword FormIDs)
    '''   u8  MinLevelForRanks, u8 AltLevelsPerTier
    '''   IncludeCount × 7 bytes: u32 Mod FormID + u8 AttachPointIdx + u8 Optional + u8 DontUseAll
    '''   PropertyCount × 24 bytes: skipped here (caller resolves Properties via OMOD lookup if needed).
    '''
    ''' Returns a parsed combination, or Nothing if the payload is malformed (length checks).
    ''' Used by both NPC_.OBTS (robot rendering) and ARMO.OBTS (multi-addon keyword swap).</summary>
    Friend Function ParseOBTSPayload(d As Byte(), rec As PluginRecord, pluginManager As PluginManager) As ARMO_Combination
        If d Is Nothing OrElse d.Length < 17 Then Return Nothing
        Dim combo As New ARMO_Combination()
        Dim includeCount = BitConverter.ToUInt32(d, 0)
        ' Offset 12: s16 'Parent Combination Index' (wbDefinitionsFO4.pas:5874). -1 = no override.
        combo.ParentCombinationIndex = CInt(BitConverter.ToInt16(d, 12))
        combo.IsDefault = (d(14) <> 0)
        Dim offset As Integer = 15
        Dim kwCount As Integer = CInt(d(offset))
        offset += 1
        For i = 0 To kwCount - 1
            If offset + 4 > d.Length Then Exit For
            Dim rawKw = BitConverter.ToUInt32(d, offset)
            If rawKw <> 0UI Then combo.Keywords.Add(ResolveFormIDReference(rec, rawKw, pluginManager))
            offset += 4
        Next
        offset += 2 ' MinLevelForRanks + AltLevelsPerTier
        For i = 0 To CInt(includeCount) - 1
            If offset + 7 > d.Length Then Exit For
            Dim rawModFID = BitConverter.ToUInt32(d, offset)
            If rawModFID <> 0UI Then combo.IncludeOMODFormIDs.Add(ResolveFormIDReference(rec, rawModFID, pluginManager))
            offset += 7
        Next
        Return combo
    End Function

    Private Function ParseNormalizedFloatColor(data As Byte()) As Color
        If data Is Nothing OrElse data.Length < 12 Then Return Color.Empty

        Dim red = BitConverter.ToSingle(data, 0)
        Dim green = BitConverter.ToSingle(data, 4)
        Dim blue = BitConverter.ToSingle(data, 8)
        Dim alpha = If(data.Length >= 16, BitConverter.ToSingle(data, 12), 1.0F)

        Return Color.FromArgb(
            NormalizeColorChannel(alpha),
            NormalizeColorChannel(red),
            NormalizeColorChannel(green),
            NormalizeColorChannel(blue))
    End Function

    Private Function NormalizeColorChannel(value As Single) As Integer
        If Single.IsNaN(value) OrElse Single.IsInfinity(value) Then Return 255
        Dim normalized = value
        If normalized <= 1.0F Then normalized *= 255.0F
        Return Math.Max(0, Math.Min(255, CInt(Math.Round(normalized))))
    End Function

    Private Function ParseClfmColor(rawColor As UInteger) As Color
        Dim r = CInt(rawColor And &HFFUI)
        Dim g = CInt((rawColor >> 8) And &HFFUI)
        Dim b = CInt((rawColor >> 16) And &HFFUI)
        Dim a = CInt((rawColor >> 24) And &HFFUI)
        Return Color.FromArgb(a, r, g, b)
    End Function

    Private Function ParseByteColor(data As Byte(), offset As Integer) As Color
        If data Is Nothing OrElse offset < 0 OrElse offset + 4 > data.Length Then Return Color.Empty
        Return Color.FromArgb(data(offset + 3), data(offset), data(offset + 1), data(offset + 2))
    End Function

    Private Function ParseLeveledEntry(rec As PluginRecord, sr As SubrecordData, pluginManager As PluginManager) As LVLN_Entry
        If sr.Data Is Nothing OrElse sr.Data.Length < 8 Then Return Nothing

        Dim entry As New LVLN_Entry With {
            .Level = BitConverter.ToUInt16(sr.Data, 0),
            .FormID = ResolveFormIDReference(rec, BitConverter.ToUInt32(sr.Data, 4), pluginManager)
        }

        If sr.Data.Length >= 10 Then entry.Count = BitConverter.ToUInt16(sr.Data, 8)
        If sr.Data.Length >= 11 Then entry.ChanceNone = sr.Data(10)

        Return entry
    End Function

    Public Function ParseNPC(rec As PluginRecord, pluginName As String, Optional pluginManager As PluginManager = Nothing) As NPC_Data
        Dim npc As New NPC_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID,
            .PluginName = pluginName
        }

        Dim morphKeys As New List(Of UInteger)
        Dim pendingTintLayer As NPC_FaceTintLayerData = Nothing
        Dim pendingFaceMorph As NPC_FaceMorphData = Nothing
        ' ObjectTemplate state: only parse the FIRST OBTS (combination #0) for robots.
        ' Future iteration will capture all combinations as outfit-like variants.
        Dim firstOBTSParsed As Boolean = False

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FULL"
                    npc.FullName = ResolveDisplayString(rec, sr, pluginManager)
                Case "RNAM"
                    npc.RaceFormID = ResolveFormIDReference(rec, sr, pluginManager)
                Case "WNAM"
                    npc.SkinFormID = ResolveFormIDReference(rec, sr, pluginManager)
                Case "DOFT"
                    npc.DefaultOutfitFormID = ResolveFormIDReference(rec, sr, pluginManager)
                Case "SOFT"
                    npc.SleepOutfitFormID = ResolveFormIDReference(rec, sr, pluginManager)
                Case "FTSF", "FTST"
                    npc.HeadTextureFormID = ResolveFormIDReference(rec, sr, pluginManager)
                Case "HCLF"
                    npc.HairColorFormID = ResolveFormIDReference(rec, sr, pluginManager)
                Case "BCLF"
                    npc.FacialHairColorFormID = ResolveFormIDReference(rec, sr, pluginManager)
                Case "QNAM"
                    npc.TextureLightingColor = ParseNormalizedFloatColor(sr.Data)
                    npc.HasTextureLighting = (sr.Data IsNot Nothing AndAlso sr.Data.Length >= 12)
                Case "TPLT"
                    npc.TemplateFormID = ResolveFormIDReference(rec, sr, pluginManager)
                Case "TPTA"
                    If sr.Data IsNot Nothing Then
                        Dim entryCount = Math.Min(sr.Data.Length \ 4, 13)
                        For i = 0 To entryCount - 1
                            Dim rawFormID = BitConverter.ToUInt32(sr.Data, i * 4)
                            If rawFormID = 0UI Then Continue For
                            Dim category = CType(i, NPC_TemplateCategory)
                            npc.TemplateActorFormIDs(category) = ResolveFormIDReference(rec, rawFormID, pluginManager)
                        Next
                    End If
                Case "PNAM"
                    Dim headPartFormID = ResolveFormIDReference(rec, sr, pluginManager)
                    If headPartFormID <> 0UI Then npc.HeadPartFormIDs.Add(headPartFormID)
                Case "ACBS"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        npc.AcbsFlags = BitConverter.ToUInt32(sr.Data, 0)
                        npc.IsFemale = (npc.AcbsFlags And 1UI) <> 0UI
                    End If
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 16 Then
                        npc.TemplateFlags = BitConverter.ToUInt16(sr.Data, 14)
                    End If
                Case "MWGT"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 12 Then
                        npc.WeightThin = BitConverter.ToSingle(sr.Data, 0)
                        npc.WeightMuscular = BitConverter.ToSingle(sr.Data, 4)
                        npc.WeightFat = BitConverter.ToSingle(sr.Data, 8)
                    End If
                Case "MSDK"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        For i = 0 To sr.Data.Length - 4 Step 4
                            morphKeys.Add(BitConverter.ToUInt32(sr.Data, i))
                        Next
                    End If
                Case "MSDV"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        Dim valueCount = Math.Min(morphKeys.Count, sr.Data.Length \ 4)
                        For i = 0 To valueCount - 1
                            npc.MorphValues(morphKeys(i)) = BitConverter.ToSingle(sr.Data, i * 4)
                        Next
                    End If
                Case "TETI"
                    If pendingTintLayer IsNot Nothing Then npc.FaceTintLayers.Add(pendingTintLayer)
                    pendingTintLayer = New NPC_FaceTintLayerData()
                    If sr.Data IsNot Nothing Then
                        pendingTintLayer.RawTetiBytes = sr.Data
                        If sr.Data.Length >= 4 Then
                            pendingTintLayer.Discriminator = BitConverter.ToUInt16(sr.Data, 0)
                            pendingTintLayer.Index = BitConverter.ToUInt16(sr.Data, 2)
                        End If
                    End If
                Case "TEND"
                    ' TEND layout per prior assumption (to verify empirically via raw-byte log):
                    '   Discriminator=1 (Palette)     : 7 bytes = Value(1) + Color(4 RGBA bytes) + TemplateColorIndex(2 signed int16)
                    '   Discriminator=2 (TextureSet)  : 1 byte  = Value(1)
                    If pendingTintLayer Is Nothing Then pendingTintLayer = New NPC_FaceTintLayerData()
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 1 Then
                        pendingTintLayer.RawTendBytes = sr.Data
                        pendingTintLayer.Value = sr.Data(0)
                        If pendingTintLayer.Discriminator = 1 AndAlso sr.Data.Length >= 4 Then
                            pendingTintLayer.Color = Color.FromArgb(255, sr.Data(1), sr.Data(2), sr.Data(3))
                        End If
                        If pendingTintLayer.Discriminator = 1 AndAlso sr.Data.Length >= 7 Then
                            pendingTintLayer.TemplateColorIndex = CInt(BitConverter.ToInt16(sr.Data, 5))
                        End If
                    End If
                    npc.FaceTintLayers.Add(pendingTintLayer)
                    pendingTintLayer = Nothing
                Case "MRSV"
                    npc.BodyMorphRegionValues.Clear()
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        For i = 0 To sr.Data.Length - 4 Step 4
                            npc.BodyMorphRegionValues.Add(BitConverter.ToSingle(sr.Data, i))
                        Next
                    End If
                Case "FMIN"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        npc.FacialMorphIntensity = BitConverter.ToSingle(sr.Data, 0)
                    End If
                Case "FMRI"
                    If pendingFaceMorph IsNot Nothing Then npc.FaceMorphs.Add(pendingFaceMorph)
                    pendingFaceMorph = New NPC_FaceMorphData()
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then pendingFaceMorph.Index = BitConverter.ToUInt32(sr.Data, 0)
                Case "FMRS"
                    If pendingFaceMorph Is Nothing Then pendingFaceMorph = New NPC_FaceMorphData()
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        For i = 0 To sr.Data.Length - 4 Step 4
                            pendingFaceMorph.Values.Add(BitConverter.ToSingle(sr.Data, i))
                        Next
                    End If
                    npc.FaceMorphs.Add(pendingFaceMorph)
                    pendingFaceMorph = Nothing
                Case "OBTS"
                    ' NPC_ flow: only consume the FIRST OBTS (combination #0) for robot rendering
                    ' per project_robot_rendering_combinations.md. ARMO multi-addon OBTS is parsed
                    ' separately in ParseARMO. Helper ParseOBTSPayload encapsulates the binary layout.
                    If Not firstOBTSParsed Then
                        Dim combo = ParseOBTSPayload(sr.Data, rec, pluginManager)
                        If combo IsNot Nothing Then
                            For Each modFID In combo.IncludeOMODFormIDs
                                npc.ObjectTemplateOMODFormIDs.Add(modFID)
                            Next
                            firstOBTSParsed = True
                        End If
                    End If
            End Select
        Next

        If pendingTintLayer IsNot Nothing Then npc.FaceTintLayers.Add(pendingTintLayer)
        If pendingFaceMorph IsNot Nothing Then npc.FaceMorphs.Add(pendingFaceMorph)

        Return npc
    End Function

    Public Function ParseRACE(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As RACE_Data
        Dim race As New RACE_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        Dim inMaleBody As Boolean = False
        Dim inFemaleBody As Boolean = False
        Dim expectingHeadGender As Boolean = False
        Dim inMaleHead As Boolean = False
        Dim inFemaleHead As Boolean = False
        Dim pendingFaceMorphDef As RACE_FaceMorphDef = Nothing
        Dim pendingMorphValueDef As RACE_MorphValueDef = Nothing
        Dim pendingMorphPresetDef As RACE_MorphPresetDef = Nothing
        Dim pendingMorphGroup As RACE_MorphGroup = Nothing
        Dim pendingTintGroup As RACE_TintTemplateGroup = Nothing
        Dim pendingTintOption As RACE_TintTemplateOption = Nothing
        ' Bone Data section state: BSMP opens WeightScale, BMMP opens RangeModifier WITHIN the same
        ' gender block (does NOT create a new block). BSMB + BSMS pairs within each section have
        ' different BSMS payload layouts: 9 floats in WeightScale, 4 floats in RangeModifier.
        Dim currentBoneSection As RACE_BoneDataSection = RACE_BoneDataSection.None
        Dim currentBoneDataGender As RACE_BoneDataGender = Nothing
        Dim currentBoneDataEntry As RACE_BoneData = Nothing

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FULL"
                    race.FullName = ResolveDisplayString(rec, sr, pluginManager)
                Case "WNAM"
                    race.SkinFormID = ResolveFormIDReference(rec, sr, pluginManager)
                Case "GNAM"
                    ' Body Part Data FormID → BPTD record with bone→part-type map.
                    race.BodyPartDataFormID = ResolveFormIDReference(rec, sr, pluginManager)
                Case "ANAM"
                    Dim path = sr.AsString
                    If path <> "" Then
                        If race.MaleSkeletonPath = "" Then
                            race.MaleSkeletonPath = path
                        ElseIf race.FemaleSkeletonPath = "" Then
                            race.FemaleSkeletonPath = path
                        End If
                    End If
                Case "NAM0"
                    expectingHeadGender = True
                    inMaleHead = False
                    inFemaleHead = False
                Case "NAM1"
                    expectingHeadGender = False
                    inMaleHead = False
                    inFemaleHead = False
                    inMaleBody = False
                    inFemaleBody = False
                Case "MNAM"
                    If expectingHeadGender Then
                        inMaleHead = True
                        inFemaleHead = False
                        inMaleBody = False
                        inFemaleBody = False
                        expectingHeadGender = False
                    Else
                        inMaleBody = True
                        inFemaleBody = False
                    End If
                Case "FNAM"
                    If expectingHeadGender Then
                        inFemaleHead = True
                        inMaleHead = False
                        inMaleBody = False
                        inFemaleBody = False
                        expectingHeadGender = False
                    Else
                        inFemaleBody = True
                        inMaleBody = False
                    End If
                Case "MODL"
                    Dim meshPath = sr.AsString
                    If meshPath <> "" AndAlso meshPath.EndsWith(".nif", StringComparison.OrdinalIgnoreCase) Then
                        If inFemaleBody Then
                            race.FemaleBodyMeshes.Add(meshPath)
                        ElseIf inMaleBody Then
                            race.MaleBodyMeshes.Add(meshPath)
                        End If
                    End If
                Case "HEAD"
                    Dim headPartFormID = ResolveFormIDReference(rec, sr, pluginManager)
                    If headPartFormID = 0UI Then Continue For
                    If inFemaleHead Then
                        race.FemaleHeadPartFormIDs.Add(headPartFormID)
                    ElseIf inMaleHead Then
                        race.MaleHeadPartFormIDs.Add(headPartFormID)
                    End If
                Case "FTSM"
                    Dim textureSetId = ResolveFormIDReference(rec, sr, pluginManager)
                    If textureSetId <> 0UI Then race.MaleFaceDetailTextureFormIDs.Add(textureSetId)
                Case "FTSF"
                    Dim textureSetId = ResolveFormIDReference(rec, sr, pluginManager)
                    If textureSetId <> 0UI Then race.FemaleFaceDetailTextureFormIDs.Add(textureSetId)
                Case "DFTM"
                    race.MaleDefaultFaceTextureFormID = ResolveFormIDReference(rec, sr, pluginManager)
                Case "DFTF"
                    race.FemaleDefaultFaceTextureFormID = ResolveFormIDReference(rec, sr, pluginManager)
                Case "HNAM"
                    race.HairColorLookupTexture = sr.AsString
                Case "HLTX"
                    race.HairColorExtendedLookupTexture = sr.AsString
                Case "MPGN"
                    ' Morph Group Name — start of a new Morph Group. Flush any pending preset
                    ' into the previous pending group, then flush the previous group itself.
                    If pendingMorphPresetDef IsNot Nothing AndAlso pendingMorphGroup IsNot Nothing Then
                        pendingMorphGroup.Presets.Add(pendingMorphPresetDef)
                        If inFemaleHead Then race.FemaleMorphPresets.Add(pendingMorphPresetDef)
                        If inMaleHead Then race.MaleMorphPresets.Add(pendingMorphPresetDef)
                        pendingMorphPresetDef = Nothing
                    End If
                    If pendingMorphGroup IsNot Nothing Then
                        If inFemaleHead Then race.FemaleMorphGroups.Add(pendingMorphGroup)
                        If inMaleHead Then race.MaleMorphGroups.Add(pendingMorphGroup)
                    End If
                    pendingMorphGroup = New RACE_MorphGroup()
                    pendingMorphGroup.Name = sr.AsString
                Case "MPPC"
                    ' Morph Preset Count — metadata, not used to split entries (xEdit handles
                    ' the RArray via SetCountPath but we can count presets directly).
                Case "MPPI"
                    ' Morph Group preset definition — flush any pending preset into the current
                    ' group (or the flat list if no group is active), then start a new preset.
                    If pendingMorphPresetDef IsNot Nothing Then
                        If pendingMorphGroup IsNot Nothing Then
                            pendingMorphGroup.Presets.Add(pendingMorphPresetDef)
                        End If
                        If inFemaleHead Then
                            race.FemaleMorphPresets.Add(pendingMorphPresetDef)
                        ElseIf inMaleHead Then
                            race.MaleMorphPresets.Add(pendingMorphPresetDef)
                        End If
                    End If
                    pendingMorphPresetDef = New RACE_MorphPresetDef()
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        pendingMorphPresetDef.Index = BitConverter.ToUInt32(sr.Data, 0)
                    End If
                Case "MPPN"
                    If pendingMorphPresetDef IsNot Nothing Then
                        pendingMorphPresetDef.PresetName = ResolveDisplayString(rec, sr, pluginManager)
                    End If
                Case "MPPM"
                    If pendingMorphPresetDef IsNot Nothing Then
                        pendingMorphPresetDef.MorphName = sr.AsString
                    End If
                Case "MPPT"
                    ' Texture FormID -> TXST for this preset. This is what the engine uses to
                    ' replace the region's base textures when the NPC selects this preset.
                    If pendingMorphPresetDef IsNot Nothing Then
                        pendingMorphPresetDef.TextureFormID = ResolveFormIDReference(rec, sr, pluginManager)
                    End If
                Case "MPPF"
                    If pendingMorphPresetDef IsNot Nothing AndAlso sr.Data IsNot Nothing AndAlso sr.Data.Length >= 1 Then
                        pendingMorphPresetDef.Playable = (sr.Data(0) <> 0)
                    End If
                Case "MPPK"
                    ' Mask enum (uint16) — identifies which face region this group applies to,
                    ' by pointing at a tint template option Index in the RACE's slot 0..6 region
                    ' masks. The actual mask texture lives in that tint option's TTET[0].
                    If pendingMorphGroup IsNot Nothing AndAlso sr.Data IsNot Nothing AndAlso sr.Data.Length >= 2 Then
                        pendingMorphGroup.MaskEnum = BitConverter.ToUInt16(sr.Data, 0)
                    End If
                Case "FMRI"
                    ' Face morph definition start (in RACE context, not NPC)
                    ' Flush pending morph preset into its owning group (if any) + flat list
                    If pendingMorphPresetDef IsNot Nothing Then
                        If pendingMorphGroup IsNot Nothing Then
                            pendingMorphGroup.Presets.Add(pendingMorphPresetDef)
                        End If
                        If inFemaleHead Then
                            race.FemaleMorphPresets.Add(pendingMorphPresetDef)
                        ElseIf inMaleHead Then
                            race.MaleMorphPresets.Add(pendingMorphPresetDef)
                        End If
                        pendingMorphPresetDef = Nothing
                    End If
                    ' Flush pending morph group (all its presets are already folded in above).
                    If pendingMorphGroup IsNot Nothing Then
                        If inFemaleHead Then race.FemaleMorphGroups.Add(pendingMorphGroup)
                        If inMaleHead Then race.MaleMorphGroups.Add(pendingMorphGroup)
                        pendingMorphGroup = Nothing
                    End If
                    If pendingFaceMorphDef IsNot Nothing Then
                        If inFemaleHead Then
                            race.FemaleFaceMorphs.Add(pendingFaceMorphDef)
                        ElseIf inMaleHead Then
                            race.MaleFaceMorphs.Add(pendingFaceMorphDef)
                        End If
                    End If
                    pendingFaceMorphDef = New RACE_FaceMorphDef()
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        pendingFaceMorphDef.Index = BitConverter.ToUInt32(sr.Data, 0)
                    End If
                Case "FMRN"
                    If pendingFaceMorphDef IsNot Nothing Then
                        pendingFaceMorphDef.Name = ResolveDisplayString(rec, sr, pluginManager)
                    End If
                Case "MSID"
                    ' Morph Value definition start
                    If pendingMorphValueDef IsNot Nothing Then race.MorphValues.Add(pendingMorphValueDef)
                    pendingMorphValueDef = New RACE_MorphValueDef()
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        pendingMorphValueDef.Index = BitConverter.ToUInt32(sr.Data, 0)
                    End If
                Case "MSM0"
                    If pendingMorphValueDef IsNot Nothing Then pendingMorphValueDef.MinName = sr.AsString
                Case "MSM1"
                    If pendingMorphValueDef IsNot Nothing Then pendingMorphValueDef.MaxName = sr.AsString
                Case "MPGS"
                    ' Morph Group Sliders - array of uint32 indices. Attach to the pending
                    ' morph group AND keep the flat per-gender list for backward compat.
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        For idx = 0 To sr.Data.Length - 4 Step 4
                            Dim sliderKey = BitConverter.ToUInt32(sr.Data, idx)
                            If pendingMorphGroup IsNot Nothing Then
                                pendingMorphGroup.SliderIndices.Add(sliderKey)
                            End If
                            If inFemaleHead Then
                                race.FemaleMorphGroupSliders.Add(sliderKey)
                            ElseIf inMaleHead Then
                                race.MaleMorphGroupSliders.Add(sliderKey)
                            End If
                        Next
                    End If

                ' --- Tint Template parsing (in HEAD section) ---
                Case "TTGP"
                    Dim name = ResolveDisplayString(rec, sr, pluginManager)
                    If pendingTintOption IsNot Nothing Then
                        ' TTGP after TETI = option name
                        pendingTintOption.Name = name
                    Else
                        ' TTGP without pending option = new group
                        ' Flush previous group
                        If pendingTintGroup IsNot Nothing Then
                            If inFemaleHead Then
                                race.FemaleTintTemplateGroups.Add(pendingTintGroup)
                            ElseIf inMaleHead Then
                                race.MaleTintTemplateGroups.Add(pendingTintGroup)
                            End If
                        End If
                        pendingTintGroup = New RACE_TintTemplateGroup With {.GroupName = name}
                    End If
                Case "TETI"
                    ' Flush pending option into current group
                    If pendingTintOption IsNot Nothing AndAlso pendingTintGroup IsNot Nothing Then
                        pendingTintGroup.Options.Add(pendingTintOption)
                    End If
                    pendingTintOption = New RACE_TintTemplateOption()
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        pendingTintOption.Slot = BitConverter.ToUInt16(sr.Data, 0)
                        pendingTintOption.Index = BitConverter.ToUInt16(sr.Data, 2)
                    End If
                Case "TTEF"
                    If pendingTintOption IsNot Nothing AndAlso sr.Data IsNot Nothing AndAlso sr.Data.Length >= 2 Then
                        pendingTintOption.Flags = BitConverter.ToUInt16(sr.Data, 0)
                    End If
                Case "TTET"
                    If pendingTintOption IsNot Nothing Then
                        pendingTintOption.Textures.Add(sr.AsString)
                    End If
                Case "TTEB"
                    ' xEdit marks this subrecord as wbUnknown — format is not documented publicly.
                    ' We store the raw bytes for empirical analysis and try a U32 BlendOp reading as
                    ' a best-effort (the field may or may not be a plain U32 blend op).
                    If pendingTintOption IsNot Nothing AndAlso sr.Data IsNot Nothing Then
                        pendingTintOption.HasBlendOperation = True
                        pendingTintOption.BlendOpRawBytes = sr.Data
                        If sr.Data.Length >= 4 Then
                            pendingTintOption.BlendOperation = BitConverter.ToUInt32(sr.Data, 0)
                        End If
                    End If
                Case "TTEC"
                    ' Template Colors array - each entry: FormID(4) + Alpha(4) + TemplateIndex(2) + BlendOp(4) = 14 bytes
                    If pendingTintOption IsNot Nothing AndAlso sr.Data IsNot Nothing AndAlso sr.Data.Length >= 14 Then
                        For idx = 0 To sr.Data.Length - 14 Step 14
                            Dim rawFID = BitConverter.ToUInt32(sr.Data, idx)
                            Dim tc As New RACE_TintTemplateColor With {
                                .ColorFormID = ResolveFormIDReference(rec, rawFID, pluginManager),
                                .Alpha = BitConverter.ToSingle(sr.Data, idx + 4),
                                .TemplateIndex = BitConverter.ToUInt16(sr.Data, idx + 8),
                                .BlendOperation = BitConverter.ToUInt32(sr.Data, idx + 10)
                            }
                            pendingTintOption.TemplateColors.Add(tc)
                        Next
                    End If
                Case "TTED"
                    If pendingTintOption IsNot Nothing AndAlso sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        pendingTintOption.DefaultValue = BitConverter.ToSingle(sr.Data, 0)
                        pendingTintOption.HasDefaultValue = True
                    End If
                Case "TTGE"
                    ' Category index — end of group
                    If pendingTintOption IsNot Nothing AndAlso pendingTintGroup IsNot Nothing Then
                        pendingTintGroup.Options.Add(pendingTintOption)
                        pendingTintOption = Nothing
                    End If
                    If pendingTintGroup IsNot Nothing Then
                        If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                            pendingTintGroup.CategoryIndex = BitConverter.ToUInt32(sr.Data, 0)
                        End If
                        If inFemaleHead Then
                            race.FemaleTintTemplateGroups.Add(pendingTintGroup)
                        ElseIf inMaleHead Then
                            race.MaleTintTemplateGroups.Add(pendingTintGroup)
                        End If
                        pendingTintGroup = Nothing
                    End If

                ' --- Bone Data (BSMP/BSMB/BSMS/BMMP) — wbBSMPSequence in xEdit, end of RACE ---
                Case "PNAM"
                    ' FaceGen Main clamp (RACE context, outside head section — inside head section
                    ' PNAM is a head part FormID but that's handled by the HDPT parser, not RACE)
                    If Not inMaleHead AndAlso Not inFemaleHead AndAlso sr.Data IsNot Nothing AndAlso sr.Data.Length = 4 Then
                        race.FaceGenMainClamp = BitConverter.ToSingle(sr.Data, 0)
                    End If
                Case "UNAM"
                    ' FaceGen Face clamp (RACE context only)
                    If Not inMaleHead AndAlso Not inFemaleHead AndAlso sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        race.FaceGenFaceClamp = BitConverter.ToSingle(sr.Data, 0)
                    End If
                Case "DATA"
                    ' RACE.DATA: first 2 floats are Male Height / Female Height (scale multipliers).
                    ' Only read at RACE top level (not inside head sections).
                    If Not inMaleHead AndAlso Not inFemaleHead AndAlso Not inMaleBody AndAlso Not inFemaleBody _
                       AndAlso sr.Data IsNot Nothing AndAlso sr.Data.Length >= 8 Then
                        race.MaleHeight = BitConverter.ToSingle(sr.Data, 0)
                        race.FemaleHeight = BitConverter.ToSingle(sr.Data, 4)
                    End If
                Case "NNAM"
                    ' "Neck Fat Adjustments Scale" per xEdit spec (wbDefinitionsFO4.pas:11639/11657):
                    ' struct { byte[4] Unknown, float X, float Y }. Appears inside Male/Female head
                    ' sections. Semantics of the 4-byte Unknown not documented by xEdit — captured
                    ' raw for diagnostic analysis.
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 12 Then
                        Dim rawBytes(3) As Byte
                        Array.Copy(sr.Data, 0, rawBytes, 0, 4)
                        Dim fx = BitConverter.ToSingle(sr.Data, 4)
                        Dim fy = BitConverter.ToSingle(sr.Data, 8)
                        If inFemaleHead Then
                            race.FemaleNeckNNAMRaw = rawBytes
                            race.FemaleNeckNNAMX = fx
                            race.FemaleNeckNNAMY = fy
                        ElseIf inMaleHead Then
                            race.MaleNeckNNAMRaw = rawBytes
                            race.MaleNeckNNAMX = fx
                            race.MaleNeckNNAMY = fy
                        End If
                    End If

                ' --- Bone Data (BSMP/BSMB/BSMS/BMMP) — wbBSMPSequence in xEdit, end of RACE ---
                Case "BSMP"
                    ' BSMP opens a new Bone Data Set (new gender block) AND simultaneously opens
                    ' the Weight Scale sub-section within that set. Layout verified against
                    ' wbDefinitionsFO4.pas:5901 wbBoneDataItem.
                    Dim block As New RACE_BoneDataGender()
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        block.Gender = BitConverter.ToUInt32(sr.Data, 0)
                    End If
                    race.BoneData.Add(block)
                    currentBoneDataGender = block
                    currentBoneSection = RACE_BoneDataSection.WeightScale
                    currentBoneDataEntry = Nothing
                Case "BMMP"
                    ' BMMP opens the Range Modifier sub-section of the CURRENT bone data set
                    ' (same gender, does NOT create a new block). The u32 payload is the gender
                    ' enum again — we don't need it because we use the current block.
                    If currentBoneDataGender IsNot Nothing Then
                        currentBoneSection = RACE_BoneDataSection.RangeModifier
                        currentBoneDataEntry = Nothing
                    End If
                Case "BSMB"
                    ' Bone name. In either sub-section, this identifies which bone the next BSMS
                    ' payload applies to. We merge both sections into a single RACE_BoneData per
                    ' bone name inside the current gender block.
                    If currentBoneDataGender IsNot Nothing AndAlso currentBoneSection <> RACE_BoneDataSection.None Then
                        currentBoneDataEntry = currentBoneDataGender.GetOrCreateBone(sr.AsString)
                    End If
                Case "BSMS"
                    ' BSMS payload layout depends on the current sub-section:
                    '   WeightScale:   9 floats = 3 Vec3 (Thin, Muscular, Fat) absolute scales around 1.0
                    '   RangeModifier: 4 floats (MinY, MinZ, MaxY, MaxZ) delta clamps around 0.0
                    If currentBoneDataEntry IsNot Nothing AndAlso sr.Data IsNot Nothing Then
                        If currentBoneSection = RACE_BoneDataSection.WeightScale AndAlso sr.Data.Length >= 36 Then
                            currentBoneDataEntry.ThinX = BitConverter.ToSingle(sr.Data, 0)
                            currentBoneDataEntry.ThinY = BitConverter.ToSingle(sr.Data, 4)
                            currentBoneDataEntry.ThinZ = BitConverter.ToSingle(sr.Data, 8)
                            currentBoneDataEntry.MuscularX = BitConverter.ToSingle(sr.Data, 12)
                            currentBoneDataEntry.MuscularY = BitConverter.ToSingle(sr.Data, 16)
                            currentBoneDataEntry.MuscularZ = BitConverter.ToSingle(sr.Data, 20)
                            currentBoneDataEntry.FatX = BitConverter.ToSingle(sr.Data, 24)
                            currentBoneDataEntry.FatY = BitConverter.ToSingle(sr.Data, 28)
                            currentBoneDataEntry.FatZ = BitConverter.ToSingle(sr.Data, 32)
                            currentBoneDataEntry.HasWeightScale = True
                        ElseIf currentBoneSection = RACE_BoneDataSection.RangeModifier AndAlso sr.Data.Length >= 16 Then
                            currentBoneDataEntry.MinY = BitConverter.ToSingle(sr.Data, 0)
                            currentBoneDataEntry.MinZ = BitConverter.ToSingle(sr.Data, 4)
                            currentBoneDataEntry.MaxY = BitConverter.ToSingle(sr.Data, 8)
                            currentBoneDataEntry.MaxZ = BitConverter.ToSingle(sr.Data, 12)
                            currentBoneDataEntry.HasRangeModifier = True
                        End If
                    End If
            End Select
        Next

        ' Flush pending
        If pendingMorphPresetDef IsNot Nothing Then
            If pendingMorphGroup IsNot Nothing Then
                pendingMorphGroup.Presets.Add(pendingMorphPresetDef)
            End If
            If inFemaleHead Then
                race.FemaleMorphPresets.Add(pendingMorphPresetDef)
            ElseIf inMaleHead Then
                race.MaleMorphPresets.Add(pendingMorphPresetDef)
            End If
        End If
        If pendingMorphGroup IsNot Nothing Then
            If inFemaleHead Then race.FemaleMorphGroups.Add(pendingMorphGroup)
            If inMaleHead Then race.MaleMorphGroups.Add(pendingMorphGroup)
        End If
        If pendingMorphValueDef IsNot Nothing Then race.MorphValues.Add(pendingMorphValueDef)
        If pendingFaceMorphDef IsNot Nothing Then
            If inFemaleHead Then
                race.FemaleFaceMorphs.Add(pendingFaceMorphDef)
            ElseIf inMaleHead Then
                race.MaleFaceMorphs.Add(pendingFaceMorphDef)
            End If
        End If
        ' Flush pending tint template
        If pendingTintOption IsNot Nothing AndAlso pendingTintGroup IsNot Nothing Then
            pendingTintGroup.Options.Add(pendingTintOption)
        End If
        If pendingTintGroup IsNot Nothing Then
            If inFemaleHead Then
                race.FemaleTintTemplateGroups.Add(pendingTintGroup)
            ElseIf inMaleHead Then
                race.MaleTintTemplateGroups.Add(pendingTintGroup)
            End If
        End If

        Return race
    End Function

    Public Function ParseARMO(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As ARMO_Data
        Dim armo As New ARMO_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        ' ARMO Models layout (wbDefinitionsFO4.pas:6187-6192):
        '   wbRArray('Models', wbRStruct('Model', [INDX u16 'Addon Index', MODL FormIDCk(ARMA)]))
        ' xEdit emits as INTERLEAVED subrecords: INDX → MODL → INDX → MODL → ...
        ' We track the most recent INDX seen and pair it with the next MODL.
        Dim pendingAddonIndex As UShort = 0US
        Dim hasPendingIndex As Boolean = False

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FULL"
                    armo.FullName = ResolveDisplayString(rec, sr, pluginManager)
                Case "BOD2", "BODT"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then armo.SlotMask = BitConverter.ToUInt32(sr.Data, 0)
                Case "RNAM"
                    armo.RaceFormID = ResolveFormIDReference(rec, sr, pluginManager)
                Case "TNAM"
                    armo.TemplateArmorFormID = ResolveFormIDReference(rec, sr, pluginManager)
                Case "INDX"
                    ' Addon Index (u16) — paired with the next MODL.
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 2 Then
                        pendingAddonIndex = BitConverter.ToUInt16(sr.Data, 0)
                        hasPendingIndex = True
                    End If
                Case "MODL"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length = 4 Then
                        Dim armaFid = ResolveFormIDReference(rec, sr, pluginManager)
                        armo.ArmorAddonFormIDs.Add(armaFid)
                        ' Preserve (Index, FormID) pair. If no INDX preceded this MODL, fall back to
                        ' the position in the list (vanilla typically lists INDX explicitly).
                        Dim idx As UShort = If(hasPendingIndex, pendingAddonIndex, CUShort(armo.ArmorAddons.Count))
                        armo.ArmorAddons.Add(New ARMO_AddonEntry With {
                            .AddonIndex = idx,
                            .ArmaFormID = armaFid
                        })
                        hasPendingIndex = False
                    End If
                Case "FNAM"
                    ' wbDefinitionsFO4.pas:6198-6203: FNAM struct = u16 ArmorRating + u16 BaseAddonIndex + u8 StaggerRating + 3 unused.
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        Dim baseIdx As UShort = BitConverter.ToUInt16(sr.Data, 2)
                        ' Convention: 0xFFFF means "no override"; treat as -1 → fallback to 0 in resolver.
                        If baseIdx = &HFFFFUS Then
                            armo.BaseAddonIndex = -1
                        Else
                            armo.BaseAddonIndex = CInt(baseIdx)
                        End If
                    End If
                Case "OBTS"
                    Dim combo = ParseOBTSPayload(sr.Data, rec, pluginManager)
                    If combo IsNot Nothing Then armo.Combinations.Add(combo)
                Case "MOD2"
                    ' ARMO.Male 'World Model' mesh path — used by robots/special armors where the mesh
                    ' is authored at ARMO level instead of ARMA (e.g. Assaultron skin).
                    armo.MaleWorldModelPath = sr.AsString
                Case "MOD4"
                    ' ARMO.Female 'World Model' mesh path — analogous to MOD2 for females.
                    armo.FemaleWorldModelPath = sr.AsString
            End Select
        Next

        Return armo
    End Function

    Public Function ParseARMA(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As ARMA_Data
        Dim arma As New ARMA_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        ' Header flags per TES5Edit wbRecord(ARMA, ...): bit 6 = No Underarmor Scaling,
        ' bit 9 = Has Sculpt Data, bit 30 = Hi-Res 1st Person Only.
        Dim hdrFlags = rec.Header.Flags
        arma.NoUnderarmorScaling = (hdrFlags And (1UI << 6)) <> 0
        arma.HasSculptData = (hdrFlags And (1UI << 9)) <> 0
        arma.HiRes1stPersonOnly = (hdrFlags And (1UI << 30)) <> 0

        ' State for the trailing Bone Scale Modifier Set block (BSMP opens per-gender,
        ' then interleaved BSMB/BSMS pairs per bone). Structure per TES5Edit
        ' wbArmorAddonBoneDataItem in wbDefinitionsFO4.pas:5949.
        Dim currentBoneScaleGender As ARMA_BoneScaleGender = Nothing
        Dim currentBoneScaleBone As String = Nothing

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "BOD2", "BODT"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then arma.SlotMask = BitConverter.ToUInt32(sr.Data, 0)
                Case "RNAM"
                    arma.RaceFormID = ResolveFormIDReference(rec, sr, pluginManager)
                Case "DNAM"
                    ' Full DNAM layout per TES5Edit: [0]=MalePriority, [1]=FemalePriority,
                    ' [2]=Male weight-slider flags (0x02=Enabled), [3]=Female weight-slider flags,
                    ' [4..5]=Unknown, [6]=DetectionSoundValue, [7]=Unknown, [8..11]=WeaponAdjust(float).
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 2 Then
                        arma.MalePriority = sr.Data(0)
                        arma.FemalePriority = sr.Data(1)
                    End If
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        arma.MaleWeightSliderFlags = sr.Data(2)
                        arma.FemaleWeightSliderFlags = sr.Data(3)
                    End If
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 7 Then
                        arma.DetectionSoundValue = sr.Data(6)
                    End If
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 12 Then
                        arma.WeaponAdjust = BitConverter.ToSingle(sr.Data, 8)
                    End If
                Case "MOD2"
                    arma.MaleMeshPath = sr.AsString
                Case "MOD3"
                    arma.FemaleMeshPath = sr.AsString
                Case "MOD4"
                    arma.MaleFPMeshPath = sr.AsString
                Case "MOD5"
                    arma.FemaleFPMeshPath = sr.AsString
                Case "NAM0", "NAM1", "NAM2", "NAM3"
                    If sr.Data Is Nothing OrElse sr.Data.Length < 4 Then Continue For
                    Select Case sr.Signature
                        Case "NAM0"
                            arma.MaleSkinTextureFormID = ResolveFormIDReference(rec, sr, pluginManager)
                        Case "NAM1"
                            arma.FemaleSkinTextureFormID = ResolveFormIDReference(rec, sr, pluginManager)
                        Case "NAM2"
                            arma.MaleSkinTextureSwapListFormID = ResolveFormIDReference(rec, sr, pluginManager)
                        Case "NAM3"
                            arma.FemaleSkinTextureSwapListFormID = ResolveFormIDReference(rec, sr, pluginManager)
                    End Select
                Case "MO2S"
                    arma.MaleMaterialSwapFormID = ResolveFormIDReference(rec, sr, pluginManager)
                Case "MO3S"
                    arma.FemaleMaterialSwapFormID = ResolveFormIDReference(rec, sr, pluginManager)
                Case "MO2C"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then arma.MaleColorRemapIndex = BitConverter.ToSingle(sr.Data, 0)
                Case "MO3C"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then arma.FemaleColorRemapIndex = BitConverter.ToSingle(sr.Data, 0)
                Case "MODL"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length = 4 Then arma.AdditionalRaces.Add(ResolveFormIDReference(rec, sr, pluginManager))
                Case "BSMP"
                    ' Open a new per-gender Bone Scale Modifier block.
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        currentBoneScaleGender = New ARMA_BoneScaleGender With {
                            .Gender = BitConverter.ToUInt32(sr.Data, 0)
                        }
                        arma.BoneScaleData.Add(currentBoneScaleGender)
                        currentBoneScaleBone = Nothing
                    End If
                Case "BSMB"
                    ' Bone name for the next BSMS delta entry.
                    If currentBoneScaleGender IsNot Nothing Then
                        currentBoneScaleBone = sr.AsString
                    End If
                Case "BSMS"
                    ' Vec3 bone scale delta for the previously-named bone.
                    If currentBoneScaleGender IsNot Nothing AndAlso Not String.IsNullOrEmpty(currentBoneScaleBone) _
                       AndAlso sr.Data IsNot Nothing AndAlso sr.Data.Length >= 12 Then
                        currentBoneScaleGender.Bones.Add(New ARMA_BoneScaleDelta With {
                            .BoneName = currentBoneScaleBone,
                            .DeltaX = BitConverter.ToSingle(sr.Data, 0),
                            .DeltaY = BitConverter.ToSingle(sr.Data, 4),
                            .DeltaZ = BitConverter.ToSingle(sr.Data, 8)
                        })
                        currentBoneScaleBone = Nothing
                    End If
            End Select
        Next

        Return arma
    End Function

    Public Function ParseOTFT(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As OTFT_Data
        Dim otft As New OTFT_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            If sr.Signature = "INAM" AndAlso sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                For i = 0 To sr.Data.Length - 4 Step 4
                    Dim rawFormID = BitConverter.ToUInt32(sr.Data, i)
                    If rawFormID = 0UI Then Continue For
                    otft.ItemFormIDs.Add(ResolveFormIDReference(rec, rawFormID, pluginManager))
                Next
            End If
        Next

        Return otft
    End Function

    Public Function ParseHDPT(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As HDPT_Data
        Dim hdpt As New HDPT_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        ' NAM0 (4-byte uint32) introduces the type of the next NAM1 (string).
        ' Pairs may repeat (typically Race Morph + Chargen Morph for face HDPTs).
        Dim pendingPartType As Integer = -1

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FULL"
                    hdpt.FullName = ResolveDisplayString(rec, sr, pluginManager)
                Case "MODL", "MOD2"
                    If hdpt.MeshPath = "" Then hdpt.MeshPath = sr.AsString
                Case "DATA"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 1 Then
                        hdpt.Flags = sr.Data(0)
                        hdpt.UsesBodyTexture = (hdpt.Flags And &H40) <> 0
                    End If
                Case "HNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        Dim extraPartFormID = ResolveFormIDReference(rec, sr, pluginManager)
                        If extraPartFormID <> 0UI Then hdpt.ExtraPartFormIDs.Add(extraPartFormID)
                    End If
                Case "CNAM"
                    hdpt.ColorFormID = ResolveFormIDReference(rec, sr, pluginManager)
                Case "TNAM"
                    hdpt.TextureSetFormID = ResolveFormIDReference(rec, sr, pluginManager)
                Case "PNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then hdpt.PartType = BitConverter.ToInt32(sr.Data, 0)
                Case "NAM0"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        pendingPartType = BitConverter.ToInt32(sr.Data, 0)
                    End If
                Case "NAM1"
                    Dim path = sr.AsString
                    If Not String.IsNullOrEmpty(path) Then
                        Select Case pendingPartType
                            Case 0 ' Race Morph
                                If hdpt.RaceMorphTriPath = "" Then hdpt.RaceMorphTriPath = path
                            Case 1 ' Tri
                                If hdpt.TriPath = "" Then hdpt.TriPath = path
                            Case 2 ' Chargen Morph
                                If hdpt.ChargenMorphTriPath = "" Then hdpt.ChargenMorphTriPath = path
                        End Select
                    End If
                    pendingPartType = -1
            End Select
        Next

        Return hdpt
    End Function

    Public Function ParseCLFM(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As CLFM_Data
        Dim clfm As New CLFM_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        Dim rawColor As UInteger = 0UI
        Dim hasRawColor As Boolean = False

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FULL"
                    clfm.FullName = ResolveDisplayString(rec, sr, pluginManager)
                Case "CNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        rawColor = sr.AsUInt32
                        hasRawColor = True
                    End If
                Case "FNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then clfm.Flags = sr.AsUInt32
            End Select
        Next

        If hasRawColor Then
            If (clfm.Flags And 2UI) = 0UI Then
                clfm.Color = ParseClfmColor(rawColor)
                clfm.HasColor = True
            Else
                clfm.RemappingIndex = BitConverter.ToSingle(BitConverter.GetBytes(rawColor), 0)
                clfm.HasRemappingIndex = True
            End If
        End If

        Return clfm
    End Function

    Public Function ParseTXST(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As TXST_Data
        Dim txst As New TXST_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "TX00"
                    txst.DiffuseTexture = sr.AsString
                Case "TX01"
                    txst.NormalTexture = sr.AsString
                Case "TX02"
                    txst.WrinklesTexture = sr.AsString
                Case "TX03"
                    txst.GlowTexture = sr.AsString
                Case "TX04"
                    txst.HeightTexture = sr.AsString
                Case "TX05"
                    txst.EnvironmentTexture = sr.AsString
                Case "TX06"
                    txst.MultilayerTexture = sr.AsString
                Case "TX07"
                    txst.SmoothSpecTexture = sr.AsString
                Case "MNAM"
                    txst.MaterialPath = sr.AsString
                Case "DNAM"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 2 Then txst.Flags = BitConverter.ToUInt16(sr.Data, 0)
            End Select
        Next

        Return txst
    End Function

    Public Function ParseLVLN(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As LVLN_Data
        Dim lvln As New LVLN_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "LVLD"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 1 Then lvln.ChanceNone = sr.Data(0)
                Case "LVLF"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 1 Then lvln.Flags = sr.Data(0)
                Case "LVLO"
                    Dim entry = ParseLeveledEntry(rec, sr, pluginManager)
                    If entry IsNot Nothing AndAlso entry.FormID <> 0UI Then lvln.Entries.Add(entry)
            End Select
        Next

        Return lvln
    End Function

    Public Function ParseLVLI(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As LVLI_Data
        Dim lvli As New LVLI_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "LVLD"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 1 Then lvli.ChanceNone = sr.Data(0)
                Case "LVLF"
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 1 Then lvli.Flags = sr.Data(0)
                Case "LVLO"
                    Dim entry = ParseLeveledEntry(rec, sr, pluginManager)
                    If entry Is Nothing OrElse entry.FormID = 0UI Then Continue For
                    lvli.Entries.Add(New LVLI_Entry With {
                        .Level = entry.Level,
                        .FormID = entry.FormID,
                        .Count = entry.Count,
                        .ChanceNone = entry.ChanceNone
                    })
                Case "LLKC"
                    ' Filter Keyword Chance: array of (Keyword FormID u32, Chance u32) per
                    ' wbDefinitionsFO4.pas:10322-10327. xEdit collapses into a single subrecord
                    ' with all entries inside.
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 8 Then
                        Dim count = sr.Data.Length \ 8
                        For i = 0 To count - 1
                            Dim off = i * 8
                            Dim rawKw = BitConverter.ToUInt32(sr.Data, off)
                            Dim chance = BitConverter.ToUInt32(sr.Data, off + 4)
                            If rawKw <> 0UI Then
                                lvli.FilterKeywords.Add(New LVLI_FilterKeyword With {
                                    .KeywordFormID = ResolveFormIDReference(rec, rawKw, pluginManager),
                                    .Chance = chance
                                })
                            End If
                        Next
                    End If
            End Select
        Next

        Return lvli
    End Function

    Public Function ParseFLST(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As FLST_Data
        Dim flst As New FLST_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        For Each sr In rec.Subrecords
            If sr.Signature <> "LNAM" OrElse sr.Data Is Nothing OrElse sr.Data.Length < 4 Then Continue For
            Dim itemId = ResolveFormIDReference(rec, sr, pluginManager)
            If itemId <> 0UI Then flst.ItemFormIDs.Add(itemId)
        Next

        Return flst
    End Function

    Public Function ParseMSWP(rec As PluginRecord, Optional pluginManager As PluginManager = Nothing) As MSWP_Data
        Dim mswp As New MSWP_Data With {
            .FormID = rec.Header.FormID,
            .EditorID = rec.EditorID
        }

        Dim current As MSWP_Substitution = Nothing

        For Each sr In rec.Subrecords
            Select Case sr.Signature
                Case "FNAM"
                    If current Is Nothing Then
                        If mswp.TreeFolder = "" Then mswp.TreeFolder = sr.AsString
                    Else
                        current.TreeFolder = sr.AsString
                    End If
                Case "BNAM"
                    If current IsNot Nothing AndAlso (current.OriginalMaterial <> "" OrElse current.ReplacementMaterial <> "") Then
                        mswp.Substitutions.Add(current)
                    End If
                    current = New MSWP_Substitution With {
                        .OriginalMaterial = sr.AsString
                    }
                Case "SNAM"
                    If current Is Nothing Then current = New MSWP_Substitution()
                    current.ReplacementMaterial = sr.AsString
                Case "CNAM"
                    If current Is Nothing Then current = New MSWP_Substitution()
                    If sr.Data IsNot Nothing AndAlso sr.Data.Length >= 4 Then
                        current.ColorRemapIndex = BitConverter.ToSingle(sr.Data, 0)
                        current.HasColorRemapIndex = True
                    End If
            End Select
        Next

        If current IsNot Nothing AndAlso (current.OriginalMaterial <> "" OrElse current.ReplacementMaterial <> "") Then
            mswp.Substitutions.Add(current)
        End If

        Return mswp
    End Function
End Module

