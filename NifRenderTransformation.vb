' Version Uploaded of Fo4Library 3.2.0

Imports NiflySharp
Imports NiflySharp.Blocks
Imports NiflySharp.Structs
Imports OpenTK.Mathematics
Public Class Transform_Class

    ' ───────────────────────────────────────────────────────────────────────────────────────────
    ' CONVENCIÓN DE NON-UNIFORM SCALE (column-multiply, ÚNICA) — fijada 2026-06-24.
    '
    ' La 3×3 final es L = R · diag(ScaleVector): column-multiply, L[i,j] = Rotation[i,j]·sv[j].
    ' Semántica: "rotate first, then scale per-axis en el frame del PARENT (post-rotación)".
    ' Para uniform scale (ScaleVector=(s,s,s)) es indistinguible de cualquier otra convención.
    ' Validada APROXIMADAMENTE para face Phase 1 (RMS=0.0066); es la hipótesis de trabajo, NO
    ' confirmada al byte vs el engine FO4.
    '
    ' Es la ÚNICA convención y debe ser COHERENTE de punta a punta: la descomposición (ctors
    ' New(Matrix4/Matrix4d), ComposeTransforms, Inverse) normaliza SIEMPRE por columna, así que
    ' ToMatrix4/ToMatrix4d recomponen por columna y el roundtrip cierra (exacto en reales, ~1 ULP
    ' en float).
    '
    ' Hubo un toggle ScaleThenRotate_LocalFrame (row-multiply, diag(s)·R) para A/B-testear cuál usa
    ' el engine. Se ELIMINÓ (2026-06-24): nunca tuvo call-site fuera de esta clase, el único A/B lo
    ' desfavoreció visualmente, y era incompatible con la descomposición column (su roundtrip NO
    ' cerraba para non-uniform). Si algún día hay evidencia de que el engine usa row-multiply para
    ' non-uniform, hay que re-introducir la convención Y alinear la descomposición de los ctors/
    ' ComposeTransforms/Inverse a la MISMA convención (no alcanza con cambiar solo ToMatrix4d).
    '
    ' ⚠ CUIDADO — invariante de shear (NO forzado por código, respetarlo al programar):
    '   ComposeTransforms con scale non-uniform puede dejar una Rotation con columnas NO mutuamente
    '   ortogonales (shear). El RENDER es correcto (todo va por ToMatrix4d, roundtrip exacto). PERO
    '   los extractores Matrix33ToBSRotation/Matrix33ToEulerXYZ NO pueden representar shear: polar-
    '   descomponen a la rotación más cercana y PIERDEN el stretch. Por eso el shear SOLO debe vivir
    '   en capas estructurales (MorphDeltaTransform/MountDeltaTransform) y NUNCA serializarse a una
    '   pose (PoseTransformData tampoco tiene campos de shear). Aplica al implementar compensaciones
    '   tipo MorphDelta_C (anti-propagación NNAM en los huesos _Offset): asignarla DIRECTO a
    '   MorphDeltaTransform como Transform_Class (vía ComposeTransforms/Inverse), jamás vía pose.
    ' ───────────────────────────────────────────────────────────────────────────────────────────

    Public Shared Function GetGlobalTransform(node As NiNode, Current_nif As Nifcontent_Class_Manolo) As Transform_Class
        Dim current As NiNode = node
        Dim GlobalTransform As Transform_Class = Nothing
        While current IsNot Nothing
            Dim LastParent = New Transform_Class(current)
            If Not IsNothing(GlobalTransform) Then
                GlobalTransform = LastParent.ComposeTransforms(GlobalTransform)
            Else
                GlobalTransform = LastParent
            End If
            current = TryCast(Current_nif.GetParentNode(current), NiNode)
        End While
        Return GlobalTransform
    End Function


    Public Property Rotation As Matrix33 = New Matrix33 With {.M11 = 1, .M22 = 1, .M33 = 1}
    Public Property Translation As Numerics.Vector3 = New Numerics.Vector3(0, 0, 0)
    Public Property Scale As Single = 1
    ''' <summary>
    ''' Per-axis scale (X, Y, Z). Default (1, 1, 1) = uniform-via-legacy-Scale.
    ''' Convención de bridge con Scale escalar (acordado 2026-04-29):
    '''   - Consumidor legacy escribe Scale = s   → ScaleVector queda (1, 1, 1) (uniform);
    '''     scale efectivo es Scale · ScaleVector componentwise = (s, s, s).
    '''   - Consumidor nuevo escribe ScaleVector = (sx, sy, sz) y deja Scale = 1
    '''     → scale efectivo = (sx, sy, sz).
    '''   - Lectura como escalar (legacy): proyectar a avg(scale_eff.X, .Y, .Z).
    '''     Para uniform es exact; para non-uniform es la mejor proyección scalar.
    ''' Toda operación interna (ComposeTransforms, ToMatrix4d, Inverse) opera sobre el
    ''' scale efectivo combinado = Scale · ScaleVector. Para input legacy uniform,
    ''' ScaleVector=(1,1,1) → comportamiento idéntico a antes (no-op).
    ''' </summary>
    Public Property ScaleVector As Numerics.Vector3 = New Numerics.Vector3(1, 1, 1)

    ''' <summary>Scale efectivo combinado: Scale (legacy uniform) · ScaleVector (per-axis).</summary>
    Public ReadOnly Property EffectiveScale As Numerics.Vector3
        Get
            Return New Numerics.Vector3(Scale * ScaleVector.X, Scale * ScaleVector.Y, Scale * ScaleVector.Z)
        End Get
    End Property

    ''' <summary>True si el scale efectivo es uniforme (X = Y = Z dentro de tolerancia).</summary>
    ''' <remarks>Para los call sites legacy que asumen Scale escalar, este check distingue
    ''' el camino no-op (uniform, sigue funcionando) vs el camino non-uniform (necesita math nuevo).</remarks>
    Public ReadOnly Property IsUniformScale As Boolean
        Get
            Const eps As Single = 0.000001F
            Dim s = EffectiveScale
            Return Math.Abs(s.X - s.Y) < eps AndAlso Math.Abs(s.Y - s.Z) < eps
        End Get
    End Property
    Public Overrides Function ToString() As String
        Return "Translation: " + Translation.ToString + vbCrLf + "Rotation:" + PrintMatrix33(Rotation) + vbCrLf + "Scale:" + Scale.ToString + vbCrLf + "ScaleVector:" + ScaleVector.ToString
    End Function

    Public Function ToStringRotationDegrees(Decimals As Integer) As String
        Dim degs = Matrix33ToEulerXYZ(Rotation)
        Return "X:" + Math.Round(degs.X, Decimals).ToString + "º Y:" + Math.Round(degs.Y, Decimals).ToString + "º Z:" + Math.Round(degs.Z, Decimals).ToString + "º"
    End Function
    Public Function ToStringRotationBS(Decimals As Integer) As String
        Dim degs = Matrix33ToBSRotation(Rotation)
        Return "X:" + Math.Round(degs.X, Decimals).ToString + " Y:" + Math.Round(degs.Y, Decimals).ToString + " Z:" + Math.Round(degs.Z, Decimals).ToString
    End Function
    Public Function ToStringTranslation(Decimals As Integer) As String
        Return "X:" + Math.Round(Translation.X, Decimals).ToString + " Y:" + Math.Round(Translation.Y, Decimals).ToString + " Z:" + Math.Round(Translation.Z, Decimals).ToString
    End Function
    Public Function ToStringScale(Decimals As Integer) As String
        Return Math.Round(Scale, Decimals).ToString
    End Function

    Sub New()

    End Sub

    Public Sub New(Origen As INiShape)
        Rotation = Origen.Rotation
        Translation = Origen.Translation
        Scale = Origen.Scale
    End Sub

    Public Shared Function GetGlobalTransform(shape As INiShape, Current_nif As Nifcontent_Class_Manolo) As Transform_Class
        If shape Is Nothing Then Return New Transform_Class()

        Dim globalTransform As New Transform_Class(shape)
        Dim current As NiNode = TryCast(Current_nif.GetParentNode(shape), NiNode)

        While current IsNot Nothing
            globalTransform = New Transform_Class(current).ComposeTransforms(globalTransform)
            current = TryCast(Current_nif.GetParentNode(current), NiNode)
        End While

        Return globalTransform
    End Function


    Public Sub New(Origen As PoseTransformData, Tipo As Poses_class.Pose_Source_Enum)
        Select Case Tipo
            Case Poses_class.Pose_Source_Enum.BodySlide, Poses_class.Pose_Source_Enum.WardrobeManager, Poses_class.Pose_Source_Enum.None
                Rotation = BSRotationToMatrix33(New Numerics.Vector3(Origen.Yaw, Origen.Pitch, Origen.Roll))
                Translation = New Numerics.Vector3(Origen.X, Origen.Y, Origen.Z)
                Scale = Origen.Scale
            Case Poses_class.Pose_Source_Enum.ScreenArcher
                Rotation = EulerXYZToMatrix33(Origen.Yaw, Origen.Pitch, Origen.Roll)
                Translation = New Numerics.Vector3(Origen.X, Origen.Y, Origen.Z)
                Scale = Origen.Scale
            Case Else
                Debugger.Break()
                Throw New Exception
        End Select
        ' Non-uniform scale → ScaleVector como property explícita (no se baked-in en Rotation).
        ' Para input uniform (ScaleX=ScaleY=ScaleZ=1, default): ScaleVector queda (1,1,1) → no-op
        ' en todas las operaciones (ToMatrix4d/ComposeTransforms/Inverse multiplican por identidad).
        ' Para non-uniform: ScaleVector tiene los valores per-axis y Rotation queda PURA (orthonormal).
        ' Refactor 2026-04-29: eliminado el column-multiply hack que rompía orthonormalidad de Rotation
        ' cuando había non-uniform scale, lo cual a su vez rompía Inverse() (Transpose válido solo
        ' para R orthonormal). Ahora la responsabilidad de combinar Rotation+ScaleVector vive en
        ' los métodos que producen la matriz final (ToMatrix4d) o la inversa.
        ScaleVector = New Numerics.Vector3(Origen.ScaleX, Origen.ScaleY, Origen.ScaleZ)
    End Sub
    Public Sub New(m As Matrix4d)
        ' Refactor 2026-06-24: descomposición por COLUMNAS, consistente con ToMatrix4d y ComposeTransforms
        ' (ambos recomponen por column-multiply: M[i,j] = R[i,j]·ScaleVector[j]). En aritmética REAL el
        ' roundtrip New(m).ToMatrix4d() reproduce m para CUALQUIER matriz lineal — incluido shear (columnas
        ' no mutuamente ortogonales) — porque (m[i,j]/colLen_j)·colLen_j = m[i,j]. En float difiere ~1 ULP
        ' (medido ~10% de elementos off-by-1-ULP; impacto práctico nulo).
        '
        ' Reemplaza la normalización por filas previa, que NO roundtripeaba con el column-multiply para
        ' escala no-uniforme (las variables se llamaban col* pero tomaban filas). Era un bug latente que
        ' contaminaba el resultado de Inverse() (numerical path) en cuanto el scale no era uniforme.
        ' Para escala UNIFORME el resultado es igual al legacy SALVO redondeo (~1 ULP): norma de fila y de
        ' columna coinciden en reales pero se computan de elementos distintos. NO afecta el path byte-exact
        ' de FaceGen (ese va por ComposeTransforms/ToMatrix4d, no por este ctor).
        Translation = New Numerics.Vector3(m.M41, m.M42, m.M43)

        ' Columnas (convención row-major OpenTK): col j = (M1j, M2j, M3j).
        Dim col0 As New Vector3d(m.M11, m.M21, m.M31)
        Dim col1 As New Vector3d(m.M12, m.M22, m.M32)
        Dim col2 As New Vector3d(m.M13, m.M23, m.M33)
        Dim sx = col0.Length
        Dim sy = col1.Length
        Dim sz = col2.Length
        If sx = 0 Then sx = 1
        If sy = 0 Then sy = 1
        If sz = 0 Then sz = 1

        Const epsUniform As Double = 0.000001
        Dim isUniform As Boolean = Math.Abs(sx - sy) < epsUniform AndAlso Math.Abs(sy - sz) < epsUniform
        If isUniform Then
            Scale = CSng(sx)
            ScaleVector = New Numerics.Vector3(1, 1, 1)
        Else
            Scale = 1.0F
            ScaleVector = New Numerics.Vector3(CSng(sx), CSng(sy), CSng(sz))
        End If

        ' Cada COLUMNA j dividida por su norma colLen_j → ScaleVector(j)=colLen_j la recompone vía
        ' column-multiply. Rotation queda con columnas unitarias (orthonormal si no hay shear).
        Rotation = New Matrix33 With {
            .M11 = CSng(m.M11 / sx), .M12 = CSng(m.M12 / sy), .M13 = CSng(m.M13 / sz),
            .M21 = CSng(m.M21 / sx), .M22 = CSng(m.M22 / sy), .M23 = CSng(m.M23 / sz),
            .M31 = CSng(m.M31 / sx), .M32 = CSng(m.M32 / sy), .M33 = CSng(m.M33 / sz)
        }
    End Sub
    Public Sub New(m As Matrix4)
        ' Ver comentario en Sub New(Matrix4d) — misma lógica column-based con singles. Roundtrip exacto en
        ' reales con ToMatrix4d (column-multiply); igual al legacy salvo ~1 ULP de redondeo para uniforme.
        Translation = New Numerics.Vector3(m.M41, m.M42, m.M43)

        ' Columnas (convención row-major OpenTK): col j = (M1j, M2j, M3j).
        Dim col0 As New Vector3(m.M11, m.M21, m.M31)
        Dim col1 As New Vector3(m.M12, m.M22, m.M32)
        Dim col2 As New Vector3(m.M13, m.M23, m.M33)
        Dim sx = col0.Length
        Dim sy = col1.Length
        Dim sz = col2.Length
        If sx = 0 Then sx = 1
        If sy = 0 Then sy = 1
        If sz = 0 Then sz = 1

        Const epsUniform As Single = 0.000001F
        Dim isUniform As Boolean = Math.Abs(sx - sy) < epsUniform AndAlso Math.Abs(sy - sz) < epsUniform
        If isUniform Then
            Scale = sx
            ScaleVector = New Numerics.Vector3(1, 1, 1)
        Else
            Scale = 1.0F
            ScaleVector = New Numerics.Vector3(sx, sy, sz)
        End If

        ' Cada COLUMNA j dividida por su norma (ver New(Matrix4d)).
        Rotation = New Matrix33 With {
            .M11 = m.M11 / sx, .M12 = m.M12 / sy, .M13 = m.M13 / sz,
            .M21 = m.M21 / sx, .M22 = m.M22 / sy, .M23 = m.M23 / sz,
            .M31 = m.M31 / sx, .M32 = m.M32 / sy, .M33 = m.M33 / sz
        }
    End Sub
    Public Overloads Function Equals(other As Transform_Class, Optional Tolerancia As Single = 0.00001) As Boolean
        If Math.Abs(Translation.X - other.Translation.X) > Tolerancia Then Return False
        If Math.Abs(Translation.Y - other.Translation.Y) > Tolerancia Then Return False
        If Math.Abs(Translation.Z - other.Translation.Z) > Tolerancia Then Return False
        ' Compare the per-axis effective scale (Scale · ScaleVector). The scalar Scale alone
        ' misses non-uniform differences (two transforms differing only in ScaleVector would
        ' otherwise compare equal). EffectiveScale folds both fields, so a legacy uniform
        ' transform still matches exactly (ScaleVector = (1,1,1)).
        Dim s1 = EffectiveScale
        Dim s2 = other.EffectiveScale
        If Math.Abs(s1.X - s2.X) > Tolerancia Then Return False
        If Math.Abs(s1.Y - s2.Y) > Tolerancia Then Return False
        If Math.Abs(s1.Z - s2.Z) > Tolerancia Then Return False
        ' Comparar la matriz Rotation elemento a elemento. Robusto a shear: no asume rotación pura ni
        ' usa axis-angle (Matrix33ToBSRotation), que es ambiguo en 180° y descartaría el shear. La
        ' magnitud (EffectiveScale) ya se comparó arriba; aquí se compara la dirección column-normalized.
        Dim r1 = Rotation, r2 = other.Rotation
        If Math.Abs(r1.M11 - r2.M11) > Tolerancia Then Return False
        If Math.Abs(r1.M12 - r2.M12) > Tolerancia Then Return False
        If Math.Abs(r1.M13 - r2.M13) > Tolerancia Then Return False
        If Math.Abs(r1.M21 - r2.M21) > Tolerancia Then Return False
        If Math.Abs(r1.M22 - r2.M22) > Tolerancia Then Return False
        If Math.Abs(r1.M23 - r2.M23) > Tolerancia Then Return False
        If Math.Abs(r1.M31 - r2.M31) > Tolerancia Then Return False
        If Math.Abs(r1.M32 - r2.M32) > Tolerancia Then Return False
        If Math.Abs(r1.M33 - r2.M33) > Tolerancia Then Return False
        Return True
    End Function
    Public Sub New(Origen As NiNode)
        Rotation = Origen.Rotation
        Translation = Origen.Translation
        Scale = Origen.Scale
    End Sub
    Public Sub New(Origen As BSSkinBoneTrans)
        Rotation = Origen.Rotation
        Translation = Origen.Translation
        Scale = Origen.Scale
    End Sub
    Public Sub New(Origen As BoneData)
        Rotation = Origen.SkinTransform.Rotation
        Translation = Origen.SkinTransform.Translation
        Scale = Origen.SkinTransform.Scale
    End Sub
    Public Shared Function EulerXYZToMatrix33(ByVal yawDeg As Double, ByVal pitchDeg As Double, ByVal rollDeg As Double) As Matrix33
        ' Convierte ángulos Z (yaw), Y (pitch), X (roll) en grados
        ' en la matriz 3×3 que produce ComposeTransforms directamente.

        ' 1) A radianes
        Dim yaw = yawDeg * Math.PI / 180.0  ' Z
        Dim pitch = pitchDeg * Math.PI / 180.0  ' Y
        Dim roll = rollDeg * Math.PI / 180.0  ' X

        ' 2) Senos y cosenos
        Dim cz = Math.Cos(yaw)
        Dim sz = Math.Sin(yaw)
        Dim cy = Math.Cos(pitch)
        Dim sy = Math.Sin(pitch)
        Dim cx = Math.Cos(roll)
        Dim sx = Math.Sin(roll)

        ' 3) Componer R_temp = Rx(roll) · Ry(pitch) · Rz(yaw)
        Dim R_temp As New Matrix33()
        ' Rx * Ry:
        ' A = Rx * Ry
        Dim A11 = 1 * cy + 0 * 0 + 0 * (-sy)
        Dim A12 = 0
        Dim A13 = 1 * sy

        Dim A21 = 0 * cy + cx * 0 + (-sx) * (-sy)
        Dim A22 = cx
        Dim A23 = -sx * cy

        Dim A31 = 0 * cy + sx * 0 + cx * (-sy)
        Dim A32 = sx
        Dim A33 = cx * cy

        ' Ahora R_temp = A * Rz
        R_temp.M11 = A11 * cz + A12 * sz + A13 * 0
        R_temp.M12 = -A11 * sz + A12 * cz + A13 * 0
        R_temp.M13 = A13 * 1

        R_temp.M21 = A21 * cz + A22 * sz + A23 * 0
        R_temp.M22 = -A21 * sz + A22 * cz + A23 * 0
        R_temp.M23 = A23 * 1

        R_temp.M31 = A31 * cz + A32 * sz + A33 * 0
        R_temp.M32 = -A31 * sz + A32 * cz + A33 * 0
        R_temp.M33 = A33 * 1

        ' 4) Aplicar la permutación J·R_temp·J
        Dim R As New Matrix33 With {
            .M11 = R_temp.M33,
            .M12 = R_temp.M32,
            .M13 = R_temp.M31,
            .M21 = R_temp.M23,
            .M22 = R_temp.M22,
            .M23 = R_temp.M21,
            .M31 = R_temp.M13,
            .M32 = R_temp.M12,
            .M33 = R_temp.M11
        }

        Return R
    End Function
    Public Shared Function Matrix33ToEulerXYZ(ByVal R As Matrix33) As Numerics.Vector3
        ' Robustez (2026-06-24): si R NO es ortonormal (p.ej. una Rotation column-normalized con shear
        ' tras un ComposeTransforms non-uniform), extraer Euler directo daría ángulos espurios. Polar-
        ' descomponemos a la rotación más cercana antes de continuar. El stretch/shear se pierde aquí
        ' (Euler no puede representarlo) — best-effort para serialización de poses; el render NUNCA usa
        ' este path (va por ToMatrix4d, que preserva el shear exacto). Para R ya ortonormal el
        ' short-circuit deja el cálculo legacy intacto ⇒ bit-idéntico.
        If Not IsRotationOrthonormal(R) Then R = PolarRotation(R)

        ' Primero deshacer la permutación: R_temp = J·R·J
        Dim Rt As New Matrix33 With {
        .M11 = R.M33, .M12 = R.M32, .M13 = R.M31,
        .M21 = R.M23, .M22 = R.M22, .M23 = R.M21,
        .M31 = R.M13, .M32 = R.M12, .M33 = R.M11
    }

        ' Clamp por posibles error numérico
        Dim sy As Double = Rt.M13
        ' Clamp manual en VB
        If sy > 1.0 Then
            sy = 1.0
        ElseIf sy < -1.0 Then
            sy = -1.0
        End If

        ' Ángulo pitch
        Dim pitchRad As Double = Math.Asin(sy)

        ' Calcular cos(pitch) para uso en denominadores
        Dim cp As Double = Math.Cos(pitchRad)

        Dim yawRad As Double
        Dim rollRad As Double

        ' Verificar singularidad (cp cerca de cero)
        If Math.Abs(cp) > 0.000001 Then
            ' yaw   a partir de Rt.M11 = cy*cz, Rt.M12 = -cy*sz
            yawRad = Math.Atan2(-Rt.M12, Rt.M11)
            ' roll  a partir de Rt.M23 = -sx*cy, Rt.M33 = cx*cy
            rollRad = Math.Atan2(-Rt.M23, Rt.M33)
        Else
            ' Gimbal lock: cos(pitch)=0
            ' Cuando pitch≈+90° (sy≈+1) ó -90° (sy≈-1)
            ' Configuramos yaw según rotación residual en XZ plano
            yawRad = 0.0
            ' roll a partir de Rt.M21 = sx*sy*cz+cx*sz y Rt.M22 = -sx*sy*sz+cx*cz
            rollRad = Math.Atan2(Rt.M21, Rt.M22)
        End If

        ' Convertir a grados
        Dim rad2deg As Double = 180.0 / Math.PI
        Return New Numerics.Vector3(
        CSng(yawRad * rad2deg),  ' Z (yaw)
        CSng(pitchRad * rad2deg),  ' Y (pitch)
        CSng(rollRad * rad2deg)   ' X (roll)
    )
    End Function

    Public Shared Function BSRotationToMatrix33(v As Numerics.Vector3) As Matrix33
        Dim angle As Double = Math.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z)
        Dim cosang As Double = Math.Cos(angle)
        Dim sinang As Double = Math.Sin(angle)
        Dim onemcosang As Double

        ' Evitar pérdida de precisión en 1 - cos(angle)
        If cosang > 0.5 Then
            onemcosang = (sinang * sinang) / (1 + cosang)
        Else
            onemcosang = 1 - cosang
        End If

        ' Vector normalizado o eje por defecto si el ángulo es 0
        Dim n As Numerics.Vector3
        If angle <> 0.0 Then
            n = New Numerics.Vector3(
            CSng(v.X / angle),
            CSng(v.Y / angle),
            CSng(v.Z / angle)
        )
        Else
            n = New Numerics.Vector3(1.0F, 0.0F, 0.0F)
        End If

        ' Construcción de matriz
        ' Diagonal
        Dim m As New Matrix33 With {
            .M11 = CSng(n.X * n.X * onemcosang + cosang),
            .M22 = CSng(n.Y * n.Y * onemcosang + cosang),
            .M33 = CSng(n.Z * n.Z * onemcosang + cosang),
            .M12 = CSng(n.X * n.Y * onemcosang - n.Z * sinang),
            .M21 = CSng(n.X * n.Y * onemcosang + n.Z * sinang),
            .M13 = CSng(n.X * n.Z * onemcosang + n.Y * sinang),
            .M31 = CSng(n.X * n.Z * onemcosang - n.Y * sinang),
            .M23 = CSng(n.Y * n.Z * onemcosang - n.X * sinang),
            .M32 = CSng(n.Y * n.Z * onemcosang + n.X * sinang)
        }

        Return m
    End Function
    Public Shared Function Matrix33ToBSRotation(ByVal M As Matrix33) As Numerics.Vector3
        ' Robustez (2026-06-24): igual que Matrix33ToEulerXYZ — si M tiene shear (no ortonormal) la
        ' fórmula axis-angle abajo da basura (la traza/(M-Mᵀ) asumen rotación pura). Polar-descomponemos
        ' a la rotación más cercana primero. Short-circuit para M ya ortonormal ⇒ bit-idéntico al legacy.
        If Not IsRotationOrthonormal(M) Then M = PolarRotation(M)

        ' 1) θ = acos((tr(M) – 1)/2)
        Dim tr As Double = M.M11 + M.M22 + M.M33
        Dim cosA As Double = (tr - 1.0) / 2.0
        If cosA > 1.0 Then
            cosA = 1.0
        ElseIf cosA < -1.0 Then
            cosA = -1.0
        End If

        Dim angle As Double = Math.Acos(cosA)

        ' 2) Si θ muy cercano a 0 o π, usar aproximaciones
        Dim sinA As Double = Math.Sin(angle)
        Dim ux, uy, uz As Double

        If Math.Abs(sinA) < 0.0001 Then
            ' Límite: (Mij - Mji)/(2 sin θ) * θ  ≈ (Mij - Mji)/(2) * sign(θ)
            ' Para θ≈0: sign(θ)=+1, para θ≈π: sin θ≈0 pero θ≈π => capturamos eje bien
            Dim half As Double = 0.5
            ' Para θ ≈ π, (tr-1)/2≈-1 => aquí manejaríamos ejes de rotación de 180°, 
            ' que corresponden a cualquier eje ortogonal al signo de (M - Mᵀ).
            ux = (M.M32 - M.M23) * half
            uy = (M.M13 - M.M31) * half
            uz = (M.M21 - M.M12) * half
            ' Ajustar longitud a θ (que puede ser π)
            Dim len As Double = Math.Sqrt(ux * ux + uy * uy + uz * uz)
            If len > 0.000001 Then
                ux = ux / len * angle
                uy = uy / len * angle
                uz = uz / len * angle
            Else
                ' Caso degenerado de 180° con eje indefinido: escoger (1,0,0)
                ux = angle : uy = 0 : uz = 0
            End If
        Else
            ' Rama normal
            Dim inv2sin As Double = 1.0 / (2.0 * sinA)
            ux = (M.M32 - M.M23) * inv2sin * angle
            uy = (M.M13 - M.M31) * inv2sin * angle
            uz = (M.M21 - M.M12) * inv2sin * angle
        End If

        Return New Numerics.Vector3(CSng(ux), CSng(uy), CSng(uz))
    End Function
    Public Function ComposeTransforms(b As Transform_Class) As Transform_Class
        ' Refactor 2026-04-29: soporta non-uniform scale via ScaleVector. Para uniform input
        ' (ambos ScaleVector=(1,1,1)), output bit-idéntico al legacy.
        '
        ' Math: T_x(v) = R_x · diag(EffectiveScale_x) · v + t_x. ComposeTransforms semántica:
        ' a.Compose(b) ≡ "apply b first then a" (row-vector convention, validated empíricamente).
        ' En notación column-vector: result.Rotation = R_b_eff · R_a_eff donde R_x_eff = R_x · diag(s_x_eff).
        ' Translation usa la fórmula legacy (preservada bit-a-bit) pero con la rotación efectiva.
        Dim a = Me
        Dim result As New Transform_Class()

        ' Rotación efectiva: bake del scale en R por column-multiply (convención única — ver cabecera
        ' de la clase): aRotEff[i,j] = R_a[i,j] · scale_eff[j] (columna j escalada). Para uniform input
        ' (ScaleVector=(1,1,1)) colapsa a Scale·R_a (escalar único).
        Dim aScaleEff = a.EffectiveScale
        Dim bScaleEff = b.EffectiveScale
        Dim aRotEff As New Matrix33 With {
            .M11 = a.Rotation.M11 * aScaleEff.X, .M12 = a.Rotation.M12 * aScaleEff.Y, .M13 = a.Rotation.M13 * aScaleEff.Z,
            .M21 = a.Rotation.M21 * aScaleEff.X, .M22 = a.Rotation.M22 * aScaleEff.Y, .M23 = a.Rotation.M23 * aScaleEff.Z,
            .M31 = a.Rotation.M31 * aScaleEff.X, .M32 = a.Rotation.M32 * aScaleEff.Y, .M33 = a.Rotation.M33 * aScaleEff.Z
        }
        Dim bRotEff As New Matrix33 With {
            .M11 = b.Rotation.M11 * bScaleEff.X, .M12 = b.Rotation.M12 * bScaleEff.Y, .M13 = b.Rotation.M13 * bScaleEff.Z,
            .M21 = b.Rotation.M21 * bScaleEff.X, .M22 = b.Rotation.M22 * bScaleEff.Y, .M23 = b.Rotation.M23 * bScaleEff.Z,
            .M31 = b.Rotation.M31 * bScaleEff.X, .M32 = b.Rotation.M32 * bScaleEff.Y, .M33 = b.Rotation.M33 * bScaleEff.Z
        }

        ' R_full = bRotEff · aRotEff (matrix multiply estándar, fila i de bRotEff con columna j de aRotEff).
        Dim rFull As Matrix33
        rFull.M11 = bRotEff.M11 * aRotEff.M11 + bRotEff.M12 * aRotEff.M21 + bRotEff.M13 * aRotEff.M31
        rFull.M12 = bRotEff.M11 * aRotEff.M12 + bRotEff.M12 * aRotEff.M22 + bRotEff.M13 * aRotEff.M32
        rFull.M13 = bRotEff.M11 * aRotEff.M13 + bRotEff.M12 * aRotEff.M23 + bRotEff.M13 * aRotEff.M33

        rFull.M21 = bRotEff.M21 * aRotEff.M11 + bRotEff.M22 * aRotEff.M21 + bRotEff.M23 * aRotEff.M31
        rFull.M22 = bRotEff.M21 * aRotEff.M12 + bRotEff.M22 * aRotEff.M22 + bRotEff.M23 * aRotEff.M32
        rFull.M23 = bRotEff.M21 * aRotEff.M13 + bRotEff.M22 * aRotEff.M23 + bRotEff.M23 * aRotEff.M33

        rFull.M31 = bRotEff.M31 * aRotEff.M11 + bRotEff.M32 * aRotEff.M21 + bRotEff.M33 * aRotEff.M31
        rFull.M32 = bRotEff.M31 * aRotEff.M12 + bRotEff.M32 * aRotEff.M22 + bRotEff.M33 * aRotEff.M32
        rFull.M33 = bRotEff.M31 * aRotEff.M13 + bRotEff.M32 * aRotEff.M23 + bRotEff.M33 * aRotEff.M33

        ' Decomposición: extraer column lengths y normalizar. Si uniform → guardar en Scale legacy
        ' (ScaleVector=(1,1,1)). Si non-uniform → guardar ScaleVector con per-axis (Scale=1).
        ' Esto preserva bit-identidad para uniform: result.Scale = a.Scale·b.Scale, R orthonormal.
        Dim col0Len = CSng(Math.Sqrt(rFull.M11 * rFull.M11 + rFull.M21 * rFull.M21 + rFull.M31 * rFull.M31))
        Dim col1Len = CSng(Math.Sqrt(rFull.M12 * rFull.M12 + rFull.M22 * rFull.M22 + rFull.M32 * rFull.M32))
        Dim col2Len = CSng(Math.Sqrt(rFull.M13 * rFull.M13 + rFull.M23 * rFull.M23 + rFull.M33 * rFull.M33))

        Const epsUniform As Single = 0.000001F
        Dim isResultUniform As Boolean = Math.Abs(col0Len - col1Len) < epsUniform AndAlso Math.Abs(col1Len - col2Len) < epsUniform

        If isResultUniform Then
            ' Uniform path: legacy semantics. result.Scale = column length (≈ a.Scale · b.Scale).
            ' result.ScaleVector = (1,1,1). result.Rotation = column-normalized (orthonormal).
            result.Scale = col0Len
            result.ScaleVector = New Numerics.Vector3(1, 1, 1)
            If col0Len > 0 Then
                Dim invLen As Single = 1.0F / col0Len
                result.Rotation = New Matrix33 With {
                    .M11 = rFull.M11 * invLen, .M12 = rFull.M12 * invLen, .M13 = rFull.M13 * invLen,
                    .M21 = rFull.M21 * invLen, .M22 = rFull.M22 * invLen, .M23 = rFull.M23 * invLen,
                    .M31 = rFull.M31 * invLen, .M32 = rFull.M32 * invLen, .M33 = rFull.M33 * invLen
                }
            Else
                result.Rotation = rFull
            End If
        Else
            ' Non-uniform path: column lengths capturan el scale per-axis efectivo. ScaleVector
            ' guarda (col0Len, col1Len, col2Len). Rotation se normaliza columna a columna.
            '
            ' Roundtrip exact (NO lossy): ToMatrix4d con column-multiply rebuild
            '   M[i,j] = R_normalized[i,j] · ScaleVector[j] = (rFull[i,j]/c_j) · c_j = rFull[i,j]
            ' bit-a-bit. R_normalized puede tener columnas no-mutuamente-ortogonales si rFull
            ' tiene shear, pero el contrato (R + ScaleVector reconstruyen rFull vía column-multiply)
            ' se cumple exact.
            '
            ' NOTA (auditoría 2026-06-24, resuelta): la R resultante puede tener columnas NO mutuamente
            ' ortogonales (shear) cuando el compose non-uniform mezcla rotación con escala per-eje. El
            ' roundtrip con ToMatrix4d (column-multiply) reproduce la matriz compuesta (exacto en reales,
            ' ~1 ULP en float), así que el RENDER es correcto (CPU y GPU skinning, bake FaceGen y mount van
            ' todos por ToMatrix4d, nunca leen .Rotation).
            ' Los extractores de rotación (Matrix33ToBSRotation/Matrix33ToEulerXYZ) — usados solo por
            ' serialización/edición de poses (export SAM, Poses_class.Clone) — detectan la
            ' no-ortonormalidad y polar-descomponen a la rotación más cercana (best-effort: ese formato
            ' no puede representar shear). Los ctors New(Matrix4/Matrix4d) e Inverse() también son
            ' column-based y roundtripean exacto. Invariante: el componente de shear SOLO debe vivir en
            ' capas estructurales (MorphDeltaTransform/MountDeltaTransform), nunca serializarse a pose.
            result.Scale = 1.0F
            result.ScaleVector = New Numerics.Vector3(col0Len, col1Len, col2Len)
            Dim invX As Single = If(col0Len > 0, 1.0F / col0Len, 1.0F)
            Dim invY As Single = If(col1Len > 0, 1.0F / col1Len, 1.0F)
            Dim invZ As Single = If(col2Len > 0, 1.0F / col2Len, 1.0F)
            result.Rotation = New Matrix33 With {
                .M11 = rFull.M11 * invX, .M12 = rFull.M12 * invY, .M13 = rFull.M13 * invZ,
                .M21 = rFull.M21 * invX, .M22 = rFull.M22 * invY, .M23 = rFull.M23 * invZ,
                .M31 = rFull.M31 * invX, .M32 = rFull.M32 * invY, .M33 = rFull.M33 * invZ
            }
        End If

        ' Translation: t_a + b.Translation · aRotEff (row-vector convention, consistente con
        ' ToMatrix4d = S·R·T donde el vector multiplica POR la fila). El cómputo de abajo es
        ' exactamente eso: out.X = bT·columna0(aRotEff) = bT.X·M11 + bT.Y·M21 + bT.Z·M31, etc.
        ' aRotEff ya absorbió a.Scale y a.ScaleVector, por eso b.Translation se pasa directo
        ' (sin escalar separadamente por a.Scale).
        Dim rotatedB As New Numerics.Vector3(
            b.Translation.X * aRotEff.M11 + b.Translation.Y * aRotEff.M21 + b.Translation.Z * aRotEff.M31,
            b.Translation.X * aRotEff.M12 + b.Translation.Y * aRotEff.M22 + b.Translation.Z * aRotEff.M32,
            b.Translation.X * aRotEff.M13 + b.Translation.Y * aRotEff.M23 + b.Translation.Z * aRotEff.M33
        )
        result.Translation = New Numerics.Vector3(a.Translation.X + rotatedB.X, a.Translation.Y + rotatedB.Y, a.Translation.Z + rotatedB.Z)
        Return result
    End Function
    Private Shared Function Transpose(m As Matrix33) As Matrix33
        Dim t As New Matrix33 With {
            .M11 = m.M11,
        .M12 = m.M21,
        .M13 = m.M31,
            .M21 = m.M12,
        .M22 = m.M22,
        .M23 = m.M32,
            .M31 = m.M13,
        .M32 = m.M23,
        .M33 = m.M33
        }
        Return t
    End Function

    Private Shared Function MultiplyMatrixVector(m As Matrix33, v As Numerics.Vector3) As Numerics.Vector3
        ' Coincide con la forma en que ComposeTransforms aplica la rotación
        Return New Numerics.Vector3(
        m.M11 * v.X + m.M21 * v.Y + m.M31 * v.Z,
        m.M12 * v.X + m.M22 * v.Y + m.M32 * v.Z,
        m.M13 * v.X + m.M23 * v.Y + m.M33 * v.Z
    )
    End Function

    ' Inverse 2026-04-29: dual-path por compatibilidad y corrección.
    '   Uniform path (ScaleVector=(1,1,1) AND Rotation orthonormal): usa Transpose como inversa
    '     de R y 1/Scale. Bit-idéntico al legacy. Ruta caliente para BodySlide/SAM/poses uniformes.
    '   Non-uniform path: usa Matrix4d.Invert numérico — correcto para non-uniform AND para
    '     Rotation non-orthonormal post-ComposeTransforms con shear (raro).
    ' Esto repara el bug latente del Inverse que asumía orthonormalidad y rompía cuando
    ' el column-multiply hack (ahora removido del constructor) había contaminado Rotation.
    Public Function Inverse() As Transform_Class
        If Me.IsUniformScale AndAlso IsRotationOrthonormal(Me.Rotation) Then
            ' Legacy fast path: Transpose es inversa de R orthonormal; 1/Scale invierte uniform.
            Dim inv As New Transform_Class With {
                .Rotation = Transpose(Me.Rotation)
            }
            If Me.Scale = 0 Then Throw New InvalidOperationException("Zero scale is not invertible")
            inv.Scale = 1.0F / Me.Scale
            inv.ScaleVector = New Numerics.Vector3(1, 1, 1)
            Dim rotatedT As Numerics.Vector3 = MultiplyMatrixVector(inv.Rotation, Me.Translation)
            inv.Translation = rotatedT * -inv.Scale
            Return inv
        Else
            ' Numerical path: Matrix4d.Invert maneja non-uniform y non-orthonormal.
            ' OpenTK Matrix4d.Invert(mat) es el overload que throws si es singular.
            Dim m4d = Me.ToMatrix4d()
            Try
                Dim inv4d = Matrix4d.Invert(m4d)
                Return New Transform_Class(inv4d)
            Catch ex As InvalidOperationException
                Throw New InvalidOperationException("Transform not invertible (singular matrix)", ex)
            End Try
        End If
    End Function

    Private Shared Function IsRotationOrthonormal(r As Matrix33, Optional eps As Single = 0.001F) As Boolean
        ' Test rápido: cada columna debe tener norma ≈ 1 y ser ortogonal a las demás.
        Dim c0x = r.M11, c0y = r.M21, c0z = r.M31
        Dim c1x = r.M12, c1y = r.M22, c1z = r.M32
        Dim c2x = r.M13, c2y = r.M23, c2z = r.M33
        Dim n0 = c0x * c0x + c0y * c0y + c0z * c0z
        Dim n1 = c1x * c1x + c1y * c1y + c1z * c1z
        Dim n2 = c2x * c2x + c2y * c2y + c2z * c2z
        If Math.Abs(n0 - 1.0F) > eps Then Return False
        If Math.Abs(n1 - 1.0F) > eps Then Return False
        If Math.Abs(n2 - 1.0F) > eps Then Return False
        ' Ortogonalidad entre columnas
        Dim d01 = c0x * c1x + c0y * c1y + c0z * c1z
        Dim d02 = c0x * c2x + c0y * c2y + c0z * c2z
        Dim d12 = c1x * c2x + c1y * c2y + c1z * c2z
        If Math.Abs(d01) > eps Then Return False
        If Math.Abs(d02) > eps Then Return False
        If Math.Abs(d12) > eps Then Return False
        Return True
    End Function

    ''' <summary>Factor rotacional (ortogonal) de la descomposición polar M = Q·P, donde Q es ortonormal
    ''' y P simétrica positiva-definida. Q es la rotación MÁS CERCANA a M en norma de Frobenius. Lo usan
    ''' los extractores de rotación (<see cref="Matrix33ToEulerXYZ"/>, <see cref="Matrix33ToBSRotation"/>)
    ''' cuando reciben una Rotation con shear (columnas no mutuamente ortogonales) producida por un
    ''' ComposeTransforms/Inverse non-uniform — convertirla a Euler/axis-angle directo daría basura.
    ''' <para>Algoritmo: iteración de Higham X←½(X + X⁻ᵀ), que converge cuadráticamente a Q. Doble
    ''' precisión. La inversa-transpuesta 3×3 se computa por cofactores (productos cruz de filas),
    ''' SIN depender de OpenTK Matrix3d.Invert/Transposed: así la guarda de singularidad es explícita
    ''' (det≈0 ⇒ corta y devuelve el mejor X alcanzado) y no depende de la versión de OpenTK.</para>
    ''' <para>Para una matriz X de filas (r0,r1,r2): X⁻ᵀ tiene filas (r1×r2, r2×r0, r0×r1)/det, con
    ''' det = r0·(r1×r2). (Verificación: para X=I da I, correcto.)</para></summary>
    Private Shared Function PolarRotation(m As Matrix33) As Matrix33
        Dim r0 As New Vector3d(m.M11, m.M12, m.M13)
        Dim r1 As New Vector3d(m.M21, m.M22, m.M23)
        Dim r2 As New Vector3d(m.M31, m.M32, m.M33)
        For iter As Integer = 1 To 64
            ' X⁻ᵀ por cofactores: fila i = (producto cruz de las otras dos filas) / det.
            Dim cof0 = Vector3d.Cross(r1, r2)
            Dim cof1 = Vector3d.Cross(r2, r0)
            Dim cof2 = Vector3d.Cross(r0, r1)
            Dim det = Vector3d.Dot(r0, cof0)
            If Math.Abs(det) < 0.000000000001 Then Exit For   ' singular: devolver el mejor X alcanzado
            Dim invDet = 1.0 / det
            Dim n0 = 0.5 * (r0 + cof0 * invDet)
            Dim n1 = 0.5 * (r1 + cof1 * invDet)
            Dim n2 = 0.5 * (r2 + cof2 * invDet)
            Dim diff = (n0 - r0).LengthSquared + (n1 - r1).LengthSquared + (n2 - r2).LengthSquared
            r0 = n0 : r1 = n1 : r2 = n2
            If diff < 0.000000000001 Then Exit For
        Next
        Return New Matrix33 With {
            .M11 = CSng(r0.X), .M12 = CSng(r0.Y), .M13 = CSng(r0.Z),
            .M21 = CSng(r1.X), .M22 = CSng(r1.Y), .M23 = CSng(r1.Z),
            .M31 = CSng(r2.X), .M32 = CSng(r2.Y), .M33 = CSng(r2.Z)
        }
    End Function

    Public Function ToMatrix4() As Matrix4
        ' 3×3 final = R · diag(ScaleVector): column-multiply, convención única (ver cabecera de la clase).
        ' Roundtrip exacto (en reales) con los ctors New(Matrix4*) y con ComposeTransforms (column-based).
        ' La escala uniforme legacy va por S = CreateScale(Scale); con ScaleVector=(1,1,1) R queda = Rotation.
        Dim sv = ScaleVector
        Dim S = Matrix4.CreateScale(Scale)
        Dim T = Matrix4.CreateTranslation(Translation.X, Translation.Y, Translation.Z)
        Dim R As New Matrix4(Rotation.M11 * sv.X, Rotation.M12 * sv.Y, Rotation.M13 * sv.Z, 0.0F,
                             Rotation.M21 * sv.X, Rotation.M22 * sv.Y, Rotation.M23 * sv.Z, 0.0F,
                             Rotation.M31 * sv.X, Rotation.M32 * sv.Y, Rotation.M33 * sv.Z, 0.0F,
                             0.0F, 0.0F, 0.0F, 1.0F)
        Return S * R * T
    End Function

    Public Function ToMatrix4d() As Matrix4d
        ' Ver ToMatrix4 — misma lógica column-multiply (R · diag(ScaleVector)) con doubles.
        Dim sv = ScaleVector
        Dim S = Matrix4d.CreateScale(Scale)
        Dim T = Matrix4d.CreateTranslation(Translation.X, Translation.Y, Translation.Z)
        Dim R As New Matrix4d(Rotation.M11 * sv.X, Rotation.M12 * sv.Y, Rotation.M13 * sv.Z, 0.0F,
                              Rotation.M21 * sv.X, Rotation.M22 * sv.Y, Rotation.M23 * sv.Z, 0.0F,
                              Rotation.M31 * sv.X, Rotation.M32 * sv.Y, Rotation.M33 * sv.Z, 0.0F,
                              0.0F, 0.0F, 0.0F, 1.0F)
        Return S * R * T
    End Function
    Private Shared Function PrintMatrix33(que As Matrix33) As String
        Dim str = "M11:" + que.M11.ToString
        str += "," + "M12:" + que.M12.ToString
        str += "," + "M13:" + que.M13.ToString
        str += "," + "M21:" + que.M21.ToString
        str += "," + "M22:" + que.M22.ToString
        str += "," + "M23:" + que.M23.ToString
        str += "," + "M31:" + que.M31.ToString
        str += "," + "M32:" + que.M32.ToString
        str += "," + "M33:" + que.M33.ToString
        Return str
    End Function

End Class
