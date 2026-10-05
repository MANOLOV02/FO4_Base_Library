' GENERATED from the SAO transcription (scratchpad sao-sse/transcripcion, propuesta-FINAL.md): do not edit the GLSL by hand.
''' <summary>THE SKYRIM SE SAO (non-temporal RawAO, PS 16012) AS THE PREVIEW RUNS IT: the six passes transcribed from the
''' bytecode line by line (CameraZ 15997, Minify 16006, MinifyContrast 16008, RawAO 16012, BlurH 15991, BlurV 15993), exact
''' literal tokens, D3D11 semantics helpers. Tools/re-docs/RE_SAO_BOTH_2026-10-03.md. The only edit to the transcribed text is
''' the user's decision C in CameraZ (a pixel with depth exactly 1.0 is the sky). ASCII only (GLSL).</summary>
Friend Module SseSaoSource
    ''' <summary>Full-screen triangle of the six passes (VS_SAO_FULLSCREEN).</summary>
    Friend Const Vs_Fullscreen As String = "#version 430
// Full-screen triangle for the six SAO passes (draw 3 vertices, no vertex buffer). Emits the D3D TEXCOORD0:
// under UPPER_LEFT, clip y = +1 lands on render-target row 0, where the D3D uv has v = 0.
// The engine's image-space VS is NOT TRACED; this VS only has to deliver uv_d3d = pixel centre / (W, H).
layout(location = 0) out vec2 v1;
void main()
{
    vec2 p = vec2((gl_VertexID == 1) ? 3.0 : -1.0, (gl_VertexID == 2) ? 3.0 : -1.0);
    v1 = vec2(p.x * 0.5 + 0.5, 0.5 - p.y * 0.5);
    gl_Position = vec4(p, 0.0, 1.0);
}
"

    Friend Const Header430 As String = "#version 430
"

    Friend Const Header430Deriv As String = "#version 430
#extension GL_ARB_derivative_control : require
"

    Friend Const Prelude As String = "// ---------------------------------------------------------------------------------------------------------------------
// CONVENTION (SSE SAO transcription) -- all six passes run under glClipControl(GL_UPPER_LEFT, GL_NEGATIVE_ONE_TO_ONE)
//  * The app sets glClipControl(GL_UPPER_LEFT, GL_NEGATIVE_ONE_TO_ONE) before CameraZ and restores
//    (GL_LOWER_LEFT, GL_NEGATIVE_ONE_TO_ONE) after BlurV. Per ARB_clip_control (core in GL 4.5) UPPER_LEFT only negates
//    the NDC y: y_d = f * y_c / w_c with f = -1; window coordinates keep their definition (glViewport/glReadPixels origin
//    'independent of the clip control origin state'). So a vertex at clip y = +1 lands in window row 0 = memory row 0,
//    exactly where D3D puts NDC y = +1 (render-target row 0). Consequences, for every SAO pass:
//      - gl_FragCoord.xy has the same value as D3D SV_Position.xy for the same pixel (row 0 = D3D top row);
//      - the window y of GL and the screen y of D3D grow in the same direction over the image, so the 2x2 derivative
//        quads (aligned on even window coordinates) are the same pixels as D3D's for any W', H';
//      - the SAO's own targets (CameraZ/Minify pyramid, raw AO, blur H, kSAO) are written with D3D row order, and a GL
//        texture coordinate t = 0 addresses memory row 0: they are sampled with the D3D uv directly, with no flip, so
//        point-sampling ties (texel borders) fall on the same texel as in D3D.
//    Face culling must be disabled (or its winding flipped) for the full-screen triangle; the depth mode is irrelevant
//    (no depth test or write in these passes, RE SAO 1.8).
//  * v1 = the D3D TEXCOORD0 (uv_d3d, (0,0) at the top-left of the render target), interpolated from VS_SAO_FULLSCREEN.
//    All pixel-derived quantities are computed from v1 exactly as the asm does (hash phi(x,y), parities, ProjInfos
//    reconstruction, 5x5 Contrast grid, |uv*2-1|^2): at pixel centres ftoi(v1 * (W,H)) == floor(gl_FragCoord.xy).
//  * The ONLY flips are on the two inputs produced by the app's normal scene render (LOWER_LEFT, memory row 0 = bottom):
//        CameraZ    asm 33  t0 = scene depth   -> texture(t0, sse_sceneInputTc(uv))
//        RawAO16012 asm 52  t1 = normals       -> textureGrad(t1, sse_sceneInputTc(uv), 0, 0)
//    sse_sceneInputTc(s, t) = (s, 1 - t). The scene targets have the same size as the SAO targets (W x H, no
//    downscale) and with cb12[43] = (1,1,1,1) both reads happen at texel centres: t = (y + 0.5)/H maps to
//    1 - t = (H - 1 - y + 0.5)/H, the centre of the mirrored row. CameraZ is point-sampled (FILT 0): the texel choice is
//    exact (the float rounding of 1 - t is ~1e-7 of a texel, far from a border). The normals are bilinear (FILT 1) at a
//    texel centre: the fractional weight is 0 up to that ~1e-7*H residue, below the hardware's subtexel resolution
//    (D3D11 requires >= 8 bits), so the read returns the texel itself. Not exact any more if dynamic resolution
//    (cb12[43].xy < 1) moves the reads off texel centres.
//  * Consumer of kSAO: the composite runs under LOWER_LEFT and must read kSAO with (u, 1 - v) (it is stored in D3D row
//    order); RE SAO 1.7: point sampling at texel centres, so that flip is exact too. Not part of these six shaders.
//  * Screen-space derivatives: deriv_rtx_coarse -> dFdxCoarse, deriv_rty_coarse -> dFdyCoarse with NO sign change.
//    GLSL 4.60 8.14.1: dFdyCoarse 'Returns the partial derivative of p with respect to the window y coordinate'; under
//    UPPER_LEFT the window y grows downwards over the image like D3D's screen y (see above).
//  * View space: the engine view space is x right, y up, +z forward (RE SAO 7.1). The app's GL view space is x right,
//    y up, -z forward. Mapping (positions and normals): v_engine = (v_gl.x, v_gl.y, -v_gl.z). Inside RawAO the
//    reconstructed C/Q live in (-x, y, z) of the engine view (g_ProjInfos.x < 0) and the decoded normal is mirrored the
//    same way (asm 16012 line 58); the app only has to encode n_engine into t1:
//        t1.xy = n_e.xy / max(sqrt(8 - 8*n_e.z), 0.001) + 0.5     with n_e = (n_gl.x, n_gl.y, -n_gl.z)
//  * Literals: every literal is the exact 32-bit token of the SHEX chunk (#define NAME uintBitsToFloat(0x........u);
//    a macro, not a const, because not every compiler folds uintBitsToFloat in a const initializer) whenever the
//    6-decimal print of the disassembler is not that same float (printed value kept in the comment).
// ---------------------------------------------------------------------------------------------------------------------

layout(location = 0) in vec2 v1;    // TEXCOORD0 v1.xy (D3D uv) from VS_SAO_FULLSCREEN (asm: dcl_input_ps linear v1.xy)

// D3D uv -> GL texture coordinate of an input rendered by the app's scene pass under LOWER_LEFT (see CONVENTION).
// Used only at CameraZ asm 33 (scene depth) and RawAO asm 52 (normals).
vec2 sse_sceneInputTc(vec2 uv) { return vec2(uv.x, 1.0 - uv.y); }
"

    Friend Const MinifyCommon As String = "// Shared body of PS 16006 (lines 29..73) and PS 16008 (lines 54..98): identical instruction streams, offset 25.
// 'asm a/b' = line a of ps_16006.asm / line b of ps_16008.asm.
layout(binding = 0) uniform sampler2D t0;          // asm 25/50  dcl_resource_texture2d t0 (source mip i-1); s0 asm 24/49
uniform vec4 g_RenderTargetResolution;              // cb2[0]  (Ws, Hs, ox, oy)
uniform vec4 g_UseDynamicSampling;                  // cb2[2]  .x tested as raw bits
uniform vec4 uCB12_41;                              // cb12[41] (.w)
uniform vec4 uCB12_43;                              // cb12[43] (.xy)

#define SSE_MIN_ONE_SIXTH   uintBitsToFloat(0x3E2AAAABu)    // l(0.166667) token 0x3E2AAAAB (= 1/6 in float)

// Returns the value the engine holds in o0 (16006) or r1 (16008) after the if/else.
vec4 SseMinifyCommon(vec2 v1)
{
    bool  r0x  = 0u < floatBitsToUint(g_UseDynamicSampling.x);                       // asm 29/54  ult r0.x, l(0), cb2[2].x
    vec2  r0yz = v1 * uCB12_43.xy;                                                     // asm 30/55  mul r0.yz, v1.xxyx, cb12[43].xxyx
    vec2  r1   = vec2(r0yz.y * g_RenderTargetResolution.y,
                      r0yz.x * g_RenderTargetResolution.x);                            // asm 31/56  mul r1.xy, r0.zyzz, cb2[0].yxyy
    ivec2 i1   = ivec2(d3d_ftoi(r1.x), d3d_ftoi(r1.y));                                // asm 32/57  ftoi r1.xy
    i1 = i1 & ivec2(1);                                                                // asm 33/58  and r1.xy, r1.xyxx, l(1,1,0,0)
    i1 = i1 ^ ivec2(1);                                                                // asm 34/59  xor r1.xy, r1.xyxx, l(1,1,0,0)
    r1 = vec2(i1);                                                                     // asm 35/60  itof r1.xy
    r1 = r1 * 2.0 + (-1.0);                                                            // asm 36/61  mad r1.xy, r1, l(2,2), l(-1,-1)
    r0yz = r1 * g_RenderTargetResolution.zw + r0yz;                                    // asm 37/62  mad r0.yz, r1.xxyx, cb2[0].zzwz, r0.yyzy
    r1 = vec2(-uCB12_41.w, -0.0);                                                      // asm 38/63, 39/64  mov r1.x, -cb12[41].w ; mov r1.y, l(-0.0)
    r1 = r1 + uCB12_43.xy;                                                             // asm 40/65  add r1.xy, r1.xyxx, cb12[43].xyxx
    r0yz = d3d_max(r0yz, vec2(0.0));                                                   // asm 41/66  max r0.yz, r0.yyzy, l(0)
    r0yz = d3d_min(r1, r0yz);                                                          // asm 42/67  min r0.yz, r1.xxyx, r0.yyzy
    r1 = vec2(v1.y * g_RenderTargetResolution.y, v1.x * g_RenderTargetResolution.x);  // asm 43/68  mul r1.xy, v1.yxyy, cb2[0].yxyy
    i1 = ivec2(d3d_ftoi(r1.x), d3d_ftoi(r1.y));                                        // asm 44/69  ftoi r1.xy
    i1 = i1 & ivec2(1);                                                                // asm 45/70  and
    i1 = i1 ^ ivec2(1);                                                                // asm 46/71  xor
    r1 = vec2(i1);                                                                     // asm 47/72  itof
    r1 = r1 * 2.0 + (-1.0);                                                            // asm 48/73  mad
    r1 = r1 * g_RenderTargetResolution.zw + v1;                                        // asm 49/74  mad r1.xy, r1.xyxx, cb2[0].zwzz, v1.xyxx
    vec2 r0xy = r0x ? r0yz : r1;                                                       // asm 50/75  movc r0.xy, r0.xxxx, r0.yzyy, r1.xyxx
    float r0z = d3d_max(g_RenderTargetResolution.y, g_RenderTargetResolution.x);       // asm 51/76  max r0.z, cb2[0].y, cb2[0].x
    if (r0z == 3.0)                                                                    // asm 52/77  eq ; asm 53/78 if_nz
    {
        ivec2 i0 = ivec2(d3d_ftoi(g_RenderTargetResolution.x),
                         d3d_ftoi(g_RenderTargetResolution.y));                        // asm 54/79  ftoi r0.zw, cb2[0].xxxy
        i0 = i0 & ivec2(1);                                                            // asm 55/80  and r0.zw, l(0,0,1,1)
        vec2 r0zw = vec2(i0);                                                          // asm 56/81  itof
        r0zw = r0zw * SSE_MIN_ONE_SIXTH + 0.5;                                         // asm 57/82  mad r0.zw, r0.zzzw, l(0.166667), l(0.5)
        vec2 d = r0zw / g_RenderTargetResolution.xy;                                   // asm 58/83  div r1.xy, r0.zwzz, cb2[0].xyxx
        vec2 pA = r0xy + d;                                                            // asm 59/84  add r0.zw, r0.xxxy, r1.xxxy
        float s = texture(t0, pA).x;                                        // asm 60/85  sample r0.z, r0.zwzz, t0.yzxw (z <- .x)
        vec2 r1zw = -d;                                                                // asm 61/86  mov r1.zw, -r1.xxxy
        vec4 r2 = r0xy.xyxy + vec4(r1zw.x, d.y, d.x, r1zw.y);                          // asm 62/87  add r2.xyzw, r0.xyxy, r1.zyxw
        float w = texture(t0, r2.xy).x;                                     // asm 63/88  sample r0.w, r2.xyxx, t0.yzwx (w <- .x)
        s = w + s;                                                                     // asm 64/89  add r0.z, r0.w, r0.z
        vec2 pC = r0xy + (-d);                                                         // asm 65/90  add r1.xy, r0.xyxx, -r1.xyxx
        w = texture(t0, pC).x;                                              // asm 66/91  sample r0.w, r1.xyxx, t0.yzwx
        s = w + s;                                                                     // asm 67/92  add
        w = texture(t0, r2.zw).x;                                           // asm 68/93  sample r0.w, r2.zwzz, t0.yzwx
        s = w + s;                                                                     // asm 69/94  add
        return vec4(s * 0.25, 0.0, 0.0, 0.0);                                         // asm 70/95  mul .x, l(0.25) ; asm 71/96 mov .yzw, l(0)
    }
    else                                                                               // asm 72/97
    {
        return texture(t0, r0xy);                                           // asm 73/98  sample xyzw, r0.xyxx, t0.xyzw
    }                                                                                  // asm 75/99  endif
}
"

    Friend Const BlurCommon As String = "// Shared body of PS 15991 (BlurH) and PS 15993 (BlurV): same line numbering, same instruction stream; the only
// differences are the immediates of lines 37/43/49 (taken along x for H, along y for V) and the outputs (104..106).
// axis = (1,0) for 15991, (0,1) for 15993: axis * k reproduces the asm immediates bit for bit, signed zeros included
//   15991: l(-6,-0,-4,-0) l(-2,-0,2,0) l(4,0,6,0) ; 15993: l(-0,-6,-0,-4) l(-0,-2,0,2) l(0,4,0,6).
layout(binding = 0) uniform sampler2D t0;          // asm 25  dcl_resource_texture2d t0 ; s0 asm 24
uniform vec4 g_BlurScreenInfos;                     // cb2[0]  (W', H', 1/W', 1/H')
uniform vec4 uCB12_43;                              // cb12[43] (.xy)
uniform vec4 uCB12_44;                              // cb12[44] (.z)

#define SSE_BLUR_KEY_HI   uintBitsToFloat(0x3F7F00FFu)   // l(0.996109) token 0x3F7F00FF (= 256/257)
#define SSE_BLUR_KEY_LO   uintBitsToFloat(0x3B7F00FFu)   // l(0.003891) token 0x3B7F00FF (= 1/257)
#define SSE_BLUR_W0       uintBitsToFloat(0x3E1CD899u)   // l(0.153170) token 0x3E1CD899
#define SSE_BLUR_W2       uintBitsToFloat(0x3EE3C904u)   // l(0.444893) token 0x3EE3C904
#define SSE_BLUR_W4       uintBitsToFloat(0x3ED86574u)   // l(0.422649) token 0x3ED86574
#define SSE_BLUR_W6       uintBitsToFloat(0x3EC92A74u)   // l(0.392902) token 0x3EC92A74
#define SSE_BLUR_EPS      uintBitsToFloat(0x38D1B717u)   // l(0.000100) token 0x38D1B717

struct SseBlurResult
{
    bool  earlyOut;   // asm 36/55: key_0 == 1
    vec2  centreYZ;   // r0.yz of the centre sample (asm 34)
    float ratio;      // asm 104: num / den
};

SseBlurResult SseSaoBlur(vec2 v1, vec2 axis)
{
    SseBlurResult res;
    vec2 r0xy = v1 * uCB12_43.xy;                                                  // asm 29  mul r0.xy, v1.xyxx, cb12[43].xyxx
    r0xy = d3d_max(r0xy, vec2(0.0));                                               // asm 30  max
    vec2 r1xy = vec2(uCB12_44.z, uCB12_43.y);                                      // asm 31  mov r1.x, cb12[44].z ; asm 32 mov r1.y, cb12[43].y
    r0xy = d3d_min(r0xy, r1xy);                                                    // asm 33  min r0.xy, r0.xyxx, r1.xyxx
    vec3 c = texture(t0, r0xy).xyz;                                     // asm 34  sample r0.xyz, r0.xyxx, t0.xyzw
    float key0 = dot(c.yz, vec2(SSE_BLUR_KEY_HI, SSE_BLUR_KEY_LO));                // asm 35  dp2 r0.w, r0.yzyy, l(0.996109, 0.003891)
    bool r1z = key0 == 1.0;                                                        // asm 36  eq r1.z, r0.w, l(1.0)

    vec4 r2 = g_BlurScreenInfos.zwzw * vec4(axis * -6.0, axis * -4.0) + v1.xyxy;   // asm 37  mad r2.xyzw, cb2[0].zwzw, l(-6|-4 along axis), v1.xyxy
    r2 = r2 * uCB12_43.xyxy;                                                       // asm 38  mul r2.xyzw, r2.xyzw, cb12[43].xyxy
    r2 = d3d_max(r2, vec4(0.0));                                                   // asm 39  max
    r2 = d3d_min(r1xy.xyxy, r2);                                                   // asm 40  min r2.xyzw, r1.xyxy, r2.xyzw
    vec3 sM6 = texture(t0, r2.xy).xyz;                                  // asm 41  sample r3.xyz, r2.xyxx   (tap -6)
    vec3 sM4 = texture(t0, r2.zw).xyz;                                  // asm 42  sample r2.xyz, r2.zwzz   (tap -4)
    vec4 r4 = g_BlurScreenInfos.zwzw * vec4(axis * -2.0, axis * 2.0) + v1.xyxy;    // asm 43  mad r4.xyzw, cb2[0].zwzw, l(-2|+2 along axis), v1.xyxy
    r4 = r4 * uCB12_43.xyxy;                                                       // asm 44  mul
    r4 = d3d_max(r4, vec4(0.0));                                                   // asm 45  max
    r4 = d3d_min(r1xy.xyxy, r4);                                                   // asm 46  min
    vec3 sM2 = texture(t0, r4.xy).xyz;                                  // asm 47  sample r5.xyz, r4.xyxx   (tap -2)
    vec3 sP2 = texture(t0, r4.zw).xyz;                                  // asm 48  sample r4.xyz, r4.zwzz   (tap +2)
    vec4 r6 = g_BlurScreenInfos.zwzw * vec4(axis * 4.0, axis * 6.0) + v1.xyxy;     // asm 49  mad r6.xyzw, cb2[0].zwzw, l(+4|+6 along axis), v1.xyxy
    r6 = r6 * uCB12_43.xyxy;                                                       // asm 50  mul
    r6 = d3d_max(r6, vec4(0.0));                                                   // asm 51  max
    r6 = d3d_min(r1xy.xyxy, r6);                                                   // asm 52  min
    vec3 sP4 = texture(t0, r6.xy).xyz;                                  // asm 53  sample r1.xyw, r6.xyxx, t0.xywz (x<-.x, y<-.y, w<-.z) (tap +4)
    vec3 sP6 = texture(t0, r6.zw).xyz;                                  // asm 54  sample r6.xyz, r6.zwzz   (tap +6)

    res.centreYZ = c.yz;
    res.earlyOut = r1z;                                                            // asm 55  if_nz r1.z
    res.ratio    = 0.0;
    if (r1z)
    {
        return res;                                                                // asm 56..58 (outputs written by the pass body)
    }                                                                              // asm 59  endif

    // tap -6 (asm 60..67; 15991 registers r1.z/r2.w, 15993 r0.y/r0.z)
    float w = dot(sM6.yz, vec2(SSE_BLUR_KEY_HI, SSE_BLUR_KEY_LO));                 // asm 60  dp2
    w = -key0 + w;                                                                 // asm 61  add  x, -r0.w, x
    w = -abs(w) * 2000.0 + 1.0;                                                    // asm 62  mad  x, -|x|, l(2000), l(1)
    w = d3d_max(w, 0.0);                                                           // asm 63  max
    float t = w * SSE_BLUR_W6;                                                     // asm 64  mul  t, w, l(0.392902)
    t = t * sM6.x;                                                                 // asm 65  mul  t, t, ao(-6)
    float num = c.x * SSE_BLUR_W0 + t;                                             // asm 66  mad  num, ao(0), l(0.153170), t
    float den = w * SSE_BLUR_W6 + SSE_BLUR_W0;                                     // asm 67  mad  den, w, l(0.392902), l(0.153170)
    // tap -4
    w = dot(sM4.yz, vec2(SSE_BLUR_KEY_HI, SSE_BLUR_KEY_LO));                       // asm 68  dp2
    w = -key0 + w;                                                                 // asm 69  add
    w = -abs(w) * 2000.0 + 1.0;                                                    // asm 70  mad
    w = d3d_max(w, 0.0);                                                           // asm 71  max
    t = w * SSE_BLUR_W4;                                                           // asm 72  mul  t, w, l(0.422649)
    num = sM4.x * t + num;                                                         // asm 73  mad  num, ao(-4), t, num
    den = w * SSE_BLUR_W4 + den;                                                   // asm 74  mad  den, w, l(0.422649), den
    // tap -2
    w = dot(sM2.yz, vec2(SSE_BLUR_KEY_HI, SSE_BLUR_KEY_LO));                       // asm 75  dp2
    w = -key0 + w;                                                                 // asm 76  add
    w = -abs(w) * 2000.0 + 1.0;                                                    // asm 77  mad
    w = d3d_max(w, 0.0);                                                           // asm 78  max
    t = w * SSE_BLUR_W2;                                                           // asm 79  mul  t, w, l(0.444893)
    num = sM2.x * t + num;                                                         // asm 80  mad
    den = w * SSE_BLUR_W2 + den;                                                   // asm 81  mad
    // tap +2
    w = dot(sP2.yz, vec2(SSE_BLUR_KEY_HI, SSE_BLUR_KEY_LO));                       // asm 82  dp2
    w = -key0 + w;                                                                 // asm 83  add
    w = -abs(w) * 2000.0 + 1.0;                                                    // asm 84  mad
    w = d3d_max(w, 0.0);                                                           // asm 85  max
    t = w * SSE_BLUR_W2;                                                           // asm 86  mul  t, w, l(0.444893)
    num = sP2.x * t + num;                                                         // asm 87  mad
    den = w * SSE_BLUR_W2 + den;                                                   // asm 88  mad
    // tap +4 (15991: r1.x = ao, r1.yw = key channels via t0.xywz)
    w = dot(sP4.yz, vec2(SSE_BLUR_KEY_HI, SSE_BLUR_KEY_LO));                       // asm 89  dp2  (15991: r1.ywyy)
    w = -key0 + w;                                                                 // asm 90  add
    w = -abs(w) * 2000.0 + 1.0;                                                    // asm 91  mad
    w = d3d_max(w, 0.0);                                                           // asm 92  max
    t = w * SSE_BLUR_W4;                                                           // asm 93  mul  t, w, l(0.422649)
    num = sP4.x * t + num;                                                         // asm 94  mad
    den = w * SSE_BLUR_W4 + den;                                                   // asm 95  mad
    // tap +6
    w = dot(sP6.yz, vec2(SSE_BLUR_KEY_HI, SSE_BLUR_KEY_LO));                       // asm 96  dp2
    w = -key0 + w;                                                                 // asm 97  add
    w = -abs(w) * 2000.0 + 1.0;                                                    // asm 98  mad
    w = d3d_max(w, 0.0);                                                           // asm 99  max
    t = w * SSE_BLUR_W6;                                                           // asm 100 mul  t, w, l(0.392902)
    num = sP6.x * t + num;                                                         // asm 101 mad
    den = w * SSE_BLUR_W6 + den;                                                   // asm 102 mad
    den = den + SSE_BLUR_EPS;                                                      // asm 103 add  den, den, l(0.0001)
    res.ratio = num / den;                                                         // asm 104 div
    return res;
}
"

    Friend Const Body_CameraZ As String = "// PS 15997 -- ISSAOCameraZ. RE SAO 1.6.1. Output R32_FLOAT (pyramid level 0).
layout(binding = 0) uniform sampler2D t0;   // asm 25  dcl_resource_texture2d t0 (scene depth) ; s0 asm 24
uniform vec4 g_ClipInfos;                   // cb2[0]  (n*f, n-f, f, zmax)
uniform vec4 uCB12_43;                      // cb12[43] (.xy)
uniform vec4 uCB12_44;                      // cb12[44] (.z)
layout(location = 0) out float o0;          // asm 18, 27  SV_Target0.x (R32F)

void main()
{
    vec2 r0xy = v1 * uCB12_43.xy;                               // asm 29  mul r0.xy, v1.xyxx, cb12[43].xyxx
    r0xy = d3d_max(r0xy, vec2(0.0));                            // asm 30  max r0.xy, r0.xyxx, l(0)
    vec2 r1xy;
    r1xy.x = d3d_min(r0xy.x, uCB12_44.z);                       // asm 31  min r1.x, r0.x, cb12[44].z
    r1xy.y = d3d_min(r0xy.y, uCB12_43.y);                       // asm 32  min r1.y, r0.y, cb12[43].y
    float r0x = texture(t0, sse_sceneInputTc(r1xy)).x;          // asm 33  sample r0.x, r1.xyxx, t0.xyzw, s0 (scene depth: LOWER_LEFT input, flipped)
    float sseSceneDepth = r0x;
    r0x = g_ClipInfos.y * r0x + g_ClipInfos.z;                  // asm 34  mad r0.x, cb2[0].y, r0.x, cb2[0].z
    r0x = g_ClipInfos.x / r0x;                                  // asm 35  div r0.x, cb2[0].x, r0.x
    // User decision C (3-oct-2026): a pixel without geometry (depth exactly 1.0) is the sky, z = zmax (fDOFMaxDepthParticipation 10000).
    if (sseSceneDepth == 1.0) r0x = g_ClipInfos.w;
    r0x = d3d_max(r0x, 0.0);                                    // asm 36  max r0.x, r0.x, l(0)
    o0 = d3d_min(r0x, g_ClipInfos.w);                           // asm 37  min o0.x, r0.x, cb2[0].w
}                                                               // asm 38  ret
"

    Friend Const Body_Minify As String = "// PS 16006 -- ISMinify. RE SAO 1.6.2 / 12.3. Output R32_FLOAT pyramid level i (o0.xyzw declared; only .x lands in R32F).
layout(location = 0) out vec4 o0;           // asm 18, 27  SV_Target0.xyzw

void main()
{
    o0 = SseMinifyCommon(v1);                                   // asm 29..75 (71: o0.x/o0.yzw ; 74: mov o0.xyzw, r0.xyzw)
}                                                               // asm 76  ret
"

    Friend Const Body_MinifyContrast As String = "// PS 16008 -- ISMinifyContrast. RE SAO 1.6.2 / 12.3. Output R32_FLOAT pyramid level i.
uniform vec4 g_ContrastParams;              // cb2[1]  (.x = 1/(fDOFCenterWeight + 1.19209e-7))
layout(location = 0) out vec4 o0;           // asm 18, 52  SV_Target0.xyzw

#define SSE_ICB_0_3   uintBitsToFloat(0x3E99999Au)    // l(0.300000) token 0x3E99999A
#define SSE_ICB_0_4   uintBitsToFloat(0x3ECCCCCDu)    // l(0.400000) token 0x3ECCCCCD
void main()
{
    // asm 22..46  dcl_immediateConstantBuffer (25 x .x; .yzw = 0 and never read). Local array: a global const
    // initializer may not call uintBitsToFloat on every compiler (glslang rejects it).
    float icbTable[25] = float[25](
        SSE_ICB_0_3, SSE_ICB_0_4, 0.5,         SSE_ICB_0_4, SSE_ICB_0_3,     // asm 22..26
        SSE_ICB_0_4, 2.0,         2.5,         2.0,         SSE_ICB_0_4,     // asm 27..31
        0.5,         2.5,         3.5,         2.5,         0.5,             // asm 32..36
        SSE_ICB_0_4, 2.0,         2.5,         2.0,         SSE_ICB_0_4,     // asm 37..41
        SSE_ICB_0_3, SSE_ICB_0_4, 0.5,         SSE_ICB_0_4, SSE_ICB_0_3);    // asm 42..46

    vec4 r1 = SseMinifyCommon(v1);                              // asm 54..99 (95/96: r1.x, r1.yzw ; 98: sample r1.xyzw)
    vec2 r0xy = v1 * vec2(5.0, 5.0);                            // asm 100 mul r0.xy, v1.xyxx, l(5,5)
    ivec2 i0 = ivec2(d3d_ftoi(r0xy.x), d3d_ftoi(r0xy.y));       // asm 101 ftoi r0.xy
    int k = i0.y * 5 + i0.x;                                    // asm 102 imad r0.x, r0.y, l(5), r0.x
    // asm 103 mul r0.x, cb2[1].x, icb[r0.x + 0].x ; out-of-range icb index (unsigned) reads 0 (RE SAO 12.3)
    float icb = (uint(k) < 25u) ? icbTable[k] : 0.0;
    float r0x = g_ContrastParams.x * icb;                       // asm 103
    o0 = vec4(r0x) * r1;                                        // asm 104 mul o0.xyzw, r0.xxxx, r1.xyzw
}                                                               // asm 105 ret
"

    Friend Const Body_RawAO As String = "// PS 16012 -- ISSAORawAONoTemporal. RE SAO 1.6.3 (16012 brackets), 5.3, 7.1. Output R8G8B8A8_UNORM.
layout(binding = 0) uniform sampler2D t0;   // asm 26  t0 = RT 0x31 CameraZ pyramid, all mips ; s0 asm 24 (FILT 2)
layout(binding = 1) uniform sampler2D t1;   // asm 27  t1 = normals target (RGBA8)        ; s1 asm 25 (FILT 1)
uniform vec4 g_ProjInfos;                   // cb2[0]
uniform vec4 g_SSAOInfos;                   // cb2[1]  (.y = 100*R, .z = bias, .w = I/R^6)
uniform vec4 g_ScreenInfos;                 // cb2[2]  (W', H', 1/W', 1/H')
uniform vec4 g_SSAOInfos2;                  // cb2[3]  (.x = fSAOExpFactor, .y = R^2, .w = zmin)
uniform vec4 uCB12_43;                      // cb12[43] (.xy)
uniform vec4 uCB12_44;                      // cb12[44] (.z)
layout(location = 0) out vec4 o0;           // asm 18, 29  SV_Target0.xyzw

#define SSE_AO_KEY_SCALE    uintBitsToFloat(0x3915CBECu)    // l(0.000143) token 0x3915CBEC (= 1/7000 in float)
#define SSE_AO_INV256       uintBitsToFloat(0x3B800000u)    // l(0.003906) token 0x3B800000 (= 1/256 exactly)
#define SSE_AO_NEG_0_3      uintBitsToFloat(0xBE99999Au)    // l(-0.300000) token 0xBE99999A
#define SSE_AO_GOLDEN       uintBitsToFloat(0x40565B7Bu)    // l(3.349334) token 0x40565B7B
#define SSE_AO_INV15        uintBitsToFloat(0x3D888889u)    // l(0.066667) token 0x3D888889 (= 1/15 in float)
#define SSE_AO_VV_EPS       uintBitsToFloat(0x3C23D70Au)    // l(0.010000) token 0x3C23D70A
#define SSE_AO_INV3         uintBitsToFloat(0x3EAAAAABu)    // l(0.333333) token 0x3EAAAAAB (= 1/3 in float)
#define SSE_AO_EXP_MIN      uintBitsToFloat(0x3727C5ACu)    // l(0.000010) token 0x3727C5AC
#define SSE_AO_EXP_MAX      uintBitsToFloat(0x3F7FFF58u)    // l(0.999990) token 0x3F7FFF58
#define SSE_AO_DERIV_GATE   uintBitsToFloat(0x3CA3D70Au)    // l(0.020000) token 0x3CA3D70A

void main()
{
    vec2 r0xy = v1 * g_ScreenInfos.xy;                                      // asm 31  mul r0.xy, v1.xyxx, cb2[2].xyxx
    ivec2 r0zw = ivec2(d3d_ftoi(r0xy.x), d3d_ftoi(r0xy.y));                 // asm 32  ftoi r0.zw, r0.xxxy  (= x_d3d, y_d3d)
    vec2 r1xy = v1 * uCB12_43.xy;                                           // asm 33  mul r1.xy, v1.xyxx, cb12[43].xyxx
    r1xy = d3d_max(r1xy, vec2(0.0));                                        // asm 34  max
    float r2x = d3d_min(r1xy.x, uCB12_44.z);                                // asm 35  min r2.x, r1.x, cb12[44].z
    vec3 r2yzw = vec3(d3d_min(r1xy.y, uCB12_43.y),
                      d3d_min(r1xy.x, uCB12_43.x),
                      d3d_min(r1xy.y, uCB12_43.y));                         // asm 36  min r2.yzw, r1.yyxy, cb12[43].yyxy
    float r1z = textureLod(t0, vec2(r2x, r2yzw.x), 0.0).x;                  // asm 37  sample_l r1.z, r2.xyxx, t0.yzxw, s0, l(0) (z <- .x) = C.z
    r0xy = trunc(r0xy);                                                     // asm 38  round_z
    r0xy = r0xy + 0.5;                                                      // asm 39  add l(0.5)
    r0xy = r0xy * g_ProjInfos.xy + g_ProjInfos.zw;                          // asm 40  mad r0.xy, r0.xyxx, cb2[0].xyxx, cb2[0].zwzz
    r1xy = vec2(r1z) * r0xy;                                                // asm 41  mul r1.xy, r1.zzzz, r0.xyxx   (C.xy)
    float r0x = d3d_sat(r1z * SSE_AO_KEY_SCALE);                            // asm 42  mul_sat r0.x, r1.z, l(0.000143)  (key)
    float r0y = r0x * 256.0;                                                // asm 43  mul l(256)
    r0y = floor(r0y);                                                       // asm 44  round_ni
    float o0y = r0y * SSE_AO_INV256;                                        // asm 45  mul o0.y, r0.y, l(0.003906)
    float o0z = r0x * 256.0 + (-r0y);                                       // asm 46  mad o0.z, r0.x, l(256), -r0.y
    int hA = r0zw.x * 3;                                                    // asm 47  imul null, r0.y, r0.z, l(3)
    int hB = r0zw.x * r0zw.y + r0zw.y;                                      // asm 48  imad r1.w, r0.z, r0.w, r0.w
    int h  = hA ^ hB;                                                       // asm 49  xor
    r0y = float(h);                                                         // asm 50  itof
    r0y = r0y * 10.0;                                                       // asm 51  mul l(10)   (phi)
    vec2 r2xy = textureGrad(t1, sse_sceneInputTc(r2yzw.yz), vec2(0.0), vec2(0.0)).xy; // asm 52  sample_d r2.xy, r2.zwzz, t1.xyzw, s1, l(0), l(0) (normals: LOWER_LEFT input, flipped)
    r2xy = r2xy * 4.0 + (-2.0);                                             // asm 53  mad l(4), l(-2)    (e)
    float r1w = dot(r2xy, r2xy);                                            // asm 54  dp2                (f)
    vec2 r3zw = -vec2(r1w) * vec2(0.25, 0.5) + vec2(1.0);                   // asm 55  mad r3.zw, -r1.wwww, l(0.25, 0.5), l(1, 1)
    r1w = sqrt(r3zw.x);                                                     // asm 56  sqrt r1.w, r3.z
    vec2 r3xy = vec2(r1w) * r2xy;                                           // asm 57  mul r3.xy, r1.wwww, r2.xyxx
    vec3 r2xyz = vec3(r3xy.x, r3xy.y, r3zw.y) * vec3(-1.0, 1.0, -1.0);      // asm 58  mul r2.xyz, r3.xywx, l(-1, 1, -1)  (N)
    r1w = g_SSAOInfos.y / r1z;                                              // asm 59  div r1.w, cb2[1].y, r1.z   (rD)
    vec4 r3 = v1.xyxy * 2.0 + (-1.0);                                       // asm 60  mad r3.xyzw, v1.xyxy, l(2), l(-1)
    float r2w = dot(r3, r3);                                                // asm 61  dp4
    float b = r0x + SSE_AO_NEG_0_3;                                         // asm 62  add r3.x, r0.x, l(-0.3)
    b = d3d_max(b, 0.0);                                                    // asm 63  max
    b = b * 10.0 + g_SSAOInfos.z;                                           // asm 64  mad r3.x, r3.x, l(10), cb2[1].z
    r2w = r2w + b;                                                          // asm 65  add r2.w, r2.w, r3.x   (b')
    float acc = 0.0;                                                        // asm 66  mov r3.x, l(0)
    int   i   = 0;                                                          // asm 66  mov r3.y, l(0)
    for (;;)                                                                // asm 67  loop
    {
        if (i >= 15) break;                                                 // asm 68  ige r3.z, r3.y, l(15) ; asm 69 breakc_nz
        float a = float(i);                                                 // asm 70  itof
        a = a + 0.5;                                                        // asm 71  add l(0.5)
        float rr = r1w * a;                                                 // asm 72  mul r3.w, r1.w, r3.z
        a = a * SSE_AO_GOLDEN + r0y;                                        // asm 73  mad r3.z, r3.z, l(3.349334), r0.y
        float sn = sin(a);                                                  // asm 74  sincos r4.x (sin), r5.x (cos), r3.z
        float cs = cos(a);                                                  // asm 74
        float rad = rr * SSE_AO_INV15;                                      // asm 75  mul r3.z, r3.w, l(0.066667)   (r)
        float lg = d3d_log2(rad);                                           // asm 76  log r3.w, r3.z
        lg = floor(lg);                                                     // asm 77  round_ni
        int mip = d3d_ftoi(lg);                                             // asm 78  ftoi
        mip = mip + (-3);                                                   // asm 79  iadd l(-3)   (wraps like D3D)
        mip = max(mip, 1);                                                  // asm 80  imax l(1)
        mip = min(mip, 4);                                                  // asm 81  imin l(4)
        vec2 r5xy = vec2(cs, sn);                                           // asm 82  mov r5.y, r4.x
        vec2 off = vec2(rad) * r5xy;                                        // asm 83  mul r4.xy, r3.zzzz, r5.xyxx
        ivec2 ioff = ivec2(d3d_ftoi(off.x), d3d_ftoi(off.y));               // asm 84  ftoi r4.zw, r4.xxxy
        ioff = r0zw + ioff;                                                 // asm 85  iadd r4.zw, r0.zzzw, r4.zzzw
        vec2 uvS = off * g_ScreenInfos.zw + v1;                             // asm 86  mad r4.xy, r4.xyxx, cb2[2].zwzz, v1.xyxx
        float lod = float(mip);                                             // asm 87  itof r3.z, r3.w
        float qz = textureLod(t0, uvS, lod).x;                              // asm 88  sample_l r5.z, r4.xyxx, t0.yzxw, s0, r3.z (z <- .x)
        vec2 q = vec2(ioff);                                                // asm 89  itof r3.zw, r4.zzzw
        q = q + 0.5;                                                        // asm 90  add l(0.5)
        q = q * g_ProjInfos.xy + g_ProjInfos.zw;                            // asm 91  mad r3.zw, r3.zzzw, cb2[0].xxxy, cb2[0].zzzw
        vec2 qxy = vec2(qz) * q;                                            // asm 92  mul r5.xy, r5.zzzz, r3.zwzz
        vec3 v = -vec3(r1xy, r1z) + vec3(qxy, qz);                          // asm 93  add r4.xyz, -r1.xyzx, r5.xyzx
        float vv = dot(v, v);                                               // asm 94  dp3 r3.z
        float vn = dot(v, r2xyz);                                           // asm 95  dp3 r3.w, r4.xyzx, r2.xyzx
        float f = -vv + g_SSAOInfos2.y;                                     // asm 96  add r4.x, -r3.z, cb2[3].y
        f = d3d_max(f, 0.0);                                                // asm 97  max
        float f2 = f * f;                                                   // asm 98  mul r4.y, r4.x, r4.x
        f = f * f2;                                                         // asm 99  mul r4.x, r4.x, r4.y
        vn = -r2w + vn;                                                     // asm 100 add r3.w, -r2.w, r3.w
        vv = vv + SSE_AO_VV_EPS;                                            // asm 101 add r3.z, r3.z, l(0.01)
        float w = vn / vv;                                                  // asm 102 div
        w = d3d_max(w, 0.0);                                                // asm 103 max
        w = w * f;                                                          // asm 104 mul
        float g = (qz >= g_SSAOInfos2.w) ? 1.0 : 0.0;                       // asm 105 ge r3.w, r5.z, cb2[3].w ; asm 106 and l(0x3f800000)
        acc = w * g + acc;                                                  // asm 107 mad r3.x, r3.z, r3.w, r3.x
        i = i + 1;                                                          // asm 108 iadd l(1)
    }                                                                       // asm 109 endloop
    r0y = acc * g_SSAOInfos.w;                                              // asm 110 mul r0.y, r3.x, cb2[1].w
    r0y = -r0y * SSE_AO_INV3 + 1.0;                                         // asm 111 mad r0.y, -r0.y, l(0.333333), l(1)
    r0y = d3d_max(r0y, 0.0);                                                // asm 112 max   (A)
    float e = d3d_max(g_SSAOInfos2.x, SSE_AO_EXP_MIN);                      // asm 113 max r1.x, cb2[3].x, l(0.00001)
    e = d3d_min(e, SSE_AO_EXP_MAX);                                         // asm 114 min l(0.99999)
    bool lo = e < 0.5;                                                      // asm 115 lt r1.y, r1.x, l(0.5)
    float pLo = e + e;                                                      // asm 116 add r1.w, r1.x, r1.x
    r0y = d3d_log2(r0y);                                                    // asm 117 log r0.y
    pLo = r0y * pLo;                                                        // asm 118 mul r1.w, r0.y, r1.w
    pLo = d3d_exp2(pLo);                                                    // asm 119 exp r1.w
    float pHi = e + (-0.5);                                                 // asm 120 add r1.x, r1.x, l(-0.5)
    pHi = -pHi * 2.0 + 1.0;                                                 // asm 121 mad r1.x, -r1.x, l(2), l(1)
    pHi = 1.0 / pHi;                                                        // asm 122 div r1.x, l(1), r1.x
    r0y = r0y * pHi;                                                        // asm 123 mul r0.y, r0.y, r1.x
    r0y = d3d_exp2(r0y);                                                    // asm 124 exp r0.y
    r0y = lo ? pLo : r0y;                                                   // asm 125 movc r0.y, r1.y, r1.w, r0.y
    float dzx = dFdxCoarse(r1z);                                            // asm 126 deriv_rtx_coarse r1.x, r1.z
    bool gx = abs(dzx) < SSE_AO_DERIV_GATE;                                 // asm 127 lt r1.x, |r1.x|, l(0.02)
    float dax = dFdxCoarse(r0y);                                            // asm 128 deriv_rtx_coarse r1.y, r0.y
    ivec2 par = r0zw & ivec2(1);                                            // asm 129 and r0.zw, r0.zzzw, l(0,0,1,1)
    vec2 pf = vec2(par);                                                    // asm 130 itof
    pf = pf + (-0.5);                                                       // asm 131 add l(-0.5)
    float sx = -dax * pf.x + r0y;                                           // asm 132 mad r0.z, -r1.y, r0.z, r0.y
    r0y = gx ? sx : r0y;                                                    // asm 133 movc r0.y, r1.x, r0.z, r0.y
    float dzy = dFdyCoarse(r1z);                                            // asm 134 deriv_rty_coarse r0.z, r1.z  (UPPER_LEFT: no sign change)
    bool gy = abs(dzy) < SSE_AO_DERIV_GATE;                                 // asm 135 lt r0.z, |r0.z|, l(0.02)
    float day = dFdyCoarse(r0y);                                            // asm 136 deriv_rty_coarse r1.x, r0.y  (on the x-smoothed value)
    float sy = -day * pf.y + r0y;                                           // asm 137 mad r0.w, -r1.x, r0.w, r0.y
    r0y = gy ? sy : r0y;                                                    // asm 138 movc r0.y, r0.z, r0.w, r0.y
    bool k1 = r0x == 1.0;                                                   // asm 139 eq r0.x, r0.x, l(1)
    o0 = vec4(k1 ? 1.0 : r0y, o0y, o0z, 1.0);                               // asm 140 movc o0.x ; asm 45 o0.y ; asm 46 o0.z ; asm 141 o0.w
}                                                                           // asm 142 ret
"

    Friend Const Body_BlurH As String = "// PS 15991 -- ISSAOBlurH. RE SAO 1.6.4 / 4.1. Output R8G8B8A8_UNORM (RT 0x42).
layout(location = 0) out vec4 o0;           // asm 18, 27  SV_Target0.xyzw

void main()
{
    SseBlurResult r = SseSaoBlur(v1, vec2(1.0, 0.0));           // asm 29..104 (axis x)
    if (r.earlyOut)                                             // asm 55  if_nz r1.z
    {
        o0 = vec4(0.0, r.centreYZ, 0.0);                        // asm 56  mov o0.xw, l(0) ; asm 57 mov o0.yz, r0.yyzy
        return;                                                 // asm 58  ret
    }
    o0 = vec4(r.ratio, r.centreYZ, 0.0);                        // asm 104 div o0.x ; asm 105 mov o0.yz, r0.yyzy ; asm 106 mov o0.w, l(0)
}                                                               // asm 107 ret
"

    Friend Const Body_BlurV As String = "// PS 15993 -- ISSAOBlurV. RE SAO 1.6.4 / 4.1. Output R8G8B8A8_UNORM (RT 0x2E kSAO).
layout(location = 0) out vec4 o0;           // asm 18, 27  SV_Target0.xyzw

void main()
{
    SseBlurResult r = SseSaoBlur(v1, vec2(0.0, 1.0));           // asm 29..104 (axis y)
    if (r.earlyOut)                                             // asm 55  if_nz r1.z
    {
        o0 = vec4(0.0, r.centreYZ, 0.0);                        // asm 56  mov o0.xw, l(0) ; asm 57 mov o0.yz, r0.yyzy
        return;                                                 // asm 58  ret
    }
    o0 = vec4(r.ratio);                                         // asm 104 div o0.xyzw, r0.xxxx, r0.yyyy
}                                                               // asm 105 ret
"

    Friend Const Fragment_CameraZ As String = Header430 & Prelude & D3d11SemanticsSource.Glsl & Body_CameraZ
    Friend Const Fragment_Minify As String = Header430 & Prelude & D3d11SemanticsSource.Glsl & MinifyCommon & Body_Minify
    Friend Const Fragment_MinifyContrast As String = Header430 & Prelude & D3d11SemanticsSource.Glsl & MinifyCommon & Body_MinifyContrast
    Friend Const Fragment_RawAO As String = Header430Deriv & Prelude & D3d11SemanticsSource.Glsl & Body_RawAO
    Friend Const Fragment_BlurH As String = Header430 & Prelude & D3d11SemanticsSource.Glsl & BlurCommon & Body_BlurH
    Friend Const Fragment_BlurV As String = Header430 & Prelude & D3d11SemanticsSource.Glsl & BlurCommon & Body_BlurV
End Module

''' <summary>WHAT THE SSE OPAQUE PASS WRITES TO THE SAO NORMALS TARGET (o2), transcribed (RE_SAO_BOTH 7.1, 12.1, 12.5-12.7): the
''' bit-26 effect VS laws and the two PS encodes. The normal is the app's (user decision 3-oct-2026); the transformation,
''' normalisation and encode are the engine's. Vertex_SSE includes EngineFrame + Vs, Fragment_SSE includes Ps (both after
''' D3d11SemanticsSource.Glsl). ASCII only (GLSL).</summary>
Friend Module SseAoNormalSource
    ''' <summary>Engine constant rows (World cb2[0..2], view cb12[0..3]) from the app's GL matrices (AONRM_ENGINE_FRAME).</summary>
    Friend Const EngineFrame_Glsl As String = "// Engine constant rows used by the normal paths, built from the app's GL matrices.
