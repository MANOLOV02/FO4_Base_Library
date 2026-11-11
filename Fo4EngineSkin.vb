''' <summary>THE FALLOUT 4 ROW of the engine's skin law (EngineSkin: one law, a row per game; ola 2 v5, user decision 12-oct-2026, D21:
''' the checkbox "Replicate engine skin-weight normalization" is gone): the four weights its 155 skinned b09 VS blend with (measured over
''' Shaders011.fxp b09; transcribed from rec2304 L47-49) - w0, w1, w2 as read, w3 = 1 - sat((w1 + w0) + w2). The view, the CPU route
''' and the bake take them through EngineSkin (EngineWeights, TryEngineWeights): the bake keeps its own accumulation
''' (SkinningHelper.BlendBoneMatrices) with these weights - the CPU SkinBlend of the engine and the CK, CreationKit.exe 0x142B822E0 =
''' Fallout4.exe 0x141837850, instruction for instruction: w3 computed, not read, no renormalisation. The edit views' GLSL
''' (Shader_Class_Fo4EditView) names this seat.</summary>
Public Module Fo4EngineSkin

    ''' <summary>The engine's four weights of a vertex from the first three BLENDWEIGHT components the VS reads (R16G16B16A16_FLOAT:
    ''' the half values, exact in Single; rec2304 L47-49): w(0..2) as read, w(3) = 1 - sat((w1 + w0) + w2), in Single. ONE law:
    ''' the view, the CPU route and the bake (EngineSkin.EngineWeights / TryEngineWeights).</summary>
    Friend Sub EngineWeights(w0 As Single, w1 As Single, w2 As Single, w() As Single)
        w(0) = w0 : w(1) = w1 : w(2) = w2
        Dim acc As Single = w(1) + w(0)
        acc = acc + w(2)
        If Not (acc > 0.0F) Then acc = 0.0F Else If acc > 1.0F Then acc = 1.0F       ' D3D11.3 _sat: NaN -> 0
        w(3) = 1.0F - acc
    End Sub
End Module
