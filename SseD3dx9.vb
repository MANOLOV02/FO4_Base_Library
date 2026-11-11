Option Strict On
Option Explicit On

Imports System.Runtime.InteropServices
Imports System.Runtime.Intrinsics
Imports OpenTK.Mathematics

''' <summary>THE d3dx9_42.dll VECTOR / MATRIX FUNCTIONS SKYRIM SE'S CONSTANT LAWS CALL, with the dispatch the DLL itself decides
''' (d3dx9_42.dll x64 in System32, MD5 1af7ae1fde027a30b9097280819a0a86: the game folder has none; sse_constants_oracle.py prints the
''' MD5 it reads). SkyrimSE.exe 1.7.104 imports D3DXVec3Normalize (IAT 0x1417C9310, thunk 0x1415A9C90), D3DXVec3TransformNormal
''' (0x1417C9308 / 0x1415A9C96), D3DXVec3TransformCoord (0x1417C92F8 / 0x1415A9CA2), D3DXMatrixTranspose (0x1417C9330 / 0x1415A9C78)
''' and D3DXMatrixMultiply (0x1417C9328 / 0x1415A9C7E). Each export is a jmp through the DLL's table 0x18024C380: Normalize +0x38
''' (0x18003CEE0), TransformNormal +0x30 (0x18003E0E8), TransformCoord +0x50 (0x18003DBE8), Transpose +0x130 (0x1800410B4), Multiply
''' +0x18 (0x180040BA8). The table's static entries are first-call stubs (e.g. 0x18003DFF0) that call the selector 0x18005F548
''' (0x18003E0B5) and jump through the table again (0x18003E0DB): every call the exe makes runs the entry the selector left.
''' <para>A matrix is a D3DXMATRIX: OpenTK <see cref="Matrix4"/> Row r = the D3DX row r (_r1.._r4), the row-vector convention
''' (v' = v M). Single arithmetic in each variant's order; the SIMD estimates (rcpps / rsqrtss) are evaluated EXACT with the DLL's
''' Newton on top - the repository's one rule for that class (user decisions 06-sep / 14-sep-2026, Havok.Motor.Simd): the bits can
''' differ from the game's processor by the estimate's ulps.</para>
''' Gate: ParityGate sse-constants-law [d3dx] (each variant of each route against the DLL's own code run in unicorn by
''' sse_constants_oracle.py --emulate) and [route].</summary>
Friend Module SseD3dx9

    ''' <summary>The table the selector 0x18005F548 leaves.</summary>
    Friend Enum SseD3dxRoute
        ''' <summary>The generic table: memcpy(0x18024C380, 0x18024C5D0, 0x250) (0x18005F571..58D) and nothing after it.</summary>
        Generic = 0
        ''' <summary>GenuineIntel with SSE: 0x180222838 -> 0x180222858 (0x18005F669..670).</summary>
        GenuineIntel = 1
        ''' <summary>Another vendor with SSE: 0x180222678 (0x18005F677..683), which with cpuid.1 edx bit 25 (0x180222680..696)
        ''' rewrites the slots.</summary>
        OtherVendor = 2
    End Enum

    ''' <summary>A REG_DWORD the selector reads under HKLM\Software\Microsoft\Direct3D (RegOpenKeyA 0x18005F5AD / 0x18005F5F1 with
    ''' HKEY_LOCAL_MACHINE 0xFFFFFFFF80000002 and the key 0x180002E18): 0x18005F438 returns 1 only when RegQueryValueExA returned 0
    ''' and the type is 4 (REG_DWORD, 0x18005F47B..486), and the value is then the 4 bytes read.</summary>
    Friend Structure SseD3dxRegistryValue
        Public Found As Boolean
        Public Value As UInteger
    End Structure

    ''' <summary>The selector 0x18005F548, as a function of what it reads:
    ''' <list type="number">
    ''' <item>v = DisablePSGP (0x180013F70) when found, else 0 (neg / sbb / and, 0x18005F5D1..5ED);</item>
    ''' <item>DisableD3DXPSGP (0x180013F80), when found, replaces v (cmovne, 0x18005F617..622);</item>
    ''' <item>v = 1: the generic table (cmp eax, 1 / je, 0x18005F626..629);</item>
    ''' <item>IsProcessorFeaturePresent(6 = PF_XMMI_INSTRUCTIONS_AVAILABLE) false: generic (0x18005F62B..638);</item>
    ''' <item>the 12 cpuid(0) vendor bytes (ebx, edx, ecx, 0x18005F63A..64C) equal to "GenuineIntel" (0x18000FFA8, 0x18005F650..667):
    ''' the Intel table;</item>
    ''' <item>else 0x180222678: cpuid(1) edx bit 25 (SSE) clear writes no slot (bt edx, 0x19 / jae 0x18022282A) - the generic
    ''' table stays; set, the other vendor's slots.</item>
    ''' </list></summary>
    Friend Function SelectRoute(disablePsgp As SseD3dxRegistryValue, disableD3dxPsgp As SseD3dxRegistryValue, xmmiPresent As Boolean,
                                cpuidVendor As String, cpuid1EdxSse As Boolean) As SseD3dxRoute
        Dim v As UInteger = If(disablePsgp.Found, disablePsgp.Value, 0UI)
        If disableD3dxPsgp.Found Then v = disableD3dxPsgp.Value
        If v = 1UI Then Return SseD3dxRoute.Generic
        If Not xmmiPresent Then Return SseD3dxRoute.Generic
        If String.Equals(cpuidVendor, "GenuineIntel", StringComparison.Ordinal) Then Return SseD3dxRoute.GenuineIntel
        If Not cpuid1EdxSse Then Return SseD3dxRoute.Generic
        Return SseD3dxRoute.OtherVendor
    End Function

    <DllImport("kernel32.dll")>
    Private Function IsProcessorFeaturePresent(feature As UInteger) As <MarshalAs(UnmanagedType.Bool)> Boolean
    End Function

    Private Function ReadDirect3DDword(name As String) As SseD3dxRegistryValue
        Using k = Microsoft.Win32.Registry.LocalMachine.OpenSubKey("Software\Microsoft\Direct3D", False)
            If k Is Nothing Then Return New SseD3dxRegistryValue()
            Dim names = k.GetValueNames()
            If Not names.Contains(name, StringComparer.OrdinalIgnoreCase) Then Return New SseD3dxRegistryValue()
            If k.GetValueKind(name) <> Microsoft.Win32.RegistryValueKind.DWord Then Return New SseD3dxRegistryValue()
            Return New SseD3dxRegistryValue With {.Found = True, .Value = CUInt(CInt(k.GetValue(name)) And &HFFFFFFFFL)}
        End Using
    End Function

    Private _machineRoute As SseD3dxRoute? = Nothing

    ''' <summary>The route the DLL takes on THIS machine: <see cref="SelectRoute"/> fed with what 0x18005F548 reads here - the two
    ''' registry values (the 64-bit view, the DLL is x64), IsProcessorFeaturePresent(6) and the cpuid vendor / SSE bit (X86Base.CpuId:
    ''' only the identification the DLL itself reads, no arithmetic). Read once.</summary>
    Friend Function MachineRoute() As SseD3dxRoute
        If _machineRoute.HasValue Then Return _machineRoute.Value
        Dim vendor As String = ""
        Dim sse As Boolean = False
        If System.Runtime.Intrinsics.X86.X86Base.IsSupported Then
            Dim c0 = System.Runtime.Intrinsics.X86.X86Base.CpuId(0, 0)
            Dim b(11) As Byte
            BitConverter.GetBytes(c0.Ebx).CopyTo(b, 0) : BitConverter.GetBytes(c0.Edx).CopyTo(b, 4) : BitConverter.GetBytes(c0.Ecx).CopyTo(b, 8)
            vendor = System.Text.Encoding.ASCII.GetString(b)
            sse = ((System.Runtime.Intrinsics.X86.X86Base.CpuId(1, 0).Edx >> 25) And 1) <> 0
        End If
        _machineRoute = SelectRoute(ReadDirect3DDword("DisablePSGP"), ReadDirect3DDword("DisableD3DXPSGP"), IsProcessorFeaturePresent(6UI), vendor, sse)
        Return _machineRoute.Value
    End Function

    ' ---------------------------------------------------------------------------------------------------------------------------
    ' The DLL's constants (sse_constants_oracle.py --values reads each one from the image).
    ' ---------------------------------------------------------------------------------------------------------------------------

    ''' <summary>[0x180002B78] = 2^-46: the Intel Normalize's threshold (comiss / jae, 0x180225FFF..6006).</summary>
    Friend ReadOnly IntelNormalizeEpsilon As Single = BitConverter.UInt32BitsToSingle(&H28800000UI)
    ''' <summary>[0x18001CF58] = 1.0: the generic Normalize's / TransformCoord's unit (0x18003CF18, 0x18003DCB4).</summary>
    Friend Const GenericOne As Single = 1.0F
    ''' <summary>[0x18001DD9C] = -2^-23 and [0x18001D4F8] = 2^-23: the generic band around 1 (0x18003CF3E / 0x18003CF47,
    ''' 0x18003DCDD / 0x18003DCE6).</summary>
    Friend ReadOnly GenericUnitBandLow As Single = BitConverter.UInt32BitsToSingle(&HB4000000UI)
    Friend ReadOnly GenericUnitBandHigh As Single = BitConverter.UInt32BitsToSingle(&H34000000UI)
    ''' <summary>[0x18001DD68] = FLT_MIN 2^-126: the generic Normalize's floor (comiss / jbe, 0x18003CF68..6F).</summary>
    Friend ReadOnly GenericNormalizeFloor As Single = BitConverter.UInt32BitsToSingle(&H800000UI)
    ''' <summary>[0x180252D90] = splat(1.0) and [0x180252CD0] = splat(FLT_MAX): written by the DLL's static initializers 0x18022D428..434
    ''' (from [0x18001CF58]) and 0x18022D40C..418 (from [0x18000552C] = 0x7F7FFFFF); the other vendor's Normalize reads them.</summary>
    Friend Const OtherVendorOne As Single = 1.0F
    Friend ReadOnly OtherVendorNormalizeCap As Single = BitConverter.UInt32BitsToSingle(&H7F7FFFFFUI)
    ''' <summary>[0x180002B7C] = 3.0 and [0x180002B80] = 0.5: the Intel Normalize's Newton (Havok.Motor.Simd.RsqrtNewton multiplies
    ''' by the same two).</summary>
    Friend Const IntelNewtonThree As Single = 3.0F
    Friend Const IntelNewtonHalf As Single = 0.5F

    ' ---------------------------------------------------------------------------------------------------------------------------
    ' D3DXVec3Normalize (slot +0x38)
    ' ---------------------------------------------------------------------------------------------------------------------------

    ''' <summary>D3DXVec3Normalize on <paramref name="route"/>:
    ''' <list type="bullet">
    ''' <item>generic 0x18003CEF0: l = (x x + y y) + z z (0x18003CF20..33); l - 1 in [-2^-23, 2^-23] (both comiss, unordered goes on)
    ''' copies v (0x18003CF37..66); else l &gt; FLT_MIN gives f = 1.0 / sqrtf(l) (msvcrt sqrtf, IAT 0x180001598, 0x18003CF74..7E) and
    ''' (x f, f y, f z) (0x18003CF82..9F); else (0, 0, 0) (0x18003CFB7..BE);</item>
    ''' <item>GenuineIntel 0x180225FD1: l = (x x + y y) + z z (0x180225FEB..FFB); not l &gt;= 2^-46 gives 0 (0x180225FFF..6012); else
    ''' r = rsqrtss(l) and f = (3 - (r l) r) (r 0.5) (0x180226014..37), v f (0x18022603B..43) - Havok.Motor.Simd.RsqrtNewton, the
    ''' repository's one owner of that Newton;</item>
    ''' <item>other vendor 0x180221084: s = (x x, y y, z z) (0x180221096..99), lane 0 of (s.yzx + s) + s.zxy = (y y + x x) + z z
    ''' (shufps 0xC9 / 0xD2, 0x18022109C..AA), f = min(1.0 / sqrtps(l), FLT_MAX) (0x1802210AD..BA), v f (0x1802210C1).</item>
    ''' </list></summary>
    Friend Function Vec3Normalize(route As SseD3dxRoute, v As Vector3) As Vector3
        Select Case route
            Case SseD3dxRoute.GenuineIntel
                Dim l As Single = (v.X * v.X + v.Y * v.Y) + v.Z * v.Z
                If Not (l >= IntelNormalizeEpsilon) Then Return Vector3.Zero
                Dim f As Single = Havok.Motor.Simd.RsqrtNewton(Vector128.Create(l)).GetElement(0)
                Return New Vector3(v.X * f, v.Y * f, v.Z * f)
            Case SseD3dxRoute.OtherVendor
                Dim l As Single = (v.Y * v.Y + v.X * v.X) + v.Z * v.Z
                Dim f As Single = OtherVendorOne / MathF.Sqrt(l)
