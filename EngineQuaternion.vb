Imports System.Numerics

''' <summary>A quaternion in the engine's memory order: w at +0, then x, y, z (NiQuaternion; the file's four floats in that order,
''' loaded sequentially by FO4 0x1416CDA40 / SSE 0x140F60100). NiflySharp reads the same four floats into System.Numerics.Quaternion
''' X, Y, Z, W (NiStreamReversible.Sync(ref Quaternion), NiStreamReversible.cs:292-299), so the file's w is .X there: FromFile.</summary>
Friend Structure EngineQuat
    Friend W As Single
    Friend X As Single
    Friend Y As Single
    Friend Z As Single

    Friend Sub New(w As Single, x As Single, y As Single, z As Single)
        Me.W = w : Me.X = x : Me.Y = y : Me.Z = z
    End Sub

    Friend Shared Function FromFile(q As Quaternion) As EngineQuat
        Return New EngineQuat(q.X, q.Y, q.Z, q.W)
    End Function
End Structure

''' <summary>THE quaternion and sin / cos arithmetic of the engine's animation code (ola 0c parte 2): each function compared FO4 /
''' SSE instruction by instruction (D:\WinTmp\rc\ola0c\re7\cmpr.py: identical) and transcribed in the exe's operation order.</summary>
Friend Module QuaternionLaw

    ''' <summary>a b (FO4 0x1416CDC20 = SSE 0x140EF96E0): x = ((bw ax + bx aw) + bz ay) - by az, y = ((by aw + bw ay) + bx az) - bz ax,
    ''' z = ((bw az + bz aw) + by ax) - bx ay, w = ((bw aw - bx ax) - by ay) - bz az.</summary>
    Friend Function QMul(a As EngineQuat, b As EngineQuat) As EngineQuat
        Return New EngineQuat(((b.W * a.W - b.X * a.X) - b.Y * a.Y) - b.Z * a.Z,
                              ((b.W * a.X + b.X * a.W) + b.Z * a.Y) - b.Y * a.Z,
                              ((b.Y * a.W + b.W * a.Y) + b.X * a.Z) - b.Z * a.X,
                              ((b.W * a.Z + b.Z * a.W) + b.Y * a.X) - b.X * a.Y)
    End Function

    ''' <summary>The conjugate (FO4 0x1416CD300 = SSE 0x140EF8D30): (w, -x, -y, -z).</summary>
    Friend Function QConj(q As EngineQuat) As EngineQuat
        Return New EngineQuat(q.W, -q.X, -q.Y, -q.Z)
    End Function

    ''' <summary>Log (FO4 0x1416CD340 = SSE 0x140EF8D70): theta = pi (0x40490FDB) when not w &gt; -1, 0 when w &gt;= 1, else acosf(w);
    ''' k = theta / sinf(theta) when |sinf(theta)| &gt;= 0.001 (0x3A83126F), else 1; (0, k x, k y, k z). acosf / sinf: the UCRT's (FO4
    ''' imports at 0x142441240 / 0x142441198, SSE 0x1417C8DE8 / 0x1417C8EC0), the ones MathF.Acos / MathF.Sin call on Windows.</summary>
    Friend Function QLog(q As EngineQuat) As EngineQuat
        Dim th As Single
        If Not (q.W > -1.0F) Then
            th = BitConverter.Int32BitsToSingle(&H40490FDB)
        ElseIf q.W >= 1.0F Then
            th = 0.0F
        Else
            th = MathF.Acos(q.W)
        End If
        Dim s = MathF.Sin(th)
        Dim k = 1.0F
        If MathF.Abs(s) >= BitConverter.Int32BitsToSingle(&H3A83126F) Then k = th / s
        Return New EngineQuat(0.0F, k * q.X, k * q.Y, k * q.Z)
    End Function

    ''' <summary>Exp (FO4 0x1416E8490 = SSE 0x140F0A820): theta = sqrt((x x + y y) + z z), (s, c) = SinCos(theta), k = s / theta when
    ''' |s| &gt;= 0.001, else 1; (c, x k, y k, z k). Nothing when SinCos is not determinable.</summary>
    Friend Function QExp(v As EngineQuat) As EngineQuat?
        Dim th = MathF.Sqrt((v.X * v.X + v.Y * v.Y) + v.Z * v.Z)
        Dim sc = SinCos(th)
        If Not sc.HasValue Then Return Nothing
        Dim k = If(MathF.Abs(sc.Value.S) >= BitConverter.Int32BitsToSingle(&H3A83126F), sc.Value.S / th, 1.0F)
        Return New EngineQuat(sc.Value.C, v.X * k, v.Y * k, v.Z * k)
    End Function

    ''' <summary>Slerp as the exe computes it (FO4 0x1416CD100 = SSE 0x140EF8B30), a corrected lerp with no sign test (the load aligned
    ''' the keys): dot = ((q.x p.x + q.w p.w) + p.y q.y) + p.z q.z; k = (1 - dot c1)^2 c2 (c1 0x3F52A2D1, c2 0x3F15E2D1); t' = for t &gt;
    ''' 0.5, u = 1 - t, 1 - (((((u + u) - 3) (k u) + 1) + k) u), else ((((t + t) - 3) (k t) + 1) + k) t; o = (q - p) t' + p per lane;
    ''' then o x (1 / sqrt(((ow ow + ox ox) + oy oy) + oz oz)).</summary>
    Friend Function Slerp(t As Single, p As EngineQuat, q As EngineQuat) As EngineQuat
        Dim dot = ((q.X * p.X + q.W * p.W) + p.Y * q.Y) + p.Z * q.Z
        Dim k = 1.0F - dot * BitConverter.Int32BitsToSingle(&H3F52A2D1)
        k = (k * k) * BitConverter.Int32BitsToSingle(&H3F15E2D1)
        Dim tt As Single
        If t > 0.5F Then
            Dim u = 1.0F - t
            tt = 1.0F - (((((u + u) - 3.0F) * (k * u) + 1.0F) + k) * u)
        Else
            tt = ((((t + t) - 3.0F) * (k * t) + 1.0F) + k) * t
        End If
        Dim ow = (q.W - p.W) * tt + p.W, ox = (q.X - p.X) * tt + p.X, oy = (q.Y - p.Y) * tt + p.Y, oz = (q.Z - p.Z) * tt + p.Z
        Dim inv = 1.0F / MathF.Sqrt(((ow * ow + ox * ox) + oy * oy) + oz * oz)
        Return New EngineQuat(inv * ow, inv * ox, inv * oy, inv * oz)
    End Function

    ''' <summary>Squad (FO4 0x1416CD560 = SSE 0x140EF8F90): Slerp((t + t)(1 - t), Slerp(t, p, q), Slerp(t, a, b)).</summary>
    Friend Function Squad(t As Single, p As EngineQuat, a As EngineQuat, b As EngineQuat, q As EngineQuat) As EngineQuat
        Return Slerp((t + t) * (1.0F - t), Slerp(t, p, q), Slerp(t, a, b))
    End Function

    ''' <summary>The exe's sin / cos (FO4 0x1422BF150 = SSE 0x1415A8050). |x| &lt; 2^-13 (0x39000000) or unordered: (x, 1 - (|x| |x|)
    ''' 0.5) (0x1422BF17D..195); else sin of the lanes x and |x| + pi/2 (0x3FC90FDB) by the main path 0x1422C0049..0D3 (SinLane) when
    ''' both are at most 10000 (0x461C4000, `pcmpgtd`); beyond, the path 0x1422C00D4.. is not transcribed: Nothing. The other two lanes
    ''' of the register are 0 in every caller (movss loads, the sqrt into a cleared register: 0x14176D3D3..3E8, 0x1416E84DA..EE).</summary>
    Friend Function SinCos(x As Single) As (S As Single, C As Single)?
        Dim ax = BitConverter.Int32BitsToSingle(BitConverter.SingleToInt32Bits(x) And &H7FFFFFFF)
        If Not (ax >= BitConverter.Int32BitsToSingle(&H39000000)) Then Return (x, 1.0F - (ax * ax) * 0.5F)
        Dim l1 = ax + BitConverter.Int32BitsToSingle(&H3FC90FDB)
        If BitConverter.SingleToInt32Bits(ax) > &H461C4000 OrElse BitConverter.SingleToInt32Bits(l1) > &H461C4000 Then Return Nothing
        Return (SinLane(x), SinLane(l1))
    End Function

    ''' <summary>One lane of 0x1422C0049..0D3: n = cvtps2dq(v (1/pi) [0x3EA2F983]) (round to nearest even), r = ((v - n 3.140625) -
    ''' n 9.6750259e-4) - n 1.5099067e-7, r1 = r - n 5.126688e-12, r2 = r r, p = ((((2.608e-6 r2 - 1.98107e-4) r2 + 8.3330749e-3) r2 -
    ''' 0.16666658) r2 (the constants by bits), sin = p r1 + r1 with its sign flipped when n is odd (pslld 31, pxor).</summary>
    Private Function SinLane(v As Single) As Single
        Dim n = CInt(Math.Round(CDbl(v * BitConverter.Int32BitsToSingle(&H3EA2F983)), MidpointRounding.ToEven))
        Dim nf = CSng(n)
        Dim r = ((v - BitConverter.Int32BitsToSingle(&H40490000) * nf) - BitConverter.Int32BitsToSingle(&H3A7DA000) * nf) - BitConverter.Int32BitsToSingle(&H34222000) * nf
        Dim r1 = r - BitConverter.Int32BitsToSingle(&H2CB4611A) * nf
        Dim r2 = r * r
        Dim p = (((BitConverter.Int32BitsToSingle(&H362F0519) * r2 + BitConverter.Int32BitsToSingle(&HB94FBAF1)) * r2 +
                  BitConverter.Int32BitsToSingle(&H3C088773)) * r2 + BitConverter.Int32BitsToSingle(&HBE2AAAA5)) * r2
        Dim res = p * r1 + r1
        Return If((n And 1) <> 0, -res, res)
    End Function
End Module
