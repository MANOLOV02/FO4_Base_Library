Option Strict On
Option Explicit On

''' <summary>THE FALLOUT 4 CONSTANTS OF THE TRANSCRIBED PROGRAMS THAT NO WRITER OF THE EXE FILLS (Ola SSE S1 F2, A2-N): image-space
''' programs no effect ever draws, the b06 forward-lighting floats the row view reads past their writers, the batch renderer's VS cb2
''' floats no writer of its command fills (past [cmd + 0], or inside it with nobody writing them), and the components of a declared name
''' no writer writes (the NOWRITE tables of the generator, marked in the uniform comments). Each is a named seat of the Kind engine-no-writer (SseLightingConstants.SeatKindEngineNoWriter)
''' citing its independent verification; ParityGate fo4-engine-no-writer [census] holds the seats against the generated blocks both
''' ways, [law] holds the b06 row law against the two traced writers and the batch laws against the verified rows and exits. The transcription reads them as uniforms the app never sets.
''' Fallout4.exe 1.11.240.0.</summary>
Friend Module Fo4EngineNoWriter

    ''' <summary>V4 (b06) CONFIRMED with the second writer's bound.</summary>
    Friend Const VerificationB06 As String = "D:\WinTmp\rc\olasse\s1\f2\A2\V\verif_A2N.md"
    ''' <summary>Image-space programs never drawn (group A, which subsumes the float-level V1).</summary>
    Friend Const VerificationNeverDrawn As String = "D:\WinTmp\rc\olasse\s1\f2\A2\V\verif_A2P_is.md"
    ''' <summary>The DOF blur's slot index over the installed IMGS / IMAD (the residue of "never drawn": the exe does not bound it).</summary>
    Friend Const NeverDrawnBlurIndexMeasurement As String = "D:\WinTmp\rc\olasse\s1\f2\A2\N\radio\radio.md"
    ''' <summary>The batch renderer's per-command VS cb2 (0x141820BF0).</summary>
    Friend Const VerificationBatch As String = "D:\WinTmp\rc\olasse\s1\f2\A2\V\verif_141820BF0.md"
    ''' <summary>The batch path's pre-builder, recorder and per-draw patcher (A2-N ronda 2: the rows, the exits, ActorCollision inside
    ''' [cmd + 0], WindVectorEx.w).</summary>
    Friend Const VerificationBatchRecorder As String = "D:\WinTmp\rc\olasse\s1\f2\A2\V\verif_A2N_r2.md"
    ''' <summary>The b05 writers and their unwritten components (ola 3: asm2glsl.EVNOWRITE / EPNOWRITE).</summary>
    Friend Const VerificationNoWriteB05 As String = "D:\WinTmp\rc\ola3\re.md"
    ''' <summary>The b06 / b07 / b10 / b11 writers and their unwritten components (ola 4: asm2glsl.LV / LP / UV / UP / DV / DP / CV / CPNOWRITE;
    ''' the measurement verify.txt beside it).</summary>
    Friend Const VerificationNoWriteOla4 As String = "D:\WinTmp\rc\ola4\v1\re\writers\writers.md"
    ''' <summary>The b09 ClipVolume rows, xyz only (ola 2b: asm2glsl.VNOWRITE[25]).</summary>
    Friend Const VerificationNoWriteB09 As String = "D:\WinTmp\rc\ola2b\re.md"
    ''' <summary>Every verification a seat of this module cites (ParityGate fo4-engine-no-writer [census]: each exists, each is cited).</summary>
    Friend ReadOnly Verifications As String() = {VerificationB06, VerificationNeverDrawn, VerificationBatch, VerificationBatchRecorder,
                                                 VerificationNoWriteB05, VerificationNoWriteOla4, VerificationNoWriteB09}

    ' ---- b06 (V4) ------------------------------------------------------------------------------------------------------------------

    ''' <summary>The technique bit the b06 SetupGeometry 0x14223B240 reads at [BSLightingShader + 0x194] before its first
    ''' DirShadowMapProj writer: SetupTechnique 0x142239ED0 stores the technique there (0x142239F6B: ebp = the technique after the type
    ''' 0x12 -&gt; 9 replacement, 0x142239EEE..F09); bit 1 = SKINNED (the b06 define builder 0x142243D90).</summary>
    Friend Const LightingSkinnedBit As UInteger = 2UI

    ''' <summary>How many of the 12 DirShadowMapProj rows (cb2 rows 25..36) the b06 forward SetupGeometry 0x14223B240 writes in one draw;
    ''' they always start at row 0. Two writers: (1) the main SG, rows 0..3 (a matrix of 0x1406B9610, movups 0x14223B679..684 at
    ''' 4 x table[16]) unless bit 1 of [shader + 0x194] (SKINNED) is set (0x14223B5A4 test / jne 0x14223B688); (2) 0x14223C3C0, rows
    ''' 3i..3i+2 for every cascade i &lt; [light + 0x190] (0x14223C891..8AC; [light + 0x190] = the cascade count, iDirShadowSplits for the
    ''' sun, .data 2 at 0x142F985B8).</summary>
    Friend Function LightingDirShadowRowsWritten(cascades As Integer, technique As UInteger) As Integer
        Dim main = If((technique And LightingSkinnedBit) = 0UI, 4, 0)
#If DEBUG Then
        ' ParityGate fo4-engine-no-writer's directed mutant: the bound before the verification (the cascade writer only)
        If Fo4GBufferConstants.GateMutantOn("b06-dirshadow-cascades-only") Then main = 0
