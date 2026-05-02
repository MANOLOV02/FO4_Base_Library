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


    Public WithEvents RenderTimer As New System.Windows.Forms.Timer
    Private DebugProc As DebugProc
    Public Property AllowMask As Boolean = False

    ' -- Pull-based pipeline state --
    Private _renderIntent As RenderIntent
    ''' <summary>Tracks the original shapes reference from the last full reload, for identity comparison.</summary>
    Private _lastLoadedShapesSource As IEnumerable(Of IRenderableShape)
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
    Public Property BrushRadiusPx As Integer = 5
    Public Property InvertMasking As Boolean = False


    ''' <summary>
    ''' Crea una textura 2D de w×h píxeles con el color indicado.
    ''' </summary>
    ''' 
    Private Shared Function CreateColorTexture(w As Integer, h As Integer, r As Byte, g As Byte, b As Byte, a As Byte) As Integer
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
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, CInt(TextureMinFilter.Nearest))
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, CInt(TextureMagFilter.Nearest))
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, CInt(TextureWrapMode.ClampToEdge))
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, CInt(TextureWrapMode.ClampToEdge))

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
        If Me.IsDisposed Then Exit Sub
        Me.MakeCurrent()
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
        .APIVersion = New Version(4, 4),
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
        i.MarkDirty(RenderDirtyFlags.Shapes Or RenderDirtyFlags.Camera)
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

    ''' <summary>
    ''' The single hot path. Reads Intent.DirtyFlags and executes the minimum work needed.
    ''' Three execution modes emerge from flag combinations:
    '''   Shapes|Force ? full reload (clean, skeleton, geometry, morphs, GPU upload)
    '''   Pose         ? incremental (skeleton, bone matrices, optional morphs)
    '''   Morphs       ? lightweight (reapply morphs, update skin buffers)
    ''' </summary>
    Private Sub ExecuteRenderPipeline()
        Dim intent = _renderIntent
        If intent Is Nothing OrElse Not intent.HasWork Then Return
        If Me.Disposing OrElse Me.IsDisposed OrElse Not Visible Then Return
        If intent.Shapes Is Nothing OrElse Not intent.Shapes.Any() Then
            Model.FloorOffset = 0
            Model.Clean(False)
            Model.CleanTextures()
            Model.LoadedShapes.Clear()
            _lastLoadedShapesSource = Nothing
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
            If needsCameraReset OrElse isNewShapeSet Then ResetCamera()
            RefreshRender()

        ElseIf needsPoseUpdate Then
            ' -- Pose change (incremental) ----------------------------
            PipelineStep_Skeleton(intent)

            ' Morphs (if also dirty — preset+pose changed simultaneously)
            If needsMorphUpdate Then
                PipelineStep_Morphs(intent)
            End If

            ' Recompute bone matrices + GPU upload — only for shapes the caller marked dirty.
            ' Empty DirtyShapes (default) means "all shapes" (back-compat single-actor flow).
            For Each mesh In Model.meshes
                If Not intent.IsShapeDirty(mesh.MeshData.Shape) Then Continue For
                Dim meshSkel As SkeletonInstance = If(intent.SkeletonResolver IsNot Nothing, intent.SkeletonResolver.ResolveFor(mesh.MeshData.Shape), Nothing)
                ' Pose is implicit in the SkeletonInstance: the caller applied it via ApplyPose.
                SkinningHelper.RecomputeGPUBoneMatrices(
                    mesh.MeshData.Shape, mesh.MeshData.Meshgeometry,
                    Model.SingleBoneSkinning, meshSkel)

                If Not Config_App.Current.Setting_GPUSkinning Then
                    mesh.MeshData.Meshgeometry.dirtyVertexIndices =
                        New HashSet(Of Integer)(Enumerable.Range(0, mesh.MeshData.Meshgeometry.Vertices.Length))
                    Array.Fill(mesh.MeshData.Meshgeometry.dirtyVertexFlags, True)
                End If

                mesh.UpdateSkinBuffers_GL()
                mesh.UpdateBoneMatricesSSBO()
                mesh.ComputeBounds()
            Next

            Model.MarkRenderBucketsDirty()
            If needsCameraReset Then ResetCamera()
            RefreshRender()

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

        intent.ClearDirty()
    End Sub

    ''' <summary>Resolve skeleton via app-provided resolver or default fallback. Pose state
    ''' lives in the SkeletonInstance(s) and gets re-applied by PrepareForShapes after
    ''' cloth-inject (idempotent — guarantees DeltaTransforms reflect the requested pose
    ''' even when cloth-inject re-creates bones).</summary>
    Private Sub PipelineStep_Skeleton(intent As RenderIntent)
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
    Private Sub PipelineStep_Morphs(intent As RenderIntent)
        For Each mesh In Model.meshes
            If Not intent.IsShapeDirty(mesh.MeshData.Shape) Then Continue For
            Dim plan As MorphPlan = Nothing
            If intent.MorphResolver IsNot Nothing Then
                plan = intent.MorphResolver.ResolveMorphPlan(mesh.MeshData.Shape, mesh.MeshData.Meshgeometry)
            End If
            MorphEngine.ApplyMorphPlan(
                mesh.MeshData.Meshgeometry, plan,
                intent.RecalculateNormals,
                allowMask:=AllowMask,
                maskedVertices:=mesh.MeshData.Shape.MaskedVertices)
        Next
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
        Me.MakeCurrent()

        ' 2) (Opcional) Debug Output para capturar sólo errores
        GL.Enable(EnableCap.DebugOutput)
        GL.Enable(EnableCap.DebugOutputSynchronous)
        DebugProc = AddressOf DebugCallback
        GL.DebugMessageCallback(DebugProc, IntPtr.Zero)
        GL.DebugMessageControl(DebugSourceControl.DontCare, DebugTypeControl.DontCare, DebugSeverityControl.DebugSeverityHigh, 0, Array.Empty(Of Integer)(), True)

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
        Dim ulog As Action(Of String) = UpdateProjectionLogger
        If ulog IsNot Nothing Then ulog($"[UpdateProjection] ctrl=({Me.Width}x{Me.Height}) aspect={aspect:F3} fovY=45deg near={nearZ:F2} far={farZ:F2} sceneSize=({size.X:F2},{size.Y:F2},{size.Z:F2}) eyeToCenter={eyeToCenter:F2}")
    End Sub
    Private Sub RenderScene()
        If Me.IsDisposed Then Exit Sub
        Me.MakeCurrent()
        GL.ClearColor(Config_App.Current.Setting_BackColor)
        GL.Clear(ClearBufferMask.ColorBufferBit Or ClearBufferMask.DepthBufferBit)
        If Model.Can_Render Then
            Dim slog As Action(Of String) = RenderSceneLogger
            If slog IsNot Nothing Then
                slog($"[RenderScene] ctrl=({Me.Width}x{Me.Height}) cam.focus=({camera.FocusPosition.X:F2},{camera.FocusPosition.Y:F2},{camera.FocusPosition.Z:F2}) cam.distance={camera.distance:F2} cam.forward=({camera.Forward.X:F2},{camera.Forward.Y:F2},{camera.Forward.Z:F2}) FloorOffset={Model.FloorOffset:F2}")
            End If
            Model.RenderAll(projection, camera)
        End If
    End Sub
    Private Shared Sub FinishRenderFrame()
        GL.DepthMask(True)
        GL.Disable(EnableCap.Blend)
    End Sub

    Public Function CaptureBitmap() As Bitmap
        If Me.IsInDesignMode OrElse Me.Width <= 0 OrElse Me.Height <= 0 Then Return Nothing

        Me.MakeCurrent()
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
        If Me.IsInDesignMode OrElse Not UpdateRequired Then Exit Sub
        MyBase.OnPaint(e)
        ' Consume the current render request up front so any new request raised
        ' during RenderScene survives this frame and schedules the next one.
        UpdateRequired = False
        Try
            RenderScene()
            SwapBuffers()
            FinishRenderFrame()
        Catch ex As Exception
            Try
                Processing_Status("Render error")
            Catch
            End Try
        End Try
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
        UpdateRequired = True
        Me.Invalidate()
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
        For Each mesh In Model.meshes
            min = Vector3.ComponentMin(min, mesh.MeshData.Meshgeometry.Minv)
            max = Vector3.ComponentMax(max, mesh.MeshData.Meshgeometry.Maxv)
        Next
    End Sub
    ''' <summary>Optional diagnostic logger called from CenterCamera at each step. Set by the
    ''' app (NPC_Manager) to forward into NpcPreviewLog. Null = no logging (default). NOT a
    ''' library bug — it's a debug injection point used only when the app explicitly hooks it.</summary>
    Public Shared CenterCameraLogger As Action(Of String) = Nothing
    ''' <summary>Optional diagnostic logger for UpdateProjection — emits the actual aspect/near/far
    ''' used to build the projection matrix at draw time. Useful to detect if the projection in
    ''' use at draw differs from what CenterCamera assumed when computing the camera distance.</summary>
    Public Shared UpdateProjectionLogger As Action(Of String) = Nothing
    ''' <summary>Diagnostic — emits camera state at each RenderScene tick (FocusPosition, distance,
    ''' Forward). Optional, default null.</summary>
    Public Shared RenderSceneLogger As Action(Of String) = Nothing

    Public Sub CenterCamera()
        If Me.IsInDesignMode Then Return

        Dim log As Action(Of String) = CenterCameraLogger

        ' 1) AABB
        Dim minB As Vector3, maxB As Vector3
        GetSceneBounds(minB, maxB)
        If log IsNot Nothing Then log($"[CenterCamera] AABB min=({minB.X:F2},{minB.Y:F2},{minB.Z:F2}) max=({maxB.X:F2},{maxB.Y:F2},{maxB.Z:F2})")

        ' 2) Centro y tamaño
        Dim center As Vector3 = (minB + maxB) * 0.5F
        Dim size As Vector3 = maxB - minB
        If log IsNot Nothing Then log($"[CenterCamera] center=({center.X:F2},{center.Y:F2},{center.Z:F2}) size=({size.X:F2},{size.Y:F2},{size.Z:F2})")

        ' 3) Focus y orbit mode
        camera.FocusPosition = center

        ' 4) Parámetros de cámara
        Dim fovY As Single = MathHelper.DegreesToRadians(45.0F)
        Dim aspect As Single = Me.Width / CSng(Me.Height)
        If log IsNot Nothing Then log($"[CenterCamera] ctrlSize=({Me.Width}x{Me.Height}) aspect={aspect:F3} fovY=45deg")

        ' ** Usamos Z para altura, X para anchura y Y para profundidad (hacia la cámara) **
        Dim halfH As Single = size.Z * 0.5F   ' vertical ? Z
        Dim halfW As Single = size.X * 0.5F   ' horizontal ? X
        Dim halfD As Single = size.Y * 0.5F   ' profundidad ? Y

        ' 5) Calculamos distancias mínimas sin margen
        Dim distH = halfH / CSng(Math.Tan(fovY * 0.5F))
        Dim fovX = 2.0F * CSng(Math.Atan(Math.Tan(fovY * 0.5F) * aspect))
        Dim distW = halfW / CSng(Math.Tan(fovX * 0.5F))
        If log IsNot Nothing Then log($"[CenterCamera] halfH={halfH:F2} halfW={halfW:F2} halfD={halfD:F2} distH={distH:F2} distW={distW:F2} fovX={CSng(MathHelper.RadiansToDegrees(fovX)):F2}deg")

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
        If log IsNot Nothing Then log($"[CenterCamera] baseDist={baseDistance:F2} ideal={idealDistance:F2} (margin {marginPct * 100:F0}%) → clamp[{camera.MinDistance:F2}..{camera.MaxDistance:F2}] → distance={clampedDist:F2}")

        ' 7) Reset ángulos y orientación
        camera.angleX = 0F
        camera.angleY = 0F
        camera.UpdateDirectionFromAngles()
        UpdateProjection(True)
        If log IsNot Nothing Then log($"[CenterCamera] DONE focus=({camera.FocusPosition.X:F2},{camera.FocusPosition.Y:F2},{camera.FocusPosition.Z:F2}) distance={camera.distance:F2}")
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then Clean()
        MyBase.Dispose(disposing)
    End Sub
    Private Sub RenderTimer_Tick(sender As Object, e As EventArgs) Handles RenderTimer.Tick
        ' Pull-based: if the intent has pending work, execute the pipeline
        If _renderIntent IsNot Nothing AndAlso _renderIntent.HasWork Then
            ExecuteRenderPipeline()
        End If

        ' Then handle the normal repaint cycle (textures loading, camera interaction, etc.)
        Dim texturesPending As Boolean = (Model IsNot Nothing AndAlso Not Model.TexturesReady)
        If (UpdateRequired OrElse texturesPending) AndAlso RenderTimer.Enabled = True Then
            UpdateRequired = True  ' ensure OnPaint won't skip
            Me.Invalidate()  ' disparará OnPaint
        End If
    End Sub
    Public Sub Clean()
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

        If RenderTimer IsNot Nothing Then
            RenderTimer.Stop()
            RenderTimer.Dispose()
            RenderTimer = Nothing
        End If
        If defaultWhiteTex <> 0 Then GL.DeleteTexture(defaultWhiteTex)
        If defaultNormalTex <> 0 Then GL.DeleteTexture(defaultNormalTex)
        If defaultCubeMap <> 0 Then GL.DeleteTexture(defaultCubeMap)
        GL.DebugMessageCallback(Nothing, IntPtr.Zero)
    End Sub
    Protected Overrides Sub Finalize()
        MyBase.Finalize()
    End Sub
