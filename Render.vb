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

    ''' <summary>Las dos fuentes GLSL del overlay de texto, IZADAS A CONSTANTES.
    ''' <para>⛔ Eran variables LOCALES dentro de este mismo <c>Sub</c>, y eso las dejaba fuera del gate
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
    ''' ⭐ El detail NO es el término del soft-light: es el multiplicador AMPLIFICADO
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
    ''' <summary>Default del SUBSURFACE (_sk, texture-set slot 2) de una cabeza FaceGen cuando falta: NEGRO.
    ''' RE byte-level: BSLightingShaderMaterialFacegen slot#10 (0x1414BA8B0) rellena subsurface(+0xB0) con
    ''' DefHeightMap (fill 0xFF000000 = negro); mapeo miembro↔slot verificado en slot#8 (0x1414BA6E0):
    ''' +0xB0↔índice 2 (_sk), +0xA8↔3 (detail), +0xA0↔6 (tint). Negro ⇒ SSS = 0 (sin subsurface glow) —
    ''' distinto del fallback no-facegen del shader (softMask=albedo), que queda intacto.</summary>
    Public defaultFacegenSubsurfaceTex As Integer
    Public Property BrushRadiusPx As Integer = 5
    Public Property InvertMasking As Boolean = False


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

        Dim texID As Integer = GL.GenTexture()
        GL.BindTexture(TextureTarget.Texture2D, texID)

        ' Alineación segura para ESTA subida, y despues se devuelve. Antes se dejaba el UNPACK_ALIGNMENT
        ' global en 1 para toda la vida del contexto: como esto corre en GenerateDefaultTextures (OnLoad),
        ' el default del contexto pasaba a ser 1, y el `Finally` del loader de DDS —que restauraba el valor
        ' que habia leido— terminaba restaurando algo que ya no era el default de OpenGL.
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

        ' 4×4 normal map por defecto: (0.5,0.5,1) ? (128,128,255)
        defaultNormalTex = CreateColorTexture(4, 4, 128, 128, 128, 128)

        ' default FaceGen detail = BSShader_DefFacegenDetail del motor (byte-exact, ver campo).
        ' Uniforme 64 = 0.251 (NO 0.5/identidad, NO la Bayer 0.1235 de BSShader_DitheringNoise).
        ' ⚠️ Se crea con MIPMAPS + LINEAR + REPEAT (como una textura real cargada, el blankdetailmap real es
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
        ' ⛔ Este loop NO fija UNPACK_ALIGNMENT y venia viviendo del 1 que dejaba `CreateColorTexture` (que
        ' lo ponia y no lo devolvia). Al arreglar aquello, acá queda el 4 por default — y funciona igual de
        ' pura casualidad: 4 px × RGBA son 16 bytes por fila, multiplo de 4. Con un ancho o un formato que
        ' no cierre en 4 bytes, las caras saldrian corridas. Se fija explicito para no depender de eso.
        ' ⛔ Y SE CAPTURA EL PREVIO, no se asume el default. Doce lineas mas arriba `CreateColorTexture` acaba
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


    ' ⛔ Sin consumidor fuera de este ensamblado (única referencia: su propia declaración).
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
    ''' <para>⛔⭐ SE SELLA EN LA RECARGA, no en el diálogo. La primera version guardaba el valor dentro de
    ''' <see cref="ApplyRenderSettingsFromConfig"/> y por eso el PRIMER cambio de skinning de la sesion se
    ''' tragaba el evento: sin valor previo no habia con que comparar, y el gesto mas obvio —abrir el
    ''' dialogo y destildar GPU skinning— era justo el que no avisaba, dejando la cara oscura. Sellando lo
    ''' que uso el ultimo frame reconstruido, la comparacion es contra lo que hay EN PANTALLA, que es lo
    ''' que importa, y no depende de por donde se haya tocado la config (el menu de la camara tambien la
    ''' escribe).</para></summary>
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
    ''' <para>⛔ Vive ACA y no en cada app: hasta ahora esto lo hacia a mano el boton "Apply to rendered
    ''' project" de Config_Form de Wardrobe Manager, y FO4_NPC_Manager —que comparte la misma libreria y
    ''' la misma config— no tenia equivalente. Con el dialogo de render compartido, la copia se volvia
    ''' dos.</para>
    '''
    ''' <para>⛔ Marca <c>Force</c> —que es una RECARGA COMPLETA: Clean, esqueleto, LoadShapesParallel,
    ''' TBN, welding, morphs y subida a GPU— SOLO si cambio algo que la geometria mira. Antes la marcaba
    ''' siempre, y como el diálogo escribe en cada <c>ValueChanged</c>, tipear "500" en el tamano del piso
    ''' costaba TRES recargas completas de un NPC con outfit. La camara y la grilla no tocan geometria.
    ''' Tampoco se llama <c>Floor.Rebuild()</c> de prepo: recrea VAO/VBO y arma ~1000 floats.</para>
    ''' <para>No marca <c>Camera</c>: mover la camara del usuario sin que lo pida es una molestia.</para>
    ''' </summary>
    Public Sub ApplyRenderSettingsFromConfig()
        If _isTearingDown OrElse Me.IsDisposed Then Exit Sub
        Dim m = Me.Model
        If m Is Nothing Then Exit Sub

        m.RecalculateNormals = Config_App.Current.Setting_RecalculateNormals
        m.SingleBoneSkinning = Config_App.Current.Setting_SingleBoneSkinning
        ' ⛔⭐ EL ESPEJO SON TRES, NO DOS: Config -> Model -> Intent. `Model.RecalculateNormals` lo lee la
        ' EXTRACCION de geometria (ExtractSkinnedGeometry) y `Intent.RecalculateNormals` lo lee el paso de
        ' MORPHS (ApplyMorphPlan). Empujar solo el Model dejaba la casilla a medias: en una escena sin
        ' morphs se veia el cambio —el full reload re-extrae— y en Wardrobe Manager, donde el cuerpo SIEMPRE
        ' esta morfeado por el preset, no se veia NADA. Ese era el sintoma reportado: la casilla de la barra
        ' principal funcionaba y la de este dialogo no, porque la de la barra pasa por Update_Render, que
        ' refresca el Intent (`intent.RecalculateNormals = ctrl.Model.RecalculateNormals`), y este camino no.
        ' ⛔ Una sonda que llame a ESTA funcion sobre un NIF sin morphs NO lo caza: da pixeles igual. El
        ' chequeo que discrimina es el INVARIANTE de abajo (los tres espejos de acuerdo), no un conteo.
        Intent.RecalculateNormals = Config_App.Current.Setting_RecalculateNormals

        Dim ahora = LeerGeomDeConfig()
        ' Sin recarga previa no hay nada en pantalla que corregir, y la que venga ya va a usar los valores
        ' nuevos: ni evento ni Force.
        Dim geomCambio As Boolean = _geomAplicada.HasValue AndAlso Not _geomAplicada.Value.Equals(ahora)
        Dim cambioSkinning As Boolean = _geomAplicada.HasValue AndAlso _geomAplicada.Value.Gpu <> ahora.Gpu

        ' ⛔⭐ EL CAMBIO DE SKINNING TIENE QUE AVISAR, no alcanza con ensuciar la geometria. La libreria
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
            ' Solo el tamano y el paso son GEOMETRIA de la grilla. Enabled y Color no: el primero es un
            ' `If` en el draw y el segundo es un uniform por draw. Rebuild borra y recrea VAO/VBO.
            Dim reconstruir As Boolean = m.Floor.Size <> CSng(g.Size) OrElse m.Floor.StepSize <> CSng(g.StepSize)
            m.Floor.Enabled = g.Enabled
            m.Floor.Size = CSng(g.Size)
            m.Floor.StepSize = CSng(g.StepSize)
            m.Floor.Color = col
            If reconstruir Then m.Floor.Rebuild()
        End If

        If geomCambio Then
            Intent.MarkDirty(RenderDirtyFlags.Force)
            InvalidateRender()
        Else
            ' ⛔ REPINTAR SIEMPRE, no solo cuando cambio el piso. Setting_DrawHiddenSegments se lee en el
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
    ''' ⛔ No alcanza con llamar a <see cref="Processing_Status"/>: ese cartel es un frame suelto y
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
    ''' <returns>True si el contexto quedó current. ⛔ DEVUELVE Boolean porque los llamadores que
    ''' BORRAN handles necesitan saberlo: los nombres de GL son por contexto y borrar creyendo que se hizo
    ''' current, cuando no se pudo, mata texturas de OTRO preview.</returns>
    ''' <remarks>⛔ `MakeCurrent()` estaba FUERA del `Try`: el `Catch` sólo cubría el chequeo de
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
            ' ⛔ El sello describe lo que hay EN PANTALLA. Con la escena vacia no hay nada que corregir, y
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
            ' â›” La nota vieja decia "gateados por LogLazy; el Stopwatch es ~ns": las dos mitades eran falsas.
            ' LogLazy hace lazy el STRING, no el CALCULO, asi que esto corria ENTERO en release, y no son unos
            ' ns sino DOS Stopwatch nuevos POR MALLA POR FRAME mas ~9 lecturas de .Elapsed. Su unico consumidor
            ' es el [RENDER-MS] del final de este bloque. El flag se toma UNA vez por frame para que el gate no
            ' cambie a mitad.
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
            ' ⛔ If MULTILINEA a proposito en todos los laps: con `If _instr Then a : b` el `:` deja las dos
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
            ' ⛔⭐ CON SOMBRAS ENCENDIDAS LOS BOUNDS SE RECALCULAN TAMBIEN EN PLAY. Congelarlos durante la
            ' animacion es una optimizacion vieja cuyo unico consumidor era el frustum culling, donde el
            ' peor caso es que una malla popee. El shadow map los usa para OTRA cosa: ShadowMapMath.Fit
            ' encuadra el ortho sobre ese AABB, y la pasada 1 del skinning (matrices -> SSBO) SI corre en
            ' cada frame de play. O sea que el vertice se mueve y la caja no: un brazo que se levanta por
            ' encima de la cabeza sale del encuadre, su silueta NO se escribe en el mapa, y el receptor lee
            ' el borde blanco = "iluminado". La sombra del brazo desaparece a mitad de camino.
            ' El margen que habia era el de la esfera envolvente sobre el AABB: para un cuerpo de 60x40x180
            ' son ~7,5 u por encima de la cabeza, o sea que cualquier brazo levantado lo pasa.
            ' El costo es la pasada O(vertices) que Option B salteaba, y MEDIDO sobre las 11 mallas del
            ' arnes (37.321 vertices) son 1,5 ms por frame — no los 8-10 ms que costaba antes de que
            ' ComputeBounds dejara de materializar el cache de mundo entero. La diferencia son las normales
            ' de mundo, que un AABB no lee: ver RenderableMesh.ComputeBounds. Sin ese arreglo previo esta
            ' correccion no era viable y habia que elegir entre sombra correcta y animacion fluida.
            ' Con la feature apagada no cambia nada.
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
                Dim inst As SkeletonInstance = If(resolved, SkeletonInstance.Default)
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
                    ' ⛔ ACA NO VA `OrElse sombrasEncendidas`, aunque parezca que si. Lo tuvo un rato, con el
                    ' argumento de "computar el cache de mundo eager para que ComputeBounds sea un min/max
                    ' barato". Es falso: RecomputeGPUBoneMatrices invalida el cache SIEMPRE (SkinningHelper,
                    ' InvalidateWorldCache) y despues, con updateWorldCache=True, llama a ComputeWorldBounds,
                    ' que entra por GetWorldVertices y dispara ComputeWorldSpaceCache lo mismo. O sea que la
                    ' pasada cara corre en los dos casos; lo unico que agregaba el OrElse era un SEGUNDO
                    ' recorrido O(vertices) de min/max y una segunda escritura de Minv/Maxv pisando la
                    ' primera. Lo que si cierra el defecto de la sombra es `computeBoundsThisFrame` mas
                    ' arriba: GetSceneBounds lee Minv/Maxv, y quien los escribe es mesh.ComputeBounds.
                    Dim updateWorldCache As Boolean = (Not playingNow) OrElse keepBounds
                    Dim updatePerVertexSkin As Boolean = cpuSkinMode OrElse updateWorldCache
                    ' Pose is implicit in the SkeletonInstance: the caller applied it via ApplyPose.
                    SkinningHelper.RecomputeGPUBoneMatrices(
                        mesh.MeshData.Shape, mesh.MeshData.Meshgeometry,
                        Model.SingleBoneSkinning, meshSkel, updateWorldCache, updatePerVertexSkin, meshGlobalCache)

                    If cpuSkinMode Then
                        ' ⭐ ESTA ES LA LINEA CALIENTE del conjunto de sucios: corre por frame y por malla
                        ' mientras dura la animacion. Construir aca un `HashSet(Of Integer)` con
                        ' `Enumerable.Range(0, n)` costaba 1,16 ms/frame MEDIDOS sobre el Serena Battle
                        ' Suit (130.500 vertices en 26 mallas) —el 10 % del frame CPU— para armar un
                        ' conjunto cuyo contenido despues NO se lee: con todos sucios la subida es
                        ' completa y solo se mira `.Count`. Ver ConjuntoDeSucios.
                        mesh.MeshData.Meshgeometry.dirtyVertexIndices.MarcarTodos(
                            mesh.MeshData.Meshgeometry.Vertices.Length)
                        Array.Fill(mesh.MeshData.Meshgeometry.dirtyVertexFlags, True)
                    End If

                    If computeBoundsThisFrame Then mesh.ComputeBounds()
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
            ' ⛔ EL LOOP TENIA UN `Stopwatch.StartNew()` POR MALLA, sin gate, y `_skinFuncMs` no lo lee nadie
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
        ' Los dos programas de profundidad se compilan SIEMPRE, aunque las sombras esten apagadas: el
        ' costo es un link por juego al abrir el control, y tenerlos condicionados al setting significaria
        ' compilar GLSL en medio de un frame la primera vez que alguien prende la opcion.
        SharedShadowFO4Shader = New Shadow_Depth_Shader_Fo4
        SharedShadowSSEShader = New Shadow_Depth_Shader_SSE
        SharedGroundShadowShader = New Ground_Shadow_Shader_Class

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
        ' ⛔ Se CAPTURA lo que habia, no se asume. Restaurar el literal `Back` es correcto solo mientras el
        ' llamador entre con el framebuffer por defecto bindeado; con un FBO bindeado el valor previo es un
        ' COLOR_ATTACHMENTi y "restaurar Back" seria dejarlo peor que antes. Lo mismo con PackAlignment: se
        ' pisa a 4 dos lineas mas abajo y hay que devolver el que habia, no el default de GL.
        Dim prevReadBuffer As Integer = CInt(ReadBufferMode.Back)
        Dim prevPackAlignment As Integer = 4
        Try
            GL.GetInteger(GetPName.ReadBuffer, prevReadBuffer)
            GL.GetInteger(GetPName.PackAlignment, prevPackAlignment)
        Catch
        End Try
        Try
            GL.ReadBuffer(ReadBufferMode.Front)
            GL.PixelStore(PixelStoreParameter.PackAlignment, 4)
            GL.ReadPixels(0, 0, bmp.Width, bmp.Height, OpenTK.Graphics.OpenGL4.PixelFormat.Bgra, PixelType.UnsignedByte, data.Scan0)
        Finally
            ' ⛔ DEVOLVER el ReadBuffer. Quedaba en Front para siempre, asi que cualquier ReadPixels o
            ' CopyTexImage posterior contra el framebuffer por defecto leia el buffer equivocado. Es el bug
            ' que "solo se ve a veces": el sintoma depende de si el frame ya se presento.
            GL.ReadBuffer(CType(prevReadBuffer, ReadBufferMode))
            GL.PixelStore(PixelStoreParameter.PackAlignment, prevPackAlignment)
            Dim rb = prevReadBuffer, pa = prevPackAlignment
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
        ' ⛔⛔ PONER EL CAMPO EN 0 DESPUES DE BORRAR. Estas ocho lineas borraban y NO anulaban, a
        ' diferencia del resto de este metodo (que si hace `= Nothing`). Y `Clean` corre DOS veces en el
        ' cierre normal: `MainForm` llama `Clean()` y enseguida `Dispose()`, que vuelve a llamar `Clean()`.
        ' En la segunda pasada `_Model` ya es Nothing, asi que NADIE hace current el contexto —el unico
        ' `EnsureContextCurrent` del camino vive adentro de `Model.Clean`— y estos ocho `DeleteTexture` se
        ' disparan con ids viejos contra EL CONTEXTO QUE ESTE CURRENT EN ESE HILO, que puede ser el de otro
        ' PreviewControl vivo. Los nombres GL son por contexto: alla esos ids son texturas en uso.
        ' [AUDIT-CLEAN] valida el arreglo del doble-delete: en la SEGUNDA pasada de Clean() los ocho
        ' tienen que venir ya en 0. Si alguno viene distinto de 0 dos veces seguidas, el bug sigue.
        ' ⚠️ ANULAR NO ES GRATIS DEL TODO: despues del primer Clean(), un `BindTexture(..., defaultWhiteTex)`
        ' bindea 0, que en GL es NEGRO, no blanco. Hoy no se dispara porque `BeginTeardown` levanta
        ' `_isTearingDown` y `RenderScene`/`OnPaint` salen antes de dibujar — o sea que esto es una red que
        ' depende de OTRA red. Si alguna vez se dibuja despues de un Clean(), el sintoma va a ser un modelo
        ' negro, y el causante es esta linea, no el shader.
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
        If defaultFacegenSubsurfaceTex <> 0 Then GL.DeleteTexture(defaultFacegenSubsurfaceTex) : defaultFacegenSubsurfaceTex = 0
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
        Private _occlEvaluated As Boolean = False

        ''' <summary>
        ''' El set de triángulos que la oclusión por segmento/partición dejó FUERA del draw, indexado
        ''' por índice de triángulo del shape (mismo orden que <c>Meshgeometry.Indices</c>).
        ''' <c>Nothing</c> = no hay nada oculto por esta vía.
        ''' <para>Es el mismo array que <see cref="EnsureZapIndexBuffer"/> usó para filtrar el element
        ''' buffer del último frame, expuesto para que un consumidor (el export a NIF) LEA lo que se
        ''' dibujó en vez de recalcularlo. Recalcularlo es reproducir el criterio, y dos copias del
        ''' criterio se desincronizan; esto no puede.</para>
        ''' <para>⚠️ Sólo es significativo si <see cref="OcclusionEvaluated"/> es True: el cómputo vive
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
            ''' <para>â›” Resuelve la contaminacion entre NPCs: el fold instalaba su resultado bajo la clave del
            ''' COMPLEXION, que es COMPARTIDA entre shapes y entre NPCs de la misma raza, asi que dos cabezas con
            ''' el mismo complexion en un PreviewModel hacian que la segunda heredara el face-paint de la
            ''' primera. El facetint no tiene el problema porque su clave ya es per-NPC; esto le aplica la MISMA
            ''' ley al diffuse.</para>
            ''' <para>âš ï¸ ES UNA CLAVE (String), NO un Texture_ID, a proposito: guardar el id crudo lo dejaria
            ''' COLGADO si alguien limpia el diccionario sin reconstruir este MaterialData, y samplear una
            ''' textura ya borrada da basura. Por clave, un diccionario limpio devuelve 0 y se cae solo al
            ''' complexion real.</para>
            ''' <para>â›” Y NO se toca <c>MaterialBase.Diffuse_or_Base_Texture</c>: el material sigue apuntando al
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
            ''' <para>⛔ Existe como propiedad —y no inline en ApplyMaterial, que es de donde salio— porque
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
                    ' ⭐ SSE plegado: el diffuse de ESTE NPC vive bajo una clave PER-NPC. Ver SseFoldedDiffuseKey.
                    ' Si la clave está vacía (todo FO4, Wardrobe, y el SSE no plegado) o el diccionario ya no la
                    ' tiene (post-CleanTextures), se cae al complexion real: comportamiento idéntico al previo.
                    If Not String.IsNullOrEmpty(SseFoldedDiffuseKey) Then
                        Dim foldedId = GetTextureID(SseFoldedDiffuseKey)
                        If foldedId <> 0 Then Return foldedId
                    End If
                    Return GetTextureID(FO4UnifiedMaterial_Class.CorrectTexturePath(MaterialBase.Diffuse_or_Base_Texture))
                End Get
            End Property
            Public ReadOnly Property NormalTexture_ID As UInteger
                Get
                    ' ⭐ SSE plegado: el _msn de ESTE NPC (con los normales de overlay compuestos) vive bajo una
                    ' clave PER-NPC. Espejo EXACTO de DiffuseTexture_ID — ver SseFoldedNormalKey. Clave vacía
                    ' (todo FO4, Wardrobe, SSE sin overlay-normal) o diccionario ya limpiado ⇒ se cae al _msn real:
                    ' comportamiento idéntico al previo.
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
                    ' Guard de CUBEMAP, igual que EnvmapMaskTexture_ID: en SSE este slot alimenta texEnvMask
                    ' (sampler2D). Si el slot 5 apunta a un DDS cubemap, bindearlo como Texture2D da
                    ' GL_INVALID_OPERATION. Se devuelve 0 y el caller cae al default.
                    Dim key As String = FO4UnifiedMaterial_Class.CorrectTexturePath(MaterialBase.FlowTexture)
                    If key = "" Then Return 0
                    Dim tex As Texture_Loaded_Class = Nothing
                    If Not TryGetTexture(key, tex) Then Return 0
                    If tex.Cubemap = True Then Return 0
                    Return tex.Texture_ID
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

        ''' <summary>Sube al GL los buffers del shape ya skineados en CPU (lee <c>PerVertexSkinMatrix</c> y
        ''' transforma local a world antes del upload). Es el camino que corre con GPU-skinning APAGADO.
        ''' <para>â›” SYNC: CPU/GPU skinning - es el gemelo del bloque de skinning del vertex shader. Con el
        ''' toggle en GPU este codigo no corre, asi que una formula cambiada de un solo lado no falla: solo se ve
        ''' mal en el otro modo. Lista completa de sitios gemelos en <c>SkinningHelper.BlendBoneMatrices</c> y en
        ''' 00-reglas-ui-y-vb.</para></summary>
        ''' <param name="recomputeBounds">True (default) = recomputa bounds tras el upload completo (full-reload
        ''' y morph, que no tienen ComputeBounds aparte). El camino de pose pasa False porque sus bounds los
        ''' maneja la linea gateada del pass 1; incondicional aca bypasseaba ese gate (8,9 ms/frame medidos).
        ''' El nombre difiere de ComputeBounds a proposito: VB es case-insensitive y un parametro homonimo
        ''' sombrearia al metodo.</param>
        ''' <summary>
        ''' Resube el VBO de UV cuando un slider uv movio <c>Uvs_Weight</c>. El buffer se creo
        ''' <c>StaticDraw</c> porque hasta ahora las UVs no cambiaban nunca despues de cargar el NIF;
        ''' con sliders uv si cambian, y sin esto el viewport mostraba las UVs viejas mientras el .nif
        ''' construido salia con las nuevas. Se sube entero (los arrays de UV son chicos y esto solo
        ''' corre cuando el flag esta prendido, no por frame).
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
        ''' <para>⛔ Se dimensionan al ALTA y no se achican: una malla no cambia de cantidad de vertices sin
        ''' pasar por una re-extraccion, que reconstruye el RenderableMesh entero.</para></summary>
        Private _upPos() As Vector3, _upNrm() As Vector3, _upTan() As Vector3, _upBitan() As Vector3

        ''' <summary>⭐ Separa COMPUTO de SUBIDA dentro del camino de upload completo, para decidir donde
        ''' vale la pena optimizar: si el que domina es el driver, vectorizar la aritmetica no mueve nada.
        ''' <para>⛔ NO se usa `Logger.Enabled` para esto. Ese flag no gobierna solo la escritura del log: es
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
                    ' ⛔⭐ SCRATCH REUTILIZADO, NO CUATRO ARRAYS NUEVOS POR FRAME. Este bloque corre por
                    ' malla y por frame durante toda una animacion con skinning CPU (y en cada morph), y
                    ' alocaba `vertexCount * 12 bytes * 4` cada vez: con 130.500 vertices son 6,3 MB de Gen0
                    ' POR FRAME, ~375 MB/s a 60 fps. Es la misma politica que este archivo ya aplica a
                    ' _shadowCasters y a BlendScratch, aca sin aplicar.
                    ' No cambia un bit: mismos valores, mismo orden, mismos indices escritos. Lo unico que
                    ' desaparece es la alocacion.
                    EnsureUploadScratch(vertexCount)
                    Dim posF = _upPos, nrmF = _upNrm, tanF = _upTan, bitanF = _upBitan
                    Dim _swFase As System.Diagnostics.Stopwatch = If(MedirFasesDeUpload, System.Diagnostics.Stopwatch.StartNew(), Nothing)

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
                        ' ⭐⛔ EL BUCLE SE FUE A FastSkin. Era el que dominaba un frame de animacion con
                        ' skinning por CPU: una inversa 3x3 POR VERTICE, POR MALLA, POR FRAME, escalar y en
                        ' Double. Medido sobre 130.500 vertices, 9,3 ms de un frame de ~20 — contra 1,3 ms
                        ' de las cuatro subidas de VBO que vienen despues.
                        ' Alla la ley esta escrita UNA vez con dos implementaciones (escalar y vectorial)
                        ' que un gate compara BIT A BIT, y a los dos anchos de vector. Ver FastSkin.
                        SkinningHelper.FastSkinTransformar(mats, lv, ln, lt, lb, isMSN, vertexCount,
                                                           posF, nrmF, tanF, bitanF)
                    Else
                        ' GPU skinning: upload local-space as-is
                        Dim gv = MeshData.Meshgeometry.Vertices
                        Dim gn = MeshData.Meshgeometry.Normals
                        Dim gt = MeshData.Meshgeometry.Tangents
                        Dim gb = MeshData.Meshgeometry.Bitangents
                        ' ⭐ Partitioner + For interno, NO `Parallel.For(0, n, Sub(i))`. La forma
                        ' anterior invocaba UN DELEGATE POR VERTICE (22.700 por malla POR FRAME) para
                        ' un cuerpo que son 12 conversiones y 4 stores: el despacho costaba del orden
                        ' del trabajo. Es el mismo hallazgo que ya se aplico en el compositor de
                        ' facetint (ver 61-perf-plan-4-hotpaths §3), acá replicado en el upload.
                        ' ⛔ Esto NO cambia un bit: la aritmetica, el orden y el redondeo son los
                        ' mismos; lo unico que se va es el despacho.
                        ' ⚠️ Y NO se vectoriza con Vector.Narrow, aunque sea el caso ideal (un run
                        ' plano de 3N doubles a 3N floats): para eso habria que ver el Vector3d() como
                        ' Double(), y eso pide MemoryMarshal/Span, que VB.NET no admite en ninguna
                        ' posicion. Copiar a un staging plano primero cuesta mas trafico de memoria
                        ' del que ahorra el narrow, asi que el camino escalar es el correcto acá.
                        Dim convertRange As Action(Of Tuple(Of Integer, Integer)) =
                            Sub(rango As Tuple(Of Integer, Integer))
                                For i = rango.Item1 To rango.Item2 - 1
                                    Dim vv = gv(i) : posF(i) = New Vector3(CSng(vv.X), CSng(vv.Y), CSng(vv.Z))
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
                    ' ⛔ `Array.Clear` Y NO UN BUCLE POR INDICE. Para entrar a esta rama hacen falta MAS DEL
                    ' 60 % de los vertices sucios, o sea que la lista trae ~78.000 indices sobre el Serena
                    ' Battle Suit: recorrerla es 78.000 lecturas de la lista mas 78.000 escrituras DISPERSAS
                    ' al array de flags. `Array.Clear` es un memset contiguo sobre el array entero.
                    ' ⛔ Es equivalente y no "de mas": esta rama sube TODOS los vertices, asi que al salir
                    ' ninguno queda sucio — poner en False los que ya estaban en False no cambia nada.
                    If MeshData.Meshgeometry.dirtyVertexFlags IsNot Nothing Then
                        Array.Clear(MeshData.Meshgeometry.dirtyVertexFlags, 0, MeshData.Meshgeometry.dirtyVertexFlags.Length)
                    End If
                    MeshData.Meshgeometry.dirtyVertexIndices.Clear()
                    If _instr Then
                        ParentModel.ParentControl._skinDirtyMs += _swSkinPhase.Elapsed.TotalMilliseconds
                        _swSkinPhase.Restart()
                    End If

                    ' Also recompute bounds after full update — SALVO cuando el caller ya los maneja.
                    ' En el pose path los computa la línea gateada del pass 1 ('If computeBoundsThisFrame
                    ' Then mesh.ComputeBounds()'); incondicional acá bypasseaba ese gate Y Option B en CPU
                    ' (ComputeBounds = pasada per-vértice a mundo; medido 6,9-8,5 ms/frame sobre las 11
                    ' mallas del arnés, y ya SIN las normales de mundo, que salieron de ese camino).
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
                        ' ⛔⭐ LA MISMA LEY QUE EL CAMINO DENSO, no una copia. Este bucle escribe por
                        ' Marshal.Copy a un buffer mapeado, indice por indice, asi que no puede llamar a
                        ' FastSkin.TransformarDirecto — pero si a la ley de UN vertice. Tenerla duplicada
                        ' hacia que la misma malla saliera con una ley u otra segun cuantos vecinos se
                        ' hubieran ensuciado ese frame (el umbral del 60 % de mas arriba).
                        Dim pS As Vector3, nS As Vector3, tS As Vector3, bS As Vector3
                        SkinningHelper.FastSkinUnVertice(sparseMats(i), MeshData.Meshgeometry.Vertices(i),
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
            ' ⛔ EL BUFFER SE IZA FUERA DEL BUCLE. Adentro estaba `BitConverter.GetBytes(vertexMask(i))`,
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

        ' Zap limpio del lado CPU: con ApplyZaps prendido se excluye todo triangulo que tenga ALGUN vertice con
        ' VertexMask = -1 (la misma regla que usa el export a NIF). Reemplaza al discard por 'flat ZappedVert'
        ' del shader, que descartaba por vertice provocador y dejaba astillas en el borde.
        ' Los vertices NO se compactan (el VBO queda completo); solo se filtra el index buffer, asi que los
        ' zapeados dejan de estar referenciados. Se reconstruye solo cuando ApplyMorphPlan re-toco la mascara o
        ' cambio el toggle: ApplyMorphPlan es el unico escritor de VertexMask=-1, asi que el flag no puede
        ' quedar rancio.
        ' âš ï¸ SkinnedGeometry es Structure: 'geom' es una COPIA del campo, asi que el clear de ZapTopologyDirty
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
            ' ⭐ N/T/B ya ESTAN en Single (ver SkinnedGeometry.Normals): van derecho al VBO. Antes cada
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
        ''' (GPU skinning: Vertices are local-space, so we need world-space for correct bounds.)
        ''' <para>⭐ NO MATERIALIZA EL CACHE DE MUNDO. Antes llamaba a <c>GetWorldVertices</c>, que con el
        ''' cache invalidado —que es SIEMPRE en este punto: RecomputeGPUBoneMatrices lo invalida un par de
        ''' lineas antes— construia el cache ENTERO, o sea tambien las normales de mundo: una
        ''' <c>Create_Normal_Matrix</c> (inversa + transpuesta 3x3) por vertice, mas dos arrays
        ''' <c>Vector3d()</c> por malla por frame. Un AABB no lee ni una de esas normales. Medido sobre las
        ''' 11 mallas del arnes (37.321 vertices), invalidando tambien <c>PerVertexMatrixValid</c> —que es
        ''' como llega el frame de play— el cache completo cuesta 10,4-13,2 ms y esta variante 6,9-8,5 ms:
        ''' <b>~35 % menos</b>. NO 86 %: esa cifra salio de una medicion que dejaba
        ''' <c>PerVertexSkinMatrix</c> valida, con lo cual ninguna de las dos pagaba el blend de 4 huesos por
        ''' vertice, que es lo que domina. El ahorro real es solo la parte de las normales.</para>
        ''' <para>Quien necesite normales de mundo (picking, exportador, raytracer de oclusion) sigue
        ''' pidiendolas por <c>GetWorldVertices</c> y las computa en ese momento. Lo que no puede pasar es
        ''' dejar el cache MEDIO lleno, y por eso la variante sin normales no lo marca valido.</para>
        ''' <para>Efecto lateral deseable: <c>Minv/Maxv/Boundingcenter</c> quedan en Double exacto. Antes se
        ''' derivaban de <c>BoundsMin/Max</c>, que son Single, asi que la clave de orden del bucket BLENDED
        ''' pasaba por un redondeo que no hacia falta.</para></summary>
        Public Sub ComputeBounds()
            If MeshData.Meshgeometry.Vertices Is Nothing OrElse MeshData.Meshgeometry.Vertices.Length = 0 Then
                BoundsMin = New Vector3(Single.MaxValue)
                BoundsMax = New Vector3(Single.MinValue)
                Exit Sub
            End If
            SkinningHelper.ComputeWorldBoundsSinNormales(MeshData.Meshgeometry)
            Dim mn = MeshData.Meshgeometry.Minv
            Dim mx = MeshData.Meshgeometry.Maxv
            ' BoundsMin/Max son Single (los consume el culling de frustum). Se REDONDEAN HACIA AFUERA: hacia
            ' adentro, un AABB que ya toca el borde podria descartar una malla visible por un ulp.
            BoundsMin = New Vector3(MathF.BitDecrement(CSng(mn.X)), MathF.BitDecrement(CSng(mn.Y)), MathF.BitDecrement(CSng(mn.Z)))
            BoundsMax = New Vector3(MathF.BitIncrement(CSng(mx.X)), MathF.BitIncrement(CSng(mx.Y)), MathF.BitIncrement(CSng(mx.Z)))
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

        ''' <summary>⛔ NO USAR EN EL CAMINO DE DIBUJO: aloca los 6 planos en cada llamada. Queda como
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

        ' Centralized tuning points for decal/depth-bias raster offset.
        Private Const DecalPolygonOffsetFactor As Single = 0.0F
        Private Const DecalPolygonOffsetUnits As Single = 0.0F
        Private Const DecalDepthBiasPolygonOffsetFactor As Single = 0.0F
        Private Const DecalDepthBiasPolygonOffsetUnits As Single = 0.0F

        ' T11: depth-bias de decals del motor. Fallout4.exe elige el preset 1 de rasterizer para el flag Decal
        ' (SF1 bit 26) via el global ToggleDepthBias, NO por el campo DepthBias del material (que es N/A en FO4
        ' v2). El preset 1 es D3D11 DepthBias=-3 y SlopeScaledDepthBias=-0.4 bajo reversed-Z.
        ' SIGNO: sale de la convencion de ESTA app (standard-Z, DepthFunc Lequal, near=0, decals dibujados
        '   despues de la base): acercar el decal al ojo para ganar el Lequal = offset GL NEGATIVO.
        ' factor <- SlopeScaledDepthBias -0.4: mapeo 1:1 real (los dos escalan la pendiente maxima).
        ' units  <- DepthBias -3: NO es una traduccion fiel entre APIs. El motor lo midio sobre un buffer
        '   reversed-Z D32_FLOAT y esta app usa standard-Z D24_UNORM; -3.0 queda solo como valor GL razonable
        '   (los decals GL tipicos van -1..-8). AJUSTAR si aparece z-fighting o peter-panning.
        ' DepthBiasClamp no tiene equivalente en GL 4.3 core, asi que se descarta.
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

        ''' <summary>Dibuja esta malla en el shadow map. Espejo REDUCIDO de <see cref="Render"/>: el MISMO
        ''' VAO, el MISMO SSBO de huesos y el MISMO vertex shader — lo unico que cambia son las matrices
        ''' (las de la luz en vez de las de la camara) y el fragment, que solo hace el alpha-test.
        '''
        ''' <para>⛔ QUE UNIFORMS HAY QUE SUBIR Y POR QUE ESOS. El unico dato del vertex shader que el
        ''' fragment de profundidad consume es <c>vColor.a</c>, y en el VS ese canal depende de UNA sola
        ''' cosa: <c>if (bShowVertexAlpha) vColor.a = vertexAlpha;</c>. Ni <c>color</c> ni <c>subColor</c>
        ''' lo tocan (el primero multiplica por un vec4 con w=1, el segundo es <c>.rgb</c>), asi que no
        ''' hace falta subirlos. Lo demas que se sube es lo que decide la POSICION (matrices + skinning) y
        ''' el recorte (zap + alpha-test).</para></summary>
        Friend Sub RenderDepthOnly(shadowShader As Shader_Base_Class, lightView As Matrix4)
            If IsNothing(MeshData.Shape) OrElse MeshData.Shape.RenderHide Then Exit Sub
            If IsNothing(Me.MeshData.Shape.NifShape) Then Exit Sub

            Dim model As Matrix4 = MeshData.Transform
            ' MISMO orden que el pase iluminado (`view * model`). Cambiarlo aca y no alla dejaria la
            ' sombra proyectada desde otro lado que la luz que la ilumina.
            Dim modelView As Matrix4 = lightView * model

            ' ⛔ matProjection / matView los sube el CALLER una sola vez por pase (son de la luz, no de la
            ' malla). Aca solo va lo que cambia POR MALLA.
            ' ⛔⛔ `mv_normalMatrix` SE SUBE, Y SOLO PARA EL EFFECT SHADER. Este comentario decia "NO SE
            ' SUBE, a proposito: solo alimenta varyings que este fragment ni declara (mv_tbn,
            ' v_msnMatrix)", y dejo de ser cierto cuando el fragment paso a declararlos para calcular el
            ' FALLOFF del .bgem. Sin subirlo, esos dos varyings valen la mat3 CERO (el valor inicial por
            ' spec de GL): `mv_tbn * vec3(0,0,0.5)` da el vector nulo, `normalize` de eso es NaN, y el
            ' falloff sale indefinido — con StartOpacity 0 el material entero se descarta y no castea nada.
            ' ⭐ Se calcula SOLO si hace falta: es un Invert + Transpose por malla y por frame, y la unica
            ' rama que lo lee es la del effect shader. Para un .bgsm no se toca.
            shadowShader.SetMatrix4("matModel", model)
            shadowShader.SetMatrix4("matModelView", modelView)

            Dim materialBase = MeshData.Material.MaterialBase
            shadowShader.SetBool("bModelSpace", materialBase IsNot Nothing AndAlso materialBase.ModelSpaceNormals)

            Dim shape = MeshData.Shape
            shadowShader.SetBool("bApplyZap", shape.ApplyZaps)
            ' bShowMask / bShowWeight / bWireframe / bShowVertexColor NO se suben: no afectan ni
            ' gl_Position ni vColor.a, que es todo lo que este pase mira.

            ' ⛔⭐ DOS CAMINOS DE RECORTE, Y EL SEGUNDO NO EXISTIA.
            '  · CUTOUT (`AlphaTest`): umbral duro con el `AlphaTestRef` del material. Es lo que ya habia.
            '  · TRANSLUCIDO (`AlphaBlend` sin test): antes NO SE RECORTABA NADA — el gate era `HasAlphaTest`
            '    a secas, asi que un material alpha-blend entraba al pase sin un solo `discard` y proyectaba
            '    la CARD ENTERA. Sintoma concreto y reportado: pelo fino que tira sombra de placa negra.
            '    Ahora va por el camino estocastico del fragment (dither ordenado 4x4 contra la opacidad
            '    real), que necesita ADEMAS el escalar Alpha del material — el mismo que el pase iluminado
            '    multiplica despues del test para el blend.
            ' ⚠️ En vanilla esto casi no se veia porque los alpha-blend de actor (pelo *_8bit, pestanas,
            ' eyewet, synthtattoo) traen CastShadows=False y ni entran al pase. Los mods de pelo suelen
            ' dejarlo en True, y ahi salia la placa.
            ' ⛔⛔ LOS DOS FLAGS SON INDEPENDIENTES, COMO EN EL PASE ILUMINADO. Antes el blend estaba
            ' gateado por `Not doAlphaTest`, con el argumento de que "gana el test, igual que en el pase
            ' iluminado (ahi se descarta por el umbral y despues se blendea lo que sobrevivio)". La primera
            ' mitad de esa frase es cierta y la segunda tambien — pero de ESTE pase no se cumplia ninguna
            ' de las dos: al ganar el cutout, `bAlphaBlend` quedaba en False y el superviviente entraba al
            ' mapa OPACO, sin pasar nunca por el dither. Y no es un caso raro: `HasAlphaBlend` es
            ' `AlphaBlendEnabled OrElse Alpha < 1`, o sea que TODO cutout con Alpha < 1 caia ahi. En
            ' pantalla se dibuja al 30 % de opacidad y su sombra salia tan negra como la de una malla
            ' solida. Ahora el fragment hace lo mismo que el iluminado: descarta por el umbral Y DESPUES
            ' aplica el dither sobre lo que sobrevivio.
            Dim doAlphaTest As Boolean = MeshData.Material.HasAlphaTest
            Dim doAlphaBlend As Boolean = MeshData.Material.HasAlphaBlend
            Dim usaTextura As Boolean = doAlphaTest OrElse doAlphaBlend
            shadowShader.SetBool("bAlphaTest", doAlphaTest)
            shadowShader.SetBool("bAlphaBlend", doAlphaBlend)
            ' ⛔⛔ `bShowTexture` ES EL DEL SHAPE, EL MISMO QUE MANDA EL PASE ILUMINADO (`Render.vb`,
            ' `shader.SetBool("bShowTexture", shape.ShowTexture)`). Antes se mandaba `usaTextura`, o sea
            ' que el uniform tenia DOS significados distintos en dos programas, y ademas `doAlphaTest` y
            ' `doAlphaBlend` llevaban `AndAlso shape.ShowTexture` — con lo cual, con la textura apagada,
            ' este pase no hacia NINGUN descarte mientras el iluminado SI seguia descartando (su
            ' `bAlphaTest` nunca estuvo gateado por la textura). Concretamente:
            '  · .bgem de los dos juegos: el bloque BGEM del fragment iluminado es HERMANO del
            '    `if (bShowTexture)`, no hijo, asi que `baseMap` se queda en su init `vec4(0.0)` y
            '    `effAlpha` da 0 ⇒ la shape desaparece de pantalla... y proyectaba la card entera.
            '  · .bgsm de SSE con Alpha = 0,3 y ref = 128: el escalar entra al test (0,3 < 0,502) ⇒ la
            '    shape entera se descarta en pantalla... y proyectaba la card entera.
            ' Es el mismo sintoma de placa negra flotando sin duenio que el dither vino a matar, entrando
            ' por la puerta de un toggle de la UI.
            shadowShader.SetBool("bShowTexture", shape.ShowTexture)
            Dim esEffectShader As Boolean = materialBase IsNot Nothing AndAlso materialBase.IsBGEM
            shadowShader.SetBool("bIsEffectShader", esEffectShader)
            ' ⛔ El fragment de profundidad es UNO para los dos juegos y la ley del alpha-test difiere en
            ' LAS DOS ramas: para el .bgem, FO4 aplica gamma 2.2 al alpha de vertice y SSE lo usa lineal;
            ' para el .bgsm, SSE mete el escalar Alpha DENTRO del test y FO4 no. El juego lo decide ACA,
            ' que es el unico lugar que lo sabe.
            shadowShader.SetBool("bLeySse", Config_App.Current.Game = Config_App.Game_Enum.Skyrim)
            ' ⛔ EL GATE DEL ALPHA DE VERTICE ES `UseVertexAlpha` PARA LAS DOS FAMILIAS Y LOS DOS JUEGOS —
            ' el MISMO que manda el pase iluminado a su vertex shader. Antes, para un .bgem, se mandaba
            ' `UseVertexColor`, "porque el fragment de FO4 gatea el alpha del effect shader con
            ' `bShowVertexColor`". Eso plegaba mal una ley de DOS pisos:
            '   · el VS del pase iluminado gatea SIEMPRE con UseVertexAlpha (es el unico que escribe
            '     vColor.a), y encima de eso
            '   · el fragment de FO4 —solo el de FO4— vuelve a gatear con UseVertexColor.
            ' O sea que el predicado neto de FO4 es `UseVertexAlpha AND UseVertexColor`, y como
            ' `UseVertexAlpha` YA IMPLICA `UseVertexColor` (ver la propiedad), se reduce a UseVertexAlpha.
            ' SSE no tiene el segundo piso: usa `vColor.a` crudo, gateado solo por el VS.
            ' Mandando UseVertexColor, el caso Tree/TreeAnim —UseVertexColor True con UseVertexAlpha
            ' False— divergia en FO4: el iluminado dejaba vColor.a en 1.0 y la sombra usaba el alpha real
            ' del vertice. Era transplantar la nota de una ley a la otra, que es el error que este archivo
            ' ya se comio dos veces con el falloff.
            shadowShader.SetBool("bShowVertexAlpha", usaTextura AndAlso MeshData.Material.UseVertexAlpha)
            If usaTextura Then
                shadowShader.SetFloat("alphaThreshold", If(materialBase Is Nothing, 0.5F, materialBase.AlphaTestRef / 255.0F))
                ' El escalar Alpha del material. ⛔ LO MIRAN LAS DOS RAMAS, no solo el dither: el cutout
                ' lo usa para todo .bgem (los dos juegos) y para el .bgsm de SSE, que mete el escalar
                ' DENTRO del test. Este comentario decia que el cutout ni lo lee — falso desde que el
                ' fragment carga las dos leyes, y era la clase de afirmacion que autoriza a mover esta
                ' subida adentro del If del dither y dejar el cutout de SSE leyendo basura.
                shadowShader.SetFloat("uMaterialAlpha", If(materialBase Is Nothing, 1.0F, materialBase.Alpha))
                If materialBase IsNot Nothing Then
                    shadowShader.SetVector2("uvOffset", New Vector2(materialBase.UOffset, materialBase.VOffset))
                    shadowShader.SetVector2("uvScale", New Vector2(materialBase.UScale, materialBase.VScale))
                Else
                    shadowShader.SetVector2("uvOffset", Vector2.Zero)
                    shadowShader.SetVector2("uvScale", Vector2.One)
                End If
                ' - EL MISMO FALLBACK QUE `texGreyscale` SEIS LINEAS ABAJO, y que el pase iluminado ya
                ' tiene: sin diffuse va la BLANCA, no el ID 0. Hoy coinciden por casualidad (un sampler2D
                ' sobre la textura 0, que esta incompleta, devuelve (0,0,0,1), o sea .a = 1 igual que la
                ' blanca), pero eso es un default del driver, no una ley — y el pase iluminado no se apoya
                ' en el. Dos pases que llegan al mismo alpha por mecanismos distintos es justo lo que el
                ' contrato de sincronia prohibe.
                Dim idDifuso = MeshData.Material.DiffuseTexture_ID
                If idDifuso = 0 Then idDifuso = Me.ParentModel.ParentControl.defaultWhiteTex
                shadowShader.BindTexture("texDiffuse", idDifuso, TextureUnit.Texture0)

                ' ⭐ LOS DOS MODIFICADORES DEL ALPHA DEL EFFECT SHADER. Sin ellos, el pase de profundidad
                ' testeaba una cantidad que el pase iluminado ni siquiera usa:
                '  · GreyscaleToPaletteAlpha REEMPLAZA el alpha por una fila de la paleta; con un diffuse
                '    de alpha 0,2 y una fila que devuelve 0,9 el objeto se dibuja opaco y la sombra se
                '    disolvia en el dither.
                '  · Falloff lo MULTIPLICA por un factor angular; sin el, un .bgem con falloff proyectaba
                '    la card ENTERA a opacidad plena mientras su borde se desvanecia en pantalla.
                ' El falloff del pase de sombra se evalua contra la LUZ (que es la camara de ese pase), que
                ' es lo correcto: lo que decide cuanta luz BLOQUEA el material es el angulo con el que la
                ' luz lo atraviesa.
                If esEffectShader Then
                    ' ⛔⛔ HAY QUE SUBIR `mv_normalMatrix`. El fragment de profundidad ahora LEE `mv_tbn` y
                    ' `v_msnMatrix` (para el falloff), y los dos salen de ese uniform en el vertex shader.
                    ' Sin subirlo vale la mat3 CERO por spec de GL: `mv_tbn * vec3(0,0,0.5)` da el vector
                    ' nulo, `normalize` de eso es NaN, y el falloff sale indefinido — con StartOpacity 0
                    ' el material entero se descarta y NO castea sombra ninguna.
                    ' El comentario de mas abajo decia "NO SE SUBE, a proposito: solo alimenta varyings que
                    ' este fragment ni declara". Dejo de ser cierto al declararlos.
                    ' La MISMA construccion que el pase iluminado (Invert + Transpose de la 3x3 del
                    ' model-view), pero con el model-view DE LA LUZ, que es el de este pase.
                    Dim nm3 As New Matrix3(modelView.M11, modelView.M12, modelView.M13,
                                           modelView.M21, modelView.M22, modelView.M23,
                                           modelView.M31, modelView.M32, modelView.M33)
                    nm3.Invert()
                    nm3.Transpose()
                    shadowShader.SetMatrix3("mv_normalMatrix", nm3)
                    shadowShader.SetBool("bModelSpace", materialBase.ModelSpaceNormals)
                    shadowShader.SetBool("bShowVertexColor", MeshData.Material.UseVertexColor)
                    shadowShader.SetBool("bEffectGreyscaleAlpha", materialBase.GrayscaleToPaletteAlpha)
                    shadowShader.SetBool("bEffectFalloff", materialBase.FalloffEnabled)
                    shadowShader.SetBool("bEffectFalloffColor", materialBase.FalloffColorEnabled)
                    shadowShader.SetVector4("effectFalloffParams",
                        New OpenTK.Mathematics.Vector4(materialBase.FalloffStartAngle, materialBase.FalloffStopAngle,
                                                       materialBase.FalloffStartOpacity, materialBase.FalloffStopOpacity))
                    ' ⛔ EL MISMO FALLBACK QUE EL PASE ILUMINADO: sin textura de paleta va la BLANCA, no el
                    ' ID 0. `bEffectGreyscaleAlpha` se sube sin el guard `<> 0` en los dos pases, asi que un
                    ' .bgem con el flag y sin paleta llega al lookup en los dos. Hoy coincidian por
                    ' casualidad —un sampler2D sobre la textura 0, que esta incompleta, devuelve (0,0,0,1),
                    ' o sea .a = 1 igual que la blanca— pero es un default del driver, no una ley, y el
                    ' canal RGB (`bGreyscaleColor`) si lleva el guard: la asimetria no era deliberada.
                    Dim idPaleta = MeshData.Material.GreyscaleTexture_ID
                    If idPaleta = 0 Then idPaleta = Me.ParentModel.ParentControl.defaultWhiteTex
                    shadowShader.BindTexture("texGreyscale", idPaleta, TextureUnit.Texture1)
                Else
                    shadowShader.SetBool("bEffectGreyscaleAlpha", False)
                    shadowShader.SetBool("bEffectFalloff", False)
                    shadowShader.SetBool("bEffectFalloffColor", False)
                End If
            End If

            shadowShader.SetBool("bGPUSkinning", ssbo_BoneMatrices > 0 AndAlso Config_App.Current.Setting_GPUSkinning)
            Dim boneCount As Integer = If(MeshData.Meshgeometry.GPUBoneMatrices IsNot Nothing, MeshData.Meshgeometry.GPUBoneMatrices.Length, 0)
            shadowShader.SetInt("uBoneCount", boneCount)
            If ssbo_BoneMatrices > 0 Then
                GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 0, ssbo_BoneMatrices)
            End If

            GL.BindVertexArray(vao)
            ' Mismo filtro de indices que el pase iluminado: un triangulo zapeado no puede castear.
            EnsureZapIndexBuffer()
            GL.DrawElements(PrimitiveType.Triangles, indexCount, DrawElementsType.UnsignedInt, 0)

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



        ''' <summary>⚠️ DIAGNOSTICO (sólo bajo <c>Logger.Enabled</c>): un parámetro de sampleo de una textura
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

            ' ⭐ SSE: la máscara de environment es el slot 5 del texture-set, y en SSE ese slot se guarda en
            ' FlowTexture, NO en EnvmapMaskTexture (ese campo sólo se puebla en la rama FO4) ⇒ sin esto la
            ' reflexión salía SIN enmascarar y el metal quedaba sobre-reflectivo.
            ' ⚠️ El mismo offset del material significa COSAS DISTINTAS según la subclase: en las reflectivas
            ' (Envmap/Eye/MultiLayerParallax) es el slot 5 = máscara de env; en Facegen es el slot 3 = detail.
            ' No confundirlos: cada subclase tiene su propio OnLoadTextureSet.
            ' ⛔ El gate es POR VALOR y no por tipo, y tiene que seguir siéndolo: hay dos caminos que SÍ dejan
            ' un EnvmapMaskTexture válido (un BGEM, que tiene el campo nativo, y un BGSM leído de disco con su
            ' sidecar JSON) y un override incondicional los borraba. La regla queda: si el slot 5 trae
            ' máscara, se usa; si no, no se toca nada.
            If isSSE Then
                Dim sseEnvMaskId = material.FlowTexture_ID
                If sseEnvMaskId <> 0 Then envmapMaskTextureId = sseEnvMaskId
            End If

            Dim hasBacklightTexture As Boolean = materialBase.BackLighting

            ' FO4: heuristica de OJOS, CONSERVADA. Si EyeEnvironmentMapping y no hay envmask, usa el `_s` como
            ' mascara de reflexion y anula el specular. ⚠️ ESTABA SIN GATE DE JUEGO y la borre por error: como
            ' `EyeEnvironmentMapping` sale de `BGSM.EnvironmentMappingEye` sin gate (FO4UnifiedMaterial:1214) y
            ' se puebla desde `shad.HasEyeEnvironmentMapping` para AMBOS juegos (:3295), borrarla le agregaba a
            ' los ojos de FO4 un highlight especular que antes no tenian. Queda SOLO para FO4; en SSE la
            ' envmask real llega por el slot 5 (arriba) y robar el slot 7 romperia specular/backlight.
            If Not isSSE AndAlso materialBase.EyeEnvironmentMapping AndAlso smoothSpecTextureId <> 0 AndAlso envmapMaskTextureId = 0 Then
                envmapMaskTextureId = smoothSpecTextureId
                smoothSpecTextureId = 0
            End If

            ' â›” ELIMINADAS DOS HEURISTICAS que existian SOLO para tapar la mascara de environment faltante en
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
            ' slot 7 (MSN), y en ambos casos rellena un default si la textura falta, asi que nunca se queda sin
            ' ninguna. Antes esto era `hasSpecMap OrElse normalTextureId <> 0`; al pasar hasSpecMap a depender de
            ' MSN, una malla SSE no-MSN con slot 7 pero SIN normal texture perdia bSpecular por completo
            ' (regresion: el motor si tendria specular, contra su normal por defecto). FO4 conserva su regla.
            Dim hasSpecularSource As Boolean = If(isSSE, True, hasSpecMap)

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
            ' ⛔ Los dos predicados viven en MaterialData (UseVertexColor / UseVertexAlpha) porque el pase de
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
                    ' ⭐ CANAL ALPHA. Faltaba, y es el que importa para una shape alpha-blend: el shader arranca
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
            ' ?? ILUMINACIÓN PRINCIPAL
            '===============================
            ' ?? ILUMINACIÓN PRINCIPAL

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

            ' ⛔ LOS UNIFORMS DE SOMBRA NO SE SUBEN ACA. Son constantes de FRAME (matriz de la luz, bias,
            ' radio del PCF, la textura), no de malla: los sube PreviewModel.UploadShadowUniforms una sola
            ' vez, justo despues del pase de profundidad. Subirlos por malla costaba ocho uniforms + un
            ' bind de textura por draw, y ademas copiaba una LightFit (tres Matrix4) en cada acceso a la
            ' propiedad. Es el mismo criterio que ya tiene el rig de luces con _frameLights.

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

            ' texSpecular = TXST slot 7. El motor lo lee DOS veces desde material+0x68: t2 (mask especular, bajo
            ' MODELSPACENORMALS) y t9 (backlight, bajo BACK_LIGHTING). Si el slot esta VACIO no lo saltea: el
            ' default-fill del material lo rellena y el ORDEN de sus ramas manda - primero el default GENERICO
            ' (normal map plano) si hay backLighting, con UNA sola condicion, y solo si esa no corrio, el
            ' default de height map (NEGRO) si (skinned && MSN).
            ' La semantica de los 5 booleanos esta MEDIDA, no supuesta: la primera rama del mismo default-fill
            ' llena el slot 2 con (a3||a4), y los dos unicos consumidores del slot 2 en SetupMaterial son
            ' SOFT_LIGHTING y RIM_LIGHTING, o sea {a3,a4} = {rim, soft}. Eso fija las posiciones 3-4, que es
            ' donde ReceiveValuesFromRootMaterial(skinned, rim, soft, backLighting, MSN) las pone.
            If smoothSpecTextureId <> 0 Then
                shader.BindTexture("texSpecular", smoothSpecTextureId, TextureUnit.Texture4)
            ElseIf isSSE AndAlso materialBase.BackLighting Then
                ' Rama 1: backlight con slot 7 vacio -> default GENERICO. Antes caia en blanco (1,1,1), que
                ' sumaba translucidez blanca a full por cada luz.
                shader.BindTexture("texSpecular", Me.ParentModel.ParentControl.defaultSseEngineGenericTex, TextureUnit.Texture4)
            ElseIf isSSE AndAlso hasSpecMap AndAlso materialBase.SpecularEnabled Then
                ' Rama 2: SPECULAR && MSN sin `_s` -> NEGRO => specular 0. El motor NUNCA cae al alpha del
                ' normal en MSN (medido 0/6096 sobre la poblacion completa sin terreno/LOD).
                ' ⛔ La condicion es SPECULAR, NO `skinned`: el call-site del default-fill (0x14AD4DF, vfunc
                ' slot 10) toma sus 5 booleanos de los flags de la shader property en [rsi+0x38], y el 1o es
                ' `bit0 || bit41` = el predicado del define SPECULAR (mapeo flag->define decodificado del
                ' constructor de descriptores 0x14ADFB0). El flag SKINNED (bit 1) NI SE LEE ahi.
                ' ⛔ NO dejar caer al `Else`: ese bindea defaultWhiteTex = 1.0 = specular MAXIMO, lo contrario.
                shader.BindTexture("texSpecular", Me.ParentModel.ParentControl.defaultSseMsnSpecTex, TextureUnit.Texture4)
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
                ElseIf materialBase.SubsurfaceLighting OrElse materialBase.RimLighting Then
                    ' NO-facegen con SOFT_LIGHTING o RIM_LIGHTING y slot 2 VACIO. El motor samplea t12 SIEMPRE
                    ' (el sample lo agrega el propio define, medido en el diff base-vs-SOFT_LIGHTING), y el
                    ' default-fill del material base rellena +0x60 con el GENERICO `BSShader_DefNormalMap`
                    ' — fill 0xffff8080 = RGBA (128,128,255,255) — bajo la condicion (rimLighting||softLighting).
                    ' O sea el mask vale (0.502, 0.502, 1.0), NO blanco y NO el albedo.
                    shader.BindTexture("texLightmask", Me.ParentModel.ParentControl.defaultSseEngineGenericTex, TextureUnit.Texture7)
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
            ' Subsurface (soft lighting) is a per-material property: bind it from the material's own
            ' Soft_Lighting flag, both engines. (The previous SSE-only `OrElse (isSSE AndAlso Facegen)`
            ' FORCE was removed 2026-07-23: RE of the SSE lighting-technique composition showed facegen
            ' subsurface is selected by the material flag via the descriptor SOFT_LIGHTING bit — it is not
            ' unconditionally forced — and vanilla/mod facegen heads ship the flag OFF, so forcing it added
            ' subsurface the engine does not apply. Face/body parity is handled by MatchBodySkinSubsurfaceToFace.)
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
            ' ⛔ GATE SÓLO EN SKYRIM. En FO4 el bool de backlight NO gatea nada: en la transferencia
            ' BGSM → material hay exactamente DOS compuertas booleanas (el rolloff del subsurface y el de
            ' wetness/SSR), y el power de backlight se copia con un `mov` pelado. Verificado en el binario del
            ' juego y en el del CK, mismo layout. Sobre el corpus vanilla hay 171 materiales con el flag en
            ' False y power > 0, y el motor les aplica la transmisión igual.
            ' ⛔ SYNC: RENDER == BAKE — la ruta de ESCRITURA lleva el MISMO gate por juego
            ' (FO4UnifiedMaterial_Class). En Skyrim el gate se conserva porque allá HasBacklight es un flag
            ' REAL del NIF; en FO4 se sintetiza con `power > 0` al leer.
            shader.SetFloat("backlightPower",
                            If(isSSE AndAlso Not materialBase.BackLighting, 0.0F, materialBase.BackLightPower))
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
                    tintVec = Shader_Base_Class.Vector_to_Linear(ScaledTintSrgb(tint, materialBase.TintColorScale))
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
            ' ⛔ CORRIGE la nota previa "facetint(t4) * softlight(diffuse, detail(t3))": tint y detail estaban
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
                    ' ⛔ Acá había una rama que, cuando el diffuse venía plegado, bindeaba un neutro (63,64,63) para
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

            ' ⚠️ DIAGNOSTICO (Logger.Enabled): el estado REAL con el que sale cada shape a dibujarse. Existe
            ' para DIFFEAR fold vs unfold: el fold de SSE no toca el material de las shapes que no son
            ' FaceTint, asi que si una de ellas (los bigotes, '_Beard') sale distinta, la diferencia tiene
            ' que estar ACA — en el bucket/orden, en el depth, en el blend o en que textura se bindeo.
            If Logger.Enabled Then
                Dim shpD = MeshData.Shape?.ShapeName
                Dim blendPair = If(hasAlphaBlend, material.Calculate_Blending(), New Integer() {0, 0})
                ' Los TRES factores del alpha del fragmento (`fragColor.a = vColor.a * texDiffuse.a * alpha`),
                ' cada uno con lo que realmente se envio, mas el estado REAL del sampler del diffuse leido del
                ' driver. Es lo unico que discrimina cual de los tres cambia entre plegado y no plegado.
                ' ⛔ El sampler se lee por DSA (GetTextureParameter con el NOMBRE de textura): bindear para
                ' consultarlo alteraria el estado del propio draw que se esta midiendo.
                Dim vcShow = shape.ShowVertexColor
                Dim vcData = hasVertexColorData
                Dim vcTree = isTreeAnim
                Dim dId = CInt(material.DiffuseTexture_ID)
                Dim dMin = TexParamOrMinus1(dId, GetTextureParameter.TextureMinFilter)
                Dim dMax = TexParamOrMinus1(dId, GetTextureParameter.TextureMaxLevel)
                Logger.LogLazy(Function() $"[DRAW-STATE] shape='{shpD}' idx={MeshData.Idx} blend={hasAlphaBlend}({blendPair(0)},{blendPair(1)}) test={hasAlphaTest} thr={materialBase.AlphaTestRef} matAlpha={materialBase.Alpha:F3} depthWrite={writeDepth} | tex D={dId} N={material.NormalTexture_ID} inner={material.InnerLayerTexture_ID} | vColor: show={vcShow} data={vcData} tree={vcTree} ⇒ bShowVertexColor={vcShow AndAlso vcData} bShowVertexAlpha={vcShow AndAlso vcData AndAlso Not vcTree} | sampler D: minFilter={dMin} maxLevel={dMax} | hair={materialBase.Hair} spec={materialBase.SpecularEnabled}x{materialBase.SpecularMult:F2} gloss={materialBase.NifGlossiness:F2} foldedKey='{material.SseFoldedDiffuseKey}'")
            End If
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

        ' ⛔ Sin consumidor fuera de este ensamblado (única referencia: su propia declaración).
        Friend Sub ExportMeshToOBJ(rutaArchivo As String)
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
        ' ⛔⛔ EL ORDEN DE `meshes` ES EL ORDEN DE `shapes`, Y NO ES COSMETICO. Aca habia un
        ' `ConcurrentBag(Of RenderableMesh)` con `Parallel.ForEach` + `AddRange`, y un ConcurrentBag NO
        ' TIENE ORDEN: su enumeracion depende de en que hilo termino cada shape. O sea que dos cargas de
        ' la MISMA escena producian dos ordenes de dibujo distintos.
        ' Eso importa porque EL ALPHA BLENDING NO ES CONMUTATIVO: dos shapes translucidas superpuestas
        ' —el pelo sobre la cabeza es el caso de todos los dias— dan un color distinto segun cual se
        ' dibuje primero.
        ' ⭐ MEDIDO, no razonado: el A/A de recarga de Tools/ShadowGate encontro que re-extraer la misma
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

    ''' <summary>Increment the per-path failure counter. Once it reaches
    ''' <see cref="MaxTextureUploadAttempts"/> the path is added to <see cref="Last_Loaded_Textures"/>
    ''' so <see cref="Process_Textures_GL"/> stops re-enqueuing it. Below the cap the path stays
    ''' eligible for retry on the next Process_Textures_GL pass.</summary>
    ''' <summary>Saca una textura del diccionario LIBERANDO su handle GL.
    ''' <para>⛔ Los dos caminos de fallo de subida hacian `Textures_Dictionary.Remove(path)` a secas. Si esa
    ''' clave ya tenia una textura VIVA —una instalada por el compositor, o una subida anterior— el handle
    ''' quedaba huerfano: fuera del diccionario, asi que `CleanTextures` no lo encuentra nunca mas. No es
    ''' memoria reciclable, es VRAM perdida hasta que muere el proceso.</para></summary>
    ''' <summary>Saca <paramref name="path"/> del diccionario tras una subida FALLIDA, liberando el handle
    ''' que hubiera quedado colgado. Sin esto la entrada se borraba y el nombre de GL quedaba huerfano.
    ''' <para>⛔⛔ NO TOCA una entrada que sea del COMPOSITOR. Es la misma ley que
    ''' <c>NpcFaceTintResolver.InstallTexture</c>: el gate es <see cref="PreviewModel.Texture_Loaded_Class.OwnedByComposer"/>
    ''' y no "id &lt;&gt; 0". Este helper lo llama el camino de fallo del LOADER, y el loader puede fallar sobre
    ''' una clave que el compositor ya repinto: el editor de cara encola el path de la cara, el usuario mueve
    ''' un slider, <c>NpcSkinLivePreview</c> instala la textura compuesta en esa misma clave, y recien ahi
    ''' vuelve el lote con error. Borrar ahi mata la textura VIVA del preview, y como
    ''' <c>DirectXDDSLoader</c> marca fallidas TODAS las entradas del lote cuando revienta una sola, un DDS
    ''' malo alcanzaba para llevarse varias. <c>GenTexture</c> recicla el nombre y otro sampler pasa a leer
    ''' pixeles ajenos, sin un solo error de GL.</para>
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
                Try : GL.DeleteTexture(previa.Texture_ID) : Catch : End Try
            End If
        End If
        Textures_Dictionary.Remove(path)
    End Sub

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
                            ' ⚠️ DIAGNOSTICO (Logger.Enabled): QUE ARCHIVO quedo detras de CADA nombre de textura GL.
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
        CutoutMeshes.Clear()
        DecalMeshes.Clear()
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
        Public Depth As Single
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

        ''' <summary>La direccion de la luz i en el ORDEN CANONICO de ShadowMapMath.LuzDelRig
        ''' (0 key, 1 fill izq, 2 fill der, 3 back). ⛔ Devuelve la direccion YA RESUELTA de este frame
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
    ''' consume ApplyMaterial. Antes esto se recalculaba POR MALLA — 18 Math.Pow + 4 Direction() idénticos, y
    ''' otra vuelta por cada overlay layer.
    ''' <para>Depende del rig activo y, SI <c>Setting_LightsFollowCamera</c> está prendido, también de la
    ''' cámara. ⚠️ Ese flag viene en <b>True</b> por default (decisión del usuario), así que orbitar SÍ mueve
    ''' las direcciones salvo que se apague. Con el flag apagado no cambia ni una.</para></summary>
    Private _frameLights As LightRigUniforms

    Friend ReadOnly Property FrameLights As LightRigUniforms
        Get
            Return _frameLights
        End Get
    End Property

    ''' <summary>Lleva una dirección del marco del RIG al marco del MUNDO usando la base de la cámara.
    ''' <para>⭐ NO HACE FALTA CONVERTIR LOS PRESETS, y esa es la razón por la que esto entra sin tocar nada
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

    Private Sub ResolveFrameLights(cam As OrbitCamera)
        ' El rig sale de ActiveLights() = el set del JUEGO activo (FO4/SSE tienen el suyo).
        Dim rig = Config_App.Current.ActiveLights()
        ' ⛔ LA RAMA APAGADA NO EJECUTA NADA NUEVO. No es `ADireccionDeCamara` con una base identidad: con la
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
        ' ⛔ EL NIVEL DE SUELO MULTIPLICA DESPUES DEL POW, y no es un detalle de orden: el tinte es un COLOR
        ' (se autora en perceptual y se decodea al subir) pero el nivel es un COCIENTE DE RADIANCIAS entre
        ' los dos hemisferios, y el mix() del shader que lo consume opera en lineal. Adentro del pow la
        ' perilla entregaba nivel^2.2 — el 0,45 del Studio valia 17,3 % del cielo, no 45 %.
        ' Ver PreviewLightRig.AmbientGroundLevel para la medicion que lo destapo.
        ' (El comentario va ACA y no en el inicializador: una linea de comentario entre dos miembros de un
        '  `With {}` corta la continuacion implicita y el parser tira BC30370.)
        _frameLights = New LightRigUniforms With {
            .AmbientSky = Shader_Base_Class.Vector_to_Linear(rig.AmbientSkyDiffuse()),
            .AmbientGround = Shader_Base_Class.Vector_to_Linear(rig.AmbientGroundDiffuse()) * rig.AmbientGroundLevel,
            .KeyDiffuse = Shader_Base_Class.Vector_to_Linear(rig.KeyLight.Diffuse()),
            .KeyDir = kd,
            .Fill0Diffuse = Shader_Base_Class.Vector_to_Linear(rig.FillLeft.Diffuse()),
            .Fill0Dir = f0,
            .Fill1Diffuse = Shader_Base_Class.Vector_to_Linear(rig.FillRight.Diffuse()),
            .Fill1Dir = f1,
            .BackDiffuse = Shader_Base_Class.Vector_to_Linear(rig.BackLight.Diffuse()),
            .BackDir = bd
        }
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
    ''' <para>⛔ Se limpia en <see cref="Clean"/>. RenderAll sale antes de RenderShadowPass cuando la escena
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
    ''' <para>⛔ NO ES UNA CONSTANTE ELEGIDA A OJO, y esa es la diferencia entre una sombra y una
    ''' calcomania. La primera version multiplicaba por <c>1 - a</c>, o sea que con Intensity = 1 el suelo
    ''' quedaba en NEGRO PURO — y ningun suelo en sombra es negro: le siguen llegando el ambiente y las
    ''' luces que no estan bloqueadas EN ESE PIXEL. Aca se evalua exactamente eso, con el MISMO rig que
    ''' esta iluminando al personaje.</para>
    '''
    ''' <para>⭐ Y AHORA ES POR LUZ, que es lo que lo vuelve correcto con N: el fragment resta el aporte
    ''' de cada capa ocluida y nada mas, asi que la sombra del suelo se COMPONE igual que la del cuerpo y
    ''' ademas se TINE — ocluir una key calida donde llega un fill frio deja el piso azulado. La version
    ''' de un solo tinte no podia expresar eso ni con N mapas.</para>
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

    ''' <summary>⭐ CRONOMETRO DE GPU DEL PASE DE PROFUNDIDAD. Apagado por default: cuando esta en False no
    ''' se crea ni una query y el camino de dibujo queda exactamente como antes.
    ''' <para>⛔ HACE FALTA UNA QUERY DE GL Y NO UN Stopwatch. El pase de profundidad es trabajo de GPU; un
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
    ''' <para>⛔ ESTADO GL: este pase cambia framebuffer, viewport, culling y depth. Los devuelve TODOS
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
        ' ⛔ SOLTAR LA VRAM EN *TODOS* LOS CAMINOS DE APAGADO, no solo en el de la opcion del suelo. El
        ' criterio "con la sombra apagada no se asigna un byte de GPU" tiene CINCO salidas —opcion apagada,
        ' sin shader, sin casters, encuadre invalido y fallo de Ensure— y liberar en una sola es no
        ' cumplirlo:
        ' prender sombras + suelo y despues DESTILDAR sombras dejaba los dos mapas colgados hasta el Clean
        ' ⚠️ Y LA CUENTA CRECIO: los dos arrays se reservan al MISMO lado y con las MISMAS capas, asi que a
        ' 2048 con una sola luz son 16 + 16 = 32 MB, con las cuatro 128 MB, y a 4096 con las cuatro 512 MB.
        ' (Este comentario decia "a 2048 + 1024 son ~16 MB", que era la aritmetica de cuando el mapa ancho se
        ' dimensionaba solo. Ver el cartel de VRAM del dialogo, que hace exactamente esta cuenta.)
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
        ' ⛔ NO HAY OVERRIDE DE ESTE FILTRO, y hubo uno por unas horas. Se agrego una opcion "ignorar el
        ' flag del material" para poder ver la sombra de mods que traen CastShadows=False — pero eso era
        ' tapar con una perilla un DEFECTO DE LECTURA: el bit no se estaba leyendo del NIF para materiales
        ' BGEM (ver FO4UnifiedMaterial_Class.CastShadows). Arreglada la lectura, la perilla sobra, y una
        ' perilla que existe para compensar un bug es justo lo que la regla "nunca un modo legacy" prohibe.
        Dim casters = _shadowCasters
        casters.Clear()
        For Each mesh In meshes
            If mesh Is Nothing OrElse mesh.MeshData Is Nothing OrElse mesh.MeshData.Shape Is Nothing Then Continue For
            If mesh.MeshData.Shape.RenderHide OrElse mesh.MeshData.Shape.Wireframe Then Continue For
            Dim mb = mesh.MeshData.Material?.MaterialBase
            If mb Is Nothing OrElse Not mb.CastShadows Then Continue For
            ' Los DECAL son overlays coplanares sobre otra superficie: en un mapa de profundidad no
            ' aportan silueta, solo z-fighting con la malla que ya esta abajo.
            If mb.Decal Then Continue For
            casters.Add(mesh)
        Next
        If casters.Count = 0 Then SoltarMapasDeSombra() : Exit Sub

        ' Encuadre sobre el AABB de TODO lo visible, no solo de los casters: asi cualquier receptor cae
        ' adentro del mapa. Lo que quede afuera lee el borde blanco de la textura = "iluminado".
        Dim bmin As Vector3, bmax As Vector3
        ParentControl.GetSceneBounds(bmin, bmax)
        ' ⛔⭐ EL PLANO DEL RECEPTOR ES EL PISO DECLARADO POR LA APP, no el punto mas bajo del AABB. Antes
        ' era `bmin.Z` y eso fallaba de tres formas distintas:
        '  1. La grilla se dibuja en `FloorOffset + 0.01` con DepthMask activo y DepthFunc Lequal. Con el
        '     receptor en z = 0 y la grilla en z = 0,01, la grilla GANA el test de profundidad en cada
        '     linea: ~43 rayas brillantes sin sombrear atravesando la sombra, a Size=400/Step=10.
        '  2. Wardrobe Manager pone `FloorOffset = -HighHeelHeight`: con un outfit de tacos el piso real y
        '     el receptor quedaban separados por la altura del taco y la sombra flotaba.
        '  3. GetSceneBounds saltea las shapes con RenderHide, asi que en un preview de UNA pieza (un
        '     guante a z = 100) `bmin.Z` era 100 y la sombra salia como una losa colgada en el aire.
        ' El +0,02 lo pone por ENCIMA del +0,01 de la grilla: el receptor gana el test contra la grilla, y
        ' el personaje lo sigue tapando porque esta mas arriba todavia.
        ' ⚠️ El desempate es de UN SOLO LADO: mirando desde ABAJO del plano (la camara llega casi a -90) la
        ' grilla queda mas cerca y le vuelve a ganar al receptor, con las mismas rayas brillantes al reves.
        ' Se deja asi a proposito: la alternativa —invertir el signo segun donde este la camara— mueve el
        ' plano de la sombra mientras se orbita, que es peor que un artefacto en una vista donde el receptor
        ' de suelo no significa nada.
        _groundZ = CSng(FloorOffset) + 0.02F

        ' ===================== MAPA 1: AJUSTADO AL PERSONAJE =====================
        ' ⭐ DOS MAPAS, NO UNO. Con un solo mapa compartido, meter el receptor de suelo obligaba a agrandar
        ' el encuadre hasta cubrir donde ATERRIZA la sombra —la cabeza esta a ~180 u y con la key a 26
        ' grados su sombra cae a 180/tan(26) = 369 u de los pies— y como el mapa tiene un tamano FIJO, cada
        ' texel pasaba a cubrir 2,4 veces mas mundo: la sombra sobre el PERSONAJE se volvia 2,4 veces mas
        ' gruesa (medido: texel 0,077 -> 0,181 u). Con un mapa propio para el suelo, el del personaje
        ' vuelve al encuadre ajustado y no se pierde nada de nitidez.
        ' El del suelo se DIBUJA a menos resolucion a proposito: es una mancha grande y difusa, no
        ' necesita filo, y cuanto menos lo decide GroundMapSize a partir de los dos radios.
        ' ⛔ PERO SE RESERVA AL MISMO TAMANO QUE EL DEL PERSONAJE, y este comentario decia lo contrario
        ' ("el extra de VRAM va de 1/16 del mapa del personaje a igualarlo"). Dejo de ser cierto al
        ' arreglar el churn: el tamano LOGICO sigue saliendo de GroundMapSize —y es el viewport con el que
        ' se dibuja— pero la TEXTURA se reserva fija, porque un tamano de textura que depende de la
        ' elevacion de la luz se recrea varias veces por arrastre de camara. O sea: la resolucion es la de
        ' antes, la VRAM no. Ver ShadowMapMath.UvScaleDeCapa.
        ' ===================== REPARTO DE CAPAS =====================
        ' Que luces castean lo decide el RIG (PreviewLight.CastsShadow) y lo resuelve una funcion PURA,
        ' con orden fijo y sin alocar: ver ShadowMapMath.SlotsDeSombra y su gate `shadow-slots`.
        Dim rigVivo = Config_App.Current.ActiveLights()
        _shadowCount = ShadowMapMath.SlotsDeSombra(rigVivo, _shadowSlots)
        If _shadowCount <= 0 Then SoltarMapasDeSombra() : Exit Sub

        ' ⭐ EL ENCUADRE ES POR LUZ PERO EL TAMANO DE TEXEL ES COMUN: Fit toma el extent de la esfera
        ' envolvente, invariante a la rotacion, asi que Radius/TexelWorld/DepthRange salen iguales para
        ' las cuatro y lo unico que cambia es la ViewProj. Por eso las capas de un array alcanzan.
        For luz = 0 To PreviewShadowSettings.MaxShadowLights - 1
            Dim capa = _shadowSlots(luz)
            If capa < 0 Then Continue For
            _shadowFits(capa) = ShadowMapMath.Fit(_frameLights.DirDeLuz(luz), bmin, bmax, cfg.MapSize)
            If Not _shadowFits(capa).Valid Then _shadowCount = 0 : SoltarMapasDeSombra() : Exit Sub
        Next

        If ParentControl.ShadowTarget Is Nothing Then ParentControl.ShadowTarget = New ShadowMapTarget()
        ' ⛔ `_shadowCount = 0` EN LAS SALIDAS TEMPRANAS, no solo en la cabecera del metodo. El render no se
        ' rompia —UploadShadowUniforms sube bShadows=False por `active`— pero `ShadowCount` es una propiedad
        ' que el ARNES lee, y en un frame que no dibujo ni un mapa reportaba "2 casters". Dos checks
        ' (`strength-cero` y `sin-casters`) se apoyan justo en ese numero: quedaban midiendo contra un valor
        ' que describe una intencion, no lo que se dibujo.
        If Not ParentControl.ShadowTarget.Ensure(cfg.MapSize, _shadowCount) Then _shadowCount = 0 : SoltarMapasDeSombra() : Exit Sub

        ' ⛔ NI UN glGet NI UN ARRAY POR FRAME ACA. El doc de ShadowMapTarget.BindForWrite dice que los
        ' glGet de framebuffer son los que fuerzan a varios drivers a vaciar la lista de comandos diferida
        ' — y el caller hacia justo uno por frame, mas otro del viewport, mas un array de 4 Integer de
        ' basura de GC en el camino de dibujo. Los dos valores ya se conocen: RenderAll solo es alcanzable
        ' desde RenderScene, que dibuja contra el framebuffer 0, y el viewport es lastW/lastH (los fija
        ' ResizeViewport y son los mismos con los que se armo la proyeccion de este frame).
        Const prevFbo As Integer = 0

        For capa = 0 To _shadowCount - 1
            ' El mapa del PERSONAJE usa la capa entera: viewport = lado reservado, escala de UV 1.0. La
            ' reserva-mas-grande-que-el-viewport es cosa del mapa ANCHO, cuyo tamano depende de la camara.
            _shadowUvScale(capa) = 1.0F
            RenderDepthInto(ParentControl.ShadowTarget, capa, cfg.MapSize, _shadowFits(capa), depthShader, casters)
        Next
        _shadowSettings = cfg
        _shadowActive = True

        ' ===================== MAPA 2: ANCHO, SOLO PARA EL SUELO =====================
        If Not cfg.GroundShadow Then
            ' Apagada la opcion, el mapa ancho se SUELTA. Sin esto quedaban colgados hasta el Clean 16 MB
            ' (2048, una luz casteante) o 64 MB (2048, las cuatro), contradiciendo el criterio del otro
            ' target ("con la opcion apagada nunca se asigna un byte de GPU").
            ' ⚠️ Este comentario decia "~3 MB": era la cuenta de cuando el mapa ancho se dimensionaba solo y
            ' salia tipicamente 512. Con la reserva fija mide lo mismo que el del personaje.
            ParentControl.GroundShadowTarget?.Release()
            OlvidarEncuadresDeSuelo()
        Else
            ' ⛔ EL RECEPTOR ES POR LUZ Y LA HUELLA ES LA UNION. Cada luz proyecta su propia sombra sobre
            ' el plano y necesita SU capa. Si se recortara a la huella de una sola, la sombra de las
            ' otras saldria cortada en seco — que es exactamente el sintoma que ExpandForGroundShadow
            ' existe para evitar.
            ' ⭐⛔ LA FORMA DEL ARRAY ES FUNCION DE LA CONFIG, NO DE LA CAMARA, Y ESO ES EL ARREGLO DEL
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
                ' ⛔⛔ NINGUNA LUZ CALIFICA ESTE FRAME => NO SE DIBUJA, PERO **NO SE SUELTA EL TARGET**.
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
                If Not ParentControl.GroundShadowTarget.Ensure(cfg.MapSize, _shadowCount) Then
                    ' El target no se pudo reservar: este frame no hay receptor, y los encuadres del frame
                    ' anterior no valen. Misma razon que la rama de arriba.
                    OlvidarEncuadresDeSuelo()
                Else
                    Dim algunaDibujada As Boolean = False
                    For capa = 0 To _shadowCount - 1
                        _groundUvScale(capa) = 1.0F
                        _groundValida(capa) = False
                        ' ⛔ Y EL ENCUADRE SE BORRA, no se deja el del frame pasado. `_groundFits(capa)` solo
                        ' se asigna si la capa califica, asi que sin esto una luz que ESTE frame quedo bajo la
                        ' elevacion minima seguia publicando por `GroundFit` el encuadre de cuando si
                        ' calificaba — un dato rancio que el arnes leeria como si fuera de este frame. Un
                        ' LightFit en cero se nota (Valid = False, Radius = 0); uno viejo se cree.
                        _groundFits(capa) = Nothing
                        If Not califica(capa) Then Continue For
                        ' El tamano LOGICO de esta capa sale de su propio radio: una key alta y un fill
                        ' rasante conservan cada uno su resolucion optima, que es lo que se perdia al
                        ' compartir un unico tamano de textura. Sigue siendo la misma funcion pura de
                        ' siempre, con sus dos trampas cubiertas por `ground-mapsize`.
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
                    ' ⛔⭐ ACA HABIA UN "LIMPIAR LAS CAPAS QUE NO CALIFICAN", Y SE SACO PORQUE NO PROTEGIA DE
                    ' NADA. El argumento era: TexImage3D con IntPtr.Zero deja el contenido indefinido, asi
                    ' que una capa nunca dibujada se samplearia como profundidad basura. Las dos mitades son
                    ' falsas hoy:
                    '  1. La poblacion que se limpiaba y la que el fragment SALTEA son la MISMA por
                    '     construccion: se limpiaba `Not _groundValida(capa)`, y SubirAporteDelSuelo le pone
                    '     contribucion CERO a esas mismas capas, y el fragment hace `continue` sobre
                    '     contribucion cero. Se limpiaba algo que no se lee nunca.
                    '  2. Y si alguien sacara ese `continue`, limpiar TAMPOCO salvaria: la capa que no
                    '     califica tiene la ViewProj en cero —se la deja asi el `_groundFits(capa) = Nothing`
                    '     del bucle de arriba, que es el UNICO punto que deja una capa en cero con
                    '     `_groundCount` todavia mayor que cero— asi que el lookup divide por w = 0 y las
                    '     coordenadas salen NaN, sin importar que profundidad haya guardada adentro.
                    ' Costaba un glFramebufferTextureLayer + un glClear por capa y por frame, o sea la misma
                    ' revalidacion de FBO que `_capaAttachada` se agrego a evitar. El unico guardian real es
                    ' el `continue` del fragment, y ahi esta dicho.
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
    ''' <para>⛔ NO ES HIGIENE: <c>_groundFits</c> sobrevive entre frames, y sin esto una capa que este frame
    ''' no califica sigue publicando por <c>GroundFit</c> el encuadre de cuando SI calificaba — con
    ''' <c>Valid = True</c>, o sea indistinguible de uno fresco para quien lo lee. Un <c>LightFit</c> en cero
    ''' se nota; uno viejo se cree.</para>
    ''' <para>⚠️ NO es esta funcion la que alimenta el <c>continue</c> del fragment del suelo, aunque lo
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
    ''' <para>⛔ EL MAPEO LUZ→CAPA VIAJA EN <c>uShadowSlot</c> Y NO SE RECALCULA EN EL SHADER. Es la misma
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

        shader.SetFloat("uShadowIntensity", _shadowSettings.Intensity)
        ' | EL NORMAL-OFFSET ES CERO PARA EL RECEPTOR DE SUELO, y no es una omision. Su unica funcion es
        ' matar el auto-sombreado de superficies rasantes, y el quad del piso NO ES CASTER: no puede
        ' auto-sombrearse. En cambio SI paga el desvio: el mapa del suelo tiene texeles mucho mas grandes
        ' y eso se traduce en sombra DESPEGADA del pie (peter-panning).
        ' | ES UN ESCALAR PARA TODAS LAS CAPAS porque el texel del mapa del personaje es el mismo en
        ' todas (esfera envolvente). Ver el comentario del bloque de uniforms en el GLSL.
        shader.SetFloat("uShadowNormalBias", normalBiasTexels * fits(0).TexelWorld)

        ' | LA SUAVIDAD ES CONTINUA: el radio entero es el techo y el SOBRANTE viaja en el espaciado de
        ' los taps, que es lo que hace continuo el desenfoque sin cambiar la cantidad de muestras.
        Dim soft As Single = Math.Clamp(_shadowSettings.SoftnessTexels, 0.0F, PreviewShadowSettings.MaxPcfRadius)
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
        ' 14 espeja el t14 del motor para el pase iluminado; 15 queda para el suelo. ⛔ Las dos son
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

        ' Resolver el rig UNA vez por frame, antes de cualquier draw. Los dos consumidores de ApplyMaterial
        ' (Render y RenderOverlayLayer) sólo se alcanzan desde los loops de abajo, así que nunca lo leen stale.
        ResolveFrameLights(camera)

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
                             TextureUnit.Texture14, _shadowSettings.NormalBiasTexels,
                             _shadowSettings.DepthBiasTexels * _shadowFits(0).TexelWorld)

        ' Note: ShapeDataLoaded is intentionally NOT checked here. Each mesh.Render() guards
        ' against null RelatedNifShape internally. Checking ShapeDataLoaded at this level would
        ' stop rendering all meshes whose VBOs are still valid just because the CPU-side shapedata
        ' was evicted by the LRU, which is an unnecessary regression in render quality.

        If RenderBucketsDirty OrElse (OpaqueMeshes.Count + CutoutMeshes.Count + DecalMeshes.Count + BlendedMeshes.Count) <> meshes.Count Then
            RebuildRenderBuckets()

            ' ⛔⛔ SACADO: el re-orden de OPAQUE y CUTOUT por `DiffuseTexture_ID` (era la optimizacion "O3.5",
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
            ' El orden que queda es el de RebuildRenderBuckets: CompareMeshIdx = Shape.ShapeIndex. Determinista
            ' y ajeno a que texturas tenga cada malla.
            ' ⭐ Si algun dia se quiere el batching de verdad: PRIMERO hacer que BindTexture saltee redundantes,
            ' y recien ahi ordenar por la RUTA DECLARADA del material (Diffuse_or_Base_Texture), que ningun
            ' camino de composicion muta — NUNCA por el id resuelto.
            '
            ' ⚠️ DIAGNOSTICO (Logger.Enabled): se conserva el volcado del orden para poder verificar que ahora
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
                Logger.LogLazy(Function() dump("CUTOUT", CutoutMeshes))
                Logger.LogLazy(Function() dump("BLENDED", BlendedMeshes))
            End If
        End If

        ' O3.3: Compute view-projection matrix for frustum culling
        Dim viewMatrix = camera.GetViewMatrix()
        Dim vp As Matrix4 = viewMatrix * projection
        ' Los planos del frustum de la camara: constantes para los cinco bucles de abajo.
        RenderableMesh.ExtractFrustumPlanes(vp, _framePlanes)

        ' 1. OPAQUE — sin blending, depth write habilitado
        For Each mesh In OpaqueMeshes
            ' O3.3: Skip meshes whose AABB is entirely outside the view frustum
            If Not RenderableMesh.IsAABBInFrustum(mesh.BoundsMin, mesh.BoundsMax, _framePlanes) Then Continue For
            mesh.Render(projection, camera)
        Next

        ' 2. CUTOUT — alpha test, sin blending, depth write habilitado
        For Each mesh In CutoutMeshes
            If Not RenderableMesh.IsAABBInFrustum(mesh.BoundsMin, mesh.BoundsMax, _framePlanes) Then Continue For
            mesh.Render(projection, camera)
        Next
        ' 3. DECAL — overlay coplanar ocluido por depth de escena
        If DecalMeshes.Count > 0 Then
            For Each mesh In DecalMeshes
                If Not RenderableMesh.IsAABBInFrustum(mesh.BoundsMin, mesh.BoundsMax, _framePlanes) Then Continue For
                mesh.Render(projection, camera)
            Next
        End If

        ' 3b. RECEPTOR DE SUELO — la silueta del personaje sobre el plano del piso.
        ' ⛔ EL ORDEN ES ESTE Y NO OTRO: despues de OPAQUE/CUTOUT/DECAL para que el personaje lo tape por
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
                                 _shadowSettings.DepthBiasTexels * _shadowFits(0).TexelWorld)
            SubirAporteDelSuelo(ParentControl.SharedGroundShadowShader)
            _groundQuad.Render(ParentControl.SharedGroundShadowShader, vp, _groundQuadCenter, _groundQuadHalf)
        End If

        ' 4. BLENDED — requiere ordenamiento por profundidad.
        ' (Was an early `Exit Sub` when empty; now a guarded block so the overlay pass 5 below still
        ' runs even with zero blended meshes — tattoos live on the OPAQUE skin body, not a blended mesh.)
        If BlendedMeshes.Count > 0 Then
            BlendedDepthBuffer.Clear()

            For Each mesh In BlendedMeshes
                ' O3.3: Frustum cull blended meshes too
                If Not RenderableMesh.IsAABBInFrustum(mesh.BoundsMin, mesh.BoundsMax, _framePlanes) Then Continue For
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
            If Not RenderableMesh.IsAABBInFrustum(mesh.BoundsMin, mesh.BoundsMax, _framePlanes) Then Continue For
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





