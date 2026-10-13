Imports System.Collections.Generic
Imports System.Numerics
Imports System.Runtime.CompilerServices
Imports NiflySharp.Enums
Imports NiflySharp.Structs

''' <summary>THE ONE SEAT of the key data the engine derives at load (FillDerivedVals: table FO4 0x14344AAA0 = SSE 0x14331EA60,
''' content x 6 + type; ola 0c parte 2, resultado5 R2): float TBC tangents, Point3 QUADRATIC / TBC polynomial coefficients, the
''' rotation keys aligned and clamped with their intermediates or TBC A / B. Every evaluator (ControllerRestLaw.FloatAt / Point3At,
''' TransformRestLaw) reads them from here. Memoized per key list: a ConditionalWeakTable keyed by the NIF's own List of keys (it
''' lives as long as the NIF does), so a NIF's keys are derived once however many frames or consumers read them. The fills that
''' depend on the key type (Point3Coefficients, RotationKeysFilled) keep the type they were built for in the entry (Point3Fill.Kind,
''' RotationFill.Kind) and are rebuilt when asked with another; FloatTangents is the TBC fill only (its one caller, FloatAt's TBC
''' case), so it takes no type. The memo holds
''' because no code of the app writes animation keys (census of the workspace, 12-oct-2026: no writer of KeyGroup.Keys,
''' QuaternionKeys, XYZRotations, Translations or Scales outside NiflySharp's reader).</summary>
Friend Module KeyFillLaw

    Private NotInheritable Class FloatFill
        Friend Tangents As (DS As Single, DD As Single)()
    End Class

    Private NotInheritable Class Point3Fill
        Friend Kind As KeyType
        Friend Coefficients As (B As Vector3, C As Vector3, D As Vector3)()
    End Class

    ''' <summary>The rotation keys as the load leaves them; Q Nothing when not determinable (Hole says why).</summary>
    Friend NotInheritable Class RotationFill
        Friend Kind As KeyType
        Friend Q As EngineQuat()
        Friend A As EngineQuat()
        Friend B As EngineQuat()
        Friend Hole As String
    End Class

    Private ReadOnly FloatMemo As New ConditionalWeakTable(Of List(Of Key(Of Single)), FloatFill)
    Private ReadOnly Point3Memo As New ConditionalWeakTable(Of List(Of Key(Of Vector3)), Point3Fill)
    Private ReadOnly RotationMemo As New ConditionalWeakTable(Of List(Of QuatKey(Of Quaternion)), RotationFill)

    ''' <summary>The float TBC tangents (FO4 0x141771810 = SSE 0x140F94600, per key 0x141771920 = 0x140F94710): fewer than two keys -
    ''' none; key 0 with prev = (v0 + v0) - v1, next = v1, Lp = Ln = 1; keys 1..n-2 with their neighbours, Lp = t_i - t_(i-1),
    ''' Ln = t_(i+1) - t_i; the last with prev = v(n-2), next = (v + v) - v(n-2), Lp = Ln = 1 (0x14177181D..90B). (DS, DD) by TbcKey.</summary>
    Friend Function FloatTangents(k As List(Of Key(Of Single))) As (DS As Single, DD As Single)()
        Return FloatMemo.GetValue(k, Function(x) New FloatFill With {.Tangents = BuildFloatTangents(x)}).Tangents
    End Function

    Private Function BuildFloatTangents(k As List(Of Key(Of Single))) As (DS As Single, DD As Single)()
        Dim n = k.Count
        Dim r(Math.Max(n - 1, 0)) As (DS As Single, DD As Single)
        If n < 2 Then Return r
        r(0) = TbcKey(k(0).Value, (k(0).Value + k(0).Value) - k(1).Value, k(1).Value, 1.0F, 1.0F, k(0).TBC)
        For i = 1 To n - 2
            r(i) = TbcKey(k(i).Value, k(i - 1).Value, k(i + 1).Value, k(i).Time - k(i - 1).Time, k(i + 1).Time - k(i).Time, k(i).TBC)
        Next
        r(n - 1) = TbcKey(k(n - 1).Value, k(n - 2).Value, (k(n - 1).Value + k(n - 1).Value) - k(n - 2).Value, 1.0F, 1.0F, k(n - 1).TBC)
        Return r
    End Function

    ''' <summary>One key's Kochanek-Bartels tangents (FO4 0x141771920 = SSE 0x140F94710; the Point3 one, 0x141772930 = 0x140F958A0, is
    ''' the same per component). The file's TBC floats in order (loaders 0x141771370 / 0x140F94080 and 0x141771DD0 / 0x140F94C60 read
    ''' them sequentially): T at +8, the second (NiflySharp .B; its Sync reads T, B, C, NiMain.TBC.g.cs:84-86) at +0xC, used as
    ''' continuity, the third (.C) at +0x10, used as bias. d0 = v - prev, d1 = next - v, h = (1 - T) 0.5, a = (1 - 2nd) h, b = (2nd + 1) h,
    ''' w = 1 / (Lp + Ln) doubled; DS = ((b (1 - 3rd)) d1 + (a (3rd + 1)) d0) (w Lp), DD = ((a (1 - 3rd)) d1 + (b (3rd + 1)) d0) (w Ln)
    ''' (0x141771927..A23).</summary>
    Private Function TbcKey(v As Single, prev As Single, nxt As Single, lp As Single, ln As Single, tbc As TBC) As (DS As Single, DD As Single)
        Dim d0 = v - prev
        Dim d1 = nxt - v
        Dim h = (1.0F - tbc.T) * 0.5F
        Dim a = (1.0F - tbc.B) * h
        Dim b = (tbc.B + 1.0F) * h
        Dim w = 1.0F / (lp + ln)
        w = w + w
        Dim ds = ((b * (1.0F - tbc.C)) * d1) + ((a * (tbc.C + 1.0F)) * d0)
        Dim dd = ((a * (1.0F - tbc.C)) * d1) + ((b * (tbc.C + 1.0F)) * d0)
        Return (ds * (w * lp), dd * (w * ln))
    End Function

    ''' <summary>The Point3 QUADRATIC / TBC coefficients the load fills for keys 0..n-2 (FO4 0x14176BAA0 = SSE 0x140F8F130; the TBC loop
    ''' 0x14177282D..91C of 0x1417726C0 = 0x140F95630): D = the outgoing tangent of key i - QUADRATIC its second file tangent (Backward,
    ''' +0x1C; the loader 0x14176AFD0 / 0x140F8E630 reads Forward then Backward), TBC its DD; with the incoming one of key i+1 (Forward /
    ''' DS) and delta = P(i+1) - P(i), per component B = (delta 3) - ((D + D) + in), C = (D + in) - (delta + delta). The TBC tangents per
    ''' component as FloatTangents (0x1417726E1..810). The last key gets none (the loop stops at n - 2).</summary>
    Friend Function Point3Coefficients(k As List(Of Key(Of Vector3)), kt As KeyType) As (B As Vector3, C As Vector3, D As Vector3)()
        Dim f As Point3Fill = Nothing
        If Point3Memo.TryGetValue(k, f) AndAlso f.Kind = kt Then Return f.Coefficients
        f = New Point3Fill With {.Kind = kt, .Coefficients = BuildPoint3Coefficients(k, kt)}
        Point3Memo.AddOrUpdate(k, f)
        Return f.Coefficients
    End Function

    Private Function BuildPoint3Coefficients(k As List(Of Key(Of Vector3)), kt As KeyType) As (B As Vector3, C As Vector3, D As Vector3)()
        Dim n = k.Count
        Dim outT(Math.Max(n - 1, 0)) As Vector3, inT(Math.Max(n - 1, 0)) As Vector3
        If kt = KeyType.QUADRATIC_KEY Then
            For i = 0 To n - 1 : outT(i) = k(i).Backward : inT(i) = k(i).Forward : Next
        Else
            Dim comp = Function(c As Integer) BuildFloatTangents(k.Select(Function(x) New Key(Of Single) With {
                .Time = x.Time, .Value = If(c = 0, x.Value.X, If(c = 1, x.Value.Y, x.Value.Z)), .TBC = x.TBC}).ToList())
            Dim tx = comp(0), ty = comp(1), tz = comp(2)
            For i = 0 To n - 1
                outT(i) = New Vector3(tx(i).DD, ty(i).DD, tz(i).DD)
                inT(i) = New Vector3(tx(i).DS, ty(i).DS, tz(i).DS)
            Next
        End If
        Dim r(Math.Max(n - 2, 0)) As (B As Vector3, C As Vector3, D As Vector3)
        For i = 0 To n - 2
            Dim p = k(i).Value, pn = k(i + 1).Value, o = outT(i), ii = inT(i + 1)
            Dim dx = pn.X - p.X, dy = pn.Y - p.Y, dz = pn.Z - p.Z
            r(i) = (New Vector3((dx * 3.0F) - ((o.X + o.X) + ii.X), (dy * 3.0F) - ((o.Y + o.Y) + ii.Y), (dz * 3.0F) - ((o.Z + o.Z) + ii.Z)),
                    New Vector3((o.X + ii.X) - (dx + dx), (o.Y + ii.Y) - (dy + dy), (o.Z + ii.Z) - (dz + dz)),
                    o)
        Next
        Return r
    End Function

    ''' <summary>The rotation keys as FillDerivedVals leaves them (FO4 0x14174CDC0 base, 0x14176C2A0 quadratic, 0x141773450 TBC = SSE
    ''' 0x140F6F670, 0x140F8F970, 0x140F964C0). QUADRATIC / TBC with fewer than two keys: untouched (0x14176C2A3 / 0x141773453).
    ''' Otherwise the base: for i = 0..n-2, dot = ((n.x c.x + n.w c.w) + n.y c.y) + n.z c.z and q(i+1) negated (all four, 0x1416CD0E0)
    ''' when not dot &gt;= 0 (0x14174CE13..5E); then every w clamped to [-1, 1], a w below -1 or unordered to -1 (0x14174CEB0..F8E).
    ''' QUADRATIC: the intermediates a_i = Intermediate(q(i-1), q(i), q(i+1)) with q(-1) = q(0) and q(n) = q(n-1) (0x14176C2DE..374).
    ''' TBC: A_i / B_i = TbcRotation(prev, i, next) with the same ends (0x141773487, 0x141773530..809, 0x141773895).</summary>
    Friend Function RotationKeysFilled(keys As List(Of QuatKey(Of Quaternion)), kt As KeyType) As RotationFill
        Dim f As RotationFill = Nothing
        If RotationMemo.TryGetValue(keys, f) AndAlso f.Kind = kt Then Return f
        f = BuildRotation(keys, kt)
        RotationMemo.AddOrUpdate(keys, f)
        Return f
    End Function

    Private Function BuildRotation(keys As List(Of QuatKey(Of Quaternion)), kt As KeyType) As RotationFill
        Dim typ = CInt(kt)
        Dim n = keys.Count
        Dim q(Math.Max(n - 1, 0)) As EngineQuat
        For i = 0 To n - 1 : q(i) = EngineQuat.FromFile(keys(i).Value) : Next
        Dim r As New RotationFill With {.Kind = kt, .Q = q}
        If (typ = 2 OrElse typ = 3) AndAlso n < 2 Then Return r
        For i = 0 To n - 2
            Dim c = q(i), nx = q(i + 1)
            Dim dot = ((nx.X * c.X + nx.W * c.W) + nx.Y * c.Y) + nx.Z * c.Z
            If Not (dot >= 0.0F) Then q(i + 1) = New EngineQuat(-nx.W, -nx.X, -nx.Y, -nx.Z)
        Next
        For i = 0 To n - 1
            Dim w = q(i).W
            If Not (w >= -1.0F) Then
                q(i).W = -1.0F
            ElseIf w > 1.0F Then
                q(i).W = 1.0F
            End If
        Next
        If typ = 2 Then
            Dim a(n - 1) As EngineQuat
            For i = 0 To n - 1
                Dim x = Intermediate(q(Math.Max(i - 1, 0)), q(i), q(Math.Min(i + 1, n - 1)))
                If Not x.HasValue Then Return New RotationFill With {.Kind = kt, .Hole = "rotation intermediate: an Exp angle beyond 10000 (0x1422C00D4 not transcribed)"}
                a(i) = x.Value
            Next
            r.A = a
        ElseIf typ = 3 Then
            Dim a(n - 1) As EngineQuat, b(n - 1) As EngineQuat
            For i = 0 To n - 1
                Dim ip = Math.Max(i - 1, 0), inx = Math.Min(i + 1, n - 1)
                Dim x = TbcRotation(keys(ip).Time, q(ip), keys(i).Time, q(i), keys(i).TBC, keys(inx).Time, q(inx))
                If Not x.HasValue Then Return New RotationFill With {.Kind = kt, .Hole = "rotation TBC: an Exp angle beyond 10000 (0x1422C00D4 not transcribed)"}
                a(i) = x.Value.A : b(i) = x.Value.B
            Next
            r.A = a : r.B = b
        End If
        Return r
    End Function

    ''' <summary>A TBC rotation key's A and B (FO4 0x1417738C0 and the loop 0x141773530..809 = SSE 0x140F96930 / its loop):
    ''' Lp = Log(conj(q_prev) q), Ln = Log(conj(q) q_next), r = 1 / (t_next - t_prev), m = 1 - T; with the file's TBC floats in order
    ''' (T, then the second - NiflySharp .B, read at +0x18 and used as continuity - then the third - NiflySharp .C, at +0x1C, the
    ''' bias; loaders 0x141772EF0 / 0x140F95F10 read them sequentially): fp = ((t - t_prev) r) m, A = q Exp(0.5 ((Lp a1 + Ln a2) - Ln))
    ''' with a1 = (fp (1 + 2nd)) (1 + 3rd), a2 = (fp (1 - 2nd)) (1 - 3rd); fn = ((t_next - t) r) m, B = q Exp(0.5 (Lp - (Lp b1 + Ln b2)))
    ''' with b1 = (fn (1 - 2nd)) (1 + 3rd), b2 = (fn (1 + 2nd)) (1 - 3rd) (0x1416CD000 set, 0x1416CD0A0 add, 0x1416CD0C0 subtract).</summary>
    Private Function TbcRotation(tp As Single, qp As EngineQuat, tc As Single, qc As EngineQuat, tbc As TBC, tn As Single, qn As EngineQuat) As (A As EngineQuat, B As EngineQuat)?
        Dim lp = QLog(QMul(QConj(qp), qc))
        Dim ln = QLog(QMul(QConj(qc), qn))
        Dim r = 1.0F / (tn - tp)
        Dim m = 1.0F - tbc.T
        Dim fp = ((tc - tp) * r) * m
        Dim a1 = (fp * (tbc.B + 1.0F)) * (tbc.C + 1.0F)
        Dim a2 = (fp * (1.0F - tbc.B)) * (1.0F - tbc.C)
        Dim sa = New EngineQuat(lp.W * a1 + ln.W * a2, lp.X * a1 + ln.X * a2, lp.Y * a1 + ln.Y * a2, lp.Z * a1 + ln.Z * a2)
        Dim ea = QExp(New EngineQuat((sa.W - ln.W) * 0.5F, (sa.X - ln.X) * 0.5F, (sa.Y - ln.Y) * 0.5F, (sa.Z - ln.Z) * 0.5F))
        If Not ea.HasValue Then Return Nothing
        Dim fn = ((tn - tc) * r) * m
        Dim b1 = (fn * (1.0F - tbc.B)) * (tbc.C + 1.0F)
        Dim b2 = (fn * (tbc.B + 1.0F)) * (1.0F - tbc.C)
        Dim sb = New EngineQuat(lp.W * b1 + ln.W * b2, lp.X * b1 + ln.X * b2, lp.Y * b1 + ln.Y * b2, lp.Z * b1 + ln.Z * b2)
        Dim eb = QExp(New EngineQuat((lp.W - sb.W) * 0.5F, (lp.X - sb.X) * 0.5F, (lp.Y - sb.Y) * 0.5F, (lp.Z - sb.Z) * 0.5F))
        If Not eb.HasValue Then Return Nothing
        Return (QMul(qc, ea.Value), QMul(qc, eb.Value))
    End Function

    ''' <summary>The squad intermediate (FO4 0x1416CD400 = SSE 0x140EF8E30): c* = conj(c), q = c Exp(((Log(c* n) + Log(c* p)) x -0.25)).</summary>
    Private Function Intermediate(p As EngineQuat, c As EngineQuat, n As EngineQuat) As EngineQuat?
        Dim inv = QConj(c)
        Dim l1 = QLog(QMul(inv, n))
        Dim l0 = QLog(QMul(inv, p))
        Dim k = BitConverter.Int32BitsToSingle(&HBE800000)
        Dim e = QExp(New EngineQuat((l1.W + l0.W) * k, (l1.X + l0.X) * k, (l1.Y + l0.Y) * k, (l1.Z + l0.Z) * k))
        If Not e.HasValue Then Return Nothing
        Return QMul(c, e.Value)
    End Function
End Module
