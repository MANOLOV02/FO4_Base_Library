Imports OpenTK.Graphics.OpenGL4
Imports OpenTK.Mathematics

''' <summary>THE FO4 IMAGE SPACE OF THE DEFAULT ROW OF THE PREVIEW'S WEATHER TABLE (PreviewImagingTable shows the row the
''' user picks): the post-process constants of one IMGS record.
''' <para>Canonical choice of the preview (a static studio has no weather): the DAY image space of the vanilla
''' weather CommonwealthClear (WTHR 0x0002B52A), IMGS <c>CW_ClearDAY_MAY19</c> (0x00216A9C) of Fallout4.esm.
''' Every value below is that record's data, read from the plugin (field order of HNAM per xEdit's IMGS
''' definition, mapped to the ImageSpaceManager offsets the engine reads, 0x14220A730):</para>
''' <list type="bullet">
''' <item>HNAM = (EyeAdaptSpeed 3.0, TonemapE 0.02, BloomThreshold 0.5, BloomScale 0.2, AutoExposureMax 3.25,
''' AutoExposureMin 1.6, SunlightScale 4.5, SkyScale 2.4, MiddleGray 0.18) - ISM+0x80..0xA0.</item>
''' <item>CNAM = (Saturation 1, Brightness 1, Contrast 1) - ISM+0xA4/0xA8/0xAC; TNAM tint amount 0 - ISM+0xB0.</item>
''' <item>TX00 = <c>Textures\Effects\LUTs\LUT_CW_ClearDAY.DDS</c> (Fallout4 - Misc.ba2; 115 of the 293 vanilla
''' IMGS use it).</item>
''' </list>
''' <para>Holes, declared and not filled: bloom (the preview has no bloom pass: 0), the weights of the four LUT
''' slots (one LUT, weight 1), and the eye-adaptation history (steady state: adapted = current mean).</para></summary>
Friend NotInheritable Class Fo4ImageSpace
    Public ReadOnly TonemapE As Single
    Public ReadOnly AutoExposureMax As Single
    Public ReadOnly AutoExposureMin As Single
    Public ReadOnly MiddleGray As Single
    ''' <summary>HNAM SunlightScale (ISM+0x98): scales the sun's DLightColor (0x1422265DA..643).</summary>
    Public ReadOnly SunlightScale As Single
    Public ReadOnly Saturation As Single
    Public ReadOnly Brightness As Single
    Public ReadOnly Contrast As Single
    Public ReadOnly TintColor As Vector3
    Public ReadOnly TintAmount As Single
    Public ReadOnly LutPath As String

    Friend Sub New(tonemapE As Single, aeMax As Single, aeMin As Single, middleGray As Single, sunlightScale As Single,
                    saturation As Single, brightness As Single, contrast As Single,
                    tintColor As Vector3, tintAmount As Single, lutPath As String)
        Me.TonemapE = tonemapE : AutoExposureMax = aeMax : AutoExposureMin = aeMin : Me.MiddleGray = middleGray
        Me.SunlightScale = sunlightScale
        Me.Saturation = saturation : Me.Brightness = brightness : Me.Contrast = contrast
        Me.TintColor = tintColor : Me.TintAmount = tintAmount : Me.LutPath = lutPath
    End Sub

    ''' <summary>IMGS CW_ClearDAY_MAY19 (Fallout4.esm 0x00216A9C). See the class summary.</summary>
    Public Shared ReadOnly PreviewDay As New Fo4ImageSpace(
        tonemapE:=0.02F, aeMax:=3.25F, aeMin:=1.6F, middleGray:=0.18F, sunlightScale:=4.5F,
        saturation:=1.0F, brightness:=1.0F, contrast:=1.0F,
        tintColor:=Vector3.Zero, tintAmount:=0.0F,
        lutPath:="Textures\Effects\LUTs\LUT_CW_ClearDAY.DDS")

    ''' <summary>The constants of PS i3700 (cb2[1..3]) for this image space.</summary>
    Public Sub Upload(post As Shader_Base_Class)
        post.SetVector4("hdrExposure", New Vector4(AutoExposureMax, AutoExposureMin, MiddleGray, EffectiveTonemapE))
        post.SetVector3("hdrCinematic", New Vector3(Saturation, Brightness, Contrast))
        ' cb2[3] of i3700 = (TintColor * TintAmount, TintAmount): the engine premultiplies when it copies the image
        ' space into the ISM (0x1421efcd0, rcx = ISM+0x80 from 0x142186312) and uploads +0xB4..+0xBC, +0xB0
        ' (SetVector(2) at 0x14220a8f2). Fallout4.exe 1.11.240.0.
        post.SetVector4("hdrTint", New Vector4(TintColor * TintAmount, TintAmount))
    End Sub

    ''' <summary>The E the tonemap receives: the record's TonemapE when it is &lt;= 1, else 0.02
    ''' (0x14220A7B1..A838; 27 vanilla IMGS carry E &gt; 1 and get 0.02).</summary>
    Public ReadOnly Property EffectiveTonemapE As Single
        Get
            Return If(TonemapE <= 1.0F, TonemapE, 0.02F)
        End Get
    End Property
End Class

''' <summary>THE FO4 WEATHER OF THE DEFAULT ROW OF THE PREVIEW'S WEATHER TABLE (PreviewImagingTable), for what the effect
''' shader reads from it.
''' <para>Canonical choice of the preview: WTHR CommonwealthClear (Fallout4.esm 0x0002B52A), DAY, whose image
''' space is <see cref="Fo4ImageSpace.PreviewDay"/>. Its Effect Lighting colour for the day is (150, 150, 150) and its
''' Sunlight (225, 225, 225).</para>
''' <para>DLightColor (cb of the BSEffect PS, the LIGHTING technique's directional term) =
''' powf(sun colour, 2.2) * sun dimmer * ImageSpace SunlightScale (0x1422265DA..643, light+0x17C..0x184, +0x144,
''' ISM+0x98); the sun colour of an effect is the weather's Effect Lighting. Hole, declared: the dimmer
''' (light+0x144) is not traced -> 1.</para>
''' <para>The sun's light (the directional the lit shaders receive) = powf(Sunlight, 2.2) * fade * SunlightScale
''' (0x14223C47F; Tools/re-docs/DISENO_CLIMA_Y_HORA_PREVIEW_2026-10-01.md 2.4): the same image-space factor as
''' DLightColor.</para></summary>
Friend NotInheritable Class Fo4PreviewWeather
    ''' <summary>Effect Lighting of the moment, as the record stores it (bytes 0..255).</summary>
    Public ReadOnly EffectLighting As Vector3
    ''' <summary>Sunlight of the moment (NAM0 colour type 4), bytes 0..255.</summary>
    Public ReadOnly Sunlight As Vector3

    ' The Effect Lighting argument stays FIRST: Tools/FabricGen/vbglsl.py reads the first Vector3 of CommonwealthClearDay.
    Friend Sub New(effectLighting As Vector3, sunlight As Vector3)
        Me.EffectLighting = effectLighting : Me.Sunlight = sunlight
    End Sub

    ''' <summary>WTHR CommonwealthClear (0x0002B52A), day.</summary>
    Public Shared ReadOnly CommonwealthClearDay As New Fo4PreviewWeather(New Vector3(150.0F, 150.0F, 150.0F), New Vector3(225.0F, 225.0F, 225.0F))

    ''' <summary>The effect shader's DLightColor under this weather and image space (linear).</summary>
    Public Function DLightColor(imgs As Fo4ImageSpace) As Vector3
        Dim lin = Shader_Base_Class.Vector_to_Linear(EffectLighting / 255.0F)
        Return lin * imgs.SunlightScale
    End Function

    ''' <summary>The sun's light under this weather and image space (linear, fade 1).</summary>
    Public Function SunLightColor(imgs As Fo4ImageSpace) As Vector3
        Return Shader_Base_Class.Vector_to_Linear(Sunlight / 255.0F) * imgs.SunlightScale
    End Function
End Class

''' <summary>THE SKYRIM SE WEATHER OF THE DEFAULT ROW OF THE PREVIEW'S WEATHER TABLE (PreviewImagingTable), for what the
''' effect shader reads from it.
''' <para>Canonical choice of the preview (user choice, 3-oct-2026): WTHR SkyrimClearSN_A (Skyrim.esm 0x0010E1F0), DAY
''' (NAM0 time-of-day 1), whose day image space (IMSP[1]) is IMGS ISSkyrimClearDAY_SN (0x0010A212). Data read from the
''' WINNING record of each (Update.esm for both; the winner is the last override, xEdit wbImplementation.pas), the
''' same read Tools/PreviewImagingTableGen does, which checks these literals against the plugins on every run:
''' Effect Lighting (NAM0 entry 9, xEdit wbDefinitionsCommon) day = (96, 104, 106); ISSkyrimClearDAY_SN HNAM
''' SunlightScale 2.1 (the rest of the image space: <see cref="SseImageSpace.SkyrimClearSnDay"/>).</para>
''' <para>The effect PS's light (cb2[7]) = sun colour(+0x14C) * sun fade(+0x134) * ISM+0xE0 (SetupGeometry
''' 0x141557435..748F); the Sky update copies the weather's Effect Lighting into sun+0x14C (0x14041C879..C88F);
''' ISM+0xE0 = HNAM SunlightScale (HNAM order: +0xD8 ReceiveBloomThreshold, +0xDC White, +0xE0 SunlightScale).
''' SSE's pipeline is raw: no powf. Holes, declared: the sun fade (1 when not fading) and the NAM0 -&gt; Sky
''' interpolation between times of day (the preview sits at the day key).</para>
''' <para>The sun's light = Sunlight * fade * SunlightScale, raw (DirLightColor, 0x141549AE5; Tools/re-docs/
''' DISENO_CLIMA_Y_HORA_PREVIEW_2026-10-01.md 2.4). Update.esm's day Sunlight = (159, 143, 132).</para></summary>
Friend NotInheritable Class SsePreviewWeather
    ''' <summary>Effect Lighting of the moment, bytes 0..255.</summary>
    Public ReadOnly EffectLighting As Vector3
    ''' <summary>HNAM SunlightScale of the moment's image space.</summary>
    Public ReadOnly SunlightScale As Single
    ''' <summary>Sunlight of the moment (NAM0 colour type 4), bytes 0..255.</summary>
    Public ReadOnly Sunlight As Vector3

    Friend Sub New(effectLighting As Vector3, sunlightScale As Single, sunlight As Vector3)
        Me.EffectLighting = effectLighting : Me.SunlightScale = sunlightScale : Me.Sunlight = sunlight
    End Sub

    ''' <summary>WTHR SkyrimClearSN_A (0x0010E1F0), day, with IMGS ISSkyrimClearDAY_SN (0x0010A212).</summary>
    Public Shared ReadOnly SkyrimClearSnDay As New SsePreviewWeather(New Vector3(96.0F, 104.0F, 106.0F), 2.1F, New Vector3(159.0F, 143.0F, 132.0F))

    ''' <summary>cb2[7] of the SSE effect PS under this weather (raw).</summary>
    Public ReadOnly Property EffectLight As Vector3
        Get
            Return EffectLighting / 255.0F * SunlightScale
        End Get
    End Property

    ''' <summary>The sun's light under this weather (raw, fade 1).</summary>
    Public ReadOnly Property SunLightColor As Vector3
        Get
            Return Sunlight / 255.0F * SunlightScale
        End Get
    End Property
