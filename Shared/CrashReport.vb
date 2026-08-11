Imports System.IO
Imports System.Linq
Imports System.Text
Imports System.Windows.Forms

''' <summary>Reporte de caída del binario que se DISTRIBUYE: escribe <c>&lt;exe&gt;_crash.log</c> al lado del
''' exe (o en <c>%TEMP%</c> si esa carpeta no admite escritura) y avisa dónde quedó.
'''
''' <para>⛔ FUENTE COMPARTIDA, ENSAMBLADO PROPIO. Este archivo vive acá pero NO se compila dentro de
''' FO4_Base_Library (<c>&lt;Compile Remove="Shared\**"&gt;</c>): cada app lo LINKEA a su propio proyecto.
''' El motivo es concreto: el modo de falla que esto vino a cubrir es que NO CARGUE <c>FO4_Base_Library.dll</c>
''' (cuarentena del antivirus, extracción parcial). Un reporter que viviera dentro de la librería se caería
''' junto con ella y el usuario volvería al silencio. Al linkearlo, el reporter de cada exe es independiente
''' de la librería, y la ley sigue siendo UNA SOLA. Ver <c>Program.Main</c> de NPC_Manager.</para>
'''
''' <para>⛔ VA EN RELEASE Y NO PASA POR <c>Logger</c>. En Release <c>Logger.Enabled</c> queda en False y su
''' propio setter descarta cualquier True, así que la caída de un usuario no dejaría UN SOLO rastro. Este
''' camino no tiene gate: ni <c>#If DEBUG</c> ni opción de config.</para>
'''
''' <para>Nada de acá puede tirar: una excepción reportando una caída se lleva puesto el reporte.</para></summary>
Friend Module CrashReport

    Private ReadOnly _gate As New Object()
    Private _reported As Boolean
    Private _console As Boolean

    ''' <summary>Engancha el handler de último recurso (excepción no controlada en cualquier hilo). El proceso
    ''' igual termina: el evento no se puede cancelar — lo único que agrega es el log.</summary>
    Friend Sub Install()
        AddHandler AppDomain.CurrentDomain.UnhandledException,
            Sub(sender As Object, e As UnhandledExceptionEventArgs)
                Report(TryCast(e.ExceptionObject, Exception), "unhandled")
            End Sub
    End Sub

    ''' <summary>Los modos headless ya tienen consola: el aviso va por stderr y NO por un MessageBox, que en una
    ''' corrida sin nadie mirando (bake automatizado, CI) colgaría el proceso hasta que alguien lo cierre.</summary>
    Friend Sub UseConsole()
        _console = True
    End Sub

    Friend Sub Report(ex As Exception, origin As String)
        Try
            SyncLock _gate
                ' El proceso ya está terminando: vale el primer reporte. El guard existe para no mostrar DOS
                ' MessageBox cuando la misma caída llega por los dos caminos (handler de la app + AppDomain).
                If _reported Then Return
                _reported = True
            End SyncLock

            Dim app = AppName()
            Dim body = Compose(ex, origin, app)
            Dim written = TryWrite(AppContext.BaseDirectory, app, body)
            If written = "" Then written = TryWrite(IO.Path.GetTempPath(), app, body)
            Notify(app, ex, written)
        Catch
            ' Reportar una caída no puede provocar otra.
        End Try
    End Sub

    ''' <summary>Nombre del exe, que es lo que separa a las apps que comparten esta fuente y además nombra el
    ''' archivo — un <c>Wardrobe_Manager_crash.log</c> no se confunde con el de NPC Manager si el usuario los
    ''' tiene en la misma carpeta.</summary>
    Private Function AppName() As String
        Try
            Dim p = Environment.ProcessPath
            If Not String.IsNullOrEmpty(p) Then Return IO.Path.GetFileNameWithoutExtension(p)
        Catch
        End Try
        Return "Application"
    End Function

    Private Function Compose(ex As Exception, origin As String, app As String) As String
        Dim sb As New StringBuilder()
        ' ⛔ TEXTO PROPIO EN ASCII. Este archivo lo abre el usuario en cualquier editor y lo pega en un foro:
        ' un guion largo o un punto medio salen como mojibake apenas alguien lo lea en la codepage local, y un
        ' reporte ilegible no se pega. Lo que puede traer no-ASCII es el mensaje de la excepción, que viene del
        ' sistema — para eso el archivo se escribe con BOM (ver TryWrite).
        sb.AppendLine("================================================================================")
        sb.AppendLine($"{app} - fatal error   {Date.Now:yyyy-MM-dd HH:mm:ss} local / {Date.UtcNow:yyyy-MM-dd HH:mm:ss}Z")
        sb.AppendLine($"  origin  : {origin}")
        AppendSafe(sb, "  app dir ", Function() AppContext.BaseDirectory)
        AppendSafe(sb, "  args    ", Function() String.Join(" ", Environment.GetCommandLineArgs().Skip(1)))
        AppendSafe(sb, "  os      ", Function() $"{Environment.OSVersion.VersionString} | {If(Environment.Is64BitOperatingSystem, "x64", "x86")} OS | {If(Environment.Is64BitProcess, "x64", "x86")} process")
        AppendSafe(sb, "  runtime ", Function() Runtime.InteropServices.RuntimeInformation.FrameworkDescription)
        AppendSafe(sb, "  culture ", Function() Globalization.CultureInfo.CurrentCulture.Name)
        sb.AppendLine()
        sb.AppendLine("exception:")
        ' ToString y no Message: arrastra el tipo, la inner chain y el stack, que es lo único que identifica
        ' el sitio. El Event ID 1000 de Windows NO trae nada de esto (el 1026 sí, pero el usuario ve el 1000).
        sb.AppendLine(If(ex Is Nothing, "  (no Exception object was supplied)", ex.ToString()))
        sb.AppendLine()
        AppendAppFolder(sb)
        sb.AppendLine()
        Return sb.ToString()
    End Function

    ''' <summary>Cada dato de entorno va en su propio Try: si uno falla, el reporte sigue con los demás en vez
    ''' de perderse entero.</summary>
    Private Sub AppendSafe(sb As StringBuilder, label As String, value As Func(Of String))
        Try
            sb.AppendLine($"{label}: {value()}")
        Catch ex As Exception
            sb.AppendLine($"{label}: (unavailable - {ex.GetType().Name})")
        End Try
    End Sub

    ''' <summary>Inventario de la carpeta del exe. Es la mitad del diagnóstico cuando la excepción es un
    ''' <c>FileNotFoundException</c> de ensamblado: dice si el DLL falta de verdad, o si está y el problema es
    ''' otro (versión cruzada, archivo truncado). Sin esto hay que pedírselo al usuario en otro mensaje.</summary>
    Private Sub AppendAppFolder(sb As StringBuilder)
        Try
            sb.AppendLine("app folder (name | bytes | last write):")
            Dim files = New DirectoryInfo(AppContext.BaseDirectory).EnumerateFiles().
                Where(Function(f) f.Extension.Equals(".dll", StringComparison.OrdinalIgnoreCase) OrElse
                                  f.Extension.Equals(".exe", StringComparison.OrdinalIgnoreCase) OrElse
                                  f.Extension.Equals(".json", StringComparison.OrdinalIgnoreCase)).
                OrderBy(Function(f) f.Name, StringComparer.OrdinalIgnoreCase)
            For Each fi In files
                sb.AppendLine($"  {fi.Name,-50} {fi.Length,10}  {fi.LastWriteTime:yyyy-MM-dd HH:mm}")
            Next
        Catch ex As Exception
            sb.AppendLine($"  (could not be listed - {ex.GetType().Name}: {ex.Message})")
        End Try
    End Sub

    ''' <summary>Escribe en modo append y devuelve la ruta, o "" si esa carpeta no admite escritura. El fallback
    ''' a %TEMP% no es teórico: una instalación bajo Program Files, o Controlled Folder Access de Defender,
    ''' bloquean la escritura justo en las máquinas donde el log más falta hace.
    ''' <para>UTF-8 CON BOM: el mensaje de la excepción viene del sistema y en un Windows no inglés trae
    ''' acentos; sin BOM el editor del usuario lo abre en su codepage y el reporte llega ilegible. El append
    ''' no repite el BOM — sólo se emite cuando el archivo arranca vacío.</para></summary>
    Private Function TryWrite(folder As String, app As String, body As String) As String
        Try
            If String.IsNullOrEmpty(folder) Then Return ""
            Dim target = IO.Path.Combine(folder, app & "_crash.log")
            File.AppendAllText(target, body, New UTF8Encoding(encoderShouldEmitUTF8Identifier:=True))
            Return target
        Catch
            Return ""
        End Try
    End Function

    Private Sub Notify(app As String, ex As Exception, logPath As String)
        Dim head = If(ex Is Nothing, "Unknown fatal error.", $"{ex.GetType().Name}: {ex.Message}")
        Dim where = If(logPath = "",
                       "The crash report could NOT be written to disk (the app folder and %TEMP% both refused).",
                       $"A crash report was written to:{vbCrLf}{logPath}")
        If _console Then
            Try
                Console.Error.WriteLine("FATAL: " & head)
                Console.Error.WriteLine(where)
            Catch
            End Try
            Return
        End If
        Try
            MessageBox.Show($"{app} could not continue.{vbCrLf}{vbCrLf}{head}{vbCrLf}{vbCrLf}{where}{vbCrLf}{vbCrLf}" &
                            "Please attach that file when reporting this.",
                            $"{app} - fatal error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        Catch
        End Try
    End Sub

End Module
