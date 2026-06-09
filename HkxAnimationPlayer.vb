' Version Uploaded of Fo4Library 3.2.0
Imports System.Collections.Generic
Imports System.Diagnostics

''' <summary>
''' Reproductor de animación HKX compartido por Wardrobe_Manager y FO4_NPC_Manager. Encapsula lo
''' reusable del playback y NADA de UI ni de render:
''' <list type="bullet">
''' <item>reloj de tiempo real (Stopwatch),</item>
''' <item>selección del frame que corresponde mostrar AHORA según un FPS objetivo (loopeado),</item>
''' <item>caché de poses por frame (un <see cref="HkxPoseImportSession.BuildPose"/> por frame único).</item>
''' </list>
''' La app lo maneja con su propio timer: cada tick, si <see cref="IsPlaying"/>, lee
''' <see cref="FrameForNow"/>; si cambió respecto del último mostrado, pide
''' <see cref="PoseForFrame"/> y aplica esa pose a su <c>SkeletonInstance</c> (capa DeltaTransform)
''' + re-render. El frame se elige por reloj real, así que respeta el FPS objetivo sin importar
''' a qué velocidad pueda renderizar la app (si va lento, saltea frames; nunca acelera).
''' </summary>
Public Class HkxAnimationPlayer
    Private ReadOnly _session As HkxPoseImportSession
    Private ReadOnly _clock As New Stopwatch()
    Private ReadOnly _poseCache As New Dictionary(Of Integer, Poses_class)
    Private _startFrame As Integer = 0
    Private _playing As Boolean = False
    Private _poseName As String = "HKX Pose"

    Public Sub New(session As HkxPoseImportSession)
        _session = session
    End Sub

    Public ReadOnly Property Session As HkxPoseImportSession
        Get
            Return _session
        End Get
    End Property

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
            _poseCache.Clear()
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
        Dim count = FrameCount
        If count <= 0 Then Return -1
        Dim fps = If(TargetFps <= 0.0, 1.0, TargetFps)
        Dim elapsedFrames As Long = CLng(Math.Floor(_clock.Elapsed.TotalSeconds * fps))
        Dim f As Long = (_startFrame + elapsedFrames) Mod count
        If f < 0 Then f += count
        Return CInt(f)
    End Function

    ''' <summary>Pose para un frame, cacheada por el player (session + <see cref="PoseName"/> fijos).
    ''' Nothing si no hay sesión o el build falla.</summary>
    Public Function PoseForFrame(frame As Integer) As Poses_class
        If _session Is Nothing Then Return Nothing
        Dim f = ClampFrame(frame)
        Dim cached As Poses_class = Nothing
        If _poseCache.TryGetValue(f, cached) Then Return cached
        Dim result = _session.BuildPose(f, _poseName, collectDiagnostics:=False)
        Dim pose As Poses_class = If(result Is Nothing, Nothing, result.Pose)
        If pose IsNot Nothing Then _poseCache(f) = pose
        Return pose
    End Function

    Public Sub ClearCache()
        _poseCache.Clear()
    End Sub

    Private Function ClampFrame(frame As Integer) As Integer
        Dim count = FrameCount
        If count <= 0 Then Return 0
        If frame < 0 Then Return 0
        If frame >= count Then Return count - 1
        Return frame
    End Function
End Class
