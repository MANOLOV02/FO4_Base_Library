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

    ''' <summary>THE SUM OF ONE RIG LIGHT (Fo4DeferredTargets.SumLightOne; an app pass: the engine has one sun, the rig up to four): the light's
    ''' isolated targets - its diffuse (texLightD, unit 6) and specular (texLightS, unit 7), already multiplied by its focus pass
    ''' (DrawFocusPass) - read texel for texel into the frame's light targets RT 0x21 / 0x22 under the lights' additive state.</summary>
    Friend Const Fragment_LightSum As String = "#version 430
layout(binding = 6) uniform sampler2D texLightD;
layout(binding = 7) uniform sampler2D texLightS;
layout(location = 0) out vec4 oD;
layout(location = 1) out vec4 oS;
void main()
{
    oD = texelFetch(texLightD, ivec2(gl_FragCoord.xy), 0);
    oS = texelFetch(texLightS, ivec2(gl_FragCoord.xy), 0);
}
"

    ''' <summary>POST OFF (direct frame, user rule: the material goes always): the composite's radiance through the 2.3.8 display tail
    ''' of lit surfaces (LegacyDisplaySource.legacyLitDisplay, one copy; Fragment_SSE's post-off tail reads it too). Drawn at z = 1
    ''' with GREATER: only engine geometry.
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
    ''' engine blends a decal's over a surface's). Geometric normal = AppMain's geoNormal law: the interpolated
    ''' vertex normal (mv_tbn column 2), flipped on back faces of a double-sided shape; MSN: the map's normal (v_msnMatrix * t1.rbg,
    ''' the decode of Fallout 4's MSN programs: VS rec2215 -&gt; PS rec2698).</item>
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
    o0 = vec4(a * pow(bg, vec3(" & Shader_Base_Class.EngineGammaGlsl & ")), 0.0);
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
    ''' <summary>F9: the program of <paramref name="spec"/>, built now (the catalog's spec of a record or a slot).</summary>
    Friend Sub New(spec As ProgramSpec)
        MyBase.New(spec)
    End Sub
    ''' <summary>F9: the program already built from <paramref name="spec"/>.</summary>
    Friend Sub New(programId As Integer, spec As ProgramSpec)
        MyBase.New(programId, spec)
    End Sub
    ''' <summary>A compute program (the image-space CS, Fo4ImageSpaceSource.ComputeOf).</summary>
    Sub New(compute As String)
        MyBase.New(compute)
    End Sub
End Class

''' <summary>The programs of the FO4 deferred frame, every one built on its FIRST USE in Release and in Debug (S0-A, user decision P2 of
''' 29-oct-2026; before, 5-oct-2026, for the records only: cold, all 470 cost 15 s, one 30 ms): the app passes (DirectResolve, DecalBase,
''' PrepassRigid, PrepassAlpha) and the light and composite programs of the techniques the frame draws (Fo4DeferredLightLaw.
''' AppLightTechniques / AppCompositeTechniques, a closed list: a technique outside it has none), each a LazyProgram whose failure aborts
''' the frame (rev-50: the frame is not presented, Fallout 4 shows its status card); the G-buffer record programs (the 470 of the source's
''' closed list, Fo4GBufferSource.Records), the effect programs, the utility programs (the shadow map's emitter and the refraction
''' normals, b07, Fo4UtilitySource), the image-space programs (Fo4ImageSpaceSource) and the decals' base surfaces, a record whose build
''' fails remembered with its error and its draw reported (GBufferProgram; never a silent "not drawn"). Every program of every
''' transcribed closed list is built by ShadowGate --program-census, not by a start. Creating the object builds nothing.
''' <para>F9: every program is built by the control's builder thread (ProgramBuilder, received at construction: r4 rev-19). In a frame a
''' record still being built answers Nothing and marks the frame pending (the frame goes on, discovering every record it needs, and is
''' not presented: "Compiling shaders n/m (once per build)"); outside a frame it is waited for. A record and its variant without early tests are one
''' request (r3 rev-12).</para></summary>
Friend NotInheritable Class Fo4DeferredPrograms
    Private ReadOnly _builder As ProgramBuilder
    Private ReadOnly _directResolve, _decalBase, _prepassRigid, _prepassAlpha, _lightSum As LazyProgram(Of Fo4_Deferred_Shader_Class)
    ''' <summary>An app pass: a rig light's isolated targets onto the frame's (Fo4DeferredTargets.RunLights; the engine has one sun: Fragment_LightSum).</summary>
    Friend ReadOnly Property LightSum As Fo4_Deferred_Shader_Class
        Get
            Return _lightSum.GetProgram()
        End Get
    End Property
    ''' <summary>The app passes, built on their first use (LazyProgram: a failure aborts the frame).</summary>
    Friend ReadOnly Property DirectResolve As Fo4_Deferred_Shader_Class
        Get
            Return _directResolve.GetProgram()
        End Get
    End Property
    Friend ReadOnly Property DecalBase As Fo4_Deferred_Shader_Class
        Get
            Return _decalBase.GetProgram()
        End Get
    End Property
    Friend ReadOnly Property PrepassRigid As Fo4_Deferred_Shader_Class
        Get
            Return _prepassRigid.GetProgram()
        End Get
    End Property
    Friend ReadOnly Property PrepassAlpha As Fo4_Deferred_Shader_Class
        Get
            Return _prepassAlpha.GetProgram()
        End Get
    End Property
    ''' <summary>The programs of one vertex route (ola 2 v5: the GPU route - the engine VS blends cb10 - and the CPU route of a skinned
    ''' VS - Fo4GBufferSource.StageOf cpuRoute -, user decision 12-oct-2026): tuple -&gt; the record programs built so far, the tuples
    ''' whose build failed with the error text, the program of a record with forceEarlyDepthStencil without its early tests
    ''' (RecordWithoutEarlyTests)
    ''' and the decals' base SURFACE program (the tuple's engine stages + the app's surface fragment, revisor-25).</summary>
    Private NotInheritable Class RoutePrograms
        Friend ReadOnly Records As New Dictionary(Of (Vs As UInteger, Hs As UInteger, Ds As UInteger, Ps As UInteger), Fo4_Deferred_Shader_Class)
        Friend ReadOnly Errors As New Dictionary(Of (Vs As UInteger, Hs As UInteger, Ds As UInteger, Ps As UInteger), String)
        Friend ReadOnly Base As New Dictionary(Of (Vs As UInteger, Hs As UInteger, Ds As UInteger, Ps As UInteger), Fo4_Deferred_Shader_Class)
        Friend ReadOnly Surface As New Dictionary(Of (Vs As UInteger, Hs As UInteger, Ds As UInteger, Ps As UInteger), Fo4_Deferred_Shader_Class)
        ''' <summary>The surfaces whose build failed, with the build's message (S0 fase 7 rev-04: remembered and reported, as the records').</summary>
        Friend ReadOnly SurfaceErrors As New Dictionary(Of (Vs As UInteger, Hs As UInteger, Ds As UInteger, Ps As UInteger), String)
        Friend ReadOnly Position As New Dictionary(Of UInteger, Fo4_Deferred_Shader_Class)
        Friend ReadOnly PositionErrors As New Dictionary(Of UInteger, String)
    End Class
    Private ReadOnly _gpuRoute As New RoutePrograms, _cpuRoute As New RoutePrograms
    ''' <summary>The route a draw's program comes from: the CPU route only for a VS that has one (a rigid VS has one route).</summary>
    Private Function RouteOf(vs As UInteger, cpuRoute As Boolean) As RoutePrograms
        Return If(Fo4GBufferSource.EffectiveCpuLayout(vs, cpuRoute) <> Fo4CpuRouteLayout.None, _cpuRoute, _gpuRoute)
    End Function
#If DEBUG Then
    ''' <summary>GateWith copies: the programs that own the records not overridden by the gate.</summary>
    Private ReadOnly _gbufferSource As Fo4DeferredPrograms
#End If
    Private ReadOnly _built As New List(Of Shader_Base_Class)

    ''' <summary>Ola 4: the light (b10) and composite (b11) programs by technique, for the techniques the frame draws (Fo4DeferredLightLaw.
    ''' AppLightTechniques / AppCompositeTechniques, a closed list), each built on its first use.</summary>
    Private ReadOnly _lights As New Dictionary(Of UInteger, LazyProgram(Of Fo4_Deferred_Shader_Class))
    Private ReadOnly _composites As New Dictionary(Of UInteger, LazyProgram(Of Fo4_Deferred_Shader_Class))

    ''' <summary>F9: the records' requests (the requests not resolved yet are its Pending, by record: family, route and key).</summary>
    Private ReadOnly _requests As ProgramRecordRequests(Of Fo4_Deferred_Shader_Class)

    ''' <summary>F9: ONE RECORD THROUGH THE BUILDER (ProgramRecordRequests, the one code with SseEnginePrograms).</summary>
    Private Function Pedir(clave As String, names As String(), specs As Func(Of ProgramSpec()), ByRef programs As Fo4_Deferred_Shader_Class(),
                           ByRef errorText As String) As ProgramState
        Return _requests.Ask(clave, names, specs, programs, errorText)
    End Function

    ''' <summary>F9: where a record is without asking for it (r3 rev-13 / rev-18: the admission of a shape outside a frame - DrawsShape -
    ''' never enqueues nor waits).</summary>
    Private Function Estado(clave As String) As ProgramState
        Return _requests.State(clave)
    End Function

    ' ---- F9 (r2 "Duplicacion" (i)): THE SPECS of the deferred frame's programs - the one place each pairs its texts. These programs
    ' build from them and ProgramCatalog enumerates them (the manifest, the census). <paramref name="cpuRoute"/> is the
    ' EFFECTIVE route (the CPU route of a VS that has one, Fo4*Source.EffectiveCpuLayout). ----

    ''' <summary>An app pass: DirectResolve (the fullscreen VS and its resolve).</summary>
    Friend Shared Function DirectResolveSpec() As ProgramSpec
        Return Shader_Base_Class.SpecOf(Fo4DeferredAppSource.Vertex_Fullscreen, Fo4DeferredAppSource.Fragment_DirectResolve)
    End Function
    ''' <summary>An app pass: LightSum (the fullscreen VS and a rig light's sum).</summary>
    Friend Shared Function LightSumSpec() As ProgramSpec
        Return Shader_Base_Class.SpecOf(Fo4DeferredAppSource.Vertex_Fullscreen, Fo4DeferredAppSource.Fragment_LightSum)
    End Function
    ''' <summary>An app pass: DecalBase (the fullscreen VS and the decals' base mark).</summary>
    Friend Shared Function DecalBaseSpec() As ProgramSpec
        Return Shader_Base_Class.SpecOf(Fo4DeferredAppSource.Vertex_Fullscreen, Fo4DeferredAppSource.Fragment_DecalBase)
    End Function
    ''' <summary>An app pass: the rigid prepass (0x14181FBCF..C19).</summary>
    Friend Shared Function PrepassRigidSpec() As ProgramSpec
        Return Shader_Base_Class.SpecOf(Fo4GBufferSource.PrepassVertex(3928), Fo4GBufferSource.PrepassFragmentNone)
    End Function
    ''' <summary>An app pass: the alpha-tested prepass (0x14181FC99..D01).</summary>
    Friend Shared Function PrepassAlphaSpec() As ProgramSpec
        Return Shader_Base_Class.SpecOf(Fo4GBufferSource.PrepassVertex(3929), Fo4GBufferSource.PrepassFragment(3930))
    End Function

    ''' <summary>A light program's (VS, PS) pair (b10).</summary>
    Friend Shared Function LightPairSpec(vs As UInteger, ps As UInteger) As ProgramSpec
        Return Shader_Base_Class.SpecOf(Fo4DFLightSource.VertexOf(vs), Fo4DFLightSource.FragmentOf(ps))
    End Function

    ''' <summary>The program of light technique <paramref name="t"/> (b10: BSDFLightShader SetupTechnique 0x142246B40, VS / PS by
    ''' Fo4DFLightSource.VertexShaderId / PixelShaderId). A technique whose program the engine does not have throws: the frame's
    ''' techniques are a closed list built at start.</summary>
    Friend Shared Function LightSpec(t As UInteger) As ProgramSpec
        Dim missing = Fo4DFLightSource.MissingEnginePrograms(t)
        If missing IsNot Nothing Then Throw New InvalidOperationException($"light technique 0x{t:X8}: {missing}")
        Return LightPairSpec(Fo4DFLightSource.VertexShaderId(t), Fo4DFLightSource.PixelShaderId(t))
    End Function

    ''' <summary>A composite program's (VS, PS) pair (b11).</summary>
    Friend Shared Function CompositePairSpec(vs As UInteger, ps As UInteger) As ProgramSpec
        Return Shader_Base_Class.SpecOf(Fo4DFCompositeSource.VertexOf(vs), Fo4DFCompositeSource.FragmentOf(ps))
    End Function

    ''' <summary>The program of composite technique <paramref name="t"/> (b11: BSDFCompositeShader SetupTechnique 0x142216AC0, VS / PS
    ''' by Fo4DFCompositeSource.VertexShaderId / PixelShaderId; no tiled lighting, Fo4DeferredLightLaw.SunTechnique).</summary>
    Friend Shared Function CompositeSpec(t As UInteger) As ProgramSpec
        Dim missing = Fo4DFCompositeSource.MissingEnginePrograms(t)
        If missing IsNot Nothing Then Throw New InvalidOperationException($"composite technique 0x{t:X8}: {missing}")
        Return CompositePairSpec(Fo4DFCompositeSource.VertexShaderId(t), Fo4DFCompositeSource.PixelShaderId(t))
    End Function

    ''' <summary>A utility program (b07): VS <paramref name="vs"/> on its route with PS <paramref name="ps"/>, or with the null fragment
    ''' (the rasterised depth only, Fo4UtilitySource.NullFragment) when <paramref name="nullPs"/>.</summary>
    Friend Shared Function UtilitySpec(vs As UInteger, ps As UInteger, nullPs As Boolean, cpuRoute As Boolean) As ProgramSpec
        Return Shader_Base_Class.SpecOf(Fo4UtilitySource.VertexOf(vs, cpuRoute), If(nullPs, Fo4UtilitySource.NullFragment(), Fo4UtilitySource.FragmentOf(ps)))
    End Function

    ''' <summary>An image-space (VS, PS) pair.</summary>
    Friend Shared Function ImageSpacePairSpec(vs As UInteger, ps As UInteger) As ProgramSpec
        Return Shader_Base_Class.SpecOf(Fo4ImageSpaceSource.VertexOf(vs), Fo4ImageSpaceSource.FragmentOf(ps))
    End Function

    ''' <summary>The image-space program of PS or CS record <paramref name="rec"/>: the CS alone, or the PS with the VS of its program
    ''' (the first of Fo4ImageSpaceSource.Programs with that PS).</summary>
    Friend Shared Function ImageSpaceSpec(rec As UInteger) As ProgramSpec
        If Fo4ImageSpaceSource.ComputeStages.ContainsKey(rec) Then Return Shader_Base_Class.ComputeSpecOf(Fo4ImageSpaceSource.ComputeOf(rec))
        Return ImageSpacePairSpec(Fo4ImageSpaceSource.Programs.First(Function(x) x.Ps = rec).Vs, rec)
    End Function

    ''' <summary>A G-buffer record's program: the tuple's engine stages (VS, + TCS / TES when it tessellates, 0x14181D60E) and the
    ''' record's PS, with its early depth tests or, for the variant (<paramref name="earlyTests"/> False), without them.</summary>
    Friend Shared Function GBufferSpec(key As (Vs As UInteger, Hs As UInteger, Ds As UInteger, Ps As UInteger), cpuRoute As Boolean,
                                       Optional earlyTests As Boolean = True) As ProgramSpec
        Dim st = Fo4GBufferSource.StageOf(key.Vs, key.Hs, key.Ds, cpuRoute)
        Return Shader_Base_Class.SpecOf(st.Vertex, Fo4GBufferSource.FragmentOf(key.Ps, earlyTests:=earlyTests), st.Control, st.Evaluation)
    End Function

    ''' <summary>The decals' base surface of a G-buffer tuple: its engine stages and the app's surface fragment (revisor-25).</summary>
    Friend Shared Function DecalSurfaceSpec(key As (Vs As UInteger, Hs As UInteger, Ds As UInteger, Ps As UInteger), cpuRoute As Boolean) As ProgramSpec
        Dim st = Fo4GBufferSource.StageOf(key.Vs, key.Hs, key.Ds, cpuRoute)
        Return Shader_Base_Class.SpecOf(st.Vertex, Fo4DeferredAppSource.Fragment_DecalBaseSurface, st.Control, st.Evaluation)
    End Function

    ''' <summary>The position capture of engine VS <paramref name="vs"/>: the VS alone, gl_Position captured by transform feedback.</summary>
    Friend Shared Function PositionSpec(vs As UInteger, cpuRoute As Boolean) As ProgramSpec
        Return Shader_Base_Class.SpecOf(Fo4GBufferSource.StageOf(vs, 0UI, 0UI, cpuRoute, positionCapture:=True).Vertex,
                                        Fo4DeferredAppSource.Fragment_PositionCapture, Nothing, Nothing, {"gl_Position"})
    End Function

    ''' <summary>An effect technique's program: Fo4EffectSource.VertexOf + FragmentOf (<paramref name="vertexEdit"/>: a Debug gate's
    ''' declared edit of the vertex text, GateWithEffectVertex; Nothing in production).</summary>
    Friend Shared Function EffectSpec(key As (Vs As UInteger, Ps As UInteger), cpuRoute As Boolean,
                                      Optional vertexEdit As Func(Of String, String) = Nothing) As ProgramSpec
        Dim vertex = Fo4EffectSource.VertexOf(key.Vs, cpuRoute)
        If vertexEdit IsNot Nothing Then vertex = vertexEdit(vertex)
        Return Shader_Base_Class.SpecOf(vertex, Fo4EffectSource.FragmentOf(key.Ps))
    End Function

    ''' <summary>An effect VS's decals' base surface: the VS with the surface foot and Fragment_DecalBaseSurfaceEffect
    ''' (<paramref name="fragment"/>: a Debug gate's fragment, GateWithEffectSurfaceFragment; Nothing in production).</summary>
    Friend Shared Function EffectSurfaceSpec(vs As UInteger, cpuRoute As Boolean, Optional fragment As String = Nothing) As ProgramSpec
        Return Shader_Base_Class.SpecOf(Fo4EffectSource.VertexOf(vs, cpuRoute, surface:=True),
                                        If(fragment, Fo4DeferredAppSource.Fragment_DecalBaseSurfaceEffect))
    End Function

    ''' <summary>A slot of these programs: <paramref name="specOf"/> composed on its first request, wrapped as the deferred class.</summary>
    Private Function NewSlot(name As String, specOf As Func(Of ProgramSpec)) As LazyProgram(Of Fo4_Deferred_Shader_Class)
        Return New LazyProgram(Of Fo4_Deferred_Shader_Class)(name, _builder, specOf, Function(id As Integer, s As ProgramSpec) New Fo4_Deferred_Shader_Class(id, s))
    End Function

    ''' <param name="builder">The builder of the control these programs are drawn in (mandatory, r4 rev-19).</param>
    Friend Sub New(builder As ProgramBuilder)
        If builder Is Nothing Then Throw New ArgumentNullException(NameOf(builder))
        _builder = builder
        _requests = New ProgramRecordRequests(Of Fo4_Deferred_Shader_Class)(_builder, Function(id, sp) New Fo4_Deferred_Shader_Class(id, sp), _built)
        For Each t In Fo4DeferredLightLaw.AppLightTechniques
            Dim tl = t
            If Not _lights.ContainsKey(t) Then _lights(t) = NewSlot($"light technique 0x{t:X8}", Function() LightSpec(tl))
        Next
        For Each t In Fo4DeferredLightLaw.AppCompositeTechniques
            Dim tc = t
            If Not _composites.ContainsKey(t) Then _composites(t) = NewSlot($"composite technique 0x{t:X8}", Function() CompositeSpec(tc))
        Next
        _directResolve = NewSlot("DirectResolve", AddressOf DirectResolveSpec)
        _lightSum = NewSlot("LightSum", AddressOf LightSumSpec)
        _decalBase = NewSlot("DecalBase", AddressOf DecalBaseSpec)
        _prepassRigid = NewSlot("PrepassRigid", AddressOf PrepassRigidSpec)
        _prepassAlpha = NewSlot("PrepassAlpha", AddressOf PrepassAlphaSpec)
#If DEBUG Then
        ' rev-E08: the rig's Darkness reaches the shadowed sun techniques the rig draws only through the line the generator
        ' rewrote in their PS (Fo4DFLightSource.AppShadowDarknessLines): a law technique outside that list would lose it. The
        ' Shadow Bias (26-oct-2026) the same, through its line (AppShadowBiasLines) in the same PS.
        For Each ambient In {True, False}
            Dim ps = Fo4DFLightSource.PixelShaderId(Fo4DeferredLightLaw.SunTechnique(True, ambient))
            If Not Fo4DFLightSource.AppShadowDarknessPs.Contains(ps) Then Throw New Exception($"light PS 0x{ps:X8} of the rig's shadowed sun is not in Fo4DFLightSource.AppShadowDarknessPs (the rig's Darkness would not reach it)")
            Dim fn = Fo4DFLightSource.PixelShaders(ps).Fn
            If Not Fo4DFLightSource.AppShadowBiasLines.Any(Function(l) fn.Contains("void " & l.Fn & "(")) Then Throw New Exception($"light PS 0x{ps:X8} of the rig's shadowed sun has no line of Fo4DFLightSource.AppShadowBiasLines (the rig's Shadow Bias would not reach it)")
        Next
#End If
    End Sub

    ''' <summary>The light program of technique <paramref name="t"/> (one of Fo4DeferredLightLaw.AppLightTechniques; a GateWithLight
    ''' copy answers its own for the overridden technique). gates-v6 (rev-E16): Nothing for a technique outside the closed list built
    ''' at start (the caller does not draw that light and says so: RunLights / SkippedLightTechniques) - except in Debug while a
    ''' library mutant is on (Fo4GBufferConstants.GateMutantOn), where a technique the engine has is built on demand. F9 (r2 rev-01): the
    ''' lights and composites of the closed lists are asked from the builder together at the first ask; in a frame, while pending, Nothing
    ''' with <paramref name="state"/> Pending (the frame is pending: no "skipped" notice).</summary>
    Friend Function LightProgram(t As UInteger, Optional ByRef state As ProgramState = ProgramState.NotBuilt) As Fo4_Deferred_Shader_Class
        state = ProgramState.Built
#If DEBUG Then
        If _gateLight.HasValue AndAlso _gateLight.Value.Technique = t Then Return _gateLight.Value.Program
#End If
#If DEBUG Then
        If _gbufferSource IsNot Nothing Then Return _gbufferSource.LightProgram(t, state)
#End If
        Dim slot As LazyProgram(Of Fo4_Deferred_Shader_Class) = Nothing
#If DEBUG Then
        ' GATE (ShadowGate --fo4-light-law, gates-v6 rev-E19): a technique taken out of _lights - the shadowed sun without the fused
        ' ambient, the technique of a rig's second and later shadowed lights - so the frame meets a light without a program
        If Fo4GBufferConstants.GateMutantOn("dfl-drop-light") AndAlso t = Fo4DeferredLightLaw.SunTechnique(True, False) Then Return Nothing
#End If
        If _lights.TryGetValue(t, slot) Then
            PedirLucesYComposites()
            Return slot.GetOrPending(state)
        End If
#If DEBUG Then
        ' GATE (ShadowGate --fo4-light-law, gates-v6 rev-E16): the mutated technique built on demand, the mutant reaches a verdict
        If Fo4GBufferConstants.GateVertexConstantsMutant IsNot Nothing AndAlso Fo4DFLightSource.MissingEnginePrograms(t) Is Nothing Then
            slot = NewSlot($"light technique 0x{t:X8}", Function() LightSpec(t))
            _lights(t) = slot
            Return slot.GetOrPending(state)
        End If
#End If
        state = ProgramState.NotBuilt
        Return Nothing
    End Function

    ''' <summary>F9 (r2 rev-01, "pedidos juntos"): the lights and composites of the closed lists asked from the builder at once, at the
    ''' first ask of any of them (the frame meets them one after another; the builder has them all queued).</summary>
    Private Sub PedirLucesYComposites()
        For Each s In _lights.Values : s.RequestOnly() : Next
        For Each s In _composites.Values : s.RequestOnly() : Next
    End Sub

    ''' <summary>The composite program of technique <paramref name="t"/> (one of Fo4DeferredLightLaw.AppCompositeTechniques; a
    ''' GateWithComposite copy answers its own for the overridden technique). F9: Nothing while pending in a frame, as LightProgram.</summary>
    Friend Function CompositeProgram(t As UInteger, Optional ByRef state As ProgramState = ProgramState.NotBuilt) As Fo4_Deferred_Shader_Class
        state = ProgramState.Built
#If DEBUG Then
        If _gateComposite.HasValue AndAlso _gateComposite.Value.Technique = t Then Return _gateComposite.Value.Program
#End If
#If DEBUG Then
        If _gbufferSource IsNot Nothing Then Return _gbufferSource.CompositeProgram(t, state)
#End If
        Dim slot As LazyProgram(Of Fo4_Deferred_Shader_Class) = Nothing
        If Not _composites.TryGetValue(t, slot) Then Throw New InvalidOperationException($"composite technique 0x{t:X8} is not one of Fo4DeferredLightLaw.AppCompositeTechniques")
        PedirLucesYComposites()
        Return slot.GetOrPending(state)
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
#If DEBUG Then
        If _gbufferSource IsNot Nothing Then Return _gbufferSource.UtilityProgram(t, errorText, cpuRoute)
#End If
        Dim vs = Fo4UtilitySource.VertexShaderId(t)
        Dim nullPs = Fo4UtilitySource.PixelShaderNull(t)
        Dim key = (vs, Fo4UtilitySource.PixelShaderId(t), nullPs)
        Dim route = If(Fo4UtilitySource.EffectiveCpuLayout(vs, cpuRoute) <> Fo4CpuRouteLayout.None, _utilityCpu, _utilityGpu)
        Dim p As Fo4_Deferred_Shader_Class = Nothing
        If route.Programs.TryGetValue(key, p) Then Return p
        If route.Errors.TryGetValue(key, errorText) Then Return Nothing
        Dim cpu = route Is _utilityCpu
        Dim ps As Fo4_Deferred_Shader_Class() = Nothing
        Select Case Pedir($"ut|{cpu}|{key}", {Nothing}, Function() {UtilitySpec(vs, key.Item2, nullPs, cpu)}, ps, errorText)
            Case ProgramState.Built
                route.Programs(key) = ps(0)
                Return ps(0)
            Case ProgramState.Failed
                route.Errors(key) = errorText
        End Select
        Return Nothing
    End Function

    ''' <summary>Ola 4: the image-space programs by record (the PS of a VS / PS program, or the CS), built on first use.</summary>
    Private ReadOnly _imageSpace As New Dictionary(Of UInteger, Fo4_Deferred_Shader_Class)
    ''' <summary>F9 (r2 table B): the image-space records whose build failed, with the message (remembered as the records'; a gesture's
    ''' retry forgets them, ForgetFailures).</summary>
    Private ReadOnly _imageSpaceErrors As New Dictionary(Of UInteger, String)

    ''' <summary>F9 (r2 "pedidos juntos"): the chain's closed list (Fo4ImageSpaceChain.Records) asked from the builder at once, before the
    ''' chain runs.</summary>
    Friend Sub RequestImageSpace(records As IEnumerable(Of UInteger))
#If DEBUG Then
        If _gbufferSource IsNot Nothing Then
            _gbufferSource.RequestImageSpace(records)
            Return
        End If
#End If
        For Each rec In records
            If _imageSpace.ContainsKey(rec) OrElse _imageSpaceErrors.ContainsKey(rec) OrElse _requests.Pending.ContainsKey($"is|{rec}") Then Continue For
            If Fo4ImageSpaceSource.GapOf(rec) <> "" Then Continue For
            Dim r = rec
            Dim sp As ProgramSpec
            Try
                sp = ImageSpaceSpec(r)
            Catch ex As Exception
                _imageSpaceErrors(r) = ex.Message
                Continue For
            End Try
            _requests.Pending($"is|{r}") = _builder.Request({Nothing}, {sp})
        Next
    End Sub

    ''' <summary>The image-space program of PS or CS record <paramref name="rec"/> (Fo4ImageSpaceSource.Programs: a PS with the VS of its
    ''' program, a CS alone), built on its first use; a declared gap throws (the post's chain uses none: its records are a closed
    ''' list, Fo4ImageSpaceChain). F9 (r2 table B): a build that fails is remembered and thrown as ProgramUnavailableException (the card
    ''' with Retry); in a frame, a record still being built throws ProgramPendingException (the chain's records were asked whole,
    ''' RequestImageSpace); outside a frame it is waited for.</summary>
    Friend Function ImageSpaceProgram(rec As UInteger) As Fo4_Deferred_Shader_Class
#If DEBUG Then
        If _gateImageSpace.HasValue AndAlso _gateImageSpace.Value.Rec = rec Then Return _gateImageSpace.Value.Program
#End If
#If DEBUG Then
        If _gbufferSource IsNot Nothing Then Return _gbufferSource.ImageSpaceProgram(rec)
#End If
        Dim p As Fo4_Deferred_Shader_Class = Nothing
        If _imageSpace.TryGetValue(rec, p) Then Return p
        Dim gap = Fo4ImageSpaceSource.GapOf(rec)
        If gap <> "" Then Throw New InvalidOperationException($"image-space record {rec}: {gap}")
        Dim errorText As String = Nothing
        If _imageSpaceErrors.TryGetValue(rec, errorText) Then Throw New ProgramUnavailableException($"image-space record {rec}: {errorText}", Nothing)
        Dim ps As Fo4_Deferred_Shader_Class() = Nothing
        Select Case Pedir($"is|{rec}", {Nothing}, Function() {ImageSpaceSpec(rec)}, ps, errorText)
            Case ProgramState.Built
                _imageSpace(rec) = ps(0)
                Return ps(0)
            Case ProgramState.Failed
                _imageSpaceErrors(rec) = errorText
                Throw New ProgramUnavailableException($"image-space record {rec}: {errorText}", Nothing)
            Case Else
                Throw New ProgramPendingException($"image-space record {rec}")
        End Select
    End Function

    ''' <summary>The program of the G-buffer technique <paramref name="t"/> on the vertex route of the draw (<paramref name="cpuRoute"/>,
    ''' RenderableMesh.EngineCpuRoute), built on its first use; Nothing when its build failed, with the error in
    ''' <paramref name="errorText"/> (remembered: it is not rebuilt). F9 (r3 rev-12): a record with forceEarlyDepthStencil and its variant
    ''' without early tests are ONE request, published when both are built (the variant under its own name for the one-shot compile
    ''' failure, ProgramCompiler.GateEarlyZVariantSlot), failed if either fails (the builder deletes the other: S0 fase 7d's rule). In a
    ''' frame, while pending, Nothing with <paramref name="state"/> Pending.</summary>
    Friend Function GBufferProgram(t As Fo4Technique, ByRef errorText As String, Optional cpuRoute As Boolean = False,
                                   Optional ByRef state As ProgramState = ProgramState.NotBuilt) As Fo4_Deferred_Shader_Class
        errorText = Nothing
        state = ProgramState.Built
        Dim key = t.Key()
        Dim route = RouteOf(key.Vs, cpuRoute)
        Dim program As Fo4_Deferred_Shader_Class = Nothing
        If route.Records.TryGetValue(key, program) Then Return program
#If DEBUG Then
        If _gbufferSource IsNot Nothing Then Return _gbufferSource.GBufferProgram(t, errorText, cpuRoute, state)
#End If
        state = ProgramState.Failed
        If route.Errors.TryGetValue(key, errorText) Then Return Nothing
        Dim cpu = route Is _cpuRoute
        Dim early = Fo4GBufferSource.Records(key.Ps).EarlyZ
        Dim ps As Fo4_Deferred_Shader_Class() = Nothing
        state = Pedir($"gb|{cpu}|{key}", If(early, {Nothing, ProgramCompiler.GateEarlyZVariantSlot}, {CType(Nothing, String)}),
                      Function() If(early, {GBufferSpec(key, cpu), GBufferSpec(key, cpu, earlyTests:=False)}, {GBufferSpec(key, cpu)}), ps, errorText)
        Select Case state
            Case ProgramState.Built
#If DEBUG Then
                If early AndAlso ps.Length <> 2 Then Throw New InvalidOperationException($"G-buffer record {key}: published without its variant without early tests (r3 rev-12)")
#End If
                route.Records(key) = ps(0)
                If early Then route.Base(key) = ps(1)
                Return ps(0)
            Case ProgramState.Failed
                route.Errors(key) = errorText
        End Select
        Return Nothing
    End Function

    ''' <summary>F9 (r3 rev-13 / rev-18): where the G-buffer record of <paramref name="t"/> is, without asking for it nor waiting
    ''' (DrawsShape's admission outside a frame: only Failed leaves the shape out).</summary>
    Friend Function PeekGBuffer(t As Fo4Technique, Optional cpuRoute As Boolean = False) As ProgramState
        Dim key = t.Key()
        Dim route = RouteOf(key.Vs, cpuRoute)
        If route.Records.ContainsKey(key) Then Return ProgramState.Built
#If DEBUG Then
        If _gbufferSource IsNot Nothing Then Return _gbufferSource.PeekGBuffer(t, cpuRoute)
#End If
        If route.Errors.ContainsKey(key) Then Return ProgramState.Failed
        Return Estado($"gb|{route Is _cpuRoute}|{key}")
    End Function

    ''' <summary>The engine VS <paramref name="vs"/> on a vertex route, alone, its clip position (gl_Position, invariant in the app
    ''' foot) captured by transform feedback: what an overlay that shares its host's geometry draws its position with
    ''' (RenderableMesh.EnginePositions, user decision 12-oct-2026). Built on first use, one per VS and route; Nothing when the
    ''' stage does not build (the overlay then keeps its effect VS's own position, as its host is not drawn by an engine stage), with
    ''' the reason in <paramref name="errorText"/> (remembered, as the records': ShadowGate --program-census names it).</summary>
    Friend Function EnginePositionProgram(vs As UInteger, cpuRoute As Boolean, Optional ByRef errorText As String = Nothing) As Fo4_Deferred_Shader_Class
        errorText = Nothing
#If DEBUG Then
        If _gbufferSource IsNot Nothing Then Return _gbufferSource.EnginePositionProgram(vs, cpuRoute, errorText)
#End If
        Dim route = RouteOf(vs, cpuRoute)
        Dim p As Fo4_Deferred_Shader_Class = Nothing
        If route.Position.TryGetValue(vs, p) Then
            route.PositionErrors.TryGetValue(vs, errorText)
            Return p
        End If
        Dim cpu = route Is _cpuRoute
        Dim ps As Fo4_Deferred_Shader_Class() = Nothing
        Select Case Pedir($"pos|{cpu}|{vs}", {Nothing}, Function() {PositionSpec(vs, cpu)}, ps, errorText)
            Case ProgramState.Built
                route.Position(vs) = ps(0)
                Return ps(0)
            Case ProgramState.Failed
                route.PositionErrors(vs) = errorText
                route.Position(vs) = Nothing
        End Select
        Return Nothing
    End Function

    ''' <summary>Ola 3: the effect programs of one vertex route (the CPU route only for a skinned VS, Fo4EffectSource.EffectiveCpuLayout):
    ''' technique key (VS, PS) -&gt; the program, its build error; VS -&gt; the decals' base surface program (the same VS with the
    ''' surface foot, Fragment_DecalBaseSurfaceEffect).</summary>
    Private NotInheritable Class EffectRoute
        Friend ReadOnly Programs As New Dictionary(Of (Vs As UInteger, Ps As UInteger), Fo4_Deferred_Shader_Class)
        Friend ReadOnly Errors As New Dictionary(Of (Vs As UInteger, Ps As UInteger), String)
        Friend ReadOnly Surface As New Dictionary(Of UInteger, Fo4_Deferred_Shader_Class)
        ''' <summary>The surfaces whose build failed, by VS, with the build's message (S0 fase 7 rev-04).</summary>
        Friend ReadOnly SurfaceErrors As New Dictionary(Of UInteger, String)
    End Class
    Private ReadOnly _effectGpu As New EffectRoute, _effectCpu As New EffectRoute
    Private Function EffectRouteOf(vs As UInteger, cpuRoute As Boolean) As EffectRoute
        Return If(Fo4EffectSource.EffectiveCpuLayout(vs, cpuRoute) <> Fo4CpuRouteLayout.None, _effectCpu, _effectGpu)
    End Function

    ''' <summary>The program of the effect technique <paramref name="t"/> on the draw's vertex route (RenderableMesh.EngineCpuRoute):
    ''' Fo4EffectSource.VertexOf + FragmentOf, built on its first use (user decision 13-oct-2026); Nothing when its build failed,
    ''' with the error in <paramref name="errorText"/> (remembered: not rebuilt).</summary>
    Friend Function EffectProgram(t As Fo4EffectTech, ByRef errorText As String, Optional cpuRoute As Boolean = False,
                                  Optional ByRef state As ProgramState = ProgramState.NotBuilt) As Fo4_Deferred_Shader_Class
        errorText = Nothing
        state = ProgramState.Built
        Dim key = t.Key()
        Dim route = EffectRouteOf(key.Vs, cpuRoute)
        Dim p As Fo4_Deferred_Shader_Class = Nothing
        If route.Programs.TryGetValue(key, p) Then Return p
#If DEBUG Then
        If _gbufferSource IsNot Nothing AndAlso _gateEffectVertex Is Nothing Then Return _gbufferSource.EffectProgram(t, errorText, cpuRoute, state)
#End If
        state = ProgramState.Failed
        If route.Errors.TryGetValue(key, errorText) Then Return Nothing
        Dim vertexEdit As Func(Of String, String) = Nothing
#If DEBUG Then
        vertexEdit = _gateEffectVertex
#End If
        Dim cpu = route Is _effectCpu
        Dim ps As Fo4_Deferred_Shader_Class() = Nothing
        state = Pedir($"fx|{cpu}|{key}", {Nothing}, Function() {EffectSpec(key, cpu, vertexEdit)}, ps, errorText)
        Select Case state
            Case ProgramState.Built
                route.Programs(key) = ps(0)
                Return ps(0)
            Case ProgramState.Failed
                route.Errors(key) = errorText
        End Select
        Return Nothing
    End Function

    ''' <summary>The translucent decals' base surface program of an effect technique (D16; ola 3, Q2): its engine VS with the surface
    ''' foot + Fragment_DecalBaseSurfaceEffect, one per VS and route, built on first use; Nothing when it does not build, with the build's
    ''' message in <paramref name="errorText"/> (remembered, as the records'; S0 fase 7 rev-04: the draw reports it,
    ''' RenderableMesh.ReportDecalBaseSurfaceNotBuilt). A copy made by GateWithEffectSurfaceFragment builds its own with the gate's
    ''' fragment.</summary>
    Friend Function EffectSurfaceProgram(t As Fo4EffectTech, Optional cpuRoute As Boolean = False, Optional ByRef errorText As String = Nothing) As Fo4_Deferred_Shader_Class
        errorText = Nothing
        Dim vs = t.Key().Vs
        Dim route = EffectRouteOf(vs, cpuRoute)
        Dim p As Fo4_Deferred_Shader_Class = Nothing
        If route.Surface.TryGetValue(vs, p) Then
            route.SurfaceErrors.TryGetValue(vs, errorText)
            Return p
        End If
#If DEBUG Then
        If _gbufferSource IsNot Nothing AndAlso _gateEffectSurfaceFragment Is Nothing Then Return _gbufferSource.EffectSurfaceProgram(t, cpuRoute, errorText)
#End If
        Dim fragment As String = Nothing
#If DEBUG Then
        fragment = _gateEffectSurfaceFragment
        If GateFailSurfaces Then
            errorText = "build failed (gate decal surfaces)"
            route.SurfaceErrors(vs) = errorText
            route.Surface(vs) = Nothing
            Return Nothing
        End If
#End If
        Dim cpu = route Is _effectCpu
        Dim ps As Fo4_Deferred_Shader_Class() = Nothing
        Select Case Pedir($"fxs|{cpu}|{vs}", {Nothing}, Function() {EffectSurfaceSpec(vs, cpu, fragment)}, ps, errorText)
            Case ProgramState.Built
                route.Surface(vs) = ps(0)
                Return ps(0)
            Case ProgramState.Failed
                route.SurfaceErrors(vs) = errorText
                route.Surface(vs) = Nothing
        End Select
        Return Nothing
    End Function

#If DEBUG Then
    ''' <summary>GATE ONLY (ShadowGate --program-census decal surfaces, S0 fase 7 rev-04): every decal base surface build (record and
    ''' effect) fails, as one that does not compile would.</summary>
    Friend Shared GateFailSurfaces As Boolean
#End If

    ''' <summary>The effect programs built so far on a vertex route (ShadowGate's program census).</summary>
    Friend Function BuiltEffectPrograms(Optional cpuRoute As Boolean = False) As IReadOnlyDictionary(Of (Vs As UInteger, Ps As UInteger), Fo4_Deferred_Shader_Class)
        Return If(cpuRoute, _effectCpu, _effectGpu).Programs
    End Function

    ''' <summary>The effect decals' base surface programs built so far on a vertex route, by VS (ShadowGate's program census).</summary>
    Friend Function BuiltEffectSurfacePrograms(Optional cpuRoute As Boolean = False) As IReadOnlyDictionary(Of UInteger, Fo4_Deferred_Shader_Class)
        Return If(cpuRoute, _effectCpu, _effectGpu).Surface
    End Function

#If DEBUG Then
    ''' <summary>GATE ONLY: a copy of these programs (GateWith) whose effect decals' base surface is drawn with <paramref name="fragment"/>,
    ''' built per VS on first use by the copy, which owns those (its Dispose releases them and nothing else).</summary>
    Friend Function GateWithEffectSurfaceFragment(fragment As String) As Fo4DeferredPrograms
        Dim c = New Fo4DeferredPrograms(Me, "DecalBase", Nothing)
        c._gateEffectSurfaceFragment = fragment
        Return c
    End Function
#End If
#If DEBUG Then
    Private _gateEffectSurfaceFragment As String
#End If

#If DEBUG Then
    ''' <summary>GATE ONLY: a copy of these programs (GateWith) whose effect programs are built with their vertex text passed through
    ''' <paramref name="vertex"/> (the gate declares the text it makes, Gl43.GateHarnessText), per technique on first use by the copy,
    ''' which owns those (its Dispose releases them and nothing else).</summary>
    Friend Function GateWithEffectVertex(vertex As Func(Of String, String)) As Fo4DeferredPrograms
        Dim c = New Fo4DeferredPrograms(Me, "DecalBase", Nothing)
        c._gateEffectVertex = vertex
        Return c
    End Function
#End If
#If DEBUG Then
    Private _gateEffectVertex As Func(Of String, String)
#End If

    ''' <summary>The decals' base surface program of technique <paramref name="t"/> on the draw's vertex route (built on its first use;
    ''' Nothing when the record has no program, when its tuple tessellates - Fo4GBufferSource.Tessellates, no message -, or when the
    ''' surface does not build, with the build's message in <paramref name="errorText"/>: remembered, as the records', and reported by
    ''' the draw - RenderableMesh.ReportDecalBaseSurfaceNotBuilt, S0 fase 7 rev-04).</summary>
    Friend Function DecalBaseSurfaceProgram(t As Fo4Technique, Optional cpuRoute As Boolean = False, Optional ByRef errorText As String = Nothing) As Fo4_Deferred_Shader_Class
        errorText = Nothing
        Dim e As String = Nothing
        If GBufferProgram(t, e, cpuRoute) Is Nothing Then Return Nothing
#If DEBUG Then
        If _gbufferSource IsNot Nothing Then Return If(_gateSurface, _gbufferSource.DecalBaseSurfaceProgram(t, cpuRoute, errorText))
#End If
        Dim key = t.Key()
        Dim route = RouteOf(key.Vs, cpuRoute)
        Dim p As Fo4_Deferred_Shader_Class = Nothing
        If route.Surface.TryGetValue(key, p) Then
            route.SurfaceErrors.TryGetValue(key, errorText)
            Return p
        End If
        ' Built on its first use (S0-A), as an effect's base surface (EffectSurfaceProgram). DIVERGENCE (ola 2 ensayo): the surface's
        ' law reads the VS layout (rows TEXCOORD0..2, uv TEXCOORD3.w / 4.w, rec2273 L103-105); a tessellated tuple's DS writes another
        ' layout (uv TEXCOORD0.xy, 29 of 69 DS without TEXCOORD4): no surface.
        If Fo4GBufferSource.Tessellates(key.Hs) Then
            route.Surface(key) = Nothing
            Return Nothing
        End If
#If DEBUG Then
        If GateFailSurfaces Then
            errorText = "build failed (gate decal surfaces)"
            route.SurfaceErrors(key) = errorText
            route.Surface(key) = Nothing
            Return Nothing
        End If
#End If
        Dim cpu = route Is _cpuRoute
        Dim ps As Fo4_Deferred_Shader_Class() = Nothing
        Select Case Pedir($"ds|{cpu}|{key}", {Nothing}, Function() {DecalSurfaceSpec(key, cpu)}, ps, errorText)
            Case ProgramState.Built
                route.Surface(key) = ps(0)
                Return ps(0)
            Case ProgramState.Failed
                route.SurfaceErrors(key) = errorText
                route.Surface(key) = Nothing
        End Select
        Return Nothing
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

    ''' <summary>THE RECORD'S PROGRAM WITHOUT ITS EARLY DEPTH TESTS, for a draw whose depth write has to follow the record's kill: the
    ''' record's own program, except for a record with forceEarlyDepthStencil, whose early depth write would keep the depth of the
    ''' pixels its PS kills (GL 4.3 14.9: with early tests the depth buffer is updated before the shader, a later discard does not
    ''' undo it): for those, the same record text without the early tests (Fo4GBufferSource.FragmentOf earlyTests:=False), built
    ''' with the record (GBufferProgram, route.Base). Nothing when the record's build failed (it is then a gap). Its readers
    ''' (RenderableMesh.RecordWithoutEarlyTests, under RenderableMesh.DrawsWithoutEarlyTests): the translucent decals' base pass
    ''' (decalBase: its kill decides which fragments give a base, as it decides which ones paint; 43 of the 143 records with technique
    ''' bit 15 have the early tests) and the G-buffer draw of a prepass shape or overlay layer in a frame without its prepass
    ''' (GateDisablePrepass, PreviewModel.FramePrepassRuns; the layers: ola 7h round 3, r2-01): in the game such
    ''' a shape draws the G-buffer without writing depth (mode 1), its depth is the prepass's; drawn writing its own, the early
    ''' write would keep its killed pixels (re23 3: e3's record PS 0x103 rec2802 is EarlyZ; ola 7h round 2, p7h-01).</summary>
    Friend Function RecordWithoutEarlyTests(t As Fo4Technique, Optional cpuRoute As Boolean = False) As Fo4_Deferred_Shader_Class
        Dim errorText As String = Nothing
        Dim program = GBufferProgram(t, errorText, cpuRoute)
        If program Is Nothing OrElse Not Fo4GBufferSource.Records(t.Key().Ps).EarlyZ Then Return program
#If DEBUG Then
        If _gbufferSource IsNot Nothing Then Return _gbufferSource.RecordWithoutEarlyTests(t, cpuRoute)
#End If
        ' F9 (r3 rev-12): the variant is published with its record (one request), so a built record has it; its absence is a defect.
        Dim baseProgram As Fo4_Deferred_Shader_Class = Nothing
        If Not RouteOf(t.Key().Vs, cpuRoute).Base.TryGetValue(t.Key(), baseProgram) Then
            Throw New InvalidOperationException($"G-buffer record {t.Key()}: built without its variant without early tests (r3 rev-12)")
        End If
        Return baseProgram
    End Function

#If DEBUG Then
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
#End If

#If DEBUG Then
    ''' <summary>GATE ONLY: on a GateWith copy, the decals' base surface drawn with <paramref name="program"/> for every technique.</summary>
    Friend Sub GateSetSurfaceProgram(program As Fo4_Deferred_Shader_Class)
        _gateSurface = program
    End Sub
#End If
#If DEBUG Then
    Private _gateSurface As Fo4_Deferred_Shader_Class
#End If

#If DEBUG Then
    ''' <summary>GATE ONLY (ShadowGate --fo4-deferred-law mutants): a copy of these programs with the one of role
    ''' <paramref name="role"/> (DirectResolve, DecalBase, PrepassRigid, PrepassAlpha) replaced (the light and composite programs:
    ''' GateWithLight / GateWithComposite; the effect decals' base surface: GateWithEffectSurfaceFragment). The closed list of roles is
    ''' checked in Debug only (rev-C4: a gate's own data, not a Release runtime check).
    ''' The copy owns nothing: its Dispose releases no program.</summary>
    Friend Function GateWith(role As String, program As Fo4_Deferred_Shader_Class) As Fo4DeferredPrograms
        Return New Fo4DeferredPrograms(Me, role, program)
    End Function
#End If

#If DEBUG Then
    ''' <summary>GATE ONLY: a copy of these programs whose light technique <paramref name="technique"/> draws with
    ''' <paramref name="program"/> (the copy owns nothing).</summary>
    Friend Function GateWithLight(technique As UInteger, program As Fo4_Deferred_Shader_Class) As Fo4DeferredPrograms
        Dim c = New Fo4DeferredPrograms(Me, "DecalBase", Nothing)
        c._gateLight = (technique, program)
        Return c
    End Function
#End If
#If DEBUG Then
    Private _gateLight As (Technique As UInteger, Program As Fo4_Deferred_Shader_Class)?
#End If

#If DEBUG Then
    ''' <summary>GATE ONLY: a copy of these programs whose composite technique <paramref name="technique"/> draws with
    ''' <paramref name="program"/> (the copy owns nothing).</summary>
    Friend Function GateWithComposite(technique As UInteger, program As Fo4_Deferred_Shader_Class) As Fo4DeferredPrograms
        Dim c = New Fo4DeferredPrograms(Me, "DecalBase", Nothing)
        c._gateComposite = (technique, program)
        Return c
    End Function
#End If
#If DEBUG Then
    Private _gateComposite As (Technique As UInteger, Program As Fo4_Deferred_Shader_Class)?
#End If

#If DEBUG Then
    ''' <summary>GATE ONLY: a copy of these programs whose image-space record <paramref name="rec"/> (a PS of the post's chain or the CS
    ''' 3860) draws with <paramref name="program"/> (ShadowGate --fo4-post-law / --fo4-camz: the probed program); the copy owns nothing.</summary>
    Friend Function GateWithImageSpace(rec As UInteger, program As Fo4_Deferred_Shader_Class) As Fo4DeferredPrograms
        Dim c = New Fo4DeferredPrograms(Me, "DecalBase", Nothing)
        c._gateImageSpace = (rec, program)
        Return c
    End Function
#End If
#If DEBUG Then
    Private _gateImageSpace As (Rec As UInteger, Program As Fo4_Deferred_Shader_Class)?
#End If

#If DEBUG Then
    ''' <summary>GATE ONLY: a copy of these programs whose utility technique <paramref name="technique"/> on the route
    ''' <paramref name="cpuRoute"/> draws with <paramref name="program"/> (ShadowGate --fo4-refraction-normals: the probed b07 program);
    ''' the copy owns nothing.</summary>
    Friend Function GateWithUtility(technique As UInteger, cpuRoute As Boolean, program As Fo4_Deferred_Shader_Class) As Fo4DeferredPrograms
        Dim c = New Fo4DeferredPrograms(Me, "DecalBase", Nothing)
        c._gateUtility = (technique, cpuRoute, program)
        Return c
    End Function
#End If
#If DEBUG Then
    Private _gateUtility As (Technique As UInteger, CpuRoute As Boolean, Program As Fo4_Deferred_Shader_Class)?
#End If

#If DEBUG Then
    Private Sub New(src As Fo4DeferredPrograms, role As String, program As Fo4_Deferred_Shader_Class)
#If DEBUG Then
        If Not {"DirectResolve", "DecalBase", "PrepassRigid", "PrepassAlpha"}.Contains(role) Then
            Throw New ArgumentException("no deferred program role " & role)
        End If
#End If
        ' The source's slots (a copy owns nothing: what they build is the source's), the role's replaced when a program is given; the
        ' source's builder (r4 rev-19: a GateWith copy inherits it).
        _builder = src._builder
        _requests = New ProgramRecordRequests(Of Fo4_Deferred_Shader_Class)(_builder, Function(id, sp) New Fo4_Deferred_Shader_Class(id, sp), _built)
        _directResolve = If(role = "DirectResolve" AndAlso program IsNot Nothing, LazyProgram(Of Fo4_Deferred_Shader_Class).Fixed(role, program), src._directResolve)
        _lightSum = src._lightSum
        _decalBase = If(role = "DecalBase" AndAlso program IsNot Nothing, LazyProgram(Of Fo4_Deferred_Shader_Class).Fixed(role, program), src._decalBase)
        _prepassRigid = If(role = "PrepassRigid" AndAlso program IsNot Nothing, LazyProgram(Of Fo4_Deferred_Shader_Class).Fixed(role, program), src._prepassRigid)
        _prepassAlpha = If(role = "PrepassAlpha" AndAlso program IsNot Nothing, LazyProgram(Of Fo4_Deferred_Shader_Class).Fixed(role, program), src._prepassAlpha)
        _gbufferSource = src
        _gateLight = src._gateLight
        _gateComposite = src._gateComposite
        _gateImageSpace = src._gateImageSpace
        _gateUtility = src._gateUtility
    End Sub
#End If

    ''' <summary>Every program these programs hold: the records built (_built) and the slots' (F9: a slot wraps its own program, not in
    ''' _built).</summary>
    Friend Function All() As IEnumerable(Of Shader_Base_Class)
        Return _built.Concat(Slots().Select(Function(s) s.BuiltProgram).Where(Function(p) p IsNot Nothing))
    End Function

    ''' <summary>The slots of the programs the frame takes by technique or role: the lights and composites of the closed lists and the
    ''' five app passes (All, ForgetFailures, Dispose; ShadowGate --program-census through GateStartSlots).</summary>
    Private Iterator Function Slots() As IEnumerable(Of IProgramSlot)
        For Each s In _lights.Values : Yield s : Next
        For Each s In _composites.Values : Yield s : Next
        For Each s As IProgramSlot In {_directResolve, _lightSum, _decalBase, _prepassRigid, _prepassAlpha}
            Yield s
        Next
    End Function

    ''' <summary>The retry of these programs (PreviewControl.ForgetProgramFailures, S0 fase 7 rev-08, only on a gesture): every slot
    ''' whose build failed forgets its failure (LazyProgram.Release), and every per-technique failure remembered with its error is
    ''' forgotten (S0 fase 7c: the records', the decal base surfaces', the position captures', the utility programs', the effects' and
    ''' the effect surfaces' - a transient failure there would otherwise stay until the process ends), so the next frame builds them
    ''' again. A surface that is Nothing by the declared divergence (a tessellated tuple: no error remembered) is kept.</summary>
    Friend Sub ForgetFailures()
        For Each s In Slots()
            If s.Failed Then s.Release()
        Next
        For Each route In {_gpuRoute, _cpuRoute}
            route.Errors.Clear()
            For Each k In route.SurfaceErrors.Keys.ToList() : route.Surface.Remove(k) : Next
            route.SurfaceErrors.Clear()
            For Each k In route.PositionErrors.Keys.ToList() : route.Position.Remove(k) : Next
            route.PositionErrors.Clear()
        Next
        For Each route In {_utilityGpu, _utilityCpu}
            route.Errors.Clear()
        Next
        For Each route In {_effectGpu, _effectCpu}
            route.Errors.Clear()
            For Each k In route.SurfaceErrors.Keys.ToList() : route.Surface.Remove(k) : Next
            route.SurfaceErrors.Clear()
        Next
        ' F9: the image-space failures (r2 table B) and the builder's own (a dead builder let go; a live one forgets its failed stages).
        _imageSpaceErrors.Clear()
        _builder.ForgetFailures()
    End Sub

#If DEBUG Then
    ''' <summary>GATE ONLY (ShadowGate --program-census, via PreviewControl.GateProgramSlots): the slots of the programs the frame takes
    ''' by technique or role, to build each one.</summary>
    Friend Function GateStartSlots() As List(Of IProgramSlot)
        Return Slots().ToList()
    End Function
#End If

    Friend Sub Dispose()
        For Each p In _built : p.Dispose() : Next
        _built.Clear()
        Dim propios = True
#If DEBUG Then
        propios = _gbufferSource Is Nothing   ' a GateWith copy shares its source's slots and owns nothing
#End If
        ' F9: the slots release their programs and let go of a request still in the builder (LazyProgram.Release).
        If propios Then
            For Each s In Slots() : s.Release() : Next
        End If
        ' F9: a record still in the builder is let go (its programs deleted when it finishes, ProgramBuilder.Abandon).
        _requests.AbandonAll()
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
    ''' <summary>The light's splits (EngineSunBox.Fo4Splits over the preview's camera; Valid only with a layer): cascade 0 and cascade 1,
    ''' and where cascade 0 ends (s_0, FadeDistances).</summary>
    Public ShadowFit As ShadowMapMath.LightFit
    Public ShadowFit1 As ShadowMapMath.LightFit
    Public SplitEnd0 As Single
    ''' <summary>The light's focus shadow (EngineSunBox.FocusFit; Valid only when the frame has it) and its layer of the frame's map.</summary>
    Public FocusFit As ShadowMapMath.LightFit
    Public FocusLayer As Integer
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
    ''' <summary>APP, the rig's Shadow Bias on the engine receiver (user decision 26-oct-2026): AppShadowBiasGrowth of the sun's PS = the
    ''' rig's deviation (PreviewLightRig.ShadowBiasDeviation, multiplier - 1; 0 = the game, as the other two). The receiver's line
    ''' rec3227 L1086 / rec3232 L1101 (ref = z - 0.275 / (far - near)) takes 0.275 (1 + growth) (Fo4DFLightSource.AppShadowBiasLines):
    ''' growth 0 is the game's line bit for bit, and so is a program whose uniform was never set (GL's 0).</summary>
    Public ShadowBiasGrowth As Single
End Structure

''' <summary>THE TARGETS AND THE NON-G-BUFFER PASSES OF THE FO4 DEFERRED FRAME (Tools/re-docs/RE_FO4_DEFERRED_FRAME_2026-10-03.md 1, 2,
''' 3, 4, 15, 17; ola 4: D:\WinTmp\rc\ola4\re.md 1.3, 1.4, 2.3): the G-buffer RTs 0x1A/0x1B/0x1D/0x1E/0x1F, the D3D-order
''' depth/stencil, the light RTs 0x21/0x22, RT 2, RT 0x28 with its mips (t15, CameraZAndMipsCS 3860). The whole frame up to the end
''' of the forward stage is in D3D row order (glClipControl UPPER_LEFT, propuesta A; composite 2 and the forward stage too since the
''' zfight-overlay fix, 8-oct-2026: one orientation for everything that tests against the G-buffer depth, as in the game): the stage
''' is opened and closed by its one owner, PreviewControl.BeginD3dRowsStage / EndD3dRowsStage (UPPER_LEFT, and the frame's targets
''' turned to GL rows at its end by FrameRowMirror), the same owner as Skyrim SE's engine frame.</summary>
Friend NotInheritable Class Fo4DeferredTargets
    Private _w As Integer, _h As Integer
    Private _albedo, _normal, _env, _gloss, _emit, _depth, _depthCopy, _lightD, _lightS, _rt2, _linZ As Integer
    ''' <summary>RT 0x28's mip count (LinearZLevels; RT 0x29 + i is its mip i).</summary>
    Private _linZLevels As Integer
    Private _fboG, _fboL, _fboC1 As Integer
    ''' <summary>A rig light with a focus shadow alone (RunLights): its diffuse and specular, and their framebuffer over the frame's depth.</summary>
    Private _lightOneD, _lightOneS, _fboOne As Integer
    ''' <summary>The translucent decals' base (BeginDecalBase / EndDecalBase), with its alpha and coverage targets.</summary>
    Private ReadOnly _base As New DecalBaseTarget(deferred:=True)
    ''' <summary>This frame's G-buffer stage drew the base (BeginDecalBase succeeded since BeginGBuffer): composite 2 takes the coverage
    ''' of its pixels from it and the direct resolve mixes by the coverage.</summary>
    Private _baseDrawn As Boolean

    ''' <summary>The G-buffer's colour targets (RT 0x1A, 0x1B, 0x1D, 0x1E, 0x1F, 0x1421F30B0).</summary>
    Friend Const GBufferTargetCount As Integer = 5
    ''' <summary>RT 0x1E's colour attachment in the G-buffer framebuffer (the order of GBufferTargetCount's list: o3).</summary>
    Private Const GlossTargetIndex As Integer = 3
    ''' <summary>RT 0x1E's clear alpha, 1.0 = id 255 (0x1421F310E mode 0, 0x1421F0BA0..BC1): THE value - BeginGBuffer clears with it
    ''' and Fo4ImageSpaceChain's t3 texel for a frame without a G-buffer stage stores it (UNORM8: 255).</summary>
    Friend Shared ReadOnly Property GlossClearAlpha As Single
        Get
#If DEBUG Then
            ' ShadowGate --fo4-h5-scene / --fo4-post-law mutant: RT 0x1E cleared with alpha 0 instead of the engine's 1.0.
            If Fo4GBufferConstants.GateMutantOn("h5-clear-0") Then Return 0.0F
#End If
            Return 1.0F
        End Get
    End Property
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
    ''' t9 = 1 (no AO: user decision 14-oct-2026 Q-AO, declared - the preview runs no SAO), t14 = 0 (no SSR: the SSLR chain's owner
    ''' is not traced, re.md H-9;
    ''' RE_FO4_DEFERRED_FRAME 17.5).</summary>
    Private _noAo, _noSsr As Integer
    Private Shared ReadOnly GBufferBuffers As DrawBuffersEnum() = {DrawBuffersEnum.ColorAttachment0, DrawBuffersEnum.ColorAttachment1,
        DrawBuffersEnum.ColorAttachment2, DrawBuffersEnum.ColorAttachment3, DrawBuffersEnum.ColorAttachment4}
    Private Shared ReadOnly LightBuffers As DrawBuffersEnum() = {DrawBuffersEnum.ColorAttachment0, DrawBuffersEnum.ColorAttachment1}

#If DEBUG Then
    ''' <summary>GATE ONLY (no UI): ShadowGate --fo4-deferred-scene mutant. True draws the G-buffer without FRAMEBUFFER_SRGB.</summary>
    Friend Shared GateNoGBufferSrgb As Boolean = False
#End If
#If DEBUG Then
    ''' <summary>GATE ONLY (no UI): ShadowGate --fo4-deferred-scene mutant. True blits the G-buffer depth mirrored in y (the row order of
    ''' the forward stage before the zfight-overlay fix).</summary>
    Friend Shared GateBlitMirror As Boolean = False
#End If
#If DEBUG Then
    ''' <summary>GATE ONLY (no UI): ShadowGate --fo4-decal-base mutant. True keeps the NEAREST translucent decal as the base (the
    ''' mark writes 1.0, the base draws LESS - RenderableMesh.ApplyDecalBaseState) instead of the farthest.</summary>
    Friend Shared GateDecalBaseNearest As Boolean = False
#End If
#If DEBUG Then
    ''' <summary>GATE ONLY (no UI): ShadowGate --decal-base-surface mutant "no mix" (review rev-02). True: the direct resolve (post
    ''' off) takes coverage 1 on every pixel (uCoverageBlend False) in a frame with the translucent decals' base.</summary>
    Friend Shared GateDirectResolveNoCoverage As Boolean = False
#End If

#If DEBUG Then
    ''' <summary>GATE ONLY: the textures of the deferred targets, by role (ShadowGate --fo4-deferred-law / --fo4-deferred-scene).</summary>
    Friend ReadOnly Property GateTextures As (Albedo As Integer, Normal As Integer, Env As Integer, Gloss As Integer, Emit As Integer,
                                              Depth As Integer, LightD As Integer, LightS As Integer, Rt2 As Integer,
                                              LinZ As Integer, DecalBase As Integer)
        Get
            Return (_albedo, _normal, _env, _gloss, _emit, _depth, _lightD, _lightS, _rt2, _linZ, _base.AuxTexture)
        End Get
    End Property
#End If

#If DEBUG Then
    ''' <summary>GATE ONLY: the samplers of the deferred passes (ShadowGate --fo4-light-law / --fo4-deferred-law: their states against
    ''' the exe's).</summary>
    Friend ReadOnly Property GateSamplers As (Point As Integer, Cube As Integer, ShadowCompare As Integer, Ao As Integer)
        Get
            Return (_point, _cubeSampler, _shadowCompare, _aoSampler)
        End Get
    End Property
#End If

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
        _lightOneD = Tex(SizedInternalFormat.R11fG11fB10f, w, h)
        _lightOneS = Tex(SizedInternalFormat.R11fG11fB10f, w, h)
        _fboOne = Gl43.CreateFramebuffer()
        Gl43.FramebufferTexture(_fboOne, FramebufferAttachment.ColorAttachment0, _lightOneD, 0)
        Gl43.FramebufferTexture(_fboOne, FramebufferAttachment.ColorAttachment1, _lightOneS, 0)
        Gl43.FramebufferTexture(_fboOne, FramebufferAttachment.DepthStencilAttachment, _depth, 0)
        ok = ok AndAlso Gl43.CheckFramebufferStatus(_fboOne, FramebufferTarget.Framebuffer) = FramebufferStatus.FramebufferComplete
        _fboC1 = Gl43.CreateFramebuffer()
        Gl43.FramebufferTexture(_fboC1, FramebufferAttachment.ColorAttachment0, _rt2, 0)
        Gl43.FramebufferTexture(_fboC1, FramebufferAttachment.DepthStencilAttachment, _depth, 0)
        ok = ok AndAlso Gl43.CheckFramebufferStatus(_fboC1, FramebufferTarget.Framebuffer) = FramebufferStatus.FramebufferComplete
        ok = ok AndAlso _base.Ensure(w, h)
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

    ''' <summary>Opens the G-buffer stage, inside the frame's D3D-order stage (PreviewControl.BeginD3dRowsStage set UPPER_LEFT, its one
    ''' owner; glFrontFace unchanged, propuesta A.2): every RT cleared (rev-29 b:
    ''' 0) except RT 0x1E's alpha, cleared to 1.0 = id 255 as the engine's clear (0x1421F310E mode 0, 0x1421F0BA0..BC1; its rgb,
    ''' Sky + 0x138, is a declared hole the preview never reads where no shape drew), depth 1 / stencil 0 (0x1421F3151..0x1421F3164),
    ''' sRGB writes on (rev-17).</summary>
    Public Sub BeginGBuffer()
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _fboG)
        GL.Viewport(0, 0, _w, _h)
        GL.ColorMask(True, True, True, True)
        GL.DepthMask(True)
        Gl43.DrawBuffers(_fboG, GBufferTargetCount, GBufferBuffers)
        For i = 0 To GBufferTargetCount - 1
            Gl43.ClearFramebuffer(_fboG, ClearBuffer.Color, i, New Single() {0.0F, 0.0F, 0.0F, If(i = GlossTargetIndex, GlossClearAlpha, 0.0F)})
        Next
        Gl43.ClearFramebuffer(_fboG, ClearBufferCombined.DepthStencil, 0, 1.0F, 0)
        _baseDrawn = False
        GL.Enable(EnableCap.FramebufferSrgb)
#If DEBUG Then
        If GateNoGBufferSrgb Then GL.Disable(EnableCap.FramebufferSrgb)
#End If
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
    ''' every frame: RT 0x28 is the composite's input whatever the post does (the SAO's own passes, slots 1..9, are not run: user
    ''' decision 14-oct-2026 Q-AO, no AO).</summary>
    ''' <summary>The record of CameraZAndMipsCS (RunCameraZ): the post's one image-space program outside its chain
    ''' (Fo4ImageSpaceChain.Records); ParityGate program-catalog-domain reads it as part of the domain.</summary>
    Friend Const RecCameraZ As UInteger = 3860UI

    Private Sub RunCameraZ(progs As Fo4DeferredPrograms, near As Single, far As Single)
        Dim p = progs.ImageSpaceProgram(RecCameraZ)
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
                                 {(_albedo, False), (_normal, False), (_env, False), (_gloss, False), (_emit, False)})
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

