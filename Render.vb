' Version Uploaded of Fo4Library 3.2.0
Imports System.Collections.Concurrent
Imports System.ComponentModel
Imports System.Drawing.Imaging
Imports System.IO
Imports System.Runtime.InteropServices
Imports System.Security.Cryptography
Imports System.Text
Imports System.Threading.Tasks
Imports MaterialLib.BaseMaterialFile
Imports OpenTK.GLControl
Imports OpenTK.Graphics.OpenGL4
Imports OpenTK.Mathematics
Imports OpenTK.Windowing.Common
Imports OpenTK.Windowing.Common.Input
Imports FO4_Base_Library.PreviewModel
Imports Windows.Win32.System.Diagnostics
Imports NiflySharp.Enums
Imports System.Xml


Public Class TextOverlayRenderer
    Private vao As Integer
    Private vbo As Integer
    Private shaderProgram As Integer
    Private textureID As Integer
    Private textWidth As Integer
    Private textHeight As Integer
    Private ReadOnly Labels As New Dictionary(Of String, Bitmap)

    Public Sub New()
        CompileShaders()
        InitBuffers()
        textureID = GL.GenTexture()
    End Sub

    Public Sub SetText(text As String, Optional fontSize As Integer = 32, Optional fontName As String = "Arial")
        Dim bmp As Bitmap
        If Labels.ContainsKey(text) = True Then
            bmp = Labels(text)
        Else
            bmp = GenerateTextBitmap(text, fontSize, fontName)
            If Labels.Count >= 5 Then
                Dim oldest = Labels.First()
                oldest.Value.Dispose()
                Labels.Remove(oldest.Key)
            End If
            Labels.Add(text, bmp)
        End If
        textWidth = bmp.Width
        textHeight = bmp.Height
        Dim data As BitmapData = bmp.LockBits(New Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, Imaging.PixelFormat.Format32bppArgb)
        GL.BindTexture(TextureTarget.Texture2D, textureID)
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, bmp.Width, bmp.Height, 0, OpenTK.Graphics.OpenGL4.PixelFormat.Bgra, PixelType.UnsignedByte, data.Scan0)
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, CInt(TextureMinFilter.Linear))
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, CInt(TextureMagFilter.Linear))
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, CInt(TextureWrapMode.ClampToEdge))
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, CInt(TextureWrapMode.ClampToEdge))
        bmp.UnlockBits(data)
    End Sub

    Public Sub RenderCentered(screenWidth As Integer, screenHeight As Integer)
        If textureID = 0 OrElse textWidth = 0 OrElse textHeight = 0 Then Return

        Dim x = (screenWidth - textWidth) \ 2
        Dim y = (screenHeight - textHeight) \ 2
        RenderAt(x, y, textWidth, textHeight, screenWidth, screenHeight)
    End Sub

    Public Sub RenderAt(x As Integer, y As Integer, width As Integer, height As Integer, screenW As Integer, screenH As Integer)
        If shaderProgram = 0 OrElse textureID = 0 Then Exit Sub

        GL.Disable(EnableCap.DepthTest)
        GL.Disable(EnableCap.CullFace)
        GL.Enable(EnableCap.Blend)
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha)

        GL.UseProgram(shaderProgram)

        Dim locSize = GL.GetUniformLocation(shaderProgram, "uSize")
        Dim locPos = GL.GetUniformLocation(shaderProgram, "uPosition")
        Dim locScreen = GL.GetUniformLocation(shaderProgram, "uScreenSize")

        GL.Uniform2(locSize, CSng(width), CSng(height))
        GL.Uniform2(locPos, CSng(x), CSng(y))
        GL.Uniform2(locScreen, CSng(screenW), CSng(screenH))

        GL.ActiveTexture(TextureUnit.Texture0)
        GL.BindTexture(TextureTarget.Texture2D, textureID)
        GL.Uniform1(GL.GetUniformLocation(shaderProgram, "uTexture"), 0)

        GL.BindVertexArray(vao)
        GL.DrawArrays(PrimitiveType.TriangleStrip, 0, 4)
        GL.BindVertexArray(0)

        GL.UseProgram(0)
        GL.Enable(EnableCap.DepthTest)
        GL.Enable(EnableCap.CullFace)
        GL.Disable(EnableCap.Blend)
    End Sub

    Private Sub InitBuffers()
        vao = GL.GenVertexArray()
        vbo = GL.GenBuffer()

        GL.BindVertexArray(vao)
        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo)

        ' Quad 0–1 with UVs
        Dim vertices As Single() = {
            0F, 0F, 0F, 0F,
            1.0F, 0F, 1.0F, 0F,
            0F, 1.0F, 0F, 1.0F,
            1.0F, 1.0F, 1.0F, 1.0F
        }

        GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * 4, vertices, BufferUsageHint.StaticDraw)

        GL.EnableVertexAttribArray(0)
        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, False, 4 * 4, 0)
        GL.EnableVertexAttribArray(1)
        GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, False, 4 * 4, 2 * 4)

        GL.BindBuffer(BufferTarget.ArrayBuffer, 0)
        GL.BindVertexArray(0)
    End Sub

    Private Sub CompileShaders()
        Dim vertexShaderSrc As String =
"#version 330 core
layout(location = 0) in vec2 aPos;
layout(location = 1) in vec2 aTexCoord;

out vec2 TexCoord;

uniform vec2 uSize;
uniform vec2 uPosition;
uniform vec2 uScreenSize;

void main()
{
    vec2 pixelPos = aPos * uSize + uPosition;
    vec2 ndc = (pixelPos / uScreenSize) * 2.0 - 1.0;
    ndc.y = -ndc.y;
    gl_Position = vec4(ndc, 0.0, 1.0);
    TexCoord = aTexCoord;
}"
        Dim fragmentShaderSrc As String =
"#version 330 core
in vec2 TexCoord;
out vec4 FragColor;

uniform sampler2D uTexture;

void main()
{
    FragColor = texture(uTexture, TexCoord);
}"

        Dim vertexShader = GL.CreateShader(ShaderType.VertexShader)
        Dim fragmentShader = GL.CreateShader(ShaderType.FragmentShader)

        GL.ShaderSource(vertexShader, vertexShaderSrc)
        GL.ShaderSource(fragmentShader, fragmentShaderSrc)

        GL.CompileShader(vertexShader)
        Dim vLog = GL.GetShaderInfoLog(vertexShader)

        GL.CompileShader(fragmentShader)
        Dim fLog = GL.GetShaderInfoLog(fragmentShader)

        shaderProgram = GL.CreateProgram()
        GL.AttachShader(shaderProgram, vertexShader)
        GL.AttachShader(shaderProgram, fragmentShader)
        GL.LinkProgram(shaderProgram)

        Dim linkLog = GL.GetProgramInfoLog(shaderProgram)

        GL.DeleteShader(vertexShader)
        GL.DeleteShader(fragmentShader)
    End Sub

    Private Shared Function GenerateTextBitmap(text As String, fontSize As Integer, fontName As String) As Bitmap
        Using testBmp As New Bitmap(1, 1)
            Using g As Graphics = Graphics.FromImage(testBmp)
                Using fnt As New Font(fontName, fontSize, FontStyle.Bold)
                    Dim size As SizeF = g.MeasureString(text, fnt)
                    Dim bmp As New Bitmap(CInt(Math.Ceiling(size.Width)), CInt(Math.Ceiling(size.Height)), Imaging.PixelFormat.Format32bppArgb)
                    Using g2 As Graphics = Graphics.FromImage(bmp)
                        g2.Clear(Color.Transparent)
                        g2.TextRenderingHint = Drawing.Text.TextRenderingHint.AntiAliasGridFit
                        g2.DrawString(text, fnt, Brushes.Gray, 0, 0)
                    End Using
                    Return bmp
                End Using
            End Using
        End Using
    End Function

    Public Sub Clean()
        If vao > 0 Then GL.DeleteVertexArray(vao) : vao = 0
        If vbo > 0 Then GL.DeleteBuffer(vbo) : vbo = 0
        If textureID > 0 Then GL.DeleteTexture(textureID) : textureID = 0
        If shaderProgram > 0 Then GL.DeleteProgram(shaderProgram) : shaderProgram = 0
        For Each lab In Labels
            lab.Value.Dispose()
        Next
        Labels.Clear()
    End Sub
