' Version Uploaded of Fo4Library 3.2.0
Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.Runtime.InteropServices
Imports System.Windows.Forms

''' <summary>
''' Reproductor de animación HKX compartido por Wardrobe_Manager y FO4_NPC_Manager. Encapsula lo
''' reusable del playback y NADA de UI ni de render:
''' <list type="bullet">
''' <item>reloj de tiempo real (Stopwatch),</item>
''' <item>selección del frame que corresponde mostrar AHORA según un FPS objetivo (loopeado),</item>
''' <item>caché de poses por frame (un <see cref="HkxPoseImportSession.BuildPose"/> por frame único).</item>
''' </list>
''' Hay DOS formas de manejarlo, y el llamador elige una: (a) su propio timer, o (b)
''' <see cref="BeginIdlePlayback"/>, el loop sobre Application.Idle de más abajo. Con un timer:
''' cada tick, si <see cref="IsPlaying"/>, lee
''' <see cref="FrameForNow"/>; si cambió respecto del último mostrado, pide
''' <see cref="PoseForFrame"/> y aplica esa pose a su <c>SkeletonInstance</c> (capa DeltaTransform)
''' + re-render. El frame se elige por reloj real, así que respeta el FPS objetivo sin importar
''' a qué velocidad pueda renderizar la app (si va lento, saltea frames; nunca acelera).
''' </summary>
Public Class HkxAnimationPlayer
    Private ReadOnly _session As HkxPoseImportSession
    Private ReadOnly _clock As New Stopwatch()
    Private _startFrame As Integer = 0
''' <summary>Primer frame REPRODUCIBLE: el motor no muestra el tramo que recorta
''' hkbClipGenerator::cropStartAmountLocalTime.</summary>
    Private _firstFrame As Integer = 0
