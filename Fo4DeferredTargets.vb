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
    ''' that Fragment_FO4 applied to lit surfaces (LegacyDisplaySource, one copy). Drawn at z = 1 with GREATER: only engine geometry.
    ''' With the translucent decals' base in the frame (uCoverageBlend; user decision 7-oct-2026, DecalBaseTarget): the radiance is
    ''' premultiplied by the composite's coverage (rev-19, as the post reads it, Fragment_PostFo4); un-premultiplied, through the
    ''' tail, and mixed over the background already in the display target with that coverage (blend colour SRC_ALPHA /
    ''' ONE_MINUS_SRC_ALPHA, alpha ZERO / ONE: the display's alpha stays the frame's, RunDirectResolve): the post's mix(bg, o, cov).
    ''' Coverage 0: the background stays. Without the base: coverage 1, no blend.</summary>
    Friend Const Fragment_DirectResolve As String = "#version 430
layout(binding = 0) uniform sampler2D texScene;
layout(binding = 1) uniform sampler2D texCoverage;
uniform float uSceneToLinear;
uniform bool uCoverageBlend;
out vec4 oColor;
" & LegacyDisplaySource.Tonemap_Glsl & "
void main()
{
    vec3 c = texelFetch(texScene, ivec2(gl_FragCoord.xy), 0).rgb;
    float cov = uCoverageBlend ? texelFetch(texCoverage, ivec2(gl_FragCoord.xy), 0).r : 1.0;
    if (cov <= 0.0)
        discard;
    oColor = vec4(legacyLitDisplay(c / cov, uSceneToLinear), cov);
}
"

    ''' <summary>The two full-screen steps of the translucent decals' base, both games (user decisions 5-oct-2026 and 7-oct-2026;
    ''' DecalBaseTarget.Begin / Fill), on the auxiliary depth/stencil target. Mark (uOnlyEmpty): where the frame depth copied before the
    ''' translucent decals holds its clear value 1.0 (FO4 BeginGBuffer, 0x1421F3151..0x1421F3164; SSE SceneTargets.BeginHdr / the
    ''' display clear: nothing behind), the constant depth uDepth
    ''' (0.0) - the stencil op marks the pixel; elsewhere discarded. Fill (not uOnlyEmpty): the constant depth uDepth (1.0) on every
    ''' pixel the stencil test lets through. Only the exact constants 0.0 and 1.0 are written (GL 4.6 2.3.5.2: both are integers once
    ''' scaled by 2^24 - 1); a decal's depth is never written by this program. texelFetch at gl_FragCoord: the copy has the
    ''' G-buffer's size and its D3D row order (a full-screen pass addresses storage texels whatever the clip origin).</summary>
    Friend Const Fragment_DecalBase As String = "#version 430
layout(binding = 0) uniform sampler2D tDepthBefore;
uniform bool uOnlyEmpty;
uniform float uDepth;
layout(location = 0) out vec4 oCoverage;   // DecalBaseTarget's coverage target (Fallout 4, mark only; masked otherwise)
void main()
{
    if (uOnlyEmpty && texelFetch(tDepthBefore, ivec2(gl_FragCoord.xy), 0).x != 1.0)
        discard;
    gl_FragDepth = uDepth;
    oCoverage = vec4(0.0);
}
"

    ''' <summary>The base SURFACE of a translucent decal in the G-buffer (user decisions 7-oct-2026; DecalBaseTarget): drawn with the
    ''' decal's geometry on the pixels whose base it gives (after every depth draw: depth EQUAL, stencil CoveredBit and not
    ''' SurfacedBit), it writes what the G-buffer of a plain surface holds there as a PREMULTIPLIED layer of the decal's opacity a
    ''' (decision 3: the base follows the decal's alpha), so that the deferred lights and composites shade it like any other surface
    ''' (RE_FO4_DEFERRED_FRAME 6, 15.1) and composite 2 hands the post the coverage of base and decals (DecalBaseTarget.CoverageTexture):
    ''' <list type="bullet">
    ''' <item>a (uBaseOpacity = RenderableMesh.DecalBaseOpacity): 0 (the decal's RT 0 draw is unblended) -&gt; 1; 1 (blended, whatever
    ''' its factors: user decision 7-oct-2026, review rev-06) -&gt; the colour-output alpha of the farthest painting fragment
    ''' (tBaseAlpha, written by the depth draws into an RGBA16 UNORM target, which clamps it to [0, 1] and rounds it to 16 bits,
    ''' GL 4.6 2.3.5.2; the clamp here states the law - the fixed-point RT 0 blend clamps the alpha, GL 4.6 17.3.6 - for any format).
    ''' Premultiplied, the base gives a decal of any blend exactly "decal over a surface B": SRC_ALPHA / INV_SRC_ALPHA a D + (1 - a) a B
    ''' over coverage a; Skyrim SE's MULTBLEND_DECAL DEST_COLOR / INV_SRC_ALPHA (D a B + (1 - a) a B) / a = (D + 1 - a) B; Fallout 4's
    ''' mode 6 DEST_COLOR / ZERO D a B / a = D B (a colour source factor adds no coverage: the base's a is the pixel's coverage).
    ''' Stacked decals: a is the FARTHEST painting decal's (the one whose depth the base takes).</item>
    ''' <item>o0 (RT 0x1A, SRGB): rgb = a x the preview's background at the pixel as the FO4 scene's material colours (powf 2.2,
    ''' Shader_Base_Class.MaterialColor; the floor's backgroundLinear): the albedo premultiplied, kept so by the decals' SRC_ALPHA
    ''' blend over it (a_d D + (1 - a_d) a B); the lights and composite 1 are linear in the albedo (3 albedo t5, DF 4.3), so the
    ''' composited radiance is premultiplied by the coverage the post divides by (Fragment_PostFo4). .w = SpecularParam.z of a
    ''' plain surface = 0 (no back light). The pixel is read in GL rows: the stage draws under UPPER_LEFT (D3D rows, AppMain),
    ''' backgroundAt wants the rows of the display (PostProcess.vb Fragment_PostFo4), so y = H - y.</item>
    ''' <item>o1 (RT 0x1B): the decal's geometric normal with the records' encode (rec2762 L75-91: engine view = (x, y, -z_gl),
    ''' z = min(z, 0), normalised, xy / sqrt(8 - 8 z) + 0.5), NOT premultiplied (a direction: the decals blend theirs over it as the
    ''' engine blends a decal's over a surface's). Geometric normal = AppMain's and Fragment_FO4's geoNormal law: the interpolated
    ''' vertex normal (mv_tbn column 2), flipped on back faces of a double-sided shape; MSN: the map's normal (v_msnMatrix * t1.rbg,
    ''' Fragment_FO4's MSN branch).</item>
    ''' <item>o2 (RT 0x1D) = 0: no SSR mask, no cube, no envmap, cb2[5].x = 0 (rec2762 L92-107 with zero constants).</item>
    ''' <item>o3 (RT 0x1E) = (0, 0, 0, 1): no gloss, no specular, SpecularParam.w = 0; id 255 = the default of the 245 PS
    ''' (mov o3.w, 1.0; RE_FO4_DEFERRED_FRAME 6, ids).</item>
    ''' <item>o4 (RT 0x1F) = 0: no emissive.</item>
    ''' <item>oShadow (app RT, propuesta B): Fo4GBufferSource.AppShadowFactors with AppMain's inputs - the geometric normal gn and the
    ''' written o1.xy; its law takes the decoded o1 for an MSN shape - the one shadow composition of that RT, input law included
    ''' (review rev-07). bModelSpace is declared there.</item>
    ''' <item>oCoverage (DecalBaseTarget's coverage target, attachment 6 of its surface framebuffer): a, accumulated over-style with
    ''' the decals' coverage draws (blend ONE / ONE_MINUS_SRC_ALPHA on that buffer only): the base is one more layer of the coverage
    ''' composite 2 hands the post, as the radiance is premultiplied by it.</item>
    ''' </list>
    ''' The decals then blend over it with their own state (write mode 1 = RGB: o0.w and o3.w stay the base's).</summary>
    Friend Const Fragment_DecalBaseSurface As String = "#version 430
" & BackgroundFadeSource.Fade_Helper & "
in mat3 mv_tbn;
in mat3 v_msnMatrix;
in vec2 vUV;
in vec3 vWorldPos;
uniform vec2 uvScale;
uniform vec2 uvOffset;
uniform bool bDoubleSided;
uniform int uBaseOpacity;            // RenderableMesh.DecalBaseOpacity: 0 (unblended) -> 1, 1 (blended) -> the decal's alpha
layout(binding = 1) uniform sampler2D t1;    // the record's normal map (Fo4GBufferSource t1): read only for an MSN shape
layout(binding = 2) uniform sampler2D tBaseAlpha;   // DecalBaseTarget.AlphaTexture (RGBA16 UNORM, .a): the farthest painting decal's alpha
layout(location = 0) out vec4 o0;    // RT 0x1A  R8G8B8A8_UNORM_SRGB
layout(location = 1) out vec4 o1;    // RT 0x1B  R16G16_UNORM
layout(location = 2) out vec4 o2;    // RT 0x1D  R8G8B8A8_UNORM
layout(location = 3) out vec4 o3;    // RT 0x1E  R8G8B8A8_UNORM
layout(location = 4) out vec4 o4;    // RT 0x1F  R8G8B8A8_UNORM_SRGB
layout(location = 5) out vec4 oShadow;   // APP: shadow factor of each rig light (propuesta B)
layout(location = 6) out vec4 oCoverage; // APP: the base's opacity a into DecalBaseTarget's coverage target (blended ONE / ONE_MINUS_SRC_ALPHA)
" & Fo4GBufferSource.AppShadowFactors & "
" & D3d11SemanticsSource.Glsl & "
void main()
{
    vec3 gn = bModelSpace ? normalize(v_msnMatrix * (texture(t1, vUV * uvScale + uvOffset).rbg * 2.0 - 1.0))
                          : normalize(mv_tbn * vec3(0.0, 0.0, 0.5));
    if (bDoubleSided && !gl_FrontFacing) gn = -gn;
    float a = (uBaseOpacity == 0) ? 1.0 : clamp(texelFetch(tBaseAlpha, ivec2(gl_FragCoord.xy), 0).a, 0.0, 1.0);
    vec3 bg = backgroundAt(vec2(gl_FragCoord.x, viewportSize.y - gl_FragCoord.y));
    o0 = vec4(a * pow(bg, vec3(2.2)), 0.0);
    vec3 n = vec3(gn.x, gn.y, d3d_min(-gn.z, 0.0));
    n = n * d3d_rsq(dot(n, n));
    float s = d3d_sqrt(n.z * -8.0 + 8.0);
    o1 = vec4(d3d_div(n.x, s) + 0.5, d3d_div(n.y, s) + 0.5, 0.0, 0.0);
    o2 = vec4(0.0);
    o3 = vec4(0.0, 0.0, 0.0, 1.0);
    o4 = vec4(0.0);
    oShadow = appShadowFactors(vWorldPos, gn, o1.xy);
    oCoverage = vec4(a);
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
    Friend ReadOnly Sun, SunShadow, Fill, Ambient, Composite1, Composite2, LinearZ, DirectResolve, DecalBase, DecalBaseSurface As Fo4_Deferred_Shader_Class
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
            DecalBaseSurface = Build(Shader_Class_Fo4.Vertex_FO4, Fo4DeferredAppSource.Fragment_DecalBaseSurface)
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
    ''' <paramref name="role"/> (Sun, SunShadow, Fill, Ambient, Composite1, Composite2, LinearZ, DirectResolve, DecalBase,
    ''' DecalBaseSurface) replaced.
    ''' The copy owns nothing: its Dispose releases no program.</summary>
    Friend Function GateWith(role As String, program As Fo4_Deferred_Shader_Class) As Fo4DeferredPrograms
        Return New Fo4DeferredPrograms(Me, role, program)
    End Function

    Private Sub New(src As Fo4DeferredPrograms, role As String, program As Fo4_Deferred_Shader_Class)
        If Not {"Sun", "SunShadow", "Fill", "Ambient", "Composite1", "Composite2", "LinearZ", "DirectResolve", "DecalBase", "DecalBaseSurface"}.Contains(role) Then
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
        DecalBaseSurface = If(role = "DecalBaseSurface", program, src.DecalBaseSurface)
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
''' 0x21/0x22, RT 2, RT 0x28 (t15). The whole frame up to the end of the forward stage is in D3D row order (glClipControl
''' UPPER_LEFT, propuesta A; composite 2 and the forward stage too since the zfight-overlay fix, 8-oct-2026: one orientation for
''' everything that tests against the G-buffer depth, as in the game), and MirrorRows turns the frame's targets to GL rows at the
''' end of that stage (PreviewControl.EndFo4Stage).</summary>
Friend NotInheritable Class Fo4DeferredTargets
    Private _w As Integer, _h As Integer
    Private _albedo, _normal, _env, _gloss, _emit, _shadow, _depth, _depthCopy, _lightD, _lightS, _rt2, _linZ As Integer
    Private _fboG, _fboL, _fboC1, _fboZ As Integer
    ''' <summary>MirrorRows: one scratch image per format it turns (R11F_G11F_B10F, RGBA8, D24S8) and its read / draw framebuffers.</summary>
    Private _mirR11, _mirRgba8, _mirDs, _fboMirSrc, _fboMirDst As Integer
    ''' <summary>The translucent decals' base (BeginDecalBase / EndDecalBase), with its alpha and coverage targets.</summary>
    Private ReadOnly _base As New DecalBaseTarget(deferred:=True)
    ''' <summary>This frame's G-buffer stage drew the base (BeginDecalBase succeeded since BeginGBuffer): composite 2 takes the coverage
    ''' of its pixels from it and the direct resolve mixes by the coverage.</summary>
    Private _baseDrawn As Boolean

    ''' <summary>The translucent decals' base target (the base draws bind its framebuffers; gates read its textures).</summary>
    Friend ReadOnly Property DecalBase As DecalBaseTarget
        Get
            Return _base
        End Get
    End Property
    Private _quadVao, _quadVbo, _triVao, _triVbo, _emptyVao As Integer
    Private _point, _cubeSampler As Integer
    Private Shared ReadOnly GBufferBuffers As DrawBuffersEnum() = {DrawBuffersEnum.ColorAttachment0, DrawBuffersEnum.ColorAttachment1,
        DrawBuffersEnum.ColorAttachment2, DrawBuffersEnum.ColorAttachment3, DrawBuffersEnum.ColorAttachment4, DrawBuffersEnum.ColorAttachment5}
    Private Shared ReadOnly LightBuffers As DrawBuffersEnum() = {DrawBuffersEnum.ColorAttachment0, DrawBuffersEnum.ColorAttachment1}

    ''' <summary>GATE ONLY (no UI): ShadowGate --fo4-deferred-scene mutant. True leaves the G-buffer stage in LOWER_LEFT.</summary>
    Friend Shared GateNoUpperLeft As Boolean = False
    ''' <summary>GATE ONLY (no UI): ShadowGate --fo4-deferred-scene mutant. True draws the G-buffer without FRAMEBUFFER_SRGB.</summary>
    Friend Shared GateNoGBufferSrgb As Boolean = False
    ''' <summary>GATE ONLY (no UI): ShadowGate --fo4-deferred-scene mutant. True blits the G-buffer depth mirrored in y (the row order of
    ''' the forward stage before the zfight-overlay fix).</summary>
    Friend Shared GateBlitMirror As Boolean = False
    ''' <summary>GATE ONLY (no UI): ShadowGate --fo4-decal-base mutant. True keeps the NEAREST translucent decal as the base (the
    ''' mark writes 1.0, the base draws LESS - RenderableMesh.ApplyDecalBaseState) instead of the farthest.</summary>
    Friend Shared GateDecalBaseNearest As Boolean = False
    ''' <summary>GATE ONLY (no UI): ShadowGate --decal-base-surface mutant "no mix" (review rev-02). True: the direct resolve (post
    ''' off) takes coverage 1 on every pixel (uCoverageBlend False) in a frame with the translucent decals' base.</summary>
    Friend Shared GateDirectResolveNoCoverage As Boolean = False

    ''' <summary>GATE ONLY: the textures of the deferred targets, by role (ShadowGate --fo4-deferred-law / --fo4-deferred-scene).</summary>
    Friend ReadOnly Property GateTextures As (Albedo As Integer, Normal As Integer, Env As Integer, Gloss As Integer, Emit As Integer,
                                              Shadow As Integer, Depth As Integer, LightD As Integer, LightS As Integer, Rt2 As Integer,
                                              LinZ As Integer, DecalBase As Integer)
        Get
            Return (_albedo, _normal, _env, _gloss, _emit, _shadow, _depth, _lightD, _lightS, _rt2, _linZ, _base.AuxTexture)
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
        ok = ok AndAlso _base.Ensure(w, h)
        _mirR11 = Tex(SizedInternalFormat.R11fG11fB10f, w, h)
        _mirRgba8 = Tex(SizedInternalFormat.Rgba8, w, h)
        _mirDs = Tex(SizedInternalFormat.Depth24Stencil8, w, h)
        GL.CreateFramebuffers(1, _fboMirSrc)
        GL.CreateFramebuffers(1, _fboMirDst)
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
        _baseDrawn = False
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

    ''' <summary>The G-buffer depth into the copy the lights and composites sample (the feedback loop, propuesta A.3; the translucent
    ''' decals' base mark reads the same texture, DecalBaseTarget.Begin copying the depth before the decals into it): CopyImageSubData,
    ''' raw texels (GL 4.6 18.3.2: "does not perform general-purpose conversions such as scaling, resizing, blending, color-space, or
    ''' format conversions. It should be considered to operate in a manner similar to a CPU memcpy").</summary>
    Private Sub CopyDepth()
        GL.CopyImageSubData(_depth, ImageTarget.Texture2D, 0, 0, 0, 0, _depthCopy, ImageTarget.Texture2D, 0, 0, 0, 0, _w, _h, 1)
    End Sub

    ''' <summary>Opens the base pass of the translucent decals (user decisions 5-oct-2026 and 7-oct-2026; an app pass, Fallout 4 has
    ''' none: its group-3 decals test LESS_EQUAL without writing, Fo4RenderPassLaw, so over the clear depth they fail and the lights
    ''' and composites skip the pixel, LightState / RunComposite1 / RunComposite2). DecalBaseTarget.Begin on the G-buffer depth
    ''' (no depth of a decal goes through a float, user order) with the six G-buffer RTs as the base surface's targets
    ''' (Fo4DeferredAppSource.Fragment_DecalBaseSurface); the mark reads this target's depth copy (_depthCopy: the copy the lights
    ''' and composites sample, taken again by EndGBuffer - the frame keeps one sampled D24S8 copy, review rev-03). False when that
    ''' target is incomplete: nothing bound, no base.</summary>
    Public Function BeginDecalBase(progs As Fo4DeferredPrograms) As Boolean
        _baseDrawn = _base.Begin(progs.DecalBase, _depth, ImageTarget.Texture2D, _depthCopy,
                                 {(_albedo, False), (_normal, False), (_env, False), (_gloss, False), (_emit, False), (_shadow, False)},
                                 GateDecalBaseNearest)
        Return _baseDrawn
    End Function

    ''' <summary>Closes the base pass: DecalBaseTarget.Fill (the marked pixels no decal took get 1.0 back), Close, and the auxiliary
    ''' depth blitted back into the G-buffer depth (DecalBaseTarget.BlitDepthTo: where something was behind it is the G-buffer's own
    ''' value, copied and blitted back; the G-buffer stencil is not in the mask). Leaves the G-buffer bound, LESS_EQUAL, stencil
    ''' test off, for the translucent decals, which then draw unchanged.</summary>
    Public Sub EndDecalBase(progs As Fo4DeferredPrograms)
        _base.Fill(progs.DecalBase)
        _base.Close()
        _base.BlitDepthTo(_fboG)
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _fboG)
        GL.Viewport(0, 0, _w, _h)
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
    End Sub

    ''' <summary>The G-buffer depth/stencil into the scene target's, straight (NEAREST, same format: an exact copy): the scene target
    ''' stays in D3D rows through composite 2 and the forward stage (the zfight-overlay fix, 8-oct-2026: the forward draws rasterise
    ''' in the orientation this depth was written in, so an overlay on its body's geometry gets the body's depth bit for bit).
    ''' GateBlitMirror: the old mirrored blit.</summary>
    Public Sub BlitDepthTo(targetFbo As Integer)
        If GateBlitMirror Then GL.BlitNamedFramebuffer(_fboG, targetFbo, 0, 0, _w, _h, 0, _h, _w, 0, ClearBufferMask.DepthBufferBit Or ClearBufferMask.StencilBufferBit, BlitFramebufferFilter.Nearest) : Return
        GL.BlitNamedFramebuffer(_fboG, targetFbo, 0, 0, _w, _h, 0, 0, _w, _h,
                                ClearBufferMask.DepthBufferBit Or ClearBufferMask.StencilBufferBit, BlitFramebufferFilter.Nearest)
    End Sub

    ''' <summary>One image of the frame turned between D3D and GL row order, in place (PreviewControl: the frame's targets at the end
    ''' of the D3D-order stage, and the post-off display's background into it): blitted mirrored in y into the scratch image of its
    ''' format (NEAREST, same size and format: nothing converted), then copied back raw (CopyImageSubData, GL 4.6 18.3.2: "a manner
    ''' similar to a CPU memcpy"). <paramref name="format"/> is the image's: R11F_G11F_B10F, RGBA8 or DEPTH24_STENCIL8 (depth and
    ''' stencil together). The frame's own framebuffers are not touched (the image is attached to this class's pair).</summary>
    Public Sub MirrorRows(image As Integer, target As ImageTarget, format As SizedInternalFormat)
        Dim isDs = format = SizedInternalFormat.Depth24Stencil8
        Dim scratch As Integer
        Select Case format
            Case SizedInternalFormat.R11fG11fB10f : scratch = _mirR11
            Case SizedInternalFormat.Rgba8 : scratch = _mirRgba8
            Case SizedInternalFormat.Depth24Stencil8 : scratch = _mirDs
            Case Else : Throw New ArgumentException($"MirrorRows: no scratch image of format {format}", NameOf(format))
        End Select
        Dim att = If(isDs, FramebufferAttachment.DepthStencilAttachment, FramebufferAttachment.ColorAttachment0)
        Dim attach = Sub(fbo As Integer, img As Integer, tgt As ImageTarget)
                         If tgt = ImageTarget.Renderbuffer Then
                             GL.NamedFramebufferRenderbuffer(fbo, att, RenderbufferTarget.Renderbuffer, img)
                         Else
                             GL.NamedFramebufferTexture(fbo, att, img, 0)
                         End If
                     End Sub
        attach(_fboMirSrc, image, target)
        attach(_fboMirDst, scratch, ImageTarget.Texture2D)
        GL.NamedFramebufferReadBuffer(_fboMirSrc, If(isDs, ReadBufferMode.None, ReadBufferMode.ColorAttachment0))
        GL.NamedFramebufferDrawBuffer(_fboMirDst, If(isDs, DrawBufferMode.None, DrawBufferMode.ColorAttachment0))
        Dim mask = If(isDs, ClearBufferMask.DepthBufferBit Or ClearBufferMask.StencilBufferBit, ClearBufferMask.ColorBufferBit)
        GL.BlitNamedFramebuffer(_fboMirSrc, _fboMirDst, 0, 0, _w, _h, 0, _h, _w, 0, mask, BlitFramebufferFilter.Nearest)
        GL.CopyImageSubData(scratch, ImageTarget.Texture2D, 0, 0, 0, 0, image, target, 0, 0, 0, 0, _w, _h, 1)
        attach(_fboMirSrc, 0, target)
        attach(_fboMirDst, 0, ImageTarget.Texture2D)
    End Sub

    ''' <summary>Composite pass 2 (3495) into the scene target (D3D rows, still UPPER_LEFT: GREATER at z = 1 against the G-buffer depth
    ''' blitted straight; draw buffers {A0, A1} = radiance and coverage, rev-19).</summary>
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
        ' The translucent decals' base (user decision 7-oct-2026): the coverage of its pixels, DecalBaseTarget.CoverageTexture at unit 11.
        p.SetBool("uDecalBaseCoverage", _baseDrawn)
        p.SetVector4("SSSSSParams", New Vector4(4.0F, 2.5F, 9.0F, 0.0F))   ' DF 17.2
        p.SetVector4("SSLRParams0", New Vector4(1.0F, 0, 0, 0))
        p.SetVector4("SSLRParams1", New Vector4(0, 0, 1.0F, 0))
        SetCb12(p, f)
        GL.BindTextureUnit(1, _normal) : GL.BindTextureUnit(2, _env) : GL.BindTextureUnit(3, _gloss) : GL.BindTextureUnit(4, _emit)
        GL.BindTextureUnit(5, _lightD) : GL.BindTextureUnit(6, _lightS) : GL.BindTextureUnit(7, _depthCopy)
        GL.BindTextureUnit(8, envArray) : GL.BindTextureUnit(10, _rt2) : GL.BindTextureUnit(15, _linZ)
        GL.BindTextureUnit(11, _base.CoverageTexture)
        For Each u In {1, 2, 3, 4, 5, 6, 7, 10, 11, 15} : GL.BindSampler(u, _point) : Next
        GL.BindSampler(8, _cubeSampler)
        GL.BindVertexArray(_triVao) : GL.DrawArrays(PrimitiveType.Triangles, 0, 3) : GL.BindVertexArray(0)
        For Each u In {1, 2, 3, 4, 5, 6, 7, 8, 10, 11, 15} : GL.BindTextureUnit(u, 0) : GL.BindSampler(u, 0) : Next
        GL.UseProgram(0)
        GL.DepthMask(True) : GL.DepthFunc(DepthFunction.Lequal) : GL.Enable(EnableCap.CullFace)
    End Sub

    ''' <summary>Post off: the composited radiance onto the display target through the 2.3.8 display tail (propuesta C). With the
    ''' translucent decals' base in the frame, mixed over the background by the composite's coverage (<paramref name="coverageTex"/>,
    ''' Fo4DeferredAppSource.Fragment_DirectResolve): blend colour SRC_ALPHA / ONE_MINUS_SRC_ALPHA, alpha ZERO / ONE (review rev-01: the
    ''' display target's alpha is the frame's - CaptureBitmap reads it, Format32bppArgb - not the coverage's); without it, unblended
    ''' as before. GateDirectResolveNoCoverage: the mutant without the mix (uCoverageBlend False).</summary>
    Public Sub RunDirectResolve(progs As Fo4DeferredPrograms, sceneTex As Integer, coverageTex As Integer, displayFbo As Integer, sceneToLinear As Single)
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, displayFbo)
        GL.Viewport(0, 0, _w, _h)
        GL.Enable(EnableCap.DepthTest) : GL.DepthFunc(DepthFunction.Greater) : GL.DepthMask(False)
        GL.Disable(EnableCap.Blend) : GL.Disable(EnableCap.CullFace)
        If _baseDrawn Then GL.Enable(EnableCap.Blend) : GL.BlendFuncSeparate(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha, BlendingFactorSrc.Zero, BlendingFactorDest.One)
        progs.DirectResolve.Use()
        progs.DirectResolve.SetFloat("uSceneToLinear", sceneToLinear)
        progs.DirectResolve.SetBool("uCoverageBlend", _baseDrawn AndAlso Not GateDirectResolveNoCoverage)
        GL.BindTextureUnit(0, sceneTex) : GL.BindTextureUnit(1, coverageTex)
        GL.BindVertexArray(_emptyVao) : GL.DrawArrays(PrimitiveType.Triangles, 0, 3) : GL.BindVertexArray(0)
        GL.BindTextureUnit(0, 0) : GL.BindTextureUnit(1, 0) : GL.UseProgram(0)
        GL.Disable(EnableCap.Blend)
        GL.DepthMask(True) : GL.DepthFunc(DepthFunction.Lequal) : GL.Enable(EnableCap.CullFace)
    End Sub

    Public Sub Free()
        For Each fb In {_fboG, _fboL, _fboC1, _fboZ, _fboMirSrc, _fboMirDst}
            If fb <> 0 Then GL.DeleteFramebuffer(fb)
        Next
        _fboG = 0 : _fboL = 0 : _fboC1 = 0 : _fboZ = 0 : _fboMirSrc = 0 : _fboMirDst = 0
        For Each t In {_albedo, _normal, _env, _gloss, _emit, _shadow, _depth, _depthCopy, _lightD, _lightS, _rt2, _linZ, _mirR11, _mirRgba8, _mirDs}
            If t <> 0 Then GL.DeleteTexture(t)
        Next
        _mirR11 = 0 : _mirRgba8 = 0 : _mirDs = 0
        _albedo = 0 : _normal = 0 : _env = 0 : _gloss = 0 : _emit = 0 : _shadow = 0 : _depth = 0 : _depthCopy = 0
        _lightD = 0 : _lightS = 0 : _rt2 = 0 : _linZ = 0
        _base.Free()
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
