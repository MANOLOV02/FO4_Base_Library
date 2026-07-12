Imports System.IO
Imports System.Text

' ============================================================================
' NPC_ subrecord serializer — writes NPC_Data back to bytes in xEdit canonical
' subrecord order. Mirror of ParseNPC: every subrecord captured by the parser
' is re-emitted in the same order it appears in wbDefinitionsFO4.pas:10617-10819.
'
' FormID re-mapping:
'   When the writer emits a FormID it walks the NEW master list (built by the
'   plugin writer) and emits the appropriate (newMasterIdx << 24) | localObjectID.
'   The mapping function is provided by the caller — this module does not own
'   the master list, only consumes the FormID transformer.
' ============================================================================

Public Module NpcSubrecordWriter

    ''' <summary>Maps a global resolved FormID (from PluginManager world) to the FormID encoding
    ''' that should be written to the new plugin's records: high byte = master index in the new
    ''' MAST list, low 24 bits = the original ObjectID. The caller (plugin writer) builds this
    ''' delegate from the plugin's MAST list + the global PluginManager.</summary>
    Public Delegate Function FormIdRemapper(globalFormID As UInteger) As UInteger

    ''' <summary>Serialize an NPC_Data into a byte buffer that contains the NPC_ record body
    ''' (subrecords only — caller wraps with the record header). Subrecord order follows xEdit
    ''' wbDefinitionsFO4.pas:10617-10819 declaration order verbatim.</summary>
    Public Function SerializeNpcBody(npc As NPC_Data, remap As FormIdRemapper) As Byte()
        ' Game discriminator (set by ParseNPC from Config_App.Current.Game). The SSE and FO4 NPC_
        ' subrecords sit at the SAME positions EXCEPT the face-data tail (post-QNAM), so the divergent
        ' subrecords branch inline on isSse and the SSE tail is appended after the (FO4-only, empty on
        ' SSE) face tail. Verified byte-exact against Skyrim.esm via NpcSseRoundtripProbe.
        Dim isSse As Boolean = (npc.Game = Config_App.Game_Enum.Skyrim)
        Using ms As New MemoryStream()
            Using bw As New BinaryWriter(ms)
                EmitEdid(bw, npc.EditorID)
                EmitVmad(bw, npc.Vmad, remap)
                EmitObnd(bw, npc.ObjectBoundsRaw)
                If npc.HasPreviewTransform Then EmitFormId(bw, "PTRN", npc.PreviewTransformFormID, remap)
                If npc.HasAnimationSound Then EmitFormId(bw, "STCP", npc.AnimationSoundFormID, remap)
                EmitAcbs(bw, npc.Acbs, isSse)
                EmitFactions(bw, npc.Factions, remap, isSse)
                If npc.HasDeathItem Then EmitFormId(bw, "INAM", npc.DeathItemFormID, remap)
                If npc.HasVoice Then EmitFormId(bw, "VTCK", npc.VoiceFormID, remap)
                If npc.HasTemplate Then EmitFormId(bw, "TPLT", npc.TemplateFormID, remap)
                If npc.HasLegendaryTemplate Then EmitFormId(bw, "LTPT", npc.LegendaryTemplateFormID, remap)
                If npc.HasLegendaryChance Then EmitFormId(bw, "LTPC", npc.LegendaryChanceFormID, remap)
                EmitTpta(bw, npc.TemplateActorFormIDs, remap)
                If npc.HasRace Then EmitFormId(bw, "RNAM", npc.RaceFormID, remap)
                EmitSpct(bw, npc)
                EmitSplos(bw, npc.ActorEffectFormIDs, remap)
                EmitDestruction(bw, npc.Destruction, remap)
                If npc.HasSkin Then EmitFormId(bw, "WNAM", npc.SkinFormID, remap)
                If npc.HasFarAwayModel Then EmitFormId(bw, "ANAM", npc.FarAwayModelFormID, remap)
                If npc.HasAttackRace Then EmitFormId(bw, "ATKR", npc.AttackRaceFormID, remap)
                EmitAttacks(bw, npc.Attacks, remap)
                If npc.HasSpectatorOverride Then EmitFormId(bw, "SPOR", npc.SpectatorOverrideFormID, remap)
                If npc.HasObserveDeadBodyOverride Then EmitFormId(bw, "OCOR", npc.ObserveDeadBodyOverrideFormID, remap)
                If npc.HasGuardWarnOverride Then EmitFormId(bw, "GWOR", npc.GuardWarnOverrideFormID, remap)
                If npc.HasCombatOverride Then EmitFormId(bw, "ECOR", npc.CombatOverrideFormID, remap)
                If npc.HasFollowerCommand Then EmitFormId(bw, "FCPL", npc.FollowerCommandFormID, remap)
                If npc.HasFollowerElevator Then EmitFormId(bw, "RCLR", npc.FollowerElevatorFormID, remap)
                EmitPrkz(bw, npc)
                EmitPerks(bw, npc.Perks, remap, isSse)
                EmitProperties(bw, npc.Properties, remap)
                If npc.HasForcedLocRefType Then EmitFormId(bw, "FTYP", npc.ForcedLocRefTypeFormID, remap)
                If npc.HasNativeTerminal Then EmitFormId(bw, "NTRM", npc.NativeTerminalFormID, remap)
                EmitCoct(bw, npc)
                EmitInventory(bw, npc.Inventory, remap)
                EmitAidt(bw, npc.AiData)
                EmitPkid(bw, npc.AiPackageFormIDs, remap)
                EmitKeywords(bw, npc, remap)
                EmitAppr(bw, npc.AttachParentSlotFormIDs, remap)
                EmitObjectTemplate(bw, npc, remap)
                If npc.HasClass Then EmitFormId(bw, "CNAM", npc.ClassFormID, remap)
                If npc.HasFull Then EmitLString(bw, "FULL", npc.FullName)
                If npc.HasShortName Then EmitLString(bw, "SHRT", npc.ShortName)
                If npc.HasDataMarker Then EmitEmptyMarker(bw, "DATA")
                EmitDnam(bw, npc)
                EmitHeadParts(bw, npc.HeadPartFormIDs, remap)
                If npc.HasHairColor Then EmitFormId(bw, "HCLF", npc.HairColorFormID, remap)
                If npc.HasFacialHairColor Then EmitFormId(bw, "BCLF", npc.FacialHairColorFormID, remap)
                If npc.HasCombatStyle Then EmitFormId(bw, "ZNAM", npc.CombatStyleFormID, remap)
                If npc.HasGiftFilter Then EmitFormId(bw, "GNAM", npc.GiftFilterFormID, remap)
                EmitNam5(bw, npc.Nam5Raw)
                If npc.HasHeightMin Then EmitFloat(bw, "NAM6", npc.HeightMin)
                EmitNam7(bw, npc.Nam7Raw)
                If npc.HasHeightMax Then EmitFloat(bw, "NAM4", npc.HeightMax)
                EmitMwgt(bw, npc)
                If npc.HasSoundLevel Then EmitU32(bw, "NAM8", npc.SoundLevel)
                EmitActorSounds(bw, npc, remap)
                If npc.HasInheritsSoundsFrom Then EmitFormId(bw, "CSCR", npc.InheritsSoundsFromFormID, remap)
                If npc.HasPowerArmorStand Then EmitFormId(bw, "PFRN", npc.PowerArmorStandFormID, remap)
                If npc.HasDefaultOutfit Then EmitFormId(bw, "DOFT", npc.DefaultOutfitFormID, remap)
                If npc.HasSleepOutfit Then EmitFormId(bw, "SOFT", npc.SleepOutfitFormID, remap)
                If npc.HasDefaultPackageList Then EmitFormId(bw, "DPLT", npc.DefaultPackageListFormID, remap)
                If npc.HasCrimeFaction Then EmitFormId(bw, "CRIF", npc.CrimeFactionFormID, remap)
                If npc.HasHeadTexture Then EmitFormId(bw, "FTST", npc.HeadTextureFormID, remap)
                EmitQnam(bw, npc.TextureLightingFloats, isSse)
                ' FO4 face-data tail (all empty for SSE data — these collections/flags are never
                ' populated by the SSE parser path, so they no-op and preserve SSE ordering).
                EmitMsdkMsdv(bw, npc)
                EmitTintLayers(bw, npc)
                EmitMrsv(bw, npc.BodyMorphRegionValues)
                EmitFaceMorphs(bw, npc)
                If npc.HasFmin Then EmitFloat(bw, "FMIN", npc.FacialMorphIntensity)
                If npc.HasActivateTextOverride Then EmitLString(bw, "ATTX", npc.ActivateTextOverride)
                ' SSE face-data tail (NAM9 + NAMA + tint). Empty for FO4 data (all Nothing/empty).
                EmitSseFaceTail(bw, npc)
            End Using
            Return ms.ToArray()
        End Using
    End Function

    ' ========================================================================
    ' Subrecord-level helpers
    ' ========================================================================

    Private Sub EmitSubrecordHeader(bw As BinaryWriter, sig As String, dataSize As Integer)
        If sig.Length <> 4 Then Throw New InvalidDataException($"Subrecord signature must be 4 chars: '{sig}'.")
        If dataSize < 0 OrElse dataSize > UShort.MaxValue Then
            ' XXXX extension is required for >65535 byte payloads. NPC_ subrecords don't normally
            ' exceed u16; if this fires we have a logic bug. Throw rather than truncate silently.
            Throw New InvalidDataException($"Subrecord '{sig}' data size {dataSize} exceeds u16. XXXX extension not implemented.")
        End If
        bw.Write(Encoding.ASCII.GetBytes(sig))
        bw.Write(CUShort(dataSize))
    End Sub

    Private Sub WriteRawSubrecord(bw As BinaryWriter, sig As String, payload As Byte())
        EmitSubrecordHeader(bw, sig, If(payload Is Nothing, 0, payload.Length))
        If payload IsNot Nothing AndAlso payload.Length > 0 Then bw.Write(payload)
    End Sub

    ' ========================================================================
    ' Per-subrecord emitters (xEdit declaration order)
    ' ========================================================================

    Private Sub EmitEdid(bw As BinaryWriter, edid As String)
        ' EDID is wbStringKC cpOverride (wbDefinitionsFO4.pas:4080) → NON-translatable → General
        ' encoding (cp1252), like xEdit bsdGetEncoding's `else Result := wbEncoding` branch. EDIDs
        ' are ASCII in practice so this equals the old ASCII path, but it's now xEdit-faithful.
        Dim bytes = PluginEncodingSettings.EncodeGeneral(If(edid, "")) ' bytes WITHOUT trailing NUL.
        Dim payload(bytes.Length) As Byte ' +1 for NUL
        Buffer.BlockCopy(bytes, 0, payload, 0, bytes.Length)
        ' payload(bytes.Length) defaults to 0 → NUL terminator already present.
        WriteRawSubrecord(bw, "EDID", payload)
    End Sub

    ''' <summary>Emit a NON-translatable string subrecord (General/cp1252 encoding). Mirror of xEdit
    ''' wbString cpNormal/cpOverride fields. Use for ATKE/ATKT/DSTA/DMDL — distinct from EmitLString
    ''' which uses Translatable for cpTranslate fields (FULL/SHRT/ATTX).</summary>
    Private Sub EmitGeneralString(bw As BinaryWriter, sig As String, value As String)
        Dim bytes = PluginEncodingSettings.EncodeGeneral(If(value, ""))
        Dim payload(bytes.Length) As Byte
        Buffer.BlockCopy(bytes, 0, payload, 0, bytes.Length)
        WriteRawSubrecord(bw, sig, payload)
    End Sub

    ''' <summary>Emit a VMAD subrecord, patching each FormID at its scanned position through the remapper.
    ''' Widened to Friend (lib-internal) so the ARMO/ARMA override path can reuse this exact logic when
    ''' preserving a source VMAD, instead of duplicating the position-patching loop.
    '''
    ''' <para>Game-agnostic on purpose: the VMAD layout is identical in FO4 and Skyrim (xEdit declares the
    ''' same wbVMAD for both, and every one of the 1333 vanilla NPC_ VMADs measured across Skyrim.esm /
    ''' Dawnguard.esm / Fallout4.esm carries ObjectFormat = 2). No game branch needed here.</para>
    '''
    ''' <para>Refuses to emit a VMAD whose scan desynced (<see cref="NPC_VmadData.ScanComplete"/> = False).
    ''' In that case the FormID position list is PARTIAL, so patching it would rewrite the FormIDs we found
    ''' and leave the rest carrying the SOURCE plugin's master index — pointing them at whatever plugin
    ''' happens to sit at that index in the override's MAST list. That is silent reference corruption of a
    ''' record we do not even own (805 of 5118 Skyrim NPC_ and 382 of 3015 FO4 NPC_ ship vanilla scripts
    ''' like workshopnpcscript / WIDeadBodyCleanupScript). Failing the save is strictly better.</para></summary>
    Friend Sub EmitVmad(bw As BinaryWriter, vmad As NPC_VmadData, remap As FormIdRemapper)
        If vmad Is Nothing OrElse vmad.RawBytes Is Nothing OrElse vmad.RawBytes.Length = 0 Then Return

        If Not vmad.ScanComplete Then
            Throw New InvalidDataException(
                "VMAD could not be fully parsed, so its FormID list is incomplete and the record cannot be " &
                "re-mastered without corrupting the script references it carries. " &
                $"Reason: {If(vmad.ScanFailureReason, "unknown")}. " &
                $"(payload {vmad.RawBytes.Length} bytes, ObjectFormat {vmad.ObjectFormat}, " &
                $"{vmad.ScriptCount} script(s) declared, {vmad.FormIdPositions.Count} FormID(s) found before the failure)")
        End If

        ' Apply FormID re-mapping by patching high bytes in a copy of the raw payload.
        Dim payload = New Byte(vmad.RawBytes.Length - 1) {}
        Buffer.BlockCopy(vmad.RawBytes, 0, payload, 0, vmad.RawBytes.Length)

        For Each ref In vmad.FormIdPositions
            Dim newRaw = remap(ref.ResolvedFormID)
            payload(ref.Offset + 0) = CByte(newRaw And &HFFUI)
            payload(ref.Offset + 1) = CByte((newRaw >> 8) And &HFFUI)
            payload(ref.Offset + 2) = CByte((newRaw >> 16) And &HFFUI)
            payload(ref.Offset + 3) = CByte((newRaw >> 24) And &HFFUI)
        Next

        WriteRawSubrecord(bw, "VMAD", payload)
    End Sub

    Private Sub EmitObnd(bw As BinaryWriter, raw As Byte())
        If raw Is Nothing Then
            ' OBND is required per spec; emit zero-bounds 12-byte payload to keep record valid.
            Dim item2 = New Byte(11) {}
            WriteRawSubrecord(bw, "OBND", item2)
        Else
            WriteRawSubrecord(bw, "OBND", raw)
        End If
    End Sub

    Private Sub EmitFormId(bw As BinaryWriter, sig As String, globalFormID As UInteger, remap As FormIdRemapper)
        Dim newRaw = remap(globalFormID)
        WriteRawSubrecord(bw, sig, BitConverter.GetBytes(newRaw))
    End Sub

    Private Sub EmitFloat(bw As BinaryWriter, sig As String, value As Single)
        WriteRawSubrecord(bw, sig, BitConverter.GetBytes(value))
    End Sub

    Private Sub EmitU32(bw As BinaryWriter, sig As String, value As UInteger)
        WriteRawSubrecord(bw, sig, BitConverter.GetBytes(value))
    End Sub

    Private Sub EmitEmptyMarker(bw As BinaryWriter, sig As String)
        WriteRawSubrecord(bw, sig, Array.Empty(Of Byte)())
    End Sub

    Private Sub EmitLString(bw As BinaryWriter, sig As String, value As String)
        ' NOTE: LString in localized plugins is a 4-byte string ID, not a literal. Our writer is
        ' targeted at non-localized auto-generated plugins (we don't ship .strings tables alongside
        ' generated ESPs), so we always emit the literal string. If/when we support localized
        ' output the caller will need to pass an LString table writer.
        '
        ' Encoding: PluginEncodingSettings.Translatable (mirror of xEdit wbEncodingTrans).
        ' Default UTF-8 for FO4 — see xeInit.pas:1118-1129. ExceptionFallback prevents silent
        ' "?" replacement of non-ASCII characters (e.g. Chinese NPC names).
        Dim bytes = PluginEncodingSettings.EncodeTranslatable(If(value, ""))
        Dim payload(bytes.Length) As Byte
        Buffer.BlockCopy(bytes, 0, payload, 0, bytes.Length)
        WriteRawSubrecord(bw, sig, payload)
    End Sub

    ''' <summary>Emit ACBS. GAME-AWARE layout — FO4 20B (Flags·XP·Level·CalcMin·CalcMax·Disp·TmplFlags·
    ''' Bleedout·Unknown[2]) vs SSE 24B (Flags·Magicka·Stamina·Level·CalcMin·CalcMax·Speed·Disp·TmplFlags·
    ''' Health·Bleedout). The order mirrors RecordParsers.ParseNPC so a parsed ACBS round-trips byte-exact,
    ''' AND an EDITED field (e.g. Template Flags) lands at the engine-correct offset for each game.
    ''' See NPC_AcbsData.</summary>
    Private Sub EmitAcbs(bw As BinaryWriter, acbs As NPC_AcbsData, isSse As Boolean)
        If acbs Is Nothing Then
            ' ACBS is required. Emit a default zero struct (game-correct length) rather than crashing —
            ' the same minimal-validity fallback xEdit uses for required missing structs.
            WriteRawSubrecord(bw, "ACBS", New Byte(If(isSse, 23, 19)) {})
            Return
        End If

        Using ms As New MemoryStream()
            Using w As New BinaryWriter(ms)
                w.Write(acbs.Flags)
                If isSse Then
                    w.Write(acbs.MagickaOffset)
                    w.Write(acbs.StaminaOffset)
                    w.Write(acbs.LevelOrLevelMult)
                    w.Write(acbs.CalcMinLevel)
                    w.Write(acbs.CalcMaxLevel)
                    w.Write(acbs.SpeedMultiplier)
                    w.Write(acbs.DispositionBase)
                    w.Write(acbs.TemplateFlags)
                    w.Write(acbs.HealthOffset)
                    w.Write(acbs.BleedoutOverride)
                Else
                    w.Write(acbs.XpValueOffset)
                    w.Write(acbs.LevelOrLevelMult)
                    w.Write(acbs.CalcMinLevel)
                    w.Write(acbs.CalcMaxLevel)
                    w.Write(acbs.DispositionBase)
                    w.Write(acbs.TemplateFlags)
                    w.Write(acbs.BleedoutOverride)
                    w.Write(acbs.Unknown18)
                End If
                If acbs.TrailingBytes IsNot Nothing AndAlso acbs.TrailingBytes.Length > 0 Then
                    w.Write(acbs.TrailingBytes)
                End If
            End Using
            WriteRawSubrecord(bw, "ACBS", ms.ToArray())
        End Using
    End Sub

    Private Sub EmitFactions(bw As BinaryWriter, factions As List(Of NPC_FactionEntry), remap As FormIdRemapper, isSse As Boolean)
        For Each f In factions
            Using ms As New MemoryStream()
                Using w As New BinaryWriter(ms)
                    w.Write(remap(f.FactionFormID))
                    w.Write(f.Rank)
                    ' FO4 SNAM Faction = 5 bytes (formid + s8 rank). NO trailing padding:
                    ' wbFaction's wbUnused(3) is on the IsFO4Plus(nil, ...) FALSE branch (pre-FO4 only),
                    ' FO4 selects nil. Emitting 3 bytes made xEdit report "Unused data in ... SNAM".
                    ' SSE selects the wbUnused(3) branch → 8 bytes; reproduce the captured trailing.
                    If isSse Then w.Write(If(f.SseUnused, New Byte(2) {}))
                End Using
                WriteRawSubrecord(bw, "SNAM", ms.ToArray())
            End Using
        Next
    End Sub

    Private Sub EmitTpta(bw As BinaryWriter, slots As Dictionary(Of NPC_TemplateCategory, UInteger), remap As FormIdRemapper)
        If slots Is Nothing OrElse slots.Count = 0 Then Return
        Using ms As New MemoryStream()
            Using w As New BinaryWriter(ms)
                For i = 0 To 12
                    Dim cat = CType(i, NPC_TemplateCategory)
                    Dim fid As UInteger = 0UI
                    If slots.TryGetValue(cat, fid) Then
                        w.Write(remap(fid))
                    Else
                        w.Write(0UI)
                    End If
                Next
            End Using
            WriteRawSubrecord(bw, "TPTA", ms.ToArray())
        End Using
    End Sub

    Private Sub EmitSpct(bw As BinaryWriter, npc As NPC_Data)
        If Not npc.HasSpctCounter Then Return
        WriteRawSubrecord(bw, "SPCT", BitConverter.GetBytes(CUInt(npc.ActorEffectFormIDs.Count)))
    End Sub

    Private Sub EmitSplos(bw As BinaryWriter, splos As List(Of UInteger), remap As FormIdRemapper)
        For Each fid In splos
            WriteRawSubrecord(bw, "SPLO", BitConverter.GetBytes(remap(fid)))
        Next
    End Sub

    Private Sub EmitDestruction(bw As BinaryWriter, dest As NPC_DestructionData, remap As FormIdRemapper)
        If dest Is Nothing Then Return

        ' DEST header (8 bytes)
        Using ms As New MemoryStream()
            Using w As New BinaryWriter(ms)
                w.Write(dest.Health)
                w.Write(dest.DestStageCount)
                w.Write(dest.Flags)
                w.Write(If(dest.Unknown6, New Byte(1) {}))
            End Using
            WriteRawSubrecord(bw, "DEST", ms.ToArray())
        End Using

        ' DAMC array (one subrecord with all entries concatenated, stride 8).
        If dest.Resistances.Count > 0 Then
            Using ms As New MemoryStream()
                Using w As New BinaryWriter(ms)
                    For Each r In dest.Resistances
                        w.Write(remap(r.DamageTypeFormID))
                        w.Write(r.Value)
                    Next
                End Using
                WriteRawSubrecord(bw, "DAMC", ms.ToArray())
            End Using
        End If

        ' Stages: each stage = DSTD + optional DSTA/DMDL/DMDT/DMDS/DMDC/DMDF + DSTF marker.
        For Each stage In dest.Stages
            Using ms As New MemoryStream()
                Using w As New BinaryWriter(ms)
                    w.Write(stage.HealthPercent)
                    w.Write(stage.Index)
                    w.Write(stage.ModelDamageStage)
                    w.Write(stage.Flags)
                    w.Write(stage.SelfDamagePerSecond)
                    w.Write(remap(stage.ExplosionFormID))
                    w.Write(remap(stage.DebrisFormID))
                    w.Write(stage.DebrisCount)
                End Using
                WriteRawSubrecord(bw, "DSTD", ms.ToArray())
            End Using
            ' DSTA/DMDL are wbString cpNormal (wbDefinitionsFO4.pas:4682,4684) → non-translatable → General.
            If stage.SequenceName <> "" Then EmitGeneralString(bw, "DSTA", stage.SequenceName)
            If stage.ModelFilename <> "" Then EmitGeneralString(bw, "DMDL", stage.ModelFilename)
            If stage.ModelTextureData IsNot Nothing AndAlso stage.ModelTextureData.Length > 0 Then
                WriteRawSubrecord(bw, "DMDT", stage.ModelTextureData)
            End If
            If stage.MaterialSwapFormID <> 0UI Then EmitFormId(bw, "DMDS", stage.MaterialSwapFormID, remap)
            If stage.HasColorRemappingIndex Then EmitFloat(bw, "DMDC", stage.ColorRemappingIndex)
            If stage.HasModelFlags Then WriteRawSubrecord(bw, "DMDF", New Byte() {stage.ModelFlags})
            EmitEmptyMarker(bw, "DSTF")
        Next
    End Sub

    Private Sub EmitAttacks(bw As BinaryWriter, attacks As List(Of NPC_AttackData), remap As FormIdRemapper)
        For Each a In attacks
            Using ms As New MemoryStream()
                Using w As New BinaryWriter(ms)
                    w.Write(a.DamageMult)
                    w.Write(a.AttackChance)
                    w.Write(remap(a.AttackSpellFormID))
                    w.Write(a.AttackFlags)
                    w.Write(a.AttackAngle)
                    w.Write(a.StrikeAngle)
                    w.Write(a.Stagger)
                    w.Write(a.Knockdown)
                    w.Write(a.RecoveryTime)
                    w.Write(a.ActionPointsMult)
                    w.Write(a.StaggerOffset)
                End Using
                WriteRawSubrecord(bw, "ATKD", ms.ToArray())
            End Using
            ' ATKE/ATKT are wbString cpNormal (wbDefinitionsFO4.pas:4487,4490) → non-translatable → General.
            EmitGeneralString(bw, "ATKE", a.AttackEvent)
            If a.HasWeaponSlot Then EmitFormId(bw, "ATKW", a.WeaponSlotFormID, remap)
            If a.HasRequiredSlot Then EmitFormId(bw, "ATKS", a.RequiredSlotFormID, remap)
            If a.HasDescription Then EmitGeneralString(bw, "ATKT", a.Description)
        Next
    End Sub

    Private Sub EmitPrkz(bw As BinaryWriter, npc As NPC_Data)
        If Not npc.HasPrkzCounter Then Return
        WriteRawSubrecord(bw, "PRKZ", BitConverter.GetBytes(CUInt(npc.Perks.Count)))
    End Sub

    Private Sub EmitPerks(bw As BinaryWriter, perks As List(Of NPC_PerkEntry), remap As FormIdRemapper, isSse As Boolean)
        For Each p In perks
            Using ms As New MemoryStream()
                Using w As New BinaryWriter(ms)
                    w.Write(remap(p.PerkFormID))
                    w.Write(p.Rank)
                    ' FO4 PRKR Perk = 5 bytes (formid + u8 rank). NO trailing padding — the FO4
                    ' wbStruct (wbDefinitionsFO4.pas:10717-10719) has no wbUnused. Emitting 3 bytes
                    ' made xEdit report "Unused data in ... PRKR".
                    ' SSE PRKR = 8 bytes; the 3 trailing bytes are often non-zero → reproduce verbatim.
                    If isSse Then w.Write(If(p.SseUnused, New Byte(2) {}))
                End Using
                WriteRawSubrecord(bw, "PRKR", ms.ToArray())
            End Using
        Next
    End Sub

    Private Sub EmitProperties(bw As BinaryWriter, props As List(Of NPC_PropertyEntry), remap As FormIdRemapper)
        If props.Count = 0 Then Return
        Using ms As New MemoryStream()
            Using w As New BinaryWriter(ms)
                For Each p In props
                    w.Write(remap(p.ActorValueFormID))
                    w.Write(p.Value)
                Next
            End Using
            WriteRawSubrecord(bw, "PRPS", ms.ToArray())
        End Using
    End Sub

    Private Sub EmitCoct(bw As BinaryWriter, npc As NPC_Data)
        If Not npc.HasCoctCounter Then Return
        WriteRawSubrecord(bw, "COCT", BitConverter.GetBytes(CUInt(npc.Inventory.Count)))
    End Sub

    Private Sub EmitInventory(bw As BinaryWriter, items As List(Of NPC_InventoryItem), remap As FormIdRemapper)
        For Each item In items
            ' CNTO (8 bytes)
            Using ms As New MemoryStream()
                Using w As New BinaryWriter(ms)
                    w.Write(remap(item.ItemFormID))
                    w.Write(item.Count)
                End Using
                WriteRawSubrecord(bw, "CNTO", ms.ToArray())
            End Using
            ' COED (12 bytes, optional)
            If item.HasCoed Then
                Using ms As New MemoryStream()
                    Using w As New BinaryWriter(ms)
                        w.Write(remap(item.CoedOwnerFormID))
                        ' CoedOwnerExtra is a union per wbCOEDOwnerDecider (wbDefinitionsCommon.pas:4103):
                        ' Owner=NPC_ → GLOB FormID (must be remapped — parser already resolved to GLOBAL).
                        ' Owner=FACT → Required Rank s32. Owner=NULL/unresolved → unused 4 bytes.
                        ' CoedExtraIsFormID is the parser's verdict; emit through the remapper iff set.
                        If item.CoedExtraIsFormID Then
                            w.Write(remap(item.CoedOwnerExtra))
                        Else
                            w.Write(item.CoedOwnerExtra)
                        End If
                        w.Write(item.CoedItemCondition)
                    End Using
                    WriteRawSubrecord(bw, "COED", ms.ToArray())
                End Using
            End If
        Next
    End Sub

    Private Sub EmitAidt(bw As BinaryWriter, ai As NPC_AiData)
        If ai Is Nothing Then Return
        Using ms As New MemoryStream()
            Using w As New BinaryWriter(ms)
                w.Write(ai.Aggression)
                w.Write(ai.Confidence)
                w.Write(ai.EnergyLevel)
                w.Write(ai.Morality)
                w.Write(ai.Mood)
                w.Write(ai.Assistance)
                w.Write(ai.AggroRadiusBehavior)
                w.Write(ai.AggroUnknown7)
                w.Write(ai.WarnRadius)
                w.Write(ai.WarnAttackRadius)
                w.Write(ai.AttackRadius)
                If ai.HasV29Fields Then
                    w.Write(ai.NoSlowApproach)
                    w.Write(If(ai.Unknown21, New Byte(2) {}))
                End If
            End Using
            WriteRawSubrecord(bw, "AIDT", ms.ToArray())
        End Using
    End Sub

    Private Sub EmitPkid(bw As BinaryWriter, packs As List(Of UInteger), remap As FormIdRemapper)
        For Each fid In packs
            WriteRawSubrecord(bw, "PKID", BitConverter.GetBytes(remap(fid)))
        Next
    End Sub

    Private Sub EmitKeywords(bw As BinaryWriter, npc As NPC_Data, remap As FormIdRemapper)
        If Not npc.HasKsizCounter AndAlso npc.KeywordFormIDs.Count = 0 Then Return
        WriteRawSubrecord(bw, "KSIZ", BitConverter.GetBytes(CUInt(npc.KeywordFormIDs.Count)))
        If npc.KeywordFormIDs.Count > 0 Then
            Using ms As New MemoryStream()
                Using w As New BinaryWriter(ms)
                    For Each fid In npc.KeywordFormIDs
                        ' KWDA FormIDs must be remapped against the new MAST list. Keywords from
                        ' DLCs/mods have non-zero high bytes that change when the master ordering
                        ' changes — failing to remap leaves them pointing at the wrong master.
                        w.Write(remap(fid))
                    Next
                End Using
                WriteRawSubrecord(bw, "KWDA", ms.ToArray())
            End Using
        End If
    End Sub

    Private Sub EmitAppr(bw As BinaryWriter, slots As List(Of UInteger), remap As FormIdRemapper)
        If slots.Count = 0 Then Return
        Using ms As New MemoryStream()
            Using w As New BinaryWriter(ms)
                For Each fid In slots
                    w.Write(remap(fid))
                Next
            End Using
            WriteRawSubrecord(bw, "APPR", ms.ToArray())
        End Using
    End Sub

    Private Sub EmitObjectTemplate(bw As BinaryWriter, npc As NPC_Data, remap As FormIdRemapper)
        If Not npc.HasObjectTemplate Then Return

        WriteRawSubrecord(bw, "OBTE", BitConverter.GetBytes(CUInt(npc.ObjectTemplateCombinations.Count)))
        For Each combo In npc.ObjectTemplateCombinations
            If combo.IsEditorOnly Then EmitEmptyMarker(bw, "OBTF")
            If combo.DisplayName <> "" Then EmitLString(bw, "FULL", combo.DisplayName)
            ' OBTS payload — two paths (mirror of the ARMO EmitArmoObjectTemplate contract):
            '   • UNEDITED (RawObtsBytes present): preserve the source bytes verbatim and only patch the
            '     master-bound FormIDs (Includes OMOD list, Keywords, FormID-typed Property Value1) at their
            '     known offsets — keeps the round-trip byte-exact for combos the user never touched.
            '   • EDITED / NEW (RawObtsBytes cleared to Nothing by the editor on commit): reconstruct the whole
            '     payload from the structured model via BuildObtsPayload — the same authoring serializer the ARMO
            '     editor uses. Without this branch an edited/new combination would emit NO OBTS at all.
            If combo.RawObtsBytes IsNot Nothing Then
                WriteRawSubrecord(bw, "OBTS", ApplyObtsRemap(combo, remap))
            ElseIf combo.Combination IsNot Nothing Then
                WriteRawSubrecord(bw, "OBTS", BuildObtsPayload(combo.Combination, remap))
            End If
        Next
        EmitEmptyMarker(bw, "STOP")
    End Sub

    ''' <summary>Re-emit OBTS bytes with FormIDs remapped for the new MAST list.
    ''' Layout per ParseOBTSPayload (RecordParsers.vb:1563, wbDefinitionsFO4.pas:5867):
    '''   u32 IncludeCount @0, u32 PropertyCount @4, u8 LevelMin @8, u8 pad, u8 LevelMax @10, u8 pad,
    '''   s16 ParentCombinationIndex @12, u8 Default @14, u8 KeywordCount @15,
    '''   KeywordCount × u32 @16+, then u8 MinLevelForRanks + u8 AltLevelsPerTier,
    '''   IncludeCount × 7 bytes (u32 OMOD FormID + u8 attach + u8 optional + u8 dontUseAll),
    '''   PropertyCount × 24 bytes (Properties, layout per wbObjectModProperties wbDefinitionsFO4.pas:5826):
    '''     u8 ValueType @0, 3 unused, u8 FunctionType @4, 3 unused, u16 Property @8, 2 unused,
    '''     4-byte Value1 @12 (FormID when ValueType is FormIDInt=4 or FormIDFloat=6, else float/int),
    '''     4-byte Value2 @16, float Step @20. The Value1 FormID slot is patched here (parser already
    '''     resolved it to GLOBAL into combo.Combination.Properties(i).Value1FormID).</summary>
    Private Function ApplyObtsRemap(combo As NPC_ObjectTemplateCombination, remap As FormIdRemapper) As Byte()
        Dim raw = combo.RawObtsBytes
        Dim payload = New Byte(raw.Length - 1) {}
        Buffer.BlockCopy(raw, 0, payload, 0, raw.Length)
        If raw.Length < 17 OrElse combo.Combination Is Nothing Then Return payload

        Dim includeCount = BitConverter.ToUInt32(payload, 0)
        Dim offset As Integer = 15
        Dim kwCount As Integer = CInt(payload(offset))
        offset += 1
        ' Re-map each keyword. We resolve its global FormID by re-reading the raw u32 + the
        ' source plugin's MAST list, NOT by indexing combo.Combination.Keywords (which can have
        ' a different count from the raw if the parser dropped 0-FormIDs). This guarantees we
        ' patch the actual FormID stored at each offset, regardless of parser-side filtering.
        For i = 0 To kwCount - 1
            If offset + 4 > payload.Length Then Exit For
            Dim rawKw = BitConverter.ToUInt32(payload, offset)
            Dim newRaw As UInteger = 0UI
            If rawKw <> 0UI Then
                ' raw is the "source-plugin local" FormID; we need to resolve to global before
                ' the remapper, but ApplyObtsRemap doesn't have a PluginManager. The combo's
                ' Keywords list is the resolved-global view captured at parse time. We match by
                ' position when counts agree, fall back to remap(rawKw) treating it as already
                ' global when counts disagree (defensive for malformed inputs).
                If kwCount = combo.Combination.Keywords.Count AndAlso i < combo.Combination.Keywords.Count Then
                    newRaw = remap(combo.Combination.Keywords(i))
                Else
                    newRaw = remap(rawKw)
                End If
            End If
            payload(offset + 0) = CByte(newRaw And &HFFUI)
            payload(offset + 1) = CByte((newRaw >> 8) And &HFFUI)
            payload(offset + 2) = CByte((newRaw >> 16) And &HFFUI)
            payload(offset + 3) = CByte((newRaw >> 24) And &HFFUI)
            offset += 4
        Next
        offset += 2 ' MinLevelForRanks + AltLevelsPerTier
        For i = 0 To CInt(includeCount) - 1
            If offset + 7 > payload.Length Then Exit For
            Dim rawModFID = BitConverter.ToUInt32(payload, offset)
            Dim newRaw As UInteger = 0UI
            If rawModFID <> 0UI Then
                If CInt(includeCount) = combo.Combination.IncludeOMODFormIDs.Count AndAlso i < combo.Combination.IncludeOMODFormIDs.Count Then
                    newRaw = remap(combo.Combination.IncludeOMODFormIDs(i))
                Else
                    newRaw = remap(rawModFID)
                End If
            End If
            payload(offset + 0) = CByte(newRaw And &HFFUI)
            payload(offset + 1) = CByte((newRaw >> 8) And &HFFUI)
            payload(offset + 2) = CByte((newRaw >> 16) And &HFFUI)
            payload(offset + 3) = CByte((newRaw >> 24) And &HFFUI)
            offset += 7
        Next

        ' Properties: 24 bytes each. The Value1 slot (bytes 12-15 within each entry) is a FormID
        ' when ValueType (byte 0) is FormIDInt(4) or FormIDFloat(6) — per wbObjectModProperties
        ' (wbDefinitionsFO4.pas:5826-5865). Patch each FormID-typed Value1 from the parser's
        ' resolved-global view in combo.Combination.Properties(i).Value1FormID, mirroring the
        ' Keywords/Includes pattern above. Other ValueTypes leave the 4 bytes untouched (they're
        ' float/int payload, not master-bound).
        Dim propertyCount = BitConverter.ToUInt32(raw, 4)
        Const propertyEntrySize As Integer = 24
        For i = 0 To CInt(propertyCount) - 1
            If offset + propertyEntrySize > payload.Length Then Exit For
            Dim valueType As Byte = payload(offset)
            If valueType = CByte(OMOD_ValueType.FormIDInt) OrElse valueType = CByte(OMOD_ValueType.FormIDFloat) Then
                Dim formIdOffset = offset + 12
                Dim rawValue1 = BitConverter.ToUInt32(payload, formIdOffset)
                Dim newRaw As UInteger = 0UI
                If rawValue1 <> 0UI Then
                    If CInt(propertyCount) = combo.Combination.Properties.Count AndAlso i < combo.Combination.Properties.Count Then
                        newRaw = remap(combo.Combination.Properties(i).Value1FormID)
                    Else
                        newRaw = remap(rawValue1)
                    End If
                End If
                payload(formIdOffset + 0) = CByte(newRaw And &HFFUI)
                payload(formIdOffset + 1) = CByte((newRaw >> 8) And &HFFUI)
                payload(formIdOffset + 2) = CByte((newRaw >> 16) And &HFFUI)
                payload(formIdOffset + 3) = CByte((newRaw >> 24) And &HFFUI)
            End If
            offset += propertyEntrySize
        Next

        Return payload
    End Function

    ''' <summary>Build an OBTS payload buffer FROM THE STRUCTURED MODEL — the inverse of
    ''' <see cref="RecordParsers.ParseOBTSPayload"/> (RecordParsers.vb:1763, wbDefinitionsFO4.pas:5867-5886).
    ''' Unlike <see cref="ApplyObtsRemap"/> (which patches preserved raw bytes) this reconstructs the whole
    ''' payload from the parsed <see cref="ARMO_Combination"/>, so it is the authoring path for NEW records
    ''' that have no source bytes. Counts (IncludeCount / PropertyCount / KeywordCount) are derived from the
    ''' model lists; all padding/unused bytes are written as 0. FormIDs (keywords, include OMODs, and each
    ''' Property's Value1 when it is a FormID type) are routed through <paramref name="remap"/>.
    ''' Layout (offsets verified against ParseOBTSPayload):
    '''   u32 IncludeCount @0, u32 PropertyCount @4, u8 LevelMin @8, u8 pad, u8 LevelMax @10, u8 pad,
    '''   s16 ParentCombinationIndex @12, u8 Default @14, u8 KeywordCount @15 (wbArray(..., -4) 1-byte prefix),
    '''   KeywordCount × u32, u8 MinLevelForRanks, u8 AltLevelsPerTier,
    '''   IncludeCount × 7 bytes (u32 Mod FormID + u8 AttachPointIndex + u8 Optional + u8 DontUseAll),
    '''   PropertyCount × 24 bytes (wbObjectModProperties, wbDefinitionsFO4.pas:5826-5865).</summary>
    Friend Function BuildObtsPayload(combo As ARMO_Combination, remap As FormIdRemapper) As Byte()
        Using ms As New MemoryStream()
            Using w As New BinaryWriter(ms)
                w.Write(CUInt(combo.Includes.Count))                        ' @0  IncludeCount
                w.Write(CUInt(combo.Properties.Count))                      ' @4  PropertyCount
                w.Write(combo.LevelMin)                                     ' @8  LevelMin
                w.Write(CByte(0))                                           ' @9  pad
                w.Write(combo.LevelMax)                                     ' @10 LevelMax
                w.Write(CByte(0))                                           ' @11 pad
                w.Write(CShort(combo.ParentCombinationIndex))               ' @12 s16 Parent Combination Index
                w.Write(If(combo.IsDefault, CByte(1), CByte(0)))            ' @14 Default
                w.Write(CByte(combo.Keywords.Count))                        ' @15 KeywordCount (u8 prefix)
                For Each kw In combo.Keywords
                    w.Write(remap(kw))
                Next
                w.Write(combo.MinLevelForRanks)                            ' u8 Min Level For Ranks
                w.Write(combo.AltLevelsPerTier)                           ' u8 Alt Levels Per Tier
                For Each inc In combo.Includes
                    w.Write(remap(inc.ModFormID))                          ' u32 Mod FormID
                    w.Write(inc.AttachPointIndex)                          ' u8  Attach Point Index
                    w.Write(If(inc.IsOptional, CByte(1), CByte(0)))        ' u8  Optional
                    w.Write(If(inc.DontUseAll, CByte(1), CByte(0)))        ' u8  Don't Use All
                Next
                For Each prop In combo.Properties
                    WriteObtsProperty(w, prop, remap)
                Next
            End Using
            Return ms.ToArray()
        End Using
    End Function

    ''' <summary>Write one 24-byte OMOD Property entry (wbObjectModProperties, wbDefinitionsFO4.pas:5826-5865)
    ''' from the model. Layout: u8 ValueType @0 (+3 unused), u8 FunctionType @4 (+3 unused), u16 PropertyIndex
    ''' @8 (+2 unused), 4B Value1 @12, 4B Value2 @16, float Step @20. Value1 is a remapped FormID when
    ''' ValueType is FormIDInt(4)/FormIDFloat(6) — the parser resolved it into <c>Value1FormID</c>; otherwise
    ''' the raw 4-byte float/int bits captured in <c>Value1</c> are written verbatim.</summary>
    Private Sub WriteObtsProperty(w As BinaryWriter, prop As OMOD_Property, remap As FormIdRemapper)
        w.Write(CByte(prop.ValueType))            ' @0  ValueType
        w.Write(CByte(0)) : w.Write(CByte(0)) : w.Write(CByte(0))   ' @1..3 unused
        w.Write(prop.FunctionType)                ' @4  FunctionType
        w.Write(CByte(0)) : w.Write(CByte(0)) : w.Write(CByte(0))   ' @5..7 unused
        w.Write(prop.PropertyIndex)               ' @8  PropertyIndex (u16)
        w.Write(CByte(0)) : w.Write(CByte(0))     ' @10..11 unused
        If prop.ValueType = OMOD_ValueType.FormIDInt OrElse prop.ValueType = OMOD_ValueType.FormIDFloat Then
            w.Write(remap(prop.Value1FormID))     ' @12 Value1 = FormID
        Else
            w.Write(prop.Value1)                  ' @12 Value1 = raw float/int bits (Single, 4 bytes)
        End If
        w.Write(prop.Value2)                      ' @16 Value2
        w.Write(prop.StepValue)                   ' @20 Step
    End Sub

    ''' <summary>Emit the ARMO/NPC_ Object Template block FROM THE MODEL: OBTE(u32 count) → per combination
    ''' [OBTF if IsEditorOnly][FULL if DisplayName][OBTS = <see cref="BuildObtsPayload"/>] → STOP. Mirror of
    ''' <see cref="EmitObjectTemplate"/> (the NPC_ path) but sourced from a plain combination list so the ARMO
    ''' new-record writer can reuse the exact same block structure. No-op for an empty list (record simply
    ''' carries no OBTE/STOP). wbDefinitionsFO4.pas:5888-5898.</summary>
    Friend Sub EmitArmoObjectTemplate(bw As BinaryWriter, combos As List(Of ARMO_Combination), remap As FormIdRemapper)
        If combos Is Nothing OrElse combos.Count = 0 Then Return
        WriteRawSubrecord(bw, "OBTE", BitConverter.GetBytes(CUInt(combos.Count)))
        For Each combo In combos
            If combo.IsEditorOnly Then EmitEmptyMarker(bw, "OBTF")
            If combo.DisplayName <> "" Then EmitLString(bw, "FULL", combo.DisplayName)
            WriteRawSubrecord(bw, "OBTS", BuildObtsPayload(combo, remap))
        Next
        EmitEmptyMarker(bw, "STOP")
    End Sub

    Private Sub EmitDnam(bw As BinaryWriter, npc As NPC_Data)
        ' SSE DNAM = 52-byte Player Skills block (wbDefinitionsTES5.pas:8707-8756) — a different subrecord
        ' from FO4's 8-byte Calculated Stats. Emitted field-by-field from the parsed struct (its unused runs
        ' and trailing bytes are verbatim captures, so an unedited record is byte-identical); the raw payload
        ' is the fallback for a malformed block too short to model.
        If npc.Game = Config_App.Game_Enum.Skyrim Then
            Dim ps = npc.SsePlayerSkills
            If ps Is Nothing Then
                If npc.DnamRawSse IsNot Nothing Then WriteRawSubrecord(bw, "DNAM", npc.DnamRawSse)
                Return
            End If
            Using ms As New MemoryStream()
                Using w As New BinaryWriter(ms)
                    w.Write(ps.SkillValues, 0, NPC_SsePlayerSkills.SkillCount)
                    w.Write(ps.SkillOffsets, 0, NPC_SsePlayerSkills.SkillCount)
                    w.Write(ps.Health)
                    w.Write(ps.Magicka)
                    w.Write(ps.Stamina)
                    w.Write(ps.Unused42, 0, 2)
                    w.Write(ps.FarAwayModelDistance)
                    w.Write(ps.GearedUpWeapons)
                    w.Write(ps.Unused49, 0, 3)
                    If ps.TrailingBytes IsNot Nothing AndAlso ps.TrailingBytes.Length > 0 Then w.Write(ps.TrailingBytes)
                End Using
                WriteRawSubrecord(bw, "DNAM", ms.ToArray())
            End Using
            Return
        End If
        Dim stats = npc.CalculatedStats
        If stats Is Nothing Then Return
        Using ms As New MemoryStream()
            Using w As New BinaryWriter(ms)
                w.Write(stats.CalculatedHealth)
                w.Write(stats.CalculatedActionPoints)
                w.Write(stats.FarAwayModelDistance)
                w.Write(stats.GearedUpWeapons)
                w.Write(stats.Unused7)
            End Using
            WriteRawSubrecord(bw, "DNAM", ms.ToArray())
        End Using
    End Sub

    Private Sub EmitHeadParts(bw As BinaryWriter, parts As List(Of UInteger), remap As FormIdRemapper)
        For Each fid In parts
            WriteRawSubrecord(bw, "PNAM", BitConverter.GetBytes(remap(fid)))
        Next
    End Sub

    Private Sub EmitNam5(bw As BinaryWriter, raw As Byte())
        ' NAM5 is required (cpNormal, True) per spec but content is unknown. Preserve verbatim.
        ' If the parser saw it absent, skip emission — but spec says it's required, so this is
        ' a trade-off: emit empty zero-byte payload as last-resort fallback, which xEdit accepts.
        If raw Is Nothing Then Return
        WriteRawSubrecord(bw, "NAM5", raw)
    End Sub

    Private Sub EmitNam7(bw As BinaryWriter, raw As Byte())
        If raw Is Nothing Then Return
        WriteRawSubrecord(bw, "NAM7", raw)
    End Sub

    Private Sub EmitMwgt(bw As BinaryWriter, npc As NPC_Data)
        If Not npc.HasMwgt Then Return
        ' Source of truth for byte-equivalent output is MwgtRaw (preserves Single.MaxValue
        ' sentinel encoding). The Nullable view (WeightThin/Muscular/Fat) is for resolvers.
        If npc.MwgtRaw IsNot Nothing AndAlso npc.MwgtRaw.Length = 12 Then
            WriteRawSubrecord(bw, "MWGT", npc.MwgtRaw)
            Return
        End If
        ' Fallback: rebuild from Nullable view if raw not preserved (e.g. value mutated by editor).
        Using ms As New MemoryStream()
            Using w As New BinaryWriter(ms)
                w.Write(If(npc.WeightThin, Single.MaxValue))
                w.Write(If(npc.WeightMuscular, Single.MaxValue))
                w.Write(If(npc.WeightFat, Single.MaxValue))
            End Using
            WriteRawSubrecord(bw, "MWGT", ms.ToArray())
        End Using
    End Sub

    Private Sub EmitActorSounds(bw As BinaryWriter, npc As NPC_Data, remap As FormIdRemapper)
        ' SSE actor sounds = CSDT/CSDI/CSDC (wbCSDT TES5:4751), NOT the FO4 CS2* block. Re-emit the
        ' captured raw sequence in source order; CSDI is a sound FormID (remapped), CSDT/CSDC verbatim.
        If npc.Game = Config_App.Game_Enum.Skyrim Then
            For Each s In npc.SseActorSounds
                If s.IsFormId AndAlso s.Data IsNot Nothing AndAlso s.Data.Length >= 4 Then
                    WriteRawSubrecord(bw, s.Sig, BitConverter.GetBytes(remap(BitConverter.ToUInt32(s.Data, 0))))
                Else
                    WriteRawSubrecord(bw, s.Sig, s.Data)
                End If
            Next
            Return
        End If
        ' Actor Sounds is a structured RArrayS (wbDefinitionsCommon.pas:7117): if the source
        ' record had no actor sounds at all, emit none of CS2H/CS2K/CS2D/CS2E/CS2F. The block
        ' is "all-or-nothing" — CS2F alone without CS2H is invalid. xEdit's RArrayS with no
        ' elements simply doesn't emit any of the wrapping subrecords (wbImplementation.pas
        ' WriteToStream skips dcfDontSave / unused groups).
        If Not npc.HasCs2hCounter AndAlso npc.ActorSounds.Count = 0 AndAlso Not npc.HasCs2eMarker Then Return
        WriteRawSubrecord(bw, "CS2H", BitConverter.GetBytes(CUInt(npc.ActorSounds.Count)))
        For Each s In npc.ActorSounds
            ' CS2K (Keyword) is optional — only re-emit it when the source had one. Vanilla
            ' AudioTemplate/UnarmedWeapon NPCs carry a bare CS2D; emitting CS2K(0) for them added a
            ' spurious subrecord (round-trip data corruption).
            If s.HasKeyword Then WriteRawSubrecord(bw, "CS2K", BitConverter.GetBytes(remap(s.KeywordFormID)))
            WriteRawSubrecord(bw, "CS2D", BitConverter.GetBytes(remap(s.SoundFormID)))
        Next
        If npc.HasCs2eMarker Then EmitEmptyMarker(bw, "CS2E")
        ' CS2F is the trailing 1-byte "Finalize" marker. Only emit when the block was present
        ' in the source (HasCs2hCounter or HasCs2eMarker = True). Without CS2H/CS2E preceding,
        ' CS2F by itself is an invalid record fragment.
        WriteRawSubrecord(bw, "CS2F", New Byte() {npc.Cs2fByte})
    End Sub

    Private Sub EmitQnam(bw As BinaryWriter, q As NPC_TextureLightingFloats, isSse As Boolean)
        If q Is Nothing Then Return
        Using ms As New MemoryStream()
            Using w As New BinaryWriter(ms)
                w.Write(q.R)
                w.Write(q.G)
                w.Write(q.B)
                ' FO4 QNAM = 4 floats RGBA (16 bytes); SSE QNAM = 3 floats RGB (12 bytes, no alpha).
                If Not isSse Then w.Write(q.A)
            End Using
            WriteRawSubrecord(bw, "QNAM", ms.ToArray())
        End Using
    End Sub

    Private Sub EmitMsdkMsdv(bw As BinaryWriter, npc As NPC_Data)
        If npc.MorphValues.Count = 0 Then Return
        ' MorphKeysOrdered preserves the original order; if missing (legacy data), fall back to
        ' dict iteration order (non-deterministic but functional).
        Dim keys = If(npc.MorphKeysOrdered.Count = npc.MorphValues.Count, npc.MorphKeysOrdered, npc.MorphValues.Keys.ToList())
        ' MSDK
        Using ms As New MemoryStream()
            Using w As New BinaryWriter(ms)
                For Each k In keys
                    w.Write(k)
                Next
            End Using
            WriteRawSubrecord(bw, "MSDK", ms.ToArray())
        End Using
        ' MSDV (parallel array, same order as MSDK)
        Using ms As New MemoryStream()
            Using w As New BinaryWriter(ms)
                For Each k In keys
                    Dim v As Single = 0.0F
                    If npc.MorphValues.TryGetValue(k, v) Then w.Write(v) Else w.Write(0.0F)
                Next
            End Using
            WriteRawSubrecord(bw, "MSDV", ms.ToArray())
        End Using
    End Sub

    Private Sub EmitTintLayers(bw As BinaryWriter, npc As NPC_Data)
        ' Source of truth for round-trip is TintLayerStructs (parsed fields). The TEND payload
        ' length is variable in vanilla files: TextureSet layers (TETI Discriminator=2) emit
        ' just 1 byte (the Value), Palette layers (Discriminator=1) emit 8 bytes (full struct).
        ' xEdit reads the struct tolerantly — fields beyond the actual payload show as 0 — and
        ' writes only the bytes that were stored. To stay byte-equivalent vs vanilla we honor
        ' the original TEND length captured by the parser in FaceTintLayers(i).RawTendBytes.
        If npc.TintLayerStructs.Count > 0 Then
            For i = 0 To npc.TintLayerStructs.Count - 1
                Dim pair = npc.TintLayerStructs(i)
                Dim teti = pair.Teti
                Dim tend = pair.Tend
                ' TETI: u16 DataType + u16 Index = 4 bytes (always full struct)
                Using ms As New MemoryStream()
                    Using w As New BinaryWriter(ms)
                        w.Write(teti.DataType)
                        w.Write(teti.Index)
                    End Using
                    WriteRawSubrecord(bw, "TETI", ms.ToArray())
                End Using
                ' TEND length follows xEdit's aOptionalFromElement=1 rule
                ' (wbDefinitionsFO4.pas:10786-10790, wbImplementation.pas:22356-22389):
                '   1 byte  → Value only
                '   5 bytes → Value + Color (R+G+B+padding)
                '   7 bytes → Value + Color + TemplateColorIndex
                ' Decide which based on which optional members are present in the struct.
                Using ms As New MemoryStream()
                    Using w As New BinaryWriter(ms)
                        w.Write(tend.RawValue)
                        If tend.HasColor Then
                            w.Write(tend.ColorR)
                            w.Write(tend.ColorG)
                            w.Write(tend.ColorB)
                            w.Write(tend.ColorPad)  ' wbUnused(1) inside wbByteColors
                        End If
                        If tend.HasTemplateColorIndex Then
                            w.Write(tend.TemplateColorIndex)
                        End If
                    End Using
                    WriteRawSubrecord(bw, "TEND", ms.ToArray())
                End Using
            Next
        End If
    End Sub

    Private Sub EmitMrsv(bw As BinaryWriter, values As List(Of Single))
        If values Is Nothing OrElse values.Count = 0 Then Return
        Using ms As New MemoryStream()
            Using w As New BinaryWriter(ms)
                For Each v In values
                    w.Write(v)
                Next
            End Using
            WriteRawSubrecord(bw, "MRSV", ms.ToArray())
        End Using
    End Sub

    Private Sub EmitFaceMorphs(bw As BinaryWriter, npc As NPC_Data)
        For i = 0 To npc.FaceMorphs.Count - 1
            Dim fm = npc.FaceMorphs(i)
            ' FMRI: 4 bytes u32 index
            WriteRawSubrecord(bw, "FMRI", BitConverter.GetBytes(fm.Index))
            ' FMRS: 7 floats + variable trailing bytes
            Using ms As New MemoryStream()
                Using w As New BinaryWriter(ms)
                    Dim valuesToWrite = Math.Min(fm.Values.Count, 7)
                    For j = 0 To valuesToWrite - 1
                        w.Write(fm.Values(j))
                    Next
                    For j = valuesToWrite To 6
                        w.Write(0.0F)
                    Next
                    Dim trailing = If(i < npc.FaceMorphTrailingBytes.Count, npc.FaceMorphTrailingBytes(i), Array.Empty(Of Byte)())
                    If trailing.Length > 0 Then w.Write(trailing)
                End Using
                WriteRawSubrecord(bw, "FMRS", ms.ToArray())
            End Using
        Next
    End Sub

    ''' <summary>SSE face-data tail emitted after QNAM: NAM9 (19 face sliders) + NAMA (4 face-parts) +
    ''' the tint RArrayS (TINI/TINC/TINV/TIAS, no FormIDs). All preserved verbatim from the parser's
    ''' SSE captures. No-op for FO4 data (Nam9Raw/NamaRaw Nothing, tint list empty).</summary>
    Private Sub EmitSseFaceTail(bw As BinaryWriter, npc As NPC_Data)
        If npc.Nam9Raw IsNot Nothing Then WriteRawSubrecord(bw, "NAM9", npc.Nam9Raw)
        If npc.NamaRaw IsNot Nothing Then WriteRawSubrecord(bw, "NAMA", npc.NamaRaw)
        For Each t In npc.SseTintRaw
            WriteRawSubrecord(bw, t.Sig, t.Data)
        Next
    End Sub

End Module
