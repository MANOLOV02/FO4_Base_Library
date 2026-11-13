Option Strict On
Option Explicit On

Imports OpenTK.Mathematics

''' <summary>THE TWO MATRICES OF A SKYRIM SE GEOMETRY'S WORLD TRANSFORM (Ola SSE S2 F2.3, rule 5: one seat for the Lighting (g6), Effect
''' (g5) and Utility (g7) constant laws - moved here from SseLightingConstants): the camera-relative World the SetupGeometry of the three
''' classes builds with the same constructor 0x1415170C0 (Lighting 0x14154B6C3, Effect SG 0x1415572EF / MEMBRANE 0x14155823F, Utility
''' 0x14156763C), and the inverse 0x140FDEEC0 of the model-space branches. The NiTransform is SseLightingConstants.SseNiTransform.</summary>
Friend Module SseWorldMatrix

    ''' <summary>The D3DXMATRIX 0x140FDEEC0 builds with rdx = 0 (every call of the constant laws passes xor edx, edx: 0x141549AAB,
    ''' 0x141567AEC) from the NiTransform at rcx (m[i][j] at 4 (3 i + j), t at +0x24, s at +0x30) into r8: inv = 1.0 / s (1.0 =
    ''' [0x141B5BE28], 0x140FDEEEB..EF3); row i = (inv m[i][0], inv m[i][1], inv m[i][2], 0) (0x140FDEFED..F0BD, the zeros
    ''' 0x140FDEFFB / F006 / F08A); row 3 = (-(inv ((t.y m[1][0] + t.x m[0][0]) + t.z m[2][0])), -(inv ((t.x m[0][1] + t.y m[1][1]) +
    ''' t.z m[2][1])), -(inv ((t.y m[1][2] + t.x m[0][2]) + t.z m[2][2])), 1) (0x140FDEF05..FD4 / FE1..003 / F03C..054; the sign
    ''' mask [0x141B5C730]; 1.0 at 0x140FDF098). The inverse of the world transform: the model-space branches' matrix.</summary>
    Friend Function InverseWorldMatrix(world As SseLightingConstants.SseNiTransform) As Matrix4
        Dim inv As Single = 1.0F / world.Scale
        Dim m = world.Rotation
        Dim t = world.Translation
        Dim r3x As Single = -(((t.Y * m.M21 + t.X * m.M11) + t.Z * m.M31) * inv)
        Dim r3y As Single = -(((t.X * m.M12 + t.Y * m.M22) + t.Z * m.M32) * inv)
        Dim r3z As Single = -(((t.Y * m.M23 + t.X * m.M13) + t.Z * m.M33) * inv)
        Return New Matrix4(inv * m.M11, inv * m.M12, inv * m.M13, 0.0F,
                           inv * m.M21, inv * m.M22, inv * m.M23, 0.0F,
                           inv * m.M31, inv * m.M32, inv * m.M33, 0.0F,
                           r3x, r3y, r3z, 1.0F)
    End Function

    ''' <summary>The camera-relative D3DXMATRIX 0x1415170C0 builds from the NiTransform at rdx into rcx: row r = (s m[0][r], s m[1][r],
    ''' s m[2][r], 0) (s = +0x30; mulss of s by each element, 0x1415170C0..13B; the zeros 0x1415170EA / 114 / 138), row 3 = (t - eye, 1)
    ''' (subss of the frame eye [0x1420CFF5C..64], 0x141517140..178). <paramref name="eye"/> = that eye (cb12 row 40's source; the
    ''' previous-frame callers swap [0x1420CFF68..70] into it first, 0x14154980A..858).</summary>
    Friend Function WorldRelativeMatrix(t As SseLightingConstants.SseNiTransform, eye As Vector3) As Matrix4
        Dim s As Single = t.Scale
        Dim m = t.Rotation
        Dim tx As Single = t.Translation.X - eye.X, ty As Single = t.Translation.Y - eye.Y, tz As Single = t.Translation.Z - eye.Z
#If DEBUG Then
        ' ParityGate sse-constants-law [geometry]: the eye not subtracted.
        If Fo4GBufferConstants.GateMutantOn("sse-world-noeye") Then tx = t.Translation.X : ty = t.Translation.Y : tz = t.Translation.Z
#End If
        Return New Matrix4(s * m.M11, s * m.M21, s * m.M31, 0.0F,
                           s * m.M12, s * m.M22, s * m.M32, 0.0F,
                           s * m.M13, s * m.M23, s * m.M33, 0.0F,
                           tx, ty, tz, 1.0F)
    End Function
End Module