//   cb2[0..2]  : World rows (VS 420 l.14..47 compose clip = sum_k cb12[8+i].k * cb2[k] => cb2[k] = row k of World).
//   cb12[0..2] : view rotation rows (RE_REFRACTION_BOTH 1.3: rows of 0x1420CFFB0, output 1 of 0x14101D400, copied
//                transposed by 0x14100AE80 at 14100AEA4..14100AF69 into cb12[0..3] = rsp+0x40..0x70).
//   cb12[3]    : (0,0,0,1): 0x14101D400 writes out1[+0x0C], [+0x1C] = 0 (14101D444, 14101D45B), [+0x2C..+0x3B] = 0
//                (14101D47F, 14101D483), [+0x3C] = 1.0 (14101D487); the transposed column 3 is stored at rsp+0x70 =
//                cb12[3] (14100AF69). Only .xyz of cb12[0..3] enter the normal paths, so only the zero .xyz matters.
// The app's view is GL (-z forward): engine view row k = row k of S * mat3(matView), S = diag(1, 1, -1).
struct SseEngineFrame
{
    vec4 cb2_0;   // World row 0
    vec4 cb2_1;   // World row 1
    vec4 cb2_2;   // World row 2
    vec4 cb12_0;  // engine view row 0
    vec4 cb12_1;  // engine view row 1
    vec4 cb12_2;  // engine view row 2
    vec4 cb12_3;  // (0, 0, 0, 1)
};

