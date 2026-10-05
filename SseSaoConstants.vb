Imports OpenTK.Mathematics

''' <summary>THE CPU SIDE OF THE SKYRIM SE SAO: the cb2 vectors of the six passes (CameraZ, Minify, MinifyContrast, RawAO, BlurH,
''' BlurV), built in Single arithmetic in the exe's operation order (SkyrimSE.exe 1.7.104, 0x141541F40;
''' Tools/re-docs/RE_SAO_BOTH_2026-10-03.md 1.2, 1.5, 1.6). One copy, read by the preview (ApplySseOpaqueComposite) and by the gate
''' (ShadowGate --sse-sao-law), so the law and its oracle are fed the same numbers.</summary>
Friend Module SseSaoConstants

    ' The Settings copied once by 0x141541E20 (RE_SAO_BOTH 1.2, .data defaults). Measured 3-oct-2026: the installed Skyrim.ini /
    ' SkyrimPrefs.ini carry only fDOFMaxDepthParticipation=10000, bSAOEnable=1 and bSAO_CS_Enable=0 of these keys.
    Friend Const SaoRadius As Single = 250.0F               ' fSAORadius:Display [0x143436384]
    Friend Const SaoBias As Single = 2.5F                   ' fSAOBias:Display [0x143436380]
    Friend Const SaoIntensity As Single = 15.0F             ' fSAOIntensity:Display [0x14343637C]
    Friend Const SaoExpFactor As Single = 0.11F             ' fSAOExpFactor:Display [0x143436388]
    Friend Const SaoValueDiffFactor As Single = 0.3F        ' fSAOValueDiffFactor:Display
    Friend Const DofApplyCenterWeight As Boolean = True     ' bDOFApplyCenterWeight:Display [0x14343639A]
    Friend Const DofCenterWeight As Single = 0.6F           ' fDOFCenterWeight:Display [0x143436390]
    ''' <summary>zmax = fDOFMaxDepthParticipation [0x143436394]: .data 50000; the installed Skyrim.ini has 10000, the value the
    ''' user chose (3-oct-2026).</summary>
    Friend Const DofMaxDepthParticipation As Single = 10000.0F

    ''' <summary>The levels of the CameraZ pyramid the preview builds: the engine makes n = min(max(floor(log2 W)+1,
    ''' floor(log2 H)+1), 12) ([0x143436358], RE 1.3) and RawAO reads mips 1..4 only (16012 asm 80-81), so levels 0..min(4, n-1).</summary>
    Friend Function PyramidLevels(w As Integer, h As Integer) As Integer
        Return Math.Min(5, CameraZMipCount(w, h))
    End Function

    ''' <summary>Number of CameraZ mips [0x143436358] = min(max(floor(log2 W)+1, floor(log2 H)+1), 12) (RE 1.3).</summary>
    Friend Function CameraZMipCount(w As Integer, h As Integer) As Integer
        Dim lw As Integer = 0, lh As Integer = 0
        Dim t As Integer = w
        While t > 1 : t >>= 1 : lw += 1 : End While
        t = h
        While t > 1 : t >>= 1 : lh += 1 : End While
        Return Math.Min(Math.Max(lw + 1, lh + 1), 12)
    End Function

    ''' <summary>CameraZ cb2[0] = (f*n, n-f, f, zmax) (141542129..141542160; n = cam+0x160, f = cam+0x164).</summary>
    Friend Function CameraZClipInfos(n As Single, f As Single, zmax As Single) As Vector4
        Dim nf As Single = f * n          ' 14154212D mulss xmm14(f), xmm15(n)
        Dim nmf As Single = n - f         ' 141542132 subss xmm15(n), xmm6(f)
        Return New Vector4(nf, nmf, f, zmax)
    End Function

    ''' <summary>Minify cb2 for destination mip i (141542220..141542297): Ws = W &gt;&gt; (i-1), Hs = H &gt;&gt; (i-1) (unsigned shifts),
    ''' cb2[0] = (Ws, Hs, i&lt;=4 ? 0.5/Ws : 0, i&lt;=4 ? 0.5/Hs : 0); cb2[2].x = integer (i == 1), raw bits.</summary>
    Friend Sub MinifyConstants(i As Integer, w As Integer, h As Integer, ByRef resolution As Vector4, ByRef useDynamicBits As UInteger)
        Dim ws As UInteger = CUInt(w) >> (i - 1)
        Dim hs As UInteger = CUInt(h) >> (i - 1)
        Dim fws As Single = CSng(ws), fhs As Single = CSng(hs)
        Dim ox As Single = If(i <= 4, 0.5F / fws, 0.0F)
        Dim oy As Single = If(i <= 4, 0.5F / fhs, 0.0F)
        resolution = New Vector4(fws, fhs, ox, oy)
        useDynamicBits = If(i = 1, 1UI, 0UI)
    End Sub

    ''' <summary>MinifyContrast replaces Minify ONCE, at the first i whose source height is &lt;= 16, with bDOFApplyCenterWeight
    ''' (141542363..1415423C5).</summary>
    Friend Function MinifyUsesContrast(i As Integer, h As Integer, contrastDone As Boolean) As Boolean
        Return DofApplyCenterWeight AndAlso Not contrastDone AndAlso (CUInt(h) >> (i - 1)) <= 16UI
    End Function

    ''' <summary>Contrast cb2[1].x = 1 / (fDOFCenterWeight + FLT_EPSILON) (141542377..141542388; xmm9 = 0x34000000).</summary>
    Friend Function ContrastScale(centerWeight As Single) As Single
        Dim eps As Single = BitConverter.UInt32BitsToSingle(&H34000000UI)
        Return 1.0F / (centerWeight + eps)
    End Function

    ''' <summary>RawAO cb2[0..3] (141541FDF..141542049, 14154241D..141542587).</summary>
    Friend Structure RawAOVectors
        Public ProjInfos As Vector4
        Public SsaoInfos As Vector4
        Public ScreenInfos As Vector4
        Public SsaoInfos2 As Vector4
    End Structure

    ''' <summary>frustum = NiCamera viewFrustum (cam+0x150..0x164: left, right, top, bottom, near, far); W', H' = the kMAIN size
    ''' &gt;&gt; bSAODownscaled (0). theta is not read by 16012 (cb2[1].x). powf(R, 6) = the CRT call 0x1415A98CA, emulated as the
    ''' correctly rounded float of R^6 (the reference does the same).</summary>
    Friend Function RawAOConstants(left As Single, right As Single, top As Single, bottom As Single, near As Single, far As Single,
                                   wPrime As Integer, hPrime As Integer) As RawAOVectors
        Dim k As New RawAOVectors()
        Dim rl As Single = right - left                     ' 14154201B
        Dim x7 As Single = 1.0F / rl : x7 = x7 + x7         ' 14154202C..141542034  (2/(r-l))
        Dim tb As Single = top - bottom                     ' 141542038
        Dim x8 As Single = 1.0F / tb : x8 = x8 + x8         ' 141542040..141542049  (2/(t-b))
        Dim fw As Single = CSng(CUInt(wPrime)), fh As Single = CSng(CUInt(hPrime))   ' 141542424, 141542447
        k.ProjInfos = New Vector4(-2.0F / (fw * x7),        ' 14154242D..14154243C
                                  -2.0F / (fh * x8),        ' 141542450..141542455
                                  1.0F / x7,                ' 141542459..14154245D
                                  1.0F / x8)                ' 141542461..141542465
        k.ScreenInfos = New Vector4(fw, fh, 1.0F / fw, 1.0F / fh)   ' 14154249E..1415424C6, 141542490..141542499
        Dim r6 As Single = CSng(Math.Pow(SaoRadius, 6.0))   ' 1415424F7 call powf
        k.SsaoInfos = New Vector4(0.0F,                     ' theta (MT19937), not read by 16012
                                  SaoRadius * 100.0F,       ' 1415424DB
                                  SaoBias,                  ' 1415424E3
                                  SaoIntensity / r6)        ' 1415424FC..141542504
        Dim nf As Single = far * near                       ' 14154212D
        Dim nmf As Single = near - far                      ' 141542132
        Dim d As Single = nmf * BitConverter.UInt32BitsToSingle(&H3C23D70AUI)   ' 14154254F (0.01f)
        d = d + far                                         ' 141542558
        k.SsaoInfos2 = New Vector4(SaoExpFactor,            ' 14154253A
                                   SaoRadius * SaoRadius,   ' 141542542
                                   SaoValueDiffFactor,      ' 141542547
                                   nf / d)                  ' 14154255F
        Return k
    End Function

    ''' <summary>Blur cb2[0] = (W', H', 1/W', 1/H') (141542815..141542847, 1415428C4..1415428F8).</summary>
    Friend Function BlurScreenInfos(wPrime As Integer, hPrime As Integer) As Vector4
        Dim fw As Single = CSng(CUInt(wPrime)), fh As Single = CSng(CUInt(hPrime))
        Return New Vector4(fw, fh, 1.0F / fw, 1.0F / fh)
    End Function

    ''' <summary>cb12[41] / [43] / [44] without dynamic resolution (RE 5.4: [0x1433364C8] != 0): (.w 0), (1,1,1,1), (1,1,1,1).</summary>
    Friend ReadOnly Cb12_41 As New Vector4(0.0F, 0.0F, 0.0F, 0.0F)
    Friend ReadOnly Cb12_43 As New Vector4(1.0F, 1.0F, 1.0F, 1.0F)
    Friend ReadOnly Cb12_44 As New Vector4(1.0F, 1.0F, 1.0F, 1.0F)
End Module
