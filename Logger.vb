' Version Uploaded of Fo4Library 3.2.0
Imports System.IO

''' <summary>
''' Logger thread-safe que mantiene un único FileStream abierto con WriteThrough,
''' escribe con AutoFlush y cierra al terminar el proceso.
''' </summary>
Public NotInheritable Class Logger
    Private Shared ReadOnly _lock As New Object()
    Private Shared _writer As StreamWriter
    Public Shared Property Enabled As Boolean = False
    ''' <summary>
    ''' Inicializa el logger con la ruta de archivo. Abre un FileStream con WriteThrough.
    ''' Debe llamarse una sola vez antes de usar Log().
    ''' </summary>
    Public Shared Sub Initialize(filePath As String)
        If Enabled = False Then Exit Sub
        SyncLock _lock
            If _writer IsNot Nothing Then
                Exit Sub
            End If
            ' Asegurar que la carpeta existe
            Dim dir = Path.GetDirectoryName(filePath)
            If Not String.IsNullOrEmpty(dir) AndAlso Not Directory.Exists(dir) Then
                Directory.CreateDirectory(dir)
            End If
            ' Abrir FileStream en modo Append, permitiendo lectura y eliminación de archivo,
            ' con WriteThrough para persistir inmediatamente en disco y minimizar cache del SO
            Dim fs As New FileStream(
                filePath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.ReadWrite Or FileShare.Delete,
                bufferSize:=8192,
                options:=FileOptions.WriteThrough)
            _writer = New StreamWriter(fs) With {
                .AutoFlush = True
            }
            ' Registrar cierre al terminar el proceso
            AddHandler AppDomain.CurrentDomain.ProcessExit, AddressOf OnProcessExit
        End SyncLock
    End Sub

    ''' <summary>
    ''' Escribe un mensaje con timestamp en el log de forma thread-safe.
    ''' </summary>
    Public Shared Sub Log(message As String)
        If Enabled = False Then Exit Sub
        Dim timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fffZ")
        Dim line = $"[{timestamp}] {message}"
        SyncLock _lock
            If _writer Is Nothing Then
                Throw New InvalidOperationException("Logger no inicializado.")
            End If
            _writer.WriteLine(line)
        End SyncLock
    End Sub

    ''' <summary>
    ''' Cierra el StreamWriter y FileStream. Invocado automáticamente al salir del proceso.
    ''' </summary>
    Private Shared Sub OnProcessExit(sender As Object, e As EventArgs)
        If Enabled = False Then Exit Sub
        SyncLock _lock
            If _writer IsNot Nothing Then
                Try
                    _writer.Close()
                    _writer.Dispose()
                Catch
                    ' Ignorar errores de cierre
                Finally
                    _writer = Nothing
                End Try
            End If
        End SyncLock
    End Sub

    ' Evitar instanciación
    Private Sub New()
    End Sub
End Class