#If DEBUG Then
    ''' <summary>GATE ONLY (no UI): ShadowGate --fo4-light-law mutant. True draws the ambient in its own pass (0x20000 rec3107) after the
    ''' lights instead of fused into the first one.</summary>
    Friend Shared GateAmbientApart As Boolean = False
    ''' <summary>GATE ONLY (ShadowGate [dos-luces]): every light of the frame through the same operations whether it casts or not (alone, its focus pass when it has one, summed; the ambient
    ''' apart): each is a monotone rounding of its input, so more casters never lighten a channel, exactly. The route the frame takes (RunLights) differs only by rounding, [ambient-route].</summary>
    Friend Shared GateSameLightRoute As Boolean = False
#End If

    ''' <summary>gates-v6 (rev-E16): the light techniques the last RunLights could not draw (no built program,
    ''' Fo4DeferredPrograms.LightProgram): the frame's notice reports them (PreviewControl.FinishFo4Deferred).</summary>
    Friend ReadOnly SkippedLightTechniques As New List(Of (Technique As UInteger, Light As Integer))

    ''' <summary>gates-v6 (rev-E16 / E19): the frame's notice of the lights RunLights could not draw - one line per technique (the
    ''' reason), each light once under it ("rig light N"; the ambient alone "rig ambient"), PreviewGap. The one statement
    ''' PreviewControl.FinishFo4Deferred and the gate use.</summary>
    Friend Shared Sub ReportSkippedLights(model As PreviewModel, skipped As IEnumerable(Of (Technique As UInteger, Light As Integer)))
        If model Is Nothing Then Return
        For Each sk In skipped
            ' The focus technique missing leaves the light drawn without its focus shadow (RunLights: the light's own program still draws it); any other
            ' missing technique leaves the light out.
            Dim efecto = If(sk.Technique = Fo4DeferredLightLaw.FocusTechnique, "the light is drawn without its focus shadow", "the light is not drawn")
            model.ReportUndrawn(If(sk.Light < 0, "rig ambient", $"rig light {sk.Light + 1}"), $"Light technique 0x{sk.Technique:X8} has no program: {efecto}", PreviewModel.FrameNoticeId.RigLightWithoutProgram)
        Next
    End Sub

    ''' <summary>The light stage (DF 3, 15.5; ola 4): RT 0x21/0x22 cleared to 0, then every rig light as a directional light with the
    ''' sun's technique of the exe's Settings (Fo4DeferredLightLaw.SunTechnique: with its shadow map when it casts), the ambient fused
    ''' into the first one (AMBIENT, 0x1421F5E9F..ECB: the preview has no tiled lighting, no entry in the list 0x143E62198 and no
    ''' [ssn + 0x150]) - or, with no light, alone (0x20000 rec3107, 0x1421F739E..7470) -, additive. A light that casts: t5 = the view of
    ''' its two layers of the frame's map (ShadowMapTarget.SplitsView: cascades 0 and 1), s5 = the comparison sampler, and the
    ''' receiver's constants of Fo4DeferredLightLaw (ShadowMapProj rows 0..5, ShadowWorldScale 0..1, ShadowSampleParam, FadeDistances,
    ''' ShadowFadeParam) of both cascades (EngineSunBox; user decision 6-nov-2026). APP (rev-E08): the rig's Darkness (AppShadowLift) and Softness (the radius
    ''' scale in ShadowSampleParam.z), the game's values for the default rig; the Shadow Bias (AppShadowBiasGrowth, 26-oct-2026).</summary>
#If DEBUG Then
    ''' <summary>DEBUG ONLY (ShadowGate --sun-box-law [receiver]): each sun pass that casts, as the GL holds it before its draw (GateFo4SunLight).
    ''' Nothing = no capture.</summary>
    Friend Shared GateFo4SunLights As List(Of GateFo4SunLight) = Nothing
#End If

    ''' <summary>THE FOCUS SHADOW'S PASS over a light's targets (0x1421F6530..69E2, after the sun): technique Fo4DeferredLightLaw.FocusTechnique, rec3271 with the focus map as its t5
    ''' and cascade 0 (SplitDistances: .y = [pass + 0x4D], .z = that + 1, 0x142247D30..56, written 0x14224828B; the pass's bytes 0x4D.. are 0, 0x1421F671C), blend mode 3 = DEST_COLOR / ZERO (0x1421F68DF, table 0x141855271: the factor multiplies the light
    ''' the sun left), the two targets (RT 0x21, 0x22, 0x1421F661E..654). The engine bounds it with a volume (special stencil states 1 and 2, the sphere of the bound + 2000 along the
    ''' light, 0x14226F950..9B1); in the preview all that is seen receives it (user decision 8-nov-2026): the whole screen. Its constants as the sun's, with the rig's knobs.</summary>
    Private Sub DrawFocusPass(p As Shader_Base_Class, f As Fo4DeferredFrame, light As Fo4DeferredLight)
        p.Use()
        p.SetVector4("SplitDistances", New Vector4(0.0F, 0.0F, 1.0F, 0.0F))
        p.SetVector4Array("ShadowMapProj", Fo4DeferredLightLaw.ShadowMapProjRows(light.FocusFit.ViewProj, f.View, f.Eye))
        p.SetVector4Array("ShadowWorldScale", {Fo4DeferredLightLaw.ShadowWorldScale(light.FocusFit)})
        Dim lift = f.ShadowLift, scale = 1.0F + f.ShadowRadiusGrowth, biasGrowth = f.ShadowBiasGrowth
#If DEBUG Then
        If Fo4GBufferConstants.GateMutantOn("rig-knobs-off") Then lift = 0.0F : scale = 1.0F : biasGrowth = 0.0F
        If Fo4GBufferConstants.GateMutantOn("shadow-bias-off") Then biasGrowth = 0.0F
#End If
        p.SetVector4("ShadowSampleParam", Fo4DeferredLightLaw.ShadowSampleParam(f.ShadowMap.Size, scale))
        p.SetFloat("AppShadowLift", lift)
        p.SetFloat("AppShadowBiasGrowth", biasGrowth)
        p.SetVector4("ShadowFadeParam", Fo4DeferredLightLaw.ShadowFadeParam())
        Gl43.BindTextureUnit(5, TextureTarget.Texture2DArray, f.ShadowMap.LayerView(light.FocusLayer))
        GL.BindSampler(5, _shadowCompare)
#If DEBUG Then
        GateFo4FocusPasses += 1
#End If
        GL.BlendFuncSeparate(BlendingFactorSrc.DstColor, BlendingFactorDest.Zero, BlendingFactorSrc.One, BlendingFactorDest.Zero)
#If DEBUG Then
        ' ShadowGate full [dos-luces]: the factor lightens by 3 % of what is there.
        If Fo4GBufferConstants.GateMutantOn("focus-pass-gain") Then
            GL.BlendColor(0.03F, 0.03F, 0.03F, 0.03F)
            GL.BlendFuncSeparate(BlendingFactorSrc.DstColor, BlendingFactorDest.ConstantColor, BlendingFactorSrc.One, BlendingFactorDest.Zero)
        End If
#End If
        DrawQuad(p, f)
        GL.BlendFunc(BlendingFactor.One, BlendingFactor.One)
        Gl43.UnbindTextureUnit(5) : GL.BindSampler(5, 0)
    End Sub

#If DEBUG Then
    ''' <summary>GATE: the focus passes drawn since a gate reset it (ShadowGate --ray-law [focus-present]).</summary>
    Friend Shared GateFo4FocusPasses As Integer
    ''' <summary>GATE (ShadowGate --ray-law): each casting light's diffuse with its shadow and without (GateLightFactor). Nothing = no capture.</summary>
    Friend Shared GateLightFactors As Dictionary(Of Integer, GateLightFactor) = Nothing

    ''' <summary>GATE (ShadowGate [ambient-route]): the two light accumulators (RT 0x21 / 0x22, R11G11B10_FLOAT) of the last frame, their values as stored.</summary>
    Friend Function GateReadAccumulators() As Single()()
        Dim r(1)() As Single
        Dim prevRead = GL.GetInteger(GetPName.ReadFramebufferBinding)
        GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, _fboL)
        GL.PixelStore(PixelStoreParameter.PackAlignment, 1)
        For k = 0 To 1
            ReDim r(k)(_w * _h * 3 - 1)
            GL.ReadBuffer(If(k = 0, ReadBufferMode.ColorAttachment0, ReadBufferMode.ColorAttachment1))
            GL.ReadPixels(0, 0, _w, _h, OpenTK.Graphics.OpenGL4.PixelFormat.Rgb, PixelType.Float, r(k))
        Next
        GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, prevRead)
        Return r
    End Function

    Private Function GateLeerUno() As Single()
        Dim rgb(_w * _h * 3 - 1) As Single
        Dim prevRead = GL.GetInteger(GetPName.ReadFramebufferBinding)
        GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, _fboOne)
        GL.ReadBuffer(ReadBufferMode.ColorAttachment0)
        GL.PixelStore(PixelStoreParameter.PackAlignment, 1)
        GL.ReadPixels(0, 0, _w, _h, OpenTK.Graphics.OpenGL4.PixelFormat.Rgb, PixelType.Float, rgb)
        GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, prevRead)
        Dim r(_w * _h - 1) As Single
        For i = 0 To r.Length - 1 : r(i) = rgb(i * 3) + rgb(i * 3 + 1) + rgb(i * 3 + 2) : Next
        Return r
    End Function
