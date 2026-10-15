Imports OpenTK.Graphics.OpenGL4
Imports OpenTK.Mathematics

''' <summary>The app's small passes of the FO4 deferred frame that are not transcribed shaders. ASCII only (GLSL).</summary>
Friend Module Fo4DeferredAppSource
    ''' <summary>The fragment of Fo4DeferredPrograms.EnginePositionProgram: none of its output is used (rasterizer discard).</summary>
    Friend Const Fragment_PositionCapture As String = "#version 430
void main() {}
"

    ''' <summary>The full-screen triangle of the app passes (attribute-less; same convention as the SAO's).</summary>
    Friend Const Vertex_Fullscreen As String = "#version 430
" & Gl43.ClipGlsl & "void main()
{
    vec2 p = vec2((gl_VertexID == 1) ? 3.0 : -1.0, (gl_VertexID == 2) ? 3.0 : -1.0);
    gl_Position = gl43ClipToGl(vec4(p, 1.0, 1.0));
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
    ''' <item>oCoverage (DecalBaseTarget's coverage target, attachment Fo4DeferredTargets.SurfaceCoverageBuffer of its surface
    ''' framebuffer): a, accumulated over-style with
    ''' the decals' coverage draws (blend ONE / ONE_MINUS_SRC_ALPHA on that buffer only): the base is one more layer of the coverage
    ''' composite 2 hands the post, as the radiance is premultiplied by it.</item>
    ''' </list>
    ''' The decals then blend over it with their own state (write mode 1 = RGB: o0.w and o3.w stay the base's).</summary>
    Friend Const Fragment_DecalBaseSurface As String = "#version 430
" & BackgroundFadeSource.Fade_Helper & "
// Ola 2: the record's engine VS feeds this app fragment: the inputs by semantic (gbufFromStageInputs, same generated helper).
#define FO4_GBUF_IN_ALL
" & Fo4GBufferSource.StageInputsBlock & DecalBaseSurfaceLaw & "
void main()
{
    Fo4GBufIn vin = gbufFromStageInputs();
    vec3 nm = texture(t1, vec2(vin.tex3.w, vin.tex4.w)).rbg * 2.0 - 1.0;   // the record's uv (TEXCOORD3.w / 4.w, rec2273 L103-105)
    vec3 ne = bModelSpace ? vec3(dot(vin.tex0.xyz, nm), dot(vin.tex1.xyz, nm), dot(vin.tex2.xyz, nm))   // MSN rows: view * world
                          : vec3(vin.tex0.z, vin.tex1.z, vin.tex2.z);                                    // Cn column, engine view
    vec3 gn = normalize(vec3(ne.x, ne.y, -ne.z));                                                        // GL view, as before
    if (bDoubleSided && !gl_FrontFacing) gn = -gn;
    decalBaseSurfaceOut(gn);
}
"

    ''' <summary>THE LAW OF THE BASE SURFACE, one text for its two inputs (D16, 12-oct-2026): the declarations, the outputs and
    ''' decalBaseSurfaceOut(gn), which writes o0..o4 and oCoverage from the geometric normal (GL view) (see
    ''' Fragment_DecalBaseSurface). Fragment_DecalBaseSurface feeds it from the record's engine VS, and
    ''' Fragment_DecalBaseSurfaceEffect from the effect's own engine VS (ola 3).</summary>
    Friend Const DecalBaseSurfaceLaw As String = "
uniform bool bDoubleSided;
uniform bool bModelSpace;            // the record's MSN (Fragment_DecalBaseSurface); never set for an effect
uniform int uBaseOpacity;            // RenderableMesh.DecalBaseOpacity: 0 (unblended) -> 1, 1 (blended) -> the decal's alpha
layout(binding = 1) uniform sampler2D t1;    // the record's normal map (Fo4GBufferSource t1): read only for an MSN shape
layout(binding = 2) uniform sampler2D tBaseAlpha;   // DecalBaseTarget.AlphaTexture (RGBA16 UNORM, .a): the farthest painting decal's alpha
layout(location = 0) out vec4 o0;    // RT 0x1A  R8G8B8A8_UNORM_SRGB
layout(location = 1) out vec4 o1;    // RT 0x1B  R16G16_UNORM
layout(location = 2) out vec4 o2;    // RT 0x1D  R8G8B8A8_UNORM
layout(location = 3) out vec4 o3;    // RT 0x1E  R8G8B8A8_UNORM
layout(location = 4) out vec4 o4;    // RT 0x1F  R8G8B8A8_UNORM_SRGB
layout(location = " & SurfaceCoverageLocationGlsl & ") out vec4 oCoverage; // APP: the base's opacity a into DecalBaseTarget's coverage target (blended ONE / ONE_MINUS_SRC_ALPHA)
" & D3d11SemanticsSource.Glsl & "
void decalBaseSurfaceOut(vec3 gn)
{
    float a = (uBaseOpacity == 0) ? 1.0 : clamp(texelFetch(tBaseAlpha, ivec2(gl_FragCoord.xy), 0).a, 0.0, 1.0);
    vec3 bg = backgroundAt(gl_FragCoord.xy);
    o0 = vec4(a * pow(bg, vec3(2.2)), 0.0);
    vec3 n = vec3(gn.x, gn.y, d3d_min(-gn.z, 0.0));
    n = n * d3d_rsq(dot(n, n));
    float s = d3d_sqrt(n.z * -8.0 + 8.0);
    o1 = vec4(d3d_div(n.x, s) + 0.5, d3d_div(n.y, s) + 0.5, 0.0, 0.0);
    o2 = vec4(0.0);
    o3 = vec4(0.0, 0.0, 0.0, 1.0);
    o4 = vec4(0.0);
    oCoverage = vec4(a);
}
"

    ''' <summary>The location of the base surface's oCoverage, written once: Fo4DeferredTargets.SurfaceCoverageBuffer reads it (a VB
    ''' Const String cannot concatenate an Integer constant; Gl43.ClipBindingGlsl's pattern).</summary>
    Friend Const SurfaceCoverageLocationGlsl As String = "5"

    ''' <summary>THE BASE SURFACE OF AN EFFECT DECAL (user decision 12-oct-2026, D16: no regression of "a decal alone is seen as laid on
    ''' something"; ola 3, Q2): the one law DecalBaseSurfaceLaw fed by the decal's own engine effect VS - the same text as its colour and
    ''' depth draws, so the same gl_Position (invariant) - with the surface foot Fo4EffectSource.AppSurfaceVertexFoot, which hands the
    ''' camera-relative world position appRel. The b05 VS carries no geometric normal for every technique: the surface's is the
    ''' triangle's, from the derivatives of appRel (GL 4.3 8.13.1 dFdx / dFdy, the driver's: GL43 decision 13-oct-2026), in the engine
    ''' view of cb12 rows 0..2, turned to the side the camera sees. An effect has no model-space normals: bModelSpace (declared by the
    ''' law) is never set. The program Fo4DeferredPrograms.EffectSurfaceProgram, drawn by
    ''' RenderableMesh.DrawDecalBaseSurface.</summary>
    Friend Const Fragment_DecalBaseSurfaceEffect As String = "#version 430
" & BackgroundFadeSource.Fade_Helper & "
in vec3 appRel;
uniform vec4 cb12_view[3];
" & DecalBaseSurfaceLaw & "
void main()
{
    vec3 nw = cross(dFdx(appRel), dFdy(appRel));
    vec3 ne = vec3(dot(cb12_view[0].xyz, nw), dot(cb12_view[1].xyz, nw), dot(cb12_view[2].xyz, nw));
    vec3 pe = vec3(dot(cb12_view[0].xyz, appRel), dot(cb12_view[1].xyz, appRel), dot(cb12_view[2].xyz, appRel));
    vec3 gn = normalize(vec3(ne.x, ne.y, -ne.z));                                              // GL view, as the records' surface
    if (dot(gn, vec3(pe.x, pe.y, -pe.z)) > 0.0) gn = -gn;                                      // the side the camera sees
    decalBaseSurfaceOut(gn);
}
"
End Module

Public Class Fo4_Deferred_Shader_Class
    Inherits Shader_Base_Class
    Sub New(vertex As String, fragment As String, Optional control As String = Nothing, Optional evaluation As String = Nothing,
            Optional transformFeedbackVaryings As String() = Nothing)
        MyBase.New(vertex, fragment, control, evaluation, transformFeedbackVaryings)
    End Sub
    ''' <summary>A compute program (the image-space CS, Fo4ImageSpaceSource.ComputeOf).</summary>
    Sub New(compute As String)
        MyBase.New(compute)
    End Sub
End Class

