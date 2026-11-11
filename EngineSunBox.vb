Imports OpenTK.Mathematics

''' <summary>THE SUN'S SPLIT BOXES OF BOTH ENGINES OVER THE PREVIEW'S CAMERA (user decision 6-nov-2026: the box of the sun's shadow map is the
''' engine's law applied to the preview's camera, in both games; it replaces rev-27 / D-9, where the box was the app's fit of the scene). Inputs,
''' all the app's: its camera (eye, unit forward / right / up and the frustum half-extents at unit distance, the engine reads camera
''' +0x150..+0x16C and scales the axes; near, far), the rig light's direction (surface to light) and the map's resolution. Out, per split: the
''' fit - View anchored at the split camera's world translate (LightFit.Origin, the emitter's cb12[40]), its orthographic Proj - and
''' the view distance where the split ends (SSE light+0x5A4 + 4k, FO4 light+0x250 + 4k). verif_ojos propuesta_caja.md.</summary>
Friend Module EngineSunBox

    ''' <summary>One split of the sun: its fit and the view distance where it ends.</summary>
    Friend Structure SunSplit
        Public Fit As ShadowMapMath.LightFit
        Public EndDistance As Single
    End Structure

    ''' <summary>The unit Y both engines cross the sun with: SSE [0x1420CED58..60] (no writer in .text: its 4 lea are const arguments,
    ''' 0x140F39D13 / 0x140F3BD29 / 0x140F3E62B), FO4 [0x142924750] = (0, 1, 0).</summary>
    Private ReadOnly UnitY As New Vector3(0.0F, 1.0F, 0.0F)
    ''' <summary>Both engines' split camera stands 15000 along the sun (SSE [0x141B4E684], 0x1415808DB..925; FO4 [0x14291A500], 0x14225102B..13C).</summary>
    Private Const SunBack As Single = 15000.0F
    ''' <summary>Fallout 4's splits: iDirShadowSplits:Display .data 2 (0x142F985B8) = [light + 0x190].</summary>
    Friend Const Fo4SplitCount As Integer = 2

    ''' <summary>The 8 corners of the view between distances d0 and d1: eye + fwd d +- right tanH d +- up tanV d (SSE 0x141580221..4F9;
    ''' FO4 0x142250E90..1422510F7, 0x142251955..19CB).</summary>
    Private Function Corners(eye As Vector3, fwd As Vector3, right As Vector3, up As Vector3, tanH As Single, tanV As Single, d0 As Single, d1 As Single) As Vector3()
        Dim c(7) As Vector3, k = 0
        For Each d In {d0, d1}
            For Each sx In {-1.0F, 1.0F}
                For Each sy In {-1.0F, 1.0F}
                    c(k) = eye + fwd * d + right * (sx * tanH * d) + up * (sy * tanV * d) : k += 1
                Next
            Next
        Next
        Return c
    End Function

    ''' <summary>The splits of the game's sun. The direction of a static frame: both engines interpolate the sun's direction over frames (SSE
    ''' 0x14157F2A3..412 toward light+0x560 with the rate light+0x5B0; FO4 0x142250BCB..D68 with light+0x200 / +0x210 / +0x220): with the rig's
    ''' direction held, the interpolation ends at it and stays (SSE: t is set to 0 and the target stored, 0x14157F3FA..409; FO4: t reaches 1,
    ''' 0x142250CA8 minss 1.0, and the target is copied, 0x142250D40..47); the preview draws that end state.</summary>
    Friend Function Splits(isSse As Boolean, lightTo As Vector3, eye As Vector3, fwd As Vector3, right As Vector3, up As Vector3,
                           tanH As Single, tanV As Single, near As Single, far As Single, mapSize As Integer) As SunSplit()
        Return If(isSse, SseSplits(lightTo, eye, fwd, right, up, tanH, tanV, near, far, mapSize),
                         Fo4Splits(lightTo, eye, fwd, right, up, tanH, tanV, near, far, mapSize))
    End Function

    ' ================================================ SKYRIM SE (BSShadowDirectionalLight 0x14157F080) ================================================

    ''' <summary>iNumSplits 2; the slices SseUtilityConstants.EngineSplitSlices (0x14157FF1C..FB5, 0x14158005F..08E).</summary>
    Private Function SseSplits(lightTo As Vector3, eye As Vector3, fwd As Vector3, right As Vector3, up As Vector3, tanH As Single, tanV As Single,
                               near As Single, far As Single, mapSize As Integer) As SunSplit()
        Dim sl = SseUtilityConstants.EngineSplitSlices(near, far, SseUtilityConstants.EngineSplitCount)
        Dim r(SseUtilityConstants.EngineSplitCount - 1) As SunSplit
        For i = 0 To r.Length - 1
            r(i).Fit = SseSplitFit(lightTo, eye, Corners(eye, fwd, right, up, tanH, tanV, sl.Starts(i), sl.Ends(i)), mapSize)
            r(i).EndDistance = sl.Ends(i)
        Next
        Return r
    End Function

    Private Function SseSplitFit(lightTo As Vector3, eye As Vector3, c As Vector3(), mapSize As Integer) As ShadowMapMath.LightFit
        Dim f As New ShadowMapMath.LightFit With {.Valid = False}
        Dim L = Vector3.Normalize(lightTo)
        ' D = the direction the light travels: column 0 of the light node's world rotation (0x14157F25D..297, handed to 0x141581730 by
        ' 0x14157F412..41A and kept in light+0x560), the field DirLightDirection negates (NiDirectionalLight +0x140, 0x141549B4D..B6E).
        Dim D = -L
        ' The basis 0x14157F46C..5D6 (D = xmm6 / xmm7 / xmm15, 0x14157F374..38F): the products give Y x D, negated (0x14157F515..522):
        ' a = norm(D x Y); then D x a, normalized and negated (0x14157F5C0..5D2): b = -norm(D x a). |D x Y| <= 1e-6 ([0x141B5BDB0],
        ' 0x14157F4E9) leaves a = 0: the engine has no other rule (the rig light parallel to Y: no box, Render.SunSplitsDeLuz).
        Dim ax = Vector3.Cross(D, UnitY)
        If ax.Length <= 0.000001F Then Return f
        Dim a = Vector3.Normalize(ax)
        Dim b = -Vector3.Normalize(Vector3.Cross(D, a))
        ' The center (0x1415805AB..8D5): the camera's position snapped in the light's axes on the split's texel scale s = [split + 0xCC]. The split
        ' ctor writes 1.0 (0x14155C8A9, the leaf 0x14155C990 at +0x51) and its other writers belong to the viewport quantization, which bDirShadowMapFullViewPort
        ' = 1 ([0x1420D94D0], .data) skips (0x141580EEA): s = 1, the branch s >= 1 (0x14158072A): k = trunc(s) = 1, c = floor(p . e / k) k.
        Const S As Single = 1.0F
        Dim k = CSng(CUInt(Math.Truncate(S)))
        Dim snap = Function(v As Single) CSng(Math.Floor(v / k)) * k
#If DEBUG Then
        ' ShadowGate --sun-box-law [box]: the center not snapped to the grid.
        If Fo4GBufferConstants.GateMutantOn("sun-snap-off") Then snap = Function(v As Single) v
#End If
        Dim center = a * snap(Vector3.Dot(eye, a)) + b * snap(Vector3.Dot(eye, b)) + D * snap(Vector3.Dot(eye, D))   ' on (a, b, D)
        ' The split camera 15000 from the center toward the sun; the provisional ortho (0, 1, 1, 0, 10 [0x141B5BE80], 15000) (0x14158094F..9A4).
        Const ProvNear As Single = 10.0F
        Dim pos = center - D * SunBack                                              ' 0x1415808DB..925
        ' The split camera's rotation, columns (D, b, a) = NiCamera's direction, up and right (0x14157F9E6..FA31): the view's x = a, y = b, looking
        ' along D. In GL the view's z is -D = L, and a x b = L: rows (a, b, L) are a rotation, the engine's frame itself.
        Dim view = ViewOfRows(a, b, L, pos)
        Dim xmin = Single.MaxValue, xmax = Single.MinValue, ymin = Single.MaxValue, ymax = Single.MinValue
        Dim zmin = Single.MaxValue, zmax = Single.MinValue
        For Each p In c
            Dim v = Vector3.TransformPosition(p, view)
            Dim zn = ((-v.Z) - ProvNear) / (SunBack - ProvNear)                     ' WorldPtToScreenPt3 0x140EF0FB0, loop 0x141580A40..BD7
            xmin = Math.Min(xmin, v.X) : xmax = Math.Max(xmax, v.X)
            ymin = Math.Min(ymin, v.Y) : ymax = Math.Max(ymax, v.Y)
            zmin = Math.Min(zmin, zn) : zmax = Math.Max(zmax, zn)
        Next
        ' near / far 0x141580E86..EA1 (14980 [0x141B4E680], 20 [0x141B5BE8C]); the whole viewport (0x141580EEA); the frustum 0x141581456..499.
        Dim zNear = zmin * 14980.0F - 20.0F, zFar = zmax * 14980.0F + 20.0F
        Return MakeFit(view, pos, xmin, xmax, ymin, ymax, zNear, zFar, mapSize, k)
    End Function

    ' ================================================ FALLOUT 4 (0x142250A00) ================================================

    ''' <summary>The split ends (0x142251620..64D): split 0 800 [0x14292968C], split 1 3000 [0x142512C10], the last min([0x143E71D8C], the
    ''' camera's far) with [0x143E71D8C] = max(fDirShadowDistance .data 3000, 1000 [0x1429296B0]) (0x142181AA4..AC8; 0x142250A80 / B17). Split 0
    ''' starts at the camera's near (0x142251924..94E), every other at the previous far corners minus 2 blend along the rays (0x14225188C..91B;
    ''' blend = [0x143E71D94] = max(fBlendSplitDirShadow .data 100, 0), 0x142181AD0..AE7; the rays times blend 0x1422514EF..15E3).</summary>
    Private Function Fo4Splits(lightTo As Vector3, eye As Vector3, fwd As Vector3, right As Vector3, up As Vector3, tanH As Single, tanV As Single,
                               near As Single, far As Single, mapSize As Integer) As SunSplit()
        Dim Blend = Fo4DeferredLightLaw.SplitBlend
        Dim dirShadowDistance = Fo4DeferredLightLaw.DirShadowDistance
        Dim r(Fo4SplitCount - 1) As SunSplit
        Dim prevEnd = 0.0F
        For i = 0 To Fo4SplitCount - 1
            Dim e As Single = 1.0F                                                  ' xmm8 = 1.0 (0x142251620)
            If i = 0 Then e = 800.0F
            If i = 1 Then e = 3000.0F
            If i = Fo4SplitCount - 1 Then e = Math.Min(dirShadowDistance, far)
            Dim s = If(i = 0, near, prevEnd - 2.0F * Blend)
            r(i).Fit = Fo4SplitFit(lightTo, eye, Corners(eye, fwd, right, up, tanH, tanV, s, e), mapSize)
            r(i).EndDistance = e
            prevEnd = e
        Next
        Return r
    End Function

    Private Function Fo4SplitFit(lightTo As Vector3, eye As Vector3, c As Vector3(), mapSize As Integer) As ShadowMapMath.LightFit
        Dim f As New ShadowMapMath.LightFit With {.Valid = False}
        Dim Lp = -Vector3.Normalize(lightTo)                                         ' the sun's travel (the light's row, 0x142250B63..BC7)
        ' The basis 0x142250D68..E89 (SIMD: the shuffles give Y x V, negated at 0x142250DE5: A = norm(V x Y); then V x A, negated at
        ' 0x142250E62: B = -norm(V x A)), V = the light's travel Lp (the light camera stands at eye - 15000 V, 0x1422510FF..13C: toward the sun).
        Dim ax = Vector3.Cross(Lp, UnitY)
        If ax.Length <= 0.000001F Then Return f
        Dim A = Vector3.Normalize(ax)
        Dim B = -Vector3.Normalize(Vector3.Cross(Lp, A))
        ' A x B = -Lp: rows (A, B, -Lp) are a GL rotation looking along Lp, the engine's frame itself (no mirror).
        ' The light camera 15000 behind the camera (0x1422510FF..13C); the corners by the matrix of 0x142253030 (scale (1/20000, 1/20000, 1)
        ' [0x14291A4D0], translation (0.5, 0.5, 15000) [0x14291A4E0]) and back (x 20000 - 10000, 0x142251F4A..FBA): x = (P - eye) . A,
        ' z = (P - eye) . Lp + 15000 (the min / max loop 0x1422519F0..B87, stored light + 0x260..0x274).
        Dim xmin = Single.MaxValue, xmax = Single.MinValue, ymin = Single.MaxValue, ymax = Single.MinValue, zmax = Single.MinValue
        For Each p In c
            Dim q = p - eye
            Dim x = Vector3.Dot(q, A), y = Vector3.Dot(q, B), zq = Vector3.Dot(q, Lp) + SunBack
            xmin = Math.Min(xmin, x) : xmax = Math.Max(xmax, x)
            ymin = Math.Min(ymin, y) : ymax = Math.Max(ymax, y)
            zmax = Math.Max(zmax, zq)
        Next
        ' Near 150 [0x1429295A8], far zmax + 150 (0x142251F86..FBE).
        Const Near150 As Single = 150.0F
        Dim zFar = zmax + Near150
        ' The texel (0x142252037..1B5): side = max(floor(r - l), floor(t - b)) as ushort, s = side / res; s <= 0.5 [0x142929448]: k = floor(1 / s),
        ' [+0xCC] = -k, texel 1 / k; else [+0xCC] = ceil(s), texel ceil(s).
        Dim side = CSng(Math.Max(CInt(Math.Floor(xmax - xmin)) And &HFFFF, CInt(Math.Floor(ymax - ymin)) And &HFFFF))
        Dim sc = side * (1.0F / mapSize)
        Dim cc As Integer, texel As Single
        If Not (sc > 0.5F) Then
            Dim kk = CSng(Math.Floor(1.0F / sc)) : cc = -CInt(kk) : texel = 1.0F / kk
        Else
            Dim n = CSng(Math.Ceiling(sc)) : cc = CInt(n) : texel = n
        End If
        ' The snap (0x1422521B5..62D): v = float([+0xCC]), inv = 1 / v; [+0xCC] > 0: x' = floor(x inv) v, else x' = floor(x v) inv - on the light
        ' camera's coordinates in (A, B, Lp) (its new translate, 0x142252441..45F) and on l, r, t, b.
        Dim vv = CSng(cc), inv = 1.0F / vv
        Dim snap = Function(x As Single) If(cc > 0, CSng(Math.Floor(x * inv)) * vv, CSng(Math.Floor(x * vv)) * inv)
#If DEBUG Then
        ' ShadowGate --sun-box-law [box]: the camera and the frustum not snapped to the texel grid.
        If Fo4GBufferConstants.GateMutantOn("sun-snap-off") Then snap = Function(x As Single) x
#End If
        Dim cam0 = eye - Lp * SunBack
        Dim pos = A * snap(Vector3.Dot(cam0, A)) + B * snap(Vector3.Dot(cam0, B)) + Lp * snap(Vector3.Dot(cam0, Lp))
        Dim l2 = snap(xmin), r2 = snap(xmax), t2 = snap(ymax), b2 = snap(ymin)
        ' The viewport (0x14225263E..784): w = trunc(r' - l') / texel, h = trunc(t' - b') / texel pixels from the map's corner: over the whole layer
        ' the same texels are the box (l', l' + (r' - l') res / w) x (b', b' + (t' - b') res / h).
        Dim w = CSng(Math.Truncate(CSng(CInt(Math.Truncate(r2 - l2)) And &HFFFF) * (1.0F / texel)))
        Dim h = CSng(Math.Truncate(CSng(CInt(Math.Truncate(t2 - b2)) And &HFFFF) * (1.0F / texel)))
        If w <= 0.0F OrElse h <= 0.0F Then Return f
        Dim rFull = l2 + (r2 - l2) * mapSize / w
        Dim tFull = b2 + (t2 - b2) * mapSize / h
        Dim view = ViewOfRows(A, B, -Lp, pos)
        Return MakeFit(view, pos, l2, rFull, b2, tFull, Near150, zFar, mapSize, texel)
    End Function

    ' ================================================ THE FOCUS SHADOW OF BOTH ENGINES ================================================

    ''' <summary>focusShadowMapDoubleEveryXUnit:Display .data 450 (SSE [0x1420D9020], FO4 [0x142F99938]).</summary>
    Private Const FocusDoubleEvery As Single = 450.0F

    ''' <summary>THE FOCUS SHADOW'S BOX of a light (user decision 8-nov-2026: both engines, per casting rig light; what the preview shows is its subject). SSE 0x14155CA00, FO4
    ''' 0x14226F5A0: the sun's axes (SSE [split 0's camera + 0x48..0x64], FO4 [+0x30..0x48], 0x1421F64E4..51F), the camera at the node's world bound's centre - k r along the
    ''' sun's travel (k: SSE 1.1 [0x141814558], 0x14155CBB3..C0E; FO4 1, 0x14226F631..68A); the frustum +-r, near 0.1 r (SSE [0x141B5BDE4], FO4 [0x142929414]), far 2.2 r (SSE
    ''' [0x141B4441C], FO4 [0x14247D3B8]), orthographic. Its side in texels: trunc(b 2^max(0, (4 x 450 - d) / 450)) capped by the map (SSE b = 50 [0x141B5BEB4], 4 [0x141B5BE68], 2
    ''' [0x141B5BE44], 0x14155CD16..DB4; FO4 b = 128 [0x142483E50], 4 [0x1429294B8], 2 [0x142929498], 0x14226F7C6..8AB), d = |the node's world translate - the camera's|. The
    ''' draw and the receiver's matrix take the frustum grown to the whole map, the same texels (SSE 0x14155D43F..4F2; FO4 the sun's own 0x14225263E..784): what MakeFit gets here.</summary>
    Friend Function FocusFit(isSse As Boolean, sunFit As ShadowMapMath.LightFit, center As Vector3, r As Single, nodeTranslate As Vector3, eye As Vector3, mapSize As Integer) As ShadowMapMath.LightFit
        Dim x As New Vector3(sunFit.View.M11, sunFit.View.M21, sunFit.View.M31)
        Dim y As New Vector3(sunFit.View.M12, sunFit.View.M22, sunFit.View.M32)
        Dim z As New Vector3(sunFit.View.M13, sunFit.View.M23, sunFit.View.M33)     ' toward the light (the sun's rows: SSE (a, b, L), FO4 (A, B, -Lp))
        Dim pos = center + z * (If(isSse, 1.1F, 1.0F) * r)
        Dim d = (nodeTranslate - eye).Length
        Dim e = Math.Max(0.0F, (4.0F * FocusDoubleEvery - d) / FocusDoubleEvery)
        Dim side = Math.Min(Math.Truncate(If(isSse, 50.0F, 128.0F) * MathF.Pow(2.0F, e)), CDbl(mapSize))
        Dim span = 2.0F * r * mapSize / CSng(side)
        Return MakeFit(ViewOfRows(x, y, z, pos), pos, -r, -r + span, -r, -r + span, 0.1F * r, 2.2F * r, mapSize, 0.0F)
    End Function

    ''' <summary>The view (OpenTK row vectors) whose rows are the camera's x, y, z axes, anchored at <paramref name="pos"/>.</summary>
    Private Function ViewOfRows(x As Vector3, y As Vector3, z As Vector3, pos As Vector3) As Matrix4
        Return New Matrix4(x.X, y.X, z.X, 0.0F,
                           x.Y, y.Y, z.Y, 0.0F,
                           x.Z, y.Z, z.Z, 0.0F,
                           -Vector3.Dot(x, pos), -Vector3.Dot(y, pos), -Vector3.Dot(z, pos), 1.0F)
    End Function

    Private Function MakeFit(view As Matrix4, pos As Vector3, l As Single, r As Single, b As Single, t As Single, n As Single, fz As Single,
                             res As Integer, snapStep As Single) As ShadowMapMath.LightFit
        Dim f As New ShadowMapMath.LightFit
        f.View = view
        f.Proj = EngineCameraLaw.Orthographic(l, r, b, t, n, fz)
        f.ViewProj = f.View * f.Proj
        f.Radius = 0.5F * Math.Max(r - l, t - b)
        f.TexelWorld = Math.Max(r - l, t - b) / res
        f.Center = pos
        f.Origin = pos
        f.DepthRange = fz - n
        f.ZNear = n : f.ZFar = fz
        f.FrustumLeft = l : f.FrustumRight = r : f.FrustumBottom = b : f.FrustumTop = t
        f.SnapStep = snapStep
        f.Valid = True
        Return f
    End Function
End Module