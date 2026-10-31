Imports System.Collections.Generic
Imports System.Numerics
Imports NiflySharp.Blocks
Imports NiflySharp.Structs
Imports NiflySharp.Enums

''' <summary>THE LOCAL TRANSFORM A NiTransformController WRITES AT THE PREVIEW'S REST (ola 0c parte 2), transcribed from Fallout4.exe
''' 1.11.240.0 and SkyrimSE.exe (the interpolator side is the same code in both: D:\WinTmp\rc\ola0c\re7\cmpr.py, pairs1-3.txt;
''' resultado5 R2). The time: ControllerRestLaw.ControllerTimeAtRest; the derived key data: KeyFillLaw; the quaternion arithmetic:
''' QuaternionLaw.
''' <para>A controller whose interpolator is a NiPathInterpolator (FO4 7, SSE 28 in the corpus): its rest on the path is postponed
''' (user order 20-oct-2026) - nothing is written, its block keeps the file's position, and `onPath` says so (the frame notice
''' PathControllerAtRest).</para>
''' <para>Holes, declared: a controller whose interpolator is a NiLookAtInterpolator (SSE 5) is NOT TRACED - nothing; a Point3
''' QUADRATIC / TBC channel evaluated past its last key reads that
''' key's B / C coefficients, which the load never writes (KeyFillLaw.Point3Coefficients; the key array comes from the allocator
''' 0x141657DE0 with only the time set, 0x141771FA0): not determinable; the sin / cos of the exe beyond 10000 (QuaternionLaw.SinCos):
''' not determinable; a float key of type 0 returns the caller's stack (0x14174C3A0 = SSE 0x140F6EC50: ret): not determinable. Each
''' gives Nothing with the reason in `hole`.</para></summary>
Friend Module TransformRestLaw

    Private ReadOnly NegMax As Single = BitConverter.Int32BitsToSingle(&HFF7FFFFF)

    ''' <summary>What the controller writes at rest: each component only when the interpolator's output is valid for it (FO4
    ''' 0x1417455BA..5E9 / 0x1417455EE..5FD / 0x14174573D..759, SSE 0x140F6987C..9A, 0x140F698B0..BF, 0x140F699F4..A10): the
    ''' translation, the rotation matrix built from the quaternion (RotationMatrix) and the scale as |s| (andps 0x141745752 /
    ''' 0x140F69A09). Nothing = that component is not written (the NIF's own stays).</summary>
    Friend Structure TransformRest
        Friend Translation As Vector3?
        Friend Rotation As Matrix33?
        Friend Scale As Single?
    End Structure

    ''' <summary>A component the controller treats as valid: not the marker -FLT_MAX ([0x14246849C] FO4 / SSE [0x1417F4248] SSE) and
    ''' ordered (`ucomiss v, marker; je` skips equal and unordered alike).</summary>
    Private Function Valid(v As Single) As Boolean
        Return Not (v = NegMax OrElse Single.IsNaN(v))
    End Function

    ''' <summary>NiTransformController::Update (FO4 0x141745490, SSE 0x140F69760) at the controller's first update: its time from
    ''' ControllerRestLaw.ControllerTimeAtRest (Nothing = no write), the interpolator's output (TransformInterpolatorAt) written per
    ''' valid component. <paramref name="hole"/> = why nothing is determinable (Nothing when the law simply writes nothing).
    ''' <paramref name="onPath"/> = a path controller the game updates at rest (its time has a value) and the preview does not
    ''' evaluate: nothing written, the file's position stays.</summary>
    Friend Function TransformAtRest(c As NiTransformController, interp As NiInterpolator, data As NiTransformData, ByRef hole As String,
                                    Optional ByRef onPath As Boolean = False) As TransformRest?
        hole = Nothing
        onPath = False
        Dim t = ControllerRestLaw.ControllerTimeAtRest(c)
        If Not t.HasValue Then Return Nothing
#If DEBUG Then
        ' Ola 7e tester (ParityGate transform-rest-law (12), ShadowGate --notice-classes c5 / s4): HEAD's law, the path a hole.
        If Fo4GBufferConstants.GateMutantOn("rest-path-dropped") AndAlso TypeOf interp Is NiPathInterpolator Then
            hole = $"{interp.GetType().Name}: NOT TRACED"
            Return Nothing
        End If
#End If
        If TypeOf interp Is NiPathInterpolator Then
            onPath = True
            Return Nothing
        End If
        If TypeOf interp Is NiLookAtInterpolator Then
            hole = $"{interp.GetType().Name}: NOT TRACED"
            Return Nothing
        End If
        Dim ti = TryCast(interp, NiTransformInterpolator)
        If ti Is Nothing Then Return Nothing
        Dim o = TransformInterpolatorAt(ti, data, t.Value, hole)
        If Not o.HasValue Then Return Nothing
        Dim v = o.Value
        Dim r As New TransformRest()
        If Valid(v.T.X) Then r.Translation = v.T
        If Valid(v.R.X) Then r.Rotation = RotationMatrix(v.R)
        If Valid(v.S) Then r.Scale = BitConverter.Int32BitsToSingle(BitConverter.SingleToInt32Bits(v.S) And &H7FFFFFFF)
        Return r
    End Function

    ''' <summary>NiTransformInterpolator::Update (FO4 0x14173F610 = SSE 0x140F63840) at time <paramref name="t"/> from the state the
    ''' load leaves: the pose = the NIF's Transform ([+0x18] translation, [+0x24] quaternion w x y z, [+0x34] scale; LoadBinary FO4
    ''' 0x14173F1F0 = SSE 0x140F63410), lastTime = -FLT_MAX (NiInterpolator ctor FO4 0x14174B455 / SSE 0x140F6CD85), the key caches 0.
    ''' bPose ([+0x46] = 1: no data and not all three components invalid, 0x14173F24E..28D) =&gt; the pose; t = lastTime (or unordered:
    ''' `ucomiss; jne`) =&gt; the pose (0x14173F65C..6AC); otherwise each channel WITH keys is evaluated - translation by the Point3
    ''' GenInterp (0x14174D500 / 0x140F6FDB0), rotation by the rotation GenInterp (0x14174CFB0 / 0x140F6F860), scale by the float
    ''' GenInterp (0x14174C400 / 0x140F6ECB0) - and a channel without keys keeps the pose's. All three invalid (S, R.x, T.x =
    ''' -FLT_MAX) =&gt; no write (0x14173F7EC..809).</summary>
    Friend Function TransformInterpolatorAt(ti As NiTransformInterpolator, data As NiTransformData, t As Single, ByRef hole As String) As (T As Vector3, R As EngineQuat, S As Single)?
        Dim p = ti.Transform
        Dim tr = p.Translation
        Dim rq = EngineQuat.FromFile(p.Rotation)
        Dim sc = p.Scale
        Dim todasInvalidas = Function() Not Valid(sc) AndAlso Not Valid(rq.X) AndAlso Not Valid(tr.X)
        If data Is Nothing AndAlso Not todasInvalidas() Then Return (tr, rq, sc)
        If ControllerRestLaw.SameOrUnordered(t, NegMax) Then
            If todasInvalidas() Then Return Nothing
            Return (tr, rq, sc)
        End If
        If data IsNot Nothing Then
            Dim tk = data.Translations
            If tk.Keys IsNot Nothing AndAlso tk.Keys.Count > 0 Then
                Dim v = ControllerRestLaw.Point3At(tk, t)
                If Not v.HasValue Then
                    hole = $"translation {tk.Interpolation} past its last key or of an unknown type: not determinable"
                    Return Nothing
                End If
                tr = v.Value
            End If
            If data.NumRotationKeys <> 0UI Then
                Dim q = RotationAt(data, t, hole)
                If Not q.HasValue Then Return Nothing
                rq = q.Value
            End If
            Dim sk = data.Scales
            If sk.Keys IsNot Nothing AndAlso sk.Keys.Count > 0 Then
                Dim v = ControllerRestLaw.FloatAt(sk, t)
                If Not v.HasValue Then
                    hole = "scale key of type 0 (0x14174C3A0: the caller's stack)"
                    Return Nothing
                End If
                sc = v.Value
            End If
        End If
        If todasInvalidas() Then Return Nothing
        Return (tr, rq, sc)
    End Function

    ''' <summary>The rotation GenInterp (FO4 0x14174CFB0 = SSE 0x140F6F860) over the keys the load filled (KeyFillLaw.RotationKeysFilled):
    ''' XYZ (type 4) -&gt; XyzRotation at t; one key or t = -FLT_MAX (or unordered) -&gt; key 0; else from lastIdx 0, i advances while
    ''' t &gt; t(i+1), j = min(i+1, n-1), u = 1 when t(j) = t(i) or unordered (`ucomiss; je`), else (t - t_i) / (t_j - t_i); then the
    ''' key function of the type (table FO4 0x14344A0E0 / SSE 0x14331E0A0): 1 Slerp(u, q_i, q_j) (0x14176F060 / 0x140F91A20); 2
    ''' Squad(u, q_i, a_i, a_j, q_j) with the intermediates (0x14176C1F0 / 0x140F8F8C0); 3 Squad(u, q_i, A_i, B_j, q_j) (0x141773390 /
    ''' 0x140F96400); 5 u &gt;= 1 ? q_j : q_i (0x141770F70 / 0x140F93BE0); 0 the identity (1, 0, 0, 0) (0x14174CD10 / 0x140F6F5C0).</summary>
    Private Function RotationAt(d As NiTransformData, t As Single, ByRef hole As String) As EngineQuat?
        Dim typ = CInt(d.RotationType)
        If typ = 4 Then Return XyzRotation(d.XYZRotations, t, hole)
        Dim keys = d.QuaternionKeys
        If keys Is Nothing OrElse keys.Count = 0 Then Return Nothing
        Dim n = keys.Count
        Dim f = KeyFillLaw.RotationKeysFilled(keys, d.RotationType)
        If f.Q Is Nothing Then
            hole = f.Hole
            Return Nothing
        End If
        If n = 1 OrElse ControllerRestLaw.SameOrUnordered(t, NegMax) Then Return f.Q(0)
        Dim i = 0
        While i + 1 < n AndAlso t > keys(i + 1).Time
            i += 1
        End While
        Dim j = Math.Min(i + 1, n - 1)
        Dim ti0 = keys(i).Time, tj0 = keys(j).Time
        Dim u = If(ControllerRestLaw.SameOrUnordered(tj0, ti0), 1.0F, (t - ti0) / (tj0 - ti0))
        Select Case typ
            Case 1 : Return QuaternionLaw.Slerp(u, f.Q(i), f.Q(j))
            Case 2 : Return QuaternionLaw.Squad(u, f.Q(i), f.A(i), f.A(j), f.Q(j))
            Case 3 : Return QuaternionLaw.Squad(u, f.Q(i), f.A(i), f.B(j), f.Q(j))
            Case 5 : Return If(u >= 1.0F, f.Q(j), f.Q(i))
            Case 0 : Return New EngineQuat(1.0F, 0.0F, 0.0F, 0.0F)
        End Select
        hole = $"rotation key type {typ}: not in the exe's table"
        Return Nothing
    End Function

    ''' <summary>The XYZ rotation (FO4 0x14176D330 = SSE 0x140F6E150): each axis's float curve by the float GenInterp (0x14174C400,
    ''' ControllerRestLaw.FloatAt, its cache at 0 from the ctor 0x14176D146), 0 without keys; (s, c) = SinCos(0.5 a) per axis; the
    ''' quaternion qZ qY qX: w = (cz cy) cx + (sz sy) sx, x = (cz cy) sx - (sz sy) cx, y = (cz sy) cx + (sz cy) sx, z = (sz cy) cx -
    ''' (cz sy) sx (0x14176D44E..4D4).</summary>
    Private Function XyzRotation(axes As KeyGroup(Of Single)(), t As Single, ByRef hole As String) As EngineQuat?
        Dim h(2) As Single
        For ax = 0 To 2
            Dim kg = axes(ax)
            Dim a = 0.0F
            If kg.Keys IsNot Nothing AndAlso kg.Keys.Count > 0 Then
                Dim v = ControllerRestLaw.FloatAt(kg, t)
                If Not v.HasValue Then
                    hole = $"XYZ axis {ax}: key of type 0 (0x14174C3A0: the caller's stack)"
                    Return Nothing
                End If
                a = v.Value
            End If
            h(ax) = a * 0.5F
        Next
        Dim x = QuaternionLaw.SinCos(h(0)), y = QuaternionLaw.SinCos(h(1)), z = QuaternionLaw.SinCos(h(2))
        If Not x.HasValue OrElse Not y.HasValue OrElse Not z.HasValue Then
            hole = "XYZ angle beyond 10000: the exe's sin / cos path 0x1422C00D4 is not transcribed"
            Return Nothing
        End If
        Dim sx = x.Value.S, cx = x.Value.C, sy = y.Value.S, cy = y.Value.C, sz = z.Value.S, cz = z.Value.C
        Return New EngineQuat(((cz * cy) * cx) + ((sz * sy) * sx), ((cz * cy) * sx) - ((sz * sy) * cx),
                              ((cz * sy) * cx) + ((sz * cy) * sx), ((sz * cy) * cx) - ((cz * sy) * sx))
    End Function

    ''' <summary>The rotation matrix the controller writes into the node (FO4 0x1417455EE..73A into [+0x30] by rows; SSE 0x140F698B0..9EF
    ''' into [+0x48] in the file's order), in NiflySharp's Matrix33 names (its Sync reads the file as M11 M21 M31 M12 M22 M32 M13 M23
    ''' M33, NiMain.Matrix33.g.cs:114-122; FO4 loads that file order transposed into its rows, 0x1416B92A0, SSE as is, 0x140EDFAE0 -
    ''' both games give the same M): with x2 = x + x, y2 = y + y, z2 = z + z: M11 = 1 - ((z z2) + (y y2)), M12 = (x y2) + (w z2),
    ''' M13 = (x z2) - (w y2), M21 = (x y2) - (w z2), M22 = 1 - ((z z2) + (x x2)), M23 = (y z2) + (w x2), M31 = (x z2) + (w y2),
    ''' M32 = (y z2) - (w x2), M33 = 1 - ((y y2) + (x x2)).</summary>
    Friend Function RotationMatrix(q As EngineQuat) As Matrix33
        Dim x2 = q.X + q.X, y2 = q.Y + q.Y, z2 = q.Z + q.Z
        Return New Matrix33 With {
            .M11 = 1.0F - ((q.Z * z2) + (q.Y * y2)), .M12 = (q.X * y2) + (q.W * z2), .M13 = (q.X * z2) - (q.W * y2),
            .M21 = (q.X * y2) - (q.W * z2), .M22 = 1.0F - ((q.Z * z2) + (q.X * x2)), .M23 = (q.Y * z2) + (q.W * x2),
            .M31 = (q.X * z2) + (q.W * y2), .M32 = (q.Y * z2) - (q.W * x2), .M33 = 1.0F - ((q.Y * y2) + (q.X * x2))}
    End Function
End Module
