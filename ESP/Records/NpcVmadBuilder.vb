Imports System.IO

' ============================================================================
' VMAD builder — the inverse of NpcVmadScanner.
'
' Produces an NPC_VmadData (raw payload bytes + FormID position list) that
' NpcSubrecordWriter.EmitVmad can emit as-is: EmitVmad copies RawBytes and
' patches each FormIdPositions entry through the MAST remapper, so a FormID we
' write here is automatically re-mastered for the target plugin. We therefore
' write GLOBAL (resolved) FormIDs and let the writer do the master-index math.
'
' ---------------------------------------------------------------------------
' UPSERT, NEVER BLIND-APPEND — and NEVER touch a script that is not ours.
'
' Two hard constraints, both from real data:
'
'  1. NEVER clobber somebody else's script. 805 of 5118 NPC_ in Skyrim.esm and
'     382 of 3015 in Fallout4.esm already ship a VMAD with vanilla scripts
'     (workshopnpcscript, WIDeadBodyCleanupScript, masterambushscript, ...).
'     Overwriting one breaks workshop settlement logic / corpse cleanup / quest
'     scripting on that actor. Other mods add their own on top. Those entries are
'     copied through byte-for-byte, always.
'
'  2. NEVER duplicate our own. Saving the same NPC twice, re-editing it later, or
'     stacking incremental mod versions must converge to exactly ONE copy of our
'     script with the CURRENT values — not N stale copies. A plain append would
'     grow one entry per save and the VM would run all of them.
'
' So UpsertScript rebuilds the scripts array as:
'       [every script NOT under our reserved prefix, verbatim] + [ours, current]
' Removing by PREFIX (not by exact name) also cleans up entries left behind by an
' older version of the app that used a different script name — otherwise those
' would linger forever as orphans.
'
' Splicing (rather than appending in place) is safe because the Scripts array is
' the LAST element of a plain wbVMAD (Version, ObjectFormat, Scripts —
' wbDefinitionsFO4.pas:4383-4388; wbDefinitionsTES5.pas:3178-3182). NPC_ uses the
' plain wbVMAD, not one of the wbVMADFragmented* variants (PERK/PACK/QUST/INFO/
' SCEN, which DO carry a fragment tail after the scripts). Verified empirically:
' all 1333 vanilla NPC_ VMAD payloads end exactly where the scripts array ends,
' with zero trailing bytes.
'
' ---------------------------------------------------------------------------
' GAME-AWARE: the header Version differs by game. Everything else does not.
'
'   Version      Skyrim = 5, FO4 = 6
'                xEdit: wbVMADVersion .SetDefaultNativeValue(5) TES5 :3174
'                                     .SetDefaultNativeValue(6) FO4  :4373
'                Measured: 951/951 Skyrim+Dawnguard = 5, 382/382 FO4 = 6.
'   ObjectFormat 2 in BOTH (xEdit default 2 for both; measured 1333/1333 = 2).
'
' When APPENDING we preserve the record's own Version/ObjectFormat rather than
' forcing the game default — the record is authoritative over the default.
'
' ---------------------------------------------------------------------------
' Constant byte values below are MEASURED across all 1333 vanilla NPC_ VMADs
' (Skyrim.esm 805 + Dawnguard.esm 146 + Fallout4.esm 382), not assumed:
'
'   script Flags   (u8)  = 0 (Local)     — 1628/1628 script entries
'   property Flags (u8)  = 1 (Edited)    — 5341/5341 properties, every type
'   Object 'Unused' (u16) = 0            — 5846/5846 objects
'   Object 'Alias'  (s16) = -1           — 5840/5846 (the 6 others are quest
'                                          aliases, which cannot apply to a
'                                          base-form script like ours)
' ============================================================================