#End If

    ''' <summary>A rig light's isolated targets added to the frame's (the engine has one sun, so its focus pass multiplies just its light; the rig has four: each light is drawn
    ''' and multiplied alone, then summed, the additive state of the lights).</summary>
    Private Sub SumLightOne(progs As Fo4DeferredPrograms)
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _fboL)
        Gl43.DrawBuffers(_fboL, 2, LightBuffers)
        GL.Disable(EnableCap.DepthTest)
        progs.LightSum.Use()
        Gl43.BindTextureUnit(6, TextureTarget.Texture2D, _lightOneD) : Gl43.BindTextureUnit(7, TextureTarget.Texture2D, _lightOneS)
        GL.BindVertexArray(_emptyVao) : GL.DrawArrays(PrimitiveType.Triangles, 0, 3) : GL.BindVertexArray(0)
        Gl43.UnbindTextureUnit(6) : Gl43.UnbindTextureUnit(7)
        GL.Enable(EnableCap.DepthTest)
    End Sub

    Public Sub RunLights(progs As Fo4DeferredPrograms, f As Fo4DeferredFrame)
        SkippedLightTechniques.Clear()
#If DEBUG Then
        GateFo4FocusPasses = 0
#End If
        Dim lightIndex = -1
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _fboL)
        Gl43.DrawBuffers(_fboL, 2, LightBuffers)
        GL.ColorMask(True, True, True, True)
        Gl43.ClearFramebuffer(_fboL, ClearBuffer.Color, 0, New Single() {0, 0, 0, 0})
        Gl43.ClearFramebuffer(_fboL, ClearBuffer.Color, 1, New Single() {0, 0, 0, 0})
        LightState()
        BindGBufferInputs()
        Dim ambientDrawn = False
        ' The ambient is fused into the first light (bit 17) only with no focus shadow in the frame ([0x142F97B44] .data 1 and the focus count [0x143E621A8], 0x1421F5EA9..EB9):
        ' with one it goes apart after them, so the focus factor multiplies the direct light alone.
        Dim focoEnElCuadro = f.ShadowMap IsNot Nothing AndAlso f.Lights.Any(Function(l) l.ShadowLayer >= 0 AndAlso l.FocusFit.Valid)
        Dim hayLuz = False
        For Each light In f.Lights
            Dim shadow = light.ShadowLayer >= 0 AndAlso f.ShadowMap IsNot Nothing
            Dim fuse = Not ambientDrawn AndAlso Not focoEnElCuadro
