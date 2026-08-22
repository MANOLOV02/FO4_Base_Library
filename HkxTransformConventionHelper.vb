Option Strict On
Option Explicit On

Imports System.Linq
Imports OpenTK.Mathematics

''' <summary>
''' Canonical HKX QS-transform conversion used by both BSClothExtraData skeleton
''' injection and HKX pose import. HKX quaternions are xyzw, matching OpenTK.
''' </summary>
Public NotInheritable Class HkxTransformConventionHelper
    Private Sub New()
    End Sub

    Public Shared Function ToTransform(source As HkxQsTransformGraph_Class) As Transform_Class
        If source Is Nothing Then Return New Transform_Class()
        Return ToTransform(source.Translation, source.Rotation, source.Scale)
    End Function

    Public Shared Function ToTransform(translation As HkxVector4Graph_Class,
                                       rotationSource As HkxQuaternionGraph_Class,
                                       scaleSource As HkxVector4Graph_Class) As Transform_Class
        Dim scale = ResolveScaleVector(scaleSource)
        Dim tx = If(translation Is Nothing, 0.0F, translation.X)
        Dim ty = If(translation Is Nothing, 0.0F, translation.Y)
        Dim tz = If(translation Is Nothing, 0.0F, translation.Z)
        Return ToTransform(tx, ty, tz, rotationSource, scale)
    End Function

    Public Shared Function ToTransform(translationX As Single,
                                       translationY As Single,
                                       translationZ As Single,
                                       rotationSource As HkxQuaternionGraph_Class,
                                       scaleX As Single,
                                       scaleY As Single,
                                       scaleZ As Single) As Transform_Class
        Dim scale = ResolveScaleVector(scaleX, scaleY, scaleZ)
        Return ToTransform(translationX, translationY, translationZ, rotationSource, scale)
    End Function

    Private Shared Function ToTransform(translationX As Single,
                                        translationY As Single,
                                        translationZ As Single,
                                        rotationSource As HkxQuaternionGraph_Class,
                                        scale As Vector3) As Transform_Class
        Dim rotation As Quaternion

        If rotationSource Is Nothing Then
            rotation = Quaternion.Identity
        Else
            rotation = New Quaternion(rotationSource.X, rotationSource.Y, rotationSource.Z, rotationSource.W)
            If rotation.LengthSquared <= 0.000001F Then
                rotation = Quaternion.Identity
            Else
                rotation = Quaternion.Normalize(rotation)
            End If
        End If

        ' ESCALA PER-EJE, no promediada. `Transform_Class` la descompone en (Scale, ScaleVector) con
        ' su unica ley de uniformidad, asi que un clip uniforme sigue cayendo en la rama uniforme
        ' EXACTA (ScaleVector = (1,1,1)) y no cambia un bit; el per-eje ahora sobrevive.
        Dim transformMatrix =
            Matrix4.CreateScale(scale.X, scale.Y, scale.Z) *
            Matrix4.CreateFromQuaternion(rotation) *
            Matrix4.CreateTranslation(translationX, translationY, translationZ)

        Return New Transform_Class(transformMatrix)
    End Function

    ''' <summary>Escala del HKX como VECTOR, sin promediar. Un componente no finito o practicamente
    ''' cero no se descarta (eso mezclaba ejes): se repone en 1.0, que es el neutro de ESE eje.
    ''' <para>MEDIDO 2026-08-22 sobre 242 esqueletos y 744 animaciones de los BA2 de FO4: 39 de 5085
    ''' huesos de refPose y 452 de 711.885 escalas de frame son per-eje. Es poco y chico (peor caso
    ''' 2,6 %, con pinta de ruido de cuantizacion del spline), pero el promedio lo borraba EN SILENCIO
    ''' y no habia forma de saberlo aguas abajo.</para></summary>
    Public Shared Function ResolveScaleVector(scale As HkxVector4Graph_Class) As Vector3
        If scale Is Nothing Then Return Vector3.One
        Return ResolveScaleVector(scale.X, scale.Y, scale.Z)
    End Function

    ''' <summary>Idem, por componentes.</summary>
    Public Shared Function ResolveScaleVector(x As Single, y As Single, z As Single) As Vector3
        Return New Vector3(EjeValido(x), EjeValido(y), EjeValido(z))
    End Function

    Private Shared Function EjeValido(v As Single) As Single
        If Not Single.IsFinite(v) OrElse Math.Abs(v) <= 0.000001F Then Return 1.0F
        Return v
    End Function

End Class
