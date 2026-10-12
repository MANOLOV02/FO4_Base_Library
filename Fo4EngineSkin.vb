Imports OpenTK.Mathematics

''' <summary>THE ENGINE'S SKIN BLEND on the CPU (ola 2 v5, user decision 12-oct-2026: "no mezcles camino CPU y GPU, pero deben ser
''' consistentes"): the palette rows the engine VS reads (cb10) and the blend its 155 skinned b09 VS run, one function each, for
''' the GPU route's palette and the CPU route's stream (Fo4EngineVertexPacker.PackCpuRoute). The law, the same in all 155
''' (measured over Shaders011.fxp b09; transcribed from rec2304 L47-86):
'''  * index k = ftoi(BLENDINDICES.k * 765.01) (L50-51) = 3 * the bone byte: the first of the bone's three rows;
'''  * w3 = 1 - sat((w1 + w0) + w2) on the three half weights (L47-49);
'''  * row r of the blend = ((w1 * row(i1 + r) then w0 * row(i0 + r) + . then w2 * row(i2 + r) + . then w3 * row(i3 + r) + .)
'''    (L54-61, L69-77, L79-86: mul, mad, mad, mad in that order);
'''  * a row index outside the bound buffer reads 0 (D3D11.3 7.5, d3d_cb10).
''' The engine adds -eye to each row's w before weighting (L52-53); the CPU leaves it out and the CPU-route VS subtracts s * eye
''' (s = the same chain over the weights alone), by distributivity (asm2glsl.cpu_route_body).
''' <para>THE ONE RULE OF FALLOUT 4 (user decision 12-oct-2026, D21: the checkbox "Replicate engine skin-weight normalization" is
''' gone): the view (G-buffer, Vertex_FO4's shadow and forward, picking, bounds, the legacy VBOs) and the bake use this law, always
''' (UsesEngineLaw). The bake keeps its own accumulation (SkinningHelper.BlendBoneMatrices) with these weights - the CPU SkinBlend
''' of the engine and the CK, CreationKit.exe 0x142B73230 = Fallout4.exe 0x141837390, instruction for instruction: w3 computed, not
''' read, no renormalisation. Skyrim SE keeps its normalised law (not verified by RE there).</para></summary>
Public Module Fo4EngineSkin

    ''' <summary>Weight slots of the law: SkinBlend reads three and computes the fourth (0x141837390; rec2304 L47-49).</summary>
    Public Const Slots As Integer = 4

    ''' <summary>cb10 of a draw: 3 rows per bone, row r of bone k = column r of OpenTK's (bones(k) * transform) (the engine's 3x4 bone
    ''' rows, 0x1422303D0; OpenTK multiplies row vectors). <paramref name="rows"/> is resized to 3 * bones.Length.</summary>
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

    ''' <summary>A BLENDINDICES byte as the input assembler hands it to the VS: UNORM8 = b / 255 (D3D11.3 3.2.3.6).</summary>
    Friend Function Unorm8(b As Byte) As Single
        Return CSng(b) / 255.0F
    End Function

    ''' <summary>THE GATE, one for the view and the bake (user decision 12-oct-2026, D21): Fallout 4 and a 4-slot skin. Every path of
    ''' the view - the CPU route's stream, the per-vertex skin matrix (Vertex_FO4's CPU skinning, the shadow, picking, bounds, the
    ''' world cache), Vertex_FO4's GPU weights - and the bake's weights. The law is 4-slot (rec2304 L47-61): a skin with another slot
    ''' count has none to replicate; Skyrim SE keeps its normalised law.</summary>
    Public Function UsesEngineLaw(weightsPerVertex As Integer) As Boolean
        Return weightsPerVertex = Slots AndAlso Config_App.Current IsNot Nothing AndAlso Config_App.Current.Game = Config_App.Game_Enum.Fallout4
    End Function

    ''' <summary>The bake's entry to the law (SkinningHelper.BlendBoneMatrices): the four weights of the vertex at
    ''' <paramref name="baseSlot"/> of the flat half array, EngineWeights; False (<paramref name="w"/> untouched) when the law does not
    ''' apply (UsesEngineLaw) or the slots are out of the array.</summary>
    Friend Function TryEngineWeights(flatWgt As System.Half(), baseSlot As Integer, wpv As Integer, w() As Single) As Boolean
        If flatWgt Is Nothing OrElse w Is Nothing OrElse w.Length < Slots OrElse Not UsesEngineLaw(wpv) Then Return False
        If baseSlot < 0 OrElse baseSlot + Slots > flatWgt.Length Then Return False
        EngineWeights(CSng(flatWgt(baseSlot)), CSng(flatWgt(baseSlot + 1)), CSng(flatWgt(baseSlot + 2)), w)
        Return True
    End Function

    ''' <summary>The homogeneous anchor (M44 = 1) of a matrix that came out of a weighted blend: call it before inverting a skinning
    ''' Mtot. The blend scales the 16 elements, M44 included (M44 = sum of the weights); the engine blends a 3x4 whose row 3 stays
    ''' [0, 0, 0, 1] (SkinBlend 0x142B73230), so Matrix4d.Invert would otherwise bring in a 1/s that cancels the law.</summary>
    Public Function ReanchorAffine(m As Matrix4d) As Matrix4d
        m.M44 = 1.0
        Return m
    End Function

    ''' <summary>Blend over the app's skin arrays (the bytes and halves the engine stage packs: Fo4EngineVertexPacker.PackVertex), slots
    ''' <paramref name="baseSlot"/>..+3, as the VS reads them; the one blend of the view (SkinningHelper's per-vertex matrix) and of
    ''' the CPU route.</summary>
    Friend Sub BlendSkin(rows As Vector4(), boneIndices As Byte(), boneWeights As System.Half(), baseSlot As Integer, w() As Single,
                         ByRef r0 As Vector4, ByRef r1 As Vector4, ByRef r2 As Vector4, ByRef s As Single)
        Blend(rows, New Vector4(Unorm8(boneIndices(baseSlot)), Unorm8(boneIndices(baseSlot + 1)), Unorm8(boneIndices(baseSlot + 2)), Unorm8(boneIndices(baseSlot + 3))),
              New Vector4(CSng(boneWeights(baseSlot)), CSng(boneWeights(baseSlot + 1)), CSng(boneWeights(baseSlot + 2)), CSng(boneWeights(baseSlot + 3))),
              w, r0, r1, r2, s)
    End Sub

    ''' <summary>The engine's four weights of a vertex from the first three BLENDWEIGHT components the VS reads (R16G16B16A16_FLOAT:
    ''' the half values, exact in Single; rec2304 L47-49): w(0..2) as read, w(3) = 1 - sat((w1 + w0) + w2), in Single. ONE law:
    ''' the view, the CPU route and the bake (TryEngineWeights).</summary>
    Friend Sub EngineWeights(w0 As Single, w1 As Single, w2 As Single, w() As Single)
        w(0) = w0 : w(1) = w1 : w(2) = w2
        Dim acc As Single = w(1) + w(0)
        acc = acc + w(2)
        If Not (acc > 0.0F) Then acc = 0.0F Else If acc > 1.0F Then acc = 1.0F       ' D3D11.3 _sat: NaN -> 0
        w(3) = 1.0F - acc
    End Sub

    ''' <summary>The first palette row of a BLENDINDICES component as the VS reads it (UNORM8 = b / 255, D3D11.3 3.2.3.6; rec2304
    ''' L50-51): ftoi(x * 765.01), toward zero (NaN -> 0 and the int range, D3D11.3 ftoi).</summary>
    Friend Function RowIndex(x As Single) As Integer
        Dim f As Single = x * 765.01F
        If Single.IsNaN(f) Then Return 0
        If f >= 2147483648.0F Then Return Integer.MaxValue
        If f <= -2147483648.0F Then Return Integer.MinValue
        Return CInt(Math.Truncate(f))
    End Function

    ''' <summary>Row <paramref name="i"/> of cb10 as the VS reads it: a row outside the bound buffer reads 0 (D3D11.3 7.5, d3d_cb10). The
    ''' one read of the palette: Blend and ShadowGate G5b's oracle.</summary>
    Friend Function PaletteRow(rows As Vector4(), i As Integer) As Vector4
        If i < 0 OrElse i >= rows.Length Then Return Vector4.Zero         ' D3D11.3 7.5 (d3d_cb10)
        Return rows(i)
    End Function

    ''' <summary>The blended rows 0..2 of one vertex and s = the same chain over the weights (the engine's order, see the module), from
    ''' BLENDINDICES / BLENDWEIGHT as the VS reads them. <paramref name="w"/> is a 4-Single scratch.</summary>
    Friend Sub Blend(rows As Vector4(), indices As Vector4, weights As Vector4, w() As Single,
                     ByRef r0 As Vector4, ByRef r1 As Vector4, ByRef r2 As Vector4, ByRef s As Single)
        EngineWeights(weights.X, weights.Y, weights.Z, w)
        Dim i0 = RowIndex(indices.X), i1 = RowIndex(indices.Y), i2 = RowIndex(indices.Z), i3 = RowIndex(indices.W)
        r0 = BlendRow(rows, 0, i0, i1, i2, i3, w)
        r1 = BlendRow(rows, 1, i0, i1, i2, i3, w)
        r2 = BlendRow(rows, 2, i0, i1, i2, i3, w)
        s = WeightSum(w)
    End Sub

    ''' <summary>s of a vertex: the blend's chain over the four engine weights alone (w1, then w0 + ., w2 + ., w3 + .; the order of
    ''' BlendRow), the factor of the eye the CPU-route VS subtracts (asm2glsl.cpu_route_body).</summary>
    Friend Function WeightSum(w() As Single) As Single
        Dim s As Single = w(1)
        s = w(0) + s
        s = w(2) + s
        s = w(3) + s
        Return s
    End Function

    Private Function BlendRow(rows As Vector4(), r As Integer, i0 As Integer, i1 As Integer, i2 As Integer, i3 As Integer, w() As Single) As Vector4
        Dim a = PaletteRow(rows, i1 + r), b = PaletteRow(rows, i0 + r), c = PaletteRow(rows, i2 + r), d = PaletteRow(rows, i3 + r)
        Dim x As Single = a.X * w(1), y As Single = a.Y * w(1), z As Single = a.Z * w(1), t As Single = a.W * w(1)
        x = w(0) * b.X + x : y = w(0) * b.Y + y : z = w(0) * b.Z + z : t = w(0) * b.W + t
        x = w(2) * c.X + x : y = w(2) * c.Y + y : z = w(2) * c.Z + z : t = w(2) * c.W + t
        x = w(3) * d.X + x : y = w(3) * d.Y + y : z = w(3) * d.Z + z : t = w(3) * d.W + t
        Return New Vector4(x, y, z, t)
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

    ''' <summary>A vertex vector the engine VS decodes and normalises before it skins it (rec2304 L94-98 / L106-109): dp3, rsq, mul,
    ''' in Single.</summary>
    Friend Function NormalizeLikeVs(v As Vector3) As Vector3
        Dim d As Single = v.X * v.X + v.Y * v.Y
        d = d + v.Z * v.Z
        Dim r = 1.0F / MathF.Sqrt(d)
        Return New Vector3(r * v.X, r * v.Y, r * v.Z)
    End Function
End Module
