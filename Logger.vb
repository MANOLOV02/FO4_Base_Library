' Version Uploaded of Fo4Library 3.2.0
Imports System.IO
Imports System.Collections.Concurrent
Imports System.Threading

''' <summary>
''' Logger thread-safe con writer thread y queue concurrente. Diseño:
'''   • <see cref="Log"/> y <see cref="LogLazy"/> NO toman lock — encolan a una
'''     <see cref="ConcurrentQueue(Of String)"/>.
'''   • Un writer thread dedicado drena la queue y escribe al StreamWriter;
'''     flushea por batch (o cuando expira el flush interval), no por línea.
'''   • <see cref="LogLazy"/> evalúa la lambda solo si <see cref="Enabled"/>=True,
'''     evitando interpolación de strings cuando off.
''' Los call sites en hot paths no serializan ni esperan I/O: no volver a un SyncLock con
''' AutoFlush/WriteThrough por línea.
''' </summary>
Public NotInheritable Class Logger
    ''' <summary>EN RELEASE ESTO NO SE PUEDE PRENDER. El setter DESCARTA cualquier <c>True</c> en un build
    ''' Release, asi que el logging (y todo lo que se gatea por <c>Logger.Enabled</c>) queda apagado por
    ''' CONSTRUCCION y no por que nadie se haya acordado de poner un <c>#If DEBUG</c> en el arranque.
    ''' <para>Motivo: <c>Logger.Enabled</c> no gobierna solo la escritura del archivo — es la compuerta de todos
    ''' los CALCULOS de diagnostico del codigo (censos por malla, histogramas, dumps por vertice). Un solo
    ''' <c>Logger.Enabled = True</c> perdido en un camino de release los enciende TODOS.</para>
    ''' <para>La UNICA forma de tener logging en Release es <see cref="AllowInReleaseBuilds"/>, que es explicita,
    ''' esta documentada y solo la usan las HERRAMIENTAS de linea de comandos. Ninguna de las tres apps
    ''' (NPC Manager, Wardrobe Manager, la libreria) la toca.</para></summary>
    Public Shared Property Enabled As Boolean
        Get
            Return _enabled
        End Get
        Set(value As Boolean)
#If DEBUG Then
            _enabled = value
#Else
            _enabled = value AndAlso _allowInRelease
