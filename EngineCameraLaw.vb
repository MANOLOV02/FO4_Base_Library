Imports OpenTK.Mathematics

''' <summary>THE WORLD CAMERA'S DEPTH LAW OF BOTH ENGINES (ola 1, 12-oct-2026; RE in D:\WinTmp\rc\conv\conv.md and ola1.md): its near
''' and far without a world, SetViewFrustum's near, and the projection matrices the renderer builds, in the [0, 1] depth of D3D the
''' whole frame is drawn with (OpenGL 4.3: every vertex stage writes gl43ClipToGl, z = 2 z - w, Gl43.ClipGlsl; the window depth is
''' z / w of these matrices). Read by PreviewControl.UpdateProjection, ShadowMap.Fit
''' and the frame's notice (PreviewModel.ReportUndrawnShapes). Gate: ParityGate `camera-law`.</summary>
Friend Module EngineCameraLaw

    ''' <summary>fNearDistance:Display, the world SceneGraph's near getter: 15.0 (FO4 Setting 0x142F4CF28, value +8, getter
    ''' 0x1417EEFA0; SSE 0x1420CF9A8 / 0x140FF4BB0).</summary>
    Friend Const NearDistance As Single = 15.0F

    ''' <summary>The world camera's far without a world loaded: the far getter's branch for a null [0x1432DD158] (FO4 0x140C439C0 -&gt;
    ''' 0x140C43A4A, 353840.0 at 0x1426A0BD8; SSE 0x140665740 -&gt; 0x1406657C8, 0x141A70458). bAutoViewDistance:Display = 0
    ''' (0x142F4CF88) keeps it from frame to frame (0x1417EEFC8).</summary>
    Friend Const FarWithoutWorld As Single = 353840.0F

    ''' <summary>The world SceneGraph's NiCamera minNear: 1.0 (FO4 0x1417EED11, SSE 0x140FF4939).</summary>
    Friend Const MinNear As Single = 1.0F

    ''' <summary>NiCamera::SetViewFrustum (FO4 0x1416D0B70, SSE 0x140EEFAE0): near = max(n, f / ratio, minNear), ratio = f / n as
    ''' SetCameraFOV sets it from the same getters (FO4 0x1421C8D71, SSE 0x140EF182B). With that ratio it is max(n, 1.0) but for the
    ''' rounding of f / (f / n), which the float sequence keeps (camera-law: bit-equal to n for 15, 7, 0.5, 1 - the app's definition).</summary>
    Friend Function FrustumNear(n As Single, f As Single) As Single
        Dim ratio = f / n
        Dim near = n
        Dim porRatio = f / ratio
        If near < porRatio Then near = porRatio
        If near < MinNear Then near = MinNear
        Return near
    End Function

    ''' <summary>The engine's perspective (FO4 0x14182D8A4..0x14182D9C1, SSE 0x14101D48E..0x14101D585): inv = 1 / (f - n), M22 = f inv,
    ''' M32 = -((n f) inv), M23 = 1, M33 = 0, depth z / w in [0, 1]; for the app's GL view (-z forward, v_engine = (x, y, -z_gl), as
    ''' BuildFo4DeferredFrame) in OpenTK's row-vector layout: M33 = -f inv, M43 = -((n f) inv), M34 = -1, M44 = 0. A symmetric frustum:
    ''' M11 = 1 / tanH, M22 = 1 / tanV (the engine's 2 / (r - l), 2 / (t - b)); the field of view is the editor's
    ''' (PreviewControl.PreviewFovYDegrees).</summary>
    Friend Function Perspective(fovY As Single, aspect As Single, n As Single, f As Single) As Matrix4
        Dim tanV = MathF.Tan(fovY * 0.5F)
        Dim inv = 1.0F / (f - n)
        Return New Matrix4(1.0F / (aspect * tanV), 0.0F, 0.0F, 0.0F,
                           0.0F, 1.0F / tanV, 0.0F, 0.0F,
                           0.0F, 0.0F, -(f * inv), -1.0F,
                           0.0F, 0.0F, -((n * f) * inv), 0.0F)
    End Function

    ''' <summary>The engine's orthographic branch of the same builder (FO4 0x14182D947..0x14182D96A, SSE 0x14101D516..0x14101D537):
    ''' M00 = 2 / (r - l), M30 = -(l + r) / (r - l), M11 = 2 / (t - b), M31 = -(t + b) / (t - b), M22 = 1 / (f - n),
    ''' M32 = -(n / (f - n)), M33 = 1; for a GL view (-z forward) M33 = -1 / (f - n), M43 = -(n / (f - n)). The app's shadow map
    ''' (ShadowMap.Fit) draws with it, so the whole context has one depth convention.</summary>
    Friend Function Orthographic(l As Single, r As Single, b As Single, t As Single, n As Single, f As Single) As Matrix4
        Dim ix = 1.0F / (r - l), iy = 1.0F / (t - b), iz = 1.0F / (f - n)
        Return New Matrix4(ix + ix, 0.0F, 0.0F, 0.0F,
                           0.0F, iy + iy, 0.0F, 0.0F,
                           0.0F, 0.0F, -iz, 0.0F,
                           -((l + r) * ix), -((t + b) * iy), -(n * iz), 1.0F)
    End Function
End Module
