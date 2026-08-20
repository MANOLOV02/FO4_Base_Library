Imports System.IO
Imports System.Linq
Imports System.Text
Imports FO4_Base_Library.Canon.CanonInterpretacion

' Save NPC ESP/ESM — emite un plugin de Bethesda con uno o más overrides de NPC_, con limpieza de masters
' (el algoritmo CleanMasters de xEdit).
'
' Secuencia: se recolectan los FormID que referencia cada override y se mapean a (plugin origen, ObjectID
' local) → se arma la MAST list nueva (el master del juego siempre primero, después todo plugin que sea
' dueño de algún FormID referenciado, ordenados por load order) → con eso se arma el remapper
' (global → (nuevo índice MAST << 24) | ObjectID) → se serializa cada NPC_ con ese remapper, se envuelven
' en un GRUP y se emite el TES4. La escritura es atómica: .tmp + fsync + rename.
'
' Al actualizar un plugin existente, los records ajenos se re-emiten VERBATIM, pero sus FormID y los
' embebidos dentro de ellos se remapean contra la MAST list NUEVA — que es justamente lo que hace
' CleanMasters cuando la lista de masters se corre.

Public Module SaveNpcEspWriter

    ''' <summary>One NPC_ override to write. Caller provides the type-safe parse model
    ''' and the source plugin name (for FormID master resolution).</summary>
    Public Class NpcOverrideEntry
        ''' <summary>The parsed NPC_Data with all subrecords captured. Required.</summary>
        Public Npc As NPC_Data
        ''' <summary>Plugin name (with extension, e.g. "Fallout4.esm") that originally
        ''' DEFINES this NPC. Used to resolve the FormID's source master and emit the
        ''' record FormID on the override side.</summary>
        Public SourcePluginName As String
        ''' <summary>Original record header (Flags, VCS, Version) — preserved verbatim
        ''' when re-emitting except for DataSize (recomputed) and the IsCompressed flag
        ''' which we always strip (we emit uncompressed bodies for tooling friendliness).</summary>
        Public OriginalHeader As RecordHeader
    End Class

    ''' <summary>One NPC_ NEW record (not override) to write into the plugin. The writer assigns a real
    ''' self-index FormID via draftRemap exactly like NEW <see cref="OtftRecordEntry"/> / <see cref="LvliRecordEntry"/>:
    ''' the caller hands a PROVISIONAL sentinel (high byte 0xFF) in <see cref="ProvisionalFormID"/>; the writer
    ''' rewrites every reference to it (including the record's own header FormID) through the single remapper.
    ''' Use case: cloning a vanilla NPC into N variants with distinct FormIDs (debug tools, derivation experiments,
    ''' or future "Duplicate NPC" features in NPC_Manager). The caller fully populates <see cref="NpcData"/> with
    ''' the modifications (EditorID, TINI/TINC/TIAS for tint intensity, MSDV for morph values, etc.) BEFORE passing.
    ''' Record header values (Flags, VCS1, Version, VCS2) are emitted as defaults — see SerializeNpcCreateRecord.
    ''' All FormID references inside NpcData (RNAM, HEAD parts, factions, etc.) are remapped against the new
    ''' MAST list — the master discovery walk includes creates so referenced plugins land in the MAST list.</summary>
    Public Class NpcCreateEntry
        ''' <summary>The fully-populated NPC_Data to serialize. Caller is responsible for modifying any fields
        ''' (TintLayerStructs, MorphValues, EditorID, etc.) BEFORE passing. The writer does NOT mutate this.</summary>
        Public NpcData As NPC_Data
        ''' <summary>Provisional sentinel FormID (high byte 0xFF). Used by the writer to allocate a real
        ''' self-index FormID and remap every reference to this NPC. Each create entry must have a unique
        ''' provisional FormID (caller responsibility — typically counter-based: 0xFFFFFFFF, 0xFFFFFFFE, ...).</summary>
        Public ProvisionalFormID As UInteger
        ''' <summary>Plugin name (with extension, e.g. "Fallout4.esm") used as the "source" context for FormID
        ''' master resolution of references INSIDE the cloned NpcData (RNAM=race, HDPT head parts, etc.).
        ''' Typically the plugin that owns the base NPC being cloned. Optional: defaults to game master if empty.</summary>
        Public BaseSourcePluginName As String = ""
    End Class

    ''' <summary>One OTFT (outfit) record to write into the same plugin as the NPC override(s).
    ''' Authored in the Edit Outfit "Create" tab. Two flavours:
    '''   • NEW (IsOverride=False): a brand-new outfit owned by this plugin. <see cref="FormID"/> is
    '''     the caller's PROVISIONAL sentinel (high byte 0xFF); the writer assigns the real plugin
    '''     self-index FormID ((masterCount &lt;&lt; 24) | objIndex, objIndex≥0x800 per xEdit) and remaps
    '''     every reference to it (notably the NPC.DOFT that points at the provisional).
    '''   • OVERRIDE (IsOverride=True): edit of an existing OTFT keeping its EditorID. <see cref="FormID"/>
    '''     is that record's real global FormID; emitted as an override (master index remapped).
    ''' Body = EDID + INAM (array of ARMO/LVLI FormIDs, remapped against the new MAST list).</summary>
    Public Class OtftRecordEntry
        ''' <summary>New: provisional sentinel (0xFF…). Override: the existing OTFT's real global FormID.</summary>
        Public FormID As UInteger
        Public EditorID As String = ""
        Public ItemArmoFormIDs As New List(Of UInteger)
        Public IsOverride As Boolean
        ''' <summary>VCS1/VCS2 preserved from the source record on preserve-existing overrides — kept
        ''' verbatim so a re-save doesn't bump the version counters CK uses for conflict detection.
        ''' Defaults to zero for NEW drafts (no source). xEdit preserves these on round-trip; mirror.</summary>
        Public OriginalVcs1 As UInteger
        Public OriginalVcs2 As UShort
    End Class

    ''' <summary>One LVLI (leveled item) record to write into the same plugin — a leveled list authored in
    ''' the Edit Outfit editor's "New LVL…" flow. ALWAYS new (owned by this plugin): <see cref="FormID"/>
    ''' is the caller's PROVISIONAL sentinel (high byte 0xFF), assigned a real self-index FormID by the
    ''' writer exactly like a NEW <see cref="OtftRecordEntry"/>. Because every draft (OTFT + LVLI) is pre-
    ''' assigned its real FormID in <c>draftRemap</c> BEFORE any record is serialized, references between
    ''' drafts resolve through the single remapper regardless of emit order — an OTFT.INAM pointing at a
    ''' draft LVLI, and a draft LVLI's LVLO pointing at another draft LVLI, both rewrite to the real
    ''' self-index FormIDs. Body layout (wbDefinitionsFO4.pas:10352): EDID + OBND + LVLD + LVLM + LVLF +
    ''' LLCT + N×LVLO (12 bytes each, wbDefinitionsCommon.pas:5704).</summary>
    Public Class LvliRecordEntry
        ''' <summary>NEW: provisional sentinel (0xFF…), rewritten to the real self-index FormID by the writer.
        ''' OVERRIDE: the existing LVLI's real global FormID (master-remapped on emit), e.g. an LVLI authored in
        ''' a prior save and re-preserved when updating the same plugin.</summary>
        Public FormID As UInteger
        Public EditorID As String = ""
        ''' <summary>OBND raw 12 bytes (6×s16). Set from <see cref="LVLI_Data.ObjectBoundsRaw"/> on preserve-existing
        ''' overrides so the writer preserves the source-LVLI's bounds verbatim. NEW drafts leave it Nothing →
        ''' writer emits 12 zero bytes (still valid per spec).</summary>
        Public ObjectBoundsRaw As Byte() = Nothing
        ''' <summary>LVLD — whole-list chance of yielding nothing (0-100).</summary>
        Public ChanceNone As Byte
        ''' <summary>LVLM — Max Count (0 = unlimited).</summary>
        Public MaxCount As Byte
        ''' <summary>LVLF — packed flag byte (0x01 all-levels, 0x02 each-in-count, 0x04 use-all).</summary>
        Public Flags As Byte
        Public Entries As New List(Of LvliEntryData)
        ''' <summary>True = override an existing LVLI (keep its real FormID + EditorID). False = brand-new
        ''' (draft) list assigned a self-index FormID.</summary>
        Public IsOverride As Boolean
        ''' <summary>LVLG — Use Global, FormID [GLOB] (wbDefinitionsFO4.pas:10362). Optional.
        ''' Set together with <see cref="UseGlobalFormID"/> on preserve-existing overrides.</summary>
        Public HasUseGlobal As Boolean
        Public UseGlobalFormID As UInteger
        ''' <summary>LLKC — Filter Keyword Chances (wbDefinitionsFO4.pas:10322-10327). Re-emitted
        ''' on preserve-existing overrides. NEW drafts authored in-app leave this empty.</summary>
        Public FilterKeywords As New List(Of LvliFilterKeywordData)
        ''' <summary>LVSG — Epic Loot Chance, FormID [GLOB] (wbDefinitionsFO4.pas:10372). Optional.</summary>
        Public HasEpicLootChance As Boolean
        Public EpicLootChanceFormID As UInteger
        ''' <summary>ONAM — Override Name, translatable lstring (wbDefinitionsFO4.pas:10373).
        ''' Emitted via the central translatable encoder so users with non-ASCII locales keep characters.</summary>
        Public HasOverrideName As Boolean
        Public OverrideName As String = ""
        ''' <summary>VCS1/VCS2 preserved from the source record on preserve-existing overrides. See
        ''' <see cref="OtftRecordEntry.OriginalVcs1"/> for rationale.</summary>
        Public OriginalVcs1 As UInteger
        Public OriginalVcs2 As UShort
        ''' <summary>True = emit as LVLN (Leveled NPC, wbDefinitionsFO4.pas:10329) instead of LVLI.
        ''' El HEAD del body coincide (EDID/OBND/LVLD/LVLM/LVLF/LVLG/LLCT/N×(LVLO+COED)/LLKC), pero el
        ''' TAIL difiere: LVLN termina con un generic model (<see cref="ModelSubrecords"/>) y NO lleva
        ''' LVSG/ONAM; LVLI lleva LVSG+ONAM y NO model. Cada LVLO de una LVLN referencia un NPC_/LVLN
        ''' FormID. LVLN va antes que LVLI en el group order de xEdit (10329 &lt; 10352).</summary>
        Public IsNpcList As Boolean = False
        ''' <summary>LVLN-only generic model subrecords (MODL/MODT/MODC/MODS/MODF, wbGenericModel @
        ''' wbDefinitionsFO4.pas:1040), preserved verbatim in source order for byte-equivalent round-trip.
        ''' This is the real divergence between the LVLN and LVLI bodies: LVLN's tail is a model, LVLI's is
        ''' LVSG+ONAM. The MODS bytes hold the GLOBAL Material Swap FormID ([MSWP], wbDefinitionsFO4.pas:4616),
        ''' remapped on emit; every other model subrecord is FormID-free. Empty for LVLI and for typical
        ''' leveled-NPC lists (which carry no model).</summary>
        Public ModelSubrecords As New List(Of (Signature As String, Data As Byte()))
    End Class

    ''' <summary>One LVLO entry inside an <see cref="LvliRecordEntry"/>. The reference is an ARMO (real),
    ''' a vanilla LVLI (real), or another draft LVLI (provisional — remapped via draftRemap). May carry a
    ''' trailing COED with per-entry Owner/Rank metadata (wbCOED, wbDefinitionsFO4.pas:3686-3694).</summary>
    Public Class LvliEntryData
        Public Level As UShort = 1
        Public RefFormID As UInteger
        Public Count As UShort = 1
        Public ChanceNone As Byte
        ''' <summary>True when the entry carries a COED. Mirror of NPC_InventoryItem COED fields.</summary>
        Public HasCoed As Boolean
        Public CoedOwnerFormID As UInteger
        ''' <summary>COED +4 union: GLOB FormID when Owner=NPC_ (CoedExtraIsFormID=True), Required Rank
        ''' s32 when Owner=FACT, unused bytes otherwise. Same conditional-remap rule as NPC_ inventory.</summary>
        Public CoedOwnerExtra As UInteger
        Public CoedExtraIsFormID As Boolean
        Public CoedItemCondition As Single
    End Class

    ''' <summary>One LLKC filter-keyword chance pair re-emitted on preserve-existing LVLI overrides.</summary>
    Public Class LvliFilterKeywordData
        Public KeywordFormID As UInteger
        Public Chance As UInteger
    End Class

    ''' <summary>One MSWP (Material Swap) record to write into the plugin. NEW-only in this task:
    ''' <see cref="FormID"/> is the caller's PROVISIONAL sentinel (high byte 0xFF), assigned a real
    ''' self-index FormID by the writer (mirror of NEW <see cref="OtftRecordEntry"/>). Body order per
    ''' wbDefinitionsFO4.pas:12798: EDID + FNAM(Tree Folder, optional) + N×(BNAM 'Original Material' +
    ''' SNAM 'Replacement Material' + CNAM 'Color Remapping Index' optional). MSWP carries NO embedded
    ''' FormIDs in its body — only its own record FormID is remapped.</summary>
    Public Class MswpRecordEntry
        ''' <summary>NEW: provisional sentinel (0xFF…). OVERRIDE (not implemented here): the existing
        ''' MSWP's real global FormID.</summary>
        Public FormID As UInteger
        Public EditorID As String = ""
        ''' <summary>FNAM 'Tree Folder' (ZSTRING, first FNAM per wbDefinitionsFO4.pas:12803). Optional —
        ''' emitted only when non-empty.</summary>
        Public TreeFolder As String = ""
        Public Substitutions As New List(Of Canon.IMswp_MaterialSubstitutions)
        Public IsOverride As Boolean = False
        Public OriginalVcs1 As UInteger = 0UI
        Public OriginalVcs2 As UShort = 0US
        ''' <summary>The original parsed source record (required when <see cref="IsOverride"/>=True). On
        ''' override the record's own FormID = the real GLOBAL FormID (caller sets <see cref="FormID"/>);
        ''' header flags/Version come from <c>SourceRecord.Header</c>. MSWP has no body FormIDs and a simple
        ''' body, so its override just re-emits the entry with the source flags/Version — no subrecord merge
        ''' (every owned field already holds the final desired state).</summary>
        Public SourceRecord As PluginRecord = Nothing
    End Class

    ''' <summary>Un record CLFM (Color) a escribir: es el vehiculo de persistencia del tinte de pelo absoluto
    ''' de RaceMenu en SSE (el .jslot guarda un RGB empaquetado, no una ref a CLFM). Dos sabores, como
    ''' <see cref="OtftRecordEntry"/>: NEW (IsOverride=False), donde <see cref="FormID"/> es el centinela
    ''' provisional del caller y el writer asigna el FormID self-index real y remapea toda referencia -entre
    ''' ellas el NPC_.HCLF-; y OVERRIDE, un CLFM autorado por un guardado previo de este mismo plugin, que se
    ''' re-emite para que un re-save no lo tire y deje colgados los HCLF que lo apuntan.
    ''' <para>Cuerpo: EDID + [FULL] + CNAM + FNAM. FULL es opcional y traducible, asi que viaja en DOS formas
    ''' (<see cref="FullName"/> autorado aca para NEW, <see cref="FullNameRaw"/> verbatim para OVERRIDE). CNAM
    ''' es wbByteRGBA [R,G,B,A]; medido sobre Skyrim.esm, los 178 CLFM llevan A=0 y los 15 de pelo FNAM=1
    ''' (Playable), que son los valores que recibe un color sintetizado. El CLFM no lleva FormID en el cuerpo.</para>
    ''' <para>âš ï¸ SSE-ONLY por construccion (lo gatea el caller): un CLFM de pelo de FO4 lleva RemappingIndex en
    ''' vez de RGB. El writer en si se mantiene game-agnostic.</para></summary>
    Public Class ClfmRecordEntry
        ''' <summary>NEW: provisional sentinel (0xFF…). OVERRIDE: the existing CLFM's real global FormID.</summary>
        Public FormID As UInteger
        Public EditorID As String = ""
        ''' <summary>FULL — optional display name, the string the CK / xEdit / our own editor combo show
        ''' instead of the EditorID. NEW entries author it here (see NpcOverrideSaver.MaterializeSseHairColors:
        ''' "NPC Manager custom hair color #RRGGBB"); empty = no FULL emitted, which is what every CLFM this
        ''' writer produced before carried. ⛔ ENCODING: emitted with <c>EncodeTranslatable</c> (FULL is a
        ''' cpTranslate lstring per wbDefinitionsTES5.pas:7946 — NOT the cp1252 General encoder that EDID uses),
        ''' and that encoder has an ExceptionFallback, so an authored name MUST be ASCII-only: this record is
        ''' not covered by the Phase 2b encoding-conflict pre-check (that one walks NPC_ FULL/SHRT/ATTX), and a
        ''' character the chosen codepage can't represent would throw mid-write instead of being reported.</summary>
        Public FullName As String = ""
        ''' <summary>OVERRIDE-only: the source FULL subrecord's payload copied VERBATIM (NUL included), so a
        ''' re-save round-trips the name byte-exactly instead of decoding it with the source plugin's encoding
        ''' and re-encoding it with the current global Translatable — a lossy step that could also throw
        ''' mid-write for a name authored under another codepage. Same reasoning as CNAM/FNAM being copied from
        ''' the bytes in the preservation sweep. Nothing = source had no FULL. Wins over <see cref="FullName"/>.</summary>
        Public FullNameRaw As Byte() = Nothing
        ''' <summary>Packed 0xRRGGBB. Emitted to CNAM as bytes R,G,B then <see cref="ColorAlpha"/>.</summary>
        Public ColorRgb As Integer
        ''' <summary>CNAM's 4th byte. Default 0 = what all 178 vanilla Skyrim.esm CLFM carry (measured).
        ''' Preserved verbatim from the source record on OVERRIDE entries.</summary>
        Public ColorAlpha As Byte = 0
        ''' <summary>FNAM. Skyrim: 'Playable' bool (1 on all 15 vanilla hair colours). FO4: a flag field.
        ''' Emitted verbatim as u32 either way.</summary>
        Public Flags As UInteger = 1UI
        Public IsOverride As Boolean = False
        ''' <summary>VCS1/VCS2 preserved from the source record on OVERRIDE entries. See
        ''' <see cref="OtftRecordEntry.OriginalVcs1"/>.</summary>
        Public OriginalVcs1 As UInteger = 0UI
        Public OriginalVcs2 As UShort = 0US
    End Class

    ''' <summary>One ARMA (Armor Addon) record to write. NEW-only in this task: <see cref="FormID"/> is
    ''' the caller's PROVISIONAL sentinel (0xFF…), assigned a real self-index FormID by the writer. Body
    ''' order per wbDefinitionsFO4.pas:6210 (see <see cref="SerializeArmaRecord"/> for the exact stream
    ''' order). Header flags (No Underarmor Scaling / Has Sculpt Data / Hi-Res 1st Person Only) encode the
    ''' three booleans at bits 6/9/30 per wbRecord(ARMA, …).</summary>
    Public Class ArmaRecordEntry
        Public FormID As UInteger
        Public EditorID As String = ""
        Public SlotMask As UInteger                 ' BOD2 (u32)
        Public RaceFormID As UInteger               ' RNAM
        Public FootstepSetFormID As UInteger        ' SNDD (FSTS)
        Public ArtObjectFormID As UInteger          ' ONAM (ARTO) — owned optional
        Public MaleFPMaterialSwapFormID As UInteger   ' MO4S (MSWP) — owned optional
        Public FemaleFPMaterialSwapFormID As UInteger ' MO5S (MSWP) — owned optional
        Public MalePriority As Byte = 0
        Public FemalePriority As Byte = 0
        Public MaleWeightSliderFlags As Byte = 0
        Public FemaleWeightSliderFlags As Byte = 0
        Public DetectionSoundValue As Byte = 0
        Public WeaponAdjust As Single = 0.0F
        Public MaleMeshPath As String = ""          ' MOD2
        Public FemaleMeshPath As String = ""        ' MOD3
        Public MaleFPMeshPath As String = ""        ' MOD4
        Public FemaleFPMeshPath As String = ""      ' MOD5
        Public MaleModelFlags As Byte = 0           ' MO2F
        Public FemaleModelFlags As Byte = 0         ' MO3F
        Public MaleFPModelFlags As Byte = 0         ' MO4F
        Public FemaleFPModelFlags As Byte = 0       ' MO5F
        Public MaleColorRemapIndex As Single? = Nothing   ' MO2C
        Public FemaleColorRemapIndex As Single? = Nothing ' MO3C
        Public MaleSkinTextureFormID As UInteger    ' NAM0 (TXST)
        Public FemaleSkinTextureFormID As UInteger  ' NAM1 (TXST)
        Public MaleSkinTextureSwapListFormID As UInteger   ' NAM2 (FLST)
        Public FemaleSkinTextureSwapListFormID As UInteger ' NAM3 (FLST)
        Public MaleMaterialSwapFormID As UInteger   ' MO2S (MSWP)
        Public FemaleMaterialSwapFormID As UInteger ' MO3S (MSWP)
        Public AdditionalRaces As New List(Of UInteger)   ' MODL array (RACE)
        Public BoneScaleData As New List(Of ARMA_BoneScaleGender)  ' BSMP/BSMB/BSMS
        Public NoUnderarmorScaling As Boolean = False   ' header flag bit 6
        Public HasSculptData As Boolean = False         ' header flag bit 9
        Public HiRes1stPersonOnly As Boolean = False    ' header flag bit 30
        Public IsOverride As Boolean = False
        Public OriginalVcs1 As UInteger = 0UI
        Public OriginalVcs2 As UShort = 0US
        ''' <summary>The original parsed source record (required when <see cref="IsOverride"/>=True). On
        ''' override: the record's own FormID = the real GLOBAL FormID (caller sets <see cref="FormID"/>);
        ''' header flags/Version come from <c>SourceRecord.Header</c> (source flags PRESERVED — NOT recomputed
        ''' from the booleans). Every subrecord NOT re-emitted from this entry is copied verbatim from
        ''' SourceRecord with its FormIDs remapped to the new MAST list (see SerializeArmaRecordOverride).</summary>
        Public SourceRecord As PluginRecord = Nothing
    End Class

    ''' <summary>One ARMO (Armor) record to write. NEW-only in this task: <see cref="FormID"/> is the
    ''' caller's PROVISIONAL sentinel (0xFF…), assigned a real self-index FormID by the writer. Body order
    ''' per wbDefinitionsFO4.pas:6151 (see <see cref="SerializeArmoRecord"/> for the exact stream order).
    ''' Header flags = 0.</summary>
    Public Class ArmoRecordEntry
        Public FormID As UInteger
        Public EditorID As String = ""
        Public FullName As String = ""              ' FULL (optional)
        Public SlotMask As UInteger                 ' BOD2
        Public RaceFormID As UInteger               ' RNAM
        Public InstanceNamingFormID As UInteger     ' INRD (INNR)
        Public EnchantmentFormID As UInteger        ' EITM (ENCH) — owned optional
        Public PatternFormID As UInteger            ' PTRN (TRNS) — owned optional
        Public EquipTypeFormID As UInteger          ' ETYP (EQUP) — owned optional
        Public PickupSoundFormID As UInteger        ' YNAM (SNDR) — owned optional
        Public DropSoundFormID As UInteger          ' ZNAM (SNDR) — owned optional
        Public AlternateBlockMaterialFormID As UInteger ' BAMT (MATT) — owned optional
        Public Description As String = ""           ' DESC (translatable) — owned optional
        ''' <summary>Si el DESC tiene que emitirse. Se copia de <c>ARMO_Data.HasDescription</c> (presencia en
        ''' el record fuente), NO de que el texto sea no vacío: en un master localizado el id puede resolver a
        ''' "" y el subrecord igual está. Ponerlo en False es la forma de decir "sacá la descripción".</summary>
        Public HasDescription As Boolean = False
        Public NonPlayable As Boolean = False       ' header flag bit 2 — owned
        ''' <summary>OBND — Object Bounds 6×i16 min/max XYZ (required, always emitted).</summary>
        Public ObndX1 As Short
        Public ObndY1 As Short
        Public ObndZ1 As Short
        Public ObndX2 As Short
        Public ObndY2 As Short
        Public ObndZ2 As Short
        ''' <summary>DAMA — Damage Type Array / Resistances (owned, omit block when empty).</summary>
        Public DamageResistances As New List(Of ARMO_DamageResist)
        Public TemplateArmorFormID As UInteger      ' TNAM (ARMO)
        Public ArmorAddons As New List(Of ARMO_AddonEntry)   ' Models: INDX + ArmaFormID
        Public KeywordFormIDs As New List(Of UInteger)       ' KWDA
        Public AttachParentSlotFormIDs As New List(Of UInteger)  ' APPR (KYWD)
        ''' <summary>Object Template combinations (OBTE/OBTF/FULL/OBTS block, wbDefinitionsFO4.pas:5888-5898).
        ''' Populated from the structured model (ARMO_Data.Combinations). When non-empty on a NEW record the
        ''' writer emits the whole block via NpcSubrecordWriter.EmitArmoObjectTemplate; empty = no OBTE block.
        ''' On OVERRIDE the writer preserves the source OBTS bytes verbatim UNLESS <see cref="CombinationsAuthored"/>
        ''' is set, in which case it re-emits the whole Object Template block from this list instead.</summary>
        Public Combinations As New List(Of ARMO_Combination)
        ''' <summary>OVERRIDE-only signal that the caller EDITED the Object Template and populated
        ''' <see cref="Combinations"/> as the authoritative model. When True the override writer emits the whole
        ''' OBTE/OBTF/FULL/OBTS/STOP block from <see cref="Combinations"/> (via NpcSubrecordWriter.EmitArmoObjectTemplate)
        ''' at the source block's position and SKIPS the preserved source template subrecords. When False (default)
        ''' the override path is byte-exact verbatim as before — the list is not consulted. Distinct from merely
        ''' having the list populated so the intent is explicit. The NEW path ignores this flag.</summary>
        Public CombinationsAuthored As Boolean = False
        Public MaleWorldModelPath As String = ""    ' MOD2 (robots)
        Public FemaleWorldModelPath As String = ""  ' MOD4
        Public MaleMaterialSwapFormID As UInteger   ' MO2S at ARMO level (MSWP)
        Public FemaleMaterialSwapFormID As UInteger ' MO4S at ARMO level (MSWP)
        Public Value As Integer = 0                 ' DATA Value (s32)
        Public Weight As Single = 0.0F              ' DATA Weight
        Public Health As UInteger = 0UI             ' DATA Health
        Public ArmorRating As UShort = 0US          ' FNAM (FO4)
        ''' <summary>SKYRIM ONLY — DNAM 'Armor Rating' (wbDefinitionsTES5.pas:4405, itS32, wire value = rating×100).
        ''' Distinct from the FO4 <see cref="ArmorRating"/> (u16 in FNAM); Skyrim has no FNAM. 0 for FO4 entries.</summary>
        Public SkyrimArmorRating As Integer = 0
        Public BaseAddonIndex As UShort = 0US       ' FNAM (0 = load addon group 0)
        Public StaggerRating As Byte = 0            ' FNAM
        Public IsOverride As Boolean = False
        Public OriginalVcs1 As UInteger = 0UI
        Public OriginalVcs2 As UShort = 0US
        ''' <summary>The original parsed source record (required when <see cref="IsOverride"/>=True). On
        ''' override: the record's own FormID = the real GLOBAL FormID (caller sets <see cref="FormID"/>);
        ''' header flags/Version come from <c>SourceRecord.Header</c> (source flags PRESERVED). Every
        ''' subrecord NOT in the OWNED set (VMAD/OBND/PTRN/EITM/textures/DEST/YNAM/ZNAM/ETYP/BIDS/BAMT/
        ''' DESC/INRD/DamageTypeArray/ObjectTemplate, etc.) is copied verbatim from SourceRecord with its
        ''' FormIDs remapped to the new MAST list (see SerializeArmoRecordOverride).</summary>
        Public SourceRecord As PluginRecord = Nothing
    End Class

    ''' <summary>Result of a save operation.</summary>
    Public Class SaveResult
        Public OutputPath As String
        Public MasterList As New List(Of String)
        Public NpcCount As Integer
        Public RemovedMasters As New List(Of String)
        Public AddedMasters As New List(Of String)
        ''' <summary>For each master in the final MAST list, the FormIDs that brought it in.
        ''' Useful for auditing whether a master is legitimately required (an actual NPC reference
        ''' resolves to that plugin) or accidentally pulled in by a parser/collection bug.
        ''' Format: master name → list of resolved FormIDs.</summary>
        Public MasterAudit As New Dictionary(Of String, List(Of UInteger))(StringComparer.OrdinalIgnoreCase)
        ''' <summary>For every NEW draft emitted (OTFT outfits + LVLI leveled lists): provisional sentinel
        ''' FormID (0xFF… as the caller handed it) → the FILE-LOCAL real FormID written into the record
        ''' header ((selfMasterIdx &lt;&lt; 24) | objectIndex). The caller resolves each file-local value to a
        ''' GLOBAL FormID after re-mounting the saved plugin (PluginManager.ResolveReferencedFormID) to
        ''' "promote" the in-memory drafts to real records — remapping any overlay/draft reference that
        ''' still points at the provisional and dropping the now-persisted drafts (no duplicate on reuse).</summary>
        Public DraftFormIdMap As New Dictionary(Of UInteger, UInteger)
    End Class

    ''' <summary>Los buffers que produce UN recorrido completo del walk de emision, agrupados por GRUP.
    ''' Existe porque ese walk se corre DOS veces: una para DESCUBRIR que masters hacen falta y otra para
    ''' escribir los bytes definitivos. Ver el Paso 1 de <see cref="SaveOverridePlugin"/>.</summary>
    Private NotInheritable Class EmittedBuffers
        Public ReadOnly Records As New List(Of Byte())
        Public ReadOnly Otft As New List(Of Byte())
        Public ReadOnly Lvli As New List(Of Byte())
        Public ReadOnly Lvln As New List(Of Byte())
        Public ReadOnly Mswp As New List(Of Byte())
        Public ReadOnly Arma As New List(Of Byte())
        Public ReadOnly Armo As New List(Of Byte())
        Public ReadOnly Clfm As New List(Of Byte())
    End Class

    ''' <summary>Save (or update) a plugin file containing the given NPC overrides.
    ''' Performs full xEdit-style MAST cleanup: any masters not referenced by the final
    ''' record set are dropped (except the game master, which is always preserved).</summary>
    ''' <param name="outputPath">Final destination path for the plugin (.esp/.esm).</param>
    ''' <param name="game">FO4 or SSE — picks game master and TES4/HEDR version constants.</param>
    ''' <param name="markAsMaster">If True, set FLAG_ESM (master flag). Independent from
    ''' <paramref name="lightMaster"/>: any combination of the two is emitted verbatim into
    ''' the TES4 header. False = no master flag (plain ESP slot semantics).</param>
    ''' <param name="lightMaster">If True, set FLAG_ESL (light slot). Independent from
    ''' <paramref name="markAsMaster"/>.</param>
    ''' <param name="overrides">List of NPC overrides to emit. Order is preserved.</param>
    ''' <param name="existingRecords">Optional: records from a pre-existing plugin (loaded
    ''' via PluginReader) that should be preserved alongside the new overrides. The caller
    ''' filters out NPCs whose FormIDs are about to be replaced by entries in 'overrides'.</param>
    ''' <param name="existingMasters">MAST list of the pre-existing plugin, if any. Used as
    ''' the source for resolving FormIDs inside 'existingRecords'.</param>
    ''' <param name="pluginManager">Required for FormID resolution (master high-byte → plugin name).</param>
    Public Function SaveOverridePlugin(outputPath As String,
                                       game As Config_App.Game_Enum,
                                       markAsMaster As Boolean,
                                       lightMaster As Boolean,
                                       entries As List(Of NpcOverrideEntry),
                                       existingRecords As List(Of PluginRecord),
                                       existingMasters As List(Of String),
                                       pluginManager As PluginManager,
                                       Optional outfitEntries As List(Of OtftRecordEntry) = Nothing,
                                       Optional leveledEntries As List(Of LvliRecordEntry) = Nothing,
                                       Optional existingNextObjectId As UInteger = 0UI,
                                       Optional npcCreateEntries As List(Of NpcCreateEntry) = Nothing,
                                       Optional armoEntries As List(Of ArmoRecordEntry) = Nothing,
                                       Optional armaEntries As List(Of ArmaRecordEntry) = Nothing,
                                       Optional mswpEntries As List(Of MswpRecordEntry) = Nothing,
                                       Optional clfmEntries As List(Of ClfmRecordEntry) = Nothing) As SaveResult

        If String.IsNullOrWhiteSpace(outputPath) Then Throw New ArgumentException("outputPath is empty.", NameOf(outputPath))
        If entries Is Nothing Then entries = New List(Of NpcOverrideEntry)()
        If existingRecords Is Nothing Then existingRecords = New List(Of PluginRecord)()
        If existingMasters Is Nothing Then existingMasters = New List(Of String)()
        If pluginManager Is Nothing Then Throw New ArgumentException("pluginManager is required for FormID resolution.", NameOf(pluginManager))
        If outfitEntries Is Nothing Then outfitEntries = New List(Of OtftRecordEntry)()
        If leveledEntries Is Nothing Then leveledEntries = New List(Of LvliRecordEntry)()
        If npcCreateEntries Is Nothing Then npcCreateEntries = New List(Of NpcCreateEntry)()
        If armoEntries Is Nothing Then armoEntries = New List(Of ArmoRecordEntry)()
        If armaEntries Is Nothing Then armaEntries = New List(Of ArmaRecordEntry)()
        If mswpEntries Is Nothing Then mswpEntries = New List(Of MswpRecordEntry)()
        If clfmEntries Is Nothing Then clfmEntries = New List(Of ClfmRecordEntry)()

        Dim gameMaster = MasterFileNamePublic(game)

        ' ====================================================================
        ' Paso 1: el walk de EMISION, parametrizado por el remapper.
        ' Es la UNICA ley sobre que FormID terminan en el archivo. Antes la MAST se armaba con un juego de
        ' COLECTORES que caminaban el modelo en paralelo a los emisores: dos leyes mantenidas a mano que ya
        ' divergieron dos veces (OBTS y CSDI), y cada divergencia es una referencia apuntando al mod
        ' equivocado. Ahora la MAST se DERIVA de lo que estos bucles realmente escriben, asi que no puede
        ' quedar corta por construccion.
        ' Se corre DOS veces porque el indice de master va horneado en cada FormID emitido y
        ' selfMasterIdx = sortedMasters.Count: el valor a escribir depende del conjunto COMPLETO de masters,
        ' que recien se conoce cuando el recorrido termino. La primera pasada solo DESCUBRE (remapper
        ' identidad, buffers descartados); la segunda, con el remapper real, es la que produce los bytes.
        ' ====================================================================
        Dim emitAll As Func(Of NpcSubrecordWriter.FormIdRemapper, EmittedBuffers) =
            Function(rm As NpcSubrecordWriter.FormIdRemapper) As EmittedBuffers
                Dim b As New EmittedBuffers
                For Each entry In entries
                    b.Records.Add(SerializeNpcRecord(entry, rm))
                Next
                For Each existing In existingRecords
                    b.Records.Add(SerializeExistingRecord(existing, existingMasters, pluginManager, rm))
                Next
                ' NEW NPC_ records (clones with self-index FormIDs). Emitted into the same NPC_ GRUP as the
                ' overrides — CK / xEdit / engine all consume NPC_ records uniformly regardless of override-vs-new.
                For Each ce In npcCreateEntries
                    b.Records.Add(SerializeNpcCreateRecord(ce, rm, game))
                Next

                ' OTFT outfit records (Edit Outfit "Create" tab). Each emits as a top-level record: NEW ones
                ' carry a self-index FormID (via draftRemap inside the remapper); OVERRIDE ones keep their real
                ' FormID. INAM items are remapped against the new MAST list.
                For Each oe In outfitEntries
                    b.Otft.Add(SerializeOtftRecord(oe, rm, game))
                Next

                ' LVLI leveled lists (Edit Outfit "New LVL…"). Each emits as a self-index top-level record; LVLO
                ' references are remapped (draft → self via draftRemap; real ARMO/LVLI → master remap).
                For Each le In leveledEntries
                    Dim buf = SerializeLvliRecord(le, rm, game)
                    If le.IsNpcList Then b.Lvln.Add(buf) Else b.Lvli.Add(buf)
                Next

                ' MSWP / ARMA / ARMO records (NEW-only). Each emits a self-index top-level record; every FormID it
                ' references is remapped (draft → self via draftRemap; real → master remap).
                For Each mw In mswpEntries
                    b.Mswp.Add(SerializeMswpRecord(mw, rm, game))
                Next
                For Each ae In armaEntries
                    b.Arma.Add(SerializeArmaRecord(ae, rm, game, pluginManager))
                Next
                For Each ao In armoEntries
                    b.Armo.Add(SerializeArmoRecord(ao, rm, game, pluginManager))
                Next

                ' CLFM colour records (SSE hair tint materialized from a RaceMenu preset). NEW ones take a self-index
                ' FormID via draftRemap; OVERRIDE ones (authored by a prior save of this plugin) keep their real FormID.
                For Each ce In clfmEntries
                    b.Clfm.Add(SerializeClfmRecord(ce, rm, game))
                Next
                Return b
            End Function

        ' ====================================================================
        ' Paso 2: armar la MAST list nueva. Espeja ReportRequiredMasters + GetReferenceFile de xEdit: por cada
        ' FormID del record se agrega el archivo que DEFINE el master (byte alto -> MAST del plugin origen ->
        ' load order). El archivo donde se vio por ultima vez el FormID NO es lo que agrega xEdit: agrega el
        ' que es dueno del record master. GetOriginatingPluginName replica eso, y nuestro ResolveFormID ya
        ' mapeo los FormID locales a archivos master por la MAST del plugin de origen.
        ' ====================================================================
        Dim referencedPluginNames As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        ' Audit table: which FormID is bringing in which master plugin? Logged so the user can
        ' identify "extra" masters as either legitimate references or as bugs upstream.
        ' HashSet, no List: el remapper se invoca UNA VEZ POR REFERENCIA EMITIDA, asi que una lista
        ' acumularia el mismo FormID decenas de veces por master. El audit contesta "que FormID trae
        ' a este master", que es una pregunta de CONJUNTO.
        Dim auditPerPlugin As New Dictionary(Of String, HashSet(Of UInteger))(StringComparer.OrdinalIgnoreCase)
        ' Pasada de DESCUBRIMIENTO: se corre el MISMO walk de emision del Paso 1 con un remapper que, de paso,
        ' anota cada FormID que le pasan. Por eso la MAST no puede quedar corta: lo que se anota es exactamente
        ' lo que despues se escribe. Los buffers que devuelve se tiran — todavia no existe el remapper real.
        ' Un solo nombre de salida para las DOS lambdas: es el predicado de "este FormID es mio, no de un
        ' master", y escribirlo dos veces es justo la clase de duplicacion que este refactor vino a sacar.
        Dim outName = Path.GetFileName(outputPath)
        Dim discoveryRemapper As NpcSubrecordWriter.FormIdRemapper =
            Function(g As UInteger) As UInteger
                If g = 0UI Then Return 0UI
                ' Los drafts provisionales (byte alto 0xFF) no son resolvibles a un master: los maneja
                ' draftRemap, que todavia no existe en esta pasada. Devolverlos tal cual.
                If IsProvisionalDraftFormID(g) Then Return g
                Dim pn = pluginManager.GetOriginatingPluginName(g)
                If String.IsNullOrEmpty(pn) Then
                    Throw New InvalidOperationException(
                        $"FormID {g:X8} does not belong to any loaded plugin, so it cannot be re-mastered into the output.")
                End If
                If Not String.Equals(pn, outName, StringComparison.OrdinalIgnoreCase) Then
                    referencedPluginNames.Add(pn)
                    Dim lst As HashSet(Of UInteger) = Nothing
                    If Not auditPerPlugin.TryGetValue(pn, lst) Then
                        lst = New HashSet(Of UInteger)
                        auditPerPlugin(pn) = lst
                    End If
                    lst.Add(g)
                End If
                ' IDENTIDAD deliberada (la misma convencion que usan los probes de round-trip): devolver el
                ' FormID sin tocar garantiza que ninguna rama del emisor que dependa de cero/no-cero tome un
                ' camino distinto al de la pasada real, o sea que las dos pasadas recorren lo mismo.
                Return g
            End Function
        Call emitAll(discoveryRemapper)

        ' We do NOT force-add the game master here. xEdit's ReportRequiredMasters only auto-adds
        ' files with fsIsGameMaster when the source record itself is hardcoded/game-master
        ' (wbImplementation.pas:13580) — for normal overrides the game master arrives via the
        ' usual FormID resolution (RNAM=Race, VTCK=Voice, etc.). In practice any NPC override
        ' references game-master records so this is a no-op, but copying xEdit's behavior
        ' verbatim avoids a spurious master if a NPC somehow doesn't reference Fallout4.esm.

        ' Build MAST list following xEdit CleanMasters convention (wbImplementation.pas:3024-3120):
        ' preserve the original master ordering for masters that survive the cleanup, drop unused
        ' ones, append any new ones at the end (in load order). This minimizes the FormID-byte
        ' churn vs the "rebuild from scratch sorted by load order" approach which would re-shuffle
        ' high bytes for every survived master that isn't already in load order.
        Dim sortedMasters As New List(Of String)
        Dim seenLower As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)

        ' Step 2a: for "Update existing" — preserve survivors from existingMasters in original
        ' order. xEdit CleanMasters (wbImplementation.pas:3045-3070) walks flMasters in their
        ' original order and keeps each one that is either (a) actually referenced by some
        ' record, or (b) the game master (special-cased on line 3060). We replicate that.
        For Each oldM In existingMasters
            If seenLower.Contains(oldM) Then Continue For
            If referencedPluginNames.Contains(oldM) OrElse String.Equals(oldM, gameMaster, StringComparison.OrdinalIgnoreCase) Then
                sortedMasters.Add(oldM)
                seenLower.Add(oldM)
            End If
        Next

        ' Step 2b: append any newly-added masters (referenced by the new override but not in the
        ' existing MAST) in load order. The game master is always added first if it wasn't in
        ' existingMasters and is referenced — Bethesda convention puts it at index 0.
        For Each plugin In pluginManager.Plugins
            If plugin Is Nothing Then Continue For
            If seenLower.Contains(plugin.FileName) Then Continue For
            If referencedPluginNames.Contains(plugin.FileName) Then
                sortedMasters.Add(plugin.FileName)
                seenLower.Add(plugin.FileName)
            End If
        Next

        ' ⛔ Paso 2c: ORDENAR la MAST por LOAD ORDER, con el game master forzado al índice 0.
        ' El invariante de xEdit es "la MAST está siempre en load order": `AddMasterIfMissing` /
        ' `AddMastersIfMissing` (wbImplementation.pas:2494, :2511) van con `aSortMasters: Boolean = True` por
        ' defecto y llaman a `TwbFile.SortMasters` (:6220), que reordena la lista COMPLETA con
        ' `wbMergeSortPtr(@flMasters[0], Length(flMasters), CompareLoadOrder)` (:6255) y remapea todos los FormID.
        ' `CleanMasters` (:3024-3120) —que es lo que el paso 2a replica— sólo SACA masters, nunca agrega; por eso
        ' preserva el orden, que ya venía ordenado. Al AGREGAR uno que carga antes que otro ya presente, el
        ' paso 2b lo dejaba al final y la lista quedaba fuera de orden.
        ' In-game es inerte (el motor resuelve la MAST por NOMBRE y nuestros tres consumidores son posicionales),
        ' pero el archivo dejaba de ser canónico: el primer guardado que le hiciera xEdit encima reordenaba todo
        ' y movía el byte alto de cada referencia en un diff enorme e inexplicable.
        ' ⚠️ Un plugin NUEVO ya salía ordenado (el paso 2b recorre pluginManager.Plugins en load order), así que
        ' esto sólo mueve bytes en un "Update existing" cuyo MAST estaba desordenado.
        Dim loadOrderRank As New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)
        For i = 0 To pluginManager.Plugins.Count - 1
            Dim pl = pluginManager.Plugins(i)
            If pl IsNot Nothing AndAlso Not loadOrderRank.ContainsKey(pl.FileName) Then loadOrderRank(pl.FileName) = i
        Next
        sortedMasters = sortedMasters.
            OrderBy(Function(m) If(String.Equals(m, gameMaster, StringComparison.OrdinalIgnoreCase), -1, 0)).
            ThenBy(Function(m)
                       Dim r As Integer
                       ' Un master que no está en el load order cargado no puede ordenarse contra los demás;
                       ' va al final, en su orden relativo previo (OrderBy es estable).
                       Return If(loadOrderRank.TryGetValue(m, r), r, Integer.MaxValue)
                   End Function).
            ToList()

        ' ====================================================================
        ' Paso 3: armar el FormIdRemapper. Por cada FormID global: resolver el plugin de origen, buscar su nuevo
        ' indice de MAST (-1 = error) y devolver (newMastIdx << 24) | (FormID & 0xFFFFFF).
        ' Casos especiales: FormID 0 se emite como 0 (ref nula); si el plugin de origen es el propio archivo de
        ' salida, el indice es el "self FileID", que xEdit codifica como len(masters).
        ' ====================================================================
        Dim masterIndexLookup As New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)
        For i = 0 To sortedMasters.Count - 1
            masterIndexLookup(sortedMasters(i)) = i
        Next
        ' "Self" master index (records owned by THIS plugin) = sortedMasters.Count.
        Dim selfMasterIdx As Integer = sortedMasters.Count

        ' Cada draft NUEVO (outfits OTFT y leveled lists LVLI) recibe su FormID self-index real ANTES de
        ' serializar: (selfMasterIdx << 24) | objIndex, con objIndex arrancando en 0x800 (convencion de
        ' nuevo record de FO4/xEdit). El caller les habia dado un centinela PROVISIONAL con byte alto 0xFF, y
        ' draftRemap mapea provisional -> real para que el remapper reescriba tanto el header del record como
        ' toda referencia a el (el NPC.DOFT, el OTFT.INAM que apunta a un LVLI draft, un LVLI a otro).
        ' Asi las referencias cruzadas resuelven por el unico remapper sin importar el orden de emision; las
        ' claves son unicas entre ambos tipos porque salen de un solo contador de la app.
        Dim draftRemap As New Dictionary(Of UInteger, UInteger)
        ' Seed the draft object-ID counter from the disk HEDR if it's ahead of the default. On
        ' update-existing, a prior save consumed object IDs 0x800..existingNextObjectId-1 (those
        ' records are in existingRecords/Entries with self-index FormIDs). Starting at 0x800 again
        ' would re-hand the same IDs and collide with already-preserved overrides.
        ' Ancho del object id del ARCHIVO QUE SE ESCRIBE: 12 bits si sale con FLAG_ESL, 24 si es completo.
        ' ⛔ Va ACÁ, antes de repartir, no sólo al escribir el HEDR. xEdit enmascara al REPARTIR
        ' (TwbFile.NewFormID, wbImplementation.pas:5076-5083 `NextObjectID := GetNextObjectID and Mask`);
        ' clampear sólo el header dejaba salir object ids > 0xFFF en un ESL, que el motor enmascara a 12
        ' bits (ModInfo::GetFormID) y hace colisionar con otro record — y ademas congelaba el contador en
        ' 0xFFF, con lo que CADA guardado siguiente volvia a repartir 0xFFF a un record distinto.
        Dim objectIdMask As UInteger = If(lightMaster, &HFFFUI, &HFFFFFFUI)

        ' PISO del espacio de object ids. ⛔ NO es la constante 0x800: el canónico lo decide POR ARCHIVO
        ' (juego + versión del HEDR + tener masters) y para un plugin SSE nuestro da 1, no 0x800 — ver
        ' PluginWriter.AllowsHardcodedRange. Usarlo cableado hacía que el agotamiento rehusara el guardado
        ' a los 2048 records cuando en SSE el canónico todavía tiene 2047 libres: un límite propio MÁS
        ' ESTRICTO que la referencia. Sólo afecta wrap / recuperación / agotamiento; el arranque de un
        ' guardado normal sale del HEDR (0x800), así que no mueve un byte del caso corriente.
        Dim objectIdFloor As UInteger = If(PluginWriter.AllowsHardcodedRange(game, sortedMasters.Count), 1UI, NEXT_OBJECT_ID_DEFAULT)

        ' Object ids YA OCUPADOS en este archivo: los de los records PROPIOS que se preservan o se
        ' re-emiten con su FormID real. xEdit consulta lo mismo antes de entregar uno nuevo
        ' (`while GetRecordByFormID(Result) <> nil do Inc`, :5100-5109).
        Dim usedObjectIds As New HashSet(Of UInteger)
        ' ⛔ Toma un FormID GLOBAL. El object id NO se saca enmascarando con el ancho de SALIDA: lo decide
        ' el encoding de ORIGEN, y esa ley ya está unificada en TryMapGlobalToFileLocal. Con el ancho de
        ' salida, un destino ESL que el usuario destilda a full registraba (lightSlot<<12)|obj en vez de
        ' obj — o sea no anotaba la ocupación real y encima bloqueaba un id espurio.
        Dim noteUsedObjectId As Action(Of UInteger) =
            Sub(g As UInteger)
                If g = 0UI OrElse IsProvisionalDraftFormID(g) Then Return
                Dim lf As UInteger = 0UI
                If pluginManager.TryMapGlobalToFileLocal(g, masterIndexLookup, selfMasterIdx, outName, lf) <> PluginManager.FileLocalMapResult.Ok Then Return
                ' Sólo los records PROPIOS ocupan object ids de este archivo; los overrides van bajo el
                ' índice de su master. El test por el alto == selfMasterIdx ES el test por "el dueño es
                ' outName": masterIndexLookup se llena con 0..sortedMasters.Count-1 (más arriba), o sea que
                ' todo índice de master es ESTRICTAMENTE MENOR que selfMasterIdx = sortedMasters.Count.
                If (lf >> 24) <> CUInt(selfMasterIdx) Then Return
                usedObjectIds.Add(lf And &HFFFFFFUI)
            End Sub
        For Each r In existingRecords
            ' ⛔ `r.Header.FormID` es LOCAL: existingRecords viene de un PluginReader FRESCO del archivo de
            ' destino, que nunca pasa por MergeRecords (el único lugar que reescribe el header a global).
            ' Pasarlo tal cual a una función que lee el byte alto como SLOT DE SESIÓN es exactamente el
            ' defecto de este dominio. SerializeExistingRecord hace esta misma conversión, y por lo mismo.
            noteUsedObjectId(pluginManager.ResolveReferencedFormID(r.SourcePluginName, r.Header.FormID))
        Next
        ' Un override de un record de otro plugin queda filtrado por el chequeo de outName; el que cuenta es
        ' el NPC que ESTE plugin creó en un guardado anterior y ahora se vuelve a editar.
        For Each e In entries
            If e.Npc IsNot Nothing Then noteUsedObjectId(e.Npc.FormID)
        Next
        For Each oe In outfitEntries : If oe.IsOverride Then noteUsedObjectId(oe.FormID)
        Next
        For Each le In leveledEntries : If le.IsOverride Then noteUsedObjectId(le.FormID)
        Next
        For Each mw In mswpEntries : If mw.IsOverride Then noteUsedObjectId(mw.FormID)
        Next
        For Each ae In armaEntries : If ae.IsOverride Then noteUsedObjectId(ae.FormID)
        Next
        For Each ao In armoEntries : If ao.IsOverride Then noteUsedObjectId(ao.FormID)
        Next
        For Each ce In clfmEntries : If ce.IsOverride Then noteUsedObjectId(ce.FormID)
        Next

        ' ⛔ Los records PROPIOS que se preservan también tienen que caber en el ancho de SALIDA.
        ' CANÓNICO, el PREDICADO verbatim — wbImplementation.pas:10891, las mismas TRES condiciones:
        '     if _File.IsLight and (FormID.ObjectID > $FFF) and (FixedFormID.FileID = _File.FileFileID[True])
        ' (la tercera es nuestro filtro `(lf >> 24) = selfMasterIdx`: sólo los records PROPIOS del archivo).
        ' CANÓNICO, la ACCIÓN — TwbFormID.SetObjectID (wbInterface.pas:22796-22798) hace
        '     if Value <> (Value and Mask) then raise ERangeError.Create('ObjectID out of bounds')
        ' con Mask = $FFF para un FileID light. El overload público (:22778-22781) llama con aSilent=False,
        ' o sea que LEVANTA; el único camino silencioso es el re-empaque interno de SetFileID (:22763-22775).
        ' ⚠️ DIFERENCIA DECLARADA: en :10891 xEdit REPORTA (es su "Check for Errors") en vez de rehusar.
        ' Esta app no tiene ese canal, así que el único punto de aplicación disponible es rehusar el
        ' guardado — que es además lo que hace el canónico al REPARTIR (xeMainForm.pas:12667
        ' `not TargetIsLight or (ObjectID <= $FFF)`).
        ' Sin esto, el remapper lo emite con el ancho de ORIGEN (que para el remapper es lo correcto) y en
        ' un archivo con FLAG_ESL el motor lo pliega a 12 bits (ModInfo::GetFormID) ⇒ colisiona con otro
        ' record IN-GAME, donde ningún assert lo ve. Se alcanza al TILDAR "Light" sobre un plugin full que
        ' ya tiene records por encima de 0xFFF, y al reabrir un ESL que el código viejo ya corrompió.
        Dim overWide = usedObjectIds.Where(Function(o) o > objectIdMask).OrderBy(Function(o) o).ToList()
        If overWide.Count > 0 Then
            Throw New InvalidOperationException(
                $"'{outName}' already contains {overWide.Count} record(s) whose object id does not fit this " &
                $"file's FormID width (first: 0x{overWide(0):X}, maximum 0x{objectIdMask:X})." &
                If(lightMaster, $" A light (ESL) plugin only addresses 0x{objectIdFloor:X}..0x{objectIdMask:X}, so the game would fold " &
                                "those records onto other FormIDs. Save it without the Light flag, or split " &
                                "the records across two plugins.", " Split the records across two plugins."))
        End If

        ' Semilla = el contador del HEDR ENMASCARADO, y nada más: es `NextObjectID := GetNextObjectID and
        ' Mask` (wbImplementation.pas:5083), que NO lleva piso. El único piso lo pone la recuperación de
        ' abajo. ⛔ Acá había un SEGUNDO piso cableado en 0x800 que el canónico no tiene: en SSE (donde el
        ' piso real es 1) un HEDR en 0x300 —alcanzable sólo si un guardado previo ya envolvió al rango
        ' hardcoded— saltaba a 0x800 y tiraba ~1280 ids todavía vigentes.
        ' El caso "plugin NUEVO" (sin HEDR en disco, existingNextObjectId = 0) sí arranca en 0x800: es la
        ' convención del CK y lo que PluginWriter escribe en el header, y mantenerla deja los FormID de un
        ' guardado corriente donde estaban.
        Dim nextSelfObjIndex As UInteger = If(existingNextObjectId > 0UI, existingNextObjectId And objectIdMask, NEXT_OBJECT_ID_DEFAULT)

        ' Recuperación de una semilla no confiable: arrancar en el object id MÁS ALTO en uso en vez de
        ' barrer desde el piso. Barrer desde abajo también sería seguro —el salteo de ocupados impide la
        ' colisión— pero reciclaría el id de un record borrado, y el canónico deliberadamente no lo hace.
        ' El canónico tiene DOS ramas con la MISMA forma, y `objectIdFloor` es justamente lo que las unifica:
        '     :5085-5090  con rango hardcoded → `if (NextObjectID < 1)     or (NextObjectID = Mask)` … piso 1
        '     :5091-5097  sin rango hardcoded → `if (NextObjectID < $800)  or (NextObjectID = Mask)` … piso $800
        ' ⚠️ Para SSE corre la PRIMERA (ver PluginWriter.AllowsHardcodedRange); citar sólo la segunda
        ' mandaría al próximo lector a "corregir" el código hacia la rama que no se ejecuta.
        ' El término `= Mask` NO es decorativo: es EXACTAMENTE el valor que escribía el código pre-fix
        ' cuando CLAMPEABA el HEDR, así que cualquier ESL que la app haya guardado tocando el tope lo tiene
        ' en disco. El canónico lo lee como "contador ya rodó, no confiable" y re-siembra; tomarlo como
        ' bueno sería confiar en el número que dejó el bug.
        If nextSelfObjIndex < objectIdFloor OrElse nextSelfObjIndex = objectIdMask Then
            Dim highest As UInteger = objectIdFloor
            For Each u In usedObjectIds
                If u >= highest Then highest = u + 1UI
            Next
            nextSelfObjIndex = If(highest > objectIdMask, objectIdFloor, highest)
        End If

        ' Entrega el próximo object id LIBRE, envolviendo AL PISO (objectIdFloor — 1 o 0x800 según el
        ' archivo, ver arriba) al pasarse del ancho y saltando los que ya están tomados.
        ' Réplica de TwbFile.NewFormID (:5083-5120), incluido el error duro al agotarse:
        ' sin él, el desborde es SILENCIOSO y produce dos records con el mismo FormID.
        Dim dispenseObjectId As Func(Of UInteger) =
            Function() As UInteger
                Dim span As Long = CLng(objectIdMask) - CLng(objectIdFloor) + 1L
                For attempt As Long = 0 To span - 1
                    If nextSelfObjIndex > objectIdMask OrElse nextSelfObjIndex < objectIdFloor Then
                        nextSelfObjIndex = objectIdFloor
                    End If
                    Dim candidate = nextSelfObjIndex
                    nextSelfObjIndex += 1UI
                    If usedObjectIds.Add(candidate) Then Return candidate
                Next
                Throw New InvalidOperationException(
                    $"'{outName}' has no free FormID left: every object id from 0x{objectIdFloor:X} to " &
                    $"0x{objectIdMask:X} is already used by a record in the file. " &
                    If(lightMaster, $"A light (ESL) plugin only addresses {span} of them — save without the " &
                                    "Light flag, or split the records across two plugins.",
                                    "Split the records across two plugins."))
            End Function
        For Each oe In outfitEntries
            If oe.IsOverride Then Continue For
            draftRemap(oe.FormID) = (CUInt(selfMasterIdx) << 24) Or dispenseObjectId()
        Next
        ' NPC_ creates ANTES que leveled: los NPC_ son los records primarios y sus FormIDs deben ser
        ' estables (los bakes en disco se nombran por FormID). Una LVLN/LVLI que los referencia toma su
        ' slot DESPUES, asi agregar/quitar una leveled list no corre los FormIDs de los NPC_. La
        ' resolucion de refs es global (draftRemap completo antes de serializar), asi que el orden de
        ' asignacion no afecta la correctitud — solo que numero recibe cada record.
        For Each ce In npcCreateEntries
            If draftRemap.ContainsKey(ce.ProvisionalFormID) Then Continue For
            draftRemap(ce.ProvisionalFormID) = (CUInt(selfMasterIdx) << 24) Or dispenseObjectId()
        Next
        For Each le In leveledEntries
            ' OVERRIDE LVLIs keep their real FormID (master-remapped on emit) — no self-index. Only NEW
            ' (draft) lists get a self-index. Guard against a duplicate provisional listed twice.
            If le.IsOverride Then Continue For
            If draftRemap.ContainsKey(le.FormID) Then Continue For
            draftRemap(le.FormID) = (CUInt(selfMasterIdx) << 24) Or dispenseObjectId()
        Next
        ' NEW MSWP / ARMA / ARMO drafts: pre-assign each a real self-index FormID so cross-draft refs
        ' resolve through the single remapper irrespective of emit order (ARMA.MO2S → draft MSWP,
        ' ARMO.MODL → draft ARMA, ARMO.MO2S → draft MSWP). OVERRIDE entries keep their real FormID. The
        ' provisional keys are globally unique across all draft kinds (one app-side counter). Order
        ' (MSWP → ARMA → ARMO) is cosmetic — resolution is global once draftRemap is fully built.
        For Each mw In mswpEntries
            If mw.IsOverride Then Continue For
            If draftRemap.ContainsKey(mw.FormID) Then Continue For
            draftRemap(mw.FormID) = (CUInt(selfMasterIdx) << 24) Or dispenseObjectId()
        Next
        For Each ae In armaEntries
            If ae.IsOverride Then Continue For
            If draftRemap.ContainsKey(ae.FormID) Then Continue For
            draftRemap(ae.FormID) = (CUInt(selfMasterIdx) << 24) Or dispenseObjectId()
        Next
        For Each ao In armoEntries
            If ao.IsOverride Then Continue For
            If draftRemap.ContainsKey(ao.FormID) Then Continue For
            draftRemap(ao.FormID) = (CUInt(selfMasterIdx) << 24) Or dispenseObjectId()
        Next
        ' NEW CLFM drafts (SSE hair colour materialized from a RaceMenu preset). Same pre-assignment as every
        ' other draft kind, so the NPC_.HCLF that points at the provisional sentinel rewrites to the real
        ' self-index FormID through the single remapper regardless of emit order.
        For Each ce In clfmEntries
            If ce.IsOverride Then Continue For
            If draftRemap.ContainsKey(ce.FormID) Then Continue For
            draftRemap(ce.FormID) = (CUInt(selfMasterIdx) << 24) Or dispenseObjectId()
        Next

        Dim remapper As NpcSubrecordWriter.FormIdRemapper =
            Function(globalFormID As UInteger) As UInteger
                If globalFormID = 0UI Then Return 0UI
                ' NEW draft (OTFT/LVLI/ARMA/ARMO/MSWP/CLFM) → real self FormID. Branch on the SAME predicate
                ' the discovery pass uses — IsProvisionalDraftFormID — and only then consult draftRemap.
                ' ⛔ Using `draftRemap.TryGetValue` as the branch instead would be the law written twice with
                ' two different tests: discovery would classify a FormID as a draft while this pass did not,
                ' or the reverse, and the two walks would stop agreeing. They must partition FormIDs
                ' identically; what differs between them is only what they DO with each part.
                If IsProvisionalDraftFormID(globalFormID) Then
                    Dim mappedDraft As UInteger
                    If draftRemap.TryGetValue(globalFormID, mappedDraft) Then Return mappedDraft
                    ' A provisional FormID that no draft claims: something references a draft record that is
                    ' not being emitted (e.g. a draft cancelled while another still points at it). Falling
                    ' through would ask GetOriginatingPluginName about a 0xFF high byte — never a valid slot,
                    ' MAX_FULL_SLOT is 0xFD — and throw the misleading "not in any loaded plugin".
                    Throw New InvalidOperationException(
                        $"Draft FormID {globalFormID:X8} is referenced by a record being written but no draft " &
                        "record claims it, so it cannot be given a real FormID. A referenced draft was most " &
                        "likely cancelled or deleted while something still pointed at it.")
                End If
                ' La conversión global → local (byte alto = índice en la MAST de ESTE archivo, self =
                ' masters.Count, ancho del object id según el encoding de ORIGEN) vive UNA sola vez, en
                ' PluginManager.TryMapGlobalToFileLocal. Acá sólo se decide QUÉ HACER con cada resultado:
                ' los dos casos de fallo son aserciones para el writer (ver los comentarios de abajo).
                Dim mappedLocal As UInteger = 0UI
                Dim mapRes = pluginManager.TryMapGlobalToFileLocal(globalFormID, masterIndexLookup, selfMasterIdx, outName, mappedLocal)
                If mapRes = PluginManager.FileLocalMapResult.Ok Then Return mappedLocal

                Dim pname = pluginManager.GetOriginatingPluginName(globalFormID)
                If mapRes = PluginManager.FileLocalMapResult.NoOwner Then
                    ' ⛔ Was "best effort: keep raw", which wrote the GLOBAL FormID into a file where the
                    ' high byte means an index into THIS file's MAST — two different numbering spaces. The
                    ' reference silently ended up pointing at whatever plugin sat at that index.
                    ' Every way a FormID could reach here without a resolvable owner is now closed
                    ' upstream: the .esl extension gets a light slot (PluginReader.ReadTES4), a re-mount
                    ' keeps its slot instead of vacating one (MergeOverridePlugin), a persisted identifier
                    ' no longer carries a stale slot (GlobalFormIDFromIdentifierLocal), masters are merged
                    ' before their dependents (OrderByMasters), an unloaded save target is refused by the
                    ' Save dialog, and MakeGlobalFormID no longer invents slot 0. So this is an assertion
                    ' on an invariant, not a user-facing failure mode: if it fires, the bug is ours.
                    Throw New InvalidOperationException(
                        $"FormID {globalFormID:X8} does not belong to any loaded plugin, so it cannot be " &
                        "re-mastered into the output. Writing it unchanged would silently repoint it at " &
                        "whichever plugin occupies that index in the new master list.")
                End If
                ' OwnerNotInMasterList: el owner resolvio pero no quedo en la MAST. Esta rama nacio como
                ' detector de drift entre los COLECTORES (que armaban la MAST caminando el modelo) y los
                ' EMISORES (que producian los bytes) — dos leyes paralelas mantenidas a mano, que ya
                ' habian divergido dos veces. Los colectores ya no existen: la MAST se DERIVA del walk de
                ' emision (Paso 1), la misma pasada que produce estos bytes, asi que todo FormID que llega
                ' hasta aca ya paso por discoveryRemapper y su plugin ya esta en la lista. Por
                ' construccion, inalcanzable.
                '
                ' ⚠️ Para el WRITER es una asercion y por eso lanza; el otro llamador de
                ' TryMapGlobalToFileLocal (NpcOverrideSaver, que mapea contra la MAST VIEJA del disco)
                ' necesita lo OPUESTO — ahi este caso es legitimo y frecuente. Por eso la funcion
                ' compartida devuelve un enum en vez de decidir por los dos.
                '
                ' Sigue tirando en vez de devolver el FormID crudo: el crudo esta indexado
                ' por load order, mientras que el byte alto de un FormID en el archivo de salida es un
                ' indice en la MAST de ESTE archivo, asi que escribirlo repuntaria la referencia en
                ' silencio al plugin que ocupe ese indice. Si algun dia se dispara, lo que se rompio es la
                ' premisa de que las dos pasadas recorren lo mismo — y mejor mil veces que el guardado
                ' falle fuerte antes que shipear un plugin apuntando al mod equivocado.
                Throw New InvalidOperationException(
                    $"FormID {globalFormID:X8} is owned by '{pname}', which is not in the master list " &
                    "being written. The master list is built from the discovery pass over this very " &
                    "emission walk, so every plugin reached here should already be in it — this means " &
                    "the two passes disagreed. Writing it unchanged would silently repoint the " &
                    "reference at whichever plugin occupies that index.")
            End Function

        ' Diff against existing masters for the SaveResult report.
        Dim result As New SaveResult With {
            .OutputPath = outputPath,
            .NpcCount = entries.Count + existingRecords.Count,
            .MasterList = sortedMasters,
            .DraftFormIdMap = New Dictionary(Of UInteger, UInteger)(draftRemap)
        }
        ' Filter the audit to only masters that actually made it into the final MAST list
        ' (drop entries for plugins that referencedPluginNames had but Step 2 filtered out
        ' because they're not in the load order).
        For Each m In sortedMasters
            Dim list As HashSet(Of UInteger) = Nothing
            If auditPerPlugin.TryGetValue(m, list) Then
                result.MasterAudit(m) = list.ToList()
            Else
                result.MasterAudit(m) = New List(Of UInteger)
            End If
        Next
        For Each oldM In existingMasters
            If Not sortedMasters.Any(Function(m) String.Equals(m, oldM, StringComparison.OrdinalIgnoreCase)) Then
                result.RemovedMasters.Add(oldM)
            End If
        Next
        For Each newM In sortedMasters
            If Not existingMasters.Any(Function(m) String.Equals(m, newM, StringComparison.OrdinalIgnoreCase)) Then
                result.AddedMasters.Add(newM)
            End If
        Next

        ' ====================================================================
        ' Paso 4: la emision de verdad. Mismo walk que la pasada de descubrimiento, ahora con el remapper
        ' real (indices de la MAST ya cerrada + draftRemap). Se conservan los nombres locales de siempre
        ' para que el Paso 5 y todo lo de aguas abajo no se entere de que la emision se movio.
        ' ====================================================================
        Dim emitted As EmittedBuffers = emitAll(remapper)
        Dim recordBuffers As List(Of Byte()) = emitted.Records
        Dim otftBuffers As List(Of Byte()) = emitted.Otft
        Dim lvliBuffers As List(Of Byte()) = emitted.Lvli
        Dim lvlnBuffers As List(Of Byte()) = emitted.Lvln
        Dim mswpBuffers As List(Of Byte()) = emitted.Mswp
        Dim armaBuffers As List(Of Byte()) = emitted.Arma
        Dim armoBuffers As List(Of Byte()) = emitted.Armo
        Dim clfmBuffers As List(Of Byte()) = emitted.Clfm

        ' ====================================================================
        ' Paso 5: envolver cada tipo de record en su GRUP de primer nivel, en orden referenced-first:
        ' CLFM -> MSWP -> ARMA -> ARMO -> OTFT -> LVLN -> LVLI -> NPC_ (un record referenciado precede a su
        ' referrer). La resolucion de FormID es global, asi que al motor el orden no le importa: es para que el
        ' archivo quede legible.
        ' âš ï¸ Es una desviacion DELIBERADA y PREEXISTENTE del wbGroupOrder canonico de xEdit, que en los dos
        ' juegos pone CLFM cerca del FINAL. Este writer ya emite referenced-first para los otros 7 grupos y CLFM
        ' sigue la MISMA convencion local en vez de partir el orden del archivo en dos reglas. Nada lo rechaza:
        ' el orden de GRUP no significa nada para el motor ni para el CK, y xEdit re-ordena por SortOrder en su
        ' propio guardado. Lo que SI tiene que estar bien es HEDR.numRecords, mas abajo.
        ' ====================================================================
        Dim grupClfmBytes As Byte() = If(clfmBuffers.Count > 0, BuildGrup("CLFM", clfmBuffers), Array.Empty(Of Byte)())
        Dim grupMswpBytes As Byte() = If(mswpBuffers.Count > 0, BuildGrup("MSWP", mswpBuffers), Array.Empty(Of Byte)())
        Dim grupArmaBytes As Byte() = If(armaBuffers.Count > 0, BuildGrup("ARMA", armaBuffers), Array.Empty(Of Byte)())
        Dim grupArmoBytes As Byte() = If(armoBuffers.Count > 0, BuildGrup("ARMO", armoBuffers), Array.Empty(Of Byte)())
        Dim grupOtftBytes As Byte() = If(otftBuffers.Count > 0, BuildGrup("OTFT", otftBuffers), Array.Empty(Of Byte)())
        ' LVLN (Leveled NPC, decl 10329) va ANTES que LVLI (10352) en el group order de xEdit.
        Dim grupLvlnBytes As Byte() = If(lvlnBuffers.Count > 0, BuildGrup("LVLN", lvlnBuffers), Array.Empty(Of Byte)())
        Dim grupLvliBytes As Byte() = If(lvliBuffers.Count > 0, BuildGrup("LVLI", lvliBuffers), Array.Empty(Of Byte)())
        Dim grupNpcBytes = BuildGrup("NPC_", recordBuffers)

        ' ====================================================================
        ' Paso 6: armar el header TES4 y emitir el stream final.
        ' nextObjectId sigue la semantica de TwbFile.NewFormID: el contador de drafts se sembro con
        ' max(0x800, HEDR del disco) y avanzo una vez por draft NUEVO, asi que su valor final es el primer slot
        ' libre despues de este guardado, que es exactamente lo que debe llevar HEDR.nextObjectId. Un plugin
        ' fresco sin drafts se queda en 0x800, y actualizar uno existente sin drafts nuevos preserva el contador
        ' por la semilla.
        ' El ancho (objectIdMask) ya se aplico AL REPARTIR, arriba, que es donde xEdit lo aplica. Aca solo
        ' queda ENVOLVER a 0x800 si el contador quedo justo pasado del tope, igual que
        ' TwbFile.NewFormID (wbImplementation.pas:5116-5120: `if NextObjectID > Mask then $800`).
        ' ⛔ Antes esto CLAMPEABA a objectIdMask, y ese clamp era el motor del defecto: dejaba el contador
        ' congelado en 0xFFF, asi que el guardado siguiente se sembraba ahi y volvia a repartir 0xFFF a un
        ' record distinto. Con el reparto ya acotado y el skip de ocupados, el clamp no protegia nada.
        ' ====================================================================
        Dim nextObjectId As UInteger = nextSelfObjIndex
        If nextObjectId > objectIdMask Then nextObjectId = objectIdFloor
        ' HEDR.numRecords = Pred(file.GetCountedRecordCount) (wbImplementation.pas:5215-5219). The file's
        ' counted count walks EVERY element: TES4 itself (TwbMainRecord → 1), plus each top-level GRUP,
        ' which returns Succ(sum of its children) (TwbGroupRecord.GetCountedRecordCount, :17762-17765) —
        ' i.e. the GRUP counts as one form ON TOP OF its records. Subtracting TES4 leaves:
        '     numRecords = (content records) + (top-level GRUPs emitted)
        ' Verified against CK-authored plugins (KSHairdos.esp: HEDR 304 = 303 records + 1 GRUP).
        ' Counting only the records made the CK pop "Form counts don't match / correct the file header?".
        ' The NPC_ GRUP is always emitted (Step 7), the rest only when non-empty.
        Dim grupCount As Integer = 1 +
                                   If(clfmBuffers.Count > 0, 1, 0) +
                                   If(mswpBuffers.Count > 0, 1, 0) + If(armaBuffers.Count > 0, 1, 0) +
                                   If(armoBuffers.Count > 0, 1, 0) + If(otftBuffers.Count > 0, 1, 0) +
                                   If(lvlnBuffers.Count > 0, 1, 0) + If(lvliBuffers.Count > 0, 1, 0)
        Dim totalRecords As Integer = recordBuffers.Count + otftBuffers.Count + lvliBuffers.Count + lvlnBuffers.Count +
                                      mswpBuffers.Count + armaBuffers.Count + armoBuffers.Count + clfmBuffers.Count + grupCount
        Dim tes4Bytes = BuildTes4Header(game, markAsMaster, lightMaster, sortedMasters, totalRecords, nextObjectId, gameMaster, Path.GetDirectoryName(outputPath))

        ' ====================================================================
        ' Step 7: escritura atómica — .tmp y después File.Replace (ver el bloque de abajo; NO es un rename
        ' a secas, que dejaba una ventana sin archivo).
        ' ====================================================================
        Dim outDir = Path.GetDirectoryName(outputPath)
        If Not String.IsNullOrEmpty(outDir) AndAlso Not Directory.Exists(outDir) Then
            Directory.CreateDirectory(outDir)
        End If

        Dim tmpPath = outputPath & ".tmp"
        Using fs As FileStream = File.Create(tmpPath)
            fs.Write(tes4Bytes, 0, tes4Bytes.Length)
            ' Canonical referenced-first GRUP order: CLFM → MSWP → ARMA → ARMO → OTFT → LVLN → LVLI → NPC_ (Step 5).
            If grupClfmBytes.Length > 0 Then fs.Write(grupClfmBytes, 0, grupClfmBytes.Length)
            If grupMswpBytes.Length > 0 Then fs.Write(grupMswpBytes, 0, grupMswpBytes.Length)
            If grupArmaBytes.Length > 0 Then fs.Write(grupArmaBytes, 0, grupArmaBytes.Length)
            If grupArmoBytes.Length > 0 Then fs.Write(grupArmoBytes, 0, grupArmoBytes.Length)
            If grupOtftBytes.Length > 0 Then fs.Write(grupOtftBytes, 0, grupOtftBytes.Length)
            If grupLvlnBytes.Length > 0 Then fs.Write(grupLvlnBytes, 0, grupLvlnBytes.Length)
            If grupLvliBytes.Length > 0 Then fs.Write(grupLvliBytes, 0, grupLvliBytes.Length)
            fs.Write(grupNpcBytes, 0, grupNpcBytes.Length)
        End Using

        ' ⛔ `Delete` + `Move` NO es atómico y el docstring de arriba afirmaba que sí: entre las dos llamadas el
        ' plugin NO EXISTE. Si el Delete sale bien y el Move falla (un handle con FILE_SHARE_DELETE de un
        ' antivirus o del mod manager deja el borrado pendiente, un corte), el usuario se queda SIN el .esp y con
        ' un .esp.tmp al lado — después de haber guardado 300 NPC.
        ' `File.Replace` es el primitivo correcto y ya se usaba en este árbol (LoadOrderActivator.vb:376); exige
        ' que el destino exista, así que el Move queda para el caso "archivo nuevo".
        If File.Exists(outputPath) Then
            File.Replace(tmpPath, outputPath, Nothing, ignoreMetadataErrors:=True)
        Else
            File.Move(tmpPath, outputPath)
        End If

        Return result
    End Function

    ' ========================================================================
    ' Record / Group / Header serialization
    ' ========================================================================

    Private Function SerializeNpcRecord(entry As NpcOverrideEntry, remapper As NpcSubrecordWriter.FormIdRemapper) As Byte()
        Dim body = NpcSubrecordWriter.SerializeNpcBody(entry.Npc, remapper)

        ' Build NPC_ record header (24 bytes).
        Using ms As New MemoryStream()
            Using bw As New BinaryWriter(ms)
                bw.Write(Encoding.ASCII.GetBytes("NPC_"))                          ' Signature
                bw.Write(CUInt(body.Length))                                       ' DataSize
                ' Flags: strip COMPRESSED (we always emit uncompressed bodies). Preserve all
                ' other flags from the original record.
                Dim flags = entry.OriginalHeader.Flags And Not FLAG_COMPRESSED
                bw.Write(flags)
                ' FormID: re-mapped against the new MAST list. The override targets the same
                ' record as the source, with the master index pointing at our new MAST entry.
                bw.Write(remapper(entry.Npc.FormID))
                bw.Write(entry.OriginalHeader.VCS1)
                bw.Write(entry.OriginalHeader.Version)
                bw.Write(entry.OriginalHeader.VCS2)
                bw.Write(body)
            End Using
            Return ms.ToArray()
        End Using
    End Function

    ''' <summary>Serialize a NEW NPC_ record (clone with self-index FormID). Mirrors
    ''' <see cref="SerializeNpcRecord"/> except header uses defaults (no source OriginalHeader): Flags=0
    ''' (no COMPRESSED, no special flags), VCS1=0, Version=record-version of the target game, VCS2=0.
    ''' FormID is the entry's provisional sentinel which the remapper rewrites to the real self-index.</summary>
    Private Function SerializeNpcCreateRecord(entry As NpcCreateEntry, remapper As NpcSubrecordWriter.FormIdRemapper, game As Config_App.Game_Enum) As Byte()
        Dim body = NpcSubrecordWriter.SerializeNpcBody(entry.NpcData, remapper)
        Dim recordVersion As UShort = If(game = Config_App.Game_Enum.Fallout4, TES4_RECORD_VERSION_FO4, TES4_RECORD_VERSION_SSE)

        Using ms As New MemoryStream()
            Using bw As New BinaryWriter(ms)
                bw.Write(Encoding.ASCII.GetBytes("NPC_"))    ' Signature
                bw.Write(CUInt(body.Length))                  ' DataSize
                bw.Write(0UI)                                 ' Flags (no special flags for fresh NPC_)
                bw.Write(remapper(entry.ProvisionalFormID))   ' FormID — remapped to real self-index
                bw.Write(0UI)                                 ' VCS1 — fresh record, no change-tracking history
                bw.Write(recordVersion)                       ' Version (FO4: 0x83 = 131)
                bw.Write(CUShort(0))                          ' VCS2
                bw.Write(body)
            End Using
            Return ms.ToArray()
        End Using
    End Function

    Private Function SerializeExistingRecord(rec As PluginRecord,
                                             existingMasters As List(Of String),
                                             pluginManager As PluginManager,
                                             remapper As NpcSubrecordWriter.FormIdRemapper) As Byte()
        ' For NPC_ records, re-serialize via ParseNPC + NpcSubrecordWriter to get full
        ' MAST cleanup. For other record types (rare in NPC_Manager-generated plugins),
        ' fall back to copy-through with a best-effort 4-byte FormID patch on each subrecord.
        If rec.Header.Signature = "NPC_" Then
            Dim parsed = RecordParsers.ParseNPC(rec, rec.SourcePluginName, pluginManager)
            ' ParseNPC copies rec.Header.FormID verbatim into parsed.FormID
            ' (RecordParsers.vb:1735). For records coming from a fresh PluginReader (the
            ' update-existing path) that value is LOCAL — but SerializeNpcRecord passes it to
            ' the remapper, which expects GLOBAL (GetOriginatingPluginName indexes the high
            ' byte against load order). Without this resolve the record's master high byte
            ' gets rewritten against the wrong plugin in the new MAST list. Subrecord FormIDs
            ' inside parsed are already GLOBAL (ResolveFormIDReference at parse time); only
            ' the record-own FormID needs the explicit resolve.
            parsed.FormID = pluginManager.ResolveReferencedFormID(rec.SourcePluginName, rec.Header.FormID)
            Dim entry As New NpcOverrideEntry With {
                .Npc = parsed,
                .SourcePluginName = rec.SourcePluginName,
                .OriginalHeader = rec.Header
            }
            Return SerializeNpcRecord(entry, remapper)
        End If

        ' Fallback path explicitly NOT supported. NPC_Manager auto-generated plugins should
        ' only ever contain NPC_ records. The "preserve existing" workflow filters them at
        ' load time. If a non-NPC record reaches here, the safest action is to throw — silent
        ' copy-through risks corrupting non-FormID 4-byte subrecords (NAM6 height float, KSIZ
        ' counter, etc. would be misidentified as FormIDs and re-mapped, producing garbage).
        ' See revisor finding m6.
        Throw New NotSupportedException(
            $"SaveNpcEspWriter currently only supports NPC_ records. Encountered '{rec.Header.Signature}' " &
            "while preserving existing records. The plugin file may have been edited externally and contains " &
            "record types this writer does not handle.")
    End Function

    ''' <summary>Serialize one OTFT (outfit) record: 24-byte header + EDID + INAM (array of remapped
    ''' ARMO/LVLI FormIDs). The record FormID is remapped (NEW → self-index via draftRemap; OVERRIDE →
    ''' master remap). INAM is omitted when there are no items.</summary>
    Private Function SerializeOtftRecord(entry As OtftRecordEntry, remapper As NpcSubrecordWriter.FormIdRemapper, game As Config_App.Game_Enum) As Byte()
        Dim body As Byte()
        Using bms As New MemoryStream()
            Using bw As New BinaryWriter(bms)
                ' EDID (ZSTRING, cp1252 — non-translatable, mirrors NpcSubrecordWriter.EmitEdid).
                Dim edidBytes = PluginEncodingSettings.EncodeGeneral(If(entry.EditorID, ""))
                WriteSubrecordHeader(bw, "EDID", edidBytes.Length + 1)
                bw.Write(edidBytes)
                bw.Write(CByte(0))
                ' INAM — array of u32 item FormIDs (ARMO/ARMA/LVLI), remapped. Zero entries skipped.
                Dim items = entry.ItemArmoFormIDs.Where(Function(f) f <> 0UI).ToList()
                If items.Count > 0 Then
                    WriteSubrecordHeader(bw, "INAM", items.Count * 4)
                    For Each fid In items
                        bw.Write(remapper(fid))
                    Next
                End If
            End Using
            body = bms.ToArray()
        End Using

        Dim recordVersion As UShort = If(game = Config_App.Game_Enum.Fallout4, TES4_RECORD_VERSION_FO4, TES4_RECORD_VERSION_SSE)
        Using ms As New MemoryStream()
            Using bw As New BinaryWriter(ms)
                bw.Write(Encoding.ASCII.GetBytes("OTFT"))   ' Signature
                bw.Write(CUInt(body.Length))                ' DataSize
                bw.Write(0UI)                               ' Flags (uncompressed; nothing to preserve)
                bw.Write(remapper(entry.FormID))            ' FormID (self-index for new / master-remap for override)
                bw.Write(entry.OriginalVcs1)                ' VCS1 (preserved from source on overrides, 0 for new drafts)
                bw.Write(recordVersion)                     ' Version
                bw.Write(entry.OriginalVcs2)                ' VCS2 (preserved from source on overrides, 0 for new drafts)
                bw.Write(body)
            End Using
            Return ms.ToArray()
        End Using
    End Function

    ''' <summary>Serializa un record CLFM (Color): header de 24 bytes + cuerpo EDID + [FULL] + CNAM + FNAM.
    ''' <list type="bullet">
    ''' <item>EDID - ZSTRING cp1252 no traducible, el mismo encoder que todos los EDID de aca.</item>
    ''' <item>FULL - nombre opcional. OVERRIDE: <see cref="ClfmRecordEntry.FullNameRaw"/> verbatim. NEW:
    '''   <see cref="ClfmRecordEntry.FullName"/> por <c>EncodeTranslatable</c>, NO por el encoder cp1252 del
    '''   EDID. Los dos vacios = sin FULL. Se emite el literal y no un id de string table porque todo el writer
    '''   apunta a plugins NO localizados y no shippea strings.</item>
    ''' <item>CNAM - wbByteRGBA, 4 bytes [R,G,B,A] (espejo del parser). Medido sobre Skyrim.esm: alpha 0 en los
    '''   178 CLFM.</item>
    ''' <item>FNAM - u32. Skyrim: bool Playable, =1 en los 15 colores de pelo vanilla. FO4: campo de flags donde
    '''   el bit 1 significa "CNAM es un RemappingIndex, no un RGB", que es justo por lo que este camino es
    '''   SSE-only en el caller.</item>
    ''' </list>
    ''' El FormID del record es el self-index real del draft (NEW, via draftRemap) o el global existente
    ''' (OVERRIDE, master-remapeado). El CLFM no lleva FormID en el cuerpo.</summary>
    Private Function SerializeClfmRecord(entry As ClfmRecordEntry, remapper As NpcSubrecordWriter.FormIdRemapper, game As Config_App.Game_Enum) As Byte()
        Dim body As Byte()
        Using bms As New MemoryStream()
            Using bw As New BinaryWriter(bms)
                Dim edidBytes = PluginEncodingSettings.EncodeGeneral(If(entry.EditorID, ""))
                WriteSubrecordHeader(bw, "EDID", edidBytes.Length + 1)
                bw.Write(edidBytes)
                bw.Write(CByte(0))
                ' FULL — verbatim on OVERRIDE (byte-exact round-trip, and no re-encode that could throw),
                ' EncodeTranslatable on NEW. Nothing/empty on both = no subrecord.
                If entry.FullNameRaw IsNot Nothing Then
                    WriteSubrecordHeader(bw, "FULL", entry.FullNameRaw.Length)
                    If entry.FullNameRaw.Length > 0 Then bw.Write(entry.FullNameRaw)
                ElseIf Not String.IsNullOrEmpty(entry.FullName) Then
                    Dim fullBytes = PluginEncodingSettings.EncodeTranslatable(entry.FullName)
                    WriteSubrecordHeader(bw, "FULL", fullBytes.Length + 1)
                    bw.Write(fullBytes)
                    bw.Write(CByte(0))
                End If
                ' CNAM — [R,G,B,A]. ColorRgb is packed 0xRRGGBB (the .jslot convention).
                WriteSubrecordHeader(bw, "CNAM", 4)
                bw.Write(CByte((entry.ColorRgb >> 16) And &HFF))
                bw.Write(CByte((entry.ColorRgb >> 8) And &HFF))
                bw.Write(CByte(entry.ColorRgb And &HFF))
                bw.Write(entry.ColorAlpha)
                ' FNAM — u32.
                WriteSubrecordHeader(bw, "FNAM", 4)
                bw.Write(entry.Flags)
            End Using
            body = bms.ToArray()
        End Using

        Dim recordVersion As UShort = If(game = Config_App.Game_Enum.Fallout4, TES4_RECORD_VERSION_FO4, TES4_RECORD_VERSION_SSE)
        Using ms As New MemoryStream()
            Using bw As New BinaryWriter(ms)
                bw.Write(Encoding.ASCII.GetBytes("CLFM"))
                bw.Write(CUInt(body.Length))
                bw.Write(0UI)                               ' Flags (uncompressed; nothing to preserve)
                bw.Write(remapper(entry.FormID))            ' self-index for NEW / master-remap for OVERRIDE
                bw.Write(entry.OriginalVcs1)
                bw.Write(recordVersion)
                bw.Write(entry.OriginalVcs2)
                bw.Write(body)
            End Using
            Return ms.ToArray()
        End Using
    End Function

    ''' <summary>Serialize one LVLI (leveled item) record: 24-byte header + body. Body order mirrors the
    ''' xEdit FO4 definition (wbDefinitionsFO4.pas:10352): EDID + OBND(zeroed) + LVLD + LVLM + LVLF + LLCT +
    ''' N×LVLO. OBND is zeroed (12 bytes) — meaningless for a leveled list but marked required by xEdit, so
    ''' emitting it keeps the record error-free in xEdit; the engine ignores it. LVLG (Use Global) is omitted
    ''' (no global). Each LVLO is 12 bytes: Level(u16)+pad(2)+Reference(u32, remapped)+Count(u16)+ChanceNone(u8)+pad(1)
    ''' per wbDefinitionsCommon.pas:5704. The record FormID is the draft's real self-index (via draftRemap).</summary>
    Private Function SerializeLvliRecord(entry As LvliRecordEntry, remapper As NpcSubrecordWriter.FormIdRemapper, game As Config_App.Game_Enum) As Byte()
        Dim body As Byte()
        ' ⛔ El cuerpo de esta función era FO4 puro y `game` sólo decidía la Version del header. Los esquemas
        ' DIFIEREN: FO4 (wbDefinitionsFO4.pas:10329-10374) tiene LVLM, LLKC, LVSG y ONAM; TES5 (:8332-8371) NO
        ' tiene ninguno de los cuatro. Ver el gate de cada uno abajo.
        Dim isFo4 As Boolean = (game = Config_App.Game_Enum.Fallout4)
        Using bms As New MemoryStream()
            Using bw As New BinaryWriter(bms)
                ' Subrecord order mirrors wbDefinitionsFO4.pas:10352-10374:
                ' EDID, OBND(req), LVLD, LVLM, LVLF(req), LVLG(opt), LLCT, N×(LVLO + COED?), LLKC(opt), LVSG(opt), ONAM(opt)
                ' En Skyrim: EDID, OBND, LVLD, LVLF, LVLG(opt), LLCT, N×(LVLO + COED?) [, generic model en LVLN].
                ' EDID (ZSTRING, cp1252 — non-translatable, mirrors SerializeOtftRecord / NpcSubrecordWriter).
                Dim edidBytes = PluginEncodingSettings.EncodeGeneral(If(entry.EditorID, ""))
                WriteSubrecordHeader(bw, "EDID", edidBytes.Length + 1)
                bw.Write(edidBytes)
                bw.Write(CByte(0))
                ' OBND (Object Bounds, 6×i16 = 12 bytes). Required per wbDefinitionsFO4.pas:10354
                ' (wbOBND(True)). Preserve verbatim from source on preserve-existing overrides so a
                ' re-save is byte-equivalent; fall back to 12 zero bytes only when no source captured
                ' (NEW drafts authored in Edit Outfit, which have no semantic bounds).
                If entry.ObjectBoundsRaw IsNot Nothing AndAlso entry.ObjectBoundsRaw.Length = 12 Then
                    WriteSubrecordHeader(bw, "OBND", 12)
                    bw.Write(entry.ObjectBoundsRaw)
                Else
                    WriteSubrecordHeader(bw, "OBND", 12)
                    bw.Write(New Byte(11) {})
                End If
                ' LVLD (Chance None, u8).
                WriteSubrecordHeader(bw, "LVLD", 1)
                bw.Write(entry.ChanceNone)
                ' LVLM (Max Count, u8) — ⛔ FO4-ONLY. `grep -c LVLM wbDefinitionsTES5.pas` = 0: el subrecord NO
                ' EXISTE en Skyrim. El esquema TES5 es EDID, OBND, LVLD, LVLF, LVLG, LLCT, entries [, model],
                ' así que emitirlo metía un subrecord desconocido Y fuera de orden entre LVLD y LVLF.
                ' Esta función sólo usaba `game` para la Version del header; el cuerpo era FO4 puro. Alcanzable
                ' con un clic: CheckBoxAddToLvlList no tiene gate de juego (SaveEsp_Form.vb:1001) ni lo tiene
                ' BuildLeveledNpcListEntries (NpcOverrideSaver.vb:929).
                ' Mismo gate para LLKC, LVSG y ONAM más abajo: los cuatro son FO4-only.
                If isFo4 Then
                    WriteSubrecordHeader(bw, "LVLM", 1)
                    bw.Write(entry.MaxCount)
                End If
                ' LVLF (Flags, u8).
                WriteSubrecordHeader(bw, "LVLF", 1)
                bw.Write(entry.Flags)
                ' LVLG (Use Global FormID, optional) — wbDefinitionsFO4.pas:10362.
                If entry.HasUseGlobal Then
                    WriteSubrecordHeader(bw, "LVLG", 4)
                    bw.Write(remapper(entry.UseGlobalFormID))
                End If
                ' LLCT (entry count, u8) — only non-zero entries are emitted. LLCT is itU8 in FO4
                ' (wbDefinitionsFO4.pas:3674), so an LVLI can hold at most 255 entries. Truncating
                ' the counter while emitting all entries leaves the file inconsistent (count claims
                ' fewer entries than are on disk), so we throw instead — the caller must split a
                ' larger list into a chain of nested LVLIs.
                Dim ents = entry.Entries.Where(Function(e) e.RefFormID <> 0UI).ToList()
                If ents.Count > 255 Then
                    Throw New InvalidOperationException(
                        $"LVLI '{If(entry.EditorID, "<no-edid>")}' has {ents.Count} entries; LLCT u8 limit is 255. " &
                        "Split into nested LVLIs.")
                End If
                WriteSubrecordHeader(bw, "LLCT", 1)
                bw.Write(CByte(ents.Count))
                ' N × (LVLO + optional COED). Per wbDefinitionsCommon.pas:5704 LVLO is 12 bytes in FO4:
                ' Level u16 + pad u16 + Reference u32 + Count u16 + ChanceNone u8 + pad u8.
                ' COED (wbDefinitionsFO4.pas:3686-3694) trails the LVLO when the entry carries
                ' per-entry Owner/Rank metadata (12 bytes: Owner u32 + union u32 + Item Condition f32).
                For Each e In ents
                    WriteSubrecordHeader(bw, "LVLO", 12)
                    bw.Write(e.Level)               ' Level (u16)
                    bw.Write(0US)                   ' pad (u16, wbUnused 2)
                    bw.Write(remapper(e.RefFormID)) ' Reference (u32, remapped)
                    bw.Write(e.Count)               ' Count (u16)
                    bw.Write(e.ChanceNone)          ' Chance None (u8)
                    bw.Write(CByte(0))              ' pad (u8, wbUnused 1)
                    If e.HasCoed Then
                        WriteSubrecordHeader(bw, "COED", 12)
                        bw.Write(remapper(e.CoedOwnerFormID))
                        ' Union: GLOB FormID if Owner=NPC_ (CoedExtraIsFormID), else int/unused raw.
                        ' Same conditional-remap rule as NPC_ inventory (wbCOEDOwnerDecider).
                        If e.CoedExtraIsFormID Then
                            bw.Write(remapper(e.CoedOwnerExtra))
                        Else
                            bw.Write(e.CoedOwnerExtra)
                        End If
                        bw.Write(e.CoedItemCondition)
                    End If
                Next
                ' LLKC (Filter Keyword Chances, optional) — wbDefinitionsFO4.pas:10322-10327. xEdit
                ' emits as a single subrecord with N×(Keyword FormID u32 + Chance u32). 0 entries → skip.
                ' ⛔ FO4-ONLY: el esquema TES5 de LVLN (wbDefinitionsTES5.pas:8332-8350) y de LVLI (:8352-8371)
                ' NO declara LLKC. Ver el gate de LVLM más arriba para el porqué de la familia entera.
                Dim filters = If(isFo4, entry.FilterKeywords.Where(Function(f) f.KeywordFormID <> 0UI).ToList(),
                                        New List(Of LvliFilterKeywordData))
                If filters.Count > 0 Then
                    WriteSubrecordHeader(bw, "LLKC", filters.Count * 8)
                    For Each f In filters
                        bw.Write(remapper(f.KeywordFormID))
                        bw.Write(f.Chance)
                    Next
                End If
                ' Tail diverges by record type — the LVLN and LVLI bodies are NOT identical:
                '   LVLN (wbDefinitionsFO4.pas:10349): generic model (MODL/MODT/MODC/MODS/MODF). NO LVSG/ONAM.
                '   LVLI (wbDefinitionsFO4.pas:10372-10373): LVSG (Epic Loot Chance) + ONAM (Override Name). NO model.
                If entry.IsNpcList Then
                    ' LVLN generic model, preserved verbatim in source order salvo el MODS, que LLEVA FormID.
                    ' ⛔ `MODS` no significa lo mismo en los dos juegos y NO se puede decidir por longitud:
                    '   FO4  (wbDefinitionsFO4.pas:4616)  = un u32 [MSWP]
                    '   SSE  (wbDefinitionsTES5.pas:3329) = array de Alternate Textures con FormID de TXST
                    ' El parser ya los dejó GLOBALES en las dos variantes (RecordParsers, Case "MODS"), así que
                    ' acá sólo hay que aplicar el remapper con el layout que corresponde al juego.
                    For Each m In entry.ModelSubrecords
                        Dim mdata = If(m.Data, Array.Empty(Of Byte)())
                        If m.Signature = "MODS" AndAlso isFo4 Then
                            If mdata.Length <> 4 Then _
                                Throw New NotSupportedException(
                                    $"LVLN MODS (Material Swap) has {mdata.Length} bytes; Fallout 4 declares it as a " &
                                    "single FormID (4 bytes). Refusing to emit rather than mis-remapping it.")
                            WriteSubrecordHeader(bw, "MODS", 4)
                            bw.Write(remapper(BitConverter.ToUInt32(mdata, 0)))
                        ElseIf m.Signature = "MODS" Then
                            Dim remapped = RemapAlternateTextures(mdata, Function(g) remapper(g), "LVLN", "MODS")
                            WriteSubrecordHeader(bw, "MODS", remapped.Length)
                            bw.Write(remapped)
                        Else
                            WriteSubrecordHeader(bw, m.Signature, mdata.Length)
                            bw.Write(mdata)
                        End If
                    Next
                ElseIf isFo4 Then
                    ' ⛔ LVSG y ONAM son FO4-only: el LVLI de TES5 (wbDefinitionsTES5.pas:8352-8371) termina en
                    ' las entries. Mismo gate que LVLM/LLKC.
                    ' LVSG (Epic Loot Chance FormID, optional) — wbDefinitionsFO4.pas:10372.
                    If entry.HasEpicLootChance Then
                        WriteSubrecordHeader(bw, "LVSG", 4)
                        bw.Write(remapper(entry.EpicLootChanceFormID))
                    End If
                    ' ONAM (Override Name, optional translatable lstring) — wbDefinitionsFO4.pas:10373.
                    ' Encoded via the central translatable path so non-ASCII overrides survive a re-save.
                    If entry.HasOverrideName Then
                        Dim onamBytes = PluginEncodingSettings.EncodeTranslatable(If(entry.OverrideName, ""))
                        WriteSubrecordHeader(bw, "ONAM", onamBytes.Length + 1)
                        bw.Write(onamBytes)
                        bw.Write(CByte(0))
                    End If
                End If
            End Using
            body = bms.ToArray()
        End Using

        Dim recordVersion As UShort = If(game = Config_App.Game_Enum.Fallout4, TES4_RECORD_VERSION_FO4, TES4_RECORD_VERSION_SSE)
        Using ms As New MemoryStream()
            Using bw As New BinaryWriter(ms)
                bw.Write(Encoding.ASCII.GetBytes(If(entry.IsNpcList, "LVLN", "LVLI")))   ' Signature (LVLN para listas de NPC_)
                bw.Write(CUInt(body.Length))                ' DataSize
                bw.Write(0UI)                               ' Flags (uncompressed)
                bw.Write(remapper(entry.FormID))            ' FormID (self-index via draftRemap for NEW, master-remap for OVERRIDE)
                bw.Write(entry.OriginalVcs1)                ' VCS1 (preserved from source on overrides, 0 for new drafts)
                bw.Write(recordVersion)                     ' Version
                bw.Write(entry.OriginalVcs2)                ' VCS2 (preserved from source on overrides, 0 for new drafts)
                bw.Write(body)
            End Using
            Return ms.ToArray()
        End Using
    End Function

    ' ------------------------------------------------------------------------
    ' MSWP / ARMA / ARMO serializers (NEW records only). Each emits the 24-byte record header
    ' (Signature, DataSize, Flags, remapped FormID, VCS1, Version, VCS2) — same shape as
    ' SerializeOtftRecord / SerializeLvliRecord — followed by the body in xEdit declaration order.
    ' ------------------------------------------------------------------------

    ''' <summary>Emit a NON-translatable ZSTRING subrecord (General/cp1252 encoding + trailing NUL),
    ''' mirror of SerializeOtftRecord's EDID emission and NpcSubrecordWriter.EmitEdid. Used for EDID and
    ''' all model/material paths (MOD2/3/4/5, MSWP BNAM/SNAM/FNAM).</summary>
    Private Sub WriteZString(bw As BinaryWriter, sig As String, value As String)
        Dim bytes = PluginEncodingSettings.EncodeGeneral(If(value, ""))
        WriteSubrecordHeader(bw, sig, bytes.Length + 1)
        bw.Write(bytes)
        bw.Write(CByte(0))
    End Sub

    ''' <summary>Serialize one MSWP (Material Swap) record. Body order per wbDefinitionsFO4.pas:12798:
    ''' EDID, FNAM 'Tree Folder' (optional), then per substitution BNAM 'Original Material' +
    ''' SNAM 'Replacement Material' + CNAM 'Color Remapping Index' (float, only when present). The obsolete
    ''' per-substitution FNAM (12808) is deliberately NOT emitted. MSWP body has no FormIDs; only its own
    ''' record FormID is remapped. Header flags = 0 for NEW records; for OVERRIDE the source header flags
    ''' (COMPRESSED stripped) and source Version are preserved while the body is fully re-emitted from the
    ''' entry — MSWP has no body FormIDs and a simple substitution list, so no subrecord merge is needed.</summary>
    Private Function SerializeMswpRecord(entry As MswpRecordEntry, remapper As NpcSubrecordWriter.FormIdRemapper, game As Config_App.Game_Enum) As Byte()
        Dim body As Byte()
        Using bms As New MemoryStream()
            Using bw As New BinaryWriter(bms)
                WriteZString(bw, "EDID", entry.EditorID)
                ' FNAM 'Tree Folder' (first FNAM) — optional.
                If Not String.IsNullOrEmpty(entry.TreeFolder) Then WriteZString(bw, "FNAM", entry.TreeFolder)
                ' Material Substitutions — preserve list order (engine reads in stream order).
                For Each subst In entry.Substitutions
                    WriteZString(bw, "BNAM", subst.SubstitutionOriginalMaterial)
                    WriteZString(bw, "SNAM", subst.SubstitutionReplacementMaterial)
                    If subst.TieneIndiceDeColor() Then
                        WriteSubrecordHeader(bw, "CNAM", 4)
                        bw.Write(subst.SubstitutionColorRemappingIndex)
                    End If
                Next
            End Using
            body = bms.ToArray()
        End Using

        ' Flags: NEW → 0; OVERRIDE → source header flags with COMPRESSED stripped (we emit uncompressed).
        ' Version: NEW → target game record version; OVERRIDE → source Version (preserve VCS-relevant header).
        Dim flags As UInteger = 0UI
        Dim recordVersion As UShort = If(game = Config_App.Game_Enum.Fallout4, TES4_RECORD_VERSION_FO4, TES4_RECORD_VERSION_SSE)
        If entry.IsOverride Then
            If entry.SourceRecord Is Nothing Then Throw New ArgumentException("MSWP override requires SourceRecord.", NameOf(entry))
            flags = entry.SourceRecord.Header.Flags And Not FLAG_COMPRESSED
            recordVersion = entry.SourceRecord.Header.Version
        End If

        Return WrapRecord("MSWP", body, flags, remapper(entry.FormID), entry.OriginalVcs1, entry.OriginalVcs2, game, recordVersion)
    End Function

    ''' <summary>Serialize one ARMA (Armor Addon). Public so the round-trip probe
    ''' (Tools\ArmoArmaSseRoundtripProbe) can exercise the serializer directly with an identity remapper.
    ''' Game-branched: FO4 body per wbDefinitionsFO4.pas:6210, Skyrim per wbDefinitionsTES5.pas:4409.</summary>
    Public Function SerializeArmaRecord(entry As ArmaRecordEntry, remapper As NpcSubrecordWriter.FormIdRemapper, game As Config_App.Game_Enum, pluginManager As PluginManager) As Byte()
        If entry.IsOverride Then Return SerializeArmaRecordOverride(entry, remapper, game, pluginManager)

        Dim body As Byte()
        Using bms As New MemoryStream()
            Using bw As New BinaryWriter(bms)
                If game = Config_App.Game_Enum.Skyrim Then
                    ' Skyrim ARMA (wbDefinitionsTES5.pas:4409). NEW records: no alternate-texture arrays
                    ' (MO2S/…), no bone-scale (FO4-only). BOD2 = 8 bytes (First-Person Flags + Armor Type).
                    EmitArmaEdid(bw, entry)                          ' EDID     :4410
                    EmitArmaBod2(bw, entry, game)                    ' BOD2     :4411 (wbBODTBOD2 :2651)
                    EmitArmaRnam(bw, entry, remapper)                ' RNAM     :4412
                    EmitArmaDnam(bw, entry)                          ' DNAM     :4413 (same 12-byte layout)
                    EmitArmaBipedModel(bw, entry, remapper, game)    ' MOD2/MOD3 :4430
                    EmitArmaFirstPersonModel(bw, entry, remapper, game) ' MOD4/MOD5 :4435
                    EmitArmaSkinTextures(bw, entry, remapper)        ' NAM0..NAM3 :4440
                    EmitArmaAdditionalRaces(bw, entry, remapper)     ' MODL      :4444
                    EmitArmaSndd(bw, entry, remapper)                ' SNDD      :4445
                    EmitArmaOnam(bw, entry, remapper)                ' ONAM      :4446
                Else
                    ' Owned subrecords in canonical order (wbDefinitionsFO4.pas:6210). Each EmitArmaXxx is the
                    ' SINGLE source of truth for that subrecord's byte layout — shared with the override path.
                    EmitArmaEdid(bw, entry)
                    EmitArmaBod2(bw, entry, game)
                    EmitArmaRnam(bw, entry, remapper)
                    EmitArmaDnam(bw, entry)
                    EmitArmaBipedModel(bw, entry, remapper, game)
                    EmitArmaFirstPersonModel(bw, entry, remapper, game)
                    EmitArmaSkinTextures(bw, entry, remapper)
                    EmitArmaAdditionalRaces(bw, entry, remapper)
                    EmitArmaSndd(bw, entry, remapper)
                    EmitArmaOnam(bw, entry, remapper)                ' ONAM [ARTO]
                    EmitArmaBoneScale(bw, entry)
                End If
            End Using
            body = bms.ToArray()
        End Using

        ' Header flags: FO4 encodes the 3 booleans (bits 6/9/30). Skyrim ARMA has NO named header flags
        ' (wbDefinitionsTES5.pas:4409 declares no wbFlags) → 0 for a NEW Skyrim record.
        Dim flags As UInteger = If(game = Config_App.Game_Enum.Skyrim, 0UI, ComputeArmaHeaderFlags(entry))

        Return WrapRecord("ARMA", body, flags, remapper(entry.FormID), entry.OriginalVcs1, entry.OriginalVcs2, game)
    End Function

    ' ------------------------------------------------------------------------
    ' Shared ARMA owned-subrecord emitters (one source of truth for create + override).
    ' ------------------------------------------------------------------------

    Private Sub EmitArmaEdid(bw As BinaryWriter, entry As ArmaRecordEntry)
        WriteZString(bw, "EDID", entry.EditorID)
    End Sub

    Private Sub EmitArmaBod2(bw As BinaryWriter, entry As ArmaRecordEntry, game As Config_App.Game_Enum, Optional srcBod As SubrecordData? = Nothing)
        If game = Config_App.Game_Enum.Skyrim Then
            ' Skyrim biped body template (wbBODTBOD2, wbDefinitionsTES5.pas:2651). Union of BOD2 (8 bytes: u32
            ' First-Person Flags + u32 Armor Type — the "General Flags" the union also shows is a zero-width
            ' `it0` overlay alias, NOT a separate field) and legacy BODT (12 bytes: adds an explicit General Flags
            ' byte before Armor Type). Neither carries a FormID. On OVERRIDE we PRESERVE the source signature and
            ' size, patching only the First-Person Flags u32 (offset 0 = the slot mask) — so an unedited override
            ' is byte-exact and a slot edit still takes effect while Armor Type is kept. On NEW there is no source
            ' → emit BOD2 (8 bytes) with Armor Type = 0 (Light Armor; the entry doesn't model Armor Type yet).
            If srcBod.HasValue AndAlso srcBod.Value.Data IsNot Nothing AndAlso srcBod.Value.Data.Length >= 4 Then
                Dim src = srcBod.Value
                Dim buf(src.Data.Length - 1) As Byte
                Buffer.BlockCopy(src.Data, 0, buf, 0, src.Data.Length)
                PatchFormIdAt(buf, 0, entry.SlotMask)   ' First-Person Flags u32 (LE) = slot mask
                WriteSubrecordHeader(bw, src.Signature, buf.Length)
                bw.Write(buf)
            Else
                WriteSubrecordHeader(bw, "BOD2", 8)
                bw.Write(entry.SlotMask)   ' First-Person Flags
                bw.Write(0UI)              ' Armor Type (0 = Light Armor; no source on NEW)
            End If
        Else
            ' FO4 BOD2 — single u32 'First Person Flags' = slot mask (wbDefinitionsFO4.pas:3782).
            WriteSubrecordHeader(bw, "BOD2", 4)
            bw.Write(entry.SlotMask)
        End If
    End Sub

    Private Sub EmitArmaRnam(bw As BinaryWriter, entry As ArmaRecordEntry, remapper As NpcSubrecordWriter.FormIdRemapper)
        ' RNAM — Race FormID (optional).
        If entry.RaceFormID <> 0UI Then
            WriteSubrecordHeader(bw, "RNAM", 4)
            bw.Write(remapper(entry.RaceFormID))
        End If
    End Sub

    Private Sub EmitArmaDnam(bw As BinaryWriter, entry As ArmaRecordEntry, Optional srcDnam As Byte() = Nothing)
        ' DNAM — 12-byte struct, REQUIRED (always emit). Layout per wbDefinitionsFO4.pas:6219-6234:
        ' u8 MalePriority, u8 FemalePriority, u8 MaleWeightSlider, u8 FemaleWeightSlider,
        ' 2 bytes 'Unknown' [4-5], u8 DetectionSoundValue, 1 byte 'Unknown' [7], float WeaponAdjust.
        ' The two 'Unknown' fields ([4],[5],[7]) are NOT modelled by ArmaRecordEntry (the editor
        ' exposes only the named fields), yet vanilla ARMAs carry non-zero values there (e.g. 02 00 / 17).
        ' On OVERRIDE we PRESERVE them verbatim from the source DNAM so the record round-trips faithfully;
        ' for a brand-NEW ARMA there is no source, so they default to 0 (CK/xEdit default too). Passing the
        ' source bytes (not plumbing them through draft/entry) keeps the fix local and survives re-edits,
        ' because the override's SourceRecord is always the current winning record (which carries them).
        Dim hasSrc As Boolean = srcDnam IsNot Nothing AndAlso srcDnam.Length >= 8
        WriteSubrecordHeader(bw, "DNAM", 12)
        bw.Write(entry.MalePriority)
        bw.Write(entry.FemalePriority)
        bw.Write(entry.MaleWeightSliderFlags)
        bw.Write(entry.FemaleWeightSliderFlags)
        bw.Write(If(hasSrc, srcDnam(4), CByte(0)))    ' Unknown [4]
        bw.Write(If(hasSrc, srcDnam(5), CByte(0)))    ' Unknown [5]
        bw.Write(entry.DetectionSoundValue)
        bw.Write(If(hasSrc, srcDnam(7), CByte(0)))    ' Unknown [7]
        bw.Write(entry.WeaponAdjust)
    End Sub

    ''' <summary><paramref name="afterMod2"/>/<paramref name="afterMod3"/> (override path only) emit the PRESERVED
    ''' texture-set hashes (MO2T/MO3T) INSIDE the wbTexturedModel struct — right after MOD2/MOD3, before the color/
    ''' swap/flags members. xEdit's ARMA model struct is NOT order-free: emitting MO2T/MO3T as a separate group AFTER
    ''' both models (the old behaviour) makes xEdit report "unexpected (or out of order) subrecord MO2T" and cascade
    ''' every following subrecord as out-of-order, so the whole tail (MOD4/MOD5/MODL/SNDD/BSMx) reads as missing.</summary>
    Private Sub EmitArmaBipedModel(bw As BinaryWriter, entry As ArmaRecordEntry, remapper As NpcSubrecordWriter.FormIdRemapper,
                                   game As Config_App.Game_Enum,
                                   Optional afterMod2 As Action = Nothing, Optional afterMod3 As Action = Nothing)
        ' Biped Model — male textured model first, then female (per xEdit RStruct order).
        ' MO2C/MO2S/MO2F are FO4-only (wbTexturedModel [wbMO2C, wbMO2S, wbMO2F], wbDefinitionsFO4.pas:6237):
        '   • MO2C = color-remap float, MO2F = model-flags byte — do NOT exist in Skyrim's schema.
        '   • MO2S under FO4 is a Material Swap FormID; under Skyrim it is an Alternate-Textures array
        '     (wbMO2S, wbDefinitionsTES5.pas:3325) that the entry does NOT model — so under Skyrim MO2S is
        '     PRESERVED (emitted by the afterMod2 callback), never written from the entry here.
        ' The FO4-only block below is therefore gated on game so a Skyrim ARMA never emits a bogus MO2S FormID
        ' (the parser reads the alt-texture COUNT into MaleMaterialSwapFormID, which must not be re-emitted).
        Dim isFO4 = (game <> Config_App.Game_Enum.Skyrim)
        If Not String.IsNullOrEmpty(entry.MaleMeshPath) Then WriteZString(bw, "MOD2", entry.MaleMeshPath)
        If afterMod2 IsNot Nothing Then afterMod2()   ' MO2T (+ Skyrim MO2S) preserved — inside the male model struct, after MOD2
        If isFO4 Then
            If entry.MaleColorRemapIndex.HasValue Then
                WriteSubrecordHeader(bw, "MO2C", 4)
                bw.Write(entry.MaleColorRemapIndex.Value)
            End If
            If entry.MaleMaterialSwapFormID <> 0UI Then
                WriteSubrecordHeader(bw, "MO2S", 4)
                bw.Write(remapper(entry.MaleMaterialSwapFormID))
            End If
            If entry.MaleModelFlags <> 0 Then
                WriteSubrecordHeader(bw, "MO2F", 1)
                bw.Write(entry.MaleModelFlags)
            End If
        End If
        If Not String.IsNullOrEmpty(entry.FemaleMeshPath) Then WriteZString(bw, "MOD3", entry.FemaleMeshPath)
        If afterMod3 IsNot Nothing Then afterMod3()   ' MO3T (+ Skyrim MO3S) preserved — inside the female model struct, after MOD3
        If isFO4 Then
            If entry.FemaleColorRemapIndex.HasValue Then
                WriteSubrecordHeader(bw, "MO3C", 4)
                bw.Write(entry.FemaleColorRemapIndex.Value)
            End If
            If entry.FemaleMaterialSwapFormID <> 0UI Then
                WriteSubrecordHeader(bw, "MO3S", 4)
                bw.Write(remapper(entry.FemaleMaterialSwapFormID))
            End If
            If entry.FemaleModelFlags <> 0 Then
                WriteSubrecordHeader(bw, "MO3F", 1)
                bw.Write(entry.FemaleModelFlags)
            End If
        End If
    End Sub

    ''' <summary><paramref name="afterMod4"/>/<paramref name="afterMod5"/> (override path only) emit the PRESERVED
    ''' 1st-person members (MO4T/MO4C, MO5T/MO5C) INSIDE the wbTexturedModel struct — right after MOD4/MOD5. Same
    ''' xEdit strict-order requirement as <see cref="EmitArmaBipedModel"/>: emitting them as a separate trailing
    ''' group corrupts the record's subrecord ordering.</summary>
    Private Sub EmitArmaFirstPersonModel(bw As BinaryWriter, entry As ArmaRecordEntry, remapper As NpcSubrecordWriter.FormIdRemapper,
                                         game As Config_App.Game_Enum,
                                         Optional afterMod4 As Action = Nothing, Optional afterMod5 As Action = Nothing)
        ' 1st Person — MOD4/MO4S/MO4F male, MOD5/MO5S/MO5F female (mirror the biped model member order). MO4C/MO5C
        ' (color-remap floats) are NOT modeled by the entry → preserved on override. MO4S/MO5S/MO4F are FO4-only
        ' (see EmitArmaBipedModel note): under Skyrim MO4S/MO5S are Alternate-Textures arrays (wbDefinitionsTES5.pas
        ' :3327-3328), PRESERVED via the afterMod4/afterMod5 callbacks, so the FO4-only block is gated on game.
        Dim isFO4 = (game <> Config_App.Game_Enum.Skyrim)
        If Not String.IsNullOrEmpty(entry.MaleFPMeshPath) Then WriteZString(bw, "MOD4", entry.MaleFPMeshPath)
        If afterMod4 IsNot Nothing Then afterMod4()   ' MO4T/MO4C (+ Skyrim MO4S) preserved — inside the male 1st-person struct
        If isFO4 Then
            If entry.MaleFPMaterialSwapFormID <> 0UI Then
                WriteSubrecordHeader(bw, "MO4S", 4)
                bw.Write(remapper(entry.MaleFPMaterialSwapFormID))
            End If
            If entry.MaleFPModelFlags <> 0 Then
                WriteSubrecordHeader(bw, "MO4F", 1)
                bw.Write(entry.MaleFPModelFlags)
            End If
        End If
        If Not String.IsNullOrEmpty(entry.FemaleFPMeshPath) Then WriteZString(bw, "MOD5", entry.FemaleFPMeshPath)
        If afterMod5 IsNot Nothing Then afterMod5()   ' MO5T/MO5C (+ Skyrim MO5S) preserved — inside the female 1st-person struct
        If isFO4 Then
            If entry.FemaleFPMaterialSwapFormID <> 0UI Then
                WriteSubrecordHeader(bw, "MO5S", 4)
                bw.Write(remapper(entry.FemaleFPMaterialSwapFormID))
            End If
            If entry.FemaleFPModelFlags <> 0 Then
                WriteSubrecordHeader(bw, "MO5F", 1)
                bw.Write(entry.FemaleFPModelFlags)
            End If
        End If
    End Sub

    Private Sub EmitArmaOnam(bw As BinaryWriter, entry As ArmaRecordEntry, remapper As NpcSubrecordWriter.FormIdRemapper)
        ' ONAM — Art Object FormID (optional, [ARTO] per wbDefinitionsFO4.pas:6252). Owned single FormID: emitted
        ' after SNDD, before the Bone Scale block. Loaded from the source on override → unchanged re-emit byte-exact.
        If entry.ArtObjectFormID <> 0UI Then
            WriteSubrecordHeader(bw, "ONAM", 4)
            bw.Write(remapper(entry.ArtObjectFormID))
        End If
    End Sub

    Private Sub EmitArmaSkinTextures(bw As BinaryWriter, entry As ArmaRecordEntry, remapper As NpcSubrecordWriter.FormIdRemapper)
        ' Skin textures / swap lists (NAM0..NAM3).
        If entry.MaleSkinTextureFormID <> 0UI Then
            WriteSubrecordHeader(bw, "NAM0", 4)
            bw.Write(remapper(entry.MaleSkinTextureFormID))
        End If
        If entry.FemaleSkinTextureFormID <> 0UI Then
            WriteSubrecordHeader(bw, "NAM1", 4)
            bw.Write(remapper(entry.FemaleSkinTextureFormID))
        End If
        If entry.MaleSkinTextureSwapListFormID <> 0UI Then
            WriteSubrecordHeader(bw, "NAM2", 4)
            bw.Write(remapper(entry.MaleSkinTextureSwapListFormID))
        End If
        If entry.FemaleSkinTextureSwapListFormID <> 0UI Then
            WriteSubrecordHeader(bw, "NAM3", 4)
            bw.Write(remapper(entry.FemaleSkinTextureSwapListFormID))
        End If
    End Sub

    Private Sub EmitArmaAdditionalRaces(bw As BinaryWriter, entry As ArmaRecordEntry, remapper As NpcSubrecordWriter.FormIdRemapper)
        ' Additional Races — one MODL subrecord per RACE FormID, list order preserved.
        For Each raceFid In entry.AdditionalRaces
            WriteSubrecordHeader(bw, "MODL", 4)
            bw.Write(remapper(raceFid))
        Next
    End Sub

    Private Sub EmitArmaSndd(bw As BinaryWriter, entry As ArmaRecordEntry, remapper As NpcSubrecordWriter.FormIdRemapper)
        ' SNDD — Footstep Sound FormID (optional, [FSTS] per wbDefinitionsFO4.pas:6251). Owned single FormID:
        ' emitted at the canonical position (after Additional Races, before ONAM). The value comes from the
        ' model (loaded from the source on override), so an unchanged override re-emits it byte-exact.
        If entry.FootstepSetFormID <> 0UI Then
            WriteSubrecordHeader(bw, "SNDD", 4)
            bw.Write(remapper(entry.FootstepSetFormID))
        End If
    End Sub

    Private Sub EmitArmaBoneScale(bw As BinaryWriter, entry As ArmaRecordEntry)
        ' Bone Scale Modifier Set — BSMP(u32 gender) then per-bone BSMB(name ZSTRING) + BSMS(3 floats).
        For Each genderBlock In entry.BoneScaleData
            WriteSubrecordHeader(bw, "BSMP", 4)
            bw.Write(genderBlock.Gender)
            For Each boneDelta In genderBlock.Bones
                WriteZString(bw, "BSMB", boneDelta.BoneName)
                WriteSubrecordHeader(bw, "BSMS", 12)
                bw.Write(boneDelta.DeltaX)
                bw.Write(boneDelta.DeltaY)
                bw.Write(boneDelta.DeltaZ)
            Next
        Next
    End Sub

    ''' <summary>ARMA header flags from the three booleans: bit 6 (No Underarmor Scaling),
    ''' bit 9 (Has Sculpt Data), bit 30 (Hi-Res 1st Person Only). NEW records only — the override path
    ''' preserves the source header flags verbatim (per task: ARMA keeps its source flags on override).</summary>
    Private Function ComputeArmaHeaderFlags(entry As ArmaRecordEntry) As UInteger
        Dim flags As UInteger = 0UI
        If entry.NoUnderarmorScaling Then flags = flags Or (1UI << 6)
        If entry.HasSculptData Then flags = flags Or (1UI << 9)
        If entry.HiRes1stPersonOnly Then flags = flags Or (1UI << 30)
        Return flags
    End Function

    ''' <summary>Serialize one ARMO (Armor) record. Body order per wbDefinitionsFO4.pas:6151:
    ''' EDID, OBND(12 zero bytes, required), FULL(opt translatable), Male world model (MOD2/MO2S),
    ''' Female world model (MOD4/MO4S), BOD2(u32 slot mask), RNAM(opt), Keywords(KSIZ+KWDA), Models array
    ''' (INDX u16 + MODL ARMA FormID), DATA(s32 Value + float Weight + u32 Health, required),
    ''' FNAM(u16 ArmorRating + u16 BaseAddonIndex + u8 StaggerRating + 3 unused), TNAM(opt),
    ''' APPR(FormID array, opt). OBTE/OBTS skipped for new records. Header flags = 0.</summary>
    Public Function SerializeArmoRecord(entry As ArmoRecordEntry, remapper As NpcSubrecordWriter.FormIdRemapper, game As Config_App.Game_Enum, pluginManager As PluginManager) As Byte()
        If entry.IsOverride Then Return SerializeArmoRecordOverride(entry, remapper, game, pluginManager)

        If game = Config_App.Game_Enum.Skyrim Then Return SerializeArmoRecordNewSkyrim(entry, remapper)

        Dim body As Byte()
        Using bms As New MemoryStream()
            Using bw As New BinaryWriter(bms)
                ' Owned subrecords in canonical order (wbDefinitionsFO4.pas:6151). Each EmitArmoXxx is the
                ' SINGLE source of truth for that subrecord's byte layout — shared with the override path.
                EmitArmoEdid(bw, entry)
                EmitArmoObnd(bw, entry)                          ' OBND (required, from model — zeroed for a blank new)
                EmitArmoPtrn(bw, entry, remapper)                ' PTRN
                EmitArmoFull(bw, entry)
                EmitArmoEitm(bw, entry, remapper)                ' EITM
                EmitArmoMaleModel(bw, entry, remapper)
                EmitArmoFemaleModel(bw, entry, remapper)
                EmitArmoBod2(bw, entry)
                EmitArmoYnam(bw, entry, remapper)                ' YNAM
                EmitArmoZnam(bw, entry, remapper)                ' ZNAM
                EmitArmoEtyp(bw, entry, remapper)                ' ETYP
                EmitArmoBamt(bw, entry, remapper)                ' BAMT
                EmitArmoRnam(bw, entry, remapper)
                EmitArmoKeywords(bw, entry, remapper)
                EmitArmoDesc(bw, entry)                          ' DESC — presencia de la fuente; ver EmitArmoDesc
                EmitArmoInrd(bw, entry, remapper)
                EmitArmoModels(bw, entry, remapper, game)
                EmitArmoData(bw, entry, game)
                EmitArmoFnam(bw, entry)
                EmitArmoDama(bw, entry, remapper)                ' DAMA
                EmitArmoTnam(bw, entry, remapper)
                EmitArmoAppr(bw, entry, remapper)
                ' OBTE/OBTF/FULL/OBTS (Object Template) — emit from the model when the entry carries
                ' combinations (built from ARMO_Data.Combinations). No-op when empty (unchanged behavior
                ' for records with no object template). wbDefinitionsFO4.pas:5888-5898.
                NpcSubrecordWriter.EmitArmoObjectTemplate(bw, entry.Combinations, remapper)
            End Using
            body = bms.ToArray()
        End Using

        Return WrapRecord("ARMO", body, ComputeArmoHeaderFlags(entry), remapper(entry.FormID), entry.OriginalVcs1, entry.OriginalVcs2, game)
    End Function

    ' ------------------------------------------------------------------------
    ' Shared ARMO owned-subrecord emitters (one source of truth for create + override).
    ' OBND is NOT here: it is owned-zeroed only for NEW records; the override PRESERVES the source OBND.
    ' ------------------------------------------------------------------------

    Private Sub EmitArmoEdid(bw As BinaryWriter, entry As ArmoRecordEntry)
        WriteZString(bw, "EDID", entry.EditorID)
    End Sub

    Private Sub EmitArmoFull(bw As BinaryWriter, entry As ArmoRecordEntry)
        ' FULL — optional, translatable (mirror NpcSubrecordWriter EmitLString).
        If Not String.IsNullOrEmpty(entry.FullName) Then
            Dim fullBytes = PluginEncodingSettings.EncodeTranslatable(entry.FullName)
            WriteSubrecordHeader(bw, "FULL", fullBytes.Length + 1)
            bw.Write(fullBytes)
            bw.Write(CByte(0))
        End If
    End Sub

    ''' <summary><paramref name="afterMod2"/> (override path) emits the PRESERVED textured-model members (MO2T/MODC)
    ''' INSIDE the struct — right after MOD2, before MO2S — mirroring the ARMA fix. xEdit's ARMO world-model struct
    ''' is strict-order too, so emitting them after MO2S corrupts the subrecord ordering (drops the tail on read).</summary>
    Private Sub EmitArmoMaleModel(bw As BinaryWriter, entry As ArmoRecordEntry, remapper As NpcSubrecordWriter.FormIdRemapper,
                                  Optional afterMod2 As Action = Nothing)
        ' Male world model (MOD2) + ARMO-level material swap (MO2S).
        If Not String.IsNullOrEmpty(entry.MaleWorldModelPath) Then WriteZString(bw, "MOD2", entry.MaleWorldModelPath)
        If afterMod2 IsNot Nothing Then afterMod2()   ' MO2T/MODC (preserved) — inside the struct, after MOD2, before MO2S
        If entry.MaleMaterialSwapFormID <> 0UI Then
            WriteSubrecordHeader(bw, "MO2S", 4)
            bw.Write(remapper(entry.MaleMaterialSwapFormID))
        End If
    End Sub

    Private Sub EmitArmoFemaleModel(bw As BinaryWriter, entry As ArmoRecordEntry, remapper As NpcSubrecordWriter.FormIdRemapper,
                                    Optional afterMod4 As Action = Nothing)
        ' Female world model (MOD4) + material swap (MO4S).
        If Not String.IsNullOrEmpty(entry.FemaleWorldModelPath) Then WriteZString(bw, "MOD4", entry.FemaleWorldModelPath)
        If afterMod4 IsNot Nothing Then afterMod4()   ' MO4T/MODC (preserved) — inside the struct, after MOD4, before MO4S
        If entry.FemaleMaterialSwapFormID <> 0UI Then
            WriteSubrecordHeader(bw, "MO4S", 4)
            bw.Write(remapper(entry.FemaleMaterialSwapFormID))
        End If
    End Sub

    Private Sub EmitArmoBod2(bw As BinaryWriter, entry As ArmoRecordEntry)
        ' BOD2 — single u32 slot mask.
        WriteSubrecordHeader(bw, "BOD2", 4)
        bw.Write(entry.SlotMask)
    End Sub

    Private Sub EmitArmoRnam(bw As BinaryWriter, entry As ArmoRecordEntry, remapper As NpcSubrecordWriter.FormIdRemapper)
        ' RNAM — Race FormID (optional).
        If entry.RaceFormID <> 0UI Then
            WriteSubrecordHeader(bw, "RNAM", 4)
            bw.Write(remapper(entry.RaceFormID))
        End If
    End Sub

    Private Sub EmitArmoInrd(bw As BinaryWriter, entry As ArmoRecordEntry, remapper As NpcSubrecordWriter.FormIdRemapper)
        ' INRD — Instance Naming FormID (optional, [INNR] per wbDefinitionsFO4.pas:6186/5632). Owned single
        ' FormID: emitted at the canonical position (after DESC, before the Models array). The value comes from
        ' the model (loaded from the source on override), so an unchanged override re-emits it byte-exact.
        If entry.InstanceNamingFormID <> 0UI Then
            WriteSubrecordHeader(bw, "INRD", 4)
            bw.Write(remapper(entry.InstanceNamingFormID))
        End If
    End Sub

    ''' <summary>Emit one owned optional single-FormID ARMO subrecord (omit when 0). Shared by EITM/PTRN/YNAM/
    ''' ZNAM/ETYP/BAMT — each is a 4-byte remapped FormID at its canonical position.</summary>
    Private Sub EmitArmoOptionalFormId(bw As BinaryWriter, sig As String, globalFormID As UInteger, remapper As NpcSubrecordWriter.FormIdRemapper)
        If globalFormID <> 0UI Then
            WriteSubrecordHeader(bw, sig, 4)
            bw.Write(remapper(globalFormID))
        End If
    End Sub

    Private Sub EmitArmoEitm(bw As BinaryWriter, entry As ArmoRecordEntry, remapper As NpcSubrecordWriter.FormIdRemapper)
        EmitArmoOptionalFormId(bw, "EITM", entry.EnchantmentFormID, remapper)   ' Object Effect [ENCH]
    End Sub

    Private Sub EmitArmoPtrn(bw As BinaryWriter, entry As ArmoRecordEntry, remapper As NpcSubrecordWriter.FormIdRemapper)
        EmitArmoOptionalFormId(bw, "PTRN", entry.PatternFormID, remapper)       ' Preview Transform [TRNS]
    End Sub

    Private Sub EmitArmoYnam(bw As BinaryWriter, entry As ArmoRecordEntry, remapper As NpcSubrecordWriter.FormIdRemapper)
        EmitArmoOptionalFormId(bw, "YNAM", entry.PickupSoundFormID, remapper)   ' Pickup Sound [SNDR]
    End Sub

    Private Sub EmitArmoZnam(bw As BinaryWriter, entry As ArmoRecordEntry, remapper As NpcSubrecordWriter.FormIdRemapper)
        EmitArmoOptionalFormId(bw, "ZNAM", entry.DropSoundFormID, remapper)     ' Drop Sound [SNDR]
    End Sub

    Private Sub EmitArmoEtyp(bw As BinaryWriter, entry As ArmoRecordEntry, remapper As NpcSubrecordWriter.FormIdRemapper)
        EmitArmoOptionalFormId(bw, "ETYP", entry.EquipTypeFormID, remapper)     ' Equip Type [EQUP]
    End Sub

    Private Sub EmitArmoBamt(bw As BinaryWriter, entry As ArmoRecordEntry, remapper As NpcSubrecordWriter.FormIdRemapper)
        EmitArmoOptionalFormId(bw, "BAMT", entry.AlternateBlockMaterialFormID, remapper)  ' Alternate Block Material [MATT]
    End Sub

    Private Sub EmitArmoDesc(bw As BinaryWriter, entry As ArmoRecordEntry, Optional required As Boolean = False)
        ' DESC — translatable lstring. FO4 (wbDESC, wbDefinitionsFO4.pas ARMO): OPTIONAL. SKYRIM
        ' (wbDESC.SetRequired, wbDefinitionsTES5.pas:4399): REQUIRED → se emite siempre, aunque el texto resuelto
        ' sea vacío (las piezas de armadura suelen traer descripción vacía; en un master localizado el DESC es un
        ' id de lstring que resuelve a ""). Tirarlo corrompe el orden de subrecords: todo lo siguiente se corre.
        '
        ' ⛔ La PRESENCIA la decide <c>entry.HasDescription</c>, no que el texto esté vacío. Antes esto se
        ' preguntaba al record fuente en el call site, y por eso era imposible expresar "sacá la descripción":
        ' limpiar el texto no quitaba el subrecord. El parámetro <paramref name="required"/> queda para el caso
        ' del FORMATO (Skyrim) y para los call sites que aún derivan la presencia de la fuente.
        ' ⛔ <paramref name="required"/> lo pasa SÓLO el camino de records NUEVOS de Skyrim, donde el DESC es
        ' `wbDESC.SetRequired` (wbDefinitionsTES5.pas:4399) y hay que emitirlo aunque el texto sea vacío.
        '
        ' ⛔⛔ En los OVERRIDES manda la PRESENCIA DE LA FUENTE (<c>entry.HasDescription</c>), NO el "required"
        ' del formato. Llegué a poner `required:=True` en el override de Skyrim "para respetar la ley" y eso
        ' INYECTA un DESC en records que no lo traían: MEDIDO, el oráculo byte-exacto pasó de 2762/2762 a
        ' 2752/2762 con `DESC-presence-only=10`. Un override reproduce lo que había; el `SetRequired` del
        ' canónico describe qué debe tener un record NUEVO, no autoriza a agregarle subrecords a uno ajeno.
        If required OrElse entry.HasDescription OrElse Not String.IsNullOrEmpty(entry.Description) Then
            Dim descBytes = PluginEncodingSettings.EncodeTranslatable(If(entry.Description, ""))
            WriteSubrecordHeader(bw, "DESC", descBytes.Length + 1)
            bw.Write(descBytes)
            bw.Write(CByte(0))
        End If
    End Sub

    Private Sub EmitArmoObnd(bw As BinaryWriter, entry As ArmoRecordEntry)
        ' OBND — required struct, 6×i16 min/max XYZ. Always emitted (from the model — zeroed for a blank new).
        WriteSubrecordHeader(bw, "OBND", 12)
        bw.Write(entry.ObndX1)
        bw.Write(entry.ObndY1)
        bw.Write(entry.ObndZ1)
        bw.Write(entry.ObndX2)
        bw.Write(entry.ObndY2)
        bw.Write(entry.ObndZ2)
    End Sub

    Private Sub EmitArmoDama(bw As BinaryWriter, entry As ArmoRecordEntry, remapper As NpcSubrecordWriter.FormIdRemapper)
        ' DAMA — Damage Type Array. FO4 stride 8: Type FormID [DMGT] @0 + Amount u32 @4. Omit when empty.
        ' Stride is always 8 here: the 12-byte Curve Table stride (Version >= 152) is FO76/SF1-only and
        ' ParseARMO rejects it at load, so the model never carries a 12-byte entry. (SSE ARMO has no DAMA.)
        Dim list = entry.DamageResistances
        If list Is Nothing OrElse list.Count = 0 Then Return
        WriteSubrecordHeader(bw, "DAMA", list.Count * 8)
        For Each dr In list
            bw.Write(remapper(dr.DamageTypeFormID))
            bw.Write(dr.Value)
        Next
    End Sub

    ''' <summary>ARMO header flags from the modeled booleans: bit 2 (Non-Playable). Other source flag bits are
    ''' preserved by the override path; NEW records only carry this bit.</summary>
    Private Function ComputeArmoHeaderFlags(entry As ArmoRecordEntry) As UInteger
        Dim flags As UInteger = 0UI
        If entry.NonPlayable Then flags = flags Or (1UI << 2)
        Return flags
    End Function

    Private Sub EmitArmoKeywords(bw As BinaryWriter, entry As ArmoRecordEntry, remapper As NpcSubrecordWriter.FormIdRemapper)
        ' Keywords — KSIZ (u32 count) + KWDA (count*4 remapped FormIDs). Mirror NpcSubrecordWriter.EmitKeywords.
        Dim kwds = entry.KeywordFormIDs.Where(Function(f) f <> 0UI).ToList()
        If kwds.Count > 0 Then
            WriteSubrecordHeader(bw, "KSIZ", 4)
            bw.Write(CUInt(kwds.Count))
            WriteSubrecordHeader(bw, "KWDA", kwds.Count * 4)
            For Each fid In kwds
                bw.Write(remapper(fid))
            Next
        End If
    End Sub

    Private Sub EmitArmoModels(bw As BinaryWriter, entry As ArmoRecordEntry, remapper As NpcSubrecordWriter.FormIdRemapper, game As Config_App.Game_Enum)
        If game = Config_App.Game_Enum.Skyrim Then
            ' Skyrim Armature = wbRArray of PLAIN MODL FormID → ARMA, NO INDX (wbDefinitionsTES5.pas:4400).
            ' Emit only the MODL FormIDs in list order.
            For Each addon In entry.ArmorAddons
                WriteSubrecordHeader(bw, "MODL", 4)
                bw.Write(remapper(addon.ArmaFormID))
            Next
        Else
            ' FO4 Models array — INDX (u16) + MODL (ARMA FormID), list order preserved (wbDefinitionsFO4.pas:6187).
            For Each addon In entry.ArmorAddons
                WriteSubrecordHeader(bw, "INDX", 2)
                bw.Write(addon.AddonIndex)
                WriteSubrecordHeader(bw, "MODL", 4)
                bw.Write(remapper(addon.ArmaFormID))
            Next
        End If
    End Sub

    Private Sub EmitArmoData(bw As BinaryWriter, entry As ArmoRecordEntry, game As Config_App.Game_Enum)
        If game = Config_App.Game_Enum.Skyrim Then
            ' Skyrim DATA — required struct: s32 Value + float Weight ONLY (8 bytes, NO Health)
            ' (wbDefinitionsTES5.pas:4401-4404).
            WriteSubrecordHeader(bw, "DATA", 8)
            bw.Write(entry.Value)
            bw.Write(entry.Weight)
        Else
            ' FO4 DATA — required (wbStruct cpNormal True): s32 Value, float Weight, u32 Health (12 bytes).
            WriteSubrecordHeader(bw, "DATA", 12)
            bw.Write(entry.Value)
            bw.Write(entry.Weight)
            bw.Write(entry.Health)
        End If
    End Sub

    ''' <summary>SKYRIM DNAM 'Armor Rating' (wbDefinitionsTES5.pas:4405, itS32, required). The wire value is the
    ''' rating×100 (xEdit divides by 100 for display); the parser captured the raw wire value into
    ''' <see cref="ArmoRecordEntry.SkyrimArmorRating"/>, so an unedited override re-emits it byte-exact. FO4 has
    ''' no ARMO DNAM (armor rating lives in FNAM), so this is only called on the Skyrim path.</summary>
    Private Sub EmitArmoDnamSkyrim(bw As BinaryWriter, entry As ArmoRecordEntry)
        WriteSubrecordHeader(bw, "DNAM", 4)
        bw.Write(entry.SkyrimArmorRating)
    End Sub

    Private Sub EmitArmoFnam(bw As BinaryWriter, entry As ArmoRecordEntry)
        ' FNAM — u16 ArmorRating, u16 BaseAddonIndex, u8 StaggerRating, 3 unused.
        WriteSubrecordHeader(bw, "FNAM", 8)
        bw.Write(entry.ArmorRating)
        bw.Write(entry.BaseAddonIndex)
        bw.Write(entry.StaggerRating)
        bw.Write(New Byte(2) {})         ' 3 unused bytes
    End Sub

    Private Sub EmitArmoTnam(bw As BinaryWriter, entry As ArmoRecordEntry, remapper As NpcSubrecordWriter.FormIdRemapper)
        ' TNAM — Template Armor FormID (optional).
        If entry.TemplateArmorFormID <> 0UI Then
            WriteSubrecordHeader(bw, "TNAM", 4)
            bw.Write(remapper(entry.TemplateArmorFormID))
        End If
    End Sub

    Private Sub EmitArmoAppr(bw As BinaryWriter, entry As ArmoRecordEntry, remapper As NpcSubrecordWriter.FormIdRemapper)
        ' APPR — Attach Parent Slots, array of remapped u32 FormIDs (mirror NpcSubrecordWriter.EmitAppr).
        Dim appr = entry.AttachParentSlotFormIDs.Where(Function(f) f <> 0UI).ToList()
        If appr.Count > 0 Then
            WriteSubrecordHeader(bw, "APPR", appr.Count * 4)
            For Each fid In appr
                bw.Write(remapper(fid))
            Next
        End If
    End Sub

    ''' <summary>Wrap a record body in the 24-byte record header (Signature, DataSize, Flags, FormID,
    ''' VCS1, Version, VCS2). Shared by create + override paths. The FormID passed is already remapped.
    ''' <paramref name="versionOverride"/> (override path) forces a specific record Version (the source
    ''' record's) instead of the target game's default — preserve the source header on re-save.</summary>
    Private Function WrapRecord(signature As String, body As Byte(), flags As UInteger, mappedFormID As UInteger,
                                vcs1 As UInteger, vcs2 As UShort, game As Config_App.Game_Enum,
                                Optional versionOverride As UShort = 0US) As Byte()
        Dim recordVersion As UShort = If(versionOverride <> 0US, versionOverride,
                                         If(game = Config_App.Game_Enum.Fallout4, TES4_RECORD_VERSION_FO4, TES4_RECORD_VERSION_SSE))
        Using ms As New MemoryStream()
            Using bw As New BinaryWriter(ms)
                bw.Write(Encoding.ASCII.GetBytes(signature))    ' Signature
                bw.Write(CUInt(body.Length))                    ' DataSize
                bw.Write(flags)                                 ' Flags
                bw.Write(mappedFormID)                          ' FormID (already remapped)
                bw.Write(vcs1)                                  ' VCS1
                bw.Write(recordVersion)                         ' Version
                bw.Write(vcs2)                                  ' VCS2
                bw.Write(body)
            End Using
            Return ms.ToArray()
        End Using
    End Function

    ' Serializers de OVERRIDE-MERGE (re-guardar un ARMA/ARMO existente con ediciones).
    ' El entry trae el estado FINAL de cada campo PROPIO y el writer emite el record en el orden canónico
    ' de xEdit: los subrecords propios salen del entry (por los MISMOS helpers que usa el camino de
    ' creación, una sola fuente para el layout de bytes) y todo lo demás se copia VERBATIM del source,
    ' remapeando los FormID que lleve de la MAST list del source a la nueva.
    ' ⛔ Un FormID guardado en los bytes del source es LOCAL a ese plugin: hay que resolverlo a global por
    ' su propia MAST list y recién ahí pasarlo por el remapper. Y si una signatura preservada puede llevar
    ' FormIDs pero su layout no está clasificado acá, se TIRA en vez de copiar los bytes crudos — que
    ' apuntarían al master equivocado. Fail-loud, igual que el resto del writer.

    ''' <summary>Firmas de subrecord preservado que llevan exactamente UN FormID de 4 bytes en el offset 0 (el
    ''' payload entero es el FormID): del ARMO quedan BIDS ([IPDS]) y DMDS ([MSWP], dentro del DEST).
    ''' <para>EITM/PTRN/YNAM/ZNAM/ETYP/BAMT del ARMO y ONAM/MO4S/MO5S del ARMA fueron promovidos a subrecords
    ''' PROPIOS (se emiten desde la entry en su posicion canonica), asi que ya no se preservan. INRD del ARMO y
    ''' SNDD del ARMA tampoco estan aca por lo mismo.</para></summary>
    Private ReadOnly _singleFormIdPreservedSigs As New HashSet(Of String)(
        {"BIDS", "DMDS"},
        StringComparer.Ordinal)

    ''' <summary>BMCT (Skyrim ARMO 'Ragdoll Constraint Template', wbDefinitionsTES5.pas:4393) is a plain string —
    ''' no FormID — so it is copied verbatim. FO4 ARMO has no BMCT, so this only matters under Skyrim.</summary>
    Private ReadOnly _verbatimPreservedSigs As New HashSet(Of String)(
        {"MO2T", "MO3T", "MO4T", "MO5T", "MODC", "MO2C", "MO3C", "MO4C", "MO5C",
         "MO2F", "MO3F", "MO4F", "MO5F", "ICON", "MICO", "ICO2", "MIC2", "EAMT",
         "DSTA", "DMDL", "DMDT", "DMDC", "DMDF", "DSTF", "STOP", "OBTF", "BMCT"},
        StringComparer.Ordinal)

    ''' <summary>Destruction-family signatures (wbDEST, wbDefinitionsFO4.pas:4641): the DEST header, the
    ''' DAMC resistances array, and the per-stage subs (DSTD/DSTA/DMDL/DMDT/DMDC/DMDF/DMDS/DSTF). A DEST with
    ''' ≥2 stages has REPEATED DSTD/DMDL/... so the region MUST be emitted as one contiguous source-ordered
    ''' block (like the Object Template block) — draining per-signature would scramble stage order without
    ''' tripping the drop-nothing assertion (silent corruption). Captured contiguously from the first DEST.</summary>
    Private ReadOnly _destructionFamilySigs As New HashSet(Of String)(
        {"DEST", "DAMC", "DSTD", "DSTA", "DMDL", "DMDT", "DMDC", "DMDF", "DMDS", "DSTF"},
        StringComparer.Ordinal)

    ''' <summary>Build a SOURCE-LOCAL→NEW-MAST FormID mapper for preserved subrecords of <paramref name="src"/>.
    ''' Resolves the source-local FormID to GLOBAL via the source plugin's MAST list, then through the global
    ''' remapper. 0 stays 0 (NULL ref).</summary>
    Private Function MakeLocalRemap(src As PluginRecord, remapper As NpcSubrecordWriter.FormIdRemapper, pluginManager As PluginManager) As Func(Of UInteger, UInteger)
        Dim srcName = src.SourcePluginName
        Return Function(rawLocal As UInteger) As UInteger
                   If rawLocal = 0UI Then Return 0UI
                   Dim globalFid = pluginManager.ResolveReferencedFormID(srcName, rawLocal)
                   Return remapper(globalFid)
               End Function
    End Function

    ''' <summary>Remap the embedded TXST 'New Texture' FormIDs of a SKYRIM Alternate-Textures array
    ''' (MO2S/MO3S/MO4S/MO5S under Skyrim; wbArrayS(sig, wbAlternateTexture, -1), wbDefinitionsTES5.pas:3325).
    ''' Payload: u32 count, then per entry { u32 3D-Name length, ASCII name (no NUL), u32 New-Texture FormID
    ''' [TXST], s32 3D-Index }. Returns a copy with each TXST FormID passed through <paramref name="mapLocal"/>;
    ''' all other bytes are preserved verbatim (identity remap → byte-exact). Throws if the payload is truncated
    ''' mid-entry (fail loud rather than corrupt).</summary>
    Private Function RemapAlternateTextures(raw As Byte(), mapLocal As Func(Of UInteger, UInteger), recSig As String, sig As String) As Byte()
        Dim buf(raw.Length - 1) As Byte
        If raw.Length > 0 Then Buffer.BlockCopy(raw, 0, buf, 0, raw.Length)
        If raw.Length < 4 Then Return buf   ' no count field → nothing to remap
        ' ⛔ TODO en Long, igual que el lector gemelo (RecordParsers.ResolveAlternateTexturesToGlobal). Con
        ' Integer, un `count`/`nameLen` basura hacía que `CInt` de un UInteger >= 2^31 tirara OverflowException
        ' —los checks de desborde de VB están ON— en vez de la NotSupportedException declarada, y el mensaje no
        ' nombraba el subrecord. Acá importa MÁS que en el lector: los MO2S/MO3S/MO4S/MO5S de SSE llegan
        ' VERBATIM (el parser los saltea bajo Skyrim), así que este es el ÚNICO punto que los valida.
        Dim count As Long = CLng(BitConverter.ToUInt32(raw, 0))
        Dim offset As Long = 4L
        For i As Long = 0 To count - 1L
            If offset + 4L > raw.Length Then _
                Throw New NotSupportedException($"{recSig} override: preserved {sig} (Alternate Textures) truncated reading entry {i} name length.")
            Dim nameLen As Long = CLng(BitConverter.ToUInt32(raw, CInt(offset)))
            offset += 4L + nameLen                        ' skip the u32 length + the ASCII 3D-Name bytes
            If offset + 8L > raw.Length Then _
                Throw New NotSupportedException($"{recSig} override: preserved {sig} (Alternate Textures) truncated reading entry {i} TXST FormID / 3D-Index.")
            Dim o = CInt(offset)                          ' seguro: el chequeo de arriba acota offset + 8 <= Length
            PatchFormIdAt(buf, o, mapLocal(BitConverter.ToUInt32(raw, o)))   ' New Texture [TXST]
            offset += 8L                                  ' skip the FormID + the s32 3D-Index
        Next
        Return buf
    End Function

    ''' <summary>Patch a 4-byte little-endian FormID at <paramref name="offset"/> inside <paramref name="buf"/>.</summary>
    Private Sub PatchFormIdAt(buf As Byte(), offset As Integer, value As UInteger)
        buf(offset + 0) = CByte(value And &HFFUI)
        buf(offset + 1) = CByte((value >> 8) And &HFFUI)
        buf(offset + 2) = CByte((value >> 16) And &HFFUI)
        buf(offset + 3) = CByte((value >> 24) And &HFFUI)
    End Sub

    ''' <summary>Remap one OBTS payload's FormIDs in a raw copy, reading counts from the bytes themselves
    ''' (IncludeCount @0, PropertyCount @4, KeywordCount @15) — layout per ParseOBTSPayload
    ''' (RecordParsers.vb:1763, wbDefinitionsFO4.pas:5867). Keywords (@16+), OMOD Includes (Mod FormID @
    ''' start of each 7-byte entry), and Property Value1 (@offset+12 when ValueType is FormIDInt=4 /
    ''' FormIDFloat=6, wbObjectModProperties:5826) are remapped via <paramref name="mapLocal"/>. Mirror of
    ''' NpcSubrecordWriter.ApplyObtsRemap, generalized to source-local raw bytes.</summary>
    Private Function RemapObtsPayload(raw As Byte(), mapLocal As Func(Of UInteger, UInteger)) As Byte()
        Dim payload(raw.Length - 1) As Byte
        Buffer.BlockCopy(raw, 0, payload, 0, raw.Length)
        If raw.Length < 17 Then Return payload

        Dim includeCount As Integer = CInt(BitConverter.ToUInt32(payload, 0))
        Dim propertyCount As Integer = CInt(BitConverter.ToUInt32(payload, 4))
        Dim offset As Integer = 15
        Dim kwCount As Integer = CInt(payload(offset))
        offset += 1
        For i = 0 To kwCount - 1
            If offset + 4 > payload.Length Then Exit For
            PatchFormIdAt(payload, offset, mapLocal(BitConverter.ToUInt32(payload, offset)))
            offset += 4
        Next
        offset += 2 ' MinLevelForRanks + AltLevelsPerTier
        For i = 0 To includeCount - 1
            If offset + 7 > payload.Length Then Exit For
            PatchFormIdAt(payload, offset, mapLocal(BitConverter.ToUInt32(payload, offset)))  ' Mod FormID @ entry start
            offset += 7
        Next
        Const propertyEntrySize As Integer = 24
        For i = 0 To propertyCount - 1
            If offset + propertyEntrySize > payload.Length Then Exit For
            Dim valueType As Byte = payload(offset)
            If valueType = CByte(OMOD_ValueType.FormIDInt) OrElse valueType = CByte(OMOD_ValueType.FormIDFloat) Then
                PatchFormIdAt(payload, offset + 12, mapLocal(BitConverter.ToUInt32(payload, offset + 12)))
            End If
            offset += propertyEntrySize
        Next
        Return payload
    End Function

    ''' <summary>Emit ONE preserved source subrecord with its FormIDs remapped to the new MAST list.
    ''' Classification table (see _singleFormIdPreservedSigs / _verbatimPreservedSigs + the complex cases
    ''' below). VMAD reuses NpcVmadScanner.Scan + NpcSubrecordWriter.EmitVmad. Unknown / unclassifiable
    ''' signatures THROW — never blind-copy FormID bytes. <paramref name="recSig"/> is the owning record
    ''' signature (ARMO/ARMA) used only for the error message.</summary>
    Private Sub EmitPreservedSubrecord(bw As BinaryWriter, sr As SubrecordData, recSig As String,
                                       src As PluginRecord, remapper As NpcSubrecordWriter.FormIdRemapper,
                                       pluginManager As PluginManager, mapLocal As Func(Of UInteger, UInteger))
        Dim sig = sr.Signature
        Dim data = If(sr.Data, Array.Empty(Of Byte)())

        ' --- Non-FormID: copy verbatim ---
        If _verbatimPreservedSigs.Contains(sig) Then
            WriteSubrecordHeader(bw, sig, data.Length)
            If data.Length > 0 Then bw.Write(data)
            Return
        End If

        ' --- Single 4-byte FormID @0 ---
        If _singleFormIdPreservedSigs.Contains(sig) Then
            If data.Length < 4 Then
                ' Defensive: a sig we expect to be a FormID but with no 4 bytes — copy verbatim
                ' (can't be a FormID). Keeps malformed-but-harmless data instead of throwing.
                WriteSubrecordHeader(bw, sig, data.Length)
                If data.Length > 0 Then bw.Write(data)
                Return
            End If
            Dim buf(data.Length - 1) As Byte
            Buffer.BlockCopy(data, 0, buf, 0, data.Length)
            PatchFormIdAt(buf, 0, mapLocal(BitConverter.ToUInt32(data, 0)))
            WriteSubrecordHeader(bw, sig, buf.Length)
            bw.Write(buf)
            Return
        End If

        ' --- Complex FormID-bearing ---
        Select Case sig
            Case "VMAD"
                ' Reuse the scanner (FormID positions) + the NPC writer's position-patching emitter.
                Dim vmad = NpcVmadScanner.Scan(data, src.SourcePluginName, pluginManager)
                If vmad Is Nothing Then
                    ' Malformed VMAD; preserve raw (no FormIDs found to remap).
                    WriteSubrecordHeader(bw, sig, data.Length)
                    If data.Length > 0 Then bw.Write(data)
                Else
                    NpcSubrecordWriter.EmitVmad(bw, vmad, remapper)
                End If

            Case "DEST"
                ' DEST 'Header' struct: Health s32 @0, DEST Count u8 @4, Flags u8 @5, Unknown 2 @6 — NO
                ' FormIDs (wbDefinitionsFO4.pas:4642). Copy verbatim.
                WriteSubrecordHeader(bw, sig, data.Length)
                If data.Length > 0 Then bw.Write(data)

            Case "DAMC"
                ' DEST 'Resistances' array (wbDEST:4656): N × (Damage Type FormID [DMGT] @0 + Value u32 @4),
                ' stride 8. Remap each entry's Type FormID.
                If data.Length Mod 8 <> 0 Then
                    Throw New NotSupportedException(
                        $"{recSig} override: preserved DAMC payload length {data.Length} is not a multiple of 8 (DMGT FormID + u32).")
                End If
                Dim buf(data.Length - 1) As Byte
                Buffer.BlockCopy(data, 0, buf, 0, data.Length)
                Dim n = data.Length \ 8
                For i = 0 To n - 1
                    PatchFormIdAt(buf, i * 8, mapLocal(BitConverter.ToUInt32(data, i * 8)))
                Next
                WriteSubrecordHeader(bw, sig, buf.Length)
                bw.Write(buf)

            Case "DSTD"
                ' Destruction Stage Data (wbDEST:4662, 20 bytes): Explosion FormID @8, Debris FormID @12.
                If data.Length < 16 Then
                    Throw New NotSupportedException(
                        $"{recSig} override: preserved DSTD payload length {data.Length} < 16 (cannot locate Explosion/Debris FormIDs).")
                End If
                Dim buf(data.Length - 1) As Byte
                Buffer.BlockCopy(data, 0, buf, 0, data.Length)
                PatchFormIdAt(buf, 8, mapLocal(BitConverter.ToUInt32(data, 8)))    ' Explosion [EXPL,NULL]
                PatchFormIdAt(buf, 12, mapLocal(BitConverter.ToUInt32(data, 12)))  ' Debris [DEBR,NULL]
                WriteSubrecordHeader(bw, sig, buf.Length)
                bw.Write(buf)

            Case "DAMA"
                ' Damage Type Array (wbDamageTypeArray, wbDefinitionsCommon.pas:5677): N × struct
                '   Type FormID [DMGT] @0 + Amount u32 @4 + (FromVersion 152) Curve Table FormID [CURV] @8.
                ' Entry stride is 8 (pre-152) or 12 (152+ with Curve Table). Determine from divisibility;
                ' both Type and Curve Table FormIDs are remapped. If the length fits neither stride cleanly,
                ' THROW (do not guess — wrong stride would corrupt every FormID).
                EmitDamageTypeArray(bw, sig, data, mapLocal, recSig)

            Case "OBTS"
                WriteSubrecordHeader(bw, sig, data.Length)
                bw.Write(RemapObtsPayload(data, mapLocal))

            Case "MO2S", "MO3S", "MO4S", "MO5S"
                ' SKYRIM Alternate-Textures array (wbMO2S = wbArrayS(MO2S, wbAlternateTexture, -1),
                ' wbDefinitionsTES5.pas:3325-3328). Payload = u32 count + count × { u32 nameLen, ASCII 3D-Name,
                ' u32 New-Texture FormID [TXST], s32 3D-Index }. The embedded TXST FormIDs are remapped. (FO4
                ' MO2S/MO3S is a single MSWP FormID handled as an OWNED entry field, never routed here — so this
                ' case is Skyrim-only. MO4S/MO5S alt-textures are also handled here under Skyrim.)
                WriteSubrecordHeader(bw, sig, data.Length)
                bw.Write(RemapAlternateTextures(data, mapLocal, recSig, sig))

            Case Else
                ' Unknown signature: may carry FormIDs we cannot place. Fail loud rather than corrupt.
                Throw New NotSupportedException(
                    $"{recSig} override: preserved subrecord '{sig}' may carry FormIDs not yet remappable")
        End Select
    End Sub

    ''' <summary>Emit a DAMA (Damage Type Array) preserved subrecord with FormIDs remapped. Entry stride is
    ''' 8 (Type+Amount) or 12 (Type+Amount+Curve Table, FromVersion 152). Both Type @0 and Curve Table @8
    ''' (when present) are FormIDs. Throws if the payload length divides cleanly by neither stride.</summary>
    Private Sub EmitDamageTypeArray(bw As BinaryWriter, sig As String, data As Byte(), mapLocal As Func(Of UInteger, UInteger), recSig As String)
        Dim stride As Integer
        If data.Length = 0 Then
            stride = 0
        ElseIf data.Length Mod 12 = 0 AndAlso data.Length Mod 8 <> 0 Then
            stride = 12
        ElseIf data.Length Mod 8 = 0 AndAlso data.Length Mod 12 <> 0 Then
            stride = 8
        ElseIf data.Length Mod 12 = 0 AndAlso data.Length Mod 8 = 0 Then
            ' Ambiguous (e.g. 24 bytes = 3×8 or 2×12). xEdit determines stride by record Version (152+ →
            ' Curve Table present). We can't read Version here without threading it, and a wrong guess
            ' corrupts FormIDs — so THROW and let the caller pass a record that disambiguates.
            Throw New NotSupportedException(
                $"{recSig} override: preserved DAMA payload length {data.Length} is ambiguous between 8- and 12-byte strides; cannot remap without record Version.")
        Else
            Throw New NotSupportedException(
                $"{recSig} override: preserved DAMA payload length {data.Length} fits neither the 8- nor 12-byte Damage Type stride.")
        End If
        Dim buf(data.Length - 1) As Byte
        If data.Length > 0 Then Buffer.BlockCopy(data, 0, buf, 0, data.Length)
        If stride > 0 Then
            Dim n = data.Length \ stride
            For i = 0 To n - 1
                Dim baseOff = i * stride
                PatchFormIdAt(buf, baseOff, mapLocal(BitConverter.ToUInt32(data, baseOff)))           ' Type [DMGT]
                If stride = 12 Then
                    PatchFormIdAt(buf, baseOff + 8, mapLocal(BitConverter.ToUInt32(data, baseOff + 8))) ' Curve Table [CURV,NULL]
                End If
            Next
        End If
        WriteSubrecordHeader(bw, sig, buf.Length)
        If buf.Length > 0 Then bw.Write(buf)
    End Sub

    ''' <summary>Serialize an ARMO OVERRIDE: canonical xEdit order (wbDefinitionsFO4.pas:6151), OWNED
    ''' subrecords from the entry, PRESERVED subrecords copied from the source with FormIDs remapped. After
    ''' the walk, asserts no preserved source subrecord was dropped (fail loud if the template is missing
    ''' a case).</summary>
    Private Function SerializeArmoRecordOverride(entry As ArmoRecordEntry, remapper As NpcSubrecordWriter.FormIdRemapper, game As Config_App.Game_Enum, pluginManager As PluginManager) As Byte()
        Dim src = entry.SourceRecord
        If src Is Nothing Then Throw New ArgumentException("ARMO override requires SourceRecord.", NameOf(entry))
        If game = Config_App.Game_Enum.Skyrim Then Return SerializeArmoRecordOverrideSkyrim(entry, remapper, pluginManager)
        Dim mapLocal = MakeLocalRemap(src, remapper, pluginManager)

        ' Owned signatures (emitted from the entry, NOT copied from source). FULL is owned only BEFORE the
        ' Object Template block (inside OBTE..STOP a FULL is a combination name → preserved).
        Static armoOwned As New HashSet(Of String)(
            {"EDID", "OBND", "PTRN", "FULL", "EITM", "MOD2", "MO2S", "MOD4", "MO4S", "BOD2",
             "YNAM", "ZNAM", "ETYP", "BAMT", "RNAM", "KSIZ", "KWDA", "DESC", "INRD",
             "INDX", "MODL", "DATA", "FNAM", "DAMA", "TNAM", "APPR"}, StringComparer.Ordinal)

        ' Split source subrecords into three groups, each emitted preserving source order:
        '   • templateBlock  — everything from the FIRST OBTE onward (Object Template, OBTE..STOP).
        '   • destructionBlock — the contiguous Destruction region (first DEST through every consecutive
        '     destruction-family sub). Emitted as ONE block at the BOD2→RNAM position so multi-stage order
        '     (repeated DSTD/DMDL/...) is preserved — per-signature draining would scramble it.
        '   • mainSubs       — the rest (per-signature steps in the canonical walk).
        Dim mainSubs As New List(Of SubrecordData)
        Dim templateBlock As New List(Of SubrecordData)
        Dim destructionBlock As New List(Of SubrecordData)
        Dim inTemplate As Boolean = False
        Dim inDestruction As Boolean = False
        Dim destructionSeen As Boolean = False   ' once the contiguous region ends, a later DEST is malformed
        For Each sr In src.Subrecords
            If sr.Signature = "OBTE" Then inTemplate = True
            If inTemplate Then
                templateBlock.Add(sr)
                Continue For
            End If
            If Not inDestruction AndAlso sr.Signature = "DEST" Then
                If destructionSeen Then
                    Throw New NotSupportedException(
                        "ARMO override: a second non-contiguous DEST region is not supported (Destruction must be one contiguous block).")
                End If
                inDestruction = True
                destructionSeen = True
            End If
            If inDestruction Then
                If _destructionFamilySigs.Contains(sr.Signature) Then
                    destructionBlock.Add(sr)
                    Continue For
                Else
                    inDestruction = False   ' region ended; fall through to main handling for this sub
                End If
            End If
            mainSubs.Add(sr)
        Next

        ' MODC (world-model color remap) shares ONE signature across the male (wbDefinitionsFO4.pas:6165) and
        ' female (:6171) world-model structs, so a per-signature preserved queue can't tell them apart. Split by
        ' SOURCE position: a MODC that appears AFTER MOD4 belongs to the female struct, otherwise the male one.
        ' The single MODC is then routed to the correct struct's callback (afterMod2 vs afterMod4) below — before,
        ' a lone FEMALE MODC was always emitted in the MALE struct (wrong position → its color remap read back as
        ' the male model's). Two MODC (both male+female) can't be split by one queue drain — refuse rather than
        ' mis-order (dual world-model color remap essentially never occurs on real armor).
        Dim modcCount = mainSubs.Where(Function(sr) sr.Signature = "MODC").Count()
        If modcCount > 1 Then
            Throw New NotSupportedException("ARMO override: multiple MODC (world-model color remap) subrecords not supported")
        End If
        Dim mod4Index = mainSubs.FindIndex(Function(sr) sr.Signature = "MOD4")
        Dim modcIndex = mainSubs.FindIndex(Function(sr) sr.Signature = "MODC")
        Dim modcIsFemale As Boolean = (modcIndex >= 0 AndAlso mod4Index >= 0 AndAlso modcIndex > mod4Index)

        ' Index preserved main subrecords (NON-owned) by signature, preserving source order. Owned sigs are
        ' NOT indexed (they come from the entry). Track consumption to assert nothing is dropped. The
        ' destruction block is counted separately (emitted as a contiguous unit, not via preservedBySig).
        Dim preservedBySig As New Dictionary(Of String, Queue(Of SubrecordData))(StringComparer.Ordinal)
        Dim preservedTotal As Integer = 0
        For Each sr In mainSubs
            If armoOwned.Contains(sr.Signature) Then Continue For   ' owned → from entry
            Dim q As Queue(Of SubrecordData) = Nothing
            If Not preservedBySig.TryGetValue(sr.Signature, q) Then
                q = New Queue(Of SubrecordData)()
                preservedBySig(sr.Signature) = q
            End If
            q.Enqueue(sr)
            preservedTotal += 1
        Next
        Dim consumed As Integer = 0

        Dim body As Byte()
        Using bms As New MemoryStream()
            Using bw As New BinaryWriter(bms)
                ' Canonical template walk. Owned → shared emit helper; Preserved → all source subs of that
                ' signature in source order (FormIDs remapped). EmitPreservedStep returns count consumed.
                EmitArmoEdid(bw, entry)                                                  ' EDID  [owned]
                consumed += EmitPreservedStep(bw, "VMAD", "ARMO", src, remapper, pluginManager, mapLocal, preservedBySig)  ' VMAD  [pres]
                EmitArmoObnd(bw, entry)                                                  ' OBND  [owned] (was preserved — now editable)
                EmitArmoPtrn(bw, entry, remapper)                                        ' PTRN  [owned] (was preserved)
                EmitArmoFull(bw, entry)                                                  ' FULL  [owned]
                EmitArmoEitm(bw, entry, remapper)                                        ' EITM  [owned] (was preserved) [ENCH]
                consumed += EmitPreservedStep(bw, "EAMT", "ARMO", src, remapper, pluginManager, mapLocal, preservedBySig)  ' EAMT  [pres] u16 amount (verbatim)
                ' Male world model struct: MOD2[owned], MO2T[pres], MODC[pres], MO2S[owned], then ICON/MICO[pres]
                ' AFTER the struct. MO2T/MODC MUST sit between MOD2 and MO2S (strict-order struct) — the old
                ' placement after MO2S corrupted the ordering (same bug as ARMA), so interleave via the callback.
                EmitArmoMaleModel(bw, entry, remapper,
                    afterMod2:=Sub()
                                   consumed += EmitPreservedStep(bw, "MO2T", "ARMO", src, remapper, pluginManager, mapLocal, preservedBySig)
                                   ' MODC here only if it's the MALE color remap (before MOD4); a female MODC is
                                   ' emitted in the female struct's afterMod4 callback instead.
                                   If Not modcIsFemale Then consumed += EmitPreservedStep(bw, "MODC", "ARMO", src, remapper, pluginManager, mapLocal, preservedBySig)
                               End Sub)
                consumed += EmitPreservedStep(bw, "ICON", "ARMO", src, remapper, pluginManager, mapLocal, preservedBySig)
                consumed += EmitPreservedStep(bw, "MICO", "ARMO", src, remapper, pluginManager, mapLocal, preservedBySig)
                ' Female world model struct: MOD4[owned], MO4T[pres], MODC[pres female], MO4S[owned], then
                ' ICO2/MIC2[pres] AFTER. MODC is emitted here only when it's the FEMALE color remap (source
                ' position after MOD4) — matching xEdit's female struct order MOD4→MO4T→MODC→MO4S.
                EmitArmoFemaleModel(bw, entry, remapper,
                    afterMod4:=Sub()
                                   consumed += EmitPreservedStep(bw, "MO4T", "ARMO", src, remapper, pluginManager, mapLocal, preservedBySig)
                                   If modcIsFemale Then consumed += EmitPreservedStep(bw, "MODC", "ARMO", src, remapper, pluginManager, mapLocal, preservedBySig)
                               End Sub)
                consumed += EmitPreservedStep(bw, "ICO2", "ARMO", src, remapper, pluginManager, mapLocal, preservedBySig)
                consumed += EmitPreservedStep(bw, "MIC2", "ARMO", src, remapper, pluginManager, mapLocal, preservedBySig)
                EmitArmoBod2(bw, entry)                                                  ' BOD2  [owned]
                ' Destruction block [pres]: emit the contiguous captured region in EXACT source order so
                ' multi-stage records keep their stage order (DEST, DAMC, then per-stage DSTD/DSTA/DMDL/...).
                ' Each sub routes through EmitPreservedSubrecord so FormIDs (DAMC DMGT, DSTD Explosion/Debris,
                ' DMDS MSWP) are still remapped. Counted toward `consumed` separately from preservedBySig.
                For Each sr In destructionBlock
                    EmitPreservedSubrecord(bw, sr, "ARMO", src, remapper, pluginManager, mapLocal)
                Next
                consumed += destructionBlock.Count
                EmitArmoYnam(bw, entry, remapper)                                        ' YNAM  [owned] (was preserved)
                EmitArmoZnam(bw, entry, remapper)                                        ' ZNAM  [owned] (was preserved)
                EmitArmoEtyp(bw, entry, remapper)                                        ' ETYP  [owned] (was preserved)
                consumed += EmitPreservedStep(bw, "BIDS", "ARMO", src, remapper, pluginManager, mapLocal, preservedBySig)  ' BIDS [pres]
                EmitArmoBamt(bw, entry, remapper)                                        ' BAMT  [owned] (was preserved)
                EmitArmoRnam(bw, entry, remapper)                                        ' RNAM  [owned]
                EmitArmoKeywords(bw, entry, remapper)                                    ' KSIZ + KWDA [owned]
                ' DESC [owned]. FO4's DESC is optional, so the emitter drops an empty one — but on a LOCALIZED
                ' master (Fallout4.esm) DESC holds a 4-byte string ID that resolves to "" for most armor, and
                ' dropping it rewrites a record the user never edited. Mirror the SOURCE's DESC presence so an
                ' untouched override round-trips byte-exact; a brand-new ARMO still omits an empty DESC.
                EmitArmoDesc(bw, entry)
                EmitArmoInrd(bw, entry, remapper)                                        ' INRD  [owned] (was preserved)
                EmitArmoModels(bw, entry, remapper, game)                                ' INDX + MODL [owned]
                EmitArmoData(bw, entry, game)                                            ' DATA  [owned]
                EmitArmoFnam(bw, entry)                                                  ' FNAM  [owned]
                EmitArmoDama(bw, entry, remapper)                                        ' DAMA  [owned] (was preserved)
                EmitArmoTnam(bw, entry, remapper)                                        ' TNAM  [owned]
                EmitArmoAppr(bw, entry, remapper)                                        ' APPR  [owned]
                ' Object Template block. Two modes at this SAME stream position:
                '   • AUTHORED (entry.CombinationsAuthored, Phase 4): the user edited the Object Template, so
                '     emit the whole OBTE/OBTF/FULL/OBTS/STOP block FROM THE MODEL. The captured source
                '     templateBlock is deliberately NOT emitted (it was never indexed into preservedBySig, so
                '     skipping it does not affect the drop-nothing assertion). FormIDs go through `remapper`
                '     (the GLOBAL remapper), matching the NEW-record path — EmitArmoObjectTemplate's OBTS
                '     payloads carry global FormIDs from the edited model, not source-local wire values.
                '   • VERBATIM (default): emit the captured source block preserving structure, with OBTS
                '     FormIDs remapped through the source-local map. Byte-exact with prior behavior.
                If entry.CombinationsAuthored Then
                    NpcSubrecordWriter.EmitArmoObjectTemplate(bw, entry.Combinations, remapper)
                Else
                    For Each sr In templateBlock
                        If sr.Signature = "OBTS" Then
                            Dim obtsData = If(sr.Data, Array.Empty(Of Byte)())
                            WriteSubrecordHeader(bw, "OBTS", obtsData.Length)
                            bw.Write(RemapObtsPayload(obtsData, mapLocal))
                        Else
                            ' OBTE/OBTF/FULL(combo)/STOP — no FormIDs, copy verbatim.
                            Dim d = If(sr.Data, Array.Empty(Of Byte)())
                            WriteSubrecordHeader(bw, sr.Signature, d.Length)
                            If d.Length > 0 Then bw.Write(d)
                        End If
                    Next
                End If
            End Using
            body = bms.ToArray()
        End Using

        ' Assert no preserved subrecord was dropped (template missing a case → fail loud). `consumed` counts
        ' both the per-signature main steps and the contiguous destruction block; the expected total is the
        ' indexed main preserved subs (preservedTotal) plus the destruction block size.
        Dim expectedConsumed = preservedTotal + destructionBlock.Count
        If consumed <> expectedConsumed Then
            Dim leftover = preservedBySig.Where(Function(kv) kv.Value.Count > 0).Select(Function(kv) $"{kv.Key}×{kv.Value.Count}")
            Throw New NotSupportedException(
                $"ARMO override: {expectedConsumed - consumed} preserved subrecord(s) not emitted by the canonical template (would be dropped): {String.Join(", ", leftover)}. Add a template step.")
        End If

        ' Header: preserve the source's UNMODELED flag bits (COMPRESSED stripped), but apply the ONE modeled
        ' flag — Non-Playable (bit 2) — from the entry boolean. For an UNCHANGED override the boolean was captured
        ' from the same source flag, so the result is byte-identical; toggling the checkbox actually takes effect.
        Const ARMO_MODELED_FLAGS As UInteger = (1UI << 2)
        Dim flags = (src.Header.Flags And Not FLAG_COMPRESSED And Not ARMO_MODELED_FLAGS) Or ComputeArmoHeaderFlags(entry)
        Return WrapRecord("ARMO", body, flags, remapper(entry.FormID), entry.OriginalVcs1, entry.OriginalVcs2, game, src.Header.Version)
    End Function

    ''' <summary>Serialize a SKYRIM ARMO OVERRIDE. Canonical order per wbDefinitionsTES5.pas:4365. Differences
    ''' from FO4: no Object Template (OBTE/OBTS), no PTRN/INRD/FNAM/DAMA/APPR; MO2S/MO4S are Alternate-Textures
    ''' arrays (preserved, not MSWP FormIDs); DATA is 8 bytes (Value+Weight, no Health); armor rating is a
    ''' separate s32 DNAM (not FNAM); Armature is plain MODL FormIDs (no INDX); BMCT (ragdoll template) is a
    ''' preserved string. OWNED come from the entry, PRESERVED copied from source (FormIDs remapped). Asserts
    ''' nothing dropped.</summary>
    Private Function SerializeArmoRecordOverrideSkyrim(entry As ArmoRecordEntry, remapper As NpcSubrecordWriter.FormIdRemapper, pluginManager As PluginManager) As Byte()
        Const game As Config_App.Game_Enum = Config_App.Game_Enum.Skyrim
        Dim src = entry.SourceRecord
        Dim mapLocal = MakeLocalRemap(src, remapper, pluginManager)

        ' Owned = re-emitted from the entry model. BOD2/BODT is owned but re-emitted from the SOURCE subrecord.
        Static armoOwnedSse As New HashSet(Of String)(
            {"EDID", "OBND", "FULL", "EITM", "MOD2", "MOD4", "BOD2", "BODT",
             "YNAM", "ZNAM", "ETYP", "BAMT", "RNAM", "KSIZ", "KWDA", "DESC",
             "MODL", "DATA", "DNAM", "TNAM"}, StringComparer.Ordinal)

        ' Split the Destruction region (DEST + family) into one contiguous block, emitted at the BOD2→YNAM
        ' position so multi-stage order is preserved (same technique as the FO4 path). Everything else is a
        ' per-signature preserved step.
        Dim mainSubs As New List(Of SubrecordData)
        Dim destructionBlock As New List(Of SubrecordData)
        Dim inDestruction As Boolean = False
        Dim destructionSeen As Boolean = False
        For Each sr In src.Subrecords
            If Not inDestruction AndAlso sr.Signature = "DEST" Then
                If destructionSeen Then Throw New NotSupportedException(
                    "ARMO (SSE) override: a second non-contiguous DEST region is not supported.")
                inDestruction = True : destructionSeen = True
            End If
            If inDestruction Then
                If _destructionFamilySigs.Contains(sr.Signature) Then
                    destructionBlock.Add(sr) : Continue For
                Else
                    inDestruction = False
                End If
            End If
            mainSubs.Add(sr)
        Next

        Dim preservedBySig As New Dictionary(Of String, Queue(Of SubrecordData))(StringComparer.Ordinal)
        Dim preservedTotal As Integer = 0
        For Each sr In mainSubs
            If armoOwnedSse.Contains(sr.Signature) Then Continue For
            Dim q As Queue(Of SubrecordData) = Nothing
            If Not preservedBySig.TryGetValue(sr.Signature, q) Then
                q = New Queue(Of SubrecordData)() : preservedBySig(sr.Signature) = q
            End If
            q.Enqueue(sr) : preservedTotal += 1
        Next
        Dim consumed As Integer = 0

        Dim srcBod = src.GetSubrecord("BOD2")
        If Not srcBod.HasValue Then srcBod = src.GetSubrecord("BODT")

        Dim body As Byte()
        Using bms As New MemoryStream()
            Using bw As New BinaryWriter(bms)
                EmitArmoEdid(bw, entry)                                                                    ' EDID :4372
                consumed += EmitPreservedStep(bw, "VMAD", "ARMO", src, remapper, pluginManager, mapLocal, preservedBySig)  ' VMAD :4373
                EmitArmoObnd(bw, entry)                                                                    ' OBND :4374
                EmitArmoFull(bw, entry)                                                                    ' FULL :4375
                EmitArmoEitm(bw, entry, remapper)                                                          ' EITM :4376 (wbEnchantment)
                consumed += EmitPreservedStep(bw, "EAMT", "ARMO", src, remapper, pluginManager, mapLocal, preservedBySig)  ' EAMT (never in SSE ARMO, safety)
                ' Male 'World Model' struct :4377-4382 — MOD2[owned], MO2T[pres], MO2S[pres alt-tex], ICON[pres], MICO[pres].
                If Not String.IsNullOrEmpty(entry.MaleWorldModelPath) Then WriteZString(bw, "MOD2", entry.MaleWorldModelPath)
                consumed += EmitPreservedStep(bw, "MO2T", "ARMO", src, remapper, pluginManager, mapLocal, preservedBySig)
                consumed += EmitPreservedStep(bw, "MO2S", "ARMO", src, remapper, pluginManager, mapLocal, preservedBySig)
                consumed += EmitPreservedStep(bw, "ICON", "ARMO", src, remapper, pluginManager, mapLocal, preservedBySig)
                consumed += EmitPreservedStep(bw, "MICO", "ARMO", src, remapper, pluginManager, mapLocal, preservedBySig)
                ' Female 'World Model' struct :4383-4388 — MOD4[owned], MO4T[pres], MO4S[pres alt-tex], ICO2[pres], MIC2[pres].
                If Not String.IsNullOrEmpty(entry.FemaleWorldModelPath) Then WriteZString(bw, "MOD4", entry.FemaleWorldModelPath)
                consumed += EmitPreservedStep(bw, "MO4T", "ARMO", src, remapper, pluginManager, mapLocal, preservedBySig)
                consumed += EmitPreservedStep(bw, "MO4S", "ARMO", src, remapper, pluginManager, mapLocal, preservedBySig)
                consumed += EmitPreservedStep(bw, "ICO2", "ARMO", src, remapper, pluginManager, mapLocal, preservedBySig)
                consumed += EmitPreservedStep(bw, "MIC2", "ARMO", src, remapper, pluginManager, mapLocal, preservedBySig)
                EmitArmaBod2ForArmo(bw, entry, srcBod)                                                     ' BODT/BOD2 :4389
                For Each sr In destructionBlock                                                            ' DEST :4390
                    EmitPreservedSubrecord(bw, sr, "ARMO", src, remapper, pluginManager, mapLocal)
                Next
                consumed += destructionBlock.Count
                EmitArmoYnam(bw, entry, remapper)                                                          ' YNAM :4391
                EmitArmoZnam(bw, entry, remapper)                                                          ' ZNAM :4392
                consumed += EmitPreservedStep(bw, "BMCT", "ARMO", src, remapper, pluginManager, mapLocal, preservedBySig)  ' BMCT :4393 (string)
                EmitArmoEtyp(bw, entry, remapper)                                                          ' ETYP :4394
                consumed += EmitPreservedStep(bw, "BIDS", "ARMO", src, remapper, pluginManager, mapLocal, preservedBySig)  ' BIDS :4395 [IPDS]
                EmitArmoBamt(bw, entry, remapper)                                                          ' BAMT :4396 [MATT]
                EmitArmoRnam(bw, entry, remapper)                                                          ' RNAM :4397 [RACE]
                EmitArmoKeywords(bw, entry, remapper)                                                      ' KSIZ+KWDA :4398
                ' DESC :4399 — schema-required, but a handful of vanilla creature-skin ARMOs (SkinDraugr,
                ' SkinSabrecat, …) ship WITHOUT it. Mirror the SOURCE's DESC presence so those round-trip exactly:
                ' emit when the source had a DESC (even one whose localized text resolves to empty), omit otherwise.
                EmitArmoDesc(bw, entry)                                                    ' DESC :4399
                EmitArmoModels(bw, entry, remapper, game)                                                  ' Armature MODL :4400 (no INDX)
                EmitArmoData(bw, entry, game)                                                              ' DATA :4401 (8 bytes)
                EmitArmoDnamSkyrim(bw, entry)                                                              ' DNAM :4405 (s32 armor rating)
                EmitArmoTnam(bw, entry, remapper)                                                          ' TNAM :4406 [ARMO]
            End Using
            body = bms.ToArray()
        End Using

        Dim expectedConsumed = preservedTotal + destructionBlock.Count
        If consumed <> expectedConsumed Then
            Dim leftover = preservedBySig.Where(Function(kv) kv.Value.Count > 0).Select(Function(kv) $"{kv.Key}×{kv.Value.Count}")
            Throw New NotSupportedException(
                $"ARMO (SSE) override: {expectedConsumed - consumed} preserved subrecord(s) not emitted (would be dropped): {String.Join(", ", leftover)}. Add a template step.")
        End If

        ' Header: preserve source flags (COMPRESSED stripped), apply modeled Non-Playable (bit 2, shared with FO4).
        Const ARMO_MODELED_FLAGS As UInteger = (1UI << 2)
        Dim flags = (src.Header.Flags And Not FLAG_COMPRESSED And Not ARMO_MODELED_FLAGS) Or ComputeArmoHeaderFlags(entry)
        Return WrapRecord("ARMO", body, flags, remapper(entry.FormID), entry.OriginalVcs1, entry.OriginalVcs2, game, src.Header.Version)
    End Function

    ''' <summary>Emit an ARMO's BODT/BOD2 from the source subrecord under Skyrim (same rule as ARMA:
    ''' preserve signature+size, patch only the First-Person Flags u32 = slot mask). Thin adapter so the ARMO
    ''' path can reuse the ARMA BOD2 logic without an ArmaRecordEntry.</summary>
    Private Sub EmitArmaBod2ForArmo(bw As BinaryWriter, entry As ArmoRecordEntry, srcBod As SubrecordData?)
        If srcBod.HasValue AndAlso srcBod.Value.Data IsNot Nothing AndAlso srcBod.Value.Data.Length >= 4 Then
            Dim s = srcBod.Value
            Dim buf(s.Data.Length - 1) As Byte
            Buffer.BlockCopy(s.Data, 0, buf, 0, s.Data.Length)
            PatchFormIdAt(buf, 0, entry.SlotMask)   ' First-Person Flags u32 (LE) = slot mask
            WriteSubrecordHeader(bw, s.Signature, buf.Length)
            bw.Write(buf)
        Else
            ' New/absent source — BOD2 8 bytes (First-Person Flags + Armor Type). Armor Type 0 = Light Armor
            ' (the entry doesn't model Armor Type yet); "General Flags" in the union is a zero-width overlay alias.
            WriteSubrecordHeader(bw, "BOD2", 8)
            bw.Write(entry.SlotMask)
            bw.Write(0UI)
        End If
    End Sub

    ''' <summary>Serialize a NEW SKYRIM ARMO (wbDefinitionsTES5.pas:4365). Uses the shared emitters; no source
    ''' to preserve, so no Alternate-Textures / DEST / VMAD. Header flags = Non-Playable bit only.</summary>
    Private Function SerializeArmoRecordNewSkyrim(entry As ArmoRecordEntry, remapper As NpcSubrecordWriter.FormIdRemapper) As Byte()
        Const game As Config_App.Game_Enum = Config_App.Game_Enum.Skyrim
        Dim body As Byte()
        Using bms As New MemoryStream()
            Using bw As New BinaryWriter(bms)
                EmitArmoEdid(bw, entry)                                          ' EDID
                EmitArmoObnd(bw, entry)                                          ' OBND
                EmitArmoFull(bw, entry)                                          ' FULL
                EmitArmoEitm(bw, entry, remapper)                               ' EITM
                If Not String.IsNullOrEmpty(entry.MaleWorldModelPath) Then WriteZString(bw, "MOD2", entry.MaleWorldModelPath)
                If Not String.IsNullOrEmpty(entry.FemaleWorldModelPath) Then WriteZString(bw, "MOD4", entry.FemaleWorldModelPath)
                EmitArmaBod2ForArmo(bw, entry, Nothing)                          ' BOD2 (8 bytes)
                EmitArmoYnam(bw, entry, remapper)                               ' YNAM
                EmitArmoZnam(bw, entry, remapper)                               ' ZNAM
                EmitArmoEtyp(bw, entry, remapper)                               ' ETYP
                EmitArmoBamt(bw, entry, remapper)                               ' BAMT
                EmitArmoRnam(bw, entry, remapper)                               ' RNAM
                EmitArmoKeywords(bw, entry, remapper)                           ' KSIZ+KWDA
                EmitArmoDesc(bw, entry, required:=True)                         ' DESC (required)
                EmitArmoModels(bw, entry, remapper, game)                       ' Armature MODL (no INDX)
                EmitArmoData(bw, entry, game)                                   ' DATA (8 bytes)
                EmitArmoDnamSkyrim(bw, entry)                                   ' DNAM (armor rating)
                EmitArmoTnam(bw, entry, remapper)                              ' TNAM
            End Using
            body = bms.ToArray()
        End Using
        Return WrapRecord("ARMO", body, ComputeArmoHeaderFlags(entry), remapper(entry.FormID), entry.OriginalVcs1, entry.OriginalVcs2, game)
    End Function

    ''' <summary>Serialize an ARMA OVERRIDE: canonical xEdit order (wbDefinitionsFO4.pas:6210), OWNED from
    ''' the entry, PRESERVED (ONAM only) copied with FormIDs remapped. Asserts nothing dropped.</summary>
    Private Function SerializeArmaRecordOverride(entry As ArmaRecordEntry, remapper As NpcSubrecordWriter.FormIdRemapper, game As Config_App.Game_Enum, pluginManager As PluginManager) As Byte()
        Dim src = entry.SourceRecord
        If src Is Nothing Then Throw New ArgumentException("ARMA override requires SourceRecord.", NameOf(entry))
        If game = Config_App.Game_Enum.Skyrim Then Return SerializeArmaRecordOverrideSkyrim(entry, remapper, pluginManager)
        Dim mapLocal = MakeLocalRemap(src, remapper, pluginManager)

        ' Owned signatures = those the ArmaRecordEntry actually models. The entry does NOT model the
        ' texture-set hashes (MO2T/MO3T/MO4T/MO5T) nor the 1st-person color-remap members (MO4C/MO5C), so those
        ' stay PRESERVED — dropping them would corrupt the record. SNDD (FootstepSetFormID), ONAM (ArtObjectFormID),
        ' and MO4S/MO5S (1st-person material swaps) are now OWNED (wbDefinitionsFO4.pas:6242-6243/6251-6252).
        Static armaOwned As New HashSet(Of String)(
            {"EDID", "BOD2", "RNAM", "DNAM",
             "MOD2", "MO2C", "MO2S", "MO2F", "MOD3", "MO3C", "MO3S", "MO3F",
             "MOD4", "MO4S", "MO4F", "MOD5", "MO5S", "MO5F",
             "NAM0", "NAM1", "NAM2", "NAM3", "MODL", "SNDD", "ONAM", "BSMP", "BSMB", "BSMS"}, StringComparer.Ordinal)

        Dim preservedBySig As New Dictionary(Of String, Queue(Of SubrecordData))(StringComparer.Ordinal)
        Dim preservedTotal As Integer = 0
        For Each sr In src.Subrecords
            If armaOwned.Contains(sr.Signature) Then Continue For
            Dim q As Queue(Of SubrecordData) = Nothing
            If Not preservedBySig.TryGetValue(sr.Signature, q) Then
                q = New Queue(Of SubrecordData)()
                preservedBySig(sr.Signature) = q
            End If
            q.Enqueue(sr)
            preservedTotal += 1
        Next
        Dim consumed As Integer = 0

        Dim body As Byte()
        Using bms As New MemoryStream()
            Using bw As New BinaryWriter(bms)
                EmitArmaEdid(bw, entry)                          ' EDID  [owned]
                EmitArmaBod2(bw, entry, game)                    ' BOD2  [owned]
                EmitArmaRnam(bw, entry, remapper)                ' RNAM  [owned]
                ' DNAM [owned] — preserve the source's 'Unknown' bytes [4],[5],[7] (not modelled by the
                ' entry; vanilla carries non-zero e.g. 02 00 / 17) so the override round-trips faithfully.
                Dim srcDnamSr = src.GetSubrecord("DNAM")
                EmitArmaDnam(bw, entry, If(srcDnamSr.HasValue, srcDnamSr.Value.Data, Nothing))
                ' Biped Model [owned MOD2/MO2C/MO2S/MO2F + MOD3/MO3C/MO3S/MO3F] with preserved texture-set hashes
                ' (MO2T/MO3T). These MUST be emitted INSIDE each wbTexturedModel struct (right after MOD2/MOD3), NOT
                ' as a separate group after both models: xEdit's ARMA model struct is strict-order, and the old
                ' "adjacent group" placement made xEdit flag MO2T (and every subrecord after it) as out-of-order,
                ' silently dropping the whole tail (MOD4/MOD5/MODL/SNDD/BSMx) on read. Interleave via callbacks.
                EmitArmaBipedModel(bw, entry, remapper, game,
                    afterMod2:=Sub() consumed += EmitPreservedStep(bw, "MO2T", "ARMA", src, remapper, pluginManager, mapLocal, preservedBySig),
                    afterMod3:=Sub() consumed += EmitPreservedStep(bw, "MO3T", "ARMA", src, remapper, pluginManager, mapLocal, preservedBySig))
                ' 1st Person [owned MOD4/MO4S/MO4F + MOD5/MO5S/MO5F] with preserved members the entry doesn't model:
                ' MO4T/MO5T (texture hashes), MO4C/MO5C (color-remap floats) — emitted INSIDE each struct after
                ' MOD4/MOD5 (same strict-order rule). MO4S/MO5S are OWNED (emitted from the entry).
                EmitArmaFirstPersonModel(bw, entry, remapper, game,
                    afterMod4:=Sub()
                                   consumed += EmitPreservedStep(bw, "MO4T", "ARMA", src, remapper, pluginManager, mapLocal, preservedBySig)
                                   consumed += EmitPreservedStep(bw, "MO4C", "ARMA", src, remapper, pluginManager, mapLocal, preservedBySig)
                               End Sub,
                    afterMod5:=Sub()
                                   consumed += EmitPreservedStep(bw, "MO5T", "ARMA", src, remapper, pluginManager, mapLocal, preservedBySig)
                                   consumed += EmitPreservedStep(bw, "MO5C", "ARMA", src, remapper, pluginManager, mapLocal, preservedBySig)
                               End Sub)
                EmitArmaSkinTextures(bw, entry, remapper)        ' NAM0..NAM3  [owned]
                EmitArmaAdditionalRaces(bw, entry, remapper)     ' MODL Additional Races [owned]
                EmitArmaSndd(bw, entry, remapper)                ' SNDD  [owned] (was preserved)
                EmitArmaOnam(bw, entry, remapper)                ' ONAM  [owned] (was preserved)
                EmitArmaBoneScale(bw, entry)                     ' BSMP/BSMB/BSMS [owned]
            End Using
            body = bms.ToArray()
        End Using

        If consumed <> preservedTotal Then
            Dim leftover = preservedBySig.Where(Function(kv) kv.Value.Count > 0).Select(Function(kv) $"{kv.Key}×{kv.Value.Count}")
            Throw New NotSupportedException(
                $"ARMA override: {preservedTotal - consumed} preserved subrecord(s) not emitted by the canonical template (would be dropped): {String.Join(", ", leftover)}. Add a template step.")
        End If

        ' Header: preserve the source's UNMODELED flag bits (COMPRESSED stripped), but apply the THREE editable
        ' flags the entry models — No Underarmor Scaling(6) / Has Sculpt Data(9) / Hi-Res 1st Person Only(30) —
        ' from the booleans. For an UNCHANGED override these booleans were captured from the same source flags, so
        ' the result is byte-identical; when the user toggles a checkbox (or adds sculpt data → HasSculptData) it
        ' actually takes effect instead of being silently dropped. Only these 3 bits are owned; every other source
        ' flag is preserved verbatim. (Previously the whole source flag word was kept, making those checkboxes inert
        ' on override — e.g. adding sculpt to a source without it wrote the BSMB/BSMS but never set bit 9.)
        Const ARMA_MODELED_FLAGS As UInteger = (1UI << 6) Or (1UI << 9) Or (1UI << 30)
        Dim flags = (src.Header.Flags And Not FLAG_COMPRESSED And Not ARMA_MODELED_FLAGS) Or ComputeArmaHeaderFlags(entry)
        Return WrapRecord("ARMA", body, flags, remapper(entry.FormID), entry.OriginalVcs1, entry.OriginalVcs2, game, src.Header.Version)
    End Function

    ''' <summary>Serialize a SKYRIM ARMA OVERRIDE. Canonical order per wbDefinitionsTES5.pas:4409. Differences
    ''' from FO4: BOD2/BODT is preserved verbatim (patched slot mask only, wbBODTBOD2 :2651); MO2S/MO3S/MO4S/MO5S
    ''' are Alternate-Textures arrays (wbDefinitionsTES5.pas:3325-3328) NOT modelled by the entry → PRESERVED
    ''' inside their textured-model struct; no MO2C/MO2F/MO3C/… (FO4-only) and no BSMP/BSMB/BSMS bone-scale
    ''' (FO4-only). Header has no named flags → preserved verbatim (COMPRESSED stripped). Asserts nothing dropped.</summary>
    Private Function SerializeArmaRecordOverrideSkyrim(entry As ArmaRecordEntry, remapper As NpcSubrecordWriter.FormIdRemapper, pluginManager As PluginManager) As Byte()
        Const game As Config_App.Game_Enum = Config_App.Game_Enum.Skyrim
        Dim src = entry.SourceRecord
        Dim mapLocal = MakeLocalRemap(src, remapper, pluginManager)

        ' Owned = the fields the entry re-emits from its model. Everything else is preserved. Under Skyrim the
        ' texture-hash (MO2T/…) AND the alternate-texture (MO2S/…) members are preserved; BOD2/BODT is owned but
        ' re-emitted from the SOURCE subrecord (below). BSMP/BSMB/BSMS/MO2C/MO2F/… never occur in Skyrim ARMA.
        Static armaOwnedSse As New HashSet(Of String)(
            {"EDID", "BOD2", "BODT", "RNAM", "DNAM",
             "MOD2", "MOD3", "MOD4", "MOD5",
             "NAM0", "NAM1", "NAM2", "NAM3", "MODL", "SNDD", "ONAM"}, StringComparer.Ordinal)

        Dim preservedBySig As New Dictionary(Of String, Queue(Of SubrecordData))(StringComparer.Ordinal)
        Dim preservedTotal As Integer = 0
        For Each sr In src.Subrecords
            If armaOwnedSse.Contains(sr.Signature) Then Continue For
            Dim q As Queue(Of SubrecordData) = Nothing
            If Not preservedBySig.TryGetValue(sr.Signature, q) Then
                q = New Queue(Of SubrecordData)()
                preservedBySig(sr.Signature) = q
            End If
            q.Enqueue(sr)
            preservedTotal += 1
        Next
        Dim consumed As Integer = 0

        ' Source BOD2 or legacy BODT — preserved verbatim (patched slot mask) by EmitArmaBod2.
        Dim srcBod = src.GetSubrecord("BOD2")
        If Not srcBod.HasValue Then srcBod = src.GetSubrecord("BODT")

        Dim body As Byte()
        Using bms As New MemoryStream()
            Using bw As New BinaryWriter(bms)
                EmitArmaEdid(bw, entry)                          ' EDID  [owned]
                EmitArmaBod2(bw, entry, game, srcBod)            ' BODT/BOD2 [owned, from source]
                EmitArmaRnam(bw, entry, remapper)                ' RNAM  [owned]
                Dim srcDnamSr = src.GetSubrecord("DNAM")
                EmitArmaDnam(bw, entry, If(srcDnamSr.HasValue, srcDnamSr.Value.Data, Nothing))  ' DNAM [owned]
                ' Biped Model — MO2T + MO2S (both PRESERVED) inside the male struct after MOD2; likewise MO3T + MO3S.
                EmitArmaBipedModel(bw, entry, remapper, game,
                    afterMod2:=Sub()
                                   consumed += EmitPreservedStep(bw, "MO2T", "ARMA", src, remapper, pluginManager, mapLocal, preservedBySig)
                                   consumed += EmitPreservedStep(bw, "MO2S", "ARMA", src, remapper, pluginManager, mapLocal, preservedBySig)
                               End Sub,
                    afterMod3:=Sub()
                                   consumed += EmitPreservedStep(bw, "MO3T", "ARMA", src, remapper, pluginManager, mapLocal, preservedBySig)
                                   consumed += EmitPreservedStep(bw, "MO3S", "ARMA", src, remapper, pluginManager, mapLocal, preservedBySig)
                               End Sub)
                ' 1st Person — MO4T + MO4S inside the male struct after MOD4; MO5T + MO5S for the female.
                EmitArmaFirstPersonModel(bw, entry, remapper, game,
                    afterMod4:=Sub()
                                   consumed += EmitPreservedStep(bw, "MO4T", "ARMA", src, remapper, pluginManager, mapLocal, preservedBySig)
                                   consumed += EmitPreservedStep(bw, "MO4S", "ARMA", src, remapper, pluginManager, mapLocal, preservedBySig)
                               End Sub,
                    afterMod5:=Sub()
                                   consumed += EmitPreservedStep(bw, "MO5T", "ARMA", src, remapper, pluginManager, mapLocal, preservedBySig)
                                   consumed += EmitPreservedStep(bw, "MO5S", "ARMA", src, remapper, pluginManager, mapLocal, preservedBySig)
                               End Sub)
                EmitArmaSkinTextures(bw, entry, remapper)        ' NAM0..NAM3 [owned]
                EmitArmaAdditionalRaces(bw, entry, remapper)     ' MODL Additional Races [owned]
                EmitArmaSndd(bw, entry, remapper)                ' SNDD  [owned]
                EmitArmaOnam(bw, entry, remapper)                ' ONAM  [owned]
            End Using
            body = bms.ToArray()
        End Using

        If consumed <> preservedTotal Then
            Dim leftover = preservedBySig.Where(Function(kv) kv.Value.Count > 0).Select(Function(kv) $"{kv.Key}×{kv.Value.Count}")
            Throw New NotSupportedException(
                $"ARMA (SSE) override: {preservedTotal - consumed} preserved subrecord(s) not emitted (would be dropped): {String.Join(", ", leftover)}. Add a template step.")
        End If

        ' Skyrim ARMA has no named header flags (wbDefinitionsTES5.pas:4409) → preserve source flags verbatim.
        Dim flags = src.Header.Flags And Not FLAG_COMPRESSED
        Return WrapRecord("ARMA", body, flags, remapper(entry.FormID), entry.OriginalVcs1, entry.OriginalVcs2, game, src.Header.Version)
    End Function

    ''' <summary>Emit ALL preserved source subrecords of <paramref name="sig"/> (in source order) for the
    ''' current template step, FormIDs remapped. Returns the count emitted (for the drop-nothing assertion).</summary>
    Private Function EmitPreservedStep(bw As BinaryWriter, sig As String, recSig As String, src As PluginRecord,
                                       remapper As NpcSubrecordWriter.FormIdRemapper, pluginManager As PluginManager,
                                       mapLocal As Func(Of UInteger, UInteger),
                                       preservedBySig As Dictionary(Of String, Queue(Of SubrecordData))) As Integer
        Dim q As Queue(Of SubrecordData) = Nothing
        If Not preservedBySig.TryGetValue(sig, q) Then Return 0
        Dim count As Integer = 0
        While q.Count > 0
            Dim sr = q.Dequeue()
            EmitPreservedSubrecord(bw, sr, recSig, src, remapper, pluginManager, mapLocal)
            count += 1
        End While
        Return count
    End Function

    ''' <summary>True if a FormID is a provisional draft sentinel (high byte 0xFF). Such FormIDs are not
    ''' resolvable to any loaded master (max real index 0xFD, ESL prefix 0xFE) — they resolve only through
    ''' draftRemap. Mirrors the app-side OutfitDraft.IsDraftFormID, kept local so the library has no app dep.</summary>
    Private Function IsProvisionalDraftFormID(formID As UInteger) As Boolean
        Return ((formID >> 24) And &HFFUI) = &HFFUI
    End Function

    Private Function BuildGrup(label As String, recordBuffers As List(Of Byte())) As Byte()
        ' GRUP header (24 bytes): Signature "GRUP" + GroupSize (incl. header) + Label (record-type
        ' signature) as u32 + GroupType (0 = top-level) + Stamp + Unknown.
        If label Is Nothing OrElse label.Length <> 4 Then Throw New ArgumentException($"GRUP label must be 4 chars: '{label}'.", NameOf(label))
        Using ms As New MemoryStream()
            Using bw As New BinaryWriter(ms)
                Dim contentSize = recordBuffers.Sum(Function(b) b.Length)
                Dim totalSize = 24 + contentSize
                bw.Write(Encoding.ASCII.GetBytes("GRUP"))
                bw.Write(CUInt(totalSize))
                bw.Write(Encoding.ASCII.GetBytes(label))     ' Label u32 = record-type signature bytes
                bw.Write(0)                                  ' GroupType = 0 (top-level)
                bw.Write(0UI)                                ' Stamp
                bw.Write(0UI)                                ' Unknown
                For Each b In recordBuffers
                    bw.Write(b)
                Next
            End Using
            Return ms.ToArray()
        End Using
    End Function

    Private Function BuildTes4Header(game As Config_App.Game_Enum,
                                     markAsMaster As Boolean,
                                     lightMaster As Boolean,
                                     masters As List(Of String),
                                     numContentRecords As Integer,
                                     nextObjectId As UInteger,
                                     gameMaster As String,
                                     outputDir As String) As Byte()
        Dim recordVersion As UShort = If(game = Config_App.Game_Enum.Fallout4, TES4_RECORD_VERSION_FO4, TES4_RECORD_VERSION_SSE)
        Dim hedrVersion As Single = If(game = Config_App.Game_Enum.Fallout4, HEDR_VERSION_FO4, HEDR_VERSION_SSE)

        ' HEDR + CNAM + (MAST + DATA)*N
        Using bodyMs As New MemoryStream()
            Using bw As New BinaryWriter(bodyMs)
                ' HEDR (12 bytes)
                WriteSubrecordHeader(bw, "HEDR", 12)
                bw.Write(hedrVersion)
                ' numRecords = content records + top-level GRUPs (TES4 itself excluded). The caller
                ' computes it; see the derivation from xEdit's GetCountedRecordCount at the call site.
                bw.Write(CUInt(numContentRecords))
                ' Next free self object index — must exceed any self-index FormID we assigned to new
                ' records (OTFT outfits start at NEXT_OBJECT_ID_DEFAULT) so the CK won't re-issue one.
                bw.Write(nextObjectId)

                ' CNAM (author, ZSTRING). xEdit treats TES4.CNAM as wbString (translatable).
                ' Literal is ASCII-only but route via central encoder for convention consistency.
                Dim authorBytes = PluginEncodingSettings.EncodeTranslatable(NPC_MANAGER_AUTHOR_CNAM)
                WriteSubrecordHeader(bw, "CNAM", authorBytes.Length + 1)
                bw.Write(authorBytes)
                bw.Write(CByte(0))

                ' SNAM (TES4 Description, ZSTRING). xEdit reads <cp:XXXX> tag from this field
                ' (wbImplementation.pas:5724-5737) to apply per-file Translatable encoding when
                ' opening the plugin — regardless of the destination user's sLanguage. xEdit does
                ' NOT auto-emit the tag (user-managed in their workflow). We DO emit it as a
                ' deliberate UX improvement (zero bug risk): plugins generated by NPC_Manager open
                ' correctly in xEdit on any sLanguage configuration. The tag does NOT help in-game
                ' (game engine ignores it) but it does NOT hurt either — just an extra readable
                ' description string. Format: "Plugin encoding: <cp:XXXX>" — descriptive prefix
                ' for users browsing the plugin in CK/MO2, parseable by xEdit (Pos('<cp:', s)
                ' matches anywhere in the string).
                Dim cpTag = PluginEncodingSettings.GetTranslatableSnamCpTag()
                If cpTag <> "" Then
                    Dim snamText = "Plugin encoding: " & cpTag
                    Dim snamBytes = PluginEncodingSettings.EncodeTranslatable(snamText)
                    WriteSubrecordHeader(bw, "SNAM", snamBytes.Length + 1)
                    bw.Write(snamBytes)
                    bw.Write(CByte(0))
                End If

                ' MAST + DATA pairs. DATA is documented as `wbByteArray('Unknown', 8, cpIgnore)`
                ' in xEdit (wbDefinitionsFO4.pas:12477) with the explicit comment "Should be set
                ' by CK but usually null". The engine ignores the field at runtime — the canonical
                ' CK output is 8 zero bytes, so we match that and skip the file-size lookup.
                For Each masterName In masters
                    ' ⛔ NO Encoding.ASCII: sustituye por '?' en silencio y el lector decodifica con la General.
                    ' Ver PluginEncodingSettings.EncodeMasterFileName — rehúsa en vez de escribir un master roto.
                    Dim masterBytes = PluginEncodingSettings.EncodeMasterFileName(masterName)
                    WriteSubrecordHeader(bw, "MAST", masterBytes.Length + 1)
                    bw.Write(masterBytes)
                    bw.Write(CByte(0))
                    WriteSubrecordHeader(bw, "DATA", 8)
                    bw.Write(0UL)
                Next

                ' INCC (Interior Cell Count, itU32) is .SetRequired per spec
                ' (wbDefinitionsFO4.pas:12488). xEdit's PrepareSave (wbImplementation.pas:5223-5232)
                ' always sets it to the count of CELL records flagged Interior (DATA bit 0 = 1) when
                ' saving FO4 plugins. NPC_Manager auto-gen plugins never contain CELL records, so
                ' INCC is always 0 — but the subrecord must be emitted (engine + CK validators
                ' expect it on FO4 ESPs).
                WriteSubrecordHeader(bw, "INCC", 4)
                bw.Write(0UI)
            End Using
            Dim bodyBytes = bodyMs.ToArray()

            Using ms As New MemoryStream()
                Using bw As New BinaryWriter(ms)
                    bw.Write(Encoding.ASCII.GetBytes("TES4"))
                    bw.Write(CUInt(bodyBytes.Length))
                    Dim flags As UInteger = 0UI
                    If markAsMaster Then flags = flags Or FLAG_ESM
                    If lightMaster Then flags = flags Or FLAG_ESL
                    bw.Write(flags)
                    bw.Write(0UI)               ' FormID always 0 for TES4
                    bw.Write(0UI)               ' VCS1
                    bw.Write(recordVersion)     ' Version
                    bw.Write(0US)               ' VCS2
                    bw.Write(bodyBytes)
                End Using
                Return ms.ToArray()
            End Using
        End Using
    End Function

    ''' <summary>The game's master file name. Public so callers building entries (NpcOverrideSaver's SSE
    ''' hair-colour materialization) can tell "this record lives in the game master, reusing it adds no
    ''' dependency" from "this record would drag a new plugin into the MAST list".</summary>
    Public Function MasterFileNamePublic(game As Config_App.Game_Enum) As String
        Select Case game
            Case Config_App.Game_Enum.Fallout4 : Return "Fallout4.esm"
            Case Config_App.Game_Enum.Skyrim : Return "Skyrim.esm"
            Case Else
                Throw New ArgumentOutOfRangeException(NameOf(game), $"Unsupported game: {game}")
        End Select
    End Function

End Module