''' <summary>The programs of the FO4 deferred frame: the app passes and the light and composite programs of the techniques the
''' frame draws (Fo4DeferredLightLaw) built at start; the G-buffer record programs (the 470 of the source's closed list,
''' Fo4GBufferSource.Records), the effect programs, the utility programs (the shadow map's emitter and the refraction normals,
''' b07, Fo4UtilitySource) and the image-space programs (Fo4ImageSpaceSource) built on their FIRST USE (user decision 5-oct-2026:
''' cold, all 470 cost 15 s; one 30 ms). Debug builds every program of every transcribed closed list at start and throws on the
''' first failure (user decision 5-oct-2026; ola 4: the b10 / b11 / b07 / image-space lists too, and the b06 vertex stages with the
''' null fragment, the one way a VS-only list links). A record whose build fails is remembered with its error and its draw is
''' reported (GBufferProgram; never a silent "not drawn"). A start program that does not compile or link throws out of the
''' constructor after releasing the ones already built (rev-50: the control then runs without the FO4 deferred frame and says
''' why).</summary>
Friend NotInheritable Class Fo4DeferredPrograms
    Friend ReadOnly DirectResolve, DecalBase As Fo4_Deferred_Shader_Class
    Friend ReadOnly PrepassRigid, PrepassAlpha As Fo4_Deferred_Shader_Class
    ''' <summary>The programs of one vertex route (ola 2 v5: the GPU route - the engine VS blends cb10 - and the CPU route of a skinned
    ''' VS - Fo4GBufferSource.StageOf cpuRoute -, user decision 12-oct-2026): tuple -&gt; the record programs built so far, the tuples
    ''' whose build failed with the error text, the base-pass program of a record with forceEarlyDepthStencil (DecalBaseProgram)
    ''' and the decals' base SURFACE program (the tuple's engine stages + the app's surface fragment, revisor-25).</summary>
    Private NotInheritable Class RoutePrograms
        Friend ReadOnly Records As New Dictionary(Of (Vs As UInteger, Hs As UInteger, Ds As UInteger, Ps As UInteger), Fo4_Deferred_Shader_Class)
        Friend ReadOnly Errors As New Dictionary(Of (Vs As UInteger, Hs As UInteger, Ds As UInteger, Ps As UInteger), String)
        Friend ReadOnly Base As New Dictionary(Of (Vs As UInteger, Hs As UInteger, Ds As UInteger, Ps As UInteger), Fo4_Deferred_Shader_Class)
        Friend ReadOnly Surface As New Dictionary(Of (Vs As UInteger, Hs As UInteger, Ds As UInteger, Ps As UInteger), Fo4_Deferred_Shader_Class)
        Friend ReadOnly Position As New Dictionary(Of UInteger, Fo4_Deferred_Shader_Class)
    End Class
    Private ReadOnly _gpuRoute As New RoutePrograms, _cpuRoute As New RoutePrograms
    ''' <summary>The route a draw's program comes from: the CPU route only for a VS that has one (a rigid VS has one route).</summary>
    Private Function RouteOf(vs As UInteger, cpuRoute As Boolean) As RoutePrograms
        Return If(Fo4GBufferSource.EffectiveCpuLayout(vs, cpuRoute) <> Fo4CpuRouteLayout.None, _cpuRoute, _gpuRoute)
    End Function
    ''' <summary>GateWith copies: the programs that own the records not overridden by the gate.</summary>
    Private ReadOnly _gbufferSource As Fo4DeferredPrograms
    Private ReadOnly _built As New List(Of Shader_Base_Class)

    ''' <summary>Ola 4: the light (b10) and composite (b11) programs by technique, built at start for the techniques the frame draws
    ''' (Fo4DeferredLightLaw.AppLightTechniques / AppCompositeTechniques).</summary>
    Private ReadOnly _lights As New Dictionary(Of UInteger, Fo4_Deferred_Shader_Class)
    Private ReadOnly _composites As New Dictionary(Of UInteger, Fo4_Deferred_Shader_Class)

    Private Function Build(vertex As String, fragment As String) As Fo4_Deferred_Shader_Class
        Dim p As New Fo4_Deferred_Shader_Class(vertex, fragment)
        _built.Add(p)
        Return p
    End Function

    Private Function BuildCompute(compute As String) As Fo4_Deferred_Shader_Class
        Dim p As New Fo4_Deferred_Shader_Class(compute)
        _built.Add(p)
        Return p
    End Function

    ''' <summary>The program of light technique <paramref name="t"/> (b10: BSDFLightShader SetupTechnique 0x142246B40, VS / PS by
    ''' Fo4DFLightSource.VertexShaderId / PixelShaderId). A technique whose program the engine does not have throws: the frame's
    ''' techniques are a closed list built at start.</summary>
    Private Function BuildLight(t As UInteger) As Fo4_Deferred_Shader_Class
        Dim missing = Fo4DFLightSource.MissingEnginePrograms(t)
        If missing IsNot Nothing Then Throw New InvalidOperationException($"light technique 0x{t:X8}: {missing}")
        Return Build(Fo4DFLightSource.VertexOf(Fo4DFLightSource.VertexShaderId(t)), Fo4DFLightSource.FragmentOf(Fo4DFLightSource.PixelShaderId(t)))
    End Function

    ''' <summary>The program of composite technique <paramref name="t"/> (b11: BSDFCompositeShader SetupTechnique 0x142216AC0, VS / PS
    ''' by Fo4DFCompositeSource.VertexShaderId / PixelShaderId; no tiled lighting, Fo4DeferredLightLaw.SunTechnique).</summary>
    Private Function BuildComposite(t As UInteger) As Fo4_Deferred_Shader_Class
        Dim missing = Fo4DFCompositeSource.MissingEnginePrograms(t)
        If missing IsNot Nothing Then Throw New InvalidOperationException($"composite technique 0x{t:X8}: {missing}")
        Return Build(Fo4DFCompositeSource.VertexOf(Fo4DFCompositeSource.VertexShaderId(t)), Fo4DFCompositeSource.FragmentOf(Fo4DFCompositeSource.PixelShaderId(t)))
    End Function

    Friend Sub New()
        Dim sw = Diagnostics.Stopwatch.StartNew()
        Try
            For Each t In Fo4DeferredLightLaw.AppLightTechniques
                If Not _lights.ContainsKey(t) Then _lights(t) = BuildLight(t)
            Next
            For Each t In Fo4DeferredLightLaw.AppCompositeTechniques
                If Not _composites.ContainsKey(t) Then _composites(t) = BuildComposite(t)
            Next
            DirectResolve = Build(Fo4DeferredAppSource.Vertex_Fullscreen, Fo4DeferredAppSource.Fragment_DirectResolve)
            DecalBase = Build(Fo4DeferredAppSource.Vertex_Fullscreen, Fo4DeferredAppSource.Fragment_DecalBase)
            PrepassRigid = Build(Fo4GBufferSource.PrepassVertex(3928), Fo4GBufferSource.PrepassFragmentNone)         ' 0x14181FBCF..C19
            PrepassAlpha = Build(Fo4GBufferSource.PrepassVertex(3929), Fo4GBufferSource.PrepassFragment(3930))      ' 0x14181FC99..D01
            PreviewControl.TraceBoot($"Fo4DeferredPrograms: the start programs ({_built.Count}: lights, composites, app passes)", sw)
#If DEBUG Then
            ' rev-E08: the rig's Darkness reaches the shadowed sun techniques the rig draws only through the line the generator
            ' rewrote in their PS (Fo4DFLightSource.AppShadowDarknessLines): a law technique outside that list would lose it.
            For Each ambient In {True, False}
                Dim ps = Fo4DFLightSource.PixelShaderId(Fo4DeferredLightLaw.SunTechnique(True, ambient))
                If Not Fo4DFLightSource.AppShadowDarknessPs.Contains(ps) Then Throw New Exception($"light PS 0x{ps:X8} of the rig's shadowed sun is not in Fo4DFLightSource.AppShadowDarknessPs (the rig's Darkness would not reach it)")
            Next
            ' Debug (user decision 5-oct-2026): every record of the closed list built at start; the first failure throws.
            For Each tv In Fo4GBufferSource.LawTechniques
                ' DIVERGENCE (ola 2 ensayo): a VS the generator could not transcribe (its reason in VertexStages: a constant without a
                ' cited writer / row count) is a declared gap, reported at draw; it does not stop the Debug start.
                If Fo4GBufferSource.VertexStages(New Fo4Technique(tv).Key().Vs).NotTranscribed <> "" Then Continue For
                Dim errorText As String = Nothing
                If GBufferProgram(New Fo4Technique(tv), errorText) Is Nothing Then Throw New Exception($"G-buffer technique 0x{tv:X8} (PS {Fo4GBufferTechnique.PixelShaderId(tv):X8}): {errorText}")
                If GBufferProgram(New Fo4Technique(tv), errorText, cpuRoute:=True) Is Nothing Then Throw New Exception($"G-buffer technique 0x{tv:X8} CPU route (PS {Fo4GBufferTechnique.PixelShaderId(tv):X8}): {errorText}")
            Next
            PreviewControl.TraceBoot($"Fo4DeferredPrograms (Debug census): G-buffer, {Fo4GBufferSource.LawTechniques.Length} law techniques x 2 routes", sw)
            ' Ola 3 (user decision 13-oct-2026: built on demand, every one in Debug): every effect technique of the law on both vertex
            ' routes, with its decals' base surface program; the first failure throws.
            For Each tv In Fo4EffectSource.LawTechniques
                Dim errorText As String = Nothing
                For Each cpu In {False, True}
                    If EffectProgram(New Fo4EffectTech(tv), errorText, cpu) Is Nothing Then Throw New Exception($"effect technique 0x{tv:X8}{If(cpu, " CPU route", "")}: {errorText}")
                    If EffectSurfaceProgram(New Fo4EffectTech(tv), cpu) Is Nothing Then Throw New Exception($"effect technique 0x{tv:X8}{If(cpu, " CPU route", "")}: base surface not built")
                Next
            Next
            PreviewControl.TraceBoot($"Fo4DeferredPrograms (Debug census): effects, {Fo4EffectSource.LawTechniques.Length} law techniques x 2 routes with their base surface", sw)
            ' Ola 4: the transcribed closed lists that are not drawn by the start techniques, every program built (and released: the
            ' census keeps none of them). b10: VS 0 with every transcribed PS (a declared gap - Fn empty - is not built); b11: every
            ' law tuple; b07: every law tuple on both routes, the null fragment where the technique binds no PS; image space: every
            ' VS / PS pair and every CS whose stages are transcribed; b06: every VS (both routes) with the null fragment.
            For Each ps In Fo4DFLightSource.PixelShaders.Keys
                If Fo4DFLightSource.PixelShaders(ps).Fn = "" Then Continue For
                CensusBuild($"light PS 0x{ps:X8}", Function() New Fo4_Deferred_Shader_Class(Fo4DFLightSource.VertexOf(0UI), Fo4DFLightSource.FragmentOf(ps)))
            Next
            PreviewControl.TraceBoot("Fo4DeferredPrograms (Debug census): b10 light PS", sw)
            For Each tu In Fo4DFCompositeSource.LawTuples
                CensusBuild($"composite (VS 0x{tu.Vs:X8}, PS 0x{tu.Ps:X8})", Function() New Fo4_Deferred_Shader_Class(Fo4DFCompositeSource.VertexOf(tu.Vs), Fo4DFCompositeSource.FragmentOf(tu.Ps)))
            Next
            PreviewControl.TraceBoot($"Fo4DeferredPrograms (Debug census): b11, {Fo4DFCompositeSource.LawTuples.Length} composite tuples", sw)
            Dim utilityVs As New HashSet(Of UInteger)
            For Each tu In Fo4UtilitySource.LawTuples
                If Fo4UtilitySource.VertexStages(tu.Vs).Fn = "" Then Continue For
                Dim ps = tu.Ps
                Dim fragment = If(Fo4UtilitySource.PixelShaders(ps).Fn = "", Nothing, Fo4UtilitySource.FragmentOf(ps))
                Dim firstOfVs = utilityVs.Add(tu.Vs)
                For Each cpu In {False, True}
                    If cpu AndAlso Fo4UtilitySource.EffectiveCpuLayout(tu.Vs, True) = Fo4CpuRouteLayout.None Then Continue For
                    Dim vertex = Fo4UtilitySource.VertexOf(tu.Vs, cpu)
                    If firstOfVs Then CensusBuild($"utility (VS 0x{tu.Vs:X8}{If(cpu, " CPU route", "")}, null PS)", Function() New Fo4_Deferred_Shader_Class(vertex, Fo4UtilitySource.NullFragment()))
                    If fragment IsNot Nothing Then CensusBuild($"utility (VS 0x{tu.Vs:X8}{If(cpu, " CPU route", "")}, PS 0x{ps:X8})", Function() New Fo4_Deferred_Shader_Class(vertex, fragment))
                Next
            Next
            PreviewControl.TraceBoot($"Fo4DeferredPrograms (Debug census): b07, {Fo4UtilitySource.LawTuples.Length} utility tuples", sw)
            For Each pr In Fo4ImageSpaceSource.Programs
                If pr.Cs <> 0UI Then
                    If Fo4ImageSpaceSource.ComputeStages(pr.Cs).Fn <> "" Then CensusBuild($"image-space CS {pr.Cs} {pr.Name}", Function() New Fo4_Deferred_Shader_Class(Fo4ImageSpaceSource.ComputeOf(pr.Cs)))
                ElseIf Fo4ImageSpaceSource.VertexStages(pr.Vs).Fn <> "" AndAlso Fo4ImageSpaceSource.PixelShaders(pr.Ps).Fn <> "" Then
                    CensusBuild($"image-space {pr.Vs}/{pr.Ps} {pr.Name}", Function() New Fo4_Deferred_Shader_Class(Fo4ImageSpaceSource.VertexOf(pr.Vs), Fo4ImageSpaceSource.FragmentOf(pr.Ps)))
                End If
            Next
            PreviewControl.TraceBoot($"Fo4DeferredPrograms (Debug census): image space, {Fo4ImageSpaceSource.Programs.Length} programs", sw)
            For Each vs In Fo4LightingSource.VertexStages.Keys
                For Each cpu In {False, True}
                    If cpu AndAlso Fo4LightingSource.VertexStages(vs).CpuLayout = Fo4CpuRouteLayout.None Then Continue For
                    CensusBuild($"lighting VS 0x{vs:X8}{If(cpu, " CPU route", "")}", Function() New Fo4_Deferred_Shader_Class(Fo4LightingSource.VertexOf(vs, cpu), Fo4GBufferSource.PrepassFragmentNone))
                Next
            Next
            PreviewControl.TraceBoot("Fo4DeferredPrograms (Debug census): b06 lighting VS", sw)
#End If
        Catch
            Dispose()
            Throw
        End Try
    End Sub

#If DEBUG Then
    ''' <summary>DEBUG ONLY (the start census of ola 4): builds one program and releases it; the first failure throws with its name.</summary>
    Private Shared Sub CensusBuild(what As String, make As Func(Of Fo4_Deferred_Shader_Class))
        Dim p As Fo4_Deferred_Shader_Class
        Try
            p = make()
        Catch ex As Exception
            Throw New Exception($"{what}: {ex.Message}", ex)
        End Try
        p.Dispose()
    End Sub
#End If

    ''' <summary>The light program of technique <paramref name="t"/> (one of Fo4DeferredLightLaw.AppLightTechniques; a GateWithLight
    ''' copy answers its own for the overridden technique). gates-v6 (rev-E16): Nothing for a technique outside the closed list built
    ''' at start (the caller does not draw that light and says so: RunLights / SkippedLightTechniques) - except in Debug while a
    ''' library mutant is on (Fo4GBufferConstants.GateMutantOn), where a technique the engine has is built on demand.</summary>
    Friend Function LightProgram(t As UInteger) As Fo4_Deferred_Shader_Class
        If _gateLight.HasValue AndAlso _gateLight.Value.Technique = t Then Return _gateLight.Value.Program
        If _gbufferSource IsNot Nothing Then Return _gbufferSource.LightProgram(t)
        Dim p As Fo4_Deferred_Shader_Class = Nothing
