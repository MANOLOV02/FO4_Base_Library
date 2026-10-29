Imports OpenTK.Mathematics
Imports SysHalf = System.Half

''' <summary>THE NIF WRITER'S VERTEX BYTES, written by the app (user decision 12-oct-2026, revisor-21: NiflySharp is not modified; gate N1
''' compares every packed shape byte for byte against NiflySharp's own writer). Layout = the order of BSVertexData.Sync (NiflySharp
''' Structs\BSVertexData.cs:46-116), which is the engine's input-layout offsets (0x141823210). The writer's rules: positions (Half)x or
''' float (BSTriShape.cs:620-627), normal / tangent / bitangent Y,Z (sbyte)Math.Round((x + 1) / 2 * 255) (:653-655, :693-695,
''' :736-737), bitangent X (Half)x or float (:733-735), colours (byte)Math.Floor(v == 1 ? 255 : v * 256) (:818-827), UV (Half)u.
''' The app's arrays map through IShapeGeometry's swap (BSTriShapeGeometry.vb:388-398): app Tangents -> NIF bitangent (w-packed Cx),
''' app Bitangents -> NIF tangent (BINORMAL).</summary>
Friend Module Fo4EngineVertexPacker

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

    ''' <summary>One vertex in the NIF / engine layout of <paramref name="desc"/> at <paramref name="i"/> * stride, the order of
    ''' BSVertexData.Sync (flags = desc &gt;&gt; 44). <paramref name="weights"/> Nothing = no skin stream written.</summary>
    Friend Sub PackVertex(stage As Byte(), desc As ULong, i As Integer, pos As Vector3d, nrm As Vector3, tanApp As Vector3, bitApp As Vector3,
                          uv As Vector2, col As Vector4, weights As SysHalf(), indices As Byte(), skinBase As Integer, eye As Single)
        Dim fl = desc >> 44
        Dim o = i * Fo4EngineVertexLayout.Stride(desc)
        Dim cx = tanApp                                   ' NIF bitangent (BSTriShapeGeometry.vb:388-389)
        If (fl And &H401UL) = &H401UL Then                 ' full precision vertex
            WFloat(stage, o, CSng(pos.X)) : WFloat(stage, o + 4, CSng(pos.Y)) : WFloat(stage, o + 8, CSng(pos.Z)) : o += 12
            If (fl And &H411UL) = &H411UL Then WFloat(stage, o, cx.X) Else W32(stage, o, 0)
            o += 4
        ElseIf (fl And &H401UL) = &H1UL Then
            WHalf(stage, o, CSng(pos.X)) : WHalf(stage, o + 2, CSng(pos.Y)) : WHalf(stage, o + 4, CSng(pos.Z)) : o += 6
            If (fl And &H411UL) = &H11UL Then WHalf(stage, o, cx.X) Else W16(stage, o, 0)
            o += 2
        End If
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

    ''' <summary>The CPU-route stream's attributes of a layout (asm2glsl.CPU_LOC: cpu_ps 6, cpu_r0 7, cpu_r1 12, cpu_r2 13): byte
    ''' offsets in the vertex's 7 vec4 - [0] (P_world, s), [1..3] N', Cy', Cx' (Tbn), [4..6] the rows 0..2 of the blended M (Rows).</summary>
    Friend Function CpuRouteAttributes(layout As Fo4CpuRouteLayout) As (Location As Integer, Offset As Integer)()
#If DEBUG Then
        ' ShadowGate --gate-mutant swap-cycx (review rev-04; Fo4GBufferConstants.GateMutantOn, the one switch): Cy' and Cx' swapped.
        If layout = Fo4CpuRouteLayout.Tbn AndAlso Fo4GBufferConstants.GateMutantOn("swap-cycx") Then Return {(6, 0), (7, 16), (12, 48), (13, 32)}
#End If
        If layout = Fo4CpuRouteLayout.Tbn Then Return {(6, 0), (7, 16), (12, 32), (13, 48)}
        If layout = Fo4CpuRouteLayout.Rows Then Return {(6, 0), (7, 64), (12, 80), (13, 96)}
        Throw New ArgumentOutOfRangeException(NameOf(layout))
    End Function

    ''' <summary>The input-assembler view of the five attributes the CPU route reads (POSITION0, NORMAL0, BINORMAL0, BLENDWEIGHT0,
    ''' BLENDINDICES0, in that order) in a vertex desc: Fo4EngineVertexLayout.Elements, taken once per shape (CpuRouteInputsOf).</summary>
    Friend Structure CpuRouteInputs
        Friend Stride As Integer
        Friend Elements As Fo4EngineVertexLayout.Element()
        Friend Present As Boolean()
    End Structure

    Private ReadOnly CpuRouteSemantics As Fo4EngineVertexLayout.Sem() = {Fo4EngineVertexLayout.Sem.Position0, Fo4EngineVertexLayout.Sem.Normal0,
                                                                         Fo4EngineVertexLayout.Sem.Binormal0, Fo4EngineVertexLayout.Sem.BlendWeight0,
                                                                         Fo4EngineVertexLayout.Sem.BlendIndices0}

    Friend Function CpuRouteInputsOf(desc As ULong) As CpuRouteInputs
        Dim r As New CpuRouteInputs With {.Stride = Fo4EngineVertexLayout.Stride(desc), .Elements = New Fo4EngineVertexLayout.Element(4) {}, .Present = New Boolean(4) {}}
        Dim all = Fo4EngineVertexLayout.Elements(desc)
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
        For b = 0 To 255 : t(b) = Fo4EngineSkin.Unorm8(CByte(b)) : Next
        Return t
    End Function

    ''' <summary>Component <paramref name="j"/> of an element at byte <paramref name="o"/>; past its components, the GL default
    ''' (0, 0, 0, 1) (OpenGL 4.6 10.2).</summary>
    Private Function Component(stage As Byte(), e As Fo4EngineVertexLayout.Element, o As Integer, j As Integer) As Single
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
        Return ElementAt(stage, inp.Elements(k), i * inp.Stride + inp.Elements(k).Offset)
    End Function

    ''' <summary>The element at byte <paramref name="o"/> as the input assembler hands it (Component, four components).</summary>
    Private Function ElementAt(stage As Byte(), e As Fo4EngineVertexLayout.Element, o As Integer) As Vector4
        Return New Vector4(Component(stage, e, o, 0), Component(stage, e, o, 1), Component(stage, e, o, 2), Component(stage, e, o, 3))
    End Function

#If DEBUG Then
    ''' <summary>GATE ONLY (ola 2b, --fo4-vertex-constants' oracle): attribute <paramref name="sem"/> of vertex <paramref name="i"/> of an
    ''' engine stage of layout <paramref name="desc"/>, as the input assembler hands it (ElementAt); Nothing when the layout has no such
    ''' element (the VS then reads the GL current attribute value, OpenGL 4.6 10.2).</summary>
    Friend Function ElementValue(engineStage As Byte(), desc As ULong, i As Integer, sem As Fo4EngineVertexLayout.Sem) As Vector4?
        For Each e In Fo4EngineVertexLayout.Elements(desc)
            If e.Semantic = sem Then Return ElementAt(engineStage, e, i * Fo4EngineVertexLayout.Stride(desc) + e.Offset)
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
    ''' the engine's blend (Fo4EngineSkin.Blend: rows r0..r2, s), then the app's skin law on one vertex (FastSkin.UnVertice, the law
    ''' AppSkinLaw transcribes for the GPU route: P_world = p * M, N' = cofactors / det (D2), Cy' and Cx' = M * v normalised) over the
    ''' vectors the VS decodes and normalises (NORMAL0.xyz, BINORMAL0.xyz, (POSITION0.w, NORMAL0.w, BINORMAL0.w): rec2304 L93-98,
    ''' L106-109). Both layouts are written: [0] (P_world, s), [1..3] N', Cy', Cx', [4..6] the rows r0..r2 (w = the translation without
    ''' the eye: the VS subtracts s * eye). <paramref name="w"/>: a 4-Single scratch.</summary>
    Friend Sub PackCpuRoute(stage As Single(), i As Integer, engineStage As Byte(), inp As CpuRouteInputs, rows As Vector4(), w() As Single)
        Dim v = CpuRouteInputValues(engineStage, inp, i)
        Dim r0, r1, r2 As Vector4, s As Single
        Fo4EngineSkin.Blend(rows, v.BlendIndices, v.BlendWeight, w, r0, r1, r2, s)
        Dim m = Fo4EngineSkin.MatrixOfRows(r0, r1, r2)
        PackCpuRouteTail(stage, i, v.Position, v.Normal, v.Binormal, m,
                         FastSkin.MatrizDeNormales(m.M11, m.M12, m.M13, m.M21, m.M22, m.M23, m.M31, m.M32, m.M33), s, r0, r1, r2)
    End Sub

    ''' <summary>PackCpuRoute with the blend already done (review note "performance": one blend and one normal matrix per vertex and per
    ''' frame): <paramref name="m"/> = the view's per-vertex skin matrix (SkinMatricesSoA), when it is the engine's blend of the rows
    ''' this draw's cb10 holds (RenderableMesh.EngineSharesViewSkin), and <paramref name="e"/> = its FastSkin.MatrizDeNormales. s is the
    ''' chain over the weights alone (Fo4EngineSkin.EngineWeights, WeightSum), the rows are m's (RowsOfMatrix): the same Single as
    ''' PackCpuRoute's own blend of those rows.</summary>
    Friend Sub PackCpuRouteBlended(stage As Single(), i As Integer, engineStage As Byte(), inp As CpuRouteInputs, m As Matrix4,
                                   e As FastSkin.MatrizNormal, w() As Single)
        Dim v = CpuRouteInputValues(engineStage, inp, i)
        Fo4EngineSkin.EngineWeights(v.BlendWeight.X, v.BlendWeight.Y, v.BlendWeight.Z, w)
        Dim r0, r1, r2 As Vector4
        Fo4EngineSkin.RowsOfMatrix(m, r0, r1, r2)
        PackCpuRouteTail(stage, i, v.Position, v.Normal, v.Binormal, m, e, Fo4EngineSkin.WeightSum(w), r0, r1, r2)
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
        n = Fo4EngineSkin.NormalizeLikeVs(n) : cy = Fo4EngineSkin.NormalizeLikeVs(cy) : cx = Fo4EngineSkin.NormalizeLikeVs(cx)
#If DEBUG Then
        ' ShadowGate --gate-mutant no-vs-normalize (review rev-04; the one switch): the VS's normalisation skipped.
        If Fo4GBufferConstants.GateMutantOn("no-vs-normalize") Then n = crudos.n : cy = crudos.cy : cx = crudos.cx
#End If
        Dim pW As Vector3, nS As Vector3, cxS As Vector3, cyS As Vector3
        FastSkin.UnVerticeCon(m, e, New Vector3d(pos.X, pos.Y, pos.Z), n, cx, cy, False, pW, nS, cxS, cyS)
        Dim o = i * CpuRouteFloats
        stage(o) = pW.X : stage(o + 1) = pW.Y : stage(o + 2) = pW.Z : stage(o + 3) = s
        stage(o + 4) = nS.X : stage(o + 5) = nS.Y : stage(o + 6) = nS.Z : stage(o + 7) = 0.0F
        stage(o + 8) = cyS.X : stage(o + 9) = cyS.Y : stage(o + 10) = cyS.Z : stage(o + 11) = 0.0F
        stage(o + 12) = cxS.X : stage(o + 13) = cxS.Y : stage(o + 14) = cxS.Z : stage(o + 15) = 0.0F
        stage(o + 16) = r0.X : stage(o + 17) = r0.Y : stage(o + 18) = r0.Z : stage(o + 19) = r0.W
        stage(o + 20) = r1.X : stage(o + 21) = r1.Y : stage(o + 22) = r1.Z : stage(o + 23) = r1.W
        stage(o + 24) = r2.X : stage(o + 25) = r2.Y : stage(o + 26) = r2.Z : stage(o + 27) = r2.W
    End Sub
End Module