End Class

''' <summary>THE SKYRIM SE IMAGE SPACE OF THE DEFAULT ROW OF THE PREVIEW'S WEATHER TABLE (PreviewImagingTable): IMGS ISSkyrimClearDAY_SN (Skyrim.esm 0x0010A212), the day
''' image space of WTHR SkyrimClearSN_A (<see cref="SsePreviewWeather"/>). Values read from the WINNING record
''' (Update.esm, see <see cref="SsePreviewWeather"/>): HNAM ReceiveBloomThreshold 0.8, White 1.075; CNAM Saturation
''' 2.0, Brightness 1.2, Contrast 1.35; TNAM amount 0.72, rgb (0.8862745, 0.9137255, 0.9137255).
''' <para>Mapped to the HDR PS 12545 by ImageSpaceEffectHDR (0x14153D860, SetVector 0x14152B170 index k -&gt; cb2[k+1]):
''' cb2[2] = (ReceiveBloomThreshold ISM+0xD8, Reinhard 1 / (1.1 White)^2 (White ISM+0xDC), bUseFilmicCurve);
''' cb2[3] = (Saturation ISM+0xEC + [0x1420D7CF0], 0, Contrast ISM+0xF4 (-0.3 if byte 0x14343635E), Brightness
''' ISM+0xF0 (+0.25 if that byte)); cb2[4] = (tint rgb ISM+0xFC..0x104, amount ISM+0xF8). The filmic flag
''' [0x1420D7DC8] is 0 in the binary and no installed INI sets it: Reinhard. Holes, declared at their static values:
''' the saturation boost [0x1420D7CF0] = 0 and the byte 0x14343635E = 0. The display exponent cb12[42].x is 1 under
''' the user's SkyrimPrefs.ini (fGamma=1.0).</para></summary>
Friend NotInheritable Class SseImageSpace
    Public ReadOnly ReceiveBloomThreshold As Single
    Public ReadOnly White As Single
    Public ReadOnly Saturation As Single
    Public ReadOnly Brightness As Single
    Public ReadOnly Contrast As Single
    Public ReadOnly TintColor As Vector3
    Public ReadOnly TintAmount As Single

    Friend Sub New(receiveBloomThreshold As Single, white As Single, saturation As Single, brightness As Single,
                    contrast As Single, tintColor As Vector3, tintAmount As Single)
        Me.ReceiveBloomThreshold = receiveBloomThreshold : Me.White = white : Me.Saturation = saturation
        Me.Brightness = brightness : Me.Contrast = contrast : Me.TintColor = tintColor : Me.TintAmount = tintAmount
    End Sub

    ''' <summary>IMGS ISSkyrimClearDAY_SN (0x0010A212).</summary>
    Public Shared ReadOnly SkyrimClearSnDay As New SseImageSpace(
        receiveBloomThreshold:=0.8F, white:=1.075F, saturation:=2.0F, brightness:=1.2F, contrast:=1.35F,
        tintColor:=New Vector3(0.8862745F, 0.9137255F, 0.9137255F), tintAmount:=0.72F)

    ''' <summary>The constants of PS 12545 (cb2[2..4]) and the display exponent.</summary>
    Public Sub Upload(post As Shader_Base_Class)
        Dim w = 1.1F * White
        post.SetVector4("sseHdr", New Vector4(ReceiveBloomThreshold, 1.0F / (w * w), 0.0F, 0.0F))
        post.SetVector4("sseCinematic", New Vector4(Saturation, 0.0F, Contrast, Brightness))
        ' cb2[4] of PS 12545 = (TintColor * TintAmount, TintAmount): premultiplied on the copy into the ISM
        ' (0x141536f80, ISM+0xFC..0x104) and uploaded by SetVector(3) at 0x14153dae6. SkyrimSE.exe 1.7.104.0.
        post.SetVector4("sseTint", New Vector4(TintColor * TintAmount, TintAmount))
        post.SetFloat("sseDisplayExponent", Shader_Base_Class.DisplayExponent(True))
    End Sub
End Class

''' <summary>GLSL OF THE FO4 POST PROCESS, IN ONE PLACE.
''' <para>ASCII ONLY AND NO DOUBLE QUOTES: these are VB <c>Const String</c>s (a quote closes the literal and a
''' non-ASCII character breaks the compile at runtime). The <c>glsl-ascii</c> gate sweeps them.</para></summary>
Friend Module PostProcessShaderSource

    ''' <summary>Luminance weights of the engine's image space shaders: the eye-adaptation CS (i3720/i3722)
    ''' and the cinematic stage of the HDR PS (i3700) both use (0.2125, 0.7154, 0.0721).</summary>
    Friend Const LumWeightsGlsl As String = "const vec3 LUM_W = vec3(0.2125, 0.7154, 0.0721);"

    ''' <summary>Pass 1 of the mean luminance: one 16x16 group per tile, each writes (sum lum*w, sum w).
    ''' Engine: arithmetic mean of the luminance with 4:1 tree reductions (CS i3720/i3722), Inf/NaN zeroed
    ''' (PS i3710) - no log average. PREVIEW DECISION (declared): the mean is taken over the ACTOR's pixels,
    ''' weighted by actor coverage (the background and the floor are UI and do not exist in the engine's
    ''' frame); the engine samples a grid of its scene target, the preview takes every pixel.</summary>
    Friend Const Compute_LumPartials As String =
"#version 430
layout(local_size_x = 16, local_size_y = 16) in;

uniform sampler2D texScene;
uniform sampler2D texCoverage;

layout(std430, binding = 7) writeonly buffer LumPartials { vec2 partials[]; };

shared vec2 acc[256];

" & LumWeightsGlsl & "

void main()
{
    ivec2 size = textureSize(texCoverage, 0);
    ivec2 p = ivec2(gl_GlobalInvocationID.xy);
    vec2 v = vec2(0.0);
    if (p.x < size.x && p.y < size.y)
    {
        vec4 cov = texelFetch(texCoverage, p, 0);
        if (cov.g > 0.0)
        {
            // The scene target holds radiance premultiplied by the composite coverage (r).
            float l = dot(texelFetch(texScene, p, 0).rgb / cov.r, LUM_W);
            // i3710: a non-finite luminance counts as 0.
            if (isnan(l) || isinf(l)) l = 0.0;
            v = vec2(l * cov.g, cov.g);
        }
    }
    uint i = gl_LocalInvocationIndex;
    acc[i] = v;
    barrier();
    for (uint s = 128u; s > 0u; s >>= 1)
    {
        if (i < s) acc[i] += acc[i + s];
        barrier();
    }
    if (i == 0u)
        partials[gl_WorkGroupID.y * gl_NumWorkGroups.x + gl_WorkGroupID.x] = acc[0];
}"

    ''' <summary>Pass 2: one group folds every partial into the mean. No actor pixel: mean 0, and the
    ''' exposure law itself takes it to AutoExposureMax (MG / (0 + 0.001) clamped).</summary>
    Friend Const Compute_LumResolve As String =
"#version 430
layout(local_size_x = 256) in;

uniform int partialCount;

layout(std430, binding = 7) readonly buffer LumPartials { vec2 partials[]; };
layout(std430, binding = 8) writeonly buffer LumResult { float avgLum; };

shared vec2 acc[256];

void main()
{
    uint i = gl_LocalInvocationIndex;
    vec2 v = vec2(0.0);
    for (uint k = i; k < uint(partialCount); k += 256u)
        v += partials[k];
    acc[i] = v;
    barrier();
    for (uint s = 128u; s > 0u; s >>= 1)
    {
        if (i < s) acc[i] += acc[i + s];
        barrier();
    }
    if (i == 0u)
        avgLum = (acc[0].y > 0.0) ? acc[0].x / acc[0].y : 0.0;
}"

    ''' <summary>THE FO4 POST LAW: ImageSpace HDR (PS i3700) followed by GammaCorrectLUT (PS i3648), then the
    ''' UI composite over the preview background.
    ''' <para>i3700 (constants from ImageSpaceEffectHDR, 0x14220A730; cb2[1] = (AEMax, AEMin, MiddleGray, E)):
    ''' exposure = min(max(MG / (avgLum + 0.001), AEMin), AEMax); x = (scene + bloom) * exposure;
    ''' H(z) = (z (0.15 z + 0.05) + 0.2 E) / (z (0.15 z + 0.5) + 0.06) - E / 0.3; c = H(2x) / H(11.2) (the PS
    ''' carries W = 11.2 folded into the literals 19.376 and 0.040856); cinematic: l = dot(c, LUM_W);
    ''' c = lerp(l, c, Saturation); c = lerp(c, l * Tint, TintAmount); c = lerp(avgLum, Brightness * c, Contrast).</para>
    ''' <para>i3648: g = pow(c, 0.454545); out = sum of LUT_i(g * 0.9375 + 0.03125) * w_i over 16^3 LUTs, written
    ''' to the UNORM swapchain (saturate). One LUT, weight 1 (weights NOT PROVEN); without a LUT, out = g.</para>
    ''' <para>Holes: bloom = 0; the t3.w bypass of i3700 (pixels flagged 4) has no analog here.</para>
    ''' <para>PREVIEW COMPOSITE (declared, not engine): the background is UI. The scene target carries the
    ''' coverage of what was drawn (r) and the ground-shadow factor that fell on the background (target 2);
    ''' the post maps the un-premultiplied radiance through the law and mixes it over the background in
    ''' display space with that coverage.</para></summary>
    Friend Const Fragment_PostFo4 As String =
"#version 430

out vec4 FragColor;
" & BackgroundFadeSource.Fade_Helper & "
uniform sampler2D texScene;
uniform sampler2D texCoverage;
uniform sampler2D texBgShadow;
uniform sampler3D texLut;
uniform float lutWeight;
// x AutoExposureMax, y AutoExposureMin, z MiddleGray, w TonemapE (cb2[1] of i3700)
uniform vec4 hdrExposure;
// x Saturation, y Brightness, z Contrast (cb2[2].x / .w / .z of i3700)
uniform vec3 hdrCinematic;
// rgb tint colour, w tint amount (cb2[3] of i3700)
uniform vec4 hdrTint;

layout(std430, binding = 8) readonly buffer LumResult { float avgLum; };

" & LumWeightsGlsl & "

vec3 hable(in vec3 z, in float e)
{
    return (z * (0.15 * z + 0.05) + 0.2 * e) / (z * (0.15 * z + 0.5) + 0.06) - e / 0.3;
}

void main()
{
    ivec2 p = ivec2(gl_FragCoord.xy);
    vec4 cov = texelFetch(texCoverage, p, 0);
    vec3 bg = backgroundAt(gl_FragCoord.xy) * texelFetch(texBgShadow, p, 0).rgb;
    if (cov.r <= 0.0)
    {
        FragColor = vec4(bg, 1.0);
        return;
    }
    vec3 scene = texelFetch(texScene, p, 0).rgb / cov.r;

    // ---- i3700: ImageSpace HDR ----
    float lavg = avgLum;
    float e = hdrExposure.w;
    float exposure = min(max(hdrExposure.z / (lavg + 0.001), hdrExposure.y), hdrExposure.x);
    vec3 x = scene * exposure;
    vec3 c = hable(2.0 * x, e) / hable(vec3(11.2), e);
    float l = dot(c, LUM_W);
    c = mix(vec3(l), c, hdrCinematic.x);
    c = mix(c, l * hdrTint.rgb, hdrTint.w);
    c = mix(vec3(lavg), hdrCinematic.y * c, hdrCinematic.z);

    // ---- i3648: GammaCorrectLUT ----
    // pow of a negative is NaN in the engine (HLSL) and undefined here; with the image space in use
    // (Contrast 1) c is never negative.
    vec3 g = pow(c, vec3(0.454545));
    vec3 o = (lutWeight > 0.0) ? texture(texLut, g * 0.9375 + 0.03125).rgb * lutWeight : g;
    o = clamp(o, 0.0, 1.0);

    FragColor = vec4(mix(bg, o, cov.r), 1.0);
}"

    ''' <summary>THE SKYRIM SE POST LAW: ImageSpace HDR PS 12545 (Fade twin 12546), transcribed, then the UI composite
    ''' over the background like the FO4 pass.
    ''' <para>lum = max(dot(scene, LUM_W), 1e-5); L = lum * t2.y / t2.x; Reinhard Lt = L (1 + L cb2[2].y) / (1 + L);
    ''' c = scene * Lt / lum + bloom * sat(cb2[2].x - Lt); l = dot(c, LUM_W); c = l + cb2[3].x (c - l);
    ''' c = lerp(c, l * cb2[4].rgb, cb2[4].w); c = t2.x + cb2[3].z (cb2[3].w c - t2.x); out = pow(sat(c), cb12[42].x).</para>
    ''' <para>t2 is the eye-adaptation texture: its x and y are the SAME mean luminance (PS 12550 writes
    ''' dot(scene, LUM_W) into x, y and z of the first level and 12549 / 12552 average it down), adapted toward the
    ''' frame's value at two different speeds (PS 12551 / 12553). In the steady state the preview shows, both equal
    ''' the mean: t2.y / t2.x = 1 and t2.x = the mean (the same actor-weighted mean as the FO4 pass). Hole: bloom = 0.</para></summary>
    Friend Const Fragment_PostSse As String =
"#version 430

out vec4 FragColor;
" & BackgroundFadeSource.Fade_Helper & "
uniform sampler2D texScene;
uniform sampler2D texCoverage;
uniform sampler2D texBgShadow;
// x ReceiveBloomThreshold, y 1/(1.1 White)^2 (cb2[2])
uniform vec4 sseHdr;
// x Saturation, z Contrast, w Brightness (cb2[3])
uniform vec4 sseCinematic;
// rgb tint, w amount (cb2[4])
uniform vec4 sseTint;
// cb12[42].x
uniform float sseDisplayExponent;

layout(std430, binding = 8) readonly buffer LumResult { float avgLum; };

" & LumWeightsGlsl & "

void main()
{
    ivec2 p = ivec2(gl_FragCoord.xy);
    vec4 cov = texelFetch(texCoverage, p, 0);
    vec3 bg = backgroundAt(gl_FragCoord.xy) * texelFetch(texBgShadow, p, 0).rgb;
    if (cov.r <= 0.0)
    {
        FragColor = vec4(bg, 1.0);
        return;
    }
    vec3 scene = texelFetch(texScene, p, 0).rgb / cov.r;
    float adapted = avgLum;                       // t2.x = t2.y in the steady state

    float lum = max(dot(LUM_W, scene), 0.00001);
    float L = lum * (adapted / adapted);          // lum * t2.y / t2.x
    float Lt = (L * sseHdr.y + 1.0) * L / (L + 1.0);
    vec3 c = scene * (Lt / lum);                  // + bloom * sat(cb2[2].x - Lt): no bloom pass (hole)
    float l = dot(c, LUM_W);
    c = sseCinematic.x * (c - vec3(l)) + vec3(l);
    c = sseTint.w * (l * sseTint.rgb - c) + c;
    c = sseCinematic.z * (sseCinematic.w * c - vec3(adapted)) + vec3(adapted);
    vec3 o = pow(clamp(c, 0.0, 1.0), vec3(sseDisplayExponent));

    FragColor = vec4(mix(bg, o, cov.r), 1.0);
}"

End Module

''' <summary>Program of the SSE post pass: fullscreen triangle of the background + <see cref="PostProcessShaderSource.Fragment_PostSse"/>.</summary>
Public Class PostProcess_Sse_Shader_Class
    Inherits Shader_Base_Class
    Sub New()
        MyBase.New(BackgroundFadeSource.Vertex_Background, PostProcessShaderSource.Fragment_PostSse)
    End Sub
End Class

''' <summary>The refraction-normals pass of each game: its game's VS (the shape's skinning and position, plus the
''' RefractionSource vertex terms) and the normals PS.</summary>
Public Class Refraction_Normals_Sse_Shader_Class
    Inherits Shader_Base_Class
    Sub New()
        MyBase.New(Shader_Class_SSE.Vertex_SSE, RefractionSource.Normals_Fragment)
    End Sub
End Class

Public Class Refraction_Normals_Fo4_Shader_Class
    Inherits Shader_Base_Class
    Sub New()
        MyBase.New(Shader_Class_Fo4.Vertex_FO4, RefractionSource.Normals_Fragment)
    End Sub
End Class

''' <summary>ISRefraction (both games; the constants per game, RefractionLaw.ImageSpaceConstants).</summary>
Public Class Refraction_ImageSpace_Shader_Class
    Inherits Shader_Base_Class
    Sub New()
        MyBase.New(RefractionSource.ImageSpace_Vertex, RefractionSource.ImageSpace_Fragment)
    End Sub
End Class

''' <summary>THE FULL-SCREEN TRIANGLE OF EVERY IMAGE-SPACE PASS (the post, ISRefraction, the SSE SAO and its composite), in one
''' place: the caller binds its framebuffer, uses <paramref name="program"/> and sets its inputs; Draw turns the depth test, the
''' depth write, the blend and the culling off, draws the attribute-less triangle (the core profile needs a VAO bound), unbinds
''' texture units 0..unitsUsed-1 (targets are attachments again later: no unit may keep them, feedback loop) and leaves the program
''' unbound and the culling ENABLED, the state the scene draws expect. The depth test / write and the blend are restored by the
''' caller (each one knows what follows).</summary>
Friend NotInheritable Class FullscreenPass
    Private Sub New()
    End Sub

    ''' <param name="writesDepth">The program writes gl_FragDepth into the bound depth buffer: the depth test on with ALWAYS (GL updates
    ''' the depth buffer only while the test is enabled) and writes on. Otherwise depth off.</param>
    Friend Shared Sub Draw(program As Shader_Base_Class, emptyVao As Integer, unitsUsed As Integer, Optional writesDepth As Boolean = False)
        If writesDepth Then
            GL.Enable(EnableCap.DepthTest)
            GL.DepthFunc(DepthFunction.Always)
            GL.DepthMask(True)
        Else
            GL.Disable(EnableCap.DepthTest)
            GL.DepthMask(False)
        End If
        GL.Disable(EnableCap.Blend)
        GL.Disable(EnableCap.CullFace)
        GL.BindVertexArray(emptyVao)
        GL.DrawArrays(PrimitiveType.Triangles, 0, 3)
        GL.BindVertexArray(0)
        For u = 0 To unitsUsed - 1 : GL.BindTextureUnit(u, 0) : Next
        GL.UseProgram(0)
        GL.Enable(EnableCap.CullFace)
    End Sub
