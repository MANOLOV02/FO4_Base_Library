Imports OpenTK.Mathematics

''' <summary>THE ENGINE'S SKIN BLEND on the CPU, one law with a row per game (ola SSE S1 F2.4, D4; Fallout 4 since ola 2 v5, user
''' decision 12-oct-2026: "no mezcles camino CPU y GPU, pero deben ser consistentes"): the palette rows the engine VS reads (cb10) and
''' the blend its skinned VS run, one function each, for the GPU route's palette, the view's per-vertex matrix and the CPU route's stream
''' (EngineVertexPacker.PackCpuRoute). What both games share:
'''  * cb10 = 3 rows per bone, row r of bone k = (R(0..2, r) * s, t.r): the bone's world transform composed with its skin-to-bone, in
'''    world space (Fallout4.exe 0x1422303D0; SkyrimSE.exe 1.7.104 UpdateBoneMatrices 0x141015800, rows at 0x141015B33..BFD);
'''  * index k = ftoi(BLENDINDICES.k * 765.01) = 3 * the bone byte: the first of the bone's three rows (FO4 rec2304 L50-51; SSE rec 3902
'''    L64-65, the same in every skinned VS of g5/g6/g7, re_vertices.md 5.2);
'''  * a row index outside the bound buffer reads 0 (D3D11.3 7.5, d3d_cb10).
''' The row of each game (Weights, BlendRow, WeightSum):
'''  * Fallout 4 (Fo4EngineSkin.EngineWeights; rec2304 L47-86): w0..w2 read, w3 = 1 - sat((w1 + w0) + w2); rows chained w1, then
'''    w0 * . + ., w2, w3 (mul, mad, mad, mad);
'''  * Skyrim SE (rec 3902 L62-108, the same law in the 49 lighting, 141 utility and 274 effect VS that read BLENDWEIGHT, re_vertices.md
'''    5.2): the four half weights raw - no normalisation, no clamp, no weight dropped; each row times its weight, added in the order
'''    ((w0 R0 + w1 R1) + w2 R2) + w3 R3 (mul, mul, add, mul, add, mul, add).
''' Both VS add -eye (cb12[40]: FO4 rec2304 L52-53, SSE L62 / L82) to each row's w before weighting, so the vertex they draw is
''' world = sum(w M v) + (1 - S) eye, S = the weight chain alone (s1_re.md 13.1). The CPU blend leaves the eye out (the rows here have
''' none) and its readers add it, in both games: the FO4 CPU-route VS subtracts s * eye (asm2glsl.cpu_route_body), Skyrim SE's view adds
''' (1 - S) * eye (Vertex_SSE), and the CPU's readers of what is drawn - picking (SkinningHelper.DrawnPositions) and the culling boxes
''' (SkinnedGeometry.EyeTMin / EyeTMax, SkinningHelper.EyeTermBox) - take t = 1 - S of every vertex (EyeT, EyeTs).
''' <para>THE ONE GATE (UsesEngineLaw): a 4-slot skin of either game takes this law in every path of the view - the per-vertex skin
''' matrix (the edit views' CPU skinning, picking, bounds, the world cache), the GPU weights, the CPU route's stream - and in the bake
''' (SkinningHelper.BlendBoneMatrices, TryEngineWeights). Skyrim SE's bake does not skin (s1_re.md 12.8).</para></summary>
Public Module EngineSkin

    ''' <summary>Weight slots of the law: the VS reads four BLENDWEIGHT components (FO4 rec2304 L47-49 reads three and computes the fourth;
    ''' SSE rec 3902 reads v4.xyzw) and four BLENDINDICES.</summary>
    Public Const Slots As Integer = 4

    ''' <summary>The game whose row the app's skin takes: the active game (Config_App.Current.Game), the one the gate reads.</summary>
    Public Function LawGame() As Config_App.Game_Enum
        Return Config_App.Current.Game
    End Function

    ''' <summary>cb10 of a draw: 3 rows per bone, row r of bone k = column r of OpenTK's (bones(k) * transform) (the engine's 3x4 bone
    ''' rows, FO4 0x1422303D0, SSE 0x141015B33..BFD; OpenTK multiplies row vectors). <paramref name="rows"/> is resized to 3 * bones.Length.</summary>
    Friend Sub PaletteRows(bones As Matrix4(), transform As Matrix4, ByRef rows As Vector4())
        Dim n = bones.Length
        If rows Is Nothing OrElse rows.Length <> 3 * n Then ReDim rows(3 * n - 1)
        For k = 0 To n - 1
            Dim m = bones(k) * transform
            rows(3 * k) = m.Column0 : rows(3 * k + 1) = m.Column1 : rows(3 * k + 2) = m.Column2
        Next
    End Sub

    ''' <summary>The same rows from the view's pose palette in Double (SkinningHelper.BuildPosePalette): each bone rounded to Single as
    ''' GPUBoneMatrices is (SkinningHelper.AMatrix4), then PaletteRows with no transform.</summary>
    Friend Sub PaletteRows(palette As Matrix4d(), ByRef rows As Vector4())
        Dim bones(palette.Length - 1) As Matrix4
        For k = 0 To palette.Length - 1 : bones(k) = SkinningHelper.AMatrix4(palette(k)) : Next
        PaletteRows(bones, Matrix4.Identity, rows)
    End Sub

    ''' <summary>Skyrim SE's cb10 rows 3 * bones .. 239 (a bone index between the skin's bone count and 80): THE ENGINE HAS NO WRITER for
    ''' them. The four cb10 ring buffers are 0xF00 bytes, DYNAMIC, CONSTANT_BUFFER, CPU WRITE, no initial data (0x141012830..88F,
    ''' ByteWidth at 0x141012865); the one writer of slot 10, 0x141578C80 (the only map of slot 10 among the 40 calls of 0x1410104F0),
    ''' maps the next buffer with D3D11_MAP_WRITE_DISCARD (0x141010536: the previous contents become undefined), copies exactly
    ''' 3 * bones rows (memcpy_s of rows * 16 bytes, 0x141578D30..45), unmaps and binds the whole buffer (VSSetConstantBuffers(10),
    ''' 0x141578D70..79). Verified in the file below; measured over the Skyrim SE Data with a skin (ParityGate sse-editor-box): 0 slots of a
    ''' referenced vertex name a bone past the palette. The copy has no clamp to the buffer: 3 * [[skin + 0x10] + 0x58] rows (0x141578D08..0F)
    ''' = 3 * NiSkinData NumBones (NiSkinData's loader, 0x140F0D764..774), and memcpy_s (0x14010C240 at 0x141578D45) is given rows * 16 as
    ''' both its count and its destination size (0x141578D30..3D): past 80 bones it would write past the 0xF00 mapped bytes, which the engine
    ''' leaves undefined. Measured over the Skyrim SE Data (ParityGate sse-palette-writes, the engine's field): the largest NiSkinData
    ''' NumBones is 78.</summary>
    Friend Const PaletteTailVerification As String = "D:\WinTmp\rc\olasse\s1\f2\C\verif_cb10_tail.md"
    ''' <summary>The seat of those rows, of the Kind of the names the engine does not have (SseLightingConstants.SeatKindEngineNoWriter,
    ''' the Kind of cb2[14]), with its verification.</summary>
    Friend ReadOnly PaletteTailSeat As (Stage As String, Name As String, Kind As String, Seat As String) =
        ("skinned-vs", "cb10 rows 3 * bones .. 239", SseLightingConstants.SeatKindEngineNoWriter, "no writer in SkyrimSE.exe (" & PaletteTailVerification & ")")

    ''' <summary>WHETHER A PARTITION'S DRAW WRITES THE PALETTE (Skyrim SE, C3). The engine draws a skinned geometry partition by partition:
    ''' the pass draw 0x141560340 zeroes the thread's last-written skin ([TLS + 0x2A00], 0x141578E10 called at 0x14156043B) and calls the
    ''' skin instance's slot 37 ([vt + 0x128], 0x14156050E..519), which walks the partitions in order (NiSkinInstance 0x140F09CA0 -&gt;
    ''' 0x140F08460, 0x140F08474..499; BSDismemberSkinInstance 0x140EF6970 skips the ones whose 4-byte entry of its [+0x90] array has a first byte 0, 0x140EF69C0..CB);
    ''' NiSkinPartition::RenderPartition 0x140F084C0 takes partition [skinPartition + 0x18] + 0x50 * index (0x140F084CA..DB) and, past its
    ''' table test (0x140F084E6..FD), calls SetBoneMatrix 0x141578C80 (0x140F0851B: rdx = the skin instance [geom + 0x130], r8 = the
    ''' partition) before drawing its [+0x3A] triangles (0x140F08537 / 0x140F08579). SetBoneMatrix writes cb10 (and cb9, the previous
    ''' bones, 0x141578D7C..DDB) only past its three exits (0x141578CD2..CEB): the skin instance is the last written one; r8 is null; the
    ''' partition's word [r8 + 0x3C] is 0 - its NumBones as NiSkinPartition's loader reads it (0x140F07673, sizing the partition's bone
    ''' list 0x140F076EF..705; 0 after the constructor, 0x140F07492). This is the last two: <paramref name="partitionPresent"/> False is a
    ''' null r8 (no caller of the engine passes one: the other two, 0x141560564 and 0x141560C33, pass a partition built on the stack with
    ''' NumBones 1, 0x141560543..548). Without a write the slot keeps the buffer the last write bound (VSSetConstantBuffers(10) only at
    ''' 0x141578D79): SeesOwnPalette.</summary>
    Friend Function SetBoneMatrixWritesPalette(partitionPresent As Boolean, partitionBones As Integer) As Boolean
#If DEBUG Then
        ' ParityGate sse-palette-writes mutant "palette-zero-writes": a partition with NumBones 0 writes its palette.
        If Fo4GBufferConstants.GateMutantOn("palette-zero-writes") Then Return partitionPresent
#End If
        Return partitionPresent AndAlso partitionBones <> 0
    End Function

    ''' <summary>WHICH PALETTE PARTITION <paramref name="k"/>'s DRAW SEES (Skyrim SE, C3), over the partitions drawn in order with
    ''' <paramref name="writes"/> (SetBoneMatrixWritesPalette of each): its own skin's when some partition 0..k writes - the first write
    ''' of the pass draw binds this skin's palette and every later partition of the same skin leaves by the same-skin exit (0x141578CD2,
    ''' [TLS + 0x2A00] set at 0x141578CF1 and zeroed for each pass draw at 0x14156043B) -, otherwise the palette the previous write of the
    ''' frame bound (another draw: the slot is not touched, 0x141578CDC..CEB). The preview draws every partition (the engine's two filters
    ''' above, the dismember bytes and RenderPartition's table, are runtime state it does not have).</summary>
    Friend Function SeesOwnPalette(writes As IReadOnlyList(Of Boolean), k As Integer) As Boolean
#If DEBUG Then
        ' ParityGate sse-palette-writes mutant "palette-carry-own": every partition sees its own skin's palette.
        If Fo4GBufferConstants.GateMutantOn("palette-carry-own") Then Return True
#End If
        For j = 0 To k
            If writes(j) Then Return True
        Next
        Return False
    End Function

    ''' <summary>WHETHER A SKINNED DRAW WRITES THE PALETTE (Fallout 4, C3). The cb10 writer 0x1422303D0 takes the geometry's skin
    ''' [geom + 0x140] and leaves without mapping or binding cb10 when it is null or its [skin + 0x38] is 0 (0x14223041A..41D); else it maps
    ''' the buffer of 3 * [skin + 0x38] rows (0x1422304B3, the per-row-count table 0x1422304E4) and copies that many rows. [skin + 0x38] is
    ''' the size of BSSkin::Instance's second array (+0x28), set with the bones array's (+0x20) by 0x1416D8650 (0x1416D86EF) from the
    ''' NumBones LoadBinary reads (vtable 0x1426866F8 slot 27, 0x1416D7749..753). Without a write the slot keeps the buffer the last write
    ''' bound: the draw sees the previous skinned draw's palette (one draw per geometry: no partitions).</summary>
    Friend Function Fo4SkinWritesPalette(skinBones As Long) As Boolean
#If DEBUG Then
        ' ParityGate fo4-palette-writes mutant "fo4-palette-zero-writes": a skin with NumBones 0 writes its palette.
        If Fo4GBufferConstants.GateMutantOn("fo4-palette-zero-writes") Then Return True
#End If
        Return skinBones <> 0
    End Function

    ''' <summary>A BLENDINDICES byte as the input assembler hands it to the VS: UNORM8 = b / 255 (D3D11.3 3.2.3.6).</summary>
    Friend Function Unorm8(b As Byte) As Single
        Return CSng(b) / 255.0F
    End Function

    ''' <summary>THE GATE, one for the view and the bake: a 4-slot skin of Fallout 4 or Skyrim SE (the active game). The law is 4-slot
    ''' (FO4 rec2304 L47-61, SSE rec 3902 L62-108): a skin with another slot count has none to replicate (the converters of both games
    ''' build 4: SSE P3 0x14101AB30 copies 16 bytes of weights per vertex, s1_re.md 12.5).</summary>
    Public Function UsesEngineLaw(weightsPerVertex As Integer) As Boolean
        If weightsPerVertex <> Slots OrElse Config_App.Current Is Nothing Then Return False
        Dim g = Config_App.Current.Game
        Return g = Config_App.Game_Enum.Fallout4 OrElse g = Config_App.Game_Enum.Skyrim
    End Function

    ''' <summary>The engine's four weights of a vertex from the four BLENDWEIGHT components the VS reads (R16G16B16A16_FLOAT: the half
    ''' values, exact in Single), by the row of <paramref name="game"/>: Fallout 4 computes w3 (Fo4EngineSkin.EngineWeights), Skyrim SE
    ''' reads all four raw (rec 3902 L67 / L69 / L74 / L73: v4.x, v4.y, v4.z, v4.w).</summary>
    Public Sub EngineWeights(game As Config_App.Game_Enum, w0 As Single, w1 As Single, w2 As Single, w3 As Single, w() As Single)
        If game = Config_App.Game_Enum.Fallout4 Then
            Fo4EngineSkin.EngineWeights(w0, w1, w2, w)
        Else
            w(0) = w0 : w(1) = w1 : w(2) = w2 : w(3) = w3
#If DEBUG Then
            ' ParityGate sse-d4-law mutant "d4-normalize": the four weights divided by their sum (the app's Skyrim SE law before D4).
            If Fo4GBufferConstants.GateMutantOn("d4-normalize") Then
                Dim s As Single = ((w0 + w1) + w2) + w3
                If s <> 0.0F Then w(0) = w0 / s : w(1) = w1 / s : w(2) = w2 / s : w(3) = w3 / s
            End If
#End If
        End If
    End Sub

    ''' <summary>The bake's entry to the law (SkinningHelper.BlendBoneMatrices): the four weights of the vertex at
    ''' <paramref name="baseSlot"/> of the flat half array, EngineWeights of the active game; False (<paramref name="w"/> untouched) when
    ''' the law does not apply (UsesEngineLaw) or the slots are out of the array.</summary>
    Friend Function TryEngineWeights(flatWgt As System.Half(), baseSlot As Integer, wpv As Integer, w() As Single) As Boolean
        If flatWgt Is Nothing OrElse w Is Nothing OrElse w.Length < Slots OrElse Not UsesEngineLaw(wpv) Then Return False
        If baseSlot < 0 OrElse baseSlot + Slots > flatWgt.Length Then Return False
        EngineWeights(LawGame(), CSng(flatWgt(baseSlot)), CSng(flatWgt(baseSlot + 1)), CSng(flatWgt(baseSlot + 2)), CSng(flatWgt(baseSlot + 3)), w)
        Return True
    End Function

    ''' <summary>Does slot weight <paramref name="w"/> enter the bake's accumulation (SkinningHelper.BlendBoneMatrices)? Fallout 4: only a
    ''' weight &gt; 0 (the CPU SkinBlend of the engine and the CK, CreationKit.exe 0x142B822E0 = Fallout4.exe 0x141837850). Skyrim SE:
    ''' every weight, zero and negative included (rec 3902: mul and add of every slot, nothing dropped, re_vertices.md 5.2).</summary>
    Friend Function BakeSlotCounts(game As Config_App.Game_Enum, w As Single) As Boolean
        Return game <> Config_App.Game_Enum.Fallout4 OrElse w > 0.0F
    End Function

    ''' <summary>The homogeneous anchor (M44 = 1) of a matrix that came out of a weighted blend: call it before inverting a skinning
    ''' Mtot. The blend scales the 16 elements, M44 included (M44 = sum of the weights); the engine blends a 3x4 whose row 3 stays
    ''' [0, 0, 0, 1] (SkinBlend 0x142B822E0), so Matrix4d.Invert would otherwise bring in a 1/s that cancels the law.</summary>
    Public Function ReanchorAffine(m As Matrix4d) As Matrix4d
        m.M44 = 1.0
        Return m
    End Function

    ''' <summary>Blend over the app's skin arrays (the bytes and halves the engine stage packs: EngineVertexPacker.PackVertex), slots
    ''' <paramref name="baseSlot"/>..+3, as the VS of <paramref name="game"/> reads them; the one blend of the view (SkinningHelper's
    ''' per-vertex matrix) and of the CPU route.</summary>
    Friend Sub BlendSkin(game As Config_App.Game_Enum, rows As Vector4(), boneIndices As Byte(), boneWeights As System.Half(), baseSlot As Integer, w() As Single,
                         ByRef r0 As Vector4, ByRef r1 As Vector4, ByRef r2 As Vector4, ByRef s As Single)
        Blend(game, rows, New Vector4(Unorm8(boneIndices(baseSlot)), Unorm8(boneIndices(baseSlot + 1)), Unorm8(boneIndices(baseSlot + 2)), Unorm8(boneIndices(baseSlot + 3))),
              New Vector4(CSng(boneWeights(baseSlot)), CSng(boneWeights(baseSlot + 1)), CSng(boneWeights(baseSlot + 2)), CSng(boneWeights(baseSlot + 3))),
              w, r0, r1, r2, s)
    End Sub

    ''' <summary>The first palette row of a BLENDINDICES component as the VS reads it (UNORM8 = b / 255, D3D11.3 3.2.3.6; FO4 rec2304
    ''' L50-51, SSE rec 3902 L64-65): ftoi(x * 765.01), toward zero (NaN -&gt; 0 and the int range, D3D11.3 ftoi).</summary>
    Friend Function RowIndex(x As Single) As Integer
        Dim f As Single = x * 765.01F
        If Single.IsNaN(f) Then Return 0
        If f >= 2147483648.0F Then Return Integer.MaxValue
        If f <= -2147483648.0F Then Return Integer.MinValue
        Return CInt(Math.Truncate(f))
    End Function

    ''' <summary>Row <paramref name="i"/> of cb10 as the VS reads it: a row outside the bound buffer reads 0 (D3D11.3 7.5, d3d_cb10). The
    ''' one read of the palette: Blend and ShadowGate G5b's oracle. Fallout 4 binds exactly the 3 * bones rows (0x1422304B3..4E4): past
    ''' them, 0. Skyrim SE binds the whole 0xF00-byte ring buffer (240 rows) and writes only 3 * bones of them: rows 3 * bones .. 239 have
    ''' no writer in the engine (PaletteTailSeat) - no value to take, the app reads 0 there too; row 240 on is outside the buffer, 0.</summary>
    Friend Function PaletteRow(rows As Vector4(), i As Integer) As Vector4
        If i < 0 OrElse i >= rows.Length Then Return Vector4.Zero         ' D3D11.3 7.5 (d3d_cb10)
        Return rows(i)
    End Function

    ''' <summary>The blended rows 0..2 of one vertex and s = the same chain over the weights (the order of <paramref name="game"/>'s row,
    ''' see the module), from BLENDINDICES / BLENDWEIGHT as the VS reads them. <paramref name="w"/> is a 4-Single scratch.</summary>
    Friend Sub Blend(game As Config_App.Game_Enum, rows As Vector4(), indices As Vector4, weights As Vector4, w() As Single,
                     ByRef r0 As Vector4, ByRef r1 As Vector4, ByRef r2 As Vector4, ByRef s As Single)
        EngineWeights(game, weights.X, weights.Y, weights.Z, weights.W, w)
        Dim i0 = RowIndex(indices.X), i1 = RowIndex(indices.Y), i2 = RowIndex(indices.Z), i3 = RowIndex(indices.W)
        r0 = BlendRow(game, rows, 0, i0, i1, i2, i3, w)
        r1 = BlendRow(game, rows, 1, i0, i1, i2, i3, w)
        r2 = BlendRow(game, rows, 2, i0, i1, i2, i3, w)
        s = WeightSum(game, w)
    End Sub

    ''' <summary>s of a vertex: the blend's chain over the four engine weights alone, in <paramref name="game"/>'s order (Fallout 4: w1,
    ''' then w0 + ., w2 + ., w3 + .; Skyrim SE: ((w0 + w1) + w2) + w3) - the factor of the eye (FO4: the CPU-route VS subtracts s * eye,
    ''' asm2glsl.cpu_route_body).</summary>
    Public Function WeightSum(game As Config_App.Game_Enum, w() As Single) As Single
        If game = Config_App.Game_Enum.Fallout4 Then
            Dim s As Single = w(1)
            s = w(0) + s
            s = w(2) + s
            s = w(3) + s
            Return s
        End If
        Dim t As Single = w(0) + w(1)
        t = t + w(2)
        t = w(3) + t
        Return t
    End Function

    ''' <summary>Row <paramref name="r"/> of one vertex's blend. Fallout 4 (rec2304 L54-61): ((w1 a then w0 b + . then w2 c + . then
    ''' w3 d + .), mul then three mad. Skyrim SE (rec 3902 L66-78 / L84-95 / L97-107, without the eye term the CPU leaves out): a * w0,
    ''' b * w1 added, c * w2 added, then d * w3 + . - each product rounded before its add (mul then add, precise).</summary>
    Private Function BlendRow(game As Config_App.Game_Enum, rows As Vector4(), r As Integer, i0 As Integer, i1 As Integer, i2 As Integer, i3 As Integer, w() As Single) As Vector4
        If game = Config_App.Game_Enum.Fallout4 Then
            Dim a = PaletteRow(rows, i1 + r), b = PaletteRow(rows, i0 + r), c = PaletteRow(rows, i2 + r), d = PaletteRow(rows, i3 + r)
            Dim x As Single = a.X * w(1), y As Single = a.Y * w(1), z As Single = a.Z * w(1), t As Single = a.W * w(1)
            x = w(0) * b.X + x : y = w(0) * b.Y + y : z = w(0) * b.Z + z : t = w(0) * b.W + t
            x = w(2) * c.X + x : y = w(2) * c.Y + y : z = w(2) * c.Z + z : t = w(2) * c.W + t
            x = w(3) * d.X + x : y = w(3) * d.Y + y : z = w(3) * d.Z + z : t = w(3) * d.W + t
            Return New Vector4(x, y, z, t)
        End If
        Dim p0 = PaletteRow(rows, i0 + r), p1 = PaletteRow(rows, i1 + r), p2 = PaletteRow(rows, i2 + r), p3 = PaletteRow(rows, i3 + r)
        Dim sx As Single = p0.X * w(0), sy As Single = p0.Y * w(0), sz As Single = p0.Z * w(0), sw As Single = p0.W * w(0)
        Dim qx As Single = p1.X * w(1), qy As Single = p1.Y * w(1), qz As Single = p1.Z * w(1), qw As Single = p1.W * w(1)
        sx = sx + qx : sy = sy + qy : sz = sz + qz : sw = sw + qw
        qx = p2.X * w(2) : qy = p2.Y * w(2) : qz = p2.Z * w(2) : qw = p2.W * w(2)
        sx = sx + qx : sy = sy + qy : sz = sz + qz : sw = sw + qw
        qx = p3.X * w(3) : qy = p3.Y * w(3) : qz = p3.Z * w(3) : qw = p3.W * w(3)
        Return New Vector4(qx + sx, qy + sy, qz + sz, qw + sw)
    End Function

    ''' <summary>OpenTK's matrix of blended engine rows r0..r2 (column r = row r; row 3 of the engine's 3x4 = (0, 0, 0, 1)), the
    ''' form FastSkin's law reads (FastSkin.Punto: p * m).</summary>
    Friend Function MatrixOfRows(r0 As Vector4, r1 As Vector4, r2 As Vector4) As Matrix4
        Return New Matrix4(r0.X, r1.X, r2.X, 0.0F,
                           r0.Y, r1.Y, r2.Y, 0.0F,
                           r0.Z, r1.Z, r2.Z, 0.0F,
                           r0.W, r1.W, r2.W, 1.0F)
    End Function

    ''' <summary>The blended engine rows r0..r2 of OpenTK's matrix <paramref name="m"/> in the form MatrixOfRows builds (row r =
    ''' column r): the inverse of MatrixOfRows, exact (the same 12 Single).</summary>
    Friend Sub RowsOfMatrix(m As Matrix4, ByRef r0 As Vector4, ByRef r1 As Vector4, ByRef r2 As Vector4)
        r0 = New Vector4(m.M11, m.M21, m.M31, m.M41)
        r1 = New Vector4(m.M12, m.M22, m.M32, m.M42)
        r2 = New Vector4(m.M13, m.M23, m.M33, m.M43)
    End Sub

    ''' <summary>A vertex vector the engine VS decodes and normalises before it skins it (FO4 rec2304 L94-98 / L106-109): dp3, rsq, mul,
    ''' in Single.</summary>
    Friend Function NormalizeLikeVs(v As Vector3) As Vector3
        Dim d As Single = v.X * v.X + v.Y * v.Y
        d = d + v.Z * v.Z
        Dim r = 1.0F / MathF.Sqrt(d)
        Return New Vector3(r * v.X, r * v.Y, r * v.Z)
    End Function

    ''' <summary>t = 1 - S of a vertex for the eye term (s1_re.md 13.1: world = sum(w M v) + t * eye), with S = the sum of the four
    ''' engine weights of its row (EngineWeights: Skyrim SE's raw halves; Fallout 4's three halves and w3 = 1 - sat(.) in Single) in
    ''' Double. Every one of them is a multiple of 2^-24 below 4 in magnitude (a half; a Single &gt;= 0.5, or 1 - acc exact by Sterbenz),
    ''' so the sum of four needs at most 27 significant bits and does not round in Double (rev-25): t is exact.</summary>
    Friend Function EyeT(w0 As Single, w1 As Single, w2 As Single, w3 As Single) As Double
#If DEBUG Then
        ' ParityGate sse-editor-box mutant "smax-float32": the sum in Single (the first measurement's, short in 103 shapes, rev-25).
        If Fo4GBufferConstants.GateMutantOn("smax-float32") Then
            Dim sf As Single = w0 + w1
            sf = sf + w2
            sf = sf + w3
            Return 1.0 - CDbl(sf)
        End If
#End If
        Return 1.0 - (((CDbl(w0) + CDbl(w1)) + CDbl(w2)) + CDbl(w3))
    End Function

    ''' <summary>EyeT of every vertex of a draw skin (4 weights per vertex as the VS reads them, <paramref name="weights"/> = the
    ''' engine's weights of the row, SkinnedGeometry.GPUBoneWeights): the per-vertex t of SkinnedGeometry.EyeT. Nothing when the
    ''' array does not hold the vertices.</summary>
    Friend Function EyeTs(weights As Single(), vertexCount As Integer) As Double()
        If weights Is Nothing OrElse weights.Length < 4 * vertexCount Then Return Nothing
        Dim t(vertexCount - 1) As Double
        For v = 0 To vertexCount - 1
            t(v) = EyeT(weights(4 * v), weights(4 * v + 1), weights(4 * v + 2), weights(4 * v + 3))
        Next
        Return t
    End Function
End Module
