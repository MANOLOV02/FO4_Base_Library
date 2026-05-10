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
    End Class

    ''' <summary>Save (or update) a plugin file containing the given NPC overrides.
    ''' Performs full xEdit-style MAST cleanup: any masters not referenced by the final
    ''' record set are dropped (except the game master, which is always preserved).</summary>
    ''' <param name="outputPath">Final destination path for the plugin (.esp/.esm).</param>
    ''' <param name="game">FO4 or SSE — picks game master and TES4/HEDR version constants.</param>
    ''' <param name="lightMaster">If True, set FLAG_ESM | FLAG_ESL (light master / ESL).
    ''' If False, emit as plain ESP (full slot in load order).</param>
    ''' <param name="overrides">List of NPC overrides to emit. Order is preserved.</param>
    ''' <param name="existingRecords">Optional: records from a pre-existing plugin (loaded
    ''' via PluginReader) that should be preserved alongside the new overrides. The caller
    ''' filters out NPCs whose FormIDs are about to be replaced by entries in 'overrides'.</param>
    ''' <param name="existingMasters">MAST list of the pre-existing plugin, if any. Used as
    ''' the source for resolving FormIDs inside 'existingRecords'.</param>
    ''' <param name="pluginManager">Required for FormID resolution (master high-byte → plugin name).</param>
    Public Function SaveOverridePlugin(outputPath As String,
                                       game As Config_App.Game_Enum,
                                       lightMaster As Boolean,
                                       entries As List(Of NpcOverrideEntry),
                                       existingRecords As List(Of PluginRecord),
                                       existingMasters As List(Of String),
                                       pluginManager As PluginManager) As SaveResult

        If String.IsNullOrWhiteSpace(outputPath) Then Throw New ArgumentException("outputPath is empty.", NameOf(outputPath))
        If entries Is Nothing Then entries = New List(Of NpcOverrideEntry)()
        If existingRecords Is Nothing Then existingRecords = New List(Of PluginRecord)()
        If existingMasters Is Nothing Then existingMasters = New List(Of String)()
        If pluginManager Is Nothing Then Throw New ArgumentException("pluginManager is required for FormID resolution.", NameOf(pluginManager))

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
        For Each rec In existingRecords
            allFormIDs.Add(rec.Header.FormID)
            CollectFormIDsFromSubrecords(rec, existingMasters, pluginManager, allFormIDs)
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

        Dim remapper As NpcSubrecordWriter.FormIdRemapper =
            Function(globalFormID As UInteger) As UInteger
                If globalFormID = 0UI Then Return 0UI
                Dim pname = pluginManager.GetOriginatingPluginName(globalFormID)
                If String.IsNullOrEmpty(pname) Then
                    ' FormID master byte not resolvable to a loaded plugin. Best effort: keep raw.
                    Return globalFormID
                End If
                Dim localObjectID = globalFormID And &HFFFFFFUI
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
            .MasterList = sortedMasters
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

        ' ====================================================================
        ' Step 5: Wrap in GRUP NPC_ container.
        ' ====================================================================
        Dim grupBytes = BuildNpcGrup(recordBuffers)

        ' ====================================================================
        ' Step 6: Build TES4 header + emit final stream.
        ' ====================================================================
        Dim tes4Bytes = BuildTes4Header(game, lightMaster, sortedMasters, recordBuffers.Count, gameMaster, Path.GetDirectoryName(outputPath))

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
            fs.Write(grupBytes, 0, grupBytes.Length)
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

    Private Function SerializeExistingRecord(rec As PluginRecord,
                                             existingMasters As List(Of String),
                                             pluginManager As PluginManager,
                                             remapper As NpcSubrecordWriter.FormIdRemapper) As Byte()
        ' For NPC_ records, re-serialize via ParseNPC + NpcSubrecordWriter to get full
        ' MAST cleanup. For other record types (rare in NPC_Manager-generated plugins),
        ' fall back to copy-through with a best-effort 4-byte FormID patch on each subrecord.
        If rec.Header.Signature = "NPC_" Then
            Dim parsed = RecordParsers.ParseNPC(rec, rec.SourcePluginName, pluginManager)
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

    Private Function BuildNpcGrup(recordBuffers As List(Of Byte())) As Byte()
        ' GRUP header (24 bytes): Signature "GRUP" + GroupSize (incl. header) + Label "NPC_" as u32
        ' + GroupType (0 = top-level) + Stamp + Unknown.
        Using ms As New MemoryStream()
            Using bw As New BinaryWriter(ms)
                Dim contentSize = recordBuffers.Sum(Function(b) b.Length)
                Dim totalSize = 24 + contentSize
                bw.Write(Encoding.ASCII.GetBytes("GRUP"))
                bw.Write(CUInt(totalSize))
                bw.Write(Encoding.ASCII.GetBytes("NPC_"))   ' Label u32 = signature bytes
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
                                     lightMaster As Boolean,
                                     masters As List(Of String),
                                     numContentRecords As Integer,
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
                ' from the GRUP content. We follow the CK convention (count NPC_ records only).
                bw.Write(CUInt(numContentRecords))
                bw.Write(NEXT_OBJECT_ID_DEFAULT)

                ' CNAM (author, ZSTRING)
                Dim authorBytes = Encoding.ASCII.GetBytes(NPC_MANAGER_AUTHOR_CNAM)
                WriteSubrecordHeader(bw, "CNAM", authorBytes.Length + 1)
                bw.Write(authorBytes)
                bw.Write(CByte(0))

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
            End Using
            Dim bodyBytes = bodyMs.ToArray()

            Using ms As New MemoryStream()
                Using bw As New BinaryWriter(ms)
                    bw.Write(Encoding.ASCII.GetBytes("TES4"))
                    bw.Write(CUInt(bodyBytes.Length))
                    Dim flags As UInteger = FLAG_ESM
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
