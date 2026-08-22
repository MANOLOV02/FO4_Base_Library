Imports System.Numerics
Imports System.Text.Json.Serialization
Imports System.Xml.Linq

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
            ' Parte PER-EJE de la escala. Se clona igual que las demas: antes se caia aca en silencio,
            ' asi que previsualizar (que usa el original) y guardar (que usa el clon) daban poses
            ' distintas en cuanto un HKX traia escala no uniforme. Ley de Transform_Class:
            ' escala efectiva = Scale (uniforme) * ScaleVector (per-eje), campos DISJUNTOS.
            Dim svx As Single = 1, svy As Single = 1, svz As Single = 1
            If Source = Pose_Source_Enum.ScreenArcher Then
                Dim Converter = New Transform_Class(tr.Value, Source)
                Dim bon As HierarchiBone_class = Nothing

                If SkeletonInstance.Default.HasSkeleton AndAlso SkeletonInstance.Default.SkeletonDictionary.TryGetValue(tr.Key, bon) Then
                    Converter = bon.OriginalLocaLTransform.Inverse.ComposeTransforms(Converter)
                End If
                rot = Transform_Class.Matrix33ToBSRotation(Converter.Rotation)
                Tras = New Vector3(Converter.Translation.X, Converter.Translation.Y, Converter.Translation.Z)
                sc = Converter.Scale
                svx = Converter.ScaleVector.X
                svy = Converter.ScaleVector.Y
                svz = Converter.ScaleVector.Z
            Else
                rot = New Vector3(tr.Value.Yaw, tr.Value.Pitch, tr.Value.Roll)
                Tras = New Vector3(tr.Value.X, tr.Value.Y, tr.Value.Z)
                sc = tr.Value.Scale
                svx = tr.Value.ScaleX
                svy = tr.Value.ScaleY
                svz = tr.Value.ScaleZ
            End If
            Dim cloned = New PoseTransformData With {.X = Tras.X, .Y = Tras.Y, .Z = Tras.Z, .Yaw = rot.X, .Pitch = rot.Y, .Roll = rot.Z, .Scale = sc,
                                                    .ScaleX = svx, .ScaleY = svy, .ScaleZ = svz}
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
    <JsonIgnore> Public Property ScaleX As Single = 1
    <JsonIgnore> Public Property ScaleY As Single = 1
    <JsonIgnore> Public Property ScaleZ As Single = 1

    ''' <summary>True cuando la escala es uniforme, o sea cuando `ScaleX/Y/Z` no aportan nada y el
    ''' `scale` solo alcanza para describirla.</summary>
    <JsonIgnore>
    Public ReadOnly Property EscalaEsUniforme As Boolean
        Get
            Return ScaleX = 1 AndAlso ScaleY = 1 AndAlso ScaleZ = 1
        End Get
    End Property

    ''' <summary>Los atributos PER-EJE del &lt;Bone&gt; del XML de poses. Nombres propios de esta app:
    ''' BodySlide/OutfitStudio lee sus atributos POR NOMBRE (`FloatAttribute(""scale"", 1.0f)`,
    ''' PoseData.cpp) y su escala es un solo float (`float poseScale`, Anim.h), asi que estos tres le
    ''' son invisibles y el archivo compartido sigue siendo valido para el.
    ''' <para>⚠️ NO SOBREVIVEN a un guardado hecho DESDE BodySlide: su escritor reescribe cada &lt;Bone&gt;
    ''' con `SetAttribute` de los atributos que el conoce, asi que se pierden. Es aceptable — el `scale`
    ''' uniforme, que es lo unico que el entiende, sigue ahi — pero hay que saberlo.</para>
    ''' <para>Se emiten SOLO si la escala es per-eje, para que una pose uniforme escriba el MISMO XML
    ''' de siempre y no ensucie el diff del archivo compartido.</para></summary>
    Public Function AtributosPerEje() As XAttribute()
        If EscalaEsUniforme Then Return New XAttribute() {}
        Dim ci = Globalization.CultureInfo.InvariantCulture
        Return New XAttribute() {
            New XAttribute("scaleX", ScaleX.ToString(ci)),
            New XAttribute("scaleY", ScaleY.ToString(ci)),
            New XAttribute("scaleZ", ScaleZ.ToString(ci))}
    End Function

    ''' <summary>Lee los tres atributos per-eje si estan; si no, deja el neutro (1,1,1) y la pose queda
    ''' exactamente como la leia antes. Un XML viejo (o reescrito por BodySlide) entra por este camino.</summary>
    Public Sub LeerPerEje(bone As XElement)
        If bone Is Nothing Then Return
        ScaleX = LeerEje(bone, "scaleX")
        ScaleY = LeerEje(bone, "scaleY")
        ScaleZ = LeerEje(bone, "scaleZ")
    End Sub

    Private Shared Function LeerEje(bone As XElement, nombre As String) As Single
        Dim a = bone.Attribute(nombre)
        If a Is Nothing Then Return 1.0F
        Dim v As Single
        If Single.TryParse(a.Value, Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture, v) Then Return v
        Return 1.0F
    End Function

    <JsonIgnore>
    Public ReadOnly Property Isidentity As Boolean
        Get
            Return X = 0 AndAlso Y = 0 AndAlso Z = 0 AndAlso Yaw = 0 AndAlso Pitch = 0 AndAlso Roll = 0 AndAlso
                   Scale = 1 AndAlso ScaleX = 1 AndAlso ScaleY = 1 AndAlso ScaleZ = 1
        End Get
    End Property
End Class