#If DEBUG Then
            If GateAmbientApart OrElse GateSameLightRoute Then fuse = False
#End If
            lightIndex += 1
            Dim tech = Fo4DeferredLightLaw.SunTechnique(shadow, fuse)
            Dim lightState As ProgramState
            Dim p = progs.LightProgram(tech, lightState)
            ' F9 (r2 rev-01): a light still being built leaves the frame pending (not presented): not a skipped light, no notice
            If lightState = ProgramState.Pending Then Continue For
            If p Is Nothing Then SkippedLightTechniques.Add((tech, lightIndex)) : Continue For
            ' The light with a focus shadow: drawn alone, multiplied, summed (DrawFocusPass, SumLightOne).
            Dim foco As Shader_Base_Class = Nothing
            If shadow AndAlso light.FocusFit.Valid Then
                Dim focoState As ProgramState
                foco = progs.LightProgram(Fo4DeferredLightLaw.FocusTechnique, focoState)
                If focoState = ProgramState.Pending Then Continue For
                If foco Is Nothing Then SkippedLightTechniques.Add((Fo4DeferredLightLaw.FocusTechnique, lightIndex))
            End If
            ' Alone only with light already in the frame's targets: the first light is alone in them (cleared, the ambient goes after), and x + 0 is x.
            Dim aislada = foco IsNot Nothing AndAlso hayLuz
