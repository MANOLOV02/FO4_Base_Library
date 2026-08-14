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

    ' ==========================================================================================
    ' Rutas del juego fijadas A MANO — dos slots por juego, uno por juego
    ' ==========================================================================================
    ' El nombre de la carpeta que el motor usa para Plugins.txt y para los .ini es una constante compilada
    ' dentro del exe, y cada tienda la cambia (la edición de GOG de Skyrim SE usa "Skyrim Special Edition
    ' GOG"). GamePathsResolver la deriva sola para las variantes que están VERIFICADAS; para todo lo demás
    ' —Epic, Microsoft Store, un juego bajo un mod manager— la única respuesta correcta es que la elija el
    ' usuario. Estas cuatro claves son esa elección.
    '
    ' ⭐ "" = AUTOMÁTICO, y es el default. Un config.json existente no trae las claves ⇒ deserializan a ""
    '    ⇒ resolución automática. Cero migración y cero cambio de comportamiento para quien ya funciona.
    ' ⭐ ≠ "" = pisado por el usuario: GANA SIEMPRE y ni siquiera se toca el disco para comprobarlo.
    ' ⭐ POR JUEGO, como el rig de luces y las sombras (ver Setting_LightRig_* y ActiveLights()). Alguien que
    '    alterna FO4 y Skyrim tiene DOS load orders reales y distintos; un slot único haría que configurar
    '    uno destruyera el otro en silencio.
    ' ⛔ NO se persiste el valor AUTOMÁTICO, sólo el pisado. Guardar el derivado lo dejaría podrido en cuanto
    '    el usuario mueva el juego, y volveríamos a tener una ruta que miente sin que nadie la haya elegido.

    ''' <summary>Ruta COMPLETA del Plugins.txt de Fallout 4 fijada por el usuario. "" = automático.</summary>
    Public Property Setting_PluginsTxtPath_FO4 As String = ""
    ''' <summary>Ruta COMPLETA del Plugins.txt de Skyrim fijada por el usuario. "" = automático.</summary>
    Public Property Setting_PluginsTxtPath_SSE As String = ""
    ''' <summary>CARPETA de los .ini de Fallout 4 fijada por el usuario. "" = automático. Es la carpeta y no
    ''' un archivo porque son tres (Fallout4.ini, Fallout4Custom.ini, Fallout4Prefs.ini).</summary>
    Public Property Setting_GameIniDir_FO4 As String = ""
    ''' <summary>CARPETA de los .ini de Skyrim fijada por el usuario. "" = automático.</summary>
    Public Property Setting_GameIniDir_SSE As String = ""

    ''' <summary>El Plugins.txt fijado a mano para el juego ACTIVO, o "" si va por automático.</summary>
    Public Function ActivePluginsTxtOverride() As String
        Return If(Game = Game_Enum.Skyrim, Setting_PluginsTxtPath_SSE, Setting_PluginsTxtPath_FO4)
    End Function

    ''' <summary>Escribe el slot del juego ACTIVO. "" lo devuelve a automático.</summary>
    Public Sub SetActivePluginsTxtOverride(value As String)
        Dim v = If(value, "").Trim()
        If Game = Game_Enum.Skyrim Then Setting_PluginsTxtPath_SSE = v Else Setting_PluginsTxtPath_FO4 = v
        GamePathsResolver.Invalidate()
    End Sub

    ''' <summary>La carpeta de .ini fijada a mano para el juego ACTIVO, o "" si va por automático.</summary>
    Public Function ActiveGameIniDirOverride() As String
        Return If(Game = Game_Enum.Skyrim, Setting_GameIniDir_SSE, Setting_GameIniDir_FO4)
    End Function

    ''' <summary>Escribe el slot del juego ACTIVO. "" lo devuelve a automático.</summary>
    Public Sub SetActiveGameIniDirOverride(value As String)
        Dim v = If(value, "").Trim()
        If Game = Game_Enum.Skyrim Then Setting_GameIniDir_SSE = v Else Setting_GameIniDir_FO4 = v
        GamePathsResolver.Invalidate()
    End Sub
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
    ''' <summary>⛔⭐ EL NOMBRE DE LA PROPIEDAD **ES** LA VERSION DEL ESQUEMA, y por eso no hay ninguna.
    '''
    ''' <para>Estas claves se llamaban <c>Setting_PreviewLights_FO4/_SSE</c>. Al re-autorarse los presets y
    ''' agregarse el casteo por luz, el rig guardado de cualquier usuario dejo de tener sentido —no hay
    ''' recomposicion posible: cambia el set entero— asi que en vez de versionar y reparar, se RENOMBRO.
    ''' El mecanismo es el que ya uso este proyecto cuando <c>Setting_Lightrig</c> paso a
    ''' <c>Setting_PreviewLights_*</c>, y esta MEDIDO sobre el config.json real: System.Text.Json ignora la
    ''' clave que no conoce al cargar, la propiedad nueva se queda con el inicializador (o sea
    ''' <c>Defaults()</c>, porque Config_App es una CLASS y sus inicializadores SI corren), y al re-guardar
    ''' se escribe sólo lo que existe hoy ⇒ <b>la clave vieja desaparece sola del archivo</b>.</para>
    '''
    ''' <para>⭐ Lo que esto compra: CERO guards. No hay <c>SchemaVersion</c>, ni funcion de reparacion, ni
    ''' centinela, ni rama que pueda no dispararse. Un config viejo no se "detecta y repara": directamente
    ''' no se lee. Es la unica forma de invalidacion que no puede fallar en silencio, y este archivo tenia
    ''' TRES mecanismos distintos para el mismo problema (version en el rig, centinela <c>MapSize &lt;= 0</c>
    ''' en las sombras, version en TBN), uno de ellos —el centinela— incapaz de distinguir "nunca existio"
    ''' de "existio con otro significado".</para>
    '''
    ''' <para>⛔ EL PRECIO, y hay que saberlo: esto sirve UNA VEZ POR NOMBRE. Si mañana cambia otra vez el
    ''' significado del rig hay que RENOMBRAR DE NUEVO; si alguien edita la semantica y se olvida, el dato
    ''' viejo se reinterpreta en silencio y no hay version que lo delate. A cambio, mientras se respete, no
    ''' existe el modo de falla contrario (un guard que se creia que corria y no corria). El gate
    ''' <c>config-esquema-borrado</c> verifica las dos mitades: que la clave vieja se ignore y que no
    ''' sobreviva al guardado.</para></summary>
    Public Property Setting_LightRig_FO4 As PreviewLightRig = PreviewLightRig.Defaults()
    Public Property Setting_LightRig_SSE As PreviewLightRig = PreviewLightRig.Defaults()

    ''' <summary>El rig del juego activo. Value-type: devuelve una COPIA (editarla no persiste nada;
    ''' para escribir usar <see cref="SetActiveLights"/>).</summary>
    Public Function ActiveLights() As PreviewLightRig
        Return If(Game = Game_Enum.Skyrim, Setting_LightRig_SSE, Setting_LightRig_FO4)
    End Function

    ''' <summary>Escribe el rig en el slot del juego activo.</summary>
    Public Sub SetActiveLights(rig As PreviewLightRig)
        If Game = Game_Enum.Skyrim Then
            Setting_LightRig_SSE = rig
        Else
            Setting_LightRig_FO4 = rig
        End If
    End Sub

    ' === Sombras proyectadas del previewer, POR JUEGO (misma convención que el rig de luces) ===
    ' Nadie las lee directo: Render y LightRigForm van por ActiveShadows()/SetActiveShadows().
    ' ⛔ RENOMBRADAS junto con el rig (eran `Setting_PreviewShadows_FO4/_SSE`) y por el mismo motivo: ver el
    ' bloque de Setting_LightRig_*. Renombrarlas ADEMAS elimina el centinela `MapSize <= 0` que tenían en
    ' LoadConfig: con la clave nueva ausente, la propiedad se queda con el `Defaults()` completo y no hay
    ' nada que detectar ni reparar. Ese centinela era el más débil de los tres mecanismos que convivían acá,
    ' porque sólo distinguía "la clave no estaba" y no "la clave estaba y significaba otra cosa".
    ' ⚠️ El clamp de PreviewShadowSettings.Sanitized() NO se va: eso defiende el camino de dibujo de un
    ' config editado a mano, que es otro problema.
    Public Property Setting_ShadowMaps_FO4 As PreviewShadowSettings = PreviewShadowSettings.Defaults()
    Public Property Setting_ShadowMaps_SSE As PreviewShadowSettings = PreviewShadowSettings.Defaults()

    ''' <summary>Los ajustes de sombra del juego activo. Value-type: devuelve una COPIA (para escribir,
    ''' <see cref="SetActiveShadows"/>).</summary>
    Public Function ActiveShadows() As PreviewShadowSettings
        Return If(Game = Game_Enum.Skyrim, Setting_ShadowMaps_SSE, Setting_ShadowMaps_FO4)
    End Function

    ''' <summary>Escribe los ajustes de sombra en el slot del juego activo.</summary>
    Public Sub SetActiveShadows(s As PreviewShadowSettings)
        If Game = Game_Enum.Skyrim Then
            Setting_ShadowMaps_SSE = s
        Else
            Setting_ShadowMaps_FO4 = s
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

    ' ⛔⭐ ACA VIVIA `RepararRigDeEsquemaViejo`, Y SE BORRO ENTERA. El rig y las sombras del preview ya no se
    ' versionan ni se reparan: sus propiedades se RENOMBRARON, con lo cual el dato viejo no se detecta —
    ' simplemente no se lee, y desaparece del archivo en el proximo guardado. Ver el bloque de
    ' `Setting_LightRig_*`, donde esta el mecanismo y su precio.
    ' El motivo de fondo: un guard puede no dispararse (y este archivo llego a tener TRES mecanismos
    ' distintos, uno de ellos incapaz de distinguir "la clave no estaba" de "estaba y significaba otra
    ' cosa"). Una clave que no existe no puede leerse mal.

    Public Shared Sub LoadConfig()
        Dim cfg = JsonConfigIO.Load(Of Config_App)(ConfigFilePath, "configuration")
        If cfg IsNot Nothing Then
            Current = cfg
            ' `hayQueGrabar` se declara aca arriba porque el centinela de la grilla tambien MUTA `Current` y
            ' antes no tocaba el grabado: se salvaba de casualidad, por el "grabado gratis" de la rama de
            ' TBN que corria en cada arranque. Desde que esa rama es de UNA VEZ por version, sin esto la
            ' grilla se repara SOLO EN MEMORIA y se rehace el mismo trabajo en cada arranque.
            Dim hayQueGrabar As Boolean = False
            If Current.Settings_RenderGrid.Size = 0 Then
                Current.Settings_RenderGrid = Default_RenderGrid_Settings()
                hayQueGrabar = True
            End If
            ' ⛔⭐ ACA HABIA CUATRO RAMAS MAS —el centinela `MapSize <= 0` de las dos configs de sombra y la
            ' reparacion por version de los dos rigs— y NO SE REEMPLAZARON POR NADA: las cuatro propiedades
            ' se RENOMBRARON, asi que el dato viejo ya no se lee y no hay nada que detectar. Ver el bloque de
            ' `Setting_LightRig_*`.
            ' ⭐ Y no hace falta grabar por eso: un config viejo se carga con los defaults en memoria, y la
            ' clave vieja desaparece del archivo en el primer guardado que ocurra por cualquier motivo. Si
            ' nunca ocurre, tampoco importa — lo que hay en disco no se vuelve a leer.
            ' ⛔ MIGRACION POR VERSION DE OPCIONES. Una opcion NUEVA no esta en el config.json de un
            ' usuario existente, y TBNOptions es una Structure: el deserializador la deja en False/0, o sea
            ' que la estrenaria APAGADA sin haberlo pedido. Si el archivo declara una version anterior,
            ' `RepararOpcionesTBN` repone los defaults COMPLETOS — SI, eso pisa lo que el usuario haya
            ' elegido; es decision suya y esta explicada en el doc de esa funcion.
            ' La migracion es PURA y vive alla: aca solo se decide si hace falta, se aplica y se graba.
            ' Separarla es lo que permite que el gate `weld-epsilon` la pruebe sin tocar el config del
            ' usuario ni el disco.
            If Current.Setting_TBN.OptionsVersion < RecalcTBN.VersionDeOpcionesTBN Then
                Current.Setting_TBN = RepararOpcionesTBN(Current.Setting_TBN)
                hayQueGrabar = True
            End If
            If hayQueGrabar Then SaveConfig()
        End If
    End Sub

    ''' <summary>⭐ CONFIG VIEJO ⇒ DEFAULTS COMPLETOS. Sin ramas, sin centinelas, sin casos por campo.
    '''
    ''' <para><b>Decisión expresa del usuario (2026-08-12).</b> Antes esto eran tres ramas encadenadas
    ''' (v&lt;1, v&lt;2, v&lt;3), cada una reponiendo unos campos y respetando otros, con un centinela distinto
    ''' por campo. Eso trajo: dos claves que se saltearon durante dos versiones, un centinela inválido
    ''' (el ángulo 0, que SÍ es elegible desde la UI) que revertía dos elecciones del usuario en
    ''' silencio, una rama que no se ejecutaba nunca, y una discusión larga sobre qué campo admite
    ''' centinela y cuál no. El costo de mantenerlo superaba lo que protegía.</para>
    '''
    ''' <para>La regla ahora es una línea: <b>si el archivo declara una versión anterior a la vigente, se
    ''' reponen TODOS los defaults.</b> Sí, eso pisa lo que el usuario hubiera elegido en esos campos —
    ''' es el precio, y está aceptado: un cambio de versión significa que los criterios cambiaron, y
    ''' arrancar con los defaults nuevos es preferible a arrastrar una mezcla que nadie eligió.</para>
    '''
    ''' <para>⛔ Al agregar una opción: subir <c>RecalcTBN.VersionDeOpcionesTBN</c> y listo. No hay rama
    ''' que escribir, así que tampoco hay rama que olvidarse — que era el defecto de origen.</para>
    '''
    ''' <para>⛔ ES PURA a propósito: no lee ni escribe <c>Current</c> ni el disco, así que el gate
    ''' <c>weld-epsilon</c> la puede probar sin arrancar la app con un config fabricado.</para></summary>
    Friend Shared Function RepararOpcionesTBN(original As RecalcTBN.TBNOptions) As RecalcTBN.TBNOptions
        If original.OptionsVersion >= RecalcTBN.VersionDeOpcionesTBN Then Return original
        Return RecalcTBN.DefaultTBNOptions()
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