vec4 sse_row(mat4 m, int k) { return vec4(m[0][k], m[1][k], m[2][k], m[3][k]); }

SseEngineFrame SseEngineFrameFromGL(mat4 matModel, mat4 matView)
{
    SseEngineFrame f;
    f.cb2_0  = sse_row(matModel, 0);
    f.cb2_1  = sse_row(matModel, 1);
    f.cb2_2  = sse_row(matModel, 2);
    f.cb12_0 = vec4(sse_row(matView, 0).xyz, 0.0);
    f.cb12_1 = vec4(sse_row(matView, 1).xyz, 0.0);
    f.cb12_2 = vec4(-sse_row(matView, 2).xyz, 0.0);   // S = diag(1,1,-1): GL -z forward -> engine +z forward
    f.cb12_3 = vec4(0.0, 0.0, 0.0, 1.0);
    return f;
}
"

    ''' <summary>The bit-26 effect VS laws 420 / 425 / 431 on the app's normal (AONRM_VS).</summary>
    Friend Const Vs_Glsl As String = "// Input n = the value of the VS's first line `mad r, NORMAL.xyz, l(2,2,2), l(-1,-1,-1)`: the IA decode 2*NORMAL-1, replaced by the app's vertex normal (user decision 3-oct-2026).

// Row composite used by VS 420 l.49..51 / l.54..65 and VS 3901 l.76..95:
//   r.xyz = cb12[k].y * cb2[1].xyz ; r.xyz = cb12[k].x * cb2[0].xyz + r ; r.xyz = cb12[k].z * cb2[2].xyz + r
vec3 sse_viewWorldRow(vec4 cb12k, SseEngineFrame f)
{
    vec3 r = cb12k.y * f.cb2_1.xyz;            // mul r.xyz, cb12[k].yyyy, cb2[1].xyzx
    r = cb12k.x * f.cb2_0.xyz + r;             // mad r.xyz, cb12[k].xxxx, cb2[0].xyzx, r
    r = cb12k.z * f.cb2_2.xyz + r;             // mad r.xyz, cb12[k].zzzz, cb2[2].xyzx, r
    return r;
}

// VS 420 (bit 26, no SKINNED/FALLOFF): o2.yzw = TEXCOORD7.yzw, view space, normalised over FOUR components.
// The 4th component is NOT NORMAL.w (the VS declares dcl_input v1.xyz, l.6) and not the bitangent byte: it is
// dot((View row 3 o World).xyz, n) with View row 3 = cb12[3] = (0,0,0,1) -> exactly 0 for finite World rows.
vec3 SseAoN_VS420(vec3 n, SseEngineFrame f)
{
    vec4 r0;
    r0.xyz = sse_viewWorldRow(f.cb12_3, f);                        // l.49..51  (cb12[3] row composite)
    vec3 r2 = n;                                                   // l.52  mad r2.xyz, v1.xyzx, l(2,2,2), l(-1,-1,-1) (the IA decode 2*NORMAL-1, replaced by the app's vertex normal (user decision 3-oct-2026))
    r0.w = dot(r0.xyz, r2);                                        // l.53  dp3 r0.w, r0.xyzx, r2.xyzx
    vec3 r3 = sse_viewWorldRow(f.cb12_0, f);                       // l.54..56
    r0.x = dot(r3, r2);                                            // l.57  dp3 r0.x
    r3 = sse_viewWorldRow(f.cb12_1, f);                            // l.58..60
    r0.y = dot(r3, r2);                                            // l.61  dp3 r0.y
    r3 = sse_viewWorldRow(f.cb12_2, f);                            // l.62..64
    r0.z = dot(r3, r2);                                            // l.65  dp3 r0.z
    float l = dot(r0, r0);                                         // l.66  dp4 r0.w, r0.xyzw, r0.xyzw
    l = d3d_rsq(l);                                             // l.67  rsq
    return vec3(l) * r0.xyz;                                       // l.68  mul o2.yzw, r0.wwww, r0.xxyz
}

// VS 425 (bit 26, SKINNED, no MEMBRANE): raw object (bind-pose) normal, no bones, no world, no view, not normalised.
vec3 SseAoN_VS425(vec3 n)
{
    return n;                                                      // l.74  mad o2.yzw, v2.xxyz, l(0,2,2,2), l(0,-1,-1,-1) (the IA decode 2*NORMAL-1, replaced by the app's vertex normal (user decision 3-oct-2026))
}

// VS 431 / 441 (bit 26, FALLOFF; 440 same law into o2.yzw): world-space normal, normalised.
vec3 SseAoN_VS431(vec3 n, SseEngineFrame f)
{
    vec3 r0 = n;                                                   // l.52  mad r0.xyz, v2.xyzx, l(2,2,2), l(-1,-1,-1) (the IA decode 2*NORMAL-1, replaced by the app's vertex normal (user decision 3-oct-2026))
    vec3 r2;
    r2.x = dot(f.cb2_0.xyz, r0);                                   // l.53  dp3 r2.y, cb2[0].xyzx, r0.xyzx
    r2.y = dot(f.cb2_1.xyz, r0);                                   // l.54  dp3 r2.z, cb2[1].xyzx
    r2.z = dot(f.cb2_2.xyz, r0);                                   // l.55  dp3 r2.w, cb2[2].xyzx
    float l = dot(r2, r2);                                         // l.56  dp3 r0.x, r2.yzwy, r2.yzwy
    l = d3d_rsq(l);                                             // l.57  rsq
    return vec3(l) * r2;                                           // l.58  mul r0.xyz, r0.xxxx, r2.yzwy ; l.67 mov o3.yzw
}
// VS 3901 (the lighting rows) is not used: the lighting class writes the normal the app lights with (user decision 3-oct).
"

    ''' <summary>The o2 encode of PS 2357 (bit-26 effect) and PS 4255 (lighting) (AONRM_PS).</summary>
    Friend Const Ps_Glsl As String = "#define SSE_O2_EIGHT      8.0