#If DEBUG Then
            If GateLightFactors IsNot Nothing Then aislada = foco IsNot Nothing
            If GateSameLightRoute Then aislada = True
#End If
            If aislada Then
                GL.BindFramebuffer(FramebufferTarget.Framebuffer, _fboOne)
                Gl43.DrawBuffers(_fboOne, 2, LightBuffers)
                Gl43.ClearFramebuffer(_fboOne, ClearBuffer.Color, 0, New Single() {0, 0, 0, 0})
                Gl43.ClearFramebuffer(_fboOne, ClearBuffer.Color, 1, New Single() {0, 0, 0, 0})
            End If
            hayLuz = True
            p.Use()
            p.SetVector4("LightVector", New Vector4(light.Dir, 0.0F))
            p.SetVector4("LightColor", New Vector4(light.Color, 0.0F))
            If fuse Then p.SetVector4Array("DirectionalAmbient", f.Ambient)
            If shadow Then
                Dim proj = Fo4DeferredLightLaw.ShadowMapProjRows(light.ShadowFit.ViewProj, f.View, f.Eye).
                           Concat(Fo4DeferredLightLaw.ShadowMapProjRows(light.ShadowFit1.ViewProj, f.View, f.Eye)).ToArray()
                Dim worldScale = {Fo4DeferredLightLaw.ShadowWorldScale(light.ShadowFit), Fo4DeferredLightLaw.ShadowWorldScale(light.ShadowFit1)}
                Dim splitEnd0 = light.SplitEnd0
                Dim view = f.ShadowMap.SplitsView(light.ShadowLayer)