#If DEBUG Then
        ' GATE (ShadowGate --fo4-light-law, gates-v6 rev-E19): a technique taken out of _lights - the shadowed sun without the fused
        ' ambient, the technique of a rig's second and later shadowed lights - so the frame meets a light without a program
        If Fo4GBufferConstants.GateMutantOn("dfl-drop-light") AndAlso t = Fo4DeferredLightLaw.SunTechnique(True, False) Then Return Nothing
#End If
        If _lights.TryGetValue(t, p) Then Return p
#If DEBUG Then
        ' GATE (ShadowGate --fo4-light-law, gates-v6 rev-E16): the mutated technique built on demand, the mutant reaches a verdict
        If Fo4GBufferConstants.GateVertexConstantsMutant IsNot Nothing AndAlso Fo4DFLightSource.MissingEnginePrograms(t) Is Nothing Then
            p = BuildLight(t)
            _lights(t) = p
            Return p
        End If
#End If
        Return Nothing
    End Function

    ''' <summary>The composite program of technique <paramref name="t"/> (one of Fo4DeferredLightLaw.AppCompositeTechniques; a
    ''' GateWithComposite copy answers its own for the overridden technique).</summary>
    Friend Function CompositeProgram(t As UInteger) As Fo4_Deferred_Shader_Class
        If _gateComposite.HasValue AndAlso _gateComposite.Value.Technique = t Then Return _gateComposite.Value.Program
        If _gbufferSource IsNot Nothing Then Return _gbufferSource.CompositeProgram(t)
        Return _composites(t)
    End Function

    ''' <summary>Ola 4: the utility programs (b07) of one vertex route, keyed by (VS, PS, null PS): built on first use, the failed
    ''' ones remembered with their error (as the records').</summary>
    Private NotInheritable Class UtilityRoute
        Friend ReadOnly Programs As New Dictionary(Of (Vs As UInteger, Ps As UInteger, NullPs As Boolean), Fo4_Deferred_Shader_Class)
        Friend ReadOnly Errors As New Dictionary(Of (Vs As UInteger, Ps As UInteger, NullPs As Boolean), String)
    End Class
    Private ReadOnly _utilityGpu As New UtilityRoute, _utilityCpu As New UtilityRoute

    ''' <summary>The program of utility technique <paramref name="t"/> on the draw's vertex route (b07: BSUtilityShader SetupTechnique
    ''' 0x1422409A0; VS Fo4UtilitySource.VertexShaderId, PS PixelShaderId or none when PixelShaderNull, 0x142240C82..C91 - the
    ''' rasterised depth only, Fo4UtilitySource.NullFragment). Nothing when the engine has no program for it (MissingEnginePrograms:
    ''' it does not draw it), when the preview cannot draw it (GapOf), or when its build failed; <paramref name="errorText"/> says
    ''' which.</summary>
    Friend Function UtilityProgram(t As UInteger, ByRef errorText As String, Optional cpuRoute As Boolean = False) As Fo4_Deferred_Shader_Class
        errorText = Fo4UtilitySource.MissingEnginePrograms(t)
        If errorText IsNot Nothing Then Return Nothing
        errorText = Fo4UtilitySource.GapOf(t)
        If errorText <> "" Then Return Nothing
        errorText = Nothing
#If DEBUG Then
        If _gateUtility.HasValue AndAlso _gateUtility.Value.Technique = t AndAlso _gateUtility.Value.CpuRoute = cpuRoute Then Return _gateUtility.Value.Program
#End If
        If _gbufferSource IsNot Nothing Then Return _gbufferSource.UtilityProgram(t, errorText, cpuRoute)
        Dim vs = Fo4UtilitySource.VertexShaderId(t)
        Dim nullPs = Fo4UtilitySource.PixelShaderNull(t)
        Dim key = (vs, Fo4UtilitySource.PixelShaderId(t), nullPs)
        Dim route = If(Fo4UtilitySource.EffectiveCpuLayout(vs, cpuRoute) <> Fo4CpuRouteLayout.None, _utilityCpu, _utilityGpu)
        Dim p As Fo4_Deferred_Shader_Class = Nothing
        If route.Programs.TryGetValue(key, p) Then Return p
        If route.Errors.TryGetValue(key, errorText) Then Return Nothing
        Try
            p = Build(Fo4UtilitySource.VertexOf(vs, route Is _utilityCpu),
                      If(nullPs, Fo4UtilitySource.NullFragment(), Fo4UtilitySource.FragmentOf(key.Item2)))
        Catch ex As Exception
            errorText = ex.Message
            route.Errors(key) = errorText
            Return Nothing
        End Try
        route.Programs(key) = p
        Return p
    End Function

    ''' <summary>Ola 4: the image-space programs by record (the PS of a VS / PS program, or the CS), built on first use.</summary>
    Private ReadOnly _imageSpace As New Dictionary(Of UInteger, Fo4_Deferred_Shader_Class)

    ''' <summary>The image-space program of PS or CS record <paramref name="rec"/> (Fo4ImageSpaceSource.Programs: a PS with the VS of its
    ''' program, a CS alone), built on its first use; a declared gap throws (the post's chain uses none: its records are a closed
    ''' list, Fo4ImageSpaceChain).</summary>
    Friend Function ImageSpaceProgram(rec As UInteger) As Fo4_Deferred_Shader_Class
        If _gateImageSpace.HasValue AndAlso _gateImageSpace.Value.Rec = rec Then Return _gateImageSpace.Value.Program
        If _gbufferSource IsNot Nothing Then Return _gbufferSource.ImageSpaceProgram(rec)
        Dim p As Fo4_Deferred_Shader_Class = Nothing
        If _imageSpace.TryGetValue(rec, p) Then Return p
        Dim gap = Fo4ImageSpaceSource.GapOf(rec)
        If gap <> "" Then Throw New InvalidOperationException($"image-space record {rec}: {gap}")
        If Fo4ImageSpaceSource.ComputeStages.ContainsKey(rec) Then
            p = BuildCompute(Fo4ImageSpaceSource.ComputeOf(rec))
        Else
            Dim pr = Fo4ImageSpaceSource.Programs.First(Function(x) x.Ps = rec)
            p = Build(Fo4ImageSpaceSource.VertexOf(pr.Vs), Fo4ImageSpaceSource.FragmentOf(rec))
        End If
        _imageSpace(rec) = p
        Return p
    End Function

    ''' <summary>The program of the G-buffer technique <paramref name="t"/> on the vertex route of the draw (<paramref name="cpuRoute"/>,
    ''' RenderableMesh.EngineCpuRoute), built on its first use; Nothing when its build failed, with the error in
    ''' <paramref name="errorText"/> (remembered: it is not rebuilt).</summary>
    Friend Function GBufferProgram(t As Fo4Technique, ByRef errorText As String, Optional cpuRoute As Boolean = False) As Fo4_Deferred_Shader_Class
        errorText = Nothing
        Dim key = t.Key()
        Dim route = RouteOf(key.Vs, cpuRoute)
        Dim program As Fo4_Deferred_Shader_Class = Nothing
        If route.Records.TryGetValue(key, program) Then Return program
        If _gbufferSource IsNot Nothing Then Return _gbufferSource.GBufferProgram(t, errorText, cpuRoute)
        If route.Errors.TryGetValue(key, errorText) Then Return Nothing
        Try
            Dim st = Fo4GBufferSource.StageOf(key.Vs, key.Hs, key.Ds, route Is _cpuRoute)
            program = BuildStages(st, Fo4GBufferSource.FragmentOf(key.Ps))
            If Fo4GBufferSource.Records(key.Ps).EarlyZ Then route.Base(key) = BuildStages(st, Fo4GBufferSource.FragmentOf(key.Ps, earlyTests:=False))
            ' DIVERGENCE (ola 2 ensayo): the surface's law reads the VS layout (rows TEXCOORD0..2, uv TEXCOORD3.w / 4.w, rec2273
            ' L103-105); a tessellated tuple's DS writes another layout (uv TEXCOORD0.xy, 29 of 69 DS without TEXCOORD4): no surface.
            If st.Control Is Nothing Then route.Surface(key) = BuildStages(st, Fo4DeferredAppSource.Fragment_DecalBaseSurface)
        Catch ex As Exception
            errorText = ex.Message
            route.Errors(key) = errorText
            Return Nothing
        End Try
        route.Records(key) = program
        Return program
    End Function

    ''' <summary>The engine VS <paramref name="vs"/> on a vertex route, alone, its clip position (gl_Position, invariant in the app
    ''' foot) captured by transform feedback: what an overlay that shares its host's geometry draws its position with
    ''' (RenderableMesh.EnginePositions, user decision 12-oct-2026). Built on first use, one per VS and route; Nothing when the
    ''' stage does not build (the overlay then keeps its effect VS's own position, as its host is not drawn by an engine stage).</summary>
    Friend Function EnginePositionProgram(vs As UInteger, cpuRoute As Boolean) As Fo4_Deferred_Shader_Class
        If _gbufferSource IsNot Nothing Then Return _gbufferSource.EnginePositionProgram(vs, cpuRoute)
        Dim route = RouteOf(vs, cpuRoute)
        Dim p As Fo4_Deferred_Shader_Class = Nothing
        If route.Position.TryGetValue(vs, p) Then Return p
        Try
            p = New Fo4_Deferred_Shader_Class(Fo4GBufferSource.StageOf(vs, 0UI, 0UI, route Is _cpuRoute, positionCapture:=True).Vertex,
                                              Fo4DeferredAppSource.Fragment_PositionCapture, Nothing, Nothing, {"gl_Position"})
            _built.Add(p)
        Catch ex As Exception
            p = Nothing
        End Try
        route.Position(vs) = p
        Return p
    End Function

    ''' <summary>The engine stages of one tuple (VS, + TCS / TES when it tessellates, 0x14181D60E) and a fragment.</summary>
    Private Function BuildStages(st As (Vertex As String, Control As String, Evaluation As String), fragment As String) As Fo4_Deferred_Shader_Class
        Dim p As New Fo4_Deferred_Shader_Class(st.Vertex, fragment, st.Control, st.Evaluation)
        _built.Add(p)
        Return p
    End Function

    ''' <summary>Ola 3: the effect programs of one vertex route (the CPU route only for a skinned VS, Fo4EffectSource.EffectiveCpuLayout):
    ''' technique key (VS, PS) -&gt; the program, its build error; VS -&gt; the decals' base surface program (the same VS with the
    ''' surface foot, Fragment_DecalBaseSurfaceEffect).</summary>
    Private NotInheritable Class EffectRoute
        Friend ReadOnly Programs As New Dictionary(Of (Vs As UInteger, Ps As UInteger), Fo4_Deferred_Shader_Class)
        Friend ReadOnly Errors As New Dictionary(Of (Vs As UInteger, Ps As UInteger), String)
        Friend ReadOnly Surface As New Dictionary(Of UInteger, Fo4_Deferred_Shader_Class)
    End Class
    Private ReadOnly _effectGpu As New EffectRoute, _effectCpu As New EffectRoute
    Private Function EffectRouteOf(vs As UInteger, cpuRoute As Boolean) As EffectRoute
        Return If(Fo4EffectSource.EffectiveCpuLayout(vs, cpuRoute) <> Fo4CpuRouteLayout.None, _effectCpu, _effectGpu)
    End Function

    ''' <summary>The program of the effect technique <paramref name="t"/> on the draw's vertex route (RenderableMesh.EngineCpuRoute):
    ''' Fo4EffectSource.VertexOf + FragmentOf, built on its first use (user decision 13-oct-2026); Nothing when its build failed,
    ''' with the error in <paramref name="errorText"/> (remembered: not rebuilt).</summary>
    Friend Function EffectProgram(t As Fo4EffectTech, ByRef errorText As String, Optional cpuRoute As Boolean = False) As Fo4_Deferred_Shader_Class
        errorText = Nothing
        Dim key = t.Key()
        Dim route = EffectRouteOf(key.Vs, cpuRoute)
        Dim p As Fo4_Deferred_Shader_Class = Nothing
        If route.Programs.TryGetValue(key, p) Then Return p
        If _gbufferSource IsNot Nothing AndAlso _gateEffectVertex Is Nothing Then Return _gbufferSource.EffectProgram(t, errorText, cpuRoute)
        If route.Errors.TryGetValue(key, errorText) Then Return Nothing
        Try
            Dim vertex = Fo4EffectSource.VertexOf(key.Vs, route Is _effectCpu)
            If _gateEffectVertex IsNot Nothing Then vertex = _gateEffectVertex(vertex)
            p = Build(vertex, Fo4EffectSource.FragmentOf(key.Ps))
        Catch ex As Exception
            errorText = ex.Message
            route.Errors(key) = errorText
            Return Nothing
        End Try
        route.Programs(key) = p
        Return p
    End Function

    ''' <summary>The translucent decals' base surface program of an effect technique (D16; ola 3, Q2): its engine VS with the surface
    ''' foot + Fragment_DecalBaseSurfaceEffect, one per VS and route, built on first use; Nothing when it does not build. A copy made
    ''' by GateWithEffectSurfaceFragment builds its own with the gate's fragment.</summary>
    Friend Function EffectSurfaceProgram(t As Fo4EffectTech, Optional cpuRoute As Boolean = False) As Fo4_Deferred_Shader_Class
        Dim vs = t.Key().Vs
        Dim route = EffectRouteOf(vs, cpuRoute)
        Dim p As Fo4_Deferred_Shader_Class = Nothing
        If route.Surface.TryGetValue(vs, p) Then Return p
        If _gbufferSource IsNot Nothing AndAlso _gateEffectSurfaceFragment Is Nothing Then Return _gbufferSource.EffectSurfaceProgram(t, cpuRoute)
        Try
            p = Build(Fo4EffectSource.VertexOf(vs, route Is _effectCpu, surface:=True), If(_gateEffectSurfaceFragment, Fo4DeferredAppSource.Fragment_DecalBaseSurfaceEffect))
        Catch ex As Exception
            p = Nothing
        End Try
        route.Surface(vs) = p
        Return p
    End Function

    ''' <summary>The effect programs built so far on a vertex route (ShadowGate's program census).</summary>
    Friend Function BuiltEffectPrograms(Optional cpuRoute As Boolean = False) As IReadOnlyDictionary(Of (Vs As UInteger, Ps As UInteger), Fo4_Deferred_Shader_Class)
        Return If(cpuRoute, _effectCpu, _effectGpu).Programs
    End Function

    ''' <summary>The effect decals' base surface programs built so far on a vertex route, by VS (ShadowGate's program census).</summary>
    Friend Function BuiltEffectSurfacePrograms(Optional cpuRoute As Boolean = False) As IReadOnlyDictionary(Of UInteger, Fo4_Deferred_Shader_Class)
        Return If(cpuRoute, _effectCpu, _effectGpu).Surface
    End Function

    ''' <summary>GATE ONLY: a copy of these programs (GateWith) whose effect decals' base surface is drawn with <paramref name="fragment"/>,
    ''' built per VS on first use by the copy, which owns those (its Dispose releases them and nothing else).</summary>
    Friend Function GateWithEffectSurfaceFragment(fragment As String) As Fo4DeferredPrograms
        Dim c = New Fo4DeferredPrograms(Me, "DecalBase", DecalBase)
        c._gateEffectSurfaceFragment = fragment
        Return c
    End Function
    Private _gateEffectSurfaceFragment As String

    ''' <summary>GATE ONLY: a copy of these programs (GateWith) whose effect programs are built with their vertex text passed through
    ''' <paramref name="vertex"/> (the gate declares the text it makes, Gl43.GateHarnessText), per technique on first use by the copy,
    ''' which owns those (its Dispose releases them and nothing else).</summary>
    Friend Function GateWithEffectVertex(vertex As Func(Of String, String)) As Fo4DeferredPrograms
        Dim c = New Fo4DeferredPrograms(Me, "DecalBase", DecalBase)
        c._gateEffectVertex = vertex
        Return c
    End Function
    Private _gateEffectVertex As Func(Of String, String)

    ''' <summary>The decals' base surface program of technique <paramref name="t"/> on the draw's vertex route (built with its record by
    ''' GBufferProgram).</summary>
    Friend Function DecalBaseSurfaceProgram(t As Fo4Technique, Optional cpuRoute As Boolean = False) As Fo4_Deferred_Shader_Class
        Dim e As String = Nothing
        If GBufferProgram(t, e, cpuRoute) Is Nothing Then Return Nothing
        If _gbufferSource IsNot Nothing Then Return If(_gateSurface, _gbufferSource.DecalBaseSurfaceProgram(t, cpuRoute))
        Dim p As Fo4_Deferred_Shader_Class = Nothing
        Return If(RouteOf(t.Key().Vs, cpuRoute).Surface.TryGetValue(t.Key(), p), p, Nothing)
    End Function

    ''' <summary>The record programs built so far on a vertex route (ShadowGate's program census).</summary>
    Friend Function BuiltGBufferPrograms(Optional cpuRoute As Boolean = False) As IReadOnlyDictionary(Of (Vs As UInteger, Hs As UInteger, Ds As UInteger, Ps As UInteger), Fo4_Deferred_Shader_Class)
        Return If(cpuRoute, _cpuRoute, _gpuRoute).Records
    End Function

    ''' <summary>The base-pass variants built so far on a vertex route (ShadowGate's program census).</summary>
    Friend Function BuiltDecalBasePrograms(Optional cpuRoute As Boolean = False) As IReadOnlyDictionary(Of (Vs As UInteger, Hs As UInteger, Ds As UInteger, Ps As UInteger), Fo4_Deferred_Shader_Class)
        Return If(cpuRoute, _cpuRoute, _gpuRoute).Base
    End Function

    ''' <summary>The decals' base surface programs built so far on a vertex route (ShadowGate's program census).</summary>
    Friend Function BuiltDecalSurfacePrograms(Optional cpuRoute As Boolean = False) As IReadOnlyDictionary(Of (Vs As UInteger, Hs As UInteger, Ds As UInteger, Ps As UInteger), Fo4_Deferred_Shader_Class)
        Return If(cpuRoute, _cpuRoute, _gpuRoute).Surface
    End Function

    ''' <summary>The program the translucent decals' base pass draws record <paramref name="psId"/> with (RenderableMesh.Render
    ''' decalBase): the record's own program - its kill decides which fragments give a base, as it decides which ones paint -
    ''' except for a record with forceEarlyDepthStencil (43 of the 143 records with technique bit 15), whose early depth write would
    ''' keep the depth of the pixels its PS kills (GL 4.3 14.9: with early tests the depth buffer is updated before the shader, a
    ''' later discard does not undo it): for those, the same record text without the early tests (Fo4GBufferSource.FragmentOf
    ''' earlyTests:=False), built with the record (GBufferProgram). Nothing when the record's build failed (it is then a gap).</summary>
    Friend Function DecalBaseProgram(t As Fo4Technique, Optional cpuRoute As Boolean = False) As Fo4_Deferred_Shader_Class
        Dim errorText As String = Nothing
        Dim program = GBufferProgram(t, errorText, cpuRoute)
        If program Is Nothing OrElse Not Fo4GBufferSource.Records(t.Key().Ps).EarlyZ Then Return program
        If _gbufferSource IsNot Nothing Then Return _gbufferSource.DecalBaseProgram(t, cpuRoute)
        Return RouteOf(t.Key().Vs, cpuRoute).Base(t.Key())
    End Function

    ''' <summary>GATE ONLY: on a GateWith copy, record <paramref name="ps"/>'s fragment replaced in EVERY tuple of the law that draws it,
    ''' on both vertex routes (each built with its own engine stages); Nothing = those tuples have no program. Returns the programs
    ''' built (the caller disposes them: the copy owns nothing).</summary>
    Friend Function GateSetGBufferFragment(ps As Fo4PsId, fragment As String) As List(Of Fo4_Deferred_Shader_Class)
        If _gbufferSource Is Nothing Then Throw New InvalidOperationException("GateSetGBufferFragment: only on a GateWith copy")
        Dim built As New List(Of Fo4_Deferred_Shader_Class)
        For Each tv In Fo4GBufferSource.LawTechniquesOfPs(ps.Value)
            Dim key = New Fo4Technique(tv).Key()
            For Each cpu In {False, True}
                Dim route = RouteOf(key.Vs, cpu)
                If cpu AndAlso route Is _gpuRoute Then Continue For
                If fragment Is Nothing Then
                    route.Records(key) = Nothing
                Else
                    Dim st = Fo4GBufferSource.StageOf(key.Vs, key.Hs, key.Ds, cpu)
                    Dim p As New Fo4_Deferred_Shader_Class(st.Vertex, fragment, st.Control, st.Evaluation)
                    built.Add(p) : route.Records(key) = p
                End If
            Next
        Next
        Return built
    End Function

    ''' <summary>GATE ONLY: on a GateWith copy, the decals' base surface drawn with <paramref name="program"/> for every technique.</summary>
    Friend Sub GateSetSurfaceProgram(program As Fo4_Deferred_Shader_Class)
        _gateSurface = program
    End Sub
    Private _gateSurface As Fo4_Deferred_Shader_Class

    ''' <summary>GATE ONLY (ShadowGate --fo4-deferred-law mutants): a copy of these programs with the one of role
    ''' <paramref name="role"/> (DirectResolve, DecalBase, PrepassRigid, PrepassAlpha) replaced (the light and composite programs:
    ''' GateWithLight / GateWithComposite; the effect decals' base surface: GateWithEffectSurfaceFragment). The closed list of roles is
    ''' checked in Debug only (rev-C4: a gate's own data, not a Release runtime check).
    ''' The copy owns nothing: its Dispose releases no program.</summary>
    Friend Function GateWith(role As String, program As Fo4_Deferred_Shader_Class) As Fo4DeferredPrograms
        Return New Fo4DeferredPrograms(Me, role, program)
    End Function

    ''' <summary>GATE ONLY: a copy of these programs whose light technique <paramref name="technique"/> draws with
    ''' <paramref name="program"/> (the copy owns nothing).</summary>
    Friend Function GateWithLight(technique As UInteger, program As Fo4_Deferred_Shader_Class) As Fo4DeferredPrograms
        Dim c = New Fo4DeferredPrograms(Me, "DecalBase", DecalBase)
        c._gateLight = (technique, program)
        Return c
    End Function
    Private _gateLight As (Technique As UInteger, Program As Fo4_Deferred_Shader_Class)?

    ''' <summary>GATE ONLY: a copy of these programs whose composite technique <paramref name="technique"/> draws with
    ''' <paramref name="program"/> (the copy owns nothing).</summary>
    Friend Function GateWithComposite(technique As UInteger, program As Fo4_Deferred_Shader_Class) As Fo4DeferredPrograms
        Dim c = New Fo4DeferredPrograms(Me, "DecalBase", DecalBase)
        c._gateComposite = (technique, program)
        Return c
    End Function
    Private _gateComposite As (Technique As UInteger, Program As Fo4_Deferred_Shader_Class)?

    ''' <summary>GATE ONLY: a copy of these programs whose image-space record <paramref name="rec"/> (a PS of the post's chain or the CS
    ''' 3860) draws with <paramref name="program"/> (ShadowGate --fo4-post-law / --fo4-camz: the probed program); the copy owns nothing.</summary>
    Friend Function GateWithImageSpace(rec As UInteger, program As Fo4_Deferred_Shader_Class) As Fo4DeferredPrograms
        Dim c = New Fo4DeferredPrograms(Me, "DecalBase", DecalBase)
        c._gateImageSpace = (rec, program)
        Return c
    End Function
    Private _gateImageSpace As (Rec As UInteger, Program As Fo4_Deferred_Shader_Class)?

#If DEBUG Then
    ''' <summary>GATE ONLY: a copy of these programs whose utility technique <paramref name="technique"/> on the route
    ''' <paramref name="cpuRoute"/> draws with <paramref name="program"/> (ShadowGate --fo4-refraction-normals: the probed b07 program);
    ''' the copy owns nothing.</summary>
    Friend Function GateWithUtility(technique As UInteger, cpuRoute As Boolean, program As Fo4_Deferred_Shader_Class) As Fo4DeferredPrograms
        Dim c = New Fo4DeferredPrograms(Me, "DecalBase", DecalBase)
        c._gateUtility = (technique, cpuRoute, program)
        Return c
    End Function
#End If
    Private _gateUtility As (Technique As UInteger, CpuRoute As Boolean, Program As Fo4_Deferred_Shader_Class)?

    Private Sub New(src As Fo4DeferredPrograms, role As String, program As Fo4_Deferred_Shader_Class)
#If DEBUG Then
        If Not {"DirectResolve", "DecalBase", "PrepassRigid", "PrepassAlpha"}.Contains(role) Then
            Throw New ArgumentException("no deferred program role " & role)
        End If
#End If
        DirectResolve = If(role = "DirectResolve", program, src.DirectResolve)
        DecalBase = If(role = "DecalBase", program, src.DecalBase)
        PrepassRigid = If(role = "PrepassRigid", program, src.PrepassRigid)
        PrepassAlpha = If(role = "PrepassAlpha", program, src.PrepassAlpha)
        _gbufferSource = src
        _gateLight = src._gateLight
        _gateComposite = src._gateComposite
        _gateImageSpace = src._gateImageSpace
        _gateUtility = src._gateUtility
    End Sub

    Friend Function All() As IEnumerable(Of Shader_Base_Class)
        Return _built
    End Function

    Friend Sub Dispose()
        For Each p In All() : p.Dispose() : Next
    End Sub
End Class

''' <summary>One rig light as the deferred light pass draws it (Fo4DeferredLightLaw: every rig light is a directional light on the
''' sun's program).</summary>
Friend Structure Fo4DeferredLight
    ''' <summary>LightVector.xyz: the engine-view direction TO the light (-(NiLight + 0x170..0x178) in view, 0x14224766B..6F9).</summary>
    Public Dir As Vector3
    ''' <summary>LightColor.rgb, linear (the rig's diffuse: pow 2.2 and the dimmer already in it, PreviewLightRig).</summary>
    Public Color As Vector3
    ''' <summary>The layer of the frame's shadow map this light draws (ShadowMapMath.SlotsDeSombra), -1 when it casts none.</summary>
    Public ShadowLayer As Integer
    ''' <summary>The light's map framing (ShadowMapMath.Fit; Valid only with a layer).</summary>
    Public ShadowFit As ShadowMapMath.LightFit
End Structure

''' <summary>What the light and composite passes of one frame receive (built by PreviewModel.BuildFo4DeferredFrame from the frame's
''' camera, its cb12 rows and the rig).</summary>
Friend Structure Fo4DeferredFrame
    Public W As Integer, H As Integer
    Public Near As Single, Far As Single
    ''' <summary>The frame's cb12 rows (Fo4GBufferConstants.EngineFrameRows: inverse view 12..15, inverse projection 20..23, eye 35,
    ''' fog 41..46) and the first-person inverse projection 24..27 (InverseProjectionRows 100 z, notas D9).</summary>
    Public Rows As Fo4FrameRows
    Public InvProj1P As Vector4()
    ''' <summary>The camera's GL view and eye (Fo4DeferredLightLaw.ShadowMapProjRows composes them with each light's map).</summary>
    Public View As Matrix4, Eye As Vector3
    ''' <summary>The rig's lights in ShadowMapMath.LuzDelRig order (key, fill left, fill right, back).</summary>
    Public Lights As Fo4DeferredLight()
    ''' <summary>DirectionalAmbient rows 0..2 (propuesta D; the rig's ambient). Row 3, which the writer copies too (0x142247199..1A4),
    ''' is read by no light PS (rec3107 / 3109 / 3232 read cb2[6..8]).</summary>
    Public Ambient As Vector4()
    ''' <summary>The frame's shadow map array (ParentControl.ShadowTarget) when a light casts; Nothing otherwise.</summary>
    Public ShadowMap As ShadowMapTarget
    ''' <summary>The composite's sun (SetupGeometry 0x1422174F0 with FOG, 0x14221790F): the rig's key, as the deferred frame's sun
    ''' (user decision rev-23 b) - SunDirection.xyz = the key's LightVector.xyz, SunColor.rgb = its colour.</summary>
    Public SunDir As Vector3, SunColor As Vector3
    ''' <summary>The post runs (the composite's pass 2 technique, Fo4DeferredLightLaw.CompositePass2).</summary>
    Public PostOn As Boolean
    ''' <summary>APP, the rig's shadow look on the engine receiver (rev-E08, user decision 14-oct-2026): AppShadowLift of the sun's PS
    ''' (Fo4DeferredLightLaw.RigShadowLift: 1 - Darkness; 0 = the game) and the growth of its Poisson radius, its scale minus 1
    ''' (Fo4DeferredLightLaw.RigShadowRadiusScale - 1; 0 = the game). Both are 0 for the game, so a frame built without them (a gate's
    ''' oracle frame) draws the game's receiver.</summary>
    Public ShadowLift As Single, ShadowRadiusGrowth As Single
End Structure

''' <summary>THE TARGETS AND THE NON-G-BUFFER PASSES OF THE FO4 DEFERRED FRAME (Tools/re-docs/RE_FO4_DEFERRED_FRAME_2026-10-03.md 1, 2,
''' 3, 4, 15, 17; ola 4: D:\WinTmp\rc\ola4\re.md 1.3, 1.4, 2.3): the G-buffer RTs 0x1A/0x1B/0x1D/0x1E/0x1F, the D3D-order
''' depth/stencil, the light RTs 0x21/0x22, RT 2, RT 0x28 with its mips (t15, CameraZAndMipsCS 3860). The whole frame up to the end
''' of the forward stage is in D3D row order (glClipControl UPPER_LEFT, propuesta A; composite 2 and the forward stage too since the
''' zfight-overlay fix, 8-oct-2026: one orientation for everything that tests against the G-buffer depth, as in the game), and
''' MirrorRows turns the frame's targets to GL rows at the end of that stage (PreviewControl.EndFo4Stage).</summary>
Friend NotInheritable Class Fo4DeferredTargets
    Private _w As Integer, _h As Integer
    Private _albedo, _normal, _env, _gloss, _emit, _depth, _depthCopy, _lightD, _lightS, _rt2, _linZ As Integer
    ''' <summary>RT 0x28's mip count (LinearZLevels; RT 0x29 + i is its mip i).</summary>
    Private _linZLevels As Integer
    Private _fboG, _fboL, _fboC1 As Integer
    ''' <summary>MirrorRows: one scratch image per format it turns (R11F_G11F_B10F, RGBA8, D24S8) and its read / draw framebuffers.</summary>
    Private _mirR11, _mirRgba8, _mirDs, _fboMirSrc, _fboMirDst As Integer
    ''' <summary>The translucent decals' base (BeginDecalBase / EndDecalBase), with its alpha and coverage targets.</summary>
    Private ReadOnly _base As New DecalBaseTarget(deferred:=True)
    ''' <summary>This frame's G-buffer stage drew the base (BeginDecalBase succeeded since BeginGBuffer): composite 2 takes the coverage
    ''' of its pixels from it and the direct resolve mixes by the coverage.</summary>
    Private _baseDrawn As Boolean

    ''' <summary>The G-buffer's colour targets (RT 0x1A, 0x1B, 0x1D, 0x1E, 0x1F, 0x1421F30B0).</summary>
    Friend Const GBufferTargetCount As Integer = 5
    ''' <summary>The draw buffer of the decals' base surface coverage: DecalBaseTarget.Begin attaches the coverage target after the
    ''' caller's colours (BeginDecalBase: the GBufferTargetCount G-buffer RTs), and Fragment_DecalBaseSurface writes oCoverage at
    ''' Fo4DeferredAppSource.SurfaceCoverageLocationGlsl - the one number, read here; ShadowGate --decal-base-surface checks it is
    ''' GBufferTargetCount (a gate's check of closed data, not a Release one).</summary>
    Friend Shared ReadOnly SurfaceCoverageBuffer As Integer = Integer.Parse(Fo4DeferredAppSource.SurfaceCoverageLocationGlsl, Globalization.CultureInfo.InvariantCulture)

    ''' <summary>The translucent decals' base target (the base draws bind its framebuffers; gates read its textures).</summary>
    Friend ReadOnly Property DecalBase As DecalBaseTarget
        Get
            Return _base
        End Get
    End Property
    Private _quadVao, _quadVbo, _triVao, _triVbo, _emptyVao As Integer
    Private _point, _cubeSampler, _shadowCompare, _aoSampler As Integer
    ''' <summary>The 1x1 inputs the composite's pass 2 reads where the preview runs no producer (Fo4DeferredTargets.RunComposite2):
    ''' t9 = 1 (no AO: decision Q-AO open, the frame as before), t14 = 0 (no SSR: the SSLR chain's owner is not traced, re.md H-9;
    ''' RE_FO4_DEFERRED_FRAME 17.5).</summary>
    Private _noAo, _noSsr As Integer
    Private Shared ReadOnly GBufferBuffers As DrawBuffersEnum() = {DrawBuffersEnum.ColorAttachment0, DrawBuffersEnum.ColorAttachment1,
        DrawBuffersEnum.ColorAttachment2, DrawBuffersEnum.ColorAttachment3, DrawBuffersEnum.ColorAttachment4}
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
                                              Depth As Integer, LightD As Integer, LightS As Integer, Rt2 As Integer,
                                              LinZ As Integer, DecalBase As Integer)
        Get
            Return (_albedo, _normal, _env, _gloss, _emit, _depth, _lightD, _lightS, _rt2, _linZ, _base.AuxTexture)
        End Get
    End Property

    ''' <summary>GATE ONLY: the samplers of the deferred passes (ShadowGate --fo4-light-law / --fo4-deferred-law: their states against
    ''' the exe's).</summary>
    Friend ReadOnly Property GateSamplers As (Point As Integer, Cube As Integer, ShadowCompare As Integer, Ao As Integer)
        Get
            Return (_point, _cubeSampler, _shadowCompare, _aoSampler)
        End Get
    End Property

    Public ReadOnly Property GBufferFramebuffer As Integer
        Get
            Return _fboG
        End Get
    End Property

    Private Shared Function Tex(fmt As SizedInternalFormat, w As Integer, h As Integer, Optional levels As Integer = 1) As Integer
        Dim t As Integer
        t = Gl43.CreateTexture(TextureTarget.Texture2D)
        Gl43.TexStorage2D(t, TextureTarget2d.Texture2D, levels, fmt, w, h)
        Return t
    End Function

    ''' <summary>A 1x1 RGBA8 texture of one colour.</summary>
    Private Shared Function Texel(rgba As Byte()) As Integer
        Dim t = Tex(SizedInternalFormat.Rgba8, 1, 1)
        Gl43.TexSubImage2D(t, TextureTarget.Texture2D, 0, 0, 0, 1, 1, PixelFormat.Rgba, PixelType.UnsignedByte, rgba)
        Return t
    End Function

    ''' <summary>RT 0x28's mip count, the engine's integer law (0x142234734..76E): m = min(W, H) (unsigned, cmovbe); from 1, a level per
    ''' rol-doubling of a power of two not above m (none for m = 0) - floor(log2 m) + 1 -, then at most 5 (cmova).</summary>
    Friend Shared Function LinearZLevels(w As Integer, h As Integer) As Integer
        Dim m = Math.Min(CUInt(w), CUInt(h))
        Dim n = 0
        If m >= 1UI Then
            Dim p = 1UI
            Do
                n += 1
                p = Numerics.BitOperations.RotateLeft(p, 1)
            Loop While m >= p
        End If
        Return Math.Min(n, 5)
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
        _depth = Tex(SizedInternalFormat.Depth24Stencil8, w, h)     ' DS 1 D24_UNORM_S8_UINT
        _depthCopy = Tex(SizedInternalFormat.Depth24Stencil8, w, h)
        Gl43.TexParameter(_depthCopy, TextureTarget.Texture2D, TextureParameterName.DepthStencilTextureMode, CInt(All.DepthComponent))
        _lightD = Tex(SizedInternalFormat.R11fG11fB10f, w, h)       ' RT 0x21
        _lightS = Tex(SizedInternalFormat.R11fG11fB10f, w, h)       ' RT 0x22 (RGBSPEC = 1, rev-24)
        _rt2 = Tex(SizedInternalFormat.R11fG11fB10f, w, h)          ' RT 2
        ' RT 0x28 R32_FLOAT with mips (flags 0x10100, 0x142234703..72F), LinearZLevels of them.
        _linZLevels = LinearZLevels(w, h)
        _linZ = Tex(SizedInternalFormat.R32f, w, h, _linZLevels)
        ' DECISION (declared, not a law; ola 4 v2): with W or H odd the CS 3860 never writes the last column / row of a level (its
        ' dispatch (W >> 1) + 7 >> 3 threads of 2 x 2), and the game's RT keeps there what it held since it was created - D3D11 leaves a
        ' resource created without initial data undefined. The app gives those texels a defined value instead: every level is
        ' cleared to 0 once, at creation; the CS never writes them, so they keep it for the life of the target.
        Dim fboZ = Gl43.CreateFramebuffer()
        GL.ColorMask(0, True, True, True, True)                    ' a clear honours the colour mask (GL 4.3 17.4.3)
        For lvl = 0 To _linZLevels - 1
            Gl43.FramebufferTexture(fboZ, FramebufferAttachment.ColorAttachment0, _linZ, lvl)
            Gl43.ClearFramebuffer(fboZ, ClearBuffer.Color, 0, New Single() {0.0F, 0.0F, 0.0F, 0.0F})
        Next
        GL.DeleteFramebuffer(fboZ)
        Dim ok = True
        _fboG = Gl43.CreateFramebuffer()
        For i = 0 To GBufferTargetCount - 1
            Gl43.FramebufferTexture(_fboG, CType(FramebufferAttachment.ColorAttachment0 + i, FramebufferAttachment),
                                       {_albedo, _normal, _env, _gloss, _emit}(i), 0)
        Next
        Gl43.FramebufferTexture(_fboG, FramebufferAttachment.DepthStencilAttachment, _depth, 0)
        ok = ok AndAlso Gl43.CheckFramebufferStatus(_fboG, FramebufferTarget.Framebuffer) = FramebufferStatus.FramebufferComplete
        _fboL = Gl43.CreateFramebuffer()
        Gl43.FramebufferTexture(_fboL, FramebufferAttachment.ColorAttachment0, _lightD, 0)
        Gl43.FramebufferTexture(_fboL, FramebufferAttachment.ColorAttachment1, _lightS, 0)
        Gl43.FramebufferTexture(_fboL, FramebufferAttachment.DepthStencilAttachment, _depth, 0)
        ok = ok AndAlso Gl43.CheckFramebufferStatus(_fboL, FramebufferTarget.Framebuffer) = FramebufferStatus.FramebufferComplete
        _fboC1 = Gl43.CreateFramebuffer()
        Gl43.FramebufferTexture(_fboC1, FramebufferAttachment.ColorAttachment0, _rt2, 0)
        Gl43.FramebufferTexture(_fboC1, FramebufferAttachment.DepthStencilAttachment, _depth, 0)
        ok = ok AndAlso Gl43.CheckFramebufferStatus(_fboC1, FramebufferTarget.Framebuffer) = FramebufferStatus.FramebufferComplete
        ok = ok AndAlso _base.Ensure(w, h)
        _mirR11 = Tex(SizedInternalFormat.R11fG11fB10f, w, h)
        _mirRgba8 = Tex(SizedInternalFormat.Rgba8, w, h)
        _mirDs = Tex(SizedInternalFormat.Depth24Stencil8, w, h)
        _fboMirSrc = Gl43.CreateFramebuffer()
        _fboMirDst = Gl43.CreateFramebuffer()
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
        Gl43.SamplerAnisotropy(_cubeSampler, 16.0F)
        ' s5 of the sun with shadow: ADDR 0 = CLAMP, FILT 4 (0x142248363..387) = Filter 0x94 COMPARISON_MIN_MAG_LINEAR_MIP_POINT,
        ' ComparisonFunc 2 = LESS (0x1418564A6..4B7), MaxLOD FLT_MAX: on the one-level map, LINEAR_MIPMAP_NEAREST / LINEAR, compare
        ' ref LESS texel (GL 4.3 8.22.1: r = 1 when ref &lt; D_t, the D3D11 comparison of the source against the texel).
        _shadowCompare = GL.GenSampler()
        For Each p In {SamplerParameterName.TextureWrapS, SamplerParameterName.TextureWrapT}
            GL.SamplerParameter(_shadowCompare, p, CInt(TextureWrapMode.ClampToEdge))
        Next
        GL.SamplerParameter(_shadowCompare, SamplerParameterName.TextureMinFilter, CInt(TextureMinFilter.LinearMipmapNearest))
        GL.SamplerParameter(_shadowCompare, SamplerParameterName.TextureMagFilter, CInt(TextureMagFilter.Linear))
        GL.SamplerParameter(_shadowCompare, SamplerParameterName.TextureCompareMode, CInt(TextureCompareMode.CompareRefToTexture))
        GL.SamplerParameter(_shadowCompare, SamplerParameterName.TextureCompareFunc, CInt(All.Less))
#If DEBUG Then
        ' ShadowGate --fo4-light-law's mutant (Fo4GBufferConstants.GateMutantOn, the one switch): the comparison LESS_EQUAL.
        If Fo4GBufferConstants.GateMutantOn("dfl-cmp-lequal") Then GL.SamplerParameter(_shadowCompare, SamplerParameterName.TextureCompareFunc, CInt(All.Lequal))
#End If
        ' t9 of the composite: ADDR 0 (CLAMP), FILT 1 (bilinear, one mip) (0x1422173FC..423).
        _aoSampler = GL.GenSampler()
        For Each p In {SamplerParameterName.TextureWrapS, SamplerParameterName.TextureWrapT}
            GL.SamplerParameter(_aoSampler, p, CInt(TextureWrapMode.ClampToEdge))
        Next
        GL.SamplerParameter(_aoSampler, SamplerParameterName.TextureMinFilter, CInt(TextureMinFilter.LinearMipmapNearest))
        GL.SamplerParameter(_aoSampler, SamplerParameterName.TextureMagFilter, CInt(TextureMagFilter.Linear))
        _noAo = Texel({255, 255, 255, 255})
        _noSsr = Texel({0, 0, 0, 0})
        If Not ok Then Free() : Return False
        _w = w : _h = h
        Return True
    End Function

    ''' <summary>Opens the G-buffer stage: D3D row order (UPPER_LEFT; glFrontFace unchanged, propuesta A.2), every RT cleared (rev-29 b:
    ''' 0), depth 1 / stencil 0 (0x1421F3151..0x1421F3164), sRGB writes on (rev-17).</summary>
    Public Sub BeginGBuffer()
        Gl43.ClipControl(ClipOrigin.UpperLeft)
        If GateNoUpperLeft Then Gl43.ClipControl(ClipOrigin.LowerLeft)
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _fboG)
        GL.Viewport(0, 0, _w, _h)
        GL.ColorMask(True, True, True, True)
        GL.DepthMask(True)
        Gl43.DrawBuffers(_fboG, GBufferTargetCount, GBufferBuffers)
        For i = 0 To GBufferTargetCount - 1
            Gl43.ClearFramebuffer(_fboG, ClearBuffer.Color, i, New Single() {0.0F, 0.0F, 0.0F, 0.0F})
        Next
        Gl43.ClearFramebuffer(_fboG, ClearBufferCombined.DepthStencil, 0, 1.0F, 0)
        _baseDrawn = False
        GL.Enable(EnableCap.FramebufferSrgb)
        If GateNoGBufferSrgb Then GL.Disable(EnableCap.FramebufferSrgb)
    End Sub

    ''' <summary>Closes the G-buffer stage: sRGB writes off, the depth copy t3/t7 (feedback loop, propuesta A.3), RT 0x28 (t15) by
    ''' CameraZAndMipsCS (RunCameraZ).</summary>
    Public Sub EndGBuffer(progs As Fo4DeferredPrograms, near As Single, far As Single)
        GL.Disable(EnableCap.FramebufferSrgb)
        CopyDepth()
        RunCameraZ(progs, near, far)
    End Sub

    ''' <summary>RT 0x28 and its mips 1, 2 (the composite's t15, DF 17.3): CameraZAndMipsCS 3860 (slot 0 of the SAO CS, its render
    ''' 0x14220D540; RE_SAO_BOTH 2.4, 2.5, 5.7, 5.8): t0 = the depth (DS 1, POINT, LOD 0), u0 / u1 / u2 = RT 0x29 / 0x2A / 0x2B = mips
    ''' 0 / 1 / 2 of RT 0x28 (a mip the target does not have, n &lt; 3, is no image: its writes are dropped, as a null UAV's), CB0[0] =
    ''' g_ClipInfos = (n f, n - f, f, 0) from the camera's near / far (+0xF0..+0xFC, 0x14220D18A..192), dispatch
    ''' ((W &gt;&gt; 1) + 7) &gt;&gt; 3 x ((H &gt;&gt; 1) + 7) &gt;&gt; 3 (0x14220D10F..112, 0x14220D164..1A7; W' = ceil(W sx), sx = 1). It runs in
    ''' every frame: RT 0x28 is the composite's input whatever the post does (the SAO's own passes, slots 1..9, are decision Q-AO).</summary>
    Private Sub RunCameraZ(progs As Fo4DeferredPrograms, near As Single, far As Single)
        Dim p = progs.ImageSpaceProgram(3860UI)
        p.Use()
        p.SetVector4("g_ClipInfos", New Vector4(near * far, near - far, far, 0.0F))
#If DEBUG Then
        ' ShadowGate --fo4-camz's mutants (Fo4GBufferConstants.GateMutantOn, the one switch).
        If Fo4GBufferConstants.GateMutantOn("camz-clipinfos") Then p.SetVector4("g_ClipInfos", New Vector4(near - far, near * far, far, 0.0F))
#End If
        Gl43.BindTextureUnit(0, TextureTarget.Texture2D, _depthCopy) : GL.BindSampler(0, _point)
        For lvl = 0 To 2
            If lvl < _linZLevels Then
                GL.BindImageTexture(lvl, _linZ, lvl, False, 0, TextureAccess.WriteOnly, SizedInternalFormat.R32f)
            Else
                GL.BindImageTexture(lvl, 0, 0, False, 0, TextureAccess.WriteOnly, SizedInternalFormat.R32f)
            End If
        Next
        Dim gx = ((_w >> 1) + 7) >> 3, gy = ((_h >> 1) + 7) >> 3
#If DEBUG Then
        If Fo4GBufferConstants.GateMutantOn("camz-dispatch-w") Then gx = (_w + 7) >> 3 : gy = (_h + 7) >> 3
#End If
        GL.DispatchCompute(gx, gy, 1)
        GL.MemoryBarrier(MemoryBarrierFlags.TextureFetchBarrierBit)
        For lvl = 0 To 2 : GL.BindImageTexture(lvl, 0, 0, False, 0, TextureAccess.WriteOnly, SizedInternalFormat.R32f) : Next
        Gl43.UnbindTextureUnit(0) : GL.BindSampler(0, 0)
        GL.UseProgram(0)
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
    ''' (no depth of a decal goes through a float, user order) with the five G-buffer RTs as the base surface's targets
    ''' (Fo4DeferredAppSource.Fragment_DecalBaseSurface); the mark reads this target's depth copy (_depthCopy: the copy the lights
    ''' and composites sample, taken again by EndGBuffer - the frame keeps one sampled D24S8 copy, review rev-03). False when that
    ''' target is incomplete: nothing bound, no base.</summary>
    Public Function BeginDecalBase(progs As Fo4DeferredPrograms) As Boolean
        _baseDrawn = _base.Begin(progs.DecalBase, _depth, ImageTarget.Texture2D, _depthCopy,
                                 {(_albedo, False), (_normal, False), (_env, False), (_gloss, False), (_emit, False)},
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
        Gl43.BindTextureUnit(0, TextureTarget.Texture2D, _albedo) : Gl43.BindTextureUnit(1, TextureTarget.Texture2D, _normal) : Gl43.BindTextureUnit(2, TextureTarget.Texture2D, _gloss) : Gl43.BindTextureUnit(3, TextureTarget.Texture2D, _depthCopy)
        For u = 0 To 3 : GL.BindSampler(u, _point) : Next
    End Sub

    Private Sub LightState()
        ' Full-screen lights: GEQUAL, no write, blend ONE/ONE, RGB, no cull (luces notas D3).
        GL.Enable(EnableCap.DepthTest) : GL.DepthFunc(DepthFunction.Gequal) : GL.DepthMask(False)
        GL.Enable(EnableCap.Blend) : GL.BlendFunc(BlendingFactor.One, BlendingFactor.One)
        GL.ColorMask(True, True, True, False)
        GL.Disable(EnableCap.CullFace)
    End Sub

    ''' <summary>The cb12 rows the light and composite programs read (writer 0x14181E1C0; the generated names of asm2glsl.cb12_text):
    ''' 12..15 inverse view, 20..23 inverse projection, 24..27 first-person inverse projection, 28 / 29 hair specular (ENV 16.3:
    ''' (0.02, 125, 1.2, 160), (0.36, -0.4, 0, 0)), 30 wetness / rain (no rain: 0), 35 eye, 41..46 fog (the fog-off state,
    ''' Fo4EffectConstants.FogOff). A program without a row ignores it.</summary>
    Private Shared Sub SetCb12(p As Shader_Base_Class, f As Fo4DeferredFrame)
        p.SetVector4Array("cb12_invview", f.Rows.InvView)
        p.SetVector4Array("cb12_invproj", f.Rows.InvProj)
        p.SetVector4Array("cb12_invproj1p", f.InvProj1P)
        p.SetVector4("cb12_28", New Vector4(0.02F, 125.0F, 1.2F, 160.0F))
        p.SetVector4("cb12_29", New Vector4(0.36F, -0.4F, 0.0F, 0.0F))
        p.SetVector4("cb12_30", Vector4.Zero)
        p.SetVector4("cb12_35", f.Rows.Eye)
        p.SetVector4Array("cb12_fog", f.Rows.Fog)
    End Sub

    ''' <summary>zq = min(fl((f*f - f*n) / ((f - n) * f)), 0x3F7FFFFE) in float32 and in that order (0x142247304..0x142247362).</summary>
    Private Shared Function Zq(n As Single, f As Single) As Single
        Dim a As Single = f * f - f * n
        Dim b As Single = (f - n) * f
        Return Math.Min(a / b, BitConverter.UInt32BitsToSingle(&H3F7FFFFEUI))
    End Function

    Private Sub DrawQuad(p As Shader_Base_Class, f As Fo4DeferredFrame)
        Dim z = Zq(f.Near, f.Far)
        p.SetVector4Array("WorldViewProj", {New Vector4(1, 0, 0, 0), New Vector4(0, 1, 0, 0), New Vector4(0, 0, 1, z), New Vector4(0, 0, 0, 1)})
        p.SetVector4("VPOSOffset", New Vector4(1.0F / f.W, 1.0F / f.H, 1.0F / f.W, 1.0F / f.H))   ' 0x142246D35..0x142246E01
        SetCb12(p, f)
        GL.BindVertexArray(_quadVao)
        GL.DrawArrays(PrimitiveType.TriangleStrip, 0, 4)
        GL.BindVertexArray(0)
    End Sub

    ''' <summary>GATE ONLY (no UI): ShadowGate --fo4-light-law mutant. True draws the ambient in its own pass (0x20000 rec3107) after the
    ''' lights instead of fused into the first one.</summary>
    Friend Shared GateAmbientApart As Boolean = False

    ''' <summary>The light stage (DF 3, 15.5; ola 4): RT 0x21/0x22 cleared to 0, then every rig light as a directional light with the
    ''' sun's technique of the exe's Settings (Fo4DeferredLightLaw.SunTechnique: with its shadow map when it casts), the ambient fused
    ''' into the first one (AMBIENT, 0x1421F5E9F..ECB: the preview has no tiled lighting, no entry in the list 0x143E62198 and no
    ''' [ssn + 0x150]) - or, with no light, alone (0x20000 rec3107, 0x1421F739E..7470) -, additive. A light that casts: t5 = the view of
    ''' its layer of the frame's map (ShadowMapTarget.LayerView: the map is the light's cascade 0), s5 = the comparison sampler, and the
    ''' receiver's constants of Fo4DeferredLightLaw (ShadowMapProj, ShadowWorldScale, ShadowSampleParam, FadeDistances,
    ''' ShadowFadeParam); the rows of cascade 1 (3..5 of ShadowMapProj, 1 of ShadowWorldScale) are never selected
    ''' (FadeDistances.x = d(far) = 1) and stay unwritten. APP (rev-E08): the rig's Darkness (AppShadowLift) and Softness (the radius
    ''' scale in ShadowSampleParam.z), the game's values for the default rig.</summary>
    ''' <summary>gates-v6 (rev-E16): the light techniques the last RunLights could not draw (no built program,
    ''' Fo4DeferredPrograms.LightProgram): the frame's notice reports them (PreviewControl.FinishFo4Deferred).</summary>
    Friend ReadOnly SkippedLightTechniques As New List(Of (Technique As UInteger, Light As Integer))

    ''' <summary>gates-v6 (rev-E16 / E19): the frame's notice of the lights RunLights could not draw - one line per technique (the
    ''' reason), each light once under it ("rig light N"; the ambient alone "rig ambient"), PreviewGap. The one statement
    ''' PreviewControl.FinishFo4Deferred and the gate use.</summary>
    Friend Shared Sub ReportSkippedLights(model As PreviewModel, skipped As IEnumerable(Of (Technique As UInteger, Light As Integer)))
        If model Is Nothing Then Return
        For Each sk In skipped
            model.ReportUndrawn(If(sk.Light < 0, "rig ambient", $"rig light {sk.Light + 1}"), $"Light technique 0x{sk.Technique:X8} has no program: the light is not drawn", PreviewModel.FrameNoticeKind.PreviewGap)
        Next
    End Sub

    Public Sub RunLights(progs As Fo4DeferredPrograms, f As Fo4DeferredFrame)
        SkippedLightTechniques.Clear()
        Dim lightIndex = -1
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _fboL)
        Gl43.DrawBuffers(_fboL, 2, LightBuffers)
        GL.ColorMask(True, True, True, True)
        Gl43.ClearFramebuffer(_fboL, ClearBuffer.Color, 0, New Single() {0, 0, 0, 0})
        Gl43.ClearFramebuffer(_fboL, ClearBuffer.Color, 1, New Single() {0, 0, 0, 0})
        LightState()
        BindGBufferInputs()
        Dim ambientDrawn = False
        For Each light In f.Lights
            Dim shadow = light.ShadowLayer >= 0 AndAlso f.ShadowMap IsNot Nothing
            Dim fuse = Not ambientDrawn AndAlso Not GateAmbientApart
            lightIndex += 1
            Dim tech = Fo4DeferredLightLaw.SunTechnique(shadow, fuse)
            Dim p = progs.LightProgram(tech)
            If p Is Nothing Then SkippedLightTechniques.Add((tech, lightIndex)) : Continue For
            p.Use()
            p.SetVector4("LightVector", New Vector4(light.Dir, 0.0F))
            p.SetVector4("LightColor", New Vector4(light.Color, 0.0F))
            If fuse Then p.SetVector4Array("DirectionalAmbient", f.Ambient)
            If shadow Then
                Dim proj = Fo4DeferredLightLaw.ShadowMapProjRows(light.ShadowFit.ViewProj, f.View, f.Eye)
                p.SetVector4Array("ShadowMapProj", proj)
                p.SetVector4Array("ShadowWorldScale", {Fo4DeferredLightLaw.ShadowWorldScale(light.ShadowFit)})
                Dim lift = f.ShadowLift, scale = 1.0F + f.ShadowRadiusGrowth
#If DEBUG Then
                ' ShadowGate --fo4-rig-knobs's mutant: the rig's knobs do not reach the engine receiver.
                If Fo4GBufferConstants.GateMutantOn("rig-knobs-off") Then lift = 0.0F : scale = 1.0F
#End If
                p.SetVector4("ShadowSampleParam", Fo4DeferredLightLaw.ShadowSampleParam(f.ShadowMap.Size, scale))
                p.SetFloat("AppShadowLift", lift)
                p.SetVector4("FadeDistances", Fo4DeferredLightLaw.FadeDistances(f.Near, f.Far))
                p.SetVector4("ShadowFadeParam", Fo4DeferredLightLaw.ShadowFadeParam())
                Gl43.BindTextureUnit(5, TextureTarget.Texture2DArray, f.ShadowMap.LayerView(light.ShadowLayer))
                GL.BindSampler(5, _shadowCompare)
            End If
            DrawQuad(p, f)
            If shadow Then Gl43.UnbindTextureUnit(5) : GL.BindSampler(5, 0)
            ambientDrawn = ambientDrawn OrElse fuse
        Next
        If Not ambientDrawn Then
            Dim a = progs.LightProgram(Fo4DeferredLightLaw.AmbientTechnique)
            If a Is Nothing Then
                SkippedLightTechniques.Add((Fo4DeferredLightLaw.AmbientTechnique, -1))
            Else
                a.Use()
                a.SetVector4Array("DirectionalAmbient", f.Ambient)
                DrawQuad(a, f)
            End If
        End If
        For u = 0 To 3 : Gl43.UnbindTextureUnit(u) : GL.BindSampler(u, 0) : Next
        GL.UseProgram(0)
    End Sub

    ''' <summary>Composite pass 1 (0x88 rec3432) -&gt; RT 2: triangle at z = 1, GREATER (DF 4.4).</summary>
    Public Sub RunComposite1(progs As Fo4DeferredPrograms, f As Fo4DeferredFrame)
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _fboC1)
        Gl43.DrawBuffer(_fboC1, DrawBufferMode.ColorAttachment0)
        GL.Disable(EnableCap.Blend)
        GL.Enable(EnableCap.DepthTest) : GL.DepthFunc(DepthFunction.Greater) : GL.DepthMask(False)
        GL.ColorMask(True, True, True, False)
        GL.Disable(EnableCap.CullFace)
        Dim p = progs.CompositeProgram(Fo4DeferredLightLaw.CompositePass1)
        p.Use()
        p.SetVector4("VPOSOffset", New Vector4(1.0F / f.W, 1.0F / f.H, 1.0F, 1.0F))   ' 0x142217560..0x1422175D0
        p.SetBool("uDecalBaseCoverage", False)                                         ' RT 2 has no coverage output (draw buffer 0 only)
        Gl43.BindTextureUnit(0, TextureTarget.Texture2D, _albedo) : Gl43.BindTextureUnit(3, TextureTarget.Texture2D, _gloss) : Gl43.BindTextureUnit(4, TextureTarget.Texture2D, _emit)
        Gl43.BindTextureUnit(5, TextureTarget.Texture2D, _lightD) : Gl43.BindTextureUnit(6, TextureTarget.Texture2D, _lightS)
        For Each u In {0, 3, 4, 5, 6} : GL.BindSampler(u, _point) : Next
        GL.BindVertexArray(_triVao) : GL.DrawArrays(PrimitiveType.Triangles, 0, 3) : GL.BindVertexArray(0)
        For Each u In {0, 3, 4, 5, 6} : Gl43.UnbindTextureUnit(u) : GL.BindSampler(u, 0) : Next
        GL.UseProgram(0)
    End Sub

    ''' <summary>The G-buffer depth/stencil into the scene target's, straight (NEAREST, same format: an exact copy): the scene target
    ''' stays in D3D rows through composite 2 and the forward stage (the zfight-overlay fix, 8-oct-2026: the forward draws rasterise
    ''' in the orientation this depth was written in, so an overlay on its body's geometry gets the body's depth bit for bit).
    ''' GateBlitMirror: the old mirrored blit.</summary>
    Public Sub BlitDepthTo(targetFbo As Integer)
        If GateBlitMirror Then Gl43.BlitFramebuffer(_fboG, targetFbo, 0, 0, _w, _h, 0, _h, _w, 0, ClearBufferMask.DepthBufferBit Or ClearBufferMask.StencilBufferBit, BlitFramebufferFilter.Nearest) : Return
        Gl43.BlitFramebuffer(_fboG, targetFbo, 0, 0, _w, _h, 0, 0, _w, _h,
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
                             Gl43.FramebufferRenderbuffer(fbo, att, RenderbufferTarget.Renderbuffer, img)
                         Else
                             Gl43.FramebufferTexture(fbo, att, img, 0)
                         End If
                     End Sub
        attach(_fboMirSrc, image, target)
        attach(_fboMirDst, scratch, ImageTarget.Texture2D)
        Gl43.ReadBuffer(_fboMirSrc, If(isDs, ReadBufferMode.None, ReadBufferMode.ColorAttachment0))
        Gl43.DrawBuffer(_fboMirDst, If(isDs, DrawBufferMode.None, DrawBufferMode.ColorAttachment0))
        Try
            ' Never copy back from a blit that did not happen: an incomplete pair would leave the scratch's old rows, and
            ' CopyImageSubData would write them over the frame silently. The blit honours the scissor (GL 4.6 18.3.1) and this
            ' runs from a Finally after whatever state an aborted stage left: off.
            Dim src = Gl43.CheckFramebufferStatus(_fboMirSrc, FramebufferTarget.ReadFramebuffer)
            Dim dst = Gl43.CheckFramebufferStatus(_fboMirDst, FramebufferTarget.DrawFramebuffer)
            If src <> FramebufferStatus.FramebufferComplete OrElse dst <> FramebufferStatus.FramebufferComplete Then
                Throw New InvalidOperationException($"MirrorRows: incomplete framebuffer (source {src}, scratch {dst}) for {format}")
            End If
            GL.Disable(EnableCap.ScissorTest)
            Dim mask = If(isDs, ClearBufferMask.DepthBufferBit Or ClearBufferMask.StencilBufferBit, ClearBufferMask.ColorBufferBit)
            Gl43.BlitFramebuffer(_fboMirSrc, _fboMirDst, 0, 0, _w, _h, 0, _h, _w, 0, mask, BlitFramebufferFilter.Nearest)
            GL.CopyImageSubData(scratch, ImageTarget.Texture2D, 0, 0, 0, 0, image, target, 0, 0, 0, 0, _w, _h, 1)
        Finally
            attach(_fboMirSrc, 0, target)
            attach(_fboMirDst, 0, ImageTarget.Texture2D)
        End Try
    End Sub

    ''' <summary>Composite pass 2 into the scene target (D3D rows, still UPPER_LEFT: GREATER at z = 1 against the G-buffer depth
    ''' blitted straight; draw buffers {A0, A1} = radiance and coverage, rev-19): the technique of the exe's Settings
    ''' (Fo4DeferredLightLaw.CompositePass2: 0xB60 rec3495 with the post, 0x160 rec3505 without). Inputs (SetupTechnique 0x142216AC0
    ''' and SetupGeometry 0x1422174F0, DF 4.2, RE_SAO_BOTH 2.6): t1 RT 0x1B, t2 RT 0x1D, t3 RT 0x1E, t4 RT 0x1F, t5 RT 0x21, t6 RT
    ''' 0x22, t7 the depth, t8 the envmap array, t10 RT 2, t15 RT 0x28; with APPLY_AO t9 = RT 0x27 (CLAMP, bilinear) and with SSLR
    ''' t14 = RT 7 - the preview binds 1 and 0 there (_noAo: decision Q-AO open; _noSsr: re.md H-9, the frame as before). Constants:
    ''' VPOSOffset (1 / W, 1 / H, 1, 1) (0x142217560..5D0), DynamicResolutionParams (Fo4DeferredLightLaw, 0x14221765F..659), the
    ''' cb0 of SetupTechnique (SSSSSParams (fSSSSSPower 4.0, fSSSSSDepthCorrection 2.5, fSSSSS1stPersonDepthMultiplier 9.0)
    ''' 0x142217097..0B2; SSLRParams0.x fSSLRIntensity 1.0, SSLRParams1.z fSSLRBlendingPower 1.0, DF 17.2), the sun of FOG
    ''' (0x14221790F: SunDirection / SunColor, Fo4DeferredFrame.SunDir / SunColor, .w Fo4DeferredLightLaw.SunDirectionW / SunColorW)
    ''' and the cb12 rows (SetCb12; the fog rows the fog-off state).</summary>
    Public Sub RunComposite2(progs As Fo4DeferredPrograms, f As Fo4DeferredFrame, sceneFbo As Integer, envArray As Integer)
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, sceneFbo)
        Gl43.DrawBuffers(sceneFbo, 2, LightBuffers)
        GL.Viewport(0, 0, _w, _h)
        GL.Disable(EnableCap.Blend)
        GL.Enable(EnableCap.DepthTest) : GL.DepthFunc(DepthFunction.Greater) : GL.DepthMask(False)
        GL.ColorMask(True, True, True, True)
        GL.Disable(EnableCap.CullFace)
        GL.Enable(EnableCap.TextureCubeMapSeamless)
        Dim p = progs.CompositeProgram(Fo4DeferredLightLaw.CompositePass2(f.PostOn))
        p.Use()
        p.SetVector4("VPOSOffset", New Vector4(1.0F / f.W, 1.0F / f.H, 1.0F, 1.0F))
        p.SetVector4("DynamicResolutionParams", Fo4DeferredLightLaw.DynamicResolutionParams(f.W, f.H))
        ' The translucent decals' base (user decision 7-oct-2026): the coverage of its pixels, DecalBaseTarget.CoverageTexture at unit 11.
        p.SetBool("uDecalBaseCoverage", _baseDrawn)
        p.SetVector4("SSSSSParams", New Vector4(4.0F, 2.5F, 9.0F, 0.0F))   ' DF 17.2
        p.SetVector4("SSLRParams0", New Vector4(1.0F, 0, 0, 0))
        p.SetVector4("SSLRParams1", New Vector4(0, 0, 1.0F, 0))
        p.SetVector4("SunDirection", New Vector4(f.SunDir, Fo4DeferredLightLaw.SunDirectionW))
        p.SetVector4("SunColor", New Vector4(f.SunColor, Fo4DeferredLightLaw.SunColorW))
        SetCb12(p, f)
        Gl43.BindTextureUnit(1, TextureTarget.Texture2D, _normal) : Gl43.BindTextureUnit(2, TextureTarget.Texture2D, _env) : Gl43.BindTextureUnit(3, TextureTarget.Texture2D, _gloss) : Gl43.BindTextureUnit(4, TextureTarget.Texture2D, _emit)
        Gl43.BindTextureUnit(5, TextureTarget.Texture2D, _lightD) : Gl43.BindTextureUnit(6, TextureTarget.Texture2D, _lightS) : Gl43.BindTextureUnit(7, TextureTarget.Texture2D, _depthCopy)
        Gl43.BindTextureUnit(8, TextureTarget.TextureCubeMapArray, envArray) : Gl43.BindTextureUnit(10, TextureTarget.Texture2D, _rt2) : Gl43.BindTextureUnit(15, TextureTarget.Texture2D, _linZ)
        Gl43.BindTextureUnit(9, TextureTarget.Texture2D, _noAo) : Gl43.BindTextureUnit(14, TextureTarget.Texture2D, _noSsr)
        Gl43.BindTextureUnit(11, TextureTarget.Texture2D, _base.CoverageTexture)
        For Each u In {1, 2, 3, 4, 5, 6, 7, 10, 11, 14, 15} : GL.BindSampler(u, _point) : Next
        GL.BindSampler(8, _cubeSampler)
        GL.BindSampler(9, _aoSampler)
        GL.BindVertexArray(_triVao) : GL.DrawArrays(PrimitiveType.Triangles, 0, 3) : GL.BindVertexArray(0)
        For Each u In {1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 14, 15} : Gl43.UnbindTextureUnit(u) : GL.BindSampler(u, 0) : Next
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
        Gl43.BindTextureUnit(0, TextureTarget.Texture2D, sceneTex) : Gl43.BindTextureUnit(1, TextureTarget.Texture2D, coverageTex)
        GL.BindVertexArray(_emptyVao) : GL.DrawArrays(PrimitiveType.Triangles, 0, 3) : GL.BindVertexArray(0)
        Gl43.UnbindTextureUnit(0) : Gl43.UnbindTextureUnit(1) : GL.UseProgram(0)
        GL.Disable(EnableCap.Blend)
        GL.DepthMask(True) : GL.DepthFunc(DepthFunction.Lequal) : GL.Enable(EnableCap.CullFace)
    End Sub

    Public Sub Free()
        For Each fb In {_fboG, _fboL, _fboC1, _fboMirSrc, _fboMirDst}
            If fb <> 0 Then GL.DeleteFramebuffer(fb)
        Next
        _fboG = 0 : _fboL = 0 : _fboC1 = 0 : _fboMirSrc = 0 : _fboMirDst = 0
        For Each t In {_albedo, _normal, _env, _gloss, _emit, _depth, _depthCopy, _lightD, _lightS, _rt2, _linZ, _mirR11, _mirRgba8, _mirDs, _noAo, _noSsr}
            If t <> 0 Then GL.DeleteTexture(t)
        Next
        _mirR11 = 0 : _mirRgba8 = 0 : _mirDs = 0 : _noAo = 0 : _noSsr = 0
        _albedo = 0 : _normal = 0 : _env = 0 : _gloss = 0 : _emit = 0 : _depth = 0 : _depthCopy = 0
        _lightD = 0 : _lightS = 0 : _rt2 = 0 : _linZ = 0 : _linZLevels = 0
        _base.Free()
        For Each v In {_quadVao, _triVao, _emptyVao}
            If v <> 0 Then GL.DeleteVertexArray(v)
        Next
        For Each b In {_quadVbo, _triVbo}
            If b <> 0 Then GL.DeleteBuffer(b)
        Next
        _quadVao = 0 : _triVao = 0 : _emptyVao = 0 : _quadVbo = 0 : _triVbo = 0
        For Each s In {_point, _cubeSampler, _shadowCompare, _aoSampler}
            If s <> 0 Then GL.DeleteSampler(s)
        Next
        _point = 0 : _cubeSampler = 0 : _shadowCompare = 0 : _aoSampler = 0
        _w = 0 : _h = 0
    End Sub
End Class
