Imports System.Numerics
Imports System.Text.Json.Serialization

Public Class Poses_class
    <JsonPropertyName("name")>
    Public Property Name As String

    <JsonPropertyName("skeleton")>
    Public Property Skeleton As String

    <JsonPropertyName("version")>
    Public Property Version As Integer

    <JsonPropertyName("transforms")>
    Public Property Transforms As Dictionary(Of String, PoseTransformData)

    Public Enum Pose_Source_Enum
        WardrobeManager
        BodySlide
        ScreenArcher
        None
    End Enum

    Public Overrides Function ToString() As String
        Return KeyName(Name, Source)
    End Function

    Public Shared Function KeyName(Name As String, sourceType As Pose_Source_Enum) As String
        Select Case sourceType
            Case Pose_Source_Enum.BodySlide
                Return Name + " (BodySlide pose)"
            Case Pose_Source_Enum.ScreenArcher
                Return Name + " (ScreenArcher pose)"
            Case Pose_Source_Enum.WardrobeManager, Pose_Source_Enum.None
                Return Name + " (Wardrobe Manager pose)"
            Case Else
                Return Name + " (Unknown pose)"
        End Select
    End Function

    <JsonIgnore>
    Public Property Source As Pose_Source_Enum = Pose_Source_Enum.ScreenArcher

    <JsonIgnore>
    Public Property Filename As String

    Public Function Clone() As Poses_class
        Dim Clon As New Poses_class With {
            .Name = "Unknown",
            .Skeleton = Skeleton,
            .Version = Version,
            .Source = Pose_Source_Enum.WardrobeManager,
            .Transforms = New Dictionary(Of String, PoseTransformData)
        }
        For Each tr In Transforms
            Dim rot As Vector3
            Dim Tras As Vector3
            Dim sc As Single
            If Source = Pose_Source_Enum.ScreenArcher Then
                Dim Converter = New Transform_Class(tr.Value, Source)
                Dim bon As Skeleton_Class.HierarchiBone_class = Nothing

                If Skeleton_Class.HasSkeleton AndAlso Skeleton_Class.SkeletonDictionary.TryGetValue(tr.Key, bon) Then
                    Converter = bon.OriginalLocaLTransform.Inverse.ComposeTransforms(Converter)
                End If
                rot = Transform_Class.Matrix33ToBSRotation(Converter.Rotation)
                Tras = New Vector3(Converter.Translation.X, Converter.Translation.Y, Converter.Translation.Z)
                sc = Converter.Scale
            Else
                rot = New Vector3(tr.Value.Yaw, tr.Value.Pitch, tr.Value.Roll)
                Tras = New Vector3(tr.Value.X, tr.Value.Y, tr.Value.Z)
                sc = tr.Value.Scale
            End If
            Dim cloned = New PoseTransformData With {.X = Tras.X, .Y = Tras.Y, .Z = Tras.Z, .Yaw = rot.X, .Pitch = rot.Y, .Roll = rot.Z, .Scale = sc}
            Clon.Transforms.Add(tr.Key, cloned)
        Next
        Return Clon
    End Function
End Class

Public Class PoseTransformData
    <JsonPropertyName("pitch")> Public Property Pitch As Single = 0
    <JsonPropertyName("roll")> Public Property Roll As Single = 0
    <JsonPropertyName("yaw")> Public Property Yaw As Single = 0
    <JsonPropertyName("x")> Public Property X As Single = 0
    <JsonPropertyName("y")> Public Property Y As Single = 0
    <JsonPropertyName("z")> Public Property Z As Single = 0
    <JsonPropertyName("scale")> Public Property Scale As Single = 1

    <JsonIgnore>
    Public ReadOnly Property Isidentity As Boolean
        Get
            Return X = 0 AndAlso Y = 0 AndAlso Z = 0 AndAlso Yaw = 0 AndAlso Pitch = 0 AndAlso Roll = 0 AndAlso Scale = 1
        End Get
    End Property
End Class
