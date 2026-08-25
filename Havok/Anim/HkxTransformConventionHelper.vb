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

    ''' <summary>El `hkQsTransform` partido en sus tres `vector4`, que es como lo entrega un track
    ''' ya descomprimido. Un componente ausente cae a su neutro: 0 en traslacion, identidad en
    ''' rotacion, 1 en escala — los mismos neutros que ponia la version con portadores.</summary>
    Public Shared Function ToTransform(translation As Single(), rotation As Single(), scale As Single()) As Transform_Class
        Dim tx = If(translation IsNot Nothing AndAlso translation.Length > 0, translation(0), 0.0F)
        Dim ty = If(translation IsNot Nothing AndAlso translation.Length > 1, translation(1), 0.0F)
        Dim tz = If(translation IsNot Nothing AndAlso translation.Length > 2, translation(2), 0.0F)
        Dim sx = If(scale IsNot Nothing AndAlso scale.Length > 0, scale(0), 1.0F)
        Dim sy = If(scale IsNot Nothing AndAlso scale.Length > 1, scale(1), 1.0F)
        Dim sz = If(scale IsNot Nothing AndAlso scale.Length > 2, scale(2), 1.0F)
        If rotation Is Nothing OrElse rotation.Length < 4 Then
            Return ToTransformRaw(tx, ty, tz, 0.0F, 0.0F, 0.0F, 1.0F, sx, sy, sz)
        End If
        Return ToTransformRaw(tx, ty, tz, rotation(0), rotation(1), rotation(2), rotation(3), sx, sy, sz)
    End Function

    Public Shared Function ToTransform(qs As Single()) As Transform_Class
        ' Delega en la version sin allocar: este camino tambien lo recorre el render por frame.
        Return ToTransformFromFloats(qs)
    End Function

    ''' <summary>
    ''' Version SIN ALLOCAR de <see cref="ToTransform"/>: el cuaternion entra como cuatro floats.
    ''' <para>⛔ Existe por el camino CALIENTE de animacion: `BuildPose` la llama por hueso y por
    ''' frame. Pasar por `HkxQuaternionGraph_Class` construia un objeto por llamada, que con ~100
    ''' huesos a 30 fps son miles de allocaciones por segundo y se nota en la fluidez.</para>
    ''' </summary>
    Public Shared Function ToTransformRaw(translationX As Single, translationY As Single, translationZ As Single,
                                          rotX As Single, rotY As Single, rotZ As Single, rotW As Single,
                                          scaleX As Single, scaleY As Single, scaleZ As Single) As Transform_Class
        Dim scale = ResolveScaleVector(scaleX, scaleY, scaleZ)
        Dim rotation As New Quaternion(rotX, rotY, rotZ, rotW)
        If rotation.LengthSquared <= 0.000001F Then
            rotation = Quaternion.Identity
        Else
            rotation = Quaternion.Normalize(rotation)
        End If
        Dim transformMatrix =
            Matrix4.CreateScale(scale.X, scale.Y, scale.Z) *
            Matrix4.CreateFromQuaternion(rotation) *
            Matrix4.CreateTranslation(translationX, translationY, translationZ)
        Return New Transform_Class(transformMatrix)
    End Function

    ''' <summary>`hkQsTransform` desde sus 12 floats crudos, sin materializar objetos intermedios.</summary>
    Public Shared Function ToTransformFromFloats(qs As Single()) As Transform_Class
        If qs Is Nothing OrElse qs.Length < 12 Then Return New Transform_Class()
        Return ToTransformRaw(qs(0), qs(1), qs(2), qs(4), qs(5), qs(6), qs(7), qs(8), qs(9), qs(10))
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