End Class
Public Class PreviewModel

    Public Textures_Dictionary As New Dictionary(Of String, Texture_Loaded_Class)(StringComparer.OrdinalIgnoreCase)
    Public Can_Render As Boolean = False
    Public Property TexturesReady As Boolean = True
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
        Public Class MaterialData
            Sub New(Parent As MeshData_Class)
                ParentMeshData = Parent
            End Sub
            Public Property ParentMeshData As MeshData_Class
            Public ReadOnly Property MaterialBase As FO4UnifiedMaterial_Class
                Get
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

            Public ReadOnly Property HasAlphaBlend
                Get
                    If IsNothing(ParentMeshData.Shape.ShapeMaterial) Then Return False
                    If MaterialBase.AlphaBlendMode = AlphaBlendModeType.None Then Return False
                    If MaterialBase.AlphaBlendMode = AlphaBlendModeType.Standard Then Return True
                    If MaterialBase.AlphaBlendMode = AlphaBlendModeType.Multiplicative Then Return True
                    If MaterialBase.AlphaBlendMode = AlphaBlendModeType.Additive Then Return True
                    If MaterialBase.AlphaBlendMode = AlphaBlendModeType.Unknown Then Return GetAlphaFromShape() OrElse MaterialBase.Alpha < 1.0F
                    Debugger.Break()
                    Return False
                End Get
            End Property
            Private Function GetAlphaFromShape() As Boolean
                If Not IsNothing(ParentMeshData.Shape.NifShape.AlphaPropertyRef) AndAlso ParentMeshData.Shape.NifShape.AlphaPropertyRef.Index <> -1 Then
                    Dim alp = CType(ParentMeshData.Shape.NifContent.Blocks(ParentMeshData.Shape.NifShape.AlphaPropertyRef.Index), NiflySharp.Blocks.NiAlphaProperty)
                    If alp.Flags.AlphaBlend Then
                        Return True
                    Else
                        Return False
                    End If
                End If
                Return False
            End Function

            Public ReadOnly Property HasAlphaTest
                Get
                    If IsNothing(ParentMeshData.Shape.ShapeMaterial) Then Return False
                    Return MaterialBase.AlphaTest
                End Get
            End Property
            Public Function Calculate_Blending() As Integer()
                Select Case MaterialBase.AlphaBlendMode
                    Case AlphaBlendModeType.Standard
                        Return {BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha}
                    Case AlphaBlendModeType.Additive
                        Return {BlendingFactor.SrcAlpha, BlendingFactor.One}
                    Case AlphaBlendModeType.Multiplicative
                        Return {BlendingFactor.DstColor, BlendingFactor.Zero}
                    Case AlphaBlendModeType.Unknown
                        Dim src As BlendingFactor = BlendingFactor.SrcAlpha
                        Dim dst As BlendingFactor = BlendingFactor.OneMinusSrcAlpha
                        Try
                            If Not IsNothing(ParentMeshData.Shape.NifShape.AlphaPropertyRef) AndAlso ParentMeshData.Shape.NifShape.AlphaPropertyRef.Index <> -1 Then
                                Dim alp = CType(ParentMeshData.Shape.NifContent.Blocks(ParentMeshData.Shape.NifShape.AlphaPropertyRef.Index), NiflySharp.Blocks.NiAlphaProperty)

                                Select Case alp.Flags.SourceBlendMode
                                    Case NiflySharp.Enums.AlphaFunction.DEST_ALPHA
                                        src = BlendingFactor.DstAlpha
                                    Case NiflySharp.Enums.AlphaFunction.DEST_COLOR
                                        src = BlendingFactor.DstColor
                                    Case NiflySharp.Enums.AlphaFunction.INV_DEST_ALPHA
                                        src = BlendingFactor.OneMinusDstAlpha
                                    Case NiflySharp.Enums.AlphaFunction.INV_DEST_COLOR
                                        src = BlendingFactor.OneMinusDstColor
                                    Case NiflySharp.Enums.AlphaFunction.INV_SRC_ALPHA
                                        src = BlendingFactor.OneMinusSrcAlpha
                                    Case NiflySharp.Enums.AlphaFunction.INV_SRC_COLOR
                                        src = BlendingFactor.OneMinusSrcColor
                                    Case NiflySharp.Enums.AlphaFunction.ONE
                                        src = BlendingFactor.One
                                    Case NiflySharp.Enums.AlphaFunction.SRC_ALPHA
                                        src = BlendingFactor.SrcAlpha
                                    Case NiflySharp.Enums.AlphaFunction.SRC_ALPHA_SATURATE
                                        src = BlendingFactor.SrcAlphaSaturate
                                    Case NiflySharp.Enums.AlphaFunction.SRC_COLOR
                                        src = BlendingFactor.SrcColor
                                    Case NiflySharp.Enums.AlphaFunction.ZERO
                                        src = BlendingFactor.Zero
                                End Select
                                Select Case alp.Flags.DestinationBlendMode
                                    Case NiflySharp.Enums.AlphaFunction.DEST_ALPHA
                                        dst = BlendingFactor.DstAlpha
                                    Case NiflySharp.Enums.AlphaFunction.DEST_COLOR
                                        dst = BlendingFactor.DstColor
                                    Case NiflySharp.Enums.AlphaFunction.INV_DEST_ALPHA
                                        dst = BlendingFactor.OneMinusDstAlpha
                                    Case NiflySharp.Enums.AlphaFunction.INV_DEST_COLOR
                                        dst = BlendingFactor.OneMinusDstColor
                                    Case NiflySharp.Enums.AlphaFunction.INV_SRC_ALPHA
                                        dst = BlendingFactor.OneMinusSrcAlpha
                                    Case NiflySharp.Enums.AlphaFunction.INV_SRC_COLOR
                                        dst = BlendingFactor.OneMinusSrcColor
                                    Case NiflySharp.Enums.AlphaFunction.ONE
                                        dst = BlendingFactor.One
                                    Case NiflySharp.Enums.AlphaFunction.SRC_ALPHA
                                        dst = BlendingFactor.SrcAlpha
                                    Case NiflySharp.Enums.AlphaFunction.SRC_ALPHA_SATURATE
                                        dst = BlendingFactor.SrcAlphaSaturate
                                    Case NiflySharp.Enums.AlphaFunction.SRC_COLOR
                                        dst = BlendingFactor.SrcColor
                                    Case NiflySharp.Enums.AlphaFunction.ZERO
                                        dst = BlendingFactor.Zero
                                End Select

                            End If
                        Catch ex As Exception
                            Debugger.Break()
                        End Try

                        Return {src, dst}
                    Case Else
                        Throw New Exception
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
            Public ReadOnly Property TintMaskTexture_ID As UInteger
                Get
                    Return GetTextureID(FO4UnifiedMaterial_Class.CorrectTexturePath(MaterialBase.TintMaskTexture))
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

        Public Sub UpdateSkinBuffers_GL()
            ' Actualiza VBOs de Normales, Tangentes, Bitangentes y Posiciones
            ' Detect skinning mode change: if the toggle changed since last upload, force ALL dirty
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
                                                             Dim nm3 As Matrix3d = New Matrix3d(m)
                                                             nm3.Invert()
                                                             nm3.Transpose()
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

                    GL.BindBuffer(BufferTarget.ArrayBuffer, vboPosition)
                    GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, totalBytes, posF)

                    GL.BindBuffer(BufferTarget.ArrayBuffer, vboNormal)
                    GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, totalBytes, nrmF)

                    GL.BindBuffer(BufferTarget.ArrayBuffer, vboTangent)
                    GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, totalBytes, tanF)

                    GL.BindBuffer(BufferTarget.ArrayBuffer, vboBitangent)
                    GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, totalBytes, bitanF)

                    GL.BindBuffer(BufferTarget.ArrayBuffer, 0)

                    ' Clear all dirty flags since everything was updated
                    For Each i As Integer In MeshData.Meshgeometry.dirtyVertexIndices
                        MeshData.Meshgeometry.dirtyVertexFlags(i) = False
                    Next
                    MeshData.Meshgeometry.dirtyVertexIndices.Clear()

                    ' Also recompute bounds after full update
                    ComputeBounds()

                    UpdateUpdateSkinBuffersMask_GL()
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
                        Dim nm3 As Matrix3d = New Matrix3d(m)
                        nm3.Invert()
                        nm3.Transpose()

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
                ComputeBounds()
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
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, ssbo_BoneMatrices)
            GL.BufferSubData(BufferTarget.ShaderStorageBuffer, IntPtr.Zero, MeshData.Meshgeometry.GPUBoneMatrices.Length * 64, MeshData.Meshgeometry.GPUBoneMatrices)
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0)
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
                GL.BindBuffer(BufferTarget.ShaderStorageBuffer, ssbo_BoneMatrices)
                GL.BufferData(BufferTarget.ShaderStorageBuffer, MeshData.Meshgeometry.GPUBoneMatrices.Length * 64, MeshData.Meshgeometry.GPUBoneMatrices, BufferUsageHint.DynamicDraw)
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

        Private Shared Function ResolvePolygonOffset(materialBase As FO4UnifiedMaterial_Class) As PolygonOffsetState
            If materialBase Is Nothing Then Return PolygonOffsetState.Disabled

            If Not materialBase.Decal Then
                Return PolygonOffsetState.Disabled
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
            Dim hasBacklightTexture As Boolean = materialBase.BackLighting

            ' ShaderType EnvironmentMap: SmoothSpec slot is actually an environment
            ' mask, not a specular map (e.g. eyeenvironmentmask_m.dds).
            ' Done here in render (not in material properties) to avoid corrupting
            ' save flows that read-modify-write the BGSM fields.
            If materialBase.NifShaderType = NiflySharp.Enums.BSLightingShaderType.EnvironmentMap AndAlso
               smoothSpecTextureId <> 0 AndAlso envmapMaskTextureId = 0 Then
                envmapMaskTextureId = smoothSpecTextureId
                smoothSpecTextureId = 0
            End If

            Dim hasSpecMap As Boolean = (smoothSpecTextureId <> 0)
            Dim isSSE As Boolean = TypeOf shader Is Shader_Class_SSE
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
            shader.SetFloat("ambient", Config_App.Current.Setting_Lightrig.Ambient)

            shader.SetVector3("frontal.diffuse", Config_App.Current.Setting_Lightrig.DirectL.GetDifuse)
            shader.SetVector3("frontal.direction", Config_App.Current.Setting_Lightrig.DirectL.GetDirection(cam))
            ' Luz direccional 0
            shader.SetVector3("directional0.diffuse", Config_App.Current.Setting_Lightrig.FillLight_1.GetDifuse)
            shader.SetVector3("directional0.direction", Config_App.Current.Setting_Lightrig.FillLight_1.GetDirection(cam))

            ' Luz direccional 1
            shader.SetVector3("directional1.diffuse", Config_App.Current.Setting_Lightrig.FillLight_2.GetDifuse)
            shader.SetVector3("directional1.direction", Config_App.Current.Setting_Lightrig.FillLight_2.GetDirection(cam))

            ' Luz direccional 2
            shader.SetVector3("directional2.diffuse", Config_App.Current.Setting_Lightrig.BackLight.GetDifuse)
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


            ' texLightmask is SSE-only (rimlight/softlight masking); FO4 does not use it
            ' For FaceTint, slot 6 (LightingTexture) is the tint mask, not lightmask
            If isSSE AndAlso Not materialBase.Facegen Then
                If lightingTextureId <> 0 Then
                    shader.BindTexture("texLightmask", lightingTextureId, TextureUnit.Texture7)
                Else
                    shader.BindTexture("texLightmask", Me.ParentModel.ParentControl.defaultWhiteTex, TextureUnit.Texture7)
                End If
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
            shader.SetBool("bEnvMap", materialBase.EnvironmentMapping)
            shader.SetBool("bAlphaTest", hasAlphaTest)
            shader.SetBool("bEnvMask", envmapMaskTextureId <> 0)
            shader.SetBool("bNormalMap", normalTextureId <> 0)
            shader.SetBool("bGreyscaleColor", materialBase.GrayscaleToPaletteColor AndAlso greyscaleTextureId <> 0)
            shader.SetBool("bSpecular", materialBase.SpecularEnabled AndAlso hasSpecularSource)
            If isSSE Then shader.SetBool("bHasSpecMap", hasSpecMap)
            shader.SetBool("bModelSpace", materialBase.ModelSpaceNormals)
            shader.SetBool("bEmissive", materialBase.EmitEnabled)
            shader.SetBool("bSoftlight", materialBase.SubsurfaceLighting)
            shader.SetBool("bGlowmap", materialBase.Glowmap AndAlso glowTextureId <> 0)
            If isSSE Then shader.SetBool("bLightmask", lightingTextureId <> 0 AndAlso Not materialBase.Facegen)
            shader.SetFloat("shininess", materialBase.Smoothness)
            shader.SetVector3("specularColor", Shader_Base_Class.Color_to_Vector(materialBase.SpecularColor))
            shader.SetFloat("specularStrength", materialBase.SpecularMult)
            shader.SetVector3("emissiveColor", Shader_Base_Class.Color_to_Vector(materialBase.EmittanceColor))
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

            ' SkinTint / HairTint
            Dim hasTint As Boolean = materialBase.SkinTint OrElse materialBase.Hair
            shader.SetBool("bHasTintColor", hasTint)
            If hasTint Then
                Dim tint As Color = If(materialBase.SkinTint, materialBase.SkinTintColor, materialBase.HairTintColor)
                shader.SetVector3("tintColor", Shader_Base_Class.Color_to_Vector(tint))
            End If

            ' FaceTint: detail mask + tint mask (SSE only)
            If isSSE Then
                Dim detailMaskId = material.DetailMaskTexture_ID
                Dim tintMaskId = material.TintMaskTexture_ID
                Dim isFaceTint As Boolean = materialBase.Facegen
                shader.SetBool("bHasDetailMask", isFaceTint AndAlso detailMaskId <> 0)
                shader.SetBool("bHasTintMask", isFaceTint AndAlso tintMaskId <> 0)
                If isFaceTint AndAlso detailMaskId <> 0 Then
                    shader.BindTexture("texDetailMask", detailMaskId, TextureUnit.Texture8)
                End If
                If isFaceTint AndAlso tintMaskId <> 0 Then
                    shader.BindTexture("texTintMask", tintMaskId, TextureUnit.Texture9)
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
            shader.SetInt("shaderType", CInt(materialBase.NifShaderType))
            shader.SetBool("bEffectFalloff", materialBase.FalloffEnabled)
            shader.SetBool("bEffectFalloffColor", materialBase.FalloffColorEnabled)
            shader.SetBool("bEffectGreyscaleAlpha", materialBase.GrayscaleToPaletteAlpha)
            shader.SetFloat("effectLightingInfluence", If(materialBase.EffectLightingEnabled, materialBase.LightingInfluence, 0.0F))
            shader.SetVector4("effectFalloffParams", New OpenTK.Mathematics.Vector4(materialBase.FalloffStartAngle, materialBase.FalloffStopAngle, materialBase.FalloffStartOpacity, materialBase.FalloffStopOpacity))
            shader.SetVector3("effectBaseColor", Shader_Base_Class.Color_to_Vector(materialBase.BaseColor))
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
        If Me.ParentControl.IsDisposed Then Exit Sub
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
            Dim skel As SkeletonInstance = If(resolver IsNot Nothing, resolver.ResolveFor(shape), Nothing)
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
            Debug.Print("[EXCEPTION] " & ex.Message)


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
        ParentControl.MakeCurrent()
        For Each mesh In meshes
            mesh.SetupMesh_GL()
        Next
    End Sub

    Private ReadOnly Last_Loaded_Textures As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)

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

                    ' Signal the GL thread to wake up and process pending uploads.
                    ' Without this, textures stay as fallback until the user interacts (rotate/zoom).
                    If controlRef IsNot Nothing AndAlso Not controlRef.IsDisposed AndAlso controlRef.IsHandleCreated Then
                        controlRef.BeginInvoke(Sub()
                                                   controlRef.UpdateRequired = True
                                                   controlRef.Invalidate()
                                               End Sub)
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
                    Debug.Print($"[O4.1] Background texture load failed: {ex.Message}")
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
                        Dim result = DirectXDDSLoader.UploadTextureToGL(tex, path)

                        If result IsNot Nothing AndAlso result.Loaded AndAlso result.Texture_ID > 0 Then
                            Textures_Dictionary(path) = result
                            Last_Loaded_Textures.Add(path)
                        Else
                            ' Failed to upload — mark as "attempted" to prevent re-enqueue loop
                            Textures_Dictionary.Remove(path)
                            Last_Loaded_Textures.Add(path)
                        End If
                    Catch ex As Exception
                        ' Upload failed — mark as "attempted" so Process_Textures_GL won't re-enqueue
                        Debug.Print($"[O4.1] GL upload failed for '{path}': {ex.Message}")
                        Textures_Dictionary.Remove(path)
                        Last_Loaded_Textures.Add(path)
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
                    ParentControl.UpdateRequired = True
                    ParentControl.Invalidate()
                End If
            End If
        End If
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
        Catch ex As Exception
            Debugger.Break()
        End Try
    End Sub
    Public Sub Clean(ShowText As Boolean)
        Cleaned = True
        Can_Render = False
        TexturesReady = True
        If Not IsNothing(ParentControl.RenderTimer) Then ParentControl.RenderTimer.Stop()
        ParentControl.MakeCurrent()
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

        If BlendedMeshes.Count = 0 Then Exit Sub

        ' 4. BLENDED — requiere ordenamiento por profundidad
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





