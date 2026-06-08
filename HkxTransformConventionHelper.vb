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
        Dim scale = ResolveUniformScale(scaleSource)
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
        Dim scale = ResolveUniformScale(scaleX, scaleY, scaleZ)
        Return ToTransform(translationX, translationY, translationZ, rotationSource, scale)
    End Function

    Private Shared Function ToTransform(translationX As Single,
                                        translationY As Single,
                                        translationZ As Single,
                                        rotationSource As HkxQuaternionGraph_Class,
                                        scale As Single) As Transform_Class
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

        Dim transformMatrix =
            Matrix4.CreateScale(scale) *
            Matrix4.CreateFromQuaternion(rotation) *
            Matrix4.CreateTranslation(translationX, translationY, translationZ)

        Return New Transform_Class(transformMatrix)
    End Function

    Public Shared Function ResolveUniformScale(scale As HkxVector4Graph_Class) As Single
        If scale Is Nothing Then Return 1.0F

        Dim values = {scale.X, scale.Y, scale.Z}.
            Where(Function(value) Single.IsFinite(value) AndAlso Math.Abs(value) > 0.000001F).
            ToArray()

        If values.Length = 0 Then Return 1.0F
        Return CSng(values.Average())
    End Function

    Public Shared Function ResolveUniformScale(x As Single, y As Single, z As Single) As Single
        Dim sum As Single = 0.0F
        Dim count = 0

        If Single.IsFinite(x) AndAlso Math.Abs(x) > 0.000001F Then
            sum += x
            count += 1
        End If
        If Single.IsFinite(y) AndAlso Math.Abs(y) > 0.000001F Then
            sum += y
            count += 1
        End If
        If Single.IsFinite(z) AndAlso Math.Abs(z) > 0.000001F Then
            sum += z
            count += 1
        End If

        If count = 0 Then Return 1.0F
        Return sum / count
    End Function
End Class
