Imports OpenTK.Graphics.OpenGL4
Imports OpenTK.Mathematics

''' <summary>The app's small passes of the FO4 deferred frame that are not transcribed shaders. ASCII only (GLSL).</summary>
Friend Module Fo4DeferredAppSource
    ''' <summary>RT 0x28 (t15 of composite pass 2): z = n*f / ((n - f) * d + f) on the raw depth, mip 0, no clamp (Tools/re-docs/
    ''' RE_FO4_DEFERRED_FRAME_2026-10-03.md 17.3: CameraZAndMipsCS 3860; its exact operation order is NOT TRACED, NT7). Runs under
    ''' UPPER_LEFT on the D3D-order depth copy: same pixels.</summary>
    Friend Const Fragment_LinearZ As String = "#version 430
layout(binding = 7) uniform sampler2D tDepth;
uniform vec2 uNearFar;
layout(location = 0) out float oZ;
void main()
{
    float d = texelFetch(tDepth, ivec2(gl_FragCoord.xy), 0).x;
    float n = uNearFar.x, f = uNearFar.y;
    oZ = (n * f) / ((n - f) * d + f);
}
"

    ''' <summary>The full-screen triangle of the app passes (attribute-less; same convention as the SAO's).</summary>
    Friend Const Vertex_Fullscreen As String = "#version 430
void main()
{
    vec2 p = vec2((gl_VertexID == 1) ? 3.0 : -1.0, (gl_VertexID == 2) ? 3.0 : -1.0);
    gl_Position = vec4(p, 1.0, 1.0);
}
"

    ''' <summary>POST OFF (direct frame, user rule: the material goes always): the composite's radiance through the 2.3.8 display tail
    ''' that Fragment_FO4 applied to lit surfaces (LegacyDisplaySource, one copy). Drawn at z = 1 with GREATER: only engine geometry.</summary>
    Friend Const Fragment_DirectResolve As String = "#version 430
layout(binding = 0) uniform sampler2D texScene;
uniform float uSceneToLinear;
out vec4 oColor;
" & LegacyDisplaySource.Tonemap_Glsl & "
void main()
{
    vec3 c = texelFetch(texScene, ivec2(gl_FragCoord.xy), 0).rgb;
    oColor = vec4(legacyLitDisplay(c, uSceneToLinear), 1.0);
}
"

    ''' <summary>The two full-screen steps of the translucent decals' base (user decision 5-oct-2026; Fo4DeferredTargets.BeginDecalBase /
    ''' EndDecalBase), on the auxiliary depth/stencil target. Mark (uOnlyEmpty): where the G-buffer depth copied before the translucent
    ''' decals holds the stage's clear value 1.0 (BeginGBuffer, 0x1421F3151..0x1421F3164: nothing behind), the constant depth uDepth
    ''' (0.0) - the stencil op marks the pixel; elsewhere discarded. Fill (not uOnlyEmpty): the constant depth uDepth (1.0) on every
    ''' pixel the stencil test lets through. Only the exact constants 0.0 and 1.0 are written (GL 4.6 2.3.5.2: both are integers once
    ''' scaled by 2^24 - 1); a decal's depth is never written by this program. texelFetch at gl_FragCoord: the copy has the
    ''' G-buffer's size and its D3D row order (a full-screen pass addresses storage texels whatever the clip origin).</summary>
    Friend Const Fragment_DecalBase As String = "#version 430
layout(binding = 0) uniform sampler2D tDepthBefore;
uniform bool uOnlyEmpty;
uniform float uDepth;
void main()
{
    if (uOnlyEmpty && texelFetch(tDepthBefore, ivec2(gl_FragCoord.xy), 0).x != 1.0)
        discard;
    gl_FragDepth = uDepth;
}
"
End Module

Public Class Fo4_Deferred_Shader_Class
    Inherits Shader_Base_Class
    Sub New(vertex As String, fragment As String)
        MyBase.New(vertex, fragment)
    End Sub
End Class

''' <summary>The programs of the FO4 deferred frame: lights, composite and the app passes built at start, and the G-buffer record
''' programs (the 470 of the source's closed list, Fo4GBufferSource.Records) built on their FIRST USE (user decision 5-oct-2026:
''' cold, all 470 cost 15 s; one 30 ms). Debug builds every record at start and throws on the first failure (user decision
''' 5-oct-2026). A record whose build fails is remembered with its error and its draw is reported (GBufferProgram; never a silent
''' "not drawn"). A start program that does not compile or link throws out of the constructor after releasing the ones already
''' built (rev-50: the control then runs without the FO4 deferred frame and says why).</summary>
Friend NotInheritable Class Fo4DeferredPrograms
    Friend ReadOnly Sun, SunShadow, Fill, Ambient, Composite1, Composite2, LinearZ, DirectResolve, DecalBase As Fo4_Deferred_Shader_Class
    ''' <summary>PS id -&gt; the record programs built so far (Vertex_FO4 + Fo4GBufferSource.FragmentOf), and the records whose build
    ''' failed with the error text.</summary>
    Private ReadOnly _gbuffer As New Dictionary(Of UInteger, Fo4_Deferred_Shader_Class)
    Private ReadOnly _gbufferErrors As New Dictionary(Of UInteger, String)
    ''' <summary>PS id -&gt; the base-pass program of a record with forceEarlyDepthStencil (DecalBaseProgram), built with the record.</summary>
    Private ReadOnly _gbufferBase As New Dictionary(Of UInteger, Fo4_Deferred_Shader_Class)
    ''' <summary>GateWith copies: the programs that own the records not overridden by the gate.</summary>
    Private ReadOnly _gbufferSource As Fo4DeferredPrograms
    Private ReadOnly _built As New List(Of Shader_Base_Class)

    Private Function Build(vertex As String, fragment As String) As Fo4_Deferred_Shader_Class
        Dim p As New Fo4_Deferred_Shader_Class(vertex, fragment)
        _built.Add(p)
        Return p
    End Function

    Friend Sub New()
        Try
            Sun = Build(Fo4DeferredSource.Vertex_Light, Fo4DeferredSource.Fragment_3321)
            SunShadow = Build(Fo4DeferredSource.Vertex_Light, Fo4DeferredSource.Fragment_3133)
            Fill = Build(Fo4DeferredSource.Vertex_Light, Fo4DeferredSource.Fragment_3323)
            Ambient = Build(Fo4DeferredSource.Vertex_Light, Fo4DeferredSource.Fragment_3107)
            Composite1 = Build(Fo4DeferredSource.Vertex_Composite, Fo4DeferredSource.Fragment_3432)
            Composite2 = Build(Fo4DeferredSource.Vertex_Composite, Fo4DeferredSource.Fragment_3495)
            LinearZ = Build(Fo4DeferredAppSource.Vertex_Fullscreen, Fo4DeferredAppSource.Fragment_LinearZ)
            DirectResolve = Build(Fo4DeferredAppSource.Vertex_Fullscreen, Fo4DeferredAppSource.Fragment_DirectResolve)
            DecalBase = Build(Fo4DeferredAppSource.Vertex_Fullscreen, Fo4DeferredAppSource.Fragment_DecalBase)
#If DEBUG Then
            ' Debug (user decision 5-oct-2026): every record of the closed list built at start; the first failure throws.
            For Each id In Fo4GBufferSource.Records.Keys
                Dim errorText As String = Nothing
                If GBufferProgram(id, errorText) Is Nothing Then Throw New Exception($"G-buffer record PS {id:X8}: {errorText}")
            Next
#End If
        Catch
            Dispose()
            Throw
        End Try
    End Sub

    ''' <summary>The program of G-buffer record <paramref name="psId"/> (a key of Fo4GBufferSource.Records), built on its first use;
    ''' Nothing when its build failed, with the error in <paramref name="errorText"/> (remembered: it is not rebuilt).</summary>
    Friend Function GBufferProgram(psId As UInteger, ByRef errorText As String) As Fo4_Deferred_Shader_Class
        errorText = Nothing
        Dim program As Fo4_Deferred_Shader_Class = Nothing
        If _gbuffer.TryGetValue(psId, program) Then Return program
        If _gbufferSource IsNot Nothing Then Return _gbufferSource.GBufferProgram(psId, errorText)
        If _gbufferErrors.TryGetValue(psId, errorText) Then Return Nothing
        Try
            program = Build(Shader_Class_Fo4.Vertex_FO4, Fo4GBufferSource.FragmentOf(psId))
            ' The base-pass variant of a record with forceEarlyDepthStencil, built with it: one build path, one error path (a failure
            ' makes the record a gap, reported as any build failure).
            If Fo4GBufferSource.Records(psId).EarlyZ Then
                _gbufferBase(psId) = Build(Shader_Class_Fo4.Vertex_FO4, Fo4GBufferSource.FragmentOf(psId, earlyTests:=False))
            End If
        Catch ex As Exception
            errorText = ex.Message
            _gbufferErrors(psId) = errorText
            Return Nothing
        End Try
        _gbuffer(psId) = program
        Return program
    End Function

    ''' <summary>The record programs built so far (ShadowGate's program census).</summary>
    Friend Function BuiltGBufferPrograms() As IReadOnlyDictionary(Of UInteger, Fo4_Deferred_Shader_Class)
        Return _gbuffer
    End Function

    ''' <summary>The base-pass variants built so far (ShadowGate's program census).</summary>
    Friend Function BuiltDecalBasePrograms() As IReadOnlyDictionary(Of UInteger, Fo4_Deferred_Shader_Class)
        Return _gbufferBase
    End Function

    ''' <summary>The program the translucent decals' base pass draws record <paramref name="psId"/> with (RenderableMesh.Render
    ''' decalBase): the record's own program - its kill decides which fragments give a base, as it decides which ones paint -
    ''' except for a record with forceEarlyDepthStencil (43 of the 143 records with technique bit 15), whose early depth write would
    ''' keep the depth of the pixels its PS kills (GL 4.3 14.9: with early tests the depth buffer is updated before the shader, a
    ''' later discard does not undo it): for those, the same record text without the early tests (Fo4GBufferSource.FragmentOf
    ''' earlyTests:=False), built with the record (GBufferProgram). Nothing when the record's build failed (it is then a gap).</summary>
    Friend Function DecalBaseProgram(psId As UInteger) As Fo4_Deferred_Shader_Class
        Dim errorText As String = Nothing
        Dim program = GBufferProgram(psId, errorText)
        If program Is Nothing OrElse Not Fo4GBufferSource.Records(psId).EarlyZ Then Return program
        If _gbufferSource IsNot Nothing Then Return _gbufferSource.DecalBaseProgram(psId)
        Return _gbufferBase(psId)
    End Function

    ''' <summary>GATE ONLY: a GateWith copy's own program for record <paramref name="psId"/> (the copy owns nothing: the caller
    ''' releases it).</summary>
    Friend Sub GateSetGBufferProgram(psId As UInteger, program As Fo4_Deferred_Shader_Class)
        If _gbufferSource Is Nothing Then Throw New InvalidOperationException("GateSetGBufferProgram: only on a GateWith copy")
        _gbuffer(psId) = program
    End Sub

    ''' <summary>GATE ONLY (ShadowGate --fo4-deferred-law mutants): a copy of these programs with the one of role
    ''' <paramref name="role"/> (Sun, SunShadow, Fill, Ambient, Composite1, Composite2, LinearZ, DirectResolve, DecalBase) replaced.
    ''' The copy owns nothing: its Dispose releases no program.</summary>
    Friend Function GateWith(role As String, program As Fo4_Deferred_Shader_Class) As Fo4DeferredPrograms
        Return New Fo4DeferredPrograms(Me, role, program)
    End Function

    Private Sub New(src As Fo4DeferredPrograms, role As String, program As Fo4_Deferred_Shader_Class)
        If Not {"Sun", "SunShadow", "Fill", "Ambient", "Composite1", "Composite2", "LinearZ", "DirectResolve", "DecalBase"}.Contains(role) Then
            Throw New ArgumentException("no deferred program role " & role)
        End If
        Sun = If(role = "Sun", program, src.Sun)
        SunShadow = If(role = "SunShadow", program, src.SunShadow)
        Fill = If(role = "Fill", program, src.Fill)
        Ambient = If(role = "Ambient", program, src.Ambient)
        Composite1 = If(role = "Composite1", program, src.Composite1)
        Composite2 = If(role = "Composite2", program, src.Composite2)
        LinearZ = If(role = "LinearZ", program, src.LinearZ)
        DirectResolve = If(role = "DirectResolve", program, src.DirectResolve)
        DecalBase = If(role = "DecalBase", program, src.DecalBase)
        _gbufferSource = src
    End Sub

    Friend Function All() As IEnumerable(Of Shader_Base_Class)
        Return _built
    End Function

    Friend Sub Dispose()
        For Each p In All() : p.Dispose() : Next
    End Sub
End Class

''' <summary>What the light and composite passes of one frame receive (built by PreviewControl from the frame's camera and rig).</summary>
Friend Structure Fo4DeferredFrame
    Public W As Integer, H As Integer
    Public Near As Single, Far As Single
    ''' <summary>cb12[20..23] / [24..27] rows (luces notas 4.2 / D9), cb12[12..14] rows (ENV 9.1).</summary>
    Public InvProj As Vector4(), InvProj1P As Vector4(), ViewToWorld As Vector4()
    ''' <summary>The sun (the rig's key): engine-view direction TO the light, linear colour.</summary>
    Public SunDir As Vector3, SunColor As Vector3, SunCastsShadow As Boolean
    ''' <summary>The three rig fills: engine-view direction, linear colour, shadow channel or -1.</summary>
    Public FillDir As Vector3(), FillColor As Vector3(), FillShadowChannel As Integer()
    ''' <summary>DirectionalAmbient[0..2] (propuesta D).</summary>
    Public Ambient As Vector4()
End Structure

''' <summary>THE TARGETS AND THE NON-G-BUFFER PASSES OF THE FO4 DEFERRED FRAME (Tools/re-docs/RE_FO4_DEFERRED_FRAME_2026-10-03.md 1, 2,
''' 3, 4, 15, 17): the G-buffer RTs 0x1A/0x1B/0x1D/0x1E/0x1F, the app's shadow RT, the D3D-order depth/stencil, the light RTs
''' 0x21/0x22, RT 2, RT 0x28 (t15). Everything up to composite 1 is in D3D row order (glClipControl UPPER_LEFT, propuesta A).</summary>
Friend NotInheritable Class Fo4DeferredTargets
    Private _w As Integer, _h As Integer
    Private _albedo, _normal, _env, _gloss, _emit, _shadow, _depth, _depthCopy, _lightD, _lightS, _rt2, _linZ, _decalBase As Integer
    Private _fboG, _fboL, _fboC1, _fboZ, _fboBase As Integer
    Private _quadVao, _quadVbo, _triVao, _triVbo, _emptyVao As Integer
    Private _point, _cubeSampler As Integer
    Private Shared ReadOnly GBufferBuffers As DrawBuffersEnum() = {DrawBuffersEnum.ColorAttachment0, DrawBuffersEnum.ColorAttachment1,
        DrawBuffersEnum.ColorAttachment2, DrawBuffersEnum.ColorAttachment3, DrawBuffersEnum.ColorAttachment4, DrawBuffersEnum.ColorAttachment5}
    Private Shared ReadOnly LightBuffers As DrawBuffersEnum() = {DrawBuffersEnum.ColorAttachment0, DrawBuffersEnum.ColorAttachment1}

    ''' <summary>GATE ONLY (no UI): ShadowGate --fo4-deferred-scene mutant. True leaves the G-buffer stage in LOWER_LEFT.</summary>
    Friend Shared GateNoUpperLeft As Boolean = False
    ''' <summary>GATE ONLY (no UI): ShadowGate --fo4-deferred-scene mutant. True draws the G-buffer without FRAMEBUFFER_SRGB.</summary>
    Friend Shared GateNoGBufferSrgb As Boolean = False
    ''' <summary>GATE ONLY (no UI): ShadowGate --fo4-deferred-scene mutant. True blits the G-buffer depth without the y mirror.</summary>
    Friend Shared GateBlitNoMirror As Boolean = False
    ''' <summary>GATE ONLY (no UI): ShadowGate --fo4-decal-base mutant. True keeps the NEAREST translucent decal as the base (the
    ''' mark writes 1.0, the base draws LESS - RenderableMesh.ApplyDecalBaseState) instead of the farthest.</summary>
    Friend Shared GateDecalBaseNearest As Boolean = False

    ''' <summary>GATE ONLY: the textures of the deferred targets, by role (ShadowGate --fo4-deferred-law / --fo4-deferred-scene).</summary>
    Friend ReadOnly Property GateTextures As (Albedo As Integer, Normal As Integer, Env As Integer, Gloss As Integer, Emit As Integer,
                                              Shadow As Integer, Depth As Integer, LightD As Integer, LightS As Integer, Rt2 As Integer,
                                              LinZ As Integer, DecalBase As Integer)
        Get
            Return (_albedo, _normal, _env, _gloss, _emit, _shadow, _depth, _lightD, _lightS, _rt2, _linZ, _decalBase)
        End Get
    End Property

    Public ReadOnly Property GBufferFramebuffer As Integer
        Get
            Return _fboG
        End Get
    End Property

    Private Shared Function Tex(fmt As SizedInternalFormat, w As Integer, h As Integer) As Integer
        Dim t As Integer
        GL.CreateTextures(TextureTarget.Texture2D, 1, t)
        GL.TextureStorage2D(t, 1, fmt, w, h)
        Return t
    End Function

    ''' <summary>Allocates for w x h; False when a framebuffer is incomplete (user decision rev-38 a: no frame, status card).</summary>
    Public Function Ensure(w As Integer, h As Integer) As Boolean
        If w <= 0 OrElse h <= 0 Then Return False
        If _fboG <> 0 AndAlso w = _w AndAlso h = _h Then Return True
        Free()
        _albedo = Tex(SizedInternalFormat.Srgb8Alpha8, w, h)        ' RT 0x1A R8G8B8A8_UNORM_SRGB
        _normal = Tex(SizedInternalFormat.Rg16, w, h)               ' RT 0x1B R16G16_UNORM
        _env = Tex(SizedInternalFormat.Rgba8, w, h)                 ' RT 0x1D R8G8B8A8_UNORM
        _gloss = Tex(SizedInternalFormat.Rgba8, w, h)               ' RT 0x1E R8G8B8A8_UNORM
        _emit = Tex(SizedInternalFormat.Srgb8Alpha8, w, h)          ' RT 0x1F R8G8B8A8_UNORM_SRGB (RGBEMIT = 1, rev-24)
        _shadow = Tex(SizedInternalFormat.Rgba8, w, h)              ' app (propuesta B)
        _depth = Tex(SizedInternalFormat.Depth24Stencil8, w, h)     ' DS 1 D24_UNORM_S8_UINT
        _depthCopy = Tex(SizedInternalFormat.Depth24Stencil8, w, h)
        GL.TextureParameter(_depthCopy, TextureParameterName.DepthStencilTextureMode, CInt(All.DepthComponent))
        _lightD = Tex(SizedInternalFormat.R11fG11fB10f, w, h)       ' RT 0x21
        _lightS = Tex(SizedInternalFormat.R11fG11fB10f, w, h)       ' RT 0x22 (RGBSPEC = 1, rev-24)
        _rt2 = Tex(SizedInternalFormat.R11fG11fB10f, w, h)          ' RT 2
        _linZ = Tex(SizedInternalFormat.R32f, w, h)                 ' RT 0x28 mip 0
        ' App: the auxiliary depth/stencil of the translucent decals' base (BeginDecalBase / EndDecalBase), DS 1's format: the copy
        ' in (CopyImageSubData, same format) and the depth blit back (GL 4.6 18.3.1: formats must match) need it; its stencil is its own.
        _decalBase = Tex(SizedInternalFormat.Depth24Stencil8, w, h)
        GL.TextureParameter(_decalBase, TextureParameterName.DepthStencilTextureMode, CInt(All.DepthComponent))
        Dim ok = True
        GL.CreateFramebuffers(1, _fboG)
        For i = 0 To 5
            GL.NamedFramebufferTexture(_fboG, CType(FramebufferAttachment.ColorAttachment0 + i, FramebufferAttachment),
                                       {_albedo, _normal, _env, _gloss, _emit, _shadow}(i), 0)
        Next
        GL.NamedFramebufferTexture(_fboG, FramebufferAttachment.DepthStencilAttachment, _depth, 0)
        ok = ok AndAlso GL.CheckNamedFramebufferStatus(_fboG, FramebufferTarget.Framebuffer) = FramebufferStatus.FramebufferComplete
        GL.CreateFramebuffers(1, _fboL)
        GL.NamedFramebufferTexture(_fboL, FramebufferAttachment.ColorAttachment0, _lightD, 0)
        GL.NamedFramebufferTexture(_fboL, FramebufferAttachment.ColorAttachment1, _lightS, 0)
        GL.NamedFramebufferTexture(_fboL, FramebufferAttachment.DepthStencilAttachment, _depth, 0)
        ok = ok AndAlso GL.CheckNamedFramebufferStatus(_fboL, FramebufferTarget.Framebuffer) = FramebufferStatus.FramebufferComplete
        GL.CreateFramebuffers(1, _fboC1)
        GL.NamedFramebufferTexture(_fboC1, FramebufferAttachment.ColorAttachment0, _rt2, 0)
        GL.NamedFramebufferTexture(_fboC1, FramebufferAttachment.DepthStencilAttachment, _depth, 0)
        ok = ok AndAlso GL.CheckNamedFramebufferStatus(_fboC1, FramebufferTarget.Framebuffer) = FramebufferStatus.FramebufferComplete
        GL.CreateFramebuffers(1, _fboZ)
        GL.NamedFramebufferTexture(_fboZ, FramebufferAttachment.ColorAttachment0, _linZ, 0)
        ok = ok AndAlso GL.CheckNamedFramebufferStatus(_fboZ, FramebufferTarget.Framebuffer) = FramebufferStatus.FramebufferComplete
        GL.CreateFramebuffers(1, _fboBase)
        GL.NamedFramebufferTexture(_fboBase, FramebufferAttachment.DepthStencilAttachment, _decalBase, 0)
        GL.NamedFramebufferDrawBuffer(_fboBase, DrawBufferMode.None)
        GL.NamedFramebufferReadBuffer(_fboBase, ReadBufferMode.None)
        ok = ok AndAlso GL.CheckNamedFramebufferStatus(_fboBase, FramebufferTarget.Framebuffer) = FramebufferStatus.FramebufferComplete
        ' The light quad (-1,1,0) (-1,-1,0) (1,-1,0) (1,1,0) (0x142246595..0x142246669) and the composite triangle (-1,1,1) (-1,-3,1)
        ' (3,1,1) (DF 4.4, 0x142216600).
        _quadVao = GL.GenVertexArray() : _quadVbo = GL.GenBuffer()
        GL.BindVertexArray(_quadVao)
        GL.BindBuffer(BufferTarget.ArrayBuffer, _quadVbo)
        Dim quad = {-1.0F, 1.0F, 0.0F, -1.0F, -1.0F, 0.0F, 1.0F, 1.0F, 0.0F, 1.0F, -1.0F, 0.0F}   ' triangle strip order
        GL.BufferData(BufferTarget.ArrayBuffer, quad.Length * 4, quad, BufferUsageHint.StaticDraw)
        GL.EnableVertexAttribArray(0) : GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, False, 0, 0)
        _triVao = GL.GenVertexArray() : _triVbo = GL.GenBuffer()
        GL.BindVertexArray(_triVao)
        GL.BindBuffer(BufferTarget.ArrayBuffer, _triVbo)
        Dim tri = {-1.0F, 1.0F, 1.0F, -1.0F, -3.0F, 1.0F, 3.0F, 1.0F, 1.0F}
        GL.BufferData(BufferTarget.ArrayBuffer, tri.Length * 4, tri, BufferUsageHint.StaticDraw)
        GL.EnableVertexAttribArray(0) : GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, False, 0, 0)
        GL.BindVertexArray(0)
        _emptyVao = GL.GenVertexArray()
        ' Samplers: FILT 0 = POINT single level (lights 0x142248F07..FF2, composite DF 4.2); t8 cube array CLAMP + ANISOTROPIC
        ' (ENV 3.1): LINEAR_MIPMAP_LINEAR, anisotropy clamp(iMaxAnisotropy, 1, 16) = 16, seamless.
        _point = GL.GenSampler()
        For Each p In {SamplerParameterName.TextureWrapS, SamplerParameterName.TextureWrapT}
            GL.SamplerParameter(_point, p, CInt(TextureWrapMode.ClampToEdge))
        Next
        GL.SamplerParameter(_point, SamplerParameterName.TextureMinFilter, CInt(TextureMinFilter.Nearest))
        GL.SamplerParameter(_point, SamplerParameterName.TextureMagFilter, CInt(TextureMagFilter.Nearest))
        _cubeSampler = GL.GenSampler()
        For Each p In {SamplerParameterName.TextureWrapS, SamplerParameterName.TextureWrapT, SamplerParameterName.TextureWrapR}
            GL.SamplerParameter(_cubeSampler, p, CInt(TextureWrapMode.ClampToEdge))
        Next
        GL.SamplerParameter(_cubeSampler, SamplerParameterName.TextureMinFilter, CInt(TextureMinFilter.LinearMipmapLinear))
        GL.SamplerParameter(_cubeSampler, SamplerParameterName.TextureMagFilter, CInt(TextureMagFilter.Linear))
        GL.SamplerParameter(_cubeSampler, CType(&H84FE, SamplerParameterName), 16.0F)   ' GL_TEXTURE_MAX_ANISOTROPY
        If Not ok Then Free() : Return False
        _w = w : _h = h
        Return True
    End Function

    ''' <summary>Opens the G-buffer stage: D3D row order (UPPER_LEFT; glFrontFace unchanged, propuesta A.2), every RT cleared (rev-29 b:
    ''' 0, the shadow RT 1), depth 1 / stencil 0 (0x1421F3151..0x1421F3164), sRGB writes on (rev-17).</summary>
    Public Sub BeginGBuffer()
        GL.ClipControl(ClipOrigin.UpperLeft, ClipDepthMode.NegativeOneToOne)
        If GateNoUpperLeft Then GL.ClipControl(ClipOrigin.LowerLeft, ClipDepthMode.NegativeOneToOne)
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _fboG)
        GL.Viewport(0, 0, _w, _h)
        GL.ColorMask(True, True, True, True)
        GL.DepthMask(True)
        GL.NamedFramebufferDrawBuffers(_fboG, 6, GBufferBuffers)
        For i = 0 To 4
            GL.ClearNamedFramebuffer(_fboG, ClearBuffer.Color, i, New Single() {0.0F, 0.0F, 0.0F, 0.0F})
        Next
        GL.ClearNamedFramebuffer(_fboG, ClearBuffer.Color, 5, New Single() {1.0F, 1.0F, 1.0F, 1.0F})
        GL.ClearNamedFramebuffer(_fboG, ClearBufferCombined.DepthStencil, 0, 1.0F, 0)
        GL.Enable(EnableCap.FramebufferSrgb)
        If GateNoGBufferSrgb Then GL.Disable(EnableCap.FramebufferSrgb)
        GL.Disable(IndexedEnableCap.Blend, 5)
    End Sub

    ''' <summary>Closes the G-buffer stage: sRGB writes off, the depth copy t3/t7 (feedback loop, propuesta A.3), t15.</summary>
    Public Sub EndGBuffer(progs As Fo4DeferredPrograms, near As Single, far As Single)
        GL.Disable(EnableCap.FramebufferSrgb)
        CopyDepth()
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _fboZ)
        GL.Viewport(0, 0, _w, _h)
        progs.LinearZ.Use()
        progs.LinearZ.SetVector2("uNearFar", New Vector2(near, far))
        GL.BindTextureUnit(7, _depthCopy) : GL.BindSampler(7, _point)
        FullscreenPass.Draw(progs.LinearZ, _emptyVao, 8)
        GL.BindSampler(7, 0)
    End Sub

    ''' <summary>The G-buffer depth into the copy the passes that read it sample (the feedback loop, propuesta A.3): EndGBuffer's
    ''' copy for the lights and composites, and BeginDecalBase's of the depth before the translucent decals.</summary>
    Private Sub CopyDepth()
        CopyDepthInto(_depthCopy)
    End Sub

    ''' <summary>The G-buffer depth/stencil texels into <paramref name="dst"/> (D24S8, the G-buffer's size): CopyImageSubData, raw texels
    ''' (GL 4.6 18.3.2: "does not perform general-purpose conversions such as scaling, resizing, blending, color-space, or format
    ''' conversions. It should be considered to operate in a manner similar to a CPU memcpy").</summary>
    Private Sub CopyDepthInto(dst As Integer)
        GL.CopyImageSubData(_depth, ImageTarget.Texture2D, 0, 0, 0, 0, dst, ImageTarget.Texture2D, 0, 0, 0, 0, _w, _h, 1)
    End Sub

    ''' <summary>Opens the base pass of the translucent decals (user decision 5-oct-2026; an app pass, Fallout 4 has none: its group-3
    ''' decals test LESS_EQUAL without writing, Fo4RenderPassLaw, so over the clear depth they fail and the lights and composites skip
    ''' the pixel, LightState / RunComposite1 / RunComposite2). No depth of a decal goes through a float (user order):
    ''' <list type="number">
    ''' <item>CopyDepth (the mark reads it) and CopyDepthInto the auxiliary target (CopyImageSubData, GL 4.6 18.3.2: raw texels, "a
    ''' CPU memcpy"); then the auxiliary stencil - the copied one - cleared to 0 (GL 4.6 17.4.3).</item>
    ''' <item>Mark: Fragment_DecalBase with uOnlyEmpty and uDepth 0.0, depth ALWAYS + write (GL 4.6 17.3.4), stencil ALWAYS ref 1,
    ''' REPLACE on depth pass (GL 4.6 17.3.3): where nothing is behind, depth 0.0 (exact, GL 4.6 2.3.5.2) and stencil 1.</item>
    ''' </list>
    ''' Leaves the base state for the caller's draws (RenderableMesh.Render decalBase: own program, colour masked, GREATER + write):
    ''' stencil test "1 &lt;= stencil" (LEQUAL ref 1, GL 4.6 17.3.3: passes on the mark and on every value a decal left there,
    ''' fails where nothing was marked), INCR on depth pass. The decal depth is written by rasterisation: the same value its colour
    ''' draw rasterises (GL 4.6 Appendix A, rule 2: the depth / stencil / colour-mask state does not change the fragment's z).</summary>
    Public Sub BeginDecalBase(progs As Fo4DeferredPrograms)
        CopyDepth()
        GL.DepthMask(True)
        GL.StencilMask(&HFF)
        CopyDepthInto(_decalBase)
        GL.ClearNamedFramebuffer(_fboBase, ClearBuffer.Stencil, 0, New Integer() {0})
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _fboBase)
        GL.Viewport(0, 0, _w, _h)
        GL.Enable(EnableCap.StencilTest)
        GL.StencilFunc(StencilFunction.Always, 1, &HFF)
        GL.StencilOp(StencilOp.Keep, StencilOp.Keep, StencilOp.Replace)
        progs.DecalBase.Use()
        progs.DecalBase.SetBool("uOnlyEmpty", True)
        progs.DecalBase.SetFloat("uDepth", If(GateDecalBaseNearest, 1.0F, 0.0F))
        GL.BindTextureUnit(0, _depthCopy) : GL.BindSampler(0, _point)
        FullscreenPass.Draw(progs.DecalBase, _emptyVao, 1, writesDepth:=True)
        GL.BindSampler(0, 0)
        GL.StencilFunc(StencilFunction.Lequal, 1, &HFF)
        GL.StencilOp(StencilOp.Keep, StencilOp.Keep, StencilOp.Incr)
    End Sub

    ''' <summary>Closes the base pass:
    ''' <list type="number">
    ''' <item>Fill: Fragment_DecalBase without uOnlyEmpty, uDepth 1.0, stencil EQUAL 1 (marked and covered by no decal), depth ALWAYS +
    ''' write: those pixels get the clear value 1.0 back (exact, GL 4.6 2.3.5.2).</item>
    ''' <item>The auxiliary depth blitted back into the G-buffer depth, DEPTH only, NEAREST, same rectangle, same format (GL 4.6 18.3.1;
    ''' "Color, depth, and stencil masks ... are ignored"): where something was behind it is the G-buffer's own value, copied and
    ''' blitted back; the G-buffer stencil is not in the mask, so it is not overwritten.</item>
    ''' </list>
    ''' Leaves the G-buffer bound, LESS_EQUAL, stencil test off, for the translucent decals, which then draw unchanged.</summary>
    Public Sub EndDecalBase(progs As Fo4DeferredPrograms)
        GL.StencilFunc(StencilFunction.Equal, 1, &HFF)
        GL.StencilOp(StencilOp.Keep, StencilOp.Keep, StencilOp.Keep)
        progs.DecalBase.Use()
        progs.DecalBase.SetBool("uOnlyEmpty", False)
        progs.DecalBase.SetFloat("uDepth", 1.0F)
        FullscreenPass.Draw(progs.DecalBase, _emptyVao, 0, writesDepth:=True)
        GL.Disable(EnableCap.StencilTest)
        GL.DepthMask(True)
        GL.BlitNamedFramebuffer(_fboBase, _fboG, 0, 0, _w, _h, 0, 0, _w, _h, ClearBufferMask.DepthBufferBit, BlitFramebufferFilter.Nearest)
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _fboG)
        GL.Viewport(0, 0, _w, _h)
        GL.DepthFunc(DepthFunction.Lequal)
    End Sub

    Private Sub BindGBufferInputs()
        ' Lights: t0 = RT 0x1A, t1 = RT 0x1B, t2 = RT 0x1E, t3 = depth copy (0x142248EAE..F02).
        GL.BindTextureUnit(0, _albedo) : GL.BindTextureUnit(1, _normal) : GL.BindTextureUnit(2, _gloss) : GL.BindTextureUnit(3, _depthCopy)
        GL.BindTextureUnit(5, _shadow)
        For u = 0 To 5 : GL.BindSampler(u, _point) : Next
    End Sub

    Private Sub LightState()
        ' Full-screen lights: GEQUAL, no write, blend ONE/ONE, RGB, no cull (luces notas D3).
        GL.Enable(EnableCap.DepthTest) : GL.DepthFunc(DepthFunction.Gequal) : GL.DepthMask(False)
        GL.Enable(EnableCap.Blend) : GL.BlendFunc(BlendingFactor.One, BlendingFactor.One)
        GL.ColorMask(True, True, True, False)
        GL.Disable(EnableCap.CullFace)
    End Sub

    Private Shared Sub SetCb12(p As Shader_Base_Class, f As Fo4DeferredFrame)
        For i = 0 To 3
            p.SetVector4($"cb12_InvProj[{i}]", f.InvProj(i))
            p.SetVector4($"cb12_InvProj1P[{i}]", f.InvProj1P(i))
        Next
        For i = 0 To 2 : p.SetVector4($"cb12_ViewToWorld[{i}]", f.ViewToWorld(i)) : Next
        p.SetVector4("cb12_HairSpec[0]", New Vector4(0.02F, 125.0F, 1.2F, 160.0F))   ' ENV 16.3
        p.SetVector4("cb12_HairSpec[1]", New Vector4(0.36F, -0.4F, 0.0F, 0.0F))
        p.SetVector4("cb12_30", Vector4.Zero)                                         ' no wetness / rain
    End Sub

    ''' <summary>zq = min(fl((f*f - f*n) / ((f - n) * f)), 0x3F7FFFFE) in float32 and in that order (0x142247304..0x142247362).</summary>
    Private Shared Function Zq(n As Single, f As Single) As Single
        Dim a As Single = f * f - f * n
        Dim b As Single = (f - n) * f
        Return Math.Min(a / b, BitConverter.UInt32BitsToSingle(&H3F7FFFFEUI))
    End Function

    Private Sub DrawQuad(p As Shader_Base_Class, f As Fo4DeferredFrame)
        Dim z = Zq(f.Near, f.Far)
        p.SetVector4("WorldViewProj[0]", New Vector4(1, 0, 0, 0))
        p.SetVector4("WorldViewProj[1]", New Vector4(0, 1, 0, 0))
        p.SetVector4("WorldViewProj[2]", New Vector4(0, 0, 1, z))
        p.SetVector4("WorldViewProj[3]", New Vector4(0, 0, 0, 1))
        p.SetVector4("VPOSOffset", New Vector4(1.0F / f.W, 1.0F / f.H, 1.0F / f.W, 1.0F / f.H))   ' 0x142246D35..0x142246E01
        SetCb12(p, f)
        GL.BindVertexArray(_quadVao)
        GL.DrawArrays(PrimitiveType.TriangleStrip, 0, 4)
        GL.BindVertexArray(0)
    End Sub

    ''' <summary>The light stage (DF 3, 15.5): RT 0x21/0x22 cleared to 0, then sun, ambient and the three rig fills, additive.</summary>
    Public Sub RunLights(progs As Fo4DeferredPrograms, f As Fo4DeferredFrame)
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _fboL)
        GL.NamedFramebufferDrawBuffers(_fboL, 2, LightBuffers)
        GL.ColorMask(True, True, True, True)
        GL.ClearNamedFramebuffer(_fboL, ClearBuffer.Color, 0, New Single() {0, 0, 0, 0})
        GL.ClearNamedFramebuffer(_fboL, ClearBuffer.Color, 1, New Single() {0, 0, 0, 0})
        LightState()
        BindGBufferInputs()
        Dim sun As Shader_Base_Class = If(f.SunCastsShadow, CType(progs.SunShadow, Shader_Base_Class), progs.Sun)
        sun.Use()
        sun.SetVector4("LightVector", New Vector4(f.SunDir, 0.0F))
        sun.SetVector4("LightColor", New Vector4(f.SunColor, 0.0F))
        If f.SunCastsShadow Then
            sun.SetVector4("SplitDistances", New Vector4(0, 0, 0, Single.PositiveInfinity))     ' neutral (NT4, propuesta B)
            sun.SetVector4("ShadowFadeParam", New Vector4(Single.PositiveInfinity, 0, 0, 0))
        End If
        DrawQuad(sun, f)
        progs.Ambient.Use()
        For c = 0 To 2 : progs.Ambient.SetVector4($"DirectionalAmbient[{c}]", f.Ambient(c)) : Next
        DrawQuad(progs.Ambient, f)
        For k = 0 To 2
            progs.Fill.Use()
            progs.Fill.SetVector4("LightVector", New Vector4(f.FillDir(k), 1.0F))
            progs.Fill.SetVector4("LightColor", New Vector4(f.FillColor(k), 0.0F))
            progs.Fill.SetVector4("LightAttenuation", New Vector4(0.0F, 0.0F, 1.0F, 0.0F))
            progs.Fill.SetInt("uFillShadowChannel", If(f.FillShadowChannel(k) >= 0, f.FillShadowChannel(k), 0))
            DrawQuad(progs.Fill, f)
        Next
        For u = 0 To 5 : GL.BindTextureUnit(u, 0) : GL.BindSampler(u, 0) : Next
        GL.UseProgram(0)
    End Sub

    ''' <summary>Composite pass 1 (3432) -&gt; RT 2: triangle at z = 1, GREATER (DF 4.4).</summary>
    Public Sub RunComposite1(progs As Fo4DeferredPrograms, f As Fo4DeferredFrame)
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _fboC1)
        GL.NamedFramebufferDrawBuffer(_fboC1, DrawBufferMode.ColorAttachment0)
        GL.Disable(EnableCap.Blend)
        GL.Enable(EnableCap.DepthTest) : GL.DepthFunc(DepthFunction.Greater) : GL.DepthMask(False)
        GL.ColorMask(True, True, True, False)
        GL.Disable(EnableCap.CullFace)
        progs.Composite1.Use()
        progs.Composite1.SetVector4("VPOSOffset", New Vector4(1.0F / f.W, 1.0F / f.H, 1.0F, 1.0F))   ' 0x142217560..0x1422175D0
        GL.BindTextureUnit(0, _albedo) : GL.BindTextureUnit(3, _gloss) : GL.BindTextureUnit(4, _emit)
        GL.BindTextureUnit(5, _lightD) : GL.BindTextureUnit(6, _lightS)
        For Each u In {0, 3, 4, 5, 6} : GL.BindSampler(u, _point) : Next
        GL.BindVertexArray(_triVao) : GL.DrawArrays(PrimitiveType.Triangles, 0, 3) : GL.BindVertexArray(0)
        For Each u In {0, 3, 4, 5, 6} : GL.BindTextureUnit(u, 0) : GL.BindSampler(u, 0) : Next
        GL.UseProgram(0)
        GL.ClipControl(ClipOrigin.LowerLeft, ClipDepthMode.NegativeOneToOne)   ' end of the D3D-order stage (propuesta A)
    End Sub

    ''' <summary>The G-buffer depth/stencil into the GL-order target's (mirrored in y, NEAREST: an exact copy, propuesta A.4).</summary>
    Public Sub BlitDepthTo(targetFbo As Integer)
        If GateBlitNoMirror Then GL.BlitNamedFramebuffer(_fboG, targetFbo, 0, 0, _w, _h, 0, 0, _w, _h, ClearBufferMask.DepthBufferBit Or ClearBufferMask.StencilBufferBit, BlitFramebufferFilter.Nearest) : Return
        GL.BlitNamedFramebuffer(_fboG, targetFbo, 0, 0, _w, _h, 0, _h, _w, 0,
                                ClearBufferMask.DepthBufferBit Or ClearBufferMask.StencilBufferBit, BlitFramebufferFilter.Nearest)
    End Sub

    ''' <summary>Composite pass 2 (3495) into the scene target (LOWER_LEFT, GREATER at z = 1 against the mirrored depth; draw buffers
    ''' {A0, A1} = radiance and coverage, rev-19).</summary>
    Public Sub RunComposite2(progs As Fo4DeferredPrograms, f As Fo4DeferredFrame, sceneFbo As Integer, envArray As Integer)
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, sceneFbo)
        GL.NamedFramebufferDrawBuffers(sceneFbo, 2, LightBuffers)
        GL.Viewport(0, 0, _w, _h)
        GL.Disable(EnableCap.Blend)
        GL.Enable(EnableCap.DepthTest) : GL.DepthFunc(DepthFunction.Greater) : GL.DepthMask(False)
        GL.ColorMask(True, True, True, True)
        GL.Disable(EnableCap.CullFace)
        GL.Enable(EnableCap.TextureCubeMapSeamless)
        Dim p = progs.Composite2
        p.Use()
        p.SetVector4("VPOSOffset", New Vector4(1.0F / f.W, 1.0F / f.H, 1.0F, 1.0F))
        p.SetFloat("uViewportH", f.H)
        p.SetVector4("SSSSSParams", New Vector4(4.0F, 2.5F, 9.0F, 0.0F))   ' DF 17.2
        p.SetVector4("SSLRParams0", New Vector4(1.0F, 0, 0, 0))
        p.SetVector4("SSLRParams1", New Vector4(0, 0, 1.0F, 0))
        SetCb12(p, f)
        GL.BindTextureUnit(1, _normal) : GL.BindTextureUnit(2, _env) : GL.BindTextureUnit(3, _gloss) : GL.BindTextureUnit(4, _emit)
        GL.BindTextureUnit(5, _lightD) : GL.BindTextureUnit(6, _lightS) : GL.BindTextureUnit(7, _depthCopy)
        GL.BindTextureUnit(8, envArray) : GL.BindTextureUnit(10, _rt2) : GL.BindTextureUnit(15, _linZ)
        For Each u In {1, 2, 3, 4, 5, 6, 7, 10, 15} : GL.BindSampler(u, _point) : Next
        GL.BindSampler(8, _cubeSampler)
        GL.BindVertexArray(_triVao) : GL.DrawArrays(PrimitiveType.Triangles, 0, 3) : GL.BindVertexArray(0)
        For Each u In {1, 2, 3, 4, 5, 6, 7, 8, 10, 15} : GL.BindTextureUnit(u, 0) : GL.BindSampler(u, 0) : Next
        GL.UseProgram(0)
        GL.DepthMask(True) : GL.DepthFunc(DepthFunction.Lequal) : GL.Enable(EnableCap.CullFace)
    End Sub

    ''' <summary>Post off: the composited radiance onto the display target through the 2.3.8 display tail (propuesta C).</summary>
    Public Sub RunDirectResolve(progs As Fo4DeferredPrograms, sceneTex As Integer, displayFbo As Integer, sceneToLinear As Single)
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, displayFbo)
        GL.Viewport(0, 0, _w, _h)
        GL.Enable(EnableCap.DepthTest) : GL.DepthFunc(DepthFunction.Greater) : GL.DepthMask(False)
        GL.Disable(EnableCap.Blend) : GL.Disable(EnableCap.CullFace)
        progs.DirectResolve.Use()
        progs.DirectResolve.SetFloat("uSceneToLinear", sceneToLinear)
        GL.BindTextureUnit(0, sceneTex)
        GL.BindVertexArray(_emptyVao) : GL.DrawArrays(PrimitiveType.Triangles, 0, 3) : GL.BindVertexArray(0)
        GL.BindTextureUnit(0, 0) : GL.UseProgram(0)
        GL.DepthMask(True) : GL.DepthFunc(DepthFunction.Lequal) : GL.Enable(EnableCap.CullFace)
    End Sub

    Public Sub Free()
        For Each fb In {_fboG, _fboL, _fboC1, _fboZ, _fboBase}
            If fb <> 0 Then GL.DeleteFramebuffer(fb)
        Next
        _fboG = 0 : _fboL = 0 : _fboC1 = 0 : _fboZ = 0 : _fboBase = 0
        For Each t In {_albedo, _normal, _env, _gloss, _emit, _shadow, _depth, _depthCopy, _lightD, _lightS, _rt2, _linZ, _decalBase}
            If t <> 0 Then GL.DeleteTexture(t)
        Next
        _albedo = 0 : _normal = 0 : _env = 0 : _gloss = 0 : _emit = 0 : _shadow = 0 : _depth = 0 : _depthCopy = 0
        _lightD = 0 : _lightS = 0 : _rt2 = 0 : _linZ = 0 : _decalBase = 0
        For Each v In {_quadVao, _triVao, _emptyVao}
            If v <> 0 Then GL.DeleteVertexArray(v)
        Next
        For Each b In {_quadVbo, _triVbo}
            If b <> 0 Then GL.DeleteBuffer(b)
        Next
        _quadVao = 0 : _triVao = 0 : _emptyVao = 0 : _quadVbo = 0 : _triVbo = 0
        For Each s In {_point, _cubeSampler}
            If s <> 0 Then GL.DeleteSampler(s)
        Next
        _point = 0 : _cubeSampler = 0
        _w = 0 : _h = 0
    End Sub
End Class
