' Version Uploaded of Fo4Library 3.2.0

Imports NiflySharp.Blocks
Imports NiflySharp.Structs
Imports OpenTK.Mathematics
Public Class Transform_Class
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
    Public Overrides Function ToString() As String
        Return "Translation: " + Translation.ToString + vbCrLf + "Rotation:" + PrintMatrix33(Rotation) + vbCrLf + "Scale:" + Scale.ToString
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
    End Sub
    Public Sub New(m As Matrix4d)
        ' 1) Extraer traslación (columna 4)
        Translation = New Numerics.Vector3(m.M41, m.M42, m.M43)

        ' 2) Extraer escala: longitud de cada columna de la submatriz 3×3
        Dim col0 As New Vector3d(m.M11, m.M12, m.M13)
        Dim col1 As New Vector3d(m.M21, m.M22, m.M23)
        Dim col2 As New Vector3d(m.M31, m.M32, m.M33)
        Dim sx = col0.Length
        Dim sy = col1.Length
        Dim sz = col2.Length
        If sx = 0 Then sx = 1
        If sy = 0 Then sy = 1
        If sz = 0 Then sz = 1
        Scale = sx
        ' 3) Formar la matriz de rotación normalizando esas columnas
        Rotation = New Matrix33 With {
        .M11 = m.M11 / sx, .M12 = m.M12 / sx, .M13 = m.M13 / sx,
        .M21 = m.M21 / sy, .M22 = m.M22 / sy, .M23 = m.M23 / sy,
        .M31 = m.M31 / sz, .M32 = m.M32 / sz, .M33 = m.M33 / sz
    }
    End Sub
    Public Sub New(m As Matrix4)
        ' 1) Extraer traslación (columna 4)
        Translation = New Numerics.Vector3(m.M41, m.M42, m.M43)

        ' 2) Extraer escala: longitud de cada columna de la submatriz 3×3
        Dim col0 As New Vector3(m.M11, m.M12, m.M13)
        Dim col1 As New Vector3(m.M21, m.M22, m.M23)
        Dim col2 As New Vector3(m.M31, m.M32, m.M33)
        Dim sx = col0.Length
        Dim sy = col1.Length
        Dim sz = col2.Length
        If sx = 0 Then sx = 1
        If sy = 0 Then sy = 1
        If sz = 0 Then sz = 1
        Scale = sx
        ' 3) Formar la matriz de rotación normalizando esas columnas
        Rotation = New Matrix33 With {
        .M11 = m.M11 / sx, .M12 = m.M12 / sx, .M13 = m.M13 / sx,
        .M21 = m.M21 / sy, .M22 = m.M22 / sy, .M23 = m.M23 / sy,
        .M31 = m.M31 / sz, .M32 = m.M32 / sz, .M33 = m.M33 / sz
    }
    End Sub
    Public Overloads Function Equals(other As Transform_Class, Optional Tolerancia As Single = 0.00001)
        If Math.Abs(Translation.X - other.Translation.X) > Tolerancia Then Return False
        If Math.Abs(Translation.Y - other.Translation.Y) > Tolerancia Then Return False
        If Math.Abs(Translation.Z - other.Translation.Z) > Tolerancia Then Return False
        If Math.Abs(Scale - other.Scale) > Tolerancia Then Return False
        Dim rot1 = Matrix33ToBSRotation(Rotation)
        Dim rot2 = Matrix33ToBSRotation(other.Rotation)
        If Math.Abs(rot1.X - rot2.X) > Tolerancia Then Return False
        If Math.Abs(rot1.Y - rot2.Y) > Tolerancia Then Return False
        If Math.Abs(rot1.Z - rot2.Z) > Tolerancia Then Return False
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
        Dim a = Me
        Dim result As New Transform_Class()

        Dim r As Matrix33
        r.M11 = b.Rotation.M11 * a.Rotation.M11 + b.Rotation.M12 * a.Rotation.M21 + b.Rotation.M13 * a.Rotation.M31
        r.M12 = b.Rotation.M11 * a.Rotation.M12 + b.Rotation.M12 * a.Rotation.M22 + b.Rotation.M13 * a.Rotation.M32
        r.M13 = b.Rotation.M11 * a.Rotation.M13 + b.Rotation.M12 * a.Rotation.M23 + b.Rotation.M13 * a.Rotation.M33

        r.M21 = b.Rotation.M21 * a.Rotation.M11 + b.Rotation.M22 * a.Rotation.M21 + b.Rotation.M23 * a.Rotation.M31
        r.M22 = b.Rotation.M21 * a.Rotation.M12 + b.Rotation.M22 * a.Rotation.M22 + b.Rotation.M23 * a.Rotation.M32
        r.M23 = b.Rotation.M21 * a.Rotation.M13 + b.Rotation.M22 * a.Rotation.M23 + b.Rotation.M23 * a.Rotation.M33

        r.M31 = b.Rotation.M31 * a.Rotation.M11 + b.Rotation.M32 * a.Rotation.M21 + b.Rotation.M33 * a.Rotation.M31
        r.M32 = b.Rotation.M31 * a.Rotation.M12 + b.Rotation.M32 * a.Rotation.M22 + b.Rotation.M33 * a.Rotation.M32
        r.M33 = b.Rotation.M31 * a.Rotation.M13 + b.Rotation.M32 * a.Rotation.M23 + b.Rotation.M33 * a.Rotation.M33

        result.Rotation = r
        result.Scale = a.Scale * b.Scale
        Dim scaledB = New Numerics.Vector3(b.Translation.X * a.Scale, b.Translation.Y * a.Scale, b.Translation.Z * a.Scale)
        Dim rotatedB As New Numerics.Vector3(scaledB.X * a.Rotation.M11 + scaledB.Y * a.Rotation.M21 + scaledB.Z * a.Rotation.M31, scaledB.X * a.Rotation.M12 + scaledB.Y * a.Rotation.M22 + scaledB.Z * a.Rotation.M32, scaledB.X * a.Rotation.M13 + scaledB.Y * a.Rotation.M23 + scaledB.Z * a.Rotation.M33)
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

    ' Y esta es la implementación completa de Inverse, sin usar Matrix4:
    Public Function Inverse() As Transform_Class
        ' 1) Rotación inversa = traspuesta
        Dim inv As New Transform_Class With {
            .Rotation = Transpose(Me.Rotation)
        }

        ' 2) Escala inversa
        If Me.Scale = 0 Then Throw New InvalidOperationException("Escala cero no inversible")
        inv.Scale = 1.0F / Me.Scale

        ' 3) Traslación inversa = -(1/Scale) * (Rotationᵀ * Translation)
        Dim rotatedT As Numerics.Vector3 = MultiplyMatrixVector(inv.Rotation, Me.Translation)
        inv.Translation = rotatedT * -inv.Scale

        Return inv
    End Function

    Public Function ToMatrix4() As Matrix4
        ' 1) Escala
        Dim S = Matrix4.CreateScale(Scale)
        ' 2) Rotación — volcamos row-major
        Dim R As New Matrix4(Rotation.M11, Rotation.M12, Rotation.M13, 0.0F,
                                 Rotation.M21, Rotation.M22, Rotation.M23, 0.0F,
                                 Rotation.M31, Rotation.M32, Rotation.M33, 0.0F,
        0.0F, 0.0F, 0.0F, 1.0F)
        ' 3) Traslación
        Dim T = Matrix4.CreateTranslation(Translation.X, Translation.Y, Translation.Z)
        ' 4) Orden: T · R · S
        Return S * R * T
    End Function

    Public Function ToMatrix4d() As Matrix4d
        ' 1) Escala
        Dim S = Matrix4d.CreateScale(Scale)
        ' 2) Rotación — volcamos row-major
        Dim R As New Matrix4d(Rotation.M11, Rotation.M12, Rotation.M13, 0.0F,
                                 Rotation.M21, Rotation.M22, Rotation.M23, 0.0F,
                                 Rotation.M31, Rotation.M32, Rotation.M33, 0.0F,
        0.0F, 0.0F, 0.0F, 1.0F)
        ' 3) Traslación
        Dim T = Matrix4d.CreateTranslation(Translation.X, Translation.Y, Translation.Z)
        ' 4) Orden: T · R · S
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