''' <summary>Ultimo frame reproducible. ⛔ -1 significa "hasta FrameCount-1", NO "frame -1": los dos
''' consumidores que crean el player sin llamar nunca a SetPlayableRange (Wardrobe_Manager\
''' HkxPoseImport_Form.vb) tienen que comportarse EXACTAMENTE
''' como antes. Con 0 quedarian congelados en el frame 0.</summary>
    Private _lastFrame As Integer = -1
''' <summary>El clip rebota en vez de loopear (hkbClipGenerator mode 3 = PING_PONG). Medido: 1 clip en
''' 14.477 (tailbehavior.hkx :: 1HM_WalkForward, SSE) — y no es un accidente de autoria: es un barrido
''' de COLA, que es el caso de uso canonico. Default False = comportamiento de siempre.</summary>
    Public Property PingPong As Boolean
    Private _playing As Boolean = False
    Private _poseName As String = "HKX Pose"

    Public Sub New(session As HkxPoseImportSession)
        _session = session
    End Sub

    ''' <summary>FPS objetivo de reproducción. Roundtrip a milisegundos: <c>ms = 1000 / FPS</c>.</summary>
    Public Property TargetFps As Double = 30.0

    ''' <summary>Nombre que se le pone a las poses generadas. Cambiarlo limpia la caché (las poses
    ''' cacheadas llevan el nombre viejo).</summary>
    Public Property PoseName As String
        Get
            Return _poseName
        End Get
        Set(value As String)
            Dim v = If(String.IsNullOrWhiteSpace(value), "HKX Pose", value.Trim())
            If String.Equals(v, _poseName, StringComparison.Ordinal) Then Return
            _poseName = v
        End Set
    End Property

    Public ReadOnly Property FrameCount As Integer
        Get
            Return If(_session Is Nothing, 0, _session.FrameCount)
        End Get
    End Property

    Public ReadOnly Property IsPlaying As Boolean
        Get
            Return _playing
        End Get
    End Property

    ''' <summary>FPS nativo de la animación (<c>1 / FrameDuration</c>), o 0 si no disponible. Útil
    ''' para inicializar <see cref="TargetFps"/> al cargar el HKX.</summary>
    Public ReadOnly Property NativeFps As Double
        Get
            If _session Is Nothing Then Return 0.0
            Dim fd = _session.FrameDuration
            If Not Single.IsFinite(fd) OrElse fd <= 0.0F Then Return 0.0
            Return 1.0 / fd
        End Get
    End Property

    ''' <summary>Arranca el reloj tomando <paramref name="fromFrame"/> como frame inicial (para
    ''' empezar desde donde esté el slider, no siempre desde 0).</summary>
    Public Sub Start(fromFrame As Integer)
        _startFrame = ClampFrame(fromFrame)
        _clock.Restart()
        _playing = True
    End Sub

    Public Sub [Stop]()
        _playing = False
        _clock.Reset()
    End Sub

    ''' <summary>Reancla el reloj manteniendo <paramref name="currentFrame"/> como inicio (p.ej.
    ''' tras cambiar <see cref="TargetFps"/>, para que la reproducción no pegue un salto).</summary>
    Public Sub Rebase(currentFrame As Integer)
        _startFrame = ClampFrame(currentFrame)
        _clock.Restart()
    End Sub

    ''' <summary>Frame que corresponde mostrar AHORA según el reloj real y <see cref="TargetFps"/>
    ''' (loopeado). Devuelve -1 si no hay animación.</summary>
    Public Function FrameForNow() As Integer
        ' ⛔ El guard va ANTES de RangoReproducible(): es CONTRATO. Los reproductores y
        ' OnAppIdle leen el -1 como "no hay animacion". Y sin el, count = 0 daria largo = 0 y el modulo
        ' tiraria DivideByZeroException dentro de un bucle Application.Idle, donde no hay Try que la agarre.
        Dim count = FrameCount
        If count <= 0 Then Return -1
        Dim r = RangoReproducible()
        Dim largo As Long = CLng(r.hi) - CLng(r.lo) + 1L
        ' ⛔ <= 1, no <= 0: con largo = 1 la rama ping-pong haria Mod 0. Y devuelve r.lo, no 0: con crop,
        ' el unico frame reproducible puede no ser el 0.
        If largo <= 1 Then Return r.lo

        ' ⛔ El SIGNO del fps se conserva: es lo que reproduce el clip al REVES. Bethesda autora la
        ' animacion hacia atras apuntando a la de adelante con playbackSpeed -1
        ' (RifleIdleReadyCoverRightKneelShuffleBackward -> ...ShuffleForward.hkt). Medido: 108 clips.
        ' Solo se coacciona el 0 y el no-finito, que no definen ninguna velocidad. (El codigo viejo hacia
        ' `If(TargetFps <= 0.0, 1.0, TargetFps)`, que ademas dejaba pasar el NaN a CLng(Math.Floor(NaN)).)
        Dim fps = TargetFps
        If Double.IsNaN(fps) OrElse Double.IsInfinity(fps) OrElse fps = 0.0 Then fps = 1.0

        ' ⛔ La MAGNITUD se floorea y despues se le pone el signo. Hacer Floor sobre el producto con signo
        ' es ASIMETRICO: con fps < 0, en t -> 0+ ya vale -1, mientras que con fps > 0 vale 0 durante todo el
        ' primer frame. Eso corre la fase un frame SOLO en reversa (el frame de anclaje no se muestra) y se
        ' ACUMULA, porque MainForm llama Rebase() en cada cambio del numeric de FPS: 20 clics = 20 frames
        ' rebobinados con tiempo transcurrido cero.
        Dim pasos As Long = CLng(Math.Floor(_clock.Elapsed.TotalSeconds * Math.Abs(fps)))
        Dim elapsedFrames As Long = If(fps < 0.0, -pasos, pasos)

        ' ⛔ El modulo va sobre el OFFSET dentro del rango, no sobre el frame absoluto:
        ' `lo + ((_startFrame + n) Mod largo)` esta MAL. Contraejemplo con el peor crop real de FO4: 260
        ' frames a 30 fps, cropStart 6,667 s => lo=200, hi=259, largo=60. En t=0 con _startFrame=200 daria
        ' 200 + (200 Mod 60) = 220: arranca 20 frames adelante y nunca muestra 200-219.
        Dim bruto As Long = CLng(_startFrame) - CLng(r.lo) + elapsedFrames
        Dim off As Long
        If PingPong Then
            ' Onda TRIANGULAR: 0,1,...,N-1,N-2,...,1,0,1,... El periodo es 2*(N-1), no 2*N, porque los
            ' extremos NO se repiten (si no, la cola se quedaria dos frames quieta en cada punta).
            ' largo >= 2 aca por el guard de arriba => periodo >= 2 => nunca Mod 0.
            ' ⛔ Bajo ping-pong el SIGNO del fps es inerte: la onda triangular es PAR, asi que invertir el
            ' tiempo da la misma secuencia desplazada. No es un bug, es la matematica; no lo "arregles".
            Dim periodo As Long = 2L * (largo - 1L)
            Dim t As Long = bruto Mod periodo
            If t < 0 Then t += periodo
            off = If(t < largo, t, periodo - t)
        Else
            off = bruto Mod largo
            If off < 0 Then off += largo
        End If
        Return CInt(CLng(r.lo) + off)
    End Function

    ''' <summary>La pose de ese frame. ⛔ SIN CACHE PROPIA: `HkxPoseImportSession.BuildPose` ya
    ''' memoiza, y por (frame, nombre). Aca habia una SEGUNDA cache sobre la misma llamada, con otra
    ''' clave — y por eso una devolvia la pose renombrada y la otra la vieja.</summary>
    Public Function PoseForFrame(frame As Integer) As Poses_class
        If _session Is Nothing Then Return Nothing
        Return _session.BuildPose(ClampFrame(frame), _poseName, collectDiagnostics:=False)?.Pose
    End Function

    ' ─────────────────────────────────────────────────────────────────────────────────────────
    ' Loop de render basado en Application.Idle (best practice WinForms/OpenTK para tiempo real).
    ' Alternativa al WinForms Timer durante el play: renderiza apenas el hilo UI queda libre (sin
    ' esperar el WM_TIMER de baja prioridad), PERO:
    '   • solo dispara onFrame cuando el frame REALMENTE cambió (paceado por reloj) → no quema GPU,
    '   • duerme 1ms cuando está adelantado → no quema CPU (no spinea al 100%),
    '   • chequea la cola en cada vuelta y SALE apenas llega input → NO congela la UI.
    ' onFrame corre en el hilo UI (igual que un Tick). Llamar Start(fromFrame) antes de Begin.
    ' ─────────────────────────────────────────────────────────────────────────────────────────
    Private _idleHandler As EventHandler = Nothing
    Private _onFrame As Action(Of Integer) = Nothing
    Private _lastShownFrame As Integer = -1

    ''' <summary>Arranca el loop Application.Idle. <paramref name="onFrame"/> se invoca en el hilo UI
    ''' con el frame a mostrar, SOLO cuando cambia respecto del último. Idempotente: nunca suscribe
    ''' dos veces. Llamar <see cref="Start"/> antes para fijar el reloj.</summary>
    Public Sub BeginIdlePlayback(onFrame As Action(Of Integer))
        If onFrame Is Nothing Then Return
        _onFrame = onFrame
        _lastShownFrame = -1
        If _idleHandler Is Nothing Then _idleHandler = AddressOf OnAppIdle
        RemoveHandler Application.Idle, _idleHandler   ' idempotente
        AddHandler Application.Idle, _idleHandler
    End Sub

    ''' <summary>Frena el loop Application.Idle (desuscribe). Llamar junto con <see cref="[Stop]"/>.
    ''' Se ejecuta en el hilo UI, donde el loop NO está corriendo, así que no hay carrera.</summary>
    Public Sub EndIdlePlayback()
        _onFrame = Nothing
        If _idleHandler IsNot Nothing Then RemoveHandler Application.Idle, _idleHandler
    End Sub

    Private Sub OnAppIdle(sender As Object, e As EventArgs)
        ' Loop MIENTRAS la cola esté vacía. Apenas llega un mensaje (click/mouse/tecla/paint),
        ' AppIsIdle()=False → sale → el pump lo procesa → Idle se vuelve a disparar. Por eso cede
        ' en cada vuelta y NO congela la UI; la latencia de input es a lo sumo un frame (~render).
        While _playing AndAlso _onFrame IsNot Nothing AndAlso AppIsIdle()
            Dim f = FrameForNow()
            If f < 0 Then Exit While
            If f <> _lastShownFrame Then
                _lastShownFrame = f
                Dim cb = _onFrame
                If cb IsNot Nothing Then cb(f)
            Else
                Threading.Thread.Sleep(1)   ' adelantado: pacing, cede el hilo 1ms (no spin)
            End If
        End While
    End Sub

    ''' <summary>True si la cola de mensajes del hilo está vacía. PeekMessage con PM_NOREMOVE: mira
    ''' sin sacar el mensaje, así no roba input al pump.</summary>
    Private Shared Function AppIsIdle() As Boolean
        Dim msg As NativeMessage
        Return Not PeekMessage(msg, IntPtr.Zero, 0UI, 0UI, 0UI)
    End Function

    ' ⛔ LAYOUT DE INTEROP: ES EL `MSG` DE WIN32. `WParam`, `LParam` y `Time` no los lee este
    ' codigo, y aun asi NO SE TOCAN: `PeekMessage` escribe sobre esta memoria y sacar un campo
    ' corre todos los que vienen despues. Un censo de miembros muertos los marca; son la
    ' excepcion, y por eso queda escrito aca.
    <StructLayout(LayoutKind.Sequential)>
    Private Structure NativeMessage
        Public Handle As IntPtr
        Public Msg As UInteger
        Public WParam As IntPtr
        Public LParam As IntPtr
        Public Time As UInteger
        Public Location As System.Drawing.Point
    End Structure

    <DllImport("user32.dll")>
    Private Shared Function PeekMessage(ByRef lpMsg As NativeMessage, hWnd As IntPtr,
                                        wMsgFilterMin As UInteger, wMsgFilterMax As UInteger,
                                        wRemoveMsg As UInteger) As Boolean
    End Function