#If DEBUG Then
                ' ShadowGate --sun-box-law [receiver]: one cascade, as before the sun box (rows 0..2, its scale, FadeDistances of the far, the layer's view).
                If Fo4GBufferConstants.GateMutantOn("sun-fo4-one-cascade") Then
                    proj = proj.Take(3).ToArray() : worldScale = {worldScale(0)} : splitEnd0 = f.Far
                    view = f.ShadowMap.LayerView(f.ShadowMap.FirstLayer(light.ShadowLayer))
                End If
#End If
                p.SetVector4Array("ShadowMapProj", proj)
                p.SetVector4Array("ShadowWorldScale", worldScale)
                Dim lift = f.ShadowLift, scale = 1.0F + f.ShadowRadiusGrowth, biasGrowth = f.ShadowBiasGrowth
#If DEBUG Then
                ' ShadowGate --fo4-rig-knobs's mutant: the rig's knobs do not reach the engine receiver.
                If Fo4GBufferConstants.GateMutantOn("rig-knobs-off") Then lift = 0.0F : scale = 1.0F : biasGrowth = 0.0F
                ' ShadowGate --fo4-light-law / --fo4-rig-knobs: the Shadow Bias does not reach the shaders (Render's read has the same switch).
                If Fo4GBufferConstants.GateMutantOn("shadow-bias-off") Then biasGrowth = 0.0F
