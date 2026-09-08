Option Strict On
Option Explicit On

Imports OpenTK.Mathematics

' =================================================================================================
' LECTURA del dato de tela — lo poco que sobrevivió al borrado de `HavokClothSimulation.vb`.
'
' ⛔⛔ ACÁ NO HAY SOLVER. El solver es `Havok.Motor`, transcrito del `.exe` instrucción por
' instrucción. `HavokClothSimulation.vb` —4.900 líneas de solver propio— se BORRÓ; lo que quedó es
' lo que nunca fue física:
'
'   · `BindWorldEmbebido`: compone la pose de bind que el `hkaSkeleton` del paquete trae embebida.
'     Es lectura del archivo; el motor no la usa (sus poses vienen del esqueleto vivo).
'   · `ANumerics` / `AOpenTK`: convertir entre las dos representaciones de matriz que conviven en el
'     árbol. Dos líneas cada una.
'
' ⛔ Lo que NO sobrevivió, por estar duplicado con el motor canónico:
'   · `SkinPoint`        ⇒ `Havok.Motor.Piel` (`hclObjectSpaceSkinPNOperator`, `0x14193DC90`).
'   · `DeformarHueso`    ⇒ `Havok.Motor.Operadores.DeformarHuesosSimple` (`0x14195AA90`).
'   · `MarcoDelTriangulo`⇒ el mismo `DeformarHuesosSimple`, que lo arma adentro.
'   · `StepShapes` / `ResetAll` ⇒ `Havok.Physics.ClothCanonico`.
'   · las tres listas declarativas ⇒ `Havok.Motor.Declarado`.
' =================================================================================================

Namespace Havok.Physics

    ''' <summary>Lectura y conversión sobre el dato de tela. No hay física acá.</summary>
    Public NotInheritable Class LecturaDeTela

        Private Sub New()
        End Sub

        ''' <summary>
        ''' La pose de bind GLOBAL de cada hueso del `hkaSkeleton` embebido, compuesta desde la raíz.
        ''' <para>El paquete `hcl` trae su propio esqueleto con las poses de referencia en LOCAL
        ''' (`hkaSkeleton.referencePose`) y el padre de cada hueso (`parentIndices`). Esto las compone.</para>
        ''' <para>⛔ El motor NO usa esto: sus matrices salen del esqueleto VIVO por cada cuadro. Es
        ''' una herramienta para auditar lo que el archivo declara.</para>
        ''' </summary>
        Public Shared Function BindWorldEmbebido(skel As Havok.Canon.Objects.HkObj_HkaSkeleton) As Matrix4()
            If skel Is Nothing OrElse skel.Bones Is Nothing Then Return Array.Empty(Of Matrix4)()
            Dim n = skel.Bones.Count
            Dim r(Math.Max(0, n - 1)) As Matrix4
            Dim listo(Math.Max(0, n - 1)) As Boolean
            If n = 0 Then Return r

            Dim locales(n - 1) As Matrix4
            For i = 0 To n - 1
                locales(i) = Matrix4.Identity
                If skel.ReferencePose Is Nothing OrElse i >= skel.ReferencePose.Count Then Continue For
                Dim qs = skel.ReferencePose(i)
                If qs Is Nothing Then Continue For
                locales(i) = HkxTransformConventionHelper.ToTransform(qs).ToMatrix4()
            Next

            ' ⛔ Por PROFUNDIDAD, no por índice: un hijo puede aparecer antes que su padre en el
            ' arreglo y componerse contra una matriz sin llenar.
            Dim guarda = 0
            While guarda <= n
                Dim algo = False
                For i = 0 To n - 1
                    If listo(i) Then Continue For
                    Dim p = -1
                    If skel.ParentIndices IsNot Nothing AndAlso i < skel.ParentIndices.Count Then p = CInt(skel.ParentIndices(i))
                    If p < 0 OrElse p >= n Then
                        r(i) = locales(i)
                        listo(i) = True
                        algo = True
                    ElseIf listo(p) Then
                        r(i) = Matrix4.Mult(locales(i), r(p))
                        listo(i) = True
                        algo = True
                    End If
                Next
                If Not algo Then Exit While
                guarda += 1
            End While
            Return r
        End Function

        ''' <summary>`Matrix4` de OpenTK a `Matrix4x4` de System.Numerics, fila por fila.</summary>
        Public Shared Function ANumerics(m As Matrix4) As System.Numerics.Matrix4x4
            Return New System.Numerics.Matrix4x4(m.M11, m.M12, m.M13, m.M14,
                                                 m.M21, m.M22, m.M23, m.M24,
                                                 m.M31, m.M32, m.M33, m.M34,
                                                 m.M41, m.M42, m.M43, m.M44)
        End Function

        ''' <summary>`Vector3` de System.Numerics a `Vector3` de OpenTK.</summary>
        Public Shared Function AOpenTK(v As System.Numerics.Vector3) As Vector3
            Return New Vector3(v.X, v.Y, v.Z)
        End Function

    End Class

End Namespace
