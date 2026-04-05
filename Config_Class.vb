' Version Uploaded of Wardrobe 3.1.0
Imports System.IO
Imports System.Numerics
Imports System.Text.Json
Imports FO4_Base_Library.RecalcTBN
Public Class Config_App
    Public Structure LightData_struct
        Public Property Strength As Single

        ' Multiplicadores relativos a la base de cámara
        Public Property Up As Single
        Public Property Down As Single
        Public Property Left As Single
        Public Property Right As Single
        Public Property Forward As Single   ' hacia cámara
        Public Property Back As Single      ' opuesto a cámara

        ''' <summary>
        ''' Calcula el vector de dirección de la luz usando la orientación actual de la cámara orbital.
        ''' </summary>
        Public Function GetDirection(cam As OrbitCamera) As OpenTK.Mathematics.Vector3
            ' Eje Up mundial
            Dim upVec As New OpenTK.Mathematics.Vector3(0, 0, 1)

            ' Eje Right relativo a la cámara
            Dim rightVec As OpenTK.Mathematics.Vector3 = OpenTK.Mathematics.Vector3.Normalize(OpenTK.Mathematics.Vector3.Cross(cam.Forward, upVec))

            ' Forward de la cámara
            Dim forwardVec As OpenTK.Mathematics.Vector3 = cam.Forward

            ' Combinación ponderada de ejes
            Dim dir As OpenTK.Mathematics.Vector3 = (Up - Down) * upVec + (Right - Left) * rightVec + (Forward - Back) * forwardVec

            ' Si el vector es muy pequeño, usar forward para evitar NaN
            If dir.LengthSquared() < 0.00000001F Then
                dir = forwardVec
            End If

            Return OpenTK.Mathematics.Vector3.Normalize(dir)
        End Function
        Public Function GetDifuse() As OpenTK.Mathematics.Vector3
            Return New OpenTK.Mathematics.Vector3(Strength)
        End Function

    End Structure
    Public Structure LightsRig_struct
        Public Property DirectL As LightData_struct
        Public Property FillLight_1 As LightData_struct
        Public Property FillLight_2 As LightData_struct
        Public Property BackLight As LightData_struct
        Public Property Ambient As Single

    End Structure


    Public Structure CameraSettings
        Public Property ResetAngles As Boolean
        Public Property ResetZoom As Boolean
        Public Property FreezeCamera As Boolean

    End Structure
    Public Structure BuildSettings
        Public Property OwnEngine As Boolean
        Public Property SaveTri As Boolean
        Public Property SaveHHS As Boolean
        Public Property DeleteUnbuilt As Boolean
        Public Property DeleteWithProject As Boolean
        Public Property AddAddintionalSliders As Boolean
        Public Property SkipManoloFixMorphs As Boolean
        Public Property ResetSlidersEachBuild As Boolean
        Public Property IgnorePreventri As Boolean
        Public Property BuildInPose As Boolean
        Public Property IgnoreWeightsFlags As Boolean
        Public Property ForceWeights As Boolean
    End Structure
    Public Structure RenderGridSettings
        Public Property Size As Single
        Public Property Enabled As Boolean
        Public Property StepSize As Single
    End Structure

    Public Property FO4ExePath As String = ""
    Public ReadOnly Property FO4EDataPath As String
        Get
            If Check_FOFolder() = False Then Return ""
            Return IO.Path.Combine(IO.Path.GetDirectoryName(FO4ExePath), "Data")
        End Get
    End Property
    Public ReadOnly Property DataPath As String
        Get
            Return FO4EDataPath
        End Get
    End Property
    Public ReadOnly Property SkeletonFilePath As String
        Get
            If SkeletonPath = "" Then Return ""
            Return SkeletonPath
        End Get
    End Property
    Public ReadOnly Property BsPath As String
        Get
            If Check_BSFolder() = False Then Return ""
            Return IO.Path.Combine(IO.Path.GetDirectoryName(BSExePath))
        End Get
    End Property
    Public Enum Game_Enum
        Fallout4 = 0
        Skyrim = 1
    End Enum
    Public Enum SliderSize
        [Default] = 0
        Big = 1
        Small = 2
    End Enum
    Public Property Game As Game_Enum = Game_Enum.Skyrim
    Public Property Bodytipe As SliderSize = SliderSize.Default

    Public Property BSExePath As String = ""
    Public Property SkeletonPath As String = ""
    Public Property OSExePath As String = ""
    ' Per-game BSA/BA2 cloning permissions persisted separately
    Public Property BSAFiles_FO4 As New List(Of String)
    Public Property BSAFiles_Clonables_FO4 As New List(Of Boolean)
    Public Property BSAFiles_SSE As New List(Of String)
    Public Property BSAFiles_Clonables_SSE As New List(Of Boolean)

    <System.Text.Json.Serialization.JsonIgnore>
    Public ReadOnly Property BSAFiles As List(Of String)
        Get
            Return If(Game = Game_Enum.Fallout4, BSAFiles_FO4, BSAFiles_SSE)
        End Get
    End Property

    <System.Text.Json.Serialization.JsonIgnore>
    Public ReadOnly Property BSAFiles_Clonables As List(Of Boolean)
        Get
            Return If(Game = Game_Enum.Fallout4, BSAFiles_Clonables_FO4, BSAFiles_Clonables_SSE)
        End Get
    End Property
    Public Property Default_Preset As String = ""
    Public Property Setting_OverWrite As Boolean = False
    Public Property Setting_SingleBoneSkinning As Boolean = False
    Public Property Setting_GPUSkinning As Boolean = True
    Public Property Setting_RecalculateNormals As Boolean = True
    Public Property Setting_KeepPhysics As Boolean = True
    Public Property Setting_ChangeOutDir As Boolean = False
    Public Property Setting_Automove As Boolean = False
    Public Property Setting_ExcludeReference As Boolean = False
    Public Property Setting_Clone_Materials As Boolean = True
    Public Property Setting_Showpacks As Boolean = False
    Public Property Setting_ShowCBBE As Boolean = True
    Public Property Setting_ShowCollections As Boolean = True
    Public Property Setting_ExportSam As Boolean = False
    Public Property theme As AppTheme = AppTheme.Light

    Public Property Setting_Lightrig As LightsRig_struct = Default_Lights()
    Private _color As Color = Color.DarkGray
    Private _colorGrod As Color = Color.LightGray

    Public Function Setting_BackColor() As Color
        If _color = Color.Empty Then _color = Color.FromName(Setting_BackColorName)
        Return _color
    End Function
    Public Function RenderGridColor() As Color
        If _colorGrod = Color.Empty Then _colorGrod = Color.FromName(Setting_RenderGridColor)
        Return _colorGrod
    End Function
    Public Property Setting_RenderGridColor As String
        Get
            Return _colorGrod.Name
        End Get
        Set(value As String)
            _colorGrod = Color.FromName(value)
        End Set
    End Property
    Public Property Setting_BackColorName As String
        Get
            Return _color.Name
        End Get
        Set(value As String)
            _color = Color.FromName(value)
        End Set
    End Property

    Public Property Settings_Camara As CameraSettings = Default_CameraSettings()
    Public Property Settings_Build As BuildSettings = Default_Build_Settings()
    Public Property Settings_RenderGrid As RenderGridSettings = Default_RenderGrid_Settings()
    Public Shared Function Default_RenderGrid_Settings() As RenderGridSettings
        Return New RenderGridSettings With {.Enabled = False, .Size = 400, .StepSize = 10}
    End Function
    Public Shared Function Default_Build_Settings() As BuildSettings
        Return New BuildSettings With {.DeleteUnbuilt = True, .DeleteWithProject = True, .SaveHHS = True, .SaveTri = False, .OwnEngine = True, .AddAddintionalSliders = True, .ResetSlidersEachBuild = False, .SkipManoloFixMorphs = True, .IgnorePreventri = False, .BuildInPose = False, .ForceWeights = True, .IgnoreWeightsFlags = False}
    End Function
    Public Shared Function Default_CameraSettings() As CameraSettings
        Return New CameraSettings With {.ResetAngles = True, .ResetZoom = True, .FreezeCamera = False}
    End Function
    Public Shared Function Default_Lights() As LightsRig_struct
        Dim Lrig = New LightsRig_struct With {.Ambient = 0.2,
            .DirectL = New LightData_struct With {.Strength = 0.7F, .Left = 0, .Right = 0, .Back = 0, .Down = 0, .Forward = 1, .Up = 0},
            .FillLight_1 = New LightData_struct With {.Strength = 0.6F, .Left = 0, .Right = 0.7, .Back = 0, .Down = 0, .Forward = 0.7, .Up = 0.7},
            .FillLight_2 = New LightData_struct With {.Strength = 0.6, .Left = 0.7, .Right = 0, .Back = 0, .Down = 0, .Forward = 0.7, .Up = 0.7},
            .BackLight = New LightData_struct With {.Strength = 0.6F, .Left = 0.0, .Right = 0, .Back = 1, .Down = 0, .Forward = 0, .Up = 0.5}}
        Return Lrig
    End Function

    Public Property Setting_TBN As TBNOptions = DefaultTBNOptions()

    ' Instancia única accesible desde cualquier parte
    Public Shared Property Current As Config_App = New Config_App()

    ' Ruta fija al archivo de configuración en la carpeta de la aplicación
    Private Shared ReadOnly ConfigFilePath As String = Path.Combine(Application.StartupPath, "config.json")

    Public Sub New()
        Try
            If FO4ExePath = "" Then
                FO4ExePath = IO.Path.Combine(IO.Path.GetDirectoryName(IO.Path.GetDirectoryName(IO.Path.GetDirectoryName(IO.Path.GetDirectoryName(Application.ExecutablePath)))), "Fallout4.exe")
                If IO.File.Exists(FO4ExePath) AndAlso IO.Directory.Exists(IO.Path.Combine(IO.Path.GetDirectoryName(FO4ExePath), "Data")) Then
                    If Environment.Is64BitOperatingSystem Then
                        BSExePath = IO.Path.Combine(IO.Path.GetDirectoryName(FO4ExePath), "Data\Tools\Bodyslide\BodySlide x64.exe")
                    Else
                        BSExePath = IO.Path.Combine(IO.Path.GetDirectoryName(FO4ExePath), "Data\Tools\Bodyslide\BodySlide.exe")
                    End If
                End If
                If IO.File.Exists(FO4ExePath) AndAlso IO.Directory.Exists(IO.Path.Combine(IO.Path.GetDirectoryName(FO4ExePath), "Data")) Then
                    If Environment.Is64BitOperatingSystem Then
                        OSExePath = IO.Path.Combine(IO.Path.GetDirectoryName(FO4ExePath), "Data\Tools\Bodyslide\OutfitStudio x64.exe")
                    Else
                        OSExePath = IO.Path.Combine(IO.Path.GetDirectoryName(FO4ExePath), "Data\Tools\Bodyslide\OutfitStudio.exe")
                    End If
                End If
            End If
        Catch ex As Exception

        End Try
    End Sub

    Public Shared Function Allowed_To_Clone(Ba2File) As Boolean
        Dim idx = Current.BSAFiles.FindIndex(Function(s) String.Equals(s, IO.Path.GetFileName(Ba2File), StringComparison.OrdinalIgnoreCase))
        If idx = -1 Then Return False
        Return Current.BSAFiles_Clonables(idx)
    End Function

    Private Shared ReadOnly SaveOptions As New JsonSerializerOptions With {.WriteIndented = True}
    Public Shared Sub SaveConfig()
        Try

            Dim jsonString As String = JsonSerializer.Serialize(Current, SaveOptions)
            IO.File.WriteAllText(ConfigFilePath, jsonString)
        Catch ex As Exception
            MessageBox.Show("Error saving configuration: " & ex.Message,
                            "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Public Shared Sub LoadConfig()
        Try
            If IO.File.Exists(ConfigFilePath) Then
                Dim jsonString As String = IO.File.ReadAllText(ConfigFilePath)
                Dim cfg As Config_App = JsonSerializer.Deserialize(Of Config_App)(jsonString)
                If cfg IsNot Nothing Then
                    Current = cfg
                    If Current.Settings_RenderGrid.Size = 0 Then Current.Settings_RenderGrid = Default_RenderGrid_Settings()
                End If
            End If
        Catch ex As Exception
            MessageBox.Show("Error loading configuration: " & ex.Message,
                            "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Public Shared Function Check_FOFolder() As Boolean
        If IO.File.Exists(Current.FO4ExePath) = False Then Return False
        If IO.Directory.Exists(IO.Path.Combine(IO.Path.GetDirectoryName(Current.FO4ExePath), "Data")) = False Then Return False
        Return True
    End Function
    Public Shared Function Check_BSFolder() As Boolean
        If IO.File.Exists(Current.BSExePath) = False Then Return False
        If IO.Directory.Exists(IO.Path.Combine(IO.Path.GetDirectoryName(Current.BSExePath), "Slidersets")) = False Then Return False
        Return True
    End Function
    Public Shared Function Check_OsFolder() As Boolean
        If IO.File.Exists(Current.OSExePath) = False Then Return False
        If IO.Directory.Exists(IO.Path.Combine(IO.Path.GetDirectoryName(Current.OSExePath), "Shapedata")) = False Then Return False
        Return True
    End Function
    Public Shared Function Check_Skeleton() As Boolean
        If IO.File.Exists(Current.SkeletonPath) = False Then Return False
        Return True
    End Function

    Public Shared Function Check_All_Folder() As Boolean
        Return Check_OsFolder() And Check_FOFolder() And Check_BSFolder()
    End Function
End Class