#End If
                p.SetVector4("ShadowSampleParam", Fo4DeferredLightLaw.ShadowSampleParam(f.ShadowMap.Size, scale))
                p.SetFloat("AppShadowLift", lift)
                p.SetFloat("AppShadowBiasGrowth", biasGrowth)
                p.SetVector4("FadeDistances", Fo4DeferredLightLaw.FadeDistances(f.Near, f.Far, splitEnd0))
                p.SetVector4("ShadowFadeParam", Fo4DeferredLightLaw.ShadowFadeParam())
                Gl43.BindTextureUnit(5, TextureTarget.Texture2DArray, view)
                GL.BindSampler(5, _shadowCompare)
#If DEBUG Then
                If GateFo4SunLights IsNot Nothing Then GateFo4SunLights.Add(GateFo4SunLight.Capture(p.ProgramId, lightIndex, light.ShadowLayer, view))
#End If
            End If
            DrawQuad(p, f)
            If shadow Then Gl43.UnbindTextureUnit(5) : GL.BindSampler(5, 0)
            If foco IsNot Nothing Then
#If DEBUG Then
                ' ShadowGate --ray-law: the same light without its shadow first (Darkness 0 on the sun's PS, no focus pass), then the frame's own.
                If GateLightFactors IsNot Nothing Then
                    Dim lf As New GateLightFactor
                    Gl43.BindTextureUnit(5, TextureTarget.Texture2DArray, f.ShadowMap.SplitsView(light.ShadowLayer))
                    GL.BindSampler(5, _shadowCompare)
                    p.Use()
                    p.SetFloat("AppShadowLift", 1.0F)
                    Gl43.ClearFramebuffer(_fboOne, ClearBuffer.Color, 0, New Single() {0, 0, 0, 0})
                    Gl43.ClearFramebuffer(_fboOne, ClearBuffer.Color, 1, New Single() {0, 0, 0, 0})
                    DrawQuad(p, f)
                    lf.Without = GateLeerUno()
                    p.SetFloat("AppShadowLift", f.ShadowLift)
                    Gl43.ClearFramebuffer(_fboOne, ClearBuffer.Color, 0, New Single() {0, 0, 0, 0})
                    Gl43.ClearFramebuffer(_fboOne, ClearBuffer.Color, 1, New Single() {0, 0, 0, 0})
                    DrawQuad(p, f)
                    Gl43.UnbindTextureUnit(5) : GL.BindSampler(5, 0)
                    DrawFocusPass(foco, f, light)
                    lf.WithShadow = GateLeerUno()
                    GateLightFactors(lightIndex) = lf
                    GateFo4FocusPasses -= 1
                    Gl43.BindTextureUnit(5, TextureTarget.Texture2DArray, f.ShadowMap.SplitsView(light.ShadowLayer))
                    GL.BindSampler(5, _shadowCompare)
                    p.Use()
                    Gl43.ClearFramebuffer(_fboOne, ClearBuffer.Color, 0, New Single() {0, 0, 0, 0})
                    Gl43.ClearFramebuffer(_fboOne, ClearBuffer.Color, 1, New Single() {0, 0, 0, 0})
                    DrawQuad(p, f)
                    Gl43.UnbindTextureUnit(5) : GL.BindSampler(5, 0)
                End If