''' <summary>Primer frame que el motor reproduce de verdad.</summary>
    Public ReadOnly Property FirstPlayableFrame As Integer
        Get
            Return RangoReproducible().lo
        End Get
    End Property

''' <summary>Ultimo frame que el motor reproduce de verdad.</summary>
    Public ReadOnly Property LastPlayableFrame As Integer
        Get
            Return RangoReproducible().hi
        End Get
    End Property

''' <summary>Rango de frames que el motor REALMENTE reproduce. Una sola ley, leida por
''' <see cref="FrameForNow"/>, <see cref="ClampFrame"/> y las dos propiedades de arriba.
''' <para>⛔ Con FrameCount = 0 devuelve (lo, lo) — un indice que NO EXISTE — porque el `If hi &lt; lo`
''' de abajo lo colapsa. Por eso los dos llamadores conservan su propio guard de count: no se puede
''' deducir "no hay frames" del rango que devuelve esta funcion.</para></summary>
    Private Function RangoReproducible() As (lo As Integer, hi As Integer)
        Dim count = FrameCount
        Dim lo = Math.Max(0, _firstFrame)
        Dim hi = If(_lastFrame < 0, count - 1, Math.Min(_lastFrame, count - 1))
        If hi < lo Then hi = lo
        Return (lo, hi)
    End Function

''' <summary>LA ley del crop, en UN solo lugar: dado lo que declara el hkbClipGenerator y lo que trae
''' el archivo de animacion, decide si el recorte se puede honrar y cual es el rango que queda.
''' <para>⛔ La usan DOS consumidores y por eso vive suelta: <see cref="SetPlayableRange"/> (que aplica
''' el rango) y BehaviorClipEnumerator.DetectHkxFlags (que solo quiere el booleano, para que el picker
''' pueda avisar ANTES de elegir el clip). Calcularla dos veces la haria divergir sin que ningun gate
''' lo vea: el picker diria "crop ignorado" y el player lo honraria, o al reves.</para>
''' <para>⛔ La validez se decide en SEGUNDOS, ANTES de dividir. Si se decidiera despues (comparando
''' lo contra hi) el redondeo podria fabricar un rango de 1 frame donde el clip no tiene ninguno.
''' Medido: los 38 clips de FO4 cuyo crop deja el rango vacio caen los 38 por esta guarda.</para>
''' <para>⛔ NaN: `cropStart >= dur - cropEnd` es False para NaN por IEEE-754, y Math.Min/Max PROPAGAN
''' NaN, asi que el clamp posterior no lo atraparia y CInt(NaN) tira OverflowException. Por eso los dos
''' crops se chequean explicitamente. Medido: 0 casos en los dos juegos (28.954 valores), o sea que
''' esto es seguro para MODS, no arreglo de un caso vivo.</para>
''' <para>`honrable = False` NO quiere decir "hay error": tambien es False cuando el clip simplemente no
''' pidio crop. Quien quiera avisarle al usuario tiene que mirar ademas si `pidioCrop`.</para></summary>
    Friend Shared Function RangoDeCrop(count As Integer, frameDuration As Double, duration As Double,
                                       cropStartSeconds As Single, cropEndSeconds As Single) As (honrable As Boolean, lo As Integer, hi As Integer)
        If count <= 1 OrElse
           Not Double.IsFinite(frameDuration) OrElse frameDuration <= 0.0 OrElse
           Not Double.IsFinite(duration) OrElse duration <= 0.0 OrElse
           Not Single.IsFinite(cropStartSeconds) OrElse Not Single.IsFinite(cropEndSeconds) OrElse
           CDbl(cropStartSeconds) >= duration - CDbl(cropEndSeconds) Then
            Return (False, 0, Math.Max(0, count - 1))
        End If
        Dim lo = AFrame(CDbl(cropStartSeconds) / frameDuration, True, count)
        Dim hi = AFrame((duration - CDbl(cropEndSeconds)) / frameDuration, False, count)
        ' El redondeo todavia puede dejar hi < lo: eso tampoco se puede honrar.
        If hi < lo Then Return (False, 0, count - 1)
        Return (True, lo, hi)
    End Function

    Public Sub SetPlayableRange(cropStartSeconds As Single, cropEndSeconds As Single)
        Dim count = FrameCount
        Dim fd As Double = If(_session Is Nothing, 0.0, CDbl(_session.FrameDuration))
        Dim dur As Double = If(_session Is Nothing, 0.0, CDbl(_session.Duration))
        Dim pidioCrop = (cropStartSeconds <> 0.0F OrElse cropEndSeconds <> 0.0F)

        Dim r = RangoDeCrop(count, fd, dur, cropStartSeconds, cropEndSeconds)
        If r.honrable Then
            _firstFrame = r.lo
            _lastFrame = r.hi
        Else
            _firstFrame = 0
            _lastFrame = -1
            ' ⛔ Solo es "ignorado" si de verdad se pidio algo: `honrable = False` tambien cubre el clip
            ' que no pidio crop, y avisar ahi seria mentir.
        End If

        ' ⛔ ULTIMA linea: ClampFrame lee RangoReproducible(), que necesita los dos campos ya asignados.
        _startFrame = ClampFrame(_startFrame)
    End Sub

''' <summary>Segundos a frame. <paramref name="haciaArriba"/> = borde de ARRANQUE (no empezar antes del
''' crop); False = borde de FINAL.
''' <para>⛔ El snap existe porque Duration y FrameDuration son dos Single autorados por SEPARADO:
''' 2.0/0.033333335 = 59,999997, y un floor crudo perderia el ultimo frame de 9.708 de 15.713
''' animaciones de FO4 (61,8 %) y 3.555 de 5.935 de SSE, incluso sin crop. La separacion es enorme — el
''' crop real mas chico vale 1,0 frame y el peor ruido medido 3e-6 — asi que TOL = 0,01 no puede
''' confundir un crop con ruido.</para>
''' <para>⛔ El clamp va en Double ANTES del CInt: CInt de un Double fuera de rango TIRA
''' OverflowException, no satura. Y ese clamp absorbe cualquier x negativo (un crop negativo es finito y
''' pasa la guarda de validez; medido 0 en vanilla, posible con mods), asi que la rama del medio de
''' Math.Round no decide nada y no hace falta AwayFromZero.</para></summary>
    Private Shared Function AFrame(x As Double, haciaArriba As Boolean, count As Integer) As Integer
        Const TOL_FRAME As Double = 0.01
        If Not Double.IsFinite(x) Then Return If(haciaArriba, 0, count - 1)
        Dim r = Math.Round(x)
        Dim v = If(Math.Abs(x - r) <= TOL_FRAME, r, If(haciaArriba, Math.Ceiling(x), Math.Floor(x)))
        v = Math.Max(0.0, Math.Min(CDbl(count - 1), v))
        Return CInt(v)
    End Function

    Private Function ClampFrame(frame As Integer) As Integer
        ' ⛔ El guard de count se conserva: con FrameCount = 0 RangoReproducible() devuelve (lo, lo), y si
        ' _firstFrame quedo en un valor viejo > 0 eso es un indice INEXISTENTE que PoseForFrame usaria.
        Dim count = FrameCount
        If count <= 0 Then Return 0
        Dim r = RangoReproducible()
        If frame < r.lo Then Return r.lo
        If frame > r.hi Then Return r.hi
        Return frame
    End Function
End Class
