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
        ''' El body de subrecords es IDENTICO (EDID/OBND/LVLD/LVLM/LVLF/LVLG/LLCT/LVLO...); solo cambia
        ''' la signature del record y el GRUP top-level. Usar para listas de NPC_ (cada LVLO referencia
        ''' un NPC_ FormID). LVLN va antes que LVLI en el group order de xEdit (10329 &lt; 10352).</summary>
        Public IsNpcList As Boolean = False
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
                                       Optional npcCreateEntries As List(Of NpcCreateEntry) = Nothing) As SaveResult

        If String.IsNullOrWhiteSpace(outputPath) Then Throw New ArgumentException("outputPath is empty.", NameOf(outputPath))
        If entries Is Nothing Then entries = New List(Of NpcOverrideEntry)()
        If existingRecords Is Nothing Then existingRecords = New List(Of PluginRecord)()
        If existingMasters Is Nothing Then existingMasters = New List(Of String)()
        If pluginManager Is Nothing Then Throw New ArgumentException("pluginManager is required for FormID resolution.", NameOf(pluginManager))
        If outfitEntries Is Nothing Then outfitEntries = New List(Of OtftRecordEntry)()
        If leveledEntries Is Nothing Then leveledEntries = New List(Of LvliRecordEntry)()
        If npcCreateEntries Is Nothing Then npcCreateEntries = New List(Of NpcCreateEntry)()

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
            If le.IsOverride AndAlso le.FormID <> 0UI Then allFormIDs.Add(le.FormID)
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
        Dim totalRecords As Integer = recordBuffers.Count + otftBuffers.Count + lvliBuffers.Count + lvlnBuffers.Count
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
            ' Canonical xEdit GRUP order: OTFT → LVLI → NPC_ (see Step 5 comment).
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