#define SSE_O2_SQRT_FLOOR uintBitsToFloat(0x3A83126Fu)   // l(0.001000) token 0x3A83126F
#define SSE_O2_SPEC_BIAS  uintBitsToFloat(0xB727C5ACu)   // l(-0.000010) token 0xB727C5AC (PS 4255 l.98)

// PS 2357 (bit-26 effect): v2yzw = TEXCOORD7.yzw interpolated (dcl_input_ps linear v2.yzw, l.7).
vec4 SseAoN_PS2357(vec3 v2yzw)
{
    float l = dot(v2yzw, v2yzw);                                   // l.33  dp3 r0.x, v2.yzwy, v2.yzwy
    l = d3d_rsq(l);                                                // l.34  rsq r0.x, r0.x
    vec3 n = vec3(l) * v2yzw;                                      // l.35  mul r0.xyz, r0.xxxx, v2.yzwy
    float d = n.z * (-SSE_O2_EIGHT) + SSE_O2_EIGHT;                // l.36  mad r0.z, r0.z, l(-8), l(8)
    d = d3d_sqrt(d);                                               // l.37  sqrt r0.z, r0.z
    d = d3d_max(d, SSE_O2_SQRT_FLOOR);                             // l.38  max r0.z, r0.z, l(0.001)
    vec2 e = n.xy / vec2(d);                                       // l.39  div r0.xy, r0.xyxx, r0.zzzz
    return vec4(e + 0.5, 0.0, 0.0);                                // l.40  add o2.xy, l(0.5) ; l.41 mov o2.zw, l(0,0,0,0)
}