#End If
                DrawFocusPass(foco, f, light)
            End If
            If aislada Then SumLightOne(progs)
            ambientDrawn = ambientDrawn OrElse fuse
        Next
        If Not ambientDrawn Then
            Dim ambientState As ProgramState
            Dim a = progs.LightProgram(Fo4DeferredLightLaw.AmbientTechnique, ambientState)
            If a Is Nothing Then
                ' F9 (r2 rev-01): pending is not skipped (the frame is pending)
                If ambientState <> ProgramState.Pending Then SkippedLightTechniques.Add((Fo4DeferredLightLaw.AmbientTechnique, -1))
            Else
                a.Use()
                a.SetVector4Array("DirectionalAmbient", f.Ambient)
#If DEBUG Then
                ' ShadowGate full [ambient-route]: the ambient apart is not the fused one.
                If Fo4GBufferConstants.GateMutantOn("ambient-apart-gain") Then a.SetVector4Array("DirectionalAmbient", f.Ambient.Select(Function(v) v * 1.25F).ToArray())
#End If
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
        ' F9 (r2 rev-01): a composite still being built: the pass is not drawn, the frame is pending (not presented)
        If p Is Nothing Then
            GL.ColorMask(True, True, True, True)
            Return
        End If
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
#If DEBUG Then
        If GateBlitMirror Then Gl43.BlitFramebuffer(_fboG, targetFbo, 0, 0, _w, _h, 0, _h, _w, 0, ClearBufferMask.DepthBufferBit Or ClearBufferMask.StencilBufferBit, BlitFramebufferFilter.Nearest) : Return
#End If
        Gl43.BlitFramebuffer(_fboG, targetFbo, 0, 0, _w, _h, 0, 0, _w, _h,
                                ClearBufferMask.DepthBufferBit Or ClearBufferMask.StencilBufferBit, BlitFramebufferFilter.Nearest)
    End Sub

    ''' <summary>RT 0x1E (the G-buffer's gloss / material id, D3D rows: the end of the D3D-order stage does not turn it) mirrored by
    ''' <paramref name="rows"/> (the control's FrameRowMirror) into <paramref name="dst"/>, an RGBA8 texture of the frame's size, in GL
    ''' rows like the scene the post reads: the tonemap's t3 (Fo4ImageSpaceChain.Run, 0x14220A587..5A7). RT 0x1E itself stays in D3D
    ''' rows.</summary>
    Friend Sub CopyGlossGlRows(rows As FrameRowMirror, dst As Integer)
#If DEBUG Then
        ' ShadowGate --fo4-h5-scene mutant: the copy without turning the rows (RT 0x1E's D3D rows under the scene's GL rows).
        If Fo4GBufferConstants.GateMutantOn("h5-gloss-nomirror") Then
            GL.CopyImageSubData(_gloss, ImageTarget.Texture2D, 0, 0, 0, 0, dst, ImageTarget.Texture2D, 0, 0, 0, 0, _w, _h, 1)
            Return
        End If
#End If
        rows.CopyMirrored(_gloss, ImageTarget.Texture2D, dst, SizedInternalFormat.Rgba8)
    End Sub

    ''' <summary>Composite pass 2 into the scene target (D3D rows, still UPPER_LEFT: GREATER at z = 1 against the G-buffer depth
    ''' blitted straight; draw buffers {A0, A1} = radiance and coverage, rev-19): the technique of the exe's Settings
    ''' (Fo4DeferredLightLaw.CompositePass2: 0xB60 rec3495 with the post, 0x160 rec3505 without). Inputs (SetupTechnique 0x142216AC0
    ''' and SetupGeometry 0x1422174F0, DF 4.2, RE_SAO_BOTH 2.6): t1 RT 0x1B, t2 RT 0x1D, t3 RT 0x1E, t4 RT 0x1F, t5 RT 0x21, t6 RT
    ''' 0x22, t7 the depth, t8 the envmap array, t10 RT 2, t15 RT 0x28; with APPLY_AO t9 = RT 0x27 (CLAMP, bilinear) and with SSLR
    ''' t14 = RT 7 - the preview binds 1 and 0 there (_noAo: no AO, user decision 14-oct-2026 Q-AO; _noSsr: re.md H-9, the frame as before). Constants:
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
        ' F9 (r2 rev-01): a composite still being built: the pass is not drawn, the frame is pending (not presented)
        If p Is Nothing Then Return
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
        Dim mezcla = _baseDrawn
#If DEBUG Then
        If GateDirectResolveNoCoverage Then mezcla = False
#End If
        progs.DirectResolve.SetBool("uCoverageBlend", mezcla)
        Gl43.BindTextureUnit(0, TextureTarget.Texture2D, sceneTex) : Gl43.BindTextureUnit(1, TextureTarget.Texture2D, coverageTex)
        GL.BindVertexArray(_emptyVao) : GL.DrawArrays(PrimitiveType.Triangles, 0, 3) : GL.BindVertexArray(0)
        Gl43.UnbindTextureUnit(0) : Gl43.UnbindTextureUnit(1) : GL.UseProgram(0)
        GL.Disable(EnableCap.Blend)
        GL.DepthMask(True) : GL.DepthFunc(DepthFunction.Lequal) : GL.Enable(EnableCap.CullFace)
    End Sub

    Public Sub Free()
        For Each fb In {_fboG, _fboL, _fboC1, _fboOne}
            If fb <> 0 Then GL.DeleteFramebuffer(fb)
        Next
        _fboG = 0 : _fboL = 0 : _fboC1 = 0 : _fboOne = 0
        For Each t In {_albedo, _normal, _env, _gloss, _emit, _depth, _depthCopy, _lightD, _lightS, _rt2, _linZ, _noAo, _noSsr, _lightOneD, _lightOneS}
            If t <> 0 Then GL.DeleteTexture(t)
        Next
        _noAo = 0 : _noSsr = 0
        _albedo = 0 : _normal = 0 : _env = 0 : _gloss = 0 : _emit = 0 : _depth = 0 : _depthCopy = 0
        _lightD = 0 : _lightS = 0 : _rt2 = 0 : _linZ = 0 : _linZLevels = 0 : _lightOneD = 0 : _lightOneS = 0
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
