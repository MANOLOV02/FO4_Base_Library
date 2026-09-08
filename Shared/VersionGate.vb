Imports System.Diagnostics
Imports System.IO
Imports System.Linq
Imports System.Reflection
Imports System.Reflection.Metadata
Imports System.Reflection.PortableExecutable
Imports System.Runtime.InteropServices
Imports System.Text
Imports System.Windows.Forms

''' <summary>Detecta la INSTALACIÓN MEZCLADA — DLL de distintas releases conviviendo en la carpeta del exe —
''' antes de que la app arranque, y la reporta en criollo en vez de dejarla explotar más tarde.
'''
''' <para>EL CASO QUE VINO A CUBRIR (reporte de un usuario, 2026-09-06): un
''' <c>NPC_Manager_FO4.dll</c> anterior al 14-jul junto a un <c>FO4_Base_Library.dll</c> posterior. El exe
''' llamaba a <c>Fill_DictionaryAsync</c> con la firma de 4 parámetros de entonces; la librería en disco ya
''' tenía la de 5 (se le agregó <c>loadedPlugins</c> en el commit 93929e0). VB hornea los argumentos
''' opcionales EN EL SITIO DE LLAMADA, así que el exe pedía un método que ya no existe y el proceso moría con
''' <c>MissingMethodException</c> al apretar OK en el Preflight, con la app entera ya levantada.</para>
'''
''' <para>⛔ POR QUÉ NO ALCANZA CON PONERLE <c>AssemblyVersion</c> A TODO. MEDIDO en un par lib+exe de prueba
''' (net8.0, VB, sin strong name, igual que acá): con el exe compilado contra la 1.0.0.0 y una lib 2.0.0.0 en
''' la carpeta, .NET 8 <b>hace roll-forward y carga la mayor sin decir una palabra</b> — corre y después
''' revienta igual. Sólo la dirección inversa (lib MENOR que la pedida) falla sola, y encima con un
''' <c>FileNotFoundException</c> que dice "no se puede encontrar el archivo" cuando el archivo está ahí. O sea
''' que la versión sola no ataja ni el caso reportado ni comunica el otro: hace falta MIRAR y AVISAR.</para>
'''
''' <para>FUENTE COMPARTIDA, ENSAMBLADO PROPIO — el mismo contrato que <c>CrashReport</c>, y por el mismo
''' motivo elevado al cuadrado: uno de los dos modos de falla que esto detecta es que
''' <c>FO4_Base_Library.dll</c> NO CARGUE. Un chequeo que viviera adentro de la librería se caería junto con
''' ella justo cuando hace falta. Por eso vive en <c>Shared\</c>, NO se compila dentro de la lib
''' (<c>&lt;Compile Remove="Shared\**"&gt;</c>), cada app lo LINKEA, y no usa NADA fuera del framework.</para>
'''
''' <para>CÓMO MIDE, y por qué así:
''' <list type="bullet">
''' <item>Lee los manifiestos con <c>PEReader</c>, SIN cargar: en la dirección "lib más vieja" cargarla es
''' justamente lo que tira, y el aviso tiene que salir igual. MEDIDO: el chequeo habla y recién después,
''' al usarla, aparece el <c>FileNotFoundException</c>.</item>
''' <item>Arranca por el <b>.dll</b> del entry assembly, no por el .exe. MEDIDO: el .exe de una app .NET es
''' un apphost NATIVO y <c>PEReader.HasMetadata</c> da False.</item>
''' <item>Sigue sólo las referencias cuyo archivo está EN LA CARPETA. Las del framework viven en
''' <c>C:\Program Files\dotnet\shared</c> y quedan afuera solas, sin lista negra de nombres que mantener.</item>
''' <item>Recorre el cierre TRANSITIVO desde el entry assembly, no la carpeta entera: así una app vieja de la
''' suite tirada en la misma carpeta (el <c>Tools\Win64</c> del reporte tiene varias) no le traba el arranque
''' a otra que está sana, pero sí se caza el par lib↔helpers, que ningún exe referencia directo.</item>
''' <item>Lo que compara es lo que el compilador REALMENTE emitió: una referencia que no se usa no queda en
''' el manifiesto (MEDIDO), así que acá no hay falsos positivos por referencias de adorno.</item>
''' </list></para>
'''
''' <para>Nada de acá puede tirar: un chequeo de instalación que se cae impide arrancar una instalación
''' sana. Cualquier excepción se traga y devuelve "seguí".</para></summary>
Friend Module VersionGate

    Private _console As Boolean

    ''' <summary>Los modos headless (<c>--bake-all</c>, <c>--build</c>, el CLI) avisan por stderr: un
    ''' MessageBox en una corrida sin nadie mirando cuelga el proceso hasta que alguien lo cierre. Mismo
    ''' criterio que <see cref="CrashReport.UseConsole"/>.</summary>
    Friend Sub UsarConsola()
        _console = True
    End Sub

    ''' <summary>True = la instalación es pareja, seguí. False = ya se avisó y el que llama tiene que ABORTAR
    ''' el arranque (cada app aborta como le corresponde: <c>Return</c> en su Main, <c>e.Cancel</c> en el
    ''' Startup del framework de VB).
    ''' <para>⛔ VA EN EL PRIMER RENGLÓN DEL ARRANQUE, y el que llama no puede tocar ningún DLL propio antes:
    ''' el JIT resuelve las referencias del cuerpo ENTERO de un método antes de correr su primera línea. Es el
    ''' mismo contrato que ya cumplen <c>Program.Main</c> y <c>MyApplication_Startup</c> con
    ''' <c>RealMain</c>/<c>ArranqueReal</c>.</para></summary>
    Friend Function VerificarInstalacion() As Boolean
        Try
            Dim carpeta = AppContext.BaseDirectory
            Dim mezclas = InspeccionarCarpeta(carpeta, RutaDelEntry(carpeta))
            If mezclas.Count = 0 Then Return True
            Avisar(mezclas)
            Return False
        Catch
            ' Un chequeo de instalación que se cae no puede ser el que impida arrancar.
            Return True
        End Try
    End Function

    ''' <summary>La versión para mostrar en el título de la ventana: la MISMA que gobierna el binding
    ''' (<c>AssemblyVersion</c>), no la informacional, para que el número que el usuario nos dicta en un
    ''' reporte sea exactamente el que compara este chequeo. Se recorta el 4º campo cuando es 0, que es el
    ''' caso normal (<c>&lt;Version&gt;2.1.2&lt;/Version&gt;</c> ⇒ <c>2.1.2.0</c>).</summary>
    Friend Function VersionDelExe() As String
        Try
            Dim v = Assembly.GetEntryAssembly()?.GetName().Version
            If v Is Nothing Then Return ""
            If v.Revision = 0 Then Return $"{v.Major}.{v.Minor}.{v.Build}"
            Return v.ToString()
        Catch
            Return ""
        End Try
    End Function

    ''' <summary>El título de una ventana con la versión pegada — UNA sola ley para las cuatro apps, y el
    ''' número que muestra es el MISMO que compara <see cref="VerificarInstalacion"/>, así lo que el usuario nos dicta en un
    ''' reporte se puede confrontar directo con el DLL que tiene en la carpeta.</summary>
    Friend Function TituloConVersion(titulo As String) As String
        Dim v = VersionDelExe()
        If v = "" Then Return titulo
        Return titulo & "  v" & v
    End Function

    ''' <summary>Una referencia que no coincide con el archivo que está en la carpeta.</summary>
    Friend Structure MezclaDeVersiones
        Public Quien As String      ' el archivo que pide
        Public Que As String        ' el assembly pedido
        Public Pide As Version
        Public Hay As Version
    End Structure

    ''' <summary>La ley, sobre una carpeta CUALQUIERA: <see cref="VerificarInstalacion"/> le pasa la del
    ''' proceso, y <c>Tools\InstalacionMezcladaGate</c> le pasa la salida de cada app para probarla contra
    ''' binarios de verdad. ⛔ Es a propósito que el gate no tenga su propia copia de esto: dos
    ''' implementaciones de la misma comparación se separan y el gate termina certificando otra cosa.</summary>
    Friend Function InspeccionarCarpeta(carpeta As String, rutaEntry As String) As List(Of MezclaDeVersiones)
        Dim salida As New List(Of MezclaDeVersiones)
        If String.IsNullOrEmpty(carpeta) OrElse Not Directory.Exists(carpeta) Then Return salida

        ' ⛔ SÓLO SE JUZGAN LOS DLL DE LA MISMA EMPRESA QUE EL EXE, Y ESO NO ES COSMÉTICO: las versiones de
        ' los paquetes de terceros las resuelve NuGet, no nosotros, y es NORMAL que un paquete quede anotado
        ' pidiendo una versión distinta de la que termina copiada (NuGet unifica al mayor del grafo). MEDIDO
        ' en la carpeta de NPC Manager: de 22 DLL administrados, 18 son ajenos, y ahí conviven
        ' `OpenTK.GLControl 4.0.2` con `OpenTK.* 4.9.3`, más `System.IO.Pipelines 6.0.0.0`. Comparando esos a
        ' rajatabla, el día que un paquete se actualice el gate le TRABA EL ARRANQUE a un usuario sano — que
        ' es peor que el bug que vino a evitar.
        ' La empresa sale del `Directory.Build.props` de cada repo, así que esto sigue siendo DERIVADO: no hay
        ' lista de nombres propios que mantener. Y se compara contra la del PROPIO exe, no contra una constante.
        Dim empresaPropia = EmpresaDe(rutaEntry)
        If empresaPropia = "" Then Return salida   ' sin empresa no se puede separar lo nuestro: no se juzga nada

        ' Índice de lo NUESTRO que viaja en el paquete: nombre de assembly -> (archivo, versión).
        Dim enCarpeta As New Dictionary(Of String, (Ruta As String, Ver As Version))(StringComparer.OrdinalIgnoreCase)
        For Each ruta In Directory.EnumerateFiles(carpeta, "*.dll")
            If Not String.Equals(EmpresaDe(ruta), empresaPropia, StringComparison.Ordinal) Then Continue For
            Dim m = LeerManifiesto(ruta)
            If m.Nombre IsNot Nothing AndAlso Not enCarpeta.ContainsKey(m.Nombre) Then
                enCarpeta(m.Nombre) = (ruta, m.Ver)
            End If
        Next
        If enCarpeta.Count = 0 Then Return salida

        ' Cierre transitivo desde el entry assembly.
        Dim pendientes As New Queue(Of String)()
        Dim vistos As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        If String.IsNullOrEmpty(rutaEntry) OrElse Not File.Exists(rutaEntry) Then Return salida
        pendientes.Enqueue(rutaEntry)

        While pendientes.Count > 0
            Dim ruta = pendientes.Dequeue()
            If Not vistos.Add(ruta) Then Continue While
            Dim m = LeerManifiesto(ruta)
            If m.Refs Is Nothing Then Continue While
            For Each r In m.Refs
                Dim destino As (Ruta As String, Ver As Version) = Nothing
                If Not enCarpeta.TryGetValue(r.Nombre, destino) Then Continue For  ' framework o ajeno
                If Not r.Ver.Equals(destino.Ver) Then
                    salida.Add(New MezclaDeVersiones With {.Quien = Path.GetFileName(ruta), .Que = r.Nombre,
                                                .Pide = r.Ver, .Hay = destino.Ver})
                End If
                pendientes.Enqueue(destino.Ruta)
            Next
        End While
        Return salida
    End Function

    ''' <summary>El .dll administrado del proceso. <c>Assembly.GetEntryAssembly().Location</c> es lo correcto;
    ''' el fallback por nombre de proceso cubre el caso en que venga vacío.</summary>
    Private Function RutaDelEntry(carpeta As String) As String
        Try
            Dim loc = Assembly.GetEntryAssembly()?.Location
            If Not String.IsNullOrEmpty(loc) AndAlso File.Exists(loc) Then Return loc
        Catch
        End Try
        Try
            Dim p = Environment.ProcessPath
            If Not String.IsNullOrEmpty(p) Then
                Dim dll = Path.Combine(carpeta, Path.GetFileNameWithoutExtension(p) & ".dll")
                If File.Exists(dll) Then Return dll
            End If
        Catch
        End Try
        Return Nothing
    End Function

    ''' <summary>La empresa que declara el PE (recurso de versión de Win32, no metadata administrada: sirve
    ''' igual para un DLL nativo y no hay que decodificar blobs de atributos). Sale del
    ''' <c>&lt;Company&gt;</c> del <c>Directory.Build.props</c> de cada repo.</summary>
    Private Function EmpresaDe(ruta As String) As String
        Try
            If String.IsNullOrEmpty(ruta) OrElse Not File.Exists(ruta) Then Return ""
            Return If(FileVersionInfo.GetVersionInfo(ruta).CompanyName, "").Trim()
        Catch
            Return ""
        End Try
    End Function

    ''' <summary>Nombre, versión y referencias de un PE, sin cargarlo en el runtime.</summary>
    Private Function LeerManifiesto(ruta As String) As (Nombre As String, Ver As Version, Refs As List(Of (Nombre As String, Ver As Version)))
        Try
            Using fs As New FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite),
                  pe As New PEReader(fs)
                If Not pe.HasMetadata Then Return (Nothing, Nothing, Nothing)   ' apphost nativo, recurso, etc.
                Dim md = pe.GetMetadataReader()
                If Not md.IsAssembly Then Return (Nothing, Nothing, Nothing)    ' módulo suelto
                Dim def = md.GetAssemblyDefinition()
                Dim refs As New List(Of (Nombre As String, Ver As Version))
                For Each h In md.AssemblyReferences
                    Dim ar = md.GetAssemblyReference(h)
                    refs.Add((md.GetString(ar.Name), ar.Version))
                Next
                Return (md.GetString(def.Name), def.Version, refs)
            End Using
        Catch
            ' Un .dll nativo, en uso exclusivo, o corrupto: no se juzga.
            Return (Nothing, Nothing, Nothing)
        End Try
    End Function

    ''' <summary>TEXTO EN INGLÉS Y EN ASCII: es lo que el usuario copia y pega en el foro o en Nexus. Mismo
    ''' criterio que el cuerpo del crash report.</summary>
    Private Sub Avisar(mezclas As List(Of MezclaDeVersiones))
        Dim app = NombreDeApp()
        Dim sb As New StringBuilder()
        sb.AppendLine("Mixed installation.")
        sb.AppendLine()
        sb.AppendLine("This folder has files from more than one release, so " & app & " will not start:")
        sb.AppendLine()
        For Each m In mezclas
            sb.AppendLine($"    {m.Quien} needs {m.Que} {m.Pide}, but the folder has {m.Hay}")
        Next
        sb.AppendLine()
        sb.AppendLine("Every file must come from the same release. Reinstall the complete package.")
        sb.AppendLine("If another tool of this suite shares this folder, update that one too.")
        sb.AppendLine()
        sb.AppendLine("Folder:")
        sb.AppendLine(AppContext.BaseDirectory)
        Dim cuerpo = sb.ToString()

        If _console Then
            Try
                AsegurarConsola()
                Console.Error.WriteLine(cuerpo)
            Catch
            End Try
            Return
        End If
        Try
            MessageBox.Show(cuerpo, app & " - mixed installation", MessageBoxButtons.OK, MessageBoxIcon.Error)
        Catch
        End Try
    End Sub

    Private Const ATTACH_PARENT_PROCESS As Integer = -1

    <DllImport("kernel32.dll", SetLastError:=True)>
    Private Function AttachConsole(dwProcessId As Integer) As Boolean
    End Function

    <DllImport("kernel32.dll", SetLastError:=True)>
    Private Function AllocConsole() As Boolean
    End Function

    ''' <summary>Las apps son <c>WinExe</c>: Windows NO les da consola, y el <c>Console.Out</c> que el BCL
    ''' cachea en el primer uso es el dispositivo nulo. Sin esto, el aviso de un <c>--bake-all</c> o un
    ''' <c>--build</c> con instalación mezclada se perdería en el aire.
    ''' <para>⛔ SE ENGANCHA ACÁ Y NO EN EL ARRANQUE DE CADA APP a propósito: esto corre sólo cuando el gate
    ''' YA decidió abortar, así que una corrida sana nunca ve una consola de más. El <c>EnsureConsole</c> de
    ''' cada app queda intacto — no llega a correr, porque el que llama vuelve enseguida.</para></summary>
    Private Sub AsegurarConsola()
        Try
            If Not AttachConsole(ATTACH_PARENT_PROCESS) Then AllocConsole()
            Dim utf8 As New UTF8Encoding(encoderShouldEmitUTF8Identifier:=False)
            Dim se = Console.OpenStandardError()
            If se IsNot Stream.Null Then Console.SetError(New StreamWriter(se, utf8) With {.AutoFlush = True})
        Catch
        End Try
    End Sub

    Private Function NombreDeApp() As String
        Try
            Dim p = Environment.ProcessPath
            If Not String.IsNullOrEmpty(p) Then Return Path.GetFileNameWithoutExtension(p)
        Catch
        End Try
        Return "This application"
    End Function

End Module