#If DEBUG Then
                ' ParityGate sse-constants-law [d3dx]: the other vendor's cap minps left out.
                If Not Fo4GBufferConstants.GateMutantOn("sse-d3dx-normalize-nocap") Then f = MinPs(f, OtherVendorNormalizeCap)
#Else
                f = MinPs(f, OtherVendorNormalizeCap)
#End If
                Return New Vector3(v.X * f, v.Y * f, v.Z * f)
            Case Else
                Dim l As Single = (v.X * v.X + v.Y * v.Y) + v.Z * v.Z
                Dim d As Single = l - GenericOne
                If d >= GenericUnitBandLow AndAlso GenericUnitBandHigh >= d Then Return v
                If Not (l > GenericNormalizeFloor) Then Return Vector3.Zero
                Dim f As Single = GenericOne / MathF.Sqrt(l)
                Return New Vector3(v.X * f, f * v.Y, f * v.Z)
        End Select
    End Function

    ''' <summary>minps / minss dst, src: dst &lt; src ? dst : src (src when either is NaN).</summary>
    Friend Function MinPs(dst As Single, src As Single) As Single
        Return If(dst < src, dst, src)
    End Function

    ''' <summary>maxps / maxss dst, src: dst &gt; src ? dst : src (src when either is NaN).</summary>
    Friend Function MaxPs(dst As Single, src As Single) As Single
        Return If(dst > src, dst, src)
    End Function

    ' ---------------------------------------------------------------------------------------------------------------------------
    ' D3DXVec3TransformNormal (slot +0x30)
    ' ---------------------------------------------------------------------------------------------------------------------------

    ''' <summary>D3DXVec3TransformNormal: out.c = (v.y M[1][c] + v.x M[0][c]) + v.z M[2][c] on every route - generic 0x18003E0F8
    ''' (x: y M10 + x M00 then + z M20, 0x18003E11B..143; y: x M01 + y M11 then + z M21, 0x18003E136..169; z: x M02 + y M12 then +
    ''' z M22, 0x18003E130..179), GenuineIntel 0x180222C48 (mulps / addps y M1 + x M0 then + z M2, 0x180222C5C..81; the unaligned
    ''' branch 0x180222C91.. the same), other vendor 0x180221678 (0x18022167C..D2): the same products and the same pairs (an IEEE
    ''' sum does not depend on its operands' order), so one body serves the three. <paramref name="route"/> is taken for the
    ''' call's record.</summary>
    Friend Function Vec3TransformNormal(route As SseD3dxRoute, v As Vector3, m As Matrix4) As Vector3
        Return New Vector3((v.Y * m.M21 + v.X * m.M11) + v.Z * m.M31,
                           (v.Y * m.M22 + v.X * m.M12) + v.Z * m.M32,
                           (v.Y * m.M23 + v.X * m.M13) + v.Z * m.M33)
    End Function

    ' ---------------------------------------------------------------------------------------------------------------------------
    ' D3DXVec3TransformCoord (slot +0x50)
    ' ---------------------------------------------------------------------------------------------------------------------------

    ''' <summary>D3DXVec3TransformCoord on <paramref name="route"/> (s = the row vector (v, 1) M):
    ''' <list type="bullet">
    ''' <item>generic 0x18003DBF8: s.c = ((v.y M[1][c] + v.x M[0][c]) + v.z M[2][c]) + M[3][c] (0x18003DBFC..CC2); w - 1 in
    ''' [-2^-23, 2^-23] leaves (s.x, s.y, s.z) (0x18003DCD9..CF1); else r = 1.0 / w (0x18003DCF3) and (r s.x, r s.y, r s.z)
    ''' (0x18003DCF7..D14);</item>
    ''' <item>GenuineIntel 0x180222DB4: s = (v.y M1 + v.x M0) + (v.z M2 + M3) (0x180222DC2..DEF; unaligned 0x180222DF4..E55 the same);
    ''' r = rcpps(w), n = (r + r) - (r w) r (0x180222E58..6A), s n (lane w broadcast, 0x180222E6D..71) - no band test;</item>
    ''' <item>other vendor 0x1802215E8: s = ((v.y M1 + v.x M0) + v.z M2) + M3 (0x1802215EC..653), s / w (divps by the broadcast w,
    ''' 0x180221656..65F).</item>
    ''' </list></summary>
    Friend Function Vec3TransformCoord(route As SseD3dxRoute, v As Vector3, m As Matrix4) As Vector3
        Select Case route
            Case SseD3dxRoute.GenuineIntel
                Dim sx As Single = (v.Y * m.M21 + v.X * m.M11) + (v.Z * m.M31 + m.M41)
                Dim sy As Single = (v.Y * m.M22 + v.X * m.M12) + (v.Z * m.M32 + m.M42)
                Dim sz As Single = (v.Y * m.M23 + v.X * m.M13) + (v.Z * m.M33 + m.M43)
                Dim sw As Single = (v.Y * m.M24 + v.X * m.M14) + (v.Z * m.M34 + m.M44)
                Dim r As Single = 1.0F / sw                                   ' rcpps evaluated exact
                Dim n As Single = (r + r) - (r * sw) * r
                Return New Vector3(sx * n, sy * n, sz * n)
            Case SseD3dxRoute.OtherVendor
                Dim sx As Single = ((v.Y * m.M21 + v.X * m.M11) + v.Z * m.M31) + m.M41
                Dim sy As Single = ((v.Y * m.M22 + v.X * m.M12) + v.Z * m.M32) + m.M42
                Dim sz As Single = ((v.Y * m.M23 + v.X * m.M13) + v.Z * m.M33) + m.M43
                Dim sw As Single = ((v.Y * m.M24 + v.X * m.M14) + v.Z * m.M34) + m.M44
                Return New Vector3(sx / sw, sy / sw, sz / sw)
            Case Else
                Dim sx As Single = ((v.Y * m.M21 + v.X * m.M11) + v.Z * m.M31) + m.M41
                Dim sy As Single = ((v.X * m.M12 + v.Y * m.M22) + v.Z * m.M32) + m.M42
                Dim sz As Single = ((v.X * m.M13 + v.Y * m.M23) + v.Z * m.M33) + m.M43
                Dim sw As Single = ((v.X * m.M14 + v.Y * m.M24) + v.Z * m.M34) + m.M44
                Dim d As Single = sw - GenericOne
#If DEBUG Then
                ' ParityGate sse-constants-law [d3dx]: the generic band test dropped (always divides).
                If Fo4GBufferConstants.GateMutantOn("sse-d3dx-coord-noband") Then d = Single.NaN
#End If
                If d >= GenericUnitBandLow AndAlso GenericUnitBandHigh >= d Then Return New Vector3(sx, sy, sz)
                Dim r As Single = GenericOne / sw
                Return New Vector3(r * sx, r * sy, r * sz)
        End Select
    End Function

    ' ---------------------------------------------------------------------------------------------------------------------------
    ' D3DXMatrixTranspose (slot +0x130) and D3DXMatrixMultiply (slot +0x18)
    ' ---------------------------------------------------------------------------------------------------------------------------

    ''' <summary>D3DXMatrixTranspose: out[r][c] = M[c][r], moves only - generic 0x1800410C4 (the Intel table 0x180222858 does not
    ''' write slot +0x130: the generic one) and other vendor 0x180221D70 (unpack / shufps 0x88 / 0xDD / 0x22 / 0x77 of the rows).</summary>
    Friend Function MatrixTranspose(route As SseD3dxRoute, m As Matrix4) As Matrix4
        Return New Matrix4(m.Column0, m.Column1, m.Column2, m.Column3)
    End Function

    ''' <summary>D3DXMatrixMultiply(out, A, B) = A B (rdx = A, r8 = B), each element in the route's order:
    ''' <list type="bullet">
    ''' <item>generic 0x180040BB8: out[r][c] = ((A[r][1] B[1][c] + A[r][0] B[0][c]) + A[r][2] B[2][c]) + A[r][3] B[3][c]
    ''' (row 0 col 0: 0x180040BD6..C0C; the 16 elements in the same order, rows 0..3 at 0x180040BD6..CBF / CC4..DB2 / DB7.. / ..);</item>
    ''' <item>GenuineIntel 0x180225C11 (aligned 0x180225C30..D31, unaligned 0x180225D46..E5F the same): rows 0 and 3 =
    ''' (a0 B0 + a1 B1) + (a2 B2 + a3 B3) (0x180225C66..78, 0x180225CF6..D2E); rows 1 and 2 = ((a0 B0 + a1 B1) + a2 B2) + a3 B3
    ''' (0x180225C96..CB1, 0x180225CC6..CEA);</item>
    ''' <item>other vendor 0x180221E0C: ((a1 B1 + a0 B0) + a2 B2) + a3 B3 per row (0x180221E65..E8D, x4).</item>
    ''' </list></summary>
    Friend Function MatrixMultiply(route As SseD3dxRoute, a As Matrix4, b As Matrix4) As Matrix4
        Dim ar = {a.Row0, a.Row1, a.Row2, a.Row3}
        Dim br = {b.Row0, b.Row1, b.Row2, b.Row3}
        Dim o(3) As Vector4
        For r = 0 To 3
            Dim p0 = ar(r).X * br(0), p1 = ar(r).Y * br(1), p2 = ar(r).Z * br(2), p3 = ar(r).W * br(3)
            If route = SseD3dxRoute.GenuineIntel AndAlso (r = 0 OrElse r = 3) Then
                o(r) = AddLanes(AddLanes(p0, p1), AddLanes(p2, p3))
            ElseIf route = SseD3dxRoute.GenuineIntel Then
                o(r) = AddLanes(AddLanes(AddLanes(p0, p1), p2), p3)
            Else
                o(r) = AddLanes(AddLanes(AddLanes(p1, p0), p2), p3)
            End If
        Next
        Return New Matrix4(o(0), o(1), o(2), o(3))
    End Function

    ''' <summary>addps lane by lane, Single.</summary>
    Private Function AddLanes(x As Vector4, y As Vector4) As Vector4
        Return New Vector4(x.X + y.X, x.Y + y.Y, x.Z + y.Z, x.W + y.W)
    End Function
End Module
