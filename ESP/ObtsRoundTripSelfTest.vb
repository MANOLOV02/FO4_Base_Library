Imports System.IO
Imports System.Text

''' <summary>Correctness gate for the OBTS authoring foundation (Phase 1). Exercises the round-trip
''' ParseOBTSPayload(bytes) → BuildObtsPayload(combo, identityRemap) → ParseOBTSPayload(rebuilt) over
''' REAL vanilla ARMO records that carry an Object Template (OBTE/OBTS). Lives in the library assembly so
''' it can drive the Friend ParseOBTSPayload / BuildObtsPayload directly; invoked by a thin console harness.
'''
''' Two comparisons per OBTS payload:
'''   • STRUCTURAL — the guaranteed invariant: every field the parser extracts must survive the rebuild
'''     (counts, level fields, ParentCombinationIndex, IsDefault, Keywords, Includes, Properties).
'''   • BYTE-EXACT — raw source bytes vs rebuilt bytes. Can legitimately differ ONLY in "unused"/pad bytes
'''     that the parser ignores (BuildObtsPayload writes 0 there); those are reported but do NOT fail the gate.</summary>
Public Module ObtsRoundTripSelfTest

    ''' <summary>Identity remapper: authoring with no MAST relocation. FormIDs must survive verbatim.</summary>
    Private ReadOnly IdentityRemap As NpcSubrecordWriter.FormIdRemapper = Function(f) f

    Public Class ObtsRoundTripReport
        Public ArmoRecordsWithObts As Integer
        Public ObtsPayloadsTested As Integer
        Public StructuralPass As Integer
        Public StructuralFail As Integer
        Public ByteExact As Integer
        Public ByteDiffInPadOnly As Integer
        Public ByteDiffStructural As Integer
        Public Lines As New List(Of String)
        Public ReadOnly Property AllPass As Boolean
            Get
                Return StructuralFail = 0 AndAlso ByteDiffStructural = 0
            End Get
        End Property
    End Class

    Public Function Run(esmPath As String) As ObtsRoundTripReport
        Dim report As New ObtsRoundTripReport()
        Dim reader As New PluginReader()
        reader.Load(esmPath)

        For Each kv In reader.Records
            Dim rec = kv.Value
            If rec.Header.Signature <> "ARMO" Then Continue For

            Dim obtsInThisRecord As Integer = 0
            For Each sr In rec.Subrecords
                If sr.Signature <> "OBTS" Then Continue For
                Dim raw = sr.Data
                If raw Is Nothing OrElse raw.Length < 17 Then Continue For
                obtsInThisRecord += 1
                report.ObtsPayloadsTested += 1

                ' pluginManager=Nothing → ResolveFormIDReference is identity, so parsed FormIDs == raw wire
                ' FormIDs. Combined with IdentityRemap this makes the round-trip a pure structure/byte test.
                Dim combo1 = RecordParsers.ParseOBTSPayload(raw, rec, Nothing)
                Dim rebuilt = NpcSubrecordWriter.BuildObtsPayload(combo1, IdentityRemap)
                Dim combo2 = RecordParsers.ParseOBTSPayload(rebuilt, rec, Nothing)

                Dim structDiffs = CompareCombos(combo1, combo2)
                Dim tag = $"ARMO {rec.EditorID}[{rec.Header.FormID:X8}] OBTS#{obtsInThisRecord}"
                If structDiffs.Count = 0 Then
                    report.StructuralPass += 1
                Else
                    report.StructuralFail += 1
                    report.Lines.Add($"STRUCT-FAIL {tag}: {String.Join("; ", structDiffs)}")
                End If

                ' Byte comparison: identical length + identical bytes = byte-exact. If different, classify
                ' whether the divergence is only at parser-ignored positions (pad/unused) — structural round
                ' trip still holds — or at a structural position (a real bug).
                ' Named reference case from the task spec (ClothesPreWarDressBlue): log it explicitly.
                If rec.Header.FormID = &H2075C0UI Then
                    report.Lines.Add($"FOCUS {tag}: struct={If(structDiffs.Count = 0, "PASS", "FAIL")} byteExact={BytesEqual(raw, rebuilt)} len={raw.Length}")
                End If

                If BytesEqual(raw, rebuilt) Then
                    report.ByteExact += 1
                Else
                    Dim padOnly = DiffIsPadOnly(raw, rebuilt)
                    If padOnly Then
                        report.ByteDiffInPadOnly += 1
                        report.Lines.Add($"BYTE-DIFF(pad-only) {tag}: {DescribeByteDiffs(raw, rebuilt)}")
                    Else
                        report.ByteDiffStructural += 1
                        report.Lines.Add($"BYTE-DIFF(STRUCTURAL) {tag}: {DescribeByteDiffs(raw, rebuilt)}")
                    End If
                End If
            Next
            If obtsInThisRecord > 0 Then report.ArmoRecordsWithObts += 1
        Next

        Return report
    End Function

    Private Function CompareCombos(a As ARMO_Combination, b As ARMO_Combination) As List(Of String)
        Dim d As New List(Of String)
        If a Is Nothing OrElse b Is Nothing Then
            d.Add("null combo") : Return d
        End If
        If a.Includes.Count <> b.Includes.Count Then d.Add($"IncludeCount {a.Includes.Count}->{b.Includes.Count}")
        If a.Properties.Count <> b.Properties.Count Then d.Add($"PropertyCount {a.Properties.Count}->{b.Properties.Count}")
        If a.LevelMin <> b.LevelMin Then d.Add($"LevelMin {a.LevelMin}->{b.LevelMin}")
        If a.LevelMax <> b.LevelMax Then d.Add($"LevelMax {a.LevelMax}->{b.LevelMax}")
        If a.ParentCombinationIndex <> b.ParentCombinationIndex Then d.Add($"Parent {a.ParentCombinationIndex}->{b.ParentCombinationIndex}")
        If a.IsDefault <> b.IsDefault Then d.Add($"Default {a.IsDefault}->{b.IsDefault}")
        If a.MinLevelForRanks <> b.MinLevelForRanks Then d.Add($"MinLvlRanks {a.MinLevelForRanks}->{b.MinLevelForRanks}")
        If a.AltLevelsPerTier <> b.AltLevelsPerTier Then d.Add($"AltLvlTier {a.AltLevelsPerTier}->{b.AltLevelsPerTier}")

        If a.Keywords.Count <> b.Keywords.Count Then
            d.Add($"KeywordCount {a.Keywords.Count}->{b.Keywords.Count}")
        Else
            For i = 0 To a.Keywords.Count - 1
                If a.Keywords(i) <> b.Keywords(i) Then d.Add($"Keyword[{i}] {a.Keywords(i):X8}->{b.Keywords(i):X8}")
            Next
        End If

        Dim n = Math.Min(a.Includes.Count, b.Includes.Count)
        For i = 0 To n - 1
            Dim ia = a.Includes(i), ib = b.Includes(i)
            If ia.ModFormID <> ib.ModFormID Then d.Add($"Inc[{i}].Mod {ia.ModFormID:X8}->{ib.ModFormID:X8}")
            If ia.AttachPointIndex <> ib.AttachPointIndex Then d.Add($"Inc[{i}].Attach {ia.AttachPointIndex}->{ib.AttachPointIndex}")
            If ia.IsOptional <> ib.IsOptional Then d.Add($"Inc[{i}].Opt {ia.IsOptional}->{ib.IsOptional}")
            If ia.DontUseAll <> ib.DontUseAll Then d.Add($"Inc[{i}].DontUseAll {ia.DontUseAll}->{ib.DontUseAll}")
        Next

        Dim m = Math.Min(a.Properties.Count, b.Properties.Count)
        For i = 0 To m - 1
            Dim pa = a.Properties(i), pb = b.Properties(i)
            If pa.ValueType <> pb.ValueType Then d.Add($"Prop[{i}].ValueType {pa.ValueType}->{pb.ValueType}")
            If pa.FunctionType <> pb.FunctionType Then d.Add($"Prop[{i}].FuncType {pa.FunctionType}->{pb.FunctionType}")
            If pa.PropertyIndex <> pb.PropertyIndex Then d.Add($"Prop[{i}].PropIdx {pa.PropertyIndex}->{pb.PropertyIndex}")
            If pa.ValueType = OMOD_ValueType.FormIDInt OrElse pa.ValueType = OMOD_ValueType.FormIDFloat Then
                If pa.Value1FormID <> pb.Value1FormID Then d.Add($"Prop[{i}].Value1FID {pa.Value1FormID:X8}->{pb.Value1FormID:X8}")
            Else
                If SingleBits(pa.Value1) <> SingleBits(pb.Value1) Then d.Add($"Prop[{i}].Value1 {pa.Value1}->{pb.Value1}")
            End If
            If SingleBits(pa.Value2) <> SingleBits(pb.Value2) Then d.Add($"Prop[{i}].Value2 {pa.Value2}->{pb.Value2}")
            If SingleBits(pa.StepValue) <> SingleBits(pb.StepValue) Then d.Add($"Prop[{i}].Step {pa.StepValue}->{pb.StepValue}")
        Next

        Return d
    End Function

    ' ========================================================================
    ' Phase 4 gate: FULL Object Template BLOCK round-trip (payload + FULL/OBTF)
    ' ========================================================================

    Public Class ObtsBlockReport
        Public ArmoBlocksTested As Integer
        Public BlockPass As Integer
        Public BlockFail As Integer
        Public CombosCompared As Integer
        Public CombosWithName As Integer
        Public CombosEditorOnly As Integer
        Public SyntheticRan As Boolean
        Public SyntheticPass As Boolean
        Public Lines As New List(Of String)
        Public ReadOnly Property AllPass As Boolean
            Get
                Return BlockFail = 0 AndAlso SyntheticRan AndAlso SyntheticPass
            End Get
        End Property
    End Class

    ''' <summary>Phase 4 gate. Exercises the WHOLE Object Template block round-trip that the override authoring
    ''' path now depends on: ParseARMO(rec) → Combinations (captures per-combination DisplayName/FULL and
    ''' IsEditorOnly/OBTF, not just the OBTS payload) → EmitArmoObjectTemplate(identityRemap) into a buffer →
    ''' split back to subrecords → ParseARMO(rebuilt) → compare the FULL combination list, including
    ''' DisplayName and IsEditorOnly on top of the entire payload (via CompareCombosFull).
    '''
    ''' Two cohorts:
    '''   • VANILLA — every ARMO in the ESM that carries an Object Template. Proves the block emit/parse is
    '''     stable over real data (counts, names, flags survive).
    '''   • SYNTHETIC — a hand-built cohort with concrete UTF-8 display names AND editor-only markers, so the
    '''     FULL/OBTF fields are proven to round-trip with REAL values (vanilla Fallout4.esm is localized, so
    '''     its FULLs are 4-byte string-ids rather than literal names).</summary>
    Public Function RunBlock(esmPath As String) As ObtsBlockReport
        Dim report As New ObtsBlockReport()
        Dim reader As New PluginReader()
        reader.Load(esmPath)

        For Each kv In reader.Records
            Dim rec = kv.Value
            If rec.Header.Signature <> "ARMO" Then Continue For
            ' Only ARMOs that actually declare an Object Template (OBTE) are in scope.
            Dim hasObte As Boolean = False
            For Each sr In rec.Subrecords
                If sr.Signature = "OBTE" Then hasObte = True : Exit For
            Next
            If Not hasObte Then Continue For

            Dim combos1 = RecordParsers.ParseARMO(rec, Nothing).Combinations
            If combos1 Is Nothing OrElse combos1.Count = 0 Then Continue For
            report.ArmoBlocksTested += 1

            Dim block = EmitBlockToBytes(combos1)
            Dim combos2 = ParseBlockToCombos(block)

            Dim tag = $"ARMO {rec.EditorID}[{rec.Header.FormID:X8}]"
            Dim diffs As New List(Of String)
            If combos1.Count <> combos2.Count Then
                diffs.Add($"ComboCount {combos1.Count}->{combos2.Count}")
            Else
                For i = 0 To combos1.Count - 1
                    report.CombosCompared += 1
                    If combos1(i).DisplayName <> "" Then report.CombosWithName += 1
                    If combos1(i).IsEditorOnly Then report.CombosEditorOnly += 1
                    Dim cd = CompareCombosFull(combos1(i), combos2(i))
                    For Each s In cd : diffs.Add($"combo[{i}] {s}") : Next
                Next
            End If

            If diffs.Count = 0 Then
                report.BlockPass += 1
            Else
                report.BlockFail += 1
                report.Lines.Add($"BLOCK-FAIL {tag}: {String.Join("; ", diffs)}")
            End If
        Next

        RunSyntheticBlock(report)
        Return report
    End Function

    ''' <summary>Hand-built cohort proving FULL (display name, incl. non-ASCII) and OBTF (editor-only) survive
    ''' the block round-trip with concrete values — plus keywords/includes/properties on the same combos.</summary>
    Private Sub RunSyntheticBlock(report As ObtsBlockReport)
        report.SyntheticRan = True
        Dim combos As New List(Of ARMO_Combination)

        ' combo 0: named, default, keywords + include + a float property.
        Dim c0 As New ARMO_Combination With {
            .DisplayName = "Standard", .IsEditorOnly = False, .IsDefault = True,
            .ParentCombinationIndex = -1, .LevelMin = 1, .LevelMax = 10,
            .MinLevelForRanks = 2, .AltLevelsPerTier = 3}
        c0.Keywords.Add(&H12345678UI)
        c0.Keywords.Add(&HABCDEF01UI)
        c0.Includes.Add(New ARMO_CombinationInclude With {.ModFormID = &H100200UI, .AttachPointIndex = 4, .IsOptional = True, .DontUseAll = False})
        c0.Properties.Add(New OMOD_Property With {.ValueType = OMOD_ValueType.FloatType, .FunctionType = 1, .PropertyIndex = 7, .Value1 = 1.5F, .Value2 = 0.25F, .StepValue = 0.1F})
        combos.Add(c0)

        ' combo 1: editor-only, NO name.
        Dim c1 As New ARMO_Combination With {
            .DisplayName = "", .IsEditorOnly = True, .IsDefault = False, .ParentCombinationIndex = 2}
        combos.Add(c1)

        ' combo 2: editor-only AND named with a non-ASCII display name + a FormID property.
        Dim c2 As New ARMO_Combination With {
            .DisplayName = "Editor Ω Variant", .IsEditorOnly = True, .IsDefault = False, .ParentCombinationIndex = -1}
        c2.Includes.Add(New ARMO_CombinationInclude With {.ModFormID = &H55AA33UI, .AttachPointIndex = 0, .IsOptional = False, .DontUseAll = True})
        c2.Properties.Add(New OMOD_Property With {.ValueType = OMOD_ValueType.FormIDInt, .FunctionType = 0, .PropertyIndex = 9, .Value1FormID = &H900800UI, .Value2 = 0.0F, .StepValue = 0.0F})
        combos.Add(c2)

        Dim block = EmitBlockToBytes(combos)
        Dim back = ParseBlockToCombos(block)

        Dim diffs As New List(Of String)
        If back.Count <> combos.Count Then
            diffs.Add($"ComboCount {combos.Count}->{back.Count}")
        Else
            For i = 0 To combos.Count - 1
                Dim cd = CompareCombosFull(combos(i), back(i))
                For Each s In cd : diffs.Add($"combo[{i}] {s}") : Next
            Next
        End If

        report.SyntheticPass = (diffs.Count = 0)
        If diffs.Count = 0 Then
            report.Lines.Add("SYNTHETIC block round-trip: PASS (names 'Standard'/''/'Editor Ω Variant', editor-only F/T/T)")
        Else
            report.Lines.Add($"SYNTHETIC block round-trip: FAIL: {String.Join("; ", diffs)}")
        End If
    End Sub

    ''' <summary>Emit the OBTE/OBTF/FULL/OBTS/STOP block for a combination list into a byte buffer using the
    ''' identity remapper (no MAST relocation) — mirrors what the override writer emits when authoring.</summary>
    Private Function EmitBlockToBytes(combos As List(Of ARMO_Combination)) As Byte()
        Using ms As New MemoryStream()
            Using bw As New BinaryWriter(ms)
                NpcSubrecordWriter.EmitArmoObjectTemplate(bw, combos, IdentityRemap)
            End Using
            Return ms.ToArray()
        End Using
    End Function

    ''' <summary>Split a raw subrecord stream (sig[4] + u16 len + data) into a synthetic ARMO PluginRecord and
    ''' re-parse it through ParseARMO, returning the reconstructed combination list. pluginManager=Nothing keeps
    ''' FormID/string resolution as identity so the comparison is a pure structure test.</summary>
    Private Function ParseBlockToCombos(block As Byte()) As List(Of ARMO_Combination)
        Dim rec As New PluginRecord With {.Header = New RecordHeader With {.Signature = "ARMO"}}
        Dim pos As Integer = 0
        While pos + 6 <= block.Length
            Dim sig = Encoding.ASCII.GetString(block, pos, 4)
            Dim len = CInt(BitConverter.ToUInt16(block, pos + 4))
            pos += 6
            If pos + len > block.Length Then Exit While
            Dim data As Byte()
            If len > 0 Then
                data = New Byte(len - 1) {}
                Buffer.BlockCopy(block, pos, data, 0, len)
            Else
                data = Array.Empty(Of Byte)()
            End If
            rec.Subrecords.Add(New SubrecordData With {.Signature = sig, .Data = data})
            pos += len
        End While
        Return RecordParsers.ParseARMO(rec, Nothing).Combinations
    End Function

    ''' <summary>CompareCombos extended with the block-level fields the OBTS payload does NOT carry: the
    ''' per-combination FULL (DisplayName) and OBTF (IsEditorOnly) markers.</summary>
    Private Function CompareCombosFull(a As ARMO_Combination, b As ARMO_Combination) As List(Of String)
        Dim d = CompareCombos(a, b)
        If a IsNot Nothing AndAlso b IsNot Nothing Then
            If Not String.Equals(a.DisplayName, b.DisplayName, StringComparison.Ordinal) Then d.Add($"DisplayName '{a.DisplayName}'->'{b.DisplayName}'")
            If a.IsEditorOnly <> b.IsEditorOnly Then d.Add($"IsEditorOnly {a.IsEditorOnly}->{b.IsEditorOnly}")
        End If
        Return d
    End Function

    Private Function SingleBits(v As Single) As Integer
        Return BitConverter.ToInt32(BitConverter.GetBytes(v), 0)
    End Function

    Private Function BytesEqual(a As Byte(), b As Byte()) As Boolean
        If a.Length <> b.Length Then Return False
        For i = 0 To a.Length - 1
            If a(i) <> b(i) Then Return False
        Next
        Return True
    End Function

    ''' <summary>True when every differing byte between raw and rebuilt sits at a parser-ignored position
    ''' (LevelMin/LevelMax pad, or the 8 unused bytes inside each 24-byte Property entry). Walks the exact
    ''' layout so it never mis-classifies a structural byte as pad.</summary>
    Private Function DiffIsPadOnly(raw As Byte(), rebuilt As Byte()) As Boolean
        If raw.Length <> rebuilt.Length Then Return False
        Dim pads = ComputePadPositions(raw)
        For i = 0 To raw.Length - 1
            If raw(i) <> rebuilt(i) AndAlso Not pads.Contains(i) Then Return False
        Next
        Return True
    End Function

    Private Function ComputePadPositions(d As Byte()) As HashSet(Of Integer)
        Dim pads As New HashSet(Of Integer)
        If d.Length < 17 Then Return pads
        pads.Add(9)   ' pad after LevelMin
        pads.Add(11)  ' pad after LevelMax
        ' Note: byte @14 'Default' is compared as boolean by the parser, so a non-{0,1} true value would be a
        ' pad-like divergence; we DON'T list it here so it shows up explicitly if it ever occurs.
        Dim includeCount = CInt(BitConverter.ToUInt32(d, 0))
        Dim propertyCount = CInt(BitConverter.ToUInt32(d, 4))
        Dim offset As Integer = 15
        Dim kwCount As Integer = CInt(d(offset))
        offset += 1
        offset += kwCount * 4
        offset += 2 ' MinLevelForRanks + AltLevelsPerTier
        offset += includeCount * 7
        For i = 0 To propertyCount - 1
            If offset + 24 > d.Length Then Exit For
            ' unused bytes inside a Property entry: @1..3, @5..7, @10..11 (relative to entry start)
            pads.Add(offset + 1) : pads.Add(offset + 2) : pads.Add(offset + 3)
            pads.Add(offset + 5) : pads.Add(offset + 6) : pads.Add(offset + 7)
            pads.Add(offset + 10) : pads.Add(offset + 11)
            offset += 24
        Next
        Return pads
    End Function

    Private Function DescribeByteDiffs(raw As Byte(), rebuilt As Byte()) As String
        Dim sb As New StringBuilder()
        Dim count As Integer = 0
        Dim maxLen = Math.Max(raw.Length, rebuilt.Length)
        If raw.Length <> rebuilt.Length Then sb.Append($"len {raw.Length}->{rebuilt.Length} ")
        For i = 0 To Math.Min(raw.Length, rebuilt.Length) - 1
            If raw(i) <> rebuilt(i) Then
                If count < 12 Then sb.Append($"@{i}:{raw(i):X2}->{rebuilt(i):X2} ")
                count += 1
            End If
        Next
        sb.Append($"({count} byte diffs)")
        Return sb.ToString()
    End Function

End Module
