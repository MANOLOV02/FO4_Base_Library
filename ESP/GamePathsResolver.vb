Imports System.IO

''' <summary>
''' Resuelve las DOS rutas que el motor deriva de una constante compilada dentro del exe y que ninguna
''' herramienta puede leer de un archivo de configuración: la carpeta de <c>Plugins.txt</c> /
''' <c>loadorder.txt</c> bajo <c>%LOCALAPPDATA%</c>, y la carpeta de los .ini bajo
''' <c>Documents\My Games</c>. Ambas cuelgan del MISMO nombre interno del juego.
'''
''' <para><b>Por qué existe esta clase.</b> Antes el nombre salía de una tabla de dos entradas por juego
''' (plana + VR) y se elegía con <c>Directory.Exists</c>. Eso rompe en cuanto la tienda cambia el nombre:
''' la edición de GOG de Skyrim SE usa <c>Skyrim Special Edition GOG</c> en las DOS raíces. Y rompía en
''' silencio, que es lo peor: en una máquina con la carpeta de Steam presente (aunque vacía) el
''' <c>Directory.Exists</c> daba True, se leía un Plugins.txt ajeno o inexistente, y la app mostraba el
''' juego sin un solo mod sin emitir ningún error.</para>
'''
''' <para><b>NO BUSCA EN DISCO.</b> Decisión explícita del usuario (2026-08-13): esta clase va DIRECTO a
''' rutas conocidas o le PREGUNTA al usuario. No hay barridos, ni patrones, ni <c>EnumerateDirectories</c>,
''' ni escaneo del exe, ni registro, ni parseo de librerías de Steam. El motivo no es el costo medido acá
''' —la app se distribuye y esta máquina no autoriza nada— sino la FORMA: un <see cref="File.Exists"/>
''' sobre una ruta conocida toca UNA entrada, mientras que una enumeración escala con la cantidad de
''' entradas y con lo que haya en el camino del filtro (antivirus, el cloud filter de OneDrive, un
''' <c>Documents</c> redirigido por política a un share de red). Nada de eso existe en el equipo de
''' desarrollo y todo eso puede existir del otro lado.</para>
'''
''' <para><b>Costo total.</b> Con override del usuario: CERO accesos. Sin override: 1-2
''' <see cref="File.Exists"/> (uno por candidato de la tabla), y sólo si ninguno acierta, 1-2 más contra
''' el .ini. Memoizado por sesión con clave (exe, juego, overrides), así que ese puñado de stats se paga
''' una vez y no por llamada — hoy <c>ResolveGameAppDataDir</c> hacía 2 <c>Directory.Exists</c> en CADA
''' invocación, sin caché.</para>
'''
''' <para><b>Lo que NO resuelve, a propósito.</b> Una variante que no esté en <see cref="CandidateFolders"/>
''' (Epic, Microsoft Store, una tienda futura) cae en <see cref="PathOrigin.NotResolved"/> y termina en el
''' selector de la UI. Como esa elección se persiste por juego, el usuario lo hace UNA vez. Preguntar una
''' vez es honesto; adivinar mal es silencioso.</para>
''' </summary>
Public NotInheritable Class GamePathsResolver

    Private Sub New()
    End Sub

    ''' <summary>De dónde salió una ruta. Lo consume la UI para decidir si la muestra como valor propio del
    ''' usuario o como valor derivado, y para explicar por qué no hay ninguna.</summary>
    Public Enum PathOrigin
        ''' <summary>No se pudo resolver: hay que preguntarle al usuario. <see cref="GamePaths.Problem"/>
        ''' dice por qué.</summary>
        NotResolved = 0
        ''' <summary>La fijó el usuario a mano y está persistida. Gana siempre, sin tocar el disco.</summary>
        UserOverride = 1
        ''' <summary>Salió de <see cref="CandidateFolders"/> y el archivo existe.</summary>
        AutoTable = 2
    End Enum

    ''' <summary>Build del juego. Sale del NOMBRE del exe, no de qué carpeta exista.</summary>
    Public Enum GameVariant
        Unknown = 0
        Flat = 1
        VR = 2
    End Enum

    ''' <summary>Resultado completo de una resolución. Inmutable en la práctica: lo produce
    ''' <see cref="Resolve"/> y la UI sólo lo lee.</summary>
    Public NotInheritable Class GamePaths
        ''' <summary>Juego contra el que se resolvió (el de la config, no el del exe).</summary>
        Public Property Game As Config_App.Game_Enum
        Public Property ExeVariant As GameVariant = GameVariant.Unknown

        ''' <summary>El nombre de carpeta elegido ("Skyrim Special Edition GOG", …), o "" si no se resolvió
        ''' o si el usuario fijó rutas a mano (que pueden no compartir nombre).</summary>
        Public Property FolderName As String = ""

        ''' <summary>Ruta COMPLETA del archivo Plugins.txt. "" cuando no se resolvió.</summary>
        Public Property PluginsTxtPath As String = ""
        Public Property PluginsTxtOrigin As PathOrigin = PathOrigin.NotResolved

        ''' <summary>Carpeta que contiene los .ini del juego. "" cuando no se resolvió.</summary>
        Public Property IniDir As String = ""
        Public Property IniDirOrigin As PathOrigin = PathOrigin.NotResolved

        ''' <summary>Texto en inglés, para el usuario, explicando qué falta. "" cuando está todo resuelto.</summary>
        Public Property Problem As String = ""

        ''' <summary>Carpeta del Plugins.txt — de ahí sale también loadorder.txt. Se DERIVA del archivo (y no
        ''' al revés) porque un override puede apuntar al Plugins.txt de un perfil de mod manager, que vive
        ''' en cualquier lado; su loadorder.txt es el que está al lado de ÉL.</summary>
        Public ReadOnly Property PluginsDir As String
            Get
                If PluginsTxtPath = "" Then Return ""
                Return If(Path.GetDirectoryName(PluginsTxtPath), "")
            End Get
        End Property

        Public ReadOnly Property HasPluginsTxt As Boolean
            Get
                Return PluginsTxtOrigin <> PathOrigin.NotResolved AndAlso PluginsTxtPath <> ""
            End Get
        End Property

        Public ReadOnly Property HasIniDir As Boolean
            Get
                Return IniDirOrigin <> PathOrigin.NotResolved AndAlso IniDir <> ""
            End Get
        End Property

        ''' <summary>Línea para la barra de estado de la UI. Dice SIEMPRE de dónde salió el valor: un usuario
        ''' que ve "Auto" y otro que ve "Set by you" están mirando problemas distintos.</summary>
        Public ReadOnly Property StatusLine As String
            Get
                If Problem <> "" Then Return "⚠ " & Problem
                Dim src = If(PluginsTxtOrigin = PathOrigin.UserOverride, "set by you", "auto")
                If FolderName <> "" AndAlso PluginsTxtOrigin = PathOrigin.AutoTable Then
                    Return $"Auto — ""{FolderName}"" (game folder detected)."
                End If
                Return $"Load order {src}."
            End Get
        End Property
    End Class

    ' ==========================================================================================
    ' Tabla — lo ÚNICO que se adivina, y sólo con nombres VERIFICADOS
    ' ==========================================================================================

    ''' <summary>Nombres de carpeta candidatos, en orden de preferencia, por (juego, variante).
    '''
    ''' <para>Acá SÓLO entran nombres verificados. Un nombre inventado es peor que ninguno: como la
    ''' elección se decide por existencia del archivo, una entrada falsa nunca acierta pero sí puede
    ''' convertir un caso limpio en un empate ambiguo, y encima le da al lector la impresión de que la
    ''' variante está soportada y probada.</para>
    '''
    ''' <list type="bullet">
    ''' <item><c>Skyrim Special Edition</c> / <c>Skyrim VR</c> / <c>Fallout4</c> / <c>Fallout4VR</c>:
    ''' los nombres de carpeta observados para cada variante, contrastados contra una instalación de
    ''' Steam real.</item>
    ''' <item><c>Skyrim Special Edition GOG</c>: la edición de GOG renombró las DOS carpetas. Las
    ''' herramientas de terceros no suelen manejar este caso (la salida típica es pedirle al usuario que
    ''' indique las rutas a mano), y es exactamente el caso que motivó todo esto.</item>
    ''' </list>
    '''
    ''' <para>NO están Epic, Microsoft Store ni un eventual Fallout 4 de GOG porque no los pude verificar.
    ''' Caen en el selector, que es la respuesta honesta.</para></summary>
    Private Shared Function CandidateFolders(game As Config_App.Game_Enum, gameVariant As GameVariant) As String()
        If game = Config_App.Game_Enum.Skyrim Then
            If gameVariant = GameVariant.VR Then Return New String() {"Skyrim VR"}
            Return New String() {"Skyrim Special Edition", "Skyrim Special Edition GOG"}
        End If
        If gameVariant = GameVariant.VR Then Return New String() {"Fallout4VR"}
        Return New String() {"Fallout4"}
    End Function

    ''' <summary>Nombre base de los .ini. NO lleva el sufijo de variante que sí lleva la carpeta: un FO4VR
    ''' sigue leyendo <c>Fallout4.ini</c> y un SkyrimVR sigue leyendo <c>Skyrim.ini</c>. Medido además sobre
    ''' el propio exe, donde las dos constantes viven contiguas:
    ''' <c>'Skyrim Special Edition'\0'Skyrim'\0'Skyrim.INI'</c>.</summary>
    Public Shared Function IniBaseName(game As Config_App.Game_Enum) As String
        Return If(game = Config_App.Game_Enum.Skyrim, "Skyrim", "Fallout4")
    End Function

    ''' <summary>Los cuatro exe canónicos y qué significan. Es el ÚNICO discriminador de variante: lo decide
    ''' el nombre del ejecutable, no qué carpeta exista.</summary>
    Private Shared ReadOnly CanonicalExes As (Name As String, Game As Config_App.Game_Enum, ExeVariant As GameVariant)() = {
        ("Fallout4.exe", Config_App.Game_Enum.Fallout4, GameVariant.Flat),
        ("Fallout4VR.exe", Config_App.Game_Enum.Fallout4, GameVariant.VR),
        ("SkyrimSE.exe", Config_App.Game_Enum.Skyrim, GameVariant.Flat),
        ("SkyrimVR.exe", Config_App.Game_Enum.Skyrim, GameVariant.VR)
    }

    ' ==========================================================================================
    ' Identificación del exe
    ' ==========================================================================================

    ''' <summary>Qué juego y qué variante declara el exe configurado. <c>Unknown</c> cuando no se pudo
    ''' establecer.
    '''
    ''' <para>Si el nombre configurado no es uno de los cuatro canónicos, se prueban los cuatro EN LA MISMA
    ''' CARPETA. Son cuatro <see cref="File.Exists"/> contra rutas armadas, no una enumeración. Cubre el
    ''' caso real de que el usuario apunte a <c>f4se_loader.exe</c>, <c>skse64_loader.exe</c> o
    ''' <c>Fallout4Launcher.exe</c>, donde el discriminador viejo (<c>EndsWith("VR")</c> sobre la ruta
    ''' configurada) devolvía cualquier cosa.</para></summary>
    Public Shared Function IdentifyExe(exePath As String) As (Game As Config_App.Game_Enum, ExeVariant As GameVariant)
        Dim none = (Config_App.Game_Enum.Fallout4, GameVariant.Unknown)
        If String.IsNullOrWhiteSpace(exePath) Then Return none

        Dim fileName = Path.GetFileName(exePath)
        For Each c In CanonicalExes
            If String.Equals(fileName, c.Name, StringComparison.OrdinalIgnoreCase) Then Return (c.Game, c.ExeVariant)
        Next

        Dim dir As String
        Try
            dir = Path.GetDirectoryName(exePath)
        Catch
            Return none
        End Try
        If String.IsNullOrEmpty(dir) Then Return none

        For Each c In CanonicalExes
            If File.Exists(Path.Combine(dir, c.Name)) Then Return (c.Game, c.ExeVariant)
        Next
        Return none
    End Function

    ''' <summary>True cuando el exe configurado es el build de VR. Reemplaza al viejo
    ''' <c>EndsWith("VR")</c>: ahora sale de <see cref="IdentifyExe"/>, así que un
    ''' <c>skse64_loader.exe</c> al lado de <c>SkyrimVR.exe</c> también da True.</summary>
    Public Shared Function IsVrBuild() As Boolean
        Return IdentifyExe(ConfiguredExePath()).ExeVariant = GameVariant.VR
    End Function

    ''' <summary>Instalación empaquetada de Microsoft Store / Game Pass. Ahí el juego escribe bajo
    ''' <c>%LOCALAPPDATA%\Packages\&lt;PFN&gt;\LocalCache\Local\…</c>, y el <c>&lt;PFN&gt;</c> no se puede
    ''' obtener sin enumerar. Se DETECTA para poder decirlo en el mensaje, pero no se adivina: cae en el
    ''' selector con el caso nombrado.</summary>
    Private Shared Function IsPackagedStoreInstall(exePath As String) As Boolean
        If String.IsNullOrWhiteSpace(exePath) Then Return False
        Return exePath.Contains("\ModifiableWindowsApps\", StringComparison.OrdinalIgnoreCase) OrElse
               exePath.Contains("\WindowsApps\", StringComparison.OrdinalIgnoreCase)
    End Function

    ' ==========================================================================================
    ' Resolución
    ' ==========================================================================================

    Private Shared ReadOnly _cacheLock As New Object()
    Private Shared _cacheKey As String = Nothing
    Private Shared _cached As GamePaths = Nothing

    Private Shared Function ConfiguredExePath() As String
        Return If(Config_App.Current?.FO4ExePath, "")
    End Function

    ''' <summary>Tira la memoización. La llaman los setters de los overrides y el cambio de exe/juego; la
    ''' clave de caché igual incluye esos tres valores, así que esto es un cinturón sobre los tiradores.</summary>
    Public Shared Sub Invalidate()
        SyncLock _cacheLock
            _cacheKey = Nothing
            _cached = Nothing
        End SyncLock
    End Sub

    ''' <summary>Las rutas del juego ACTIVO (el de <c>Config_App.Current.Game</c>). Memoizado por
    ''' (exe, juego, overrides): mientras no cambie ninguno de los tres, no se vuelve a tocar el disco.
    ''' Nunca tira excepción — todo problema vuelve como <see cref="GamePaths.Problem"/>.</summary>
    Public Shared Function Resolve() As GamePaths
        Dim cfg = Config_App.Current
        Dim game = If(cfg IsNot Nothing, cfg.Game, Config_App.Game_Enum.Skyrim)
        Dim exePath = ConfiguredExePath()
        Dim ovPlugins = If(cfg?.ActivePluginsTxtOverride(), "")
        Dim ovIni = If(cfg?.ActiveGameIniDirOverride(), "")

        Dim key = $"{CInt(game)}|{exePath}|{ovPlugins}|{ovIni}"
        SyncLock _cacheLock
            If _cached IsNot Nothing AndAlso String.Equals(_cacheKey, key, StringComparison.Ordinal) Then Return _cached
        End SyncLock

        Dim res = ResolveUncached(game, exePath, ovPlugins, ovIni)

        SyncLock _cacheLock
            _cacheKey = key
            _cached = res
        End SyncLock
        Return res
    End Function

    Private Shared Function ResolveUncached(game As Config_App.Game_Enum, exePath As String,
                                            ovPlugins As String, ovIni As String) As GamePaths
        Dim r As New GamePaths With {.Game = game}
        Try
            ' --- 1. Overrides. Ganan siempre y no cuestan un solo acceso a disco. Se aplican por separado
            '        porque son dos slots independientes: alguien puede necesitar fijar sólo el Plugins.txt.
            If ovPlugins <> "" Then
                r.PluginsTxtPath = ovPlugins
                r.PluginsTxtOrigin = PathOrigin.UserOverride
            End If
            If ovIni <> "" Then
                r.IniDir = ovIni
                r.IniDirOrigin = PathOrigin.UserOverride
            End If
            If r.HasPluginsTxt AndAlso r.HasIniDir Then Return r

            ' --- 2. El exe dice juego y variante.
            Dim ident = IdentifyExe(exePath)
            r.ExeVariant = ident.ExeVariant

            If ident.ExeVariant = GameVariant.Unknown Then
                r.Problem = If(exePath = "",
                    "The game executable is not set, so the load order location is unknown.",
                    "The configured executable is not a recognised game exe, so the load order location is unknown.")
                Return r
            End If

            ' El juego lo manda el selector, NO el exe: es el que decide el layout de records que se va a
            ' parsear. Si discrepan, resolver igual sería leer el Plugins.txt del juego equivocado — un dato
            ' mal, callado. Se corta acá y se pide que lo resuelva el usuario.
            If ident.Game <> game Then
                r.Problem = "The selected game and the configured executable disagree, so the paths were not " &
                            "resolved automatically. Fix the game selector or set the paths by hand."
                Return r
            End If

            If IsPackagedStoreInstall(exePath) Then
                r.Problem = "This is a Microsoft Store / Game Pass install, which keeps its files under a " &
                            "package folder that cannot be located automatically. Pick Plugins.txt by hand."
                Return r
            End If

            ' --- 3. Un nombre, dos raíces. Se decide UNA vez (la constante del motor es la misma para las dos
            '        carpetas) y sólo con rutas directas.
            Dim named = ResolveFolderName(game, ident.ExeVariant)
            r.FolderName = named.Name

            If named.Name = "" Then
                r.Problem = If(named.Ambiguous,
                    "More than one installation of this game was found and none can be preferred over the " &
                    "other, so the paths were not guessed. Pick Plugins.txt by hand.",
                    "The game's load order folder was not found where this game normally keeps it. If this is " &
                    "a GOG / Epic / Store edition, or the game is managed by a mod manager, pick Plugins.txt by hand.")
                Return r
            End If

            If Not r.HasPluginsTxt Then
                r.PluginsTxtPath = Path.Combine(LocalAppDataRoot(), named.Name, "Plugins.txt")
                r.PluginsTxtOrigin = PathOrigin.AutoTable
            End If
            If Not r.HasIniDir Then
                r.IniDir = Path.Combine(MyGamesRoot(), named.Name)
                r.IniDirOrigin = PathOrigin.AutoTable
            End If
            Return r

        Catch ex As Exception
            ' La resolución de rutas no puede tumbar el arranque de la app. Un fallo se reporta como
            ' "no resuelto" y el usuario lo arregla con el selector.
            r.Problem = "The game paths could not be resolved: " & ex.Message
            Return r
        End Try
    End Function

    ''' <summary>Elige el nombre de carpeta probando los candidatos de la tabla contra rutas DIRECTAS.
    '''
    ''' <para>Manda el <c>Plugins.txt</c>, que es el archivo cuya ausencia rompe todo. Si ningún candidato lo
    ''' tiene, se prueba el .ini como segundo testigo del mismo nombre — cubre al usuario que todavía no
    ''' corrió el juego (no hay Plugins.txt) pero sí abrió el launcher (que sí escribe los .ini).</para>
    '''
    ''' <para>Dos aciertos = empate y NO se desempata. Un usuario con Steam y GOG instalados a la vez tiene
    ''' dos load orders reales y distintos: elegir uno a dedo es exactamente el error que esta clase vino a
    ''' borrar. Se pregunta.</para></summary>
    Private Shared Function ResolveFolderName(game As Config_App.Game_Enum,
                                              gameVariant As GameVariant) As (Name As String, Ambiguous As Boolean)
        Dim candidates = CandidateFolders(game, gameVariant)

        Dim localRoot = LocalAppDataRoot()
        Dim hits As New List(Of String)
        If localRoot <> "" Then
            For Each c In candidates
                If File.Exists(Path.Combine(localRoot, c, "Plugins.txt")) Then hits.Add(c)
            Next
        End If
        If hits.Count = 1 Then Return (hits(0), False)
        If hits.Count > 1 Then Return ("", True)

        Dim myGames = MyGamesRoot()
        If myGames = "" Then Return ("", False)
        Dim iniName = IniBaseName(game) & ".ini"
        hits.Clear()
        For Each c In candidates
            If File.Exists(Path.Combine(myGames, c, iniName)) Then hits.Add(c)
        Next
        If hits.Count = 1 Then Return (hits(0), False)
        Return ("", hits.Count > 1)
    End Function

    ''' <summary>Raíz de <c>%LOCALAPPDATA%</c>. Es una consulta al shell, no un acceso a disco.</summary>
    Private Shared Function LocalAppDataRoot() As String
        Try
            Return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
        Catch
            Return ""
        End Try
    End Function

    ''' <summary>Raíz de <c>Documents\My Games</c>. <c>MyDocuments</c> respeta la redirección (OneDrive,
    ''' política de dominio), que es justo lo que hay que respetar acá.</summary>
    Private Shared Function MyGamesRoot() As String
        Try
            Dim docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            If docs = "" Then Return ""
            Return Path.Combine(docs, "My Games")
        Catch
            Return ""
        End Try
    End Function

    ''' <summary>Ruta completa de un .ini (<c>"Skyrim.ini"</c>, <c>"Fallout4Custom.ini"</c>, …), o "" si no
    ''' hay carpeta de inis resuelta.
    '''
    ''' <para>Conserva la regla extra de VR: los juegos VR no crean el .ini en My Games por defecto, así que
    ''' en un build de VR, si el .ini no está ahí, se cae a la raíz del juego — la carpeta que contiene
    ''' Data, o sea la del propio exe.</para></summary>
    Public Shared Function ResolveIniPath(iniFileName As String) As String
        Dim r = Resolve()
        If Not r.HasIniDir Then Return ""

        Dim myGamesPath = Path.Combine(r.IniDir, iniFileName)
        If File.Exists(myGamesPath) OrElse r.ExeVariant <> GameVariant.VR Then Return myGamesPath

        Dim exePath = ConfiguredExePath()
        If exePath = "" Then Return myGamesPath
        Dim gameRoot As String
        Try
            gameRoot = Path.GetDirectoryName(exePath)
        Catch
            Return myGamesPath
        End Try
        If String.IsNullOrEmpty(gameRoot) Then Return myGamesPath

        Dim gameRootPath = Path.Combine(gameRoot, iniFileName)
        If File.Exists(gameRootPath) Then Return gameRootPath
        Return myGamesPath
    End Function

End Class