#End If
        Return Math.Max(3 * cascades, main)
    End Function

    ''' <summary>Whether PointLightColor[<paramref name="light"/>].w has a writer in a b06 draw: only row 0 (float(n), 0x14223CE58..E6F;
    ''' 0 when n &lt;= 0, 0x14223B8F5); the light loop 0x14223CEE1..D06D writes .xyz of rows 1..5.</summary>
    Friend Function LightingPointLightColorWWritten(light As Integer) As Boolean
        Return light = 0
    End Function

    ' ---- the batch renderer's VS cb2 (0x141820BF0) ----------------------------------------------------------------------------------

    ''' <summary>Where a float of the VS cb2 a batch-renderer draw reads comes from. The batch path 0x14221F620 / 0x1422204E0 calls
    ''' 0x141820BF0, which binds to VS slot 2 the group-2 pool buffer of the VS's size class ([[state + 0x688] + 0x48], 0x141820C9A..CC3;
    ''' VSSetConstantBuffers(2) 0x141820D2A) and, for every recorded command (loop 0x141820DA2..0x141821555), maps it WRITE_DISCARD
    ''' (0x141820F69), copies the command's [cmd + 0] bytes from cmd + 0x88 + [cmd + 8] (memcpy 0x1422C46B4, 0x141820F76), unmaps
    ''' (0x141820F8F) and only then draws (0x1418214E6 / 0x141821536). Size class 0: no buffer, slot 2 bound to null (edge 0x141820F41 -&gt;
    ''' 0x141820F94), and a read of an unbound constant buffer gives 0 (D3D11). The G-buffer commands are recorded at GetRenderPasses time
    ''' (0x14217A050 calls the pre-builder 0x142207C80 at 0x14217AAA7; the command goes to [pass + 0], 0x14217AAB4) by 0x142230660:
    ''' [cmd + 0] = 16 x [desc + 8] (0x14223069E, 0x14223074B), [cmd + 8] = 0x60 x [[geom + 0x140] + 0x38] (the bones, 0x142230688..68E,
    ''' 0x142230747), a memcpy of [cmd + 0] bytes of the pre-builder's VS block to cmd + 0x88 + [cmd + 8] (0x1422307EB..EF), and
    ''' [cmd + 0x78 / 0x7C / 0x80] = [desc + 0x54 / 0x58 / 0x5C] (0x1422307C4..7D5). The VS block is the pre-builder's own stack,
    ''' align16(rbp + 0x2EF) (0x142207D7F..D93), never cleared (no memset / stos in 0x142207C80) and reached by no callee
    ''' (V\verif_A2N_r2.md 2). Before each draw the per-draw patcher 0x142220E70 writes into the command (BatchPerDrawVsFloats); nothing
    ''' else writes it (V\verif_A2N_r2.md 2). The recorder's other caller, the utility pre-builder 0x1422421D0 (0x1422428FC), fills its own
    ''' stack block (rbp + 0x260) the same way: BatchUtilityVsBlock; the upload and the patcher are the same (they read only the command).</summary>
    Friend Enum BatchVsCb2Float
        ''' <summary>Slot 2 is null: 0.</summary>
        Zero
        ''' <summary>The pre-builder wrote it into its block and the recorder copied it.</summary>
        Recorded
        ''' <summary>The per-draw patcher 0x142220E70 writes it into the command before the upload.</summary>
        PerDraw
        ''' <summary>Below [cmd + 0] and no writer: the stack bytes of the pre-builder's frame (seat BatchInsideSeatName).</summary>
        StackResidue
        ''' <summary>At or past [cmd + 0]: the WRITE_DISCARD map leaves it undefined (seat "fo4 batch vs cb2 bytes past [cmd+0]").</summary>
        NoWriter
    End Enum

    ''' <summary>The VS-table names (k of [[rsp + 0x50] + 0x68 + k]: the VS the hash [r14 + 0xC0] gives for the key 0x142273B80(technique))
    ''' the pre-builder 0x142207C80 reads: 1 0x1422080EE, 2 0x142208147, 3 0x142208351, 7 0x1422083FE, 15 0x142208855, 16 0x14220841E /
    ''' 0x142208832, 17 0x14220843E / 0x142208848, 18 0x1422088BE, 22 0x142208792, 23 0x1422087D8. Not 20 (LandLODParamsVS): 0x14220859E
    ''' reads [[[geom + 0x138] + 0x50] + 0x7C], a byte of the property's object that goes to the PS row k17 (0x14220857D, 0x1422085C5);
    ''' not 4: 0x142208912 reads the PS table [rsp + 0x40] (V\verif_A2N_r2.md 1.2, correction 1). Their rows: BatchBuilderVsBlock.</summary>
    Friend ReadOnly BatchBuilderVsNames As Integer() = {1, 2, 3, 7, 15, 16, 17, 18, 22, 23}

    ''' <summary>Byte k of a VS's table (the float of name k, 0xFF = absent); past the table, absent.</summary>
    Private Function TableByte(table As Byte(), k As Integer) As Integer
        Return If(k < table.Length, CInt(table(k)), &HFF)
    End Function

    ''' <summary>The technique bit the pre-builder's VertexAttribOffsets rows follow. 0x142208705 writes them only when the geometry's vfunc
    ''' [[geom] + 0x1F8] (0x1422086FF) returns non-null; GetRenderPasses ORs 0x18000000 into the technique of the same pass exactly when
    ''' that vfunc of the same geometry is non-null (0x14217A9C7..9D2; [pass + 0x18] = that geometry, 0x14217A9F2), and the only other
    ''' source of bit 28 is geometry type 0xF (0x14217A9AE..9B8: 0x10000140, without bit 27), a type the pre-builder refuses
    ''' (0x142207CDE..CF4). So in the pre-builder, bit 27 set = the vfunc non-null.</summary>
    Friend Const BatchVertexAttribOffsetsBit As UInteger = &H8000000UI

    ''' <summary>What the pre-builder 0x142207C80 leaves for the recorder: the rows ([desc + 8], 0x142208C7C), the floats of its VS block it
    ''' writes, and the per-draw offsets [cmd + 0x7C] / [cmd + 0x80] in bytes (-1 = none: 0x1422083DD / 0x1422083E5).</summary>
    Friend Structure BatchBuilderVs
        Friend Rows As Integer
        Friend Written As HashSet(Of Integer)
        Friend TreeParamsOffset As Integer
        Friend WindOffset As Integer
    End Structure

    ''' <summary>The pre-builder's VS block for a pass of <paramref name="technique"/> whose VS has <paramref name="table"/> (its
    ''' [VS + 0x68 + k] bytes), as the code does it: every table byte as read, 0xFF checked only where the code checks it. A name of n rows
    ''' at float b raises the row count to max(rows, b &gt;&gt; 2 + n).
    ''' <list type="bullet">
    ''' <item>Without bit 2: World, 4 rows at k1 (movups 0x142208125..142, rows 0x14220810B), and PreviousWorld at k2 when k2 &lt;&gt; 0xFF
    ''' (0x14220814B; 0x1422081BD..1DA, rows 0x1422081DF..1E9).</item>
    ''' <item>(technique AND 0x800400) = 0x800000 (0x142208330..33C): WorldView, 4 rows at k3 (0x1422083CA..3D8, rows 0x14220835A..367).</item>
    ''' <item>Bit 10 (0x1422083ED): without bit 23 (0x1422083F8) TreeParams counted (0x1422083FE..41B) and [cmd + 0x7C] = 4 x k7
    ''' (0x142208413); WindVector counted and [cmd + 0x80] = 4 x k16 (0x14220841E..43A); WindVectorEx counted (0x14220843E..44A). None of
    ''' the three is written here.</item>
    ''' <item>BatchVertexAttribOffsetsBit: VertexAttribOffsets1 / 2 when k22 / k23 &lt;&gt; 0xFF, 4 dwords each from [obj + 0x180] /
    ''' [obj + 0x190] (0x1422087C0..7D6, 0x142208809..81C; rows 0x1422087A1..7B3, 0x1422087EF..801).</item>
    ''' <item>Bit 7 (0x142208829): WindVector / WindVectorEx counted and [cmd + 0x80] = 4 x k16 (0x142208832..851); ScaleMask, 1 row at
    ''' k15 (0x14220889F..8B5), and AlphaParam, 1 row at k18 (0x1422088D0..904), counted (0x142208855..878, 0x1422088DD..8F8).</item>
    ''' </list>
    ''' Directed mutant of ParityGate fo4-engine-no-writer (Debug library): batch-builder-writes-actorcollision (ActorCollision written and
    ''' counted, 4 rows at k24).</summary>
    Friend Function BatchBuilderVsBlock(technique As UInteger, table As Byte()) As BatchBuilderVs
        Dim r As New BatchBuilderVs With {.Written = New HashSet(Of Integer), .TreeParamsOffset = -1, .WindOffset = -1}
        Dim put = Sub(k As Integer, n As Integer, write As Boolean)
                      Dim b = TableByte(table, k)
                      r.Rows = Math.Max(r.Rows, (b >> 2) + n)
                      If write Then
                          For i = 0 To 4 * n - 1 : r.Written.Add(b + i) : Next
                      End If
                  End Sub
        If (technique And 4UI) = 0UI Then
            put(1, 4, True)
            If TableByte(table, 2) <> &HFF Then put(2, 4, True)
        End If
        If (technique And &H800400UI) = &H800000UI Then put(3, 4, True)
        If (technique And &H400UI) <> 0UI Then
            If (technique And &H800000UI) = 0UI Then
                put(7, 1, False) : r.TreeParamsOffset = 4 * TableByte(table, 7)
            End If
            put(16, 1, False) : r.WindOffset = 4 * TableByte(table, 16)
            put(17, 1, False)
        End If
        If (technique And BatchVertexAttribOffsetsBit) <> 0UI Then
            If TableByte(table, 22) <> &HFF Then put(22, 1, True)
            If TableByte(table, 23) <> &HFF Then put(23, 1, True)
        End If
        If (technique And &H80UI) <> 0UI Then
            put(16, 1, False) : r.WindOffset = 4 * TableByte(table, 16)
            put(17, 1, False)
            put(15, 1, True)
            put(18, 1, True)
        End If
#If DEBUG Then
        If TableByte(table, 24) <> &HFF AndAlso Fo4GBufferConstants.GateMutantOn("batch-builder-writes-actorcollision") Then put(24, 4, True)
#End If
        Return r
    End Function

    ''' <summary>The state of a draw the per-draw patcher 0x142220E70 reads besides the command.</summary>
    Friend Structure BatchPerDrawState
        ''' <summary>[cmd + 8] = 0, no bones (0x142220ED6..EE6; with bones it writes the palette instead, 0x142230300, below the VS block).</summary>
        Friend NoBones As Boolean
        ''' <summary>[[pass + 0x10] + 0x30] AND 0x8000000000 (0x142220F16..F28).</summary>
        Friend PropertyBit39 As Boolean
        ''' <summary>[[pass + 8] + 0x18] = 4 (0x142220FB4..FBC).</summary>
        Friend ShaderType4 As Boolean
    End Structure

    ''' <summary>The VS-block floats the per-draw patcher 0x142220E70 (rdi = the pass, rsi = [pass + 0] the command, 0x142220EBD) writes into
    ''' the command before the upload; it runs while the byte [0x142F98A10] &lt;&gt; 0 (0x142220EC9; .data 1, and the exe's 5 references
    ''' to it are cmp). With no bones and property bit 39: World as floats 0..15 from the geometry's matrix (0x142220F2E..FB0) and, with
    ''' shader type 4, PreviousWorld as floats 16..31 (cmd + 0xC8 + [cmd + 8], 0x142220FC2..104D) - at those floats, not at k1 / k2.
    ''' [cmd + 0x78] (2 floats, 0x142221051..10D) is -1 from this pre-builder ([desc + 0x54] = -1, 0x142208C72). TreeParams: 4 floats at
    ''' [cmd + 0x7C] (0x142221236..27B) when it is not -1 (0x142221119). WindVector xyzw and the 3 floats after it - WindVectorEx.xyz when
    ''' k17 = k16 + 4 - at [cmd + 0x80] (0x142221285..38B) when it is not -1 (0x142221291); the float after them (WindVectorEx.w) has no
    ''' writer (V\verif_A2N_r2.md 1.2, correction 2) but the World rewrite when it lies in floats 0..15 (a VS without World: bit 2). The
    ''' order is World, [cmd + 0x78], TreeParams, Wind: a later one overwrites an earlier one's float. Directed mutant of ParityGate fo4-engine-no-writer (Debug library):
    ''' batch-perdraw-no-treeparams (the TreeParams row not written).</summary>
    Friend Function BatchPerDrawVsFloats(b As BatchBuilderVs, s As BatchPerDrawState) As HashSet(Of Integer)
        Dim w As New HashSet(Of Integer)
        If s.NoBones AndAlso s.PropertyBit39 Then
            For i = 0 To 15 : w.Add(i) : Next
            If s.ShaderType4 Then
                For i = 16 To 31 : w.Add(i) : Next
            End If
        End If
        Dim tree = b.TreeParamsOffset <> -1
#If DEBUG Then
        If Fo4GBufferConstants.GateMutantOn("batch-perdraw-no-treeparams") Then tree = False
#End If
        If tree Then
            For i = 0 To 3 : w.Add(b.TreeParamsOffset \ 4 + i) : Next
        End If
        If b.WindOffset <> -1 Then
            For i = 0 To 6 : w.Add(b.WindOffset \ 4 + i) : Next
        End If
        Return w
    End Function

    ''' <summary>The source of float <paramref name="floatIndex"/> of the VS cb2 in a batch draw: 0 with the null buffer; at or past
    ''' [cmd + 0] = 16 x b.Rows bytes, no writer; below it, the per-draw patcher's floats, else the pre-builder's, else the stack residue.</summary>
    Friend Function BatchVsCb2Source(floatIndex As Integer, bufferNull As Boolean, b As BatchBuilderVs, perDraw As HashSet(Of Integer)) As BatchVsCb2Float
        If bufferNull Then Return BatchVsCb2Float.Zero
        If floatIndex >= 4 * b.Rows Then Return BatchVsCb2Float.NoWriter
        If perDraw.Contains(floatIndex) Then Return BatchVsCb2Float.PerDraw
        If b.Written.Contains(floatIndex) Then Return BatchVsCb2Float.Recorded
        Return BatchVsCb2Float.StackResidue
    End Function

    ''' <summary>The pass the pre-builder 0x142207C80 reads before it records (rdx = the pass, kept in r15; geom = [pass + 0x18]
    ''' 0x142207CAB; prop = [geom + 0x138] 0x142207CCE).</summary>
    Friend Structure BatchBuilderPass
        ''' <summary>[pass + 0x48] (0x142207CFA).</summary>
        Friend Technique As UInteger
        ''' <summary>Byte [geom + 0x158] (0x142207CDE).</summary>
        Friend GeometryType As Byte
        ''' <summary>Byte [pass + 0x4C] (0x142207D1F).</summary>
        Friend PassKind As Byte
        ''' <summary>[prop + 0x18] &lt;&gt; 0 (0x142207D2A).</summary>
        Friend PropertyField18 As Boolean
        ''' <summary>r = 0x14221D620(pass) (0x142207CD9) non-null and [r + 0x18] &lt;&gt; 0 (0x142207D35..D3F).</summary>
        Friend PassObjectField18 As Boolean
        ''' <summary>[prop + 0x30] (0x142207D4F).</summary>
        Friend PropertyFlags As ULong
        ''' <summary>The VS found in the hash [r14 + 0xC0] with the key 0x142273B80(technique) (0x142207DCC..E28).</summary>
        Friend VsFound As Boolean
        ''' <summary>The PS found in the hash [r14 + 0x150] with the key 0x142273C10(technique) (0x142207E49..FD7).</summary>
        Friend PsFound As Boolean
        ''' <summary>The material's ([prop + 0x58]) current UV set, + 8 x [0x143E5E0B4]: offset +0xC / +0x10, scale +0x1C / +0x20.</summary>
        Friend UvOffsetU As Single
        Friend UvOffsetV As Single
        Friend UvScaleU As Single
        Friend UvScaleV As Single
    End Structure

    ''' <summary>The pre-builder's exits that read only the technique: technique AND 0x580000 (0x142207D09), bit 30 (0x142207D15), bit 24
    ''' (0x142207D45), bit 5 (0x142207D72). They are sufficient, not complete: BatchBuilderRecords holds every exit; a technique that passes
    ''' these is recorded by a pass whose other inputs pass the rest.</summary>
    Friend Function BatchBuilderMayRecord(technique As UInteger) As Boolean
        Return (technique And &H580000UI) = 0UI AndAlso (technique And (1UI << 30)) = 0UI AndAlso (technique And (1UI << 24)) = 0UI AndAlso (technique And &H20UI) = 0UI
    End Function

    ''' <summary>ucomiss / jne against a constant: unordered (NaN) sets ZF, so only an ordered unequal value takes the exit.</summary>
    Private Function OrderedNotEqual(a As Single, c As Single) As Boolean
        Return Not Single.IsNaN(a) AndAlso a <> c
    End Function

    ''' <summary>Whether the pre-builder 0x142207C80 records a command for the pass; every exit goes to 0x142208CEC (xor eax, eax): geometry
    ''' type not 3..5 nor 8 (0x142207CDE..CF4); BatchBuilderMayRecord; [pass + 0x4C] = 0xA (0x142207D1F); [prop + 0x18] &lt;&gt; 0
    ''' (0x142207D2A); [0x14221D620(pass) + 0x18] &lt;&gt; 0 (0x142207D35..D3F); [prop + 0x30] without bit 22 or with bit 49
    ''' (0x142207D4F..D6C); the VS or the PS not found (0x142207E9F..EAD); and, with (technique AND 0x22) = 2 (0x142207EC2..ED7), a UV
    ''' offset other than 0 or a scale other than 1.0 (0x142929458) in the material's current set (0x142207EDD..F0D)
    ''' (V\verif_A2N_r2.md 1.3).</summary>
    Friend Function BatchBuilderRecords(p As BatchBuilderPass) As Boolean
        If Not ((p.GeometryType >= 3 AndAlso p.GeometryType <= 5) OrElse p.GeometryType = 8) Then Return False
        If Not BatchBuilderMayRecord(p.Technique) Then Return False
        If p.PassKind = &HA Then Return False
        If p.PropertyField18 OrElse p.PassObjectField18 Then Return False
        If (p.PropertyFlags And &H400000UL) = 0UL OrElse (p.PropertyFlags And (1UL << 49)) <> 0UL Then Return False
        If Not p.VsFound OrElse Not p.PsFound Then Return False
        If (p.Technique And &H22UI) = 2UI AndAlso (OrderedNotEqual(p.UvOffsetU, 0F) OrElse OrderedNotEqual(p.UvOffsetV, 0F) OrElse
                                                   OrderedNotEqual(p.UvScaleU, 1.0F) OrElse OrderedNotEqual(p.UvScaleV, 1.0F)) Then Return False
        Return True
    End Function

    ' ---- the utility pre-builder 0x1422421D0 (b07 passes of GetRenderPasses 0x14217A050 and of 0x14217AEA0 / 0x14217B8F0 / 0x14217BC60 /
    ' 0x14217BE00; the command at [pass + 0]) ------------------------------------------------------------------------------------------

    ''' <summary>The rows the utility pre-builder counts for a b07 VS name: 0x142277090 (jump table 0x1422770C0): World 4, EyePos /
    ''' ShadowFadeParam / TreeParams / WaterParams / VertexAttribOffsets1 / 2 / WindVector / WindVectorEx 1, ClipVolume 2, the others 0.</summary>
    Friend Function BatchUtilityNameRows(k As Integer) As Integer
        Select Case k
            Case 0 : Return 4
            Case 2, 5, 6, 7, 8, 9, 11, 12 : Return 1
            Case 13 : Return 2
            Case Else : Return 0
        End Select
    End Function

    ''' <summary>The b07 VS-table names (k of [VS + 0x68 + k], the VS the hash [r14 + 0xC0] gives for the key 0x1422776C0(technique) =
    ''' technique AND 0xF7A5FFDF) the utility pre-builder reads: 0 0x142242447, 6 0x14224258E, 11 0x1422425CC, 12 0x1422425FD, 8 0x1422426C5,
    ''' 9 0x142242717.</summary>
    Friend ReadOnly BatchUtilityVsNames As Integer() = {0, 6, 8, 9, 11, 12}

    ''' <summary>The technique bit the utility pre-builder's VertexAttribOffsets rows follow. 0x142242621 writes them only when the
    ''' geometry's vfunc [[geom] + 0x1F8] returns non-null; the utility techniques of 0x14217A050 (0x14217A5F5..600 / 0x14217A6CB..6D6) and
    ''' 0x14217B8F0 (0x14217BB0F..B1A) OR 0xA00000 into the technique exactly then, and bit 23 has no other source in them (0x14217B8F0 sets
    ''' bit 21 alone, 0x2000C0 / 0x200040). The other builders have no source of it either: 0x14217AEA0 (0x14217B5E3..614) and 0x14217BC60
    ''' (0x14217BD1F..D5C) start from the property's own vfunc [[this] + 0x1E8] (0x142179FB0, BSLightingShaderProperty and BSGrassShaderProperty:
    ''' bits 0..6, 26, 30 from the property's flags) and add bit 13, or bit 7 and 0x80000 / 0x2000000; 0x14217BE00 (0x14217BEA3..F4B)
    ''' adds bits 5, 6, 7, 26, 27, 30.</summary>
    Friend Const BatchUtilityVertexAttribOffsetsBit As UInteger = &H800000UI

    ''' <summary>The utility pre-builder's VS block (rbp + 0x260; not cleared) for a pass of <paramref name="technique"/> whose b07 VS has
    ''' <paramref name="table"/>. A name at float b raises the row count to max(rows, b &gt;&gt; 2 + BatchUtilityNameRows(k)).
    ''' <list type="bullet">
    ''' <item>Without bit 2 (0x142242434): World, 4 rows at k0, no 0xFF check (movups 0x1422424E8..521; rows 0x1422424A6..4FD).</item>
    ''' <item>k6 &lt;&gt; 0xFF: TreeParams counted (0x14224258E..5C9) and [cmd + 0x7C] = 4 x k6 ([desc + 0x58], 0x1422428C6..CC); not
    ''' written.</item>
    ''' <item>k11 &lt;&gt; 0xFF: WindVector counted and [cmd + 0x80] = 4 x k11 ([desc + 0x5C], 0x1422428CF..D3), WindVectorEx (k12, no
    ''' 0xFF check) counted (0x1422425CC..61D); not written.</item>
    ''' <item>BatchUtilityVertexAttribOffsetsBit: VertexAttribOffsets1 (k8 &lt;&gt; 0xFF), 4 dwords from [obj + 0x180] (0x1422426C5..715),
    ''' and VertexAttribOffsets2 (k9 &lt;&gt; 0xFF), 4 dwords from [obj + 0x190] (0x142242717..76E) whose .z 0x1417E8EB0 then rewrites
    ''' (0x142242780..785), counted.</item>
    ''' </list>
    ''' [desc + 0x54] = -1 (0x142242869): no [cmd + 0x78].</summary>
    Friend Function BatchUtilityVsBlock(technique As UInteger, table As Byte()) As BatchBuilderVs
        Dim r As New BatchBuilderVs With {.Written = New HashSet(Of Integer), .TreeParamsOffset = -1, .WindOffset = -1}
        Dim put = Sub(k As Integer, write As Boolean)
                      Dim b = TableByte(table, k), n = BatchUtilityNameRows(k)
                      r.Rows = Math.Max(r.Rows, (b >> 2) + n)
                      If write Then
                          For i = 0 To 4 * n - 1 : r.Written.Add(b + i) : Next
                      End If
                  End Sub
        Dim writeWorld = True
#If DEBUG Then
        ' ParityGate fo4-engine-no-writer's directed mutant: World counted but not written
        If Fo4GBufferConstants.GateMutantOn("batch-utility-world-unwritten") Then writeWorld = False
#End If
        If (technique And 4UI) = 0UI Then put(0, writeWorld)
        If TableByte(table, 6) <> &HFF Then
            put(6, False) : r.TreeParamsOffset = 4 * TableByte(table, 6)
        End If
        If TableByte(table, 11) <> &HFF Then
            put(11, False) : r.WindOffset = 4 * TableByte(table, 11)
            put(12, False)
        End If
        If (technique And BatchUtilityVertexAttribOffsetsBit) <> 0UI Then
            If TableByte(table, 8) <> &HFF Then put(8, True)
            If TableByte(table, 9) <> &HFF Then put(9, True)
        End If
        Return r
    End Function

    ''' <summary>The utility pre-builder's exits that read only the technique (the VS id keeps them: 0x1422776C0 clears bits 5, 17, 19, 20,
    ''' 22, 27): bit 9 (0x142242275), bit 28 (0x14224227F), and, with technique AND 0x1C000 = 0 (0x142242533), technique AND 0x1200 = 0x1200
    ''' (0x142242550..55C). Sufficient, not complete: BatchUtilityRecords.</summary>
    Friend Function BatchUtilityMayRecord(technique As UInteger) As Boolean
        If (technique And (1UI << 9)) <> 0UI OrElse (technique And (1UI << 28)) <> 0UI Then Return False
        Return (technique And &H1C000UI) <> 0UI OrElse (technique And &H1200UI) <> &H1200UI
    End Function

    ''' <summary>The pass the utility pre-builder reads besides BatchBuilderPass's fields (whose VsFound / PsFound it does not check).</summary>
    Friend Structure BatchUtilityPass
        Friend Common As BatchBuilderPass
        ''' <summary>Byte [0x143E71B48] &lt;&gt; 0 (0x1422421FA).</summary>
        Friend GlobalSkip As Boolean
        ''' <summary>[[prop + 0x50] + 0x20] &lt;&gt; 0 (0x142242541..54A).</summary>
        Friend PropertyObjectField20 As Boolean
        ''' <summary>[0x143E5DEE8] (0x14224257C).</summary>
        Friend Global5DEE8 As Integer
    End Structure

    ''' <summary>Whether the utility pre-builder 0x1422421D0 records a command; every exit goes to 0x142242903 (xor eax, eax):
    ''' [0x143E71B48] &lt;&gt; 0; geometry type not 3..5 nor 8 (0x14224222E..247); [pass + 0x4C] = 0xA (0x14224224D); [prop + 0x18] &lt;&gt; 0
    ''' (0x14224225A); [0x14221D620(pass) + 0x18] &lt;&gt; 0 (0x142242265..26F); bit 9; bit 28; with bit 1 and technique AND 0x2040280
    ''' (0x142242387..399), a UV offset other than 0 or a scale other than 1.0 in the material's current set (0x14224239F..3D6, ucomiss /
    ''' jne); with technique AND 0x1C000 = 0: bit 20 and [[prop + 0x50] + 0x20] &lt;&gt; 0, technique AND 0x1200 = 0x1200, bit 17, bit 19,
    ''' bit 24 and [0x143E5DEE8] - 0x21 &lt;= 2 unsigned (0x14224253B..588).</summary>
    Friend Function BatchUtilityRecords(p As BatchUtilityPass) As Boolean
        Dim c = p.Common
        Dim t = c.Technique
        If p.GlobalSkip Then Return False
        If Not ((c.GeometryType >= 3 AndAlso c.GeometryType <= 5) OrElse c.GeometryType = 8) Then Return False
        If c.PassKind = &HA OrElse c.PropertyField18 OrElse c.PassObjectField18 Then Return False
        If (t And (1UI << 9)) <> 0UI OrElse (t And (1UI << 28)) <> 0UI Then Return False
        If (t And 2UI) <> 0UI AndAlso (t And &H2040280UI) <> 0UI AndAlso
           (OrderedNotEqual(c.UvOffsetU, 0F) OrElse OrderedNotEqual(c.UvOffsetV, 0F) OrElse OrderedNotEqual(c.UvScaleU, 1.0F) OrElse OrderedNotEqual(c.UvScaleV, 1.0F)) Then Return False
        If (t And &H1C000UI) = 0UI Then
            If (t And (1UI << 20)) <> 0UI AndAlso p.PropertyObjectField20 Then Return False
            If (t And &H1200UI) = &H1200UI OrElse (t And (1UI << 17)) <> 0UI OrElse (t And (1UI << 19)) <> 0UI Then Return False
            If (t And (1UI << 24)) <> 0UI AndAlso p.Global5DEE8 >= &H21 AndAlso p.Global5DEE8 <= &H23 Then Return False   ' eax - 0x21 <= 2 unsigned
        End If
        Return True
    End Function

    ' ---- the seats ------------------------------------------------------------------------------------------------------------------

    ''' <summary>The seat of an image-space program no effect draws (record = the one that names constants).</summary>
    Friend Function NeverDrawnSeatName(record As Integer) As String
        Return $"fo4 is rec{record} never drawn"
    End Function

    Friend Const SeatPointLightColorW As String = "fo4 b06 PointLightColor[1..5].w"
    Friend Const SeatDirShadowRows As String = "fo4 b06 cb2 rows >= 25 + max(3 * cascades, 4)"
    Friend Const SeatBatchTail As String = "fo4 batch vs cb2 bytes past [cmd+0]"

    ''' <summary>The seat of a VS name a batch draw reads inside [cmd + 0] with no writer (BatchVsCb2Float.StackResidue).</summary>
    Friend Function BatchInsideSeatName(name As String) As String
        Return $"fo4 batch vs cb2 {name} inside [cmd+0]"
    End Function

    ''' <summary>The seats. Never drawn (group A): the ISM array [[0x143E5E4B0] + 0x18] slot 107 (HDRDownSample4LightAdapt, 3711 / 3712)
    ''' and 106 (HDRDownSample4LumClamp, 3713 / 3714) are read only by the ISM ctor 0x142186DA0 and RefreshShaders 0x1405F0600 (a reload,
    ''' no draw); slot 63, the effect RainSplash (3897..3902), only by the ctor, and its Render is ret 0 (0x14221CEB0); the DOF blur's
    ''' unbounded slot index stays in 23..29 / 47..53 over the installed IMGS / IMAD (NeverDrawnBlurIndexMeasurement); is_tables
    ''' .IS_NEVER_DRAWN. b06: LightingPointLightColorWWritten and LightingDirShadowRowsWritten, read through the row view when the light
    ''' count passes 6 (asm2glsl.row_view_read); the b06 cb2 is a WRITE_DISCARD map of the size-class pool (0x1421A80F0, 0x1421A813C..163),
    ''' no CPU copy persists between draws. Batch: BatchVsCb2Source NoWriter and StackResidue. NOWRITE: one seat per component set the
    ''' generated texts mark ("engine-no-writer seat: fo4 &lt;block&gt; [rec&lt;r&gt;] &lt;stage&gt; &lt;name&gt;.&lt;components&gt;",
    ''' asm2glsl.nowrite_seat), the generator's assert holding that no class reads them.</summary>
    Friend ReadOnly Seats As (Game As String, Name As String, Kind As String, Seat As String)() = {
        ("FO4", NeverDrawnSeatName(3712), SseLightingConstants.SeatKindEngineNoWriter, "every name of rec3712 (ISM slot 107, HDRDownSample4LightAdapt): never drawn (" & VerificationNeverDrawn & "; " & NeverDrawnBlurIndexMeasurement & ")"),
        ("FO4", NeverDrawnSeatName(3714), SseLightingConstants.SeatKindEngineNoWriter, "every name of rec3714 (ISM slot 106, HDRDownSample4LumClamp): never drawn (" & VerificationNeverDrawn & "; " & NeverDrawnBlurIndexMeasurement & ")"),
        ("FO4", NeverDrawnSeatName(3898), SseLightingConstants.SeatKindEngineNoWriter, "every name of rec3898 (effect RainSplash, ISM slot 63, Render ret 0): never drawn (" & VerificationNeverDrawn & "; " & NeverDrawnBlurIndexMeasurement & ")"),
        ("FO4", NeverDrawnSeatName(3900), SseLightingConstants.SeatKindEngineNoWriter, "every name of rec3900 (effect RainSplash, ISM slot 63, Render ret 0): never drawn (" & VerificationNeverDrawn & "; " & NeverDrawnBlurIndexMeasurement & ")"),
        ("FO4", NeverDrawnSeatName(3901), SseLightingConstants.SeatKindEngineNoWriter, "every name of rec3901 (effect RainSplash, ISM slot 63, Render ret 0): never drawn (" & VerificationNeverDrawn & "; " & NeverDrawnBlurIndexMeasurement & ")"),
        ("FO4", SeatPointLightColorW, SseLightingConstants.SeatKindEngineNoWriter, ".w of PointLightColor rows 1..5: only row 0 has a writer (0x14223CE58..E6F), the loop 0x14223CEE1..D06D writes .xyz (" & VerificationB06 & ")"),
        ("FO4", SeatDirShadowRows, SseLightingConstants.SeatKindEngineNoWriter, "DirShadowMapProj rows from LightingDirShadowRowsWritten on (the main SG's 4 rows 0x14223B679..684 only without SKINNED, 0x14223B5A4; 3 per cascade 0x14223C891..8AC) (" & VerificationB06 & ")"),
        ("FO4", SeatBatchTail, SseLightingConstants.SeatKindEngineNoWriter, "VS cb2 bytes of a batch draw past the command's [cmd + 0]: Map WRITE_DISCARD 0x141820F69, memcpy of [cmd + 0] bytes 0x141820F76 (" & VerificationBatch & ")"),
        ("FO4", BatchInsideSeatName("ActorCollision"), SseLightingConstants.SeatKindEngineNoWriter, "ActorCollision rows 13..16 of the grass VS with TREE_ANIM (rec2181 / 2383, 18 rows recorded): the pre-builder's stack block rsp + 0x4B0..0x4EF, written by nobody before the recording (0x1422307EF copies it) nor after it - the residue of the thread's stack (" & VerificationBatchRecorder & ")"),
        ("FO4", "fo4 b05 ps AlphaTestRef.zw", SseLightingConstants.SeatKindEngineNoWriter, "AlphaTestRef.zw: no writer - the writer of the name (group 2, SG) writes the other components only; asm2glsl.EPNOWRITE[1] and the addresses of its comment (" & VerificationNoWriteB05 & ")"),
        ("FO4", "fo4 b05 ps BaseColorScale.yzw", SseLightingConstants.SeatKindEngineNoWriter, "BaseColorScale.yzw: no writer - the writer of the name (group 1, SM) writes the other components only; asm2glsl.EPNOWRITE[19] and the addresses of its comment (" & VerificationNoWriteB05 & ")"),
        ("FO4", "fo4 b05 ps CameraData.xw", SseLightingConstants.SeatKindEngineNoWriter, "CameraData.xw: no writer - the writer of the name (group 0, ST) writes the other components only; asm2glsl.EPNOWRITE[17] and the addresses of its comment (" & VerificationNoWriteB05 & ")"),
        ("FO4", "fo4 b05 ps DLightColor.w", SseLightingConstants.SeatKindEngineNoWriter, "DLightColor.w: no writer - the writer of the name (group 2, SG) writes the other components only; asm2glsl.EPNOWRITE[16] and the addresses of its comment (" & VerificationNoWriteB05 & ")"),
        ("FO4", "fo4 b05 ps EnvmapScale.yzw", SseLightingConstants.SeatKindEngineNoWriter, "EnvmapScale.yzw: no writer - the writer of the name (group 1, SM) writes the other components only; asm2glsl.EPNOWRITE[21] and the addresses of its comment (" & VerificationNoWriteB05 & ")"),
        ("FO4", "fo4 b05 ps LightingInfluence.zw", SseLightingConstants.SeatKindEngineNoWriter, "LightingInfluence.zw: no writer - the writer of the name (group 1, SM) writes the other components only; asm2glsl.EPNOWRITE[20] and the addresses of its comment (" & VerificationNoWriteB05 & ")"),
        ("FO4", "fo4 b05 ps OcclusionMapSize.yzw", SseLightingConstants.SeatKindEngineNoWriter, "OcclusionMapSize.yzw: no writer - the writer of the name (group 1, SM) writes the other components only; asm2glsl.EPNOWRITE[26] and the addresses of its comment (" & VerificationNoWriteB05 & ")"),
        ("FO4", "fo4 b05 vs EnvcubeSize.yzw", SseLightingConstants.SeatKindEngineNoWriter, "EnvcubeSize.yzw: no writer - the writer of the name (group 2, SG 0x142227287..2AD) writes the other components only; asm2glsl.EVNOWRITE[15] and the addresses of its comment (" & VerificationNoWriteB05 & ")"),
        ("FO4", "fo4 b05 vs EnvcubeVelocity.w", SseLightingConstants.SeatKindEngineNoWriter, "EnvcubeVelocity.w: no writer - the writer of the name (group 2, SG 0x14222708A..0C1) writes the other components only; asm2glsl.EVNOWRITE[16] and the addresses of its comment (" & VerificationNoWriteB05 & ")"),
        ("FO4", "fo4 b05 vs ScaleAdjust.zw", SseLightingConstants.SeatKindEngineNoWriter, "ScaleAdjust.zw: no writer - the writer of the name (group 0, ST 0x1422251EC..22A) writes the other components only; asm2glsl.EVNOWRITE[12] and the addresses of its comment (" & VerificationNoWriteB05 & ")"),
        ("FO4", "fo4 b06 ps AlphaTestRef.yzw", SseLightingConstants.SeatKindEngineNoWriter, "AlphaTestRef.yzw: no writer - the writer of the name (group 2, SG 0x14223B958) writes the other components only; asm2glsl.LPNOWRITE[8] and the addresses of its comment (" & VerificationNoWriteOla4 & ")"),
        ("FO4", "fo4 b06 ps DirLightColor.w", SseLightingConstants.SeatKindEngineNoWriter, "DirLightColor.w: no writer - the writer of the name (group 2, SG>0x14223C3C0 0x14223C4C9..4F0) writes the other components only; asm2glsl.LPNOWRITE[3] and the addresses of its comment (" & VerificationNoWriteOla4 & ")"),
        ("FO4", "fo4 b06 ps DirLightDirection.w", SseLightingConstants.SeatKindEngineNoWriter, "DirLightDirection.w: no writer - the writer of the name (group 2, SG>0x14223C3C0 0x14223C5AD..61E) writes the other components only; asm2glsl.LPNOWRITE[2] and the addresses of its comment (" & VerificationNoWriteOla4 & ")"),
        ("FO4", "fo4 b06 ps EmitColor.w", SseLightingConstants.SeatKindEngineNoWriter, "EmitColor.w: no writer - the writer of the name (group 2, SG 0x14223B7E1..7E5) writes the other components only; asm2glsl.LPNOWRITE[7] and the addresses of its comment (" & VerificationNoWriteOla4 & ")"),
        ("FO4", "fo4 b06 ps EnvmapData.zw", SseLightingConstants.SeatKindEngineNoWriter, "EnvmapData.zw: no writer - the writer of the name (group 1, SM 0x14223AA37..AED) writes the other components only; asm2glsl.LPNOWRITE[19] and the addresses of its comment (" & VerificationNoWriteOla4 & ")"),
        ("FO4", "fo4 b06 ps LightingEffectParams.zw", SseLightingConstants.SeatKindEngineNoWriter, "LightingEffectParams.zw: no writer - the writer of the name (group 1, SM 0x14223B09C..0A5) writes the other components only; asm2glsl.LPNOWRITE[27] and the addresses of its comment (" & VerificationNoWriteOla4 & ")"),
        ("FO4", "fo4 b06 ps MaterialData.yw", SseLightingConstants.SeatKindEngineNoWriter, "MaterialData.yw: no writer - the writer of the name (group 2, SG 0x14223B363..730) writes the other components only; asm2glsl.LPNOWRITE[6] and the addresses of its comment (" & VerificationNoWriteOla4 & ")"),
        ("FO4", "fo4 b06 ps ShadowLightCounts.yzw", SseLightingConstants.SeatKindEngineNoWriter, "ShadowLightCounts.yzw: no writer - the writer of the name (group 2, SG>0x14223C3C0 0x14223C45B) writes the other components only; asm2glsl.LPNOWRITE[13] and the addresses of its comment (" & VerificationNoWriteOla4 & ")"),
        ("FO4", "fo4 b06 vs EyePosition.w", SseLightingConstants.SeatKindEngineNoWriter, "EyePosition.w: no writer - the writer of the name (group 2, SG>0x14223D120 0x14223D19E..23E) writes the other components only; asm2glsl.LVNOWRITE[3] and the addresses of its comment (" & VerificationNoWriteOla4 & ")"),
        ("FO4", "fo4 b07 ps AlphaTestRef.yz", SseLightingConstants.SeatKindEngineNoWriter, "AlphaTestRef.yz: no writer - the writer of the name (group 2, SG 0x142241EBB..0x1422420AC) writes the other components only; asm2glsl.UPNOWRITE[0] and the addresses of its comment (" & VerificationNoWriteOla4 & ")"),
        ("FO4", "fo4 b07 ps RefractionPower.yzw", SseLightingConstants.SeatKindEngineNoWriter, "RefractionPower.yzw: no writer - the writer of the name (group 1, SM 0x142241163) writes the other components only; asm2glsl.UPNOWRITE[1] and the addresses of its comment (" & VerificationNoWriteOla4 & ")"),
        ("FO4", "fo4 b07 vs ClipVolume.w", SseLightingConstants.SeatKindEngineNoWriter, "ClipVolume.w: no writer - the writer of the name (group 0, ST 0x142240D66..D98) writes the other components only; asm2glsl.UVNOWRITE[13] and the addresses of its comment (" & VerificationNoWriteOla4 & ")"),
        ("FO4", "fo4 b07 vs ParabolaParam.zw", SseLightingConstants.SeatKindEngineNoWriter, "ParabolaParam.zw: no writer - the writer of the name (group 0, ST 0x142240BFF..C0B) writes the other components only; asm2glsl.UVNOWRITE[4] and the addresses of its comment (" & VerificationNoWriteOla4 & ")"),
        ("FO4", "fo4 b07 vs WaterParams.yzw", SseLightingConstants.SeatKindEngineNoWriter, "WaterParams.yzw: no writer - the writer of the name (group 2, SG 0x1422418E0) writes the other components only; asm2glsl.UVNOWRITE[7] and the addresses of its comment (" & VerificationNoWriteOla4 & ")"),
        ("FO4", "fo4 b07 vs WindVectorEx.w", SseLightingConstants.SeatKindEngineNoWriter, "WindVectorEx.w: no writer - the writer of the name (group 2, SG 0x142241E67..E8B) writes the other components only; asm2glsl.UVNOWRITE[12] and the addresses of its comment (" & VerificationNoWriteOla4 & ")"),
        ("FO4", "fo4 b09 vs ClipVolume.w", SseLightingConstants.SeatKindEngineNoWriter, "ClipVolume.w: no writer - the writer of the name (group 0, 0x142203878) writes the other components only; asm2glsl.VNOWRITE[25] and the addresses of its comment (" & VerificationNoWriteB09 & ")"),
        ("FO4", "fo4 b09 vs WindVectorEx.w", SseLightingConstants.SeatKindEngineNoWriter, "WindVectorEx.w: no writer - the immediate writer 0x142206FFD..026 and the batch patcher 0x142221285..38B write .xyz only (the patcher's World rewrite of floats 0..15, 0x142220F2E..FB0, lands on it only for a VS without World whose WindVectorEx lies there: rec2169 / 2380); no class reads .w; asm2glsl.VNOWRITE[17] (" & VerificationBatchRecorder & ")"),
        ("FO4", "fo4 b10 ps LightAttenuation.w", SseLightingConstants.SeatKindEngineNoWriter, "LightAttenuation.w: no writer - the writer of the name (group 2, SG 0x1422488B0..8C1; 0x1422495F0 0x142249C34..C47) writes the other components only; asm2glsl.DPNOWRITE[3] and the addresses of its comment (" & VerificationNoWriteOla4 & ")"),
        ("FO4", "fo4 b10 ps ShadowFadeParam.yzw", SseLightingConstants.SeatKindEngineNoWriter, "ShadowFadeParam.yzw: no writer - the writer of the name (group 2, SG 0x1422482C6 / 0x142248BD7) writes the other components only; asm2glsl.DPNOWRITE[12] and the addresses of its comment (" & VerificationNoWriteOla4 & ")"),
        ("FO4", "fo4 b10 ps ShadowLightParam.w", SseLightingConstants.SeatKindEngineNoWriter, "ShadowLightParam.w: no writer - the writer of the name (group 2, SG 0x1422486C5..0x142248D16; 0x1422495F0 0x142249D06) writes the other components only; asm2glsl.DPNOWRITE[13] and the addresses of its comment (" & VerificationNoWriteOla4 & ")"),
        ("FO4", "fo4 b11 ps EnvmapScaleR_CubeMapIdxG.zw", SseLightingConstants.SeatKindEngineNoWriter, "EnvmapScaleR_CubeMapIdxG.zw: no writer - the writer of the name (group 2, DECAL 0x14221840E..479) writes the other components only; asm2glsl.CPNOWRITE[1] and the addresses of its comment (" & VerificationNoWriteOla4 & ")"),
        ("FO4", "fo4 b11 ps ParallaxOcclusion_ScaleR_MaxStepsG.zw", SseLightingConstants.SeatKindEngineNoWriter, "ParallaxOcclusion_ScaleR_MaxStepsG.zw: no writer - the writer of the name (group 2, DECAL 0x1422184C8..4DD) writes the other components only; asm2glsl.CPNOWRITE[6] and the addresses of its comment (" & VerificationNoWriteOla4 & ")"),
        ("FO4", "fo4 b11 ps SSSSSParams.w", SseLightingConstants.SeatKindEngineNoWriter, "SSSSSParams.w: no writer - the writer of the name (group 0, ST 0x142217097..0B2) writes the other components only; asm2glsl.CPNOWRITE[7] and the addresses of its comment (" & VerificationNoWriteOla4 & ")"),
        ("FO4", "fo4 b11 vs BufferStartIdx.yzw", SseLightingConstants.SeatKindEngineNoWriter, "BufferStartIdx.yzw: no writer - the writer of the name (group 2, DECAL 0x142217DA7) writes the other components only; asm2glsl.CVNOWRITE[1] and the addresses of its comment (" & VerificationNoWriteOla4 & ")"),
        ("FO4", "fo4 is rec3883 vs ViewportSize.zw", SseLightingConstants.SeatKindEngineNoWriter, "ViewportSize.zw: no writer - the writer of the name (group 0, SSLRBlurH SetupTechnique 0x1421AC7D0: VS group 0 (0x1421AC8E8..8FC), VS k1 ([VS + 0x69], 0x1421AC90B) = (float(render target width 0x141839830), float(height 0x1418398A0)) (0x1421AC92E..957); VSSetConstantBuffers(0) 0x1421AC9B4) writes the other components only; asm2glsl.is_tables.IS_GROUP0[1] of rec3883 and the addresses of its comment (" & VerificationB06 & ")"),
        ("FO4", "fo4 is rec3885 vs ViewportSize.zw", SseLightingConstants.SeatKindEngineNoWriter, "ViewportSize.zw: no writer - the writer of the name (group 0, the VS cb0 SSLRBlurH binds (0x1421AC9B4; k1 = (float(width), float(height)) 0x1421AC92E..957): the SSLR owner 0x1421F82A0 draws BlurH (0x1421F88E5) and then BlurV (0x1421F8937); BlurV SetupTechnique 0x1421ACA90 and slot 7 0x1421AADD0 bind no VS cb0 and no VSSetConstantBuffers(0) of the D3D context is reachable from its draw (A2-N N\re\blurv_reach_vscb0.txt)) writes the other components only; asm2glsl.is_tables.IS_GROUP0[1] of rec3885 and the addresses of its comment (" & VerificationB06 & ")")}

End Module
