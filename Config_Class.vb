' Version Uploaded of Fo4Library 3.2.0
Imports System.IO
Imports System.Numerics
Imports System.Text.Json
Imports FO4_Base_Library.RecalcTBN
Public Class Config_App
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

    ''' <summary>El rig de luces gira CON la cámara en vez de quedar fijo al mundo.
    ''' <para>⚠️ EL DEFAULT ES <b>True</b> — decisión del usuario, 2026-08-11. O sea que NO es el
    ''' comportamiento histórico: un usuario existente cuyo config no traiga la clave va a ver el rig
    ''' acompañar a la cámara la primera vez que abra esta versión. Es intencional; queda dicho acá porque
    ''' es la clase de cambio que después se reporta como "se me movieron las luces solas".</para>
    ''' <para>⭐ Son dos modelos y sirven para cosas distintas. <b>Fijo al mundo</b> (False) es lo que hace el
    ''' motor: la luz está en la escena, orbitar gira al personaje DENTRO de la luz y le ves la espalda a
    ''' contraluz — que es lo que hay que juzgar si querés saber cómo va a verse en el juego. <b>Pegado a la
    ''' cámara</b> (True) es lo que hacen los visores de malla (Substance, Marmoset, el viewport de Blender):
    ''' la luz te acompaña y el modelo se ve siempre igual de iluminado, que es lo que querés para inspeccionar
    ''' una prenda. Por eso es una opción y no una decisión.</para>
    ''' <para>⛔ CON FALSE NO SE EJECUTA UNA SOLA LÍNEA NUEVA. La rama que rota las direcciones está detrás de
    ''' este flag en <c>ResolveFrameLights</c>, así que la paridad con el comportamiento anterior es EXACTA por
    ''' construcción y no por redondeo — que importa, porque `right*d.X + Forward*d.Y + upPlane*d.Z` con la
    ''' base identidad suma ceros y eso convierte un -0.0 en +0.0 (la trampa que ya documenta
    ''' <c>ParentGlobalTransform</c>). El gate [luces-camara] verifica las dos ramas.</para>
    ''' <para>La sombra lo sigue SOLA: <c>RenderShadowPass</c> se encuadra sobre <c>_frameLights.KeyDir</c>,
    ''' que es la misma dirección que va a los uniforms. No hay una segunda ley que mantener en sincronía.</para>
    ''' </summary>
    Public Property Setting_LightsFollowCamera As Boolean = True

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
    ' Compresiones PER-GAME (sets separados, como convención/sort → no se filtra el valor al cambiar de juego). El bake
    ' lee la del juego activo via OutputSettings. FO4 y SSE tienen DEFAULTS DISTINTOS del normal (ver abajo).
    Public Property Setting_FaceGenDiffuseCompression As FaceTintConvention.FaceTintDiffuseCompression = FaceTintConvention.FaceTintDiffuseCompression.Bc3
    Public Property Setting_FaceGenDiffuseCompression_SSE As FaceTintConvention.FaceTintDiffuseCompression = FaceTintConvention.FaceTintDiffuseCompression.Bc3
    ' Normal FO4 = BC5 (DEFAULT): el _n vanilla de FaceCustomization es tangent-space 2-canales = BC5 (y el modo All lo
    ' deriva del diffuse via NsCompressionFromDiffuse).
    ' ⛔ Normal SSE = UNCOMPRESSED (DEFAULT) — el _msn es MODEL-SPACE (3 canales INDEPENDIENTES X/Y/Z). Cualquier BCn
    ' comprime RGB a una línea por bloque 4×4 y DESTRUYE la dirección de la normal. MEDIDO (probe --reencodetest, mismo
    ' encoder del bake, MaleHead_msn 1024²): BC3 → RGB RMS 5.07/255, max B 148/255, 97.5% de pixels alterados;
    ' UNCOMPRESSED → RMS 0.000 = round-trip EXACTO (pixel-idéntico al vanilla, que ES Uncompressed 32bpp, medido del BSA).
    ' El shader facegen lee el G-buffer de normales (o2.xy) de este slot ⇒ un normal degradado rompe lighting/sombras/
    ' reflexiones de TODA la cara. El BC3 anterior era un "compromiso" por creer que la alternativa era BC7 (lento):
    ' Uncompressed es lossless Y rápido (no comprime). Costo: ~5.6 MB vs 1.4 MB = el mismo tamaño que usa el vanilla.
    Public Property Setting_FaceGenNormalCompression As FaceTintConvention.FaceTintNormalSpecularCompression = FaceTintConvention.FaceTintNormalSpecularCompression.Bc5
    Public Property Setting_FaceGenNormalCompression_SSE As FaceTintConvention.FaceTintNormalSpecularCompression = FaceTintConvention.FaceTintNormalSpecularCompression.Uncompressed
    Public Property Setting_FaceGenSpecularCompression As FaceTintConvention.FaceTintNormalSpecularCompression = FaceTintConvention.FaceTintNormalSpecularCompression.Bc5
    Public Property Setting_FaceGenGenerateTga As Boolean = False
    ''' <summary>OFF (default) = el compositor toma el MIP STORED del tamaño destino. ON = parte del mip 0 y
    ''' baja con un bilineal, mas lento (4096 → 1024 desempaqueta 16x los pixeles, y la clave del cache lleva
    ''' el tamaño ⇒ un decode por canal). Lo consume <c>FaceTintCpuCompositor.DownsizeFromMip0</c>, que gatea
    ''' la ley de los DOS compositores a la vez.</summary>
    Public Property Setting_FaceGenDownsizeFromMip0 As Boolean = False

    ' === Fixes (botón "CharGen Options" → tab "Fixes") ===
    ' Eyebrows fixed-color override (SkipEyebrowsTone.ini → LUT sintética Dark->Light). Antes el feature
    ' se activaba SOLO por la presencia del archivo; ahora requiere AMBOS: este toggle persistido Y el
    ' archivo en el appdir. Default = True (= comportamiento previo: si el archivo está, el override aplica).
    ' Vive en Config_App (no NPC_Config) porque lo consume FaceTintInputBuilder en la librería, que lee
    ' Config_App.Current directamente (igual que Setting_FaceTintSort). Ver BuildSyntheticEyebrowLut.
    Public Property Setting_ApplyEyebrowsFixedColor As Boolean = True

    ' Mouth vanilla fix for BaseFemaleHeadChargen.tri. Vanilla ships spurious mouth-region deltas baked
    ' into DefaultFaceType0 + EyesLowLidUp + EyesLowSunken (measured: 22 shared verts below the nose tip,
    ' up to 0.73u). When True, those 22 deltas are zeroed at TRI read time (render AND bake), ONLY for that
    ' file. Consumed in the library by ChargenMouthFix (NpcMorphResolver.TryLoadTriHead + FaceGenBuildPipeline.
    ' ParseHeadTri), which reads Config_App.Current directly — hence Config_App, not NPC_Config. Default False
    ' (= pure vanilla). Cache is key-suffixed on this flag so toggling re-reads instead of serving a stale head.
    Public Property Setting_ApplyMouthVanillaFix As Boolean = False

    ' CharGen Options → tab Fixes (ambos juegos). OFF por defecto = cada material de piel usa su PROPIO
    ' subsurface (flag + rolloff) como viene autorado (engine-faithful; medido: cara y cuerpo difieren a
    ' propósito en varios casos — SSE argonian-F 0.3/0.4, FO4 basehuman-M flag OFF/ON, cryohuman 0.6/0.4).
    ' ON = MatchBodySkinSubsurfaceToFace copia SOLO el FLAG on/off de la cara al cuerpo (nunca el rolloff,
    ' que queda siempre autorado). Útil p.ej. FO4 macho (head OFF/body ON) para quien quiera igualarlos.
    Public Property Setting_MatchHeadSubsurfaceFlagToBody As Boolean = False

    ' SSE (CharGen Options → tab Fixes, SSE-only): bakear los overlays de RaceMenu de la CARA (Face [Ovl]
    ' face-paint) DENTRO de un diffuse por-NPC (slot 0 del FaceGeom). El engine los renderiza en vivo y NO los
    ' hornea; con esto quedan en la textura (WYSIWYG). Default True. GATEADO: NPCs sin overlays de cara no emiten
    ' diffuse (slot 0 sigue el complexion vanilla compartido). Lo lee FaceGenBuilder.WriteSseFaceDiffuseWithOverlays.
    Public Property Setting_BakeSseRaceMenuOverlays As Boolean = True

    ' SSE (CharGen Options → tab Fixes, SSE-only): redirect de los .tri de morph de CABEZA a High Poly Head
    ' (KouLeifoh) cuando el record apunta a un .tri que NO resuelve o cuya topología NO coincide con la malla de
    ' la cabeza, y la cabeza es EXACTAMENTE una cabeza HPH (Female=3832 / Male=3598 verts). Caso típico: followers/
    ' replacers construidos sobre HPH cuyo HDPT dejó los slots RaceMorph (NAM0=0) y ChargenMorph (NAM0=2) apuntando
    ' a la ruta vanilla `Actors\Character\Character Assets\FemaleHeadRaces/CharGen.tri` (996 verts) en vez de a
    ' `meshes\KL\High Poly Head\` (3832). El motor no lo nota (usa el bake); un rebuild fiel-al-record aplicaría
    ' deltas de 996 a una cabeza de 3832 → cara destrozada en el editor. Con esto, si HPH está instalado, el resolver
    ' toma el .tri correcto de HPH (mismo basename). GUARD EXACTO por vertex-count ⇒ nunca toca una cabeza no-HPH, y
    ' solo redirige cuando el .tri del record falta o su topología no matchea (un .tri presente y compatible se
    ' respeta). Default False (opt-in). Lo lee NpcMorphResolver.LoadTriForShape (camino de render/preview).
    Public Property Setting_SseResolveHighPolyHeadTri As Boolean = False

    ' === FaceTint convention (botón "CharGen Options" → tab "FaceTint Conventions") ===
    ' La convención de composición FaceTint por bucket (Diffuse / Normal+Specular / Swaps), valores
    ' CONCRETOS. Los defaults los pone el constructor de FaceTintConventionSettings = la ley derivada
    ' (byte-match con CK si no se tocan). El usuario los edita acá o desde la UI y ESOS pasan a ser la ley:
    ' FaceTintConvention.ResolveConvention los lee SIEMPRE. Blend NO está (record-driven / Replace, read-only).
    ' Un config.json viejo sin la key deserializa al default del constructor.
    ' ⭐ El SETTER es el punto por el que el set ENTRA al sistema: la deserialización de System.Text.Json
    ' escribe por acá, y también los `--config` del CLI y FaceTintConvention.SetActiveSettings. Por eso el
    ' upgrade de versión va acá y no en cada lectura (ver FaceTintConventionSettings.UpgradeInPlace): un set
    ' de la versión 0 trae SeedMode/SeedConstant con el default del CONSTRUCTOR, que es el de Fallout.
    Private _faceTintConvention As FaceTintConvention.FaceTintConventionSettings =
        FaceTintConvention.FaceTintConventionSettings.DefaultsFor(Game_Enum.Fallout4)
    Public Property Setting_FaceTintConvention As FaceTintConvention.FaceTintConventionSettings
        Get
            Return _faceTintConvention
        End Get
        Set(value As FaceTintConvention.FaceTintConventionSettings)
            FaceTintConvention.FaceTintConventionSettings.UpgradeInPlace(value, Game_Enum.Fallout4)
            _faceTintConvention = value
        End Set
    End Property

    ' La ley SSE (facegen-tint del CreationKit): seed constante 0.5, lerp uniforme por cobertura (sin blend-op
    ' por tipo), todo LINEAR, máscara por canal ROJO. Set SEPARADO del de FO4 para no tocar sus valores byte-
    ' exactos. Default = FaceTintConventionSettings.DefaultsFor(Skyrim). FaceTintConvention.ActiveSettings elige
    ' este cuando Game=Skyrim. Un config.json viejo sin la key deserializa a la ley SSE por default (abajo).
    ' Mismo upgrade que el slot de FO4, con el juego de ESTE slot (ver el comentario de arriba). Es el caso
    ' que lo motiva: un config de Skyrim de la versión 0 pide sembrar desde una textura base que el facetint
    ' de Skyrim no tiene.
    Private _faceTintConventionSse As FaceTintConvention.FaceTintConventionSettings =
        FaceTintConvention.FaceTintConventionSettings.DefaultsFor(Game_Enum.Skyrim)
    Public Property Setting_FaceTintConvention_SSE As FaceTintConvention.FaceTintConventionSettings
        Get
            Return _faceTintConventionSse
        End Get
        Set(value As FaceTintConvention.FaceTintConventionSettings)
            FaceTintConvention.FaceTintConventionSettings.UpgradeInPlace(value, Game_Enum.Skyrim)
            _faceTintConventionSse = value
        End Set
    End Property

    ' === FaceTint sort order (botón "CharGen Options" → tab "Tint Order") ===
    ' Orden de composición configurable (multi-clave asc/desc) de tints y swaps + placement del SkinTone.
    ' Default = comportamiento previo (tints PhysIndex desc, swaps forward, skintone Positional); editar
    ' acá o en la UI cambia el orden con que el builder compone las capas. Ver FaceTintSortSettings.
    Public Property Setting_FaceTintSort As New FaceTintSortSettings()
    ' Orden SSE SEPARADO (estructura distinta: tints = capas del RACE, "swaps" = overlays Face[Ovl]). Default =
    ' RaceMenu-fiel (tints [Race_Order asc], overlays [Ovl_Index asc], skintone Positional) = IDENTIDAD ⇒ el
    ' compose SSE queda byte-idéntico. Claves interpretadas como FaceTintSseTintSortKey/FaceTintSseOverlaySortKey.
    ' Set aparte para no tocar el de FO4 (Setting_FaceTintSort). ActiveSort() elige por Game.
    Public Property Setting_FaceTintSort_SSE As FaceTintSortSettings = FaceTintSortSettings.DefaultsForSse()
    ' (El compositor GPU/CPU NO es una preferencia persistida: es una REGLA derivada — render = GPU si
    '  skinning=GPU, sino CPU ; chargen = siempre CPU (async, no toca GL). Ver FaceGenBuilder.)

    ' === Rig de luces del previewer, SEPARADO POR JUEGO (misma convención que las opciones de CharGen) ===
    ' Nadie lee estas dos directamente: el render y LightRigForm van por ActiveLights()/SetActiveLights(),
    ' así cambiar Game cambia el rig sin que el caller se entere. Ver PreviewLightRig.vb.
    ' ⛔ Reemplaza al viejo `Setting_Lightrig` (LightsRig_struct), BORRADO sin compatibilidad: guardaba los
    ' colores como System.Numerics.Vector3 (X/Y/Z son CAMPOS) y System.Text.Json los escribía como `{}`,
    ' así que al releer volvían (0,0,0) = ambient negro. La key vieja del config.json se ignora al cargar
    ' (STJ saltea las desconocidas) y desaparece en el próximo guardado.
    Public Property Setting_PreviewLights_FO4 As PreviewLightRig = PreviewLightRig.Defaults()
    Public Property Setting_PreviewLights_SSE As PreviewLightRig = PreviewLightRig.Defaults()

    ''' <summary>El rig del juego activo. Value-type: devuelve una COPIA (editarla no persiste nada;
    ''' para escribir usar <see cref="SetActiveLights"/>).</summary>
    Public Function ActiveLights() As PreviewLightRig
        Return If(Game = Game_Enum.Skyrim, Setting_PreviewLights_SSE, Setting_PreviewLights_FO4)
    End Function

    ''' <summary>Escribe el rig en el slot del juego activo.</summary>
    Public Sub SetActiveLights(rig As PreviewLightRig)
        If Game = Game_Enum.Skyrim Then
            Setting_PreviewLights_SSE = rig
        Else
            Setting_PreviewLights_FO4 = rig
        End If
    End Sub

    ' === Sombras proyectadas del previewer, POR JUEGO (misma convención que el rig de luces) ===
    ' Nadie las lee directo: Render y LightRigForm van por ActiveShadows()/SetActiveShadows().
    ' ⛔ Un config.json anterior a esta opción no trae la clave y PreviewShadowSettings es una Structure:
    ' el deserializador la deja en cero. El repuesto está en LoadConfig con MapSize=0 de centinela.
    Public Property Setting_PreviewShadows_FO4 As PreviewShadowSettings = PreviewShadowSettings.Defaults()
    Public Property Setting_PreviewShadows_SSE As PreviewShadowSettings = PreviewShadowSettings.Defaults()

    ''' <summary>Los ajustes de sombra del juego activo. Value-type: devuelve una COPIA (para escribir,
    ''' <see cref="SetActiveShadows"/>).</summary>
    Public Function ActiveShadows() As PreviewShadowSettings
        Return If(Game = Game_Enum.Skyrim, Setting_PreviewShadows_SSE, Setting_PreviewShadows_FO4)
    End Function

    ''' <summary>Escribe los ajustes de sombra en el slot del juego activo.</summary>
    Public Sub SetActiveShadows(s As PreviewShadowSettings)
        If Game = Game_Enum.Skyrim Then
            Setting_PreviewShadows_SSE = s
        Else
            Setting_PreviewShadows_FO4 = s
        End If
    End Sub

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

    ''' <summary>Convierte los rigs guardados con el esquema VIEJO (6 multiplicadores relativos a la
    ''' camara) al nuevo (azimut/elevacion de mundo). Idempotente: un rig ya migrado trae
    ''' <c>SchemaVersion &gt;= 1</c> y no se toca.
    ''' <para>⛔ Si el JSON no trae las claves viejas tampoco (config nuevo, o rig recortado a mano) se
    ''' repone el preset por default para ESE rig, y se dice por Logger. Lo que NO se hace es dejarlo en
    ''' cero: cuatro luces apuntando al mismo lado no es un estado que nadie haya elegido.</para></summary>
    Private Shared Sub MigrateLightRigsToWorldSpace(path As String)
        If Current.Setting_PreviewLights_FO4.SchemaVersion >= PreviewLightRig.CurrentSchemaVersion AndAlso
           Current.Setting_PreviewLights_SSE.SchemaVersion >= PreviewLightRig.CurrentSchemaVersion Then Return

        ' ⛔⭐ SE PASA EL JsonDocument, NO UN `JsonElement?`. Estaba
        '     Dim root As JsonElement? = If(doc Is Nothing, Nothing, doc.RootElement)
        ' y en VB ese ternario unifica al TIPO DE VALOR, no al Nullable: con doc = Nothing devuelve un
        ' JsonElement por default ENVUELTO, o sea HasValue = True. El guard `Not root.HasValue` de abajo
        ' era codigo muerto —la rama de fallback documentada nunca corria— y la primera llamada a
        ' TryGetProperty sobre ese elemento hueco tira InvalidOperationException desde LoadConfig: crash
        ' de arranque, antes de que exista el Logger. Verificado compilando el caso aparte.
        Using doc = JsonConfigIO.TryOpenRaw(path)
            If Current.Setting_PreviewLights_FO4.SchemaVersion < PreviewLightRig.CurrentSchemaVersion Then
                Current.Setting_PreviewLights_FO4 = MigrateOneRig(doc, "Setting_PreviewLights_FO4")
            End If
            If Current.Setting_PreviewLights_SSE.SchemaVersion < PreviewLightRig.CurrentSchemaVersion Then
                Current.Setting_PreviewLights_SSE = MigrateOneRig(doc, "Setting_PreviewLights_SSE")
            End If
        End Using
    End Sub

    Friend Shared Function MigrateOneRig(doc As System.Text.Json.JsonDocument, key As String) As PreviewLightRig
        Dim rigEl As System.Text.Json.JsonElement
        If doc Is Nothing OrElse Not doc.RootElement.TryGetProperty(key, rigEl) OrElse
           rigEl.ValueKind <> System.Text.Json.JsonValueKind.Object Then
            Logger.Log($"[RIG-MIGRA] '{key}' no estaba en el config.json: se repone el preset por default.")
            Return PreviewLightRig.Defaults()
        End If

        ' El resto del rig (ambiente, colores, strengths) lo deserializo bien el tipo actual: sus claves no
        ' cambiaron. Solo hay que reconstruir las CUATRO direcciones.
        Dim rig = If(key.EndsWith("_SSE", StringComparison.Ordinal), Current.Setting_PreviewLights_SSE, Current.Setting_PreviewLights_FO4)
        ' ⛔ EL RESGUARDO ES POR LUZ, NO POR RIG. `anyConverted` era uno solo: si UNA sola luz traia los
        ' multiplicadores viejos, las otras tres se quedaban con lo que dio el deserializador —azimut 0,
        ' elevacion 0, o sea las cuatro apuntando horizontal desde +Y— y el rig se marcaba migrado, con lo
        ' cual no se reintentaba nunca. El doc de arriba dice justo que eso no es un estado que nadie haya
        ' elegido; cubrirlo solo en el caso todo-o-nada es no cubrirlo.
        Dim def = PreviewLightRig.Defaults()
        Dim anyConverted As Boolean = False
        rig.KeyLight = MigrateOneLight(rigEl, "KeyLight", rig.KeyLight, def.KeyLight, anyConverted)
        rig.FillLeft = MigrateOneLight(rigEl, "FillLeft", rig.FillLeft, def.FillLeft, anyConverted)
        rig.FillRight = MigrateOneLight(rigEl, "FillRight", rig.FillRight, def.FillRight, anyConverted)
        rig.BackLight = MigrateOneLight(rigEl, "BackLight", rig.BackLight, def.BackLight, anyConverted)

        If Not anyConverted Then
            Logger.Log($"[RIG-MIGRA] '{key}' no traia los multiplicadores viejos: se repone el preset por default.")
            Return PreviewLightRig.Defaults()
        End If
        rig.SchemaVersion = PreviewLightRig.CurrentSchemaVersion
        Logger.Log($"[RIG-MIGRA] '{key}' convertido a luces fijas al mundo.")
        Return rig
    End Function

    Private Shared Function MigrateOneLight(rigEl As System.Text.Json.JsonElement, name As String,
                                            current As PreviewLight, fallback As PreviewLight,
                                            ByRef anyConverted As Boolean) As PreviewLight
        ' Sin direccion vieja que convertir se toma la del PRESET, no la del deserializador: esta ultima es
        ' (0, 0) y dejaria la luz apuntando al horizonte. Strength y Color del usuario se conservan.
        Dim sinDireccion = fallback
        sinDireccion.Strength = current.Strength
        sinDireccion.Color = current.Color
        Dim el As System.Text.Json.JsonElement
        If Not rigEl.TryGetProperty(name, el) OrElse el.ValueKind <> System.Text.Json.JsonValueKind.Object Then Return sinDireccion
        Dim up, dn, le, ri, fw, bk As Single
        Dim got As Boolean = JsonConfigIO.TryGetSingle(el, "Up", up)
        got = JsonConfigIO.TryGetSingle(el, "Down", dn) OrElse got
        got = JsonConfigIO.TryGetSingle(el, "Left", le) OrElse got
        got = JsonConfigIO.TryGetSingle(el, "Right", ri) OrElse got
        got = JsonConfigIO.TryGetSingle(el, "Forward", fw) OrElse got
        got = JsonConfigIO.TryGetSingle(el, "Back", bk) OrElse got
        If Not got Then Return sinDireccion
        anyConverted = True
        ' Strength y Color ya vienen bien del deserializador: solo se reemplaza la DIRECCION.
        Dim conv = PreviewLight.FromCameraRelative(current.Strength, up, dn, le, ri, fw, bk)
        conv.Color = current.Color
        Return conv
    End Function

    Public Shared Sub LoadConfig()
        Dim cfg = JsonConfigIO.Load(Of Config_App)(ConfigFilePath, "configuration")
        If cfg IsNot Nothing Then
            Current = cfg
            If Current.Settings_RenderGrid.Size = 0 Then Current.Settings_RenderGrid = Default_RenderGrid_Settings()
            ' ⛔ CENTINELA de las sombras. PreviewShadowSettings es una Structure y el config.json de todo
            ' usuario existente NO trae la clave, así que el deserializador la deja entera en cero: las
            ' sombras arrancarían apagadas Y con MapSize=0, o sea rotas si alguien las prendiera. Un mapa
            ' de 0 texeles no es un valor legítimo, que es justo lo que se le pide a un centinela (un
            ' Boolean no serviría: False-por-ausencia y False-por-decisión son indistinguibles).
            ' Ver memoria 10-stack-json-structure-defaults.
            If Current.Setting_PreviewShadows_FO4.MapSize <= 0 Then Current.Setting_PreviewShadows_FO4 = PreviewShadowSettings.Defaults()
            If Current.Setting_PreviewShadows_SSE.MapSize <= 0 Then Current.Setting_PreviewShadows_SSE = PreviewShadowSettings.Defaults()

            ' ⛔ MIGRACION DEL RIG A LUCES FIJAS AL MUNDO (esquema 0 -> 1). El esquema viejo guardaba SEIS
            ' multiplicadores por luz relativos a la CAMARA; el nuevo guarda azimut/elevacion de mundo. El
            ' deserializador ignora las claves viejas y deja las nuevas en CERO, o sea que sin esto todo
            ' usuario existente arrancaria con las cuatro luces apuntando al mismo lado (azim 0, elev 0) y
            ' el preview cambiado, en silencio.
            ' Se convierten LEYENDO EL JSON CRUDO, que es donde siguen estando los 6 multiplicadores. La
            ' conversion es exacta: en la vista por defecto la base de camara ES la del mundo (ver
            ' PreviewLight.FromCameraRelative), asi que el rig del usuario conserva su aspecto en esa vista.
            MigrateLightRigsToWorldSpace(ConfigFilePath)
            ' ⛔ Un config.json ANTERIOR a la opcion de costuras no trae estas dos claves, y TBNOptions
            ' es una Structure: el deserializador la crea en CERO y solo asigna lo que encuentra, asi
            ' que el usuario existente arrancaria con el suavizado APAGADO y el angulo en 0 — o sea con
            ' la correccion desactivada sin haberlo pedido. El angulo 0 no es un valor legitimo (con
            ' 0 grados no se promedia ningun companero), asi que sirve de centinela de "clave ausente".
            If Current.Setting_TBN.SmoothSeamNormalsAngle <= 0.0 Then
                ' ⛔ Los valores salen de DefaultTBNOptions, que es donde viven los defaults. Repetirlos
                ' aca dejaba el default declarado en dos lugares y este pisaba al otro en silencio.
                Dim d = RecalcTBN.DefaultTBNOptions()
                Dim t = Current.Setting_TBN
                t.SmoothSeamNormals = d.SmoothSeamNormals
                t.SmoothSeamNormalsAngle = d.SmoothSeamNormalsAngle
                Current.Setting_TBN = t
            End If

            ' ⛔ MIGRACION POR VERSION DE OPCIONES. Una opcion NUEVA no esta en el config.json de un
            ' usuario existente, y TBNOptions es una Structure: el deserializador la deja en False/0.
            ' O sea que la estrenaria APAGADA sin haberlo pedido, en silencio. `OptionsVersion` dice con
            ' que juego de opciones se escribio el archivo y aca se rellenan SOLO las posteriores; lo
            ' que el usuario si eligio no se toca. Al agregar una opcion: subir la constante en
            ' RecalcTBN y agregar su rama.
            ' La migracion en si es PURA y vive en RepararOpcionesTBN: aca solo se decide si hace
            ' falta, se aplica y se graba. Separarla es lo que permite que el gate `weld-epsilon` la
            ' pruebe sin tocar el config del usuario ni el disco.
            If Current.Setting_TBN.OptionsVersion < RecalcTBN.VersionDeOpcionesTBN Then
                Current.Setting_TBN = RepararOpcionesTBN(Current.Setting_TBN)
                SaveConfig()
            End If
        End If
    End Sub

    ''' <summary>⛔ MIGRACION POR VERSION DE OPCIONES, PURA. Una opcion NUEVA no esta en el config.json
    ''' de un usuario existente, y <c>TBNOptions</c> es una Structure: el deserializador la deja en
    ''' False/0. O sea que la estrenaria APAGADA sin haberlo pedido, en silencio.
    ''' <c>OptionsVersion</c> dice con que juego de opciones se escribio el archivo y aca se rellenan SOLO
    ''' las posteriores; lo que el usuario SI eligio no se toca.
    ''' <para>Al agregar una opcion: subir <c>RecalcTBN.VersionDeOpcionesTBN</c>, agregar su rama, y sumar
    ''' el caso al gate <c>weld-epsilon</c> de ParityGate.</para>
    ''' <para>⛔ ES PURA A PROPOSITO: no lee ni escribe <c>Current</c> ni el disco. Estando enterrada
    ''' adentro de LoadConfig no habia forma de probarla sin arrancar la app con un config fabricado, y
    ''' por eso se le escaparon dos claves durante dos versiones.</para></summary>
    Friend Shared Function RepararOpcionesTBN(original As RecalcTBN.TBNOptions) As RecalcTBN.TBNOptions
        Dim d = RecalcTBN.DefaultTBNOptions()
        Dim t = original
        If t.OptionsVersion >= RecalcTBN.VersionDeOpcionesTBN Then Return t
                If t.OptionsVersion < 1 Then
                    ' Opcion NUEVA: sin esto quedaria en False para todo usuario existente.
                    t.DeterministicOnCollapse = d.DeterministicOnCollapse
                    ' ⛔ Y los dos defaults que CAMBIARON. No alcanza con migrar las claves nuevas: la
                    ' version anterior ESCRIBIO estos valores al disco, asi que un usuario existente
                    ' los tiene como si los hubiera elegido, y los dos estan medidos como peores.
                    '   EpsilonPos 1e-12 -> 0 : el 1e-12 EMPEORA (FO4 CBBE, bitangente de costura
                    '     0,52 -> 0,85 grados y su maximo 153 -> 180) y en SSE es inerte.
                    '   WeldByPositionOnly False -> True : con False el welding no agrupa nada y la
                    '     opcion queda en un no-op (dispersion del marco en el grupo 84,13 vs 6,19
                    '     grados). Solo se lee con EnableWelding puesta, que viene apagada.
                    ' Los dos se pueden volver a poner desde la pantalla de configuracion.
                    t.EpsilonPos = d.EpsilonPos
                    t.WeldByPositionOnly = d.WeldByPositionOnly
                End If
                If t.OptionsVersion < 2 Then
                    ' ⛔ NO es una opcion nueva: es la MISMA clave con otro significado. Hasta la v1 el
                    ' numero se comparaba contra `LengthSquared`, o sea que el umbral EFECTIVO sobre la
                    ' longitud era su raiz; desde la v2 el numero ES la longitud. Sin esta rama, un
                    ' usuario que hubiera elegido un valor se encontraria con un filtro mil veces mas
                    ' agresivo sin haber tocado nada. La raiz deja el comportamiento EXACTAMENTE igual
                    ' al que tenia. Con el default 0 es un no-op (sqrt(0) = 0), que es el caso de
                    ' practicamente todos: la migracion a v1 ya habia forzado EpsilonPos a 0.
                    If t.EpsilonPos > 0.0 Then t.EpsilonPos = Math.Sqrt(t.EpsilonPos)
                End If
                If t.OptionsVersion < 3 Then
                    ' ⛔ LOS DOS EPSILON DE WELDING NUNCA SE MIGRARON. `TBNOptions` es una Structure, asi
                    ' que toda clave ausente en el JSON queda en el CERO DEL TIPO, no en su default. Las
                    ' migraciones a v1 y v2 rellenaron DeterministicOnCollapse, EpsilonPos y
                    ' WeldByPositionOnly, y a estas dos se las saltearon.
                    ' Lo que hacia el 0: el dialogo lo ACOTA en silencio al minimo del control (1e-12) al
                    ' mostrarlo, y despues, apenas el usuario toca CUALQUIER otra casilla de la pestana,
                    ' `VolcarRenderEnModelo` reescribe las diez propiedades de una sola vez — o sea que el
                    ' valor acotado se COMMITEA sin que nadie lo haya elegido. El mecanismo viejo de WM
                    ' (`_cargaCompleta`) dejaba que la asignacion tirara y NO guardaba nada; al mudar la
                    ' pestana al dialogo compartido se perdio esa proteccion.
                    ' ⛔ El 0 sirve de CENTINELA porque NO es alcanzable desde la UI: el minimo de los dos
                    ' NumericUpDown es 1e-12 (ver LightRigForm.Designer). Un 0 en el JSON solo puede venir
                    ' de una clave que no estaba. Es el patron de 10-stack-json-structure-defaults.
                    If t.WeldPosEpsilon <= 0.0 Then t.WeldPosEpsilon = d.WeldPosEpsilon
                    If t.WeldUVEpsilon <= 0.0 Then t.WeldUVEpsilon = d.WeldUVEpsilon
                End If
        t.OptionsVersion = RecalcTBN.VersionDeOpcionesTBN
        Return t
    End Function


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