// PS 4255 (lighting) o2 from the normal the app lights with (user decision 3-oct-2026: the app's recalculated normals stay):
//   nE    = that normal in engine view space (x, y, -z_gl); it stands for the result of lines 92..94 (the TBN rows)
//   m     = the SSR-mask source of this PS class (RE 12.1: t1.w)
//   cb2_7 = SSRParams (RE 12.1/12.5): (fSpecMaskBegin, fSpecMaskBegin + fSpecMaskSpan, selector, specularLODFade); the
//           world pass has selector cb2[7].z = 0 (RE 12.5), so the l.114 movc keeps r1 (the colour select is not written).
vec4 SseAoN_PS4255_N(vec3 nE, float m, vec4 cb2_7)
{
    vec3 n = nE;
    float l = dot(n, n);                                           // l.95  dp3 r0.w
    l = d3d_rsq(l);                                                // l.96  rsq
    n = vec3(l) * n;                                               // l.97  mul r0.xyz, r0.wwww, r0.xyzx
    float a = cb2_7.x + SSE_O2_SPEC_BIAS;                          // l.98  add r0.w, cb2[7].x, l(-0.00001)
    float span = -a + cb2_7.y;                                     // l.99  add r1.x, -r0.w, cb2[7].y
    float t = -a + m;                                              // l.100 add r0.w, -r0.w, r1.w
    float inv = 1.0 / span;                                        // l.101 div r1.x, l(1), r1.x
    t = d3d_sat(t * inv);                                          // l.102 mul_sat r0.w, r0.w, r1.x
    float k = t * -2.0 + 3.0;                                      // l.103 mad r1.x, r0.w, l(-2), l(3)
    t = t * t;                                                     // l.104 mul r0.w, r0.w, r0.w
    t = t * k;                                                     // l.105 mul r0.w, r0.w, r1.x
    float w = t * cb2_7.w;                                         // l.106 mul r1.w, r0.w, cb2[7].w
    float d = n.z * (-SSE_O2_EIGHT) + SSE_O2_EIGHT;                // l.107 mad r0.z, r0.z, l(-8), l(8)
    d = d3d_sqrt(d);                                               // l.108 sqrt
    d = d3d_max(d, SSE_O2_SQRT_FLOOR);                             // l.109 max l(0.001)
    vec2 e = n.xy / vec2(d);                                       // l.110 div r0.xy, r0.xyxx, r0.zzzz
    return vec4(e + 0.5, 0.0, w);                                  // l.111 add r1.xy, l(0.5) ; l.113 mov r1.z, l(0) ; l.114 movc -> r1
}
"

End Module
