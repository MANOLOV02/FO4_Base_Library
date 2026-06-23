' Version Uploaded of Fo4Library 3.2.0
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
        ''' <summary>Tinte RGB de la luz (0..1). Default/legacy (config viejo sin la key) = (0,0,0),
        ''' que <see cref="GetDifuse"/> trata como blanco -&gt; preserva el comportamiento gris×Strength.</summary>
        Public Property Tint As Vector3

        Public Function GetDifuse() As OpenTK.Mathematics.Vector3
            ' Tint (0,0,0) = legacy/unset -> blanco (luz gris neutra). Para "apagar" una luz usar Strength=0.
            Dim t = Tint
            If t.X = 0 AndAlso t.Y = 0 AndAlso t.Z = 0 Then t = New Vector3(1, 1, 1)
            Return New OpenTK.Mathematics.Vector3(t.X, t.Y, t.Z) * Strength
        End Function

    End Structure
    Public Structure LightsRig_struct
        Public Property DirectL As LightData_struct
        Public Property FillLight_1 As LightData_struct
        Public Property FillLight_2 As LightData_struct
        Public Property BackLight As LightData_struct
        ''' <summary>Intensidad global del ambient (legacy). Sigue siendo el control de fuerza; el color
        ''' del hemisferio lo dan <see cref="AmbientSky"/>/<see cref="AmbientGround"/>.</summary>
        Public Property Ambient As Single
        ''' <summary>Ambient HEMISFÉRICO engine-faithful (FO4/SSE usan ambient dependiente de la normal,
        ''' no plano): color que recibe la superficie cuando su normal apunta hacia ARRIBA (world +Z).
        ''' Default/legacy (0,0,0) -&gt; Render lo deriva del escalar <see cref="Ambient"/>.</summary>
        Public Property AmbientSky As Vector3
        ''' <summary>TINTE (hue) del ambient cuando la normal apunta hacia ABAJO. El BRILLO del suelo
        ''' lo da <see cref="AmbientGroundLevel"/>, no este color (default blanco = neutro).</summary>
        Public Property AmbientGround As Vector3
        ''' <summary>HEMISFERIO: brillo del suelo (normal hacia abajo) como fracción del cielo, 0..1.
        ''' 1 = ambient plano (suelo = cielo); 0.5 = suelo a la mitad; 0 = suelo negro. Independiente de
        ''' la intensidad (<see cref="Ambient"/>) y del tinte. Default 0.5. Legacy (=0) -&gt; <see cref="NormalizeAmbient"/>.</summary>
        Public Property AmbientGroundLevel As Single
        ''' <summary>True una vez que el ambient se guardó en el modelo de 3 perillas. Distingue
        ''' "usuario puso groundLevel=0" (válido, suelo negro) de "campo ausente en config viejo".
        ''' False por default (JSON sin la key) -&gt; <see cref="NormalizeAmbient"/> migra una vez.</summary>
        Public Property AmbientConfigured As Boolean

    End Structure


    Public Structure CameraSettings
        Public Property ResetAngles As Boolean
        Public Property ResetZoom As Boolean
        Public Property FreezeCamera As Boolean
    End Structure
    ' BuildSettings struct moved to WM_Config
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
    ' BsPath, SliderSize enum, Bodytipe, BSExePath, OSExePath moved to WM_Config
    Public Enum Game_Enum
        Fallout4 = 0
        Skyrim = 1
    End Enum
    Public Property Game As Game_Enum = Game_Enum.Skyrim
    Public Property SkeletonPath As String = ""
    ' BSAFiles, BSAFiles_Clonables, Allowed_To_Clone, and all WM-only settings moved to WM_Config
    Public Property Setting_SingleBoneSkinning As Boolean = False
    Public Property Setting_GPUSkinning As Boolean = True
    ' WM inspection toggle: when True, EnsureZapIndexBuffer bypasses per-segment occlusion so all geometry
    ' draws. Default TRUE = "draw everything" (the neutral renderer default; WM wants it ON, and an existing
    ' WM config without the key deserializes to this default → ON, while a saved True/False is respected).
    ' FO4_NPC_Manager FORCES this False at startup (Program/MainForm) because its render RELIES on the
    ' per-segment occlusion (Pip-Boy 60/160 swap, head-part hiding) — see the "= False" there.
    Public Property Setting_DrawHiddenSegments As Boolean = True
    Public Property Setting_RecalculateNormals As Boolean = True
    Public Property Setting_KeepPhysics As Boolean = True
    Public Property theme As AppTheme = AppTheme.Light

    ' === CharGen / FaceGen bake output settings (botón "CharGen Options") ===
    ' Tamaño del bake + compresión del diffuse de salida. Persistidos junto al resto del config
    ' (config.json). Default = All + Inherit + BC3 = comportamiento actual / byte-comparable a gen3.
    ' Lógica de tamaño:
    '   Setting_FaceGenPerLayerResolution = False (ALL, default): los 3 canales usan el tamaño Diffuse
    '       (N/S heredan de Diffuse, deshabilitados en la UI). Cubre "heredar las 3" (Diffuse=Inherit) y
    '       "unificar a X" (Diffuse=enum).
    '   = True (PER LAYER): cada canal usa su propio tamaño (los 3 habilitados en la UI).
    ' Tamaño por canal: Inherit (MIP0 nativo, sin downgrade) o un enum (512/1024/2048/4096/8192).
    ' Compresión por canal (misma sincronía All/Per-layer que el tamaño): Diffuse BC3(default)/BC7/Uncompressed,
    ' N/S BC5(default)/Uncompressed. En All, N/S siguen al Diffuse (Uncompressed si lo es, sino BC5).
    Public Property Setting_FaceGenPerLayerResolution As Boolean = False
    Public Property Setting_FaceGenDiffuseResolution As FaceTintConvention.FaceTintChannelResolution = FaceTintConvention.FaceTintChannelResolution.Inherit
    Public Property Setting_FaceGenNormalResolution As FaceTintConvention.FaceTintChannelResolution = FaceTintConvention.FaceTintChannelResolution.Inherit
    Public Property Setting_FaceGenSpecularResolution As FaceTintConvention.FaceTintChannelResolution = FaceTintConvention.FaceTintChannelResolution.Inherit
    Public Property Setting_FaceGenDiffuseCompression As FaceTintConvention.FaceTintDiffuseCompression = FaceTintConvention.FaceTintDiffuseCompression.Bc3
    ' Compresión N/S: BC5 default / Uncompressed. En modo All siguen al Diffuse (Uncompressed si el Diffuse
    ' lo es, sino BC5); en Per layer cada uno el suyo. GenerateTga = tilde del diálogo (TGA uncompressed al
    ' lado de cada .dds). Defaults: BC3 / BC5 / no TGA.
    Public Property Setting_FaceGenNormalCompression As FaceTintConvention.FaceTintNormalSpecularCompression = FaceTintConvention.FaceTintNormalSpecularCompression.Bc5
    Public Property Setting_FaceGenSpecularCompression As FaceTintConvention.FaceTintNormalSpecularCompression = FaceTintConvention.FaceTintNormalSpecularCompression.Bc5
    Public Property Setting_FaceGenGenerateTga As Boolean = False

    ' === FaceTint convention (botón "CharGen Options" → tab "FaceTint Conventions") ===
    ' La convención de composición FaceTint por bucket (Diffuse / Normal+Specular / Swaps), valores
    ' CONCRETOS. Los defaults los pone el constructor de FaceTintConventionSettings = la ley derivada
    ' (byte-match con CK si no se tocan). El usuario los edita acá o desde la UI y ESOS pasan a ser la ley:
    ' FaceTintConvention.ResolveConvention los lee SIEMPRE. Blend NO está (record-driven / Replace, read-only).
    ' Un config.json viejo sin la key deserializa al default del constructor.
    Public Property Setting_FaceTintConvention As New FaceTintConvention.FaceTintConventionSettings()

    ' === FaceTint sort order (botón "CharGen Options" → tab "Tint Order") ===
    ' Orden de composición configurable (multi-clave asc/desc) de tints y swaps + placement del SkinTone.
    ' Default = comportamiento previo (tints PhysIndex desc, swaps forward, skintone Positional); editar
    ' acá o en la UI cambia el orden con que el builder compone las capas. Ver FaceTintSortSettings.
    Public Property Setting_FaceTintSort As New FaceTintSortSettings()
    ' (El compositor GPU/CPU NO es una preferencia persistida: es una REGLA derivada — render = GPU si
    '  skinning=GPU, sino CPU ; chargen = siempre CPU (async, no toca GL). Ver FaceGenBuilder.)

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
    ' Settings_Build moved to WM_Config
    Public Property Settings_RenderGrid As RenderGridSettings = Default_RenderGrid_Settings()
    Public Shared Function Default_RenderGrid_Settings() As RenderGridSettings
        Return New RenderGridSettings With {.Enabled = False, .Size = 400, .StepSize = 10}
    End Function
    ' Default_Build_Settings moved to WM_Config
    Public Shared Function Default_CameraSettings() As CameraSettings
        Return New CameraSettings With {.ResetAngles = True, .ResetZoom = True, .FreezeCamera = False}
    End Function
    Public Shared Function Default_Lights() As LightsRig_struct
        Dim white As New Vector3(1, 1, 1)
        ' Ambient = intensidad global (slider). AmbientGroundLevel = hemisferio (brillo del suelo vs cielo,
        ' 0.5 = mitad). AmbientSky/Ground = solo TINTE (blanco = neutro). Tres perillas independientes.
        Dim Lrig = New LightsRig_struct With {.Ambient = 0.2,
            .AmbientSky = white,
            .AmbientGround = white,
            .AmbientGroundLevel = 0.5F,
            .AmbientConfigured = True,
            .DirectL = New LightData_struct With {.Strength = 0.7F, .Tint = white, .Left = 0, .Right = 0, .Back = 0, .Down = 0, .Forward = 1, .Up = 0},
            .FillLight_1 = New LightData_struct With {.Strength = 0.6F, .Tint = white, .Left = 0, .Right = 0.7, .Back = 0, .Down = 0, .Forward = 0.7, .Up = 0.7},
            .FillLight_2 = New LightData_struct With {.Strength = 0.6, .Tint = white, .Left = 0.7, .Right = 0, .Back = 0, .Down = 0, .Forward = 0.7, .Up = 0.7},
            .BackLight = New LightData_struct With {.Strength = 0.6F, .Tint = white, .Left = 0.0, .Right = 0, .Back = 1, .Down = 0, .Forward = 0, .Up = 0.5}}
        Return Lrig
    End Function

    ''' <summary>
    ''' Normaliza el ambient al modelo de 3 perillas (intensity + groundLevel + tinte). Migra configs
    ''' previos: si <see cref="LightsRig_struct.AmbientGroundLevel"/> &lt;= 0 (campo ausente), deriva el
    ''' level del BRILLO del color ground del modelo intermedio (o 0.5 legacy) y deja los tintes en blanco;
    ''' un tinte (0,0,0) ausente -&gt; blanco. Idempotente (no toca un rig ya normalizado). Trabaja sobre el
    ''' struct pasado ByRef (es value-type; el caller decide si persistir).
    ''' </summary>
    Public Shared Sub NormalizeAmbient(ByRef rig As LightsRig_struct)
        If rig.AmbientConfigured Then Return   ' ya en el modelo nuevo -> respetar todo (incl. groundLevel=0)
        Dim g = rig.AmbientGround
        If g.X = 0 AndAlso g.Y = 0 AndAlso g.Z = 0 Then
            rig.AmbientGroundLevel = 0.5F
        Else
            ' modelo intermedio: el brillo del suelo vivía en el color -> pasarlo al level
            rig.AmbientGroundLevel = (g.X + g.Y + g.Z) / 3.0F
        End If
        rig.AmbientGround = New Vector3(1, 1, 1)
        If rig.AmbientSky.X = 0 AndAlso rig.AmbientSky.Y = 0 AndAlso rig.AmbientSky.Z = 0 Then
            rig.AmbientSky = New Vector3(1, 1, 1)
        End If
        rig.AmbientConfigured = True
    End Sub

    Public Property Setting_TBN As TBNOptions = DefaultTBNOptions()

    ' Instancia única accesible desde cualquier parte
    Public Shared Property Current As Config_App = New Config_App()

    ' Ruta fija al archivo de configuración en la carpeta de la aplicación
    Private Shared ReadOnly ConfigFilePath As String = Path.Combine(Application.StartupPath, "config.json")

    Public Sub New()
        Try
            If FO4ExePath = "" Then
                FO4ExePath = IO.Path.Combine(IO.Path.GetDirectoryName(IO.Path.GetDirectoryName(IO.Path.GetDirectoryName(IO.Path.GetDirectoryName(Application.ExecutablePath)))), "Fallout4.exe")
            End If
            ' BS/OS auto-detection moved to WM_Config.AutoDetectBSPaths()
        Catch ex As Exception
        End Try
    End Sub

    ' Allowed_To_Clone moved to WM_Config

    Public Shared Sub SaveConfig()
        JsonConfigIO.Save(Current, ConfigFilePath, "configuration")
    End Sub

    Public Shared Sub LoadConfig()
        Dim cfg = JsonConfigIO.Load(Of Config_App)(ConfigFilePath, "configuration")
        If cfg IsNot Nothing Then
            Current = cfg
            If Current.Settings_RenderGrid.Size = 0 Then Current.Settings_RenderGrid = Default_RenderGrid_Settings()
        End If
    End Sub

    Public Shared Function Check_FOFolder() As Boolean
        If IO.File.Exists(Current.FO4ExePath) = False Then Return False
        If IO.Directory.Exists(IO.Path.Combine(IO.Path.GetDirectoryName(Current.FO4ExePath), "Data")) = False Then Return False
        Return True
    End Function
    ' Check_BSFolder, Check_OsFolder, Check_All_Folder moved to WM_Config
    Public Shared Function Check_Skeleton() As Boolean
        Return IO.File.Exists(Current.SkeletonPath)
    End Function
End Class
