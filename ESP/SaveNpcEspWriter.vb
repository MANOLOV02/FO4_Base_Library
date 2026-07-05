Imports System.IO
Imports System.Linq
Imports System.Text

' ============================================================================
' Save NPC ESP/ESM — emits a Bethesda plugin containing one or more NPC_
' overrides with proper master cleanup (xEdit CleanMasters algorithm).
'
' Algorithm overview:
'   1. For each NPC override the caller wants to save:
'      a. Collect all FormIDs the override references (from the type-safe parse
'         model + VMAD position list).
'      b. Map each global FormID to (sourcePluginName, localObjectID).
'   2. Build the new plugin's MAST list:
'      a. Start with the game master (Fallout4.esm / Skyrim.esm) — always present.
'      b. Add any plugin name that owns at least one referenced FormID.
'      c. Sort by load order (PluginManager is the authority).
'   3. Build the FormIdRemapper closure:
'      For a given global FormID, find its source plugin name + local ObjectID,
'      look up the new MAST index, return (newMastIdx << 24) | localObjectID.
'   4. Serialize each NPC_ record:
'      a. Body via NpcSubrecordWriter.SerializeNpcBody(npc, remapper).
'      b. Wrap in NPC_ record header (24 bytes) with the original (post-remap)
'         FormID. Override semantics: the record FormID is the same as the
'         source NPC's, with master index re-mapped to point at the source via
'         our new MAST list.
'   5. Wrap all NPC_ records in a single GRUP NPC_ top-level group.
'   6. Emit TES4 header with HEDR (numRecords = NPC count + 1 for TES4 itself),
'      CNAM (author), MAST/DATA pairs for each MAST in the new list, then GRUP.
'   7. Atomic write: write to .tmp, fsync, rename to final path.
'
' Update-existing flow:
'   When the caller passes an existing plugin path that already contains other
'   NPC overrides, the old records are preserved verbatim (their PluginRecord
'   subrecords are re-emitted unchanged) but their FormIDs and any embedded
'   FormID references inside them are remapped against the NEW MAST list. This
'   matches xEdit's CleanMasters behavior of recomputing master indices when
'   the master list shifts.
' ============================================================================

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
        Public Substitutions As New List(Of MSWP_Substitution)
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
        Public ArmorRating As UShort = 0US          ' FNAM
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
                                       Optional mswpEntries As List(Of MswpRecordEntry) = Nothing) As SaveResult

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

        Dim gameMaster = MasterFileNamePublic(game)

        ' ====================================================================
        ' Step 1: Collect every FormID that will end up in the final plugin.
        ' ====================================================================
        Dim allFormIDs As New HashSet(Of UInteger)
        For Each entry In entries
            CollectFormIDs(entry.Npc, allFormIDs)
            ' Also include the record's own FormID (for the master ownership reference).
            allFormIDs.Add(entry.Npc.FormID)
        Next
        ' NEW NPC_ create entries: walk the cloned NpcData for FormID references (RNAM/HEAD/etc).
        ' Their own FormID is the provisional sentinel (0xFF high byte) — DO NOT add it: it's not
        ' resolvable to a master, draftRemap handles it. Skip provisional FormIDs in references too
        ' (cross-draft refs go through draftRemap, same pattern as OTFT/LVLI).
        For Each ce In npcCreateEntries
            If ce.NpcData Is Nothing Then Continue For
            CollectFormIDs(ce.NpcData, allFormIDs)
        Next
        For Each rec In existingRecords
            ' rec.Header.FormID is LOCAL when rec comes from a fresh PluginReader (caller's
            ' "update existing" path in NpcOverrideSaver Phase 2 loads with a new reader that
            ' never goes through PluginManager.MergeRecords, which is what mutates the header
            ' to GLOBAL — see PluginManager.vb:320-324). The audit downstream uses
            ' GetOriginatingPluginName(fid) which interprets the high byte as a LOAD ORDER
            ' slot, so passing LOCAL would pull in whichever plugin happens to occupy that
            ' slot (typically a DLC ESM) as a spurious master. Resolve to GLOBAL via the
            ' source plugin's MAST list — same operation MergeRecords does on load, and the
            ' same convention xEdit uses internally (records carry FixedFormID = LoadOrder
            ' FormID; CleanMasters / MastersUpdated / ReportRequiredMasters all operate over
            ' GLOBAL FormIDs — wbImplementation.pas:3024-3120 / 5014 / 13572).
            Dim globalRecFid = pluginManager.ResolveReferencedFormID(rec.SourcePluginName, rec.Header.FormID)
            allFormIDs.Add(globalRecFid)
            CollectFormIDsFromSubrecords(rec, existingMasters, pluginManager, allFormIDs)
        Next
        ' OTFT outfits: the INAM items (ARMO/LVLI) bring in masters; an OVERRIDE entry also brings in
        ' its own record's master. NEW entries are owned by this plugin (no external master).
        For Each oe In outfitEntries
            For Each armoFid In oe.ItemArmoFormIDs
                ' Skip provisional draft FormIDs (0xFF high byte): they resolve through draftRemap, not a
                ' master. Adding them would have GetOriginatingPluginName fail (no master at index 0xFF)
                ' anyway, but skipping keeps the audit clean and the intent explicit.
                If armoFid <> 0UI AndAlso Not IsProvisionalDraftFormID(armoFid) Then allFormIDs.Add(armoFid)
            Next
            If oe.IsOverride AndAlso oe.FormID <> 0UI Then allFormIDs.Add(oe.FormID)
        Next
        ' LVLI leveled lists: each LVLO reference (ARMO or a real/nested LVLI) brings in its master.
        ' Provisional refs to other DRAFT leveled lists resolve via draftRemap (skipped here). An OVERRIDE
        ' entry (existing LVLI being preserved) also brings in its own record's master.
        ' Additional FormID-bearing fields (preserve-existing only; NEW drafts leave them empty):
        '   LVLG (Use Global GLOB), LVSG (Epic Loot Chance GLOB), LLKC (Filter Keyword KYWD),
        '   per-entry COED (Owner NPC_/FACT + extra GLOB if Owner=NPC_).
        ' Each FormID that ends up in the file MUST appear here so the master discovery walks include it
        ' (mirror of xEdit ReportRequiredMasters, wbImplementation.pas:13572).
        For Each le In leveledEntries
            For Each ent In le.Entries
                If ent.RefFormID <> 0UI AndAlso Not IsProvisionalDraftFormID(ent.RefFormID) Then allFormIDs.Add(ent.RefFormID)
                If ent.HasCoed Then
                    If ent.CoedOwnerFormID <> 0UI Then allFormIDs.Add(ent.CoedOwnerFormID)
                    If ent.CoedExtraIsFormID AndAlso ent.CoedOwnerExtra <> 0UI Then allFormIDs.Add(ent.CoedOwnerExtra)
                End If
            Next
            If le.HasUseGlobal AndAlso le.UseGlobalFormID <> 0UI Then allFormIDs.Add(le.UseGlobalFormID)
            If le.HasEpicLootChance AndAlso le.EpicLootChanceFormID <> 0UI Then allFormIDs.Add(le.EpicLootChanceFormID)
            For Each fk In le.FilterKeywords
                If fk.KeywordFormID <> 0UI Then allFormIDs.Add(fk.KeywordFormID)
            Next
            ' LVLN generic model: MODS = Material Swap FormID [MSWP]. The bytes hold the GLOBAL FormID
            ' (resolved at parse), so add it verbatim to the master walk. Other model subrecords are FormID-free.
            For Each m In le.ModelSubrecords
                If m.Signature = "MODS" AndAlso m.Data IsNot Nothing AndAlso m.Data.Length = 4 Then
                    Dim mswp = BitConverter.ToUInt32(m.Data, 0)
                    If mswp <> 0UI AndAlso Not IsProvisionalDraftFormID(mswp) Then allFormIDs.Add(mswp)
                End If
            Next
            If le.IsOverride AndAlso le.FormID <> 0UI Then allFormIDs.Add(le.FormID)
        Next
        ' ARMA / ARMO / MSWP records (NEW-only in this task). Each FormID they reference must enter the
        ' master walk so the defining plugin lands in the MAST list (mirror of LVLI/OTFT above). Provisional
        ' draft FormIDs (0xFF high byte, cross-record refs to sibling drafts) resolve via draftRemap, not a
        ' master, so they are skipped — same convention as the OTFT/LVLI collectors. MSWP has no body FormIDs.
        For Each ae In armaEntries
            CollectFormIDsFromArma(ae, allFormIDs, pluginManager)
        Next
        For Each ao In armoEntries
            CollectFormIDsFromArmo(ao, allFormIDs, pluginManager)
        Next
        For Each mw In mswpEntries
            CollectFormIDsFromMswp(mw, allFormIDs)
        Next

        ' ====================================================================
        ' Step 2: Build the new MAST list.
        ' Mirrors xEdit's TwbMainRecord.ReportRequiredMasters (wbImplementation.pas:13572) +
        ' TwbMainRecord.GetReferenceFile (wbImplementation.pas:12185): for each FormID in the
        ' record we add the file that DEFINES the master (via FormID high byte → MAST list of
        ' source plugin → load order). The override file (where the FormID was last seen) is
        ' NOT what xEdit adds — it adds the file that owns the master record.
        '
        ' GetOriginatingPluginName(fid) replicates this: it indexes the high byte against the
        ' load-order, and our ResolveFormID already mapped local FormIDs to master files via
        ' the source plugin's MAST list. So GetOriginatingPluginName returns the master-defining
        ' plugin, equivalent to xEdit's GetMasterForFileID.
        ' ====================================================================
        Dim referencedPluginNames As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        ' Audit table: which FormID is bringing in which master plugin? Logged so the user can
        ' identify "extra" masters as either legitimate references or as bugs in CollectFormIDs.
        Dim auditPerPlugin As New Dictionary(Of String, List(Of UInteger))(StringComparer.OrdinalIgnoreCase)
        For Each fid In allFormIDs
            Dim pname = pluginManager.GetOriginatingPluginName(fid)
            If String.IsNullOrEmpty(pname) Then Continue For
            If String.Equals(pname, Path.GetFileName(outputPath), StringComparison.OrdinalIgnoreCase) Then Continue For
            referencedPluginNames.Add(pname)
            Dim list As List(Of UInteger) = Nothing
            If Not auditPerPlugin.TryGetValue(pname, list) Then
                list = New List(Of UInteger)
                auditPerPlugin(pname) = list
            End If
            list.Add(fid)
        Next

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

        ' ====================================================================
        ' Step 3: Build FormIdRemapper.
        ' For each global FormID:
        '   - Resolve to source plugin name via PluginManager.
        '   - Look up new MAST index (or -1 if plugin not in new MAST list — error).
        '   - Return (newMastIdx << 24) | (FormID & 0xFFFFFF).
        ' Special cases:
        '   - FormID == 0: emit 0 (NULL reference).
        '   - Source plugin == output plugin name: master idx is the plugin's own
        '     "self FileFileID" which xEdit encodes as len(masters). Auto-generated
        '     plugins don't typically own non-override records, but support it.
        ' ====================================================================
        Dim outputName = Path.GetFileName(outputPath)
        Dim masterIndexLookup As New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)
        For i = 0 To sortedMasters.Count - 1
            masterIndexLookup(sortedMasters(i)) = i
        Next
        ' "Self" master index (records owned by THIS plugin) = sortedMasters.Count.
        Dim selfMasterIdx As Integer = sortedMasters.Count

        ' Assign real self-index FormIDs to NEW OTFT outfits. The caller handed each NEW entry a
        ' PROVISIONAL sentinel (0xFF high byte); the real FormID is (selfMasterIdx << 24) | objIndex,
        ' objIndex starting at NEXT_OBJECT_ID_DEFAULT (0x800) — the FO4/xEdit new-record convention.
        ' draftRemap maps provisional → real so the remapper rewrites BOTH the OTFT record header AND
        ' every reference to it (notably the NPC.DOFT pointing at the provisional FormID). OVERRIDE
        ' entries keep their real FormID and resolve through the normal master-remap path.
        ' Every NEW draft (OTFT outfits AND LVLI leveled lists) is pre-assigned its real self-index
        ' FormID BEFORE serialization, so cross-draft references (OTFT.INAM → draft LVLI, draft LVLI.LVLO
        ' → another draft LVLI, NPC.DOFT → draft OTFT) resolve through the single remapper irrespective of
        ' emit order. OTFTs are numbered first, then LVLIs — any stable order works; the keys (provisional
        ' FormIDs) are globally unique across both kinds because they come from one app-side counter.
        Dim draftRemap As New Dictionary(Of UInteger, UInteger)
        ' Seed the draft object-ID counter from the disk HEDR if it's ahead of the default. On
        ' update-existing, a prior save consumed object IDs 0x800..existingNextObjectId-1 (those
        ' records are in existingRecords/Entries with self-index FormIDs). Starting at 0x800 again
        ' would re-hand the same IDs and collide with already-preserved overrides.
        Dim nextSelfObjIndex As UInteger = If(existingNextObjectId > NEXT_OBJECT_ID_DEFAULT, existingNextObjectId, NEXT_OBJECT_ID_DEFAULT)
        For Each oe In outfitEntries
            If oe.IsOverride Then Continue For
            draftRemap(oe.FormID) = (CUInt(selfMasterIdx) << 24) Or nextSelfObjIndex
            nextSelfObjIndex += 1UI
        Next
        ' NPC_ creates ANTES que leveled: los NPC_ son los records primarios y sus FormIDs deben ser
        ' estables (los bakes en disco se nombran por FormID). Una LVLN/LVLI que los referencia toma su
        ' slot DESPUES, asi agregar/quitar una leveled list no corre los FormIDs de los NPC_. La
        ' resolucion de refs es global (draftRemap completo antes de serializar), asi que el orden de
        ' asignacion no afecta la correctitud — solo que numero recibe cada record.
        For Each ce In npcCreateEntries
            If draftRemap.ContainsKey(ce.ProvisionalFormID) Then Continue For
            draftRemap(ce.ProvisionalFormID) = (CUInt(selfMasterIdx) << 24) Or nextSelfObjIndex
            nextSelfObjIndex += 1UI
        Next
        For Each le In leveledEntries
            ' OVERRIDE LVLIs keep their real FormID (master-remapped on emit) — no self-index. Only NEW
            ' (draft) lists get a self-index. Guard against a duplicate provisional listed twice.
            If le.IsOverride Then Continue For
            If draftRemap.ContainsKey(le.FormID) Then Continue For
            draftRemap(le.FormID) = (CUInt(selfMasterIdx) << 24) Or nextSelfObjIndex
            nextSelfObjIndex += 1UI
        Next
        ' NEW MSWP / ARMA / ARMO drafts: pre-assign each a real self-index FormID so cross-draft refs
        ' resolve through the single remapper irrespective of emit order (ARMA.MO2S → draft MSWP,
        ' ARMO.MODL → draft ARMA, ARMO.MO2S → draft MSWP). OVERRIDE entries keep their real FormID. The
        ' provisional keys are globally unique across all draft kinds (one app-side counter). Order
        ' (MSWP → ARMA → ARMO) is cosmetic — resolution is global once draftRemap is fully built.
        For Each mw In mswpEntries
            If mw.IsOverride Then Continue For
            If draftRemap.ContainsKey(mw.FormID) Then Continue For
            draftRemap(mw.FormID) = (CUInt(selfMasterIdx) << 24) Or nextSelfObjIndex
            nextSelfObjIndex += 1UI
        Next
        For Each ae In armaEntries
            If ae.IsOverride Then Continue For
            If draftRemap.ContainsKey(ae.FormID) Then Continue For
            draftRemap(ae.FormID) = (CUInt(selfMasterIdx) << 24) Or nextSelfObjIndex
            nextSelfObjIndex += 1UI
        Next
        For Each ao In armoEntries
            If ao.IsOverride Then Continue For
            If draftRemap.ContainsKey(ao.FormID) Then Continue For
            draftRemap(ao.FormID) = (CUInt(selfMasterIdx) << 24) Or nextSelfObjIndex
            nextSelfObjIndex += 1UI
        Next

        Dim remapper As NpcSubrecordWriter.FormIdRemapper =
            Function(globalFormID As UInteger) As UInteger
                If globalFormID = 0UI Then Return 0UI
                ' NEW OTFT provisional → real self FormID. Checked FIRST: the 0xFF high byte is not a
                ' resolvable master, so GetOriginatingPluginName would fail to map it otherwise.
                Dim mappedDraft As UInteger
                If draftRemap.TryGetValue(globalFormID, mappedDraft) Then Return mappedDraft
                Dim pname = pluginManager.GetOriginatingPluginName(globalFormID)
                If String.IsNullOrEmpty(pname) Then
                    ' FormID master byte not resolvable to a loaded plugin. Best effort: keep raw.
                    Return globalFormID
                End If
                ' Object ID width depends on the SOURCE encoding: an ESL/light global is
                ' 0xFE | (lightSlot << 12) | object12 — masking 0xFFFFFF would keep the light-slot bits
                ' and corrupt the reference, so take only the low 12 bits. Full sources use the low 24.
                ' The OUTPUT is always full-form (newIdx << 24 | object) — xEdit references ESL masters
                ' the same way (LoadOrderFileIDtoFileFileID emits CreateFull(i) on FO4).
                Dim isLightSource As Boolean = ((globalFormID >> 24) And &HFFUI) = &HFEUI
                Dim localObjectID = If(isLightSource, globalFormID And &HFFFUI, globalFormID And &HFFFFFFUI)
                Dim newIdx As Integer
                If masterIndexLookup.TryGetValue(pname, newIdx) Then
                    Return (CUInt(newIdx) << 24) Or localObjectID
                ElseIf String.Equals(pname, outputName, StringComparison.OrdinalIgnoreCase) Then
                    Return (CUInt(selfMasterIdx) << 24) Or localObjectID
                Else
                    ' Unknown plugin (referenced but not in MAST list). Should not happen since
                    ' Step 2 added all referenced names. Defensive fallback: keep raw FormID.
                    Return globalFormID
                End If
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
            Dim list As List(Of UInteger) = Nothing
            If auditPerPlugin.TryGetValue(m, list) Then
                result.MasterAudit(m) = list
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
        ' Step 4: Serialize each NPC_ record (overrides + preserved existing).
        ' ====================================================================
        Dim recordBuffers As New List(Of Byte())
        For Each entry In entries
            recordBuffers.Add(SerializeNpcRecord(entry, remapper))
        Next
        For Each existing In existingRecords
            recordBuffers.Add(SerializeExistingRecord(existing, existingMasters, pluginManager, remapper))
        Next
        ' NEW NPC_ records (clones with self-index FormIDs). Emitted into the same NPC_ GRUP as the
        ' overrides — CK / xEdit / engine all consume NPC_ records uniformly regardless of override-vs-new.
        For Each ce In npcCreateEntries
            recordBuffers.Add(SerializeNpcCreateRecord(ce, remapper, game))
        Next

        ' OTFT outfit records (Edit Outfit "Create" tab). Each emits as a top-level record: NEW ones
        ' carry a self-index FormID (via draftRemap inside the remapper); OVERRIDE ones keep their real
        ' FormID. INAM items are remapped against the new MAST list.
        Dim otftBuffers As New List(Of Byte())
        For Each oe In outfitEntries
            otftBuffers.Add(SerializeOtftRecord(oe, remapper, game))
        Next

        ' LVLI leveled lists (Edit Outfit "New LVL…"). Each emits as a self-index top-level record; LVLO
        ' references are remapped (draft → self via draftRemap; real ARMO/LVLI → master remap).
        Dim lvliBuffers As New List(Of Byte())
        Dim lvlnBuffers As New List(Of Byte())
        For Each le In leveledEntries
            Dim buf = SerializeLvliRecord(le, remapper, game)
            If le.IsNpcList Then lvlnBuffers.Add(buf) Else lvliBuffers.Add(buf)
        Next

        ' MSWP / ARMA / ARMO records (NEW-only). Each emits a self-index top-level record; every FormID it
        ' references is remapped (draft → self via draftRemap; real → master remap).
        Dim mswpBuffers As New List(Of Byte())
        For Each mw In mswpEntries
            mswpBuffers.Add(SerializeMswpRecord(mw, remapper, game))
        Next
        Dim armaBuffers As New List(Of Byte())
        For Each ae In armaEntries
            armaBuffers.Add(SerializeArmaRecord(ae, remapper, game, pluginManager))
        Next
        Dim armoBuffers As New List(Of Byte())
        For Each ao In armoEntries
            armoBuffers.Add(SerializeArmoRecord(ao, remapper, game, pluginManager))
        Next

        ' ====================================================================
        ' Step 5: Wrap each record type in its own top-level GRUP. Order matches xEdit canonical
        ' wbGroupOrder (built from wbRecord(...) declaration order in wbDefinitionsFO4.pas):
        '   OTFT (9698) → LVLI (10352) → NPC_ (10617). xEdit's PrepareSave sorts top-level GRUPs
        '   by SortOrder (wbImplementation.pas:5212 wbMergeSortPtr CompareSortOrder), so any
        '   plugin loaded into xEdit + re-saved comes out in this order. Match it on write so
        '   our output is byte-comparable.
        ' FormID resolution is global, so engine doesn't require this order — it's pure xEdit
        ' canonicality.
        ' ====================================================================
        ' Referenced-first GRUP order: MSWP → ARMA → ARMO → OTFT → LVLN → LVLI → NPC_. A referenced record
        ' precedes its referrer (MSWP is referenced by ARMA/ARMO; ARMA by ARMO; ARMA/ARMO by OTFT/LVLI/NPC_).
        ' FormID resolution is global so the engine doesn't require this — it keeps the file readable/clean.
        Dim grupMswpBytes As Byte() = If(mswpBuffers.Count > 0, BuildGrup("MSWP", mswpBuffers), Array.Empty(Of Byte)())
        Dim grupArmaBytes As Byte() = If(armaBuffers.Count > 0, BuildGrup("ARMA", armaBuffers), Array.Empty(Of Byte)())
        Dim grupArmoBytes As Byte() = If(armoBuffers.Count > 0, BuildGrup("ARMO", armoBuffers), Array.Empty(Of Byte)())
        Dim grupOtftBytes As Byte() = If(otftBuffers.Count > 0, BuildGrup("OTFT", otftBuffers), Array.Empty(Of Byte)())
        ' LVLN (Leveled NPC, decl 10329) va ANTES que LVLI (10352) en el group order de xEdit.
        Dim grupLvlnBytes As Byte() = If(lvlnBuffers.Count > 0, BuildGrup("LVLN", lvlnBuffers), Array.Empty(Of Byte)())
        Dim grupLvliBytes As Byte() = If(lvliBuffers.Count > 0, BuildGrup("LVLI", lvliBuffers), Array.Empty(Of Byte)())
        Dim grupNpcBytes = BuildGrup("NPC_", recordBuffers)

        ' ====================================================================
        ' Step 6: Build TES4 header + emit final stream.
        ' numRecords counts every content record (NPC_ + OTFT + LVLI). Matches xEdit's
        ' Pred(GetCountedRecordCount) at wbImplementation.pas:5219 — TES4 itself excluded.
        ' nextObjectId follows TwbFile.NewFormID semantics (wbImplementation.pas:5083-5122):
        '   - The draft counter (nextSelfObjIndex) was seeded from max(0x800, disk HEDR) and
        '     advanced once per NEW draft (OTFT + LVLI). Its final value is the first free slot
        '     after this save → exactly what HEDR.nextObjectId must hold.
        '   - Fresh plugin with no drafts: nextSelfObjIndex stayed at 0x800.
        '   - Update-existing with no new drafts but disk had advanced counter: preserved through
        '     the seed.
        ' Mask clamps the counter to the object-ID width: ESL/light = 12 bits (0xFFF),
        ' full plugin = 24 bits (0xFFFFFF). Mirror of TwbFile.NewFormID mask logic.
        ' ====================================================================
        Dim objectIdMask As UInteger = If(lightMaster, &HFFFUI, &HFFFFFFUI)
        Dim nextObjectId As UInteger = nextSelfObjIndex
        If nextObjectId > objectIdMask Then nextObjectId = objectIdMask
        Dim totalRecords As Integer = recordBuffers.Count + otftBuffers.Count + lvliBuffers.Count + lvlnBuffers.Count +
                                      mswpBuffers.Count + armaBuffers.Count + armoBuffers.Count
        Dim tes4Bytes = BuildTes4Header(game, markAsMaster, lightMaster, sortedMasters, totalRecords, nextObjectId, gameMaster, Path.GetDirectoryName(outputPath))

        ' ====================================================================
        ' Step 7: Atomic write (.tmp + rename).
        ' ====================================================================
        Dim outDir = Path.GetDirectoryName(outputPath)
        If Not String.IsNullOrEmpty(outDir) AndAlso Not Directory.Exists(outDir) Then
            Directory.CreateDirectory(outDir)
        End If

        Dim tmpPath = outputPath & ".tmp"
        Using fs As FileStream = File.Create(tmpPath)
            fs.Write(tes4Bytes, 0, tes4Bytes.Length)
            ' Canonical referenced-first GRUP order: MSWP → ARMA → ARMO → OTFT → LVLN → LVLI → NPC_ (Step 5).
            If grupMswpBytes.Length > 0 Then fs.Write(grupMswpBytes, 0, grupMswpBytes.Length)
            If grupArmaBytes.Length > 0 Then fs.Write(grupArmaBytes, 0, grupArmaBytes.Length)
            If grupArmoBytes.Length > 0 Then fs.Write(grupArmoBytes, 0, grupArmoBytes.Length)
            If grupOtftBytes.Length > 0 Then fs.Write(grupOtftBytes, 0, grupOtftBytes.Length)
            If grupLvlnBytes.Length > 0 Then fs.Write(grupLvlnBytes, 0, grupLvlnBytes.Length)
            If grupLvliBytes.Length > 0 Then fs.Write(grupLvliBytes, 0, grupLvliBytes.Length)
            fs.Write(grupNpcBytes, 0, grupNpcBytes.Length)
        End Using

        If File.Exists(outputPath) Then File.Delete(outputPath)
        File.Move(tmpPath, outputPath)

        Return result
    End Function

    ' ========================================================================
    ' FormID collection helpers — walk an NPC_Data and report every FormID.
    ' Mirrors NpcSubrecordWriter emission paths. Anything new added to the
    ' writer must be added here too.
    ' ========================================================================

    Private Sub CollectFormIDs(npc As NPC_Data, sink As HashSet(Of UInteger))
        ' AddNZ: only adds non-zero FormIDs. Zero means NULL reference / absent — it does NOT
        ' contribute a master, and adding it would cause GetOriginatingPluginName(0) to return
        ' the plugin at load-order-index 0 (typically Fallout4.esm) which is innocuous (game
        ' master is forced anyway), but it pollutes the audit and risks false positives in
        ' "preserve survivor" logic if the same FormID resolves to an unloaded plugin.
        If npc.HasPreviewTransform AndAlso npc.PreviewTransformFormID <> 0UI Then sink.Add(npc.PreviewTransformFormID)
        If npc.HasAnimationSound AndAlso npc.AnimationSoundFormID <> 0UI Then sink.Add(npc.AnimationSoundFormID)
        For Each f In npc.Factions
            If f.FactionFormID <> 0UI Then sink.Add(f.FactionFormID)
        Next
        If npc.HasDeathItem AndAlso npc.DeathItemFormID <> 0UI Then sink.Add(npc.DeathItemFormID)
        If npc.HasVoice AndAlso npc.VoiceFormID <> 0UI Then sink.Add(npc.VoiceFormID)
        If npc.HasTemplate AndAlso npc.TemplateFormID <> 0UI Then sink.Add(npc.TemplateFormID)
        If npc.HasLegendaryTemplate AndAlso npc.LegendaryTemplateFormID <> 0UI Then sink.Add(npc.LegendaryTemplateFormID)
        If npc.HasLegendaryChance AndAlso npc.LegendaryChanceFormID <> 0UI Then sink.Add(npc.LegendaryChanceFormID)
        For Each kv In npc.TemplateActorFormIDs
            If kv.Value <> 0UI Then sink.Add(kv.Value)
        Next
        If npc.HasRace AndAlso npc.RaceFormID <> 0UI Then sink.Add(npc.RaceFormID)
        For Each fid In npc.ActorEffectFormIDs
            If fid <> 0UI Then sink.Add(fid)
        Next
        If npc.Destruction IsNot Nothing Then
            For Each r In npc.Destruction.Resistances
                If r.DamageTypeFormID <> 0UI Then sink.Add(r.DamageTypeFormID)
            Next
            For Each s In npc.Destruction.Stages
                If s.ExplosionFormID <> 0UI Then sink.Add(s.ExplosionFormID)
                If s.DebrisFormID <> 0UI Then sink.Add(s.DebrisFormID)
                If s.MaterialSwapFormID <> 0UI Then sink.Add(s.MaterialSwapFormID)
            Next
        End If
        If npc.HasSkin AndAlso npc.SkinFormID <> 0UI Then sink.Add(npc.SkinFormID)
        If npc.HasFarAwayModel AndAlso npc.FarAwayModelFormID <> 0UI Then sink.Add(npc.FarAwayModelFormID)
        If npc.HasAttackRace AndAlso npc.AttackRaceFormID <> 0UI Then sink.Add(npc.AttackRaceFormID)
        For Each a In npc.Attacks
            If a.AttackSpellFormID <> 0UI Then sink.Add(a.AttackSpellFormID)
            If a.HasWeaponSlot AndAlso a.WeaponSlotFormID <> 0UI Then sink.Add(a.WeaponSlotFormID)
            If a.HasRequiredSlot AndAlso a.RequiredSlotFormID <> 0UI Then sink.Add(a.RequiredSlotFormID)
        Next
        If npc.HasSpectatorOverride AndAlso npc.SpectatorOverrideFormID <> 0UI Then sink.Add(npc.SpectatorOverrideFormID)
        If npc.HasObserveDeadBodyOverride AndAlso npc.ObserveDeadBodyOverrideFormID <> 0UI Then sink.Add(npc.ObserveDeadBodyOverrideFormID)
        If npc.HasGuardWarnOverride AndAlso npc.GuardWarnOverrideFormID <> 0UI Then sink.Add(npc.GuardWarnOverrideFormID)
        If npc.HasCombatOverride AndAlso npc.CombatOverrideFormID <> 0UI Then sink.Add(npc.CombatOverrideFormID)
        If npc.HasFollowerCommand AndAlso npc.FollowerCommandFormID <> 0UI Then sink.Add(npc.FollowerCommandFormID)
        If npc.HasFollowerElevator AndAlso npc.FollowerElevatorFormID <> 0UI Then sink.Add(npc.FollowerElevatorFormID)
        For Each p In npc.Perks
            If p.PerkFormID <> 0UI Then sink.Add(p.PerkFormID)
        Next
        For Each pr In npc.Properties
            If pr.ActorValueFormID <> 0UI Then sink.Add(pr.ActorValueFormID)
        Next
        If npc.HasForcedLocRefType AndAlso npc.ForcedLocRefTypeFormID <> 0UI Then sink.Add(npc.ForcedLocRefTypeFormID)
        If npc.HasNativeTerminal AndAlso npc.NativeTerminalFormID <> 0UI Then sink.Add(npc.NativeTerminalFormID)
        For Each item In npc.Inventory
            If item.ItemFormID <> 0UI Then sink.Add(item.ItemFormID)
            If item.HasCoed AndAlso item.CoedOwnerFormID <> 0UI Then sink.Add(item.CoedOwnerFormID)
            ' COED extra slot is a GLOB FormID when Owner is NPC_ (wbCOEDOwnerDecider). Including
            ' it here ensures the master defining that Global Variable lands in the new MAST list.
            If item.HasCoed AndAlso item.CoedExtraIsFormID AndAlso item.CoedOwnerExtra <> 0UI Then sink.Add(item.CoedOwnerExtra)
        Next
        For Each fid In npc.AiPackageFormIDs
            If fid <> 0UI Then sink.Add(fid)
        Next
        For Each fid In npc.KeywordFormIDs
            If fid <> 0UI Then sink.Add(fid)
        Next
        For Each fid In npc.AttachParentSlotFormIDs
            If fid <> 0UI Then sink.Add(fid)
        Next
        For Each combo In npc.ObjectTemplateCombinations
            If combo.Combination IsNot Nothing Then
                For Each fid In combo.Combination.IncludeOMODFormIDs
                    If fid <> 0UI Then sink.Add(fid)
                Next
                For Each fid In combo.Combination.Keywords
                    If fid <> 0UI Then sink.Add(fid)
                Next
                ' OBTS Properties: Value1 is a FormID when ValueType is FormIDInt(4) or FormIDFloat(6)
                ' — per wbObjectModProperties (wbDefinitionsFO4.pas:5826-5865). Must enter the master
                ' audit so an ESL/light-master Actor Value (AVIF) reference brings its plugin into the
                ' new MAST list and the writer remap doesn't fall back to "keep raw FormID".
                For Each prop In combo.Combination.Properties
                    If (prop.ValueType = OMOD_ValueType.FormIDInt OrElse prop.ValueType = OMOD_ValueType.FormIDFloat) AndAlso prop.Value1FormID <> 0UI Then
                        sink.Add(prop.Value1FormID)
                    End If
                Next
            End If
        Next
        If npc.HasClass AndAlso npc.ClassFormID <> 0UI Then sink.Add(npc.ClassFormID)
        For Each fid In npc.HeadPartFormIDs
            If fid <> 0UI Then sink.Add(fid)
        Next
        If npc.HasHairColor AndAlso npc.HairColorFormID <> 0UI Then sink.Add(npc.HairColorFormID)
        If npc.HasFacialHairColor AndAlso npc.FacialHairColorFormID <> 0UI Then sink.Add(npc.FacialHairColorFormID)
        If npc.HasCombatStyle AndAlso npc.CombatStyleFormID <> 0UI Then sink.Add(npc.CombatStyleFormID)
        If npc.HasGiftFilter AndAlso npc.GiftFilterFormID <> 0UI Then sink.Add(npc.GiftFilterFormID)
        For Each s In npc.ActorSounds
            If s.KeywordFormID <> 0UI Then sink.Add(s.KeywordFormID)
            If s.SoundFormID <> 0UI Then sink.Add(s.SoundFormID)
        Next
        If npc.HasInheritsSoundsFrom AndAlso npc.InheritsSoundsFromFormID <> 0UI Then sink.Add(npc.InheritsSoundsFromFormID)
        If npc.HasPowerArmorStand AndAlso npc.PowerArmorStandFormID <> 0UI Then sink.Add(npc.PowerArmorStandFormID)
        If npc.HasDefaultOutfit AndAlso npc.DefaultOutfitFormID <> 0UI Then sink.Add(npc.DefaultOutfitFormID)
        If npc.HasSleepOutfit AndAlso npc.SleepOutfitFormID <> 0UI Then sink.Add(npc.SleepOutfitFormID)
        If npc.HasDefaultPackageList AndAlso npc.DefaultPackageListFormID <> 0UI Then sink.Add(npc.DefaultPackageListFormID)
        If npc.HasCrimeFaction AndAlso npc.CrimeFactionFormID <> 0UI Then sink.Add(npc.CrimeFactionFormID)
        If npc.HasHeadTexture AndAlso npc.HeadTextureFormID <> 0UI Then sink.Add(npc.HeadTextureFormID)
        ' VMAD FormIDs (already resolved by scanner).
        If npc.Vmad IsNot Nothing Then
            For Each ref In npc.Vmad.FormIdPositions
                If ref.ResolvedFormID <> 0UI Then sink.Add(ref.ResolvedFormID)
            Next
        End If
    End Sub

    ''' <summary>Walk an existing PluginRecord (loaded via PluginReader) and collect every
    ''' FormID candidate. Since PluginRecord stores raw subrecord bytes, we scan known
    ''' FormID positions per signature. This is conservative: unknown subrecords are
    ''' skipped (their FormIDs will resolve as raw and may break — caller's responsibility
    ''' to flag this if "Update existing" is used with non-NPC records).</summary>
    Private Sub CollectFormIDsFromSubrecords(rec As PluginRecord,
                                             existingMasters As List(Of String),
                                             pluginManager As PluginManager,
                                             sink As HashSet(Of UInteger))
        ' For now we only support NPC_ records when preserving existing — the parser
        ' has already extracted everything via ParseNPC, so it's enough to re-parse
        ' and feed CollectFormIDs.
        If rec.Header.Signature = "NPC_" Then
            ' Build a temporary parsed view to enumerate FormIDs.
            Dim parsedNpc = RecordParsers.ParseNPC(rec, rec.SourcePluginName, pluginManager)
            CollectFormIDs(parsedNpc, sink)
        Else
            ' For non-NPC records, conservatively walk subrecords and collect any 4-byte
            ' payload as a candidate FormID. Acceptable for "preserve existing" since the
            ' alternative (losing them) is worse. False positives don't break: PluginManager
            ' just resolves them as no-ops.
            For Each subrec In rec.Subrecords
                If subrec.Data IsNot Nothing AndAlso subrec.Data.Length = 4 Then
                    Dim raw = BitConverter.ToUInt32(subrec.Data, 0)
                    If raw <> 0UI Then
                        ' Resolve via the source plugin's MAST list (rec.SourcePluginName).
                        Dim resolved = pluginManager.ResolveReferencedFormID(rec.SourcePluginName, raw)
                        sink.Add(resolved)
                    End If
                End If
            Next
        End If
    End Sub

    ''' <summary>Collect every FormID an ARMA record references so the defining plugin lands in the new
    ''' MAST list (mirror of xEdit ReportRequiredMasters). Provisional draft FormIDs (0xFF high byte) are
    ''' skipped — they resolve via draftRemap, not a master (same rule as the OTFT/LVLI collectors). NEW
    ''' records do NOT add their own (provisional) FormID; OVERRIDE records add it. For OVERRIDE we ALSO walk
    ''' the preserved source subrecords (YNAM/DEST/OBTS/MO4S/...) so every master they reference enters the
    ''' new MAST list — otherwise the override-merge remapper would fall back to "keep raw" and corrupt them.</summary>
    Private Sub CollectFormIDsFromArma(entry As ArmaRecordEntry, sink As HashSet(Of UInteger), pluginManager As PluginManager)
        AddRefFormID(sink, entry.RaceFormID)
        AddRefFormID(sink, entry.FootstepSetFormID)             ' SNDD (owned)
        For Each fid In entry.AdditionalRaces
            AddRefFormID(sink, fid)
        Next
        AddRefFormID(sink, entry.MaleSkinTextureFormID)         ' NAM0
        AddRefFormID(sink, entry.FemaleSkinTextureFormID)       ' NAM1
        AddRefFormID(sink, entry.MaleSkinTextureSwapListFormID) ' NAM2
        AddRefFormID(sink, entry.FemaleSkinTextureSwapListFormID) ' NAM3
        AddRefFormID(sink, entry.MaleMaterialSwapFormID)        ' MO2S
        AddRefFormID(sink, entry.FemaleMaterialSwapFormID)      ' MO3S
        AddRefFormID(sink, entry.MaleFPMaterialSwapFormID)      ' MO4S (owned)
        AddRefFormID(sink, entry.FemaleFPMaterialSwapFormID)    ' MO5S (owned)
        AddRefFormID(sink, entry.ArtObjectFormID)               ' ONAM (owned)
        If entry.IsOverride AndAlso entry.FormID <> 0UI AndAlso Not IsProvisionalDraftFormID(entry.FormID) Then sink.Add(entry.FormID)
        If entry.IsOverride AndAlso entry.SourceRecord IsNot Nothing Then
            CollectPreservedSourceFormIDs(entry.SourceRecord, pluginManager, sink)
        End If
    End Sub

    ''' <summary>Collect every FormID an ARMO record references. See <see cref="CollectFormIDsFromArma"/>
    ''' for the draft-skip / own-FormID / OVERRIDE-preserved rules.</summary>
    Private Sub CollectFormIDsFromArmo(entry As ArmoRecordEntry, sink As HashSet(Of UInteger), pluginManager As PluginManager)
        AddRefFormID(sink, entry.RaceFormID)             ' RNAM
        AddRefFormID(sink, entry.InstanceNamingFormID)   ' INRD (owned)
        AddRefFormID(sink, entry.EnchantmentFormID)      ' EITM (owned)
        AddRefFormID(sink, entry.PatternFormID)          ' PTRN (owned)
        AddRefFormID(sink, entry.EquipTypeFormID)        ' ETYP (owned)
        AddRefFormID(sink, entry.PickupSoundFormID)      ' YNAM (owned)
        AddRefFormID(sink, entry.DropSoundFormID)        ' ZNAM (owned)
        AddRefFormID(sink, entry.AlternateBlockMaterialFormID) ' BAMT (owned)
        For Each dr In entry.DamageResistances
            AddRefFormID(sink, dr.DamageTypeFormID)      ' DAMA Type [DMGT] (owned)
        Next
        AddRefFormID(sink, entry.TemplateArmorFormID)    ' TNAM
        For Each addon In entry.ArmorAddons
            AddRefFormID(sink, addon.ArmaFormID)         ' MODL
        Next
        For Each fid In entry.KeywordFormIDs
            AddRefFormID(sink, fid)                      ' KWDA
        Next
        For Each fid In entry.AttachParentSlotFormIDs
            AddRefFormID(sink, fid)                      ' APPR
        Next
        AddRefFormID(sink, entry.MaleMaterialSwapFormID)   ' MO2S
        AddRefFormID(sink, entry.FemaleMaterialSwapFormID) ' MO4S
        ' OBTS Object Template combinations: when the record emits its OBTS block FROM THE MODEL — a NEW record
        ' ALWAYS does; an OVERRIDE only when CombinationsAuthored (else the source bytes are preserved verbatim and
        ' their masters arrive via existingMasters) — the referenced FormIDs live in entry.Combinations, NOT in a
        ' source record. Without collecting them here the defining plugin of an authored Include OMOD / FormID
        ' Property Value1 / combination Keyword never enters the MAST list → the OBTS FormID is written raw
        ' (dangling) by the remapper's "unknown plugin" fallback. Matches BuildObtsPayload's own remap set
        ' (NpcSubrecordWriter: keywords, Include.ModFormID, FormID-typed Property Value1).
        If (Not entry.IsOverride) OrElse entry.CombinationsAuthored Then
            For Each combo In entry.Combinations
                If combo Is Nothing Then Continue For
                For Each kw In combo.Keywords
                    AddRefFormID(sink, kw)
                Next
                For Each inc In combo.Includes
                    AddRefFormID(sink, inc.ModFormID)
                Next
                For Each prop In combo.Properties
                    If prop.ValueType = OMOD_ValueType.FormIDInt OrElse prop.ValueType = OMOD_ValueType.FormIDFloat Then
                        AddRefFormID(sink, prop.Value1FormID)
                    End If
                Next
            Next
        End If
        If entry.IsOverride AndAlso entry.FormID <> 0UI AndAlso Not IsProvisionalDraftFormID(entry.FormID) Then sink.Add(entry.FormID)
        If entry.IsOverride AndAlso entry.SourceRecord IsNot Nothing Then
            CollectPreservedSourceFormIDs(entry.SourceRecord, pluginManager, sink)
        End If
    End Sub

    ''' <summary>Collect every FormID an MSWP record references. MSWP has NO embedded FormIDs in its body
    ''' (per wbDefinitionsFO4.pas:12798) — only its own FormID matters, and only when overriding.</summary>
    Private Sub CollectFormIDsFromMswp(entry As MswpRecordEntry, sink As HashSet(Of UInteger))
        If entry.IsOverride AndAlso entry.FormID <> 0UI AndAlso Not IsProvisionalDraftFormID(entry.FormID) Then sink.Add(entry.FormID)
    End Sub

    ''' <summary>For an OVERRIDE, walk the source record's subrecords and add every FormID the PRESERVED
    ''' subrecords reference to the master-discovery sink (resolved to GLOBAL via the source plugin's MAST
    ''' list). This guarantees the defining plugins land in the new MAST list so the override-merge remapper
    ''' never falls back to "keep raw" on a preserved FormID. We do NOT filter by owned-vs-preserved here: a
    ''' FormID an OWNED subrecord references is already collected from the entry, and adding the same FormID
    ''' twice (via the source) is harmless (the sink is a HashSet). Classification mirrors EmitPreservedSubrecord:
    ''' single-FormID sigs @0, DAMC stride 8, DSTD @8/@12, DAMA, OBTS, VMAD (via scanner). Unknown FormID-
    ''' bearing sigs are NOT special-cased here (collection is best-effort for the master walk; the SERIALIZER
    ''' is where an unclassified FormID-bearing sig throws). Non-FormID sigs contribute nothing.</summary>
    Private Sub CollectPreservedSourceFormIDs(src As PluginRecord, pluginManager As PluginManager, sink As HashSet(Of UInteger))
        Dim srcName = src.SourcePluginName
        Dim addLocal = Sub(rawLocal As UInteger)
                           If rawLocal = 0UI Then Return
                           Dim g = pluginManager.ResolveReferencedFormID(srcName, rawLocal)
                           If g <> 0UI AndAlso Not IsProvisionalDraftFormID(g) Then sink.Add(g)
                       End Sub
        For Each sr In src.Subrecords
            Dim data = If(sr.Data, Array.Empty(Of Byte)())
            Select Case sr.Signature
                Case "BIDS", "DMDS"
                    ' Owned single-FormID sigs are collected from the entry, NOT here: ARMO INRD/EITM/PTRN/YNAM/
                    ' ZNAM/ETYP/BAMT (CollectFormIDsFromArmo) and ARMA SNDD/ONAM/MO4S/MO5S (CollectFormIDsFromArma).
                    ' Only BIDS (still preserved) and DMDS (inside DEST) remain preserved single-FormID here.
                    If data.Length >= 4 Then addLocal(BitConverter.ToUInt32(data, 0))
                Case "DAMC"
                    Dim n = data.Length \ 8
                    For i = 0 To n - 1
                        addLocal(BitConverter.ToUInt32(data, i * 8))
                    Next
                Case "DSTD"
                    If data.Length >= 16 Then
                        addLocal(BitConverter.ToUInt32(data, 8))
                        addLocal(BitConverter.ToUInt32(data, 12))
                    End If
                ' DAMA is now OWNED by the ARMO entry (CollectFormIDsFromArmo walks its DamageResistances) —
                ' no preserved-source collection here.
                Case "OBTS"
                    CollectObtsLocalFormIDs(data, addLocal)
                Case "VMAD"
                    Dim vmad = NpcVmadScanner.Scan(data, srcName, pluginManager)
                    If vmad IsNot Nothing Then
                        For Each ref In vmad.FormIdPositions
                            If ref.ResolvedFormID <> 0UI AndAlso Not IsProvisionalDraftFormID(ref.ResolvedFormID) Then sink.Add(ref.ResolvedFormID)
                        Next
                    End If
            End Select
        Next
    End Sub

    ''' <summary>Read an OBTS payload's FormIDs (source-local) into <paramref name="addLocal"/>. Layout per
    ''' ParseOBTSPayload (RecordParsers.vb:1763): Keywords (@16+), Includes (Mod FormID), Property Value1
    ''' (ValueType-gated). Mirror of RemapObtsPayload's walk but collect-only.</summary>
    Private Sub CollectObtsLocalFormIDs(raw As Byte(), addLocal As Action(Of UInteger))
        If raw Is Nothing OrElse raw.Length < 17 Then Return
        Dim includeCount As Integer = CInt(BitConverter.ToUInt32(raw, 0))
        Dim propertyCount As Integer = CInt(BitConverter.ToUInt32(raw, 4))
        Dim offset As Integer = 15
        Dim kwCount As Integer = CInt(raw(offset))
        offset += 1
        For i = 0 To kwCount - 1
            If offset + 4 > raw.Length Then Exit For
            addLocal(BitConverter.ToUInt32(raw, offset))
            offset += 4
        Next
        offset += 2
        For i = 0 To includeCount - 1
            If offset + 7 > raw.Length Then Exit For
            addLocal(BitConverter.ToUInt32(raw, offset))
            offset += 7
        Next
        Const propertyEntrySize As Integer = 24
        For i = 0 To propertyCount - 1
            If offset + propertyEntrySize > raw.Length Then Exit For
            Dim valueType As Byte = raw(offset)
            If valueType = CByte(OMOD_ValueType.FormIDInt) OrElse valueType = CByte(OMOD_ValueType.FormIDFloat) Then
                addLocal(BitConverter.ToUInt32(raw, offset + 12))
            End If
            offset += propertyEntrySize
        Next
    End Sub

    ''' <summary>Add a referenced FormID to the master-discovery sink, skipping NULL (0) and provisional
    ''' draft sentinels (0xFF high byte — resolved via draftRemap, never a master). Mirror of the inline
    ''' guard the OTFT/LVLI collectors use.</summary>
    Private Sub AddRefFormID(sink As HashSet(Of UInteger), fid As UInteger)
        If fid <> 0UI AndAlso Not IsProvisionalDraftFormID(fid) Then sink.Add(fid)
    End Sub

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

    ''' <summary>Serialize one LVLI (leveled item) record: 24-byte header + body. Body order mirrors the
    ''' xEdit FO4 definition (wbDefinitionsFO4.pas:10352): EDID + OBND(zeroed) + LVLD + LVLM + LVLF + LLCT +
    ''' N×LVLO. OBND is zeroed (12 bytes) — meaningless for a leveled list but marked required by xEdit, so
    ''' emitting it keeps the record error-free in xEdit; the engine ignores it. LVLG (Use Global) is omitted
    ''' (no global). Each LVLO is 12 bytes: Level(u16)+pad(2)+Reference(u32, remapped)+Count(u16)+ChanceNone(u8)+pad(1)
    ''' per wbDefinitionsCommon.pas:5704. The record FormID is the draft's real self-index (via draftRemap).</summary>
    Private Function SerializeLvliRecord(entry As LvliRecordEntry, remapper As NpcSubrecordWriter.FormIdRemapper, game As Config_App.Game_Enum) As Byte()
        Dim body As Byte()
        Using bms As New MemoryStream()
            Using bw As New BinaryWriter(bms)
                ' Subrecord order mirrors wbDefinitionsFO4.pas:10352-10374:
                ' EDID, OBND(req), LVLD, LVLM, LVLF(req), LVLG(opt), LLCT, N×(LVLO + COED?), LLKC(opt), LVSG(opt), ONAM(opt)
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
                ' LVLM (Max Count, u8).
                WriteSubrecordHeader(bw, "LVLM", 1)
                bw.Write(entry.MaxCount)
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
                Dim filters = entry.FilterKeywords.Where(Function(f) f.KeywordFormID <> 0UI).ToList()
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
                    ' LVLN generic model, preserved verbatim in source order. MODS = Material Swap FormID
                    ' ([MSWP], wbDefinitionsFO4.pas:4616), remapped like any other FormID; all other model
                    ' subrecords are FormID-free and copied byte-for-byte.
                    For Each m In entry.ModelSubrecords
                        Dim mdata = If(m.Data, Array.Empty(Of Byte)())
                        If m.Signature = "MODS" AndAlso mdata.Length = 4 Then
                            WriteSubrecordHeader(bw, "MODS", 4)
                            bw.Write(remapper(BitConverter.ToUInt32(mdata, 0)))
                        Else
                            WriteSubrecordHeader(bw, m.Signature, mdata.Length)
                            bw.Write(mdata)
                        End If
                    Next
                Else
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
                    WriteZString(bw, "BNAM", subst.OriginalMaterial)
                    WriteZString(bw, "SNAM", subst.ReplacementMaterial)
                    If subst.HasColorRemapIndex Then
                        WriteSubrecordHeader(bw, "CNAM", 4)
                        bw.Write(subst.ColorRemapIndex)
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

    ''' <summary>Serialize one ARMA (Armor Addon) record. Body order per wbDefinitionsFO4.pas:6210:
    ''' EDID, BOD2(u32 slot mask), RNAM(opt), DNAM(12-byte struct, required), Biped Model
    ''' (MOD2/MO2C/MO2S/MO2F then MOD3/MO3C/MO3S/MO3F), 1st Person (MOD4/MO4F then MOD5/MO5F), NAM0..NAM3,
    ''' Additional Races (MODL array), Bone Scale (BSMP + per-bone BSMB/BSMS). Header flags carry bits
    ''' 6 (No Underarmor Scaling) / 9 (Has Sculpt Data) / 30 (Hi-Res 1st Person Only).
    ''' BOD2 confirmed single u32 at wbDefinitionsFO4.pas:3782.</summary>
    Private Function SerializeArmaRecord(entry As ArmaRecordEntry, remapper As NpcSubrecordWriter.FormIdRemapper, game As Config_App.Game_Enum, pluginManager As PluginManager) As Byte()
        If entry.IsOverride Then Return SerializeArmaRecordOverride(entry, remapper, game, pluginManager)

        Dim body As Byte()
        Using bms As New MemoryStream()
            Using bw As New BinaryWriter(bms)
                ' Owned subrecords in canonical order (wbDefinitionsFO4.pas:6210). Each EmitArmaXxx is the
                ' SINGLE source of truth for that subrecord's byte layout — shared with the override path.
                EmitArmaEdid(bw, entry)
                EmitArmaBod2(bw, entry)
                EmitArmaRnam(bw, entry, remapper)
                EmitArmaDnam(bw, entry)
                EmitArmaBipedModel(bw, entry, remapper)
                EmitArmaFirstPersonModel(bw, entry, remapper)
                EmitArmaSkinTextures(bw, entry, remapper)
                EmitArmaAdditionalRaces(bw, entry, remapper)
                EmitArmaSndd(bw, entry, remapper)
                EmitArmaOnam(bw, entry, remapper)                ' ONAM [ARTO]
                EmitArmaBoneScale(bw, entry)
            End Using
            body = bms.ToArray()
        End Using

        ' Header flags computed from the booleans (NEW records only — override preserves source flags).
        Dim flags As UInteger = ComputeArmaHeaderFlags(entry)

        Return WrapRecord("ARMA", body, flags, remapper(entry.FormID), entry.OriginalVcs1, entry.OriginalVcs2, game)
    End Function

    ' ------------------------------------------------------------------------
    ' Shared ARMA owned-subrecord emitters (one source of truth for create + override).
    ' ------------------------------------------------------------------------

    Private Sub EmitArmaEdid(bw As BinaryWriter, entry As ArmaRecordEntry)
        WriteZString(bw, "EDID", entry.EditorID)
    End Sub

    Private Sub EmitArmaBod2(bw As BinaryWriter, entry As ArmaRecordEntry)
        ' BOD2 — single u32 'First Person Flags' = slot mask (wbDefinitionsFO4.pas:3782).
        WriteSubrecordHeader(bw, "BOD2", 4)
        bw.Write(entry.SlotMask)
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
                                   Optional afterMod2 As Action = Nothing, Optional afterMod3 As Action = Nothing)
        ' Biped Model — male textured model first, then female (per xEdit RStruct order).
        If Not String.IsNullOrEmpty(entry.MaleMeshPath) Then WriteZString(bw, "MOD2", entry.MaleMeshPath)
        If afterMod2 IsNot Nothing Then afterMod2()   ' MO2T (preserved) — inside the male model struct, after MOD2
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
        If Not String.IsNullOrEmpty(entry.FemaleMeshPath) Then WriteZString(bw, "MOD3", entry.FemaleMeshPath)
        If afterMod3 IsNot Nothing Then afterMod3()   ' MO3T (preserved) — inside the female model struct, after MOD3
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
    End Sub

    ''' <summary><paramref name="afterMod4"/>/<paramref name="afterMod5"/> (override path only) emit the PRESERVED
    ''' 1st-person members (MO4T/MO4C, MO5T/MO5C) INSIDE the wbTexturedModel struct — right after MOD4/MOD5. Same
    ''' xEdit strict-order requirement as <see cref="EmitArmaBipedModel"/>: emitting them as a separate trailing
    ''' group corrupts the record's subrecord ordering.</summary>
    Private Sub EmitArmaFirstPersonModel(bw As BinaryWriter, entry As ArmaRecordEntry, remapper As NpcSubrecordWriter.FormIdRemapper,
                                         Optional afterMod4 As Action = Nothing, Optional afterMod5 As Action = Nothing)
        ' 1st Person — MOD4/MO4S/MO4F male, MOD5/MO5S/MO5F female (mirror the biped model member order). MO4C/MO5C
        ' (color-remap floats) are NOT modeled by the entry → preserved on override.
        If Not String.IsNullOrEmpty(entry.MaleFPMeshPath) Then WriteZString(bw, "MOD4", entry.MaleFPMeshPath)
        If afterMod4 IsNot Nothing Then afterMod4()   ' MO4T/MO4C (preserved) — inside the male 1st-person struct
        If entry.MaleFPMaterialSwapFormID <> 0UI Then
            WriteSubrecordHeader(bw, "MO4S", 4)
            bw.Write(remapper(entry.MaleFPMaterialSwapFormID))
        End If
        If entry.MaleFPModelFlags <> 0 Then
            WriteSubrecordHeader(bw, "MO4F", 1)
            bw.Write(entry.MaleFPModelFlags)
        End If
        If Not String.IsNullOrEmpty(entry.FemaleFPMeshPath) Then WriteZString(bw, "MOD5", entry.FemaleFPMeshPath)
        If afterMod5 IsNot Nothing Then afterMod5()   ' MO5T/MO5C (preserved) — inside the female 1st-person struct
        If entry.FemaleFPMaterialSwapFormID <> 0UI Then
            WriteSubrecordHeader(bw, "MO5S", 4)
            bw.Write(remapper(entry.FemaleFPMaterialSwapFormID))
        End If
        If entry.FemaleFPModelFlags <> 0 Then
            WriteSubrecordHeader(bw, "MO5F", 1)
            bw.Write(entry.FemaleFPModelFlags)
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
    Private Function SerializeArmoRecord(entry As ArmoRecordEntry, remapper As NpcSubrecordWriter.FormIdRemapper, game As Config_App.Game_Enum, pluginManager As PluginManager) As Byte()
        If entry.IsOverride Then Return SerializeArmoRecordOverride(entry, remapper, game, pluginManager)

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
                EmitArmoDesc(bw, entry)                          ' DESC
                EmitArmoInrd(bw, entry, remapper)
                EmitArmoModels(bw, entry, remapper)
                EmitArmoData(bw, entry)
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

    Private Sub EmitArmoDesc(bw As BinaryWriter, entry As ArmoRecordEntry)
        ' DESC — optional translatable lstring (mirror EmitArmoFull). Omit when empty.
        If Not String.IsNullOrEmpty(entry.Description) Then
            Dim descBytes = PluginEncodingSettings.EncodeTranslatable(entry.Description)
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

    Private Sub EmitArmoModels(bw As BinaryWriter, entry As ArmoRecordEntry, remapper As NpcSubrecordWriter.FormIdRemapper)
        ' Models array — INDX (u16) + MODL (ARMA FormID), list order preserved.
        For Each addon In entry.ArmorAddons
            WriteSubrecordHeader(bw, "INDX", 2)
            bw.Write(addon.AddonIndex)
            WriteSubrecordHeader(bw, "MODL", 4)
            bw.Write(remapper(addon.ArmaFormID))
        Next
    End Sub

    Private Sub EmitArmoData(bw As BinaryWriter, entry As ArmoRecordEntry)
        ' DATA — required (wbStruct cpNormal True): s32 Value, float Weight, u32 Health.
        WriteSubrecordHeader(bw, "DATA", 12)
        bw.Write(entry.Value)
        bw.Write(entry.Weight)
        bw.Write(entry.Health)
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

    ' ========================================================================
    ' OVERRIDE-MERGE serializers (re-save an EXISTING ARMA/ARMO with edits).
    '
    ' The entry carries the FINAL desired state of every OWNED field (app pre-fills from the parsed
    ' source, then applies the user's edits). The writer emits the record in CANONICAL xEdit order:
    '   • OWNED subrecords come from the entry (via the SAME EmitArmoXxx/EmitArmaXxx helpers the create
    '     path uses — one source of truth for byte layout).
    '   • PRESERVED subrecords (everything else in the source) are copied verbatim from SourceRecord,
    '     with any FormIDs they carry remapped from the SOURCE plugin's MAST list to the NEW MAST list.
    '
    ' FormID remap of a preserved subrecord: each FormID stored in the source bytes is SOURCE-LOCAL
    ' (master-indexed to the source plugin). We resolve it to GLOBAL via the source plugin's MAST list
    ' (pluginManager.ResolveReferencedFormID — same operation the parser's ResolveFormIDReference does),
    ' then through the single 'remapper' (global → new MAST index). This is the identical technique
    ' EmitVmad / ApplyObtsRemap use (patch FormIDs at known offsets in a raw copy), generalized to raw
    ' source subrecords. If a preserved signature might carry FormIDs but its layout is not classified
    ' here, we THROW (NotSupportedException) rather than copy raw FormID bytes (which would point at the
    ' wrong master) — mirror of SerializeExistingRecord's fail-loud policy.
    ' ========================================================================

    ''' <summary>Preserved-subrecord signatures that carry exactly ONE 4-byte FormID at offset 0 (the
    ''' whole payload is the FormID). Confirmed against wbDefinitionsFO4.pas:
    '''   ARMO: PTRN(5626), EITM(wbEnchantment→wbFormIDCk EITM), YNAM(5629), ZNAM(5630), ETYP(5236),
    '''         BIDS(6180), BAMT(6181), DMDS(inside DEST stage = [MSWP]).
    '''   ARMA: ONAM(6252 [ARTO]), MO4S/MO5S (1st-person material swap [MSWP], not modeled by ArmaRecordEntry
    '''         so PRESERVED).
    ''' NOTE: ARMO INRD (5632 [INNR]) and ARMA SNDD (6251 [FSTS]) are NOT here — they are OWNED single-FormID
    ''' subrecords (entry.InstanceNamingFormID / entry.FootstepSetFormID), emitted at their canonical position by
    ''' EmitArmoInrd / EmitArmaSndd, so they never route through EmitPreservedSubrecord.</summary>
    ''' NOTE: EITM/PTRN/YNAM/ZNAM/ETYP/BAMT (ARMO) and ONAM/MO4S/MO5S (ARMA) were promoted to OWNED single-FormID
    ''' subrecords (emitted from the entry at their canonical position) — they are no longer preserved. Only BIDS
    ''' ([IPDS], ARMO) and DMDS ([MSWP], inside DEST) remain preserved single-FormID subrecords.
    Private ReadOnly _singleFormIdPreservedSigs As New HashSet(Of String)(
        {"BIDS", "DMDS"},
        StringComparer.Ordinal)

    ''' <summary>Preserved-subrecord signatures that carry NO FormIDs — copied byte-for-byte. Confirmed
    ''' against xEdit: OBND(bounds), MO2T/MO3T/MO4T/MO5T(texture-set hashes, not FormIDs), MODC/MO2C/MO3C/
    ''' MO4C/MO5C(color-remap float), MO2F/MO3F/MO4F/MO5F(model flag byte), ICON/MICO/ICO2/MIC2(icon path
    ''' string), DESC(translatable lstring), DSTA(sequence name string), DMDL(model filename string),
    ''' DMDT(texture data blob), DMDF(model flag byte), DSTF/STOP/OBTF(empty markers),
    ''' EAMT(wbEnchantment Capacity = u16 amount, NOT a FormID — wbDefinitionsFO4.pas wbEnchantment).
    ''' NOTE: this set is consulted only for signatures NOT otherwise classified (single-FormID / complex /
    ''' owned). It is the "known non-FormID" allow-list; anything outside ALL classifications throws.</summary>
    ''' NOTE: OBND (now editable 6×i16) and DESC (now owned translatable) were promoted to OWNED — removed here.
    Private ReadOnly _verbatimPreservedSigs As New HashSet(Of String)(
        {"MO2T", "MO3T", "MO4T", "MO5T", "MODC", "MO2C", "MO3C", "MO4C", "MO5C",
         "MO2F", "MO3F", "MO4F", "MO5F", "ICON", "MICO", "ICO2", "MIC2", "EAMT",
         "DSTA", "DMDL", "DMDT", "DMDC", "DMDF", "DSTF", "STOP", "OBTF"},
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

        ' Fail-loud: two MODC subrecords (Male + Female world-model color remap, wbDefinitionsFO4.pas:6165 &
        ' 6171) share the signature MODC and can't be positionally disambiguated by a per-signature step.
        ' Dual world-model color remap essentially never occurs on real armor; refuse rather than reorder.
        Dim modcCount = mainSubs.Where(Function(sr) sr.Signature = "MODC").Count()
        If modcCount > 1 Then
            Throw New NotSupportedException("ARMO override: multiple MODC (world-model color remap) subrecords not supported")
        End If

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
                                   consumed += EmitPreservedStep(bw, "MODC", "ARMO", src, remapper, pluginManager, mapLocal, preservedBySig)
                               End Sub)
                consumed += EmitPreservedStep(bw, "ICON", "ARMO", src, remapper, pluginManager, mapLocal, preservedBySig)
                consumed += EmitPreservedStep(bw, "MICO", "ARMO", src, remapper, pluginManager, mapLocal, preservedBySig)
                ' Female world model struct: MOD4[owned], MO4T[pres], MO4S[owned], then ICO2/MIC2[pres] AFTER.
                EmitArmoFemaleModel(bw, entry, remapper,
                    afterMod4:=Sub() consumed += EmitPreservedStep(bw, "MO4T", "ARMO", src, remapper, pluginManager, mapLocal, preservedBySig))
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
                EmitArmoDesc(bw, entry)                                                  ' DESC  [owned] (was preserved)
                EmitArmoInrd(bw, entry, remapper)                                        ' INRD  [owned] (was preserved)
                EmitArmoModels(bw, entry, remapper)                                      ' INDX + MODL [owned]
                EmitArmoData(bw, entry)                                                  ' DATA  [owned]
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

    ''' <summary>Serialize an ARMA OVERRIDE: canonical xEdit order (wbDefinitionsFO4.pas:6210), OWNED from
    ''' the entry, PRESERVED (ONAM only) copied with FormIDs remapped. Asserts nothing dropped.</summary>
    Private Function SerializeArmaRecordOverride(entry As ArmaRecordEntry, remapper As NpcSubrecordWriter.FormIdRemapper, game As Config_App.Game_Enum, pluginManager As PluginManager) As Byte()
        Dim src = entry.SourceRecord
        If src Is Nothing Then Throw New ArgumentException("ARMA override requires SourceRecord.", NameOf(entry))
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
                EmitArmaBod2(bw, entry)                          ' BOD2  [owned]
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
                EmitArmaBipedModel(bw, entry, remapper,
                    afterMod2:=Sub() consumed += EmitPreservedStep(bw, "MO2T", "ARMA", src, remapper, pluginManager, mapLocal, preservedBySig),
                    afterMod3:=Sub() consumed += EmitPreservedStep(bw, "MO3T", "ARMA", src, remapper, pluginManager, mapLocal, preservedBySig))
                ' 1st Person [owned MOD4/MO4S/MO4F + MOD5/MO5S/MO5F] with preserved members the entry doesn't model:
                ' MO4T/MO5T (texture hashes), MO4C/MO5C (color-remap floats) — emitted INSIDE each struct after
                ' MOD4/MOD5 (same strict-order rule). MO4S/MO5S are OWNED (emitted from the entry).
                EmitArmaFirstPersonModel(bw, entry, remapper,
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
                ' numRecords reported by xEdit = total records in plugin INCLUDING TES4 itself.
                ' Bethesda actually counts only non-TES4 records here; xEdit/CK both compute it
                ' from the GRUP content. We count every content record (NPC_ + OTFT).
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
                    Dim masterBytes = Encoding.ASCII.GetBytes(masterName)
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

    Private Function TryReadMasterFileSizeForDir(outDir As String, masterName As String) As ULong
        Try
            If String.IsNullOrEmpty(outDir) Then Return 0UL
            Dim masterPath = Path.Combine(outDir, masterName)
            If Not File.Exists(masterPath) Then Return 0UL
            Return CULng(New FileInfo(masterPath).Length)
        Catch
            Return 0UL
        End Try
    End Function

    Private Function MasterFileNamePublic(game As Config_App.Game_Enum) As String
        Select Case game
            Case Config_App.Game_Enum.Fallout4 : Return "Fallout4.esm"
            Case Config_App.Game_Enum.Skyrim : Return "Skyrim.esm"
            Case Else
                Throw New ArgumentOutOfRangeException(NameOf(game), $"Unsupported game: {game}")
        End Select
    End Function

End Module