Public Module NpcVmadBuilder

    ' --- measured constants (see header) ---
    Private Const ScriptFlagLocal As Byte = 0
    Private Const PropertyFlagEdited As Byte = 1
    Private Const ObjectAliasNone As Short = -1S
    Private Const ObjectUnused As UShort = 0US

    ' --- property type ids (wbPropTypeEnum, wbDefinitionsFO4.pas:4087+) ---
    Public Enum VmadPropType As Byte
        ObjectRef = 1
        Str = 2
        Int32 = 3
        Float = 4
        Bool = 5
        ArrayOfObject = 11
        ArrayOfString = 12
        ArrayOfInt32 = 13
        ArrayOfFloat = 14
        ArrayOfBool = 15
    End Enum

    ''' <summary>One Papyrus script property to author. Build these with the From* factories rather
    ''' than by hand — they set the type tag and the matching value field together.
    ''' <para>FormIDs in <see cref="ObjectValue"/> / <see cref="ObjectArray"/> are GLOBAL (resolved)
    ''' FormIDs. The builder records their byte offsets so NpcSubrecordWriter.EmitVmad re-masters
    ''' them for the target plugin; do NOT pre-encode a master index here.</para></summary>
    Public Class VmadPropertySpec
        Public Name As String = ""
        Public PropType As VmadPropType

        Public StringValue As String
        Public IntValue As Integer
        Public FloatValue As Single
        Public BoolValue As Boolean
        Public ObjectValue As UInteger

        Public StringArray As List(Of String)
        Public IntArray As List(Of Integer)
        Public FloatArray As List(Of Single)
        Public BoolArray As List(Of Boolean)
        Public ObjectArray As List(Of UInteger)

        Public Shared Function FromString(name As String, value As String) As VmadPropertySpec
            Return New VmadPropertySpec With {.Name = name, .PropType = VmadPropType.Str, .StringValue = If(value, "")}
        End Function

        Public Shared Function FromInt(name As String, value As Integer) As VmadPropertySpec
            Return New VmadPropertySpec With {.Name = name, .PropType = VmadPropType.Int32, .IntValue = value}
        End Function

        Public Shared Function FromFloat(name As String, value As Single) As VmadPropertySpec
            Return New VmadPropertySpec With {.Name = name, .PropType = VmadPropType.Float, .FloatValue = value}
        End Function

        Public Shared Function FromBool(name As String, value As Boolean) As VmadPropertySpec
            Return New VmadPropertySpec With {.Name = name, .PropType = VmadPropType.Bool, .BoolValue = value}
        End Function

        ''' <param name="globalFormID">Resolved/global FormID — NOT a source-plugin-encoded one.</param>
        Public Shared Function FromObject(name As String, globalFormID As UInteger) As VmadPropertySpec
            Return New VmadPropertySpec With {.Name = name, .PropType = VmadPropType.ObjectRef, .ObjectValue = globalFormID}
        End Function

        Public Shared Function FromStringArray(name As String, values As IEnumerable(Of String)) As VmadPropertySpec
            Return New VmadPropertySpec With {
                .Name = name, .PropType = VmadPropType.ArrayOfString,
                .StringArray = If(values Is Nothing, New List(Of String), values.Select(Function(s) If(s, "")).ToList())}
        End Function

        Public Shared Function FromIntArray(name As String, values As IEnumerable(Of Integer)) As VmadPropertySpec
            Return New VmadPropertySpec With {
                .Name = name, .PropType = VmadPropType.ArrayOfInt32,
                .IntArray = If(values Is Nothing, New List(Of Integer), values.ToList())}
        End Function

        Public Shared Function FromFloatArray(name As String, values As IEnumerable(Of Single)) As VmadPropertySpec
            Return New VmadPropertySpec With {
                .Name = name, .PropType = VmadPropType.ArrayOfFloat,
                .FloatArray = If(values Is Nothing, New List(Of Single), values.ToList())}
        End Function

        Public Shared Function FromBoolArray(name As String, values As IEnumerable(Of Boolean)) As VmadPropertySpec
            Return New VmadPropertySpec With {
                .Name = name, .PropType = VmadPropType.ArrayOfBool,
                .BoolArray = If(values Is Nothing, New List(Of Boolean), values.ToList())}
        End Function

        Public Shared Function FromObjectArray(name As String, globalFormIDs As IEnumerable(Of UInteger)) As VmadPropertySpec
            Return New VmadPropertySpec With {
                .Name = name, .PropType = VmadPropType.ArrayOfObject,
                .ObjectArray = If(globalFormIDs Is Nothing, New List(Of UInteger), globalFormIDs.ToList())}
        End Function
    End Class

    ''' <summary>One Papyrus script to attach. <see cref="Name"/> must match the compiled .pex file
    ''' name (case-insensitive to the VM, but write it as authored).</summary>
    Public Class VmadScriptSpec
        Public Name As String = ""
        Public Properties As New List(Of VmadPropertySpec)
    End Class

    ''' <summary>Name prefix RESERVED for scripts this app authors. Every script under this prefix is
    ''' considered ours and is replaced wholesale on each write; every script NOT under it belongs to
    ''' vanilla or another mod and is copied through untouched.
    '''
    ''' <para>⚠ This prefix is a DELETE POWER: anything matching it gets removed on every save. It is
    ''' deliberately long and author-namespaced so it cannot collide with a real mod's script name —
    ''' a collision would mean silently deleting that mod's script from the NPC. VMAD script names are
    ''' u16-length-prefixed strings (65535 bytes max), so length costs nothing; the only real constraint
    ''' is that the name must match the compiled .pex file name on disk.</para></summary>
    Public Const ReservedScriptPrefix As String = "NPCM_Manolov_"

    ''' <summary>Write our script into <paramref name="existing"/>, IDEMPOTENTLY.
    '''
    ''' <para>Rebuilds the scripts array as <c>[every script NOT under <paramref name="reservedPrefix"/>,
    ''' byte-for-byte] + [<paramref name="script"/>]</c>. So: saving twice does not duplicate it,
    ''' re-editing replaces the stale values, an entry left by an older app version (different name,
    ''' same prefix) is cleaned up, and vanilla / other-mod scripts are never disturbed. See the
    ''' UPSERT note in the file header.</para>
    '''
    ''' <para>Pass <paramref name="script"/> = Nothing to REMOVE our script and keep the rest — the
    ''' "user cleared all the RaceMenu extras" path. If nothing is left afterwards the function returns
    ''' Nothing, which makes the writer drop the VMAD subrecord entirely (correct: the record had no
    ''' scripts of its own).</para>
    '''
    ''' <para><paramref name="existing"/> may be Nothing / empty to author a VMAD from scratch, in which
    ''' case the header uses the game's Version (Skyrim 5 / FO4 6) and ObjectFormat 2.</para>
    '''
    ''' <para>Returns a NEW NPC_VmadData; <paramref name="existing"/> is never mutated.</para></summary>
    ''' <exception cref="InvalidDataException">If <paramref name="existing"/> could not be fully parsed
    ''' (<see cref="NPC_VmadData.ScanComplete"/> = False). We refuse to rewrite a payload whose structure
    ''' we could not walk: we would not know where the other scripts start and end, nor where their
    ''' FormIDs are, so we could neither preserve them nor re-master them.</exception>
    Public Function UpsertScript(existing As NPC_VmadData,
                                 script As VmadScriptSpec,
                                 game As Config_App.Game_Enum,
                                 Optional reservedPrefix As String = ReservedScriptPrefix) As NPC_VmadData
        If script IsNot Nothing Then
            If String.IsNullOrWhiteSpace(script.Name) Then
                Throw New ArgumentException("VMAD script spec requires a script name.", NameOf(script))
            End If
            If Not script.Name.StartsWith(reservedPrefix, StringComparison.OrdinalIgnoreCase) Then
                ' Guard against the foot-gun: a name outside the reserved prefix would NOT be found by
                ' the next upsert, so the next save would append a second copy instead of replacing it.
                Throw New ArgumentException(
                    $"Script name '{script.Name}' must start with the reserved prefix '{reservedPrefix}', " &
                    "otherwise repeated saves would duplicate it instead of updating it.", NameOf(script))
            End If
        End If

        Dim hasExisting = existing IsNot Nothing AndAlso
                          existing.RawBytes IsNot Nothing AndAlso
                          existing.RawBytes.Length >= 6

        If hasExisting AndAlso Not existing.ScanComplete Then
            Throw New InvalidDataException(
                "Cannot author a script into a VMAD that could not be fully parsed — the other scripts' " &
                "spans and FormID layout are unknown, so they could be neither preserved nor re-mastered. " &
                $"Reason: {If(existing.ScanFailureReason, "unknown")}.")
        End If

        ' Version/ObjectFormat: the record wins over the game default when one already exists.
        Dim version As Short = If(hasExisting, existing.Version, DefaultVmadVersion(game))
        Dim objectFormat As Short = If(hasExisting, existing.ObjectFormat, CShort(2))

        ' Keep everything that is NOT ours, in its original order.
        Dim kept As New List(Of NPC_VmadScriptRef)
        If hasExisting Then
            For Each s In existing.Scripts
                If s IsNot Nothing AndAlso
                   Not If(s.Name, "").StartsWith(reservedPrefix, StringComparison.OrdinalIgnoreCase) Then
                    kept.Add(s)
                End If
            Next
        End If

        Dim totalScripts = kept.Count + If(script Is Nothing, 0, 1)
        If totalScripts = 0 Then Return Nothing   ' nothing left → writer drops the VMAD subrecord
        If totalScripts > UShort.MaxValue Then
            Throw New InvalidDataException($"VMAD would hold {totalScripts} scripts (u16 ScriptCount max).")
        End If

        Dim result As New NPC_VmadData With {
            .Version = version,
            .ObjectFormat = objectFormat,
            .ScriptCount = CUShort(totalScripts),
            .ScanComplete = True   ' we built it, so we know every span and every FormID position
        }

        Using ms As New MemoryStream()
            Using bw As New BinaryWriter(ms)
                bw.Write(version)
                bw.Write(objectFormat)
                bw.Write(CUShort(totalScripts))

                ' --- the scripts we keep: copied byte-for-byte, FormID offsets rebased
                For Each s In kept
                    Dim newOffset = CInt(ms.Position)
                    bw.Write(existing.RawBytes, s.Offset, s.Length)

                    For Each ref In existing.FormIdPositions
                        If ref.Offset >= s.Offset AndAlso ref.Offset < s.Offset + s.Length Then
                            result.FormIdPositions.Add(New NPC_VmadFormIdRef With {
                                .Offset = newOffset + (ref.Offset - s.Offset),
                                .RawFormID = ref.RawFormID,
                                .ResolvedFormID = ref.ResolvedFormID})
                        End If
                    Next

                    result.Scripts.Add(New NPC_VmadScriptRef With {
                        .Name = s.Name, .Offset = newOffset, .Length = s.Length})
                Next

                ' --- ours, last, with the current values
                If script IsNot Nothing Then
                    Dim relativeFormIds As New List(Of NPC_VmadFormIdRef)
                    Dim entry = BuildScriptEntry(script, objectFormat, relativeFormIds)
                    Dim entryBase = CInt(ms.Position)
                    bw.Write(entry)

                    For Each ref In relativeFormIds
                        result.FormIdPositions.Add(New NPC_VmadFormIdRef With {
                            .Offset = entryBase + ref.Offset,
                            .RawFormID = ref.RawFormID,
                            .ResolvedFormID = ref.ResolvedFormID})
                    Next

                    result.Scripts.Add(New NPC_VmadScriptRef With {
                        .Name = script.Name, .Offset = entryBase, .Length = entry.Length})
                End If
            End Using
            result.RawBytes = ms.ToArray()
        End Using

        result.FormIdPositions.Sort(Function(a, b) a.Offset.CompareTo(b.Offset))
        Return result
    End Function

    ''' <summary>Remove our script (everything under <paramref name="reservedPrefix"/>) and keep every
    ''' other one. Returns Nothing when no script survives, which drops the VMAD subrecord.</summary>
    Public Function RemoveAppScripts(existing As NPC_VmadData,
                                     game As Config_App.Game_Enum,
                                     Optional reservedPrefix As String = ReservedScriptPrefix) As NPC_VmadData
        Return UpsertScript(existing, Nothing, game, reservedPrefix)
    End Function

    ''' <summary>A stable, order-sensitive hash of every property in <paramref name="script"/> EXCEPT the one
    ''' named <paramref name="excludeProperty"/>. Deterministic across runs and machines (plain FNV-1a over the
    ''' canonical text of each name/type/value — no GetHashCode, whose string seed is randomized per process).
    '''
    ''' <para>Purpose: give the emitted script a version number that is a function of THIS NPC's payload. The
    ''' script remembers, per actor instance in the savegame, which version it already applied; when the user
    ''' edits an NPC and re-saves, only THAT NPC's hash changes, so only that actor re-applies on its next load.
    ''' Every other NPC keeps its number and stays quiet. A global constant would instead force every NPC in the
    ''' plugin to re-apply on any edit.</para></summary>
    Public Function StablePayloadHash(script As VmadScriptSpec, excludeProperty As String) As Integer
        ' FNV-1a depends on the 32-bit multiply WRAPPING AROUND. VB.NET has integer overflow checks on by
        ' default, so `h * 16777619UI` on a UInteger throws OverflowException instead of truncating (C gets
        ' the wrap for free; VB does not). Accumulate in 64 bits and mask back to 32 after every step — same
        ' result as the C reference, no exception, and no dependency on the project's overflow-check setting.
        Const Mask32 As ULong = &HFFFFFFFFUL
        Const Prime As ULong = 16777619UL
        Dim h As ULong = 2166136261UL               ' FNV-1a 32-bit offset basis

        Dim mix = Sub(s As String)
                      For Each ch In If(s, "")
                          h = ((h Xor CULng(AscW(ch) And &HFFFF)) * Prime) And Mask32
                      Next
                      h = ((h Xor 10UL) * Prime) And Mask32  ' field separator — "ab"+"c" must not hash like "a"+"bc"
                  End Sub

        If script IsNot Nothing AndAlso script.Properties IsNot Nothing Then
            mix(script.Name)
            For Each p In script.Properties
                If p Is Nothing Then Continue For
                If String.Equals(p.Name, excludeProperty, StringComparison.OrdinalIgnoreCase) Then Continue For

                mix(p.Name)
                mix(CInt(p.PropType).ToString(Globalization.CultureInfo.InvariantCulture))

                Dim inv = Globalization.CultureInfo.InvariantCulture
                Select Case p.PropType
                    Case VmadPropType.ObjectRef : mix(p.ObjectValue.ToString("X8", inv))
                    Case VmadPropType.Str : mix(p.StringValue)
                    Case VmadPropType.Int32 : mix(p.IntValue.ToString(inv))
                        ' "R" round-trips the float exactly, so a 1-ULP edit still changes the hash.
                    Case VmadPropType.Float : mix(p.FloatValue.ToString("R", inv))
                    Case VmadPropType.Bool : mix(If(p.BoolValue, "1", "0"))
                    Case VmadPropType.ArrayOfObject
                        For Each v In If(p.ObjectArray, New List(Of UInteger)) : mix(v.ToString("X8", inv)) : Next
                    Case VmadPropType.ArrayOfString
                        For Each v In If(p.StringArray, New List(Of String)) : mix(v) : Next
                    Case VmadPropType.ArrayOfInt32
                        For Each v In If(p.IntArray, New List(Of Integer)) : mix(v.ToString(inv)) : Next
                    Case VmadPropType.ArrayOfFloat
                        For Each v In If(p.FloatArray, New List(Of Single)) : mix(v.ToString("R", inv)) : Next
                    Case VmadPropType.ArrayOfBool
                        For Each v In If(p.BoolArray, New List(Of Boolean)) : mix(If(v, "1", "0")) : Next
                End Select
            Next
        End If

        ' Fold to a positive Int32: the script compares it against an int it stores, and its "never applied"
        ' sentinel is -1, which must never collide with a real hash.
        Return CInt(h And &H7FFFFFFFUL)
    End Function

    ''' <summary>True when the payload already carries a script of ours. Cheap check for the save path
    ''' (e.g. "does this NPC need its VMAD rewritten at all?").</summary>
    Public Function HasAppScript(existing As NPC_VmadData,
                                 Optional reservedPrefix As String = ReservedScriptPrefix) As Boolean
        If existing Is Nothing OrElse existing.Scripts Is Nothing Then Return False
        Return existing.Scripts.Any(Function(s) s IsNot Nothing AndAlso
                                                If(s.Name, "").StartsWith(reservedPrefix, StringComparison.OrdinalIgnoreCase))
    End Function

    ''' <summary>VMAD header Version by game. THE one game-aware field in a VMAD payload.</summary>
    Public Function DefaultVmadVersion(game As Config_App.Game_Enum) As Short
        ' xEdit: wbVMADVersion default 5 (TES5 :3174) / 6 (FO4 :4373). Matches vanilla 1333/1333.
        Return If(game = Config_App.Game_Enum.Skyrim, CShort(5), CShort(6))
    End Function

    ' ========================================================================
    ' Entry / property encoders. Offsets recorded into formIds are RELATIVE to
    ' the start of the returned buffer; AppendScript rebases them.
    ' ========================================================================

    ''' <summary>ScriptEntry = u16 nameLen + name + u8 Flags + u16 PropertyCount + properties.
    ''' (wbScriptEntry, wbDefinitionsFO4.pas:4207-4216.)</summary>
    Private Function BuildScriptEntry(script As VmadScriptSpec,
                                      objectFormat As Short,
                                      formIds As List(Of NPC_VmadFormIdRef)) As Byte()
        Using ms As New MemoryStream()
            Using bw As New BinaryWriter(ms)
                WriteLenString(bw, script.Name)
                bw.Write(ScriptFlagLocal)

                Dim props = If(script.Properties, New List(Of VmadPropertySpec)())
                If props.Count > UShort.MaxValue Then
                    Throw New InvalidDataException($"Script '{script.Name}' has {props.Count} properties (u16 max).")
                End If
                bw.Write(CUShort(props.Count))

                For Each p In props
                    WriteProperty(bw, ms, p, objectFormat, formIds)
                Next
            End Using
            Return ms.ToArray()
        End Using
    End Function

    ''' <summary>ScriptProperty = u16 nameLen + name + u8 Type + u8 Flags + Value.
    ''' (wbScriptProperty, wbDefinitionsFO4.pas:4168-4194.)</summary>
    Private Sub WriteProperty(bw As BinaryWriter,
                              ms As MemoryStream,
                              p As VmadPropertySpec,
                              objectFormat As Short,
                              formIds As List(Of NPC_VmadFormIdRef))
        If p Is Nothing OrElse String.IsNullOrWhiteSpace(p.Name) Then
            Throw New ArgumentException("VMAD property requires a name.")
        End If

        WriteLenString(bw, p.Name)
        bw.Write(CByte(p.PropType))
        bw.Write(PropertyFlagEdited)

        Select Case p.PropType
            Case VmadPropType.ObjectRef
                WriteObject(bw, ms, p.ObjectValue, objectFormat, formIds)
            Case VmadPropType.Str
                WriteLenString(bw, p.StringValue)
            Case VmadPropType.Int32
                bw.Write(p.IntValue)
            Case VmadPropType.Float
                bw.Write(p.FloatValue)
            Case VmadPropType.Bool
                bw.Write(If(p.BoolValue, CByte(1), CByte(0)))

            Case VmadPropType.ArrayOfObject
                Dim items = If(p.ObjectArray, New List(Of UInteger)())
                bw.Write(CUInt(items.Count))
                For Each fid In items
                    WriteObject(bw, ms, fid, objectFormat, formIds)
                Next
            Case VmadPropType.ArrayOfString
                Dim items = If(p.StringArray, New List(Of String)())
                bw.Write(CUInt(items.Count))
                For Each s In items
                    WriteLenString(bw, s)
                Next
            Case VmadPropType.ArrayOfInt32
                Dim items = If(p.IntArray, New List(Of Integer)())
                bw.Write(CUInt(items.Count))
                For Each v In items
                    bw.Write(v)
                Next
            Case VmadPropType.ArrayOfFloat
                Dim items = If(p.FloatArray, New List(Of Single)())
                bw.Write(CUInt(items.Count))
                For Each v In items
                    bw.Write(v)
                Next
            Case VmadPropType.ArrayOfBool
                Dim items = If(p.BoolArray, New List(Of Boolean)())
                bw.Write(CUInt(items.Count))
                For Each v In items
                    bw.Write(If(v, CByte(1), CByte(0)))
                Next

            Case Else
                Throw New InvalidDataException($"VMAD property type {CByte(p.PropType)} not supported by the builder.")
        End Select
    End Sub

    ''' <summary>The 8-byte Object union. Layout depends on the payload's ObjectFormat — the SAME rule
    ''' NpcVmadScanner.ScanObject reads back (wbScriptPropertyObject, wbDefinitionsFO4.pas:4115-4137):
    ''' ObjectFormat == 1 → v1 (FormID, s16 Alias, u16 Unused); anything else → v2 (u16 Unused,
    ''' s16 Alias, FormID). We record the FormID's byte offset so EmitVmad can re-master it.</summary>
    Private Sub WriteObject(bw As BinaryWriter,
                            ms As MemoryStream,
                            globalFormID As UInteger,
                            objectFormat As Short,
                            formIds As List(Of NPC_VmadFormIdRef))
        Dim start = CInt(ms.Position)
        Dim formIdOffset As Integer

        If objectFormat = 1 Then
            formIdOffset = start           ' v1: FormID @ +0
            bw.Write(globalFormID)
            bw.Write(ObjectAliasNone)
            bw.Write(ObjectUnused)
        Else
            formIdOffset = start + 4       ' v2: FormID @ +4
            bw.Write(ObjectUnused)
            bw.Write(ObjectAliasNone)
            bw.Write(globalFormID)
        End If

        ' RawFormID == ResolvedFormID: we author with the global FormID and let EmitVmad map it into
        ' the target plugin's MAST index. Nothing here is source-plugin-encoded.
        formIds.Add(New NPC_VmadFormIdRef With {
            .Offset = formIdOffset, .RawFormID = globalFormID, .ResolvedFormID = globalFormID})
    End Sub

    ''' <summary>u16 length + UTF-8 bytes, no NUL (wbLenString(..., 2); wbEncodingVMAD = UTF-8).</summary>
    Private Sub WriteLenString(bw As BinaryWriter, value As String)
        Dim bytes = PluginEncodingSettings.EncodeVmad(If(value, ""))
        If bytes.Length > UShort.MaxValue Then
            Throw New InvalidDataException($"VMAD string exceeds u16 length: {bytes.Length} bytes.")
        End If
        bw.Write(CUShort(bytes.Length))
        If bytes.Length > 0 Then bw.Write(bytes)
    End Sub

End Module
