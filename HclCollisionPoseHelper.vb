' Version Uploaded of Fo4Library 3.2.0
Option Strict On
Option Explicit On

' =============================================================================
' ESTADO: DEBUG / EN REVISIÓN — NO CERRADO
' -----------------------------------------------------------------------------
' Construye cápsulas de colisión vivas a partir de datos HKX + esqueleto activo.
'
' BUILT BUT NOT CONNECTED AL RENDER.
' Ningún caller activo en el proyecto.
'
' PENDIENTES CONOCIDOS:
'  - LocalReferencePoseToTransform y ResolveUniformScale: duplicadas aquí y en
'    SkeletonClothOverlayHelper.vb. Implementaciones idénticas (OpenTK Matrix4).
'    Candidatas a extraer a un módulo compartido.
'  - BuildReferenceSkeletonTransforms: asume que parentIndices siempre tienen
'    índices menores que el hijo (topological order). Correcto en Havok pero
'    sin validación explícita.
'  - ToMatrix4Local: sin validación de que Values tenga 16 elementos.
'    ReadMatrix4 ya garantiza esto en teoría, pero a revisar.
' =============================================================================

Imports System.Linq
Imports NiflySharp.Structs
Imports OpenTK.Mathematics

Public NotInheritable Class HclCollisionPoseHelper_Class
    ''' <param name="skeleton">SkeletonInstance to read live bone transforms from. If Nothing,
    ''' falls back to <see cref="SkeletonInstance.Default"/> (transitional back-compat).</param>
    Public Shared Function BuildCapsulesFromLiveSkeleton(config As HclClothConfigGraph_Class, Optional skeleton As SkeletonInstance = Nothing) As List(Of HclLiveCapsuleCollider_Class)
        Dim effectiveSkel As SkeletonInstance = If(skeleton, SkeletonInstance.Default)
        Dim lookup As New Dictionary(Of String, Transform_Class)(StringComparer.OrdinalIgnoreCase)
        For Each kvp In effectiveSkel.SkeletonDictionary
            If kvp.Value Is Nothing Then Continue For
            lookup(kvp.Key) = CloneTransformLocal(kvp.Value.GetGlobalTransform())
        Next
        Return BuildCapsules(config, lookup)
    End Function

    Public Shared Function BuildCapsulesFromReferenceSkeleton(config As HclClothConfigGraph_Class,
                                                              package As HclClothPackageGraph_Class) As List(Of HclLiveCapsuleCollider_Class)
        Return BuildCapsules(config, BuildReferenceSkeletonTransforms(package?.Skeleton))
    End Function

    Public Shared Function BuildCapsules(config As HclClothConfigGraph_Class,
                                         boneTransforms As IReadOnlyDictionary(Of String, Transform_Class)) As List(Of HclLiveCapsuleCollider_Class)
        Dim result As New List(Of HclLiveCapsuleCollider_Class)
        If config Is Nothing OrElse boneTransforms Is Nothing Then Return result

        For Each sim In config.SimClothDatas
            For Each binding In sim.CollidableBindings
                If binding?.Collidable?.ShapeDetail Is Nothing Then Continue For
                If String.IsNullOrWhiteSpace(binding.BoneName) Then Continue For

                Dim boneTransform As Transform_Class = Nothing
                If Not boneTransforms.TryGetValue(binding.BoneName, boneTransform) Then Continue For

                Dim world = CloneTransformLocal(boneTransform)
                If binding.Matrix IsNot Nothing AndAlso binding.Matrix.Values IsNot Nothing AndAlso binding.Matrix.Values.Length >= 16 Then
                    world = world.ComposeTransforms(New Transform_Class(ToMatrix4Local(binding.Matrix)))
                End If
                If binding.Collidable.TransformMatrix IsNot Nothing AndAlso binding.Collidable.TransformMatrix.Values IsNot Nothing AndAlso binding.Collidable.TransformMatrix.Values.Length >= 16 Then
                    world = world.ComposeTransforms(New Transform_Class(ToMatrix4Local(binding.Collidable.TransformMatrix)))
                End If

                Dim matrix = world.ToMatrix4d()
                Dim shape = binding.Collidable.ShapeDetail
                result.Add(New HclLiveCapsuleCollider_Class With {
                    .ConfigName = config.ClothData?.Name,
                    .BoneName = binding.BoneName,
                    .EndpointA = TransformPointLocal(shape.EndpointA, matrix),
                    .EndpointB = TransformPointLocal(shape.EndpointB, matrix),
                    .Radius = shape.Radius,
                    .AuxiliaryRadius = shape.AuxiliaryRadius
                })
            Next
        Next

        Return result
    End Function

    Private Shared Function BuildReferenceSkeletonTransforms(skeleton As HkaSkeletonGraph_Class) As Dictionary(Of String, Transform_Class)
        Dim result As New Dictionary(Of String, Transform_Class)(StringComparer.OrdinalIgnoreCase)
        If skeleton?.ReferencePose Is Nothing OrElse skeleton.ParentIndices Is Nothing OrElse skeleton.Bones Is Nothing Then Return result

        Dim globals As New List(Of Transform_Class)
        For i = 0 To skeleton.ReferencePose.Count - 1
            Dim localTransform = LocalReferencePoseToTransform(skeleton.ReferencePose(i))
            Dim globalTransform = CloneTransformLocal(localTransform)
            Dim parentIndex = If(i < skeleton.ParentIndices.Count, CInt(skeleton.ParentIndices(i)), -1)
            If parentIndex >= 0 AndAlso parentIndex < globals.Count Then
                globalTransform = globals(parentIndex).ComposeTransforms(localTransform)
            End If
            globals.Add(CloneTransformLocal(globalTransform))

            Dim boneName = If(i < skeleton.Bones.Count, skeleton.Bones(i).Name, String.Empty)
            If Not String.IsNullOrWhiteSpace(boneName) Then
                result(boneName) = CloneTransformLocal(globalTransform)
            End If
        Next

        Return result
    End Function

    Private Shared Function LocalReferencePoseToTransform(source As HkxQsTransformGraph_Class) As Transform_Class
        If source Is Nothing Then Return New Transform_Class()

        Dim scale = ResolveUniformScale(source.Scale)
        Dim rotation As Quaternion
        If source.Rotation Is Nothing Then
            rotation = Quaternion.Identity
        Else
            rotation = New Quaternion(source.Rotation.X, source.Rotation.Y, source.Rotation.Z, source.Rotation.W)
            If rotation.LengthSquared <= 0.000001F Then
                rotation = Quaternion.Identity
            Else
                rotation = Quaternion.Normalize(rotation)
            End If
        End If

        Dim matrix = Matrix4.CreateScale(scale) * Matrix4.CreateFromQuaternion(rotation) * Matrix4.CreateTranslation(source.Translation.X, source.Translation.Y, source.Translation.Z)
        Return New Transform_Class(matrix)
    End Function

    Private Shared Function ResolveUniformScale(scale As HkxVector4Graph_Class) As Single
        If scale Is Nothing Then Return 1.0F
        Dim values = {scale.X, scale.Y, scale.Z}.Where(Function(value) Single.IsFinite(value) AndAlso Math.Abs(value) > 0.000001F).ToArray()
        If values.Length = 0 Then Return 1.0F
        Return CSng(values.Average())
    End Function

    Private Shared Function ToMatrix4Local(source As HkxMatrix4Graph_Class) As Matrix4
        Return New Matrix4(source.Values(0), source.Values(1), source.Values(2), source.Values(3),
                           source.Values(4), source.Values(5), source.Values(6), source.Values(7),
                           source.Values(8), source.Values(9), source.Values(10), source.Values(11),
                           source.Values(12), source.Values(13), source.Values(14), source.Values(15))
    End Function

    Private Shared Function TransformPointLocal(source As HkxVector4Graph_Class, matrix As Matrix4d) As System.Numerics.Vector3
        If source Is Nothing Then Return New System.Numerics.Vector3()
        Dim point = Vector3d.TransformPosition(New Vector3d(source.X, source.Y, source.Z), matrix)
        Return New System.Numerics.Vector3(CSng(point.X), CSng(point.Y), CSng(point.Z))
    End Function

    Private Shared Function CloneTransformLocal(source As Transform_Class) As Transform_Class
        If source Is Nothing Then Return New Transform_Class()
        Return New Transform_Class(source.ToMatrix4d())
    End Function
End Class

Public Class HclLiveCapsuleCollider_Class
    Public Property ConfigName As String
    Public Property BoneName As String
    Public Property EndpointA As System.Numerics.Vector3
    Public Property EndpointB As System.Numerics.Vector3
    Public Property Radius As Single
    Public Property AuxiliaryRadius As Single
End Class