End Class
Public Class PreviewControl
    Inherits OpenTK.GLControl.GLControl
    Private overlay As TextOverlayRenderer
    Public SharedActiveShader As Shader_Class_Fo4
    Public SharedSSEShader As Shader_Class_SSE
    Public SharedFloorShader As Floor_Shader_Class
    ''' <summary>Raised when user toggles GPU/CPU skinning mode. Consumers handle this to rerender with their pipeline.</summary>
    Public Event SkinningModeToggled(sender As PreviewControl)
    Public ReadOnly Property CurrentShader As Shader_Base_Class
        Get
            If Config_App.Current.Game = Config_App.Game_Enum.Skyrim AndAlso SharedSSEShader IsNot Nothing Then Return SharedSSEShader
            Return SharedActiveShader
        End Get
    End Property
    ''' <summary>Playback mode for fast pose ticks: suppresses camera/cursor-style UI churn
    ''' and skips non-essential bounds bookkeeping while animation frames are advancing.</summary>
    Private _playingAnimation As Boolean = False

    ''' <summary>True mientras se está REPRODUCIENDO la animación (botón Play apretado; Stop/pausa →
    ''' False). El setter PARA el RenderTimer general durante el play — el PlaybackTimer/animTimer de
    ''' la app es el único driver (corre el pipeline vía InvalidateRender y repinta vía RefreshRender)
    ''' — y lo REACTIVA al parar (sin esto, en pausa no se podría rotar/zoom). Además habilita el
    ''' present SINCRÓNICO en RefreshRender (sin diferir a WM_PAINT) y el skip de reset de cámara/bounds.
    ''' IMPORTANTE: debe seguir la lógica del botón Play (True al reproducir, False al parar), NO "hay
    ''' un clip seleccionado" — si quedara True en pausa, el RenderTimer no correría y se congelaría.</summary>
    Public Property PlayingAnimation As Boolean
        Get
            Return _playingAnimation
        End Get
        Set(value As Boolean)
            If _playingAnimation = value Then Return
            _playingAnimation = value
            If RenderTimer IsNot Nothing Then
                If value Then RenderTimer.Stop() Else RenderTimer.Start()
            End If
            ' Al PARAR la animación: durante el play se saltearon world-cache + bounds para meshes
            ' opacos (Option B), y mesh.ComputeBounds quedó gateado (frustum congelado). Forzar un Pose
            ' dirty (todos los shapes) + render síncrono YA con PlayingAnimation=False → el pipeline
            ' recomputa con computeBoundsThisFrame=True y updateWorldCache=True → frustum / cámara /
            ' picking / world-cache frescos antes de que el usuario rote o seleccione. Cubre WM y NPC
            ' (ambos paran vía este setter). Guard de Shapes para no disparar el branch "empty" del
            ' pipeline si no hay nada cargado.
            If Not value AndAlso _renderIntent IsNot Nothing AndAlso
               _renderIntent.Shapes IsNot Nothing AndAlso _renderIntent.Shapes.Any() Then
                _renderIntent.MarkDirty(RenderDirtyFlags.Pose)
                InvalidateRender()
            End If
        End Set
    End Property

    Public WithEvents RenderTimer As New System.Windows.Forms.Timer
    Private DebugProc As DebugProc
    Public Property AllowMask As Boolean = False

    ' -- Pull-based pipeline state --
    Private _renderIntent As RenderIntent
    ''' <summary>Tracks the original shapes reference from the last full reload, for identity comparison.</summary>
    Private _lastLoadedShapesSource As IEnumerable(Of IRenderableShape)
    ''' <summary>Shape set para el que el skeleton ya fue preparado (cloth bones inyectados vía
    ''' PipelineStep_Skeleton). En pose-only se compara por identidad para saltear el re-inject
    ''' per-frame (caro en WM con física; no-op en NPC). Se setea en cada PipelineStep_Skeleton.</summary>
    Private _skeletonPreparedForShapes As IEnumerable(Of IRenderableShape)
    ''' <summary>[RENDER-MS] acumuladores del desglose de UpdateSkinBuffers_GL: cómputo per-vértice
    ''' (world-transform + invert 3×3) vs upload (BufferSubData). Los resetea ExecuteRenderPipeline
    ''' antes del loop GL y los suma UpdateSkinBuffers_GL. Solo instrumentación.</summary>
    Friend _skinComputeMs As Double
    Friend _skinUploadMs As Double
    ''' <summary>[RENDER-MS] dirty-vertex bookkeeping (limpiar el HashSet de 32k flags por mesh).
    ''' Sospechoso del "gap" en CPU-anim (todos los verts dirty cada frame → el HashSet es overhead).</summary>
    Friend _skinDirtyMs As Double
    ''' <summary>[RENDER-MS] EnsureContextCurrent por mesh (sospechoso #2 del gap: si el contexto no
    ''' está current cada llamada, MakeCurrent ×19/frame; o Context.IsCurrent es caro por sí solo).</summary>
    Friend _skinCtxMs As Double
    ''' <summary>[RENDER-MS] ComputeBounds() INCONDICIONAL dentro de UpdateSkinBuffers_GL (sospechoso #3,
    ''' el más fuerte: pasada per-vértice a mundo que bypassea el gate computeBoundsThisFrame).</summary>
    Friend _skinBoundsMs As Double
    Friend _skinMaskMs As Double
    ''' <summary>[RENDER-MS] mide el PERÍODO real entre pose-updates (= 1000/fps efectivo). Si period >>
    ''' total, el cuello está ENTRE frames (ApplyPose/BuildPose del callback, pacing del Idle, vsync),
    ''' no en el pipeline medido.</summary>
    Private ReadOnly _posePeriodSw As New System.Diagnostics.Stopwatch
    ''' <summary>
    ''' The declarative render intent for this control. Apps set properties + dirty flags,
    ''' then call InvalidateRender(). The timer-driven pipeline consumes it.
    ''' </summary>
    Public ReadOnly Property Intent As RenderIntent
        Get
            If _renderIntent Is Nothing Then _renderIntent = New RenderIntent()
            Return _renderIntent
        End Get
    End Property
    Public defaultWhiteTex As Integer
    Public defaultNormalTex As Integer
    Public defaultCubeMap As Integer
    ''' <summary>Emulación de <c>BSShader_DefFacegenDetail</c>: el default que el motor bindea al slot detail
    ''' (t3) de una cabeza FaceGen cuyo texture-set slot 3 está VACÍO. RE byte-level de SkyrimSE.exe
    ''' (0x140E57E30 rellena la textura con 0x40404040 = 64/255 = 0.251; = vanilla blankdetailmap.dds). El
    ''' motor SIEMPRE softlightea un detail sobre la cara facegen; sin textura usa este 0.251 (oscurece), NO
    ''' identidad. Se emula acá para que render == lo que el NIF horneado (slot 3 vacío) rinde in-game.</summary>
    Public defaultFacegenDetailTex As Integer
    ''' <summary>Default del SUBSURFACE (_sk, texture-set slot 2) de una cabeza FaceGen cuando falta: NEGRO.
    ''' RE byte-level: BSLightingShaderMaterialFacegen slot#10 (0x1414BA8B0) rellena subsurface(+0xB0) con
    ''' DefHeightMap (fill 0xFF000000 = negro); mapeo miembro↔slot verificado en slot#8 (0x1414BA6E0):
    ''' +0xB0↔índice 2 (_sk), +0xA8↔3 (detail), +0xA0↔6 (tint). Negro ⇒ SSS = 0 (sin subsurface glow) —
    ''' distinto del fallback no-facegen del shader (softMask=albedo), que queda intacto.</summary>
    Public defaultFacegenSubsurfaceTex As Integer
    Public Property BrushRadiusPx As Integer = 5
    Public Property InvertMasking As Boolean = False


    ''' <summary>
    ''' Crea una textura 2D de w×h píxeles con el color indicado.
    ''' </summary>
    ''' 
    ''' <summary>Textura 2D uniforme de w×h con el color dado. <paramref name="mipped"/>=False (default):
    ''' Nearest + ClampToEdge, sin mips (comportamiento histórico de defaultWhiteTex/defaultNormalTex).
    ''' <paramref name="mipped"/>=True: mipmaps generados + LINEAR_MIPMAP_LINEAR + REPEAT — igual que una
    ''' textura cargada del pipeline, para defaults que el shader debe samplear idéntico a una real (los
    ''' defaults facegen: detail 0.251 / subsurface negro).</summary>
    Private Shared Function CreateColorTexture(w As Integer, h As Integer, r As Byte, g As Byte, b As Byte, a As Byte,
                                               Optional mipped As Boolean = False) As Integer
        If w <= 0 OrElse h <= 0 Then Throw New ArgumentOutOfRangeException("w/h deben ser > 0")

        ' Evita overflow en el tamaño del array
        Dim total As Long = CLng(w) * CLng(h) * 4L
        If total > Integer.MaxValue Then Throw New OutOfMemoryException("Textura demasiado grande.")

        ' Rellena RGBA
        Dim pixelData(CInt(total) - 1) As Byte
        For i As Integer = 0 To pixelData.Length - 1 Step 4
            pixelData(i + 0) = r
            pixelData(i + 1) = g
            pixelData(i + 2) = b
            pixelData(i + 3) = a
        Next

        Dim texID As Integer = GL.GenTexture()
        GL.BindTexture(TextureTarget.Texture2D, texID)

        ' Alineación segura
        GL.PixelStore(PixelStoreParameter.UnpackAlignment, 1)

        GL.TexImage2D(TextureTarget.Texture2D,
                  level:=0,
                  internalformat:=PixelInternalFormat.Rgba8,
                  width:=w, height:=h,
                  border:=0,
                  format:=OpenTK.Graphics.OpenGL4.PixelFormat.Rgba,
                  type:=PixelType.UnsignedByte,
                  pixels:=pixelData)

        ' Filtros y wrap
        If mipped Then
            GL.GenerateMipmap(GenerateMipmapTarget.Texture2D)
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, CInt(TextureMinFilter.LinearMipmapLinear))
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, CInt(TextureMagFilter.Linear))
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, CInt(TextureWrapMode.Repeat))
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, CInt(TextureWrapMode.Repeat))
        Else
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, CInt(TextureMinFilter.Nearest))
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, CInt(TextureMagFilter.Nearest))
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, CInt(TextureWrapMode.ClampToEdge))
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, CInt(TextureWrapMode.ClampToEdge))
        End If

        GL.BindTexture(TextureTarget.Texture2D, 0)
        Return texID
    End Function

    ''' <summary>
    ''' Inicializa defaultWhiteTex, defaultNormalTex y defaultCubeMap como 4×4.
    ''' Llamar una vez tras crear el contexto GL.
    ''' </summary>
    Public Sub GenerateDefaultTextures()
        ' 4×4 blanco puro
        defaultWhiteTex = CreateColorTexture(4, 4, 255, 255, 255, 255)

        ' 4×4 normal map por defecto: (0.5,0.5,1) ? (128,128,255)
        defaultNormalTex = CreateColorTexture(4, 4, 128, 128, 128, 128)

        ' default FaceGen detail = BSShader_DefFacegenDetail del motor (byte-exact, ver campo).
        ' Uniforme 64 = 0.251 (NO 0.5/identidad, NO la Bayer 0.1235 de BSShader_DitheringNoise).
        ' ⚠️ Se crea con MIPMAPS + LINEAR + REPEAT (como una textura real cargada, el blankdetailmap real es
        ' 256² con mips) para que el shader la samplee IDÉNTICO a la real y no haya diferencia por estado de
        ' sampler / minificación (un 4×4 Nearest sin mips sampleaba distinto → cabeza más clara de lo debido).
        defaultFacegenDetailTex = CreateColorTexture(64, 64, 64, 64, 64, 255, mipped:=True)

        ' 64×64 default FaceGen SUBSURFACE (_sk faltante) = NEGRO (engine: DefHeightMap → SSS=0; ver campo).
        defaultFacegenSubsurfaceTex = CreateColorTexture(64, 64, 0, 0, 0, 255, mipped:=True)

        ' Cubemap 4×4 blanco en todas las caras
        defaultCubeMap = GL.GenTexture()
        GL.BindTexture(TextureTarget.TextureCubeMap, defaultCubeMap)

        ' Preparamos datos 4×4 blancos para cada cara
        Dim faceData(4 * 4 * 4 - 1) As Byte
        For i As Integer = 0 To faceData.Length - 1 Step 4
            faceData(i + 0) = 255
            faceData(i + 1) = 255
            faceData(i + 2) = 255
            faceData(i + 3) = 255
        Next

        Dim faces As TextureTarget() = {
            TextureTarget.TextureCubeMapPositiveX,
            TextureTarget.TextureCubeMapNegativeX,
            TextureTarget.TextureCubeMapPositiveY,
            TextureTarget.TextureCubeMapNegativeY,
            TextureTarget.TextureCubeMapPositiveZ,
            TextureTarget.TextureCubeMapNegativeZ
        }
        For Each face In faces
            GL.TexImage2D(face,
                          level:=0,
                          internalformat:=PixelInternalFormat.Rgba,
                          width:=4, height:=4,
                          border:=0,
                          format:=OpenTK.Graphics.OpenGL4.PixelFormat.Rgba,
                          type:=PixelType.UnsignedByte,
                          pixels:=faceData)
        Next

        ' Filtros y wrap para cubemap
        GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMinFilter, CInt(TextureMinFilter.Linear))
        GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMagFilter, CInt(TextureMagFilter.Linear))
        GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapS, CInt(TextureWrapMode.ClampToEdge))
        GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapT, CInt(TextureWrapMode.ClampToEdge))
        GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapR, CInt(TextureWrapMode.ClampToEdge))

        GL.BindTexture(TextureTarget.TextureCubeMap, 0)
    End Sub


    Public Class VerticesAffectedEventArgs
        Inherits EventArgs
        Public ReadOnly Property Affected As New Dictionary(Of IRenderableShape, HashSet(Of Integer))
        Public Sub New(d As Dictionary(Of IRenderableShape, HashSet(Of Integer)))
            For Each sh In d.Keys
                Affected.TryAdd(sh, New HashSet(Of Integer))
                Affected(sh).UnionWith(d(sh))
            Next

        End Sub
    End Class
    Private Sub DebugCallback(source As DebugSource, glType As DebugType, id As Integer, severity As DebugSeverity, length As Integer, message As IntPtr, userParam As IntPtr)
        If severity = DebugSeverity.DebugSeverityHigh Or glType = DebugType.DebugTypeError Then
            If glType = DebugType.DebugTypeError Then
#If DEBUG Then
                Debugger.Break()
                Dim Errorx = GL.GetError
#End If

            End If
            Dim msg As String = Marshal.PtrToStringAnsi(message, length)
            Debug.Print($"GL {glType} [{severity}] ({id}): {msg}")
        End If
    End Sub

    Private ReadOnly Property IsInDesignMode As Boolean
        Get
            Return LicenseManager.UsageMode = LicenseUsageMode.Designtime OrElse
               (Not Me.Created AndAlso (Me.Site IsNot Nothing AndAlso Me.Site.DesignMode))
        End Get
    End Property

    Private _Model As PreviewModel
    Public camera As New OrbitCamera()
    Private projection As Matrix4
    Public LastUpdateMs As Double = 0
    ' Set at the very start of Clean(); blocks every GL-touching path (Tick, OnPaint,
    ' RenderScene, ExecuteRenderPipeline) so queued WM_PAINTs that drain after Clean()
    ' nulls out shaders/VAOs/textures cannot dispatch draw calls against dead handles.
    Private _isTearingDown As Boolean = False
    ' Backing field for updateRequired — Integer (not Boolean) so Volatile.Read/Write overloads resolve cleanly.
    ' 0 = False, 1 = True. Use the property from all call sites; direct field access is intentionally avoided.
    Private _updateRequired As Integer = 1
    Public Property UpdateRequired As Boolean
        Get
            Return Threading.Volatile.Read(_updateRequired) <> 0
        End Get
        Set(value As Boolean)
            Threading.Volatile.Write(_updateRequired, If(value, 1, 0))
        End Set
    End Property

    Public Sub Processing_Status(Texto As String)
        If _isTearingDown OrElse Me.IsDisposed OrElse Me.Disposing Then Exit Sub
        Me.EnsureContextCurrent()
        GL.ClearColor(Config_App.Current.Setting_BackColor)
        GL.Clear(ClearBufferMask.ColorBufferBit Or ClearBufferMask.DepthBufferBit)
        If Not IsNothing(overlay) Then
            overlay.SetText(Texto)
            overlay.RenderCentered(Me.Width, Me.Height)
        End If
        SwapBuffers()
        ' Keep the status frame on screen until some later step explicitly requests
        ' another render; pumping the message loop here can re-enter selection/render.
        UpdateRequired = False
    End Sub


    Public Property Model As PreviewModel
        Get
            If _Model Is Nothing AndAlso Not IsInDesignMode Then
                _Model = New PreviewModel(Me)
            End If
            Return _Model
        End Get
        Set(value As PreviewModel)
            _Model = value
        End Set
    End Property
    Public Sub New()
        Me.New(New GLControlSettings With {
        .API = ContextAPI.OpenGL,
        .APIVersion = New Version(4, 3),
        .Flags = ContextFlags.ForwardCompatible,
        .Profile = ContextProfile.Core
    })

    End Sub
    Public Sub New(settings As GLControlSettings)
        MyBase.New(settings)
        RenderTimer = New System.Windows.Forms.Timer With {
            .Interval = 16    ' 16 ms ˜ 60 Hz
            }
        RenderTimer.Start()
    End Sub
    ''' <summary>Simple render entry point: shapes + optional pose. No morphs, no modifiers.
    ''' Synchronous bridge — applies pose to <see cref="SkeletonInstance.Default"/> (single-actor
    ''' convenience), fills the intent and executes the pipeline immediately.</summary>
    Public Sub RenderShapes(shapes As IEnumerable(Of IRenderableShape), Optional pose As Poses_class = Nothing)
        ' Apply pose to the default instance — pose state lives there post-refactor.
        SkeletonInstance.Default.ApplyPose(pose)
        Dim i = Me.Intent
        i.Shapes = shapes
        i.FloorOffset = 0
        i.ResetCamera = True
        i.RecalculateNormals = True
        i.SkeletonResolver = Nothing
        i.MorphResolver = Nothing
        i.GeometryModifiers = Nothing
        i.TexturePrefetchAction = Nothing
        i.MarkDirty(RenderDirtyFlags.Shapes Or RenderDirtyFlags.Camera)
        ExecuteRenderPipeline()
    End Sub

    ''' <summary>Full render entry point with pluggable resolvers (legacy push API).
    ''' Synchronous bridge — caller is expected to have applied pose to its SkeletonInstance(s)
    ''' BEFORE invoking. Converts RenderRequest to intent and executes immediately.</summary>
    Public Sub RenderShapes(request As RenderRequest)
        If request Is Nothing OrElse request.Shapes Is Nothing Then Exit Sub
        Dim i = Me.Intent
        i.Shapes = request.Shapes
        i.FloorOffset = request.FloorOffset
        i.ResetCamera = request.ResetCamera
        i.RecalculateNormals = request.RecalculateNormals
        i.SkeletonResolver = request.SkeletonResolver
        i.MorphResolver = request.MorphResolver
        i.GeometryModifiers = request.GeometryModifiers
        i.TexturePrefetchAction = Nothing
        i.PreserveTextureCache = request.PreserveTextureCache
        Dim dirty = RenderDirtyFlags.Shapes
        If request.ResetCamera AndAlso Not PlayingAnimation Then dirty = dirty Or RenderDirtyFlags.Camera
        i.MarkDirty(dirty)
        ExecuteRenderPipeline()
    End Sub

    ' ------------------------------------------------------------------
    '  Pull-based unified pipeline
    ' ------------------------------------------------------------------

    ''' <summary>
    ''' Signal that the render intent has pending work and execute the pipeline immediately.
    ''' If called multiple times between frames, dirty flags accumulate via OR before execution.
    ''' </summary>
    Public Sub InvalidateRender()
        ExecuteRenderPipeline()
    End Sub

    ''' <summary>Hace el contexto GL current SOLO si no lo está ya. MakeCurrent() (cambio de
    ''' contexto) es caro aunque el contexto ya sea el current; llamarlo por-mesh por-buffer en el
    ''' loop de upload (UpdateSkinBuffers_GL + UpdateBoneMatricesSSBO) cuesta. El guard con
    ''' Context.IsCurrent es 100% equivalente (el contexto queda current igual) y evita el switch
    ''' redundante. Fallback a MakeCurrent si IsCurrent falla → peor caso = comportamiento actual.</summary>
    Public Sub EnsureContextCurrent()
        Try
            If Context IsNot Nothing AndAlso Context.IsCurrent Then Return
        Catch
        End Try
        MakeCurrent()
    End Sub

    ''' <summary>
    ''' The single hot path. Reads Intent.DirtyFlags and executes the minimum work needed.
    ''' Three execution modes emerge from flag combinations:
    '''   Shapes|Force ? full reload (clean, skeleton, geometry, morphs, GPU upload)
    '''   Pose         ? incremental (skeleton, bone matrices, optional morphs)
    '''   Morphs       ? lightweight (reapply morphs, update skin buffers)
    ''' </summary>
    Private Sub ExecuteRenderPipeline()
        If _isTearingDown Then Return
        Dim intent = _renderIntent
        If intent Is Nothing OrElse Not intent.HasWork Then Return
        If Me.Disposing OrElse Me.IsDisposed OrElse Not Visible Then Return
        If intent.Shapes Is Nothing OrElse Not intent.Shapes.Any() Then
            Model.FloorOffset = 0
            Model.Clean(False)
            Model.CleanTextures()
            Model.LoadedShapes.Clear()
            _lastLoadedShapesSource = Nothing
            _skeletonPreparedForShapes = Nothing
            intent.TexturePrefetchAction = Nothing
            Model.Processing_Status_GL("Empty")
            intent.ClearDirty()
            Return
        End If

        Dim flags = intent.DirtyFlags
        Dim needsFullReload = (flags And (RenderDirtyFlags.Shapes Or RenderDirtyFlags.Force)) <> 0
        Dim needsPoseUpdate = (flags And RenderDirtyFlags.Pose) <> 0
        Dim needsMorphUpdate = (flags And RenderDirtyFlags.Morphs) <> 0
        Dim needsTextureUpdate = (flags And RenderDirtyFlags.Textures) <> 0
        Dim needsCameraReset = (flags And RenderDirtyFlags.Camera) <> 0
        Dim allowCameraReset = intent.ResetCamera AndAlso Not PlayingAnimation

        Model.FloorOffset = intent.FloorOffset

        If needsFullReload Then
            ' -- Full reload ------------------------------------------
            Dim isNewShapeSet = (_lastLoadedShapesSource Is Nothing) OrElse
                                Not ReferenceEquals(_lastLoadedShapesSource, intent.Shapes)
            If isNewShapeSet Then
                Model.Clean(True)
                Model.Processing_Status_GL("Loading...")
                ' Caller opt-in: preserve already-uploaded GL textures across the swap. Pending
                ' uploads are still cancelled — those were keyed on the OLD shape set and racing
                ' them with the new set is unsafe. Already-resident textures get reused if the
                ' new set asks for the same paths, otherwise they linger until disposal or the
                ' next non-preserving reload.
                If intent.PreserveTextureCache Then
                    Model.CancelPendingTextureUploads()
                Else
                    Model.CleanTextures()
                End If
            Else
                Model.Clean(False)
            End If
            _lastLoadedShapesSource = intent.Shapes

            ' Texture prefetch (async, before geometry — app provides the action)
            If intent.TexturePrefetchAction IsNot Nothing Then
                intent.TexturePrefetchAction.Invoke()
                intent.TexturePrefetchAction = Nothing  ' one-shot
            End If

            ' Skeleton
            PipelineStep_Skeleton(intent)
            _skeletonPreparedForShapes = intent.Shapes

            ' Geometry extraction (parallel) — resolver consulted per shape for SkeletonInstance
            Model.LoadShapesParallel(intent.Shapes, intent.SkeletonResolver)

            ' Morphs
            PipelineStep_Morphs(intent)

            ' Geometry modifiers (zaps, etc.)
            PipelineStep_GeometryModifiers(intent)

            ' GPU upload
            Model.Setup_GL()
            If Not Config_App.Current.Setting_GPUSkinning Then
                For Each mesh In Model.meshes
                    mesh.UpdateSkinBuffers_GL()
                Next
            End If

            ' Display
            If allowCameraReset AndAlso (needsCameraReset OrElse isNewShapeSet) Then ResetCamera()
            RefreshRender()

        ElseIf needsPoseUpdate Then
            ' -- Pose change (incremental) ----------------------------
            ' Candidato #2: solo re-preparar el skeleton (clear+reinject cloth bones) si CAMBIÓ el shape
            ' set. En pose-only durante animación el set es estable → los bones inyectados del último
            ' prepare siguen vivos (ApplyPose no los toca) → se saltea el churn per-frame (caro en WM
            ' con física; PrepareForShapes. NPC: resolver no-op, así que esto es no-op para NPC).
            ' [RENDER-MS] período REAL entre pose-updates (= 1000/fps efectivo). vs total = trabajo.
            Dim _periodMs = _posePeriodSw.Elapsed.TotalMilliseconds : _posePeriodSw.Restart()
            ' [RENDER-MS] timers por fase (gateados por LogLazy; el Stopwatch es ~ns, despreciable).
            Dim _sw = System.Diagnostics.Stopwatch.StartNew()
            If Not ReferenceEquals(_skeletonPreparedForShapes, intent.Shapes) Then
                PipelineStep_Skeleton(intent)
                _skeletonPreparedForShapes = intent.Shapes
            End If
            Dim _msSkel = _sw.Elapsed.TotalMilliseconds : _sw.Restart()

            ' Dirty-mesh list — only for shapes the caller marked dirty.
            ' Empty DirtyShapes (default) means "all shapes" (back-compat single-actor flow).
            ' Se computa UNA vez por frame y se reusa: PipelineStep_Morphs (abajo) y las dos
            ' pasadas de skinning leen la misma lista (mismo predicado, mismo orden).
            Dim dirtyMeshes = Model.meshes.Where(Function(m) intent.IsShapeDirty(m.MeshData.Shape)).ToList()

            ' Morphs (if also dirty — preset+pose changed simultaneously)
            If needsMorphUpdate Then
                PipelineStep_Morphs(intent, dirtyMeshes)
            End If
            Dim _msMorph = _sw.Elapsed.TotalMilliseconds : _sw.Restart()

            ' Recompute bone matrices + GPU upload.
            ' Two-pass split (mismo patrón que LoadShapesParallel → Setup_GL):
            '   Pasada 1 = CPU puro (sin GL) → paralela sobre los meshes dirty.
            '   Pasada 2 = GL (MakeCurrent + BufferSubData) → serial en el hilo del contexto.
            Dim cpuSkinMode As Boolean = Not Config_App.Current.Setting_GPUSkinning
            Dim playingNow As Boolean = PlayingAnimation
            Dim computeBoundsThisFrame As Boolean = (Not playingNow) OrElse needsMorphUpdate

            ' Memoización #3: construir la cache de global transforms UNA vez por SkeletonInstance única
            ' (BFS parent-first), ANTES del Parallel.ForEach. Compartida read-only por todos los meshes de
            ' esa instancia → O(bones) en vez de O(shapes × bonesPalette × profundidad). Se reconstruye
            ' cada frame desde el estado actual (sin invalidación stale). Corre DESPUÉS de
            ' PipelineStep_Skeleton (inyección, arriba) y de que la app aplicó pose/morph/mount → capas
            ' finales. WM: 1 instancia (Default). NPC: base + clones por-ARMA (vía resolver).
            Dim globalCaches As New Dictionary(Of SkeletonInstance, SkeletonGlobalTransformCache)
            ' Resolve each mesh's SkeletonInstance ONCE here (serial), then read it back in the
            ' parallel body below. This removes the redundant per-mesh ResolveFor call inside the
            ' Parallel.ForEach (which also dropped the implicit thread-safety requirement on custom
            ' resolvers). Stores the raw resolver result (may be Nothing) — the parallel body folds
            ' Nothing → Default for the cache lookup exactly as before.
            Dim resolvedSkels As New Dictionary(Of RenderableMesh, SkeletonInstance)
            For Each mesh In dirtyMeshes
                Dim resolved As SkeletonInstance = intent.SkeletonResolver?.ResolveFor(mesh.MeshData.Shape)
                resolvedSkels(mesh) = resolved
                Dim inst As SkeletonInstance = If(resolved, SkeletonInstance.Default)
                If inst IsNot Nothing AndAlso Not globalCaches.ContainsKey(inst) Then
                    globalCaches(inst) = inst.BuildGlobalTransformCacheForRenderPass()
                End If
            Next
            Dim _msCache = _sw.Elapsed.TotalMilliseconds : _sw.Restart()

            ' --- Pasada 1: CPU (paralela) -------------------------------------------------
            ' RecomputeGPUBoneMatrices + ComputeBounds escriben SOLO el geo de su propio mesh
            ' (memoria distinta por mesh) y leen el SkeletonInstance read-only (GetGlobalTransform
            ' recompone y devuelve objetos nuevos, no muta). Orden por-mesh recompute→bounds
            ' preservado (bounds lee el PerVertexSkinMatrix que recompute acaba de poblar).
            ' Threading contract (lock-free read): este Parallel.ForEach lee globalCaches /
            ' SkeletonInstance.SkeletonDictionary SIN lock. Es seguro por la invariante de
            ' SkeletonInstance.BuildGlobalTransformCacheForRenderPass: toda mutación del esqueleto
            ' (pose/morph/mount/inyección) y la construcción de las caches (serial, arriba) COMPLETAN
            ' antes de esta lectura → sin solapamiento mutación↔lectura. No agregar locks acá.
            Parallel.ForEach(dirtyMeshes,
                Sub(mesh)
                    ' Read the SkeletonInstance resolved once in the serial pre-pass (no ResolveFor here).
                    Dim meshSkel As SkeletonInstance = Nothing
                    resolvedSkels.TryGetValue(mesh, meshSkel)
                    Dim meshGlobalCache As SkeletonGlobalTransformCache = Nothing
                    globalCaches.TryGetValue(If(meshSkel, SkeletonInstance.Default), meshGlobalCache)
                    ' Option B (GPU y CPU). Pasada 3 (world-cache/bounds): solo fuera de play, o para
                    ' meshes que el sort de transparentes lee por Boundingcenter en play. Ese bucket
                    ' (BlendedMeshes en RebuildRenderBuckets) = HasAlphaBlend ∪ Wireframe → el carve-out
                    ' DEBE matchearlo exacto, o un wireframe leería Boundingcenter stale (z-sort mal).
                    ' Para opacos en play nadie la muestra (frustum usa mesh.BoundsMin, congelado aparte;
                    ' el display no lee el world-cache) y en CPU es redundante con UpdateSkinBuffers.
                    ' Pasada 2 (PerVertexSkinMatrix): además en CPU-skin la necesita el display. Pasada 1
                    ' (matrices→SSBO) corre siempre dentro de Recompute.
                    Dim keepBounds As Boolean =
                        (mesh.MeshData.Material IsNot Nothing AndAlso mesh.MeshData.Material.HasAlphaBlend) OrElse
                        (mesh.MeshData.Shape IsNot Nothing AndAlso mesh.MeshData.Shape.Wireframe)
                    Dim updateWorldCache As Boolean = (Not playingNow) OrElse keepBounds
                    Dim updatePerVertexSkin As Boolean = cpuSkinMode OrElse updateWorldCache
                    ' Pose is implicit in the SkeletonInstance: the caller applied it via ApplyPose.
                    SkinningHelper.RecomputeGPUBoneMatrices(
                        mesh.MeshData.Shape, mesh.MeshData.Meshgeometry,
                        Model.SingleBoneSkinning, meshSkel, updateWorldCache, updatePerVertexSkin, meshGlobalCache)

                    If cpuSkinMode Then
                        mesh.MeshData.Meshgeometry.dirtyVertexIndices =
                            New HashSet(Of Integer)(Enumerable.Range(0, mesh.MeshData.Meshgeometry.Vertices.Length))
                        Array.Fill(mesh.MeshData.Meshgeometry.dirtyVertexFlags, True)
                    End If

                    If computeBoundsThisFrame Then mesh.ComputeBounds()
                End Sub)
            Dim _msPass1 = _sw.Elapsed.TotalMilliseconds : _sw.Restart()

            ' --- Pasada 2: GL (serial) ----------------------------------------------------
            ' Timer separado en 3: skinCompute (world-transform + invert 3×3/vértice) + skinUpload (4
            ' BufferSubData/mesh) los acumula UpdateSkinBuffers_GL en _skinComputeMs/_skinUploadMs; ssbo
            ' (matrices de hueso — desperdicio en CPU-skin) es el segundo loop. Loops separados = mismo
            ' resultado (cada uno escribe buffers independientes por mesh).
            _skinComputeMs = 0 : _skinUploadMs = 0 : _skinDirtyMs = 0 : _skinCtxMs = 0 : _skinBoundsMs = 0 : _skinMaskMs = 0
            Dim _gc0Before = GC.CollectionCount(0)   ' Gen0 GCs durante el loop skin (los arrays alocan ~28MB/frame)
            Dim _skinFuncMs As Double = 0            ' tiempo de la función entera (vs el wall del loop = overhead/GC entre meshes)
            For Each mesh In dirtyMeshes
                Dim _swM = System.Diagnostics.Stopwatch.StartNew()
                mesh.UpdateSkinBuffers_GL(recomputeBounds:=False)   ' pose path: bounds los maneja la línea gateada del pass 1
                _skinFuncMs += _swM.Elapsed.TotalMilliseconds
            Next
            Dim _msSkin = _sw.Elapsed.TotalMilliseconds : _sw.Restart()
            Dim _gc0 = GC.CollectionCount(0) - _gc0Before
            For Each mesh In dirtyMeshes
                mesh.UpdateBoneMatricesSSBO()
            Next
            Dim _msSsbo = _sw.Elapsed.TotalMilliseconds : _sw.Restart()

            If needsMorphUpdate Then
                Model.MarkRenderBucketsDirty()
            End If
            If needsCameraReset AndAlso allowCameraReset Then ResetCamera()
            RefreshRender()
            ' present solo es síncrono (y por lo tanto medible aquí) en PlayingAnimation; en scrub
            ' RefreshRender solo hace Invalidate (el draw real es diferido a OnPaint) → ~0 acá.
            Dim _msPresent = _sw.Elapsed.TotalMilliseconds
            Dim _scMs As Double = _skinComputeMs : Dim _suMs As Double = _skinUploadMs : Dim _sdMs As Double = _skinDirtyMs   ' snapshot p/ el closure
            Dim _sfMs As Double = _skinFuncMs : Dim _gc0n As Integer = _gc0 : Dim _sctxMs As Double = _skinCtxMs
            Dim _sbMs As Double = _skinBoundsMs : Dim _smMs As Double = _skinMaskMs
            If Logger.Enabled Then
                Logger.LogLazy(Function() $"[RENDER-MS] period={_periodMs:F2} meshes={dirtyMeshes.Count} skel={_msSkel:F2} morph={_msMorph:F2} cache={_msCache:F2} pass1={_msPass1:F2} ctx={_sctxMs:F2} skinCompute={_scMs:F2} skinUpload={_suMs:F2} skinDirty={_sdMs:F2} skinBounds={_sbMs:F2} skinMask={_smMs:F2} skinFunc={_sfMs:F2} skin={_msSkin:F2} gc0={_gc0n} ssbo={_msSsbo:F2} present={_msPresent:F2} total={(_msSkel + _msMorph + _msCache + _msPass1 + _msSkin + _msSsbo + _msPresent):F2} play={playingNow} cpuSkin={cpuSkinMode}")
            End If

        ElseIf needsMorphUpdate Then
            ' -- Morph-only (lightweight) -----------------------------
            If needsTextureUpdate Then Model.Process_Textures_GL()

            PipelineStep_Morphs(intent)

            ' Upload only meshes whose morph plan was reapplied.
            For Each mesh In Model.meshes
                If Not intent.IsShapeDirty(mesh.MeshData.Shape) Then Continue For
                mesh.UpdateSkinBuffers_GL()
            Next

            Model.MarkRenderBucketsDirty()
            RefreshRender()

        ElseIf needsTextureUpdate Then
            ' -- Texture-only -----------------------------------------
            Model.Process_Textures_GL()
            Model.MarkRenderBucketsDirty()
            RefreshRender()
        End If

        ' If the caller registered a PostTextureUploadAction but the pipeline didn't actually
        ' kick off a background load (texture cache reuse / PreserveTextureCache / no new
        ' shapes), TexturesReady never transitioned False→True so the watchdog hook below
        ' never fires. Run the action synchronously here instead — same observable outcome,
        ' just with zero defer. The watchdog deadline armed by LoadTexturesAsync is the only
        ' code path that ever sets _postTextureUploadDeadlineUtc; if it's still Nothing here
        ' it means no async load began, so the success action is safe to fire immediately.
        If Model.TexturesReady AndAlso intent.PostTextureUploadAction IsNot Nothing _
           AndAlso Not Model.HasPendingPostTextureDeadline Then
            Model.FlushPostTextureUploadHookSyncSuccess()
        End If

        intent.ClearDirty()
    End Sub

    ''' <summary>Resolve skeleton via app-provided resolver or default fallback. Pose state
    ''' lives in the SkeletonInstance(s) and gets re-applied by PrepareForShapes after
    ''' cloth-inject (idempotent — guarantees DeltaTransforms reflect the requested pose
    ''' even when cloth-inject re-creates bones).</summary>
    Private Shared Sub PipelineStep_Skeleton(intent As RenderIntent)
        If intent.SkeletonResolver IsNot Nothing Then
            intent.SkeletonResolver.ResolveSkeleton(intent.Shapes)
        Else
            SkeletonInstance.Default.PrepareForShapes(intent.Shapes)
        End If
    End Sub

    ''' <summary>Apply morphs via app-provided resolver — only for shapes marked dirty.
    ''' Empty <see cref="RenderIntent.DirtyShapes"/> means "all shapes" (back-compat). If the
    ''' resolver is Nothing or yields a null/empty plan for a shape, <see cref="MorphEngine.ApplyMorphPlan"/>
    ''' resets that shape's geometry to NifLocalVertices (raw, pre-skin) — this is the
    ''' explicit "no morphs" contract, so callers can toggle morphs OFF simply by
    ''' clearing the resolver instead of carrying stale deltas.</summary>
    Private Sub PipelineStep_Morphs(intent As RenderIntent, Optional dirtyMeshes As List(Of RenderableMesh) = Nothing)
        ' CPU puro (sin GL): por cada shape dirty resuelve su MorphPlan y lo aplica a su geo.
        ' Paralelizado across-shapes — cada mesh escribe SOLO su propio geo; ResolveMorphPlan se
        ' llama concurrente sobre la misma instancia de resolver (sus campos son read-only y las
        ' cachés TRI Shared están protegidas con SyncLock: NpcMorphResolver, BodySlideTriResolver._pirtCache).
        ' Ver el contrato de concurrencia en IMorphResolver.ResolveMorphPlan.
        ' El caller del pose-path ya computó esta lista (mismo predicado); la reusamos para no
        ' rehacer el .Where(...).ToList() sobre todos los meshes. Sin lista → computar como antes.
        If dirtyMeshes Is Nothing Then
            dirtyMeshes = Model.meshes.Where(Function(m) intent.IsShapeDirty(m.MeshData.Shape)).ToList()
        End If
        Parallel.ForEach(dirtyMeshes,
            Sub(mesh)
                Dim plan As MorphPlan = Nothing
                If intent.MorphResolver IsNot Nothing Then
                    plan = intent.MorphResolver.ResolveMorphPlan(mesh.MeshData.Shape, mesh.MeshData.Meshgeometry)
                End If
                MorphEngine.ApplyMorphPlan(
                    mesh.MeshData.Meshgeometry, plan,
                    intent.RecalculateNormals,
                    allowMask:=AllowMask,
                    maskedVertices:=mesh.MeshData.Shape.MaskedVertices)
            End Sub)
    End Sub

    ''' <summary>Apply geometry modifiers in order. Skips if none set.</summary>
    Private Sub PipelineStep_GeometryModifiers(intent As RenderIntent)
        If intent.GeometryModifiers Is Nothing Then Return
        For Each gmod In intent.GeometryModifiers
            For Each mesh In Model.meshes
                gmod.Apply(mesh.MeshData.Shape, mesh.MeshData.Meshgeometry)
            Next
        Next
    End Sub

    Protected Overrides Sub OnLoad(e As EventArgs)
        MyBase.OnLoad(e)
        If Me.IsInDesignMode Then Return
        ApplyResize(True)
        GenerateDefaultTextures()
        SharedActiveShader = New Shader_Class_Fo4
        SharedSSEShader = New Shader_Class_SSE
        SharedFloorShader = New Floor_Shader_Class

        ' 1) Aseguramos que el contexto GL está activo
        Me.EnsureContextCurrent()

        ' 2) (Opcional) Debug Output para capturar sólo errores — solo en build DEBUG.
        ' Synchronous fuerza al driver a serializar el pipeline para que el callback
        ' caiga en la llamada GL culpable, lo que penaliza Release sin aportar nada
        ' (DebugCallback ya gatea Debugger.Break a #If DEBUG).
#If DEBUG Then
        GL.Enable(EnableCap.DebugOutput)
        GL.Enable(EnableCap.DebugOutputSynchronous)
        DebugProc = AddressOf DebugCallback
        GL.DebugMessageCallback(DebugProc, IntPtr.Zero)
        GL.DebugMessageControl(DebugSourceControl.DontCare, DebugTypeControl.DontCare, DebugSeverityControl.DebugSeverityHigh, 0, Array.Empty(Of Integer)(), True)
#End If

        ' 3) Estado GL estándar
        GL.Enable(EnableCap.DepthTest)
        GL.DepthFunc(DepthFunction.Lequal)

        GL.Enable(EnableCap.CullFace)
        GL.CullFace(TriangleFace.Back)
        GL.FrontFace(FrontFaceDirection.Ccw)

        overlay = New TextOverlayRenderer()

    End Sub

    Protected Overrides Sub OnLocationChanged(e As EventArgs)
        If Me.IsInDesignMode Then Return
        MyBase.OnLocationChanged(e)
    End Sub
    Private lastW As Integer = -1
    Private lastH As Integer = -1
    Protected Overrides Sub OnResize(e As EventArgs)
        If Me.IsInDesignMode Then Return
        MyBase.OnResize(e)
        ApplyResize(False)
    End Sub
    Public Sub ApplyResize(Force As Boolean)
        If Me.IsInDesignMode Then Return
        If Force OrElse (Me.Width <> lastW OrElse Me.Height <> lastH) Then
            EnsureContextCurrent()
            GL.Viewport(0, 0, Me.Width, Me.Height)
            lastW = Me.Width
            lastH = Me.Height
            UpdateProjection(True)
        End If
    End Sub
    ' === Frustum dinámico ===
    Private lastNear As Single = 0.1F
    Private lastFar As Single = 1000.0F

    ' Recalcula la proyección en función del tamaño de escena y la distancia actual de la cámara.
    Public Sub UpdateProjection(Optional force As Boolean = False)
        If Me.Height <= 0 Then Return

        ' Bounds de escena (si no hay meshes aún, usa un AABB mínimo)
        Dim minB As Vector3
        Dim maxB As Vector3
        If Model IsNot Nothing AndAlso Model.meshes IsNot Nothing AndAlso Model.meshes.Count > 0 Then
            GetSceneBounds(minB, maxB)
        Else
            minB = New Vector3(-1.0F)
            maxB = New Vector3(1.0F)
        End If

        Dim size As Vector3 = maxB - minB
        ' Ejes: X=ancho, Y=profundidad, Z=alto (tu código ya usa esta convención)
        Dim halfW As Single = Math.Abs(size.X) * 0.5F
        Dim halfD As Single = Math.Abs(size.Y) * 0.5F
        Dim halfH As Single = Math.Abs(size.Z) * 0.5F

        ' Radio: cuanto “crece” la escena alrededor del centro
        Dim radius As Single = Math.Max(halfW, Math.Max(halfD, halfH))
        If Double.IsInfinity(radius) Then radius = 1

        ' Distancia actual cámara ? foco
        Dim eyeToCenter As Single = Math.Max(1.0F, camera.distance)

        ' Margen para asegurar que no clippea por el far plane
        Dim margin As Single = 0.2F

        ' Far plane sugerido: distancia + radio + margen
        Dim farZ As Single = eyeToCenter + radius * (1.0F + margin) + 1.0F
        ' Mínimo razonable para escenas pequeñas
        farZ = Math.Max(1000.0F, farZ)

        ' Near plane: suficientemente pequeño, pero no exagerado para no perder precisión de Z
        Dim nearZ As Single = Math.Max(0.05F, farZ / 10000.0F)

        ' Evitar recalcular si el cambio es mínimo
        If Not force AndAlso Math.Abs(farZ - lastFar) < 1.0F AndAlso Math.Abs(nearZ - lastNear) < 0.01F Then
            Return
        End If

        Dim aspect As Single = Me.Width / CSng(Math.Max(1, Me.Height))
        Dim fovY As Single = MathHelper.DegreesToRadians(45.0F)

        projection = Matrix4.CreatePerspectiveFieldOfView(fovY, aspect, nearZ, farZ)
        lastNear = nearZ
        lastFar = farZ
        UpdateRequired = True
    End Sub
    Private Sub RenderScene()
        If _isTearingDown OrElse Me.IsDisposed OrElse Me.Disposing Then Exit Sub
        If _Model Is Nothing Then Exit Sub
        If SharedActiveShader Is Nothing AndAlso SharedSSEShader Is Nothing Then Exit Sub
        ApplyResize(False)
        Me.EnsureContextCurrent()
        GL.ClearColor(Config_App.Current.Setting_BackColor)
        GL.Clear(ClearBufferMask.ColorBufferBit Or ClearBufferMask.DepthBufferBit)
        If Model.Can_Render Then
            Model.RenderAll(projection, camera)
        End If
    End Sub
    Private Shared Sub FinishRenderFrame()
        GL.DepthMask(True)
        GL.Disable(EnableCap.Blend)
    End Sub

    Public Function CaptureBitmap() As Bitmap
        If Me.IsInDesignMode OrElse Me.Width <= 0 OrElse Me.Height <= 0 Then Return Nothing

        Me.EnsureContextCurrent()
        ApplyResize(True)

        If UpdateRequired Then
            ' Consume the current render request up front so any new request raised
            ' during RenderScene survives this frame and schedules the next one.
            UpdateRequired = False
            RenderScene()
            SwapBuffers()
            FinishRenderFrame()
        End If

        Dim bmp As New Bitmap(Me.Width, Me.Height, Imaging.PixelFormat.Format32bppArgb)
        Dim rect As New Rectangle(0, 0, bmp.Width, bmp.Height)
        Dim data As BitmapData = bmp.LockBits(rect, ImageLockMode.WriteOnly, Imaging.PixelFormat.Format32bppArgb)
        Try
            GL.ReadBuffer(ReadBufferMode.Front)
            GL.PixelStore(PixelStoreParameter.PackAlignment, 4)
            GL.ReadPixels(0, 0, bmp.Width, bmp.Height, OpenTK.Graphics.OpenGL4.PixelFormat.Bgra, PixelType.UnsignedByte, data.Scan0)
        Finally
            bmp.UnlockBits(data)
        End Try

        bmp.RotateFlip(RotateFlipType.RotateNoneFlipY)
        Return bmp
    End Function

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        If _isTearingDown OrElse Me.IsDisposed OrElse Me.Disposing Then Exit Sub
        If Me.IsInDesignMode OrElse Not UpdateRequired Then Exit Sub
        MyBase.OnPaint(e)
        ' Consume the current render request up front so any new request raised
        ' during RenderScene survives this frame and schedules the next one.
        UpdateRequired = False
        _ticksSinceLastPresent = 0  ' reset safety-repaint heartbeat
        Try
            PresentFrame()
        Catch ex As Exception
            Try
                Processing_Status("Render error")
            Catch
            End Try
        End Try
    End Sub

    ''' <summary>Dibuja y presenta un frame: RenderScene + SwapBuffers + FinishRenderFrame. Lo
    ''' llaman OnPaint (camino diferido normal, vía WM_PAINT) y RefreshRender durante el play
    ''' (camino sincrónico). Centralizado para tener un único punto de present.</summary>
    Private Sub PresentFrame()
        RenderScene()
        SwapBuffers()
        FinishRenderFrame()
    End Sub
    Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
        MyBase.OnMouseDown(e)
        If e.Button = MouseButtons.Left OrElse e.Button = MouseButtons.Middle Then
            lastX = e.X
            lastY = e.Y
        End If
    End Sub


    Private lastX As Integer
    Private lastY As Integer
    Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
        If Me.IsInDesignMode Then Return
        MyBase.OnMouseMove(e)
        ' Left drag sin Ctrl ni Alt: salir de FreeMode (si aplica) y luego ROTATE orbit manteniendo el mismo radio
        ' Left drag sin Ctrl ni Alt: salimos de free-cam (si era el caso) y rotamos en orbit
        If e.Button = MouseButtons.Left AndAlso (Control.ModifierKeys And Keys.Control) = 0 AndAlso (Control.ModifierKeys And Keys.Alt) = 0 Then
            ' Si venimos de free-cam, restauramos el radio original
            ' Ahora la rotación orbital normal
            Dim dx = e.X - lastX
            Dim dy = e.Y - lastY
            lastX = e.X
            lastY = e.Y

            camera.Rotate(dx, dy)
            UpdateRequired = True
            Return
        End If

        If (e.Button = MouseButtons.Left AndAlso (Control.ModifierKeys And Keys.Alt) <> 0) OrElse
            e.Button = MouseButtons.Middle Then
            Dim dx = e.X - lastX
            Dim dy = e.Y - lastY
            lastX = e.X
            lastY = e.Y
            camera.Pan(dx, dy)
            UpdateRequired = True
            Return
        End If


        ' 2) Barrido con Ctrl + botón izquierdo
        If AllowMask AndAlso e.Button = MouseButtons.Left AndAlso (Control.ModifierKeys And Keys.Control) <> 0 Then
            Cursor.Current = Cursors.Hand
            Dim vw = Me.Width
            Dim vh = Me.Height
            Dim r2 As Single = BrushRadiusPx * BrushRadiusPx
            ' — Hoist de matrices: calcula viewProj una sola vez
            Dim viewMatrix As Matrix4 = camera.GetViewMatrix()
            Dim viewProj As Matrix4 = viewMatrix * projection
            Dim camPos = camera.GetEyePosition()
            For Each mesh In Model.meshes.Where(Function(pf) pf.MeshData.Shape.ShowMask)
                Dim key = mesh.MeshData.Shape
                ' GPU Skinning: use world-space cache (Vertices are now local-space)
                Dim verts = SkinningHelper.GetWorldVertices(mesh.MeshData.Meshgeometry)
                Dim norms = SkinningHelper.GetWorldNormals(mesh.MeshData.Meshgeometry)

                For i = 0 To verts.Length - 1
                    If mesh.MeshData.Meshgeometry.VertexMask(i) = -1 And mesh.MeshData.Shape.ApplyZaps Then Continue For
                    If mesh.MeshData.Meshgeometry.VertexMask(i) = -1 Then If mesh.MeshData.Shape.MaskedVertices.Contains(i) Then mesh.MeshData.Meshgeometry.VertexMask(i) = 1 Else mesh.MeshData.Meshgeometry.VertexMask(i) = 0
                    If (mesh.MeshData.Meshgeometry.VertexMask(i) = 1 AndAlso Not InvertMasking) OrElse (mesh.MeshData.Meshgeometry.VertexMask(i) = 0 AndAlso InvertMasking) Then Continue For
                    ' 2.1b) Filtrar solo vértices de la cara delantera (normal-camera)
                    Dim normal As Vector3 = norms(i)
                    Dim toCam As Vector3 = camPos - verts(i)
                    If Vector3.Dot(normal, toCam) <= 0 Then Continue For

                    Dim clipPos As Vector4 = New Vector4(verts(i), 1.0F) * viewProj


                    ' 2.2) Filtrado de frustum (W>0) — opcional quitar para probar
                    If clipPos.W <= 0 Then Continue For

                    ' 2.3) De clip a NDC
                    Dim ndcX = clipPos.X / clipPos.W
                    Dim ndcY = clipPos.Y / clipPos.W

                    ' 2.4) De NDC a ventana (0,0 arriba)
                    Dim sx = (ndcX + 1.0F) * 0.5F * vw
                    Dim sy = (1.0F - ndcY) * 0.5F * vh

                    ' 2.5) Calcula distancia al cursor
                    Dim dx2 = sx - e.X
                    Dim dy2 = sy - e.Y
                    Dim dist2 = dx2 * dx2 + dy2 * dy2

                    ' 2.6) Si entra en el radio, lo marcamos
                    If dist2 <= r2 Then
                        mesh.MeshData.Meshgeometry.dirtyMaskIndices.Add(i)
                        mesh.MeshData.Meshgeometry.dirtyMaskFlags(i) = True
                        mesh.MeshData.Meshgeometry.VertexMask(i) = 1 - mesh.MeshData.Meshgeometry.VertexMask(i)
                        If InvertMasking Then mesh.MeshData.Shape.MaskedVertices.Remove(i) Else mesh.MeshData.Shape.MaskedVertices.Add(i)
                        Me.UpdateRequired = True
                    End If
                Next
                mesh.UpdateUpdateSkinBuffersMask_GL()
            Next
            Me.Invalidate()
            Return
        End If
    End Sub

    Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
        MyBase.OnMouseUp(e)
        Cursor.Current = Cursors.Default
        If e.Button = MouseButtons.Right Then
            ShowPreviewContextMenu(e.Location)
        End If
    End Sub

    Public Event FloorToggled As EventHandler(Of Boolean)

    Private Sub ShowPreviewContextMenu(location As Point)
        Dim menu As New ContextMenuStrip()

        Dim resetFull As New ToolStripMenuItem("Reset Camera")
        AddHandler resetFull.Click, Sub()
                                        ResetCamera(True)
                                        UpdateRequired = True
                                    End Sub

        menu.Items.Add(resetFull)
        menu.Items.Add(New ToolStripSeparator())

        Dim cameraSubMenu As New ToolStripMenuItem("Camera on Change")

        Dim resetRotation As New ToolStripMenuItem("Reset rotation") With {
            .Checked = Config_App.Current.Settings_Camara.ResetAngles,
            .CheckOnClick = True,
            .Enabled = Not Config_App.Current.Settings_Camara.FreezeCamera
        }
        AddHandler resetRotation.Click, Sub()
                                            Dim cam = Config_App.Current.Settings_Camara
                                            cam.ResetAngles = resetRotation.Checked
                                            Config_App.Current.Settings_Camara = cam
                                        End Sub

        Dim resetZoom As New ToolStripMenuItem("Reset to optimal zoom") With {
            .Checked = Config_App.Current.Settings_Camara.ResetZoom,
            .CheckOnClick = True,
            .Enabled = Not Config_App.Current.Settings_Camara.FreezeCamera
        }
        AddHandler resetZoom.Click, Sub()
                                        Dim cam = Config_App.Current.Settings_Camara
                                        cam.ResetZoom = resetZoom.Checked
                                        Config_App.Current.Settings_Camara = cam
                                    End Sub

        Dim freezeCamera As New ToolStripMenuItem("Freeze camera") With {
            .Checked = Config_App.Current.Settings_Camara.FreezeCamera,
            .CheckOnClick = True
        }
        AddHandler freezeCamera.Click, Sub()
                                           Dim cam = Config_App.Current.Settings_Camara
                                           cam.FreezeCamera = freezeCamera.Checked
                                           Config_App.Current.Settings_Camara = cam
                                           resetRotation.Enabled = Not freezeCamera.Checked
                                           resetZoom.Enabled = Not freezeCamera.Checked
                                       End Sub

        cameraSubMenu.DropDownItems.Add(resetRotation)
        cameraSubMenu.DropDownItems.Add(resetZoom)
        cameraSubMenu.DropDownItems.Add(New ToolStripSeparator())
        cameraSubMenu.DropDownItems.Add(freezeCamera)

        menu.Items.Add(cameraSubMenu)
        menu.Items.Add(New ToolStripSeparator())

        Dim floorEnabled = Model IsNot Nothing AndAlso Model.Floor IsNot Nothing AndAlso Model.Floor.Enabled
        Dim toggleFloor As New ToolStripMenuItem("Render Floor") With {
            .Checked = floorEnabled,
            .CheckOnClick = True
        }
        AddHandler toggleFloor.Click, Sub()
                                          If Model IsNot Nothing AndAlso Model.Floor IsNot Nothing Then
                                              Model.Floor.Enabled = toggleFloor.Checked
                                              RaiseEvent FloorToggled(Me, toggleFloor.Checked)
                                              UpdateRequired = True
                                          End If
                                      End Sub

        menu.Items.Add(toggleFloor)
        menu.Items.Add(New ToolStripSeparator())
        Dim toggleSkinning As New ToolStripMenuItem("GPU Skinning") With {
            .Checked = Config_App.Current.Setting_GPUSkinning,
            .CheckOnClick = True
        }
        AddHandler toggleSkinning.Click, Sub()
                                             Config_App.Current.Setting_GPUSkinning = toggleSkinning.Checked
                                             RaiseEvent SkinningModeToggled(Me)
                                             ' Forzamos full reload preservando el Intent actual (MorphResolver,
                                             ' GeometryModifiers, Shapes, Pose) que seteo el ultimo Update_Render.
                                             ' NO usamos RenderShapes(shapes, pose) porque ese overload wipea
                                             ' MorphResolver: sin el, PipelineStep_Morphs early-returns y los
                                             ' zaps (que dependen de VertexMask, modificado por ApplyMorphPlan
                                             ' en los zap channels) nunca se re-aplican.
                                             If Model.LoadedShapes.Count > 0 AndAlso Intent.Shapes IsNot Nothing Then
                                                 Dim sw = System.Diagnostics.Stopwatch.StartNew()
                                                 Intent.MarkDirty(RenderDirtyFlags.Shapes Or RenderDirtyFlags.Force)
                                                 InvalidateRender()
                                                 sw.Stop()
                                                 LastUpdateMs = sw.Elapsed.TotalMilliseconds
                                             End If
                                         End Sub
        menu.Items.Add(toggleSkinning)
        menu.Items.Add(New ToolStripSeparator())
        Dim timeLabel As New ToolStripMenuItem($"Last update: {LastUpdateMs:F1} ms") With {.Enabled = False}
        menu.Items.Add(timeLabel)
        menu.Show(Me, location)
    End Sub

    Protected Overrides Sub OnMouseWheel(e As MouseEventArgs)
        If Me.IsInDesignMode Then Return
        MyBase.OnMouseWheel(e)
        camera.Zoom(e.Delta / 120.0F)
        UpdateProjection(False)
        UpdateRequired = True
    End Sub

    Public Sub RefreshRender()
        If PlayingAnimation Then
            ' En play: dibujar SINCRÓNICO (sin diferir a WM_PAINT) para sacar la latencia del
            ' message-pump. No dejamos UpdateRequired=True → OnPaint no redibuja el mismo frame
            ' (evita doble draw; OnPaint ya se auto-saltea con UpdateRequired=False). FUERA del
            ' play, el camino normal diferido (Invalidate→OnPaint) queda IGUAL — sin cambios para
            ' editar/rotar cámara (coalescing, reentrancy-safe, no quema CPU en idle).
            UpdateRequired = False
            _ticksSinceLastPresent = 0
            Try
                PresentFrame()
            Catch ex As Exception
                Try
                    Processing_Status("Render error")
                Catch
                End Try
            End Try
        Else
            UpdateRequired = True
            Me.Invalidate()
        End If
    End Sub
    Public Sub ResetCamera(Optional Force As Boolean = False)
        If Me.IsInDesignMode Then Return

        Dim oldcamera = camera
        camera = New OrbitCamera()
        CenterCamera()

        If Not Config_App.Current.Settings_Camara.ResetAngles And Not Force Then
            camera.angleX = oldcamera.angleX
            camera.angleY = oldcamera.angleY
            camera.UpdateDirectionFromAngles()
        End If
        If Not Config_App.Current.Settings_Camara.ResetZoom And Not Force Then
            If oldcamera.Optimaldistance <> 0 Then
                camera.distance *= (oldcamera.distance / oldcamera.Optimaldistance)
                camera.distance = Math.Clamp(camera.distance, camera.MinDistance, camera.MaxDistance)
            End If
        End If

        If Config_App.Current.Settings_Camara.FreezeCamera And oldcamera.Optimaldistance <> 0 And Not Force Then
            camera = oldcamera
        End If

    End Sub

    Public Sub GetSceneBounds(ByRef min As Vector3, ByRef max As Vector3)
        min = New Vector3(Single.MaxValue)
        max = New Vector3(Single.MinValue)
        Dim anyVisible As Boolean = False
        For Each mesh In Model.meshes
            ' Skip hidden shapes so the camera frames only what's actually drawn — mirror of the draw-time
            ' skip (Render: MeshData.Shape Is Nothing OrElse RenderHide). Without this, hiding the body
            ' (e.g. the Edit Outfit "piece only" preview, or "Render body" off) still framed the invisible
            ' body AABB, so a small visible piece ended up zoomed as if the whole body were present.
            If mesh.MeshData.Shape Is Nothing OrElse mesh.MeshData.Shape.RenderHide Then Continue For
            min = Vector3.ComponentMin(min, mesh.MeshData.Meshgeometry.Minv)
            max = Vector3.ComponentMax(max, mesh.MeshData.Meshgeometry.Maxv)
            anyVisible = True
        Next
        ' Fallback: if every shape is hidden, frame all meshes so the camera math doesn't degenerate.
        If Not anyVisible Then
            For Each mesh In Model.meshes
                min = Vector3.ComponentMin(min, mesh.MeshData.Meshgeometry.Minv)
                max = Vector3.ComponentMax(max, mesh.MeshData.Meshgeometry.Maxv)
            Next
        End If
    End Sub
    Public Sub CenterCamera()
        If Me.IsInDesignMode Then Return

        ' 1) AABB
        Dim minB As Vector3, maxB As Vector3
        GetSceneBounds(minB, maxB)

        ' 2) Centro y tamaño
        Dim center As Vector3 = (minB + maxB) * 0.5F
        Dim size As Vector3 = maxB - minB

        ' 3) Focus y orbit mode
        camera.FocusPosition = center

        ' 4) Parámetros de cámara
        Dim fovY As Single = MathHelper.DegreesToRadians(45.0F)
        Dim aspect As Single = Me.Width / CSng(Me.Height)

        ' ** Usamos Z para altura, X para anchura y Y para profundidad (hacia la cámara) **
        Dim halfH As Single = size.Z * 0.5F   ' vertical ? Z
        Dim halfW As Single = size.X * 0.5F   ' horizontal ? X
        Dim halfD As Single = size.Y * 0.5F   ' profundidad ? Y

        ' 5) Calculamos distancias mínimas sin margen
        Dim distH = halfH / CSng(Math.Tan(fovY * 0.5F))
        Dim fovX = 2.0F * CSng(Math.Atan(Math.Tan(fovY * 0.5F) * aspect))
        Dim distW = halfW / CSng(Math.Tan(fovX * 0.5F))

        ' 6) Margen uniforme (p.ej. 15% extra)
        Dim marginPct As Single = 0.1F
        ' SUMAMOS la media profundidad para asegurar que el punto más cercano también entra en FOV
        Dim baseDistance As Single = halfD + Math.Max(distH, distW)
        Dim idealDistance As Single = baseDistance * (1.0F + marginPct)
        Dim oldMin = camera.MinDistance, oldMax = camera.MaxDistance
        camera.MaxDistance = idealDistance * 10
        camera.MinDistance = idealDistance / 10
        Dim clampedDist = Math.Clamp(idealDistance, camera.MinDistance, camera.MaxDistance)
        camera.distance = clampedDist
        camera.Optimaldistance = camera.distance

        ' 7) Reset ángulos y orientación
        camera.angleX = 0F
        camera.angleY = 0F
        camera.UpdateDirectionFromAngles()
        UpdateProjection(True)
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then Clean()
        MyBase.Dispose(disposing)
    End Sub
    ' Heartbeat for the safety repaint: tick count since the last presented frame.
    ' At 16ms/tick, 63 ticks ˜ 1s. When this overflows we force a present so the
    ' control recovers from front-buffer loss (hide/show, handle recreation, DWM).
    Private _ticksSinceLastPresent As Integer = 0
    Private Const SafetyRepaintTicks As Integer = 63  ' ˜1000 ms at Interval=16

    Private Sub RenderTimer_Tick(sender As Object, e As EventArgs) Handles RenderTimer.Tick
        ' Bail out if Clean/Dispose started — Tick runs on UI thread, but a stale
        ' Tick scheduled before Clean()'s timer.Stop() can still arrive.
        If _isTearingDown OrElse RenderTimer Is Nothing OrElse Me.Disposing OrElse Me.IsDisposed Then Exit Sub

        ' Pull-based: if the intent has pending work, execute the pipeline
        If _renderIntent IsNot Nothing AndAlso _renderIntent.HasWork Then
            ExecuteRenderPipeline()
        End If

        ' On-demand repaint: any subsystem (mouse, texture loader, callback) that
        ' set UpdateRequired=True schedules a paint this tick.
        Dim texturesPending As Boolean = (Model IsNot Nothing AndAlso Not Model.TexturesReady)
        Dim onDemand As Boolean = UpdateRequired OrElse texturesPending

        ' Safety net: if no frame has been presented for ~1s, force one. Covers
        ' loss of the front buffer (hide/show, handle recreation, DWM compositor)
        ' without paying the cost of redrawing every tick.
        '
        ' GUARD: only fire the safety repaint if we currently own the GL context. Multiple
        ' PreviewControls coexist across MainForm + modal editors (EditFace_Form,
        ' EditBody_Form, etc.), each with its own GL context. OpenTK's "current context" is
        ' per-thread, process-wide — Invalidate→OnPaint→MakeCurrent on a non-current control
        ' steals the context from whichever sibling owns it right now (typically mid-frame),
        ' corrupting both renders. If we are not current, hold the counter at the threshold so
        ' we re-fire on the very next tick once the context returns to us, rather than waiting
        ' another full second.
        _ticksSinceLastPresent += 1
        Dim safetyDue As Boolean = (_ticksSinceLastPresent >= SafetyRepaintTicks)
        If safetyDue Then
            Dim isCurrent As Boolean

            Try
                isCurrent = (Me.Context IsNot Nothing AndAlso Me.Context.IsCurrent)
            Catch
                isCurrent = False
            End Try
            If Not isCurrent Then
                safetyDue = False
                _ticksSinceLastPresent = SafetyRepaintTicks
            End If
        End If

        If onDemand OrElse safetyDue Then
            UpdateRequired = True
            Me.Invalidate()
        End If
    End Sub
    ''' <summary>
    ''' Quiesces the render loop without freeing GL resources. Call this BEFORE disposing
    ''' anything that owns GL handles (host caches, tint caches, etc.) so that paints
    ''' queued by the safety-repaint heartbeat cannot drain mid-teardown and draw against
    ''' handles the host is about to delete. After this returns the GL context is still
    ''' alive — Clean()/Dispose() can be called next to actually release resources.
    ''' Idempotent.
    ''' </summary>
    Public Sub BeginTeardown()
        _isTearingDown = True
        If RenderTimer IsNot Nothing Then
            RenderTimer.Stop()
            RenderTimer.Dispose()
            RenderTimer = Nothing
        End If
        UpdateRequired = False
    End Sub

    Public Sub Clean()
        ' Mark teardown in progress BEFORE touching anything. Every GL-touching
        ' path checks this flag so queued WM_PAINTs draining mid-Clean cannot fire
        ' draw calls against shaders/VAOs/textures we are about to delete.
        ' If BeginTeardown was already called, this is a no-op for those two lines.
        BeginTeardown()

        If overlay IsNot Nothing Then
            overlay.Clean()
            overlay = Nothing
        End If

        If _Model IsNot Nothing Then
            _Model.Clean(True)
            _Model.CleanTextures()
            If _Model.Floor IsNot Nothing Then
                _Model.Floor.Dispose()
                _Model.Floor = Nothing
            End If
            _Model = Nothing
        End If

        If SharedActiveShader IsNot Nothing Then
            SharedActiveShader.Dispose()
            SharedActiveShader = Nothing
        End If

        If SharedSSEShader IsNot Nothing Then
            SharedSSEShader.Dispose()
            SharedSSEShader = Nothing
        End If

        If SharedFloorShader IsNot Nothing Then
            SharedFloorShader.Dispose()
            SharedFloorShader = Nothing
        End If
        If defaultWhiteTex <> 0 Then GL.DeleteTexture(defaultWhiteTex)
        If defaultNormalTex <> 0 Then GL.DeleteTexture(defaultNormalTex)
        If defaultFacegenDetailTex <> 0 Then GL.DeleteTexture(defaultFacegenDetailTex)
        If defaultFacegenSubsurfaceTex <> 0 Then GL.DeleteTexture(defaultFacegenSubsurfaceTex)
        If defaultCubeMap <> 0 Then GL.DeleteTexture(defaultCubeMap)
#If DEBUG Then
        GL.DebugMessageCallback(Nothing, IntPtr.Zero)
#End If
    End Sub
    Protected Overrides Sub Finalize()
        MyBase.Finalize()
    End Sub
End Class
Public Class PreviewModel

    Public Textures_Dictionary As New Dictionary(Of String, Texture_Loaded_Class)(StringComparer.OrdinalIgnoreCase)
    ''' <summary>Paths of COLOR textures (diffuse / base color) that must be sampled as sRGB so the GPU
    ''' gamma-decodes them on load (mirroring the engine's per-texture sRGB flag + MakeSRGB). Populated in
    ''' Process_Textures_GL from each material's color-texture roles; read by the Phase-2 GL upload. Data
    ''' textures (normal/spec/mask/flow) are NOT added -> they stay linear.</summary>
    Public ReadOnly SRGBTexturePaths As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
    Public Can_Render As Boolean = False
    Public Property TexturesReady As Boolean = True

    ''' <summary>UTC deadline for the post-texture-upload watchdog. Set when a background
    ''' upload begins (TexturesReady False→pending). When <see cref="ProcessPendingTextureUploads"/>
    ''' detects this deadline has passed without all uploads completing, it fires
    ''' <see cref="RenderIntent.PostTextureUploadTimeoutAction"/> instead of waiting forever.
    ''' Cleared (Nothing) once either the success or timeout action has fired so the next render
    ''' starts with a clean slate.</summary>
    Private _postTextureUploadDeadlineUtc As DateTime?

    ''' <summary>True iff <see cref="LoadTexturesAsync"/> armed a watchdog deadline whose
    ''' callbacks have not yet fired. Used by <see cref="PreviewControl.ExecuteRenderPipeline"/>
    ''' to distinguish "no async load needed, fire hook synchronously" from "async load in
    ''' progress, let the watchdog handle it".</summary>
    Public ReadOnly Property HasPendingPostTextureDeadline As Boolean
        Get
            Return _postTextureUploadDeadlineUtc.HasValue
        End Get
    End Property

    ''' <summary>Synchronous success-path dispatch of the post-texture-upload hook for the case
    ''' when the pipeline did NOT trigger an async load (texture cache reuse / no new shapes).
    ''' Same one-shot semantics as the watchdog success path: clear callbacks first, invoke
    ''' inside Try, MarkRenderBucketsDirty after.</summary>
    Public Sub FlushPostTextureUploadHookSyncSuccess()
        InvokePostTextureUploadHook(success:=True)
    End Sub
    Public meshes As New List(Of RenderableMesh)
    Private ReadOnly ParentControl As PreviewControl
    Public Floor As FloorRenderer
    Public Property LoadedShapes As New List(Of IRenderableShape)
    Public Property Cleaned As Boolean = True
    Public Property SingleBoneSkinning As Boolean = False
    Public Property RecalculateNormals As Boolean = True
    Private ReadOnly OpaqueMeshes As New List(Of RenderableMesh)
    Private ReadOnly CutoutMeshes As New List(Of RenderableMesh)
    Private ReadOnly DecalMeshes As New List(Of RenderableMesh)
    Private ReadOnly BlendedMeshes As New List(Of RenderableMesh)
    Private ReadOnly BlendedDepthBuffer As New List(Of MeshDepth)
    Private RenderBucketsDirty As Boolean = True
    Private Shared Function CompareMeshIdx(x As RenderableMesh, y As RenderableMesh) As Integer
        Return x.MeshData.Idx.CompareTo(y.MeshData.Idx)
    End Function

    Public Sub MarkRenderBucketsDirty()
        RenderBucketsDirty = True
    End Sub

    Private Sub RebuildRenderBuckets()
        OpaqueMeshes.Clear()
        CutoutMeshes.Clear()
        DecalMeshes.Clear()
        BlendedMeshes.Clear()
        BlendedDepthBuffer.Clear()

        For Each mesh In meshes
            If IsNothing(mesh) OrElse IsNothing(mesh.MeshData) OrElse IsNothing(mesh.MeshData.Shape) Then Continue For

            Dim isWireframe As Boolean = mesh.MeshData.Shape.Wireframe
            Dim material = mesh.MeshData.Material
            Dim hasAlphaBlend As Boolean = Not IsNothing(material) AndAlso material.HasAlphaBlend
            Dim hasAlphaTest As Boolean = Not IsNothing(material) AndAlso material.HasAlphaTest

            Dim isDecal As Boolean = Not IsNothing(material) AndAlso material.MaterialBase.Decal

            If isWireframe Then
                BlendedMeshes.Add(mesh)
            ElseIf isDecal Then
                DecalMeshes.Add(mesh)
            ElseIf hasAlphaBlend Then
                BlendedMeshes.Add(mesh)
            ElseIf hasAlphaTest Then
                CutoutMeshes.Add(mesh)
            Else
                OpaqueMeshes.Add(mesh)
            End If
        Next

        OpaqueMeshes.Sort(AddressOf CompareMeshIdx)
        CutoutMeshes.Sort(AddressOf CompareMeshIdx)
        DecalMeshes.Sort(AddressOf CompareMeshIdx)
        BlendedMeshes.Sort(AddressOf CompareMeshIdx)

        RenderBucketsDirty = False
    End Sub
    Public Class Texture_Loaded_Class
        Public Property Loaded As Boolean = False
        Public Property Cubemap As Boolean = False
        Public Property Path As String = ""
        Public Property Size As New Size
        Public Property DGXFormat_Original As Integer
        Public Property DGXFormat_Final As Integer
        Public Property Texture_ID As Integer
        ''' <summary>True si se subió como SRV sRGB (color/diffuse): la GPU gamma-decodea al samplear ⇒ el
        ''' sample devuelve LINEAL. False = cruda. Se setea AL CARGAR con la decisión de rol (SRGBTexturePaths
        ''' / ColorTextures_Path_List). Viaja con la textura y se reusa (el compositor FaceTint lee el IsSRGB
        ''' del base para no doble-decodear el seed).</summary>
        Public Property IsSRGB As Boolean = False

        ''' <summary>True cuando el Texture_ID actual lo instalo un compositor (FaceTint / fold SSE) y NO el
        ''' loader de DDS. Sirve para saber si se puede LIBERAR al reemplazarlo: la textura del loader puede
        ''' seguir referenciada en otro lado (borrarla deja el sampler en BLANCO), pero una que instalamos
        ''' nosotros no la referencia nadie mas una vez que se pisa el Texture_ID, y sin borrarla queda
        ''' huerfana para siempre (el fold se re-ejecuta en cada refresh de edicion en vivo: a 4096x4096 son
        ''' 268 MB de VRAM por tick). Se setea al instalar; el loader deja el default False.</summary>
        Public Property OwnedByComposer As Boolean = False

    End Class
    Public Class RenderableMesh
        Public Class MeshData_Class
            Sub New(Parent As RenderableMesh)
                ParentMesh = Parent
            End Sub
            Sub New()
            End Sub
            Public Property ParentMesh As RenderableMesh
            Public ReadOnly Property ShapeName As String
                Get
                    Return Shape.ShapeName
                End Get
            End Property

            Public ReadOnly Property Idx As Integer
                Get
                    Return Shape.ShapeIndex
                End Get
            End Property

            Public Meshgeometry As SkinnedGeometry
            Public Property Material As MaterialData
            Public Property Transform As Matrix4 = Matrix4.Identity
            Public Property Shape As IRenderableShape

        End Class


        Public vao As Integer
        Public ebo As Integer
        Private vboPosition As Integer
        Private vboNormal As Integer
        Private vboTangent As Integer
        Private vboBitangent As Integer

        Public vboColorAlpha As Integer
        Public vboUVMaskWeight As Integer



        ' Añade **sólo** estas dos líneas:
        Private vboMask As Integer                                    ' VBO dedicado a máscara

        ' GPU Skinning: SSBO for bone matrices + VBOs for per-vertex bone indices/weights
        Private ssbo_BoneMatrices As Integer = 0  ' SSBO for bone matrices
        ' Capacity (in bytes) the SSBO was allocated with via glBufferData. UpdateBoneMatricesSSBO
        ' compares the current GPUBoneMatrices.Length*64 against this — if the array grew, a plain
        ' BufferSubData fails with GL_INVALID_VALUE because the driver only sees the original size.
        ' Diagnostic only for now: log the mismatch with shape identity so we can find the call
        ' site that's reassigning GPUBoneMatrices to a bigger array post-creation.
        Private ssbo_BoneMatricesCapacityBytes As Integer = 0
        Private vboBoneIndices As Integer = 0     ' VBO for per-vertex bone indices
        Private vboBoneWeights As Integer = 0     ' VBO for per-vertex bone weights

        ' Tracks which skinning mode was used for the last VBO upload.
        ' When the mode changes, all vertices must be re-uploaded.
        Private _lastUploadWasGPU As Boolean = True

        ' O3.3: Cached AABB for frustum culling
        Public BoundsMin As Vector3
        Public BoundsMax As Vector3

        Public MeshData As MeshData_Class
        Private indexCount As Integer

        ' Clean CPU-side zap state. When ApplyZaps is on we filter the element buffer to drop every
        ' triangle that references a zapped vertex (VertexMask = -1) instead of relying on the ragged
        ' 'flat ZappedVert' shader discard. EnsureZapIndexBuffer rebuilds only when the geometry's
        ' ZapTopologyDirty flag is set (MorphEngine.ApplyMorphPlan is the single writer of VertexMask=-1)
        ' or when the ApplyZaps toggle flips (_lastApplyZaps tracks the last observed state).
        Private _zapFilteredActive As Boolean = False
        Private _lastApplyZaps As Boolean = False
        ' Per-segment worn-slot occlusion (Fase 2): last observed Shape.CoveredSlotsMask + cached
        ' hidden-triangle set. The dirty gate also rebuilds when the mask changes; _occlHidden is
        ' indexed by the shape's triangle index (same order as geom.Indices, see EnsureZapIndexBuffer).
        ' Initialized to a sentinel no real mask equals so the FIRST draw always computes occlusion —
        ' an N+100 occupied-variant segment is HIDDEN at mask 0 (the "no item" default), so a shape
        ' with coveredMask=0 must not skip its first pass and leave those segments showing.
        Private _lastCoveredSlotsMask As UInteger = &HFFFFFFFFUI
        ' Last observed Config_App.Setting_DrawHiddenSegments. Sentinel-init True (opposite of the
        ' lib default False) so the dirty gate's first pass always recomputes occlusion regardless
        ' of the runtime value.
        Private _lastDrawHidden As Boolean = True
        Private _occlHidden As Boolean() = Nothing
        Public Class MaterialData
            Sub New(Parent As MeshData_Class)
                ParentMeshData = Parent
            End Sub
            Public Property ParentMeshData As MeshData_Class

            ''' <summary>Optional render-only material override (LooksMenu overlay layer). When set,
            ''' MaterialBase (and everything that flows through it: Textures_Path_List, the *_ID props,
            ''' HasAlphaBlend, ...) reads this material instead of the shape's own ShapeMaterial — so a
            ''' transient MaterialData can render an overlay layer's material over the SAME base geometry.
            ''' Defaults Nothing, so every existing mesh resolves through ParentMeshData.Shape.ShapeMaterial
            ''' exactly as before (the no-overlay path is unchanged).</summary>
            Public Property OverrideRelatedMaterial As Nifcontent_Class_Manolo.RelatedMaterial_Class = Nothing

            Public ReadOnly Property MaterialBase As FO4UnifiedMaterial_Class
                Get
                    ' Overlay layer: bind the override material (the app pre-configured it). Same
                    ' null-safety as the base path below.
                    If OverrideRelatedMaterial IsNot Nothing Then
                        If OverrideRelatedMaterial.material Is Nothing Then Return New FO4UnifiedMaterial_Class()
                        Return OverrideRelatedMaterial.material
                    End If
                    Dim rel = ParentMeshData.Shape.ShapeMaterial
                    If rel Is Nothing OrElse rel.material Is Nothing Then Return New FO4UnifiedMaterial_Class()
                    Return rel.material
                End Get
            End Property

            ''' <summary>Optional GL texture ID of a face tint overlay (TETI/TEND composed via FBO).
            ''' When &gt; 0, the shader will sample this texture and blend it ON TOP of the face diffuse.
            ''' Lives on MaterialData (not on the shared FO4UnifiedMaterial_Class) so it survives material
            ''' cloning — each RenderableMesh keeps its own composed overlay.</summary>
            Public Property FaceTintOverlay_ID As Integer = 0

            ''' <summary>"Ya está" flag — the skin tone is ALREADY baked into this mesh's diffuse, so the
            ''' render shader's own SkinTint soft-light (tintColor branch) must be a no-op for it; otherwise
            ''' the tone is applied twice. Set True by the NPC manager after the FaceTint compositor bakes
            ''' the slot-12 tone into the FACE diffuse (TryApplyFaceTints), and on the Skyrim legacy BODY
            ''' bake path. Stays False for the FO4 BODY, whose tone is soft-lit at render from
            ''' <see cref="SkinToneColor"/> (engine model, NOT a double). Per-mesh on MaterialData so it
            ''' survives material cloning, same as <see cref="SkinToneColor"/> / <see cref="FaceTintOverlay_ID"/>.</summary>
            Public Property SkinToneBaked As Boolean = False

            ' OS-faithful blend decision. Two independent triggers, either suffices:
            '   1. NIF NiAlphaProperty.Flags.AlphaBlend (bit 0) — carried in the wrapper's
            '      AlphaBlendEnabled field (Apply'd from the shape's NiAlphaProperty at load).
            '   2. material.Alpha < 1.0 — the BGSM-level alpha multiplier. OS replicates
            '      this even when there is no NiAlphaProperty on the shape (GLShader.cpp:186):
            '        if (!alphaBlend && value < 1.0f) { glEnable(GL_BLEND); glBlendFunc(SrcAlpha, InvSrcAlpha); }
            ' Testigo: NIF sin NiAlphaProperty + BGSM Unknown + Alpha < 1 → OS blendea,
            ' la regla previa "enum-based" no — el enum Unknown perdía la independencia que
            ' el modelo de tres campos restauró, pero el render todavía consultaba el enum.
            Public ReadOnly Property HasAlphaBlend
                Get
                    ' An overlay layer carries its own material in OverrideRelatedMaterial, so the
                    ' "no material on the shape" guard must consult the override too — otherwise an
                    ' overlay over a shape with no ShapeMaterial would wrongly report not-blended.
                    If OverrideRelatedMaterial Is Nothing AndAlso IsNothing(ParentMeshData.Shape.ShapeMaterial) Then Return False
                    Return MaterialBase.AlphaBlendEnabled OrElse MaterialBase.Alpha < 1.0F
                End Get
            End Property

            Public ReadOnly Property HasAlphaTest
                Get
                    If OverrideRelatedMaterial Is Nothing AndAlso IsNothing(ParentMeshData.Shape.ShapeMaterial) Then Return False
                    Return MaterialBase.AlphaTest
                End Get
            End Property
            ' Resolve the GL blend factors for the active blend mode. Two cases mirror
            ' OS GLShader.cpp:181-189:
            '   - NIF NiAlphaProperty drives blend → use the loaded Source/Dest verbatim
            '     (whatever the author set, including exotic combos that classify Unknown).
            '   - blend forced by Alpha<1 (no NIF flag) → OS hardcodes SRC_ALPHA/INV_SRC_ALPHA;
            '     the BGSM-level Alpha multiplier doesn't carry per-shape factors so this is
            '     the only sensible default.
            Public Function Calculate_Blending() As Integer()
                If MaterialBase.AlphaBlendEnabled Then
                    Return {CInt(MapAlphaFunctionToBlendingFactor(MaterialBase.BlendFunctionSource)),
                            CInt(MapAlphaFunctionToBlendingFactor(MaterialBase.BlendFunctionDest))}
                End If
                Return {CInt(BlendingFactor.SrcAlpha), CInt(BlendingFactor.OneMinusSrcAlpha)}
            End Function

            Private Shared Function MapAlphaFunctionToBlendingFactor(f As NiflySharp.Enums.AlphaFunction) As BlendingFactor
                Select Case f
                    Case NiflySharp.Enums.AlphaFunction.SRC_ALPHA : Return BlendingFactor.SrcAlpha
                    Case NiflySharp.Enums.AlphaFunction.INV_SRC_ALPHA : Return BlendingFactor.OneMinusSrcAlpha
                    Case NiflySharp.Enums.AlphaFunction.SRC_COLOR : Return BlendingFactor.SrcColor
                    Case NiflySharp.Enums.AlphaFunction.INV_SRC_COLOR : Return BlendingFactor.OneMinusSrcColor
                    Case NiflySharp.Enums.AlphaFunction.DEST_ALPHA : Return BlendingFactor.DstAlpha
                    Case NiflySharp.Enums.AlphaFunction.INV_DEST_ALPHA : Return BlendingFactor.OneMinusDstAlpha
                    Case NiflySharp.Enums.AlphaFunction.DEST_COLOR : Return BlendingFactor.DstColor
                    Case NiflySharp.Enums.AlphaFunction.INV_DEST_COLOR : Return BlendingFactor.OneMinusDstColor
                    Case NiflySharp.Enums.AlphaFunction.ONE : Return BlendingFactor.One
                    Case NiflySharp.Enums.AlphaFunction.ZERO : Return BlendingFactor.Zero
                    Case NiflySharp.Enums.AlphaFunction.SRC_ALPHA_SATURATE : Return BlendingFactor.SrcAlphaSaturate
                    Case Else : Return BlendingFactor.SrcAlpha
                End Select
            End Function


            Public ReadOnly Property Textures_Path_List As IEnumerable(Of String)
                Get
                    Return {FO4UnifiedMaterial_Class.CorrectTexturePath(MaterialBase.NormalTexture),
                     FO4UnifiedMaterial_Class.CorrectTexturePath(MaterialBase.Diffuse_or_Base_Texture),
                     FO4UnifiedMaterial_Class.CorrectTexturePath(MaterialBase.SmoothSpecTexture),
                     FO4UnifiedMaterial_Class.CorrectTexturePath(MaterialBase.GreyscaleTexture),
                     FO4UnifiedMaterial_Class.CorrectTexturePath(MaterialBase.EnvmapTexture),
                     FO4UnifiedMaterial_Class.CorrectTexturePath(MaterialBase.FlowTexture),
                     FO4UnifiedMaterial_Class.CorrectTexturePath(MaterialBase.GlowTexture),
                     FO4UnifiedMaterial_Class.CorrectTexturePath(MaterialBase.DisplacementTexture),
                     FO4UnifiedMaterial_Class.CorrectTexturePath(MaterialBase.InnerLayerTexture),
                     FO4UnifiedMaterial_Class.CorrectTexturePath(MaterialBase.LightingTexture),
                     FO4UnifiedMaterial_Class.CorrectTexturePath(MaterialBase.SpecularTexture),
                     FO4UnifiedMaterial_Class.CorrectTexturePath(MaterialBase.WrinklesTexture),
                     FO4UnifiedMaterial_Class.CorrectTexturePath(MaterialBase.DistanceFieldAlphaTexture),
                     FO4UnifiedMaterial_Class.CorrectTexturePath(MaterialBase.EnvmapMaskTexture)
                                              }
                End Get
            End Property

            ''' <summary>The COLOR textures of this material that the GPU must gamma-decode (sRGB) at load,
            ''' decided PER-SLOT from how the engine shaders sample each one (a slot is sRGB iff its sample is
            ''' used as a color feeding linear lighting). Returns: Diffuse (unless grayscale-recolor, where it
            ''' is a data index map), InnerLayer (inner base color), and the Envmap cube (a color reflection
            ''' added to the linear output; format-aware -> only LDR cubes upgrade). NOT data slots
            ''' (Normal/SmoothSpec/Specular/EnvMask/Flow/Wrinkles/Displacement/Lighting/DistanceField), NOT the
            ''' palette LUT (decoded in-shader), NOT Glow (ambiguous + dual-use hair flow), NOT BGEM (display
            ''' space). See the body for the per-slot rationale.</summary>
            Public ReadOnly Property ColorTextures_Path_List As IEnumerable(Of String)
                Get
                    If MaterialBase.IsBGEM Then Return Array.Empty(Of String)()
                    Dim colors As New List(Of String)()
                    If Not MaterialBase.GrayscaleToPaletteColor Then colors.Add(FO4UnifiedMaterial_Class.CorrectTexturePath(MaterialBase.Diffuse_or_Base_Texture))
                    ' InnerLayer: en FACEGEN (SSE FaceTint) el InnerLayer es el facetint _d = DATA, no color. El engine
                    ' lo samplea CRUDO para fgTint=(t4+off)·255/64 — la neutral (63,64,63)/255 da fgTint=1 SÓLO si es
                    ' raw (sRGB daría 0.214 y oscurecería). El live render ya lo sube IsSRGB=False (NpcFaceTintResolver).
                    ' Sólo es COLOR (sRGB) en el multilayer NO-facegen. Sin este gate, un NIF facegen cargado standalone
                    ' samplea el facetint sRGB y renderiza oscuro (bug del _2c). FO4 no-facegen intacto.
                    If Not MaterialBase.Facegen Then colors.Add(FO4UnifiedMaterial_Class.CorrectTexturePath(MaterialBase.InnerLayerTexture))
                    colors.Add(FO4UnifiedMaterial_Class.CorrectTexturePath(MaterialBase.EnvmapTexture))
                    Return colors
                End Get
            End Property
            Private Function GetTextureID(texturePath As String) As UInteger
                If String.IsNullOrEmpty(texturePath) Then Return 0
                Dim tex As Texture_Loaded_Class = Nothing
                If ParentMeshData.ParentMesh.ParentModel.Textures_Dictionary.TryGetValue(texturePath, tex) Then Return tex.Texture_ID
                Return 0
            End Function
            Private Function TryGetTexture(texturePath As String, ByRef tex As Texture_Loaded_Class) As Boolean
                If String.IsNullOrEmpty(texturePath) Then
                    tex = Nothing
                    Return False
                End If
                Return ParentMeshData.ParentMesh.ParentModel.Textures_Dictionary.TryGetValue(texturePath, tex)
            End Function
            Public ReadOnly Property DiffuseTexture_ID As UInteger
                Get
                    Return GetTextureID(FO4UnifiedMaterial_Class.CorrectTexturePath(MaterialBase.Diffuse_or_Base_Texture))
                End Get
            End Property
            Public ReadOnly Property NormalTexture_ID As UInteger
                Get
                    Return GetTextureID(FO4UnifiedMaterial_Class.CorrectTexturePath(MaterialBase.NormalTexture))
                End Get
            End Property
            Public ReadOnly Property SpecularTexture_ID As UInteger
                Get
                    Return GetTextureID(FO4UnifiedMaterial_Class.CorrectTexturePath(MaterialBase.SpecularTexture))
                End Get
            End Property
            Public ReadOnly Property SmoothSpecTexture_ID As UInteger
                Get
                    Return GetTextureID(FO4UnifiedMaterial_Class.CorrectTexturePath(MaterialBase.SmoothSpecTexture))
                End Get
            End Property
            Public ReadOnly Property EnvmapTexture_ID As UInteger
                Get
                    Return GetTextureID(FO4UnifiedMaterial_Class.CorrectTexturePath(MaterialBase.EnvmapTexture))
                End Get
            End Property
            Public ReadOnly Property GreyscaleTexture_ID As UInteger
                Get
                    Return GetTextureID(FO4UnifiedMaterial_Class.CorrectTexturePath(MaterialBase.GreyscaleTexture))
                End Get
            End Property
            Public ReadOnly Property GlowTexture_ID As UInteger
                Get
                    Return GetTextureID(FO4UnifiedMaterial_Class.CorrectTexturePath(MaterialBase.GlowTexture))
                End Get
            End Property
            Public ReadOnly Property WrinklesTexture_ID As UInteger
                Get
                    Return GetTextureID(FO4UnifiedMaterial_Class.CorrectTexturePath(MaterialBase.WrinklesTexture))
                End Get
            End Property
            Public ReadOnly Property DisplacementTexture_ID As UInteger
                Get
                    Return GetTextureID(FO4UnifiedMaterial_Class.CorrectTexturePath(MaterialBase.DisplacementTexture))
                End Get
            End Property
            Public ReadOnly Property InnerLayerTexture_ID As UInteger
                Get
                    Return GetTextureID(FO4UnifiedMaterial_Class.CorrectTexturePath(MaterialBase.InnerLayerTexture))
                End Get
            End Property
            Public ReadOnly Property LightingTexture_ID As UInteger
                Get
                    Return GetTextureID(FO4UnifiedMaterial_Class.CorrectTexturePath(MaterialBase.LightingTexture))
                End Get
            End Property
            Public ReadOnly Property DistanceFieldAlphaTexture_ID As UInteger
                Get
                    Return GetTextureID(FO4UnifiedMaterial_Class.CorrectTexturePath(MaterialBase.DistanceFieldAlphaTexture))
                End Get
            End Property

            Public ReadOnly Property EnvmapMaskTexture_ID As UInteger
                Get
                    Dim key As String = FO4UnifiedMaterial_Class.CorrectTexturePath(MaterialBase.EnvmapMaskTexture)
                    If key = "" Then Return 0
                    Dim tex As Texture_Loaded_Class = Nothing
                    If Not TryGetTexture(key, tex) Then Return 0
                    If tex.Cubemap = True Then Return 0
                    Return tex.Texture_ID
                End Get
            End Property
            Public ReadOnly Property FlowTexture_ID As UInteger
                Get
                    Return GetTextureID(FO4UnifiedMaterial_Class.CorrectTexturePath(MaterialBase.FlowTexture))
                End Get
            End Property
            Public ReadOnly Property DetailMaskTexture_ID As UInteger
                Get
                    Return GetTextureID(FO4UnifiedMaterial_Class.CorrectTexturePath(MaterialBase.DetailMaskTexture))
                End Get
            End Property

            Public ReadOnly Property HasCubemap As Boolean
                Get
                    Dim tex As Texture_Loaded_Class = Nothing
                    If Not TryGetTexture(FO4UnifiedMaterial_Class.CorrectTexturePath(MaterialBase.EnvmapTexture), tex) Then Return False
                    Return tex.Cubemap
                End Get
            End Property

            Public ReadOnly Property HasGrayscale As Boolean
                Get
                    Dim tex As Texture_Loaded_Class = Nothing
                    If Not TryGetTexture(FO4UnifiedMaterial_Class.CorrectTexturePath(MaterialBase.GreyscaleTexture), tex) Then Return False
                    Return tex.Loaded
                End Get
            End Property



        End Class

        Private ReadOnly ParentModel As PreviewModel

        Public Sub Clean()
            ' — Eliminar VAO y buffers de atributos —
            If vao > 0 Then GL.DeleteVertexArray(vao) : vao = 0
            If ebo > 0 Then GL.DeleteBuffer(ebo) : ebo = 0
            If vboPosition > 0 Then GL.DeleteBuffer(vboPosition) : vboPosition = 0
            If vboNormal > 0 Then GL.DeleteBuffer(vboNormal) : vboNormal = 0
            If vboTangent > 0 Then GL.DeleteBuffer(vboTangent) : vboTangent = 0
            If vboBitangent > 0 Then GL.DeleteBuffer(vboBitangent) : vboBitangent = 0
            If vboColorAlpha > 0 Then GL.DeleteBuffer(vboColorAlpha) : vboColorAlpha = 0
            If vboUVMaskWeight > 0 Then GL.DeleteBuffer(vboUVMaskWeight) : vboUVMaskWeight = 0
            If vboMask > 0 Then GL.DeleteBuffer(vboMask) : vboMask = 0

            ' GPU Skinning: clean up SSBO and bone attribute VBOs
            If ssbo_BoneMatrices > 0 Then GL.DeleteBuffer(ssbo_BoneMatrices) : ssbo_BoneMatrices = 0
            If vboBoneIndices > 0 Then GL.DeleteBuffer(vboBoneIndices) : vboBoneIndices = 0
            If vboBoneWeights > 0 Then GL.DeleteBuffer(vboBoneWeights) : vboBoneWeights = 0

            ' — Reducir flags de dirty-tracking a mínima expresión —
            MeshData.Meshgeometry = Nothing
        End Sub

        Public Sub New(data As MeshData_Class, Parent_Model As PreviewModel)
            MeshData = data
            ParentModel = Parent_Model
            MeshData.ParentMesh = Me
        End Sub

        ''' <param name="recomputeBounds">True (default) = recomputa bounds tras el full upload (full-reload
        ''' y morph, que no tienen ComputeBounds aparte). El pose path pasa False porque sus bounds los
        ''' maneja la línea gateada del pass 1 (computeBoundsThisFrame); incondicional acá bypasseaba ese
        ''' gate Y Option B en CPU (8.9ms/frame de pasada per-vértice a mundo, medido). Nombre ≠ ComputeBounds
        ''' a propósito: VB es case-insensitive y un param 'computeBounds' sombrearía el método.</param>
        Public Sub UpdateSkinBuffers_GL(Optional recomputeBounds As Boolean = True)
            ' Actualiza VBOs de Normales, Tangentes, Bitangentes y Posiciones
            ' Detect skinning mode change: if the toggle changed since last upload, force ALL dirty
            Dim _swCtx = System.Diagnostics.Stopwatch.StartNew()
            Me.ParentModel.ParentControl.EnsureContextCurrent()
            ParentModel.ParentControl._skinCtxMs += _swCtx.Elapsed.TotalMilliseconds
            Dim gpuMode As Boolean = Config_App.Current.Setting_GPUSkinning
            If gpuMode <> _lastUploadWasGPU Then
                _lastUploadWasGPU = gpuMode
                If MeshData.Meshgeometry.Vertices IsNot Nothing AndAlso MeshData.Meshgeometry.Vertices.Length > 0 Then
                    MeshData.Meshgeometry.dirtyVertexIndices = New HashSet(Of Integer)(Enumerable.Range(0, MeshData.Meshgeometry.Vertices.Length))
                    Array.Fill(MeshData.Meshgeometry.dirtyVertexFlags, True)
                End If
            End If

            If MeshData.Meshgeometry.dirtyVertexIndices.Count > 0 Then
                Const elementSize As Integer = 3 * 4
                Dim vertexCount As Integer = MeshData.Meshgeometry.Vertices.Length
                Dim totalBytes As Integer = vertexCount * elementSize
                Dim cpuSkin As Boolean = Not gpuMode AndAlso MeshData.Meshgeometry.PerVertexSkinMatrix IsNot Nothing

                ' O3.1: Smart threshold — full BufferSubData upload when >60% vertices are dirty
                If MeshData.Meshgeometry.dirtyVertexIndices.Count > vertexCount * 0.6 Then
                    Dim _swSkinPhase = System.Diagnostics.Stopwatch.StartNew()   ' [RENDER-MS] compute vs upload
                    Dim posF(vertexCount - 1) As Vector3
                    Dim nrmF(vertexCount - 1) As Vector3
                    Dim tanF(vertexCount - 1) As Vector3
                    Dim bitanF(vertexCount - 1) As Vector3

                    If cpuSkin Then
                        ' CPU skinning: transform local ? world using PerVertexSkinMatrix
                        Dim mats = MeshData.Meshgeometry.PerVertexSkinMatrix
                        Dim lv = MeshData.Meshgeometry.Vertices
                        Dim ln = MeshData.Meshgeometry.Normals
                        Dim lt = MeshData.Meshgeometry.Tangents
                        Dim lb = MeshData.Meshgeometry.Bitangents
                        Dim isMSN As Boolean = MeshData.Material?.MaterialBase IsNot Nothing AndAlso MeshData.Material.MaterialBase.ModelSpaceNormals
                        ' NOTA: antes habia una optimizacion "isSingle" que cacheaba un solo
                        ' normal matrix cuando mats(0) == mats(vertexCount-1), asumiendo que
                        ' todos los vertices tenian skinning uniforme. Falso positivo muy
                        ' facil de disparar (primer y ultimo vertex comparten bone pero los
                        ' del medio no), causando que las normales del medio usaran el nm3
                        ' del vertex 0. Se removio — ahora siempre per-vertex para coincidir
                        ' con el shader GPU que tambien computa skinNormalMat per-vertex.
                        Dim body As Action(Of Integer) = Sub(i)
                                                             Dim m = mats(i)
                                                             Dim wp = Vector3d.TransformPosition(lv(i), m)
                                                             posF(i) = New Vector3(CSng(wp.X), CSng(wp.Y), CSng(wp.Z))
                                                             Dim nm3 As Matrix3d = SkinningHelper.NormalMatrixOrIdentity(m)
                                                             If isMSN Then
                                                                 ' MSN: pack nm3.Row0/1/2 en los tres VBOs. El shader los lee y
                                                                 ' reconstruye via mat3(vertexNormal, vertexTangent, vertexBitangent).
                                                                 ' GLSL column-major: col0=vertexNormal, col1=vertexTangent, col2=vertexBitangent.
                                                                 ' Target del shader es mat3(m)^-1 (lo que tambien computa el GPU path).
                                                                 ' Como nm3 = (m^-1)^T, tenemos nm3.Row_i = math Col_i de m^-1, osea
                                                                 ' packear las filas de nm3 da exactamente las columnas del target GLSL.
                                                                 nrmF(i) = New Vector3(CSng(nm3.Row0.X), CSng(nm3.Row0.Y), CSng(nm3.Row0.Z))
                                                                 tanF(i) = New Vector3(CSng(nm3.Row1.X), CSng(nm3.Row1.Y), CSng(nm3.Row1.Z))
                                                                 bitanF(i) = New Vector3(CSng(nm3.Row2.X), CSng(nm3.Row2.Y), CSng(nm3.Row2.Z))
                                                             Else
                                                                 ' Aplicar nm3 al normal/tangent/bitangent via dot products explicitos.
                                                                 ' Convencion row-vector de OpenTK: result = v * nm3 donde
                                                                 ' result.i = v.X*Row0.i + v.Y*Row1.i + v.Z*Row2.i
                                                                 ' (dotted con las columnas matematicas de nm3).
                                                                 Dim lnI = ln(i) : Dim ltI = lt(i) : Dim lbI = lb(i)
                                                                 Dim r0 = nm3.Row0 : Dim r1 = nm3.Row1 : Dim r2 = nm3.Row2
                                                                 Dim wn As New Vector3d(
                                                                     lnI.X * r0.X + lnI.Y * r1.X + lnI.Z * r2.X,
                                                                     lnI.X * r0.Y + lnI.Y * r1.Y + lnI.Z * r2.Y,
                                                                     lnI.X * r0.Z + lnI.Y * r1.Z + lnI.Z * r2.Z)
                                                                 wn = Vector3d.Normalize(wn)
                                                                 nrmF(i) = New Vector3(CSng(wn.X), CSng(wn.Y), CSng(wn.Z))

                                                                 Dim wt As New Vector3d(
                                                                     ltI.X * r0.X + ltI.Y * r1.X + ltI.Z * r2.X,
                                                                     ltI.X * r0.Y + ltI.Y * r1.Y + ltI.Z * r2.Y,
                                                                     ltI.X * r0.Z + ltI.Y * r1.Z + ltI.Z * r2.Z)
                                                                 wt = Vector3d.Normalize(wt)
                                                                 tanF(i) = New Vector3(CSng(wt.X), CSng(wt.Y), CSng(wt.Z))

                                                                 Dim wb As New Vector3d(
                                                                     lbI.X * r0.X + lbI.Y * r1.X + lbI.Z * r2.X,
                                                                     lbI.X * r0.Y + lbI.Y * r1.Y + lbI.Z * r2.Y,
                                                                     lbI.X * r0.Z + lbI.Y * r1.Z + lbI.Z * r2.Z)
                                                                 wb = Vector3d.Normalize(wb)
                                                                 bitanF(i) = New Vector3(CSng(wb.X), CSng(wb.Y), CSng(wb.Z))
                                                             End If
                                                         End Sub
                        If vertexCount >= 500 Then Parallel.For(0, vertexCount, body) Else For i = 0 To vertexCount - 1 : body(i) : Next
                    Else
                        ' GPU skinning: upload local-space as-is
                        Dim gv = MeshData.Meshgeometry.Vertices
                        Dim gn = MeshData.Meshgeometry.Normals
                        Dim gt = MeshData.Meshgeometry.Tangents
                        Dim gb = MeshData.Meshgeometry.Bitangents
                        Dim gpuBody As Action(Of Integer) = Sub(i)
                                                                Dim vv = gv(i) : posF(i) = New Vector3(CSng(vv.X), CSng(vv.Y), CSng(vv.Z))
                                                                Dim nn = gn(i) : nrmF(i) = New Vector3(CSng(nn.X), CSng(nn.Y), CSng(nn.Z))
                                                                Dim tt = gt(i) : tanF(i) = New Vector3(CSng(tt.X), CSng(tt.Y), CSng(tt.Z))
                                                                Dim bb = gb(i) : bitanF(i) = New Vector3(CSng(bb.X), CSng(bb.Y), CSng(bb.Z))
                                                            End Sub
                        If vertexCount >= 2000 Then Parallel.For(0, vertexCount, gpuBody) Else For i = 0 To vertexCount - 1 : gpuBody(i) : Next
                    End If
                    ParentModel.ParentControl._skinComputeMs += _swSkinPhase.Elapsed.TotalMilliseconds : _swSkinPhase.Restart()

                    GL.BindBuffer(BufferTarget.ArrayBuffer, vboPosition)
                    GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, totalBytes, posF)

                    GL.BindBuffer(BufferTarget.ArrayBuffer, vboNormal)
                    GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, totalBytes, nrmF)

                    GL.BindBuffer(BufferTarget.ArrayBuffer, vboTangent)
                    GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, totalBytes, tanF)

                    GL.BindBuffer(BufferTarget.ArrayBuffer, vboBitangent)
                    GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, totalBytes, bitanF)

                    GL.BindBuffer(BufferTarget.ArrayBuffer, 0)
                    ParentModel.ParentControl._skinUploadMs += _swSkinPhase.Elapsed.TotalMilliseconds : _swSkinPhase.Restart()

                    ' Clear all dirty flags since everything was updated
                    For Each i As Integer In MeshData.Meshgeometry.dirtyVertexIndices
                        MeshData.Meshgeometry.dirtyVertexFlags(i) = False
                    Next
                    MeshData.Meshgeometry.dirtyVertexIndices.Clear()
                    ParentModel.ParentControl._skinDirtyMs += _swSkinPhase.Elapsed.TotalMilliseconds : _swSkinPhase.Restart()

                    ' Also recompute bounds after full update — SALVO cuando el caller ya los maneja.
                    ' En el pose path los computa la línea gateada del pass 1 ('If computeBoundsThisFrame
                    ' Then mesh.ComputeBounds()'); incondicional acá bypasseaba ese gate Y Option B en CPU
                    ' (ComputeBounds→GetWorldVertices = pasada per-vértice a mundo, 8.9ms/frame medido).
                    If recomputeBounds Then Me.ComputeBounds()
                    ParentModel.ParentControl._skinBoundsMs += _swSkinPhase.Elapsed.TotalMilliseconds : _swSkinPhase.Restart()

                    UpdateUpdateSkinBuffersMask_GL()
                    ParentModel.ParentControl._skinMaskMs += _swSkinPhase.Elapsed.TotalMilliseconds
                    Return
                End If

                ' Sparse update path — used when fewer vertices changed
                Dim mapMask As MapBufferAccessMask = MapBufferAccessMask.MapWriteBit Or MapBufferAccessMask.MapUnsynchronizedBit Or MapBufferAccessMask.MapFlushExplicitBit

                ' Mapear buffers
                GL.BindBuffer(BufferTarget.ArrayBuffer, vboNormal)
                Dim ptrN As IntPtr = GL.MapBufferRange(BufferTarget.ArrayBuffer, IntPtr.Zero, totalBytes, mapMask)
                GL.BindBuffer(BufferTarget.ArrayBuffer, vboTangent)
                Dim ptrT As IntPtr = GL.MapBufferRange(BufferTarget.ArrayBuffer, IntPtr.Zero, totalBytes, mapMask)
                GL.BindBuffer(BufferTarget.ArrayBuffer, vboBitangent)
                Dim ptrB As IntPtr = GL.MapBufferRange(BufferTarget.ArrayBuffer, IntPtr.Zero, totalBytes, mapMask)
                GL.BindBuffer(BufferTarget.ArrayBuffer, vboPosition)
                Dim ptrP As IntPtr = GL.MapBufferRange(BufferTarget.ArrayBuffer, IntPtr.Zero, totalBytes, mapMask)

                ' Un solo bucle para actualizar todos los atributos
                Dim buf(2) As Single
                Dim sparseMats = If(cpuSkin, MeshData.Meshgeometry.PerVertexSkinMatrix, Nothing)
                Dim sparseIsMSN As Boolean = cpuSkin AndAlso MeshData.Material?.MaterialBase IsNot Nothing AndAlso MeshData.Material.MaterialBase.ModelSpaceNormals
                ' NOTA: la optimizacion de cachear cachedNM3 basada en comparar sparseMats(0)
                ' con sparseMats(vertexCount-1) se removio — daba falsos positivos para
                ' shapes donde primer y ultimo vertex comparten bone pero el medio no, y
                ' causaba que las normales del medio usaran el nm3 del vertex 0. El shader
                ' GPU siempre computa skinNormalMat per-vertex; alineamos el CPU path.

                For Each i As Integer In MeshData.Meshgeometry.dirtyVertexIndices
                    Dim offsetBytes As Int64 = CLng(i) * elementSize
                    Dim baseN As IntPtr = ptrN + offsetBytes
                    Dim baseT As IntPtr = ptrT + offsetBytes
                    Dim baseB As IntPtr = ptrB + offsetBytes
                    Dim baseP As IntPtr = ptrP + offsetBytes

                    If cpuSkin Then
                        Dim m = sparseMats(i)
                        Dim nm3 As Matrix3d = SkinningHelper.NormalMatrixOrIdentity(m)

                        Dim wp = Vector3d.TransformPosition(MeshData.Meshgeometry.Vertices(i), m)
                        buf(0) = CSng(wp.X) : buf(1) = CSng(wp.Y) : buf(2) = CSng(wp.Z)
                        Marshal.Copy(buf, 0, baseP, 3)

                        If sparseIsMSN Then
                            ' MSN: packear las FILAS de nm3 en los VBOs. El shader reconstruye
                            ' mat3(vertexNormal, vertexTangent, vertexBitangent) con convencion
                            ' column-major, y como nm3.Row_i corresponde a la math Col_i del
                            ' target mat3(m)^-1, queda correcto. Coincide con el GPU path.
                            buf(0) = CSng(nm3.Row0.X) : buf(1) = CSng(nm3.Row0.Y) : buf(2) = CSng(nm3.Row0.Z)
                            Marshal.Copy(buf, 0, baseN, 3)
                            buf(0) = CSng(nm3.Row1.X) : buf(1) = CSng(nm3.Row1.Y) : buf(2) = CSng(nm3.Row1.Z)
                            Marshal.Copy(buf, 0, baseT, 3)
                            buf(0) = CSng(nm3.Row2.X) : buf(1) = CSng(nm3.Row2.Y) : buf(2) = CSng(nm3.Row2.Z)
                            Marshal.Copy(buf, 0, baseB, 3)
                        Else
                            ' Convencion row-vector de OpenTK: result = v * nm3 donde
                            ' result.i = v.X*Row0.i + v.Y*Row1.i + v.Z*Row2.i
                            Dim lnI = MeshData.Meshgeometry.Normals(i)
                            Dim ltI = MeshData.Meshgeometry.Tangents(i)
                            Dim lbI = MeshData.Meshgeometry.Bitangents(i)
                            Dim r0 = nm3.Row0 : Dim r1 = nm3.Row1 : Dim r2 = nm3.Row2

                            Dim wn As New Vector3d(
                                lnI.X * r0.X + lnI.Y * r1.X + lnI.Z * r2.X,
                                lnI.X * r0.Y + lnI.Y * r1.Y + lnI.Z * r2.Y,
                                lnI.X * r0.Z + lnI.Y * r1.Z + lnI.Z * r2.Z)
                            wn = Vector3d.Normalize(wn)
                            buf(0) = CSng(wn.X) : buf(1) = CSng(wn.Y) : buf(2) = CSng(wn.Z)
                            Marshal.Copy(buf, 0, baseN, 3)

                            Dim wt As New Vector3d(
                                ltI.X * r0.X + ltI.Y * r1.X + ltI.Z * r2.X,
                                ltI.X * r0.Y + ltI.Y * r1.Y + ltI.Z * r2.Y,
                                ltI.X * r0.Z + ltI.Y * r1.Z + ltI.Z * r2.Z)
                            wt = Vector3d.Normalize(wt)
                            buf(0) = CSng(wt.X) : buf(1) = CSng(wt.Y) : buf(2) = CSng(wt.Z)
                            Marshal.Copy(buf, 0, baseT, 3)

                            Dim wb As New Vector3d(
                                lbI.X * r0.X + lbI.Y * r1.X + lbI.Z * r2.X,
                                lbI.X * r0.Y + lbI.Y * r1.Y + lbI.Z * r2.Y,
                                lbI.X * r0.Z + lbI.Y * r1.Z + lbI.Z * r2.Z)
                            wb = Vector3d.Normalize(wb)
                            buf(0) = CSng(wb.X) : buf(1) = CSng(wb.Y) : buf(2) = CSng(wb.Z)
                            Marshal.Copy(buf, 0, baseB, 3)
                        End If
                    Else
                        Dim v = MeshData.Meshgeometry.Vertices(i)
                        buf(0) = v.X : buf(1) = v.Y : buf(2) = v.Z
                        Marshal.Copy(buf, 0, baseP, 3)
                        Dim n = MeshData.Meshgeometry.Normals(i)
                        buf(0) = n.X : buf(1) = n.Y : buf(2) = n.Z
                        Marshal.Copy(buf, 0, baseN, 3)
                        Dim t = MeshData.Meshgeometry.Tangents(i)
                        buf(0) = t.X : buf(1) = t.Y : buf(2) = t.Z
                        Marshal.Copy(buf, 0, baseT, 3)
                        Dim b = MeshData.Meshgeometry.Bitangents(i)
                        buf(0) = b.X : buf(1) = b.Y : buf(2) = b.Z
                        Marshal.Copy(buf, 0, baseB, 3)
                    End If

                    MeshData.Meshgeometry.dirtyVertexFlags(i) = False
                Next

                ' Flush y desmapear en orden inverso
                GL.BindBuffer(BufferTarget.ArrayBuffer, vboPosition)
                GL.FlushMappedBufferRange(BufferTarget.ArrayBuffer, IntPtr.Zero, New IntPtr(totalBytes))
                GL.UnmapBuffer(BufferTarget.ArrayBuffer)

                GL.BindBuffer(BufferTarget.ArrayBuffer, vboBitangent)
                GL.FlushMappedBufferRange(BufferTarget.ArrayBuffer, IntPtr.Zero, New IntPtr(totalBytes))
                GL.UnmapBuffer(BufferTarget.ArrayBuffer)
                GL.BindBuffer(BufferTarget.ArrayBuffer, vboTangent)
                GL.FlushMappedBufferRange(BufferTarget.ArrayBuffer, IntPtr.Zero, New IntPtr(totalBytes))
                GL.UnmapBuffer(BufferTarget.ArrayBuffer)
                GL.BindBuffer(BufferTarget.ArrayBuffer, vboNormal)
                GL.FlushMappedBufferRange(BufferTarget.ArrayBuffer, IntPtr.Zero, New IntPtr(totalBytes))
                GL.UnmapBuffer(BufferTarget.ArrayBuffer)
                GL.BindBuffer(BufferTarget.ArrayBuffer, 0)

                MeshData.Meshgeometry.dirtyVertexIndices.Clear()
                ' Recompute AABB after sparse update — bounds are needed for frustum culling
                ' and blended-mesh depth sorting. Full update path already calls this above.
                Me.ComputeBounds()
            End If
            UpdateUpdateSkinBuffersMask_GL()
        End Sub
        Public Sub UpdateUpdateSkinBuffersMask_GL()
            If MeshData Is Nothing Then Exit Sub

            Dim geom = MeshData.Meshgeometry
            Dim dirtyMaskIndices = geom.dirtyMaskIndices
            Dim vertexMask = geom.VertexMask
            Dim dirtyMaskFlags = geom.dirtyMaskFlags

            If dirtyMaskIndices Is Nothing OrElse dirtyMaskIndices.Count = 0 Then Exit Sub
            If vertexMask Is Nothing OrElse dirtyMaskFlags Is Nothing Then
                dirtyMaskIndices.Clear()
                Exit Sub
            End If
            If vboMask = 0 Then
                dirtyMaskIndices.Clear()
                Exit Sub
            End If

            Const maskSize As Integer = 4 ' bytes por máscara
            Dim totalMaskBytes As Integer = vertexMask.Length * maskSize
            If totalMaskBytes <= 0 Then
                dirtyMaskIndices.Clear()
                Exit Sub
            End If

            ' Usar misma lógica de MapBufferRange y MapUnsynchronizedBit
            Dim mapMask As MapBufferAccessMask = MapBufferAccessMask.MapWriteBit Or MapBufferAccessMask.MapFlushExplicitBit Or MapBufferAccessMask.MapUnsynchronizedBit

            ' Mapear buffer de máscara
            GL.BindBuffer(BufferTarget.ArrayBuffer, vboMask)
            Dim ptrM As IntPtr = GL.MapBufferRange(BufferTarget.ArrayBuffer, IntPtr.Zero, totalMaskBytes, mapMask)
            If ptrM = IntPtr.Zero Then
                GL.BindBuffer(BufferTarget.ArrayBuffer, 0)
                dirtyMaskIndices.Clear()
                Exit Sub
            End If

            ' Un solo bucle para escribir máscaras sucias
            For Each i As Integer In dirtyMaskIndices
                If i < 0 OrElse i >= vertexMask.Length OrElse i >= dirtyMaskFlags.Length Then Continue For

                Dim offsetBytes As Int64 = CLng(i) * maskSize
                Dim baseM As IntPtr = ptrM + offsetBytes
                Dim mBytes() As Byte = BitConverter.GetBytes(vertexMask(i))
                Marshal.Copy(mBytes, 0, baseM, maskSize)
                dirtyMaskFlags(i) = False
            Next

            ' Flush y desmapear buffer de máscara
            GL.BindBuffer(BufferTarget.ArrayBuffer, vboMask)
            GL.FlushMappedBufferRange(BufferTarget.ArrayBuffer, IntPtr.Zero, New IntPtr(totalMaskBytes))
            GL.UnmapBuffer(BufferTarget.ArrayBuffer)
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0)
            dirtyMaskIndices.Clear()
        End Sub
        ''' <summary>
        ''' GPU Skinning: Updates the SSBO with current bone matrices when pose changes.
        ''' Call this after recomputing GPUBoneMatrices for a new pose.
        ''' </summary>
        Public Sub UpdateBoneMatricesSSBO()
            If ssbo_BoneMatrices = 0 OrElse MeshData.Meshgeometry.GPUBoneMatrices Is Nothing Then Exit Sub
            Me.ParentModel.ParentControl.EnsureContextCurrent()
            Dim sizeBytes = MeshData.Meshgeometry.GPUBoneMatrices.Length * 64
            ' Diagnostic: GL_INVALID_VALUE fires here when sizeBytes > the buffer's allocated size
            ' (the original glBufferData capacity). Logged with shape name so the caller mutating
            ' GPUBoneMatrices to a larger array can be traced. Cause is upstream — fix is in the
            ' code path that grew the array, NOT here (silently reallocating would mask the bug).
            If sizeBytes > ssbo_BoneMatricesCapacityBytes Then
                Try
                    Dim shapeName As String = "<unknown>"
                    If MeshData IsNot Nothing AndAlso MeshData.Meshgeometry.Geometry IsNot Nothing AndAlso MeshData.Meshgeometry.Geometry.BackingShape IsNot Nothing Then
                        Dim nm = MeshData.Meshgeometry.Geometry.BackingShape.Name
                        If nm IsNot Nothing AndAlso nm.String IsNot Nothing Then shapeName = nm.String
                    End If
                    Logger.LogLazy(Function() $"[GL-SSBO-DIAG] UpdateBoneMatricesSSBO size mismatch: shape='{shapeName}' newSize={sizeBytes} capacity={ssbo_BoneMatricesCapacityBytes} newCount={MeshData.Meshgeometry.GPUBoneMatrices.Length} capCount={ssbo_BoneMatricesCapacityBytes \ 64}")
                Catch
                End Try
                ' Skip the BufferSubData call — it would fire GL_INVALID_VALUE. Returning silently
                ' means this frame renders with stale bone matrices, but that's preferable to a
                ' driver-level error log spam. Caller should reallocate the SSBO via re-creation.
                GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0)
                Exit Sub
            End If
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, ssbo_BoneMatrices)
            GL.BufferSubData(BufferTarget.ShaderStorageBuffer, IntPtr.Zero, sizeBytes, MeshData.Meshgeometry.GPUBoneMatrices)
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0)
        End Sub

        ' Clean CPU-side zap: when ApplyZaps is on, exclude every triangle that has ANY vertex with
        ' VertexMask = -1 (the same rule the NIF export uses). This replaces the ragged 'flat ZappedVert'
        ' shader discard, which dropped triangles by provoking-vertex only and left boundary slivers.
        ' Vertices are NOT compacted (render keeps the full VBO); only the index buffer is filtered, so
        ' zapped verts simply stop being referenced. Dirty-gated: rebuilds only when ApplyMorphPlan
        ' re-touched the zap mask (geom.ZapTopologyDirty) or the ApplyZaps toggle flipped — no per-frame
        ' signature scan. ApplyMorphPlan is the single writer of VertexMask=-1, so the flag can't go stale.
        ' VertexMask is a Single() array whose zapped sentinel is -1.0F (= -1 here), matching the
        ' existing '= -1' comparisons used elsewhere in this file and the export's RemoveZaps.
        '
        ' NOTE: SkinnedGeometry is a Structure (value type); 'geom' below is a COPY of the field, so the
        ' ZapTopologyDirty clear must be written back to MeshData.Meshgeometry.ZapTopologyDirty (the field),
        ' not to the local copy. Reads through 'geom' are fine — Indices/VertexMask are reference arrays.
        Private Sub EnsureZapIndexBuffer()
            Dim geom = MeshData.Meshgeometry
            Dim full = geom.Indices
            If full Is Nothing OrElse full.Length = 0 Then Return

            Dim applyZaps As Boolean = MeshData.Shape IsNot Nothing AndAlso MeshData.Shape.ApplyZaps
            ' Per-segment worn-slot occlusion (Fase 2): the actor's worn biped-slot mask. 0 = no occlusion
            ' (the default — Wardrobe_Manager never sets it, so its render is unaffected).
            Dim coveredMask As UInteger = If(MeshData.Shape IsNot Nothing, MeshData.Shape.CoveredSlotsMask, 0UI)
            ' drawHidden = WM inspection toggle: when True we bypass per-segment occlusion (occl stays
            ' Nothing -> nothing hidden -> all drawn). Default False keeps NPC occlusion active.
            Dim drawHidden As Boolean = Config_App.Current.Setting_DrawHiddenSegments
            ' Dirty-gated: only rebuild when ApplyMorphPlan re-touched the zap mask (ZapTopologyDirty), the
            ' ApplyZaps toggle flipped, the worn-slot mask changed, or drawHidden flipped. Otherwise a few
            ' cheap checks and out — no per-frame scan. ApplyMorphPlan is the single writer of VertexMask=-1.
            If Not geom.ZapTopologyDirty AndAlso applyZaps = _lastApplyZaps AndAlso coveredMask = _lastCoveredSlotsMask AndAlso drawHidden = _lastDrawHidden Then Return

            ' Recompute the per-segment hidden-triangle set only when the dirty gate above tripped (mask
            ' changed, etc.). occl is indexed by the SHAPE's triangle index — the SAME order as geom.Indices
            ' (ExtractSkinnedGeometry flattens GetTriangles() in order; ComputeHiddenTriangles indexes
            ' GetSegmentation.TriParts in subIndex.Triangles order — verified aligned). Nothing when no mask.
            ' Computed whenever the shape is a BSSubIndexTriShape, REGARDLESS of coveredMask: an N+100
            ' occupied-variant segment is HIDDEN at mask 0 (the "no item" default), so mask 0 is NOT a
            ' no-op for segmented shapes. (The dirty gate above + the sentinel-initialized field ensure
            ' the first pass still runs even at mask 0.)
            Dim occl As Boolean() = Nothing
            ' Only compute the per-segment hidden set when occlusion is active. When drawHidden (WM
            ' inspection toggle) is True, occl stays Nothing so no per-segment triangle is hidden ->
            ' all geometry draws. The vertex-zap (applyZaps/VertexMask) path is untouched below.
            If Not drawHidden Then
                Dim subIdx = TryCast(If(MeshData.Shape Is Nothing, Nothing, MeshData.Shape.NifShape), NiflySharp.Blocks.BSSubIndexTriShape)
                If subIdx IsNot Nothing Then
                    ' FO4: per-segment occlusion via BSSubIndexTriShape/BSGeometrySegmentData.
                    occl = BSTriShapeGeometry.ComputeHiddenTriangles(subIdx, coveredMask, MeshData.Shape.OwnSlotsMask)
                ElseIf coveredMask <> 0UI AndAlso Config_App.Current.Game = Config_App.Game_Enum.Skyrim AndAlso
                       MeshData.Shape IsNot Nothing AndAlso MeshData.Shape.NifContent IsNot Nothing AndAlso MeshData.Shape.NifShape IsNot Nothing Then
                    ' SSE: per-partition occlusion via BSDismemberSkinInstance partitions (engine
                    ' ApplyOcclusionToGeometry 0x1403C56B0). Keyed on the mesh's REAL partition SBP slot,
                    ' NOT the ARMA BOD2 (which declares incidental extra slots — e.g. NakedTorso BOD2
                    ' includes calves(38), so boots would whole-hide the body under a BOD2 check; the
                    ' body mesh's partition is SBP 32, so per-partition hides it only when slot 32 is
                    ' covered). The app sets CoveredSlotsMask only on SKIN shapes (see NpcRenderHost),
                    ' so this never runs on outfit shapes. For vanilla single-partition skin meshes every
                    ' triangle shares one SBP → whole-mesh result, insensitive to triangle order.
                    occl = MeshData.Shape.NifContent.ComputeHiddenTrianglesDismember(MeshData.Shape.NifShape, coveredMask)
                End If
            End If
            _occlHidden = occl

            Dim shouldFilter As Boolean = applyZaps
            If shouldFilter Then
                Dim vm = geom.VertexMask
                Dim anyZap As Boolean = False
                If vm IsNot Nothing Then
                    For i = 0 To vm.Length - 1
                        If vm(i) = -1 Then anyZap = True : Exit For
                    Next
                End If
                If Not anyZap Then shouldFilter = False
            End If
            ' Also filter when any triangle is hidden per-segment (independent of the vertex-zap path).
            If Not shouldFilter AndAlso occl IsNot Nothing Then
                For i = 0 To occl.Length - 1
                    If occl(i) Then shouldFilter = True : Exit For
                Next
            End If

            If Not shouldFilter Then
                If _zapFilteredActive Then
                    GL.BindBuffer(BufferTarget.ElementArrayBuffer, ebo)
                    GL.BufferData(BufferTarget.ElementArrayBuffer, full.Length * 4, full, BufferUsageHint.StaticDraw)
                    indexCount = full.Length
                    _zapFilteredActive = False
                End If
                _lastApplyZaps = applyZaps
                _lastCoveredSlotsMask = coveredMask
                _lastDrawHidden = drawHidden
                MeshData.Meshgeometry.ZapTopologyDirty = False
                Return
            End If

            Dim vmask = geom.VertexMask
            Dim filtered As New List(Of UInteger)(full.Length)
            Dim t As Integer = 0
            Do While t + 2 < full.Length
                Dim a = full(t) : Dim b = full(t + 1) : Dim c = full(t + 2)
                Dim triHidden As Boolean = (occl IsNot Nothing AndAlso (t \ 3) < occl.Length AndAlso occl(t \ 3))
                ' vmask is non-Nothing whenever the vertex-zap path is active (anyZap requires vm IsNot Nothing);
                ' the per-segment-only path may run with no zaps, so the vertex test is null-safe here.
                Dim vertZapped As Boolean = (vmask IsNot Nothing AndAlso (vmask(CInt(a)) = -1 OrElse vmask(CInt(b)) = -1 OrElse vmask(CInt(c)) = -1))
                If Not triHidden AndAlso Not vertZapped Then
                    filtered.Add(a) : filtered.Add(b) : filtered.Add(c)
                End If
                t += 3
            Loop
            Dim arr = filtered.ToArray()
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, ebo)
            GL.BufferData(BufferTarget.ElementArrayBuffer, arr.Length * 4, arr, BufferUsageHint.DynamicDraw)
            indexCount = arr.Length
            _zapFilteredActive = True
            _lastApplyZaps = applyZaps
            _lastCoveredSlotsMask = coveredMask
            _lastDrawHidden = drawHidden
            MeshData.Meshgeometry.ZapTopologyDirty = False
        End Sub

        Public Sub SetupMesh_GL()
            vao = GL.GenVertexArray()
            ebo = GL.GenBuffer()
            vboPosition = GL.GenBuffer()
            vboNormal = GL.GenBuffer()
            vboTangent = GL.GenBuffer()
            vboBitangent = GL.GenBuffer()
            vboColorAlpha = GL.GenBuffer()
            vboUVMaskWeight = GL.GenBuffer()
            vboMask = GL.GenBuffer()

            Dim count = MeshData.Meshgeometry.Vertices.Length

            GL.BindVertexArray(vao)

            Dim posF() As Vector3 = Array.ConvertAll(MeshData.Meshgeometry.Vertices, Function(v) New Vector3(v.X, v.Y, v.Z))
            Dim nrmF() As Vector3 = Array.ConvertAll(MeshData.Meshgeometry.Normals, Function(v) New Vector3(v.X, v.Y, v.Z))
            Dim tanF() As Vector3 = Array.ConvertAll(MeshData.Meshgeometry.Tangents, Function(v) New Vector3(v.X, v.Y, v.Z))
            Dim bitanF() As Vector3 = Array.ConvertAll(MeshData.Meshgeometry.Bitangents, Function(v) New Vector3(v.X, v.Y, v.Z))

            ' POSICIONES — DynamicDraw
            GL.BindBuffer(BufferTarget.ArrayBuffer, vboPosition)
            GL.BufferData(BufferTarget.ArrayBuffer, posF.Length * 3 * 4, posF, BufferUsageHint.DynamicDraw)
            GL.EnableVertexAttribArray(0)
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, False, 0, 0)

            ' NORMALES — DynamicDraw
            GL.BindBuffer(BufferTarget.ArrayBuffer, vboNormal)
            GL.BufferData(BufferTarget.ArrayBuffer, nrmF.Length * 3 * 4, nrmF, BufferUsageHint.DynamicDraw)
            GL.EnableVertexAttribArray(1)
            GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, False, 0, 0)

            ' TANGENTES — DynamicDraw
            GL.BindBuffer(BufferTarget.ArrayBuffer, vboTangent)
            GL.BufferData(BufferTarget.ArrayBuffer, tanF.Length * 3 * 4, tanF, BufferUsageHint.DynamicDraw)
            GL.EnableVertexAttribArray(2)
            GL.VertexAttribPointer(2, 3, VertexAttribPointerType.Float, False, 0, 0)

            ' BITANGENTES — DynamicDraw
            GL.BindBuffer(BufferTarget.ArrayBuffer, vboBitangent)
            GL.BufferData(BufferTarget.ArrayBuffer, bitanF.Length * 3 * 4, bitanF, BufferUsageHint.DynamicDraw)
            GL.EnableVertexAttribArray(3)
            GL.VertexAttribPointer(3, 3, VertexAttribPointerType.Float, False, 0, 0)

            ' COLOR + ALPHA — StaticDraw

            GL.BindBuffer(BufferTarget.ArrayBuffer, vboColorAlpha)
            GL.BufferData(BufferTarget.ArrayBuffer, MeshData.Meshgeometry.VertexColors.Length * 4 * 4, MeshData.Meshgeometry.VertexColors, BufferUsageHint.StaticDraw)
            GL.EnableVertexAttribArray(4)
            GL.VertexAttribPointer(4, 3, VertexAttribPointerType.Float, False, 4 * 4, 0)
            GL.EnableVertexAttribArray(5)
            GL.VertexAttribPointer(5, 1, VertexAttribPointerType.Float, False, 4 * 4, 3 * 4)

            ' UV + WEIGHT — StaticDraw
            GL.BindBuffer(BufferTarget.ArrayBuffer, vboUVMaskWeight)
            GL.BufferData(BufferTarget.ArrayBuffer, MeshData.Meshgeometry.Uvs_Weight.Length * 3 * 4, MeshData.Meshgeometry.Uvs_Weight, BufferUsageHint.StaticDraw)
            GL.EnableVertexAttribArray(6)
            GL.VertexAttribPointer(6, 2, VertexAttribPointerType.Float, False, 3 * 4, 0)
            GL.EnableVertexAttribArray(8)
            GL.VertexAttribPointer(8, 1, VertexAttribPointerType.Float, False, 3 * 4, 2 * 4)

            ' MÁSCARA — DynamicDraw
            GL.BindBuffer(BufferTarget.ArrayBuffer, vboMask)
            GL.BufferData(BufferTarget.ArrayBuffer, MeshData.Meshgeometry.VertexMask.Length * 4, MeshData.Meshgeometry.VertexMask, BufferUsageHint.DynamicDraw)

            GL.EnableVertexAttribArray(7)
            GL.VertexAttribPointer(7, 1, VertexAttribPointerType.Float, False, 4, 0)

            ' GPU Skinning: bone indices VBO (4 bytes per vertex, as unsigned bytes)
            If MeshData.Meshgeometry.GPUBoneIndices IsNot Nothing AndAlso MeshData.Meshgeometry.GPUBoneIndices.Length > 0 Then
                vboBoneIndices = GL.GenBuffer()
                GL.BindBuffer(BufferTarget.ArrayBuffer, vboBoneIndices)
                GL.BufferData(BufferTarget.ArrayBuffer, MeshData.Meshgeometry.GPUBoneIndices.Length, MeshData.Meshgeometry.GPUBoneIndices, BufferUsageHint.StaticDraw)
                GL.EnableVertexAttribArray(9)
                GL.VertexAttribPointer(9, 4, VertexAttribPointerType.UnsignedByte, False, 0, 0)
                ' Note: UnsignedByte without normalization, shader receives as float 0-255, cast to int
            End If

            ' GPU Skinning: bone weights VBO (4 floats per vertex)
            If MeshData.Meshgeometry.GPUBoneWeights IsNot Nothing AndAlso MeshData.Meshgeometry.GPUBoneWeights.Length > 0 Then
                vboBoneWeights = GL.GenBuffer()
                GL.BindBuffer(BufferTarget.ArrayBuffer, vboBoneWeights)
                GL.BufferData(BufferTarget.ArrayBuffer, MeshData.Meshgeometry.GPUBoneWeights.Length * 4, MeshData.Meshgeometry.GPUBoneWeights, BufferUsageHint.StaticDraw)
                GL.EnableVertexAttribArray(10)
                GL.VertexAttribPointer(10, 4, VertexAttribPointerType.Float, False, 0, 0)
            End If

            ' GPU Skinning: SSBO for bone matrices
            If MeshData.Meshgeometry.GPUBoneMatrices IsNot Nothing AndAlso MeshData.Meshgeometry.GPUBoneMatrices.Length > 0 Then
                ssbo_BoneMatrices = GL.GenBuffer()
                ssbo_BoneMatricesCapacityBytes = MeshData.Meshgeometry.GPUBoneMatrices.Length * 64
                GL.BindBuffer(BufferTarget.ShaderStorageBuffer, ssbo_BoneMatrices)
                GL.BufferData(BufferTarget.ShaderStorageBuffer, ssbo_BoneMatricesCapacityBytes, MeshData.Meshgeometry.GPUBoneMatrices, BufferUsageHint.DynamicDraw)
                GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0)
            End If

            ' EBO
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, ebo)
            GL.BufferData(BufferTarget.ElementArrayBuffer, MeshData.Meshgeometry.Indices.Length * 4, MeshData.Meshgeometry.Indices, BufferUsageHint.StaticDraw)
            GL.BindVertexArray(0)
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0)
            indexCount = MeshData.Meshgeometry.Indices.Length

            ' O3.3: Compute initial AABB for frustum culling
            ComputeBounds()
        End Sub

        ''' <summary>
        ''' O3.3: Compute axis-aligned bounding box from world-space vertex positions for frustum culling.
        ''' Uses the world-space cache (GPU skinning: Vertices are local-space, so we need world-space for correct bounds).
        ''' </summary>
        Public Sub ComputeBounds()
            BoundsMin = New Vector3(Single.MaxValue)
            BoundsMax = New Vector3(Single.MinValue)
            Dim wv = SkinningHelper.GetWorldVertices(MeshData.Meshgeometry)
            For Each v In wv
                Dim vf = New Vector3(CSng(v.X), CSng(v.Y), CSng(v.Z))
                BoundsMin = Vector3.ComponentMin(BoundsMin, vf)
                BoundsMax = Vector3.ComponentMax(BoundsMax, vf)
            Next
            ' Keep SkinnedGeometry world-space bounds in sync with RenderableMesh bounds.
            ' Meshgeometry.Minv/Maxv are used by GetSceneBounds (camera centering).
            ' Meshgeometry.Boundingcenter is used for blended-mesh depth sorting in RenderAll.
            ' Without this, those values stay frozen at ExtractSkinnedGeometry time and become
            ' stale after any morph, pose, or shape update that changes world-space geometry.
            If wv.Length > 0 Then
                Dim bmin3 As New Vector3d(BoundsMin.X, BoundsMin.Y, BoundsMin.Z)
                Dim bmax3 As New Vector3d(BoundsMax.X, BoundsMax.Y, BoundsMax.Z)
                MeshData.Meshgeometry.Minv = bmin3
                MeshData.Meshgeometry.Maxv = bmax3
                MeshData.Meshgeometry.Boundingcenter = (bmin3 + bmax3) * 0.5
            End If
        End Sub

        ''' <summary>
        ''' O3.3: Test AABB against view-projection frustum using Gribb-Hartmann plane extraction.
        ''' Returns True if the AABB is at least partially inside the frustum.
        ''' </summary>
        Public Shared Function IsAABBInFrustum(bmin As Vector3, bmax As Vector3, vp As Matrix4) As Boolean
            ' Extract 6 frustum planes from the view-projection matrix (Gribb-Hartmann method)
            ' vp is row-major in OpenTK: Row0..Row3
            ' Plane normals point inward; a point is inside when dot+w >= 0 for all planes
            Dim planes(5) As Vector4
            ' Left
            planes(0) = New Vector4(vp.M14 + vp.M11, vp.M24 + vp.M21, vp.M34 + vp.M31, vp.M44 + vp.M41)
            ' Right
            planes(1) = New Vector4(vp.M14 - vp.M11, vp.M24 - vp.M21, vp.M34 - vp.M31, vp.M44 - vp.M41)
            ' Bottom
            planes(2) = New Vector4(vp.M14 + vp.M12, vp.M24 + vp.M22, vp.M34 + vp.M32, vp.M44 + vp.M42)
            ' Top
            planes(3) = New Vector4(vp.M14 - vp.M12, vp.M24 - vp.M22, vp.M34 - vp.M32, vp.M44 - vp.M42)
            ' Near
            planes(4) = New Vector4(vp.M14 + vp.M13, vp.M24 + vp.M23, vp.M34 + vp.M33, vp.M44 + vp.M43)
            ' Far
            planes(5) = New Vector4(vp.M14 - vp.M13, vp.M24 - vp.M23, vp.M34 - vp.M33, vp.M44 - vp.M43)

            For Each plane In planes
                ' Pick the vertex most in the direction of the plane normal (p-vertex)
                Dim px As Single = If(plane.X >= 0, bmax.X, bmin.X)
                Dim py As Single = If(plane.Y >= 0, bmax.Y, bmin.Y)
                Dim pz As Single = If(plane.Z >= 0, bmax.Z, bmin.Z)

                ' If the p-vertex is outside this plane, the entire AABB is outside
                If plane.X * px + plane.Y * py + plane.Z * pz + plane.W < 0 Then
                    Return False
                End If
            Next

            Return True
        End Function

        Private Structure PolygonOffsetState
            Public ReadOnly Enabled As Boolean
            Public ReadOnly Factor As Single
            Public ReadOnly Units As Single

            Public Sub New(enabled As Boolean, factor As Single, units As Single)
                Me.Enabled = enabled
                Me.Factor = factor
                Me.Units = units
            End Sub

            Public Shared ReadOnly Disabled As New PolygonOffsetState(False, 0.0F, 0.0F)
        End Structure

        ' Centralized tuning points for decal/depth-bias raster offset.
        Private Const DecalPolygonOffsetFactor As Single = 0.0F
        Private Const DecalPolygonOffsetUnits As Single = 0.0F
        Private Const DecalDepthBiasPolygonOffsetFactor As Single = 0.0F
        Private Const DecalDepthBiasPolygonOffsetUnits As Single = 0.0F

        ' T11: engine decal depth-bias. Fallout4.exe selects rasterizer preset 1 (FUN_141855cb0)
        ' for the Decal flag (SF1 bit 26) via the global ToggleDepthBias (console 'tdb', default on),
        ' NOT the material DepthBias field (that field is N/A in FO4 v2). Preset 1 = D3D11
        ' DepthBias=-3, SlopeScaledDepthBias=-0.4 under reversed-Z.
        '
        ' SIGN: derived from THIS app's convention (standard-Z, DepthFunc Lequal, near=0, decals drawn
        '   after the opaque base) -- pulling the decal toward the viewer (smaller depth) to win Lequal
        '   = NEGATIVE GL offset. (Correct independently of the engine's reversed-Z sign.)
        ' factor <- D3D SlopeScaledDepthBias = -0.4: a TRUE 1:1 map (both scale max-slope, which is
        '   format-independent).
        ' units  <- D3D DepthBias = -3: NOT a faithful cross-API translation -- the engine measured -3
        '   on a reversed-Z D32_FLOAT buffer (per-primitive r), this app uses a standard-Z D24_UNORM
        '   buffer (constant r ~ 2^-24). -3.0 is kept only as a sane in-range GL starting value
        '   (typical GL decals use -1..-8); TUNE if z-fighting/peter-panning appears.
        ' DepthBiasClamp=-100 has no GL 4.3-core equivalent (no glPolygonOffsetClamp) -> dropped.
        Private Const DecalEnginePolygonOffsetFactor As Single = -0.4F
        Private Const DecalEnginePolygonOffsetUnits As Single = -3.0F

        Private Shared Function ResolvePolygonOffset(materialBase As FO4UnifiedMaterial_Class) As PolygonOffsetState
            If materialBase Is Nothing Then Return PolygonOffsetState.Disabled

            If Not materialBase.Decal Then
                Return PolygonOffsetState.Disabled
            End If

            ' FO4: bias the Decal pass with the engine preset (preset 1). Skyrim keeps its own decal handling.
            If Config_App.Current IsNot Nothing AndAlso Config_App.Current.Game = Config_App.Game_Enum.Fallout4 Then
                Return New PolygonOffsetState(True, DecalEnginePolygonOffsetFactor, DecalEnginePolygonOffsetUnits)
            End If

            If materialBase.DepthBias Then
                Return New PolygonOffsetState(True, DecalDepthBiasPolygonOffsetFactor, DecalDepthBiasPolygonOffsetUnits)
            End If

            Return New PolygonOffsetState(True, DecalPolygonOffsetFactor, DecalPolygonOffsetUnits)
        End Function

        Private Shared Function ResolveDepthTestEnabled(materialBase As FO4UnifiedMaterial_Class, hasAlphaBlend As Boolean) As Boolean
            If materialBase Is Nothing Then Return hasAlphaBlend = False
            If materialBase.Decal Then Return True

            Return materialBase.ZBufferTest OrElse (hasAlphaBlend = False)
        End Function

        Private Shared Function ResolveDepthWriteEnabled(materialBase As FO4UnifiedMaterial_Class, hasAlphaBlend As Boolean, hasAlphaTest As Boolean, isWireframe As Boolean) As Boolean
            If hasAlphaBlend OrElse isWireframe Then
                Return False
            End If

            If hasAlphaTest Then
                Return True
            End If

            If materialBase Is Nothing Then Return True
            Return materialBase.ZBufferWrite
        End Function
        Public Sub Render(projection As Matrix4, ByRef camera As OrbitCamera)

            If IsNothing(MeshData.Shape) OrElse MeshData.Shape.RenderHide = True Then Exit Sub
            If IsNothing(Me.MeshData.Shape.NifShape) Then Exit Sub
            '=============================== MATRICES ===============================
            Dim model As Matrix4 = MeshData.Transform
            Dim view As Matrix4 = camera.GetViewMatrix()
            Dim modelView As Matrix4 = view * model

            Dim normalMatrix As New OpenTK.Mathematics.Matrix3(modelView)
            normalMatrix.Invert()
            normalMatrix.Transpose()

            Dim modelViewInverse As Matrix4 = modelView.Inverted()


            '=============================== SHADER ===============================
            Dim shader = Me.ParentModel.ParentControl.CurrentShader
            shader.Use()
            shader.SetMatrix4("matProjection", projection)
            shader.SetMatrix4("matView", view)
            shader.SetMatrix4("matModel", model)
            shader.SetMatrix4("matModelView", modelView)
            shader.SetMatrix4("matModelViewInverse", modelViewInverse)
            shader.SetMatrix3("mv_normalMatrix", normalMatrix)
            ' bModelSpace needed in vertex shader for MSN CPU skinning path
            Dim materialBase = MeshData.Material.MaterialBase
            shader.SetBool("bModelSpace", materialBase IsNot Nothing AndAlso materialBase.ModelSpaceNormals)
            ApplyMaterial(MeshData.Material)

            ' GPU Skinning: bind SSBO and set uniforms
            shader.SetBool("bGPUSkinning", ssbo_BoneMatrices > 0 AndAlso Config_App.Current.Setting_GPUSkinning)
            Dim boneCount As Integer = If(MeshData.Meshgeometry.GPUBoneMatrices IsNot Nothing, MeshData.Meshgeometry.GPUBoneMatrices.Length, 0)
            shader.SetInt("uBoneCount", boneCount)
            If ssbo_BoneMatrices > 0 Then
                GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 0, ssbo_BoneMatrices)
            End If

            '=============================== DRAW ===============================
            GL.BindVertexArray(vao)
            ' Clean CPU-side zap: filter the element buffer to drop zapped triangles before drawing,
            ' so indexCount is correct for the DrawElements calls below. Cheap (re-uploads only when
            ' the zapped vertex set changes); no-op when ApplyZaps is off or nothing is zapped.
            EnsureZapIndexBuffer()
            Dim mat = MeshData.Material.MaterialBase
            Dim faceMode = ResolveEffectiveFaceMode(MeshData.Shape, mat)
            Dim writeDepth As Boolean = ResolveDepthWriteEnabled(mat, MeshData.Material.HasAlphaBlend, MeshData.Material.HasAlphaTest, MeshData.Shape.Wireframe)

            Dim isTwoPassBlended As Boolean = False
            If MeshData.Material.HasAlphaBlend AndAlso Not MeshData.Shape.Wireframe AndAlso faceMode = EffectiveFaceMode.DrawBoth Then
                isTwoPassBlended = True
            End If

            If isTwoPassBlended Then
                GL.Enable(EnableCap.CullFace)

                GL.CullFace(TriangleFace.Front)
                GL.DepthMask(False)
                GL.DrawElements(PrimitiveType.Triangles, indexCount, DrawElementsType.UnsignedInt, 0)
                GL.CullFace(TriangleFace.Back)
                GL.DepthMask(writeDepth)
                GL.DrawElements(PrimitiveType.Triangles, indexCount, DrawElementsType.UnsignedInt, 0)
            Else
                ApplyFaceMode(faceMode)
                GL.DrawElements(PrimitiveType.Triangles, indexCount, DrawElementsType.UnsignedInt, 0)
            End If

            ' GPU Skinning: unbind SSBO after draw — prevents contamination of binding 0
            ' for subsequent meshes that may not have their own SSBO (ssbo_BoneMatrices=0 path
            ' skips BindBufferBase, so a stale binding from this draw would leak into them).
            If ssbo_BoneMatrices > 0 Then
                GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 0, 0)
            End If

            ' (Opcional) restaurar estado si luego renderizas más cosas:
            GL.DepthMask(True)
            GL.Disable(EnableCap.Blend)
            GL.Disable(EnableCap.PolygonOffsetFill)
            GL.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Fill)
            GL.CullFace(TriangleFace.Back)

        End Sub

        ' Per-layer transient MaterialData cache for the overlay render path. Built once per layer
        ' (keyed on the layer instance) so RenderOverlayLayer does not allocate a MaterialData every
        ' frame. Each entry has OverrideRelatedMaterial = layer.Material so MaterialBase + all the
        ' *_ID/Has* props flow from the overlay material; ParentMeshData stays this mesh's MeshData so
        ' Shape-derived state (TintColor/ShowTexture/...) still resolves to the BASE shape, matching
        ' how ApplyMaterial reads MeshData.Shape directly (~2905-2925).
        Private _overlayMaterialCache As Dictionary(Of OverlayMaterialLayer, MaterialData)

        Private Function GetOverlayMaterialData(layer As OverlayMaterialLayer) As MaterialData
            If _overlayMaterialCache Is Nothing Then _overlayMaterialCache = New Dictionary(Of OverlayMaterialLayer, MaterialData)
            Dim md As MaterialData = Nothing
            If Not _overlayMaterialCache.TryGetValue(layer, md) Then
                md = New MaterialData(MeshData) With {.OverrideRelatedMaterial = layer.Material}
                _overlayMaterialCache(layer) = md
            End If
            Return md
        End Function

        ''' <summary>Texture paths of every OverlayLayer's material on <paramref name="meshData"/>'s shape,
        ''' reusing the standard 14-slot MaterialData.Textures_Path_List via a transient override MaterialData.
        ''' Returns empty when the shape has no overlay layers (Nothing/empty) — so the no-overlay path adds
        ''' nothing to the texture-load set. Used only at texture-gather time, not per frame.</summary>
        Friend Shared Function EnumerateOverlayTexturePaths(meshData As MeshData_Class) As IEnumerable(Of String)
            Dim layers = meshData.Shape?.OverlayLayers
            If layers Is Nothing OrElse layers.Count = 0 Then Return Array.Empty(Of String)()
            Dim paths As New List(Of String)
            For Each layer In layers
                If layer Is Nothing OrElse layer.Material Is Nothing Then Continue For
                Dim md As New MaterialData(meshData) With {.OverrideRelatedMaterial = layer.Material}
                paths.AddRange(md.Textures_Path_List)
            Next
            Return paths
        End Function

        ''' <summary>Color (sRGB) texture paths of every OverlayLayer's material, mirroring
        ''' MaterialData.ColorTextures_Path_List. Empty when there are no overlay layers.</summary>
        Friend Shared Function EnumerateOverlayColorTexturePaths(meshData As MeshData_Class) As IEnumerable(Of String)
            Dim layers = meshData.Shape?.OverlayLayers
            If layers Is Nothing OrElse layers.Count = 0 Then Return Array.Empty(Of String)()
            Dim paths As New List(Of String)
            For Each layer In layers
                If layer Is Nothing OrElse layer.Material Is Nothing Then Continue For
                Dim md As New MaterialData(meshData) With {.OverrideRelatedMaterial = layer.Material}
                paths.AddRange(md.ColorTextures_Path_List)
            Next
            Return paths
        End Function

        ''' <summary>
        ''' Draws ONE overlay material layer over this mesh's ALREADY-deformed (morphed + skinned)
        ''' geometry as a coplanar decal — the LooksMenu overlay/tattoo model. REUSES the existing
        ''' VAO / SSBO / EBO / indexCount (no re-skin, no re-morph): same vertices, same skinning,
        ''' only the bound material differs. Modeled on <see cref="Render"/> (~2695).
        '''
        ''' Coplanar-decal GL state: depth-test Lequal so the coplanar fragment passes against the
        ''' base's own depth, DepthMask(False) so the overlay NEVER writes depth, blend as configured
        ''' by ApplyMaterial for the (alpha-blended BGEM) material. Back-face culling uses the same
        ''' effective face mode as the base draw. Restored at the end exactly like Render; DepthFunc is
        ''' left at Lequal (the frame-wide default this code uses — see the restore block).
        ''' </summary>
        Public Sub RenderOverlayLayer(projection As Matrix4, ByRef camera As OrbitCamera, layer As OverlayMaterialLayer)
            If layer Is Nothing OrElse layer.Material Is Nothing Then Exit Sub
            If IsNothing(MeshData.Shape) OrElse MeshData.Shape.RenderHide = True Then Exit Sub
            If IsNothing(Me.MeshData.Shape.NifShape) Then Exit Sub

            '=============================== MATRICES (identical to Render) ===============================
            Dim model As Matrix4 = MeshData.Transform
            Dim view As Matrix4 = camera.GetViewMatrix()
            Dim modelView As Matrix4 = view * model

            Dim normalMatrix As New OpenTK.Mathematics.Matrix3(modelView)
            normalMatrix.Invert()
            normalMatrix.Transpose()

            Dim modelViewInverse As Matrix4 = modelView.Inverted()

            '=============================== SHADER ===============================
            Dim shader = Me.ParentModel.ParentControl.CurrentShader
            shader.Use()
            shader.SetMatrix4("matProjection", projection)
            shader.SetMatrix4("matView", view)
            shader.SetMatrix4("matModel", model)
            shader.SetMatrix4("matModelView", modelView)
            shader.SetMatrix4("matModelViewInverse", modelViewInverse)
            shader.SetMatrix3("mv_normalMatrix", normalMatrix)

            ' Bind the LAYER's material (transient MaterialData with OverrideRelatedMaterial).
            Dim overlayMat = GetOverlayMaterialData(layer)
            Dim materialBase = overlayMat.MaterialBase
            shader.SetBool("bModelSpace", materialBase IsNot Nothing AndAlso materialBase.ModelSpaceNormals)
            ApplyMaterial(overlayMat)

            ' GPU Skinning: bind the SAME SSBO / bone uniforms as the base draw (geometry is shared).
            shader.SetBool("bGPUSkinning", ssbo_BoneMatrices > 0 AndAlso Config_App.Current.Setting_GPUSkinning)
            Dim boneCount As Integer = If(MeshData.Meshgeometry.GPUBoneMatrices IsNot Nothing, MeshData.Meshgeometry.GPUBoneMatrices.Length, 0)
            shader.SetInt("uBoneCount", boneCount)
            If ssbo_BoneMatrices > 0 Then
                GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 0, ssbo_BoneMatrices)
            End If

            '=============================== DRAW ===============================
            GL.BindVertexArray(vao)
            EnsureZapIndexBuffer()
            Dim faceMode = ResolveEffectiveFaceMode(MeshData.Shape, materialBase)

            ' Coplanar decal: never write depth, depth-test Lequal so the coplanar overlay passes
            ' against the base mesh's depth. ApplyMaterial already enabled blend (HasAlphaBlend) and
            ' set DepthFunc(Lequal); reassert both here so the overlay never writes depth regardless
            ' of the material's ZBufferWrite.
            GL.DepthFunc(DepthFunction.Lequal)
            GL.DepthMask(False)
            ApplyFaceMode(faceMode)
            GL.DrawElements(PrimitiveType.Triangles, indexCount, DrawElementsType.UnsignedInt, 0)

            ' Unbind SSBO after draw — same hygiene as Render.
            If ssbo_BoneMatrices > 0 Then
                GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 0, 0)
            End If

            ' Restore GL state exactly like Render (~2787-2792). DepthFunc is intentionally left at
            ' Lequal: that is the prior value here (the one-time GL init sets Lequal @ line 911 and
            ' ApplyMaterial re-sets Lequal every draw @ line 3344) — Render itself never restores
            ' DepthFunc, so re-setting it would diverge from the inert base path. The Lequal we set
            ' above (for the coplanar decal) already equals the frame-wide default, so later passes/
            ' frames are unaffected. (The spec's "restore to Less" assumed a Less default this code
            ' does not have.)
            GL.DepthMask(True)
            GL.Disable(EnableCap.Blend)
            GL.Disable(EnableCap.PolygonOffsetFill)
            GL.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Fill)
            GL.CullFace(TriangleFace.Back)
        End Sub

        Private Enum EffectiveFaceMode
            DrawCCW = 1
            DrawCW = 2
            DrawBoth = 3
        End Enum

        Private Const StencilDrawMask As Integer = &HC00
        Private Const StencilDrawShift As Integer = 10

        Private Shared Function ResolveDefaultFaceMode(materialBase As FO4UnifiedMaterial_Class) As EffectiveFaceMode
            If materialBase IsNot Nothing AndAlso materialBase.TwoSided Then
                Return EffectiveFaceMode.DrawBoth
            End If

            Return EffectiveFaceMode.DrawCCW
        End Function

        Private Shared Function TryGetStencilDrawMode(shape As IRenderableShape, ByRef drawMode As Integer) As Boolean
            drawMode = 0

            If shape Is Nothing Then Return False
            If shape.NifShape Is Nothing Then Return False
            If shape.NifContent Is Nothing Then Return False
            If shape.NifShape Is Nothing Then Return False
            If shape.NifShape.Properties Is Nothing Then Return False
            Dim stencil = shape.NifContent.GetPropertyOfType(Of NiflySharp.Blocks.NiStencilProperty)(shape.NifShape)
            If stencil Is Nothing Then Return False

            Try
                Dim flagsProp = stencil.GetType().GetProperty("Flags")
                If flagsProp Is Nothing Then Return False

                Dim flagsObj = flagsProp.GetValue(stencil, Nothing)
                If flagsObj Is Nothing Then Return False

                Dim drawModeProp = flagsObj.GetType().GetProperty("DrawMode")
                If drawModeProp IsNot Nothing Then
                    Dim drawModeObj = drawModeProp.GetValue(flagsObj, Nothing)
                    If drawModeObj IsNot Nothing Then
                        drawMode = Convert.ToInt32(drawModeObj)
                        Return True
                    End If
                End If

                drawMode = (Convert.ToInt32(flagsObj) And StencilDrawMask) >> StencilDrawShift
                Return True
            Catch
                Return False
            End Try
        End Function

        Private Shared Function ResolveEffectiveFaceMode(shape As IRenderableShape, materialBase As FO4UnifiedMaterial_Class) As EffectiveFaceMode
            Dim fallback As EffectiveFaceMode = ResolveDefaultFaceMode(materialBase)

            Dim drawMode As Integer
            If Not TryGetStencilDrawMode(shape, drawMode) Then
                Return fallback
            End If

            Select Case drawMode
                Case 2 ' DRAW_CW
                    Return EffectiveFaceMode.DrawCW
                Case 3 ' DRAW_BOTH
                    Return EffectiveFaceMode.DrawBoth
                Case 1 ' DRAW_CCW
                    Return EffectiveFaceMode.DrawCCW
                Case Else ' DRAW_CCW_OR_BOTH
                    Return fallback
            End Select
        End Function

        Private Shared Sub ApplyFaceMode(faceMode As EffectiveFaceMode)
            Select Case faceMode
                Case EffectiveFaceMode.DrawBoth
                    GL.Disable(EnableCap.CullFace)

                Case EffectiveFaceMode.DrawCW
                    GL.Enable(EnableCap.CullFace)
                    GL.CullFace(TriangleFace.Front)

                Case Else
                    GL.Enable(EnableCap.CullFace)
                    GL.CullFace(TriangleFace.Back)
            End Select
        End Sub



        Public Sub ApplyMaterial(material As PreviewModel.RenderableMesh.MaterialData)

            Dim shader = Me.ParentModel.ParentControl.CurrentShader
            Dim materialBase = material.MaterialBase

            Dim diffuseTextureId = material.DiffuseTexture_ID
            Dim normalTextureId = material.NormalTexture_ID
            Dim envmapTextureId = material.EnvmapTexture_ID
            Dim envmapMaskTextureId = material.EnvmapMaskTexture_ID
            Dim smoothSpecTextureId = material.SmoothSpecTexture_ID
            Dim greyscaleTextureId = material.GreyscaleTexture_ID
            Dim glowTextureId = material.GlowTexture_ID
            Dim lightingTextureId = material.LightingTexture_ID
            Dim WrinklesTextureId = material.WrinklesTexture_ID

            ' FO4 = engine-faithful path (Fragment_FO4, always on); Skyrim = Fragment_SSE (its own path).
            ' The shader instance is the single source of truth for which game we are rendering.
            Dim isSSE As Boolean = TypeOf shader Is Shader_Class_SSE

            Dim hasBacklightTexture As Boolean = materialBase.BackLighting

            If materialBase.EyeEnvironmentMapping AndAlso smoothSpecTextureId <> 0 AndAlso envmapMaskTextureId = 0 Then
                envmapMaskTextureId = smoothSpecTextureId
                smoothSpecTextureId = 0
            End If
            ' T13: Wrinkles is a FaceGen WrinkleSampler, NOT a reflection mask. FO4 never routes it to
            ' env-mask; only the Skyrim path does this.
            If isSSE AndAlso materialBase.Facegen AndAlso WrinklesTextureId <> 0 AndAlso envmapMaskTextureId = 0 Then
                envmapMaskTextureId = WrinklesTextureId
                WrinklesTextureId = 0
            End If

            Dim hasSpecMap As Boolean = (smoothSpecTextureId <> 0)
            ' SSE: specular can come from normalMap.a even without a dedicated spec map
            Dim hasSpecularSource As Boolean = hasSpecMap OrElse (isSSE AndAlso normalTextureId <> 0)

            Dim hasCubemap = material.HasCubemap
            Dim hasAlphaBlend = material.HasAlphaBlend
            Dim hasAlphaTest = material.HasAlphaTest
            Dim shape = Me.MeshData.Shape
            Dim nifShader = shape.NifShader
            Dim shapeGeom = MeshData.Meshgeometry.Geometry

            '===============================
            ' ?? PROPIEDADES DE COLOR BÁSICO
            '===============================
            shader.SetVector3("color", Shader_Base_Class.Color_to_Vector(MeshData.Shape.Wirecolor))
            shader.SetFloat("WireAlpha", MeshData.Shape.WireAlpha)
            shader.SetVector3("subColor", Shader_Base_Class.Color_to_Vector(MeshData.Shape.TintColor))

            '===============================
            ' ?? TOGGLES DE VISUALIZACIÓN
            '===============================
            shader.SetBool("bShowTexture", shape.ShowTexture)
            shader.SetBool("bShowMask", shape.ShowMask)
            shader.SetBool("bShowWeight", shape.ShowWeight)
            ' Vertex color: gated by NIF data + user toggle.
            ' Vertex alpha: not gated here (kept as before — original behavior).
            Dim hasVertexColorData As Boolean = shapeGeom IsNot Nothing AndAlso shapeGeom.HasVertexColors
            Dim shaderUsesVertexAlpha As Boolean = nifShader IsNot Nothing AndAlso nifShader.HasVertexAlpha

            ' Tree_Anim interpretation of vertex alpha (anim param vs transparency).
            ' Triggered by either the BGSM.Tree flag OR the BSLightingShaderType.TreeAnim shader type;
            ' vanilla content often sets only one of them for vegetation/grass.
            ' Tree vertex-alpha semantics: TreeAnim uses vertex ALPHA as a wind/anim param, not
            ' transparency, so it must not feed the vertex-alpha display. (The vColor RGB gamma-decode
            ' that used to be Tree-only is now universal in the BGSM base path -- the engine decodes
            ' vColor for every BGSM, not just trees.)
            Dim isTreeAnim As Boolean = materialBase.Tree OrElse materialBase.NifShaderType = NiflySharp.Enums.BSLightingShaderType.TreeAnim
            shader.SetBool("bShowVertexColor", shape.ShowVertexColor AndAlso hasVertexColorData)
            shader.SetBool("bShowVertexAlpha", shape.ShowVertexColor AndAlso hasVertexColorData AndAlso Not isTreeAnim)
            shader.SetBool("bApplyZap", shape.ApplyZaps)
            shader.SetBool("bWireframe", shape.Wireframe)
            shader.SetBool("bHide", shape.RenderHide)

            '===============================
            ' ?? ILUMINACIÓN PRINCIPAL
            '===============================
            ' ?? ILUMINACIÓN PRINCIPAL

            ' main “frontal” light
            Dim cam = ParentModel.ParentControl.camera

            shader.SetBool("bLightEnabled", True)
            ' Light rig: authored in perceptual (sRGB) space, decoded to linear (pow 2.2) AT UPLOAD so
            ' the rig config stays untouched and one rig serves both games. Both FO4 and SSE run the
            ' engine's linear pipeline (verified: the LightingShader PS samples diffuse via sRGB SRV and
            ' lights/outputs in linear, with tonemap + sRGB-encode in a separate ImageSpace pass; the app
            ' folds that tonemap + encode into the fragment tail). Directions are geometric, never converted.
            ' Ambient HEMISFÉRICO (engine-faithful: FO4/SSE iluminan el ambient dependiente de la normal,
            ' no plano). Dos colores -- cielo (normal hacia world +Z) y suelo (-Z) -- que el shader mezcla
            ' por la componente up de la normal. Config legacy (sin hemisferio) -> derivar uno neutro del
            ' escalar Ambient (cielo=Ambient, suelo=Ambient/2). Se linealizan (pow 2.2) como el resto del rig.
            ' Ambient = 3 perillas independientes (NormalizeAmbient migra configs viejos): intensidad global
            ' (Ambient), hemisferio (AmbientGroundLevel = brillo del suelo respecto del cielo) y tinte
            ' (Sky/Ground, blanco = neutro). sky = skyTint*intensity ; ground = groundTint*intensity*groundLevel.
            ' Linealizado (pow 2.2) como el resto del rig.
            Dim arig = Config_App.Current.Setting_Lightrig
            Config_App.NormalizeAmbient(arig)
            Dim ambientVal As Single = arig.Ambient
            Dim aSky = arig.AmbientSky
            Dim aGround = arig.AmbientGround
            Dim gLevel As Single = arig.AmbientGroundLevel
            shader.SetVector3("ambientSky", Shader_Base_Class.Vector_to_Linear(New OpenTK.Mathematics.Vector3(aSky.X, aSky.Y, aSky.Z) * ambientVal))
            shader.SetVector3("ambientGround", Shader_Base_Class.Vector_to_Linear(New OpenTK.Mathematics.Vector3(aGround.X, aGround.Y, aGround.Z) * (ambientVal * gLevel)))

            shader.SetVector3("frontal.diffuse", Shader_Base_Class.Vector_to_Linear(Config_App.Current.Setting_Lightrig.DirectL.GetDifuse))
            shader.SetVector3("frontal.direction", Config_App.Current.Setting_Lightrig.DirectL.GetDirection(cam))
            ' Luz direccional 0
            shader.SetVector3("directional0.diffuse", Shader_Base_Class.Vector_to_Linear(Config_App.Current.Setting_Lightrig.FillLight_1.GetDifuse))
            shader.SetVector3("directional0.direction", Config_App.Current.Setting_Lightrig.FillLight_1.GetDirection(cam))

            ' Luz direccional 1
            shader.SetVector3("directional1.diffuse", Shader_Base_Class.Vector_to_Linear(Config_App.Current.Setting_Lightrig.FillLight_2.GetDifuse))
            shader.SetVector3("directional1.direction", Config_App.Current.Setting_Lightrig.FillLight_2.GetDirection(cam))

            ' Luz direccional 2
            shader.SetVector3("directional2.diffuse", Shader_Base_Class.Vector_to_Linear(Config_App.Current.Setting_Lightrig.BackLight.GetDifuse))
            shader.SetVector3("directional2.direction", Config_App.Current.Setting_Lightrig.BackLight.GetDirection(cam))

            '===============================
            ' ?? TEXTURAS (Sample BINDs)
            '===============================
            If diffuseTextureId <> 0 Then
                shader.BindTexture("texDiffuse", diffuseTextureId, TextureUnit.Texture0)
            Else
                shader.BindTexture("texDiffuse", Me.ParentModel.ParentControl.defaultWhiteTex, TextureUnit.Texture0)
            End If

            If normalTextureId <> 0 Then
                shader.BindTexture("texNormal", normalTextureId, TextureUnit.Texture1)
            Else
                shader.BindTexture("texNormal", Me.ParentModel.ParentControl.defaultNormalTex, TextureUnit.Texture1)
            End If

            If envmapTextureId <> 0 AndAlso hasCubemap Then
                shader.BindCubeMap("texCubemap", envmapTextureId, TextureUnit.Texture2)
            Else
                shader.BindCubeMap("texCubemap", Me.ParentModel.ParentControl.defaultCubeMap, TextureUnit.Texture2)
            End If

            If envmapMaskTextureId <> 0 Then
                shader.BindTexture("texEnvMask", envmapMaskTextureId, TextureUnit.Texture3)
            Else
                shader.BindTexture("texEnvMask", Me.ParentModel.ParentControl.defaultWhiteTex, TextureUnit.Texture3)
            End If

            If smoothSpecTextureId <> 0 Then
                shader.BindTexture("texSpecular", smoothSpecTextureId, TextureUnit.Texture4)
            Else
                shader.BindTexture("texSpecular", Me.ParentModel.ParentControl.defaultWhiteTex, TextureUnit.Texture4)
            End If

            If greyscaleTextureId <> 0 Then
                shader.BindTexture("texGreyscale", greyscaleTextureId, TextureUnit.Texture5)
            Else
                shader.BindTexture("texGreyscale", Me.ParentModel.ParentControl.defaultWhiteTex, TextureUnit.Texture5)
            End If

            If glowTextureId <> 0 Then
                shader.BindTexture("texGlowmap", glowTextureId, TextureUnit.Texture6)
            Else
                shader.BindTexture("texGlowmap", Me.ParentModel.ParentControl.defaultWhiteTex, TextureUnit.Texture6)
            End If


            ' texLightmask is SSE-only (rim/soft-light masking); FO4 does not use it. For FaceTint
            ' (technique 4) the LightingTexture (texture-set slot 6, the _sk map) is the SUBSURFACE
            ' map -- VERIFIED in SkyrimSE.exe BSLightingShader::SetupGeometry @0x1414DC310: the facegen
            ' branch binds texture-set slots {3,5,6} -> PS registers t3(detail)/t4(skintone)/t12(subsurface),
            ' and the facegen-skin PS modulates SSS by t12. So bind it for facegen too (it is the
            ' subsurface, NOT a tint-mask overlay). The same slot is reused at other registers per technique.
            If isSSE Then
                If lightingTextureId <> 0 Then
                    shader.BindTexture("texLightmask", lightingTextureId, TextureUnit.Texture7)
                ElseIf materialBase.Facegen Then
                    ' ENGINE-FAITHFUL: BSLightingShaderMaterialFacegen defaultea el subsurface faltante a NEGRO
                    ' (fill slot#10 0x1414BA8B0: +0xB0←DefHeightMap; miembro↔slot verificado en 0x1414BA6E0:
                    ' +0xB0↔índice 2 = _sk) ⇒ SSS=0. El fallback softMask=albedo del shader es para NO-facegen;
                    ' acá se bindea el negro y bLightmask=True (abajo) para que el shader lo samplee.
                    shader.BindTexture("texLightmask", Me.ParentModel.ParentControl.defaultFacegenSubsurfaceTex, TextureUnit.Texture7)
                Else
                    shader.BindTexture("texLightmask", Me.ParentModel.ParentControl.defaultWhiteTex, TextureUnit.Texture7)
                End If
            End If

            ' SSE FaceGen albedo tint: the FACETINT (texture-set slot 6 -> engine PS t4) MULTIPLIES the albedo
            ' (amplified) -- that is where the baked makeup/skin-tone shows (lips/eyes/tint). VERIFIED
            ' sse_facegen_skin.asm: r1 = t4_amp * softlight(diffuse, detail). Bind it to texGlowmap (faces have
            ' no glow); the _sk stays the subsurface on texLightmask above (engine t12). SSE + facegen gated,
            ' so FO4 and non-face SSE are untouched.
            If isSSE AndAlso materialBase.Facegen Then
                Dim facetintId As UInteger = material.InnerLayerTexture_ID
                shader.BindTexture("texGlowmap", If(facetintId <> 0, facetintId, Me.ParentModel.ParentControl.defaultWhiteTex), TextureUnit.Texture6)
            End If

            '===============================
            ' ?? PROPIEDADES DEL MATERIAL
            '===============================
            shader.SetVector2("uvOffset", New Vector2(materialBase.UOffset, materialBase.VOffset))
            shader.SetVector2("uvScale", New Vector2(materialBase.UScale, materialBase.VScale))
            ' Umbral de alpha (solo necesario si usás discard por transparencia)
            shader.SetFloat("alphaThreshold", materialBase.AlphaTestRef / 255)

            '===============================
            ' ?? TOGGLES DE EFECTOS Y SOMBREADO
            '===============================
            shader.SetBool("bCubemap", hasCubemap)
            shader.SetBool("bEnvMap", materialBase.EnvironmentMapping OrElse materialBase.EyeEnvironmentMapping)
            ' SSE Eye technique (16): the engine reflects the cubemap about the eyeball's radial (geometric)
            ' normal, not the bump normal (Fragment_SSE bEye branch; sse_eye.asm L108-118 / eye VS o7).
            If isSSE Then shader.SetBool("bEye", materialBase.EyeEnvironmentMapping)
            ' SSE Hair + ANISO_LIGHTING (SLSF2 Anisotropic_Lighting): 2-lobe shifted-normal Kajiya-Kay
            ' (Fragment_SSE; sse_hair_aniso.asm). FO4 hair is always KK via flow map; SSE only when the
            ' aniso flag is set (plain sse_hair vs sse_hair_aniso). Gated isSSE; the shader also needs bHairTint.
            If isSSE Then shader.SetBool("bAnisoLighting", materialBase.AnisoLighting)
            ' Alpha-blend (forward b6) vs opaque (deferred): gates the strong forward material-cube envmap.
            ' Opaque BGSM (pierce-type chrome gems) render deferred where the engine uses the scene IBL,
            ' not the material cube -- so the forward *3 over-grays them. (Eye keeps it via its inline path.)
            shader.SetBool("bHasAlphaBlend", hasAlphaBlend OrElse materialBase.EyeEnvironmentMapping)
            shader.SetBool("bAlphaTest", hasAlphaTest)
            shader.SetBool("bEnvMask", envmapMaskTextureId <> 0)
            shader.SetBool("bNormalMap", normalTextureId <> 0)
            shader.SetBool("bGreyscaleColor", materialBase.GrayscaleToPaletteColor AndAlso greyscaleTextureId <> 0)
            shader.SetBool("bSpecular", materialBase.SpecularEnabled AndAlso hasSpecularSource)
            If isSSE Then shader.SetBool("bHasSpecMap", hasSpecMap)
            shader.SetBool("bModelSpace", materialBase.ModelSpaceNormals)
            shader.SetBool("bEmissive", materialBase.EmitEnabled)
            ' FaceTint (technique 4) does subsurface unconditionally in the engine PS (no Soft_Lighting gate,
            ' verified sse_facegen_skin.asm); force it on for facegen since the head's flag may not be set.
            shader.SetBool("bSoftlight", materialBase.SubsurfaceLighting OrElse (isSSE AndAlso materialBase.Facegen))
            shader.SetBool("bGlowmap", materialBase.Glowmap AndAlso glowTextureId <> 0)
            ' Hair (FO4 carries Hair=true AND Glowmap=true): the glow slot holds the _f strand FLOW map,
            ' not a glow. bHair drives the Kajiya-Kay anisotropic specular + hair tint, robust vs the type.
            shader.SetBool("bHair", materialBase.Hair)
            shader.SetBool("bHasGlowTex", glowTextureId <> 0)
            ' bLightmask: the _sk subsurface map drives the SSS/rim mask, incl. facegen (engine t12, above).
            ' FACEGEN sin _sk: True igual — arriba quedó bindeado el default NEGRO del engine (SSS=0);
            ' con False el shader caería al fallback softMask=albedo (eso es solo para NO-facegen).
            If isSSE Then shader.SetBool("bLightmask", lightingTextureId <> 0 OrElse materialBase.Facegen)
            ' bFacetintAlbedo: facegen multiplies the albedo by the facetint (engine t4, slot 6 on texGlowmap).
            If isSSE Then shader.SetBool("bFacetintAlbedo", materialBase.Facegen AndAlso material.InnerLayerTexture_ID <> 0)
            shader.SetFloat("shininess", materialBase.Smoothness)
            ' SSE: exponente de glossiness CRUDO (shad.Glossiness), no reconstruido por el shader.
            If isSSE Then shader.SetFloat("glossiness", materialBase.NifGlossiness)
            shader.SetVector3("specularColor", Shader_Base_Class.Color_to_Vector_Linear(materialBase.SpecularColor))
            shader.SetFloat("specularStrength", materialBase.SpecularMult)
            shader.SetVector3("emissiveColor", Shader_Base_Class.Color_to_Vector_Linear(materialBase.EmittanceColor))
            shader.SetFloat("emissiveMultiple", materialBase.EmittanceMult)
            shader.SetFloat("fresnelPower", materialBase.FresnelPower)
            shader.SetFloat("subsurfaceRolloff", materialBase.SubsurfaceLightingRolloff)
            shader.SetFloat("paletteScale", materialBase.GrayscaleToPaletteScale)
            shader.SetFloat("envReflection", materialBase.EnvironmentMappingMaskScale)
            shader.SetBool("bBacklight", materialBase.BackLighting)
            shader.SetFloat("backlightPower", materialBase.BackLightPower)
            shader.SetBool("bRimlight", materialBase.RimLighting)
            shader.SetFloat("rimlightPower", materialBase.RimPower)
            shader.SetBool("bDoubleSided", materialBase.TwoSided)
            shader.SetBool("bDiffuseIsColor", materialBase.IsColorDiffuse())

            ' SkinTint / HairTint tint color.
            ' FO4 (engine): SkinTint = the per-actor SKIN TONE soft-lit at render (the FaceGen genetic-blend
            '   pass writes it to material+0xC0 for every SkinTint shape; SetupMaterial case 5 gamma-corrects
            '   pow 2.2 -> cb1[1]). Source = the per-mesh SkinToneColor (NPC) or the material SkinTintColor (WM).
            '   The body diffuse stays UNTONED (no bake). Hair = HairTintColor.
            ' Skyrim (SSE): SkinTint forces White (no-op); Hair = HairTintColor.
            Dim hasTint As Boolean = materialBase.SkinTint OrElse materialBase.Hair
            ' "Ya está": if the skin tone is already baked into this mesh's diffuse (FaceTint composite,
            ' or Skyrim legacy body bake), the shader's own SkinTint soft-light must be a no-op for it —
            ' otherwise the tone is applied twice. Hair tint is independent of skin-tone baking, never suppressed.
            If material.SkinToneBaked AndAlso Not materialBase.Hair Then hasTint = False
            shader.SetBool("bHasTintColor", hasTint)
            ' SSE Hair: engine applies HairTintColor to the LIT color masked by vertex-green
            ' (mix(1, tint, vColor.g)), not as a flat albedo multiply. Route via bHairTint.
            shader.SetBool("bHairTint", isSSE AndAlso materialBase.Hair)
            If hasTint Then
                Dim tint As Color
                Dim tintVec As Vector3
                If materialBase.SkinTint Then
                    ' SkinTint tone = per-actor SkinToneColor (NPC, set by the manager) or the material
                    ' SkinTintColor (WM / fallback). No White special-case: the engine never bakes the
                    ' tone into the texture; it is soft-lit at render from this color.
                    tint = materialBase.SkinTintColor
                    ' SSE: el motor copia el skin tone resuelto (QNAM = lerp(0.5,TINC/255,TINV)) al
                    ' tintColor del material CRUDO (×1/255, SIN gamma) — verificado en SkyrimSE.exe
                    ' 0x3B8D80 (resolver, mulss 1/255) + 0x4365E0 (copy verbatim al material type-5).
                    ' FO4: el engine gamma-corrige (pow 2.2) el skin tone en SetupMaterial → mantener Linear.
                    tintVec = If(isSSE, ScaledTintSrgb(tint, materialBase.TintColorScale),
                                        Shader_Base_Class.Vector_to_Linear(ScaledTintSrgb(tint, materialBase.TintColorScale)))
                Else
                    tint = materialBase.HairTintColor
                    tintVec = Shader_Base_Class.Vector_to_Linear(ScaledTintSrgb(tint, materialBase.TintColorScale))
                End If
                shader.SetVector3("tintColor", tintVec)
            End If

            ' SkinTint deferred W3C soft-light strength = the skin tone .w (engine material+0xCC). The app
            ' SkinTintAlpha carries it (default 1.0 = full). Consumed by Fragment_FO4 uEffectiveType==4.
            shader.SetFloat("skinTintStrength", materialBase.SkinTintAlpha)

            ' FaceGen detail map (SSE only): texture-set slot 3 (DisplacementTexture) -> engine t3, soft-lighted
            ' onto the diffuse BEFORE the facetint albedo tint. Full engine-faithful facegen albedo chain
            ' (sse_facegen_skin.asm): albedo = facetint(t4, slot 6) * softlight(diffuse(t0), detail(t3)); the
            ' _sk map (slot 2) is the SUBSURFACE colour -> engine t12 (texLightmask + SSS, above). CORRECTED:
            ' the earlier note had slot 6/2 swapped -- slot 6 is the FACETINT (albedo tint), NOT the _sk.
            If isSSE Then
                Dim detailMaskId = material.DetailMaskTexture_ID
                Dim isFaceTint As Boolean = materialBase.Facegen
                ' ENGINE-FAITHFUL (RE SkyrimSE.exe): una cabeza FaceGen SIEMPRE softlightea un detail sobre el
                ' diffuse. Si el texture-set slot 3 está VACÍO, el motor NO lo saltea: bindea su default interno
                ' BSShader_DefFacegenDetail (uniforme 0.251 = vanilla blankdetailmap) y softlightea ESO -> oscurece
                ' la cara al tono del cuerpo. Por eso acá se habilita el softlight para TODA cabeza facegen; si el
                ' slot resuelve vacío se bindea el default 0.251. Así el preview matchea lo que el NIF horneado
                ' (slot 3 vacío) rinde in-game (render == bake). Mods que borran el TX04 del TXST (Enhanced
                ' Khajiit) caen acá; antes el preview dejaba la cara sin oscurecer (más clara que el cuerpo).
                shader.SetBool("bHasDetailMask", isFaceTint)
                If isFaceTint Then
                    Dim detailTex = If(detailMaskId <> 0, detailMaskId, Me.ParentModel.ParentControl.defaultFacegenDetailTex)
                    shader.BindTexture("texDetailMask", detailTex, TextureUnit.Texture8)
                End If
            End If

            ' FaceTint overlay: NPC-specific composed tint texture (TETI/TEND layers composited via FBO).
            ' Lives on MaterialData (per-mesh) instead of on FO4UnifiedMaterial_Class (which is shared/cloned).
            Dim faceTintOverlayId = material.FaceTintOverlay_ID
            If faceTintOverlayId <> 0 Then
                shader.BindTexture("texFaceTintOverlay", faceTintOverlayId, TextureUnit.Texture10)
                shader.SetBool("bHasFaceTintOverlay", True)
            Else
                shader.BindTexture("texFaceTintOverlay", Me.ParentModel.ParentControl.defaultWhiteTex, TextureUnit.Texture10)
                shader.SetBool("bHasFaceTintOverlay", False)
            End If

            ' Effect Shader (BGEM) properties
            Dim isBGEM As Boolean = materialBase.IsBGEM
            shader.SetBool("bIsEffectShader", isBGEM)
            shader.SetBool("bDecal", materialBase.Decal)
            ' T2: 'shaderType' (NifShaderType enum) was dead code in the GLSL. Send the effective type
            ' (factory priority) instead, consumed by the engine-faithful per-type branch (linear path).
            shader.SetInt("uEffectiveType", CInt(materialBase.ResolveEffectiveType()))
            shader.SetBool("bEffectFalloff", materialBase.FalloffEnabled)
            shader.SetBool("bEffectFalloffColor", materialBase.FalloffColorEnabled)
            shader.SetBool("bEffectGreyscaleAlpha", materialBase.GrayscaleToPaletteAlpha)
            shader.SetFloat("effectLightingInfluence", If(materialBase.EffectLightingEnabled, materialBase.LightingInfluence, 0.0F))
            shader.SetVector4("effectFalloffParams", New OpenTK.Mathematics.Vector4(materialBase.FalloffStartAngle, materialBase.FalloffStopAngle, materialBase.FalloffStartOpacity, materialBase.FalloffStopOpacity))
            ' BGEM (BSEffectShader) is a separate shader family; the BSLighting linear pipeline does
            ' NOT touch its color path (would mix linear color with the un-decoded sRGB base texture).
            ' Keep effectBaseColor in the legacy space; the BGEM block + C3 are gated !bIsEffectShader.
            shader.SetVector3("effectBaseColor", Shader_Base_Class.Color_to_Vector(materialBase.BaseColor))
            ' BGEM output alpha = diffuse.a * cb1[0].w(BaseColor.a) * cb2[13].w(PropertyColor.w) (rec1026).
            ' En el formato BGEM el BaseColor field es RGB-only y la opacidad la lleva el common Alpha
            ' (bgem.Alpha): el app YA aliasa BaseColor.A = ClampByte(bgem.Alpha) -> son LA MISMA propiedad,
            ' NO multiplicar (seria alpha^2). cb1[0].w (BaseColor field .a) = 1.0; el alpha real = el
            ' common Alpha = PropertyColor.w (cb2[13].w). effectBaseColorAlpha = materialBase.Alpha.
            shader.SetFloat("effectBaseColorAlpha", materialBase.Alpha)
            shader.SetFloat("effectBaseColorScale", materialBase.BaseColorScale)

            '

            ' === DebugMode ===

            shader.SetFloat("DebugMode", shader.Debugmode)

            ' Alpha global
            shader.SetFloat("alpha", materialBase.Alpha)
            ' === Depth Test ===
            If ResolveDepthTestEnabled(materialBase, hasAlphaBlend) Then
                GL.Enable(EnableCap.DepthTest)
                GL.DepthFunc(DepthFunction.Lequal)   ' o el que uses por defecto
            Else
                GL.Disable(EnableCap.DepthTest)
            End If

            ' === Depth Write ===
            Dim writeDepth As Boolean = ResolveDepthWriteEnabled(materialBase, hasAlphaBlend, hasAlphaTest, MeshData.Shape.Wireframe)
            GL.DepthMask(writeDepth)
            ' === Blending / Alpha Test / Wireframe ===
            If MeshData.Shape.Wireframe Then
                ' Pasada en modo wireframe
                GL.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Line)
                GL.Enable(EnableCap.Blend)
                GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha)
            ElseIf hasAlphaBlend Then
                ' Blending estándar
                GL.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Fill)
                GL.Enable(EnableCap.Blend)
                Dim blend = material.Calculate_Blending()
                GL.BlendFunc(CType(blend(0), BlendingFactor), CType(blend(1), BlendingFactor))
            ElseIf hasAlphaTest Then
                ' Alpha test (recorte)
                GL.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Fill)
                GL.Disable(EnableCap.Blend)
            Else
                ' Material completamente opaco
                GL.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Fill)
                GL.Disable(EnableCap.Blend)
            End If

            Dim polygonOffset = ResolvePolygonOffset(materialBase)
            If polygonOffset.Enabled Then
                GL.Enable(EnableCap.PolygonOffsetFill)
                GL.PolygonOffset(polygonOffset.Factor, polygonOffset.Units)
            Else
                GL.Disable(EnableCap.PolygonOffsetFill)
            End If

            ' === Culling ===
            ' Se resuelve en la etapa de draw según el face mode efectivo del shape.

        End Sub

        ''' <summary>Tint del material en espacio sRGB 0..1 con el <see cref="FO4UnifiedMaterial_Class.TintColorScale"/>
        ''' aplicado. RENDER == BAKE: es la misma cuenta que <c>Save_To_Shader</c> hace para el Color3 del NIF
        ''' (byte/255 × scale). El scale existe porque el storage del material es de BYTES (techo duro 1.0) y la
        ''' convención SSE de pelo dobla el color del CLFM EN FLOAT — CK: 2,0 × (130/255) = 1,020, mientras que
        ''' doblar en bytes daba min(255,260)/255 = 1,000 (MEDIDO: 9 NPCs / 25 shapes, p.ej. BrowsMaleSnowElf).
        ''' El resultado puede exceder 1.0 a propósito; el shader lo tolera
        ''' (<c>color.rgb *= vec3(1.0) + vColor.y * (tintColor - vec3(1.0))</c>). Se escala ANTES de linearizar
        ''' porque pow(2c,2.2) ≠ 2·pow(c,2.2). Con scale=1.0F (default) es idéntico al comportamiento previo.</summary>
        Private Shared Function ScaledTintSrgb(tint As Color, scale As Single) As Vector3
            Dim v = Shader_Base_Class.Color_to_Vector(tint)
            If scale <> 1.0F Then v *= scale
            Return v
        End Function

        Public Sub ExportMeshToOBJ(rutaArchivo As String)
            Using sw As New StreamWriter(rutaArchivo, False, Encoding.UTF8)

                sw.WriteLine("# Exportado por ExportMeshToOBJ")
                sw.WriteLine("# Shape: " & MeshData.ShapeName)

                ' GPU Skinning: export world-space vertices (Vertices are now local-space)
                Dim wv = SkinningHelper.GetWorldVertices(MeshData.Meshgeometry)
                For Each v In wv
                    sw.WriteLine(String.Format(System.Globalization.CultureInfo.InvariantCulture, "v {0} {1} {2}", v.X, v.Y, v.Z))
                Next

                ' GPU Skinning: export world-space normals
                Dim wn = SkinningHelper.GetWorldNormals(MeshData.Meshgeometry)
                If wn IsNot Nothing AndAlso wn.Length = wv.Length Then
                    For Each n In wn
                        sw.WriteLine(String.Format(System.Globalization.CultureInfo.InvariantCulture, "vn {0} {1} {2}", n.X, n.Y, n.Z))
                    Next
                End If

                ' ?? UVs
                If MeshData.Meshgeometry.Uvs_Weight IsNot Nothing AndAlso MeshData.Meshgeometry.Uvs_Weight.Length = MeshData.Meshgeometry.Vertices.Length Then
                    For Each uv In MeshData.Meshgeometry.Uvs_Weight
                        sw.WriteLine(String.Format(System.Globalization.CultureInfo.InvariantCulture, "vt {0} {1}", uv.X, 1 - uv.Y)) ' invertir V
                    Next
                End If

                ' ?? Caras (triángulos)
                Dim tieneUV As Boolean = MeshData.Meshgeometry.Uvs_Weight IsNot Nothing AndAlso MeshData.Meshgeometry.Uvs_Weight.Length = MeshData.Meshgeometry.Vertices.Length
                Dim tieneNorm As Boolean = MeshData.Meshgeometry.Normals IsNot Nothing AndAlso MeshData.Meshgeometry.Normals.Length = MeshData.Meshgeometry.Vertices.Length

                For i = 0 To MeshData.Meshgeometry.Indices.Length - 1 Step 3
                    Dim i1 = MeshData.Meshgeometry.Indices(i) + 1
                    Dim i2 = MeshData.Meshgeometry.Indices(i + 1) + 1
                    Dim i3 = MeshData.Meshgeometry.Indices(i + 2) + 1

                    Dim f1 As String = i1.ToString()
                    Dim f2 As String = i2.ToString()
                    Dim f3 As String = i3.ToString()

                    If tieneUV AndAlso tieneNorm Then
                        f1 &= "/" & i1 & "/" & i1
                        f2 &= "/" & i2 & "/" & i2
                        f3 &= "/" & i3 & "/" & i3
                    ElseIf tieneUV Then
                        f1 &= "/" & i1
                        f2 &= "/" & i2
                        f3 &= "/" & i3
                    ElseIf tieneNorm Then
                        f1 &= "//" & i1
                        f2 &= "//" & i2
                        f3 &= "//" & i3
                    End If

                    sw.WriteLine("f " & f1 & " " & f2 & " " & f3)
                Next

            End Using
        End Sub

        Protected Overrides Sub Finalize()
            MyBase.Finalize()
        End Sub
    End Class

    Public Sub New(Parent_control As PreviewControl)
        ParentControl = Parent_control
        Floor = New FloorRenderer(ParentControl)
    End Sub

    Public Sub Processing_Status_GL(text As String)
        If Me.ParentControl Is Nothing OrElse Me.ParentControl.IsDisposed Then Exit Sub
        ' Processing_Status itself guards against teardown; this wrapper bails out
        ' early so we don't even queue the call when the control is dying.
        Me.ParentControl.Processing_Status(text)
    End Sub
    ''' <summary>
    ''' Extracts skinned geometry for each shape in parallel.
    ''' IMPORTANT: Skeleton must be prepared BEFORE calling this method
    ''' (via ISkeletonResolver, PrepareSkeletonForShapes, or equivalent).
    ''' </summary>
    ''' <param name="resolver">Optional resolver consulted per shape (<see cref="ISkeletonResolver.ResolveFor"/>)
    ''' to pick a per-shape <see cref="SkeletonInstance"/>. If Nothing, all shapes use
    ''' <see cref="SkeletonInstance.Default"/>.</param>
    Public Sub LoadShapesParallel(shapes As IEnumerable(Of IRenderableShape), Optional resolver As ISkeletonResolver = Nothing)
        If Not shapes.Any() Then Exit Sub
        LoadedShapes = shapes.ToList()
        Dim result As New ConcurrentBag(Of RenderableMesh)
        Parallel.ForEach(shapes, Sub(shape)
                                     'For Each shape In shapes
                                     Dim mesh = LoadShapeSafe(shape, resolver)

                                     If mesh IsNot Nothing Then result.Add(mesh)
                                     'Next
                                 End Sub)
        meshes.AddRange(result)
        MarkRenderBucketsDirty()
    End Sub

    Public Sub BakeOrInvertPose(inverse As Boolean)
        If LoadedShapes.Count = 0 Then Exit Sub
        For Each shap In LoadedShapes
            BakeOrInvertPose(shap, inverse)
        Next
    End Sub

    Public Sub BakeOrInvertPose(Shape As IRenderableShape, inverse As Boolean)
        Dim mesh = Me.meshes.FirstOrDefault(Function(pf) pf.MeshData.Shape Is Shape)
        If mesh Is Nothing Then Return
        ' Source of truth for "is a pose applied?" is the SkeletonInstance assigned to this
        ' shape by the resolver — its Pose property reflects the last ApplyPose() call.
        Dim resolver = ParentControl.Intent.SkeletonResolver
        Dim skel As SkeletonInstance = If(resolver IsNot Nothing, resolver.ResolveFor(Shape), SkeletonInstance.Default)
        If skel Is Nothing OrElse skel.Pose Is Nothing OrElse skel.Pose.Source = Poses_class.Pose_Source_Enum.None Then Return
        SkinningHelper.BakeFromMemoryUsingOriginal(Shape, mesh.MeshData.Meshgeometry, inverse:=inverse, ApplyMorph:=False, RemoveZaps:=False, SingleBoneSkinning)
    End Sub

    Private Function LoadShapeSafe(shape As IRenderableShape, Optional resolver As ISkeletonResolver = Nothing) As RenderableMesh
        Try
            ' 1) Obtener shape + geometría skinned (polimórfico via IShapeGeometry).
            If IsNothing(shape.NifShape) Then Return Nothing
            Dim skel As SkeletonInstance = resolver?.ResolveFor(shape)
            Dim geom = SkinningHelper.ExtractSkinnedGeometry(shape, SingleBoneSkinning, RecalculateNormals, skel)

            ' 2) Rellenar MeshData con la geometría final
            Dim mesh As New RenderableMesh.MeshData_Class With {
                .Shape = shape,
                .Meshgeometry = geom
                        }
            mesh.Material = New RenderableMesh.MaterialData(mesh)

            Dim Renderable = New RenderableMesh(mesh, Me)

            Return Renderable
        Catch ex As Exception
            Logger.LogLazy(Function() "[Render] BuildRenderable EXCEPTION: " & ex.Message)
            Debugger.Break()
            Return Nothing
        End Try
    End Function

    Public Sub Setup_GL()
        If ParentControl.IsDisposed Then Exit Sub
        Process_Indices_GL()
        Process_Textures_GL()
        If Floor Is Nothing Then Floor = New FloorRenderer(ParentControl)
        If ParentControl.IsDisposed Then Exit Sub
        ParentControl.RenderTimer.Start()
        ParentControl.UpdateProjection(True)  ' ? ya hay meshes/bounds; ajusta frustum
        Can_Render = True
        Cleaned = False
    End Sub

    Private Sub Process_Indices_GL()
        If Me.ParentControl.IsDisposed Then Exit Sub
        ParentControl.EnsureContextCurrent()
        For Each mesh In meshes
            mesh.SetupMesh_GL()
        Next
    End Sub

    Private ReadOnly Last_Loaded_Textures As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)

    ''' <summary>Per-path upload-failure counter. A path is retried up to
    ''' <see cref="MaxTextureUploadAttempts"/> times before being marked as a permanent
    ''' dead end (added to <see cref="Last_Loaded_Textures"/>). Covers the case where the
    ''' path is genuinely unloadable (corrupt DDS, format the driver refuses, etc.) so the
    ''' retry loop can't run forever and starve TexturesReady.</summary>
    Private ReadOnly _uploadFailureCount As New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)

    Private Const MaxTextureUploadAttempts As Integer = 5

    ' O4.1: Background Texture Loading — two-phase pipeline
    ' Phase 1 runs on a background thread (DDS I/O + decompression, no GL calls).
    ' Phase 2 runs on the GL thread each frame (upload a limited batch via PBO).
    ' Between phases, meshes are hidden (TexturesReady=False) and a status overlay is shown.

    ''' <summary>
    ''' Queue of batches produced by background DDS loading, waiting for GL upload.
    ''' Each entry contains the texture paths and their decompressed pixel data.
    ''' Written by background tasks, read only on the GL thread.
    ''' </summary>
    Private ReadOnly _pendingTextureUploads As New ConcurrentQueue(Of Dictionary(Of String, DirectXTexWrapperCLI.TextureLoaded))

    ''' <summary>
    ''' Cancellation source for the currently running background texture load.
    ''' Replaced atomically when a new load is requested.
    ''' </summary>
    Private _backgroundLoadCts As Threading.CancellationTokenSource = Nothing

    ''' <summary>
    ''' The currently running background texture load task, used for awaiting/checking completion.
    ''' </summary>
    Private _backgroundLoadTask As Task = Task.CompletedTask

    ''' <summary>
    ''' Maximum number of individual textures to upload to GL per frame.
    ''' Keeps frame time bounded while progressively loading textures.
    ''' </summary>
    Private Const MaxTextureUploadsPerFrame As Integer = 64

    ''' <summary>
    ''' Set of texture paths currently queued for background loading (to avoid duplicate loads).
    ''' Cleared when background task completes or is cancelled.
    ''' </summary>
    Private ReadOnly _pendingBackgroundPaths As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)

    ''' <summary>Increment the per-path failure counter. Once it reaches
    ''' <see cref="MaxTextureUploadAttempts"/> the path is added to <see cref="Last_Loaded_Textures"/>
    ''' so <see cref="Process_Textures_GL"/> stops re-enqueuing it. Below the cap the path stays
    ''' eligible for retry on the next Process_Textures_GL pass.</summary>
    Private Sub RegisterUploadFailure(path As String, reason As String)
        Dim count As Integer = 0
        _uploadFailureCount.TryGetValue(path, count)
        count += 1
        _uploadFailureCount(path) = count
        If count >= MaxTextureUploadAttempts Then
            Last_Loaded_Textures.Add(path)
            Logger.LogLazy(Function() $"[Render] '{path}' marked dead after {count} upload failures (last: {reason})")
        End If
    End Sub

    Public Sub Process_Textures_GL()
        If Me.ParentControl.IsDisposed Then Exit Sub

        ' Collect all texture paths needed by current meshes that are not yet loaded
        Dim texturas As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        texturas.UnionWith(
            Me.meshes.
                SelectMany(Function(pf) pf.MeshData.Material.Textures_Path_List).
                Where(Function(pf) pf <> "").
                Distinct(StringComparer.OrdinalIgnoreCase).
                Where(Function(pf) Textures_Dictionary.ContainsKey(pf) = False))

        ' Overlay layers (LooksMenu/tattoos): each layer's material textures MUST also be uploaded or
        ' the overlay renders untextured/white. Reuse the SAME 14-slot Textures_Path_List by building
        ' a transient MaterialData with OverrideRelatedMaterial = layer.Material (no path-list dup).
        ' No-overlay path (every WM render, every untattooed NPC): OverlayLayers is Nothing -> this
        ' adds nothing, so the loaded set is byte-identical to before.
        texturas.UnionWith(
            Me.meshes.
                SelectMany(Function(pf) RenderableMesh.EnumerateOverlayTexturePaths(pf.MeshData)).
                Where(Function(pf) pf <> "").
                Distinct(StringComparer.OrdinalIgnoreCase).
                Where(Function(pf) Textures_Dictionary.ContainsKey(pf) = False))

        ' Record which of those paths are COLOR textures (base color) so the GL upload decodes them sRGB,
        ' like the engine's per-texture sRGB flag. Persistent + additive: a path's role is stable, and the
        ' set is consulted by name at upload time. Data textures are never added -> they stay linear.
        For Each m In Me.meshes
            For Each cp In m.MeshData.Material.ColorTextures_Path_List
                If cp <> "" Then SRGBTexturePaths.Add(cp)
            Next
            ' Same for each overlay layer's color textures (transient MaterialData over the layer
            ' material). No-overlay path adds nothing (OverlayLayers Nothing -> zero iterations).
            For Each cp In RenderableMesh.EnumerateOverlayColorTexturePaths(m.MeshData)
                If cp <> "" Then SRGBTexturePaths.Add(cp)
            Next
        Next

        texturas.ExceptWith(Last_Loaded_Textures)

        ' Also exclude paths already queued for background loading
        SyncLock _pendingBackgroundPaths
            texturas.ExceptWith(_pendingBackgroundPaths)
        End SyncLock

        If texturas.Count = 0 Then Exit Sub

        ' Cancel any previous background load that hasn't finished
        If _backgroundLoadCts IsNot Nothing Then
            _backgroundLoadCts.Cancel()
            _backgroundLoadCts.Dispose()
        End If
        _backgroundLoadCts = New Threading.CancellationTokenSource()
        Dim ct = _backgroundLoadCts.Token

        ' Mark textures as not ready — meshes will be hidden until all uploads complete
        TexturesReady = False

        ' Arm the post-texture-upload watchdog if the caller registered a timeout action with
        ' a positive deadline. If timeout is 0 or no action was registered, leave the deadline
        ' Nothing — the success path still works without watchdog.
        Dim intent = ParentControl.Intent
        If intent.PostTextureUploadAction IsNot Nothing AndAlso intent.PostTextureUploadTimeoutMs > 0 Then
            _postTextureUploadDeadlineUtc = DateTime.UtcNow.AddMilliseconds(intent.PostTextureUploadTimeoutMs)
        Else
            _postTextureUploadDeadlineUtc = Nothing
        End If

        ' Track which paths we are about to load
        Dim pathsArray = texturas.ToArray()
        SyncLock _pendingBackgroundPaths
            For Each p In pathsArray
                _pendingBackgroundPaths.Add(p)
            Next
        End SyncLock

        ' Capture control reference before entering the background thread
        Dim controlRef = Me.ParentControl

        ' Launch background DDS loading task (Phase 1: I/O + decompression, no GL)
        _backgroundLoadTask = Task.Run(
            Sub()
                Try
                    ct.ThrowIfCancellationRequested()
                    Dim loaded = DirectXDDSLoader.LoadTexturesFromDictionary_Background(
                        pathsArray, useCompress:=True, forceOpenGL:=True, ct:=ct)

                    ct.ThrowIfCancellationRequested()

                    ' Enqueue result for GL-thread upload (Phase 2)
                    _pendingTextureUploads.Enqueue(loaded)

                    ' Signal the GL thread to wake up and process pending uploads. Setting
                    ' UpdateRequired alone is enough — the RenderTimer_Tick polls for it and
                    ' calls Invalidate() guarded by Context.IsCurrent so we don't steal the
                    ' GL context from a sibling PreviewControl (e.g. an active modal editor)
                    ' just because a background-thread texture decode happened to finish.
                    If controlRef IsNot Nothing AndAlso Not controlRef.IsDisposed AndAlso controlRef.IsHandleCreated Then
                        controlRef.BeginInvoke(Sub() controlRef.UpdateRequired = True)
                    End If
                Catch ex As OperationCanceledException
                    ' Cancelled — remove paths from pending set so they can be retried
                    SyncLock _pendingBackgroundPaths
                        For Each p In pathsArray
                            _pendingBackgroundPaths.Remove(p)
                        Next
                    End SyncLock
                Catch ex As Exception
                    ' On unexpected failure, remove pending paths and log
                    SyncLock _pendingBackgroundPaths
                        For Each p In pathsArray
                            _pendingBackgroundPaths.Remove(p)
                        Next
                    End SyncLock
                    Logger.LogLazy(Function() $"[Render] Background texture load failed: {ex.Message}")
                End Try
            End Sub, ct)

        ' Return immediately — meshes are hidden (TexturesReady=False) until
        ' ProcessPendingTextureUploads() uploads all textures and sets TexturesReady=True.
    End Sub

    ''' <summary>
    ''' O4.1 Phase 2 — Called on the GL thread each frame (from RenderAll).
    ''' Drains the pending texture upload queue, uploading up to MaxTextureUploadsPerFrame
    ''' textures per frame to avoid frame-time spikes.
    ''' Updates Textures_Dictionary with the new GL texture IDs and triggers a repaint.
    ''' </summary>
    Public Sub ProcessPendingTextureUploads()
        If Me.ParentControl.IsDisposed Then Exit Sub

        Dim uploadedThisFrame As Integer = 0
        Dim anyUploaded As Boolean = False

        ' Process batches from the queue
        If Not _pendingTextureUploads.IsEmpty Then
            While Not _pendingTextureUploads.IsEmpty AndAlso uploadedThisFrame < MaxTextureUploadsPerFrame
                Dim batch As Dictionary(Of String, DirectXTexWrapperCLI.TextureLoaded) = Nothing

                ' Peek at current batch; we may not finish it in one frame
                If Not _pendingTextureUploads.TryPeek(batch) Then Exit While
                If batch Is Nothing Then
                    _pendingTextureUploads.TryDequeue(batch)
                    Continue While
                End If

                ' Upload textures from this batch, up to per-frame limit
                Dim keysToRemove As New List(Of String)
                For Each kvp In batch
                    If uploadedThisFrame >= MaxTextureUploadsPerFrame Then Exit For

                    Dim path = kvp.Key
                    Dim tex = kvp.Value

                    Try
                        Dim result = DirectXDDSLoader.UploadTextureToGL(tex, path, SRGBTexturePaths.Contains(path))

                        If result IsNot Nothing AndAlso result.Loaded AndAlso result.Texture_ID > 0 Then
                            ' Re-upload to an existing key: free the previous GL texture before
                            ' overwriting, or its handle leaks. Render buckets are rebuilt this frame
                            ' via MarkRenderBucketsDirty (below), so no live bucket keeps the old ID.
                            Dim old As Texture_Loaded_Class = Nothing
                            If Textures_Dictionary.TryGetValue(path, old) AndAlso old IsNot Nothing AndAlso
                               old.Texture_ID > 0 AndAlso old.Texture_ID <> result.Texture_ID Then
                                GL.DeleteTexture(old.Texture_ID)
                            End If
                            Textures_Dictionary(path) = result
                            Last_Loaded_Textures.Add(path)
                            _uploadFailureCount.Remove(path)
                        Else
                            Textures_Dictionary.Remove(path)
                            RegisterUploadFailure(path, "silent")
                        End If
                    Catch ex As Exception
                        Logger.LogLazy(Function() $"[Render] GL upload failed for '{path}': {ex.Message}")
                        Textures_Dictionary.Remove(path)
                        RegisterUploadFailure(path, ex.Message)
                    End Try

                    ' Remove from pending tracking
                    SyncLock _pendingBackgroundPaths
                        _pendingBackgroundPaths.Remove(path)
                    End SyncLock

                    keysToRemove.Add(path)
                    uploadedThisFrame += 1
                    anyUploaded = True
                Next

                ' Remove uploaded entries from the batch
                For Each key In keysToRemove
                    batch.Remove(key)
                Next

                ' If the batch is now empty, dequeue it
                If batch.Count = 0 Then
                    _pendingTextureUploads.TryDequeue(batch)
                Else
                    ' Batch still has remaining textures — stop for this frame
                    Exit While
                End If
            End While
        End If

        ' If textures were uploaded, rebuild render buckets (for texture sort order)
        ' and trigger a repaint so the new textures are visible immediately
        If anyUploaded Then
            MarkRenderBucketsDirty()
            ParentControl.UpdateRequired = True
            ParentControl.Invalidate()
        End If

        ' If there are STILL pending textures (batch not fully processed or more batches),
        ' keep the render loop active so the next frame processes more uploads
        If Not _pendingTextureUploads.IsEmpty Then
            ParentControl.UpdateRequired = True
        End If

        ' Check if all textures are now loaded (queue empty AND no background task running).
        ' Before declaring Ready, call Process_Textures_GL to catch any textures that were
        ' dropped due to a prior cancellation (cancel removes paths from _pendingBackgroundPaths
        ' but the new task may not have included them, leaving them unloaded indefinitely).
        If _pendingTextureUploads.IsEmpty AndAlso (_backgroundLoadTask Is Nothing OrElse _backgroundLoadTask.IsCompleted) Then
            Process_Textures_GL()  ' no-op if all mesh textures are already loaded or pending
            ' Only mark Ready if the retry check found nothing new to queue
            If _pendingTextureUploads.IsEmpty AndAlso (_backgroundLoadTask Is Nothing OrElse _backgroundLoadTask.IsCompleted) Then
                If Not TexturesReady Then
                    TexturesReady = True
                    ' Fire the post-texture-upload hook BEFORE the repaint so any GL state the
                    ' callback mutates (e.g. re-uploading a diffuse with bake passes applied)
                    ' is visible in the same frame the textures become ready. The hook is the
                    ' single point where post-upload work is sequenced relative to the False→True
                    ' transition — replaces the per-app polling timer that competed with the
                    ' pipeline order. Watchdog deadline (if armed) is cleared on success too so
                    ' a stale deadline can't fire after a healthy completion.
                    InvokePostTextureUploadHook(success:=True)
                    ParentControl.UpdateRequired = True
                    ParentControl.Invalidate()
                End If
            End If
        End If

        ' Watchdog: if a deadline was armed and we're still not ready by the time it elapses,
        ' fire the timeout action instead of leaving the caller waiting forever. This covers
        ' BA2 corruption, FilesDictionary misses that drop a path, and cancelled background
        ' loads that left an upload queue in an inconsistent state. Done AFTER the success
        ' branch so a healthy late completion in the same frame still wins over the deadline.
        If Not TexturesReady AndAlso _postTextureUploadDeadlineUtc.HasValue _
           AndAlso DateTime.UtcNow >= _postTextureUploadDeadlineUtc.Value Then
            InvokePostTextureUploadHook(success:=False)
        End If
    End Sub

    ''' <summary>One-shot dispatch of the post-texture-upload hook. Reads the appropriate
    ''' callback (success vs timeout) from the active <see cref="RenderIntent"/>, clears BOTH
    ''' callbacks + the deadline so neither can fire again, then invokes inside a Try so an
    ''' exception in app code can't break the render loop. After the callback returns, the
    ''' render buckets are marked dirty in case the callback replaced any
    ''' <c>Textures_Dictionary[path].Texture_ID</c> entry — the texture-sort buckets keyed by
    ''' Texture_ID would otherwise reference dead GL handles.</summary>
    Private Sub InvokePostTextureUploadHook(success As Boolean)
        Dim intent = ParentControl.Intent
        Dim hook As Action(Of PreviewModel) = If(success, intent.PostTextureUploadAction, intent.PostTextureUploadTimeoutAction)
        ' Clear BEFORE invoking so a re-entrant render kicked off inside the callback (typical:
        ' the callback runs RefreshFaceTintLivePreview which calls InvalidateRender) cannot see
        ' the already-firing hook and double-dispatch.
        intent.PostTextureUploadAction = Nothing
        intent.PostTextureUploadTimeoutAction = Nothing
        _postTextureUploadDeadlineUtc = Nothing
        If hook Is Nothing Then Return
        Try
            hook.Invoke(Me)
        Catch ex As Exception
            Logger.LogLazy(Function() $"[Render] PostTextureUpload {(If(success, "success", "timeout"))} hook threw: {ex}")
        End Try
        ' The callback may have replaced one or more entry.Texture_ID values (face/body skin
        ' softlight passes do this when baking QNAM into the diffuse). Sort order in
        ' OpaqueMeshes / CutoutMeshes / DecalMeshes / BlendedMeshes is keyed by Texture_ID at
        ' line 3210 — rebuild on next paint so the new IDs replace the dead handles.
        MarkRenderBucketsDirty()
    End Sub

    Public Sub CleanTextures()
        CancelPendingTextureUploads()

        ' — Eliminar texturas cargadas —
        Dim seen As New HashSet(Of UInteger)
        For Each texID In Textures_Dictionary.Values.Select(Function(pf) pf.Texture_ID)
            If texID > 0 AndAlso Not seen.Contains(texID) Then
                GL.DeleteTexture(texID)
                seen.Add(texID)
            End If
        Next
        ' Limpia diccionario
        Textures_Dictionary.Clear()
        Last_Loaded_Textures.Clear()
        _uploadFailureCount.Clear()
        ' Clear the raw-bytes cache so that loose .dds/.bgsm files modified on disk
        ' while the app is running are re-read fresh on the next load, not returned stale.
        FilesDictionary_class.ClearBytesCache()
    End Sub

    ''' <summary>Cancels any in-flight background texture load + drains the pending upload queue
    ''' + clears the pending-paths tracker. Does NOT touch <see cref="Textures_Dictionary"/>,
    ''' <see cref="Last_Loaded_Textures"/>, or the raw-bytes cache — already-uploaded GL textures
    ''' stay live and reusable. Used by the shape-set-swap path when the caller opted into
    ''' <see cref="RenderIntent.PreserveTextureCache"/>: cancelling pending uploads is unsafe to
    ''' skip because the in-flight loads were keyed on the previous shape set's texture paths and
    ''' could race with the new set's loads, but tearing down the GPU-resident cache is wasteful
    ''' when the caller knows the new set will mostly reuse the same textures.</summary>
    Public Sub CancelPendingTextureUploads()
        ' O4.1: Cancel any in-flight background texture load and drain the pending queue
        If _backgroundLoadCts IsNot Nothing Then
            _backgroundLoadCts.Cancel()
            _backgroundLoadCts.Dispose()
            _backgroundLoadCts = Nothing
        End If
        ' Drain and discard pending uploads (free decompressed pixel data)
        Dim discarded As Dictionary(Of String, DirectXTexWrapperCLI.TextureLoaded) = Nothing
        While _pendingTextureUploads.TryDequeue(discarded)
            If discarded IsNot Nothing Then
                For Each kvp In discarded
                    If kvp.Value IsNot Nothing AndAlso kvp.Value.Levels IsNot Nothing Then
                        For Each lvl In kvp.Value.Levels
                            lvl.Data = Nothing
                        Next
                        kvp.Value.Levels.Clear()
                    End If
                Next
            End If
        End While
        SyncLock _pendingBackgroundPaths
            _pendingBackgroundPaths.Clear()
        End SyncLock
    End Sub
    Public Sub CleanSingleTexture(Cual As String)
        Try
            Cual = FO4UnifiedMaterial_Class.CorrectTexturePath(Cual)
            ' O4.1: Also remove from pending background paths so it can be re-requested
            SyncLock _pendingBackgroundPaths
                _pendingBackgroundPaths.Remove(Cual)
            End SyncLock
            ' Remove from any already-decoded batches waiting in _pendingTextureUploads.
            ' Without this, a batch queued before the single-texture invalidation can re-upload
            ' the obsolete GL texture right after we deleted it (hot-reload race condition).
            For Each batch In _pendingTextureUploads
                batch.Remove(Cual)
            Next
            ' — Eliminar texturas cargadas —
            Dim seen As New HashSet(Of UInteger)
            For Each texID In Textures_Dictionary.Values.Where(Function(pf) pf.Path.Equals(Cual, StringComparison.OrdinalIgnoreCase)).Select(Function(pf) pf.Texture_ID)
                If texID > 0 AndAlso Not seen.Contains(texID) Then
                    GL.DeleteTexture(texID)
                    seen.Add(texID)
                End If
            Next
            ' Limpia diccionario
            Textures_Dictionary.Remove(Cual)
            Last_Loaded_Textures.Remove(Cual)
            _uploadFailureCount.Remove(Cual)
        Catch ex As Exception
            Debugger.Break()
        End Try
    End Sub
    Public Sub Clean(ShowText As Boolean)
        Cleaned = True
        Can_Render = False
        TexturesReady = True
        If Not IsNothing(ParentControl.RenderTimer) Then ParentControl.RenderTimer.Stop()
        ParentControl.EnsureContextCurrent()
        ParentControl.UpdateRequired = True
        If ShowText Then Me.ParentControl.Processing_Status("Cleaned")
        ' Limpia meshes internamente
        For Each mesh In meshes
            mesh.Clean()
        Next
        ' Borra Meshes
        meshes.Clear()
        OpaqueMeshes.Clear()
        CutoutMeshes.Clear()
        DecalMeshes.Clear()
        BlendedMeshes.Clear()
        BlendedDepthBuffer.Clear()
        MarkRenderBucketsDirty()

        Dim i = 0
        While GL.GetError() <> ErrorCode.NoError
            i += 1
            If i > 10 Then Debugger.Break() : Exit While
        End While
    End Sub

    Structure MeshDepth
        Public Mesh As RenderableMesh
        Public Depth As Single
    End Structure

    Public Property FloorOffset As Double = -0.00F
    Public Sub RenderAll(projection As Matrix4, camera As OrbitCamera)
        ' O4.1: Process pending background texture uploads (Phase 2) each frame
        ProcessPendingTextureUploads()

        ' Hide meshes while textures are still loading — show status overlay instead
        If Not TexturesReady Then
            If Floor IsNot Nothing AndAlso Floor.Enabled = True Then Floor.Render(projection, camera, FloorOffset)
            ParentControl.Processing_Status("Texturing...")
            ParentControl.UpdateRequired = True
            Exit Sub
        End If

        If Floor IsNot Nothing AndAlso Floor.Enabled = True Then Floor.Render(projection, camera, FloorOffset)
        If meshes.Count = 0 Then Exit Sub
        ' Note: ShapeDataLoaded is intentionally NOT checked here. Each mesh.Render() guards
        ' against null RelatedNifShape internally. Checking ShapeDataLoaded at this level would
        ' stop rendering all meshes whose VBOs are still valid just because the CPU-side shapedata
        ' was evicted by the LRU, which is an unnecessary regression in render quality.

        If RenderBucketsDirty OrElse (OpaqueMeshes.Count + CutoutMeshes.Count + DecalMeshes.Count + BlendedMeshes.Count) <> meshes.Count Then
            RebuildRenderBuckets()

            ' O3.5: Sort opaque and cutout meshes by diffuse texture ID to minimize GL state changes.
            ' Texture binds are expensive; grouping meshes with the same textures reduces bind calls.
            OpaqueMeshes.Sort(Function(a, b) a.MeshData.Material.DiffuseTexture_ID.CompareTo(b.MeshData.Material.DiffuseTexture_ID))
            CutoutMeshes.Sort(Function(a, b) a.MeshData.Material.DiffuseTexture_ID.CompareTo(b.MeshData.Material.DiffuseTexture_ID))
        End If

        ' O3.3: Compute view-projection matrix for frustum culling
        Dim viewMatrix = camera.GetViewMatrix()
        Dim vp As Matrix4 = viewMatrix * projection

        ' 1. OPAQUE — sin blending, depth write habilitado
        For Each mesh In OpaqueMeshes
            ' O3.3: Skip meshes whose AABB is entirely outside the view frustum
            If Not RenderableMesh.IsAABBInFrustum(mesh.BoundsMin, mesh.BoundsMax, vp) Then Continue For
            mesh.Render(projection, camera)
        Next

        ' 2. CUTOUT — alpha test, sin blending, depth write habilitado
        For Each mesh In CutoutMeshes
            If Not RenderableMesh.IsAABBInFrustum(mesh.BoundsMin, mesh.BoundsMax, vp) Then Continue For
            mesh.Render(projection, camera)
        Next
        ' 3. DECAL — overlay coplanar ocluido por depth de escena
        If DecalMeshes.Count > 0 Then
            For Each mesh In DecalMeshes
                If Not RenderableMesh.IsAABBInFrustum(mesh.BoundsMin, mesh.BoundsMax, vp) Then Continue For
                mesh.Render(projection, camera)
            Next
        End If

        ' 4. BLENDED — requiere ordenamiento por profundidad.
        ' (Was an early `Exit Sub` when empty; now a guarded block so the overlay pass 5 below still
        ' runs even with zero blended meshes — tattoos live on the OPAQUE skin body, not a blended mesh.)
        If BlendedMeshes.Count > 0 Then
            BlendedDepthBuffer.Clear()

            For Each mesh In BlendedMeshes
                ' O3.3: Frustum cull blended meshes too
                If Not RenderableMesh.IsAABBInFrustum(mesh.BoundsMin, mesh.BoundsMax, vp) Then Continue For
                Dim viewPos = Vector3.TransformPosition(mesh.MeshData.Meshgeometry.Boundingcenter, viewMatrix)
                BlendedDepthBuffer.Add(New MeshDepth With {.Mesh = mesh, .Depth = -viewPos.Z})
            Next
            BlendedDepthBuffer.Sort(Function(a, b) b.Depth.CompareTo(a.Depth))
            For Each item In BlendedDepthBuffer
                item.Mesh.Render(projection, camera)
            Next
        End If

        ' 5. OVERLAY LAYERS (LooksMenu/tattoos) — drawn LAST, after every base mesh, as coplanar
        ' decals over each shape's already-deformed geometry (RenderableMesh.RenderOverlayLayer).
        ' INERTNESS: when no shape carries OverlayLayers (every Wardrobe_Manager render, every NPC
        ' render with no tattoos), MeshData.Shape.OverlayLayers is Nothing/empty for all meshes, so
        ' this loop binds nothing and draws nothing — behavior is identical to before this pass existed.
        For Each mesh In meshes
            Dim layers = mesh.MeshData.Shape?.OverlayLayers
            If layers Is Nothing OrElse layers.Count = 0 Then Continue For
            ' Frustum-cull like the other passes (same AABB as the base shape — geometry is shared).
            If Not RenderableMesh.IsAABBInFrustum(mesh.BoundsMin, mesh.BoundsMax, vp) Then Continue For
            ' List order = draw order (app pre-sorts by LooksMenu priority ascending).
            For Each layer In layers
                mesh.RenderOverlayLayer(projection, camera, layer)
            Next
        Next
    End Sub
End Class
Public Class FloorRenderer
    Implements IDisposable

    Private ReadOnly ParentControl As PreviewControl
    Private vao As Integer
    Private vbo As Integer
    Private vertexCount As Integer

    Public Initialized As Boolean = False
    Public Property Enabled As Boolean = False
    Public Property Size As Single = 400.0F
    Public Property StepSize As Single = 10.0F
    Public Property Color As Color = Color.FromKnownColor(KnownColor.ControlLight)

    Public Sub New(parentControl As PreviewControl)
        Me.ParentControl = parentControl
    End Sub

    Private Sub CreateGeometry()
        If vao > 0 Then GL.DeleteVertexArray(vao) : vao = 0
        If vbo > 0 Then GL.DeleteBuffer(vbo) : vbo = 0

        If StepSize <= 0 Then StepSize = 10.0F
        If Size <= 0 Then Size = 100.0F

        Dim halfSize As Single = Size * 0.5F
        Dim lineCountPerAxis As Integer = CInt(Math.Floor(Size / StepSize)) + 1

        Dim verts As New List(Of Single)

        Dim startPos As Single = -halfSize
        Dim endPos As Single = halfSize

        For i As Integer = 0 To lineCountPerAxis - 1
            Dim p As Single = startPos + (i * StepSize)

            If p > endPos Then Exit For

            ' línea paralela al eje Y, en X = p
            verts.Add(p) : verts.Add(startPos) : verts.Add(0.0F)
            verts.Add(p) : verts.Add(endPos) : verts.Add(0.0F)

            ' línea paralela al eje X, en Y = p
            verts.Add(startPos) : verts.Add(p) : verts.Add(0.0F)
            verts.Add(endPos) : verts.Add(p) : verts.Add(0.0F)
        Next

        ' asegurar borde final si no cayó exacto
        If Math.Abs(endPos - (startPos + ((lineCountPerAxis - 1) * StepSize))) > 0.0001F Then
            Dim p As Single = endPos

            verts.Add(p) : verts.Add(startPos) : verts.Add(0.0F)
            verts.Add(p) : verts.Add(endPos) : verts.Add(0.0F)

            verts.Add(startPos) : verts.Add(p) : verts.Add(0.0F)
            verts.Add(endPos) : verts.Add(p) : verts.Add(0.0F)
        End If

        Dim vertices As Single() = verts.ToArray()
        vertexCount = vertices.Length \ 3

        vao = GL.GenVertexArray()
        vbo = GL.GenBuffer()

        GL.BindVertexArray(vao)

        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo)
        GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * 4, vertices, BufferUsageHint.StaticDraw)

        GL.EnableVertexAttribArray(0)
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, False, 12, 0)

        GL.BindBuffer(BufferTarget.ArrayBuffer, 0)
        GL.BindVertexArray(0)
    End Sub

    Public Sub Render(projection As Matrix4, camera As OrbitCamera, offsetZ As Double)
        If Not Enabled Then Exit Sub
        If Not Initialized Then Rebuild()
        If Not Initialized Then Exit Sub
        If vao = 0 OrElse vertexCount <= 0 Then Exit Sub
        If IsNothing(ParentControl) OrElse IsNothing(ParentControl.SharedFloorShader) Then Exit Sub

        Dim shader = ParentControl.SharedFloorShader

        shader.Use()

        GL.Disable(EnableCap.Blend)
        GL.Enable(EnableCap.DepthTest)
        GL.DepthMask(True)
        GL.Disable(EnableCap.CullFace)

        Dim view As Matrix4 = camera.GetViewMatrix()
        Dim model As Matrix4 = Matrix4.CreateTranslation(0.0F, 0.0F, CSng(offsetZ) + 0.01F)

        shader.SetMatrix4("matProjection", projection)
        shader.SetMatrix4("matView", view)
        shader.SetMatrix4("matModel", model)
        shader.SetVector3("gridColor", New Vector3(Color.R / 255.0F, Color.G / 255.0F, Color.B / 255.0F))

        GL.BindVertexArray(vao)
        GL.DrawArrays(PrimitiveType.Lines, 0, vertexCount)
        GL.BindVertexArray(0)

        GL.UseProgram(0)
        GL.Enable(EnableCap.CullFace)
    End Sub

    Public Sub Rebuild()
        CreateGeometry()
        Initialized = (vao <> 0 AndAlso vbo <> 0 AndAlso vertexCount > 0)
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        If vao > 0 Then GL.DeleteVertexArray(vao) : vao = 0
        If vbo > 0 Then GL.DeleteBuffer(vbo) : vbo = 0
        Initialized = False
        GC.SuppressFinalize(Me)
    End Sub
End Class

Public Class OrbitCamera
    Private Const RotateScale As Single = 0.01F
    Private Shared ReadOnly MaxElevation As Single = MathF.PI / 2.0F - 0.02F

    Friend angleX As Single
    Friend angleY As Single
    Public distance As Single
    Public Optimaldistance As Single = 0

    Public Property FocusPosition As Vector3
    Public Property MinDistance As Single = 20
    Public Property MaxDistance As Single = 900

    Public Property Forward As Vector3
    Public right As Vector3
    Public upPlane As Vector3

    Public Sub New()
        angleX = 0
        angleY = 0
        distance = 167
        FocusPosition = Vector3.Zero
        UpdateDirectionFromAngles()
    End Sub

    Public Sub UpdateDirectionFromAngles()
        Dim cosElev = CSng(Math.Cos(angleY))
        Dim sinElev = CSng(Math.Sin(angleY))
        Dim cosAz = CSng(Math.Cos(angleX))
        Dim sinAz = CSng(Math.Sin(angleX))
        Forward = Vector3.Normalize(New Vector3(cosElev * sinAz, cosElev * cosAz, sinElev))
        right = Vector3.Normalize(Vector3.Cross(Forward, Vector3.UnitZ))
        upPlane = Vector3.Normalize(Vector3.Cross(right, Forward))
    End Sub

    Public Sub Rotate(dx As Single, dy As Single)
        angleX += dx * RotateScale
        angleY = Math.Clamp(angleY + dy * RotateScale, -MaxElevation, MaxElevation)
        UpdateDirectionFromAngles()
    End Sub

    ''' <summary>
    ''' Pan en pixels de pantalla. Grab-and-drag: mouse derecha mueve modelo derecha.
    ''' </summary>
    Public Sub Pan(dxPixels As Single, dyPixels As Single)
        Dim scale As Single = distance * RotateScale * 0.2F
        FocusPosition += (dxPixels * scale) * right + (dyPixels * scale) * upPlane
    End Sub

    Public Sub Zoom(delta As Single)
        Dim factor As Single = MathF.Exp(-RotateScale * 5 * delta)
        distance = Math.Clamp(distance * factor, MinDistance, MaxDistance)
    End Sub

    Public Function GetViewMatrix() As Matrix4
        Dim eye = FocusPosition + Forward * distance
        Return Matrix4.LookAt(eye, FocusPosition, Vector3.UnitZ)
    End Function

    Public Function GetEyePosition() As Vector3
        Return FocusPosition + Forward * distance
    End Function
End Class





