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

    ''' <summary>
    ''' `hkQsTransform` desde sus 12 floats crudos, que es como lo entrega el objeto generado
    ''' (`HavokObjects.vb`). El layout lo declara la reflexion: 48 bytes = translation(vector4) +
    ''' rotation(quaternion) + scale(vector4).
    ''' <para>Esta conversion NO se genera: la reflexion dice donde estan los floats, no que la
    ''' rotacion es un cuaternion que hay que normalizar ni como se resuelve una escala en cero.
    ''' Por eso el camino de decodificacion sigue siendo codigo escrito a mano, y vive aca, en el
    ''' mismo lugar que la version que toma los objetos del parser viejo — asi las dos no pueden
    ''' divergir.</para>
    ''' </summary>
    ''' <summary>
    ''' Los 12 floats crudos de un `hkQsTransform` (lo que entrega el objeto generado) en la forma
    ''' estructurada que espera la matematica de animacion. El layout lo declara la reflexion:
    ''' translation(vector4) @0, rotation(quaternion) @16, scale(vector4) @32.
    ''' <para>Es el UNICO punto donde se interpreta ese layout, para que no vuelva a haber dos
    ''' lecturas del mismo dato que puedan divergir.</para>
    ''' </summary>
    Public Shared Function QsFromFloats(qs As Single()) As HkxQsTransformGraph_Class
        If qs Is Nothing OrElse qs.Length < 12 Then Return Nothing
        Return New HkxQsTransformGraph_Class With {
            .Translation = New HkxVector4Graph_Class With {.X = qs(0), .Y = qs(1), .Z = qs(2), .W = qs(3)},
            .Rotation = New HkxQuaternionGraph_Class With {.X = qs(4), .Y = qs(5), .Z = qs(6), .W = qs(7)},
            .Scale = New HkxVector4Graph_Class With {.X = qs(8), .Y = qs(9), .Z = qs(10), .W = qs(11)}
        }
    End Function

    Public Shared Function ToTransform(qs As Single()) As Transform_Class
        ' Delega en la version sin allocar: este camino tambien lo recorre el render por frame.
        Return ToTransformFromFloats(qs)
    End Function

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