#End If
        End Set
    End Property
    Private Shared _enabled As Boolean = False

    ''' <summary>ESCAPE HATCH SOLO PARA HERRAMIENTAS CLI, no para las apps. Habilita que
    ''' <see cref="Enabled"/> acepte <c>True</c> en un build Release.
    ''' <para>Existe porque el CLI (<c>FO4_FaceTint_CLI --buildfacegen</c>) usa <c>Logger.Enabled</c> como
    ''' interruptor SEMANTICO y no solo de log: <c>FaceGenBuilder.DebugMode</c> lo lee y de el sale el sufijo
    ''' <c>_2</c> del sandbox. Con el logger forzado a False en Release, ese barrido escribiria los nombres
    ''' CANONICOS y PISARIA el bake del CK — que es justamente la referencia contra la que compara. Apagarlo sin
    ''' esta valvula seria un cambio destructivo, no una optimizacion.</para>
    ''' Se setea ANTES de tocar <see cref="Enabled"/> (si no, el setter ya descarto el valor).</summary>
    Public Shared Property AllowInReleaseBuilds As Boolean
        Get
            Return _allowInRelease
        End Get
        Set(value As Boolean)
            _allowInRelease = value
        End Set
    End Property
    Private Shared _allowInRelease As Boolean = False

    Private Shared _writer As StreamWriter
    Private Shared ReadOnly _queue As New ConcurrentQueue(Of String)
    Private Shared _signal As ManualResetEventSlim
    Private Shared _writerThread As Thread
    Private Shared _shuttingDown As Integer = 0    ' 0 = run, 1 = stop
    Private Shared ReadOnly _initLock As New Object()

    ' Tunables: batch size + max wait for partial-batch flush.
    Private Const FlushBatchSize As Integer = 64
    Private Const FlushIntervalMs As Integer = 200

    ''' <summary>
    ''' Inicializa el logger con la ruta de archivo. Idempotente. Si <see cref="Enabled"/>
    ''' está en False, no hace nada (call al Log() también será no-op).
    ''' <para>OJO con <c>Enabled = True</c> SIN <c>Initialize</c>: no hay writer ni hilo que drene, y
    ''' <c>EnqueueInternal</c> DESCARTA los mensajes (es lo correcto — ver la nota de ahí). Conviene
    ''' saberlo antes de buscar un log que no existe.</para>
    ''' </summary>
    Public Shared Sub Initialize(filePath As String)
        If Enabled = False Then Exit Sub
        SyncLock _initLock
            If _writer IsNot Nothing Then Exit Sub

            Dim dir = Path.GetDirectoryName(filePath)
            If Not String.IsNullOrEmpty(dir) AndAlso Not Directory.Exists(dir) Then
                Directory.CreateDirectory(dir)
            End If
            ' Buffered FileStream (sin WriteThrough) — flush manual desde el writer thread.
            Dim fs As New FileStream(
                filePath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.ReadWrite Or FileShare.Delete,
                bufferSize:=8192)
            _writer = New StreamWriter(fs) With {.AutoFlush = False}

            _signal = New ManualResetEventSlim(initialState:=False)
            _writerThread = New Thread(AddressOf WriterLoop) With {
                .IsBackground = True,
                .Name = "FO4Logger"
            }
            _writerThread.Start()

            AddHandler AppDomain.CurrentDomain.ProcessExit, AddressOf OnProcessExit
        End SyncLock
    End Sub

    ''' <summary>
    ''' Encola un mensaje con timestamp. Lock-free (usa ConcurrentQueue). Cuando
    ''' <see cref="Enabled"/>=False es un no-op inmediato (pero la interpolación del
    ''' string en el call site igual corrió — usar <see cref="LogLazy"/> en hot paths).
    ''' </summary>
    Public Shared Sub Log(message As String)
        If Enabled = False Then Exit Sub
        EnqueueInternal(message)
    End Sub

    ''' <summary>
    ''' Variante lazy: la lambda solo se evalúa si <see cref="Enabled"/>=True. Usar
    ''' en hot paths donde el costo de la interpolación importa.
    ''' </summary>
    Public Shared Sub LogLazy(messageBuilder As Func(Of String))
        If Enabled = False Then Exit Sub
        If messageBuilder Is Nothing Then Exit Sub
        EnqueueInternal(messageBuilder())
    End Sub

    Private Shared Sub EnqueueInternal(message As String)
        ' SIN WRITER NO SE ENCOLA. `Enabled = True` sin `Initialize()` previo es un estado alcanzable —el
        ' CLI de FaceTint prende `Enabled` en --buildfacegen y --vertexbatch y NUNCA llama a `Initialize`—
        ' y en ese estado la cola NO LA DRENA NADIE: los ~700 call sites de la lib y del NPC Manager
        ' encolarian un string con timestamp cada uno, algunos por hueso por malla, durante todo el
        ' barrido; una fuga que crece con el corpus y que distorsiona los tiempos JUSTO en la herramienta
        ' con la que se mide. `_signal Is Nothing` es exactamente la condicion de "no hay hilo escritor".
        If _signal Is Nothing Then Exit Sub
        Dim timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fffZ")
        _queue.Enqueue($"[{timestamp}] {message}")
        ' Wake the writer thread; cheap when already signaled.
        Dim sig = _signal
        If sig IsNot Nothing Then sig.Set()
    End Sub

    Private Shared Sub WriterLoop()
        Dim sig = _signal
        Dim writer = _writer
        If sig Is Nothing OrElse writer Is Nothing Then Exit Sub

        Dim wroteSinceLastFlush As Integer = 0
        Do
            ' Drain whatever is in the queue right now.
            Dim line As String = Nothing
            Dim drained As Integer = 0
            While _queue.TryDequeue(line)
                writer.WriteLine(line)
                drained += 1
                wroteSinceLastFlush += 1
                ' Flush by batch — no flush per line.
                If wroteSinceLastFlush >= FlushBatchSize Then
                    Try
                        writer.Flush()
                    Catch
                    End Try
                    wroteSinceLastFlush = 0
                End If
            End While

            ' If shutting down and queue empty, final flush + exit.
            If Volatile.Read(_shuttingDown) = 1 AndAlso _queue.IsEmpty Then
                Try
                    writer.Flush()
                Catch
                End Try
                Exit Do
            End If

            ' Sleep until signaled or interval elapsed (timeout = partial-batch flush).
            sig.Reset()
            sig.Wait(FlushIntervalMs)

            ' Periodic partial-batch flush so logs aren't stuck in buffer for long.
            If wroteSinceLastFlush > 0 AndAlso _queue.IsEmpty Then
                Try
                    writer.Flush()
                Catch
                End Try
                wroteSinceLastFlush = 0
            End If
        Loop
    End Sub

    Private Shared Sub OnProcessExit(sender As Object, e As EventArgs)
        If Enabled = False Then Exit Sub
        ' Signal writer thread to drain remainder + exit, then close stream.
        Volatile.Write(_shuttingDown, 1)
        Dim sig = _signal
        If sig IsNot Nothing Then sig.Set()
        Dim t = _writerThread
        If t IsNot Nothing Then
            Try
                t.Join(2000)
            Catch
            End Try
        End If
        SyncLock _initLock
            If _writer IsNot Nothing Then
                Try
                    _writer.Close()
                    _writer.Dispose()
                Catch
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
