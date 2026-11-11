Imports OpenTK.Mathematics
Imports SysHalf = System.Half

''' <summary>THE ENGINE VERTEX BUFFER of a shape: ONE packer with a row per game (ola SSE S1 F2.3, regla 5); the layout it fills is
''' EngineVertexLayout.
''' <list type="bullet">
''' <item>BSTriShape, both games: THE NIF WRITER'S VERTEX BYTES, for what the app edited or recalculated (RenderableMesh.PackOne; user decision 12-oct-2026, revisor-21: NiflySharp is not
''' modified; gate N1 compares a packed Fallout 4 shape byte for byte against NiflySharp's own writer). What it did not edit, with Recalculate normals off, is the block's own bytes (user decision
''' 5-nov-2026). The engines copy that buffer raw: Fallout 4 by 0x141818910 (read 0x1418189B4..C1, copied 0x14184F65B..667, 0x14185209D, 0x14185214C); Skyrim SE through BSTriShape LoadBinary and
''' SkyrimSE.exe 0x14100B530, which reads ((desc * 4) AND &amp;H3C) * nVerts bytes and creates the buffer without converting them. Layout = the order
''' of the writer's Sync of each game, which is the engine's input-layout offsets: BSVertexData.Sync (NiflySharp Structs\BSVertexData.cs:46-116)
''' and BSVertexDataSSE.Sync (Structs\BSVertexDataSSE.cs:40-95). The writer's rules (BSTriShape.cs): positions (Half)x or float
''' (:620-627; BSVertexDataSSE always float), normal / tangent / bitangent Y,Z (sbyte)Math.Round((x + 1) / 2 * 255) (:653-655, :693-695,
''' :736-737), bitangent X (Half)x or float (:733-735; BSVertexDataSSE always float), colours (byte)Math.Floor(v == 1 ? 255 : v * 256)
''' (:818-827), UV (Half)u. A BSDynamicTriShape's slot-1 stream is the writer's CalcDynamicData (Blocks\BSDynamicTriShape.cs:28-80).</item>
''' <item>A v83 NiTriShape of Skyrim SE: the engine's own converter (P3, re_vertices.md 3), region "P3" below.</item>
''' </list>
''' The app's arrays map through IShapeGeometry's swap (BSTriShapeGeometry.vb:388-398; NiTriShapeGeometry GetTangents / GetBitangents):
''' app Tangents -> NIF bitangent (w-packed Cx), app Bitangents -> NIF tangent (BINORMAL).</summary>
Friend Module EngineVertexPacker

    ''' <summary>The writer's byte of a normal component (U1, H11): (sbyte)Math.Round(double) - MidpointRounding.ToEven - unchecked on
    ''' net8 x64 (FO4_Base_Library.vbproj:5, NiflySharp net8.0 build): low byte of the int32 truncation (cvttsd2si; NaN and out of range
    ''' -> 0x80000000 -> 0).</summary>
    Friend Function NormalByte(c As Single) As Byte
        Dim d = Math.Round(CDbl((c + 1.0F) / 2.0F * 255.0F))
        If Double.IsNaN(d) OrElse d >= 2147483648.0 OrElse d < -2147483648.0 Then Return 0
        Return CByte(CLng(Math.Truncate(d)) And &HFFL)
    End Function

    ''' <summary>The writer's colour byte: (byte)Math.Floor(v == 1 ? 255 : v * 256), same unchecked conversion.</summary>
    Friend Function ColorByte(v As Single) As Byte
        Dim d = Math.Floor(CDbl(If(v = 1.0F, 255.0F, v * 256.0F)))
        If Double.IsNaN(d) OrElse d >= 2147483648.0 OrElse d < -2147483648.0 Then Return 0
        Return CByte(CLng(d) And &HFFL)
    End Function

    ''' <summary>The value a normal component has after the NIF's byte: NormalByte decoded as UNORM (D3D11.3 3.2.3.6) * 2 - 1, as the
    ''' engine VS decodes it (rec2273 L60-61). ONE quantisation for every FO4 upload (propuesta-v4 1, row 1).</summary>
    Friend Function RoundTrip(v As Vector3) As Vector3
        Return New Vector3(RoundTripComponent(v.X), RoundTripComponent(v.Y), RoundTripComponent(v.Z))
    End Function

    ''' <summary>One component of RoundTrip: NormalByte decoded as UNORM * 2 - 1. The one round trip of a normal byte (review rev-06 d):
    ''' RoundTrip and SkinningHelper.NormalComoLaVeElCanonico (the TBN's raw normal).</summary>
    Friend Function RoundTripComponent(c As Single) As Single
        Return NormalByte(c) / 255.0F * 2.0F - 1.0F
    End Function

    ''' <summary>The position the NIF holds: Half per component when the vertex desc is not full precision (flag 0x400 clear).</summary>
    Friend Function HalfRound(v As Vector3d, fullPrecision As Boolean) As Vector3d
        If fullPrecision Then Return v
        Return New Vector3d(CSng(CType(CSng(v.X), SysHalf)), CSng(CType(CSng(v.Y), SysHalf)), CSng(CType(CSng(v.Z), SysHalf)))
    End Function

    Private Sub W16(b As Byte(), o As Integer, v As Short)
        b(o) = CByte(v And &HFF) : b(o + 1) = CByte((v >> 8) And &HFF)
    End Sub
    Private Sub W32(b As Byte(), o As Integer, v As Integer)
        b(o) = CByte(v And &HFF) : b(o + 1) = CByte((v >> 8) And &HFF) : b(o + 2) = CByte((v >> 16) And &HFF) : b(o + 3) = CByte((v >> 24) And &HFF)
    End Sub
    Private Sub WHalf(b As Byte(), o As Integer, x As Single)
        W16(b, o, BitConverter.HalfToInt16Bits(CType(x, SysHalf)))
    End Sub
    Private Sub WFloat(b As Byte(), o As Integer, x As Single)
        W32(b, o, BitConverter.SingleToInt32Bits(x))
    End Sub

    ''' <summary>The position of a vertex of flags <paramref name="fl"/> (desc &gt;&gt; 44) is floats, not halves: always in Skyrim SE's format (BSVertexDataSSE.Sync), with flag 0x400 in Fallout 4's
    ''' (BSVertexData.Sync; the input layout's format 0x141823225..25F). The one decision of PackVertex, PackPosition, BasePositionW and the stage's precision (RenderableMesh).</summary>
    Friend Function PositionIsFloat(game As Config_App.Game_Enum, fl As ULong) As Boolean
        Return game = Config_App.Game_Enum.Skyrim OrElse (fl And &H400UL) <> 0UL
    End Function

    ''' <summary>One vertex in the NIF / engine layout of <paramref name="desc"/> at <paramref name="i"/> * stride, the order of the
    ''' writer's Sync of <paramref name="game"/> (flags = desc &gt;&gt; 44). <paramref name="weights"/> Nothing = no skin stream written.
    ''' <paramref name="unusedW"/>: the bytes after the position of a vertex without the bitangent, the NIF's (BasePositionW), in both rows.</summary>
    Friend Sub PackVertex(game As Config_App.Game_Enum, stage As Byte(), desc As ULong, i As Integer, pos As Vector3d, nrm As Vector3,
                          tanApp As Vector3, bitApp As Vector3, uv As Vector2, col As Vector4, weights As SysHalf(), indices As Byte(),
                          skinBase As Integer, eye As Single, unusedW As UInteger)
        Dim fl = desc >> 44
        Dim o = i * EngineVertexLayout.Stride(desc)
        Dim cx = tanApp                                   ' NIF bitangent (BSTriShapeGeometry.vb:388-389)
        ' BSVertexDataSSE.Sync (:42-56) and BSVertexData.Sync (:46-74): the position with flag 0x1, three floats or three halves (PositionIsFloat), then the bitangent's X with 0x10, else the NIF's own
        ' bytes (<paramref name="unusedW"/>, BasePositionW): the engines copy the buffer raw (SkyrimSE.exe 0x14100B530, Fallout4.exe 0x141818910).
        If (fl And &H1UL) <> 0UL Then
#If DEBUG Then
            ' ParityGate sse-stream100-unusedw mutant "sse-unusedw-zero": the writer's 0 of a new struct in place of the NIF's bytes.
            If Fo4GBufferConstants.GateMutantOn("sse-unusedw-zero") Then unusedW = 0UI
#End If
            If PositionIsFloat(game, fl) Then
                WFloat(stage, o, CSng(pos.X)) : WFloat(stage, o + 4, CSng(pos.Y)) : WFloat(stage, o + 8, CSng(pos.Z)) : o += 12
                If (fl And &H10UL) <> 0UL Then WFloat(stage, o, cx.X) Else Array.Copy(BitConverter.GetBytes(unusedW), 0, stage, o, 4)
                o += 4
            Else
                WHalf(stage, o, CSng(pos.X)) : WHalf(stage, o + 2, CSng(pos.Y)) : WHalf(stage, o + 4, CSng(pos.Z)) : o += 6
                If (fl And &H10UL) <> 0UL Then WHalf(stage, o, cx.X) Else Array.Copy(BitConverter.GetBytes(unusedW), 0, stage, o, 2)
                o += 2
            End If
        End If
        ' from here on the two writers' Sync are the same (UV 0x2, normal 0x8, tangent 0x18, colour 0x20, skin 0x40, eye 0x100)
        If (fl And &H2UL) <> 0UL Then WHalf(stage, o, uv.X) : WHalf(stage, o + 2, uv.Y) : o += 4
        If (fl And &H8UL) <> 0UL Then
            stage(o) = NormalByte(nrm.X) : stage(o + 1) = NormalByte(nrm.Y) : stage(o + 2) = NormalByte(nrm.Z) : stage(o + 3) = NormalByte(cx.Y) : o += 4
        End If
        If (fl And &H18UL) = &H18UL Then
            stage(o) = NormalByte(bitApp.X) : stage(o + 1) = NormalByte(bitApp.Y) : stage(o + 2) = NormalByte(bitApp.Z) : stage(o + 3) = NormalByte(cx.Z) : o += 4
        End If
        If (fl And &H20UL) <> 0UL Then
            stage(o) = ColorByte(col.X) : stage(o + 1) = ColorByte(col.Y) : stage(o + 2) = ColorByte(col.Z) : stage(o + 3) = ColorByte(col.W) : o += 4
        End If
        If (fl And &H40UL) <> 0UL Then
            For j = 0 To 3
                W16(stage, o + 2 * j, If(weights Is Nothing, CShort(0), BitConverter.HalfToInt16Bits(weights(skinBase + j))))
                stage(o + 8 + j) = If(indices Is Nothing, CByte(0), indices(skinBase + j))
            Next
            o += 12
        End If
        If (fl And &H100UL) <> 0UL Then WFloat(stage, o, eye) : o += 4
    End Sub

    ''' <summary>Floats per vertex of the CPU-route stream (PackCpuRoute): 7 x vec4.</summary>
    Friend Const CpuRouteFloats As Integer = 28

    ''' <summary>The CPU-route stream's attributes of a layout (asm2glsl.CPU_LOC: cpu_ps 6, cpu_r0 7, cpu_r1 12, cpu_r2 13; TbnAlt / RowsAlt
    ''' asm2glsl.CPU_LOC_ALT: cpu_r1 / cpu_r2 at 5 / 8): byte offsets in the vertex's 7 vec4 - [0] (P_world, s), [1..3] N', Cy', Cx' (Tbn), [4..6] the
    ''' rows 0..2 of the blended M (Rows).</summary>
    Friend Function CpuRouteAttributes(layout As Fo4CpuRouteLayout) As (Location As Integer, Offset As Integer)()
#If DEBUG Then
        ' ShadowGate --gate-mutant swap-cycx (review rev-04; Fo4GBufferConstants.GateMutantOn, the one switch): Cy' and Cx' swapped.
        If layout = Fo4CpuRouteLayout.Tbn AndAlso Fo4GBufferConstants.GateMutantOn("swap-cycx") Then Return {(6, 0), (7, 16), (12, 48), (13, 32)}
#End If
        If layout = Fo4CpuRouteLayout.Tbn Then Return {(6, 0), (7, 16), (12, 32), (13, 48)}
        If layout = Fo4CpuRouteLayout.Rows Then Return {(6, 0), (7, 64), (12, 80), (13, 96)}
        If layout = Fo4CpuRouteLayout.TbnAlt Then Return {(6, 0), (7, 16), (5, 32), (8, 48)}
        If layout = Fo4CpuRouteLayout.RowsAlt Then Return {(6, 0), (7, 64), (5, 80), (8, 96)}
        Throw New ArgumentOutOfRangeException(NameOf(layout))
    End Function

    ''' <summary>The input-assembler view of the five attributes the CPU route reads (POSITION0, NORMAL0, BINORMAL0, BLENDWEIGHT0,
    ''' BLENDINDICES0, in that order) in a vertex desc: EngineVertexLayout.Elements, taken once per shape (CpuRouteInputsOf), and the game
    ''' whose row of the skin law the route blends with (EngineSkin).</summary>
    Friend Structure CpuRouteInputs
        Friend Game As Config_App.Game_Enum
        Friend Stride As Integer
        Friend Elements As EngineVertexLayout.Element()
        Friend Present As Boolean()
        ''' <summary>Skyrim SE: the slot-1 stream (POSITION of a BSDynamicTriShape, 0x141011789..7B6) and its stride; Nothing on a layout
        ''' without it (Fallout 4's stage has one buffer).</summary>
        Friend Slot1 As Byte()
        Friend Slot1Stride As Integer
    End Structure

    Private ReadOnly CpuRouteSemantics As Fo4EngineVertexLayout.Sem() = {Fo4EngineVertexLayout.Sem.Position0, Fo4EngineVertexLayout.Sem.Normal0,
                                                                         Fo4EngineVertexLayout.Sem.Binormal0, Fo4EngineVertexLayout.Sem.BlendWeight0,
                                                                         Fo4EngineVertexLayout.Sem.BlendIndices0}

    Friend Function CpuRouteInputsOf(desc As ULong, game As Config_App.Game_Enum) As CpuRouteInputs
        Dim r As New CpuRouteInputs With {.Game = game, .Stride = EngineVertexLayout.Stride(desc), .Elements = New EngineVertexLayout.Element(4) {}, .Present = New Boolean(4) {}}
        Dim all = EngineVertexLayout.Elements(desc, game)
        For k = 0 To 4
            For Each e In all
                If e.Semantic = CpuRouteSemantics(k) Then r.Elements(k) = e : r.Present(k) = True
            Next
        Next
        Return r
    End Function

    ''' <summary>Every HALF bit pattern as Single (the conversion itself, CSng(Half), tabulated once: exact by construction).</summary>
    Private ReadOnly HalfToSingle As Single() = TabulateHalf()
    ''' <summary>Every UNORM8 byte as the input assembler converts it, b / 255 (D3D11.3 3.2.3.6), tabulated once.</summary>
    Private ReadOnly UnormToSingle As Single() = TabulateUnorm8()

    Private Function TabulateHalf() As Single()
        Dim t(65535) As Single
        For b = 0 To 65535 : t(b) = CSng(BitConverter.Int16BitsToHalf(CShort(If(b > 32767, b - 65536, b)))) : Next
        Return t
    End Function

    Private Function TabulateUnorm8() As Single()
        Dim t(255) As Single
        For b = 0 To 255 : t(b) = EngineSkin.Unorm8(CByte(b)) : Next
        Return t
    End Function

    ''' <summary>Component <paramref name="j"/> of an element at byte <paramref name="o"/>; past its components, the GL default
    ''' (0, 0, 0, 1) (OpenGL 4.6 10.2).</summary>
    Private Function Component(stage As Byte(), e As EngineVertexLayout.Element, o As Integer, j As Integer) As Single
        If j >= e.Components Then Return If(j = 3, 1.0F, 0.0F)
        Select Case e.GlType
            Case OpenTK.Graphics.OpenGL4.VertexAttribPointerType.Float : Return BitConverter.ToSingle(stage, o + 4 * j)
            Case OpenTK.Graphics.OpenGL4.VertexAttribPointerType.HalfFloat : Return HalfToSingle(stage(o + 2 * j) Or (CInt(stage(o + 2 * j + 1)) << 8))
            Case Else : Return UnormToSingle(stage(o + j))
        End Select
    End Function

    ''' <summary>Attribute <paramref name="k"/> (CpuRouteInputs order) of vertex <paramref name="i"/> as the input assembler hands it to
    ''' the VS: a FLOAT / HALF component as is, an UNORM8 one b / 255 (D3D11.3 3.2.3.6); an absent element is the GL current attribute
    ''' default (0, 0, 0, 1) (OpenGL 4.6 10.2).</summary>
    Private Function ReadElement(stage As Byte(), inp As CpuRouteInputs, i As Integer, k As Integer) As Vector4
        If Not inp.Present(k) Then Return New Vector4(0.0F, 0.0F, 0.0F, 1.0F)
        If inp.Slot1 IsNot Nothing AndAlso inp.Elements(k).Slot = 1 Then Return ElementAt(inp.Slot1, inp.Elements(k), i * inp.Slot1Stride + inp.Elements(k).Offset)
        Return ElementAt(stage, inp.Elements(k), i * inp.Stride + inp.Elements(k).Offset)
    End Function

    ''' <summary>The element at byte <paramref name="o"/> as the input assembler hands it (Component, four components).</summary>
    Private Function ElementAt(stage As Byte(), e As EngineVertexLayout.Element, o As Integer) As Vector4
        Return New Vector4(Component(stage, e, o, 0), Component(stage, e, o, 1), Component(stage, e, o, 2), Component(stage, e, o, 3))
    End Function

#If DEBUG Then
    ''' <summary>GATE ONLY (ola 2b, --fo4-vertex-constants' oracle): attribute <paramref name="sem"/> of vertex <paramref name="i"/> of an
    ''' engine stage of layout <paramref name="desc"/>, as the input assembler hands it (ElementAt); Nothing when the layout has no such
    ''' element (the VS then reads the GL current attribute value, OpenGL 4.6 10.2).</summary>
    Friend Function ElementValue(engineStage As Byte(), desc As ULong, game As Config_App.Game_Enum, i As Integer, sem As Fo4EngineVertexLayout.Sem) As Vector4?
        For Each e In EngineVertexLayout.Elements(desc, game)
            If e.Semantic = sem Then Return ElementAt(engineStage, e, i * EngineVertexLayout.Stride(desc) + e.Offset)
        Next
        Return Nothing
    End Function
#End If

    ''' <summary>The five attributes of vertex <paramref name="i"/> the CPU route reads, as the VS receives them (ReadElement): the one
    ''' read of the CPU route's inputs (PackCpuRoute and ShadowGate G5b's oracle).</summary>
    Friend Function CpuRouteInputValues(engineStage As Byte(), inp As CpuRouteInputs, i As Integer) As (Position As Vector4, Normal As Vector4, Binormal As Vector4, BlendWeight As Vector4, BlendIndices As Vector4)
        Return (ReadElement(engineStage, inp, i, 0), ReadElement(engineStage, inp, i, 1), ReadElement(engineStage, inp, i, 2),
                ReadElement(engineStage, inp, i, 3), ReadElement(engineStage, inp, i, 4))
    End Function

    ''' <summary>A UNORM-encoded vector as the engine VS decodes it: x * 2 - 1 (mad, rec2304 L94-95 / L106).</summary>
    Private Function Decode(x As Single) As Single
        Return x * 2.0F + -1.0F
    End Function

    ''' <summary>THE CPU-ROUTE STREAM of vertex <paramref name="i"/> (user decision 12-oct-2026: the CPU skins, the GPU only projects;
    ''' asm2glsl.cpu_route_body), from the engine stage's own packed bytes - the inputs the GPU route's VS reads - and cb10's rows:
    ''' the engine's blend (EngineSkin.Blend, the row of inp.Game: rows r0..r2, s), then the app's skin law on one vertex (FastSkin.UnVertice, the law
    ''' AppSkinLaw transcribes for the GPU route: P_world = p * M, N' = cofactors / det (D2), Cy' and Cx' = M * v normalised) over the
    ''' vectors the VS decodes and normalises (NORMAL0.xyz, BINORMAL0.xyz, (POSITION0.w, NORMAL0.w, BINORMAL0.w): rec2304 L93-98,
    ''' L106-109). Both layouts are written: [0] (P_world, s), [1..3] N', Cy', Cx', [4..6] the rows r0..r2 (w = the translation without
    ''' the eye: the VS subtracts s * eye). <paramref name="w"/>: a 4-Single scratch.</summary>
    Friend Sub PackCpuRoute(stage As Single(), i As Integer, engineStage As Byte(), inp As CpuRouteInputs, rows As Vector4(), w() As Single)
        Dim v = CpuRouteInputValues(engineStage, inp, i)
        Dim r0, r1, r2 As Vector4, s As Single
        EngineSkin.Blend(inp.Game, rows, v.BlendIndices, v.BlendWeight, w, r0, r1, r2, s)
        Dim m = EngineSkin.MatrixOfRows(r0, r1, r2)
        PackCpuRouteTail(stage, i, v.Position, v.Normal, v.Binormal, m,
                         FastSkin.MatrizDeNormales(m.M11, m.M12, m.M13, m.M21, m.M22, m.M23, m.M31, m.M32, m.M33), s, r0, r1, r2)
    End Sub

    ''' <summary>PackCpuRoute with the blend already done (review note "performance": one blend and one normal matrix per vertex and per
    ''' frame): <paramref name="m"/> = the view's per-vertex skin matrix (SkinMatricesSoA), when it is the engine's blend of the rows
    ''' this draw's cb10 holds (RenderableMesh.EngineSharesViewSkin), and <paramref name="e"/> = its FastSkin.MatrizDeNormales. s is the
    ''' chain over the weights alone (EngineSkin.EngineWeights, WeightSum, the row of inp.Game), the rows are m's (RowsOfMatrix): the same Single as
    ''' PackCpuRoute's own blend of those rows.</summary>
    Friend Sub PackCpuRouteBlended(stage As Single(), i As Integer, engineStage As Byte(), inp As CpuRouteInputs, m As Matrix4,
                                   e As FastSkin.MatrizNormal, w() As Single)
        Dim v = CpuRouteInputValues(engineStage, inp, i)
        EngineSkin.EngineWeights(inp.Game, v.BlendWeight.X, v.BlendWeight.Y, v.BlendWeight.Z, v.BlendWeight.W, w)
        Dim r0, r1, r2 As Vector4
        EngineSkin.RowsOfMatrix(m, r0, r1, r2)
        PackCpuRouteTail(stage, i, v.Position, v.Normal, v.Binormal, m, e, EngineSkin.WeightSum(inp.Game, w), r0, r1, r2)
    End Sub

    ''' <summary>The common tail of PackCpuRoute and PackCpuRouteBlended: the vectors the VS decodes and normalises (NORMAL0.xyz,
    ''' BINORMAL0.xyz, (POSITION0.w, NORMAL0.w, BINORMAL0.w)), the app's skin law on one vertex (FastSkin.UnVerticeCon) and the
    ''' stream's 7 vec4.</summary>
    Private Sub PackCpuRouteTail(stage As Single(), i As Integer, pos As Vector4, nrm As Vector4, bin As Vector4, m As Matrix4,
                                 e As FastSkin.MatrizNormal, s As Single, r0 As Vector4, r1 As Vector4, r2 As Vector4)
        Dim n = New Vector3(Decode(nrm.X), Decode(nrm.Y), Decode(nrm.Z))
        Dim cy = New Vector3(Decode(bin.X), Decode(bin.Y), Decode(bin.Z))
        Dim cx = New Vector3(pos.W, Decode(nrm.W), Decode(bin.W))
#If DEBUG Then
        Dim crudos = (n, cy, cx)
#End If
        n = EngineSkin.NormalizeLikeVs(n) : cy = EngineSkin.NormalizeLikeVs(cy) : cx = EngineSkin.NormalizeLikeVs(cx)
#If DEBUG Then
        ' ShadowGate --gate-mutant no-vs-normalize (review rev-04; the one switch): the VS's normalisation skipped.
        If Fo4GBufferConstants.GateMutantOn("no-vs-normalize") Then n = crudos.n : cy = crudos.cy : cx = crudos.cx
#End If
        Dim pW As Vector3, nS As Vector3, cxS As Vector3, cyS As Vector3
        FastSkin.UnVerticeCon(m, e, New Vector3d(pos.X, pos.Y, pos.Z), n, cx, cy, False, pW, nS, cxS, cyS)
        Dim o = i * CpuRouteFloats
#If DEBUG Then
        ' ShadowGate --sse-cpu-route [sse-cpu-route]: the eye term (1 - S) e left out (the VS subtracts 1 * eye, not S * eye).
        If Fo4GBufferConstants.GateMutantOn("sse-cpu-eye-absent") Then s = 1.0F
#End If
        stage(o) = pW.X : stage(o + 1) = pW.Y : stage(o + 2) = pW.Z : stage(o + 3) = s
        stage(o + 4) = nS.X : stage(o + 5) = nS.Y : stage(o + 6) = nS.Z : stage(o + 7) = 0.0F
        stage(o + 8) = cyS.X : stage(o + 9) = cyS.Y : stage(o + 10) = cyS.Z : stage(o + 11) = 0.0F
        stage(o + 12) = cxS.X : stage(o + 13) = cxS.Y : stage(o + 14) = cxS.Z : stage(o + 15) = 0.0F
        stage(o + 16) = r0.X : stage(o + 17) = r0.Y : stage(o + 18) = r0.Z : stage(o + 19) = r0.W
        stage(o + 20) = r1.X : stage(o + 21) = r1.Y : stage(o + 22) = r1.Z : stage(o + 23) = r1.W
        stage(o + 24) = r2.X : stage(o + 25) = r2.Y : stage(o + 26) = r2.Z : stage(o + 27) = r2.W
    End Sub

    ''' <summary>Vertex <paramref name="i"/> of a Skyrim SE BSDynamicTriShape's slot-1 stream (POSITION float4 in slot 1, EngineVertexLayout:
    ''' bit 54 without bit 44): the writer's CalcDynamicData (NiflySharp Blocks\BSDynamicTriShape.cs:66-79): x, y, z of the vertex and
    ''' w = its bitangent X, the NIF bitangent the app calls Tangents. Stride EngineVertexLayout.Slot1Stride.</summary>
    Friend Sub PackSlot1Position(stage1 As Byte(), desc As ULong, i As Integer, pos As Vector3d, tanApp As Vector3)
        Dim o = i * EngineVertexLayout.Slot1Stride(desc)
        WFloat(stage1, o, CSng(pos.X)) : WFloat(stage1, o + 4, CSng(pos.Y)) : WFloat(stage1, o + 8, CSng(pos.Z)) : WFloat(stage1, o + 12, tanApp.X)
    End Sub

#Region "P3"
    ' P3 (re_vertices.md 3, s1_re.md 12.2..12.6): SkyrimSE.exe 1.7.104 converts a v83 NiTriShape / BSLODTriShape / BSSegmentedTriShape
    ' into a BSTriShape on load (0x14101B070; NiTriStrips is not converted: vtable slot 15 returns 0, 0x14101B09E..0A7) and every branch
    ' (0x14101AB30 with a skin, 0x141017BB0 static, 0x141017EE0 dynamic, 0x141018210 segmented) packs with ONE packer, 0x141015CA0,
    ' transcribed ENTIRE below (EnginePackVertices: every argument, the remap list, UV1, the land data and every slot bit, whoever the
    ' caller). The v83 callers pass no UV1, no land data and no remap list (their +0x80 argument: 0x14101AE49 / 0x14101AF08,
    ' 0x141017C42 / 0x141017D22, 0x141017F63 / 0x1410180D4, 0x141018290 / 0x141018370). The separate side is
    ' Tools\ShaderTranscription\sse-common\sse_v83_oracle.py (ParityGate sse-v83-pack: the corpus and the closed domain of the arguments).

    ''' <summary>cvttss2si r32: truncation toward zero; NaN or out of the Int32 range gives &amp;H80000000.</summary>
    Private Function CvttSs2Si32(v As Single) As Integer
        If Single.IsNaN(v) OrElse v >= 2147483648.0F OrElse v < -2147483648.0F Then Return Integer.MinValue
        Return CInt(Math.Truncate(CDbl(v)))
    End Function

    ''' <summary>cvttss2si r64 (the colour, SkyrimSE.exe 0x14101686E: f3 48 0f 2c): truncation; NaN or out of the Int64 range gives
    ''' &amp;H8000000000000000.</summary>
    Private Function CvttSs2Si64(v As Single) As Long
        If Single.IsNaN(v) OrElse v >= 9.223372E+18F OrElse v < -9.223372E+18F Then Return Long.MinValue
        Return CLng(Math.Truncate(CDbl(v)))
    End Function

    ''' <summary>THE ENGINE'S BYTE of a component (Skyrim SE 0x141019090): scale = 255 / (max - min) in float (0x1410190A3..B5; 255 is
    ''' the .rdata float at 0x141B5BF6C), byte = the low byte of (int32)trunc((x - min) * scale) (subss / mulss / cvttss2si / mov byte,
    ''' 0x1410190E5..F1): truncated toward zero WITHOUT a clamp, so x = +1 gives 255, x &gt;= 1.00392 gives 256 -&gt; 0 and x &lt; -1 a
    ''' negative int's low byte.</summary>
    Friend Function EnginePackByte(x As Single, mn As Single, mx As Single) As Byte
        Dim scale As Single = 255.0F / (mx - mn)
        Dim v As Single = (x - mn) * scale
#If DEBUG Then
        ' ParityGate sse-v83-pack mutant "v83-round": rounding to the nearest (the plan's first reading) in place of the truncation.
        If Fo4GBufferConstants.GateMutantOn("v83-round") Then v = CSng(Math.Round(CDbl(v), MidpointRounding.ToEven))
#End If
        Return CByte(CvttSs2Si32(v) And &HFF)
    End Function

    ''' <summary>The common tail of the two float -&gt; half of the packer (Skyrim SE 0x141018DB5..DEF / 0x14101BDC7..E01), on the bits
    ''' a = |f|: below &amp;H38800000 the subnormal path ((mantissa OR &amp;H800000) &gt;&gt; (113 - exponent), the count of `shr r32, cl`
    ''' masked to 5 bits), else a + &amp;HC8000000 (the exponent re-biased, 32-bit wrap); then (m + &amp;HFFF + ((m &gt;&gt; 13) AND 1)) &gt;&gt; 13
    ''' AND &amp;H7FFF: the nearest, ties to even.</summary>
    Private Function HalfMagnitude(a As UInteger) As UInteger
        Dim m As UInteger
        If a < &H38800000UI Then
            m = ((a And &H7FFFFFUI) Or &H800000UI) >> CInt((113UI - (a >> 23)) And 31UI)
        Else
            m = CUInt((CULng(a) + &HC8000000UL) And &HFFFFFFFFUL)
        End If
#If DEBUG Then
        ' ParityGate sse-v83-pack mutant "v83-half-trunc": the half truncated (no rounding term).
        If Fo4GBufferConstants.GateMutantOn("v83-half-trunc") Then Return (m >> 13) And &H7FFFUI
#End If
        Return CUInt(((CULng(m) + &HFFFUL + ((m >> 13) And 1UI)) And &HFFFFFFFFUL) >> 13) And &H7FFFUI
    End Function

    ''' <summary>Float -&gt; half A, UV0 and UV1 (Skyrim SE 0x141018D20, XMConvertFloatToHalf without F16C, the old version): |f| above
    ''' &amp;H47FFEFFF (0x141018DA6) gives &amp;H7FFF, Inf and NaN included; the sign bit (b &gt;&gt; 16) AND &amp;H8000 OR'd in (0x141018DF4).</summary>
    Friend Function EngineHalfA(f As Single) As UShort
        Dim b = BitConverter.SingleToUInt32Bits(f)
        Dim s = (b >> 16) And &H8000UI, a = b And &H7FFFFFFFUI
        If a > &H47FFEFFFUI Then Return CUShort(s Or &H7FFFUI)
        Return CUShort(s Or HalfMagnitude(a))
    End Function

    ''' <summary>Float -&gt; half B, the weights (Skyrim SE 0x14101BD20): |f| above &amp;H477FE000 (0x14101BD92) gives &amp;H7FFF for a NaN
    ''' (exponent all ones, mantissa not 0) and &amp;H7C00 otherwise (0x14101BD9A..DB9); |f| = 0 gives 0 (0x14101BDBF..DC5); else as A.</summary>
    Friend Function EngineHalfB(f As Single) As UShort
        Dim b = BitConverter.SingleToUInt32Bits(f)
        Dim s = (b >> 16) And &H8000UI, a = b And &H7FFFFFFFUI
        If a > &H477FE000UI Then
            Dim nan = (a And &H7F800000UI) = &H7F800000UI AndAlso (a And &H7FFFFFUI) <> 0UI
            Return CUShort(s Or If(nan, &H7FFFUI, &H7C00UI))
        End If
        If a = 0UI Then Return CUShort(s)
        Return CUShort(s Or HalfMagnitude(a))
    End Function

    ''' <summary>The arguments of the packer 0x141015CA0 (rdx, r8, r9, then the stack +0x28..+0x98): the float arrays as the engine
    ''' reads them (Nothing = a null pointer), the per-vertex remap list (+0x80: source index of output vertex v; Nothing = v itself),
    ''' the slot bits (+0x88: bit k = attribute k goes to slot 1 and the packer does not write it) and, for POSITION in slot 1, the
    ''' destination of pos.w and its stride (+0x90 / +0x98).</summary>
    Friend NotInheritable Class EnginePackerArgs
        ''' <summary>float x 3 per vertex (rdx; stride 12, 0x141016086).</summary>
        Friend Positions As Single()
        ''' <summary>float x 2 (r8; stride 8, 0x141016143).</summary>
        Friend Uv0 As Single()
        ''' <summary>float x 4 (r9; stride 16, 0x1410161A9).</summary>
        Friend Uv1 As Single()
        ''' <summary>float x 3 each (+0x28, +0x30, +0x38; stride 12).</summary>
        Friend Normals As Single(), Tangents As Single(), Bitangents As Single()
        ''' <summary>float x 4 (+0x40; stride 16, 0x1410164C8).</summary>
        Friend Colors As Single()
        ''' <summary>float x 4 (+0x48; stride 16, 0x14101657A) and their 4 index bytes (+0x50; 4 per vertex, 0x141016610).</summary>
        Friend Weights As Single(), Indices As Byte()
        ''' <summary>float x 4 each (+0x58, +0x60; stride 16, 0x141016652).</summary>
        Friend Land As Single(), Land2 As Single()
        ''' <summary>One dword per vertex (+0x68, 0x141016809): the eye float's bits.</summary>
        Friend Eye As UInteger()
        Friend Remap As UShort()
        Friend SlotBits As Integer
        Friend Slot1PosW As Byte(), Slot1PosWOffset As Integer, Slot1PosWStride As Integer
    End Class

    ''' <summary>THE PACKER 0x141015CA0, entire. Returns the desc and the slot-0 size (<paramref name="size"/>, nVerts * stride written to
    ''' the +0x78 pointer, 0x141016B5B..B6F) and packs into <paramref name="dest"/> when it is not Nothing and holds that size
    ''' (0x14101603E..055).
    ''' <list type="bullet">
    ''' <item>Layout (0x141015D16..FF8): attribute k (POSITION 0, TEXCOORD0 1, TEXCOORD1 2, NORMAL 3, BINORMAL 4 = the tangents, COLOR 5,
    ''' skin 6, land 7, eye 8) is in the desc when its pointer is not null or its slot bit is set: bit 44 + 10 * slot + k, and the slot's
    ''' running offset in nibble k + 1; sizes 16 (0x141015D2B), 4, 8, 4, 4, 4, 12, 8, 4. Then the slot sizes / 4 in nibbles 0 and 1
    ''' (0x141016001..022).</item>
    ''' <item>An attribute is written only when its pointer is not null and its slot bit is clear: POSITION x, y, z by dword copies
    ''' (0x1410193C0 at 0x141016097..0E1); pos.w = bitangent.x with the bitangents and slot bit 4 clear (0x1410160E6..124), at slot 0
    ''' offset 12 whatever the stride (0x14101611E: past the vertex when the stride is under 16, past the buffer at the last one) or, with slot bit 0, at the +0x90 destination with the +0x98 stride; TEXCOORD0 two halves A (0x14101615E / 185);
    ''' TEXCOORD1 four halves A (0x1410161C4..239); NORMAL = EnginePackByte(n.xyz, -1, +1) (0x14101629C..302; -1 / +1 the .rdata floats
    ''' 0x1417E2820 / 0x141B5BE28) and w = EnginePackByte(bitangent.y) with the bitangents (0x141016342); BINORMAL =
    ''' EnginePackByte(tangent.xyz) (0x14101639F..40D) and w = EnginePackByte(bitangent.z) (0x14101643D); COLOR byte k = low byte of
    ''' (int64)trunc(c_k * 255) (0x1410164C0..528 / 0x141016850..9F7); skin four halves B (0x141016580..5E6, NO remap) and the 4 index
    ''' bytes copied in vertex order (0x141016610..616); land = EnginePackByte(x, 0, 1) of the four land floats then the four land2
    ''' floats (0x141016676..7AA); eye = the dword (0x141016801..80D, 0x141016AF1..B59).</item>
    ''' <item>The remap list replaces the source index of every attribute except the weights and the bone indices (0x1410193C0 /
    ''' 0x141018D20 / 0x141019090 take it; 0x14101BD20 and the index copy do not).</item>
    ''' </list></summary>
    Friend Function EnginePackVertices(n As Integer, a As EnginePackerArgs, dest As Byte(), ByRef size As Integer) As ULong
        Dim sizes = {16, 4, 8, 4, 4, 4, 12, 8, 4}
        Dim present = {a.Positions IsNot Nothing, a.Uv0 IsNot Nothing, a.Uv1 IsNot Nothing, a.Normals IsNot Nothing, a.Tangents IsNot Nothing,
                       a.Colors IsNot Nothing, a.Weights IsNot Nothing, a.Land IsNot Nothing, a.Eye IsNot Nothing}
        Dim acc(1) As Integer, off(8) As Integer, desc As ULong = 0
        For k = 0 To 8
            Dim slot = (a.SlotBits >> k) And 1
            If Not present(k) AndAlso slot = 0 Then Continue For
            off(k) = acc(slot)
            desc = desc Or (1UL << (44 + 10 * slot + k))
            If k > 0 Then
                Dim sh = 4 * (k + 1)
                desc = (desc And Not (&HFUL << sh)) Or (CULng(off(k) >> 2) << sh)
            End If
            acc(slot) += sizes(k)
        Next
        desc = (desc And Not &HFFUL) Or (CULng(acc(1)) << 2) Or ((CULng(acc(0)) >> 2) And &H3FFFFFFFFFFFFF0FUL)
        Dim stride = acc(0)
        size = stride * n
        If dest Is Nothing OrElse dest.Length < size Then Return desc
        Dim wr = Function(k As Integer) present(k) AndAlso ((a.SlotBits >> k) And 1) = 0
        Dim src = Function(v As Integer) If(a.Remap Is Nothing, v, CInt(a.Remap(v)))
        Const MinusOne As Single = -1.0F, One As Single = 1.0F, Zero As Single = 0.0F
        For v = 0 To n - 1
            Dim b0 = v * stride, s = src(v)
            If wr(0) Then
                WFloat(dest, b0, a.Positions(3 * s)) : WFloat(dest, b0 + 4, a.Positions(3 * s + 1)) : WFloat(dest, b0 + 8, a.Positions(3 * s + 2))
            End If
            If a.Bitangents IsNot Nothing AndAlso ((a.SlotBits >> 4) And 1) = 0 Then
                If (a.SlotBits And 1) = 0 Then
                    WFloat(dest, b0 + 12, a.Bitangents(3 * s))
                Else
                    WFloat(a.Slot1PosW, a.Slot1PosWOffset + v * a.Slot1PosWStride, a.Bitangents(3 * s))
                End If
            End If
            If wr(1) Then
                WHalfBits(dest, b0 + off(1), EngineHalfA(a.Uv0(2 * s))) : WHalfBits(dest, b0 + off(1) + 2, EngineHalfA(a.Uv0(2 * s + 1)))
            End If
            If wr(2) Then
                For j = 0 To 3 : WHalfBits(dest, b0 + off(2) + 2 * j, EngineHalfA(a.Uv1(4 * s + j))) : Next
            End If
            If wr(3) Then
                For j = 0 To 2 : dest(b0 + off(3) + j) = EnginePackByte(a.Normals(3 * s + j), MinusOne, One) : Next
                If a.Bitangents IsNot Nothing Then dest(b0 + off(3) + 3) = EnginePackByte(a.Bitangents(3 * s + 1), MinusOne, One)
            End If
            If wr(4) Then
                For j = 0 To 2 : dest(b0 + off(4) + j) = EnginePackByte(a.Tangents(3 * s + j), MinusOne, One) : Next
                If a.Bitangents IsNot Nothing Then dest(b0 + off(4) + 3) = EnginePackByte(a.Bitangents(3 * s + 2), MinusOne, One)
            End If
            If wr(5) Then
                For j = 0 To 3 : dest(b0 + off(5) + j) = CByte(CvttSs2Si64(a.Colors(4 * s + j) * 255.0F) And &HFFL) : Next
            End If
            If wr(6) Then
                For j = 0 To 3
                    WHalfBits(dest, b0 + off(6) + 2 * j, EngineHalfB(a.Weights(4 * v + j)))
                    dest(b0 + off(6) + 8 + j) = a.Indices(4 * v + j)
                Next
            End If
            If wr(7) Then
                For j = 0 To 3
                    dest(b0 + off(7) + j) = EnginePackByte(a.Land(4 * s + j), Zero, One)
                    dest(b0 + off(7) + 4 + j) = EnginePackByte(a.Land2(4 * s + j), Zero, One)
                Next
            End If
            If wr(8) Then Array.Copy(BitConverter.GetBytes(a.Eye(s)), 0, dest, b0 + off(8), 4)
        Next
        Return desc
    End Function

    Private Sub WHalfBits(b As Byte(), o As Integer, h As UShort)
        b(o) = CByte(h And &HFFUS) : b(o + 1) = CByte(h >> 8)
    End Sub

    ''' <summary>An entire v83 shape packed by P3: the desc, the slot-0 buffer, the slot-1 buffer (POSITION float4, dynamic) and the index
    ''' list (S-83A, NiTriShapeGeometry.EngineV83Indices).</summary>
    Friend NotInheritable Class V83Packed
        Friend Desc As ULong
        Friend Slot0 As Byte(), Slot1 As Byte()
        Friend Indices As UShort()
        ''' <summary>What the packer read (PackV83LiveVertex repacks an edited vertex with it).</summary>
        Friend Args As EnginePackerArgs
    End Class

    ''' <summary>P3 over the shape's NIF data, as the converter feeds the packer (the file's floats, not the app's normalised live arrays -
    ''' SkinningHelper normalises N/T/B, P3 packs the NIF's own, s1_re.md 12.6): positions [data+0x28], normals [data+0x30], tangents /
    ''' bitangents at normals + 12 n / + 24 n with the tangent flags (0x14101AB8A..BB4), colours [data+0x38], UV set 0 [data+0x40]; with a
    ''' skin, the converter's weights and indices (NiTriShapeGeometry.EngineV83Skin) and the eye (V-2b, NiTriShapeGeometry.GetEyeData; only
    ''' the skinned converter passes it). <paramref name="dynamic"/> = the model loader's dynamic geometry argument (+9 of its arguments,
    ''' SkyrimSE.exe 0x140FC89D7, through 0x1410194E0 and 0x14101B9E0 to 0x14101B070's dl): POSITION goes to slot 1 (slot bit 0: with a
    ''' skin 0x14101ADA7..ADEA, without 0x141017F5B / 0x1410180CC) and the slot-1 buffer is the dynamic one (16 bytes per vertex,
    ''' 0x14101B26E / 0x141018082): x, y, z copied by 0x140EFD7C0 (three dword copies, 0x14101B352 / 0x14101B52C) and w = bitangent.x
    ''' through the +0x90 destination (0x14101ADCE / 0x1410180AC). Nothing when the converter does not take the shape
    ''' (NiTriShapeGeometry.EngineConvertsToBSTriShape) or its partitions cannot be read the converter's way (EngineV83Skin).</summary>
    Friend Function PackV83(geom As NiTriShapeGeometry, dynamic As Boolean) As V83Packed
        If Not geom.EngineConvertsToBSTriShape() Then Return Nothing
        Dim n = geom.VertexCount
        ' No vertex: the arrays the converter reads come from a zero-size allocation (0x140EF9E14 -> 0x140CDE250), which returns a
        ' global that alternates 0 and 0x10 (0x140CDE2B0..2C3): whether POSITION and the rest enter the desc depends on the engine's
        ' allocation history, not on the shape. Nothing is drawn either way (no vertex).
        If n = 0 Then Return Nothing
        Dim flat3 = Function(l As List(Of System.Numerics.Vector3)) As Single()
                        Dim f(l.Count * 3 - 1) As Single
                        For i = 0 To l.Count - 1 : f(3 * i) = l(i).X : f(3 * i + 1) = l(i).Y : f(3 * i + 2) = l(i).Z : Next
                        Return f
                    End Function
        Dim a As New EnginePackerArgs With {.SlotBits = If(dynamic, 1, 0)}
        Dim p = geom.GetVertexPositions()
        If p.Count = n AndAlso n > 0 Then a.Positions = flat3(p)
        If geom.HasNormals Then
            a.Normals = flat3(geom.GetNormals())
            If geom.HasTangents Then a.Tangents = flat3(geom.GetBitangents()) : a.Bitangents = flat3(geom.GetTangents())   ' the IShapeGeometry swap
        End If
        If geom.HasVertexColors Then
            Dim c = geom.GetVertexColors()
            a.Colors = New Single(c.Count * 4 - 1) {}
            For i = 0 To c.Count - 1 : a.Colors(4 * i) = c(i).R : a.Colors(4 * i + 1) = c(i).G : a.Colors(4 * i + 2) = c(i).B : a.Colors(4 * i + 3) = c(i).A : Next
        End If
        If geom.HasUVs Then
            Dim uv = geom.GetUVs()
            a.Uv0 = New Single(n * 2 - 1) {}
            For i = 0 To n - 1 : a.Uv0(2 * i) = uv(i).U : a.Uv0(2 * i + 1) = uv(i).V : Next
        End If
        If geom.BackingShape.IsSkinned Then
            If Not geom.EngineV83Skin(a.Weights, a.Indices) Then Return Nothing
            If geom.HasEyeData Then
                Dim e = geom.GetEyeData()
                a.Eye = New UInteger(n - 1) {}
                For i = 0 To n - 1 : a.Eye(i) = BitConverter.SingleToUInt32Bits(e(i)) : Next
            End If
        End If
        Dim r As New V83Packed With {.Indices = geom.EngineV83Indices(), .Args = a}
        If r.Indices Is Nothing Then Return Nothing
        If dynamic Then
            r.Slot1 = New Byte(n * 16 - 1) {}
            a.Slot1PosW = r.Slot1 : a.Slot1PosWOffset = 12 : a.Slot1PosWStride = 16
            If a.Positions IsNot Nothing Then
                For i = 0 To n - 1 : WFloat(r.Slot1, 16 * i, a.Positions(3 * i)) : WFloat(r.Slot1, 16 * i + 4, a.Positions(3 * i + 1)) : WFloat(r.Slot1, 16 * i + 8, a.Positions(3 * i + 2)) : Next
            End If
        Else
            r.Slot1 = Array.Empty(Of Byte)()
        End If
        Dim size As Integer
        r.Desc = EnginePackVertices(n, a, Nothing, size)
        r.Slot0 = New Byte(size - 1) {}
        EnginePackVertices(n, a, r.Slot0, size)
        Return r
    End Function

    ''' <summary>The position of vertex <paramref name="i"/> of a slot-0 POSITION (Skyrim SE: float3 at the vertex's start, 0x14101611E's vertex).</summary>
    Friend Sub PackPosition(game As Config_App.Game_Enum, stage As Byte(), desc As ULong, i As Integer, pos As Vector3d)
        Dim fl = desc >> 44
        If (fl And &H1UL) = 0UL Then Exit Sub
        Dim o = i * EngineVertexLayout.Stride(desc)
        Dim comoFloat = PositionIsFloat(game, fl)
#If DEBUG Then
        ' ShadowGate --stage-file-bytes [intact], [position]: Skyrim SE's position over a row of halves.
        If Fo4GBufferConstants.GateMutantOn("stage-position-float") Then comoFloat = True
#End If
#If DEBUG Then
        ' ShadowGate --stage-file-bytes [position]: the moved vertex's position left at the file's, per branch.
        If comoFloat AndAlso Fo4GBufferConstants.GateMutantOn("stage-position-skip-float") Then Exit Sub
        If Not comoFloat AndAlso Fo4GBufferConstants.GateMutantOn("stage-position-skip-half") Then Exit Sub
#End If
        If comoFloat Then
            WFloat(stage, o, CSng(pos.X)) : WFloat(stage, o + 4, CSng(pos.Y)) : WFloat(stage, o + 8, CSng(pos.Z))
        Else
            WHalf(stage, o, CSng(pos.X)) : WHalf(stage, o + 2, CSng(pos.Y)) : WHalf(stage, o + 4, CSng(pos.Z))
        End If
    End Sub

    ''' <summary>The bytes after the position of a vertex whose desc has the position without the bitangent, as <paramref name="baseBytes"/> (the block's bytes) holds them; 0 without them
    ''' (a vertex past the block's end, another desc). PackVertex's unusedW.</summary>
    Friend Function BasePositionW(game As Config_App.Game_Enum, baseBytes As Byte(), desc As ULong, i As Integer) As UInteger
#If DEBUG Then
        If Fo4GBufferConstants.GateMutantOn("stage-w-zero") Then Return 0UI
#End If
        Dim fl = desc >> 44, o = i * EngineVertexLayout.Stride(desc)
        If baseBytes Is Nothing OrElse o >= baseBytes.Length OrElse (fl And &H11UL) <> &H1UL Then Return 0UI
        Return If(PositionIsFloat(game, fl), BitConverter.ToUInt32(baseBytes, o + 12), CUInt(BitConverter.ToUInt16(baseBytes, o + 6)))
    End Function

    ''' <summary>Vertex <paramref name="i"/> of a converted v83 after a live edit of the app (a morph, a recalculated normal): the packer's own
    ''' law (0x141015CA0, EnginePackVertices) over one vertex - the edited position, normal, tangent and bitangent, the rest as the file gave it
    ''' (<paramref name="p"/>.Args) - into <paramref name="stage"/> at i * stride. <paramref name="tanFile"/> / <paramref name="bitFile"/> in
    ''' the file's naming (the IShapeGeometry swap of PackV83).</summary>
    Friend Sub PackV83LiveVertex(p As V83Packed, stage As Byte(), i As Integer, pos As Vector3d, nrm As Vector3, tanFile As Vector3, bitFile As Vector3)
        Dim a = p.Args
        Dim one As New EnginePackerArgs With {.SlotBits = a.SlotBits}
        If a.Positions IsNot Nothing Then one.Positions = {CSng(pos.X), CSng(pos.Y), CSng(pos.Z)}
        If a.Normals IsNot Nothing Then one.Normals = {nrm.X, nrm.Y, nrm.Z}
        If a.Tangents IsNot Nothing Then one.Tangents = {tanFile.X, tanFile.Y, tanFile.Z}
        If a.Bitangents IsNot Nothing Then one.Bitangents = {bitFile.X, bitFile.Y, bitFile.Z}
        If a.Colors IsNot Nothing Then one.Colors = {a.Colors(4 * i), a.Colors(4 * i + 1), a.Colors(4 * i + 2), a.Colors(4 * i + 3)}
        If a.Uv0 IsNot Nothing Then one.Uv0 = {a.Uv0(2 * i), a.Uv0(2 * i + 1)}
        If a.Weights IsNot Nothing Then one.Weights = {a.Weights(4 * i), a.Weights(4 * i + 1), a.Weights(4 * i + 2), a.Weights(4 * i + 3)}
        If a.Indices IsNot Nothing Then one.Indices = {a.Indices(4 * i), a.Indices(4 * i + 1), a.Indices(4 * i + 2), a.Indices(4 * i + 3)}
        If a.Eye IsNot Nothing Then one.Eye = {a.Eye(i)}
        Dim stride = EngineVertexLayout.Stride(p.Desc)
        Dim size As Integer
        EnginePackVertices(1, one, Nothing, size)
        Dim tmp(size - 1) As Byte
        EnginePackVertices(1, one, tmp, size)
        Array.Copy(tmp, 0, stage, i * stride, stride)
    End Sub
#End Region
End Module
