Option Strict On
Option Explicit On

Imports OpenTK.Mathematics

''' <summary>Ola SSE S1 F1.b (A2-M, after the independent verification D:\WinTmp\rc\olasse\s1\f2\A2\V\verif_cb12_engine_no_writer.md):
''' THE PER-FRAME ROTATION OF A CAMERA ENTRY, the state the cb12 rows of the previous frame come from (read only by the motion-vector
''' code, under #ifdef ENGINE_MOTION_VECTORS: the app never defines the symbol and keeps no entry; the law is here so the gates hold it).
''' Both games keep, per camera, an entry of an array (Skyrim SE: [state 0x1433363A0 + 0xB8], count +0xC8, 0x290 bytes each; Fallout 4:
''' [state 0x143D871A0 + 0x140], count +0x150, 0x250 bytes each) with three 64-byte slots of the view x projection before the sub-pixel
''' offset / jitter and three 12-byte slots of the eye. The steps below, in the order each CALLER runs them (EngineCameraCaller,
''' CallerSteps):
''' <list type="number">
''' <item>the entry is found or made and its CURRENT slots written: SSE 0x14101CB50 -&gt; 0x14101E4E0 (+0x140..+0x17F = the view x
''' projection before the offset, 0x14101E64D..684; +0x260..+0x268 = the NiCamera world translate, 0x14101E794..7C2); FO4 0x14182BF20 -&gt;
''' 0x14182D750 (+0x110..+0x14F = the view x projection before the jitter, 0x14182D9D9..A0F; +0x210..+0x218 = [camera + 0xA0..0xA8],
''' 0x14182DAE9..DB27);</item>
''' <item>the ROTATION over every entry of the array (<see cref="Rotate"/>): SSE 0x14101CA60 (called at 0x140657150 in 0x140656E60 after
''' the four 0x14101CB50 at 0x1406570F5..7144, and at 0x140989C34 in 0x1409899C0 after 0x140989C28); FO4 0x14182C050 (called at
''' 0x1421C3F94 in 0x1421C3ED0 after 0x14182BF20 at 0x1421C3F76 / F88, at 0x1421F32DD in 0x1421F30B0 after 0x14182BF20 at
''' 0x1421F32A5 / 2BB / 2D1, and alone at 0x1421F9B1B in 0x1421F9B10); NOT run by 0x140A3DCA0, which calls 0x14182BF20 (0x140A3DD05)
''' and then 0x14182BC80 (0x140A3DD29) with the same camera and flag: there the copy reads the entry with no rotation between;</item>
''' <item>the copy to the frame block and the cb12 writer: SSE 0x14101C660 -&gt; 0x14101E7F0 (+0x180 -&gt; 0x1420D00F0, 0x14101E959..9A7;
''' +0x278 -&gt; [0x1420CFF68..70], 0x14101EA88..AB0; +0x260 -&gt; [0x1420CFF5C..64], 0x14101EAB8..AE0) -&gt; 0x14100AE80 (rows 16..19 =
''' 0x1420D00F0 transposed, 0x14100B007..B08C; row 41, 0x14100B273..2E3; row 40, 0x14100B28D..2D4); FO4 0x14182BC80 (+0x190 -&gt; block
''' +0x860, 0x14182BDFA..E2B; +0x228 -&gt; +0x6C0, 0x14182BE7A..E9F; +0x210 -&gt; +0x6B0, 0x14182BEB5..EEA; +0x110 -&gt; +0x7E0,
''' 0x14182BE32..E63) -&gt; 0x14181E1C0 (rows 31..34 = +0x860 transposed, 0x14181E680..6ED; row 35 .xyz = +0x6B0, 0x14181E70B..729; row 36
''' .xyz = +0x6C0, 0x14181E743..780; rows 37..40 = +0x7E0 transposed, 0x14181E795..80A).</item>
''' </list>
''' So the PREVIOUS slots read after the rotation hold the current slots of the frame before; read with no rotation (0x140A3DCA0) they
''' hold what the last rotation left. The slots are kept in the entry's own
''' layout (each Vector4 = 16 bytes of the entry, in order); <see cref="TransposeRows"/> is the writers' shufps transpose. A slot whose
''' bytes the exe does not determine is Nothing: a new array entry's slots that its maker does not write hold the array buffer's bytes -
''' BSTArray add (SSE 0x140CE0A20, FO4 0x141659940) hands out the slot past the size when the capacity allows (SSE 0x140CE0A32..A3E /
''' FO4 0x141659952..95E: whatever that slot held) or reserves / grows the buffer (SSE 0x140CE0CD0 / 0x140CE0D50, FO4 0x141659BF0 /
''' 0x141659C70) through the memory manager (SSE 0x140CDE250, FO4 0x141657DE0), whose heaps (SSE MemoryHeap 0x140CED210 / 0x140CED230,
''' ZeroOverheadHeap 0x140CE8E10, ScrapHeap 0x140CDF350, BSSmallBlockAllocator 0x140CEA590 / 0x140CEA5A0; FO4 0x1416672B0 / 0x1416672D0,
''' 0x1416629F0, 0x14165BF20, 0x141664710 / 0x141664720) and CRT fallbacks (malloc / _aligned_malloc: SSE IAT 0x1417C8D60 / 0x1417C8D78,
''' FO4 0x1424410A8 / 0x142441098) clear nothing (no memset / calloc / rep stos on those paths; zero only on a page VirtualAlloc has just
''' committed: D:\WinTmp\rc\olasse\s1\f2\A2\M\re\sse_clears.txt / fo4_clears.txt). It depends on the process's heap, not on the exe;
''' confirmed by the independent verification FirstFrameVerification, whose seats are FirstFrameSeats. The first four entries of each
''' array come from the reserve of its static initializer (SSE 0x14101C190 from 0x1400EA27B: 0x140CE0C10 with r9d = 4, 0x14101C2B4;
''' FO4 0x14182B870 from 0x14018A6FB: 0x141659B30 with r9d = 4, 0x14182BA12..BA1F), the same allocator: that block is fresh committed
''' pages (0) or a block freed earlier (its last owner's bytes) by the history of the static initializers that run before it - in
''' the .CRT$XCU table (SSE 0x1417C93E8.., FO4 0x142441948..) 9526 / 12732 initializers precede it, of which 685 / 41 reach the
''' manager's free (SSE 0x140CDE6A0, FO4 0x141658240) or the BSTArray growth that frees the old buffer (SSE 0x140CE0D50, FO4
''' 0x141659C70) by direct calls alone (D:\WinTmp\rc\olasse\s1\f2\A2\M\scripts\initreach.py; their indirect calls - virtual
''' destructors - not followed) - and a reused block carries its last owner's data, pointers into regions the system chooses
''' included. Not a value of the exe either.</summary>
Friend NotInheritable Class EngineCameraMotionEntry

    ''' <summary>The view x projection before the offset / jitter of this frame: SSE entry +0x140..+0x17F, FO4 +0x110..+0x14F.</summary>
    Public Current As Vector4()
    ''' <summary>SSE entry +0x100..+0x13F, FO4 +0x150..+0x18F.</summary>
    Public Middle As Vector4()
    ''' <summary>SSE entry +0x180..+0x1BF (cb12 rows 16..19), FO4 +0x190..+0x1CF (cb12 rows 31..34).</summary>
    Public Previous As Vector4()
    ''' <summary>The eye of this frame: SSE entry +0x260..+0x268 (cb12 row 40), FO4 +0x210..+0x218 (cb12 row 35).</summary>
    Public CurrentEye As Vector3?
    ''' <summary>SSE entry +0x26C..+0x274, FO4 +0x21C..+0x224.</summary>
    Public MiddleEye As Vector3?
    ''' <summary>SSE entry +0x278..+0x280 (cb12 row 41 .xyz), FO4 +0x228..+0x230 (cb12 row 36 .xyz).</summary>
    Public PreviousEye As Vector3?

    ''' <summary>A Skyrim SE entry 0x14101CB50 adds to the array (0x14101CBEB): +0x260..+0x283 zeroed (0x14101CC3B..CC57); the
    ''' view x projection slots +0x100..+0x1BF hold the array buffer's bytes (Nothing: see the class).</summary>
    Friend Shared Function NewSseListEntry() As EngineCameraMotionEntry
        Return New EngineCameraMotionEntry With {.CurrentEye = Vector3.Zero, .MiddleEye = Vector3.Zero, .PreviousEye = Vector3.Zero}
    End Function

    ''' <summary>The Skyrim SE default entry 0x1420D0520 (0x14101C6CA, the camera with no entry in the array): no instruction writes its
    ''' +0x180..+0x1BF (0x1420D06A0..06DF) or +0x278..+0x283 - its one pointer, 0x14101C6CA, goes to 0x14101E4E0 (0x14101C6EB), which writes
    ''' none of them, and to 0x14101E7F0 (0x14101C714), which reads them - and its image bytes there are 0 (.data, initialized up to
    ''' 0x14219D200); the rotation walks only the array, so these slots stay 0.</summary>
    Friend Shared Function SseDefaultEntry() As EngineCameraMotionEntry
        Return New EngineCameraMotionEntry With {.Previous = ZeroRows(), .PreviousEye = Vector3.Zero}
    End Function

    ''' <summary>A Fallout 4 entry 0x14182BF20 adds to the array (0x14182BFB5): it writes only +0x0..+0x17, +0x238 and +0x240
    ''' (0x14182BFD8..C012); every slot of the rotation holds the array buffer's bytes (Nothing: see the class).</summary>
    Friend Shared Function NewFo4ListEntry() As EngineCameraMotionEntry
        Return New EngineCameraMotionEntry()
    End Function

    ''' <summary>The Fallout 4 entry embedded in the state, 0x143D871A0 + 0x160 (0x14182BCF2, the camera with no entry in the array): the
    ''' state is in .bss (past the file bytes of .data, which end at 0x1430E5400: zero at load), its constructor 0x14182B870 (static initializer
    ''' 0x14018A6F4) writes only +0x160..+0x177, +0x398 and +0x3A0 of it (0x14182B99D..9E6), 0x14182D750 none of +0x190..+0x1CF /
    ''' +0x228..+0x233, no rip-relative store reaches them, and the rotation walks only the array: these slots stay 0.</summary>
    Friend Shared Function Fo4EmbeddedEntry() As EngineCameraMotionEntry
        Return New EngineCameraMotionEntry With {.Previous = ZeroRows(), .PreviousEye = Vector3.Zero}
    End Function

    Private Shared Function ZeroRows() As Vector4()
        Return {Vector4.Zero, Vector4.Zero, Vector4.Zero, Vector4.Zero}
    End Function

    ''' <summary>Step 1 of the class: the entry's current slots written (SSE 0x14101E4E0, FO4 0x14182D750).</summary>
    Friend Sub WriteCurrent(entryRows As Vector4(), eye As Vector3)
        Me.Current = CType(entryRows.Clone(), Vector4())
        Me.CurrentEye = eye
    End Sub

    ''' <summary>What one call of <paramref name="caller"/> does to the entry, in its order (CallerSteps): step 1 when it writes, then
    ''' the rotation when it rotates; the copy to the frame block (step 3, the row laws) reads after.</summary>
    Friend Sub Run(caller As EngineCameraCaller, entryRows As Vector4(), eye As Vector3)
        Dim st = CallerSteps(caller)
        Dim rotates = st.Rotates
#If DEBUG Then
        If caller = EngineCameraCaller.Fo4_140A3DCA0 AndAlso Fo4GBufferConstants.GateMutantOn("camera-fo4-a3dca0-rotates") Then rotates = True
#End If
        If st.Writes Then WriteCurrent(entryRows, eye)
        If rotates Then Rotate()
    End Sub

    ''' <summary>The callers of the camera entry and their order (each cited by its calls). Writes = 0x14101CB50 / 0x14182BF20 (step 1);
    ''' Rotates = 0x14101CA60 / 0x14182C050 after it; Reads = 0x14101C660 / 0x14182BC80 after both (the copy the cb12 rows come from).</summary>
    Friend Shared ReadOnly CallerSteps As New Dictionary(Of EngineCameraCaller, (Writes As Boolean, Rotates As Boolean, Reads As Boolean)) From {
        {EngineCameraCaller.Sse_140656E60, (True, True, True)},
        {EngineCameraCaller.Sse_1409899C0, (True, True, True)},
        {EngineCameraCaller.Fo4_1421C3ED0, (True, True, True)},
        {EngineCameraCaller.Fo4_1421F30B0, (True, True, True)},
        {EngineCameraCaller.Fo4_140A3DCA0, (True, False, True)},
        {EngineCameraCaller.Fo4_1421F9B10, (False, True, False)}}

    ''' <summary>The independent verification of the first frame's bytes (the heap's), the file each FirstFrameSeats seat cites.</summary>
    Friend Const FirstFrameVerification As String = "D:\WinTmp\rc\olasse\s1\f2\A2\V\verif_cb12_primer_cuadro.md"

    ''' <summary>The slots a new array entry reads on its first frame that hold the heap's bytes (Nothing in the laws): the names the
    ''' engine has no value for, Kind engine-no-writer (SseLightingConstants.SeatKindEngineNoWriter) with FirstFrameVerification
    ''' (listed in SseLightingConstants.EngineNoWriterVerifications; ParityGate sse-constants-law [census] and engine-camera-motion
    ''' [census] hold them). The SSE previous eye of that frame is 0, a law (0x14101CB50 0x14101CC3B..CC57), not a seat.</summary>
    Friend Shared ReadOnly FirstFrameSeats As (Game As String, Name As String, Kind As String, Seat As String)() = {
        ("SSE", "cb12 entry +0x180..+0x1BF first frame", SseLightingConstants.SeatKindEngineNoWriter, "cb12 rows 16..19 on a new array entry's first frame: the heap's bytes (" & FirstFrameVerification & ")"),
        ("FO4", "cb12 entry +0x190..+0x1CF first frame", SseLightingConstants.SeatKindEngineNoWriter, "cb12 rows 31..34 on a new array entry's first frame: the heap's bytes (" & FirstFrameVerification & ")"),
        ("FO4", "cb12 entry +0x228..+0x233 first frame", SseLightingConstants.SeatKindEngineNoWriter, "cb12 row 36 on a new array entry's first frame: the heap's bytes (" & FirstFrameVerification & ")")}

    ''' <summary>The rotation, the same in both games (SSE 0x14101CA60 with rcx = entry + 0x280, FO4 0x14182C050 with rcx = entry +
    ''' 0x230, movaps / mov copies of the bytes): Previous &lt;- Middle (SSE 0x14101CA90..CAD6: +0x180..+0x1BF &lt;- +0x100..+0x13F; FO4
    ''' 0x14182C080..C0C3: +0x190..+0x1CF &lt;- +0x150..+0x18F), then Middle &lt;- Current (SSE 0x14101CAC1..CAF9; FO4 0x14182C0AE..C0E3);
    ''' PreviousEye &lt;- MiddleEye (SSE 0x14101CB00..CB0F: +0x278..+0x280 &lt;- +0x26C..+0x274; FO4 0x14182C0EA..C0F9: +0x228..+0x230
    ''' &lt;- +0x21C..+0x224), then MiddleEye &lt;- CurrentEye (SSE 0x14101CB11..CB20; FO4 0x14182C0FB..C10A).</summary>
    Friend Sub Rotate()
#If DEBUG Then
        If Fo4GBufferConstants.GateMutantOn("camera-rotation-order") Then
            Middle = Current : Previous = Middle : MiddleEye = CurrentEye : PreviousEye = MiddleEye
            Return
        End If
#End If
        Previous = Middle
        Middle = Current
        PreviousEye = MiddleEye
        MiddleEye = CurrentEye
    End Sub

    ''' <summary>The cb12 writers' 4x4 transpose (SSE 0x14100B007..B08C, FO4 0x14181E680..6ED: shufps 0x44 / 0xEE / 0x88 / 0xDD, lane
    ''' copies): row k = (r0[k], r1[k], r2[k], r3[k]). Nothing for Nothing.</summary>
    Friend Shared Function TransposeRows(r As Vector4()) As Vector4()
        If r Is Nothing Then Return Nothing
        Return {New Vector4(r(0).X, r(1).X, r(2).X, r(3).X), New Vector4(r(0).Y, r(1).Y, r(2).Y, r(3).Y),
                New Vector4(r(0).Z, r(1).Z, r(2).Z, r(3).Z), New Vector4(r(0).W, r(1).W, r(2).W, r(3).W)}
    End Function
End Class

''' <summary>The functions that run the camera entry's steps (EngineCameraMotionEntry.CallerSteps): SkyrimSE.exe 0x140656E60
''' (0x14101CB50 at 0x1406570F5 / 710E / 712F / 7144, 0x14101CA60 at 0x140657150, 0x14101C660 at 0x140657165) and 0x1409899C0
''' (0x140989C28, 0x140989C34, 0x140989C52); Fallout4.exe 0x1421C3ED0 (0x14182BF20 at 0x1421C3F76 / F88, 0x14182C050 at 0x1421C3F94,
''' 0x14182BC80 at 0x1421C3FC2), 0x1421F30B0 (0x1421F32A5 / 2BB / 2D1, 0x1421F32DD, 0x1421F33C0), 0x140A3DCA0 (0x14182BF20 at
''' 0x140A3DD05, 0x14182BC80 at 0x140A3DD29: no rotation) and 0x1421F9B10 (0x14182C050 at 0x1421F9B1B only).</summary>
Friend Enum EngineCameraCaller
    Sse_140656E60
    Sse_1409899C0
    Fo4_1421C3ED0
    Fo4_1421F30B0
    Fo4_140A3DCA0
    Fo4_1421F9B10
End Enum
