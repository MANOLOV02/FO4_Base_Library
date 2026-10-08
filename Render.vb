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

    ''' <summary>Sets the label to draw. <paramref name="maxWidth"/> &gt; 0 wraps the text at that width in pixels (GenerateTextBitmap),
    ''' so a label drawn at x on a window x + maxWidth wide or wider is never cut at its right edge; 0 = no wrap.
    ''' <paramref name="textColor"/> / <paramref name="outline"/>: see GenerateTextBitmap.</summary>
    Public Sub SetText(text As String, Optional fontSize As Integer = 32, Optional fontName As String = "Arial", Optional maxWidth As Integer = 0,
                       Optional textColor As Color? = Nothing, Optional outline As Color? = Nothing)
        ' The cache key holds everything the bitmap depends on: the same text at another size, font, width or colour is another bitmap.
        Dim key = $"{fontSize}|{fontName}|{maxWidth}|{If(textColor, Color.Gray).ToArgb()}|{If(outline.HasValue, outline.Value.ToArgb().ToString(), "-")}|{text}"
        Dim bmp As Bitmap = Nothing
        If Not Labels.TryGetValue(key, bmp) Then
            bmp = GenerateTextBitmap(text, fontSize, fontName, maxWidth, textColor, outline)
            If Labels.Count >= 5 Then
                Dim oldest = Labels.First()
                oldest.Value.Dispose()
                Labels.Remove(oldest.Key)
            End If
            Labels.Add(key, bmp)
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

    ''' <summary>Size in pixels of the text last set (SetText).</summary>
    Public ReadOnly Property LabelWidth As Integer
        Get
            Return textWidth
        End Get
    End Property
    Public ReadOnly Property LabelHeight As Integer
        Get
            Return textHeight
        End Get
    End Property

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

    ''' <summary>Las dos fuentes GLSL del overlay de texto, IZADAS A CONSTANTES.
    ''' <para>Eran variables LOCALES dentro de este mismo <c>Sub</c>, y eso las dejaba fuera del gate
    ''' <c>glsl-ascii</c> por partida doble: no estaban en <c>FaceTintCompositor.AllShaderSources()</c> y
    ''' el barrido por reflexion no puede verlas —una variable local no es un campo, por definicion—.
    ''' O sea que un solo caracter no-ASCII en un comentario de estas dos dejaba el shader sin compilar
    ''' con el fallo MUDO en Release que ese gate existe para impedir, y ningun gate lo veia.
    ''' La exclusion estaba anotada en el doc de <c>AllShaderSources</c> diciendo "habria que izarlos a
    ''' constantes"; esto es eso. Al ser <c>Const</c>, la reflexion las descubre sola y el gate exige
    ''' ademas que esten registradas.</para></summary>
    Friend Const VertexOverlaySrc As String =
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

    Friend Const FragmentOverlaySrc As String =
"#version 330 core
in vec2 TexCoord;
out vec4 FragColor;

uniform sampler2D uTexture;

void main()
{
    FragColor = texture(uTexture, TexCoord);
}"

    Private Sub CompileShaders()
        Dim vertexShaderSrc As String = VertexOverlaySrc
        Dim fragmentShaderSrc As String = FragmentOverlaySrc

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

    ''' <summary>The size of a label as GenerateTextBitmap lays it out: the same font, the same rendering hint, the same layout width
    ''' (<paramref name="maxWidth"/> &gt; 0: GDI+ word wrap at that width).</summary>
    Friend Shared Function MeasureLabel(text As String, fontSize As Integer, fontName As String, Optional maxWidth As Integer = 0) As SizeF
        Using testBmp As New Bitmap(1, 1)
            Using g As Graphics = Graphics.FromImage(testBmp)
                g.TextRenderingHint = Drawing.Text.TextRenderingHint.AntiAliasGridFit
                Using fnt As New Font(fontName, fontSize, FontStyle.Bold)
                    Return If(maxWidth > 0, g.MeasureString(text, fnt, maxWidth), g.MeasureString(text, fnt))
                End Using
            End Using
        End Using
    End Function

    ''' <summary>The lines joined, or - when they do not fit <paramref name="maxHeight"/> at <paramref name="maxWidth"/> - the first ones
    ''' that fit followed by "+K more" (K = the lines left out), measured with MeasureLabel; at least the "+K more" line.</summary>
    Friend Shared Function FitLines(lines As IList(Of String), fontSize As Integer, fontName As String, maxWidth As Integer, maxHeight As Integer) As String
        Dim fits = Function(t As String) Math.Ceiling(MeasureLabel(t, fontSize, fontName, maxWidth).Height) <= maxHeight
        Dim full = String.Join(vbLf, lines)
        If fits(full) Then Return full
        For shown = lines.Count - 1 To 0 Step -1
            Dim t = String.Join(vbLf, lines.Take(shown).Concat({$"+{lines.Count - shown} more"}))
            If shown = 0 OrElse fits(t) Then Return t
        Next
        Return full
    End Function

    ''' <summary>The label's bitmap, measured (MeasureLabel) and drawn with ONE layout (the same rendering hint, the same layout width),
    ''' so the measured size is the drawn one. <paramref name="maxWidth"/> &gt; 0: GDI+ word wrap at that width (DrawString into a
    ''' rectangle that wide, which clips there), and the bitmap is never wider. Friend for ShadowGate's notice gate.
    ''' <paramref name="textColor"/>: the glyphs' colour (Nothing: gray, the status label's). <paramref name="outline"/>: when given, a
    ''' 1 px ring of that colour around the glyphs (the text drawn at the 8 neighbour offsets first), so the label reads over any
    ''' background - the clear colour or the model under it.</summary>
    Friend Shared Function GenerateTextBitmap(text As String, fontSize As Integer, fontName As String, Optional maxWidth As Integer = 0,
                                              Optional textColor As Color? = Nothing, Optional outline As Color? = Nothing) As Bitmap
        Dim size = MeasureLabel(text, fontSize, fontName, maxWidth)
        Using fnt As New Font(fontName, fontSize, FontStyle.Bold)
            Dim w = CInt(Math.Ceiling(size.Width))
            If maxWidth > 0 Then w = Math.Min(w, maxWidth)
            Dim bmp As New Bitmap(Math.Max(1, w), Math.Max(1, CInt(Math.Ceiling(size.Height))), Imaging.PixelFormat.Format32bppArgb)
            Using g2 As Graphics = Graphics.FromImage(bmp), fore As New SolidBrush(If(textColor, Color.Gray))
                g2.Clear(Color.Transparent)
                g2.TextRenderingHint = Drawing.Text.TextRenderingHint.AntiAliasGridFit
                Dim draw = Sub(b As Brush, dx As Single, dy As Single)
                               If maxWidth > 0 Then
                                   g2.DrawString(text, fnt, b, New RectangleF(dx, dy, maxWidth, size.Height))
                               Else
                                   g2.DrawString(text, fnt, b, dx, dy)
                               End If
                           End Sub
                If outline.HasValue Then
                    Using ring As New SolidBrush(outline.Value)
                        For dy = -1 To 1
                            For dx = -1 To 1
                                If dx <> 0 OrElse dy <> 0 Then draw(ring, dx, dy)
                            Next
                        Next
                    End Using
                End If
                draw(fore, 0, 0)
            End Using
            Return bmp
        End Using
    End Function

    ''' <summary>The frame notice's colours against the clear colour <paramref name="back"/>: by its relative luminance (sRGB
    ''' decoded, Rec. 709 weights), yellow over a dark background and black over a light one, ringed with the other end (black /
    ''' white) - WCAG contrast of yellow on black 19:1, black on white 21:1.</summary>
    Friend Shared Function NoticeColours(back As Color) As (Text As Color, Ring As Color)
        Dim lin = Function(c As Byte) As Double
                      Dim v = c / 255.0
                      Return If(v <= 0.04045, v / 12.92, Math.Pow((v + 0.055) / 1.055, 2.4))
                  End Function
        Dim y = 0.2126 * lin(back.R) + 0.7152 * lin(back.G) + 0.0722 * lin(back.B)
        ' The luminance at which black and white give the same WCAG contrast: (y + 0.05) / 0.05 = 1.05 / (y + 0.05) -> y = 0.179.
        Return If(y > 0.179, (Color.Black, Color.White), (Color.Yellow, Color.Black))
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
    ''' <summary>Programa del quad de fondo (fade radial). Ver <see cref="PaintBackground"/>.</summary>
    Public SharedBackgroundShader As Background_Shader_Class
    ''' <summary>FO4 post pass (ImageSpace HDR i3700 + GammaCorrectLUT i3648) and the two compute passes of
    ''' the mean luminance. See PostProcess.vb.</summary>
    Friend SharedPostFo4Shader As PostProcess_Fo4_Shader_Class
    Friend SharedPostSseShader As PostProcess_Sse_Shader_Class
    Friend SharedLumPartialsShader As Luminance_Partials_Shader_Class
    Friend SharedLumResolveShader As Luminance_Resolve_Shader_Class
    Friend SharedRefractionNormalsSse As Refraction_Normals_Sse_Shader_Class
    Friend SharedRefractionNormalsFo4 As Refraction_Normals_Fo4_Shader_Class
    Friend SharedRefractionImageSpace As Refraction_ImageSpace_Shader_Class
    Friend SharedSseOpaqueComposite As Sse_Opaque_Composite_Shader_Class
    ''' <summary>The six programs of the Skyrim SE SAO (SseSaoSource).</summary>
    Friend SharedSseSao As SseSaoPrograms
    ''' <summary>Skyrim SE's mark / fill program of the translucent decals' base (DecalBaseTarget).</summary>
    Friend SharedSseDecalBaseMark As Shader_Base_Class
    ''' <summary>The Fallout 4 deferred frame's programs (Fo4DeferredPrograms: lights, composite, every G-buffer record).</summary>
    Friend SharedFo4Deferred As Fo4DeferredPrograms
    ''' <summary>The compile or link error that left Skyrim SE / Fallout 4 without their programs (v4 B-rev-05), for the status card.</summary>
    Friend SseProgramError As String, Fo4ProgramError As String
    ''' <summary>VAO VACIO del triangulo de pantalla completa del fondo. El perfil core exige un VAO
    ''' bindeado aunque el shader no lea ningun atributo (los vertices salen de <c>gl_VertexID</c>), asi
    ''' que existe solo para satisfacer esa regla: no tiene VBO ni buffers colgando.</summary>
    Private _bgVao As Integer = 0
    ''' <summary>Programas del pase de profundidad del shadow map. Son el VS de cada juego (el MISMO que
    ''' usa el pase iluminado, sin copia del skinning) + el fragment de alpha-test de
    ''' <see cref="ShadowDepthShaderSource"/>. Ver Shadow_Depth_Shader_Fo4.</summary>
    Public SharedShadowFO4Shader As Shadow_Depth_Shader_Fo4
    Public SharedShadowSSEShader As Shadow_Depth_Shader_SSE
    ''' <summary>Programa del receptor de sombra del suelo. Ver GroundShadowShaderSource.</summary>
    Public SharedGroundShadowShader As Ground_Shadow_Shader_Class
    ''' <summary>El FBO + textura de profundidad. Se crea perezosamente en el primer frame con sombras
    ''' encendidas y se libera en Clean; con la opcion apagada nunca se asigna un byte de GPU.</summary>
    Friend ShadowTarget As ShadowMapTarget
    ''' <summary>El segundo mapa, ANCHO y a media resolucion, que consume unicamente el receptor de suelo.
    ''' Existe para que meter la sombra en el piso no le robe nitidez a la del personaje. Ver
    ''' PreviewModel.RenderShadowPass.</summary>
    Friend GroundShadowTarget As ShadowMapTarget
    ''' <summary>Raised when user toggles GPU/CPU skinning mode. Consumers handle this to rerender with their pipeline.</summary>
    Public Event SkinningModeToggled(sender As PreviewControl)

    ' ===== Modo "elegir color" (OPT-IN, apagado por default) =====
    ' [!] Este control es COMPARTIDO (NPC Manager / Wardrobe Manager / Nif Explorer). Con el modo apagado
    ' -el default y el estado en el que arranca siempre- NO hay un solo cambio de comportamiento: el
    ' boton izquierdo sigue orbitando. Prenderlo es una decision explicita de UN formulario, y apagarlo
    ' es responsabilidad suya (el propio setter restaura el cursor, asi que no queda un cursor de cruz
    ' sin modo).
    Private _colorPickMode As Boolean = False
    ''' <summary>Se traga el arrastre del boton izquierdo hasta que se suelte, aunque el modo picker ya se haya
    ''' apagado en el medio. HACE FALTA porque <c>ColorPicked</c> se levanta DENTRO del MouseDown y el que lo
    ''' escucha suele desarmar el modo ahi mismo (picker de un solo disparo): sin este latch, el primer MouseMove
    ''' del MISMO click ya entra por la rama de orbita y la camara pega un salto.</summary>
    Private _pickSwallowLeftDrag As Boolean = False

    ''' <summary>Cuando esta en True el boton IZQUIERDO deja de orbitar y pasa a MUESTREAR el pixel
    ''' clickeado: se levanta <see cref="ColorPicked"/> con la posicion y el color TAL COMO SE VE (el
    ''' framebuffer ya trae luz, sombra, tonemap y el encode a display - no es el albedo ni el tono
    ''' crudo). El resto de los gestos (rueda, boton del medio, menu contextual) no cambia, asi que el
    ''' usuario puede seguir encuadrando. Setearlo en False restaura el cursor.</summary>
    Public Property ColorPickMode As Boolean
        Get
            Return _colorPickMode
        End Get
        Set(value As Boolean)
            If _colorPickMode = value Then Return
            _colorPickMode = value
            Try
                Me.Cursor = If(value, Cursors.Cross, Cursors.Default)
            Catch
            End Try
        End Set
    End Property

    ''' <summary>Pixel muestreado en modo <see cref="ColorPickMode"/>. X/Y en coordenadas del CONTROL
    ''' (origen arriba-izquierda), Color = el pixel tal como se ve.</summary>
    Public Class ColorPickedEventArgs
        Inherits EventArgs
        Public ReadOnly Property X As Integer
        Public ReadOnly Property Y As Integer
        Public ReadOnly Property Color As Color
        Public Sub New(px As Integer, py As Integer, c As Color)
            _X = px : _Y = py : _Color = c
        End Sub
    End Class

    Public Event ColorPicked As EventHandler(Of ColorPickedEventArgs)
    Public ReadOnly Property CurrentShader As Shader_Base_Class
        Get
            If Config_App.Current.Game = Config_App.Game_Enum.Skyrim AndAlso SharedSSEShader IsNot Nothing Then Return SharedSSEShader
            Return SharedActiveShader
        End Get
    End Property

    ''' <summary>El programa de profundidad del juego activo. Mismo eje que <see cref="CurrentShader"/>:
    ''' el shader elegido ES la fuente de verdad de que juego se esta dibujando, y los dos pases tienen
    ''' que coincidir o el pase de sombra correria un VS con otra convencion de skinning.</summary>
    Friend ReadOnly Property CurrentShadowShader As Shader_Base_Class
        Get
            If Config_App.Current.Game = Config_App.Game_Enum.Skyrim AndAlso SharedShadowSSEShader IsNot Nothing Then Return SharedShadowSSEShader
            Return SharedShadowFO4Shader
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
    ''' <summary>Emulación de <c>BSShader_DefFacegenDetail</c>: el default que el motor bindea al slot DETAIL
    ''' (texture-set slot 3 → material+0xA8 → PS <b>t4</b>) de una cabeza FaceGen cuyo slot 3 está VACÍO. RE
    ''' byte-level de SkyrimSE.exe: la init de defaults 0x140E57E30 crea <c>BSShader_DefFacegenDetail</c> con
    ''' fill <c>0x40404040</c> = 64/255 = 0.251 y la guarda en manager+0x88 (singleton 0x328CC20 ⇒ 0x328CCA8,
    ''' que es justo el default que <c>BSLightingShaderMaterialFacegen</c> slot#10 (0x1414BA8B0) mete en +0xA8).
    ''' El detail NO es el término del soft-light: es el multiplicador AMPLIFICADO
    ''' <c>(detail + (1/255,0,1/255)) × 255/64</c>. Por eso el neutro del engine es 64 (→ ×1.0 exacto en G) y
    ''' por eso 0.251 NO oscurece: da (1.015625, 1.0, 1.015625). Se emula acá para que render == lo que el NIF
    ''' horneado (slot 3 vacío) rinde in-game.</summary>
    Public defaultFacegenDetailTex As Integer
    ' (ELIMINADA `defaultFacegenFoldNeutralDetailTex`, el detail neutro del amplify (63,64,63).) La bindeaba
    ' la rama `SseFoldDetailNeutralized` del render, que era código muerto: con la ley actual el fold deja los
    ' slots 3/6 REALES y pre-compensa la cadena, así que el amplify del engine SIEMPRE debe aplicarse.
    ''' <summary>Emulación de <c>DefaultGreyMap</c>: el default que el motor bindea al slot TINT (texture-set
    ''' slot 6 → material+0xA0 → PS <b>t3</b>) cuando no hay facetint. RE byte-level: init 0x140E57E30 crea
    ''' <c>DefaultGreyMap</c> con fill <c>0x80808080</c> = 0.5 y la guarda en manager+0x70 (= 0x328CC90, el
    ''' default que slot#10 0x1414BA8B0 mete en +0xA0). 0.5 es la IDENTIDAD del soft-light
    ''' (<c>a² + 2·a·0.5·(1−a) = a</c>) ⇒ sin facetint la cara queda con su diffuse crudo, que es exactamente lo
    ''' que hace el motor. Sirve para los DOS casos: slot 6 ausente (unfolded) y slot 6 neutralizado (folded).</summary>
    Public defaultFacegenTintTex As Integer
    ''' <summary>SSE: default del slot 7 (specular mask) cuando la malla es MODELSPACENORMALS y el slot esta
    ''' VACIO = <b>NEGRO</b> (specular 0). El motor nunca cae al alpha del normal en MSN; rellena material+0x68
    ''' con <c>BSShader_DefHeightMap</c> (fill 0xff000000) por la rama <c>skinned &amp;&amp; MSN</c> del
    ''' default-fill 0x1414B7B00. Ver la cadena de evidencia en el bind de texSpecular. FO4 no lo usa (alli el
    ''' `_s` es universal y el gate es su presencia).</summary>
    Public defaultSseMsnSpecTex As Integer
    ''' <summary>SSE: default del slot 7 cuando hay BACK_LIGHTING y el slot esta VACIO = el default GENERICO del
    ''' motor, <c>BSShader_DefNormalMap</c> (init 0x140E57E30, fill <c>0xffff8080</c> = RGBA 128,128,255,255).
    ''' Es la PRIMERA rama del default-fill 0x1414B7B00 y por eso gana sobre la negra. Antes este caso caia en
    ''' blanco (1,1,1) y sumaba translucidez blanca a full por luz. FO4 no lo usa.</summary>
    Public defaultSseEngineGenericTex As Integer
    ''' <summary>NEGRO 4x4 para el diffuse de las HELPER SHAPES sin textura (ver
    ''' <see cref="IRenderableShape.IsHelperShape"/>). Sin esto caian en <c>defaultWhiteTex</c> y una malla
    ''' de colision se veia como una mancha BLANCA que tapaba el modelo. Negro las deja como silueta.
    ''' <para>Textura PROPIA y no <c>defaultSseMsnSpecTex</c> (que tambien es negra): esa tiene una ley
    ''' del motor detras y repurposarla ata dos cosas que no tienen nada que ver.</para>
    ''' <para>Es una decision NUESTRA, no del canonico: BodySlide deja el sin-shader "untextured"
    ''' (ResourceLoader.h:30-31, no le da ni el placeholder NoImg.png).</para></summary>
    Public defaultHelperTex As Integer
    ''' <summary>Default del SUBSURFACE (_sk, texture-set slot 2) de una cabeza FaceGen cuando falta: NEGRO.
    ''' RE byte-level: BSLightingShaderMaterialFacegen slot#10 (0x1414BA8B0) rellena subsurface(+0xB0) con
    ''' DefHeightMap (fill 0xFF000000 = negro); mapeo miembro↔slot verificado en slot#8 (0x1414BA6E0):
    ''' +0xB0↔índice 2 (_sk), +0xA8↔3 (detail), +0xA0↔6 (tint). Negro ⇒ SSS = 0 (sin subsurface glow) —
    ''' distinto del fallback no-facegen del shader (softMask=albedo), que queda intacto.</summary>
    Public defaultFacegenSubsurfaceTex As Integer
    ''' <summary>Fallout 4's DefaultTexture_DissolvePattern, the t15 of the G-buffer's ADDITIONAL_ALPHA_MASK records (read with ld at
    ''' SV_POSITION mod 4): 4x4 R8G8B8A8_UNORM, 1 mip, the same byte in R, G, B and A; row y, column x of
    ''' 00 88 22 AA / CC 44 EE 66 / 33 BB 11 99 / FF 77 DD 55 (Fallout4.exe 1.11.240.0, 0x14182D03D..0x14182D1A1, bytes
    ''' 0x14182D10C..0x14182D15C; bound by SetupGeometry 0x1422072B8..0x1422072D9).</summary>
    Public defaultDissolvePatternTex As Integer
    ''' <summary>Fallout 4's DefaultTexture_DiffuseMap: 1x1 R8G8B8A8_UNORM (128, 0, 255, 255), never promoted to sRGB (creator
    ''' 0x14182C5B0, dword 0xFFFF0080 at 0x14182CBD1; EngineDefaultTextureLaw.Rgba). What the game samples in an sRGB slot (diffuse,
    ''' palette, effect base) that is empty.</summary>
    Public defaultFo4DiffuseMapTex As Integer
    Public Property BrushRadiusPx As Integer = 5
    Public Property InvertMasking As Boolean = False


    ''' <summary>The app's GL texture for a built-in texture of the engine (EngineDefaultTextureLaw). The ones whose bytes the app
    ''' already had are reused (same RGBA: defaultNormalTex, defaultWhiteTex, defaultSseEngineGenericTex, defaultSseMsnSpecTex; every
    ''' texel the same value, so every sample is, whatever the size). 0 for Own, NoTexture, PreviousDraw, NotTraced and the cubes (the
    ''' envmap cube is MaterialData.EnvmapTexturePath's; SSE DefCubeMap = no reflection, bCubemap off).</summary>
    Friend Function EngineDefaultTextureId(t As EngineDefaultTextureLaw.EngineTexture) As Integer
        Select Case t
            Case EngineDefaultTextureLaw.EngineTexture.Fo4DiffuseMap : Return defaultFo4DiffuseMapTex
            Case EngineDefaultTextureLaw.EngineTexture.Fo4NormalMap : Return defaultNormalTex
            Case EngineDefaultTextureLaw.EngineTexture.Fo4White, EngineDefaultTextureLaw.EngineTexture.SseDefaultWhiteMap : Return defaultWhiteTex
            Case EngineDefaultTextureLaw.EngineTexture.SseDefNormalMap : Return defaultSseEngineGenericTex
            Case EngineDefaultTextureLaw.EngineTexture.SseDefHeightMap : Return defaultSseMsnSpecTex
            Case Else : Return 0
        End Select
    End Function

    ''' <summary>Textura 2D uniforme de w×h con el color dado. <paramref name="mipped"/>=False (default):
    ''' Nearest + ClampToEdge, sin mips (comportamiento histórico de defaultWhiteTex/defaultNormalTex).
    ''' <paramref name="mipped"/>=True: mipmaps generados + LINEAR_MIPMAP_LINEAR + REPEAT — igual que una
    ''' textura cargada del pipeline, para defaults que el shader debe samplear idéntico a una real (los
    ''' defaults facegen: detail 0.251 / subsurface negro).</summary>
    Private Shared Function CreateColorTexture(w As Integer, h As Integer, r As Byte, g As Byte, b As Byte, a As Byte,
                                               Optional mipped As Boolean = False) As Integer
        If w <= 0 OrElse h <= 0 Then Throw New ArgumentOutOfRangeException("w/h must be > 0")

        ' Evita overflow en el tamaño del array
        Dim total As Long = CLng(w) * CLng(h) * 4L
        If total > Integer.MaxValue Then Throw New OutOfMemoryException("Texture too large.")

        ' Rellena RGBA
        Dim pixelData(CInt(total) - 1) As Byte
        For i As Integer = 0 To pixelData.Length - 1 Step 4
            pixelData(i + 0) = r
            pixelData(i + 1) = g
            pixelData(i + 2) = b
            pixelData(i + 3) = a
        Next
        Return CreateRgba8Texture(w, h, pixelData, mipped)
    End Function

    ''' <summary>THE upload of a w x h RGBA8 texture from <paramref name="pixelData"/> (texel row 0 first: texelFetch row y = data row y; the
    ''' samplers of CreateColorTexture: Nearest + ClampToEdge without mips, or mipmaps + LINEAR_MIPMAP_LINEAR + REPEAT).</summary>
    Private Shared Function CreateRgba8Texture(w As Integer, h As Integer, pixelData As Byte(), mipped As Boolean) As Integer
        Dim texID As Integer = GL.GenTexture()
        GL.BindTexture(TextureTarget.Texture2D, texID)

        ' El UNPACK_ALIGNMENT se fija para ESTA subida y se restaura al salir. Dejarlo en 1 globalmente
        ' (esto corre en GenerateDefaultTextures/OnLoad) hace que el `Finally` del loader de DDS restaure 1
        ' en vez del default del contexto.
        Dim alineacionPrevia As Integer = 4
        GL.GetInteger(CType(&HCF5, GetPName), alineacionPrevia)   ' GL_UNPACK_ALIGNMENT
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

        GL.PixelStore(PixelStoreParameter.UnpackAlignment, alineacionPrevia)
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

        ' 4×4 normal map por defecto: el neutro de espacio tangente (0.5,0.5,1) → (128,128,255).
        ' El alpha va en 255 porque los DOS consumidores de normalMap.a caen a 1.0 cuando no hay normal
        ' map: 255 es ese mismo neutro. Se cita por CODIGO y no por numero de linea, que se corre solo:
        ' en Shader_Class.vb, `bNormalMap ? normalMap.a : 1.0` aparece EXACTAMENTE dos veces, y esas dos
        ' son los consumidores. Hay una tercera lectura de `normalMap.a`, pero cuelga de un
        ' `else if (bNormalMap)`, o sea que tampoco se alcanza con esta textura bindeada.
        ' MEDIDO: hoy ningún shader llega a leer esta textura. Se bindea sólo cuando normalTextureId=0, y
        ' `bNormalMap` sale de la MISMA condición (SetBool("bNormalMap", normalTextureId <> 0), más abajo),
        ' así que todo sampleo de texNormal queda del lado falso del guard. Antes decía (128,128,128,128),
        ' que contradecía a este mismo comentario; corregirlo dio 0 píxeles de diferencia en ShadowGate
        ' (10/10 frames byte-idénticos), como tenía que dar. Se corrige igual: el día que alguien saque un
        ' guard, el valor que hay acá es el que se lee, y un (128,128,128) decodifica a (0,0,-1).
        defaultNormalTex = CreateColorTexture(4, 4, 128, 128, 255, 255)

        ' default FaceGen detail = BSShader_DefFacegenDetail del motor (byte-exact, ver campo).
        ' Uniforme 64 = 0.251 (NO 0.5/identidad, NO la Bayer 0.1235 de BSShader_DitheringNoise).
        ' Se crea con MIPMAPS + LINEAR + REPEAT (como una textura real cargada, el blankdetailmap real es
        ' 256² con mips) para que el shader la samplee IDÉNTICO a la real y no haya diferencia por estado de
        ' sampler / minificación (un 4×4 Nearest sin mips sampleaba distinto → cabeza más clara de lo debido).
        defaultFacegenDetailTex = CreateColorTexture(64, 64, 64, 64, 64, 255, mipped:=True)

        ' Detail neutro del AMPLIFY (63,64,63) ⇒ (v+off)·255/64 = 1 exacto, para heads PLEGADOS sin slot 3
        ' (ver campo). NO es 0.5 (esa es la identidad del soft-light = slot 6). Mismo 64²+mips que el 0.251.

        ' default FaceGen TINT = DefaultGreyMap del motor (0x80 = 0.5 = soft-light identidad; ver campo).
        ' Mismo 64²+mips que los otros dos para que el sampler no meta diferencia por minificación.
        defaultFacegenTintTex = CreateColorTexture(64, 64, 128, 128, 128, 255, mipped:=True)

        ' SSE: default del slot 7 en mallas MSN sin `_s` = NEGRO (specular 0), ver campo.
        defaultSseMsnSpecTex = CreateColorTexture(64, 64, 0, 0, 0, 255, mipped:=True)

        ' SSE: default GENERICO del slot 7 (rama backlight) = BSShader_DefNormalMap del motor, ver campo.
        defaultSseEngineGenericTex = CreateColorTexture(64, 64, 128, 128, 255, 255, mipped:=True)

        ' Diffuse de las HELPER SHAPES sin textura = NEGRO (ver el campo). Sin mips: son 4x4 planos.
        defaultHelperTex = CreateColorTexture(4, 4, 0, 0, 0, 255, mipped:=False)

        ' 64×64 default FaceGen SUBSURFACE (_sk faltante) = NEGRO (engine: DefHeightMap → SSS=0; ver campo).
        defaultFacegenSubsurfaceTex = CreateColorTexture(64, 64, 0, 0, 0, 255, mipped:=True)

        ' FO4 DefaultTexture_DissolvePattern (see the field). The engine's row y is texel row y of the D3D texture; ld addresses it
        ' by SV_POSITION (rows from the top) and GL texelFetch by the same integer coordinates: row y is uploaded as GL row y.
        Dim dissolve As Byte() = {&H0, &H88, &H22, &HAA, &HCC, &H44, &HEE, &H66, &H33, &HBB, &H11, &H99, &HFF, &H77, &HDD, &H55}
        Dim dissolveRgba(dissolve.Length * 4 - 1) As Byte
        For i = 0 To dissolve.Length - 1
            For c = 0 To 3 : dissolveRgba(i * 4 + c) = dissolve(i) : Next
        Next
        defaultDissolvePatternTex = CreateRgba8Texture(4, 4, dissolveRgba, mipped:=False)

        ' FO4 DefaultTexture_DiffuseMap (see the field): the engine's size, 1 mip, UNORM; the bytes from the one table of the law.
        defaultFo4DiffuseMapTex = CreateRgba8Texture(1, 1, EngineDefaultTextureLaw.Rgba(EngineDefaultTextureLaw.EngineTexture.Fo4DiffuseMap), mipped:=False)

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
        ' Este loop NO fija UNPACK_ALIGNMENT y venia viviendo del 1 que dejaba `CreateColorTexture` (que
        ' lo ponia y no lo devolvia). Al arreglar aquello, acá queda el 4 por default — y funciona igual de
        ' pura casualidad: 4 px × RGBA son 16 bytes por fila, multiplo de 4. Con un ancho o un formato que
        ' no cierre en 4 bytes, las caras saldrian corridas. Se fija explicito para no depender de eso.
        ' Y SE CAPTURA EL PREVIO, no se asume el default. Doce lineas mas arriba `CreateColorTexture` acaba
        ' de documentar por que asumirlo esta mal, y esta funcion lo restauraba al literal 4: si el llamador
        ' entraba con 1, 2 u 8, salia con 4. Es la misma trampa, re-sembrada en el archivo que la sacaba.
        Dim alineacionPreviaCube As Integer = 4
        GL.GetInteger(CType(&HCF5, GetPName), alineacionPreviaCube)   ' GL_UNPACK_ALIGNMENT
        GL.PixelStore(PixelStoreParameter.UnpackAlignment, 1)
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
        GL.PixelStore(PixelStoreParameter.UnpackAlignment, alineacionPreviaCube)   ' el que habia, no el default

        ' Filtros y wrap para cubemap
        GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMinFilter, CInt(TextureMinFilter.Linear))
        GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMagFilter, CInt(TextureMagFilter.Linear))
        GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapS, CInt(TextureWrapMode.ClampToEdge))
        GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapT, CInt(TextureWrapMode.ClampToEdge))
        GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapR, CInt(TextureWrapMode.ClampToEdge))

        GL.BindTexture(TextureTarget.TextureCubeMap, 0)
    End Sub


    ' Sin consumidor fuera de este ensamblado (única referencia: su propia declaración).
    Friend Class VerticesAffectedEventArgs
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
        ' The card is display-space UI: it goes to the DISPLAY target (also when called in the middle of a
        ' frame whose scene target is the HDR one), and is presented from there like any frame.
        Dim offscreen = _targets.Ensure(Me.Width, Me.Height)
        If offscreen Then
            _targets.BindDisplay()
        Else
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0)
            GL.Viewport(0, 0, Me.Width, Me.Height)
        End If
        GL.ClearColor(Config_App.Current.Setting_BackColor)
        GL.Clear(ClearBufferMask.ColorBufferBit Or ClearBufferMask.DepthBufferBit)
        PaintBackground()
        If Not IsNothing(overlay) Then
            overlay.SetText(Texto)
            overlay.RenderCentered(Me.Width, Me.Height)
        End If
        If offscreen Then _targets.Present()
        _statusDrawnInFrame = True
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
        i.BaseGeometryProvider = Nothing
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
        i.BaseGeometryProvider = request.BaseGeometryProvider
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

    ''' <summary>Los ajustes que gobiernan la GEOMETRIA, tal como los uso la ULTIMA recarga completa.
    ''' <para>SE SELLA EN LA RECARGA, no en el diálogo. Sellarlo dentro de
    ''' <see cref="ApplyRenderSettingsFromConfig"/> se traga el PRIMER cambio de skinning de la sesion —sin
    ''' valor previo no hay con que comparar— y el gesto mas obvio, abrir el dialogo y destildar GPU skinning,
    ''' es justo el que no avisa, dejando la cara oscura. Sellando lo que uso el ultimo frame reconstruido, la
    ''' comparacion es contra lo que hay EN PANTALLA y no depende de por donde se haya tocado la config (el
    ''' menu de la camara tambien la escribe).</para></summary>
    Private Structure AjustesDeGeometria
        Public Gpu As Boolean
        Public Recalc As Boolean
        Public SingleBone As Boolean
        Public Tbn As RecalcTBN.TBNOptions
    End Structure

    Private _geomAplicada As AjustesDeGeometria?

    Private Shared Function LeerGeomDeConfig() As AjustesDeGeometria
        Return New AjustesDeGeometria With {
            .Gpu = Config_App.Current.Setting_GPUSkinning,
            .Recalc = Config_App.Current.Setting_RecalculateNormals,
            .SingleBone = Config_App.Current.Setting_SingleBoneSkinning,
            .Tbn = Config_App.Current.Setting_TBN}
    End Function

    ''' <summary>Empuja al PreviewModel VIVO los ajustes de render de Config_App y re-corre el pipeline.
    '''
    ''' <para>Existe porque esos ajustes NO se leen de Config_App en el camino de dibujo: viven duplicados
    ''' como estado del modelo (<c>Model.RecalculateNormals</c>, <c>Model.SingleBoneSkinning</c>) y del
    ''' Floor (Enabled/Size/StepSize/Color). Cambiar la config sin empujarlos no hace nada visible, y el
    ''' usuario ve una casilla que "no funciona".</para>
    '''
    ''' <para>Vive ACA y no en cada app: la libreria y la config son compartidas por las dos, y una copia por
    ''' app se vuelve dos.</para>
    '''
    ''' <para>Marca <c>Force</c> —que es una RECARGA COMPLETA: Clean, esqueleto, LoadShapesParallel,
    ''' TBN, welding, morphs y subida a GPU— SOLO si cambio algo que la geometria mira. Marcarlo siempre sale
    ''' carisimo: el diálogo escribe en cada <c>ValueChanged</c>, asi que tipear "500" en el tamano del piso
    ''' costaria TRES recargas completas de un NPC con outfit. La camara y la grilla no tocan geometria.
    ''' Tampoco se llama <c>Floor.Rebuild()</c> de prepo: recrea VAO/VBO y arma ~1000 floats.</para>
    ''' <para>No marca <c>Camera</c>: mover la camara del usuario sin que lo pida es una molestia.</para>
    ''' </summary>
    Public Sub ApplyRenderSettingsFromConfig()
        If _isTearingDown OrElse Me.IsDisposed Then Exit Sub
        Dim m = Me.Model
        If m Is Nothing Then Exit Sub

        m.RecalculateNormals = Config_App.Current.Setting_RecalculateNormals
        m.SingleBoneSkinning = Config_App.Current.Setting_SingleBoneSkinning
        ' EL ESPEJO SON TRES, NO DOS: Config -> Model -> Intent. `Model.RecalculateNormals` lo lee la
        ' EXTRACCION de geometria (ExtractSkinnedGeometry) y `Intent.RecalculateNormals` lo lee el paso de
        ' MORPHS (ApplyMorphPlan). Empujar solo el Model dejaba la casilla a medias: en una escena sin
        ' morphs se veia el cambio —el full reload re-extrae— y en Wardrobe Manager, donde el cuerpo SIEMPRE
        ' esta morfeado por el preset, no se veia NADA. Ese era el sintoma reportado: la casilla de la barra
        ' principal funcionaba y la de este dialogo no, porque la de la barra pasa por Update_Render, que
        ' refresca el Intent (`intent.RecalculateNormals = ctrl.Model.RecalculateNormals`), y este camino no.
        ' Una sonda que llame a ESTA funcion sobre un NIF sin morphs NO lo caza: da pixeles igual. El
        ' chequeo que discrimina es el INVARIANTE de abajo (los tres espejos de acuerdo), no un conteo.
        Intent.RecalculateNormals = Config_App.Current.Setting_RecalculateNormals

        Dim ahora = LeerGeomDeConfig()
        ' Sin recarga previa no hay nada en pantalla que corregir, y la que venga ya va a usar los valores
        ' nuevos: ni evento ni Force.
        Dim geomCambio As Boolean = _geomAplicada.HasValue AndAlso Not _geomAplicada.Value.Equals(ahora)
        Dim cambioSkinning As Boolean = _geomAplicada.HasValue AndAlso _geomAplicada.Value.Gpu <> ahora.Gpu

        ' EL CAMBIO DE SKINNING TIENE QUE AVISAR, no alcanza con ensuciar la geometria. La libreria
        ' re-corre la GEOMETRIA y nada mas; el diffuse plegado se queda pegado en el diccionario de
        ' texturas mientras el MaterialData nuevo pierde su estado per-mesh (SkinToneBaked,
        ' FaceTintOverlay_ID) y la cara sale OSCURA. FO4_NPC_Manager engancha SkinningModeToggled justo
        ' para re-armar su hook de post-texture-upload; sin el evento no se arma nada. Es el mismo modo de
        ' falla que ya cerraba HookSkinningToggleRefresh para el menu contextual de la camara — este
        ' camino nuevo, el de la pestana Rendering del dialogo compartido, lo habia reabierto.
        If cambioSkinning Then RaiseEvent SkinningModeToggled(Me)

        If m.Floor IsNot Nothing Then
            Dim g = Config_App.Current.Settings_RenderGrid
            Dim col = Config_App.Current.RenderGridColor()
            m.Floor.Enabled = g.Enabled
            m.Floor.Size = CSng(g.Size)
            m.Floor.StepSize = CSng(g.StepSize)
            m.Floor.Color = col
        End If

        If geomCambio Then
            Intent.MarkDirty(RenderDirtyFlags.Force)
            InvalidateRender()
        Else
            ' REPINTAR SIEMPRE, no solo cuando cambio el piso. Setting_DrawHiddenSegments se lee en el
            ' camino de dibujo detras de un gate de sucio, asi que le alcanza un repaint — pero sin este
            ' Else no habia ninguno y la casilla no mostraba nada hasta el latido de seguridad de ~1 s.
            ' El boton "Apply to rendered project" que esto reemplaza era inmediato.
            UpdateRequired = True
        End If
    End Sub

    ''' <summary>
    ''' Signal that the render intent has pending work and execute the pipeline immediately.
    ''' If called multiple times between frames, dirty flags accumulate via OR before execution.
    ''' </summary>
    Public Sub InvalidateRender()
        ExecuteRenderPipeline()
    End Sub

    ''' <summary>
    ''' Deja el control SIN nada que dibujar y con <paramref name="statusText"/> en pantalla.
    ''' No alcanza con llamar a <see cref="Processing_Status"/>: ese cartel es un frame suelto y
    ''' NO toca el modelo, así que las mallas anteriores siguen vivas con <c>Can_Render=True</c> y
    ''' el primer repaint que llegue (el heartbeat de seguridad de ~1 s del Tick, un resize, el
    ''' mouse) vuelve a dibujar el contenido VIEJO encima del cartel. Este camino pasa por el
    ''' pipeline vacío, que limpia mallas/texturas, frena el RenderTimer y recién ahí pinta el
    ''' texto — por eso el cartel queda.
    ''' </summary>
    Public Sub ClearRender(Optional statusText As String = "Empty")
        Intent.Shapes = Nothing
        Intent.EmptyStatusText = statusText
        Intent.MarkDirty(RenderDirtyFlags.Shapes)
        InvalidateRender()
    End Sub

    ''' <summary>Hace el contexto GL current SOLO si no lo está ya. MakeCurrent() (cambio de
    ''' contexto) es caro aunque el contexto ya sea el current; llamarlo por-mesh por-buffer en el
    ''' loop de upload (UpdateSkinBuffers_GL + UpdateBoneMatricesSSBO) cuesta. El guard con
    ''' Context.IsCurrent es 100% equivalente (el contexto queda current igual) y evita el switch
    ''' redundante. Fallback a MakeCurrent si IsCurrent falla → peor caso = comportamiento actual.</summary>
    ''' <returns>True si el contexto quedó current. DEVUELVE Boolean porque los llamadores que
    ''' BORRAN handles necesitan saberlo: los nombres de GL son por contexto y borrar creyendo que se hizo
    ''' current, cuando no se pudo, mata texturas de OTRO preview.</returns>
    ''' <remarks>`MakeCurrent()` estaba FUERA del `Try`: el `Catch` sólo cubría el chequeo de
    ''' `IsCurrent`. Sobre un control ya dispuesto —el caso normal en un teardown— tiraba
    ''' `ObjectDisposedException('PreviewControl')` y escapaba. Ahora todo el cuerpo está protegido y el
    ''' control dispuesto se responde con False, que es la verdad: no hay contexto que hacer current.</remarks>
    Public Function EnsureContextCurrent() As Boolean
        Try
            If IsDisposed OrElse Disposing Then Return False
            If Context IsNot Nothing AndAlso Context.IsCurrent Then Return True
            MakeCurrent()
            Return True
        Catch
            Return False
        End Try
    End Function

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
            ' El sello describe lo que hay EN PANTALLA. Con la escena vacia no hay nada que corregir, y
            ' dejarlo con valor hacia que el siguiente cambio de skinning levantara SkinningModeToggled
            ' contra un modelo recien limpiado (en NPC Manager eso re-arma el hook de post-texture-upload).
            _geomAplicada = Nothing
            Model.Processing_Status_GL(If(String.IsNullOrEmpty(intent.EmptyStatusText), "Empty", intent.EmptyStatusText))
            intent.ClearDirty()
            Return
        End If

        Dim flags = intent.DirtyFlags
        Dim needsFullReload = (flags And (RenderDirtyFlags.Shapes Or RenderDirtyFlags.Force)) <> 0
        ' Sellar ANTES de recargar: lo que la recarga esta por usar es, desde ya, lo que va a estar en
        ' pantalla. Ver AjustesDeGeometria.
        If needsFullReload Then _geomAplicada = LeerGeomDeConfig()
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
            ' Las UVs se suben SIEMPRE, tambien en GPU-skinning: ahi UpdateSkinBuffers_GL no corre
            ' (el shader skinnea del SSBO) pero un slider uv igual movio Uvs_Weight. Es no-op si el
            ' flag esta apagado, que es el caso de todo modelo sin sliders uv.
            For Each mesh In Model.meshes
                mesh.UpdateUvBuffer_GL()
            Next
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
            ' Solo se re-prepara el skeleton (clear + reinject de cloth bones) si CAMBIO el shape set: en
            ' pose-only durante animacion el set es estable, asi que los bones inyectados del ultimo prepare
            ' siguen vivos (ApplyPose no los toca) y se saltea el churn por frame, caro en WM con fisica.
            ' [RENDER-MS] INSTRUMENTACION DE FASES - TODA gateada por Logger.Enabled.
            ' `LogLazy` hace lazy el STRING, no el CALCULO: sin este gate explicito corren en release DOS
            ' Stopwatch por malla por frame mas ~9 lecturas de .Elapsed. El flag se toma UNA vez por frame
            ' para que el gate no cambie a mitad.
            Dim _instr As Boolean = Logger.Enabled
            ' [RENDER-MS] período REAL entre pose-updates (= 1000/fps efectivo). vs total = trabajo.
            Dim _periodMs As Double = 0
            If _instr Then
                _periodMs = _posePeriodSw.Elapsed.TotalMilliseconds
                _posePeriodSw.Restart()
            End If
            Dim _sw As System.Diagnostics.Stopwatch = If(_instr, System.Diagnostics.Stopwatch.StartNew(), Nothing)
            If Not ReferenceEquals(_skeletonPreparedForShapes, intent.Shapes) Then
                PipelineStep_Skeleton(intent)
                _skeletonPreparedForShapes = intent.Shapes
            End If
            ' If MULTILINEA a proposito en todos los laps: con `If _instr Then a : b` el `:` deja las dos
            ' sentencias dentro del Then (semantica correcta de VB), pero si alguna vez alguien reformatea
            ' eso mal, `_sw.Restart()` corre con `_sw = Nothing` ⇒ NullReference en CADA frame de release.
            ' No vale la pena ahorrar dos lineas a cambio de ese riesgo.
            Dim _msSkel As Double = 0
            If _instr Then
                _msSkel = _sw.Elapsed.TotalMilliseconds
                _sw.Restart()
            End If

            ' Dirty-mesh list — only for shapes the caller marked dirty.
            ' Empty DirtyShapes (default) means "all shapes" (back-compat single-actor flow).
            ' Se computa UNA vez por frame y se reusa: PipelineStep_Morphs (abajo) y las dos
            ' pasadas de skinning leen la misma lista (mismo predicado, mismo orden).
            Dim dirtyMeshes = Model.meshes.Where(Function(m) intent.IsShapeDirty(m.MeshData.Shape)).ToList()

            ' Morphs (if also dirty — preset+pose changed simultaneously)
            If needsMorphUpdate Then
                PipelineStep_Morphs(intent, dirtyMeshes)
            End If
            Dim _msMorph As Double = 0
            If _instr Then
                _msMorph = _sw.Elapsed.TotalMilliseconds
                _sw.Restart()
            End If

            ' Recompute bone matrices + GPU upload.
            ' Two-pass split (mismo patrón que LoadShapesParallel → Setup_GL):
            '   Pasada 1 = CPU puro (sin GL) → paralela sobre los meshes dirty.
            '   Pasada 2 = GL (MakeCurrent + BufferSubData) → serial en el hilo del contexto.
            Dim cpuSkinMode As Boolean = Not Config_App.Current.Setting_GPUSkinning
            Dim playingNow As Boolean = PlayingAnimation
            ' CON SOMBRAS ENCENDIDAS LOS BOUNDS EXACTOS SE RECALCULAN TAMBIEN EN PLAY. Sin sombras, la caja de recorte en play es la
            ' caja por hueso (ComputeBoundsFromBoneBoxes, O(huesos), conservadora: chunk C8 D-L6); ShadowMapMath.Fit encuadra el ortho
            ' sobre ESE AABB y la pasada 1 del skinning si corre por frame, asi que con la caja congelada un brazo
            ' levantado sale del encuadre, no escribe silueta, y el receptor lee borde blanco = "iluminado".
            ' El margen de la esfera envolvente no alcanza (~7,5 u sobre la cabeza en un cuerpo de 60x40x180).
            ' Costo medido de la pasada O(vertices): 1,5 ms/frame sobre 11 mallas / 37.321 vertices.
            Dim sombrasEncendidas As Boolean = Config_App.Current.ActiveShadows().Enabled
            Dim computeBoundsThisFrame As Boolean = (Not playingNow) OrElse needsMorphUpdate OrElse sombrasEncendidas

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
            Next

            ' ===================== FÍSICA HAVOK (opt-in, apagada por defecto) =====================
            ' EL LUGAR es éste y no otro: la app ya aplicó pose + morph + mount (capas Delta/Morph/Mount)
            ' y la cache de transforms globales TODAVÍA no se construyó. La física lee el esqueleto ya
            ' posado y escribe la capa PhysicsDeltaTransform, que la cache de abajo tiene que ver.
            ' Con el interruptor apagado esto limpia la capa y sale: el render queda bit-idéntico.
            ' Ver FO4_Base_Library/Havok/Physics/ClothCanonico.vb.
            ' ⛔ El código entra en todas las configuraciones; en Release `ApplyHavokPhysicsSettings`
            ' deja `Enabled = False` y el `If` de abajo no entra.
            Try
                ' La config es la PERSISTENCIA del interruptor y del modo; el módulo estático es donde se
                ' leen. Volcarla acá hace que cambiar `Setting_HavokPhysics` en config.json tenga efecto
                ' sin que las apps tengan que acordarse de propagarlo.
                Config_App.Current?.ApplyHavokPhysicsSettings()
                ' ⛔ CON LA FISICA APAGADA NO SE PAGA NADA. `StepShapes` ya sale temprano, pero el
                ' diccionario de abajo se armaba IGUAL todos los frames — una asignacion y una pasada
                ' sobre los dirty meshes por frame para despues no usarla. La perilla viene apagada por
                ' defecto, asi que ese era el caso NORMAL.
                If Havok.Physics.HavokPhysicsSettings.Enabled AndAlso
                   Havok.Physics.HavokPhysicsSettings.Mode <> Havok.Physics.HavokPhysicsMode.Off Then
                    Dim physByInstance As New Dictionary(Of SkeletonInstance, List(Of IRenderableShape))
                    For Each mesh In dirtyMeshes
                        Dim inst As SkeletonInstance = If(resolvedSkels(mesh), SkeletonInstance.Default)
                        If inst Is Nothing Then Continue For
                        Dim lst As List(Of IRenderableShape) = Nothing
                        If Not physByInstance.TryGetValue(inst, lst) Then
                            lst = New List(Of IRenderableShape)
                            physByInstance(inst) = lst
                        End If
                        lst.Add(mesh.MeshData.Shape)
                    Next
                    ' ⛔ UNA llamada por render con TODOS los esqueletos: el reloj de la tela avanza un
                    ' cuadro por render, no uno por esqueleto.
                    ' La cámara: la distancia² del LOD de la tela (`0x140DB329F` lee la de la PlayerCamera).
                    Dim ojo = camera.GetEyePosition()
                    Havok.Physics.ClothCanonico.StepShapes(physByInstance, Havok.Physics.HavokPhysicsSettings.MilisegundosDelCuadro,
                                                           camara:=New System.Numerics.Vector3(ojo.X, ojo.Y, ojo.Z))
                End If
            Catch exPhys As Exception
                Dim exL = exPhys
                Logger.LogLazy(Function() $"[HAVOK-PHYS] el paso de física falló y se omite este frame: {exL.GetType().Name}: {exL.Message}")
            End Try

            For Each mesh In dirtyMeshes
                Dim inst As SkeletonInstance = If(resolvedSkels(mesh), SkeletonInstance.Default)
                If inst IsNot Nothing AndAlso Not globalCaches.ContainsKey(inst) Then
                    globalCaches(inst) = inst.BuildGlobalTransformCacheForRenderPass()
                End If
            Next
            Dim _msCache As Double = 0
            If _instr Then
                _msCache = _sw.Elapsed.TotalMilliseconds
                _sw.Restart()
            End If

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
                    ' Option B (GPU y CPU). Pasada 3 (world-cache/bounds): solo fuera de play. El orden de transparentes ya no la lee
                    ' (O-PT: EngineWorldBound, del global de los huesos; chunk C8), asi que en play ninguna malla la necesita; la caja de
                    ' recorte en play la da la caja por hueso (D-L6). Pasada 2 (PerVertexSkinMatrix): en CPU-skin la necesita el display.
                    ' Pasada 1 (matrices -> SSBO) corre siempre dentro de Recompute.
                    ' ACA NO VA `OrElse sombrasEncendidas`, aunque parezca que si. Lo tuvo un rato, con el
                    ' argumento de "computar el cache de mundo eager para que ComputeBounds sea un min/max
                    ' barato". Es falso: RecomputeGPUBoneMatrices invalida el cache SIEMPRE (SkinningHelper,
                    ' InvalidateWorldCache) y despues, con updateWorldCache=True, llama a ComputeWorldBounds,
                    ' que entra por GetWorldVertices y dispara ComputeWorldSpaceCache lo mismo. O sea que la
                    ' pasada cara corre en los dos casos; lo unico que agregaba el OrElse era un SEGUNDO
                    ' recorrido O(vertices) de min/max y una segunda escritura de Minv/Maxv pisando la
                    ' primera. Lo que si cierra el defecto de la sombra es `computeBoundsThisFrame` mas
                    ' arriba: GetSceneBounds lee Minv/Maxv, y quien los escribe es mesh.ComputeBounds.
                    Dim updateWorldCache As Boolean = Not playingNow
                    Dim updatePerVertexSkin As Boolean = cpuSkinMode OrElse updateWorldCache
                    ' Pose is implicit in the SkeletonInstance: the caller applied it via ApplyPose.
                    SkinningHelper.RecomputeGPUBoneMatrices(
                        mesh.MeshData.Shape, mesh.MeshData.Meshgeometry,
                        Model.SingleBoneSkinning, meshSkel, updateWorldCache, updatePerVertexSkin, meshGlobalCache)

                    If cpuSkinMode Then
                        ' ESTA ES LA LINEA CALIENTE del conjunto de sucios: corre por frame y por malla
                        ' mientras dura la animacion. Construir aca un `HashSet(Of Integer)` con
                        ' `Enumerable.Range(0, n)` costaba 1,16 ms/frame MEDIDOS sobre el Serena Battle
                        ' Suit (130.500 vertices en 26 mallas) —el 10 % del frame CPU— para armar un
                        ' conjunto cuyo contenido despues NO se lee: con todos sucios la subida es
                        ' completa y solo se mira `.Count`. Ver ConjuntoDeSucios.
                        mesh.MeshData.Meshgeometry.dirtyVertexIndices.MarcarTodos(
                            mesh.MeshData.Meshgeometry.Vertices.Length)
                        Array.Fill(mesh.MeshData.Meshgeometry.dirtyVertexFlags, True)
                    End If

                    If computeBoundsThisFrame Then mesh.ComputeBounds() Else mesh.ComputeBoundsFromBoneBoxes()
                End Sub)
            Dim _msPass1 As Double = 0
            If _instr Then
                _msPass1 = _sw.Elapsed.TotalMilliseconds
                _sw.Restart()
            End If

            ' --- Pasada 2: GL (serial) ----------------------------------------------------
            ' Timer separado en 3: skinCompute (world-transform + invert 3×3/vértice) + skinUpload (4
            ' BufferSubData/mesh) los acumula UpdateSkinBuffers_GL en _skinComputeMs/_skinUploadMs; ssbo
            ' (matrices de hueso — desperdicio en CPU-skin) es el segundo loop. Loops separados = mismo
            ' resultado (cada uno escribe buffers independientes por mesh).
            Dim _gc0Before As Integer = 0
            If _instr Then
                _skinComputeMs = 0 : _skinUploadMs = 0 : _skinDirtyMs = 0 : _skinCtxMs = 0 : _skinBoundsMs = 0 : _skinMaskMs = 0
                _gc0Before = GC.CollectionCount(0)   ' Gen0 GCs durante el loop skin (los arrays alocan ~28MB/frame)
            End If
            Dim _skinFuncMs As Double = 0            ' tiempo de la función entera (vs el wall del loop = overhead/GC entre meshes)
            ' EL LOOP TENIA UN `Stopwatch.StartNew()` POR MALLA, sin gate, y `_skinFuncMs` no lo lee nadie
            ' salvo el [RENDER-MS]. Con el flag apagado ahora el loop es el loop pelado.
            If _instr Then
                For Each mesh In dirtyMeshes
                    Dim _swM = System.Diagnostics.Stopwatch.StartNew()
                    mesh.UpdateSkinBuffers_GL(recomputeBounds:=False)   ' pose path: bounds los maneja la línea gateada del pass 1
                    _skinFuncMs += _swM.Elapsed.TotalMilliseconds
                Next
            Else
                For Each mesh In dirtyMeshes
                    mesh.UpdateSkinBuffers_GL(recomputeBounds:=False)
                Next
            End If
            Dim _msSkin As Double = 0
            Dim _gc0 As Integer = 0
            If _instr Then
                _msSkin = _sw.Elapsed.TotalMilliseconds
                _sw.Restart()
                _gc0 = GC.CollectionCount(0) - _gc0Before
            End If
            For Each mesh In dirtyMeshes
                mesh.UpdateBoneMatricesSSBO()
            Next
            Dim _msSsbo As Double = 0
            If _instr Then
                _msSsbo = _sw.Elapsed.TotalMilliseconds
                _sw.Restart()
            End If

            If needsMorphUpdate Then
                Model.MarkRenderBucketsDirty()
            End If
            If needsCameraReset AndAlso allowCameraReset Then ResetCamera()
            RefreshRender()
            ' present solo es síncrono (y por lo tanto medible aquí) en PlayingAnimation; en scrub
            ' RefreshRender solo hace Invalidate (el draw real es diferido a OnPaint) → ~0 acá.
            Dim _msPresent As Double = 0
            If _instr Then _msPresent = _sw.Elapsed.TotalMilliseconds
            Dim _scMs As Double = _skinComputeMs : Dim _suMs As Double = _skinUploadMs : Dim _sdMs As Double = _skinDirtyMs   ' snapshot p/ el closure
            Dim _sfMs As Double = _skinFuncMs : Dim _gc0n As Integer = _gc0 : Dim _sctxMs As Double = _skinCtxMs
            Dim _sbMs As Double = _skinBoundsMs : Dim _smMs As Double = _skinMaskMs
            If _instr Then
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

        ' Geometría BASE pre-skin (opcional; Nothing = base del NIF, comportamiento de siempre).
        ' EN SERIE y ANTES del Parallel.ForEach a propósito: el provider puede necesitar estado
        ' compartido por actor (cachés de .tri, esqueletos) y así no hay que blindarlo para
        ' concurrencia. Es el ÚNICO chokepoint: los tres caminos del pipeline (full reload,
        ' pose+morphs, morph-only) pasan por acá, así que no hay camino que lo saltee.
        ' Ver IBaseGeometryProvider para el contrato (in-place, absoluto, nunca lee geom.Vertices).
        If intent.BaseGeometryProvider IsNot Nothing Then
            For Each mesh In dirtyMeshes
                Try
                    intent.BaseGeometryProvider.TryProvideBaseGeometry(mesh.MeshData.Shape, mesh.MeshData.Meshgeometry)
                Catch ex As Exception
                    ' Un provider que falla degrada a la base del NIF; nunca tumba el render. Pero se
                    ' LOGUEA: el sintoma es visual y silencioso (malla sin hornear) y sin esto no queda
                    ' rastro para diagnosticarlo.
                    Dim shpLog = mesh?.MeshData?.Shape?.ShapeName
                    Dim exLog = ex
                    Logger.LogLazy(Function() $"[BASEGEOM] provider fallo en '{shpLog}': {exLog.GetType().Name}: {exLog.Message}")
                End Try
            Next
        End If

        Parallel.ForEach(dirtyMeshes,
            Sub(mesh)
                Dim plan As MorphPlan = Nothing
                If intent.MorphResolver IsNot Nothing Then
                    plan = intent.MorphResolver.ResolveMorphPlan(mesh.MeshData.Shape, mesh.MeshData.Meshgeometry)
                End If
                MorphEngine.ApplyMorphPlan(
                    mesh.MeshData.Meshgeometry, plan,
                    mesh.MeshData.Shape,
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
        SharedBackgroundShader = New Background_Shader_Class
        SharedPostFo4Shader = New PostProcess_Fo4_Shader_Class
        SharedPostSseShader = New PostProcess_Sse_Shader_Class
        SharedLumPartialsShader = New Luminance_Partials_Shader_Class
        SharedLumResolveShader = New Luminance_Resolve_Shader_Class
        ' rev-50 / v4 B-rev-05: a program set that does not compile or link leaves ITS game without a frame (the status card names
        ' the error, FrameUnavailable), never the control - and the other game - dead. The refraction image-space pass runs in both.
        Try
            SharedRefractionImageSpace = New Refraction_ImageSpace_Shader_Class
        Catch ex As Exception
            SharedRefractionImageSpace = Nothing
            SseProgramError = ex.Message : Fo4ProgramError = ex.Message
        End Try
        Try
            SharedRefractionNormalsSse = New Refraction_Normals_Sse_Shader_Class
            SharedSseOpaqueComposite = New Sse_Opaque_Composite_Shader_Class
            SharedSseSao = New SseSaoPrograms()
            SharedSseDecalBaseMark = New DecalBase_Mark_Shader_Class
        Catch ex As Exception
            SharedRefractionNormalsSse?.Dispose() : SharedRefractionNormalsSse = Nothing
            SharedSseOpaqueComposite?.Dispose() : SharedSseOpaqueComposite = Nothing
            SharedSseSao?.Dispose() : SharedSseSao = Nothing
            SharedSseDecalBaseMark?.Dispose() : SharedSseDecalBaseMark = Nothing
            SseProgramError = If(SseProgramError, ex.Message)
        End Try
        Try
            SharedRefractionNormalsFo4 = New Refraction_Normals_Fo4_Shader_Class
            SharedFo4Deferred = New Fo4DeferredPrograms()
        Catch ex As Exception
            SharedRefractionNormalsFo4?.Dispose() : SharedRefractionNormalsFo4 = Nothing
            SharedFo4Deferred = Nothing
            Fo4ProgramError = If(Fo4ProgramError, ex.Message)
        End Try
        _bgVao = GL.GenVertexArray()
        ' Los dos programas de profundidad se compilan SIEMPRE, aunque las sombras esten apagadas: el
        ' costo es un link por juego al abrir el control, y tenerlos condicionados al setting significaria
        ' compilar GLSL en medio de un frame la primera vez que alguien prende la opcion.
        SharedShadowFO4Shader = New Shadow_Depth_Shader_Fo4
        SharedShadowSSEShader = New Shadow_Depth_Shader_SSE
        SharedGroundShadowShader = New Ground_Shadow_Shader_Class
        ' Cube filtering ACROSS faces, like every D3D10+ cube sample of both engines (GL defaults to per-face).
        GL.Enable(EnableCap.TextureCubeMapSeamless)

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
    ' Friend y no Private: RenderShadowPass los usa para restaurar el viewport sin un glGet por frame.
    Friend lastW As Integer = -1
    Friend lastH As Integer = -1
    Protected Overrides Sub OnResize(e As EventArgs)
        If Me.IsInDesignMode Then Return
        MyBase.OnResize(e)
        ApplyResize(False)
    End Sub
    ''' <summary>Aplica el tamaño del control al viewport GL y recalcula la proyección.
    '''
    ''' <para>⛔⛔ <b>SI NO SE PUDO HACER CURRENT EL CONTEXTO PROPIO, NO SE TOCA <c>GL.Viewport</c>.</b>
    ''' Acá se llamaba a <c>EnsureContextCurrent()</c> IGNORANDO lo que devuelve — y ese Boolean existe
    ''' justamente para esto (ver su doc: «los nombres de GL son por contexto»). Un <c>PreviewControl</c>
    ''' todavía sin handle —el caso de un editor que lo crea en el CONSTRUCTOR, antes de que el formulario
    ''' exista— no tiene contexto que hacer current, así que el <c>GL.Viewport</c> se aplicaba al contexto
    ''' del preview que SÍ estaba current (típicamente el del MainForm) con el tamaño del panel del OTRO
    ''' control. El síntoma que reportó el usuario: el preview principal quedaba rasterizando en un
    ''' rectángulo chico y el cartel «Loading…» salía encogido.</para>
    '''
    ''' <para>⛔ Y <c>lastW/lastH</c> TAMPOCO se sellan en ese caso: sellarlos dejaba el control creyendo
    ''' que ya publicó un viewport que nunca publicó, y como el <c>If</c> de abajo compara contra ellos,
    ''' el <c>GL.Viewport</c> correcto no se emitía NUNCA más (salvo un <c>Force</c>). Dejándolos como
    ''' estaban, el próximo <c>OnResize</c> —o el <c>ApplyResize(True)</c> del <c>OnLoad</c>, que corre
    ''' cuando el contexto ya existe— reintenta.</para>
    '''
    ''' <para>⛔ <c>UpdateProjection</c> SÍ corre igual: no emite un solo comando GL, sólo escribe el campo
    ''' <c>projection</c> de ESTE control. Saltearlo dejaría la matriz con el aspecto viejo.</para></summary>
    Public Sub ApplyResize(Force As Boolean)
        If Me.IsInDesignMode Then Return
        If Force OrElse (Me.Width <> lastW OrElse Me.Height <> lastH) Then
            If EnsureContextCurrent() Then
                GL.Viewport(0, 0, Me.Width, Me.Height)
                lastW = Me.Width
                lastH = Me.Height
            End If
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
        Dim fovY As Single = MathHelper.DegreesToRadians(PreviewFovYDegrees)

        projection = Matrix4.CreatePerspectiveFieldOfView(fovY, aspect, nearZ, farZ)
        lastNear = nearZ
        lastFar = farZ
        UpdateRequired = True
    End Sub
    ''' <summary>The preview camera's VERTICAL field of view, degrees: the one constant of the projection (UpdateProjection
    ''' and the framing). The refraction LOD fade takes the preview's FOV as the world camera's (RefractionLaw.LodScale).</summary>
    Friend Const PreviewFovYDegrees As Single = 45.0F

    ''' <summary>El fade radial del fondo, normalizado a -1..+1. Lo consumen el quad de fondo y el piso:
    ''' los dos tienen que evaluar la MISMA curva o queda costura en el horizonte.</summary>
    Friend ReadOnly Property BackgroundFadeUnit As Single
        Get
            Return Config_App.Current.Setting_BackFade / 100.0F
        End Get
    End Property

    ''' <summary>El tamano del VIEWPORT GL, que es contra lo que se compara <c>gl_FragCoord</c> — no
    ''' <c>Width</c>/<c>Height</c> del control. Salen de <c>lastW</c>/<c>lastH</c>, que es lo que
    ''' <see cref="ApplyResize"/> le paso a <c>GL.Viewport</c> y lo que el pase de sombra restaura al
    ''' terminar. Antes del primer resize valen -1: el Max los deja en 1 y el fondo sale plano en vez de
    ''' dividir por cero.</summary>
    Friend ReadOnly Property ViewportSizeGl As Vector2
        Get
            Return New Vector2(Math.Max(1, lastW), Math.Max(1, lastH))
        End Get
    End Property

    ''' <summary>Pinta el FONDO del frame con el fade radial, encima del color plano que dejo el
    ''' <c>GL.Clear</c>.
    '''
    ''' <para>⛔ CON FADE = 0 NO DIBUJA NADA Y SALE. No es una optimizacion: es lo que garantiza que la
    ''' opcion apagada sea el camino de codigo de SIEMPRE, bit por bit, sin depender de que un
    ''' <c>mix(bg, target, 0.0)</c> devuelva exactamente el mismo byte que el clear. Un A/B contra el
    ''' commit anterior con el fade en 0 tiene que dar 0 pixeles de diferencia, y asi lo da por
    ''' construccion.</para>
    '''
    ''' <para>Se dibuja con el depth test APAGADO y sin escribir profundidad: el fondo no participa de la
    ''' oclusion, solo repinta el plano de color. El estado se devuelve como estaba —depth test,
    ''' depth mask y cull face— porque el resto del frame lo asume prendido (misma disciplina que
    ''' <c>FinishRenderFrame</c>).</para></summary>
    ''' <summary>Las tres perillas del fondo DIRECCIONAL, para los DOS programas que evaluan
    ''' <c>backgroundAt()</c> — el quad de fondo y el piso. Una sola funcion porque si los dos no reciben
    ''' exactamente lo mismo vuelve la costura del horizonte que el Const compartido existe para evitar.
    ''' <para>Con la casilla apagada sube <c>dirStrength = 0</c> y el shader ni entra a la rama: el
    ''' resultado es la viñeta centrada de siempre.</para></summary>
    ''' <summary>EL FONDO COMPLETO que evalua <c>backgroundAt()</c>: color, fade, viewport y las perillas
    ''' direccionales. Lo reciben los TRES programas que lo evaluan (el quad de fondo, el piso y la pasada de
    ''' post de FO4) desde esta unica funcion: si no reciben exactamente lo mismo vuelve la costura.</summary>
    Friend Sub SubirUniformsDeFondo(shader As Shader_Base_Class)
        shader.SetVector3("backgroundColor", Shader_Base_Class.Color_to_Vector(Config_App.Current.Setting_BackColor()))
        shader.SetFloat("backFade", BackgroundFadeUnit)
        shader.SetVector2("viewportSize", ViewportSizeGl)
        shader.SetBool("backgroundRowsD3D", _fo4RowsD3D)
        AplicarUniformsDireccionales(shader)
    End Sub

    Private Sub AplicarUniformsDireccionales(shader As Shader_Base_Class)
        Dim direccionalidad As Single = 0.0F
        Dim neto As Vector3 = Vector3.UnitZ
        If Config_App.Current.Setting_BackFadeDirectional AndAlso _Model IsNot Nothing Then
            ' El frame en curso, no el anterior: ver EnsureFrameLights.
            _Model.EnsureFrameLights(camera)
            direccionalidad = _Model.FrameLights.Directionality
            neto = _Model.FrameLights.NetDir
        End If
        shader.SetMatrix4("matView", camera.GetViewMatrix())
        shader.SetVector3("netDirWorld", neto)
        shader.SetFloat("rigDirectionality", direccionalidad)
    End Sub

    Private Sub PaintBackground()
        Dim fade As Single = BackgroundFadeUnit
        If fade = 0.0F Then Exit Sub
        Dim shader = SharedBackgroundShader
        If shader Is Nothing OrElse _bgVao = 0 Then Exit Sub

        shader.Use()
        GL.Disable(EnableCap.DepthTest)
        GL.DepthMask(False)
        GL.Disable(EnableCap.Blend)
        GL.Disable(EnableCap.CullFace)

        SubirUniformsDeFondo(shader)

        GL.BindVertexArray(_bgVao)
        GL.DrawArrays(PrimitiveType.Triangles, 0, 3)
        GL.BindVertexArray(0)
        GL.UseProgram(0)

        GL.DepthMask(True)
        GL.Enable(EnableCap.DepthTest)
        GL.Enable(EnableCap.CullFace)
    End Sub

    ' ── FRAME TARGETS ───────────────────────────────────────────────────────────────────────────────────────
    ' The frame is drawn offscreen (SceneTargets, PostProcess.vb) and presented with a blit. Every read
    ' (CaptureBitmap, ReadPixelPatch) reads the DISPLAY target, so a capture is always the frame just drawn (no
    ' front/back buffer timing). Allocated lazily by the first frame (a control that never renders - the bake
    ' runner's context - allocates nothing), reallocated on resize, freed in Clean.
    Private ReadOnly _targets As New SceneTargets()
    Private ReadOnly _sceneDepth As New SceneDepthCopy()
    Private ReadOnly _refraction As New RefractionTargets()
    Private _samplers As TextureSamplers

    ''' <summary>The GL sampler objects of the engines' sampler tables (one context).</summary>
    Friend ReadOnly Property Samplers As TextureSamplers
        Get
            If _samplers Is Nothing Then _samplers = New TextureSamplers()
            Return _samplers
        End Get
    End Property

    ''' <summary>GATE ONLY: the HDR target's radiance and coverage attachments as the last frame left them (0 before the first
    ''' HDR frame).</summary>
    Friend ReadOnly Property HdrSceneTexture As Integer
        Get
            Return _targets.SceneTexture
        End Get
    End Property

    Friend ReadOnly Property HdrCoverageTexture As Integer
        Get
            Return _targets.CoverageTexture
        End Get
    End Property

    ''' <summary>GATE ONLY: the refraction-normals target of the last frame (0 before the first refraction pass).</summary>
    Friend ReadOnly Property RefractionNormalsTexture As Integer
        Get
            Return _refraction.NormalsTexture
        End Get
    End Property

    ''' <summary>The refraction-normals program of the game drawing the frame.</summary>
    Friend Function RefractionNormalsProgram(isSse As Boolean) As Shader_Base_Class
        Return If(isSse, CType(SharedRefractionNormalsSse, Shader_Base_Class), SharedRefractionNormalsFo4)
    End Function

    ''' <summary>Binds and clears the refraction-normals target; it tests against the frame's own depth (the offscreen
    ''' targets' renderbuffer, or a copy of the window's).</summary>
    Friend Sub BeginRefractionNormals(isSse As Boolean)
        _refraction.BeginNormals(isSse, Me.Width, Me.Height, If(_frameFbo <> 0, _targets.DepthRenderbuffer, 0), _frameFbo)
    End Sub

    ''' <summary>Back to the frame's framebuffer (and its draw buffers).</summary>
    Friend Sub EndRefractionNormals()
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _frameFbo)
        If _frameFbo = _targets.HdrFramebuffer AndAlso _frameFbo <> 0 Then
            _targets.SetGroundCatcherOutputs(False)
        ElseIf _frameFbo <> 0 Then
            GL.DrawBuffer(DrawBufferMode.ColorAttachment0)
        Else
            GL.DrawBuffer(DrawBufferMode.Back)
        End If
        GL.Viewport(0, 0, Me.Width, Me.Height)
    End Sub

    Private ReadOnly _opaqueComposite As New SceneColorPass()
    ''' <summary>The scene depth the SAO's CameraZ reads (a copy after the opaque finish; not the SOFT copy, which is post-prepass).</summary>
    Private ReadOnly _saoDepth As New SceneDepthCopy()
    Private ReadOnly _sao As New SseSaoTargets()
    Private ReadOnly _fo4 As New Fo4DeferredTargets()
    ''' <summary>Skyrim SE's translucent decals' base (BeginSseDecalBase / EndSseDecalBase).</summary>
    Private ReadOnly _sseDecalBase As New DecalBaseTarget(deferred:=False)
    ''' <summary>Fallout 4's world envmap array of this GL context (Fo4EnvMapArray).</summary>
    Friend ReadOnly Fo4EnvMap As New Fo4EnvMapArray()
    Private _fo4Period As Long
    ''' <summary>kSAO is read by the composite with POINT sampling at texel centres (RE_SAO_BOTH 1.7).</summary>
    Private _saoPointSampler As Integer

    ''' <summary>GATE ONLY (ShadowGate --sse-sao-scene / --sse-composite): the SAO passes are skipped and the composite reads AO = 1.</summary>
    Friend Shared GateSseSaoOff As Boolean
    Private _gateWhite As Integer
    Private Function GateWhiteTexture() As Integer
        If _gateWhite = 0 Then
            GL.CreateTextures(TextureTarget.Texture2D, 1, _gateWhite)
            GL.TextureStorage2D(_gateWhite, 1, SizedInternalFormat.Rgba8, 1, 1)
            GL.TextureSubImage2D(_gateWhite, 0, 0, 0, 1, 1, OpenTK.Graphics.OpenGL4.PixelFormat.Rgba, PixelType.UnsignedByte, New Byte() {255, 255, 255, 255})
        End If
        Return _gateWhite
    End Function

    ''' <summary>GATE ONLY: Fallout 4's deferred targets of this control.</summary>
    Friend ReadOnly Property GateFo4Targets As Fo4DeferredTargets
        Get
            Return _fo4
        End Get
    End Property

    ''' <summary>GATE ONLY: the scene targets (HDR / display) of this control.</summary>
    Friend ReadOnly Property GateSceneTargets As SceneTargets
        Get
            Return _targets
        End Get
    End Property

    ''' <summary>GATE ONLY: the SAO targets of the last frame.</summary>
    Friend ReadOnly Property SseSao As SseSaoTargets
        Get
            Return _sao
        End Get
    End Property

    ''' <summary>GATE ONLY: the SAO normals target (attachment 3 of the HDR target).</summary>
    Friend ReadOnly Property HdrAoNormalTexture As Integer
        Get
            Return _targets.AoNormalTexture
        End Get
    End Property

    ''' <summary>The SSE SAO and its composite over the opaque scene (0x141541F40: the compute block, then ISSAOCompositeSAOFog), on the
    ''' HDR target: RenderAll calls it after the opaque groups and the decals, before the alpha list (the world render's order).
    ''' Only the post's frame: a direct frame carries the 2.3.8 display law in its fragments, not the engine's scene.</summary>
    Friend Sub ApplySseOpaqueComposite()
        If _frameFbo = 0 OrElse _frameFbo <> _targets.HdrFramebuffer Then Return
        If Not GateSseSaoOff Then
            _saoDepth.CopyFrom(_frameFbo, Me.Width, Me.Height)
            ' NiCamera viewFrustum of the preview's projection (symmetric perspective): right = 1/P00, top = 1/P11.
            Dim r As Single = 1.0F / projection.M11, t As Single = 1.0F / projection.M22
            _sao.Run(SharedSseSao, _saoDepth.Texture, _targets.AoNormalTexture, Me.Width, Me.Height, lastNear, lastFar,
                     New Vector4(-r, r, t, -t), _bgVao)
        End If
        If _saoPointSampler = 0 Then
            _saoPointSampler = GL.GenSampler()
            GL.SamplerParameter(_saoPointSampler, SamplerParameterName.TextureMinFilter, CInt(TextureMinFilter.Nearest))
            GL.SamplerParameter(_saoPointSampler, SamplerParameterName.TextureMagFilter, CInt(TextureMagFilter.Nearest))
            GL.SamplerParameter(_saoPointSampler, SamplerParameterName.TextureWrapS, CInt(TextureWrapMode.ClampToEdge))
            GL.SamplerParameter(_saoPointSampler, SamplerParameterName.TextureWrapT, CInt(TextureWrapMode.ClampToEdge))
        End If
        If GateCompositeSnapshot Then GatePreComposite = GateReadScene()
        _opaqueComposite.Apply(SharedSseOpaqueComposite, _frameFbo, SizedInternalFormat.R11fG11fB10f, Me.Width, Me.Height, _bgVao,
                               Sub(prog)
                                   prog.SetFloat("sseInvFramebufferRange", PreviewModel.RenderableMesh.SseInvFrameBufferRangeWorld)
                                   GL.BindTextureUnit(1, _targets.CoverageTexture)
                                   prog.SetInt("texCoverage", 1)
                                   GL.BindTextureUnit(2, If(GateSseSaoOff, GateWhiteTexture(), _sao.KSaoTexture))
                                   GL.BindSampler(2, _saoPointSampler)
                                   prog.SetInt("texSao", 2)
                                   ' S-N: kSNOW_SPECALPHA at unit 3 (SceneColorPass.Apply binds only unit 0, texScene).
                                   GL.BindTextureUnit(3, _targets.SnowSpecAlphaTexture)
                                   prog.SetInt("texSnowSpecAlpha", 3)
                                   prog.SetFloat("sseImprovedSnow", If(SseRenderPassLaw.ImprovedSnowExeDefault, 1.0F, 0.0F))
                                   prog.SetFloat("sseDeactivateAoOnSnow", SseRenderPassLaw.DeactivateAoOnSnowExeDefault)
                               End Sub)
        GL.BindSampler(2, 0)
        GL.BindTextureUnit(3, 0)   ' RT 0x70 is attachment 4 of the target the next draws write: not left bound to a sampler unit
        If GateCompositeSnapshot Then GatePostComposite = GateReadScene()
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _frameFbo)
        _targets.SetGroundCatcherOutputs(False)
    End Sub

    ''' <summary>Why this frame cannot be drawn (user decision rev-38 a, rev-50, v4 B-rev-05: no frame, RenderAll shows this on the
    ''' status card; never a fallback), or Nothing when it can: the game's programs exist, and for Fallout 4's deferred frame the
    ''' frame draws into the scene targets and Fo4DeferredTargets allocated.</summary>
    Friend Function FrameUnavailable(isSse As Boolean, fo4Deferred As Boolean) As String
        Dim errorText = If(isSse, SseProgramError, Fo4ProgramError)
        If errorText IsNot Nothing Then Return "Shaders unavailable: " & errorText
        If Not isSse AndAlso fo4Deferred AndAlso
           (_frameFbo = 0 OrElse _targets.HdrFramebuffer = 0 OrElse Not _fo4.Ensure(Me.Width, Me.Height)) Then Return "Deferred targets unavailable"
        Return Nothing
    End Function

    ''' <summary>Opens Fallout 4's G-buffer stage (Fo4DeferredTargets.BeginGBuffer: D3D row order, every RT cleared) and the envmap
    ''' array's period (the frame, Fo4EnvMapArray).</summary>
    Friend Sub BeginFo4GBuffer()
        _fo4RowsD3D = False
        _fo4Period += 1
        Fo4EnvMap.BeginPeriod(_fo4Period)
        _fo4.BeginGBuffer()
    End Sub

    ''' <summary>Fallout 4's G-buffer stage: the base pass of the translucent decals opens (Fo4DeferredTargets.BeginDecalBase), its
    ''' surface program with the frame's background (SubirUniformsDeFondo: the one function the background's programs take it from).
    ''' False: no base this frame (the target is incomplete).</summary>
    Friend Function BeginFo4DecalBase() As Boolean
        SharedFo4Deferred.DecalBaseSurface.Use()
        SubirUniformsDeFondo(SharedFo4Deferred.DecalBaseSurface)
        Return _fo4.BeginDecalBase(SharedFo4Deferred)
    End Function

    ''' <summary>Skyrim SE's frame: the base pass of the translucent decals opens (DecalBaseTarget.Begin) on the frame's depth (the
    ''' scene renderbuffer) and its colour targets - the HDR target's radiance and coverage, or the display target - and the game
    ''' shader gets the frame's background (SubirUniformsDeFondo). False (no base, the decals draw as before) when the frame draws into
    ''' the window (no scene targets: _targets.Ensure failed), the mark program does not exist, or the target is incomplete.</summary>
    Friend Function BeginSseDecalBase() As Boolean
        If SharedSseDecalBaseMark Is Nothing OrElse _frameFbo = 0 OrElse Not _sseDecalBase.Ensure(Me.Width, Me.Height) Then Return False
        Dim colours = If(_frameFbo = _targets.HdrFramebuffer,
                         {(_targets.SceneTexture, False), (_targets.CoverageTexture, False)},
                         {(_targets.DisplayRenderbuffer, True)})
        SharedSSEShader.Use()
        SubirUniformsDeFondo(SharedSSEShader)
        Return _sseDecalBase.Begin(SharedSseDecalBaseMark, _targets.DepthRenderbuffer, ImageTarget.Renderbuffer, 0, colours, False)
    End Function

    ''' <summary>Skyrim SE's frame: the base pass closes (DecalBaseTarget.Close; the frame depth was never written) and the frame's
    ''' framebuffer is bound again for the decals' colour draws.</summary>
    Friend Sub EndSseDecalBase()
        _sseDecalBase.Close()
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _frameFbo)
        GL.Viewport(0, 0, Me.Width, Me.Height)
    End Sub

    ''' <summary>The translucent decals' base target of the frame being drawn: Fallout 4's (Fo4DeferredTargets.DecalBase) during its
    ''' G-buffer stage, Skyrim SE's otherwise. The base draws bind its framebuffers (RenderableMesh.DrawDecalBaseSurface).</summary>
    Friend ReadOnly Property FrameDecalBase As DecalBaseTarget
        Get
            Return If(_Model IsNot Nothing AndAlso _Model.FrameGBufferStage, _fo4.DecalBase, _sseDecalBase)
        End Get
    End Property

    ''' <summary>GATE ONLY: Skyrim SE's base target (its auxiliary depth texture).</summary>
    Friend ReadOnly Property GateSseDecalBase As DecalBaseTarget
        Get
            Return _sseDecalBase
        End Get
    End Property

    ''' <summary>Fallout 4's G-buffer stage: the base pass of the translucent decals closes into the G-buffer depth (EndDecalBase).</summary>
    Friend Sub EndFo4DecalBase()
        _fo4.EndDecalBase(SharedFo4Deferred)
    End Sub

    ''' <summary>The rest of Fallout 4's opaque frame after the G-buffer (propuesta v3 G): depth copy and t15, lights, the envmap copy
    ''' (the composite's first call, 0x1421F82C4), composite 1, the depth blit, composite 2 into the scene target; post off: the
    ''' direct resolve onto the display target. Everything stays in D3D row order (UPPER_LEFT) for the forward stage that follows
    ''' (the zfight-overlay fix, 8-oct-2026: one orientation for every draw that tests against the G-buffer depth, as the game has
    ''' one); EndFo4Stage turns the frame's targets to GL rows. Leaves the frame's framebuffer bound.</summary>
    Friend Sub FinishFo4Deferred(frame As Fo4DeferredFrame)
        _fo4.EndGBuffer(SharedFo4Deferred, lastNear, lastFar)
        _fo4.RunLights(SharedFo4Deferred, frame)
        Fo4EnvMap.CopyQueued()
        _fo4.RunComposite1(SharedFo4Deferred, frame)
        _fo4.BlitDepthTo(_targets.HdrFramebuffer)
        ' Post off: the display target holds the background RenderScene painted in GL rows (PaintBackground): into D3D rows.
        If _frameFbo <> _targets.HdrFramebuffer Then _fo4.MirrorRows(_targets.DisplayRenderbuffer, ImageTarget.Renderbuffer, SizedInternalFormat.Rgba8)
        _fo4RowsD3D = True
        _fo4.RunComposite2(SharedFo4Deferred, frame, _targets.HdrFramebuffer, Fo4EnvMap.Texture)
        If _frameFbo <> _targets.HdrFramebuffer Then
            _fo4.RunDirectResolve(SharedFo4Deferred, _targets.SceneTexture, _targets.CoverageTexture, _frameFbo, Shader_Base_Class.SceneToLinearExponent(False))
        End If
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _frameFbo)
        GL.Viewport(0, 0, Me.Width, Me.Height)
        If _frameFbo = _targets.HdrFramebuffer Then _targets.SetGroundCatcherOutputs(False)
    End Sub

    ''' <summary>The end of Fallout 4's D3D-order stage (G-buffer, lights, composites and the forward stage), however it ended: when
    ''' the frame reached composite 2 (FinishFo4Deferred), the frame's targets turned to GL rows - the scene target's radiance and
    ''' coverage, the background-shadow target (post on) or the display target (post off), and the depth/stencil
    ''' (Fo4DeferredTargets.MirrorRows) - so everything after the stage (refraction normals, the post, the display overlays, the
    ''' capture) finds them as before the zfight-overlay fix; the SOFT depth copy taken in the stage holds D3D rows, so the next SOFT
    ''' reader copies again (FrameDepthDirty). Then GL row order and linear writes back, so nothing after it inherits the D3D-order
    ''' state (propuesta A.2).</summary>
    Friend Sub EndFo4Stage()
        If GateFo4SkipEndStage Then Return
        If _fo4RowsD3D Then
            _fo4RowsD3D = False
            _fo4.MirrorRows(_targets.SceneTexture, ImageTarget.Texture2D, SizedInternalFormat.R11fG11fB10f)
            _fo4.MirrorRows(_targets.CoverageTexture, ImageTarget.Texture2D, SizedInternalFormat.Rgba8)
            If _frameFbo = _targets.HdrFramebuffer Then
                _fo4.MirrorRows(_targets.BgShadowTexture, ImageTarget.Texture2D, SizedInternalFormat.Rgba8)
            Else
                _fo4.MirrorRows(_targets.DisplayRenderbuffer, ImageTarget.Renderbuffer, SizedInternalFormat.Rgba8)
            End If
            _fo4.MirrorRows(_targets.DepthRenderbuffer, ImageTarget.Renderbuffer, SizedInternalFormat.Depth24Stencil8)
            If _Model IsNot Nothing Then _Model.FrameDepthDirty = True
        End If
        GL.ClipControl(ClipOrigin.LowerLeft, ClipDepthMode.NegativeOneToOne)
        GL.Disable(EnableCap.FramebufferSrgb)
    End Sub

    ''' <summary>The frame is between composite 2 and the end of Fallout 4's D3D-order stage (FinishFo4Deferred .. EndFo4Stage): its
    ''' targets hold D3D rows, and the background is evaluated in them (SubirUniformsDeFondo, backgroundRowsD3D).</summary>
    Private _fo4RowsD3D As Boolean

    ''' <summary>GATE ONLY (no UI): ShadowGate --fo4-deferred-scene mutant. True makes EndFo4Stage do nothing.</summary>
    Friend Shared GateFo4SkipEndStage As Boolean = False

    ''' <summary>GATE ONLY (ShadowGate --sse-sao-scene): the HDR radiance right before and right after the composite of the frame.</summary>
    Friend Shared GateCompositeSnapshot As Boolean
    Friend GatePreComposite As Single(), GatePostComposite As Single()
    Private Function GateReadScene() As Single()
        Dim px(Me.Width * Me.Height * 4 - 1) As Single
        GL.GetTextureImage(_targets.SceneTexture, 0, OpenTK.Graphics.OpenGL4.PixelFormat.Rgba, PixelType.Float, px.Length * 4, px)
        Return px
    End Function

    ''' <summary>ISRefraction over the frame's colour (attachment 0 of <paramref name="fbo"/>).</summary>
    Private Sub ApplyRefraction(isSse As Boolean, fbo As Integer, fmt As SizedInternalFormat)
        If Not _Model.FrameRefractionActive Then Return
        _refraction.ApplyImageSpace(SharedRefractionImageSpace, isSse, fbo, fmt, Me.Width, Me.Height, _bgVao)
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, fbo)
        If fbo = _targets.HdrFramebuffer AndAlso fbo <> 0 Then _targets.SetGroundCatcherOutputs(False)
    End Sub
    Private _frameFbo As Integer = 0

    ''' <summary>Copies the depth of the framebuffer being drawn for the effects' SOFT fade (see SceneDepthCopy).</summary>
    Friend Sub CopySceneDepth()
        ' Fallout 4's G-buffer stage draws into the deferred targets (D3D rows, as gl_FragCoord under UPPER_LEFT): their depth.
        _sceneDepth.CopyFrom(If(_Model IsNot Nothing AndAlso _Model.FrameGBufferStage, _fo4.GBufferFramebuffer, _frameFbo), Me.Width, Me.Height)
    End Sub

    ''' <summary>The copied scene depth (0 before the first copy).</summary>
    Friend ReadOnly Property SceneDepthTexture As Integer
        Get
            Return _sceneDepth.Texture
        End Get
    End Property

    ''' <summary>Near and far of the projection this frame draws with (UpdateProjection sets both with it).</summary>
    Friend ReadOnly Property FrameNearFar As Vector2
        Get
            Return New Vector2(lastNear, lastFar)
        End Get
    End Property
    Private _statusDrawnInFrame As Boolean

    ''' <summary>The framebuffer RenderScene is drawing into right now (HDR target, display target, or 0).
    ''' Passes that bind their own framebuffer (the shadow maps) return to THIS one, without a glGet per frame.</summary>
    Friend ReadOnly Property SceneFramebuffer As Integer
        Get
            Return _frameFbo
        End Get
    End Property

    ''' <summary>The ground catcher also writes its factor for the UI background (HDR frames only).</summary>
    Friend Sub SetGroundCatcherOutputs(enabled As Boolean)
        _targets.SetGroundCatcherOutputs(enabled)
    End Sub

    ''' <summary>THE FRAME GOES THROUGH THE GAME'S POST LAW (FO4: i3700 + i3648; SSE: 12545) unless the user turned the post off (Rendering tab) or a shader debug
    ''' view is on (a debug view shows raw values: it is drawn straight to the display target, 8-bit exact).
    ''' Returns the post program of the game drawing the frame, or Nothing for a direct frame.</summary>
    Private Function FramePostProgram() As Shader_Base_Class
        If _bgVao = 0 OrElse CurrentShader Is Nothing OrElse SharedLumPartialsShader Is Nothing OrElse
           SharedLumResolveShader Is Nothing OrElse Shader_Base_Class.DebugView <> ShaderDebugView.None OrElse
           Not _Model.FrameImaging.Settings.ApplyPostProcess Then Return Nothing
        If CurrentShader Is SharedActiveShader Then Return SharedPostFo4Shader
        If CurrentShader Is SharedSSEShader Then Return SharedPostSseShader
        Return Nothing
    End Function

    ''' <summary>Binds the framebuffer reads come from (the scene target, or the window's default one when the
    ''' scene target could not be allocated) and returns the previous read binding and read buffer to restore.</summary>
    Private Function BindSceneForRead(fallback As ReadBufferMode) As (PrevFbo As Integer, PrevBuffer As Integer)
        Dim prevFbo As Integer = 0, prevBuf As Integer = CInt(ReadBufferMode.Back)
        Try
            GL.GetInteger(GetPName.ReadFramebufferBinding, prevFbo)
            GL.GetInteger(GetPName.ReadBuffer, prevBuf)
        Catch
        End Try
        If _targets.DisplayFramebuffer <> 0 Then
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, _targets.DisplayFramebuffer)
            GL.ReadBuffer(ReadBufferMode.ColorAttachment0)
        Else
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, 0)
            GL.ReadBuffer(fallback)
        End If
        Return (prevFbo, prevBuf)
    End Function

    Private Shared Sub RestoreRead(prev As (PrevFbo As Integer, PrevBuffer As Integer))
        GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, prev.PrevFbo)
        GL.ReadBuffer(CType(prev.PrevBuffer, ReadBufferMode))
    End Sub

    Private Sub RenderScene()
        If _isTearingDown OrElse Me.IsDisposed OrElse Me.Disposing Then Exit Sub
        If _Model Is Nothing Then Exit Sub
        If SharedActiveShader Is Nothing AndAlso SharedSSEShader Is Nothing Then Exit Sub
        ApplyResize(False)
        Me.EnsureContextCurrent()
        Dim offscreen = _targets.Ensure(Me.Width, Me.Height)
        ' The weather/moment and the two options of THIS frame, for the game of the shader that draws it.
        _Model.ResolveFrameImaging(CurrentShader Is SharedSSEShader)
        Dim postProgram = If(offscreen, FramePostProgram(), Nothing)
        Dim hdr = postProgram IsNot Nothing
        _statusDrawnInFrame = False
        If hdr Then
            ' Linear radiance + coverage; the background is composited by the post (it is UI).
            _targets.BeginHdr(CurrentShader Is SharedSSEShader)
            _frameFbo = _targets.HdrFramebuffer
        Else
            If offscreen Then
                _targets.BindDisplay()
                _frameFbo = _targets.DisplayFramebuffer
            Else
                GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0)
                GL.Viewport(0, 0, Me.Width, Me.Height)
                _frameFbo = 0
            End If
            GL.ClearColor(Config_App.Current.Setting_BackColor)
            GL.Clear(ClearBufferMask.ColorBufferBit Or ClearBufferMask.DepthBufferBit)
            PaintBackground()
        End If
        Model.FrameIsHdr = hdr
        Try
            If Model.Can_Render Then
                Model.RenderAll(projection, camera)
            End If
            Dim isSse = CurrentShader Is SharedSSEShader
            ' ISRefraction: SSE effect index 1, BEFORE the HDR (0x1414EDBF0 runs effects 0..7, then the HDR effect 0x1E),
            ' over the HDR scene; FO4 effect index 5, AFTER ImageSpaceEffectHDR (3) / HDRCS (4) (0x142185420), over the
            ' tonemapped image. A direct frame (post off) has no HDR step: it applies to the frame as drawn.
            If Not hdr Then
                ApplyRefraction(isSse, _frameFbo, SizedInternalFormat.Rgba8)
            ElseIf isSse AndAlso Not _statusDrawnInFrame Then
                ApplyRefraction(True, _targets.HdrFramebuffer, SizedInternalFormat.R11fG11fB10f)
            End If
            ' A status card drawn during the frame (textures still loading) is already in the display target.
            If hdr AndAlso Not _statusDrawnInFrame Then
                _targets.ReduceLuminance(SharedLumPartialsShader, SharedLumResolveShader)
                Dim row = _Model.FrameImaging.Row
                If postProgram Is SharedPostSseShader Then
                    _targets.Composite(postProgram, AddressOf row.SseImageSpace.Upload, Nothing, AddressOf SubirUniformsDeFondo, _bgVao)
                Else
                    _targets.Composite(postProgram, AddressOf row.Fo4ImageSpace.Upload, row.Fo4ImageSpace.LutPath, AddressOf SubirUniformsDeFondo, _bgVao)
                End If
                _frameFbo = _targets.DisplayFramebuffer
                If Not isSse Then ApplyRefraction(False, _targets.DisplayFramebuffer, SizedInternalFormat.Rgba8)
                ' What is drawn in display values (wireframes) goes on top of the post, depth-tested
                ' against the scene's depth (shared by both targets).
                Model.RenderDisplayOverlays(projection, camera)
            End If
        Finally
            Model.FrameIsHdr = False
        End Try
        If offscreen Then _targets.Present()
        If Not _statusDrawnInFrame Then DrawPreviewGapNotice()
        _frameFbo = 0
    End Sub

    ''' <summary>The frame's notice (PreviewModel.ReportUndrawn): one line per reason with its shape count - the preview's gaps
    ''' (FramePreviewGaps), the shapes the game itself does not draw (FrameEngineUndrawn, user decision 5-oct-2026: same law as the
    ''' game, but never silent) and the ones the game does not draw while the preview does (FrameDrawnForEditing, 6-oct-2026) -
    ''' top-left on the WINDOW after the frame is presented (rev-59): CaptureBitmap reads the scene target, so captures (NPC
    ''' portraits, gate PNGs) never carry it - except when the offscreen targets could not be allocated and the capture reads the
    ''' front buffer. The text wraps at the window's width less the margin on each side. "Show errors" off
    ''' (Config_App.Setting_ShowErrors) draws none of it; the census is not touched.</summary>
    Private Sub DrawPreviewGapNotice()
        If overlay Is Nothing OrElse _Model Is Nothing Then Exit Sub
        If Config_App.Current IsNot Nothing AndAlso Not Config_App.Current.Setting_ShowErrors Then Exit Sub
        Dim lines = _Model.FrameNoticeLines()
        If lines.Count = 0 Then Exit Sub
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0)
        GL.Viewport(0, 0, Me.Width, Me.Height)
        ' Wrapped at the window's width; when the lines do not fit its height, the first ones and "+K more".
        Dim maxW = Math.Max(1, Me.Width - 2 * NoticeMargin)
        Dim colours = TextOverlayRenderer.NoticeColours(If(Config_App.Current Is Nothing, Color.Black, Config_App.Current.Setting_BackColor))
        overlay.SetText(TextOverlayRenderer.FitLines(lines, NoticeFontSize, NoticeFontName, maxW, Math.Max(1, Me.Height - 2 * NoticeMargin)),
                        NoticeFontSize, NoticeFontName, maxW, colours.Text, colours.Ring)
        overlay.RenderAt(NoticeMargin, NoticeMargin, overlay.LabelWidth, overlay.LabelHeight, Me.Width, Me.Height)
    End Sub

    ''' <summary>The notice's font size and its margin from the window's top-left corner (the label wraps at the window's width less
    ''' this margin on each side). Friend: ShadowGate's notice gate measures the label with them.</summary>
    Friend Const NoticeFontSize As Integer = 9
    Friend Const NoticeMargin As Integer = 8
    Friend Const NoticeFontName As String = "Arial"
    Private Shared Sub FinishRenderFrame()
        GL.DepthMask(True)
        GL.ColorMask(True, True, True, True)
        GL.Disable(EnableCap.Blend)
    End Sub

    ''' <summary>Lee el color de UN punto del framebuffer, promediando una ventana de
    ''' <paramref name="box"/>x<paramref name="box"/> pixeles centrada en (<paramref name="x"/>,
    ''' <paramref name="y"/>) - coordenadas del CONTROL, origen arriba-izquierda. Devuelve
    ''' <c>Color.Empty</c> si el punto cae fuera o si no hay contexto.
    '''
    ''' <para>[!] Lo que devuelve es el pixel TAL COMO SE VE: iluminado y pasado por la ley de display
    ''' (el post del juego de PostProcess.vb, o con el post apagado la cola de 2.3.8 de LegacyDisplaySource).
    ''' NO es albedo, NO es lineal y NO es el tono del material. Cualquier consumidor que compare dos
    ''' muestras esta comparando resultados finales, que es exactamente para lo que existe.</para>
    '''
    ''' <para>Misma disciplina de estado que <see cref="CaptureBitmap"/>: se CAPTURA y se DEVUELVE el
    ''' ReadBuffer y el PackAlignment que habia - dejarlos pisados es el bug que "solo se ve a veces".</para></summary>
    Public Function ReadPixelDisplay(x As Integer, y As Integer, Optional box As Integer = 3,
                                     Optional presentFrame As Boolean = True) As Color
        Return ReadPixelPatch(x, y, box, presentFrame).Mean
    End Function

    ''' <summary>Igual que <see cref="ReadPixelDisplay"/> pero devuelve tambien la DISPERSION del parche:
    ''' el mayor rango (max - min) entre los canales, en niveles de pantalla.
    ''' <para>Para que sirve: una ventana de muestreo grande promedia el ruido y el moteado especular -por eso
    ''' conviene- pero si el punto elegido cae sobre el borde de la silueta, sobre una costura o sobre el filo de
    ''' una sombra, el parche mezcla piel con fondo o con penumbra y el promedio deja de representar al color que
    ''' el usuario quiso elegir. La dispersion permite AVISARLO en vez de comparar dos numeros envenenados.</para>
    ''' <para>Un parche de piel plana da tipicamente &lt; 10; arriba de ~25 casi siempre hay un borde adentro.</para>
    ''' <para><paramref name="wantImage"/> agrega la IMAGEN del parche (los pixeles crudos, sin escalar) para
    ''' poder MOSTRAR la muestra en vez de un color plano. Es opt-in porque aloca un Bitmap por llamada y el
    ''' lazo del auto-calc muestrea cientos de veces: ahi se pide sin imagen y no se aloca nada. El caller es
    ''' duenio del Bitmap y tiene que hacerle Dispose.</para></summary>
    Public Function ReadPixelPatch(x As Integer, y As Integer, Optional box As Integer = 3,
                                   Optional presentFrame As Boolean = True,
                                   Optional wantImage As Boolean = False) As (Mean As Color, Spread As Double, Image As Bitmap)
        Dim failed = (CType(Color.Empty, Color), 0.0R, CType(Nothing, Bitmap))
        If Me.IsInDesignMode OrElse Me.Width <= 0 OrElse Me.Height <= 0 Then Return failed
        If x < 0 OrElse y < 0 OrElse x >= Me.Width OrElse y >= Me.Height Then Return failed
        If box < 1 Then box = 1

        Me.EnsureContextCurrent()
        ApplyResize(True)
        Dim readTarget As ReadBufferMode
        If presentFrame Then
            If UpdateRequired Then
                ' Mismo consumo up-front que CaptureBitmap: cualquier pedido nuevo que levante RenderScene
                ' sobrevive a este frame y agenda el siguiente.
                UpdateRequired = False
                RenderScene()
                SwapBuffers()
                FinishRenderFrame()
            End If
            readTarget = ReadBufferMode.Front
        Else
            ' MUESTREO DETERMINISTA (presentFrame:=False). Se dibuja SIEMPRE -no condicionado a
            ' UpdateRequired- en el BACK buffer, se espera a que la GPU termine y se lee de ahi SIN
            ' SwapBuffers.
            '   * Por que no leer el FRONT despues de un swap: el swap puede encolarse, asi que el front
            '     puede seguir teniendo el frame ANTERIOR. En un lazo de medicion (aplicar -> renderizar ->
            '     leer) eso es un desfase de UNA iteracion: se mide el candidato viejo y la busqueda entera
            '     queda envenenada. Es el modo de falla que NO se nota en una captura suelta.
            '   * Por que renderizar siempre: tras un swap el contenido del back buffer es INDEFINIDO por
            '     spec, asi que leerlo sin haber dibujado no garantiza nada.
            '   * Efecto lateral querido: sin swap el lazo no parpadea en pantalla.
            UpdateRequired = False
            RenderScene()
            GL.Finish()
            FinishRenderFrame()
            readTarget = ReadBufferMode.Back
        End If

        ' Ventana EXACTA de box x box centrada en el punto, recortada contra los bordes del control (en
        ' coordenadas GL, origen abajo). El tamano pedido tiene que ser el tamano leido: el intervalo
        ' [x-half, x+half] NO sirve, para un box PAR da box+1 pixeles (8 -> 9).
        Dim half As Integer = box \ 2
        Dim x0 As Integer = Math.Max(0, x - half)
        Dim x1 As Integer = Math.Min(Me.Width - 1, x - half + box - 1)
        Dim glY As Integer = Me.Height - 1 - y
        Dim y0 As Integer = Math.Max(0, glY - half)
        Dim y1 As Integer = Math.Min(Me.Height - 1, glY - half + box - 1)
        Dim w As Integer = x1 - x0 + 1
        Dim h As Integer = y1 - y0 + 1
        If w <= 0 OrElse h <= 0 Then Return failed

        Dim buf(w * h * 4 - 1) As Byte
        Dim prevPackAlignment As Integer = 4
        Try
            GL.GetInteger(GetPName.PackAlignment, prevPackAlignment)
        Catch
        End Try
        Dim handle As GCHandle = Nothing
        Dim prevRead = BindSceneForRead(readTarget)
        Try
            handle = GCHandle.Alloc(buf, GCHandleType.Pinned)
            GL.PixelStore(PixelStoreParameter.PackAlignment, 1)
            GL.ReadPixels(x0, y0, w, h, OpenTK.Graphics.OpenGL4.PixelFormat.Bgra, PixelType.UnsignedByte, handle.AddrOfPinnedObject())
        Catch ex As Exception
            Return failed
        Finally
            Try
                RestoreRead(prevRead)
                GL.PixelStore(PixelStoreParameter.PackAlignment, prevPackAlignment)
            Catch
            End Try
            If handle.IsAllocated Then handle.Free()
        End Try

        Dim sumB As Long = 0, sumG As Long = 0, sumR As Long = 0, sumA As Long = 0
        Dim minR As Integer = 255, minG As Integer = 255, minB As Integer = 255
        Dim maxR As Integer = 0, maxG As Integer = 0, maxB As Integer = 0
        Dim n As Integer = w * h
        For i As Integer = 0 To n - 1
            Dim pb As Integer = buf(i * 4)
            Dim pg As Integer = buf(i * 4 + 1)
            Dim pr As Integer = buf(i * 4 + 2)
            sumB += pb : sumG += pg : sumR += pr
            sumA += buf(i * 4 + 3)
            If pr < minR Then minR = pr
            If pr > maxR Then maxR = pr
            If pg < minG Then minG = pg
            If pg > maxG Then maxG = pg
            If pb < minB Then minB = pb
            If pb > maxB Then maxB = pb
        Next
        Dim spread As Double = Math.Max(maxR - minR, Math.Max(maxG - minG, maxB - minB))

        Dim img As Bitmap = Nothing
        If wantImage Then
            ' GL entrega las filas de ABAJO hacia arriba: se copian invertidas para que el Bitmap quede
            ' orientado como la pantalla. El formato de lectura ya es BGRA, que es el layout de
            ' Format32bppArgb en memoria, asi que la fila se copia tal cual.
            img = New Bitmap(w, h, Imaging.PixelFormat.Format32bppArgb)
            Dim bd = img.LockBits(New Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, Imaging.PixelFormat.Format32bppArgb)
            Try
                For row As Integer = 0 To h - 1
                    Dim srcOffset As Integer = (h - 1 - row) * w * 4
                    Dim dstPtr = IntPtr.Add(bd.Scan0, row * bd.Stride)
                    Runtime.InteropServices.Marshal.Copy(buf, srcOffset, dstPtr, w * 4)
                Next
            Finally
                img.UnlockBits(bd)
            End Try
        End If

        Return (Color.FromArgb(CInt(sumA \ n), CInt(sumR \ n), CInt(sumG \ n), CInt(sumB \ n)), spread, img)
    End Function

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
        ' Se CAPTURA lo que habia, no se asume. Restaurar el literal `Back` es correcto solo mientras el
        ' llamador entre con el framebuffer por defecto bindeado; con un FBO bindeado el valor previo es un
        ' COLOR_ATTACHMENTi y "restaurar Back" seria dejarlo peor que antes. Lo mismo con PackAlignment: se
        ' pisa a 4 dos lineas mas abajo y hay que devolver el que habia, no el default de GL.
        Dim prevPackAlignment As Integer = 4
        Try
            GL.GetInteger(GetPName.PackAlignment, prevPackAlignment)
        Catch
        End Try
        ' The scene target holds the last frame drawn (the window's front buffer only when the target could not
        ' be allocated).
        Dim prevRead = BindSceneForRead(ReadBufferMode.Front)
        Try
            GL.PixelStore(PixelStoreParameter.PackAlignment, 4)
            GL.ReadPixels(0, 0, bmp.Width, bmp.Height, OpenTK.Graphics.OpenGL4.PixelFormat.Bgra, PixelType.UnsignedByte, data.Scan0)
        Finally
            ' DEVOLVER el ReadBuffer y el binding de lectura: dejarlos pisados es el bug que "solo se ve a veces".
            RestoreRead(prevRead)
            GL.PixelStore(PixelStoreParameter.PackAlignment, prevPackAlignment)
            Dim rb = prevRead.PrevBuffer, pa = prevPackAlignment
            Logger.LogLazy(Function() $"[AUDIT-CAPTURE] tras la captura se devuelven ReadBuffer=0x{rb:X} y PackAlignment={pa}")
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
        ' Modo picker: el click izquierdo MUESTREA y no orbita.
        If _colorPickMode AndAlso e.Button = MouseButtons.Left Then
            ' lastX/lastY SE SIEMBRAN IGUAL aunque no haya arrastre que continuar: el handler de ColorPicked
            ' desarma el modo dentro de este mismo MouseDown, y el MouseMove siguiente -con el boton todavia
            ' apretado- caeria en la orbita con un lastX de hace dos interacciones, girando la camara de golpe.
            lastX = e.X
            lastY = e.Y
            _pickSwallowLeftDrag = True
            ' presentFrame:=False = MISMA lectura que hace un lazo de medicion (dibuja al back y lee de ahi).
            ' Importa que sea la misma: si el color de origen saliera del FRONT y los del destino del BACK,
            ' cualquier diferencia entre los dos buffers entraria como sesgo constante en la comparacion.
            Dim c = ReadPixelDisplay(e.X, e.Y, presentFrame:=False)
            RaiseEvent ColorPicked(Me, New ColorPickedEventArgs(e.X, e.Y, c))
            Return
        End If
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
        ' El izquierdo esta reservado al muestreo mientras dure el click del pick. Se consulta el LATCH y no
        ' solo el modo: el modo puede haberse apagado dentro del MouseDown (picker de un solo disparo) y el
        ' arrastre de ese mismo click no debe orbitar.
        If (_colorPickMode OrElse _pickSwallowLeftDrag) AndAlso e.Button = MouseButtons.Left Then Return
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
        ' El latch del pick vive exactamente lo que dura el click que lo prendio.
        _pickSwallowLeftDrag = False
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

        Dim floorShadowRig = Config_App.Current.ActiveLights()
        Dim toggleFloorShadows As New ToolStripMenuItem("Cast Floor Shadows") With {
            .Checked = floorShadowRig.ShadowOnGround,
            .CheckOnClick = True
        }
        AddHandler toggleFloorShadows.Click, Sub()
                                                 Dim currentRig = Config_App.Current.ActiveLights()
                                                 currentRig.ShadowOnGround = toggleFloorShadows.Checked
                                                 Config_App.Current.SetActiveLights(currentRig)
                                                 UpdateRequired = True
                                             End Sub
        menu.Items.Add(toggleFloorShadows)
        menu.Items.Add(New ToolStripSeparator())

        Dim shadowSettings = Config_App.Current.ActiveShadows().Sanitized()
        Dim toggleShadows As New ToolStripMenuItem("Cast Shadows") With {
            .Checked = shadowSettings.Enabled,
            .CheckOnClick = True
        }
        AddHandler toggleShadows.Click, Sub()
                                            Dim current = Config_App.Current.ActiveShadows().Sanitized()
                                            current.Enabled = toggleShadows.Checked
                                            Config_App.Current.SetActiveShadows(current)
                                            UpdateRequired = True
                                        End Sub
        menu.Items.Add(toggleShadows)

        Dim lightsFollowCamera As New ToolStripMenuItem("Lights Follow Camera") With {
            .Checked = Config_App.Current.Setting_LightsFollowCamera,
            .CheckOnClick = True
        }
        AddHandler lightsFollowCamera.Click, Sub()
                                                 Config_App.Current.Setting_LightsFollowCamera = lightsFollowCamera.Checked
                                                 UpdateRequired = True
                                             End Sub
        menu.Items.Add(lightsFollowCamera)

        Dim presetsMenu As New ToolStripMenuItem("Light Preset")
        For Each preset In PreviewLightRig.Presets()
            ' Copia local deliberada: el handler sobrevive a esta iteracion del For Each.
            Dim selectedPreset = preset
            Dim presetItem As New ToolStripMenuItem(selectedPreset.Name) With {
                .Checked = selectedPreset.MatchesConfig(Config_App.Current),
                .ToolTipText = selectedPreset.Description
            }
            AddHandler presetItem.Click, Sub()
                                             selectedPreset.ApplyTo(Config_App.Current)
                                             UpdateRequired = True
                                         End Sub
            presetsMenu.DropDownItems.Add(presetItem)
        Next
        menu.Items.Add(presetsMenu)
        menu.Items.Add(New ToolStripSeparator())
        Dim toggleSkinning As New ToolStripMenuItem("GPU Skinning") With {
            .Checked = Config_App.Current.Setting_GPUSkinning,
            .CheckOnClick = True
        }
        AddHandler toggleSkinning.Click, Sub()
                                             Config_App.Current.Setting_GPUSkinning = toggleSkinning.Checked
                                             RaiseEvent SkinningModeToggled(Me)
                                             ' El sellado lo hace la recarga de abajo (MarkDirty Shapes Or
                                             ' Force -> needsFullReload), asi que este camino y el del
                                             ' dialogo comparten el mismo espejo y no se pisan.
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

        ' ===== Geometria oculta =====
        ' Los DOS ajustes de "mostrar lo que normalmente no se ve", juntos y en el mismo lugar donde el
        ' usuario ya cambia el resto del render. APP-AWARE igual que en el dialogo del rig:
        '  · Draw hidden segments -> se MUESTRA siempre, DESHABILITADO donde la app no lo permite (NPC
        '    Manager: su render depende de la oclusion por segmento). Ver Config_App.AllowDrawHiddenSegments.
        '  · Show helper shapes   -> editable en las DOS apps; lo que cambia por app es el DEFAULT.
        ' Los dos RE-DISPARAN el render en el mismo gesto: son decisiones por-frame, asi que alcanza con
        ' UpdateRequired (mismo idiom que Render Floor y el resto del menu). Sin esto no se veia nada hasta
        ' el latido del timer.
        menu.Items.Add(New ToolStripSeparator())

        Dim hiddenSegs As New ToolStripMenuItem("Render hidden segments") With {
            .Checked = Config_App.Current.Setting_DrawHiddenSegments,
            .CheckOnClick = True,
            .Enabled = Config_App.AllowDrawHiddenSegments,
            .ToolTipText = If(Config_App.AllowDrawHiddenSegments,
                              "Render mesh segments the NIF marks as not drawn (e.g. the with-item Pip-Boy forearm variant).",
                              "Forced OFF in NPC Manager: the NPC render relies on per-segment occlusion.")
        }
        AddHandler hiddenSegs.Click, Sub()
                                         If Not Config_App.AllowDrawHiddenSegments Then Exit Sub
                                         Config_App.Current.Setting_DrawHiddenSegments = hiddenSegs.Checked
                                         UpdateRequired = True
                                     End Sub
        menu.Items.Add(hiddenSegs)

        Dim helperShapes As New ToolStripMenuItem("Render hidden shapes") With {
            .Checked = Config_App.ShowHelperShapesEfectivo(),
            .CheckOnClick = True,
            .ToolTipText = "Render whole shapes the NIF marks as not drawn: no shader property, the NiAVObject hidden flag, or a hidden node above them (damage stages out of range included). Shapes the game culls by their bound: not drawn in a composite view, drawn in a piece view; the frame's notice lists them."
        }
        AddHandler helperShapes.Click, Sub()
                                           Config_App.Current.Setting_ShowHelperShapes = helperShapes.Checked
                                           UpdateRequired = True
                                       End Sub
        menu.Items.Add(helperShapes)

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
            ' HelperShapeGate cubre RenderHide + las helper shapes. Encuadrar sobre una helper deforma la
            ' camara: el VirtualGround de KS Hairdos SMP es un quad de radio 113 centrado en el origen,
            ' o sea que la escena entera se aleja por una malla que ni siquiera se dibuja.
            If mesh Is Nothing OrElse Not mesh.IsDrawable() Then Continue For
            ' A Fallout 4 shape the preview cannot draw yet (preview Gap) leaves every pass (rev-55 a): it is not drawn, so it is
            ' not framed either.
            If Not Model.FrameIsSse AndAlso mesh.MeshData.Material IsNot Nothing AndAlso
               mesh.MeshData.Material.Fo4PreviewGap(SharedFo4Deferred) Then Continue For
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
        Dim fovY As Single = MathHelper.DegreesToRadians(PreviewFovYDegrees)
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

        ' Red de seguridad: si no se presento un frame en ~1 s, forzar uno. Cubre la perdida del front buffer
        ' (hide/show, recreacion de handle, compositor DWM) sin pagar un redraw en cada tick.
        ' GUARD: solo dispara si ESTE control tiene el contexto GL. Varios PreviewControl conviven entre el
        ' MainForm y los editores modales, cada uno con su contexto, y el "contexto actual" de OpenTK es por
        ' hilo y global al proceso: un Invalidate -> OnPaint -> MakeCurrent desde un control que no es el actual
        ' le roba el contexto al hermano que lo tiene (tipicamente a mitad de frame) y corrompe los dos renders.
        ' Si no somos los actuales, el contador se mantiene en el umbral para re-disparar en el proximo tick.
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

        ' ⛔⛔ ACÁ PUSE UN `EnsureContextCurrent()` Y LO SAQUÉ, porque no cerraba nada y no tenía
        ' medición. El razonamiento era por analogía —`NpcRenderHost.Dispose` lo hace, y el comentario de
        ' los ocho defaults (más abajo) dice que los borrados «irian con ids viejos contra el contexto de
        ' OTRO PreviewControl vivo»— y `overlay.Clean()` sí corre antes del único `EnsureContextCurrent`
        ' del teardown, que vive dentro de `Model.Clean`. Suena bien y es FALSO como arreglo:
        ' `Tools\ViewportAjenoGate`, caso V-3, mide el daño en PÍXELES del cartel del preview hermano y da
        ' **8432 píxeles idénticos con y sin esa línea**. O sea que el daño existe (es real y repetible)
        ' pero NO lo causa el contexto que esté current, así que la línea era decoración.
        ' ⛔ EL DEFECTO SIGUE ABIERTO y su sujeto NO está identificado: ver el encabezado de
        ' `ViewportAjenoGate` (V-3, declarado como medición sin veredicto).

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
            _Model.DisposeShadowResources()
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

        If SharedBackgroundShader IsNot Nothing Then
            SharedBackgroundShader.Dispose()
            SharedBackgroundShader = Nothing
        End If

        If SharedPostFo4Shader IsNot Nothing Then
            SharedPostFo4Shader.Dispose()
            SharedPostFo4Shader = Nothing
        End If

        If SharedPostSseShader IsNot Nothing Then
            SharedPostSseShader.Dispose()
            SharedPostSseShader = Nothing
        End If

        If SharedLumPartialsShader IsNot Nothing Then
            SharedLumPartialsShader.Dispose()
            SharedLumPartialsShader = Nothing
        End If
        For Each sh As Shader_Base_Class In {SharedRefractionNormalsSse, SharedRefractionNormalsFo4, SharedRefractionImageSpace, SharedSseOpaqueComposite, SharedSseDecalBaseMark}
            sh?.Dispose()
        Next
        SharedRefractionNormalsSse = Nothing : SharedRefractionNormalsFo4 = Nothing : SharedRefractionImageSpace = Nothing
        SharedSseOpaqueComposite = Nothing : SharedSseDecalBaseMark = Nothing
        SharedSseSao?.Dispose()
        SharedSseSao = Nothing
        SharedFo4Deferred?.Dispose()
        SharedFo4Deferred = Nothing

        If SharedLumResolveShader IsNot Nothing Then
            SharedLumResolveShader.Dispose()
            SharedLumResolveShader = Nothing
        End If

        If _bgVao > 0 Then
            GL.DeleteVertexArray(_bgVao)
            _bgVao = 0
        End If

        _targets.Free()
        _sceneDepth.Free()
        _refraction.Free()
        _opaqueComposite.Free()
        _saoDepth.Free()
        _sao.Free()
        _fo4.Free()
        _sseDecalBase.Free()
        Fo4EnvMap.Reset()
        If _saoPointSampler <> 0 Then GL.DeleteSampler(_saoPointSampler) : _saoPointSampler = 0
        If _gateWhite <> 0 Then GL.DeleteTexture(_gateWhite) : _gateWhite = 0
        _samplers?.Dispose() : _samplers = Nothing

        If SharedShadowFO4Shader IsNot Nothing Then
            SharedShadowFO4Shader.Dispose()
            SharedShadowFO4Shader = Nothing
        End If

        If SharedShadowSSEShader IsNot Nothing Then
            SharedShadowSSEShader.Dispose()
            SharedShadowSSEShader = Nothing
        End If

        If SharedGroundShadowShader IsNot Nothing Then
            SharedGroundShadowShader.Dispose()
            SharedGroundShadowShader = Nothing
        End If

        If ShadowTarget IsNot Nothing Then
            ShadowTarget.Dispose()
            ShadowTarget = Nothing
        End If

        If GroundShadowTarget IsNot Nothing Then
            GroundShadowTarget.Dispose()
            GroundShadowTarget = Nothing
        End If
        ' PONER EL CAMPO EN 0 DESPUES DE BORRAR. `Clean` corre DOS veces en el cierre normal (`MainForm` llama
        ' `Clean()` y enseguida `Dispose()`), y en la segunda pasada `_Model` ya es Nothing, asi que nadie hace
        ' current el contexto —el unico `EnsureContextCurrent` vive dentro de `Model.Clean`— y estos ocho
        ' `DeleteTexture` irian con ids viejos contra el contexto de OTRO PreviewControl vivo, donde esos mismos
        ' ids son texturas en uso (los nombres GL son por contexto).
        ' [AUDIT-CLEAN] es el gate: en la SEGUNDA pasada de Clean() los ocho tienen que venir ya en 0.
        ' Efecto lateral a tener presente: con el campo en 0, `BindTexture(..., defaultWhiteTex)` bindea 0, que
        ' en GL es NEGRO. Hoy no se dispara porque `BeginTeardown` levanta `_isTearingDown` y `RenderScene`/
        ' `OnPaint` salen antes de dibujar; si alguna vez se dibuja tras un Clean(), el sintoma es un modelo
        ' negro y el causante es esta linea, no el shader.
        If Logger.Enabled Then
            Dim a1 = defaultWhiteTex, a2 = defaultNormalTex, a3 = defaultFacegenDetailTex, a4 = defaultFacegenTintTex
            Dim a5 = defaultSseMsnSpecTex, a6 = defaultSseEngineGenericTex, a7 = defaultFacegenSubsurfaceTex, a8 = defaultCubeMap
            Logger.LogLazy(Function() $"[AUDIT-CLEAN] ids de defaults al entrar: {a1},{a2},{a3},{a4},{a5},{a6},{a7},{a8}")
        End If
        If defaultWhiteTex <> 0 Then GL.DeleteTexture(defaultWhiteTex) : defaultWhiteTex = 0
        If defaultNormalTex <> 0 Then GL.DeleteTexture(defaultNormalTex) : defaultNormalTex = 0
        If defaultFacegenDetailTex <> 0 Then GL.DeleteTexture(defaultFacegenDetailTex) : defaultFacegenDetailTex = 0
        If defaultFacegenTintTex <> 0 Then GL.DeleteTexture(defaultFacegenTintTex) : defaultFacegenTintTex = 0
        If defaultSseMsnSpecTex <> 0 Then GL.DeleteTexture(defaultSseMsnSpecTex) : defaultSseMsnSpecTex = 0
        If defaultSseEngineGenericTex <> 0 Then GL.DeleteTexture(defaultSseEngineGenericTex) : defaultSseEngineGenericTex = 0
        If defaultHelperTex <> 0 Then GL.DeleteTexture(defaultHelperTex) : defaultHelperTex = 0
        If defaultFacegenSubsurfaceTex <> 0 Then GL.DeleteTexture(defaultFacegenSubsurfaceTex) : defaultFacegenSubsurfaceTex = 0
        If defaultDissolvePatternTex <> 0 Then GL.DeleteTexture(defaultDissolvePatternTex) : defaultDissolvePatternTex = 0
        If defaultFo4DiffuseMapTex <> 0 Then GL.DeleteTexture(defaultFo4DiffuseMapTex) : defaultFo4DiffuseMapTex = 0
        If defaultCubeMap <> 0 Then GL.DeleteTexture(defaultCubeMap) : defaultCubeMap = 0
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
    Private ReadOnly DecalMeshes As New List(Of RenderableMesh)
    ' Decals that BLEND are drawn after the opaque ones and before the ordinary alpha geometry in both engines:
    ' FO4 G-buffer stage: groups 2/3 then group 4 (blended decal, test LESS_EQUAL no write; 0x1421D4AFB, 0x141855670);
    ' SSE: opaque decal group, then blended decal group, then the alpha list (0x14151EF40, 0x14151FD60, 0x14151FF00).
    Private ReadOnly DecalBlendedMeshes As New List(Of RenderableMesh)
    Private ReadOnly BlendedMeshes As New List(Of RenderableMesh)
    ' SSE: the billboard effects (group 0x12 -> list 0xD, drawn by the opaque finish after the opaque lists and before the
    ' decals, 0x14151EF40) and the shapes with no colour pass (SseRenderPassLaw: refraction lighting, kTempRefraction +
    ' kDynamicDecal effects, Hair + DEPTH_WRITE_DECALS without its PS) - those still go through the z-prepass.
    Private ReadOnly BillboardMeshes As New List(Of RenderableMesh)
    ' FO4: the opaque effects (bucket 0), drawn by the forward stage after the whole G-buffer, decals included.
    Private ReadOnly ForwardEffectMeshes As New List(Of RenderableMesh)
    Private ReadOnly NoColourPassMeshes As New List(Of RenderableMesh)
    Private ReadOnly BlendedDepthBuffer As New List(Of MeshDepth)
    Private RenderBucketsDirty As Boolean = True
    ''' <summary>O-ST (user decision 6-oct-2026: a stable order with the full key): where the game's cull walk registers a mesh - its
    ''' file, in the order the scene loads it (the first mesh of each NIF in <c>meshes</c>, which keeps the host's order,
    ''' LoadShapesParallel), then its place in the depth-first walk of that NIF (NiNode::OnVisible visits the children 0..n-1: SSE
    ''' 0x140EE2CC0 loop 0x140EE2D10..D33, FO4 0x1416BF8C0 loop 0x1416BF910..930), then its place in <c>meshes</c> (the same shape
    ''' loaded twice). A total order. A shape no node of its NIF reaches (the game's walk never registers it) goes after the reached
    ''' ones of its file, in block order (declared: the game does not draw it at all).</summary>
    Friend Structure RegistrationOrder
        Implements IComparable(Of RegistrationOrder)
        Public ReadOnly File As Integer
        Public ReadOnly Walk As Integer
        Public ReadOnly Load As Integer
        Public Sub New(file As Integer, walk As Integer, load As Integer)
            Me.File = file : Me.Walk = walk : Me.Load = load
        End Sub
        Public Function CompareTo(other As RegistrationOrder) As Integer Implements IComparable(Of RegistrationOrder).CompareTo
            Dim c = File.CompareTo(other.File)
            If c <> 0 Then Return c
            c = Walk.CompareTo(other.Walk)
            If c <> 0 Then Return c
            Return Load.CompareTo(other.Load)
        End Function
    End Structure

    ''' <summary>O-OP: a mesh's place in the game's opaque draw order. Slot = the list in the finish (SSE SseOpaqueSlot; FO4 0), Pass =
    ''' SSE pass ID / FO4 technique activation rank, SubList = SSE sub-list, NewestFirst = the list PREPENDS on register (drawn in
    ''' reverse registration order: the first registered is drawn last and wins a depth tie).</summary>
    Friend Structure OpaqueOrder
        Public Slot As Integer
        Public Pass As UInteger
        Public SubList As Integer
        Public NewestFirst As Boolean
    End Structure

    Private Shared Function CompareRegistration(x As RenderableMesh, y As RenderableMesh) As Integer
        Return x.RegistrationKey.CompareTo(y.RegistrationKey)
    End Function

    Private Shared Function CompareOpaqueDrawOrder(x As RenderableMesh, y As RenderableMesh) As Integer
        Dim a = x.OpaqueOrderKey, b = y.OpaqueOrderKey
        Dim c = a.Slot.CompareTo(b.Slot)
        If c <> 0 Then Return c
        c = a.Pass.CompareTo(b.Pass)
        If c <> 0 Then Return c
        c = a.SubList.CompareTo(b.SubList)
        If c <> 0 Then Return c
        c = x.RegistrationKey.CompareTo(y.RegistrationKey)
        Return If(a.NewestFirst, -c, c)
    End Function

    ''' <summary>Writes RenderableMesh.RegistrationKey for every mesh (RegistrationOrder). Once per RebuildRenderBuckets.</summary>
    Private Sub AssignRegistrationOrder()
        Dim files As New Dictionary(Of Nifcontent_Class_Manolo, (File As Integer, Walk As Dictionary(Of Integer, Integer)))(ReferenceEqualityComparer.Instance)
        For i = 0 To meshes.Count - 1
            Dim m = meshes(i)
            Dim shp = m?.MeshData?.Shape
            If shp Is Nothing Then Continue For
            Dim nif = shp.NifContent
            Dim file = Integer.MaxValue, walk = Integer.MaxValue
            If nif IsNot Nothing Then
                Dim e As (File As Integer, Walk As Dictionary(Of Integer, Integer)) = Nothing
                If Not files.TryGetValue(nif, e) Then
                    e = (files.Count, NifWalkOrder(nif))
                    files(nif) = e
                End If
                file = e.File
                Dim blockId As Integer = -1
                If shp.NifShape Is Nothing OrElse Not nif.GetBlockIndex(shp.NifShape, blockId) OrElse Not e.Walk.TryGetValue(blockId, walk) Then
                    walk = e.Walk.Count + Math.Max(blockId, 0)
                End If
            End If
            m.RegistrationKey = New RegistrationOrder(file, walk, i)
        Next
    End Sub

    ''' <summary>The depth-first walk of a NIF from its parentless nodes in block order, children 0..n-1 (the census rule of
    ''' oop_census.py dfs_order, which found it equal to block order in every SSE coplanar group): block index -&gt; position.</summary>
    Private Shared Function NifWalkOrder(nif As Nifcontent_Class_Manolo) As Dictionary(Of Integer, Integer)
        Dim order As New Dictionary(Of Integer, Integer)
        Dim hasParent As New HashSet(Of Integer)
        For Each b In nif.Blocks
            Dim n = TryCast(b, NiflySharp.Blocks.NiNode)
            If n?.Children Is Nothing Then Continue For
            For Each k In n.Children.Indices
                If k >= 0 Then hasParent.Add(k)
            Next
        Next
        ' Explicit stack (no recursion): children pushed in reverse so that child 0 is visited first. A block already visited is not
        ' visited again (a child under two parents: its first visit; a malformed cycle cannot hang the render).
        Dim stack As New Stack(Of Integer)
        For root = 0 To nif.Blocks.Count - 1
            If TypeOf nif.Blocks(root) IsNot NiflySharp.Blocks.NiNode OrElse hasParent.Contains(root) Then Continue For
            stack.Push(root)
            Do While stack.Count > 0
                Dim k = stack.Pop()
                If order.ContainsKey(k) Then Continue Do
                order(k) = order.Count
                Dim n = TryCast(nif.Blocks(k), NiflySharp.Blocks.NiNode)
                If n?.Children Is Nothing Then Continue Do
                Dim kids = n.Children.Indices.ToList()
                For j = kids.Count - 1 To 0 Step -1
                    If kids(j) >= 0 AndAlso kids(j) < nif.Blocks.Count Then stack.Push(kids(j))
                Next
            Loop
        Next
        Return order
    End Function

    ''' <summary>O-OP, Skyrim SE (D-L2): slot of the opaque finish 0x14151EF40, pass ID ascending, sub-list 0..4, reverse registration
    ''' inside a sub-list (prepend 0x14155F024..02B). List 0 (LOD landscape): insertion not traced - registration order (declared).
    ''' A shape without material (a helper the checkbox shows: the game draws none): after every game shape, registration order.</summary>
    Private Function SseOpaqueOrder(m As RenderableMesh) As OpaqueOrder
        Dim md = m.MeshData.Material
        If md?.MaterialBase Is Nothing Then Return New OpaqueOrder With {.Slot = Integer.MaxValue}
        Dim s = md.SseInputs()
        Dim slot = SseRenderPassLaw.OpaqueSlot(s)
        If slot = SseRenderPassLaw.SseOpaqueSlot.List0 Then Return New OpaqueOrder With {.Slot = slot}
        Return New OpaqueOrder With {.Slot = slot, .Pass = SseRenderPassLaw.PassId(s), .SubList = SseRenderPassLaw.SubList(s), .NewestFirst = True}
    End Function

    ''' <summary>O-OP, Fallout 4 (D-L3): the G-buffer techniques in order of first activation (0x1422201F0 links a technique at the
    ''' tail the first time its list gets a pass, 0x142220355..37F: the first registered shape of each technique, in registration
    ''' order), reverse registration inside a technique (prepend 0x142221F51..F58). A shape the game's cull never registers (C-ANC,
    ''' C-NAN, a helper: NifSceneVisibility) activates nothing and goes last (rev-07). A shape without a G-buffer technique (no pass,
    ''' a preview gap): its own rank, in registration order.</summary>
    Private Sub AssignFo4TechniqueActivation(list As List(Of RenderableMesh))
        Dim rank As New Dictionary(Of UInteger, UInteger)
        Dim nextRank As UInteger = 0
        For Each m In list.OrderBy(Function(x) x.RegistrationKey)
            Dim vis = NifSceneVisibility.Evaluate(m.MeshData.Shape)
            If vis.HiddenByAncestor OrElse vis.BoundCullReason IsNot Nothing OrElse m.MeshData.Shape.IsHelperShape Then
                m.OpaqueOrderKey = New OpaqueOrder With {.Pass = UInteger.MaxValue} : Continue For
            End If
            Dim md = m.MeshData.Material
            Dim t As UInteger? = Nothing
            If md?.MaterialBase IsNot Nothing AndAlso Not md.MaterialBase.IsBGEM() Then
                Dim g As Integer
                t = md.Fo4Technique(md.Fo4Flags(), g)
            End If
            Dim r As UInteger
            If t.HasValue Then
                If Not rank.TryGetValue(t.Value, r) Then r = nextRank : rank(t.Value) = r : nextRank += 1UI
                m.OpaqueOrderKey = New OpaqueOrder With {.Pass = r, .NewestFirst = True}
            Else
                m.OpaqueOrderKey = New OpaqueOrder With {.Pass = nextRank}
                nextRank += 1UI
            End If
        Next
    End Sub

    ''' <summary>GATE ONLY: the opaque list in draw order (ShadowGate --draw-order).</summary>
    Friend ReadOnly Property GateOpaqueDrawOrder As IReadOnlyList(Of RenderableMesh)
        Get
            Return OpaqueMeshes
        End Get
    End Property

    Public Sub MarkRenderBucketsDirty()
        RenderBucketsDirty = True
    End Sub

    Private Enum RenderBucket
        Opaque
        Decal
        DecalBlended
        Blended
        Billboard
        ForwardEffect
        NoColourPass
    End Enum

    ''' <summary>The colour-pass bucket of a material on a shape - ONE routing for the meshes and their overlay layers:
    ''' the list each game's pass law gives (SseRenderPassLaw / Fo4RenderPassLaw: the engine's group -> list map). FO4's
    ''' G-buffer bucket is one list: alpha test is a technique bit, ordered with the rest (O-OP, chunk C8). A wireframe
    ''' always goes with the blended draws.</summary>
    Private Function RouteBucket(material As RenderableMesh.MaterialData, isWireframe As Boolean) As RenderBucket
        If isWireframe Then Return RenderBucket.Blended
        If material Is Nothing OrElse material.MaterialBase Is Nothing Then Return RenderBucket.Opaque
        If FrameIsSse Then
            Select Case material.SsePass().List
                Case SseRenderPassLaw.SseList.OpaqueBatch : Return RenderBucket.Opaque
                Case SseRenderPassLaw.SseList.Billboard : Return RenderBucket.Billboard
                Case SseRenderPassLaw.SseList.DecalOpaque : Return RenderBucket.Decal
                Case SseRenderPassLaw.SseList.DecalTranslucent : Return RenderBucket.DecalBlended
                Case SseRenderPassLaw.SseList.Translucent : Return RenderBucket.Blended
                Case Else : Return RenderBucket.NoColourPass
            End Select
        End If
        Select Case material.Fo4Pass().List
            Case Fo4RenderPassLaw.Fo4List.GBufferOpaque : Return RenderBucket.Opaque   ' bucket 4: one list, technique order (O-OP D-L3)
            Case Fo4RenderPassLaw.Fo4List.DecalOpaque : Return RenderBucket.Decal
            Case Fo4RenderPassLaw.Fo4List.DecalTranslucent : Return RenderBucket.DecalBlended
            Case Fo4RenderPassLaw.Fo4List.ForwardEffect : Return RenderBucket.ForwardEffect
            Case Fo4RenderPassLaw.Fo4List.ForwardBillboard : Return RenderBucket.Billboard
            Case Fo4RenderPassLaw.Fo4List.Translucent : Return RenderBucket.Blended
            Case Else : Return RenderBucket.NoColourPass
        End Select
    End Function

    ''' <summary>The frame is drawn with the Skyrim SE shader (and so with its pass law).</summary>
    Friend ReadOnly Property FrameIsSse As Boolean
        Get
            Return TypeOf ParentControl.CurrentShader Is Shader_Class_SSE
        End Get
    End Property

    ''' <summary>The frame is Fallout 4's deferred frame: every FO4 frame except a debug view, which draws the shapes' raw values
    ''' through Fragment_FO4's debug block (rev-20: no forward lighting exists for a lighting shape).</summary>
    Friend ReadOnly Property FrameUsesFo4Deferred As Boolean
        Get
            Return Not FrameIsSse AndAlso Shader_Base_Class.DebugView = ShaderDebugView.None
        End Get
    End Property

    ''' <summary>RenderAll is drawing the lists of Fallout 4's G-buffer stage: a lighting shape draws with its record's program
    ''' (RenderableMesh.ColourDrawProgram), an effect decal only into RT 0x1A.</summary>
    Friend Property FrameGBufferStage As Boolean

    Private Sub RebuildRenderBuckets()
        OpaqueMeshes.Clear()
        DecalMeshes.Clear()
        DecalBlendedMeshes.Clear()
        BlendedMeshes.Clear()
        BillboardMeshes.Clear()
        ForwardEffectMeshes.Clear()
        NoColourPassMeshes.Clear()
        BlendedDepthBuffer.Clear()

        For Each mesh In meshes
            If IsNothing(mesh) OrElse IsNothing(mesh.MeshData) OrElse IsNothing(mesh.MeshData.Shape) Then Continue For
            ' S-W: a game switch (RenderBucketsGame) or a material edit that changes the technique (Editor_Form ForceRerender ->
            ' MarkRenderBucketsDirty) rebuilds the buckets; the TREE_ANIM rest pose of the positions follows it here.
            mesh.RefreshLeafPose_GL()
            Select Case RouteBucket(mesh.MeshData.Material, mesh.MeshData.Shape.Wireframe)
                Case RenderBucket.Opaque : OpaqueMeshes.Add(mesh)
                Case RenderBucket.Decal : DecalMeshes.Add(mesh)
                Case RenderBucket.DecalBlended : DecalBlendedMeshes.Add(mesh)
                Case RenderBucket.Blended : BlendedMeshes.Add(mesh)
                Case RenderBucket.Billboard : BillboardMeshes.Add(mesh)
                Case RenderBucket.ForwardEffect : ForwardEffectMeshes.Add(mesh)
                Case Else : NoColourPassMeshes.Add(mesh)
            End Select
        Next

        ' O-ST: every list in the game's registration order (a total key: List.Sort's instability cannot show). The opaque list in
        ' the game's opaque draw order (O-OP). Huecos declarados (registration order kept): SSE list 0 / list 0xD / decal lists 3-2,
        ' FO4 forward bucket 0 (the BSEffectShader technique of FO4 is not transcribed).
        AssignRegistrationOrder()
        If FrameIsSse Then
            For Each m In OpaqueMeshes : m.OpaqueOrderKey = SseOpaqueOrder(m) : Next
        Else
            AssignFo4TechniqueActivation(OpaqueMeshes)
        End If
        OpaqueMeshes.Sort(AddressOf CompareOpaqueDrawOrder)
        DecalMeshes.Sort(AddressOf CompareRegistration)
        DecalBlendedMeshes.Sort(AddressOf CompareRegistration)
        BlendedMeshes.Sort(AddressOf CompareRegistration)
        BillboardMeshes.Sort(AddressOf CompareRegistration)
        ForwardEffectMeshes.Sort(AddressOf CompareRegistration)
        NoColourPassMeshes.Sort(AddressOf CompareRegistration)
        RenderBucketsGame = FrameIsSse

        RenderBucketsDirty = False
    End Sub

    ' The game the buckets were routed for: switching game re-routes them.
    Private RenderBucketsGame As Boolean
    Public Class Texture_Loaded_Class
        Public Property Loaded As Boolean = False
        Public Property Cubemap As Boolean = False
        Public Property Path As String = ""
        Public Property Size As New Size
        Public Property DGXFormat_Original As Integer
        Public Property DGXFormat_Final As Integer
        Private _textureId As Integer
        ''' <summary>The GL texture of this entry. ASSIGNING IT (any value, the same number included: GL names are
        ''' recycled) drops the colour-space views of the previous storage.</summary>
        Public Property Texture_ID As Integer
            Get
                Return _textureId
            End Get
            Set(value As Integer)
                ReleaseViews()
                _textureId = value
                _loaderStorage = False
            End Set
        End Property

        ' True only while Texture_ID is the storage the DDS loader uploaded (DirectXDDSLoader marks it). A
        ' composer that swaps Texture_ID (FaceTint, SSE fold, rollback) clears it through the setter: its
        ' textures hold values in their own convention (e.g. linear values in RGBA8), never re-interpreted.
        Private _loaderStorage As Boolean

        ''' <summary>Called by the DDS loader on the entry it creates for the texture it uploaded.</summary>
        Friend Sub MarkLoaderStorage()
            _loaderStorage = _textureId <> 0
        End Sub

        ' Views of the storage in the other colour space (DirectXDDSLoader.CreateColorSpaceView): -1 = not probed,
        ' 0 = no view applies (bind the storage), > 0 = the view.
        Private _viewSrgb As Integer = -1
        Private _viewRaw As Integer = -1

        ''' <summary>The texture to bind for a slot the engine samples sRGB (<paramref name="wantSrgb"/>) or raw:
        ''' the storage when it already is in that space (or cannot be viewed), else a view of it, created once.</summary>
        Friend Function ColorSpaceView(wantSrgb As Boolean) As Integer
            If _textureId = 0 OrElse OwnedByComposer OrElse Not _loaderStorage Then Return _textureId
            Dim cached = If(wantSrgb, _viewSrgb, _viewRaw)
            If cached < 0 Then
                cached = DirectXDDSLoader.CreateColorSpaceView(_textureId, Cubemap, wantSrgb, DGXFormat_Original)
                If wantSrgb Then _viewSrgb = cached Else _viewRaw = cached
            End If
            Return If(cached > 0, cached, _textureId)
        End Function

        ''' <summary>Deletes the views (the context must be current). Called by every path that deletes or
        ''' replaces the storage.</summary>
        Friend Sub ReleaseViews()
            If _viewSrgb > 0 Then GL.DeleteTexture(_viewSrgb)
            If _viewRaw > 0 Then GL.DeleteTexture(_viewRaw)
            _viewSrgb = -1 : _viewRaw = -1
        End Sub

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
        ''' <summary>Location 11: always a copy of MeshData.Meshgeometry.Normals, the pre-skin normals (the SSE bit-26 skinned effect VS reads
        ''' the bind pose for the SAO normals target; RE_SAO_BOTH 12.7).</summary>
        Private vboPreSkinNormal As Integer
        ''' <summary>Location 12 (Vertex_SSE vertexEyeCenter, S-O1): per vertex, the SSE eye VS's reflection centre - bind space
        ''' (the GPU-skinning upload) or through the vertex's skin matrix (CPU skinning). 0 when the shape has no eye centres
        ''' (EyeCentresForUpload Nothing).</summary>
        Private vboEyeCenter As Integer
        ''' <summary>vboEyeCenter holds the bind-space centres. They have no writer after load (NIF shader block + eye data), so
        ''' the GPU-skinning uploads skip them while this holds.</summary>
        Private _eyeCentersAreBind As Boolean

        ''' <summary>GATE ONLY (no UI): ShadowGate --fo4-deferred-scene mutant. True leaves RT1..5 writable for an effect decal drawn
        ''' in the FO4 G-buffer stage.</summary>
        Friend Shared GateFo4EffectDecalMaskOpen As Boolean = False

        ''' <summary>GATE ONLY (no UI): ShadowGate --fo4-deferred-scene mutant. True enables blending on the app's shadow RT
        ''' (attachment 5) in a blended G-buffer draw.</summary>
        Friend Shared GateFo4BlendRt5 As Boolean = False

        ''' <summary>GATE ONLY: the location-11 buffer.</summary>
        Friend ReadOnly Property GatePreSkinNormalVbo As Integer
            Get
                Return vboPreSkinNormal
            End Get
        End Property
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

        ''' <summary>O-ST / O-OP (chunk C8): where the game's cull walk registers this mesh (PreviewModel.AssignRegistrationOrder).</summary>
        Friend RegistrationKey As RegistrationOrder
        ''' <summary>O-OP: this mesh's key in the game's opaque draw order (PreviewModel.RebuildRenderBuckets).</summary>
        Friend OpaqueOrderKey As OpaqueOrder
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
        ' Último Shape.OccluderSlotMask y Shape.OcclusionAsWornItem observados. Los dos cambian el resultado
        ' de la oclusión, así que entran al gate sucio igual que la máscara. Centinelas al revés del valor
        ' real posible (0xFFFFFFFF no es una máscara de un solo slot; True no es el default de la librería)
        ' para que la PRIMERA pasada siempre calcule.
        Private _lastOccluderSlotMask As UInteger = &HFFFFFFFFUI
        Private _lastOcclusionAsWornItem As Boolean = True
        ' Y el estado del slot occluder. ⛔ Entra al gate SÍ O SÍ: es lo que cambia al equipar o sacar el
        ' dispositivo, y sin esto un toggle deja el antebrazo con el estado viejo. Centinela True porque el
        ' default de la librería es False.
        Private _lastOccluderConDispositivo As Boolean = True
        Private _occlHidden As Boolean() = Nothing
        Private _occlEvaluated As Boolean = False

        ''' <summary>
        ''' El set de triángulos que la oclusión por segmento/partición dejó FUERA del draw, indexado
        ''' por índice de triángulo del shape (mismo orden que <c>Meshgeometry.Indices</c>).
        ''' <c>Nothing</c> = no hay nada oculto por esta vía.
        ''' <para>Es el mismo array que <see cref="EnsureZapIndexBuffer"/> usó para filtrar el element
        ''' buffer del último frame, expuesto para que un consumidor (el export a NIF) LEA lo que se
        ''' dibujó en vez de recalcularlo. Recalcularlo es reproducir el criterio, y dos copias del
        ''' criterio se desincronizan; esto no puede.</para>
        ''' <para>Sólo es significativo si <see cref="OcclusionEvaluated"/> es True: el cómputo vive
        ''' dentro de Render(), DESPUÉS del early-return por <c>RenderHide</c>, así que un shape que
        ''' nunca se dibujó no tiene valor válido acá.</para></summary>
        Public ReadOnly Property HiddenTriangles As Boolean()
            Get
                Return _occlHidden
            End Get
        End Property

        ''' <summary>True una vez que este mesh pasó por el cómputo de oclusión al dibujar. False =
        ''' <see cref="HiddenTriangles"/> no significa "nada oculto", significa "no se sabe".</summary>
        Public ReadOnly Property OcclusionEvaluated As Boolean
            Get
                Return _occlEvaluated
            End Get
        End Property

        Public Class MaterialData
            Sub New(Parent As MeshData_Class)
                ParentMeshData = Parent
            End Sub

            ''' <summary>GATE ONLY (no UI): ShadowGate --fo4-technique-law (rev-40 invariant). True makes Fo4Flags return the NIF's raw
            ''' F4SPF1 | F4SPF2 &lt;&lt; 32, without the BGSM applicator and the slot-42 fixer.</summary>
            Friend Shared GateFo4RawFlags As Boolean = False
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

            ''' <summary>SSE, camino PLEGADO: clave del diccionario de texturas donde vive el diffuse plegado de
            ''' ESTE NPC. Vacia (default) = camino normal, y entonces <see cref="DiffuseTexture_ID"/> se resuelve
            ''' como siempre, asi que FO4, Wardrobe y el SSE no plegado quedan byte-identicos.
            ''' <para>⛔ LA CLAVE ES PER-NPC, y por eso no hay contaminacion entre NPCs: instalar el resultado
            ''' del fold bajo la clave del COMPLEXION —que es COMPARTIDA entre shapes y entre NPCs de la misma
            ''' raza— hace que dos cabezas con el mismo complexion en un PreviewModel compartan el face-paint.
            ''' Es la MISMA ley que ya cumple el facetint.</para>
            ''' <para>⚠️ ES UNA CLAVE (String), NO un Texture_ID, a proposito: guardar el id crudo lo dejaria
            ''' COLGADO si alguien limpia el diccionario sin reconstruir este MaterialData, y samplear una
            ''' textura ya borrada da basura. Por clave, un diccionario limpio devuelve 0 y se cae solo al
            ''' complexion real.</para>
            ''' <para>⛔ Y NO se toca <c>MaterialBase.Diffuse_or_Base_Texture</c>: el material sigue apuntando al
            ''' complexion REAL. Es lo que impide la cara blanca - el loader pide los paths de
            ''' <see cref="Textures_Path_List"/> que no esten ya en el diccionario, asi que una ruta sintetica
            ''' tras un CleanTextures no existiria en disco y la shape saldria BLANCA.</para></summary>
            Public Property SseFoldedDiffuseKey As String = ""

            ''' <summary>Gemelo de <see cref="SseFoldedDiffuseKey"/> para el NORMAL (<c>_msn</c>) de la cabeza en
            ''' SSE: clave per-NPC bajo la que vive el <c>_msn</c> con los normales de los overlays de cara ya
            ''' plegados. "" = sin pliegue de normal (todo FO4, Wardrobe y el SSE sin overlays con normal) y el
            ''' bind cae al <c>_msn</c> real.
            ''' <para>Existe para que el PREVIEW muestre lo que el bake hornea: el bake ya plegaba el normal y el
            ''' render no lo hacia NUNCA, asi que un face-paint con relieve se horneaba pero no se veia. Mismas
            ''' razones de diseno que el diffuse: clave y no id, y el material sigue apuntando al real.</para></summary>
            Public Property SseFoldedNormalKey As String = ""

            ' (ELIMINADA `SseFoldDetailNeutralized`.) Era el flag "el amplify del detail ya está plegado en el
            ' diffuse, bindeá el neutro (63,64,63) en vez del 0.251". Quedó MUERTA cuando el fold dejó de
            ' neutralizar los slots 3/6 y pasó a PRE-COMPENSAR la cadena entera: desde entonces sus dos únicas
            ' asignaciones (NpcFaceTintResolver, camino plegado y no plegado) la ponían en False, así que la rama
            ' del render que la consultaba nunca se tomaba y `defaultFacegenFoldNeutralDetailTex` no se bindeaba
            ' jamás. Se fueron las tres cosas juntas: propiedad, rama y textura.

            ' OS-faithful blend decision. Two independent triggers, either suffices:
            '   1. NIF NiAlphaProperty.Flags.AlphaBlend (bit 0) — carried in the wrapper's
            '      AlphaBlendEnabled field (Apply'd from the shape's NiAlphaProperty at load).
            '   2. material.Alpha < 1.0 — the BGSM-level alpha multiplier. OS replicates
            '      this even when there is no NiAlphaProperty on the shape (GLShader.cpp:186):
            '        if (!alphaBlend && value < 1.0f) { glEnable(GL_BLEND); glBlendFunc(SrcAlpha, InvSrcAlpha); }
            ' Testigo: NIF sin NiAlphaProperty + BGSM Unknown + Alpha < 1 → OS blendea,
            ' la regla previa "enum-based" no — el enum Unknown perdía la independencia que
            ' el modelo de tres campos restauró, pero el render todavía consultaba el enum.
            ''' <summary>THE blend predicate: the engine-resolved blend bit OR the material alpha &lt; 1 (the engine routes
            ''' x = Alpha*fade &lt; 1 as blended too: Fallout4.exe 0x14217A485..A489, SkyrimSE 0x14151A47B..484). One formula, two
            ''' readers: <paramref name="engine"/> = True reads the RAW alpha (the game's law, EngineHasAlphaBlend), False the alpha
            ''' the preview draws (PreviewAlpha, HasAlphaBlend). The alpha is read after the guard: PreviewAlpha reads MaterialBase.</summary>
            Private Function AlphaBlend(engine As Boolean) As Boolean
                ' An overlay layer carries its own material in OverrideRelatedMaterial, so the
                ' "no material on the shape" guard must consult the override too — otherwise an
                ' overlay over a shape with no ShapeMaterial would wrongly report not-blended.
                If OverrideRelatedMaterial Is Nothing AndAlso IsNothing(ParentMeshData.Shape.ShapeMaterial) Then Return False
                Dim alpha = If(engine, MaterialBase.Alpha, PreviewAlpha())
                Return MaterialBase.ResolveEngineAlpha().Blend OrElse alpha < 1.0F
            End Function

            ''' <summary>The GAME's blend for this material (raw alpha). Read by what follows the engine and not the preview's
            ''' drawing - Wardrobe Manager's occlusion tool (OcclusionRaytracer.ResolverMaterial, OcclusionMask_Form).</summary>
            Public ReadOnly Property EngineHasAlphaBlend As Boolean
                Get
                    Return AlphaBlend(engine:=True)
                End Get
            End Property

            ''' <summary>The PREVIEW's blend for this material (PreviewAlpha): equal to EngineHasAlphaBlend except for an alpha-0 base
            ''' shape drawn with a forced alpha (C2 L5, both games: its controller's maximum in any view, 1 without a controller in a view
            ''' that draws for editing). Read by the drawing (buckets, face mode, bHasAlphaBlend).</summary>
            Public ReadOnly Property HasAlphaBlend As Boolean
                Get
                    Return AlphaBlend(engine:=False)
                End Get
            End Property

            ''' <summary>ref / 255 of the engine-resolved alpha state (ResolveEngineAlpha). Read by the occlusion tool
            ''' (OcclusionRaytracer) and as the generic default upload of ApplyMaterial; the shadow pass does not read it (both
            ''' games' branches set its threshold). NOT an engine law, which is one exe float32 writer per pass (AlphaRefLaw): the
            ''' FO4 colour pass takes Fo4RenderPassLaw.ColourPassAlphaTest (MainPassAlphaThreshold / EffectAlphaThreshold), the FO4
            ''' shadow pass Fo4RenderPassLaw.ShadowPassAlphaTest (ShadowMapAlphaThreshold, the BSUtilityShader writer), the FO4
            ''' z-prepass PrepassAlphaThreshold; the SSE colour pass and prepass SseRenderPassLaw.Classify, the SSE shadow pass
            ''' SseRenderPassLaw.ShadowAlphaTest.</summary>
            Public ReadOnly Property AlphaTestThreshold As Single
                Get
                    Return MaterialBase.ResolveEngineAlpha().Threshold / 255.0F
                End Get
            End Property

            Public ReadOnly Property HasAlphaTest
                Get
                    If OverrideRelatedMaterial Is Nothing AndAlso IsNothing(ParentMeshData.Shape.ShapeMaterial) Then Return False
                    Return MaterialBase.ResolveEngineAlpha().Test
                End Get
            End Property

            ''' <summary>What Skyrim SE's pass law reads from this material and its shape (SseRenderPassLaw.SseShapeInputs).
            ''' The editable fields (alpha state, ZTest / ZWrite, decal, alpha, falloff, refraction, palette alpha, specular and - rev-05,
            ''' 7-oct-2026 - env map, eye env map, glow map, soft / rim / back light and tree anim; chunk C8 - two-sided, model-space
            ''' normals, anisotropic light and, on an effect, effect lighting, soft, weapon blood and palette colour: Create_From_Shader reads them from
            ''' these same SLSF1 / SLSF2 bits and Save_To_Shader writes them back) come from the material; the flags the material does not carry, from the NIF shader property's
            ''' SLSF1 / SLSF2.</summary>
            Friend Function SseInputs() As SseRenderPassLaw.SseShapeInputs
                Dim mb = MaterialBase
                Dim shp = ParentMeshData.Shape
                Dim sh = shp?.NifShader
                Dim f1 As UInteger = If(sh Is Nothing, 0UI, CUInt(sh.ShaderFlags_SSPF1))
                Dim f2 As UInteger = If(sh Is Nothing, 0UI, CUInt(sh.ShaderFlags_SSPF2))
                Dim bit = Function(v As UInteger, b As Integer) ((v >> b) And 1UI) <> 0UI
                Dim ea = mb.ResolveEngineAlpha()
                Dim geo = shp?.NifShape
                Return New SseRenderPassLaw.SseShapeInputs With {
                    .IsEffect = mb.IsBGEM(),
                    .Alpha = PreviewAlpha(),
                    .AlphaPropertyPresent = mb.NifAlphaPropertyPresent OrElse ea.Blend OrElse ea.Test,
                    .Blend = ea.Blend, .Test = ea.Test, .TestRef = ea.Threshold,
                    .ZTest = mb.ZBufferTest, .ZWrite = mb.ZBufferWrite,
                    .Decal = mb.RendersAsDecal(), .DynamicDecal = bit(f1, 27), .HairSoftLighting = bit(f1, 18),
                    .Refraction = mb.Refraction, .TempRefraction = bit(f1, 2),
                    .Falloff = mb.IsBGEM() AndAlso mb.FalloffEnabled,
                    .Billboard = bit(f2, 13), .NoTransparencyMultisampling = bit(f2, 22), .LodLandscape = bit(f2, 1),
                    .ProjectedUV = bit(f1, 23), .ParallaxOcclusion = bit(f1, 28), .MultiTextureLandscape = bit(f1, 14),
                    .MultiIndexSnow = bit(f2, 9), .Specular = mb.SpecularEnabled, .LitSkinned = bit(f1, 1),
                    .Slsf1Bit30 = bit(f1, 30), .LodObjects = bit(f2, 2), .NoLodLandBlend = bit(f2, 14), .Slsf2Bit28 = bit(f2, 28),
                    .TreeAnim = mb.Tree, .HdLodObjects = bit(f2, 31),
                    .EnvMap = mb.EnvironmentMapping, .FaceGenDetail = bit(f1, 10), .Parallax = bit(f1, 11), .Eye = mb.EyeEnvironmentMapping,
                    .FaceGenRgbTint = bit(f1, 21), .GlowMap = mb.Glowmap, .WeaponBlood = If(mb.IsBGEM(), mb.BloodEnabled, bit(f2, 17)), .MultiLayerParallax = bit(f2, 24),
                    .SoftLighting = mb.SubsurfaceLighting, .RimLighting = mb.RimLighting, .BackLighting = mb.BackLighting,
                    .HasNormals = geo IsNot Nothing AndAlso geo.HasNormals,
                    .PaletteAlpha = mb.IsBGEM() AndAlso mb.GrayscaleToPaletteAlpha AndAlso Not String.IsNullOrEmpty(mb.GreyscaleTexture),
                    .HasVertexColors = geo IsNot Nothing AndAlso geo.HasVertexColors,
                    .Skinned = SseRenderPassLaw.EffectSkinnedBit(f1, VertexDescValue(geo)),
                    .VertexColorsFlag = bit(f2, 5), .ModelSpaceNormals = mb.ModelSpaceNormals, .TwoSided = mb.TwoSided,
                    .PackedTangent = bit(f2, 8), .AnisoLighting = mb.AnisoLighting, .Slsf2Bit23 = bit(f2, 23),
                    .EffectLighting = mb.EffectLightingEnabled, .SoftEffect = mb.SoftEnabled,
                    .PaletteColor = mb.IsBGEM() AndAlso mb.GrayscaleToPaletteColor AndAlso Not String.IsNullOrEmpty(mb.GreyscaleTexture),
                    .EffectBaseTexture = mb.IsBGEM() AndAlso Not String.IsNullOrEmpty(mb.Diffuse_or_Base_Texture),
                    .AlphaSrc = CInt(ea.Src) And &HF, .AlphaDst = CInt(ea.Dst) And &HF}
            End Function

            ''' <summary>The NIF block's vertexDesc (geom+0x148), 0 for a shape without one. Not IShapeGeometry.IsSkinned (it also reports the app's synthetic skinning).</summary>
            Private Shared Function VertexDescValue(geo As NiflySharp.INiShape) As ULong
                Dim tri = TryCast(geo, NiflySharp.Blocks.BSTriShape)
                If tri Is Nothing OrElse tri.VertexDesc Is Nothing Then Return 0UL
                Return tri.VertexDesc.Value
            End Function

            ''' <summary>How Skyrim SE draws this material (SseRenderPassLaw.Classify).</summary>
            Friend Function SsePass() As SseRenderPassLaw.SsePass
                Return SseRenderPassLaw.Classify(SseInputs())
            End Function

            ''' <summary>Skyrim SE lighting technique type of this material (SseRenderPassLaw.LitTechniqueType; -1 for an effect). A
            ''' material without its base has no technique either: -1 (SseInputs reads the base).</summary>
            Friend Function SseLitTechniqueType() As Integer
                If MaterialBase Is Nothing Then Return -1
                Return SseRenderPassLaw.LitTechniqueType(SseInputs())
            End Function

            ''' <summary>What Fallout 4's pass law reads (Fo4RenderPassLaw.Fo4ShapeInputs): the material's editable fields,
            ''' and the F4SF1 / F4SF2 bits the material does not carry from the NIF shader property.</summary>
            Friend Function Fo4Inputs() As Fo4RenderPassLaw.Fo4ShapeInputs
                Dim mb = MaterialBase
                Dim sh = ParentMeshData.Shape?.NifShader
                Dim geo = ParentMeshData.Shape?.NifShape
                Dim indicesPresentes = ParentMeshData.Meshgeometry.Geometry IsNot Nothing
                ' The flags GetRenderPasses reads (rev-40: one owner, Fo4Flags; the BGSM applicator and the slot-42 fixer included).
                Dim ff = Fo4Flags()
                Dim f1 As UInteger = CUInt(ff And &HFFFFFFFFUL)
                Dim f2 As UInteger = CUInt(ff >> 32)
                Dim bit = Function(v As UInteger, b As Integer) ((v >> b) And 1UI) <> 0UI
                Dim ea = mb.ResolveEngineAlpha()
                ' rev-45 / v4 B-rev-02: a lighting shape's group AND technique are GetRenderPasses' own (Fo4Technique); the pass law
                ' reads the prepass mask, the depth mode and the vertex-alpha test from the technique bits, it re-derives none of them.
                Dim lightingGroup As Integer? = Nothing
                Dim lightingTechnique As UInteger? = Nothing
                Dim lightingNoPass = Fo4RenderPassLaw.Fo4NoPass.None
                If Not mb.IsBGEM() Then
                    Dim g As Integer
                    lightingTechnique = Fo4Technique(ff, g, lightingNoPass)
                    If lightingTechnique.HasValue Then lightingGroup = g
                End If
                Return New Fo4RenderPassLaw.Fo4ShapeInputs With {.LightingGroup = lightingGroup, .LightingTechnique = lightingTechnique,
                    .LightingNoPass = lightingNoPass,
                    .IsEffect = mb.IsBGEM(), .Alpha = PreviewAlpha(), .Blend = ea.Blend,
                    .ZTest = mb.ZBufferTest, .ZWrite = mb.ZBufferWrite,
                    .Decal = EngineDecal(False), .Refraction = mb.Refraction, .TempRefraction = bit(f1, 2),
                    .Falloff = mb.IsBGEM() AndAlso (mb.FalloffEnabled OrElse mb.FalloffColorEnabled),
                    .Billboard = bit(f2, 13), .PipboyScreen = bit(f2, 28),
                    .Soft = mb.IsBGEM() AndAlso mb.SoftEnabled AndAlso Not PreviewModel.GateDisableSoft,
                    .AlphaTest = ea.Test, .TestRef = ea.Threshold,
                    .HasColorStream = geo IsNot Nothing AndAlso geo.HasVertexColors,
                    .PrepassGeometry = geo IsNot Nothing AndAlso Fo4PrepassGeometryClass(geo) AndAlso indicesPresentes}
            End Function

            ''' <summary>F4SF1 | F4SF2 &lt;&lt; 32 as Fallout 4's GetRenderPasses reads them (rev-40: the one owner; Fo4Inputs and the
            ''' G-buffer technique read these): a lighting shape's go through Fo4GBufferTechnique.EffectiveFlags (the BGSM applicator
            ''' 0x14216AF90 when a material file was applied, else the flags of the block the save writes; then the slot-42 fixer
            ''' 0x1421793F0); an effect's are the NIF's.</summary>
            Friend Function Fo4Flags() As ULong
                Dim sh = ParentMeshData.Shape?.NifShader
                Dim raw As ULong = If(sh Is Nothing, 0UL, CULng(CUInt(sh.ShaderFlags_F4SPF1)) Or (CULng(CUInt(sh.ShaderFlags_F4SPF2)) << 32))
                If GateFo4RawFlags Then Return raw
                If MaterialBase Is Nothing OrElse MaterialBase.IsBGEM() Then Return raw
                Dim st = Fo4EngineState()
                Return If(st.HasValue, st.Value.Flags, raw)
            End Function

            Private _ctlFrame As Integer = -1
            Private _ctlChain As List(Of NiflySharp.Blocks.NiTimeController)
            Private _alphaCtlDone As Boolean
            Private _alphaCtl As (Present As Boolean, Max As Single?)
            Private _rest As MaterialRest

            ''' <summary>C10 rev-07: THE one walk of the base shape's shader controller chain per frame (NifContent.CadenaDeShader, the owner of
            ''' CadenasDeShape's DeShader - the shader chain alone, v2.2; PreviewModel.FrameSerial: edits are read on the next frame).
            ''' AlphaController and Rest read it, each evaluated lazily and at
            ''' most once per frame on it. Nothing for an overlay layer's material (no NIF property) or without a NIF shape.</summary>
            Private Function ShaderChain() As List(Of NiflySharp.Blocks.NiTimeController)
                Dim shp = ParentMeshData.Shape
                If OverrideRelatedMaterial IsNot Nothing OrElse shp?.NifContent Is Nothing OrElse shp.NifShape Is Nothing Then Return Nothing
                Dim frame = If(ParentMeshData.ParentMesh?.ParentModel Is Nothing, -2, ParentMeshData.ParentMesh.ParentModel.FrameSerial)
                If frame >= 0 AndAlso frame = _ctlFrame AndAlso _ctlChain IsNot Nothing Then Return _ctlChain
                _ctlChain = shp.NifContent.CadenaDeShader(shp.NifShape)
                _alphaCtlDone = False
                _rest = Nothing
                _ctlFrame = frame
                Return _ctlChain
            End Function

            ''' <summary>The base shape's shader alpha controller (NifContent.LightingAlphaController on ShaderChain), once per frame. An
            ''' overlay layer's material has no NIF property: none.</summary>
            Friend Function AlphaController() As (Present As Boolean, Max As Single?)
                Dim chain = ShaderChain()
                If chain Is Nothing Then Return (False, Nothing)
                If Not _alphaCtlDone Then
                    Dim shp = ParentMeshData.Shape
                    _alphaCtl = shp.NifContent.LightingAlphaController(shp.NifShape, chain)
                    _alphaCtlDone = True
                End If
                Return _alphaCtl
            End Function

            ''' <summary>C10: the base shape's material at rest (NifContent.MaterialAtRest on ShaderChain), once per frame. An overlay
            ''' layer's material: none.</summary>
            Friend Function Rest() As MaterialRest
                Dim chain = ShaderChain()
                If chain Is Nothing Then Return MaterialRest.None
                If _rest Is Nothing Then _rest = ParentMeshData.Shape.NifContent.MaterialAtRest(chain, isFo4:=Not FrameSse())
                Return _rest
            End Function

            ''' <summary>The game this frame draws (PreviewModel.FrameIsSse); a material outside any model: Fallout 4.</summary>
            Private Function FrameSse() As Boolean
                Dim pm = ParentMeshData.ParentMesh?.ParentModel
                Return pm IsNot Nothing AndAlso pm.FrameIsSse
            End Function

            ''' <summary>C10: does a shader controller of this shape write <paramref name="v"/> at rest? Rest() has it and the engine's update
            ''' writes it - Fallout 4's lighting var 8 (EnvMapScale) only on an Envmap material (feature 1, 0x14224A8FB..904, the feature
            ''' from vfunc +0x28 at 0x14224A8D9; Fo4EngineState, the file's material); Skyrim SE writes every variable without asking the
            ''' feature (0x14157ACE4..0x14157AD41). It reads Fo4EngineState: the rest values are applied to the draw's copy (CR-7), never
            ''' inside Fo4EngineState (RestWrites -&gt; Fo4EngineState would recurse).</summary>
            Friend Function RestWrites(v As RestVariable) As Boolean
                If Not Rest().Has(v) Then Return False
                If v <> RestVariable.EnvMapScale OrElse FrameSse() Then Return True
                Dim st = Fo4EngineState()
                Return st.HasValue AndAlso st.Value.Material.Feature = 1
            End Function

            ''' <summary>C10 rev-07: THE value the draws use for a float variable a shader controller can move - its rest value when one
            ''' writes it (RestWrites), else <paramref name="own"/> when given (the caller's value: the FO4 .bgsm refraction clamp, the FO4
            ''' G-buffer's engine material fields), else the material's field (MaterialRest.MaterialValue). Every draw-side reader of a
            ''' RestVariable float goes through here.</summary>
            Friend Function RestValue(v As RestVariable, Optional own As Single? = Nothing) As Single
                Dim mat = If(own.HasValue, own.Value, MaterialRest.MaterialValue(MaterialBase, v, FrameSse()))
                Return If(RestWrites(v), Rest().Value(v, mat), mat)
            End Function

            ''' <summary>C10: THE colour the draws use for a colour variable a shader controller can move - its rest value (0..1 floats, as
            ''' the engine stores it) when one writes it (RestWrites), else Nothing (the caller keeps the material's, through its colour law).</summary>
            Friend Function RestColourAt(v As RestVariable) As OpenTK.Mathematics.Vector3?
                Return If(RestWrites(v), Rest().Colour(v), CType(Nothing, OpenTK.Mathematics.Vector3?))
            End Function

            ''' <summary>The UV transform the draws use (RestValue of the four UV variables, C10).</summary>
            Friend Function RestUv() As (Offset As Vector2, Scale As Vector2)
                Return (New Vector2(RestValue(RestVariable.UOffset), RestValue(RestVariable.VOffset)),
                        New Vector2(RestValue(RestVariable.UScale), RestValue(RestVariable.VScale)))
            End Function

            ''' <summary>RestVariable's members, once (RestDrivenNotice: no Enum.GetValues per shape and frame, v2.2).</summary>
            Private Shared ReadOnly RestVariables As RestVariable() = [Enum].GetValues(Of RestVariable)()

            ''' <summary>C10 rev-04: this shape's reason in the frame's notice - the variables its controllers write at rest (RestWrites), by
            ''' name (MaterialRest.DisplayName), in RestVariable order; Nothing when none. Listed whether or not this shape's technique reads
            ''' the variable: the game's material holds the controller's value, and the editors show and save the block's.</summary>
            Friend Function RestDrivenNotice() As String
                If Rest().IsEmpty Then Return Nothing
                Dim sse = FrameSse()
                Dim names As List(Of String) = Nothing
                For Each v In RestVariables
                    If Not RestWrites(v) Then Continue For
                    If names Is Nothing Then names = New List(Of String)
                    names.Add(MaterialRest.DisplayName(v, sse))
                Next
                Return If(names Is Nothing, Nothing, String.Join(", ", names))
            End Function

            ''' <summary>The game's alpha branch (L1c) for this material on <paramref name="alpha"/>: Skyrim SE
            ''' SseRenderPassLaw.LightingAlphaSkipsPasses (0x14151A554, no decal exception), Fallout 4 Fo4RenderPassLaw.LightingAlphaSkipsPasses
            ''' with the engine's decal bits (0x14217A147 -> 0x14217A22A; EngineDecal = Fo4Flags AND 0xC000000). The notice asks it on the
            ''' raw alpha (EngineAlphaSkipsPasses) and on the drawn one (RenderableMesh.ReportUndrawn, "shown").</summary>
            Friend Function AlphaLawSkips(alpha As Single) As Boolean
                Dim mb = MaterialBase
                If mb Is Nothing Then Return False
                Dim pm = ParentMeshData.ParentMesh?.ParentModel
                If pm IsNot Nothing AndAlso pm.FrameIsSse Then Return SseRenderPassLaw.LightingAlphaSkipsPasses(mb.IsBGEM(), alpha)
                Return Fo4RenderPassLaw.LightingAlphaSkipsPasses(mb.IsBGEM(), alpha, EngineDecal(False))
            End Function

            ''' <summary>F-A0 / S-ND: the GAME builds no pass for this material because of its RAW alpha (MaterialBase.Alpha, the engine's
            ''' +0x80). Read by PreviewAlpha (whether to force) and RenderableMesh.AlphaZeroNotice (the notice). It reads neither
            ''' PreviewAlpha nor the technique.</summary>
            Friend Function EngineAlphaSkipsPasses() As Boolean
                Return MaterialBase IsNot Nothing AndAlso AlphaLawSkips(MaterialBase.Alpha)
            End Function

            ''' <summary>THE alpha the preview DRAWS this material with, and why (C2 v6 L5) - the one owner of that choice; PreviewAlpha is its
            ''' value, read by every drawing consumer (SseInputs, Fo4Inputs, Fo4Technique, Fo4GBufferPass's engine material -
            ''' AdditionalAlphaMaskRef, AlphaScale, SetupAlphaBlend -, the colour pass "alpha", the shadow pass "uMaterialAlpha",
            ''' HasAlphaBlend); the game's law on the raw alpha and the notice read MaterialBase.Alpha (EngineAlphaSkipsPasses). The raw
            ''' alpha, except for a base shape whose raw alpha the game builds no pass for: (1) an alpha controller whose highest value
            ''' (AlphaController.Max) is above 0 - that maximum, in both views, Animated (the game shows it while it animates; rev-04,
            ''' rev-26); (2) otherwise - no controller, no readable key, or a maximum of 0 or less - 1 in a view that draws for editing
            ''' (RenderIntent.DrawEngineSkippedForEditing: what the user edits is seen; aud-B-06, provisional) and the raw alpha in a
            ''' composite view (the game never draws it, L1c). A material outside any control (no ParentControl: a census, a tool) gets the
            ''' composite rule - RenderIntent's default (C2 v7, D-G1: one answer for one shape). Overlay layers (rev-03): the raw alpha.
            ''' PreviewModel.GateRawPreviewAlpha: the raw alpha.</summary>
            Friend Function PreviewAlphaRule() As (Alpha As Single, Animated As Boolean)
                Dim mb = MaterialBase
                Dim pm = ParentMeshData.ParentMesh?.ParentModel
                Dim pc = pm?.ParentControl
                If OverrideRelatedMaterial IsNot Nothing OrElse
                   PreviewModel.GateRawPreviewAlpha OrElse Not EngineAlphaSkipsPasses() Then Return (mb.Alpha, False)
                Dim ctl = AlphaController()
                ' Both views: the game shows it while its controller animates (rev-04) - the highest value it reaches, when above 0.
                If ctl.Present AndAlso ctl.Max.HasValue AndAlso ctl.Max.Value > 0.0F Then Return (ctl.Max.Value, True)
                ' Composite view - also a material outside any control (RenderIntent's default): nothing lifts it above 0, the game
                ' never draws it (L1c).
                If pc Is Nothing OrElse Not pc.Intent.DrawEngineSkippedForEditing Then Return (mb.Alpha, False)
                ' Piece view: the shape the user edits is seen (aud-B-06, provisional).
                Return (1.0F, False)
            End Function

            ''' <summary>The alpha the preview draws (PreviewAlphaRule().Alpha).</summary>
            Friend Function PreviewAlpha() As Single
                Return PreviewAlphaRule().Alpha
            End Function

            ''' <summary>The pass law's own answer (C2 v6 L7, aud-B-02): the game's alpha branch cut this lighting material's passes on the
            ''' alpha the preview DRAWS - Fo4Pass().NoPass / SsePass().NoPass = LightingAlphaZero (Fo4GBufferTechnique.Technique,
            ''' SseRenderPassLaw.Classify; Fo4GBufferTechnique.GateIgnoreAlphaBranch included). Read by the notice ("shown") and the
            ''' refraction pass (Refraction).</summary>
            Friend Function DrawnAlphaSkipsPasses(isSse As Boolean) As Boolean
                Dim mb = MaterialBase
                If mb Is Nothing OrElse mb.IsBGEM() Then Return False
                If isSse Then Return SsePass().NoPass = SseRenderPassLaw.SseNoPass.LightingAlphaZero
                Return Fo4Pass().NoPass = Fo4RenderPassLaw.Fo4NoPass.LightingAlphaZero
            End Function

            Private _fo4StateFrame As Integer = -1
            Private _fo4State As (Flags As ULong, Material As Fo4EngineLightingMaterial)?

            ''' <summary>A Fallout 4 lighting shape's flags and engine material (Fo4GBufferTechnique.EngineState), evaluated once per frame
            ''' (PreviewModel.FrameSerial: the app's edits between frames are read on the next one), from the NIF's block as loaded and
            ''' the material in memory (Fo4GBufferTechnique.EngineState). Nothing for an effect or without a lighting block.</summary>
            Friend Function Fo4EngineState() As (Flags As ULong, Material As Fo4EngineLightingMaterial)?
                Dim mb = MaterialBase
                Dim sh = ParentMeshData.Shape?.NifShader
                Dim lsh = TryCast(sh, NiflySharp.Blocks.BSLightingShaderProperty)
                If mb Is Nothing OrElse mb.IsBGEM() OrElse lsh Is Nothing Then Return Nothing
                Dim frame = If(ParentMeshData.ParentMesh?.ParentModel Is Nothing, -2, ParentMeshData.ParentMesh.ParentModel.FrameSerial)
                If frame >= 0 AndAlso frame = _fo4StateFrame Then Return _fo4State
                Dim geo = ParentMeshData.Shape?.NifShape
                Try
                    Dim raw As ULong = CULng(CUInt(lsh.ShaderFlags_F4SPF1)) Or (CULng(CUInt(lsh.ShaderFlags_F4SPF2)) << 32)
                    Dim skinned = geo IsNot Nothing AndAlso geo.SkinInstanceRef IsNot Nothing AndAlso Not geo.SkinInstanceRef.IsEmpty()
                    _fo4State = Fo4GBufferTechnique.EngineState(raw, mb, skinned, VertexDescValue(geo), lsh)
                    _fo4StateError = Nothing
                Catch ex As Exception
                    ' The engine material could not be built for this shape: a preview gap (reported, the shape leaves every pass).
                    _fo4State = Nothing
                    _fo4StateError = "engine material not built: " & ex.Message
                End Try
                _fo4StateFrame = frame
                Return _fo4State
            End Function

            Private _fo4StateError As String

            ''' <summary>Why Fo4EngineState is Nothing for a lighting shape (its build threw), else Nothing.</summary>
            Friend Function Fo4EngineStateError() As String
                Fo4EngineState()
                Return _fo4StateError
            End Function

            ''' <summary>TintColor's SKIN_TINT tone (0x142206AAB..0x142206B7C: the SkinTint material class only): the per-actor tone the
            ''' manager puts in SkinTintColor (WM: the material's), x TintColorScale, linearised (pow 2.2), and its strength SkinTintAlpha
            ''' (mat+0xCC). Not present when the tone is already composited into this mesh's diffuse (SkinToneBaked: the FaceGen face,
            ''' whose technique has no SKIN_TINT - census: the head is rec2956).</summary>
            Friend Function Fo4SkinTone() As (Linear As Vector3, Alpha As Single, Present As Boolean)
                Dim mb = MaterialBase
                If mb Is Nothing OrElse SkinToneBaked OrElse mb.ResolveEffectiveType() <> FO4UnifiedMaterial_Class.EffectiveLightingType.SkinTint Then
                    Return (Vector3.Zero, 0.0F, False)
                End If
                Return (Shader_Base_Class.Vector_to_Linear(ScaledTintSrgb(mb.SkinTintColor, mb.TintColorScale)), mb.SkinTintAlpha, True)
            End Function

            ''' <summary>The geometry carries an extra data named "BTED" (0x142507510, read by GetRenderPasses 0x14217A802).</summary>
            Private Function HasBtedExtraData() As Boolean
                Dim obj = TryCast(ParentMeshData.Shape?.NifShape, NiflySharp.Blocks.NiObjectNET)
                Dim nif = ParentMeshData.Shape?.NifContent
                If obj Is Nothing OrElse nif Is Nothing OrElse obj.ExtraDataList Is Nothing Then Return False
                For Each r In obj.ExtraDataList.References
                    If r Is Nothing OrElse r.Index < 0 OrElse r.Index >= nif.Blocks.Count Then Continue For
                    Dim ed = TryCast(nif.Blocks(r.Index), NiflySharp.Blocks.NiExtraData)
                    If ed IsNot Nothing AndAlso String.Equals(ed.Name?.String, "BTED", StringComparison.Ordinal) Then Return True
                Next
                Return False
            End Function

            ''' <summary>GetRenderPasses' deferred technique and group of this lighting material (Fo4GBufferTechnique.Technique on
            ''' <paramref name="f"/> = Fo4Flags(), the engine's NiAlphaProperty, the alpha the preview draws (PreviewAlpha: the raw
            ''' material alpha, or the shown one in a view that draws for editing - L1b / L1c / L5 of C2 v3), the skin bit and the "BTED"
            ''' extra data): the ONE owner of the lighting group (rev-45; Fo4Inputs and Fo4GBufferPass read it). Nothing = no G-buffer
            ''' pass (its reason in noPass, LightingAlphaZero included).</summary>
            Friend Function Fo4Technique(f As ULong, ByRef group As Integer, Optional ByRef noPass As Fo4RenderPassLaw.Fo4NoPass = Fo4RenderPassLaw.Fo4NoPass.None) As UInteger?
                Dim mb = MaterialBase
                Dim ea = mb.ResolveEngineAlpha()
                Dim alphaFlags As Integer? = If(ea.Present, ea.NiFlags(), CType(Nothing, Integer?))
                Return Fo4GBufferTechnique.Technique(f, (f And 2UL) <> 0UL, VertexDescValue(ParentMeshData.Shape?.NifShape), alphaFlags,
                                                     PreviewAlpha(), HasBtedExtraData(), group, noPass)
            End Function

            ''' <summary>The engine draws this lighting shape and the preview cannot (Fo4GBufferDraw.Gap): it leaves every pass of the
            ''' frame - prepass, G-buffer, shadow casters - and is listed in the notice (rev-55 a, user decision 5-oct-2026).</summary>
            Friend Function Fo4PreviewGap(programs As Fo4DeferredPrograms) As Boolean
                Dim d = Fo4GBufferPass(programs)
                Return d.HasValue AndAlso d.Value.Gap <> ""
            End Function

            ''' <summary>Is this a decal for the engine? A Fallout 4 lighting shape: bit 26 or 27 of its effective flags (Fo4Flags: the
            ''' applicator writes them from the Decal byte only when its 3rd argument is 0, a material swap / overlay keeps the NIF's;
            ''' GetRenderPasses tests flags AND 0xC000000, 0x14217A9F6). Otherwise (Skyrim SE, effects) the material's (RendersAsDecal).</summary>
            Friend Function EngineDecal(isSse As Boolean) As Boolean
                Dim mb = MaterialBase
                If mb Is Nothing Then Return False
                If Not isSse AndAlso Not mb.IsBGEM() Then Return (Fo4Flags() And &HC000000UL) <> 0UL
                Return mb.RendersAsDecal()
            End Function

            ''' <summary>Does the game build a SHADOW-MAP pass for this material? The pass laws' own answer on the inputs the colour
            ''' pass law reads (Fo4Inputs / SseInputs: the alpha the preview draws, the engine's NiAlphaProperty and flags):
            ''' Fo4RenderPassLaw.ShadowMapCasts / SseRenderPassLaw.ShadowMapCasts - no effect, no blend, no alpha below 1, no
            ''' refraction; a decal never (Fallout 4 by law; Skyrim SE a declared hole, kept out). Read by the caster filter
            ''' (PreviewModel.RenderShadowPass), next to CastShadows.</summary>
            Friend Function EngineShadowMapCasts(isSse As Boolean) As Boolean
                If MaterialBase Is Nothing Then Return False
                If isSse Then Return SseRenderPassLaw.ShadowMapCasts(SseInputs())
                Return Fo4RenderPassLaw.ShadowMapCasts(Fo4Inputs())
            End Function

            ''' <summary>The material's slot-4 texture entry (EnvmapTexturePath): what the EnvMapArray registers (rev-03 / rev-46).</summary>
            Friend Function EnvmapTexture() As Texture_Loaded_Class
                Dim tex As Texture_Loaded_Class = Nothing
                TryGetTexture(EnvmapTexturePath, tex)
                Return tex
            End Function

            ''' <summary>Fallout 4's G-buffer pass of this lighting material (propuesta v3 E1, v5): its technique (Fo4GBufferTechnique,
            ''' GetRenderPasses 0x14217A050), group, flags and the record's program. Nothing when GetRenderPasses builds no G-buffer pass.
            ''' A technique whose PS or VS is not in the shader cache comes back with MissingPrograms and no program (the engine does not
            ''' draw it: SetupTechnique aborts, Fo4GBufferTechnique.MissingEnginePrograms). A record the preview cannot draw yet, or whose
            ''' program failed to build, comes back with its Gap. Both are listed in the frame's notice (PreviewModel.ReportUndrawnShapes).</summary>
            Friend Function Fo4GBufferPass(programs As Fo4DeferredPrograms) As Fo4GBufferDraw?
                Dim frame = If(ParentMeshData.ParentMesh?.ParentModel Is Nothing, -2, ParentMeshData.ParentMesh.ParentModel.FrameSerial)
                If frame >= 0 AndAlso frame = _fo4PassFrame AndAlso programs Is _fo4PassPrograms Then Return _fo4Pass
                _fo4Pass = BuildFo4GBufferPass(programs)
                _fo4PassFrame = frame : _fo4PassPrograms = programs
                Return _fo4Pass
            End Function

            Private _fo4PassFrame As Integer = -1
            Private _fo4PassPrograms As Fo4DeferredPrograms
            Private _fo4Pass As Fo4GBufferDraw?

            ''' <summary>The body of Fo4GBufferPass, built once per frame and programs object (PreviewModel.FrameSerial, the same rule
            ''' as Fo4EngineState): the G-buffer draw, ReportUndrawn and the slot law (EngineSlotDefault) read one result.</summary>
            Private Function BuildFo4GBufferPass(programs As Fo4DeferredPrograms) As Fo4GBufferDraw?
                Dim mb = MaterialBase
                If mb Is Nothing OrElse mb.IsBGEM() OrElse programs Is Nothing Then Return Nothing
                Dim stateError = Fo4EngineStateError()
                If stateError IsNot Nothing Then Return New Fo4GBufferDraw With {.Gap = stateError}
                ' No BSLightingShaderProperty (e.g. a helper shape shown by "Render hidden shapes"): there is no engine lighting
                ' material, so there is no G-buffer pass - not a crash on the Nothing state below.
                If Not Fo4EngineState().HasValue Then Return Nothing
                Dim f = Fo4Flags()
                Dim group As Integer
                Dim t = Fo4Technique(f, group)
                If Not t.HasValue Then Return Nothing
                Dim missing = Fo4GBufferTechnique.MissingEnginePrograms(t.Value)
                If missing IsNot Nothing Then Return New Fo4GBufferDraw With {.Technique = t.Value, .Flags = f, .Group = group, .Gap = "", .MissingPrograms = missing}
                Dim psId = Fo4GBufferTechnique.PixelShaderId(t.Value)
                ' The engine material of THIS draw: its +0x80 is the alpha the preview draws (PreviewAlpha) - the engine's own for every
                ' shape the game draws (Fo4EngineMaterial copies MaterialBase.Alpha, Fo4EngineMaterial.vb:105 / :142), the shown one
                ' for a shape drawn for editing (L1b: AdditionalAlphaMaskRef.z, AlphaScale.x and SetupAlphaBlend read it). A Structure
                ' copy: the per-frame engine state (Fo4EngineState) keeps the raw value.
                Dim em = Fo4EngineState().Value.Material
                em.Alpha = PreviewAlpha()
                ' C10: the controllers write the engine material at rest (lighting float/colour tables 0x14224A940 /
                ' 0x14224B550), after load and fixer: the draw's copy, as the alpha above; Fo4EngineState keeps the file's.
                ' RestValue / RestColourAt on the engine's fields (RestWrites: var 8 only on feature 1, 0x14224A8FB..904).
                em.Smoothness = RestValue(RestVariable.Glossiness, em.Smoothness)
                em.SpecularMult = RestValue(RestVariable.SpecularStrength, em.SpecularMult)
                em.SpecularColor = If(RestColourAt(RestVariable.SpecularColor), em.SpecularColor)
                em.MaterialD0 = RestValue(RestVariable.EnvMapScale, em.MaterialD0)
                em.EmissiveMult = RestValue(RestVariable.LightingEmissiveMultiple, em.EmissiveMult)
                em.EmissiveColor = If(RestColourAt(RestVariable.LightingEmissiveColor), em.EmissiveColor)
                Dim d As New Fo4GBufferDraw With {.Technique = t.Value, .Flags = f, .Group = group, .Gap = Fo4GBufferSource.Records(psId).Gap,
                                                  .Material = em}
                If d.Gap = "" Then
                    Dim errorText As String = Nothing
                    d.Program = programs.GBufferProgram(psId, errorText)
                    If d.Program Is Nothing Then d.Gap = "program build failed: " & errorText
                End If
                Return d
            End Function

            ''' <summary>The FO4 z-prepass takes only these exact geometry classes (RTTI 0x143449778 BSTriShape, 0x1438DF4C0
            ''' BSSubIndexTriShape, 0x143E71B70 BSMeshLODTriShape; instanced geometry is runtime-only).</summary>
            Private Shared Function Fo4PrepassGeometryClass(geo As NiflySharp.INiShape) As Boolean
                Dim n = geo.GetType().Name
                Return n = "BSTriShape" OrElse n = "BSSubIndexTriShape" OrElse n = "BSMeshLODTriShape"
            End Function

            ''' <summary>What the sampler law reads (SamplerLaw.SamplerInputs): the material's clamp value - lighting from the NIF's
            ''' BSLightingShaderProperty Texture Clamp Mode, effect from byte 0 of BSEffectShaderProperty's; on FO4 a material file
            ''' that the engine applied overrides it with bTileU * 2 + bTileV -, the FaceGen technique, the env map min LOD.</summary>
            Friend Function SamplerInputs(isSse As Boolean) As SamplerLaw.SamplerInputs
                Dim mb = MaterialBase
                Dim sh = ParentMeshData.Shape?.NifShader
                Dim esEfecto = mb.IsBGEM()
                Dim clamp = 3
                Dim lsp = TryCast(sh, NiflySharp.Blocks.BSLightingShaderProperty)
                Dim esp = TryCast(sh, NiflySharp.Blocks.BSEffectShaderProperty)
                If lsp IsNot Nothing Then clamp = CInt(lsp.TextureClampMode) And 3
                If esp IsNot Nothing Then clamp = CInt(esp.TextureClampMode) And 3
                If Not isSse AndAlso mb.ArchivoFo4Aplicado() Then clamp = If(mb.TileU, 2, 0) + If(mb.TileV, 1, 0)
                Return New SamplerLaw.SamplerInputs With {.IsSse = isSse, .IsEffect = esEfecto, .Clamp = clamp,
                                                         .Facegen = isSse AndAlso Not esEfecto AndAlso mb.Facegen,
                                                         .EnvMapMinLod = mb.EnvmapMinLOD,
                                                         .LitType = If(isSse, SseLitTechniqueType(), -1)}
            End Function

            ''' <summary>How Fallout 4 draws this material (Fo4RenderPassLaw.Classify).</summary>
            Friend Function Fo4Pass() As Fo4RenderPassLaw.Fo4Pass
                Return Fo4RenderPassLaw.Classify(Fo4Inputs())
            End Function

            ''' <summary>Whether and how this material refracts (RefractionLaw): the material's refraction fields, the
            ''' NIF's SLSF1 / F4SF1 bits 1, 2, 12, 13, 16 and SLSF2 / F4SF2 bits 5, 28, 31.</summary>
            Friend Function Refraction(isSse As Boolean) As RefractionLaw.RefractionPass
                Dim mb = MaterialBase
                Dim sh = ParentMeshData.Shape?.NifShader
                ' FO4: the flags GetRenderPasses reads (v4 B-rev-01: Fo4Flags, the one owner - applicator / inline material and the
                ' slot-42 fixer, RE_REFRACTION_BOTH 4.1 builds the technique on them); SSE: the NIF's.
                Dim ff As ULong = If(isSse, 0UL, Fo4Flags())
                Dim f1 As UInteger = If(isSse, If(sh Is Nothing, 0UI, CUInt(sh.ShaderFlags_SSPF1)), CUInt(ff And &HFFFFFFFFUL))
                Dim f2 As UInteger = If(isSse, If(sh Is Nothing, 0UI, CUInt(sh.ShaderFlags_SSPF2)), CUInt(ff >> 32))
                Dim bit = Function(v As UInteger, b As Integer) ((v >> b) And 1UI) <> 0UI
                Dim esEfecto = mb.IsBGEM()
                ' Strength: SSE lighting material+0x84 raw (NIF); FO4 lighting clamp(fRefractionPower, 0, 1) from a .bgsm
                ' (0x14216B850..86D), raw from the NIF; FO4 effect the .bgem fRefractionPower unsaturated (0x14216BD09..D2C).
                Dim fuerzaMat = mb.RefractionPower
                If Not isSse AndAlso Not esEfecto AndAlso mb.ArchivoFo4Aplicado() Then fuerzaMat = Math.Min(1.0F, Math.Max(0.0F, fuerzaMat))
                ' C10: the controller (lighting var 0) writes +0x84 after the load, raw: the .bgsm clamp above does not reach it.
                Dim fuerza = RestValue(RestVariable.Refraction, fuerzaMat)
                Dim pase = RefractionLaw.Classify(New RefractionLaw.RefractionInputs With {
                    .IsSse = isSse, .IsEffect = esEfecto,
                    .Refraction = mb.Refraction, .TempRefraction = bit(f1, 2),
                    .RefractionFalloff = mb.RefractionFalloff OrElse bit(f1, 16),
                    .ClampVariant = bit(f1, 13), .ModelSpaceNormals = mb.ModelSpaceNormals, .Skinned = bit(f1, 1),
                    .VertexColors = bit(f2, 5), .PipboyScreen = bit(f2, 28), .RefractionWritesDepth = bit(f2, 31),
                    .Strength = fuerza})
                ' C2 v6 L1c (aud-B-01): the game's alpha branch clears the WHOLE list, the refraction utility pass included - FO4 0x14217A147
                ' before 0x14217A566, SSE 0x14151A554 before 0x14151A6BB: a lighting shape the pass law cut for its drawn alpha has no
                ' refraction pass either (DrawnAlphaSkipsPasses, the law's own answer).
                If pase.Applies AndAlso DrawnAlphaSkipsPasses(isSse) Then pase.Drawn = False
                Return pase
            End Function

            ''' <summary>El NIF trae color por vertice Y el usuario tiene el toggle prendido. Es el
            ''' predicado del uniform <c>bShowVertexColor</c>.</summary>
            Friend ReadOnly Property UseVertexColor As Boolean
                Get
                    Dim shp = ParentMeshData.Shape
                    If shp Is Nothing OrElse Not shp.ShowVertexColor Then Return False
                    ' Meshgeometry es una Structure (SkinnedGeometry): no admite `?.`, y no puede ser Nothing.
                    Dim geom = ParentMeshData.Meshgeometry.Geometry
                    Return geom IsNot Nothing AndAlso geom.HasVertexColors
                End Get
            End Property

            ''' <summary>Idem, MENOS los TreeAnim: ahi el alpha de vertice es un parametro de viento, no
            ''' transparencia. Es el predicado del uniform <c>bShowVertexAlpha</c>.
            ''' <para>Existe como propiedad —y no inline en ApplyMaterial, que es de donde salio— porque
            ''' el PASE DE SOMBRA necesita el MISMO valor: <c>vColor.a</c> es el lado izquierdo del
            ''' alpha-test, y si los dos pases no coinciden la silueta que castea deja de ser la que se
            ''' dibuja (un cutout casteando el quad entero).</para></summary>
            Friend ReadOnly Property UseVertexAlpha As Boolean
                Get
                    If Not UseVertexColor Then Return False
                    Dim mb = MaterialBase
                    If mb Is Nothing Then Return True
                    Return Not (mb.Tree OrElse mb.NifShaderType = NiflySharp.Enums.BSLightingShaderType.TreeAnim)
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
                Dim r = MaterialBase.ResolveEngineAlpha()
                If r.Blend Then
                    Return {CInt(MapAlphaFunctionToBlendingFactor(r.Src)),
                            CInt(MapAlphaFunctionToBlendingFactor(r.Dst))}
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


            ''' <summary>The cube a FO4 material (lighting or effect) binds when it turns environment mapping on and
            ''' names no cube or a file that does not exist: Textures\Shared\Cubemaps\EyeCubeMap.dds (string
            ''' 0x142904438; ctor default 0x14222435B, load fallback 0x1421825B6 via 0x142181B38) - the file of
            ''' EngineDefaultTextureLaw.EngineTexture.Fo4EyeCubeMap (MaterialSlot.Cube).</summary>
            Friend Const EngineEffectDefaultCube As String = "Textures\Shared\Cubemaps\EyeCubeMap.dds"

            ''' <summary>The envmap path this material binds: the declared one, or, for a FO4 material with environment
            ''' mapping whose cube is missing, the engine's default cube.</summary>
            Public ReadOnly Property EnvmapTexturePath As String
                Get
                    Dim declared = FO4UnifiedMaterial_Class.CorrectTexturePath(MaterialBase.EnvmapTexture)
                    ' FO4 lighting AND effect: an empty or unresolvable slot-4 path loads EyeCubeMap (0x142182420 -> 0x1421825B6;
                    ' lighting 0x14216A257..288, effect 0x142224B20..41; Tools/re-docs/RE_FO4_WORLD_ENVMAP_2026-10-03.md 9.2, 19).
                    If Config_App.Current.Game = Config_App.Game_Enum.Skyrim OrElse Not MaterialBase.EnvironmentMapping Then Return declared
                    If TextureFileExists(declared) Then Return declared
                    Return FO4UnifiedMaterial_Class.CorrectTexturePath(EngineEffectDefaultCube)
                End Get
            End Property

            ''' <summary>A FO4 material slot in the colour space the engine loads it (the sRGB flag each material
            ''' class passes per slot, see ApplyMaterial). <paramref name="id"/> must be the id this entry resolves
            ''' to (else it is returned unchanged).</summary>
            Friend Function SlotTextureInColorSpace(path As String, id As UInteger, srgb As Boolean) As UInteger
                If id = 0 OrElse String.IsNullOrEmpty(path) Then Return id
                Dim tex As Texture_Loaded_Class = Nothing
                If Not TryGetTexture(path, tex) OrElse tex Is Nothing OrElse CUInt(tex.Texture_ID) <> id Then Return id
                Return CUInt(tex.ColorSpaceView(srgb))
            End Function

            ''' <summary>THE predicate of "absent" (rev-03): the file is not in the game's data (archives + loose,
            ''' FilesDictionary_class.TryGetEntry). Shared by EnvmapTexturePath and the slot law.</summary>
            Friend Shared Function TextureFileExists(path As String) As Boolean
                Return Not String.IsNullOrEmpty(path) AndAlso FilesDictionary_class.TryGetEntry(path) IsNot Nothing
            End Function

            ''' <summary>The GL id of <paramref name="path"/>'s entry when it is a 2D texture (a cube in a 2D slot would be
            ''' GL_INVALID_OPERATION: 0, the caller falls to its default): the one body of EnvmapMaskTexture_ID / FlowTexture_ID and of
            ''' ApplyMaterial's mask (rev-06).</summary>
            Friend Function Texture2DId(path As String) As UInteger
                If String.IsNullOrEmpty(path) Then Return 0
                Dim tex As Texture_Loaded_Class = Nothing
                If Not TryGetTexture(path, tex) OrElse tex Is Nothing OrElse tex.Cubemap Then Return 0
                Return CUInt(tex.Texture_ID)
            End Function

            ''' <summary>The declared path of <paramref name="slot"/> (CorrectTexturePath'd): the field each game's reader fills
            ''' (FO4UnifiedMaterial_Class.ReadBgsmTexturesFromTextureSet; BGSM v1/v2 string 7 sWrinklesTexture = slot 5, reader map
            ''' 0x14216CA37..CE67). The env slot's own path, not the EyeCubeMap EnvmapTexturePath falls back to. The environment mask
            ''' (rev-06, the one owner of that choice): in Skyrim SE it is texture-set slot 5, stored in FlowTexture (EnvmapMaskTexture is
            ''' filled only by the Fallout 4 reader); the same material offset means the env mask in the reflective classes
            ''' (Envmap / Eye / MultiLayerParallax) and the detail in Facegen, each class with its own OnLoadTextureSet. Slot 5 rules
            ''' when it NAMES a path; else the native field, which two paths do fill (a BGEM, and a BGSM read from disk with its JSON
            ''' sidecar). Fallout 4: the native field.</summary>
            Friend Function SlotPath(slot As EngineDefaultTextureLaw.MaterialSlot) As String
                Dim mb = MaterialBase
                Dim tp = Function(p As String) FO4UnifiedMaterial_Class.CorrectTexturePath(p)
                Select Case slot
                    Case EngineDefaultTextureLaw.MaterialSlot.Diffuse : Return tp(mb.Diffuse_or_Base_Texture)
                    Case EngineDefaultTextureLaw.MaterialSlot.Normal : Return tp(mb.NormalTexture)
                    Case EngineDefaultTextureLaw.MaterialSlot.Glow : Return tp(mb.GlowTexture)
                    Case EngineDefaultTextureLaw.MaterialSlot.Lightmask : Return tp(mb.LightingTexture)
                    Case EngineDefaultTextureLaw.MaterialSlot.Greyscale : Return tp(mb.GreyscaleTexture)
                    Case EngineDefaultTextureLaw.MaterialSlot.Cube : Return tp(mb.EnvmapTexture)
                    Case EngineDefaultTextureLaw.MaterialSlot.EnvMask
                        Dim flow = tp(mb.FlowTexture)
                        If Config_App.Current.Game = Config_App.Game_Enum.Skyrim AndAlso flow <> "" Then Return flow
                        Return tp(mb.EnvmapMaskTexture)
                    Case EngineDefaultTextureLaw.MaterialSlot.FaceSlot5 : Return tp(mb.WrinklesTexture)
                    Case EngineDefaultTextureLaw.MaterialSlot.Slot7 : Return tp(mb.SmoothSpecTexture)
                    ' SSE slot 3 is read to DisplacementTexture, slot 6 to InnerLayerTexture (FO4UnifiedMaterial_Class texture-set reader).
                    Case EngineDefaultTextureLaw.MaterialSlot.Height
                        Return If(Config_App.Current.Game = Config_App.Game_Enum.Skyrim, tp(mb.DisplacementTexture), "")
                    Case EngineDefaultTextureLaw.MaterialSlot.InnerLayer
                        Return If(Config_App.Current.Game = Config_App.Game_Enum.Skyrim, tp(mb.InnerLayerTexture), "")
                    Case Else : Return ""
                End Select
            End Function

            ''' <summary>THE recolor predicate of an effect (rev-14): technique bits 13 / 14 (Fallout 4, 0x142178150) and 19 / 20 (Skyrim
            ''' SE, 0x14152A2B0) = the GrayscaleToPalette flag AND a non-empty palette path (MEASURED: the path, not whether it loaded).
            ''' Read by the slot law (EngineSlotDefault) and bEffectGreyscaleAlpha of the colour pass.</summary>
            Friend Function EngineRecolors() As (Color As Boolean, Alpha As Boolean)
                Dim named = SlotPath(EngineDefaultTextureLaw.MaterialSlot.Greyscale) <> ""
                Return (MaterialBase.GrayscaleToPaletteColor AndAlso named, MaterialBase.GrayscaleToPaletteAlpha AndAlso named)
            End Function

            ''' <summary>EngineDefaultTextureLaw for <paramref name="slot"/> of this material: the game, the family and its context (FO4
            ''' lighting: the bits of its G-buffer record, the per-frame Fo4GBufferPass, its one owner). The one place a slot's engine
            ''' default is decided. C4 scope (decision 7-oct-2026, facetints closed): Skyrim SE Facegen's slot 2 stays the app's own
            ''' branch (ApplyMaterial) -> Own.</summary>
            Friend Function EngineSlotDefault(slot As EngineDefaultTextureLaw.MaterialSlot, state As EngineDefaultTextureLaw.SlotState) As EngineDefaultTextureLaw.EngineTexture
                Dim mb = MaterialBase
                Dim sse = Config_App.Current.Game = Config_App.Game_Enum.Skyrim
                If mb.IsBGEM() Then
                    Dim rc = EngineRecolors()
                    Dim ec As New EngineDefaultTextureLaw.EffectContext With {
                        .Recolor = rc.Color OrElse rc.Alpha,
                        .Envmap = Not sse AndAlso mb.EnvironmentMapping}
                    If sse Then Return EngineDefaultTextureLaw.SseEffect(slot, state, ec)
                    Return EngineDefaultTextureLaw.Fo4Effect(slot, state, ec)
                End If
                If Not sse Then
                    Dim c As New EngineDefaultTextureLaw.Fo4LightingContext With {.EnvironmentFlag = mb.EnvironmentMapping}
                    Dim d = Fo4GBufferPass(ParentMeshData.ParentMesh.ParentModel.ParentControl.SharedFo4Deferred)
                    If d.HasValue AndAlso d.Value.MissingPrograms Is Nothing AndAlso d.Value.Gap = "" Then
                        Dim t = d.Value.Technique
                        c.Textured = (t And Fo4GBufferTechnique.TechTexture) <> 0UI
                        c.GlowMap = (t And Fo4GBufferTechnique.TechGlowMap) <> 0UI
                        c.GradientRemap = (t And Fo4GBufferTechnique.TechGradientRemap) <> 0UI
                        c.Face = (t And Fo4GBufferTechnique.TechFace) <> 0UI
                    End If
                    Return EngineDefaultTextureLaw.Fo4Lighting(slot, state, c)
                End If
                If mb.Facegen AndAlso slot = EngineDefaultTextureLaw.MaterialSlot.Lightmask Then Return EngineDefaultTextureLaw.EngineTexture.Own
                Dim st = mb.NifShaderType
                Return EngineDefaultTextureLaw.SseLighting(slot, state, New EngineDefaultTextureLaw.SseLightingContext With {
                    .GlowClass = st = NiflySharp.Enums.BSLightingShaderType.GlowShader,
                    .EnvmapClass = st = NiflySharp.Enums.BSLightingShaderType.EnvironmentMap OrElse st = NiflySharp.Enums.BSLightingShaderType.EyeEnvmap,
                    .ParallaxClass = st = NiflySharp.Enums.BSLightingShaderType.Parallax,
                    .MultiLayerParallaxClass = st = NiflySharp.Enums.BSLightingShaderType.MultiLayerParallax,
                    .RimOrSoft = mb.RimLighting OrElse mb.SubsurfaceLighting,
                    .BackLighting = mb.BackLighting,
                    .SpecularMsn = mb.SpecularEnabled AndAlso mb.ModelSpaceNormals})
            End Function

            ''' <summary>The slot's state (rev-03): loaded / no path / absent from the data / in the data but not loaded by the preview.</summary>
            Friend Function SlotState(slot As EngineDefaultTextureLaw.MaterialSlot, ownId As UInteger) As EngineDefaultTextureLaw.SlotState
                Dim p = SlotPath(slot)
                Return EngineDefaultTextureLaw.StateOf(p, ownId <> 0, TextureFileExists(p))
            End Function

            ''' <summary>The texture the game samples in <paramref name="slot"/>: <paramref name="ownId"/> when it loaded, else the app's
            ''' copy of the engine's default; <paramref name="ownId"/> (0) when the engine has no texture of its own there or it is not
            ''' traced (NoTexture / PreviousDraw / NotTraced / a cube): the caller keeps its fallback (HEAD).</summary>
            Friend Function EngineSlotTextureId(slot As EngineDefaultTextureLaw.MaterialSlot, ownId As UInteger) As UInteger
                Dim d = EngineSlotDefault(slot, SlotState(slot, ownId))
                If d = EngineDefaultTextureLaw.EngineTexture.Own Then Return ownId
                Dim id = ParentMeshData.ParentMesh.ParentModel.ParentControl.EngineDefaultTextureId(d)
                Return If(id <> 0, CUInt(id), ownId)
            End Function

            ''' <summary>The recolor-without-base line (rev-07 / rev-11): C4's class "drawn differently by the preview" (the game draws
            ''' it, reading the previous draw's t0; the preview draws white).</summary>
            Friend Const RecolorWithoutBase As String = "Recolor without a base texture: drawn white; the game reads the previous draw's texture"

            ''' <summary>This material's texture lines of the frame's notice (rev-03 / rev-07 / rev-08 / rev-11):
            ''' <list type="bullet">
            ''' <item>every declared path absent from the data -&gt; MissingTexture (the path), always;</item>
            ''' <item>every declared path in the data that the preview did not load -&gt; DrawnDifferently (the game draws it, the preview
            ''' does not reproduce it: "Texture not loaded by the preview: path");</item>
            ''' <item>every EMPTY slot the game samples with a default that is not neutral (EngineDefaultTextureLaw.IsNeutral) -&gt;
            ''' EmptyTextureSlot (the slot's name);</item>
            ''' <item>an effect base that is empty under a recolor -&gt; DrawnDifferently (RecolorWithoutBase).</item>
            ''' </list>
            ''' A slot the SSE fold stands in for (DiffuseTexture_ID / NormalTexture_ID through a per-NPC key) counts as loaded.</summary>
            Friend Function TextureNotices() As List(Of (Kind As PreviewModel.FrameNoticeKind, Reason As String))
                Dim out As New List(Of (Kind As PreviewModel.FrameNoticeKind, Reason As String))
                Dim diffusePath = SlotPath(EngineDefaultTextureLaw.MaterialSlot.Diffuse), normalPath = SlotPath(EngineDefaultTextureLaw.MaterialSlot.Normal)
                For Each p In Textures_Path_List.Append(SlotPath(EngineDefaultTextureLaw.MaterialSlot.Cube)).Distinct(StringComparer.OrdinalIgnoreCase)
                    If p = "" OrElse GetTextureID(p) <> 0 Then Continue For
                    If String.Equals(p, diffusePath, StringComparison.OrdinalIgnoreCase) AndAlso DiffuseTexture_ID <> 0 Then Continue For
                    If String.Equals(p, normalPath, StringComparison.OrdinalIgnoreCase) AndAlso NormalTexture_ID <> 0 Then Continue For
                    If TextureFileExists(p) Then
                        out.Add((PreviewModel.FrameNoticeKind.DrawnDifferently, "Texture not loaded by the preview: " & p))
                    Else
                        out.Add((PreviewModel.FrameNoticeKind.MissingTexture, p))
                    End If
                Next
                For Each slot In [Enum].GetValues(GetType(EngineDefaultTextureLaw.MaterialSlot)).Cast(Of EngineDefaultTextureLaw.MaterialSlot)()
                    If SlotPath(slot) <> "" Then Continue For
                    Dim d = EngineSlotDefault(slot, EngineDefaultTextureLaw.SlotState.Empty)
                    Select Case d
                        Case EngineDefaultTextureLaw.EngineTexture.Own, EngineDefaultTextureLaw.EngineTexture.NoTexture, EngineDefaultTextureLaw.EngineTexture.NotTraced
                        Case EngineDefaultTextureLaw.EngineTexture.PreviousDraw
                            out.Add((PreviewModel.FrameNoticeKind.DrawnDifferently, RecolorWithoutBase))
                        Case Else
                            If Not EngineDefaultTextureLaw.IsNeutral(slot, d) Then out.Add((PreviewModel.FrameNoticeKind.EmptyTextureSlot, SlotNoticeName(slot)))
                    End Select
                Next
                Return out
            End Function

            ''' <summary>The name of <paramref name="slot"/> in the notice (UI English): the texture-set index of the lighting families,
            ''' the BGEM field of the effects.</summary>
            Private Function SlotNoticeName(slot As EngineDefaultTextureLaw.MaterialSlot) As String
                Dim effect = MaterialBase.IsBGEM()
                Select Case slot
                    Case EngineDefaultTextureLaw.MaterialSlot.Diffuse : Return If(effect, "base texture", "slot 0 (diffuse)")
                    Case EngineDefaultTextureLaw.MaterialSlot.Normal : Return If(effect, "normal texture", "slot 1 (normal)")
                    Case EngineDefaultTextureLaw.MaterialSlot.Glow : Return "slot 2 (glow)"
                    Case EngineDefaultTextureLaw.MaterialSlot.Lightmask : Return "slot 2 (subsurface / rim mask)"
                    Case EngineDefaultTextureLaw.MaterialSlot.Greyscale : Return If(effect, "greyscale texture", "slot 3 (greyscale palette)")
                    Case EngineDefaultTextureLaw.MaterialSlot.Cube : Return If(effect, "environment cube", "slot 4 (environment cube)")
                    Case EngineDefaultTextureLaw.MaterialSlot.EnvMask : Return If(effect, "environment mask", "slot 5 (environment mask)")
                    Case EngineDefaultTextureLaw.MaterialSlot.FaceSlot5 : Return "slot 5 (face)"
                    Case EngineDefaultTextureLaw.MaterialSlot.Height : Return "slot 3 (height)"
                    Case EngineDefaultTextureLaw.MaterialSlot.InnerLayer : Return "slot 6 (inner layer)"
                    Case EngineDefaultTextureLaw.MaterialSlot.Slot7
                        Return If(Config_App.Current.Game = Config_App.Game_Enum.Skyrim, "slot 7 (specular / back light)", "slot 7 (smooth-spec)")
                    Case Else : Return slot.ToString()
                End Select
            End Function

            Public ReadOnly Property Textures_Path_List As IEnumerable(Of String)
                Get
                    Return {FO4UnifiedMaterial_Class.CorrectTexturePath(MaterialBase.NormalTexture),
                     FO4UnifiedMaterial_Class.CorrectTexturePath(MaterialBase.Diffuse_or_Base_Texture),
                     FO4UnifiedMaterial_Class.CorrectTexturePath(MaterialBase.SmoothSpecTexture),
                     FO4UnifiedMaterial_Class.CorrectTexturePath(MaterialBase.GreyscaleTexture),
                     EnvmapTexturePath,
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
                    colors.Add(EnvmapTexturePath)
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
                    ' SSE plegado: el diffuse de ESTE NPC vive bajo una clave PER-NPC. Ver SseFoldedDiffuseKey.
                    ' Si la clave está vacía (todo FO4, Wardrobe, y el SSE no plegado) o el diccionario ya no la
                    ' tiene (post-CleanTextures), se cae al complexion real.
                    If Not String.IsNullOrEmpty(SseFoldedDiffuseKey) Then
                        Dim foldedId = GetTextureID(SseFoldedDiffuseKey)
                        If foldedId <> 0 Then Return foldedId
                    End If
                    Return GetTextureID(FO4UnifiedMaterial_Class.CorrectTexturePath(MaterialBase.Diffuse_or_Base_Texture))
                End Get
            End Property
            Public ReadOnly Property NormalTexture_ID As UInteger
                Get
                    ' SSE plegado: el _msn de ESTE NPC (con los normales de overlay compuestos) vive bajo una
                    ' clave PER-NPC. Espejo EXACTO de DiffuseTexture_ID — ver SseFoldedNormalKey. Clave vacía
                    ' (todo FO4, Wardrobe, SSE sin overlay-normal) o diccionario ya limpiado ⇒ se cae al _msn real.
                    If Not String.IsNullOrEmpty(SseFoldedNormalKey) Then
                        Dim foldedId = GetTextureID(SseFoldedNormalKey)
                        If foldedId <> 0 Then Return foldedId
                    End If
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
                    Return GetTextureID(EnvmapTexturePath)
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
                    Return Texture2DId(FO4UnifiedMaterial_Class.CorrectTexturePath(MaterialBase.EnvmapMaskTexture))
                End Get
            End Property
            Public ReadOnly Property FlowTexture_ID As UInteger
                Get
                    Return Texture2DId(FO4UnifiedMaterial_Class.CorrectTexturePath(MaterialBase.FlowTexture))
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
                    If Not TryGetTexture(EnvmapTexturePath, tex) Then Return False
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
            If vboPreSkinNormal > 0 Then GL.DeleteBuffer(vboPreSkinNormal) : vboPreSkinNormal = 0
            If vboEyeCenter > 0 Then GL.DeleteBuffer(vboEyeCenter) : vboEyeCenter = 0
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

        ''' <summary>
        ''' Resube el VBO de UV cuando un slider uv movio <c>Uvs_Weight</c>. El buffer es <c>StaticDraw</c>:
        ''' las UVs solo cambian por slider uv. Sin esta resubida el viewport muestra las UVs previas mientras
        ''' el .nif construido sale con las nuevas. Se sube entero — los arrays de UV son chicos y esto solo
        ''' corre con el flag prendido, no por frame.
        ''' </summary>
        Public Sub UpdateUvBuffer_GL()
            If MeshData Is Nothing Then Exit Sub
            Dim geom = MeshData.Meshgeometry
            If Not geom.UvsDirty Then Exit Sub
            ' El flag se limpia DESPUES de subir, no antes: si el VBO todavia no existe hay que
            ' volver a intentarlo en el proximo update. Limpiarlo primero perdia el aviso para
            ' siempre y las UVs morpheadas no subian nunca.
            If vboUVMaskWeight = 0 OrElse geom.Uvs_Weight Is Nothing OrElse geom.Uvs_Weight.Length = 0 Then Exit Sub
            Me.ParentModel.ParentControl.EnsureContextCurrent()
            GL.BindBuffer(BufferTarget.ArrayBuffer, vboUVMaskWeight)
            GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero,
                             geom.Uvs_Weight.Length * 3 * 4, geom.Uvs_Weight)
            geom.UvsDirty = False
            MeshData.Meshgeometry = geom
        End Sub

        ''' <summary>Buffers de staging del upload, UNO POR MALLA y reusados entre frames. Ver el uso.
        ''' <para>Se dimensionan al ALTA y no se achican: una malla no cambia de cantidad de vertices sin
        ''' pasar por una re-extraccion, que reconstruye el RenderableMesh entero.</para></summary>
        Private _upPos() As Vector3, _upNrm() As Vector3, _upTan() As Vector3, _upBitan() As Vector3

        ''' <summary>Separa COMPUTO de SUBIDA dentro del camino de upload completo, para decidir donde
        ''' vale la pena optimizar: si el que domina es el driver, vectorizar la aritmetica no mueve nada.
        ''' <para>NO se usa `Logger.Enabled` para esto. Ese flag no gobierna solo la escritura del log: es
        ''' la compuerta de TODOS los calculos de diagnostico del codigo y ademas mete dos Stopwatch por
        ''' malla por frame, o sea que mediria un frame que no es el que corre en produccion.</para>
        ''' <para>Apagado por default: cuando esta en False el costo es un test de booleano por llamada.</para>
        ''' </summary>
        Friend Shared Property MedirFasesDeUpload As Boolean = False
        Friend Shared MsComputo As Double
        Friend Shared MsSubida As Double

        Private Sub EnsureUploadScratch(n As Integer)
            If _upPos IsNot Nothing AndAlso _upPos.Length >= n Then Exit Sub
            ReDim _upPos(n - 1) : ReDim _upNrm(n - 1) : ReDim _upTan(n - 1) : ReDim _upBitan(n - 1)
        End Sub

        ''' <summary>Sube al GL los buffers del shape ya skineados en CPU (lee <c>PerVertexSkinMatrix</c> y
        ''' transforma local a world antes del upload). Es el camino que corre con GPU-skinning APAGADO.
        ''' <para>⛔ SYNC: CPU/GPU skinning - es el gemelo del bloque de skinning del vertex shader. Con el
        ''' toggle en GPU este codigo no corre, asi que una formula cambiada de un solo lado no falla: solo se ve
        ''' mal en el otro modo. Lista completa de sitios gemelos en <c>SkinningHelper.BlendBoneMatrices</c> y en
        ''' 00-reglas-ui-y-vb.</para></summary>
        ''' <param name="recomputeBounds">True (default) = recomputa bounds tras el upload completo (full-reload
        ''' y morph, que no tienen ComputeBounds aparte). El camino de pose pasa False porque sus bounds los
        ''' maneja la linea gateada del pass 1; hacerlo incondicional aca saltea ese gate (8,9 ms/frame medidos).
        ''' El nombre difiere de ComputeBounds a proposito: VB es case-insensitive y un parametro homonimo
        ''' sombrearia al metodo.</param>
        ''' <summary>S-W: the raw UNORM8 normals of a Skyrim SE TREE_ANIM shape (IShapeGeometry.GetNormals: b / 255 * 2 - 1, the VS's own
        ''' decode, NOT the normalized Meshgeometry.Normals) when the frame's game is SSE and the technique is TREE_ANIM
        ''' (SseRenderPassLaw.TreeAnimTechnique); Nothing otherwise. Read once per geometry: the NIF's normals do not change with the
        ''' pose or the morphs.</summary>
        Private _leafRawNormals As List(Of System.Numerics.Vector3)
        Private _leafRawNormalsFor As Object
        ''' <summary>S-W: what the positions VBO carries - the TREE_ANIM rest pose or not -, written by the full uploads (SetupMesh_GL, the
        ''' dense branch of UpdateSkinBuffers_GL); RefreshLeafPose_GL compares it with what the frame asks for.</summary>
        Private _vboLeafPose As Boolean
        ''' <summary>S-W: scratch of the CPU-skinning upload's displaced local positions (sized up, never down: the policy of _upPos,
        ''' EnsureUploadScratch).</summary>
        Private _upLeafPos() As Vector3d

        Private Function LeafRawNormals() As List(Of System.Numerics.Vector3)
            If Not ParentModel.FrameIsSse Then Return Nothing
            Dim mat = MeshData.Material
            If mat Is Nothing OrElse mat.MaterialBase Is Nothing OrElse Not SseRenderPassLaw.TreeAnimTechnique(mat.SseInputs()) Then Return Nothing
            Dim g = MeshData.Meshgeometry.Geometry
            If g Is Nothing OrElse Not g.HasNormals Then Return Nothing
            If _leafRawNormalsFor IsNot g Then _leafRawNormals = g.GetNormals() : _leafRawNormalsFor = g
            If _leafRawNormals.Count <> MeshData.Meshgeometry.Vertices.Length Then Return Nothing
            Return _leafRawNormals
        End Function

        ''' <summary>S-W: the position the VBO carries for vertex <paramref name="i"/>: the NIF's local vertex, or with
        ''' <paramref name="leafN"/> the TREE_ANIM rest pose (SseRenderPassLaw.LeafRestPosition = o2 of VS rec 3993) BEFORE skinning, as
        ''' VS rec 3994 does. The data (Meshgeometry.Vertices) is never displaced: what the user edits and saves is the NIF.</summary>
        Private Function DrawPosition(i As Integer, leafN As List(Of System.Numerics.Vector3)) As Vector3d
            Dim v = MeshData.Meshgeometry.Vertices(i)
            If leafN Is Nothing Then Return v
            Dim n = leafN(i)
            Dim p = SseRenderPassLaw.LeafRestPosition(New Vector3(CSng(v.X), CSng(v.Y), CSng(v.Z)), New Vector3(n.X, n.Y, n.Z),
                                                      MeshData.Meshgeometry.VertexColors(i).W)
            Return New Vector3d(p.X, p.Y, p.Z)
        End Function

        ''' <summary>S-W: DrawPosition of the first <paramref name="n"/> vertices into the scratch (the CPU-skinning dense upload).</summary>
        Private Function LeafDrawPositions(leafN As List(Of System.Numerics.Vector3), n As Integer) As Vector3d()
            If _upLeafPos Is Nothing OrElse _upLeafPos.Length < n Then ReDim _upLeafPos(n - 1)
            For i = 0 To n - 1 : _upLeafPos(i) = DrawPosition(i, leafN) : Next
            Return _upLeafPos
        End Function

        ''' <summary>S-W: the positions VBO carries the TREE_ANIM rest pose exactly when this frame asks for it (LeafRawNormals). A game
        ''' switch (RenderBucketsGame) or a material edit that changes the technique rebuilds the buckets (PreviewModel.RebuildRenderBuckets
        ''' calls this): a mismatch re-uploads every position, through the dense branch, which records the new state.</summary>
        Friend Sub RefreshLeafPose_GL()
            If MeshData Is Nothing Then Exit Sub
            Dim v = MeshData.Meshgeometry.Vertices
            If v Is Nothing OrElse v.Length = 0 OrElse vboPosition = 0 Then Exit Sub
            If (LeafRawNormals() IsNot Nothing) = _vboLeafPose Then Exit Sub
            MeshData.Meshgeometry.dirtyVertexIndices.MarcarTodos(v.Length)
            Array.Fill(MeshData.Meshgeometry.dirtyVertexFlags, True)
            UpdateSkinBuffers_GL(recomputeBounds:=False)
        End Sub

        Public Sub UpdateSkinBuffers_GL(Optional recomputeBounds As Boolean = True)
            UpdateUvBuffer_GL()
            ' Actualiza VBOs de Normales, Tangentes, Bitangentes y Posiciones
            ' Detect skinning mode change: if the toggle changed since last upload, force ALL dirty
            ' [RENDER-MS] instrumentacion — gateada por Logger.Enabled. Esta funcion corre POR MALLA POR
            ' FRAME, asi que los dos `Stopwatch.StartNew()` que tenia (este y `_swSkinPhase`) eran DOS
            ' allocations por malla por frame, incondicionales, y sus acumuladores (`_skin*Ms`) no los lee
            ' nadie salvo el `[RENDER-MS]` de RenderShapes. Se toma el flag UNA vez por llamada.
            Dim _instr As Boolean = Logger.Enabled
            Dim _swCtx As System.Diagnostics.Stopwatch = If(_instr, System.Diagnostics.Stopwatch.StartNew(), Nothing)
            Me.ParentModel.ParentControl.EnsureContextCurrent()
            If _instr Then ParentModel.ParentControl._skinCtxMs += _swCtx.Elapsed.TotalMilliseconds
            Dim gpuMode As Boolean = Config_App.Current.Setting_GPUSkinning
            If gpuMode <> _lastUploadWasGPU Then
                _lastUploadWasGPU = gpuMode
                If MeshData.Meshgeometry.Vertices IsNot Nothing AndAlso MeshData.Meshgeometry.Vertices.Length > 0 Then
                    MeshData.Meshgeometry.dirtyVertexIndices.MarcarTodos(MeshData.Meshgeometry.Vertices.Length)
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
                    ' [RENDER-MS] compute vs upload — gateado (ver la nota del tope de la funcion).
                    Dim _swSkinPhase As System.Diagnostics.Stopwatch = If(_instr, System.Diagnostics.Stopwatch.StartNew(), Nothing)
                    ' SCRATCH REUTILIZADO, NO CUATRO ARRAYS NUEVOS POR FRAME: este bloque corre por malla y por
                    ' frame durante toda una animacion con skinning CPU (y en cada morph); alocar
                    ' `vertexCount * 12 bytes * 4` son 6,3 MB de Gen0 por frame con 130.500 vertices (~375 MB/s
                    ' a 60 fps). Misma politica que _shadowCasters y BlendScratch.
                    EnsureUploadScratch(vertexCount)
                    Dim posF = _upPos, nrmF = _upNrm, tanF = _upTan, bitanF = _upBitan
                    Dim _swFase As System.Diagnostics.Stopwatch = If(MedirFasesDeUpload, System.Diagnostics.Stopwatch.StartNew(), Nothing)
                    ' S-W: the TREE_ANIM rest pose (DrawPosition) of every position this branch uploads, before the skinning (VS rec 3994).
                    Dim leafN = LeafRawNormals()
                    _vboLeafPose = leafN IsNot Nothing

                    If cpuSkin Then
                        ' CPU skinning: transform local ? world using PerVertexSkinMatrix
                        Dim mats = MeshData.Meshgeometry.PerVertexSkinMatrix
                        Dim lv = If(leafN Is Nothing, MeshData.Meshgeometry.Vertices, LeafDrawPositions(leafN, vertexCount))
                        Dim ln = MeshData.Meshgeometry.Normals
                        Dim lt = MeshData.Meshgeometry.Tangents
                        Dim lb = MeshData.Meshgeometry.Bitangents
                        Dim isMSN As Boolean = MeshData.Material?.MaterialBase IsNot Nothing AndAlso MeshData.Material.MaterialBase.ModelSpaceNormals
                        ' Normal matrix SIEMPRE POR VERTICE, para coincidir con el `skinNormalMat` per-vertex
                        ' del shader GPU. Cachear una sola cuando mats(0) == mats(vertexCount-1) es un falso
                        ' positivo facil de disparar (primer y ultimo vertice comparten bone y los del medio no)
                        ' y deja las normales del medio con el nm3 del vertice 0.
                        ' El bucle vive en FastSkin, donde la ley esta escrita UNA vez con dos implementaciones
                        ' (escalar y vectorial) que un gate compara BIT A BIT a los dos anchos de vector. Es el
                        ' hotpath del frame con skinning CPU: una inversa 3x3 por vertice/malla/frame en Double,
                        ' 9,3 ms de un frame de ~20 sobre 130.500 vertices, contra 1,3 ms de las 4 subidas de VBO.
                        SkinningHelper.FastSkinTransformar(mats, lv, ln, lt, lb, isMSN, vertexCount,
                                                           posF, nrmF, tanF, bitanF)
                    Else
                        ' GPU skinning: upload local-space as-is
                        Dim gn = MeshData.Meshgeometry.Normals
                        Dim gt = MeshData.Meshgeometry.Tangents
                        Dim gb = MeshData.Meshgeometry.Bitangents
                        ' Partitioner + For interno, NO `Parallel.For(0, n, Sub(i))`: esa forma invoca UN
                        ' DELEGATE POR VERTICE (22.700 por malla por frame) para un cuerpo de 12 conversiones y
                        ' 4 stores — el despacho cuesta del orden del trabajo. Mismo hallazgo que el compositor
                        ' de facetint (ver la memoria 61-perf-plan-4-hotpaths §3).
                        ' Y NO se vectoriza con Vector.Narrow aunque sea el caso ideal (un run plano de 3N
                        ' doubles a 3N floats): haria falta ver el Vector3d() como Double(), o sea
                        ' MemoryMarshal/Span, que VB.NET no admite en ninguna posicion; copiar a un staging
                        ' plano cuesta mas trafico de memoria del que ahorra el narrow.
                        Dim convertRange As Action(Of Tuple(Of Integer, Integer)) =
                            Sub(rango As Tuple(Of Integer, Integer))
                                For i = rango.Item1 To rango.Item2 - 1
                                    Dim vv = DrawPosition(i, leafN) : posF(i) = New Vector3(CSng(vv.X), CSng(vv.Y), CSng(vv.Z))
                                    ' N/T/B ya son Single: copia de struct, sin conversion.
                                    nrmF(i) = gn(i)
                                    tanF(i) = gt(i)
                                    bitanF(i) = gb(i)
                                Next
                            End Sub
                        If vertexCount >= 2000 Then
                            Parallel.ForEach(SkinningHelper.RangosDe(vertexCount), convertRange)
                        Else
                            convertRange(Tuple.Create(0, vertexCount))
                        End If
                    End If
                    If _instr Then
                        ParentModel.ParentControl._skinComputeMs += _swSkinPhase.Elapsed.TotalMilliseconds
                        _swSkinPhase.Restart()
                    End If

                    If MedirFasesDeUpload Then
                        MsComputo += _swFase.Elapsed.TotalMilliseconds
                        _swFase.Restart()
                    End If

                    GL.BindBuffer(BufferTarget.ArrayBuffer, vboPosition)
                    GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, totalBytes, posF)

                    GL.BindBuffer(BufferTarget.ArrayBuffer, vboNormal)
                    GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, totalBytes, nrmF)

                    ' SAO (SSE): location 11 is always the pre-skin normal array (the bit-26 skinned effect VS reads the bind pose).
                    Debug.Assert(MeshData.Meshgeometry.Normals.Length = vertexCount)
                    GL.BindBuffer(BufferTarget.ArrayBuffer, vboPreSkinNormal)
                    GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, totalBytes, MeshData.Meshgeometry.Normals)

                    ' S-O1: the eye's reflection centres follow the positions (skinned with them on the CPU path).
                    UploadEyeCenters_GL(cpuSkin)

                    GL.BindBuffer(BufferTarget.ArrayBuffer, vboTangent)
                    GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, totalBytes, tanF)

                    GL.BindBuffer(BufferTarget.ArrayBuffer, vboBitangent)
                    GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, totalBytes, bitanF)
                    If MedirFasesDeUpload Then MsSubida += _swFase.Elapsed.TotalMilliseconds

                    GL.BindBuffer(BufferTarget.ArrayBuffer, 0)
                    If _instr Then
                        ParentModel.ParentControl._skinUploadMs += _swSkinPhase.Elapsed.TotalMilliseconds
                        _swSkinPhase.Restart()
                    End If

                    ' Clear all dirty flags since everything was updated.
                    ' `Array.Clear` Y NO UN BUCLE POR INDICE: para entrar a esta rama hacen falta MAS DEL 60 %
                    ' de los vertices sucios (~78.000 indices sobre el Serena Battle Suit), o sea 78.000 lecturas
                    ' de la lista mas 78.000 escrituras DISPERSAS al array de flags; `Array.Clear` es un memset
                    ' contiguo. Esta rama sube TODOS los vertices, asi que al salir ninguno queda sucio.
                    If MeshData.Meshgeometry.dirtyVertexFlags IsNot Nothing Then
                        Array.Clear(MeshData.Meshgeometry.dirtyVertexFlags, 0, MeshData.Meshgeometry.dirtyVertexFlags.Length)
                    End If
                    MeshData.Meshgeometry.dirtyVertexIndices.Clear()
                    If _instr Then
                        ParentModel.ParentControl._skinDirtyMs += _swSkinPhase.Elapsed.TotalMilliseconds
                        _swSkinPhase.Restart()
                    End If

                    ' Also recompute bounds after full update — SALVO cuando el caller ya los maneja: en el
                    ' pose path los computa la línea gateada del pass 1 ('If computeBoundsThisFrame Then
                    ' mesh.ComputeBounds()'), y hacerlo incondicional acá saltea ese gate. ComputeBounds es una
                    ' pasada per-vértice a mundo: 6,9-8,5 ms/frame sobre las 11 mallas del arnés.
                    If recomputeBounds Then Me.ComputeBounds()
                    If _instr Then
                        ParentModel.ParentControl._skinBoundsMs += _swSkinPhase.Elapsed.TotalMilliseconds
                        _swSkinPhase.Restart()
                    End If

                    UpdateUpdateSkinBuffersMask_GL()
                    If _instr Then ParentModel.ParentControl._skinMaskMs += _swSkinPhase.Elapsed.TotalMilliseconds
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
                GL.BindBuffer(BufferTarget.ArrayBuffer, vboPreSkinNormal)
                Dim ptrPre As IntPtr = GL.MapBufferRange(BufferTarget.ArrayBuffer, IntPtr.Zero, totalBytes, mapMask)

                ' Un solo bucle para actualizar todos los atributos
                Dim buf(2) As Single
                Dim sparseMats = If(cpuSkin, MeshData.Meshgeometry.PerVertexSkinMatrix, Nothing)
                ' S-W: the rest pose of a TREE_ANIM shape (DrawPosition), before the skinning as in VS rec 3994. A partial upload does not
                ' record _vboLeafPose: RefreshLeafPose_GL re-uploads everything when the state changed.
                Dim sparseLeaf = LeafRawNormals()
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
                        ' LA MISMA LEY QUE EL CAMINO DENSO, no una copia. Este bucle escribe por
                        ' Marshal.Copy a un buffer mapeado, indice por indice, asi que no puede llamar a
                        ' FastSkin.TransformarDirecto — pero si a la ley de UN vertice. Tenerla duplicada
                        ' hacia que la misma malla saliera con una ley u otra segun cuantos vecinos se
                        ' hubieran ensuciado ese frame (el umbral del 60 % de mas arriba).
                        Dim pS As Vector3, nS As Vector3, tS As Vector3, bS As Vector3
                        SkinningHelper.FastSkinUnVertice(sparseMats(i), DrawPosition(i, sparseLeaf),
                                                         MeshData.Meshgeometry.Normals(i),
                                                         MeshData.Meshgeometry.Tangents(i),
                                                         MeshData.Meshgeometry.Bitangents(i),
                                                         sparseIsMSN, pS, nS, tS, bS)
                        buf(0) = pS.X : buf(1) = pS.Y : buf(2) = pS.Z
                        Marshal.Copy(buf, 0, baseP, 3)
                        buf(0) = nS.X : buf(1) = nS.Y : buf(2) = nS.Z
                        Marshal.Copy(buf, 0, baseN, 3)
                        buf(0) = tS.X : buf(1) = tS.Y : buf(2) = tS.Z
                        Marshal.Copy(buf, 0, baseT, 3)
                        buf(0) = bS.X : buf(1) = bS.Y : buf(2) = bS.Z
                        Marshal.Copy(buf, 0, baseB, 3)
                        Dim nPre = MeshData.Meshgeometry.Normals(i) : buf(0) = nPre.X : buf(1) = nPre.Y : buf(2) = nPre.Z : Marshal.Copy(buf, 0, ptrPre + offsetBytes, 3)
                    Else
                        Dim v = DrawPosition(i, sparseLeaf)
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
                        Dim nPre = MeshData.Meshgeometry.Normals(i) : buf(0) = nPre.X : buf(1) = nPre.Y : buf(2) = nPre.Z : Marshal.Copy(buf, 0, ptrPre + offsetBytes, 3)
                    End If

                    MeshData.Meshgeometry.dirtyVertexFlags(i) = False
                Next

                ' Flush y desmapear en orden inverso
                GL.BindBuffer(BufferTarget.ArrayBuffer, vboPreSkinNormal)
                GL.FlushMappedBufferRange(BufferTarget.ArrayBuffer, IntPtr.Zero, New IntPtr(totalBytes))
                GL.UnmapBuffer(BufferTarget.ArrayBuffer)
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
                ' S-O1: the eye's reflection centres follow the positions (all of them: an eye is a few hundred vertices).
                UploadEyeCenters_GL(cpuSkin)
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
            ' EL BUFFER SE IZA FUERA DEL BUCLE. Adentro estaba `BitConverter.GetBytes(vertexMask(i))`,
            ' que aloca un Byte(3) NUEVO por indice sucio, por malla, por tick de morph: con la malla
            ' entera sucia son 130.500 arrays Gen0 en un solo frame, para copiar 4 bytes. El bucle hermano
            ' de veinte lineas mas arriba ya lo resuelve asi (`Dim buf(2) As Single` izado + Marshal.Copy);
            ' este quedo sin migrar. Mismos bytes escritos: `Marshal.Copy(Single(), ...)` mueve el patron
            ' IEEE-754 tal cual, igual que GetBytes.
            Dim mBuf(0) As Single
            For Each i As Integer In dirtyMaskIndices
                If i < 0 OrElse i >= vertexMask.Length OrElse i >= dirtyMaskFlags.Length Then Continue For

                Dim offsetBytes As Int64 = CLng(i) * maskSize
                Dim baseM As IntPtr = ptrM + offsetBytes
                mBuf(0) = vertexMask(i)
                Marshal.Copy(mBuf, 0, baseM, 1)
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
                ' Gate: Logger.LogLazy YA chequea Logger.Enabled adentro, así que el gate NO es por la
                ' escritura — es por el deref en cadena de abajo (Meshgeometry→Geometry→BackingShape→Name),
                ' que se hace FUERA del lambda y por lo tanto corre con el log apagado, en camino GL caliente.
                If Logger.Enabled Then
                    Try
                        Dim shapeName As String = "<unknown>"
                        If MeshData IsNot Nothing AndAlso MeshData.Meshgeometry.Geometry IsNot Nothing AndAlso MeshData.Meshgeometry.Geometry.BackingShape IsNot Nothing Then
                            Dim nm = MeshData.Meshgeometry.Geometry.BackingShape.Name
                            If nm IsNot Nothing AndAlso nm.String IsNot Nothing Then shapeName = nm.String
                        End If
                        Logger.LogLazy(Function() $"[GL-SSBO-DIAG] UpdateBoneMatricesSSBO size mismatch: shape='{shapeName}' newSize={sizeBytes} capacity={ssbo_BoneMatricesCapacityBytes} newCount={MeshData.Meshgeometry.GPUBoneMatrices.Length} capCount={ssbo_BoneMatricesCapacityBytes \ 64}")
                    Catch
                    End Try
                End If
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

        ''' <summary>S-83A (chunk C8): Meshgeometry.Indices in the order the game draws the triangles (DrawTriangleOrder), the data order
        ''' when it has none or when Indices is no longer the array the order was computed for (DrawTriangleOrderFor: RemoveZaps
        ''' compacts Indices; rev-03). Every upload of the element buffer goes through here; the masks (zap, occlusion) stay indexed by
        ''' the DATA triangle (EnsureZapIndexBuffer walks this order and reads them by the data index).</summary>
        Private Function DrawOrderTriangles() As Integer()
            Dim order = MeshData.Meshgeometry.DrawTriangleOrder
            If order IsNot Nothing AndAlso ReferenceEquals(MeshData.Meshgeometry.DrawTriangleOrderFor, MeshData.Meshgeometry.Indices) Then Return order
            Dim n = If(MeshData.Meshgeometry.Indices Is Nothing, 0, MeshData.Meshgeometry.Indices.Length \ 3)
            Return Enumerable.Range(0, n).ToArray()
        End Function

        Private Function DrawOrderIndices() As UInteger()
            Dim full = MeshData.Meshgeometry.Indices
            Dim order = MeshData.Meshgeometry.DrawTriangleOrder
            If full Is Nothing OrElse Not (order IsNot Nothing AndAlso ReferenceEquals(MeshData.Meshgeometry.DrawTriangleOrderFor, MeshData.Meshgeometry.Indices)) Then Return full
            Dim r(order.Length * 3 - 1) As UInteger
            For i = 0 To order.Length - 1
                r(i * 3) = full(order(i) * 3) : r(i * 3 + 1) = full(order(i) * 3 + 1) : r(i * 3 + 2) = full(order(i) * 3 + 2)
            Next
            Return r
        End Function

        ' Zap limpio del lado CPU: con ApplyZaps prendido se excluye todo triangulo que tenga ALGUN vertice con
        ' VertexMask = -1 (la misma regla que usa el export a NIF). Reemplaza al discard por 'flat ZappedVert'
        ' del shader, que descartaba por vertice provocador y dejaba astillas en el borde.
        ' Los vertices NO se compactan (el VBO queda completo); solo se filtra el index buffer, asi que los
        ' zapeados dejan de estar referenciados. Se reconstruye solo cuando ApplyMorphPlan re-toco la mascara o
        ' cambio el toggle: ApplyMorphPlan es el unico escritor de VertexMask=-1, asi que el flag no puede
        ' quedar rancio.
        ' ⚠️ SkinnedGeometry es Structure: 'geom' es una COPIA del campo, asi que el clear de ZapTopologyDirty
        ' hay que escribirlo al campo, no a la copia local. Leer por 'geom' esta bien (los arrays son
        ' referencias).
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
            ' Slot occluder de la RACE del actor y camino de oclusión (worn item vs head part): los dos entran
            ' al gate sucio de abajo porque los dos CAMBIAN el resultado. Ver IRenderableShape.
            Dim occluderMask As UInteger = If(MeshData.Shape IsNot Nothing, MeshData.Shape.OccluderSlotMask, 0UI)
            Dim asWorn As Boolean = MeshData.Shape IsNot Nothing AndAlso MeshData.Shape.OcclusionAsWornItem
            Dim occlDispositivo As Boolean = MeshData.Shape IsNot Nothing AndAlso MeshData.Shape.OccluderConDispositivo
            If Not geom.ZapTopologyDirty AndAlso applyZaps = _lastApplyZaps AndAlso coveredMask = _lastCoveredSlotsMask AndAlso
               drawHidden = _lastDrawHidden AndAlso occluderMask = _lastOccluderSlotMask AndAlso asWorn = _lastOcclusionAsWornItem AndAlso
               occlDispositivo = _lastOccluderConDispositivo Then Return

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
                Dim subIdx = TryCast(MeshData.Shape?.NifShape, NiflySharp.Blocks.BSSubIndexTriShape)
                If subIdx IsNot Nothing Then
                    ' FO4: per-segment occlusion via BSSubIndexTriShape/BSGeometrySegmentData.
                    occl = BSTriShapeGeometry.ComputeHiddenTriangles(subIdx, coveredMask, occluderMask, occlDispositivo)
                ElseIf (coveredMask <> 0UI OrElse asWorn) AndAlso Config_App.Current.Game = Config_App.Game_Enum.Skyrim AndAlso
                       MeshData.Shape IsNot Nothing AndAlso MeshData.Shape.NifContent IsNot Nothing AndAlso MeshData.Shape.NifShape IsNot Nothing Then
                    ' SSE: per-partition occlusion via BSDismemberSkinInstance partitions. Keyed on the mesh's
                    ' REAL partition SBP slot, NOT the ARMA BOD2 (which declares incidental extra slots — e.g.
                    ' NakedTorso BOD2 includes calves(38), so boots would whole-hide the body under a BOD2
                    ' check; the body mesh's partition is SBP 32, so per-partition hides it only when slot 32
                    ' is covered). Para vanilla single-partition skin meshes cada triángulo comparte un SBP →
                    ' resultado whole-mesh, insensible al orden de triángulos.
                    ' `asWorn` elige el camino del motor y con él el DEFAULT de una partición fuera de banda:
                    ' head parts (0x1403CC770) la fuerza visible, worn items (0x14021DAE0 fase 1) la oculta.
                    ' Por eso una shape de worn item entra acá aunque su máscara sea 0: con máscara 0 el motor
                    ' igual oculta lo que cae fuera de [30,61], así que 0 NO es no-op en ese camino.
                    ' NpcRenderHost setea CoveredSlotsMask en TODA shape no-headpart de SSE (piel Y outfits),
                    ' que es lo que hace la fase 1 del motor.
                    occl = MeshData.Shape.NifContent.ComputeHiddenTrianglesDismember(MeshData.Shape.NifShape, coveredMask, asWorn)
                End If
            End If
            _occlHidden = occl
            ' Marcado acá y no en los returns de abajo: el early-return del dirty gate sólo puede
            ' darse si ya hubo una pasada previa por este punto (los centinelas de _lastCoveredSlotsMask
            ' y _lastDrawHidden fuerzan que la PRIMERA siempre llegue), así que el flag no puede
            ' quedar en False con un _occlHidden ya válido.
            _occlEvaluated = True

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
                    GL.BufferData(BufferTarget.ElementArrayBuffer, full.Length * 4, DrawOrderIndices(), BufferUsageHint.StaticDraw)
                    indexCount = full.Length
                    _zapFilteredActive = False
                End If
                _lastApplyZaps = applyZaps
                _lastCoveredSlotsMask = coveredMask
                _lastDrawHidden = drawHidden
                _lastOccluderSlotMask = occluderMask
                _lastOcclusionAsWornItem = asWorn
                _lastOccluderConDispositivo = occlDispositivo
                MeshData.Meshgeometry.ZapTopologyDirty = False
                Return
            End If

            Dim vmask = geom.VertexMask
            Dim filtered As New List(Of UInteger)(full.Length)
            ' In the game's draw order (S-83A, DrawOrderTriangles); the masks are read by the DATA triangle index.
            For Each ti In DrawOrderTriangles()
                Dim t = ti * 3
                If t + 2 >= full.Length Then Continue For
                Dim a = full(t) : Dim b = full(t + 1) : Dim c = full(t + 2)
                Dim triHidden As Boolean = (occl IsNot Nothing AndAlso ti < occl.Length AndAlso occl(ti))
                ' vmask is non-Nothing whenever the vertex-zap path is active (anyZap requires vm IsNot Nothing);
                ' the per-segment-only path may run with no zaps, so the vertex test is null-safe here.
                Dim vertZapped As Boolean = (vmask IsNot Nothing AndAlso (vmask(CInt(a)) = -1 OrElse vmask(CInt(b)) = -1 OrElse vmask(CInt(c)) = -1))
                If Not triHidden AndAlso Not vertZapped Then
                    filtered.Add(a) : filtered.Add(b) : filtered.Add(c)
                End If
            Next
            Dim arr = filtered.ToArray()
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, ebo)
            GL.BufferData(BufferTarget.ElementArrayBuffer, arr.Length * 4, arr, BufferUsageHint.DynamicDraw)
            indexCount = arr.Length
            _zapFilteredActive = True
            _lastApplyZaps = applyZaps
            _lastCoveredSlotsMask = coveredMask
            _lastDrawHidden = drawHidden
            _lastOccluderSlotMask = occluderMask
            _lastOcclusionAsWornItem = asWorn
            _lastOccluderConDispositivo = occlDispositivo
            MeshData.Meshgeometry.ZapTopologyDirty = False
        End Sub

        ''' <summary>S-O1: the SSE eye VS's reflection centre of a vertex, in the VS's own order and precision:
        ''' (R - L) * eye + L in float (`add [precise] r12, -cb1[0], cb1[1] ; mul r12, v6.xxxx ; add r12, cb1[0]`, VS recs
        ''' 4019-4022). L / R = LeftEyeReflectionCenter / RightEyeReflectionCenter of the NIF block (LoadBinary vf13 0x1415257F0
        ''' reads them; SetupMaterial 0x141548931..99E sends material +0xB4 / +0xC0 as cb1[0] / cb1[1]; no runtime writer).</summary>
        Friend Shared Function SseEyeReflectionCenter(l As System.Numerics.Vector3, r As System.Numerics.Vector3, eye As Single) As Vector3
            Return New Vector3((r.X - l.X) * eye + l.X, (r.Y - l.Y) * eye + l.Y, (r.Z - l.Z) * eye + l.Z)
        End Function

        ''' <summary>S-O1: location 12 for this shape, or Nothing when it is not a Skyrim SE eye (technique type 16 of the full
        ''' selector, MaterialData.SseLitTechniqueType: user decision 7-oct-2026, rev-04) or has no eye centres: the NIF block is not a
        ''' BSLightingShaderProperty of type 16 (the only one that carries the two centres: nif.xml, NiflySharp Sync `_shaderType
        ''' == 16`), or the geometry has no per-vertex eye data (the game's input layout then has no TEXCOORD2, 0x141011AB8..B0E:
        ''' Fragment_SSE keeps the vertex normal, bEyeRadial false, and the frame notice lists it). <paramref name="cpuSkin"/>:
        ''' each centre through the vertex's own skin matrix with the position law (SkinningHelper.FastSkinPunto), as the
        ''' positions are uploaded; else bind space, as the GPU-skinning positions.</summary>
        Private Function EyeCentresForUpload(cpuSkin As Boolean) As Vector3()
            If Config_App.Current.Game <> Config_App.Game_Enum.Skyrim OrElse MeshData.Material Is Nothing OrElse MeshData.Material.SseLitTechniqueType() <> &H10 Then Return Nothing
            Dim lsh = TryCast(MeshData.Shape?.NifShader, NiflySharp.Blocks.BSLightingShaderProperty)
            Dim geo = MeshData.Meshgeometry
            If lsh Is Nothing OrElse Not lsh.IsTypeEyeEnvironmentMap OrElse geo.Geometry Is Nothing OrElse Not geo.Geometry.HasEyeData Then Return Nothing
            Dim n = geo.Vertices.Length
            If geo.Eyedata Is Nothing OrElse geo.Eyedata.Length <> n Then Return Nothing
            Dim l = lsh.LeftEyeReflectionCenter, r = lsh.RightEyeReflectionCenter
            Dim mats = If(cpuSkin, geo.PerVertexSkinMatrix, Nothing)
            Dim out(n - 1) As Vector3
            For i = 0 To n - 1
                Dim c = SseEyeReflectionCenter(l, r, geo.Eyedata(i))
                out(i) = If(mats Is Nothing, c, SkinningHelper.FastSkinPunto(mats(i), New Vector3d(c.X, c.Y, c.Z)))
            Next
            Return out
        End Function

        ''' <summary>S-O1: rewrites location 12 after the positions (both upload paths of UpdateSkinBuffers_GL). The bind-space
        ''' centres are not rewritten while they are the ones in the buffer (_eyeCentersAreBind).</summary>
        Private Sub UploadEyeCenters_GL(cpuSkin As Boolean)
            If vboEyeCenter = 0 OrElse (Not cpuSkin AndAlso _eyeCentersAreBind) Then Exit Sub
            Dim c = EyeCentresForUpload(cpuSkin)
            If c Is Nothing Then Exit Sub
            GL.BindBuffer(BufferTarget.ArrayBuffer, vboEyeCenter)
            GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, c.Length * 3 * 4, c)
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0)
            _eyeCentersAreBind = Not cpuSkin
        End Sub

        Public Sub SetupMesh_GL()
            vao = GL.GenVertexArray()
            ebo = GL.GenBuffer()
            vboPosition = GL.GenBuffer()
            vboNormal = GL.GenBuffer()
            vboPreSkinNormal = GL.GenBuffer()
            vboTangent = GL.GenBuffer()
            vboBitangent = GL.GenBuffer()
            vboColorAlpha = GL.GenBuffer()
            vboUVMaskWeight = GL.GenBuffer()
            vboMask = GL.GenBuffer()

            Dim count = MeshData.Meshgeometry.Vertices.Length

            GL.BindVertexArray(vao)

            ' S-W: the TREE_ANIM rest pose (DrawPosition) from the first upload: with GPU skinning this is the only upload of the
            ' positions until a morph or an edit dirties them (RenderShapes runs UpdateSkinBuffers_GL only without GPU skinning).
            Dim leafNs = LeafRawNormals()
            Dim posF(count - 1) As Vector3
            For iP = 0 To count - 1
                Dim vP = DrawPosition(iP, leafNs) : posF(iP) = New Vector3(CSng(vP.X), CSng(vP.Y), CSng(vP.Z))
            Next
            _vboLeafPose = leafNs IsNot Nothing
            ' N/T/B ya ESTAN en Single (ver SkinnedGeometry.Normals): van derecho al VBO. Antes cada
            ' creacion de buffers alocaba tres arrays float de la malla entera solo para convertir
            ' desde Double — 36 B por vertice de basura y un barrido de N por shape. El valor que sube
            ' a la GPU es exactamente el mismo: el ConvertAll hacia esa misma narrowing.
            Dim nrmF() As Vector3 = MeshData.Meshgeometry.Normals
            Dim tanF() As Vector3 = MeshData.Meshgeometry.Tangents
            Dim bitanF() As Vector3 = MeshData.Meshgeometry.Bitangents

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

            ' PRE-SKIN NORMALS (location 11) — a copy of the same array; Vertex_FO4 does not declare it.
            GL.BindBuffer(BufferTarget.ArrayBuffer, vboPreSkinNormal)
            GL.BufferData(BufferTarget.ArrayBuffer, nrmF.Length * 3 * 4, nrmF, BufferUsageHint.DynamicDraw)
            GL.EnableVertexAttribArray(11)
            GL.VertexAttribPointer(11, 3, VertexAttribPointerType.Float, False, 0, 0)

            ' EYE REFLECTION CENTRES (location 12, Vertex_SSE, S-O1): bind space, like the raw positions above; only a shape that
            ' has them (EyeCentresForUpload).
            Dim eyeC = EyeCentresForUpload(False)
            If eyeC IsNot Nothing Then
                vboEyeCenter = GL.GenBuffer()
                GL.BindBuffer(BufferTarget.ArrayBuffer, vboEyeCenter)
                GL.BufferData(BufferTarget.ArrayBuffer, eyeC.Length * 3 * 4, eyeC, BufferUsageHint.DynamicDraw)
                GL.EnableVertexAttribArray(12)
                GL.VertexAttribPointer(12, 3, VertexAttribPointerType.Float, False, 0, 0)
                _eyeCentersAreBind = True
            End If

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
            GL.BufferData(BufferTarget.ElementArrayBuffer, MeshData.Meshgeometry.Indices.Length * 4, DrawOrderIndices(), BufferUsageHint.StaticDraw)
            GL.BindVertexArray(0)
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0)
            indexCount = MeshData.Meshgeometry.Indices.Length

            ' O3.3: Compute initial AABB for frustum culling
            ComputeBounds()
        End Sub

        ''' <summary>
        ''' Compute axis-aligned bounding box from world-space vertex positions for frustum culling.
        ''' (GPU skinning: Vertices are local-space, so we need world-space for correct bounds.)
        ''' <para>NO MATERIALIZA EL CACHE DE MUNDO: <c>GetWorldVertices</c> con el cache invalidado —que es
        ''' SIEMPRE en este punto, RecomputeGPUBoneMatrices lo invalida un par de lineas antes— construye el
        ''' cache ENTERO, incluidas las normales de mundo (una <c>Create_Normal_Matrix</c> por vertice mas dos
        ''' arrays <c>Vector3d()</c> por malla y por frame) que un AABB no lee. Medido sobre las 11 mallas del
        ''' arnes (37.321 vertices) con <c>PerVertexMatrixValid</c> invalida —como llega el frame de play—:
        ''' cache completo 10,4-13,2 ms contra 6,9-8,5 ms de esta variante. El ahorro es SOLO la parte de las
        ''' normales; el blend de 4 huesos por vertice, que es lo que domina, lo pagan las dos (una medicion
        ''' que deje <c>PerVertexSkinMatrix</c> valida infla el ahorro aparente).</para>
        ''' <para>Quien necesite normales de mundo (picking, exportador, raytracer de oclusion) las pide por
        ''' <c>GetWorldVertices</c> y las computa en ese momento. Lo que no puede pasar es dejar el cache
        ''' MEDIO lleno, y por eso la variante sin normales no lo marca valido.</para>
        ''' <para><c>Minv/Maxv/Boundingcenter</c> quedan en Double exacto, NO derivados de
        ''' <c>BoundsMin/Max</c> (que son Single): asi la clave de orden del bucket BLENDED no pasa por un
        ''' redondeo de mas.</para></summary>
        Public Sub ComputeBounds()
            If MeshData.Meshgeometry.Vertices Is Nothing OrElse MeshData.Meshgeometry.Vertices.Length = 0 Then
                BoundsMin = New Vector3(Single.MaxValue)
                BoundsMax = New Vector3(Single.MinValue)
                Exit Sub
            End If
            MeshData.Meshgeometry.BoneBoxesValid = False    ' the vertices may have changed (morphs): rebuilt on the next play frame (D-L6)
            SkinningHelper.ComputeWorldBoundsSinNormales(MeshData.Meshgeometry)
            Dim mn = MeshData.Meshgeometry.Minv
            Dim mx = MeshData.Meshgeometry.Maxv
            ' BoundsMin/Max son Single (los consume el culling de frustum). Se REDONDEAN HACIA AFUERA: hacia
            ' adentro, un AABB que ya toca el borde podria descartar una malla visible por un ulp.
            BoundsMin = New Vector3(MathF.BitDecrement(CSng(mn.X)), MathF.BitDecrement(CSng(mn.Y)), MathF.BitDecrement(CSng(mn.Z)))
            BoundsMax = New Vector3(MathF.BitIncrement(CSng(mx.X)), MathF.BitIncrement(CSng(mx.Y)), MathF.BitIncrement(CSng(mx.Z)))
        End Sub

        ''' <summary>D-L6 (chunk C8): the frustum box of a play frame without exact bounds - per palette bone, the 8 corners of the local
        ''' box of the vertices it weighs, through its pose matrix (GPUBoneMatrices); the union contains every skinned vertex (a convex
        ''' combination with normalized weights, the [aabb-por-hueso] proof of the harness). O(bones). Built once per Vertices
        ''' (O(vertices)). Every skinned shape, one bone included (synthetic anchors, rigid skins: they move in play); an unskinned
        ''' shape keeps its box. A shape with a vertex without any weight &gt; 0 gets the exact box, once per frame (rev-05).</summary>
        Public Sub ComputeBoundsFromBoneBoxes()
            Dim geo = MeshData.Meshgeometry
            Dim mats = geo.GPUBoneMatrices
            If geo.Vertices Is Nothing OrElse mats Is Nothing OrElse mats.Length = 0 OrElse Not MeshData.Shape.IsSkinned Then Exit Sub
            If Not geo.BoneBoxesValid Then
                SkinningHelper.BuildBoneBoxes(MeshData.Meshgeometry)
                geo = MeshData.Meshgeometry
            End If
            If Not geo.BoneBoxesUsable Then ComputeBounds() : MeshData.Meshgeometry.BoneBoxesValid = True : Exit Sub
            Dim mn As New Vector3(Single.MaxValue), mx As New Vector3(Single.MinValue)
            For k = 0 To mats.Length - 1
                Dim a = geo.BoneBoxMin(k), b = geo.BoneBoxMax(k)
                If a.X > b.X Then Continue For
                For c = 0 To 7
                    Dim w = Vector3.TransformPosition(New Vector3(If((c And 1) = 0, a.X, b.X), If((c And 2) = 0, a.Y, b.Y), If((c And 4) = 0, a.Z, b.Z)), mats(k))
                    mn = Vector3.ComponentMin(mn, w) : mx = Vector3.ComponentMax(mx, w)
                Next
            Next
            BoundsMin = New Vector3(MathF.BitDecrement(mn.X), MathF.BitDecrement(mn.Y), MathF.BitDecrement(mn.Z))
            BoundsMax = New Vector3(MathF.BitIncrement(mx.X), MathF.BitIncrement(mx.Y), MathF.BitIncrement(mx.Z))
        End Sub

        ''' <summary>Extrae los 6 planos del frustum de una view-projection (Gribb-Hartmann). Separado de
        ''' <see cref="IsAABBInFrustum"/> porque los planos son CONSTANTES para todo un pase: extraerlos por
        ''' malla alocaba un array de 6 Vector4 por llamada, y los dos pases de sombra multiplicaron esa
        ''' cuenta. Se extraen una vez y se pasan.</summary>
        Public Shared Sub ExtractFrustumPlanes(vp As Matrix4, planes As Vector4())
            ' vp is row-major in OpenTK: Row0..Row3
            ' Plane normals point inward; a point is inside when dot+w >= 0 for all planes
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
        End Sub

        ''' <summary>NO USAR EN EL CAMINO DE DIBUJO: aloca los 6 planos en cada llamada. Queda como
        ''' conveniencia para un call site suelto; los bucles de RenderAll y el pase de sombra usan la
        ''' sobrecarga de abajo con un array reusado (_framePlanes / _shadowPlanes). Hoy no la llama
        ''' nadie.</summary>
        Public Shared Function IsAABBInFrustum(bmin As Vector3, bmax As Vector3, vp As Matrix4) As Boolean
            Dim planes(5) As Vector4
            ExtractFrustumPlanes(vp, planes)
            Return IsAABBInFrustum(bmin, bmax, planes)
        End Function

        Public Shared Function IsAABBInFrustum(bmin As Vector3, bmax As Vector3, planes As Vector4()) As Boolean
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

        ' SKYRIM SE decal depth bias (Tools/re-docs/RE_SSE_PASS_GROUPS_DEPTH_2026-10-03.md 15): list 3 (group 2) uses
        ' rasterizer bias index tdb ? 6 + b51 : 0 (0x14151FDA8), list 4 (group 3) 0xA + b51 (0x14151FF57); tdb =
        ' ToggleDepthBias [0x1420D683A], 1 in the PE; b51 = 1 only in an interior cell or a kNoSky / kFixedDimensions
        ' worldspace (0x140657C90). The preview's weathers are exterior ones: indices 6 / 10 = DepthBias -1,
        ' SlopeScaledDepthBias -0.65 (table 0x14102B8D8; clamp -100, never reached). kMAIN is D24_UNORM_S8_UINT, standard
        ' Z (clear 1, LESS_EQUAL), so D3D's DepthBias * 2^-24 + Slope * MaxDepthSlope is GL's polygon offset (units = the
        ' 24-bit r, factor = the slope), same sign. The z-prepass draws its decals with bias 0 (14.3).
        Private Const SseDecalSlopeScaledDepthBias As Single = -0.65F
        Private Const SseDecalDepthBias As Single = -1.0F


        ''' <summary>The depth bias of a colour draw, as a GL polygon offset (both games: D24 UNORM, standard Z - the D3D
        ''' DepthBias in units of 2^-24 and the slope scale map 1:1). SSE: both decal lists (above). FO4: the bias index of
        ''' Fo4RenderPassLaw (decal lists index 1 = -3 / -0.4, flag-45 effects index 4 = -9 / -1.2).</summary>
        Private Function ResolvePolygonOffset(material As MaterialData) As PolygonOffsetState
            Dim mb = material.MaterialBase
            If mb Is Nothing OrElse MeshData.Shape.Wireframe Then Return PolygonOffsetState.Disabled
            If Me.ParentModel.FrameIsSse Then
                If Not mb.RendersAsDecal() Then Return PolygonOffsetState.Disabled
                Return New PolygonOffsetState(True, SseDecalSlopeScaledDepthBias, SseDecalDepthBias)
            End If
            Dim b = Fo4RenderPassLaw.BiasOf(material.Fo4Pass().BiasIndex)
            If b.DepthBias = 0.0F AndAlso b.Slope = 0.0F Then Return PolygonOffsetState.Disabled
            Return New PolygonOffsetState(True, b.Slope, b.DepthBias)
        End Function


        ''' <summary>An effect whose technique has no shader in the game's cache is not drawn. FO4: MULTBLEND_DECAL and
        ''' ADD+MULT, the lookup returns 0 and the draw is skipped (0x142231AE0, 0x14221FB9F; Fo4EffectBlend). SSE: SOFT +
        ''' MULTBLEND_DECAL (src DEST_COLOR + dst INV_SRC_ALPHA; bits 18 and 22 set independently, 0x14152A5B8 / 0x14152A57C):
        ''' 0x440042 exists as neither VS nor PS, BeginTechnique 0x141579120 fails and the pass renderer 0x141560340 skips it
        ''' (Tools/re-docs/RE_SSE_ZPREPASS_BGSM_JSON_2026-10-03.md, C).</summary>
        Private Function EngineSkipsEffectDraw(md As MaterialData) As Boolean
            Dim mb = md?.MaterialBase
            If mb Is Nothing Then Return False
            ' SSE: a shape with no colour pass (SseRenderPassLaw: refraction lighting, kTempRefraction + kDynamicDecal
            ' effects, Hair + DEPTH_WRITE_DECALS without its PS).
            If Me.ParentModel.FrameIsSse AndAlso Not md.SsePass().Drawn Then Return True
            Return EffectSkipNotice(mb) IsNot Nothing
        End Function

        ''' <summary>The notice of an effect whose blend has no shader in the game's cache (EffectDrawSkipped: the engine skips the
        ''' draw), naming the blend pair; Nothing when the effect is drawn, or for a lighting material.</summary>
        Private Function EffectSkipNotice(mb As FO4UnifiedMaterial_Class) As String
            If Not mb.IsBGEM() Then Return Nothing
            Dim ea = mb.ResolveEngineAlpha()
            Dim isSse = Me.ParentModel.FrameIsSse
            If Not EffectDrawSkipped(isSse, mb.SoftEnabled, ea.Blend, ea.Src, ea.Dst) Then Return Nothing
            If isSse Then Return $"Soft effect with blend {ea.Src} / {ea.Dst}, which has no Skyrim SE shader: Skyrim SE does not draw it"
            Return $"Effect with blend {ea.Src} / {ea.Dst}, which has no Fallout 4 shader: Fallout 4 does not draw it"
        End Function

        ''' <summary>Why the GAME draws no colour pass of <paramref name="md"/> on this shape, as the frame's notice says it (user decision
        ''' 5-oct-2026: the law stays the game's, only the silence goes); Nothing when the game draws it, or when only its refraction
        ''' pass does (user decision: no notice). Each reason comes from the law that decides it: Fo4RenderPassLaw / SseRenderPassLaw
        ''' (NoPass), Fo4GBufferPass (MissingPrograms, only in Fallout 4's deferred frame, the one that draws the G-buffer programs),
        ''' EffectSkipNotice. Nothing too for a NoPass of RefractionOnly or LightingAlphaZero (AlphaZeroNotice lists it before this is asked).</summary>
        Friend Function EngineUndrawnNotice(md As MaterialData) As String
            Dim mb = md?.MaterialBase
            If mb Is Nothing Then Return Nothing
            Dim pm = Me.ParentModel
            Dim drawn As Boolean
            If pm.FrameIsSse Then
                Dim p = md.SsePass()
                Select Case p.NoPass
                    Case SseRenderPassLaw.SseNoPass.EffectTempRefractionDynamicDecal
                        Return "Effect with Temp_Refraction and Dynamic_Decal: Skyrim SE does not draw it"
                    Case SseRenderPassLaw.SseNoPass.HairDepthWriteDecalWithoutAlphaTest
                        Return "Hair decal that writes depth, without alpha test, which has no Skyrim SE shader: Skyrim SE does not draw it"
                End Select
                drawn = p.Drawn
            Else
                Dim p = md.Fo4Pass()
                Select Case p.NoPass
                    Case Fo4RenderPassLaw.Fo4NoPass.EffectTempRefraction
                        Return "Effect with Temp_Refraction and without Pipboy_Screen: Fallout 4 does not draw it"
                    Case Fo4RenderPassLaw.Fo4NoPass.LightingBlendWithoutDecal
                        Return "Blended without decal: not drawn"
                End Select
                drawn = p.Drawn
                If drawn AndAlso Not mb.IsBGEM() AndAlso pm.FrameUsesFo4Deferred Then
                    Dim d = md.Fo4GBufferPass(pm.ParentControl.SharedFo4Deferred)
                    If d.HasValue AndAlso d.Value.MissingPrograms IsNot Nothing Then
                        Return $"No {d.Value.MissingPrograms} in Fallout 4 for technique 0x{d.Value.Technique:X8}: Fallout 4 does not draw it"
                    End If
                End If
            End If
            ' A shape without a colour pass whose reason is not above is a refraction shape: its refraction pass draws it.
            If Not drawn Then Return Nothing
            Return EffectSkipNotice(mb)
        End Function

        Private _boundCentreFrame As Integer = -1
        Private _boundCentre As Vector3

        ''' <summary>O-PT (chunk C8): the centre of this shape's world bound as its game computes it (EngineWorldBound.Centre), once per
        ''' frame (PreviewModel.FrameSerial). A shape the game gives no sphere to (a skinned shape without bone spheres): the centre of
        ''' its vertex box, as before (declared).</summary>
        Friend Function WorldBoundCentre() As Vector3
            Dim frame = If(ParentModel Is Nothing, -2, ParentModel.FrameSerial)
            If frame >= 0 AndAlso frame = _boundCentreFrame Then Return _boundCentre
            Dim c = EngineWorldBound.Centre(MeshData.Shape, MeshData.Meshgeometry)
            _boundCentre = If(c.HasValue, c.Value, CType(MeshData.Meshgeometry.Boundingcenter, Vector3))
            _boundCentreFrame = frame
            Return _boundCentre
        End Function

        Private _sceneVisFrame As Integer = -1
        Private _sceneVis As NifSceneVisibility.LoadState

        ''' <summary>This shape's NifSceneVisibility.LoadState (chunk C3), evaluated once per frame (PreviewModel.FrameSerial, the memo of
        ''' MaterialData.Fo4EngineState: an edit between frames - WM's UpdateBounds, Make helper - is read on the next one).</summary>
        Friend Function SceneVisibility() As NifSceneVisibility.LoadState
            Dim frame = If(ParentModel Is Nothing, -2, ParentModel.FrameSerial)
            If frame >= 0 AndAlso frame = _sceneVisFrame Then Return _sceneVis
            _sceneVis = NifSceneVisibility.Evaluate(MeshData?.Shape)
            _sceneVisFrame = frame
            Return _sceneVis
        End Function

        ''' <summary>The view draws, for editing, what the game does not (RenderIntent.DrawEngineSkippedForEditing, C2 L5); a mesh outside
        ''' any control: the composite rule (RenderIntent's default).</summary>
        Friend Function DrawEngineSkippedForEditing() As Boolean
            Dim pc = ParentModel?.ParentControl
            Return pc IsNot Nothing AndAlso pc.Intent.DrawEngineSkippedForEditing
        End Function

        ''' <summary>The draw gate of every pass of this mesh: HelperShapeGate.IsShapeDrawable with this frame's SceneVisibility and view.</summary>
        Friend Function IsDrawable() As Boolean
            Dim shp = MeshData?.Shape
            If shp Is Nothing OrElse shp.RenderHide Then Return False
            Return HelperShapeGate.IsShapeDrawable(shp, SceneVisibility(), DrawEngineSkippedForEditing())
        End Function

        ''' <summary>The name the frame's notice lists this shape (or its overlay layer <paramref name="layer"/>) under: one entry per
        ''' instance - the shape name and the mesh's index in the model ("name [idx]", two NIFs may carry shapes of the same name),
        ''' then the layer's position ("name [idx] (overlay n)").</summary>
        Friend Function NoticeName(layer As OverlayMaterialLayer) As String
            Dim name = $"{MeshData.ShapeName} [{MeshData.Idx}]"
            If layer Is Nothing Then Return name
            Dim layers = MeshData.Shape?.OverlayLayers
            Dim n = 0
            If layers IsNot Nothing Then
                For i = 0 To layers.Count - 1
                    If layers(i) Is layer Then n = i + 1 : Exit For
                Next
            End If
            Return $"{name} (overlay {n})"
        End Function

        ''' <summary>The reason of the frame's notice for a precombined shape (C-PC): the game draws its .csg geometry, the preview does
        ''' not read it (user decision 6-oct-2026).</summary>
        Friend Const PrecombinedGap As String = "Precombined: geometry lives in the .csg, not supported"

        ''' <summary>S-P: the reason of the frame's notice for a Parallax technique (type 3) whose VS camera cb2[6] SetupGeometry does not
        ''' write (SseRenderPassLaw.LitViewVectorWritten): its relief reads the buffer's previous content in the game; the preview uses
        ''' the camera (user decision 6-oct-2026: the game's law + a notice). 2 shapes in the corpus (shaderspheres02, 03000001).</summary>
        Friend Const ParallaxUndefinedView As String = "Parallax without specular: Skyrim SE leaves its view vector unset, the relief in the game is undefined; drawn with the camera's"

        ''' <summary>S-N: the reason of the frame's notice for a snow technique (bit 21): the SAO composite's sparkles take their colour from
        ''' the scene (iSnowSparklesColor 2, 0x141542E6C..E85: not traced), the preview does not draw them (user decision 6-oct-2026).</summary>
        Friend Const SnowSparklesNotDrawn As String = "Snow sparkles not drawn: their colour comes from the scene"

        ''' <summary>The S-P notice of <paramref name="md"/>, Nothing when it does not apply.</summary>
        Private Function ParallaxViewNotice(md As MaterialData) As String
            If Not Me.ParentModel.FrameIsSse Then Return Nothing
            Dim s = md.SseInputs()
            If SseRenderPassLaw.LitTechniqueType(s) <> 3 OrElse SseRenderPassLaw.LitViewVectorWritten(s) Then Return Nothing
            Return ParallaxUndefinedView
        End Function

        ''' <summary>S-O1: the reason of the frame's notice for an SSE eye without per-vertex eye data (UI English).</summary>
        Friend Const SseEyeWithoutEyeData As String = "Eye without per-vertex eye data: Skyrim SE has no reflection centre for it; reflection about the vertex normal"

        ''' <summary>C-PC: a precombined shape (NifContent.HasPrecombinedData) whose NIF carries no vertices - all 1,072,150 measured
        ''' (re2/cpc, data size 0): the preview has nothing to draw. The vertex test goes first: it is the cheap one.</summary>
        Friend Function IsPrecombinedWithoutGeometry() As Boolean
            Dim v = MeshData.Meshgeometry.Vertices
            If v IsNot Nothing AndAlso v.Length > 0 Then Return False
            Dim shp = MeshData.Shape
            Return shp?.NifContent IsNot Nothing AndAlso shp.NifShape IsNot Nothing AndAlso shp.NifContent.HasPrecombinedData(shp.NifShape)
        End Function

        ''' <summary>The preview's own gap for <paramref name="md"/> in Fallout 4's deferred frame (Fo4GBufferDraw.Gap), Nothing when
        ''' it has none. One place: ReportUndrawn reads it for the gap notice and to know whether the preview draws the shape.</summary>
        Private Function PreviewGap(md As MaterialData) As String
            Dim pm = Me.ParentModel
            If Not pm.FrameUsesFo4Deferred OrElse md.MaterialBase.IsBGEM() Then Return Nothing
            Dim d = md.Fo4GBufferPass(pm.ParentControl.SharedFo4Deferred)
            Return If(d.HasValue AndAlso d.Value.Gap <> "", d.Value.Gap, Nothing)
        End Function

        ''' <summary>F-A0 / S-ND: the material alpha 0 (or NaN) for which the GAME builds no pass (MaterialData.EngineAlphaSkipsPasses,
        ''' on the RAW alpha), as the notice says it: Drawn = the reason when the preview shows the shape, Undrawn = when it does not.
        ''' "at rest" when the shape's shader carries an alpha controller (MaterialData.AlphaController); a base shape drawn for
        ''' editing (L5) is drawn with MaterialData.PreviewAlpha, which the reason states. Nothing when the law does not cut.</summary>
        Friend Function AlphaZeroNotice(md As MaterialData, layer As OverlayMaterialLayer) As (Drawn As String, Undrawn As String)?
            If Not md.EngineAlphaSkipsPasses() Then Return Nothing
            Dim mb = md.MaterialBase
            Dim game = If(Me.ParentModel.FrameIsSse, "Skyrim SE", "Fallout 4")
            Dim what = $"Material alpha {If(Single.IsNaN(mb.Alpha), "NaN", "0")}{If(md.AlphaController().Present, " at rest (an alpha controller animates it)", "")}"
            Dim shownAs = If(layer Is Nothing,
                             $"; shown with alpha {md.PreviewAlpha().ToString("0.###", Globalization.CultureInfo.InvariantCulture)}", "")
            Return ($"{game}: {what}{shownAs}", $"{what}: {game} does not draw it")
        End Function

        ''' <summary>Lists this shape - or its overlay layer <paramref name="layer"/>, a shape of its own in the engine - in the frame's
        ''' notice, in the engine's order (rev-11): (1) the material alpha 0 - GetRenderPasses tests it before anything else it cuts on
        ''' (FO4 0x14217A147 before blend 0x14217A16A and refraction 0x14217A566; SSE 0x14151A554 before refraction 0x14151A6BB) -
        ''' listed as drawn for editing when the preview shows it, else as not drawn; (2) a precombined shape (preview gap); (3) the
        ''' preview's own gap (Fo4GBufferDraw.Gap); (4) the game's own law the preview follows (EngineUndrawnNotice).
        ''' PreviewModel.ReportUndrawnShapes calls it once per visible shape and layer per frame. A shape the preview never draws for
        ''' the app's own reasons (hidden, a helper or under a hidden node with the checkbox off, without geometry other than a precombined
        ''' one, a wireframe) is not listed. Before all of them, C-NAN (chunk C3): the game's culling of a world bound of radius 0.</summary>
        Friend Sub ReportUndrawn(layer As OverlayMaterialLayer)
            Dim shp = MeshData.Shape
            If shp Is Nothing OrElse shp.Wireframe OrElse shp.NifShape Is Nothing Then Exit Sub
            Dim vis = SceneVisibility()
            If Not HelperShapeGate.IsShapeShown(shp, vis) Then Exit Sub
            ' C-NAN (chunk C3) first: the game's culling never reaches GetRenderPasses for it (BSCullingProcess SSE 0x140FEDA82, FO4
            ' 0x1417E1C6D), so no later reason applies. Listed once, under the base shape (its overlay layers leave every pass with it
            ' and are not NIF shapes with a bound of their own). A shape with its OWN hidden bit or without a shader (IsHelperShape)
            ' is not listed even with the checkbox on (user decision rev-05, C-NAN only). Composite view: not drawn (EngineUndrawn);
            ' piece view: drawn for editing (DrawnForEditing, C2 L5).
            If vis.BoundCullReason IsNot Nothing Then
                If layer Is Nothing AndAlso Not shp.IsHelperShape Then
                    If DrawEngineSkippedForEditing() Then
                        ParentModel.ReportUndrawn(NoticeName(Nothing), vis.BoundCullShown, FrameNoticeKind.DrawnForEditing)
                    Else
                        ParentModel.ReportUndrawn(NoticeName(Nothing), vis.BoundCullReason, FrameNoticeKind.EngineUndrawn)
                    End If
                End If
                Exit Sub
            End If
            Dim pm = Me.ParentModel
            Dim name = NoticeName(layer)
            Dim md = If(layer Is Nothing, MeshData.Material, OverlayMaterialData(layer))
            Dim precombined = layer Is Nothing AndAlso IsPrecombinedWithoutGeometry()
            If md?.MaterialBase IsNot Nothing Then
                Dim alpha0 = AlphaZeroNotice(md, layer)
                If alpha0.HasValue Then
                    ' Shown: the preview has vertices, no gap, a pass the game's law gives it, and the pass law did not cut it for the alpha it
                    ' DRAWS (DrawnAlphaSkipsPasses: the law's own NoPass, one owner - C2 v6 L7). Its kind: shown at its animated maximum when
                    ' PreviewAlphaRule took the controller's maximum, else drawn for editing (L5).
                    Dim shown = Not precombined AndAlso PreviewGap(md) Is Nothing AndAlso EngineUndrawnNotice(md) Is Nothing AndAlso
                                Not md.DrawnAlphaSkipsPasses(pm.FrameIsSse)
                    If shown Then
                        pm.ReportUndrawn(name, alpha0.Value.Drawn, If(md.PreviewAlphaRule().Animated, FrameNoticeKind.ShownAtAnimatedMaximum, FrameNoticeKind.DrawnForEditing))
                    Else
                        pm.ReportUndrawn(name, alpha0.Value.Undrawn, FrameNoticeKind.EngineUndrawn)
                    End If
                    Exit Sub
                End If
            End If
            If precombined Then
                pm.ReportUndrawn(name, PrecombinedGap, FrameNoticeKind.PreviewGap)
                Exit Sub
            End If
            If md?.MaterialBase Is Nothing Then Exit Sub
            Dim gap = PreviewGap(md)
            If gap IsNot Nothing Then
                pm.ReportUndrawn(name, gap, FrameNoticeKind.PreviewGap)
                Exit Sub
            End If
            Dim why = EngineUndrawnNotice(md)
            If why IsNot Nothing Then
                pm.ReportUndrawn(name, why, FrameNoticeKind.EngineUndrawn)
                Exit Sub
            End If
            ' C10 rev-04 (user decision 7-oct-2026): a value this shape's draw takes from a shader controller at rest, not from the block
            ' (MaterialData.RestDrivenNotice). Under the same exits as C4 below (a shape the frame draws as the game does); the base shape
            ' only (an overlay layer has no NIF property), never a helper shape the user chose to see.
            If layer Is Nothing AndAlso Not shp.IsHelperShape Then
                Dim driven = md.RestDrivenNotice()
                If driven IsNot Nothing Then pm.ReportUndrawn(name, driven, FrameNoticeKind.DrivenByControllerAtRest)
            End If
            ' C4: missing textures, empty slots sampled with a non-neutral default, what the preview draws differently
            ' (MaterialData.TextureNotices) - only for a shape the frame draws as the game does (the earlier exits: alpha 0,
            ' precombined, the preview's gap, the game's own law). A helper shape the user chose to see is not listed.
            If PreviewModel.GateNoTextureNoticeCensus OrElse (layer Is Nothing AndAlso shp.IsHelperShape) Then Exit Sub
            For Each n In md.TextureNotices()
                pm.ReportUndrawn(name, n.Reason, n.Kind)
            Next
            ' TECHNIQUE NOTICES of Skyrim SE (what the preview draws differently because of the technique), under the same exits as
            ' C4's lines above: only a shape the frame draws as the game does, never a helper shape the user chose to see.
            ' S-O1: an SSE eye (technique type 16) without its reflection centres - no per-vertex eye data, or no type-16 block to carry
            ' them (45 shapes in sse_lit_shapes.pkl: UBE lenses, bsver 83 NiTriShape eyes, childfeet / benthiclurker): the game's input
            ' layout has no TEXCOORD2 for it (0x141011AB8..B0E) and what it draws is not traced; the preview reflects about the vertex
            ' normal (Fragment_SSE bEyeRadial).
            If pm.FrameIsSse AndAlso md.SseLitTechniqueType() = &H10 AndAlso layer Is Nothing AndAlso vboEyeCenter = 0 Then
                pm.ReportUndrawn(name, SseEyeWithoutEyeData, FrameNoticeKind.DrawnDifferently)
            End If
            ' S-P (user decision 6-oct-2026): a Parallax technique whose view vector the game leaves unwritten (ParallaxViewNotice).
            Dim parallaxV = ParallaxViewNotice(md)
            If parallaxV IsNot Nothing Then pm.ReportUndrawn(name, parallaxV, FrameNoticeKind.DrawnDifferently)
            ' S-N: the composite's snow sparkles are not drawn (their colour, iSnowSparklesColor 2, comes from the scene: not traced).
            If pm.FrameIsSse AndAlso Not md.MaterialBase.IsBGEM() AndAlso
               SseRenderPassLaw.SnowTechnique(md.SseInputs(), SseRenderPassLaw.ImprovedSnowExeDefault) Then
                pm.ReportUndrawn(name, SnowSparklesNotDrawn, FrameNoticeKind.DrawnDifferently)
            End If
        End Sub

        ''' <summary>The depth state of this material's colour pass: Skyrim SE by SseRenderPassLaw (the list's default, the
        ''' shader's SetupGeometry, EQUAL for the opaque batch, the billboard list's inherited mode), Fallout 4 by its
        ''' test / write rules. The one place Render, RenderOverlayLayer and ApplyMaterial read it from.</summary>
        Friend Function ResolveColourDepthState(material As MaterialData) As (Test As Boolean, Func As DepthFunction, Write As Boolean)
            Dim mb = material.MaterialBase
            Dim wire = MeshData.Shape.Wireframe
            If Me.ParentModel.FrameIsSse AndAlso Not wire AndAlso mb IsNot Nothing Then
                Dim inp = material.SseInputs()
                Dim pass = SseRenderPassLaw.Classify(inp)
                Dim f = pass.DepthFunc
                Dim w = pass.DepthWrite
                If pass.List = SseRenderPassLaw.SseList.Billboard AndAlso SseRenderPassLaw.BillboardInheritsEqual(inp, Me.ParentModel.FrameBillboardEffectDrawn) Then
                    f = SseRenderPassLaw.SseDepthFunc.Equal : w = False
                End If
                Select Case f
                    Case SseRenderPassLaw.SseDepthFunc.Off : Return (False, DepthFunction.Lequal, False)
                    Case SseRenderPassLaw.SseDepthFunc.Equal
                        If PreviewModel.GateDisablePrepass Then Return (True, DepthFunction.Lequal, True)
                        Return (True, DepthFunction.Equal, False)
                    Case Else : Return (True, DepthFunction.Lequal, w)
                End Select
            End If
            ' Wireframe (app UI): tested, never written. No material: the plain opaque state.
            If wire Then Return (True, DepthFunction.Lequal, False)
            If mb Is Nothing OrElse Me.ParentModel.FrameIsSse Then Return (True, DepthFunction.Lequal, True)
            Dim fo4 = material.Fo4Pass()
            ' Gate only: without the prepass an eligible shape writes its own depth (the pre-prepass behaviour).
            If PreviewModel.GateDisablePrepass AndAlso fo4.InPrepass Then Return (fo4.DepthTest, DepthFunction.Lequal, True)
            ' Gate only (--fo4-decal-base b1): the translucent decals test EQUAL against their base.
            If PreviewModel.GateDecalColourEqual AndAlso fo4.Group = 3 Then Return (fo4.DepthTest, DepthFunction.Equal, fo4.DepthWrite)
            Return (fo4.DepthTest, DepthFunction.Lequal, fo4.DepthWrite)
        End Function

        ''' <summary>Binds the engine's sampler of each material texture unit (SamplerLaw) for this draw: the material's clamp or
        ''' the slot's fixed address mode, the slot's filter (an inherited one reads the frame's per-slot state, an explicit one
        ''' writes it), the non-mip form for a single-level texture. <paramref name="soloUnidades"/> limits it to those units (the
        ''' utility passes: prepass, shadow map, refraction).</summary>
        Private Sub BindMaterialSamplers(material As MaterialData, slots As List(Of SamplerLaw.SlotSampler))
            Dim cache = Me.ParentModel.ParentControl.Samplers
            Dim estado = Me.ParentModel.FrameSamplerFilter
            For Each sl In slots
                Dim filt = sl.Filt
                If filt = SamplerLaw.FiltInherit Then filt = estado(sl.EngineSlot) Else estado(sl.EngineSlot) = filt
                Dim unidad = CInt(sl.Unit)
                GL.ActiveTexture(TextureUnit.Texture0 + unidad)
                Dim tex As Integer
                GL.GetInteger(If(sl.Unit = SamplerLaw.AppUnit.Cube, GetPName.TextureBindingCubeMap, GetPName.TextureBinding2D), tex)
                GL.BindSampler(unidad, cache.Get_(sl.Addr, filt, sl.Lod, TextureSamplers.HasMips(tex)))
                _samplerUnitsBound = _samplerUnitsBound Or (1 << unidad)
            Next
        End Sub

        ''' <summary>Back to the textures' own sampling state on every unit this mesh bound a sampler to.</summary>
        Private Sub UnbindMaterialSamplers()
            For u = 0 To 15
                If (_samplerUnitsBound And (1 << u)) <> 0 Then GL.BindSampler(u, 0)
            Next
            _samplerUnitsBound = 0
        End Sub
        Private _samplerUnitsBound As Integer

        ''' <summary>The z-prepass state of a draw (Skyrim SE, SseRenderPassLaw / SsePrepassSource): depth only - the
        ''' colour masked off, no blend, depth test + write - and the prepass alpha test's uniforms. Called after
        ''' ApplyMaterial, which leaves bSsePrepass off for every colour draw.</summary>
        Private Sub ApplySsePrepassState(shader As Shader_Base_Class, material As MaterialData)
            If Me.ParentModel.FrameIsSse Then
                Dim inp = material.SseInputs()
                Dim pass = SseRenderPassLaw.Classify(inp)
                shader.SetBool("bSsePrepass", True)
                shader.SetBool("bSsePrepassAlphaTest", pass.PrepassAlphaTest)
                shader.SetBool("bSsePrepassDecal", inp.Decal OrElse inp.DynamicDecal)
                shader.SetFloat("ssePrepassThrG", pass.PrepassThreshold)
                shader.SetFloat("ssePrepassThrS", SseRenderPassLaw.SubgroupThreshold(inp))
            Else
                ' FO4 (Fo4RenderPassLaw, 10): the 0x1A test; the vertex alpha the VS passes is the geometry's own when it
                ' has a COLOR stream (the prepass ignores the display toggles of the lit pass).
                Dim inp = material.Fo4Inputs()
                Dim pass = Fo4RenderPassLaw.Classify(inp)
                shader.SetBool("bFo4Prepass", True)
                shader.SetBool("bFo4PrepassAlphaTest", pass.PrepassAlphaTest)
                shader.SetBool("bFo4PrepassVertexAlpha", inp.HasColorStream)
                shader.SetBool("bShowVertexAlpha", inp.HasColorStream)
                shader.SetFloat("fo4PrepassThreshold", pass.PrepassThreshold)
            End If
            GL.Disable(EnableCap.Blend)
            GL.Disable(EnableCap.PolygonOffsetFill)     ' bias 0 in the prepass (14.3)
            GL.ColorMask(False, False, False, False)
            GL.Enable(EnableCap.DepthTest)
            GL.DepthFunc(SsePrepassDepthFunc)
            GL.DepthMask(True)
        End Sub

        ''' <summary>The base depth draw of a translucent decal (both games, PreviewModel.DrawOpaqueStage; user decisions 5-oct-2026 and
        ''' 7-oct-2026), called after ApplyMaterial: the decal's own colour draw - its program, constants, textures, culling and depth
        ''' bias, so its fragments, kills and depths are the ones its colour draw will have - made depth only: colour writes and blend
        ''' off, depth GREATER with writes on, into the auxiliary target DecalBaseTarget.Begin bound (marked pixels at 0.0; stencil
        ''' DecalBaseTarget.DepthDrawStencil: the farthest decal surface of each marked pixel stays, written by rasterisation). Which fragments
        ''' give a base: a G-buffer record's own kill (all 143 records with technique bit 15 kill A x AlphaScale.x - 0.015686 &lt; 0 and
        ''' output o0.w = A x AlphaScale.x, A = their texture [x vertex] alpha, so every fragment that survives paints); an effect decal
        ''' (the game shader) by its blend's source factor (uDecalBaseMode, DecalBaseMode: SRC_ALPHA discards alpha &lt;= 0; ONE / DEST_COLOR keeps
        ''' every fragment that survives its own discards). Gate mutants: GateDecalBaseNoDiscard (no discard),
        ''' GateDecalBaseModeOneForAll (mode 1 for every effect), Fo4DeferredTargets.GateDecalBaseNearest (LESS).</summary>
        Private Sub ApplyDecalBaseState(shader As Shader_Base_Class, material As MaterialData, gbuf As Fo4GBufferDraw?)
            If gbuf.HasValue Then
                ' AlphaScale (1, 0): A = 1 and x 1, the record's kill never fires.
                If PreviewModel.GateDecalBaseNoDiscard Then shader.SetVector4("AlphaScale", New Vector4(1.0F, 0.0F, 0.0F, 0.0F))
            Else
                shader.SetInt("uDecalBaseMode", DecalBaseMode(material, Me.ParentModel.FrameIsSse))
            End If
            GL.ColorMask(False, False, False, False)
            GL.Disable(EnableCap.Blend)
            GL.Enable(EnableCap.DepthTest)
            GL.DepthFunc(If(Fo4DeferredTargets.GateDecalBaseNearest, DepthFunction.Less, DepthFunction.Greater))
            GL.DepthMask(True)
            If PreviewModel.GateDecalBaseNoBias Then GL.Disable(EnableCap.PolygonOffsetFill)
            DecalBaseTarget.DepthDrawStencil()
            Me.ParentModel.ParentControl.FrameDecalBase.DepthDrawAlpha()
        End Sub

        ''' <summary>The colour blend of a translucent decal's draw on RT 0 (Fallout 4's G-buffer) or on the radiance target, from the one
        ''' place its colour draw takes it: a G-buffer record (<paramref name="gbuf"/>) Fo4GBufferBlendMode (ApplyFo4GBufferDraw; mode 6
        ''' = RT 0 DEST_COLOR / ZERO, 0x1418552AA); otherwise under HasAlphaBlend (ApplyMaterial), a Fallout 4 effect Fo4EffectBlend,
        ''' Skyrim SE Calculate_Blending. Enabled False: the draw is not blended.</summary>
        Friend Shared Function DecalColourBlend(material As MaterialData, gbuf As Fo4GBufferDraw?, isSse As Boolean) As (Enabled As Boolean, Src As BlendingFactor, Dst As BlendingFactor)
            If gbuf.HasValue Then
                Dim mode = Fo4GBufferBlendMode(material, gbuf.Value)
                If mode = 0 Then Return (False, BlendingFactor.One, BlendingFactor.Zero)
                If mode = 6 Then Return (True, BlendingFactor.DstColor, BlendingFactor.Zero)
                Dim m = Fo4BlendModeFactors(mode)
                Return (True, m.ColorSrc, m.ColorDst)
            End If
            If Not material.HasAlphaBlend Then Return (False, BlendingFactor.One, BlendingFactor.Zero)
            If isSse Then
                Dim b = material.Calculate_Blending()
                Return (True, CType(b(0), BlendingFactor), CType(b(1), BlendingFactor))
            End If
            Dim mb = material.MaterialBase
            Dim ea = mb.ResolveEngineAlpha()
            Dim f = Fo4EffectBlend(ea.Present, ea.Blend, ea.Src, ea.Dst, mb.Alpha)
            Return (f.Enabled, f.ColorSrc, f.ColorDst)
        End Function

        ''' <summary>The blend mode of a lighting shape's G-buffer draw: SetupAlphaBlend 0x1422301E0 (Fo4SetupAlphaBlendMode) with the
        ''' pass alpha [prop+0x28] (0x14217A47C..4AB) of THIS draw (d.Material.Alpha, the alpha the preview draws, Fo4GBufferPass), not
        ''' called when technique AND 0x1000020 = 0x1000000 (0x142207116..0x142207120: the list's state, no blend); property bit 49 =
        ''' mode 6 (0x14220784E..0x14220789C). 0 = no blend. One law for ApplyFo4GBufferDraw and the decals' base (DecalColourBlend).</summary>
        Friend Shared Function Fo4GBufferBlendMode(material As MaterialData, d As Fo4GBufferDraw) As Integer
            Dim ea = material.MaterialBase.ResolveEngineAlpha()
            Dim mode = 0
            If (d.Technique And &H1000020UI) <> &H1000000UI Then mode = Fo4SetupAlphaBlendMode(ea.Present, ea.Blend, ea.Src, ea.Dst, d.Material.Alpha)
            If ((d.Flags >> 49) And 1UL) <> 0UL Then mode = 6
            Return mode
        End Function

        ''' <summary>Which fragments of a game-shader decal (FO4: an effect; SSE: every decal) give a base: those that change the
        ''' destination under its colour draw's blend (DecalColourBlend). A fragment leaves the destination unchanged when its source
        ''' factor is 0 and its destination factor 1 (GL 4.6 17.3.6.2, Table 17.2); for the factors that depend on the source alpha
        ''' only: source ZERO always 0, SRC_ALPHA 0 at alpha &lt;= 0, ONE_MINUS_SRC_ALPHA 0 at alpha &gt;= 1; destination ONE always 1,
        ''' ONE_MINUS_SRC_ALPHA 1 at alpha &lt;= 0, SRC_ALPHA 1 at alpha &gt;= 1. Any other factor depends on a colour and is taken as
        ''' painting (proposal B's rule for ONE / DEST_COLOR). uDecalBaseMode: 0 no blend (every surviving fragment paints), 1 discard
        ''' alpha &lt;= 0, 2 keep all, 3 discard all, 4 discard alpha &gt;= 1. Fallout 4's modes 1..4 give 1, 1, 2, 2 (proposal B).</summary>
        Private Shared Function DecalBaseMode(material As MaterialData, isSse As Boolean) As Integer
            Dim b = DecalColourBlend(material, Nothing, isSse)
            Dim mode = 0
            If b.Enabled Then
                ' Where each factor takes its neutral value: 3 always, 1 alpha <= 0, 4 alpha >= 1, 0 never; the unchanged set is their
                ' intersection (never -> 2).
                Dim s0 = If(b.Src = BlendingFactor.Zero, 3, If(b.Src = BlendingFactor.SrcAlpha, 1, If(b.Src = BlendingFactor.OneMinusSrcAlpha, 4, 0)))
                Dim d1 = If(b.Dst = BlendingFactor.One, 3, If(b.Dst = BlendingFactor.OneMinusSrcAlpha, 1, If(b.Dst = BlendingFactor.SrcAlpha, 4, 0)))
                mode = If(s0 = 0 OrElse d1 = 0, 2, If(s0 = 3, d1, If(d1 = 3 OrElse d1 = s0, s0, 2)))
            End If
            If PreviewModel.GateDecalBaseNoDiscard Then mode = 0
            If PreviewModel.GateDecalBaseModeOneForAll Then mode = 1
            Return mode
        End Function

        ''' <summary>THE OPACITY OF A DECAL'S BASE (user decisions 7-oct-2026: 3, the base follows the decal's alpha, mixing with the
        ''' background in the same proportion; review rev-06, for every blended decal whatever its factors): 0 = its colour draw
        ''' (DecalColourBlend) is unblended - opacity 1; 1 = blended - opacity = its colour-output alpha. The base is a premultiplied
        ''' layer of that opacity and its coverage (Fo4DeferredAppSource.Fragment_DecalBaseSurface): with a source factor that is a
        ''' colour (Skyrim SE MULTBLEND_DECAL DEST_COLOR / INV_SRC_ALPHA, Fallout 4 mode 6 / 3 DEST_COLOR / ZERO) the decal over it gives
        ''' exactly "decal over a surface B" ((D + 1 - a) B, D B), the decal adding no coverage of its own (DecalCoverageMode 2). Gate
        ''' mutant: PreviewModel.GateDecalBaseOpaque (0 for every decal: the opaque base of propuesta v1).</summary>
        Friend Shared Function DecalBaseOpacity(material As MaterialData, gbuf As Fo4GBufferDraw?, isSse As Boolean) As Integer
            If PreviewModel.GateDecalBaseOpaque Then Return 0
            Return If(DecalColourBlend(material, gbuf, isSse).Enabled, 1, 0)
        End Function

        ''' <summary>The coverage a decal's colour draw adds, by the app's one coverage law (CoverageModeForSourceFactor of its colour
        ''' source factor, DecalColourBlend - the law ApplyMaterial gives the forward draws; the post mixes the background by that
        ''' coverage, PostProcess.vb Fragment_PostFo4 / Fragment_PostSse): 0 = unblended (coverage 1), 1 = its alpha, over-accumulated
        ''' (SRC_ALPHA, ONE, ...), 2 = none (a source factor that is a colour: ZERO, DEST_COLOR, ...). Fallout 4's coverage draws
        ''' (DrawDecalBaseSurface) follow it.</summary>
        Friend Shared Function DecalCoverageMode(material As MaterialData, gbuf As Fo4GBufferDraw?, isSse As Boolean) As Integer
            Dim b = DecalColourBlend(material, gbuf, isSse)
            Return If(b.Enabled, CoverageModeForSourceFactor(b.Src), 0)
        End Function

        ''' <summary>After a base draw: the game shader's base switch off again (uniforms persist per program) and the colour writes back.</summary>
        Private Sub EndDecalBaseDraw(shader As Shader_Base_Class, gbuf As Fo4GBufferDraw?)
            If Not gbuf.HasValue Then shader.SetInt("uDecalBaseMode", 0)
            GL.ColorMask(True, True, True, True)
        End Sub

        ''' <summary>THE BASE SURFACE of a translucent decal (user decisions 7-oct-2026, both games), drawn after every depth draw of
        ''' the base pass (PreviewModel.DrawOpaqueStage, DecalBaseStep.Surface) with the same geometry, vertex inputs, culling and depth
        ''' bias as its depth draw, under DecalBaseTarget.SurfaceDrawStencil (depth EQUAL: only the farthest painting decal's fragments;
        ''' one per pixel). Its opacity is DecalBaseOpacity (the base follows the decal's alpha):
        ''' <list type="bullet">
        ''' <item>Fallout 4: Fo4DeferredPrograms.DecalBaseSurface writes the six G-buffer RTs of a plain surface, its albedo
        ''' premultiplied by the opacity (uBaseOpacity; the decal's alpha from DecalBaseTarget.AlphaTexture at unit 2), and adds that
        ''' opacity to the coverage target (its buffer 6, blended ONE / ONE_MINUS_SRC_ALPHA: the base is a layer); its VS is
        ''' Vertex_FO4 with the uniforms that decide the position set as for the depth draw (invariant gl_Position, GLSL 4.60
        ''' 4.8.1). Then the coverage draw of the decal's own colour draw (DecalCoverageMode): its own program into DecalBaseTarget's
        ''' coverage target (CoverageDrawStencil), alpha blended ONE / ONE_MINUS_SRC_ALPHA (mode 1: its alpha, the over-accumulation the
        ''' HDR coverage takes from the forward draws, ApplyMaterial) or 1 (mode 0, unblended: logic op SET); none for mode 2 (a colour
        ''' source factor adds no coverage). Composite 2 hands that coverage to the post.</item>
        ''' <item>Skyrim SE: the decal's own program in its uSseDecalBaseSurface mode with its discards (uDecalBaseMode: the same
        ''' fragments), forward-lit, blended by its alpha - radiance SRC_ALPHA / ONE_MINUS_SRC_ALPHA, coverage ONE / ONE_MINUS_SRC_ALPHA
        ''' (uCoverageMode 1, the forward law) - or unblended with coverage 1. Buffer 0's alpha factors are ZERO / ONE (review rev-01:
        ''' in the display target, post off, the frame's alpha stays; the HDR radiance target has no alpha).</item>
        ''' </list>
        ''' Gate mutants: PreviewModel.GateDecalBaseNoSurface (the surface not drawn; Fallout 4's coverage draws still are), GateDecalBaseOpaque (opacity 1), GateDecalBaseNoCoverage
        ''' (Fallout 4: no coverage draws).</summary>
        Private Sub DrawDecalBaseSurface(shader As Shader_Base_Class, material As MaterialData, gbuf As Fo4GBufferDraw?, projection As Matrix4,
                                         view As Matrix4, model As Matrix4, modelView As Matrix4, modelViewInverse As Matrix4,
                                         normalMatrix As Matrix3, boneCount As Integer)
            Dim pm = Me.ParentModel
            Dim target = pm.ParentControl.FrameDecalBase
            Dim opacity = DecalBaseOpacity(material, gbuf, pm.FrameIsSse)
            If PreviewModel.GateDecalBaseNoBias Then GL.Disable(EnableCap.PolygonOffsetFill)
            GL.Enable(EnableCap.DepthTest)
            If pm.FrameIsSse Then
                If PreviewModel.GateDecalBaseNoSurface Then Exit Sub
                target.SurfaceDrawStencil()
                shader.SetInt("uDecalBaseMode", DecalBaseMode(material, True))
                shader.SetBool("uSseDecalBaseSurface", True)
                ' Buffer 0: the radiance blended by the opacity, its alpha ZERO / ONE (the display's alpha stays); buffer 1: the coverage,
                ' over-accumulated (opacity 1) or replaced by the shader's 1 (opacity 0, uCoverageMode 0).
                GL.Enable(EnableCap.Blend)
                If opacity = 1 Then
                    GL.BlendFuncSeparate(0, BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha, BlendingFactorSrc.Zero, BlendingFactorDest.One)
                    GL.BlendFunc(1, BlendingFactorSrc.One, BlendingFactorDest.OneMinusSrcAlpha)
                Else
                    GL.BlendFuncSeparate(0, BlendingFactorSrc.One, BlendingFactorDest.Zero, BlendingFactorSrc.Zero, BlendingFactorDest.One)
                    GL.BlendFunc(1, BlendingFactorSrc.One, BlendingFactorDest.Zero)
                End If
                shader.SetInt("uCoverageMode", If(opacity = 1, 1, 0))
                GL.DrawElements(PrimitiveType.Triangles, indexCount, DrawElementsType.UnsignedInt, 0)
                shader.SetBool("uSseDecalBaseSurface", False)
                GL.Disable(EnableCap.Blend)
            Else
                If Not PreviewModel.GateDecalBaseNoSurface Then
                    target.SurfaceDrawStencil()
                    ' The six G-buffer RTs unblended (the premultiplied surface); the coverage target (buffer 6) accumulates a.
                    GL.Disable(EnableCap.Blend)
                    GL.Enable(IndexedEnableCap.Blend, 6)
                    GL.BlendFunc(6, BlendingFactorSrc.One, BlendingFactorDest.OneMinusSrcAlpha)
                    GL.ColorMask(6, False, False, False, True)
                    Dim p = pm.ParentControl.SharedFo4Deferred.DecalBaseSurface
                    Dim mb = material.MaterialBase
                    p.Use()
                    p.SetMatrix4("matProjection", projection)
                    p.SetMatrix4("matView", view)
                    p.SetMatrix4("matModel", model)
                    p.SetMatrix4("matModelView", modelView)
                    p.SetMatrix4("matModelViewInverse", modelViewInverse)
                    p.SetMatrix3("mv_normalMatrix", normalMatrix)
                    p.SetBool("bModelSpace", mb.ModelSpaceNormals)
                    Dim uv = material.RestUv()
                    p.SetVector2("uvOffset", uv.Offset)
                    p.SetVector2("uvScale", uv.Scale)
                    p.SetBool("bDoubleSided", mb.TwoSided)
                    p.SetBool("bGPUSkinning", ssbo_BoneMatrices > 0 AndAlso Config_App.Current.Setting_GPUSkinning)
                    p.SetInt("uBoneCount", boneCount)
                    p.SetInt("uBaseOpacity", opacity)
                    pm.EnsureFrameShadowUniforms(p)
                    p.Use()
                    target.WithAlphaBound(2, Sub() GL.DrawElements(PrimitiveType.Triangles, indexCount, DrawElementsType.UnsignedInt, 0))
                    GL.Disable(EnableCap.Blend)
                End If
                shader.Use()
                Dim coverage = DecalCoverageMode(material, gbuf, False)
                If coverage <> 2 AndAlso Not PreviewModel.GateDecalBaseNoCoverage Then
                    target.CoverageDrawStencil()
                    If coverage = 1 Then
                        GL.Enable(EnableCap.Blend)
                        GL.BlendFuncSeparate(0, BlendingFactorSrc.One, BlendingFactorDest.OneMinusSrcAlpha, BlendingFactorSrc.One, BlendingFactorDest.OneMinusSrcAlpha)
                    Else
                        ' Unblended: coverage 1 whatever the fragment's alpha - logic op SET, all ones in the fixed-point target (GL 4.6
                        ' 17.3.9; blending does not apply while the logic op is enabled).
                        GL.Disable(EnableCap.Blend)
                        GL.Enable(EnableCap.ColorLogicOp)
                        GL.LogicOp(LogicOp.Set)
                    End If
                    GL.DrawElements(PrimitiveType.Triangles, indexCount, DrawElementsType.UnsignedInt, 0)
                    GL.Disable(EnableCap.ColorLogicOp)
                    GL.LogicOp(LogicOp.Copy)
                    GL.Disable(EnableCap.Blend)
                End If
            End If
        End Sub

        ''' <summary>The program of a translucent decal's base draw: its colour program (<paramref name="colour"/>), or for a G-buffer
        ''' record Fo4DeferredPrograms.DecalBaseProgram (the record without its early depth tests, when it has them), put in
        ''' <paramref name="gbuf"/> so that ApplyMaterial sets that program's constants. Nothing when it does not exist.</summary>
        Private Function DecalBaseProgram(colour As Shader_Base_Class, ByRef gbuf As Fo4GBufferDraw?) As Shader_Base_Class
            If Not gbuf.HasValue Then Return colour
            Dim d = gbuf.Value
            d.Program = Me.ParentModel.ParentControl.SharedFo4Deferred.DecalBaseProgram(Fo4GBufferTechnique.PixelShaderId(d.Technique))
            gbuf = d
            Return d.Program
        End Function

        ''' <summary>The law of <see cref="EngineSkipsEffectDraw"/>, pure (gate `effect-draw-skip`).</summary>
        Friend Shared Function EffectDrawSkipped(isSse As Boolean, soft As Boolean, blendBit As Boolean,
                                                 src As NiflySharp.Enums.AlphaFunction, dst As NiflySharp.Enums.AlphaFunction) As Boolean
            If isSse Then Return soft AndAlso src = NiflySharp.Enums.AlphaFunction.DEST_COLOR AndAlso dst = NiflySharp.Enums.AlphaFunction.INV_SRC_ALPHA
            Return Fo4EffectTechnique(src, dst).Skip
        End Function

        ''' <param name="ssePrepass">Skyrim SE's z-prepass draw of this shape (RenderAll): depth only, its own alpha test;
        ''' the shape's colour-pass existence does not matter there (slot 0x2B is a separate pass).</param>
        ''' <summary>A lighting shape's G-buffer draw (propuesta v3 E4): the record's program, technique, group and flags. Gap (not
        ''' empty): the engine draws this technique but the preview cannot (Fo4GBufferSource.Records(...).Gap, or the program's build
        ''' error). MissingPrograms (not Nothing): the engine itself does not draw it (its PS or VS is not in the cache). Program is
        ''' Nothing in both, and the frame's notice lists the shape (PreviewModel.ReportUndrawnShapes).</summary>
        Friend Structure Fo4GBufferDraw
            Public Program As Shader_Base_Class
            Public Gap As String
            ''' <summary>Fo4GBufferTechnique.MissingEnginePrograms of the technique; Nothing when both programs are in the cache.</summary>
            Public MissingPrograms As String
            ''' <summary>The engine's material of this draw (Fo4EngineMaterial): every G-buffer constant reads it.</summary>
            Public Material As Fo4EngineLightingMaterial
            Public Technique As UInteger
            Public Flags As ULong
            Public Group As Integer
        End Structure

        ''' <summary>The program a draw of <paramref name="md"/> uses: the frame's game shader, or in Fallout 4's G-buffer stage a
        ''' lighting shape's record program (<paramref name="gbuf"/> set). Nothing = nothing is drawn: a lighting shape without a
        ''' G-buffer pass, a technique the engine has no programs for (MissingPrograms), or a preview gap (Gap) - the frame's notice
        ''' lists the last two (PreviewModel.ReportUndrawnShapes, before the frame draws). The prepass keeps the game shader (its
        ''' depth-only branch).</summary>
        Private Function ColourDrawProgram(md As MaterialData, prepass As Boolean, ByRef gbuf As Fo4GBufferDraw?) As Shader_Base_Class
            gbuf = Nothing
            Dim pm = Me.ParentModel
            If prepass OrElse Not pm.FrameGBufferStage OrElse md?.MaterialBase Is Nothing OrElse md.MaterialBase.IsBGEM() Then Return pm.ParentControl.CurrentShader
            gbuf = md.Fo4GBufferPass(pm.ParentControl.SharedFo4Deferred)
            If Not gbuf.HasValue Then Return Nothing
            If gbuf.Value.Gap <> "" OrElse gbuf.Value.MissingPrograms IsNot Nothing Then
                gbuf = Nothing
                Return Nothing
            End If
            Return gbuf.Value.Program
        End Function

        ''' <param name="decalBase">The translucent decals' base pass (PreviewModel.DrawOpaqueStage, both games): this decal's base depth
        ''' draw (DecalBaseStep.Depth, ApplyDecalBaseState) or its base surface draw (DecalBaseStep.Surface, DrawDecalBaseSurface) instead of its
        ''' colour draw.</param>
        Public Sub Render(projection As Matrix4, ByRef camera As OrbitCamera, Optional ssePrepass As Boolean = False, Optional decalBase As DecalBaseStep = DecalBaseStep.None)

            If Not IsDrawable() Then Exit Sub
            If IsNothing(Me.MeshData.Shape.NifShape) Then Exit Sub
            If Not ssePrepass AndAlso EngineSkipsEffectDraw(MeshData.Material) Then Exit Sub
            '=============================== MATRICES ===============================
            Dim model As Matrix4 = MeshData.Transform
            Dim view As Matrix4 = camera.GetViewMatrix()
            Dim modelView As Matrix4 = view * model

            Dim normalMatrix As New OpenTK.Mathematics.Matrix3(modelView)
            normalMatrix.Invert()
            normalMatrix.Transpose()

            Dim modelViewInverse As Matrix4 = modelView.Inverted()


            '=============================== SHADER ===============================
            Dim gbuf As Fo4GBufferDraw? = Nothing
            Dim shader = ColourDrawProgram(MeshData.Material, ssePrepass, gbuf)
            If shader IsNot Nothing AndAlso decalBase <> DecalBaseStep.None Then shader = DecalBaseProgram(shader, gbuf)
            If shader Is Nothing Then Exit Sub
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
            ApplyMaterial(MeshData.Material, gbuf)

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
            Dim faceMode = ResolveDrawFaceMode(MeshData.Material)
            Dim writeDepth As Boolean = ResolveColourDepthState(MeshData.Material).Write

            If ssePrepass Then
                ApplySsePrepassState(shader, MeshData.Material)
                ApplyFaceMode(faceMode)
                GL.DrawElements(PrimitiveType.Triangles, indexCount, DrawElementsType.UnsignedInt, 0)
                UnbindMaterialSamplers()
                shader.SetBool(If(Me.ParentModel.FrameIsSse, "bSsePrepass", "bFo4Prepass"), False)
                If ssbo_BoneMatrices > 0 Then GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 0, 0)
                GL.DepthMask(True)
                GL.DepthFunc(DepthFunction.Lequal)
                GL.Disable(EnableCap.PolygonOffsetFill)
                GL.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Fill)
                GL.CullFace(TriangleFace.Back)
                Exit Sub
            End If

            Dim isTwoPassBlended As Boolean = False
            If MeshData.Material.HasAlphaBlend AndAlso Not MeshData.Shape.Wireframe AndAlso faceMode = EffectiveFaceMode.DrawBoth Then
                isTwoPassBlended = True
            End If

            If decalBase = DecalBaseStep.Depth Then
                ApplyDecalBaseState(shader, MeshData.Material, gbuf)
                ApplyFaceMode(faceMode)
                GL.DrawElements(PrimitiveType.Triangles, indexCount, DrawElementsType.UnsignedInt, 0)
                EndDecalBaseDraw(shader, gbuf)
            ElseIf decalBase = DecalBaseStep.Surface Then
                ApplyFaceMode(faceMode)
                DrawDecalBaseSurface(shader, MeshData.Material, gbuf, projection, view, model, modelView, modelViewInverse, normalMatrix, boneCount)
                EndDecalBaseDraw(shader, gbuf)
            ElseIf isTwoPassBlended Then
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

            ' SSE billboard list: every effect pass leaves mode 4 for the next (RestoreGeometry 0x141557FF1..8019).
            If Me.ParentModel.FrameIsSse AndAlso mat IsNot Nothing AndAlso MeshData.Material.SsePass().List = SseRenderPassLaw.SseList.Billboard Then
                Me.ParentModel.FrameBillboardEffectDrawn = True
            End If

            UnbindMaterialSamplers()

            ' (Opcional) restaurar estado si luego renderizas más cosas:
            GL.DepthMask(True)
            GL.DepthFunc(DepthFunction.Lequal)
            GL.Disable(EnableCap.Blend)
            GL.Disable(EnableCap.PolygonOffsetFill)
            GL.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Fill)
            GL.CullFace(TriangleFace.Back)

        End Sub

        ''' <summary>The refraction-normals draw of this shape (RefractionLaw / RefractionSource), into the target the control
        ''' bound: its game's VS (skinning and position as the colour pass), the normals PS, depth LESS_EQUAL against the
        ''' frame (written only for FO4's Refraction_Writes_Depth), no blend. <paramref name="lodScale"/> = s of the LOD fade.
        ''' Returns False when the shape is not drawn (no refraction, missing technique, or faded out).</summary>
        Friend Function RenderRefractionNormals(program As Shader_Base_Class, projection As Matrix4, camera As OrbitCamera,
                                                isSse As Boolean, lodScale As Single) As Boolean
            If Not IsDrawable() OrElse IsNothing(MeshData.Shape.NifShape) Then Return False
            If MeshData.Material?.MaterialBase Is Nothing Then Return False
            Dim r = MeshData.Material.Refraction(isSse)
            If Not r.Applies OrElse Not r.Drawn Then Return False
            Dim fade = 1.0F
            Dim view As Matrix4 = camera.GetViewMatrix()
            If r.UsesLodFade Then
                ' x = |camera - world bound centre| * s / 7200 (0x1414E7120 / 0x14217EC00); the view-space length is that distance.
                Dim c = Vector3.TransformPosition(Vector3.TransformPosition(WorldBoundCentre(), MeshData.Transform), view)
                fade = RefractionLaw.LodFade(c.Length, lodScale)
                If fade < 0.0F Then Return False
            End If
            Dim model As Matrix4 = MeshData.Transform
            Dim modelView As Matrix4 = view * model
            Dim normalMatrix As New OpenTK.Mathematics.Matrix3(modelView)
            normalMatrix.Invert() : normalMatrix.Transpose()
            program.Use()
            program.SetMatrix4("matProjection", projection)
            program.SetMatrix4("matView", view)
            program.SetMatrix4("matModel", model)
            program.SetMatrix4("matModelView", modelView)
            program.SetMatrix3("mv_normalMatrix", normalMatrix)
            Dim mb = MeshData.Material.MaterialBase
            program.SetBool("bModelSpace", r.ModelSpace)
            program.SetVector3("refractMsnNormal", If(isSse, New Vector3(1, 1, 1), New Vector3(0, 0, 1)))
            program.SetBool("bRefractSkinned", r.Skinned)
            program.SetBool("bRefractClamp", r.ClampVariant)
            program.SetBool("bRefractClampPs", r.ClampVariant)
            program.SetVector2("uRefractNearFar", Me.ParentModel.ParentControl.FrameNearFar)
            program.SetBool("bRefractFalloff", r.Falloff)
            program.SetBool("bRefractVertexAlpha", r.VertexAlpha)
            program.SetBool("bShowVertexAlpha", r.VertexAlpha)
            program.SetBool("bShowVertexColor", False)
            program.SetBool("bShowMask", False)
            program.SetBool("bShowWeight", False)
            program.SetBool("bWireframe", False)
            program.SetBool("bVertexFalloff", False)
            program.SetFloat("refractStrength", r.Strength)
            program.SetFloat("refractFade", fade)
            program.SetBool("bApplyZap", MeshData.Shape.ApplyZaps)
            Dim uv = MeshData.Material.RestUv()
            program.SetVector2("uvOffset", uv.Offset)
            program.SetVector2("uvScale", uv.Scale)
            ' The colour pass's diffuse (EngineDefaultTextureLaw); 0 (an effect base without TEXTURE) stays 0 as before.
            program.BindTexture("texDiffuse", CInt(MeshData.Material.EngineSlotTextureId(EngineDefaultTextureLaw.MaterialSlot.Diffuse, MeshData.Material.DiffuseTexture_ID)), TextureUnit.Texture0)
            BindMaterialSamplers(MeshData.Material, New List(Of SamplerLaw.SlotSampler) From {SamplerLaw.UtilityDiffuse(MeshData.Material.SamplerInputs(isSse))})
            program.SetBool("bGPUSkinning", ssbo_BoneMatrices > 0 AndAlso Config_App.Current.Setting_GPUSkinning)
            program.SetInt("uBoneCount", If(MeshData.Meshgeometry.GPUBoneMatrices IsNot Nothing, MeshData.Meshgeometry.GPUBoneMatrices.Length, 0))
            If ssbo_BoneMatrices > 0 Then GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 0, ssbo_BoneMatrices)
            GL.BindVertexArray(vao)
            EnsureZapIndexBuffer()
            GL.Disable(EnableCap.Blend)
            GL.Enable(EnableCap.DepthTest)
            GL.DepthFunc(DepthFunction.Lequal)
            GL.DepthMask(r.DepthWrite)
            GL.Disable(EnableCap.PolygonOffsetFill)
            ApplyFaceMode(ResolveDrawFaceMode(MeshData.Material))
            GL.DrawElements(PrimitiveType.Triangles, indexCount, DrawElementsType.UnsignedInt, 0)
            If ssbo_BoneMatrices > 0 Then GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 0, 0)
            UnbindMaterialSamplers()
            GL.DepthMask(True)
            GL.CullFace(TriangleFace.Back)
            Return True
        End Function

        ''' <summary>Dibuja esta malla en el shadow map. Espejo REDUCIDO de <see cref="Render"/>: el MISMO
        ''' VAO, el MISMO SSBO de huesos y el MISMO vertex shader — lo unico que cambia son las matrices
        ''' (las de la luz en vez de las de la camara) y el fragment, que solo hace el alpha-test.
        '''
        ''' <para>SOLO LLEGAN FORMAS DE ILUMINACION OPACAS: el filtro de casters (MaterialData.EngineShadowMapCasts) deja
        ''' afuera los efectos, el blend, el alfa &lt; 1 y la refraccion, como los dos motores (Fo4RenderPassLaw /
        ''' SseRenderPassLaw.ShadowMapCasts). Por eso este pase no tiene rama de efecto ni camino translucido.</para>
        '''
        ''' <para>QUE UNIFORMS HAY QUE SUBIR Y POR QUE ESOS. El unico dato del vertex shader que el
        ''' fragment de profundidad consume es <c>vColor.a</c>, y en el VS ese canal depende de UNA sola
        ''' cosa: <c>if (bShowVertexAlpha) vColor.a = vertexAlpha;</c>. Ni <c>color</c> ni <c>subColor</c>
        ''' lo tocan (el primero multiplica por un vec4 con w=1, el segundo es <c>.rgb</c>), asi que no
        ''' hace falta subirlos. Lo demas que se sube es lo que decide la POSICION (matrices + skinning) y
        ''' el recorte (zap + alpha-test).</para></summary>
        Friend Sub RenderDepthOnly(shadowShader As Shader_Base_Class, lightView As Matrix4)
            ' Sin esto la helper deja de VERSE pero sigue proyectando sombra.
            If Not IsDrawable() Then Exit Sub
            If IsNothing(Me.MeshData.Shape.NifShape) Then Exit Sub

            Dim model As Matrix4 = MeshData.Transform
            ' MISMO orden que el pase iluminado (`view * model`). Cambiarlo aca y no alla dejaria la
            ' sombra proyectada desde otro lado que la luz que la ilumina.
            Dim modelView As Matrix4 = lightView * model

            ' matProjection / matView los sube el CALLER una sola vez por pase (son de la luz, no de la
            ' malla). Aca solo va lo que cambia POR MALLA.
            shadowShader.SetMatrix4("matModel", model)
            shadowShader.SetMatrix4("matModelView", modelView)

            Dim materialBase = MeshData.Material.MaterialBase
            shadowShader.SetBool("bModelSpace", materialBase IsNot Nothing AndAlso materialBase.ModelSpaceNormals)

            Dim shape = MeshData.Shape
            shadowShader.SetBool("bApplyZap", shape.ApplyZaps)
            ' bShowMask / bShowWeight / bWireframe / bShowVertexColor NO se suben: no afectan ni
            ' gl_Position ni vColor.a, que es todo lo que este pase mira.

            ' EL RECORTE: un umbral duro (CUTOUT), el del escritor del shadow map de cada juego. NO HAY camino translucido:
            ' el blend y el alfa < 1 no proyectan en ningun motor (FO4 0x14217B95C..98A, SSE 0x14151B3C3..405) y el filtro
            ' de casters los deja afuera (MaterialData.EngineShadowMapCasts).
            Dim doAlphaTest As Boolean = MeshData.Material.HasAlphaTest
            Dim umbralSombra As Single = 0.5F   ' a shape without a material; both games' branches set it below
            ' THE SHADOW-MAP WRITER'S TEST AND THRESHOLD, each game's own (SHADOW SYNC CONTRACT). FO4:
            ' Fo4RenderPassLaw.ShadowPassAlphaTest - the NiAlphaProperty test bit against ShadowMapAlphaThreshold (BSUtilityShader
            ' 0x142241F41..F64). SSE: SseRenderPassLaw.ShadowAlphaTest (chunk C11) - the NiAlphaProperty test (utility technique
            ' bit 7, 0x14151B641..65A) against the batch's cb11[0].x (SubgroupThreshold, the exe's float32 law).
            If Config_App.Current.Game = Config_App.Game_Enum.Fallout4 AndAlso materialBase IsNot Nothing Then
                Dim atS = Fo4RenderPassLaw.ShadowPassAlphaTest(doAlphaTest, materialBase.ResolveEngineAlpha().Threshold)
                doAlphaTest = atS.Test
                umbralSombra = atS.Threshold
            ElseIf Config_App.Current.Game = Config_App.Game_Enum.Skyrim AndAlso materialBase IsNot Nothing Then
                Dim atSse = SseRenderPassLaw.ShadowAlphaTest(MeshData.Material.SseInputs())
                doAlphaTest = atSse.Test
                umbralSombra = atSse.Threshold
            End If
            Dim usaTextura As Boolean = doAlphaTest
            shadowShader.SetBool("bAlphaTest", doAlphaTest)
            ' `bShowTexture` ES EL DEL SHAPE, EL MISMO QUE MANDA EL PASE ILUMINADO — NO `usaTextura`: el
            ' uniform tendria DOS significados distintos en dos programas. Y `doAlphaTest` NO lleva
            ' `AndAlso shape.ShowTexture`: con la textura apagada este pase dejaria de descartar mientras el
            ' iluminado sigue haciendolo (su `bAlphaTest` nunca estuvo gateado por la textura).
            shadowShader.SetBool("bShowTexture", shape.ShowTexture)
            ' El fragment de profundidad es UNO para los dos juegos y la cantidad que testea difiere: SSE mete el
            ' escalar Alpha del material DENTRO de la cantidad y FO4 no (chunk C11, DECISIONES 1). El juego lo decide
            ' ACA, que es el unico lugar que lo sabe.
            shadowShader.SetBool("bLeySse", Config_App.Current.Game = Config_App.Game_Enum.Skyrim)
            ' EL GATE DEL ALPHA DE VERTICE ES `UseVertexAlpha` PARA LAS DOS FAMILIAS Y LOS DOS JUEGOS — el
            ' MISMO que manda el pase iluminado a su vertex shader. La ley tiene DOS pisos:
            '   · el VS del pase iluminado gatea SIEMPRE con UseVertexAlpha (es el unico que escribe vColor.a);
            '   · encima, el fragment de FO4 —solo el de FO4— vuelve a gatear con UseVertexColor.
            ' El predicado neto de FO4 es `UseVertexAlpha AND UseVertexColor`, y como `UseVertexAlpha` YA
            ' IMPLICA `UseVertexColor` (ver la propiedad), se reduce a UseVertexAlpha. SSE no tiene el segundo
            ' piso: usa `vColor.a` crudo, gateado solo por el VS.
            ' Mandar `UseVertexColor` acá —transplantando el gate del fragment de FO4— rompe Tree/TreeAnim
            ' (UseVertexColor True con UseVertexAlpha False): el iluminado deja vColor.a en 1.0 y la sombra usa
            ' el alpha real del vertice.
            ' FO4 lighting: the lit test multiplies the vertex alpha only under the G-buffer's gate (Fo4RenderPassLaw) - the
            ' same gate here, so the cast silhouette is the drawn one. Hole, declared (H-S3): the FO4 SHADOWMAP PS multiplies
            ' the vertex alpha by its own variant (b07 t00004083 / t04004083), not transcribed.
            Dim fo4LitGate = Config_App.Current.Game <> Config_App.Game_Enum.Fallout4 OrElse MeshData.Material.Fo4Pass().GBufferTestVertexAlpha
            shadowShader.SetBool("bShowVertexAlpha", usaTextura AndAlso MeshData.Material.UseVertexAlpha AndAlso fo4LitGate)
            If usaTextura Then
                shadowShader.SetFloat("alphaThreshold", umbralSombra)
                ' El escalar Alpha del material: el cutout de SSE lo mete DENTRO de la cantidad (chunk C11, DECISIONES 1); FO4 no lo lee.
                shadowShader.SetFloat("uMaterialAlpha", If(materialBase Is Nothing, 1.0F, MeshData.Material.PreviewAlpha()))
                If materialBase IsNot Nothing Then
                    ' The shadow's UV is the colour pass's (the alpha test samples the same texel): RestUv, C10.
                    Dim uv = MeshData.Material.RestUv()
                    shadowShader.SetVector2("uvOffset", uv.Offset)
                    shadowShader.SetVector2("uvScale", uv.Scale)
                Else
                    shadowShader.SetVector2("uvOffset", Vector2.Zero)
                    shadowShader.SetVector2("uvScale", Vector2.One)
                End If
                ' - EL MISMO FALLBACK que el pase iluminado ya
                ' tiene: sin diffuse va la BLANCA, no el ID 0. Hoy coinciden por casualidad (un sampler2D
                ' sobre la textura 0, que esta incompleta, devuelve (0,0,0,1), o sea .a = 1 igual que la
                ' blanca), pero eso es un default del driver, no una ley — y el pase iluminado no se apoya
                ' en el. Dos pases que llegan al mismo alpha por mecanismos distintos es justo lo que el
                ' contrato de sincronia prohibe.
                ' The colour pass's diffuse (EngineDefaultTextureLaw): its alpha is what the alpha test of this pass reads.
                Dim idDifuso = MeshData.Material.EngineSlotTextureId(EngineDefaultTextureLaw.MaterialSlot.Diffuse, MeshData.Material.DiffuseTexture_ID)
                If idDifuso = 0 Then idDifuso = CUInt(Me.ParentModel.ParentControl.defaultWhiteTex)
                shadowShader.BindTexture("texDiffuse", idDifuso, TextureUnit.Texture0)
            End If

            shadowShader.SetBool("bGPUSkinning", ssbo_BoneMatrices > 0 AndAlso Config_App.Current.Setting_GPUSkinning)
            Dim boneCount As Integer = If(MeshData.Meshgeometry.GPUBoneMatrices IsNot Nothing, MeshData.Meshgeometry.GPUBoneMatrices.Length, 0)
            shadowShader.SetInt("uBoneCount", boneCount)
            If ssbo_BoneMatrices > 0 Then
                GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 0, ssbo_BoneMatrices)
            End If

            ' The shadow maps are drawn by the utility shader: t0 the material's clamp / aniso (SSE 0x141567010, FO4
            ' 0x142240F40). Effects never reach this pass (MaterialData.EngineShadowMapCasts).
            If materialBase IsNot Nothing Then
                Dim si = MeshData.Material.SamplerInputs(Config_App.Current.Game = Config_App.Game_Enum.Skyrim)
                BindMaterialSamplers(MeshData.Material, New List(Of SamplerLaw.SlotSampler) From {SamplerLaw.UtilityDiffuse(si)})
            End If
            GL.BindVertexArray(vao)
            ' Mismo filtro de indices que el pase iluminado: un triangulo zapeado no puede castear.
            EnsureZapIndexBuffer()
            GL.DrawElements(PrimitiveType.Triangles, indexCount, DrawElementsType.UnsignedInt, 0)
            UnbindMaterialSamplers()

            ' Idem Render(): desbindear el binding 0 para no contaminar a una malla sin SSBO propio.
            If ssbo_BoneMatrices > 0 Then
                GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 0, 0)
            End If
        End Sub

        ' Per-layer transient MaterialData cache for the overlay render path. Built once per layer
        ' (keyed on the layer instance) so RenderOverlayLayer does not allocate a MaterialData every
        ' frame. Each entry has OverrideRelatedMaterial = layer.Material so MaterialBase + all the
        ' *_ID/Has* props flow from the overlay material; ParentMeshData stays this mesh's MeshData so
        ' Shape-derived state (TintColor/ShowTexture/...) still resolves to the BASE shape, matching
        ' how ApplyMaterial reads MeshData.Shape directly (~2905-2925).
        Private _overlayMaterialCache As Dictionary(Of OverlayMaterialLayer, MaterialData)

        ''' <summary>The transient MaterialData of an overlay layer (shared with RenderOverlayLayer).</summary>
        Friend Function OverlayMaterialData(layer As OverlayMaterialLayer) As MaterialData
            Return GetOverlayMaterialData(layer)
        End Function

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

        ''' <summary>Dibuja UNA capa de material de overlay sobre la geometria YA deformada (morph + skin) de
        ''' esta malla, como decal coplanar: es el modelo de overlays/tatuajes de LooksMenu. REUSA el VAO/SSBO/
        ''' EBO/indexCount existentes (sin re-skin ni re-morph): mismos vertices, mismo skinning, solo cambia el
        ''' material bindeado.
        ''' <para>Estado GL del decal coplanar: depth-test Lequal para que el fragmento coplanar pase contra la
        ''' profundidad de la base, DepthMask(False) para que el overlay NUNCA escriba depth, y el blend que
        ''' configure ApplyMaterial. El culling usa el mismo modo efectivo que el draw base. Todo se restaura al
        ''' final igual que en <see cref="Render"/>.</para></summary>
        Public Sub RenderOverlayLayer(projection As Matrix4, ByRef camera As OrbitCamera, layer As OverlayMaterialLayer, Optional ssePrepass As Boolean = False,
                                      Optional decalBase As DecalBaseStep = DecalBaseStep.None)
            If layer Is Nothing OrElse layer.Material Is Nothing Then Exit Sub
            If Not IsDrawable() Then Exit Sub
            If IsNothing(Me.MeshData.Shape.NifShape) Then Exit Sub
            If Not ssePrepass AndAlso EngineSkipsEffectDraw(OverlayMaterialData(layer)) Then Exit Sub

            '=============================== MATRICES (identical to Render) ===============================
            Dim model As Matrix4 = MeshData.Transform
            Dim view As Matrix4 = camera.GetViewMatrix()
            Dim modelView As Matrix4 = view * model

            Dim normalMatrix As New OpenTK.Mathematics.Matrix3(modelView)
            normalMatrix.Invert()
            normalMatrix.Transpose()

            Dim modelViewInverse As Matrix4 = modelView.Inverted()

            '=============================== SHADER ===============================
            ' Bind the LAYER's material (transient MaterialData with OverrideRelatedMaterial).
            Dim overlayMat = GetOverlayMaterialData(layer)
            Dim gbuf As Fo4GBufferDraw? = Nothing
            Dim shader = ColourDrawProgram(overlayMat, ssePrepass, gbuf)
            If shader IsNot Nothing AndAlso decalBase <> DecalBaseStep.None Then shader = DecalBaseProgram(shader, gbuf)
            If shader Is Nothing Then Exit Sub
            shader.Use()
            shader.SetMatrix4("matProjection", projection)
            shader.SetMatrix4("matView", view)
            shader.SetMatrix4("matModel", model)
            shader.SetMatrix4("matModelView", modelView)
            shader.SetMatrix4("matModelViewInverse", modelViewInverse)
            shader.SetMatrix3("mv_normalMatrix", normalMatrix)

            Dim materialBase = overlayMat.MaterialBase
            shader.SetBool("bModelSpace", materialBase IsNot Nothing AndAlso materialBase.ModelSpaceNormals)
            ApplyMaterial(overlayMat, gbuf)

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
            Dim faceMode = ResolveDrawFaceMode(overlayMat)

            ' Depth/blend/offset come from ApplyMaterial exactly as for any shape: the overlay is a shape of its
            ' own in the engine (an F4EE / skee clone), routed by its material (decal or alpha list). Its
            ' geometry and vertex path are the base's, so the coplanar depth is identical and LESS_EQUAL passes.
            If ssePrepass Then ApplySsePrepassState(shader, overlayMat)
            If decalBase = DecalBaseStep.Depth Then ApplyDecalBaseState(shader, overlayMat, gbuf)
            ApplyFaceMode(faceMode)
            If decalBase = DecalBaseStep.Surface Then
                DrawDecalBaseSurface(shader, overlayMat, gbuf, projection, view, model, modelView, modelViewInverse, normalMatrix, boneCount)
            Else
                GL.DrawElements(PrimitiveType.Triangles, indexCount, DrawElementsType.UnsignedInt, 0)
            End If
            UnbindMaterialSamplers()
            If ssePrepass Then shader.SetBool(If(Me.ParentModel.FrameIsSse, "bSsePrepass", "bFo4Prepass"), False)
            If decalBase <> DecalBaseStep.None Then EndDecalBaseDraw(shader, gbuf)
            If Not ssePrepass AndAlso Me.ParentModel.FrameIsSse AndAlso overlayMat.SsePass().List = SseRenderPassLaw.SseList.Billboard Then
                Me.ParentModel.FrameBillboardEffectDrawn = True
            End If

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
            GL.DepthFunc(DepthFunction.Lequal)
            GL.Disable(EnableCap.Blend)
            GL.Disable(EnableCap.PolygonOffsetFill)
            GL.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Fill)
            GL.CullFace(TriangleFace.Back)
        End Sub

        ''' <summary>What a blended draw adds to the coverage of the HDR target, from its SOURCE blend factor
        ''' evaluated over an EMPTY destination (colour 0; alpha 1, the R11G11B10 target has no alpha): a
        ''' factor that is 0 there (ZERO, DST_COLOR, ONE_MINUS_DST_ALPHA, SRC_ALPHA_SATURATE) means the draw
        ''' adds no light of its own - it only scales what is behind - so it leaves the coverage (2); any
        ''' other factor lets its own colour in, weighted by its alpha (1).</summary>

        ''' <summary>THE FO4 EFFECT SHADER'S BLEND, from the alpha property's src/dst (the .bgem's copied into it,
        ''' 0x142171954..19CC; a NIF-inline shape keeps its own). Tools/re-docs/RE_BGEM_BLENDMODES_FO4_2026-10-03.md.
        ''' <list type="bullet">
        ''' <item>Pass mode (0x1422301E0), only these pairs (src, dst) with the blend bit set; any other pair draws with
        ''' blending OFF: (6,7) -&gt; 1; (6,0) (0,0) (6,9) -&gt; 2; (1,2) (4,1) -&gt; 3; (0,7) -&gt; 4. Without the blend bit:
        ''' mode 1 when the property alpha is below 1, else off (the app's hasAlphaBlend already says which).</item>
        ''' <item>Modes (table 0x141855180, op ADD): 1 SRC_ALPHA/INV_SRC_ALPHA; 2 SRC_ALPHA/ONE; 3 colour DEST_COLOR/ZERO,
        ''' alpha ONE/ZERO; 4 ONE/INV_SRC_ALPHA.</item>
        ''' <item>Technique (0x142178150): MULTBLEND_DECAL = (4,7), MULTBLEND = dst 2 or src 4 otherwise, ADDBLEND = dst 0.
        ''' No PS exists for MULTBLEND_DECAL nor for ADD+MULT (4,0): the lookup returns 0 (0x142231AE0) and the draw is
        ''' skipped (0x14221FB9F).</item>
        ''' </list>
        ''' PREMULTIPLY (mode 4 forced) is never set in world rendering (0x143E5E345, Interface3D only): not applied.
        ''' World effects write RGB only (0x1421D5C22): the colour mask in ApplyMaterial. ENVCUBE (mode 2) does not reach a
        ''' NIF effect (Tools/re-docs/RE_EFFECT_PARTICLE_ENVCUBE_2026-10-03.md).</summary>
        ''' <summary>SSE invFrameBufferRange in world rendering: BSShaderManager::State+0x9C, initial .data value at
        ''' 0x1420D694C = 1/1.2; its only writers are the UI render 0x14116AC80 (1.0 while it draws, then restored) and the
        ''' console command SetFramebufferRange (Tools/re-docs/RE_EFFECT_PARTICLE_ENVCUBE_2026-10-03.md Q3).</summary>
        Friend Const SseInvFrameBufferRangeWorld As Single = 1.0F / 1.2F
        ''' <summary>The SSE lighting PS output clamp C: fLightingOutputColourClampPostLit / PostSpec:General, copied by the
        ''' BSLightingShader ctor 0x141547210 (0x141547454..47C) and written to cb0[1].x / .z by SetupTechnique 0x141547D20
        ''' (0x141548619..656); the Setting objects 0x1420D8CF8 / 0x1420D8D28 hold 1.0 in .data, the installed
        ''' SkyrimPrefs.ini too (Tools/re-docs/RE_SSE_LIGHTING_PREPASS_FOGTAIL_2026-10-03.md 2.2).</summary>
        Friend Const SseLightingOutputClampPostLit As Single = 1.0F
        Friend Const SseLightingOutputClampPostSpec As Single = 1.0F

        ''' <summary>uSseLitSpace of Fragment_SSE: an app-WORLD direction -> the space the SSE lighting PS works in.
        ''' SetupGeometry 0x141549550 reads technique bit 1 SKINNED (0x1415495A4..5C1; the bit is SLSF1 1, descriptor
        ''' 0x14151A976..A985): skinned, the directional light stays in world space (D3DXVec3Normalize only, 0x141549B9E..BA1);
        ''' not skinned, it goes through D3DXVec3TransformNormal by the inverse of the geometry's world transform
        ''' (0x141549B83..B8E; the same matrix takes the camera to model space for cb2[6], 0x14154A646..657) - model space.
        ''' The app's geometry world is the ONE matrix SkinningHelper places the shape with, read here and not derived again:
        ''' <paramref name="placement"/> = Meshgeometry.GPUBoneMatrices, a palette of one: the unskinned shape's own world
        ''' (ExtractSkinnedGeometry Case Else branch (A), RecomputeGPUBoneMatrices branch (3)), bone 0 of a skinned shape in
        ''' single-bone mode (ExtractSkinnedGeometry Case single-bone, RecomputeGPUBoneMatrices branch (1)) or the identity of a
        ''' skinned shape without a resolvable palette (branches (B) / (2)) - in every case the matrix its vertices are drawn
        ''' with. A palette of several (a skinned shape whose SLSF1 1 is off: 0 of the 40.013 BSTriShape lighting shapes with
        ''' vertex skin data and 0 of the 265 NiTriShape with a NiSkinInstance in sse_lit_shapes.pkl,
        ''' D:\WinTmp\scopeverify\re2\c5v3\skin_vs_slsf1.py, nitrishape_skin.py) has no single placement; the engine draws it
        ''' rigid at the geometry's own world, local x parents (SkinningHelper.ShapeGlobalTransform). The shader normalizes
        ''' after it.
        ''' <para>WORLD space too for the technique types 1, 11 and 16 whatever SKINNED says (SseRenderPassLaw.LitPsWorldSpace: their
        ''' SetupGeometry case 0x141549705 clears the model-space flag, xor r15d at 0x14154971E; S-P / S-M, re2 vs_space.py).</para></summary>
        Friend Shared Function SseLitSpace(litSkinned As Boolean, litType As Integer, placement As Matrix4(), shape As IRenderableShape) As Matrix3
            If litSkinned OrElse SseRenderPassLaw.LitPsWorldSpace(litType) Then Return Matrix3.Identity
            If placement IsNot Nothing AndAlso placement.Length = 1 Then Return SseLitSpaceFromPlacement(placement(0))
            ' No NIF block or no NIF to walk its parents in: no geometry world to invert (the guard SseLitSpace had before C5).
            If shape?.Geometry?.BackingShape Is Nothing OrElse shape.NifContent Is Nothing Then Return Matrix3.Identity
            Return SseLitSpaceFromPlacement(SkinningHelper.AMatrix4(SkinningHelper.ShapeGlobalTransform(shape)))
        End Function

        ''' <summary>The model-space branch of <see cref="SseLitSpace"/>: the inverse of the 3x3 of the geometry's world
        ''' transform as the app uploads it (rows = OpenTK rows), by cofactors over the determinant - R^T / s for a NIF
        ''' transform, what 0x140fdeec0 builds (rotation transposed, divss 1.0 / scale at 0x140fdeeeb..EF3).</summary>
        Friend Shared Function SseLitSpaceFromPlacement(w As Matrix4) As Matrix3
            Dim a = CDbl(w.M11), b = CDbl(w.M12), c = CDbl(w.M13), d = CDbl(w.M21), e = CDbl(w.M22), f = CDbl(w.M23)
            Dim g = CDbl(w.M31), h = CDbl(w.M32), i = CDbl(w.M33)
            Dim k = 1.0 / (a * (e * i - f * h) - b * (d * i - f * g) + c * (d * h - e * g))
            Return New Matrix3(CSng((e * i - f * h) * k), CSng((c * h - b * i) * k), CSng((b * f - c * e) * k),
                               CSng((f * g - d * i) * k), CSng((a * i - c * g) * k), CSng((c * d - a * f) * k),
                               CSng((d * h - e * g) * k), CSng((b * g - a * h) * k), CSng((a * e - b * d) * k))
        End Function

        ''' <summary>Depth state of the SSE z-prepass draws: mode 3 = LESS_EQUAL + write, no colour (write mode 0,
        ''' 0x1415390EC). Nothing writes [0x1420CFC88] between the kMAIN clear and RenderBatches[0x2B, 0x4000002B)
        ''' (0x14153902B..911B, 0x1415206A0); the value is the one the shadow-map finish leaves (list-4 function
        ''' 0x14151FF00 ends with 3, 0x141520070..094) - a world frame renders the sun's shadow maps just before
        ''' (0x1415385DE). BSUtilityShader's prepass techniques do not touch it; the hair-decal list 4 sets 3 itself
        ''' (flags &amp; 0x20, 0x14151FF86..FB6). Depth bias 0. Tools/re-docs/RE_SSE_PASS_GROUPS_DEPTH_2026-10-03.md 14.</summary>
        Friend Const SsePrepassDepthFunc As DepthFunction = DepthFunction.Lequal


        ''' <summary>The effect technique law (0x142178150): MULTBLEND_DECAL = (4,7), MULTBLEND = dst 2 or src 4 otherwise, ADDBLEND =
        ''' dst 0; no PS exists for MULTBLEND_DECAL nor for ADD+MULT, so the draw is skipped (0x142231AE0, 0x14221FB9F).</summary>
        Friend Shared Function Fo4EffectTechnique(src As NiflySharp.Enums.AlphaFunction, dst As NiflySharp.Enums.AlphaFunction) As (Skip As Boolean, MultBlend As Boolean)
            Dim s = CInt(src), d = CInt(dst)
            Dim multDecal = s = 4 AndAlso d = 7
            Dim mult = Not multDecal AndAlso (d = 2 OrElse s = 4)
            Dim add = d = 0
            Return (multDecal OrElse (mult AndAlso add), mult)
        End Function

        ''' <summary>An effect's pass: its technique (Fo4EffectTechnique) and its blend (SetupAlphaBlend with the property's real alpha,
        ''' Fo4SetupAlphaBlendMode / Fo4BlendModeFactors - the table the G-buffer shares).</summary>
        Friend Shared Function Fo4EffectBlend(present As Boolean, blendBit As Boolean, src As NiflySharp.Enums.AlphaFunction, dst As NiflySharp.Enums.AlphaFunction,
                                              passAlpha As Single) As (Skip As Boolean, MultBlend As Boolean, Enabled As Boolean, ColorSrc As BlendingFactor, ColorDst As BlendingFactor, AlphaSrc As BlendingFactor, AlphaDst As BlendingFactor)
            Dim tq = Fo4EffectTechnique(src, dst)
            Dim m = Fo4BlendModeFactors(Fo4SetupAlphaBlendMode(present, blendBit, src, dst, passAlpha))
            Return (tq.Skip, tq.MultBlend, m.Enabled, m.ColorSrc, m.ColorDst, m.AlphaSrc, m.AlphaDst)
        End Function

        Private Shared Function CoverageModeForSourceFactor(src As BlendingFactor) As Integer
            Select Case src
                Case BlendingFactor.Zero, BlendingFactor.DstColor, BlendingFactor.OneMinusDstAlpha, BlendingFactor.SrcAlphaSaturate
                    Return 2
                Case Else
                    Return 1
            End Select
        End Function

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

        ''' <summary>The cull of a draw (colour pass and SSE z-prepass alike - they share the rule, 15.5): the shape / material
        ''' face mode, except that Skyrim SE's opaque batch (0x14155FF40) culls BACK for a two-sided shape whose SLSF2
        ''' No_Transparency_Multisampling puts it in subgroup 4 (0x14155FFCE..0172); the decal and translucent list paths
        ''' (0x14155DF50, 0x1415618B0) ignore that flag.</summary>
        Private Function ResolveDrawFaceMode(material As MaterialData) As EffectiveFaceMode
            Dim f = ResolveEffectiveFaceMode(MeshData.Shape, material.MaterialBase)
            If f = EffectiveFaceMode.DrawBoth AndAlso Me.ParentModel.FrameIsSse AndAlso material.MaterialBase IsNot Nothing Then
                Dim inp = material.SseInputs()
                If inp.NoTransparencyMultisampling AndAlso SseRenderPassLaw.Classify(inp).List = SseRenderPassLaw.SseList.OpaqueBatch Then Return EffectiveFaceMode.DrawCCW
            End If
            Return f
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



        ''' <summary>DIAGNOSTICO (sólo bajo <c>Logger.Enabled</c>): un parámetro de sampleo de una textura
        ''' por su NOMBRE, sin bindearla — <c>glGetTextureParameteriv</c> (DSA, GL 4.5). Devuelve -1 si el
        ''' contexto no lo soporta o el id es 0, en vez de tirar: es una sonda, no un camino.</summary>
        Private Shared Function TexParamOrMinus1(texId As Integer, pname As GetTextureParameter) As Integer
            If texId <= 0 Then Return -1
            Try
                Dim v As Integer = -1
                GL.GetTextureParameter(texId, pname, v)
                If GL.GetError() <> ErrorCode.NoError Then Return -1
                Return v
            Catch
                Return -1
            End Try
        End Function

        ''' <param name="gbuf">Fallout 4's G-buffer draw of a lighting shape (ColourDrawProgram): its record's program draws, and
        ''' ApplyFo4GBufferDraw sets its write mode, blend and constants after the shared material state.</param>
        ''' <summary>C10: a material colour as the shader gets it - the controller's rest colour (floats, as the engine stores it), else
        ''' the material's (8-bit) - through the game's colour law (Shader_Base_Class.MaterialColor: FO4 powf 2.2, SSE raw).</summary>
        Private Shared Function RestColour(rest As OpenTK.Mathematics.Vector3?, materialColour As Drawing.Color, isSse As Boolean) As OpenTK.Mathematics.Vector3
            If Not rest.HasValue Then Return Shader_Base_Class.MaterialColor(materialColour, isSse)
            Return If(isSse, rest.Value, Shader_Base_Class.Vector_to_Linear(rest.Value))
        End Function

        ''' <summary>C10: the effect's base colour as the engine stores it (+0x48..0x50, 0..1 floats): the colour controller's rest value,
        ''' else the material's 8-bit colour / 255.</summary>
        Private Shared Function RestBase(material As PreviewModel.RenderableMesh.MaterialData) As OpenTK.Mathematics.Vector3
            Dim c = material.MaterialBase.BaseColor
            Return If(material.RestColourAt(RestVariable.EffectEmissiveColor), New OpenTK.Mathematics.Vector3(c.R / 255.0F, c.G / 255.0F, c.B / 255.0F))
        End Function

        Friend Sub ApplyMaterial(material As PreviewModel.RenderableMesh.MaterialData, Optional gbuf As Fo4GBufferDraw? = Nothing)

            Dim shader = If(gbuf.HasValue, gbuf.Value.Program, Me.ParentModel.ParentControl.CurrentShader)
            Dim materialBase = material.MaterialBase

            Dim diffuseTextureId = material.DiffuseTexture_ID
            Dim normalTextureId = material.NormalTexture_ID
            Dim envmapTextureId = material.EnvmapTexture_ID
            Dim envmapMaskTextureId = material.Texture2DId(material.SlotPath(EngineDefaultTextureLaw.MaterialSlot.EnvMask))
            Dim smoothSpecTextureId = material.SmoothSpecTexture_ID
            Dim greyscaleTextureId = material.GreyscaleTexture_ID
            Dim glowTextureId = material.GlowTexture_ID
            Dim lightingTextureId = material.LightingTexture_ID
            Dim WrinklesTextureId = material.WrinklesTexture_ID

            ' FO4 = engine-faithful path (Fragment_FO4, always on); Skyrim = Fragment_SSE (its own path).
            ' The shader instance is the single source of truth for which game we are rendering.
            Dim isSSE As Boolean = TypeOf shader Is Shader_Class_SSE
            ' Skyrim SE: what the pass and technique laws read (MaterialData.SseInputs), ONCE per draw, and the lighting technique TYPE
            ' (SseRenderPassLaw.LitTechniqueType, the full selector; -1 for an effect and outside Skyrim SE): Eye (16) the radial
            ' reflection (S-O1), Parallax (3) and MultiLayerParallax (11) their PS law (S-P / S-M), types 1 / 11 / 16 the world space
            ' (SseLitSpace).
            Dim sseIn0 As SseRenderPassLaw.SseShapeInputs = If(isSSE, material.SseInputs(), Nothing)
            Dim sseLitType As Integer = If(isSSE, SseRenderPassLaw.LitTechniqueType(sseIn0), -1)

            ' FO4: EACH SLOT IN THE COLOUR SPACE THE ENGINE LOADS IT. The flag is per material class and per
            ' slot, passed to the texture set's loader (BSShaderTextureSet vtable+0x158 = 0x14216A1E0 ->
            ' 0x142182420 -> 0x1417A3A60 -> MakeSRGB 0x14183E680):
            '  - effect (BSEffectShaderMaterial, 0x1422249CE..C52): base, palette, cube sRGB; normal, mask raw.
            '  - lighting (every BSLightingShaderMaterial class; common part 0x1421D3C60): slot 0 diffuse sRGB
            '    (ALSO with greyscale-to-palette: the prepass re-encodes its index, pow(t0.g, 1/2.2) in b09
            '    rec2985), 1 normal raw, 2 glow raw, 3 greyscale palette sRGB, 7 smooth-spec raw; Envmap/Eye/
            '    MultiLayerParallax (0x1421CF8C0 / 0x1421D00A0 / 0x1421D3330) slot 4 cube sRGB; Face (0x1421D15C0)
            '    slot 5 raw. The env mask (slot 5) is raw.
            ' The paths are the ones the *_ID properties resolve, so a slot whose id came from elsewhere (the eye
            ' env-mask swap, a composer) is left alone; SSE stays on its raw pipeline.
            If isSSE Then
                ' SSE: the legacy loader never promotes a texture to sRGB (0x14101FED0 maps to UNORM/SNORM/FLOAT only;
                ' an authored _SRGB DDS keeps its own SRV, 0x14101F616): every slot is read raw, whatever role
                ' registered the path's storage. ColorSpaceView(False) gives exactly that (authored sRGB stays sRGB).
                Dim tpS = Function(p As String) FO4UnifiedMaterial_Class.CorrectTexturePath(p)
                diffuseTextureId = material.SlotTextureInColorSpace(tpS(materialBase.Diffuse_or_Base_Texture), diffuseTextureId, False)
                normalTextureId = material.SlotTextureInColorSpace(tpS(materialBase.NormalTexture), normalTextureId, False)
                greyscaleTextureId = material.SlotTextureInColorSpace(tpS(materialBase.GreyscaleTexture), greyscaleTextureId, False)
                envmapTextureId = material.SlotTextureInColorSpace(material.EnvmapTexturePath, envmapTextureId, False)
                envmapMaskTextureId = material.SlotTextureInColorSpace(material.SlotPath(EngineDefaultTextureLaw.MaterialSlot.EnvMask), envmapMaskTextureId, False)
                glowTextureId = material.SlotTextureInColorSpace(tpS(materialBase.GlowTexture), glowTextureId, False)
                smoothSpecTextureId = material.SlotTextureInColorSpace(tpS(materialBase.SmoothSpecTexture), smoothSpecTextureId, False)
                lightingTextureId = material.SlotTextureInColorSpace(tpS(materialBase.LightingTexture), lightingTextureId, False)
            Else
                Dim tp = Function(p As String) FO4UnifiedMaterial_Class.CorrectTexturePath(p)
                diffuseTextureId = material.SlotTextureInColorSpace(tp(materialBase.Diffuse_or_Base_Texture), diffuseTextureId, True)
                normalTextureId = material.SlotTextureInColorSpace(tp(materialBase.NormalTexture), normalTextureId, False)
                greyscaleTextureId = material.SlotTextureInColorSpace(tp(materialBase.GreyscaleTexture), greyscaleTextureId, True)
                envmapTextureId = material.SlotTextureInColorSpace(material.EnvmapTexturePath, envmapTextureId, True)
                envmapMaskTextureId = material.SlotTextureInColorSpace(material.SlotPath(EngineDefaultTextureLaw.MaterialSlot.EnvMask), envmapMaskTextureId, False)
                If Not materialBase.IsBGEM() Then
                    glowTextureId = material.SlotTextureInColorSpace(tp(materialBase.GlowTexture), glowTextureId, False)
                    smoothSpecTextureId = material.SlotTextureInColorSpace(tp(materialBase.SmoothSpecTexture), smoothSpecTextureId, False)
                End If
            End If

            Dim hasBacklightTexture As Boolean = materialBase.BackLighting

            ' ⛔ ELIMINADAS DOS HEURISTICAS que existian SOLO para tapar la mascara de environment faltante en
            ' SSE (el slot 5 nunca llegaba al shader). Con el slot 5 ya ruteado arriba, las dos son daninas:
            '  1) "eye": movia el SLOT 7 a la envmask y ademas lo ponia en 0. El slot 7 es el mask ESPECULAR
            '     (t2, mallas MSN) o el BACKLIGHT (t9), medido en OnLoadTextureSet y en SetupMaterial: robarlo
            '     rompia esos dos. Y para la tecnica Eye la envmask del motor es el slot 5, que ya se rutea bien.
            '  2) "wrinkles": mandaba el wrinkle map de facegen a la mascara de reflexion, y el propio comentario
            '     admitia que no es una mascara de reflexion. Ademas el BSLightingShader de SSE no tiene sampler
            '     de wrinkles.
            ' EyeEnvironmentMapping es ademas un campo BGSM v<7, o sea FO4.

            ' QUE TEXTURA APORTA EL MASK ESPECULAR: leyes DISTINTAS por juego, medidas a nivel byte.
            '  Â· FO4: el normal es BC5 (sin alpha) y el `_s` es UNIVERSAL. En los 18 b06_BSLighting_PS
            '    dumpeados, t2 (el `_s`) se samplea en 18/18 sin depender de MODELSPACENORMALS, asi que el gate
            '    correcto es "hay _s".
            '  Â· SSE: el gate es MODELSPACENORMALS, NO la presencia del slot 7. Medido sobre la poblacion
            '    COMPLETA de BSLightingShader (6924 PS; 6864 excluyendo terreno/LOD, donde t2 es una capa de
            '    blend del landscape): MSN samplea t2 en 768/768 y no-MSN NUNCA lo samplea (0/6096), tomando el
            '    mask del ALPHA del normal. Las variantes MSN viven en Default, Facegen y FacegenRGBTint, asi
            '    que afecta cabeza, cuerpo y objetos genericos con _msn.
            Dim hasSpecMap As Boolean
            If isSSE Then
                hasSpecMap = materialBase.ModelSpaceNormals
            Else
                hasSpecMap = (smoothSpecTextureId <> 0)
            End If
            ' SSE: SIEMPRE hay fuente de mask especular — el motor la toma del alpha del normal (no-MSN) o del
            ' slot 7 (MSN), y en ambos casos rellena un default si la textura falta. NO condicionarlo a
            ' `hasSpecMap OrElse normalTextureId <> 0`: como `hasSpecMap` depende de MSN, una malla SSE no-MSN
            ' con slot 7 pero SIN normal texture perderia bSpecular por completo, y el motor si le da specular
            ' contra su normal por defecto. FO4 conserva su regla.
            Dim hasSpecularSource As Boolean = If(isSSE, True, hasSpecMap)

            ' ENGINE DEFAULT TEXTURES (EngineDefaultTextureLaw; user decision 7-oct-2026): a slot whose path is empty or names a file that
            ' did not load samples what the game samples there. After the colour-space views (the engine's defaults are UNORM, so are
            ' the app's) and after hasSpecMap. A helper shape keeps its black diffuse (app decision, below).
            If Not (MeshData.Shape IsNot Nothing AndAlso MeshData.Shape.IsHelperShape) Then
                diffuseTextureId = material.EngineSlotTextureId(EngineDefaultTextureLaw.MaterialSlot.Diffuse, diffuseTextureId)
            End If
            normalTextureId = material.EngineSlotTextureId(EngineDefaultTextureLaw.MaterialSlot.Normal, normalTextureId)
            greyscaleTextureId = material.EngineSlotTextureId(EngineDefaultTextureLaw.MaterialSlot.Greyscale, greyscaleTextureId)
            glowTextureId = material.EngineSlotTextureId(EngineDefaultTextureLaw.MaterialSlot.Glow, glowTextureId)
            smoothSpecTextureId = material.EngineSlotTextureId(EngineDefaultTextureLaw.MaterialSlot.Slot7, smoothSpecTextureId)
            lightingTextureId = material.EngineSlotTextureId(EngineDefaultTextureLaw.MaterialSlot.Lightmask, lightingTextureId)
            envmapMaskTextureId = material.EngineSlotTextureId(EngineDefaultTextureLaw.MaterialSlot.EnvMask, envmapMaskTextureId)

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
            ' Los dos predicados viven en MaterialData (UseVertexColor / UseVertexAlpha) porque el pase de
            ' SOMBRA necesita el mismo bShowVertexAlpha — vColor.a es el lado izquierdo del alpha-test, y si
            ' los dos pases discrepan la silueta que castea deja de ser la que se dibuja. Aca se leen de ahi;
            ' `hasVertexColorData` e `isTreeAnim` siguen calculados arriba porque los usa el volcado de
            ' diagnostico de mas abajo.
            shader.SetBool("bShowVertexColor", material.UseVertexColor)
            shader.SetBool("bShowVertexAlpha", material.UseVertexAlpha)
            ' [VCOLOR-DBG] Sonda diagnostica: reporta la distribucion real del vertex color del mesh para
            ' decidir si la divergencia app-vs-motor del orden del vColor (motor: post-softlight; app: en la
            ' base del softlight) es visible o inerte (vColor blanco => inerte). min/mean/max en 0..1 crudo.
            If Logger.Enabled AndAlso hasVertexColorData Then
                Dim vcs = MeshData.Meshgeometry.VertexColors
                If vcs IsNot Nothing AndAlso vcs.Length > 0 Then
                    Dim n = vcs.Length
                    Dim mnR = Single.MaxValue, mnG = Single.MaxValue, mnB = Single.MaxValue
                    Dim mxR = Single.MinValue, mxG = Single.MinValue, mxB = Single.MinValue
                    Dim sR As Double = 0, sG As Double = 0, sB As Double = 0
                    Dim whiteN = 0
                    ' CANAL ALPHA. Faltaba, y es el que importa para una shape alpha-blend: el shader arranca
                    ' con `color = vColor` y hace `color.a *= texDiffuse.a`, así que el alpha POR VÉRTICE es un
                    ' factor de primera clase del alpha del fragmento — no una curiosidad del color. Sin esto no
                    ' se puede distinguir "cambió la textura" de "cambió el dato de vértice".
                    Dim mnA = Single.MaxValue, mxA = Single.MinValue
                    Dim sA As Double = 0
                    Dim opaqueN = 0
                    For Each c In vcs
                        sR += c.X : sG += c.Y : sB += c.Z : sA += c.W
                        mnR = Math.Min(mnR, c.X) : mnG = Math.Min(mnG, c.Y) : mnB = Math.Min(mnB, c.Z)
                        mxR = Math.Max(mxR, c.X) : mxG = Math.Max(mxG, c.Y) : mxB = Math.Max(mxB, c.Z)
                        mnA = Math.Min(mnA, c.W) : mxA = Math.Max(mxA, c.W)
                        If c.W >= 0.996F Then opaqueN += 1
                        If c.X >= 0.996F AndAlso c.Y >= 0.996F AndAlso c.Z >= 0.996F Then whiteN += 1
                    Next
                    Dim shpVc = MeshData.Shape?.ShapeName
                    Logger.LogLazy(Function() $"[VCOLOR-DBG] shape='{shpVc}' isSSE={isSSE} type={materialBase.NifShaderType} facegen={materialBase.Facegen} skinTint={materialBase.SkinTint} hair={materialBase.Hair} nVerts={n} " &
                                              $"mean=({sR / n:F3},{sG / n:F3},{sB / n:F3}) min=({mnR:F3},{mnG:F3},{mnB:F3}) max=({mxR:F3},{mxG:F3},{mxB:F3}) whiteFrac={whiteN / CSng(n):F3} " &
                                              $"| ALPHA mean={sA / n:F4} min={mnA:F4} max={mxA:F4} opaqueFrac={opaqueN / CSng(n):F3}")
                End If
            End If
            shader.SetBool("bApplyZap", shape.ApplyZaps)
            shader.SetBool("bWireframe", shape.Wireframe)
            shader.SetBool("bHide", shape.RenderHide)

            '===============================
            ' ILUMINACIÓN PRINCIPAL
            '===============================

            shader.SetBool("bLightEnabled", True)
            ' El rig de luces se autora en espacio PERCEPTUAL (sRGB) y se decodea a lineal AL SUBIR, así la
            ' config queda intacta y un mismo rig sirve a los dos juegos, que corren el pipeline lineal del
            ' motor. Las direcciones son geométricas y nunca se convierten.
            ' Ambient HEMISFÉRICO (engine-faithful: el ambient del motor depende de la normal, no es plano):
            ' dos colores —cielo y suelo— que el shader mezcla por la componente up. Una config vieja sin
            ' hemisferio deriva uno neutro del escalar. Son 3 perillas independientes: intensidad global,
            ' nivel del suelo respecto del cielo, y tinte.
            ' El rig ya viene resuelto para este frame: depende sólo del rig activo y la cámara, constantes
            ' durante el frame.
            Dim lights = Me.ParentModel.FrameLights
            shader.SetVector3("ambientSky", lights.AmbientSky)
            shader.SetVector3("ambientGround", lights.AmbientGround)

            shader.SetVector3("frontal.diffuse", lights.KeyDiffuse)
            shader.SetVector3("frontal.direction", lights.KeyDir)
            ' Luz direccional 0
            shader.SetVector3("directional0.diffuse", lights.Fill0Diffuse)
            shader.SetVector3("directional0.direction", lights.Fill0Dir)

            ' Luz direccional 1
            shader.SetVector3("directional1.diffuse", lights.Fill1Diffuse)
            shader.SetVector3("directional1.direction", lights.Fill1Dir)

            ' Luz direccional 2
            shader.SetVector3("directional2.diffuse", lights.BackDiffuse)
            shader.SetVector3("directional2.direction", lights.BackDir)

            ' LOS UNIFORMS DE SOMBRA NO SE SUBEN ACA. Son constantes de FRAME (matriz de la luz, bias,
            ' radio del PCF, la textura), no de malla: los sube PreviewModel.UploadShadowUniforms una sola
            ' vez, justo despues del pase de profundidad. Subirlos por malla costaba ocho uniforms + un
            ' bind de textura por draw, y ademas copiaba una LightFit (tres Matrix4) en cada acceso a la
            ' propiedad. Es el mismo criterio que ya tiene el rig de luces con _frameLights.

            '===============================
            ' ?? TEXTURAS (Sample BINDs)
            '===============================
            If diffuseTextureId <> 0 Then
                shader.BindTexture("texDiffuse", diffuseTextureId, TextureUnit.Texture0)
            ElseIf MeshData.Shape IsNot Nothing AndAlso MeshData.Shape.IsHelperShape Then
                ' Una helper que el usuario decidio VER (casilla "Show helper shapes") no tiene material ni
                ' texturas: en blanco tapaba el modelo como una mancha. Negro la deja como silueta.
                shader.BindTexture("texDiffuse", Me.ParentModel.ParentControl.defaultHelperTex, TextureUnit.Texture0)
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

            ' texSpecular = TXST slot 7. El motor lo lee DOS veces desde material+0x68: t2 (mask especular, bajo
            ' MODELSPACENORMALS) y t9 (backlight, bajo BACK_LIGHTING). Si el slot esta VACIO no lo saltea: el
            ' default-fill del material lo rellena y el ORDEN de sus ramas manda - primero el default GENERICO
            ' (normal map plano) si hay backLighting, con UNA sola condicion, y solo si esa no corrio, el
            ' default de height map (NEGRO) si (skinned && MSN).
            ' La semantica de los 5 booleanos esta MEDIDA, no supuesta: la primera rama del mismo default-fill
            ' llena el slot 2 con (a3||a4), y los dos unicos consumidores del slot 2 en SetupMaterial son
            ' SOFT_LIGHTING y RIM_LIGHTING, o sea {a3,a4} = {rim, soft}. Eso fija las posiciones 3-4, que es
            ' donde ReceiveValuesFromRootMaterial(skinned, rim, soft, backLighting, MSN) las pone.
            ' smoothSpecTextureId is already the engine's (EngineDefaultTextureLaw.MaterialSlot.Slot7): FO4 White; SSE back light ->
            ' DefNormalMap, else SPECULAR && MSN -> DefHeightMap (black: the engine never falls to the normal's alpha under MSN, 0/6096),
            ' in the fill's order (0x141523F76..FBA). 0 = the slot is not sampled: white stands in, unread.
            shader.BindTexture("texSpecular", If(smoothSpecTextureId <> 0, smoothSpecTextureId, CUInt(Me.ParentModel.ParentControl.defaultWhiteTex)), TextureUnit.Texture4)

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
            ' (technique 4) the LightingTexture (texture-set slot 2, the _sk map) is the SUBSURFACE map.
            ' VERIFIED in SkyrimSE.exe BSLightingShader::SetupMaterial @0x1414DC310 (jump table 0x14DCFD4,
            ' facegen branch 0x1414DC542): SetPSTexture(3, mat+0xA0) / (4, mat+0xA8) / (12, mat+0xB0), y
            ' OnLoadTextureSet 0x1414BA6E0 llena +0xA0<-slot6, +0xA8<-slot3, +0xB0<-slot2. Es decir el mapeo
            ' real es slots {6,3,2} -> PS t3(TINT) / t4(DETAIL) / t12(subsurface). El slot 5 NO participa.
            ' (Corrige la nota previa "{3,5,6} -> t3/t4/t12", que tenia el tint y el detail intercambiados.)
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

            ' SSE FaceGen albedo tint: el FACETINT (texture-set slot 6 -> material+0xA0 -> engine PS t3) entra por
            ' SOFT-LIGHT sobre el diffuse, igual que el skin tint del CUERPO (tecnica FacegenRGBTint). NO es el
            ' multiplicador amplificado: ese es el DETAIL (slot 3 -> t4). Ver el bloque de la ley en Shader_Class.
            ' Se bindea a texGlowmap (las caras no tienen glow); el _sk queda en texLightmask (t12) arriba.
            ' Default con slot 6 vacio = DefaultGreyMap 0.5 del motor = soft-light IDENTIDAD (NO blanco: blanco
            ' daria softlight(d,1) = 2d - d^2, que aclara la cara). SSE + facegen gated; FO4 intacto.
            If isSSE AndAlso materialBase.Facegen Then
                Dim facetintId As UInteger = material.InnerLayerTexture_ID
                shader.BindTexture("texGlowmap", If(facetintId <> 0, facetintId, Me.ParentModel.ParentControl.defaultFacegenTintTex), TextureUnit.Texture6)
            End If

            ' SSE PARALLAX (type 3) height = texture-set slot 3 (Parallax vf8 0x141525FE7..FF3 -> +0xA0 -> PS t3, SetupMaterial
            ' 0x141548A07..A16) and MULTILAYER PARALLAX (type 11) inner layer = slot 6 (vf8 0x141528647..656 -> +0xA0 -> PS t8,
            ' 0x141548EF5..F04): raw like every SSE slot, an empty / absent slot the engine's default (EngineDefaultTextureLaw). A
            ' technique that does not sample the slot: white stands in, unread.
            If isSSE Then
                If sseLitType = 3 Then
                    Dim heightId = material.EngineSlotTextureId(EngineDefaultTextureLaw.MaterialSlot.Height,
                        material.SlotTextureInColorSpace(material.SlotPath(EngineDefaultTextureLaw.MaterialSlot.Height), material.DisplacementTexture_ID, False))
                    shader.BindTexture("texHeight", If(heightId <> 0, heightId, CUInt(Me.ParentModel.ParentControl.defaultWhiteTex)), TextureUnit.Texture11)
                Else
                    shader.BindTexture("texHeight", Me.ParentModel.ParentControl.defaultWhiteTex, TextureUnit.Texture11)
                End If
                If sseLitType = 11 Then
                    Dim innerId = material.EngineSlotTextureId(EngineDefaultTextureLaw.MaterialSlot.InnerLayer,
                        material.SlotTextureInColorSpace(material.SlotPath(EngineDefaultTextureLaw.MaterialSlot.InnerLayer), material.InnerLayerTexture_ID, False))
                    shader.BindTexture("texInnerLayer", If(innerId <> 0, innerId, CUInt(Me.ParentModel.ParentControl.defaultWhiteTex)), TextureUnit.Texture12)
                Else
                    shader.BindTexture("texInnerLayer", Me.ParentModel.ParentControl.defaultWhiteTex, TextureUnit.Texture12)
                End If
            End If

            '===============================
            ' ?? PROPIEDADES DEL MATERIAL
            '===============================
            Dim uv = material.RestUv()
            shader.SetVector2("uvOffset", uv.Offset)
            shader.SetVector2("uvScale", uv.Scale)
            ' Umbral de alpha (solo necesario si usás discard por transparencia)
            shader.SetFloat("alphaThreshold", material.AlphaTestThreshold)

            '===============================
            ' ?? TOGGLES DE EFECTOS Y SOMBREADO
            '===============================
            ' S-P / S-M (Fragment_SSE bSseParallax / bSseMultiLayer). MultiLayerParallax constants: PS cb1[6] = mat+0xB8..C4 (SetupMaterial
            ' case 11, 0x141548F10..F48) and cb1[2].x = mat+0xC8 (0x141548F86..F9A), read by LoadBinary 0x141528A00 from the NIF block
            ' (ParallaxInnerLayerThickness, ParallaxRefractionScale, ParallaxInnerLayerTextureScale, ParallaxEnvmapStrength): the material
            ' does not carry them, the block does (as SseInputs reads the flags).
            Dim sseMlp = If(sseLitType = 11, TryCast(shape.NifShader, NiflySharp.Blocks.BSLightingShaderProperty), Nothing)
            If isSSE Then
                shader.SetBool("bSseParallax", sseLitType = 3)
                shader.SetBool("bSseMultiLayer", sseMlp IsNot Nothing)
                If sseMlp IsNot Nothing Then shader.SetVector4("uMlpLayer", New Vector4(sseMlp.ParallaxInnerLayerThickness, sseMlp.ParallaxRefractionScale,
                                                                                         sseMlp.ParallaxInnerLayerTextureScale.U, sseMlp.ParallaxInnerLayerTextureScale.V))
            End If
            shader.SetBool("bCubemap", hasCubemap)
            ' MultiLayerParallax: SetupMaterial case 11 binds the cube t4 and the mask t5 whatever the flags (0x141548F4B..F68); SLSF1 7 is
            ' off in 691 / 691 (user decision 6-oct-2026: the reflection is not tied to bit 7).
            shader.SetBool("bEnvMap", materialBase.EnvironmentMapping OrElse If(isSSE, sseLitType = &H10, materialBase.IsEngineEye()) OrElse sseMlp IsNot Nothing)
            ' SSE Eye technique (16): the cube and the ambient take the eye VS's radial vector o7 (Fragment_SSE bEye / bEyeRadial,
            ' Vertex_SSE vertexEyeCenter, S-O1). bEye = technique type 16 of the full selector (sseLitType; user decision 7-oct-2026,
            ' rev-04: one selector of the eye); bEyeRadial = this shape uploaded its centres (EyeCentresForUpload).
            If isSSE Then shader.SetBool("bEye", sseLitType = &H10)
            If isSSE Then shader.SetBool("bEyeRadial", vboEyeCenter <> 0)
            ' SSE ANISO_LIGHTING = technique bit 16 (SLSF2 Anisotropic_Lighting) in ANY technique type: the light's specular
            ' is the shifted-normal lobe 0.7*a1 (PS 0x00010201 rec 4112); the HairTint technique adds the second, tinted lobe
            ' (0x06010203 rec 8996); both times max(L.z, 0) in the PS space (Fragment_SSE directionalLight, uSseLitSpace).
            ' FO4 hair is Kajiya-Kay through its flow map (Fragment_FO4). Gated isSSE.
            If isSSE Then shader.SetBool("bAnisoLighting", materialBase.AnisoLighting)
            ' Alpha-blend (forward b6) vs opaque (deferred): gates the strong forward material-cube envmap.
            ' Opaque BGSM (pierce-type chrome gems) render deferred where the engine uses the scene IBL,
            ' not the material cube -- so the forward *3 over-grays them. (Eye keeps it via its inline path.)
            shader.SetBool("bHasAlphaBlend", hasAlphaBlend OrElse If(isSSE, sseLitType = &H10, materialBase.IsEngineEye()))
            shader.SetBool("bAlphaTest", hasAlphaTest)
            shader.SetBool("bEnvMask", envmapMaskTextureId <> 0)
            shader.SetBool("bNormalMap", normalTextureId <> 0)
            ' Kept on the resolved id on purpose (C4): greyscaleTextureId is already the law's - SSE: a missing palette gives
            ' DefNormalMap (id <> 0), so this equals MaterialData.EngineRecolors().Color; FO4: a missing palette is NotTraced -> id 0 ->
            ' no recolor, as before (rev-01: the pixels of an untraced branch do not change).
            shader.SetBool("bGreyscaleColor", materialBase.GrayscaleToPaletteColor AndAlso greyscaleTextureId <> 0)
            shader.SetBool("bSpecular", materialBase.SpecularEnabled AndAlso hasSpecularSource)
            If isSSE Then shader.SetBool("bHasSpecMap", hasSpecMap)
            shader.SetBool("bModelSpace", materialBase.ModelSpaceNormals)
            shader.SetBool("bEmissive", materialBase.EmitEnabled)
            ' Subsurface (soft lighting) is a per-material property: bind it from the material's own
            ' Soft_Lighting flag, both engines. Do NOT force it for SSE facegen (`OrElse (isSSE AndAlso
            ' Facegen)`): the engine selects facegen subsurface by the material flag via the descriptor
            ' SOFT_LIGHTING bit, and vanilla/mod facegen heads ship the flag OFF, so forcing it adds
            ' subsurface the engine does not apply. Face/body parity: MatchBodySkinSubsurfaceToFace.
            shader.SetBool("bSoftlight", materialBase.SubsurfaceLighting)
            shader.SetBool("bGlowmap", materialBase.Glowmap AndAlso glowTextureId <> 0)
            ' Hair (FO4 carries Hair=true AND Glowmap=true): the glow slot holds the _f strand FLOW map,
            ' not a glow. bHair drives the Kajiya-Kay anisotropic specular + hair tint, robust vs the type.
            shader.SetBool("bHair", materialBase.Hair)
            shader.SetBool("bHasGlowTex", glowTextureId <> 0)
            ' bLightmask: the _sk subsurface map drives the SSS/rim mask, incl. facegen (engine t12, above).
            ' FACEGEN sin _sk: True igual — arriba quedó bindeado el default NEGRO del engine (SSS=0);
            ' con False el shader caería al fallback softMask=albedo (eso es solo para NO-facegen).
            ' Con SOFT_LIGHTING o RIM_LIGHTING el motor samplea t12 SIEMPRE (rellena el slot vacio con su
            ' default generico), asi que el shader tambien debe samplearlo: sin esto caia a `albedo` (soft) o
            ' a 1.0 (rim), que no es lo que hace el motor. Ver el bind de texLightmask arriba.
            If isSSE Then shader.SetBool("bLightmask", lightingTextureId <> 0 OrElse materialBase.Facegen _
                                                      OrElse materialBase.SubsurfaceLighting OrElse materialBase.RimLighting)
            ' (bFacetintAlbedo ELIMINADO: la cadena facegen completa la gatea bHasDetailMask, abajo. El engine no
            ' gatea por "hay facetint": rellena el slot vacio con DefaultGreyMap 0.5 = soft-light identidad, y el
            ' bind de texGlowmap de arriba hace exactamente eso. Un gate aparte solo podia desincronizarse.)
            ' rev-05: Fallout 4's smoothness is lighting var 9 (+0x88) at rest; Skyrim SE's +0x88 is the raw glossiness, below.
            shader.SetFloat("shininess", If(isSSE, materialBase.Smoothness, material.RestValue(RestVariable.Glossiness)))
            ' SSE: exponente de glossiness CRUDO (shad.Glossiness), no reconstruido por el shader.
            If isSSE Then shader.SetFloat("glossiness", material.RestValue(RestVariable.Glossiness))
            ' FO4 SetupMaterial powf(2.2)s the material colours (DAT_142475358); SSE's pipeline is raw (no gamma
            ' constant anywhere in its setup, raw cbuffer writes).
            shader.SetVector3("specularColor", RestColour(material.RestColourAt(RestVariable.SpecularColor), materialBase.SpecularColor, isSSE))
            shader.SetFloat("specularStrength", material.RestValue(RestVariable.SpecularStrength))
            shader.SetVector3("emissiveColor", RestColour(material.RestColourAt(RestVariable.LightingEmissiveColor), materialBase.EmittanceColor, isSSE))
            shader.SetFloat("emissiveMultiple", material.RestValue(RestVariable.LightingEmissiveMultiple))
            shader.SetFloat("fresnelPower", materialBase.FresnelPower)
            shader.SetFloat("subsurfaceRolloff", materialBase.SubsurfaceLightingRolloff)
            shader.SetFloat("paletteScale", materialBase.GrayscaleToPaletteScale)
            ' MultiLayerParallax: cb1[2].x = ParallaxEnvmapStrength (mat+0xC8, 0x141548F8A..F9A); cb2[3].x stays 1 (ctor 0x141518E71).
            ' C10: lighting var 8 at rest (RestValue: Fallout 4 writes it on an Envmap material only, MaterialData.RestWrites).
            shader.SetFloat("envReflection", If(sseMlp IsNot Nothing, sseMlp.ParallaxEnvmapStrength, material.RestValue(RestVariable.EnvMapScale)))
            shader.SetBool("bBacklight", materialBase.BackLighting)
            ' GATE SÓLO EN SKYRIM. En FO4 el bool de backlight NO gatea nada: en la transferencia
            ' BGSM → material hay exactamente DOS compuertas booleanas (el rolloff del subsurface y el de
            ' wetness/SSR), y el power de backlight se copia con un `mov` pelado. Verificado en el binario del
            ' juego y en el del CK, mismo layout. Sobre el corpus vanilla hay 171 materiales con el flag en
            ' False y power > 0, y el motor les aplica la transmisión igual.
            ' SYNC: RENDER == BAKE — la ruta de ESCRITURA lleva el MISMO gate por juego
            ' (FO4UnifiedMaterial_Class). En Skyrim el gate se conserva porque allá HasBacklight es un flag
            ' REAL del NIF; en FO4 se sintetiza con `power > 0` al leer.
            shader.SetFloat("backlightPower",
                            If(isSSE AndAlso Not materialBase.BackLighting, 0.0F, materialBase.BackLightPower))
            shader.SetBool("bRimlight", materialBase.RimLighting)
            shader.SetFloat("rimlightPower", materialBase.RimPower)
            shader.SetBool("bDoubleSided", materialBase.TwoSided)

            ' SkinTint / HairTint tint color.
            ' FO4 (engine): SkinTint = the per-actor SKIN TONE soft-lit at render (the FaceGen genetic-blend
            '   pass writes it to material+0xC0 for every SkinTint shape; SetupMaterial case 5 gamma-corrects
            '   pow 2.2 -> cb1[1]). Source = the per-mesh SkinToneColor (NPC) or the material SkinTintColor (WM).
            '   The body diffuse stays UNTONED (no bake). Hair = HairTintColor.
            ' Skyrim (SSE): SkinTint = technique 5, albedo = softlight(t0, tintColor) x rgbFix (PS idx 8577) with the
            '   material tintColor CRUDE (QNAM/255 on skin, key 7 or the template's (1,1,1) on skee layers). NOT a
            '   white no-op: with t = 1 the albedo is 2a - a^2. Hair = HairTintColor.
            Dim hasTint As Boolean = materialBase.SkinTint OrElse materialBase.Hair
            ' "Ya está": if the skin tone is already baked into this mesh's diffuse (FaceTint composite,
            ' or Skyrim legacy body bake), the shader's own SkinTint soft-light must be a no-op for it —
            ' otherwise the tone is applied twice. Hair tint is independent of skin-tone baking, never suppressed.
            If material.SkinToneBaked AndAlso Not materialBase.Hair Then hasTint = False
            shader.SetBool("bHasTintColor", hasTint)
            If Logger.Enabled AndAlso isSSE AndAlso materialBase.SkinTint AndAlso Not materialBase.Facegen Then
                Dim shpNb = MeshData.Shape?.ShapeName
                Logger.LogLazy(Function() $"[SKIN-DBG] BODY shape='{shpNb}' hasTintColor={hasTint} skinToneBaked={material.SkinToneBaked} tint=({materialBase.SkinTintColor.R},{materialBase.SkinTintColor.G},{materialBase.SkinTintColor.B}) specStr={materialBase.SpecularMult:F2} specColor=({materialBase.SpecularColor.R},{materialBase.SpecularColor.G},{materialBase.SpecularColor.B}) gloss={materialBase.NifGlossiness:F3} | LIGHT-ADD soft={materialBase.SubsurfaceLighting}/roll={materialBase.SubsurfaceLightingRolloff:F3} back={materialBase.BackLighting}/pow={materialBase.BackLightPower:F3} rim={materialBase.RimLighting}/pow={materialBase.RimPower:F3} emit={materialBase.EmitEnabled}/col=({materialBase.EmittanceColor.R},{materialBase.EmittanceColor.G},{materialBase.EmittanceColor.B})x{materialBase.EmittanceMult:F2}")
            End If
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
                    ' SSE copies the hair colour into cb1[1] raw; FO4 powf(2.2)s it in SetupMaterial.
                    tintVec = If(isSSE, ScaledTintSrgb(tint, materialBase.TintColorScale),
                                        Shader_Base_Class.Vector_to_Linear(ScaledTintSrgb(tint, materialBase.TintColorScale)))
                End If
                shader.SetVector3("tintColor", tintVec)
            End If

            ' SkinTint deferred W3C soft-light strength = the skin tone .w (engine material+0xCC). The app
            ' SkinTintAlpha carries it (default 1.0 = full). Consumed by Fragment_FO4 uEffectiveType==4.
            shader.SetFloat("skinTintStrength", materialBase.SkinTintAlpha)

            ' FaceGen detail map (SSE only): texture-set slot 3 (DisplacementTexture) -> material+0xA8 -> engine
            ' PS t4, el MULTIPLICADOR AMPLIFICADO de la cadena. Ley completa (Shader_Class, DXBC + RE):
            '   albedo = softlight(diffuse(t0), facetint(t3, slot 6)) * ((detail(t4, slot 3) + off) * 255/64)
            ' El _sk (slot 2) es el SUBSURFACE -> t12 (texLightmask + SSS, arriba).
            ' CORRIGE la nota previa "facetint(t4) * softlight(diffuse, detail(t3))": tint y detail estaban
            ' INTERCAMBIADOS. El x255/64 normaliza el DETAIL (neutro 64 -> 1.0), no el facetint; con el tint
            ' pasando por el amplify un skin tone saturado aplastaba R/B (cuello mucho mas saturado que el pecho).
            ' bHasDetailMask gatea la cadena ENTERA (softlight + amplify), no solo el detail.
            If isSSE Then
                Dim detailMaskId = material.DetailMaskTexture_ID
                Dim isFaceTint As Boolean = materialBase.Facegen
                ' ENGINE-FAITHFUL (RE SkyrimSE.exe): una cabeza FaceGen SIEMPRE corre la cadena entera. Si el
                ' texture-set slot 3 está VACÍO, el motor NO lo saltea: bindea su default interno
                ' BSShader_DefFacegenDetail (uniforme 0.251 = vanilla blankdetailmap), que AMPLIFICADO da
                ' (1.015625, 1.0, 1.015625) — es decir un no-op, no un oscurecimiento. Mods que borran el TX04
                ' del TXST (Enhanced Khajiit) caen acá. Así el preview matchea lo que el NIF horneado rinde
                ' in-game (render == bake).
                shader.SetBool("bHasDetailMask", isFaceTint)
                If isFaceTint Then
                    ' Slot 3 vacío ⇒ default del engine 0.251 (BSShader_DefFacegenDetail), SIEMPRE.
                    ' Acá había una rama que, cuando el diffuse venía plegado, bindeaba un neutro (63,64,63) para
                    ' que el amplify fuera identidad. Eso valía con la ley VIEJA del fold (que neutralizaba los
                    ' slots 3 y 6). Con la ley actual el fold deja los slots REALES y PRE-COMPENSA la cadena
                    ' (SseFaceGenBaker.PreCompensateEngineChain), así que el shader tiene que aplicar el amplify
                    ' NORMALMENTE — es justo lo que cancela la pre-compensación. La rama, su flag
                    ' (MaterialData.SseFoldDetailNeutralized) y su textura quedaron muertos y se eliminaron.
                    Dim detailTex = If(detailMaskId <> 0, detailMaskId, Me.ParentModel.ParentControl.defaultFacegenDetailTex)
                    If Logger.Enabled Then
                        Dim shpN = MeshData.Shape?.ShapeName
                        Dim hasSlot = (detailMaskId <> 0)
                        Dim defVal = "0.251-engine-default"
                        Logger.LogLazy(Function() $"[DETAIL-DBG] FACE shape='{shpN}' detailSlotBound={hasSlot} → default={defVal} | facegenChain={materialBase.Facegen} skinTintColor=({materialBase.SkinTintColor.R},{materialBase.SkinTintColor.G},{materialBase.SkinTintColor.B}) specStr={materialBase.SpecularMult:F2} specColor=({materialBase.SpecularColor.R},{materialBase.SpecularColor.G},{materialBase.SpecularColor.B}) gloss={materialBase.NifGlossiness:F3} | LIGHT-ADD soft={materialBase.SubsurfaceLighting}/roll={materialBase.SubsurfaceLightingRolloff:F3} back={materialBase.BackLighting}/pow={materialBase.BackLightPower:F3} rim={materialBase.RimLighting}/pow={materialBase.RimPower:F3} emit={materialBase.EmitEnabled}/col=({materialBase.EmittanceColor.R},{materialBase.EmittanceColor.G},{materialBase.EmittanceColor.B})x{materialBase.EmittanceMult:F2} glowFlag={materialBase.Glowmap}/glowTexId={glowTextureId}")
                    End If
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
            ' Palette alpha = MaterialData.EngineRecolors (technique bit 14 / 20: the flag AND a palette path).
            shader.SetBool("bEffectGreyscaleAlpha", material.EngineRecolors().Alpha)
            shader.SetVector4("effectFalloffParams", New OpenTK.Mathematics.Vector4(material.RestValue(RestVariable.FalloffStartAngle), material.RestValue(RestVariable.FalloffStopAngle),
                                                                                   material.RestValue(RestVariable.FalloffStartOpacity), material.RestValue(RestVariable.FalloffStopOpacity)))
            ' FO4 effect falloff per vertex (Vertex_FO4) when the DRAWN geometry has vertex normals: the geometry is
            ' this mesh's (MeshData), whose own material says whether it is model-space-normal (no vertex normals),
            ' also when the material bound here is an overlay layer's.
            Dim geometryHasNormals = Not (MeshData.Material?.MaterialBase IsNot Nothing AndAlso MeshData.Material.MaterialBase.ModelSpaceNormals)
            shader.SetBool("bVertexFalloff", isBGEM AndAlso (materialBase.FalloffEnabled OrElse materialBase.FalloffColorEnabled) AndAlso geometryHasNormals)
            ' Output alpha = diffuse.a * cb1[0].w * cb2[13].w (i1385). cb1[0].w = kBaseColor.a = the FILE Alpha (the
            ' parser stores it at data+0x60 and ApplyMaterialData builds kBaseColor from it, 0x14216C065..C185,
            ' 0x1421717EF..1835); cb2[13].w = the property's fAlpha = 1.0 (ctor 0x1421688FB, never written by
            ' ApplyMaterialData). The app aliases BaseColor.A to the same file Alpha: it enters ONCE.
            shader.SetFloat("effectBaseColorAlpha", materialBase.Alpha)
            shader.SetFloat("effectBaseColorScale", material.RestValue(RestVariable.EffectEmissiveMultiple))
            ' The LIGHTING light: the weather's effect light kept in the game's proportion to the frame's key
            ' (PreviewImagingRow.EffectLightForKey; the key in the space the lit shaders get it, FrameLights).
            Dim effectL = Me.ParentModel.FrameImaging.Row.EffectLightForKey(Me.ParentModel.FrameLights.KeyDiffuse,
                                                                            Me.ParentModel.FrameImaging.Settings.EffectLightVsKey)
            If isSSE Then
                ' SSE SetupMaterial of the effect property (FO4UnifiedMaterial_Class.EngineSseEffectConstants), raw.
                ' The palette branch is the shader's bGreyscaleColor: same predicate (bit 19 = flag + greyscale tex).
                Dim ssePalette = materialBase.GrayscaleToPaletteColor AndAlso greyscaleTextureId <> 0
                Dim ks = FO4UnifiedMaterial_Class.EngineSseEffectConstants(RestBase(material), material.RestValue(RestVariable.EffectEmissiveMultiple), ssePalette,
                                                                          materialBase.LightingInfluence, materialBase.EffectLightingEnabled)
                shader.SetVector3("effectBaseColor", ks.BaseColor)
                shader.SetFloat("effectLightingInfluence", ks.Influence)
                shader.SetBool("bEffectLighting", ks.LightingTechnique)
                shader.SetVector3("effectLight", effectL)
            Else
                ' FO4 SetupMaterial of the effect material (FO4UnifiedMaterial_Class.EngineEffectConstants). The
                ' palette branch is the shader's bGreyscaleColor: same predicate.
                Dim palette = materialBase.GrayscaleToPaletteColor AndAlso greyscaleTextureId <> 0
                Dim k = FO4UnifiedMaterial_Class.EngineEffectConstants(RestBase(material), material.RestValue(RestVariable.EffectEmissiveMultiple), palette,
                                                                       materialBase.LightingInfluence, materialBase.EffectLightingEnabled)
                shader.SetVector3("effectBaseColor", k.BaseColorLinear)
                shader.SetFloat("effectLightingInfluence", k.InfluenceByte / 255.0F)
                shader.SetBool("bEffectLighting", k.LightingTechnique)
                shader.SetVector3("effectDLightColor", effectL)
                shader.SetFloat("effectEnvMinLod", materialBase.EnvmapMinLOD)
            End If
            ' SOFT (FO4 technique bit 12 / SSE bit 18 = SLSF1 bit 30, the BGEM's SoftEnabled; no setting can turn it
            ' off, and the preview is not first person): the fade reads the scene depth copied this frame. Before the
            ' copy (the opaque groups) there is none: no fade (hole, see RenderAll).
            Dim softOn = isBGEM AndAlso materialBase.SoftEnabled AndAlso Not PreviewModel.GateDisableSoft
            If softOn AndAlso Not isSSE Then
                ' FO4 reads the LIVE depth (logical depth 1, bound read-only while it is an SRV, 0x14183C863): all that was
                ' drawn before this effect. Copy it when something wrote depth since the last copy.
                If Me.ParentModel.FrameDepthDirty Then
                    Me.ParentModel.ParentControl.CopySceneDepth()
                    Me.ParentModel.FrameDepthDirty = False
                    Me.ParentModel.FrameSceneDepthValid = True
                End If
            End If
            ' SSE reads the copy RenderAll took after the opaque groups; an effect drawn before it (the opaque groups)
            ' waits on the z-prepass question (Tools/re-docs/RE_EFFECT_PARTICLE_ENVCUBE_2026-10-03.md).
            softOn = softOn AndAlso Me.ParentModel.FrameSceneDepthValid
            shader.SetBool("bSoftEffect", softOn)
            ' FO4 MULTBLEND (technique bit 6) changes the PS (Fo4EffectBlend).
            If Not isSSE Then
                Dim eaM = materialBase.ResolveEngineAlpha()
                shader.SetBool("bEffectMultBlend", isBGEM AndAlso Fo4EffectTechnique(eaM.Src, eaM.Dst).MultBlend)
            End If
            ' What this draw uploads as its alpha test, for the [DRAW-STATE] log: the generic upload above, overwritten by each
            ' game's branch below (SSE: SseRenderPassLaw; FO4: Fo4RenderPassLaw.ColourPassAlphaTest).
            Dim testSubido As Boolean = CBool(hasAlphaTest)
            Dim umbralSubido As Single = material.AlphaTestThreshold
            ' The SSE pass law's inputs, its verdict and the SPECULAR technique bit of this draw, read ONCE: the lighting tail and
            ' the pass law below and the SAO normals target further down take them.
            Dim sseIn As SseRenderPassLaw.SseShapeInputs, ssePass As SseRenderPassLaw.SsePass, sseSpecular As Boolean, sseSnow As Boolean
            ' SSE MULTBLEND (technique bit 11): an alpha property whose dest blend is SRC_COLOR (0x14152A4BE..A570).
            If isSSE Then
                ' Technique bits of the SSE effect (0x14152A4BE..A570): MULTBLEND (11) = dest SRC_COLOR, MULTBLEND_DECAL (22) =
                ' src DEST_COLOR + dest INV_SRC_ALPHA. MULTBLEND drops the soft 0.003 discard; both drop invFrameBufferRange.
                Dim eaS = materialBase.ResolveEngineAlpha()
                shader.SetBool("bSseMultBlend", isBGEM AndAlso eaS.Dst = NiflySharp.Enums.AlphaFunction.SRC_COLOR)
                shader.SetBool("bSseMultBlendDecal", isBGEM AndAlso eaS.Src = NiflySharp.Enums.AlphaFunction.DEST_COLOR AndAlso eaS.Dst = NiflySharp.Enums.AlphaFunction.INV_SRC_ALPHA)
                ' The preview is a world render: the world value (the UI render 0x14116AC80 sets 1.0 only while it draws).
                shader.SetFloat("sseInvFramebufferRange", SseInvFrameBufferRangeWorld)
                ' The lighting tail: both clamps (cb0[1].x PostLit, cb0[1].z PostSpec) and the SPECULAR permutation (technique
                ' bit 9, SseRenderPassLaw.SpecularTechnique) that runs it in two stages; Z by the list the shape lands in.
                sseIn = sseIn0
                shader.SetFloat("sseLitClampPostLit", SseLightingOutputClampPostLit)
                shader.SetFloat("sseLitClampPostSpec", SseLightingOutputClampPostSpec)
                sseSpecular = SseRenderPassLaw.SpecularTechnique(sseIn, SseRenderPassLaw.ImprovedSnowExeDefault)
                shader.SetBool("bSseSpecularTech", sseSpecular)
                ' S-N SNOW (technique bit 21): no specular in o0, o3 = (rim, t0.a) into kSNOW_SPECALPHA (draw buffer 4, masked below next
                ' to the SAO normals' buffer 3).
                sseSnow = Not isBGEM AndAlso SseRenderPassLaw.SnowTechnique(sseIn, SseRenderPassLaw.ImprovedSnowExeDefault)
                shader.SetBool("bSseSnow", sseSnow)
                shader.SetVector4("uSseSnowRim", SseRenderPassLaw.SnowRimLightParameters)
                ' S1 BLOOD (effect technique bit 14) at rest: cb2[9] = (threshold / 255, 0, 0) - the two weapon-blood globals are 0 until the
                ' temporary blood effect runs (0x141558820); the threshold term is the NiAlphaProperty's (0x141557B94..BB7, 1/255 = token
                ' [0x141B5BDC8] 0x3B808081).
                shader.SetBool("bSseBlood", SseRenderPassLaw.EffectBloodTechnique(sseIn))
                shader.SetVector3("uSseBloodParams", New Vector3(materialBase.ResolveEngineAlpha().Threshold * (1.0F / 255.0F), 0.0F, 0.0F))
                ' The space the lighting PS works in (read by the anisotropic lobe's max(L.z, 0)): SseLitSpace, from the placement
                ' SkinningHelper uploaded for this shape.
                shader.SetMatrix3("uSseLitSpace", SseLitSpace(sseIn.LitSkinned, sseLitType, MeshData.Meshgeometry.GPUBoneMatrices, MeshData.Shape))
                ' The pass law: tail Z, main-pass alpha test (the pass's own reference, SubgroupRefThreshold), the effect's
                ' bit 26 (no test) and prop+0x30, the Hair + DEPTH_WRITE_DECALS rule. The prepass branch is off for a colour draw.
                ssePass = SseRenderPassLaw.Classify(sseIn)
                shader.SetFloat("sseLitTailZ", ssePass.TailZ)
                shader.SetBool("bAlphaTest", ssePass.MainAlphaTest)
                shader.SetFloat("alphaThreshold", ssePass.MainAlphaThreshold)
                testSubido = ssePass.MainAlphaTest
                umbralSubido = ssePass.MainAlphaThreshold
                shader.SetFloat("sseEffectPropAlpha", If(isBGEM, materialBase.Alpha, 1.0F))
                shader.SetBool("bSseHairDepthWriteDecal", Not isBGEM AndAlso ssePass.HairDepthWriteDecal)
                shader.SetBool("bSsePrepass", False)
            Else
                shader.SetBool("bFo4Prepass", False)
                ' FO4 colour pass: its alpha test and threshold (Fo4RenderPassLaw.ColourPassAlphaTest; an effect tests always,
                ' 0 without a test). Overrides the generic upload. The shadow pass has its own writer (ShadowPassAlphaTest).
                Dim atC = Fo4RenderPassLaw.ColourPassAlphaTest(isBGEM, CBool(hasAlphaTest), materialBase.ResolveEngineAlpha().Threshold)
                shader.SetBool("bAlphaTest", atC.Test)
                shader.SetFloat("alphaThreshold", atC.Threshold)
                testSubido = atC.Test
                umbralSubido = atC.Threshold
                ' FO4 world lighting is drawn by the G-buffer pass: its vertex-alpha gate (Fo4RenderPassLaw).
                If Not isBGEM Then shader.SetBool("bFo4GBufferTestVertexAlpha", material.Fo4Pass().GBufferTestVertexAlpha)
            End If
            If softOn Then
                shader.SetFloat("softDepth", materialBase.SoftDepth)
                shader.SetVector2("uNearFar", Me.ParentModel.ParentControl.FrameNearFar)
                shader.BindTexture("texSceneDepth", Me.ParentModel.ParentControl.SceneDepthTexture, TextureUnit.Texture9)
            End If
            ' Post off (PreviewImagingSettings) = the frame is direct and the fragment applies the 2.3.8 display law,
            ' unless a debug view wants the raw values.
            shader.SetBool("bLegacyDisplay", Not Me.ParentModel.FrameIsHdr AndAlso Not Me.ParentModel.FrameGBufferStage AndAlso
                                             Shader_Base_Class.DebugView = ShaderDebugView.None)
            shader.SetFloat("uSceneToLinear", Shader_Base_Class.SceneToLinearExponent(isSSE))

            '

            ' === DebugMode ===
            ' La vista de depuracion es UNA sola para el proceso y para los dos juegos: se lee de la
            ' propiedad Shared, NO del shader que toco dibujar. Ver Shader_Base_Class.DebugView, que
            ' explica por que dejo de ser un campo de instancia. Con None (el default y el unico estado
            ' en el que arranca la app) el GLSL ni entra al bloque: `if (DebugMode > 0.0)`.
            shader.SetFloat("DebugMode", CSng(Shader_Base_Class.DebugView))

            ' Alpha global
            shader.SetFloat("alpha", material.PreviewAlpha())
            ' === Depth Test / Write (ResolveColourDepthState: SSE pass law, FO4 rules) ===
            Dim depthState = ResolveColourDepthState(material)
            If depthState.Test Then
                GL.Enable(EnableCap.DepthTest)
                GL.DepthFunc(depthState.Func)
            Else
                GL.Disable(EnableCap.DepthTest)
            End If
            Dim writeDepth As Boolean = depthState.Write
            GL.DepthMask(writeDepth)
            If writeDepth Then Me.ParentModel.FrameDepthDirty = True
            ' FO4 world effects write RGB only (0x1421D5C22; DECAL forces the same): the target's alpha is left alone.
            Dim fo4Effect = materialBase IsNot Nothing AndAlso materialBase.IsBGEM() AndAlso Not isSSE
            GL.ColorMask(0, True, True, True, Not fo4Effect)
            ' SSE SAO normals target (draw buffer 3): the opaque finish's RT2 write state in draw order (SseRenderPassLaw.AoNormalWrite);
            ' the overlay layers go through here too. Without A3 bound (FO4, post off, after the composite) the mask is inert.
            ' uSseSsrParams = cb2[7] of PS 4255 (RE_SAO_BOTH 12.1/12.5): (fSpecMaskBegin 0.1, + fSpecMaskSpan 0, selector 0, the
            ' SPECULAR technique bit 9 (SseRenderPassLaw.SpecularTechnique) x specularLODFade 1 - declared hole); .data values, SpecMask is in none of the installed
            ' Skyrim.ini / SkyrimPrefs.ini / SkyrimCustom.ini (measured 3-oct-2026).
            If isSSE Then
                Dim aoKind = SseRenderPassLaw.SseAoNormal.None
                Dim aoEffect = SseRenderPassLaw.SseAoEffectNormal.None
                Dim aoWrite = False
                If materialBase IsNot Nothing Then
                    Dim listAfter As Boolean
                    aoWrite = SseRenderPassLaw.AoNormalWrite(Me.ParentModel.FrameAoListOn, sseIn, ssePass, listAfter)
                    Me.ParentModel.FrameAoListOn = listAfter
                    aoEffect = SseRenderPassLaw.AoEffectNormalClass(sseIn)
                    aoKind = If(Not sseIn.IsEffect OrElse aoEffect <> SseRenderPassLaw.SseAoEffectNormal.None,
                                SseRenderPassLaw.SseAoNormal.Normal, SseRenderPassLaw.SseAoNormal.Colour)
                    shader.SetVector4("uSseSsrParams", New Vector4(0.1F, 0.1F, 0.0F, If(sseSpecular, 1.0F, 0.0F)))
                End If
                GL.ColorMask(3, aoWrite, aoWrite, aoWrite, aoWrite)
                ' S-N: kSNOW_SPECALPHA (draw buffer 4) only from a snow draw: a PS without o3 leaves RT 0x70 as it is (D3D does not touch a
                ' target the PS does not declare); in GL an unwritten output is undefined, so every other draw masks it. Same place, so
                ' same coverage, as the SAO normals' mask (the overlay layers included); every other GL.ColorMask of the frame is the
                ' global one (all buffers), which the next draw here narrows again.
                GL.ColorMask(4, sseSnow, sseSnow, False, False)
                shader.SetInt("uSseAoNormal", CInt(aoKind))
                shader.SetInt("uSseAoEffectClass", CInt(aoEffect))
            End If
            ' === Blending / Alpha Test / Wireframe ===
            Dim coverageMode As Integer = 0
            If MeshData.Shape.Wireframe Then
                ' Pasada en modo wireframe
                GL.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Line)
                GL.Enable(EnableCap.Blend)
                GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha)
            ElseIf hasAlphaBlend AndAlso materialBase IsNot Nothing AndAlso materialBase.IsBGEM() AndAlso Not isSSE Then
                ' FO4 effect: the engine's blend table (Fo4EffectBlend); the draw is skipped before this when the
                ' engine has no PS for it (Render).
                GL.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Fill)
                Dim ea = materialBase.ResolveEngineAlpha()
                Dim fb = Fo4EffectBlend(ea.Present, ea.Blend, ea.Src, ea.Dst, materialBase.Alpha)
                If fb.Enabled Then
                    GL.Enable(EnableCap.Blend)
                    GL.BlendFuncSeparate(CType(fb.ColorSrc, BlendingFactorSrc), CType(fb.ColorDst, BlendingFactorDest),
                                         CType(fb.AlphaSrc, BlendingFactorSrc), CType(fb.AlphaDst, BlendingFactorDest))
                    coverageMode = CoverageModeForSourceFactor(fb.ColorSrc)
                Else
                    GL.Disable(EnableCap.Blend)
                End If
            ElseIf hasAlphaBlend Then
                ' Blending estándar
                GL.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Fill)
                GL.Enable(EnableCap.Blend)
                Dim blend = material.Calculate_Blending()
                GL.BlendFunc(CType(blend(0), BlendingFactor), CType(blend(1), BlendingFactor))
                ' Skyrim SE (user decision 7-oct-2026, review rev-01): the colour blends as above and buffer 0's alpha is kept (ZERO / ONE):
                ' the display target's alpha (post off, RGBA8; CaptureBitmap reads it, Format32bppArgb) is the frame's, not a decal's
                ' blend of it. The HDR radiance target has no alpha (R11F_G11F_B10F); buffer 1 (coverage) is set below, buffer 3 (SAO
                ' normals) keeps the blend above.
                If isSSE Then GL.BlendFuncSeparate(0, CType(blend(0), BlendingFactorSrc), CType(blend(1), BlendingFactorDest), BlendingFactorSrc.Zero, BlendingFactorDest.One)
                coverageMode = CoverageModeForSourceFactor(CType(blend(0), BlendingFactor))
            ElseIf hasAlphaTest Then
                ' Alpha test (recorte)
                GL.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Fill)
                GL.Disable(EnableCap.Blend)
            Else
                ' Material completamente opaco
                GL.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Fill)
                GL.Disable(EnableCap.Blend)
            End If
            ' Coverage of the HDR target (attachment 1, see SceneTargets): blended draws accumulate it
            ' over-style (ONE, ONE_MINUS_SRC_ALPHA) whatever their colour blend is; glBlendFunc above set
            ' every draw buffer, so attachment 1 is set again after it. Display targets have no buffer 1.
            If coverageMode <> 0 Then GL.BlendFunc(1, BlendingFactorSrc.One, BlendingFactorDest.OneMinusSrcAlpha)
            shader.SetInt("uCoverageMode", coverageMode)

            ' Texture sampling: the engine's sampler of each slot (SamplerLaw, game-aware).
            BindMaterialSamplers(material, SamplerLaw.Slots(material.SamplerInputs(isSSE)))

            ' FALLOUT 4'S G-BUFFER STAGE: a lighting shape's record state replaces the forward blend / mask above; an effect decal of
            ' the G-buffer lists writes RT 0x1A only (notas G-buffer 6.3: its PS has one output; NT6, the others masked).
            If gbuf.HasValue Then
                ApplyFo4GBufferDraw(shader, material, gbuf.Value)
            ElseIf Me.ParentModel.FrameGBufferStage Then
                For rt = 1 To 5 : GL.ColorMask(rt, False, False, False, False) : Next
                If GateFo4EffectDecalMaskOpen Then GL.ColorMask(1, True, True, True, True) : GL.ColorMask(2, True, True, True, True) : GL.ColorMask(3, True, True, True, True) : GL.ColorMask(4, True, True, True, True) : GL.ColorMask(5, True, True, True, True)
            End If

            Dim polygonOffset = ResolvePolygonOffset(material)
            If polygonOffset.Enabled Then
                GL.Enable(EnableCap.PolygonOffsetFill)
                GL.PolygonOffset(polygonOffset.Factor, polygonOffset.Units)
            Else
                GL.Disable(EnableCap.PolygonOffsetFill)
            End If

            ' === Culling ===
            ' Se resuelve en la etapa de draw según el face mode efectivo del shape.

            ' DIAGNOSTICO (Logger.Enabled): el estado REAL con el que sale cada shape a dibujarse. Existe
            ' para DIFFEAR fold vs unfold: el fold de SSE no toca el material de las shapes que no son
            ' FaceTint, asi que si una de ellas (los bigotes, '_Beard') sale distinta, la diferencia tiene
            ' que estar ACA — en el bucket/orden, en el depth, en el blend o en que textura se bindeo.
            If Logger.Enabled Then
                Dim shpD = MeshData.Shape?.ShapeName
                Dim blendPair = If(hasAlphaBlend, material.Calculate_Blending(), New Integer() {0, 0})
                ' Los TRES factores del alpha del fragmento (`fragColor.a = vColor.a * texDiffuse.a * alpha`),
                ' cada uno con lo que realmente se envio, mas el estado REAL del sampler del diffuse leido del
                ' driver. Es lo unico que discrimina cual de los tres cambia entre plegado y no plegado.
                ' El sampler se lee por DSA (GetTextureParameter con el NOMBRE de textura): bindear para
                ' consultarlo alteraria el estado del propio draw que se esta midiendo.
                Dim vcShow = shape.ShowVertexColor
                Dim vcData = hasVertexColorData
                Dim vcTree = isTreeAnim
                Dim dId = CInt(material.DiffuseTexture_ID)
                Dim dMin = TexParamOrMinus1(dId, GetTextureParameter.TextureMinFilter)
                Dim dMax = TexParamOrMinus1(dId, GetTextureParameter.TextureMaxLevel)
                Logger.LogLazy(Function() $"[DRAW-STATE] shape='{shpD}' idx={MeshData.Idx} blend={hasAlphaBlend}({blendPair(0)},{blendPair(1)}) test={testSubido} thr=0x{BitConverter.SingleToInt32Bits(umbralSubido):X8} ({umbralSubido * 255.0F:F3}/255) matAlpha={materialBase.Alpha:F3} depthWrite={writeDepth} | tex D={dId} N={material.NormalTexture_ID} inner={material.InnerLayerTexture_ID} | vColor: show={vcShow} data={vcData} tree={vcTree} ⇒ bShowVertexColor={vcShow AndAlso vcData} bShowVertexAlpha={vcShow AndAlso vcData AndAlso Not vcTree} | sampler D: minFilter={dMin} maxLevel={dMax} | hair={materialBase.Hair} spec={materialBase.SpecularEnabled}x{materialBase.SpecularMult:F2} gloss={materialBase.NifGlossiness:F2} foldedKey='{material.SseFoldedDiffuseKey}'")
            End If
        End Sub

        ''' <summary>A lighting shape's G-buffer draw (propuesta v3 E2/E4; Tools/re-docs/RE_FO4_DEFERRED_FRAME_2026-10-03.md 2.3):
        ''' <list type="bullet">
        ''' <item>Write mode: technique bit 15 writes RGB (SetupTechnique 0x142203646..0x14220369D), except bit 15 with a blending
        ''' NiAlphaProperty and bit 17 HAIR, RGBA (SetupGeometry 0x14220738B..0x142207428); one mode for RT 0x1A..0x1F. The app's shadow
        ''' RT (attachment 5) is written whole and never blends (propuesta B).</item>
        ''' <item>Blend: SetupAlphaBlend 0x1422301E0 (Fo4SetupAlphaBlendMode), not called when technique AND 0x1000020 = 0x1000000
        ''' (0x142207116..0x142207120: the list's state, no blend); property bit 49 = mode 6 (0x14220784E..0x14220789C).</item>
        ''' <item>The record's constants by name (Fo4GBufferConstants) and the envmap slice registered inside this draw (rev-07).</item>
        ''' <item>The frame's shadow uniforms on this program (PreviewModel.EnsureFrameShadowUniforms).</item>
        ''' </list></summary>
        Private Sub ApplyFo4GBufferDraw(shader As Shader_Base_Class, material As MaterialData, d As Fo4GBufferDraw)
            Dim mb = material.MaterialBase
            Dim ea = mb.ResolveEngineAlpha()
            Dim t = d.Technique
            Dim bit = Function(v As ULong, n As Integer) ((v >> n) And 1UL) <> 0UL
            Dim rgbOnly = (t And (1UI << 15)) <> 0UI AndAlso Not (ea.Present AndAlso ea.Blend AndAlso (t And (1UI << 17)) <> 0UI)
            For rt = 0 To 4 : GL.ColorMask(rt, True, True, True, Not rgbOnly) : Next
            GL.ColorMask(5, True, True, True, True)
            Dim mode = Fo4GBufferBlendMode(material, d)
            If mode = 0 Then
                GL.Disable(EnableCap.Blend)
            Else
                GL.Enable(EnableCap.Blend)
                Dim m = Fo4BlendModeFactors(If(mode = 6, 1, mode))
                For rt = 0 To 4
                    GL.BlendFuncSeparate(rt, CType(m.ColorSrc, BlendingFactorSrc), CType(m.ColorDst, BlendingFactorDest),
                                         CType(m.AlphaSrc, BlendingFactorSrc), CType(m.AlphaDst, BlendingFactorDest))
                Next
                ' Mode 6 (0x1418552AA): RT0 DEST_COLOR / ZERO, alpha ONE / ZERO; RT1..7 SRC_ALPHA / INV_SRC_ALPHA.
                If mode = 6 Then GL.BlendFuncSeparate(0, BlendingFactorSrc.DstColor, BlendingFactorDest.Zero, BlendingFactorSrc.One, BlendingFactorDest.Zero)
                GL.Disable(IndexedEnableCap.Blend, 5)
                If GateFo4BlendRt5 Then GL.Enable(IndexedEnableCap.Blend, 5)
            End If
            shader.SetInt("uCoverageMode", 0)

            ' The record's constants (BSDFPrePassShader::SetupGeometry 0x142205540 / SetupMaterial 0x142204140), read from the engine's
            ' material of this draw (Fo4EngineMaterial: the applicator's or LoadBinary's, then the fixer's).
            Dim em = d.Material
            shader.SetVector4("SpecularParam", Fo4GBufferConstants.SpecularParam(t, d.Flags, em.Smoothness, em.SpecularMult,
                                                                                 em.Backlight, em.Rolloff))
            shader.SetVector4("EmissiveColor", Fo4GBufferConstants.EmissiveColor(em.EmissiveColor, em.EmissiveMult, t, ea.Present AndAlso ea.Blend, ea.Threshold))
            shader.SetVector4("AlphaScale", Fo4GBufferConstants.AlphaScale(t, em.Alpha, ea.Present AndAlso ea.Blend))
            Dim tone = material.Fo4SkinTone()
            shader.SetVector4("TintColor", Fo4GBufferConstants.TintColor(t, tone.Linear, tone.Alpha, tone.Present, mb.GrayscaleToPaletteScale))
            Dim w = em.Wetness
            shader.SetVector4("SpecularParam2", Fo4GBufferConstants.SpecularParam2(t, w(1), w(0)))
            shader.SetVector4("WetnessControl_Spec", Fo4GBufferConstants.WetnessControlSpec(t, em.Feature = 1, w(0), w(1), w(2), w(5), em.Ssr, em.WetSsr))
            ' The envmap slice: registered INSIDE this draw (rev-07), only with property bit 7 (ENV 2.3).
            Dim slice = If(bit(d.Flags, 7), Me.ParentModel.ParentControl.Fo4EnvMap.Register(material.EnvmapTexture()), -1)
            shader.SetVector4("CubeMapIdxR_EnvmapScaleG", Fo4GBufferConstants.CubeMapIdxEnvmapScale(d.Flags, slice, em.MaterialD0, w(3)))
            shader.SetVector4("cb12_30", Vector4.Zero)
            ' ADDITIONAL_ALPHA_MASK (0x14220714B..0x142207340 and 0x1422072B8..0x1422072D9): the constant and t15 together.
            If Fo4GBufferConstants.HasAdditionalAlphaMask(t) Then
                shader.SetVector4("AdditionalAlphaMaskRef", Fo4GBufferConstants.AdditionalAlphaMaskRef(d.Flags, em.Alpha))
                shader.BindTexture("t15", Me.ParentModel.ParentControl.defaultDissolvePatternTex, TextureUnit.Texture15)
            End If
            ' FACE (technique bit 31): t8 = the Face class's +0xC0, texture-set slot 5 raw (0x1421D168A..697), bound by SetupMaterial
            ' 0x142204EB0..FCC; empty = DefaultTexture_NormalMap (EngineDefaultTextureLaw); absent from the data: not traced (C4 L7).
            If (t And Fo4GBufferTechnique.TechFace) <> 0UI Then
                shader.BindTexture("t8", CInt(material.EngineSlotTextureId(EngineDefaultTextureLaw.MaterialSlot.FaceSlot5, material.WrinklesTexture_ID)), TextureUnit.Texture8)
            End If
            Me.ParentModel.EnsureFrameShadowUniforms(shader)
            shader.Use()
        End Sub

        ''' <summary>SetupAlphaBlend 0x1422301E0 (Tools/re-docs/RE_BGEM_BLENDMODES_FO4_2026-10-03.md 3.2): without a blending
        ''' NiAlphaProperty, mode 1 when the pass alpha is below 1, else 0; with one, only seven (src, dst) pairs blend - (6,7) 1;
        ''' (6,0) (0,0) (6,9) 2; (1,2) (4,1) 3; (0,7) 4 - and every other pair is opaque (0x14223021A..0x1422302EC).</summary>
        Friend Shared Function Fo4SetupAlphaBlendMode(present As Boolean, blendBit As Boolean, src As NiflySharp.Enums.AlphaFunction,
                                                      dst As NiflySharp.Enums.AlphaFunction, passAlpha As Single) As Integer
            If Not (present AndAlso blendBit) Then Return If(passAlpha < 1.0F, 1, 0)
            Dim s = CInt(src), d = CInt(dst)
            Select Case True
                Case s = 6 AndAlso d = 7 : Return 1
                Case (s = 6 AndAlso d = 0) OrElse (s = 0 AndAlso d = 0) OrElse (s = 6 AndAlso d = 9) : Return 2
                Case (s = 1 AndAlso d = 2) OrElse (s = 4 AndAlso d = 1) : Return 3
                Case s = 0 AndAlso d = 7 : Return 4
                Case Else : Return 0
            End Select
        End Function

        ''' <summary>The D3D11 blend of FO4's modes 0..4 (table 0x141855180, op ADD): 1 SRC_ALPHA / INV_SRC_ALPHA; 2 SRC_ALPHA / ONE;
        ''' 3 colour DEST_COLOR / ZERO, alpha ONE / ZERO; 4 ONE / INV_SRC_ALPHA; 0 off.</summary>
        Friend Shared Function Fo4BlendModeFactors(mode As Integer) As (Enabled As Boolean, ColorSrc As BlendingFactor, ColorDst As BlendingFactor, AlphaSrc As BlendingFactor, AlphaDst As BlendingFactor)
            Select Case mode
                Case 1 : Return (True, BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha, BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha)
                Case 2 : Return (True, BlendingFactor.SrcAlpha, BlendingFactor.One, BlendingFactor.SrcAlpha, BlendingFactor.One)
                Case 3 : Return (True, BlendingFactor.DstColor, BlendingFactor.Zero, BlendingFactor.One, BlendingFactor.Zero)
                Case 4 : Return (True, BlendingFactor.One, BlendingFactor.OneMinusSrcAlpha, BlendingFactor.One, BlendingFactor.OneMinusSrcAlpha)
                Case Else : Return (False, BlendingFactor.One, BlendingFactor.Zero, BlendingFactor.One, BlendingFactor.Zero)
            End Select
        End Function

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

        ''' <summary>Exporta la malla a un OBJ. ⛔ HOY NO TIENE CONSUMIDOR fuera de este ensamblado (única
        ''' referencia: su propia declaración), y aun así se migró a la ley de escritura en vez de dejarle
        ''' un comentario: el día que alguien la cablee, el defecto ya no está. Un `StreamWriter(ruta)`
        ''' pide CREATE_ALWAYS —ACCESS_DENIED sobre un destino OCULTO, y un archivo nuevo rompe el VFS de
        ''' MO2 / el hardlink de Vortex—. La ley vive en `Ba2_Bsa_Library\EscrituraEnElLugar.vb`.
        ''' <para>`Escribir` y no `GuardarConCopia`: un OBJ exportado es salida REGENERABLE (se vuelve a
        ''' exportar de la malla, que no se toca).</para>
        ''' <para>Se serializa a memoria y se vuelca el buffer, en vez de envolver el stream del cuerpo en
        ''' un `StreamWriter`: así la trampa del `leaveOpen` que documenta la ley ni se presenta.</para>
        ''' </summary>
        Friend Sub ExportMeshToOBJ(rutaArchivo As String)
            Using msObj As New MemoryStream()
                Using sw As New StreamWriter(msObj, New UTF8Encoding(False), 1024, leaveOpen:=True)

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
                Dim objBytes = msObj.ToArray()
                BSA_BA2_Library_DLL.EscrituraEnElLugar.Escribir(
                    rutaArchivo, Sub(fs) fs.Write(objBytes, 0, objBytes.Length))
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
        ' EL ORDEN DE `meshes` ES EL ORDEN DE `shapes`, Y NO ES COSMETICO. Aca habia un
        ' `ConcurrentBag(Of RenderableMesh)` con `Parallel.ForEach` + `AddRange`, y un ConcurrentBag NO
        ' TIENE ORDEN: su enumeracion depende de en que hilo termino cada shape. O sea que dos cargas de
        ' la MISMA escena producian dos ordenes de dibujo distintos.
        ' Eso importa porque EL ALPHA BLENDING NO ES CONMUTATIVO: dos shapes translucidas superpuestas
        ' —el pelo sobre la cabeza es el caso de todos los dias— dan un color distinto segun cual se
        ' dibuje primero.
        ' MEDIDO, no razonado: el A/A de recarga de Tools/ShadowGate encontro que re-extraer la misma
        ' geometria cambiaba 11 px de 648.000 en ~3 de cada 8 recargas, siempre los mismos, siempre en el
        ' bbox (332,75)-(387,152) = la silueta del pelo, con delta de canal 88. Se descarto el recalculo de
        ' TBN (pasa igual apagado) y se confirmo registrando la secuencia de nombres: el orden cambiaba.
        ' Consecuencia para el usuario: el preview podia dibujarse distinto en cada carga sin que nada
        ' cambiara, que es exactamente lo que vuelve irreproducible un reporte de bug.
        Dim lista = LoadedShapes
        Dim porIndice(lista.Count - 1) As RenderableMesh
        Parallel.For(0, lista.Count, Sub(i) porIndice(i) = LoadShapeSafe(lista(i), resolver))
        ' `LoadShapeSafe` devuelve Nothing para una shape que no se pudo cargar: esos huecos se saltean, y
        ' las que si cargaron conservan su posicion relativa.
        For i = 0 To porIndice.Length - 1
            If porIndice(i) IsNot Nothing Then meshes.Add(porIndice(i))
        Next
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
#If DEBUG Then
            Debugger.Break()
#End If
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

    ''' <summary>Saca <paramref name="path"/> del diccionario tras una subida FALLIDA, LIBERANDO el handle GL.
    ''' Borrar la entrada a secas deja el nombre de GL huerfano —fuera del diccionario, asi que
    ''' <c>CleanTextures</c> no lo encuentra nunca mas—: no es memoria reciclable, es VRAM perdida hasta que
    ''' muere el proceso.
    ''' <para>NO TOCA una entrada que sea del COMPOSITOR. Es la misma ley que
    ''' <c>NpcFaceTintResolver.InstallTexture</c>: el gate es <see cref="PreviewModel.Texture_Loaded_Class.OwnedByComposer"/>
    ''' y no "id &lt;&gt; 0". Este helper lo llama el camino de fallo del LOADER, y el loader puede fallar sobre
    ''' una clave que el compositor ya repinto: el editor de cara encola el path de la cara, el usuario mueve
    ''' un slider, <c>NpcSkinLivePreview</c> instala la textura compuesta en esa misma clave, y recien ahi
    ''' vuelve el lote con error. Borrar ahi mata la textura VIVA del preview, y como
    ''' <c>DirectXDDSLoader</c> marca fallidas TODAS las entradas del lote cuando revienta una sola, un DDS
    ''' malo se lleva varias. <c>GenTexture</c> recicla el nombre y otro sampler pasa a leer pixeles ajenos,
    ''' sin un solo error de GL.</para>
    ''' <para>La entrada del compositor es la AUTORIDAD: un upload del loader que fallo sobre esa clave ya no
    ''' es relevante, asi que se deja como esta. La que se limpia es la del loader, donde el handle no lo
    ''' conserva nadie mas (el diccionario tiene UNA entrada por path).</para></summary>
    Private Sub OlvidarTexturaLiberandoHandle(path As String)
        Dim previa As PreviewModel.Texture_Loaded_Class = Nothing
        If Textures_Dictionary.TryGetValue(path, previa) AndAlso previa IsNot Nothing Then
            If previa.OwnedByComposer Then
                Dim cId = previa.Texture_ID, cP = path
                Logger.LogLazy(Function() $"[AUDIT-ORPHAN] subida fallida sobre '{cP}', pero la clave la tiene el COMPOSITOR (handle {cId}): NO se toca")
                Return                              ' ni se borra el handle ni se saca la entrada
            End If
            If previa.Texture_ID <> 0 Then
                Dim aId = previa.Texture_ID, aP = path
                Logger.LogLazy(Function() $"[AUDIT-ORPHAN] subida fallida sobre una clave con textura viva del loader: se libera el handle {aId} de '{aP}'")
                Try : previa.ReleaseViews() : GL.DeleteTexture(previa.Texture_ID) : Catch : End Try
            End If
        End If
        Textures_Dictionary.Remove(path)
    End Sub

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
                                old.ReleaseViews()
                                GL.DeleteTexture(old.Texture_ID)
                            End If
                            ' DIAGNOSTICO (Logger.Enabled): QUE ARCHIVO quedo detras de CADA nombre de textura GL.
                            ' Los nombres se RECICLAN: GenTexture devuelve el menor libre, y cualquier DeleteTexture
                            ' previo (el compose del pliegue borra sus intermedios) libera nombres bajos. Sin este
                            ' mapeo no se puede distinguir "otro id" de "otra imagen", que son dos bugs distintos.
                            If Logger.Enabled Then
                                Dim pth = path, nid = result.Texture_ID
                                Dim oid = If(old Is Nothing, 0UI, old.Texture_ID)
                                Dim sz = result.Size, srgb = result.IsSRGB
                                Dim mx As Integer = -1
                                Try
                                    GL.GetTextureParameter(CInt(nid), GetTextureParameter.TextureMaxLevel, mx)
                                    If GL.GetError() <> ErrorCode.NoError Then mx = -1
                                Catch
                                    mx = -1
                                End Try
                                Dim mxv = mx
                                Logger.LogLazy(Function() $"[TEX-UPLOAD] id={nid} (prev={oid}) {sz.Width}x{sz.Height} maxLevel={mxv} sRGB={srgb} '{pth}'")
                            End If
                            Textures_Dictionary(path) = result
                            Last_Loaded_Textures.Add(path)
                            _uploadFailureCount.Remove(path)
                        Else
                            OlvidarTexturaLiberandoHandle(path)
                            RegisterUploadFailure(path, "silent")
                        End If
                    Catch ex As Exception
                        Logger.LogLazy(Function() $"[Render] GL upload failed for '{path}': {ex.Message}")
                        OlvidarTexturaLiberandoHandle(path)
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
        ' OpaqueMeshes / DecalMeshes / BlendedMeshes is keyed by Texture_ID at
        ' line 3210 — rebuild on next paint so the new IDs replace the dead handles.
        MarkRenderBucketsDirty()
    End Sub

    Public Sub CleanTextures()
        CancelPendingTextureUploads()

        ' — Eliminar texturas cargadas —
        Dim seen As New HashSet(Of UInteger)
        For Each t In Textures_Dictionary.Values
            t?.ReleaseViews()
        Next
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
            For Each t In Textures_Dictionary.Values.Where(Function(pf) pf.Path.Equals(Cual, StringComparison.OrdinalIgnoreCase))
                t.ReleaseViews()
            Next
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
#If DEBUG Then
            Debugger.Break()
#End If
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
        ' The envmap array is reset per model load (user decision rev-09 b).
        ParentControl.Fo4EnvMap.Reset()
        ' Limpia meshes internamente
        For Each mesh In meshes
            mesh.Clean()
        Next
        ' Borra Meshes
        ' Los casters van con ellos: RenderAll sale antes del pase de sombra con la escena vacia, asi que
        ' esta lista es lo unico que quedaria referenciando las mallas del NPC anterior.
        _shadowCasters.Clear()
        _shadowActive = False
        _shadowCount = 0
        _groundActive = False
        _groundCount = 0
        meshes.Clear()
        OpaqueMeshes.Clear()
        DecalMeshes.Clear()
        DecalBlendedMeshes.Clear()
        BlendedMeshes.Clear()
        BlendedDepthBuffer.Clear()
        MarkRenderBucketsDirty()

        Dim i = 0
        While GL.GetError() <> ErrorCode.NoError
            i += 1
            If i > 10 Then
#If DEBUG Then
                Debugger.Break()
#End If
                Exit While
            End If
        End While
    End Sub

    Structure MeshDepth
        Public Mesh As RenderableMesh
        ''' <summary>Nothing = the mesh's own draw; else one of its overlay layers.</summary>
        Public Layer As OverlayMaterialLayer
        Public Depth As Single
        ''' <summary>Insertion order: the tie-break of the stable back-to-front sort.</summary>
        Public Seq As Integer
    End Structure

    ''' <summary>El rig de luces ya resuelto a uniforms: 4 colores linealizados (pow 2.2) + el ambient
    ''' hemisférico + las 4 direcciones derivadas de la cámara. Es lo que ApplyMaterial sube tal cual.</summary>
    Friend Structure LightRigUniforms
        Public AmbientSky As Vector3
        Public AmbientGround As Vector3
        Public KeyDiffuse As Vector3, KeyDir As Vector3
        Public Fill0Diffuse As Vector3, Fill0Dir As Vector3
        Public Fill1Diffuse As Vector3, Fill1Dir As Vector3
        Public BackDiffuse As Vector3, BackDir As Vector3

        ''' <summary>Direccion NETA del rig (unitaria, mundo) y cuan DIRECCIONAL es (0..1). Las calcula
        ''' <c>ResolveFrameLights</c> sobre estas mismas luces resueltas; las consume el fondo radial.
        ''' <para>0 = las luces se cancelan entre si o el ambiente las tapa (rig plano) ⇒ el fondo queda
        ''' centrado. 1 = una sola luz y nada de ambiente ⇒ doblez maximo. No hay constante en el medio:
        ''' el numero ES el rig.</para></summary>
        Public NetDir As Vector3
        Public Directionality As Single

        ''' <summary>La direccion de la luz i en el ORDEN CANONICO de ShadowMapMath.LuzDelRig
        ''' (0 key, 1 fill izq, 2 fill der, 3 back). Devuelve la direccion YA RESUELTA de este frame
        ''' —o sea con follow-camera aplicado si esta prendido—, que es la MISMA que va a los uniforms
        ''' del fragment. Tomarla del rig crudo permitiria que la sombra se proyecte desde una direccion
        ''' y la luz venga de otra, y con `Setting_LightsFollowCamera` en True (el default) eso pasaria
        ''' en cuanto el usuario orbite.</summary>
        Friend Function DirDeLuz(i As Integer) As Vector3
            Select Case i
                Case 0 : Return KeyDir
                Case 1 : Return Fill0Dir
                Case 2 : Return Fill1Dir
                Case Else : Return BackDir
            End Select
        End Function

        ''' <summary>El difuso LINEAL de la luz i, mismo orden.</summary>
        Friend Function DifusoDeLuz(i As Integer) As Vector3
            Select Case i
                Case 0 : Return KeyDiffuse
                Case 1 : Return Fill0Diffuse
                Case 2 : Return Fill1Diffuse
                Case Else : Return BackDiffuse
            End Select
        End Function
    End Structure


    ''' <summary>Rig resuelto para el frame en curso. Lo llena <see cref="RenderAll"/> antes de dibujar y lo
    ''' consume ApplyMaterial. Se resuelve UNA vez por frame: por malla serian 18 Math.Pow + 4 Direction()
    ''' idénticos, mas otra vuelta por cada overlay layer.
    ''' <para>Depende del rig activo y, SI <c>Setting_LightsFollowCamera</c> está prendido, también de la
    ''' cámara. Ese flag viene en <b>True</b> por default (decisión del usuario), así que orbitar SÍ mueve
    ''' las direcciones salvo que se apague. Con el flag apagado no cambia ni una.</para></summary>
    Private _frameLights As LightRigUniforms

    ''' <summary>La imagen del frame en curso: la elección persistida del juego que dibuja y su fila de la tabla.
    ''' La resuelve RenderScene una vez por frame, antes de decidir el post; la leen el post, ApplyMaterial y el
    ''' piso.</summary>
    Friend ReadOnly Property FrameImaging As (Settings As PreviewImagingSettings, Row As PreviewImagingRow, IsSse As Boolean)

    Friend Sub ResolveFrameImaging(isSse As Boolean)
        Dim s = Config_App.Current.PreviewImaging(isSse)
        Dim row = PreviewImagingTable.Resolve(isSse, s)
        If row Is Nothing OrElse row.IsSse <> isSse Then
            Throw New InvalidOperationException("PreviewImagingTable: no row of the drawing game (imaging-table gate).")
        End If
        _FrameImaging = (s, row, isSse)
    End Sub

    Friend ReadOnly Property FrameLights As LightRigUniforms
        Get
            Return _frameLights
        End Get
    End Property

    ''' <summary>Lleva una dirección del marco del RIG al marco del MUNDO usando la base de la cámara.
    ''' <para>NO HACE FALTA CONVERTIR LOS PRESETS, y esa es la razón por la que esto entra sin tocar nada
    ''' más: la base de <see cref="OrbitCamera"/> en la vista por defecto (angleX = angleY = 0) es
    ''' EXACTAMENTE la del mundo — <c>right = (1,0,0)</c>, <c>Forward = (0,1,0)</c>,
    ''' <c>upPlane = (0,0,1)</c>, ver UpdateDirectionFromAngles— que es la misma base en la que están
    ''' autorados los presets. O sea que un preset significa lo mismo en los dos modos mientras no
    ''' orbites.</para>
    ''' <para>`Forward` de la cámara apunta del foco HACIA el ojo (`eye = Focus + Forward*distance`), y
    ''' <c>Direction()</c> devuelve superficie→luz. Los dos van en el mismo sentido, así que el componente Y
    ''' del rig es "luz desde donde mira el observador" en los dos marcos. No hay que invertir nada.</para>
    ''' </summary>
    Private Shared Function ADireccionDeCamara(d As Vector3, cam As OrbitCamera) As Vector3
        Return cam.right * d.X + cam.Forward * d.Y + cam.upPlane * d.Z
    End Function

    ''' <summary>Deja <c>FrameLights</c> al dia para ESTE frame. Existe porque el fondo direccional se
    ''' dibuja ANTES de <c>RenderAll</c> y necesita la direccion de la key del frame en curso: sin esto
    ''' leeria la del frame anterior y el punto claro llegaria un frame tarde mientras se orbita.
    ''' <para>Es PURA e idempotente —recalcula del rig y la camara—, asi que la llamada que <c>RenderAll</c>
    ''' hace un instante despues da lo mismo. Se expone en vez de duplicar la ley del follow-camera en el
    ''' camino del fondo, que es lo que habria que hacer si no.</para></summary>
    Friend Sub EnsureFrameLights(cam As OrbitCamera)
        ResolveFrameLights(cam)
    End Sub

    Private Sub ResolveFrameLights(cam As OrbitCamera)
        ' El rig sale de ActiveLights() = el set del JUEGO activo (FO4/SSE tienen el suyo).
        Dim rig = Config_App.Current.ActiveLights()
        ' LA RAMA APAGADA NO EJECUTA NADA NUEVO. No es `ADireccionDeCamara` con una base identidad: con la
        ' base identidad la cuenta es `d.X*1 + d.Y*0 + d.Z*0`, que SUMA CEROS y convierte un -0,0 en +0,0.
        ' Este repo ya se comió esa exacta trampa con ParentGlobalTransform. Con el If, el default es
        ' bit-idéntico por construcción y no hay nada que verificar.
        Dim seguir As Boolean = Config_App.Current.Setting_LightsFollowCamera AndAlso cam IsNot Nothing
        Dim kd = rig.KeyLight.Direction()
        Dim f0 = rig.FillLeft.Direction()
        Dim f1 = rig.FillRight.Direction()
        Dim bd = rig.BackLight.Direction()
        If seguir Then
            kd = ADireccionDeCamara(kd, cam)
            f0 = ADireccionDeCamara(f0, cam)
            f1 = ADireccionDeCamara(f1, cam)
            bd = ADireccionDeCamara(bd, cam)
        End If
        ' EL NIVEL DE SUELO MULTIPLICA DESPUES DEL POW, y no es un detalle de orden: el tinte es un COLOR
        ' (se autora en perceptual y se decodea al subir) pero el nivel es un COCIENTE DE RADIANCIAS entre
        ' los dos hemisferios, y el mix() del shader que lo consume opera en lineal. Adentro del pow la
        ' perilla entregaba nivel^2.2 — el 0,45 del Studio valia 17,3 % del cielo, no 45 %.
        ' Ver PreviewLightRig.AmbientGroundLevel para la medicion que lo destapo.
        ' (El comentario va ACA y no en el inicializador: una linea de comentario entre dos miembros de un
        '  `With {}` corta la continuacion implicita y el parser tira BC30370.)
        ' The rig is authored in perceptual (display) values; FO4's pipeline is linear (decode them), SSE's is raw
        ' (use them as authored, like every SSE light colour the engine uploads).
        Dim rawRig = ParentControl IsNot Nothing AndAlso ParentControl.CurrentShader IsNot Nothing AndAlso
                     ParentControl.CurrentShader Is ParentControl.SharedSSEShader
        Dim lc = Function(v As Vector3) If(rawRig, v, Shader_Base_Class.Vector_to_Linear(v))
        _frameLights = New LightRigUniforms With {
            .AmbientSky = lc(rig.AmbientSkyDiffuse()),
            .AmbientGround = lc(rig.AmbientGroundDiffuse()) * rig.AmbientGroundLevel,
            .KeyDiffuse = lc(rig.KeyLight.Diffuse()),
            .KeyDir = kd,
            .Fill0Diffuse = lc(rig.FillLeft.Diffuse()),
            .Fill0Dir = f0,
            .Fill1Diffuse = lc(rig.FillRight.Diffuse()),
            .Fill1Dir = f1,
            .BackDiffuse = lc(rig.BackLight.Diffuse()),
            .BackDir = bd
        }

        ' DIRECCIONALIDAD DEL RIG, para el fondo radial. Es el mismo gesto que ya hace el piso con
        ' `floorExposure`: resumir el rig en un escalar para decidir la apariencia de una superficie. Y usa
        ' la MISMA luma Rec.709 (Shader_Base_Class.Luma), sobre los difusos LINEALES, que es lo que el piso
        ' promedia en `upLuma`.
        '   d = |suma de las direcciones pesadas por luma| / (esa luma total + la del ambiente)
        ' Cuatro luces simetricas se cancelan y dan ~0; una sola luz sin ambiente da 1. El ambiente entra
        ' SOLO en el denominador porque es omnidireccional: cuanto mas hay, mas plano es el rig y menos
        ' tiene que doblarse el fondo.
        Dim wK = Shader_Base_Class.Luma(_frameLights.KeyDiffuse)
        Dim w0 = Shader_Base_Class.Luma(_frameLights.Fill0Diffuse)
        Dim w1 = Shader_Base_Class.Luma(_frameLights.Fill1Diffuse)
        Dim wB = Shader_Base_Class.Luma(_frameLights.BackDiffuse)
        Dim neto As Vector3 = kd * wK + f0 * w0 + f1 * w1 + bd * wB
        Dim total As Single = wK + w0 + w1 + wB +
                              Shader_Base_Class.Luma(_frameLights.AmbientSky) +
                              Shader_Base_Class.Luma(_frameLights.AmbientGround)
        Dim mag As Single = neto.Length
        _frameLights.NetDir = If(mag > 0.000001F, neto / mag, Vector3.Zero)
        _frameLights.Directionality = If(total > 0.000001F, Math.Clamp(mag / total, 0.0F, 1.0F), 0.0F)
    End Sub

    ' ===================== SOMBRAS =====================
    ' Estado del shadow map de ESTE frame. Lo llena RenderShadowPass antes de cualquier draw iluminado y
    ' lo consume ApplyMaterial, que es quien sube los uniforms. Igual que _frameLights: se resuelve una
    ' vez por frame y depende solo de (rig activo, camara, geometria).
    ''' <summary>Encuadre de CADA capa del mapa del personaje, indexado por CAPA (no por luz).</summary>
    Private ReadOnly _shadowFits(PreviewShadowSettings.MaxShadowLights - 1) As ShadowMapMath.LightFit
    ''' <summary>Luz del rig -> capa, o -1. El fragment lo indexa por LUZ. Orden canonico:
    ''' ShadowMapMath.LuzDelRig.</summary>
    Private ReadOnly _shadowSlots(PreviewShadowSettings.MaxShadowLights - 1) As Integer
    ''' <summary>Cuantas capas tiene el mapa del personaje este frame. 0 = ninguna luz castea.</summary>
    Private _shadowCount As Integer
    Private _shadowSettings As PreviewShadowSettings
    ''' <summary>El LOOK de la sombra, copiado del RIG al frame (ver RenderShadowPass). Vive alla y no en
    ''' PreviewShadowSettings porque blandura y oscuridad son parte del set de luces, no del presupuesto
    ''' de maquina.</summary>
    Private _shadowSoftness As Single
    Private _shadowDarkness As Single
    Private _shadowActive As Boolean
    ' Buffers REUTILIZADOS para subir los uniforms de array. Campos y no locales por la misma razon que
    ' _shadowCasters y _shadowPlanes: esto corre en cada frame que se repinta.
    Private ReadOnly _bufViewProj(PreviewShadowSettings.MaxShadowLights * 16 - 1) As Single
    Private ReadOnly _bufDepthBias(PreviewShadowSettings.MaxShadowLights - 1) As Single
    Private ReadOnly _bufContrib(PreviewShadowSettings.MaxShadowLights * 3 - 1) As Single
    ''' <summary>Escala de UV por capa del mapa del PERSONAJE. Es 1.0 siempre —ese mapa ocupa la capa
    ''' entera—; existe para que el uniform se suba con la misma ruta que el del suelo y no haya dos
    ''' caminos, uno de los cuales se olvidaria de actualizar el dia que el personaje tambien reserve
    ''' de mas.</summary>
    Private ReadOnly _shadowUvScale(PreviewShadowSettings.MaxShadowLights - 1) As Single
    ''' <summary>Buffer REUTILIZADO de casters: el pase corre en cada frame que se repinta, asi que una
    ''' List nueva por frame es basura de GC en el camino de dibujo — el mismo motivo por el que
    ''' BlendedDepthBuffer es un campo y no un local.
    ''' <para>Se limpia en <see cref="Clean"/>. RenderAll sale antes de RenderShadowPass cuando la escena
    ''' queda vacia, asi que sin eso la lista seguia referenciando cada RenderableMesh del ultimo NPC —y con
    ''' ellos su MeshData y su SkinnedGeometry— por toda la vida del control. Con la List local de antes
    ''' morian con el frame; convertirla en campo para no alocar por frame trajo esto de regalo.</para>
    ''' </summary>
    Private ReadOnly _shadowCasters As New List(Of RenderableMesh)
    ''' <summary>Los 6 planos del frustum de la LUZ, reusados. Mismo motivo que _shadowCasters: el pase
    ''' corre por frame y por mapa, y los planos son constantes dentro de cada pase.</summary>
    Private ReadOnly _shadowPlanes(5) As Vector4
    ''' <summary>Los 6 planos del frustum de la CAMARA, reusados por los cinco bucles de RenderAll. Mismo
    ''' motivo que <see cref="_shadowPlanes"/>: la sobrecarga que toma una Matrix4 aloca un Vector4(5) por
    ''' llamada, o sea por malla y por bucket.</summary>
    Private ReadOnly _framePlanes(5) As Vector4
    ''' <summary>Sube lo que recibe un plano de normal +Z: el TOTAL y el aporte de cada capa casteante.
    '''
    ''' <para>NO ES UNA CONSTANTE ELEGIDA A OJO, y esa es la diferencia entre una sombra y una calcomania.
    ''' Multiplicar por <c>1 - a</c> deja el suelo en NEGRO PURO con Intensity = 1, y ningun suelo en sombra
    ''' es negro: le siguen llegando el ambiente y las luces que no estan bloqueadas EN ESE PIXEL. Aca se
    ''' evalua exactamente eso, con el MISMO rig que esta iluminando al personaje.</para>
    '''
    ''' <para>Y ES POR LUZ, que es lo que lo vuelve correcto con N: el fragment resta el aporte de cada capa
    ''' ocluida y nada mas, asi que la sombra del suelo se COMPONE igual que la del cuerpo y ademas se TINE
    ''' —ocluir una key calida donde llega un fill frio deja el piso azulado—, cosa que un tinte unico no
    ''' puede expresar ni con N mapas.</para>
    '''
    ''' <para>El ambiente que entra es el del hemisferio de ARRIBA porque la normal del plano es +Z, que
    ''' es justo donde <c>hemiAmbient</c> devuelve <c>ambientSky</c> puro. El pow 1/2.2 lo hace el
    ''' fragment, no esta funcion: ver el comentario del GLSL.</para></summary>
    Private Sub SubirAporteDelSuelo(shader As Shader_Base_Class)
        If shader Is Nothing Then Exit Sub
        Dim total As Vector3 = _frameLights.AmbientSky
        For luz = 0 To PreviewShadowSettings.MaxShadowLights - 1
            total += _frameLights.DifusoDeLuz(luz) * Math.Max(_frameLights.DirDeLuz(luz).Z, 0.0F)
        Next
        For capa = 0 To _groundCount - 1
            Dim luz = _groundLuzDeCapa(capa)
            ' Una capa que este frame no califica (su luz esta por debajo de la elevacion minima) existe,
            ' esta limpia y se samplea igual — pero no aporta: su contribucion va en CERO. Asi el termino
            ' que le resta el fragment es nulo pase lo que pase con el lookup.
            Dim c As Vector3 = If(_groundValida(capa),
                                  _frameLights.DifusoDeLuz(luz) * Math.Max(_frameLights.DirDeLuz(luz).Z, 0.0F),
                                  Vector3.Zero)
            _bufContrib(capa * 3 + 0) = c.X
            _bufContrib(capa * 3 + 1) = c.Y
            _bufContrib(capa * 3 + 2) = c.Z
        Next
        shader.SetVector3("uGroundTotal", total)
        shader.SetVector3Array("uGroundContrib[0]", _bufContrib, _groundCount)
        shader.SetInt("uGroundCount", _groundCount)
    End Sub

    ''' <summary>Libera el VAO/VBO del receptor de suelo. Lo llama PreviewControl.Clean junto con el
    ''' Floor, que es donde ya se libera la geometria propia del modelo.</summary>
    Friend Sub DisposeShadowResources()
        If _groundQuad IsNot Nothing Then
            _groundQuad.Dispose()
            _groundQuad = Nothing
        End If
    End Sub

    ''' <summary>Plano del receptor de suelo (Z de mundo) y si este frame lo dibuja.</summary>
    Private _groundZ As Single
    Private _groundActive As Boolean
    ''' <summary>Encuadre del mapa ANCHO por CAPA, a que luz corresponde cada capa, su escala de UV
    ''' (region logica / textura reservada) y si este frame se dibujo de verdad.</summary>
    Private ReadOnly _groundFits(PreviewShadowSettings.MaxShadowLights - 1) As ShadowMapMath.LightFit
    Private ReadOnly _groundLuzDeCapa(PreviewShadowSettings.MaxShadowLights - 1) As Integer
    Private ReadOnly _groundUvScale(PreviewShadowSettings.MaxShadowLights - 1) As Single
    Private ReadOnly _groundValida(PreviewShadowSettings.MaxShadowLights - 1) As Boolean
    Private _groundCount As Integer
    Private _groundQuadCenter As Vector3
    Private _groundQuadHalf As Vector2
    Private _groundQuad As GroundShadowQuad

    ''' <summary>True si este frame tiene un shadow map dibujado y utilizable. False = ApplyMaterial sube
    ''' <c>bShadows = false</c> y el fragment ni calcula el factor.</summary>
    Friend ReadOnly Property ShadowActive As Boolean
        Get
            Return _shadowActive
        End Get
    End Property

    ''' <summary>CRONOMETRO DE GPU DEL PASE DE PROFUNDIDAD. Apagado por default: cuando esta en False no
    ''' se crea ni una query y el camino de dibujo queda exactamente como antes.
    ''' <para>HACE FALTA UNA QUERY DE GL Y NO UN Stopwatch. El pase de profundidad es trabajo de GPU; un
    ''' cronometro de CPU alrededor mide el ENCOLADO de comandos, que no tiene nada que ver con lo que
    ''' cuesta. Y un <c>GL.Finish</c> para forzarlo mediria ademas todo lo que hubiera pendiente de antes.
    ''' <c>TimeElapsed</c> lo mide en la GPU y se lee UN FRAME DESPUES, para no frenar el pipeline.</para>
    ''' <para>Existe para contestar una pregunta concreta: cuanto del costo de la feature es el pase de
    ''' profundidad —que se podria saltear en un frame donde nada que lo alimenta cambio— y cuanto es el
    ''' lookup por fragmento, que se paga siempre. El A/B que alterna `Enabled` los mezcla.</para></summary>
    Friend Shared Property MedirPaseDeProfundidad As Boolean = False

    ''' <summary>Nanosegundos de GPU del ultimo pase de profundidad medido (los DOS mapas). 0 si no hay
    ''' medicion todavia.</summary>
    Friend Shared ReadOnly Property NsPaseDeProfundidad As Long
        Get
            Return _nsPaseProfundidad
        End Get
    End Property

    Private Shared _nsPaseProfundidad As Long
    Private _queryProf As Integer
    Private _queryEnVuelo As Boolean

    ''' <summary>Arranca la query si la medicion esta prendida, y cosecha la del frame anterior.</summary>
    Private Sub AbrirCronometroDeProfundidad()
        If Not MedirPaseDeProfundidad Then Exit Sub
        If _queryProf = 0 Then _queryProf = GL.GenQuery()
        If _queryEnVuelo Then
            ' Un frame de atraso: para este punto el resultado ya esta y no se frena nada.
            Dim listo As Integer = 0
            GL.GetQueryObject(_queryProf, GetQueryObjectParam.QueryResultAvailable, listo)
            If listo <> 0 Then
                Dim ns As Long = 0
                GL.GetQueryObject(_queryProf, GetQueryObjectParam.QueryResult, ns)
                _nsPaseProfundidad = ns
                _queryEnVuelo = False
            Else
                Exit Sub   ' todavia en vuelo: no se puede reusar la misma query
            End If
        End If
        GL.BeginQuery(QueryTarget.TimeElapsed, _queryProf)
    End Sub

    Private Sub CerrarCronometroDeProfundidad()
        If Not MedirPaseDeProfundidad OrElse _queryProf = 0 OrElse _queryEnVuelo Then Exit Sub
        GL.EndQuery(QueryTarget.TimeElapsed)
        _queryEnVuelo = True
    End Sub

    ''' <summary>Encuadre de la capa 0 (la primera luz que castea). Lo consume el arnes.
    ''' <para>Sirve como representante para TexelWorld y DepthRange porque los dos son iguales en todas
    ''' las capas: el extent sale de la esfera envolvente, que no depende de la direccion de la luz.</para></summary>
    Friend ReadOnly Property ShadowFit As ShadowMapMath.LightFit
        Get
            Return _shadowFits(0)
        End Get
    End Property

    ''' <summary>Encuadre de una capa concreta. Para el arnes: verificar que cada luz encuadra desde SU
    ''' direccion y no desde la de la key.</summary>
    Friend ReadOnly Property ShadowFitDeCapa(capa As Integer) As ShadowMapMath.LightFit
        Get
            If capa < 0 OrElse capa >= PreviewShadowSettings.MaxShadowLights Then Return Nothing
            Return _shadowFits(capa)
        End Get
    End Property

    ''' <summary>Cuantas luces castean este frame, y a que capa fue cada una.</summary>
    Friend ReadOnly Property ShadowCount As Integer
        Get
            Return _shadowCount
        End Get
    End Property

    Friend ReadOnly Property ShadowSlotDeLuz(luz As Integer) As Integer
        Get
            If luz < 0 OrElse luz >= PreviewShadowSettings.MaxShadowLights Then Return -1
            Return _shadowSlots(luz)
        End Get
    End Property

    ''' <summary>Encuadre del mapa ANCHO del suelo, y si este frame lo dibujo. Los consume el arnes
    ''' Tools/ShadowGate para verificar que prender el suelo NO le cambia el encuadre al personaje.</summary>
    Friend ReadOnly Property GroundFit As ShadowMapMath.LightFit
        Get
            Return _groundFits(0)
        End Get
    End Property

    Friend ReadOnly Property GroundActive As Boolean
        Get
            Return _groundActive
        End Get
    End Property

    Friend ReadOnly Property ShadowSettings As PreviewShadowSettings
        Get
            Return _shadowSettings
        End Get
    End Property

    ''' <summary>Suelta la VRAM de los dos shadow maps. Se llama desde CADA salida temprana del pase:
    ''' con la feature apagada —por la opcion, por falta de shader, por falta de casters o por un encuadre
    ''' degenerado— no queda un byte de GPU reservado.</summary>
    Private Sub SoltarMapasDeSombra()
        If ParentControl Is Nothing Then Exit Sub
        ParentControl.ShadowTarget?.Release()
        ParentControl.GroundShadowTarget?.Release()
    End Sub

    ''' <summary>Dibuja una capa de shadow map POR CADA LUZ QUE CASTEE. Cualquier salida temprana deja <c>_shadowActive</c> en
    ''' False, o sea el frame se dibuja exactamente como antes de que existiera esta feature — nunca a
    ''' medias contra un mapa viejo.
    '''
    ''' <para>ESTADO GL: este pase cambia framebuffer, viewport, culling y depth. Los devuelve TODOS
    ''' antes de salir. El viewport se restaura de <c>lastW/lastH</c> y el framebuffer del 0: los dos son
    ''' conocidos (RenderAll solo se alcanza desde RenderScene, que dibuja contra el default) y leerlos del
    ''' driver costaba un glGet por frame. Si algo de esto se escapa, el frame entero sale escalado o sin
    ''' depth y el sintoma no apunta para aca.</para></summary>
    Private Sub RenderShadowPass()
        _shadowActive = False
        _shadowCount = 0
        _groundActive = False
        _groundCount = 0
        If ParentControl Is Nothing Then Exit Sub

        Dim cfg = Config_App.Current.ActiveShadows().Sanitized()
        ' SOLTAR LA VRAM EN *TODOS* LOS CAMINOS DE APAGADO, no solo en el de la opcion del suelo. El
        ' criterio "con la sombra apagada no se asigna un byte de GPU" tiene CINCO salidas —opcion apagada,
        ' sin shader, sin casters, encuadre invalido y fallo de Ensure— y liberar en una sola es no
        ' cumplirlo: prender sombras + suelo y despues DESTILDAR sombras deja los dos mapas colgados hasta el
        ' Clean. Los dos arrays se reservan al MISMO lado y con las MISMAS capas, asi que a 2048 con una sola
        ' luz son 16 + 16 = 32 MB, con las cuatro 128 MB, y a 4096 con las cuatro 512 MB. Ver el cartel de VRAM
        ' del dialogo, que hace exactamente esta cuenta.
        If Not cfg.Enabled Then SoltarMapasDeSombra() : Exit Sub

        Dim depthShader = ParentControl.CurrentShadowShader
        If depthShader Is Nothing Then SoltarMapasDeSombra() : Exit Sub

        ' CASTERS. El gate es CastShadows del material resuelto, que ya es game-aware (bit 9 de SF1 en
        ' FO4 y en SK; y en FO4 el BGSM PISA al NIF, ver 30-fo4-material-vs-nif). Sobre el corpus vanilla
        ' ese flag ya hace el filtrado correcto solo: medido sobre los 6616 BGSM de Fallout4 -
        ' Materials.ba2, los materiales de actor alpha-blend que traen CastShadows=False son EXACTAMENTE
        ' eyelashes, eyewet, eyestearduct, stubble, el pelo *_8bit, beard_8bit* y synthtattoo — o sea los
        ' que proyectarian una barra negra sobre el ojo o un bloque solido en vez de mechones. Sus gemelos
        ' *_1bit (cutout) SI lo traen en True. Por eso no hay excepcion por bucket: alcanza el flag.
        ' NO HAY OVERRIDE DE ESTE FILTRO: una opcion "ignorar el flag del material" para ver la sombra de mods
        ' que traen CastShadows=False tapa con una perilla un DEFECTO DE LECTURA del bit —el del NIF para
        ' materiales BGEM, ver FO4UnifiedMaterial_Class.CastShadows—, y una perilla que existe para compensar
        ' un bug es justo lo que la regla "nunca un modo legacy" prohibe.
        Dim casters = _shadowCasters
        casters.Clear()
        For Each mesh In meshes
            If mesh Is Nothing OrElse mesh.MeshData Is Nothing OrElse mesh.MeshData.Shape Is Nothing Then Continue For
            If Not mesh.IsDrawable() OrElse mesh.MeshData.Shape.Wireframe Then Continue For
            Dim mb = mesh.MeshData.Material?.MaterialBase
            If mb Is Nothing OrElse Not mb.CastShadows Then Continue For
            ' THE ENGINE'S LAW (MaterialData.EngineShadowMapCasts): effects, blend, alpha < 1 and refraction cast in neither
            ' game; a decal never in FO4 (flags 0xC000000, 0x14217B979). SSE: a decal is a declared hole (whether the
            ' shadow-map accumulators register one, 0x14151EC10 +0x12C / +0x12D, is not traced) and stays out, as before group B.
            If Not mesh.MeshData.Material.EngineShadowMapCasts(FrameIsSse) Then Continue For
            If Not FrameIsSse AndAlso mesh.MeshData.Material.Fo4PreviewGap(ParentControl.SharedFo4Deferred) Then Continue For
            casters.Add(mesh)
        Next
        If casters.Count = 0 Then SoltarMapasDeSombra() : Exit Sub

        ' Encuadre sobre el AABB de TODO lo visible, no solo de los casters: asi cualquier receptor cae
        ' adentro del mapa. Lo que quede afuera lee el borde blanco de la textura = "iluminado".
        Dim bmin As Vector3, bmax As Vector3
        ParentControl.GetSceneBounds(bmin, bmax)
        ' EL PLANO DEL RECEPTOR ES EL PISO DECLARADO POR LA APP, no el punto mas bajo del AABB. Antes
        ' era `bmin.Z` y eso fallaba de dos formas distintas:
        '  1. Wardrobe Manager pone `FloorOffset = -HighHeelHeight`: con un outfit de tacos el piso real y
        '     el receptor quedaban separados por la altura del taco y la sombra flotaba.
        '  2. GetSceneBounds saltea las shapes con RenderHide, asi que en un preview de UNA pieza (un
        '     guante a z = 100) `bmin.Z` era 100 y la sombra salia como una losa colgada en el aire.
        ' El piso procedural es una superficie opaca completa. Separar el receptor +0,02 en Z produce
        ' z-fighting a distancia y en vistas cenitales. Ambos comparten ahora el plano fisico exacto; el
        ' pase del receptor resuelve el empate con PolygonOffset, como un decal, sin mover la sombra.
        _groundZ = CSng(FloorOffset)

        ' ===================== MAPA 1: AJUSTADO AL PERSONAJE =====================
        ' DOS MAPAS, NO UNO. Con un solo mapa compartido, meter el receptor de suelo obligaba a agrandar
        ' el encuadre hasta cubrir donde ATERRIZA la sombra —la cabeza esta a ~180 u y con la key a 26
        ' grados su sombra cae a 180/tan(26) = 369 u de los pies— y como el mapa tiene un tamano FIJO, cada
        ' texel pasaba a cubrir 2,4 veces mas mundo: la sombra sobre el PERSONAJE se volvia 2,4 veces mas
        ' gruesa (medido: texel 0,077 -> 0,181 u). Con un mapa propio para el suelo, el del personaje
        ' vuelve al encuadre ajustado y no se pierde nada de nitidez.
        ' El del suelo se DIBUJA a menos resolucion a proposito: es una mancha grande y difusa, no
        ' necesita filo, y cuanto menos lo decide GroundMapSize a partir de los dos radios.
        ' PERO SE RESERVA AL MISMO TAMANO QUE EL DEL PERSONAJE, y este comentario decia lo contrario
        ' ("el extra de VRAM va de 1/16 del mapa del personaje a igualarlo"). Dejo de ser cierto al
        ' arreglar el churn: el tamano LOGICO sigue saliendo de GroundMapSize —y es el viewport con el que
        ' se dibuja— pero la TEXTURA se reserva fija, porque un tamano de textura que depende de la
        ' elevacion de la luz se recrea varias veces por arrastre de camara. O sea: la resolucion es la de
        ' antes, la VRAM no. Ver ShadowMapMath.UvScaleDeCapa.
        ' ===================== REPARTO DE CAPAS =====================
        ' Que luces castean lo decide el RIG (PreviewLight.CastsShadow) y lo resuelve una funcion PURA,
        ' con orden fijo y sin alocar: ver ShadowMapMath.SlotsDeSombra y su gate `shadow-slots`.
        Dim rigVivo = Config_App.Current.ActiveLights()
        ' El LOOK de la sombra (blandura, oscuridad, receptor de suelo) vive en el RIG, no en
        ' PreviewShadowSettings: es parte del set de luces igual que la temperatura o el balance key/fill.
        ' Se copia al frame aca, junto al reparto de capas, porque UploadShadowUniforms corre despues y no
        ' tiene el rig a mano.
        _shadowSoftness = rigVivo.ShadowSoftnessTexels
        _shadowDarkness = rigVivo.ShadowDarkness
        _shadowCount = ShadowMapMath.SlotsDeSombra(rigVivo, _shadowSlots)
        If _shadowCount <= 0 Then SoltarMapasDeSombra() : Exit Sub

        ' EL ENCUADRE ES POR LUZ PERO EL TAMANO DE TEXEL ES COMUN: Fit toma el extent de la esfera
        ' envolvente, invariante a la rotacion, asi que Radius/TexelWorld/DepthRange salen iguales para
        ' las cuatro y lo unico que cambia es la ViewProj. Por eso las capas de un array alcanzan.
        For luz = 0 To PreviewShadowSettings.MaxShadowLights - 1
            Dim capa = _shadowSlots(luz)
            If capa < 0 Then Continue For
            _shadowFits(capa) = ShadowMapMath.Fit(_frameLights.DirDeLuz(luz), bmin, bmax, cfg.MapSize)
            If Not _shadowFits(capa).Valid Then _shadowCount = 0 : SoltarMapasDeSombra() : Exit Sub
        Next

        If ParentControl.ShadowTarget Is Nothing Then ParentControl.ShadowTarget = New ShadowMapTarget()
        ' `_shadowCount = 0` EN LAS SALIDAS TEMPRANAS, no solo en la cabecera del metodo. El render no se
        ' rompia —UploadShadowUniforms sube bShadows=False por `active`— pero `ShadowCount` es una propiedad
        ' que el ARNES lee, y en un frame que no dibujo ni un mapa reportaba "2 casters". Dos checks
        ' (`strength-cero` y `sin-casters`) se apoyan justo en ese numero: quedaban midiendo contra un valor
        ' que describe una intencion, no lo que se dibujo.
        If Not ParentControl.ShadowTarget.Ensure(cfg.MapSize, _shadowCount, media:=cfg.Depth16) Then _shadowCount = 0 : SoltarMapasDeSombra() : Exit Sub

        ' NI UN glGet NI UN ARRAY POR FRAME ACA. El doc de ShadowMapTarget.BindForWrite dice que los
        ' glGet de framebuffer son los que fuerzan a varios drivers a vaciar la lista de comandos diferida
        ' — y el caller hacia justo uno por frame, mas otro del viewport, mas un array de 4 Integer de
        ' basura de GC en el camino de dibujo. Los dos valores ya se conocen: RenderAll solo es alcanzable
        ' desde RenderScene, que dibuja contra el SCENE TARGET del control (o el framebuffer 0 si no se pudo
        ' alocar), y el viewport es lastW/lastH (los fija ResizeViewport y son los mismos con los que se armo
        ' la proyeccion de este frame).
        Dim prevFbo As Integer = ParentControl.SceneFramebuffer

        For capa = 0 To _shadowCount - 1
            ' El mapa del PERSONAJE usa la capa entera: viewport = lado reservado, escala de UV 1.0. La
            ' reserva-mas-grande-que-el-viewport es cosa del mapa ANCHO, cuyo tamano depende de la camara.
            _shadowUvScale(capa) = 1.0F
            RenderDepthInto(ParentControl.ShadowTarget, capa, cfg.MapSize, _shadowFits(capa), depthShader, casters)
        Next
        _shadowSettings = cfg
        _shadowActive = True

        ' ===================== MAPA 2: ANCHO, SOLO PARA EL SUELO =====================
        If Not rigVivo.ShadowOnGround Then
            ' Apagada la opcion, el mapa ancho se SUELTA. Sin esto quedaban colgados hasta el Clean 16 MB
            ' (2048, una luz casteante) o 64 MB (2048, las cuatro), contradiciendo el criterio del otro
            ' target ("con la opcion apagada nunca se asigna un byte de GPU").
            ' Este comentario decia "~3 MB": era la cuenta de cuando el mapa ancho se dimensionaba solo y
            ' salia tipicamente 512. Con la reserva fija mide lo mismo que el del personaje.
            ParentControl.GroundShadowTarget?.Release()
            OlvidarEncuadresDeSuelo()
        Else
            ' EL RECEPTOR ES POR LUZ Y LA HUELLA ES LA UNION. Cada luz proyecta su propia sombra sobre
            ' el plano y necesita SU capa. Si se recortara a la huella de una sola, la sombra de las
            ' otras saldria cortada en seco — que es exactamente el sintoma que ExpandForGroundShadow
            ' existe para evitar.
            ' LA FORMA DEL ARRAY ES FUNCION DE LA CONFIG, NO DE LA CAMARA, Y ESO ES EL ARREGLO DEL
            ' CHURN. Se reservan SIEMPRE `_shadowCount` capas de `cfg.MapSize`, aunque una luz no
            ' califique este frame: la cantidad de luces que superan la elevacion minima CAMBIA al
            ' orbitar (la direccion la rota la camara con el default de luces-siguen-camara), y si eso
            ' decidiera la forma del array, cada cruce del umbral seria un Release + TexImage3D en el
            ' camino de dibujo. Una capa que no califica no se dibuja: queda en 1.0 = iluminada, y su
            ' aporte entra en cero. Cuesta VRAM y no cuesta ni un frame que dependa de la historia.
            Dim gmin = bmin, gmax = bmax          ' union de huellas
            Dim hayAlguna As Boolean = False
            Dim minPorCapa(PreviewShadowSettings.MaxShadowLights - 1) As Vector3
            Dim maxPorCapa(PreviewShadowSettings.MaxShadowLights - 1) As Vector3
            Dim califica(PreviewShadowSettings.MaxShadowLights - 1) As Boolean
            For luz = 0 To PreviewShadowSettings.MaxShadowLights - 1
                Dim capa = _shadowSlots(luz)
                If capa < 0 Then Continue For
                _groundLuzDeCapa(capa) = luz
                Dim lmin = bmin, lmax = bmax
                Dim expandida As Boolean
                ShadowMapMath.ExpandForGroundShadow(lmin, lmax, _frameLights.DirDeLuz(luz), _groundZ, expandida)
                califica(capa) = expandida
                If Not expandida Then Continue For
                minPorCapa(capa) = lmin
                maxPorCapa(capa) = lmax
                gmin = Vector3.ComponentMin(gmin, lmin)
                gmax = Vector3.ComponentMax(gmax, lmax)
                hayAlguna = True
            Next

            If Not hayAlguna Then
                ' NINGUNA LUZ CALIFICA ESTE FRAME => NO SE DIBUJA, PERO **NO SE SUELTA EL TARGET**.
                ' Soltarlo era el ultimo agujero del arreglo del churn, y contradecia el principio que este
                ' mismo bloque enuncia doce lineas mas arriba: la forma del array es funcion de la CONFIG,
                ' no de la camara. `hayAlguna` SI depende de la camara —es "alguna luz casteante supera
                ' L.Z >= 0,2" y con luces-siguen-camara (el default) orbitar rota esas direcciones en cada
                ' frame del arrastre—, asi que un Release aca es un TexImage3D de 2048x2048 en el camino de
                ' dibujo cada vez que el usuario cruza esa elevacion, ida y vuelta, con la sombra de piso
                ' apareciendo y desapareciendo.
                ' La VRAM queda reservada mientras la OPCION siga prendida, que es exactamente el criterio:
                ' apagar "Shadow on the ground" (config) si suelta, y eso lo hace la rama de arriba.
                OlvidarEncuadresDeSuelo()
            Else
                If ParentControl.GroundShadowTarget Is Nothing Then ParentControl.GroundShadowTarget = New ShadowMapTarget()
                ' RESERVA FIJA: mismo lado que el mapa del personaje, mismas capas. Los dos numeros salen
                ' de la config, asi que Ensure devuelve True sin recrear nada mientras el usuario no toque
                ' la calidad ni las casillas.
                ' Misma precision que el mapa del personaje: es UNA perilla para toda la feature, no dos.
                If Not ParentControl.GroundShadowTarget.Ensure(cfg.MapSize, _shadowCount, media:=cfg.Depth16) Then
                    ' El target no se pudo reservar: este frame no hay receptor, y los encuadres del frame
                    ' anterior no valen. Misma razon que la rama de arriba.
                    OlvidarEncuadresDeSuelo()
                Else
                    Dim algunaDibujada As Boolean = False
                    For capa = 0 To _shadowCount - 1
                        _groundUvScale(capa) = 1.0F
                        _groundValida(capa) = False
                        ' Y EL ENCUADRE SE BORRA, no se deja el del frame pasado. `_groundFits(capa)` solo
                        ' se asigna si la capa califica, asi que sin esto una luz que ESTE frame quedo bajo la
                        ' elevacion minima seguia publicando por `GroundFit` el encuadre de cuando si
                        ' calificaba — un dato rancio que el arnes leeria como si fuera de este frame. Un
                        ' LightFit en cero se nota (Valid = False, Radius = 0); uno viejo se cree.
                        _groundFits(capa) = Nothing
                        If Not califica(capa) Then Continue For
                        ' El tamano LOGICO de esta capa sale de su propio radio: una key alta y un fill rasante
                        ' conservan cada uno su resolucion optima, que un unico tamano de textura para todas
                        ' pierde. Las dos trampas de la funcion las cubre `ground-mapsize`.
                        Dim dirLuz = _frameLights.DirDeLuz(_groundLuzDeCapa(capa))
                        Dim radioTent = ShadowMapMath.Fit(dirLuz, minPorCapa(capa), maxPorCapa(capa), cfg.MapSize).Radius
                        Dim gLog As Integer = ShadowMapMath.GroundMapSize(_shadowFits(0).Radius, radioTent, cfg.MapSize)
                        If gLog <= 0 Then Continue For
                        Dim gfit = ShadowMapMath.Fit(dirLuz, minPorCapa(capa), maxPorCapa(capa), gLog)
                        If Not gfit.Valid Then Continue For
                        _groundFits(capa) = gfit
                        _groundUvScale(capa) = ShadowMapMath.UvScaleDeCapa(gLog, cfg.MapSize)
                        _groundValida(capa) = True
                        RenderDepthInto(ParentControl.GroundShadowTarget, capa, gLog, gfit, depthShader, casters)
                        algunaDibujada = True
                    Next
                    ' NO SE LIMPIAN LAS CAPAS QUE NO CALIFICAN, aunque TexImage3D con IntPtr.Zero deje el
                    ' contenido indefinido: limpiarlas cuesta un glFramebufferTextureLayer + un glClear por capa
                    ' y por frame —la misma revalidacion de FBO que `_capaAttachada` existe para evitar— y no
                    ' protegen de nada, porque esas capas nunca se leen:
                    '  1. `SubirAporteDelSuelo` le pone contribucion CERO a las capas `Not _groundValida(capa)`,
                    '     y el fragment hace `continue` sobre contribucion cero. Ese `continue` es el guardian.
                    '  2. Y sin el, limpiar tampoco salvaria: esas capas tienen la ViewProj en cero —se la deja
                    '     el `_groundFits(capa) = Nothing` del bucle de arriba, UNICO punto que deja una capa en
                    '     cero con `_groundCount` todavia mayor que cero— asi que el lookup divide por w = 0 y
                    '     las coordenadas salen NaN, sin importar que profundidad haya guardada adentro.
                    If algunaDibujada Then
                        _groundCount = _shadowCount
                        ' El quad NO se dimensiona con un radio: ese radio es la media diagonal de la
                        ' esfera 3D e incluye la ALTURA. La huella real es la union gmin/gmax en XY.
                        ShadowMapMath.GroundQuadFromFootprint(gmin, gmax, _groundZ, _groundQuadCenter, _groundQuadHalf)
                        _groundActive = True
                    End If
                End If
            End If
        End If

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, prevFbo)
        GL.Viewport(0, 0, ParentControl.lastW, ParentControl.lastH)
        GL.Enable(EnableCap.CullFace)
        GL.CullFace(TriangleFace.Back)
    End Sub

    ''' <summary>Dibuja la silueta de <paramref name="casters"/> en un shadow map. Es el cuerpo compartido
    ''' por los dos mapas (el ajustado al personaje y el ancho del suelo): el estado GL y el orden de los
    ''' draws tienen que ser IDENTICOS en los dos o la sombra del piso no coincidiria con la del cuerpo.
    ''' <para>NO restaura framebuffer ni viewport: lo hace el caller una sola vez despues del ultimo mapa.</para></summary>
    ''' <param name="viewport">Lado LOGICO que ocupa esta capa dentro de la textura. El
    ''' <c>GL.Clear</c> de abajo limpia la capa ENTERA (glClear no mira el viewport, lo acota el scissor
    ''' y esta apagado), asi que lo que quede fuera de la region logica queda en 1.0 = nada ocluye — que
    ''' es exactamente lo que devuelve el borde blanco. De eso depende que la reserva fija sea correcta.</param>
    Private Sub RenderDepthInto(target As ShadowMapTarget, capa As Integer, viewport As Integer,
                                fit As ShadowMapMath.LightFit,
                                depthShader As Shader_Base_Class, casters As List(Of RenderableMesh))
        target.BindForWrite(capa, viewport)

        GL.Clear(ClearBufferMask.DepthBufferBit)
        GL.Enable(EnableCap.DepthTest)
        GL.DepthFunc(DepthFunction.Lequal)
        GL.DepthMask(True)
        GL.Disable(EnableCap.Blend)
        ' SIN culling de caras, a proposito. El truco clasico contra el acne es cullear las frontales,
        ' pero aca hay superficies ABIERTAS (cards de pelo, tela) y materiales TwoSided: descartar una
        ' cara les abre huecos en la sombra. El acne se ataca con el normal-offset del fragment.
        GL.Disable(EnableCap.CullFace)
        GL.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Fill)

        ' Las matrices de la LUZ son del pase, no de la malla: se suben una sola vez. Los planos del
        ' frustum tampoco cambian adentro del pase.
        RenderableMesh.ExtractFrustumPlanes(fit.ViewProj, _shadowPlanes)
        depthShader.Use()
        depthShader.SetMatrix4("matProjection", fit.Proj)
        depthShader.SetMatrix4("matView", fit.View)

        For Each mesh In casters
            ' Cull por el frustum de la LUZ (no el de la camara): un caster fuera de pantalla puede
            ' proyectar adentro. Misma funcion y misma convencion (view * proj) que el pase iluminado.
            If Not RenderableMesh.IsAABBInFrustum(mesh.BoundsMin, mesh.BoundsMax, _shadowPlanes) Then Continue For
            mesh.RenderDepthOnly(depthShader, fit.View)
        Next

        GL.BindVertexArray(0)
    End Sub

    ''' <summary>Borra los encuadres del mapa ANCHO y apaga su cuenta. Se llama desde TODA salida en la que
    ''' el receptor de suelo no se dibuja pero el target NO se suelta.
    ''' <para>NO ES HIGIENE: <c>_groundFits</c> sobrevive entre frames, y sin esto una capa que este frame
    ''' no califica sigue publicando por <c>GroundFit</c> el encuadre de cuando SI calificaba — con
    ''' <c>Valid = True</c>, o sea indistinguible de uno fresco para quien lo lee. Un <c>LightFit</c> en cero
    ''' se nota; uno viejo se cree.</para>
    ''' <para>NO es esta funcion la que alimenta el <c>continue</c> del fragment del suelo, aunque lo
    ''' parezca: aca siempre se sale con <c>_groundCount = 0</c>, o sea que el bucle de ese fragment ni
    ''' itera. La capa en cero que ESE guardian ataja la deja el <c>_groundFits(capa) = Nothing</c> del bucle
    ''' de dibujo de <see cref="RenderShadowPass"/>, que es el unico punto donde una capa queda en cero con
    ''' <c>_groundCount</c> todavia mayor que cero.</para></summary>
    Private Sub OlvidarEncuadresDeSuelo()
        For capa = 0 To PreviewShadowSettings.MaxShadowLights - 1
            _groundFits(capa) = Nothing
            _groundValida(capa) = False
            _groundUvScale(capa) = 1.0F
        Next
        _groundCount = 0
    End Sub

    ''' <summary>Sube al shader ILUMINADO (o al del suelo) todo lo que necesita <c>shadowFactorAt()</c>.
    ''' Se llama UNA vez por frame y por programa, no por malla: son constantes del frame.
    ''' <para>Con <c>active = False</c> sube <c>bShadows = false</c> y nada mas: el fragment ni calcula el
    ''' factor, y el frame sale igual al de antes de que existiera la feature.</para>
    ''' <para>EL MAPEO LUZ→CAPA VIAJA EN <c>uShadowSlot</c> Y NO SE RECALCULA EN EL SHADER. Es la misma
    ''' tabla que uso el pase de profundidad; derivarla dos veces es como se termina proyectando la
    ''' sombra de una luz sobre el difuso de otra.</para></summary>
    ''' <param name="fits">Encuadres POR CAPA.</param>
    ''' <param name="count">Cuantas capas tiene el target.</param>
    ''' <param name="slots">Luz→capa, o Nothing para el programa del suelo (que indexa por capa directo).</param>
    ''' <param name="uvScale">Region logica / textura reservada, POR CAPA. 1.0 en el mapa del personaje.</param>
    Private Sub UploadShadowUniforms(shader As Shader_Base_Class, fits() As ShadowMapMath.LightFit,
                                     count As Integer, slots() As Integer, uvScale() As Single,
                                     target As ShadowMapTarget, active As Boolean,
                                     unit As TextureUnit, normalBiasTexels As Single,
                                     depthBiasWorld As Single)
        If shader Is Nothing Then Exit Sub
        shader.Use()
        If Not active OrElse target Is Nothing OrElse count <= 0 Then
            shader.SetBool("bShadows", False)
            Exit Sub
        End If

        shader.SetBool("bShadows", True)

        ' Las matrices van aplanadas a un buffer REUTILIZADO: una llamada de GL para las N.
        For capa = 0 To count - 1
            CopiarMatriz(fits(capa).ViewProj, _bufViewProj, capa * 16)
            ' EL BIAS DE PROFUNDIDAD ENTRA EN UNIDADES DE MUNDO Y SE NORMALIZA CON EL DepthRange DE SU
            ' PROPIA CAPA. En el mapa del personaje las N capas tienen el mismo rango (esfera envolvente);
            ' en el ANCHO no, porque su extent es la huella proyectada y depende de la elevacion de cada
            ' luz. Un solo valor para todas dejaba la sombra de la luz mas rasante despegada del pie.
            _bufDepthBias(capa) = If(fits(capa).DepthRange > 0.0F, depthBiasWorld / fits(capa).DepthRange, 0.0F)
        Next
        ' El nombre va con el `[0]` puesto: es un literal internado y asi el setter no aloca una String por
        ' llamada en el camino de dibujo. Ver el doc de SetMatrix4Array.
        shader.SetMatrix4Array("matShadowViewProj[0]", _bufViewProj, count)
        shader.SetFloatArray("uShadowDepthBias[0]", _bufDepthBias, count)
        shader.SetFloatArray("uShadowUvScale[0]", uvScale, count)

        ' El programa del suelo indexa por CAPA (su bucle va de 0 a uGroundCount): no usa uShadowSlot.
        If slots IsNot Nothing Then shader.SetIntArray("uShadowSlot[0]", slots, PreviewShadowSettings.MaxShadowLights)

        ' Acotado ACA: el rig no tiene un Sanitized() y estos dos ya no pasan por el de PreviewShadowSettings.
        shader.SetFloat("uShadowIntensity", Math.Clamp(_shadowDarkness, 0.0F, 1.0F))
        ' | EL NORMAL-OFFSET ES CERO PARA EL RECEPTOR DE SUELO, y no es una omision. Su unica funcion es
        ' matar el auto-sombreado de superficies rasantes, y el quad del piso NO ES CASTER: no puede
        ' auto-sombrearse. En cambio SI paga el desvio: el mapa del suelo tiene texeles mucho mas grandes
        ' y eso se traduce en sombra DESPEGADA del pie (peter-panning).
        ' | ES UN ESCALAR PARA TODAS LAS CAPAS porque el texel del mapa del personaje es el mismo en
        ' todas (esfera envolvente). Ver el comentario del bloque de uniforms en el GLSL.
        shader.SetFloat("uShadowNormalBias", normalBiasTexels * fits(0).TexelWorld)

        ' | LA SUAVIDAD ES CONTINUA: el radio entero es el techo y el SOBRANTE viaja en el espaciado de
        ' los taps, que es lo que hace continuo el desenfoque sin cambiar la cantidad de muestras.
        Dim soft As Single = Math.Clamp(_shadowSoftness, 0.0F, PreviewShadowSettings.MaxPcfRadius)
        Dim radio As Integer = CInt(Math.Ceiling(soft))
        shader.SetInt("uShadowPcfRadius", radio)
        Dim paso As Single = If(radio > 0, soft / radio, 1.0F)
        Dim invSize As Single = If(target.Size > 0, paso / target.Size, 0.0F)
        shader.SetVector2("uShadowTexelUV", New Vector2(invSize, invSize))
        ' | LA UNIDAD ES UN PARAMETRO, y tiene que serlo. El sampler uniform es POR PROGRAMA, pero el
        ' binding de la unidad es ESTADO GLOBAL del contexto. El pase del suelo corre entre DECAL y
        ' BLENDED; si usara la misma unidad que el pase iluminado, dejaria ahi el array ANCHO y las
        ' mallas BLENDED y el pase de OVERLAYS samplearian esa textura con las matrices del encuadre
        ' AJUSTADO: coordenadas de un encuadre contra la textura de otro.
        ' 14 espeja el t14 del motor para el pase iluminado; 15 queda para el suelo. Las dos son
        ' EXCLUSIVAS de sombras y ahora ademas de target TEXTURE_2D_ARRAY: ver BindTextureArray.
        shader.BindTextureArray("texShadowMap", target.Texture, unit)
    End Sub

    ''' <summary>Aplana una Matrix4 de OpenTK a un buffer de floats en el orden que espera glUniform
    ''' (column-major, que es como OpenTK guarda sus filas y como ya se sube con SetMatrix4).</summary>
    Friend Shared Sub CopiarMatriz(m As Matrix4, destino As Single(), offset As Integer)
        destino(offset + 0) = m.M11 : destino(offset + 1) = m.M12 : destino(offset + 2) = m.M13 : destino(offset + 3) = m.M14
        destino(offset + 4) = m.M21 : destino(offset + 5) = m.M22 : destino(offset + 6) = m.M23 : destino(offset + 7) = m.M24
        destino(offset + 8) = m.M31 : destino(offset + 9) = m.M32 : destino(offset + 10) = m.M33 : destino(offset + 11) = m.M34
        destino(offset + 12) = m.M41 : destino(offset + 13) = m.M42 : destino(offset + 14) = m.M43 : destino(offset + 15) = m.M44
    End Sub

    Public Property FloorOffset As Double = -0.00F
    ''' <summary>True while PreviewControl.RenderScene draws a frame into the HDR target of the FO4 post: the
    ''' floor and the ground catcher write linear radiance, and what is drawn in display values (wireframes)
    ''' is held back for <see cref="RenderDisplayOverlays"/>.</summary>
    Friend Property FrameIsHdr As Boolean

    ''' <summary>Wireframe meshes held back by an HDR frame, in the order RenderAll reached them.</summary>
    Private ReadOnly _displayOverlays As New List(Of RenderableMesh)

    ''' <summary>Draws, on the display target after the post, what an HDR frame held back: the wireframe
    ''' meshes (display-space colours by construction - a texture or a flat tint with WireAlpha - that the
    ''' image-space law must not touch). Depth-tested against the scene's depth.</summary>
    Friend Sub RenderDisplayOverlays(projection As Matrix4, camera As OrbitCamera)
        For Each mesh In _displayOverlays
            mesh.Render(projection, camera)
        Next
        _displayOverlays.Clear()
    End Sub

    ''' <summary>This frame's preview gaps: reason -&gt; the shapes the engine draws and the preview cannot yet (Fo4GBufferDraw.Gap, a
    ''' precombined shape), shown by PreviewControl as a notice on the frame (never a silent "not drawn").</summary>
    Friend ReadOnly FramePreviewGaps As New SortedDictionary(Of String, SortedSet(Of String))(StringComparer.Ordinal)

    ''' <summary>This frame's shapes the GAME itself does not draw and the preview does not show either (RenderableMesh.ReportUndrawn):
    ''' reason -&gt; shapes, shown in the same notice (user decision 5-oct-2026: the engine's law is kept, only the silence goes).</summary>
    Friend ReadOnly FrameEngineUndrawn As New SortedDictionary(Of String, SortedSet(Of String))(StringComparer.Ordinal)

    ''' <summary>This frame's shapes the GAME does not draw and the preview DOES, so that they can be edited (user decision 6-oct-2026;
    ''' RenderableMesh.ReportUndrawn): reason -&gt; shapes, shown in the same notice.</summary>
    Friend ReadOnly FrameDrawnForEditing As New SortedDictionary(Of String, SortedSet(Of String))(StringComparer.Ordinal)

    ''' <summary>This frame's shapes whose material alpha at rest is 0 and whose alpha controller lifts it: the game draws them while it
    ''' animates, the preview shows them at that maximum (C2 v6 L5, aud-B-05): reason -&gt; shapes, shown in the same notice.</summary>
    Friend ReadOnly FrameShownAtAnimatedMaximum As New SortedDictionary(Of String, SortedSet(Of String))(StringComparer.Ordinal)

    ''' <summary>C4: this frame's shapes the game draws and the preview draws differently (FrameNoticeKind.DrawnDifferently).</summary>
    Friend ReadOnly FrameDrawnDifferently As New SortedDictionary(Of String, SortedSet(Of String))(StringComparer.Ordinal)

    ''' <summary>C4: this frame's texture paths absent from the game's data: path -&gt; the shapes naming it.</summary>
    Friend ReadOnly FrameMissingTextures As New SortedDictionary(Of String, SortedSet(Of String))(StringComparer.OrdinalIgnoreCase)

    ''' <summary>C4: this frame's empty texture slots sampled with a non-neutral default: slot name -&gt; shapes.</summary>
    Friend ReadOnly FrameEmptyTextureSlots As New SortedDictionary(Of String, SortedSet(Of String))(StringComparer.Ordinal)

    ''' <summary>C10 rev-04: this frame's shapes whose drawn value of a material variable comes from a shader controller at rest
    ''' (FrameNoticeKind.DrivenByControllerAtRest): the variables' names -&gt; shapes.</summary>
    Friend ReadOnly FrameDrivenAtRest As New SortedDictionary(Of String, SortedSet(Of String))(StringComparer.Ordinal)

    ''' <summary>GATE ONLY (no UI): ShadowGate --default-textures mutant. True skips the frame's texture census.</summary>
    Friend Shared GateNoTextureNoticeCensus As Boolean = False

    ''' <summary>The eight kinds of the frame's notice, one map each (DrawnDifferently / MissingTexture / EmptyTextureSlot: C4, the
    ''' engine's default textures; DrivenByControllerAtRest: C10).</summary>
    Friend Enum FrameNoticeKind
        ''' <summary>The game draws it, the preview cannot (FramePreviewGaps).</summary>
        PreviewGap
        ''' <summary>The game does not draw it, and the preview does not show it (FrameEngineUndrawn).</summary>
        EngineUndrawn
        ''' <summary>The game does not draw it, the preview draws it so that it can be edited (FrameDrawnForEditing).</summary>
        DrawnForEditing
        ''' <summary>Its alpha at rest is 0 and the game draws it while its alpha controller animates it: the preview shows it at the
        ''' highest value the controller reaches (C2 v6 L5, aud-B-05; FrameShownAtAnimatedMaximum).</summary>
        ShownAtAnimatedMaximum
        ''' <summary>C4 extension (rev-07 / rev-11): the game draws it, the preview draws it differently and cannot reproduce it
        ''' (FrameDrawnDifferently): a recolor without a base texture, a texture in the data the preview did not load.</summary>
        DrawnDifferently
        ''' <summary>C4: a texture path absent from the game's data (FrameMissingTextures).</summary>
        MissingTexture
        ''' <summary>C4: an empty texture slot the game samples with a default that is not neutral (FrameEmptyTextureSlots).</summary>
        EmptyTextureSlot
        ''' <summary>C10 rev-04 (user decision 7-oct-2026): the game draws it with a value its shader controller writes at the clock's
        ''' origin, and so does the preview (MaterialData.RestValue); what the editors show and save is the block's (FrameDrivenAtRest).</summary>
        DrivenByControllerAtRest
    End Enum

    ''' <summary>The frame's serial (RenderAll): per-frame caches key on it (MaterialData.Fo4EngineState, MaterialData.PreviewAlpha).</summary>
    Friend Property FrameSerial As Integer

    ''' <summary>The one sink of the frame's notice: <paramref name="kind"/> picks the map (FrameNoticeKind). A shape is listed once
    ''' under each reason.</summary>
    Friend Sub ReportUndrawn(shapeName As String, reason As String, kind As FrameNoticeKind)
        Dim map As SortedDictionary(Of String, SortedSet(Of String))
        Select Case kind
            Case FrameNoticeKind.PreviewGap : map = FramePreviewGaps
            Case FrameNoticeKind.EngineUndrawn : map = FrameEngineUndrawn
            Case FrameNoticeKind.DrawnForEditing : map = FrameDrawnForEditing
            Case FrameNoticeKind.ShownAtAnimatedMaximum : map = FrameShownAtAnimatedMaximum
            Case FrameNoticeKind.DrawnDifferently : map = FrameDrawnDifferently
            Case FrameNoticeKind.MissingTexture : map = FrameMissingTextures
            Case FrameNoticeKind.EmptyTextureSlot : map = FrameEmptyTextureSlots
            Case FrameNoticeKind.DrivenByControllerAtRest : map = FrameDrivenAtRest
            Case Else : Throw New ArgumentOutOfRangeException(NameOf(kind))
        End Select
        Dim shapes As SortedSet(Of String) = Nothing
        If Not map.TryGetValue(reason, shapes) Then
            shapes = New SortedSet(Of String)(StringComparer.Ordinal)
            map(reason) = shapes
        End If
        shapes.Add(If(shapeName, ""))
    End Sub

    ''' <summary>The lines of the frame's notice (PreviewControl draws them on the window when Config_App.Setting_ShowErrors): one per
    ''' reason with its shape count - the preview's gaps ("Not drawn by the preview (n shapes): reason"), the game's own ("reason
    ''' (n shapes)": each such reason already says that the game does not draw it), then what the game does not draw and the preview
    ''' does ("Drawn by the preview, not by the game (n shapes): reason"), what it shows at its animated maximum, then C4's texture
    ''' lines ("Drawn differently by the preview", "Missing texture", "Empty texture slot"), then C10's ("Value driven by a controller
    ''' at rest", rev-04). Empty when the frame draws every shape as
    ''' the game does.</summary>
    Friend Function FrameNoticeLines() As List(Of String)
        Dim shapes = Function(n As Integer) $"{n} shape{If(n = 1, "", "s")}"
        Dim lines As New List(Of String)
        For Each kv In FramePreviewGaps
            lines.Add($"Not drawn by the preview ({shapes(kv.Value.Count)}): {kv.Key}")
        Next
        For Each kv In FrameEngineUndrawn
            lines.Add($"{kv.Key} ({shapes(kv.Value.Count)})")
        Next
        For Each kv In FrameDrawnForEditing
            lines.Add($"Drawn by the preview, not by the game ({shapes(kv.Value.Count)}): {kv.Key}")
        Next
        For Each kv In FrameShownAtAnimatedMaximum
            lines.Add($"Shown at its animated maximum ({shapes(kv.Value.Count)}): {kv.Key}")
        Next
        For Each kv In FrameDrawnDifferently
            lines.Add($"Drawn differently by the preview ({shapes(kv.Value.Count)}): {kv.Key}")
        Next
        ' Fallout 4: what the game samples for an absent file is not traced (C4 L7) - the preview keeps its own fallback and says so.
        Dim missingLabel = If(FrameIsSse, "Missing texture", "Missing texture, the game's fallback not traced")
        For Each kv In FrameMissingTextures
            lines.Add($"{missingLabel} ({shapes(kv.Value.Count)}): {kv.Key}")
        Next
        For Each kv In FrameEmptyTextureSlots
            lines.Add($"Empty texture slot ({shapes(kv.Value.Count)}): {kv.Key}")
        Next
        For Each kv In FrameDrivenAtRest
            lines.Add($"Value driven by a controller at rest ({shapes(kv.Value.Count)}): {kv.Key}")
        Next
        Return lines
    End Function

    Public Sub RenderAll(projection As Matrix4, camera As OrbitCamera)
        _displayOverlays.Clear()
        FramePreviewGaps.Clear()
        FrameEngineUndrawn.Clear()
        FrameDrawnForEditing.Clear()
        FrameShownAtAnimatedMaximum.Clear()
        FrameDrawnDifferently.Clear()
        FrameMissingTextures.Clear()
        FrameEmptyTextureSlots.Clear()
        FrameDrivenAtRest.Clear()
        FrameSerial = If(FrameSerial = Integer.MaxValue, 0, FrameSerial + 1)
        For i = 0 To FrameSamplerFilter.Length - 1 : FrameSamplerFilter(i) = 3 : Next
        ' SOFT's scene depth: nothing copied yet this frame, and the depth buffer is "written" (cleared).
        FrameSceneDepthValid = False
        FrameDepthDirty = True
        ' A FO4 effect draw masks the alpha of draw buffer 0 (ApplyMaterial): every frame starts with all channels.
        GL.ColorMask(True, True, True, True)
        ' O4.1: Process pending background texture uploads (Phase 2) each frame
        ProcessPendingTextureUploads()

        ' El piso procedural usa el mismo rig resuelto que las mallas y debe seguir siendo valido en las
        ' salidas tempranas de carga de texturas y modelo vacio. Resolverlo no depende de ninguna textura.
        ResolveFrameLights(camera)

        ' Hide meshes while textures are still loading — show status overlay instead
        If Not TexturesReady Then
            If Floor IsNot Nothing AndAlso Floor.Enabled = True Then Floor.Render(projection, camera, FloorOffset)
            ParentControl.Processing_Status("Texturing...")
            ParentControl.UpdateRequired = True
            Exit Sub
        End If

        ' The floor stands for the world around the subject: it is not part of the engine scene, so it is drawn after the opaque
        ' finish and its composite (outside the SAO, user decision B) in both games and every mode (user decision A).
        If meshes.Count = 0 Then
            If Floor IsNot Nothing AndAlso Floor.Enabled = True Then Floor.Render(projection, camera, FloorOffset)
            Exit Sub
        End If

        ' A GAME WITHOUT ITS PROGRAMS, OR FALLOUT 4 WITHOUT ITS DEFERRED TARGETS: no frame, the status card (user decision rev-38 a,
        ' rev-50, v4 B-rev-05; never a fallback).
        Dim unavailable = ParentControl.FrameUnavailable(FrameIsSse, FrameUsesFo4Deferred)
        If unavailable IsNot Nothing Then
            ParentControl.Processing_Status(unavailable)
            Exit Sub
        End If

        ' SOMBRAS: el shadow map se dibuja ANTES de cualquier pase iluminado y DESPUES de resolver el rig,
        ' porque la direccion de la key sale de _frameLights.KeyDir — la MISMA que va a los uniforms del
        ' fragment. Tomarla de otro lado permitiria que la sombra se proyecte desde una direccion y la luz
        ' venga de otra.
        AbrirCronometroDeProfundidad()
        RenderShadowPass()
        CerrarCronometroDeProfundidad()
        ' Y los uniforms que el fragment iluminado necesita, una sola vez para todos los draws del frame.
        UploadShadowUniforms(ParentControl.CurrentShader, _shadowFits, _shadowCount, _shadowSlots, _shadowUvScale,
                             ParentControl.ShadowTarget, _shadowActive,
                             TextureUnit.Texture14, PreviewShadowSettings.NormalBiasTexels,
                             PreviewShadowSettings.DepthBiasTexels * _shadowFits(0).TexelWorld)
        _frameShadowPrograms.Clear()

        ' Note: ShapeDataLoaded is intentionally NOT checked here. Each mesh.Render() guards
        ' against null RelatedNifShape internally. Checking ShapeDataLoaded at this level would
        ' stop rendering all meshes whose VBOs are still valid just because the CPU-side shapedata
        ' was evicted by the LRU, which is an unnecessary regression in render quality.

        If RenderBucketsDirty OrElse RenderBucketsGame <> FrameIsSse OrElse
           (OpaqueMeshes.Count + DecalMeshes.Count + DecalBlendedMeshes.Count + BlendedMeshes.Count +
            BillboardMeshes.Count + ForwardEffectMeshes.Count + NoColourPassMeshes.Count) <> meshes.Count Then
            RebuildRenderBuckets()

            ' SACADO: el re-orden de OPAQUE y CUTOUT (hoy un solo bucket OPAQUE, chunk C8) por `DiffuseTexture_ID` (era la optimizacion "O3.5",
            ' "sort by diffuse texture ID to minimize GL state changes"). NO VOLVER A PONERLO ASI.
            '
            ' 1) EL ORDEN DE ESTOS DOS BUCKETS ES SEMANTICO, no cosmetico. Los dos escriben depth con
            '    DepthFunc=Lequal ⇒ en un EMPATE de profundidad gana el ULTIMO dibujado. Con superficies
            '    coincidentes (head parts pegados a la cara: pelo facial, cejas, pestañas) el orden decide
            '    cual se ve.
            ' 2) LA CLAVE ERA UN VALOR DE RUNTIME QUE VARIOS CAMINOS LEGITIMOS REEMPLAZAN:
            '      · SSE plegado  -> MaterialData.SseFoldedDiffuseKey hace que DiffuseTexture_ID devuelva la
            '        textura per-NPC en vez del complexion (otro id).
            '      · FO4 facetint -> NpcFaceTintResolver.ApplyPipelineResultToDict pisa entry.Texture_ID con
            '        el id fresco del compositor, BAJO LA MISMA RUTA (otro id, sin tocar el path).
            '    O sea que componer la cara reordenaba el bucket y cambiaba lo que se veia en OTRAS shapes.
            '    MEDIDO (Khajiit, toggle del render plegado): CUTOUT pasaba de [head(11), Beard(18)] a
            '    [Beard(15), head(34)], y el diff de framebuffer POR PASE daba 0 px de diferencia tras
            '    OPAQUE y 1411 px tras CUTOUT — la diferencia entera nacia en este pase.
            ' 3) Y NO AHORRABA NADA: Shader_Base_Class.BindTexture no tiene chequeo de redundancia (siempre
            '    ActiveTexture + BindTexture + uniform), asi que agrupar por textura no elimina UNA sola
            '    llamada GL. El beneficio prometido no existia mientras ese metodo no saltee lo ya bindeado.
            '
            ' El orden que queda es el de RebuildRenderBuckets: el del juego (O-ST registro, O-OP opacos; chunk C8). Determinista
            ' y ajeno a que texturas tenga cada malla.
            ' Si algun dia se quiere el batching de verdad: PRIMERO hacer que BindTexture saltee redundantes,
            ' y recien ahi ordenar por la RUTA DECLARADA del material (Diffuse_or_Base_Texture), que ningun
            ' camino de composicion muta — NUNCA por el id resuelto.
            '
            ' DIAGNOSTICO (Logger.Enabled): se conserva el volcado del orden para poder verificar que ahora
            ' es el MISMO con y sin composicion de cara, en vez de suponerlo.
            If Logger.Enabled Then
                Dim dump = Function(name As String, bucket As List(Of RenderableMesh)) As String
                               Dim sb As New Text.StringBuilder($"[BUCKET-ORDER] {name} n={bucket.Count}: ")
                               For i = 0 To bucket.Count - 1
                                   Dim m = bucket(i)
                                   sb.Append($"{i}:'{m.MeshData.Shape?.ShapeName}'(idx={m.MeshData.Idx},dif={m.MeshData.Material.DiffuseTexture_ID}) ")
                               Next
                               Return sb.ToString()
                           End Function
                Logger.LogLazy(Function() dump("OPAQUE", OpaqueMeshes))
                Logger.LogLazy(Function() dump("BLENDED", BlendedMeshes))
            End If
        End If

        ' O3.3: Compute view-projection matrix for frustum culling
        Dim viewMatrix = camera.GetViewMatrix()
        Dim vp As Matrix4 = viewMatrix * projection
        ' Los planos del frustum de la camara: constantes para los cinco bucles de abajo.
        RenderableMesh.ExtractFrustumPlanes(vp, _framePlanes)

        ' The overlay layers' buckets (they are shapes of their own: same routing as the meshes).
        CollectOverlayItems()
        ' The frame's notice: every visible shape and layer this frame does not draw, with its reason.
        If Not GateNoUndrawnCensus Then ReportUndrawnShapes()

        ' SKYRIM SE Z-PREPASS (0x141538FA0): kMAIN gets the depth of every shape SseRenderPassLaw puts in it (lighting and
        ' effect slot 0x2B, with the prepass alpha test), no colour; then CopyResource(ds[7], kMAIN) - the scene depth SOFT
        ' reads (kPOST_ZPREPASS_COPY, 0x141539F65). The opaque batch then draws EQUAL against it. The floor is drawn after the opaque finish (below).
        ' FALLOUT 4 Z-PREPASS (0x14181FAA0, first in the G-buffer stage): the eligible lighting shapes (Fo4RenderPassLaw),
        ' depth only; their G-buffer draw then tests without writing. FO4's SOFT reads the live depth (ApplyMaterial).
        ' FALLOUT 4 (propuesta v3 G): the same lists, drawn into the deferred targets in D3D row order (prepass -> _gbDepth, then the
        ' G-buffer), then the lights and the composite into the scene target. The stage's GL state is undone whatever happens.
        If FrameUsesFo4Deferred Then
            Try
                ParentControl.BeginFo4GBuffer()
                If GateFo4ThrowInStage Then GateFo4ThrowInStage = False : Throw New InvalidOperationException("GATE: aborted FO4 G-buffer stage")
                FrameGBufferStage = True
                DrawOpaqueStage(projection, camera)
                FrameGBufferStage = False
                ParentControl.FinishFo4Deferred(BuildFo4DeferredFrame(projection, viewMatrix))
                ' The blit wrote the scene depth: the forward effects' SOFT copies it again (FO4 reads the live depth). Since the blit
                ' is straight (zfight-overlay fix) a copy taken inside the G-buffer stage holds the same values wherever no later write
                ' dirtied it, so this copies from the buffer the forward stage reads rather than changing what a copy holds.
                FrameDepthDirty = True
                ' The forward stage in the D3D row order the G-buffer depth was written in (one orientation, as the game: an overlay
                ' on its body's geometry gets the body's depth bit for bit); EndFo4Stage turns the targets to GL rows after it.
                DrawForwardStage(projection, camera, viewMatrix, vp)
            Finally
                FrameGBufferStage = False
                ParentControl.EndFo4Stage()
            End Try
        Else
            DrawOpaqueStage(projection, camera)
            ' SSE: the SAO composite over the opaque scene, between the opaque finish and the alpha finish (0x14153A1CA).
            If FrameIsSse AndAlso FrameIsHdr AndAlso Not GateDisableSseComposite Then ParentControl.ApplySseOpaqueComposite()
            DrawForwardStage(projection, camera, viewMatrix, vp)
        End If

        DrawRefractionNormals(projection, camera)
    End Sub

    ''' <summary>The frame after the opaque finish (and SSE's composite): the floor, Fallout 4's forward stage (bucket 0, list 0xE),
    ''' the ground catcher and the blended list. In Fallout 4's deferred frame it runs inside the D3D-order stage (RenderAll).</summary>
    Private Sub DrawForwardStage(projection As Matrix4, camera As OrbitCamera, viewMatrix As Matrix4, vp As Matrix4)
        If Floor IsNot Nothing AndAlso Floor.Enabled = True Then Floor.Render(projection, camera, FloorOffset)

        ' FO4 forward stage (0x1421F9100), after the whole G-buffer: bucket 0 (opaque effects), then list 0xE.
        If Not FrameIsSse Then
            DrawBucket(ForwardEffectMeshes, _ovlForwardEffect, projection, camera)
            DrawBucket(BillboardMeshes, _ovlBillboard, projection, camera)
        End If

        ' 3b. RECEPTOR DE SUELO — la silueta del personaje sobre el plano del piso.
        ' EL ORDEN ES ESTE Y NO OTRO: despues de OPAQUE/DECAL para que el personaje lo tape por
        ' depth-test, y ANTES de BLENDED para que el pelo alpha-blend y los ojos compongan encima. Movido
        ' arriba de todo taparia la sombra con el cuerpo; movido al final la sombra pisaria al pelo.
        If _shadowActive AndAlso _groundActive Then
            If _groundQuad Is Nothing Then _groundQuad = New GroundShadowQuad()
            ' El quad usa OTRO programa, asi que necesita su propia copia de los uniforms de sombra
            ' (los uniforms son por-programa, no globales).
            ' El quad usa OTRO programa, asi que recibe su propia copia de los uniforms — y ahi esta el
            ' truco de los dos mapas: MISMOS nombres de uniform, valores DISTINTOS (el encuadre ancho y su
            ' textura). Por eso no hubo que tocar una linea de GLSL.
            UploadShadowUniforms(ParentControl.SharedGroundShadowShader, _groundFits, _groundCount, Nothing, _groundUvScale,
                                 ParentControl.GroundShadowTarget, _groundActive,
                                 TextureUnit.Texture15, 0.0F,
                                 PreviewShadowSettings.DepthBiasTexels * _shadowFits(0).TexelWorld)
            SubirAporteDelSuelo(ParentControl.SharedGroundShadowShader)
            ParentControl.SharedGroundShadowShader.Use()
            ParentControl.SharedGroundShadowShader.SetBool("bHdrTarget", FrameIsHdr)
            ' The ratio goes to the display background through the game's display encode: FO4 pow(1/2.2), SSE raw.
            ParentControl.SharedGroundShadowShader.SetFloat("uDisplayExponent",
                Shader_Base_Class.DisplayExponent(ParentControl.CurrentShader Is ParentControl.SharedSSEShader))
            If FrameIsHdr Then ParentControl.SetGroundCatcherOutputs(True)
            _groundQuad.Render(ParentControl.SharedGroundShadowShader, vp, _groundQuadCenter, _groundQuadHalf)
            If FrameIsHdr Then ParentControl.SetGroundCatcherOutputs(False)
        End If

        ' 4. BLENDED — back to front, STABLE on equal keys (FO4 alpha list: bottom-up merge sort that keeps the
        ' left run first on equal keys, 0x14221DBD0/DBD2). Overlay layers that blend join this list with their
        ' base shape's key, after the blended meshes in insertion order.
        ' The point is the game's world-bound centre (O-PT, EngineWorldBound; chunk C8).
        BlendedDepthBuffer.Clear()
        Dim seq = 0
        For Each mesh In BlendedMeshes
            ' O3.3: Frustum cull blended meshes too
            If Not RenderableMesh.IsAABBInFrustum(mesh.BoundsMin, mesh.BoundsMax, _framePlanes) Then Continue For
            Dim viewPos = Vector3.TransformPosition(mesh.WorldBoundCentre(), viewMatrix)
            BlendedDepthBuffer.Add(New MeshDepth With {.Mesh = mesh, .Depth = -viewPos.Z, .Seq = seq}) : seq += 1
        Next
        For Each it In _ovlBlended
            Dim viewPos = Vector3.TransformPosition(it.Mesh.WorldBoundCentre(), viewMatrix)
            BlendedDepthBuffer.Add(New MeshDepth With {.Mesh = it.Mesh, .Layer = it.Layer, .Depth = -viewPos.Z, .Seq = seq}) : seq += 1
        Next
        Dim ordered = BlendedDepthBuffer.OrderByDescending(Function(d) d.Depth).ThenBy(Function(d) d.Seq).ToList()
        For Each item In ordered
            If FrameIsHdr AndAlso item.Layer Is Nothing AndAlso item.Mesh.MeshData.Shape IsNot Nothing AndAlso item.Mesh.MeshData.Shape.Wireframe Then
                _displayOverlays.Add(item.Mesh)
            ElseIf item.Layer Is Nothing Then
                item.Mesh.Render(projection, camera)
            Else
                item.Mesh.RenderOverlayLayer(projection, camera, item.Layer)
            End If
        Next
    End Sub

    ''' <summary>The refraction normals of the frame (after the D3D-order stage of Fallout 4's frame: GL rows, as ISRefraction reads
    ''' them).</summary>
    Private Sub DrawRefractionNormals(projection As Matrix4, camera As OrbitCamera)
        ' REFRACTION NORMALS (SSE 0x141520A10 after the world's opaque + alpha render; FO4 0x1421D6540 after the forward
        ' stage incl. the alpha finish): the refracting shapes have no colour pass (they sit in NoColourPassMeshes); the
        ' target is cleared and bound only when at least one of them draws. ISRefraction then runs from RenderScene.
        FrameRefractionActive = False
        If Not GateDisableRefraction Then
            Dim lodScale = RefractionLaw.LodScale(FrameIsSse)
            Dim abierto = False
            For Each mesh In NoColourPassMeshes
                If mesh.MeshData?.Shape Is Nothing OrElse mesh.MeshData.Shape.Wireframe Then Continue For
                If Not RenderableMesh.IsAABBInFrustum(mesh.BoundsMin, mesh.BoundsMax, _framePlanes) Then Continue For
                Dim r = mesh.MeshData.Material?.MaterialBase
                If r Is Nothing Then Continue For
                Dim pass = mesh.MeshData.Material.Refraction(FrameIsSse)
                If Not pass.Applies OrElse Not pass.Drawn Then Continue For
                If Not abierto Then ParentControl.BeginRefractionNormals(FrameIsSse) : abierto = True
                If mesh.RenderRefractionNormals(ParentControl.RefractionNormalsProgram(FrameIsSse), projection, camera, FrameIsSse, lodScale) Then FrameRefractionActive = True
            Next
            If abierto Then ParentControl.EndRefractionNormals()
        End If
    End Sub

    ''' <summary>The engines' per-slot filter state in the frame (SamplerLaw: an inherited slot reads it): reset to FILT 3 every
    ''' frame (SSE 0x141007B70, FO4 0x141816080).</summary>
    Friend ReadOnly FrameSamplerFilter(15) As Integer

    ''' <summary>The refraction-normals pass drew at least one shape this frame: RenderScene runs ISRefraction (the engine's
    ''' IsActive = the list-5 flag, SSE [0x143698B31], FO4 [0x143E71D99]).</summary>
    Friend Property FrameRefractionActive As Boolean

    ''' <summary>GATE ONLY (no UI): True skips the refraction passes (the refracting shapes then show nothing).</summary>
    Friend Shared GateDisableRefraction As Boolean = False

    ''' <summary>GATE ONLY (no UI): True skips the SSE SAO composite over the opaque scene.</summary>
    Friend Shared GateDisableSseComposite As Boolean = False

    Private Structure OverlayItem
        Public Mesh As RenderableMesh
        Public Layer As OverlayMaterialLayer
    End Structure

    ''' <summary>The G-buffer programs that already have this frame's shadow uniforms (uniforms are per program).</summary>
    Private ReadOnly _frameShadowPrograms As New HashSet(Of Shader_Base_Class)

    ''' <summary>The frame's shadow uniforms on a G-buffer record's program, the first time the frame draws with it: the SAME values
    ''' RenderAll uploads to the game shader (propuesta B: the G-buffer writes the shadow factor of every rig light).</summary>
    Friend Sub EnsureFrameShadowUniforms(program As Shader_Base_Class)
        If Not _frameShadowPrograms.Add(program) Then Return
        UploadShadowUniforms(program, _shadowFits, _shadowCount, _shadowSlots, _shadowUvScale,
                             ParentControl.ShadowTarget, _shadowActive,
                             TextureUnit.Texture14, PreviewShadowSettings.NormalBiasTexels,
                             PreviewShadowSettings.DepthBiasTexels * _shadowFits(0).TexelWorld)
    End Sub

    ''' <summary>What the FO4 light and composite passes receive this frame (propuesta D; Tools/re-docs/RE_FO4_DEFERRED_FRAME_2026-10-03.md
    ''' and opaque-fo4/transcripcion/notas.md 4.2, D9). Engine view = GL view with z negated. cb12[20..23] = inverse(A * P_e),
    ''' cb12[24..27] = inverse(B * P_e) (D9: A maps z to 1.01 z - 0.01 w, B to 100 z; P_e = the GL projection's rows 0, 1,
    ''' (2 + 3) / 2, 3 times diag(1, 1, -1, 1)); cb12[12..14] = the rows of the engine view -&gt; world rotation. The sun = the rig's
    ''' key (no IMGS Sunlight Scale, user decision rev-23 b); the fills = the other three rig lights (rev-21 b: shadow channel k + 1 of
    ''' the G-buffer's shadow RT); the ambient from the RAW rig colours: sky -&gt; world Z+ facing, ground -&gt; Z-, the sides their
    ''' mean (rev-22), the 3107 PS applies pow 2.2 after the dot.</summary>
    Private Function BuildFo4DeferredFrame(projection As Matrix4, view As Matrix4) As Fo4DeferredFrame
        Dim nf = ParentControl.FrameNearFar
        ' Column-vector rows of the GL projection = the columns of OpenTK's row-vector matrix.
        Dim flipZ = Function(r As Vector4d) New Vector4d(r.X, r.Y, -r.Z, r.W)
        Dim p0 = flipZ(CType(projection.Column0, Vector4d)), p1 = flipZ(CType(projection.Column1, Vector4d))
        Dim p3 = flipZ(CType(projection.Column3, Vector4d))
        Dim p2 = flipZ((CType(projection.Column2, Vector4d) + CType(projection.Column3, Vector4d)) * 0.5)
        Dim rowsOf = Function(zRow As Vector4d) As Vector4()
                         Dim m As New Matrix4d(p0, p1, zRow, p3)
                         m.Invert()
                         Return {CType(m.Row0, Vector4), CType(m.Row1, Vector4), CType(m.Row2, Vector4), CType(m.Row3, Vector4)}
                     End Function
        ' Engine view -> world: R = inverse(GL view rotation) * diag(1, 1, -1); its rows (column-vector convention).
        Dim v3 As New Matrix3d(view.M11, view.M12, view.M13, view.M21, view.M22, view.M23, view.M31, view.M32, view.M33)
        ' OpenTK stores the row-vector matrix: the column-vector view rotation is its transpose, whose inverse is v3 itself
        ' when orthonormal; inverted in general, then the columns' z negated (diag(1, 1, -1) on the right).
        Dim rcv = Matrix3d.Transpose(v3)
        rcv.Invert()
        Dim rRow = Function(i As Integer) New Vector3d(rcv(i, 0), rcv(i, 1), -rcv(i, 2))
        Dim r0 = rRow(0), r1 = rRow(1), r2 = rRow(2)
        Dim toEngineView = Function(worldDir As Vector3) As Vector3
                               Dim g = (New Vector4(worldDir, 0.0F) * view).Xyz
                               Return New Vector3(g.X, g.Y, -g.Z)
                           End Function
        Dim rig = Config_App.Current.ActiveLights()
        Dim sky = rig.AmbientSkyDiffuse()
        Dim ground = rig.AmbientGroundDiffuse() * CSng(Math.Pow(rig.AmbientGroundLevel, 1.0 / 2.2))
        Dim amb(2) As Vector4
        For c = 0 To 2
            Dim dz = 0.5 * (sky(c) - ground(c))
            amb(c) = New Vector4(CSng(dz * r2.X), CSng(dz * r2.Y), CSng(dz * r2.Z), 0.5F * (sky(c) + ground(c)))
        Next
        Return New Fo4DeferredFrame With {
            .W = ParentControl.Width, .H = ParentControl.Height, .Near = nf.X, .Far = nf.Y,
            .InvProj = rowsOf(New Vector4d(1.01 * p2 - 0.01 * p3)),
            .InvProj1P = rowsOf(100.0 * p2),
            .ViewToWorld = {New Vector4(CType(r0, Vector3), 0.0F), New Vector4(CType(r1, Vector3), 0.0F), New Vector4(CType(r2, Vector3), 0.0F)},
            .SunDir = toEngineView(_frameLights.KeyDir), .SunColor = _frameLights.KeyDiffuse,
            .SunCastsShadow = _shadowActive AndAlso _shadowSlots(0) >= 0,
            .FillDir = {toEngineView(_frameLights.Fill0Dir), toEngineView(_frameLights.Fill1Dir), toEngineView(_frameLights.BackDir)},
            .FillColor = {_frameLights.Fill0Diffuse, _frameLights.Fill1Diffuse, _frameLights.BackDiffuse},
            .FillShadowChannel = {1, 2, 3},
            .Ambient = amb}
    End Function

    ''' <summary>The z-prepass and the opaque lists of a frame, in the engine's order (RenderAll). Fallout 4's deferred frame draws
    ''' them inside its G-buffer stage (FrameGBufferStage).</summary>
    Private Sub DrawOpaqueStage(projection As Matrix4, camera As OrbitCamera)
        FrameBillboardEffectDrawn = False
        Dim enPrepass = Function(md As RenderableMesh.MaterialData) As Boolean
                            If md?.MaterialBase Is Nothing Then Return False
                            Return If(FrameIsSse, md.SsePass().InPrepass,
                                      md.Fo4Pass().InPrepass AndAlso Not md.Fo4PreviewGap(ParentControl.SharedFo4Deferred))
                        End Function
        If Not GateDisablePrepass Then
            GL.ColorMask(False, False, False, False)
            For Each mesh In meshes
                If mesh Is Nothing OrElse mesh.MeshData?.Shape Is Nothing OrElse mesh.MeshData.Shape.Wireframe Then Continue For
                If Not RenderableMesh.IsAABBInFrustum(mesh.BoundsMin, mesh.BoundsMax, _framePlanes) Then Continue For
                If Not enPrepass(mesh.MeshData.Material) Then Continue For
                mesh.Render(projection, camera, ssePrepass:=True)
            Next
            For Each it In AllOverlayItems()
                If enPrepass(it.Mesh.OverlayMaterialData(it.Layer)) Then it.Mesh.RenderOverlayLayer(projection, camera, it.Layer, ssePrepass:=True)
            Next
            GL.ColorMask(True, True, True, True)
            FrameDepthDirty = True
            If FrameIsSse AndAlso Not GateDisableSoft Then
                ParentControl.CopySceneDepth()
                FrameSceneDepthValid = True
            End If
        End If

        FrameAoListOn = True
        ' 1. OPAQUE — the game's opaque order (SSE: batch / lists 9-8-1-0; FO4: G-buffer techniques, alpha test included; chunk C8)
        For Each mesh In OpaqueMeshes
            ' O3.3: Skip meshes whose AABB is entirely outside the view frustum
            If Not RenderableMesh.IsAABBInFrustum(mesh.BoundsMin, mesh.BoundsMax, _framePlanes) Then Continue For
            mesh.Render(projection, camera)
        Next

        ' OVERLAY LAYERS (LooksMenu / RaceMenu): each layer is a shape of its own in the engine and goes to the
        ' group its MATERIAL routes it to, like any shape (no final pass): opaque, opaque decal, blended
        ' decal, or the alpha list keyed with its base shape's depth (the clone shares the base's bound).
        For Each it In _ovlOpaque : it.Mesh.RenderOverlayLayer(projection, camera, it.Layer) : Next

        ' SSE list 0xD (billboard effects): after the opaque lists, before the decals (0x14151EF40).
        If FrameIsSse Then DrawBucket(BillboardMeshes, _ovlBillboard, projection, camera)

        FrameAoListOn = True
        ' 3. DECAL — opaque decals, then blended decals (and decal overlay layers) in their own group.
        For Each mesh In DecalMeshes
            If Not RenderableMesh.IsAABBInFrustum(mesh.BoundsMin, mesh.BoundsMax, _framePlanes) Then Continue For
            mesh.Render(projection, camera)
        Next
        For Each it In _ovlDecal : it.Mesh.RenderOverlayLayer(projection, camera, it.Layer) : Next
        FrameAoListOn = False
        ' THE BASE OF THE TRANSLUCENT DECALS, BOTH GAMES (user decisions 5-oct-2026 and 7-oct-2026; an app pass, neither engine has
        ' one; DecalBaseTarget): where nothing opaque is behind them, the farthest decal of group 3 that paints a pixel lays a plain
        ' surface there - albedo = the preview's background, normal = its geometric normal - lit as any surface of its game, with that
        ' decal's opacity (the base follows the decal's alpha: RenderableMesh.DecalBaseOpacity), and the decals' unchanged draws below
        ' blend over it; the post mixes the background by the coverage of base and decals. Fallout 4 also gives those pixels that
        ' decal's depth, so that the decals pass LESS_EQUAL and the lights and composites shade them; Skyrim SE keeps its depth (its
        ' decals do not write it). Where something is behind, nothing changes. Only when the frame has translucent decals. Every
        ' depth draw first, then every surface draw (DecalBaseTarget.BeginSurfaces: a blended surface cannot be overwritten by a
        ' farther one). The base draws go through ApplyMaterial as the colour draws do: the per-slot filter state they leave
        ' (SamplerLaw, an inherited slot reads it) is put back, so the colour draws read the state they read without the base pass.
        If (DecalBlendedMeshes.Count > 0 OrElse _ovlDecalBlended.Count > 0) AndAlso
           (FrameGBufferStage AndAlso Not GateFo4SkipDecalBase OrElse FrameIsSse AndAlso Not GateSseSkipDecalBase) Then
            Dim filters = CType(FrameSamplerFilter.Clone(), Integer())
            Dim baseDraws = Sub()
                                DrawBucket(DecalBlendedMeshes, _ovlDecalBlended, projection, camera, DecalBaseStep.Depth)
                                ParentControl.FrameDecalBase.BeginSurfaces()
                                DrawBucket(DecalBlendedMeshes, _ovlDecalBlended, projection, camera, DecalBaseStep.Surface)
                            End Sub
            If FrameGBufferStage Then
                If ParentControl.BeginFo4DecalBase() Then
                    baseDraws()
                    ParentControl.EndFo4DecalBase()
                    ' FO4's SOFT reads the live depth: the base wrote it.
                    FrameDepthDirty = True
                End If
            ElseIf ParentControl.BeginSseDecalBase() Then
                baseDraws()
                ParentControl.EndSseDecalBase()
            End If
            Array.Copy(filters, FrameSamplerFilter, filters.Length)
        End If
        If Not GateDecalColourSkip Then DrawBucket(DecalBlendedMeshes, _ovlDecalBlended, projection, camera)
    End Sub

    ''' <summary>The step of the translucent decals' base pass a draw makes (RenderableMesh.Render / RenderOverlayLayer).</summary>
    Public Enum DecalBaseStep
        ''' <summary>The colour draw.</summary>
        None
        ''' <summary>The base depth draw (ApplyDecalBaseState).</summary>
        Depth
        ''' <summary>The base surface draw, after every depth draw (DrawDecalBaseSurface).</summary>
        Surface
    End Enum

    ''' <summary>One bucket in shape order, then its overlay layers (frustum-culled like the others). <paramref name="decalBase"/>: the
    ''' translucent decals' base draws of that step (RenderableMesh.Render decalBase) instead of the colour draws.</summary>
    Private Sub DrawBucket(bucket As List(Of RenderableMesh), overlays As List(Of OverlayItem), projection As Matrix4, camera As OrbitCamera,
                           Optional decalBase As DecalBaseStep = DecalBaseStep.None)
        For Each mesh In bucket
            If Not RenderableMesh.IsAABBInFrustum(mesh.BoundsMin, mesh.BoundsMax, _framePlanes) Then Continue For
            mesh.Render(projection, camera, decalBase:=decalBase)
        Next
        For Each it In overlays : it.Mesh.RenderOverlayLayer(projection, camera, it.Layer, decalBase:=decalBase) : Next
    End Sub

    ''' <summary>True after the scene depth was copied in THIS frame (RenderAll for SSE, ApplyMaterial for FO4).</summary>
    Friend Property FrameSceneDepthValid As Boolean

    ''' <summary>SSE: an effect pass of the billboard list (0xD) was drawn this frame - its RestoreGeometry left depth
    ''' mode 4 for the next one (SseRenderPassLaw.BillboardInheritsEqual).</summary>
    Friend Property FrameBillboardEffectDrawn As Boolean

    ''' <summary>SSE: the SAO normals target's write state of the list being drawn (SseRenderPassLaw.AoNormalWrite): RenderAll opens
    ''' each list with its mode, ApplyMaterial carries it from draw to draw.</summary>
    Friend FrameAoListOn As Boolean

    ''' <summary>Something wrote depth since the last scene-depth copy (set at the frame start - clear and floor - and by
    ''' every draw that writes depth, ApplyMaterial): FO4's SOFT reads the live depth, so it copies again only then.</summary>
    Friend Property FrameDepthDirty As Boolean

    ''' <summary>GATE ONLY (no UI): True turns the scene-depth copy off, so every effect draws without SOFT. ShadowGate
    ''' --soft-scene compares a frame with and without it.</summary>
    Friend Shared GateDisableSoft As Boolean = False

    ''' <summary>GATE ONLY (no UI): True draws without the z-prepass of either game: SSE's opaque batch with LESS_EQUAL +
    ''' write instead of EQUAL, FO4's eligible lighting writing its own depth. ShadowGate --sse-prepass compares the two on
    ''' opaque shapes without alpha test (they must be identical).</summary>
    Friend Shared GateDisablePrepass As Boolean = False

    ''' <summary>GATE ONLY (no UI): ShadowGate --fo4-deferred-scene. True throws once right after BeginFo4GBuffer (then resets
    ''' itself): the aborted G-buffer stage whose GL state EndFo4Stage must undo.</summary>
    Friend Shared GateFo4ThrowInStage As Boolean = False

    ''' <summary>GATE ONLY (no UI): ShadowGate --fo4-decal-base mutant. True skips the base pass of the translucent decals.</summary>
    Friend Shared GateFo4SkipDecalBase As Boolean = False

    ''' <summary>GATE ONLY (no UI): ShadowGate --decal-base-surface. True skips Skyrim SE's base pass of the translucent decals (the
    ''' frame before the base).</summary>
    Friend Shared GateSseSkipDecalBase As Boolean = False

    ''' <summary>GATE ONLY (no UI): ShadowGate --decal-base-surface mutant. True skips the base SURFACE draws (FO4: the depth base
    ''' of proposal B alone).</summary>
    Friend Shared GateDecalBaseNoSurface As Boolean = False

    ''' <summary>GATE ONLY (no UI): ShadowGate --decal-base-surface mutant "opaque base": every decal's base with opacity 1
    ''' (RenderableMesh.DecalBaseOpacity 0), whatever its alpha - the base of propuesta v1.</summary>
    Friend Shared GateDecalBaseOpaque As Boolean = False

    ''' <summary>GATE ONLY (no UI): ShadowGate --decal-base-surface mutant. True skips Fallout 4's coverage draws of the base.</summary>
    Friend Shared GateDecalBaseNoCoverage As Boolean = False

    ''' <summary>GATE ONLY (no UI): ShadowGate --decal-base-surface. True skips the colour draws of the translucent decals (the
    ''' frame shows the base surface alone).</summary>
    Friend Shared GateDecalColourSkip As Boolean = False

    ''' <summary>GATE ONLY (no UI): ShadowGate --fo4-decal-base mutant. True draws the base without the decals' discards (a G-buffer
    ''' record with AlphaScale (1, 0), whose kill then never fires; a game-shader decal with uDecalBaseMode 0).</summary>
    Friend Shared GateDecalBaseNoDiscard As Boolean = False

    ''' <summary>GATE ONLY (no UI): ShadowGate --fo4-decal-base mutant. True draws every game-shader decal's base with uDecalBaseMode 1
    ''' (the SRC_ALPHA discard), whatever its blend.</summary>
    Friend Shared GateDecalBaseModeOneForAll As Boolean = False

    ''' <summary>GATE ONLY (no UI): ShadowGate --fo4-decal-base. True draws the colour pass of group 3 with EQUAL (gate b1: against the
    ''' base, it covers what LESS_EQUAL covers only if the base is the decal's own depth).</summary>
    Friend Shared GateDecalColourEqual As Boolean = False

    ''' <summary>GATE ONLY (no UI): ShadowGate --fo4-decal-base mutant. True draws the base without the decal's depth bias.</summary>
    Friend Shared GateDecalBaseNoBias As Boolean = False

    Private ReadOnly _ovlOpaque As New List(Of OverlayItem)
    Private ReadOnly _ovlDecal As New List(Of OverlayItem)
    Private ReadOnly _ovlDecalBlended As New List(Of OverlayItem)
    Private ReadOnly _ovlBlended As New List(Of OverlayItem)
    Private ReadOnly _ovlBillboard As New List(Of OverlayItem)
    Private ReadOnly _ovlForwardEffect As New List(Of OverlayItem)
    Private ReadOnly _ovlNoColourPass As New List(Of OverlayItem)

    ''' <summary>Every overlay layer of the frame (CollectOverlayItems' lists, in their draw order).</summary>
    Private Function AllOverlayItems() As IEnumerable(Of OverlayItem)
        Return _ovlOpaque.Concat(_ovlDecal).Concat(_ovlDecalBlended).Concat(_ovlBlended).Concat(_ovlBillboard).Concat(_ovlForwardEffect).Concat(_ovlNoColourPass)
    End Function

    ''' <summary>GATE ONLY (no UI): ShadowGate --fo4-undrawn-notice mutant. True skips the frame's notice census.</summary>
    Friend Shared GateNoUndrawnCensus As Boolean = False

    ''' <summary>GATE ONLY (no UI): True makes MaterialData.PreviewAlpha return the raw alpha (the frame as the game's law alone would
    ''' draw it, L1c). ShadowGate --sse-undrawn-notice (A/B), --fo4-undrawn-notice (6a), --fo4-gbuffer-scenes (10 z0) and
    ''' [sombra-alpha] (the caster choice and the opacity sweep measure the shadow on the raw alpha; C2 v3 D4).</summary>
    Friend Shared GateRawPreviewAlpha As Boolean = False

    ''' <summary>The frame's notice census (user decision 5-oct-2026): every shape in the view and every overlay layer of the frame
    ''' (CollectOverlayItems: their shape in the view) asks RenderableMesh.ReportUndrawn once - the preview's gaps, the shapes the
    ''' game does not draw and the ones it does not draw while the preview does, each with its reason. Runs before the frame draws:
    ''' the notice does not depend on which pass reaches the shape. A precombined shape has no vertices: the census lists it without a
    ''' frustum test, whatever its box. ShadowGate --fo4-undrawn-notice (7a-bypass) proves it in a scene with a finite bound (the
    ''' precombined NIF with the musket): finite planes, the empty-geometry box outside them, every precombined mesh still listed.</summary>
    Private Sub ReportUndrawnShapes()
        For Each mesh In meshes
            If mesh?.MeshData?.Shape Is Nothing Then Continue For
            If Not mesh.IsPrecombinedWithoutGeometry() AndAlso Not RenderableMesh.IsAABBInFrustum(mesh.BoundsMin, mesh.BoundsMax, _framePlanes) Then Continue For
            mesh.ReportUndrawn(Nothing)
        Next
        For Each it In AllOverlayItems()
            it.Mesh.ReportUndrawn(it.Layer)
        Next
    End Sub

    ''' <summary>Routes every overlay layer of the visible shapes to its group with the SAME rule as
    ''' RebuildRenderBuckets (decal before blend before test), from the layer's own material. List order is kept
    ''' (meshes in model order, layers in the resolver's draw order). No layers: every list empty (Wardrobe
    ''' Manager, NPCs without overlays).</summary>
    Private Sub CollectOverlayItems()
        _ovlOpaque.Clear() : _ovlDecal.Clear() : _ovlDecalBlended.Clear() : _ovlBlended.Clear()
        _ovlBillboard.Clear() : _ovlForwardEffect.Clear() : _ovlNoColourPass.Clear()
        For Each mesh In meshes
            If mesh Is Nothing OrElse mesh.MeshData Is Nothing Then Continue For
            Dim layers = mesh.MeshData.Shape?.OverlayLayers
            If layers Is Nothing OrElse layers.Count = 0 Then Continue For
            If Not RenderableMesh.IsAABBInFrustum(mesh.BoundsMin, mesh.BoundsMax, _framePlanes) Then Continue For
            For Each layer In layers
                If layer Is Nothing OrElse layer.Material Is Nothing Then Continue For
                Dim md = mesh.OverlayMaterialData(layer)
                Dim it As New OverlayItem With {.Mesh = mesh, .Layer = layer}
                If md.MaterialBase Is Nothing Then Continue For
                Select Case RouteBucket(md, False)
                    Case RenderBucket.Opaque : _ovlOpaque.Add(it)
                    Case RenderBucket.Decal : _ovlDecal.Add(it)
                    Case RenderBucket.DecalBlended : _ovlDecalBlended.Add(it)
                    Case RenderBucket.Blended : _ovlBlended.Add(it)
                    Case RenderBucket.Billboard : _ovlBillboard.Add(it)
                    Case RenderBucket.ForwardEffect : _ovlForwardEffect.Add(it)
                    Case Else : _ovlNoColourPass.Add(it)
                End Select
            Next
        Next
    End Sub
End Class
Public Class FloorRenderer
    Implements IDisposable

    Friend Shared Property MedirCostoGpu As Boolean
    Friend Shared ReadOnly Property NsCostoGpu As Long
        Get
            Return _nsCostoGpu
        End Get
    End Property
    Private Shared _nsCostoGpu As Long
    Private queryTiempo As Integer
    Private queryEnVuelo As Boolean

    Private ReadOnly ParentControl As PreviewControl
    Private vao As Integer
    Private vbo As Integer
    Private vertexCount As Integer

    Public Initialized As Boolean = False
    ''' <summary>Fachada sobre la unica fuente de verdad: Config_App.Settings_RenderGrid.Enabled.
    ''' Todos los previews y todas las puertas de UI leen y escriben el mismo valor.</summary>
    Public Property Enabled As Boolean
        Get
            Return Config_App.Current IsNot Nothing AndAlso Config_App.Current.Settings_RenderGrid.Enabled
        End Get
        Set(value As Boolean)
            If Config_App.Current Is Nothing Then Return
            Dim floor = Config_App.Current.Settings_RenderGrid
            floor.Enabled = value
            Config_App.Current.Settings_RenderGrid = floor
        End Set
    End Property
    Public Property Size As Single = 400.0F
    Public Property StepSize As Single = 10.0F
    Public Property Color As Color = Color.FromKnownColor(KnownColor.ControlLight)

    Public Sub New(parentControl As PreviewControl)
        Me.ParentControl = parentControl
    End Sub

    Private Sub CreateGeometry()
        If vao > 0 Then GL.DeleteVertexArray(vao) : vao = 0
        If vbo > 0 Then GL.DeleteBuffer(vbo) : vbo = 0

        ' Unit quad, counter-clockwise from above. Size and tile spacing are uniforms/model state, so
        ' changing render-grid settings no longer reallocates GPU geometry.
        Dim vertices As Single() = {
            -0.5F, -0.5F, 0.0F, 0.5F, -0.5F, 0.0F, 0.5F, 0.5F, 0.0F,
            -0.5F, -0.5F, 0.0F, 0.5F, 0.5F, 0.0F, -0.5F, 0.5F, 0.0F
        }
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

        Dim cronometrando As Boolean = False
        If MedirCostoGpu Then
            If queryTiempo = 0 Then queryTiempo = GL.GenQuery()
            If queryEnVuelo Then
                Dim listo As Integer
                GL.GetQueryObject(queryTiempo, GetQueryObjectParam.QueryResultAvailable, listo)
                If listo <> 0 Then
                    Dim ns As Long
                    GL.GetQueryObject(queryTiempo, GetQueryObjectParam.QueryResult, ns)
                    _nsCostoGpu = ns
                    queryEnVuelo = False
                End If
            End If
            If Not queryEnVuelo Then
                GL.BeginQuery(QueryTarget.TimeElapsed, queryTiempo)
                cronometrando = True
            End If
        End If

        Dim shader = ParentControl.SharedFloorShader

        shader.Use()

        GL.Disable(EnableCap.Blend)
        GL.Enable(EnableCap.DepthTest)
        GL.DepthMask(True)
        GL.Enable(EnableCap.CullFace)
        GL.CullFace(TriangleFace.Back)

        Dim view As Matrix4 = camera.GetViewMatrix()
        Dim safeSize = Math.Max(Size, 1.0F)
        Dim safeStep = Math.Max(StepSize, 0.001F)
        Dim model As Matrix4 = Matrix4.CreateTranslation(0.0F, 0.0F, CSng(offsetZ)) *
                               Matrix4.CreateScale(safeSize, safeSize, 1.0F)
        Dim background = Config_App.Current.Setting_BackColor()
        Dim lights = ParentControl.Model.FrameLights

        shader.SetMatrix4("matProjection", projection)
        shader.SetMatrix4("matView", view)
        shader.SetMatrix4("matModel", model)
        shader.SetFloat("tileStep", safeStep)
        shader.SetFloat("floorHalfSize", safeSize * 0.5F)
        ' El fondo COMPLETO, de la misma funcion que el quad de fondo y la pasada de post: el piso se funde
        ' contra el fondo en el horizonte y tiene que evaluar la misma funcion en el mismo pixel.
        ParentControl.SubirUniformsDeFondo(shader)
        Dim hdr = ParentControl.Model.FrameIsHdr
        shader.SetBool("bHdrTarget", hdr)
        ' The 2.3.8 curve of a direct frame wants linear: FO4 scene linear, SSE raw (Shader_Base_Class.SceneToLinearExponent).
        shader.SetFloat("uSceneToLinear", Shader_Base_Class.SceneToLinearExponent(ParentControl.CurrentShader Is ParentControl.SharedSSEShader))
        ' The floor's colours live in the space of the game's scene (FO4 linear, SSE raw).
        Dim sseFrame = ParentControl.CurrentShader Is ParentControl.SharedSSEShader
        shader.SetVector3("backgroundLinear", Shader_Base_Class.MaterialColor(background, sseFrame))
        shader.SetVector3("groutColorLinear", Shader_Base_Class.MaterialColor(Color, sseFrame))
        shader.SetVector3("cameraPosition", camera.GetEyePosition())
        shader.SetVector3("ambientSky", lights.AmbientSky)
        shader.SetVector3("ambientGround", lights.AmbientGround)
        Dim upLighting = lights.AmbientSky
        For i = 0 To PreviewShadowSettings.MaxShadowLights - 1
            upLighting += lights.DifusoDeLuz(i) * Math.Max(lights.DirDeLuz(i).Z, 0.0F)
        Next
        Dim upLuma = Shader_Base_Class.Luma(upLighting)
        ' Display target only: the HDR frame is exposed by the post (the image space's auto exposure).
        shader.SetFloat("floorExposure", If(hdr, 1.0F, Math.Clamp(0.72F / Math.Max(upLuma, 0.001F), 0.55F, 1.8F)))
        For i = 0 To PreviewShadowSettings.MaxShadowLights - 1
            shader.SetVector3($"lightDiffuse[{i}]", lights.DifusoDeLuz(i))
            shader.SetVector3($"lightDirection[{i}]", lights.DirDeLuz(i))
        Next

        GL.BindVertexArray(vao)
        GL.DrawArrays(PrimitiveType.Triangles, 0, vertexCount)
        GL.BindVertexArray(0)

        GL.UseProgram(0)
        GL.Enable(EnableCap.CullFace)
        If cronometrando Then
            GL.EndQuery(QueryTarget.TimeElapsed)
            queryEnVuelo = True
        End If
    End Sub

    Public Sub Rebuild()
        CreateGeometry()
        Initialized = (vao <> 0 AndAlso vbo <> 0 AndAlso vertexCount > 0)
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        If vao > 0 Then GL.DeleteVertexArray(vao) : vao = 0
        If vbo > 0 Then GL.DeleteBuffer(vbo) : vbo = 0
        If queryTiempo > 0 Then GL.DeleteQuery(queryTiempo) : queryTiempo = 0
        queryEnVuelo = False
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