End Class

''' <summary>A FULL-SCREEN PASS OVER THE FRAME'S COLOUR (attachment 0 of a framebuffer, or the window's back buffer): the colour is
''' copied first (t0 "texScene", bilinear, clamped) and the program writes the frame. The image-space passes of the engines that read
''' the scene they write (ISRefraction, the SSE SAO composite) go through it.</summary>
Friend NotInheritable Class SceneColorPass
    Private _copy As Integer, _fmt As SizedInternalFormat, _w As Integer, _h As Integer

    Public Sub Apply(program As Shader_Base_Class, targetFbo As Integer, fmt As SizedInternalFormat, w As Integer, h As Integer,
                     emptyVao As Integer, bindInputs As Action(Of Shader_Base_Class))
        If _copy = 0 OrElse w <> _w OrElse h <> _h OrElse fmt <> _fmt Then
            If _copy <> 0 Then GL.DeleteTexture(_copy)
            GL.CreateTextures(TextureTarget.Texture2D, 1, _copy)
            GL.TextureStorage2D(_copy, 1, fmt, w, h)
            For Each prm In {TextureParameterName.TextureMinFilter, TextureParameterName.TextureMagFilter}
                GL.TextureParameter(_copy, prm, CInt(TextureMinFilter.Linear))
            Next
            For Each prm In {TextureParameterName.TextureWrapS, TextureParameterName.TextureWrapT}
                GL.TextureParameter(_copy, prm, CInt(TextureWrapMode.ClampToEdge))
            Next
            _fmt = fmt : _w = w : _h = h
        End If
        GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, targetFbo)
        GL.ReadBuffer(If(targetFbo = 0, ReadBufferMode.Back, ReadBufferMode.ColorAttachment0))
        GL.CopyTextureSubImage2D(_copy, 0, 0, 0, 0, 0, w, h)
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, targetFbo)
        If targetFbo <> 0 Then GL.DrawBuffer(DrawBufferMode.ColorAttachment0) Else GL.DrawBuffer(DrawBufferMode.Back)
        GL.Viewport(0, 0, w, h)
        GL.ColorMask(True, True, True, True)
        program.Use()
        GL.BindTextureUnit(0, _copy)
        program.SetInt("texScene", 0)
        bindInputs?.Invoke(program)
        FullscreenPass.Draw(program, emptyVao, 4)
        GL.DepthMask(True)
        GL.Enable(EnableCap.DepthTest)
    End Sub

    Public Sub Free()
        If _copy <> 0 Then GL.DeleteTexture(_copy) : _copy = 0
        _w = 0 : _h = 0
    End Sub
End Class

''' <summary>THE SKYRIM SE SAO COMPOSITE over the opaque scene (Tools/re-docs/AUDIT_SSE_RE_2026-10-03.md, follow-up 2): the world
''' render runs it after the opaque finish (decals and list 0xD included) and before the alpha finish (0x141539F10 -> 0x141541F40,
''' 0x14153A1CA), ISSAOCompositeSAOFog PS 16004 with the default INIs (bSAOEnable 1, bSAOApplyFog 1): per geometry pixel
''' saturate(lerp(ao * (scene + ssr), fogColor, F) * invFrameBufferRange); a sky pixel (depth >= 0.999999) gets saturate(ao *
''' (scene + ssr)), no fog and no invFBR. The preview: no fog (user decision), no SSR and no snow terms, and no AO pass (not
''' computed by the preview). The engine's geometry is the meshes; the preview's background and its floor are app elements the
''' engine does not have (the floor is matched to the background at the horizon): a pixel no mesh drew (coverage g = 0, which
''' only mesh draws write) is left as it is.</summary>
Public Class Sse_Opaque_Composite_Shader_Class
    Inherits Shader_Base_Class
    Sub New()
        MyBase.New(RefractionSource.ImageSpace_Vertex, SseCompositeSource.Fragment)
    End Sub
End Class

''' <summary>One pass of the Skyrim SE SAO: the full-screen VS of the transcription + one of SseSaoSource's six fragments.</summary>
Public Class Sse_Sao_Pass_Shader_Class
    Inherits Shader_Base_Class
    Sub New(fragment As String)
        MyBase.New(SseSaoSource.Vs_Fullscreen, fragment)
    End Sub
End Class

''' <summary>The six SAO programs (CameraZ 15997, Minify 16006, MinifyContrast 16008, RawAO 16012, BlurH 15991, BlurV 15993).</summary>
Friend NotInheritable Class SseSaoPrograms
    Friend ReadOnly CameraZ As Sse_Sao_Pass_Shader_Class
    Friend ReadOnly Minify As Sse_Sao_Pass_Shader_Class
    Friend ReadOnly MinifyContrast As Sse_Sao_Pass_Shader_Class
    Friend ReadOnly RawAO As Sse_Sao_Pass_Shader_Class
    Friend ReadOnly BlurH As Sse_Sao_Pass_Shader_Class
    Friend ReadOnly BlurV As Sse_Sao_Pass_Shader_Class

    Friend Sub New()
        Me.New(Nothing)
    End Sub

    ''' <summary>GATE ONLY with <paramref name="gateMutation"/> (ShadowGate --sse-sao-law mutants): each fragment text goes through it.</summary>
    Friend Sub New(gateMutation As Func(Of String, String))
        Dim m = If(gateMutation, Function(s As String) s)
        ' A pass that does not compile or link throws out of here after releasing the ones already built (v4 B-rev-05).
        Try
            CameraZ = New Sse_Sao_Pass_Shader_Class(m(SseSaoSource.Fragment_CameraZ))
            Minify = New Sse_Sao_Pass_Shader_Class(m(SseSaoSource.Fragment_Minify))
            MinifyContrast = New Sse_Sao_Pass_Shader_Class(m(SseSaoSource.Fragment_MinifyContrast))
            RawAO = New Sse_Sao_Pass_Shader_Class(m(SseSaoSource.Fragment_RawAO))
            BlurH = New Sse_Sao_Pass_Shader_Class(m(SseSaoSource.Fragment_BlurH))
            BlurV = New Sse_Sao_Pass_Shader_Class(m(SseSaoSource.Fragment_BlurV))
        Catch
            Dispose()
            Throw
        End Try
    End Sub

    Friend Sub Dispose()
        For Each p As Shader_Base_Class In {CameraZ, Minify, MinifyContrast, RawAO, BlurH, BlurV}
            p?.Dispose()
        Next
    End Sub
End Class

''' <summary>THE TARGETS AND THE RUN OF THE SKYRIM SE SAO (Tools/re-docs/RE_SAO_BOTH_2026-10-03.md 1.5): CameraZ into level 0 of an
''' R32F pyramid (RT 0x31/0x32), Minify / MinifyContrast into levels 1..min(4, n-1) (RawAO reads mips &lt;= 4), RawAO 16012 into an
''' RGBA8 target (RT 0x3E), BlurH into RT 0x42 and BlurV into kSAO (RT 0x2E). Every pass runs under
''' glClipControl(UPPER_LEFT): the SAO targets are in D3D row order and only the two scene inputs (depth, normals) are flipped on
''' read (SseSaoSource.Prelude). The samplers are the engine's table (RE 1.5): FILT 0 point, FILT 1 bilinear mip-point, FILT 2
''' trilinear, all CLAMP; the blurs inherit the RawAO slot-0 sampler (FILT 2).</summary>
Friend NotInheritable Class SseSaoTargets
    Private _w As Integer, _h As Integer, _levels As Integer
    Private _pyramid As Integer
    Private _views As Integer()
    Private _ao As Integer, _blurH As Integer, _kSao As Integer
    Private _fbo As Integer
    Private _sFilt0 As Integer, _sFilt1 As Integer, _sFilt2 As Integer

    ''' <summary>GATE ONLY (ShadowGate --sse-sao-law mutant): run the passes without glClipControl(UPPER_LEFT).</summary>
    Friend Shared GateNoClipControl As Boolean

    ''' <summary>kSAO, in D3D row order (0 before the first Run).</summary>
    Public ReadOnly Property KSaoTexture As Integer
        Get
            Return _kSao
        End Get
    End Property

    ''' <summary>GATE ONLY: the CameraZ / Minify pyramid, the raw AO and the BlurH output of the last Run.</summary>
    Friend ReadOnly Property PyramidTexture As Integer
        Get
            Return _pyramid
        End Get
    End Property
    Friend ReadOnly Property RawAoTexture As Integer
        Get
            Return _ao
        End Get
    End Property
    Friend ReadOnly Property BlurHTexture As Integer
        Get
            Return _blurH
        End Get
    End Property
    Friend ReadOnly Property Levels As Integer
        Get
            Return _levels
        End Get
    End Property

    Private Shared Function NewSampler(minF As TextureMinFilter, magF As TextureMagFilter, maxLod As Single) As Integer
        Dim s = GL.GenSampler()
        GL.SamplerParameter(s, SamplerParameterName.TextureWrapS, CInt(TextureWrapMode.ClampToEdge))
        GL.SamplerParameter(s, SamplerParameterName.TextureWrapT, CInt(TextureWrapMode.ClampToEdge))
        GL.SamplerParameter(s, SamplerParameterName.TextureMinFilter, CInt(minF))
        GL.SamplerParameter(s, SamplerParameterName.TextureMagFilter, CInt(magF))
        GL.SamplerParameter(s, SamplerParameterName.TextureMinLod, -Single.MaxValue)
        GL.SamplerParameter(s, SamplerParameterName.TextureMaxLod, maxLod)
        GL.SamplerParameter(s, SamplerParameterName.TextureCompareMode, CInt(TextureCompareMode.None))
        Return s
    End Function

    Private Shared Function NewTex(fmt As SizedInternalFormat, levels As Integer, w As Integer, h As Integer) As Integer
        Dim t As Integer
        GL.CreateTextures(TextureTarget.Texture2D, 1, t)
        GL.TextureStorage2D(t, levels, fmt, w, h)
        Return t
    End Function

    Private Sub Ensure(w As Integer, h As Integer)
        If _fbo <> 0 AndAlso w = _w AndAlso h = _h Then Return
        Free()
        _levels = SseSaoConstants.PyramidLevels(w, h)
        _pyramid = NewTex(SizedInternalFormat.R32f, _levels, w, h)
        ReDim _views(_levels - 1)
        For i = 0 To _levels - 1
            _views(i) = GL.GenTexture()
            GL.TextureView(_views(i), TextureTarget.Texture2D, _pyramid, PixelInternalFormat.R32f, i, 1, 0, 1)
        Next
        _ao = NewTex(SizedInternalFormat.Rgba8, 1, w, h)
        _blurH = NewTex(SizedInternalFormat.Rgba8, 1, w, h)
        _kSao = NewTex(SizedInternalFormat.Rgba8, 1, w, h)
        _fbo = GL.GenFramebuffer()
        _sFilt0 = NewSampler(TextureMinFilter.NearestMipmapNearest, TextureMagFilter.Nearest, 0.0F)
        _sFilt1 = NewSampler(TextureMinFilter.LinearMipmapNearest, TextureMagFilter.Linear, 0.0F)
        _sFilt2 = NewSampler(TextureMinFilter.LinearMipmapLinear, TextureMagFilter.Linear, Single.MaxValue)
        _w = w : _h = h
    End Sub

    Private Sub Target(tex As Integer, level As Integer, w As Integer, h As Integer)
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo)
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, tex, level)
        GL.DrawBuffer(DrawBufferMode.ColorAttachment0)
        GL.Viewport(0, 0, w, h)
    End Sub

    Private Shared Sub Cb12(p As Shader_Base_Class, with41 As Boolean, with44 As Boolean)
        If with41 Then p.SetVector4("uCB12_41", SseSaoConstants.Cb12_41)
        p.SetVector4("uCB12_43", SseSaoConstants.Cb12_43)
        If with44 Then p.SetVector4("uCB12_44", SseSaoConstants.Cb12_44)
    End Sub

    ''' <summary>The five passes of 0x141541F40 over <paramref name="depthTex"/> (the scene depth copy, LOWER_LEFT) and
    ''' <paramref name="normalsTex"/> (the SAO normals target, LOWER_LEFT), W x H; leaves kSAO. The caller rebinds its framebuffer
    ''' and viewport. <paramref name="frustum"/> = NiCamera viewFrustum (left, right, top, bottom) of the frame's projection.</summary>
    Public Sub Run(progs As SseSaoPrograms, depthTex As Integer, normalsTex As Integer, w As Integer, h As Integer,
                   near As Single, far As Single, frustum As Vector4, emptyVao As Integer)
        If w <= 0 OrElse h <= 0 Then Return
        Ensure(w, h)
        GL.ColorMask(True, True, True, True)
        If Not GateNoClipControl Then GL.ClipControl(ClipOrigin.UpperLeft, ClipDepthMode.NegativeOneToOne)
        Try
            ' 1. CameraZ (15997) -> pyramid level 0.
            Target(_pyramid, 0, w, h)
            progs.CameraZ.Use()
            progs.CameraZ.SetVector4("g_ClipInfos", SseSaoConstants.CameraZClipInfos(near, far, SseSaoConstants.DofMaxDepthParticipation))
            Cb12(progs.CameraZ, False, True)
            GL.BindTextureUnit(0, depthTex)
            GL.BindSampler(0, _sFilt0)
            FullscreenPass.Draw(progs.CameraZ, emptyVao, 1)

            ' 2. Minify / MinifyContrast (16006 / 16008) -> level i from level i-1 (one view per level: no feedback loop).
            Dim contrastDone = False
            For i = 1 To _levels - 1
                Dim contrast = SseSaoConstants.MinifyUsesContrast(i, h, contrastDone)
                If contrast Then contrastDone = True
                Dim p As Shader_Base_Class = If(contrast, CType(progs.MinifyContrast, Shader_Base_Class), progs.Minify)
                Target(_pyramid, i, Math.Max(1, w >> i), Math.Max(1, h >> i))
                p.Use()
                Dim res As Vector4, dynBits As UInteger
                SseSaoConstants.MinifyConstants(i, w, h, res, dynBits)
                p.SetVector4("g_RenderTargetResolution", res)
                p.SetVector4("g_UseDynamicSampling", New Vector4(BitConverter.UInt32BitsToSingle(dynBits), 0.0F, 0.0F, 0.0F))
                If contrast Then p.SetVector4("g_ContrastParams", New Vector4(SseSaoConstants.ContrastScale(SseSaoConstants.DofCenterWeight), 0.0F, 0.0F, 0.0F))
                Cb12(p, True, False)
                GL.BindTextureUnit(0, _views(i - 1))
                GL.BindSampler(0, If(i <= 4, _sFilt0, _sFilt1))
                FullscreenPass.Draw(p, emptyVao, 1)
            Next

            ' 3. RawAO (16012) -> _ao. t0 = the whole pyramid (FILT 2), t1 = the normals (FILT 1).
            Target(_ao, 0, w, h)
            progs.RawAO.Use()
            Dim k = SseSaoConstants.RawAOConstants(frustum.X, frustum.Y, frustum.Z, frustum.W, near, far, w, h)
            progs.RawAO.SetVector4("g_ProjInfos", k.ProjInfos)
            progs.RawAO.SetVector4("g_SSAOInfos", k.SsaoInfos)
            progs.RawAO.SetVector4("g_ScreenInfos", k.ScreenInfos)
            progs.RawAO.SetVector4("g_SSAOInfos2", k.SsaoInfos2)
            Cb12(progs.RawAO, False, True)
            GL.BindTextureUnit(0, _pyramid)
            GL.BindSampler(0, _sFilt2)
            GL.BindTextureUnit(1, normalsTex)
            GL.BindSampler(1, _sFilt1)
            FullscreenPass.Draw(progs.RawAO, emptyVao, 2)

            ' 4. BlurH (15991) -> _blurH; 5. BlurV (15993) -> kSAO. Slot 0 keeps the RawAO sampler (FILT 2).
            RunBlurs(progs, _ao, w, h, emptyVao)
        Finally
            GL.BindSampler(0, 0)
            GL.BindSampler(1, 0)
            GL.ClipControl(ClipOrigin.LowerLeft, ClipDepthMode.NegativeOneToOne)
        End Try
    End Sub

    ''' <summary>BlurH from <paramref name="aoTex"/> into _blurH, BlurV into kSAO; slot 0 with the inherited FILT 2 sampler.</summary>
    Private Sub RunBlurs(progs As SseSaoPrograms, aoTex As Integer, w As Integer, h As Integer, emptyVao As Integer)
        GL.BindSampler(0, _sFilt2)
        Dim blur = SseSaoConstants.BlurScreenInfos(w, h)
        Target(_blurH, 0, w, h)
        progs.BlurH.Use()
        progs.BlurH.SetVector4("g_BlurScreenInfos", blur)
        Cb12(progs.BlurH, False, True)
        GL.BindTextureUnit(0, aoTex)
        FullscreenPass.Draw(progs.BlurH, emptyVao, 1)

        Target(_kSao, 0, w, h)
        progs.BlurV.Use()
        progs.BlurV.SetVector4("g_BlurScreenInfos", blur)
        Cb12(progs.BlurV, False, True)
        GL.BindTextureUnit(0, _blurH)
        FullscreenPass.Draw(progs.BlurV, emptyVao, 1)
    End Sub

    ''' <summary>GATE ONLY (ShadowGate --sse-sao-law): the two blur passes over a given RGBA8 AO texture (w x h, D3D row order).</summary>
    Friend Sub GateRunBlurs(progs As SseSaoPrograms, aoTex As Integer, w As Integer, h As Integer, emptyVao As Integer)
        Ensure(w, h)
        GL.ColorMask(True, True, True, True)
        If Not GateNoClipControl Then GL.ClipControl(ClipOrigin.UpperLeft, ClipDepthMode.NegativeOneToOne)
        Try
            RunBlurs(progs, aoTex, w, h, emptyVao)
        Finally
            GL.BindSampler(0, 0)
            GL.ClipControl(ClipOrigin.LowerLeft, ClipDepthMode.NegativeOneToOne)
        End Try
    End Sub

    Public Sub Free()
        If _fbo <> 0 Then GL.DeleteFramebuffer(_fbo) : _fbo = 0
        If _views IsNot Nothing Then
            For Each v In _views
                If v <> 0 Then GL.DeleteTexture(v)
            Next
            _views = Nothing
        End If
        For Each t In {_pyramid, _ao, _blurH, _kSao}
            If t <> 0 Then GL.DeleteTexture(t)
        Next
        _pyramid = 0 : _ao = 0 : _blurH = 0 : _kSao = 0
        For Each s In {_sFilt0, _sFilt1, _sFilt2}
            If s <> 0 Then GL.DeleteSampler(s)
        Next
        _sFilt0 = 0 : _sFilt1 = 0 : _sFilt2 = 0
        _w = 0 : _h = 0 : _levels = 0
    End Sub
End Class

''' <summary>THE REFRACTION TARGETS OF ONE PREVIEW (Tools/re-docs/RE_REFRACTION_BOTH_2026-10-03.md).
''' <para>Normals: SSE RT 0xD kREFRACTION_NORMALS = R8G8B8A8_UNORM (0x14153B73F..759: saturates, 1/255 steps); FO4 logical RT
''' 0xE = R11G11B10_FLOAT (0x142233E88..ECC: no alpha - it samples as 1 -, negatives to 0). Cleared to (0.5, 0.5, 0, 0) on the
''' first bind of the frame; the pass tests against the frame's depth (the shared depth renderbuffer of the offscreen
''' targets, or a copy of the window's depth). Scene copy: what ISRefraction samples as t0 while it writes the frame.</para></summary>
Friend NotInheritable Class RefractionTargets
    Private _normalsTex As Integer, _normalsFmt As SizedInternalFormat, _fbo As Integer
    Private _depthCopy As New SceneDepthCopy()
    Private ReadOnly _pass As New SceneColorPass()
    Private _w As Integer, _h As Integer

    Public ReadOnly Property NormalsTexture As Integer
        Get
            Return _normalsTex
        End Get
    End Property

    ''' <summary>Binds the normals target (allocated / reallocated for the game and size), with <paramref name="depthRb"/>
    ''' (the offscreen frame's depth renderbuffer) or, when 0, a copy of <paramref name="frameFbo"/>'s depth, and clears it.</summary>
    Public Sub BeginNormals(isSse As Boolean, w As Integer, h As Integer, depthRb As Integer, frameFbo As Integer)
        Dim fmt = If(isSse, SizedInternalFormat.Rgba8, SizedInternalFormat.R11fG11fB10f)
        If _normalsTex = 0 OrElse w <> _w OrElse h <> _h OrElse fmt <> _normalsFmt Then
            FreeNormals()
            GL.CreateTextures(TextureTarget.Texture2D, 1, _normalsTex)
            GL.TextureStorage2D(_normalsTex, 1, fmt, w, h)
            For Each prm In {TextureParameterName.TextureMinFilter, TextureParameterName.TextureMagFilter}
                GL.TextureParameter(_normalsTex, prm, CInt(TextureMinFilter.Nearest))
            Next
            For Each prm In {TextureParameterName.TextureWrapS, TextureParameterName.TextureWrapT}
                GL.TextureParameter(_normalsTex, prm, CInt(TextureWrapMode.ClampToEdge))
            Next
            _fbo = GL.GenFramebuffer()
            _normalsFmt = fmt : _w = w : _h = h
        End If
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo)
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, _normalsTex, 0)
        If depthRb <> 0 Then
            GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthStencilAttachment, RenderbufferTarget.Renderbuffer, depthRb)
        Else
            _depthCopy.CopyFrom(frameFbo, w, h)
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _fbo)
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthStencilAttachment, TextureTarget.Texture2D, _depthCopy.Texture, 0)
        End If
        GL.DrawBuffer(DrawBufferMode.ColorAttachment0)
        GL.Viewport(0, 0, w, h)
        GL.ColorMask(True, True, True, True)
        GL.ClearBuffer(ClearBuffer.Color, 0, New Single() {0.5F, 0.5F, 0.0F, 0.0F})
    End Sub

    ''' <summary>ISRefraction over attachment 0 of <paramref name="targetFbo"/> (0 = the window's back buffer): the scene is
    ''' copied first (t0, bilinear) and the pass writes the frame (t1 = the normals, nearest).</summary>
    Public Sub ApplyImageSpace(program As Shader_Base_Class, isSse As Boolean, targetFbo As Integer, sceneFmt As SizedInternalFormat,
                               w As Integer, h As Integer, emptyVao As Integer)
        Dim k = RefractionLaw.ImageSpaceConstants(isSse)
        _pass.Apply(program, targetFbo, sceneFmt, w, h, emptyVao,
                    Sub(prog)
                        GL.BindTextureUnit(1, _normalsTex)
                        prog.SetInt("texRefractNormals", 1)
                        prog.SetFloat("refractOffsetScale", k.OffsetScale)
                        prog.SetBool("bRefractSceneAlpha", k.SceneAlpha)
                        prog.SetVector4("refractTint", k.Tint)
                    End Sub)
    End Sub

    Private Sub FreeNormals()
        If _fbo <> 0 Then GL.DeleteFramebuffer(_fbo) : _fbo = 0
        If _normalsTex <> 0 Then GL.DeleteTexture(_normalsTex) : _normalsTex = 0
        _w = 0 : _h = 0
    End Sub

    Public Sub Free()
        FreeNormals()
        _depthCopy.Free()
        _pass.Free()
    End Sub
End Class

''' <summary>Program of the FO4 post pass: fullscreen triangle of the background + <see cref="PostProcessShaderSource.Fragment_PostFo4"/>.</summary>
Public Class PostProcess_Fo4_Shader_Class
    Inherits Shader_Base_Class
    Sub New()
        MyBase.New(BackgroundFadeSource.Vertex_Background, PostProcessShaderSource.Fragment_PostFo4)
    End Sub
End Class

''' <summary>Compute program, pass 1 of the mean luminance.</summary>
Public Class Luminance_Partials_Shader_Class
    Inherits Shader_Base_Class
    Sub New()
        MyBase.New(PostProcessShaderSource.Compute_LumPartials)
    End Sub
End Class

''' <summary>Compute program, pass 2 of the mean luminance.</summary>
Public Class Luminance_Resolve_Shader_Class
    Inherits Shader_Base_Class
    Sub New()
        MyBase.New(PostProcessShaderSource.Compute_LumResolve)
    End Sub
End Class

''' <summary>THE SCENE DEPTH THE EFFECT SHADERS' SOFT FADE READS (one GL context), as the engine binds it to t3: FO4 the
''' world's depth (logical depth-stencil 1, 0x1422250D0 -&gt; 0x14183A530), SSE the post-z-prepass copy (depth-stencil 7,
''' 0x141556AD5). Both hold the opaque geometry already drawn; the preview copies its depth after the OPAQUE and CUTOUT
''' groups (Render.RenderAll), with ONE mechanism for the three frame paths (HDR target, display target, window): a
''' copy from the framebuffer being drawn. A copy and not the live depth: FO4 effects in the alpha list write depth
''' (Fo4RenderPassLaw / RenderableMesh.ResolveColourDepthState), and sampling the attachment being written is a feedback loop; the engine reads a
''' read-only view (0x14183C863). Tools/re-docs/DISENO_SOFT_EFFECTS_2026-10-03.md.</summary>
Friend NotInheritable Class SceneDepthCopy
    Private _tex As Integer, _w As Integer, _h As Integer

    Public ReadOnly Property Texture As Integer
        Get
            Return _tex
        End Get
    End Property

    ''' <summary>Copies the depth of framebuffer <paramref name="fbo"/> (0 = the window), w x h from the origin.</summary>
    Public Sub CopyFrom(fbo As Integer, w As Integer, h As Integer)
        If w <= 0 OrElse h <= 0 Then Return
        If _tex = 0 OrElse w <> _w OrElse h <> _h Then
            Free()
            GL.CreateTextures(TextureTarget.Texture2D, 1, _tex)
            ' Same format as the frame's depth (Depth24Stencil8, SceneTargets and the window): a depth copy needs it.
            GL.TextureStorage2D(_tex, 1, SizedInternalFormat.Depth24Stencil8, w, h)
            GL.TextureParameter(_tex, TextureParameterName.TextureMinFilter, CInt(TextureMinFilter.Nearest))
            GL.TextureParameter(_tex, TextureParameterName.TextureMagFilter, CInt(TextureMagFilter.Nearest))
            GL.TextureParameter(_tex, TextureParameterName.TextureWrapS, CInt(TextureWrapMode.ClampToEdge))
            GL.TextureParameter(_tex, TextureParameterName.TextureWrapT, CInt(TextureWrapMode.ClampToEdge))
            GL.TextureParameter(_tex, TextureParameterName.DepthStencilTextureMode, CInt(All.DepthComponent))
            _w = w : _h = h
        End If
        ' The read framebuffer is the one being drawn (RenderScene binds both): the copy reads its depth buffer.
        GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, fbo)
        GL.CopyTextureSubImage2D(_tex, 0, 0, 0, 0, 0, w, h)
    End Sub

    Public Sub Free()
        If _tex <> 0 Then GL.DeleteTexture(_tex) : _tex = 0
        _w = 0 : _h = 0
    End Sub
End Class

''' <summary>THE FRAME TARGETS OF ONE PREVIEW (one GL context).
''' <para>HDR target (the engine's forward target is R11G11B10_FLOAT, 0x142233F0A / 0x1421F9412):
''' attachment 0 = linear radiance R11F_G11F_B10F; attachment 1 = coverage RGBA8 (r composite, g actor);
''' attachment 2 = RGBA8 ground-shadow factor on the background (UI, cleared to 1, drawn only by the ground
''' catcher). Display target: RGBA8, what is presented and what every read (capture, pixel probe) reads.
''' One depth/stencil renderbuffer (24/8, like the window's) is shared by both, so what is drawn after the
''' post (wireframes) depth-tests against the scene.</para>
''' <para>Allocated lazily by the first frame (a control that never renders allocates nothing), reallocated
''' on resize, freed with the control.</para></summary>
Friend NotInheritable Class SceneTargets
    Private Const LumGroup As Integer = 16

    Private _w As Integer, _h As Integer
    Private _hdrFbo As Integer, _displayFbo As Integer
    Private _sceneTex As Integer, _coverageTex As Integer, _bgShadowTex As Integer
    ''' <summary>Attachment 3: the SSE SAO normals target (o2 of the opaque MRT, R8G8B8A8_UNORM; RE_SAO_BOTH 7.1, 11).</summary>
    Private _aoNormalTex As Integer
    Private _displayRb As Integer, _depthRb As Integer
    Private _lumPartials As Integer, _lumResult As Integer, _partialCount As Integer
    Private _lutTex As Integer
    Private _lutPath As String

    Public ReadOnly Property HdrFramebuffer As Integer
        Get
            Return _hdrFbo
        End Get
    End Property

    Public ReadOnly Property DisplayFramebuffer As Integer
        Get
            Return _displayFbo
        End Get
    End Property

    ''' <summary>Attachment 0 of the HDR target, the linear radiance (0 before the first Ensure).</summary>
    Public ReadOnly Property SceneTexture As Integer
        Get
            Return _sceneTex
        End Get
    End Property

    ''' <summary>Attachment 1 of the HDR target, the coverage (0 before the first Ensure).</summary>
    Public ReadOnly Property CoverageTexture As Integer
        Get
            Return _coverageTex
        End Get
    End Property

    ''' <summary>Attachment 3 of the HDR target, the SSE SAO normals (0 before the first Ensure).</summary>
    Public ReadOnly Property AoNormalTexture As Integer
        Get
            Return _aoNormalTex
        End Get
    End Property

    ''' <summary>The depth/stencil renderbuffer both targets share (0 before the first Ensure).</summary>
    Public ReadOnly Property DepthRenderbuffer As Integer
        Get
            Return _depthRb
        End Get
    End Property

    Public Function Ensure(w As Integer, h As Integer) As Boolean
        If w <= 0 OrElse h <= 0 Then Return False
        If _displayFbo <> 0 AndAlso w = _w AndAlso h = _h Then Return True
        FreeFrame()

        _depthRb = GL.GenRenderbuffer()
        GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _depthRb)
        GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, RenderbufferStorage.Depth24Stencil8, w, h)
        _displayRb = GL.GenRenderbuffer()
        GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _displayRb)
        GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, RenderbufferStorage.Rgba8, w, h)
        GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, 0)

        _sceneTex = NewTarget(SizedInternalFormat.R11fG11fB10f, w, h)
        _coverageTex = NewTarget(SizedInternalFormat.Rgba8, w, h)
        _bgShadowTex = NewTarget(SizedInternalFormat.Rgba8, w, h)
        _aoNormalTex = NewTarget(SizedInternalFormat.Rgba8, w, h)

        _displayFbo = GL.GenFramebuffer()
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _displayFbo)
        GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, RenderbufferTarget.Renderbuffer, _displayRb)
        GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthStencilAttachment, RenderbufferTarget.Renderbuffer, _depthRb)
        Dim ok = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer) = FramebufferErrorCode.FramebufferComplete

        _hdrFbo = GL.GenFramebuffer()
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _hdrFbo)
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, _sceneTex, 0)
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment1, TextureTarget.Texture2D, _coverageTex, 0)
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment2, TextureTarget.Texture2D, _bgShadowTex, 0)
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment3, TextureTarget.Texture2D, _aoNormalTex, 0)
        GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthStencilAttachment, RenderbufferTarget.Renderbuffer, _depthRb)
        GL.DrawBuffers(2, SceneDrawBuffers)
        ok = ok AndAlso GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer) = FramebufferErrorCode.FramebufferComplete
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0)

        _partialCount = ((w + LumGroup - 1) \ LumGroup) * ((h + LumGroup - 1) \ LumGroup)
        _lumPartials = GL.GenBuffer()
        GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _lumPartials)
        GL.BufferData(BufferTarget.ShaderStorageBuffer, _partialCount * 8, IntPtr.Zero, BufferUsageHint.DynamicCopy)
        _lumResult = GL.GenBuffer()
        GL.BindBuffer(BufferTarget.ShaderStorageBuffer, _lumResult)
        GL.BufferData(BufferTarget.ShaderStorageBuffer, 4, IntPtr.Zero, BufferUsageHint.DynamicCopy)
        GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0)

        If Not ok Then
            FreeFrame()
            Return False
        End If
        _w = w : _h = h
        Return True
    End Function

    Private Shared ReadOnly SceneDrawBuffers As DrawBuffersEnum() =
        {DrawBuffersEnum.ColorAttachment0, DrawBuffersEnum.ColorAttachment1, DrawBuffersEnum.ColorAttachment2, DrawBuffersEnum.ColorAttachment3}
    ''' <summary>An SSE frame with the post, from BeginHdr to the SAO: radiance, coverage and the SAO normals (draw buffer 3).</summary>
    Private Shared ReadOnly SceneDrawBuffersAo As DrawBuffersEnum() =
        {DrawBuffersEnum.ColorAttachment0, DrawBuffersEnum.ColorAttachment1, DrawBuffersEnum.None, DrawBuffersEnum.ColorAttachment3}

    Private Shared Function NewTarget(fmt As SizedInternalFormat, w As Integer, h As Integer) As Integer
        Dim t As Integer
        GL.CreateTextures(TextureTarget.Texture2D, 1, t)
        GL.TextureStorage2D(t, 1, fmt, w, h)
        GL.TextureParameter(t, TextureParameterName.TextureMinFilter, CInt(TextureMinFilter.Nearest))
        GL.TextureParameter(t, TextureParameterName.TextureMagFilter, CInt(TextureMagFilter.Nearest))
        GL.TextureParameter(t, TextureParameterName.TextureWrapS, CInt(TextureWrapMode.ClampToEdge))
        GL.TextureParameter(t, TextureParameterName.TextureWrapT, CInt(TextureWrapMode.ClampToEdge))
        Return t
    End Function

    ''' <summary>Binds the HDR target and clears it: radiance 0, coverage 0, background shadow 1, SAO normals (0.5, 0.5, 0, 0)
    ''' (the engine's clear of the normals RT, RE_SAO_BOTH 7.1), depth 1. Leaves attachments 0 and 1 as the draw buffers (2 is
    ''' written only by the ground catcher), plus 3 when <paramref name="aoNormals"/> (an SSE frame: until the SAO runs).</summary>
    Public Sub BeginHdr(aoNormals As Boolean)
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _hdrFbo)
        GL.Viewport(0, 0, _w, _h)
        GL.DrawBuffers(4, SceneDrawBuffers)
        GL.ClearBuffer(ClearBuffer.Color, 0, New Single() {0.0F, 0.0F, 0.0F, 0.0F})
        GL.ClearBuffer(ClearBuffer.Color, 1, New Single() {0.0F, 0.0F, 0.0F, 0.0F})
        GL.ClearBuffer(ClearBuffer.Color, 2, New Single() {1.0F, 1.0F, 1.0F, 1.0F})
        GL.ClearBuffer(ClearBuffer.Color, 3, New Single() {0.5F, 0.5F, 0.0F, 0.0F})
        GL.Clear(ClearBufferMask.DepthBufferBit Or ClearBufferMask.StencilBufferBit)
        If aoNormals Then GL.DrawBuffers(4, SceneDrawBuffersAo) Else GL.DrawBuffers(2, SceneDrawBuffers)
    End Sub

    ''' <summary>The ground catcher also writes its display-space factor on the background (attachment 2).</summary>
    Public Sub SetGroundCatcherOutputs(enabled As Boolean)
        GL.DrawBuffers(If(enabled, 3, 2), SceneDrawBuffers)
    End Sub

    ''' <summary>Binds the display target (viewport set, nothing cleared).</summary>
    Public Sub BindDisplay()
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _displayFbo)
        GL.Viewport(0, 0, _w, _h)
    End Sub

    ''' <summary>Mean luminance of the HDR target into the result buffer (GPU only, no readback).</summary>
    Public Sub ReduceLuminance(partials As Shader_Base_Class, resolve As Shader_Base_Class)
        partials.Use()
        GL.BindTextureUnit(0, _sceneTex)
        GL.BindTextureUnit(1, _coverageTex)
        partials.SetInt("texScene", 0)
        partials.SetInt("texCoverage", 1)
        GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 7, _lumPartials)
        GL.DispatchCompute((_w + LumGroup - 1) \ LumGroup, (_h + LumGroup - 1) \ LumGroup, 1)
        GL.MemoryBarrier(MemoryBarrierFlags.ShaderStorageBarrierBit)

        resolve.Use()
        resolve.SetInt("partialCount", _partialCount)
        GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 8, _lumResult)
        GL.DispatchCompute(1, 1, 1)
        GL.MemoryBarrier(MemoryBarrierFlags.ShaderStorageBarrierBit)
        GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 7, 0)
        GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 8, 0)
        GL.BindTextureUnit(0, 0)
        GL.BindTextureUnit(1, 0)
        GL.UseProgram(0)
    End Sub

    ''' <summary>The post pass into the display target: the image-space law over the HDR target, composited
    ''' over the background. <paramref name="setBackground"/> uploads the background uniforms (the same
    ''' ones the background quad receives). <paramref name="emptyVao"/> is the attribute-less VAO of the
    ''' fullscreen triangle: the core profile rejects a draw with no VAO bound (GL_INVALID_OPERATION, and the
    ''' display target keeps the previous frame).</summary>
    Public Sub Composite(post As Shader_Base_Class, uploadImageSpace As Action(Of Shader_Base_Class), lutPath As String,
                         setBackground As Action(Of Shader_Base_Class), emptyVao As Integer)
        BindDisplay()
        post.Use()

        Dim lut = If(String.IsNullOrEmpty(lutPath), 0, EnsureLut(lutPath))
        GL.BindTextureUnit(0, _sceneTex)
        GL.BindTextureUnit(1, _coverageTex)
        GL.BindTextureUnit(2, _bgShadowTex)
        GL.BindTextureUnit(3, lut)
        post.SetInt("texScene", 0)
        post.SetInt("texCoverage", 1)
        post.SetInt("texBgShadow", 2)
        post.SetInt("texLut", 3)
        post.SetFloat("lutWeight", If(lut <> 0, 1.0F, 0.0F))
        uploadImageSpace(post)
        setBackground(post)
        GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 8, _lumResult)

        ' The targets are attachments again next frame: FullscreenPass unbinds the four units.
        FullscreenPass.Draw(post, emptyVao, 4)
        GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 8, 0)
        GL.DepthMask(True)
        GL.Enable(EnableCap.DepthTest)
    End Sub

    ''' <summary>Copies the display target to the window's framebuffer (same size: 1:1, Nearest).</summary>
    Public Sub Present()
        GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, _displayFbo)
        GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, 0)
        GL.BlitFramebuffer(0, 0, _w, _h, 0, 0, _w, _h, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest)
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0)
    End Sub

    ''' <summary>The image space's LUT as the engine samples it: a 16^3 volume, UNORM (raw, no sRGB), linear
    ''' filter, clamped. The legacy 256x16 RGB24 file is unfolded with the layout of the game's identity LUT
    ''' (Textures\Effects\ColorLUT.dds measured: r = x mod 16, g = y, b = x div 16). Retried every frame while
    ''' the data dictionary cannot give the file (returns 0 = no LUT).</summary>
    Private Function EnsureLut(path As String) As Integer
        If _lutTex <> 0 AndAlso String.Equals(_lutPath, path, StringComparison.OrdinalIgnoreCase) Then Return _lutTex
        If _lutTex <> 0 Then GL.DeleteTexture(_lutTex) : _lutTex = 0
        _lutPath = Nothing
        Dim vol = LutVolume(path)
        If vol Is Nothing Then Return 0
        Dim t As Integer
        GL.CreateTextures(TextureTarget.Texture3D, 1, t)
        GL.TextureStorage3D(t, 1, SizedInternalFormat.Rgb8, 16, 16, 16)
        Dim prevUnpack As Integer
        GL.GetInteger(GetPName.UnpackAlignment, prevUnpack)
        GL.PixelStore(PixelStoreParameter.UnpackAlignment, 1)
        GL.TextureSubImage3D(t, 0, 0, 0, 0, 16, 16, 16, PixelFormat.Rgb, PixelType.UnsignedByte, vol)
        GL.PixelStore(PixelStoreParameter.UnpackAlignment, prevUnpack)
        GL.TextureParameter(t, TextureParameterName.TextureMinFilter, CInt(TextureMinFilter.Linear))
        GL.TextureParameter(t, TextureParameterName.TextureMagFilter, CInt(TextureMagFilter.Linear))
        GL.TextureParameter(t, TextureParameterName.TextureWrapS, CInt(TextureWrapMode.ClampToEdge))
        GL.TextureParameter(t, TextureParameterName.TextureWrapT, CInt(TextureWrapMode.ClampToEdge))
        GL.TextureParameter(t, TextureParameterName.TextureWrapR, CInt(TextureWrapMode.ClampToEdge))
        _lutTex = t
        _lutPath = path
        Return t
    End Function

    ''' <summary>The 16^3 RGB volume of a legacy 256x16 LUT (layout of the game's identity LUT, see EnsureLut), or Nothing
    ''' when the data dictionary cannot give the file, it does not decode or it is not 256x16: THE one answer to "does the
    ''' post use this LUT", shared by EnsureLut and the Rendering tab.</summary>
    Friend Shared Function LutVolume(path As String) As Byte()
        If String.IsNullOrEmpty(path) Then Return Nothing
        Dim bytes As Byte() = Nothing
        Try : bytes = FilesDictionary_class.GetBytes(path) : Catch : Return Nothing : End Try
        If bytes Is Nothing OrElse bytes.Length = 0 Then Return Nothing
        Dim dec = FaceTintCpuCompositor.DecodeDds(bytes)
        If dec Is Nothing OrElse dec.Rgba8 Is Nothing OrElse dec.Width <> 256 OrElse dec.Height <> 16 Then Return Nothing
        Dim vol(16 * 16 * 16 * 3 - 1) As Byte
        For b = 0 To 15
            For g = 0 To 15
                For r = 0 To 15
                    Dim src = (g * 256 + b * 16 + r) * 4
                    Dim dst = ((b * 16 + g) * 16 + r) * 3
                    vol(dst) = dec.Rgba8(src) : vol(dst + 1) = dec.Rgba8(src + 1) : vol(dst + 2) = dec.Rgba8(src + 2)
                Next
            Next
        Next
        Return vol
    End Function

    Private Sub FreeFrame()
        If _hdrFbo <> 0 Then GL.DeleteFramebuffer(_hdrFbo) : _hdrFbo = 0
        If _displayFbo <> 0 Then GL.DeleteFramebuffer(_displayFbo) : _displayFbo = 0
        If _sceneTex <> 0 Then GL.DeleteTexture(_sceneTex) : _sceneTex = 0
        If _coverageTex <> 0 Then GL.DeleteTexture(_coverageTex) : _coverageTex = 0
        If _bgShadowTex <> 0 Then GL.DeleteTexture(_bgShadowTex) : _bgShadowTex = 0
        If _aoNormalTex <> 0 Then GL.DeleteTexture(_aoNormalTex) : _aoNormalTex = 0
        If _displayRb <> 0 Then GL.DeleteRenderbuffer(_displayRb) : _displayRb = 0
        If _depthRb <> 0 Then GL.DeleteRenderbuffer(_depthRb) : _depthRb = 0
        If _lumPartials <> 0 Then GL.DeleteBuffer(_lumPartials) : _lumPartials = 0
        If _lumResult <> 0 Then GL.DeleteBuffer(_lumResult) : _lumResult = 0
        _partialCount = 0
        _w = 0 : _h = 0
    End Sub

    ''' <summary>Frees every GL object (the context must be current).</summary>
    Public Sub Free()
        FreeFrame()
        If _lutTex <> 0 Then GL.DeleteTexture(_lutTex) : _lutTex = 0
        _lutPath = Nothing
    End Sub
End Class
